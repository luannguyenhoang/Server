using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using System.Security.Claims;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrdersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _context.Users.FindAsync(userId);

        var orderNumber = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .Include(v => v.Color)
                .Include(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
                .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId);

            if (variant == null)
            {
                return BadRequest(new { message = $"Sản phẩm không tồn tại" });
            }

            var variantSize = variant.VariantSizes?.FirstOrDefault(vs => vs.SizeId == item.SizeId);
            if (variantSize == null)
            {
                return BadRequest(new { message = $"Size không tồn tại cho variant này" });
            }

            if (variantSize.Stock < item.Quantity)
            {
                return BadRequest(new { message = $"Sản phẩm {variant.Product?.Name ?? ""} không đủ tồn kho" });
            }

            if (variant.Product == null)
            {
                return BadRequest(new { message = $"Không tìm thấy thông tin sản phẩm" });
            }

            // Lấy giá: ưu tiên SalePrice nếu có và > 0, nếu không dùng Price
            decimal price;
            if (variant.Product.SalePrice.HasValue && variant.Product.SalePrice.Value > 0)
            {
                price = variant.Product.SalePrice.Value;
            }
            else
            {
                price = variant.Product.Price;
            }
            
            if (price <= 0)
            {
                return BadRequest(new { message = $"Sản phẩm {variant.Product.Name} có giá không hợp lệ (giá: {price})" });
            }

            var subTotal = price * item.Quantity;
            totalAmount += subTotal;

            var size = variantSize.Size;
            orderItems.Add(new OrderItem
            {
                ProductId = variant.ProductId,
                ProductVariantId = variant.Id,
                SizeId = item.SizeId,
                ProductName = variant.Product.Name,
                ColorName = variant.Color?.Name ?? "",
                SizeName = size?.Name ?? "",
                Price = price,
                Quantity = item.Quantity,
                SubTotal = subTotal
            });

            variantSize.Stock -= item.Quantity;
        }

        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = userId,
            TotalAmount = totalAmount,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = "Pending",
            OrderStatus = "Pending",
            ShippingAddress = request.ShippingAddress ?? user?.Address,
            Phone = request.Phone ?? user?.Phone,
            Notes = request.Notes
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var item in orderItems)
        {
            item.OrderId = order.Id;
            _context.OrderItems.Add(item);
        }

        // Lấy danh sách ProductVariantId và SizeId từ request để filter cart items
        var productVariantIds = request.Items.Select(i => i.ProductVariantId).ToList();
        var sizeIds = request.Items.Select(i => i.SizeId).ToList();
        
        // Tạo một dictionary để check nhanh (ProductVariantId, SizeId) có trong request.Items không
        var itemKeys = request.Items.Select(i => new { i.ProductVariantId, i.SizeId }).ToList();
        
        var cartItems = await _context.Carts
            .Where(c => c.UserId == userId && 
                       productVariantIds.Contains(c.ProductVariantId) && 
                       sizeIds.Contains(c.SizeId))
            .ToListAsync();
        
        // Filter lại để chỉ lấy các cart items khớp chính xác với (ProductVariantId, SizeId) trong request
        cartItems = cartItems
            .Where(c => itemKeys.Any(k => k.ProductVariantId == c.ProductVariantId && k.SizeId == c.SizeId))
            .ToList();

        _context.Carts.RemoveRange(cartItems);

        await _context.SaveChangesAsync();

        var orderDto = await GetOrderDto(order.Id);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, orderDto);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<OrderDto>>> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Limit max page size

        var query = _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(o => o.UserId == userId);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orderDtos = orders.Select(o => new OrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            UserId = o.UserId,
            UserName = o.User?.FullName ?? "",
            TotalAmount = o.TotalAmount,
            PaymentMethod = o.PaymentMethod,
            PaymentStatus = o.PaymentStatus,
            OrderStatus = o.OrderStatus,
            ShippingAddress = o.ShippingAddress,
            Phone = o.Phone,
            Notes = o.Notes,
            CreatedAt = o.CreatedAt,
            Items = o.Items?.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ColorName = i.ColorName,
                SizeName = i.SizeName,
                Price = i.Price,
                Quantity = i.Quantity,
                SubTotal = i.SubTotal
            }).ToList()
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new PaginatedResponse<OrderDto>
        {
            Data = orderDtos,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");

        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (!isAdmin && order.UserId != userId)
        {
            return Forbid();
        }

        var orderDto = await GetOrderDto(id);
        return Ok(orderDto);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<OrderDto>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            return NotFound();
        }

        order.OrderStatus = request.OrderStatus;
        if (!string.IsNullOrEmpty(request.PaymentStatus))
        {
            order.PaymentStatus = request.PaymentStatus;
        }
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var orderDto = await GetOrderDto(id);
        return Ok(orderDto);
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<OrderDto>> CancelOrder(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        if (!isAdmin && order.UserId != userId)
        {
            return Forbid();
        }

        if (order.OrderStatus == "Cancelled" || order.OrderStatus == "Completed")
        {
            return BadRequest(new { message = "Không thể hủy đơn hàng này" });
        }

        foreach (var item in order.Items!)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.VariantSizes)
                .FirstOrDefaultAsync(v => v.Id == item.ProductVariantId);
            if (variant != null)
            {
                var variantSize = variant.VariantSizes?.FirstOrDefault(vs => vs.SizeId == item.SizeId);
                if (variantSize != null)
                {
                    variantSize.Stock += item.Quantity;
                }
            }
        }

        order.OrderStatus = "Cancelled";
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var orderDto = await GetOrderDto(id);
        return Ok(orderDto);
    }

    private async Task<OrderDto> GetOrderDto(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return new OrderDto
        {
            Id = order!.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            UserName = order.User?.FullName ?? "",
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            OrderStatus = order.OrderStatus,
            ShippingAddress = order.ShippingAddress,
            Phone = order.Phone,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            Items = order.Items?.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ColorName = i.ColorName,
                SizeName = i.SizeName,
                Price = i.Price,
                Quantity = i.Quantity,
                SubTotal = i.SubTotal
            }).ToList()
        };
    }
}


