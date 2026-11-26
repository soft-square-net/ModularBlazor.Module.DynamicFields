using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddUrlPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishedUrlPrefix",
                schema: "dynamicfields",
                table: "fields",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Existing published root fields get the URL prefix their key suggests (FieldRules.SuggestUrlPrefix):
            // post.article -> articles, post.news -> news, post.review -> reviews, post.blog -> blog, any other key without
            // its "post." start and with '.' as '-'. A prefix two fields of a tenant would share gets "-{Id}" on all but
            // the first. Their stored drafts keep no prefix; FieldDefinition.Draft reads the published one into them.
            migrationBuilder.Sql("UPDATE [dynamicfields].[fields] SET [PublishedUrlPrefix] = CASE WHEN [Key] = 'post.article' THEN 'articles' WHEN [Key] = 'post.news' THEN 'news' WHEN [Key] = 'post.review' THEN 'reviews' WHEN [Key] = 'post.blog' THEN 'blog' WHEN [Key] LIKE 'post.%' THEN REPLACE(SUBSTRING([Key], 6, 64), '.', '-') ELSE REPLACE([Key], '.', '-') END WHERE [CanBeRoot] = 1 AND [LatestVersion] > 0");
            migrationBuilder.Sql("UPDATE [dynamicfields].[fields] SET [PublishedUrlPrefix] = [PublishedUrlPrefix] + '-' + CAST([Id] AS nvarchar(20)) WHERE EXISTS (SELECT 1 FROM [dynamicfields].[fields] o WHERE o.[TenantId] = [dynamicfields].[fields].[TenantId] AND o.[PublishedUrlPrefix] = [dynamicfields].[fields].[PublishedUrlPrefix] AND o.[Id] < [dynamicfields].[fields].[Id])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedUrlPrefix",
                schema: "dynamicfields",
                table: "fields");
        }
    }
}
