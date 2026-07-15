// ============================================================
// FORSAKEN GENERATOR — RANDOM SOLVABLE FLOW 6x6
// fixed: anti-skip, layer lock, leaderboard highlight
// ============================================================

var FG_SIZE = typeof VIP_BOARD_SIZE !== 'undefined' ? VIP_BOARD_SIZE : 6;
var FG_LAYERS = 4;
var FG_MIN_PATH_CELLS = 4;

var FG_COLORS = [
    '#ff4f7b', '#e60028', '#ff8a00', '#f5dfc3',
    '#d000d8', '#f0c94a', '#1d45ff', '#00a000',
    '#eaff00', '#00d8ff', '#b16cff', '#ffffff', '#ff4040',
    '#39ff88', '#ff66d8', '#8dfffb', '#b8ff2e', '#ffb347',
    '#7a5cff', '#ff2f8a'
];

var startTime = 0;
var finishTime = 0;
var timerInt = null;
var gameReady = false;
var gameStarted = false;

var fgLayer = 1;
var fgMoves = 0;
var fgPairs = [];
var fgPaths = {};
var fgActivePair = null;
var fgMouseDown = false;
var fgLastCellKey = null;
var fgLayerLocked = false;
var fgInputCooldownUntil = 0;

// ============================================================
// TIMER
// ============================================================

function currentGenerator() {
    return typeof CURRENT_GENERATOR !== 'undefined' ? CURRENT_GENERATOR : 'forsaken';
}

function fgIsVipGenerator() {
    return currentGenerator() === 'vip';
}

function pvpMatchId() {
    try { return new URLSearchParams(location.search).get('pvpMatchId'); }
    catch (e) { return null; }
}

function submitPvpRound(ms) {
    var matchId = pvpMatchId();
    if (!matchId) return;

    fetch('/api/pvp?handler=submitRound', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ matchId: parseInt(matchId, 10), timeMs: ms })
    })
        .then(function (r) { return r.json().catch(function () { return {}; }); })
        .then(function (body) {
            var match = body && body.match;
            if (window.parent && window.parent !== window) {
                window.parent.postMessage({ type: 'pvp-round-submitted', match: match }, location.origin);
            }
            var fr = document.getElementById('finishRank');
            if (!fr || !match) return;

            var my = match.myWins || 0;
            var opp = match.opponentWins || 0;
            if (match.status === 'complete') {
                fr.textContent = (match.iWonMatch ? 'PVP матч выигран' : 'PVP матч проигран') + ' · счет ' + my + ':' + opp;
            } else if (match.opponentTimeMs) {
                fr.textContent = 'PVP раунд завершен · счет ' + my + ':' + opp + ' · вернись в PVP';
            } else {
                fr.textContent = 'PVP результат отправлен · ждем соперника';
            }
        })
        .catch(function () {
            var fr = document.getElementById('finishRank');
            if (fr) fr.textContent = 'PVP результат не отправлен';
        });
}

function startTimer() {
    stopTimer();
    startTime = Date.now();
    finishTime = 0;
    timerInt = setInterval(tickTimer, 13);
}

function stopTimer() {
    if (timerInt) clearInterval(timerInt);
    timerInt = null;
}

function tickTimer() {
    var el = document.getElementById('timerDisplay');
    if (el) el.textContent = fmt(Date.now() - startTime);
}

function fmt(ms) {
    var m = Math.floor(ms / 60000);
    var s = Math.floor((ms % 60000) / 1000);
    var x = ms % 1000;
    return p2(m) + ':' + p2(s) + ':' + p3(x);
}

function p2(n) { return String(n).padStart(2, '0'); }
function p3(n) { return String(n).padStart(3, '0'); }

// ============================================================
// START / FINISH
// ============================================================

