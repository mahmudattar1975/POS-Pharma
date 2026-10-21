using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaApp.Entity.Models
{
    [Table("cashier")]
    public class Cashier
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = default!;

        [Required]
        public string PasswordHash { get; set; } = default!;

        [Required, MaxLength(100)]
        public string FullName { get; set; } = default!;

        public bool IsActive { get; set; } = true;

        // Gates CashierController (creating other accounts) and CommissionItemController
        // (editing the commission list) - a regular cashier must not be able to grant themselves
        // commission or create new logins.
        public bool IsAdmin { get; set; } = false;

        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
