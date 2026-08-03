using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;

namespace Cthulu.Domain.Effects;

internal static class ArcanaHelpers
{
    public static EffectValidation RequireOtherPlayer(
        GameState state, PlayerId actor, ArcanaTarget target, int count = 1)
    {
        if (target.PlayerIds.Count != count)
            return EffectValidation.Fail(
                DomainErrorCodes.InvalidTarget,
                count == 1 ? "需指定 1 名玩家" : $"需指定 {count} 名玩家");

        var seen = new HashSet<PlayerId>();
        foreach (var pid in target.PlayerIds)
        {
            if (!seen.Add(pid))
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标玩家不可重复");
            if (pid.Equals(actor) && count == 1)
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "不能指定自己");
            if (state.FindPlayer(pid) is null)
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标不在对局中");
        }

        return EffectValidation.Success();
    }

    public static EffectValidation RequirePlayersWithGems(
        GameState state, PlayerId actor, ArcanaTarget target, int count, bool allowSelf = true)
    {
        if (target.PlayerIds.Count != count)
            return EffectValidation.Fail(
                DomainErrorCodes.InvalidTarget, $"需指定 {count} 名有宝石的玩家");

        var seen = new HashSet<PlayerId>();
        foreach (var pid in target.PlayerIds)
        {
            if (!seen.Add(pid))
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标玩家不可重复");
            if (!allowSelf && pid.Equals(actor))
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "不能指定自己");
            var p = state.FindPlayer(pid);
            if (p is null)
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标不在对局中");
            if (p.GemHand.Count == 0)
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, $"{p.Name} 没有宝石");
        }

        return EffectValidation.Success();
    }
}

/// <summary>
/// 窥视：指定他玩家，查看其供奉后双方可重供奉（多步）。
/// Steps: ReOffer (actor/target) → Done (actor ends).
/// </summary>
public sealed class PeekEffect : IArcanaEffect
{
    public const string StepReOffer = "ReOffer";
    public const string StepDone = "Done";

