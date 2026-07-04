using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneratorGame.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratorToScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Generator",
                table: "Scores",
                type: "TEXT",
                nullable: false,
                defaultValue: "bitebynight");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_Generator_TimeMs",
                table: "Scores",
                columns: new[] { "Generator", "TimeMs" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_Generator_TimeMs",
                table: "Scores");

            migrationBuilder.DropColumn(
                name: "Generator",
                table: "Scores");
        }
    }
}
