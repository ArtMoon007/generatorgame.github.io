(function () {
    var tipTimer = null;
    var lastTips = [];

    function render(card, data) {
        if (!data || !data.loggedIn) {
            card.innerHTML = '<div class="player-level-empty">Войди в аккаунт, чтобы видеть уровень</div>';
            return;
        }

        var progress = Math.max(0, Math.min(100, data.progress || 0));
        var tips = Array.isArray(data.tips) && data.tips.length
            ? data.tips
            : [data.tip || 'Играй в PVP: там опыт идет в 2 раза быстрее.'];
        lastTips = tips;

        card.innerHTML =
            '<div class="player-level-ring" style="--progress:' + progress + '">' +
            '<div class="player-level-ring-center"><span>LVL</span><strong>' + Number(data.level || 1) + '</strong></div>' +
            '</div>' +
            '<div class="player-level-info">' +
            '<strong>' + escapeHtml(data.username || 'Player') + '</strong>' +
            '<span>' + Number(data.experience || 0) + ' XP собрано</span>' +
            '<span>До уровня: ' + Number(data.experienceToNextLevel || 0) + ' XP</span>' +
            '</div>' +
            '<div class="player-diamons-box" title="Diamons">' +
            '<span class="player-diamons-icon">◆</span>' +
            '<strong>' + Number(data.diamons || 0) + '</strong>' +
            '<button type="button" class="player-diamons-help" aria-label="Как получить Diamons">?</button>' +
            '</div>' +
            '<button type="button" class="player-shop-btn">Магазин</button>';

        var help = card.querySelector('.player-diamons-help');
        if (help) {
            help.addEventListener('click', function (event) {
                event.stopPropagation();
                showDiamonsInfo();
            });
        }

        var shop = card.querySelector('.player-shop-btn');
        if (shop) {
            shop.addEventListener('click', function (event) {
                event.stopPropagation();
                showShopInfo();
            });
        }

        ensureTipBubble(card, randomTip());
        scheduleTips();
    }

    function showDiamonsInfo() {
        var modal = document.querySelector('.diamons-info-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.className = 'diamons-info-modal';
            modal.innerHTML =
                '<div class="diamons-info-card" role="dialog" aria-modal="true">' +
                '<button type="button" class="diamons-info-close" aria-label="Закрыть">×</button>' +
                '<h2>Как получить Diamons?</h2>' +
                '<p>Diamons дают за попадание в топ, победы и участие в PVP, ежедневные награды, а также за открытие достижений.</p>' +
                '<p>За обычные игры Diamons не начисляются. С активным VIP все Diamons выдаются x2.</p>' +
                '</div>';
            document.body.appendChild(modal);
            modal.addEventListener('click', function (event) {
                if (event.target === modal || event.target.classList.contains('diamons-info-close')) {
                    modal.classList.remove('is-visible');
                }
            });
        }
        requestAnimationFrame(function () { modal.classList.add('is-visible'); });
    }

    function showShopInfo() {
        var modal = document.querySelector('.diamons-shop-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.className = 'diamons-info-modal diamons-shop-modal';
            modal.innerHTML =
                '<div class="diamons-info-card diamons-shop-card" role="dialog" aria-modal="true">' +
                '<button type="button" class="diamons-info-close" aria-label="Закрыть">×</button>' +
                '<div class="diamons-shop-hero">' +
                '<div class="diamons-shop-kicker">DIAMONS SHOP</div>' +
                '<h2>Магазин почти открыт</h2>' +
                '<p>Товары уже подготовлены, но пока скрыты. Они станут доступны через 3 дня.</p>' +
                '</div>' +
                '<div class="diamons-shop-grid">' +
                shopItem("🌈", "Цвет ника", "Скрытый стиль профиля", "???") +
                shopItem("💎", "Значок возле ника", "Редкий косметический предмет", "???") +
                shopItem("⚡", "Эффект победы", "Анимация после результата", "???") +
                shopItem("♛", "Профильная рамка", "Украшение профиля", "???") +
                shopItem("🔥", "PVP эмоция", "Покажи стиль в дуэли", "???") +
                shopItem("?", "Секретный товар", "Откроется позже", "???") +
                '</div>' +
                '<div class="diamons-shop-lock">Копи Diamons, пока есть время :)</div>' +
                '</div>';
            document.body.appendChild(modal);
            modal.addEventListener('click', function (event) {
                if (event.target === modal || event.target.classList.contains('diamons-info-close')) {
                    modal.classList.remove('is-visible');
                }
            });
        }
        requestAnimationFrame(function () { modal.classList.add('is-visible'); });
    }

    function shopItem(icon, title, desc, price) {
        return '<article class="diamons-shop-item is-locked">' +
            '<div class="diamons-shop-item-icon">' + escapeHtml(icon) + '</div>' +
            '<strong>' + escapeHtml(title) + '</strong>' +
            '<span>' + escapeHtml(desc) + '</span>' +
            '<b>◆ ' + escapeHtml(price) + '</b>' +
            '<small>СКОРО</small>' +
            '</article>';
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
