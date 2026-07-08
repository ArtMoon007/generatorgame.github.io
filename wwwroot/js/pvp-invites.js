(function () {
    var activeInviteId = null;
    var hiddenInviteIds = {};

    function ensureToast() {
        var existing = document.getElementById('pvpInviteToast');
        if (existing) return existing;

        var toast = document.createElement('div');
        toast.id = 'pvpInviteToast';
        toast.className = 'pvp-invite-toast';
        toast.hidden = true;
        toast.innerHTML =
            '<div class="pvp-invite-kicker">ВАС ПОЗВАЛИ НА ПОЕДИНОК</div>' +
            '<div class="pvp-invite-title" id="pvpInviteToastTitle"></div>' +
            '<div class="pvp-invite-meta" id="pvpInviteToastMeta"></div>' +
            '<div class="pvp-invite-actions">' +
            '<button type="button" class="pvp-invite-accept" id="pvpInviteAcceptBtn">ПРИНЯТЬ</button>' +
            '<button type="button" class="pvp-invite-decline" id="pvpInviteDeclineBtn">ОТКАЗАТЬСЯ</button>' +
            '</div>';
        document.body.appendChild(toast);

        document.getElementById('pvpInviteAcceptBtn').addEventListener('click', function () {
            respond(true);
        });

        document.getElementById('pvpInviteDeclineBtn').addEventListener('click', function () {
            respond(false);
        });

        return toast;
    }

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

    function showInvite(invite) {
        if (!invite || hiddenInviteIds[invite.id]) return;
        var toast = ensureToast();
        activeInviteId = invite.id;
        document.getElementById('pvpInviteToastTitle').textContent = invite.senderName;
        document.getElementById('pvpInviteToastMeta').textContent = invite.generatorTitle + ' PVP';
        toast.hidden = false;
        if (window.GameSounds) window.GameSounds.play('found');
    }

    function hideToast() {
        var toast = ensureToast();
        toast.hidden = true;
        activeInviteId = null;
    }

    function respond(accept) {
        if (!activeInviteId) return;
        var inviteId = activeInviteId;
        hiddenInviteIds[inviteId] = true;
        api('respondInvite', 'POST', { inviteId: inviteId, accept: accept })
            .then(function (data) {
                hideToast();
                if (accept && data && data.accepted) {
                    location.href = '/pvp';
                }
            })
            .catch(function () {
                hideToast();
            });
    }

    function pollInvite() {
        api('invite')
            .then(function (data) {
                if (data && data.invite) showInvite(data.invite);
            })
            .catch(function () { });
    }

    setTimeout(pollInvite, 1200);
    setInterval(pollInvite, 4500);
})();
