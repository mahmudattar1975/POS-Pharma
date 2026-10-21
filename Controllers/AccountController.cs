using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;
using PharmaApp.Models;

namespace PharmaApp.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<Cashier> _hasher = new();

    public AccountController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = default!;
        public int CashierId { get; set; }
        public string Username { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public bool IsAdmin { get; set; }
    }

    // POST /Account/Login - returns a JWT for the React frontend to attach as
    // "Authorization: Bearer <token>" on every subsequent request.
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model?.Username) || string.IsNullOrWhiteSpace(model?.Password))
            return BadRequest("Username and password are required.");

        var cashier = await _db.Cashiers
            .FirstOrDefaultAsync(c => c.Username == model.Username && c.IsActive);

        // Same error for "no such user" and "wrong password" - don't reveal which one it was.
        if (cashier == null ||
            _hasher.VerifyHashedPassword(cashier, cashier.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid username or password." });
        }

        var token = IssueToken(cashier);

        return Ok(new LoginResponse
        {
            Token = token,
            CashierId = cashier.Id,
            Username = cashier.Username,
            FullName = cashier.FullName,
            IsAdmin = cashier.IsAdmin,
        });
    }

    private string IssueToken(Cashier cashier)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, cashier.Id.ToString()),
            new Claim(ClaimTypes.Name, cashier.FullName),
            new Claim("isAdmin", cashier.IsAdmin.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12), // long enough to cover a full shift
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
