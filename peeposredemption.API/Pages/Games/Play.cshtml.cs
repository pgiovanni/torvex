using Microsoft.AspNetCore.Mvc;

namespace peeposredemption.API.Pages.Games;

/// <summary>/Games/Play/{id} — the board. The JS fetches the match and renders by game.</summary>
public class PlayModel : GamesPageBase
{
    public Guid MatchId { get; private set; }

    public IActionResult OnGet(Guid id)
    {
        if (RequireLogin() is IActionResult redirect) return redirect;
        MatchId = id;
        return Page();
    }
}
