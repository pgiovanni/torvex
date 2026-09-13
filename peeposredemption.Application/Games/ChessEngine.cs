using System.Text.Json;
using Chess;

namespace peeposredemption.Application.Games;

/// <summary>
/// Chess position as stored on the match: the current FEN (for display) plus
/// the SAN move list (the source of truth — the board is rebuilt by replaying
/// it so repetition and the fifty-move rule work).
/// </summary>
public sealed class ChessState
{
    public string Fen { get; set; } = StartFen;
    public List<string> Moves { get; set; } = new();
    /// <summary>UCI of the last move ("e2e4") so the page can highlight it.</summary>
    public string? LastUci { get; set; }

    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static ChessState New() => new();
    public static ChessState FromJson(string json)
        => string.IsNullOrWhiteSpace(json) || json == "{}" ? New()
           : JsonSerializer.Deserialize<ChessState>(json, ChessEngine.JsonOpts) ?? New();
    public string ToJson() => JsonSerializer.Serialize(this, ChessEngine.JsonOpts);
}

public enum ChessOutcome
{
    Ongoing,
    Checkmate,
    Stalemate,
    InsufficientMaterial,
    FiftyMoveRule,
    Repetition,
}

/// <summary>Thin adapter over Gera.Chess (NuGet) — the only place that library is touched.</summary>
public static class ChessEngine
{
    internal static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Rebuild the live board from the SAN list.</summary>
    public static ChessBoard Board(ChessState s)
    {
        var b = new ChessBoard { AutoEndgameRules = AutoEndgameRules.All };
        foreach (var san in s.Moves) b.Move(san);
        return b;
    }

    /// <summary>1 = white to move (P1), 2 = black (P2).</summary>
    public static int SideToMove(ChessState s) => s.Moves.Count % 2 == 0 ? 1 : 2;

    public static bool TryParseUci(string? text, out string from, out string to, out char? promo)
    {
        from = to = string.Empty; promo = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToLowerInvariant();
        if (t.Length is < 4 or > 5) return false;
        if (!IsSquare(t[..2]) || !IsSquare(t[2..4])) return false;
        if (t.Length == 5)
        {
            if ("qrbn".IndexOf(t[4]) < 0) return false;
            promo = t[4];
        }
        from = t[..2]; to = t[2..4];
        return true;
    }

    private static bool IsSquare(string sq) => sq.Length == 2 && sq[0] is >= 'a' and <= 'h' && sq[1] is >= '1' and <= '8';

    private static PromotionType PromoOf(char? c) => c switch
    {
        'q' => PromotionType.ToQueen,
        'r' => PromotionType.ToRook,
        'b' => PromotionType.ToBishop,
        'n' => PromotionType.ToKnight,
        _ => PromotionType.Default,
    };

    private static char PromoChar(PromotionType t) => t switch
    {
        PromotionType.ToRook => 'r',
        PromotionType.ToBishop => 'b',
        PromotionType.ToKnight => 'n',
        _ => 'q',
    };

    /// <summary>All legal moves as UCI (promotions expanded: e7e8q, e7e8r, …).</summary>
    public static List<string> LegalUci(ChessBoard b)
    {
        var list = new List<string>();
        if (b.IsEndGame) return list;
        foreach (var m in b.Moves(false, false))
        {
            var uci = m.OriginalPosition.ToString() + m.NewPosition.ToString();
            if (m.IsPromotion)
            {
                var pt = (m.Parameter as MovePromotion)?.PromotionType ?? PromotionType.Default;
                uci += PromoChar(pt);
            }
            list.Add(uci);
        }
        return list;
    }

    /// <summary>
    /// Plays a UCI move for <paramref name="side"/>. Returns the SAN played.
    /// Throws ArgumentException on an illegal move, InvalidOperationException
    /// when it isn't that side's turn or the game is over.
    /// </summary>
    public static string ApplyUci(ChessState s, string uci, int side)
    {
        if (side != SideToMove(s)) throw new InvalidOperationException("Not your turn.");
        if (!TryParseUci(uci, out var from, out var to, out var promo))
            throw new ArgumentException("Moves look like e2e4 or e7e8q.");
        var b = Board(s);
        if (b.IsEndGame) throw new InvalidOperationException("The game is over.");

        Move? chosen = null;
        foreach (var m in b.Moves(new Position(from), false, true))
        {
            if (m.NewPosition.ToString() != to) continue;
            if (m.IsPromotion)
            {
                var pt = (m.Parameter as MovePromotion)?.PromotionType ?? PromotionType.ToQueen;
                if (pt != PromoOf(promo ?? 'q')) continue;
            }
            chosen = m;
            break;
        }
        if (chosen is null) throw new ArgumentException("Illegal move.");
        var san = chosen.San ?? b.ParseToSan(chosen);
        if (!b.Move(san)) throw new ArgumentException("Illegal move.");
        s.Moves.Add(san);
        s.Fen = b.ToFen();
        s.LastUci = from + to + (promo?.ToString() ?? string.Empty);
        return san;
    }

    public static ChessOutcome Outcome(ChessBoard b, out int winner)
    {
        winner = 0;
        if (!b.IsEndGame || b.EndGame is null) return ChessOutcome.Ongoing;
        var eg = b.EndGame;
        if (eg.EndgameType == EndgameType.Checkmate)
        {
            winner = eg.WonSide == PieceColor.White ? 1 : eg.WonSide == PieceColor.Black ? 2 : 0;
            return ChessOutcome.Checkmate;
        }
        return eg.EndgameType switch
        {
            EndgameType.Stalemate => ChessOutcome.Stalemate,
            EndgameType.InsufficientMaterial => ChessOutcome.InsufficientMaterial,
            EndgameType.FiftyMoveRule => ChessOutcome.FiftyMoveRule,
            EndgameType.Repetition => ChessOutcome.Repetition,
            _ => ChessOutcome.Ongoing,
        };
    }

    public static bool InCheck(ChessBoard b) => b.WhiteKingChecked || b.BlackKingChecked;

    /// <summary>Pieces P1 (white) has captured = the black pieces off the board, as FEN chars.</summary>
    public static (List<string> byP1, List<string> byP2) Captured(ChessBoard b)
    {
        var byP1 = b.CapturedBlack.Select(p => p.ToFenChar().ToString()).ToList();
        var byP2 = b.CapturedWhite.Select(p => p.ToFenChar().ToString()).ToList();
        return (byP1, byP2);
    }

    public static string Pgn(ChessBoard b)
    {
        try { return b.ToPgn(); }
        catch { return string.Join(" ", b.MovesToSan); }
    }

    /// <summary>A uniformly random legal move (fallback when Stockfish is unavailable).</summary>
    public static string? RandomUci(ChessState s, Random? rng = null)
    {
        var legal = LegalUci(Board(s));
        if (legal.Count == 0) return null;
        return legal[(rng ?? Random.Shared).Next(legal.Count)];
    }
}