function startGame() {
    if (window.GameSounds) window.GameSounds.stop('generatorStart');
    var sb = document.getElementById('startBar');
    if (sb) sb.style.display = 'none';

    var fo = document.getElementById('finishOverlay');
    if (fo) fo.style.display = 'none';

    var square = document.getElementById('gameStageSquare');
    if (square) square.classList.remove('is-finished');

    var td = document.getElementById('timerDisplay');
    if (td) td.textContent = '00:00:000';

    fgLayer = 1;
    fgMoves = 0;
    fgPairs = [];
    fgPaths = {};
    fgActivePair = null;
    fgMouseDown = false;
    fgLastCellKey = null;
    fgLayerLocked = false;
    fgInputCooldownUntil = 0;

    gameReady = true;
    gameStarted = true;

    startTimer();
    fgGenerateRandomLayer();
}

function completeGame() {
    stopTimer();
    if (window.GameSounds) window.GameSounds.play('generatorStart');

    gameReady = false;
    gameStarted = false;
    fgLayerLocked = true;
    fgMouseDown = false;
    fgActivePair = null;

    var ms = (finishTime || Date.now()) - startTime;
    submitPvpRound(ms);

    var square = document.getElementById('gameStageSquare');
    if (square) square.classList.add('is-finished');

    var fo = document.getElementById('finishOverlay');
    if (fo) fo.style.display = 'flex';

    var ft = document.getElementById('finishTime');
    if (ft) ft.textContent = fmt(ms);

    var fr = document.getElementById('finishRank');
    if (fr) fr.textContent = 'результат сохраняется...';

    if (pvpMatchId()) {
        if (fr) fr.textContent = 'PVP результат отправляется...';
        return;
    }

    if (typeof IS_LOGGED_IN !== 'undefined' && IS_LOGGED_IN === true) {
        fetch('/api/submit-score', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                timeMs: ms,
                generator: currentGenerator()
            })
        })
            .then(function (r) {
                return r.json().catch(function () { return {}; }).then(function (body) {
                    if (!r.ok) {
                        var err = new Error(body && body.error ? body.error : 'save failed');
                        err.status = r.status;
                        throw err;
                    }

                    return body;
                });
            })
            .then(function (body) {
                loadLeaderboard();
                loadMyScores();
                if (window.refreshPlayerLevelCard) window.refreshPlayerLevelCard();
                if (window.showAchievementUnlocks) window.showAchievementUnlocks(body.newAchievements);
                if (window.showRankUnlock) window.showRankUnlock(body.rankNotification);

                var fr2 = document.getElementById('finishRank');
                if (fr2) fr2.textContent = 'результат добавлен в таблицу';
            })
            .catch(function (err) {
                var fr2 = document.getElementById('finishRank');
                if (fr2) fr2.textContent = fgSaveErrorText(err);
            });
    }
}

function fgSaveErrorText(err) {
    if (err && err.status === 401) return 'войдите в аккаунт, чтобы сохранить результат';
    if (err && err.message === 'Suspicious time') return 'результат слишком быстрый';
    return 'не удалось сохранить результат';
}

// ============================================================
// RANDOM SOLVABLE GENERATOR
// ============================================================

function fgTargetPairsForLayer() {
    if (fgIsVipGenerator()) {
        if (fgLayer === 1) return fgRand(7, 9);
        if (fgLayer === 2) return fgRand(9, 11);
        if (fgLayer === 3) return fgRand(11, 13);
        return fgRand(13, 15);
    }

    if (fgLayer === 1) return fgRand(3, 5);
    if (fgLayer === 2) return fgRand(4, 7);
    if (fgLayer === 3) return fgRand(5, 9);
    return fgRand(6, 11);
}

function fgGenerateRandomLayer() {
    fgPairs = [];
    fgPaths = {};
    fgActivePair = null;
    fgMouseDown = false;
    fgLastCellKey = null;
    fgLayerLocked = false;
    fgInputCooldownUntil = Date.now() + 180;

    var target = fgTargetPairsForLayer();
    var best = null;

    for (var attempt = 0; attempt < 260; attempt++) {
        var puzzle = fgTryBuildPuzzle(target);

        if (puzzle && puzzle.pairs.length >= 2) {
            if (!best || puzzle.pairs.length > best.pairs.length) best = puzzle;
            if (puzzle.pairs.length >= target) break;
        }
    }

    if (!best) {
        best = fgTryBuildPuzzle(2);
    }

    if (!best || !best.pairs.length) {
        best = { pairs: fgFallbackPairs() };
    }

    fgPairs = best.pairs;
    fgPaths = {};

    for (var i = 0; i < fgPairs.length; i++) {
        fgPaths[fgPairs[i].id] = [];
    }

    fgRender();
}

