using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;

namespace Cthulu.Domain.Phases;

/// <summary>
/// Offering day trade (§5.1) and post-offering VoteContinue (R10).
/// </summary>
public static class OfferingDayPipeline
{
    public static void BeginOfferingDay(GameState state)
    {
        state.Phase = GamePhase.OfferingDay_Trade;
        state.ActiveTrade = null;
        state.CurrentActorSeat = state.DealerSeat;
        foreach (var p in state.Players)
            p.HasActedTrade = false;

        state.Log(
            "OfferingDayStart",
            $"进入供奉日交易，从庄家 {state.PlayerAtSeat(state.DealerSeat).Name} 开始");
    }

    public static DomainResult PassTrade(GameState state, PlayerId playerId)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        if (state.ActiveTrade is not null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前有进行中的交易");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");
        if (player.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");
        if (player.HasActedTrade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "你本供奉日已行动过");

        player.HasActedTrade = true;
        state.LogAction(playerId, "TradePass", $"{player.Name} 跳过交易");
        AdvanceTradeActor(state);
        return DomainResult.Success();
    }

    public static DomainResult ProposeTrade(
        GameState state,
        PlayerId buyerId,
        PlayerId sellerId,
        CardInstanceId relicInstanceId,
        IReadOnlyList<CardInstanceId> buyerGemIds)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        if (state.ActiveTrade is not null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前有进行中的交易");

        var buyer = state.FindPlayer(buyerId);
        if (buyer is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");
        if (buyer.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");
        if (buyer.HasActedTrade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "你本供奉日已行动过");
        if (buyerId.Equals(sellerId))
            return DomainResult.Fail(DomainErrorCodes.InvalidTarget, "不能与自己交易");

        var seller = state.FindPlayer(sellerId);
        if (seller is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidTarget, "卖家不在对局中");

        var relic = seller.Relics.FirstOrDefault(r => r.Id.Equals(relicInstanceId));
        if (relic is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "卖家没有该祭品");
        if (seller.RelicsTradedSuccessfully.Contains(relicInstanceId))
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "该祭品本局已成功交易过，不可再卖");

        if (buyerGemIds is null || buyerGemIds.Count < 1)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "出价至少 1 张宝石");
        if (buyerGemIds.Count > GameRules.MaxTradeDisplayGems)
            return DomainResult.Fail(
                DomainErrorCodes.InvalidCards,
                $"出价宝石张数不能超过 {GameRules.MaxTradeDisplayGems}");
        if (buyerGemIds.Distinct().Count() != buyerGemIds.Count)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");

        var gems = new List<CardInstance>();
        foreach (var id in buyerGemIds)
        {
            var g = buyer.FindGem(id);
            if (g is null)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
            gems.Add(g);
        }

        foreach (var g in gems)
            buyer.GemHand.Remove(g);

        var trade = new TradeState
        {
            BuyerId = buyerId,
            SellerId = sellerId,
            RelicInstanceId = relicInstanceId,
            SubPhase = TradeSubPhase.AwaitSellerResponse,
        };
        trade.BuyerEscrow.AddRange(gems);
        state.ActiveTrade = trade;

        state.LogAction(buyerId,
            "TradePropose",
            $"{buyer.Name} 向 {seller.Name} 求购「{PrepDayPipeline.RelicDisplay(relic)}」（出价 {gems.Count} 张宝石）");
        return DomainResult.Success();
    }

