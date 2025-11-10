using System.Linq;

namespace HoodLab.Api.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public Category? Category { get; set; }
    public Brand? Brand { get; set; }
    public List<ProductVariant>? Variants { get; set; }
}

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int ColorId { get; set; }
    public string? ImageUrl { get; set; }
    
    public Product? Product { get; set; }
    public Color? Color { get; set; }
    public List<ProductVariantSize>? VariantSizes { get; set; }
    
    // Computed property for total stock
    public int Stock => VariantSizes?.Sum(vs => vs.Stock) ?? 0;
}

public class ProductVariantSize
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public int SizeId { get; set; }
    public int Stock { get; set; }
    
    public ProductVariant? ProductVariant { get; set; }
    public Size? Size { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public List<ProductVariantDto>? Variants { get; set; }
}

public class ProductVariantDto
{
    public int Id { get; set; }
    public int ColorId { get; set; }
    public string ColorName { get; set; } = string.Empty;
    public string ColorHexCode { get; set; } = string.Empty;
    public List<ProductVariantSizeDto> SizeIds { get; set; } = new();
    public int Stock { get; set; } // Tổng stock từ sizeIds
    public string? ImageUrl { get; set; }
}

public class ProductVariantSizeDto
{
    public int IdSize { get; set; }
    public string NameSize { get; set; } = string.Empty;
    public int Stock { get; set; }
}

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CreateProductVariantRequest>? Variants { get; set; }
}

public class CreateProductVariantRequest
{
    public int ColorId { get; set; }
    public List<CreateProductVariantSizeRequest> SizeIds { get; set; } = new();
    public string? ImageUrl { get; set; }
}

public class CreateProductVariantSizeRequest
{
    public int IdSize { get; set; }
    public int Stock { get; set; }
}

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int CategoryId { get; set; }
    public int BrandId { get; set; }
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? ImageUrls { get; set; }
    public bool IsActive { get; set; } = true;
    public List<UpdateProductVariantRequest>? Variants { get; set; }
}

public class UpdateProductVariantRequest
{
    public int? Id { get; set; }
    public int ColorId { get; set; }
    public List<UpdateProductVariantSizeRequest> SizeIds { get; set; } = new();
    public string? ImageUrl { get; set; }
}

public class UpdateProductVariantSizeRequest
{
    public int? Id { get; set; } // Id của ProductVariantSize nếu đã tồn tại
    public int IdSize { get; set; }
    public int Stock { get; set; }
}


