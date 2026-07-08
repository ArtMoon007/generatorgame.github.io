(function () {
    var selectedGenerator = null;
    var activeMatch = null;
    var pollTimer = null;
    var redirecting = false;
    var countdownTimer = null;
    var activeRoundKey = null;

    function qs(sel) { return document.querySelector(sel); }
    function qsa(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }

    function api(handler, method, body) {
        var url = '/api/pvp?handler=' + encodeURIComponent(handler);
        return fetch(url, {
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

    function setSearching(isSearching, queueCount) {
        var box = qs('#pvpSearching');
        var count = qs('#pvpQueueCount');
        if (box) box.hidden = !isSearching;
        if (count) count.textContent = String(queueCount || 0);
    }

    function setCountdown(show, label, value) {
        var box = qs('#pvpCountdown');
        var lbl = qs('#pvpCountdownLabel');
        var val = qs('#pvpCountdownValue');
        if (box) box.hidden = !show;
        if (lbl && label) lbl.textContent = label;
        if (val && value != null) val.textContent = String(value);
    }

    function fmtTime(ms) {
        if (!ms) return '—';
        var m = Math.floor(ms / 60000);
        var s = Math.floor((ms % 60000) / 1000);
        var x = ms % 1000;
        return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0') + '.' + String(x).padStart(3, '0');
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
            if (line) {
                line.className = 'pvp-rank-line rank-' + rating.rankCss;
            }
            var icon = card.querySelector('.pvp-rank-icon');
            if (icon) icon.textContent = rating.rankIcon;
            var rankName = card.querySelector('.pvp-rank-badge span:not(.pvp-rank-icon)');
            if (rankName) rankName.textContent = rating.rank;
            var rankPoints = card.querySelector('.pvp-rank-badge div small');
            if (rankPoints) rankPoints.textContent = rating.points + ' кубков';
        });
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
            setCountdown(false);
            setGameFrame(false);
            renderRoundResults(null);
            return;
        }

        setSearching(false, 0);
        selectedGenerator = match.generator;
        setTitle(generatorTitle(match.generator) + ' PVP');
        if (score) score.hidden = false;
        if (opponentName) opponentName.textContent = match.opponentName || 'Соперник';
        renderDots(match.myWins || 0, match.opponentWins || 0);
        renderRoundResults(match);

        if (match.status === 'waiting_ready') {
            setStatus('Соперник найден: ' + match.opponentName + '. Готов к игре?');
            redirecting = false;
            activeRoundKey = null;
            setGameFrame(false);
            if (ready) {
                ready.hidden = false;
                ready.disabled = !!match.myReady;
                var readyText = match.currentRound > 1 ? 'ГОТОВ К ' + match.currentRound + ' РАУНДУ' : 'ГОТОВ К ИГРЕ';
                ready.textContent = match.myReady ? 'ЖДЕМ СОПЕРНИКА' : readyText;
            }
            if (exit) exit.hidden = false;
            if (cancel) cancel.hidden = true;
            if (match.myReady) {
                setCountdown(true, 'Ждем готовность соперника', '...');
            } else {
                setCountdown(false);
            }
            return;
        }

        if (match.status === 'in_round') {
            if (ready) ready.hidden = true;
            if (exit) exit.hidden = false;
            if (cancel) cancel.hidden = true;
            if (activeRoundKey === match.id + ':' + match.currentRound) {
                setCountdown(false);
                if (match.myTimeMs) {
                    setGameFrame(false);
                    if (!match.opponentTimeMs) setStatus('Твой результат отправлен. Ждем соперника до 2 минут.');
                } else {
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
            setCountdown(false);
            setGameFrame(false);
            setStatus(match.iWonMatch ? 'Матч выигран. Кубки начислены.' : 'Матч проигран. Можно взять реванш.');
        }
    }

    function startCountdown(match) {
        if (redirecting) return;
        redirecting = true;
        activeRoundKey = match.id + ':' + match.currentRound;
        var n = 5;
        if (countdownTimer) clearInterval(countdownTimer);
        setSearching(false, 0);
        setGameFrame(false);
        setCountdown(true, 'Раунд ' + match.currentRound + ' начинается', n);
        setStatus('Раунд ' + match.currentRound + ' начинается через ' + n);
        countdownTimer = setInterval(function () {
            n--;
            if (n <= 0) {
                clearInterval(countdownTimer);
                setCountdown(false);
                setGameFrame(true, match);
                setStatus('Раунд ' + match.currentRound + ' идет. Пройди генератор быстрее соперника.');
                return;
            }
            setCountdown(true, 'Раунд ' + match.currentRound + ' начинается', n);
            setStatus('Раунд ' + match.currentRound + ' начинается через ' + n);
        }, 1000);
    }

    function poll() {
        var suffix = selectedGenerator ? '&generator=' + encodeURIComponent(selectedGenerator) : '';
        fetch('/api/pvp?handler=status' + suffix)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (!data) return;
                renderRatings(data.ratings);
                if (data.match) renderMatch(data.match);
                else if (selectedGenerator) {
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

    qsa('.pvp-search-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            selectedGenerator = btn.getAttribute('data-generator');
            redirecting = false;
            setTitle(generatorTitle(selectedGenerator) + ' PVP');
            setStatus('Идет поиск соперника в твоем звании...');
            setSearching(true, 0);
            var cancel = qs('#pvpCancelBtn');
            if (cancel) cancel.hidden = false;

            api('search', 'POST', { generator: selectedGenerator })
                .then(function (data) {
                    if (data.match) renderMatch(data.match);
                    else {
                        setStatus('Идет поиск соперника в твоем звании...');
                        setSearching(true, data.result ? data.result.queueCount : 0);
                    }
                    startPolling();
                })
                .catch(function () {
                    setSearching(false, 0);
                    setStatus('Не удалось начать поиск. Войди в аккаунт и попробуй еще раз.');
                });
        });
    });

    var readyBtn = qs('#pvpReadyBtn');
    if (readyBtn) {
        readyBtn.addEventListener('click', function () {
            if (!activeMatch) return;
            api('ready', 'POST', { matchId: activeMatch.id })
                .then(function (data) {
                    renderMatch(data.match);
                    startPolling();
                    setTimeout(poll, 250);
                    setTimeout(poll, 900);
                })
                .catch(function () { setStatus('Не удалось подтвердить готовность.'); });
        });
    }

    var cancelBtn = qs('#pvpCancelBtn');
    if (cancelBtn) {
        cancelBtn.addEventListener('click', function () {
            if (!selectedGenerator) return;
            api('cancel', 'POST', { generator: selectedGenerator })
                .then(function () {
                    selectedGenerator = null;
                    activeMatch = null;
                    setSearching(false, 0);
                    setTitle('Выбери генератор для PVP');
                    setStatus('Очередь подбирает игрока твоего звания или соседнего звания.');
                    cancelBtn.hidden = true;
                });
        });
    }

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

    poll();
})();
