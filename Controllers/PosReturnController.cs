using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

namespace PharmaApp.Controllers;

[Authorize]
public class PosReturnController : Controller
{
    private readonly AppDbContext _db;

    public PosReturnController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentCashierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public class CreateReturnLineRequest
    {
        public int PosItemId { get; set; }
        public decimal Qty { get; set; }
    }

    public class CreateReturnRequest
    {
        public int OriginalPosId { get; set; }
        public string? Reason { get; set; }
        public List<CreateReturnLineRequest> Items { get; set; } = new();
    }

    // POST /PosReturn/Create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("A return needs at least one item.");

        var originalSale = await _db.Pos
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.OriginalPosId);
        if (originalSale == null) return NotFound("Original sale not found.");

        var cashier = await _db.Cashiers.FindAsync(CurrentCashierId);
        if (cashier == null) return Unauthorized();

        var openShift = await _db.Shifts
            .FirstOrDefaultAsync(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Open);
        if (openShift == null)
            return BadRequest("No open shift/drawer for this cashier. Open the drawer before processing a return.");

        var posReturn = new PosReturn
        {
            OriginalPosId = originalSale.Id,
            CashierId = cashier.Id,
            ShiftId = openShift.Id,
            ReturnDate = DateTime.Now,
            Reason = request.Reason,
        };

        foreach (var line in request.Items)
        {
            var originalLine = originalSale.Items.FirstOrDefault(i => i.Id == line.PosItemId);
            if (originalLine == null)
                return BadRequest($"Line {line.PosItemId} does not belong to sale #{originalSale.Id}.");

            var remaining = originalLine.Qty - originalLine.ReturnedQty;
            if (line.Qty <= 0 || line.Qty > remaining)
                return BadRequest($"Item '{originalLine.ItemName}': can only return up to {remaining}, requested {line.Qty}.");

            var unitRefund = originalLine.LineTotal / originalLine.Qty; // VAT-inclusive unit price actually charged
            var refundAmount = unitRefund * line.Qty;

            // Proportional clawback of the commission originally earned on this line.
            var commissionClawback = originalLine.Qty > 0
                ? -(originalLine.CommissionAmount * (line.Qty / originalLine.Qty))
                : 0;

            originalLine.ReturnedQty += line.Qty;

            posReturn.Items.Add(new PosReturnItem
            {
                PosItemId = originalLine.Id,
                Qty = line.Qty,
                RefundAmount = refundAmount,
                CommissionClawback = commissionClawback,
            });
        }

        posReturn.TotalRefund = posReturn.Items.Sum(i => i.RefundAmount);

        // Reflect whether the original sale is now fully or only partially returned.
        var fullyReturned = originalSale.Items.All(i => i.ReturnedQty >= i.Qty);
        originalSale.Status = fullyReturned ? PosStatus.Returned : PosStatus.PartiallyReturned;

        // The commission clawback always belongs to the cashier who originally made the sale, not
        // whoever is processing this return (a supervisor or a different shift cashier might be
        // the one standing at the register right now). If that original cashier's shift is still
        // open, ShiftController.Close will pick up this clawback itself when it closes (it queries
        // by the original sale's ShiftId - see there). But if that shift is already closed, its
        // CommissionEarned snapshot is final and Close will never run again for it, so patch it
        // here, immediately, or the clawback would silently never apply.
        var totalClawback = posReturn.Items.Sum(i => i.CommissionClawback); // zero or negative
        if (totalClawback != 0)
        {
            var originalShift = await _db.Shifts.FindAsync(originalSale.ShiftId);
            if (originalShift != null && originalShift.Status == ShiftStatus.Closed)
            {
                originalShift.CommissionEarned = (originalShift.CommissionEarned ?? 0) + totalClawback;
            }
        }

        _db.PosReturns.Add(posReturn);
        await _db.SaveChangesAsync();

        // Explicit response shape, not the raw entity - PosReturn.Shift/Cashier navigation
        // properties would otherwise leak Cashier.PasswordHash and risk the same circular-
        // reference crash the Pos endpoints had (Shift -> Returns -> PosReturn -> Shift -> ...).
        return Ok(new
        {
            posReturn.Id,
            posReturn.OriginalPosId,
            posReturn.CashierId,
            posReturn.ShiftId,
            posReturn.ReturnDate,
            posReturn.TotalRefund,
            posReturn.Reason,
            Items = posReturn.Items.Select(i => new
            {
                i.Id,
                i.PosItemId,
                i.Qty,
                i.RefundAmount,
                i.CommissionClawback,
            }),
        });
    }
}
