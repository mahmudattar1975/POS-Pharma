using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

namespace PharmaApp.Controllers;

[Authorize]
public class CashierController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Cashier> _hasher = new();

    public CashierController(AppDbContext db)
    {
        _db = db;
    }

    private async Task<bool> IsCurrentUserAdmin()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var cashier = await _db.Cashiers.FindAsync(id);
        return cashier?.IsAdmin == true;
    }

    public class CreateCashierRequest
    {
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public bool IsAdmin { get; set; } = false;
    }

    // POST /Cashier/Create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashierRequest request)
    {
        if (!await IsCurrentUserAdmin()) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Username and password are required.");

        if (await _db.Cashiers.AnyAsync(c => c.Username == request.Username))
            return BadRequest("That username is already taken.");

        var cashier = new Cashier
        {
            Username = request.Username,
            FullName = request.FullName,
            IsAdmin = request.IsAdmin,
            IsActive = true,
            CreatedOn = DateTime.Now,
        };
        cashier.PasswordHash = _hasher.HashPassword(cashier, request.Password);

        _db.Cashiers.Add(cashier);
        await _db.SaveChangesAsync();

        return Ok(new { cashier.Id, cashier.Username, cashier.FullName, cashier.IsAdmin });
    }

    // GET /Cashier
    public async Task<IActionResult> Index()
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        var cashiers = await _db.Cashiers
            .Select(c => new { c.Id, c.Username, c.FullName, c.IsActive, c.IsAdmin, c.CreatedOn })
            .ToListAsync();
        return Ok(cashiers);
    }

    // POST /Cashier/Deactivate/5
    [HttpPost]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        var cashier = await _db.Cashiers.FindAsync(id);
        if (cashier == null) return NotFound();
        cashier.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
