using System.Diagnostics;

namespace peeposredemption.Application.Games;

/// <summary>
/// Computer chess via a Stockfish binary (UCI). One short-lived process per
/// move — simple, no shared state, and a crash can't take the app with it.
/// Missing binary / timeout / garbage → a random legal move, never an error,
/// so dev boxes without Stockfish still work (weakly).
/// </summary>
public sealed class StockfishService
{
    public const string DefaultPath = "/usr/games/stockfish";
    private readonly string _path;
    private readonly TimeSpan _timeout;
    private readonly Random _rng = new();

    public StockfishService(string? path = null, TimeSpan? timeout = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public bool IsAvailable => File.Exists(_path);

    /// <summary>Search depth per difficulty. Easy also blunders 30% of the time.</summary>
    public static int Depth(string? difficulty) => (difficulty ?? "medium").ToLowerInvariant() switch
    {
        "easy" => 1,
        "hard" => 14,
        _ => 6,
    };

    /// <summary>Best move as UCI for the side to move in <paramref name="state"/>, or null if none.</summary>
    public async Task<string?> BestMoveAsync(ChessState state, string? difficulty, CancellationToken ct = default)
    {
        var legal = ChessEngine.LegalUci(ChessEngine.Board(state));
        if (legal.Count == 0) return null;
        var d = (difficulty ?? "medium").ToLowerInvariant();
        if (d == "easy" && _rng.NextDouble() < 0.30) return legal[_rng.Next(legal.Count)];
        if (!IsAvailable) return legal[_rng.Next(legal.Count)];

        try
        {
            var uci = await RunAsync(state.Fen, Depth(d), ct);
            if (uci != null && legal.Contains(uci)) return uci;
        }
        catch
        {
            // fall through to the random move
        }
        return legal[_rng.Next(legal.Count)];
    }

    private async Task<string?> RunAsync(string fen, int depth, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(_path)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc is null) return null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);
        try
        {
            await proc.StandardInput.WriteLineAsync("uci");
            await proc.StandardInput.WriteLineAsync("isready");
            await proc.StandardInput.WriteLineAsync($"position fen {fen}");
            await proc.StandardInput.WriteLineAsync($"go depth {depth}");
            await proc.StandardInput.FlushAsync();
            while (!cts.IsCancellationRequested)
            {
                var line = await proc.StandardOutput.ReadLineAsync(cts.Token);
                if (line is null) break;
                if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var mv = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : null;
                    return mv == "(none)" ? null : mv;
                }
            }
            return null;
        }
        finally
        {
            try { if (!proc.HasExited) { await proc.StandardInput.WriteLineAsync("quit"); proc.Kill(true); } } catch { }
        }
    }
}