    public static DomainResult RespondTrade(GameState state, PlayerId sellerId, bool accept)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        var trade = state.ActiveTrade;
        if (trade is null || trade.SubPhase != TradeSubPhase.AwaitSellerResponse)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前没有待回应的交易");
        if (!sellerId.Equals(trade.SellerId))
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "只有卖家可以回应");

        var buyer = state.FindPlayer(trade.BuyerId)!;
        var seller = state.FindPlayer(trade.SellerId)!;

        if (accept)
        {
            CompleteSuccessfulTrade(state, trade, buyer, seller);
            state.LogAction(sellerId, "TradeAccept", $"{seller.Name} 接受了 {buyer.Name} 的交易");
            state.RecordAction(buyer.Id, $"{buyer.Name} 与 {seller.Name} 的交易已成交");
            FinishBuyerAction(state, buyer);
            return DomainResult.Success();
        }

        trade.SubPhase = TradeSubPhase.AwaitBuyerChoice;
        state.LogAction(sellerId, "TradeReject", $"{seller.Name} 拒绝了交易，等待 {buyer.Name} 抉择");
        return DomainResult.Success();
    }

    public static DomainResult CancelTrade(GameState state, PlayerId buyerId)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        var trade = state.ActiveTrade;
        if (trade is null || trade.SubPhase != TradeSubPhase.AwaitBuyerChoice)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不能取消交易");
        if (!buyerId.Equals(trade.BuyerId))
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "只有买家可以取消");

        var buyer = state.FindPlayer(trade.BuyerId)!;
        // Return escrow gems
        buyer.GemHand.AddRange(trade.BuyerEscrow);
        trade.BuyerEscrow.Clear();
        state.ActiveTrade = null;
        state.LogAction(buyerId, "TradeCancel", $"{buyer.Name} 选择原路返回，交易终止");
        FinishBuyerAction(state, buyer);
        return DomainResult.Success();
    }

    /// <summary>
    /// Buyer chooses force-buy after reject. Optional new gem set replaces escrow
    /// (from hand + current escrow pool).
    /// </summary>
    public static DomainResult BeginForceBuy(
        GameState state,
        PlayerId buyerId,
        IReadOnlyList<CardInstanceId>? newBuyerGemIds)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        var trade = state.ActiveTrade;
        if (trade is null || trade.SubPhase != TradeSubPhase.AwaitBuyerChoice)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不能强买");
        if (!buyerId.Equals(trade.BuyerId))
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "只有买家可以强买");

        var buyer = state.FindPlayer(trade.BuyerId)!;

        if (newBuyerGemIds is not null)
        {
            // Return current escrow to hand, then re-lock selected
            buyer.GemHand.AddRange(trade.BuyerEscrow);
            trade.BuyerEscrow.Clear();

            if (newBuyerGemIds.Count < 1)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "强买出价至少 1 张宝石");
            if (newBuyerGemIds.Count > GameRules.MaxTradeDisplayGems)
                return DomainResult.Fail(
                    DomainErrorCodes.InvalidCards,
                    $"强买出价宝石张数不能超过 {GameRules.MaxTradeDisplayGems}");
            if (newBuyerGemIds.Distinct().Count() != newBuyerGemIds.Count)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");

            var gems = new List<CardInstance>();
            foreach (var id in newBuyerGemIds)
            {
                var g = buyer.FindGem(id);
                if (g is null)
                    return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
                gems.Add(g);
            }

            foreach (var g in gems)
                buyer.GemHand.Remove(g);
            trade.BuyerEscrow.AddRange(gems);
        }
        else if (trade.BuyerEscrow.Count < 1)
        {
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "强买出价至少 1 张宝石");
        }

        trade.SubPhase = TradeSubPhase.ForceBuyBids;
        trade.BuyerBidCommitted = false;
        trade.SellerBidCommitted = false;
        trade.SellerEscrow.Clear();

        state.LogAction(buyerId, "TradeForceBuy", $"{buyer.Name} 选择强买，双方提交暗价");
        return DomainResult.Success();
    }

    /// <summary>
    /// Commit force-buy bid. Buyer: replaces escrow with gemIds (1–MaxTradeDisplayGems).
    /// Seller: sets floor bid (0–MaxTradeDisplayGems).
    /// </summary>
    public static DomainResult CommitForceBuyBid(
        GameState state,
        PlayerId playerId,
        IReadOnlyList<CardInstanceId> gemIds)
    {
        if (state.Phase != GamePhase.OfferingDay_Trade)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉日交易");
        var trade = state.ActiveTrade;
        if (trade is null || trade.SubPhase != TradeSubPhase.ForceBuyBids)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是强买阶段");

        var isBuyer = playerId.Equals(trade.BuyerId);
        var isSeller = playerId.Equals(trade.SellerId);
        if (!isBuyer && !isSeller)
            return DomainResult.Fail(DomainErrorCodes.Unauthorized, "你不是本交易双方");

        gemIds ??= Array.Empty<CardInstanceId>();
        if (gemIds.Distinct().Count() != gemIds.Count)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");
        if (gemIds.Count > GameRules.MaxTradeDisplayGems)
            return DomainResult.Fail(
                DomainErrorCodes.InvalidCards,
                $"展示宝石张数不能超过 {GameRules.MaxTradeDisplayGems}");

        if (isBuyer)
        {
            if (trade.BuyerBidCommitted)
                return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "买家已提交暗价");
            if (gemIds.Count < 1)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "买家出价至少 1 张");

            var buyer = state.FindPlayer(trade.BuyerId)!;
            // Escrow is already out of hand; pool = escrow + hand
            buyer.GemHand.AddRange(trade.BuyerEscrow);
            trade.BuyerEscrow.Clear();

            var gems = new List<CardInstance>();
            foreach (var id in gemIds)
            {
                var g = buyer.FindGem(id);
                if (g is null)
                    return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
                gems.Add(g);
            }

            foreach (var g in gems)
                buyer.GemHand.Remove(g);
            trade.BuyerEscrow.AddRange(gems);
            trade.BuyerBidCommitted = true;
            state.LogAction(buyer.Id, "TradeForceBid", $"{buyer.Name} 已提交强买暗价");
        }
        else
        {
            if (trade.SellerBidCommitted)
                return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "卖家已提交暗价");

            var seller = state.FindPlayer(trade.SellerId)!;
            // Return previous seller escrow if any
            if (trade.SellerEscrow.Count > 0)
            {
                seller.GemHand.AddRange(trade.SellerEscrow);
                trade.SellerEscrow.Clear();
            }

            var gems = new List<CardInstance>();
            foreach (var id in gemIds)
            {
                var g = seller.FindGem(id);
                if (g is null)
                    return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
                gems.Add(g);
            }

            foreach (var g in gems)
                seller.GemHand.Remove(g);
            trade.SellerEscrow.AddRange(gems);
            trade.SellerBidCommitted = true;
            state.LogAction(seller.Id, "TradeForceBid", $"{seller.Name} 已提交强买底价");
        }

        if (trade.BuyerBidCommitted && trade.SellerBidCommitted)
            ResolveForceBuy(state, trade);

        return DomainResult.Success();
    }

    private static void ResolveForceBuy(GameState state, TradeState trade)
    {
        var buyer = state.FindPlayer(trade.BuyerId)!;
        var seller = state.FindPlayer(trade.SellerId)!;
        var buyerSum = trade.BuyerEscrowSum;
        var sellerSum = trade.SellerEscrowSum;

        var outcome = buyerSum >= sellerSum
            ? $"{buyer.Name} 对 {seller.Name} 的强买成功，取得祭品"
            : $"{buyer.Name} 对 {seller.Name} 的强买失败，出价宝石弃置";
        state.RecordAction(buyer.Id, outcome);
        state.RecordAction(seller.Id, outcome);

        if (buyerSum >= sellerSum)
        {
            CompleteSuccessfulTrade(state, trade, buyer, seller);
            // Seller floor gems return
            seller.GemHand.AddRange(trade.SellerEscrow);
            trade.SellerEscrow.Clear();
            state.Log(
                "TradeForceSuccess",
                $"强买成功（买家点数 {buyerSum} ≥ 卖家 {sellerSum}）：祭品归 {buyer.Name}");
        }
        else
        {
            // Buyer gems discarded; seller escrow returned; relic stays
            state.GemDiscard.AddRange(trade.BuyerEscrow);
            trade.BuyerEscrow.Clear();
            seller.GemHand.AddRange(trade.SellerEscrow);
            trade.SellerEscrow.Clear();
            state.ActiveTrade = null;
            state.Log(
                "TradeForceFail",
                $"强买失败（买家点数 {buyerSum} < 卖家 {sellerSum}）：买家出价弃置");
        }

        FinishBuyerAction(state, buyer);
    }

    private static void CompleteSuccessfulTrade(
        GameState state,
        TradeState trade,
        PlayerState buyer,
        PlayerState seller)
    {
        var relic = seller.Relics.FirstOrDefault(r => r.Id.Equals(trade.RelicInstanceId));
        if (relic is not null)
        {
            seller.Relics.Remove(relic);
            buyer.Relics.Add(relic);
            buyer.RelicsTradedSuccessfully.Add(relic.Id);
        }

        seller.GemHand.AddRange(trade.BuyerEscrow);
        trade.BuyerEscrow.Clear();
        state.ActiveTrade = null;
    }

    private static void FinishBuyerAction(GameState state, PlayerState buyer)
    {
        buyer.HasActedTrade = true;
        state.ActiveTrade = null;
        AdvanceTradeActor(state);
    }

    private static void AdvanceTradeActor(GameState state)
    {
        if (state.Players.All(p => p.HasActedTrade))
        {
            OnOfferingDayComplete(state);
            return;
        }

        var seat = state.CurrentActorSeat;
        for (var i = 0; i < state.PlayerCount; i++)
        {
            seat = state.NextSeat(seat);
            var p = state.PlayerAtSeat(seat);
            if (!p.HasActedTrade)
            {
                state.CurrentActorSeat = seat;
                return;
            }
        }

        OnOfferingDayComplete(state);
    }

    /// <summary>
    /// After a full Prep×3 + Offering×1 cycle: first
    /// <see cref="GameRules.MandatoryCyclesBeforeVote"/> cycles auto-continue;
    /// only then enter VoteContinue.
    /// </summary>
    public static void OnOfferingDayComplete(GameState state)
    {
        state.ActiveTrade = null;

        // CycleIndex is 0-based for the cycle just finished.
        var completedCycles = state.CycleIndex + 1;
        if (completedCycles < GameRules.MandatoryCyclesBeforeVote)
        {
            state.Log(
                "CalendarAutoContinue",
                $"第 {completedCycles} 个循环结束（强制 {GameRules.MandatoryCyclesBeforeVote} 轮），自动进入下一循环");
            BeginNextStandardCycle(state);
            return;
        }

        BeginVote(state);
    }

    public static void BeginVote(GameState state)
    {
        state.Phase = GamePhase.VoteContinue;
        state.ActiveTrade = null;
        foreach (var p in state.Players)
        {
            p.HasVoted = false;
            p.VoteYes = null;
        }

        state.Log(
            "VoteStart",
            $"已完成 {state.CycleIndex + 1} 个循环，投票是否继续下一循环（筹备×3+供奉×1）");
    }

    public static DomainResult CastVote(GameState state, PlayerId playerId, bool yes)
    {
        if (state.Phase != GamePhase.VoteContinue)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是投票阶段");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");
        if (player.HasVoted)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "你已投过票");

        player.HasVoted = true;
        player.VoteYes = yes;
        state.LogAction(playerId, "VoteCast", $"{player.Name} 投票：{(yes ? "继续" : "结束循环")}");

        if (state.Players.All(p => p.HasVoted))
            ResolveVote(state);

        return DomainResult.Success();
    }

    public static void ResolveVote(GameState state)
    {
        var yesCount = state.Players.Count(p => p.VoteYes == true);
        var majority = yesCount > state.PlayerCount / 2; // strict majority

        state.Log(
            "VoteResult",
            $"投票结果：赞成 {yesCount}/{state.PlayerCount}，{(majority ? "继续" : "进入终局筹备")}");

        foreach (var p in state.Players)
        {
            p.HasVoted = false;
            p.VoteYes = null;
        }

        if (majority)
        {
            BeginNextStandardCycle(state);
        }
        else
        {
            state.DealerSeat = state.NextSeat(state.DealerSeat);
            state.Log("DealerRotate", $"庄家轮换为 {state.PlayerAtSeat(state.DealerSeat).Name}");
            state.PrepDaysRemainingInCycle = GameRules.FinalPrepDaysBeforeRecovery;
            state.NextSegmentIsRecovery = true;
            state.Log("CalendarFinalPrep", "多数不同意继续：筹备×1 后进入复苏日");
            PrepDayPipeline.BeginPrepDay(state);
        }
    }

    /// <summary>Rotate dealer and start the next Prep×3 + Offering×1 cycle.</summary>
    public static void BeginNextStandardCycle(GameState state)
    {
        state.DealerSeat = state.NextSeat(state.DealerSeat);
        state.Log("DealerRotate", $"庄家轮换为 {state.PlayerAtSeat(state.DealerSeat).Name}");

        state.CycleIndex++;
        state.PrepDaysRemainingInCycle = GameRules.StandardPrepDaysPerCycle;
        state.NextSegmentIsRecovery = false;
        state.Log("CalendarContinue", $"第 {state.CycleIndex + 1} 循环：筹备×3 + 供奉×1");
        PrepDayPipeline.BeginPrepDay(state);
    }
}
