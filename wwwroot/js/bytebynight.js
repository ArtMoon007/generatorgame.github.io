// ============================================================
//  GENERATOR GAME  v5 (multi-generator)
// ============================================================

var COLORS = ['#4db34d','#e05050','#4488ff','#cc44ff'];
var startTime=0, timerInt=null, stage=0, gameStarted=false, gameReady=false;
var STAGE_DELAY_MS = 140;
var CORD_RETURN_MS = 120;

function currentGenerator(){
    return typeof CURRENT_GENERATOR !== 'undefined' ? CURRENT_GENERATOR : 'bitebynight';
}

// ── TIMER ──────────────────────────────────────────────────
function startTimer(){ startTime=Date.now(); timerInt=setInterval(tickTimer,13); }
function stopTimer(){ clearInterval(timerInt); }
function tickTimer(){ var el=document.getElementById('timerDisplay'); if(el) el.textContent=fmt(Date.now()-startTime); }
function fmt(ms){ var m=Math.floor(ms/60000),s=Math.floor((ms%60000)/1000),x=ms%1000; return p2(m)+':'+p2(s)+':'+p3(x); }
function p2(n){ return String(n).padStart(2,'0'); }
function p3(n){ return String(n).padStart(3,'0'); }

// ── STAGE SYSTEM ───────────────────────────────────────────
function setStage(n){
    stage=n;
    var wire=document.getElementById('wirePanel');
    var sw=document.getElementById('swPanel');
    var cord=document.getElementById('cordPanel');
    [wire,sw,cord].forEach(function(el){ if(el){ el.classList.remove('stage-active','stage-dim','stage-done'); } });
    var s0=n===0?'stage-active':'stage-done';
    var s1=n>1?'stage-done':n===1?'stage-active':'stage-dim';
    var s2=n>2?'stage-done':n===2?'stage-active':'stage-dim';
    if(wire) wire.classList.add(s0);
    if(sw)   sw.classList.add(s1);
    if(cord) cord.classList.add(s2);
}

// ── START / RESTART ───────────────────────────────────────
function startGame(){
    var sb=document.getElementById('startBar'); if(sb) sb.style.display='none';
    var fo=document.getElementById('finishOverlay'); if(fo) fo.style.display='none';
    var square=document.getElementById('gameStageSquare'); if(square) square.classList.remove('is-finished');
    var wp=document.getElementById('wirePanel'); if(wp) wp.style.display='flex';
    stopTimer();
    var td=document.getElementById('timerDisplay'); if(td) td.textContent='00:00:000';
    initWires();
    initSwitches(); renderSwitches();
    initCord();
    setStage(0);
    gameReady=true;
    startTimer();
    gameStarted=true;
}

// ==========================================================
//  ЭТАП 1: ПРОВОДА  (Canvas + физика + snap на hover)
// ==========================================================
var plugs=[], jacks=[], dragging=null, done=[];
var canvas, ctx;
var WW, WH;

function initWires() {
    canvas = document.getElementById('wireCanvas');
    if (!canvas) return;

    ctx = canvas.getContext('2d');
    if (!ctx) return;

    var rect = canvas.getBoundingClientRect();
    WW = Math.max(260, rect.width || 320);
    WH = Math.max(220, rect.height || 260);

    var dpr = Math.max(1, Math.min(2, window.devicePixelRatio || 1));
    canvas.width = Math.round(WW * dpr);
    canvas.height = Math.round(WH * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.imageSmoothingEnabled = true;

    ctx.clearRect(0, 0, WW, WH);
    ctx.fillStyle = '#060606';
    ctx.fillRect(0, 0, WW, WH);
    ctx.strokeStyle = 'rgba(255,255,255,0.04)';
    ctx.lineWidth = 1;
    for (var x = 0; x <= WW; x += 40) {
        ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, WH); ctx.stroke();
    }
    for (var y = 0; y <= WH; y += 40) {
        ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(WW, y); ctx.stroke();
    }

    var order = shuffle([0, 1, 2, 3]);

    plugs = [0, 1, 2, 3].map(i => ({
        ci: i, done: false, x: lx(), y: py(i)
    }));

    jacks = order.map((ci, i) => ({
        ci: ci, done: false, x: rx(), y: py(i)
    }));

    done = [];
    dragging = null;

    renderWires();

    canvas.style.touchAction = 'none';
    canvas.onmousedown = wDown;
    canvas.onmousemove = wMove;
    canvas.onmouseup = wUp;
    canvas.onmouseleave = wUp;
    canvas.ontouchstart = function(e){ e.preventDefault(); wDown(e); };
    canvas.ontouchmove = function(e){ e.preventDefault(); wMove(e); };
    canvas.ontouchend = function(e){ e.preventDefault(); wUp(e); };
    canvas.ontouchcancel = function(e){ e.preventDefault(); wUp(e); };
}

