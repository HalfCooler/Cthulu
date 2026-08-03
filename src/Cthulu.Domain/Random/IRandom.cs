namespace Cthulu.Domain.Random;

/// <summary>Injectable RNG for shuffle / random picks (tests use fixed seed).</summary>
public interface IRandom
{
    /// <summary>Inclusive min, exclusive max — same contract as System.Random.Next.</summary>
    int Next(int minValue, int maxValue);

    int Next(int maxValue);
}
