using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XCloneAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddPostEditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "edited_at",
                table: "posts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: the local database was built by hand and may not have every object the migrations create
            migrationBuilder.Sql("ALTER TABLE posts DROP COLUMN IF EXISTS edited_at;");
        }
    }
}
