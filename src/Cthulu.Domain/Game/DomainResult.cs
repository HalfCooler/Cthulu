namespace Cthulu.Domain.Game;

/// <summary>Domain-layer command result (mirrors Application error codes as strings).</summary>
public sealed class DomainResult
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public static DomainResult Success() => new() { Ok = true };

    public static DomainResult Fail(string errorCode, string message) =>
        new() { Ok = false, ErrorCode = errorCode, Message = message };
}

/// <summary>Stable error codes (ENGINEERING §14) — shared string constants for Domain.</summary>
public static class DomainErrorCodes
{
    public const string NotInRoom = "NotInRoom";
    public const string NotYourTurn = "NotYourTurn";
    public const string InvalidPhase = "InvalidPhase";
    public const string InvalidCards = "InvalidCards";
    public const string InvalidTarget = "InvalidTarget";
    public const string OfferIncomplete = "OfferIncomplete";
    public const string ArcanaNotEnabled = "ArcanaNotEnabled";
    public const string EffectInvalid = "EffectInvalid";
    public const string Unauthorized = "Unauthorized";
}
