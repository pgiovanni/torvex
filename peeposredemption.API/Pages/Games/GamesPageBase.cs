using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace peeposredemption.API.Pages.Games;

/// <summary>
/// Shared base for every games-hub page. The pages are thin: they only confirm
/// the visitor is logged in (redirect to /Auth/Login otherwise) and hand the
/// user id to the JS, which talks to /api/games/* directly. All game logic and
/// data lives behind the API — see docs/GAMES-HUB.md.
/// </summary>
public abstract class GamesPageBase : PageModel
{
    /// <summary>Lower-case guid string of the signed-in user, exposed to JS as window.TORVEX_UID.</summary>
    public string CurrentUserId { get; private set; } = string.Empty;

    /// <summary>Returns a redirect when not logged in; null when the page may render.</summary>
    protected IActionResult? RequireLogin()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (claim == null || !Guid.TryParse(claim, out var id))
            return RedirectToPage("/Auth/Login", new { returnUrl = Request.Path + Request.QueryString });
        CurrentUserId = id.ToString();
        return null;
    }
}
