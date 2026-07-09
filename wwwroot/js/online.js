(function () {
    var countEls = Array.prototype.slice.call(document.querySelectorAll('#onlineCount, [data-online-count]'));
    var labelEls = Array.prototype.slice.call(document.querySelectorAll('#onlineLabel, [data-online-label]'));
    var badges = Array.prototype.slice.call(document.querySelectorAll('#onlineBadge, [data-online-badge]'));
    if (!countEls.length) return;

    var modeKey = 'generator_online_badge_mode';
    var mode = localStorage.getItem(modeKey) || 'online';
    var latest = { online: 0, visits: 0 };
    var key = 'generator_online_client_id';
    var visitKey = 'generator_visit_counted';
    var clientId = localStorage.getItem(key);

    if (!clientId) {
        clientId = 'c_' + Date.now().toString(36) + '_' + Math.random().toString(36).slice(2);
        localStorage.setItem(key, clientId);
    }

    function render() {
        if (mode === 'visits') {
            countEls.forEach(function (el) { el.textContent = latest.visits; });
            labelEls.forEach(function (label) { label.textContent = 'визиты'; });
            badges.forEach(function (badge) {
                badge.title = 'Показать онлайн';
                badge.classList.add('is-visits');
            });
            return;
        }

        countEls.forEach(function (el) { el.textContent = latest.online; });
        labelEls.forEach(function (label) { label.textContent = 'онлайн'; });
        badges.forEach(function (badge) {
            badge.classList.remove('is-visits');
            badge.title = 'Показать визиты';
        });
    }

    function updateOnline() {
        var shouldCountVisit = sessionStorage.getItem(visitKey) !== '1';

        fetch('/api/online', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                clientId: clientId,
                countVisit: shouldCountVisit
            })
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (typeof data.online === 'number') {
                    latest.online = data.online;
                }
                if (typeof data.visits === 'number') {
                    latest.visits = data.visits;
                }
                if (shouldCountVisit) {
                    sessionStorage.setItem(visitKey, '1');
                }
                render();
            })
            .catch(function () {});
    }

    badges.forEach(function (badge) {
        badge.addEventListener('click', function () {
            mode = mode === 'online' ? 'visits' : 'online';
            localStorage.setItem(modeKey, mode);
            render();
        });
    });

    render();
    updateOnline();
    window.setInterval(updateOnline, 10000);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) updateOnline();
    });
})();
