'use strict';
const $=id=>document.getElementById(id);
const agents=['gemini','grok','claude','deepseek','qwen','gpt','kimi'];
const names={gemini:'Gemini',grok:'Grok',claude:'Claude',deepseek:'DeepSeek',qwen:'Qwen',gpt:'GPT',kimi:'Kimi'};
const themes={gemini:['星屑花园','✧','#b29cda'],grok:['小小星轨','◌','#baa47e'],claude:['暖橘花枝','✿','#d9ab84'],deepseek:['泡泡海湾','○','#94b8cf'],qwen:['紫灯小径','◇','#bba0d0'],gpt:['叶间微风','❧','#95b49e'],kimi:['月光音符','☾','#a5afd3']};
const native=!!window.chrome?.webview;
let state={projects:[{id:'inbox',name:'收集箱',color:'#8b7ad7'}],active:'inbox',inbox:'inbox',selectedAgent:'gemini',shots:[],quiet:false,busy:false};
let filter='all',query='',selected=null,limit=60,toastTimer,wheelAngle=0,galleryKey='',navigationKey='',hangingKey='',detailAgentKey='';
let proposedShortcut=null,shortcutSaving=false;
const galleryCards=new Map();
const findShot=id=>state.shots.find(s=>s.id===id)||(state.hanging||[]).find(s=>s.id===id);
const node=(tag,cls,text)=>{const n=document.createElement(tag);if(cls)n.className=cls;if(text!==undefined)n.textContent=text;return n;};
function icon(name){const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');const use=document.createElementNS(svg.namespaceURI,'use');use.setAttribute('href','#i-'+name);svg.append(use);return svg;}
function avatar(agent,cls){const img=node('img',cls);img.src=`assets/agents/${agent}.jpg`;img.alt=names[agent]||agent;img.title=img.alt;return img;}
function toast(text){$('toast').textContent=text;$('toast').classList.add('show');clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').classList.remove('show'),3500);}
function send(action,extra={}){
 if(native){window.chrome.webview.postMessage({action,...extra});return;}
 if(action==='switch'){state.active=extra.id;demoRefresh();}
 else if(action==='create'){if(state.projects.some(p=>p.name.toLocaleLowerCase()===extra.name.toLocaleLowerCase()))return toast('已经有同名项目了。');const id=crypto.randomUUID();state.projects.push({id,name:extra.name,color:'#c39ab4'});state.active=id;state.shots=[];render();}
 else if(action==='favorite'){const shot=state.shots.find(s=>s.id===extra.id);if(shot){shot.favorite=!shot.favorite;render();}}
 else if(action==='agent'){const shot=state.shots.find(s=>s.id===extra.id);if(shot&&agents.includes(extra.agent)){shot.agent=extra.agent;render();}}
 else if(action==='note'){const shot=state.shots.find(s=>s.id===extra.id);if(shot){shot.note=extra.text;render();toast('演示备注已更新（仅本次预览）');}}
 else if(action==='quiet'){state.quiet=!state.quiet;render();}
 else if(action==='theme'&&agents.includes(extra.agent)){state.selectedAgent=extra.agent;render();}
 else if(action==='category'){const shot=findShot(extra.id);if(shot){shot.category=extra.category===state.inbox?null:extra.category;shot.note=extra.text;demoRefresh();}}
 else if(action==='clear-history'){state.lastClear=[...new Set([...(state.lastClear||[]),...demoShots.filter(s=>!s.archived&&!s.favorite).map(s=>s.id)])];demoShots.forEach(s=>{if(!s.favorite){s.archived=true;s.hung=false;}});if('skipConfirm' in extra)state.skipClearConfirm=!!extra.skipConfirm;state.canRestore=state.lastClear.length>0;demoRefresh();toast('已清理，可恢复最近一次清理。');}
 else if(action==='restore-history'){demoShots.forEach(s=>{if(state.lastClear?.includes(s.id))s.archived=false;});state.canRestore=false;demoRefresh();}
 else if(action==='hang'){const shot=findShot(extra.id);if(shot){shot.hung=true;demoRefresh();toast('桌面版会把它飞挂到屏幕上缘。');}}
 else if(action==='startup'){state.startup=extra.enabled;render();}
 else if(action==='ready')return;
 else toast('此处是外观预览；截图、文件与剪贴板操作请使用 AgentBloom 桌面版。');
}
function render(){
 $('hotkey-label').textContent=state.captureKeyLabel||'·';const captureHint=$('capture').querySelector('kbd');if(captureHint)captureHint.textContent=state.captureKeyLabel||'·';
 if($('shortcut-dialog').open){const input=$('shortcut-input'),wasDisabled=input.disabled;input.disabled=native&&!state.shortcutEditing;if(wasDisabled&&!input.disabled)input.focus();$('shortcut-save').disabled=!proposedShortcut||input.disabled;if(shortcutSaving&&!state.shortcutEditing)$('shortcut-dialog').close();}
 if(/^#[0-9a-f]{6}$/i.test(state.paletteAccent||'')){document.body.dataset.palette=state.palette;document.documentElement.style.setProperty('--accent',state.paletteAccent);document.documentElement.style.setProperty('--palette-wash',state.paletteWash);}
 const project=state.projects.find(p=>p.id===state.active)||state.projects[0];
 document.body.classList.toggle('is-busy',!!state.busy);document.querySelector('main').setAttribute('aria-busy',String(!!state.busy));
 document.body.classList.toggle('quiet',state.quiet);$('quiet').setAttribute('aria-pressed',String(state.quiet));
 const agent=state.selectedAgent||'gemini';const theme=themes[agent];const stage=document.querySelector('.rope-stage');stage.dataset.decoration=agent;stage.style.setProperty('--theme-color',state.paletteAccent||theme[2]);$('theme-description').textContent=names[agent]+' · '+theme[0];stage.querySelectorAll('.branch-flowers text').forEach(e=>e.textContent=theme[1]);document.querySelectorAll('.theme-agent').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.agent===agent)));
 $('keys').checked=state.keysRequested!==false;if(state.qa){$('preview-label').hidden=false;$('preview-label').textContent='验收模式 · 独立测试数据';}
 $('startup').checked=!!state.startup;$('restore-history').hidden=!state.canRestore;$('all-space').classList.toggle('active',state.active===state.inbox);
 $('shot-count').textContent=state.shots.length+' 张';
 const nextNavigationKey=JSON.stringify([state.active,state.projects]);
 if(nextNavigationKey!==navigationKey){navigationKey=nextNavigationKey;
 $('project-name').replaceChildren(document.createTextNode(project.name),node('span','title-flower','✿'));
 $('projects').replaceChildren(...state.projects.filter(p=>p.id!==state.inbox).map(p=>{
  const b=node('button','project-nav'+(p.id===state.active?' current':''));b.dataset.id=p.id;b.setAttribute('aria-current',p.id===state.active?'page':'false');
  const swatch=node('span','project-swatch');swatch.style.background=/^#[0-9a-f]{6}$/i.test(p.color)?p.color:'#8b7ad7';
  b.append(swatch,node('span','nav-name',p.name),node('span','nav-arrow',p.id===state.active?'›':''));b.title=p.name;
  b.onclick=()=>{limit=60;send('switch',{id:p.id});};b.disabled=state.busy;return b;
 }));
 }
 $('projects').querySelectorAll('button').forEach(b=>b.disabled=state.busy);
 const hanging=(state.hanging||state.shots.filter(s=>s.hung)).slice(0,4);
 $('stage-empty').hidden=hanging.length>0;
 const nextHangingKey=JSON.stringify(hanging.map(s=>[s.id,s.uri,s.name,s.agent]));
 if(nextHangingKey!==hangingKey){hangingKey=nextHangingKey;$('hanging').replaceChildren(...hanging.map((shot,i)=>{
  const b=node('button','hanging-card');b.style.setProperty('--tilt',[-5,3,-3,4][i]+'deg');b.style.setProperty('--drop',[0,17,4,12][i]+'px');b.style.setProperty('--delay',(-i*1.7)+'s');b.title=shot.name+'，点击查看';
  const image=node('img');image.src=shot.thumbnail||shot.uri;image.decoding='async';image.alt=shot.name;
  b.append(image,avatar(shot.agent,'pin-face'),node('small','',shot.name));b.onclick=()=>showDetail(shot.id);return b;
 }));}
 renderGallery();
 for(const id of ['capture','import','empty-capture','export','clear-history','restore-history'])$(id).disabled=state.busy;
 if(selected&&$('detail').open){const current=findShot(selected);if(current){$('detail-favorite').classList.toggle('saved',current.favorite);renderDetailAgent(current);}else $('detail').close();}
}
function renderGallery(){
 const key=JSON.stringify([state.active,state.shots.map(s=>[s.id,s.uri,s.thumbnail,s.name,s.date,s.width,s.height,s.note,s.favorite,s.agent,s.category,s.hung]),filter,query,limit]);if(key===galleryKey)return;galleryKey=key;
 const found=state.shots.filter(s=>(filter!=='favorite'||s.favorite)&&(filter!=='hung'||s.hung)&&(!query||(s.name+' '+(s.note||'')+' '+names[s.agent]).toLocaleLowerCase().includes(query)));
 const visible=found.slice(0,limit),retained=new Set(visible.map(s=>s.id));
 for(const id of galleryCards.keys())if(!retained.has(id))galleryCards.delete(id);
 const cards=visible.map((shot,i)=>{
  const signature=JSON.stringify([shot.uri,shot.thumbnail,shot.name,shot.date,shot.width,shot.height,shot.note,shot.favorite,shot.agent,i%6]);
  const previous=galleryCards.get(shot.id);if(previous?.signature===signature)return previous.card;
  const card=node('article','shot');card.tabIndex=0;card.setAttribute('role','button');card.setAttribute('aria-label','查看 '+shot.name);card.style.setProperty('--rotation',[-.8,.6,-.3,.85,-.5,.3][i%6]+'deg');
  const picture=node('div','shot-picture'),image=node('img');image.src=shot.thumbnail||shot.uri;image.alt=shot.name;image.loading='lazy';image.decoding='async';image.width=shot.width;image.height=shot.height;picture.append(image);
  image.onerror=()=>{if(shot.thumbnail&&image.getAttribute('src')===shot.thumbnail)image.src=shot.uri;};
  const operations=node('div','shot-operations');for(const action of ['copy','favorite']){const b=node('button',action==='favorite'&&shot.favorite?'saved':'');b.title=action==='copy'?'复制图片':shot.favorite?'取消收藏':'收藏图片';b.setAttribute('aria-label',b.title);b.append(icon(action==='favorite'?'star':'copy'));b.onclick=e=>{e.stopPropagation();send(action,{id:shot.id});};operations.append(b);}picture.append(operations);
  const caption=node('div','shot-caption'),copy=node('div','shot-caption-copy');copy.append(node('div','shot-title',shot.name),node('div','shot-date',shot.date));caption.append(avatar(shot.agent,'shot-agent'),copy);card.append(picture,caption);if(shot.note)card.append(node('p','shot-note',shot.note));
  card.onclick=()=>showDetail(shot.id);card.onkeydown=e=>{if(e.target===card&&['Enter',' '].includes(e.key)){e.preventDefault();showDetail(shot.id);}};galleryCards.set(shot.id,{signature,card});return card;
 });
 if(found.length>limit){const more=node('button','button secondary','再看看后面的 '+(found.length-limit)+' 张');more.onclick=()=>{limit+=60;renderGallery();};cards.push(more);}
 const gallery=$('gallery'),wanted=new Set(cards);
 for(const child of [...gallery.children])if(!wanted.has(child))child.remove();
 let cursor=gallery.firstChild;
 for(const card of cards){if(card===cursor)cursor=cursor.nextSibling;else gallery.insertBefore(card,cursor);}
 while(cursor){const next=cursor.nextSibling;cursor.remove();cursor=next;}
 $('empty').hidden=found.length>0;
 $('empty').querySelector('h2').textContent=state.shots.length?'这一页还没有找到匹配的小收藏':'给这个项目，留下一张小发现';
 $('empty').querySelector('p').textContent=state.shots.length?'换个关键词，或试试“全部”。':'代码片段、好看的设计、想记住的一句话，都可以。';
 $('empty-capture').hidden=state.shots.length>0;
 $('footer-text').textContent=found.length?`这里有 ${found.length} 张值得留下的片刻。`:'慢慢收集，灵感不必匆忙。';
}
function showDetail(id){
 const shot=findShot(id);if(!shot)return;selected=id;
 $('detail-image').src=shot.uri;$('detail-image').alt=shot.name;$('detail-name').textContent=shot.name;$('detail-meta').textContent=`${shot.width} × ${shot.height} 像素　${shot.date}`;
 renderDetailAgent(shot);$('note').value=shot.note||'';$('detail-favorite').classList.toggle('saved',shot.favorite);
 $('category').replaceChildren(...state.projects.map(p=>{const option=node('option','',p.id===state.inbox?'未分类（全部截图仍保留）':p.name);option.value=p.id;return option;}));$('category').value=shot.category||state.inbox;
 if(!$('detail').open)$('detail').showModal();
}
function renderDetailAgent(shot){
 const nextKey=shot.id+'|'+shot.agent;if(nextKey===detailAgentKey)return;detailAgentKey=nextKey;
 const restoreFocus=$('agent-picker').contains(document.activeElement);
 $('detail-agent').src=`assets/agents/${shot.agent}.jpg`;$('detail-agent-name').textContent=names[shot.agent];
 $('agent-picker').replaceChildren(...agents.map((agent,index)=>{const button=node('button','agent-choice'+(shot.agent===agent?' chosen':''));button.type='button';button.tabIndex=shot.agent===agent?0:-1;button.setAttribute('role','radio');button.setAttribute('aria-label',names[agent]);button.setAttribute('aria-checked',String(shot.agent===agent));button.title=names[agent];button.append(avatar(agent));button.onclick=()=>send('agent',{id:shot.id,agent});button.onkeydown=e=>{const d={ArrowRight:1,ArrowDown:1,ArrowLeft:-1,ArrowUp:-1}[e.key];if(d){e.preventDefault();send('agent',{id:shot.id,agent:agents[(index+d+agents.length)%agents.length]});}};return button;}));
 if(restoreFocus)$('agent-picker').querySelector('[aria-checked="true"]').focus();
}
function openProject(){$('project-input').value='';$('project-dialog').showModal();$('project-input').focus();}
for(const id of ['new-side','new-dashed'])$(id).onclick=openProject;
$('project-form').onsubmit=e=>{e.preventDefault();const name=$('project-input').value.trim();if(!name)return;$('project-dialog').close();send('create',{name});};
document.querySelectorAll('[data-close]').forEach(b=>b.onclick=()=>$(b.dataset.close).close());
for(const id of ['capture','empty-capture'])$(id).onclick=()=>send('capture');
$('import').onclick=()=>send('import');$('export').onclick=()=>send('export');$('quiet').onclick=()=>send('quiet');$('keys').onchange=()=>send('keys',{enabled:$('keys').checked});
$('startup').onchange=()=>send('startup',{enabled:$('startup').checked});$('clear-history').onclick=()=>{if(state.skipClearConfirm)send('clear-history');else{$('clear-skip').checked=false;$('clear-dialog').showModal();}};$('restore-history').onclick=()=>send('restore-history');$('window-hide').onclick=()=>{if(native)send('hide');else toast('桌面版会把收集箱收回左侧书签。');};
$('page-tab').onclick=()=>$('window-hide').click();
$('appearance').onclick=()=>send('appearance');
$('hotkey-settings').onclick=()=>openShortcutSettings();
function openShortcutSettings(){proposedShortcut=null;shortcutSaving=false;$('shortcut-input').value=state.captureKeyLabel||'·';$('shortcut-input').disabled=native;$('shortcut-save').disabled=true;if(!$('shortcut-dialog').open)$('shortcut-dialog').showModal();if(native)send('shortcut-begin');else{state.shortcutEditing=true;$('shortcut-input').disabled=false;$('shortcut-input').focus();}}
function recordShortcut(key,modifiers,label){proposedShortcut={key,modifiers,label};shortcutSaving=false;$('shortcut-input').value=label;$('shortcut-save').disabled=false;}
$('shortcut-input').onkeydown=e=>{if(e.key==='Escape')return;e.preventDefault();e.stopPropagation();if(['Control','Shift','Alt','Meta'].includes(e.key))return;let key;if(e.code==='Backquote')key=0xC0;else if(/^Key[A-Z]$/.test(e.code))key=e.code.charCodeAt(3);else if(/^Digit[0-9]$/.test(e.code))key=e.code.charCodeAt(5);else if(/^F([1-9]|1[0-9]|2[0-4])$/.test(e.code))key=0x6F+Number(e.code.slice(1));else key={PrintScreen:0x2C,Space:0x20,Insert:0x2D,Delete:0x2E,Home:0x24,End:0x23,PageUp:0x21,PageDown:0x22,Minus:0xBD,Equal:0xBB}[e.code];if(!key){toast('试试 F8，或 Ctrl / Alt 加上一个按键。');return;}const modifiers=(e.ctrlKey?2:0)|(e.altKey?1:0)|(e.shiftKey?4:0)|(e.metaKey?8:0);recordShortcut(key,modifiers,(e.ctrlKey?'Ctrl+':'')+(e.altKey?'Alt+':'')+(e.shiftKey?'Shift+':'')+(e.metaKey?'Win+':'')+(key===0xC0?'·':e.code.startsWith('Key')?e.code.slice(3):e.code.startsWith('Digit')?e.code.slice(5):e.key));};
$('shortcut-default').onclick=()=>recordShortcut(0xC0,0,'·');
$('shortcut-dialog').addEventListener('close',()=>{if(native)send('shortcut-end');else state.shortcutEditing=false;shortcutSaving=false;});
$('shortcut-form').onsubmit=e=>{e.preventDefault();if(!proposedShortcut)return;shortcutSaving=true;if(native)send('shortcut-save',proposedShortcut);else{state.captureKey=proposedShortcut.key;state.captureModifiers=proposedShortcut.modifiers;state.captureKeyLabel=proposedShortcut.label;state.shortcutEditing=false;render();toast('截图快捷键已保存 ✿');}};
$('window-minimize').onclick=()=>$('window-hide').click();
$('clear-form').onsubmit=e=>{e.preventDefault();$('clear-dialog').close();send('clear-history',{confirmed:true,skipConfirm:$('clear-skip').checked});};
$('category').onchange=()=>send('category',{id:selected,category:$('category').value,text:$('note').value});
$('drag-zone').onpointerdown=e=>{if(native&&!e.target.closest('button,input,select'))send('drag');};
$('save-note').onclick=()=>{if(selected)send('note',{id:selected,text:$('note').value});};
document.querySelectorAll('[data-shot-action]').forEach(b=>b.onclick=()=>{if(!selected)return;const action=b.dataset.shotAction;if(['pin','edit','open','save'].includes(action))$('detail').close();send(action,{id:selected});});
$('search').oninput=()=>{query=$('search').value.trim().toLocaleLowerCase();limit=60;renderGallery();};
document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{filter=b.dataset.filter;limit=60;document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('selected',x===b));renderGallery();});
let wheelAt=0;
$('reel').addEventListener('wheel',e=>{e.preventDefault();if(state.busy||Date.now()-wheelAt<100)return;wheelAt=Date.now();wheelAngle+=(e.deltaY>0?1:-1)*360/7;$('orbit-track').style.setProperty('--wheel-angle',wheelAngle+'deg');},{passive:false});
agents.forEach((agent,i)=>{const b=node('button','theme-agent');b.dataset.agent=agent;b.style.setProperty('--idle-delay',(-i*.7)+'s');b.title=names[agent]+' · '+themes[agent][0];b.setAttribute('aria-label',b.title);b.setAttribute('aria-pressed','false');b.style.setProperty('--orbit-x',64+46*Math.cos(i*2*Math.PI/7)+'px');b.style.setProperty('--orbit-y',64+46*Math.sin(i*2*Math.PI/7)+'px');b.append(avatar(agent));b.onclick=()=>{send('theme',{agent});if(state.quiet||matchMedia('(prefers-reduced-motion:reduce)').matches)return;for(let j=0;j<8;j++){const star=node('span','theme-spark',themes[agent][1]);star.style.setProperty('--x',Math.cos(j*Math.PI/4)*47+'px');star.style.setProperty('--y',Math.sin(j*Math.PI/4)*47+'px');b.append(star);star.onanimationend=()=>star.remove();}};$('orbit-track').append(b);});
$('all-space').onclick=()=>{filter='all';$('search').value='';query='';document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('selected',x.dataset.filter==='all'));send('switch',{id:state.inbox});};
function dismissCollection(){const dialog=document.querySelector('dialog[open]');if(dialog){dialog.close();return;}if(native&&!state.busy)send('hide');}
document.addEventListener('keydown',e=>{if(e.key==='Escape'){e.preventDefault();dismissCollection();return;}if(e.key==='/'&&!['INPUT','TEXTAREA'].includes(document.activeElement.tagName)&&!$('detail').open&&!$('project-dialog').open){e.preventDefault();$('search').focus();}});
document.addEventListener('visibilitychange',()=>{document.querySelectorAll('.companion,.hanging-card,.title-flower').forEach(el=>el.style.animationPlayState=document.hidden?'paused':'');});
// The decorative stage keeps its movement when visible, but does not animate offscreen.
const stageVisibility=new IntersectionObserver(entries=>{for(const entry of entries)entry.target.classList.toggle('motion-offscreen',!entry.isIntersecting);});
stageVisibility.observe(document.querySelector('.rope-stage'));
stageVisibility.observe(document.querySelector('.project-intro'));
$('companions').append(...agents.map((agent,i)=>{const img=avatar(agent,'companion');img.style.setProperty('--delay',(-i*1.3)+'s');return img;}));
$('empty-friends').append(...['gemini','gpt','claude'].map((a,i)=>{const img=avatar(a);img.style.setProperty('--delay',(-i*2)+'s');return img;}));
let demoShots=[];
function demoRefresh(){state.shots=demoShots.filter(s=>!s.archived&&(state.active===state.inbox||s.category===state.active));state.hanging=demoShots.filter(s=>s.hung&&!s.archived);render();}
if(!native){
 $('preview-label').hidden=false;
 if(new URLSearchParams(location.search).has('demo')){
  $('preview-label').textContent='演示素材，非实际截图';
  state.projects=[{id:'inbox',name:'收集箱',color:'#8b7ad7'},{id:'design',name:'界面灵感',color:'#9d8bcf'},{id:'code',name:'开发笔记',color:'#7ca4b4'},{id:'life',name:'生活小发现',color:'#c99baa'}];state.active='inbox';
  const demos=[['花园里的界面参考','garden',800,520,'gemini','留白像呼吸一样，把有用的部分留在视线里。'],['一段值得留住的实现','code',760,660,'claude','截图不只是图片，也是一段思路的入口。'],['让颜色轻一点','palette',800,460,'deepseek','鸢尾紫与叶绿，可以安静地待在一起。'],['画册的排版随想','notes',660,780,'gpt','错落，但要看得清。'],['小小的收藏地图','map',800,580,'qwen','先收集，再慢慢找到它们的关系。'],['给灵感一点空间','window',780,500,'kimi','把片刻挂起来，就不容易丢掉。']];
  demoShots=demos.map((d,i)=>({id:'demo'+i,category:'design',name:d[0],uri:`assets/demo/${d[1]}.svg`,width:d[2],height:d[3],agent:d[4],note:d[5],favorite:i===0||i===3,hung:i<4,date:'10月24日 '+(9+i)+':24'}));state.shots=demoShots;state.hanging=demoShots.filter(s=>s.hung);
 }
 render();
}else{window.chrome.webview.addEventListener('message',e=>{if(e.data.type==='state'){state=e.data;render();}else if(e.data.type==='toast')toast(e.data.text);});send('ready');}
