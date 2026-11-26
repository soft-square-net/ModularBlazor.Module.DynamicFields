using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.MySql
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
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            // Existing published root fields get the URL prefix their key suggests (FieldRules.SuggestUrlPrefix):
            // post.article -> articles, post.news -> news, post.review -> reviews, post.blog -> blog, any other key without
            // its "post." start and with '.' as '-'. A prefix two fields of a tenant would share gets "-{Id}" on all but
            // the first. Their stored drafts keep no prefix; FieldDefinition.Draft reads the published one into them.
            migrationBuilder.Sql("UPDATE `dynamicfields_fields` SET `PublishedUrlPrefix` = CASE WHEN `Key` = 'post.article' THEN 'articles' WHEN `Key` = 'post.news' THEN 'news' WHEN `Key` = 'post.review' THEN 'reviews' WHEN `Key` = 'post.blog' THEN 'blog' WHEN `Key` LIKE 'post.%' THEN REPLACE(SUBSTRING(`Key`, 6), '.', '-') ELSE REPLACE(`Key`, '.', '-') END WHERE `CanBeRoot` = TRUE AND `LatestVersion` > 0");
            migrationBuilder.Sql("UPDATE `dynamicfields_fields` f JOIN (SELECT a.`Id` FROM `dynamicfields_fields` a JOIN `dynamicfields_fields` b ON b.`TenantId` = a.`TenantId` AND b.`PublishedUrlPrefix` = a.`PublishedUrlPrefix` AND b.`Id` < a.`Id` GROUP BY a.`Id`) d ON d.`Id` = f.`Id` SET f.`PublishedUrlPrefix` = CONCAT(f.`PublishedUrlPrefix`, '-', f.`Id`)");
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
