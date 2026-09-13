using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using peeposredemption.API.Hubs;

namespace peeposredemption.API.Games;

/// <summary>/api/games/* — see docs/GAMES-HUB.md. Every route needs a logged-in user.</summary>
public static class GamesEndpoints
{
    public static void MapGamesEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/games").RequireAuthorization();

        // ── profile / boards ─────────────────────────────────────────────────
        g.MapGet("/me", (HttpContext ctx, GameMatchService svc)
            => Run(() => svc.MeAsync(Uid(ctx))));

        g.MapGet("/leaderboard", (string? game, int? limit, GameMatchService svc)
            => Run(() => svc.LeaderboardAsync(game ?? "chess", limit ?? 50)));

        g.MapGet("/players/{userId:guid}", async (Guid userId, string? game, HttpContext ctx, GameMatchService svc) =>
        {
            try
            {
                var page = await svc.PlayerAsync(userId, game, Uid(ctx));
                return page is null ? Results.NotFound(new { error = "No such player." }) : Results.Ok(page);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        // ── matches ──────────────────────────────────────────────────────────
        g.MapGet("/matches/open", (string? game, HttpContext ctx, GameMatchService svc)
            => Run(() => svc.OpenAsync(game, Uid(ctx))));

        g.MapPost("/matches", async (CreateMatchRequest req, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var state = await svc.CreateAsync(Uid(ctx), req);
                if (!state.VsComputer) await hub.Clients.Group(GamesHub.LobbyGroup(state.Game)).SendAsync("LobbyChanged", state.Game);
                return Results.Ok(state);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapPost("/matches/{id:guid}/join", async (Guid id, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var state = await svc.JoinAsync(id, Uid(ctx));
                await Notify(hub, id, state.Game, lobby: true);
                return Results.Ok(state);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapPost("/matches/{id:guid}/cancel", async (Guid id, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var before = await svc.GetAsync(id, Uid(ctx));
                await svc.CancelAsync(id, Uid(ctx));
                await Notify(hub, id, before.Game, lobby: true);
                return Results.NoContent();
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapGet("/matches/{id:guid}", (Guid id, HttpContext ctx, GameMatchService svc)
            => Run(() => svc.GetAsync(id, Uid(ctx))));

        g.MapPost("/matches/{id:guid}/move", async (Guid id, MoveRequest req, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var state = await svc.MoveAsync(id, Uid(ctx), req.Move);
                await Notify(hub, id, state.Game, lobby: false);
                return Results.Ok(state);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapPost("/matches/{id:guid}/resign", async (Guid id, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var state = await svc.ResignAsync(id, Uid(ctx));
                await Notify(hub, id, state.Game, lobby: false);
                return Results.Ok(state);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapPost("/matches/{id:guid}/draw", async (Guid id, DrawRequest req, HttpContext ctx, GameMatchService svc, IHubContext<GamesHub> hub) =>
        {
            try
            {
                var state = await svc.DrawAsync(id, Uid(ctx), req.Action);
                await Notify(hub, id, state.Game, lobby: false);
                return Results.Ok(state);
            }
            catch (GameApiException e) { return Fail(e); }
        });

        g.MapGet("/history", (string? game, int? limit, HttpContext ctx, GameMatchService svc)
            => Run(() => svc.HistoryAsync(Uid(ctx), game, limit ?? 20)));

        // ── wordle ───────────────────────────────────────────────────────────
        g.MapGet("/wordle/today", (HttpContext ctx, WordleService svc) => Run(() => svc.TodayAsync(Uid(ctx))));
        g.MapPost("/wordle/guess", (WordleGuessRequest req, HttpContext ctx, WordleService svc) => Run(() => svc.GuessAsync(Uid(ctx), req)));
        g.MapPost("/wordle/practice", (bool? hard, HttpContext ctx, WordleService svc) => Run(() => svc.NewPracticeAsync(Uid(ctx), hard ?? false)));
        g.MapGet("/wordle/practice", async (HttpContext ctx, WordleService svc) =>
        {
            var s = await svc.PracticeAsync(Uid(ctx));
            return s is null ? Results.Ok((object?)null) : Results.Ok(s);
        });
        g.MapGet("/wordle/stats", (HttpContext ctx, WordleService svc) => Run(() => svc.StatsAsync(Uid(ctx))));
        g.MapGet("/wordle/leaderboard", (int? days, WordleService svc) => Run(() => svc.LeaderboardAsync(days ?? 30)));
    }

    private static Guid Uid(HttpContext ctx)
    {
        var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(claim, out var id)) throw new GameApiException(401, "Sign in first.");
        return id;
    }

    private static async Task<IResult> Run<T>(Func<Task<T>> body)
    {
        try { return Results.Ok(await body()); }
        catch (GameApiException e) { return Fail(e); }
    }

    private static IResult Fail(GameApiException e)
        => Results.Json(new { error = e.Message }, statusCode: e.Status);

    private static async Task Notify(IHubContext<GamesHub> hub, Guid matchId, string game, bool lobby)
    {
        await hub.Clients.Group(GamesHub.MatchGroup(matchId)).SendAsync("MatchUpdated", matchId);
        if (lobby) await hub.Clients.Group(GamesHub.LobbyGroup(game)).SendAsync("LobbyChanged", game);
    }
}
