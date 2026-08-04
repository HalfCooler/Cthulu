using Cthulu.Application.Rooms;
using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Scoring;
using Microsoft.Extensions.Options;

namespace Cthulu.Application.Views;

/// <summary>Projects full room/game state into an observer-specific RoomView (sole outbound path).</summary>
public sealed class ViewProjector
{
    private readonly GameOptions _options;

    public ViewProjector(IOptions<GameOptions> options)
    {
        _options = options.Value;
    }

    private int MinPlayers =>
        Math.Clamp(_options.MinPlayers, GameRules.MinPlayers, GameRules.MaxPlayers);

    private int MaxPlayers =>
        Math.Clamp(_options.MaxPlayers, GameRules.MinPlayers, GameRules.MaxPlayers);

    private int LogDisplayLimit =>
        Math.Clamp(_options.EventLogDisplayLimit <= 0 ? 80 : _options.EventLogDisplayLimit, 20, 500);

    public RoomView Project(GameRoom room, PlayerId observerId)
    {
        var self = room.FindById(observerId);
        var game = room.Game;

        if (game is null)
            return ProjectLobby(room, observerId, self);

        return ProjectGame(room, game, observerId, self);
    }

    private RoomView ProjectLobby(GameRoom room, PlayerId observerId, RoomPlayer? self)
    {
        var players = room.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p => new PlayerPublicView
            {
                PlayerId = p.Id.ToString(),
                Name = p.Name,
                SeatIndex = p.SeatIndex,
                IsConnected = p.IsConnected,
                IsHost = p.IsHost,
                IsSelf = p.Id.Equals(observerId),
                IsBot = p.IsBot,
            })
            .ToList();

        var isHost = self?.IsHost ?? false;
        var humanCount = room.Players.Count(p => !p.IsBot);
        var enough = room.Players.Count >= MinPlayers
                     && room.Players.Count <= MaxPlayers;
        var canStart = room.Phase == GamePhase.Lobby && enough && isHost;
        var canStartCreative = room.Phase == GamePhase.Lobby && isHost && humanCount == 1;

        string hint;
        if (humanCount < MinPlayers && !canStartCreative)
            hint = $"等待玩家加入（至少 {MinPlayers} 人才能开始标准局，当前 {humanCount}；房主可单独开「创造模式」）";
        else if (canStartCreative && !canStart)
            hint = isHost
                ? "可单独启动「创造模式」（1 真人 + 3 人机），或等待更多玩家开始标准局"
                : "等待房主开始";
        else if (!isHost)
            hint = "人数已够，等待房主开始游戏";
        else
            hint = "人数已够，可点击「开始游戏」；或使用「创造模式」";

