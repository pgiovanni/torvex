using Microsoft.EntityFrameworkCore;
using peeposredemption.Application.Games;
using peeposredemption.Domain.Entities.Games;
using peeposredemption.Infrastructure.Persistence;

namespace peeposredemption.API.Games;

/// <summary>Daily + practice Wordle on top of <see cref="WordleEngine"/>. The answer never leaves the server before the game is over.</summary>
public sealed class WordleService
{
    private readonly AppDbContext _db;
    public WordleService(AppDbContext db) => _db = db;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<WordleState> TodayAsync(Guid me)
    {
        var today = Today;
        var play = await _db.WordlePlays.FirstOrDefaultAsync(w => w.UserId == me && w.PuzzleDate == today);
        if (play is null)
        {
            play = new WordlePlay { UserId = me, PuzzleDate = today, Answer = WordleEngine.DailyAnswer(today) };
            _db.WordlePlays.Add(play);
            await _db.SaveChangesAsync();
        }
        return await ToStateAsync(play, me);
    }

    public async Task<WordleState?> PracticeAsync(Guid me)
    {
        var play = await _db.WordlePlays.Where(w => w.UserId == me && w.PuzzleDate == null)
            .OrderByDescending(w => w.StartedAt).FirstOrDefaultAsync();
        return play is null ? null : await ToStateAsync(play, me);
    }

    public async Task<WordleState> NewPracticeAsync(Guid me, bool hard)
    {
        // an unfinished practice game is simply abandoned (marked finished, unsolved)
        var open = await _db.WordlePlays.Where(w => w.UserId == me && w.PuzzleDate == null && !w.Finished).ToListAsync();
        foreach (var o in open) { o.Finished = true; o.FinishedAt = DateTime.UtcNow; }
        var play = new WordlePlay { UserId = me, PuzzleDate = null, Answer = WordleEngine.RandomAnswer(), Hard = hard };
        _db.WordlePlays.Add(play);
        await _db.SaveChangesAsync();
        return await ToStateAsync(play, me);
    }

    public async Task<WordleState> GuessAsync(Guid me, WordleGuessRequest req)
    {
        var word = WordleEngine.Normalize(req.Word);
        if (!WordleEngine.LooksLikeWord(word)) throw new GameApiException(400, "Five letters, A-Z.");
        if (!WordleEngine.IsAllowed(word)) throw new GameApiException(400, "Not in word list.");

        WordlePlay? play;
        if (req.Practice == true)
        {
            play = await _db.WordlePlays.Where(w => w.UserId == me && w.PuzzleDate == null)
                .OrderByDescending(w => w.StartedAt).FirstOrDefaultAsync();
            if (play is null || play.Finished) throw new GameApiException(400, "Start a practice game first.");
        }
        else
        {
            var today = Today;
            play = await _db.WordlePlays.FirstOrDefaultAsync(w => w.UserId == me && w.PuzzleDate == today);
            if (play is null)
            {
                play = new WordlePlay { UserId = me, PuzzleDate = today, Answer = WordleEngine.DailyAnswer(today) };
                _db.WordlePlays.Add(play);
            }
        }
        if (play.Finished) throw new GameApiException(400, "Already finished.");
        if (req.Hard == true && play.GuessCount == 0) play.Hard = true;

        var prior = Split(play.Guesses);
        if (play.Hard)
        {
            var violation = WordleEngine.HardModeViolation(word, prior, play.Answer);
            if (violation != null) throw new GameApiException(400, violation);
        }

        prior.Add(word);
        play.Guesses = string.Join(",", prior);
        play.GuessCount = prior.Count;
        if (word == play.Answer) { play.Solved = true; play.Finished = true; }
        else if (play.GuessCount >= WordleEngine.MaxGuesses) play.Finished = true;
        if (play.Finished) play.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await ToStateAsync(play, me);
    }

