using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RestaurantDelivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyMatrixSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reordered from the scaffolded default (which dropped Category before
            // anything else existed to migrate its values into) so the data-preservation
            // step below can run with both the old Category column and the new
            // SurveyMatrixSections table/MatrixSectionId column present at once.
            migrationBuilder.CreateTable(
                name: "SurveyMatrixSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyMatrixSections", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "MatrixSectionId",
                table: "SurveyQuestions",
                type: "integer",
                nullable: true);

            // Data migration: turns every distinct non-blank free-text Category value into
            // a real SurveyMatrixSections row, then points each question's new
            // MatrixSectionId at the matching row - preserving existing section groupings
            // instead of silently discarding them when Category is dropped below. Matches
            // case-insensitively (LOWER) and after trimming whitespace, so pre-existing
            // near-duplicates like "Service" vs "service " collapse into one real section
            // rather than becoming two - exactly the kind of drift this feature exists to
            // stop happening going forward.
            migrationBuilder.Sql(
                """
                INSERT INTO "SurveyMatrixSections" ("Name")
                SELECT DISTINCT ON (LOWER(TRIM("Category"))) TRIM("Category")
                FROM "SurveyQuestions"
                WHERE "Category" IS NOT NULL AND TRIM("Category") <> ''
                ORDER BY LOWER(TRIM("Category"));

                UPDATE "SurveyQuestions" q
                SET "MatrixSectionId" = s."Id"
                FROM "SurveyMatrixSections" s
                WHERE LOWER(TRIM(q."Category")) = LOWER(s."Name");
                """);

            migrationBuilder.DropColumn(
                name: "Category",
                table: "SurveyQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyQuestions_MatrixSectionId",
                table: "SurveyQuestions",
                column: "MatrixSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyMatrixSections_Name",
                table: "SurveyMatrixSections",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SurveyQuestions_SurveyMatrixSections_MatrixSectionId",
                table: "SurveyQuestions",
                column: "MatrixSectionId",
                principalTable: "SurveyMatrixSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SurveyQuestions_SurveyMatrixSections_MatrixSectionId",
                table: "SurveyQuestions");

            migrationBuilder.DropTable(
                name: "SurveyMatrixSections");

            migrationBuilder.DropIndex(
                name: "IX_SurveyQuestions_MatrixSectionId",
                table: "SurveyQuestions");

            migrationBuilder.DropColumn(
                name: "MatrixSectionId",
                table: "SurveyQuestions");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "SurveyQuestions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
