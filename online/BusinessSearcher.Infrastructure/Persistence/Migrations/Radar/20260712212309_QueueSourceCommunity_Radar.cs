using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusinessSearcher.Infrastructure.Persistence.Migrations.Radar
{
    /// <inheritdoc />
    public partial class QueueSourceCommunity_Radar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "queue_status",
                schema: "public",
                table: "availability_reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source",
                schema: "public",
                table: "availability_reports",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "community_questions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asker_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "community_answers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    answerer_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_answers", x => x.id);
                    table.ForeignKey(
                        name: "fk_community_answers_community_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "public",
                        principalTable: "community_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_answers_question_id",
                schema: "public",
                table: "community_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_questions_city",
                schema: "public",
                table: "community_questions",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_community_questions_created_at",
                schema: "public",
                table: "community_questions",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_answers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "community_questions",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "queue_status",
                schema: "public",
                table: "availability_reports");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "public",
                table: "availability_reports");
        }
    }
}
