using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTrading.Alerts.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlertTriggerSourceEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertTriggers_AlertId_MarketPriceUsd",
                table: "AlertTriggers");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                table: "AlertTriggers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AlertTriggers_AlertId_SourceEventId",
                table: "AlertTriggers",
                columns: new[] { "AlertId", "SourceEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlertTriggers_AlertId_SourceEventId",
                table: "AlertTriggers");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "AlertTriggers");

            migrationBuilder.CreateIndex(
                name: "IX_AlertTriggers_AlertId_MarketPriceUsd",
                table: "AlertTriggers",
                columns: new[] { "AlertId", "MarketPriceUsd" },
                unique: true);
        }
    }
}
