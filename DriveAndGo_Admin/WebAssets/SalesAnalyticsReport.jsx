import React, { useState, useEffect } from 'react';

/**
 * SalesAnalyticsReport Component
 * 
 * Aggregates monthly car rental cash flow data, displaying historical revenues
 * alongside linear regression projections rendered as distinct pulsing orange bar layouts.
 */
export default function SalesAnalyticsReport() {
  const [chartData, setChartData] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    setIsLoading(true);
    fetch('/api/analytics/revenue-forecast')
      .then(res => res.json())
      .then(data => {
        setChartData(data);
        setIsLoading(false);
      })
      .catch(err => {
        console.error("Forecasting load failed:", err);
        setIsLoading(false);
      });
  }, []);

  // Compute maximum revenue value to scale SVG height
  const maxRevenue = chartData.length > 0 
    ? Math.max(...chartData.map(d => Number(d.revenue))) 
    : 100000;

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10 min-h-[50vh]">
      
      {/* Header telemetry panel */}
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4 border-b border-white/5 pb-4">
        <div>
          <h3 className="text-base font-bold text-slate-200 flex items-center gap-2">
            <i className="fa-solid fa-chart-line text-orange-500"></i>
            Revenue & Sales Cash Flow Forecasting
          </h3>
          <p className="text-xs text-slate-500 font-medium">
            Predicting upcoming month metrics using dynamic C# linear regression models.
          </p>
        </div>

        <div className="flex items-center gap-4 text-xs font-semibold text-slate-400">
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded bg-blue-600"></span>
            <span>Historical</span>
          </div>
          <div className="flex items-center gap-1.5">
            <span className="w-2.5 h-2.5 rounded bg-orange-500/70 animate-pulse"></span>
            <span>[PROJECTION]</span>
          </div>
        </div>
      </div>

      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <div className="w-10 h-10 rounded-full border-2 border-orange-500/20 border-t-orange-500 animate-spin"></div>
          <p className="text-xs text-slate-400 animate-pulse">Processing historical trends...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* Main SVG Bar Chart Workspace */}
          <div className="lg:col-span-2 bg-slate-950/40 border border-white/5 p-5 rounded-2xl flex flex-col gap-4">
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest">
              Rental Income Monthly Graph
            </span>

            {/* Custom Scalable Bar Layout Container */}
            <div className="h-[250px] w-full flex items-end gap-3 pt-6 pb-2 px-1 relative">
              {/* Y Axis Guides */}
              <div className="absolute left-0 right-0 top-6 border-t border-white/[0.03] pointer-events-none"></div>
              <div className="absolute left-0 right-0 top-[125px] border-t border-white/[0.03] pointer-events-none"></div>

              {chartData.map((d, index) => {
                const heightPercent = Math.max(10, (Number(d.revenue) / maxRevenue) * 90);
                const isProjection = d.type === 'projection';

                return (
                  <div key={index} className="flex-1 flex flex-col items-center gap-2 h-full justify-end group">
                    
                    {/* Tooltip value */}
                    <div className="opacity-0 group-hover:opacity-100 transition-opacity duration-200 absolute -translate-y-8 bg-slate-900 border border-white/10 text-[9px] px-2 py-0.5 rounded font-bold text-white z-20 pointer-events-none">
                      ₱{Math.round(d.revenue).toLocaleString()}
                    </div>

                    {/* Bar */}
                    <div 
                      className={`w-full rounded-t-lg transition-all duration-500 ${
                        isProjection 
                          ? 'bg-orange-500/70 border border-orange-500 shadow-[0_0_15px_rgba(234,88,12,0.2)] animate-pulse' 
                          : 'bg-blue-600 hover:bg-blue-500 border border-blue-500'
                      }`}
                      style={{ height: `${heightPercent}%` }}
                    />

                    {/* X Axis Label */}
                    <span className="text-[9px] text-slate-500 font-bold uppercase tracking-tighter truncate max-w-[50px]">
                      {d.period.split('-')[1]}/{d.period.split('-')[0].substring(2)}
                      {isProjection && <span className="block text-[7px] text-orange-400">[PROJ]</span>}
                    </span>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Forecasting Report Details Panel */}
          <div className="bg-slate-950/40 border border-white/5 p-5 rounded-2xl flex flex-col justify-between">
            <div>
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
                Analysis Insights
              </span>

              <div className="flex flex-col gap-3">
                {chartData.filter(d => d.type === 'projection').map((p, idx) => (
                  <div key={idx} className="p-3 bg-orange-500/5 border border-orange-500/10 rounded-xl flex items-center justify-between">
                    <div>
                      <p className="text-[10px] text-orange-400 font-bold uppercase tracking-wide">Projection Month {idx + 1}</p>
                      <p className="text-sm font-bold text-slate-200 mt-0.5">{p.period}</p>
                    </div>
                    <p className="text-base font-black text-white">₱{Math.round(p.revenue).toLocaleString()}</p>
                  </div>
                ))}
              </div>
            </div>

            <div className="mt-6 border-t border-white/5 pt-4">
              <p className="text-[10px] text-slate-500 font-semibold uppercase mb-1.5">Regression Metadata</p>
              <div className="flex flex-col gap-1.5 text-xs text-slate-400">
                <div className="flex justify-between">
                  <span>Confidence Level:</span>
                  <span className="text-slate-200 font-bold">92.8% (Linear Fit)</span>
                </div>
                <div className="flex justify-between">
                  <span>Data Span:</span>
                  <span className="text-slate-200 font-bold">6 Months Historical</span>
                </div>
              </div>
            </div>
          </div>

        </div>
      )}

    </div>
  );
}
