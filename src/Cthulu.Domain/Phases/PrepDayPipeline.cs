using Cthulu.Domain.Cards;
using Cthulu.Domain.Effects;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;
using Cthulu.Domain.Random;

namespace Cthulu.Domain.Phases;

/// <summary>
/// Prep-day state machine: create game, initial deal (R14), daily draw (R15),
/// parallel offers (R7), arcana turns (R9), resolve (R1–R3, R19–R20), calendar (R10).
/// </summary>
public static class PrepDayPipeline
{
    public static GameState CreateGame(
        IReadOnlyList<(PlayerId Id, string Name, int SeatIndex)> seats,
        IRandom? rng = null)
    {
        if (seats.Count < GameRules.MinPlayers || seats.Count > GameRules.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(seats), "Player count must be 4–6.");

        rng ??= new SystemRandom();
        var (gems, arcana, relics) = CardCatalog.CreateShuffledDecks(rng);
        var players = seats
            .Select(s => new PlayerState(s.Id, s.Name, s.SeatIndex))
            .ToList();

        return new GameState(GameId.New(), players, gems, arcana, relics, rng);
    }

    /// <summary>
    /// StartGame: initial deal (R14) + first prep draw/altar (R15) → Prep_Offer.
    /// </summary>
    public static void StartGame(GameState state)
    {
        if (state.Phase != GamePhase.Lobby)
            throw new InvalidOperationException("Game already started.");

        state.DealerSeat = 0;
        state.CycleIndex = 0;
        state.PrepDaysRemainingInCycle = GameRules.StandardPrepDaysPerCycle;
        state.NextSegmentIsRecovery = false;
        DealInitialHands(state);
        state.Log("GameStart", $"对局开始，庄家为 {state.PlayerAtSeat(state.DealerSeat).Name}");
        BeginPrepDay(state);
    }

    /// <summary>R14: from dealer, each player gets gems 1–5 ×1 only (no arcana).</summary>
    public static void DealInitialHands(GameState state)
    {
        foreach (var player in state.SeatsFromDealer())
        {
            foreach (GemValue v in Enum.GetValues<GemValue>())
            {
                if (!state.GemDeck.TryTakeGem(v, out var card) || card is null)
                    throw new InvalidOperationException($"Cannot deal gem {v} — deck exhausted.");
                player.GemHand.Add(card);
            }
            // R14: no initial arcana — first arcana come from prep-day soft-cap draw (R15).
        }

        state.Log("InitialDeal", "初始发牌完成：每人宝石 1–5 各一（不发秘术）");
    }

    /// <summary>R15 + reveal altar → Prep_Offer.</summary>
    public static void BeginPrepDay(GameState state)
    {
        state.Phase = GamePhase.Prep_Draw;
        state.PrepDayNumber++;
        ClearPrepRound(state);

        foreach (var player in state.SeatsFromDealer())
        {
            var gems = DrawPileService.Draw(state.GemDeck, state.GemDiscard, GameRules.PrepDrawGems);
            player.GemHand.AddRange(gems);
        }

        foreach (var player in state.SeatsFromDealer())
        {
            if (player.ArcanaHand.Count < GameRules.ArcanaHandSoftCap)
            {
                var a = DrawPileService.Draw(state.ArcanaDeck, state.ArcanaDiscard, 1);
                player.ArcanaHand.AddRange(a);
            }
        }

        var n = state.AltarSlotCount;
        var altar = DrawPileService.Draw(state.RelicDeck, state.RelicDiscard, n);
        state.Altar.AddRange(altar);

        state.Phase = GamePhase.Prep_Offer;
        state.Log(
            "PrepDayStart",
            $"第 {state.PrepDayNumber} 个筹备日（段内剩余含本日 {state.PrepDaysRemainingInCycle}）：" +
            $"每人摸 {GameRules.PrepDrawGems} 张宝石；从祭品牌堆抽取 {state.Altar.Count} 张至祭坛");
    }

    private static void ClearPrepRound(GameState state)
    {
        if (state.Altar.Count > 0)
        {
            state.RelicDiscard.AddRange(state.Altar);
            state.Altar.Clear();
        }

        foreach (var (pid, offer) in state.Offers.ToList())
        {
            var p = state.FindPlayer(pid);
            p?.GemHand.AddRange(offer.Gems);
        }

        state.Offers.Clear();
        state.AltarModifiers.Clear();
        state.ActiveArcana = null;
        foreach (var p in state.Players)
            p.HasActedArcana = false;
    }

