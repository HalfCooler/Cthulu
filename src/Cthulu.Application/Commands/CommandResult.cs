namespace Cthulu.Application.Commands;

/// <summary>Result of a room/game command. Serializable over SignalR.</summary>
public sealed class CommandResult
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; private init; }
    public string? Message { get; init; }

    /// <summary>Optional string bag (roomCode, playerId, …).</summary>
    public Dictionary<string, string>? Data { get; init; }

    public static CommandResult Success(Dictionary<string, string>? data = null) =>
        new() { Ok = true, Data = data };

    public static CommandResult Fail(string errorCode, string message) =>
        new() { Ok = false, ErrorCode = errorCode, Message = message };
}

/// <summary>Stable error codes (ENGINEERING §14).</summary>
public static class ErrorCodes
{
    public const string RoomNotFound = "RoomNotFound";
    public const string RoomFull = "RoomFull";
    public const string GameAlreadyStarted = "GameAlreadyStarted";
    public const string NotInRoom = "NotInRoom";
    public const string NotYourTurn = "NotYourTurn";
    public const string InvalidPhase = "InvalidPhase";
    public const string InvalidCards = "InvalidCards";
    public const string InvalidTarget = "InvalidTarget";
    public const string OfferIncomplete = "OfferIncomplete";
    public const string ArcanaNotEnabled = "ArcanaNotEnabled";
    public const string EffectInvalid = "EffectInvalid";
    public const string NotReady = "NotReady";
    public const string Unauthorized = "Unauthorized";
    public const string InvalidName = "InvalidName";
    public const string NotEnoughPlayers = "NotEnoughPlayers";
}
