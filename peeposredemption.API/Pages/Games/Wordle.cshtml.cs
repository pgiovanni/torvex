using Microsoft.AspNetCore.Mvc;

namespace peeposredemption.API.Pages.Games;

public class WordleModel : GamesPageBase
{
    public IActionResult OnGet() => RequireLogin() ?? Page();
}
