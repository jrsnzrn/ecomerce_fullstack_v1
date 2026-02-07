using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Authorization;
using BackendAPI.Dtos;



namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;
    
[HttpPut("{id:int}")]
public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return BadRequest("Name is required.");

    var user = await _db.Users.FindAsync(id);
    if (user is null) return NotFound();

    user.Name = req.Name.Trim();
    await _db.SaveChangesAsync();

    return Ok(user);
}

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Users.AsNoTracking().ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

  [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
