(function () {
    var selectedGenerator = null;
    var activeMatch = null;
    var pollTimer = null;
    var countdownTimer = null;
    var activeRoundKey = null;
    var lastFoundMatchId = null;
    var autoReadyKeys = {};

    function qs(sel) { return document.querySelector(sel); }
    function qsa(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }

    function api(handler, method, body) {
        return fetch('/api/pvp?handler=' + encodeURIComponent(handler), {
            method: method || 'GET',
            headers: method === 'POST' ? { 'Content-Type': 'application/json' } : {},
            body: body ? JSON.stringify(body) : undefined
        }).then(function (r) {
            return r.json().catch(function () { return {}; }).then(function (data) {
                if (!r.ok) throw data;
                return data;
            });
        });
    }

    function generatorTitle(generator) {
        return generator === 'forsaken' ? 'Forsaken' : 'Bite by Night';
    }

    function generatorUrl(generator, matchId) {
        var path = generator === 'forsaken' ? '/generators/forsaken' : '/generators/bitebynight';
        return path + '?pvpMatchId=' + encodeURIComponent(matchId) + '&embedded=1';
    }

    function setStatus(text) {
        var el = qs('#pvpStatus');
        if (el) el.textContent = text;
    }

    function setTitle(text) {
        var el = qs('#pvpPanelTitle');
        if (el) el.textContent = text;
    }

    function fmtTime(ms) {
        if (!ms) return '—';
        var m = Math.floor(ms / 60000);
        var s = Math.floor((ms % 60000) / 1000);
        var x = ms % 1000;
        return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0') + '.' + String(x).padStart(3, '0');
    }

    function playFound() {
        if (window.GameSounds) window.GameSounds.play('found');
    }

    function ensureOverlayExtras() {
        var card = qs('.pvp-countdown-card');
        if (!card) return {};

        var hint = qs('#pvpCountdownHint') || card.querySelector('small');
        if (hint) hint.id = 'pvpCountdownHint';

        var progress = qs('#pvpVictoryProgress');
        if (!progress) {
            progress = document.createElement('div');
            progress.id = 'pvpVictoryProgress';
            progress.className = 'pvp-overlay-progress';
            progress.hidden = true;
            progress.innerHTML =
                '<div><span id="pvpVictoryRank"></span><b id="pvpVictoryNext"></b></div>' +
                '<div class="pvp-overlay-progress-bar"><i id="pvpVictoryProgressBar"></i></div>';
            card.appendChild(progress);
        }

        var cancel = qs('#pvpOverlayCancelBtn');
        if (!cancel) {
            cancel = document.createElement('button');
            cancel.type = 'button';
            cancel.id = 'pvpOverlayCancelBtn';
            cancel.className = 'pvp-overlay-btn';
            cancel.textContent = 'ОТМЕНИТЬ ПОИСК';
            cancel.hidden = true;
            card.appendChild(cancel);
            cancel.addEventListener('click', cancelSearch);
        }

        var exit = qs('#pvpOverlayExitBtn');
        if (!exit) {
            exit = document.createElement('button');
            exit.type = 'button';
            exit.id = 'pvpOverlayExitBtn';
            exit.className = 'pvp-overlay-btn is-gold';
            exit.textContent = 'ВЫЙТИ НА ГЛАВНЫЙ ЭКРАН';
            exit.hidden = true;
            card.appendChild(exit);
            exit.addEventListener('click', function () {
                location.href = '/pvp';
            });
        }

        var confetti = qs('#pvpConfetti');
        if (!confetti) {
            confetti = document.createElement('div');
            confetti.id = 'pvpConfetti';
            confetti.className = 'pvp-confetti';
            confetti.hidden = true;
            for (var i = 0; i < 20; i++) {
                var piece = document.createElement('i');
                piece.style.setProperty('--x', (Math.random() * 100).toFixed(1) + '%');
                piece.style.setProperty('--d', (0.7 + Math.random() * 1.2).toFixed(2) + 's');
                piece.style.setProperty('--c', ['#f8db7a', '#73ff98', '#8fd5ff', '#ff6b6b'][i % 4]);
                confetti.appendChild(piece);
            }
            card.appendChild(confetti);
        }

        return { hint: hint, progress: progress, cancel: cancel, exit: exit, confetti: confetti };
    }

    function setOverlay(mode, options) {
        options = options || {};
        var box = qs('#pvpCountdown');
        var label = qs('#pvpCountdownLabel');
        var value = qs('#pvpCountdownValue');
        var extra = ensureOverlayExtras();
        if (!box || !label || !value) return;

        if (!mode || mode === 'hidden') {
            box.hidden = true;
            box.className = 'pvp-countdown';
            if (extra.progress) extra.progress.hidden = true;
            if (extra.cancel) extra.cancel.hidden = true;
            if (extra.exit) extra.exit.hidden = true;
            if (extra.confetti) extra.confetti.hidden = true;
            return;
        }

        box.hidden = false;
        box.className = 'pvp-countdown mode-' + mode;
        label.textContent = options.label || '';
        value.textContent = options.value || '';
        if (extra.hint) extra.hint.textContent = options.hint || '';
        if (extra.cancel) extra.cancel.hidden = !options.cancel;
        if (extra.exit) extra.exit.hidden = !options.exit;
        if (extra.confetti) extra.confetti.hidden = mode !== 'victory' || !options.win;

        if (extra.progress) {
            extra.progress.hidden = !options.progress;
            if (options.progress) {
                var rank = qs('#pvpVictoryRank');
                var next = qs('#pvpVictoryNext');
                var bar = qs('#pvpVictoryProgressBar');
                if (rank) rank.textContent = options.rankText || '';
                if (next) next.textContent = options.nextText || '';
                if (bar) bar.style.width = Math.max(0, Math.min(100, options.progressPercent || 0)) + '%';
            }
        }
    }

    function setSearching(isSearching, queueCount) {
        var box = qs('#pvpSearching');
        var count = qs('#pvpQueueCount');
        if (box) box.hidden = true;
        if (count) count.textContent = String(queueCount || 0);
        if (isSearching) {
            setOverlay('search', {
                label: 'ИДЕТ ПОИСК',
                value: '...',
                hint: 'Игроков в очереди: ' + (queueCount || 0),
                cancel: true
            });
        }
    }

    function setGameFrame(show, match) {
        var wrap = qs('#pvpGameFrameWrap');
        var frame = qs('#pvpGameFrame');
        if (!wrap || !frame) return;

        wrap.hidden = !show;
        if (show && match) {
            var src = generatorUrl(match.generator, match.id);
            if (frame.getAttribute('src') !== src) frame.setAttribute('src', src);
        }
        if (!show) frame.removeAttribute('src');
    }

    function renderRoundResults(match) {
        var box = qs('#pvpRoundResults');
        var my = qs('#pvpMyResult');
        var opp = qs('#pvpOpponentResult');
        var oppName = qs('#pvpOpponentResultName');
        if (!box || !my || !opp) return;

        var hasAny = !!(match && (match.myTimeMs || match.opponentTimeMs));
        box.hidden = !hasAny;
        if (!hasAny) return;

        if (oppName) oppName.textContent = match.opponentName || 'Соперник';
        my.textContent = fmtTime(match.myTimeMs);
        opp.textContent = fmtTime(match.opponentTimeMs);
    }

    function renderDots(myWins, opponentWins) {
        var mine = qs('#pvpMyDots');
        var opp = qs('#pvpOpponentDots');
        if (!mine || !opp) return;

        mine.innerHTML = '';
        opp.innerHTML = '';
        for (var i = 0; i < 2; i++) {
            mine.innerHTML += '<span class="' + (i < myWins ? 'is-win' : '') + '"></span>';
            opp.innerHTML += '<span class="' + (i < opponentWins ? 'is-lose' : '') + '"></span>';
        }
    }

    function renderRatings(ratings) {
        if (!Array.isArray(ratings)) return;
        ratings.forEach(function (rating) {
            var card = document.querySelector('.pvp-generator-card[data-generator="' + rating.generator + '"]');
            if (!card) return;
            var cups = card.querySelector('.pvp-cups');
            if (cups) cups.textContent = rating.points + ' 🏆';
            var line = card.querySelector('.pvp-rank-line');
            if (line) line.className = 'pvp-rank-line rank-' + rating.rankCss;
            var icon = card.querySelector('.pvp-rank-icon');
            if (icon) icon.textContent = rating.rankIcon;
            var rankName = card.querySelector('.pvp-rank-badge span:not(.pvp-rank-icon)');
            if (rankName) rankName.textContent = rating.rank;
            var rankPoints = card.querySelector('.pvp-rank-badge div small');
            if (rankPoints) rankPoints.textContent = rating.points + ' кубков';
        });
    }

    function maybeAutoReady(match) {
        if (!match || match.myReady) return;
        if (match.currentRound !== 1) return;

        var key = match.id + ':' + match.currentRound;
        if (autoReadyKeys[key]) return;
        autoReadyKeys[key] = true;

        setTimeout(function () {
            if (!activeMatch || activeMatch.id !== match.id || activeMatch.status !== 'waiting_ready') return;
            api('ready', 'POST', { matchId: match.id })
                .then(function (data) {
                    renderMatch(data.match);
                    startPolling();
                    setTimeout(poll, 250);
                })
                .catch(function () {
                    setOverlay('hidden');
                    setStatus('Не удалось подтвердить готовность. Нажми готов к игре вручную.');
                    var ready = qs('#pvpReadyBtn');
                    if (ready) ready.hidden = false;
                });
        }, 1600);
    }

    function renderMatch(match) {
        activeMatch = match || null;
        var score = qs('#pvpScore');
        var ready = qs('#pvpReadyBtn');
        var cancel = qs('#pvpCancelBtn');
        var exit = qs('#pvpExitBtn');
        var opponentName = qs('#pvpOpponentName');

        if (!match) {
            if (score) score.hidden = true;
            if (ready) ready.hidden = true;
            if (exit) exit.hidden = true;
            if (cancel) cancel.hidden = selectedGenerator == null;
            setGameFrame(false);
            renderRoundResults(null);
            return;
        }

        selectedGenerator = match.generator;
        setTitle(generatorTitle(match.generator) + ' PVP');
        if (score) score.hidden = false;
        if (opponentName) opponentName.textContent = match.opponentName || 'Соперник';
        renderDots(match.myWins || 0, match.opponentWins || 0);
        renderRoundResults(match);

        if (match.id !== lastFoundMatchId) {
            lastFoundMatchId = match.id;
            playFound();
        }

        if (match.status === 'waiting_ready') {
            activeRoundKey = null;
            setGameFrame(false);
            if (exit) exit.hidden = false;
            if (cancel) cancel.hidden = true;

            if (match.currentRound === 1 && !match.myReady) {
                if (ready) ready.hidden = true;
                setStatus('Соперник найден: ' + match.opponentName + '. Готовим матч...');
                setOverlay('found', {
                    label: 'СОПЕРНИК НАЙДЕН',
                    value: match.opponentName || 'Соперник',
                    hint: generatorTitle(match.generator) + ' PVP. Скоро начнется отсчет.'
                });
                maybeAutoReady(match);
                return;
            }

            if (ready) {
                ready.hidden = false;
                ready.disabled = !!match.myReady;
                ready.textContent = match.myReady ? 'ЖДЕМ СОПЕРНИКА' : 'ГОТОВ К ' + match.currentRound + ' РАУНДУ';
            }

            if (match.myReady) {
                setStatus('Ждем готовность соперника. Если он не ответит 2 минуты, победа будет за тобой.');
                setOverlay('wait', {
                    label: 'ЖДЕМ СОПЕРНИКА',
                    value: '...',
                    hint: 'Если соперник пропал, система отдаст победу тебе через 2 минуты.'
                });
            } else {
                setOverlay('hidden');
                setStatus('Раунд завершен. Нажми готов, когда будешь готов к следующему раунду.');
            }
            return;
        }

        if (match.status === 'in_round') {
            if (ready) ready.hidden = true;
            if (exit) exit.hidden = false;
            if (cancel) cancel.hidden = true;

            if (activeRoundKey === match.id + ':' + match.currentRound) {
                if (match.myTimeMs) {
                    setGameFrame(false);
                    if (!match.opponentTimeMs) {
                        setStatus('Твой результат отправлен. Ждем соперника до 2 минут.');
                        setOverlay('wait', {
                            label: 'РЕЗУЛЬТАТ ОТПРАВЛЕН',
                            value: fmtTime(match.myTimeMs),
                            hint: 'Ждем результат соперника.'
                        });
                    } else {
                        setOverlay('hidden');
                    }
                } else {
                    setOverlay('hidden');
                    setGameFrame(true, match);
                }
                return;
            }

            startCountdown(match);
            return;
        }

        if (match.status === 'complete') {
            if (ready) ready.hidden = true;
            if (exit) exit.hidden = true;
            if (cancel) cancel.hidden = true;
            setGameFrame(false);
            renderVictory(match);
        }
    }

    function startCountdown(match) {
        activeRoundKey = match.id + ':' + match.currentRound;
        var n = 5;
        if (countdownTimer) clearInterval(countdownTimer);
        setGameFrame(false);
        setStatus('Раунд ' + match.currentRound + ' начинается через ' + n);
        setOverlay('countdown', {
            label: 'РАУНД ' + match.currentRound,
            value: n,
            hint: 'После отсчета блюр спадет и генератор откроется прямо здесь.'
        });

        countdownTimer = setInterval(function () {
            n--;
            if (n <= 0) {
                clearInterval(countdownTimer);
                setOverlay('hidden');
                setGameFrame(true, match);
                setStatus('Раунд ' + match.currentRound + ' идет. Пройди генератор быстрее соперника.');
                return;
            }

            setOverlay('countdown', {
                label: 'РАУНД ' + match.currentRound,
                value: n,
                hint: 'После отсчета блюр спадет и генератор откроется прямо здесь.'
            });
            setStatus('Раунд ' + match.currentRound + ' начинается через ' + n);
        }, 1000);
    }

    function renderVictory(match) {
        var delta = Number(match.myCupDelta || 0);
        var positive = delta >= 0;
        var nextText = match.nextRank
            ? (match.pointsToNextRank + ' кубков до ' + match.nextRank)
            : 'Максимальное звание';

        setStatus(match.iWonMatch ? 'Матч выигран. Кубки начислены.' : 'Матч проигран. Можно взять реванш.');
        setOverlay('victory', {
            label: match.iWonMatch ? 'УРА, ПОБЕДА!' : 'МАТЧ ЗАВЕРШЕН',
            value: (positive ? '+' : '') + delta + ' кубков',
            hint: match.iWonMatch ? 'Красиво забрал матч. Кубки уже в рейтинге.' : 'Кубки списаны. Следующий матч можно отыграть.',
            progress: true,
            rankText: 'Сейчас: ' + (match.myRank || 'Дерево') + ' · ' + (match.myPoints || 0) + ' кубков',
            nextText: nextText,
            progressPercent: match.rankProgressPercent || 0,
            exit: true,
            win: !!match.iWonMatch
        });
    }

    function poll() {
        var suffix = selectedGenerator ? '&generator=' + encodeURIComponent(selectedGenerator) : '';
        fetch('/api/pvp?handler=status' + suffix)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                renderRatings(data.ratings);
                if (data.match) {
                    renderMatch(data.match);
                } else if (selectedGenerator) {
                    setStatus('Идет поиск соперника в твоем звании...');
                    setSearching(true, data.queueCount || 0);
                }
            })
            .catch(function () { });
    }

    function startPolling() {
        if (pollTimer) return;
        pollTimer = setInterval(poll, 900);
    }

    function startSearch(generator) {
        if (window.GameSounds && window.GameSounds.unlock) window.GameSounds.unlock();
        selectedGenerator = generator;
        activeRoundKey = null;
        activeMatch = null;
        setTitle(generatorTitle(selectedGenerator) + ' PVP');
        setStatus('Идет поиск соперника в твоем звании...');
        setSearching(true, 0);
        var cancel = qs('#pvpCancelBtn');
        if (cancel) cancel.hidden = false;

        api('search', 'POST', { generator: selectedGenerator })
            .then(function (data) {
                if (data.match) renderMatch(data.match);
                else setSearching(true, data.result ? data.result.queueCount : 0);
                startPolling();
            })
            .catch(function () {
                setOverlay('hidden');
                setStatus('Не удалось начать поиск. Войди в аккаунт и попробуй еще раз.');
            });
    }

    function cancelSearch() {
        if (!selectedGenerator) return;
        api('cancel', 'POST', { generator: selectedGenerator })
            .then(function () {
                selectedGenerator = null;
                activeMatch = null;
                setOverlay('hidden');
                setTitle('Выбери генератор для PVP');
                setStatus('Очередь подбирает игрока твоего звания или соседнего звания.');
                var cancel = qs('#pvpCancelBtn');
                if (cancel) cancel.hidden = true;
            });
    }

    qsa('.pvp-search-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            startSearch(btn.getAttribute('data-generator'));
        });
    });

    var readyBtn = qs('#pvpReadyBtn');
    if (readyBtn) {
        readyBtn.addEventListener('click', function () {
            if (!activeMatch) return;
            readyBtn.disabled = true;
            api('ready', 'POST', { matchId: activeMatch.id })
                .then(function (data) {
                    renderMatch(data.match);
                    startPolling();
                    setTimeout(poll, 250);
                    setTimeout(poll, 900);
                })
                .catch(function () { setStatus('Не удалось подтвердить готовность.'); })
                .finally(function () { readyBtn.disabled = false; });
        });
    }

    var cancelBtn = qs('#pvpCancelBtn');
    if (cancelBtn) cancelBtn.addEventListener('click', cancelSearch);

    var exitBtn = qs('#pvpExitBtn');
    if (exitBtn) {
        exitBtn.addEventListener('click', function () {
            if (!activeMatch) {
                location.href = '/pvp';
                return;
            }

            if (!confirm('Выйти из PVP матча? Сопернику засчитается победа.')) return;

            api('forfeit', 'POST', { matchId: activeMatch.id })
                .then(function (data) {
                    setGameFrame(false);
                    renderMatch(data.match);
                })
                .catch(function () {
                    setStatus('Не удалось выйти из матча.');
                });
        });
    }

    window.addEventListener('message', function (event) {
        if (event.origin !== location.origin) return;
        if (!event.data || event.data.type !== 'pvp-round-submitted') return;
        setGameFrame(false);
        if (event.data.match) renderMatch(event.data.match);
        setTimeout(poll, 300);
    });

    var inviteBtn = qs('#pvpInviteBtn');
    if (inviteBtn) {
        inviteBtn.addEventListener('click', function () {
            var nick = qs('#pvpInviteNick');
            var gen = qs('#pvpInviteGenerator');
            var status = qs('#pvpInviteStatus');
            var nickname = nick ? nick.value.trim() : '';
            var generator = gen ? gen.value : 'bitebynight';
            if (!nickname) {
                if (status) status.textContent = 'Введите ник игрока';
                return;
            }

            inviteBtn.disabled = true;
            if (status) status.textContent = 'Отправляем приглашение...';
            api('invite', 'POST', { nickname: nickname, generator: generator })
                .then(function (data) {
                    selectedGenerator = generator;
                    if (status) status.textContent = data.message || 'Приглашение отправлено';
                    startPolling();
                })
                .catch(function () {
                    if (status) status.textContent = 'Не удалось отправить приглашение';
                })
                .finally(function () {
                    inviteBtn.disabled = false;
                });
        });
    }

    ensureOverlayExtras();
    poll();
})();
