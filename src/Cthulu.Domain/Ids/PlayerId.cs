namespace Cthulu.Domain.Ids;

/// <summary>Strongly-typed player identity.</summary>
public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());

    public static PlayerId Parse(string value) => new(Guid.Parse(value));

    public static bool TryParse(string? value, out PlayerId id)
    {
        if (Guid.TryParse(value, out var g))
        {
            id = new PlayerId(g);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Value.ToString("N");
}
