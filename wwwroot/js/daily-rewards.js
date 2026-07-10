(function () {
    var state = null;
    var modal = null;
    var timer = null;
    var refreshing = false;

    function load(options) {
        options = options || {};
        return fetch('/api/daily-rewards', { cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data || !data.loggedIn || !data.reward) return;
                state = data.reward;
                ensureGiftButton();
                if (modal) renderModal();
                if (state.canClaim && !options.silent) openModal();
                startCountdown();
            })
            .catch(function () { });
    }

    function ensureGiftButton() {
        if (document.querySelector('.daily-gift-btn')) return;
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'daily-gift-btn';
        btn.innerHTML = '<span>🎁</span><b>Награды</b>';
        btn.addEventListener('click', function () {
            load({ silent: true }).then(openModal);
        });
        document.body.appendChild(btn);
    }

    function openModal() {
        ensureModal();
        renderModal();
        modal.classList.add('is-visible');
    }

    function closeModal() {
        if (modal) modal.classList.remove('is-visible');
    }

    function ensureModal() {
        if (modal) return;
        modal = document.createElement('div');
        modal.className = 'daily-reward-modal';
        modal.innerHTML =
            '<section class="daily-reward-card" role="dialog" aria-modal="true">' +
            '<button type="button" class="daily-reward-close" aria-label="Закрыть">×</button>' +
            '<div class="daily-reward-head">' +
            '<span>ЕЖЕДНЕВНЫЕ НАГРАДЫ</span>' +
            '<h2>Забирай подарок каждый день</h2>' +
            '<p>7 дней подряд: Diamons, переливающийся ник и диамант возле ника. С VIP Diamons выдаются x2.</p>' +
            '</div>' +
            '<div class="daily-reward-diamons"></div>' +
            '<div class="daily-reward-grid"></div>' +
            '<div class="daily-reward-status"></div>' +
            '<button type="button" class="daily-reward-claim">ЗАБРАТЬ</button>' +
            '</section>';
        document.body.appendChild(modal);
        modal.addEventListener('click', function (event) {
            if (event.target === modal || event.target.classList.contains('daily-reward-close')) closeModal();
        });
        modal.querySelector('.daily-reward-claim').addEventListener('click', claim);
    }

    function renderModal() {
        if (!modal || !state) return;
        modal.querySelector('.daily-reward-diamons').innerHTML =
            '<span>Твои Diamons</span><strong>◆ ' + Number(state.diamons || 0) + '</strong>';

        var currentDay = Number(state.currentDay || 1);
        modal.querySelector('.daily-reward-grid').innerHTML = (state.rewards || []).map(function (reward) {
            var day = Number(reward.day || 1);
            var cls = day === currentDay ? ' is-current' : '';
            var icon = reward.cosmetic === 'rainbow_name' ? '✦' : reward.cosmetic === 'diamond_emoji' ? '💎' : '◆';
            return '<article class="daily-reward-day' + cls + '">' +
                '<span>День ' + day + '</span>' +
                '<b>' + icon + '</b>' +
                '<strong>' + escapeHtml(reward.title || '') + '</strong>' +
                '<small>' + escapeHtml(reward.description || '') + '</small>' +
                '</article>';
        }).join('');

        var claimBtn = modal.querySelector('.daily-reward-claim');
        claimBtn.disabled = !state.canClaim;
        claimBtn.textContent = state.canClaim ? 'ЗАБРАТЬ ПОДАРОК' : 'УЖЕ ЗАБРАНО';
        updateCountdownText();
    }

    function updateCountdownText() {
        if (!state) return;
        var status = modal ? modal.querySelector('.daily-reward-status') : null;
        var claimBtn = modal ? modal.querySelector('.daily-reward-claim') : null;

        if (state.canClaim) {
            if (status) status.textContent = state.message || 'Сегодняшний подарок готов.';
            if (claimBtn) {
                claimBtn.disabled = false;
                claimBtn.textContent = 'ЗАБРАТЬ ПОДАРОК';
            }
            return;
        }

        var next = new Date(state.nextClaimAt);
        var left = next.getTime() - Date.now();
        if (left <= 0) {
            if (status) status.textContent = 'Проверяем новый подарок...';
            refreshAfterCountdown();
            return;
        }

        var hours = Math.floor(left / 3600000);
        var minutes = Math.floor((left % 3600000) / 60000);
        var seconds = Math.floor((left % 60000) / 1000);
        if (status) {
            status.textContent = 'Следующий подарок через ' + pad(hours) + ':' + pad(minutes) + ':' + pad(seconds);
        }
    }

    function refreshAfterCountdown() {
        if (refreshing) return;
        refreshing = true;
        load({ silent: true }).finally(function () {
            refreshing = false;
            if (state && state.canClaim) openModal();
        });
    }

    function startCountdown() {
        if (timer) clearInterval(timer);
        timer = setInterval(updateCountdownText, 1000);
    }

    function claim() {
        var btn = modal.querySelector('.daily-reward-claim');
        btn.disabled = true;
        fetch('/api/daily-rewards?handler=claim', { method: 'POST', cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data || !data.ok || !data.reward) return;
                state = data.reward;
                renderModal();
                if (window.refreshPlayerLevelCard) window.refreshPlayerLevelCard();
            })
            .catch(function () {
                btn.disabled = false;
                modal.querySelector('.daily-reward-status').textContent = 'Не удалось забрать награду. Попробуй еще раз.';
            });
    }

    function pad(value) {
        return String(Math.max(0, value)).padStart(2, '0');
    }

    function escapeHtml(value) {
        return String(value).replace(/[&<>"']/g, function (ch) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch];
        });
    }

    document.addEventListener('DOMContentLoaded', load);
    window.openDailyRewards = openModal;
})();