function tp(e){ var t=e.touches[0]||e.changedTouches[0]; return {clientX:t.clientX,clientY:t.clientY}; }
function lx(){ return 34; }
function rx(){ return WW-34; }
function py(i){ var m=28,step=(WH-m*2)/3; return m+i*step; }

// Физика провода: вычисляем набор точек с провисанием
function wirePoints(x1,y1,x2,y2,slack){
    var segs=14;
    var pts=[];
    var mx=(x1+x2)/2, my=(y1+y2)/2+slack;
    for(var i=0;i<=segs;i++){
        var t=i/segs, mt=1-t;
        var bx=mt*mt*x1+2*mt*t*mx+t*t*x2;
        var by=mt*mt*y1+2*mt*t*my+t*t*y2;
        pts.push([bx,by]);
    }
    return pts;
}

// Анимационный цикл для «качания» висящего провода
var wireAnim=0;
function renderWires(){
    if(!ctx) return;
    ctx.clearRect(0,0,WW,WH);
    ctx.fillStyle='#060606';
    ctx.fillRect(0,0,WW,WH);
    ctx.strokeStyle='rgba(255,255,255,0.06)';
    ctx.lineWidth=1;
    for(var x=0;x<=WW;x+=40){ ctx.beginPath(); ctx.moveTo(x,0); ctx.lineTo(x,WH); ctx.stroke(); }
    for(var y=0;y<=WH;y+=40){ ctx.beginPath(); ctx.moveTo(0,y); ctx.lineTo(WW,y); ctx.stroke(); }
    wireAnim+=0.03;

    done.forEach(function(d){
        var p=plugs[d.pi], j=jacks[d.ji];
        var sway=Math.sin(wireAnim+d.pi)*2.5;
        drawWirePts(wirePoints(p.x,p.y,j.x,j.y,14+sway), COLORS[p.ci], 1.0, 3);
    });

    if(dragging){
        var p=plugs[dragging.pi];
        var slack=Math.abs(dragging.y-p.y)*0.35+10;
        drawWirePts(wirePoints(p.x,p.y,dragging.x,dragging.y,slack), COLORS[p.ci], 0.9, 3);
    }

    jacks.forEach(function(j){ drawJack(j.x,j.y,COLORS[j.ci],j.done); });
    plugs.forEach(function(p,i){ drawPlug(p.x,p.y,COLORS[p.ci],p.done); });

    if(gameStarted) requestAnimationFrame(renderWires);
}

function drawWirePts(pts,col,alpha,lw){
    ctx.save(); ctx.globalAlpha=alpha; ctx.strokeStyle=col; ctx.lineWidth=lw; ctx.lineCap='round'; ctx.lineJoin='round';
    ctx.shadowColor=col; ctx.shadowBlur=7;
    ctx.beginPath(); ctx.moveTo(pts[0][0],pts[0][1]);
    for(var i=1;i<pts.length;i++) ctx.lineTo(pts[i][0],pts[i][1]);
    ctx.stroke(); ctx.restore();
}

