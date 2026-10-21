using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    // Minimal item catalog - PosItem snapshots name/price at time of sale (so historical receipts
    // never change), but needs a stable ItemId to unambiguously match against CommissionItem.
    // Matching commission eligibility by free-text name alone ("Augmentin" vs "Augmentin BD" vs
    // "augmentin") is too fragile when real money (commission payout) depends on it.
    [Table("item")]
    public class Item
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? Code { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [Column(TypeName = "decimal(18,4)")]
        public decimal SalePrice { get; set; }

        // 0 for SFDA-registered medicines (zero-rated), 0.15 for standard-rated items - set per
        // item, never a single invoice-wide rate.
        [Column(TypeName = "decimal(5,4)")]
        public decimal VatRate { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
