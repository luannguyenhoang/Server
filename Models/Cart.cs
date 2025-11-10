namespace HoodLab.Api.Models;

public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductVariantId { get; set; }
    public int SizeId { get; set; } // Size cụ thể được chọn
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public User? User { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Size? Size { get; set; }
}

public class CartItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int ProductVariantId { get; set; }
    public int SizeId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
}

public class AddToCartRequest
{
    public int ProductVariantId { get; set; }
    public int SizeId { get; set; } // Size cụ thể được chọn
    public int Quantity { get; set; }
}

public class UpdateCartRequest
{
    public int Quantity { get; set; }
}


