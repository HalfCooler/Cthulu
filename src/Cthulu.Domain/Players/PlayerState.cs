using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Players;

public sealed class PlayerState
{
    public PlayerId Id { get; }
    public string Name { get; }
    public int SeatIndex { get; }
    public List<CardInstance> GemHand { get; } = new();
    public List<CardInstance> ArcanaHand { get; } = new();
    public List<CardInstance> Relics { get; } = new();
    public HashSet<CardInstanceId> RelicsTradedSuccessfully { get; } = new();
    public int VirtualForbiddenKnowledge { get; set; }
    public bool IsConnected { get; set; } = true;

    /// <summary>Whether this player has already acted in the current arcana round.</summary>
    public bool HasActedArcana { get; set; }

    /// <summary>Whether this player already used their offering-day trade action (pass or trade).</summary>
    public bool HasActedTrade { get; set; }

    /// <summary>Whether this player has cast a vote in the current VoteContinue phase.</summary>
    public bool HasVoted { get; set; }

    /// <summary>Vote yes/no when <see cref="HasVoted"/>.</summary>
    public bool? VoteYes { get; set; }

    public PlayerState(PlayerId id, string name, int seatIndex)
    {
        Id = id;
        Name = name;
        SeatIndex = seatIndex;
    }

    public CardInstance? FindGem(CardInstanceId id) =>
        GemHand.FirstOrDefault(c => c.Id.Equals(id));

    public CardInstance? FindArcana(CardInstanceId id) =>
        ArcanaHand.FirstOrDefault(c => c.Id.Equals(id));

    public CardInstance? FindArcana(ArcanaKind kind) =>
        ArcanaHand.FirstOrDefault(c => c.Def.ArcanaKind == kind);
}
