using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using System.Security.Claims;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<NewsDto>>> GetNews()
    {
        var isAdmin = User.Identity?.IsAuthenticated == true &&
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));

        var query = _context.News.AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(n => n.IsActive);
        }

        var news = await query
            .OrderByDescending(n => n.PublishedAt)
            .ThenByDescending(n => n.CreatedAt)
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Excerpt = n.Excerpt,
                Content = n.Content,
                ImageUrl = n.ImageUrl,
                Category = n.Category,
                PublishedAt = n.PublishedAt,
                IsActive = n.IsActive,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync();

        return Ok(news);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NewsDto>> GetNewsItem(int id)
    {
        var newsItem = await _context.News.FindAsync(id);
        if (newsItem == null)
        {
            return NotFound();
        }

        var isAdmin = User.Identity?.IsAuthenticated == true &&
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));

        if (!isAdmin && !newsItem.IsActive)
        {
            return NotFound();
        }

        var newsDto = new NewsDto
        {
            Id = newsItem.Id,
            Title = newsItem.Title,
            Excerpt = newsItem.Excerpt,
            Content = newsItem.Content,
            ImageUrl = newsItem.ImageUrl,
            Category = newsItem.Category,
            PublishedAt = newsItem.PublishedAt,
            IsActive = newsItem.IsActive,
            CreatedAt = newsItem.CreatedAt,
            UpdatedAt = newsItem.UpdatedAt
        };

        return Ok(newsDto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<NewsDto>> CreateNews([FromBody] CreateNewsRequest request)
    {
        var news = new News
        {
            Title = request.Title,
            Excerpt = request.Excerpt,
            Content = request.Content,
            ImageUrl = request.ImageUrl,
            Category = request.Category,
            PublishedAt = request.PublishedAt ?? DateTime.UtcNow,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.News.Add(news);
        await _context.SaveChangesAsync();

        var newsDto = new NewsDto
        {
            Id = news.Id,
            Title = news.Title,
            Excerpt = news.Excerpt,
            Content = news.Content,
            ImageUrl = news.ImageUrl,
            Category = news.Category,
            PublishedAt = news.PublishedAt,
            IsActive = news.IsActive,
            CreatedAt = news.CreatedAt,
            UpdatedAt = news.UpdatedAt
        };

        return CreatedAtAction(nameof(GetNewsItem), new { id = news.Id }, newsDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UpdateNews(int id, [FromBody] UpdateNewsRequest request)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
        {
            return NotFound();
        }

        news.Title = request.Title;
        news.Excerpt = request.Excerpt;
        news.Content = request.Content;
        news.ImageUrl = request.ImageUrl;
        news.Category = request.Category;
        if (request.PublishedAt.HasValue)
        {
            news.PublishedAt = request.PublishedAt.Value;
        }
        news.IsActive = request.IsActive;
        news.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteNews(int id)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
        {
            return NotFound();
        }

        _context.News.Remove(news);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

