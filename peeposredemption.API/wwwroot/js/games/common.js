// Shared helpers for the games hub pages (torvex.app/Games). Classic script —
// everything hangs off window.G so the per-page scripts can use it.
(function () {
    const UID = (window.TORVEX_UID || '').toLowerCase();
    const GAME_NAMES = { chess: 'Chess', connect4: 'Connect Four', tictactoe: 'Tic-Tac-Toe', wordle: 'Wordle' };
    const GAME_ICONS = { chess: '♟️', connect4: '🔴', tictactoe: '❌', wordle: '🟩' };
    const GAME_PATHS = { chess: '/Games/Chess', connect4: '/Games/Connect4', tictactoe: '/Games/TicTacToe', wordle: '/Games/Wordle' };
    const END_REASONS = {
        none: '', checkmate: 'by checkmate', stalemate: 'by stalemate', resigned: 'by resignation',
        drawagreed: 'by agreement', insufficientmaterial: 'insufficient material', fiftymoverule: 'fifty-move rule',
        repetition: 'threefold repetition', line: 'four in a row', boardfull: 'board full', timeout: 'on time', aborted: 'aborted',
    };

    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    function jwt() { return document.querySelector('meta[name="jwt"]')?.content || ''; }

    // fetch wrapper: JSON in/out, throws Error(message) with .status on failure.
    async function api(path, opts = {}) {
        const init = { method: opts.method || 'GET', credentials: 'same-origin', headers: { 'Accept': 'application/json' } };
        const t = jwt();
        if (t) init.headers['Authorization'] = 'Bearer ' + t;
        if (opts.body !== undefined) {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(opts.body);
        }
        const resp = await fetch('/api/games' + path, init);
        if (resp.status === 401) { window.location.href = '/Auth/Login?returnUrl=' + encodeURIComponent(location.pathname + location.search); throw Object.assign(new Error('Signed out'), { status: 401 }); }
        if (resp.status === 204) return null;
        let data = null;
        const text = await resp.text();
        if (text) { try { data = JSON.parse(text); } catch (_) { data = { error: text }; } }
        if (!resp.ok) {
            const msg = (data && (data.error || data.title || data.message)) || `Request failed (${resp.status})`;
            throw Object.assign(new Error(msg), { status: resp.status, data });
        }
        return data;
    }

    let toastTimer = null;
    function toast(msg, isErr) {
        document.querySelectorAll('.g-toast').forEach(t => t.remove());
        const el = document.createElement('div');
        el.className = 'g-toast' + (isErr ? ' err' : '');
        el.textContent = msg;
        document.body.appendChild(el);
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => el.remove(), 3200);
    }

    // SignalR to /hubs/games. Resolves to the connection (or null when signalR is unavailable).
    let hubPromise = null;
    function hub() {
        if (hubPromise) return hubPromise;
        hubPromise = (async () => {
            if (typeof signalR === 'undefined') return null;
            const conn = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/games', { accessTokenFactory: () => jwt() })
                .withAutomaticReconnect()
                .build();
            try { await conn.start(); } catch (e) { console.warn('games hub offline', e); return null; }
            return conn;
        })();
        return hubPromise;
    }

    function relTime(iso) {
        if (!iso) return '';
        const d = new Date(iso), s = Math.round((Date.now() - d.getTime()) / 1000);
        if (s < 45) return 'just now';
        if (s < 3600) return Math.round(s / 60) + ' min ago';
        if (s < 86400) return Math.round(s / 3600) + ' h ago';
        if (s < 7 * 86400) return Math.round(s / 86400) + ' d ago';
        return d.toLocaleDateString();
    }

    function initial(name) { return (name || '?').trim().charAt(0).toUpperCase() || '?'; }

    // A player chip: avatar (image or letter), name, optional rating.
    function chip(p, rating, extraClass) {
        if (!p) return `<span class="g-chip ${extraClass || ''}"><span class="g-avatar" style="background:#404249">?</span><span class="g-chip-name g-muted">waiting…</span></span>`;
        const av = p.avatarUrl ? `<span class="g-avatar"><img src="${esc(p.avatarUrl)}" alt=""></span>`
                               : `<span class="g-avatar" style="background:${colorFor(p.userId || p.name)}">${esc(initial(p.name))}</span>`;
        const r = rating != null ? `<span class="g-chip-rating">${rating}</span>` : '';
        const me = p.userId && p.userId.toLowerCase() === UID ? ' (you)' : '';
        return `<span class="g-chip ${extraClass || ''}">${av}<span class="g-chip-name">${esc(p.name)}${me}</span>${r}</span>`;
    }

    function colorFor(key) {
        const palette = ['#00BFA5', '#5865F2', '#EB459E', '#FEE75C', '#57F287', '#ED4245', '#F47B67', '#9B59B6'];
        let h = 0; for (const c of String(key || '')) h = (h * 31 + c.charCodeAt(0)) >>> 0;
        return palette[h % palette.length];
    }

    function delta(n) {
        if (n == null) return '';
        const cls = n > 0 ? 'g-delta-up' : n < 0 ? 'g-delta-down' : 'g-delta-zero';
        return `<span class="${cls}">${n > 0 ? '+' : ''}${n}</span>`;
    }

    // One row for a MatchSummary in a list (lobby / active / recent).
    function matchRow(m, mode) {
        const me = m.youAre;                     // "p1" | "p2" | null
        const opp = me === 'p1' ? m.p2 : me === 'p2' ? m.p1 : null;
        const iconName = `${GAME_ICONS[m.game] || ''} ${GAME_NAMES[m.game] || m.game}`;
        const pill = `<span class="g-pill ${m.rated ? 'rated' : 'casual'}">${m.rated ? 'rated' : 'casual'}</span>`;
        let body = '', action = '';
        if (mode === 'open') {
            const who = m.p1 || m.p2;
            const mine = who && who.userId && who.userId.toLowerCase() === UID;
            body = `<span class="g-grow">${chip(who)}<span class="g-sub">${iconName} · plays ${m.p1 ? seatName(m.game, 'p1') : seatName(m.game, 'p2')} · ${relTime(m.createdAt)}</span></span>${pill}`;
            action = mine ? `<button class="g-btn g-btn-danger" data-cancel="${m.id}">Cancel</button>`
                          : `<button class="g-btn g-btn-teal" data-join="${m.id}">Join</button>`;
        } else if (mode === 'active') {
            const yourTurn = me && m.turn === me;
            body = `<span class="g-grow"><a class="g-rowlink" href="/Games/Play/${m.id}">${iconName} vs ${esc(opp ? opp.name : '…')}</a><span class="g-sub">move ${m.moveCount} · ${relTime(m.lastMoveAt || m.createdAt)}</span></span>${pill}${yourTurn ? '<span class="g-pill turn">your turn</span>' : m.status === 'active' ? '<span class="g-pill">their turn</span>' : '<span class="g-pill">waiting</span>'}`;
            action = `<a class="g-btn" href="/Games/Play/${m.id}">Open</a>`;
        } else {
            const res = resultWord(m);
            const d = me === 'p1' ? m.ratingDeltaP1 : me === 'p2' ? m.ratingDeltaP2 : null;
            body = `<span class="g-grow"><a class="g-rowlink" href="/Games/Play/${m.id}">${iconName} vs ${esc(opp ? opp.name : '…')}</a><span class="g-sub">${res} ${esc(END_REASONS[(m.endReason || '').toLowerCase()] || '')} · ${relTime(m.finishedAt || m.createdAt)}</span></span>${pill}${m.rated ? delta(d) : ''}`;
        }
        return `<div class="g-row" data-id="${m.id}">${body}${action}</div>`;
    }

    function resultWord(m) {
        if (m.status !== 'finished') return m.status;
        if (m.isDraw) return 'Draw';
        if (!m.youAre) return (m.winner === 'p1' ? (m.p1?.name || 'P1') : (m.p2?.name || 'P2')) + ' won';
        return m.winner === m.youAre ? 'Won' : 'Lost';
    }

    function seatName(game, seat) {
        if (game === 'chess') return seat === 'p1' ? 'white' : 'black';
        if (game === 'connect4') return seat === 'p1' ? 'red (first)' : 'yellow (second)';
        if (game === 'tictactoe') return seat === 'p1' ? 'X (first)' : 'O (second)';
        return seat === 'p1' ? 'first' : 'second';
    }

    function empty(text) { return `<div class="g-empty">${esc(text)}</div>`; }

    // Wire Join / Cancel buttons inside a list container.
    function wireLobbyButtons(container, refresh) {
        container.addEventListener('click', async e => {
            const j = e.target.closest('[data-join]'), c = e.target.closest('[data-cancel]');
            try {
                if (j) { j.disabled = true; await api(`/matches/${j.dataset.join}/join`, { method: 'POST' }); location.href = `/Games/Play/${j.dataset.join}`; }
                else if (c) { c.disabled = true; await api(`/matches/${c.dataset.cancel}/cancel`, { method: 'POST' }); toast('Challenge cancelled'); refresh && refresh(); }
            } catch (err) { toast(err.message, true); refresh && refresh(); }
        });
    }

    window.G = { UID, GAME_NAMES, GAME_ICONS, GAME_PATHS, END_REASONS, esc, api, toast, hub, relTime, chip, delta, matchRow, resultWord, seatName, empty, wireLobbyButtons, colorFor };
})();
