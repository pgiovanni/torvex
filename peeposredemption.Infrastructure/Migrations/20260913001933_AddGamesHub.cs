using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace peeposredemption.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamesHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    rated = table.Column<bool>(type: "boolean", nullable: false),
                    player1_id = table.Column<Guid>(type: "uuid", nullable: true),
                    player2_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_seat = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    vs_computer = table.Column<bool>(type: "boolean", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    state_json = table.Column<string>(type: "jsonb", nullable: false),
                    turn = table.Column<int>(type: "integer", nullable: true),
                    move_count = table.Column<int>(type: "integer", nullable: false),
                    draw_offer_by = table.Column<int>(type: "integer", nullable: true),
                    winner = table.Column<int>(type: "integer", nullable: true),
                    is_draw = table.Column<bool>(type: "boolean", nullable: false),
                    end_reason = table.Column<int>(type: "integer", nullable: false),
                    rating_delta_p1 = table.Column<int>(type: "integer", nullable: true),
                    rating_delta_p2 = table.Column<int>(type: "integer", nullable: true),
                    rating_p1_before = table.Column<int>(type: "integer", nullable: true),
                    rating_p2_before = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_move_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_game_matches", x => x.id);
                    table.ForeignKey(
                        name: "f_k_game_matches__users_player1_id",
                        column: x => x.player1_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "f_k_game_matches__users_player2_id",
                        column: x => x.player2_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "game_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    peak = table.Column<int>(type: "integer", nullable: false),
                    games = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false),
                    draws = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_game_ratings", x => x.id);
                    table.ForeignKey(
                        name: "f_k_game_ratings__users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wordle_plays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    puzzle_date = table.Column<DateOnly>(type: "date", nullable: true),
                    answer = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    guesses = table.Column<string>(type: "text", nullable: false),
                    guess_count = table.Column<int>(type: "integer", nullable: false),
                    solved = table.Column<bool>(type: "boolean", nullable: false),
                    finished = table.Column<bool>(type: "boolean", nullable: false),
                    hard = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_wordle_plays", x => x.id);
                    table.ForeignKey(
                        name: "f_k_wordle_plays__users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_matches_game_status_created_at",
                table: "game_matches",
                columns: new[] { "game", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_game_matches_player1_id_status",
                table: "game_matches",
                columns: new[] { "player1_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_game_matches_player2_id_status",
                table: "game_matches",
                columns: new[] { "player2_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_game_ratings_game_rating",
                table: "game_ratings",
                columns: new[] { "game", "rating" });

            migrationBuilder.CreateIndex(
                name: "IX_game_ratings_user_id_game",
                table: "game_ratings",
                columns: new[] { "user_id", "game" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wordle_plays_puzzle_date_finished",
                table: "wordle_plays",
                columns: new[] { "puzzle_date", "finished" });

            migrationBuilder.CreateIndex(
                name: "IX_wordle_plays_user_id_puzzle_date",
                table: "wordle_plays",
                columns: new[] { "user_id", "puzzle_date" },
                unique: true,
                filter: "puzzle_date IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_matches");

            migrationBuilder.DropTable(
                name: "game_ratings");

            migrationBuilder.DropTable(
                name: "wordle_plays");
        }
    }
}
