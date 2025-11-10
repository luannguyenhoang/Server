using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HoodLab.Api.Data;
using HoodLab.Api.Models;
using System.Security.Claims;

namespace HoodLab.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ColorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ColorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Color>>> GetColors()
    {
        var isAdmin = User.Identity?.IsAuthenticated == true && 
                     (User.IsInRole("Admin") || User.IsInRole("Staff"));
        
        var query = _context.Colors.AsQueryable();
        
        if (!isAdmin)
        {
            query = query.Where(c => c.IsActive);
        }

        var colors = await query.ToListAsync();

        return Ok(colors);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<Color>> CreateColor([FromBody] Color color)
    {
        _context.Colors.Add(color);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetColors), new { id = color.Id }, color);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UpdateColor(int id, [FromBody] Color request)
    {
        var color = await _context.Colors.FindAsync(id);
        if (color == null)
        {
            return NotFound();
        }

        color.Name = request.Name;
        color.HexCode = request.HexCode;
        color.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteColor(int id)
    {
        var color = await _context.Colors.FindAsync(id);
        if (color == null)
        {
            return NotFound();
        }

        _context.Colors.Remove(color);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}


