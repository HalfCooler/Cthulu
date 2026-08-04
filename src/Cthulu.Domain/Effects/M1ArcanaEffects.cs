using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Phases;

namespace Cthulu.Domain.Effects;

/// <summary>
/// 渔网：抽 2 宝石。
/// </summary>
public sealed class FishingNetEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.FishingNet;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var player = state.FindPlayer(actor)!;
        var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, 2);
        player.GemHand.AddRange(drawn);
        state.Log("Arcana_FishingNet", $"{player.Name} 使用渔网，抽取 {drawn.Count} 张宝石");
        state.Log("Private", $"你抽到了宝石 {string.Join("、", drawn.Select(c => c.Def.FaceValue))}。", player.Id);
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 全知者：宝石少于 5 的玩家各抽 1 宝石。
/// </summary>
public sealed class OmniscientEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.Omniscient;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var names = new List<string>();
        foreach (var p in state.Players)
        {
            if (p.GemHand.Count >= GameRules.OmniscientGemThreshold) continue;
            
            var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, 1);
            p.GemHand.AddRange(drawn);
            if (drawn.Count > 0)
                names.Add(p.Name);
            state.Log("Private", $"你抽到了宝石 {string.Join("、", drawn.Select(c => c.Def.FaceValue))}。", p.Id);
        }

        var actorName = state.FindPlayer(actor)!.Name;
        state.Log("Arcana_Omniscient", names.Count == 0 ? $"{actorName} 使用全知者，无人补牌" : $"{actorName} 使用全知者，{string.Join("、", names)} 各抽 1 宝石");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 拉莱耶迷雾：指定 2 个祭坛槽互换祭品。
/// </summary>
public sealed class RlyehFogEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.RlyehFog;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        if (target.SlotIndices.Count != 2)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定 2 个祭坛槽位互换");
        var a = target.SlotIndices[0];
        var b = target.SlotIndices[1];
        if (a == b)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "两个槽位不能相同");
        if (a < 0 || b < 0 || a >= state.Altar.Count || b >= state.Altar.Count)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "祭坛槽位越界");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var a = target.SlotIndices[0];
        var b = target.SlotIndices[1];
        (state.Altar[a], state.Altar[b]) = (state.Altar[b], state.Altar[a]);
        var ma = state.AltarModifiers.Get(a);
        var mb = state.AltarModifiers.Get(b);
        var tmpRev = ma.ReverseThinking;
        var tmpEvil = ma.EvilProphecy;
        ma.ReverseThinking = mb.ReverseThinking;
        ma.EvilProphecy = mb.EvilProphecy;
        mb.ReverseThinking = tmpRev;
        mb.EvilProphecy = tmpEvil;

        var name = state.FindPlayer(actor)!.Name;
        state.Log("Arcana_RlyehFog", $"{name} 使用拉莱耶迷雾，互换祭坛槽 {a + 1} 与 {b + 1}");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 遗忘权杖：弃置若干个祭坛上的祭品并从牌堆中抽取新祭品。
/// </summary>
public sealed class StaffOfForgettingEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.StaffOfForgetting;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        if (target.SlotIndices.Count < 1)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "至少指定 1 个祭坛槽位");
        if (target.SlotIndices.Distinct().Count() != target.SlotIndices.Count)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "槽位不可重复");
        return target.SlotIndices.Any(s => s < 0 || s >= state.Altar.Count) ? EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "祭坛槽位越界") : EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        foreach (var slot in target.SlotIndices.Distinct().OrderByDescending(x => x))
        {
            var old = state.Altar[slot];
            state.RelicDiscard.Add(old);
            var drawn = DrawPileService.Draw(state.RelicDeck, state.RelicDiscard, 1);
            if (drawn.Count == 1)
                state.Altar[slot] = drawn[0];
            else
            {
                state.RelicDiscard.Remove(old);
                state.Altar[slot] = old;
            }
        }

        var name = state.FindPlayer(actor)!.Name;
        var slots = string.Join("、", target.SlotIndices.Select(s => (s + 1).ToString()));
        state.Log("Arcana_StaffOfForgetting", $"{name} 使用遗忘权杖，刷新祭坛槽 {slots}");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 邪能补充：秘术少于 2 的玩家各摸 1 秘术，不计入打出次数。
