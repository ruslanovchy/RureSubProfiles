using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RureSubProfiles.Migrations
{
    /// <inheritdoc />
    public partial class Initial3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BannerUrl",
                table: "Profiles",
                newName: "BannerPath");

            migrationBuilder.RenameColumn(
                name: "AvatarUrl",
                table: "Profiles",
                newName: "AvatarPath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BannerPath",
                table: "Profiles",
                newName: "BannerUrl");

            migrationBuilder.RenameColumn(
                name: "AvatarPath",
                table: "Profiles",
                newName: "AvatarUrl");
        }
    }
}
