namespace Cthulu.Domain.Cards;

/// <summary>Draw with per-pile discard reshuffle (R16).</summary>
public static class DrawPileService
{
    /// <summary>
    /// Draw up to <paramref name="count"/> cards. When the draw pile is empty,
    /// the discard pile is shuffled back into the deck (and cleared).
    /// Returns fewer cards only if both piles are exhausted.
    /// </summary>
    public static List<CardInstance> Draw(
        Deck deck,
        List<CardInstance> discard,
        int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var drawn = new List<CardInstance>(count);
        for (var i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                if (discard.Count == 0)
                    break;
                ReshuffleDiscardIntoDeck(deck, discard);
            }

            if (deck.Count == 0)
                break;

            drawn.Add(deck.Draw());
        }

        return drawn;
    }

    public static void ReshuffleDiscardIntoDeck(Deck deck, List<CardInstance> discard)
    {
        if (discard.Count == 0)
            return;
        deck.AddRange(discard);
        discard.Clear();
        deck.Shuffle();
    }
}
