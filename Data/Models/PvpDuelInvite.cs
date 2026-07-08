namespace GeneratorGame.Data.Models;

public class PvpDuelInvite
{
    public int Id { get; set; }
    public int SenderUserId { get; set; }
    public int TargetUserId { get; set; }
    public string Generator { get; set; } = "bitebynight";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(2);
    public int? MatchId { get; set; }

    public User SenderUser { get; set; } = null!;
    public User TargetUser { get; set; } = null!;
}
