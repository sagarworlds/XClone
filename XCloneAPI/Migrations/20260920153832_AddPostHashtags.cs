using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPostHashtags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "post_hashtags",
                columns: table => new
                {
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_hashtags", x => new { x.post_id, x.tag });
                    table.ForeignKey(
                        name: "FK_post_hashtags_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_hashtags_tag_post_id",
                table: "post_hashtags",
                columns: new[] { "tag", "post_id" });

            // Posts written before this migration have no rows yet: read their hashtags out of the text, the way
            // HashtagParser does (a # not glued to a word or symbol in front, 1-50 letters/digits/underscores with at
            // least one letter, lower case, each tag once per post, the first 10 only). PostgreSQL's letter classes
            // follow the database's locale, so for unusual scripts this can differ from the parser at the edges; posts
            // written from now on always use the parser.
            migrationBuilder.Sql("""
                INSERT INTO post_hashtags (post_id, tag)
                SELECT post_id, tag
                FROM (
                    SELECT post_id, tag, row_number() OVER (PARTITION BY post_id ORDER BY first_at) AS nth
                    FROM (
                        SELECT p.id AS post_id, lower(m.found[1]) AS tag, min(m.ord) AS first_at
                        FROM posts p
                        CROSS JOIN LATERAL regexp_matches(
                            p.content,
                            '(?<![[:alnum:]_#&])#([[:alnum:]_]{1,50})(?![[:alnum:]_])',
                            'g') WITH ORDINALITY AS m(found, ord)
                        WHERE m.found[1] ~ '[[:alpha:]]'
                        GROUP BY p.id, lower(m.found[1])
                    ) found
                ) ranked
                WHERE nth <= 10;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_hashtags");
        }
    }
}
