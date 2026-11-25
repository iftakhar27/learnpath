using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnPath.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NcertSyllabus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "NcertChapterId",
                table: "Chapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NcertChapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassLevel = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Subject = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleHi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NcertChapters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chapters_NcertChapterId",
                table: "Chapters",
                column: "NcertChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_NcertChapters_ClassLevel_Subject_Number",
                table: "NcertChapters",
                columns: new[] { "ClassLevel", "Subject", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_NcertChapters_NcertChapterId",
                table: "Chapters",
                column: "NcertChapterId",
                principalTable: "NcertChapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_NcertChapters_NcertChapterId",
                table: "Chapters");

            migrationBuilder.DropTable(
                name: "NcertChapters");

            migrationBuilder.DropIndex(
                name: "IX_Chapters_NcertChapterId",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "NcertChapterId",
                table: "Chapters");
        }
    }
}
