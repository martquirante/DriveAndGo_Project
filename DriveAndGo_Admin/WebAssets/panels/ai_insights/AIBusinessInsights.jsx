import React, { useState, useEffect } from 'react';

/**
 * AIBusinessInsights Component
 * 
 * Handles dynamic Markdown text streaming, neon glowing pulse animations,
 * and typewriter chunk fade-in loops with clean native styling.
 */
export default function AIBusinessInsights({ streamData = '', isLoading = false }) {
  const [displayText, setDisplayText] = useState('');
  const [currentIndex, setCurrentIndex] = useState(0);
  const [internalLoading, setInternalLoading] = useState(false);
  const [insightsContent, setInsightsContent] = useState('');

  // 1. Fetch data on mount if no streamData is passed
  useEffect(() => {
    if (!streamData) {
      setInternalLoading(true);
      fetch('/api/analytics/ai-summary')
        .then(res => res.json())
        .then(data => {
          setInsightsContent(data.content || '');
          setInternalLoading(false);
        })
        .catch(err => {
          console.error("AI Summary error:", err);
          setInternalLoading(false);
        });
    } else {
      setInsightsContent(streamData);
    }
  }, [streamData]);

  // 2. Simulated Typewriter Streaming Effect for Markdown Chunks
  useEffect(() => {
    if (!insightsContent) return;
    
    setDisplayText('');
    setCurrentIndex(0);
    
    let index = 0;
    const interval = setInterval(() => {
      if (index < insightsContent.length) {
        // Read chunks of 4 characters to mimic steady web stream speeds
        const nextChunk = insightsContent.slice(0, index + 4);
        setDisplayText(nextChunk);
        index += 4;
      } else {
        clearInterval(interval);
      }
    }, 15);

    return () => clearInterval(interval);
  }, [insightsContent]);

  const activeLoading = isLoading || internalLoading;

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10 min-h-[50vh]">
      
      {/* Header telemetry panel */}
      <div className="flex items-center justify-between border-b border-white/5 pb-4">
        <div className="flex items-center gap-3">
          {/* Neon glowing AI pulse animation loop */}
          <div className="relative flex h-4 w-4">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-orange-500/50 opacity-75 shadow-[0_0_12px_#ea580c]"></span>
            <span className="relative inline-flex rounded-full h-4 w-4 bg-orange-500 shadow-[0_0_10px_#ea580c]"></span>
          </div>
          <div>
            <h2 className="text-base font-bold tracking-tight bg-gradient-to-r from-orange-500 via-amber-400 to-orange-600 bg-clip-text text-transparent">
              AI Operations Advisor
            </h2>
            <p className="text-[10px] text-slate-500 font-semibold uppercase tracking-wider">
              Llama-3.1 Operational Analytics Active
            </p>
          </div>
        </div>

        <span className="text-[10px] bg-slate-950 border border-white/5 text-slate-400 px-3 py-1 rounded-full uppercase tracking-wider font-bold">
          Groq Cloud
        </span>
      </div>

      {activeLoading ? (
        /* Sophisticated neon-glowing pulse loading interface */
        <div className="flex flex-col items-center justify-center py-20 gap-6">
          <div className="relative w-16 h-16 flex items-center justify-center">
            <div className="absolute inset-0 rounded-full border border-orange-500/35 bg-orange-500/5 animate-pulse shadow-[0_0_20px_rgba(234,88,12,0.25)]" />
            <i className="fa-solid fa-brain-circuit text-orange-500 text-2xl animate-bounce"></i>
          </div>
          <div className="text-center">
            <p className="text-xs font-bold text-slate-400 uppercase tracking-widest animate-pulse">
              Compiling Database Telemetry
            </p>
            <p className="text-[10px] text-slate-500 mt-1">
              Evaluating utilization indices & monthly revenue streams...
            </p>
          </div>
        </div>
      ) : (
        /* Glassmorphic card canvas showing typewriter output logs */
        <div className="bg-slate-950/40 border border-white/5 p-6 rounded-2xl flex flex-col gap-4 shadow-[0_4px_30px_rgba(0,0,0,0.3)]">
          <div className="prose prose-invert prose-sm max-w-none text-slate-300 leading-relaxed text-sm">
            {/* Simple Markdown Parser / Renderer support for headings, bold, lists */}
            {displayText.split('\n').map((line, idx) => {
              const trimmed = line.trim();
              if (trimmed.startsWith('###')) {
                return <h3 key={idx} className="text-sm font-bold text-white uppercase tracking-wider mt-4 mb-2 border-b border-white/5 pb-1">{trimmed.replace(/###/g, '').trim()}</h3>;
              }
              if (trimmed.startsWith('####')) {
                return <h4 key={idx} className="text-xs font-bold text-orange-400 uppercase tracking-widest mt-3 mb-1.5">{trimmed.replace(/####/g, '').trim()}</h4>;
              }
              if (trimmed.startsWith('*') || trimmed.startsWith('-')) {
                return (
                  <div key={idx} className="flex items-start gap-2.5 my-1.5 pl-2 group">
                    <span className="text-orange-500 mt-0.5">•</span>
                    <span className="text-slate-300 group-hover:text-white transition-colors">{trimmed.substring(1).trim()}</span>
                  </div>
                );
              }
              if (trimmed.startsWith('1.')) {
                return (
                  <div key={idx} className="flex items-start gap-2.5 my-1.5 pl-2 group">
                    <span className="text-orange-500 font-bold mt-0.5">1.</span>
                    <span className="text-slate-300 group-hover:text-white transition-colors">{trimmed.substring(2).trim()}</span>
                  </div>
                );
              }
              return (
                <p key={idx} className="my-2.5">
                  {trimmed}
                </p>
              );
            })}
          </div>

          <div className="mt-4 border-t border-white/5 pt-4 flex items-center justify-between text-[10px] text-slate-500 font-semibold">
            <span>LLM Response Complete</span>
            <span>Typewriter stream synced</span>
          </div>
        </div>
      )}

    </div>
  );
}
