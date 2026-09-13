using Microsoft.AspNetCore.Mvc;

namespace peeposredemption.API.Pages.Games;

/// <summary>
/// One lobby page for the three two-player games: /Games/Chess, /Games/Connect4,
/// /Games/TicTacToe. The route segment is the display name; <see cref="GameKey"/>
/// is the API key the JS uses.
/// </summary>
public class LobbyModel : GamesPageBase
{
    public static readonly IReadOnlyDictionary<string, (string Key, string Title, string Icon, string Blurb, string SeatP1, string SeatP2)> Games =
        new Dictionary<string, (string, string, string, string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chess"] = ("chess", "Chess", "♟️",
                "Full rules, rated Elo like lichess: checkmate, stalemate, threefold repetition, the fifty-move rule and insufficient material are all called automatically. White moves first. Offer a draw or resign from the board.",
                "White", "Black"),
            ["Connect4"] = ("connect4", "Connect Four", "🔴",
                "Drop discs into a 7×6 grid. Four in a row — across, down or diagonal — wins. A full board with no line is a draw. Red goes first.",
                "First (red)", "Second (yellow)"),
            ["TicTacToe"] = ("tictactoe", "Tic-Tac-Toe", "❌",
                "Three in a row wins. X goes first. Perfect play always draws, so the rating rewards not slipping.",
                "First (X)", "Second (O)"),
        };

    public string GameKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Icon { get; private set; } = string.Empty;
    public string Blurb { get; private set; } = string.Empty;
    public string SeatP1 { get; private set; } = string.Empty;
    public string SeatP2 { get; private set; } = string.Empty;

    public IActionResult OnGet(string game)
    {
        if (RequireLogin() is IActionResult redirect) return redirect;
        if (!Games.TryGetValue(game, out var g)) return NotFound();
        (GameKey, Title, Icon, Blurb, SeatP1, SeatP2) = g;
        return Page();
    }
}
