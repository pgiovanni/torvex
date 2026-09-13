using Microsoft.AspNetCore.Mvc;

namespace peeposredemption.API.Pages.Games;

public class IndexModel : GamesPageBase
{
    public IActionResult OnGet() => RequireLogin() ?? Page();
}