function drawRoundedRect(x,y,w,h,r){
    if(typeof ctx.roundRect==='function'){
        ctx.beginPath(); ctx.roundRect(x,y,w,h,r); return;
    }
    ctx.beginPath();
    ctx.moveTo(x+r,y);
    ctx.lineTo(x+w-r,y); ctx.quadraticCurveTo(x+w,y,x+w,y+r);
    ctx.lineTo(x+w,y+h-r); ctx.quadraticCurveTo(x+w,y+h,x+w-r,y+h);
    ctx.lineTo(x+r,y+h); ctx.quadraticCurveTo(x,y+h,x,y+h-r);
    ctx.lineTo(x,y+r); ctx.quadraticCurveTo(x,y,x+r,y);
    ctx.closePath();
}

function drawPlug(x,y,col,connected){
    ctx.fillStyle=connected?'#162416':'#202020';
    ctx.strokeStyle=col; ctx.lineWidth=2;
    drawRoundedRect(x-(connected?4:26),y-9,28,18,4); ctx.fill(); ctx.stroke();
    ctx.fillStyle=col;
    ctx.beginPath(); ctx.arc(connected?x+7:x-12,y,5.5,0,Math.PI*2); ctx.fill();
    if(!connected){
        ctx.fillStyle='#777'; ctx.fillRect(x+1,y-3,7,6);
        ctx.fillStyle='#444'; ctx.fillRect(x+7,y-2,4,4);
    }
}

function drawJack(x,y,col,connected){
    ctx.fillStyle=connected?'#0c1a0c':'#0f0f0f';
    ctx.strokeStyle=connected?col:'#2a2a2a'; ctx.lineWidth=2;
    drawRoundedRect(x-2,y-10,26,20,4); ctx.fill(); ctx.stroke();
    ctx.fillStyle=connected?col:'#060606';
    drawRoundedRect(x+4,y-5,12,10,3); ctx.fill();
    ctx.fillStyle=col; ctx.globalAlpha=connected?0.9:0.35;
    ctx.beginPath(); ctx.arc(x+20,y,4,0,Math.PI*2); ctx.fill(); ctx.globalAlpha=1;
}

function cp(e){
    var evt=e && e.touches && e.touches[0] ? e.touches[0] : (e && e.changedTouches && e.changedTouches[0] ? e.changedTouches[0] : e);
    var r=canvas.getBoundingClientRect();
    return {x:(evt.clientX-r.left)*(WW/r.width), y:(evt.clientY-r.top)*(WH/r.height)};
}

function wDown(e){
    if(!gameReady || stage!==0) return;
    var point=e && e.touches && e.touches[0] ? e.touches[0] : (e && e.changedTouches && e.changedTouches[0] ? e.changedTouches[0] : e);
    if(point){ e=point; }
    e.preventDefault && e.preventDefault();
    var pos=cp(e);
    for(var i=0;i<plugs.length;i++){
        if(plugs[i].done) continue;
        var dx=pos.x-plugs[i].x, dy=pos.y-plugs[i].y;
        if(dx*dx+dy*dy<24*24){ dragging={pi:i,x:pos.x,y:pos.y}; canvas.style.cursor='grabbing'; return; }
    }
}

function wMove(e){
    if(!gameReady || !dragging) return;
    var point=e && e.touches && e.touches[0] ? e.touches[0] : (e && e.changedTouches && e.changedTouches[0] ? e.changedTouches[0] : e);
    if(point){ e=point; }
    e.preventDefault && e.preventDefault();
    var pos=cp(e);
    dragging.x=pos.x; dragging.y=pos.y;
    renderWires();
    for(var i=0;i<jacks.length;i++){
        if(jacks[i].done) continue;
        var dx=pos.x-jacks[i].x, dy=pos.y-jacks[i].y;
        if(dx*dx+dy*dy<28*28){
            if(jacks[i].ci===plugs[dragging.pi].ci){
                connectWire(dragging.pi,i); return;
            }
        }
    }
}

