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
        new AchievementDefinition("level_10", "Десятый уровень", "Достигни 10 уровня", "X", 200),
        new AchievementDefinition("games_100", "100 игр", "Сыграй 100 игр", "100", 360),
        new AchievementDefinition("games_250", "250 игр", "Сыграй 250 игр", "250", 600),
        new AchievementDefinition("games_500", "500 игр", "Сыграй 500 игр", "500", 1000),
        new AchievementDefinition("hours_1", "Первый час", "Проведи 1 час в игре", "1H", 320),
        new AchievementDefinition("hours_5", "Пять часов", "Проведи 5 часов в игре", "5H", 800),
        new AchievementDefinition("pvp_wood", "PVP Дерево", "Получи звание Дерево в PVP", "🪵", 100),
        new AchievementDefinition("pvp_bronze", "PVP Бронза", "Получи звание Бронза в PVP", "🥉", 180),
        new AchievementDefinition("pvp_silver", "PVP Серебро", "Получи звание Серебро в PVP", "🥈", 260),
        new AchievementDefinition("pvp_gold", "PVP Золото", "Получи звание Золото в PVP", "🥇", 420),
        new AchievementDefinition("pvp_diamond", "PVP Алмаз", "Получи звание Алмаз в PVP", "💎", 620),
        new AchievementDefinition("pvp_sapphire", "PVP Сапфир", "Получи звание Сапфир в PVP", "🔷", 820),
        new AchievementDefinition("pvp_legend", "PVP Легенда", "Получи звание Легенда в PVP", "👑", 1200)
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
