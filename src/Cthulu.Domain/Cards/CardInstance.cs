using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Cards;

/// <summary>A physical card instance in a game.</summary>
public sealed class CardInstance
{
    public CardInstanceId Id { get; }
    public CardDef Def { get; }

    public CardInstance(CardInstanceId id, CardDef def)
    {
        Id = id;
        Def = def;
    }

    public static CardInstance Create(CardDef def) =>
        new(CardInstanceId.New(), def);
}
