using System.Collections.Concurrent;
using Cthulu.Domain.Ids;

namespace Cthulu.Application.Rooms;

/// <summary>Process-local room store. Restart loses all rooms (acceptable for M0–M3).</summary>
public sealed class InMemoryGameRoomStore : IGameRoomStore
{
    private readonly ConcurrentDictionary<string, GameRoom> _byCode =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<Guid, GameRoom> _byId = new();

    private readonly ConcurrentDictionary<string, (RoomId RoomId, PlayerId PlayerId)> _connections =
        new(StringComparer.Ordinal);

    public void Add(GameRoom room)
    {
        if (!_byCode.TryAdd(room.Code, room))
            throw new InvalidOperationException($"Room code collision: {room.Code}");

        _byId[room.Id.Value] = room;
    }

    public GameRoom? GetByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        _byCode.TryGetValue(code.Trim(), out var room);
        return room;
    }

    public GameRoom? GetById(RoomId id)
    {
        _byId.TryGetValue(id.Value, out var room);
        return room;
    }

    public IReadOnlyList<GameRoom> ListAll() => _byId.Values.ToList();

    public (RoomId RoomId, PlayerId PlayerId)? GetConnectionBinding(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var binding))
            return binding;
        return null;
    }

    public void BindConnection(string connectionId, RoomId roomId, PlayerId playerId)
    {
        // Drop any previous binding for this connection.
        UnbindConnection(connectionId);
        _connections[connectionId] = (roomId, playerId);
    }

    public void UnbindConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }
}
