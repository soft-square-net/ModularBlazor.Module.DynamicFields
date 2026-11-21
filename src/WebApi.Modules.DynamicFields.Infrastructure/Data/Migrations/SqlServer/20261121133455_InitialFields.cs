using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class InitialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dynamicfields");

            migrationBuilder.CreateTable(
                name: "fields",
                schema: "dynamicfields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SchemaType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CanBeRoot = table.Column<bool>(type: "bit", nullable: false),
                    LatestVersion = table.Column<int>(type: "int", nullable: false),
                    HasDraftChanges = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "field_versions",
                schema: "dynamicfields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FieldId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_field_versions_fields_FieldId",
                        column: x => x.FieldId,
                        principalSchema: "dynamicfields",
                        principalTable: "fields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_field_versions_FieldId_Number",
                schema: "dynamicfields",
                table: "field_versions",
                columns: new[] { "FieldId", "Number" },
                unique: true,
                filter: "[FieldId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_fields_TenantId",
                schema: "dynamicfields",
                table: "fields",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_fields_TenantId_Key",
                schema: "dynamicfields",
                table: "fields",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_versions",
                schema: "dynamicfields");

            migrationBuilder.DropTable(
                name: "fields",
                schema: "dynamicfields");
        }
    }
}
