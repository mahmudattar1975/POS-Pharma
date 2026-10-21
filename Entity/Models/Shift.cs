using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    public enum ShiftStatus
    {
        Open = 1,
        Closed = 2
    }

    // "Open drawer" at the start of a shift, "close drawer" at the end. Every Pos sale and
    // PosReturn made while a shift is open is tagged with its ShiftId, so commission (and
    // sales/returns totals) can be computed exactly for that shift at close time, not just by
    // calendar date - matches how a real register/drawer session works.
    [Table("shift")]
    public class Shift
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Cashier))]
        public int CashierId { get; set; }
        public Cashier? Cashier { get; set; }

        public DateTime OpenedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningFloat { get; set; }

        // Cash actually counted at close time, entered by the cashier/supervisor - not computed,
        // so it can be compared against what the system expects (OpeningFloat + cash sales).
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ClosingAmount { get; set; }

        public ShiftStatus Status { get; set; } = ShiftStatus.Open;

        // Computed once at close time from this shift's Pos/PosReturn rows and stored as a
        // snapshot - a later change to a CommissionItem rate must never retroactively change a
        // past shift's payout, same reasoning as PosItem.CommissionAmount.
        [Column(TypeName = "decimal(18,4)")]
        public decimal? CommissionEarned { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalSales { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalReturns { get; set; }

        public ICollection<Pos> Sales { get; set; } = new List<Pos>();
        public ICollection<PosReturn> Returns { get; set; } = new List<PosReturn>();
    }
}
