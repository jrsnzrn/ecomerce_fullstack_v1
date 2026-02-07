using BackendAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    // ✅ Any logged-in user can access
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var email = User.FindFirstValue(ClaimTypes.Email);
        var role = User.FindFirstValue(ClaimTypes.Role);

        // optional: confirm account still exists in DB
        var exists = await _db.AppUsers.AnyAsync(u => u.Id == userId);
        if (!exists) return Unauthorized("Account no longer exists.");

        return Ok(new { id = userId, email, role });
    }

    // ✅ Admin-only stats endpoint
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/stats")]
    public async Task<IActionResult> AdminStats()
    {
        var totalAccounts = await _db.AppUsers.CountAsync();
        var totalUsersTable = await _db.Users.CountAsync();

        return Ok(new
        {
            totalAccounts,
            totalUsersTable
        });
    }
}