function fgTryBuildPuzzle(target) {
    var occupied = {};
    var pairs = [];

    for (var id = 1; id <= target; id++) {
        var path = null;

        for (var t = 0; t < 100; t++) {
            path = fgRandomPath(occupied);
            if (path && path.length >= FG_MIN_PATH_CELLS) break;
        }

        if (!path || path.length < FG_MIN_PATH_CELLS) break;

        var a = path[0];
        var b = path[path.length - 1];

        pairs.push({
            id: id,
            color: FG_COLORS[(id - 1) % FG_COLORS.length],
            a: { x: a.x, y: a.y },
            b: { x: b.x, y: b.y },
            done: false
        });

        for (var i = 0; i < path.length; i++) {
            occupied[path[i].x + ',' + path[i].y] = true;
        }
    }

    return { pairs: pairs };
}

function fgRandomPath(occupied) {
    var start = fgRandomFreeCell(occupied);
    if (!start) return null;

    var path = [{ x: start.x, y: start.y }];
    var local = {};
    local[start.x + ',' + start.y] = true;

    var maxSteps = fgRand(FG_MIN_PATH_CELLS - 1, 7);

    for (var i = 0; i < maxSteps; i++) {
        var last = path[path.length - 1];
        var ns = fgShuffle(fgNeighbors(last.x, last.y)).filter(function (c) {
            return !occupied[c.x + ',' + c.y] && !local[c.x + ',' + c.y];
        });

        if (!ns.length) break;

        var next = ns[0];
        path.push(next);
        local[next.x + ',' + next.y] = true;
    }

    if (path.length < FG_MIN_PATH_CELLS) return null;

    var end = path[path.length - 1];
    if (fgDistance(path[0], end) < FG_MIN_PATH_CELLS - 1) return null;

    return path;
}

function fgRandomFreeCell(occupied) {
    var cells = [];

    for (var y = 0; y < FG_SIZE; y++) {
        for (var x = 0; x < FG_SIZE; x++) {
            if (!occupied[x + ',' + y]) cells.push({ x: x, y: y });
        }
    }

    if (!cells.length) return null;
    return cells[Math.floor(Math.random() * cells.length)];
}

function fgFallbackPairs() {
    return [
        {
            id: 1,
            color: FG_COLORS[0],
            a: { x: 0, y: 0 },
            b: { x: 3, y: 0 },
            done: false
        },
        {
            id: 2,
            color: FG_COLORS[1],
            a: { x: 0, y: 2 },
            b: { x: 3, y: 2 },
            done: false
        }
    ];
}

function fgNeighbors(x, y) {
    return [
        { x: x + 1, y: y },
        { x: x - 1, y: y },
        { x: x, y: y + 1 },
        { x: x, y: y - 1 }
    ].filter(function (c) {
        return c.x >= 0 && c.x < FG_SIZE && c.y >= 0 && c.y < FG_SIZE;
    });
}

function fgRand(min, max) {
    return min + Math.floor(Math.random() * (max - min + 1));
}

function fgDistance(a, b) {
    return Math.abs(a.x - b.x) + Math.abs(a.y - b.y);
}

function fgShuffle(arr) {
    var a = arr.slice();
    for (var i = a.length - 1; i > 0; i--) {
        var j = Math.floor(Math.random() * (i + 1));
        var tmp = a[i];
        a[i] = a[j];
        a[j] = tmp;
    }
    return a;
}

// ============================================================
// RENDER
// ============================================================

