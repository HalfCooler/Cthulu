namespace Cthulu.Domain.Cards;

public enum CardType
{
    Gem = 0,
    Sequence = 1, // not used as physical card (R4)
    Relic = 2,
    Arcana = 3,
}

public enum GemValue
{
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
}

/// <summary>Offer target annotation for altar slot (1-based). No physical card (R4/R5).</summary>
public enum SequenceRank
{
    I = 1,
    II = 2,
    III = 3,
    IV = 4,
}

public enum RelicKind
{
    VisionEye,         // 灵视眼瞳
    WeirdStatue,       // 诡异雕像
    Necronomicon,      // 死灵之书
    BrokenScript,      // 残缺咒文
    Lantern,           // 提灯
    ObsidianCup,       // 黑曜石酒杯
    BloodyBone,        // 沾血手骨
    HolyMedium,        // 神圣媒介
    RitualTool,        // 仪式用具
    ForbiddenKnowledge // 禁忌知识
}

public enum ArcanaKind
{
    RlyehFog,
    StaffOfForgetting,
    FelReplenish,
    Peek,
    MaliciousSwap,
    Alchemy,
    MentalInterference,
    MightyGrasp,
    BlackWind,
    ArtificialBreeding,
    Spiritism,
    FishingNet,
    KnowledgeErosion,
    FearResonance,
    Transplant,
    Omniscient,
    ReverseThinking,
    MindSuggestion,
    Fanaticism,
    EvilProphecy,
}
