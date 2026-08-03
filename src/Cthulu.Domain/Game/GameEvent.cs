namespace Cthulu.Domain.Game;

/// <summary>Desensitized event for public log (Chinese message ready for UI).</summary>
public sealed class GameEvent
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string Code { get; init; }
    public required string Message { get; init; }
}
