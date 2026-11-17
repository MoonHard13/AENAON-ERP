using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>
/// Προσθετική ενδέκατη migration για πρόχειρα μενού, χωρίς δημοσίευση, POS και φορολογικά δεδομένα.
/// Δεν εφαρμόζεται αυτόματα σε καμία βάση.
/// </summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261117110000_MenuDraftManagement")]
public partial class MenuDraftManagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RestaurantMenus", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false,
                    collation: "Greek_CI_AS"),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IsArchived = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RestaurantMenus", x => x.Id);
                table.UniqueConstraint("AK_RestaurantMenus_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_RestaurantMenus_Tenants_TenantId",
                    column: x => x.TenantId, principalSchema: "dbo", principalTable: "Tenants",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_RestaurantMenus_TenantId",
                    "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_RestaurantMenus_Id",
                    "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_RestaurantMenus_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_RestaurantMenus_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_RestaurantMenus_TenantId_Code", schema: "dbo", table: "RestaurantMenus",
            columns: new[] { "TenantId", "Code" }, unique: true);

        migrationBuilder.CreateTable(
            name: "MenuDrafts", schema: "dbo",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                EditedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuDrafts", x => new { x.TenantId, x.MenuId });
                table.ForeignKey(
                    name: "FK_MenuDrafts_RestaurantMenus_TenantId_MenuId",
                    columns: x => new { x.TenantId, x.MenuId }, principalSchema: "dbo",
                    principalTable: "RestaurantMenus",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_MenuDrafts_Revision", "[Revision] >= 1");
            });

        migrationBuilder.CreateTable(
            name: "MenuDraftOperationReceipts", schema: "dbo",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64,
                    unicode: false, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuDraftOperationReceipts",
                    x => new { x.TenantId, x.RequestId });
                table.ForeignKey(
                    name: "FK_MenuDraftOperationReceipts_RestaurantMenus_TenantId_MenuId",
                    columns: x => new { x.TenantId, x.MenuId }, principalSchema: "dbo",
                    principalTable: "RestaurantMenus",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_MenuDraftReceipts_Revision", "[Revision] >= 1");
            });

        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftOperationReceipts_TenantId_MenuId_OccurredUtc",
            schema: "dbo", table: "MenuDraftOperationReceipts",
            columns: new[] { "TenantId", "MenuId", "OccurredUtc" });

        migrationBuilder.CreateTable(
            name: "MenuDraftCategories", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Position = table.Column<int>(type: "int", nullable: false),
                IsVisible = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuDraftCategories", x => x.Id);
                table.UniqueConstraint("AK_MenuDraftCategories_TenantId_MenuId_Id",
                    x => new { x.TenantId, x.MenuId, x.Id });
                table.ForeignKey(
                    name: "FK_MenuDraftCategories_MenuDrafts_TenantId_MenuId",
                    columns: x => new { x.TenantId, x.MenuId }, principalSchema: "dbo",
                    principalTable: "MenuDrafts",
                    principalColumns: new[] { "TenantId", "MenuId" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_MenuDraftCategories_Position",
                    "[Position] BETWEEN 1 AND 10000");
                table.CheckConstraint("CK_MenuDraftCategories_Name",
                    "LEN(LTRIM(RTRIM([Name]))) > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftCategories_TenantId_MenuId_Position_Id",
            schema: "dbo", table: "MenuDraftCategories",
            columns: new[] { "TenantId", "MenuId", "Position", "Id" });

        migrationBuilder.CreateTable(
            name: "MenuDraftItems", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MenuCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Position = table.Column<int>(type: "int", nullable: false),
                NameOverride = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MenuDraftItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId",
                    columns: x => new { x.TenantId, x.MenuId, x.MenuCategoryId },
                    principalSchema: "dbo", principalTable: "MenuDraftCategories",
                    principalColumns: new[] { "TenantId", "MenuId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MenuDraftItems_CatalogItems_TenantId_CatalogItemId",
                    columns: x => new { x.TenantId, x.CatalogItemId },
                    principalSchema: "dbo", principalTable: "CatalogItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_MenuDraftItems_Position", "[Position] BETWEEN 1 AND 10000");
            });

        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "CatalogItemId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "MenuId", "MenuCategoryId", "Position", "Id" });
        migrationBuilder.CreateIndex(
            name: "IX_MenuDraftItems_TenantId_CatalogItemId",
            schema: "dbo", table: "MenuDraftItems",
            columns: new[] { "TenantId", "CatalogItemId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MenuDraftItems", schema: "dbo");
        migrationBuilder.DropTable(name: "MenuDraftOperationReceipts", schema: "dbo");
        migrationBuilder.DropTable(name: "MenuDraftCategories", schema: "dbo");
        migrationBuilder.DropTable(name: "MenuDrafts", schema: "dbo");
        migrationBuilder.DropTable(name: "RestaurantMenus", schema: "dbo");
    }
}
