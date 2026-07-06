(function () {
    var el = document.getElementById('onlineCount');
    if (!el) return;

    var key = 'generator_online_client_id';
    var clientId = localStorage.getItem(key);

    if (!clientId) {
        clientId = 'c_' + Date.now().toString(36) + '_' + Math.random().toString(36).slice(2);
        localStorage.setItem(key, clientId);
    }

    function updateOnline() {
        fetch('/api/online', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ clientId: clientId })
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (typeof data.online === 'number') {
                    el.textContent = data.online;
                }
            })
            .catch(function () {});
    }

    updateOnline();
    window.setInterval(updateOnline, 10000);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) updateOnline();
    });
})();
