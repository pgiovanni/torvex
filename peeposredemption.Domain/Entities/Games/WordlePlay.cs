namespace peeposredemption.Domain.Entities.Games;

/// <summary>
/// One Wordle game for one user. <see cref="PuzzleDate"/> set = the shared
/// daily puzzle (one row per user per day, the leaderboard reads these);
/// null = an unlimited practice game. The answer stays server-side until the
/// game is over — the API never returns it while <see cref="Finished"/> is false.
/// </summary>
public class WordlePlay
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateOnly? PuzzleDate { get; set; }
    public string Answer { get; set; } = string.Empty;
    /// <summary>Comma-separated guesses in order, upper-case.</summary>
    public string Guesses { get; set; } = string.Empty;
    public int GuessCount { get; set; }
    public bool Solved { get; set; }
    public bool Finished { get; set; }
    public bool Hard { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}
