namespace GeneratorGame.Data.Models;

public class UserAchievement
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Key { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Icon { get; set; } = "★";
    public int Experience { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
