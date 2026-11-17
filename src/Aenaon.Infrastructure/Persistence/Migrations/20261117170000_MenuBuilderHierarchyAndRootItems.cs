using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>
/// Προσθέτει ιεραρχία κατηγοριών και είδη ρίζας χωρίς διαγραφή υφιστάμενων εγγραφών.
/// Η εφαρμογή στη βάση απαιτεί ξεχωριστή έγκριση και έλεγχο SQL/backup.
/// </summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261117170000_MenuBuilderHierarchyAndRootItems")]
public partial class MenuBuilderHierarchyAndRootItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ParentCategoryId", schema: "dbo", table: "MenuDraftCategories",
            type: "uniqueidentifier", nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_MenuDraftCategories_NotOwnParent",
            schema: "dbo", table: "MenuDraftCategories",
            sql: "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [Id]");

        // Μόνο οι υπάρχουσες κατηγορίες που συνδέονται ρητά με ERP Catalog
        // αποκτούν γονέα, χωρίς αντιστοίχιση βάσει ονόματος.
        migrationBuilder.Sql("""
            UPDATE child SET ParentCategoryId = parent.Id
            FROM dbo.MenuDraftCategories AS child
            INNER JOIN dbo.CatalogCategories AS source
                ON source.TenantId = child.TenantId AND source.Id = child.CatalogCategoryId
            INNER JOIN dbo.MenuDraftCategories AS parent
                ON parent.TenantId = child.TenantId AND parent.MenuId = child.MenuId
                AND parent.CatalogCategoryId = source.ParentCategoryId
            WHERE child.CatalogCategoryId IS NOT NULL
              AND child.ParentCategoryId IS NULL
              AND parent.Id <> child.Id;
            """);

        // Κανονικοποίηση θέσεων ανά επίπεδο, διατηρώντας την υπάρχουσα σειρά.
        migrationBuilder.Sql("""
            WITH Ordered AS (
                SELECT Id, ROW_NUMBER() OVER (
                    PARTITION BY TenantId, MenuId, ParentCategoryId
                    ORDER BY Position, Id) AS NewPosition
                FROM dbo.MenuDraftCategories
            )
            UPDATE category SET Position = CONVERT(int, ordered.NewPosition)
            FROM dbo.MenuDraftCategories AS category
            INNER JOIN Ordered AS ordered ON ordered.Id = category.Id;
            """);

        migrationBuilder.DropIndex(
            name: "IX_MenuDraftCategories_TenantId_MenuId_Position_Id",
            schema: "dbo", table: "MenuDraftCategories");
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftCategories_TenantId_MenuId_ParentCategoryId_Position_Id",
            schema: "dbo", table: "MenuDraftCategories",
            columns: new[] { "TenantId", "MenuId", "ParentCategoryId", "Position", "Id" });
        migrationBuilder.AddForeignKey(
            name: "FK_MenuDraftCategories_MenuDraftCategories_TenantId_MenuId_ParentCategoryId",
            schema: "dbo", table: "MenuDraftCategories",
            columns: new[] { "TenantId", "MenuId", "ParentCategoryId" },
            principalSchema: "dbo", principalTable: "MenuDraftCategories",
            principalColumns: new[] { "TenantId", "MenuId", "Id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropForeignKey(
            "FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId",
            "MenuDraftItems", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id",
            "MenuDraftItems", "dbo");
        migrationBuilder.DropIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems");
        migrationBuilder.AlterColumn<Guid>(
            name: "MenuCategoryId", schema: "dbo", table: "MenuDraftItems",
            type: "uniqueidentifier", nullable: true,
            oldClrType: typeof(Guid), oldType: "uniqueidentifier");
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "CatalogItemId" },
            unique: true, filter: "[MenuCategoryId] IS NOT NULL");
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "CatalogItemId" },
            unique: true, filter: "[MenuCategoryId] IS NULL");
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "Position", "Id" });
        migrationBuilder.AddForeignKey(
            name: "FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId" },
            principalSchema: "dbo", principalTable: "MenuDraftCategories",
            principalColumns: new[] { "TenantId", "MenuId", "Id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Απαγορεύεται ασφαλής επαναφορά αν υπάρχουν είδη στη ρίζα.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.MenuDraftItems WHERE MenuCategoryId IS NULL)
                THROW 51000, 'Root items exist: move them before reverting this migration.', 1;
            """);
        migrationBuilder.DropForeignKey(
            "FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId",
            "MenuDraftItems", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id",
            "MenuDraftItems", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftItems_TenantId_MenuId_CatalogItemId", "MenuDraftItems", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId",
            "MenuDraftItems", "dbo");
        migrationBuilder.AlterColumn<Guid>(
            name: "MenuCategoryId", schema: "dbo", table: "MenuDraftItems",
            type: "uniqueidentifier", nullable: false,
            oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "CatalogItemId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "Position", "Id" });
        migrationBuilder.AddForeignKey(
            name: "FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId" },
            principalSchema: "dbo", principalTable: "MenuDraftCategories",
            principalColumns: new[] { "TenantId", "MenuId", "Id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.DropForeignKey(
            "FK_MenuDraftCategories_MenuDraftCategories_TenantId_MenuId_ParentCategoryId",
            "MenuDraftCategories", "dbo");
        migrationBuilder.DropIndex(
            "IX_MenuDraftCategories_TenantId_MenuId_ParentCategoryId_Position_Id",
            "MenuDraftCategories", "dbo");
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftCategories_TenantId_MenuId_Position_Id",
            schema: "dbo", table: "MenuDraftCategories",
            columns: new[] { "TenantId", "MenuId", "Position", "Id" });
        migrationBuilder.DropCheckConstraint(
            "CK_MenuDraftCategories_NotOwnParent", "MenuDraftCategories", "dbo");
        migrationBuilder.DropColumn("ParentCategoryId", "MenuDraftCategories", "dbo");
    }
}
