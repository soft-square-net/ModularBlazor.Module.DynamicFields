using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.Oracle
{
    /// <inheritdoc />
    public partial class InitialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dynamicfields_fields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TenantId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    DraftJson = table.Column<string>(type: "NCLOB", nullable: false),
                    Title = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    SchemaType = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    CanBeRoot = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LatestVersion = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HasDraftChanges = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    CreatedBy = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dynamicfields_fields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dynamicfields_field_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Number = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "NCLOB", nullable: false),
                    PublishedBy = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    FieldId = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dynamicfields_field_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dynamicfields_field_versions_dynamicfields_fields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "dynamicfields_fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dynamicfields_field_versions_FieldId_Number",
                table: "dynamicfields_field_versions",
                columns: new[] { "FieldId", "Number" },
                unique: true,
                filter: "\"FieldId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_dynamicfields_fields_TenantId",
                table: "dynamicfields_fields",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_dynamicfields_fields_TenantId_Key",
                table: "dynamicfields_fields",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dynamicfields_field_versions");

            migrationBuilder.DropTable(
                name: "dynamicfields_fields");
        }
    }
}
