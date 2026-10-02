using Cthulu.Domain.Ids;

namespace Cthulu.Domain.Game;

/// <summary>Public, detached receipt captured before the altar and offers are cleared.</summary>
public sealed class PrepSettlement
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public int DayNumber { get; init; }
    public List<SettlementSlot> Slots { get; } = new();
    public HashSet<PlayerId> ConfirmedPlayers { get; } = new();
    public HashSet<PlayerId> SkippedPlayers { get; } = new();
}

public sealed class SettlementSlot
{
    public required string Rank { get; init; }
    public required string RelicName { get; init; }
    public bool Reverse { get; init; }
    public bool EvilProphecy { get; init; }
    public IReadOnlyList<SettlementOffer> Offers { get; init; } = Array.Empty<SettlementOffer>();
    public string Outcome { get; set; } = "";
}

public sealed record SettlementOffer(string PlayerName, int GemCount, int Sum, string Status);

/// <summary>Only deliberately public action text may be recorded here.</summary>
public sealed record PlayerAction(int DayNumber, GamePhase Phase, string Message, IReadOnlyList<string> Details);
