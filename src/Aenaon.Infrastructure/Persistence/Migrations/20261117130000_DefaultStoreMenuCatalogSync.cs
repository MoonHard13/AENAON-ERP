using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>
/// Προσθετική migration για ασφαλή σύνδεση ERP κατηγοριών και προεπιλεγμένου μενού ανά Store.
/// Απαιτεί ξεχωριστή έγκριση πριν από κάθε εφαρμογή, ακόμη και στη Development.
/// </summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261117130000_DefaultStoreMenuCatalogSync")]
public partial class DefaultStoreMenuCatalogSync : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CatalogCategoryId",
            schema: "dbo",
            table: "MenuDraftCategories",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftCategories_TenantId_CatalogCategoryId",
            schema: "dbo",
            table: "MenuDraftCategories",
            columns: new[] { "TenantId", "CatalogCategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftCategories_TenantId_MenuId_CatalogCategoryId",
            schema: "dbo",
            table: "MenuDraftCategories",
            columns: new[] { "TenantId", "MenuId", "CatalogCategoryId" },
            unique: true,
            filter: "[CatalogCategoryId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_MenuDraftCategories_CatalogCategories_TenantId_CatalogCategoryId",
            schema: "dbo",
            table: "MenuDraftCategories",
            columns: new[] { "TenantId", "CatalogCategoryId" },
            principalSchema: "dbo",
            principalTable: "CatalogCategories",
            principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "StoreDefaultMenus",
            schema: "dbo",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreDefaultMenus",
                    x => new { x.TenantId, x.StoreId });
                table.ForeignKey(
                    name: "FK_StoreDefaultMenus_Stores_TenantId_StoreId",
                    columns: x => new { x.TenantId, x.StoreId },
                    principalSchema: "dbo",
                    principalTable: "Stores",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StoreDefaultMenus_RestaurantMenus_TenantId_MenuId",
                    columns: x => new { x.TenantId, x.MenuId },
                    principalSchema: "dbo",
                    principalTable: "RestaurantMenus",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StoreDefaultMenus_TenantId_MenuId",
            schema: "dbo",
            table: "StoreDefaultMenus",
            columns: new[] { "TenantId", "MenuId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("StoreDefaultMenus", "dbo");
        migrationBuilder.DropForeignKey(
            "FK_MenuDraftCategories_CatalogCategories_TenantId_CatalogCategoryId",
            "MenuDraftCategories", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftCategories_TenantId_CatalogCategoryId",
            "MenuDraftCategories", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftCategories_TenantId_MenuId_CatalogCategoryId",
            "MenuDraftCategories", "dbo");
        migrationBuilder.DropColumn("CatalogCategoryId", "MenuDraftCategories", "dbo");
    }
}
