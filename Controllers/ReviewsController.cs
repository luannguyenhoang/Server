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
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("product/{productId}")]
    public async Task<ActionResult<List<ReviewDto>>> GetProductReviews(int productId)
    {
        var reviews = await _context.Reviews
            .Where(r => r.ProductId == productId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                UserId = r.UserId,
                UserName = r.User!.FullName,
                OrderItemId = r.OrderItemId,
                Rating = r.Rating,
                Comment = r.Comment,
                ImageUrls = r.ImageUrls,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<List<ReviewDto>>> GetOrderReviews(int orderId)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        var reviews = await _context.Reviews
            .Where(r => r.OrderId == orderId && r.UserId == userId)
            .Include(r => r.User)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                ProductId = r.ProductId,
                UserId = r.UserId,
                UserName = r.User!.FullName,
                OrderItemId = r.OrderItemId,
                Rating = r.Rating,
                Comment = r.Comment,
                ImageUrls = r.ImageUrls,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(reviews);
    }

    [HttpPost]
    public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Kiểm tra đơn hàng có tồn tại và thuộc về user không
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.UserId == userId);

        if (order == null)
        {
            return NotFound(new { message = "Không tìm thấy đơn hàng" });
        }

        // Kiểm tra đơn hàng đã hoàn thành chưa
        if (order.OrderStatus != "Completed")
        {
            return BadRequest(new { message = "Chỉ có thể đánh giá sản phẩm khi đơn hàng đã hoàn thành" });
        }

        // Kiểm tra OrderItem có tồn tại trong đơn hàng không
        var orderItem = order.Items?.FirstOrDefault(i => i.Id == request.OrderItemId);
        if (orderItem == null)
        {
            return BadRequest(new { message = "Sản phẩm không tồn tại trong đơn hàng" });
        }

        // Kiểm tra ProductId có khớp không
        if (orderItem.ProductId != request.ProductId)
        {
            return BadRequest(new { message = "ProductId không khớp với OrderItem" });
        }

        // Kiểm tra đã đánh giá chưa
        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.OrderId == request.OrderId && 
                                      r.OrderItemId == request.OrderItemId && 
                                      r.UserId == userId);

        if (existingReview != null)
        {
            return BadRequest(new { message = "Bạn đã đánh giá sản phẩm này rồi" });
        }

        // Validate rating
        if (request.Rating < 1 || request.Rating > 5)
        {
            return BadRequest(new { message = "Đánh giá phải từ 1 đến 5 sao" });
        }

        var review = new Review
        {
            ProductId = request.ProductId,
            UserId = userId,
            OrderId = request.OrderId,
            OrderItemId = request.OrderItemId,
            Rating = request.Rating,
            Comment = request.Comment,
            ImageUrls = request.ImageUrls,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);
        var reviewDto = new ReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            UserName = user?.FullName ?? "",
            OrderItemId = review.OrderItemId,
            Rating = review.Rating,
            Comment = review.Comment,
            ImageUrls = review.ImageUrls,
            CreatedAt = review.CreatedAt
        };

        return CreatedAtAction(nameof(GetProductReviews), new { productId = review.ProductId }, reviewDto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ReviewDto>> UpdateReview(int id, [FromBody] UpdateReviewRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (review == null)
        {
            return NotFound(new { message = "Không tìm thấy đánh giá" });
        }

        // Validate rating
        if (request.Rating < 1 || request.Rating > 5)
        {
            return BadRequest(new { message = "Đánh giá phải từ 1 đến 5 sao" });
        }

        review.Rating = request.Rating;
        review.Comment = request.Comment;
        review.ImageUrls = request.ImageUrls;
        review.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(userId);
        var reviewDto = new ReviewDto
        {
            Id = review.Id,
            ProductId = review.ProductId,
            UserId = review.UserId,
            UserName = user?.FullName ?? "",
            OrderItemId = review.OrderItemId,
            Rating = review.Rating,
            Comment = review.Comment,
            ImageUrls = review.ImageUrls,
            CreatedAt = review.CreatedAt
        };

        return Ok(reviewDto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await _context.Reviews
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (review == null)
        {
            return NotFound(new { message = "Không tìm thấy đánh giá" });
        }

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

