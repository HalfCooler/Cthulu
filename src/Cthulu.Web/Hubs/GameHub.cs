using Cthulu.Application.Commands;
using Cthulu.Application.Rooms;
using Cthulu.Application.Views;
using Microsoft.AspNetCore.SignalR;

namespace Cthulu.Web.Hubs;

/// <summary>
/// Game room hub. After every state-changing command, pushes a per-player RoomView
/// via Clients.Client(connectionId) — never a shared Group payload with private data.
/// </summary>
public sealed class GameHub(RoomService rooms, ILogger<GameHub> logger) : Hub
{
    public async Task<CommandResult> CreateRoom(string hostName)
    {
        var result = rooms.CreateRoom(hostName, Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> JoinRoom(string roomCode, string playerName)
    {
        var result = rooms.JoinRoom(roomCode, playerName, Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> StartGame(int? debugSeed = null)
    {
        var result = rooms.StartGame(Context.ConnectionId, debugSeed);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> SubmitOffer(string rank, string[] gemInstanceIds)
    {
        var result = rooms.SubmitOffer(Context.ConnectionId, rank, gemInstanceIds);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> ClearOffer()
    {
        var result = rooms.ClearOffer(Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> PassArcana()
    {
        var result = rooms.PassArcana(Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> PlayArcana(
        string arcanaKind,
        int[]? targetSlotIndices,
        string[]? targetPlayerIds,
        string[]? targetCardIds,
        string? rank)
    {
        var result = rooms.PlayArcana(
            Context.ConnectionId,
            arcanaKind,
            targetSlotIndices,
            targetPlayerIds,
            targetCardIds,
            rank);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> ArcanaStepResponse(
        string stepId,
        int[]? targetSlotIndices,
        string[]? targetPlayerIds,
        string[]? targetCardIds,
        string? rank)
    {
        var result = rooms.ArcanaStepResponse(
            Context.ConnectionId,
            stepId,
            targetSlotIndices,
            targetPlayerIds,
            targetCardIds,
            rank);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> PassTrade()
    {
        var result = rooms.PassTrade(Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> ProposeTrade(
        string sellerId,
        string relicInstanceId,
        string[] buyerGemIds)
    {
        var result = rooms.ProposeTrade(
            Context.ConnectionId,
            sellerId,
            relicInstanceId,
            buyerGemIds);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> RespondTrade(bool accept)
    {
        var result = rooms.RespondTrade(Context.ConnectionId, accept);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> CancelTrade()
    {
        var result = rooms.CancelTrade(Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> BeginForceBuy(string[]? newBuyerGemIds)
    {
        var result = rooms.BeginForceBuy(Context.ConnectionId, newBuyerGemIds);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> CommitForceBuyBid(string[] gemIds)
    {
        var result = rooms.CommitForceBuyBid(Context.ConnectionId, gemIds);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> CastVote(bool yes)
    {
        var result = rooms.CastVote(Context.ConnectionId, yes);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> RaiseAuction(string[] gemInstanceIds)
    {
        var result = rooms.RaiseAuction(
            Context.ConnectionId, gemInstanceIds);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> PassAuction()
    {
        var result = rooms.PassAuction(Context.ConnectionId);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public async Task<CommandResult> SubmitAllInOffer(string rank)
    {
        var result = rooms.SubmitAllInOffer(Context.ConnectionId, rank);
        if (result.Ok)
            await PushRoomToAllAsync(Context.ConnectionId);
        return result;
    }

    public Task<RoomView?> Sync()
    {
        var (result, view) = rooms.Sync(Context.ConnectionId);
        if (!result.Ok)
            logger.LogDebug("Sync failed: {Code} {Message}", result.ErrorCode, result.Message);
        return Task.FromResult(view);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var room = rooms.GetRoomForConnection(Context.ConnectionId);
        rooms.HandleDisconnect(Context.ConnectionId);

        if (room is not null)
            await PushRoomViewsAsync(room);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PushRoomToAllAsync(string connectionId)
    {
        var room = rooms.GetRoomForConnection(connectionId);
        if (room is null)
            return;
        await PushRoomViewsAsync(room);
    }

    private async Task PushRoomViewsAsync(GameRoom room)
    {
        var payloads = rooms.BuildViewsForPush(room);
        foreach (var (connectionId, view) in payloads)
        {
            await Clients.Client(connectionId).SendAsync("RoomUpdated", view);
        }
    }
}
