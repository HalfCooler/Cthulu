namespace Cthulu.Domain.Ids;

/// <summary>Strongly-typed internal room identity (not the human-facing short code).</summary>
public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.NewGuid());

    public static RoomId Parse(string value) => new(Guid.Parse(value));

    public static bool TryParse(string? value, out RoomId id)
    {
        if (Guid.TryParse(value, out var g))
        {
            id = new RoomId(g);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("N");
}
