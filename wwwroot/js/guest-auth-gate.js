(() => {
    const modal = document.getElementById('guestAuthModal');
    if (!modal) return;

    const interactiveSelector = [
        'button:not([data-guest-auth-close])',
        'input',
        'select',
        'textarea',
        '[onclick]',
        '.fg-game',
        '.bnb-game',
        '.game-stage-square',
        '[data-generator]'
    ].join(',');

    function openGate() {
        modal.hidden = false;
        document.body.classList.add('guest-auth-open');
        requestAnimationFrame(() => modal.classList.add('open'));
    }

    function closeGate() {
        modal.classList.remove('open');
        document.body.classList.remove('guest-auth-open');
        setTimeout(() => { modal.hidden = true; }, 180);
    }

    document.addEventListener('click', event => {
        if (event.target.closest('[data-fold-toggle]')) return;
        if (event.target.closest('#guestAuthModal, .navbar, .site-footer, .generator-tabs, a')) return;
        if (!event.target.closest(interactiveSelector)) return;
        event.preventDefault();
        event.stopPropagation();
        event.stopImmediatePropagation();
        openGate();
    }, true);

    document.addEventListener('focusin', event => {
        if (event.target.closest('#guestAuthModal')) return;
        if (!event.target.matches('input,select,textarea')) return;
        event.target.blur();
        openGate();
    }, true);

    modal.addEventListener('click', event => {
        if (event.target.closest('[data-guest-auth-close]')) closeGate();
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !modal.hidden) closeGate();
    });
})();