function fgRender() {
    var board = document.getElementById('fgBoard');
    if (!board) return;

    board.innerHTML = '';
    board.style.gridTemplateColumns = 'repeat(' + FG_SIZE + ', 1fr)';
    board.style.gridTemplateRows = 'repeat(' + FG_SIZE + ', 1fr)';

    for (var y = 0; y < FG_SIZE; y++) {
        for (var x = 0; x < FG_SIZE; x++) {
            var cell = document.createElement('div');
            cell.className = 'fg-cell';
            cell.dataset.x = x;
            cell.dataset.y = y;

            var dot = fgDotAt(x, y);

            if (dot) {
                var dotEl = document.createElement('div');
                dotEl.className = 'fg-dot';
                dotEl.style.background = dot.color;
                dotEl.style.boxShadow = '0 0 18px ' + dot.color;
                dotEl.innerHTML = '<span>' + dot.id + '</span>';
                cell.appendChild(dotEl);
            }

            board.appendChild(cell);
        }
    }

    fgBindBoardEvents();
    fgRenderLines();
    fgUpdateInfo();
}

function fgBindBoardEvents() {
    var board = document.getElementById('fgBoard');
    if (!board) return;

    board.onmousedown = function (e) {
        if (!fgCanInput()) return;
        e.preventDefault();
        fgMouseDown = true;
        fgHandlePointer(e.clientX, e.clientY);
    };

    board.onmousemove = function (e) {
        if (!fgCanInput() || !fgMouseDown) return;
        e.preventDefault();
        fgHandlePointer(e.clientX, e.clientY);
    };

    document.onmouseup = function () {
        fgStopDraw();
    };

    board.ontouchstart = function (e) {
        if (!fgCanInput()) return;
        e.preventDefault();
        fgMouseDown = true;
        var t = e.touches[0];
        fgHandlePointer(t.clientX, t.clientY);
    };

    board.ontouchmove = function (e) {
        if (!fgCanInput() || !fgMouseDown) return;
        e.preventDefault();
        var t = e.touches[0];
        fgHandlePointer(t.clientX, t.clientY);
    };

    document.ontouchend = function () {
        fgStopDraw();
    };

    document.ontouchcancel = function () {
        fgStopDraw();
    };
}

function fgCanInput() {
    return gameReady && gameStarted && !fgLayerLocked && Date.now() >= fgInputCooldownUntil;
}

function fgHandlePointer(clientX, clientY) {
    if (!fgCanInput()) return;

    var board = document.getElementById('fgBoard');
    if (!board) return;

    var rect = board.getBoundingClientRect();

    if (
        clientX < rect.left ||
        clientX > rect.right ||
        clientY < rect.top ||
        clientY > rect.bottom
    ) return;

    var cellW = rect.width / FG_SIZE;
    var cellH = rect.height / FG_SIZE;

    var x = Math.floor((clientX - rect.left) / cellW);
    var y = Math.floor((clientY - rect.top) / cellH);

    x = Math.max(0, Math.min(FG_SIZE - 1, x));
    y = Math.max(0, Math.min(FG_SIZE - 1, y));

    var key = x + ',' + y;
    if (key === fgLastCellKey) return;

    fgLastCellKey = key;
    fgHandleCell(x, y);
}

function fgRenderLines() {
    var svg = document.getElementById('fgSvg');
    var board = document.getElementById('fgBoard');
    if (!svg || !board) return;

    var rect = board.getBoundingClientRect();
    var w = rect.width;
    var h = rect.height;

    if (w <= 0 || h <= 0) return;

    svg.setAttribute('viewBox', '0 0 ' + w + ' ' + h);
    svg.innerHTML = '';

    for (var id in fgPaths) {
        var path = fgPaths[id];
        if (!path || path.length < 2) continue;

        var pair = fgPairById(Number(id));
        if (!pair) continue;

        var d = '';

        for (var i = 0; i < path.length; i++) {
            var c = fgCenter(path[i].x, path[i].y, w, h);
            d += (i === 0 ? 'M ' : ' L ') + c.x + ' ' + c.y;
        }

        var glow = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        glow.setAttribute('d', d);
        glow.setAttribute('fill', 'none');
        glow.setAttribute('stroke', pair.color);
        glow.setAttribute('stroke-width', Math.max(24, w / 15));
        glow.setAttribute('stroke-linecap', 'round');
        glow.setAttribute('stroke-linejoin', 'round');
        glow.setAttribute('opacity', '0.22');
        svg.appendChild(glow);

        var line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        line.setAttribute('d', d);
        line.setAttribute('fill', 'none');
        line.setAttribute('stroke', pair.color);
        line.setAttribute('stroke-width', Math.max(15, w / 25));
        line.setAttribute('stroke-linecap', 'round');
        line.setAttribute('stroke-linejoin', 'round');
        svg.appendChild(line);
    }
}

