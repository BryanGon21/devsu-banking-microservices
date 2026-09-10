using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devsu.Accounts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMovementCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMovementCorrections",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MovementId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PreviousValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    PreviousBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NewValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NewOccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    NewBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMovementCorrections", x => x.Id);
                    table.CheckConstraint("CK_AccountMovementCorrections_NewBalance", "[NewBalance] >= 0");
                    table.CheckConstraint("CK_AccountMovementCorrections_NewType", "[NewType] IN ('Deposit', 'Withdrawal')");
                    table.CheckConstraint("CK_AccountMovementCorrections_NewValue", "([NewType] = 'Deposit' AND [NewValue] > 0) OR ([NewType] = 'Withdrawal' AND [NewValue] < 0)");
                    table.CheckConstraint("CK_AccountMovementCorrections_PreviousBalance", "[PreviousBalance] >= 0");
                    table.CheckConstraint("CK_AccountMovementCorrections_PreviousType", "[PreviousType] IN ('Deposit', 'Withdrawal')");
                    table.CheckConstraint("CK_AccountMovementCorrections_PreviousValue", "([PreviousType] = 'Deposit' AND [PreviousValue] > 0) OR ([PreviousType] = 'Withdrawal' AND [PreviousValue] < 0)");
                    table.ForeignKey(
                        name: "FK_AccountMovementCorrections_AccountMovements_MovementId",
                        column: x => x.MovementId,
                        principalSchema: "accounts",
                        principalTable: "AccountMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovementCorrections_MovementId_CorrectedAtUtc",
                schema: "accounts",
                table: "AccountMovementCorrections",
                columns: new[] { "MovementId", "CorrectedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMovementCorrections",
                schema: "accounts");
        }
    }
}
