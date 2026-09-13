using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using peeposredemption.Application.Games;
using peeposredemption.Domain.Entities;
using peeposredemption.Domain.Entities.Games;
using peeposredemption.Infrastructure.Persistence;

namespace peeposredemption.API.Games;

/// <summary>
/// Everything a two-player board match does: lobby, seats, moves, computer
/// replies, resign/draw, and Elo on finish. Rules live in the Application
/// engines; this class owns persistence and the wire DTOs.
/// </summary>
public sealed class GameMatchService
{
    private readonly AppDbContext _db;
    private readonly StockfishService _stockfish;
    private readonly ILogger<GameMatchService> _log;

    // One lock per match so two simultaneous move requests can't both pass the
    // turn check (single-process app; good enough without a DB concurrency token).
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public const int MaxActivePerUser = 10;

    public GameMatchService(AppDbContext db, StockfishService stockfish, ILogger<GameMatchService> log)
    {
        _db = db;
        _stockfish = stockfish;
        _log = log;
    }

    // ── queries ──────────────────────────────────────────────────────────────

    public async Task<MeResponse> MeAsync(Guid me)
    {
        var ratings = await _db.GameRatings.Where(r => r.UserId == me).ToListAsync();
        var cards = new List<RatingCard>();
        foreach (var r in ratings) cards.Add(await CardAsync(r));

        var active = await MatchQuery()
            .Where(m => m.Status == GameMatchStatus.Active && (m.Player1Id == me || m.Player2Id == me))
            .OrderByDescending(m => m.LastMoveAt ?? m.StartedAt ?? m.CreatedAt)
            .ToListAsync();
        var open = await MatchQuery()
            .Where(m => m.Status == GameMatchStatus.Open && m.CreatorId == me)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        var recent = await MatchQuery()
            .Where(m => m.Status == GameMatchStatus.Finished && (m.Player1Id == me || m.Player2Id == me))
            .OrderByDescending(m => m.FinishedAt)
            .Take(10)
            .ToListAsync();
        return new MeResponse(cards, active.Select(m => Summary(m, me)).ToList(),
                              open.Select(m => Summary(m, me)).ToList(),
                              recent.Select(m => Summary(m, me)).ToList());
    }

    public async Task<List<LeaderRow>> LeaderboardAsync(string game, int limit)
    {
        if (!GameKeys.IsBoardGame(game)) throw new GameApiException(400, "Unknown game.");
        limit = Math.Clamp(limit, 1, 200);
        // Everyone with a rated game is listed so a small community never stares at
        // an empty board: established players (10+ rated games) rank first by
        // rating, provisional players follow, flagged so the UI can show the "?".
        var rows = await _db.GameRatings.Include(r => r.User)
            .Where(r => r.Game == game && r.Games >= 1)
            .OrderBy(r => r.Games >= GameRating.ProvisionalGames ? 0 : 1)
            .ThenByDescending(r => r.Rating).ThenByDescending(r => r.Games).ThenBy(r => r.UpdatedAt)
            .Take(limit)
            .ToListAsync();
        return rows.Select((r, i) => new LeaderRow(i + 1, Ref(r.User), r.Rating, r.Games, r.Wins, r.Losses, r.Draws, r.IsProvisional)).ToList();
    }

