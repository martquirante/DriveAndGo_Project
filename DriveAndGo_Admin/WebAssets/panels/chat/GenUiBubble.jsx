/**
 * GenUiBubble.jsx — Generative UI Chat Bubble Renderer
 * Facebook Messenger-style: hover menus, emoji picker, modals, smooth transitions.
 */

const RechartsObj = (typeof window !== 'undefined' && window.Recharts) ? window.Recharts : {};
const { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } = RechartsObj;

const CHART_COLORS = [
  '#ea580c','#f59e0b','#3b82f6','#10b981',
  '#8b5cf6','#ec4899','#06b6d4','#84cc16'
];

const QUICK_EMOJIS = ['👍','❤️','😂','😮','😢','😡'];

/* ── Keyframe injection ─────────────────────────────────────────────────── */
(function injectStyles() {
  if (document.getElementById('gub-styles')) return;
  const s = document.createElement('style');
  s.id = 'gub-styles';
  s.textContent = `
    @keyframes gub-fade-in   { from{opacity:0} to{opacity:1} }
    @keyframes gub-scale-up  { from{opacity:0;transform:scale(0.88) translateY(6px)} to{opacity:1;transform:scale(1) translateY(0)} }
    @keyframes gub-emoji-pop { from{opacity:0;transform:scale(0.7) translateY(8px)} to{opacity:1;transform:scale(1) translateY(0)} }
    @keyframes gub-react-pop { from{opacity:0;transform:scale(0.5) translateY(10px)} to{opacity:1;transform:scale(1) translateY(0)} }
    @keyframes gub-slide-up  { from{opacity:0;transform:translateY(8px)} to{opacity:1;transform:translateY(0)} }
    .gub-overlay  { animation: gub-fade-in 0.18s ease forwards; }
    .gub-modal    { animation: gub-scale-up 0.22s cubic-bezier(0.34,1.56,0.64,1) forwards; }
    .gub-emoji-bar{ animation: gub-emoji-pop 0.2s cubic-bezier(0.34,1.56,0.64,1) forwards; }
    .gub-action-btn:hover { background: rgba(255,255,255,0.12) !important; }
    .gub-action-btn:active { transform: scale(0.88); }
    /* reaction pill */
    .gub-reaction-pill { cursor: pointer; transition: filter 0.15s, transform 0.12s; }
    .gub-reaction-pill:hover { filter: brightness(1.3) !important; transform: scale(1.06); }
    .gub-reaction-pill:active { transform: scale(0.94); }
    .gub-menu-item:hover { background: rgba(255,255,255,0.08) !important; }
    .gub-menu-item:active{ background: rgba(255,255,255,0.14) !important; }
    .gub-btn-cancel:hover  { background: rgba(255,255,255,0.08) !important; }
    .gub-btn-remove:hover  { background: #c0392b !important; }
    .gub-btn-send:hover    { background: #d95e08 !important; }
    @keyframes gub-bubble-in {
      from { opacity: 0; transform: translateY(10px) scale(0.97); }
      to   { opacity: 1; transform: translateY(0) scale(1); }
    }
    .gub-bubble-animate {
      animation: gub-bubble-in 0.32s cubic-bezier(0.16,1,0.3,1) forwards;
    }
    .gub-chart-clickable:hover {
      border-color: rgba(234,88,12,0.5) !important;
      box-shadow: 0 6px 24px rgba(234,88,12,0.2) !important;
      transform: translateY(-1px);
    }

    /* ═══════════════════════════════════════════════════════
       MESSENGER-STYLE HOVER SYSTEM
       ─ Emoji reaction strip appears ABOVE the bubble
       ─ Action icon bar appears BELOW the bubble
       ═══════════════════════════════════════════════════════ */

    /* Wrapper: relative so hover bars can be positioned */
    .gub-bubble-row {
      position: relative;
    }

    /* ── Reaction Emoji Strip (Messenger style, floats above 😊 button) ── */
    .gub-react-strip {
      position: absolute;
      bottom: calc(100% + 6px);
      display: flex;
      align-items: center;
      gap: 2px;
      background: #2a2b32;
      border: 1px solid rgba(255,255,255,0.14);
      border-radius: 999px;
      padding: 3px 6px;
      box-shadow: 0 8px 24px rgba(0,0,0,0.7);
      backdrop-filter: blur(12px);
      z-index: 60;
      white-space: nowrap;
      animation: fadeIn 0.15s ease-out;
    }
    .gub-react-strip.mine  { right: 0; }
    .gub-react-strip.theirs { left: 0; }

    .gub-react-emoji-btn {
      width: 30px;
      height: 30px;
      border: none;
      background: transparent;
      cursor: pointer;
      font-family: 'Segoe UI Emoji', 'Apple Color Emoji', sans-serif;
      font-size: 17px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: transform 0.15s cubic-bezier(0.34,1.56,0.64,1);
      line-height: 1;
    }
    .gub-react-emoji-btn:hover { transform: scale(1.35); }
    .gub-react-emoji-btn:active { transform: scale(0.9); }

    /* ── Action Icon Buttons (Inline Messenger style: [ ⋮ ] [ ↩️ ] [ 😊 ]) ── */
    .gub-action-bar {
      position: absolute;
      top: 50%;
      display: flex;
      align-items: center;
      gap: 3px;
      opacity: 0;
      pointer-events: none;
      transform: translateY(-50%);
      transition: opacity 0.15s ease;
      z-index: 50;
    }
    .gub-action-bar.mine   { right: calc(100% + 4px); left: auto; }
    .gub-action-bar.theirs { left: calc(100% + 4px); right: auto; }

    /* On narrow containers (e.g. Float mode), overlay action bar on top right of bubble so buttons never get cut off */
    @media (max-width: 580px) {
      .gub-action-bar.theirs {
        left: auto;
        right: 8px;
        top: -12px;
        transform: none;
        background: rgba(15, 23, 42, 0.92);
        backdrop-filter: blur(8px);
        padding: 3px 6px;
        border-radius: 999px;
        border: 1px solid rgba(255, 255, 255, 0.15);
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
      }
    }

    /* Hover on bubble row reveals inline 3 buttons cleanly without vertical jump */
    .gub-bubble-row:hover .gub-action-bar {
      opacity: 1;
      pointer-events: auto;
    }

    .gub-icon-btn {
      width: 28px;
      height: 28px;
      border: none;
      background: rgba(255,255,255,0.06);
      cursor: pointer;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: background 0.14s, transform 0.14s;
      color: #b0b3b8;
    }
    .gub-icon-btn:hover { background: rgba(255,255,255,0.15); color: #ffffff; transform: scale(1.1); }
    .gub-icon-btn:active { transform: scale(0.95); }
    .gub-icon-btn svg { display: block; }
    .gub-icon-btn:active { background: rgba(255,255,255,0.18); }
    .gub-icon-btn svg { display: block; }

    /* Separator between icon groups */
    .gub-icon-sep {
      width: 1px;
      height: 18px;
      background: rgba(255,255,255,0.1);
      flex-shrink: 0;
      margin: 0 1px;
    }

    /* ── Quoted Reply Bubble ── */
    .gub-reply-quote {
      border-left: 3px solid #ea580c;
      background: rgba(234,88,12,0.08);
      border-radius: 8px;
      padding: 5px 10px;
      margin-bottom: 4px;
      font-size: 10.5px;
      color: #94a3b8;
      cursor: pointer;
      transition: background 0.15s;
      max-width: 100%;
      overflow: hidden;
    }
    .gub-reply-quote:hover { background: rgba(234,88,12,0.14); }
    .gub-reply-quote .gub-rq-sender { color: #fb923c; font-weight: 700; font-size: 10px; margin-bottom: 2px; }
    .gub-reply-quote .gub-rq-body   { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

    /* ── Human incoming bubble ── */
    .gub-human-bubble {
      background: #2a2b32;
      border: 1px solid rgba(255,255,255,0.08);
      border-radius: 0 18px 18px 18px;
      padding: 10px 14px;
      min-width: 48px;
      max-width: 100%;
      box-sizing: border-box;
      word-break: break-word;
      overflow-wrap: break-word;
      white-space: pre-wrap;
      font-size: 13px;
      font-weight: 400;
      line-height: 1.55;
      color: #e2e8f0;
    }
    [data-theme="light"] .gub-human-bubble {
      background: #f1f5f9 !important;
      border: 1px solid #cbd5e1 !important;
      color: #0f172a !important;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }

    /* ── AI incoming bubble ── */
    .gub-ai-bubble {
      background: rgba(20,22,32,0.9);
      border: 1px solid rgba(234,88,12,0.2);
      border-radius: 0 18px 18px 18px;
      padding: 10px 14px;
      min-width: 48px;
      max-width: 100%;
      box-sizing: border-box;
      word-break: break-word;
      overflow-wrap: break-word;
      white-space: pre-wrap;
      font-size: 13px;
      font-weight: 400;
      line-height: 1.6;
      color: #e2e8f0;
    }
    [data-theme="light"] .gub-ai-bubble {
      background: #fff7ed !important;
      border: 1px solid #fed7aa !important;
      color: #1e293b !important;
      box-shadow: 0 1px 3px rgba(234,88,12,0.08);
    }

    /* ── Outgoing bubble ── */
    .gub-out-bubble {
      background: linear-gradient(135deg, #ea580c, #d97706);
      border-radius: 18px 18px 0 18px;
      padding: 10px 14px;
      min-width: 48px;
      max-width: 100%;
      box-sizing: border-box;
      word-break: break-word;
      overflow-wrap: break-word;
      white-space: pre-wrap;
      font-size: 13px;
      font-weight: 500;
      line-height: 1.55;
      color: #fff;
      box-shadow: 0 4px 16px rgba(234,88,12,0.25);
    }

    /* ── Image bubble Messenger-style ── */
    .gub-img-bubble {
      border-radius: 18px;
      overflow: hidden;
      cursor: pointer;
      display: block;
      max-width: 260px;
      width: 100%;
      min-height: 140px;
      aspect-ratio: 4/3;
      object-fit: cover;
      border: 1px solid rgba(255,255,255,0.1);
      transition: filter 0.15s;
      background-color: rgba(255,255,255,0.05);
      -webkit-user-select: auto !important;
      user-select: auto !important;
      pointer-events: auto !important;
    }
    .gub-img-bubble:hover { filter: brightness(0.9); }

    /* ── Voice note pill ── */
    .gub-voice-pill {
      display: flex;
      align-items: center;
      gap: 10px;
      background: linear-gradient(135deg, rgba(37,99,235,0.3), rgba(234,88,12,0.3));
      border: 1px solid rgba(255,255,255,0.15);
      border-radius: 999px;
      padding: 8px 14px;
      min-width: 210px;
      box-shadow: 0 4px 16px rgba(0,0,0,0.3);
    }

    @keyframes aiDotBounce {
      0%, 80%, 100% { transform: translateY(0) scale(0.85); opacity: 0.35; }
      40% { transform: translateY(-9px) scale(1.3); opacity: 1; filter: drop-shadow(0 4px 12px rgba(234,88,12,0.9)); }
    }
    @keyframes aiSparklePulse {
      0%, 100% { transform: scale(1) rotate(0deg); opacity: 0.9; }
      50%      { transform: scale(1.15) rotate(15deg); opacity: 1; filter: drop-shadow(0 0 10px rgba(234,88,12,0.8)); }
    }
  `;
  document.head.appendChild(s);
})();

/* ══════════════════════════════════════════════════════════════════════════
   MODAL COMPONENTS
══════════════════════════════════════════════════════════════════════════ */

