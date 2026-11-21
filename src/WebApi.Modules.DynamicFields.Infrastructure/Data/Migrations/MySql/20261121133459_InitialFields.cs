using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class InitialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "dynamicfields_fields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    DraftJson = table.Column<string>(type: "longtext", nullable: false),
                    Title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    SchemaType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    CanBeRoot = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LatestVersion = table.Column<int>(type: "int", nullable: false),
                    HasDraftChanges = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dynamicfields_fields", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "dynamicfields_field_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Number = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "longtext", nullable: false),
                    PublishedBy = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FieldId = table.Column<int>(type: "int", nullable: true)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_dynamicfields_field_versions_FieldId_Number",
                table: "dynamicfields_field_versions",
                columns: new[] { "FieldId", "Number" },
                unique: true);

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
