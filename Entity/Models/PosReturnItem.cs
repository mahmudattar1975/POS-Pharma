using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    [Table("pos_return_item")]
    public class PosReturnItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(PosReturn))]
        public int PosReturnId { get; set; }
        public PosReturn? PosReturn { get; set; }

        [ForeignKey(nameof(PosItem))]
        public int PosItemId { get; set; }
        public PosItem? PosItem { get; set; }

        [Column(TypeName = "decimal(18,3)")]
        public decimal Qty { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; }

        // Negative amount - the commission being clawed back from the cashier for this returned
        // qty, proportional to what was earned on the original line:
        //   clawback = -(originalLine.CommissionAmount * (returnedQty / originalLine.Qty))
        [Column(TypeName = "decimal(18,4)")]
        public decimal CommissionClawback { get; set; } = 0;
    }
}
