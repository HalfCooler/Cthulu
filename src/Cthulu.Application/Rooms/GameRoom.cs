using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;

namespace Cthulu.Application.Rooms;

/// <summary>In-memory room aggregate holding lobby players and optional GameState.</summary>
public sealed class GameRoom
{
    private readonly object _gate = new();

    public RoomId Id { get; }
    public string Code { get; }
    public List<RoomPlayer> Players { get; } = new();
    public PlayerId HostId { get; private set; }

    /// <summary>Lobby / match mode; set when the host starts the game.</summary>
    public GameMode Mode { get; set; } = GameMode.Standard;

    /// <summary>Null until StartGame. Source of truth for in-game phase.</summary>
    public GameState? Game { get; set; }

    public GameRoom(RoomId id, string code, RoomPlayer host)
    {
        Id = id;
        Code = code;
        HostId = host.Id;
        host.IsHost = true;
        host.SeatIndex = 0;
        Players.Add(host);
    }

    public object SyncRoot => _gate;

    public GamePhase Phase => Game?.Phase ?? GamePhase.Lobby;

    public RoomPlayer? FindById(PlayerId id) =>
        Players.FirstOrDefault(p => p.Id.Equals(id));

    public RoomPlayer? FindByName(string name) =>
        Players.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
