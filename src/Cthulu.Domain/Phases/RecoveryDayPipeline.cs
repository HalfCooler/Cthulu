using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;
using Cthulu.Domain.Scoring;

namespace Cthulu.Domain.Phases;

/// <summary>
/// Recovery day: auctions 1–2 (R12 / §5.2) + all-in round 3 (R13) → FinalScoring → Finished.
/// </summary>
public static class RecoveryDayPipeline
{
    /// <summary>Enter recovery after the final prep day (vote no path).</summary>
    public static void BeginRecovery(GameState state)
    {
        state.NextSegmentIsRecovery = false;
        state.RecoveryRound = 0;
        state.ActiveAuction = null;
        state.ActiveTrade = null;
        state.ActiveArcana = null;
        state.Offers.Clear();
        state.Log("RecoveryStart", "进入复苏日：竞拍 ×2 + 全下 ×1");
        StartNextAuctionRound(state);
    }

    /// <summary>Start recovery auction round 1–2: draw 2 gems each, reveal altar, dealer acts first.</summary>
    public static void StartNextAuctionRound(GameState state)
    {
        if (state.RecoveryRound >= GameRules.RecoveryAuctionRounds)
        {
            BeginAllIn(state);
            return;
        }

        state.RecoveryRound++;
        ClearTableForRecoveryRound(state);

        foreach (var player in state.SeatsFromDealer())
        {
            var gems = DrawPileService.Draw(
                state.GemDeck, state.GemDiscard, GameRules.RecoveryDrawGems);
            player.GemHand.AddRange(gems);
        }

        var n = state.AltarSlotCount;
        var altar = DrawPileService.Draw(state.RelicDeck, state.RelicDiscard, n);
        state.Altar.AddRange(altar);

        state.ActiveAuction = new AuctionState
        {
            RoundIndex = state.RecoveryRound,
        };
        state.Phase = GamePhase.Recovery_Auction;
        state.CurrentActorSeat = state.DealerSeat;

        state.Log(
            "RecoveryAuctionStart",
            $"复苏竞拍第 {state.RecoveryRound}/{GameRules.RecoveryAuctionRounds} 次：" +
            $"每人摸 {GameRules.RecoveryDrawGems} 张宝石，祭坛 {state.Altar.Count} 张打包；" +
            $"从庄家 {state.PlayerAtSeat(state.DealerSeat).Name} 开始");
    }

    /// <summary>Raise auction bid: NewSum must be strictly greater than CurrentSum.</summary>
    public static DomainResult RaiseAuction(
        GameState state,
        PlayerId playerId,
        IReadOnlyList<CardInstanceId> gemIds)
    {
        if (state.Phase != GamePhase.Recovery_Auction)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是复苏竞拍阶段");

        var auction = state.ActiveAuction;
        if (auction is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "没有进行中的竞拍");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (player.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");

        if (auction.PassedPlayers.Contains(playerId))
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "你已放弃本轮竞拍");

        if (gemIds is null || gemIds.Count == 0)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "加价至少 1 张宝石");

        if (gemIds.Distinct().Count() != gemIds.Count)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");

        var selected = new List<CardInstance>(gemIds.Count);
        foreach (var id in gemIds)
        {
            var card = player.FindGem(id);
            if (card is null)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
            selected.Add(card);
        }

        var newSum = selected.Sum(g => g.Def.FaceValue);
        if (newSum <= auction.CurrentSum)
        {
            return DomainResult.Fail(
                DomainErrorCodes.InvalidCards,
                $"加价点数总和须严格大于当前最高价（{auction.CurrentSum}），你的 sum={newSum}");
        }

        // Refund previous leader.
        if (auction.CurrentBidLeader is { } prevId && auction.CurrentBidGems.Count > 0)
        {
            var prev = state.FindPlayer(prevId);
            if (prev is not null)
            {
                prev.GemHand.AddRange(auction.CurrentBidGems);
                state.Log(
                    "AuctionBidRefund",
                    $"{prev.Name} 的出价被超越，{auction.CurrentBidGems.Count} 张宝石退回手牌");
            }
            else
            {
                state.GemDiscard.AddRange(auction.CurrentBidGems);
            }

            auction.CurrentBidGems.Clear();
        }

        foreach (var c in selected)
            player.GemHand.Remove(c);

        auction.CurrentBidLeader = playerId;
        auction.CurrentBidGems.AddRange(selected);
        auction.ConsecutivePasses = 0;

        state.Log(
            "AuctionRaise",
            $"{player.Name} 加价 {selected.Count} 张宝石（sum={newSum}，公开张数）");

        // If only this player remains active, they win immediately.
        var stillIn = CountActiveBidders(state, auction);
        if (stillIn <= 1)
        {
            ResolveAuctionWinner(state, auction);
            return DomainResult.Success();
        }

