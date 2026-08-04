namespace Cthulu.Domain.Game;

/// <summary>Match mode selected when the host starts the game from lobby.</summary>
public enum GameMode
{
    /// <summary>Standard multiplayer (4–6 humans).</summary>
    Standard = 0,

    /// <summary>
    /// Creative / sandbox: 1 human + 3 bots; human may freely take cards from decks.
    /// Bots always pass when possible, otherwise pick the first N available options.
    /// </summary>
    Creative = 1,
}
