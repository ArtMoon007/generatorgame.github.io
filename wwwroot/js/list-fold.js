(() => {
    const LIMIT = 10;

    function rowHeight(list, rows) {
        if (!rows.length) return 0;
        const style = getComputedStyle(list);
        const gap = parseFloat(style.rowGap || style.gap) || 0;
        return rows.slice(0, LIMIT).reduce((sum, row) => sum + row.getBoundingClientRect().height, 0)
            + gap * Math.max(0, Math.min(rows.length, LIMIT) - 1);
    }

    function setup(list, kind) {
        if (!list || list.dataset.foldReady) return;
        list.dataset.foldReady = '1';
        list.classList.add('fold-list');
        list.closest('.lb-panel,.personal-panel')?.classList.add('has-fold-list');

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'list-fold-toggle';
        button.dataset.foldToggle = kind;
        button.innerHTML = '<span>РАЗВЕРНУТЬ</span><i aria-hidden="true">⌄</i>';
        list.insertAdjacentElement('afterend', button);

        let expanded = false;
        let animating = false;

        function rows() {
            return Array.from(list.children).filter(row =>
                row.classList.contains(kind === 'top' ? 'lb-row' : 'pp-row'));
        }

        function apply(animate) {
            const items = rows();
            const canFold = items.length > LIMIT;
            button.hidden = !canFold;
            list.classList.toggle('is-foldable', canFold);

            if (!canFold) {
                expanded = false;
                list.classList.remove('is-expanded', 'is-collapsed');
                list.style.removeProperty('max-height');
                return;
            }

            const collapsedHeight = Math.ceil(rowHeight(list, items));
            const fullHeight = Math.ceil(list.scrollHeight);
            list.style.setProperty('--fold-height', collapsedHeight + 'px');
            list.style.setProperty('--fold-full-height', fullHeight + 'px');
            list.classList.toggle('is-expanded', expanded);
            list.classList.toggle('is-collapsed', !expanded);
            button.classList.toggle('is-expanded', expanded);
            button.querySelector('span').textContent = expanded ? 'СВЕРНУТЬ' : 'РАЗВЕРНУТЬ';
            button.querySelector('i').textContent = '⌄';
            list.closest('.lb-panel,.personal-panel')?.classList.toggle('list-is-expanded', expanded);
            const page = list.closest('.generator-page');
            if (page) page.classList.toggle('has-expanded-list', !!page.querySelector('.list-is-expanded'));

            if (!animate) {
                list.style.setProperty('max-height', (expanded ? fullHeight : collapsedHeight) + 'px', 'important');
                return;
            }

            const from = list.getBoundingClientRect().height;
            list.style.setProperty('max-height', from + 'px', 'important');
            requestAnimationFrame(() => requestAnimationFrame(() => {
                list.style.setProperty('max-height', (expanded ? fullHeight : collapsedHeight) + 'px', 'important');
            }));
        }

        button.addEventListener('click', () => {
            if (animating) return;
            animating = true;
            button.disabled = true;
            expanded = !expanded;
            apply(true);
            setTimeout(() => {
                animating = false;
                button.disabled = false;
                apply(false);
            }, 680);
        });

        new MutationObserver(() => requestAnimationFrame(() => apply(false)))
            .observe(list, { childList: true });
        window.addEventListener('resize', () => apply(false));
        window.addEventListener('load', () => apply(false), { once: true });
        window.addEventListener('orientationchange', () => setTimeout(() => apply(false), 250));
        apply(false);
    }

    setup(document.getElementById('lbList'), 'top');
    setup(document.getElementById('ppList'), 'attempts');
})();
