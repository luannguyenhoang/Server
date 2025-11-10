using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using System.Security.Claims;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SizesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SizesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Size>>> GetSizes()
    {
        var isAdmin = User.Identity?.IsAuthenticated == true && 
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));
        
        var query = _context.Sizes.AsQueryable();
        
        if (!isAdmin)
        {
            query = query.Where(s => s.IsActive);
        }

        var sizes = await query.ToListAsync();

        return Ok(sizes);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<Size>> CreateSize([FromBody] Size size)
    {
        _context.Sizes.Add(size);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSizes), new { id = size.Id }, size);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UpdateSize(int id, [FromBody] Size request)
    {
        var size = await _context.Sizes.FindAsync(id);
        if (size == null)
        {
            return NotFound();
        }

        size.Name = request.Name;
        size.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSize(int id)
    {
        var size = await _context.Sizes.FindAsync(id);
        if (size == null)
        {
            return NotFound();
        }

        _context.Sizes.Remove(size);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}


