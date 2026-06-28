using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTrading.Portfolio.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardIdToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CardId",
                table: "CollectionItems",
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
                table: "CollectionItems",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
