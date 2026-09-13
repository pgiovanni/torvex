// /Games/Leaderboard?game=…: full rated table for one game.
(function () {
    const { api, chip, empty, UID } = window.G;
    const game = window.GAME_KEY || 'chess';
    const el = document.getElementById('board');

    api(`/leaderboard?game=${game}&limit=100`).then(rows => {
        if (!rows.length) { el.innerHTML = empty('Nobody is ranked yet — 10 rated games gets you on the board.'); return; }
        el.innerHTML = `<table class="g-table"><thead><tr><th>#</th><th>Player</th><th class="num">Rating</th><th class="num">Games</th><th class="num">W</th><th class="num">L</th><th class="num">D</th></tr></thead><tbody>${
            rows.map(r => `<tr class="${r.player?.userId?.toLowerCase() === UID ? 'me' : ''}"><td class="g-rank r${r.rank}">${r.rank}</td><td>${chip(r.player)}</td><td class="num"><b>${r.rating}</b></td><td class="num">${r.games}</td><td class="num">${r.wins}</td><td class="num">${r.losses}</td><td class="num">${r.draws}</td></tr>`).join('')
        }</tbody></table>`;
    }).catch(e => { el.innerHTML = empty(e.message); });
})();
