using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    public enum CommissionType
    {
        Flat = 1,     // fixed SAR amount per unit sold
        Percent = 2   // fraction of (qty * unit sale price), e.g. 0.12 = 12%
    }

    // The "commission item list" - a small set of items (promoted / slow-moving stock, etc.) that
    // earn the cashier a special commission to incentivize selling them quickly.
    //   Flat    -> commission = CommissionValue * qty
    //   Percent -> commission = CommissionValue * qty * unitPrice   (e.g. .12 * 1 * 16 = 1.92 SAR)
    [Table("commission_item")]
    public class CommissionItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Item))]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public CommissionType CommissionType { get; set; } = CommissionType.Flat;

        [Column(TypeName = "decimal(10,4)")]
        public decimal CommissionValue { get; set; }

        public DateTime ActiveFrom { get; set; } = DateTime.Today;
        public DateTime ActiveTo { get; set; } = DateTime.Today.AddMonths(1);

        public bool IsActive { get; set; } = true;
    }
}