/* ── Remove Confirmation Modal ──────────────────────────────────────────── */
function RemoveModal({ onConfirm, onCancel }) {
  return React.createElement('div', {
    className: 'gub-overlay',
    style: {
      position:'fixed', inset:0, zIndex:9999,
      background:'rgba(0,0,0,0.55)', backdropFilter:'blur(4px)',
      display:'flex', alignItems:'center', justifyContent:'center'
    },
    onClick: onCancel
  },
    React.createElement('div', {
      className: 'gub-modal',
      style: {
        background:'#242526', borderRadius:16, padding:'28px 28px 22px',
        maxWidth:360, width:'90%', boxShadow:'0 24px 64px rgba(0,0,0,0.7)',
        border:'1px solid rgba(255,255,255,0.1)'
      },
      onClick: e => e.stopPropagation()
    },
      React.createElement('div', { style:{ width:46, height:46, borderRadius:'50%', background:'rgba(224,45,60,0.15)', display:'flex', alignItems:'center', justifyContent:'center', margin:'0 auto 12px' } },
        React.createElement('svg', { width: 22, height: 22, viewBox: '0 0 24 24', fill: 'none', stroke: '#e02d3c', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
          React.createElement('polyline', { points: '3 6 5 6 21 6' }),
          React.createElement('path', { d: 'M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2' })
        )
      ),
      React.createElement('h3', { style:{color:'#e4e6eb',fontSize:16,fontWeight:700,textAlign:'center',margin:'0 0 10px'} }, 'Remove message?'),
      React.createElement('p', { style:{color:'#b0b3b8',fontSize:13,lineHeight:1.6,textAlign:'center',margin:'0 0 24px'} },
        'This message will be removed for you. Other chat members will still be able to see it.'
      ),
      React.createElement('div', { style:{display:'flex',gap:10} },
        React.createElement('button', {
          className: 'gub-btn-cancel',
          onClick: onCancel,
          style:{flex:1,padding:'10px',borderRadius:10,border:'1px solid rgba(255,255,255,0.15)',background:'transparent',color:'#e4e6eb',fontSize:14,fontWeight:600,cursor:'pointer',transition:'background 0.15s'}
        }, 'Cancel'),
        React.createElement('button', {
          className: 'gub-btn-remove',
          onClick: onConfirm,
          style:{flex:1,padding:'10px',borderRadius:10,border:'none',background:'#e02d3c',color:'white',fontSize:14,fontWeight:600,cursor:'pointer',transition:'background 0.15s'}
        }, 'Remove')
      )
    )
  );
}

/* ── Context Menu Options Modal (3-Dots Menu - Screenshot 2 Match) ─────── */
function ContextMenuModal({ isMine, onCopy, onReply, onForward, onRemove, onEdit, onPin, onClose }) {
  return React.createElement('div', {
    className: 'gub-overlay',
    style: {
      position: 'fixed', inset: 0, zIndex: 99999,
      background: 'rgba(0,0,0,0.45)', backdropFilter: 'blur(4px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center'
    },
    onClick: onClose
  },
    React.createElement('div', {
      className: 'gub-modal',
      style: {
        background: '#24252f', border: '1px solid rgba(255,255,255,0.15)',
        borderRadius: 14, padding: 6, width: 180,
        boxShadow: '0 20px 60px rgba(0,0,0,0.8)',
        display: 'flex', flexDirection: 'column', gap: 2
      },
      onClick: e => e.stopPropagation()
    },
      isMine && onEdit && React.createElement('button', {
        onClick: (e) => { e.stopPropagation(); onEdit(); onClose(); },
        style: { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', background: 'transparent', border: 'none', borderRadius: 8, color: '#e2e8f0', fontSize: 13, fontWeight: 600, cursor: 'pointer', textAlign: 'left', transition: 'background 0.15s' },
        onMouseEnter: (e) => e.target.style.background = 'rgba(255,255,255,0.1)',
        onMouseLeave: (e) => e.target.style.background = 'transparent'
      }, 'Edit'),
      React.createElement('button', {
        onClick: (e) => { e.stopPropagation(); onRemove(); onClose(); },
        style: { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', background: 'transparent', border: 'none', borderRadius: 8, color: '#e2e8f0', fontSize: 13, fontWeight: 600, cursor: 'pointer', textAlign: 'left', transition: 'background 0.15s' },
        onMouseEnter: (e) => e.target.style.background = 'rgba(255,255,255,0.1)',
        onMouseLeave: (e) => e.target.style.background = 'transparent'
      }, isMine ? 'Unsend' : 'Remove'),
      React.createElement('button', {
        onClick: (e) => { e.stopPropagation(); onForward(); onClose(); },
        style: { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', background: 'transparent', border: 'none', borderRadius: 8, color: '#e2e8f0', fontSize: 13, fontWeight: 600, cursor: 'pointer', textAlign: 'left', transition: 'background 0.15s' },
        onMouseEnter: (e) => e.target.style.background = 'rgba(255,255,255,0.1)',
        onMouseLeave: (e) => e.target.style.background = 'transparent'
      }, 'Forward'),
      React.createElement('button', {
        onClick: (e) => { e.stopPropagation(); if (onPin) onPin(); onClose(); },
        style: { display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', background: 'transparent', border: 'none', borderRadius: 8, color: '#e2e8f0', fontSize: 13, fontWeight: 600, cursor: 'pointer', textAlign: 'left', transition: 'background 0.15s' },
        onMouseEnter: (e) => e.target.style.background = 'rgba(255,255,255,0.1)',
        onMouseLeave: (e) => e.target.style.background = 'transparent'
      }, 'Pin')
    )
  );
}

/* ── Forward Message Modal ──────────────────────────────────────────────── */
function ForwardModal({ onSend, onCancel }) {
  const [search, setSearch] = React.useState('');
  const MOCK_CONTACTS = [
    { id:'user_1', name:'Juan dela Cruz', role:'Driver',   avatar:'J' },
    { id:'user_2', name:'Maria Santos',   role:'Customer', avatar:'M' },
    { id:'user_3', name:'Carlos Reyes',   role:'Driver',   avatar:'C' },
    { id:'user_4', name:'Ana Lim',        role:'Customer', avatar:'A' },
    { id:'user_5', name:'Fleet Group',    role:'Group',    avatar:'G' },
  ];
  const filtered = MOCK_CONTACTS.filter(c =>
    c.name.toLowerCase().includes(search.toLowerCase()) ||
    c.role.toLowerCase().includes(search.toLowerCase())
  );
  const roleColor = r => r==='Driver'?'#3b82f6':r==='Group'?'#22c55e':'#a855f7';

  return React.createElement('div', {
    className: 'gub-overlay',
    style:{position:'fixed',inset:0,zIndex:9999,background:'rgba(0,0,0,0.55)',backdropFilter:'blur(4px)',display:'flex',alignItems:'center',justifyContent:'center'},
    onClick: onCancel
  },
    React.createElement('div', {
      className:'gub-modal',
      style:{background:'#242526',borderRadius:16,padding:'22px 22px 16px',maxWidth:380,width:'92%',boxShadow:'0 24px 64px rgba(0,0,0,0.7)',border:'1px solid rgba(255,255,255,0.1)'},
      onClick: e => e.stopPropagation()
    },
      React.createElement('div', {style:{display:'flex',alignItems:'center',justifyContent:'space-between',marginBottom:16}},
        React.createElement('h3', {style:{color:'#e4e6eb',fontSize:16,fontWeight:700,margin:0}}, 'Forward Message'),
        React.createElement('button',{onClick:onCancel,style:{background:'none',border:'none',color:'#b0b3b8',fontSize:20,cursor:'pointer',lineHeight:1}}, '×')
      ),
      React.createElement('input',{
        type:'text', placeholder:'Search people or groups...',
        value:search, onChange:e=>setSearch(e.target.value),
        style:{width:'100%',boxSizing:'border-box',background:'#3a3b3c',border:'1px solid rgba(255,255,255,0.1)',borderRadius:22,padding:'9px 14px',color:'#e4e6eb',fontSize:13,outline:'none',marginBottom:14}
      }),
      React.createElement('p',{style:{color:'#b0b3b8',fontSize:11,fontWeight:600,letterSpacing:'0.05em',marginBottom:8}},'RECENT'),
      React.createElement('div',{style:{display:'flex',flexDirection:'column',gap:2,maxHeight:260,overflowY:'auto'}},
        filtered.map(c =>
          React.createElement('div',{
            key:c.id,
            style:{display:'flex',alignItems:'center',gap:12,padding:'8px 10px',borderRadius:10,transition:'background 0.15s',cursor:'default'},
            className:'gub-menu-item'
          },
            React.createElement('div',{style:{width:38,height:38,borderRadius:'50%',background:roleColor(c.role),display:'flex',alignItems:'center',justifyContent:'center',color:'white',fontWeight:700,fontSize:14,flexShrink:0}}, c.avatar),
            React.createElement('div',{style:{flex:1,minWidth:0}},
              React.createElement('div',{style:{color:'#e4e6eb',fontSize:13,fontWeight:600}}),
              React.createElement('div',{style:{color:'#b0b3b8',fontSize:11}}),
              c.name, ' ', React.createElement('span',{style:{color:roleColor(c.role),fontSize:10}}, c.role)
            ),
            React.createElement('button',{
              className:'gub-btn-send',
              onClick:()=>onSend(c.id, c.name),
              style:{padding:'6px 14px',borderRadius:20,border:'none',background:'#ea580c',color:'white',fontSize:12,fontWeight:700,cursor:'pointer',transition:'background 0.15s',flexShrink:0}
            }, 'Send')
          )
        )
      )
    )
  );
}

/* ── Reaction Details Modal (Screenshot 5 Match) ───────────────────────── */
function ReactionDetailsModal({ reactions, onClose, onRemoveOwnReaction }) {
  const parsed = React.useMemo(() => {
    if (!reactions || reactions === '{}') return {};
    try { return typeof reactions === 'string' ? JSON.parse(reactions) : reactions; } catch { return {}; }
  }, [reactions]);

  const entries = Object.entries(parsed); // [[userId, emoji], ...]
  const emojiGroups = entries.reduce((acc, [uid, emoji]) => {
    acc[emoji] = acc[emoji] || [];
    acc[emoji].push(uid);
    return acc;
  }, {});
  const emojiList = Object.keys(emojiGroups);
  const [tab, setTab] = React.useState('All');
  const displayEntries = tab === 'All' ? entries : entries.filter(([,e]) => e === tab);

  return React.createElement('div',{
    className:'gub-overlay',
    style:{position:'fixed',inset:0,zIndex:9999,background:'rgba(0,0,0,0.6)',backdropFilter:'blur(4px)',display:'flex',alignItems:'center',justifyContent:'center'},
    onClick:onClose
  },
    React.createElement('div',{
      className:'gub-modal',
      style:{background:'#24252f',borderRadius:16,padding:'0',maxWidth:440,width:'92%',boxShadow:'0 24px 64px rgba(0,0,0,0.8)',border:'1px solid rgba(255,255,255,0.12)',overflow:'hidden'},
      onClick:e=>e.stopPropagation()
    },
      React.createElement('div',{style:{display:'flex',alignItems:'center',justifyContent:'space-between',padding:'18px 20px 12px'}},
        React.createElement('h3',{style:{color:'#f1f5f9',fontSize:18,fontWeight:700,margin:0,flex:1,textAlign:'center'}},'Message reactions'),
        React.createElement('button',{onClick:onClose,style:{background:'rgba(255,255,255,0.1)',border:'none',color:'#94a3b8',width:30,height:30,borderRadius:'50%',fontSize:16,cursor:'pointer',display:'flex',alignItems:'center',justifyContent:'center'}}, '✕')
      ),
      React.createElement('div',{style:{display:'flex',gap:16,padding:'0 20px',borderBottom:'1px solid rgba(255,255,255,0.1)'}},
        ['All', ...emojiList].map(t =>
          React.createElement('button',{
            key:t,
            onClick:()=>setTab(t),
            style:{
              padding:'8px 4px',border:'none',cursor:'pointer',
              fontSize: t === 'All' ? 14 : 16,
              fontWeight:700,transition:'all 0.15s',
              background:'transparent',
              color: tab===t ? '#3b82f6' : '#94a3b8',
              borderBottom: tab===t ? '3px solid #3b82f6' : '3px solid transparent',
              lineHeight:1
            }
          }, t === 'All' ? `All ${entries.length}` : `${t} ${emojiGroups[t].length}`)
        )
      ),
      React.createElement('div',{style:{maxHeight:280,overflowY:'auto',padding:'8px 0'}},
        displayEntries.length === 0
          ? React.createElement('p',{style:{color:'#94a3b8',fontSize:13,textAlign:'center',padding:'20px'}},'No reactions yet')
          : displayEntries.map(([uid, emojiVal], i) => {
              const emoji = typeof emojiVal === 'string' ? emojiVal : (emojiVal?.emoji || emojiVal?.reaction || '');
              const avatar = typeof emojiVal === 'object' ? (emojiVal?.avatarUrl || emojiVal?.avatar_url || emojiVal?.avatarBase64) : null;
              const displayName = uid === 'admin' ? 'Raymart Quirante' : (typeof emojiVal === 'object' && emojiVal?.userName ? emojiVal.userName : uid);
              const isOwn = uid === 'admin';

              const renderAvatar = () => {
                if (avatar && typeof avatar === 'string' && avatar.length > 5) {
                  const src = (avatar.startsWith('http') || avatar.startsWith('data:') || avatar.startsWith('blob:')) ? avatar : `data:image/png;base64,${avatar}`;
                  return React.createElement('img', { src, alt: displayName, style: { width: '100%', height: '100%', objectFit: 'cover', borderRadius: '50%' } });
                }
                if (uid === 'admin' || uid === '1' || uid === 1) {
                  const adminPic = typeof window !== 'undefined' && (window.ADMIN_AVATAR || localStorage.getItem('admin_avatar') || localStorage.getItem('user_avatar'));
                  if (adminPic && adminPic.length > 20) {
                    const src = (adminPic.startsWith('http') || adminPic.startsWith('data:') || adminPic.startsWith('blob:')) ? adminPic : `data:image/png;base64,${adminPic}`;
                    return React.createElement('img', { src, alt: displayName, style: { width: '100%', height: '100%', objectFit: 'cover', borderRadius: '50%' } });
                  }
                }
                return (displayName[0] || '?').toUpperCase();
              };

              return React.createElement('div',{
                key:i,
                onClick: isOwn ? onRemoveOwnReaction : undefined,
                style:{display:'flex',alignItems:'center',gap:14,padding:'10px 20px',cursor: isOwn ? 'pointer' : 'default', transition:'background 0.12s'},
                onMouseEnter: (e) => e.currentTarget.style.background = 'rgba(255,255,255,0.05)',
                onMouseLeave: (e) => e.currentTarget.style.background = 'transparent'
              },
                React.createElement('div',{style:{width:40,height:40,borderRadius:'50%',background:'linear-gradient(135deg,#3b82f6,#8b5cf6)',display:'flex',alignItems:'center',justifyContent:'center',color:'white',fontWeight:700,fontSize:14,flexShrink:0,overflow:'hidden'}},
                  renderAvatar()
                ),
                React.createElement('div',{style:{display:'flex',flexDirection:'column',flex:1,minWidth:0}},
                  React.createElement('span',{style:{color:'#f1f5f9',fontSize:14,fontWeight:600}}, displayName),
                  isOwn && React.createElement('span',{style:{color:'#94a3b8',fontSize:11}}, 'Click to remove')
                ),
                React.createElement('span',{style:{fontFamily:'Segoe UI Emoji, Apple Color Emoji, sans-serif', fontSize:22}}, emoji)
              );
            })
      )
    )
  );
}

/* ── Chart Fullscreen Modal ─────────────────────────────────────────────── */
function ChartFullscreenModal({ componentType, data, onClose }) {
  const [showToast, setShowToast] = React.useState(false);

  const copyImage = async () => {
    try {
      if (componentType === 'image' && data) {
        const response = await fetch(data);
        const blob = await response.blob();
        await navigator.clipboard.write([
          new ClipboardItem({ [blob.type]: blob })
        ]);
        setShowToast(true);
        setTimeout(() => setShowToast(false), 2500);
      }
    } catch (err) {
      try {
        await navigator.clipboard.writeText(data);
        setShowToast(true);
        setTimeout(() => setShowToast(false), 2500);
      } catch (e) {}
    }
  };

  const handleContext = (e) => {
    // Explicitly allow native Chromium context menu ("Copy image", "Save image as...", "Copy link address")
    if (e.target.tagName === 'IMG' || e.target.tagName === 'VIDEO' || e.target.closest('.gub-img-bubble')) {
      return true;
    }
  };

  React.useEffect(() => {
    try {
      if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage(JSON.stringify({ action: 'TOGGLE_FULLSCREEN', enabled: true }));
      }
    } catch(e) {}

    const handleKeyDown = (e) => { if (e.key === 'Escape') {
      try {
        if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
          window.chrome.webview.postMessage(JSON.stringify({ action: 'TOGGLE_FULLSCREEN', enabled: false }));
        }
      } catch(e) {}
      onClose();
    }};
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      try {
        if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
          window.chrome.webview.postMessage(JSON.stringify({ action: 'TOGGLE_FULLSCREEN', enabled: false }));
        }
      } catch(e) {}
    };
  }, [onClose]);

  const modalContent = React.createElement('div', {
    className: 'gub-overlay',
    style: {
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      width: '100vw', height: '100vh', zIndex: 999999,
      background: 'rgba(10, 15, 26, 0.92)',
      backdropFilter: 'blur(16px)', WebkitBackdropFilter: 'blur(16px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      padding: '16px', boxSizing: 'border-box'
    },
    onClick: onClose
  },
    React.createElement('div', {
      className: 'gub-modal',
      style: {
        background: '#161922',
        borderRadius: 20,
        padding: '20px 24px',
        width: '94vw', maxWidth: 1100, maxHeight: '92vh',
        boxShadow: '0 32px 96px rgba(0,0,0,0.9), 0 0 0 1px rgba(255,255,255,0.15)',
        border: '1px solid rgba(255,255,255,0.12)',
        display: 'flex', flexDirection: 'column', gap: 14,
        overflow: 'hidden', boxSizing: 'border-box', position: 'relative'
      },
      onClick: e => e.stopPropagation()
    },
      // Header
      React.createElement('div', {
        style: {
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          borderBottom: '1px solid rgba(255,255,255,0.08)', paddingBottom: 12,
          gap: 12, flexWrap: 'nowrap'
        }
      },
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 10, minWidth: 0, overflow: 'hidden' } },
          React.createElement('span', { style: { width: 10, height: 10, borderRadius: '50%', background: '#ea580c', boxShadow: '0 0 10px #ea580c', flexShrink: 0 } }),
          React.createElement('h3', { style: { color: '#f8fafc', fontSize: 16, fontWeight: 700, margin: 0, letterSpacing: '0.03em', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' } }, componentType + ' — Fullscreen View'),
          React.createElement('span', { style: { fontSize: 10, color: '#ea580c', background: 'rgba(234,88,12,0.15)', border: '1px solid rgba(234,88,12,0.3)', borderRadius: 999, padding: '3px 10px', fontWeight: 700, whiteSpace: 'nowrap', flexShrink: 0 } }, 'HD Interactive')
        ),
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 8 } },
          componentType === 'image' && React.createElement('button', {
            onClick: copyImage,
            title: 'Copy image to clipboard',
            style: {
              background: '#ea580c', border: 'none', borderRadius: 8,
              padding: '6px 14px', color: 'white', fontSize: 12, fontWeight: 700,
              cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 6
            }
          }, '📋 Copy Image'),
          React.createElement('button', {
            onClick: onClose,
            title: 'Close (ESC)',
            style: {
              background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)',
              borderRadius: '50%', width: 34, height: 34, color: '#e4e6eb',
              fontSize: 20, cursor: 'pointer', display: 'flex', alignItems: 'center',
              justifyContent: 'center', transition: 'all 0.15s', flexShrink: 0
            }
          }, '×')
        )
      ),
      // Expanded Chart/Media Body
      React.createElement('div', { 
        style: { flex: 1, overflowY: 'auto', overflowX: 'auto', padding: '8px 4px', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative' },
        onContextMenu: handleContext
      },
        componentType === 'image'      && React.createElement('img', { src: data, onClick: copyImage, title: 'Click to copy image to clipboard', style: { maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', borderRadius: 12, cursor: 'pointer' } }),
        componentType === 'video'      && React.createElement('video', { src: data, controls: true, autoPlay: true, style: { maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', borderRadius: 12 } }),
        componentType === 'BarChart'   && React.createElement(SvgBarChart,   { data, isLarge: true }),
        componentType === 'PieChart'   && React.createElement(SvgDonutChart, { data, isLarge: true }),
        componentType === 'MetricCard' && React.createElement(MetricCardGrid,{ data, isLarge: true }),
        componentType === 'DataGrid'   && React.createElement(DataGrid,      { data, isLarge: true })
      ),

      showToast && React.createElement('div', {
        style: {
          position: 'absolute', bottom: 60, left: '50%', transform: 'translateX(-50%)',
          background: 'linear-gradient(135deg,#ea580c,#d97706)', color: 'white',
          padding: '8px 20px', borderRadius: 999, fontSize: 12, fontWeight: 700,
          boxShadow: '0 8px 24px rgba(0,0,0,0.5)', zIndex: 100000,
          animation: 'gub-fade-in 0.2s ease forwards'
        }
      }, 'Copied image to clipboard! 📋'),

      // Footer hint & Close button
      React.createElement('div', {
        style: {
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
          borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: 10, gap: 10
        }
      },
        React.createElement('span', { style: { fontSize: 11, color: '#64748b', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' } }, '💡 Click image or button to copy to clipboard • Press ESC to return'),
        React.createElement('button', {
          onClick: onClose,
          style: {
            background: 'linear-gradient(135deg,#ea580c,#d97706)', color: 'white',
            border: 'none', borderRadius: 8, padding: '7px 18px', fontWeight: 700,
            fontSize: 12, cursor: 'pointer', whiteSpace: 'nowrap', flexShrink: 0,
            boxShadow: '0 4px 12px rgba(234,88,12,0.3)'
          }
        }, 'Close')
      )
    )
  );

  const ReactDOMObj = (typeof window !== 'undefined' && window.ReactDOM) ? window.ReactDOM : null;
  if (ReactDOMObj && typeof ReactDOMObj.createPortal === 'function' && document.body) {
    return ReactDOMObj.createPortal(modalContent, document.body);
  }
  return modalContent;
}

/* ══════════════════════════════════════════════════════════════════════════
   CHART / UI HELPERS (unchanged from original)
══════════════════════════════════════════════════════════════════════════ */
/* ══════════════════════════════════════════════════════════════════════════
   INTERACTIVE MARKDOWN TABLE WITH FULLSCREEN LIGHTBOX
══════════════════════════════════════════════════════════════════════════ */
function MarkdownTable({ headerCols, dataRows }) {
  const [isFullscreen, setIsFullscreen] = React.useState(false);
  const [searchQuery, setSearchQuery] = React.useState('');

  React.useEffect(() => {
    try {
      if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage(JSON.stringify({ action: 'TOGGLE_FULLSCREEN', enabled: isFullscreen }));
      }
    } catch(e) {}

    if (!isFullscreen) return;
    const handleKeyDown = (e) => { if (e.key === 'Escape') setIsFullscreen(false); };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isFullscreen]);

  const filteredRows = React.useMemo(() => {
    if (!searchQuery.trim()) return dataRows;
    const q = searchQuery.toLowerCase();
    return dataRows.filter(row => row.some(cell => String(cell).toLowerCase().includes(q)));
  }, [dataRows, searchQuery]);

  const isDark = (typeof document !== 'undefined' && document.documentElement.getAttribute('data-theme') !== 'light');

  const modalContent = isFullscreen ? React.createElement('div', {
    className: 'gub-overlay',
    style: {
      position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
      width: '100vw', height: '100vh', zIndex: 999999,
      background: isDark ? 'rgba(10, 15, 26, 0.92)' : 'rgba(15, 23, 42, 0.55)',
      backdropFilter: 'blur(16px)', WebkitBackdropFilter: 'blur(16px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      padding: '12px', boxSizing: 'border-box'
    },
    onClick: () => setIsFullscreen(false)
  },
    React.createElement('div', {
      className: 'gub-modal',
      style: {
        background: isDark ? '#131722' : '#ffffff',
        borderRadius: 16,
        padding: '16px 20px',
        width: '98vw', maxWidth: 1250, maxHeight: '94vh',
        boxShadow: isDark ? '0 32px 96px rgba(0,0,0,0.95), 0 0 0 1px rgba(255,255,255,0.15)' : '0 32px 96px rgba(15,23,42,0.22), 0 0 0 1px rgba(0,0,0,0.08)',
        border: isDark ? '1px solid rgba(255,255,255,0.14)' : '1px solid #cbd5e1',
        display: 'flex', flexDirection: 'column', gap: 12,
        overflow: 'hidden', boxSizing: 'border-box'
      },
      onClick: e => e.stopPropagation()
    },
      // Modal Header
      React.createElement('div', {
        style: {
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          borderBottom: isDark ? '1px solid rgba(255,255,255,0.1)' : '1px solid #e2e8f0', paddingBottom: 10,
          gap: 8, flexWrap: 'nowrap', width: '100%', overflow: 'hidden'
        }
      },
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 8, minWidth: 0, flex: 1, overflow: 'hidden' } },
          React.createElement('span', { style: { width: 8, height: 8, borderRadius: '50%', background: '#ea580c', boxShadow: '0 0 8px #ea580c', flexShrink: 0 } }),
          React.createElement('h3', { style: { color: isDark ? '#f8fafc' : '#0f172a', fontSize: 13, fontWeight: 700, margin: 0, letterSpacing: '0.02em', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' } }, 'Fleet Overview — Fullscreen HD View'),
          React.createElement('span', { style: { fontSize: 9.5, color: '#ea580c', background: 'rgba(234,88,12,0.18)', border: '1px solid rgba(234,88,12,0.4)', borderRadius: 999, padding: '2px 8px', fontWeight: 700, flexShrink: 0, whiteSpace: 'nowrap' } }, `${filteredRows.length} Rows`)
        ),
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 } },
          React.createElement('div', { style: { position: 'relative', display: 'flex', alignItems: 'center' } },
            React.createElement('svg', {
              width: 12, height: 12, viewBox: '0 0 24 24', fill: 'none', stroke: isDark ? '#94a3b8' : '#64748b',
              strokeWidth: 2.2, strokeLinecap: 'round', strokeLinejoin: 'round',
              style: { position: 'absolute', left: 9, pointerEvents: 'none' }
            },
              React.createElement('circle', { cx: '11', cy: '11', r: '8' }),
              React.createElement('line', { x1: '21', y1: '21', x2: '16.65', y2: '16.65' })
            ),
            React.createElement('input', {
              type: 'text',
              placeholder: 'Search table data...',
              value: searchQuery,
              onChange: e => setSearchQuery(e.target.value),
              style: {
                background: isDark ? 'rgba(255,255,255,0.06)' : '#f8fafc',
                border: isDark ? '1px solid rgba(255,255,255,0.18)' : '1px solid #cbd5e1',
                borderRadius: 6, padding: '4px 10px 4px 28px', color: isDark ? '#f8fafc' : '#0f172a', fontSize: 11,
                outline: 'none', width: 145
              }
            })
          ),
          React.createElement('button', {
            onClick: () => setIsFullscreen(false),
            title: 'Close (ESC)',
            style: {
              background: isDark ? 'rgba(255,255,255,0.08)' : '#f1f5f9',
              border: isDark ? '1px solid rgba(255,255,255,0.18)' : '1px solid #cbd5e1',
              borderRadius: '50%', width: 28, height: 28, color: isDark ? '#e4e6eb' : '#334155',
              fontSize: 18, cursor: 'pointer', display: 'flex', alignItems: 'center',
              justifyContent: 'center', transition: 'all 0.15s', flexShrink: 0
            }
          }, '×')
        )
      ),
      // Table Content
      React.createElement('div', { style: { flex: 1, overflowY: 'auto', overflowX: 'auto', borderRadius: 12, border: isDark ? '1px solid rgba(255,255,255,0.08)' : '1px solid #e2e8f0' } },
        React.createElement('table', { style: { width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' } },
          React.createElement('thead', null,
            React.createElement('tr', { style: { background: isDark ? 'rgba(234, 88, 12, 0.2)' : '#ffedd5', borderBottom: isDark ? '1px solid rgba(255,255,255,0.12)' : '1px solid #fed7aa', position: 'sticky', top: 0, zIndex: 1 } },
              headerCols.map((col, cIdx) =>
                React.createElement('th', {
                  key: cIdx,
                  style: { padding: '12px 16px', color: isDark ? '#fb923c' : '#c2410c', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', whiteSpace: 'nowrap' },
                  dangerouslySetInnerHTML: { __html: parseInlineMarkdown(col) }
                })
              )
            )
          ),
          React.createElement('tbody', null,
            filteredRows.length > 0 ? filteredRows.map((row, rIdx) =>
              React.createElement('tr', {
                key: rIdx,
                style: {
                  borderBottom: isDark ? '1px solid rgba(255,255,255,0.05)' : '1px solid #f1f5f9',
                  background: isDark
                    ? (rIdx % 2 === 0 ? 'rgba(255,255,255,0.01)' : 'rgba(255,255,255,0.04)')
                    : (rIdx % 2 === 0 ? '#ffffff' : '#f8fafc')
                }
              },
                row.map((cell, cIdx) =>
                  React.createElement('td', {
                    key: cIdx,
                    style: { padding: '12px 16px', color: isDark ? '#e2e8f0' : '#0f172a', fontWeight: 600, whiteSpace: 'nowrap' },
                    dangerouslySetInnerHTML: { __html: parseInlineMarkdown(cell) }
                  })
                )
              )
            ) : React.createElement('tr', null,
              React.createElement('td', { colSpan: headerCols.length, style: { padding: 32, textAlign: 'center', color: '#64748b', fontStyle: 'italic' } }, 'No matching rows found.')
            )
          )
        )
      ),
      // Footer
      React.createElement('div', { style: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', paddingTop: 8 } },
        React.createElement('span', { style: { fontSize: 11, color: '#64748b' } }, '💡 Press ESC or click Close to return'),
        React.createElement('button', {
          onClick: () => setIsFullscreen(false),
          style: {
            background: 'linear-gradient(135deg,#ea580c,#d97706)', color: 'white',
            border: 'none', borderRadius: 8, padding: '8px 20px', fontWeight: 700,
            fontSize: 12, cursor: 'pointer'
          }
        }, 'Close View')
      )
    )
  ) : null;

  const ReactDOMObj = (typeof window !== 'undefined' && window.ReactDOM) ? window.ReactDOM : null;
  const renderedPortal = (isFullscreen && ReactDOMObj && typeof ReactDOMObj.createPortal === 'function' && document.body)
    ? ReactDOMObj.createPortal(modalContent, document.body)
    : modalContent;

  return React.createElement(React.Fragment, null,
    React.createElement('div', {
      className: 'gub-chart-clickable',
      onClick: () => setIsFullscreen(true),
      title: 'Click to open in Fullscreen HD view',
      style: {
        overflowX: 'auto',
        margin: '10px 0',
        borderRadius: 12,
        border: '1px solid rgba(234, 88, 12, 0.3)',
        background: 'rgba(15, 23, 42, 0.6)',
        cursor: 'pointer',
        boxShadow: '0 4px 14px rgba(0,0,0,0.2)',
        position: 'relative',
        transition: 'all 0.2s'
      }
    },
      React.createElement('div', {
        style: {
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          padding: '8px 12px', background: 'rgba(234, 88, 12, 0.12)',
          borderBottom: '1px solid rgba(234, 88, 12, 0.25)'
        }
      },
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 6 } },
          React.createElement('span', { style: { width: 6, height: 6, borderRadius: '50%', background: '#ea580c', boxShadow: '0 0 6px #ea580c' } }),
          React.createElement('span', { style: { fontSize: 9.5, fontWeight: 700, color: '#fb923c', textTransform: 'uppercase', letterSpacing: '0.06em' } }, 'Fleet Overview')
        ),
        React.createElement('div', {
          style: { fontSize: 8.5, fontWeight: 700, color: '#ea580c', background: 'rgba(234,88,12,0.18)', border: '1px solid rgba(234,88,12,0.4)', borderRadius: 6, padding: '2px 8px', display: 'flex', alignItems: 'center', gap: 4 }
        }, '⛶ Fullscreen')
      ),
      React.createElement('table', { style: { width: '100%', borderCollapse: 'collapse', fontSize: '11px', textAlign: 'left' } },
        React.createElement('thead', null,
          React.createElement('tr', { style: { background: 'rgba(255, 255, 255, 0.04)', borderBottom: '1px solid var(--border)' } },
            headerCols.map((col, cIdx) =>
              React.createElement('th', {
                key: cIdx,
                style: { padding: '6px 10px', color: '#fb923c', fontWeight: 700, whiteSpace: 'nowrap' },
                dangerouslySetInnerHTML: { __html: parseInlineMarkdown(col) }
              })
            )
          )
        ),
        React.createElement('tbody', null,
          dataRows.map((row, rIdx) =>
            React.createElement('tr', {
              key: rIdx,
              style: {
                borderBottom: '1px solid rgba(255,255,255,0.05)',
                background: rIdx % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.02)'
              }
            },
              row.map((cell, cIdx) =>
                React.createElement('td', {
                  key: cIdx,
                  style: { padding: '6px 10px', color: 'var(--text-main)', whiteSpace: 'nowrap' },
                  dangerouslySetInnerHTML: { __html: parseInlineMarkdown(cell) }
                })
              )
            )
          )
        )
      )
    ),
    renderedPortal
  );
}

function parseInlineMarkdown(str) {
  if (!str) return '';
  const mentionStyle = 'display:inline-flex;align-items:center;background:rgba(249,115,22,0.2);color:#fb923c;border:1px solid rgba(249,115,22,0.4);font-weight:700;padding:1px 7px;border-radius:12px;font-size:11px;margin:0 2px;';
  return str
    .replace(/@(Drive&Go AI|DriveAndGo AI|Meta AI)/gi, `<span style="${mentionStyle}">$1</span>`)
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.*?)\*/g,   '<em>$1</em>')
    .replace(/`(.*?)`/g, '<code style="background:rgba(255,255,255,0.1);padding:1px 4px;border-radius:4px;color:#fb923c;font-size:10px">$1</code>');
}

function renderMarkdown(text) {
  if (!text) return null;
  const lines = text.split('\n');
  const elements = [];
  let i = 0;

  while (i < lines.length) {
    const rawLine = lines[i];
    const trimmed = rawLine.trim();

    // Markdown Table block detection (| col1 | col2 |)
    if (trimmed.startsWith('|') && trimmed.endsWith('|')) {
      const tableLines = [];
      while (i < lines.length && lines[i].trim().startsWith('|') && lines[i].trim().endsWith('|')) {
        tableLines.push(lines[i].trim());
        i++;
      }

      if (tableLines.length >= 2) {
        const headerCols = tableLines[0].split('|').slice(1, -1).map(c => c.trim());
        const startDataIdx = (tableLines.length > 1 && tableLines[1].includes('---')) ? 2 : 1;
        const dataRows = tableLines.slice(startDataIdx).map(r => r.split('|').slice(1, -1).map(c => c.trim()));

        elements.push(
          React.createElement(MarkdownTable, {
            key: `tbl-${i}`,
            headerCols: headerCols,
            dataRows: dataRows
          })
        );
        continue;
      }
    }

    const html = parseInlineMarkdown(rawLine);

    if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
      elements.push(React.createElement('hr', { key: i, style: { border: 'none', borderTop: '1px solid var(--border)', margin: '8px 0', opacity: 0.5 } }));
    } else if (rawLine.startsWith('### ')) {
      elements.push(React.createElement('h3', { key: i, style:{color:'#fb923c',fontSize:'11px',fontWeight:700,marginTop:6}, dangerouslySetInnerHTML:{__html:html.slice(4)} }));
    } else if (rawLine.startsWith('## ')) {
      elements.push(React.createElement('h2', { key: i, style:{color:'#e2e8f0',fontSize:'12px',fontWeight:700,marginTop:6}, dangerouslySetInnerHTML:{__html:html.slice(3)} }));
    } else if (rawLine.startsWith('- ') || rawLine.startsWith('• ')) {
      elements.push(React.createElement('li', { key: i, style:{fontSize:'11.5px',color:'var(--text-main)',lineHeight:1.6,marginLeft:8,listStyle:'disc'}, dangerouslySetInnerHTML:{__html:html.slice(2)} }));
    } else if (trimmed !== '') {
      elements.push(React.createElement('p', { key: i, style:{fontSize:'11.5px',color:'var(--text-main)',lineHeight:1.6,marginBottom:6}, dangerouslySetInnerHTML:{__html:html} }));
    }

    i++;
  }

  return elements;
}

