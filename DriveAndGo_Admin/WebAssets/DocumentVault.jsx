import React, { useState, useEffect } from 'react';

/**
 * DocumentVault Component
 * 
 * Implements the immersive split-screen document review workspace,
 * allowing admins to select pending driver credentials, rotate and zoom ID files,
 * and approve or reject submissions with instant API integrations.
 */
export default function DocumentVault() {
  const [drivers, setDrivers] = useState([
    { id: 1, name: 'Mart Quirante', licenseNo: 'DL-A01-84-18239', email: 'mart.quirante@gmail.com', status: 'pending', idPhoto: 'https://images.unsplash.com/photo-1554151228-14d9def656e4?auto=format&fit=crop&q=80&w=600' },
    { id: 2, name: 'Allan Turing', licenseNo: 'DL-B89-19-48201', email: 'allan.turing@driveandgo.com', status: 'pending', idPhoto: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&q=80&w=600' },
    { id: 3, name: 'Grace Hopper', licenseNo: 'DL-G22-77-91023', email: 'grace.hopper@navy.gov', status: 'pending', idPhoto: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&q=80&w=600' }
  ]);

  const [selectedDriver, setSelectedDriver] = useState(null);
  const [rotation, setRotation] = useState(0);
  const [zoomScale, setZoomScale] = useState(1);
  const [isMagnifying, setIsMagnifying] = useState(false);
  const [magnifierPos, setMagnifierPos] = useState({ x: 0, y: 0 });
  const [isFullScreen, setIsFullScreen] = useState(false);
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [rejectReasons, setRejectReasons] = useState({
    blur: false,
    expired: false,
    mismatch: false
  });

  const [showConfetti, setShowConfetti] = useState(false);
  const [biometricScore, setBiometricScore] = useState(null);
  const [biometricStatus, setBiometricStatus] = useState('');
  const [isAnalyzingBiometrics, setIsAnalyzingBiometrics] = useState(false);

  const handleSelectDriver = (driver) => {
    setSelectedDriver(driver);
    setRotation(0);
    setZoomScale(1);
    setBiometricScore(null);
    setBiometricStatus('');
  };

  const handleVerifyBiometrics = async () => {
    if (!selectedDriver) return;
    setIsAnalyzingBiometrics(true);
    try {
      const res = await fetch(`/api/drivers/${selectedDriver.id}/verify-identity`, { method: 'POST' });
      const data = await res.json();
      setBiometricScore(data.confidenceScore);
      setBiometricStatus(data.verificationStatus);
      if (data.confidenceScore < 80) {
        setDrivers(prev => prev.map(d => d.id === selectedDriver.id ? { ...d, status: 'suspended' } : d));
        setSelectedDriver(prev => ({ ...prev, status: 'suspended', status: 'suspended' }));
      }
    } catch (err) {
      console.error("Biometric matching failed:", err);
    } finally {
      setIsAnalyzingBiometrics(false);
    }
  };

  const handleRotate = () => {
    setRotation(prev => (prev + 90) % 360);
  };

  const handleZoom = (direction) => {
    setZoomScale(prev => {
      if (direction === 'in') return Math.min(2.5, prev + 0.25);
      return Math.max(1, prev - 0.25);
    });
  };

  // Magnifying lens hover calculations
  const handleMouseMove = (e) => {
    const { left, top, width, height } = e.currentTarget.getBoundingClientRect();
    const x = ((e.clientX - left) / width) * 100;
    const y = ((e.clientY - top) / height) * 100;
    setMagnifierPos({ x, y });
  };

  const handleApprove = async () => {
    if (!selectedDriver) return;
    
    // Simulate PATCH /api/drivers/{id}/verify
    try {
      // In production: await fetch(`/api/drivers/${selectedDriver.id}/verify`, { method: 'PATCH', body: JSON.stringify({ status: 'approved' }) })
      setShowConfetti(true);
      setDrivers(prev => prev.map(d => d.id === selectedDriver.id ? { ...d, status: 'approved' } : d));
      setSelectedDriver(prev => ({ ...prev, status: 'approved' }));
      
      setTimeout(() => {
        setShowConfetti(false);
      }, 3000);
    } catch (err) {
      console.error(err);
    }
  };

  const handleReject = () => {
    if (!selectedDriver) return;
    
    // Simulate PATCH /api/drivers/{id}/verify
    const reasons = Object.keys(rejectReasons).filter(k => rejectReasons[k]).join(', ');
    setDrivers(prev => prev.map(d => d.id === selectedDriver.id ? { ...d, status: 'rejected' } : d));
    setSelectedDriver(prev => ({ ...prev, status: 'rejected' }));
    setRejectModalOpen(false);
    
    alert(`Driver rejected. Reasons sent: ${reasons || 'Blurry document image.'}`);
  };

  return (
    <div className="w-full flex flex-col lg:flex-row gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10 min-h-[70vh]">
      
      {/* Dynamic Master sidebar list */}
      <div className="w-full lg:w-1/3 flex flex-col gap-4 border-r border-white/5 pr-0 lg:pr-6">
        <div>
          <h3 className="text-base font-bold text-slate-200 flex items-center gap-2">
            <i className="fa-solid fa-folder-open text-orange-500"></i>
            Credential Review Workspace
          </h3>
          <p className="text-xs text-slate-500 font-medium">
            Manage submitted driver verification profiles, security flags, and credentials validation state.
          </p>
        </div>

        <div className="flex flex-col gap-2.5 mt-2">
          {drivers.map(d => (
            <div 
              key={d.id}
              onClick={() => handleSelectDriver(d)}
              className={`p-4 rounded-xl border cursor-pointer transition-all duration-300 flex items-center justify-between ${
                selectedDriver?.id === d.id 
                  ? 'bg-orange-600/15 border-orange-500/40 shadow-[0_0_15px_rgba(234,88,12,0.1)]' 
                  : 'bg-slate-950/40 border-white/5 hover:border-white/10'
              }`}
            >
              <div>
                <h4 className="text-xs font-bold text-slate-200">{d.name}</h4>
                <p className="text-[10px] text-slate-500 font-semibold uppercase mt-0.5">{d.licenseNo}</p>
              </div>
              <span className={`text-[9px] font-extrabold px-2 py-0.5 rounded-full uppercase ${
                d.status === 'approved' 
                  ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' 
                  : d.status === 'rejected'
                  ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20'
                  : 'bg-amber-500/10 text-amber-400 border border-amber-500/20 animate-pulse'
              }`}>
                {d.status}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* Dynamic Detail layout preview on the right */}
      <div className="flex-1 flex flex-col justify-between bg-slate-950/30 border border-white/5 rounded-2xl p-5 relative overflow-hidden">
        {showConfetti && (
          <div className="absolute inset-0 bg-emerald-500/10 flex items-center justify-center text-emerald-400 font-extrabold text-lg pointer-events-none z-50 animate-pulse">
            🎉 CREDENTIALS VERIFIED & ACTIVATED!
          </div>
        )}

        {selectedDriver ? (
          <div className="flex flex-col gap-5 h-full">
            <div className="flex justify-between items-center border-b border-white/5 pb-3">
              <div>
                <h3 className="text-sm font-bold text-white">{selectedDriver.name}</h3>
                <p className="text-[10px] text-slate-400 font-medium">{selectedDriver.email}</p>
              </div>
            </div>

            {biometricScore !== null && biometricScore < 80 && (
              <div className="bg-rose-500/10 border border-rose-500/35 p-3.5 rounded-xl text-xs text-rose-400 font-semibold flex items-center gap-2.5 shadow-[0_0_12px_rgba(244,63,94,0.15)] animate-pulse">
                <i className="fa-solid fa-triangle-exclamation text-rose-500 text-sm"></i>
                <span>⚠️ WARNING: High Fraud Risk - Facial Biometrics Mismatch! (Match confidence: {biometricScore}%)</span>
              </div>
            )}

              {/* Manipulation Toolbar */}
              <div className="flex items-center gap-1.5 bg-slate-900 border border-white/5 p-1 rounded-lg">
                <button 
                  onClick={handleRotate} 
                  title="Rotate 90°"
                  className="p-1.5 text-slate-400 hover:text-white rounded"
                >
                  <i className="fa-solid fa-rotate-right text-xs"></i>
                </button>
                <button 
                  onClick={() => handleZoom('in')} 
                  title="Zoom In"
                  className="p-1.5 text-slate-400 hover:text-white rounded"
                >
                  <i className="fa-solid fa-magnifying-glass-plus text-xs"></i>
                </button>
                <button 
                  onClick={() => handleZoom('out')} 
                  title="Zoom Out"
                  className="p-1.5 text-slate-400 hover:text-white rounded"
                >
                  <i className="fa-solid fa-magnifying-glass-minus text-xs"></i>
                </button>
                <button 
                  onClick={() => setIsFullScreen(!isFullScreen)} 
                  title="Toggle Fullscreen"
                  className="p-1.5 text-slate-400 hover:text-white rounded"
                >
                  <i className="fa-solid fa-expand text-xs"></i>
                </button>
              </div>
            </div>

            {/* Interactive ID Image Workspace */}
            <div 
              className="flex-1 relative bg-slate-950 rounded-xl overflow-hidden flex items-center justify-center min-h-[300px] border border-white/5 group cursor-crosshair"
              onMouseEnter={() => setIsMagnifying(true)}
              onMouseLeave={() => setIsMagnifying(false)}
              onMouseMove={handleMouseMove}
            >
              <img 
                src={selectedDriver.idPhoto} 
                alt="Driver's License" 
                className="max-h-[350px] object-contain transition-transform duration-300"
                style={{ 
                  transform: `rotate(${rotation}deg) scale(${zoomScale})`,
                }}
              />

              {/* Hover-to-magnify Lens */}
              {isMagnifying && !isFullScreen && (
                <div 
                  className="absolute pointer-events-none w-32 h-32 rounded-full border-2 border-orange-500 bg-no-repeat shadow-lg z-30"
                  style={{
                    left: `calc(${magnifierPos.x}% - 64px)`,
                    top: `calc(${magnifierPos.y}% - 64px)`,
                    backgroundImage: `url(${selectedDriver.idPhoto})`,
                    backgroundSize: `${400 * zoomScale}%`,
                    backgroundPosition: `${magnifierPos.x}% ${magnifierPos.y}%`
                  }}
                />
              )}
            </div>

            {/* Verification controls */}
            <div className="flex flex-col gap-3 border-t border-white/5 pt-4">
              <div className="flex items-center justify-between text-xs px-1">
                <span className="text-slate-400">Biometric Verification Status:</span>
                <span className={`font-bold ${
                  biometricScore !== null 
                    ? biometricScore >= 80 ? 'text-emerald-400' : 'text-rose-400'
                    : 'text-slate-500'
                }`}>
                  {biometricScore !== null ? `${biometricStatus} (${biometricScore}%)` : 'Not Checked'}
                </span>
              </div>

              <div className="flex items-center gap-3">
                <button
                  onClick={handleVerifyBiometrics}
                  disabled={isAnalyzingBiometrics}
                  className="flex-1 bg-orange-600 hover:bg-orange-700 disabled:bg-slate-800 disabled:text-slate-600 text-white font-extrabold py-3 rounded-lg text-xs transition-colors uppercase tracking-wider flex items-center justify-center gap-1.5"
                >
                  <i className={`fa-solid ${isAnalyzingBiometrics ? 'fa-spinner animate-spin' : 'fa-fingerprint'}`}></i>
                  <span>{isAnalyzingBiometrics ? 'Analyzing Facial Match...' : 'Verify Biometrics'}</span>
                </button>

                <button 
                  onClick={handleApprove}
                  disabled={selectedDriver.status === 'approved' || (biometricScore !== null && biometricScore < 80)}
                  className="flex-1 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-800 disabled:text-slate-600 text-white font-extrabold py-3 rounded-lg text-xs transition-colors uppercase tracking-wider flex items-center justify-center gap-1.5"
                >
                  <i className="fa-solid fa-circle-check"></i>
                  <span>Approve Credentials</span>
                </button>

                <button 
                  onClick={() => setRejectModalOpen(true)}
                  disabled={selectedDriver.status === 'rejected'}
                  className="bg-slate-900 hover:bg-slate-800 border border-white/5 disabled:bg-slate-950 disabled:text-slate-700 disabled:border-transparent text-slate-300 font-extrabold px-6 py-3 rounded-lg text-xs transition-colors uppercase tracking-wider"
                >
                  Reject / Flag
                </button>
              </div>
            </div>
          </div>
        ) : (
          <div className="flex-1 flex flex-col items-center justify-center text-slate-500 text-xs py-20">
            <i className="fa-solid fa-id-card text-3xl text-slate-600 mb-3"></i>
            <span>Select a pending driver credential from the sidebar list to inspect document details.</span>
          </div>
        )}
      </div>

      {/* Reject Reason Context Modal */}
      {rejectModalOpen && (
        <div className="fixed inset-0 bg-[#07070e]/80 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-slate-950 border border-white/10 p-6 rounded-2xl w-full max-w-sm flex flex-col gap-4 shadow-2xl">
            <div>
              <h4 className="text-sm font-bold text-slate-200">Flag Document Exception</h4>
              <p className="text-[10px] text-slate-500 font-medium mt-0.5">Specify rejection codes to dispatch in app alert notification.</p>
            </div>

            <div className="flex flex-col gap-2.5 my-2">
              <label className="flex items-center gap-2.5 text-xs text-slate-300 cursor-pointer">
                <input 
                  type="checkbox" 
                  checked={rejectReasons.blur} 
                  onChange={(e) => setRejectReasons(prev => ({ ...prev, blur: e.target.checked }))}
                  className="rounded bg-slate-900 border-white/5 text-orange-500 focus:ring-0"
                />
                <span>Blurry or illegible document image</span>
              </label>

              <label className="flex items-center gap-2.5 text-xs text-slate-300 cursor-pointer">
                <input 
                  type="checkbox" 
                  checked={rejectReasons.expired} 
                  onChange={(e) => setRejectReasons(prev => ({ ...prev, expired: e.target.checked }))}
                  className="rounded bg-slate-900 border-white/5 text-orange-500 focus:ring-0"
                />
                <span>Document indicates expired credentials date</span>
              </label>

              <label className="flex items-center gap-2.5 text-xs text-slate-300 cursor-pointer">
                <input 
                  type="checkbox" 
                  checked={rejectReasons.mismatch} 
                  onChange={(e) => setRejectReasons(prev => ({ ...prev, mismatch: e.target.checked }))}
                  className="rounded bg-slate-900 border-white/5 text-orange-500 focus:ring-0"
                />
                <span>License details mismatch system user details</span>
              </label>
            </div>

            <div className="flex items-center gap-2 border-t border-white/5 pt-3">
              <button 
                onClick={handleReject}
                className="flex-1 bg-rose-600 hover:bg-rose-700 text-white font-bold py-2 rounded-lg text-xs transition-colors"
              >
                Confirm Rejection
              </button>
              <button 
                onClick={() => setRejectModalOpen(false)}
                className="px-4 py-2 bg-slate-900 text-slate-400 hover:text-white rounded-lg text-xs border border-white/5"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Fullscreen Overlay */}
      {isFullScreen && selectedDriver && (
        <div className="fixed inset-0 bg-[#07070e] z-50 flex items-center justify-center p-6">
          <button 
            onClick={() => setIsFullScreen(false)}
            className="absolute top-6 right-6 p-2 bg-slate-900 hover:bg-slate-800 text-white border border-white/10 rounded-full"
          >
            <i className="fa-solid fa-xmark text-lg"></i>
          </button>
          <img 
            src={selectedDriver.idPhoto} 
            alt="Fullscreen license" 
            className="max-h-[90vh] max-w-[90vw] object-contain transition-transform"
            style={{ transform: `rotate(${rotation}deg)` }}
          />
        </div>
      )}

    </div>
  );
}
