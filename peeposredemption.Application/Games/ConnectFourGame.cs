using peeposredemption.Domain.Entities.Games;

namespace peeposredemption.Application.Games;

public sealed class ConnectFourGame : IBoardGame
{
    public string Key => GameKeys.Connect4;
    public bool SupportsDrawOffers => false;
    public string InitialState() => ConnectFourState.New().ToJson();

    public string Apply(string stateJson, string move, GameSide side, out MoveOutcome outcome)
    {
        if (!ConnectFourEngine.TryParseMove(move, out var col)) throw new ArgumentException("Pick a column 0-6.");
        var s = ConnectFourState.FromJson(stateJson);
        ConnectFourEngine.Apply(s, col, (int)side);
        var w = ConnectFourEngine.Winner(s.Cells, out _);
        outcome = w != 0 ? MoveOutcome.Won((GameSide)w, GameEndReason.Line)
                : ConnectFourEngine.IsFull(s.Cells) ? MoveOutcome.Draw(GameEndReason.BoardFull)
                : MoveOutcome.Ongoing;
        return s.ToJson();
    }

    public Task<string?> ComputerMoveAsync(string stateJson, GameSide side, string? difficulty, CancellationToken ct = default)
    {
        var col = ConnectFourEngine.BestMove(ConnectFourState.FromJson(stateJson), (int)side, difficulty);
        return Task.FromResult(col < 0 ? null : col.ToString());
    }

    public object Board(string stateJson, bool includeLegalMoves)
    {
        var s = ConnectFourState.FromJson(stateJson);
        ConnectFourEngine.Winner(s.Cells, out var win);
        return new Connect4BoardDto(ConnectFourState.Rows, ConnectFourState.Cols, s.Cells,
                                    s.Moves.Count > 0 ? s.Moves[^1] : null, win);
    }
}
