using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    public enum PosStatus
    {
        Completed = 1,
        Returned = 2,
        PartiallyReturned = 3,
        Voided = 4
    }

    public enum PosPaymentMethod
    {
        Cash = 1,
        Card = 2,
        Insurance = 3,
        Points = 4
    }

    [Table("pos")]
    public class Pos
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string InvoiceNo { get; set; } = default!;

        public DateTime SaleDate { get; set; } = DateTime.Now;

        [ForeignKey(nameof(Cashier))]
        public int CashierId { get; set; }
        public Cashier? Cashier { get; set; }

        [ForeignKey(nameof(Shift))]
        public int ShiftId { get; set; }
        public Shift? Shift { get; set; }

        // Snapshot of the cashier's name at time of sale - reports stay accurate even if a
        // cashier's FullName is edited later, same reasoning as PosItem.ItemName below.
        [MaxLength(100)]
        public string? CashierName { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public PosPaymentMethod PaymentMethod { get; set; } = PosPaymentMethod.Cash;

        public PosStatus Status { get; set; } = PosStatus.Completed;

        public DateTime CreatedOn { get; set; } = DateTime.Now;

        public ICollection<PosItem> Items { get; set; } = new List<PosItem>();
        public ICollection<PosPayment> Payments { get; set; } = new List<PosPayment>();
    }
}