function wUp(e){
    if(!gameReady || !dragging) return;
    var point=e && e.touches && e.touches[0] ? e.touches[0] : (e && e.changedTouches && e.changedTouches[0] ? e.changedTouches[0] : e);
    if(point){ e=point; }
    e.preventDefault && e.preventDefault();
    var pos=cp(e);
    for(var i=0;i<jacks.length;i++){
        if(jacks[i].done) continue;
        var dx=pos.x-jacks[i].x, dy=pos.y-jacks[i].y;
        if(dx*dx+dy*dy<30*30){
            if(jacks[i].ci===plugs[dragging.pi].ci){
                connectWire(dragging.pi,i); return;
            } else {
                flashCanvas('#cc2222'); break;
            }
        }
    }
    dragging=null;
    renderWires();
    if(canvas) canvas.style.cursor='grab';
}

function connectWire(pi,ji){
    plugs[pi].done=true; jacks[ji].done=true;
    done.push({pi:pi,ji:ji});
    dragging=null;
    renderWires();
    flashCanvas('#22aa22');
    if(done.length===4) setTimeout(finishWires, STAGE_DELAY_MS);
}

function flashCanvas(col){
    canvas.style.outline='2px solid '+col;
    setTimeout(function(){ canvas.style.outline=''; },420);
}

function finishWires(){
    if(!canvas) return;
    canvas.onmousedown=canvas.onmousemove=canvas.onmouseup=null;
    canvas.ontouchstart=canvas.ontouchmove=canvas.ontouchend=null;
    gameStarted=false;
    var h=document.getElementById('wireHint'); if(h) h.textContent='Провода подключены ✓';
    setStage(1);
}

// ==========================================================
//  ЭТАП 2: РУБИЛЬНИКИ
// ==========================================================
var swSt=[];
function initSwitches(){ swSt=[0,0,0,0,0].map(function(){ return Math.random()>0.5; }); }
function renderSwitches(){
    var g=document.getElementById('switchesGrid'); if(!g) return; g.innerHTML='';
    swSt.forEach(function(on,i){
        var d=document.createElement('div'); d.className='sw-item'+(on?' on':'');
        d.innerHTML='<div class="sw-led"></div><div class="sw-body"><div class="sw-track"><div class="sw-knob"></div></div><span class="sw-lbl">'+(on?'ON':'OFF')+'</span></div>';
        d.addEventListener('click',(function(idx){return function(){clickSw(idx);};})(i));
        g.appendChild(d);
    });
    var cnt=swSt.filter(Boolean).length;
    var h=document.getElementById('swHint'); if(h) h.textContent=cnt+'/5';
}
function clickSw(i){
    if(!gameReady || stage!==1) return;
    swSt[i]=!swSt[i]; renderSwitches();
    if(swSt.every(function(s){return s;})) setTimeout(finishSwitches, STAGE_DELAY_MS);
}
function finishSwitches(){ setStage(2); }

// ==========================================================
//  ЭТАП 3: ТРОС  (10 рывков)
// ==========================================================
var PULLS=10, pullsDone=0, cordDrag=false, cordStartY=0, cordStartTop=0, cordArmed=false, cordAutoCounted=false;
var CORD_MAX=110; // пикселей вниз

function initCord(){
    pullsDone=0; cordDrag=false; cordStartY=0; cordStartTop=0; cordArmed=false; cordAutoCounted=false;
    var h=document.getElementById('cordHandle'); if(h) h.style.top='0px';
    var r=document.getElementById('cordRope'); if(r) r.style.height='0px';
    updateCordUI();
    var handle=document.getElementById('cordHandle'); if(!handle) return;
    handle.onmousedown=function(e){ e.preventDefault(); if(!gameReady || stage!==2) return; cordDrag=true; cordStartY=e.clientY; cordStartTop=parseFloat(getComputedStyle(handle).top||0); cordArmed=false; };
    handle.ontouchstart=function(e){ e.preventDefault(); if(!gameReady || stage!==2) return; cordDrag=true; cordStartY=e.touches[0].clientY; cordStartTop=parseFloat(getComputedStyle(handle).top||0); cordArmed=false; };
}

