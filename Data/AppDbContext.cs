using PharmaApp.Entity.Models;
using Microsoft.EntityFrameworkCore;

namespace PharmaApp.DataBase
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Pos> Pos { get; set; }
        public DbSet<PosItem> PosItems { get; set; }
        public DbSet<Cashier> Cashiers { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<CommissionItem> CommissionItems { get; set; }
        public DbSet<PosReturn> PosReturns { get; set; }
        public DbSet<PosReturnItem> PosReturnItems { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<PosPayment> PosPayments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Financial/audit records must never silently cascade-delete - e.g. deleting a
            // Cashier should not wipe out their entire sales history, and it also happens to be
            // what SQL Server requires here anyway (multiple cascade paths reach Cashier/Pos
            // through pos_return + pos_return_item, which it refuses to create by default).
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // Code is now the barcode-matching key PosController.Create resolves/auto-registers
            // items by - must be unique or two different items could silently merge into one
            // commission/sales history.
            // Filtered (NULLs excluded) because SQL Server's plain unique index only tolerates one
            // NULL total, and Code is optional for items never sold through PosController.Create.
            modelBuilder.Entity<Item>().HasIndex(i => i.Code).IsUnique().HasFilter("[Code] IS NOT NULL");
        }
    }
}
