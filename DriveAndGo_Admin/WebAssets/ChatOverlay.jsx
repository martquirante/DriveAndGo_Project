import React, { useState, useEffect, useRef } from 'react';

/**
 * ChatOverlay Component
 * 
 * World-Class B2B SaaS real-time chat overlay (Vercel / Linear inspired).
 * Features true Fullscreen Command Center mode, stabilized flexbox layout, 
 * locked bottom input bar, smooth custom scrollbars, and modern vector icons.
 */
export default function ChatOverlay({ isOpen = false, onClose }) {
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [activeChannelId, setActiveChannelId] = useState('driver_1');
  const [inputText, setInputText] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [typingUser, setTypingUser] = useState('');
  const messageEndRef = useRef(null);

  // Dynamic DM & Group Channel Data Matrix
  const channels = [
    { id: 'driver_1', name: 'Manuel Santos', role: 'Driver', status: 'online', isGroup: false },
    { id: 'customer_1', name: 'Sophia Loren', role: 'Customer', status: 'online', isGroup: false },
    { id: 'conductor_1', name: 'Conductor Reyes', role: 'Conductor', status: 'offline', isGroup: false },
    { id: 'group_drivers', name: 'Drivers Community GC', role: 'Group Chat', status: '24 Members', isGroup: true },
    { id: 'group_customers', name: 'Customers General Support', role: 'Group Chat', status: '150 Members', isGroup: true }
  ];

  // Chat History Logs
  const [messages, setMessages] = useState({
    driver_1: [
      { id: 1, sender: 'Manuel Santos', body: 'The passenger is boarded. Ready to dispatch Nissan LND-482.', isMine: false, time: '10:42 AM' },
      { id: 2, sender: 'Admin', body: 'Excellent. Please confirm fuel level before starting trip.', isMine: true, time: '10:44 AM' }
    ],
    customer_1: [
      { id: 1, sender: 'Sophia Loren', body: 'Can I extend my rental dropoff duration by 2 hours?', isMine: false, time: '09:12 AM' },
      { id: 2, sender: 'Admin', body: 'Sure, extensions are billed at ₱250/hr. Shall I process this?', isMine: true, time: '09:15 AM' }
    ],
    conductor_1: [
      { id: 1, sender: 'Conductor Reyes', body: 'Bus liner manifest registered. Passenger count: 42.', isMine: false, time: '08:00 AM' }
    ],
    group_drivers: [
      { id: 1, sender: 'Pedro Mendoza', body: 'Heads up: heavy traffic along EDSA northbound near Cubao.', isMine: false, time: '11:00 AM' },
      { id: 2, sender: 'Manuel Santos', body: 'Noted, copying that. Will reroute via C5.', isMine: false, time: '11:02 AM' }
    ],
    group_customers: [
      { id: 1, sender: 'System Bot', body: 'Welcome to the general support channel. Agents are ready to assist.', isMine: false, time: '08:00 AM' }
    ]
  });

  // Simulate typing indicator trigger
  useEffect(() => {
    if (!isOpen) return;
    const timer = setTimeout(() => {
      const activeChan = channels.find(c => c.id === activeChannelId);
      if (activeChan && !activeChan.isGroup) {
        setTypingUser(`${activeChan.name} is typing...`);
      }
    }, 2500);

    const clearTimer = setTimeout(() => {
      setTypingUser('');
    }, 6000);

    return () => {
      clearTimeout(timer);
      clearTimeout(clearTimer);
    };
  }, [activeChannelId, isOpen]);

  // Scroll to bottom helper
  useEffect(() => {
    messageEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, activeChannelId]);

  if (!isOpen) return null;

  const handleSend = () => {
    if (!inputText.trim()) return;
    const now = new Date();
    const timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    
    setMessages(prev => ({
      ...prev,
      [activeChannelId]: [
        ...(prev[activeChannelId] || []),
        { id: Date.now(), sender: 'Admin', body: inputText, isMine: true, time: timeStr }
      ]
    }));
    setInputText('');
    setTypingUser('');
  };

  const filteredChannels = channels.filter(c => 
    c.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
    c.role.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const activeChannel = channels.find(c => c.id === activeChannelId) || channels[0];
  const activeMessages = messages[activeChannelId] || [];

  return (
    <>
      <style>{`
        @keyframes chatSlideIn {
          0%   { opacity: 0; transform: translateX(100%); }
          60%  { opacity: 1; transform: translateX(-8px); }
          80%  { transform: translateX(4px); }
          100% { transform: translateX(0); }
        }
        @keyframes chatBubbleIn {
          from {
            opacity: 0;
            transform: translateY(10px) scale(0.95);
          }
          to {
            opacity: 1;
            transform: translateY(0) scale(1);
          }
        }
        @keyframes fadeInUp {
          from {
            opacity: 0;
            transform: translateY(14px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
        .animate-chat-slide {
          animation: chatSlideIn 0.45s cubic-bezier(0.16, 1, 0.3, 1) forwards;
        }
        .animate-chat-bubble {
          animation: fadeInUp 0.32s cubic-bezier(0.16, 1, 0.3, 1) forwards;
          opacity: 0;
        }

        /* ── Custom WebKit dark scrollbar (5px width) ────────────────────── */
        .chat-overlay-scrollbar::-webkit-scrollbar { width: 5px; }
        .chat-overlay-scrollbar::-webkit-scrollbar-track { background: transparent; }
        .chat-overlay-scrollbar::-webkit-scrollbar-thumb {
          background: rgba(255,255,255,0.12);
          border-radius: 999px;
        }
        .chat-overlay-scrollbar::-webkit-scrollbar-thumb:hover {
          background: rgba(234,88,12,0.4);
        }
      `}</style>

      {/* Dimmed Background Overlay in Fullscreen Mode */}
      {isFullscreen && (
        <div 
          onClick={() => setIsFullscreen(false)} 
          className="fixed inset-0 z-[998] bg-black/70 backdrop-blur-sm transition-opacity duration-300"
        />
      )}

      <div className={`animate-chat-slide fixed right-0 top-0 z-[999] h-screen ${
        isFullscreen 
          ? 'w-screen inset-0 bg-[#07070e] shadow-2xl' 
          : 'w-[100vw] md:w-[420px] bg-[#07070e]/80 backdrop-blur-xl border-l border-white/[0.08] shadow-[0_0_60px_rgba(0,0,0,0.9)]'
      } flex text-slate-100 font-sans overflow-hidden`}>
        
        {/* LEFT SIDEBAR NAVIGATION */}
        {(isFullscreen || !activeChannelId) && (
          <div className="w-80 border-r border-white/[0.06] flex flex-col shrink-0 bg-[#0b0b16]/80 backdrop-blur-xl">
            {/* Header */}
            <div className="p-4 border-b border-white/[0.06] flex items-center justify-between">
              <h2 className="text-sm font-bold tracking-tight text-white flex items-center gap-2">
                <span className="w-2.5 h-2.5 rounded-full bg-orange-500 shadow-[0_0_10px_#ea580c]"></span>
                DriveAndGo Hubs
              </h2>
              {isFullscreen && (
                <button 
                  onClick={() => setIsFullscreen(false)} 
                  className="text-[11px] bg-white/[0.04] border border-white/[0.08] hover:bg-white/[0.08] text-slate-300 px-2.5 py-1 rounded-lg transition-all duration-200 cursor-pointer"
                >
                  Exit Fullscreen
                </button>
              )}
            </div>

            {/* Modern Search Bar */}
            <div className="p-3 border-b border-white/[0.04]">
              <div className="flex items-center gap-2 bg-white/[0.04] border border-white/[0.06] rounded-full px-3.5 py-1.5 focus-within:border-orange-500/40 transition-all">
                <svg className="w-3.5 h-3.5 text-slate-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Search channels & DMs..."
                  className="w-full bg-transparent text-xs text-slate-300 outline-none placeholder-slate-500"
                />
              </div>
            </div>
            
            {/* Channel list */}
            <div className="flex-1 overflow-y-auto p-2.5 flex flex-col gap-1 chat-overlay-scrollbar">
              {filteredChannels.map((chan) => (
                <button
                  key={chan.id}
                  onClick={() => setActiveChannelId(chan.id)}
                  className={`w-full flex items-center gap-3 p-2.5 rounded-xl text-left transition-all duration-200 cursor-pointer ${
                    activeChannelId === chan.id 
                      ? 'bg-white/[0.09] border border-white/10 text-white shadow-lg' 
                      : 'bg-transparent text-slate-400 hover:bg-white/[0.04] hover:text-slate-200'
                  }`}
                >
                  <div className="relative shrink-0">
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-white text-xs ${
                      chan.isGroup ? 'bg-indigo-600/80' : 'bg-white/[0.08] border border-white/10'
                    }`}>
                      {chan.name[0]}
                    </div>
                    {!chan.isGroup && (
                      <span className={`absolute bottom-0 right-0 w-2 h-2 rounded-full border border-[#07070e] ${
                        chan.status === 'online' ? 'bg-emerald-500' : 'bg-slate-500'
                      }`}></span>
                    )}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-xs font-semibold truncate text-slate-200">{chan.name}</p>
                    <p className="text-[10px] text-slate-400 font-medium truncate">{chan.role}</p>
                  </div>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* RIGHT SIDE: MESSAGE CONTAINER PANEL */}
        <div className="flex-1 flex flex-col h-full min-w-0 bg-[#07070e]/80 backdrop-blur-xl">
          
          {/* Chat Thread Header */}
          <div className="h-14 border-b border-white/[0.06] px-5 flex items-center justify-between shrink-0 bg-[#0b0b16]/80 backdrop-blur-xl">
            <div className="flex items-center gap-3 min-w-0">
              <div className="w-7 h-7 rounded-full bg-gradient-to-tr from-orange-600 to-amber-500 flex items-center justify-center text-xs font-bold text-white shadow-[0_0_10px_rgba(234,88,12,0.4)] shrink-0">
                {activeChannel.name[0]}
              </div>
              <div className="flex flex-col min-w-0">
                <div className="flex items-center gap-2">
                  <h3 className="text-xs font-bold text-slate-100 truncate">{activeChannel.name}</h3>
                  <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${
                    activeChannel.status === 'online' || activeChannel.isGroup ? 'bg-emerald-500' : 'bg-slate-500'
                  }`}></span>
                </div>
                <p className="text-[9px] text-slate-400 font-semibold uppercase tracking-wider truncate">
                  {activeChannel.role} • {activeChannel.status}
                </p>
              </div>
            </div>
            
            <div className="flex items-center gap-1 shrink-0">
              <button 
                onClick={() => setIsFullscreen(!isFullscreen)} 
                className="p-2 hover:bg-white/[0.08] text-slate-400 hover:text-white rounded-lg transition-all duration-200 cursor-pointer"
                title={isFullscreen ? "Exit Fullscreen Mode" : "Expand to Fullscreen Mode"}
              >
                {isFullscreen ? (
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 9L4 4m0 0l5 0m-5 0l0 5m11 5l5 5m0 0l-5 0m5 0l0-5M9 15l-5 5m0 0l5 0m-5 0l0-5m11-11l5-5m0 0l-5 0m5 0l0 5" />
                  </svg>
                ) : (
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
                  </svg>
                )}
              </button>
              
              <button 
                onClick={onClose} 
                className="p-2 hover:bg-white/[0.08] text-slate-400 hover:text-white rounded-lg transition-all duration-200 cursor-pointer"
                title="Close chat panel"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>

          {/* Message bubbles thread flow with smooth @keyframes fadeInUp entrance */}
          <div className="flex-1 overflow-y-auto p-4 flex flex-col gap-3 chat-overlay-scrollbar" style={{ minHeight: 0 }}>
            {activeMessages.map((msg, idx) => (
              <div 
                key={msg.id} 
                className={`animate-chat-bubble flex flex-col max-w-[85%] ${
                  msg.isMine ? 'self-end items-end' : 'self-start items-start'
                }`}
                style={{ animationDelay: `${idx * 40}ms` }}
              >
                <span className="text-[9px] text-slate-400 font-semibold mb-0.5 px-1">{msg.sender}</span>
                <div className={`p-3.5 rounded-2xl text-xs font-medium leading-relaxed shadow-sm transition-all duration-200 ${
                  msg.isMine 
                    ? 'bg-gradient-to-r from-orange-600 to-amber-600 text-white rounded-tr-none shadow-[0_4px_16px_rgba(234,88,12,0.25)]'
                    : 'bg-[#121324]/90 border border-white/10 text-slate-200 rounded-tl-none hover:border-white/20'
                }`}>
                  {msg.body}
                </div>
                <span className="text-[8px] text-slate-500 font-medium mt-1 px-1">{msg.time}</span>
              </div>
            ))}
            <div ref={messageEndRef} />
          </div>

          {/* Typing indicator bubble */}
          {typingUser && (
            <div className="px-4 py-2 flex items-center gap-2 text-[10px] text-orange-400/90 italic font-medium animate-pulse shrink-0">
              <span className="w-1.5 h-1.5 rounded-full bg-orange-500 animate-bounce" style={{ animationDelay: '0ms' }}></span>
              <span className="w-1.5 h-1.5 rounded-full bg-orange-500 animate-bounce" style={{ animationDelay: '150ms' }}></span>
              <span className="w-1.5 h-1.5 rounded-full bg-orange-500 animate-bounce" style={{ animationDelay: '300ms' }}></span>
              <span>{typingUser}</span>
            </div>
          )}

          {/* Input Bar — sleek floating pill-shaped container locked to bottom */}
          <div className="p-3.5 border-t border-white/[0.06] bg-[#0b0b16]/90 backdrop-blur-xl shrink-0" style={{ flexShrink: 0, marginTop: 'auto' }}>
            <div className="flex items-center gap-2 bg-[#121324]/90 border border-white/10 rounded-full px-4 py-2 shadow-lg focus-within:border-orange-500/50 transition-all duration-300">
              <input
                type="text"
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                placeholder={`Message ${activeChannel.name}...`}
                className="flex-1 bg-transparent border-none text-xs text-slate-100 outline-none placeholder-slate-500"
              />
              <button 
                onClick={handleSend}
                className="w-8 h-8 flex items-center justify-center bg-gradient-to-r from-orange-600 to-amber-500 hover:brightness-110 active:scale-95 rounded-full text-white shadow-[0_0_12px_rgba(234,88,12,0.4)] transition-all duration-200 cursor-pointer shrink-0"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                </svg>
              </button>
            </div>
          </div>

        </div>

    </>
  );
}
