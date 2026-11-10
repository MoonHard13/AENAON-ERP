using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <summary>Πέμπτη migration: απομονωμένο staging και καταγραφή μίας τελικής κρίσης πηγής.</summary>
public partial class FiscalImportStagingReview : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FiscalImportBatches", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Source = table.Column<int>(type: "int", nullable: false),
                CodeSystem = table.Column<int>(type: "int", nullable: false),
                SourceVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ArtifactSha256 = table.Column<string>(type: "varchar(64)", unicode: false,
                    maxLength: 64, nullable: false),
                ContentSha256 = table.Column<string>(type: "varchar(64)", unicode: false,
                    maxLength: 64, nullable: false),
                VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalImportBatches", x => x.Id);
                table.UniqueConstraint("AK_FiscalImportBatches_Id_SubmittedByUserId",
                    x => new { x.Id, x.SubmittedByUserId });
                table.CheckConstraint("CK_FiscalImportBatches_Status", "[Status] IN (1, 2, 3)");
                table.CheckConstraint("CK_FiscalImportBatches_Source", "[Source] IN (1, 2)");
                table.CheckConstraint("CK_FiscalImportBatches_CodeSystem", "[CodeSystem] BETWEEN 1 AND 8");
                table.CheckConstraint("CK_FiscalImportBatches_Evidence",
                    "LEN([ArtifactSha256]) = 64 AND LEN([ContentSha256]) = 64 AND [VerifiedAtUtc] <= [SubmittedAtUtc]");
                table.CheckConstraint("CK_FiscalImportBatches_Review", """
                    ([Status] = 1 AND [ReviewedByUserId] IS NULL AND [ReviewedAtUtc] IS NULL AND [ReviewReason] IS NULL)
                    OR ([Status] IN (2, 3) AND [ReviewedByUserId] IS NOT NULL
                        AND [ReviewedByUserId] <> [SubmittedByUserId]
                        AND [ReviewedAtUtc] >= [SubmittedAtUtc] AND [ReviewReason] IS NOT NULL)
                    """);
                table.ForeignKey(
                    name: "FK_FiscalImportBatches_AspNetUsers_SubmittedByUserId",
                    column: x => x.SubmittedByUserId,
                    principalSchema: "dbo", principalTable: "AspNetUsers", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalImportBatches_AspNetUsers_ReviewedByUserId",
                    column: x => x.ReviewedByUserId,
                    principalSchema: "dbo", principalTable: "AspNetUsers", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FiscalImportBatchRows", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64,
                    nullable: false, collation: "Latin1_General_100_BIN2"),
                Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalImportBatchRows", x => x.Id);
                table.CheckConstraint("CK_FiscalImportBatchRows_Period",
                    "[EffectiveUntil] IS NULL OR [EffectiveUntil] >= [EffectiveFrom]");
                table.CheckConstraint("CK_FiscalImportBatchRows_Code",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.ForeignKey(
                    name: "FK_FiscalImportBatchRows_FiscalImportBatches_BatchId",
                    column: x => x.BatchId, principalSchema: "dbo",
                    principalTable: "FiscalImportBatches", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FiscalImportReviews", schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Decision = table.Column<int>(type: "int", nullable: false),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ContentSha256 = table.Column<string>(type: "varchar(64)", unicode: false,
                    maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FiscalImportReviews", x => x.Id);
                table.CheckConstraint("CK_FiscalImportReviews_Decision", "[Decision] IN (2, 3)");
                table.CheckConstraint("CK_FiscalImportReviews_Reviewer",
                    "[ReviewerUserId] <> [SubmittedByUserId]");
                table.CheckConstraint("CK_FiscalImportReviews_Hash", "LEN([ContentSha256]) = 64");
                table.CheckConstraint("CK_FiscalImportReviews_Reason", "LEN(LTRIM(RTRIM([Reason]))) > 0");
                table.ForeignKey(
                    name: "FK_FiscalImportReviews_FiscalImportBatches_BatchId_SubmittedByUserId",
                    columns: x => new { x.BatchId, x.SubmittedByUserId },
                    principalSchema: "dbo", principalTable: "FiscalImportBatches",
                    principalColumns: new[] { "Id", "SubmittedByUserId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FiscalImportReviews_AspNetUsers_ReviewerUserId",
                    column: x => x.ReviewerUserId,
                    principalSchema: "dbo", principalTable: "AspNetUsers", principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportBatches_Source_CodeSystem_SourceVersion",
            schema: "dbo", table: "FiscalImportBatches",
            columns: new[] { "Source", "CodeSystem", "SourceVersion" });
        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportBatches_SubmittedByUserId",
            schema: "dbo", table: "FiscalImportBatches", column: "SubmittedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportBatches_ReviewedByUserId",
            schema: "dbo", table: "FiscalImportBatches", column: "ReviewedByUserId");
        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportBatchRows_BatchId_Code_EffectiveFrom",
            schema: "dbo", table: "FiscalImportBatchRows",
            columns: new[] { "BatchId", "Code", "EffectiveFrom" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportReviews_BatchId_SubmittedByUserId",
            schema: "dbo", table: "FiscalImportReviews",
            columns: new[] { "BatchId", "SubmittedByUserId" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_FiscalImportReviews_ReviewerUserId",
            schema: "dbo", table: "FiscalImportReviews", column: "ReviewerUserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FiscalImportBatchRows", schema: "dbo");
        migrationBuilder.DropTable(name: "FiscalImportReviews", schema: "dbo");
        migrationBuilder.DropTable(name: "FiscalImportBatches", schema: "dbo");
    }
}
