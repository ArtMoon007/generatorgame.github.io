using System.Collections.Concurrent;
using GeneratorGame.Data;
using GeneratorGame.Data.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GeneratorGame.Services;

public sealed class KillerGameHub : Hub
{
    private readonly KillerGameService _game;
    private readonly AppDbContext _db;
    public KillerGameHub(KillerGameService game, AppDbContext db) { _game = game; _db = db; }

    private async Task<(int Id, string Name, string? Avatar)> PlayerAsync()
    {
        var http = Context.GetHttpContext()!;
        var id = http.Session.GetInt32("UserId");
        if (id is null) throw new HubException("Нужно войти в аккаунт");
        var level = await _db.UserStats.Where(x => x.UserId == id.Value).Select(x => x.Level).FirstOrDefaultAsync();
        if (level < 8) throw new HubException("Мини-игра доступна только с 8 уровня");
        return (id.Value, http.Session.GetString("UserName") ?? "Игрок", http.Session.GetString("UserAvatar"));
    }

    public Task GetServers() => _game.SendServers(Context.ConnectionId);
    public async Task CreateServer(string? name, bool isPrivate) { var p = await PlayerAsync(); await _game.Create(Context.ConnectionId, p.Id, p.Name, p.Avatar, name, isPrivate); }
    public async Task JoinServer(string code) { var p = await PlayerAsync(); await _game.Join(Context.ConnectionId, p.Id, p.Name, p.Avatar, code); }
    public Task LeaveServer() => _game.Leave(Context.ConnectionId);
    public Task SetReady(bool ready) => _game.Ready(Context.ConnectionId, ready);
    public Task SetPrivate(bool isPrivate) => _game.SetPrivate(Context.ConnectionId, isPrivate);
    public Task StartTest() => _game.StartTest(Context.ConnectionId);
    public Task AskQuestion(string text) => _game.Ask(Context.ConnectionId, text);
    public Task AnswerQuestion(string text) => _game.Answer(Context.ConnectionId, text);
    public Task SendChat(string text) => _game.Chat(Context.ConnectionId, text);
    public Task OpenVoting() => _game.OpenVoting(Context.ConnectionId);
    public Task Vote(int userId) => _game.Vote(Context.ConnectionId, userId);
    public Task SkipVote() => _game.Vote(Context.ConnectionId, null);
    public override async Task OnDisconnectedAsync(Exception? exception) { await _game.Leave(Context.ConnectionId); await base.OnDisconnectedAsync(exception); }
}

public sealed class KillerGameService
{
    private readonly IHubContext<KillerGameHub> _hub;
    private readonly IServiceScopeFactory _scopes;
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _connections = new();
    private static readonly Killer[] Killers =
    [
        new("Slasher", "#df3d2f", "/images/killer-game/slasher.webp"),
        new("Noli", "#6745e8", "/images/killer-game/noli.webp"),
        new("Nosferatu", "#c72c36", "/images/killer-game/nosferatu.webp"),
        new("Jhone Doe", "#7d55d9", "/images/killer-game/jhone-doe.webp"),
        new("Guest 666", "#e11f26", "/images/killer-game/guest-666.webp"),
        new("1x1x1x1", "#50db6c", "/images/killer-game/1x1x1x1.webp"),
        new("C00lkidd", "#e84c87", "/images/killer-game/c00lkidd.webp")
    ];

    public KillerGameService(IHubContext<KillerGameHub> hub, IServiceScopeFactory scopes) { _hub = hub; _scopes = scopes; }

    public Task SendServers(string connection) => _hub.Clients.Client(connection).SendAsync("Servers", ServerList());

    public async Task Create(string connection, int id, string name, string? avatar, string? title, bool isPrivate)
    {
        await Leave(connection);
        var code = MakeCode();
        var room = new Room { Code = code, Name = Clean(title, 24, $"Сервер {name}"), OwnerId = id, IsPrivate = isPrivate };
        room.Players.Add(new Player(id, name, avatar, connection));
        _rooms[code] = room; _connections[connection] = code;
        await _hub.Groups.AddToGroupAsync(connection, code);
        await Publish(room); await BroadcastServers();
    }

