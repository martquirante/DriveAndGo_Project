import React, { useState, useEffect } from 'react';

/**
 * OfflineSyncWrapper Component
 * 
 * Intercepts api operations during network outages, queues requests locally,
 * and handles background synchronization routines using navigator status states.
 */
export default function OfflineSyncWrapper({ children }) {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [queue, setQueue] = useState([]);
  const [syncStatus, setSyncStatus] = useState(''); // 'idle', 'syncing', 'success', 'failed'

  // Load initial queue from localStorage
  useEffect(() => {
    const savedQueue = localStorage.getItem('driveandgo_offline_queue');
    if (savedQueue) {
      setQueue(JSON.parse(savedQueue));
    }
  }, []);

  const [wasOffline, setWasOffline] = useState(false);
  const [showRestored, setShowRestored] = useState(false);

  // Update status indicators and listeners
  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);
      if (wasOffline) {
        setShowRestored(true);
        setTimeout(() => {
          setShowRestored(false);
          setWasOffline(false);
        }, 3500);
      }
      triggerBackgroundSync();
    };

    const handleOffline = () => {
      setIsOnline(false);
      setWasOffline(true);
      setShowRestored(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [queue, wasOffline]);

  // Method to manually queue logs (used by hooks/controllers)
  const queueOfflineLog = (type, payload) => {
    const newLog = {
      idempotencyKey: `idemp-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      type,
      payload,
      timestamp: new Date().toISOString()
    };

    const updatedQueue = [...queue, newLog];
    setQueue(updatedQueue);
    localStorage.setItem('driveandgo_offline_queue', JSON.stringify(updatedQueue));
  };

  const triggerBackgroundSync = async () => {
    const savedQueue = localStorage.getItem('driveandgo_offline_queue');
    if (!savedQueue) return;

    const logs = JSON.parse(savedQueue);
    if (logs.length === 0) return;

    setSyncStatus('syncing');

    try {
      const response = await fetch('/api/fleet/sync-offline-logs', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(logs)
      });

      if (response.ok) {
        setSyncStatus('success');
        setQueue([]);
        localStorage.removeItem('driveandgo_offline_queue');
        setTimeout(() => setSyncStatus('idle'), 3000);
      } else {
        setSyncStatus('failed');
      }
    } catch (err) {
      console.error("Offline sync connection error:", err);
      setSyncStatus('failed');
    }
  };

  return (
    <div className="relative w-full h-full min-h-screen bg-[#07070e] text-slate-100">
      
      {/* 🔴 OFFLINE WARNING BANNER */}
      {!isOnline && (
        <div className="sticky top-0 z-50 bg-red-600 border-b border-red-500 px-6 py-2.5 flex items-center justify-between shadow-[0_4px_15px_rgba(220,38,38,0.3)] animate-pulse">
          <div className="flex items-center gap-2">
            <i className="fa-solid fa-cloud-slash text-white text-sm"></i>
            <span className="text-xs font-bold text-white">
              ⚠️ Working Offline — Changes are being saved locally ({queue.length} pending logs)
            </span>
          </div>
          <span className="text-[9px] bg-white/20 text-white font-extrabold px-2.5 py-0.5 rounded-full uppercase tracking-wider">
            Offline Mode
          </span>
        </div>
      )}

      {/* 🟢 INTERNET RESTORED BANNER (Spotify Style) */}
      {isOnline && showRestored && (
        <div className="sticky top-0 z-50 bg-emerald-600 border-b border-emerald-500 px-6 py-2.5 flex items-center justify-between shadow-[0_4px_15px_rgba(16,185,129,0.3)] transition-all duration-300">
          <div className="flex items-center gap-2">
            <i className="fa-solid fa-wifi text-white text-sm"></i>
            <span className="text-xs font-bold text-white">
              📶 Internet Connection Restored! You are back online.
            </span>
          </div>
          <span className="text-[9px] bg-white/20 text-white font-extrabold px-2.5 py-0.5 rounded-full uppercase tracking-wider">
            Connected
          </span>
        </div>
      )}

      {/* Sync State Floating Notification (Top-Right) */}
      {syncStatus === 'syncing' && (
        <div className="fixed top-6 right-6 z-[99999] bg-orange-600 border border-orange-500 p-4 rounded-xl flex items-center gap-3 shadow-2xl animate-in slide-in-from-top-2">
          <i className="fa-solid fa-spinner animate-spin text-white"></i>
          <span className="text-xs font-bold text-white">Syncing offline queue data...</span>
        </div>
      )}

      {syncStatus === 'success' && (
        <div className="fixed top-6 right-6 z-[99999] bg-emerald-600 border border-emerald-500 p-4 rounded-xl flex items-center gap-3 shadow-2xl animate-in slide-in-from-top-2">
          <i className="fa-solid fa-circle-check text-white"></i>
          <span className="text-xs font-bold text-white">Data synchronized successfully! Queue cleared.</span>
        </div>
      )}

      {/* Test triggers to help evaluate queue loops */}
      <div className="absolute top-20 right-6 z-40 bg-slate-950/90 border border-white/5 p-4 rounded-xl flex flex-col gap-2.5 max-w-xs shadow-2xl">
        <span className="text-[9px] font-bold text-slate-500 uppercase tracking-widest">
          Offline Pipeline Diagnostics
        </span>
        
        <div className="flex justify-between text-[10px] text-slate-400 font-semibold">
          <span>Connection:</span>
          <span className={isOnline ? 'text-emerald-400' : 'text-amber-400'}>
            {isOnline ? 'Online' : 'Offline'}
          </span>
        </div>

        <div className="flex justify-between text-[10px] text-slate-400 font-semibold">
          <span>Queue Count:</span>
          <span className="text-slate-200">{queue.length} items</span>
        </div>

        <button 
          onClick={() => queueOfflineLog('location', { rentalId: 1, vehicleId: 1, latitude: 14.5995, longitude: 120.9842 })}
          className="w-full bg-slate-900 hover:bg-slate-800 border border-white/5 font-bold py-1.5 rounded text-[10px] transition-colors"
        >
          Mock Log Location Log
        </button>

        {!isOnline && (
          <button 
            onClick={() => setIsOnline(true)}
            className="w-full bg-emerald-600 hover:bg-emerald-700 font-bold py-1.5 rounded text-[10px] text-white transition-colors"
          >
            Simulate Reconnection
          </button>
        )}
      </div>

      <div className="w-full h-full">
        {children}
      </div>

    </div>
  );
}
