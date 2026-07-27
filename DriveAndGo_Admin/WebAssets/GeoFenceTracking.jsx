import React, { useState, useEffect } from 'react';

/**
 * GeoFenceTracking Component
 * 
 * Renders the interactive Safe Zone Geo-Fencing configuration panel,
 * simulating custom polygon/circle boundaries, active maps, and real-time compliance breaches.
 */
export default function GeoFenceTracking() {
  const [activeTool, setActiveTool] = useState('select'); // 'polygon', 'circle', 'select', 'delete'
  const [fences, setFences] = useState([
    { id: 1, name: 'Metro Manila Dispatch Core', type: 'polygon', isCompliant: true },
    { id: 2, name: 'Baguio City Terminal Buffer', type: 'circle', isCompliant: false }
  ]);

  const [vehicles, setVehicles] = useState([
    { id: 101, name: 'Nissan Navara (LND-482)', lat: 14.5995, lng: 120.9842, speed: '45 km/h', status: 'compliant' },
    { id: 102, name: 'Hyundai Tucson (ZPR-918)', lat: 16.4023, lng: 120.5960, speed: '62 km/h', status: 'breached' }
  ]);

  const [activeBreachAlert, setActiveBreachAlert] = useState(null);

  // Trigger simulated real-time breach warnings
  useEffect(() => {
    const breachedVehicle = vehicles.find(v => v.status === 'breached');
    if (breachedVehicle) {
      setActiveBreachAlert({
        vehicleName: breachedVehicle.name,
        fenceName: 'Baguio City Terminal Buffer',
        time: 'Just now'
      });
    }
  }, [vehicles]);

  const handleAddFence = (type) => {
    setActiveTool(type);
    const newFence = {
      id: Date.now(),
      name: `${type.charAt(0).toUpperCase() + type.slice(1)} Zone #${fences.length + 1}`,
      type: type,
      isCompliant: true
    };
    setFences(prev => [...prev, newFence]);
  };

  const handleDeleteFence = (id) => {
    setFences(prev => prev.filter(f => f.id !== id));
  };

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10">
      
      {/* Breach Warning Top Banner */}
      {activeBreachAlert && (
        <div className="bg-red-500/10 border border-red-500/30 p-4 rounded-xl flex items-center justify-between shadow-[0_0_15px_rgba(239,68,68,0.15)] animate-pulse">
          <div className="flex items-center gap-3">
            <div className="w-5 h-5 rounded-full bg-red-500 text-white flex items-center justify-center text-[10px] font-extrabold animate-ping">
              !
            </div>
            <div>
              <h4 className="text-xs font-bold text-red-400 uppercase tracking-wider">
                Critical Geofence Breach Active
              </h4>
              <p className="text-sm font-semibold text-white mt-0.5">
                {activeBreachAlert.vehicleName} has exited the safe boundary [{activeBreachAlert.fenceName}]!
              </p>
            </div>
          </div>
          <span className="text-[10px] text-slate-500 font-bold uppercase mr-2">
            {activeBreachAlert.time}
          </span>
        </div>
      )}

      {/* Header bar and controls */}
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4 border-b border-white/5 pb-4">
        <div>
          <h3 className="text-base font-bold text-slate-200 flex items-center gap-2">
            <i className="fa-solid fa-map-location-dot text-orange-500"></i>
            Smart Geo-Fencing & Safe Zone Tracking
          </h3>
          <p className="text-xs text-slate-500 font-medium">
            Monitor real-time compliance boundaries, sketch dynamic tracking zones, and capture breach exceptions.
          </p>
        </div>

        {/* Glassmorphic Drawing Toolbar selectors */}
        <div className="flex items-center gap-1.5 p-1 bg-slate-900/80 border border-white/5 rounded-xl backdrop-blur-md">
          <button 
            onClick={() => setActiveTool('select')}
            className={`p-2 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all ${
              activeTool === 'select' ? 'bg-orange-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <i className="fa-solid fa-arrow-pointer"></i>
            <span>Select</span>
          </button>
          <button 
            onClick={() => handleAddFence('polygon')}
            className={`p-2 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all ${
              activeTool === 'polygon' ? 'bg-orange-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <i className="fa-solid fa-draw-polygon"></i>
            <span>Draw Polygon</span>
          </button>
          <button 
            onClick={() => handleAddFence('circle')}
            className={`p-2 rounded-lg text-xs font-semibold flex items-center gap-1.5 transition-all ${
              activeTool === 'circle' ? 'bg-orange-600 text-white shadow-md' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            <i className="fa-solid fa-circle-dot"></i>
            <span>Draw Circle</span>
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left Map Viewport Container */}
        <div className="lg:col-span-2 relative h-[50vh] bg-slate-950 border border-white/5 rounded-2xl overflow-hidden flex items-center justify-center">
          
          {/* Simulated Satellite Map Grid background */}
          <div className="absolute inset-0 opacity-20 pointer-events-none bg-[radial-gradient(#1e293b_1px,transparent_1px)] [background-size:24px_24px]"></div>
          
          {/* Drawn boundaries (polygons/circles with translucent orange fill) */}
          {fences.map(f => (
            <div 
              key={f.id}
              className="absolute pointer-events-none rounded-2xl flex items-center justify-center text-[10px] font-bold text-orange-500/50 uppercase border-2 border-orange-500/35 bg-orange-500/10 shadow-[inset_0_0_20px_rgba(234,88,12,0.1)]"
              style={{
                width: f.type === 'circle' ? '180px' : '280px',
                height: f.type === 'circle' ? '180px' : '150px',
                top: f.type === 'circle' ? '10%' : '40%',
                left: f.type === 'circle' ? '50%' : '15%',
              }}
            >
              {f.name}
            </div>
          ))}

          {/* Map markers for vehicles (pulsing red for breached) */}
          {vehicles.map(v => (
            <div 
              key={v.id}
              className="absolute flex flex-col items-center cursor-pointer"
              style={{
                top: v.status === 'breached' ? '18%' : '55%',
                left: v.status === 'breached' ? '68%' : '28%'
              }}
            >
              {/* Marker Icon */}
              <div className="relative">
                {v.status === 'breached' ? (
                  <div className="relative flex h-8 w-8 items-center justify-center">
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-rose-500/40 opacity-75"></span>
                    <div className="relative w-7 h-7 rounded-full bg-rose-600 border border-white text-white flex items-center justify-center text-xs font-black shadow-[0_0_10px_#f43f5e]">
                      !
                    </div>
                  </div>
                ) : (
                  <div className="w-6 h-6 rounded-full bg-emerald-500 border border-white text-white flex items-center justify-center text-[10px] shadow-[0_0_8px_#10b981]">
                    🚗
                  </div>
                )}
              </div>
              <span className="mt-1 bg-slate-900/90 border border-white/5 text-[9px] px-2 py-0.5 rounded font-bold text-slate-200">
                {v.name.split(' ')[0]}
              </span>
            </div>
          ))}

          {/* Compass layout widget */}
          <div className="absolute bottom-4 right-4 bg-slate-900/95 border border-white/5 p-2 rounded-xl flex flex-col gap-1 text-[10px] font-semibold text-slate-400">
            <div>N: 14.5995°</div>
            <div>E: 120.9842°</div>
          </div>
        </div>

        {/* Right Active Fences and Coordinates compliance list */}
        <div className="bg-slate-950/40 border border-white/5 p-5 rounded-2xl flex flex-col justify-between">
          <div>
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
              Registered Zone Compliance
            </span>

            <div className="flex flex-col gap-3">
              {fences.map(f => (
                <div key={f.id} className="flex items-center justify-between p-3.5 bg-slate-900/50 border border-white/[0.03] rounded-xl hover:border-white/10 transition-all">
                  <div className="flex flex-col gap-0.5">
                    <p className="text-xs font-bold text-slate-200">{f.name}</p>
                    <p className="text-[10px] text-slate-500 font-semibold uppercase">{f.type} boundary</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`text-[9px] font-bold px-2 py-0.5 rounded-full ${
                      f.isCompliant ? 'bg-emerald-500/10 text-emerald-400' : 'bg-rose-500/10 text-rose-400'
                    }`}>
                      {f.isCompliant ? 'Secure' : 'Breach Exception'}
                    </span>
                    <button 
                      onClick={() => handleDeleteFence(f.id)}
                      className="text-slate-500 hover:text-rose-400 p-1 rounded"
                    >
                      <i className="fa-solid fa-trash-can text-xs"></i>
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="mt-6 border-t border-white/5 pt-4">
            <p className="text-[10px] text-slate-500 font-semibold uppercase mb-2">Live Compliance Diagnostics</p>
            <div className="flex flex-col gap-1.5 text-xs text-slate-400">
              <div className="flex justify-between">
                <span>Total Active Fences:</span>
                <span className="text-slate-200 font-bold">{fences.length}</span>
              </div>
              <div className="flex justify-between">
                <span>Compliant Fleet count:</span>
                <span className="text-emerald-400 font-bold">1 / 2</span>
              </div>
            </div>
          </div>

        </div>

      </div>

    </div>
  );
}
