namespace Cthulu.Domain.Random;

/// <summary>Deterministic RNG for unit tests.</summary>
public sealed class SeededRandom : IRandom
{
    private readonly System.Random _rng;

    public SeededRandom(int seed) => _rng = new System.Random(seed);

    public int Next(int minValue, int maxValue) => _rng.Next(minValue, maxValue);

    public int Next(int maxValue) => _rng.Next(maxValue);
}
