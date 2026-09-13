using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace peeposredemption.API.Hubs;

/// <summary>
/// Live updates for the games hub. Pure fan-out: the REST endpoints change
/// state and then push "MatchUpdated"/"LobbyChanged" to these groups; clients
/// re-fetch. No game logic lives here.
/// </summary>
[Authorize]
public class GamesHub : Hub
{
    public static string MatchGroup(Guid matchId) => $"match:{matchId}";
    public static string LobbyGroup(string game) => $"lobby:{game}";

    public Task JoinMatch(Guid matchId) => Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(matchId));
    public Task LeaveMatch(Guid matchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroup(matchId));
    public Task JoinLobby(string game) => Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup(game));
    public Task LeaveLobby(string game) => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup(game));
}
