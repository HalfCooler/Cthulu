namespace Cthulu.Application;

/// <summary>
/// Runtime game configuration (bound from appsettings "Game" section).
/// MaxPlayers is clamped to Domain <c>GameRules</c> hard limits at use sites.
/// </summary>
public sealed class GameOptions
{
    public const string SectionName = "Game";

    /// <summary>Minimum players to start (default 4; cannot go below domain hard min).</summary>
    public int MinPlayers { get; set; } = 4;

    /// <summary>Maximum seats per room (default 6; cannot exceed domain hard max).</summary>
    public int MaxPlayers { get; set; } = 6;

    /// <summary>
    /// Enabled arcana kinds (enum names, e.g. "FishingNet").
    /// Empty / null → all kinds enabled (M2 default).
    /// </summary>
    public List<string> EnabledArcana { get; set; } = new();

    /// <summary>How many recent log lines to project to clients.</summary>
    public int EventLogDisplayLimit { get; set; } = 80;

    /// <summary>Hard cap of in-memory domain event log entries per game.</summary>
    public int EventLogStoreLimit { get; set; } = 200;

    /// <summary>
    /// When true, host may pass a fixed RNG seed on StartGame (Development debug).
    /// </summary>
    public bool AllowDebugSeed { get; set; }
}
