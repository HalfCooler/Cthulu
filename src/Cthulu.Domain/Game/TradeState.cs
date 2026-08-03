using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>Sub-phases of an active offering-day trade (ENGINEERING §5.1).</summary>
public enum TradeSubPhase
{
    /// <summary>Buyer has proposed; waiting for seller accept/reject.</summary>
    AwaitSellerResponse = 0,

    /// <summary>Seller rejected; buyer must Cancel or ForceBuy.</summary>
    AwaitBuyerChoice = 1,

    /// <summary>Force-buy dual-blind bids in progress.</summary>
    ForceBuyBids = 2,
}

/// <summary>Active trade session on <see cref="GameState"/>.</summary>
public sealed class TradeState
{
    public required PlayerId BuyerId { get; init; }
    public required PlayerId SellerId { get; init; }
    public required CardInstanceId RelicInstanceId { get; init; }
    public TradeSubPhase SubPhase { get; set; } = TradeSubPhase.AwaitSellerResponse;

    /// <summary>Buyer gems locked for the offer / force-buy.</summary>
    public List<CardInstance> BuyerEscrow { get; } = new();

    /// <summary>Seller force-buy floor bid (returned to seller always; used for sum compare only).</summary>
    public List<CardInstance> SellerEscrow { get; } = new();

    public bool BuyerBidCommitted { get; set; }
    public bool SellerBidCommitted { get; set; }

    public int BuyerEscrowSum => BuyerEscrow.Sum(g => g.Def.FaceValue);
    public int SellerEscrowSum => SellerEscrow.Sum(g => g.Def.FaceValue);
    public int BuyerEscrowCount => BuyerEscrow.Count;
}
