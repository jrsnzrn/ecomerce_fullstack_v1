using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;


namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher = new();
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password are required.");

        var exists = await _db.AppUsers.AnyAsync(u => u.Email == email);
        if (exists) return Conflict("Email is already registered.");

        var user = new AppUser { Email = email, Role ="User"};
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null) return Unauthorized("Invalid credentials.");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid credentials.");

        var token = CreateJwt(user);
        return Ok(new { token });
    }

    private string CreateJwt(AppUser user)
    {
        var key = _config["Jwt:Key"]!;
        var issuer = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var expiresMinutes = int.Parse(_config["Jwt:ExpiresMinutes"]!);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),

        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

        // ✅ ADMIN ONLY: Set user role (fix old users)
    [Authorize(Roles = "Admin")]
    [HttpPut("set-role/{userId:int}")]
    public async Task<IActionResult> SetRole(int userId, [FromQuery] string role)
    {
        role = role.Trim();

        if (role != "User" && role != "Admin")
            return BadRequest("Role must be 'User' or 'Admin'.");

        var user = await _db.AppUsers.FindAsync(userId);
        if (user is null) return NotFound();

        user.Role = role;
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Email, user.Role });
    }

    [Authorize(Roles = "Admin")]
[HttpGet("users")]
public async Task<IActionResult> GetAllUsers()
{
    var users = await _db.AppUsers
        .Select(u => new { u.Id, u.Email, u.Role })
        .ToListAsync();

    return Ok(users);
}





}
