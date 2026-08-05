namespace Cthulu.Application.Views;

/// <summary>
/// Public lobby listing entry (no private player data).
/// </summary>
public sealed class RoomListItem
{
    public required string RoomCode { get; init; }
    public required string HostName { get; init; }
    public int PlayerCount { get; init; }
    public int MaxPlayers { get; init; }
    public bool IsJoinable { get; init; }
}
