using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? brandId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.Color)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => p.Name.Contains(search) || 
                                    (p.Description != null && p.Description.Contains(search)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.SalePrice.HasValue ? p.SalePrice >= minPrice : p.Price >= minPrice);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.SalePrice.HasValue ? p.SalePrice <= maxPrice : p.Price <= maxPrice);
        }

        var products = await query.ToListAsync();

        var productDtos = products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            SalePrice = p.SalePrice,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? "",
            BrandId = p.BrandId,
            BrandName = p.Brand?.Name ?? "",
            // Tính stock từ tổng stock của tất cả variants
            Stock = p.Variants?.SelectMany(v => v.VariantSizes ?? new List<ProductVariantSize>())
                .Sum(vs => vs.Stock) ?? p.Stock,
            ImageUrl = p.ImageUrl,
            ImageUrls = p.ImageUrls,
            Variants = p.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                ColorId = v.ColorId,
                ColorName = v.Color?.Name ?? "",
                ColorHexCode = v.Color?.HexCode ?? "",
                SizeIds = v.VariantSizes?.Select(vs => new ProductVariantSizeDto
                {
                    IdSize = vs.SizeId,
                    NameSize = vs.Size?.Name ?? "",
                    Stock = vs.Stock
                }).ToList() ?? new List<ProductVariantSizeDto>(),
                Stock = v.Stock,
                ImageUrl = v.ImageUrl
            }).ToList()
        }).ToList();

        return Ok(productDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.Color)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
        {
            return NotFound();
        }

        var productDto = new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            SalePrice = product.SalePrice,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? "",
            BrandId = product.BrandId,
            BrandName = product.Brand?.Name ?? "",
            // Tính stock từ tổng stock của tất cả variants
            Stock = product.Variants?.SelectMany(v => v.VariantSizes ?? new List<ProductVariantSize>())
                .Sum(vs => vs.Stock) ?? product.Stock,
            ImageUrl = product.ImageUrl,
            ImageUrls = product.ImageUrls,
            Variants = product.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                ColorId = v.ColorId,
                ColorName = v.Color?.Name ?? "",
                ColorHexCode = v.Color?.HexCode ?? "",
                SizeIds = v.VariantSizes?.Select(vs => new ProductVariantSizeDto
                {
                    IdSize = vs.SizeId,
                    NameSize = vs.Size?.Name ?? "",
                    Stock = vs.Stock
                }).ToList() ?? new List<ProductVariantSizeDto>(),
                Stock = v.Stock,
                ImageUrl = v.ImageUrl
            }).ToList()
        };

        return Ok(productDto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
        {
            return BadRequest(new { message = "Category không tồn tại" });
        }

        var brand = await _context.Brands.FindAsync(request.BrandId);
        if (brand == null)
        {
            return BadRequest(new { message = "Brand không tồn tại" });
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            SalePrice = request.SalePrice,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Stock = 0, // Sẽ được tính từ variants sau
            ImageUrl = request.ImageUrl,
            ImageUrls = request.ImageUrls,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        int totalStock = 0;

        if (request.Variants != null && request.Variants.Any())
        {
            foreach (var variantRequest in request.Variants)
            {
                var color = await _context.Colors.FindAsync(variantRequest.ColorId);
                if (color == null)
                {
                    return BadRequest(new { message = $"Color với ID {variantRequest.ColorId} không tồn tại" });
                }

                if (variantRequest.SizeIds == null || !variantRequest.SizeIds.Any())
                {
                    return BadRequest(new { message = "Variant phải có ít nhất một size" });
                }

                var variant = new ProductVariant
                {
                    ProductId = product.Id,
                    ColorId = variantRequest.ColorId,
                    ImageUrl = variantRequest.ImageUrl
                };

                _context.ProductVariants.Add(variant);
                await _context.SaveChangesAsync();

                // Thêm các size cho variant
                foreach (var sizeRequest in variantRequest.SizeIds)
                {
                    var size = await _context.Sizes.FindAsync(sizeRequest.IdSize);
                    if (size == null)
                    {
                        return BadRequest(new { message = $"Size với ID {sizeRequest.IdSize} không tồn tại" });
                    }

                    var variantSize = new ProductVariantSize
                    {
                        ProductVariantId = variant.Id,
                        SizeId = sizeRequest.IdSize,
                        Stock = sizeRequest.Stock
                    };

                    _context.ProductVariantSizes.Add(variantSize);
                    
                    // Tính tổng stock
                    totalStock += sizeRequest.Stock;
                }
            }

            await _context.SaveChangesAsync();
        }

        // Cập nhật stock của product từ tổng stock của variants
        product.Stock = totalStock;
        await _context.SaveChangesAsync();

        var createdProduct = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.Color)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .FirstOrDefaultAsync(p => p.Id == product.Id);

        var productDto = new ProductDto
        {
            Id = createdProduct!.Id,
            Name = createdProduct.Name,
            Description = createdProduct.Description,
            Price = createdProduct.Price,
            SalePrice = createdProduct.SalePrice,
            CategoryId = createdProduct.CategoryId,
            CategoryName = createdProduct.Category?.Name ?? "",
            BrandId = createdProduct.BrandId,
            BrandName = createdProduct.Brand?.Name ?? "",
            // Tính stock từ tổng stock của tất cả variants
            Stock = createdProduct.Variants?.SelectMany(v => v.VariantSizes ?? new List<ProductVariantSize>())
                .Sum(vs => vs.Stock) ?? createdProduct.Stock,
            ImageUrl = createdProduct.ImageUrl,
            ImageUrls = createdProduct.ImageUrls,
            Variants = createdProduct.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                ColorId = v.ColorId,
                ColorName = v.Color?.Name ?? "",
                ColorHexCode = v.Color?.HexCode ?? "",
                SizeIds = v.VariantSizes?.Select(vs => new ProductVariantSizeDto
                {
                    IdSize = vs.SizeId,
                    NameSize = vs.Size?.Name ?? "",
                    Stock = vs.Stock
                }).ToList() ?? new List<ProductVariantSizeDto>(),
                Stock = v.Stock,
                ImageUrl = v.ImageUrl
            }).ToList()
        };

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, productDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
        
        if (product == null)
        {
            return NotFound();
        }

        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
        {
            return BadRequest(new { message = "Category không tồn tại" });
        }

        var brand = await _context.Brands.FindAsync(request.BrandId);
        if (brand == null)
        {
            return BadRequest(new { message = "Brand không tồn tại" });
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.SalePrice = request.SalePrice;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.Stock = 0; // Sẽ được tính từ variants sau
        product.ImageUrl = request.ImageUrl;
        product.ImageUrls = request.ImageUrls;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        int totalStock = 0;

        if (request.Variants != null && request.Variants.Any())
        {
            var existingVariantIds = request.Variants
                .Where(v => v.Id.HasValue)
                .Select(v => v.Id!.Value)
                .ToList();

            var variantsToDelete = product.Variants?
                .Where(v => !existingVariantIds.Contains(v.Id))
                .ToList() ?? new List<ProductVariant>();

            foreach (var variantToDelete in variantsToDelete)
            {
                _context.ProductVariants.Remove(variantToDelete);
            }

            foreach (var variantRequest in request.Variants)
            {
                if (variantRequest.Id.HasValue)
                {
                    var existingVariant = await _context.ProductVariants
                        .Include(v => v.VariantSizes)
                        .FirstOrDefaultAsync(v => v.Id == variantRequest.Id.Value && v.ProductId == id);
                    
                    if (existingVariant != null)
                    {
                        var color = await _context.Colors.FindAsync(variantRequest.ColorId);
                        if (color == null)
                        {
                            return BadRequest(new { message = $"Color với ID {variantRequest.ColorId} không tồn tại" });
                        }

                        if (variantRequest.SizeIds == null || !variantRequest.SizeIds.Any())
                        {
                            return BadRequest(new { message = "Variant phải có ít nhất một size" });
                        }

                        existingVariant.ColorId = variantRequest.ColorId;
                        existingVariant.ImageUrl = variantRequest.ImageUrl;

                        // Xử lý sizes
                        var existingSizeIds = variantRequest.SizeIds
                            .Where(s => s.Id.HasValue)
                            .Select(s => s.Id!.Value)
                            .ToList();

                        // Xóa các size không còn trong request
                        var sizesToDelete = existingVariant.VariantSizes?
                            .Where(vs => !existingSizeIds.Contains(vs.Id))
                            .ToList() ?? new List<ProductVariantSize>();

                        foreach (var sizeToDelete in sizesToDelete)
                        {
                            _context.ProductVariantSizes.Remove(sizeToDelete);
                        }

                        // Cập nhật hoặc thêm mới sizes
                        foreach (var sizeRequest in variantRequest.SizeIds)
                        {
                            var size = await _context.Sizes.FindAsync(sizeRequest.IdSize);
                            if (size == null)
                            {
                                return BadRequest(new { message = $"Size với ID {sizeRequest.IdSize} không tồn tại" });
                            }

                            if (sizeRequest.Id.HasValue)
                            {
                                // Cập nhật size hiện có
                                var existingSize = existingVariant.VariantSizes?
                                    .FirstOrDefault(vs => vs.Id == sizeRequest.Id.Value);
                                if (existingSize != null)
                                {
                                    existingSize.SizeId = sizeRequest.IdSize;
                                    existingSize.Stock = sizeRequest.Stock;
                                }
                            }
                            else
                            {
                                // Thêm size mới
                                var newVariantSize = new ProductVariantSize
                                {
                                    ProductVariantId = existingVariant.Id,
                                    SizeId = sizeRequest.IdSize,
                                    Stock = sizeRequest.Stock
                                };
                                _context.ProductVariantSizes.Add(newVariantSize);
                            }
                        }
                    }
                }
                else
                {
                    var color = await _context.Colors.FindAsync(variantRequest.ColorId);
                    if (color == null)
                    {
                        return BadRequest(new { message = $"Color với ID {variantRequest.ColorId} không tồn tại" });
                    }

                    if (variantRequest.SizeIds == null || !variantRequest.SizeIds.Any())
                    {
                        return BadRequest(new { message = "Variant phải có ít nhất một size" });
                    }

                    var newVariant = new ProductVariant
                    {
                        ProductId = id,
                        ColorId = variantRequest.ColorId,
                        ImageUrl = variantRequest.ImageUrl
                    };

                    _context.ProductVariants.Add(newVariant);
                    await _context.SaveChangesAsync();

                    // Thêm các size cho variant mới
                    foreach (var sizeRequest in variantRequest.SizeIds)
                    {
                        var size = await _context.Sizes.FindAsync(sizeRequest.IdSize);
                        if (size == null)
                        {
                            return BadRequest(new { message = $"Size với ID {sizeRequest.IdSize} không tồn tại" });
                        }

                        var variantSize = new ProductVariantSize
                        {
                            ProductVariantId = newVariant.Id,
                            SizeId = sizeRequest.IdSize,
                            Stock = sizeRequest.Stock
                        };

                        _context.ProductVariantSizes.Add(variantSize);
                        
                        // Tính tổng stock
                        totalStock += sizeRequest.Stock;
                    }
                }
            }
            
            await _context.SaveChangesAsync();
            
            // Tính lại tổng stock từ tất cả variants
            var allVariants = await _context.ProductVariants
                .Include(v => v.VariantSizes)
                .Where(v => v.ProductId == id)
                .ToListAsync();
            
            totalStock = allVariants
                .SelectMany(v => v.VariantSizes ?? new List<ProductVariantSize>())
                .Sum(vs => vs.Stock);
        }

        // Cập nhật stock của product từ tổng stock của variants
        product.Stock = totalStock;
        await _context.SaveChangesAsync();

        var updatedProduct = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.Color)
            .Include(p => p.Variants!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .FirstOrDefaultAsync(p => p.Id == id);

        var productDto = new ProductDto
        {
            Id = updatedProduct!.Id,
            Name = updatedProduct.Name,
            Description = updatedProduct.Description,
            Price = updatedProduct.Price,
            SalePrice = updatedProduct.SalePrice,
            CategoryId = updatedProduct.CategoryId,
            CategoryName = updatedProduct.Category?.Name ?? "",
            BrandId = updatedProduct.BrandId,
            BrandName = updatedProduct.Brand?.Name ?? "",
            // Tính stock từ tổng stock của tất cả variants
            Stock = updatedProduct.Variants?.SelectMany(v => v.VariantSizes ?? new List<ProductVariantSize>())
                .Sum(vs => vs.Stock) ?? updatedProduct.Stock,
            ImageUrl = updatedProduct.ImageUrl,
            ImageUrls = updatedProduct.ImageUrls,
            Variants = updatedProduct.Variants?.Select(v => new ProductVariantDto
            {
                Id = v.Id,
                ColorId = v.ColorId,
                ColorName = v.Color?.Name ?? "",
                ColorHexCode = v.Color?.HexCode ?? "",
                SizeIds = v.VariantSizes?.Select(vs => new ProductVariantSizeDto
                {
                    IdSize = vs.SizeId,
                    NameSize = vs.Size?.Name ?? "",
                    Stock = vs.Stock
                }).ToList() ?? new List<ProductVariantSizeDto>(),
                Stock = v.Stock,
                ImageUrl = v.ImageUrl
            }).ToList()
        };

        return Ok(productDto);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}


