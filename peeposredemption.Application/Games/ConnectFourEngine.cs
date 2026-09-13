using System.Text.Json;

namespace peeposredemption.Application.Games;

/// <summary>
/// Connect Four position. <see cref="Cells"/> is [row][col], row 0 = TOP (what
/// the page draws), 0 empty / 1 P1 / 2 P2. A piece dropped in a column lands
/// on the lowest empty row.
/// </summary>
public sealed class ConnectFourState
{
    public const int Rows = 6;
    public const int Cols = 7;

    public int[][] Cells { get; set; } = NewCells();
    public List<int> Moves { get; set; } = new();

    public static int[][] NewCells()
    {
        var c = new int[Rows][];
        for (var r = 0; r < Rows; r++) c[r] = new int[Cols];
        return c;
    }

    public static ConnectFourState New() => new();
    public static ConnectFourState FromJson(string json)
        => string.IsNullOrWhiteSpace(json) || json == "{}" ? New()
           : JsonSerializer.Deserialize<ConnectFourState>(json, ConnectFourEngine.JsonOpts) ?? New();
    public string ToJson() => JsonSerializer.Serialize(this, ConnectFourEngine.JsonOpts);
}

public static class ConnectFourEngine
{
    internal static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private const int R = ConnectFourState.Rows, C = ConnectFourState.Cols;

    public static int SideToMove(ConnectFourState s) => s.Moves.Count % 2 == 0 ? 1 : 2;

    public static bool TryParseMove(string? text, out int col)
        => int.TryParse(text, out col) && col >= 0 && col < C;

    /// <summary>Lowest empty row in a column, or -1 when full.</summary>
    public static int DropRow(int[][] cells, int col)
    {
        for (var r = R - 1; r >= 0; r--) if (cells[r][col] == 0) return r;
        return -1;
    }

    public static void Apply(ConnectFourState s, int col, int side)
    {
        if (side != SideToMove(s)) throw new InvalidOperationException("Not your turn.");
        if (col < 0 || col >= C) throw new ArgumentException("Column must be 0-6.");
        if (Winner(s.Cells, out _) != 0) throw new InvalidOperationException("The game is over.");
        var row = DropRow(s.Cells, col);
        if (row < 0) throw new ArgumentException("That column is full.");
        s.Cells[row][col] = side;
        s.Moves.Add(col);
    }

    public static bool IsFull(int[][] cells)
    {
        for (var c = 0; c < C; c++) if (cells[0][c] == 0) return false;
        return true;
    }

    public static IEnumerable<int> LegalMoves(int[][] cells)
    {
        // centre-out ordering helps alpha-beta and makes ties prefer the middle
        foreach (var c in new[] { 3, 2, 4, 1, 5, 0, 6 })
            if (cells[0][c] == 0) yield return c;
    }

    private static readonly (int dr, int dc)[] Dirs = { (0, 1), (1, 0), (1, 1), (1, -1) };

    /// <summary>0 = none; otherwise the winning side and the four cells as [row, col] pairs.</summary>
    public static int Winner(int[][] cells, out int[][] winningCells)
    {
        for (var r = 0; r < R; r++)
            for (var c = 0; c < C; c++)
            {
                var p = cells[r][c];
                if (p == 0) continue;
                foreach (var (dr, dc) in Dirs)
                {
                    int er = r + 3 * dr, ec = c + 3 * dc;
                    if (er < 0 || er >= R || ec < 0 || ec >= C) continue;
                    if (cells[r + dr][c + dc] == p && cells[r + 2 * dr][c + 2 * dc] == p && cells[er][ec] == p)
                    {
                        winningCells = new[]
                        {
                            new[] { r, c }, new[] { r + dr, c + dc },
                            new[] { r + 2 * dr, c + 2 * dc }, new[] { er, ec },
                        };
                        return p;
                    }
                }
            }
        winningCells = Array.Empty<int[]>();
        return 0;
    }

    /// <summary>
    /// Computer move: minimax + alpha-beta at depth 2 / 4 / 7 (easy / medium /
    /// hard) over a window-count heuristic that favours the centre column.
    /// Returns -1 when the board is full.
    /// </summary>
    public static int BestMove(ConnectFourState s, int side, string? difficulty, Random? rng = null)
    {
        rng ??= Random.Shared;
        var depth = (difficulty ?? "medium").ToLowerInvariant() switch
        {
            "easy" => 2,
            "hard" => 7,
            _ => 4,
        };
        var legal = LegalMoves(s.Cells).ToArray();
        if (legal.Length == 0) return -1;
        if (depth == 2 && rng.NextDouble() < 0.25)
            return legal[rng.Next(legal.Length)];   // easy blunders sometimes

        var best = int.MinValue;
        var choices = new List<int>();
        foreach (var col in legal)
        {
            var row = DropRow(s.Cells, col);
            s.Cells[row][col] = side;
            var score = -Negamax(s.Cells, 3 - side, depth - 1, int.MinValue + 1, int.MaxValue - 1);
            s.Cells[row][col] = 0;
            if (score > best) { best = score; choices.Clear(); choices.Add(col); }
            else if (score == best) choices.Add(col);
        }
        return choices[rng.Next(choices.Count)];
    }

    private const int WinScore = 100_000;

    private static int Negamax(int[][] cells, int side, int depth, int alpha, int beta)
    {
        var w = Winner(cells, out _);
        if (w != 0) return w == side ? WinScore + depth : -(WinScore + depth);
        if (IsFull(cells)) return 0;
        if (depth == 0) return Evaluate(cells, side);

        var best = int.MinValue + 1;
        foreach (var col in LegalMoves(cells))
        {
            var row = DropRow(cells, col);
            cells[row][col] = side;
            var score = -Negamax(cells, 3 - side, depth - 1, -beta, -alpha);
            cells[row][col] = 0;
            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }

    /// <summary>Heuristic from <paramref name="side"/>'s view: every 4-window counts.</summary>
    private static int Evaluate(int[][] cells, int side)
    {
        var score = 0;
        var opp = 3 - side;
        // centre column preference
        for (var r = 0; r < R; r++)
        {
            if (cells[r][3] == side) score += 3;
            else if (cells[r][3] == opp) score -= 3;
        }
        for (var r = 0; r < R; r++)
            for (var c = 0; c < C; c++)
                foreach (var (dr, dc) in Dirs)
                {
                    int er = r + 3 * dr, ec = c + 3 * dc;
                    if (er < 0 || er >= R || ec < 0 || ec >= C) continue;
                    int mine = 0, theirs = 0;
                    for (var k = 0; k < 4; k++)
                    {
                        var v = cells[r + k * dr][c + k * dc];
                        if (v == side) mine++;
                        else if (v == opp) theirs++;
                    }
                    if (mine > 0 && theirs > 0) continue;
                    score += mine switch { 3 => 50, 2 => 10, _ => 0 };
                    score -= theirs switch { 3 => 60, 2 => 10, _ => 0 };
                }
        return score;
    }
}
