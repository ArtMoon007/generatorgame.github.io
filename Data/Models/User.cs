namespace GeneratorGame.Data.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;
    public DateTime? UsernameChangedAt { get; set; }

    public string? Email { get; set; }

    // 🔐 ЛОКАЛЬНЫЙ ВХОД
    public string? PasswordHash { get; set; }

    // 🤖 ROBLOX LOGIN
    public string? RobloxId { get; set; }
    public string? RobloxUsername { get; set; }
    public string? RobloxAvatarUrl { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime? VipUntil { get; set; }
    public bool HideStatsFromOthers { get; set; }

    // 🎮 СКОРОСТЬ / СЧЕТ
    public List<Score> Scores { get; set; } = new();
    public UserStat? Stat { get; set; }
    public List<UserAchievement> Achievements { get; set; } = new();
}
