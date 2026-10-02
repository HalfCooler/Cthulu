namespace Cthulu.Application.Views;

/// <summary>Per-observer room snapshot (public + private fields for the observer).</summary>
public sealed class RoomView
{
    public required string RoomCode { get; init; }
    public required string Phase { get; init; }
    public required string PhaseDisplayName { get; init; }
    public required string SelfPlayerId { get; init; }
    public required string SelfName { get; init; }
    public required bool IsHost { get; init; }
    public required int PlayerCount { get; init; }
    public required int MinPlayers { get; init; }
    public required int MaxPlayers { get; init; }
    public required bool CanStart { get; init; }
    /// <summary>Host alone in lobby may start creative mode (1 human + 3 bots).</summary>
    public bool CanStartCreative { get; init; }
    /// <summary>True when host may open the debug seed field (Development config).</summary>
    public bool AllowDebugSeed { get; init; }
    /// <summary>Observer is ready in lobby.</summary>
    public bool SelfIsReady { get; init; }
    /// <summary>Lobby only: may change nickname while not ready.</summary>
    public bool CanRename { get; init; }
    /// <summary>Humans who have clicked ready / total humans (lobby).</summary>
    public int ReadyCount { get; init; }
    public int HumanCount { get; init; }
    /// <summary>All human seats are ready (lobby start gate).</summary>
    public bool AllPlayersReady { get; init; }
    /// <summary>Standard or Creative (sandbox).</summary>
    public string GameMode { get; init; } = "Standard";
    public bool IsCreativeMode { get; init; }
    public required IReadOnlyList<PlayerPublicView> Players { get; init; }
    public string? Hint { get; init; }

    // --- Game ---
    public int PrepDayNumber { get; init; }
    public PrepSettlementView? LastPrepSettlement { get; init; }
    public int CycleIndex { get; init; }
    public int PrepDaysRemainingInCycle { get; init; }
    /// <summary>After remaining prep days, go to recovery instead of offering day.</summary>
    public bool NextSegmentIsRecovery { get; init; }
    public string? DealerName { get; init; }
    public string? CurrentActorName { get; init; }
    public string? CurrentActorPlayerId { get; init; }
    public bool IsMyTurn { get; init; }
    public bool CanSubmitOffer { get; init; }
    public bool CanPlayArcana { get; init; }
    public bool HasSubmittedOffer { get; init; }

