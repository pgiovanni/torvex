// /Games hub page: ratings strip, active matches, open lobby, recent results.
(function () {
    const { api, esc, chip, matchRow, empty, toast, hub, wireLobbyButtons, GAME_NAMES, GAME_ICONS } = window.G;
    const $ = id => document.getElementById(id);

    function ratingCard(r) {
        const prov = r.provisional ? '<sup title="provisional — fewer than 10 rated games">?</sup>' : '';
        const rank = r.rank ? `<span class="g-rating-rank">#${r.rank}</span> · ` : '';
        return `<div class="g-rating">
            <span class="g-rating-game">${GAME_ICONS[r.game] || ''} ${esc(GAME_NAMES[r.game] || r.game)}</span>
            <span class="g-rating-num">${r.rating}${prov}</span>
            <span class="g-rating-meta">${rank}${r.wins}W · ${r.losses}L · ${r.draws}D · peak ${r.peak}</span>
        </div>`;
    }

    async function load() {
        try {
            const me = await api('/me');
            const ratings = me.ratings || [];
            $('ratings').innerHTML = ratings.length ? ratings.map(ratingCard).join('')
                : empty('No ratings yet — play a rated game and one appears here (everyone starts at 1200).');
            const active = me.active || [];
            $('active').innerHTML = active.length ? active.map(m => matchRow(m, 'active')).join('') : empty('Nothing in progress. Post a challenge or join one →');
            const open = (me.open || []);
            $('open').innerHTML = open.length ? open.map(m => matchRow(m, 'open')).join('') : empty('No open challenges right now — be the first.');
            const recent = me.recent || [];
            $('recent').innerHTML = recent.length ? recent.map(m => matchRow(m, 'recent')).join('') : empty('No finished games yet.');
        } catch (e) {
            toast(e.message, true);
        }
    }

    wireLobbyButtons($('open'), load);
    load();

    hub().then(conn => {
        if (!conn) return;
        ['chess', 'connect4', 'tictactoe'].forEach(g => conn.invoke('JoinLobby', g).catch(() => {}));
        let t = null;
        conn.on('LobbyChanged', () => { clearTimeout(t); t = setTimeout(load, 150); });
    });
    setInterval(load, 30000);
})();