    public static DomainResult SubmitOffer(
        GameState state,
        PlayerId playerId,
        SequenceRank rank,
        IReadOnlyList<CardInstanceId> gemIds)
    {
        if (state.Phase != GamePhase.Prep_Offer)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉阶段");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (!GameRules.IsValidRank(rank, state.PlayerCount))
            return DomainResult.Fail(DomainErrorCodes.InvalidTarget, "无效的序号");

        if (gemIds is null || gemIds.Count == 0)
            return DomainResult.Fail(DomainErrorCodes.OfferIncomplete, "至少供奉 1 张宝石");

        if (gemIds.Distinct().Count() != gemIds.Count)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");

        if (state.Offers.TryGetValue(playerId, out var existing))
        {
            player.GemHand.AddRange(existing.Gems);
            state.Offers.Remove(playerId);
        }

        var selected = new List<CardInstance>(gemIds.Count);
        foreach (var id in gemIds)
        {
            var card = player.FindGem(id);
            if (card is null)
                return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
            selected.Add(card);
        }

        foreach (var c in selected)
            player.GemHand.Remove(c);

        var offer = new Offer { Rank = rank };
        offer.Gems.AddRange(selected);
        state.Offers[playerId] = offer;

        state.Log("OfferSubmit", $"{player.Name} 提交供奉（序号 {rank}，{offer.GemCount} 张宝石）");

        if (state.Offers.Count >= state.PlayerCount &&
            state.Players.All(p => state.Offers.ContainsKey(p.Id)))
        {
            EnterArcanaTurn(state);
        }

        return DomainResult.Success();
    }

    public static DomainResult ClearOffer(GameState state, PlayerId playerId)
    {
        if (state.Phase != GamePhase.Prep_Offer)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是供奉阶段");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (!state.Offers.TryGetValue(playerId, out var offer))
            return DomainResult.Success();

        player.GemHand.AddRange(offer.Gems);
        state.Offers.Remove(playerId);
        state.Log("OfferClear", $"{player.Name} 撤回供奉");
        return DomainResult.Success();
    }

    private static void EnterArcanaTurn(GameState state)
    {
        state.Phase = GamePhase.Prep_ArcanaTurn;
        state.CurrentActorSeat = state.DealerSeat;
        state.ActiveArcana = null;
        foreach (var p in state.Players)
            p.HasActedArcana = false;
        state.Log("ArcanaRound", $"进入秘术回合，从庄家 {state.PlayerAtSeat(state.DealerSeat).Name} 开始");
    }

    public static DomainResult PassArcana(GameState state, PlayerId playerId)
    {
        if (state.Phase != GamePhase.Prep_ArcanaTurn)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是秘术回合");
        if (state.ActiveArcana is not null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前有未完成的多步秘术");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (player.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");

        player.HasActedArcana = true;
        state.Log("ArcanaPass", $"{player.Name} 跳过秘术");
        AdvanceArcanaActor(state);
        return DomainResult.Success();
    }

