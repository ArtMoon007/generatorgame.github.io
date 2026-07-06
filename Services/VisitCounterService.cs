using GeneratorGame.Data;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public class VisitCounterService
{
    private const string CounterKey = "site_visits";
    private readonly AppDbContext _db;

    public VisitCounterService(AppDbContext db)
    {
        _db = db;
    }

    public async Task EnsureSchemaAsync()
    {
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "SiteCounters" (
                    "Key" text PRIMARY KEY,
                    "Value" bigint NOT NULL DEFAULT 0
                );

                INSERT INTO "SiteCounters" ("Key", "Value")
                VALUES ('site_visits', 0)
                ON CONFLICT ("Key") DO NOTHING;
                """);
            return;
        }

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SiteCounters" (
                "Key" TEXT PRIMARY KEY,
                "Value" INTEGER NOT NULL DEFAULT 0
            );

            INSERT OR IGNORE INTO "SiteCounters" ("Key", "Value")
            VALUES ('site_visits', 0);
            """);
    }

    public async Task<long> GetVisitsAsync()
    {
        await EnsureSchemaAsync();

        if (_db.Database.IsNpgsql())
        {
            return await _db.Database
                .SqlQueryRaw<long>("SELECT \"Value\" FROM \"SiteCounters\" WHERE \"Key\" = 'site_visits'")
                .SingleAsync();
        }

        return await _db.Database
            .SqlQueryRaw<long>("SELECT \"Value\" FROM \"SiteCounters\" WHERE \"Key\" = 'site_visits'")
            .SingleAsync();
    }

    public async Task<long> RegisterVisitAsync(bool countVisit)
    {
        await EnsureSchemaAsync();

        if (countVisit)
        {
            if (_db.Database.IsNpgsql())
            {
                return await _db.Database
                    .SqlQueryRaw<long>("""
                        UPDATE "SiteCounters"
                        SET "Value" = "Value" + 1
                        WHERE "Key" = 'site_visits'
                        RETURNING "Value"
                        """)
                    .SingleAsync();
            }

            await _db.Database.ExecuteSqlRawAsync("""
                UPDATE "SiteCounters"
                SET "Value" = "Value" + 1
                WHERE "Key" = 'site_visits'
                """);
        }

        return await GetVisitsAsync();
    }
}
