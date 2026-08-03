namespace Cthulu.Domain.Ids;

/// <summary>Unique identity for a card instance in a running game.</summary>
public readonly record struct CardInstanceId(Guid Value)
{
    public static CardInstanceId New() => new(Guid.NewGuid());

    public static CardInstanceId Parse(string value) => new(Guid.Parse(value));

    public static bool TryParse(string? value, out CardInstanceId id)
    {
        if (Guid.TryParse(value, out var g))
        {
            id = new CardInstanceId(g);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("N");
}
