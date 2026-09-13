namespace peeposredemption.Application.Games;

// Wire shapes for MatchState.board (docs/GAMES-HUB.md). Each IBoardGame returns
// one of these from Board(); the API serialises them camelCase as-is.
public record MoveSquares(string From, string To);
public record ChessBoardDto(string Fen, List<string> Moves, MoveSquares? LastMove, bool Check, List<string> Legal,
                            List<string> CapturedByP1, List<string> CapturedByP2, string Pgn);
public record Connect4BoardDto(int Rows, int Cols, int[][] Cells, int? LastCol, int[][] WinningCells);
public record TicTacToeBoardDto(int[] Cells, int[] WinningCells);
