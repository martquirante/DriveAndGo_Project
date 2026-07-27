import React, { useState, useEffect } from 'react';

/**
 * DiagnosticsPanel Component
 * 
 * Renders the Server Telemetry & Performance Diagnostic Panel,
 * featuring real-time latencies, thread availability trackers,
 * and memory metrics.
 */
export default function DiagnosticsPanel() {
  const [telemetry, setTelemetry] = useState(null);
  const [latencyHistory, setLatencyHistory] = useState([12, 18, 14, 22, 19, 15, 24]);
  const [healthStatus, setHealthStatus] = useState('Healthy');
  const [isPolling, setIsPolling] = useState(true);

  useEffect(() => {
    let intervalId;

    const fetchTelemetry = async () => {
      try {
        // Fetch custom telemetry
        const res = await fetch('/api/diagnostics/telemetry');
        if (res.ok) {
          const data = await res.json();
          setTelemetry(data);
          setHealthStatus(data.status);
          
          // Update database response latency curve metrics
          setLatencyHistory(prev => {
            const next = [...prev.slice(1), data.dbLatencyMs];
            return next;
          });
        }

        // Fetch official Microsoft health check state
        const healthRes = await fetch('/api/health');
        if (!healthRes.ok) {
          setHealthStatus('Unhealthy');
        }
      } catch (err) {
        console.error("Telemetry query failed:", err);
        setHealthStatus('Unhealthy');
      }
    };

    if (isPolling) {
      fetchTelemetry();
      intervalId = setInterval(fetchTelemetry, 3000);
    }

    return () => {
      if (intervalId) clearInterval(intervalId);
    };
  }, [isPolling]);

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10 min-h-[50vh]">
      
      {/* Header diagnostics panel */}
      <div className="flex items-center justify-between border-b border-white/5 pb-4">
        <div>
          <h3 className="text-base font-bold text-slate-200 flex items-center gap-2">
            <i className="fa-solid fa-terminal text-cyan-400"></i>
            Server Telemetry & System Diagnostics
          </h3>
          <p className="text-xs text-slate-500 font-medium">
            Real-time API health stats, database response latency, and active threads context.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <span className={`w-2.5 h-2.5 rounded-full animate-ping ${
            healthStatus === 'Healthy' 
              ? 'bg-emerald-500' 
              : healthStatus === 'Degraded' 
              ? 'bg-amber-500' 
              : 'bg-rose-500'
          }`}></span>
          <span className={`text-xs font-bold uppercase ${
            healthStatus === 'Healthy' 
              ? 'text-emerald-400' 
              : healthStatus === 'Degraded' 
              ? 'text-amber-400' 
              : 'text-rose-400'
          }`}>
            System {healthStatus}
          </span>
        </div>
      </div>

      {telemetry ? (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          
          {/* Latency Monitor */}
          <div className="bg-slate-900/80 border border-cyan-500/30 p-5 rounded-2xl flex flex-col gap-4 shadow-[0_0_15px_rgba(34,211,238,0.05)]">
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block">
              DB Query Response Latency
            </span>
            
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-black text-white">{telemetry.dbLatencyMs}</span>
              <span className="text-xs text-cyan-400 font-bold">ms</span>
            </div>

            {/* Custom SVG Sparkline Latency curve */}
            <div className="h-16 w-full flex items-end justify-between pt-2">
              {latencyHistory.map((val, idx) => {
                const heightPercent = Math.max(10, Math.min(90, (val / 100) * 80));
                return (
                  <div 
                    key={idx} 
                    className="w-4 bg-cyan-500/20 hover:bg-cyan-400/50 transition-colors rounded-t-sm"
                    style={{ height: `${heightPercent}%` }}
                    title={`${val} ms`}
                  />
                );
              })}
            </div>
          </div>

          {/* Core Hardware Metrics */}
          <div className="bg-slate-900/80 border border-white/5 p-5 rounded-2xl flex flex-col justify-between">
            <div>
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
                Resources Allocation
              </span>

              <div className="flex flex-col gap-3">
                <div className="flex justify-between text-xs">
                  <span className="text-slate-400">Allocated RAM:</span>
                  <span className="text-white font-extrabold">{telemetry.allocatedMemoryMb} MB</span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-slate-400">Active Threads:</span>
                  <span className="text-white font-extrabold">{telemetry.activeThreads} CPU threads</span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-slate-400">CPU load:</span>
                  <span className="text-cyan-400 font-extrabold">{telemetry.systemLoad.cpuLoadPercentage}%</span>
                </div>
              </div>
            </div>

            <div className="text-[10px] text-slate-500 mt-4 border-t border-white/5 pt-2 font-medium">
              Live updates polling: Active
            </div>
          </div>

          {/* Active Connections Context */}
          <div className="bg-slate-900/80 border border-white/5 p-5 rounded-2xl flex flex-col justify-between">
            <div>
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
                Active Client Connections
              </span>

              <div className="flex flex-col gap-3">
                <div className="flex justify-between text-xs">
                  <span className="text-slate-400">SignalR WebSockets:</span>
                  <span className="text-white font-extrabold">{telemetry.systemLoad.activeSignalRConnections} connections</span>
                </div>
                <div className="flex justify-between text-xs">
                  <span className="text-slate-400">DB Connection Pools:</span>
                  <span className="text-white font-extrabold">{telemetry.systemLoad.openDatabasePools} open pools</span>
                </div>
              </div>
            </div>

            <button 
              onClick={() => setIsPolling(!isPolling)}
              className="mt-6 w-full bg-slate-950 hover:bg-slate-900 text-slate-400 font-bold py-2 rounded-lg text-xs transition-colors border border-white/5 uppercase tracking-wider"
            >
              {isPolling ? "Pause Polling" : "Resume Polling"}
            </button>
          </div>

        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-20">
          <div className="w-10 h-10 rounded-full border-2 border-cyan-500/20 border-t-cyan-500 animate-spin"></div>
        </div>
      )}

    </div>
  );
}
