using Cthulu.Domain.Random;

namespace Cthulu.Domain.Cards;

/// <summary>Builds full card pools per ENGINEERING §6.3 / R26.</summary>
public static class CardCatalog
{
    /// <summary>
    /// Gem face-value copies: 1 &amp; 5 → 6 each; 2 &amp; 4 → 10 each; 3 → 13.
    /// Total 45. Initial deal (R14) takes 1 of each value from this entity pile.
    /// </summary>
    public static readonly IReadOnlyDictionary<GemValue, int> GemCounts =
        new Dictionary<GemValue, int>
        {
            [GemValue.One] = 6,
            [GemValue.Two] = 10,
            [GemValue.Three] = 13,
            [GemValue.Four] = 10,
            [GemValue.Five] = 6,
        };

    public static readonly IReadOnlyDictionary<RelicKind, int> RelicCounts =
        new Dictionary<RelicKind, int>
        {
            [RelicKind.VisionEye] = 6,
            [RelicKind.WeirdStatue] = 4,
            [RelicKind.Necronomicon] = 2,
            [RelicKind.BrokenScript] = 4,
            [RelicKind.Lantern] = 5,
            [RelicKind.ObsidianCup] = 6,
            [RelicKind.BloodyBone] = 3,
            [RelicKind.HolyMedium] = 1,
            [RelicKind.RitualTool] = 5,
            [RelicKind.ForbiddenKnowledge] = 4,
        };

    public static IReadOnlyList<ArcanaKind> AllArcanaKinds { get; } =
        Enum.GetValues<ArcanaKind>();

    /// <summary>Legacy M1 subset (kept for docs/tests). M2 enables all arcana.</summary>
    public static readonly HashSet<ArcanaKind> M1EnabledArcana = new()
    {
        ArcanaKind.FishingNet,
        ArcanaKind.Omniscient,
        ArcanaKind.RlyehFog,
        ArcanaKind.StaffOfForgetting,
        ArcanaKind.FelReplenish,
        ArcanaKind.ReverseThinking,
        ArcanaKind.EvilProphecy,
        ArcanaKind.Alchemy,
    };

    private static HashSet<ArcanaKind> _enabledArcana = new(Enum.GetValues<ArcanaKind>());

    /// <summary>
    /// Currently enabled arcana kinds. Default: all (M2).
    /// Reconfigured at process start via <see cref="ConfigureEnabledArcana"/>.
    /// </summary>
    public static IReadOnlySet<ArcanaKind> EnabledArcana => _enabledArcana;

    /// <summary>
    /// Configure which arcana can be played. Empty / null → all kinds.
    /// Unimplemented kinds still fail at Get(); Play returns ArcanaNotEnabled when missing here.
    /// </summary>
    public static void ConfigureEnabledArcana(IEnumerable<ArcanaKind>? kinds)
    {
        if (kinds is null)
        {
            _enabledArcana = new HashSet<ArcanaKind>(Enum.GetValues<ArcanaKind>());
            return;
        }

        var set = kinds.ToHashSet();
        _enabledArcana = set.Count == 0
            ? new HashSet<ArcanaKind>(Enum.GetValues<ArcanaKind>())
            : set;
    }

    /// <summary>Parse enum names (case-insensitive); unknown names are ignored.</summary>
    public static IReadOnlyList<ArcanaKind> ParseArcanaNames(IEnumerable<string>? names)
    {
        if (names is null)
            return Array.Empty<ArcanaKind>();

        var list = new List<ArcanaKind>();
        foreach (var raw in names)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (Enum.TryParse<ArcanaKind>(raw.Trim(), ignoreCase: true, out var kind))
                list.Add(kind);
        }
        return list;
    }

    public static List<CardInstance> CreateAllGems()
    {
        var list = new List<CardInstance>(ExpectedGemCount);
        foreach (var (value, count) in GemCounts)
        {
            for (var i = 0; i < count; i++)
                list.Add(CardInstance.Create(CardDef.Gem(value)));
        }
        return list;
    }

    public static List<CardInstance> CreateAllRelics()
    {
        var list = new List<CardInstance>();
        foreach (var (kind, count) in RelicCounts)
        {
            for (var i = 0; i < count; i++)
                list.Add(CardInstance.Create(CardDef.Relic(kind)));
        }
        return list;
    }

    public static List<CardInstance> CreateAllArcana()
    {
        var list = new List<CardInstance>(AllArcanaKinds.Count);
        foreach (var kind in AllArcanaKinds)
            list.Add(CardInstance.Create(CardDef.Arcana(kind)));
        return list;
    }

    public static (Deck Gems, Deck Arcana, Deck Relics) CreateShuffledDecks(IRandom rng)
    {
        var gems = new Deck(CreateAllGems(), rng);
        gems.Shuffle();
        var arcana = new Deck(CreateAllArcana(), rng);
        arcana.Shuffle();
        var relics = new Deck(CreateAllRelics(), rng);
        relics.Shuffle();
        return (gems, arcana, relics);
    }

    public static int ExpectedGemCount => GemCounts.Values.Sum(); // 45
    public static int ExpectedRelicCount => RelicCounts.Values.Sum(); // 40
    public static int ExpectedArcanaCount => AllArcanaKinds.Count; // 20
}
