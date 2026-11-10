using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProductCatalogPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CatalogCategories",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Greek_CI_AS"),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogCategories", x => x.Id);
                table.UniqueConstraint("AK_CatalogCategories_TenantId_Id", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_CatalogCategories_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogCategories_TenantId", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogCategories_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_CatalogCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.ForeignKey(
                    name: "FK_CatalogCategories_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "dbo",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CatalogItems",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Greek_CI_AS"),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Kind = table.Column<int>(type: "int", nullable: false),
                TracksStock = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogItems", x => x.Id);
                table.CheckConstraint("CK_CatalogItems_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogItems_TenantId", "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogItems_CategoryId", "[CategoryId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogItems_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_CatalogItems_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.CheckConstraint("CK_CatalogItems_Kind", "[Kind] IN (1, 2)");
                table.CheckConstraint("CK_CatalogItems_ServiceStock", "[Kind] <> 2 OR [TracksStock] = 0");
                table.ForeignKey(
                    name: "FK_CatalogItems_CatalogCategories_TenantId_CategoryId",
                    columns: x => new { x.TenantId, x.CategoryId },
                    principalSchema: "dbo",
                    principalTable: "CatalogCategories",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CatalogItems_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "dbo",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CatalogCategories_TenantId_Code",
            schema: "dbo",
            table: "CatalogCategories",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CatalogItems_TenantId_CategoryId",
            schema: "dbo",
            table: "CatalogItems",
            columns: new[] { "TenantId", "CategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_CatalogItems_TenantId_Code",
            schema: "dbo",
            table: "CatalogItems",
            columns: new[] { "TenantId", "Code" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Η αναστροφή διαγράφει τα δεδομένα καταλόγου και επιτρέπεται μόνο με ασφαλές backup.
        migrationBuilder.DropTable(name: "CatalogItems", schema: "dbo");
        migrationBuilder.DropTable(name: "CatalogCategories", schema: "dbo");
    }
}
