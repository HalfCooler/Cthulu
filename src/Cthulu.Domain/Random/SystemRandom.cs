namespace Cthulu.Domain.Random;

public sealed class SystemRandom : IRandom
{
    private readonly System.Random _rng = new();

    public int Next(int minValue, int maxValue) => _rng.Next(minValue, maxValue);

    public int Next(int maxValue) => _rng.Next(maxValue);
}
