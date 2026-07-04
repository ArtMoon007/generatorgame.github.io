using System.ComponentModel.DataAnnotations.Schema;

namespace GeneratorGame.Data.Models;

public class Score
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Какой генератор: "bitebynight", "forsaken" и т.д.
    public string Generator { get; set; } = "bitebynight";

    // Время в миллисекундах
    public long TimeMs { get; set; }

    // НЕ ХРАНИТСЯ В БД (важно!)
    [NotMapped]
    public string TimeFormatted =>
        $"{TimeMs / 60000:D2}:{(TimeMs % 60000) / 1000:D2}.{TimeMs % 1000:D3}";

    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}