function cordMoveGlobal(e){
    if(!gameReady || !cordDrag||stage!==2) return;
    var cy=e.touches?e.touches[0].clientY:e.clientY;
    var dy=Math.max(0,Math.min(cy - cordStartY + cordStartTop, CORD_MAX));
    var h=document.getElementById('cordHandle'); if(h) h.style.top=dy+'px';
    var r=document.getElementById('cordRope'); if(r) r.style.height=dy+'px';

    if(dy>=CORD_MAX-8){
        cordArmed=true;
        return;
    }

    if(cordArmed && dy<=8){
        cordArmed=false;
        cordAutoCounted=true;
        onPull();
        snapCordBack();
        cordDrag=false;
    }
}
function cordUpGlobal(){
    if(!gameReady || !cordDrag) return;
    cordDrag=false;
    if(cordArmed){
        onPull();
    }
    cordArmed=false;
    cordAutoCounted=false;
    snapCordBack();
}
function onPull(){
    if(navigator.vibrate) navigator.vibrate(25);
    pullsDone++; updateCordUI();
    var drum=document.getElementById('cordDrum');
    if(drum) drum.style.transform='rotate('+(-pullsDone*36)+'deg)';
    if(pullsDone>=PULLS) setTimeout(finishCord, 120);
}
function snapCordBack(count){
    var h=document.getElementById('cordHandle'), r=document.getElementById('cordRope');
    if(h){ h.style.transition='top '+(CORD_RETURN_MS/1000)+'s cubic-bezier(.2,.8,.2,1)'; h.style.top='0px'; setTimeout(function(){ h.style.transition=''; }, CORD_RETURN_MS+20); }
    if(r){ r.style.transition='height '+(CORD_RETURN_MS/1000)+'s cubic-bezier(.2,.8,.2,1)'; r.style.height='0px'; setTimeout(function(){ r.style.transition=''; }, CORD_RETURN_MS+20); }
}
function updateCordUI(){
    var b=document.getElementById('cordBar'); if(b) b.style.width=(pullsDone/PULLS*100)+'%';
    var c=document.getElementById('cordCount'); if(c) c.textContent=pullsDone+' / '+PULLS;
}
function finishCord(){ completeGame(); }

// ==========================================================
//  ФИНИШ
// ==========================================================
function completeGame(){
    stopTimer(); setStage(3); gameReady=false; gameStarted=false;
    var ms=Date.now()-startTime;
    var square=document.getElementById('gameStageSquare'); if(square) square.classList.add('is-finished');
    var fo=document.getElementById('finishOverlay'); if(fo) fo.style.display='flex';
    var wp=document.getElementById('wirePanel'); if(wp) wp.style.display='none';
    var ft=document.getElementById('finishTime'); if(ft) ft.textContent=fmt(ms);
    var fr=document.getElementById('finishRank'); if(fr) fr.textContent='результат сохранён в таблицу';
    if(typeof IS_LOGGED_IN!=='undefined'&&IS_LOGGED_IN===true){
        fetch('/api/submit-score',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({timeMs:ms, generator: currentGenerator()})})
            .then(function(r){ return r.json().catch(function(){ return {}; }); })
            .then(function(body){
                loadLeaderboard();
                loadMyScores();
                if(window.showAchievementUnlocks) window.showAchievementUnlocks(body.newAchievements);
                if(window.showRankUnlock) window.showRankUnlock(body.rankNotification);
                var fr2=document.getElementById('finishRank'); if(fr2) fr2.textContent='результат добавлен в таблицу';
            })
            .catch(function(){
                var fr2=document.getElementById('finishRank'); if(fr2) fr2.textContent='не удалось сохранить результат';
            });
    }
}

