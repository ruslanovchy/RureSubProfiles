using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RureSubProfiles.Migrations
{
    /// <inheritdoc />
    public partial class Initial6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RedisId",
                table: "Profiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_RedisId",
                table: "Profiles",
                column: "RedisId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Profiles_RedisId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "RedisId",
                table: "Profiles");
        }
    }
}
