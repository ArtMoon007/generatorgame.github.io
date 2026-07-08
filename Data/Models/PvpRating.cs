namespace GeneratorGame.Data.Models;

public class PvpRating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Generator { get; set; } = "bitebynight";
    public int Points { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