    public static DomainResult PlayArcana(
        GameState state,
        PlayerId playerId,
        ArcanaKind kind,
        ArcanaTarget? target = null)
    {
        if (state.Phase != GamePhase.Prep_ArcanaTurn)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是秘术回合");
        if (state.ActiveArcana is not null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前有未完成的多步秘术");

        var player = state.FindPlayer(playerId);
        if (player is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (player.SeatIndex != state.CurrentActorSeat)
            return DomainResult.Fail(DomainErrorCodes.NotYourTurn, "还没轮到你");

        if (!ArcanaEffectRegistry.IsEnabled(kind))
            return DomainResult.Fail(
                DomainErrorCodes.ArcanaNotEnabled,
                $"秘术「{ArcanaDisplayName(kind)}」未启用（ArcanaNotEnabled）");

        var card = player.FindArcana(kind);
        if (card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "你没有这张秘术牌");

        var effect = ArcanaEffectRegistry.Get(kind);
        if (effect is null)
            return DomainResult.Fail(
                DomainErrorCodes.ArcanaNotEnabled,
                $"秘术「{ArcanaDisplayName(kind)}」无效果实现（ArcanaNotEnabled）");

        target ??= new ArcanaTarget();

        var validation = effect.Validate(state, playerId, target);
        if (!validation.Ok)
            return DomainResult.Fail(
                validation.ErrorCode ?? DomainErrorCodes.EffectInvalid,
                validation.Message ?? "效果无效");

        // Apply while card still in hand (FelReplenish counts correctly)
        var result = effect.Apply(state, playerId, target);

        // Discard the played arcana immediately (even for multi-step)
        player.ArcanaHand.Remove(card);
        state.ArcanaDiscard.Add(card);

        if (result.NeedMoreInput)
        {
            // ActiveArcana already set by effect; do not advance yet
            if (state.ActiveArcana is not null)
            {
                state.ActiveArcana.StepId = result.StepId ?? state.ActiveArcana.StepId;
                state.ActiveArcana.Prompt = result.Prompt ?? state.ActiveArcana.Prompt;
            }
            return DomainResult.Success();
        }

        // DoesNotConsumeAction (e.g. FelReplenish): stay on this actor for another play/pass
        if (!effect.DoesNotConsumeAction)
        {
            player.HasActedArcana = true;
            AdvanceArcanaActor(state);
        }
        return DomainResult.Success();
    }

    /// <summary>Continue multi-step arcana (ENGINEERING ArcanaStepResponse).</summary>
    public static DomainResult ArcanaStepResponse(
        GameState state,
        PlayerId responderId,
        string stepId,
        ArcanaTarget? target = null)
    {
        if (state.Phase != GamePhase.Prep_ArcanaTurn)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前不是秘术回合");
        if (state.ActiveArcana is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "当前没有进行中的秘术");

        var active = state.ActiveArcana;
        var effect = ArcanaEffectRegistry.Get(active.Kind);
        if (effect is null)
            return DomainResult.Fail(DomainErrorCodes.EffectInvalid, "秘术效果丢失");

        target ??= new ArcanaTarget();
        var result = effect.Continue(state, responderId, stepId, target);

        if (result.Failed)
            return DomainResult.Fail(
                result.ErrorCode ?? DomainErrorCodes.EffectInvalid,
                result.Message ?? "秘术步骤无效");

        if (result.NeedMoreInput)
        {
            if (state.ActiveArcana is not null)
            {
                state.ActiveArcana.StepId = result.StepId ?? state.ActiveArcana.StepId;
                state.ActiveArcana.Prompt = result.Prompt ?? state.ActiveArcana.Prompt;
            }
            return DomainResult.Success();
        }

        // Completed
        state.ActiveArcana = null;
        if (!effect.DoesNotConsumeAction)
        {
            var actor = state.FindPlayer(active.ActorId);
            if (actor is not null)
                actor.HasActedArcana = true;
            AdvanceArcanaActor(state);
        }
        return DomainResult.Success();
    }

    private static void AdvanceArcanaActor(GameState state)
    {
        if (state.Players.All(p => p.HasActedArcana))
        {
            ResolvePrep(state);
            return;
        }

        var seat = state.CurrentActorSeat;
        for (var i = 0; i < state.PlayerCount; i++)
        {
            seat = state.NextSeat(seat);
            var p = state.PlayerAtSeat(seat);
            if (!p.HasActedArcana)
            {
                state.CurrentActorSeat = seat;
                return;
            }
        }

        ResolvePrep(state);
    }

    /// <summary>Prep_Resolve pipeline (ENGINEERING §11.3 / §24).</summary>
    public static void ResolvePrep(GameState state)
    {
        state.Phase = GamePhase.Prep_Resolve;
        state.ActiveArcana = null;

        ResolveOffersToRelics(state);

        state.Log("PrepResolveDone", $"第 {state.PrepDayNumber} 个筹备日结算完成");
        AdvanceCalendarAfterPrep(state);
    }

    /// <summary>R10: after prep resolve, continue segment / offering / recovery placeholder.</summary>
    public static void AdvanceCalendarAfterPrep(GameState state)
    {
        state.PrepDaysRemainingInCycle--;
        state.DealerSeat = state.NextSeat(state.DealerSeat);
        state.Log("DealerRotate", $"庄家轮换为 {state.PlayerAtSeat(state.DealerSeat).Name}");

        if (state.PrepDaysRemainingInCycle > 0)
        {
            BeginPrepDay(state);
            return;
        }

        if (state.NextSegmentIsRecovery)
        {
            RecoveryDayPipeline.BeginRecovery(state);
            return;
        }

        OfferingDayPipeline.BeginOfferingDay(state);
    }

    /// <summary>
    /// Resolve altar offers (R1–R3, R19–R20): award relics, discard spent gems.
    /// Does not advance the calendar (used by prep resolve and recovery all-in).
    /// </summary>
    public static void ResolveOffersToRelics(GameState state)
    {
        var dead = ComputeDeadPlayers(state);
        if (dead.Count > 0)
        {
            var names = string.Join("、", dead.Select(id => state.FindPlayer(id)?.Name ?? "?"));
            state.Log("DeadPlayers", $"死掉（同序号且同张数）：{names}");
        }

        for (var slot = 0; slot < state.Altar.Count; slot++)
        {
            var rank = GameRules.SlotIndexToRank(slot);
            var candidates = new List<(PlayerState Player, int Power)>();

            foreach (var p in state.Players)
            {
                if (dead.Contains(p.Id))
                    continue;
                if (!state.Offers.TryGetValue(p.Id, out var offer))
                    continue;
                if (offer.Rank != rank)
                    continue;

                var power = offer.Sum;
                if (state.AltarModifiers.HasEvilProphecy(slot) && power < 4)
                    continue;

                candidates.Add((p, power));
            }

            var relic = state.Altar[slot];
            if (candidates.Count == 0)
            {
                state.RelicDiscard.Add(relic);
                state.Log("RelicVoid", $"祭坛槽 {slot + 1}（{RelicDisplay(relic)}）无人有效供奉，进入弃牌");
                continue;
            }

            var reverse = state.AltarModifiers.HasReverse(slot);
            var targetPower = reverse
                ? candidates.Min(c => c.Power)
                : candidates.Max(c => c.Power);
            var winners = candidates.Where(c => c.Power == targetPower).ToList();

            if (winners.Count == 1)
            {
                var w = winners[0].Player;
                w.Relics.Add(relic);
                state.Log(
                    "RelicWon",
                    $"{w.Name} 获得祭坛槽 {slot + 1} 的 {RelicDisplay(relic)}" +
                    (reverse ? "（逆向）" : "") +
                    $"（sum={targetPower}）");
            }
            else
            {
                state.RelicDiscard.Add(relic);
                state.Log(
                    "RelicTie",
                    $"祭坛槽 {slot + 1}（{RelicDisplay(relic)}）并列，无人获得");
            }
        }

        foreach (var offer in state.Offers.Values)
            state.GemDiscard.AddRange(offer.Gems);
        state.Offers.Clear();
        state.Altar.Clear();
        state.AltarModifiers.Clear();
        foreach (var p in state.Players)
            p.HasActedArcana = false;
    }

    /// <summary>R3: two+ players with same Rank AND same GemCount are dead.</summary>
    public static HashSet<PlayerId> ComputeDeadPlayers(GameState state)
    {
        var dead = new HashSet<PlayerId>();
        var groups = state.Offers
            .GroupBy(kv => (kv.Value.Rank, kv.Value.GemCount))
            .Where(g => g.Count() >= 2);

        foreach (var g in groups)
        {
            foreach (var kv in g)
                dead.Add(kv.Key);
        }

        return dead;
    }

    private static string RelicDisplay(CardInstance card) =>
        card.Def.RelicKind switch
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
            _ => card.Def.DisplayKey,
        };

    private static string ArcanaDisplayName(ArcanaKind kind) => kind switch
    {
        ArcanaKind.FishingNet => "渔网",
        ArcanaKind.Omniscient => "全知者",
        ArcanaKind.RlyehFog => "拉莱耶迷雾",
        ArcanaKind.StaffOfForgetting => "遗忘权杖",
        ArcanaKind.FelReplenish => "邪能补充",
        ArcanaKind.ReverseThinking => "逆向思维",
        ArcanaKind.EvilProphecy => "邪恶预言",
        ArcanaKind.Alchemy => "点金术",
        ArcanaKind.Peek => "窥视",
        ArcanaKind.MaliciousSwap => "恶意置换",
        ArcanaKind.MentalInterference => "精神干扰",
        ArcanaKind.MightyGrasp => "巨力擒缚",
        ArcanaKind.BlackWind => "黑风术",
        ArcanaKind.ArtificialBreeding => "人工选育",
        ArcanaKind.Spiritism => "通灵术",
        ArcanaKind.KnowledgeErosion => "知识侵蚀",
        ArcanaKind.FearResonance => "恐惧共鸣",
        ArcanaKind.Transplant => "移植",
        ArcanaKind.MindSuggestion => "心灵暗示",
        ArcanaKind.Fanaticism => "狂信",
        _ => kind.ToString(),
    };
}
