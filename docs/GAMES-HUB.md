# Games hub — torvex.app/Games (contract, 2026-09-12)

Paul: "let's start building the game panel. chess, wordle, connect four, all of
the games … chess has to be like lichess with elo/mmr ranking." Games are FREE
engagement features (no SKU — see business memory). Web boards beat Discord
embeds; the bot cogs become thin entry points later.

This file is the contract between the backend (Application + API) and the
frontend (Razor pages + JS). Both sides build against it; change it here first.

## Games
| key         | players | rated | computer | notes |
|-------------|---------|-------|----------|-------|
| `chess`     | 2       | yes   | Stockfish (`/usr/games/stockfish` on the VPS; random legal move fallback in dev) | Gera.Chess for rules |
| `connect4`  | 2       | yes   | minimax depth by difficulty | 7 cols × 6 rows |
| `tictactoe` | 2       | yes   | minimax (perfect on hard) | 3×3 |
| `wordle`    | 1       | no    | —        | daily shared puzzle + practice |

Seats: **P1 moves first** (white in chess). `GameSide` enum: P1=1, P2=2.

## Data (Domain/Entities/Games, migration `AddGamesHub` — DONE)
- `game_ratings` — (user, game) unique; Rating (start 1200), Peak, Games, Wins, Losses, Draws. Provisional = < 10 rated games.
- `game_matches` — Game, Status (Open/Active/Finished/Aborted), Rated, Player1Id/Player2Id (nullable), CreatorId, CreatorSeat ("p1"/"p2"/"random"), VsComputer, Difficulty, StateJson (jsonb), Turn, MoveCount, DrawOfferBy, Winner, IsDraw, EndReason, RatingDeltaP1/P2, RatingP1Before/P2Before, timestamps.
- `wordle_plays` — UserId, PuzzleDate (null = practice), Answer, Guesses (CSV, upper), GuessCount, Solved, Finished, Hard.

## Elo (Application/Games/Elo.cs)
Standard Elo, expected = 1/(1+10^((Rb-Ra)/400)). K = 40 while Games < 30, 20 otherwise, 10 once Rating ≥ 2400 (lichess-style ladder). Draw = 0.5. Floor 100. Only human-vs-human `Rated` matches change ratings; a rating row is created on a player's first rated match. Deltas are stored on the match.

## REST (all `RequireAuthorization()`, user = `ClaimTypes.NameIdentifier`)
Base `/api/games`. Errors: `400 { error: "..." }` for illegal input, `403` when not your seat/turn, `404` unknown match.

```
GET  /me                         → { ratings: RatingCard[], active: MatchSummary[], open: MatchSummary[], recent: MatchSummary[] }
GET  /leaderboard?game=chess&limit=50 → LeaderRow[]
GET  /players/{userId}?game=chess    → { player: PlayerRef, rating: RatingCard|null, recent: MatchSummary[] }
GET  /matches/open?game=             → MatchSummary[]     (lobby: Status=Open, not mine, not vsComputer)
POST /matches                        { game, rated, vsComputer, difficulty?, seat: "p1"|"p2"|"random" } → MatchState
      vsComputer → Status=Active immediately (never rated). Otherwise Status=Open.
      One Open challenge per user per game (400 otherwise). Max 10 Active matches per user.
POST /matches/{id}/join              → MatchState  (resolves seats; 400 if own challenge / not Open)
POST /matches/{id}/cancel            → 204         (creator, Status=Open only)
GET  /matches/{id}                   → MatchState  (anyone logged in may spectate)
POST /matches/{id}/move              { move } → MatchState
      chess: "e2e4" | "e7e8q" (UCI, promotion letter q/r/b/n lower)   connect4: "0".."6" (column)   tictactoe: "0".."8" (cell, row-major)
      After a human move in a vsComputer match the server plays the reply before returning.
POST /matches/{id}/resign            → MatchState
POST /matches/{id}/draw              { action: "offer"|"accept"|"decline" } → MatchState (chess only; offer is cleared by any move)
GET  /history?game=&limit=20         → MatchSummary[] (my finished matches, newest first)

GET  /wordle/today                   → WordleState (creates today's row on first call)
POST /wordle/guess                   { word, practice?: bool } → WordleState   (400 "not in word list" / "already finished")
POST /wordle/practice                → WordleState (new practice game; abandons an unfinished one)
GET  /wordle/practice                → WordleState|null (current practice game)
GET  /wordle/stats                   → { played, solved, streak, maxStreak, distribution: int[6] }
GET  /wordle/leaderboard?days=30     → [{ player: PlayerRef, solved, played, avgGuesses, streak }]
```