    public IReadOnlyList<AltarSlotView> Altar { get; init; } = Array.Empty<AltarSlotView>();
    public IReadOnlyList<string> AvailableRanks { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CardView> MyGems { get; init; } = Array.Empty<CardView>();
    public IReadOnlyList<CardView> MyArcana { get; init; } = Array.Empty<CardView>();
    public MyOfferView? MyOffer { get; init; }
    public IReadOnlyList<LogEntryView> Log { get; init; } = Array.Empty<LogEntryView>();
    /// <summary>Room chat (lobby + in-game). Chronological, oldest first.</summary>
    public IReadOnlyList<ChatMessageView> Chat { get; init; } = Array.Empty<ChatMessageView>();
    public int GemDeckCount { get; init; }
    public int ArcanaDeckCount { get; init; }
    public int RelicDeckCount { get; init; }

    // --- Score preview (M2) ---
    public ScorePreviewView? ScorePreview { get; init; }

    // --- Active multi-step arcana ---
    public ActiveArcanaView? ActiveArcana { get; init; }

    // --- Trade (offering day) ---
    public TradeView? Trade { get; init; }
    public bool CanPassTrade { get; init; }
    public bool CanProposeTrade { get; init; }

    // --- Vote ---
    public bool CanVote { get; init; }
    public bool HasVoted { get; init; }
    public int VoteYesCount { get; init; }
    public int VoteTotalCast { get; init; }

    // --- Recovery ---
    public int RecoveryRound { get; init; }
    public AuctionView? Auction { get; init; }
    public bool CanRaiseAuction { get; init; }
    public bool CanPassAuction { get; init; }
    public bool CanSubmitAllIn { get; init; }
    public bool HasSubmittedAllIn { get; init; }
    public bool IsFinished { get; init; }

    /// <summary>Creative mode: deck contents available to take into hand/relics.</summary>
    public CreativeDeckView? CreativeDeck { get; init; }
}

/// <summary>Aggregated deck inventory for creative-mode take-from-deck UI.</summary>
public sealed class CreativeDeckView
{
    public IReadOnlyList<DeckStockView> Gems { get; init; } = Array.Empty<DeckStockView>();
    public IReadOnlyList<DeckStockView> Arcana { get; init; } = Array.Empty<DeckStockView>();
    public IReadOnlyList<DeckStockView> Relics { get; init; } = Array.Empty<DeckStockView>();
}

public sealed class DeckStockView
{
    public required string Kind { get; init; }
    public required string DisplayName { get; init; }
    public int Count { get; init; }
    public string Description { get; init; } = "";
}

public sealed class AuctionView
{
    public int RoundIndex { get; init; }
    public int MaxAuctionRounds { get; init; } = 2;
    public string? LeaderPlayerId { get; init; }
    public string? LeaderName { get; init; }
    public int CurrentBidCount { get; init; }
    /// <summary>Only visible to the current bid leader (face values).</summary>
    public IReadOnlyList<CardView>? MyBidGems { get; init; }
    public int? MyBidSum { get; init; }
    public IReadOnlyList<string> PassedPlayerIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PassedPlayerNames { get; init; } = Array.Empty<string>();
    public string? CurrentActorPlayerId { get; init; }
    public string? CurrentActorName { get; init; }
    public bool IsMyTurn { get; init; }
    public bool IHavePassed { get; init; }
    public bool IAmLeader { get; init; }
}

public sealed class PlayerPublicView
{
    public string? RecentAction { get; init; }
    public string? RecentActionContext { get; init; }
    public IReadOnlyList<string> RecentActionDetails { get; init; } = Array.Empty<string>();
    public required string PlayerId { get; init; }
    public required string Name { get; init; }
    public required int SeatIndex { get; init; }
    public required bool IsConnected { get; init; }
    public required bool IsHost { get; init; }
    public required bool IsSelf { get; init; }
    public bool IsBot { get; init; }
    /// <summary>Lobby ready status (ignored in-game).</summary>
    public bool IsReady { get; init; }

    public int GemCount { get; init; }
    public int ArcanaCount { get; init; }
    public int OfferGemCount { get; init; }
    public bool HasOffer { get; init; }
    /// <summary>
    /// When set, this player's offer rank is public (Spiritism guessed correctly)
    /// and should stay lit on the player card until modified.
    /// </summary>
    public string? RevealedRank { get; init; }
    public bool IsDealer { get; init; }
    public bool IsCurrentActor { get; init; }
    public IReadOnlyList<string> RelicKinds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RelicDisplayNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RelicItemView> Relics { get; init; } = Array.Empty<RelicItemView>();
    public int VirtualForbiddenKnowledge { get; init; }
    public bool HasActedTrade { get; init; }
    public bool HasVoted { get; init; }
}

public sealed class PrepSettlementView
{
    public required string Id { get; init; }
    public int DayNumber { get; init; }
    public bool AwaitingConfirmation { get; init; }
    public bool HasConfirmed { get; init; }
    public bool CanSkipDisconnected { get; init; }
    public int ConfirmedCount { get; init; }
    public int SkippedCount { get; init; }
    public int HumanCount { get; init; }
    public IReadOnlyList<string> WaitingNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<SettlementSlotView> Slots { get; init; } = Array.Empty<SettlementSlotView>();
}

public sealed class SettlementSlotView
{
    public required string Rank { get; init; }
    public required string RelicName { get; init; }
    public bool Reverse { get; init; }
    public bool EvilProphecy { get; init; }
    public required string Outcome { get; init; }
    public IReadOnlyList<SettlementOfferView> Offers { get; init; } = Array.Empty<SettlementOfferView>();
}

public sealed record SettlementOfferView(string PlayerName, int GemCount, int Sum, string Status);

public sealed class RelicItemView
{
    public required string InstanceId { get; init; }
    public required string Kind { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>Scoring / function blurb for hover tooltip.</summary>
    public string Description { get; init; } = "";
    public bool TradedSuccessfully { get; init; }
}

public sealed class AltarSlotView
{
    public int SlotIndex { get; init; }
    public string Rank { get; init; } = "";
    public string RelicKind { get; init; } = "";
    public string DisplayName { get; init; } = "";
    /// <summary>Scoring / function blurb for hover tooltip.</summary>
    public string Description { get; init; } = "";
    public bool ReverseThinking { get; init; }
    public bool EvilProphecy { get; init; }
}

public sealed class CardView
{
    public required string InstanceId { get; init; }
    public required string Type { get; init; }
    public string? Kind { get; init; }
    public int? FaceValue { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>Effect / scoring blurb for hover tooltip.</summary>
    public string Description { get; init; } = "";
}

public sealed class MyOfferView
{
    public required string Rank { get; init; }
    public required IReadOnlyList<CardView> Gems { get; init; }
    public int Sum { get; init; }
    public int GemCount { get; init; }
}

public sealed class LogEntryView
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string Timestamp { get; init; }