    public async Task Join(string connection, int id, string name, string? avatar, string code)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (!_rooms.TryGetValue(code, out var room)) throw new HubException("Сервер не найден");
        lock (room.Gate)
        {
            if (room.Phase != "lobby") throw new HubException("Игра уже началась");
            if (room.Players.Count >= 4) throw new HubException("Сервер заполнен");
            if (room.Players.Any(x => x.Id == id)) throw new HubException("Вы уже на этом сервере");
            room.Players.Add(new Player(id, name, avatar, connection));
        }
        await Leave(connection);
        _connections[connection] = code;
        await _hub.Groups.AddToGroupAsync(connection, code);
        await Publish(room); await BroadcastServers();
    }

    public async Task Leave(string connection)
    {
        if (!_connections.TryRemove(connection, out var code) || !_rooms.TryGetValue(code, out var room)) return;
        lock (room.Gate)
        {
            room.Players.RemoveAll(x => x.Connection == connection);
            if (room.Players.Count > 0 && room.Players.All(x => x.Id != room.OwnerId)) room.OwnerId = room.Players[0].Id;
        }
        await _hub.Groups.RemoveFromGroupAsync(connection, code);
        if (room.Players.Count == 0) { room.Timer?.Cancel(); _rooms.TryRemove(code, out _); }
        else if (room.Phase == "lobby") await Publish(room);
        else await Finish(room, "Игра завершена: игрок вышел", null);
        await BroadcastServers();
    }

    public async Task Ready(string connection, bool ready)
    {
        var (room, player) = Find(connection);
        lock (room.Gate) { if (room.Phase != "lobby") return; player.Ready = ready; }
        await Publish(room);
        if (room.Players.Count == 4 && room.Players.All(x => x.Ready)) await Countdown(room);
    }

    public async Task SetPrivate(string connection, bool isPrivate)
    {
        var (room, player) = Find(connection);
        lock (room.Gate)
        {
            if (room.Phase != "lobby" || room.OwnerId != player.Id) throw new HubException("Только создатель может закрыть сервер");
            room.IsPrivate = isPrivate;
        }
        await Publish(room); await BroadcastServers();
    }

    public async Task StartTest(string connection)
    {
        var (room, player) = Find(connection);
        lock (room.Gate)
        {
            if (room.Phase != "lobby" || room.OwnerId != player.Id) throw new HubException("Запустить тест может только создатель");
        }
        await Countdown(room, true);
    }

    private async Task Countdown(Room room, bool testMode = false)
    {
        lock (room.Gate) { if (room.Phase != "lobby") return; room.Phase = "countdown"; room.Deadline = DateTime.UtcNow.AddSeconds(5); }
        await Publish(room); room.Timer?.Cancel(); room.Timer = new CancellationTokenSource(); var token = room.Timer.Token;
        try { await Task.Delay(5000, token); } catch { return; }
        lock (room.Gate)
        {
            if ((!testMode && room.Players.Count != 4) || room.Players.Count == 0) { room.Phase = "lobby"; return; }
            room.TestMode = testMode;
            var rng = Random.Shared; room.TraitorId = room.Players[rng.Next(room.Players.Count)].Id;
            var common = Killers[rng.Next(Killers.Length)]; Killer other; do other = Killers[rng.Next(Killers.Length)]; while (other.Name == common.Name);
            room.CommonKiller = common; room.TraitorKiller = other; room.Phase = "reveal"; room.Deadline = DateTime.UtcNow.AddSeconds(12);
            room.Turn = 0; room.Round = 1;
        }
        foreach (var p in room.Players) await _hub.Clients.Client(p.Connection).SendAsync("Role", new { killer = p.Id == room.TraitorId ? room.TraitorKiller : room.CommonKiller, isTraitor = p.Id == room.TraitorId });
        await Publish(room);
        try { await Task.Delay(12000, token); } catch { return; }
        lock (room.Gate) { room.Phase = "questions"; room.Deadline = null; }
        await Publish(room);
    }

    public async Task Ask(string connection, string text)
    {
        var (r, p) = Find(connection); text = Clean(text, 120, "");
        lock (r.Gate)
        {
            if (r.Phase != "questions" || r.Players[r.Turn].Id != p.Id || r.CurrentQuestion != null) throw new HubException("Сейчас нельзя задать вопрос");
            if (text.Length < 3) throw new HubException("Вопрос слишком короткий");
            r.CurrentQuestion = text; r.Answers.Clear();
        }
        await Publish(r);
        if (r.Players.Count == 1)
        {
            lock (r.Gate)
            {
                r.History.Add(new Qna(p.Name, r.CurrentQuestion!, []));
                r.CurrentQuestion = null; r.Turn++; r.Phase = "discussion"; r.Deadline = DateTime.UtcNow.AddMinutes(1);
            }
            await Publish(r); StartDiscussionTimer(r);
        }
    }

    public async Task Answer(string connection, string text)
    {
        var (r, p) = Find(connection); text = Clean(text, 160, ""); bool discuss = false;
        lock (r.Gate)
        {
            if (r.Phase != "questions" || r.CurrentQuestion == null || r.Players[r.Turn].Id == p.Id) throw new HubException("Сейчас нельзя отвечать");
            if (text.Length < 1) return; r.Answers[p.Id] = text;
            if (r.Answers.Count == r.Players.Count - 1)
            {
                r.History.Add(new Qna(r.Players[r.Turn].Name, r.CurrentQuestion, r.Players.Where(x => x.Id != r.Players[r.Turn].Id).Select(x => new NamedAnswer(x.Name, r.Answers[x.Id])).ToList()));
                r.CurrentQuestion = null; r.Answers.Clear(); r.Turn++;
                if (r.Turn >= r.Players.Count) { r.Phase = "discussion"; r.Deadline = DateTime.UtcNow.AddMinutes(1); discuss = true; }
            }
        }
        await Publish(r); if (discuss) StartDiscussionTimer(r);
    }

    public async Task Chat(string connection, string text)
    {
        var (r, p) = Find(connection); text = Clean(text, 220, "");
        if (r.Phase != "discussion" || text.Length == 0) return;
        await _hub.Clients.Group(r.Code).SendAsync("Chat", new { userId = p.Id, name = p.Name, text, at = DateTime.UtcNow });
    }

    public async Task Vote(string connection, int? target)
    {
        var (r, p) = Find(connection); bool resolve = false;
        lock (r.Gate)
        {
            if (r.Phase != "voting" && r.Phase != "discussion") throw new HubException("Голосование ещё не открыто");
            if (target != null && r.Players.All(x => x.Id != target)) throw new HubException("Игрок не найден");
            r.Phase = "voting"; r.Deadline ??= DateTime.UtcNow.AddSeconds(30); r.Votes[p.Id] = target; resolve = r.Votes.Count == r.Players.Count;
        }
        await Publish(r); if (resolve) await ResolveVote(r);
    }

    public async Task OpenVoting(string connection)
    {
        var (r, _) = Find(connection);
        lock (r.Gate) { if (r.Phase != "discussion") return; r.Phase = "voting"; r.Deadline = DateTime.UtcNow.AddSeconds(30); r.Timer?.Cancel(); }
        await Publish(r);
        r.Timer = new CancellationTokenSource(); var token = r.Timer.Token;
        _ = Task.Run(async () => { try { await Task.Delay(30000, token); await ResolveVote(r); } catch { } });
    }

    private void StartDiscussionTimer(Room r)
    {
        r.Timer?.Cancel(); r.Timer = new CancellationTokenSource(); var token = r.Timer.Token;
        _ = Task.Run(async () => { try { await Task.Delay(60000, token); lock (r.Gate) { if (r.Phase != "discussion") return; r.Phase = "voting"; r.Deadline = DateTime.UtcNow.AddSeconds(30); } await Publish(r); await Task.Delay(30000, token); await ResolveVote(r); } catch { } });
    }

    private async Task ResolveVote(Room r)
    {
        int? kicked;
        lock (r.Gate)
        {
            if (r.Phase != "voting") return;
            r.Phase = "resolving";
            var groups = r.Votes.GroupBy(x => x.Value).OrderByDescending(x => x.Count()).ToList();
            kicked = groups.Count > 0 && (groups.Count == 1 || groups[0].Count() > groups[1].Count()) ? groups[0].Key : null;
            r.Timer?.Cancel();
        }
        if (kicked is null) { await NextRound(r, "Голосование пропущено"); return; }
        if (kicked == r.TraitorId) { await Reward(r, r.Players.Where(x => x.Id != r.TraitorId).Select(x => x.Id), 15); await Finish(r, "Мирные нашли предателя! +15 💎 каждому", kicked); }
        else { await Reward(r, new[] { r.TraitorId }, 50); await Finish(r, "Предатель победил и получил 50 💎", kicked); }
    }

    private async Task NextRound(Room r, string message)
    {
        lock (r.Gate) { r.Phase = "questions"; r.Round++; r.Turn = 0; r.Votes.Clear(); r.CurrentQuestion = null; r.Deadline = null; }
        await _hub.Clients.Group(r.Code).SendAsync("Notice", message); await Publish(r);
    }

    private async Task Reward(Room r, IEnumerable<int> ids, int amount)
    {
        await using var scope = _scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var id in ids)
        {
            var stat = await db.UserStats.FirstOrDefaultAsync(x => x.UserId == id);
            if (stat == null) { stat = new UserStat { UserId = id }; db.UserStats.Add(stat); }
            stat.Diamons += amount; stat.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private async Task Finish(Room r, string message, int? kicked)
    {
        lock (r.Gate) { r.Phase = "finished"; r.Deadline = null; r.Timer?.Cancel(); }
        await _hub.Clients.Group(r.Code).SendAsync("GameOver", new { message, kicked, traitorId = r.TraitorId, traitorName = r.Players.FirstOrDefault(x => x.Id == r.TraitorId)?.Name }); await Publish(r);
        _ = Task.Run(async () => { await Task.Delay(10000); lock (r.Gate) { r.Phase = "lobby"; r.Players.ForEach(x => x.Ready = false); r.Votes.Clear(); r.History.Clear(); } await Publish(r); await BroadcastServers(); });
    }

    private (Room, Player) Find(string connection)
    {
        if (!_connections.TryGetValue(connection, out var code) || !_rooms.TryGetValue(code, out var room)) throw new HubException("Вы не на сервере");
        return (room, room.Players.First(x => x.Connection == connection));
    }

    private async Task Publish(Room r)
    {
        object state; lock (r.Gate) state = new { r.Code, r.Name, r.Phase, r.Round, r.Deadline, r.OwnerId, r.IsPrivate, r.TestMode, players = r.Players.Select(x => new { x.Id, x.Name, x.Avatar, x.Ready }).ToList(), turnUserId = r.Phase == "questions" && r.Turn < r.Players.Count ? r.Players[r.Turn].Id : (int?)null, r.CurrentQuestion, answers = r.Answers.Select(x => new { userId = x.Key, text = x.Value }).ToList(), r.History, votesCast = r.Votes.Keys.ToList() };
        await _hub.Clients.Group(r.Code).SendAsync("State", state);
    }
    private object[] ServerList() => _rooms.Values.Where(x => x.Phase == "lobby" && !x.IsPrivate).Select(x => new { x.Code, x.Name, players = x.Players.Count, capacity = 4 }).ToArray();
    private Task BroadcastServers() => _hub.Clients.All.SendAsync("Servers", ServerList());
    private string MakeCode() { string c; do c = Random.Shared.Next(100000, 999999).ToString(); while (_rooms.ContainsKey(c)); return c; }
    private static string Clean(string? s, int max, string fallback) { s = (s ?? "").Trim(); if (s.Length > max) s = s[..max]; return s.Length == 0 ? fallback : s; }

    private sealed class Room { public object Gate { get; } = new(); public string Code=""; public string Name=""; public string Phase="lobby"; public List<Player> Players=[]; public int OwnerId; public bool IsPrivate; public bool TestMode; public int TraitorId; public Killer? CommonKiller; public Killer? TraitorKiller; public int Turn; public int Round=1; public DateTime? Deadline; public string? CurrentQuestion; public Dictionary<int,string> Answers=[]; public Dictionary<int,int?> Votes=[]; public List<Qna> History=[]; public CancellationTokenSource? Timer; }
    private sealed record Player(int Id, string Name, string? Avatar, string Connection) { public bool Ready { get; set; } }
    private sealed record Killer(string Name, string Color, string Image);
    private sealed record Qna(string Author, string Question, List<NamedAnswer> Answers);
    private sealed record NamedAnswer(string Name, string Text);
}
