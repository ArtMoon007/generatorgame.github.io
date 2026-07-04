using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneratorGame.Migrations
{
    /// <inheritdoc />
    public partial class InitPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Generator",
                table: "Scores",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValue: "bitebynight");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Generator",
                table: "Scores",
                type: "TEXT",
                nullable: false,
                defaultValue: "bitebynight",
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
