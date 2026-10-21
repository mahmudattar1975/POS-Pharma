using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dotnetpos.Migrations
{
    /// <inheritdoc />
    public partial class AddPosPaymentsAndBarcodeMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pos_payment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PosId = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_payment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pos_payment_pos_PosId",
                        column: x => x.PosId,
                        principalTable: "pos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_Code",
                table: "item",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_pos_payment_PosId",
                table: "pos_payment",
                column: "PosId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pos_payment");

            migrationBuilder.DropIndex(
                name: "IX_item_Code",
                table: "item");
        }
    }
}
