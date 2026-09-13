using peeposredemption.Domain.Entities.Games;

namespace peeposredemption.Application.Games;

public sealed class TicTacToeGame : IBoardGame
{
    public string Key => GameKeys.TicTacToe;
    public bool SupportsDrawOffers => false;
    public string InitialState() => TicTacToeState.New().ToJson();

    public string Apply(string stateJson, string move, GameSide side, out MoveOutcome outcome)
    {
        if (!TicTacToeEngine.TryParseMove(move, out var cell)) throw new ArgumentException("Pick a square 0-8.");
        var s = TicTacToeState.FromJson(stateJson);
        TicTacToeEngine.Apply(s, cell, (int)side);
        var w = TicTacToeEngine.Winner(s.Cells, out _);
        outcome = w != 0 ? MoveOutcome.Won((GameSide)w, GameEndReason.Line)
                : TicTacToeEngine.IsFull(s.Cells) ? MoveOutcome.Draw(GameEndReason.BoardFull)
                : MoveOutcome.Ongoing;
        return s.ToJson();
    }

    public Task<string?> ComputerMoveAsync(string stateJson, GameSide side, string? difficulty, CancellationToken ct = default)
    {
        var cell = TicTacToeEngine.BestMove(TicTacToeState.FromJson(stateJson), (int)side, difficulty);
        return Task.FromResult(cell < 0 ? null : cell.ToString());
    }

    public object Board(string stateJson, bool includeLegalMoves)
    {
        var s = TicTacToeState.FromJson(stateJson);
        TicTacToeEngine.Winner(s.Cells, out var win);
        return new TicTacToeBoardDto(s.Cells, win);
    }
}
