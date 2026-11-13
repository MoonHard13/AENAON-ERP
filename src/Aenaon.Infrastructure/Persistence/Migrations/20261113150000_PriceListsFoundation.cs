using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Όγδοη migration: ανεξάρτητοι τιμοκατάλογοι και tenant-scoped εμπορικές τιμές.</summary>
public partial class PriceListsFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ο σύνθετος περιορισμός εγγυάται ότι επιλέγεται κατάστημα της ίδιας επιχείρησης.
        migrationBuilder.AddUniqueConstraint(
            name: "AK_Stores_TenantId_Id", schema: "dbo", table: "Stores",
            columns: new[] { "TenantId", "Id" });

        migrationBuilder.CreateTable(
            name: "PriceLists",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30,
                    nullable: false, collation: "Greek_CI_AS"),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Channel = table.Column<int>(type: "int", nullable: false),
                Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                PricesIncludeVat = table.Column<bool>(type: "bit", nullable: false),
                ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceLists", x => x.Id);
                table.UniqueConstraint("AK_PriceLists_TenantId_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_PriceLists_Id",
                    "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceLists_TenantId",
                    "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceLists_StoreId",
                    "[StoreId] IS NULL OR [StoreId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceLists_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_PriceLists_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.CheckConstraint("CK_PriceLists_Currency", "[Currency] = 'EUR'");
                table.CheckConstraint("CK_PriceLists_Channel", "[Channel] BETWEEN 0 AND 5");
                table.CheckConstraint("CK_PriceLists_Period",
                    "[ValidUntil] IS NULL OR [ValidUntil] >= [ValidFrom]");
                table.ForeignKey(
                    name: "FK_PriceLists_Tenants_TenantId",
                    column: x => x.TenantId, principalSchema: "dbo", principalTable: "Tenants",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PriceLists_Stores_TenantId_StoreId",
                    columns: x => new { x.TenantId, x.StoreId },
                    principalSchema: "dbo", principalTable: "Stores",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PriceListItems",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18,
                    scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceListItems", x => x.Id);
                table.CheckConstraint("CK_PriceListItems_Id",
                    "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceListItems_TenantId",
                    "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceListItems_UnitPrice",
                    "[UnitPrice] >= 0 AND [UnitPrice] <= 999999999999.9999");
                table.ForeignKey(
                    name: "FK_PriceListItems_PriceLists_TenantId_PriceListId",
                    columns: x => new { x.TenantId, x.PriceListId },
                    principalSchema: "dbo", principalTable: "PriceLists",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PriceListItems_CatalogItems_TenantId_CatalogItemId",
                    columns: x => new { x.TenantId, x.CatalogItemId },
                    principalSchema: "dbo", principalTable: "CatalogItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PriceLists_TenantId_Code", schema: "dbo", table: "PriceLists",
            columns: new[] { "TenantId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PriceLists_TenantId_StoreId_Channel_IsActive", schema: "dbo",
            table: "PriceLists",
            columns: new[] { "TenantId", "StoreId", "Channel", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_PriceListItems_TenantId_PriceListId_CatalogItemId", schema: "dbo",
            table: "PriceListItems",
            columns: new[] { "TenantId", "PriceListId", "CatalogItemId" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PriceListItems_TenantId_CatalogItemId", schema: "dbo",
            table: "PriceListItems", columns: new[] { "TenantId", "CatalogItemId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Προειδοποίηση: η αναίρεση διαγράφει όλα τα δεδομένα τιμοκαταλόγων.
        migrationBuilder.DropTable(name: "PriceListItems", schema: "dbo");
        migrationBuilder.DropTable(name: "PriceLists", schema: "dbo");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_Stores_TenantId_Id", schema: "dbo", table: "Stores");
    }
}
