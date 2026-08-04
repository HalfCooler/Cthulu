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
    /// <summary>True when host may open the debug seed field (Development config).</summary>
    public bool AllowDebugSeed { get; init; }
    public required IReadOnlyList<PlayerPublicView> Players { get; init; }
    public string? Hint { get; init; }

    // --- Game ---
    public int PrepDayNumber { get; init; }
    public int CycleIndex { get; init; }
    public int PrepDaysRemainingInCycle { get; init; }
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
    public required string PlayerId { get; init; }
    public required string Name { get; init; }
    public required int SeatIndex { get; init; }
    public required bool IsConnected { get; init; }
    public required bool IsHost { get; init; }
    public required bool IsSelf { get; init; }

    public int GemCount { get; init; }
    public int ArcanaCount { get; init; }
    public int OfferGemCount { get; init; }
    public bool HasOffer { get; init; }
    public bool IsDealer { get; init; }
    public bool IsCurrentActor { get; init; }
    public IReadOnlyList<string> RelicKinds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RelicDisplayNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RelicItemView> Relics { get; init; } = Array.Empty<RelicItemView>();
    public int VirtualForbiddenKnowledge { get; init; }
    public bool HasActedTrade { get; init; }
    public bool HasVoted { get; init; }
}

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
