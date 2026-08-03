/**
 * ChatOverlay Component — World-Class B2B SaaS Chat Overlay + Drive&Go AI
 */
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

function ChatOverlay({ initialQuery = '' }) {
  const { useState, useEffect, useRef } = React;

  const GenUiBubbleComp = typeof GenUiBubble !== 'undefined' ? GenUiBubble : (window.GenUiBubble || null);
  const AiThinkingBubbleComp = typeof AiThinkingBubble !== 'undefined' ? AiThinkingBubble : (window.AiThinkingBubble || null);
  const CommandPaletteComp = typeof CommandPalette !== 'undefined' ? CommandPalette : (window.CommandPalette || null);

  const [inputText, setInputText] = useState('');
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);

  const [aiSessionId, setAiSessionId] = useState(null);
  const [aiMessages, setAiMessages] = useState([]);
  const [suggestions, setSuggestions] = useState([]);
  const [isAiLoading, setIsAiLoading] = useState(false);

  const messageEndRef = useRef(null);
  const chatScrollContainerRef = useRef(null);
  const adminUserId = 1;

  const scrollToBottom = (instant = true) => {
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
  };

  const apiBase = (window.API_BASE_URL || 'http://localhost:5233').replace(/\/$/, '');

  function sanitizeNonTechText(text) {
    if (!text || typeof text !== 'string') return text;
    const lower = text.toLowerCase();
    if (lower.includes('.env') || lower.includes('api key') || lower.includes('groq') || lower.includes('gemini api') || lower.includes('quotaexhausted') || lower.includes('rate limit') || lower.includes('limitasyon sa sistema')) {
      return 'Sorry, this data is temporarily unavailable right now. Please try asking your question again in a moment.';
    }
    return text;
  }

  useEffect(() => {
    async function initAi() {
      // Track if persistent history loaded OK — can't use aiMessages.length here
      // because React state updates are async and it will always read 0 (stale closure).
      let chatHistoryLoaded = false;

      // 1. Fetch persistent chat history from PostgreSQL (chat_messages table)
      try {
        const chatRes = await fetch(`${apiBase}/api/messages?senderId=admin&receiverId=ai_copilot`);
        if (chatRes.ok) {
          const chatList = await chatRes.json();
          if (chatList && chatList.length > 0) {
            const formatted = chatList.map(m => {
              let bodyText = m.messageBody || '';
              let uiComp = 'Text Only';
              let uiData = [];

              // AI responses can be JSON blobs containing { text, ui_component, data }
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

              return {
                id: m.messageId,
                sender: m.senderId === 'admin' ? 'Admin' : 'Drive&Go AI',
                body: bodyText,
                isMine: m.senderId === 'admin',
                time: formatLocalTime(m.timestamp),
                ui_component: uiComp,
                data: uiData
              };
            });
            setAiMessages(formatted);
            chatHistoryLoaded = true;
            setTimeout(() => scrollToBottom(true), 20);
            setTimeout(() => scrollToBottom(true), 150);
            setTimeout(() => scrollToBottom(true), 400);
            console.log(`[ChatOverlay] Loaded ${formatted.length} messages from DB.`);
          }
        }
      } catch (err) {
        console.warn('[ChatOverlay] Persistent chat history fetch warning:', err);
      }

      // 2. Init/fetch AI session (for context window only — not for display history)
      try {
        const res = await fetch(`${apiBase}/api/ai/sessions?adminUserId=${adminUserId}`);
        if (res.ok) {
          const sessions = await res.json();
          if (sessions && sessions.length > 0) {
            const latestSession = sessions[0];
            setAiSessionId(latestSession.sessionId);
            // Only fall back to session history if chat_messages returned nothing
            if (!chatHistoryLoaded) {
              loadHistory(latestSession.sessionId);
            }
          } else {
            createSession();
          }
        } else {
          createSession();
        }
      } catch (err) {
        createSession();
      }

      try {
        const sugRes = await fetch(`${apiBase}/api/ai/suggestions`);
        if (sugRes.ok) {
          const sugData = await sugRes.json();
          if (sugData.suggestions) {
            setSuggestions(sugData.suggestions);
          }
        }
      } catch (err) {}
    }
    initAi();
  }, []);


  useEffect(() => {
    if (initialQuery) {
      handleSendAiMessage(initialQuery);
    }
  }, [initialQuery]);

  async function createSession() {
    try {
      const res = await fetch(`${apiBase}/api/ai/sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ adminUserId, title: 'AI Operational Session' })
      });
      if (res.ok) {
        const data = await res.json();
        setAiSessionId(data.sessionId);
        if (aiMessages.length === 0) loadHistory(data.sessionId);
      }
    } catch (err) {}
  }

  async function loadHistory(sid) {
    try {
      const res = await fetch(`${apiBase}/api/ai/sessions/${sid}/history`);
      if (res.ok) {
        const history = await res.json();
        const formatted = history.map(m => {
          let bodyText = m.content || '';
          let uiComp = m.uiComponentType;
          let uiData = m.uiPayload;

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

          return {
            id: m.copilotMsgId,
            sender: m.llmRole === 'user' ? 'Admin' : 'Drive&Go AI',
            body: bodyText,
            isMine: m.llmRole === 'user',
            time: formatLocalTime(m.sentAt),
            ui_component: uiComp,
            data: uiData,
            providerUsed: m.providerUsed
          };
        });
        setAiMessages(formatted);
        setTimeout(() => scrollToBottom(true), 20);
        setTimeout(() => scrollToBottom(true), 150);
        setTimeout(() => scrollToBottom(true), 400);
      }
    } catch (err) {}
  }

  useEffect(() => {
    scrollToBottom(true);
    const delays = [10, 50, 150, 300, 500, 800, 1200];
    const timers = delays.map(d => setTimeout(() => scrollToBottom(true), d));

    let observer;
    if (chatScrollContainerRef.current && typeof ResizeObserver !== 'undefined') {
      const el = chatScrollContainerRef.current;
      observer = new ResizeObserver(() => {
        el.scrollTop = el.scrollHeight + 99999;
      });
      observer.observe(el);
      Array.from(el.children).forEach(c => observer.observe(c));
    }

    return () => {
      timers.forEach(t => clearTimeout(t));
      if (observer) observer.disconnect();
    };
  }, [aiMessages, isAiLoading]);

  const handleSend = () => {
    if (!inputText.trim()) return;
    const msgText = inputText.trim();
    setInputText('');
    handleSendAiMessage(msgText);
  };

  const handleSendAiMessage = async (userMessage) => {
    if (!userMessage) return;

    let currentSid = aiSessionId;
    if (!currentSid) {
      try {
        const sRes = await fetch(`${apiBase}/api/ai/sessions`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ adminUserId, title: 'AI Operational Session' })
        });
        if (sRes.ok) {
          const sData = await sRes.json();
          currentSid = sData.sessionId;
          setAiSessionId(currentSid);
        }
      } catch (err) {}
    }

    const nowStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const userMsgId = Date.now();

    const userBubble = {
      id: userMsgId,
      sender: 'Admin',
      body: userMessage,
      isMine: true,
      time: nowStr,
      status: 'sending'
    };
    setAiMessages(prev => [...prev, userBubble]);
    setIsAiLoading(true);

    try {
      const res = await fetch(`${apiBase}/api/ai/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: currentSid || 1,
          adminUserId,
          userMessage
        })
      });

      if (res.ok) {
        const data = await res.json();
        setAiMessages(prev => prev.map(m => m.isMine ? { ...m, status: 'seen' } : m));

        const aiBubble = {
          id: data.messageId || Date.now(),
          sender: 'Drive&Go AI',
          body: sanitizeNonTechText(data.text),
          isMine: false,
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          ui_component: data.ui_component,
          data: data.data,
          providerUsed: data.providerUsed
        };
        setAiMessages(prev => [...prev, aiBubble]);
      } else {
        setAiMessages(prev => prev.map(m => m.id === userMsgId ? { ...m, status: 'sent' } : m));
        const errBubble = {
          id: Date.now(),
          sender: 'Drive&Go AI',
          body: '⚠️ Unable to process request.',
          isMine: false,
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          ui_component: 'Text Only',
          data: []
        };
        setAiMessages(prev => [...prev, errBubble]);
      }
    } catch (err) {
      setAiMessages(prev => prev.map(m => m.id === userMsgId ? { ...m, status: 'sent' } : m));
      setAiMessages(prev => [
        ...prev,
        {
          id: Date.now(),
          sender: 'Drive&Go AI',
          body: '⚠️ Network connection issue.',
          isMine: false,
          time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
          ui_component: 'Text Only',
          data: []
        }
      ]);
    } finally {
      setIsAiLoading(false);
    }
  };

  return (
    <>
      {CommandPaletteComp && (
        <CommandPaletteComp
          isOpen={isCommandPaletteOpen}
          onClose={() => setIsCommandPaletteOpen(false)}
          onOpenAiCopilot={(q) => handleSendAiMessage(q)}
        />
      )}

      <style>{`
        :root, [data-theme="dark"] {
          --bg-color: #07070e;
          --bg-panel: rgba(18,19,36,0.9);
          --text-main: #f1f5f9;
          --text-muted: #94a3b8;
          --border: rgba(255,255,255,0.1);
          --bubble-ai: rgba(18,19,36,0.9);
        }
        [data-theme="light"] {
          --bg-color: #f8fafc;
          --bg-panel: #ffffff;
          --text-main: #0f172a;
          --text-muted: #64748b;
          --border: #e2e8f0;
          --bubble-ai: #ffffff;
        }
        @keyframes chatBubbleIn {
          from { opacity: 0; transform: translateY(10px) scale(0.95); }
          to   { opacity: 1; transform: translateY(0) scale(1); }
        }
        @keyframes fadeInUp {
          from { opacity: 0; transform: translateY(14px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        .animate-chat-bubble { animation: fadeInUp 0.32s cubic-bezier(0.16, 1, 0.3, 1) forwards; }
        .chat-overlay-scrollbar {
          color-scheme: dark !important;
          scrollbar-width: thin;
          scrollbar-color: rgba(255, 255, 255, 0.18) transparent;
        }
        .chat-overlay-scrollbar::-webkit-scrollbar { width: 5px !important; background: transparent !important; }
        .chat-overlay-scrollbar::-webkit-scrollbar-track { background: transparent !important; }
        .chat-overlay-scrollbar::-webkit-scrollbar-thumb {
          background: rgba(255,255,255,0.16) !important;
          border-radius: 999px !important;
        }
        .chat-overlay-scrollbar::-webkit-scrollbar-thumb:hover {
          background: rgba(234, 88, 12, 0.6) !important;
        }
        @keyframes fadeSlideUp {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
      `}</style>

      <div className="w-full h-full flex flex-col bg-[var(--bg-color)] text-[var(--text-main)] font-sans overflow-hidden">
        
        {/* Message Thread Flow */}
        <div ref={chatScrollContainerRef} className="flex-1 overflow-y-auto p-4 flex flex-col gap-3.5 chat-overlay-scrollbar" style={{ minHeight: 0, overflowAnchor: 'none', overscrollBehavior: 'none' }}>
          {aiMessages.length === 0 && !isAiLoading && (
            <div className="my-auto text-center flex flex-col items-center justify-center p-6 bg-[var(--bg-panel)] border border-[var(--border)] rounded-2xl mx-auto w-full max-w-sm">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-orange-500 to-violet-600 flex items-center justify-center text-xl shadow-[0_0_20px_rgba(234,88,12,0.4)] mb-3">
                ✨
              </div>
              <h4 className="text-sm font-bold text-[var(--text-main)] mb-1">Drive&Go AI</h4>
              <p className="text-xs text-[var(--text-muted)] leading-relaxed">
                Ask anything about revenues, overdue rentals, fleet status, driver ratings, or operational insights.
              </p>
            </div>
          )}

          {aiMessages.map((msg) => (
            GenUiBubbleComp ? <GenUiBubbleComp key={msg.id} message={msg} /> : null
          ))}

          {isAiLoading && AiThinkingBubbleComp && <AiThinkingBubbleComp />}
          
          <div ref={messageEndRef} />
        </div>

        {/* INPUT BAR AREA */}
        <div className="p-3 border-t border-[var(--border)] bg-transparent shrink-0">
          
          {/* ── Smart Autocomplete Engine (fuzzy scoring, EN + TL) ── */}
          {(() => {
            const suggestionBank = [
              // ── 💰 REVENUE & SALES (KITA AT BENTA) ──
              { text: "📊 Magkano ang kita natin ngayong buwan?", query: "Show me the monthly revenue trend for the last 6 months.", keywords: ["revenue","monthly","trend","kita","buwanan","kinikita","income","earnings","sales","buwan","pera","sweldo","kinita","magkano","kumita","profit","benta","gaano","ilang","month","6"] },
              { text: "📊 Show me the monthly revenue trend for the last 6 months.", query: "Show me the monthly revenue trend for the last 6 months.", keywords: ["revenue","monthly","trend","income","earnings","sales","profit","month","6","last"] },
              { text: "💵 Magkano ang kita natin ngayong araw?", query: "Show today's revenue breakdown.", keywords: ["today","revenue","breakdown","ngayon","araw","kita","daily","magkano","kumita","earnings","dito","kahapon","pera","sweldo","ilang","this day"] },
              { text: "💵 Show today's revenue breakdown.", query: "Show today's revenue breakdown.", keywords: ["today","revenue","breakdown","daily","earnings","income","sales"] },
              { text: "📈 Hulaan ang magiging sales sa susunod na taon.", query: "Predict next year's sales.", keywords: ["predict","forecast","next year","hulaan","projection","future","susunod","taon","prediksyon","hula","estimate","ano","magiging","mangyayari","paano","growth","year","next","bukas","sales"] },
              { text: "📈 Predict next year's sales.", query: "Predict next year's sales.", keywords: ["predict","forecast","next year","projection","future","estimate","growth","year","sales"] },
              { text: "📅 Ipakita ang weekly revenue analytics.", query: "Show me the weekly revenue analytics.", keywords: ["weekly","week","lingguhan","analytics","linggo","7 day","revenue","kita","this week","nitong linggo","past week","week analytics","pitong araw"] },
              { text: "📅 Show me the weekly revenue analytics.", query: "Show me the weekly revenue analytics.", keywords: ["weekly","week","analytics","revenue","earnings","income","sales"] },

              // ── 🚗 FLEET & VEHICLES (SASAKYAN AT FLEET STATUS) ──
              { text: "🚗 Ilan ang magagamit na sasakyan ngayon?", query: "Show me the fleet status breakdown.", keywords: ["fleet","status","available","sasakyan","vehicle","car","kotse","breakdown","magkano","ilan","ilang","libre","bakante","gamit","naka","available","nasa","out","in","how many"] },
              { text: "🚗 Show me the fleet status breakdown.", query: "Show me the fleet status breakdown.", keywords: ["fleet","status","available","vehicle","car","breakdown","count","available","how many"] },
              { text: "🏆 Aling mga sasakyan ang may pinakamalaking kita?", query: "Which vehicles are the top earners this month?", keywords: ["vehicle","top earner","kotse","sasakyan","car","fleet","earner","kumita","pinakamalaki","malaki","anong","alin","pinakamarami","top","earning","most","sasakyan","unit"] },
              { text: "🏆 Which vehicles are the top earners this month?", query: "Which vehicles are the top earners this month?", keywords: ["vehicle","top earner","car","fleet","earner","earnings","top","most"] },
              { text: "🔧 May mga sira ba o maintenance alert sa mga kotse?", query: "What are the current fleet maintenance alerts?", keywords: ["maintenance","alert","repair","oil","tire","fleet","ayos","pagkukumpuni","sira","service","kumpuni","change oil","gulong","check","checkup","inspect","vehicle","fix","nasira","palitan"] },
              { text: "🔧 What are the current fleet maintenance alerts?", query: "What are the current fleet maintenance alerts?", keywords: ["maintenance","alert","repair","oil","tire","fleet","service","check","checkup","inspect","vehicle","fix"] },
              { text: "🚘 Ilang active rentals ang mayroon ngayon?", query: "How many active rentals are there right now?", keywords: ["active","rental","current","now","kasalukuyan","upa","ongoing","ilan","how many","magkano","ngayon","naka rent","renta","gamit","lahatan","bilang","count","gaano karami"] },
              { text: "🚘 How many active rentals are there right now?", query: "How many active rentals are there right now?", keywords: ["active","rental","current","now","ongoing","how many","count"] },
              { text: "⛽ Check fuel anomaly at mileage consumption.", query: "Check fuel anomaly and mileage consumption.", keywords: ["fuel","gas","gasolina","anomaly","mileage","consumption","patak","bawas","konsumo","check","anomalya"] },
              { text: "⛽ Check fuel anomaly and mileage consumption.", query: "Check fuel anomaly and mileage consumption.", keywords: ["fuel","gas","anomaly","mileage","consumption","check"] },

              // ── ⚠️ OVERDUE & PENALTIES (LATE SA PAG-SOLI AT MULTA) ──
              { text: "⚠️ Ipakita ang listahan ng overdue rentals at multa.", query: "List all overdue rentals with penalty estimates.", keywords: ["overdue","penalty","late","lampas","multa","bayad","huli","lagpas","di pa bumalik","hindi pa","rental","nag late","past due","di nagsauli","palya","siningil","charge","upa","renta"] },
              { text: "⚠️ List all overdue rentals with penalty estimates.", query: "List all overdue rentals with penalty estimates.", keywords: ["overdue","penalty","late","multa","rental","past due","charge"] },

              // ── 📋 BOOKINGS & APPROVALS (RESERBA AT DISPATCH) ──
              { text: "📋 Mga pending bookings na kailangan ng approval.", query: "List the pending bookings that need my approval.", keywords: ["pending","booking","approval","approve","hinihintay","pag-approve","waiting","request","pahintulot","approve","need","action","aksyon","kailangan","book","reserve","naghihintay","paki"] },
              { text: "📋 List the pending bookings that need my approval.", query: "List the pending bookings that need my approval.", keywords: ["pending","booking","approval","approve","waiting","request","action"] },
              { text: "⚡ Auto-assign driver at sasakyan sa booking.", query: "Auto-assign driver and vehicle to a booking.", keywords: ["assign","dispatch","auto","booking","driver","vehicle","ilagay","auto assign","ibigay","i-dispatch","rental","assign driver","assign vehicle","lagay"] },
              { text: "⚡ Auto-assign driver and vehicle to a booking.", query: "Auto-assign driver and vehicle to a booking.", keywords: ["assign","dispatch","auto","booking","driver","vehicle","assign driver","assign vehicle"] },

              // ── 👨‍✈️ DRIVERS & RATINGS (MGA DRAYBER) ──
              { text: "⭐ Sino ang top 5 drivers batay sa rating?", query: "Who are the top 5 drivers by rating?", keywords: ["driver","top","rating","best","pinakamahusay","drayber","star","performer","rank","sino","magaling","galing","best","mahusay","husay","nangunguna","rated","top 5","piloto"] },
              { text: "⭐ Who are the top 5 drivers by rating?", query: "Who are the top 5 drivers by rating?", keywords: ["driver","top","rating","best","drayber","star","performer","rank","rated","top 5"] },

              // ── 📊 BUSINESS OVERVIEW & SURGE PRICING (PANGKALAHATAN) ──
              { text: "🏥 Ano ang business health summary ngayong araw?", query: "Give me a business health summary for today.", keywords: ["health","summary","business","status","kalagayan","buod","overview","dashboard","report","kamusta","estado","lagay","overall","ops","operations","ano","paano","update","brief","bukas"] },
              { text: "🏥 Give me a business health summary for today.", query: "Give me a business health summary for today.", keywords: ["health","summary","business","status","overview","dashboard","report","overall","operations","update","brief"] },
              { text: "🔥 Suriin ang kasalukuyang surge pricing rates.", query: "Check the current surge pricing rates.", keywords: ["surge","pricing","dynamic","rate","presyo","taas","price","multiplier","mahal","patong","dagdag","singil","magkano","bayarin","increase","rate","charge","weekend"] },
              { text: "🔥 Check the current surge pricing rates.", query: "Check the current surge pricing rates.", keywords: ["surge","pricing","dynamic","rate","price","multiplier","increase","charge"] }
            ];

            const q = inputText.toLowerCase().trim();
            if (q.length < 2) return null;

            // ── Fuzzy Scoring Algorithm ──
            const userTokens = q.split(/\s+/).filter(t => t.length >= 2);
            if (userTokens.length === 0) return null;

            const scored = suggestionBank.map(suggestion => {
              let score = 0;

              for (const token of userTokens) {
                for (const kw of suggestion.keywords) {
                  if (kw === token) { score += 10; continue; }
                  if (kw.startsWith(token)) { score += 7; continue; }
                  if (token.startsWith(kw)) { score += 6; continue; }
                  if (kw.includes(token)) { score += 4; continue; }
                  if (token.includes(kw) && kw.length >= 3) { score += 3; continue; }
                  if (token.length >= 4 && kw.length >= 4) {
                    let diff = 0;
                    const minLen = Math.min(token.length, kw.length);
                    for (let c = 0; c < minLen; c++) {
                      if (token[c] !== kw[c]) diff++;
                    }
                    diff += Math.abs(token.length - kw.length);
                    if (diff <= 1) { score += 5; }
                    else if (diff <= 2) { score += 2; }
                  }
                }
                const sugLower = suggestion.text.toLowerCase();
                if (sugLower.includes(token)) { score += 3; }
              }

              const coveredTokens = userTokens.filter(t =>
                suggestion.keywords.some(kw => kw.includes(t) || t.includes(kw)) ||
                suggestion.text.toLowerCase().includes(t)
              ).length;
              score += coveredTokens * 2;

              return { ...suggestion, score };
            });

            const topMatches = scored
              .filter(s => s.score > 0)
              .sort((a, b) => b.score - a.score)
              .slice(0, 4);

            if (topMatches.length === 0) return null;

            return (
              <div className="flex flex-col gap-1.5 mb-2">
                {topMatches.map((sug, i) => (
                  <button
                    key={i}
                    onClick={() => { setInputText(''); handleSendAiMessage(sug.query || sug.text); }}
                    className="w-full text-left text-[11px] font-medium text-[var(--text-muted)] hover:text-orange-300 bg-[var(--bg-panel)]/60 hover:bg-orange-500/10 border border-[var(--border)] hover:border-orange-500/30 px-3 py-2 rounded-xl transition-all cursor-pointer flex items-center gap-2 backdrop-blur-sm"
                    style={{ animation: `fadeSlideUp 0.15s ease-out ${i * 0.05}s both` }}
                  >
                    <svg className="w-3.5 h-3.5 text-amber-500 shrink-0" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
                    </svg>
                    <span>{sug.text}</span>
                  </button>
                ))}
              </div>
            );
          })()}

          {/* Input pill */}
          <div className="flex items-center gap-2 bg-[var(--bg-panel)] border border-[var(--border)] rounded-full px-4 py-2 shadow-lg focus-within:border-orange-500/50 transition-all">
            <button
              onClick={() => setIsCommandPaletteOpen(true)}
              className="w-7 h-7 flex items-center justify-center text-amber-400 hover:text-amber-300 bg-[var(--bg-panel)] hover:brightness-125 rounded-full transition-all cursor-pointer shrink-0"
              title="Open Command Palette (Ctrl+K)"
            >
              <svg className="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 00-2.456 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z" />
              </svg>
            </button>
            <input
              type="text"
              value={inputText}
              onChange={(e) => setInputText(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSend()}
              placeholder="Ask Drive&Go AI about revenue, fleet, analytics..."
              className="flex-1 bg-transparent border-none text-xs text-[var(--text-main)] outline-none placeholder-[var(--text-muted)]"
            />
            <button
              onClick={handleSend}
              disabled={isAiLoading}
              className="w-8 h-8 flex items-center justify-center bg-gradient-to-r from-orange-600 to-amber-500 hover:brightness-110 active:scale-95 disabled:opacity-50 rounded-full text-white shadow-[0_0_12px_rgba(234,88,12,0.4)] transition-all cursor-pointer shrink-0"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
              </svg>
            </button>
          </div>
          {/* Disclaimer text */}
          <p className="text-[9.5px] text-[var(--text-muted)] text-center mt-1.5 opacity-60 font-medium">
            Drive&Go AI can make mistakes. Verify important business data.
          </p>
        </div>

      </div>
    </>
  );
}

if (typeof window !== 'undefined') {
  window.ChatOverlay = ChatOverlay;
}
