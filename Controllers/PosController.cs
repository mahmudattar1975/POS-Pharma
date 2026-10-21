using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaApp.DataBase;
using PharmaApp.Entity.Models;

namespace PharmaApp.Controllers;

[Authorize]
public class PosController : Controller
{
    private readonly AppDbContext _db;

    public PosController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentCashierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Explicit response shapes, never raw EF entities - returning entities directly leaks
    // sensitive fields reachable through navigation properties (e.g. Pos.Cashier.PasswordHash)
    // and, with Pos<->Shift<->Sales now linked both ways, causes a circular-reference JSON
    // serialization crash (Pos.Shift.Sales contains the same Pos again).
    public class PosItemResponse
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; } = default!;
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineTotal { get; set; }
        public decimal ReturnedQty { get; set; }
        public decimal CommissionAmount { get; set; }
    }

    public class PosPaymentResponse
    {
        public PosPaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
    }

    public class PosResponse
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = default!;
        public DateTime SaleDate { get; set; }
        public int CashierId { get; set; }
        public string? CashierName { get; set; }
        public int ShiftId { get; set; }
        public string? CustomerName { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public PosPaymentMethod PaymentMethod { get; set; }
        public PosStatus Status { get; set; }
        public List<PosItemResponse> Items { get; set; } = new();
        public List<PosPaymentResponse> Payments { get; set; } = new();
    }

    private static PosResponse ToResponse(Pos p) => new()
    {
        Id = p.Id,
        InvoiceNo = p.InvoiceNo,
        SaleDate = p.SaleDate,
        CashierId = p.CashierId,
        CashierName = p.CashierName,
        ShiftId = p.ShiftId,
        CustomerName = p.CustomerName,
        SubTotal = p.SubTotal,
        VatAmount = p.VatAmount,
        TotalAmount = p.TotalAmount,
        PaymentMethod = p.PaymentMethod,
        Status = p.Status,
        Items = p.Items.Select(i => new PosItemResponse
        {
            Id = i.Id,
            ItemId = i.ItemId,
            ItemName = i.ItemName,
            Qty = i.Qty,
            UnitPrice = i.UnitPrice,
            VatRate = i.VatRate,
            LineTotal = i.LineTotal,
            ReturnedQty = i.ReturnedQty,
            CommissionAmount = i.CommissionAmount,
        }).ToList(),
        Payments = p.Payments.Select(pay => new PosPaymentResponse
        {
            Method = pay.Method,
            Amount = pay.Amount,
        }).ToList(),
    };

    // GET /Pos
    public async Task<IActionResult> Index()
    {
        var sales = await _db.Pos
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .OrderByDescending(p => p.SaleDate)
            .ToListAsync();
        return Ok(sales.Select(ToResponse));
    }

    // GET /Pos/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var sale = await _db.Pos
            .Include(p => p.Items)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (sale == null) return NotFound();
        return Ok(ToResponse(sale));
    }

    // The React POS screen already runs its own full pricing pipeline (offers, customer-tier
    // discounts, manual line discounts, FEFO batch pricing) before checkout - this endpoint's job
    // is to mirror that already-decided sale for commission/shift/cash-drawer tracking, not to
    // recompute pricing a second time from a separate catalog. So UnitPrice/VatRate/LineTotal here
    // are what the cashier's screen actually charged (post all discounts), trusted as given - the
    // one thing still computed authoritatively server-side is the commission itself (the rate is
    // never client-controlled, looked up from CommissionItem by the resolved ItemId).
    public class CreatePosLineRequest
    {
        public string Barcode { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Qty { get; set; } = 1;
        public decimal UnitPrice { get; set; } // VAT-exclusive, net of any discount already applied
        public decimal VatRate { get; set; }
        public decimal LineTotal { get; set; } // VAT-inclusive total actually charged for this line
    }

    public class CreatePosPaymentRequest
    {
        public PosPaymentMethod Method { get; set; }
        public decimal Amount { get; set; }
    }

    public class CreatePosRequest
    {
        public string? InvoiceNo { get; set; } // the real invoice number the frontend already generated (SIM-/STD-), kept in sync for reconciliation
        public string? CustomerName { get; set; }
        public List<CreatePosPaymentRequest> Payments { get; set; } = new();
        public List<CreatePosLineRequest> Items { get; set; } = new();
    }

    // POST /Pos/Create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePosRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("A POS sale needs at least one item.");
        if (request.Payments == null || request.Payments.Count == 0)
            return BadRequest("A POS sale needs at least one payment.");

        var cashier = await _db.Cashiers.FindAsync(CurrentCashierId);
        if (cashier == null) return Unauthorized();

        var openShift = await _db.Shifts
            .FirstOrDefaultAsync(s => s.CashierId == CurrentCashierId && s.Status == ShiftStatus.Open);
        if (openShift == null)
            return BadRequest("No open shift/drawer for this cashier. Open the drawer before making a sale.");

        var today = DateTime.Today;
        var sale = new Pos
        {
            InvoiceNo = string.IsNullOrWhiteSpace(request.InvoiceNo) ? "POS-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") : request.InvoiceNo,
            SaleDate = DateTime.Now,
            CreatedOn = DateTime.Now,
            CashierId = cashier.Id,
            CashierName = cashier.FullName,
            ShiftId = openShift.Id,
            CustomerName = request.CustomerName,
            // Display-only "representative" method for a split sale, same convention the receipt
            // itself uses for ZATCA's single-payment-means field - the real breakdown is Payments.
            PaymentMethod = request.Payments.OrderByDescending(p => p.Amount).First().Method,
            Status = PosStatus.Completed,
        };

        foreach (var line in request.Items)
        {
            if (line.Qty <= 0) return BadRequest($"Item '{line.Name}': quantity must be greater than zero.");
            if (string.IsNullOrWhiteSpace(line.Barcode)) return BadRequest($"Item '{line.Name}' has no barcode to match against the commission list.");

            // First time this barcode is seen, register it - the React catalog is the real source
            // of truth for the item master, dotnet-pos only needs a stable Id per barcode to match
            // CommissionItem against (see Item.cs's comment on why not matching by name).
            var item = await _db.Items.FirstOrDefaultAsync(i => i.Code == line.Barcode);
            if (item == null)
            {
                item = new Item { Code = line.Barcode, Name = line.Name, SalePrice = line.UnitPrice, VatRate = line.VatRate, IsActive = true };
                _db.Items.Add(item);
            }

            var commission = await ComputeCommission(item.Id, line.Qty, line.UnitPrice, today);

            sale.Items.Add(new PosItem
            {
                Item = item,
                ItemName = line.Name,
                Qty = line.Qty,
                UnitPrice = line.UnitPrice,
                VatRate = line.VatRate,
                LineTotal = line.LineTotal,
                CommissionAmount = commission,
            });
        }

        foreach (var payment in request.Payments)
        {
            if (payment.Amount <= 0) return BadRequest("Each payment must have an amount greater than zero.");
            sale.Payments.Add(new PosPayment { Method = payment.Method, Amount = payment.Amount });
        }

        sale.SubTotal = sale.Items.Sum(i => i.Qty * i.UnitPrice);
        sale.VatAmount = sale.Items.Sum(i => i.Qty * i.UnitPrice * i.VatRate);
        sale.TotalAmount = sale.Items.Sum(i => i.LineTotal);

        _db.Pos.Add(sale);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Details), new { id = sale.Id }, ToResponse(sale));
    }

    // POST /Pos/Void/5
    [HttpPost]
    public async Task<IActionResult> Void(int id)
    {
        var sale = await _db.Pos.FindAsync(id);
        if (sale == null) return NotFound();

        sale.Status = PosStatus.Voided;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // Shared commission lookup. The amount is computed once here and stored on the PosItem as a
    // snapshot - a later edit to CommissionItem's rate must never retroactively change what a
    // past month's commission report shows.
    private async Task<decimal> ComputeCommission(int itemId, decimal qty, decimal unitPrice, DateTime onDate)
    {
        var rate = await _db.CommissionItems
            .Where(c => c.ItemId == itemId && c.IsActive && c.ActiveFrom <= onDate && onDate <= c.ActiveTo)
            .FirstOrDefaultAsync();

        if (rate == null) return 0;

        return rate.CommissionType == CommissionType.Flat
            ? rate.CommissionValue * qty
            : rate.CommissionValue * qty * unitPrice;
    }
}
