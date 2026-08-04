using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;
using Cthulu.Domain.Random;

namespace Cthulu.Domain.Game;

/// <summary>Full authoritative game state (ENGINEERING §6.5).</summary>
public sealed class GameState
{
    public GameId Id { get; }
    public IRandom Rng { get; }
    public List<PlayerState> Players { get; }
    public int DealerSeat { get; set; }
    public int CurrentActorSeat { get; set; }
    public GamePhase Phase { get; set; }

    /// <summary>Match mode (standard multiplayer vs creative sandbox).</summary>
    public GameMode Mode { get; set; } = GameMode.Standard;

    public bool IsCreative => Mode == GameMode.Creative;

    /// <summary>1-based counter of prep days started.</summary>
    public int PrepDayNumber { get; set; }

    /// <summary>
    /// R10: which full Prep×3+Offering cycle we are in (0-based).
    /// VoteContinue is unlocked only after <see cref="GameRules.MandatoryCyclesBeforeVote"/> cycles complete.
    /// </summary>
    public int CycleIndex { get; set; }

    /// <summary>
    /// Remaining prep days in the current segment including the day about to start / in progress.
    /// After Resolve, decremented; if still &gt;0 continue prep, else offering or recovery.
    /// </summary>
    public int PrepDaysRemainingInCycle { get; set; }

    /// <summary>
    /// When true, finishing the current prep segment (remaining reaches 0) enters
    /// <see cref="GamePhase.Recovery_Auction"/> instead of offering day (post-vote "no").
    /// </summary>
    public bool NextSegmentIsRecovery { get; set; }

    /// <summary>
    /// Recovery progress: 0 = not in recovery; 1–2 = auction rounds; 3 = all-in.
    /// </summary>
    public int RecoveryRound { get; set; }

    public Deck GemDeck { get; }
    public Deck ArcanaDeck { get; }
    public Deck RelicDeck { get; }
    public List<CardInstance> GemDiscard { get; } = new();
    public List<CardInstance> ArcanaDiscard { get; } = new();
    public List<CardInstance> RelicDiscard { get; } = new();

    public List<CardInstance> Altar { get; } = new();
    public Dictionary<PlayerId, Offer> Offers { get; } = new();
    public AltarModifiers AltarModifiers { get; } = new();

    public TradeState? ActiveTrade { get; set; }
    public AuctionState? ActiveAuction { get; set; }
    public ArcanaResolutionState? ActiveArcana { get; set; }

    public List<GameEvent> EventLog { get; } = new();

    public int PlayerCount => Players.Count;

    public int AltarSlotCount => GameRules.GetAltarCount(PlayerCount);

    public IReadOnlyList<SequenceRank> AvailableRanks => GameRules.GetAvailableRanks(PlayerCount);

    public GameState(
        GameId id,
        IReadOnlyList<PlayerState> players,
        Deck gemDeck,
        Deck arcanaDeck,
        Deck relicDeck,
        IRandom rng)
    {
        Id = id;
        Players = players.OrderBy(p => p.SeatIndex).ToList();
        GemDeck = gemDeck;
        ArcanaDeck = arcanaDeck;
        RelicDeck = relicDeck;
        Rng = rng;
        Phase = GamePhase.Lobby;
        DealerSeat = 0;
        CurrentActorSeat = 0;
        PrepDayNumber = 0;
        CycleIndex = 0;
        PrepDaysRemainingInCycle = GameRules.StandardPrepDaysPerCycle;
        NextSegmentIsRecovery = false;
        RecoveryRound = 0;
    }

    public PlayerState? FindPlayer(PlayerId id) =>
        Players.FirstOrDefault(p => p.Id.Equals(id));

    public PlayerState PlayerAtSeat(int seat) =>
        Players.First(p => p.SeatIndex == seat);

    public PlayerState? CurrentActor =>
        Players.FirstOrDefault(p => p.SeatIndex == CurrentActorSeat);

    /// <summary>Soft cap so long matches do not unbounded-grow memory.</summary>
    public const int EventLogSoftCap = 250;

    /// <summary>
    /// Append a <b>public</b> log line visible to every player.
    /// <paramref name="code"/> is a stable machine key (tooltip / CSS classify);
    /// <paramref name="message"/> is the Chinese text shown in the UI.
    /// </summary>
    public void Log(string code, string message) =>
        AppendLog(new GameEvent { Code = code, Message = message });

    /// <summary>
    /// Append a <b>personal</b> log line only <paramref name="visibleTo"/> can see (blue in UI).
    /// Other players never receive this entry in their projected view.
    /// </summary>
    public void Log(string code, string message, PlayerId visibleTo) =>
        AppendLog(new GameEvent { Code = code, Message = message, VisibleTo = visibleTo });

    private void AppendLog(GameEvent entry)
    {
        EventLog.Add(entry);
        if (EventLog.Count > EventLogSoftCap)
            EventLog.RemoveRange(0, EventLog.Count - EventLogSoftCap + 50);
    }

    /// <summary>Seat index clockwise after <paramref name="seat"/>.</summary>
    public int NextSeat(int seat) => (seat + 1) % PlayerCount;

    public IEnumerable<PlayerState> SeatsFromDealer()
    {
        for (var i = 0; i < PlayerCount; i++)
            yield return PlayerAtSeat((DealerSeat + i) % PlayerCount);
    }

    /// <summary>Pick a random gem from hand; returns null if empty.</summary>
    public CardInstance? TakeRandomGem(PlayerState player)
    {
        if (player.GemHand.Count == 0)
            return null;
        var idx = Rng.Next(player.GemHand.Count);
        var card = player.GemHand[idx];
        player.GemHand.RemoveAt(idx);
        return card;
    }
}