    public ArcanaKind Kind => ArcanaKind.Peek;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var v = ArcanaHelpers.RequireOtherPlayer(state, actor, target);
        if (!v.Ok) return v;
        var tid = target.PlayerIds[0];
        if (!state.Offers.ContainsKey(tid))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标尚未供奉");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var tid = target.PlayerIds[0];
        var actorP = state.FindPlayer(actor)!;
        var targetP = state.FindPlayer(tid)!;
        state.ActiveArcana = new ArcanaResolutionState
        {
            Kind = Kind,
            ActorId = actor,
            StepId = StepReOffer,
            Prompt = $"窥视 {targetP.Name} 的供奉；双方可重新供奉，施术者可结束",
        };
        state.ActiveArcana.TargetPlayerIds.Add(tid);
        state.Log("Arcana_Peek", $"{actorP.Name} 使用窥视，查看了 {targetP.Name} 的供奉");
        return EffectApplicationResult.NeedInput(StepReOffer, state.ActiveArcana.Prompt!);
    }

    public EffectApplicationResult Continue(
        GameState state, PlayerId responder, string stepId, ArcanaTarget target)
    {
        var active = state.ActiveArcana;
        if (active is null || active.Kind != Kind)
            return EffectApplicationResult.Done();

        var targetId = active.TargetPlayerIds[0];
        var isActor = responder.Equals(active.ActorId);
        var isTarget = responder.Equals(targetId);
        if (!isActor && !isTarget)
            return EffectApplicationResult.Fail(DomainErrorCodes.Unauthorized, "仅施术者与被窥视者可操作");

        if (string.Equals(stepId, StepDone, StringComparison.OrdinalIgnoreCase))
        {
            if (!isActor)
                return EffectApplicationResult.Fail(DomainErrorCodes.Unauthorized, "仅施术者可结束窥视");
            Finish(state);
            return EffectApplicationResult.Done();
        }

        if (!string.Equals(stepId, StepReOffer, StringComparison.OrdinalIgnoreCase))
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "未知步骤");

        // Re-offer: Rank + gem ids from hand (+ previous offer gems returned first)
        if (target.Rank is null)
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "请指定新序号");
        if (!GameRules.IsValidRank(target.Rank.Value, state.PlayerCount))
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "无效序号");
        if (target.CardIds.Count == 0)
            return EffectApplicationResult.Fail(DomainErrorCodes.OfferIncomplete, "至少供奉 1 张宝石");

        var player = state.FindPlayer(responder)!;
        if (state.Offers.TryGetValue(responder, out var existing))
        {
            player.GemHand.AddRange(existing.Gems);
            state.Offers.Remove(responder);
        }

        var selected = new List<CardInstance>();
        foreach (var id in target.CardIds)
        {
            var card = player.FindGem(id);
            if (card is null)
                return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有指定的宝石");
            selected.Add(card);
        }

        if (selected.Select(c => c.Id).Distinct().Count() != selected.Count)
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");

        foreach (var c in selected)
            player.GemHand.Remove(c);

        var offer = new Offer { Rank = target.Rank.Value };
        offer.Gems.AddRange(selected);
        state.Offers[responder] = offer;

        if (isActor) active.ActorReoffered = true;
        if (isTarget) active.TargetReoffered = true;

        state.Log("Arcana_Peek_ReOffer", $"{player.Name} 在窥视后重新供奉（序号 {offer.Rank}，{offer.GemCount} 张）");

        // Auto-finish when both have re-offered once
        if (active.ActorReoffered && active.TargetReoffered)
        {
            Finish(state);
            return EffectApplicationResult.Done();
        }

        active.StepId = StepReOffer;
        return EffectApplicationResult.NeedInput(
            StepReOffer,
            "可继续重供奉，或施术者结束窥视");
    }

    private static void Finish(GameState state)
    {
        state.ActiveArcana = null;
        state.Log("Arcana_Peek_Done", "窥视结算完成");
    }
}

/// <summary>恶意置换：他玩家 1 祭品 ↔ 祭坛 1 槽。</summary>
public sealed class MaliciousSwapEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.MaliciousSwap;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var v = ArcanaHelpers.RequireOtherPlayer(state, actor, target);
        if (!v.Ok) return v;
        if (target.SlotIndices.Count != 1)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定 1 个祭坛槽位");
        var slot = target.SlotIndices[0];
        if (slot < 0 || slot >= state.Altar.Count)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "祭坛槽位越界");
        if (target.CardIds.Count != 1)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定目标的 1 张祭品");

        var targetP = state.FindPlayer(target.PlayerIds[0])!;
        var relic = targetP.Relics.FirstOrDefault(r => r.Id.Equals(target.CardIds[0]));
        if (relic is null)
            return EffectValidation.Fail(DomainErrorCodes.InvalidCards, "目标没有该祭品");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var targetP = state.FindPlayer(target.PlayerIds[0])!;
        var slot = target.SlotIndices[0];
        var relic = targetP.Relics.First(r => r.Id.Equals(target.CardIds[0]));
        var altarCard = state.Altar[slot];

        targetP.Relics.Remove(relic);
        state.Altar[slot] = relic;
        targetP.Relics.Add(altarCard);

        // Traded flags: altar card newly held is not "traded successfully"
        targetP.RelicsTradedSuccessfully.Remove(relic.Id);

        var actorP = state.FindPlayer(actor)!;
        state.Log(
            "Arcana_MaliciousSwap",
            $"{actorP.Name} 使用恶意置换，将 {targetP.Name} 的祭品与祭坛槽 {slot + 1} 互换");
        return EffectApplicationResult.Done();
    }
}

