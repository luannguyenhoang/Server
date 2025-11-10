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
public class CartController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CartController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CartItemDto>>> GetCart()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var cartItems = await _context.Carts
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Product)
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Color)
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .Include(c => c.Size)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        var cartItemDtos = cartItems
            .Where(c => c.ProductVariant != null && c.ProductVariant.Product != null)
            .Select(c => new CartItemDto
            {
                Id = c.Id,
                ProductId = c.ProductVariant!.ProductId,
                ProductName = c.ProductVariant.Product!.Name,
                ProductVariantId = c.ProductVariantId,
                SizeId = c.SizeId,
                ColorName = c.ProductVariant.Color?.Name ?? "",
                SizeName = c.Size?.Name ?? "",
                Price = c.ProductVariant.Product.Price, // Giá gốc
                SalePrice = c.ProductVariant.Product.SalePrice, // Giá giảm (nếu có)
                Quantity = c.Quantity,
                ImageUrl = c.ProductVariant.ImageUrl ?? c.ProductVariant.Product.ImageUrl,
                Stock = c.ProductVariant.VariantSizes?.FirstOrDefault(vs => vs.SizeId == c.SizeId)?.Stock ?? 0
            }).ToList();

        return Ok(cartItemDtos);
    }

    [HttpPost]
    public async Task<ActionResult<CartItemDto>> AddToCart([FromBody] AddToCartRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var variant = await _context.ProductVariants
            .Include(v => v.Product)
            .Include(v => v.VariantSizes!)
                .ThenInclude(vs => vs.Size)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId);

        if (variant == null)
        {
            return NotFound(new { message = "Sản phẩm không tồn tại" });
        }

        var variantSize = variant.VariantSizes?.FirstOrDefault(vs => vs.SizeId == request.SizeId);
        if (variantSize == null)
        {
            return BadRequest(new { message = "Size không tồn tại cho variant này" });
        }

        if (variantSize.Stock < request.Quantity)
        {
            return BadRequest(new { message = "Số lượng sản phẩm không đủ" });
        }

        var existingCart = await _context.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductVariantId == request.ProductVariantId && c.SizeId == request.SizeId);

        if (existingCart != null)
        {
            existingCart.Quantity += request.Quantity;
            if (existingCart.Quantity > variantSize.Stock)
            {
                return BadRequest(new { message = "Số lượng vượt quá tồn kho" });
            }
            existingCart.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var cart = new Cart
            {
                UserId = userId,
                ProductVariantId = request.ProductVariantId,
                SizeId = request.SizeId,
                Quantity = request.Quantity
            };
            _context.Carts.Add(cart);
        }

        await _context.SaveChangesAsync();

        var cartItem = await _context.Carts
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Product)
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Color)
            .Include(c => c.Size)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductVariantId == request.ProductVariantId && c.SizeId == request.SizeId);

        return Ok(new CartItemDto
        {
            Id = cartItem!.Id,
            ProductId = cartItem.ProductVariant!.ProductId,
            ProductName = cartItem.ProductVariant.Product!.Name,
            ProductVariantId = cartItem.ProductVariantId,
            ColorName = cartItem.ProductVariant.Color?.Name ?? "",
            SizeName = cartItem.Size?.Name ?? "",
            Price = cartItem.ProductVariant.Product.Price, // Giá gốc
            SalePrice = cartItem.ProductVariant.Product.SalePrice, // Giá giảm (nếu có)
            Quantity = cartItem.Quantity,
            ImageUrl = cartItem.ProductVariant.ImageUrl ?? cartItem.ProductVariant.Product.ImageUrl,
            Stock = variantSize.Stock
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CartItemDto>> UpdateCart(int id, [FromBody] UpdateCartRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var cart = await _context.Carts
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Product)
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.VariantSizes!)
                    .ThenInclude(vs => vs.Size)
            .Include(c => c.Size)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (cart == null)
        {
            return NotFound();
        }

        var variantSize = cart.ProductVariant!.VariantSizes?.FirstOrDefault(vs => vs.SizeId == cart.SizeId);
        if (variantSize == null)
        {
            return BadRequest(new { message = "Size không tồn tại cho variant này" });
        }

        if (request.Quantity > variantSize.Stock)
        {
            return BadRequest(new { message = "Số lượng vượt quá tồn kho" });
        }

        cart.Quantity = request.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var cartItem = await _context.Carts
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Product)
            .Include(c => c.ProductVariant!)
                .ThenInclude(v => v.Color)
            .Include(c => c.Size)
            .FirstOrDefaultAsync(c => c.Id == id);

        return Ok(new CartItemDto
        {
            Id = cartItem!.Id,
            ProductId = cartItem.ProductVariant!.ProductId,
            ProductName = cartItem.ProductVariant.Product!.Name,
            ProductVariantId = cartItem.ProductVariantId,
            ColorName = cartItem.ProductVariant.Color?.Name ?? "",
            SizeName = cartItem.Size?.Name ?? "",
            Price = cartItem.ProductVariant.Product.Price, // Giá gốc
            SalePrice = cartItem.ProductVariant.Product.SalePrice, // Giá giảm (nếu có)
            Quantity = cartItem.Quantity,
            ImageUrl = cartItem.ProductVariant.ImageUrl ?? cartItem.ProductVariant.Product.ImageUrl,
            Stock = variantSize.Stock
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFromCart(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var cart = await _context.Carts
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (cart == null)
        {
            return NotFound();
        }

        _context.Carts.Remove(cart);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}


