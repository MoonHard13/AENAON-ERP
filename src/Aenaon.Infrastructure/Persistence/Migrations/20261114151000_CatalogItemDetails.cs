using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Ένατη, μη καταστροφική migration: προαιρετικό barcode και αναλυτική περιγραφή ειδών.</summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261114151000_CatalogItemDetails")]
public partial class CatalogItemDetails : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Και οι δύο στήλες είναι nullable: οι παλιές καρτέλες διατηρούν αμετάβλητα δεδομένα.
        migrationBuilder.AddColumn<string>(
            name: "Barcode", schema: "dbo", table: "CatalogItems",
            type: "nvarchar(40)", maxLength: 40, nullable: true,
            collation: "Greek_CI_AS");

        migrationBuilder.AddColumn<string>(
            name: "LongDescription", schema: "dbo", table: "CatalogItems",
            type: "nvarchar(2000)", maxLength: 2000, nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CatalogItems_Barcode", schema: "dbo", table: "CatalogItems",
            sql: "[Barcode] IS NULL OR (LEN([Barcode]) BETWEEN 1 AND 40 " +
                "AND DATALENGTH([Barcode]) = DATALENGTH(LTRIM(RTRIM([Barcode]))) " +
                "AND PATINDEX('%[^-0-9A-Z._]%', [Barcode] COLLATE Latin1_General_100_BIN2) = 0)");

        // Ο μοναδικός filtered index επιτρέπει απεριόριστα NULL αλλά όχι διπλά barcodes ανά Tenant.
        migrationBuilder.CreateIndex(
            name: "IX_CatalogItems_TenantId_Barcode", schema: "dbo", table: "CatalogItems",
            columns: new[] { "TenantId", "Barcode" }, unique: true,
            filter: "[Barcode] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CatalogItems_TenantId_Barcode", schema: "dbo", table: "CatalogItems");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CatalogItems_Barcode", schema: "dbo", table: "CatalogItems");
        migrationBuilder.DropColumn(name: "Barcode", schema: "dbo", table: "CatalogItems");
        migrationBuilder.DropColumn(name: "LongDescription", schema: "dbo", table: "CatalogItems");
    }
}
