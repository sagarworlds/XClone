using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPostMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "post_mentions",
                columns: table => new
                {
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_mentions", x => new { x.post_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_post_mentions_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_mentions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_mentions_user_id",
                table: "post_mentions",
                column: "user_id");

            // Posts written before this migration have no rows yet: read the names out of the text the way
            // MentionParser does (an @ not glued to a word in front, 3-50 letters/digits/underscores, once per post
            // however it is spelled, the first 10 names) and look each one up without regard to case; when two
            // accounts differ only by case, the one spelled exactly as written wins, else the oldest. (PostgreSQL's
            // letter classes do not count combining accents as letters, so the common range of them is added by hand.)
            // Only the links are filled in: nobody is notified about posts from before.
            migrationBuilder.Sql("""
                INSERT INTO post_mentions (post_id, user_id)
                SELECT DISTINCT r.post_id, u.id
                FROM (
                    SELECT post_id, spelling, row_number() OVER (PARTITION BY post_id ORDER BY first_at) AS nth
                    FROM (
                        SELECT p.id AS post_id,
                               lower(m.found[1]) AS lowered,
                               min(m.ord) AS first_at,
                               (array_agg(m.found[1] ORDER BY m.ord))[1] AS spelling
                        FROM posts p
                        CROSS JOIN LATERAL regexp_matches(
                            p.content,
                            '(?<![[:alnum:]_@\u0300-\u036F])@([A-Za-z0-9_]{3,50})(?![[:alnum:]_\u0300-\u036F])',
                            'g') WITH ORDINALITY AS m(found, ord)
                        GROUP BY p.id, lower(m.found[1])
                    ) names
                ) r
                CROSS JOIN LATERAL (
                    SELECT id FROM users
                    WHERE lower(username) = lower(r.spelling)
                    ORDER BY (username = r.spelling) DESC, id
                    LIMIT 1
                ) u
                WHERE r.nth <= 10;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_mentions");
        }
    }
}
