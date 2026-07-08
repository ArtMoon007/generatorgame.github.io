using GeneratorGame.Data;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public static class ProfileDatabaseInitializer
{
    public static async Task EnsureSchemaAsync(AppDbContext db)
    {
        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "UsernameChangedAt" timestamp with time zone NULL;
                """);
            return;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Users" ADD COLUMN "UsernameChangedAt" TEXT NULL;""");
        }
        catch
        {
            // SQLite throws when the column already exists.
        }
    }
}
