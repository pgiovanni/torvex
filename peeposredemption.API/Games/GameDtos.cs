using peeposredemption.Application.Games;
namespace peeposredemption.API.Games;

// Wire shapes — docs/GAMES-HUB.md is the contract. camelCase via the default JSON options.

public record PlayerRef(Guid? UserId, string Name, string? AvatarUrl);

public record RatingCard(string Game, int Rating, int Peak, int Games, int Wins, int Losses, int Draws,
                         bool Provisional, int? Rank);

public record LeaderRow(int Rank, PlayerRef Player, int Rating, int Games, int Wins, int Losses, int Draws, bool Provisional);

public class MatchSummary
{
    public Guid Id { get; set; }
    public string Game { get; set; } = string.Empty;
    public string Status { get; set; } = "open";
    public bool Rated { get; set; }
    public bool VsComputer { get; set; }
    public string? Difficulty { get; set; }
    public PlayerRef? P1 { get; set; }
    public PlayerRef? P2 { get; set; }
    public string? YouAre { get; set; }
    public string? Turn { get; set; }
    public string? Winner { get; set; }
    public bool IsDraw { get; set; }
    public string EndReason { get; set; } = "none";
    public int? RatingDeltaP1 { get; set; }
    public int? RatingDeltaP2 { get; set; }
    public int MoveCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMoveAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public sealed class MatchState : MatchSummary
{
    public string? DrawOfferBy { get; set; }
    public object Board { get; set; } = new { };
}


public record CreateMatchRequest(string Game, bool Rated, bool VsComputer, string? Difficulty, string? Seat);
public record MoveRequest(string Move);
public record DrawRequest(string Action);

public record WordleGuess(string Word, string Marks);
public record WordleState(Guid Id, string Mode, string? Date, int MaxGuesses, List<WordleGuess> Guesses,
                          bool Finished, bool Solved, string? Answer, bool Hard, int Streak);
public record WordleGuessRequest(string Word, bool? Practice, bool? Hard);
public record WordleStats(int Played, int Solved, int Streak, int MaxStreak, int[] Distribution);
public record WordleLeaderRow(PlayerRef Player, int Solved, int Played, double AvgGuesses, int Streak);
public record PlayerPage(PlayerRef Player, RatingCard? Rating, List<MatchSummary> Recent);
public record MeResponse(List<RatingCard> Ratings, List<MatchSummary> Active, List<MatchSummary> Open, List<MatchSummary> Recent);

/// <summary>Thrown by the services for user-facing failures; the endpoints map it to a status code.</summary>
public sealed class GameApiException : Exception
{
    public int Status { get; }
    public GameApiException(int status, string message) : base(message) => Status = status;
}
