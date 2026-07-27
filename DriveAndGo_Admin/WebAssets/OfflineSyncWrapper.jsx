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

  // Update status indicators and listeners
  useEffect(() => {
    const handleOnline = () => {
      setIsOnline(true);
      triggerBackgroundSync();
    };

    const handleOffline = () => {
      setIsOnline(false);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [queue]);

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
      
      {/* Dynamic Offline Status Banner */}
      {!isOnline && (
        <div className="sticky top-0 z-50 bg-amber-500/10 border-b border-amber-500/20 px-6 py-2.5 flex items-center justify-between shadow-[0_4px_15px_rgba(245,158,11,0.1)] animate-pulse">
          <div className="flex items-center gap-2">
            <i className="fa-solid fa-cloud-slash text-amber-500 text-sm"></i>
            <span className="text-xs font-bold text-slate-200">
              Working Offline — Changes are being saved locally ({queue.length} pending logs)
            </span>
          </div>
          <span className="text-[9px] bg-amber-500/20 text-amber-400 font-extrabold px-2.5 py-0.5 rounded-full uppercase">
            Local Mode
          </span>
        </div>
      )}

      {/* Sync State Floating Notification */}
      {syncStatus === 'syncing' && (
        <div className="fixed bottom-6 right-6 z-50 bg-orange-600 border border-orange-500 p-4 rounded-xl flex items-center gap-3 shadow-lg">
          <i className="fa-solid fa-spinner animate-spin text-white"></i>
          <span className="text-xs font-bold text-white">Syncing offline queue data...</span>
        </div>
      )}

      {syncStatus === 'success' && (
        <div className="fixed bottom-6 right-6 z-50 bg-emerald-600 border border-emerald-500 p-4 rounded-xl flex items-center gap-3 shadow-lg">
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
