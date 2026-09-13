// Per-game lobby (/Games/Chess, /Games/Connect4, /Games/TicTacToe).
(function () {
    const { api, esc, chip, matchRow, empty, toast, hub, wireLobbyButtons, UID } = window.G;
    const game = window.GAME_KEY;
    const $ = id => document.getElementById(id);

    async function create(vsComputer, difficulty) {
        const rated = vsComputer ? false : $('rated').checked;
        const seat = vsComputer ? 'p1' : (document.querySelector('input[name=seat]:checked')?.value || 'random');
        try {
            const m = await api('/matches', { method: 'POST', body: { game, rated, vsComputer, difficulty: vsComputer ? difficulty : null, seat } });
            location.href = `/Games/Play/${m.id}`;
        } catch (e) { toast(e.message, true); }
    }

    $('create').addEventListener('click', () => { $('create').disabled = true; create(false).finally(() => $('create').disabled = false); });
    document.querySelectorAll('[data-diff]').forEach(b => b.addEventListener('click', () => create(true, b.dataset.diff)));

    async function loadOpen() {
        try {
            const open = await api(`/matches/open?game=${game}`);
            $('open').innerHTML = open.length ? open.map(m => matchRow(m, 'open')).join('') : empty('No open challenges — post one above.');
        } catch (e) { $('open').innerHTML = empty(e.message); }
    }

    async function loadTop() {
        try {
            const rows = await api(`/leaderboard?game=${game}&limit=10`);
            if (!rows.length) { $('top').innerHTML = empty('Nobody is ranked yet — 10 rated games gets you on the board.'); return; }
            $('top').innerHTML = `<table class="g-table"><thead><tr><th>#</th><th>Player</th><th class="num">Rating</th><th class="num">W-L-D</th></tr></thead><tbody>${
                rows.map(r => `<tr class="${r.player?.userId?.toLowerCase() === UID ? 'me' : ''}"><td class="g-rank r${r.rank}">${r.rank}</td><td>${chip(r.player)}</td><td class="num"><b>${r.rating}</b>${r.provisional ? '<sup title="provisional — fewer than 10 rated games">?</sup>' : ''}</td><td class="num">${r.wins}-${r.losses}-${r.draws}</td></tr>`).join('')
            }</tbody></table>`;
        } catch (e) { $('top').innerHTML = empty(e.message); }
    }

    async function loadRecent() {
        try {
            const rows = await api(`/history?game=${game}&limit=10`);
            $('recent').innerHTML = rows.length ? rows.map(m => matchRow(m, 'recent')).join('') : empty('No finished games yet.');
        } catch (e) { $('recent').innerHTML = empty(e.message); }
    }

    wireLobbyButtons($('open'), loadOpen);
    loadOpen(); loadTop(); loadRecent();

    hub().then(conn => {
        if (!conn) return;
        conn.invoke('JoinLobby', game).catch(() => {});
        let t = null;
        conn.on('LobbyChanged', () => { clearTimeout(t); t = setTimeout(loadOpen, 150); });
    });
    setInterval(loadOpen, 20000);
})();
