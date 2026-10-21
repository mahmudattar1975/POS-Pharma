using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

namespace PharmaApp.Controllers;

// Admin-only management of the "commission item list" (CommissionItem.cs) - the ~30 items that
// earn a cashier special commission. Without this, the only way to add/edit a rate was a direct
// SQL write, which is how the one test row (Augmentin) got there.
[Authorize]
public class CommissionItemController : Controller
{
    private readonly AppDbContext _db;

    public CommissionItemController(AppDbContext db)
    {
        _db = db;
    }

    private async Task<bool> IsCurrentUserAdmin()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var cashier = await _db.Cashiers.FindAsync(id);
        return cashier?.IsAdmin == true;
    }

    public class CommissionItemResponse
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public string? Barcode { get; set; }
        public CommissionType CommissionType { get; set; }
        public decimal CommissionValue { get; set; }
        public DateTime ActiveFrom { get; set; }
        public DateTime ActiveTo { get; set; }
        public bool IsActive { get; set; }
    }

    private static CommissionItemResponse ToResponse(CommissionItem c) => new()
    {
        Id = c.Id,
        ItemId = c.ItemId,
        ItemName = c.Item?.Name ?? string.Empty,
        Barcode = c.Item?.Code,
        CommissionType = c.CommissionType,
        CommissionValue = c.CommissionValue,
        ActiveFrom = c.ActiveFrom,
        ActiveTo = c.ActiveTo,
        IsActive = c.IsActive,
    };

    // GET /CommissionItem
    public async Task<IActionResult> Index()
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        var list = await _db.CommissionItems
            .Include(c => c.Item)
            .OrderByDescending(c => c.Id)
            .ToListAsync();
        return Ok(list.Select(ToResponse));
    }

    public class UpsertCommissionItemRequest
    {
        public string Barcode { get; set; } = default!;
        // Only used to register the item if dotnet-pos has never seen this barcode before (e.g.
        // it hasn't been sold here yet) - ignored if the item already exists.
        public string? ItemName { get; set; }
        public CommissionType CommissionType { get; set; } = CommissionType.Flat;
        public decimal CommissionValue { get; set; }
        public DateTime ActiveFrom { get; set; } = DateTime.Today;
        public DateTime ActiveTo { get; set; } = DateTime.Today.AddMonths(1);
    }

    // POST /CommissionItem/Create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCommissionItemRequest request)
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Barcode)) return BadRequest("Barcode is required.");
        if (request.CommissionValue <= 0) return BadRequest("Commission value must be greater than zero.");
        if (request.ActiveTo < request.ActiveFrom) return BadRequest("Active-to date must be on or after active-from date.");

        var item = await _db.Items.FirstOrDefaultAsync(i => i.Code == request.Barcode);
        if (item == null)
        {
            if (string.IsNullOrWhiteSpace(request.ItemName))
                return BadRequest("That barcode hasn't been sold here before - provide itemName to register it.");
            item = new Item { Code = request.Barcode, Name = request.ItemName, IsActive = true };
            _db.Items.Add(item);
        }
        else if (await _db.CommissionItems.AnyAsync(c => c.ItemId == item.Id && c.IsActive))
        {
            return BadRequest("This item already has an active commission rate. Deactivate it before adding a new one.");
        }

        var commissionItem = new CommissionItem
        {
            Item = item,
            CommissionType = request.CommissionType,
            CommissionValue = request.CommissionValue,
            ActiveFrom = request.ActiveFrom,
            ActiveTo = request.ActiveTo,
            IsActive = true,
        };
        _db.CommissionItems.Add(commissionItem);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(commissionItem));
    }

    public class UpdateCommissionItemRequest
    {
        public CommissionType CommissionType { get; set; }
        public decimal CommissionValue { get; set; }
        public DateTime ActiveFrom { get; set; }
        public DateTime ActiveTo { get; set; }
        public bool IsActive { get; set; }
    }

    // POST /CommissionItem/Update/5
    [HttpPost]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCommissionItemRequest request)
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        var commissionItem = await _db.CommissionItems.Include(c => c.Item).FirstOrDefaultAsync(c => c.Id == id);
        if (commissionItem == null) return NotFound();
        if (request.CommissionValue <= 0) return BadRequest("Commission value must be greater than zero.");
        if (request.ActiveTo < request.ActiveFrom) return BadRequest("Active-to date must be on or after active-from date.");

        // CommissionAmount on past PosItem rows is already a locked-in snapshot (see
        // PosController.ComputeCommission) - editing the rate here never touches sales history,
        // only what future sales of this item will earn.
        commissionItem.CommissionType = request.CommissionType;
        commissionItem.CommissionValue = request.CommissionValue;
        commissionItem.ActiveFrom = request.ActiveFrom;
        commissionItem.ActiveTo = request.ActiveTo;
        commissionItem.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
        return Ok(ToResponse(commissionItem));
    }

    // POST /CommissionItem/Deactivate/5
    [HttpPost]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (!await IsCurrentUserAdmin()) return Forbid();
        var commissionItem = await _db.CommissionItems.FindAsync(id);
        if (commissionItem == null) return NotFound();
        commissionItem.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
