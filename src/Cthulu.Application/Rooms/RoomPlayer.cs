using Cthulu.Domain.Ids;

namespace Cthulu.Application.Rooms;

/// <summary>Seat occupant in a room (lobby + early game). Full PlayerState lands in M1 Domain.</summary>
public sealed class RoomPlayer
{
    public required PlayerId Id { get; init; }
    public required string Name { get; init; }
    public int SeatIndex { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsHost { get; set; }

    /// <summary>AI seat filler (creative mode). Never has a real ConnectionId.</summary>
    public bool IsBot { get; set; }

    public bool IsConnected => IsBot || !string.IsNullOrEmpty(ConnectionId);
}
