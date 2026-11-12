using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Έκτη migration: ιεραρχικές κατηγορίες, ατομικοί μετρητές και ανθεκτικές επαναλήψεις αιτημάτων.</summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261112130000_CatalogHierarchyAndNumbering")]
public partial class CatalogHierarchyAndNumbering : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Η nullable προσθήκη διατηρεί όλες τις υφιστάμενες κατηγορίες ως ρίζες.
        migrationBuilder.AddColumn<Guid>(
            name: "ParentCategoryId", schema: "dbo", table: "CatalogCategories",
            type: "uniqueidentifier", nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_CatalogCategories_Parent_NotSelf", schema: "dbo",
            table: "CatalogCategories",
            sql: "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [Id]");
        migrationBuilder.CreateIndex(
            name: "IX_CatalogCategories_TenantId_ParentCategoryId",
            schema: "dbo", table: "CatalogCategories",
            columns: new[] { "TenantId", "ParentCategoryId" });
        migrationBuilder.AddForeignKey(
            name: "FK_CatalogCategories_CatalogCategories_TenantId_ParentCategoryId",
            schema: "dbo", table: "CatalogCategories",
            columns: new[] { "TenantId", "ParentCategoryId" },
            principalSchema: "dbo", principalTable: "CatalogCategories",
            principalColumns: new[] { "TenantId", "Id" },
            onDelete: ReferentialAction.Restrict);

        // Ο μετρητής αφορά έναν Tenant και έναν χώρο κωδικών: 1=κατηγορίες, 2=όλα τα είδη.
        migrationBuilder.CreateTable(
            name: "CatalogCodeCounters", schema: "dbo",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Scope = table.Column<byte>(type: "tinyint", nullable: false),
                LastNumber = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogCodeCounters", x => new { x.TenantId, x.Scope });
                table.CheckConstraint("CK_CatalogCodeCounters_Scope", "[Scope] IN (1, 2)");
                table.CheckConstraint("CK_CatalogCodeCounters_Last", "[LastNumber] >= 0");
                table.ForeignKey(
                    name: "FK_CatalogCodeCounters_Tenants_TenantId", column: x => x.TenantId,
                    principalSchema: "dbo", principalTable: "Tenants", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Μοναδικό κλειδί αίτησης για ασφαλή επανάληψη ενός POST μετά από απώλεια απάντησης.
        migrationBuilder.CreateTable(
            name: "CatalogMutationReceipts", schema: "dbo",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Entity = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CatalogMutationReceipts", x => new { x.TenantId, x.RequestId });
                table.CheckConstraint("CK_CatalogMutationReceipts_RequestId",
                    "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_CatalogMutationReceipts_Entity", "[Entity] IN (0, 1, 2)");
                table.ForeignKey(
                    name: "FK_CatalogMutationReceipts_Tenants_TenantId", column: x => x.TenantId,
                    principalSchema: "dbo", principalTable: "Tenants", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        // Οι ήδη καταχωρισμένοι αριθμητικοί κωδικοί 001/002 υπολογίζονται ως 1/2.
        // Η προετοιμασία δεν μεταβάλλει κανέναν υπάρχοντα κωδικό.
        migrationBuilder.Sql("""
            INSERT INTO dbo.CatalogCodeCounters (TenantId, Scope, LastNumber)
            SELECT TenantId, CONVERT(tinyint, 1), MAX(TRY_CONVERT(bigint, Code))
            FROM dbo.CatalogCategories
            WHERE Code NOT LIKE N'%[^0-9]%' AND TRY_CONVERT(bigint, Code) > 0
            GROUP BY TenantId;

            INSERT INTO dbo.CatalogCodeCounters (TenantId, Scope, LastNumber)
            SELECT TenantId, CONVERT(tinyint, 2), MAX(TRY_CONVERT(bigint, Code))
            FROM dbo.CatalogItems
            WHERE Code NOT LIKE N'%[^0-9]%' AND TRY_CONVERT(bigint, Code) > 0
            GROUP BY TenantId;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Το Down χάνει την ιεραρχία/τους μετρητές. Απαιτεί έγκριση και backup.
        migrationBuilder.DropTable(name: "CatalogMutationReceipts", schema: "dbo");
        migrationBuilder.DropTable(name: "CatalogCodeCounters", schema: "dbo");
        migrationBuilder.DropForeignKey(
            name: "FK_CatalogCategories_CatalogCategories_TenantId_ParentCategoryId",
            schema: "dbo", table: "CatalogCategories");
        migrationBuilder.DropIndex(
            name: "IX_CatalogCategories_TenantId_ParentCategoryId",
            schema: "dbo", table: "CatalogCategories");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CatalogCategories_Parent_NotSelf",
            schema: "dbo", table: "CatalogCategories");
        migrationBuilder.DropColumn(
            name: "ParentCategoryId", schema: "dbo", table: "CatalogCategories");
    }
}
