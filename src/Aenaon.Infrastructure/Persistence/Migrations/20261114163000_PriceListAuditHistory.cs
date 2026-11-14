using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Δέκατη, προσθετική migration εμπορικού ιστορικού και SQL απαγόρευσης μεταβολών.</summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261114163000_PriceListAuditHistory")]
public partial class PriceListAuditHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PriceListAuditEntries", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                Action = table.Column<int>(type: "int", nullable: false),
                OccurredUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                BeforePrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                AfterPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                BeforePolicy = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                AfterPolicy = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceListAuditEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_PriceListAuditEntries_PriceLists_TenantId_PriceListId",
                    columns: x => new { x.TenantId, x.PriceListId },
                    principalSchema: "dbo", principalTable: "PriceLists",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_PriceListAuditEntries_Action", "[Action] BETWEEN 1 AND 7");
                table.CheckConstraint("CK_PriceListAuditEntries_Source",
                    "[Source] IN ('Web', 'CLI', 'System')");
                table.CheckConstraint("CK_PriceListAuditEntries_Item",
                    "([Action] BETWEEN 1 AND 4 AND [CatalogItemId] IS NULL) OR " +
                    "([Action] BETWEEN 5 AND 7 AND [CatalogItemId] IS NOT NULL)");
                table.CheckConstraint("CK_PriceListAuditEntries_Prices",
                    "([BeforePrice] IS NULL OR [BeforePrice] >= 0) AND " +
                    "([AfterPrice] IS NULL OR [AfterPrice] >= 0)");
            });

        migrationBuilder.CreateIndex(
            name: "IX_PriceListAuditEntries_TenantId_PriceListId_OccurredUtc_Id",
            schema: "dbo", table: "PriceListAuditEntries",
            columns: new[] { "TenantId", "PriceListId", "OccurredUtc", "Id" });

        // SQL Server προστασία ιστορικού ακόμη και από τυχαίο UPDATE ή DELETE εκτός EF.
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_PriceListAuditEntries_Immutable]
            ON [dbo].[PriceListAuditEntries]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51010, 'Price list audit entries are append-only.', 1;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_PriceListAuditEntries_Immutable];");
        migrationBuilder.DropTable(name: "PriceListAuditEntries", schema: "dbo");
    }
}
