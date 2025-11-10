using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using System.Security.Claims;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlidersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SlidersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<SliderDto>>> GetSliders()
    {
        var isAdmin = User.Identity?.IsAuthenticated == true &&
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));

        var query = _context.Sliders.AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(s => s.IsActive);
        }

        var sliders = await query
            .OrderBy(s => s.DisplayOrder)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new SliderDto
            {
                Id = s.Id,
                ImageUrl = s.ImageUrl,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(sliders);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SliderDto>> GetSlider(int id)
    {
        var slider = await _context.Sliders.FindAsync(id);
        if (slider == null)
        {
            return NotFound();
        }

        var isAdmin = User.Identity?.IsAuthenticated == true &&
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));

        if (!isAdmin && !slider.IsActive)
        {
            return NotFound();
        }

        var sliderDto = new SliderDto
        {
            Id = slider.Id,
            ImageUrl = slider.ImageUrl,
            DisplayOrder = slider.DisplayOrder,
            IsActive = slider.IsActive,
            CreatedAt = slider.CreatedAt,
            UpdatedAt = slider.UpdatedAt
        };

        return Ok(sliderDto);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<SliderDto>> CreateSlider([FromBody] CreateSliderRequest request)
    {
        var slider = new Slider
        {
            ImageUrl = request.ImageUrl,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Sliders.Add(slider);
        await _context.SaveChangesAsync();

        var sliderDto = new SliderDto
        {
            Id = slider.Id,
            ImageUrl = slider.ImageUrl,
            DisplayOrder = slider.DisplayOrder,
            IsActive = slider.IsActive,
            CreatedAt = slider.CreatedAt,
            UpdatedAt = slider.UpdatedAt
        };

        return CreatedAtAction(nameof(GetSlider), new { id = slider.Id }, sliderDto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UpdateSlider(int id, [FromBody] UpdateSliderRequest request)
    {
        var slider = await _context.Sliders.FindAsync(id);
        if (slider == null)
        {
            return NotFound();
        }

        slider.ImageUrl = request.ImageUrl;
        slider.DisplayOrder = request.DisplayOrder;
        slider.IsActive = request.IsActive;
        slider.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSlider(int id)
    {
        var slider = await _context.Sliders.FindAsync(id);
        if (slider == null)
        {
            return NotFound();
        }

        _context.Sliders.Remove(slider);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

