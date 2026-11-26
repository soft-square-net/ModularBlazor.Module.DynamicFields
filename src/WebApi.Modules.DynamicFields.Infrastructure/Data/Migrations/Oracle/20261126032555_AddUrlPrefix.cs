using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.Oracle
{
    /// <inheritdoc />
    public partial class AddUrlPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishedUrlPrefix",
                table: "dynamicfields_fields",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            // Existing published root fields get the URL prefix their key suggests (FieldRules.SuggestUrlPrefix):
            // post.article -> articles, post.news -> news, post.review -> reviews, post.blog -> blog, any other key without
            // its "post." start and with '.' as '-'. A prefix two fields of a tenant would share gets "-{Id}" on all but
            // the first. Their stored drafts keep no prefix; FieldDefinition.Draft reads the published one into them.
            migrationBuilder.Sql("UPDATE \"dynamicfields_fields\" SET \"PublishedUrlPrefix\" = CASE WHEN \"Key\" = 'post.article' THEN N'articles' WHEN \"Key\" = 'post.news' THEN N'news' WHEN \"Key\" = 'post.review' THEN N'reviews' WHEN \"Key\" = 'post.blog' THEN N'blog' WHEN \"Key\" LIKE 'post.%' THEN REPLACE(SUBSTR(\"Key\", 6), N'.', N'-') ELSE REPLACE(\"Key\", N'.', N'-') END WHERE \"CanBeRoot\" = TRUE AND \"LatestVersion\" > 0");
            migrationBuilder.Sql("UPDATE \"dynamicfields_fields\" SET \"PublishedUrlPrefix\" = \"PublishedUrlPrefix\" || N'-' || TO_NCHAR(\"Id\") WHERE EXISTS (SELECT 1 FROM \"dynamicfields_fields\" o WHERE o.\"TenantId\" = \"dynamicfields_fields\".\"TenantId\" AND o.\"PublishedUrlPrefix\" = \"dynamicfields_fields\".\"PublishedUrlPrefix\" AND o.\"Id\" < \"dynamicfields_fields\".\"Id\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedUrlPrefix",
                table: "dynamicfields_fields");
        }
    }
}
