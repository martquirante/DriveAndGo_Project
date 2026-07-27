import React, { useState } from 'react';

/**
 * DamageClaimForm Component
 * 
 * Renders the incident reporting and damage claims form inside the rental details workspace,
 * enabling severity selectors, attachment uploads, and automated liability audits.
 */
export default function DamageClaimForm({ rentalId = 1, onClose }) {
  const [severity, setSeverity] = useState('Low'); // 'Low', 'Medium', 'Critical'
  const [description, setDescription] = useState('');
  const [fileName, setFileName] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [claimResult, setClaimResult] = useState(null);

  const handleFileUpload = (e) => {
    const file = e.target.files[0];
    if (file) {
      setFileName(file.name);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!description) return;

    setIsSubmitting(true);
    
    // Simulate photo upload to Supabase bucket -> returns bucket url
    const mockPhotoUrl = "https://images.unsplash.com/photo-1597481499750-3e6b22637e12?auto=format&fit=crop&q=80&w=600";

    try {
      const response = await fetch('/api/claims/submit', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          rentalId,
          damageSeverity: severity,
          description,
          photoUrl: mockPhotoUrl
        })
      });

      if (response.ok) {
        const data = await response.json();
        setClaimResult(data);
      } else {
        alert("Submission failed. Please check network logs.");
      }
    } catch (err) {
      console.error("Dispute filing failed:", err);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10 max-w-md mx-auto">
      
      {/* Header bar */}
      <div className="flex items-center justify-between border-b border-white/5 pb-4">
        <div className="flex items-center gap-2">
          <i className="fa-solid fa-car-burst text-rose-500"></i>
          <span className="text-sm font-bold text-slate-200">Incident & Damage Claims Vault</span>
        </div>
        {onClose && (
          <button onClick={onClose} className="text-slate-500 hover:text-white transition-colors">
            <i className="fa-solid fa-xmark"></i>
          </button>
        )}
      </div>

      {!claimResult ? (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          
          {/* Severity Selector */}
          <div>
            <label className="text-[10px] text-slate-500 font-semibold uppercase block mb-2">Damage Severity Level</label>
            <div className="grid grid-cols-3 gap-2.5">
              {['Low', 'Medium', 'Critical'].map(level => (
                <button
                  key={level}
                  type="button"
                  onClick={() => setSeverity(level)}
                  className={`py-2 rounded-xl text-xs font-bold transition-all border ${
                    severity === level 
                      ? 'bg-rose-500/15 border-rose-500/50 text-rose-400 shadow-[0_0_12px_rgba(244,63,94,0.15)]' 
                      : 'bg-slate-950/40 border-white/5 text-slate-400 hover:border-white/10'
                  }`}
                >
                  {level}
                </button>
              ))}
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="text-[10px] text-slate-500 font-semibold uppercase block mb-1">Accident Description</label>
            <textarea 
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Provide a detailed description of the damages, location of the incident, and circumstances."
              className="w-full h-24 bg-slate-900 border border-white/5 focus:border-orange-500/40 rounded-lg p-2.5 text-xs text-white placeholder-slate-600 transition-colors resize-none"
              required
            />
          </div>

          {/* File Attachment Dropzone */}
          <div>
            <label className="text-[10px] text-slate-500 font-semibold uppercase block mb-2">Accident Photo Attachments</label>
            <div className="relative border border-dashed border-white/10 hover:border-orange-500/40 transition-colors rounded-xl p-6 text-center cursor-pointer flex flex-col items-center justify-center bg-slate-900/20 group">
              <input 
                type="file" 
                accept="image/*" 
                onChange={handleFileUpload}
                className="absolute inset-0 opacity-0 cursor-pointer"
              />
              <i className="fa-solid fa-camera text-slate-500 group-hover:text-rose-400 text-xl mb-1.5 transition-colors"></i>
              <p className="text-xs font-semibold text-slate-300">
                {fileName ? fileName : "Upload incident/damage photos"}
              </p>
              <p className="text-[9px] text-slate-500 mt-1">
                Supports standard image files up to 10MB
              </p>
            </div>
          </div>

          {/* Action button */}
          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full mt-2 bg-rose-600 hover:bg-rose-700 disabled:bg-slate-800 disabled:text-slate-600 text-white font-extrabold py-3 rounded-lg text-xs transition-colors uppercase tracking-wider flex items-center justify-center gap-1.5"
          >
            <i className={`fa-solid ${isSubmitting ? 'fa-spinner animate-spin' : 'fa-circle-exclamation'}`}></i>
            <span>{isSubmitting ? 'Filing Incident...' : 'Submit Incident Claim'}</span>
          </button>

        </form>
      ) : (
        /* Dynamic Cost Evaluation Results */
        <div className="flex flex-col gap-4 animate-fadeIn">
          <div className="bg-rose-500/10 border border-rose-500/20 p-4 rounded-xl flex items-center gap-3">
            <i className="fa-solid fa-circle-check text-rose-500 text-xl"></i>
            <div>
              <h4 className="text-xs font-bold text-rose-400 uppercase">Incident Filed Successfully</h4>
              <p className="text-xs text-slate-300 mt-0.5">Automated cost adjusters have updated the invoice.</p>
            </div>
          </div>

          <div className="bg-slate-950/60 border border-white/5 p-4 rounded-xl flex flex-col gap-2.5 text-xs">
            <div className="flex justify-between">
              <span className="text-slate-400">Claim ID:</span>
              <span className="text-slate-200 font-bold">#CLM-{claimResult.claimId}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Damage Severity:</span>
              <span className="text-rose-400 font-bold uppercase">{severity}</span>
            </div>
            <div className="flex justify-between border-t border-white/5 pt-2.5">
              <span className="text-slate-400">Computed Liability Fee:</span>
              <span className="text-white font-extrabold">₱{claimResult.computedLiability.toLocaleString()}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-slate-400">Total Adjusted Cost:</span>
              <span className="text-orange-400 font-extrabold">₱{claimResult.totalAdjustedAmount.toLocaleString()}</span>
            </div>
          </div>

          {onClose && (
            <button 
              onClick={onClose}
              className="w-full bg-slate-900 hover:bg-slate-800 text-slate-300 font-bold py-2.5 rounded-lg text-xs transition-colors border border-white/5"
            >
              Close Workspace
            </button>
          )}
        </div>
      )}

    </div>
  );
}