/// </summary>
public sealed class FelReplenishEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.FelReplenish;
    public bool DoesNotConsumeAction => true;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) =>
        EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var names = new List<string>();
        foreach (var p in state.Players)
        {
            var count = p.ArcanaHand.Count;
            if (p.Id.Equals(actor))
                count -= 1;
            if (count >= GameRules.ArcanaHandSoftCap) continue;
            
            var drawn = DrawPileService.Draw(state.ArcanaDeck, state.ArcanaDiscard, 1);
            p.ArcanaHand.AddRange(drawn);
            if (drawn.Count > 0)
                names.Add(p.Name);
            
            state.Log("Private", $"你摸到了 {string.Join("、", drawn.Select(c => c.Def.ArcanaKind is { } k ? PrepDayPipeline.ArcanaDisplayName(k) : c.Def.DisplayKey))}", p.Id);
        }

        var actorName = state.FindPlayer(actor)!.Name;
        state.Log("Arcana_FelReplenish", names.Count == 0 ? $"{actorName} 使用邪能补充，无人补牌" : $"{actorName} 使用邪能补充，{string.Join("、", names)} 各摸 1 秘术");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 逆向思维：指定 1 祭坛槽加 Reverse 标记。
/// </summary>
public sealed class ReverseThinkingEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.ReverseThinking;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        if (target.SlotIndices.Count != 1)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定 1 个祭坛槽位");
        var s = target.SlotIndices[0];
        if (s < 0 || s >= state.Altar.Count)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "祭坛槽位越界");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var s = target.SlotIndices[0];
        state.AltarModifiers.Get(s).ReverseThinking = true;
        var name = state.FindPlayer(actor)!.Name;
        state.Log("Arcana_ReverseThinking", $"{name} 使用逆向思维，祭坛槽 {s + 1} 改为最低者得");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 邪恶预言：指定 1 祭坛槽加 sum &lt; 4 无效标记。
/// </summary>
public sealed class EvilProphecyEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.EvilProphecy;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target)
    {
        if (target.SlotIndices.Count != 1)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "需指定 1 个祭坛槽位");
        var s = target.SlotIndices[0];
        if (s < 0 || s >= state.Altar.Count)
            return EffectValidation.Fail(DomainErrorCodes.InvalidTarget, "祭坛槽位越界");
        return EffectValidation.Success();
    }

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var s = target.SlotIndices[0];
        state.AltarModifiers.Get(s).EvilProphecy = true;
        var name = state.FindPlayer(actor)!.Name;
        state.Log("Arcana_EvilProphecy", $"{name} 使用邪恶预言，祭坛槽 {s + 1} 上 sum < 4 的供奉无效");
        return EffectApplicationResult.Done();
    }
}

/// <summary>
/// 点金术：弃全部宝石手牌，抽等量。
/// </summary>
public sealed class AlchemyEffect : IArcanaEffect
{
    public ArcanaKind Kind => ArcanaKind.Alchemy;

    public EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target) => EffectValidation.Success();

    public EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target)
    {
        var player = state.FindPlayer(actor)!;
        var n = player.GemHand.Count;
        var sumOld = player.GemHand.Sum(gem => gem.Def.FaceValue);
        if (n > 0)
        {
            state.GemDiscard.AddRange(player.GemHand);
            player.GemHand.Clear();
        }

        var drawn = DrawPileService.Draw(state.GemDeck, state.GemDiscard, n);
        var sumNew = drawn.Sum(gem => gem.Def.FaceValue);
        var delta = sumNew - sumOld;
        player.GemHand.AddRange(drawn);
        state.Log("Arcana_Alchemy", $"{player.Name} 使用点金术，弃 {n} 抽 {drawn.Count} 张宝石");
        var message = delta == 0 ? "不亏也不赚" : delta > 0 ? $"赚了 {delta}" : $"亏了 {-delta}";
        state.Log("Private", $"你之前宝石总和为 {sumOld}，现在为 {sumNew}。{message}。", actor);
        return EffectApplicationResult.Done();
    }
}
