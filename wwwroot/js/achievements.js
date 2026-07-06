(function () {
    var queue = [];
    var showing = false;

    function ensureHost() {
        var host = document.getElementById('achievementToastHost');
        if (host) return host;

        host = document.createElement('div');
        host.id = 'achievementToastHost';
        host.className = 'achievement-toast-host';
        document.body.appendChild(host);
        return host;
    }

    function showNext() {
        if (showing || queue.length === 0) return;

        showing = true;
        var achievement = queue.shift();
        var host = ensureHost();
        var toast = document.createElement('div');
        toast.className = 'achievement-toast';
        var xpText = typeof achievement.experience === 'number'
            ? '+' + achievement.experience + ' XP'
            : escapeHtml(achievement.experience || '');

        toast.innerHTML =
            '<div class="achievement-toast-icon">' + escapeHtml(achievement.icon || '*') + '</div>' +
            '<div class="achievement-toast-body">' +
            '<div class="achievement-toast-kicker">ДОСТИЖЕНИЕ</div>' +
            '<div class="achievement-toast-title">' + escapeHtml(achievement.title || 'Ачивка') + '</div>' +
            '<div class="achievement-toast-desc">' + escapeHtml(achievement.description || '') + '</div>' +
            '<div class="achievement-toast-xp">' + xpText + '</div>' +
            '</div>';

        host.appendChild(toast);
        requestAnimationFrame(function () {
            toast.classList.add('is-visible');
        });

        window.setTimeout(function () {
            toast.classList.remove('is-visible');
            window.setTimeout(function () {
                toast.remove();
                showing = false;
                showNext();
            }, 260);
        }, 4200);
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    window.showAchievementUnlocks = function (achievements) {
        if (!Array.isArray(achievements) || achievements.length === 0) return;
        queue = queue.concat(achievements);
        showNext();
    };

    window.showRankUnlock = function (notification) {
        if (!notification) return;

        window.showAchievementUnlocks([{
            icon: '#',
            title: notification.title || 'Поздравляю!',
            description: notification.description || ('Ты топ ' + notification.rank),
            experience: notification.meta || ('#' + notification.rank)
        }]);
    };

    window.openLeaderboardProfile = function (userId) {
        if (!userId) return;

        ensureProfileModal();
        var modal = document.getElementById('profileModal');
        var body = document.getElementById('profileModalBody');
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        body.innerHTML = '<div class="profile-modal-loading">Загрузка...</div>';

        fetch('/api/player-profile?userId=' + encodeURIComponent(userId))
            .then(function (r) {
                if (!r.ok) throw new Error('profile failed');
                return r.json();
            })
            .then(renderProfileModal)
            .catch(function () {
                body.innerHTML = '<div class="profile-modal-loading">Не удалось открыть профиль</div>';
            });
    };

    function ensureProfileModal() {
        if (document.getElementById('profileModal')) return;

        var modal = document.createElement('div');
        modal.id = 'profileModal';
        modal.className = 'profile-modal';
        modal.setAttribute('aria-hidden', 'true');
        modal.innerHTML =
            '<div class="profile-modal-backdrop" data-profile-close="1"></div>' +
            '<section class="profile-modal-card" role="dialog" aria-modal="true" aria-label="Профиль игрока">' +
            '<button class="profile-modal-close" type="button" data-profile-close="1" aria-label="Закрыть">×</button>' +
            '<div id="profileModalBody"></div>' +
            '</section>';

        document.body.appendChild(modal);
        modal.addEventListener('click', function (event) {
            if (event.target && event.target.getAttribute('data-profile-close') === '1') {
                closeProfileModal();
            }
        });
    }

    function closeProfileModal() {
        var modal = document.getElementById('profileModal');
        if (!modal) return;
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    }

    function renderProfileModal(profile) {
        var body = document.getElementById('profileModalBody');
        if (!body) return;

        var avatar = profile.avatarUrl
            ? '<img class="profile-modal-avatar" src="' + escapeHtml(profile.avatarUrl) + '" alt=""/>'
            : '<div class="profile-modal-avatar profile-modal-avatar-fallback">' + escapeHtml((profile.username || '?')[0].toUpperCase()) + '</div>';

        var achievements = Array.isArray(profile.achievements) && profile.achievements.length
            ? profile.achievements.map(function (a) {
                return '<div class="profile-modal-achievement">' +
                    '<span>' + escapeHtml(a.icon || '*') + '</span>' +
                    '<div><strong>' + escapeHtml(a.title || 'Ачивка') + '</strong><small>' + escapeHtml(a.description || '') + '</small></div>' +
                    '</div>';
            }).join('')
            : '<div class="profile-modal-empty">Пока нет открытых ачивок</div>';

        body.innerHTML =
            '<div class="profile-modal-head">' +
            avatar +
            '<div><div class="profile-modal-kicker">PLAYER</div><h2>' + escapeHtml(profile.username || 'Player') + '</h2>' +
            '<div class="profile-modal-level">LVL ' + Number(profile.level || 1) + ' · ' + Number(profile.experience || 0) + ' XP</div></div>' +
            '</div>' +
            '<div class="profile-modal-stats">' +
            stat('Лучшее', profile.bestTime || '-') +
            stat('Среднее', profile.averageTime || '-') +
            stat('Игры', profile.gamesPlayed || 0) +
            stat('Любимый', profile.favoriteGenerator || '-') +
            stat('Часы', profile.totalHours || 0) +
            '</div>' +
            '<div class="profile-modal-ach-head">Ачивки ' + Number(profile.achievementsUnlocked || 0) + ' / ' + Number(profile.achievementsTotal || 0) + '</div>' +
            '<div class="profile-modal-achievements">' + achievements + '</div>';
    }

    function stat(label, value) {
        return '<div><span>' + escapeHtml(label) + '</span><strong>' + escapeHtml(value) + '</strong></div>';
    }
})();
