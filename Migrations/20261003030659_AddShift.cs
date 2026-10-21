using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dotnetpos.Migrations
{
    /// <inheritdoc />
    public partial class AddShift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "pos_return",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "pos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "shift",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashierId = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClosingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CommissionEarned = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TotalSales = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalReturns = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shift_cashier_CashierId",
                        column: x => x.CashierId,
                        principalTable: "cashier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pos_return_ShiftId",
                table: "pos_return",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_ShiftId",
                table: "pos",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_shift_CashierId",
                table: "shift",
                column: "CashierId");

            migrationBuilder.AddForeignKey(
                name: "FK_pos_shift_ShiftId",
                table: "pos",
                column: "ShiftId",
                principalTable: "shift",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pos_return_shift_ShiftId",
                table: "pos_return",
                column: "ShiftId",
                principalTable: "shift",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pos_shift_ShiftId",
                table: "pos");

            migrationBuilder.DropForeignKey(
                name: "FK_pos_return_shift_ShiftId",
                table: "pos_return");

            migrationBuilder.DropTable(
                name: "shift");

            migrationBuilder.DropIndex(
                name: "IX_pos_return_ShiftId",
                table: "pos_return");

            migrationBuilder.DropIndex(
                name: "IX_pos_ShiftId",
                table: "pos");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "pos_return");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "pos");
        }
    }
}
