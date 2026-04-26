using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace School.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeUploadConfirmations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradeUploadConfirmations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    GradeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeUploadConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeUploadConfirmations_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeUploadConfirmations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeUploadConfirmations_SubjectId",
                table: "GradeUploadConfirmations",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeUploadConfirmations_TeacherId_SubjectId_GradeType_Date",
                table: "GradeUploadConfirmations",
                columns: new[] { "TeacherId", "SubjectId", "GradeType", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeUploadConfirmations");
        }
    }
}
