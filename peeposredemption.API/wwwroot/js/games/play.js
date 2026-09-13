// /Games/Play/{id}: renders chess / connect4 / tictactoe from MatchState and
// drives moves. Live via /hubs/games (MatchUpdated) with a 10 s poll fallback.
(function () {
    const { api, esc, chip, toast, hub, END_REASONS, GAME_NAMES, GAME_ICONS, GAME_PATHS, seatName, delta, UID } = window.G;
    const id = window.MATCH_ID;
    const $ = s => document.getElementById(s);

    const GLYPH = { K: '♔', Q: '♕', R: '♖', B: '♗', N: '♘', P: '♙', k: '♚', q: '♛', r: '♜', b: '♝', n: '♞', p: '♟' };
    const FILES = 'abcdefgh';

    let state = null;        // last MatchState
    let prevCells = null;    // connect4 previous board for the drop animation
    let sel = null;          // chess: selected square "e2"
    let busy = false;
    let pendingPromo = null; // { from, to }

    // ── data ──────────────────────────────────────────────────────────────
    async function load() {
        try {
            const s = await api(`/matches/${id}`);
            state = s;
            render();
        } catch (e) {
            $('status').textContent = e.message;
            if (e.status === 404) $('play-title').textContent = 'Match not found';
        }
    }

    async function post(path, body) {
        if (busy) return;
        busy = true;
        try {
            const s = await api(`/matches/${id}${path}`, { method: 'POST', body });
            state = s; sel = null; render();
        } catch (e) {
            toast(e.message, true);
            await load();
        } finally { busy = false; }
    }

    // ── shared frame ──────────────────────────────────────────────────────
    function mySeat() { return state.youAre; }
    function isPlayer() { return !!state.youAre; }
    function myTurn() { return state.status === 'active' && isPlayer() && state.turn === state.youAre; }
    function seatPlayer(seat) { return seat === 'p1' ? state.p1 : state.p2; }
    function seatLabel(seat) { return seatName(state.game, seat); }

    function render() {
        const s = state;
        document.title = `${GAME_NAMES[s.game] || 'Play'} - Torvex`;
        $('play-title').textContent = `${GAME_ICONS[s.game] || ''} ${GAME_NAMES[s.game] || s.game}`;
        const tag = $('play-tag');
        tag.hidden = false;
        tag.textContent = !isPlayer() ? 'watching' : s.rated ? 'rated' : 'casual';
        $('back-link').href = GAME_PATHS[s.game] || '/Games';
        $('lobby-link').href = GAME_PATHS[s.game] || '/Games';

        // player bars: opponent on top, me (or p1 for spectators) at the bottom
        const bottomSeat = mySeat() === 'p2' ? 'p2' : 'p1';
        const topSeat = bottomSeat === 'p1' ? 'p2' : 'p1';
        $('bar-top').innerHTML = bar(topSeat);
        $('bar-bottom').innerHTML = bar(bottomSeat);
        $('bar-top').classList.toggle('to-move', s.status === 'active' && s.turn === topSeat);
        $('bar-bottom').classList.toggle('to-move', s.status === 'active' && s.turn === bottomSeat);

        if (s.game === 'chess') renderChess();
        else if (s.game === 'connect4') renderC4();
        else if (s.game === 'tictactoe') renderTTT();

        renderStatus();
        renderControls();
        renderResult();
    }

    function bar(seat) {
        const p = seatPlayer(seat);
        const r = seat === 'p1' ? state.ratingP1Before : state.ratingP2Before;
        const rating = state.rated && r != null ? r : (p && p.rating != null ? p.rating : null);
        return `<span class="turn-dot"></span><span class="g-grow">${chip(p, rating)}</span><span class="g-muted g-small">${esc(seatLabel(seat))}</span>`;
    }

    function renderStatus() {
        const s = state, el = $('status');
        if (s.status === 'open') { el.innerHTML = `Waiting for an opponent… share this page or leave it open — the game starts the moment someone joins.`; return; }
        if (s.status === 'aborted') { el.textContent = 'This challenge was cancelled.'; return; }
        if (s.status === 'finished') { el.innerHTML = finishText(); return; }
        const turnP = seatPlayer(s.turn);
        let t = myTurn() ? '<b>Your move.</b>' : `Waiting for <b>${esc(turnP ? turnP.name : seatLabel(s.turn))}</b>…`;
        if (s.game === 'chess' && s.board && s.board.check) t += ' <span style="color:#ed4245;font-weight:700">Check!</span>';
        if (s.drawOfferBy) t += s.drawOfferBy === mySeat() ? '<br><span class="g-muted">You offered a draw.</span>' : `<br><b>${esc(seatPlayer(s.drawOfferBy)?.name || 'Opponent')}</b> offers a draw.`;
        el.innerHTML = t;
    }

    function finishText() {
        const s = state;
        const reason = END_REASONS[(s.endReason || '').toLowerCase()] || '';
        if (s.isDraw) return `Draw ${esc(reason)}.`;
        const w = seatPlayer(s.winner);
        return `<b>${esc(w ? w.name : seatLabel(s.winner))}</b> won ${esc(reason)}.`;
    }

    function renderControls() {
        const s = state, el = $('controls');
        const parts = [];
        if (s.status === 'active' && isPlayer()) {
            parts.push(`<button class="g-btn g-btn-danger" id="resign">Resign</button>`);
            if (s.game === 'chess' && !s.vsComputer) {
                if (s.drawOfferBy && s.drawOfferBy !== mySeat()) {
                    parts.push(`<button class="g-btn g-btn-teal" data-draw="accept">Accept draw</button><button class="g-btn" data-draw="decline">Decline</button>`);
                } else if (!s.drawOfferBy) {
                    parts.push(`<button class="g-btn" data-draw="offer">Offer draw</button>`);
                }
            }
        }
        if (s.status === 'open' && isPlayer()) parts.push(`<button class="g-btn g-btn-danger" id="cancel">Cancel challenge</button>`);
        if (s.game === 'chess' && s.board && s.board.pgn) parts.push(`<button class="g-btn" id="pgn">Copy PGN</button>`);
        el.innerHTML = parts.join('');
        $('resign')?.addEventListener('click', () => { if (confirm('Resign this game?')) post('/resign'); });
        $('cancel')?.addEventListener('click', async () => { try { await api(`/matches/${id}/cancel`, { method: 'POST' }); location.href = $('back-link').href; } catch (e) { toast(e.message, true); } });
        el.querySelectorAll('[data-draw]').forEach(b => b.addEventListener('click', () => post('/draw', { action: b.dataset.draw })));
        $('pgn')?.addEventListener('click', () => navigator.clipboard.writeText(s.board.pgn).then(() => toast('PGN copied')).catch(() => toast('Copy failed', true)));

        const rm = $('rematch');
        rm.hidden = !(s.status === 'finished' && isPlayer());
        rm.onclick = async () => {
            try {
                const m = await api('/matches', { method: 'POST', body: { game: s.game, rated: s.rated, vsComputer: s.vsComputer, difficulty: s.difficulty, seat: s.vsComputer ? 'p1' : (mySeat() === 'p1' ? 'p2' : 'p1') } });
                location.href = `/Games/Play/${m.id}`;
            } catch (e) { toast(e.message, true); }
        };
    }

    function renderResult() {
        const s = state, el = $('result');
        if (s.status !== 'finished') { el.hidden = true; return; }
        el.hidden = false;
        const me = mySeat();
        let cls = 'draw', title;
        if (s.isDraw) title = 'Draw';
        else if (me) { const won = s.winner === me; cls = won ? '' : 'lost'; title = won ? 'You won 🎉' : 'You lost'; }
        else { title = `${esc(seatPlayer(s.winner)?.name || seatLabel(s.winner))} won`; cls = ''; }
        el.className = 'g-result ' + cls;
        let deltas = '';
        if (s.rated && (s.ratingDeltaP1 != null || s.ratingDeltaP2 != null)) {
            deltas = ` · ${esc(s.p1?.name || 'P1')} ${delta(s.ratingDeltaP1)} · ${esc(s.p2?.name || 'P2')} ${delta(s.ratingDeltaP2)}`;
        }
        el.innerHTML = `<div class="r-title">${title}</div><div class="r-sub">${finishText()}${deltas}</div>`;
    }

    // ── chess ─────────────────────────────────────────────────────────────
    function parseFen(fen) {
        const rows = fen.split(' ')[0].split('/');
        const sq = {};
        rows.forEach((row, ri) => {
            let f = 0;
            for (const ch of row) {
                if (/\d/.test(ch)) f += parseInt(ch, 10);
                else { sq[FILES[f] + (8 - ri)] = ch; f++; }
            }
        });
        return sq;
    }

    function renderChess() {
        const b = state.board || {};
        const pieces = parseFen(b.fen || '8/8/8/8/8/8/8/8 w - - 0 1');
        const flip = mySeat() === 'p2';
        const legal = myTurn() ? (b.legal || []) : [];
        const targets = sel ? legal.filter(m => m.startsWith(sel)).map(m => m.slice(2, 4)) : [];
        const myColorUpper = mySeat() === 'p1';               // p1 = white = upper-case FEN
        const last = b.lastMove || null;
        const kingCh = state.turn === 'p1' ? 'K' : 'k';
        const kingSq = b.check ? Object.keys(pieces).find(k => pieces[k] === kingCh) : null;

        const ranks = flip ? [1, 2, 3, 4, 5, 6, 7, 8] : [8, 7, 6, 5, 4, 3, 2, 1];
        const files = flip ? [...FILES].reverse() : [...FILES];
        let html = `<div class="chess"><div class="ranks">${ranks.map(r => `<span>${r}</span>`).join('')}</div><div class="squares">`;
        for (const r of ranks) for (const f of files) {
            const name = f + r;
            const light = (FILES.indexOf(f) + r) % 2 === 1;
            const ch = pieces[name];
            const own = ch && myTurn() && (ch === ch.toUpperCase()) === myColorUpper && legal.some(m => m.startsWith(name));
            const cls = ['sq', light ? 'light' : 'dark'];
            if (last && (last.from === name || last.to === name)) cls.push('last');
            if (sel === name) cls.push('sel');
            if (kingSq === name) cls.push('check');
            if (targets.includes(name)) { cls.push('can'); if (ch) cls.push('cap'); }
            if (own) cls.push('own');
            html += `<div class="${cls.join(' ')}" data-sq="${name}">${ch ? `<span class="pc ${ch === ch.toUpperCase() ? 'w' : 'b'}">${GLYPH[ch]}</span>` : ''}</div>`;
        }
        html += `</div><div class="files">${files.map(f => `<span>${f}</span>`).join('')}</div></div>`;
        $('board').innerHTML = html;

        // moves list
        const mv = $('moves'); mv.hidden = false;
        const moves = b.moves || [];
        if (!moves.length) mv.innerHTML = '<span class="g-muted g-small">No moves yet.</span>';
        else {
            let rows = '';
            for (let i = 0; i < moves.length; i += 2) {
                const lastW = i === moves.length - 1, lastB = i + 1 === moves.length - 1;
                rows += `<div class="mv-row"><span class="mv-n">${i / 2 + 1}.</span><span class="${lastW ? 'mv-last' : ''}">${esc(moves[i])}</span><span class="${lastB ? 'mv-last' : ''}">${esc(moves[i + 1] || '')}</span></div>`;
            }
            mv.innerHTML = rows; mv.scrollTop = mv.scrollHeight;
        }
        // captured
        const cap = $('captured'); cap.hidden = false;
        const row = (label, arr) => `<div class="cap-row"><span class="cap-label">${label}</span>${arr && arr.length ? arr.map(c => `<span class="pc ${c === c.toUpperCase() ? 'w' : 'b'}">${GLYPH[c] || ''}</span>`).join('') : '<span class="cap-empty">none</span>'}</div>`;
        cap.innerHTML = row(esc(state.p1?.name || 'White') + ' took', b.capturedByP1) + row(esc(state.p2?.name || 'Black') + ' took', b.capturedByP2);

        wireChess(legal);
    }

    function wireChess(legal) {
        const squares = $('board').querySelector('.squares');
        if (!squares) return;
        const onPick = name => {
            if (!myTurn()) return;
            if (sel && sel !== name) {
                const cands = legal.filter(m => m.startsWith(sel) && m.slice(2, 4) === name);
                if (cands.length === 1) { const m = cands[0]; sel = null; return post('/move', { move: m }); }
                if (cands.length > 1) { pendingPromo = { from: sel, to: name }; $('promo').hidden = false; return; }
            }
            if (legal.some(m => m.startsWith(name))) { sel = sel === name ? null : name; renderChess(); return; }
            if (sel) { sel = null; renderChess(); }
        };
        squares.addEventListener('click', e => { const sq = e.target.closest('[data-sq]'); if (sq) onPick(sq.dataset.sq); });
        // pointer drag: press on own piece, release on target
        let dragFrom = null;
        squares.addEventListener('pointerdown', e => {
            const sq = e.target.closest('[data-sq]');
            if (!sq || !sq.classList.contains('own')) return;
            dragFrom = sq.dataset.sq; sq.classList.add('dragging');
        });
        squares.addEventListener('pointerup', e => {
            if (!dragFrom) return;
            const el = document.elementFromPoint(e.clientX, e.clientY)?.closest('[data-sq]');
            const from = dragFrom; dragFrom = null;
            squares.querySelectorAll('.dragging').forEach(x => x.classList.remove('dragging'));
            if (el && el.dataset.sq !== from) { sel = from; onPick(el.dataset.sq); }
        });
    }

    $('promo').addEventListener('click', e => {
        const b = e.target.closest('[data-p]');
        if (b && pendingPromo) { const { from, to } = pendingPromo; pendingPromo = null; $('promo').hidden = true; sel = null; post('/move', { move: from + to + b.dataset.p }); }
        else if (e.target === $('promo')) { pendingPromo = null; $('promo').hidden = true; }
    });

    // ── connect four ──────────────────────────────────────────────────────
    function renderC4() {
        const b = state.board || { rows: 6, cols: 7, cells: [] };
        const cells = b.cells || Array.from({ length: 6 }, () => Array(7).fill(0));
        const can = myTurn();
        const win = new Set((b.winningCells || []).map(([r, c]) => `${r},${c}`));
        const colOpen = c => cells[0][c] === 0;
        let html = `<div class="c4"><div class="c4-cols">${Array.from({ length: 7 }, (_, c) =>
            `<div class="c4-col ${can && colOpen(c) ? 'can' : ''}" data-col="${c}" tabindex="0"><div class="c4-hint"><span class="disc ${mySeat() || 'p1'}"></span></div></div>`).join('')}</div><div class="c4-grid">`;
        for (let r = 0; r < 6; r++) for (let c = 0; c < 7; c++) {
            const v = cells[r][c];
            const isNew = prevCells && prevCells[r] && prevCells[r][c] === 0 && v !== 0;
            html += `<div class="cell ${can && colOpen(c) ? 'can' : ''} ${win.has(`${r},${c}`) ? 'win' : ''}" data-col="${c}">${v ? `<span class="disc p${v} ${isNew ? 'drop' : ''}"></span>` : ''}</div>`;
        }
        html += `</div></div>`;
        $('board').innerHTML = html;
        prevCells = cells.map(r => r.slice());
        $('moves').hidden = true; $('captured').hidden = true;
        $('board').querySelectorAll('[data-col]').forEach(el => el.addEventListener('click', () => { if (myTurn() && colOpen(+el.dataset.col)) post('/move', { move: String(el.dataset.col) }); }));
    }

    // ── tic tac toe ───────────────────────────────────────────────────────
    function renderTTT() {
        const b = state.board || { cells: Array(9).fill(0) };
        const cells = b.cells || Array(9).fill(0);
        const win = new Set(b.winningCells || []);
        const can = myTurn();
        $('board').innerHTML = `<div class="ttt">${cells.map((v, i) =>
            `<div class="cell ${v === 1 ? 'x' : v === 2 ? 'o' : ''} ${can && !v ? 'can' : ''} ${win.has(i) ? 'win' : ''}" data-cell="${i}">${v === 1 ? 'X' : v === 2 ? 'O' : ''}</div>`).join('')}</div>`;
        $('moves').hidden = true; $('captured').hidden = true;
        $('board').querySelectorAll('[data-cell]').forEach(el => el.addEventListener('click', () => { if (myTurn() && !cells[+el.dataset.cell]) post('/move', { move: String(el.dataset.cell) }); }));
    }

    // ── live ──────────────────────────────────────────────────────────────
    load();
    hub().then(conn => {
        if (!conn) return;
        conn.invoke('JoinMatch', id).catch(() => {});
        conn.on('MatchUpdated', mid => { if (!mid || String(mid).toLowerCase() === id.toLowerCase()) load(); });
        conn.onreconnected(() => { conn.invoke('JoinMatch', id).catch(() => {}); load(); });
    });
    setInterval(() => { if (!busy && state && state.status !== 'finished' && state.status !== 'aborted') load(); }, 10000);
})();
