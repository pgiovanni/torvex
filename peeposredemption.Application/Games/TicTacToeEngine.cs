using System.Text.Json;

namespace peeposredemption.Application.Games;

/// <summary>Tic-Tac-Toe position: 9 cells row-major, 0 empty / 1 P1 / 2 P2, plus the move list.</summary>
public sealed class TicTacToeState
{
    public int[] Cells { get; set; } = new int[9];
    public List<int> Moves { get; set; } = new();

    public static TicTacToeState New() => new();
    public static TicTacToeState FromJson(string json)
        => string.IsNullOrWhiteSpace(json) || json == "{}" ? New()
           : JsonSerializer.Deserialize<TicTacToeState>(json, TicTacToeEngine.JsonOpts) ?? New();
    public string ToJson() => JsonSerializer.Serialize(this, TicTacToeEngine.JsonOpts);
}

public static class TicTacToeEngine
{
    internal static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static readonly int[][] Lines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
    };

    /// <summary>Side to move: P1 on even move counts.</summary>
    public static int SideToMove(TicTacToeState s) => s.Moves.Count % 2 == 0 ? 1 : 2;

    public static bool TryParseMove(string? text, out int cell)
        => int.TryParse(text, out cell) && cell >= 0 && cell < 9;

    /// <summary>Plays <paramref name="cell"/> for <paramref name="side"/>. Throws on an illegal move.</summary>
    public static void Apply(TicTacToeState s, int cell, int side)
    {
        if (side != SideToMove(s)) throw new InvalidOperationException("Not your turn.");
        if (cell < 0 || cell > 8) throw new ArgumentException("Cell must be 0-8.");
        if (s.Cells[cell] != 0) throw new ArgumentException("That square is taken.");
        if (Winner(s.Cells, out _) != 0) throw new InvalidOperationException("The game is over.");
        s.Cells[cell] = side;
        s.Moves.Add(cell);
    }

    /// <summary>0 = no winner yet; otherwise the side, with the completed line's cells.</summary>
    public static int Winner(int[] cells, out int[] winningCells)
    {
        foreach (var line in Lines)
        {
            var a = cells[line[0]];
            if (a != 0 && a == cells[line[1]] && a == cells[line[2]])
            {
                winningCells = line;
                return a;
            }
        }
        winningCells = Array.Empty<int>();
        return 0;
    }

    public static bool IsFull(int[] cells) => Array.IndexOf(cells, 0) < 0;

    public static IEnumerable<int> LegalMoves(int[] cells)
    {
        for (var i = 0; i < 9; i++) if (cells[i] == 0) yield return i;
    }

    /// <summary>
    /// Computer move. easy = random; medium = random 40% of the time, otherwise
    /// perfect; hard = perfect minimax. Returns -1 when no move is possible.
    /// </summary>
    public static int BestMove(TicTacToeState s, int side, string? difficulty, Random? rng = null)
    {
        rng ??= Random.Shared;
        var legal = LegalMoves(s.Cells).ToArray();
        if (legal.Length == 0) return -1;
        var d = (difficulty ?? "medium").ToLowerInvariant();
        if (d == "easy" || (d == "medium" && rng.NextDouble() < 0.4))
            return legal[rng.Next(legal.Length)];

        var best = int.MinValue;
        var choices = new List<int>();
        foreach (var m in legal)
        {
            s.Cells[m] = side;
            var score = -Negamax(s.Cells, 3 - side, int.MinValue + 1, int.MaxValue - 1, 1);
            s.Cells[m] = 0;
            if (score > best) { best = score; choices.Clear(); choices.Add(m); }
            else if (score == best) choices.Add(m);
        }
        return choices[rng.Next(choices.Count)];
    }

    // Score from the point of view of `side` (the player about to move). Wins
    // sooner score higher so the bot finishes instead of dawdling.
    private static int Negamax(int[] cells, int side, int alpha, int beta, int depth)
    {
        var w = Winner(cells, out _);
        if (w != 0) return w == side ? 10 - depth : depth - 10;
        if (IsFull(cells)) return 0;
        var best = int.MinValue + 1;
        for (var i = 0; i < 9; i++)
        {
            if (cells[i] != 0) continue;
            cells[i] = side;
            var score = -Negamax(cells, 3 - side, -beta, -alpha, depth + 1);
            cells[i] = 0;
            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }
}
