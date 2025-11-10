using System.Security.Cryptography;
using System.Text;
using System.Net;

namespace HoodLab.Api.Services;

public class VNPayService
{
    private readonly string _tmnCode;
    private readonly string _hashSecret;
    private readonly string _paymentUrl;
    private readonly string _returnUrl;
    private readonly string _ipnUrl;

    public VNPayService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
    {
        _tmnCode = configuration["VNPay:TmnCode"] ?? "2UHLTHQR";
        _hashSecret = configuration["VNPay:HashSecret"] ?? "CPA1N7P1XFMG4ATVOUEFO05U5O4N1AWP";
        _paymentUrl = configuration["VNPay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        
        var request = httpContextAccessor.HttpContext?.Request;
        var scheme = request?.Scheme ?? "http";
        var host = request?.Host.Value ?? "localhost:5000";
        
        _returnUrl = configuration["VNPay:ReturnUrl"] ?? $"{scheme}://{host}/api/payment/vnpay/return";
        _ipnUrl = configuration["VNPay:IpnUrl"] ?? $"{scheme}://{host}/api/payment/vnpay/ipn";
    }

    public (string PaymentUrl, string SignData, string SecureHash) CreatePaymentUrlWithDebug(string orderId, decimal amount, string orderInfo, string ipAddress, string? bankCode = null)
    {
        // VNPAY yêu cầu timezone GMT+7 (Vietnam)
        // Sử dụng DateTime.Now để lấy thời gian local (GMT+7)
        var createDate = DateTime.Now;
        
        var vnp_Params = new SortedDictionary<string, string>
        {
            { "vnp_Version", "2.1.0" },
            { "vnp_Command", "pay" },
            { "vnp_TmnCode", _tmnCode },
            { "vnp_Amount", ((long)(amount * 100)).ToString() }, // Nhân 100 để chuyển sang đơn vị nhỏ nhất
            { "vnp_CurrCode", "VND" },
            { "vnp_TxnRef", orderId },
            { "vnp_OrderInfo", orderInfo },
            { "vnp_OrderType", "other" },
            { "vnp_Locale", "vn" },
            { "vnp_ReturnUrl", _returnUrl },
            { "vnp_IpAddr", ipAddress },
            { "vnp_CreateDate", createDate.ToString("yyyyMMddHHmmss") }
        };

        if (!string.IsNullOrEmpty(bankCode))
        {
            vnp_Params.Add("vnp_BankCode", bankCode);
        }

        // Loại bỏ các tham số rỗng trước khi tạo chữ ký (giữ nguyên thứ tự sắp xếp)
        var filteredParams = new SortedDictionary<string, string>(
            vnp_Params.Where(kvp => !string.IsNullOrEmpty(kvp.Value))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        );

        // Tạo query string với encode (theo code mẫu chính thức của VNPAY)
        // Code mẫu VNPAY sử dụng WebUtility.UrlEncode để encode cả key và value
        var queryStringBuilder = new StringBuilder();
        foreach (var kvp in filteredParams)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                queryStringBuilder.Append(WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value) + "&");
            }
        }
        var queryString = queryStringBuilder.ToString();
        
        // Tạo signData từ queryString (đã encode) và loại bỏ ký tự & cuối cùng
        var signData = queryString;
        if (signData.Length > 0)
        {
            signData = signData.Remove(signData.Length - 1, 1);
        }
        
        // Debug: Log signData và các tham số để kiểm tra
        System.Diagnostics.Debug.WriteLine($"=== VNPAY Debug ===");
        System.Diagnostics.Debug.WriteLine($"TmnCode: {_tmnCode}");
        System.Diagnostics.Debug.WriteLine($"HashSecret: {_hashSecret}");
        System.Diagnostics.Debug.WriteLine($"SignData: {signData}");
        
        // Sử dụng HMAC-SHA512 với secretKey làm key (hash signData đã encode)
        var vnp_SecureHash = HmacSHA512(_hashSecret, signData);
        
        // Debug: Log hash để kiểm tra
        System.Diagnostics.Debug.WriteLine($"SecureHash: {vnp_SecureHash}");
        System.Diagnostics.Debug.WriteLine($"==================");

        // Tạo URL thanh toán (theo code mẫu chính thức của VNPAY)
        var paymentUrl = $"{_paymentUrl}?{queryString}vnp_SecureHash={vnp_SecureHash}";

        return (paymentUrl, signData, vnp_SecureHash);
    }

    public string CreatePaymentUrl(string orderId, decimal amount, string orderInfo, string ipAddress, string? bankCode = null)
    {
        var result = CreatePaymentUrlWithDebug(orderId, amount, orderInfo, ipAddress, bankCode);
        return result.PaymentUrl;
    }

    public bool ValidateSignature(Dictionary<string, string> vnp_Params, string vnp_SecureHash)
    {
        var filteredParams = vnp_Params
            .Where(kvp => kvp.Key != "vnp_SecureHash" && kvp.Key != "vnp_SecureHashType")
            .OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var signDataBuilder = new StringBuilder();
        foreach (var kvp in filteredParams)
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                signDataBuilder.Append(WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value) + "&");
            }
        }
        var signData = signDataBuilder.ToString();
        
        // Loại bỏ ký tự & cuối cùng
        if (signData.Length > 0)
        {
            signData = signData.Remove(signData.Length - 1, 1);
        }
        
        // Hash signData đã encode với secretKey
        var signed = HmacSHA512(_hashSecret, signData);

        return signed.Equals(vnp_SecureHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);

        using (var hmac = new HMACSHA512(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(inputBytes);
            foreach (var hashByte in hashBytes)
            {
                hash.Append(hashByte.ToString("x2"));
            }
        }

        return hash.ToString();
    }
}