// ==========================================================
//  LEADERBOARD
// ==========================================================
function loadLeaderboard(){
    fetch('/api/leaderboard?generator=' + encodeURIComponent(currentGenerator()))
        .then(function(r){return r.json();}).then(renderLB).catch(function(){});
}
function renderLB(list){
    var el=document.getElementById('lbList'); if(!el) return;
    if(!list||!list.length){ el.innerHTML='<div class="lb-empty">Пока никто не играл</div>'; return; }
    var me=typeof CURRENT_USER!=='undefined'?CURRENT_USER:'';
    el.innerHTML=list.map(function(e,i){
        var rc=i===0?'gold':i===1?'silver':i===2?'bronze':'';
        var displayName=e.robloxUsername||e.username||'Player';
        var av=e.robloxAvatarUrl?'<img src="'+e.robloxAvatarUrl+'" alt=""/>':'<span class="lb-av-txt">'+displayName[0].toUpperCase()+'</span>';
        var isMe = (e.username||'') === me || (e.robloxUsername||'') === me;
        var nm='<button type="button" class="lb-profile-btn'+(isMe?' is-me':'')+'" onclick="openLeaderboardProfile('+(e.id||0)+')">'+displayName+'</button>';
        return '<div class="lb-row'+(isMe?' lb-me':'')+'"><div class="lb-num '+rc+'">'+(i+1)+'</div><div class="lb-av">'+av+'</div><div class="lb-name">'+nm+'</div><div class="lb-time">'+e.timeFormatted+'</div></div>';
    }).join('');
    var myI=list.findIndex(function(e){ return (e.username||'')===me || (e.robloxUsername||'')===me; });
    var mr=document.getElementById('myRow');
    if(mr){
        if(myI>=0){
            var e=list[myI];
            var displayName=e.robloxUsername||e.username||'Player';
            mr.innerHTML='<div class="lb-my-inner"><div class="lb-num" style="color:#e05050">'+(myI+1)+'</div><div class="lb-av"><span class="lb-av-txt">'+displayName[0].toUpperCase()+'</span></div><div class="lb-name" style="color:#e05050">'+displayName+'</div><div class="lb-time" style="color:#e05050">'+e.timeFormatted+'</div></div><div class="lb-my-lbl">твоё место</div>';
        } else {
            mr.innerHTML='';
        }
    }
}
function loadMyScores(){
    fetch('/api/my-scores?generator=' + encodeURIComponent(currentGenerator()))
        .then(function(r){return r.json();}).then(function(d){
        var data = d && typeof d === 'object' && !Array.isArray(d) ? d : { username: typeof CURRENT_USER!=='undefined'?CURRENT_USER:'Игрок', scores: Array.isArray(d)?d:[] };
        var username=data.username||'Игрок';
        var scores=Array.isArray(data.scores)?data.scores:[];
        scores = scores.slice().sort(function(a,b){ return (a.timeMs||0) - (b.timeMs||0); });
        var best=document.getElementById('ppBest'); if(best){ best.textContent=scores.length ? 'лучший: '+scores[0].timeFormatted : '—'; }
        var user=document.getElementById('ppUser'); if(user){ user.textContent=username; }
        var list=document.getElementById('ppList'); if(!list) return;
        if(!scores.length){
            list.innerHTML='<div class="pp-empty">Пока нет попыток</div>';
            return;
        }
        list.innerHTML=scores.map(function(s,i){return '<div class="pp-row"><span>#'+(i+1)+'</span><span>'+username+'</span><span>'+s.timeFormatted+'</span></div>';}).join('');
    }).catch(function(){});
}

// ==========================================================
//  UTILS
// ==========================================================
function shuffle(a){ var b=a.slice(); for(var i=b.length-1;i>0;i--){ var j=Math.floor(Math.random()*(i+1)); var t=b[i];b[i]=b[j];b[j]=t; } return b; }

// ==========================================================
//  INIT
// ==========================================================
document.addEventListener('DOMContentLoaded',function(){
    document.addEventListener('mousemove',cordMoveGlobal);
    document.addEventListener('mouseup',cordUpGlobal);
    document.addEventListener('touchmove',function(e){ if(cordDrag){e.preventDefault();cordMoveGlobal(e);} },{passive:false});
    document.addEventListener('touchend',function(){ if(cordDrag) cordUpGlobal(); });

    initWires();
    initSwitches(); renderSwitches();
    initCord();
    setStage(0);
    gameReady=false;
    gameStarted=false;
    renderWires();

    loadLeaderboard();
    if(typeof IS_LOGGED_IN!=='undefined'&&IS_LOGGED_IN===true) loadMyScores();
});
