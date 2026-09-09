using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Devsu.Accounts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountMovements",
                schema: "accounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestFingerprint = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", precision: 7, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMovements", x => x.Id);
                    table.CheckConstraint("CK_AccountMovements_Balance", "[Balance] >= 0");
                    table.CheckConstraint("CK_AccountMovements_Type", "[Type] IN ('Deposit', 'Withdrawal')");
                    table.CheckConstraint("CK_AccountMovements_Value", "([Type] = 'Deposit' AND [Value] > 0) OR ([Type] = 'Withdrawal' AND [Value] < 0)");
                    table.ForeignKey(
                        name: "FK_AccountMovements_Accounts_AccountNumber",
                        column: x => x.AccountNumber,
                        principalSchema: "accounts",
                        principalTable: "Accounts",
                        principalColumn: "Number",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountMovements_AccountNumber_OccurredAtUtc_Id",
                schema: "accounts",
                table: "AccountMovements",
                columns: new[] { "AccountNumber", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_AccountMovements_IdempotencyKey",
                schema: "accounts",
                table: "AccountMovements",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMovements",
                schema: "accounts");
        }
    }
}
