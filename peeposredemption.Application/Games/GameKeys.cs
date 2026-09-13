namespace peeposredemption.Application.Games;

/// <summary>Stable game keys shared by the DB, the API and the pages (docs/GAMES-HUB.md).</summary>
public static class GameKeys
{
    public const string Chess = "chess";
    public const string Connect4 = "connect4";
    public const string TicTacToe = "tictactoe";
    public const string Wordle = "wordle";

    // Which keys are board games is answered by GameRegistry (the DI-registered IBoardGame set).
    public static readonly string[] Difficulties = { "easy", "medium", "hard" };
}
