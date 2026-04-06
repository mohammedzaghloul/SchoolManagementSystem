using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace School.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoGradeAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GradeLevelId",
                table: "Videos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Videos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Videos_GradeLevelId",
                table: "Videos",
                column: "GradeLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_GradeLevels_GradeLevelId",
                table: "Videos",
                column: "GradeLevelId",
                principalTable: "GradeLevels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_GradeLevels_GradeLevelId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_GradeLevelId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "GradeLevelId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Videos");
        }
    }
}
