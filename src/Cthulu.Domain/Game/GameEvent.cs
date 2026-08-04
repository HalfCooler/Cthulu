using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>
/// Desensitized event for the event log (Chinese message ready for UI).
/// </summary>
public sealed class GameEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string Code { get; init; }
    public required string Message { get; init; }

    /// <summary>
    /// When set, only this player receives the entry in their projected view.
    /// Null = public (all players).
    /// </summary>
    public PlayerId? VisibleTo { get; init; }

    public bool IsPrivate => VisibleTo is not null;
}
