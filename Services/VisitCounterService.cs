using GeneratorGame.Data;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public class VisitCounterService
{
    private const string CounterKey = "site_visits";
    private const int OnlineWindowSeconds = 30;
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

                CREATE TABLE IF NOT EXISTS "SitePresence" (
                    "ClientId" text PRIMARY KEY,
                    "LastSeen" timestamp with time zone NOT NULL
                );

                INSERT INTO "SiteCounters" ("Key", "Value")
                VALUES ('site_visits', 0)
                ON CONFLICT ("Key") DO NOTHING;

                CREATE INDEX IF NOT EXISTS "IX_SitePresence_LastSeen"
                    ON "SitePresence" ("LastSeen");
                """);
            return;
        }

        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SiteCounters" (
                "Key" TEXT PRIMARY KEY,
                "Value" INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS "SitePresence" (
                "ClientId" TEXT PRIMARY KEY,
                "LastSeen" TEXT NOT NULL
            );

            INSERT OR IGNORE INTO "SiteCounters" ("Key", "Value")
            VALUES ('site_visits', 0);

            CREATE INDEX IF NOT EXISTS "IX_SitePresence_LastSeen"
                ON "SitePresence" ("LastSeen");
            """);
    }

    public async Task<SiteTrafficSnapshot> GetSnapshotAsync()
    {
        await EnsureSchemaAsync();

        if (_db.Database.IsNpgsql())
        {
            var visits = await _db.Database
                .SqlQueryRaw<long>("SELECT \"Value\" AS \"Value\" FROM \"SiteCounters\" WHERE \"Key\" = 'site_visits'")
                .SingleAsync();

            var online = await _db.Database
                .SqlQueryRaw<int>("""
                    SELECT count(*)::int AS "Value"
                    FROM "SitePresence"
                    WHERE "LastSeen" >= now() - interval '30 seconds'
                    """)
                .SingleAsync();

            return new SiteTrafficSnapshot(online, visits);
        }

        var sqliteVisits = await _db.Database
            .SqlQueryRaw<long>("SELECT \"Value\" AS \"Value\" FROM \"SiteCounters\" WHERE \"Key\" = 'site_visits'")
            .SingleAsync();

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-OnlineWindowSeconds).ToString("O");
        var sqliteOnline = await _db.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS "Value"
                FROM "SitePresence"
                WHERE "LastSeen" >= {cutoff}
                """)
            .SingleAsync();

        return new SiteTrafficSnapshot(sqliteOnline, sqliteVisits);
    }

    public async Task<SiteTrafficSnapshot> RegisterVisitAsync(string clientId, bool countVisit)
    {
        await EnsureSchemaAsync();

        clientId = string.IsNullOrWhiteSpace(clientId)
            ? Guid.NewGuid().ToString("N")
            : clientId.Trim();

        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "SitePresence" ("ClientId", "LastSeen")
                VALUES ({clientId}, now())
                ON CONFLICT ("ClientId")
                DO UPDATE SET "LastSeen" = EXCLUDED."LastSeen";
                """);
        }
        else
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            await _db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "SitePresence" ("ClientId", "LastSeen")
                VALUES ({clientId}, {now})
                ON CONFLICT ("ClientId")
                DO UPDATE SET "LastSeen" = excluded."LastSeen";
                """);
        }

        if (countVisit)
        {
            if (_db.Database.IsNpgsql())
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    UPDATE "SiteCounters"
                    SET "Value" = "Value" + 1
                    WHERE "Key" = 'site_visits'
                    """);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    UPDATE "SiteCounters"
                    SET "Value" = "Value" + 1
                    WHERE "Key" = 'site_visits'
                    """);
            }
        }

        return await GetSnapshotAsync();
    }
}

public record SiteTrafficSnapshot(int Online, long Visits);
