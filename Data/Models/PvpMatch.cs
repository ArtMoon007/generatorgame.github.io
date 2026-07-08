namespace GeneratorGame.Data.Models;

public class PvpMatch
{
    public int Id { get; set; }
    public string Generator { get; set; } = "bitebynight";
    public int Player1Id { get; set; }
    public int Player2Id { get; set; }
    public bool Player1Ready { get; set; }
    public bool Player2Ready { get; set; }
    public int Player1Wins { get; set; }
    public int Player2Wins { get; set; }
    public bool RatingApplied { get; set; }
    public int CurrentRound { get; set; } = 1;
    public string Status { get; set; } = "waiting_ready";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Player1 { get; set; } = null!;
    public User Player2 { get; set; } = null!;
}