function fgCenter(x, y, w, h) {
    var cw = w / FG_SIZE;
    var ch = h / FG_SIZE;

    return {
        x: x * cw + cw / 2,
        y: y * ch + ch / 2
    };
}

// ============================================================
// MECHANIC
// ============================================================

function fgHandleCell(x, y) {
    if (!fgCanInput()) return;

    var dot = fgDotAt(x, y);

    if (!fgActivePair) {
        var continuation = fgIncompletePathEndingAt(x, y);
        if (continuation) {
            fgActivePair = continuation.pair;
            fgMoves++;
            fgRenderLines();
            fgUpdateInfo();
            return;
        }

        if (!dot) return;

        fgResetPair(dot.id);
        fgActivePair = fgPairById(dot.id);
        fgPaths[dot.id] = [{ x: x, y: y }];
        fgMoves++;

        fgRenderLines();
        fgUpdateInfo();
        return;
    }

    var pair = fgActivePair;
    var path = fgPaths[pair.id];

    if (!path || !path.length) return;

    var last = path[path.length - 1];
    if (last.x === x && last.y === y) return;

    var ownIndex = fgIndexInPath(path, x, y);

    if (ownIndex >= 0) {
        fgPaths[pair.id] = path.slice(0, ownIndex + 1);
        pair.done = false;
        fgRenderLines();
        fgUpdateInfo();
        return;
    }

    if (!fgIsNear(last, { x: x, y: y })) return;

    var start = path[0];
    var target = start.x === pair.a.x && start.y === pair.a.y ? pair.b : pair.a;
    var isTarget = target.x === x && target.y === y;

    var otherPath = fgPathAt(x, y);
    if (otherPath && otherPath.id !== pair.id) return;

    var otherDot = fgDotAt(x, y);
    if (otherDot && otherDot.id !== pair.id) return;
    if (otherDot && otherDot.id === pair.id && !isTarget) return;

    if (isTarget && path.length + 1 < FG_MIN_PATH_CELLS) {
        return;
    }

    path.push({ x: x, y: y });

    if (isTarget) {
        if (window.GameSounds) window.GameSounds.play('pointConnect');
        pair.done = true;
        fgActivePair = null;
        fgMouseDown = false;
        fgLastCellKey = null;
        fgInputCooldownUntil = Date.now() + 90;

        fgRenderLines();
        fgUpdateInfo();

        if (fgPairs.every(function (p) { return p.done; })) {
            fgFinishLayer();
        }

        return;
    }

    fgRenderLines();
    fgUpdateInfo();
}

function fgStopDraw() {
    fgMouseDown = false;
    fgLastCellKey = null;

    if (!fgActivePair) return;

    fgActivePair = null;
    fgRenderLines();
    fgUpdateInfo();
}

function fgFinishLayer() {
    if (fgLayerLocked) return;
    if (window.GameSounds) window.GameSounds.play('stageComplete');

    fgLayerLocked = true;
    fgMouseDown = false;
    fgActivePair = null;
    fgLastCellKey = null;
    fgInputCooldownUntil = Date.now() + 300;

    if (fgLayer >= FG_LAYERS) {
        finishTime = Date.now();
        stopTimer();
        var timerDisplay = document.getElementById('timerDisplay');
        if (timerDisplay) timerDisplay.textContent = fmt(finishTime - startTime);
    }

    var fill = document.getElementById('fgProgressFill');
    if (fill) fill.style.height = (fgLayer / FG_LAYERS * 100) + '%';

    setTimeout(function () {
        if (fgLayer >= FG_LAYERS) {
            completeGame();
            return;
        }

        fgLayer++;
        fgGenerateRandomLayer();
    }, 300);
}

