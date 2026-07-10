namespace GeneratorGame.Data.Models;

public class UserStat
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public long TotalPlayTimeMs { get; set; }
    public int Experience { get; set; }
    public int Level { get; set; } = 1;
    public int Diamons { get; set; }
    public bool RainbowNameUnlocked { get; set; }
    public bool RainbowNameEnabled { get; set; }
    public bool DiamondEmojiUnlocked { get; set; }
    public bool DiamondEmojiEnabled { get; set; }
    public string FavoriteGenerator { get; set; } = "bitebynight";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
