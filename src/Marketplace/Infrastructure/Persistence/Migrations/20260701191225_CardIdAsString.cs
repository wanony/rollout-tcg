using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTrading.Marketplace.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardIdAsString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CardId",
                table: "Listings",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CardId",
                table: "Listings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
