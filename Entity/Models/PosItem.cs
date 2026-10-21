using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    [Table("pos_item")]
    public class PosItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Pos))]
        public int PosId { get; set; }
        public Pos? Pos { get; set; }

        [ForeignKey(nameof(Item))]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        // Snapshot of the item's name at time of sale - the printed/historical receipt never
        // changes even if the catalog item is later renamed.
        [Required, MaxLength(150)]
        public string ItemName { get; set; } = default!;

        [Column(TypeName = "decimal(18,3)")]
        public decimal Qty { get; set; } = 1;

        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal VatRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        // How much of Qty has been returned so far via PosReturnItem rows against this line -
        // caps future returns at (Qty - ReturnedQty), same running-total pattern used for the
        // commission clawback.
        [Column(TypeName = "decimal(18,3)")]
        public decimal ReturnedQty { get; set; } = 0;

        // Commission earned on this line at time of sale, computed from whatever CommissionItem
        // rate was active then (Flat: value * qty, Percent: value * qty * unitPrice). Stored as a
        // snapshot, not recomputed later, so a later change to CommissionItem's rate never
        // retroactively changes a historical month's commission report.
        [Column(TypeName = "decimal(18,4)")]
        public decimal CommissionAmount { get; set; } = 0;
    }
}
