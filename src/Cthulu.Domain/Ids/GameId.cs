namespace Cthulu.Domain.Ids;

/// <summary>Identity of a running game instance inside a room.</summary>
public readonly record struct GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