function SvgBarChart({ data, isLarge }) {
  if (!data || !data.length) return React.createElement('p', {style:{fontSize:11,color:'#64748b',fontStyle:'italic'}}, 'No data.');
  const W = isLarge ? 780 : 340, H = isLarge ? 360 : 185, PL = isLarge ? 64 : 44, PR = 16, PT = isLarge ? 28 : 16, PB = isLarge ? 68 : 46;
  const cW = W - PL - PR, cH = H - PT - PB;
  const maxVal = Math.max(...data.map(d => Number(d.value) || 0), 1);
  const bW = Math.min(isLarge ? 54 : 28, (cW / data.length) - 8);
  const gap = (cW - bW * data.length) / (data.length + 1);
  const ticks = [0, 0.25, 0.5, 0.75, 1].map(t => ({ v: maxVal * t, y: PT + cH - cH * t }));
  return React.createElement('svg', { viewBox: `0 0 ${W} ${H}`, style: { width: '100%', height: isLarge ? 360 : 185, overflow: 'visible' } },
    ...ticks.map((t, i) => React.createElement('g', { key: i },
      React.createElement('line', { x1: PL, y1: t.y, x2: PL + cW, y2: t.y, stroke: 'rgba(255,255,255,0.08)', strokeWidth: 1 }),
      React.createElement('text', { x: PL - 6, y: t.y + 4, textAnchor: 'end', fontSize: isLarge ? 12 : 8, fill: 'rgba(148,163,184,0.8)' }, t.v >= 1000 ? `₱${(t.v / 1000).toFixed(0)}k` : Math.round(t.v))
    )),
    ...data.map((d, i) => {
      const x = PL + gap + i * (bW + gap);
      const bH = Math.max(4, (Number(d.value) / maxVal) * cH);
      const y = PT + cH - bH;
      const col = CHART_COLORS[i % CHART_COLORS.length];
      const fmtV = Number(d.value) >= 1000 ? `₱${(Number(d.value) / 1000).toFixed(1)}k` : Math.round(Number(d.value));
      return React.createElement('g', { key: i },
        React.createElement('rect', { x, y: y + 4, width: bW, height: bH, rx: 4, fill: col, opacity: 0.15 }),
        React.createElement('rect', { x, y, width: bW, height: bH, rx: 4, fill: col }),
        React.createElement('text', { x: x + bW / 2, y: y - 6, textAnchor: 'middle', fontSize: isLarge ? 12 : 7.5, fill: col, fontWeight: 700 }, fmtV),
        React.createElement('text', { x: x + bW / 2, y: PT + cH + (isLarge ? 20 : 13), textAnchor: 'middle', fontSize: isLarge ? 12 : 7.5, fill: 'rgba(148,163,184,0.9)', transform: `rotate(-28,${x + bW / 2},${PT + cH + (isLarge ? 20 : 13)})` }, String(d.label).slice(0, isLarge ? 16 : 9))
      );
    }),
    React.createElement('line', { x1: PL, y1: PT, x2: PL, y2: PT + cH, stroke: 'rgba(255,255,255,0.15)', strokeWidth: 1 })
  );
}

