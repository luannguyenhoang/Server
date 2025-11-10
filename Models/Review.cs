namespace HoodLab.Api.Models;

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public Product? Product { get; set; }
    public User? User { get; set; }
    public Order? Order { get; set; }
}

public class ReviewDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int OrderItemId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReviewRequest
{
    public int OrderId { get; set; }
    public int OrderItemId { get; set; }
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}

public class UpdateReviewRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? ImageUrls { get; set; }
}

