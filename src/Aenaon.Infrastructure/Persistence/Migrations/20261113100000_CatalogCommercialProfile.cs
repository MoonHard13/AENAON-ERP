using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Έβδομη migration: μονάδα μέτρησης και εμπορικός/λογιστικός τύπος είδους.</summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261113100000_CatalogCommercialProfile")]
public partial class CatalogCommercialProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Τα προεπιλεγμένα πεδία διατηρούν όλα τα υφιστάμενα προϊόντα ως τεμάχια/εμπορεύματα.
        migrationBuilder.AddColumn<int>(
            name: "UnitOfMeasure", schema: "dbo", table: "CatalogItems",
            type: "int", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<int>(
            name: "AccountingType", schema: "dbo", table: "CatalogItems",
            type: "int", nullable: false, defaultValue: 1);

        // Οι προϋπάρχουσες υπηρεσίες μεταφέρονται σε Υπηρεσία/Μονάδα Υπηρεσίας,
        // πριν προστεθούν οι νέοι περιορισμοί ακεραιότητας.
        migrationBuilder.Sql("""
            UPDATE dbo.CatalogItems
            SET UnitOfMeasure = 8, AccountingType = 5
            WHERE Kind = 2;
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CatalogItems_UnitOfMeasure", schema: "dbo",
            table: "CatalogItems", sql: "[UnitOfMeasure] BETWEEN 1 AND 8");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CatalogItems_AccountingType", schema: "dbo",
            table: "CatalogItems", sql: "[AccountingType] BETWEEN 1 AND 6");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CatalogItems_CommercialProfile", schema: "dbo",
            table: "CatalogItems",
            sql: "([Kind] = 2 AND [AccountingType] = 5 AND [UnitOfMeasure] IN (7, 8)) " +
                "OR ([Kind] = 1 AND [AccountingType] <> 5 AND [UnitOfMeasure] <> 8)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Το rollback του schema χάνει τις εμπορικές ταξινομήσεις: απαιτεί backup.
        migrationBuilder.DropCheckConstraint(
            name: "CK_CatalogItems_CommercialProfile", schema: "dbo",
            table: "CatalogItems");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CatalogItems_AccountingType", schema: "dbo",
            table: "CatalogItems");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CatalogItems_UnitOfMeasure", schema: "dbo",
            table: "CatalogItems");
        migrationBuilder.DropColumn(
            name: "AccountingType", schema: "dbo", table: "CatalogItems");
        migrationBuilder.DropColumn(
            name: "UnitOfMeasure", schema: "dbo", table: "CatalogItems");
    }
}
