using Cthulu.Domain.Cards;
using Cthulu.Domain.Effects;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Phases;

namespace Cthulu.Application.Bots;

/// <summary>
/// Creative-mode bot policy: always pass/skip when possible;
/// when a choice is mandatory, pick the first N available options from the option list.
/// </summary>
public static class BotDriver
{
    private const int MaxSteps = 64;

    /// <summary>
    /// Drive all pending bot actions until a human decision is required or the game is idle.
    /// </summary>
    public static void RunAll(GameState game)
    {
        if (game.Players.All(p => !p.IsBot))
            return;

        for (var step = 0; step < MaxSteps; step++)
        {
            if (!TryOneAction(game))
                break;
        }
    }

    private static bool TryOneAction(GameState game)
    {
        if (game.Phase is GamePhase.Finished or GamePhase.FinalScoring or GamePhase.Lobby)
            return false;

        // Multi-step arcana responses (bots only when they must respond).
        if (game.ActiveArcana is not null)
            return TryArcanaStep(game);

        // Active trade responses.
        if (game.ActiveTrade is not null)
            return TryTradeResponse(game);

        return game.Phase switch
        {
            GamePhase.Prep_Offer => TryOffers(game),
            GamePhase.Prep_ArcanaTurn => TryPassArcana(game),
            GamePhase.OfferingDay_Trade => TryPassTrade(game),
            GamePhase.VoteContinue => TryVotes(game),
            GamePhase.Recovery_Auction => TryPassAuction(game),
            GamePhase.Recovery_AllIn => TryAllIn(game),
            _ => false,
        };
    }

    private static bool TryOffers(GameState game)
    {
        foreach (var bot in game.Players.Where(p => p.IsBot))
        {
            if (game.Offers.ContainsKey(bot.Id))
                continue;

            // Must choose: first available rank + first 1 gem.
            var ranks = game.AvailableRanks;
            if (ranks.Count == 0 || bot.GemHand.Count == 0)
                continue;

            var rank = ranks[0];
            var gemIds = new[] { bot.GemHand[0].Id };
            var r = PrepDayPipeline.SubmitOffer(game, bot.Id, rank, gemIds);
            return r.Ok;
        }

        return false;
    }

    private static bool TryPassArcana(GameState game)
    {
        var actor = game.CurrentActor;
        if (actor is null || !actor.IsBot || actor.HasActedArcana)
            return false;

        var r = PrepDayPipeline.PassArcana(game, actor.Id);
        return r.Ok;
    }

    private static bool TryArcanaStep(GameState game)
    {
        var active = game.ActiveArcana!;
        var kind = active.Kind;

        // Peek: actor must Ack reveal; then both may Skip re-offer.
        if (kind == ArcanaKind.Peek)
        {
            if (string.Equals(active.StepId, PeekEffect.StepReveal, StringComparison.OrdinalIgnoreCase))
            {
                var actor = game.FindPlayer(active.ActorId);
                if (actor is { IsBot: true })
                {
                    var r = PrepDayPipeline.ArcanaStepResponse(
                        game, actor.Id, PeekEffect.StepAck, new ArcanaTarget());
                    return r.Ok;
                }

                return false;
            }

            // ReOffer phase: skip when possible (bots always keep offer).
            if (active.TargetPlayerIds.Count > 0)
            {
                var targetId = active.TargetPlayerIds[0];
                if (!active.ActorFinished)
                {
                    var actor = game.FindPlayer(active.ActorId);
                    if (actor is { IsBot: true })
                    {
                        var r = PrepDayPipeline.ArcanaStepResponse(
                            game, actor.Id, PeekEffect.StepSkip, new ArcanaTarget());
                        return r.Ok;
                    }
                }

                if (!active.TargetFinished)
                {
                    var target = game.FindPlayer(targetId);
                    if (target is { IsBot: true })
                    {
                        var r = PrepDayPipeline.ArcanaStepResponse(
                            game, target.Id, PeekEffect.StepSkip, new ArcanaTarget());
                        return r.Ok;
                    }
                }
            }

            return false;
        }

        // ArtificialBreeding: keep first temp card.
        if (kind == ArcanaKind.ArtificialBreeding)
        {
            var actor = game.FindPlayer(active.ActorId);
            if (actor is not { IsBot: true })
                return false;
            if (active.TempCards.Count == 0)
                return false;

            var keep = active.TempCards[0];
            var r = PrepDayPipeline.ArcanaStepResponse(
                game,
                actor.Id,
                active.StepId,
                new ArcanaTarget { CardIds = new List<CardInstanceId> { keep.Id } });
            return r.Ok;
        }

        // Transplant: first 2 cards from pool (temp + hand) — first N available.
        if (kind == ArcanaKind.Transplant)
        {
            var actor = game.FindPlayer(active.ActorId);
            if (actor is not { IsBot: true })
                return false;

            var pool = active.TempCards.Concat(actor.GemHand).ToList();
            if (pool.Count < 2)
                return false;

            var r = PrepDayPipeline.ArcanaStepResponse(
                game,
                actor.Id,
                active.StepId,
                new ArcanaTarget
                {
                    CardIds = new List<CardInstanceId> { pool[0].Id, pool[1].Id },
                });
            return r.Ok;
        }

        // Other multi-step: if actor is bot, try first-N generic (usually not reached — bots pass).
        var actorBot = game.FindPlayer(active.ActorId);
        if (actorBot is { IsBot: true })
        {
            // No safe generic action — leave for human if any; otherwise stall.
            return false;
        }

        return false;
    }