    public async Task<WordleStats> StatsAsync(Guid me)
    {
        var plays = await _db.WordlePlays
            .Where(w => w.UserId == me && w.PuzzleDate != null && w.Finished)
            .Select(w => new { w.PuzzleDate, w.Solved, w.GuessCount })
            .ToListAsync();
        var dist = new int[WordleEngine.MaxGuesses];
        foreach (var p in plays.Where(p => p.Solved && p.GuessCount is >= 1 and <= WordleEngine.MaxGuesses))
            dist[p.GuessCount - 1]++;
        var solvedDates = plays.Where(p => p.Solved).Select(p => p.PuzzleDate!.Value).ToHashSet();
        return new WordleStats(plays.Count, solvedDates.Count, CurrentStreak(solvedDates), MaxStreak(solvedDates), dist);
    }

    public async Task<List<WordleLeaderRow>> LeaderboardAsync(int days)
    {
        days = Math.Clamp(days, 1, 365);
        var since = Today.AddDays(-days + 1);
        var rows = await _db.WordlePlays.Include(w => w.User)
            .Where(w => w.PuzzleDate != null && w.PuzzleDate >= since && w.Finished)
            .Select(w => new { w.UserId, w.User.Username, w.User.DisplayName, w.User.AvatarUrl, w.PuzzleDate, w.Solved, w.GuessCount })
            .ToListAsync();
        var result = new List<WordleLeaderRow>();
        foreach (var g in rows.GroupBy(r => r.UserId))
        {
            var first = g.First();
            var solved = g.Where(x => x.Solved).ToList();
            var avg = solved.Count == 0 ? 0 : Math.Round(solved.Average(x => x.GuessCount), 2);
            // streak needs the whole history, not just the window
            var allSolved = await _db.WordlePlays.Where(w => w.UserId == g.Key && w.PuzzleDate != null && w.Solved)
                .Select(w => w.PuzzleDate!.Value).ToListAsync();
            result.Add(new WordleLeaderRow(new PlayerRef(g.Key, first.DisplayName ?? first.Username, first.AvatarUrl),
                                           solved.Count, g.Count(), avg, CurrentStreak(allSolved.ToHashSet())));
        }
        return result.OrderByDescending(r => r.Solved).ThenBy(r => r.AvgGuesses == 0 ? 99 : r.AvgGuesses)
                     .ThenByDescending(r => r.Streak).Take(50).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static List<string> Split(string csv)
        => string.IsNullOrEmpty(csv) ? new List<string>() : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>Consecutive solved daily puzzles ending today or yesterday.</summary>
    private static int CurrentStreak(HashSet<DateOnly> solved)
    {
        var day = Today;
        if (!solved.Contains(day)) day = day.AddDays(-1);
        var n = 0;
        while (solved.Contains(day)) { n++; day = day.AddDays(-1); }
        return n;
    }

    private static int MaxStreak(HashSet<DateOnly> solved)
    {
        var best = 0;
        foreach (var d in solved)
        {
            if (solved.Contains(d.AddDays(-1))) continue;   // only start counting at a run's first day
            var n = 0; var day = d;
            while (solved.Contains(day)) { n++; day = day.AddDays(1); }
            best = Math.Max(best, n);
        }
        return best;
    }

    private async Task<WordleState> ToStateAsync(WordlePlay p, Guid me)
    {
        var guesses = Split(p.Guesses).Select(g => new WordleGuess(g, WordleEngine.Marks(g, p.Answer))).ToList();
        var streak = 0;
        if (p.PuzzleDate != null)
        {
            var solved = await _db.WordlePlays.Where(w => w.UserId == me && w.PuzzleDate != null && w.Solved)
                .Select(w => w.PuzzleDate!.Value).ToListAsync();
            streak = CurrentStreak(solved.ToHashSet());
        }
        return new WordleState(p.Id, p.PuzzleDate is null ? "practice" : "daily",
                               p.PuzzleDate?.ToString("yyyy-MM-dd"), WordleEngine.MaxGuesses, guesses,
                               p.Finished, p.Solved, p.Finished ? p.Answer : null, p.Hard, streak);
    }
}
