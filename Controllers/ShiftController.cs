using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

namespace PharmaApp.Controllers;

[Authorize]
public class ShiftController : Controller
{
    private readonly AppDbContext _db;

    public ShiftController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentCashierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Explicit response shape, not the raw entity - Shift.Cashier/Sales/Returns navigation
    // properties would otherwise risk leaking Cashier.PasswordHash (if ever eager-loaded) or a
    // circular-reference crash (Shift -> Sales -> Pos -> Shift -> ...), same issue the Pos
    // endpoints had. Relying on "the query just didn't Include it this time" is fragile.
    public class ShiftResponse
    {
        public int Id { get; set; }
        public int CashierId { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningFloat { get; set; }
        public decimal? ClosingAmount { get; set; }
        public ShiftStatus Status { get; set; }
        public decimal? CommissionEarned { get; set; }
        public decimal? TotalSales { get; set; }
        public decimal? TotalReturns { get; set; }
    }

    private static ShiftResponse ToResponse(Shift s) => new()
    {
        Id = s.Id,
        CashierId = s.CashierId,
        OpenedAt = s.OpenedAt,
        ClosedAt = s.ClosedAt,
        OpeningFloat = s.OpeningFloat,
        ClosingAmount = s.ClosingAmount,
        Status = s.Status,
        CommissionEarned = s.CommissionEarned,
        TotalSales = s.TotalSales,
        TotalReturns = s.TotalReturns,
    };

    // GET /Shift/Current - lets the frontend check "is there an open shift" on load/refresh,
    // e.g. to decide whether to show the Open Drawer prompt or go straight to the POS screen.
    [HttpGet]
    public async Task<IActionResult> Current()
    {
        var shift = await _db.Shifts
            .FirstOrDefaultAsync(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Open);
        return Ok(shift == null ? null : ToResponse(shift));
    }

    public class OpenShiftRequest
    {
        public decimal OpeningFloat { get; set; }
    }

    // POST /Shift/Open
    [HttpPost]
    public async Task<IActionResult> Open([FromBody] OpenShiftRequest request)
    {
        var alreadyOpen = await _db.Shifts
            .AnyAsync(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Open);
        if (alreadyOpen)
            return BadRequest("You already have an open shift. Close it before opening a new one.");

        var shift = new Shift
        {
            CashierId = CurrentCashierId,
            OpenedAt = DateTime.Now,
            OpeningFloat = request.OpeningFloat,
            Status = ShiftStatus.Open,
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(shift));
    }

    public class CloseShiftRequest
    {
        public decimal? ClosingAmount { get; set; }
    }

    // POST /Shift/Close - computes totals and commission for everything that happened during
    // this shift (sales minus voided ones, plus clawbacks from any returns processed in the same
    // shift), then locks the shift closed. All amounts stored as a snapshot on the Shift row.
    [HttpPost]
    public async Task<IActionResult> Close([FromBody] CloseShiftRequest request)
    {
        var shift = await _db.Shifts
            .Include(s => s.Sales).ThenInclude(p => p.Items)
            .Include(s => s.Sales).ThenInclude(p => p.Payments)
            .Include(s => s.Returns).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Open);

        if (shift == null) return BadRequest("No open shift to close.");

        var activeSales = shift.Sales.Where(p => p.Status != PosStatus.Voided).ToList();

        var totalSales = activeSales.Sum(p => p.TotalAmount);
        // Cash impact of returns belongs to whichever drawer actually paid the refund out, so this
        // stays keyed off shift.Returns (PosReturn.ShiftId = the processing cashier's shift).
        var totalReturns = shift.Returns.Sum(r => r.TotalRefund);
        var commissionFromSales = activeSales.SelectMany(p => p.Items).Sum(i => i.CommissionAmount);
        // Commission clawback belongs to whoever originally earned it - the cashier who made the
        // sale - not whoever happens to process the return. So this is keyed off the ORIGINAL
        // sale's ShiftId, not shift.Returns (which is the processing cashier's shift and may be a
        // different person/shift entirely). See PosReturnController.Create for the other half: if
        // this shift is already closed by the time the return comes in, the clawback is applied
        // immediately there instead, since this query won't run again for a closed shift.
        var commissionClawback = await _db.PosReturns
            .Where(r => r.OriginalPos!.ShiftId == shift.Id)
            .SelectMany(r => r.Items)
            .SumAsync(i => i.CommissionClawback); // already negative

        // Summed from each sale's actual Payments breakdown, not Pos.PaymentMethod/TotalAmount -
        // a split sale (part cash, part card) must only count its cash leg toward the drawer, not
        // the whole invoice total just because cash happened to be the "representative" method.
        var expectedCash = shift.OpeningFloat +
            activeSales.SelectMany(p => p.Payments).Where(pay => pay.Method == PosPaymentMethod.Cash).Sum(pay => pay.Amount);

        shift.TotalSales = totalSales;
        shift.TotalReturns = totalReturns;
        shift.CommissionEarned = commissionFromSales + commissionClawback;
        shift.ClosingAmount = request.ClosingAmount;
        shift.ClosedAt = DateTime.Now;
        shift.Status = ShiftStatus.Closed;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            shift.Id,
            shift.OpenedAt,
            shift.ClosedAt,
            shift.OpeningFloat,
            shift.ClosingAmount,
            shift.TotalSales,
            shift.TotalReturns,
            shift.CommissionEarned,
            ExpectedCash = expectedCash,
            CashVariance = shift.ClosingAmount.HasValue ? shift.ClosingAmount.Value - expectedCash : (decimal?)null,
        });
    }

    // GET /Shift/History?from=...&to=... - closed shifts for the logged-in cashier, for their own
    // reference (a supervisor-wide view across all cashiers belongs in the Report controller).
    [HttpGet]
    public async Task<IActionResult> History(DateTime? from, DateTime? to)
    {
        var start = from?.Date ?? DateTime.Today.AddMonths(-1);
        var end = to?.Date.AddDays(1) ?? DateTime.Today.AddDays(1);

        var shifts = await _db.Shifts
            .Where(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Closed
                     && s.OpenedAt >= start && s.OpenedAt < end)
            .OrderByDescending(s => s.OpenedAt)
            .ToListAsync();

        return Ok(shifts.Select(ToResponse));
    }
}
