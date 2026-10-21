using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    [Table("pos_return")]
    public class PosReturn
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(OriginalPos))]
        public int OriginalPosId { get; set; }
        public Pos? OriginalPos { get; set; }

        [ForeignKey(nameof(Cashier))]
        public int CashierId { get; set; }
        public Cashier? Cashier { get; set; }

        [ForeignKey(nameof(Shift))]
        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }

        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRefund { get; set; }

        [MaxLength(300)]
        public string? Reason { get; set; }

        public ICollection<PosReturnItem> Items { get; set; } = new List<PosReturnItem>();
    }
}
