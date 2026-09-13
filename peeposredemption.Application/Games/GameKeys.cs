namespace peeposredemption.Application.Games;

/// <summary>Stable game keys shared by the DB, the API and the pages (docs/GAMES-HUB.md).</summary>
public static class GameKeys
{
    public const string Chess = "chess";
    public const string Connect4 = "connect4";
    public const string TicTacToe = "tictactoe";
    public const string Wordle = "wordle";

    /// <summary>The two-player board games that run through GameMatch.</summary>
    public static readonly string[] Boards = { Chess, Connect4, TicTacToe };

    public static bool IsBoardGame(string? key) => key != null && Array.IndexOf(Boards, key) >= 0;

    public static readonly string[] Difficulties = { "easy", "medium", "hard" };
}
