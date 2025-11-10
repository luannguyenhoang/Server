using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using HoodLab.Api.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly VNPayService _vnPayService;

    public PaymentController(ApplicationDbContext context, IConfiguration configuration, VNPayService vnPayService)
    {
        _context = context;
        _configuration = configuration;
        _vnPayService = vnPayService;
    }

    [HttpPost("momo")]
    public async Task<ActionResult<PaymentResponse>> CreateMoMoPayment([FromBody] PaymentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var order = await _context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == userId);

        if (order == null)
        {
            return NotFound(new { message = "Đơn hàng không tồn tại" });
        }

        if (order.PaymentStatus == "Paid")
        {
            return BadRequest(new { message = "Đơn hàng đã được thanh toán" });
        }

        var partnerCode = _configuration["MoMo:PartnerCode"] ?? "MOMO";
        var accessKey = _configuration["MoMo:AccessKey"] ?? "";
        var secretKey = _configuration["MoMo:SecretKey"] ?? "";
        var apiEndpoint = _configuration["MoMo:ApiEndpoint"] ?? "https://test-payment.momo.vn/v2/gateway/api/create";

        var requestId = Guid.NewGuid().ToString();
        var orderId = order.OrderNumber;
        var amount = (long)order.TotalAmount;
        var orderInfo = $"Thanh toán đơn hàng {order.OrderNumber}";
        var redirectUrl = "http://localhost:3000/payment/success";
        var ipnUrl = $"{Request.Scheme}://{Request.Host}/api/payment/momo/callback";
        var requestType = "captureWallet";
        var extraData = "";

        var rawHash = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";

        var signature = ComputeHmacSha256(rawHash, secretKey);

        var paymentRequest = new
        {
            partnerCode = partnerCode,
            partnerName = "HoodLab",
            storeId = "HoodLab",
            requestId = requestId,
            amount = amount,
            orderId = orderId,
            orderInfo = orderInfo,
            redirectUrl = redirectUrl,
            ipnUrl = ipnUrl,
            lang = "vi",
            requestType = requestType,
            autoCapture = true,
            extraData = extraData,
            signature = signature
        };

        using var client = new HttpClient();
        var json = JsonSerializer.Serialize(paymentRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(apiEndpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var paymentResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                
                if (paymentResponse.TryGetProperty("payUrl", out var payUrl))
                {
                    return Ok(new PaymentResponse
                    {
                        PaymentUrl = payUrl.GetString() ?? "",
                        OrderNumber = order.OrderNumber
                    });
                }
            }

            return BadRequest(new { message = "Không thể tạo thanh toán MoMo" });
        }
        catch
        {
            return BadRequest(new { message = "Lỗi kết nối đến MoMo" });
        }
    }

    [HttpPost("momo/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> MoMoCallback([FromBody] JsonElement request)
    {
        try
        {
            if (request.TryGetProperty("orderId", out var orderIdElement))
            {
                var orderNumber = orderIdElement.GetString();
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

                if (order != null && request.TryGetProperty("resultCode", out var resultCode))
                {
                    var result = resultCode.GetInt32();
                    if (result == 0)
                    {
                        order.PaymentStatus = "Paid";
                        order.OrderStatus = "Pending"; // Chờ xử lý sau khi thanh toán thành công
                        order.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return Ok();
        }
        catch
        {
            return BadRequest();
        }
    }

    [HttpPost("ship")]
    public async Task<ActionResult<PaymentResponse>> CreateShipPayment([FromBody] PaymentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == userId);

        if (order == null)
        {
            return NotFound(new { message = "Đơn hàng không tồn tại" });
        }

        order.PaymentMethod = "Ship";
        order.PaymentStatus = "Pending";
        order.OrderStatus = "Pending";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new PaymentResponse
        {
            PaymentUrl = "",
            OrderNumber = order.OrderNumber
        });
    }

    [HttpGet("vnpay/test")]
    [AllowAnonymous]
    public IActionResult TestVNPaySignature()
    {
        // Test với dữ liệu mẫu giống như trong tài liệu VNPAY
        var testOrderId = "5";
        var testAmount = 18060m; // 180.600 VND
        // Loại bỏ dấu : để tránh vấn đề với encoding
        var testOrderInfo = "Thanh toan don hang 5";
        var testIpAddress = "127.0.0.1";
        
        var (paymentUrl, signData, secureHash) = _vnPayService.CreatePaymentUrlWithDebug(
            testOrderId,
            testAmount,
            testOrderInfo,
            testIpAddress,
            null
        );
        
        return Ok(new { 
            paymentUrl,
            signData,
            secureHash,
            secretKey = _configuration["VNPay:HashSecret"],
            tmnCode = _configuration["VNPay:TmnCode"],
            message = "Kiểm tra URL này trong browser. So sánh với URL mẫu trong tài liệu VNPAY. Kiểm tra signData và secureHash để debug."
        });
    }

    [HttpPost("vnpay")]
    public async Task<ActionResult<PaymentResponse>> CreateVNPayPayment([FromBody] PaymentRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var order = await _context.Orders
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == userId);

        if (order == null)
        {
            return NotFound(new { message = "Đơn hàng không tồn tại" });
        }

        if (order.PaymentStatus == "Paid")
        {
            return BadRequest(new { message = "Đơn hàng đã được thanh toán" });
        }

        // Lấy IP address (xử lý IPv6 nếu có)
        // VNPAY yêu cầu IP phải là định dạng IPv4
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        // Nếu là IPv6 hoặc không phải IPv4, chuyển sang IPv4 hoặc dùng localhost
        if (ipAddress.Contains("::") || 
            !System.Net.IPAddress.TryParse(ipAddress, out var parsedIp) || 
            parsedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            ipAddress = "127.0.0.1";
        }
        
        // OrderInfo không được chứa ký tự đặc biệt, chỉ dùng ASCII
        var orderInfo = $"Thanh toan don hang {order.OrderNumber}";
        var bankCode = order.PaymentMethod == "VNPAYQR" ? "VNPAYQR" : null;

        var paymentUrl = _vnPayService.CreatePaymentUrl(
            order.OrderNumber,
            order.TotalAmount,
            orderInfo,
            ipAddress,
            bankCode
        );

        return Ok(new PaymentResponse
        {
            PaymentUrl = paymentUrl,
            OrderNumber = order.OrderNumber
        });
    }

    [HttpGet("vnpay/return")]
    [AllowAnonymous]
    public async Task<IActionResult> VNPayReturn()
    {
        var vnp_Params = Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
        
        if (!vnp_Params.ContainsKey("vnp_SecureHash"))
        {
            return BadRequest(new { message = "Thiếu tham số vnp_SecureHash" });
        }

        var vnp_SecureHash = vnp_Params["vnp_SecureHash"];
        var isValid = _vnPayService.ValidateSignature(vnp_Params, vnp_SecureHash);

        if (!isValid)
        {
            return BadRequest(new { message = "Chữ ký không hợp lệ" });
        }

        var orderNumber = vnp_Params.GetValueOrDefault("vnp_TxnRef", "");
        var responseCode = vnp_Params.GetValueOrDefault("vnp_ResponseCode", "");
        var transactionStatus = vnp_Params.GetValueOrDefault("vnp_TransactionStatus", "");
        var transactionNo = vnp_Params.GetValueOrDefault("vnp_TransactionNo", "");

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order != null)
        {
            // Chỉ cập nhật nếu order chưa được thanh toán (tránh ghi đè trạng thái đã được IPN cập nhật)
            if (order.PaymentStatus != "Paid")
            {
                // Xử lý các trạng thái thanh toán
                // Theo code mẫu VNPAY: vnp_ResponseCode == "00" && vnp_TransactionStatus == "00" => Thanh toán thành công
                if (responseCode == "00" && transactionStatus == "00")
                {
                    // Thanh toán thành công
                    order.PaymentStatus = "Paid";
                    order.OrderStatus = "Pending"; // Chờ xử lý sau khi thanh toán thành công
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                else if (responseCode == "24")
                {
                    // User hủy giao dịch
                    order.PaymentStatus = "Cancelled";
                    order.OrderStatus = "Cancelled";
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                else if (!string.IsNullOrEmpty(responseCode) && responseCode != "00")
                {
                    // Các lỗi khác (thất bại thanh toán)
                    order.PaymentStatus = "Failed";
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
        }

        // Redirect về frontend với kết quả
        var frontendUrl = $"http://localhost:3000/payment/return?orderNumber={orderNumber}&responseCode={responseCode}&transactionNo={transactionNo}";
        return Redirect(frontendUrl);
    }

    [HttpPost("vnpay/ipn")]
    [HttpGet("vnpay/ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VNPayIpn()
    {
        // VNPAY gọi IPN bằng GET với QueryString (theo code mẫu chính thức)
        var vnp_Params = new Dictionary<string, string>();
        
        if (Request.Method == "GET" && Request.Query.Count > 0)
        {
            // Lấy từ QueryString (theo code mẫu chính thức của VNPAY)
            foreach (var key in Request.Query.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnp_Params[key] = Request.Query[key].ToString();
                }
            }
        }
        else if (Request.Method == "POST" && Request.Form.Count > 0)
        {
            // Lấy từ Form (nếu VNPAY gửi POST)
            foreach (var key in Request.Form.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnp_Params[key] = Request.Form[key].ToString();
                }
            }
        }
        
        if (vnp_Params.Count == 0)
        {
            return BadRequest(new { RspCode = "99", Message = "Input data required" });
        }
        
        if (!vnp_Params.ContainsKey("vnp_SecureHash"))
        {
            return BadRequest(new { RspCode = "99", Message = "Thiếu tham số vnp_SecureHash" });
        }

        var vnp_SecureHash = vnp_Params["vnp_SecureHash"];
        var isValid = _vnPayService.ValidateSignature(vnp_Params, vnp_SecureHash);

        if (!isValid)
        {
            return BadRequest(new { RspCode = "97", Message = "Invalid signature" });
        }

        var orderNumber = vnp_Params.GetValueOrDefault("vnp_TxnRef", "");
        var responseCode = vnp_Params.GetValueOrDefault("vnp_ResponseCode", "");
        var transactionStatus = vnp_Params.GetValueOrDefault("vnp_TransactionStatus", "");
        var transactionNo = vnp_Params.GetValueOrDefault("vnp_TransactionNo", "");
        var amountStr = vnp_Params.GetValueOrDefault("vnp_Amount", "");

        if (string.IsNullOrEmpty(orderNumber))
        {
            return BadRequest(new { RspCode = "01", Message = "Order not found" });
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        if (order == null)
        {
            return BadRequest(new { RspCode = "01", Message = "Order not found" });
        }

        // Kiểm tra số tiền (chuyển từ đơn vị nhỏ nhất về VND)
        if (!string.IsNullOrEmpty(amountStr) && long.TryParse(amountStr, out var vnp_Amount))
        {
            var amount = vnp_Amount / 100m; // Chuyển từ đơn vị nhỏ nhất về VND
            if (order.TotalAmount != amount)
            {
                return BadRequest(new { RspCode = "04", Message = "Invalid amount" });
            }
        }

        // Kiểm tra trạng thái hiện tại của order (tránh xử lý trùng lặp)
        if (order.PaymentStatus == "Paid")
        {
            return Ok(new { RspCode = "02", Message = "Order already confirmed" });
        }

        // Xử lý các trạng thái thanh toán
        // Theo code mẫu VNPAY: vnp_ResponseCode == "00" && vnp_TransactionStatus == "00" => Thanh toán thành công
        if (responseCode == "00" && transactionStatus == "00")
        {
            // Thanh toán thành công
            order.PaymentStatus = "Paid";
            order.OrderStatus = "Pending"; // Chờ xử lý sau khi thanh toán thành công
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            return Ok(new { RspCode = "00", Message = "Confirm Success" });
        }
        else
        {
            // Thanh toán không thành công (hủy, thất bại, v.v.)
            // Cập nhật trạng thái dựa trên response code
            if (responseCode == "24") // User hủy giao dịch
            {
                order.PaymentStatus = "Cancelled";
                order.OrderStatus = "Cancelled";
            }
            else
            {
                // Các lỗi khác (thất bại thanh toán)
                order.PaymentStatus = "Failed";
                // Giữ nguyên OrderStatus hoặc có thể set về "Pending" tùy logic nghiệp vụ
            }
            
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            return Ok(new { RspCode = "00", Message = "Confirm Success" });
        }
    }

    private string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
}


