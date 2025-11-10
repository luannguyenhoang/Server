namespace HoodLab.Api.Models;

public class RevenueStatistics
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class StatisticsRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? GroupBy { get; set; } = "day";
}

public class StatisticsResponse
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }
    public List<RevenueStatistics>? RevenueByPeriod { get; set; }
}

public class ProductSalesStatistics
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }
}

public class ProductSalesResponse
{
    public int TotalProductsSold { get; set; }
    public int TotalQuantitySold { get; set; }
    public List<ProductSalesStatistics>? Products { get; set; }
}


