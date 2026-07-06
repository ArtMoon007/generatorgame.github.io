(function () {
    var el = document.getElementById('onlineCount');
    var label = document.getElementById('onlineLabel');
    var badge = document.getElementById('onlineBadge');
    if (!el) return;

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
            el.textContent = latest.visits;
            if (label) label.textContent = 'визиты';
            if (badge) badge.title = 'Показать онлайн';
            if (badge) badge.classList.add('is-visits');
            return;
        }

        el.textContent = latest.online;
        if (badge) badge.classList.remove('is-visits');
        if (label) label.textContent = 'онлайн';
        if (badge) badge.title = 'Показать визиты';
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

    if (badge) {
        badge.addEventListener('click', function () {
            mode = mode === 'online' ? 'visits' : 'online';
            localStorage.setItem(modeKey, mode);
            render();
        });
    }

    render();
    updateOnline();
    window.setInterval(updateOnline, 10000);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) updateOnline();
    });
})();
