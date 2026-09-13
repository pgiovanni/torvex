namespace peeposredemption.Application.Games;

/// <summary>
/// Plain Elo with a lichess-style K ladder: 40 while a player has fewer than
/// 30 rated games, 20 after, 10 once they sit at 2400+. Draw scores 0.5.
/// Ratings never drop below <see cref="Floor"/>.
/// </summary>
public static class Elo
{
    public const int Start = 1200;
    public const int Floor = 100;

    public static double Expected(int rating, int opponent)
        => 1.0 / (1.0 + Math.Pow(10.0, (opponent - rating) / 400.0));

    public static int KFactor(int rating, int gamesPlayed)
    {
        if (rating >= 2400) return 10;
        return gamesPlayed < 30 ? 40 : 20;
    }

    /// <summary>
    /// Rating change for one player. <paramref name="score"/> = 1 win, 0.5 draw, 0 loss.
    /// Rounded to the nearest point; a win never yields less than +1 and a loss
    /// never more than -1 so the result is always visible.
    /// </summary>
    public static int Delta(int rating, int gamesPlayed, int opponentRating, double score)
    {
        var k = KFactor(rating, gamesPlayed);
        var raw = k * (score - Expected(rating, opponentRating));
        var delta = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        if (score > 0.5 && delta < 1) delta = 1;
        if (score < 0.5 && delta > -1) delta = -1;
        if (rating + delta < Floor) delta = Floor - rating;
        return delta;
    }

    /// <summary>Both deltas at once. scoreA is from A's point of view.</summary>
    public static (int deltaA, int deltaB) Pair(int ratingA, int gamesA, int ratingB, int gamesB, double scoreA)
        => (Delta(ratingA, gamesA, ratingB, scoreA), Delta(ratingB, gamesB, ratingA, 1.0 - scoreA));
}