function SvgDonutChart({ data, isLarge }) {
  if (!data || !data.length) return React.createElement('p',{style:{fontSize:11,color:'#64748b'}}, 'No data.');
  const total = data.reduce((s,d)=>s+(Number(d.value)||0),0);
  if (!total) return React.createElement('p',{style:{fontSize:11,color:'#64748b'}}, 'All values are zero.');
  const CX = isLarge ? 140 : 70, CY = isLarge ? 140 : 70, RO = isLarge ? 116 : 56, RI = isLarge ? 70 : 34;
  let angle = -Math.PI/2;
  const slices = data.map((d,i)=>{
    const pct=Number(d.value)/total, sw=pct*2*Math.PI;
    const x1=CX+RO*Math.cos(angle), y1=CY+RO*Math.sin(angle);
    angle+=sw;
    const x2=CX+RO*Math.cos(angle), y2=CY+RO*Math.sin(angle);
    const xi1=CX+RI*Math.cos(angle), yi1=CY+RI*Math.sin(angle);
    const xi2=CX+RI*Math.cos(angle-sw), yi2=CY+RI*Math.sin(angle-sw);
    const lg=sw>Math.PI?1:0;
    return {path:`M ${x1} ${y1} A ${RO} ${RO} 0 ${lg} 1 ${x2} ${y2} L ${xi1} ${yi1} A ${RI} ${RI} 0 ${lg} 0 ${xi2} ${yi2} Z`,color:CHART_COLORS[i%CHART_COLORS.length],pct,label:d.label};
  });
  const svgSize = isLarge ? 280 : 124;
  const viewBoxSize = isLarge ? 280 : 140;
  return React.createElement('div',{style:{display:'flex',alignItems:'center',gap:isLarge?36:14,flexWrap:'wrap',padding:isLarge?'16px 0':0}},
    React.createElement('svg',{viewBox:`0 0 ${viewBoxSize} ${viewBoxSize}`,style:{width:svgSize,height:svgSize,flexShrink:0}},
      ...slices.map((s,i)=>React.createElement('path',{key:i,d:s.path,fill:s.color,opacity:0.9})),
      React.createElement('text',{x:CX,y:CY-8,textAnchor:'middle',fontSize:isLarge?14:9,fill:'rgba(148,163,184,0.6)'},'Total'),
      React.createElement('text',{x:CX,y:CY+12,textAnchor:'middle',fontSize:isLarge?18:11,fill:'white',fontWeight:700},total>=1000?`₱${(total/1000).toFixed(1)}k`:Math.round(total))
    ),
    React.createElement('div',{style:{display:'flex',flexDirection:'column',gap:isLarge?10:5,flex:1,minWidth:0}},
      ...slices.map((s,i)=>React.createElement('div',{key:i,style:{display:'flex',alignItems:'center',gap:8}},
        React.createElement('span',{style:{width:isLarge?14:9,height:isLarge?14:9,borderRadius:3,background:s.color,flexShrink:0}}),
        React.createElement('span',{style:{fontSize:isLarge?14:10,color:'var(--text-main)',flex:1,overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}},s.label),
        React.createElement('span',{style:{fontSize:isLarge?14:10,fontWeight:700,color:'#e2e8f0',flexShrink:0}},(s.pct*100).toFixed(1)+'%')
      ))
    )
  );
}

function MetricCardGrid({ data, isLarge }) {
  if (!data || !data.length) return null;
  return React.createElement('div',{style:{display:'grid',gridTemplateColumns:isLarge?'1fr 1fr 1fr':'1fr 1fr',gap:isLarge?16:8,marginTop:4,width:'100%',maxWidth:'100%',overflow:'hidden'}},
    ...data.map((d,i)=>{
      const val=Number(d.value)||0;
      const lbl=String(d.label).toLowerCase();
      const isPeso=lbl.includes('revenue')||lbl.includes('amount')||lbl.includes('earning')||lbl.includes('penalty');
      const col=CHART_COLORS[i%CHART_COLORS.length];
      const fmtV=val>=1000000?`${(val/1000000).toFixed(2)}M`:val>=1000?`${(val/1000).toFixed(1)}k`:String(Number.isInteger(val)?val:val.toFixed(1));
      return React.createElement('div',{key:i,style:{background:'var(--bg-panel)',border:'1px solid var(--border)',borderRadius:14,padding:isLarge?'18px 20px':'10px 12px',display:'flex',flexDirection:'column',gap:4,cursor:'default',transition:'all .2s',minWidth:0}},
        React.createElement('p',{style:{fontSize:isLarge?12:9,fontWeight:600,color:'#94a3b8',textTransform:'uppercase',letterSpacing:'0.06em',overflow:'hidden',textOverflow:'ellipsis',whiteSpace:'nowrap'}},d.label),
        React.createElement('p',{style:{fontSize:isLarge?32:22,fontWeight:800,lineHeight:1,color:col}},(isPeso?'₱':'')+fmtV),
        d.trend&&React.createElement('span',{style:{fontSize:isLarge?12:9,fontWeight:600,color:d.trend==='up'?'#34d399':'#f87171',display:'flex',alignItems:'center',gap:2}},d.trend==='up'?'↑ ':'↓ ',d.trendLabel||'')
      );
    })
  );
}

function DataGrid({ data, isLarge }) {
  if (!data || !data.length) return React.createElement('div',{style:{padding:'8px 12px',fontSize:11,color:'#94a3b8',fontStyle:'italic',display:'flex',alignItems:'center',gap:6}},'No records available at this time (0 items).');
  const cols=Object.keys(data[0]).filter(k=>k!=='id');
  const rowsToDisplay = isLarge ? data : data.slice(0, 8);
  return React.createElement('div',{style:{overflowX:'auto',borderRadius:10,border:'1px solid var(--border)',marginTop:4}},
    React.createElement('table',{style:{width:'100%',borderCollapse:'collapse',fontSize:isLarge?13:10}},
      React.createElement('thead',null,React.createElement('tr',{style:{borderBottom:'1px solid var(--border)',background:'var(--bg-panel)'}},
        ...cols.map(c=>React.createElement('th',{key:c,style:{padding:isLarge?'10px 14px':'6px 10px',textAlign:'left',fontWeight:600,color:'#64748b',textTransform:'uppercase',letterSpacing:'0.06em',whiteSpace:'nowrap'}},c.replace(/_/g,' ')))
      )),
      React.createElement('tbody',null,
        ...rowsToDisplay.map((row,i)=>React.createElement('tr',{key:i,style:{borderBottom:'1px solid var(--border)'}},
          ...cols.map(c=>{
            const v=row[c],isN=typeof v==='number';
            const isPeso=isN&&(c.toLowerCase().includes('amount')||c.toLowerCase().includes('revenue')||c.toLowerCase().includes('penalty'));
            const display=isPeso?`₱${Number(v).toLocaleString('en-PH',{minimumFractionDigits:2})}`:isN?v.toLocaleString():String(v??'-');
            return React.createElement('td',{key:c,style:{padding:isLarge?'8px 14px':'5px 10px',color:'var(--text-main)',whiteSpace:'nowrap'}},display);
          })
        )),
        !isLarge && data.length>8 && React.createElement('tr',null,React.createElement('td',{colSpan:cols.length,style:{padding:'4px 10px',fontSize:9,color:'#475569',fontStyle:'italic'}},`+ ${data.length-8} more rows (click chart to expand full view)`))
      )
    )
  );
}

/* ── AI Thinking Bubble ─────────────────────────────────────────────────── */
function AiThinkingBubble() {
  return React.createElement('div',{style:{display:'flex',alignItems:'flex-end',gap:8,maxWidth:'85%'}},
    React.createElement('div',{style:{width:26,height:26,borderRadius:'50%',flexShrink:0,display:'flex',alignItems:'center',justifyContent:'center',fontSize:10,background:'linear-gradient(135deg,#ea580c,#f59e0b,#8b5cf6)',boxShadow:'0 0 14px rgba(234,88,12,0.6)',animation:'aiSparklePulse 2s infinite ease-in-out'}},'✨'),
    React.createElement('div',{style:{background:'var(--bubble-ai)',border:'1px solid var(--border)',borderRadius:'0 16px 16px 16px',padding:'10px 16px',display:'flex',alignItems:'center',gap:7}},
      ...[0,180,360].map(d=>React.createElement('span',{key:d,style:{width:8,height:8,borderRadius:'50%',background:'linear-gradient(135deg,#ea580c,#f59e0b,#8b5cf6)',display:'inline-block',animation:'aiDotBounce 1.4s infinite ease-in-out',animationDelay:`${d}ms`}})),
      React.createElement('span',{style:{fontSize:11,color:'var(--text-muted)',fontStyle:'italic',marginLeft:6,fontWeight:500}},'Drive&Go AI is thinking…')
    )
  );
}

