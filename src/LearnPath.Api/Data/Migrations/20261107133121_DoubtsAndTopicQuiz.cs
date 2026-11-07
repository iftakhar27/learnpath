using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LearnPath.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DoubtsAndTopicQuiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuizPassPercent",
                table: "Topics",
                type: "integer",
                nullable: false,
                defaultValue: 60); // existing topics get the default quiz (the model sets new ones)

            migrationBuilder.AddColumn<int>(
                name: "QuizQuestionCount",
                table: "Topics",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "Passed",
                table: "PracticeSessions",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScorePercent",
                table: "PracticeSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Doubts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Doubts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Doubts_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Doubts_LearningResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "LearningResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Doubts_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Doubts_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Doubts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoubtMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoubtId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStaff = table.Column<bool>(type: "boolean", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoubtMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoubtMessages_Doubts_DoubtId",
                        column: x => x.DoubtId,
                        principalTable: "Doubts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DoubtMessages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DoubtMessages_AuthorId",
                table: "DoubtMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_DoubtMessages_DoubtId_CreatedAt",
                table: "DoubtMessages",
                columns: new[] { "DoubtId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_CourseId",
                table: "Doubts",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_QuestionId",
                table: "Doubts",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_ResourceId",
                table: "Doubts",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_Status_UpdatedAt",
                table: "Doubts",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_TopicId",
                table: "Doubts",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Doubts_UserId_UpdatedAt",
                table: "Doubts",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoubtMessages");

            migrationBuilder.DropTable(
                name: "Doubts");

            migrationBuilder.DropColumn(
                name: "QuizPassPercent",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "QuizQuestionCount",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "Passed",
                table: "PracticeSessions");

            migrationBuilder.DropColumn(
                name: "ScorePercent",
                table: "PracticeSessions");
        }
    }
}
