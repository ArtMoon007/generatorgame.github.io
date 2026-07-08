namespace GeneratorGame.Data.Models;

public class PvpRound
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int RoundNumber { get; set; }
    public long? Player1TimeMs { get; set; }
    public long? Player2TimeMs { get; set; }
    public int? WinnerUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PvpMatch Match { get; set; } = null!;
}
