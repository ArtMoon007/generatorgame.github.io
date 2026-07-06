using GeneratorGame.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneratorGame.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260706162000_FixAchievementDateColumnsForPostgres")]
public partial class FixAchievementDateColumnsForPostgres : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("Npgsql"))
        {
            return;
        }

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'UserAchievements'
                      AND column_name = 'UnlockedAt'
                      AND data_type = 'text'
                ) THEN
                    ALTER TABLE "UserAchievements"
                    ALTER COLUMN "UnlockedAt" TYPE timestamp with time zone
                    USING COALESCE(NULLIF("UnlockedAt", '')::timestamp with time zone, now());
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'UserStats'
                      AND column_name = 'UpdatedAt'
                      AND data_type = 'text'
                ) THEN
                    ALTER TABLE "UserStats"
                    ALTER COLUMN "UpdatedAt" TYPE timestamp with time zone
                    USING COALESCE(NULLIF("UpdatedAt", '')::timestamp with time zone, now());
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("Npgsql"))
        {
            return;
        }

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'UserAchievements'
                      AND column_name = 'UnlockedAt'
                      AND data_type LIKE 'timestamp%'
                ) THEN
                    ALTER TABLE "UserAchievements"
                    ALTER COLUMN "UnlockedAt" TYPE text
                    USING "UnlockedAt"::text;
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_name = 'UserStats'
                      AND column_name = 'UpdatedAt'
                      AND data_type LIKE 'timestamp%'
                ) THEN
                    ALTER TABLE "UserStats"
                    ALTER COLUMN "UpdatedAt" TYPE text
                    USING "UpdatedAt"::text;
                END IF;
            END $$;
            """);
    }
}
