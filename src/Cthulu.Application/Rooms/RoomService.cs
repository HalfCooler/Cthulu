using System.Security.Cryptography;
using System.Text;
using Cthulu.Application.Commands;
using Cthulu.Application.Views;
using Cthulu.Domain.Cards;
using Cthulu.Domain.Effects;
using Cthulu.Domain.Game;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Phases;
using Cthulu.Domain.Random;
using Microsoft.Extensions.Options;

namespace Cthulu.Application.Rooms;

/// <summary>
/// Application service for room lifecycle and game commands.
/// Thread-safe per room via GameRoom.SyncRoot.
/// </summary>
public sealed class RoomService
{
    private readonly IGameRoomStore _store;
    private readonly ViewProjector _projector;
    private readonly GameOptions _options;

    private static readonly char[] CodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public RoomService(
        IGameRoomStore store,
        ViewProjector projector,
        IOptions<GameOptions> options)
    {
        _store = store;
        _projector = projector;
        _options = options.Value;
    }

    public int ConfiguredMinPlayers => ClampMinPlayers(_options.MinPlayers);
    public int ConfiguredMaxPlayers => ClampMaxPlayers(_options.MaxPlayers);
    public bool AllowDebugSeed => _options.AllowDebugSeed;

    public CommandResult CreateRoom(string hostName, string connectionId)
    {
        var name = NormalizeName(hostName);
        if (name is null)
            return CommandResult.Fail(ErrorCodes.InvalidName, "昵称不能为空（1–16 字）");

        var host = new RoomPlayer
        {
            Id = PlayerId.New(),
            Name = name,
            ConnectionId = connectionId,
            IsHost = true,
            SeatIndex = 0,
        };

        var room = new GameRoom(RoomId.New(), GenerateUniqueCode(), host);
        _store.Add(room);
        _store.BindConnection(connectionId, room.Id, host.Id);

        return CommandResult.Success(new Dictionary<string, string>
        {
            ["roomCode"] = room.Code,
            ["playerId"] = host.Id.ToString(),
            ["roomId"] = room.Id.ToString(),
        });
    }

    public CommandResult JoinRoom(string roomCode, string playerName, string connectionId)
    {
        var name = NormalizeName(playerName);
        if (name is null)
            return CommandResult.Fail(ErrorCodes.InvalidName, "昵称不能为空（1–16 字）");

        var room = _store.GetByCode(roomCode ?? string.Empty);
        if (room is null)
            return CommandResult.Fail(ErrorCodes.RoomNotFound, "房间不存在");

        lock (room.SyncRoot)
        {
            var existing = room.FindByName(name);
            if (existing is not null)
            {
                if (existing.IsConnected &&
                    !string.Equals(existing.ConnectionId, connectionId, StringComparison.Ordinal))
                {
                    return CommandResult.Fail(ErrorCodes.Unauthorized, "该昵称已在房间中且在线");
                }

                if (!string.IsNullOrEmpty(existing.ConnectionId))
                    _store.UnbindConnection(existing.ConnectionId);

                existing.ConnectionId = connectionId;
                _store.BindConnection(connectionId, room.Id, existing.Id);

                // Restore in-game seat connectivity (same PlayerId / seat).
                if (room.Game is not null)
                {
                    var gs = room.Game.FindPlayer(existing.Id);
                    if (gs is not null)
                        gs.IsConnected = true;
                }

                return CommandResult.Success(new Dictionary<string, string>
                {
                    ["roomCode"] = room.Code,
                    ["playerId"] = existing.Id.ToString(),
                    ["roomId"] = room.Id.ToString(),
                    ["rejoined"] = "true",
                });
            }

            if (room.Phase != GamePhase.Lobby)
                return CommandResult.Fail(ErrorCodes.GameAlreadyStarted, "对局已开始，无法加入");

            var maxPlayers = ConfiguredMaxPlayers;
            if (room.Players.Count >= maxPlayers)
                return CommandResult.Fail(ErrorCodes.RoomFull, $"房间已满（最多 {maxPlayers} 人）");

            var player = new RoomPlayer
            {
                Id = PlayerId.New(),
                Name = name,
                ConnectionId = connectionId,
                SeatIndex = room.Players.Count,
                IsHost = false,
            };
            room.Players.Add(player);
            _store.BindConnection(connectionId, room.Id, player.Id);

            return CommandResult.Success(new Dictionary<string, string>
            {
                ["roomCode"] = room.Code,
                ["playerId"] = player.Id.ToString(),
                ["roomId"] = room.Id.ToString(),
            });
        }
    }

