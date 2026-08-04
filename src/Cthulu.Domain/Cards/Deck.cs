using Cthulu.Domain.Ids;
using Cthulu.Domain.Random;

namespace Cthulu.Domain.Cards;

/// <summary>Draw pile backed by a list; shuffle via injected <see cref="IRandom"/>.</summary>
public sealed class Deck
{
    private readonly List<CardInstance> _cards;
    private readonly IRandom _rng;

    public Deck(IEnumerable<CardInstance> cards, IRandom rng)
    {
        _cards = cards.ToList();
        _rng = rng;
    }

    public int Count => _cards.Count;

    public IReadOnlyList<CardInstance> Cards => _cards;

    public void Shuffle()
    {
        // Fisher–Yates
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public CardInstance Draw()
    {
        if (_cards.Count == 0)
            throw new InvalidOperationException("Deck is empty.");
        var last = _cards.Count - 1;
        var card = _cards[last];
        _cards.RemoveAt(last);
        return card;
    }

    public void AddToTop(CardInstance card) => _cards.Add(card);

    public void AddRange(IEnumerable<CardInstance> cards) => _cards.AddRange(cards);

    public bool TryRemove(CardInstance card) => _cards.Remove(card);

    public CardInstance? FindGem(GemValue value) =>
        _cards.FirstOrDefault(c => c.Def.Type == CardType.Gem && c.Def.GemValue == value);

    public bool TryTakeGem(GemValue value, out CardInstance? card)
    {
        card = FindGem(value);
        if (card is null)
            return false;
        _cards.Remove(card);
        return true;
    }

    public CardInstance? FindArcana(ArcanaKind kind) =>
        _cards.FirstOrDefault(c => c.Def.Type == CardType.Arcana && c.Def.ArcanaKind == kind);

    public bool TryTakeArcana(ArcanaKind kind, out CardInstance? card)
    {
        card = FindArcana(kind);
        if (card is null)
            return false;
        _cards.Remove(card);
        return true;
    }

    public CardInstance? FindRelic(RelicKind kind) =>
        _cards.FirstOrDefault(c => c.Def.Type == CardType.Relic && c.Def.RelicKind == kind);

    public bool TryTakeRelic(RelicKind kind, out CardInstance? card)
    {
        card = FindRelic(kind);
        if (card is null)
            return false;
        _cards.Remove(card);
        return true;
    }

    /// <summary>Remove a specific instance from the pile (e.g. creative return).</summary>
    public bool TryTakeById(CardInstanceId id, out CardInstance? card)
    {
        card = _cards.FirstOrDefault(c => c.Id.Equals(id));
        if (card is null)
            return false;
        _cards.Remove(card);
        return true;
    }
}