/* ── Delivery Badge ─────────────────────────────────────────────────────── */
function renderDeliveryBadge(status = 'delivered') {
  const st = (status || 'delivered').toLowerCase();
  if (st === 'sending') return React.createElement('div',{style:{display:'flex',alignItems:'center',gap:3,fontSize:8,color:'#94a3b8'}},React.createElement('svg',{width:11,height:11,viewBox:'0 0 24 24',fill:'none',stroke:'#3b82f6',strokeWidth:2,strokeDasharray:'3 3'},React.createElement('circle',{cx:12,cy:12,r:10})),React.createElement('span',null,'Sending'));
  if (st === 'sent') return React.createElement('div',{style:{display:'flex',alignItems:'center',gap:3,fontSize:8,color:'#94a3b8'}},React.createElement('svg',{width:11,height:11,viewBox:'0 0 24 24',fill:'none',stroke:'#3b82f6',strokeWidth:2},React.createElement('circle',{cx:12,cy:12,r:10}),React.createElement('path',{d:'M7 12l3.5 3.5L17 8',strokeLinecap:'round',strokeLinejoin:'round'})),React.createElement('span',null,'Sent'));
  if (st === 'seen' || st === 'read') return React.createElement('div',{style:{display:'flex',alignItems:'center',gap:4,fontSize:8.5,color:'#fb923c',fontWeight:600}},
    React.createElement('div',{style:{width:13,height:13,borderRadius:'50%',background:'linear-gradient(135deg,#ea580c,#f59e0b)',display:'flex',alignItems:'center',justifyContent:'center',color:'white',fontSize:7,fontWeight:800,boxShadow:'0 0 6px rgba(234,88,12,0.4)'}},
      React.createElement('svg', { width: 8, height: 8, viewBox: '0 0 24 24', fill: 'white' },
        React.createElement('path', { d: 'M13 2L4.5 13.5H11V22L19.5 10.5H13V2Z' })
      )
    ),
    React.createElement('svg',{width:12,height:12,viewBox:'0 0 24 24',fill:'none',stroke:'#fb923c',strokeWidth:2.5,strokeLinecap:'round',strokeLinejoin:'round'},
      React.createElement('path',{d:'M18 6L7 17l-5-5'}),
      React.createElement('path',{d:'M22 10l-7.5 7.5-1.5-1.5'})
    ),
    React.createElement('span',null,'Seen')
  );
  return React.createElement('div',{style:{display:'flex',alignItems:'center',gap:3,fontSize:8,color:'#94a3b8'}},React.createElement('svg',{width:11,height:11,viewBox:'0 0 24 24',fill:'#3b82f6'},React.createElement('circle',{cx:12,cy:12,r:11}),React.createElement('path',{d:'M7 12l3.5 3.5L17 8',fill:'none',stroke:'white',strokeWidth:2.5,strokeLinecap:'round',strokeLinejoin:'round'})),React.createElement('span',null,'Delivered'));
}

/* ══════════════════════════════════════════════════════════════════════════
   HOVER ACTION BAR (Smile + 3-dots)
══════════════════════════════════════════════════════════════════════════ */
/* ── Hover Action Bar — ABSOLUTELY POSITIONED so it never causes layout shifts ── */
function HoverActionBar({ isMine, onSmile, onMore, isVisible }) {
  if (!isVisible) return null;
  const btnStyle = {
    width:28, height:28, borderRadius:'50%', border:'none',
    background:'rgba(255,255,255,0.08)', cursor:'pointer',
    display:'flex', alignItems:'center', justifyContent:'center',
    fontSize:14, transition:'background 0.15s, transform 0.12s',
    color:'#e4e6eb', flexShrink:0
  };
  // Absolutely positioned beside the bubble: right side for mine, left for received.
  // top:50%/translateY(-50%) keeps it vertically centred WITHOUT changing row height.
  const barStyle = {
    position:'absolute', top:'50%', transform:'translateY(-50%)',
    display:'flex', gap:4, alignItems:'center',
    animation:'gub-fade-in 0.15s ease forwards',
    zIndex:20, pointerEvents:'auto',
    ...(isMine ? { right:'calc(100% + 6px)' } : { left:'calc(100% + 6px)' })
  };
  return React.createElement('div',{ style: barStyle },
    React.createElement('button',{
      type:'button', className:'gub-action-btn', style:btnStyle, title:'React',
      onClick:(e)=>{ e.preventDefault(); e.stopPropagation(); onSmile(e); }
    },
      React.createElement('svg',{width:15,height:15,viewBox:'0 0 24 24',fill:'none',stroke:'currentColor',strokeWidth:2},
        React.createElement('circle',{cx:12,cy:12,r:10}),
        React.createElement('path',{d:'M8 13s1.5 2 4 2 4-2 4-2',strokeLinecap:'round'}),
        React.createElement('circle',{cx:9,cy:10,r:0.8,fill:'currentColor'}),
        React.createElement('circle',{cx:15,cy:10,r:0.8,fill:'currentColor'})
      )
    ),
    React.createElement('button',{
      type:'button', className:'gub-action-btn', style:btnStyle, title:'More actions',
      onClick:(e)=>{ e.preventDefault(); e.stopPropagation(); onMore(e); }
    },
      React.createElement('svg',{width:15,height:15,viewBox:'0 0 24 24',fill:'currentColor'},
        React.createElement('circle',{cx:5,cy:12,r:1.5}),
        React.createElement('circle',{cx:12,cy:12,r:1.5}),
        React.createElement('circle',{cx:19,cy:12,r:1.5})
      )
    )
  );
}

/* ── Emoji Quick-Reaction Pill ──────────────────────────────────────────── */
function EmojiPicker({ onSelect, onClose, currentEmoji }) {
  return React.createElement('div',{
    className:'gub-emoji-bar',
    style:{
      position:'absolute', bottom:'calc(100% + 8px)',
      background:'#3a3b3c', borderRadius:24, padding:'6px 10px',
      display:'flex', gap:4, boxShadow:'0 8px 28px rgba(0,0,0,0.6)',
      border:'1px solid rgba(255,255,255,0.1)', zIndex:50,
      whiteSpace:'nowrap'
    }
  },
    QUICK_EMOJIS.map(em =>
      React.createElement('button',{
        key:em, type:'button', className:'gub-emoji-btn',
        onClick:(e)=>{ e.preventDefault(); e.stopPropagation(); onSelect(em); onClose(); },
        title: currentEmoji === em ? `Remove ${em}` : em,
        style:{
          background: currentEmoji === em ? 'rgba(234,88,12,0.3)' : 'none',
          border: currentEmoji === em ? '1.5px solid #ea580c' : '1.5px solid transparent',
          borderRadius:'50%', fontSize:20, cursor:'pointer',
          padding:'2px', transition:'transform 0.12s, background 0.15s',
          lineHeight:1, width:34, height:34,
          display:'flex', alignItems:'center', justifyContent:'center'
        }
      }, em)
    )
  );
}

/* ── Edit History Glassmorphism Modal ────────────────────────────────────── */
function EditHistoryModal({ isOpen, onClose, historyJson }) {
  if (!isOpen) return null;
  let history = [];
  try {
    history = typeof historyJson === 'string' ? JSON.parse(historyJson) : (historyJson || []);
  } catch (e) { history = []; }

  return React.createElement('div', {
    className: 'gub-overlay',
    style: {
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.75)',
      backdropFilter: 'blur(8px)', display: 'flex', alignItems: 'center',
      justifyContent: 'center', zIndex: 1000, padding: 16
    },
    onClick: onClose
  },
    React.createElement('div', {
      className: 'gub-modal',
      style: {
        background: 'rgba(18, 19, 36, 0.95)', border: '1px solid rgba(234, 88, 12, 0.4)',
        borderRadius: 20, width: '100%', maxWidth: 440, padding: 20,
        boxShadow: '0 16px 48px rgba(0,0,0,0.8)', display: 'flex', flexDirection: 'column', gap: 14
      },
      onClick: e => e.stopPropagation()
    },
      React.createElement('div', { style: { display: 'flex', alignItems: 'center', justifyContent: 'space-between' } },
        React.createElement('div', { style: { display: 'flex', alignItems: 'center', gap: 8 } },
          React.createElement('span', { style: { fontSize: 18 } }, '✏️'),
          React.createElement('h3', { style: { margin: 0, fontSize: 14, fontWeight: 700, color: '#f1f5f9' } }, 'Edit History')
        ),
        React.createElement('button', {
          onClick: onClose,
          style: { background: 'rgba(255,255,255,0.08)', border: 'none', color: '#94a3b8', borderRadius: '50%', width: 28, height: 28, cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center' }
        }, '✕')
      ),
      React.createElement('div', { style: { display: 'flex', flexDirection: 'column', gap: 10, maxHeight: 300, overflowY: 'auto' } },
        history.length > 0 ? history.map((item, idx) =>
          React.createElement('div', {
            key: idx,
            style: {
              background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.08)',
              borderRadius: 12, padding: 12, display: 'flex', flexDirection: 'column', gap: 4
            }
          },
            React.createElement('div', { style: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' } },
              React.createElement('span', { style: { fontSize: 10, fontWeight: 700, color: '#ea580c' } }, idx === 0 ? 'Original Message' : `Revision #${idx}`),
              React.createElement('span', { style: { fontSize: 9, color: '#64748b' } }, item.edited_at ? new Date(item.edited_at).toLocaleString() : '')
            ),
            React.createElement('p', { style: { margin: 0, fontSize: 12, color: '#e2e8f0', whiteSpace: 'pre-wrap' } }, item.text || item.messageBody || '')
          )
        ) : React.createElement('p', { style: { fontSize: 12, color: '#94a3b8', fontStyle: 'italic', textAlign: 'center' } }, 'No edit history available.')
      )
    )
  );
}

