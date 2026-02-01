using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataImporter.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_CompositeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "future_bars_full",
                columns: table => new
                {
                    SymbolCode = table.Column<string>(type: "CHAR(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "DATE", nullable: false),
                    ExecutionDate = table.Column<DateTime>(type: "DATE", nullable: true),
                    IndexType = table.Column<string>(type: "CHAR(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    Open = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    High = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Low = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Close = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    VWAP = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    NumberOfTrade = table.Column<int>(type: "int", nullable: false),
                    RecordNo = table.Column<long>(type: "bigint", nullable: false),
                    ContractMonth = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_future_bars_full", x => new { x.SymbolCode, x.Timestamp });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "option_bars_full",
                columns: table => new
                {
                    SymbolCode = table.Column<string>(type: "CHAR(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Timestamp = table.Column<DateTime>(type: "DATETIME", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "DATE", nullable: false),
                    ExecutionDate = table.Column<DateTime>(type: "DATE", nullable: true),
                    UnderlyingCode = table.Column<string>(type: "CHAR(4)", maxLength: 4, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PutCallType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    Open = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    High = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Low = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Close = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false),
                    VWAP = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    NumberOfTrade = table.Column<int>(type: "int", nullable: false),
                    RecordNo = table.Column<long>(type: "bigint", nullable: false),
                    ContractMonth = table.Column<int>(type: "int", nullable: false),
                    ExercisePrice = table.Column<decimal>(type: "DECIMAL(12,4)", nullable: false),
                    CashFuturesType = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_option_bars_full", x => new { x.SymbolCode, x.Timestamp });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_future_bars_full_ContractMonth",
                table: "future_bars_full",
                column: "ContractMonth");

            migrationBuilder.CreateIndex(
                name: "IX_option_bars_full_ContractMonth_ExercisePrice_PutCallType",
                table: "option_bars_full",
                columns: new[] { "ContractMonth", "ExercisePrice", "PutCallType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "future_bars_full");

            migrationBuilder.DropTable(
                name: "option_bars_full");
        }
    }
}
