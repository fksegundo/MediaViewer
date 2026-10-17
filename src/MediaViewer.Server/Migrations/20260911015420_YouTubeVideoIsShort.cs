using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaViewer.Server.Migrations
{
    /// <inheritdoc />
    public partial class YouTubeVideoIsShort : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShort",
                table: "YouTubeVideos",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShort",
                table: "YouTubeVideos");
        }
    }
}
