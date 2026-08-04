using Cthulu.Application.Commands;
using Cthulu.Application.Views;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cthulu.Web.Services;

/// <summary>
/// Scoped Blazor service: owns HubConnection, latest RoomView, and UI change notifications.
/// </summary>
public sealed class GameClientService(NavigationManager nav, ILogger<GameClientService> logger) : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public RoomView? CurrentView { get; private set; }
    private string? RoomCode { get; set; }
    private string? DisplayName { get; set; }

    /// <summary>Latest command failure text (ErrorCode + Message). Not cleared by RoomUpdated.</summary>
    public string? LastError { get; private set; }

    /// <summary>Monotonic toast id so UI can re-show even if text repeats.</summary>
    public int ToastSeq { get; private set; }

    public event Action? OnChange;

    public bool IsConnected =>
        _hub?.State == HubConnectionState.Connected;

    public async Task EnsureConnectedAsync()
    {
        await _connectLock.WaitAsync();
        try
        {
            switch (_hub)
            {
                case { State: HubConnectionState.Connected }:
                    return;
                case null:
                    _hub = new HubConnectionBuilder()
                        .WithUrl(nav.ToAbsoluteUri("/hubs/game"))
                        .WithAutomaticReconnect()
                        .Build();

                    _hub.On<RoomView>("RoomUpdated", view =>
                    {
                        CurrentView = view;
                        RoomCode = view.RoomCode;
                        DisplayName = view.SelfName;
                        // Do NOT clear LastError here — illegal ops must remain visible as Toast.
                        NotifyStateChanged();
                    });

                    _hub.Reconnected += async _ =>
                    {
                        try
                        {
                            // Same room code + nickname reclaims the seat and rebinds ConnectionId.
                            if (!string.IsNullOrEmpty(RoomCode) && !string.IsNullOrEmpty(DisplayName))
                            {
                                var rejoin = await _hub.InvokeAsync<CommandResult>(
                                    "JoinRoom", RoomCode, DisplayName);
                                if (!rejoin.Ok)
                                {
                                    logger.LogWarning(
                                        "Rejoin after reconnect failed: {Code} {Msg}",
                                        rejoin.ErrorCode, rejoin.Message);
                                    SetError(rejoin);
                                }
                            }
                            
                            await SyncAsync();
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Re-bind after reconnect failed");
                            SetErrorRaw(null, $"重连失败：{ex.Message}");
                        }
                    };
                    break;
            }

            if (_hub.State == HubConnectionState.Disconnected)
                await _hub.StartAsync();
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public void DismissError()
    {
        LastError = null;
        NotifyStateChanged();
    }

    private void SetError(CommandResult result)
    {
        var msg = result.Message ?? "操作失败";
        LastError = string.IsNullOrEmpty(result.ErrorCode)
            ? msg
            : $"[{result.ErrorCode}] {msg}";
        ToastSeq++;
    }

    private void SetErrorRaw(string? code, string message)
    {
        LastError = string.IsNullOrEmpty(code) ? message : $"[{code}] {message}";
        ToastSeq++;
    }

    private CommandResult Track(CommandResult result)
    {
        if (!result.Ok)
            SetError(result);
        NotifyStateChanged();
        return result;
    }

    public async Task<CommandResult> CreateRoomAsync(string hostName)
    {
        await EnsureConnectedAsync();
        DisplayName = hostName.Trim();
        var result = await _hub!.InvokeAsync<CommandResult>("CreateRoom", hostName);
        if (result is { Ok: true, Data: not null })
        {
            RoomCode = result.Data.GetValueOrDefault("roomCode");
            result.Data.GetValueOrDefault("playerId");
            DismissError();
        }
        else
        {
            SetError(result);
        }

        NotifyStateChanged();
        return result;
    }

    public async Task<CommandResult> JoinRoomAsync(string roomCode, string playerName)
    {
        await EnsureConnectedAsync();
        DisplayName = playerName.Trim();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "JoinRoom",
            roomCode.Trim().ToUpperInvariant(),
            playerName);
        if (result is { Ok: true, Data: not null })
        {
            RoomCode = result.Data.GetValueOrDefault("roomCode");
            result.Data.GetValueOrDefault("playerId");
            DismissError();
        }
        else
        {
            SetError(result);
        }

        NotifyStateChanged();
        return result;
    }

    public async Task<CommandResult> StartGameAsync(int? debugSeed = null)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("StartGame", debugSeed);
        return Track(result);
    }

    public async Task<CommandResult> StartCreativeModeAsync(int? debugSeed = null)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("StartCreativeMode", debugSeed);
        return Track(result);
    }

    public async Task<CommandResult> CreativeAddGemAsync(string gemValue)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeAddGem", gemValue);
        return Track(result);
    }

    public async Task<CommandResult> CreativeRemoveGemAsync(string instanceId)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeRemoveGem", instanceId);
        return Track(result);
    }

    public async Task<CommandResult> CreativeAddArcanaAsync(string arcanaKind)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeAddArcana", arcanaKind);
        return Track(result);
    }

    public async Task<CommandResult> CreativeRemoveArcanaAsync(string instanceId)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeRemoveArcana", instanceId);
        return Track(result);
    }

    public async Task<CommandResult> CreativeAddRelicAsync(string relicKind)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeAddRelic", relicKind);
        return Track(result);
    }

    public async Task<CommandResult> CreativeRemoveRelicAsync(string instanceId)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CreativeRemoveRelic", instanceId);
        return Track(result);
    }

    public async Task<CommandResult> SubmitOfferAsync(string rank, IReadOnlyList<string> gemInstanceIds)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "SubmitOffer", rank, gemInstanceIds.ToArray());
        return Track(result);
    }

    public async Task<CommandResult> ClearOfferAsync()
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("ClearOffer");
        return Track(result);
    }

    public async Task<CommandResult> PassArcanaAsync()
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("PassArcana");
        return Track(result);
    }

    public async Task<CommandResult> PlayArcanaAsync(
        string arcanaKind,
        int[]? targetSlotIndices = null,
        string[]? targetPlayerIds = null,
        string[]? targetCardIds = null,
        string? rank = null)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "PlayArcana",
            arcanaKind,
            targetSlotIndices,
            targetPlayerIds,
            targetCardIds,
            rank);
        return Track(result);
    }

    public async Task<CommandResult> ArcanaStepResponseAsync(
        string stepId,
        int[]? targetSlotIndices = null,
        string[]? targetPlayerIds = null,
        string[]? targetCardIds = null,
        string? rank = null)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "ArcanaStepResponse",
            stepId,
            targetSlotIndices,
            targetPlayerIds,
            targetCardIds,
            rank);
        return Track(result);
    }

    public async Task<CommandResult> PassTradeAsync()
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("PassTrade");
        return Track(result);
    }

    public async Task<CommandResult> ProposeTradeAsync(
        string sellerId, string relicInstanceId, IReadOnlyList<string> buyerGemIds)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "ProposeTrade", sellerId, relicInstanceId, buyerGemIds.ToArray());
        return Track(result);
    }

    public async Task<CommandResult> RespondTradeAsync(bool accept)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("RespondTrade", accept);
        return Track(result);
    }

    public async Task<CommandResult> CancelTradeAsync()
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CancelTrade");
        return Track(result);
    }

    public async Task<CommandResult> BeginForceBuyAsync(IReadOnlyList<string>? newBuyerGemIds = null)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "BeginForceBuy", newBuyerGemIds?.ToArray());
        return Track(result);
    }

    public async Task<CommandResult> CommitForceBuyBidAsync(IReadOnlyList<string> gemIds)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "CommitForceBuyBid", gemIds.ToArray());
        return Track(result);
    }

    public async Task<CommandResult> CastVoteAsync(bool yes)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("CastVote", yes);
        return Track(result);
    }

    public async Task<CommandResult> RaiseAuctionAsync(IReadOnlyList<string> gemInstanceIds)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>(
            "RaiseAuction", gemInstanceIds.ToArray());
        return Track(result);
    }

    public async Task<CommandResult> PassAuctionAsync()
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("PassAuction");
        return Track(result);
    }

    public async Task<CommandResult> SubmitAllInOfferAsync(string rank)
    {
        await EnsureConnectedAsync();
        var result = await _hub!.InvokeAsync<CommandResult>("SubmitAllInOffer", rank);
        return Track(result);
    }

    public async Task SyncAsync()
    {
        await EnsureConnectedAsync();
        var view = await _hub!.InvokeAsync<RoomView?>("Sync");
        if (view is not null)
        {
            CurrentView = view;
            RoomCode = view.RoomCode;
            DisplayName = view.SelfName;
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            try
            {
                await _hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Hub dispose");
            }
        }

        _connectLock.Dispose();
    }
}
