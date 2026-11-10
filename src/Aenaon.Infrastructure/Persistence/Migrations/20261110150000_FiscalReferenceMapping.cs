using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Προσθέτει ασφαλή αποθήκευση φορολογικών αναφορών και προσωρινών αντιστοιχίσεων.</summary>
public partial class FiscalReferenceMapping : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_CatalogItems_TenantId_Id",
            schema: "dbo",
            table: "CatalogItems",
            columns: new[] { "TenantId", "Id" });

        migrationBuilder.CreateTable(
            name: "FiscalReferenceCodes",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Source = table.Column<int>(type: "int", nullable: false),
                CodeSystem = table.Column<int>(type: "int", nullable: false),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false,
                    collation: "Latin1_General_100_BIN2"),
                SourceVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false,
                    collation: "Latin1_General_100_BIN2"),
                Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalReferenceCodes", x => x.Id);
                table.CheckConstraint("CK_FiscalReferences_Source", "[Source] IN (1, 2, 3)");
                table.CheckConstraint("CK_FiscalReferences_CodeSystem", "[CodeSystem] BETWEEN 1 AND 8");
                table.CheckConstraint("CK_FiscalReferences_Period",
                    "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
                table.CheckConstraint("CK_FiscalReferences_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_FiscalReferences_Version",
                    "LEN(LTRIM(RTRIM([SourceVersion]))) > 0");
            });

        migrationBuilder.CreateTable(
            name: "FiscalTaxMappings",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CatalogItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerChannel = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                VatCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IncomeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IncomeTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VatExemptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MeasurementUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                MeasurementUnitEnId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalTaxMappings", x => x.Id);
                table.CheckConstraint("CK_FiscalMappings_Channel", "[CustomerChannel] IN (1, 2, 3)");
                table.CheckConstraint("CK_FiscalMappings_Period",
                    "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
                table.CheckConstraint("CK_FiscalMappings_Tenant",
                    "[TenantId] <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_CatalogItems_TenantId_CatalogItemId",
                    columns: x => new { x.TenantId, x.CatalogItemId },
                    principalSchema: "dbo",
                    principalTable: "CatalogItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_VatCategoryId",
                    column: x => x.VatCategoryId, principalSchema: "dbo", principalTable: "FiscalReferenceCodes",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_IncomeCategoryId",
                    column: x => x.IncomeCategoryId, principalSchema: "dbo", principalTable: "FiscalReferenceCodes",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_IncomeTypeId",
                    column: x => x.IncomeTypeId, principalSchema: "dbo", principalTable: "FiscalReferenceCodes",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_VatExemptionId",
                    column: x => x.VatExemptionId, principalSchema: "dbo", principalTable: "FiscalReferenceCodes",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_MeasurementUnitId",
                    column: x => x.MeasurementUnitId, principalSchema: "dbo",
                    principalTable: "FiscalReferenceCodes", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalTaxMappings_FiscalReferenceCodes_MeasurementUnitEnId",
                    column: x => x.MeasurementUnitEnId, principalSchema: "dbo",
                    principalTable: "FiscalReferenceCodes", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FiscalReferenceCodes_Source_CodeSystem_Code_SourceVersion_EffectiveFrom",
            schema: "dbo", table: "FiscalReferenceCodes",
            columns: new[] { "Source", "CodeSystem", "Code", "SourceVersion", "EffectiveFrom" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FiscalTaxMappings_TenantId_CatalogItemId_CustomerChannel_EffectiveFrom",
            schema: "dbo", table: "FiscalTaxMappings",
            columns: new[] { "TenantId", "CatalogItemId", "CustomerChannel", "EffectiveFrom" },
            unique: true);

        foreach (string column in new[] { "VatCategoryId", "IncomeCategoryId", "IncomeTypeId",
            "VatExemptionId", "MeasurementUnitId", "MeasurementUnitEnId" })
        {
            migrationBuilder.CreateIndex("IX_FiscalTaxMappings_" + column,
                schema: "dbo", table: "FiscalTaxMappings", column: column);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FiscalTaxMappings", schema: "dbo");
        migrationBuilder.DropTable(name: "FiscalReferenceCodes", schema: "dbo");
        migrationBuilder.DropUniqueConstraint(name: "AK_CatalogItems_TenantId_Id",
            schema: "dbo", table: "CatalogItems");
    }
}