/// <summary>精神干扰：令除自己外的目标重新选择供奉序号（改 Offer.Rank）。</summary>
public sealed class MentalInterferenceEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.MentalInterference;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var v = ArcanaHelpers.RequireOtherPlayer(state, actor, target);
        if (!v.Ok) return v;
        var tid = target.PlayerIds[0];
        if (!state.Offers.ContainsKey(tid))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标尚未供奉");
        if (target.Rank is null)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定新序号");
        if (!GameRules.IsValidRank(target.Rank.Value, state.PlayerCount))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "无效序号");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var tid = target.PlayerIds[0];
        var offer = state.Offers[tid];
        var old = offer.Rank;
        offer.Rank = target.Rank!.Value;
        var actorP = state.FindPlayer(actor)!;
        var targetP = state.FindPlayer(tid)!;
        state.Log(
            "Arcana_MentalInterference",
            $"{actorP.Name} 使用精神干扰，将 {targetP.Name} 的序号 {old} → {offer.Rank}");
        return EffectApplicationResult.Done();
    }
}

/// <summary>巨力擒缚：从有宝石玩家手牌随机拿 1 宝石（R22）。</summary>
public sealed class MightyGraspEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.MightyGrasp;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        ArcanaHelpers.RequirePlayersWithGems(state, actor, target, 1, allowSelf: false);

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var targetP = state.FindPlayer(target.PlayerIds[0])!;
        var actorP = state.FindPlayer(actor)!;
        var gem = state.TakeRandomGem(targetP)!;
        actorP.GemHand.Add(gem);
        state.Log(
            "Arcana_MightyGrasp",
            $"{actorP.Name} 使用巨力擒缚，从 {targetP.Name} 手牌随机获得 1 张宝石");
        return EffectApplicationResult.Done();
    }
}

/// <summary>黑风术：全员顺时针交换供奉宝石（序号不变）。</summary>
public sealed class BlackWindEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.BlackWind;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        // Seat order 0..n-1 clockwise: each player's gems go to next seat.
        var ordered = state.Players.OrderBy(p => p.SeatIndex).ToList();
        var gemBags = ordered
            .Select(p =>
            {
                if (state.Offers.TryGetValue(p.Id, out var o))
                    return o.Gems.ToList();
                return new List<CardInstance>();
            })
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var from = (i - 1 + ordered.Count) % ordered.Count; // receive from previous seat
            var player = ordered[i];
            if (!state.Offers.TryGetValue(player.Id, out var offer))
            {
                // RULE-OPEN: player without offer — still create empty? skip receive if no offer
                if (gemBags[from].Count == 0)
                    continue;
                offer = new Offer { Rank = SequenceRank.I };
                state.Offers[player.Id] = offer;
            }

            offer.Gems.Clear();
            offer.Gems.AddRange(gemBags[from]);
        }

        var actorP = state.FindPlayer(actor)!;
        state.Log("Arcana_BlackWind", $"{actorP.Name} 使用黑风术，全员顺时针交换供奉宝石");
        return EffectApplicationResult.Done();
    }
}

/// <summary>人工选育：从堆拿 3 留 1（多步）。</summary>
public sealed class ArtificialBreedingEffect : IArcanaEffect
{
    public const string StepKeep = "Keep";