    private static bool TryPassTrade(GameState game)
    {
        var actor = game.CurrentActor;
        if (actor is null || !actor.IsBot || actor.HasActedTrade)
            return false;
        if (game.ActiveTrade is not null)
            return false;

        var r = OfferingDayPipeline.PassTrade(game, actor.Id);
        return r.Ok;
    }

    private static bool TryTradeResponse(GameState game)
    {
        var trade = game.ActiveTrade!;
        switch (trade.SubPhase)
        {
            case TradeSubPhase.AwaitSellerResponse:
            {
                var seller = game.FindPlayer(trade.SellerId);
                if (seller is not { IsBot: true })
                    return false;
                // First option in UI is 接受.
                var r = OfferingDayPipeline.RespondTrade(game, seller.Id, accept: true);
                return r.Ok;
            }
            case TradeSubPhase.AwaitBuyerChoice:
            {
                var buyer = game.FindPlayer(trade.BuyerId);
                if (buyer is not { IsBot: true })
                    return false;
                // Skip-like first: 原路返回.
                var r = OfferingDayPipeline.CancelTrade(game, buyer.Id);
                return r.Ok;
            }
            case TradeSubPhase.ForceBuyBids:
            {
                // Buyer must choose ≥1 gem; seller may choose 0 (first N empty for seller).
                if (!trade.BuyerBidCommitted)
                {
                    var buyer = game.FindPlayer(trade.BuyerId);
                    if (buyer is { IsBot: true })
                    {
                        // Escrow already has gems; re-commit first escrow card or first hand gem.
                        var pool = trade.BuyerEscrow.Concat(buyer.GemHand).ToList();
                        if (pool.Count == 0)
                            return false;
                        var r = OfferingDayPipeline.CommitForceBuyBid(
                            game, buyer.Id, new[] { pool[0].Id });
                        return r.Ok;
                    }
                }

                if (!trade.SellerBidCommitted)
                {
                    var seller = game.FindPlayer(trade.SellerId);
                    if (seller is { IsBot: true })
                    {
                        // First N available for optional bid: empty (0 gems).
                        var r = OfferingDayPipeline.CommitForceBuyBid(
                            game, seller.Id, Array.Empty<CardInstanceId>());
                        return r.Ok;
                    }
                }

                return false;
            }
            default:
                return false;
        }
    }

    private static bool TryVotes(GameState game)
    {
        foreach (var bot in game.Players.Where(p => p.IsBot && !p.HasVoted))
        {
            // First option in UI: 继续 (yes).
            var r = OfferingDayPipeline.CastVote(game, bot.Id, yes: true);
            return r.Ok;
        }

        return false;
    }

    private static bool TryPassAuction(GameState game)
    {
        var actor = game.CurrentActor;
        if (actor is null || !actor.IsBot)
            return false;
        if (game.ActiveAuction is null)
            return false;
        if (game.ActiveAuction.PassedPlayers.Contains(actor.Id))
            return false;

        var r = RecoveryDayPipeline.PassAuction(game, actor.Id);
        return r.Ok;
    }

    private static bool TryAllIn(GameState game)
    {
        foreach (var bot in game.Players.Where(p => p.IsBot))
        {
            if (game.Offers.ContainsKey(bot.Id))
                continue;
            if (bot.GemHand.Count == 0)
                continue;

            var ranks = game.AvailableRanks;
            if (ranks.Count == 0)
                continue;

            var r = RecoveryDayPipeline.SubmitAllInOffer(game, bot.Id, ranks[0]);
            return r.Ok;
        }

        return false;
    }
}
