/**
 * ChatOverlay Component — World-Class B2B SaaS Chat Overlay + Drive&Go AI
 * Combined Aesthetics: Dark Mode + Glassmorphism + Bento/Card-Based UI
 * 100% Real Database & API Integration with PostgreSQL & SignalR Push
 * Renders Full Generative UI Widgets & Messages via GenUiBubble
 *
 * ✅ Pillar 01: Dynamic Online/Offline Status Dots
 * ✅ Pillar 02: Reply/Quote System (Preview Bar + Quoted Bubble)
 * ✅ Pillar 03: Hover Action Bar (React, Reply, More — 3 dots)
 * ✅ Pillar 04: Emoji Reaction Badges on Bubbles
 * ✅ Pillar 05: Media Attachment (hidden for AI)
 * ✅ Pillar 06: Voice Note Mic (hidden for AI)
 * ✅ Pillar 07: AI Suggestion Pills & Command Palette
 * ✅ Pillar 08: Info Panel (Contact Profile Drawer)
 * ✅ Pillar 09: Mention Autocomplete (@-tagging)
 * ✅ Pillar 10: RAF-Throttled Scroll Engine
 * ✅ Pillar 11: Native Chromium Context Menu for Images
 * ✅ Pillar 12: Link Preview in Input + Lightbox
 */
const { useState, useEffect, useRef, useCallback } = React;

