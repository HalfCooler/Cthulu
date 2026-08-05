using Cthulu.Domain.Ids;

namespace Cthulu.Application.Rooms;

/// <summary>Room-scoped chat line (lobby + in-game; not part of domain EventLog).</summary>
public sealed class RoomChatMessage
{
    /// <summary>Stable id for client-side “only speak new lines” tracking.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required PlayerId PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public required string Text { get; init; }
}
