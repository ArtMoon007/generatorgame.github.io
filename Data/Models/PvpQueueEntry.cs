namespace GeneratorGame.Data.Models;

public class PvpQueueEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Generator { get; set; } = "bitebynight";
    public int RankIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