    /// <summary>Key lifecycle / scoring events for UI highlight.</summary>
    public bool Highlight { get; init; }

    /// <summary>info | success | warn | danger | private — CSS class suffix.</summary>
    public string Level { get; init; } = "info";

    /// <summary>True when this line is a personal (only-self) log entry.</summary>
    public bool IsPrivate { get; init; }
}

public sealed class ChatMessageView
{
    public required string Id { get; init; }
    public required string PlayerId { get; init; }
    public required string PlayerName { get; init; }
    public required string Text { get; init; }
    public required string Timestamp { get; init; }
    public bool IsSelf { get; init; }
}

public sealed class ScorePreviewView
{
    public bool IsFinal { get; init; }
    public IReadOnlyList<PlayerScoreView> Players { get; init; } = Array.Empty<PlayerScoreView>();
}

public sealed class PlayerScoreView
{
    public required string PlayerId { get; init; }
    public required string Name { get; init; }
    public int Total { get; init; }
    public bool IsLeader { get; init; }
}

public sealed class ActiveArcanaView
{
    public required string Kind { get; init; }
    public required string KindDisplayName { get; init; }
    public required string ActorPlayerId { get; init; }
    public required string ActorName { get; init; }
    public required string StepId { get; init; }
    public string? Prompt { get; init; }
    public bool IsActor { get; init; }
    public bool CanRespond { get; init; }

    /// <summary>Peek: observer has finished their re-offer choice.</summary>
    public bool IHaveFinished { get; init; }

    /// <summary>Peek: one-shot reveal of target offer (only during Reveal step, actor only).</summary>
    public PeekedOfferView? PeekedOffer { get; init; }

    /// <summary>
    /// Peek: actor-only text reminder of the peeked offer during ReOffer
    /// (after Reveal Ack; does not re-open full card view).
    /// </summary>
    public string? PeekReminder { get; init; }

    /// <summary>ArtificialBreeding / Transplant temp cards visible to actor.</summary>
    public IReadOnlyList<CardView> TempCards { get; init; } = Array.Empty<CardView>();

    public IReadOnlyList<string> TargetPlayerIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TargetPlayerNames { get; init; } = Array.Empty<string>();
}

public sealed class PeekedOfferView
{
    public required string TargetPlayerId { get; init; }
    public required string TargetName { get; init; }
    public required string Rank { get; init; }
    public IReadOnlyList<CardView> Gems { get; init; } = Array.Empty<CardView>();
    public int Sum { get; init; }
}

public sealed class TradeView
{
    public required string SubPhase { get; init; }
    public required string BuyerPlayerId { get; init; }
    public required string BuyerName { get; init; }
    public required string SellerPlayerId { get; init; }
    public required string SellerName { get; init; }
    public required string RelicInstanceId { get; init; }
    public required string RelicDisplayName { get; init; }

    /// <summary>Always public: buyer bid count during bargain.</summary>
    public int BuyerEscrowCount { get; init; }

    /// <summary>Only buyer sees face values during bargain / own force bid.</summary>
    public IReadOnlyList<CardView>? BuyerEscrowGems { get; init; }

    public bool IsBuyer { get; init; }
    public bool IsSeller { get; init; }
    public bool BuyerBidCommitted { get; init; }
    public bool SellerBidCommitted { get; init; }

    /// <summary>Seller sees own force-buy floor after commit (private).</summary>
    public IReadOnlyList<CardView>? SellerEscrowGems { get; init; }
    public int? SellerEscrowCountPrivate { get; init; }
}
