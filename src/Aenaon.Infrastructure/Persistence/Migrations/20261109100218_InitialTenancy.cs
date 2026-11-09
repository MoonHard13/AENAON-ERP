using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aenaon.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialTenancy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "dbo");

        migrationBuilder.CreateTable(
            name: "Tenants",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tenants", x => x.Id);
                table.CheckConstraint("CK_Tenants_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Tenants_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            });

        migrationBuilder.CreateTable(
            name: "Stores",
            schema: "dbo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "Greek_CI_AS"),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Stores", x => x.Id);
                table.CheckConstraint("CK_Stores_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.CheckConstraint("CK_Stores_Id", "[Id] <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_Stores_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                table.ForeignKey(
                    name: "FK_Stores_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalSchema: "dbo",
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Stores_TenantId_Code",
            schema: "dbo",
            table: "Stores",
            columns: new[] { "TenantId", "Code" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Stores",
            schema: "dbo");

        migrationBuilder.DropTable(
            name: "Tenants",
            schema: "dbo");
    }
}