/* ── Custom Audio Player for Voice Notes ───────────────────────────────── */
function VoiceNotePlayer({ audioUrl, metadata }) {
  const [isPlaying, setIsPlaying]       = React.useState(false);
  const [progress, setProgress]         = React.useState(0);
  const [duration, setDuration]         = React.useState(0);
  const [playbackRate, setPlaybackRate] = React.useState(1);
  const waveformRef                     = React.useRef(null);

  // Extract real recorded waveform heights from metadata if present
  const baseHeights = React.useMemo(() => {
    if (metadata) {
      try {
        const obj = typeof metadata === 'string' ? JSON.parse(metadata) : metadata;
        if (obj && Array.isArray(obj.waveform) && obj.waveform.length > 0) {
          return obj.waveform;
        }
      } catch (e) {}
    }
    return [8, 14, 10, 20, 14, 24, 10, 18, 26, 10, 20, 14, 22, 8, 16, 20, 12, 18, 10, 14];
  }, [metadata]);

  const [dynHeights, setDynHeights] = React.useState(baseHeights);
  const audioRef                    = React.useRef(null);
  const analyserRef                 = React.useRef(null);
  const dataArrayRef                = React.useRef(null);

  const getAudio = () => {
    if (!audioRef.current) {
      const serverRoot = (window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233` : 'http://localhost:5233')).replace(/\/api\/?$/i, '').replace(/\/$/, '');
      let fullUrl = audioUrl || '';
      if (!fullUrl.startsWith('http') && !fullUrl.startsWith('data:') && !fullUrl.startsWith('blob:')) {
        fullUrl = serverRoot + (fullUrl.startsWith('/') ? '' : '/') + fullUrl;
      }
      const audio = new Audio(fullUrl);
      audio.crossOrigin = "anonymous";
      audio.playbackRate = playbackRate;
      audioRef.current = audio;

      try {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (AudioContext) {
          const ctx = new AudioContext();
          const source = ctx.createMediaElementSource(audio);
          const analyser = ctx.createAnalyser();
          analyser.fftSize = 64;
          source.connect(analyser);
          analyser.connect(ctx.destination);
          analyserRef.current = analyser;
          dataArrayRef.current = new Uint8Array(analyser.frequencyBinCount);
        }
      } catch (e) { /* ignore cors/context errors */ }

      audio.onloadedmetadata = () => setDuration(audio.duration || 0);
      audio.ontimeupdate = () => {
        if (audio.duration) {
          setProgress((audio.currentTime / audio.duration) * 100);
        }
      };
      audio.onended = () => { setIsPlaying(false); setProgress(0); setDynHeights(baseHeights); };
    }
    return audioRef.current;
  };

  React.useEffect(() => {
    let frameId;
    const updateWaveform = () => {
      if (isPlaying && analyserRef.current && dataArrayRef.current) {
        analyserRef.current.getByteFrequencyData(dataArrayRef.current);
        const newHeights = baseHeights.map((h, i) => {
          const bin = Math.floor((i / baseHeights.length) * (dataArrayRef.current.length * 0.7));
          const val = dataArrayRef.current[bin] / 255.0;
          return Math.max(3, h * 0.4 + (h * 1.5 * val));
        });
        setDynHeights(newHeights);
      } else if (!isPlaying) {
         setDynHeights(baseHeights);
      }
      frameId = requestAnimationFrame(updateWaveform);
    };
    frameId = requestAnimationFrame(updateWaveform);
    return () => cancelAnimationFrame(frameId);
  }, [isPlaying]);

  const togglePlay = () => {
    const audio = getAudio();
    if (!audio) return;
    if (isPlaying) {
      audio.pause();
      setIsPlaying(false);
    } else {
      if (analyserRef.current && analyserRef.current.context && analyserRef.current.context.state === 'suspended') {
         analyserRef.current.context.resume().catch(() => {});
      }
      audio.play()
        .then(() => setIsPlaying(true))
        .catch((err) => {
          console.error("Audio playback error:", err);
          setIsPlaying(false);
        });
    }
  };

  const handleSeek = (e) => {
    if (!waveformRef.current) return;
    const rect = waveformRef.current.getBoundingClientRect();
    const clickX = e.clientX - rect.left;
    const pct = Math.max(0, Math.min(1, clickX / rect.width));
    const audio = getAudio();
    if (audio && audio.duration) {
      audio.currentTime = pct * audio.duration;
      setProgress(pct * 100);
    }
  };

  const toggleSpeed = (e) => {
    e.stopPropagation();
    const rates = [1, 1.5, 2];
    const nextIdx = (rates.indexOf(playbackRate) + 1) % rates.length;
    const nextRate = rates[nextIdx];
    setPlaybackRate(nextRate);
    const audio = getAudio();
    if (audio) audio.playbackRate = nextRate;
  };

  const fmt = (s) => `${Math.floor(s/60)}:${String(Math.floor(s%60)).padStart(2,'0')}`;

  return React.createElement('div', {
    style: {
      display: 'flex', alignItems: 'center', gap: 10,
      background: 'linear-gradient(135deg, #0084ff, #0066cc)',
      border: '1px solid rgba(255,255,255,0.25)',
      borderRadius: 24, padding: '8px 14px', minWidth: 220, boxShadow: '0 4px 16px rgba(0,132,255,0.35)'
    }
  },
    React.createElement('button', {
      type: 'button', onClick: togglePlay,
      style: {
        width: 34, height: 34, borderRadius: '50%',
        background: '#09152e',
        border: 'none', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center',
        cursor: 'pointer', fontSize: 14, boxShadow: '0 2px 8px rgba(0,0,0,0.4)', flexShrink: 0
      }
    }, isPlaying
      ? React.createElement('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'white' }, React.createElement('rect', { x: 6, y: 4, width: 4, height: 16 }), React.createElement('rect', { x: 14, y: 4, width: 4, height: 16 }))
      : React.createElement('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'white' }, React.createElement('polygon', { points: '5 3 19 12 5 21 5 3' }))
    ),
    React.createElement('div', {
      ref: waveformRef,
      onClick: handleSeek,
      style: {
        flex: 1, position: 'relative', display: 'flex', alignItems: 'center', gap: 2.5, height: 28, cursor: 'pointer', padding: '0 2px'
      }
    },
      dynHeights.map((h, i) => {
        const pct = (i / dynHeights.length) * 100;
        const isActive = pct <= progress;
        return React.createElement('div', {
          key: i,
          style: {
            width: 3,
            height: `${h}px`,
            background: isActive ? '#ffffff' : 'rgba(255,255,255,0.45)',
            borderRadius: 2,
            transition: 'height 0.05s ease, background 0.1s ease'
          }
        });
      }),
      // White Scrubber Dot (Knob)
      React.createElement('div', {
        style: {
          position: 'absolute',
          top: '50%',
          left: `${Math.min(97, Math.max(3, progress))}%`,
          transform: 'translate(-50%, -50%)',
          width: 11,
          height: 11,
          borderRadius: '50%',
          background: '#ffffff',
          boxShadow: '0 1px 6px rgba(0,0,0,0.5), 0 0 4px rgba(255,255,255,0.8)',
          pointerEvents: 'none',
          transition: isPlaying ? 'left 0.05s linear' : 'left 0.1s ease'
        }
      })
    ),
    React.createElement('div', {
      style: { display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 2, flexShrink: 0 }
    },
      React.createElement('span', {
        style: { fontSize: 10.5, color: '#ffffff', fontWeight: 700 }
      }, duration > 0 ? fmt(duration) : '0:05'),
      React.createElement('button', {
        type: 'button',
        onClick: toggleSpeed,
        title: 'Playback Speed',
        style: {
          background: 'rgba(0, 0, 0, 0.45)',
          border: 'none',
          borderRadius: 999,
          padding: '1px 6px',
          fontSize: 9.5,
          fontWeight: 800,
          color: '#ffffff',
          cursor: 'pointer',
          lineHeight: '1.2'
        }
      }, `${playbackRate}x`)
    )
  );
}


/* ── Helper for Opening External URLs ─────────────────────────────────── */
const openExternalUrl = (url) => {
  if (!url) return;
  const href = url.startsWith('http') ? url : `https://${url}`;
  if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
    window.chrome.webview.postMessage(JSON.stringify({ type: 'open_external_url', url: href }));
  } else {
    window.open(href, '_blank', 'noopener,noreferrer');
  }
};

/* ── Clickable Hyperlink & Auto-Link & @Mention Parser ────────────────────── */
function renderFormattedTextWithLinks(text, isMine = false) {
  if (!text || typeof text !== 'string') return text;
  const combinedRegex = /(https?:\/\/[^\s]+|www\.[^\s]+|@(Drive&Go AI|DriveAndGo AI|Meta AI))/gi;
  const parts = [];
  let lastIdx = 0;
  let match;

  while ((match = combinedRegex.exec(text)) !== null) {
    if (match.index > lastIdx) {
      parts.push(text.slice(lastIdx, match.index));
    }
    const token = match[0];
    if (token.startsWith('@')) {
      parts.push(
        React.createElement('span', {
          key: match.index,
          style: {
            display: 'inline-flex',
            alignItems: 'center',
            background: isMine ? 'rgba(255, 255, 255, 0.25)' : 'rgba(249, 115, 22, 0.22)',
            color: isMine ? '#ffffff' : '#fb923c',
            border: isMine ? '1px solid rgba(255, 255, 255, 0.4)' : '1px solid rgba(249, 115, 22, 0.45)',
            fontWeight: 700,
            padding: '1px 7px',
            borderRadius: '12px',
            fontSize: '11px',
            letterSpacing: '0.01em',
            margin: '0 2px'
          }
        }, token)
      );
    } else {
      const rawUrl = token;
      const href = rawUrl.startsWith('http') ? rawUrl : `https://${rawUrl}`;
      parts.push(
        React.createElement('a', {
          key: match.index,
          href: href,
          target: '_blank',
          rel: 'noopener noreferrer',
          onClick: (e) => {
            e.preventDefault();
            e.stopPropagation();
            openExternalUrl(href);
          },
          style: {
            color: isMine ? '#ffffff' : '#60a5fa',
            textDecoration: 'underline',
            fontWeight: 600,
            wordBreak: 'break-all',
            cursor: 'pointer'
          }
        }, rawUrl)
      );
    }
    lastIdx = match.index + token.length;
  }
  if (lastIdx < text.length) {
    parts.push(text.slice(lastIdx));
  }
  return parts.length > 0 ? parts : text;
}

/* ── Rich Link Card Renderer ────────────────────────────────────────────── */
/* ── Messenger-Style Rich Link Card Renderer ───────────────────────────── */
function RichLinkCard({ url, previewData }) {
  const [meta, setMeta] = React.useState(previewData || null);
  const [loading, setLoading] = React.useState(!previewData);

  const apiBase = (window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233` : 'http://localhost:5233')).replace(/\/$/, '');

  React.useEffect(() => {
    if (previewData) {
      setMeta(previewData);
      setLoading(false);
      return;
    }
    if (!url) return;
    let isMounted = true;

    let cleanDomain = url;
    try { cleanDomain = new URL(url).hostname.replace('www.', ''); } catch (e) {}

    async function fetchMeta() {
      try {
        const res = await fetch(`${apiBase}/api/media/link-preview?url=${encodeURIComponent(url)}`);
        if (res.ok) {
          const data = await res.json();
          if (isMounted && data && (data.image || data.title)) {
            setMeta(data);
            setLoading(false);
            return;
          }
        }
      } catch (e) {}

      // Fallback 1: Free public microlink OpenGraph API for arbitrary websites
      try {
        const mRes = await fetch(`https://api.microlink.io/?url=${encodeURIComponent(url)}`);
        if (mRes.ok) {
          const mData = await mRes.json();
          if (isMounted && mData?.data) {
            setMeta({
              title: mData.data.title || cleanDomain,
              description: mData.data.description || url,
              image: mData.data.image?.url || mData.data.logo?.url || `https://image.thum.io/get/width/600/crop/800/${url}`,
              domain: cleanDomain
            });
            setLoading(false);
            return;
          }
        }
      } catch (e) {}

      // Fallback 2: Universal Live Screenshot + Favicon
      if (isMounted) {
        setMeta({
          title: cleanDomain,
          description: url,
          image: `https://image.thum.io/get/width/600/crop/800/${url}`,
          domain: cleanDomain
        });
        setLoading(false);
      }
    }

    fetchMeta();
    return () => { isMounted = false; };
  }, [url, previewData]);

  let domainName = meta?.domain || url;
  try { domainName = new URL(url).hostname.replace('www.', ''); } catch (e) {}

  let ytId = null;
  if (url.includes('youtu.be/')) {
    ytId = url.split('youtu.be/')[1]?.split('?')[0]?.split('#')[0];
  } else if (url.includes('youtube.com/watch')) {
    try {
      const uParams = new URLSearchParams(new URL(url).search);
      ytId = uParams.get('v');
    } catch (e) {}
  }

  const cardTitle = meta?.title || (ytId ? 'YouTube Video' : domainName);
  const cardDesc  = meta?.description || meta?.siteName || (ytId ? `https://youtu.be/${ytId}` : url);
  const imageUrl  = meta?.image || (ytId ? `https://img.youtube.com/vi/${ytId}/hqdefault.jpg` : `https://image.thum.io/get/width/600/crop/800/${url}`);

  return React.createElement('a', {
    href: url, target: '_blank', rel: 'noopener noreferrer',
    onClick: (e) => {
      e.preventDefault();
      e.stopPropagation();
      openExternalUrl(url);
    },
    style: { textDecoration: 'none', marginTop: 8, display: 'block', maxWidth: 380, width: '100%', cursor: 'pointer' }
  },
    React.createElement('div', {
      style: {
        background: 'rgba(30, 32, 46, 0.95)', border: '1px solid rgba(255, 255, 255, 0.12)',
        borderRadius: 16, overflow: 'hidden', display: 'flex', flexDirection: 'column',
        boxShadow: '0 8px 24px rgba(0,0,0,0.4)', transition: 'transform 0.2s, box-shadow 0.2s',
        cursor: 'pointer'
      }
    },
      imageUrl ? React.createElement('div', {
        style: { width: '100%', height: 190, overflow: 'hidden', background: '#0f111a', position: 'relative' }
      },
        React.createElement('img', {
          src: imageUrl,
          alt: cardTitle,
          onError: (e) => {
             // Fallback to favicon icon banner if image load fails
             e.target.style.display = 'none';
          },
          style: { width: '100%', height: '100%', objectFit: 'cover', display: 'block' }
        })
      ) : null,
      React.createElement('div', {
        style: { padding: '12px 14px', display: 'flex', flexDirection: 'column', gap: 4, background: '#1c1e2d' }
      },
        React.createElement('span', {
          style: { fontSize: 13, fontWeight: 700, color: '#f8fafc', lineHeight: 1.35, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }
        }, cardTitle),
        cardDesc && React.createElement('span', {
          style: { fontSize: 11, color: '#94a3b8', lineHeight: 1.4, display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }
        }, cardDesc),
        React.createElement('div', {
          style: { display: 'flex', alignItems: 'center', gap: 6, marginTop: 4 }
        },
          React.createElement('img', {
            src: `https://www.google.com/s2/favicons?domain=${domainName}&sz=32`,
            alt: domainName,
            style: { width: 14, height: 14, borderRadius: 3 }
          }),
          React.createElement('span', { style: { fontSize: 10.5, color: '#94a3b8', fontWeight: 600, textTransform: 'lowercase' } }, domainName)
        )
      )
    )
  );
}