    public CommandResult StartGame(string connectionId, int? debugSeed = null)
    {
        return WithPlayer(connectionId, (room, player) =>
        {
            if (room.Phase != GamePhase.Lobby || room.Game is not null)
                return CommandResult.Fail(ErrorCodes.InvalidPhase, "当前阶段无法开始游戏");

            var minPlayers = ConfiguredMinPlayers;
            if (room.Players.Count < minPlayers)
                return CommandResult.Fail(
                    ErrorCodes.NotEnoughPlayers,
                    $"至少需要 {minPlayers} 名玩家");

            if (room.Players.Count > ConfiguredMaxPlayers)
                return CommandResult.Fail(
                    ErrorCodes.RoomFull,
                    $"人数超过上限 {ConfiguredMaxPlayers}");

            if (!player.IsHost)
                return CommandResult.Fail(ErrorCodes.Unauthorized, "仅房主可开始游戏");

            var seats = room.Players
                .OrderBy(p => p.SeatIndex)
                .Select(p => (p.Id, p.Name, p.SeatIndex))
                .ToList();

            IRandom rng;
            int? appliedSeed = null;
            if (debugSeed is int seed && _options.AllowDebugSeed)
            {
                rng = new SeededRandom(seed);
                appliedSeed = seed;
            }
            else
            {
                rng = new SystemRandom();
            }

            var state = PrepDayPipeline.CreateGame(seats, rng);
            PrepDayPipeline.StartGame(state);
            if (appliedSeed is int s)
                state.Log("DebugSeed", $"调试固定种子已启用：{s}");
            room.Game = state;
            return CommandResult.Success();
        });
    }

    public CommandResult SubmitOffer(
        string connectionId,
        string rank,
        IReadOnlyList<string> gemInstanceIds)
    {
        return WithGamePlayer(connectionId, (room, game, playerId) =>
        {
            if (!Enum.TryParse<SequenceRank>(rank, ignoreCase: true, out var sequenceRank))
                return CommandResult.Fail(ErrorCodes.InvalidTarget, "无效序号");

            var ids = ParseCardIds(gemInstanceIds);
            if (ids is null)
                return CommandResult.Fail(ErrorCodes.InvalidCards, "无效的宝石实例 ID");

            return ToCommand(PrepDayPipeline.SubmitOffer(game, playerId, sequenceRank, ids));
        });
    }

