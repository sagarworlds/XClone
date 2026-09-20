using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRepliesAndRetweets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_post_id",
                table: "posts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "retweets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retweets", x => x.id);
                    table.ForeignKey(
                        name: "FK_retweets_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_retweets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_posts_parent_post_id",
                table: "posts",
                column: "parent_post_id");

            migrationBuilder.CreateIndex(
                name: "IX_retweets_post_id",
                table: "retweets",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_retweets_user_id_post_id",
                table: "retweets",
                columns: new[] { "user_id", "post_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_posts_posts_parent_post_id",
                table: "posts",
                column: "parent_post_id",
                principalTable: "posts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_posts_posts_parent_post_id",
                table: "posts");

            migrationBuilder.DropTable(
                name: "retweets");

            migrationBuilder.DropIndex(
                name: "IX_posts_parent_post_id",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "parent_post_id",
                table: "posts");
        }
    }
}