/* ── 3-Dots Context Menu ────────────────────────────────────────────────── */
function BubbleContextMenu({ isMine, onForward, onRemove, onReact, onUnsend, onEdit, onClose }) {
  const items = [
    { label:'React', icon: React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, React.createElement('circle', { cx: 12, cy: 12, r: 10 }), React.createElement('path', { d: 'M8 14s1.5 2 4 2 4-2 4-2' }), React.createElement('line', { x1: 9, y1: 9, x2: 9.01, y2: 9 }), React.createElement('line', { x1: 15, y1: 9, x2: 15.01, y2: 9 })), action: onReact },
    { label:'Forward', icon: React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, React.createElement('polyline', { points: '15 17 20 12 15 7' }), React.createElement('path', { d: 'M4 18v-2a4 4 0 0 1 4-4h12' })), action: onForward },
    ...(isMine && onEdit ? [{ label:'Edit', icon: React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, React.createElement('path', { d: 'M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7' }), React.createElement('path', { d: 'M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z' })), action: onEdit }] : []),
    { label:'Remove for you', icon: React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, React.createElement('polyline', { points: '3 6 5 6 21 6' }), React.createElement('path', { d: 'M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2' })), action: onRemove },
    ...(isMine ? [{ label:'Unsend', icon: React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' }, React.createElement('polyline', { points: '9 14 4 9 9 4' }), React.createElement('path', { d: 'M20 20v-7a4 4 0 0 0-4-4H4' })), action: onUnsend }] : [])
  ];
  return React.createElement('div',{
    style:{position:'fixed',inset:0,zIndex:200},
    onClick:onClose
  },
    React.createElement('div',{
      style:{
        position:'absolute', top:'100%',
        ...(isMine ? {right:0} : {left:0}),
        background:'#3a3b3c', borderRadius:12, minWidth:180,
        boxShadow:'0 8px 32px rgba(0,0,0,0.6)',
        border:'1px solid rgba(255,255,255,0.1)',
        overflow:'hidden', zIndex:300,
        animation:'gub-scale-up 0.15s cubic-bezier(0.34,1.56,0.64,1) forwards'
      },
      onClick:e=>e.stopPropagation()
    },
      items.map((item,i) =>
        React.createElement('button',{
          key:i, className:'gub-menu-item',
          onClick:()=>{ item.action(); onClose(); },
          style:{
            width:'100%', display:'flex', alignItems:'center', gap:10,
            padding:'11px 16px', background:'transparent', border:'none',
            color:'#e4e6eb', fontSize:13, fontWeight:500, cursor:'pointer',
            textAlign:'left', transition:'background 0.12s',
            ...(item.label==='Remove for you'||item.label==='Unsend'?{color:'#ff6b6b'}:{})
          }
        },
          React.createElement('span',{style:{fontSize:16,width:20,textAlign:'center'}}, item.icon),
          item.label
        )
      )
    )
  );
}

/* ══════════════════════════════════════════════════════════════════════════
   MAIN GenUiBubble COMPONENT
══════════════════════════════════════════════════════════════════════════ */
function GenUiBubble({ message, groupPosition = 'single', showSenderHeader = true, isLastSeenMessage = false, seenAvatarUrl = null }) {
  const {
    id, sender, body, isMine: isMineProp, time, status, deliveryStatus,
    ui_component, data, providerUsed, is_unsent, is_edited, reactions,
    // ── Pillar 02: Reply/Quote fields ──
    replyToId, replyToSender, replyToBody, replyToMediaType,
    // ── Sender info ──
    senderName, senderId
  } = message;

  const isMine = !!(
    isMineProp ||
    message.isMine ||
    message.is_mine ||
    senderId === 'admin' ||
    senderId === '1' ||
    senderId === 1 ||
    (typeof sender === 'string' && sender.toLowerCase() === 'admin') ||
    (typeof senderName === 'string' && senderName.toLowerCase() === 'admin')
  );

  const { useState, useRef, useEffect } = React;

  // ── Detect if incoming message is AI or human ──────────────────────────
  const isAiMessage = !isMine && (
    senderId === 'ai_copilot' ||
    senderId === '@Drive&Go AI' ||
    sender === 'Drive&Go AI' ||
    senderName === 'Drive&Go AI' ||
    (typeof sender === 'string' && sender.toLowerCase().includes('ai')) ||
    (typeof senderName === 'string' && senderName.toLowerCase().includes('ai'))
  );
  const displaySender = senderName || sender || (isMine ? 'Admin' : 'Contact');
  const senderInitial = (displaySender || 'U')[0].toUpperCase();

  let displayText = body || '';
  let dynamicUiComponent = ui_component;
  let dynamicData = [];
  try { dynamicData = typeof data === 'string' ? JSON.parse(data) : (data || []); } catch { dynamicData = []; }

  if (displayText && typeof displayText === 'string') {
    displayText = displayText
      .replace(/-*\s*UI_COMPONENT\s*-*/gi, '')
      .replace(/\bUI_COMPONENT\b/gi, '')
      .replace(/^(?:[\s\r\n]*---+\s*)+/g, '')
      .replace(/(?:[\s\r\n]*---+\s*)+$/g, '')
      .trim();
  }

  // ── FIX 3: HIDE RAW JSON FROM UI ─────────────────────────────────────────
  if (displayText && typeof displayText === 'string' && displayText.trim().startsWith('{')) {
    try {
      const parsedJson = JSON.parse(displayText.trim());
      if (parsedJson && typeof parsedJson === 'object') {
        if (parsedJson.text !== undefined) {
          displayText = parsedJson.text;
        } else if (parsedJson.message !== undefined) {
          displayText = parsedJson.message;
        }
        if (!dynamicUiComponent && parsedJson.ui_component) {
          dynamicUiComponent = parsedJson.ui_component;
        }
        if ((!dynamicData || dynamicData.length === 0) && parsedJson.data) {
          dynamicData = Array.isArray(parsedJson.data) ? parsedJson.data : [parsedJson.data];
        }
      }
    } catch (e) {}
  }

  const hasChart = dynamicUiComponent && dynamicUiComponent !== 'Text Only';

  // ── Local UI State ───────────────────────────────────────────────────────
  const [isHovered,           setIsHovered]           = useState(false);
  const [showEmojiPicker,     setShowEmojiPicker]     = useState(false);
  const [showContextMenu,     setShowContextMenu]     = useState(false);
  const [showRemoveModal,     setShowRemoveModal]     = useState(false);
  const [showForwardModal,    setShowForwardModal]    = useState(false);
  const [showReactionModal,   setShowReactionModal]   = useState(false);
  const [showFullscreenChart, setShowFullscreenChart] = useState(false);
  const [fullscreenMediaUrl,  setFullscreenMediaUrl]  = useState('');
  const [fullscreenMediaType, setFullscreenMediaType] = useState('');
  const [showEditHistoryModal,setShowEditHistoryModal]= useState(false);

  const [showInlineEdits,     setShowInlineEdits]     = useState(false);
  const [isFetchingHistory,   setIsFetchingHistory]   = useState(false);
  const [userReaction,        setUserReaction]        = useState(null);
  const [historyItems,        setHistoryItems]        = useState(() => {
    let hist = message.editHistory || message.edit_history || [];
    if (typeof hist === 'string') {
      try { hist = JSON.parse(hist); } catch { hist = []; }
    }
    return Array.isArray(hist) ? hist : [];
  });
  const mType = message.mediaType || message.media_type;
  const mUrl  = message.mediaUrl  || message.media_url;
  const urlMatch = displayText ? displayText.match(/(https?:\/\/[^\s]+|www\.[^\s]+)/gi) : null;
  const detectedUrl = urlMatch ? (urlMatch[0].startsWith('http') ? urlMatch[0] : `https://${urlMatch[0]}`) : null;

  const isVoiceNote = (mType === 'audio') ||
    (displayText && (
      displayText.startsWith('[Voice Note') ||
      displayText.includes('🎙️ Voice Note') ||
      displayText.includes('Voice Note')
    )) ||
    (mUrl && typeof mUrl === 'string' && (mUrl.endsWith('.webm') || mUrl.endsWith('.mp3') || mUrl.endsWith('.m4a') || mUrl.endsWith('.wav') || mUrl.endsWith('.ogg')));

  const apiBase = (window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233` : 'http://localhost:5233')).replace(/\/api\/?$/i, '').replace(/\/$/, '');

  const bubbleRef  = useRef(null);
  const mountedRef = useRef(false);

  useEffect(() => {
    if (!mountedRef.current && bubbleRef.current) {
      mountedRef.current = true;
      const el = bubbleRef.current;
      el.classList.add('gub-bubble-animate');
      const cleanup = () => el.classList.remove('gub-bubble-animate');
      el.addEventListener('animationend', cleanup, { once: true });
    }
  }, []);

  const postMessage = (type, extra = {}) => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type, messageId: id, ...extra }));
    }
  };

  useEffect(() => {
    if (reactions) {
      try {
        const parsed = typeof reactions === 'string' ? JSON.parse(reactions) : reactions;
        if (parsed && typeof parsed === 'object') {
          const own = parsed.admin || parsed['1'];
          const ownEmoji = typeof own === 'string' ? own : (own?.emoji || own?.reaction);
          if (ownEmoji) setUserReaction(ownEmoji);
        }
      } catch (e) {}
    }
  }, [reactions]);

  const rxParsed = React.useMemo(() => {
    let res = {};
    if (reactions && reactions !== '{}') {
      try { res = typeof reactions === 'string' ? JSON.parse(reactions) : reactions; } catch { res = {}; }
    }
    if (userReaction) {
      res = { ...res, admin: userReaction };
    }
    return res || {};
  }, [reactions, userReaction]);

  const activeReactionEmojis = React.useMemo(() => {
    const list = Object.values(rxParsed).map(v => typeof v === 'string' ? v : (v?.emoji || v?.reaction || '')).filter(Boolean);
    return Array.from(new Set(list));
  }, [rxParsed]);

  const wrapperStyle = {
    position: 'relative', display: 'flex', flexDirection: 'column',
    alignItems: isMine ? 'flex-end' : 'flex-start',
    maxWidth: '100%',
    width: '100%',
    alignSelf: isMine ? 'flex-end' : 'flex-start',
    wordBreak: 'break-word', overflowWrap: 'anywhere', opacity: 1
  };



  const currentStatus = status || deliveryStatus || 'delivered';

  const handleReplyClick = (e) => {
    e.stopPropagation();
    window.dispatchEvent(new CustomEvent('chat:replyTo', { detail: {
      id: message.id || message.messageId,
      sender: displaySender,
      body: body || '',
      mediaType: message.mediaType || message.media_type || null
    }}));
  };

  const metaObj = React.useMemo(() => {
    let meta = message.mediaMetadata || message.media_metadata;
    if (meta && typeof meta === 'string') {
      try { return JSON.parse(meta); } catch { return {}; }
    }
    return meta && typeof meta === 'object' ? meta : {};
  }, [message]);

  const isAiSender = displaySender === 'Drive&Go AI' || displaySender === '@Drive&Go AI' || (senderId && String(senderId).includes('AI'));

  const effectiveReplyBody = React.useMemo(() => {
    const directBody = message.replyToBody || message.reply_to_body || message.replyBody || replyToBody || metaObj?.replyToBody || message.prompt || message.userPrompt || null;
    if (directBody && directBody !== '...') return directBody;
    return null;
  }, [message, metaObj, replyToBody]);

  const effectiveReplySender = message.replyToSender || message.reply_to_sender || message.replySender || replyToSender || metaObj?.replyToSender || (isAiSender && effectiveReplyBody ? 'you' : null);
  const effectiveReplyMediaType = message.replyToMediaType || message.reply_to_media_type || message.replyMediaType || replyToMediaType || metaObj?.replyToMediaType || null;

  const isForwarded = React.useMemo(() => {
    if (message.isForwarded || message.is_forwarded || message.isForward || message.forwarded) return true;
    let meta = message.mediaMetadata || message.media_metadata;
    if (meta && typeof meta === 'string') {
      try { meta = JSON.parse(meta); } catch { meta = {}; }
    }
    return !!(meta && (meta.isForwarded || meta.is_forwarded || meta.isForward || meta.forwarded));
  }, [message]);

  const ForwardedLabel = () => React.createElement('div', {
    style: {
      display: 'flex',
      alignItems: 'center',
      gap: 4,
      fontSize: 10.5,
      fontWeight: 500,
      color: '#94a3b8',
      fontStyle: 'italic',
      marginBottom: 3,
      paddingLeft: isMine ? 0 : 2,
      paddingRight: isMine ? 2 : 0
    }
  },
    React.createElement('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
      React.createElement('polyline', { points: '15 17 20 12 15 7' }),
      React.createElement('path', { d: 'M4 18v-2a4 4 0 0 1 4-4h12' })
    ),
    'Forwarded'
  );

  const QuotedReplyBubble = () => {
    if (!effectiveReplySender && !effectiveReplyBody) return null;
    const quotedText = effectiveReplyMediaType === 'audio' ? 'Voice Note'
      : effectiveReplyMediaType === 'image' ? 'Photo'
      : effectiveReplyMediaType === 'video' ? 'Video'
      : effectiveReplyBody || '...';

    const isAiSender = displaySender === 'Drive&Go AI' || displaySender === '@Drive&Go AI' || (senderId && String(senderId).includes('AI'));

    const replyHeaderLabel = isMine
      ? `You replied to ${effectiveReplySender === displaySender || effectiveReplySender === 'Admin' ? 'yourself' : (effectiveReplySender || 'a message')}`
      : isAiSender
      ? `${displaySender} replied to ${effectiveReplySender === 'you' || effectiveReplySender === 'admin' || effectiveReplySender === 'Admin' ? 'you' : (effectiveReplySender || 'you')}`
      : `${displaySender} replied to ${effectiveReplySender || 'a message'}`;

    return React.createElement('div', {
      style: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: isMine ? 'flex-end' : 'flex-start',
        marginBottom: 3,
        maxWidth: '100%'
      }
    },
      React.createElement('div', {
        style: {
          fontSize: 10,
          color: '#94a3b8',
          fontWeight: 600,
          display: 'flex',
          alignItems: 'center',
          gap: 4,
          marginBottom: 3
        }
      },
        React.createElement(SvgReply),
        replyHeaderLabel
      ),
      React.createElement('div', {
        style: {
          background: 'rgba(255, 255, 255, 0.08)',
          border: '1px solid rgba(255, 255, 255, 0.1)',
          borderRadius: 14,
          padding: '6px 12px',
          fontSize: 11,
          color: '#cbd5e1',
          maxWidth: 240,
          whiteSpace: 'nowrap',
          overflow: 'hidden',
          textOverflow: 'ellipsis'
        }
      }, renderFormattedTextWithLinks(quotedText, false))
    );
  };
  const toggleInlineEdits = async (e) => {
    if (e) { e.preventDefault(); e.stopPropagation(); }
    if (!showInlineEdits && historyItems.length === 0) {
      const rawHist = message.editHistory || message.edit_history;
      if (rawHist) {
        try {
          const parsed = typeof rawHist === 'string' ? JSON.parse(rawHist) : rawHist;
          if (Array.isArray(parsed) && parsed.length > 0) {
            setHistoryItems(parsed);
            setShowInlineEdits(true);
            return;
          }
        } catch (e) {}
      }
      if (id) {
        setIsFetchingHistory(true);
        try {
          const res = await fetch(`${apiBase}/api/messages/${id}/history`);
          if (res.ok) {
            const list = await res.json();
            setHistoryItems(list || []);
          }
        } catch (err) {}
        finally { setIsFetchingHistory(false); }
      }
    }
    setShowInlineEdits(prev => !prev);
  };

  const handleEmojiSelect = (emoji, e) => {
    if (e) e.stopPropagation();
    setShowEmojiPicker(false);
    setUserReaction(prev => prev === emoji ? null : emoji);
    window.dispatchEvent(new CustomEvent('chat:reactToMessage', {
      detail: { msg: message, emoji }
    }));
  };

  const EditHistorySection = ({ side = 'mine' }) => {
    const isEditedMsg = !!(is_edited || message.isEdited || message.is_edited || (message.editHistory && message.editHistory !== '[]') || (message.edit_history && message.edit_history !== '[]'));
    if (!isEditedMsg) return null;

    return React.createElement('div', {
      style: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: side === 'mine' ? 'flex-end' : 'flex-start',
        marginBottom: 4,
        maxWidth: '100%',
        width: '100%'
      }
    },
      React.createElement('button', {
        type: 'button',
        onClick: toggleInlineEdits,
        style: {
          background: 'none',
          border: 'none',
          color: '#3b82f6',
          fontSize: 11,
          fontWeight: 600,
          cursor: 'pointer',
          padding: '2px 4px',
          marginBottom: 2,
          transition: 'opacity 0.15s'
        }
      }, showInlineEdits ? 'Hide edits' : 'Edited'),

      showInlineEdits && React.createElement('div', {
        style: {
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
          width: '100%',
          alignItems: side === 'mine' ? 'flex-end' : 'flex-start',
          marginBottom: 4
        }
      },
        historyItems.length > 0 ? historyItems.map((item, idx) =>
          React.createElement('div', {
            key: idx,
            style: {
              background: 'rgba(255, 255, 255, 0.05)',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              color: '#cbd5e1',
              borderRadius: 14,
              padding: '6px 12px',
              fontSize: 11.5,
              width: 'fit-content',
              maxWidth: '85%',
              wordBreak: 'break-word',
              whiteSpace: 'pre-wrap',
              boxSizing: 'border-box'
            }
          }, typeof item === 'string' ? item : (item.text || item.body || item.messageBody || item.old_text || ''))
        ) : (!isFetchingHistory && React.createElement('div', {
          style: {
            background: 'rgba(255, 255, 255, 0.05)',
            border: '1px solid rgba(255, 255, 255, 0.15)',
            color: '#94a3b8',
            borderRadius: 14,
            padding: '6px 12px',
            fontSize: 11,
            fontStyle: 'italic',
            width: 'fit-content'
          }
        }, 'No previous edits found')),
        isFetchingHistory && React.createElement('span', { style: { fontSize: 10, color: '#94a3b8', fontStyle: 'italic' } }, 'Loading edit history...')
      )
    );
  };

  // ── SVG Icon helpers ─────────────────────────────────────────────────────
  const SvgReply = () => React.createElement('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
    React.createElement('polyline', { points: '9 17 4 12 9 7' }),
    React.createElement('path', { d: 'M20 18v-2a4 4 0 00-4-4H4' })
  );
  const SvgCopy = () => React.createElement('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
    React.createElement('rect', { x: '9', y: '9', width: '13', height: '13', rx: '2', ry: '2' }),
    React.createElement('path', { d: 'M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1' })
  );
  const SvgForward = () => React.createElement('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
    React.createElement('polyline', { points: '15 17 20 12 15 7' }),
    React.createElement('path', { d: 'M4 18v-2a4 4 0 014-4h12' })
  );
  const SvgDelete = () => React.createElement('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
    React.createElement('polyline', { points: '3 6 5 6 21 6' }),
    React.createElement('path', { d: 'M19 6l-1 14a2 2 0 01-2 2H8a2 2 0 01-2-2L5 6' }),
    React.createElement('path', { d: 'M10 11v6' }),
    React.createElement('path', { d: 'M14 11v6' }),
    React.createElement('path', { d: 'M9 6V4a1 1 0 011-1h4a1 1 0 011 1v2' })
  );
  const SvgMore = () => React.createElement('svg', { width: 16, height: 16, viewBox: '0 0 24 24', fill: 'currentColor' },
    React.createElement('circle', { cx: '5', cy: '12', r: '2' }),
    React.createElement('circle', { cx: '12', cy: '12', r: '2' }),
    React.createElement('circle', { cx: '19', cy: '12', r: '2' })
  );
  const SvgPlay = () => React.createElement('svg', { width: 18, height: 18, viewBox: '0 0 24 24', fill: 'white' },
    React.createElement('path', { d: 'M8 5v14l11-7z' })
  );
  const SvgExpand = () => React.createElement('svg', { width: 10, height: 10, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2.5 },
    React.createElement('path', { d: 'M8 3H5a2 2 0 00-2 2v3m18 0V5a2 2 0 00-2-2h-3M3 16v3a2 2 0 002 2h3m8 0h3a2 2 0 002-2v-3' })
  );



  const isUnsent = !!(
    is_unsent ||
    message.isUnsent ||
    message.is_unsent ||
    message.status === 'unsent' ||
    (typeof body === 'string' && body.trim() === '' && (message.isUnsent || message.is_unsent))
  );

  if (isUnsent) {
    return React.createElement('div', { style: wrapperStyle },
      React.createElement('div', {
        style: {
          padding: '9px 14px',
          fontSize: 12,
          color: '#94a3b8',
          fontStyle: 'italic',
          border: '1px dashed rgba(255,255,255,0.2)',
          borderRadius: isMine ? '18px 18px 4px 18px' : '18px 18px 18px 4px',
          background: 'rgba(255,255,255,0.03)',
          display: 'inline-flex',
          alignItems: 'center',
          gap: 6
        }
      },
        React.createElement('svg', { width: 13, height: 13, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
          React.createElement('circle', { cx: 12, cy: 12, r: 10 }),
          React.createElement('line', { x1: 4.93, y1: 4.93, x2: 19.07, y2: 19.07 })
        ),
        isMine ? 'You unsent a message' : 'This message was unsent'
      )
    );
  }

  // ── Action Bars (Messenger-style: 3 inline buttons [ ⋮ ] [ ↩️ ] [ 😊 ]) ──
  const ReactStrip = ({ side }) => React.createElement('div', {
    className: `gub-react-strip ${side}`,
    onClick: e => e.stopPropagation()
  },
    ['❤️','😆','😮','😢','😡','👍'].map((emoji, i) =>
      React.createElement('button', {
        key: i,
        className: 'gub-react-emoji-btn',
        title: emoji,
        onClick: (e) => handleEmojiSelect(emoji, e)
      }, emoji)
    ),
    React.createElement('button', {
      className: 'gub-react-emoji-btn',
      title: 'More reactions',
      style: {
        width: 26, height: 26, borderRadius: '50%',
        background: 'rgba(255,255,255,0.12)',
        color: '#e4e6eb', fontSize: 14, fontWeight: 700
      },
      onClick: (e) => { e.stopPropagation(); setShowEmojiPicker(prev => !prev); }
    }, React.createElement('svg', { width: 12, height: 12, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2.5, strokeLinecap: 'round' },
      React.createElement('line', { x1: 12, y1: 5, x2: 12, y2: 19 }),
      React.createElement('line', { x1: 5, y1: 12, x2: 19, y2: 12 })
    ))
  );

  const ActionBar = ({ side }) => React.createElement('div', {
    className: `gub-action-bar ${side} ${(showEmojiPicker || showContextMenu) ? 'is-open' : ''}`,
    onClick: e => e.stopPropagation()
  },
    React.createElement('button', { className: 'gub-icon-btn', title: 'More options', onClick: (e) => { e.stopPropagation(); setShowContextMenu(prev => !prev); } },
      React.createElement(SvgMore)
    ),
    React.createElement('button', { className: 'gub-icon-btn', title: 'Reply to this message', onClick: handleReplyClick },
      React.createElement(SvgReply)
    ),
    React.createElement('button', { className: 'gub-icon-btn', title: 'React', onClick: (e) => { e.stopPropagation(); setShowEmojiPicker(prev => !prev); } },
      React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round', strokeLinejoin: 'round' },
        React.createElement('circle', { cx: 12, cy: 12, r: 10 }),
        React.createElement('path', { d: 'M8 14s1.5 2 4 2 4-2 4-2' }),
        React.createElement('line', { x1: 9, y1: 9, x2: 9.01, y2: 9 }),
        React.createElement('line', { x1: 15, y1: 9, x2: 15.01, y2: 9 })
      )
    )
  );

  const openGlobalLightbox = (url, type, title) => {
    window.dispatchEvent(new CustomEvent('chat:openLightbox', {
      detail: {
        url,
        type,
        title: title || (type === 'video' ? 'Shared Video Preview' : 'Shared Image Preview')
      }
    }));
  };

  const groupRow = React.createElement('div',{
    ref: bubbleRef,
    className: 'gub-bubble-row',
    style:{
      display:'flex', alignItems:'center',
      width:'100%',
      justifyContent: isMine ? 'flex-end' : 'flex-start',
      position:'relative',
      minHeight: 36,
      paddingBottom: 2
    }
  },

    React.createElement('div',{
      style:{
        position:'relative',
        display:'inline-flex',
        alignItems:'center',
        justifyContent: isMine ? 'flex-end' : 'flex-start',
        maxWidth: isMine ? '70%' : '75%',
        marginLeft: isMine ? 'auto' : undefined
      }
    },

      isMine && React.createElement('div',{style:{position:'relative', display:'inline-flex', flexDirection:'column', alignItems:'flex-end', gap:4, maxWidth:'100%'}},
        showEmojiPicker && React.createElement(ReactStrip, { side: 'mine' }),
        React.createElement(ActionBar, { side: 'mine' }),
        isForwarded && React.createElement(ForwardedLabel),
        (effectiveReplySender || effectiveReplyBody) && React.createElement(QuotedReplyBubble),
        React.createElement(EditHistorySection, { side: 'mine' }),
        mType === 'image' && mUrl && React.createElement('img', {
          src: mUrl.startsWith('http') ? mUrl : apiBase + mUrl,
          className: 'gub-img-bubble',
          onClick: () => openGlobalLightbox(mUrl.startsWith('http') ? mUrl : apiBase + mUrl, 'image', message.fileName)
        }),
        mType === 'video' && mUrl && React.createElement('div', {
          style: { position: 'relative', cursor: 'pointer', maxWidth: 260, width: '100%', minHeight: 140, aspectRatio: '16/9', borderRadius: 18, overflow: 'hidden', background: '#090a0f' },
          onClick: () => openGlobalLightbox(mUrl.startsWith('http') ? mUrl : apiBase + mUrl, 'video', message.fileName)
        },
          React.createElement('video', { src: mUrl.startsWith('http') ? mUrl : apiBase + mUrl, style: { width: '100%', height: '100%', objectFit: 'cover', display: 'block' } }),
          React.createElement('div', { style: { position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', width: 48, height: 48, borderRadius: '50%', background: 'rgba(0,0,0,0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center' } },
            React.createElement(SvgPlay)
          )
        ),
        isVoiceNote && React.createElement(VoiceNotePlayer, { audioUrl: mUrl || detectedUrl, metadata: message.mediaMetadata || message.media_metadata }),
        (!isVoiceNote && displayText && !['audio', 'image', 'video'].includes(mType) && displayText !== '[Photo]' && displayText !== '[Video]') && React.createElement('div', {
          className: 'gub-out-bubble',
          style: {
            borderRadius: groupPosition === 'first' ? '18px 18px 4px 18px' : groupPosition === 'middle' ? '18px 4px 4px 18px' : groupPosition === 'last' ? '18px 4px 18px 18px' : '18px 18px 0 18px'
          }
        }, renderFormattedTextWithLinks(displayText, true)),
        (!isVoiceNote && detectedUrl) && React.createElement(RichLinkCard, { url: detectedUrl })
      ),

      !isMine && React.createElement('div',{style:{display:'flex',alignItems:'flex-start',gap:8,maxWidth:'100%'}},
        isAiMessage
          ? React.createElement('div',{style:{ width:28, height:28, borderRadius:'50%', flexShrink:0, display:'flex', alignItems:'center', justifyContent:'center', background:'linear-gradient(135deg,#ea580c,#f59e0b)', boxShadow:'0 0 10px rgba(234,88,12,0.5)', alignSelf:'flex-start', marginTop:3, visibility: (groupPosition === 'last' || groupPosition === 'single') ? 'visible' : 'hidden' }},
              React.createElement('svg', { width: 14, height: 14, viewBox: '0 0 24 24', fill: 'white' },
                React.createElement('path', { d: 'M13 2L4.5 13.5H11V22L19.5 10.5H13V2Z' })
              )
            )
          : React.createElement('div',{style:{ width:28, height:28, borderRadius:'50%', flexShrink:0, display:'flex', alignItems:'center', justifyContent:'center', fontSize:11, fontWeight:700, background:'linear-gradient(135deg,#6d28d9,#a855f7)', color:'white', boxShadow:'0 0 8px rgba(139,92,246,0.4)', alignSelf:'flex-start', marginTop:3, visibility: (groupPosition === 'last' || groupPosition === 'single') ? 'visible' : 'hidden' }}, senderInitial),

        React.createElement('div',{style:{display:'flex',flexDirection:'column',gap:2,minWidth:0,maxWidth:'100%'}},
          showSenderHeader && React.createElement('div',{style:{display:'flex',alignItems:'center',gap:5}},
            isAiMessage
              ? React.createElement('span',{style:{fontSize:9.5,color:'#fb923c',fontWeight:700,letterSpacing:'0.02em'}}, 'Drive&Go AI')
              : React.createElement('span',{style:{fontSize:9.5,color:'#a78bfa',fontWeight:700}}, displaySender)
          ),
          React.createElement('div',{style:{position:'relative', display:'inline-flex', flexDirection:'column', maxWidth:'100%', width:'fit-content'}},
            showEmojiPicker && React.createElement(ReactStrip, { side: 'theirs' }),
            React.createElement(ActionBar, { side: 'theirs' }),
            isForwarded && React.createElement(ForwardedLabel),
            (effectiveReplySender || effectiveReplyBody) && React.createElement(QuotedReplyBubble),
            React.createElement(EditHistorySection, { side: 'theirs' }),
            isVoiceNote && React.createElement(VoiceNotePlayer, { audioUrl: message.mediaUrl || mUrl || detectedUrl, metadata: message.mediaMetadata || message.media_metadata }),
            (!isVoiceNote && (() => {
              const txtToRender = (displayText && displayText.trim().length > 0) ? displayText : '';
              if (!txtToRender) return null;
              const customInRadius = groupPosition === 'first' ? '18px 18px 18px 4px' : groupPosition === 'middle' ? '4px 18px 18px 4px' : groupPosition === 'last' ? '4px 18px 18px 18px' : '0 18px 18px 18px';
              if (isAiMessage) return React.createElement('div', { className: 'gub-ai-bubble', style: { borderRadius: customInRadius } }, ...renderMarkdown(txtToRender));
              return React.createElement('div', { className: 'gub-human-bubble', style: { borderRadius: customInRadius } }, renderFormattedTextWithLinks(txtToRender, false));
            })()),
            (!isVoiceNote && detectedUrl) && React.createElement(RichLinkCard, { url: detectedUrl }),
            message.mediaType === 'image' && message.mediaUrl && React.createElement('img', {
              src: (message.mediaUrl || '').startsWith('http') ? message.mediaUrl : apiBase + message.mediaUrl,
              className: 'gub-img-bubble',
              onClick: () => openGlobalLightbox((message.mediaUrl || '').startsWith('http') ? message.mediaUrl : apiBase + message.mediaUrl, 'image', message.fileName)
            })
          ),
          message.mediaType === 'video' && message.mediaUrl && React.createElement('div', {
            style: { position: 'relative', cursor: 'pointer', maxWidth: 240, borderRadius: 18, overflow: 'hidden', background: '#090a0f' },
            onClick: () => openGlobalLightbox((message.mediaUrl || '').startsWith('http') ? message.mediaUrl : apiBase + message.mediaUrl, 'video', message.fileName)
          },
            React.createElement('video', { src: (message.mediaUrl || '').startsWith('http') ? message.mediaUrl : apiBase + message.mediaUrl, style: { width: '100%', display: 'block' } }),
            React.createElement('div', { style: { position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%,-50%)', width: 44, height: 44, borderRadius: '50%', background: 'rgba(0,0,0,0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center' } },
              React.createElement(SvgPlay)
            )
          ),
          hasChart && (dynamicData.length > 0 || dynamicUiComponent === 'DataGrid') && React.createElement('div',{
            className: 'gub-chart-clickable',
            onClick: () => setShowFullscreenChart(true),
            title: 'Click to open in full screen view',
            style: {
              background: 'var(--bg-panel,rgba(15,23,42,0.6))',
              border: '1px solid var(--border,rgba(255,255,255,0.1))',
              borderRadius: 14,
              padding: '10px 12px',
              maxWidth: '100%',
              overflowX: 'hidden',
              cursor: 'pointer',
              transition: 'all 0.2s',
              position: 'relative'
            }
          },
            React.createElement('div',{style:{display:'flex',alignItems:'center',justifyContent:'space-between',marginBottom:8}},
              React.createElement('div',{style:{display:'flex',alignItems:'center',gap:5}},
                React.createElement('span',{style:{width:6,height:6,borderRadius:'50%',background:'#ea580c',boxShadow:'0 0 6px #ea580c'}}),
                React.createElement('span',{style:{fontSize:9,fontWeight:600,color:'#64748b',textTransform:'uppercase',letterSpacing:'0.06em'}},dynamicUiComponent)
              ),
              React.createElement('div',{
                style:{fontSize:8.5,fontWeight:700,color:'#ea580c',background:'rgba(234,88,12,0.12)',border:'1px solid rgba(234,88,12,0.3)',borderRadius:6,padding:'2px 6px',display:'flex',alignItems:'center',gap:3}
              },'⛶ Fullscreen')
            ),
            dynamicUiComponent==='BarChart'   && React.createElement(SvgBarChart,  {data:dynamicData}),
            dynamicUiComponent==='PieChart'   && React.createElement(SvgDonutChart,{data:dynamicData}),
            dynamicUiComponent==='MetricCard' && React.createElement(MetricCardGrid,{data:dynamicData}),
            dynamicUiComponent==='DataGrid'   && React.createElement(DataGrid,     {data:dynamicData})
          ),
          React.createElement('span',{style:{fontSize:8,color:'#475569',fontWeight:500,paddingLeft:2}},time)
        )
      )
    ),

    // Reaction Badge at bottom right of bubble
    activeReactionEmojis.length > 0 && React.createElement('div', {
      className: 'gub-reaction-badge',
      style: {
        position: 'absolute',
        bottom: -10,
        right: isMine ? 4 : 'auto',
        left: !isMine ? 36 : 'auto',
        background: '#24252f',
        border: '1px solid rgba(255,255,255,0.15)',
        borderRadius: 999,
        padding: '1px 6px',
        fontSize: 12,
        boxShadow: '0 2px 8px rgba(0,0,0,0.5)',
        zIndex: 10,
        cursor: 'pointer',
        display: 'inline-flex',
        alignItems: 'center',
        gap: 2
      },
      title: 'View reactions',
      onClick: (e) => { e.stopPropagation(); setShowReactionModal(true); }
    }, activeReactionEmojis.join(' ')),

    showReactionModal && React.createElement(ReactionDetailsModal, {
      reactions: rxParsed,
      onClose: () => setShowReactionModal(false),
      onRemoveOwnReaction: (e) => { handleEmojiSelect(userReaction, e); setShowReactionModal(false); }
    })
  );

  return React.createElement('div',{ ref: bubbleRef, style: wrapperStyle },
    groupRow,

    showContextMenu && React.createElement(ContextMenuModal, {
      isMine,
      onCopy: async () => { try { await navigator.clipboard.writeText(body || ''); } catch (err) {} },
      onReply: handleReplyClick,
      onForward: () => window.dispatchEvent(new CustomEvent('chat:forwardMessage', { detail: message })),
      onRemove: () => window.dispatchEvent(new CustomEvent('chat:unsendMessage', { detail: message })),
      onEdit: () => window.dispatchEvent(new CustomEvent('chat:editMessage', { detail: message })),
      onPin: () => window.dispatchEvent(new CustomEvent('chat:pinMessage', { detail: message })),
      onClose: () => setShowContextMenu(false)
    }),

    showFullscreenChart && React.createElement(ChartFullscreenModal, {
      componentType: fullscreenMediaType || dynamicUiComponent,
      data: fullscreenMediaUrl || dynamicData,
      onClose: () => {
         setShowFullscreenChart(false);
         setFullscreenMediaType('');
         setFullscreenMediaUrl('');
      }
    }),

    React.createElement(EditHistoryModal, {
      isOpen: showEditHistoryModal,
      onClose: () => setShowEditHistoryModal(false),
      historyJson: message.editHistory || message.edit_history
    }),

    // Delivery / Seen Read Receipt state row (own messages only)
    isMine && React.createElement('div',{style:{display:'flex',alignItems:'center',gap:4,marginTop:2,paddingRight:0,justifyContent:'flex-end'}},
      React.createElement('span',{style:{fontSize:8,color:'#475569',fontWeight:500}},time),
      isLastSeenMessage
        ? (seenAvatarUrl && (seenAvatarUrl.startsWith('http') || seenAvatarUrl.startsWith('/'))
            ? React.createElement('img', { src: seenAvatarUrl.startsWith('http') ? seenAvatarUrl : apiBase + seenAvatarUrl, style: { width: 14, height: 14, borderRadius: '50%', objectFit: 'cover', boxShadow: '0 0 4px rgba(0,0,0,0.5)' }, title: 'Seen' })
            : React.createElement('div', { style: { width: 14, height: 14, borderRadius: '50%', background: 'linear-gradient(135deg, #ea580c, #f59e0b)', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 8, fontWeight: 700, color: 'white' }, title: 'Seen' }, (seenAvatarUrl || 'U')[0].toUpperCase())
          )
        : renderDeliveryBadge(currentStatus)
    )
  );
}

/* ── Exports ──────────────────────────────────────────────────────────────── */
if (typeof window !== 'undefined') {
  window.GenUiBubble       = GenUiBubble;
  window.AiThinkingBubble  = AiThinkingBubble;
}
