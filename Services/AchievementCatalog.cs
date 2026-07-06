namespace GeneratorGame.Services;

public static class AchievementCatalog
{
    public static readonly IReadOnlyList<AchievementDefinition> All = new[]
    {
        new AchievementDefinition("first_game", "Первый запуск", "Сыграй первую игру", "⚡", 80),
        new AchievementDefinition("five_games", "Разогрев", "Сыграй 5 игр", "🔥", 120),
        new AchievementDefinition("twenty_games", "Ночной марафон", "Сыграй 20 игр", "🌙", 240),
        new AchievementDefinition("first_bbn", "Bite by Night", "Заверши Байт бай найт", "🟥", 100),
        new AchievementDefinition("first_forsaken", "Forsaken", "Заверши Форсакен", "🔌", 100),
        new AchievementDefinition("bbn_top_10", "Про в Bite by Night", "Займи топ 10 в Bite by Night", "10", 260),
        new AchievementDefinition("forsaken_top_10", "Про в Forsaken", "Займи топ 10 в Форсакен", "10", 260),
        new AchievementDefinition("sub_30", "Быстрые руки", "Получи результат быстрее 30 секунд", "⏱", 120),
        new AchievementDefinition("sub_10", "Почти молния", "Получи результат быстрее 10 секунд", "⚡", 180),
        new AchievementDefinition("sub_5", "Невозможный темп", "Получи результат быстрее 5 секунд", "💥", 300),
        new AchievementDefinition("level_5", "Пятый уровень", "Достигни 5 уровня", "V", 120),
        new AchievementDefinition("level_10", "Десятый уровень", "Достигни 10 уровня", "X", 200)
    };

    public static AchievementDefinition? Get(string key) =>
        All.FirstOrDefault(a => a.Key == key);
}

public record AchievementDefinition(
    string Key,
    string Title,
    string Description,
    string Icon,
    int Experience);
