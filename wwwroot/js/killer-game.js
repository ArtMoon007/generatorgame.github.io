(() => {
    const root = document.querySelector('.kg-page'); if (!root) return;
    const me = Number(root.dataset.userId), $ = id => document.getElementById(id);
    let state = null, role = null, ready = false, voted = false, timerHandle;
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/killer-game').withAutomaticReconnect().build();

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    const avatar = p => p.avatar ? `<img src="${esc(p.avatar)}" alt="">` : `<span>${esc((p.name || '?')[0].toUpperCase())}</span>`;
    function toast(text) { const el=$('kgToast'); el.textContent=text; el.classList.add('show'); setTimeout(()=>el.classList.remove('show'),3000); }
    function invoke(name, ...args) { return connection.invoke(name, ...args).catch(e => toast(e.message.replace(/^.*HubException: /,''))); }
    function show(id) { ['kgLobby','kgReveal','kgQuestions','kgDiscussion','kgVoting','kgFinished','kgCountdown'].forEach(x => $(x).hidden=x!==id); }
    function phaseName(p) { return ({lobby:'ЛОББИ',countdown:'СТАРТ',reveal:'ТВОЯ РОЛЬ',questions:'ВОПРОСЫ',discussion:'ОБСУЖДЕНИЕ',voting:'ГОЛОСОВАНИЕ',finished:'ФИНАЛ'})[p]||p; }

    connection.on('Servers', servers => {
        $('kgServerList').innerHTML = servers.length ? servers.map(s => `<article class="kg-server"><div><small>СЕРВЕР #${esc(s.code)}</small><strong>${esc(s.name)}</strong></div><div class="kg-server-slots"><span>${s.players}/4</span>${[0,1,2,3].map(i=>`<i class="${i<s.players?'full':''}"></i>`).join('')}</div><button class="kg-btn" data-join="${esc(s.code)}" ${s.players>=4?'disabled':''}>ИГРАТЬ</button></article>`).join('') : '<div class="kg-empty"><b>ПОКА НЕТ СЕРВЕРОВ</b><span>Создай первый и позови друзей по коду</span></div>';
    });
    connection.on('Role', r => { role=r; renderRole(); });
    connection.on('Notice', toast);
    connection.on('Chat', m => { const c=$('kgChat'); c.insertAdjacentHTML('beforeend',`<div class="kg-message ${m.userId===me?'mine':''}"><b>${esc(m.name)}</b><span>${esc(m.text)}</span></div>`); c.scrollTop=c.scrollHeight; });
    connection.on('GameOver', x => { $('kgFinishTitle').textContent=x.message; $('kgFinishTraitor').textContent=`Предателем был: ${x.traitorName}`; show('kgFinished'); });
    connection.on('State', s => { state=s; $('kgBrowser').hidden=true; $('kgRoom').hidden=false; render(); });
    connection.onreconnected(() => invoke('GetServers'));

    function render() {
        $('kgCode').textContent=state.code; $('kgPhase').textContent=phaseName(state.phase);
        if (state.phase==='lobby') renderLobby();
        if (state.phase==='countdown') { show('kgCountdown'); startTimer(state.deadline,$('kgCountdown').querySelector('b')); }
        if (state.phase==='reveal') { show('kgReveal'); renderRole(); startTimer(state.deadline,$('kgReveal').querySelector('[data-timer]')); }
        if (state.phase==='questions') renderQuestions();
        if (state.phase==='discussion') { show('kgDiscussion'); startTimer(state.deadline,$('kgDiscussion').querySelector('[data-timer]')); }
        if (state.phase==='voting') renderVoting();
        if (state.phase==='finished') show('kgFinished');
    }
    function renderLobby() {
        show('kgLobby'); ready=!!state.players.find(p=>p.id===me)?.ready;
        $('kgPlayers').innerHTML=[0,1,2,3].map(i=>{const p=state.players[i];return p?`<div class="kg-player ${p.ready?'ready':''}"><em>${p.ready?'ГОТОВ':'НЕ ГОТОВ'}</em><div>${avatar(p)}</div><strong>${esc(p.name)}</strong></div>`:`<div class="kg-player empty"><em>СВОБОДНО</em><div>+</div><strong>ЖДЁМ ИГРОКА</strong></div>`}).join('');
        const count=state.players.filter(p=>p.ready).length; $('kgReadyCount').textContent=`${count}/4 ГОТОВЫ`; $('kgReady').textContent=ready?'НЕ ГОТОВ':'ГОТОВ'; $('kgReady').classList.toggle('active',ready);
        const owner=state.ownerId===me; $('kgOwnerTools').hidden=!owner; $('kgPrivateToggle').checked=!!state.isPrivate;
    }
    function renderRole() {
        if (!role) return; $('kgKillerName').textContent=role.killer.name; $('kgKillerImage').src=role.killer.image; $('kgKillerImage').alt=role.killer.name; $('kgKillerCard').style.setProperty('--killer',role.killer.color);
        $('kgRoleLabel').textContent=role.isTraitor?'ТЫ — ПРЕДАТЕЛЬ':'ТВОЙ КИЛЛЕР'; $('kgRoleLabel').classList.toggle('traitor',role.isTraitor);
        $('kgRoleNote').textContent=role.isTraitor?'У остальных другой киллер. Не дай себя раскрыть.':'У одного игрока другой киллер. Найди его по ответам.';
    }
    function renderQuestions() {
        show('kgQuestions'); $('kgRound').textContent=`РАУНД ${state.round}`; const turn=state.players.find(p=>p.id===state.turnUserId); $('kgTurnText').textContent=turn?`ХОД: ${turn.name}`:'';
        const asking=state.turnUserId===me, has=!!state.currentQuestion, answered=state.answers.some(a=>a.userId===me);
        $('kgQuestionComposer').hidden=!asking||has; $('kgCurrentQuestion').hidden=!has; $('kgAnswerComposer').hidden=!has||asking||answered;
        $('kgCurrentQuestion').querySelector('strong').textContent=state.currentQuestion||'';
        $('kgAnswers').innerHTML=state.answers.map(a=>{const p=state.players.find(x=>x.id===a.userId);return `<div><b>${esc(p?.name)}</b><span>${esc(a.text)}</span></div>`}).join('')+(has&&state.answers.length<3?`<p>Ответили ${state.answers.length}/3...</p>`:'');
        $('kgHistory').innerHTML=state.history.map(h=>`<details><summary>${esc(h.author)}: ${esc(h.question)}</summary>${h.answers.map(a=>`<p><b>${esc(a.name)}:</b> ${esc(a.text)}</p>`).join('')}</details>`).join('');
    }
    function renderVoting() {
        show('kgVoting'); $('kgVotesCount').textContent=state.votesCast.length; voted=state.votesCast.includes(me);
        $('kgVoteGrid').innerHTML=state.players.map(p=>`<button class="kg-vote-player" data-vote="${p.id}" ${voted?'disabled':''}><div>${avatar(p)}</div><strong>${esc(p.name)}</strong><span>ВЫГНАТЬ</span></button>`).join(''); $('kgSkip').disabled=voted;
    }
    function startTimer(deadline, el) { clearInterval(timerHandle); const tick=()=>{const n=Math.max(0,Math.ceil((new Date(deadline)-Date.now())/1000)); el.textContent=n;};tick();timerHandle=setInterval(tick,250); }

    $('kgServerList').onclick=e=>{const b=e.target.closest('[data-join]');if(b)invoke('JoinServer',b.dataset.join)};
    $('kgCreateOpen').onclick=()=>{$('kgCreateModal').hidden=false;$('kgServerName').focus()}; document.querySelector('.kg-modal-x').onclick=()=>$('kgCreateModal').hidden=true;
    $('kgCreateForm').onsubmit=e=>{e.preventDefault();invoke('CreateServer',$('kgServerName').value,$('kgCreatePrivate').checked);$('kgCreateModal').hidden=true};
    $('kgJoinByCode').onclick=()=>{const code=$('kgJoinCode').value.trim();if(code.length===6)invoke('JoinServer',code);else toast('Введи код сервера из 6 цифр')};
    $('kgRefresh').onclick=()=>invoke('GetServers'); $('kgLeave').onclick=()=>{invoke('LeaveServer');state=role=null;$('kgRoom').hidden=true;$('kgBrowser').hidden=false;invoke('GetServers')};
    $('kgReady').onclick=()=>invoke('SetReady',!ready); $('kgAsk').onclick=()=>{invoke('AskQuestion',$('kgQuestionInput').value);$('kgQuestionInput').value=''};
    $('kgPrivateToggle').onchange=e=>invoke('SetPrivate',e.target.checked); $('kgStartTest').onclick=()=>invoke('StartTest');
    $('kgAnswer').onclick=()=>{invoke('AnswerQuestion',$('kgAnswerInput').value);$('kgAnswerInput').value=''};
    const sendChat=()=>{const v=$('kgChatInput').value;if(v.trim()){invoke('SendChat',v);$('kgChatInput').value=''}}; $('kgChatSend').onclick=sendChat;$('kgChatInput').onkeydown=e=>{if(e.key==='Enter')sendChat()};
    $('kgOpenVote').onclick=()=>invoke('OpenVoting');
    $('kgVoteGrid').onclick=e=>{const b=e.target.closest('[data-vote]');if(b)invoke('Vote',Number(b.dataset.vote))}; $('kgSkip').onclick=()=>invoke('SkipVote');
    connection.start().then(()=>invoke('GetServers')).catch(e=>toast('Не удалось подключиться: '+e.message));
})();
