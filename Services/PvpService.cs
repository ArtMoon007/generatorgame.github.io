using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public class PvpService
{
    public static readonly string[] AllowedGenerators = ["bitebynight", "forsaken"];
    private static readonly PvpRank[] Ranks =
    [
        new("Дерево", 0, 0, 50),
        new("Бронза", 1, 50, 150),
        new("Серебро", 2, 150, 400),
        new("Золото", 3, 400, 700),
        new("Алмаз", 4, 700, 1000),
        new("Сапфир", 5, 1000, 1500),
        new("Легенда", 6, 1500, null)
    ];

    private readonly AppDbContext _db;

    public PvpService(AppDbContext db)
    {
        _db = db;
    }

    public static bool IsAllowedGenerator(string generator) =>
        AllowedGenerators.Contains(generator, StringComparer.OrdinalIgnoreCase);

    public static PvpRank GetRank(int points)
    {
        if (points >= 1500) return new("Легенда", 6, 1500, null);
        if (points >= 1000) return new("Сапфир", 5, 1000, 1500);
        if (points >= 700) return new("Алмаз", 4, 700, 1000);
        if (points >= 400) return new("Золото", 3, 400, 700);
        if (points >= 150) return new("Серебро", 2, 150, 400);
        if (points >= 50) return new("Бронза", 1, 50, 150);
        return new("Дерево", 0, 0, 50);
    }

    public async Task<PvpRating> GetOrCreateRatingAsync(int userId, string generator)
    {
        generator = NormalizeGenerator(generator);
        var rating = await _db.PvpRatings.FirstOrDefaultAsync(r => r.UserId == userId && r.Generator == generator);
        if (rating != null) return rating;

        rating = new PvpRating
        {
            UserId = userId,
            Generator = generator,
            Points = 0,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PvpRatings.Add(rating);
        await _db.SaveChangesAsync();
        return rating;
    }

    public async Task<IReadOnlyList<PvpRatingView>> GetRatingsAsync(int userId)
    {
        var result = new List<PvpRatingView>();
        foreach (var generator in AllowedGenerators)
        {
            var rating = await GetOrCreateRatingAsync(userId, generator);
            var rank = GetRank(rating.Points);
            result.Add(new PvpRatingView(generator, GeneratorTitle(generator), rating.Points, rank.Name, rank.Index, RankIcon(rank.Index), RankCss(rank.Index), rating.Wins, rating.Losses));
        }

        return result;
    }

    public async Task<int> GetQueueCountAsync(int userId, string generator)
    {
        generator = NormalizeGenerator(generator);
        var rating = await GetOrCreateRatingAsync(userId, generator);
        var rank = GetRank(rating.Points);
        var staleBefore = DateTime.UtcNow.AddMinutes(-5);

        await _db.PvpQueueEntries
            .Where(q => q.CreatedAt < staleBefore)
            .ExecuteDeleteAsync();

        return await _db.PvpQueueEntries.CountAsync(q =>
            q.Generator == generator &&
            q.UserId != userId &&
            Math.Abs(q.RankIndex - rank.Index) <= 1);
    }

    public async Task<PvpSearchResult> SearchAsync(int userId, string generator)
    {
        generator = NormalizeGenerator(generator);
        var rating = await GetOrCreateRatingAsync(userId, generator);
        var rank = GetRank(rating.Points);
        var now = DateTime.UtcNow;
        var staleBefore = now.AddMinutes(-5);

        await _db.PvpQueueEntries
            .Where(q => q.CreatedAt < staleBefore)
            .ExecuteDeleteAsync();

        var activeMatch = await GetActiveMatchAsync(userId, generator);
        if (activeMatch != null)
        {
            var count = await GetQueueCountAsync(userId, generator);
            return new PvpSearchResult(true, activeMatch.Id, "match_found", count);
        }

        var opponent = await _db.PvpQueueEntries
            .Where(q => q.Generator == generator &&
                        q.UserId != userId &&
                        Math.Abs(q.RankIndex - rank.Index) <= 1)
            .OrderBy(q => q.CreatedAt)
            .FirstOrDefaultAsync();

        if (opponent != null)
        {
            var match = new PvpMatch
            {
                Generator = generator,
                Player1Id = opponent.UserId,
                Player2Id = userId,
                Status = "waiting_ready",
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.PvpMatches.Add(match);
            _db.PvpQueueEntries.Remove(opponent);

            var myQueue = await _db.PvpQueueEntries.FirstOrDefaultAsync(q => q.UserId == userId && q.Generator == generator);
            if (myQueue != null) _db.PvpQueueEntries.Remove(myQueue);

            await _db.SaveChangesAsync();
            return new PvpSearchResult(true, match.Id, "match_found", 0);
        }

        var existingQueue = await _db.PvpQueueEntries.FirstOrDefaultAsync(q => q.UserId == userId && q.Generator == generator);
        if (existingQueue == null)
        {
            _db.PvpQueueEntries.Add(new PvpQueueEntry
            {
                UserId = userId,
                Generator = generator,
                RankIndex = rank.Index,
                CreatedAt = now
            });
        }
        else
        {
            existingQueue.RankIndex = rank.Index;
            existingQueue.CreatedAt = now;
        }

        await _db.SaveChangesAsync();
        var queueCount = await GetQueueCountAsync(userId, generator);
        return new PvpSearchResult(false, null, "searching", queueCount);
    }

    public async Task CancelSearchAsync(int userId, string generator)
    {
        generator = NormalizeGenerator(generator);
        await _db.PvpQueueEntries
            .Where(q => q.UserId == userId && q.Generator == generator)
            .ExecuteDeleteAsync();
    }

    public async Task<PvpInviteSendResult> SendInviteAsync(int senderUserId, string targetNickname, string generator)
    {
        generator = NormalizeGenerator(generator);
        var nickname = (targetNickname ?? string.Empty).Trim();
        if (nickname.Length < 2) return new(false, "Введите ник игрока", null);

        var target = await _db.Users
            .Where(u => u.Id != senderUserId &&
                        (u.Username.ToLower() == nickname.ToLower() ||
                         (u.RobloxUsername != null && u.RobloxUsername.ToLower() == nickname.ToLower())))
            .Select(u => new { u.Id, u.Username, u.RobloxUsername })
            .FirstOrDefaultAsync();

        if (target == null) return new(false, "Игрок не найден", null);

        var now = DateTime.UtcNow;
        await _db.PvpDuelInvites
            .Where(i => i.ExpiresAt < now && i.Status == "pending")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "expired"));

        var existing = await _db.PvpDuelInvites.FirstOrDefaultAsync(i =>
            i.SenderUserId == senderUserId &&
            i.TargetUserId == target.Id &&
            i.Generator == generator &&
            i.Status == "pending" &&
            i.ExpiresAt > now);

        if (existing != null)
        {
            existing.ExpiresAt = now.AddMinutes(2);
            await _db.SaveChangesAsync();
            return new(true, "Приглашение уже отправлено", existing.Id);
        }

        var invite = new PvpDuelInvite
        {
            SenderUserId = senderUserId,
            TargetUserId = target.Id,
            Generator = generator,
            Status = "pending",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(2)
        };

        _db.PvpDuelInvites.Add(invite);
        await _db.SaveChangesAsync();
        return new(true, $"Приглашение отправлено игроку {target.RobloxUsername ?? target.Username}", invite.Id);
    }

    public async Task<PvpInviteView?> GetPendingInviteAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await _db.PvpDuelInvites
            .Where(i => i.ExpiresAt < now && i.Status == "pending")
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, "expired"));

        var invite = await _db.PvpDuelInvites
            .Where(i => i.TargetUserId == userId && i.Status == "pending" && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .Join(_db.Users,
                i => i.SenderUserId,
                u => u.Id,
                (i, u) => new
                {
                    i.Id,
                    i.Generator,
                    i.ExpiresAt,
                    SenderName = u.RobloxUsername ?? u.Username
                })
            .FirstOrDefaultAsync();

        return invite == null
            ? null
            : new PvpInviteView(invite.Id, invite.SenderName, invite.Generator, GeneratorTitle(invite.Generator), invite.ExpiresAt);
    }

    public async Task<PvpInviteResponseResult?> RespondInviteAsync(int userId, int inviteId, bool accept)
    {
        var invite = await _db.PvpDuelInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.TargetUserId == userId);
        if (invite == null || invite.Status != "pending") return null;

        if (!accept)
        {
            invite.Status = "declined";
            await _db.SaveChangesAsync();
            return new(false, null, null);
        }

        if (invite.ExpiresAt <= DateTime.UtcNow)
        {
            invite.Status = "expired";
            await _db.SaveChangesAsync();
            return new(false, null, null);
        }

        await _db.PvpQueueEntries
            .Where(q => q.UserId == invite.SenderUserId || q.UserId == invite.TargetUserId)
            .ExecuteDeleteAsync();

        var match = new PvpMatch
        {
            Generator = invite.Generator,
            Player1Id = invite.SenderUserId,
            Player2Id = invite.TargetUserId,
            Status = "waiting_ready",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PvpMatches.Add(match);
        await _db.SaveChangesAsync();

        invite.Status = "accepted";
        invite.MatchId = match.Id;
        await _db.SaveChangesAsync();

        return new(true, match.Id, await BuildMatchViewAsync(match, userId));
    }

    public async Task<PvpMatchView?> GetActiveMatchViewAsync(int userId, string? generator = null)
    {
        var match = await GetActiveMatchAsync(userId, generator);
        if (match == null) return null;
        await ApplyTimeoutAsync(match);
        return await BuildMatchViewAsync(match, userId);
    }

    public async Task<PvpMatchView?> ForfeitAsync(int userId, int matchId)
    {
        var match = await _db.PvpMatches.FirstOrDefaultAsync(m => m.Id == matchId && (m.Player1Id == userId || m.Player2Id == userId));
        if (match == null || match.Status == "complete") return null;

        if (match.Player1Id == userId)
        {
            match.Player2Wins = Math.Max(match.Player2Wins, 2);
        }
        else
        {
            match.Player1Wins = Math.Max(match.Player1Wins, 2);
        }

        match.Status = "complete";
        match.UpdatedAt = DateTime.UtcNow;
        await ApplyRatingAsync(match);
        await _db.SaveChangesAsync();

        return await BuildMatchViewAsync(match, userId);
    }

    public async Task<PvpMatchView?> ReadyAsync(int userId, int matchId)
    {
        var match = await _db.PvpMatches.FirstOrDefaultAsync(m => m.Id == matchId && (m.Player1Id == userId || m.Player2Id == userId));
        if (match == null || match.Status == "complete") return null;

        if (match.Player1Id == userId) match.Player1Ready = true;
        if (match.Player2Id == userId) match.Player2Ready = true;

        if (match.Player1Ready && match.Player2Ready)
        {
            match.Status = "in_round";
            match.UpdatedAt = DateTime.UtcNow;
            await EnsureRoundAsync(match.Id, match.CurrentRound);
        }

        await _db.SaveChangesAsync();
        return await BuildMatchViewAsync(match, userId);
    }

    private async Task ApplyTimeoutAsync(PvpMatch match)
    {
        if (match.Status == "complete") return;
        var now = DateTime.UtcNow;

        if (match.Status == "waiting_ready" && (match.Player1Ready ^ match.Player2Ready) && now - match.UpdatedAt > TimeSpan.FromMinutes(2))
        {
            if (match.Player1Ready) match.Player1Wins = Math.Max(match.Player1Wins, 2);
            else match.Player2Wins = Math.Max(match.Player2Wins, 2);

            match.Status = "complete";
            match.UpdatedAt = now;
            await ApplyRatingAsync(match);
            await _db.SaveChangesAsync();
            return;
        }

        if (match.Status == "in_round")
        {
            var round = await _db.PvpRounds.FirstOrDefaultAsync(r => r.MatchId == match.Id && r.RoundNumber == match.CurrentRound);
            if (round == null) return;
            var onlyPlayer1Submitted = round.Player1TimeMs.HasValue && !round.Player2TimeMs.HasValue;
            var onlyPlayer2Submitted = round.Player2TimeMs.HasValue && !round.Player1TimeMs.HasValue;
            if ((onlyPlayer1Submitted || onlyPlayer2Submitted) && now - round.UpdatedAt > TimeSpan.FromMinutes(2))
            {
                if (onlyPlayer1Submitted) match.Player1Wins = Math.Max(match.Player1Wins, 2);
                else match.Player2Wins = Math.Max(match.Player2Wins, 2);

                round.WinnerUserId = onlyPlayer1Submitted ? match.Player1Id : match.Player2Id;
                match.Status = "complete";
                match.UpdatedAt = now;
                round.UpdatedAt = now;
                await ApplyRatingAsync(match);
                await _db.SaveChangesAsync();
            }
        }
    }

    public async Task<PvpMatchView?> SubmitRoundAsync(int userId, int matchId, long timeMs)
    {
        var match = await _db.PvpMatches.FirstOrDefaultAsync(m => m.Id == matchId && (m.Player1Id == userId || m.Player2Id == userId));
        if (match == null || match.Status == "complete") return null;

        var round = await EnsureRoundAsync(match.Id, match.CurrentRound);
        if (match.Player1Id == userId) round.Player1TimeMs ??= timeMs;
        if (match.Player2Id == userId) round.Player2TimeMs ??= timeMs;

        if (round.Player1TimeMs.HasValue && round.Player2TimeMs.HasValue && round.WinnerUserId == null)
        {
            round.WinnerUserId = round.Player1TimeMs.Value <= round.Player2TimeMs.Value
                ? match.Player1Id
                : match.Player2Id;

            if (round.WinnerUserId == match.Player1Id) match.Player1Wins++;
            else match.Player2Wins++;

            if (match.Player1Wins >= 2 || match.Player2Wins >= 2)
            {
                match.Status = "complete";
                await ApplyRatingAsync(match);
            }
            else
            {
                match.CurrentRound++;
                match.Player1Ready = false;
                match.Player2Ready = false;
                match.Status = "waiting_ready";
            }
        }

        round.UpdatedAt = DateTime.UtcNow;
        match.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await BuildMatchViewAsync(match, userId);
    }

    private async Task ApplyRatingAsync(PvpMatch match)
    {
        if (match.RatingApplied) return;

        var winnerId = match.Player1Wins > match.Player2Wins ? match.Player1Id : match.Player2Id;
        var loserId = winnerId == match.Player1Id ? match.Player2Id : match.Player1Id;

        var winner = await GetOrCreateRatingAsync(winnerId, match.Generator);
        var loser = await GetOrCreateRatingAsync(loserId, match.Generator);
        var winnerDelta = RollCupDelta(GetRank(winner.Points).Index, true);
        var loserDelta = RollCupDelta(GetRank(loser.Points).Index, false);

        if (winnerId == match.Player1Id)
        {
            match.Player1CupDelta = winnerDelta;
            match.Player2CupDelta = -loserDelta;
        }
        else
        {
            match.Player1CupDelta = -loserDelta;
            match.Player2CupDelta = winnerDelta;
        }

        winner.Points += winnerDelta;
        winner.Wins++;
        winner.UpdatedAt = DateTime.UtcNow;

        loser.Points = Math.Max(0, loser.Points - loserDelta);
        loser.Losses++;
        loser.UpdatedAt = DateTime.UtcNow;

        match.RatingApplied = true;
    }

    private static int RollCupDelta(int rankIndex, bool won)
    {
        var range = (rankIndex, won) switch
        {
            (0, true) => (32, 40),
            (0, false) => (6, 12),
            (1, true) => (27, 34),
            (1, false) => (14, 21),
            (2, true) => (22, 27),
            (2, false) => (24, 29),
            (3, true) => (17, 22),
            (3, false) => (24, 28),
            (4, true) => (14, 19),
            (4, false) => (24, 28),
            (5, true) => (13, 18),
            (5, false) => (34, 44),
            (_, true) => (10, 15),
            (_, false) => (38, 50)
        };

        return Random.Shared.Next(range.Item1, range.Item2 + 1);
    }

    private async Task<PvpRound> EnsureRoundAsync(int matchId, int roundNumber)
    {
        var round = await _db.PvpRounds.FirstOrDefaultAsync(r => r.MatchId == matchId && r.RoundNumber == roundNumber);
        if (round != null) return round;

        round = new PvpRound
        {
            MatchId = matchId,
            RoundNumber = roundNumber,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PvpRounds.Add(round);
        return round;
    }

    private async Task<PvpMatch?> GetActiveMatchAsync(int userId, string? generator = null)
    {
        var query = _db.PvpMatches
            .Where(m => m.Status != "complete" && (m.Player1Id == userId || m.Player2Id == userId));

        if (!string.IsNullOrWhiteSpace(generator))
        {
            var normalized = NormalizeGenerator(generator);
            query = query.Where(m => m.Generator == normalized);
        }

        return await query.OrderByDescending(m => m.UpdatedAt).FirstOrDefaultAsync();
    }

    private async Task<PvpMatchView> BuildMatchViewAsync(PvpMatch match, int userId)
    {
        var opponentId = match.Player1Id == userId ? match.Player2Id : match.Player1Id;
        var opponent = await _db.Users
            .Where(u => u.Id == opponentId)
            .Select(u => new { u.Username, u.RobloxUsername })
            .FirstOrDefaultAsync();

        var displayRoundNumber = match.Status == "waiting_ready" && match.CurrentRound > 1
            ? match.CurrentRound - 1
            : match.CurrentRound;

        if (match.Status == "complete")
        {
            displayRoundNumber = await _db.PvpRounds
                .Where(r => r.MatchId == match.Id)
                .MaxAsync(r => (int?)r.RoundNumber) ?? match.CurrentRound;
        }

        var displayRound = await _db.PvpRounds
            .Where(r => r.MatchId == match.Id && r.RoundNumber == displayRoundNumber)
            .Select(r => new
            {
                MyTime = match.Player1Id == userId ? r.Player1TimeMs : r.Player2TimeMs,
                OpponentTime = match.Player1Id == userId ? r.Player2TimeMs : r.Player1TimeMs,
                r.WinnerUserId
            })
            .FirstOrDefaultAsync();

        var myWins = match.Player1Id == userId ? match.Player1Wins : match.Player2Wins;
        var opponentWins = match.Player1Id == userId ? match.Player2Wins : match.Player1Wins;
        var myReady = match.Player1Id == userId ? match.Player1Ready : match.Player2Ready;
        var opponentReady = match.Player1Id == userId ? match.Player2Ready : match.Player1Ready;
        var winnerId = match.Status == "complete"
            ? (match.Player1Wins > match.Player2Wins ? match.Player1Id : match.Player2Id)
            : (int?)null;
        var myRating = await GetOrCreateRatingAsync(userId, match.Generator);
        var myRank = GetRank(myRating.Points);
        var nextRank = Ranks.FirstOrDefault(r => r.Index == myRank.Index + 1);
        var rankProgress = nextRank == null
            ? 100
            : Math.Clamp((int)Math.Round((myRating.Points - myRank.MinPoints) * 100.0 / (nextRank.MinPoints - myRank.MinPoints)), 0, 100);
        var pointsToNextRank = nextRank == null ? 0 : Math.Max(0, nextRank.MinPoints - myRating.Points);
        var myCupDelta = match.Player1Id == userId ? match.Player1CupDelta : match.Player2CupDelta;

        return new PvpMatchView(
            match.Id,
            match.Generator,
            GeneratorTitle(match.Generator),
            match.Status,
            match.CurrentRound,
            opponent?.RobloxUsername ?? opponent?.Username ?? "Opponent",
            myWins,
            opponentWins,
            myReady,
            opponentReady,
            displayRound?.MyTime,
            displayRound?.OpponentTime,
            displayRound?.WinnerUserId,
            winnerId,
            winnerId == userId,
            myCupDelta,
            myRating.Points,
            myRank.Name,
            nextRank?.Name,
            rankProgress,
            pointsToNextRank);
    }

    private static string NormalizeGenerator(string generator) =>
        generator.Trim().ToLowerInvariant() switch
        {
            "forsaken" => "forsaken",
            _ => "bitebynight"
        };

    public static string GeneratorTitle(string generator) =>
        generator == "forsaken" ? "Forsaken" : "Bite by Night";

    public static string RankIcon(int rankIndex) => rankIndex switch
    {
        0 => "🪵",
        1 => "🥉",
        2 => "🥈",
        3 => "🥇",
        4 => "💎",
        5 => "🔷",
        _ => "👑"
    };

    public static string RankCss(int rankIndex) => rankIndex switch
    {
        0 => "wood",
        1 => "bronze",
        2 => "silver",
        3 => "gold",
        4 => "diamond",
        5 => "sapphire",
        _ => "legend"
    };
}

public record PvpRank(string Name, int Index, int MinPoints, int? MaxPoints);
public record PvpRatingView(string Generator, string Title, int Points, string Rank, int RankIndex, string RankIcon, string RankCss, int Wins, int Losses);
public record PvpSearchResult(bool Matched, int? MatchId, string Status, int QueueCount);
public record PvpInviteSendResult(bool Ok, string Message, int? InviteId);
public record PvpInviteView(int Id, string SenderName, string Generator, string GeneratorTitle, DateTime ExpiresAt);
public record PvpInviteResponseResult(bool Accepted, int? MatchId, PvpMatchView? Match);
public record PvpMatchView(
    int Id,
    string Generator,
    string GeneratorTitle,
    string Status,
    int CurrentRound,
    string OpponentName,
    int MyWins,
    int OpponentWins,
    bool MyReady,
    bool OpponentReady,
    long? MyTimeMs,
    long? OpponentTimeMs,
    int? RoundWinnerUserId,
    int? MatchWinnerUserId,
    bool IWonMatch,
    int MyCupDelta,
    int MyPoints,
    string MyRank,
    string? NextRank,
    int RankProgressPercent,
    int PointsToNextRank);
