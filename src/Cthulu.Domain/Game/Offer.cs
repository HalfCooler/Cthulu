using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>A player's pending offer: rank annotates altar target; gems are escrowed from hand.</summary>
public sealed class Offer
{
    public SequenceRank Rank { get; set; }
    public List<CardInstance> Gems { get; } = new();

    public int Sum => Gems.Sum(g => g.Def.FaceValue);
    public int GemCount => Gems.Count;
}