    public ArcanaKind Kind => ArcanaKind.ArtificialBreeding;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, 3);
        var actorP = state.FindPlayer(actor)!;
        if (drawn.Count == 0)
        {
            state.Log("Arcana_ArtificialBreeding", $"{actorP.Name} 使用人工选育，牌堆已空");
            return EffectApplicationResult.Done();
        }

        if (drawn.Count == 1)
        {
            actorP.GemHand.Add(drawn[0]);
            state.Log("Arcana_ArtificialBreeding", $"{actorP.Name} 使用人工选育，仅得 1 张并保留");
            return EffectApplicationResult.Done();
        }

        state.ActiveArcana = new ArcanaResolutionState
        {
            Kind = Kind,
            ActorId = actor,
            StepId = StepKeep,
            Prompt = "从抽到的宝石中选择 1 张保留，其余弃置",
        };
        state.ActiveArcana.TempCards.AddRange(drawn);
        state.Log("Arcana_ArtificialBreeding", $"{actorP.Name} 使用人工选育，抽取 {drawn.Count} 张待选");
        return EffectApplicationResult.NeedInput(StepKeep, state.ActiveArcana.Prompt!);
    }

    public EffectApplicationResult Continue(
        GameState state, PlayerId responder, string stepId, ArcanaTarget target)
    {
        var active = state.ActiveArcana;
        if (active is null || active.Kind != Kind)
            return EffectApplicationResult.Done();
        if (!responder.Equals(active.ActorId))
            return EffectApplicationResult.Fail(DomainErrorCodes.Unauthorized, "仅施术者可选择");
        if (!string.Equals(stepId, StepKeep, StringComparison.OrdinalIgnoreCase))
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "请选择保留的宝石");
        if (target.CardIds.Count != 1)
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "请选择恰好 1 张保留");

        var keepId = target.CardIds[0];
        var keep = active.TempCards.FirstOrDefault(c => c.Id.Equals(keepId));
        if (keep is null)
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "只能从抽到的牌中选择");

        var actorP = state.FindPlayer(active.ActorId)!;
        actorP.GemHand.Add(keep);
        foreach (var c in active.TempCards)
        {
            if (!c.Id.Equals(keepId))
                state.GemDiscard.Add(c);
        }

        active.TempCards.Clear();
        state.ActiveArcana = null;
        state.Log("Arcana_ArtificialBreeding_Keep", $"{actorP.Name} 人工选育保留 1 张宝石，其余弃置");
        return EffectApplicationResult.Done();
    }
}

/// <summary>通灵术：猜目标供奉序号；对则施术者抽 3 宝石，错则目标从宝石牌堆抽 1（R23）。</summary>
public sealed class SpiritismEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.Spiritism;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var v = ArcanaHelpers.RequireOtherPlayer(state, actor, target);
        if (!v.Ok) return v;
        if (target.Rank is null)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需猜测序号");
        if (!GameRules.IsValidRank(target.Rank.Value, state.PlayerCount))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "无效序号");
        if (!state.Offers.ContainsKey(target.PlayerIds[0]))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标尚未供奉");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var tid = target.PlayerIds[0];
        var actorP = state.FindPlayer(actor)!;
        var targetP = state.FindPlayer(tid)!;
        var actual = state.Offers[tid].Rank;
        var guess = target.Rank!.Value;

        if (actual == guess)
        {
            var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, 3);
            actorP.GemHand.AddRange(drawn);
            state.Log(
                "Arcana_Spiritism",
                $"{actorP.Name} 通灵术猜中 {targetP.Name} 的序号 {guess}，抽取 {drawn.Count} 张宝石");
        }
        else
        {
            var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, 1);
            targetP.GemHand.AddRange(drawn);
            state.Log(
                "Arcana_Spiritism",
                $"{actorP.Name} 通灵术猜错（猜 {guess}，实为 {actual}），{targetP.Name} 从宝石牌堆抽取 {drawn.Count} 张");
        }

        return EffectApplicationResult.Done();
    }
}

/// <summary>知识侵蚀：目标 VirtualForbiddenKnowledge += 1（R18）。</summary>
public sealed class KnowledgeErosionEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.KnowledgeErosion;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        ArcanaHelpers.RequireOtherPlayer(state, actor, target);

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var targetP = state.FindPlayer(target.PlayerIds[0])!;
        targetP.VirtualForbiddenKnowledge += 1;
        var actorP = state.FindPlayer(actor)!;
        state.Log(
            "Arcana_KnowledgeErosion",
            $"{actorP.Name} 使用知识侵蚀，{targetP.Name} 虚拟禁忌知识 = {targetP.VirtualForbiddenKnowledge}");
        return EffectApplicationResult.Done();
    }
}

