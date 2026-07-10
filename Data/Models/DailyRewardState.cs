namespace GeneratorGame.Data.Models;

public class DailyRewardState
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CurrentDay { get; set; } = 1;
    public DateTime? LastClaimedAt { get; set; }
    public DateTime CycleStartedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
