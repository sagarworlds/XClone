using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddKeysetPagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old single-column indexes may be missing from a database that was not built by these migrations
            // (a plain DropIndex would fail there), and the new indexes make them redundant anyway.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_posts_parent_post_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_posts_user_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_notifications_recipient_id_created_at\";");

            migrationBuilder.CreateIndex(
                name: "IX_posts_parent_post_id_id",
                table: "posts",
                columns: new[] { "parent_post_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_posts_user_id_id",
                table: "posts",
                columns: new[] { "user_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_id_id",
                table: "notifications",
                columns: new[] { "recipient_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posts_parent_post_id_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_posts_user_id_id",
                table: "posts");

            migrationBuilder.DropIndex(
                name: "IX_notifications_recipient_id_id",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_posts_parent_post_id",
                table: "posts",
                column: "parent_post_id");

            migrationBuilder.CreateIndex(
                name: "IX_posts_user_id",
                table: "posts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at" });
        }
    }
}