/// <summary>恐惧共鸣：2 名有宝石玩家各随机丢 1 到手弃堆（R22）。</summary>
public sealed class FearResonanceEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.FearResonance;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        ArcanaHelpers.RequirePlayersWithGems(state, actor, target, 2, allowSelf: true);

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var names = new List<string>();
        foreach (var pid in target.PlayerIds)
        {
            var p = state.FindPlayer(pid)!;
            var gem = state.TakeRandomGem(p);
            if (gem is not null)
            {
                state.GemDiscard.Add(gem);
                names.Add(p.Name);
            }
        }

        var actorP = state.FindPlayer(actor)!;
        state.Log(
            "Arcana_FearResonance",
            $"{actorP.Name} 使用恐惧共鸣，{string.Join("、", names)} 各弃 1 张宝石");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 移植：从 2 名有宝石玩家各拿 1 随机，再从这些+自己手牌中分别选 1 张交还（共 2 张，多步）。
/// </summary>
public sealed class TransplantEffect : IArcanaEffect
{
    public const string StepReturn = "Return";

    public ArcanaKind Kind => ArcanaKind.Transplant;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        ArcanaHelpers.RequirePlayersWithGems(state, actor, target, 2, allowSelf: true);

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var actorP = state.FindPlayer(actor)!;
        state.ActiveArcana = new ArcanaResolutionState
        {
            Kind = Kind,
            ActorId = actor,
            StepId = StepReturn,
            Prompt = "分别选择 1 张宝石归还给两名目标（先选给目标1，再选给目标2）",
        };

        foreach (var pid in target.PlayerIds)
        {
            var p = state.FindPlayer(pid)!;
            var gem = state.TakeRandomGem(p)!;
            state.ActiveArcana.TempCards.Add(gem);
            state.ActiveArcana.TargetPlayerIds.Add(pid);
        }

        state.Log(
            "Arcana_Transplant",
            $"{actorP.Name} 使用移植，从两名玩家各取 1 张宝石，待归还");
        return EffectApplicationResult.NeedInput(StepReturn, state.ActiveArcana.Prompt!);
    }

    public EffectApplicationResult Continue(
        GameState state, PlayerId responder, string stepId, ArcanaTarget target)
    {
        var active = state.ActiveArcana;
        if (active is null || active.Kind != Kind)
            return EffectApplicationResult.Done();
        if (!responder.Equals(active.ActorId))
            return EffectApplicationResult.Fail(DomainErrorCodes.Unauthorized, "仅施术者可归还");
        if (!string.Equals(stepId, StepReturn, StringComparison.OrdinalIgnoreCase))
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "请归还宝石");
        if (target.CardIds.Count != 2)
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidTarget, "需分别选择 1 张宝石交还给两名目标（共 2 张）");
        if (target.CardIds[0].Equals(target.CardIds[1]))
            return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "两张归还宝石不可相同");

        var actorP = state.FindPlayer(active.ActorId)!;

        // Eligible pool: TempCards + actor gem hand
        var pool = new List<CardInstance>();
        pool.AddRange(active.TempCards);
        pool.AddRange(actorP.GemHand);

        var chosen = new List<CardInstance>();
        foreach (var id in target.CardIds)
        {
            var card = pool.FirstOrDefault(c => c.Id.Equals(id));
            if (card is null)
                return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "只能从取得的牌与自己手牌中选择");
            if (chosen.Any(c => c.Id.Equals(id)))
                return EffectApplicationResult.Fail(DomainErrorCodes.InvalidCards, "宝石不可重复");
            chosen.Add(card);
        }

        // Remove chosen from sources
        foreach (var c in chosen)
        {
            active.TempCards.Remove(c);
            actorP.GemHand.Remove(c);
        }

        // Leftover temp cards stay with actor
        actorP.GemHand.AddRange(active.TempCards);
        active.TempCards.Clear();

        // Return one gem to each target in order
        for (var i = 0; i < 2; i++)
        {
            var tid = active.TargetPlayerIds[i];
            var tp = state.FindPlayer(tid)!;
            tp.GemHand.Add(chosen[i]);
        }

        state.ActiveArcana = null;
        state.Log("Arcana_Transplant_Return", $"{actorP.Name} 完成移植归还");
        return EffectApplicationResult.Done();
    }
}

