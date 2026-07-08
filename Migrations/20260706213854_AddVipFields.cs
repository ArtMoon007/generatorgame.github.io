using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneratorGame.Migrations
{
    /// <inheritdoc />
    public partial class AddVipFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "HideStatsFromOthers" boolean NOT NULL DEFAULT false;
                    ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "VipUntil" timestamp with time zone NULL;
                    """);
                return;
            }

            migrationBuilder.AddColumn<bool>(
                name: "HideStatsFromOthers",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VipUntil",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("""
                    ALTER TABLE "Users" DROP COLUMN IF EXISTS "HideStatsFromOthers";
                    ALTER TABLE "Users" DROP COLUMN IF EXISTS "VipUntil";
                    """);
                return;
            }

            migrationBuilder.DropColumn(
                name: "HideStatsFromOthers",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VipUntil",
                table: "Users");
        }
    }
}