        AdvanceAuctionActor(state, auction);
        return DomainResult.Success();
    }

    /// <summary>Fold this auction round (no longer bid).</summary>
    public static DomainResult PassAuction(GameState state, PlayerId playerId)
    {
        if (state.Phase != GamePhase.Recovery_Auction)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是复苏竞拍阶段");

        var auction = state.ActiveAuction;
        if (auction is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "没有进行中的竞拍");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (player.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");

        if (auction.PassedPlayers.Contains(playerId))
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "你已放弃本轮竞拍");

        auction.PassedPlayers.Add(playerId);
        auction.ConsecutivePasses++;
        state.Log("AuctionPass", $"{player.Name} 放弃本轮竞拍");

        if (TryEndAuctionAfterPass(state, auction))
            return DomainResult.Success();

        AdvanceAuctionActor(state, auction);
        return DomainResult.Success();
    }

    /// <summary>All-in round: draw gems, reveal altar, wait for parallel rank submits.</summary>
    public static void BeginAllIn(GameState state)
    {
        state.RecoveryRound = GameRules.RecoveryAllInRound;
        ClearTableForRecoveryRound(state);

        foreach (var player in state.SeatsFromDealer())
        {
            var gems = DrawPileService.Draw(
                state.GemDeck, state.GemDiscard, GameRules.RecoveryDrawGems);
            player.GemHand.AddRange(gems);
        }

        var n = state.AltarSlotCount;
        var altar = DrawPileService.Draw(state.RelicDeck, state.RelicDiscard, n);
        state.Altar.AddRange(altar);

        state.ActiveAuction = null;
        state.Phase = GamePhase.Recovery_AllIn;
        state.Offers.Clear();

        var eligible = state.Players.Count(p => p.GemHand.Count > 0);
        state.Log(
            "RecoveryAllInStart",
            $"复苏第 3 次全下：每人摸 {GameRules.RecoveryDrawGems} 张宝石，祭坛 {state.Altar.Count} 张；" +
            $"有宝石的玩家（{eligible} 人）选择序号后全下手牌宝石");

        // Nobody has gems → resolve empty / void all relics and finish.
        if (eligible == 0)
            ResolveAllInAndFinish(state);
    }

    /// <summary>
    /// Submit all-in offer: all gems in hand become the offer; rank annotates altar target.
    /// Players with zero gems do not participate and need not submit.
    /// </summary>
    public static DomainResult SubmitAllInOffer(
        GameState state,
        PlayerId playerId,
        SequenceRank rank)
    {
        if (state.Phase != GamePhase.Recovery_AllIn)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是复苏全下阶段");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (player.GemHand.Count == 0 && !state.Offers.ContainsKey(playerId))
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "你没有宝石，无需参与全下");

        if (!GameRules.IsValidRank(rank, state.PlayerCount))
            return DomainResult.Fail(DomainErrorCodes.InvalidTarget, "无效的序号");

        // Re-submit: return previous offer gems to hand first (then take all again).
        if (state.Offers.TryGetValue(playerId, out var existing))
        {
            player.GemHand.AddRange(existing.Gems);
            state.Offers.Remove(playerId);
        }

        if (player.GemHand.Count == 0)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "没有可全下的宝石");

        var offer = new Offer { Rank = rank };
        offer.Gems.AddRange(player.GemHand);
        player.GemHand.Clear();
        state.Offers[playerId] = offer;

        state.Log(
            "AllInSubmit",
            $"{player.Name} 全下供奉（序号 {rank}，{offer.GemCount} 张，sum={offer.Sum}）");

        if (AllEligibleAllInSubmitted(state))
            ResolveAllInAndFinish(state);

        return DomainResult.Success();
    }

    private static bool AllEligibleAllInSubmitted(GameState state)
    {
        foreach (var p in state.Players)
        {
            var hasOffer = state.Offers.ContainsKey(p.Id);
            var hasGems = p.GemHand.Count > 0;
            // Eligible = currently holding gems OR already submitted (gems in offer).
            // Not eligible = no gems and no offer → skip.
            if (hasGems && !hasOffer)
                return false;
            // If they have gems in hand they haven't submitted yet.
        }

        return true;
    }

    private static void ResolveAllInAndFinish(GameState state)
    {
        state.Log("AllInResolve", "全下结算开始");
        PrepDayPipeline.ResolveOffersToRelics(state);
        EnterFinalScoring(state);
    }

    public static void EnterFinalScoring(GameState state)
    {
        state.Phase = GamePhase.FinalScoring;
        state.ActiveAuction = null;
        state.ActiveTrade = null;
        state.ActiveArcana = null;

        var scores = ScoreCalculator.Compute(state.Players);
        foreach (var s in scores.OrderByDescending(x => x.Total))
        {
            var mark = s.IsWinner ? " ★胜者" : "";
            state.Log("FinalScore", $"{s.Name}：{s.Total} 分{mark}");
        }

        var winners = scores.Where(s => s.IsWinner).Select(s => s.Name).ToList();
        state.Log(
            "GameFinished",
            winners.Count == 0
                ? "终局计分完成"
                : $"终局！胜者：{string.Join("、", winners)}");

        state.Phase = GamePhase.Finished;
    }

    private static bool TryEndAuctionAfterPass(GameState state, AuctionState auction)
    {
        var stillIn = CountActiveBidders(state, auction);

        if (!auction.HasAnyBid)
        {
            // All folded with no bid → void.
            if (stillIn == 0)
            {
                ResolveAuctionVoid(state, auction);
                return true;
            }

            return false;
        }

        // Standing bid ends when every non-leader has folded
        // (equivalent to consecutive ActiveCount-1 passes after the last raise).
        if (CountActiveChallengers(state, auction) == 0)
        {
            ResolveAuctionWinner(state, auction);
            return true;
        }

        return false;
    }

    /// <summary>Active players who are not the current bid leader (can still raise).</summary>
    private static int CountActiveChallengers(GameState state, AuctionState auction)
    {
        if (auction.CurrentBidLeader is null)
            return CountActiveBidders(state, auction);

        return state.Players.Count(p =>
            !auction.PassedPlayers.Contains(p.Id) &&
            !p.Id.Equals(auction.CurrentBidLeader.Value));
    }

    private static int CountActiveBidders(GameState state, AuctionState auction) =>
        state.Players.Count(p => !auction.PassedPlayers.Contains(p.Id));

    private static void ResolveAuctionWinner(GameState state, AuctionState auction)
    {
        if (!auction.HasAnyBid || auction.CurrentBidLeader is null)
        {
            ResolveAuctionVoid(state, auction);
            return;
        }

        var winner = state.FindPlayer(auction.CurrentBidLeader.Value);
        if (winner is null)
        {
            ResolveAuctionVoid(state, auction);
            return;
        }

        // Pay: escrow gems → discard.
        state.GemDiscard.AddRange(auction.CurrentBidGems);
        var paid = auction.CurrentBidGems.Count;
        var sum = auction.CurrentSum;
        auction.CurrentBidGems.Clear();

        // Award all altar relics.
        foreach (var relic in state.Altar)
            winner.Relics.Add(relic);
        var count = state.Altar.Count;
        state.Altar.Clear();

        state.Log(
            "AuctionWon",
            $"{winner.Name} 竞拍成功：支付 {paid} 张宝石（sum={sum}），获得祭坛全部 {count} 张祭品");

        FinishAuctionRound(state);
    }

    private static void ResolveAuctionVoid(GameState state, AuctionState auction)
    {
        // Return any stray escrow (should be empty if no bid).
        if (auction.CurrentBidLeader is { } lid && auction.CurrentBidGems.Count > 0)
        {
            var p = state.FindPlayer(lid);
            if (p is not null)
                p.GemHand.AddRange(auction.CurrentBidGems);
            else
                state.GemDiscard.AddRange(auction.CurrentBidGems);
            auction.CurrentBidGems.Clear();
        }

        if (state.Altar.Count > 0)
        {
            state.RelicDiscard.AddRange(state.Altar);
            state.Log(
                "AuctionVoid",
                $"竞拍无人出价，祭坛 {state.Altar.Count} 张祭品进入弃牌");
            state.Altar.Clear();
        }
        else
        {
            state.Log("AuctionVoid", "竞拍无人出价");
        }

        FinishAuctionRound(state);
    }

    private static void FinishAuctionRound(GameState state)
    {
        state.ActiveAuction = null;
        state.DealerSeat = state.NextSeat(state.DealerSeat);
        state.Log("DealerRotate", $"庄家轮换为 {state.PlayerAtSeat(state.DealerSeat).Name}");
        StartNextAuctionRound(state);
    }

    private static void AdvanceAuctionActor(GameState state, AuctionState auction)
    {
        var seat = state.CurrentActorSeat;
        for (var i = 0; i < state.PlayerCount; i++)
        {
            seat = state.NextSeat(seat);
            var p = state.PlayerAtSeat(seat);
            if (!auction.PassedPlayers.Contains(p.Id))
            {
                state.CurrentActorSeat = seat;
                return;
            }
        }

        // No active actor left — end by remaining bid or void.
        if (auction.HasAnyBid)
            ResolveAuctionWinner(state, auction);
        else
            ResolveAuctionVoid(state, auction);
    }

    private static void ClearTableForRecoveryRound(GameState state)
    {
        if (state.Altar.Count > 0)
        {
            state.RelicDiscard.AddRange(state.Altar);
            state.Altar.Clear();
        }

        // Return any leftover offers to hand.
        foreach (var (pid, offer) in state.Offers.ToList())
        {
            var p = state.FindPlayer(pid);
            p?.GemHand.AddRange(offer.Gems);
        }

        state.Offers.Clear();
        state.AltarModifiers.Clear();
        state.ActiveArcana = null;
        state.ActiveTrade = null;
    }
}