    public async Task<PlayerPage?> PlayerAsync(Guid userId, string? game, Guid me)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;
        game = string.IsNullOrWhiteSpace(game) ? GameKeys.Chess : game;
        var rating = await _db.GameRatings.FirstOrDefaultAsync(r => r.UserId == userId && r.Game == game);
        var recent = await MatchQuery()
            .Where(m => m.Game == game && m.Status == GameMatchStatus.Finished && (m.Player1Id == userId || m.Player2Id == userId))
            .OrderByDescending(m => m.FinishedAt).Take(20).ToListAsync();
        return new PlayerPage(Ref(user), rating is null ? null : await CardAsync(rating),
                              recent.Select(m => Summary(m, me)).ToList());
    }

    public async Task<List<MatchSummary>> OpenAsync(string? game, Guid me)
    {
        var q = MatchQuery().Where(m => m.Status == GameMatchStatus.Open && !m.VsComputer && m.CreatorId != me);
        if (!string.IsNullOrWhiteSpace(game)) q = q.Where(m => m.Game == game);
        var list = await q.OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync();
        return list.Select(m => Summary(m, me)).ToList();
    }

    public async Task<List<MatchSummary>> HistoryAsync(Guid me, string? game, int limit)
    {
        limit = Math.Clamp(limit, 1, 100);
        var q = MatchQuery().Where(m => m.Status == GameMatchStatus.Finished && (m.Player1Id == me || m.Player2Id == me));
        if (!string.IsNullOrWhiteSpace(game)) q = q.Where(m => m.Game == game);
        var list = await q.OrderByDescending(m => m.FinishedAt).Take(limit).ToListAsync();
        return list.Select(m => Summary(m, me)).ToList();
    }

    public async Task<MatchState> GetAsync(Guid id, Guid me)
    {
        var m = await LoadAsync(id);
        return State(m, me);
    }

    // ── lobby ────────────────────────────────────────────────────────────────

    public async Task<MatchState> CreateAsync(Guid me, CreateMatchRequest req)
    {
        var game = (req.Game ?? string.Empty).Trim().ToLowerInvariant();
        if (!GameKeys.IsBoardGame(game)) throw new GameApiException(400, "Unknown game.");
        var seat = (req.Seat ?? "random").Trim().ToLowerInvariant();
        if (seat is not ("p1" or "p2" or "random")) throw new GameApiException(400, "Seat must be p1, p2 or random.");
        var difficulty = (req.Difficulty ?? "medium").Trim().ToLowerInvariant();
        if (req.VsComputer && Array.IndexOf(GameKeys.Difficulties, difficulty) < 0)
            throw new GameApiException(400, "Difficulty must be easy, medium or hard.");

        var activeCount = await _db.GameMatches.CountAsync(m =>
            m.Status == GameMatchStatus.Active && (m.Player1Id == me || m.Player2Id == me));
        if (activeCount >= MaxActivePerUser)
            throw new GameApiException(400, $"You already have {MaxActivePerUser} games going — finish one first.");

        var match = new GameMatch
        {
            Game = game,
            CreatorId = me,
            CreatorSeat = seat,
            Rated = req.Rated && !req.VsComputer,
            VsComputer = req.VsComputer,
            Difficulty = req.VsComputer ? difficulty : null,
            StateJson = InitialState(game),
        };

        if (req.VsComputer)
        {
            var mySeat = seat == "random" ? (Random.Shared.Next(2) == 0 ? "p1" : "p2") : seat;
            if (mySeat == "p1") match.Player1Id = me; else match.Player2Id = me;
            match.Status = GameMatchStatus.Active;
            match.StartedAt = DateTime.UtcNow;
            match.Turn = GameSide.P1;
            _db.GameMatches.Add(match);
            await _db.SaveChangesAsync();
            if (mySeat == "p2")
            {
                await ComputerTurnsAsync(match);
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            var mine = await _db.GameMatches.AnyAsync(m => m.Status == GameMatchStatus.Open && m.CreatorId == me && m.Game == game);
            if (mine) throw new GameApiException(400, "You already have an open challenge for this game.");
            // provisional seat while Open; a random seat is resolved when someone joins
            if (seat == "p2") match.Player2Id = me; else match.Player1Id = me;
            match.Status = GameMatchStatus.Open;
            _db.GameMatches.Add(match);
            await _db.SaveChangesAsync();
        }
        return await GetAsync(match.Id, me);
    }

    public async Task<MatchState> JoinAsync(Guid id, Guid me)
    {
        var sem = LockFor(id);
        await sem.WaitAsync();
        try
        {
            var m = await LoadAsync(id);
            if (m.Status != GameMatchStatus.Open) throw new GameApiException(400, "That challenge isn't open any more.");
            if (m.VsComputer) throw new GameApiException(400, "That's a computer game.");
            if (m.CreatorId == me) throw new GameApiException(400, "You can't join your own challenge.");

            var activeCount = await _db.GameMatches.CountAsync(x =>
                x.Status == GameMatchStatus.Active && (x.Player1Id == me || x.Player2Id == me));
            if (activeCount >= MaxActivePerUser)
                throw new GameApiException(400, $"You already have {MaxActivePerUser} games going — finish one first.");

            var creatorSeat = m.CreatorSeat == "random"
                ? (Random.Shared.Next(2) == 0 ? "p1" : "p2")
                : m.CreatorSeat;
            if (creatorSeat == "p1") { m.Player1Id = m.CreatorId; m.Player2Id = me; }
            else { m.Player2Id = m.CreatorId; m.Player1Id = me; }

            m.Status = GameMatchStatus.Active;
            m.StartedAt = DateTime.UtcNow;
            m.Turn = GameSide.P1;
            if (m.Rated)
            {
                var r1 = await RatingAsync(m.Player1Id!.Value, m.Game, create: true);
                var r2 = await RatingAsync(m.Player2Id!.Value, m.Game, create: true);
                m.RatingP1Before = r1.Rating;
                m.RatingP2Before = r2.Rating;
            }
            await _db.SaveChangesAsync();
            return State(await LoadAsync(id), me);
        }
        finally { sem.Release(); }
    }

    public async Task CancelAsync(Guid id, Guid me)
    {
        var m = await LoadAsync(id);
        if (m.CreatorId != me) throw new GameApiException(403, "Only the challenger can cancel.");
        if (m.Status != GameMatchStatus.Open) throw new GameApiException(400, "Only open challenges can be cancelled.");
        m.Status = GameMatchStatus.Aborted;
        m.EndReason = GameEndReason.Aborted;
        m.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ── play ─────────────────────────────────────────────────────────────────

    public async Task<MatchState> MoveAsync(Guid id, Guid me, string? move)
    {
        var sem = LockFor(id);
        await sem.WaitAsync();
        try
        {
            var m = await LoadAsync(id);
            if (m.Status != GameMatchStatus.Active) throw new GameApiException(400, "This game isn't in progress.");
            var seat = SeatOf(m, me) ?? throw new GameApiException(403, "You're watching this one.");
            if (m.Turn != seat) throw new GameApiException(403, "Not your turn.");

            ApplyMove(m, move, seat);
            m.DrawOfferBy = null;
            if (m.Status == GameMatchStatus.Active && m.VsComputer) await ComputerTurnsAsync(m);
            await SettlePendingAsync();
            try { await _db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { throw new GameApiException(409, "The game changed under you — reload."); }
            return State(await LoadAsync(id), me);
        }
        finally { sem.Release(); }
    }

    public async Task<MatchState> ResignAsync(Guid id, Guid me)
    {
        var sem = LockFor(id);
        await sem.WaitAsync();
        try
        {
            var m = await LoadAsync(id);
            if (m.Status != GameMatchStatus.Active) throw new GameApiException(400, "This game isn't in progress.");
            var seat = SeatOf(m, me) ?? throw new GameApiException(403, "You're watching this one.");
            await FinishAsync(m, winner: seat == GameSide.P1 ? GameSide.P2 : GameSide.P1, draw: false, GameEndReason.Resigned);
            await _db.SaveChangesAsync();
            return State(await LoadAsync(id), me);
        }
        finally { sem.Release(); }
    }

    public async Task<MatchState> DrawAsync(Guid id, Guid me, string? action)
    {
        var sem = LockFor(id);
        await sem.WaitAsync();
        try
        {
            var m = await LoadAsync(id);
            if (m.Game != GameKeys.Chess) throw new GameApiException(400, "Draw offers are a chess thing.");
            if (m.Status != GameMatchStatus.Active) throw new GameApiException(400, "This game isn't in progress.");
            if (m.VsComputer) throw new GameApiException(400, "The computer never agrees to a draw.");
            var seat = SeatOf(m, me) ?? throw new GameApiException(403, "You're watching this one.");
            var other = seat == GameSide.P1 ? GameSide.P2 : GameSide.P1;
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "offer":
                    if (m.DrawOfferBy == seat) throw new GameApiException(400, "You've already offered a draw.");
                    if (m.DrawOfferBy == other)
                    {
                        await FinishAsync(m, null, true, GameEndReason.DrawAgreed);
                        break;
                    }
                    m.DrawOfferBy = seat;
                    break;
                case "accept":
                    if (m.DrawOfferBy != other) throw new GameApiException(400, "There's no draw offer to accept.");
                    await FinishAsync(m, null, true, GameEndReason.DrawAgreed);
                    break;
                case "decline":
                    if (m.DrawOfferBy != other) throw new GameApiException(400, "There's no draw offer to decline.");
                    m.DrawOfferBy = null;
                    break;
                default:
                    throw new GameApiException(400, "Action must be offer, accept or decline.");
            }
            await _db.SaveChangesAsync();
            return State(await LoadAsync(id), me);
        }
        finally { sem.Release(); }
    }

    // ── engine glue ──────────────────────────────────────────────────────────

    private static string InitialState(string game) => game switch
    {
        GameKeys.Chess => ChessState.New().ToJson(),
        GameKeys.Connect4 => ConnectFourState.New().ToJson(),
        GameKeys.TicTacToe => TicTacToeState.New().ToJson(),
        _ => "{}",
    };

    /// <summary>Applies one move for <paramref name="seat"/> and advances/finishes the match.</summary>
    private void ApplyMove(GameMatch m, string? move, GameSide seat)
    {
        var side = (int)seat;
        try
        {
            switch (m.Game)
            {
                case GameKeys.Chess:
                {
                    var s = ChessState.FromJson(m.StateJson);
                    ChessEngine.ApplyUci(s, move ?? string.Empty, side);
                    m.StateJson = s.ToJson();
                    var board = ChessEngine.Board(s);
                    var outcome = ChessEngine.Outcome(board, out var winner);
                    Advance(m);
                    switch (outcome)
                    {
                        case ChessOutcome.Checkmate: Finish(m, (GameSide)winner, false, GameEndReason.Checkmate); break;
                        case ChessOutcome.Stalemate: Finish(m, null, true, GameEndReason.Stalemate); break;
                        case ChessOutcome.InsufficientMaterial: Finish(m, null, true, GameEndReason.InsufficientMaterial); break;
                        case ChessOutcome.FiftyMoveRule: Finish(m, null, true, GameEndReason.FiftyMoveRule); break;
                        case ChessOutcome.Repetition: Finish(m, null, true, GameEndReason.Repetition); break;
                    }
                    break;
                }
                case GameKeys.Connect4:
                {
                    if (!ConnectFourEngine.TryParseMove(move, out var col)) throw new ArgumentException("Pick a column 0-6.");
                    var s = ConnectFourState.FromJson(m.StateJson);
                    ConnectFourEngine.Apply(s, col, side);
                    m.StateJson = s.ToJson();
                    Advance(m);
                    var w = ConnectFourEngine.Winner(s.Cells, out _);
                    if (w != 0) Finish(m, (GameSide)w, false, GameEndReason.Line);
                    else if (ConnectFourEngine.IsFull(s.Cells)) Finish(m, null, true, GameEndReason.BoardFull);
                    break;
                }
                case GameKeys.TicTacToe:
                {
                    if (!TicTacToeEngine.TryParseMove(move, out var cell)) throw new ArgumentException("Pick a square 0-8.");
                    var s = TicTacToeState.FromJson(m.StateJson);
                    TicTacToeEngine.Apply(s, cell, side);
                    m.StateJson = s.ToJson();
                    Advance(m);
                    var w = TicTacToeEngine.Winner(s.Cells, out _);
                    if (w != 0) Finish(m, (GameSide)w, false, GameEndReason.Line);
                    else if (TicTacToeEngine.IsFull(s.Cells)) Finish(m, null, true, GameEndReason.BoardFull);
                    break;
                }
                default:
                    throw new GameApiException(400, "Unknown game.");
            }
        }
        catch (ArgumentException e) { throw new GameApiException(400, e.Message); }
        catch (InvalidOperationException e) { throw new GameApiException(400, e.Message); }
    }

    private static void Advance(GameMatch m)
    {
        m.MoveCount++;
        m.LastMoveAt = DateTime.UtcNow;
        m.Turn = m.Turn == GameSide.P1 ? GameSide.P2 : GameSide.P1;
    }

    /// <summary>Let the computer play until it's a human's turn or the game ends.</summary>
    private async Task ComputerTurnsAsync(GameMatch m)
    {
        var guard = 0;
        while (m.Status == GameMatchStatus.Active && m.Turn is { } t && PlayerIdOf(m, t) is null && guard++ < 4)
        {
            string? mv = m.Game switch
            {
                GameKeys.Chess => await _stockfish.BestMoveAsync(ChessState.FromJson(m.StateJson), m.Difficulty),
                GameKeys.Connect4 => ConnectFourEngine.BestMove(ConnectFourState.FromJson(m.StateJson), (int)t, m.Difficulty).ToString(),
                GameKeys.TicTacToe => TicTacToeEngine.BestMove(TicTacToeState.FromJson(m.StateJson), (int)t, m.Difficulty).ToString(),
                _ => null,
            };
            if (string.IsNullOrEmpty(mv) || mv == "-1")
            {
                _log.LogWarning("Computer had no move in match {Id} ({Game}) — declaring a draw", m.Id, m.Game);
                Finish(m, null, true, GameEndReason.BoardFull);
                break;
            }
            ApplyMove(m, mv, t);
        }
    }

    // Sync finish for engine-detected ends; rated Elo needs the DB, so the
    // caller's SaveChanges path runs SettleRatingsAsync via FinishAsync when a
    // human ends the game, and via the pending flag otherwise.
    private void Finish(GameMatch m, GameSide? winner, bool draw, GameEndReason reason)
    {
        m.Status = GameMatchStatus.Finished;
        m.Turn = null;
        m.Winner = winner;
        m.IsDraw = draw;
        m.EndReason = reason;
        m.FinishedAt = DateTime.UtcNow;
        m.DrawOfferBy = null;
        _pendingSettle.Add(m);
    }

    private readonly List<GameMatch> _pendingSettle = new();

    private async Task FinishAsync(GameMatch m, GameSide? winner, bool draw, GameEndReason reason)
    {
        Finish(m, winner, draw, reason);
        await SettlePendingAsync();
    }

    /// <summary>Applies Elo for every match finished during this request (rated, human vs human only).</summary>
    public async Task SettlePendingAsync()
    {
        foreach (var m in _pendingSettle.ToList())
        {
            _pendingSettle.Remove(m);
            if (!m.Rated || m.VsComputer || m.Player1Id is null || m.Player2Id is null) continue;
            if (m.RatingDeltaP1 is not null) continue;   // already settled
            var r1 = await RatingAsync(m.Player1Id.Value, m.Game, create: true);
            var r2 = await RatingAsync(m.Player2Id.Value, m.Game, create: true);
            var score1 = m.IsDraw ? 0.5 : m.Winner == GameSide.P1 ? 1.0 : 0.0;
            var (d1, d2) = Elo.Pair(r1.Rating, r1.Games, r2.Rating, r2.Games, score1);
            m.RatingP1Before ??= r1.Rating;
            m.RatingP2Before ??= r2.Rating;
            Apply(r1, d1, score1);
            Apply(r2, d2, 1.0 - score1);
            m.RatingDeltaP1 = d1;
            m.RatingDeltaP2 = d2;
        }

        static void Apply(GameRating r, int delta, double score)
        {
            r.Rating += delta;
            r.Peak = Math.Max(r.Peak, r.Rating);
            r.Games++;
            if (score > 0.5) r.Wins++; else if (score < 0.5) r.Losses++; else r.Draws++;
            r.UpdatedAt = DateTime.UtcNow;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static SemaphoreSlim LockFor(Guid id) => _locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

    private IQueryable<GameMatch> MatchQuery() => _db.GameMatches.Include(m => m.Player1).Include(m => m.Player2);

    private async Task<GameMatch> LoadAsync(Guid id)
        => await MatchQuery().FirstOrDefaultAsync(m => m.Id == id) ?? throw new GameApiException(404, "No such game.");

    private async Task<GameRating> RatingAsync(Guid userId, string game, bool create)
    {
        var r = await _db.GameRatings.FirstOrDefaultAsync(x => x.UserId == userId && x.Game == game);
        if (r is null && create)
        {
            r = new GameRating { UserId = userId, Game = game };
            _db.GameRatings.Add(r);
        }
        return r!;
    }

    private async Task<RatingCard> CardAsync(GameRating r)
    {
        // Same ordering as the leaderboard: established players first, then provisional.
        int? rank = null;
        if (r.Games >= 1)
        {
            var establishedAhead = await _db.GameRatings.CountAsync(x => x.Game == r.Game && x.Games >= GameRating.ProvisionalGames
                                                                       && (r.IsProvisional || x.Rating > r.Rating));
            var provisionalAhead = r.IsProvisional
                ? await _db.GameRatings.CountAsync(x => x.Game == r.Game && x.Games >= 1 && x.Games < GameRating.ProvisionalGames && x.Rating > r.Rating)
                : 0;
            rank = 1 + establishedAhead + provisionalAhead;
        }
        return new RatingCard(r.Game, r.Rating, r.Peak, r.Games, r.Wins, r.Losses, r.Draws, r.IsProvisional, rank);
    }

    private static Guid? PlayerIdOf(GameMatch m, GameSide side) => side == GameSide.P1 ? m.Player1Id : m.Player2Id;

    private static GameSide? SeatOf(GameMatch m, Guid me)
        => m.Player1Id == me ? GameSide.P1 : m.Player2Id == me ? GameSide.P2 : null;

    private static PlayerRef Ref(User u) => new(u.Id, u.DisplayName ?? u.Username, u.AvatarUrl);

    private static PlayerRef? SeatRef(GameMatch m, GameSide side)
    {
        var user = side == GameSide.P1 ? m.Player1 : m.Player2;
        if (user is not null) return Ref(user);
        if (m.VsComputer && PlayerIdOf(m, side) is null && m.Status != GameMatchStatus.Open)
            return new PlayerRef(null, $"Computer ({m.Difficulty ?? "medium"})", null);
        return null;
    }

    private static string SideName(GameSide? s) => s switch { GameSide.P1 => "p1", GameSide.P2 => "p2", _ => null! };

    private static string ReasonName(GameEndReason r)
    {
        var n = r.ToString();
        return char.ToLowerInvariant(n[0]) + n[1..];
    }

    private MatchSummary Summary(GameMatch m, Guid me) => Fill(new MatchSummary(), m, me);

    private T Fill<T>(T dto, GameMatch m, Guid me) where T : MatchSummary
    {
        dto.Id = m.Id;
        dto.Game = m.Game;
        dto.Status = m.Status.ToString().ToLowerInvariant();
        dto.Rated = m.Rated;
        dto.VsComputer = m.VsComputer;
        dto.Difficulty = m.Difficulty;
        dto.P1 = SeatRef(m, GameSide.P1);
        dto.P2 = SeatRef(m, GameSide.P2);
        dto.YouAre = SideName(SeatOf(m, me));
        dto.Turn = m.Status == GameMatchStatus.Active ? SideName(m.Turn) : null;
        dto.Winner = SideName(m.Winner);
        dto.IsDraw = m.IsDraw;
        dto.EndReason = ReasonName(m.EndReason);
        dto.RatingDeltaP1 = m.RatingDeltaP1;
        dto.RatingDeltaP2 = m.RatingDeltaP2;
        dto.MoveCount = m.MoveCount;
        dto.CreatedAt = m.CreatedAt;
        dto.LastMoveAt = m.LastMoveAt;
        dto.FinishedAt = m.FinishedAt;
        return dto;
    }

    private MatchState State(GameMatch m, Guid me)
    {
        var dto = Fill(new MatchState(), m, me);
        dto.DrawOfferBy = SideName(m.DrawOfferBy);
        var mySeat = SeatOf(m, me);
        var myTurn = m.Status == GameMatchStatus.Active && mySeat is not null && m.Turn == mySeat;
        dto.Board = m.Game switch
        {
            GameKeys.Chess => ChessBoard(m, myTurn),
            GameKeys.Connect4 => Connect4Board(m),
            GameKeys.TicTacToe => TicTacToeBoard(m),
            _ => new { },
        };
        return dto;
    }

    private static ChessBoardDto ChessBoard(GameMatch m, bool includeLegal)
    {
        var s = ChessState.FromJson(m.StateJson);
        var b = ChessEngine.Board(s);
        var (byP1, byP2) = ChessEngine.Captured(b);
        MoveSquares? last = s.LastUci is { Length: >= 4 } u ? new MoveSquares(u[..2], u[2..4]) : null;
        var legal = includeLegal ? ChessEngine.LegalUci(b) : new List<string>();
        return new ChessBoardDto(s.Fen, s.Moves, last, ChessEngine.InCheck(b), legal, byP1, byP2, ChessEngine.Pgn(b));
    }

    private static Connect4BoardDto Connect4Board(GameMatch m)
    {
        var s = ConnectFourState.FromJson(m.StateJson);
        ConnectFourEngine.Winner(s.Cells, out var win);
        return new Connect4BoardDto(ConnectFourState.Rows, ConnectFourState.Cols, s.Cells,
                                    s.Moves.Count > 0 ? s.Moves[^1] : null, win);
    }

    private static TicTacToeBoardDto TicTacToeBoard(GameMatch m)
    {
        var s = TicTacToeState.FromJson(m.StateJson);
        TicTacToeEngine.Winner(s.Cells, out var win);
        return new TicTacToeBoardDto(s.Cells, win);
    }
}
