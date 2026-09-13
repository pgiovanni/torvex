namespace peeposredemption.Domain.Entities.Games;

/// <summary>
/// One player's rating in one game (chess / connect4 / tictactoe). Elo with a
/// provisional K-factor (see Application.Games.Elo). One row per (user, game);
/// created on the first RATED match, so casual-only players have no row.
/// </summary>
public class GameRating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    /// <summary>Stable game key: "chess", "connect4", "tictactoe".</summary>
    public string Game { get; set; } = string.Empty;
    public int Rating { get; set; } = 1200;
    public int Peak { get; set; } = 1200;
    public int Games { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Fewer than this many rated games = provisional (shown with a "?").</summary>
    public const int ProvisionalGames = 10;
    public bool IsProvisional => Games < ProvisionalGames;
}
