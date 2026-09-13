using Microsoft.AspNetCore.Mvc;

namespace peeposredemption.API.Pages.Games;

public class LeaderboardModel : GamesPageBase
{
    private static readonly string[] Keys = { "chess", "connect4", "tictactoe" };

    /// <summary>Selected game key (query ?game=), defaults to chess.</summary>
    public string GameKey { get; private set; } = "chess";

    public IActionResult OnGet(string? game)
    {
        if (RequireLogin() is IActionResult redirect) return redirect;
        if (game != null && Keys.Contains(game.ToLowerInvariant())) GameKey = game.ToLowerInvariant();
        return Page();
    }
}
