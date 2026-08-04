using Cthulu.Domain.Cards;

namespace Cthulu.Domain.Game;

/// <summary>Global constants and helpers from ENGINEERING defaults (R4–R6, etc.).</summary>
public static class GameRules
{
    public const int MinPlayers = 4;
    public const int MaxPlayers = 6;
    public const int RoomCodeLength = 6;

    /// <summary>R14: initial deal does not give arcana (only gems 1–5).</summary>
    public const int InitialArcanaPerPlayer = 0;
    public const int PrepDrawGems = 1;
    public const int ArcanaHandSoftCap = 2; // draw 1 if below this (R15)
    public const int OmniscientGemThreshold = 5; // draw if gems < 5

    /// <summary>R10: full cycle segment is Prep×3 then Offering×1.</summary>
    public const int StandardPrepDaysPerCycle = 3;

    /// <summary>
    /// R10: the first N full cycles (Prep×3 + Offering×1 each) are mandatory;
    /// VoteContinue only starts after this many cycles have been completed.
    /// </summary>
    public const int MandatoryCyclesBeforeVote = 2;

    /// <summary>R10: after majority-no vote, one last prep day then recovery.</summary>
    public const int FinalPrepDaysBeforeRecovery = 1;

    /// <summary>
    /// Offering-day trade: displayed bid piles (buyer offer / force-buy buyer bid / seller floor)
    /// may not exceed this many gem cards.
    /// </summary>
    public const int MaxTradeDisplayGems = 5;

    /// <summary>R12: recovery auctions 1–2 each draw this many gems per player.</summary>
    public const int RecoveryDrawGems = 2;

    /// <summary>R12: number of packaged auction rounds before all-in (rounds 1–2).</summary>
    public const int RecoveryAuctionRounds = 2;

    /// <summary>R13: recovery round index for all-in (round 3, after 2 auctions).</summary>
    public const int RecoveryAllInRound = 3;

    /// <summary>
    /// R5 / R6: room size m ∈ [4,6]; altar count N satisfies m = N + 2 (so N = m − 2).
    /// 4→2, 5→3, 6→4. N also equals max sequence rank.
    /// </summary>
    public static int GetAltarCount(int playerCount) => playerCount - 2;

    public static IReadOnlyList<SequenceRank> GetAvailableRanks(int playerCount)
    {
        var n = GetAltarCount(playerCount);
        var ranks = new SequenceRank[n];
        for (var i = 0; i < n; i++)
            ranks[i] = (SequenceRank)(i + 1);
        return ranks;
    }

    public static bool IsValidRank(SequenceRank rank, int playerCount) =>
        (int)rank >= 1 && (int)rank <= GetAltarCount(playerCount);

    /// <summary>0-based altar index from sequence rank.</summary>
    public static int RankToSlotIndex(SequenceRank rank) => (int)rank - 1;

    public static SequenceRank SlotIndexToRank(int slotIndex) =>
        (SequenceRank)(slotIndex + 1);
}
