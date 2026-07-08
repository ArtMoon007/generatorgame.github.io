using GeneratorGame.Data;
using System.Data.Common;
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

        if (!await ColumnExistsAsync(db, "Users", "UsernameChangedAt"))
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Users" ADD COLUMN "UsernameChangedAt" TEXT NULL;""");
        }
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
