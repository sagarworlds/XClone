using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowKeysetIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old single-column indexes may be missing from a database that was not built by these migrations
            // (a plain DropIndex would fail there), and the new indexes make them redundant anyway.
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_follows_follower_id\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_follows_following_id\";");

            migrationBuilder.CreateIndex(
                name: "IX_follows_follower_id_id",
                table: "follows",
                columns: new[] { "follower_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_follows_following_id_id",
                table: "follows",
                columns: new[] { "following_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_follows_follower_id_id",
                table: "follows");

            migrationBuilder.DropIndex(
                name: "IX_follows_following_id_id",
                table: "follows");

            migrationBuilder.CreateIndex(
                name: "IX_follows_follower_id",
                table: "follows",
                column: "follower_id");

            migrationBuilder.CreateIndex(
                name: "IX_follows_following_id",
                table: "follows",
                column: "following_id");
        }
    }
}
