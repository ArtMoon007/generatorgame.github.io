(function () {
    var tipTimer = null;
    var lastTips = [];

    function render(card, data) {
        if (!data || !data.loggedIn) {
            card.innerHTML = '<div class="player-level-empty">Войди в аккаунт, чтобы видеть уровень</div>';
            return;
        }

        var progress = Math.max(0, Math.min(100, data.progress || 0));
        var tips = Array.isArray(data.tips) && data.tips.length ? data.tips : [data.tip || 'Играй в PVP: там опыт идет в 2 раза быстрее.'];
        lastTips = tips;

        card.innerHTML =
            '<div class="player-level-ring" style="--progress:' + progress + '">' +
            '<div class="player-level-ring-center"><span>LVL</span><strong>' + data.level + '</strong></div>' +
            '</div>' +
            '<div class="player-level-info">' +
            '<strong>' + escapeHtml(data.username || 'Player') + '</strong>' +
            '<span>' + (data.experience || 0) + ' XP собрано</span>' +
            '<span>До уровня: ' + (data.experienceToNextLevel || 0) + ' XP</span>' +
            '</div>';

        ensureTipBubble(card, randomTip());
        scheduleTips();
    }

    function ensureTipBubble(card, text) {
        var bubble = card.querySelector('.player-level-tip');
        if (!bubble) {
            bubble = document.createElement('div');
            bubble.className = 'player-level-tip';
            bubble.innerHTML =
                '<button type="button" class="player-level-tip-close" aria-label="Закрыть подсказку">×</button>' +
                '<span></span>';
            card.appendChild(bubble);

            bubble.querySelector('button').addEventListener('click', function () {
                bubble.classList.remove('is-visible');
            });
        }

        bubble.querySelector('span').textContent = text;
        setTimeout(function () { bubble.classList.add('is-visible'); }, 120);
    }

    function showRandomTip() {
        var cards = Array.prototype.slice.call(document.querySelectorAll('[data-player-level-card]'));
        if (!cards.length || !lastTips.length) return;

        cards.forEach(function (card) {
            ensureTipBubble(card, randomTip());
        });
    }

    function scheduleTips() {
        if (tipTimer) return;
        tipTimer = setInterval(showRandomTip, 180000);
    }

    function randomTip() {
        if (!lastTips.length) return 'Играй в PVP: там опыт идет в 2 раза быстрее.';
        return lastTips[Math.floor(Math.random() * lastTips.length)];
    }

    function escapeHtml(value) {
        return String(value).replace(/[&<>"']/g, function (ch) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch];
        });
    }

    function load() {
        var cards = Array.prototype.slice.call(document.querySelectorAll('[data-player-level-card]'));
        if (!cards.length) return;

        fetch('/api/player-level')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                cards.forEach(function (card) { render(card, data); });
            })
            .catch(function () {
                cards.forEach(function (card) {
                    card.innerHTML = '<div class="player-level-empty">Уровень временно недоступен</div>';
                });
            });
    }

    document.addEventListener('DOMContentLoaded', load);
    window.refreshPlayerLevelCard = load;
})();
