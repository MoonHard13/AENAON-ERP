using System;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Δέκατη additive migration: διατηρήσιμο ιστορικό εμπορικών τιμών.</summary>
[DbContext(typeof(AenaonDbContext))]
[Migration("20261114190000_PricingIntegrityHistory")]
public partial class PricingIntegrityHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Η νέα δομή δεν αλλάζει τα δεδομένα των υπαρχόντων τιμοκαταλόγων.
        migrationBuilder.CreateTable(
            name: "PriceListAuditEntries", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Action = table.Column<int>(type: "int", nullable: false),
                Source = table.Column<int>(type: "int", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                BeforeValue = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                AfterValue = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true)
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
                table.CheckConstraint("CK_PriceListAuditEntries_Actor",
                    "[ActorUserId] IS NULL OR [ActorUserId] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_PriceListAuditEntries_Action", "[Action] BETWEEN 0 AND 6");
                table.CheckConstraint("CK_PriceListAuditEntries_Source", "[Source] BETWEEN 0 AND 2");
            });
        migrationBuilder.CreateIndex(
            name: "IX_PriceListAuditEntries_TenantId_PriceListId_OccurredAtUtc_Id",
            schema: "dbo", table: "PriceListAuditEntries",
            columns: new[] { "TenantId", "PriceListId", "OccurredAtUtc", "Id" });
        // Απαγορεύονται ακόμη και χειροκίνητα UPDATE/DELETE σε εγγραφές audit.
        migrationBuilder.Sql("""
            CREATE TRIGGER [dbo].[TR_PriceListAuditEntries_AppendOnly]
            ON [dbo].[PriceListAuditEntries]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51010, 'Price list audit history is append-only.', 1;
            END
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Προσοχή: rollback της migration διαγράφει το ιστορικό.
        migrationBuilder.DropTable(name: "PriceListAuditEntries", schema: "dbo");
    }
}
