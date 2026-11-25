using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnPath.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LessonNoteFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "LearningResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningResources_FileId",
                table: "LearningResources",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningResources_StoredFiles_FileId",
                table: "LearningResources",
                column: "FileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningResources_StoredFiles_FileId",
                table: "LearningResources");

            migrationBuilder.DropIndex(
                name: "IX_LearningResources_FileId",
                table: "LearningResources");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "LearningResources");
        }
    }
}
