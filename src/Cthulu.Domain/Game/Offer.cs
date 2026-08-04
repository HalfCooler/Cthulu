using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>A player's pending offer: rank annotates altar target; gems are escrowed from hand.</summary>
public sealed class Offer
{
    public SequenceRank Rank { get; set; }
    public List<CardInstance> Gems { get; } = new();

    /// <summary>
    /// When true, <see cref="Rank"/> is public knowledge (e.g. Spiritism guessed correctly)
    /// and stays visible on player cards until the rank is modified or the offer is replaced.
    /// </summary>
    public bool IsRankPublic { get; set; }

    public int Sum => Gems.Sum(g => g.Def.FaceValue);
    public int GemCount => Gems.Count;
}
