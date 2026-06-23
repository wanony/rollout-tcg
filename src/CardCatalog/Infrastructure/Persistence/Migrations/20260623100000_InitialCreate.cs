using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCGTrading.CardCatalog.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cards",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Set = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Rarity = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cards", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Cards");
    }
}