### Shapes
```ts
PlayerRef     { userId, name, avatarUrl }                 // name = DisplayName ?? Username
RatingCard    { game, rating, peak, games, wins, losses, draws, provisional, rank }   // rank = 1-based among rated players, null if provisional
LeaderRow     { rank, player: PlayerRef, rating, games, wins, losses, draws }
MatchSummary  { id, game, status: "open"|"active"|"finished"|"aborted", rated, vsComputer, difficulty,
                p1: PlayerRef|null, p2: PlayerRef|null, youAre: "p1"|"p2"|null, turn: "p1"|"p2"|null,
                winner: "p1"|"p2"|null, isDraw, endReason: string, ratingDeltaP1, ratingDeltaP2,
                moveCount, createdAt, lastMoveAt, finishedAt }
MatchState    MatchSummary & { drawOfferBy: "p1"|"p2"|null, board: ChessBoard|Connect4Board|TicTacToeBoard }
ChessBoard    { fen, moves: string[] /*SAN*/, lastMove: {from,to}|null, check: bool,
                legal: string[] /*UCI incl. promotions, only when it's the viewer's turn*/,
                capturedByP1: string[], capturedByP2: string[] /*FEN chars*/, pgn }
Connect4Board { rows: 6, cols: 7, cells: number[][] /*[row][col], 0 empty, 1 P1, 2 P2, row 0 = top*/, lastCol, winningCells: [row,col][] }
TicTacToeBoard{ cells: number[] /*9, 0/1/2*/, winningCells: number[] }
WordleState   { id, mode: "daily"|"practice", date: "YYYY-MM-DD"|null, maxGuesses: 6, guesses: [{ word, marks }],
                finished, solved, answer: string|null /*only when finished*/, hard, streak }
                marks = 5 chars per guess: G (right spot) Y (wrong spot) B (absent) — classic duplicate-letter handling
```
Computer seats: `p1`/`p2` is `{ userId: null, name: "Computer (hard)", avatarUrl: null }`.

## SignalR `/hubs/games` (`[Authorize]`)
Client → server: `JoinMatch(matchId)`, `LeaveMatch(matchId)`, `JoinLobby(game)`, `LeaveLobby(game)`.
Server → client: `MatchUpdated(matchId)` to group `match:{id}` after every state change (client re-fetches `GET /matches/{id}`);
`LobbyChanged(game)` to group `lobby:{game}` when a challenge is created / joined / cancelled.
The JS connects with `accessTokenFactory` reading `<meta name="jwt">` like chat.js does.

## Pages (Razor, Dashboard shell = `_DashboardNav` + `.app-shell`)
- `/Games` — hub: game cards, my ratings, lobby (open challenges across games), my active matches, recent results.
- `/Games/Chess`, `/Games/Connect4`, `/Games/TicTacToe` — per-game lobby: create challenge (rated/casual, seat), play computer (difficulty), open challenges, leaderboard top 10, my recent.
- `/Games/Play/{id}` — the board. One page, renders the right board by `game`. Live via the hub. Spectators see it read-only.
- `/Games/Wordle` — daily + practice tabs, keyboard, stats, leaderboard.
- `/Games/Leaderboard?game=chess` — full table.
- `_DashboardNav`: "🎮 Games" under COMMUNITY.
Page models only check login (redirect to /Auth/Login) and pass `CurrentUserId`; data is fetched by JS from the API (same-origin cookie → middleware turns it into the bearer header).

## Wordle words
Answers: 5-letter words from the bot's curated `data/words.json` `common` list (peepos-reclaimer). Valid guesses: a large public 5-letter list embedded as a resource (fetched once at build time), unioned with the answers. Daily answer = deterministic pick from the answer list by date (UTC) so every player gets the same word.
