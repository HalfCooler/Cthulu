using Cthulu.Domain.Ids;

namespace Cthulu.Application.Rooms;

public interface IGameRoomStore
{
    void Add(GameRoom room);
    GameRoom? GetByCode(string code);
    GameRoom? GetById(RoomId id);

    /// <summary>Snapshot of all rooms currently in memory (order undefined).</summary>
    IReadOnlyList<GameRoom> ListAll();

    (RoomId RoomId, PlayerId PlayerId)? GetConnectionBinding(string connectionId);
    void BindConnection(string connectionId, RoomId roomId, PlayerId playerId);
    void UnbindConnection(string connectionId);
}
