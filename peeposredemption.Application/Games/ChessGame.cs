using peeposredemption.Domain.Entities.Games;

namespace peeposredemption.Application.Games;

/// <summary>Chess as an <see cref="IBoardGame"/>: rules via ChessEngine (Gera.Chess), computer via Stockfish.</summary>
public sealed class ChessGame : IBoardGame
{
    private readonly StockfishService _stockfish;
    public ChessGame(StockfishService stockfish) => _stockfish = stockfish;

    public string Key => GameKeys.Chess;
    public bool SupportsDrawOffers => true;
    public string InitialState() => ChessState.New().ToJson();

    public string Apply(string stateJson, string move, GameSide side, out MoveOutcome outcome)
    {
        var s = ChessState.FromJson(stateJson);
        ChessEngine.ApplyUci(s, move ?? string.Empty, (int)side);
        var board = ChessEngine.Board(s);
        outcome = ChessEngine.Outcome(board, out var winner) switch
        {
            ChessOutcome.Checkmate => MoveOutcome.Won((GameSide)winner, GameEndReason.Checkmate),
            ChessOutcome.Stalemate => MoveOutcome.Draw(GameEndReason.Stalemate),
            ChessOutcome.InsufficientMaterial => MoveOutcome.Draw(GameEndReason.InsufficientMaterial),
            ChessOutcome.FiftyMoveRule => MoveOutcome.Draw(GameEndReason.FiftyMoveRule),
            ChessOutcome.Repetition => MoveOutcome.Draw(GameEndReason.Repetition),
            _ => MoveOutcome.Ongoing,
        };
        return s.ToJson();
    }

    public Task<string?> ComputerMoveAsync(string stateJson, GameSide side, string? difficulty, CancellationToken ct = default)
        => _stockfish.BestMoveAsync(ChessState.FromJson(stateJson), difficulty, ct);

    public object Board(string stateJson, bool includeLegalMoves)
    {
        var s = ChessState.FromJson(stateJson);
        var b = ChessEngine.Board(s);
        var (byP1, byP2) = ChessEngine.Captured(b);
        MoveSquares? last = s.LastUci is { Length: >= 4 } u ? new MoveSquares(u[..2], u[2..4]) : null;
        var legal = includeLegalMoves ? ChessEngine.LegalUci(b) : new List<string>();
        return new ChessBoardDto(s.Fen, s.Moves, last, ChessEngine.InCheck(b), legal, byP1, byP2, ChessEngine.Pgn(b));
    }
}
