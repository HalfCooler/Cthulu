using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;

namespace Cthulu.Domain.Phases;

/// <summary>
/// Creative-mode sandbox ops: take cards from decks into hand/relics, or return them to decks.
/// Only valid when <see cref="GameState.IsCreative"/>.
/// </summary>
public static class CreativeModePipeline
{
    public static DomainResult AddGem(GameState state, PlayerId playerId, GemValue value)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        if (!state.GemDeck.TryTakeGem(value, out var card) || card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, $"宝石牌库中没有面值 {(int)value}");

        player.GemHand.Add(card);
        state.Log("CreativeAdd", $"{player.Name} 从牌库取了宝石 {(int)value}", playerId);
        return DomainResult.Success();
    }

    public static DomainResult RemoveGem(GameState state, PlayerId playerId, CardInstanceId cardId)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        var card = player.FindGem(cardId);
        if (card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有该宝石");

        player.GemHand.Remove(card);
        state.GemDeck.AddToTop(card);
        state.Log("CreativeRemove", $"{player.Name} 将宝石 {(int?)card.Def.GemValue} 归还牌库", playerId);
        return DomainResult.Success();
    }

    public static DomainResult AddArcana(GameState state, PlayerId playerId, ArcanaKind kind)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        if (!state.ArcanaDeck.TryTakeArcana(kind, out var card) || card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "秘术牌库中没有该秘术");

        player.ArcanaHand.Add(card);
        state.Log("CreativeAdd", $"{player.Name} 从牌库取了秘术", playerId);
        return DomainResult.Success();
    }

    public static DomainResult RemoveArcana(GameState state, PlayerId playerId, CardInstanceId cardId)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        var card = player.FindArcana(cardId);
        if (card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "手牌中没有该秘术");

        player.ArcanaHand.Remove(card);
        state.ArcanaDeck.AddToTop(card);
        state.Log("CreativeRemove", $"{player.Name} 将秘术归还牌库", playerId);
        return DomainResult.Success();
    }

    public static DomainResult AddRelic(GameState state, PlayerId playerId, RelicKind kind)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        if (!state.RelicDeck.TryTakeRelic(kind, out var card) || card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "祭品牌库中没有该祭品");

        player.Relics.Add(card);
        state.Log("CreativeAdd", $"{player.Name} 从牌库取了祭品", playerId);
        return DomainResult.Success();
    }

    public static DomainResult RemoveRelic(GameState state, PlayerId playerId, CardInstanceId cardId)
    {
        var guard = Guard(state, playerId, out var player);
        if (guard is not null)
            return guard;

        var card = player.Relics.FirstOrDefault(r => r.Id.Equals(cardId));
        if (card is null)
            return DomainResult.Fail(DomainErrorCodes.InvalidCards, "你没有该祭品");

        player.Relics.Remove(card);
        player.RelicsTradedSuccessfully.Remove(cardId);
        state.RelicDeck.AddToTop(card);
        state.Log("CreativeRemove", $"{player.Name} 将祭品归还牌库", playerId);
        return DomainResult.Success();
    }

    private static DomainResult? Guard(GameState state, PlayerId playerId, out PlayerState player)
    {
        player = null!;

        if (!state.IsCreative)
            return DomainResult.Fail(DomainErrorCodes.Unauthorized, "仅创造模式可自由取还牌");

        if (state.Phase is GamePhase.Finished or GamePhase.FinalScoring)
            return DomainResult.Fail(DomainErrorCodes.InvalidPhase, "对局已结束");

        var found = state.FindPlayer(playerId);
        if (found is null)
            return DomainResult.Fail(DomainErrorCodes.NotInRoom, "玩家不在对局中");

        if (found.IsBot)
            return DomainResult.Fail(DomainErrorCodes.Unauthorized, "人机不可操作创造模式取还牌");

        player = found;
        return null;
    }
}
