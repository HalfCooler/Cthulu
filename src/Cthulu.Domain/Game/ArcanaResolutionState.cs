using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>In-progress multi-step arcana resolution (ENGINEERING §11.1).</summary>
public sealed class ArcanaResolutionState
{
    public required ArcanaKind Kind { get; init; }
    public required PlayerId ActorId { get; init; }
    public required string StepId { get; set; }
    public string? Prompt { get; set; }

    /// <summary>Players involved as targets (order matters for Transplant / FearResonance).</summary>
    public List<PlayerId> TargetPlayerIds { get; } = new();

    /// <summary>Temporary cards (drawn for ArtificialBreeding, taken for Transplant, etc.).</summary>
    public List<CardInstance> TempCards { get; } = new();

    /// <summary>Peek: whether actor / target have finished re-offer step.</summary>
    public bool ActorReoffered { get; set; }
    public bool TargetReoffered { get; set; }

    public SequenceRank? GuessedRank { get; set; }

    /// <summary>Arbitrary payload (e.g. slot index as string).</summary>
    public Dictionary<string, string> Data { get; } = new(StringComparer.Ordinal);
}
