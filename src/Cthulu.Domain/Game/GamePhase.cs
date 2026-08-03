namespace Cthulu.Domain.Game;

/// <summary>Game phase state machine (ENGINEERING §6.5 / §7).</summary>
public enum GamePhase
{
    Lobby = 0,
    Prep_Draw = 1,
    Prep_Offer = 2,
    Prep_ArcanaTurn = 3,
    Prep_Resolve = 4,
    OfferingDay_Trade = 5,
    VoteContinue = 6,
    Recovery_Auction = 7,
    Recovery_AllIn = 8,
    FinalScoring = 9,
    Finished = 10,
}