    public CommandResult ClearOffer(string connectionId)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(PrepDayPipeline.ClearOffer(game, playerId)));
    }

    public CommandResult PassArcana(string connectionId)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(PrepDayPipeline.PassArcana(game, playerId)));
    }

    public CommandResult PlayArcana(
        string connectionId,
        string arcanaKind,
        IReadOnlyList<int>? targetSlotIndices,
        IReadOnlyList<string>? targetPlayerIds,
        IReadOnlyList<string>? targetCardIds,
        string? rank)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            if (!Enum.TryParse<ArcanaKind>(arcanaKind, ignoreCase: true, out var kind))
                return CommandResult.Fail(ErrorCodes.InvalidCards, "未知秘术");

            var target = BuildTarget(targetSlotIndices, targetPlayerIds, targetCardIds, rank);
            if (target is null)
                return CommandResult.Fail(ErrorCodes.InvalidTarget, "无效的目标参数");

            return ToCommand(PrepDayPipeline.PlayArcana(game, playerId, kind, target));
        });
    }

    public CommandResult ArcanaStepResponse(
        string connectionId,
        string stepId,
        IReadOnlyList<int>? targetSlotIndices,
        IReadOnlyList<string>? targetPlayerIds,
        IReadOnlyList<string>? targetCardIds,
        string? rank)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            var target = BuildTarget(targetSlotIndices, targetPlayerIds, targetCardIds, rank);
            if (target is null)
                return CommandResult.Fail(ErrorCodes.InvalidTarget, "无效的目标参数");

            return ToCommand(
                PrepDayPipeline.ArcanaStepResponse(game, playerId, stepId ?? "", target));
        });
    }

    public CommandResult PassTrade(string connectionId)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(OfferingDayPipeline.PassTrade(game, playerId)));
    }

    public CommandResult ProposeTrade(
        string connectionId,
        string sellerId,
        string relicInstanceId,
        IReadOnlyList<string> buyerGemIds)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            if (!PlayerId.TryParse(sellerId, out var seller))
                return CommandResult.Fail(ErrorCodes.InvalidTarget, "无效卖家");
            if (!CardInstanceId.TryParse(relicInstanceId, out var relicId))
                return CommandResult.Fail(ErrorCodes.InvalidCards, "无效祭品");
            var gems = ParseCardIds(buyerGemIds);
            if (gems is null)
                return CommandResult.Fail(ErrorCodes.InvalidCards, "无效宝石");

            return ToCommand(
                OfferingDayPipeline.ProposeTrade(game, playerId, seller, relicId, gems));
        });
    }

    public CommandResult RespondTrade(string connectionId, bool accept)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(OfferingDayPipeline.RespondTrade(game, playerId, accept)));
    }

    public CommandResult CancelTrade(string connectionId)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(OfferingDayPipeline.CancelTrade(game, playerId)));
    }

    public CommandResult BeginForceBuy(string connectionId, IReadOnlyList<string>? newBuyerGemIds)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            IReadOnlyList<CardInstanceId>? gems = null;
            if (newBuyerGemIds is not null)
            {
                gems = ParseCardIds(newBuyerGemIds);
                if (gems is null)
                    return CommandResult.Fail(ErrorCodes.InvalidCards, "无效宝石");
            }

            return ToCommand(OfferingDayPipeline.BeginForceBuy(game, playerId, gems));
        });
    }

    public CommandResult CommitForceBuyBid(string connectionId, IReadOnlyList<string> gemIds)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            var gems = ParseCardIds(gemIds ?? Array.Empty<string>());
            if (gems is null)
                return CommandResult.Fail(ErrorCodes.InvalidCards, "无效宝石");
            return ToCommand(OfferingDayPipeline.CommitForceBuyBid(game, playerId, gems));
        });
    }

    public CommandResult CastVote(string connectionId, bool yes)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
            ToCommand(OfferingDayPipeline.CastVote(game, playerId, yes)));
    }

    public CommandResult RaiseAuction(string connectionId, IReadOnlyList<string> gemInstanceIds)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            if (game.Phase == GamePhase.Finished || game.Phase == GamePhase.FinalScoring)
                return CommandResult.Fail(ErrorCodes.InvalidPhase, "对局已结束");

            var ids = ParseCardIds(gemInstanceIds);
            if (ids is null)
                return CommandResult.Fail(ErrorCodes.InvalidCards, "无效的宝石实例 ID");

            return ToCommand(RecoveryDayPipeline.RaiseAuction(game, playerId, ids));
        });
    }

    public CommandResult PassAuction(string connectionId)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            if (game.Phase == GamePhase.Finished || game.Phase == GamePhase.FinalScoring)
                return CommandResult.Fail(ErrorCodes.InvalidPhase, "对局已结束");

            return ToCommand(RecoveryDayPipeline.PassAuction(game, playerId));
        });
    }

    public CommandResult SubmitAllInOffer(string connectionId, string rank)
    {
        return WithGamePlayer(connectionId, (_, game, playerId) =>
        {
            if (game.Phase == GamePhase.Finished || game.Phase == GamePhase.FinalScoring)
                return CommandResult.Fail(ErrorCodes.InvalidPhase, "对局已结束");

            if (!Enum.TryParse<SequenceRank>(rank, ignoreCase: true, out var sequenceRank))
                return CommandResult.Fail(ErrorCodes.InvalidTarget, "无效序号");

            return ToCommand(RecoveryDayPipeline.SubmitAllInOffer(game, playerId, sequenceRank));
        });
    }

    public (CommandResult Result, RoomView? View) Sync(string connectionId)
    {
        var binding = _store.GetConnectionBinding(connectionId);
        if (binding is null)
            return (CommandResult.Fail(ErrorCodes.NotInRoom, "你不在任何房间中"), null);

        var room = _store.GetById(binding.Value.RoomId);
        if (room is null)
            return (CommandResult.Fail(ErrorCodes.RoomNotFound, "房间不存在"), null);

        lock (room.SyncRoot)
        {
            var view = _projector.Project(room, binding.Value.PlayerId);
            return (CommandResult.Success(), view);
        }
    }

    public IReadOnlyList<(string ConnectionId, RoomView View)> BuildViewsForPush(GameRoom room)
    {
        lock (room.SyncRoot)
        {
            var list = new List<(string, RoomView)>();
            foreach (var p in room.Players)
            {
                if (string.IsNullOrEmpty(p.ConnectionId))
                    continue;
                list.Add((p.ConnectionId, _projector.Project(room, p.Id)));
            }
            return list;
        }
    }

    public GameRoom? GetRoomForConnection(string connectionId)
    {
        var binding = _store.GetConnectionBinding(connectionId);
        if (binding is null)
            return null;
        return _store.GetById(binding.Value.RoomId);
    }

    public void HandleDisconnect(string connectionId)
    {
        var binding = _store.GetConnectionBinding(connectionId);
        if (binding is null)
            return;

        var room = _store.GetById(binding.Value.RoomId);
        if (room is not null)
        {
            lock (room.SyncRoot)
            {
                var player = room.FindById(binding.Value.PlayerId);
                if (player is not null &&
                    string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal))
                {
                    player.ConnectionId = null;
                    if (room.Game is not null)
                    {
                        var gs = room.Game.FindPlayer(player.Id);
                        if (gs is not null)
                            gs.IsConnected = false;
                    }
                }
            }
        }

        _store.UnbindConnection(connectionId);
    }

    /// <summary>
    /// Guard gameplay commands when the match is finished (Sync/rejoin still allowed).
    /// </summary>
    private static CommandResult? RejectIfFinished(GameState game)
    {
        if (game.Phase is GamePhase.Finished or GamePhase.FinalScoring)
            return CommandResult.Fail(ErrorCodes.InvalidPhase, "对局已结束，仅可查看结果");
        return null;
    }

    private static ArcanaTarget? BuildTarget(
        IReadOnlyList<int>? slotIndices,
        IReadOnlyList<string>? playerIds,
        IReadOnlyList<string>? cardIds,
        string? rank)
    {
        var players = new List<PlayerId>();
        if (playerIds is not null)
        {
            foreach (var s in playerIds)
            {
                if (!PlayerId.TryParse(s, out var pid))
                    return null;
                players.Add(pid);
            }
        }

        var cards = new List<CardInstanceId>();
        if (cardIds is not null)
        {
            foreach (var s in cardIds)
            {
                if (!CardInstanceId.TryParse(s, out var cid))
                    return null;
                cards.Add(cid);
            }
        }

        SequenceRank? sequenceRank = null;
        if (!string.IsNullOrWhiteSpace(rank))
        {
            if (!Enum.TryParse<SequenceRank>(rank, ignoreCase: true, out var r))
                return null;
            sequenceRank = r;
        }

        return new ArcanaTarget
        {
            SlotIndices = slotIndices?.ToArray() ?? Array.Empty<int>(),
            PlayerIds = players,
            CardIds = cards,
            Rank = sequenceRank,
        };
    }

    private static List<CardInstanceId>? ParseCardIds(IReadOnlyList<string>? raw)
    {
        var ids = new List<CardInstanceId>();
        foreach (var s in raw ?? Array.Empty<string>())
        {
            if (!CardInstanceId.TryParse(s, out var id))
                return null;
            ids.Add(id);
        }
        return ids;
    }

    private CommandResult WithPlayer(
        string connectionId,
        Func<GameRoom, RoomPlayer, CommandResult> action)
    {
        var binding = _store.GetConnectionBinding(connectionId);
        if (binding is null)
            return CommandResult.Fail(ErrorCodes.NotInRoom, "你不在任何房间中");

        var room = _store.GetById(binding.Value.RoomId);
        if (room is null)
            return CommandResult.Fail(ErrorCodes.RoomNotFound, "房间不存在");

        lock (room.SyncRoot)
        {
            var player = room.FindById(binding.Value.PlayerId);
            if (player is null)
                return CommandResult.Fail(ErrorCodes.NotInRoom, "你不在该房间中");
            return action(room, player);
        }
    }

    private CommandResult WithGamePlayer(
        string connectionId,
        Func<GameRoom, GameState, PlayerId, CommandResult> action)
    {
        return WithPlayer(connectionId, (room, player) =>
        {
            if (room.Game is null)
                return CommandResult.Fail(ErrorCodes.InvalidPhase, "对局尚未开始");
            if (RejectIfFinished(room.Game) is { } done)
                return done;
            return action(room, room.Game, player.Id);
        });
    }

    private static CommandResult ToCommand(DomainResult r) =>
        r.Ok
            ? CommandResult.Success()
            : CommandResult.Fail(r.ErrorCode ?? "Error", r.Message ?? "操作失败");

    private string GenerateUniqueCode()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var code = GenerateCode();
            if (_store.GetByCode(code) is null)
                return code;
        }

        return GenerateCode() + GenerateCode()[..2];
    }

    private static string GenerateCode()
    {
        Span<byte> bytes = stackalloc byte[GameRules.RoomCodeLength];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(GameRules.RoomCodeLength);
        for (var i = 0; i < GameRules.RoomCodeLength; i++)
            sb.Append(CodeAlphabet[bytes[i] % CodeAlphabet.Length]);
        return sb.ToString();
    }

    private static string? NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var name = raw.Trim();
        if (name.Length is < 1 or > 16)
            return null;
        return name;
    }

    private static int ClampMinPlayers(int value) =>
        Math.Clamp(value, GameRules.MinPlayers, GameRules.MaxPlayers);

    private static int ClampMaxPlayers(int value) =>
        Math.Clamp(value, GameRules.MinPlayers, GameRules.MaxPlayers);
}