        return new RoomView
        {
            RoomCode = room.Code,
            Phase = room.Phase.ToString(),
            PhaseDisplayName = PhaseDisplayName(room.Phase),
            SelfPlayerId = observerId.ToString(),
            SelfName = self?.Name ?? string.Empty,
            IsHost = isHost,
            PlayerCount = room.Players.Count,
            MinPlayers = MinPlayers,
            MaxPlayers = MaxPlayers,
            CanStart = canStart,
            CanStartCreative = canStartCreative,
            AllowDebugSeed = isHost && _options.AllowDebugSeed,
            GameMode = room.Mode.ToString(),
            IsCreativeMode = room.Mode == GameMode.Creative,
            Players = players,
            Hint = hint,
            Log = Array.Empty<LogEntryView>(),
        };
    }

    private RoomView ProjectGame(
        GameRoom room,
        GameState game,
        PlayerId observerId,
        RoomPlayer? selfRoom)
    {
        var selfState = game.FindPlayer(observerId);
        var isOfferPhase = game.Phase == GamePhase.Prep_Offer;
        var isArcanaPhase = game.Phase == GamePhase.Prep_ArcanaTurn;
        var isTradePhase = game.Phase == GamePhase.OfferingDay_Trade;
        var isVotePhase = game.Phase == GamePhase.VoteContinue;
        var isAuctionPhase = game.Phase == GamePhase.Recovery_Auction;
        var isAllInPhase = game.Phase == GamePhase.Recovery_AllIn;
        var isFinished = game.Phase is GamePhase.FinalScoring or GamePhase.Finished;
        var isMyTurn = selfState is not null && selfState.SeatIndex == game.CurrentActorSeat
                       && game.ActiveArcana is null
                       && game.ActiveTrade is null
                       && (isArcanaPhase || isTradePhase || isAuctionPhase);
        var hasOffer = game.Offers.ContainsKey(observerId);

        var players = game.Players
            .OrderBy(p => p.SeatIndex)
            .Select(p =>
            {
                var roomPlayer = room.FindById(p.Id);
                game.Offers.TryGetValue(p.Id, out var offer);
                return new PlayerPublicView
                {
                    PlayerId = p.Id.ToString(),
                    Name = p.Name,
                    SeatIndex = p.SeatIndex,
                    IsConnected = roomPlayer?.IsConnected ?? p.IsConnected,
                    IsHost = roomPlayer?.IsHost ?? false,
                    IsSelf = p.Id.Equals(observerId),
                    IsBot = p.IsBot || (roomPlayer?.IsBot ?? false),
                    GemCount = p.GemHand.Count,
                    ArcanaCount = p.ArcanaHand.Count,
                    OfferGemCount = offer?.GemCount ?? 0,
                    HasOffer = offer is not null,
                    RevealedRank = offer is { IsRankPublic: true }
                        ? offer.Rank.ToString()
                        : null,
                    IsDealer = p.SeatIndex == game.DealerSeat,
                    IsCurrentActor = (isArcanaPhase || isTradePhase || isAuctionPhase)
                                     && p.SeatIndex == game.CurrentActorSeat,
                    RelicKinds = p.Relics
                        .Select(r => r.Def.RelicKind?.ToString() ?? "")
                        .ToList(),
                    RelicDisplayNames = p.Relics
                        .Select(r => RelicName(r.Def.RelicKind))
                        .ToList(),
                    Relics = p.Relics.Select(r => new RelicItemView
                    {
                        InstanceId = r.Id.ToString(),
                        Kind = r.Def.RelicKind?.ToString() ?? "",
                        DisplayName = RelicName(r.Def.RelicKind),
                        Description = RelicDescription(r.Def.RelicKind),
                        TradedSuccessfully = p.RelicsTradedSuccessfully.Contains(r.Id),
                    }).ToList(),
                    VirtualForbiddenKnowledge = p.VirtualForbiddenKnowledge,
                    HasActedTrade = p.HasActedTrade,
                    HasVoted = p.HasVoted,
                };
            })
            .ToList();

        var altar = new List<AltarSlotView>();
        for (var i = 0; i < game.Altar.Count; i++)
        {
            var card = game.Altar[i];
            altar.Add(new AltarSlotView
            {
                SlotIndex = i,
                Rank = GameRules.SlotIndexToRank(i).ToString(),
                RelicKind = card.Def.RelicKind?.ToString() ?? "",
                DisplayName = RelicName(card.Def.RelicKind),
                Description = RelicDescription(card.Def.RelicKind),
                ReverseThinking = game.AltarModifiers.HasReverse(i),
                EvilProphecy = game.AltarModifiers.HasEvilProphecy(i),
            });
        }

        var myGems = selfState?.GemHand
            .Select(ToCardView)
            .ToList() ?? new List<CardView>();

        var myArcana = selfState?.ArcanaHand
            .Select(ToCardView)
            .ToList() ?? new List<CardView>();

        MyOfferView? myOffer = null;
        if (selfState is not null && game.Offers.TryGetValue(observerId, out var offerSelf))
        {
            myOffer = new MyOfferView
            {
                Rank = offerSelf.Rank.ToString(),
                Gems = offerSelf.Gems.Select(ToCardView).ToList(),
                Sum = offerSelf.Sum,
                GemCount = offerSelf.GemCount,
            };
        }

        // Public entries for everyone; personal (VisibleTo) only for the matching observer.
        var log = game.EventLog
            .Where(e => e.VisibleTo is null || e.VisibleTo.Value.Equals(observerId))
            .TakeLast(LogDisplayLimit)
            .Select(e =>
            {
                var isPrivate = e.IsPrivate;
                var (highlight, level) = isPrivate
                    ? (true, "private")
                    : ClassifyLog(e.Code);
                return new LogEntryView
                {
                    Code = e.Code,
                    Message = e.Message,
                    Timestamp = e.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                    Highlight = highlight,
                    Level = level,
                    IsPrivate = isPrivate,
                };
            })
            .ToList();

        var dealer = game.PlayerAtSeat(game.DealerSeat);
        var actor = game.CurrentActor;

        var scores = ScoreCalculator.Compute(game.Players);
        var scorePreview = new ScorePreviewView
        {
            IsFinal = isFinished,
            Players = scores
                .OrderByDescending(s => s.Total)
                .Select(s => new PlayerScoreView
                {
                    PlayerId = s.PlayerId.ToString(),
                    Name = s.Name,
                    Total = s.Total,
                    IsLeader = s.IsWinner,
                }).ToList(),
        };

        var activeArcana = ProjectActiveArcana(game, observerId);
        var trade = ProjectTrade(game, observerId);
        var auction = ProjectAuction(game, observerId);

        var canPlayArcana = isArcanaPhase
                            && selfState is not null
                            && selfState.SeatIndex == game.CurrentActorSeat
                            && game.ActiveArcana is null;

        var canPassTrade = isTradePhase
                           && game.ActiveTrade is null
                           && selfState is not null
                           && selfState.SeatIndex == game.CurrentActorSeat
                           && !selfState.HasActedTrade;

        var canVote = isVotePhase && selfState is not null && !selfState.HasVoted;

        var canAuctionAct = isAuctionPhase
                            && selfState is not null
                            && selfState.SeatIndex == game.CurrentActorSeat
                            && game.ActiveAuction is not null
                            && !game.ActiveAuction.PassedPlayers.Contains(observerId);

        var canSubmitAllIn = isAllInPhase
                             && selfState is not null
                             && (selfState.GemHand.Count > 0 || hasOffer);

        var isCreative = game.IsCreative;
        CreativeDeckView? creativeDeck = null;
        if (isCreative && !isFinished)
            creativeDeck = ProjectCreativeDeck(game);

        return new RoomView
        {
            RoomCode = room.Code,
            Phase = game.Phase.ToString(),
            PhaseDisplayName = PhaseDisplayName(game.Phase),
            SelfPlayerId = observerId.ToString(),
            SelfName = selfState?.Name ?? selfRoom?.Name ?? string.Empty,
            IsHost = selfRoom?.IsHost ?? false,
            PlayerCount = game.PlayerCount,
            MinPlayers = MinPlayers,
            MaxPlayers = MaxPlayers,
            CanStart = false,
            CanStartCreative = false,
            AllowDebugSeed = false,
            GameMode = game.Mode.ToString(),
            IsCreativeMode = isCreative,
            Players = players,
            Hint = BuildHint(game, isMyTurn, hasOffer, isOfferPhase, selfState),
            PrepDayNumber = game.PrepDayNumber,
            CycleIndex = game.CycleIndex,
            PrepDaysRemainingInCycle = game.PrepDaysRemainingInCycle,
            DealerName = dealer.Name,
            CurrentActorName = (isArcanaPhase || isTradePhase || isAuctionPhase) ? actor?.Name : null,
            CurrentActorPlayerId = (isArcanaPhase || isTradePhase || isAuctionPhase)
                ? actor?.Id.ToString()
                : null,
            IsMyTurn = isMyTurn,
            CanSubmitOffer = isOfferPhase && selfState is not null && !isFinished,
            CanPlayArcana = canPlayArcana && !isFinished,
            HasSubmittedOffer = hasOffer,
            Altar = altar,
            AvailableRanks = game.AvailableRanks.Select(r => r.ToString()).ToList(),
            MyGems = myGems,
            MyArcana = myArcana,
            MyOffer = myOffer,
            Log = log,
            GemDeckCount = game.GemDeck.Count,
            ArcanaDeckCount = game.ArcanaDeck.Count,
            RelicDeckCount = game.RelicDeck.Count,
            ScorePreview = scorePreview,
            ActiveArcana = activeArcana,
            Trade = trade,
            CanPassTrade = canPassTrade && !isFinished,
            CanProposeTrade = canPassTrade && !isFinished,
            CanVote = canVote && !isFinished,
            HasVoted = selfState?.HasVoted ?? false,
            VoteYesCount = game.Players.Count(p => p.VoteYes == true),
            VoteTotalCast = game.Players.Count(p => p.HasVoted),
            RecoveryRound = game.RecoveryRound,
            Auction = auction,
            CanRaiseAuction = canAuctionAct && !isFinished,
            CanPassAuction = canAuctionAct && !isFinished,
            CanSubmitAllIn = canSubmitAllIn && !isFinished,
            HasSubmittedAllIn = isAllInPhase && hasOffer,
            IsFinished = isFinished,
            CreativeDeck = creativeDeck,
        };
    }

    private static CreativeDeckView ProjectCreativeDeck(GameState game)
    {
        var gems = Enum.GetValues<GemValue>()
            .Select(v => new DeckStockView
            {
                Kind = ((int)v).ToString(),
                DisplayName = $"宝石 {(int)v}",
                Count = game.GemDeck.Cards.Count(c => c.Def.GemValue == v),
                Description = $"面值 {(int)v}",
            })
            .Where(s => s.Count > 0)
            .ToList();

        var arcana = Enum.GetValues<ArcanaKind>()
            .Select(k => new DeckStockView
            {
                Kind = k.ToString(),
                DisplayName = ArcanaName(k),
                Count = game.ArcanaDeck.Cards.Count(c => c.Def.ArcanaKind == k),
                Description = ArcanaDescription(k),
            })
            .Where(s => s.Count > 0)
            .ToList();

        var relics = Enum.GetValues<RelicKind>()
            .Select(k => new DeckStockView
            {
                Kind = k.ToString(),
                DisplayName = RelicName(k),
                Count = game.RelicDeck.Cards.Count(c => c.Def.RelicKind == k),
                Description = RelicDescription(k),
            })
            .Where(s => s.Count > 0)
            .ToList();

        return new CreativeDeckView
        {
            Gems = gems,
            Arcana = arcana,
            Relics = relics,
        };
    }

    private static AuctionView? ProjectAuction(GameState game, PlayerId observerId)
    {
        var a = game.ActiveAuction;
        if (a is null || game.Phase != GamePhase.Recovery_Auction)
            return null;

        var leader = a.CurrentBidLeader is { } lid ? game.FindPlayer(lid) : null;
        var actor = game.CurrentActor;
        var isLeader = a.CurrentBidLeader is { } bl && bl.Equals(observerId);
        var iPassed = a.PassedPlayers.Contains(observerId);

        IReadOnlyList<CardView>? myBidGems = null;
        int? myBidSum = null;
        if (isLeader)
        {
            myBidGems = a.CurrentBidGems.Select(ToCardView).ToList();
            myBidSum = a.CurrentSum;
        }

        var passedIds = a.PassedPlayers.Select(id => id.ToString()).ToList();
        var passedNames = a.PassedPlayers
            .Select(id => game.FindPlayer(id)?.Name ?? "?")
            .ToList();

        return new AuctionView
        {
            RoundIndex = a.RoundIndex,
            MaxAuctionRounds = GameRules.RecoveryAuctionRounds,
            LeaderPlayerId = a.CurrentBidLeader?.ToString(),
            LeaderName = leader?.Name,
            CurrentBidCount = a.CurrentBidCount,
            MyBidGems = myBidGems,
            MyBidSum = myBidSum,
            PassedPlayerIds = passedIds,
            PassedPlayerNames = passedNames,
            CurrentActorPlayerId = actor?.Id.ToString(),
            CurrentActorName = actor?.Name,
            IsMyTurn = actor is not null && actor.Id.Equals(observerId) && !iPassed,
            IHavePassed = iPassed,
            IAmLeader = isLeader,
        };
    }

    private static ActiveArcanaView? ProjectActiveArcana(GameState game, PlayerId observerId)
    {
        var a = game.ActiveArcana;
        if (a is null)
            return null;

        var actor = game.FindPlayer(a.ActorId);
        var isActor = observerId.Equals(a.ActorId);
        var isTarget = a.TargetPlayerIds.Any(t => t.Equals(observerId));

        // Peek: one-shot card snapshot only during Reveal, only to the actor.
        // After Ack, TempCards/rank UI snapshot is cleared; PeekReminder text remains for actor.
        PeekedOfferView? peeked = null;
        var isPeekReveal = a.Kind == ArcanaKind.Peek
            && string.Equals(a.StepId, "Reveal", StringComparison.OrdinalIgnoreCase);
        if (isPeekReveal && isActor && a.TargetPlayerIds.Count > 0 && a.PeekSnapshotRank is not null)
        {
            var tid = a.TargetPlayerIds[0];
            var tp = game.FindPlayer(tid);
            peeked = new PeekedOfferView
            {
                TargetPlayerId = tid.ToString(),
                TargetName = tp?.Name ?? "?",
                Rank = a.PeekSnapshotRank,
                Gems = a.TempCards.Select(ToCardView).ToList(),
                Sum = a.PeekSnapshotSum,
            };
        }

        string? peekReminder = null;
        if (a.Kind == ArcanaKind.Peek && isActor && !isPeekReveal
            && a.Data.TryGetValue("PeekReminder", out var reminderText)
            && !string.IsNullOrWhiteSpace(reminderText))
        {
            peekReminder = reminderText;
        }

        // Breeding/Transplant temp cards stay on TempCards for actor; Peek uses them only as reveal snapshot.
        var tempCards = isActor && a.Kind != ArcanaKind.Peek
            ? a.TempCards.Select(ToCardView).ToList()
            : new List<CardView>();

        var iHaveFinished = a.Kind == ArcanaKind.Peek && (
            (isActor && a.ActorFinished) || (isTarget && a.TargetFinished));

        var canRespond = a.Kind switch
        {
            ArcanaKind.Peek when isPeekReveal => isActor,
            ArcanaKind.Peek => (isActor || isTarget) && !iHaveFinished,
            ArcanaKind.ArtificialBreeding => isActor,
            ArcanaKind.Transplant => isActor,
            _ => isActor,
        };

        var prompt = a.Prompt;
        if (a.Kind == ArcanaKind.Peek)
        {
            if (isPeekReveal && !isActor)
                prompt = $"{actor?.Name ?? "施术者"} 正在确认窥视结果......";
            else if (!isPeekReveal && iHaveFinished)
                prompt = "你已完成重供选择，等待对方......";
            else if (!isPeekReveal && (isActor || isTarget))
                prompt = "可重新供奉（先退回原宝石）或保持原供奉；双方都决定后秘术结束";
            else if (!isPeekReveal)
                prompt = "双方正在处理窥视后的重供选择......";

            // Actor: keep opponent offer in mind during ReOffer (also mirrored in private log).
            if (peekReminder is not null)
                prompt = $"【窥视提醒】对方供奉 {peekReminder}。{prompt}";
        }

        return new ActiveArcanaView
        {
            Kind = a.Kind.ToString(),
            KindDisplayName = ArcanaName(a.Kind),
            ActorPlayerId = a.ActorId.ToString(),
            ActorName = actor?.Name ?? "?",
            StepId = a.StepId,
            Prompt = prompt,
            IsActor = isActor,
            CanRespond = canRespond,
            IHaveFinished = iHaveFinished,
            PeekedOffer = peeked,
            PeekReminder = peekReminder,
            TempCards = tempCards,
            TargetPlayerIds = a.TargetPlayerIds.Select(id => id.ToString()).ToList(),
            TargetPlayerNames = a.TargetPlayerIds
                .Select(id => game.FindPlayer(id)?.Name ?? "?")
                .ToList(),
        };
    }

    private static TradeView? ProjectTrade(GameState game, PlayerId observerId)
    {
        var t = game.ActiveTrade;
        if (t is null)
            return null;

        var buyer = game.FindPlayer(t.BuyerId);
        var seller = game.FindPlayer(t.SellerId);
        var relic = seller?.Relics.FirstOrDefault(r => r.Id.Equals(t.RelicInstanceId));
        var isBuyer = observerId.Equals(t.BuyerId);
        var isSeller = observerId.Equals(t.SellerId);

        // Bargain visibility: others see count only; buyer sees faces.
        // Force-buy: dual-blind until both committed — neither sees the other's bid.
        IReadOnlyList<CardView>? buyerGems = null;
        if (isBuyer)
            buyerGems = t.BuyerEscrow.Select(ToCardView).ToList();

        IReadOnlyList<CardView>? sellerGems = null;
        int? sellerCountPrivate = null;
        if (isSeller && t.SubPhase == TradeSubPhase.ForceBuyBids)
        {
            sellerGems = t.SellerEscrow.Select(ToCardView).ToList();
            sellerCountPrivate = t.SellerEscrow.Count;
        }

        // After both committed force buy is resolved immediately — no reveal window.
        // During bargain, seller and others only see buyer count.
        var publicBuyerCount = t.SubPhase == TradeSubPhase.ForceBuyBids && !isBuyer
            ? 0 // dual-blind: hide buyer count from non-buyer during force
            : t.BuyerEscrowCount;
        // Buyer still sees own count via BuyerEscrowGems
        if (isBuyer)
            publicBuyerCount = t.BuyerEscrowCount;

        return new TradeView
        {
            SubPhase = t.SubPhase.ToString(),
            BuyerPlayerId = t.BuyerId.ToString(),
            BuyerName = buyer?.Name ?? "?",
            SellerPlayerId = t.SellerId.ToString(),
            SellerName = seller?.Name ?? "?",
            RelicInstanceId = t.RelicInstanceId.ToString(),
            RelicDisplayName = RelicName(relic?.Def.RelicKind),
            BuyerEscrowCount = publicBuyerCount,
            BuyerEscrowGems = buyerGems,
            IsBuyer = isBuyer,
            IsSeller = isSeller,
            BuyerBidCommitted = t.BuyerBidCommitted,
            SellerBidCommitted = t.SellerBidCommitted,
            SellerEscrowGems = sellerGems,
            SellerEscrowCountPrivate = sellerCountPrivate,
        };
    }

    private static string BuildHint(
        GameState game,
        bool isMyTurn,
        bool hasOffer,
        bool isOfferPhase,
        Domain.Players.PlayerState? self)
    {
        if (game.ActiveArcana is not null)
            return game.ActiveArcana.Prompt ?? $"多步秘术进行中：{ArcanaName(game.ActiveArcana.Kind)}";

        if (game.ActiveTrade is not null)
        {
            return game.ActiveTrade.SubPhase switch
            {
                TradeSubPhase.AwaitSellerResponse =>
                    $"交易议价：等待卖家 {game.FindPlayer(game.ActiveTrade.SellerId)?.Name} 回应",
                TradeSubPhase.AwaitBuyerChoice =>
                    $"交易被拒：等待买家 {game.FindPlayer(game.ActiveTrade.BuyerId)?.Name} 原路返回或强买",
                TradeSubPhase.ForceBuyBids => "强买进行中：双方提交暗价",
                _ => "交易进行中",
            };
        }

        return game.Phase switch
        {
            GamePhase.Prep_Offer when hasOffer =>
                $"供奉已提交（{game.Offers.Count}/{game.PlayerCount}），可撤回修改，等待他人…",
            GamePhase.Prep_Offer =>
                "选择序号与宝石，提交供奉（他人仅见你的供奉张数）",
            GamePhase.Prep_ArcanaTurn when isMyTurn =>
                "轮到你：打出 1 张秘术，或跳过",
            GamePhase.Prep_ArcanaTurn =>
                $"等待 {game.CurrentActor?.Name ?? "他人"} 的秘术行动…",
            GamePhase.Prep_Resolve => "结算中…",
            GamePhase.Prep_Draw => "摸牌中…",
            GamePhase.OfferingDay_Trade when isMyTurn =>
                "轮到你：发起交易或跳过",
            GamePhase.OfferingDay_Trade =>
                $"等待 {game.CurrentActor?.Name ?? "他人"} 的交易行动…",
            GamePhase.VoteContinue when self is { HasVoted: false } =>
                $"已完成 {game.CycleIndex + 1} 个循环，投票：是否继续下一循环（筹备×3+供奉×1）？",
            GamePhase.VoteContinue =>
                $"已投票，等待他人…（{game.Players.Count(p => p.HasVoted)}/{game.PlayerCount}）",
            GamePhase.Recovery_Auction when game.ActiveAuction is { } auc && isMyTurn =>
                $"复苏竞拍第 {auc.RoundIndex} 次：轮到你 — 选宝石加价（sum 须严格大于当前最高价）或放弃",
            GamePhase.Recovery_Auction when game.ActiveAuction is { } auc =>
                $"复苏竞拍第 {auc.RoundIndex} 次：当前最高 {auc.CurrentBidCount} 张" +
                (auc.CurrentBidLeader is null ? "（尚无出价）" : $" · 领先 {game.FindPlayer(auc.CurrentBidLeader.Value)?.Name}") +
                $" · 等待 {game.CurrentActor?.Name ?? "他人"}",
            GamePhase.Recovery_AllIn when hasOffer =>
                $"全下已提交（{game.Offers.Count}/{game.Players.Count(p => p.GemHand.Count > 0 || game.Offers.ContainsKey(p.Id))}），可改序号重交，等待他人…",
            GamePhase.Recovery_AllIn =>
                "复苏全下：选择序号后提交（将押上全部手牌宝石）",
            GamePhase.FinalScoring or GamePhase.Finished =>
                "对局结束 — 见终局计分",
            _ => $"第 {game.PrepDayNumber} 个筹备日 · 循环 {game.CycleIndex + 1}",
        };
    }

    private static CardView ToCardView(CardInstance c)
    {
        return new CardView
        {
            InstanceId = c.Id.ToString(),
            Type = c.Def.Type.ToString(),
            Kind = c.Def.Type switch
            {
                CardType.Arcana => c.Def.ArcanaKind?.ToString(),
                CardType.Relic => c.Def.RelicKind?.ToString(),
                CardType.Gem => c.Def.GemValue?.ToString(),
                _ => null,
            },
            FaceValue = c.Def.Type == CardType.Gem ? c.Def.FaceValue : null,
            DisplayName = c.Def.Type switch
            {
                CardType.Gem => $"宝石 {c.Def.FaceValue}",
                CardType.Arcana => ArcanaName(c.Def.ArcanaKind),
                CardType.Relic => RelicName(c.Def.RelicKind),
                _ => c.Def.DisplayKey,
            },
            Description = c.Def.Type switch
            {
                CardType.Gem => $"面值 {c.Def.FaceValue}，供奉时计入点数之和",
                CardType.Arcana => ArcanaDescription(c.Def.ArcanaKind),
                CardType.Relic => RelicDescription(c.Def.RelicKind),
                _ => "",
            },
        };
    }

    public static string PhaseDisplayName(GamePhase phase) => phase switch
    {
        GamePhase.Lobby => "大厅",
        GamePhase.Prep_Draw => "筹备日 · 摸牌",
        GamePhase.Prep_Offer => "筹备日 · 供奉",
        GamePhase.Prep_ArcanaTurn => "筹备日 · 秘术",
        GamePhase.Prep_Resolve => "筹备日 · 结算",
        GamePhase.OfferingDay_Trade => "供奉日 · 交易",
        GamePhase.VoteContinue => "投票",
        GamePhase.Recovery_Auction => "复苏日 · 竞拍",
        GamePhase.Recovery_AllIn => "复苏日 · 全下",
        GamePhase.FinalScoring => "终局计分",
        GamePhase.Finished => "已结束",
        _ => phase.ToString(),
    };

    /// <summary>Classify public log codes for UI highlight (desensitized already).</summary>
    /// <remarks>Personal logs use level <c>private</c> (blue) and are assigned in ProjectGame, not here.</remarks>
    public static (bool Highlight, string Level) ClassifyLog(string code) => code switch
    {
        "GameStart" or "FinalScore" or "GameFinished" or "AllInResolve" => (true, "success"),
        "PrepResolveDone" or "RecoveryStart" or "CalendarContinue" or "CalendarFinalPrep" => (true, "warn"),
        "RelicWon" or "AuctionWon" or "TradeAccept" or "TradeForceSuccess" => (true, "success"),
        "RelicVoid" or "RelicTie" or "AuctionVoid" or "DeadPlayers" or "TradeForceFail" => (true, "danger"),
        "VoteStart" or "VoteResult" or "DebugSeed" or "SeatOrder" or "CreativeStart" => (true, "warn"),
        var c when c.StartsWith("Arcana_", StringComparison.Ordinal) => (true, "info"),
        var c when c.StartsWith("Creative", StringComparison.Ordinal) => (true, "info"),
        _ => (false, "info"),
    };

    public static string RelicName(RelicKind? kind) => kind switch
    {
        RelicKind.VisionEye => "灵视眼瞳",
        RelicKind.WeirdStatue => "诡异雕像",
        RelicKind.Necronomicon => "死灵之书",
        RelicKind.BrokenScript => "残缺咒文",
        RelicKind.Lantern => "提灯",
        RelicKind.ObsidianCup => "黑曜石酒杯",
        RelicKind.BloodyBone => "沾血手骨",
        RelicKind.HolyMedium => "神圣媒介",
        RelicKind.RitualTool => "仪式用具",
        RelicKind.ForbiddenKnowledge => "禁忌知识",
        _ => kind?.ToString() ?? "祭品",
    };

    public static string ArcanaName(ArcanaKind? kind) => kind switch
    {
        ArcanaKind.RlyehFog => "拉莱耶迷雾",
        ArcanaKind.StaffOfForgetting => "遗忘权杖",
        ArcanaKind.FelReplenish => "邪能补充",
        ArcanaKind.Peek => "窥视",
        ArcanaKind.MaliciousSwap => "恶意置换",
        ArcanaKind.Alchemy => "点金术",
        ArcanaKind.MentalInterference => "精神干扰",
        ArcanaKind.MightyGrasp => "巨力擒缚",
        ArcanaKind.BlackWind => "黑风术",
        ArcanaKind.ArtificialBreeding => "人工选育",
        ArcanaKind.Spiritism => "通灵术",
        ArcanaKind.FishingNet => "渔网",
        ArcanaKind.KnowledgeErosion => "知识侵蚀",
        ArcanaKind.FearResonance => "恐惧共鸣",
        ArcanaKind.Transplant => "移植",
        ArcanaKind.Omniscient => "全知者",
        ArcanaKind.ReverseThinking => "逆向思维",
        ArcanaKind.MindSuggestion => "心灵暗示",
        ArcanaKind.Fanaticism => "狂信",
        ArcanaKind.EvilProphecy => "邪恶预言",
        _ => kind?.ToString() ?? "秘术",
    };

    /// <summary>End-game scoring rule for a relic kind (hover tooltip).</summary>
    public static string RelicDescription(RelicKind? kind) => kind switch
    {
        RelicKind.VisionEye => "终局计分：每凑齐 2 张得 10 分，奇数张不计分",
        RelicKind.WeirdStatue => "终局计分：全场总计超过 3 张时每张 3 分，否则每张 7 分",
        RelicKind.Necronomicon => "终局计分：持有时 +6 分（最多计 1 张）",
        RelicKind.BrokenScript => "终局计分：每张 3 分",
        RelicKind.Lantern => "终局计分：全场总计超过 3 张时每张 5 分，否则每张 2 分",
        RelicKind.ObsidianCup => "终局计分：每张 1 分；持有数量最多者额外 +15（并列均得）",
        RelicKind.BloodyBone => "终局计分：每张 4 分",
        RelicKind.HolyMedium => "终局计分：每张 8 分",
        RelicKind.RitualTool => "终局计分：每张 2 分；持有 ≥3 张时额外 +15",
        RelicKind.ForbiddenKnowledge => "终局计分：1 张 −3 / 2 张 +9 / 3 张 −27 / 4 张及以上 0 分（含虚禁）",
        _ => "祭品：终局按持有规则计分",
    };

    /// <summary>Play effect blurb for an arcana kind (hover tooltip).</summary>
    public static string ArcanaDescription(ArcanaKind? kind) => kind switch
    {
        ArcanaKind.FishingNet => "抽取 2 张宝石",
        ArcanaKind.Omniscient => "宝石少于 5 张的玩家各抽 1 张宝石",
        ArcanaKind.RlyehFog => "指定 2 个祭坛槽位，互换其上的祭品",
        ArcanaKind.StaffOfForgetting => "弃置指定祭坛槽位的祭品，并从牌堆补新",
        ArcanaKind.FelReplenish => "秘术少于 2 张的玩家各摸 1 张秘术（不消耗本回合出牌）",
        ArcanaKind.ReverseThinking => "指定 1 个祭坛槽位：该槽改为最低者得",
        ArcanaKind.EvilProphecy => "指定 1 个祭坛槽位：其上 sum<4 的供奉无效",
        ArcanaKind.Alchemy => "弃掉全部宝石手牌，再抽等量宝石",
        ArcanaKind.Peek => "一次性查看一名其他玩家的供奉；随后双方可重供或保持",
        ArcanaKind.MaliciousSwap => "将目标玩家的 1 张祭品与祭坛 1 个槽位互换",
        ArcanaKind.MentalInterference => "令一名其他玩家重新选择供奉序号",
        ArcanaKind.MightyGrasp => "从一名有宝石的玩家手中随机拿走 1 张宝石",
        ArcanaKind.BlackWind => "全员顺时针交换供奉宝石（序号不变）",
        ArcanaKind.ArtificialBreeding => "从牌堆拿 3 张宝石，保留 1 张",
        ArcanaKind.Spiritism => "猜目标供奉序号：猜对自己抽 3 宝石，猜错目标抽 1 宝石",
        ArcanaKind.KnowledgeErosion => "目标的虚拟禁忌知识 +1",
        ArcanaKind.FearResonance => "指定 2 名有宝石的玩家，各随机丢弃 1 张宝石",
        ArcanaKind.Transplant => "从 2 名有宝石玩家各拿 1 张随机宝石，再分别归还 1 张",
        ArcanaKind.MindSuggestion => "任选 2 名玩家，交换他们的供奉宝石",
        ArcanaKind.Fanaticism => "与目标交换全部宝石手牌",
        _ => "打出以发动秘术效果",
    };
}
