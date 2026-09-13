namespace peeposredemption.Domain.Entities.Games;

public enum GameMatchStatus
{
    /// <summary>A challenge waiting in the lobby for a second player.</summary>
    Open = 0,
    Active = 1,
    Finished = 2,
    /// <summary>Cancelled before it started (creator withdrew, or expired).</summary>
    Aborted = 3,
}

public enum GameSide
{
    /// <summary>Player 1 — white in chess, first to move everywhere.</summary>
    P1 = 1,
    P2 = 2,
}

public enum GameEndReason
{
    None = 0,
    Checkmate = 1,
    Stalemate = 2,
    Resigned = 3,
    DrawAgreed = 4,
    InsufficientMaterial = 5,
    FiftyMoveRule = 6,
    Repetition = 7,
    /// <summary>Connect Four / Tic-Tac-Toe: a line was completed.</summary>
    Line = 8,
    /// <summary>Board full with no line.</summary>
    BoardFull = 9,
    Timeout = 10,
    Aborted = 11,
}

/// <summary>
/// One match of a two-player board game. The engine-specific position lives in
/// <see cref="StateJson"/> (chess: FEN + SAN move list; connect4/tictactoe: the
/// cell array + move list) — the API layer's engines own that shape, the DB
/// just stores it. Rated matches write Elo deltas back here at finish so the
/// history page can show "+12 / -12" without recomputing.
/// </summary>
public class GameMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>"chess", "connect4", "tictactoe".</summary>
    public string Game { get; set; } = string.Empty;
    public GameMatchStatus Status { get; set; } = GameMatchStatus.Open;
    public bool Rated { get; set; }

    /// <summary>The creator's preferred side while Open; the actual seat once Active.</summary>
    public Guid? Player1Id { get; set; }
    public User? Player1 { get; set; }
    public Guid? Player2Id { get; set; }
    public User? Player2 { get; set; }
    /// <summary>Who created the challenge (always one of the two players).</summary>
    public Guid CreatorId { get; set; }
    /// <summary>Creator asked for a specific seat: "p1", "p2" or "random" (resolved at join).</summary>
    public string CreatorSeat { get; set; } = "random";

    /// <summary>Computer opponent. The engine plays the seat that has no user.</summary>
    public bool VsComputer { get; set; }
    /// <summary>"easy" | "medium" | "hard" when VsComputer.</summary>
    public string? Difficulty { get; set; }

    public string StateJson { get; set; } = "{}";
    /// <summary>Whose turn it is; null once finished.</summary>
    public GameSide? Turn { get; set; }
    public int MoveCount { get; set; }
    /// <summary>Set when a player offers a draw (chess); cleared by any move.</summary>
    public GameSide? DrawOfferBy { get; set; }

    public GameSide? Winner { get; set; }
    public bool IsDraw { get; set; }
    public GameEndReason EndReason { get; set; } = GameEndReason.None;
    public int? RatingDeltaP1 { get; set; }
    public int? RatingDeltaP2 { get; set; }
    /// <summary>Ratings at the start of the match, for the history card.</summary>
    public int? RatingP1Before { get; set; }
    public int? RatingP2Before { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? LastMoveAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
