(function () {
    var config = {
        hover: { src: '/audio/ui-hover.aac', volume: 0.12, pool: 5 },
        click: { src: '/audio/ui-click.aac', volume: 0.42, pool: 5 },
        found: { src: '/audio/pvp-found.aac', volume: 0.42, pool: 3 },
        wire: { src: '/audio/wire-connect.aac', volume: 0.42, pool: 4 },
        cordPull: { src: '/audio/cord-pull.aac', volume: 0.42, pool: 4, maxMs: 520 },
        generatorStart: { src: '/audio/generator-start.aac', volume: 0.42, pool: 2 },
        switch: { src: '/audio/switch.aac', volume: 0.42, pool: 5 },
        pointConnect: { src: '/audio/point-connect.aac', volume: 0.42, pool: 5 },
        stageComplete: { src: '/audio/stage-complete.aac', volume: 0.42, pool: 3 }
    };
    var pools = {};
    var cursors = {};
    var unlocked = false;
    var lastHoverAt = 0;
    var pendingFound = false;
    var volumeKey = 'generator_sound_volume';
    var mutedKey = 'generator_sound_muted';
    var versionKey = 'generator_sound_settings_version';
    if (localStorage.getItem(versionKey) !== '2') {
        localStorage.setItem(volumeKey, '70');
        localStorage.setItem(mutedKey, '0');
        localStorage.setItem(versionKey, '2');
    }
    var volume = readVolume();
    var muted = localStorage.getItem(mutedKey) === '1';

    Object.keys(config).forEach(function (name) {
        var item = config[name];
        pools[name] = [];
        cursors[name] = 0;

        for (var i = 0; i < item.pool; i++) {
            var audio = new Audio(item.src);
            audio.preload = 'auto';
            audio.volume = effectiveVolume(item.volume);
            audio.setAttribute('playsinline', '');
            pools[name].push(audio);
        }
    });

    function readVolume() {
        var stored = parseInt(localStorage.getItem(volumeKey) || '70', 10);
        if (!Number.isFinite(stored)) return 70;
        return Math.max(0, Math.min(100, stored));
    }

    function effectiveVolume(base) {
        return muted ? 0 : base * (volume / 100);
    }

    function applyVolume() {
        Object.keys(pools).forEach(function (name) {
            var base = config[name].volume;
            pools[name].forEach(function (audio) {
                audio.volume = effectiveVolume(base);
                audio.muted = muted;
            });
        });
    }

    function nextAudio(name) {
        var pool = pools[name];
        if (!pool || pool.length === 0) return null;

        var index = cursors[name] % pool.length;
        cursors[name] = index + 1;
        return pool[index];
    }

    function play(name, force) {
        if (muted || volume <= 0) return;
        if (!force && !unlocked) {
            if (name === 'found') pendingFound = true;
            return;
        }

        var audio = nextAudio(name);
        if (!audio) return;

        try {
            audio.pause();
            audio.muted = false;
            audio.volume = effectiveVolume(config[name].volume);
            audio.currentTime = 0;
            var result = audio.play();
            if (config[name].maxMs) {
                setTimeout(function () {
                    try {
                        audio.pause();
                        audio.currentTime = 0;
                    } catch {}
                }, config[name].maxMs);
            }
            if (result && typeof result.catch === 'function') {
                result.catch(function () {
                    if (name === 'found') pendingFound = true;
                });
            }
        } catch {
            if (name === 'found') pendingFound = true;
        }
    }

    function unlock() {
        if (unlocked) return;
        unlocked = true;

        Object.keys(pools).forEach(function (name) {
            pools[name].forEach(function (audio) {
                try { audio.load(); } catch {}
            });
        });

        if (pendingFound) {
            pendingFound = false;
            setTimeout(function () { play('found', true); }, 120);
        }
    }

    function isUiTarget(target) {
        return target && target.closest && target.closest(
            'button,a,input,select,.generator-tab,.profile-stat,.profile-achievement,.profile-rank,.pvp-generator-card,.pvp-match-panel,.pvp-duel-box,.lb-profile-btn,.online-badge,.nav-user'
        );
    }

    function syncControls() {
        document.querySelectorAll('[data-sound-volume]').forEach(function (input) {
            input.value = String(volume);
        });
        document.querySelectorAll('[data-sound-value]').forEach(function (el) {
            el.textContent = volume + '%';
        });
        document.querySelectorAll('[data-sound-muted]').forEach(function (input) {
            input.checked = muted;
        });
        document.querySelectorAll('[data-sound-toggle]').forEach(function (btn) {
            btn.classList.toggle('is-muted', muted || volume <= 0);
            btn.setAttribute('aria-label', muted || volume <= 0 ? 'Включить звук' : 'Настроить звук');
            btn.textContent = muted || volume <= 0 ? '♪ OFF' : '♪ ' + volume + '%';
        });
    }

    function setVolume(nextVolume) {
        volume = Math.max(0, Math.min(100, parseInt(nextVolume, 10) || 0));
        if (volume > 0 && muted) muted = false;
        localStorage.setItem(volumeKey, String(volume));
        localStorage.setItem(mutedKey, muted ? '1' : '0');
        applyVolume();
        syncControls();
    }

    function setMuted(nextMuted) {
        muted = !!nextMuted;
        localStorage.setItem(mutedKey, muted ? '1' : '0');
        applyVolume();
        syncControls();
    }

    function ensureFloatingControl() {
        if (document.getElementById('soundControl')) return;

        var wrap = document.createElement('div');
        wrap.id = 'soundControl';
        wrap.className = 'sound-control';
        wrap.innerHTML =
            '<button type="button" class="sound-float-btn" data-sound-toggle aria-expanded="false">♪</button>' +
            '<div class="sound-float-panel" id="soundFloatPanel" hidden>' +
            '<div class="sound-panel-head"><span>Звук</span><b data-sound-value></b></div>' +
            '<input type="range" min="0" max="100" step="1" data-sound-volume aria-label="Громкость звука" />' +
            '<label><input type="checkbox" data-sound-muted /> <span>Выключить</span></label>' +
            '</div>';
        document.body.appendChild(wrap);

        var btn = wrap.querySelector('[data-sound-toggle]');
        var panel = wrap.querySelector('#soundFloatPanel');
        btn.addEventListener('click', function () {
            var open = panel.hidden;
            panel.hidden = !open;
            btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        });

        document.addEventListener('pointerdown', function (event) {
            if (wrap.contains(event.target)) return;
            panel.hidden = true;
            btn.setAttribute('aria-expanded', 'false');
        }, { passive: true });
    }

    function bindControls() {
        document.addEventListener('input', function (event) {
            if (event.target && event.target.matches('[data-sound-volume]')) {
                setVolume(event.target.value);
            }
        });

        document.addEventListener('change', function (event) {
            if (event.target && event.target.matches('[data-sound-muted]')) {
                setMuted(event.target.checked);
            }
        });
    }

    document.addEventListener('pointerdown', function (event) {
        unlock();
        if (isUiTarget(event.target)) play('click', true);
    }, { passive: true, capture: true });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        unlock();
        if (isUiTarget(event.target)) play('click', true);
    }, true);

    document.addEventListener('pointerover', function (event) {
        if (!unlocked || event.pointerType === 'touch') return;
        var target = isUiTarget(event.target);
        if (!target || (event.relatedTarget && target.contains(event.relatedTarget))) return;

        var now = Date.now();
        if (now - lastHoverAt < 90) return;
        lastHoverAt = now;
        play('hover', true);
    }, { passive: true, capture: true });

    window.GameSounds = {
        play: function (name) { play(name, false); },
        unlock: unlock,
        setVolume: setVolume,
        setMuted: setMuted,
        getSettings: function () {
            return { volume: volume, muted: muted };
        }
    };

    ensureFloatingControl();
    bindControls();
    applyVolume();
    syncControls();
})();