/// <summary>心灵暗示：任选 2 玩家交换供奉宝石。</summary>
public sealed class MindSuggestionEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.MindSuggestion;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        if (target.PlayerIds.Count != 2)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定 2 名玩家");
        if (target.PlayerIds[0].Equals(target.PlayerIds[1]))
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "两名目标不能相同");
        foreach (var pid in target.PlayerIds)
        {
            if (state.FindPlayer(pid) is null)
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标不在对局中");
            if (!state.Offers.ContainsKey(pid))
                return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "目标尚未供奉");
        }
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var a = state.Offers[target.PlayerIds[0]];
        var b = state.Offers[target.PlayerIds[1]];
        var tmp = a.Gems.ToList();
        a.Gems.Clear();
        a.Gems.AddRange(b.Gems);
        b.Gems.Clear();
        b.Gems.AddRange(tmp);

        var actorP = state.FindPlayer(actor)!;
        var n0 = state.FindPlayer(target.PlayerIds[0])!.Name;
        var n1 = state.FindPlayer(target.PlayerIds[1])!.Name;
        state.Log("Arcana_MindSuggestion", $"{actorP.Name} 使用心灵暗示，交换 {n0} 与 {n1} 的供奉宝石");
        return EffectApplicationResult.Done();
    }
}

/// <summary>狂信：与目标交换全部宝石手牌（R21）。</summary>
public sealed class FanaticismEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.Fanaticism;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        ArcanaHelpers.RequireOtherPlayer(state, actor, target);

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var actorP = state.FindPlayer(actor)!;
        var targetP = state.FindPlayer(target.PlayerIds[0])!;
        var aGems = actorP.GemHand.ToList();
        var tGems = targetP.GemHand.ToList();
        actorP.GemHand.Clear();
        targetP.GemHand.Clear();
        actorP.GemHand.AddRange(tGems);
        targetP.GemHand.AddRange(aGems);
        state.Log(
            "Arcana_Fanaticism",
            $"{actorP.Name} 使用狂信，与 {targetP.Name} 交换全部宝石手牌");
        return EffectApplicationResult.Done();
    }
}

/// <summary>Registry of all arcana effects (M1 + M2).</summary>
public static class ArcanaEffectRegistry
{
    private static readonly Dictionary<ArcanaKind, IArcanaEffect> Effects =
        new IArcanaEffect[]
        {
            new FishingNetEffect(),
            new OmniscientEffect(),
            new RlyehFogEffect(),
            new StaffOfForgettingEffect(),
            new FelReplenishEffect(),
            new ReverseThinkingEffect(),
            new EvilProphecyEffect(),
            new AlchemyEffect(),
            new PeekEffect(),
            new MaliciousSwapEffect(),
            new MentalInterferenceEffect(),
            new MightyGraspEffect(),
            new BlackWindEffect(),
            new ArtificialBreedingEffect(),
            new SpiritismEffect(),
            new KnowledgeErosionEffect(),
            new FearResonanceEffect(),
            new TransplantEffect(),
            new MindSuggestionEffect(),
            new FanaticismEffect(),
        }.ToDictionary(e => e.Kind);

    public static bool IsEnabled(ArcanaKind kind) => CardCatalog.EnabledArcana.Contains(kind);

    public static IArcanaEffect? Get(ArcanaKind kind) =>
        Effects.TryGetValue(kind, out var e) ? e : null;
}
