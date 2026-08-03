using Cthulu.Domain.Cards;
using Cthulu.Domain.Ids;
using Cthulu.Domain.Players;

namespace Cthulu.Domain.Scoring;

public sealed record PlayerScore(
    PlayerId PlayerId,
    string Name,
    int Total,
    bool IsWinner);

/// <summary>
/// Pure scoring from held relics + VirtualForbiddenKnowledge (Cthulu.md 祭品牌).
/// </summary>
public static class ScoreCalculator
{
    public static IReadOnlyList<PlayerScore> Compute(IReadOnlyList<PlayerState> players)
    {
        // Global counts across all players for quantity-gated relics.
        var totalWeirdStatue = CountRelicAll(players, RelicKind.WeirdStatue);
        var totalLantern = CountRelicAll(players, RelicKind.Lantern);

        var cupCounts = players
            .Select(p => (p.Id, Count: CountRelic(p, RelicKind.ObsidianCup)))
            .ToList();
        var maxCups = cupCounts.Count == 0 ? 0 : cupCounts.Max(c => c.Count);
        var cupLeaders = cupCounts
            .Where(c => c.Count > 0 && c.Count == maxCups)
            .Select(c => c.Id)
            .ToHashSet();

        var scores = new List<PlayerScore>(players.Count);
        foreach (var p in players)
        {
            var total = 0;

            // VisionEye: pairs of 2 → 10 each; odd one out → 0
            var eyes = CountRelic(p, RelicKind.VisionEye);
            total += (eyes / 2) * 10;

            // WeirdStatue: >3 total on table → 3 each, else 7 each
            var statues = CountRelic(p, RelicKind.WeirdStatue);
            total += statues * (totalWeirdStatue > 3 ? 3 : 7);

            // Necronomicon: 6 each, max 1 counts (R24)
            var necros = CountRelic(p, RelicKind.Necronomicon);
            if (necros >= 1)
                total += 6;

            // BrokenScript: 3 each
            total += CountRelic(p, RelicKind.BrokenScript) * 3;

            // Lantern: >3 total → 5 each, else 2 each
            var lanterns = CountRelic(p, RelicKind.Lantern);
            total += lanterns * (totalLantern > 3 ? 5 : 2);

            // ObsidianCup: 1 each + 15 if among max holders (ties all get +15)
            var cups = CountRelic(p, RelicKind.ObsidianCup);
            total += cups * 1;
            if (cups > 0 && cupLeaders.Contains(p.Id))
                total += 15;

            // BloodyBone: 4 each
            total += CountRelic(p, RelicKind.BloodyBone) * 4;

            // HolyMedium: 8 each
            total += CountRelic(p, RelicKind.HolyMedium) * 8;

            // RitualTool: 2 each; if this player holds ≥3, +15 once
            var tools = CountRelic(p, RelicKind.RitualTool);
            total += tools * 2;
            if (tools >= 3)
                total += 15;

            // ForbiddenKnowledge + virtual: 1→-3, 2→9, 3→-27, 4+→0
            var fk = CountRelic(p, RelicKind.ForbiddenKnowledge) + p.VirtualForbiddenKnowledge;
            total += ScoreForbiddenKnowledge(fk);

            scores.Add(new PlayerScore(p.Id, p.Name, total, IsWinner: false));
        }

        if (scores.Count == 0)
            return scores;

        var best = scores.Max(s => s.Total);
        return scores
            .Select(s => s with { IsWinner = s.Total == best })
            .ToList();
    }

    public static int ScoreForbiddenKnowledge(int count) => count switch
    {
        0 => 0,
        1 => -3,
        2 => 9,
        3 => -27,
        _ => 0, // 4+
    };

    private static int CountRelic(PlayerState p, RelicKind kind) =>
        p.Relics.Count(r => r.Def.RelicKind == kind);

    private static int CountRelicAll(IReadOnlyList<PlayerState> players, RelicKind kind) =>
        players.Sum(p => CountRelic(p, kind));
}
