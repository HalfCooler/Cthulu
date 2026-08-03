namespace Cthulu.Domain.Cards;

/// <summary>Immutable card definition (type + kind/value).</summary>
public sealed record CardDef(
    CardType Type,
    GemValue? GemValue = null,
    RelicKind? RelicKind = null,
    ArcanaKind? ArcanaKind = null)
{
    public static CardDef Gem(GemValue value) =>
        new(CardType.Gem, GemValue: value);

    public static CardDef Relic(RelicKind kind) =>
        new(CardType.Relic, RelicKind: kind);

    public static CardDef Arcana(ArcanaKind kind) =>
        new(CardType.Arcana, ArcanaKind: kind);

    public int FaceValue =>
        Type == CardType.Gem && GemValue.HasValue ? (int)GemValue.Value : 0;

    public string DisplayKey => Type switch
    {
        CardType.Gem => $"Gem{(int)(GemValue ?? Cards.GemValue.One)}",
        CardType.Relic => RelicKind?.ToString() ?? "Relic",
        CardType.Arcana => ArcanaKind?.ToString() ?? "Arcana",
        _ => Type.ToString(),
    };
}
