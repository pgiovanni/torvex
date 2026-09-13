using peeposredemption.Domain.Entities.Games;

namespace peeposredemption.Application.Games;

/// <summary>How a move left the match: still going, or ended with a winner / a draw.</summary>
public readonly record struct MoveOutcome(GameEndReason Reason, GameSide? Winner, bool IsDraw)
{
    public static readonly MoveOutcome Ongoing = new(GameEndReason.None, null, false);
    public bool Ended => Reason != GameEndReason.None;
    public static MoveOutcome Won(GameSide side, GameEndReason reason) => new(reason, side, false);
    public static MoveOutcome Draw(GameEndReason reason) => new(reason, null, true);
}

/// <summary>
/// One two-player board game as the match service sees it. The service owns
/// persistence, seats, ratings and the wire format; a game owns nothing but its
/// rules. Adding a game = one class implementing this + a JS board renderer
/// (docs/GAMES-HUB.md). State is an opaque JSON string the game round-trips.
/// </summary>
public interface IBoardGame
{
    /// <summary>Stable key stored on GameMatch.Game and used in URLs ("chess").</summary>
    string Key { get; }
    /// <summary>Whether players may offer / accept draws (chess yes, connect4 no).</summary>
    bool SupportsDrawOffers { get; }

    string InitialState();

    /// <summary>
    /// Applies <paramref name="move"/> for <paramref name="side"/> and returns the new
    /// state. Throws <see cref="ArgumentException"/> for an unparseable move and
    /// <see cref="InvalidOperationException"/> for an illegal one.
    /// </summary>
    string Apply(string stateJson, string move, GameSide side, out MoveOutcome outcome);

    /// <summary>The computer's reply for <paramref name="side"/>, or null if it has none.</summary>
    Task<string?> ComputerMoveAsync(string stateJson, GameSide side, string? difficulty, CancellationToken ct = default);

    /// <summary>The board as the client renders it (one of the records in BoardDtos.cs).</summary>
    object Board(string stateJson, bool includeLegalMoves);
}

/// <summary>Registry of the board games the server knows, keyed by <see cref="IBoardGame.Key"/>.</summary>
public sealed class GameRegistry
{
    private readonly IReadOnlyDictionary<string, IBoardGame> _games;
    public GameRegistry(IEnumerable<IBoardGame> games)
        => _games = games.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> Keys => _games.Keys;
    public bool Contains(string? key) => key is not null && _games.ContainsKey(key);
    public IBoardGame this[string key] => _games.TryGetValue(key, out var g)
        ? g : throw new KeyNotFoundException($"Unknown game '{key}'.");
    public bool TryGet(string? key, out IBoardGame game)
    {
        if (key is not null && _games.TryGetValue(key, out var g)) { game = g; return true; }
        game = null!;
        return false;
    }
}