// ============================================================
// HELPERS
// ============================================================

function fgPairById(id) {
    return fgPairs.find(function (p) {
        return p.id === id;
    });
}

function fgResetPair(id) {
    fgPaths[id] = [];

    var pair = fgPairById(id);
    if (pair) pair.done = false;
}

function fgDotAt(x, y) {
    for (var i = 0; i < fgPairs.length; i++) {
        var p = fgPairs[i];

        if (p.a.x === x && p.a.y === y) return { id: p.id, color: p.color };
        if (p.b.x === x && p.b.y === y) return { id: p.id, color: p.color };
    }

    return null;
}

function fgPathAt(x, y) {
    for (var id in fgPaths) {
        var path = fgPaths[id];

        for (var i = 0; i < path.length; i++) {
            if (path[i].x === x && path[i].y === y) {
                var pair = fgPairById(Number(id));
                return {
                    id: Number(id),
                    color: pair ? pair.color : '#fff'
                };
            }
        }
    }

    return null;
}

function fgIncompletePathEndingAt(x, y) {
    for (var id in fgPaths) {
        var pair = fgPairById(Number(id));
        var path = fgPaths[id];
        if (!pair || pair.done || !path || !path.length) continue;

        var last = path[path.length - 1];
        if (last.x === x && last.y === y) {
            return { id: Number(id), pair: pair, path: path };
        }
    }

    return null;
}

function fgIndexInPath(path, x, y) {
    for (var i = 0; i < path.length; i++) {
        if (path[i].x === x && path[i].y === y) return i;
    }

    return -1;
}

function fgIsNear(a, b) {
    return Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;
}

function fgUpdateInfo() {
    var done = fgPairs.filter(function (p) { return p.done; }).length;
    var total = fgPairs.length;

    var flows = document.getElementById('fgFlows');
    if (flows) flows.textContent = done + '/' + total;

    var moves = document.getElementById('fgMoves');
    if (moves) moves.textContent = fgMoves;

    var pipe = document.getElementById('fgPipe');
    if (pipe) pipe.textContent = total ? Math.round(done / total * 100) + '%' : '0%';

    var layer = document.getElementById('fgLayer');
    if (layer) layer.textContent = 'LAYER ' + fgLayer + ' / ' + FG_LAYERS;
}

// ============================================================
// LEADERBOARD
// ============================================================

function loadLeaderboard() {
    fetch('/api/leaderboard?generator=' + encodeURIComponent(currentGenerator()))
        .then(function (r) { return r.json(); })
        .then(renderLB)
        .catch(function () { });
}

function renderLB(data) {
    var list = Array.isArray(data) ? data : (data && Array.isArray(data.top) ? data.top : []);
    var myRankFromServer = data && typeof data.myRank === 'number' ? data.myRank : null;

    var el = document.getElementById('lbList');
    if (!el) return;

    if (!list || !list.length) {
        el.innerHTML = '<div class="lb-empty">Пока никто не играл</div>';
        renderMyRankRow(null, myRankFromServer);
        return;
    }

    var me = typeof CURRENT_USER !== 'undefined' ? CURRENT_USER : '';

    el.innerHTML = list.map(function (e, i) {
        var rc = i === 0 ? 'gold' : i === 1 ? 'silver' : i === 2 ? 'bronze' : '';
        var displayName = (e.isVip ? '\u265B ' : '') + (e.robloxUsername || e.username || 'Player') + (e.diamondEmoji ? ' 💎' : '');

        var av = e.robloxAvatarUrl
            ? '<img src="' + e.robloxAvatarUrl + '" alt=""/>'
            : '<span class="lb-av-txt">' + displayName[0].toUpperCase() + '</span>';

        var isMe =
            (e.username || '').toLowerCase() === String(me).toLowerCase() ||
            (e.robloxUsername || '').toLowerCase() === String(me).toLowerCase();

        var nm = '<button type="button" class="lb-profile-btn' + (isMe ? ' is-me' : '') + (e.isVip ? ' is-vip' : '') + (e.rainbowName ? ' is-rainbow' : '') + '" onclick="openLeaderboardProfile(' + (e.id || 0) + ')">' + displayName + '</button>';

        return '<div class="lb-row' + (isMe ? ' lb-me' : '') + '">' +
            '<div class="lb-num ' + rc + '">' + (i + 1) + '</div>' +
            '<div class="lb-av">' + av + '</div>' +
            '<div class="lb-name">' + nm + '</div>' +
            '<div class="lb-time">' + e.timeFormatted + '</div>' +
            '</div>';
    }).join('');

    var myIndex = list.findIndex(function (e) {
        return (e.username || '').toLowerCase() === String(me).toLowerCase() ||
            (e.robloxUsername || '').toLowerCase() === String(me).toLowerCase();
    });

    renderMyRankRow(myIndex >= 0 ? { index: myIndex, entry: list[myIndex] } : null, myRankFromServer);
}

