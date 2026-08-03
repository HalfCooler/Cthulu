using Cthulu.Domain.Cards;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Effects;

/// <summary>Target / step payload for arcana (slots, players, cards, rank guess…).</summary>
public sealed class ArcanaTarget
{
    /// <summary>0-based altar slot indices (order matters for swap).</summary>
    public IReadOnlyList<int> SlotIndices { get; init; } = Array.Empty<int>();

    /// <summary>Target player IDs (order matters for multi-target effects).</summary>
    public IReadOnlyList<PlayerId> PlayerIds { get; init; } = Array.Empty<PlayerId>();

    /// <summary>Card instance IDs (relics, keep/return gems, etc.).</summary>
    public IReadOnlyList<CardInstanceId> CardIds { get; init; } = Array.Empty<CardInstanceId>();

    /// <summary>Spiritism rank guess / MentalInterference new rank / Peek re-offer rank.</summary>
    public SequenceRank? Rank { get; init; }
}

public sealed class EffectValidation
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static EffectValidation Success() => new() { Ok = true };

    public static EffectValidation Fail(string code, string message) =>
        new() { Ok = false, ErrorCode = code, Message = message };
}

/// <summary>Result of Apply / Continue — may request further input (multi-step).</summary>
public sealed class EffectApplicationResult
{
    public bool Completed { get; init; }
    public bool NeedMoreInput { get; init; }
    public bool Failed { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public string? StepId { get; init; }
    public string? Prompt { get; init; }

    public static EffectApplicationResult Done() =>
        new() { Completed = true };

    public static EffectApplicationResult NeedInput(string stepId, string prompt) =>
        new() { NeedMoreInput = true, StepId = stepId, Prompt = prompt };

    public static EffectApplicationResult Fail(string code, string message) =>
        new() { Failed = true, ErrorCode = code, Message = message };
}

public interface IArcanaEffect
{
    ArcanaKind Kind { get; }
    bool DoesNotConsumeAction => false;

    EffectValidation Validate(GameState state, PlayerId actor, ArcanaTarget target);

    /// <summary>Initial apply. May set <see cref="GameState.ActiveArcana"/> and return NeedMoreInput.</summary>
    EffectApplicationResult Apply(GameState state, PlayerId actor, ArcanaTarget target);

    /// <summary>Continue multi-step. Default: not supported.</summary>
    EffectApplicationResult Continue(
        GameState state,
        PlayerId responder,
        string stepId,
        ArcanaTarget target) =>
        EffectApplicationResult.Done();
}
