// /Games/Wordle: daily + practice boards, on-screen keyboard, stats, leaderboard.
(function () {
    const { api, esc, chip, toast, empty, UID } = window.G;
    const $ = s => document.getElementById(s);
    const ROWS = 6, COLS = 5;
    const KEYS = ['QWERTYUIOP', 'ASDFGHJKL', 'ENTER ZXCVBNM BACK'];

    let mode = 'daily';          // daily | practice | stats
    let game = null;             // WordleState for the active board
    let typed = '';              // current unsubmitted guess
    let busy = false;
    let lastRenderedGuesses = 0;

    // ── board ─────────────────────────────────────────────────────────────
    function buildGrid() {
        $('w-grid').innerHTML = Array.from({ length: ROWS }, (_, r) =>
            `<div class="w-row" data-r="${r}">${Array.from({ length: COLS }, (_, c) => `<div class="w-tile" data-r="${r}" data-c="${c}"></div>`).join('')}</div>`).join('');
    }

    function buildKeys() {
        $('w-keys').innerHTML = KEYS.map(row => `<div class="w-keyrow">${row.split(' ').map(seg => {
            if (seg === 'ENTER') return `<button class="w-key wide" data-k="ENTER">Enter</button>`;
            if (seg === 'BACK') return `<button class="w-key wide" data-k="BACK">⌫</button>`;
            return [...seg].map(k => `<button class="w-key" data-k="${k}">${k}</button>`).join('');
        }).join('')}</div>`).join('');
    }

    function paint(animateLast) {
        const g = game;
        const guesses = g ? g.guesses : [];
        for (let r = 0; r < ROWS; r++) {
            const guess = guesses[r];
            for (let c = 0; c < COLS; c++) {
                const t = $('w-grid').querySelector(`.w-tile[data-r="${r}"][data-c="${c}"]`);
                t.className = 'w-tile';
                if (guess) {
                    t.textContent = guess.word[c];
                    const mark = guess.marks[c];
                    if (animateLast && r === guesses.length - 1) {
                        t.style.animationDelay = `${c * 90}ms`;
                        t.classList.add('flip');
                        setTimeout(() => { t.classList.add(mark); }, c * 90 + 250);
                    } else { t.classList.add(mark); t.style.animationDelay = ''; }
                } else if (r === guesses.length && !g?.finished) {
                    t.textContent = typed[c] || '';
                    if (typed[c]) t.classList.add('filled');
                } else t.textContent = '';
            }
        }
        // key colors: G beats Y beats B
        const best = {};
        for (const gs of guesses) for (let i = 0; i < COLS; i++) {
            const L = gs.word[i], m = gs.marks[i];
            const rank = { G: 3, Y: 2, B: 1 }[m] || 0;
            if (!best[L] || rank > best[L].rank) best[L] = { rank, m };
        }
        $('w-keys').querySelectorAll('.w-key').forEach(k => {
            k.classList.remove('G', 'Y', 'B');
            const b = best[k.dataset.k]; if (b) k.classList.add(b.m);
        });
        // caption + done card
        const cap = $('w-caption');
        if (!g) cap.textContent = '';
        else if (g.mode === 'daily') cap.textContent = `Daily puzzle · ${g.date} · everyone gets the same word · ${g.guesses.length}/${g.maxGuesses} guesses`;
        else cap.textContent = `Practice · unlimited · ${g.guesses.length}/${g.maxGuesses} guesses`;
        const done = $('w-done');
        if (g && g.finished) {
            done.hidden = false;
            const n = g.guesses.length;
            const head = g.solved ? ['Genius', 'Magnificent', 'Impressive', 'Splendid', 'Great', 'Phew'][n - 1] || 'Solved' : 'So close';
            const streak = g.mode === 'daily' ? `<div class="w-sub">Streak: <b>${g.streak}</b> day${g.streak === 1 ? '' : 's'} · come back tomorrow for the next one</div>` : '';
            done.innerHTML = `<div><b>${head}</b>${g.solved ? ` — in ${n}` : ''}</div><div class="w-answer">${esc(g.answer || '')}</div>${streak}<button class="g-btn" id="w-share">Copy result</button>`;
            $('w-share').onclick = () => {
                const grid = g.guesses.map(x => [...x.marks].map(m => m === 'G' ? '🟩' : m === 'Y' ? '🟨' : '⬛').join('')).join('\n');
                const text = `Torvex Wordle ${g.mode === 'daily' ? g.date : 'practice'} ${g.solved ? n : 'X'}/${g.maxGuesses}\n${grid}\nhttps://torvex.app/Games/Wordle`;
                navigator.clipboard.writeText(text).then(() => toast('Copied — paste it anywhere')).catch(() => toast('Copy failed', true));
            };
        } else done.hidden = true;
        $('w-practice-row').hidden = mode !== 'practice';
    }

    function msg(text) {
        const m = $('w-msg'); m.textContent = text; m.hidden = false;
        clearTimeout(msg.t); msg.t = setTimeout(() => m.hidden = true, 1400);
    }

    function shakeRow() {
        const row = $('w-grid').querySelector(`.w-row[data-r="${game ? game.guesses.length : 0}"]`);
        if (!row) return; row.classList.remove('shake'); void row.offsetWidth; row.classList.add('shake');
    }

    async function key(k) {
        if (busy || !game || game.finished) return;
        if (k === 'BACK') { typed = typed.slice(0, -1); paint(); return; }
        if (k === 'ENTER') {
            if (typed.length < COLS) { msg('Not enough letters'); shakeRow(); return; }
            busy = true;
            try {
                const s = await api('/wordle/guess', { method: 'POST', body: { word: typed, practice: mode === 'practice' } });
                game = s; typed = '';
                paint(true);
                if (s.finished) setTimeout(() => { if (s.mode === 'daily') loadStats(); }, 800);
            } catch (e) { msg(e.message); shakeRow(); }
            finally { busy = false; }
            return;
        }
        if (/^[A-Z]$/.test(k) && typed.length < COLS) { typed += k; paint(); }
    }

    $('w-keys').addEventListener('click', e => { const b = e.target.closest('[data-k]'); if (b) key(b.dataset.k); });
    document.addEventListener('keydown', e => {
        if (mode === 'stats' || e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.key === 'Enter') key('ENTER');
        else if (e.key === 'Backspace') key('BACK');
        else if (/^[a-zA-Z]$/.test(e.key)) key(e.key.toUpperCase());
    });

    // ── modes ─────────────────────────────────────────────────────────────
    async function loadBoard() {
        typed = '';
        try {
            if (mode === 'daily') game = await api('/wordle/today');
            else {
                game = await api('/wordle/practice');
                if (!game) game = await api('/wordle/practice', { method: 'POST' });
            }
            paint(false);
        } catch (e) { toast(e.message, true); }
    }

    $('w-new').addEventListener('click', async () => {
        try { game = await api('/wordle/practice', { method: 'POST' }); typed = ''; paint(false); } catch (e) { toast(e.message, true); }
    });

    $('mode-tabs').addEventListener('click', e => {
        const b = e.target.closest('[data-mode]'); if (!b) return;
        mode = b.dataset.mode;
        $('mode-tabs').querySelectorAll('.g-tab').forEach(t => t.classList.toggle('active', t === b));
        $('pane-game').hidden = mode === 'stats';
        $('pane-stats').hidden = mode !== 'stats';
        if (mode === 'stats') { loadStats(); loadBoardList(); } else loadBoard();
    });

    async function loadStats() {
        try {
            const s = await api('/wordle/stats');
            const pct = s.played ? Math.round(100 * s.solved / s.played) : 0;
            const dist = s.distribution || [0, 0, 0, 0, 0, 0];
            const max = Math.max(1, ...dist);
            const lastN = game && game.mode === 'daily' && game.finished && game.solved ? game.guesses.length : 0;
            $('w-stats').innerHTML = `<div class="w-stat-grid">
                <div class="w-stat"><div class="n">${s.played}</div><div class="l">Played</div></div>
                <div class="w-stat"><div class="n">${pct}</div><div class="l">Win %</div></div>
                <div class="w-stat"><div class="n">${s.streak}</div><div class="l">Streak</div></div>
                <div class="w-stat"><div class="n">${s.maxStreak}</div><div class="l">Max streak</div></div></div>
                <div class="g-label">Guess distribution</div>
                <div class="w-dist">${dist.map((n, i) => `<div class="w-dist-row"><span>${i + 1}</span><div class="w-dist-bar ${i + 1 === lastN ? 'hi' : ''}" style="width:${Math.max(8, 100 * n / max)}%">${n}</div></div>`).join('')}</div>`;
        } catch (e) { $('w-stats').innerHTML = empty(e.message); }
    }

    async function loadBoardList() {
        try {
            const rows = await api('/wordle/leaderboard?days=30');
            if (!rows.length) { $('w-board').innerHTML = empty('Nobody has finished a daily puzzle in the last 30 days.'); return; }
            $('w-board').innerHTML = `<table class="g-table"><thead><tr><th>#</th><th>Player</th><th class="num">Solved</th><th class="num">Avg</th><th class="num">Streak</th></tr></thead><tbody>${
                rows.map((r, i) => `<tr class="${r.player?.userId?.toLowerCase() === UID ? 'me' : ''}"><td class="g-rank r${i + 1}">${i + 1}</td><td>${chip(r.player)}</td><td class="num">${r.solved}/${r.played}</td><td class="num">${r.avgGuesses != null ? Number(r.avgGuesses).toFixed(2) : '—'}</td><td class="num">${r.streak}</td></tr>`).join('')
            }</tbody></table>`;
        } catch (e) { $('w-board').innerHTML = empty(e.message); }
    }

    buildGrid(); buildKeys(); loadBoard();
})();