function formatLocalTime(ts) {
  if (!ts) return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  let str = String(ts).trim();
  const hasTimezoneOffset = str.endsWith('Z') || /[+-]\d{2}:?\d{2}$/.test(str);
  const isoUtc = hasTimezoneOffset ? str : str + 'Z';
  const d = new Date(isoUtc);
  return isNaN(d.getTime())
    ? new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function formatMessengerTimestamp(ts) {
  if (!ts) return new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  let str = String(ts).trim();
  const hasTimezoneOffset = str.endsWith('Z') || /[+-]\d{2}:?\d{2}$/.test(str);
  const isoUtc = hasTimezoneOffset ? str : str + 'Z';
  const d = new Date(isoUtc);
  if (isNaN(d.getTime())) return formatLocalTime(ts);

  const now = new Date();
  
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const msgDateStart = new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
  const diffDays = Math.round((todayStart - msgDateStart) / (1000 * 60 * 60 * 24));

  const timeStr = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  // 1. Same Day (Today) -> "06:08 PM"
  if (diffDays === 0) {
    return timeStr;
  }
  
  // 2. Yesterday -> "Yesterday 06:08 PM"
  if (diffDays === 1) {
    return `Yesterday ${timeStr}`;
  }
  
  // 3. Same Week (2 to 6 days ago) -> "Mon 06:08 PM"
  if (diffDays > 1 && diffDays < 7) {
    const dayName = d.toLocaleDateString([], { weekday: 'short' });
    return `${dayName} ${timeStr}`;
  }
  
  // 4. Same Year (7+ days ago, within current year) -> "Aug 5, 06:08 PM"
  if (d.getFullYear() === now.getFullYear()) {
    const monthDay = d.toLocaleDateString([], { month: 'short', day: 'numeric' });
    return `${monthDay}, ${timeStr}`;
  }
  
  // 5. Different Year -> "Aug 5, 2025, 06:08 PM"
  const fullDate = d.toLocaleDateString([], { month: 'short', day: 'numeric', year: 'numeric' });
  return `${fullDate}, ${timeStr}`;
}

function sanitizeNonTechText(text) {
  if (!text || typeof text !== 'string') return text;
  const lower = text.toLowerCase();
  if (lower.includes('.env') || lower.includes('api key') || lower.includes('groq') || lower.includes('gemini api') || lower.includes('quotaexhausted') || lower.includes('rate limit') || lower.includes('limitasyon sa sistema')) {
    return 'Drive&Go AI is currently processing a high volume of requests. Please try asking your question again in a moment!';
  }
  return text
    .replace(/-*\s*UI_COMPONENT\s*-*/gi, '')
    .replace(/\bUI_COMPONENT\b/gi, '')
    .replace(/^(?:[\s\r\n]*---+\s*)+/g, '')
    .replace(/(?:[\s\r\n]*---+\s*)+$/g, '')
    .trim();
}

// Initial Conversations matching PostgreSQL contacts & Group channels
const INITIAL_CONVERSATIONS = [
  {
    id: 'ai_copilot',
    name: 'Drive&Go AI',
    role: 'AI COPILOT',
    status: 'Omniscient AI Intelligence',
    isOnline: true,
    isGroup: false,
    avatar: null,
    avatarBg: 'from-orange-500 to-amber-600',
    lastMessage: 'Ready to assist with fleet analytics, bookings & driver insights.',
    time: 'Just now',
    unreadCount: 0
  },
  {
    id: 'group_dispatch',
    name: 'Group @Drive&Go Admin',
    role: 'GROUP',
    status: '5 Members • Admin Dispatch',
    isOnline: true,
    isGroup: true,
    avatar: null,
    avatarBg: 'from-emerald-500 to-teal-600',
    lastMessage: 'Hi! How can I assist you with Drive&Go rental operations?',
    time: 'Sat',
    unreadCount: 0
  },
  {
    id: 'gc_drivers',
    name: 'Drivers Community GC',
    role: 'GROUP',
    status: '18 Drivers Active',
    isOnline: true,
    isGroup: true,
    avatar: null,
    avatarBg: 'from-blue-500 to-cyan-600',
    lastMessage: '@Drive&Go AI hi! Check current surge pricing rates.',
    time: 'Sat',
    unreadCount: 0
  },
  {
    id: 'gc_customers',
    name: 'Customers General Support',
    role: 'GROUP',
    status: 'General Customer Enquiries Channel',
    isOnline: true,
    isGroup: true,
    avatar: null,
    avatarBg: 'from-emerald-500 to-teal-600',
    lastMessage: '[Voice Note 0:06]',
    time: '10:53 PM',
    unreadCount: 0
  }
];

const DEFAULT_SUGGESTION_PILLS = [
  { text: '📊 Today\'s Revenue Summary', query: 'Hi magkano kinita natin ngayon araw??', keywords: ['revenue', 'sales', 'money', 'kinita', 'today', 'summary', 'kita', 'earning', 'financial', 'total'] },
  { text: '⚠️ Overdue Rentals & Penalties', query: 'List all overdue rentals with penalty estimates.', keywords: ['overdue', 'penalty', 'late', 'penalties', 'overdue rentals', 'unpaid', 'delay'] },
  { text: '🚗 Active Vehicle Leases', query: 'Show active vehicle rentals overview', keywords: ['vehicle', 'car', 'fleet', 'leases', 'active', 'rentals', 'auto', 'suv', 'sedan'] },
  { text: '🏎️ Competitor Market Analysis', query: 'What do u say about Anis Transport competitive analysis?', keywords: ['competitor', 'market', 'analysis', 'price', 'rates', 'surge', 'compare'] },
  { text: '🧑‍✈️ Driver Performance Ratings', query: 'Show top drivers and recent performance ratings', keywords: ['driver', 'ratings', 'performance', 'drivers', 'top driver', 'earning'] },
  { text: '🔧 Maintenance & Service Alerts', query: 'Show vehicles due for maintenance', keywords: ['maintenance', 'service', 'repair', 'oil', 'checkup', 'due', 'issue'] }
];

const InfoPanel = ({
  conv,
  onClose,
  viewMode = 'floating',
  apiBase,
  activeMessages,
  openAccordions,
  setOpenAccordions,
  fileInputRef,
  isUploading,
  uploadStatus,
  renderAvatarIcon,
  setLightboxMedia,
  uploadErrorModal,
  setUploadErrorModal
}) => {
    const [muted, setMuted] = useState(false);

    if (!conv) return null;
    const isAi = conv.id === 'ai_copilot';
    const isGroup = conv.isGroup || conv.role === 'Group' || conv.id.startsWith('gc_');
    const pfp = conv.avatarUrl || conv.pfp || conv.profilePicture;
    const fullPfp = pfp ? (pfp.startsWith('http') || pfp.startsWith('data:') ? pfp : (apiBase + (pfp.startsWith('/') ? '' : '/') + pfp)) : null;

    const toggleAccordion = (key) => {
      setOpenAccordions(prev => ({ ...prev, [key]: !prev[key] }));
    };

    const resolveMediaUrl = (url) => !url ? '' : (url.startsWith('http') || url.startsWith('data:') ? url : apiBase + (url.startsWith('/') ? '' : '/') + url);
    
    const mediaMessages = activeMessages.filter(m => {
      const type = m.mediaType || m.media_type;
      const url = m.mediaUrl || m.media_url || m.body || '';
      return ['image', 'video'].includes(type) || (url && (url.match(/\.(jpeg|jpg|gif|png|webp)/i) || url.startsWith('data:image')));
    });

    const isDark = (typeof document !== 'undefined' && document.documentElement.getAttribute('data-theme') !== 'light');

    return (
      <>
        {/* Subtle backdrop overlay on small/medium screens to dismiss info panel */}
        <div 
          onClick={onClose}
          className="absolute inset-0 bg-black/40 backdrop-blur-[2px] z-30 transition-opacity animate-fadeIn cursor-pointer"
        />

        <div className={`absolute inset-y-0 right-0 w-full sm:w-80 h-full flex flex-col overflow-y-auto chat-overlay-scrollbar transition-all duration-200 z-40 border-l shadow-2xl animate-in slide-in-from-right duration-200 ${
          isDark ? 'bg-[#0d0e1b] border-white/10 text-slate-100' : 'bg-white border-slate-200 text-slate-900 shadow-2xl'
        }`}>
        

        {/* Immediate High-Priority Red Error Alert Modal Popup */}
        {uploadErrorModal && (
          <div className="fixed inset-0 bg-slate-950/85 backdrop-blur-md flex items-center justify-center z-[9999] p-5 animate-in fade-in">
            <div className="bg-[#121424] border border-red-500/50 rounded-3xl p-6 max-w-xs w-full text-center shadow-2xl space-y-4 relative">
              <div className="w-14 h-14 rounded-2xl bg-red-500/20 text-red-400 flex items-center justify-center mx-auto text-2xl border border-red-500/30 shadow-inner">
                ⚠️
              </div>
              <div>
                <h3 className="text-sm font-extrabold text-white">Upload Error</h3>
                <p className="text-[11px] text-slate-300 mt-2 leading-relaxed font-medium">{uploadErrorModal}</p>
              </div>
              <button
                onClick={() => setUploadErrorModal(null)}
                className="w-full py-2.5 rounded-xl bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-500 hover:to-rose-500 text-white font-bold text-xs transition-all shadow-lg cursor-pointer"
              >
                Dismiss & Try Again
              </button>
            </div>
          </div>
        )}

        {/* Top Header */}
        <div className="flex items-center justify-between p-4 border-b border-white/10 shrink-0">
          <h3 className="text-sm font-bold text-white">{isGroup ? 'Group Details' : 'Chat Info'}</h3>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full bg-white/5 hover:bg-white/10 text-slate-400 hover:text-white flex items-center justify-center transition-all cursor-pointer text-base"
            title="Close Info Sidebar"
          >
            ✕
          </button>
        </div>

        {/* Top Profile Summary */}
        <div className="flex flex-col items-center p-6 gap-3 border-b border-white/10 shrink-0">
          <div
            onClick={() => isGroup && uploadStatus === 'idle' && fileInputRef.current?.click()}
            className={`w-20 h-20 rounded-3xl bg-gradient-to-br ${conv.avatarBg || 'from-slate-800 to-slate-900'} flex items-center justify-center text-white text-3xl font-extrabold shadow-xl relative group overflow-hidden ${
              isGroup ? 'cursor-pointer hover:ring-4 hover:ring-orange-500/40 transition-all' : ''
            }`}
            title={isGroup ? 'Click to change group photo' : ''}
          >
            {fullPfp ? (
              <img src={fullPfp} alt={conv.name} className="w-full h-full object-cover rounded-3xl" />
            ) : isGroup ? (
              <div className="w-full h-full rounded-3xl bg-gradient-to-br from-emerald-600 to-teal-800 flex items-center justify-center text-white font-extrabold text-xl">
                {conv.name.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase()}
              </div>
            ) : (
              renderAvatarIcon(conv)
            )}

            {/* Camera Badge Overlay at Bottom-Right for Group Channels */}
            {isGroup && uploadStatus === 'idle' && (
              <div className="absolute bottom-1 right-1 w-6 h-6 rounded-full bg-orange-500 text-white flex items-center justify-center shadow-lg border-2 border-[#0d0e1b] z-20 group-hover:scale-110 transition-transform">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/>
                  <circle cx="12" cy="13" r="4"/>
                </svg>
              </div>
            )}

            {/* Hover Dark Mask */}
            {isGroup && uploadStatus === 'idle' && (
              <div className="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 flex flex-col items-center justify-center transition-all text-white z-10">
                <span className="text-[9px] font-bold">Change Photo</span>
              </div>
            )}

            {/* Animated Circular SVG Progress Ring Overlay (Active during uploading/success/error) */}
            {uploadStatus !== 'idle' && (
              <div className="absolute inset-0 bg-slate-950/85 backdrop-blur-sm flex flex-col items-center justify-center text-white z-30 transition-all rounded-3xl">
                <div className="relative w-12 h-12 flex items-center justify-center">
                  <svg className="w-12 h-12 transform -rotate-90" viewBox="0 0 48 48">
                    <circle cx="24" cy="24" r="19" className="stroke-white/20" strokeWidth="3.5" fill="transparent" />
                    <circle
                      cx="24"
                      cy="24"
                      r="19"
                      className={`transition-all duration-150 ease-out ${
                        uploadStatus === 'error' ? 'stroke-red-500' : uploadStatus === 'success' ? 'stroke-emerald-400' : 'stroke-orange-500'
                      }`}
                      strokeWidth="3.5"
                      strokeDasharray="119.38"
                      strokeDashoffset={119.38 - (119.38 * uploadProgress) / 100}
                      strokeLinecap="round"
                      fill="transparent"
                    />
                  </svg>
                  <div className="absolute inset-0 flex items-center justify-center font-extrabold text-[11px]">
                    {uploadStatus === 'success' ? (
                      <span className="text-emerald-400 text-sm">✓</span>
                    ) : uploadStatus === 'error' ? (
                      <span className="text-red-400 text-xs">⚠️</span>
                    ) : (
                      <span className="text-white tracking-tighter">{uploadProgress}%</span>
                    )}
                  </div>
                </div>
                <span className={`text-[8.5px] font-bold mt-1 tracking-tight truncate max-w-[70px] text-center ${
                  uploadStatus === 'error' ? 'text-red-400' : uploadStatus === 'success' ? 'text-emerald-400' : 'text-slate-300'
                }`}>
                  {uploadStatus === 'success' ? 'Done 100%' : uploadStatus === 'error' ? 'Failed!' : `${uploadProgress}%`}
                </span>
              </div>
            )}
          </div>

          <div className="text-center w-full flex flex-col items-center">
            <h4 className="text-base font-bold text-white">{conv.name}</h4>
            <p className="text-[11px] text-slate-400 mt-0.5">{conv.status || (isGroup ? 'Group Channel' : 'User')}</p>

            {/* Explicit Change Group Photo Button */}
            {isGroup && (
              <button
                onClick={() => fileInputRef.current?.click()}
                disabled={isUploading}
                className="mt-2.5 px-3 py-1.5 rounded-xl bg-orange-500/15 hover:bg-orange-500/25 active:bg-orange-500/35 text-orange-400 text-xs font-bold transition-all border border-orange-500/30 flex items-center gap-1.5 cursor-pointer shadow-sm"
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M23 19a2 2 0 01-2 2H3a2 2 0 01-2-2V8a2 2 0 012-2h4l2-3h6l2 3h4a2 2 0 012 2z"/>
                  <circle cx="12" cy="13" r="4"/>
                </svg>
                <span>Change Group Photo</span>
              </button>
            )}

            <span className={`text-[10px] font-bold px-2.5 py-0.5 rounded-full mt-1.5 inline-block ${
              conv.role === 'AI COPILOT' ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30' :
              isGroup ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' :
              'bg-purple-500/20 text-purple-400 border border-purple-500/30'
            }`}>
              {conv.role}
            </span>

            {/* ── CUSTOMER ACCOUNT SHORTCUT BUTTON ── */}
            {!isGroup && !isAi && (
              <button
                onClick={() => {
                  if (window.chrome?.webview?.postMessage) {
                    window.chrome.webview.postMessage(JSON.stringify({
                      action: 'navigateToAccount',
                      customerId: conv.id,
                      customerName: conv.name
                    }));
                  }
                  showToast(`Navigating to ${conv.name}'s Account...`);
                }}
                className="w-full py-2.5 px-3 rounded-xl bg-orange-600/20 hover:bg-orange-600/30 border border-orange-500/40 text-orange-300 text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer shadow-sm mt-3"
                title="Navigate to Customer Accounts & Bookings Panel"
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/><line x1="18" y1="8" x2="23" y2="8"/><line x1="18" y1="12" x2="23" y2="12"/></svg>
                <span>View Customer Account</span>
              </button>
            )}
          </div>

          {/* Quick Action Buttons (SVG Icons: Mute & Search) */}
          <div className="flex items-center gap-4 mt-1">
            <button
              onClick={() => setMuted(prev => !prev)}
              className={`flex flex-col items-center gap-1 text-[10px] font-semibold transition-colors cursor-pointer ${muted ? 'text-orange-400' : 'text-slate-400 hover:text-white'}`}
            >
              <div className={`w-9 h-9 rounded-full border flex items-center justify-center ${muted ? 'bg-orange-500/20 border-orange-500/40 text-orange-400' : 'bg-white/5 border-white/10'}`}>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>
              </div>
              <span>{muted ? 'Muted' : 'Mute'}</span>
            </button>

            <button
              onClick={() => setSearchQuery(conv.name)}
              className="flex flex-col items-center gap-1 text-[10px] font-semibold text-slate-400 hover:text-white transition-colors cursor-pointer"
            >
              <div className="w-9 h-9 rounded-full bg-white/5 border border-white/10 flex items-center justify-center">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
              </div>
              <span>Search</span>
            </button>
          </div>
        </div>

        {/* Accordion 1: Chat Info */}
        <div className="border-b border-white/10">
          <button
            onClick={() => toggleAccordion('info')}
            className="w-full p-4 flex items-center justify-between text-xs font-bold text-slate-200 hover:bg-white/5 transition-colors cursor-pointer"
          >
            <span className="flex items-center gap-2">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-orange-400"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>
              Chat Info
            </span>
            <span className="text-slate-400 text-xs">{openAccordions.info ? '▲' : '▼'}</span>
          </button>
          {openAccordions.info && (
            <div className="px-4 pb-4 flex flex-col gap-2">
              <div className="p-3 rounded-xl bg-white/5 border border-white/5">
                <p className="text-[11px] font-bold text-slate-200">{conv.name}</p>
                <p className="text-[10px] text-slate-400 mt-1">{isGroup ? 'Group Channel' : isAi ? 'Drive&Go AI Assistant Thread' : 'Direct Customer Channel'}</p>
              </div>
              <div className="p-3 rounded-xl bg-white/5 border border-white/5">
                <p className="text-[11px] font-bold text-slate-200">Status</p>
                <p className="text-[10px] text-slate-400 mt-1">{conv.status || 'Active Contact'}</p>
              </div>
            </div>
          )}
        </div>

        {/* Accordion 3: Group Members (For Groups) */}
        {isGroup && (
          <div className="border-b border-white/10">
            <button
              onClick={() => toggleAccordion('members')}
              className="w-full p-4 flex items-center justify-between text-xs font-bold text-slate-200 hover:bg-white/5 transition-colors cursor-pointer"
            >
              <span className="flex items-center gap-2">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-orange-400"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
                Group Members (3)
              </span>
              <span className="text-slate-400 text-xs">{openAccordions.members ? '▲' : '▼'}</span>
            </button>
            {openAccordions.members && (
              <div className="px-4 pb-4 flex flex-col gap-2">
                {[
                  {
                    name: 'Admin Dispatcher',
                    role: 'ADMIN',
                    status: 'Online',
                    icon: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-emerald-400"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
                  },
                  {
                    name: 'Drive&Go AI',
                    role: 'BOT',
                    status: 'Active',
                    icon: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-amber-400"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>
                  },
                  {
                    name: 'Drivers Community',
                    role: 'MEMBER',
                    status: 'Active',
                    icon: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-cyan-400"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
                  }
                ].map(m => (
                  <div key={m.name} className="flex items-center justify-between p-2 rounded-xl bg-white/5 border border-white/5">
                    <div className="flex items-center gap-2 min-w-0">
                      <div className="w-7 h-7 rounded-full bg-slate-800 border border-white/10 flex items-center justify-center shrink-0">
                        {m.icon}
                      </div>
                      <div className="flex flex-col min-w-0">
                        <span className="text-xs font-bold text-slate-200 truncate">{m.name}</span>
                        <span className="text-[9.5px] text-slate-400">{m.status}</span>
                      </div>
                    </div>
                    <span className="text-[9px] font-extrabold px-1.5 py-0.5 rounded bg-orange-500/20 text-orange-400 border border-orange-500/30 shrink-0">
                      {m.role}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Accordion 4: Shared Media & Files */}
        <div className="border-b border-white/10">
          <button
            onClick={() => toggleAccordion('media')}
            className="w-full p-4 flex items-center justify-between text-xs font-bold text-slate-200 hover:bg-white/5 transition-colors cursor-pointer"
          >
            <span className="flex items-center gap-2">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-orange-400"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
              Shared Media & Files ({mediaMessages.length})
            </span>
            <span className="text-slate-400 text-xs">{openAccordions.media ? '▲' : '▼'}</span>
          </button>
          {openAccordions.media && (
            <div className="px-4 pb-4">
              {mediaMessages.length > 0 ? (
                <div className="grid grid-cols-3 gap-2">
                  {mediaMessages.map((m, i) => {
                    const rawUrl = m.mediaUrl || m.media_url || m.body || '';
                    const resolvedUrl = resolveMediaUrl(rawUrl);
                    return (
                      <div
                        key={m.id || i}
                        className="aspect-square rounded-xl overflow-hidden bg-slate-900 border border-white/10 relative group cursor-pointer shadow-sm"
                        onClick={() => {
                          if (resolvedUrl) {
                            const isVid = m.mediaType === 'video' || resolvedUrl.match(/\.(mp4|webm|ogg|mov)/i);
                            setLightboxMedia({
                              url: resolvedUrl,
                              type: isVid ? 'video' : 'image',
                              title: m.fileName || (isVid ? 'Shared Video File' : 'Shared Image File')
                            });
                          }
                        }}
                      >
                        {resolvedUrl ? (
                          <img
                            src={resolvedUrl}
                            alt="Shared Media"
                            onError={(e) => {
                              e.target.style.display = 'none';
                              if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex';
                            }}
                            className="w-full h-full object-cover group-hover:scale-105 transition-transform"
                          />
                        ) : null}
                        <div className={`w-full h-full bg-slate-900 flex-col items-center justify-center gap-1 p-1 text-center ${resolvedUrl ? 'hidden' : 'flex'}`}>
                          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-orange-400"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
                          <span className="text-[9px] text-slate-400 font-bold truncate w-full">Media</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <p className="text-[11px] text-slate-500 italic text-center py-2">No media shared yet</p>
              )}
            </div>
          )}
        </div>
      </div>
    </>
  );
};

function ChatOverlay({ initialQuery = '' }) {
  const { useState, useEffect, useRef, useCallback } = React;

  const GenUiBubbleComp = typeof GenUiBubble !== 'undefined' ? GenUiBubble : (window.GenUiBubble || null);
  const AiThinkingBubbleComp = typeof AiThinkingBubble !== 'undefined' ? AiThinkingBubble : (window.AiThinkingBubble || null);
  const CommandPaletteComp = typeof CommandPalette !== 'undefined' ? CommandPalette : (window.CommandPalette || null);

  // ── States ─────────────────────────────────────────────────────────────────
  const [conversations, setConversations] = useState(INITIAL_CONVERSATIONS);
  const [allContacts, setAllContacts] = useState([]);
  const [activeConvId, setActiveConvId] = useState('ai_copilot');
  const [activeTabFilter, setActiveTabFilter] = useState('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [viewMode, setViewMode] = useState('floating'); // 'floating' | 'split' | 'fullscreen'
  const [showMobileSidebar, setShowMobileSidebar] = useState(false);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [showNewChatModal, setShowNewChatModal] = useState(false);
  const [newChatSearch, setNewChatSearch] = useState('');
  const [newChatTab, setNewChatTab] = useState('all');

  const [theme, setTheme] = useState(() => {
    return (typeof document !== 'undefined' && document.documentElement.getAttribute('data-theme')) || 'dark';
  });

  useEffect(() => {
    window.setChatTheme = function(mode) {
      if (mode === 'light' || mode === 'dark') {
        document.documentElement.setAttribute('data-theme', mode);
        setTheme(mode);
      }
    };
    const obs = new MutationObserver(() => {
      const currentMode = document.documentElement.getAttribute('data-theme') || 'dark';
      setTheme(currentMode);
    });
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    return () => obs.disconnect();
  }, []);

  const isDark = theme === 'dark';

  // Push total unread messages count to C# Host for FAB badge display
  useEffect(() => {
    const totalUnread = conversations.reduce((acc, c) => acc + (c.unreadCount || 0), 0);
    if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
      window.chrome.webview.postMessage(JSON.stringify({
        action: "updateUnreadCount",
        count: totalUnread
      }));
    }
  }, [conversations]);

  // ── PILLAR 08: Info Panel State ────────────────────────────────────────────
  const [showInfoPanel, setShowInfoPanel] = useState(false);

  // Info Panel Accordions State (Lifted to prevent auto-closing on live chat updates)
  const [openAccordions, setOpenAccordions] = useState({
    info: true,
    customize: false,
    members: true,
    media: true,
    privacy: false
  });

  // Upload Group Avatar States (Lifted to ChatOverlay to prevent unmounting bugs)
  const [isUploading, setIsUploading]         = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploadStatus, setUploadStatus]     = useState('idle');
  const [uploadError, setUploadError]       = useState(null);
  const [uploadErrorModal, setUploadErrorModal] = useState(null);
  const fileInputRef                       = useRef(null);

  const [inputText, setInputText] = useState('');
  const [hasAiMentionTag, setHasAiMentionTag] = useState(false);
  const [drafts, setDrafts] = useState({}); // { [convId]: { text: string, hasMention: boolean } }
  const draftPrevConvIdRef = useRef(activeConvId);
  const scrollPrevConvIdRef = useRef(activeConvId);
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);
  const inputRef = useRef(null);
  const inputShellRef = useRef(null);
  const isSendingRef = useRef(false);
  const lastSendTimeRef = useRef(0);

  // Messages State indexed by Conv ID
  const [messagesByConv, setMessagesByConv] = useState({});

  // ── PILLAR 02: Reply/Quote State ───────────────────────────────────────────
  const [replyingTo, setReplyingTo] = useState(null); // { id, sender, body, mediaType }

  // Link Preview State
  const [linkPreviewData, setLinkPreviewData] = useState(null);
  const [isLinkPreviewDismissed, setIsLinkPreviewDismissed] = useState(false);
  const linkDebounceTimer = useRef(null);

  // Media Attachment Upload State
  const mediaInputRef = useRef(null);
  const [attachedMedia, setAttachedMedia] = useState(null);

  // Web Audio API Recording State
  const [isRecording, setIsRecording] = useState(false);
  const [recordingSeconds, setRecordingSeconds] = useState(0);
  const mediaRecorderRef = useRef(null);
  const audioChunksRef = useRef([]);
  const audioContextRef = useRef(null);
  const analyserRef = useRef(null);
  const visualizerCanvasRef = useRef(null);
  const animFrameIdRef = useRef(null);
  const timerIntervalRef = useRef(null);

  // Mention Autocomplete State (@Drive&Go AI)
  const [mentionQuery, setMentionQuery] = useState(null);
  const [mentionIndex, setMentionIndex] = useState(0);
  const mentionTargets = [
    { id: 'ai', name: 'Drive&Go AI', role: 'AI Support Assistant', avatar: 'AI' }
  ];

  const [activePopup, setActivePopup] = useState(null); // { type, message, rect }
  const [systemModal, setSystemModal] = useState(null); // { type, message }
  const [unsendScope, setUnsendScope] = useState('everyone');
  const [forwardQuery, setForwardQuery] = useState('');
  const [reactionTab, setReactionTab] = useState('All');
  const [editingMessage, setEditingMessage] = useState(null);
  const [pinnedMessage, setPinnedMessage] = useState(null);
  const [adminProfilePfp, setAdminProfilePfp] = useState(null);
  const [toastMessage, setToastMessage] = useState('');
  const [lightboxMedia, setLightboxMedia] = useState(null);

  useEffect(() => {
    fetch(`${apiBase}/api/users/1`)
      .then(res => res.json())
      .then(data => {
        if (data && (data.avatarBase64 || data.avatar_base64)) {
          const pic = data.avatarBase64 || data.avatar_base64;
          if (pic && pic.length > 20) {
            setAdminProfilePfp(pic.startsWith('data:') ? pic : `data:image/png;base64,${pic}`);
          }
        }
      })
      .catch(() => {});
  }, []);

  // AI & Messenger Typing States
  const [aiSessionId, setAiSessionId] = useState(null);
  const [aiLoadingConvoId, setAiLoadingConvoId] = useState(null);
  const isAiLoading = Boolean(aiLoadingConvoId) && String(aiLoadingConvoId) === String(activeConvId);

  useEffect(() => {
    if (isAiLoading) {
      setTimeout(() => scrollToBottom(), 50);
    }
  }, [isAiLoading]);

  // ── Auto-Expand Textarea Height Dynamic Effect ──────────────────────────────
  useEffect(() => {
    if (inputRef.current) {
      inputRef.current.style.height = 'auto';
      const newHeight = Math.min(inputRef.current.scrollHeight, 160);
      inputRef.current.style.height = `${newHeight}px`;
    }
  }, [inputText]);

  // Antigravity-Style AI Request Queue State & Ref
  const [aiQueue, setAiQueue] = useState([]);
  const aiQueueRef = useRef([]);
  const isProcessingQueueRef = useRef(false);
  const cancelledQueueItemIdsRef = useRef(new Set());

  const updateAiQueue = (newQueue) => {
    aiQueueRef.current = newQueue;
    setAiQueue(newQueue);
  };

  // ── Per-Conversation Draft Sync Effect (Messenger Style) ───────────────────
  useEffect(() => {
    const prevId = draftPrevConvIdRef.current;
    if (prevId === activeConvId) return;

    // 1. Save draft for previous conversation thread
    setDrafts(prev => ({
      ...prev,
      [prevId]: {
        text: inputText,
        hasMention: hasAiMentionTag
      }
    }));

    // 2. Load draft for newly selected activeConvId (or clear if empty)
    const targetDraft = drafts[activeConvId];
    if (targetDraft) {
      setInputText(targetDraft.text || '');
      setHasAiMentionTag(!!targetDraft.hasMention);
    } else {
      setInputText('');
      setHasAiMentionTag(false);
    }

    // 3. Update previous conv ref
    draftPrevConvIdRef.current = activeConvId;
  }, [activeConvId]);

  // Desktop Mouse Drag-to-Scroll State for AI Suggestion Pills
  const pillScrollRef = useRef(null);
  const [isPillDragging, setIsPillDragging] = useState(false);
  const [pillStartX, setPillStartX] = useState(0);
  const [pillScrollLeft, setPillScrollLeft] = useState(0);
  const [hasPillDragged, setHasPillDragged] = useState(false);

  const handlePillMouseDown = (e) => {
    if (!pillScrollRef.current) return;
    setIsPillDragging(true);
    setHasPillDragged(false);
    setPillStartX(e.pageX - pillScrollRef.current.offsetLeft);
    setPillScrollLeft(pillScrollRef.current.scrollLeft);
  };

  const handlePillMouseLeaveOrUp = () => {
    setIsPillDragging(false);
  };

  const handlePillMouseMove = (e) => {
    if (!isPillDragging || !pillScrollRef.current) return;
    e.preventDefault();
    const x = e.pageX - pillScrollRef.current.offsetLeft;
    const walk = (x - pillStartX) * 1.8;
    if (Math.abs(walk) > 4) {
      setHasPillDragged(true);
    }
    pillScrollRef.current.scrollLeft = pillScrollLeft - walk;
  };
  const [typingUsers, setTypingUsers] = useState({});
  const typingTimeoutRef = useRef(null);

  const messageEndRef = useRef(null);
  const chatScrollContainerRef = useRef(null);
  const isUserScrolledUp = useRef(false);

  // ── Render Custom SVG Badges & Customer Profile Pictures ─────────────────
  const renderAvatarIcon = (conv) => {
    if (!conv) return null;
    const pfp = conv.avatarUrl || conv.avatar_url || conv.pfp || conv.profilePicture || conv.profile_picture;
    if (pfp && typeof pfp === 'string' && pfp.trim().length > 0 && !pfp.includes('undefined') && !pfp.includes('null')) {
      const fullPfp = (pfp.startsWith('http') || pfp.startsWith('data:') || pfp.startsWith('blob:'))
        ? pfp
        : (apiBase + (pfp.startsWith('/') ? '' : '/') + pfp);
      return <img src={fullPfp} alt={conv.name || ''} className="w-full h-full object-cover rounded-xl" />;
    }
    if (conv.id === 'ai_copilot') {
      return (
        <svg className="w-5 h-5 text-amber-300 drop-shadow-[0_0_8px_rgba(245,158,11,0.8)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
        </svg>
      );
    }
    if (conv.isGroup || conv.role === 'GROUP') {
      if (conv.id === 'group_dispatch' || (conv.name && conv.name.includes('Admin'))) {
        return (
          <svg className="w-5 h-5 text-emerald-300 drop-shadow-[0_0_8px_rgba(16,185,129,0.8)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
          </svg>
        );
      }
      if (conv.id === 'gc_drivers' || (conv.name && conv.name.toLowerCase().includes('driver'))) {
        return (
          <svg className="w-5 h-5 text-cyan-300 drop-shadow-[0_0_8px_rgba(6,182,212,0.8)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h8m-8 4h8m-4 4h4M5 3h14a2 2 0 012 2v14a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2z" />
          </svg>
        );
      }
      return (
        <svg className="w-5 h-5 text-teal-300 drop-shadow-[0_0_8px_rgba(20,184,166,0.8)]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
        </svg>
      );
    }
    const initial = ((conv.name || 'User').trim())[0].toUpperCase();
    return <span className="font-extrabold text-sm text-white tracking-wider">{initial}</span>;
  };

  const getRoleBadgeClasses = (role) => {
    const r = (role || '').toUpperCase();
    if (r.includes('ADMIN')) return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
    if (r.includes('DRIVER')) return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
    if (r.includes('MAINTENANCE') || r.includes('MECHANIC')) return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
    if (r.includes('STAFF') || r.includes('DISPATCH')) return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
    if (r.includes('GROUP')) return 'bg-cyan-500/20 text-cyan-400 border border-cyan-500/30';
    if (r.includes('AI')) return 'bg-amber-400/20 text-amber-300 border border-amber-400/30';
    return 'bg-purple-500/20 text-purple-400 border border-purple-500/30';
  };

  const apiBase = (window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233` : 'http://localhost:5233')).replace(/\/api\/?$/i, '').replace(/\/$/, '');
  const activeMessages = messagesByConv[activeConvId] || [];
  const activeConv = conversations.find(c => String(c.id) === String(activeConvId)) ||
                     allContacts.find(c => String(c.id) === String(activeConvId)) ||
                     conversations[0] || {
    id: activeConvId,
    name: activeConvId === 'ai_copilot' ? 'Drive&Go Copilot' : 'Conversation',
    isGroup: false,
    avatar: '👤',
    avatarBg: 'from-purple-500 to-indigo-600',
    isAi: activeConvId === 'ai_copilot',
    isOnline: false,
    status: 'Active Contact',
    role: 'Customer'
  };

  // Is AI channel? Controls which input controls are shown
  const isAiChannel = activeConvId === 'ai_copilot';
  const currentMentionOptions = mentionTargets.filter(t => t.name.toLowerCase().includes((mentionQuery || '').toLowerCase()));

  const Portal = ({ children }) => {
    const ReactDOMObj = (typeof window !== 'undefined' && window.ReactDOM) ? window.ReactDOM : null;
    if (ReactDOMObj && typeof ReactDOMObj.createPortal === 'function' && document.body) {
      return ReactDOMObj.createPortal(children, document.body);
    }
    return children;
  };

  const getMessageKey = (msg) => msg?.messageId || msg?.id;
  const getNumericMessageId = (msg) => {
    const raw = getMessageKey(msg);
    const parsed = Number.parseInt(raw, 10);
    return Number.isFinite(parsed) ? parsed : null;
  };

  const parseReactions = (reactions) => {
    if (!reactions || reactions === '{}') return {};
    try {
      return typeof reactions === 'string' ? JSON.parse(reactions) : (reactions || {});
    } catch {
      return {};
    }
  };

  const updateMessageLocally = (messageKey, patcher) => {
    setMessagesByConv(prev => ({
      ...prev,
      [activeConvId]: (prev[activeConvId] || []).map(m => {
        if (String(getMessageKey(m)) !== String(messageKey)) return m;
        return typeof patcher === 'function' ? patcher(m) : { ...m, ...patcher };
      })
    }));
  };

  const refreshActiveThread = async () => {
    await fetchThreadMessages(activeConvId);
    fetchDbConversations();
  };

  const showToast = (message) => {
    setToastMessage(message);
    window.clearTimeout(showToast._timer);
    showToast._timer = window.setTimeout(() => setToastMessage(''), 2200);
  };

  const openAnchoredPopup = (type, message, event) => {
    const node = event?.currentTarget;
    const rect = node?.getBoundingClientRect ? node.getBoundingClientRect() : null;
    setActivePopup({ type, message, rect });
  };

  const closePopups = () => setActivePopup(null);

  const callMessageAction = async (msg, action, body = null, method = 'POST') => {
    const id = getNumericMessageId(msg);
    if (!id) {
      showToast('This message is still syncing.');
      return false;
    }
    const res = await fetch(`${apiBase}/api/messages/${id}/${action}`, {
      method,
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined
    });
    if (!res.ok) throw new Error(`${action} failed`);
    return true;
  };

  const handleReactToMessage = async (msg, emoji) => {
    const id = getNumericMessageId(msg);
    if (!id) {
      showToast('This message is still syncing.');
      return;
    }
    const current = parseReactions(msg.reactions);
    const removing = current.admin === emoji || !emoji;
    const next = { ...current };
    if (removing) delete next.admin;
    else next.admin = emoji;

    updateMessageLocally(getMessageKey(msg), m => ({ ...m, reactions: JSON.stringify(next) }));
    closePopups();

    try {
      if (removing) {
        await fetch(`${apiBase}/api/messages/${id}/react?userId=admin`, { method: 'DELETE' });
      } else {
        await fetch(`${apiBase}/api/messages/${id}/react`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ userId: 'admin', emoji, Emoji: emoji })
        });
      }
      await refreshActiveThread();
    } catch (err) {
      console.warn('[ChatOverlay] Reaction update failed:', err);
      fetchThreadMessages(activeConvId);
    }
  };

  const handleForwardMessage = async (msg, receiverId) => {
    const id = getNumericMessageId(msg);
    if (!id) {
      showToast('This message is still syncing.');
      return;
    }
    const target = receiverId;
    try {
      await fetch(`${apiBase}/api/messages/forward`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ originalMessageId: id, senderId: 'admin', newReceiverId: target })
      });
      setSystemModal(null);
      showToast('Message forwarded.');
      fetchDbConversations();
    } catch (err) {
      console.warn('[ChatOverlay] Forward failed:', err);
      showToast('Forward failed.');
    }
  };

  const handleUnsendMessage = async (msg, scope) => {
    try {
      // Cancel any pending AI queue items matching this message
      const msgKey = getMessageKey(msg);
      cancelledQueueItemIdsRef.current.add(msgKey);
      if (msg.id) cancelledQueueItemIdsRef.current.add(msg.id);
      updateAiQueue(aiQueueRef.current.filter(item => item.id !== msgKey && item.id !== msg.id));

      if (scope === 'everyone') {
        await callMessageAction(msg, 'unsend', null, 'DELETE');
        updateMessageLocally(getMessageKey(msg), { isUnsent: true, is_unsent: true, body: '', messageBody: '' });
      } else {
        await callMessageAction(msg, 'remove', { userId: 'admin' });
        setMessagesByConv(prev => ({
          ...prev,
          [activeConvId]: (prev[activeConvId] || []).filter(m => String(getMessageKey(m)) !== String(getMessageKey(msg)))
        }));
      }
      setSystemModal(null);
      await refreshActiveThread();
    } catch (err) {
      console.warn('[ChatOverlay] Unsend/remove failed:', err);
      showToast('Remove failed.');
    }
  };

  const startEditMessage = (msg) => {
    closePopups();
    setSystemModal(null);
    setEditingMessage(msg);
    setInputText(msg.body || msg.messageBody || '');
    window.setTimeout(() => {
      inputRef.current?.focus();
      inputRef.current?.setSelectionRange?.((msg.body || msg.messageBody || '').length, (msg.body || msg.messageBody || '').length);
    }, 0);
  };

  const saveEditedMessage = async () => {
    if (!editingMessage || !inputText.trim()) return;
    const id = getNumericMessageId(editingMessage);
    if (!id) {
      showToast('This message is still syncing.');
      return;
    }
    const newText = inputText.trim();
    updateMessageLocally(getMessageKey(editingMessage), { body: newText, messageBody: newText, isEdited: true, is_edited: true });
    try {
      await fetch(`${apiBase}/api/messages/${id}/edit`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ newText, text: newText, body: newText })
      });
      setEditingMessage(null);
      setInputText('');
      await refreshActiveThread();
    } catch (err) {
      console.warn('[ChatOverlay] Edit failed:', err);
      showToast('Edit failed.');
      fetchThreadMessages(activeConvId);
    }
  };

  const cancelEditMessage = () => {
    setEditingMessage(null);
    setInputText('');
    inputRef.current?.focus();
  };

  const handlePinOrReport = async (msg, action) => {
    try {
      await callMessageAction(msg, action, { userId: 'admin' });
      closePopups();
      showToast(action === 'pin' ? 'Message pinned.' : 'Report sent.');
    } catch (err) {
      console.warn(`[ChatOverlay] ${action} failed:`, err);
      showToast(`${action === 'pin' ? 'Pin' : 'Report'} failed.`);
    }
  };

  // ══════════════════════════════════════════════════════════════════════════
  //  REAL DATABASE INTEGRATION & LIVE FETCHING
  // ══════════════════════════════════════════════════════════════════════════

  // 1. Fetch Conversations from Database API (/api/messages/conversations?userId=admin)
  const fetchDbConversations = async () => {
    try {
      const res = await fetch(`${apiBase}/api/messages/conversations?userId=admin`);
      if (res.ok) {
        const rawJson = await res.json();
        const dbList = Array.isArray(rawJson) ? rawJson : (rawJson && Array.isArray(rawJson.value) ? rawJson.value : []);

        if (dbList.length > 0) {
          const dbFormatted = dbList.map(c => {
            const cId = String(c.id || c.contactId);
            const isAi = cId === 'ai_copilot';
            const isGroup = !!c.isGroupChat || !!c.isGroup || cId.startsWith('gc_') || cId.startsWith('g') || cId.startsWith('@');

            let name = c.name;
            if (cId === '@Drive&Go AI') name = 'Group @Drive&Go Admin';

            let avatar = (name || 'U')[0].toUpperCase();
            let avatarBg = 'from-purple-500 to-indigo-600';

            if (isAi) {
              avatar = 'AI';
              avatarBg = 'from-orange-500 to-amber-600';
            } else if (isGroup) {
              avatar = cId === 'gc_drivers' ? '🚘' : '🏢';
              avatarBg = cId === 'gc_drivers' ? 'from-blue-500 to-cyan-600' : 'from-emerald-500 to-teal-600';
            }

            return {
              id: cId,
              name: name || (isGroup ? 'Group ' + cId : 'User ' + cId),
              role: isAi ? 'AI COPILOT' : isGroup ? 'GROUP' : (c.role || 'CUSTOMER').toUpperCase(),
              status: isAi ? 'Omniscient AI Intelligence' : isGroup ? 'Group Channel' : 'Active Contact',
              isOnline: c.isOnline !== undefined ? !!c.isOnline : isAi || isGroup,
              isGroup,
              avatar,
              avatarBg,
              avatarUrl: c.avatarUrl || c.avatar_url || c.pfp || c.profilePicture || c.profile_picture || null,
              lastMessage: sanitizeNonTechText(c.lastMessage || ''),
              time: c.time || formatLocalTime(c.timestamp),
              unreadCount: c.unreadCount || 0
            };
          });

          setConversations(prev => {
            const dbMap = new Map(dbFormatted.map(x => [x.id, x]));
            const prevMap = new Map(prev.map(x => [x.id, x]));

            const merged = INITIAL_CONVERSATIONS.map(item => {
              let match = dbMap.get(item.id);
              if (!match && item.id === 'group_dispatch') {
                match = dbMap.get('@Drive&Go AI');
              }
              const prevItem = prevMap.get(item.id);
              const activeAvatar = (match && match.avatarUrl) || (prevItem && prevItem.avatarUrl) || item.avatarUrl || null;

              if (match) {
                return {
                  ...item,
                  ...match,
                  id: item.id, // preserve frontend ID 'group_dispatch'
                  avatarUrl: activeAvatar,
                  pfp: activeAvatar
                };
              }
              return {
                ...item,
                avatarUrl: activeAvatar,
                pfp: activeAvatar
              };
            });
            
            const initialIds = new Set(INITIAL_CONVERSATIONS.map(x => x.id));
            dbFormatted.forEach(x => {
              if (!initialIds.has(x.id) && x.id !== '@Drive&Go AI') {
                const prevItem = prevMap.get(x.id);
                const activeAvatar = x.avatarUrl || (prevItem && prevItem.avatarUrl) || null;
                merged.push({ ...x, avatarUrl: activeAvatar, pfp: activeAvatar });
              }
            });
            return merged;
          });
        }
      }
    } catch (err) {
      console.warn("[ChatOverlay] DB Conversations fetch warning:", err);
    }
  };

  // 1b. Fetch All Registered Database Contacts (/api/messages/contacts)
  const fetchDbContacts = async () => {
    try {
      const res = await fetch(`${apiBase}/api/messages/contacts`);
      if (res.ok) {
        const rawJson = await res.json();
        const list = Array.isArray(rawJson) ? rawJson : [];
        const formatted = list.map(c => {
          const rawRole = (c.role || 'Customer').trim();
          const roleUpper = rawRole.toUpperCase();
          let avatarBg = 'from-purple-500 to-indigo-600';
          if (roleUpper.includes('ADMIN')) avatarBg = 'from-rose-500 to-amber-600';
          else if (roleUpper.includes('DRIVER')) avatarBg = 'from-emerald-500 to-teal-600';
          else if (roleUpper.includes('MAINTENANCE') || roleUpper.includes('MECHANIC')) avatarBg = 'from-amber-500 to-orange-600';
          else if (roleUpper.includes('STAFF') || roleUpper.includes('DISPATCH')) avatarBg = 'from-sky-500 to-blue-600';

          return {
            id: String(c.id),
            name: c.name || ('User ' + c.id),
            role: roleUpper,
            displayRole: rawRole,
            status: `${rawRole} Account`,
            isOnline: false,
            isGroup: false,
            avatar: (c.name || 'U')[0].toUpperCase(),
            avatarBg,
            avatarUrl: c.avatarUrl || null,
            email: c.email || '',
            phone: c.phone || '',
            lastMessage: 'Tap to start conversation',
            time: '',
            unreadCount: 0,
            hasNoMessages: true
          };
        });
        setAllContacts(formatted);
      }
    } catch (e) {
      console.warn("[ChatOverlay] DB Contacts fetch warning:", e);
    }
  };

  // 2. Fetch Thread Messages for Active Contact from DB (/api/messages?senderId=admin&receiverId={contactId})
  const fetchThreadMessages = async (contactId) => {
    if (!contactId) return;
    try {
      const targetQueryId = contactId;
      const res = await fetch(`${apiBase}/api/messages?senderId=admin&receiverId=${encodeURIComponent(targetQueryId)}`);
      if (res.ok) {
        const rawJson = await res.json();
        const dbMsgs = Array.isArray(rawJson) ? rawJson : (rawJson && Array.isArray(rawJson.value) ? rawJson.value : []);

        if (dbMsgs.length > 0) {
          const formattedMsgs = dbMsgs.map((m, idx) => {
            let bodyText = m.messageBody || m.body || '';
            let uiComp = m.ui_component || m.uiComponentType || 'Text Only';
            let uiData = m.data || m.uiPayload || [];

            if (bodyText && typeof bodyText === 'string' && bodyText.trim().startsWith('{')) {
              try {
                const pJson = JSON.parse(bodyText.trim());
                if (pJson && typeof pJson === 'object') {
                  if (pJson.text !== undefined) bodyText = pJson.text;
                  if (pJson.ui_component) uiComp = pJson.ui_component;
                  if (pJson.data) uiData = pJson.data;
                }
              } catch (e) {}
            }

            bodyText = sanitizeNonTechText(bodyText);

            const isMine = m.senderId === 'admin';
            const senderDisplayName = isMine
              ? 'Admin'
              : (m.senderName || (contactId === 'ai_copilot' ? 'Drive&Go AI' : activeConv.name));

            let metaObj = {};
            if (m.mediaMetadata) {
              try {
                metaObj = typeof m.mediaMetadata === 'string' ? JSON.parse(m.mediaMetadata) : m.mediaMetadata;
              } catch (e) {}
            }

            let delStatus = m.deliveryStatus || 'delivered';
            if (isMine && (contactId === 'ai_copilot' || m.receiverId === 'ai_copilot' || contactId === 'group_dispatch')) {
              const hasAiResponseAfter = dbMsgs.slice(idx + 1).some(next => 
                next.senderId === 'ai_copilot' || next.senderId === '@Drive&Go AI' || next.senderName === 'Drive&Go AI'
              );
              if (hasAiResponseAfter) {
                delStatus = 'seen';
              }
            }

            return {
              id: m.messageId || m.id || Date.now(),
              messageId: m.messageId || m.id,
              senderId: m.senderId,
              receiverId: m.receiverId,
              sender: senderDisplayName,
              senderName: senderDisplayName,
              body: bodyText,
              messageBody: bodyText,
              isMine,
              time: formatLocalTime(m.timestamp || m.time),
              timestamp: m.timestamp,
              deliveryStatus: delStatus,
              status: delStatus,
              isEdited: !!(m.isEdited || m.is_edited),
              editHistory: m.editHistory || m.edit_history,
              isUnsent: !!(m.isUnsent || m.is_unsent),
              is_unsent: !!(m.isUnsent || m.is_unsent),
              reactions: typeof m.reactions === 'string' ? JSON.parse(m.reactions || '{}') : (m.reactions || {}),
              mediaType: m.mediaType,
              mediaUrl: m.mediaUrl,
              mediaMetadata: m.mediaMetadata,
              ui_component: uiComp,
              data: uiData,
              // Reply/Quote fields (from DB columns or JSONB mediaMetadata)
              replyToId: m.replyToId || m.reply_to_id || metaObj.replyToId || metaObj.reply_to_id || null,
              replyToSender: m.replyToSender || m.reply_to_sender || metaObj.replyToSender || metaObj.reply_to_sender || null,
              replyToBody: m.replyToBody || m.reply_to_body || metaObj.replyToBody || metaObj.reply_to_body || null,
              replyToMediaType: m.replyToMediaType || m.reply_to_media_type || metaObj.replyToMediaType || metaObj.reply_to_media_type || null,
            };
          });

          // Deduplicate thread messages by sender + body + time to prevent any duplicate bubbles
          const seenKeys = new Set();
          const uniqueMsgs = formattedMsgs.filter(msg => {
            const cleanBody = (msg.body || '').trim().toLowerCase();
            const textKey = `${msg.senderId}:${cleanBody}:${msg.time}`;
            if (seenKeys.has(textKey)) return false;
            seenKeys.add(textKey);
            return true;
          });

          setMessagesByConv(prev => ({
            ...prev,
            [contactId]: uniqueMsgs
          }));

          try {
            await fetch(`${apiBase}/api/messages/thread/${encodeURIComponent(targetQueryId)}/seen`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ viewerId: 'admin' })
            });
          } catch (e) {}
        }
      }
    } catch (err) {
      console.warn(`[ChatOverlay] DB Messages fetch warning for ${contactId}:`, err);
    }
  };

  // 3. Listen to Real-Time SignalR Web Messages from C# Host Control
  useEffect(() => {
    fetchDbConversations();
    fetchDbContacts();
    fetchThreadMessages(activeConvId);

    const handleWebMessage = (e) => {
      try {
        const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
        if (!data || !data.event_name) return;

        if (data.event_name === 'ReceiveChatMessage') {
          const isMineOrAi = data.senderId === 'admin' || data.senderId === '@Drive&Go AI' || data.senderId === 'Drive&Go AI' || (data.senderId && data.senderId.includes('AI'));
          const targetThread = isMineOrAi ? data.receiverId : data.senderId;
          fetchThreadMessages(targetThread);
          fetchDbConversations();
        } else if (data.event_name === 'MessageStatusChanged' || data.event_name === 'MessageEdited' || data.event_name === 'MessageUnsent' || data.event_name === 'MessageReactionChanged') {
          fetchThreadMessages(activeConvId);
        } else if (data.event_name === 'TypingStatusChanged') {
          if (data.senderId !== 'admin') {
            setTypingUsers(prev => ({ ...prev, [data.senderId]: !!data.isTyping }));
          }
        } else if (data.event_name === 'ThreadSeen') {
          fetchThreadMessages(activeConvId);
          fetchDbConversations();
        }
      } catch (err) {}
    };

    if (window.chrome?.webview?.addEventListener) {
      window.chrome.webview.addEventListener('message', handleWebMessage);
    }

    const interval = setInterval(() => {
      fetchDbConversations();
      if (activeConvId) fetchThreadMessages(activeConvId);
    }, 3000);

    return () => {
      if (window.chrome?.webview?.removeEventListener) {
        window.chrome.webview.removeEventListener('message', handleWebMessage);
      }
      clearInterval(interval);
    };
  }, [activeConvId]);

  // ── Helper: Extract URL for Link Previews ──────────────────────────────────
  const extractUrl = (text) => {
    if (!text) return null;
    const match = text.match(/(https?:\/\/[^\s]+)/i);
    return match ? match[0] : null;
  };

  useEffect(() => {
    const url = extractUrl(inputText);
    if (!url) {
      setLinkPreviewData(null);
      setIsLinkPreviewDismissed(false);
      return;
    }

    if (linkDebounceTimer.current) clearTimeout(linkDebounceTimer.current);

    linkDebounceTimer.current = setTimeout(async () => {
      try {
        const res = await fetch(`${apiBase}/api/media/link-preview?url=${encodeURIComponent(url)}`);
        if (res.ok) {
          const data = await res.json();
          setLinkPreviewData(data);
        }
      } catch (err) { }
    }, 300);

    return () => {
      if (linkDebounceTimer.current) clearTimeout(linkDebounceTimer.current);
    };
  }, [inputText, apiBase]);

  // ── Send Live Typing Signal (Messenger real-time flow) ─────────────────────
  const sendTypingSignal = useCallback(async (isTyping) => {
    try {
      const targetReceiver = activeConvId;
      await fetch(`${apiBase}/api/messages/typing`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ senderId: 'admin', receiverId: targetReceiver, isTyping })
      });
    } catch (e) {}
  }, [activeConvId, apiBase]);

  // ── Mention Parser & Typing Signal on Input Change ─────────────────────────
  const handleInputChange = (e) => {
    let val = e.target.value;

    if (val.includes('@Drive&Go AI') || val.includes('@DriveAndGo AI') || val.includes('@Meta AI')) {
      setHasAiMentionTag(true);
      val = val.replace(/@(Drive&Go AI|DriveAndGo AI|Meta AI)/gi, '').trimStart();
    }

    setInputText(val);

    // Persist draft message per active conversation thread
    setDrafts(prev => ({
      ...prev,
      [activeConvId]: { text: val, hasMention: hasAiMentionTag }
    }));

    // Broadcast live typing status
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    sendTypingSignal(true);
    typingTimeoutRef.current = setTimeout(() => {
      sendTypingSignal(false);
    }, 2000);

    const cursorPos = e.target.selectionStart;
    const textBeforeCursor = val.slice(0, cursorPos);
    const lastAtIndex = textBeforeCursor.lastIndexOf('@');

    if (lastAtIndex !== -1 && (lastAtIndex === 0 || /\s/.test(textBeforeCursor[lastAtIndex - 1]))) {
      const query = textBeforeCursor.slice(lastAtIndex + 1);
      if (!/\s/.test(query)) {
        setMentionQuery(query.toLowerCase());
        setMentionIndex(0);
        return;
      }
    }
    setMentionQuery(null);
  };

  const insertMention = (target) => {
    if (target.id === 'ai' || (target.name && target.name.includes('Drive&Go AI'))) {
      setHasAiMentionTag(true);
      const textarea = inputRef.current;
      const cursorPos = textarea?.selectionStart ?? inputText.length;
      const textBeforeCursor = inputText.slice(0, cursorPos);
      const lastAtIndex = textBeforeCursor.lastIndexOf('@');
      if (lastAtIndex !== -1) {
        const prefix = inputText.slice(0, lastAtIndex);
        const suffix = inputText.slice(cursorPos);
        setInputText(`${prefix}${suffix}`.trimStart());
      }
    } else {
      const textarea = inputRef.current;
      const cursorPos = textarea?.selectionStart ?? inputText.length;
      const textBeforeCursor = inputText.slice(0, cursorPos);
      const lastAtIndex = textBeforeCursor.lastIndexOf('@');
      if (lastAtIndex !== -1) {
        const prefix = inputText.slice(0, lastAtIndex);
        const suffix = inputText.slice(cursorPos);
        const newText = `${prefix}@${target.name} ${suffix}`;
        setInputText(newText);
      }
    }
    setMentionQuery(null);
    setMentionIndex(0);
  };

  // ── Web Audio API Microphone Visualizer & Voice Recorder ──────────────────
  const startRecordingVoice = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      audioChunksRef.current = [];

      const mediaRecorder = new MediaRecorder(stream);
      mediaRecorderRef.current = mediaRecorder;

      const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
      audioContextRef.current = audioCtx;
      const source = audioCtx.createMediaStreamSource(stream);
      const analyser = audioCtx.createAnalyser();
      analyser.fftSize = 64;
      source.connect(analyser);
      analyserRef.current = analyser;

      mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) audioChunksRef.current.push(e.data);
      };

      mediaRecorder.onstop = async () => {
        const audioBlob = new Blob(audioChunksRef.current, { type: 'audio/webm' });
        const audioUrl = URL.createObjectURL(audioBlob);
        
        let uploadedUrl = audioUrl;
        try {
          const formData = new FormData();
          formData.append('file', audioBlob, 'voicenote.webm');
          const upRes = await fetch(`${apiBase}/api/messages/upload`, { method: 'POST', body: formData });
          if (upRes.ok) {
            const upData = await upRes.json();
            if (upData.url) uploadedUrl = upData.url;
          }
        } catch (e) {}

        const targetReceiver = activeConvId;

        try {
          await fetch(`${apiBase}/api/messages`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              senderId: 'admin',
              receiverId: targetReceiver,
              messageBody: '🎙️ Voice Note',
              isGroupChat: activeConv.isGroup,
              mediaType: 'audio',
              mediaUrl: uploadedUrl
            })
          });
          fetchThreadMessages(activeConvId);
        } catch (e) {}

        stream.getTracks().forEach(t => t.stop());
        if (audioCtx.state !== 'closed') audioCtx.close();
        if (animFrameIdRef.current) cancelAnimationFrame(animFrameIdRef.current);
      };

      mediaRecorder.start();
      setIsRecording(true);
      setRecordingSeconds(0);

      timerIntervalRef.current = setInterval(() => {
        setRecordingSeconds(prev => prev + 1);
      }, 1000);

      drawVisualizer();
    } catch (err) {
      alert("Microphone access is required for voice recording.");
    }
  };

  const stopRecordingVoice = () => {
    if (mediaRecorderRef.current && isRecording) {
      mediaRecorderRef.current.stop();
      setIsRecording(false);
      if (timerIntervalRef.current) clearInterval(timerIntervalRef.current);
    }
  };

  const drawVisualizer = () => {
    if (!analyserRef.current || !visualizerCanvasRef.current) return;
    const canvas = visualizerCanvasRef.current;
    const ctx = canvas.getContext('2d');
    const bufferLength = analyserRef.current.frequencyBinCount;
    const dataArray = new Uint8Array(bufferLength);

    const render = () => {
      if (!isRecording) return;
      animFrameIdRef.current = requestAnimationFrame(render);
      analyserRef.current.getByteFrequencyData(dataArray);

      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const barWidth = (canvas.width / bufferLength) * 2;
      let x = 0;

      for (let i = 0; i < bufferLength; i++) {
        const barHeight = (dataArray[i] / 255) * canvas.height;
        ctx.fillStyle = '#ea580c';
        ctx.fillRect(x, canvas.height - barHeight, barWidth - 1, barHeight);
        x += barWidth;
      }
    };
    render();
  };

  // ── PILLAR 10: Double-RAF Scroll Engine & Layout Shift Prevention ──────────
  const scrollToBottom = useCallback((instant = true) => {
    requestAnimationFrame(() => {
      if (chatScrollContainerRef.current) {
        const el = chatScrollContainerRef.current;
        el.scrollTop = el.scrollHeight + 99999;
      }
      if (messageEndRef.current) {
        try {
          messageEndRef.current.scrollIntoView(instant ? false : { behavior: 'smooth', block: 'end' });
        } catch (e) {
          if (messageEndRef.current?.scrollIntoViewIfNeeded) {
            messageEndRef.current.scrollIntoViewIfNeeded(false);
          }
        }
      }
    });
  }, []);

  const lastMsgCountRef = useRef(0);

  useEffect(() => {
    const isNewConv = scrollPrevConvIdRef.current !== activeConvId;
    const isNewMsgAdded = activeMessages.length > lastMsgCountRef.current;

    scrollPrevConvIdRef.current = activeConvId;
    lastMsgCountRef.current = activeMessages.length;

    if (isNewConv) {
      isUserScrolledUp.current = false;
      scrollToBottom(true);
    } else if (isNewMsgAdded || isAiLoading || !isUserScrolledUp.current) {
      scrollToBottom(false);
    }
  }, [activeConvId, activeMessages.length, isAiLoading, scrollToBottom]);

  // ResizeObserver + Image Load listener: Prevents layout jumping when images download & paint
  useEffect(() => {
    const el = chatScrollContainerRef.current;
    if (!el) return;

    let resizeObserver = null;
    if (typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver(() => {
        if (!isUserScrolledUp.current) {
          scrollToBottom(true);
        }
      });
      resizeObserver.observe(el);
    }

    const handleMediaLoaded = () => {
      if (!isUserScrolledUp.current) {
        scrollToBottom(true);
      }
    };

    // Event capture phase to detect all inner image & video loads
    el.addEventListener('load', handleMediaLoaded, true);

    return () => {
      if (resizeObserver) resizeObserver.disconnect();
      el.removeEventListener('load', handleMediaLoaded, true);
    };
  }, [activeConvId, scrollToBottom]);

  // Native Context Menu Handler: explicitly allow right-click context menu on images/media
  useEffect(() => {
    const handleNativeContextMenu = (e) => {
      const isMediaTarget = e.target.tagName === 'IMG' || e.target.tagName === 'VIDEO' || e.target.closest('.gub-img-bubble');
      if (isMediaTarget) {
        return true;
      }
    };
    document.addEventListener('contextmenu', handleNativeContextMenu, false);
    return () => document.removeEventListener('contextmenu', handleNativeContextMenu, false);
  }, []);

  // ── Fullscreen & Layout Mode Handlers ─────────────────────────────────────
  const handleSetViewMode = (mode) => {
    setViewMode(mode);
    if (mode === 'floating') {
      setIsSidebarCollapsed(true);
    } else {
      setIsSidebarCollapsed(false);
    }
    const fs = mode === 'fullscreen';
    setIsFullscreen(fs);
    if (window.chrome?.webview?.postMessage) {
      window.chrome.webview.postMessage(JSON.stringify({
        action: 'setLayoutMode',
        mode: mode,
        isFullscreen: fs
      }));
    }
  };

  const cycleViewMode = () => {
    let nextMode = 'floating';
    if (viewMode === 'floating') nextMode = 'split';
    else if (viewMode === 'split') nextMode = 'fullscreen';
    else if (viewMode === 'fullscreen') nextMode = 'floating';
    handleSetViewMode(nextMode);
  };

  const handleToggleFullscreen = () => {
    const nextState = !isFullscreen;
    handleSetViewMode(nextState ? 'fullscreen' : 'floating');
  };

  // ── Send Message Handler (DATABASE API CONNECTED) ──────────────────────────
  const handleSend = async () => {
    if (editingMessage) {
      await saveEditedMessage();
      return;
    }
    if (!inputText.trim() && !attachedMedia && !hasAiMentionTag) return;

    const now = Date.now();
    if (isSendingRef.current || (now - lastSendTimeRef.current < 450)) {
      return;
    }
    isSendingRef.current = true;
    lastSendTimeRef.current = now;

    try {
    const isAiTagged = hasAiMentionTag || inputText.includes('@Drive&Go AI') || inputText.includes('@DriveAndGo AI') || inputText.includes('@Meta AI');
    const cleanUserText = inputText.replace(/@(Drive&Go AI|DriveAndGo AI|Meta AI)/gi, '').trim();
    const msgText = (isAiTagged ? '@Drive&Go AI ' : '') + cleanUserText;
    const nowStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const newMsgId = Date.now();
    const targetReceiver = activeConvId;

    const mediaMetaObj = attachedMedia ? { ...(attachedMedia.metadata || {}) } : {};
    if (replyingTo) {
      mediaMetaObj.replyToId = replyingTo.id;
      mediaMetaObj.replyToSender = replyingTo.sender;
      mediaMetaObj.replyToBody = replyingTo.body;
      mediaMetaObj.replyToMediaType = replyingTo.mediaType;
    }
    const finalMediaMetaStr = Object.keys(mediaMetaObj).length > 0 ? JSON.stringify(mediaMetaObj) : null;

    const isAiTarget = activeConvId === 'ai_copilot' || isAiTagged;
    const isAiBusy = isAiLoading || isProcessingQueueRef.current;

    setInputText('');
    setHasAiMentionTag(false);
    setDrafts(prev => {
      const next = { ...prev };
      delete next[activeConvId];
      return next;
    });
    setAttachedMedia(null);
    setLinkPreviewData(null);
    setIsLinkPreviewDismissed(false);
    setReplyingTo(null); // Clear reply state after send

    if (isAiTarget) {
      // Post user message bubble immediately to DB & UI so it never lags or disappears
      postUserMessageToDbAndUi(msgText, activeConvId, activeConv.isGroup, replyingTo, attachedMedia, finalMediaMetaStr, newMsgId, nowStr);
      setAiLoadingConvoId(activeConvId);
      enqueueAiRequest({
        id: Date.now(),
        text: msgText,
        convId: activeConvId,
        isGroup: activeConv.isGroup,
        isCopilot: activeConvId === 'ai_copilot',
        shouldPostUserMsg: false,
        replyingTo
      });
    } else {
      // Regular non-AI message: post immediately
      await postUserMessageToDbAndUi(msgText, activeConvId, activeConv.isGroup, replyingTo, attachedMedia, finalMediaMetaStr, newMsgId, nowStr);
    }
    } finally {
      setTimeout(() => {
        isSendingRef.current = false;
      }, 350);
    }
  };

  // ── Helper to Post User Message Bubble to Database & UI ───────────────────
  const postUserMessageToDbAndUi = async (msgText, convId, isGroup = false, replyCtx = null, media = null, mediaMetaStr = null, msgId = Date.now(), timeStr = null) => {
    const nowStr = timeStr || new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    const newMsg = {
      id: msgId,
      messageId: msgId,
      senderId: 'admin',
      receiverId: convId,
      sender: 'Admin',
      senderName: 'Admin',
      body: msgText,
      messageBody: msgText,
      isMine: true,
      time: nowStr,
      status: 'sending',
      media: media,
      reactions: {},
      mediaMetadata: mediaMetaStr,
      replyToId: replyCtx?.id || null,
      replyToSender: replyCtx?.sender || null,
      replyToBody: replyCtx?.body || null,
      replyToMediaType: replyCtx?.mediaType || null,
    };

    setMessagesByConv(prev => ({
      ...prev,
      [convId]: [...(prev[convId] || []), newMsg]
    }));

    try {
      const res = await fetch(`${apiBase}/api/messages`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          senderId: 'admin',
          receiverId: convId,
          messageBody: msgText,
          isGroupChat: !!isGroup,
          mediaType: media ? media.type : null,
          mediaUrl: media ? media.url : null,
          mediaMetadata: mediaMetaStr,
          replyToId: replyCtx?.id || null,
          replyToSender: replyCtx?.sender || null,
          replyToBody: replyCtx?.body || null,
          replyToMediaType: replyCtx?.mediaType || null,
        })
      });

      if (res.ok) {
        const resData = await res.json();
        const realId = resData.messageId || resData.MessageId;
        updateMessageLocally(msgId, m => ({
          ...m,
          id: realId || m.id,
          messageId: realId || m.messageId,
          status: 'delivered',
          deliveryStatus: 'delivered'
        }));
      }

      await fetchThreadMessages(convId);
      fetchDbConversations();
    } catch (err) {
      console.warn("[ChatOverlay] Post message to DB API failed:", err);
      updateMessageLocally(msgId, m => ({ ...m, status: 'failed' }));
    }
  };

  // ── Antigravity-Style AI Sequential Queue Worker ────────────────────────────
  const enqueueAiRequest = (reqObj) => {
    // Drop identical prompt if already queued for this conversation
    const isDuplicate = aiQueueRef.current.some(item => 
      item.convId === reqObj.convId && 
      item.text.trim().toLowerCase() === reqObj.text.trim().toLowerCase()
    );
    if (isDuplicate) {
      console.log("[ChatOverlay] Dropped duplicate AI prompt:", reqObj.text);
      return;
    }

    const nextQueue = [...aiQueueRef.current, reqObj];
    updateAiQueue(nextQueue);
    processNextAiQueueItem();
  };

  const processNextAiQueueItem = async () => {
    if (isProcessingQueueRef.current) return;
    if (aiQueueRef.current.length === 0) return;

    isProcessingQueueRef.current = true;
    const item = aiQueueRef.current[0];

    // Dequeue active working item immediately so queue only shows pending waiting items
    updateAiQueue(aiQueueRef.current.slice(1));

    // Abort if item was deleted/canceled by user
    if (cancelledQueueItemIdsRef.current.has(item.id)) {
      cancelledQueueItemIdsRef.current.delete(item.id);
      isProcessingQueueRef.current = false;
      if (aiQueueRef.current.length > 0) {
        setTimeout(() => processNextAiQueueItem(), 100);
      }
      return;
    }

    try {
      // 1. If this queued message was held, post the user message bubble to DB & UI NOW as its turn starts!
      if (item.shouldPostUserMsg) {
        if (cancelledQueueItemIdsRef.current.has(item.id)) return;
        await postUserMessageToDbAndUi(item.text, item.convId, item.isGroup, item.replyingTo);
      }

      // Set AI thinking state AFTER the user message is posted so the UI makes sense (User bubble appears, then AI thinking bubble).
      setAiLoadingConvoId(item.convId);

      // 2. Query AI backend
      if (item.isCopilot) {
        await handleSendAiMessageInternal(item.text);
      } else {
        await handleSendInChatMentionInternal(item.text, item.convId, item.isGroup);
      }
    } catch (err) {
      console.warn("[ChatOverlay] Queue processing error:", err);
    } finally {
      isProcessingQueueRef.current = false;
      // Process remaining queued items immediately
      if (aiQueueRef.current.length > 0) {
        processNextAiQueueItem();
      }
    }
  };

  const handleRemoveQueuedItem = (itemId) => {
    cancelledQueueItemIdsRef.current.add(itemId);
    updateAiQueue(aiQueueRef.current.filter(item => item.id !== itemId));
  };

  // ── In-Chat Mention Sender (/api/messages/mention-ai) ──────────────────────
  const handleSendInChatMentionInternal = async (userPrompt, convId, isGroup = false) => {
    setAiLoadingConvoId(convId);

    try {
      const res = await fetch(`${apiBase}/api/messages/mention-ai`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          conversationId: convId,
          senderId: 'admin',
          userPrompt: userPrompt,
          isGroupChat: !!isGroup
        })
      });

      if (res.ok) {
        const resData = await res.json();
        const aiMsgObj = {
          id: resData.messageId || Date.now(),
          messageId: resData.messageId || Date.now(),
          senderId: '@Drive&Go AI',
          receiverId: convId,
          senderName: 'Drive&Go AI',
          sender: 'Drive&Go AI',
          body: resData.messageBody || resData.text || '',
          messageBody: resData.messageBody || resData.text || '',
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          isMine: false,
          replyToSender: 'you',
          replyToBody: userPrompt
        };

        setMessagesByConv(prev => {
          const threadMsgs = prev[convId] || [];
          const updatedThread = threadMsgs.map(m => {
            if (m.isMine || m.senderId === 'admin' || m.sender === 'Admin') {
              return { ...m, deliveryStatus: 'seen', status: 'seen', isSeen: true };
            }
            return m;
          });
          return {
            ...prev,
            [convId]: [...updatedThread.filter(m => (m.id || m.messageId) !== aiMsgObj.id), aiMsgObj]
          };
        });
        fetchThreadMessages(convId);
        fetchDbConversations();
        if (window.chrome?.webview?.postMessage) {
          window.chrome.webview.postMessage(JSON.stringify({ action: 'playAiSound' }));
        }
      } else {
        const errorMsgObj = {
          id: Date.now(),
          messageId: Date.now(),
          senderId: '@Drive&Go AI',
          receiverId: convId,
          senderName: 'Drive&Go AI',
          sender: 'Drive&Go AI',
          body: 'Sorry, I am currently unavailable. Please try again later.',
          messageBody: 'Sorry, I am currently unavailable. Please try again later.',
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          isMine: false,
          replyToSender: 'you',
          replyToBody: userPrompt
        };
        setMessagesByConv(prev => {
          const threadMsgs = prev[convId] || [];
          return { ...prev, [convId]: [...threadMsgs, errorMsgObj] };
        });
        fetchThreadMessages(convId);
      }
    } catch (err) {
      console.warn("[ChatOverlay] Mention AI error:", err);
      const errorMsgObj = {
        id: Date.now(),
        messageId: Date.now(),
        senderId: '@Drive&Go AI',
        receiverId: convId,
        senderName: 'Drive&Go AI',
        sender: 'Drive&Go AI',
        body: 'Sorry, I am currently unavailable. Please try again later.',
        messageBody: 'Sorry, I am currently unavailable. Please try again later.',
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        isMine: false,
        replyToSender: 'you',
        replyToBody: userPrompt
      };
      setMessagesByConv(prev => {
        const threadMsgs = prev[convId] || [];
        return { ...prev, [convId]: [...threadMsgs, errorMsgObj] };
      });
      fetchThreadMessages(convId);
    } finally {
      setAiLoadingConvoId(prev => (prev === convId ? null : prev));
    }
  };

  // ── AI Message Sender (/api/ai/chat) ───────────────────────────────────────
  const handleSendAiMessageInternal = async (userMessage) => {
    setAiLoadingConvoId('ai_copilot');

    try {
      const res = await fetch(`${apiBase}/api/ai/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: aiSessionId || 1,
          adminUserId: 1,
          userMessage: userMessage.replace('@Drive&Go AI', '').replace('@Meta AI', '').trim()
        })
      });

      if (res.ok) {
        const resData = await res.json();
        const aiMsgObj = {
          id: resData.messageId || Date.now(),
          messageId: resData.messageId || Date.now(),
          senderId: '@Drive&Go AI',
          receiverId: 'ai_copilot',
          senderName: 'Drive&Go AI',
          sender: 'Drive&Go AI',
          body: resData.text || resData.messageBody || '',
          messageBody: resData.text || resData.messageBody || '',
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          isMine: false,
          replyToSender: 'you',
          replyToBody: userMessage
        };

        setMessagesByConv(prev => {
          const threadMsgs = prev.ai_copilot || [];
          const updatedThread = threadMsgs.map(m => {
            if (m.isMine || m.senderId === 'admin' || m.sender === 'Admin') {
              return { ...m, deliveryStatus: 'seen', status: 'seen', isSeen: true };
            }
            return m;
          });
          return {
            ...prev,
            ai_copilot: [...updatedThread.filter(m => (m.id || m.messageId) !== aiMsgObj.id), aiMsgObj]
          };
        });
        fetchThreadMessages('ai_copilot');
        fetchDbConversations();
        if (window.chrome?.webview?.postMessage) {
          window.chrome.webview.postMessage(JSON.stringify({ action: 'playAiSound' }));
        }
      } else {
        const errorMsgObj = {
          id: Date.now(),
          messageId: Date.now(),
          senderId: '@Drive&Go AI',
          receiverId: 'ai_copilot',
          senderName: 'Drive&Go AI',
          sender: 'Drive&Go AI',
          body: 'Sorry, I am currently unavailable. Please try again later.',
          messageBody: 'Sorry, I am currently unavailable. Please try again later.',
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          isMine: false,
          replyToSender: 'you',
          replyToBody: userMessage
        };
        setMessagesByConv(prev => {
          const threadMsgs = prev.ai_copilot || [];
          return { ...prev, ai_copilot: [...threadMsgs, errorMsgObj] };
        });
        fetchThreadMessages('ai_copilot');
      }
    } catch (err) {
      console.warn("[ChatOverlay] AI Chat warning:", err);
      const errorMsgObj = {
        id: Date.now(),
        messageId: Date.now(),
        senderId: '@Drive&Go AI',
        receiverId: 'ai_copilot',
        senderName: 'Drive&Go AI',
        sender: 'Drive&Go AI',
        body: 'Sorry, I am currently unavailable. Please try again later.',
        messageBody: 'Sorry, I am currently unavailable. Please try again later.',
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        isMine: false,
        replyToSender: 'you',
        replyToBody: userMessage
      };
      setMessagesByConv(prev => {
        const threadMsgs = prev.ai_copilot || [];
        return { ...prev, ai_copilot: [...threadMsgs, errorMsgObj] };
      });
      fetchThreadMessages('ai_copilot');
    } finally {
      setAiLoadingConvoId(prev => (prev === 'ai_copilot' ? null : prev));
    }
  };

  // ── PILLAR 02 & CONTEXT MENU: Global Message Action Handlers ──────────────────
  useEffect(() => {
    const handleReplyEvent = (e) => {
      const msg = e.detail;
      if (msg) {
        setReplyingTo({
          id: msg.id || msg.messageId,
          sender: msg.sender || msg.senderName || 'Unknown',
          body: msg.body || msg.messageBody || '',
          mediaType: msg.mediaType || msg.media_type || null
        });
      }
    };
    const handleEditEvent = (e) => {
      const msg = e.detail;
      if (msg) {
        startEditMessage(msg);
      }
    };
    const handleUnsendEvent = (e) => {
      const msg = e.detail;
      if (msg) {
        closePopups();
        setSystemModal({ type: 'unsend', message: msg });
      }
    };
    const handleForwardEvent = (e) => {
      const msg = e.detail;
      if (msg) {
        closePopups();
        setSystemModal({ type: 'forward', message: msg });
      }
    };
    const handlePinEvent = (e) => {
      const msg = e.detail;
      if (msg) {
        setPinnedMessage(prev => (prev?.id === msg.id || prev?.messageId === msg.messageId) ? null : msg);
      }
    };

    const handleLightboxEvent = (e) => {
      const media = e.detail;
      if (media && media.url) {
        setLightboxMedia(media);
      }
    };

    const handleReactEvent = (e) => {
      const { msg, emoji } = e.detail || {};
      if (msg) handleReactToMessage(msg, emoji);
    };

    window.addEventListener('chat:replyTo', handleReplyEvent);
    window.addEventListener('chat:editMessage', handleEditEvent);
    window.addEventListener('chat:unsendMessage', handleUnsendEvent);
    window.addEventListener('chat:forwardMessage', handleForwardEvent);
    window.addEventListener('chat:pinMessage', handlePinEvent);
    window.addEventListener('chat:openLightbox', handleLightboxEvent);
    window.addEventListener('chat:reactToMessage', handleReactEvent);

    return () => {
      window.removeEventListener('chat:replyTo', handleReplyEvent);
      window.removeEventListener('chat:editMessage', handleEditEvent);
      window.removeEventListener('chat:unsendMessage', handleUnsendEvent);
      window.removeEventListener('chat:forwardMessage', handleForwardEvent);
      window.removeEventListener('chat:pinMessage', handlePinEvent);
      window.removeEventListener('chat:openLightbox', handleLightboxEvent);
      window.removeEventListener('chat:reactToMessage', handleReactEvent);
    };
  }, []);

  // ── Filtered Conversations ─────────────────────────────────────────────────
  const filteredConversations = React.useMemo(() => {
    const q = searchQuery.trim().toLowerCase();

    // 1. If NO search query:
    // Show only real conversations that have messages, active groups, or AI Copilot.
    // Zero-message users stay hidden from default inbox list.
    if (!q) {
      return conversations.filter(c => {
        if (c.hasNoMessages) return false;
        if (activeTabFilter === 'unread') return (c.unreadCount || 0) > 0;
        if (activeTabFilter === 'groups') return c.isGroup;
        if (activeTabFilter === 'ai') return c.id === 'ai_copilot';
        return true;
      });
    }

    // 2. If SEARCHING:
    // Search across active conversations AND all registered DB contacts (customers/drivers)
    const seenIds = new Set();
    const result = [];

    conversations.forEach(c => {
      const matches = (c.name && c.name.toLowerCase().includes(q)) ||
                      (c.role && c.role.toLowerCase().includes(q)) ||
                      (c.lastMessage && c.lastMessage.toLowerCase().includes(q)) ||
                      (c.status && c.status.toLowerCase().includes(q)) ||
                      (c.id && String(c.id).toLowerCase().includes(q)) ||
                      (c.email && c.email.toLowerCase().includes(q)) ||
                      (c.phone && c.phone.toLowerCase().includes(q));
      if (matches) {
        seenIds.add(String(c.id));
        result.push(c);
      }
    });

    allContacts.forEach(c => {
      if (!seenIds.has(String(c.id))) {
        const matches = (c.name && c.name.toLowerCase().includes(q)) ||
                        (c.role && c.role.toLowerCase().includes(q)) ||
                        (c.email && c.email.toLowerCase().includes(q)) ||
                        (c.phone && c.phone.toLowerCase().includes(q));
        if (matches) {
          seenIds.add(String(c.id));
          result.push(c);
        }
      }
    });

    if (activeTabFilter === 'unread') return result.filter(c => (c.unreadCount || 0) > 0);
    if (activeTabFilter === 'groups') return result.filter(c => c.isGroup);
    if (activeTabFilter === 'ai') return result.filter(c => c.id === 'ai_copilot');
    return result;
  }, [conversations, allContacts, searchQuery, activeTabFilter]);

  // ── PILLAR 01: Online Status Helper ───────────────────────────────────────
  const getOnlineIndicator = (conv) => {
    if (conv.id === 'ai_copilot') {
      return (
        <span className="text-[9px] font-bold text-amber-400 bg-amber-400/10 border border-amber-400/30 rounded-full px-1.5 py-0.5 flex items-center gap-1">
          ✨ Active
        </span>
      );
    }
    if (conv.isGroup) {
      return (
        <span className="text-[9px] font-bold text-emerald-400 bg-emerald-400/10 border border-emerald-400/30 rounded-full px-1.5 py-0.5 flex items-center gap-1">
          👥 Live
        </span>
      );
    }
    if (conv.isOnline) {
      return <span className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 bg-emerald-500 border-2 border-[#07070e] rounded-full shadow-[0_0_8px_rgba(16,185,129,0.8)]" />;
    }
    // Offline — gray dot
    return <span className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 bg-slate-600 border-2 border-[#07070e] rounded-full" />;
  };

  // ── PILLAR 01: Header online dot ──────────────────────────────────────────
  const getHeaderStatusBadge = (conv) => {
    if (conv.id === 'ai_copilot') {
      return <span className="w-2 h-2 rounded-full bg-amber-400 shadow-[0_0_8px_rgba(245,158,11,0.8)]" />;
    }
    if (conv.isGroup) {
      return <span className="w-2 h-2 rounded-full bg-emerald-400 shadow-[0_0_8px_rgba(16,185,129,0.8)]" />;
    }
    if (conv.isOnline) {
      return <span className="w-2 h-2 rounded-full bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.8)]" />;
    }
    return <span className="w-2 h-2 rounded-full bg-slate-500" />;
  };

  const QUICK_REACTIONS = ['❤️', '👍', '😂', '😮', '😢', '😡'];
  const EMOJI_LIBRARY = ['❤️','👍','👎','😂','😮','😢','😡','👏','🔥','🎉','🙏','💯','✅','🚗','🛠️','⭐','💬','🤝','😍','😎','🤔','🙌','⚡'];

  const getReactionGroups = (msg) => {
    const reactions = parseReactions(msg.reactions);
    return Object.entries(reactions).reduce((acc, [userId, emoji]) => {
      if (!emoji) return acc;
      if (!acc[emoji]) acc[emoji] = [];
      acc[emoji].push(userId);
      return acc;
    }, {});
  };

  const getReactionTotal = (msg) => Object.keys(parseReactions(msg.reactions)).length;

  const displayReactionUser = (userId) => {
    if (userId === 'admin' || userId === '1' || userId === 1) return 'Raymart Quirante';
    const found = mentionTargets.find(m => m.id === userId) || conversations.find(c => c.id === userId);
    return found?.name || userId;
  };

  const getReactionUserAvatar = (uId) => {
    if (uId === 'admin' || uId === '1' || uId === 1) {
      if (adminProfilePfp) {
        return <img src={adminProfilePfp} alt="Raymart Quirante" className="w-full h-full object-cover rounded-full" />;
      }
      const adminPic = (typeof window !== 'undefined' && window.ADMIN_AVATAR && window.ADMIN_AVATAR.length > 20)
        ? window.ADMIN_AVATAR
        : (typeof window !== 'undefined' ? (localStorage.getItem('admin_avatar') || localStorage.getItem('user_avatar') || localStorage.getItem('profile_picture')) : null);
      
      if (adminPic) {
        return <img src={adminPic} alt="Raymart Quirante" className="w-full h-full object-cover rounded-full" />;
      }
    }

    const target = mentionTargets.find(m => String(m.id) === String(uId)) ||
                   conversations.find(c => String(c.id) === String(uId) || (c.name && c.name.toLowerCase() === String(uId).toLowerCase()));

    if (target) {
      const pfp = target.avatarUrl || target.avatar_url || target.pfp || target.profilePicture || target.profile_picture || target.avatarBase64 || target.avatar_base64;
      if (pfp && typeof pfp === 'string' && pfp.trim().length > 0 && !pfp.includes('undefined') && !pfp.includes('null')) {
        const fullPfp = (pfp.startsWith('http') || pfp.startsWith('data:') || pfp.startsWith('blob:'))
          ? pfp
          : (apiBase + (pfp.startsWith('/') ? '' : '/') + pfp);
        return <img src={fullPfp} alt={target.name || ''} className="w-full h-full object-cover rounded-full" />;
      }
      if (target.avatar && typeof target.avatar === 'string' && target.avatar.length <= 2) {
        return <span className="text-white font-bold">{target.avatar}</span>;
      }
    }

    const name = displayReactionUser(uId);
    return <span className="text-white font-bold">{(name || 'U')[0].toUpperCase()}</span>;
  };

  const MessageActionShell = ({ msg, children }) => {
    const isMine = !!msg.isMine;
    const isUnsent = !!(msg.isUnsent || msg.is_unsent);
    const groups = getReactionGroups(msg);
    const total = getReactionTotal(msg);
    const previewEmojis = Object.keys(groups).slice(0, 3);

    return (
      <div className={`group/msg relative flex w-full ${isMine ? 'justify-end' : 'justify-start'}`}>
        <div className="relative max-w-full">
          {!isUnsent && (
            <div
              className={`absolute top-1/2 -translate-y-1/2 z-40 hidden group-hover/msg:flex items-center gap-1 rounded-full bg-[#242526]/95 border border-white/10 shadow-2xl px-1.5 py-1 ${
                isMine ? 'right-full mr-2 flex-row-reverse' : 'left-full ml-2'
              }`}
            >
              <button title="React" onClick={(e) => openAnchoredPopup('reactions', msg, e)} className="messenger-icon-btn">☺</button>
              <button title="Reply" onClick={() => window.dispatchEvent(new CustomEvent('chat:replyTo', { detail: msg }))} className="messenger-icon-btn">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 17 4 12 9 7"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/></svg>
              </button>
              <button title="More" onClick={(e) => openAnchoredPopup('context', msg, e)} className="messenger-icon-btn">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor"><circle cx="5" cy="12" r="2"/><circle cx="12" cy="12" r="2"/><circle cx="19" cy="12" r="2"/></svg>
              </button>
            </div>
          )}

          {children}

          {!isUnsent && total > 0 && (
            <button
              onClick={() => setSystemModal({ type: 'reactions', message: msg })}
              className={`absolute -bottom-2 z-30 flex items-center gap-1 rounded-full bg-[#242526] border border-white/15 px-1.5 py-0.5 text-[11px] shadow-xl hover:brightness-125 active:scale-95 transition-all ${
                isMine ? 'right-2' : 'left-9'
              }`}
              title="Message reactions"
            >
              <span className="font-emoji">{previewEmojis.join('')}</span>
              <span className="text-slate-200 font-bold">{total}</span>
            </button>
          )}
        </div>
      </div>
    );
  };

  const EmojiPickerPopup = ({ msg, popupStyle }) => {
    const [emojiSearch, setEmojiSearch] = useState('');
    const filtered = EMOJI_LIBRARY.filter(e => e.includes(emojiSearch.trim()) || !emojiSearch.trim());
    return (
      <Portal>
        <div className="fixed inset-0 z-[99970]" onClick={closePopups} />
        <div style={popupStyle} className="w-72 rounded-2xl bg-[#242526]/98 border border-white/10 shadow-2xl p-3 animate-fadeIn">
          <input
            autoFocus
            value={emojiSearch}
            onChange={(e) => setEmojiSearch(e.target.value)}
            placeholder="Search emoji"
            className="w-full bg-[#3a3b3c] border border-white/10 rounded-xl px-3 py-2 text-xs text-white outline-none mb-3"
          />
          <div className="grid grid-cols-8 gap-1 max-h-44 overflow-y-auto">
            {filtered.map((emoji, i) => (
              <button key={`${emoji}-${i}`} onClick={() => handleReactToMessage(msg, emoji)} className="w-8 h-8 rounded-lg hover:bg-white/10 text-lg font-emoji">
                {emoji}
              </button>
            ))}
          </div>
        </div>
      </Portal>
    );
  };



  const PopupLayer = () => {
    if (!activePopup) return null;
    const rect = activePopup.rect || { left: window.innerWidth / 2, top: window.innerHeight / 2, right: window.innerWidth / 2, bottom: window.innerHeight / 2 };
    const msg = activePopup.message;
    const isMine = !!msg?.isMine;
    const baseStyle = {
      position: 'fixed',
      zIndex: 99980,
      left: Math.min(Math.max((isMine ? rect.left - 8 : rect.right + 8), 12), window.innerWidth - 280),
      top: Math.min(Math.max(rect.top - 8, 12), window.innerHeight - 360)
    };

    if (activePopup.type === 'reactions') {
      return (
        <Portal>
          <div className="fixed inset-0 z-[99970]" onClick={closePopups} />
          <div style={baseStyle} className="rounded-full bg-[#242526]/95 border border-white/10 shadow-2xl px-2 py-1.5 flex items-center gap-1 animate-fadeIn">
            {QUICK_REACTIONS.map(emoji => (
              <button key={emoji} onClick={() => handleReactToMessage(msg, emoji)} className="w-9 h-9 rounded-full hover:bg-white/10 hover:scale-125 active:scale-95 transition-all text-xl font-emoji">
                {emoji}
              </button>
            ))}
          </div>
        </Portal>
      );
    }

    if (activePopup.type === 'emojiPicker') {
      return <EmojiPickerPopup msg={msg} popupStyle={baseStyle} />;
    }

    const menuItems = [
      { label: 'Reply', action: () => window.dispatchEvent(new CustomEvent('chat:replyTo', { detail: msg })) },
      { label: 'React', action: () => setActivePopup({ type: 'reactions', message: msg, rect }) },
      ...(isMine ? [{ label: 'Edit', action: () => startEditMessage(msg) }] : []),
      { label: isMine ? 'Unsend' : 'Remove', danger: true, action: () => { closePopups(); setUnsendScope(isMine ? 'everyone' : 'you'); setSystemModal({ type: 'unsend', message: msg }); } },
      { label: 'Forward', action: () => { closePopups(); setSystemModal({ type: 'forward', message: msg }); } },
      { label: 'Pin', action: () => handlePinOrReport(msg, 'pin') },
      { label: 'Report', danger: true, action: () => handlePinOrReport(msg, 'report') }
    ];

    return (
      <Portal>
        <div className="fixed inset-0 z-[99970]" onClick={closePopups} />
        <div style={baseStyle} className="w-52 overflow-hidden rounded-xl bg-[#242526]/98 border border-white/10 shadow-2xl py-1 animate-fadeIn">
          {menuItems.map(item => (
            <button
              key={item.label}
              onClick={() => { item.action(); if (!['React'].includes(item.label)) closePopups(); }}
              className={`w-full px-4 py-2.5 text-left text-sm font-semibold hover:bg-white/10 transition-colors ${item.danger ? 'text-red-300' : 'text-slate-100'}`}
            >
              {item.label}
            </button>
          ))}
        </div>
      </Portal>
    );
  };

  const MentionPopup = () => {
    if (mentionQuery === null || currentMentionOptions.length === 0) return null;
    const rect = inputShellRef.current?.getBoundingClientRect?.();
    const left = rect ? Math.max(12, rect.left) : 24;
    const bottom = rect ? Math.max(12, window.innerHeight - rect.top + 8) : 92;
    return (
      <Portal>
        <div
          className="fixed w-72 max-h-80 overflow-hidden rounded-2xl bg-[#242526]/98 border border-white/10 shadow-2xl backdrop-blur-xl animate-fadeIn"
          style={{ left, bottom, zIndex: 99990 }}
        >
          <div className="px-3 py-2 border-b border-white/10 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
            Mention someone
          </div>
          <div className="max-h-64 overflow-y-auto py-1">
            {currentMentionOptions.map((target, idx) => (
              <button
                key={target.id}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => insertMention(target)}
                className={`w-full text-left px-3 py-2.5 flex items-center gap-3 transition-all ${idx === mentionIndex ? 'bg-orange-500/25 text-orange-200' : 'hover:bg-white/10 text-slate-100'}`}
              >
                <span className="w-9 h-9 rounded-full bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center text-sm font-extrabold text-white shrink-0 font-emoji">{target.avatar}</span>
                <span className="flex flex-col min-w-0">
                  <span className="text-sm font-bold truncate">{target.name}</span>
                  <span className="text-[10px] text-slate-400 truncate">{target.role}</span>
                </span>
              </button>
            ))}
          </div>
        </div>
      </Portal>
    );
  };



  // ── PILLAR 08: Messenger 3rd Pane: Right Chat Info Sidebar ──────────────────
const handleGroupAvatarChange = async (e) => {
      const file = e.target.files?.[0];
      if (!file) return;

      // 1. Client-Side Image Type Validation
      if (file.type && !file.type.startsWith('image/')) {
        setUploadErrorModal('Invalid file type selected. Please select an image file (JPG, PNG, WEBP, GIF).');
        return;
      }

      // 2. Client-Side File Size Limit (Max 35MB)
      if (file.size > 35 * 1024 * 1024) {
        setUploadErrorModal(`File size is too large (${(file.size / (1024 * 1024)).toFixed(1)} MB). Maximum allowed image size is 35 MB.`);
        return;
      }

      setIsUploading(true);
      setUploadStatus('uploading');
      setUploadProgress(15);
      setUploadError(null);

      // Instant local preview via FileReader
      try {
        const reader = new FileReader();
        reader.onload = (event) => {
          const dataUrl = event.target.result;
          setConversations(prev => prev.map(c => 
            (c.id === activeConv.id) 
              ? { ...c, avatarUrl: dataUrl, pfp: dataUrl } 
              : c
          ));
        };
        reader.readAsDataURL(file);
      } catch (err) {}

      let currentProgress = 20;
      const progressTimer = setInterval(() => {
        currentProgress += 15;
        if (currentProgress <= 85) {
          setUploadProgress(currentProgress);
        }
      }, 120);

      try {
        const formData = new FormData();
        formData.append('file', file);

        const targetId = activeConv.id;
        const res = await fetch(`${apiBase}/api/messages/groups/${encodeURIComponent(targetId)}/avatar`, {
          method: 'POST',
          body: formData
        });

        clearInterval(progressTimer);

        if (res.ok) {
          const data = await res.json();
          const finalUrl = data.url || data.avatarUrl;

          setUploadProgress(100);
          setUploadStatus('success');

          if (finalUrl) {
            setConversations(prev => prev.map(c => 
              (c.id === activeConv.id) 
                ? { ...c, avatarUrl: finalUrl, pfp: finalUrl } 
                : c
            ));
          }
          showToast('Group profile picture updated successfully! ✨');

          setTimeout(() => {
            setUploadStatus('idle');
            setUploadProgress(0);
            setIsUploading(false);
          }, 1800);
        } else {
          let errText = `Server error (Status ${res.status})`;
          try {
            const errData = await res.json();
            if (errData && errData.message) errText = errData.message;
          } catch (err) {}
          throw new Error(errText);
        }
      } catch (err) {
        clearInterval(progressTimer);
        console.error("[ChatOverlay] Group avatar upload error:", err);
        setUploadStatus('error');
        setUploadError(err.message || 'Upload error');
        setUploadErrorModal(`Failed to upload group profile picture: ${err.message || 'Server connection error'}`);
        showToast(`Upload error: ${err.message || 'Failed to update avatar'}`);

        setTimeout(() => {
          setUploadStatus('idle');
          setUploadProgress(0);
          setIsUploading(false);
        }, 2000);
      }
    };



  return (
    <>

      {/* Hidden File Input Triggered Programmatically via fileInputRef */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        className="hidden"
        onChange={handleGroupAvatarChange}
        onClick={(e) => { e.target.value = null; }}
      />

      {/* Immediate High-Priority Red Error Alert Modal Popup */}
      {uploadErrorModal && (
        <div className="fixed inset-0 bg-slate-950/85 backdrop-blur-md flex items-center justify-center z-[9999] p-5 animate-in fade-in">
          <div className="bg-[#121424] border border-red-500/50 rounded-3xl p-6 max-w-xs w-full text-center shadow-2xl space-y-4 relative">
            <div className="w-14 h-14 rounded-2xl bg-red-500/20 text-red-400 flex items-center justify-center mx-auto text-2xl border border-red-500/30 shadow-inner">
              ⚠️
            </div>
            <div>
              <h3 className="text-sm font-extrabold text-white">Upload Error</h3>
              <p className="text-[11px] text-slate-300 mt-2 leading-relaxed font-medium">{uploadErrorModal}</p>
            </div>
            <button
              onClick={() => setUploadErrorModal(null)}
              className="w-full py-2.5 rounded-xl bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-500 hover:to-rose-500 text-white font-bold text-xs transition-all shadow-lg cursor-pointer"
            >
              Dismiss & Try Again
            </button>
          </div>
        </div>
      )}

      {CommandPaletteComp && (
        <CommandPaletteComp
          isOpen={isCommandPaletteOpen}
          onClose={() => setIsCommandPaletteOpen(false)}
          onOpenAiCopilot={(q) => {
            setActiveConvId('ai_copilot');
            handleSendAiMessage(q);
          }}
        />
      )}

      <MentionPopup />

      {/* Floating Top-Right Toast Notification */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-[99999] px-4 py-2.5 rounded-xl bg-slate-900/95 border border-brand/40 text-white font-bold text-xs shadow-2xl backdrop-blur-xl animate-in slide-in-from-top-2 flex items-center gap-2.5 pointer-events-auto">
          <span className="w-2 h-2 rounded-full bg-[#FF6B00] animate-pulse"></span>
          <span>{toastMessage}</span>
        </div>
      )}

      <div className="w-full h-full flex bg-[#07070e] text-slate-100 font-sans overflow-hidden select-none">
        
        {/* ════════════════════════════════════════════════════════════════════
            STRICT RESPONSIVE LEFT SIDEBAR: Collapsible icon bar / w-56 / w-64 / w-80
           ════════════════════════════════════════════════════════════════════ */}
        <div className={`${
          isSidebarCollapsed
            ? 'w-16'
            : viewMode === 'floating'
              ? 'w-56 sm:w-64'
              : viewMode === 'split'
                ? 'w-64 sm:w-72'
                : 'w-72 sm:w-80 lg:w-[320px]'
        } h-full bg-[#0d0e1b] border-r border-white/10 flex flex-col shrink-0 overflow-hidden transition-all duration-200`}>
          
          {/* Sidebar Header */}
          <div className="p-3 sm:p-4 border-b border-white/10 flex flex-col gap-3">
            <div className="flex items-center justify-between">
              {!isSidebarCollapsed && (
                <h2 className="text-base sm:text-lg font-extrabold text-white tracking-tight flex items-center gap-2">
                  <span>Messages</span>
                </h2>
              )}
              <div className={`flex items-center gap-1.5 ${isSidebarCollapsed ? 'w-full justify-center' : 'ml-auto'}`}>
                <button
                  onClick={() => setIsSidebarCollapsed(prev => !prev)}
                  className="w-7 h-7 rounded-lg bg-white/5 hover:bg-white/10 text-slate-400 hover:text-white flex items-center justify-center transition-all cursor-pointer text-xs"
                  title={isSidebarCollapsed ? "Expand Sidebar" : "Collapse Sidebar"}
                >
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    {isSidebarCollapsed ? (
                      <path d="M13 17l5-5-5-5M6 17l5-5-5-5" />
                    ) : (
                      <path d="M11 17l-5-5 5-5M18 17l-5-5 5-5" />
                    )}
                  </svg>
                </button>
                {!isSidebarCollapsed && (
                  <button
                    onClick={() => {
                      setNewChatSearch('');
                      setShowNewChatModal(true);
                    }}
                    className="w-7 h-7 rounded-lg bg-orange-500/20 hover:bg-orange-500/30 text-orange-400 flex items-center justify-center font-bold transition-all cursor-pointer shadow-[0_0_12px_rgba(234,88,12,0.3)]"
                    title="Start New Conversation (Customer, Driver, AI)"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
                      <line x1="12" y1="5" x2="12" y2="19"/>
                      <line x1="5" y1="12" x2="19" y2="12"/>
                    </svg>
                  </button>
                )}
              </div>
            </div>

            {!isSidebarCollapsed && (
              <>
                {/* Search Box */}
                <div className="relative flex items-center">
                  <svg className="w-4 h-4 text-slate-400 absolute left-3 pointer-events-none" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                  </svg>
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search conversations, drivers, customers..."
                    className="w-full bg-slate-950/60 border border-white/10 rounded-xl pl-9 pr-8 py-1.5 text-xs text-slate-200 placeholder-slate-500 outline-none focus:border-orange-500/50 transition-all"
                  />
                  {searchQuery && (
                    <button
                      onClick={() => setSearchQuery('')}
                      className="absolute right-2.5 text-slate-400 hover:text-white text-xs font-bold"
                    >
                      ✕
                    </button>
                  )}
                </div>

                {/* Category Filter Chips */}
                <div className="flex items-center gap-1 overflow-x-auto pb-0.5 scrollbar-none text-[11px] font-medium">
                  {[
                    { id: 'all', label: 'All' },
                    { id: 'unread', label: 'Unread' },
                    { id: 'groups', label: 'Groups' },
                    { id: 'ai', label: '✨ AI Copilot' }
                  ].map(tab => (
                    <button
                      key={tab.id}
                      onClick={() => setActiveTabFilter(tab.id)}
                      className={`px-2.5 py-1 rounded-lg whitespace-nowrap transition-all cursor-pointer ${
                        activeTabFilter === tab.id
                          ? 'bg-orange-600 text-white font-bold shadow-[0_0_10px_rgba(234,88,12,0.4)]'
                          : 'bg-white/5 text-slate-400 hover:text-white hover:bg-white/10'
                      }`}
                    >
                      {tab.label}
                    </button>
                  ))}
                </div>

                {/* Quick Suggested Contacts Horizontal Row */}
                {searchQuery === '' && activeTabFilter === 'all' && allContacts.length > 0 && (
                  <div className={`pt-1.5 pb-1 border-t flex flex-col gap-1.5 animate-fadeIn ${isDark ? 'border-white/5' : 'border-slate-200/80'}`}>
                    <div className="flex items-center justify-between px-0.5">
                      <span className={`text-[9.5px] font-bold uppercase tracking-wider ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                        Suggested Contacts
                      </span>
                      <button 
                        onClick={() => {
                          setNewChatSearch('');
                          setShowNewChatModal(true);
                        }}
                        className="text-[10px] font-bold text-orange-400 hover:text-orange-300 cursor-pointer"
                      >
                        All ({allContacts.length}) &rarr;
                      </button>
                    </div>
                    <div 
                      className="flex items-center gap-2 overflow-x-auto pb-1 scrollbar-none"
                      style={{ scrollbarWidth: 'none', msOverflowStyle: 'none' }}
                    >
                      {allContacts.slice(0, 8).map(contact => {
                        return (
                          <button
                            key={contact.id}
                            onClick={() => {
                              setActiveConvId(contact.id);
                              setShowInfoPanel(false);
                              setConversations(prev => {
                                if (!prev.some(x => String(x.id) === String(contact.id))) {
                                  return [...prev, contact];
                                }
                                return prev;
                              });
                              fetchThreadMessages(contact.id);
                            }}
                            className="flex flex-col items-center gap-1 shrink-0 group cursor-pointer focus:outline-none"
                            title={`${contact.name} (${contact.role})`}
                          >
                            <div className={`w-8 h-8 rounded-xl bg-gradient-to-br ${contact.avatarBg || 'from-slate-700 to-slate-900'} flex items-center justify-center text-white font-black text-xs shadow-md overflow-hidden ring-2 ring-transparent group-hover:ring-orange-500 group-hover:scale-105 transition-all`}>
                              {renderAvatarIcon(contact) || contact.avatar || (contact.name ? contact.name[0] : '👤')}
                            </div>
                            <span className={`text-[9.5px] font-bold max-w-[44px] truncate ${isDark ? 'text-slate-300 group-hover:text-white' : 'text-slate-700 group-hover:text-orange-600'}`}>
                              {contact.name.split(' ')[0]}
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </>
            )}
          </div>

          {/* Conversation List */}
          <div className="flex-1 overflow-y-auto p-2 flex flex-col gap-1.5 chat-overlay-scrollbar">
            {filteredConversations.map(conv => {
              const isActive = conv.id === activeConvId;
              const isUnread = (conv.unreadCount || 0) > 0;

              const cardStyle = isDark
                ? (isUnread
                    ? 'bg-gradient-to-r from-orange-500/20 via-white/[0.08] to-white/[0.03] border-l-4 border-l-orange-500 border-white/20 shadow-md shadow-orange-950/20 hover:border-orange-500/80 hover:bg-white/[0.12]'
                    : (isActive
                        ? 'bg-orange-500/15 border-orange-500/30 shadow-[0_0_15px_rgba(234,88,12,0.15)]'
                        : 'bg-slate-900/40 border-white/5 hover:bg-white/5 hover:border-white/10'))
                : (isUnread
                    ? 'bg-gradient-to-r from-orange-500/15 via-orange-50/80 to-white border-l-4 border-l-orange-500 border-orange-300 shadow-md shadow-orange-100/80 hover:border-orange-500/80 hover:bg-orange-50/90'
                    : (isActive
                        ? 'bg-orange-50 border-orange-300 shadow-sm'
                        : 'bg-white/80 border-slate-200/80 hover:bg-slate-50 hover:border-slate-300'));

              return (
                <div
                  key={conv.id}
                  onClick={() => {
                    setActiveConvId(conv.id);
                    setShowInfoPanel(false);
                    setConversations(prev => {
                      if (!prev.some(x => String(x.id) === String(conv.id))) {
                        return [...prev, conv];
                      }
                      return prev;
                    });
                    fetchThreadMessages(conv.id);
                  }}
                  title={isSidebarCollapsed ? conv.name : undefined}
                  className={`group p-2.5 rounded-2xl border transition-all cursor-pointer flex items-center ${
                    isSidebarCollapsed ? 'justify-center' : 'gap-3'
                  } relative ${cardStyle}`}
                >
                  {/* Avatar + Online Dot */}
                  <div className="relative shrink-0">
                    <div className={`w-8 h-8 rounded-xl bg-gradient-to-br ${conv.avatarBg || 'from-slate-700 to-slate-900'} flex items-center justify-center text-white font-extrabold text-xs shadow-md overflow-hidden`}>
                      {renderAvatarIcon(conv)}
                    </div>
                    {(conv.isOnline && !conv.isGroup && conv.id !== 'ai_copilot') && (
                      <span className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 bg-emerald-500 border-2 border-[#07070e] rounded-full shadow-[0_0_8px_rgba(16,185,129,0.8)]" />
                    )}
                    {(!conv.isOnline && !conv.isGroup && conv.id !== 'ai_copilot') && (
                      <span className="absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 bg-slate-600 border-2 border-[#07070e] rounded-full" />
                    )}
                  </div>

                  {/* Details (Hidden when sidebar is collapsed) */}
                  {!isSidebarCollapsed && (
                    <div className="flex-1 min-w-0 flex flex-col gap-0.5">
                      <div className="flex items-center justify-between gap-1">
                        <span className={`text-xs truncate ${
                          isUnread
                            ? (isDark ? 'text-white font-black' : 'text-slate-950 font-black')
                            : (isActive 
                                ? (isDark ? 'text-orange-300 font-bold' : 'text-orange-600 font-bold')
                                : (isDark ? 'text-slate-100 font-bold' : 'text-slate-800 font-bold'))
                        }`}>
                          {conv.name}
                        </span>
                        <div className="flex items-center gap-1.5 shrink-0">
                          {/* AI/Group live badges */}
                          {conv.id === 'ai_copilot' && (
                            <span className="text-[8px] font-bold text-amber-400 bg-amber-400/10 border border-amber-400/20 rounded-full px-1.5 py-0.5">✨</span>
                          )}
                          {conv.isGroup && conv.id !== 'ai_copilot' && (
                            <span className="text-[8px] font-bold text-emerald-400 bg-emerald-400/10 border border-emerald-400/20 rounded-full px-1.5 py-0.5">GC</span>
                          )}
                          <span className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>{conv.time}</span>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <span className={`text-[9px] font-black tracking-wider uppercase px-1.5 py-0.5 rounded-md ${getRoleBadgeClasses(conv.role)}`}>
                          {conv.role}
                        </span>
                        {conv.hasNoMessages && (
                          <span className="text-[9px] font-black text-orange-400 bg-orange-500/15 border border-orange-500/25 px-1.5 py-0.5 rounded-md">
                            Start Chat
                          </span>
                        )}
                      </div>
                      <p className={`text-[11px] truncate mt-0.5 ${
                        isUnread
                          ? (isDark ? 'text-slate-100 font-bold' : 'text-slate-950 font-bold')
                          : (isDark ? 'text-slate-400' : 'text-slate-600')
                      }`}>
                        {(conv.lastMessage || 'Channel active')
                          .replace(/[\u2600-\u27BF\uD83C-\uDBFF\uDC00-\uDFFF]/g, '')
                          .replace(/\*\*(.*?)\*\*/g, '$1')
                          .replace(/\*(.*?)\*/g, '$1')
                          .replace(/^#+\s*/g, '')
                          .trim() || 'Channel active'}
                      </p>
                    </div>
                  )}

                  {/* Unread Badge */}
                  {conv.unreadCount > 0 && (
                    <span className="w-5 h-5 rounded-full bg-orange-600 text-white font-black text-[10px] flex items-center justify-center shadow-[0_0_8px_rgba(234,88,12,0.6)] shrink-0 animate-pulse">
                      {conv.unreadCount}
                    </span>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* ════════════════════════════════════════════════════════════════════
            RIGHT MAIN CANVAS: Active Message Thread & Header
           ════════════════════════════════════════════════════════════════════ */}
        <div className={`flex-1 h-full flex flex-col relative min-w-[320px] overflow-hidden ${isDark ? 'bg-[#07070e] text-white' : 'bg-slate-50 text-slate-900'}`}>
          
          {/* Header Bar */}
          <div className={`h-16 px-4 border-b backdrop-blur-xl flex items-center justify-between shrink-0 overflow-hidden min-w-0 ${
            isDark ? 'border-white/10 bg-[#0d0e1b]/90 text-white' : 'border-slate-200 bg-white/95 text-slate-900 shadow-sm'
          }`}>
            <div className="flex items-center gap-3 min-w-0 overflow-hidden">
              <div className={`w-10 h-10 rounded-2xl bg-gradient-to-br ${activeConv.avatarBg || 'from-slate-800 to-slate-900'} flex items-center justify-center text-white font-bold text-sm shadow-md overflow-hidden shrink-0`}>
                {renderAvatarIcon(activeConv) || activeConv.avatar}
              </div>
              <div className="flex flex-col min-w-0 overflow-hidden justify-center leading-tight">
                <div className="flex items-center gap-1.5 min-w-0">
                  <h3 className={`text-xs sm:text-sm font-bold truncate min-w-0 block ${isDark ? 'text-white' : 'text-slate-900'}`}>{activeConv.name}</h3>
                  {/* ── PILLAR 01: Dynamic Header Online Indicator ── */}
                  <div className="shrink-0">{getHeaderStatusBadge(activeConv)}</div>
                </div>
                <span className={`text-[10px] sm:text-[11px] font-medium truncate min-w-0 block mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                  {activeConv.isOnline
                    ? (activeConv.id === 'ai_copilot' ? '✨ Always Online' : activeConv.isGroup ? `${activeConv.status}` : 'Online')
                    : activeConv.status || 'Offline'}
                </span>
              </div>
            </div>

            {/* Header Right Action Buttons */}
            <div className="flex items-center gap-1.5 shrink-0 ml-2">
              
              {/* ── Single Dynamic Layout Mode Toggle Button (SVG Icons: Float -> Split -> Full) ── */}
              <button
                onClick={cycleViewMode}
                className="h-9 px-2.5 sm:px-3 rounded-xl bg-white/5 hover:bg-orange-500/20 border border-white/10 hover:border-orange-500/40 text-slate-300 hover:text-orange-300 flex items-center gap-1.5 transition-all cursor-pointer shadow-sm shrink-0"
                title={
                  viewMode === 'floating' ? 'Layout: Floating Overlay. Click to switch to 50/50 Split-Screen' :
                  viewMode === 'split' ? 'Layout: 50/50 Split-Screen. Click to switch to Fullscreen' :
                  'Layout: Fullscreen. Click to switch to Floating Overlay'
                }
              >
                {viewMode === 'floating' && (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <rect x="3" y="3" width="18" height="18" rx="3"/>
                      <rect x="11" y="11" width="8" height="8" rx="1.5" fill="currentColor" fillOpacity="0.4"/>
                    </svg>
                    <span className="text-xs font-bold text-slate-200">Float</span>
                  </>
                )}

                {viewMode === 'split' && (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <rect x="3" y="3" width="18" height="18" rx="3"/>
                      <line x1="12" y1="3" x2="12" y2="21"/>
                    </svg>
                    <span className="text-xs font-bold text-orange-400">Split</span>
                  </>
                )}

                {viewMode === 'fullscreen' && (
                  <>
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M8 3H5a2 2 0 00-2 2v3m18 0V5a2 2 0 00-2-2h-3M3 16v3a2 2 0 002 2h3m8 0h3a2 2 0 002-2v-3"/>
                    </svg>
                    <span className="text-xs font-bold text-amber-400">Full</span>
                  </>
                )}
              </button>

              {/* ── PILLAR 08: Info Panel Toggle Button ── */}
              <button
                onClick={() => setShowInfoPanel(prev => !prev)}
                className={`w-8 h-8 sm:w-9 sm:h-9 rounded-xl border text-slate-300 hover:text-white flex items-center justify-center transition-all cursor-pointer ${
                  showInfoPanel ? 'bg-orange-500/20 border-orange-500/40 text-orange-300' : 'bg-white/5 hover:bg-white/10 border-white/10'
                }`}
                title="Contact Info"
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10"/>
                  <line x1="12" y1="16" x2="12" y2="12"/>
                  <line x1="12" y1="8" x2="12.01" y2="8"/>
                </svg>
              </button>
            </div>
          </div>

          {/* ════════════════════════════════════════════════════════════════════
              MESSENGER MESSAGE THREAD CANVAS (SCROLLABLE MIDDLE SECTION)
             ════════════════════════════════════════════════════════════════════ */}
          <div ref={chatScrollContainerRef} className="flex-1 overflow-y-auto px-3 sm:px-4 py-4 flex flex-col gap-1.5 chat-overlay-scrollbar min-h-0 w-full relative">
            {/* 📌 Pinned Message Banner */}
            {pinnedMessage && (
              <div className="sticky top-0 z-40 mb-3 p-2.5 bg-slate-900/95 border border-orange-500/40 rounded-xl backdrop-blur-xl flex items-center justify-between shadow-xl animate-fadeIn">
                <div className="flex items-center gap-2 min-w-0">
                  <span className="text-orange-400 text-sm">📌</span>
                  <div className="flex flex-col min-w-0">
                    <span className="text-[9.5px] font-bold text-orange-400 uppercase tracking-wider">Pinned Message</span>
                    <span className="text-xs font-medium text-slate-200 truncate">{pinnedMessage.body || pinnedMessage.messageBody || 'Pinned message'}</span>
                  </div>
                </div>
                <button
                  onClick={() => setPinnedMessage(null)}
                  className="px-2.5 py-1 text-[11px] font-bold text-slate-400 hover:text-white bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-colors cursor-pointer shrink-0 ml-2"
                >
                  Unpin
                </button>
              </div>
            )}
            {/* Empty State Welcome & Say Hello Banner */}
            {activeMessages.length === 0 && activeConv.id !== 'ai_copilot' && (
              <div className="flex-1 flex flex-col items-center justify-center p-6 text-center my-auto animate-fadeIn select-none">
                {/* Large avatar with glowing pulse */}
                <div className="relative mb-3.5">
                  <div className={`w-20 h-20 rounded-3xl bg-gradient-to-br ${activeConv.avatarBg || 'from-orange-500 to-amber-600'} flex items-center justify-center text-white text-3xl font-black shadow-xl ring-4 ring-orange-500/20 overflow-hidden`}>
                    {renderAvatarIcon(activeConv) || activeConv.avatar || (activeConv.name ? activeConv.name[0] : '👤')}
                  </div>
                  <span className="absolute -bottom-1.5 -right-1.5 text-2xl drop-shadow-md">👋</span>
                </div>

                <h3 className={`text-base sm:text-lg font-extrabold mb-1 tracking-tight ${isDark ? 'text-white' : 'text-slate-900'}`}>
                  Say hello to {activeConv.name}!
                </h3>

                <span className={`text-[10px] font-bold uppercase tracking-wider px-3 py-0.5 rounded-full mb-3 ${
                  activeConv.role === 'DRIVER' ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' :
                  activeConv.isGroup ? 'bg-purple-500/20 text-purple-400 border border-purple-500/30' :
                  'bg-orange-500/20 text-orange-400 border border-orange-500/30'
                }`}>
                  {activeConv.role || 'Customer'}
                </span>

                <p className={`text-xs max-w-sm leading-relaxed mb-6 font-medium ${isDark ? 'text-slate-400' : 'text-slate-600'}`}>
                  You haven't exchanged messages with <strong className={isDark ? 'text-slate-200' : 'text-slate-800'}>{activeConv.name}</strong> yet. Send a message below to start the conversation!
                </p>

                {/* Quick starter conversation pills */}
                <div className="flex flex-wrap items-center justify-center gap-2 max-w-md">
                  {[
                    { label: '👋 Kamusta! Paano kami makakatulong?', text: 'Hello! Kumusta po? Paano po namin kayo matutulungan sa inyong rental booking?' },
                    { label: '🚗 Vehicle Booking Follow-up', text: 'Hi! Gusto ko lang po mag-follow up regarding your vehicle booking reservation with Drive&Go.' },
                    { label: '📋 Document Requirement Update', text: 'Good day! May update lang po kami regarding your driver verification documents.' }
                  ].map((starter, sIdx) => (
                    <button
                      key={sIdx}
                      onClick={() => {
                        setInputText(starter.text);
                        if (inputRef.current) inputRef.current.focus();
                      }}
                      className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition-all cursor-pointer shadow-sm hover:scale-[1.02] active:scale-[0.98] ${
                        isDark
                          ? 'bg-white/5 hover:bg-orange-500/20 border-white/10 hover:border-orange-500/40 text-slate-200 hover:text-orange-300'
                          : 'bg-white hover:bg-orange-50 border-slate-200 hover:border-orange-300 text-slate-700 hover:text-orange-700'
                      }`}
                    >
                      {starter.label}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* ════════════════════════════════════════════════════════════════════
                AI COPILOT WELCOME & GET STARTED EXPERIENCE
               ════════════════════════════════════════════════════════════════════ */}
            {activeMessages.length === 0 && activeConv.id === 'ai_copilot' && (
              <div className="flex-1 flex flex-col items-center justify-center p-4 sm:p-6 text-center my-auto animate-fadeIn select-none max-w-xl mx-auto w-full">
                {/* Glowing AI Avatar & Online Ring */}
                <div className="relative mb-3.5 group">
                  <div className="absolute -inset-2 bg-gradient-to-r from-orange-600 via-amber-500 to-yellow-500 rounded-3xl blur-xl opacity-35 group-hover:opacity-60 transition duration-700 animate-pulse"></div>
                  <div className="relative w-16 h-16 sm:w-20 sm:h-20 rounded-3xl bg-gradient-to-br from-amber-500 via-orange-600 to-orange-700 flex items-center justify-center text-white text-3xl sm:text-4xl font-black shadow-2xl ring-4 ring-orange-500/30">
                    <svg width="34" height="34" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round">
                      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon>
                    </svg>
                  </div>
                  <span className="absolute -top-1 -right-1 flex h-4 w-4">
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                    <span className="relative inline-flex rounded-full h-4 w-4 bg-emerald-500 border-2 border-[#07070e]"></span>
                  </span>
                </div>

                {/* Pill Tag */}
                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-orange-500/10 border border-orange-500/25 text-orange-400 text-[11px] font-bold tracking-wide uppercase mb-2">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/>
                  </svg>
                  Autonomous Fleet Copilot
                </div>

                <h2 className={`text-xl sm:text-2xl font-black tracking-tight mb-2 ${isDark ? 'text-white' : 'text-slate-900'}`}>
                  Welcome to Drive&amp;Go AI
                </h2>

                <p className={`text-xs sm:text-sm max-w-md leading-relaxed mb-5 font-medium ${isDark ? 'text-slate-400' : 'text-slate-600'}`}>
                  Your intelligent enterprise assistant powered by Multi-Model Cloud AI. Query live fleet telematics, booking schedules, revenue analytics, and maintenance alerts.
                </p>

                {/* "Get Started" Quick Action Grid */}
                <div className="w-full mb-3.5 text-left">
                  <div className="flex items-center justify-between mb-2 px-1">
                    <span className={`text-[11px] font-bold uppercase tracking-wider ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                      ✨ Get Started / Quick Queries
                    </span>
                    <span className={`text-[10.5px] font-semibold ${isDark ? 'text-orange-400/80' : 'text-orange-600'}`}>Click to ask</span>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 w-full">
                    {[
                      {
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <rect x="3" y="11" width="18" height="10" rx="2"></rect>
                            <circle cx="7" cy="16" r="1.5"></circle>
                            <circle cx="17" cy="16" r="1.5"></circle>
                            <path d="M5 11l2-5h10l2 5"></path>
                          </svg>
                        ),
                        iconColor: 'text-orange-400 bg-orange-500/15 border-orange-500/30',
                        title: 'Fleet Availability',
                        subtitle: 'Available cars ready for rental right now',
                        prompt: 'Which vehicles are currently available in our fleet right now?'
                      },
                      {
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <line x1="12" y1="1" x2="12" y2="23"></line>
                            <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
                          </svg>
                        ),
                        iconColor: 'text-emerald-400 bg-emerald-500/15 border-emerald-500/30',
                        title: 'Revenue Breakdown',
                        subtitle: 'Today and this month\'s total sales performance',
                        prompt: 'Show me today\'s revenue breakdown and active rental sales performance.'
                      },
                      {
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <circle cx="12" cy="12" r="10"></circle>
                            <polyline points="12 6 12 12 16 14"></polyline>
                          </svg>
                        ),
                        iconColor: 'text-amber-400 bg-amber-500/15 border-amber-500/30',
                        title: 'Overdue Returns',
                        subtitle: 'Delayed vehicle returns requiring customer follow-up',
                        prompt: 'Are there any overdue rental returns or delayed vehicles today?'
                      },
                      {
                        icon: (
                          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"></path>
                          </svg>
                        ),
                        iconColor: 'text-sky-400 bg-sky-500/15 border-sky-500/30',
                        title: 'Maintenance Alerts',
                        subtitle: 'Vehicles scheduled for oil change or inspection',
                        prompt: 'Which vehicles are scheduled for maintenance or oil change this week?'
                      }
                    ].map((item, idx) => (
                      <button
                        key={idx}
                        onClick={() => {
                          setInputText(item.prompt);
                          if (inputRef.current) inputRef.current.focus();
                        }}
                        className={`p-2.5 rounded-xl border text-left flex items-start gap-2.5 transition-all duration-200 cursor-pointer group shadow-sm hover:scale-[1.02] active:scale-[0.98] ${
                          isDark
                            ? 'bg-slate-900/70 hover:bg-slate-800/90 border-white/10 hover:border-orange-500/40'
                            : 'bg-white hover:bg-orange-50/70 border-slate-200 hover:border-orange-300'
                        }`}
                      >
                        <div className={`w-7 h-7 rounded-lg border flex items-center justify-center shrink-0 transition-transform group-hover:scale-110 ${item.iconColor}`}>
                          {item.icon}
                        </div>
                        <div className="flex flex-col min-w-0">
                          <span className={`text-xs font-bold truncate group-hover:text-orange-400 transition-colors ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>
                            {item.title}
                          </span>
                          <span className={`text-[10.5px] font-medium line-clamp-1 mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                            {item.subtitle}
                          </span>
                        </div>
                      </button>
                    ))}
                  </div>
                </div>

                {/* One-Click Query Pills */}
                <div className="flex flex-wrap items-center justify-center gap-1.5 max-w-md">
                  {[
                    { label: '🚗 Available SUVs', q: 'Show me all available SUV vehicles in the fleet right now.' },
                    { label: '📊 Daily Sales Summary', q: 'Give me a summary of today\'s total sales and rental revenue.' },
                    { label: '⚠️ Driver License Expirations', q: 'Check driver licenses that are expiring soon or already expired.' },
                    { label: '🌧️ Weather & Flood Radar', q: 'What is the current weather condition and flood advisory around our garage hub?' }
                  ].map((chip, cIdx) => (
                    <button
                      key={cIdx}
                      onClick={() => {
                        setInputText(chip.q);
                        if (inputRef.current) inputRef.current.focus();
                      }}
                      className={`px-2.5 py-1 rounded-full text-[10.5px] font-semibold border transition-all cursor-pointer shadow-sm hover:scale-105 active:scale-95 ${
                        isDark
                          ? 'bg-white/5 hover:bg-orange-500/20 border-white/10 hover:border-orange-500/30 text-slate-300 hover:text-orange-300'
                          : 'bg-slate-100 hover:bg-orange-100 border-slate-200 hover:border-orange-300 text-slate-700 hover:text-orange-700'
                      }`}
                    >
                      {chip.label}
                    </button>
                  ))}
                </div>
              </div>
            )}

            {/* Render Messages with Messenger Consecutive Grouping & Timestamps */}
            {(() => {
              // Find index of the last outgoing message marked seen
              let lastSeenIdx = -1;
              for (let i = activeMessages.length - 1; i >= 0; i--) {
                const m = activeMessages[i];
                if (m.isMine && (m.status === 'seen' || m.deliveryStatus === 'seen')) {
                  lastSeenIdx = i;
                  break;
                }
              }

              return activeMessages.map((msg, idx) => {
                // Messenger Timestamp Divider (> 15 minutes threshold or first message)
                let showTimestamp = idx === 0;
                if (idx > 0) {
                  const currTs = new Date(msg.timestamp || Date.now()).getTime();
                  const prevTs = new Date(activeMessages[idx - 1].timestamp || Date.now()).getTime();
                  if (!isNaN(currTs) && !isNaN(prevTs) && (currTs - prevTs > 15 * 60 * 1000)) {
                    showTimestamp = true;
                  }
                }

                // Messenger Consecutive Bubble Grouping Position
                const prev = idx > 0 ? activeMessages[idx - 1] : null;
                const next = idx < activeMessages.length - 1 ? activeMessages[idx + 1] : null;
                const samePrev = prev && ((prev.senderId && msg.senderId && prev.senderId === msg.senderId) || (prev.isMine === msg.isMine));
                const sameNext = next && ((next.senderId && msg.senderId && next.senderId === msg.senderId) || (next.isMine === msg.isMine));

                let groupPos = 'single';
                if (samePrev && sameNext) groupPos = 'middle';
                else if (samePrev && !sameNext) groupPos = 'last';
                else if (!samePrev && sameNext) groupPos = 'first';

                const marginTopStyle = samePrev ? { marginTop: '3px' } : { marginTop: idx === 0 ? '4px' : '14px' };
                const isLastSeen = idx === lastSeenIdx;

                return (
                  <React.Fragment key={msg.id || msg.messageId || idx}>
                    {showTimestamp && (
                      <div className="flex items-center justify-center my-3">
                        <span className="text-[10.5px] font-bold text-slate-300 bg-white/10 border border-white/15 px-3 py-1 rounded-full shadow-sm">
                          {formatMessengerTimestamp(msg.timestamp || msg.time)}
                        </span>
                      </div>
                    )}

                    <div style={marginTopStyle}>
                      {(() => {
                        const ActiveBubble = (typeof GenUiBubble !== 'undefined' ? GenUiBubble : null) || window.GenUiBubble || GenUiBubbleComp;
                        if (ActiveBubble) {
                          return (
                            <ActiveBubble
                              message={msg}
                              groupPosition={groupPos}
                              showSenderHeader={!samePrev}
                              isLastSeenMessage={isLastSeen}
                              seenAvatarUrl={activeConv.avatarUrl || activeConv.avatar}
                            />
                          );
                        }
                        return (
                          <div className={`flex gap-2.5 max-w-[80%] ${msg.isMine ? 'ml-auto flex-row-reverse' : 'mr-auto'}`}>
                            <div className={`p-3.5 rounded-2xl border ${msg.isMine ? 'bg-orange-600 text-white' : 'bg-slate-900 text-slate-100'}`}>
                              {(() => {
                                const txt = msg.body || '';
                                if (txt.startsWith('[Voice Note') || txt.includes('🎙️ Voice Note') || txt.includes('Voice Note')) {
                                  return (
                                    <div className="flex items-center gap-2 bg-orange-950/60 border border-orange-500/30 rounded-2xl px-3.5 py-2 text-xs font-bold text-orange-300">
                                      <span className="w-6 h-6 rounded-full bg-orange-600 flex items-center justify-center text-white text-xs">▶</span>
                                      <span>🎙️ Voice Note</span>
                                    </div>
                                  );
                                }
                                const combinedRegex = /(https?:\/\/[^\s]+|www\.[^\s]+|@(Drive&Go AI|DriveAndGo AI|Meta AI))/gi;
                                const parts = [];
                                let lastIdx = 0;
                                let match;
                                while ((match = combinedRegex.exec(txt)) !== null) {
                                  if (match.index > lastIdx) parts.push(txt.slice(lastIdx, match.index));
                                  const token = match[0];
                                  if (token.startsWith('@')) {
                                    parts.push(
                                      <span key={match.index} className="inline-flex items-center px-1.5 py-0.5 rounded-full text-[11px] font-bold bg-amber-500/25 text-amber-200 border border-amber-500/40 mx-0.5 shadow-sm">
                                        {token}
                                      </span>
                                    );
                                  } else {
                                    const u = token;
                                    const href = u.startsWith('http') ? u : `https://${u}`;
                                    parts.push(
                                      <a key={match.index} href={href} target="_blank" rel="noopener noreferrer" className="text-blue-400 underline font-semibold break-all" onClick={e => e.stopPropagation()}>
                                        {u}
                                      </a>
                                    );
                                  }
                                  lastIdx = match.index + token.length;
                                }
                                if (lastIdx < txt.length) parts.push(txt.slice(lastIdx));
                                return <p className="text-xs whitespace-pre-wrap">{parts.length > 0 ? parts : txt}</p>;
                              })()}
                              <span className="text-[9px] opacity-60 mt-1 block">{msg.time}</span>
                            </div>
                          </div>
                        );
                      })()}
                    </div>
                  </React.Fragment>
                );
              });
            })()}

            {/* Messenger Real-Time Live Typing Indicator Bubble */}
            {typingUsers[activeConvId] && (
              <div className="flex items-center gap-2 mt-3 animate-fadeIn">
                <div className="w-7 h-7 rounded-full bg-gradient-to-br from-slate-800 to-slate-900 flex items-center justify-center text-xs font-bold text-white shrink-0">
                  {renderAvatarIcon(activeConv) || activeConv.avatar}
                </div>
                <div className="bg-[#2a2b32] border border-white/10 rounded-2xl px-3.5 py-2.5 flex items-center gap-1.5 shadow-md">
                  <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '0ms' }} />
                  <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '150ms' }} />
                  <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '300ms' }} />
                </div>
              </div>
            )}

            {/* AI Thinking Bubble Indicator */}
            {isAiLoading && (
              <div style={{ marginTop: '14px' }}>
                {AiThinkingBubbleComp ? (
                  <AiThinkingBubbleComp />
                ) : (
                  <div className="flex items-center gap-3 p-3 bg-orange-500/10 border border-orange-500/20 rounded-2xl animate-pulse">
                    <div className="w-8 h-8 rounded-full bg-gradient-to-tr from-orange-500 to-amber-500 flex items-center justify-center text-white text-xs font-bold shadow-md shadow-orange-500/20">
                      <IconSparkles sz={14} />
                    </div>
                    <div className="flex flex-col gap-1">
                      <span className="text-[11px] font-bold text-orange-400">Drive&Go AI is thinking...</span>
                      <div className="flex items-center gap-1.5">
                        <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '0ms' }} />
                        <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '150ms' }} />
                        <span className="w-1.5 h-1.5 rounded-full bg-orange-400 animate-bounce" style={{ animationDelay: '300ms' }} />
                      </div>
                    </div>
                  </div>
                )}
              </div>
            )}

            <div ref={messageEndRef} />
          </div>

          {/* ════════════════════════════════════════════════════════════════════
              FLOATING GLASS INPUT BAR AREA
             ════════════════════════════════════════════════════════════════════ */}
          <div className="p-2.5 sm:p-3.5 border-t border-white/10 bg-[#0d0e1b]/95 backdrop-blur-xl shrink-0 flex flex-col gap-2 relative w-full overflow-hidden">
            
            {/* Antigravity-Style Queued Messages Container */}
            {aiQueue.filter(q => q.convId === activeConvId).length > 0 && (
              <div className="p-3 bg-[#161724]/90 border border-white/10 rounded-2xl backdrop-blur-md shadow-lg transition-all animate-fadeIn mb-1">
                <div className="flex items-center justify-between pb-2 border-b border-white/10">
                  <div className="flex items-center gap-2 text-[11px]">
                    <span className="font-bold text-slate-200">Queued Messages</span>
                    <span className="w-4 h-4 rounded-full bg-orange-500/30 border border-orange-500/50 text-orange-300 font-bold text-[10px] flex items-center justify-center">
                      {aiQueue.filter(q => q.convId === activeConvId).length}
                    </span>
                    <span className="text-slate-400 text-[10px]">Sends after agent finishes working</span>
                  </div>
                  <button
                    onClick={() => updateAiQueue(aiQueueRef.current.filter(q => q.convId !== activeConvId))}
                    className="text-slate-400 hover:text-slate-200 transition-colors p-1 cursor-pointer"
                    title="Collapse / Clear active queue"
                  >
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                      <polyline points="18 15 12 9 6 15"/>
                    </svg>
                  </button>
                </div>
                <div className="mt-2 flex flex-col gap-1.5 max-h-28 overflow-y-auto">
                  {aiQueue.filter(q => q.convId === activeConvId).map((item) => (
                    <div key={item.id} className="flex items-center justify-between bg-white/5 hover:bg-white/10 px-3 py-2 rounded-xl border border-white/5 text-xs text-slate-200">
                      <span className="truncate max-w-[82%] font-medium">{item.text}</span>
                      <div className="flex items-center gap-2 shrink-0">
                        <button
                          onClick={() => handleRemoveQueuedItem(item.id)}
                          className="text-slate-400 hover:text-red-400 transition-colors p-1 cursor-pointer"
                          title="Delete queued message"
                        >
                          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <polyline points="3 6 5 6 21 6"/>
                            <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/>
                          </svg>
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            
            {/* AI Smart Suggestion Pills ONLY when user is actively typing */}
            {(() => {
              const cleanInputQuery = inputText.trim().toLowerCase();
              const filteredSuggestionPills = cleanInputQuery.length > 0
                ? DEFAULT_SUGGESTION_PILLS.filter(sug => {
                    const textMatch = sug.text.toLowerCase().includes(cleanInputQuery);
                    const queryMatch = sug.query.toLowerCase().includes(cleanInputQuery);
                    const keywordMatch = sug.keywords && sug.keywords.some(kw => kw.toLowerCase().includes(cleanInputQuery) || cleanInputQuery.includes(kw.toLowerCase()));
                    return textMatch || queryMatch || keywordMatch;
                  })
                : [];

              if (activeConvId !== 'ai_copilot' || filteredSuggestionPills.length === 0) return null;

              return (
                <div
                  ref={pillScrollRef}
                  onMouseDown={handlePillMouseDown}
                  onMouseLeave={handlePillMouseLeaveOrUp}
                  onMouseUp={handlePillMouseLeaveOrUp}
                  onMouseMove={handlePillMouseMove}
                  className={`flex items-center gap-1.5 overflow-x-auto pb-1 scrollbar-none text-[11px] select-none transition-all animate-fadeIn ${
                    isPillDragging ? 'cursor-grabbing' : 'cursor-grab'
                  }`}
                >
                  {filteredSuggestionPills.map((sug, i) => (
                    <button
                      key={i}
                      onClick={(e) => {
                        if (hasPillDragged) {
                          e.preventDefault();
                          e.stopPropagation();
                          return;
                        }
                        setInputText('');
                        enqueueAiRequest({ id: Date.now(), text: sug.query, convId: 'ai_copilot', isGroup: false, isCopilot: true });
                      }}
                      className="whitespace-nowrap bg-white/5 hover:bg-orange-500/20 hover:text-orange-300 border border-white/10 hover:border-orange-500/30 px-3 py-1.5 rounded-xl transition-all flex items-center gap-1.5 text-slate-300 shrink-0 cursor-pointer"
                    >
                      <span>{sug.text}</span>
                    </button>
                  ))}
                </div>
              );
            })()}

            {/* Mention Autocomplete Popup (@Drive&Go AI) */}
            {mentionQuery !== null && (
              <div className="absolute bottom-full left-4 mb-2 w-64 bg-slate-900/95 border border-white/10 rounded-2xl shadow-2xl backdrop-blur-xl overflow-hidden z-50 animate-fadeIn">
                <div className="p-2 border-b border-white/10 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                  Mention Dispatch Target
                </div>
                {mentionTargets.filter(t => t.name.toLowerCase().includes(mentionQuery)).map(target => (
                  <button
                    key={target.id}
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => insertMention(target)}
                    className="w-full text-left px-3 py-2 hover:bg-orange-500/20 hover:text-orange-300 flex items-center gap-2 transition-all cursor-pointer border-b border-white/5 last:border-none"
                  >
                    <span className="text-base">{target.avatar}</span>
                    <div className="flex flex-col">
                      <span className="text-xs font-bold text-slate-100">{target.name}</span>
                      <span className="text-[9.5px] text-slate-400">{target.role}</span>
                    </div>
                  </button>
                ))}
              </div>
            )}

            {/* Web Audio API Real Visualizer Panel during recording */}
            {isRecording && (
              <div className="flex items-center justify-between bg-orange-950/60 border border-orange-500/40 rounded-2xl px-4 py-2 animate-fadeIn">
                <div className="flex items-center gap-2">
                  <span className="w-3 h-3 rounded-full bg-red-500 animate-ping" />
                  <span className="text-xs font-bold text-orange-400">Recording Voice Note...</span>
                  <span className="text-xs font-mono text-white">00:0{recordingSeconds}</span>
                </div>
                <canvas ref={visualizerCanvasRef} width={120} height={20} className="rounded-lg" />
                <button
                  onClick={stopRecordingVoice}
                  className="bg-orange-600 hover:bg-orange-500 text-white font-bold text-xs px-3 py-1 rounded-xl transition-all cursor-pointer"
                >
                  Send Voice
                </button>
              </div>
            )}

            {/* ── Editing Message Banner ── */}
            {editingMessage && (
              <div className="flex items-center gap-2 bg-slate-900/90 border-l-4 border-blue-500 rounded-xl px-3 py-2 animate-fadeIn">
                <div className="flex-1 min-w-0">
                  <div className="text-[10px] font-bold text-blue-400 mb-0.5">
                    Editing Message
                  </div>
                  <p className="text-[11px] text-slate-300 truncate">
                    {editingMessage.body || editingMessage.messageBody || '...'}
                  </p>
                </div>
                <button
                  onClick={cancelEditMessage}
                  className="w-6 h-6 rounded-full bg-white/10 hover:bg-white/20 text-slate-400 hover:text-white flex items-center justify-center text-sm transition-all cursor-pointer shrink-0"
                  title="Cancel editing"
                >
                  ×
                </button>
              </div>
            )}

            {/* ── PILLAR 02: Reply Preview Bar ── */}
            {replyingTo && (
              <div className="flex items-center gap-2 bg-slate-900/80 border-l-4 border-orange-500 rounded-xl px-3 py-2 animate-fadeIn">
                <div className="flex-1 min-w-0">
                  <div className="text-[10px] font-bold text-orange-400 mb-0.5">
                    Replying to {replyingTo.sender}
                  </div>
                  <p className="text-[11px] text-slate-400 truncate">
                    {replyingTo.mediaType === 'audio' ? (
                      <span className="flex items-center gap-1 text-slate-300">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><line x1="12" y1="19" x2="12" y2="23"/><line x1="8" y1="23" x2="16" y2="23"/></svg>
                        Voice Note
                      </span>
                    ) : replyingTo.mediaType === 'image' ? (
                      <span className="flex items-center gap-1 text-slate-300">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><polyline points="21 15 16 10 5 21"/></svg>
                        Photo
                      </span>
                    ) : replyingTo.mediaType === 'video' ? (
                      <span className="flex items-center gap-1 text-slate-300">
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polygon points="23 7 16 12 23 17 23 7"/><rect x="1" y="5" width="15" height="14" rx="2"/></svg>
                        Video
                      </span>
                    ) : (
                      replyingTo.body || '...'
                    )}
                  </p>
                </div>
                <button
                  onClick={() => setReplyingTo(null)}
                  className="w-6 h-6 rounded-full bg-white/10 hover:bg-white/20 text-slate-400 hover:text-white flex items-center justify-center text-sm transition-all cursor-pointer shrink-0"
                >
                  ×
                </button>
              </div>
            )}

            {/* Messenger Input Bar Layout: [Media 📎] [Mic 🎙️] [AI Palette ✨] -> [Pill Textarea] -> [Emoji 😊] -> [Send ➔] */}
            <div className={`flex items-center ${viewMode === 'floating' ? 'gap-1 px-2 py-1.5' : 'gap-1.5 sm:gap-2 px-2.5 sm:px-3.5 py-2'} bg-slate-950/80 border border-white/10 rounded-2xl focus-within:border-orange-500/50 shadow-inner transition-all w-full box-border overflow-hidden`}>
              
              {/* Left Action Buttons Group */}
              <div className={`flex items-center ${viewMode === 'floating' ? 'gap-0.5' : 'gap-1'} shrink-0 self-center`}>
                {/* ── Attachment Media Icon — Hidden for AI ── */}
                {!isAiChannel && (
                  <>
                    <button
                      onClick={() => mediaInputRef.current?.click()}
                      className="text-slate-400 hover:text-orange-400 transition-colors p-1 cursor-pointer shrink-0"
                      title="Attach Media (Photos/Videos)"
                    >
                      <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M21.44 11.05l-9.19 9.19a6 6 0 01-8.49-8.49l9.19-9.19a4 4 0 015.66 5.66l-9.2 9.19a2 2 0 01-2.83-2.83l8.49-8.48"/>
                      </svg>
                    </button>
                    <input
                      type="file"
                      ref={mediaInputRef}
                      accept="image/*,video/*,audio/*"
                      className="hidden"
                      onChange={(e) => {
                        const file = e.target.files?.[0];
                        if (!file) return;
                        const url = URL.createObjectURL(file);
                        const type = file.type.startsWith('image') ? 'image' : file.type.startsWith('video') ? 'video' : 'audio';
                        setAttachedMedia({ type, url, name: file.name });
                      }}
                    />
                  </>
                )}

                {/* ── Voice Note Mic Icon — Hidden for AI ── */}
                {!isAiChannel && (
                  <button
                    onClick={isRecording ? stopRecordingVoice : startRecordingVoice}
                    className={`p-1 cursor-pointer transition-all shrink-0 ${isRecording ? 'text-red-500 animate-bounce' : 'text-slate-400 hover:text-orange-400'}`}
                    title="Record Microphone Voice Note"
                  >
                    {isRecording
                      ? <svg width="17" height="17" viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="8"/></svg>
                      : <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 1a3 3 0 00-3 3v8a3 3 0 006 0V4a3 3 0 00-3-3z"/><path d="M19 10v2a7 7 0 01-14 0v-2"/><line x1="12" y1="19" x2="12" y2="23"/><line x1="8" y1="23" x2="16" y2="23"/></svg>
                    }
                  </button>
                )}

                {/* ── Mention @ Icon — Hidden for AI Channel & when tag is active ── */}
                {!isAiChannel && !hasAiMentionTag && (
                  <button
                    type="button"
                    onClick={() => {
                      if (mentionQuery !== null) {
                        setMentionQuery(null);
                      } else {
                        setMentionQuery('');
                        setMentionIndex(0);
                        setTimeout(() => inputRef.current?.focus(), 20);
                      }
                    }}
                    className={`p-1 cursor-pointer transition-colors shrink-0 ${mentionQuery !== null ? 'text-amber-400 font-bold' : 'text-slate-400 hover:text-amber-400'}`}
                    title="Mention @Drive&Go AI"
                  >
                    <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="12" r="4"/>
                      <path d="M16 8v5a3 3 0 0 0 6 0v-1a10 10 0 1 0-4 8"/>
                    </svg>
                  </button>
                )}

                {/* Command Palette Trigger — ONLY for AI Chatbot or @Drive&Go AI mention */}
                {(activeConvId === 'ai_copilot' || hasAiMentionTag || inputText.includes('@Drive&Go AI')) && (
                  <button
                    onClick={() => setIsCommandPaletteOpen(true)}
                    className="text-amber-400 hover:text-amber-300 transition-colors p-1 cursor-pointer shrink-0 flex items-center gap-1"
                    title="Open AI Command Palette (Ctrl+K)"
                  >
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>
                    </svg>
                  </button>
                )}
              </div>

              {/* Single Highlight Mention Capsule Pill in Textbox */}
              {hasAiMentionTag && (
                <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-lg text-[10px] sm:text-[10.5px] font-bold bg-amber-500/25 text-amber-200 border border-amber-500/40 shrink-0 max-w-[62px] sm:max-w-[125px] shadow-sm transition-all animate-fadeIn self-center">
                  <span className="truncate">{viewMode === 'floating' ? '@AI' : '@Drive&Go AI'}</span>
                  <button
                    type="button"
                    onClick={() => setHasAiMentionTag(false)}
                    className="text-amber-300 hover:text-white transition-colors cursor-pointer text-xs font-bold px-0.5 shrink-0 ml-0.5"
                    title="Remove AI Mention"
                  >
                    ×
                  </button>
                </span>
              )}

              <textarea
                ref={inputRef}
                rows={1}
                value={inputText}
                onChange={handleInputChange}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    if (!e.repeat) {
                      handleSend();
                    }
                  }
                }}
                placeholder={hasAiMentionTag ? "Ask AI..." : (isAiChannel ? "Ask Drive&Go AI..." : "Message")}
                className="flex-1 min-w-[40px] bg-transparent border-none text-xs sm:text-[13px] text-slate-100 placeholder-slate-400/80 outline-none resize-none py-1 leading-[20px] self-center max-h-40 overflow-y-auto transition-all"
                style={{ minHeight: '20px', height: 'auto' }}
              />

              {/* Emoji Picker Quick Button */}
              <button
                onClick={() => {
                  setInputText(prev => prev + ' 😊 ');
                }}
                className="text-slate-400 hover:text-amber-400 transition-colors p-0.5 sm:p-1 cursor-pointer shrink-0"
                title="Insert Emoji"
              >
                <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10"/>
                  <path d="M8 14s1.5 2 4 2 4-2 4-2"/>
                  <line x1="9" y1="9" x2="9.01" y2="9"/>
                  <line x1="15" y1="9" x2="15.01" y2="9"/>
                </svg>
              </button>

              {/* Send Button */}
              <button
                onClick={handleSend}
                disabled={!inputText.trim() && !attachedMedia}
                className="w-7 h-7 sm:w-8 sm:h-8 rounded-xl bg-gradient-to-r from-orange-600 to-amber-500 hover:brightness-110 active:scale-95 disabled:opacity-40 text-white font-bold flex items-center justify-center shadow-[0_0_15px_rgba(234,88,12,0.4)] transition-all cursor-pointer shrink-0 ml-0.5"
              >
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="22" y1="2" x2="11" y2="13"/>
                  <polygon points="22 2 15 22 11 13 2 9 22 2"/>
                </svg>
              </button>
            </div>

            {/* Attached Media Tag Indicator */}
            {attachedMedia && (
              <div className="flex items-center gap-2 bg-slate-900 border border-white/10 rounded-xl px-3 py-1 text-xs text-orange-300 w-fit">
                <span>📎 Attachment: {attachedMedia.name}</span>
                <button onClick={() => setAttachedMedia(null)} className="text-slate-400 hover:text-white font-bold">✕</button>
              </div>
            )}

          </div>

        </div>

        {/* ── PILLAR 08: Info Panel Drawer (right side) ── */}
        {showInfoPanel && (
          <InfoPanel
            conv={activeConv}
            onClose={() => setShowInfoPanel(false)}
            viewMode={viewMode}
            apiBase={apiBase}
            activeMessages={activeMessages}
            openAccordions={openAccordions}
            setOpenAccordions={setOpenAccordions}
            fileInputRef={fileInputRef}
            isUploading={isUploading}
            uploadStatus={uploadStatus}
            renderAvatarIcon={renderAvatarIcon}
            setLightboxMedia={setLightboxMedia}
            uploadErrorModal={uploadErrorModal}
            setUploadErrorModal={setUploadErrorModal}
          />
        )}

        {/* ── Global System Modals (Unsend Choice Modal, Forward Modal, Reactions) ── */}
        {systemModal?.type === 'unsend' && systemModal.message && (() => {
          const targetMsg = systemModal.message;
          const isMine = !!(targetMsg.isMine || targetMsg.is_mine || targetMsg.senderId === 'admin' || targetMsg.sender === 'Admin');
          const options = isMine ? [
            { id: 'everyone', label: 'Unsend for everyone', hint: 'Removes the message for everyone in this chat.' },
            { id: 'you', label: 'Unsend for you', hint: 'Removes the message only from your view.' }
          ] : [
            { id: 'you', label: 'Unsend for you', hint: 'Removes the message only from your view. Other chat members will still be able to see it.' }
          ];
          const activeScope = isMine ? unsendScope : 'you';

          return (
            <Portal>
              <div className="fixed inset-0 z-[99999] bg-black/60 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setSystemModal(null)}>
                <div className="w-full max-w-sm rounded-2xl bg-[#242526] border border-white/10 shadow-2xl p-5" onClick={(e) => e.stopPropagation()}>
                  <h3 className="text-lg font-bold text-white mb-2">
                    {isMine ? 'Who do you want to unsend this message for?' : 'Remove for you?'}
                  </h3>
                  <div className="space-y-2 my-4">
                    {options.map(option => (
                      <button key={option.id} onClick={() => setUnsendScope(option.id)} className={`w-full text-left rounded-xl border px-3 py-3 transition-all cursor-pointer ${activeScope === option.id ? 'border-orange-500 bg-orange-500/15' : 'border-white/10 bg-white/5 hover:bg-white/10'}`}>
                        <span className="block text-sm font-bold text-white">{option.label}</span>
                        <span className="block text-xs text-slate-400 mt-0.5">{option.hint}</span>
                      </button>
                    ))}
                  </div>
                  <div className="flex justify-end gap-2">
                    <button onClick={() => setSystemModal(null)} className="px-4 py-2 rounded-lg bg-white/10 hover:bg-white/15 text-white text-sm font-bold cursor-pointer">Cancel</button>
                    <button onClick={() => handleUnsendMessage(targetMsg, activeScope)} className="px-4 py-2 rounded-lg bg-red-600 hover:bg-red-500 text-white text-sm font-bold cursor-pointer">Remove</button>
                  </div>
                </div>
              </div>
            </Portal>
          );
        })()}

        {/* ── FORWARD MESSAGE MODAL ────────────────────────────────────── */}
        {systemModal?.type === 'forward' && systemModal.message && (
          <Portal>
            <div className="fixed inset-0 z-[99999] bg-black/65 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setSystemModal(null)}>
              <div className={`w-full max-w-md rounded-3xl border shadow-2xl overflow-hidden flex flex-col max-h-[85vh] animate-modal ${
                isDark ? 'bg-[#181924] border-white/10 text-white' : 'bg-white border-slate-200 text-slate-900'
              }`} onClick={(e) => e.stopPropagation()}>
                
                {/* Header */}
                <div className={`px-5 py-4 border-b flex items-center justify-between shrink-0 ${
                  isDark ? 'border-white/10 bg-white/[0.02]' : 'border-slate-200 bg-slate-50'
                }`}>
                  <div>
                    <h3 className="text-base font-extrabold tracking-tight">Forward Message</h3>
                    <p className={`text-[11px] font-medium mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                      Select any group, customer, or driver to forward
                    </p>
                  </div>
                  <button 
                    onClick={() => setSystemModal(null)} 
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold transition-all cursor-pointer ${
                      isDark ? 'bg-white/5 hover:bg-white/10 text-slate-300 hover:text-white' : 'bg-slate-100 hover:bg-slate-200 text-slate-600 hover:text-slate-900'
                    }`}
                  >
                    ✕
                  </button>
                </div>

                {/* Search Bar */}
                <div className="p-3.5 pb-2 shrink-0">
                  <div className={`relative flex items-center px-3 py-2 rounded-xl border ${
                    isDark ? 'bg-slate-900/60 border-white/10 text-white' : 'bg-slate-100/80 border-slate-200 text-slate-900'
                  }`}>
                    <svg className="w-4 h-4 text-slate-400 mr-2.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                    <input 
                      value={forwardQuery} 
                      onChange={(e) => setForwardQuery(e.target.value)} 
                      autoFocus 
                      placeholder="Search customers, drivers, groups..." 
                      className="w-full bg-transparent text-xs outline-none placeholder:text-slate-500 font-medium" 
                    />
                    {forwardQuery && (
                      <button onClick={() => setForwardQuery('')} className="text-slate-400 hover:text-white text-xs px-1 font-bold">✕</button>
                    )}
                  </div>
                </div>

                {/* Combined Contacts List */}
                <div className="px-3 pb-3 flex-1 overflow-y-auto chat-overlay-scrollbar flex flex-col gap-1">
                  {(() => {
                    const q = forwardQuery.trim().toLowerCase();
                    const seenIds = new Set();
                    const combined = [];

                    conversations.forEach(c => {
                      if (c.id === 'ai_copilot') return;
                      const idStr = String(c.id);
                      seenIds.add(idStr);
                      combined.push(c);
                    });

                    allContacts.forEach(c => {
                      const idStr = String(c.id);
                      if (!seenIds.has(idStr)) {
                        seenIds.add(idStr);
                        combined.push(c);
                      }
                    });

                    const list = combined.filter(c => {
                      if (!q) return true;
                      return (
                        (c.name && c.name.toLowerCase().includes(q)) ||
                        (c.role && c.role.toLowerCase().includes(q)) ||
                        (c.email && c.email.toLowerCase().includes(q)) ||
                        (c.phone && c.phone.toLowerCase().includes(q))
                      );
                    });

                    if (list.length === 0) {
                      return (
                        <div className="py-12 text-center text-xs text-slate-400 font-medium">
                          No matching contacts or groups found for "{forwardQuery}"
                        </div>
                      );
                    }

                    return list.map(conv => {
                      const isDriver = (conv.role || '').toUpperCase() === 'DRIVER';
                      const isGroup = conv.isGroup;
                      return (
                        <div 
                          key={conv.id} 
                          className={`flex items-center gap-3 rounded-2xl p-2.5 transition-all border ${
                            isDark 
                              ? 'border-white/5 hover:border-white/15 hover:bg-white/[0.04]' 
                              : 'border-slate-100 hover:border-slate-200 hover:bg-slate-50'
                          }`}
                        >
                          <div className={`w-10 h-10 rounded-2xl bg-gradient-to-br ${conv.avatarBg || 'from-slate-700 to-slate-900'} flex items-center justify-center text-white font-black overflow-hidden shrink-0 shadow-sm ring-1 ring-white/10`}>
                            {renderAvatarIcon(conv) || conv.avatar || (conv.name ? conv.name[0] : '👤')}
                          </div>

                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className={`text-xs font-black truncate ${isDark ? 'text-white' : 'text-slate-900'}`}>
                                {conv.name}
                              </p>
                              <span className={`text-[9px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${getRoleBadgeClasses(conv.role)}`}>
                                {conv.role || 'Contact'}
                              </span>
                            </div>
                            <p className={`text-[11px] font-medium truncate mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                              {conv.phone || conv.email || (conv.isGroup ? 'Group Channel' : 'Active Account')}
                            </p>
                          </div>

                          <button 
                            onClick={() => handleForwardMessage(systemModal.message, conv.id)} 
                            className="px-4 py-1.5 rounded-xl bg-orange-600 hover:bg-orange-500 active:scale-95 text-white text-xs font-bold transition-all shadow-md cursor-pointer shrink-0"
                          >
                            Send
                          </button>
                        </div>
                      );
                    });
                  })()}
                </div>
              </div>
            </div>
          </Portal>
        )}

        {/* ── NEW MESSAGE / CONTACT PICKER MODAL ─────────────────────────── */}
        {showNewChatModal && (
          <Portal>
            <div className="fixed inset-0 z-[99999] bg-black/65 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setShowNewChatModal(false)}>
              <div className={`w-full max-w-lg rounded-3xl border shadow-2xl overflow-hidden flex flex-col max-h-[85vh] animate-modal ${
                isDark ? 'bg-[#181924] border-white/10 text-white' : 'bg-white border-slate-200 text-slate-900'
              }`} onClick={(e) => e.stopPropagation()}>
                
                {/* Modal Header */}
                <div className={`px-5 py-4 border-b flex items-center justify-between shrink-0 ${
                  isDark ? 'border-white/10 bg-white/[0.02]' : 'border-slate-200 bg-slate-50'
                }`}>
                  <div>
                    <h3 className="text-base font-extrabold tracking-tight">New Conversation</h3>
                    <p className={`text-[11px] font-medium mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                      Select any registered Admin, Driver, Customer, Staff, or AI Copilot
                    </p>
                  </div>
                  <button 
                    onClick={() => setShowNewChatModal(false)} 
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold transition-all cursor-pointer ${
                      isDark ? 'bg-white/5 hover:bg-white/10 text-slate-300 hover:text-white' : 'bg-slate-100 hover:bg-slate-200 text-slate-600 hover:text-slate-900'
                    }`}
                  >
                    ✕
                  </button>
                </div>

                {/* Search Bar & Filter Tabs */}
                <div className="p-4 pb-2 shrink-0 flex flex-col gap-2.5">
                  <div className={`relative flex items-center px-3.5 py-2 rounded-xl border ${
                    isDark ? 'bg-slate-900/60 border-white/10 text-white' : 'bg-slate-100/80 border-slate-200 text-slate-900'
                  }`}>
                    <svg className="w-4 h-4 text-slate-400 mr-2.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                    </svg>
                    <input 
                      value={newChatSearch} 
                      onChange={(e) => setNewChatSearch(e.target.value)} 
                      autoFocus 
                      placeholder="Search by name, role, phone, or email..." 
                      className="w-full bg-transparent text-xs outline-none placeholder:text-slate-500 font-medium" 
                    />
                    {newChatSearch && (
                      <button onClick={() => setNewChatSearch('')} className="text-slate-400 hover:text-white text-xs px-1 font-bold">✕</button>
                    )}
                  </div>

                  {/* Tabs: All, Admins, Drivers, Customers, Staff/Maintenance, AI */}
                  <div className="flex items-center gap-1.5 overflow-x-auto pb-1 scrollbar-none text-xs font-bold">
                    {[
                      { id: 'all', label: 'All Contacts' },
                      { id: 'admin', label: 'Admins' },
                      { id: 'driver', label: 'Drivers' },
                      { id: 'customer', label: 'Customers' },
                      { id: 'staff', label: 'Staff & Maintenance' },
                      { id: 'ai', label: '✨ AI Copilot' }
                    ].map(t => (
                      <button
                        key={t.id}
                        onClick={() => setNewChatTab(t.id)}
                        className={`px-3 py-1.5 rounded-xl whitespace-nowrap transition-all cursor-pointer text-[11px] ${
                          newChatTab === t.id
                            ? 'bg-orange-600 text-white shadow-md'
                            : (isDark ? 'bg-white/5 text-slate-400 hover:text-white' : 'bg-slate-100 text-slate-600 hover:text-slate-900')
                        }`}
                      >
                        {t.label}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Contacts List */}
                <div className="px-4 pb-4 flex-1 overflow-y-auto chat-overlay-scrollbar flex flex-col gap-1.5">
                  {(() => {
                    const q = newChatSearch.trim().toLowerCase();
                    const seen = new Set();
                    const combined = [];

                    if (newChatTab === 'all' || newChatTab === 'ai') {
                      combined.push({
                        id: 'ai_copilot',
                        name: 'Drive&Go Copilot',
                        role: 'AI COPILOT',
                        isAi: true,
                        avatar: '⚡',
                        avatarBg: 'from-amber-500 to-orange-600',
                        email: 'ai-copilot@driveandgo.internal',
                        phone: 'Autonomous Fleet AI'
                      });
                    }

                    allContacts.forEach(c => {
                      const idStr = String(c.id);
                      if (!seen.has(idStr)) {
                        seen.add(idStr);
                        combined.push(c);
                      }
                    });

                    conversations.forEach(c => {
                      if (c.id === 'ai_copilot') return;
                      const idStr = String(c.id);
                      if (!seen.has(idStr)) {
                        seen.add(idStr);
                        combined.push(c);
                      }
                    });

                    const list = combined.filter(c => {
                      const r = (c.role || '').toUpperCase();
                      if (newChatTab === 'admin' && !r.includes('ADMIN')) return false;
                      if (newChatTab === 'driver' && !r.includes('DRIVER')) return false;
                      if (newChatTab === 'customer' && !r.includes('CUSTOMER')) return false;
                      if (newChatTab === 'staff' && !(r.includes('MAINTENANCE') || r.includes('MECHANIC') || r.includes('STAFF') || r.includes('DISPATCH') || r.includes('ACCOUNT') || r.includes('MANAGER'))) return false;
                      if (newChatTab === 'ai' && !c.isAi) return false;
                      if (!q) return true;
                      return (
                        (c.name && c.name.toLowerCase().includes(q)) ||
                        (c.role && c.role.toLowerCase().includes(q)) ||
                        (c.email && c.email.toLowerCase().includes(q)) ||
                        (c.phone && c.phone.toLowerCase().includes(q))
                      );
                    });

                    if (list.length === 0) {
                      return (
                        <div className="py-12 text-center text-xs text-slate-400 font-medium">
                          No contacts found matching your search.
                        </div>
                      );
                    }

                    return list.map(c => {
                      const isAi = c.isAi || c.id === 'ai_copilot';
                      return (
                        <div
                          key={c.id}
                          onClick={() => {
                            setActiveConvId(c.id);
                            setShowInfoPanel(false);
                            setShowNewChatModal(false);
                            setConversations(prev => {
                              if (!prev.some(x => String(x.id) === String(c.id))) {
                                return [...prev, c];
                              }
                              return prev;
                            });
                            if (!isAi) fetchThreadMessages(c.id);
                          }}
                          className={`flex items-center gap-3.5 rounded-2xl p-2.5 transition-all border cursor-pointer ${
                            isDark 
                              ? 'border-white/5 hover:border-orange-500/40 hover:bg-white/[0.04]' 
                              : 'border-slate-100 hover:border-orange-300 hover:bg-orange-50/50'
                          }`}
                        >
                          <div className={`w-11 h-11 rounded-2xl bg-gradient-to-br ${c.avatarBg || 'from-slate-700 to-slate-900'} flex items-center justify-center text-white font-black overflow-hidden shrink-0 shadow-sm ring-1 ring-white/10`}>
                            {renderAvatarIcon(c) || c.avatar || (c.name ? c.name[0] : '👤')}
                          </div>

                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <p className={`text-xs font-black truncate ${isDark ? 'text-white' : 'text-slate-900'}`}>
                                {c.name}
                              </p>
                              <span className={`text-[9px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${getRoleBadgeClasses(c.role)}`}>
                                {c.role || 'Contact'}
                              </span>
                            </div>
                            <p className={`text-[11px] font-medium truncate mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                              {c.phone || c.email || 'Registered Drive&Go User'}
                            </p>
                          </div>

                          <button
                            className="px-3.5 py-1.5 rounded-xl bg-orange-600/20 hover:bg-orange-600 text-orange-400 hover:text-white text-xs font-bold transition-all shadow-sm shrink-0 cursor-pointer"
                          >
                            Chat &rarr;
                          </button>
                        </div>
                      );
                    });
                  })()}
                </div>
              </div>
            </div>
          </Portal>
        )}

        {systemModal?.type === 'reactions' && systemModal.message && (() => {
          const msg = systemModal.message;
          const groups = getReactionGroups(msg);
          const allEntries = Object.entries(parseReactions(msg.reactions));
          const tabs = ['All', ...Object.keys(groups)];
          const shown = reactionTab === 'All' ? allEntries : allEntries.filter(([, emoji]) => emoji === reactionTab);
          return (
            <Portal>
              <div className="fixed inset-0 z-[99999] bg-black/60 backdrop-blur-sm flex items-center justify-center p-4" onClick={() => setSystemModal(null)}>
                <div className="w-full max-w-sm rounded-2xl bg-[#242526] border border-white/10 shadow-2xl overflow-hidden" onClick={(e) => e.stopPropagation()}>
                  <div className="px-5 pt-4 flex items-center justify-between">
                    <h3 className="text-lg font-bold text-white">Message reactions</h3>
                    <button onClick={() => setSystemModal(null)} className="w-8 h-8 rounded-full bg-white/10 hover:bg-white/15 text-white flex items-center justify-center text-sm font-bold cursor-pointer">×</button>
                  </div>
                  <div className="px-4 pt-3 flex gap-1 border-b border-white/10 overflow-x-auto">
                    {tabs.map(t => (
                      <button key={t} onClick={() => setReactionTab(t)} className={`px-3 py-2 text-sm font-bold border-b-2 ${reactionTab === t ? 'border-orange-500 text-orange-300' : 'border-transparent text-slate-400'}`}>
                        {t === 'All' ? `All ${allEntries.length}` : `${t} ${groups[t].length}`}
                      </button>
                    ))}
                  </div>
                  <div className="max-h-72 overflow-y-auto py-2">
                    {shown.map(([userId, emoji]) => (
                      <button
                        key={userId}
                        onClick={() => userId === 'admin' && handleReactToMessage(msg, emoji)}
                        className="w-full px-5 py-3 flex items-center gap-3 hover:bg-white/5 text-left"
                      >
                        <div className="w-9 h-9 rounded-full bg-gradient-to-br from-orange-500 to-purple-600 flex items-center justify-center text-white font-bold overflow-hidden shrink-0 border border-white/10 shadow-md">
                          {getReactionUserAvatar(userId)}
                        </div>
                        <span className="flex-1">
                          <span className="block text-sm font-bold text-white">{displayReactionUser(userId)}</span>
                          {userId === 'admin' && <span className="block text-xs text-slate-400">Click to remove</span>}
                        </span>
                        <span className="text-xl font-emoji">{emoji}</span>
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            </Portal>
          );
        })()}

        {/* ── Fullscreen Media Lightbox Modal ────────────────────────────────────── */}
        {lightboxMedia && (
          <Portal>
            <div
              className="fixed inset-0 z-[999999] bg-slate-950/90 backdrop-blur-2xl flex items-center justify-center p-4 sm:p-8 animate-in fade-in"
              onClick={() => setLightboxMedia(null)}
            >
              <div
                className="relative max-w-5xl max-h-[92vh] w-full flex flex-col items-center justify-center bg-[#0d0e1b] border border-white/15 rounded-3xl p-5 shadow-2xl overflow-hidden"
                onClick={(e) => e.stopPropagation()}
              >
                {/* Header Control Bar */}
                <div className="w-full flex items-center justify-between pb-3 mb-3 border-b border-white/10 shrink-0 px-2">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-2xl bg-orange-500/20 text-orange-400 border border-orange-500/30 flex items-center justify-center shadow-inner">
                      {lightboxMedia.type === 'video' ? (
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-orange-400">
                          <polygon points="23 7 16 12 23 17 23 7"/>
                          <rect x="1" y="5" width="15" height="14" rx="2" ry="2"/>
                        </svg>
                      ) : (
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-orange-400">
                          <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
                          <circle cx="8.5" cy="8.5" r="1.5"/>
                          <polyline points="21 15 16 10 5 21"/>
                        </svg>
                      )}
                    </div>
                    <div>
                      <h3 className="text-sm font-extrabold text-white truncate max-w-xs sm:max-w-md">
                        {lightboxMedia.title || (lightboxMedia.type === 'video' ? 'Shared Video Preview' : 'Shared Image Preview')}
                      </h3>
                      <p className="text-[10px] text-slate-400 font-medium">Click outside or press ✕ to exit preview</p>
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    {lightboxMedia.url && (
                      <a
                        href={lightboxMedia.url}
                        download
                        target="_blank"
                        rel="noreferrer"
                        className="px-3.5 py-2 rounded-xl bg-orange-600/90 hover:bg-orange-500 text-white text-xs font-bold transition-all flex items-center gap-1.5 shadow-lg cursor-pointer"
                        title="Download File"
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
                        <span>Download</span>
                      </a>
                    )}
                    <button
                      onClick={() => setLightboxMedia(null)}
                      className="w-9 h-9 rounded-full bg-white/10 hover:bg-red-500/80 text-slate-300 hover:text-white flex items-center justify-center text-xl font-bold transition-all cursor-pointer shadow-md"
                      title="Close Lightbox"
                    >
                      ×
                    </button>
                  </div>
                </div>

                {/* Main Media Container */}
                <div className="w-full h-full flex items-center justify-center overflow-hidden rounded-2xl bg-black/80 p-2 border border-white/5">
                  {lightboxMedia.type === 'video' ? (
                    <video
                      src={lightboxMedia.url}
                      controls
                      autoPlay
                      className="max-w-full max-h-[75vh] rounded-xl object-contain shadow-2xl"
                    />
                  ) : (
                    <img
                      src={lightboxMedia.url}
                      alt="Lightbox Preview"
                      className="max-w-full max-h-[75vh] rounded-xl object-contain shadow-2xl hover:scale-[1.01] transition-transform"
                    />
                  )}
                </div>
              </div>
            </div>
          </Portal>
        )}
      </div>
    </>
  );
}

if (typeof window !== 'undefined') {
  window.ChatOverlay = ChatOverlay;
}
