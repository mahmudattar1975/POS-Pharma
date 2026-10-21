using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    // One row per tender leg on a sale - a split payment (e.g. half cash, half card) is several of
    // these against the same Pos, not representable by Pos.PaymentMethod alone (that field is kept
    // only as a display-convenience "primary method", same thing the POS receipt itself picks as
    // the representative ZATCA payment means for a split sale). ShiftController.Close sums the Cash
    // rows here for the drawer's expected-cash reconciliation, not Pos.TotalAmount.
    [Table("pos_payment")]
    public class PosPayment
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Pos))]
        public int PosId { get; set; }
        public Pos? Pos { get; set; }

        public PosPaymentMethod Method { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
    }
}
