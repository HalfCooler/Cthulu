using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>Active recovery-day auction (rounds 1–2). ENGINEERING §5.2.</summary>
public sealed class AuctionState
{
    /// <summary>1-based auction index within recovery (1..3).</summary>
    public int RoundIndex { get; set; }

    public PlayerId? CurrentBidLeader { get; set; }

    /// <summary>Gems escrowed as the current highest bid.</summary>
    public List<CardInstance> CurrentBidGems { get; } = new();

    /// <summary>Players who folded this auction round (no longer bid).</summary>
    public HashSet<PlayerId> PassedPlayers { get; } = new();

    /// <summary>Consecutive passes since last successful raise (for round-end).</summary>
    public int ConsecutivePasses { get; set; }

    public bool HasAnyBid => CurrentBidLeader is not null;

    public int CurrentSum => CurrentBidGems.Sum(g => g.Def.FaceValue);

    public int CurrentBidCount => CurrentBidGems.Count;

    public void ClearBid()
    {
        CurrentBidLeader = null;
        CurrentBidGems.Clear();
        ConsecutivePasses = 0;
    }
}
