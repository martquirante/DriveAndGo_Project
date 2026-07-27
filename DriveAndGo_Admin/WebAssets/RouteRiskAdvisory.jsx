import React, { useState, useEffect } from 'react';

/**
 * RouteRiskAdvisory Component
 * 
 * Renders the Weather Risk Warning Banners, Predictive Maintenance Progress Meters,
 * and Automated OCR Driver License Vault verification card.
 */
export default function RouteRiskAdvisory({ rentalId = 1, vehicleId = 1, driverId = 1 }) {
  // 1. Weather Advisory State
  const [advisory, setAdvisory] = useState({
    destination: 'Baguio',
    weatherCondition: 'Heavy Rain & Low Visibility',
    isHighRisk: true,
    warningMessage: '[⚠️ WEATHER RISK: Heavy Rain in Destination Route] Landslide warning active on Kennon Road.',
    recommendedAction: 'Postpone travel or use alternative expressways.'
  });

  // 2. Telematics Odometer State
  const [odometerData, setOdometerData] = useState({
    currentOdometer: 18450,
    lastMaintenanceOdometer: 14200,
    limit: 5000
  });

  // 3. OCR Document Vault State
  const [ocrStatus, setOcrStatus] = useState({
    fileName: '',
    isProcessing: false,
    extractedExpiry: '',
    status: 'Pending Upload',
    confidence: '',
    rejectionReason: ''
  });

  // Calculate maintenance limits
  const distanceSinceMaint = odometerData.currentOdometer - odometerData.lastMaintenanceOdometer;
  const maintProgressPercent = Math.min(100, (distanceSinceMaint / odometerData.limit) * 100);
  const isMaintenanceOverdue = distanceSinceMaint >= odometerData.limit;

  // Mock upload action simulating OCR Driver Verification
  const handleOcrUpload = (e, fileType) => {
    const file = e.target.files[0];
    if (!file) return;

    setOcrStatus(prev => ({
      ...prev,
      fileName: file.name,
      isProcessing: true,
      status: 'Parsing binary stream...'
    }));

    // Simulate OCR processing latency
    setTimeout(() => {
      const isExpiredFile = file.name.toLowerCase().includes('expired') || file.name.toLowerCase().includes('invalid');
      
      setOcrStatus({
        fileName: file.name,
        isProcessing: false,
        extractedExpiry: isExpiredFile ? '2026-07-07' : '2029-07-12',
        status: isExpiredFile ? 'Expired Credentials - Locked' : 'Verified & Active',
        confidence: isExpiredFile ? '81.4%' : '97.2%',
        rejectionReason: isExpiredFile ? 'Automated OCR Vetting: Credential expired on 2026-07-07.' : ''
      });
    }, 1500);
  };

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10">
      
      {/* SECTION 1: Weather & Route Risk Advisory Banner */}
      {advisory.isHighRisk && (
        <div className="bg-orange-500/10 backdrop-blur-md border border-orange-500/30 p-4 rounded-xl flex items-start gap-3 shadow-[0_0_15px_rgba(234,88,12,0.1)] animate-pulse">
          <div className="text-orange-500 mt-0.5 text-lg">
            <i className="fa-solid fa-triangle-exclamation"></i>
          </div>
          <div className="flex-1">
            <h4 className="text-xs font-bold uppercase tracking-wider text-orange-400">
              Active Environmental Safety Risk
            </h4>
            <p className="text-sm font-semibold text-white mt-1">
              {advisory.warningMessage}
            </p>
            <div className="mt-2 text-xs text-slate-400 flex items-center gap-1.5">
              <span className="font-bold text-orange-400">Recommended Action:</span>
              <span>{advisory.recommendedAction}</span>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        
        {/* SECTION 2: Telematics-Driven Predictive Maintenance Progress */}
        <div className="bg-slate-950/40 border border-white/5 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-3">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest">
                Odometer Telematics Monitor
              </span>
              <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                isMaintenanceOverdue ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20' : 'bg-emerald-500/10 text-emerald-400'
              }`}>
                {isMaintenanceOverdue ? 'Overdue Exception' : 'Optimal'}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-4 my-2">
              <div>
                <p className="text-[10px] text-slate-500 font-semibold uppercase">Current Odometer</p>
                <p className="text-xl font-extrabold text-white mt-0.5">
                  {odometerData.currentOdometer.toLocaleString()} km
                </p>
              </div>
              <div>
                <p className="text-[10px] text-slate-500 font-semibold uppercase">Last Serviced</p>
                <p className="text-xl font-extrabold text-slate-300 mt-0.5">
                  {odometerData.lastMaintenanceOdometer.toLocaleString()} km
                </p>
              </div>
            </div>

            {/* Visual limits progress bar */}
            <div className="mt-4">
              <div className="flex justify-between text-[10px] text-slate-400 font-medium mb-1.5">
                <span>Service Limit (5,000 km)</span>
                <span>{distanceSinceMaint.toLocaleString()} / 5,000 km</span>
              </div>
              <div className="w-full h-2 bg-slate-900 rounded-full overflow-hidden border border-white/5">
                <div 
                  className={`h-full rounded-full transition-all duration-500 ${
                    isMaintenanceOverdue ? 'bg-rose-500 shadow-[0_0_8px_#f43f5e]' : 'bg-orange-500'
                  }`}
                  style={{ width: `${maintProgressPercent}%` }}
                />
              </div>
            </div>
          </div>

          {isMaintenanceOverdue && (
            <div className="mt-4 p-3 bg-rose-500/5 border border-rose-500/20 rounded-lg text-xs text-rose-400 font-medium flex items-center gap-2">
              <i className="fa-solid fa-screwdriver-wrench animate-bounce"></i>
              <span>[🔧 MAINTENANCE DUE: Oil change required immediately]</span>
            </div>
          )}
        </div>

        {/* SECTION 3: Automated OCR Driver Verification Vault */}
        <div className="bg-slate-950/40 border border-white/5 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
              OCR Vetting & Credential Locker
            </span>

            {/* Custom file drag-and-drop trigger */}
            <div className="relative border border-dashed border-white/10 hover:border-orange-500/40 transition-colors rounded-xl p-6 text-center cursor-pointer flex flex-col items-center justify-center bg-slate-900/20 group">
              <input 
                type="file" 
                accept="image/*" 
                onChange={(e) => handleOcrUpload(e, 'license')}
                className="absolute inset-0 opacity-0 cursor-pointer"
              />
              <i className="fa-solid fa-cloud-arrow-up text-slate-500 group-hover:text-orange-400 text-2xl mb-2 transition-colors"></i>
              <p className="text-xs font-semibold text-slate-300">
                Drag and drop driver's license image here
              </p>
              <p className="text-[10px] text-slate-500 mt-1">
                Supports JPG, PNG formats up to 5MB
              </p>
            </div>
          </div>

          {/* Real-time OCR Parsing Vetting results */}
          {ocrStatus.fileName && (
            <div className="mt-4 p-3 bg-slate-900/60 border border-white/5 rounded-xl flex flex-col gap-2">
              <div className="flex items-center justify-between text-xs">
                <span className="text-slate-400 truncate max-w-[150px]">{ocrStatus.fileName}</span>
                <span className={`font-bold ${
                  ocrStatus.status.includes('Locked') ? 'text-rose-400' : ocrStatus.isProcessing ? 'text-orange-400' : 'text-emerald-400'
                }`}>
                  {ocrStatus.status}
                </span>
              </div>
              
              {!ocrStatus.isProcessing && (
                <div className="flex flex-col gap-1 text-[10px] text-slate-500 font-semibold border-t border-white/5 pt-2">
                  <div className="flex justify-between">
                    <span>Parsed Expiry Date:</span>
                    <span className="text-slate-300">{ocrStatus.extractedExpiry}</span>
                  </div>
                  <div className="flex justify-between">
                    <span>OCR Parser Confidence:</span>
                    <span className="text-slate-300">{ocrStatus.confidence}</span>
                  </div>
                  {ocrStatus.rejectionReason && (
                    <div className="mt-1 p-2 bg-rose-500/5 text-rose-400 border border-rose-500/10 rounded font-medium">
                      {ocrStatus.rejectionReason}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

      </div>

    </div>
  );
}