function renderMyRankRow(found, myRankFromServer) {
    var mr = document.getElementById('myRow');
    if (!mr) return;

    if (found) {
        var e = found.entry;
        var displayName = (e.robloxUsername || e.username || 'Player') + (e.diamondEmoji ? ' 💎' : '');

        mr.innerHTML =
            '<div class="lb-my-inner">' +
            '<div class="lb-num" style="color:#e05050">' + (found.index + 1) + '</div>' +
            '<div class="lb-av"><span class="lb-av-txt">' + displayName[0].toUpperCase() + '</span></div>' +
            '<div class="lb-name" style="color:#e05050">' + displayName + '</div>' +
            '<div class="lb-time" style="color:#e05050">' + e.timeFormatted + '</div>' +
            '</div><div class="lb-my-lbl">твоё место</div>';
        return;
    }

    if (myRankFromServer && myRankFromServer > 100) {
        mr.innerHTML =
            '<div class="lb-my-inner">' +
            '<div class="lb-num" style="color:#e05050">#' + myRankFromServer + '</div>' +
            '<div class="lb-name" style="color:#e05050">ты вне TOP 100</div>' +
            '</div><div class="lb-my-lbl">твоё место после топа</div>';
        return;
    }

    mr.innerHTML = '';
}

function loadMyScores() {
    fetch('/api/my-scores?generator=' + encodeURIComponent(currentGenerator()))
        .then(function (r) { return r.json(); })
        .then(function (d) {
            var data = d && typeof d === 'object' && !Array.isArray(d)
                ? d
                : {
                    username: typeof CURRENT_USER !== 'undefined' ? CURRENT_USER : 'Игрок',
                    scores: Array.isArray(d) ? d : []
                };

            var username = data.username || 'Игрок';
            var scores = Array.isArray(data.scores) ? data.scores : [];

            scores = scores.slice().sort(function (a, b) {
                return (a.timeMs || 0) - (b.timeMs || 0);
            });

            var best = document.getElementById('ppBest');
            if (best) best.textContent = scores.length ? 'лучший: ' + scores[0].timeFormatted : '—';

            var user = document.getElementById('ppUser');
            if (user) user.textContent = username;

            var list = document.getElementById('ppList');
            if (!list) return;

            if (!scores.length) {
                list.innerHTML = '<div class="pp-empty">Пока нет попыток</div>';
                return;
            }

            list.innerHTML = scores.map(function (s, i) {
                return '<div class="pp-row">' +
                    '<span>#' + (i + 1) + '</span>' +
                    '<span>' + username + '</span>' +
                    '<span>' + s.timeFormatted + '</span>' +
                    '</div>';
            }).join('');
        })
        .catch(function () { });
}

// ============================================================
// INIT
// ============================================================

window.addEventListener('resize', function () {
    fgRenderLines();
});

document.addEventListener('DOMContentLoaded', function () {
    loadLeaderboard();

    if (typeof IS_LOGGED_IN !== 'undefined' && IS_LOGGED_IN === true) {
        loadMyScores();
    }

    fgRender();
});
