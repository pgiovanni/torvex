using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace peeposredemption.Application.Games;

/// <summary>
/// Wordle rules + word lists. Answers come from the bot's curated common-word
/// list; guesses are accepted from a much larger public list (both embedded).
/// Marks per letter: G right spot, Y in the word elsewhere, B absent — with the
/// classic rule that a letter is only marked Y as many times as it appears
/// unmatched in the answer.
/// </summary>
public static class WordleEngine
{
    public const int WordLength = 5;
    public const int MaxGuesses = 6;

    private static readonly Lazy<string[]> _answers = new(() => Load("answers.txt"));
    private static readonly Lazy<HashSet<string>> _allowed = new(() =>
    {
        var set = new HashSet<string>(Load("allowed.txt"), StringComparer.Ordinal);
        foreach (var a in _answers.Value) set.Add(a);
        return set;
    });

    public static IReadOnlyList<string> Answers => _answers.Value;
    public static int AnswerCount => _answers.Value.Length;

    private static string[] Load(string name)
    {
        var asm = Assembly.GetExecutingAssembly();
        var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException($"Embedded word list {name} missing.");
        using var stream = asm.GetManifestResourceStream(res)!;
        using var reader = new StreamReader(stream);
        var words = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            var w = line.Trim().ToUpperInvariant();
            if (w.Length == WordLength && w.All(c => c is >= 'A' and <= 'Z')) words.Add(w);
        }
        return words.Distinct().ToArray();
    }

    public static bool IsAllowed(string word) => _allowed.Value.Contains(Normalize(word));

    public static string Normalize(string? word) => (word ?? string.Empty).Trim().ToUpperInvariant();

    public static bool LooksLikeWord(string word)
        => word.Length == WordLength && word.All(c => c is >= 'A' and <= 'Z');

    /// <summary>Deterministic daily answer: everyone gets the same word for a UTC date.</summary>
    public static string DailyAnswer(DateOnly date)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"torvex-wordle-{date:yyyy-MM-dd}"));
        var n = BitConverter.ToUInt32(bytes, 0);
        return _answers.Value[(int)(n % (uint)_answers.Value.Length)];
    }

    public static string RandomAnswer(Random? rng = null)
        => _answers.Value[(rng ?? Random.Shared).Next(_answers.Value.Length)];

    /// <summary>"GYBBG"-style marks for <paramref name="guess"/> against <paramref name="answer"/> (both upper, 5 letters).</summary>
    public static string Marks(string guess, string answer)
    {
        var marks = new char[WordLength];
        var remaining = new int[26];
        for (var i = 0; i < WordLength; i++)
        {
            if (guess[i] == answer[i]) marks[i] = 'G';
            else remaining[answer[i] - 'A']++;
        }
        for (var i = 0; i < WordLength; i++)
        {
            if (marks[i] == 'G') continue;
            var idx = guess[i] - 'A';
            if (remaining[idx] > 0) { marks[i] = 'Y'; remaining[idx]--; }
            else marks[i] = 'B';
        }
        return new string(marks);
    }

    /// <summary>
    /// Hard mode: every G from earlier guesses must stay in place and every Y
    /// letter must be reused. Returns null when fine, else the reason.
    /// </summary>
    public static string? HardModeViolation(string guess, IEnumerable<string> priorGuesses, string answer)
    {
        foreach (var prior in priorGuesses)
        {
            var m = Marks(prior, answer);
            for (var i = 0; i < WordLength; i++)
            {
                if (m[i] == 'G' && guess[i] != prior[i])
                    return $"Letter {i + 1} must be {prior[i]}.";
            }
            for (var i = 0; i < WordLength; i++)
            {
                if (m[i] == 'Y' && !guess.Contains(prior[i]))
                    return $"Guess must contain {prior[i]}.";
            }
        }
        return null;
    }
}
