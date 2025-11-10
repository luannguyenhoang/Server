using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Staff")]
public class StatisticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StatisticsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<StatisticsResponse>> GetStatistics([FromQuery] StatisticsRequest request)
    {
        DateTime startDate;
        DateTime endDate;

        if (request.StartDate.HasValue)
        {
            // Nếu có startDate, set về đầu ngày (00:00:00) UTC
            startDate = request.StartDate.Value.Date.ToUniversalTime();
        }
        else
        {
            startDate = DateTime.UtcNow.AddMonths(-1).Date;
        }

        if (request.EndDate.HasValue)
        {
            // Nếu có endDate, set về cuối ngày (23:59:59) UTC
            endDate = request.EndDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        else
        {
            endDate = DateTime.UtcNow;
        }

        var ordersQuery = _context.Orders
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && 
                       o.PaymentStatus == "Paid");

        var totalRevenue = await ordersQuery.SumAsync(o => o.TotalAmount);
        var totalOrders = await ordersQuery.CountAsync();
        var totalProducts = await _context.Products.CountAsync(p => p.IsActive);
        var totalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer" && u.IsActive);

        var revenueByPeriod = new List<RevenueStatistics>();

        if (request.GroupBy == "day")
        {
            revenueByPeriod = await ordersQuery
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new RevenueStatistics
                {
                    Date = g.Key,
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(s => s.Date)
                .ToListAsync();
        }
        else if (request.GroupBy == "month")
        {
            revenueByPeriod = await ordersQuery
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .Select(g => new RevenueStatistics
                {
                    Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(s => s.Date)
                .ToListAsync();
        }
        else if (request.GroupBy == "year")
        {
            revenueByPeriod = await ordersQuery
                .GroupBy(o => o.CreatedAt.Year)
                .Select(g => new RevenueStatistics
                {
                    Date = new DateTime(g.Key, 1, 1),
                    Revenue = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                })
                .OrderBy(s => s.Date)
                .ToListAsync();
        }

        return Ok(new StatisticsResponse
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,
            TotalProducts = totalProducts,
            TotalCustomers = totalCustomers,
            RevenueByPeriod = revenueByPeriod
        });
    }

    [HttpGet("products")]
    public async Task<ActionResult<ProductSalesResponse>> GetProductSales([FromQuery] StatisticsRequest request)
    {
        DateTime startDate;
        DateTime endDate;

        if (request.StartDate.HasValue)
        {
            startDate = request.StartDate.Value.Date.ToUniversalTime();
        }
        else
        {
            startDate = DateTime.UtcNow.AddMonths(-1).Date;
        }

        if (request.EndDate.HasValue)
        {
            endDate = request.EndDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
        }
        else
        {
            endDate = DateTime.UtcNow;
        }

        var orders = await _context.Orders
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate && 
                       o.PaymentStatus == "Paid")
            .Include(o => o.Items)
            .ToListAsync();

        var orderItems = orders
            .SelectMany(o => o.Items ?? new List<OrderItem>())
            .ToList();

        var productSales = orderItems
            .GroupBy(oi => new { oi.ProductId, oi.ProductName })
            .Select(g => new ProductSalesStatistics
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantitySold = g.Sum(oi => oi.Quantity),
                TotalRevenue = g.Sum(oi => oi.SubTotal),
                OrderCount = g.Select(oi => oi.OrderId).Distinct().Count()
            })
            .OrderByDescending(p => p.TotalQuantitySold)
            .ToList();

        // Lấy ImageUrl từ Product
        var productIds = productSales.Select(p => p.ProductId).ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ImageUrl })
            .ToListAsync();

        var productImageMap = products.ToDictionary(p => p.Id, p => p.ImageUrl);
        foreach (var productSale in productSales)
        {
            productSale.ImageUrl = productImageMap.GetValueOrDefault(productSale.ProductId);
        }

        var totalQuantitySold = productSales.Sum(p => p.TotalQuantitySold);
        var totalProductsSold = productSales.Count;

        return Ok(new ProductSalesResponse
        {
            TotalProductsSold = totalProductsSold,
            TotalQuantitySold = totalQuantitySold,
            Products = productSales
        });
    }
}


