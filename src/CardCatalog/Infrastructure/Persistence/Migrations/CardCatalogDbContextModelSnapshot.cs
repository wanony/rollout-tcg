using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using TCGTrading.CardCatalog.Infrastructure.Persistence;

#nullable disable

namespace TCGTrading.CardCatalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(CardCatalogDbContext))]
partial class CardCatalogDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

        modelBuilder.Entity("TCGTrading.CardCatalog.Domain.Entities.Card", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<int>("Rarity")
                .HasColumnType("integer");

            b.Property<string>("Set")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<string>("Text")
                .IsRequired()
                .HasMaxLength(2000)
                .HasColumnType("character varying(2000)");

            b.Property<string>("Type")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.HasKey("Id");

            b.ToTable("Cards");
        });
#pragma warning restore 612, 618
    }
}
