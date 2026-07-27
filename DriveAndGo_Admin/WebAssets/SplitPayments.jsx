import React, { useState, useEffect } from 'react';

/**
 * SplitPayments (Barkada Mode) Component
 * 
 * Renders the mathematical allocation grid for dividing vehicle rental fees,
 * including validation constraints and real-time SignalR payment updates.
 */
export default function SplitPayments({ baseRentalId = 1, baseAmount = 8500 }) {
  const [renters, setRenters] = useState([
    { email: 'mart.quirante@gmail.com', amount: 3500, status: 'Paid', isNewFlash: false },
    { email: 'allan.admin@driveandgo.com', amount: 2500, status: 'Unpaid', isNewFlash: false }
  ]);

  const [newEmail, setNewEmail] = useState('');
  const [newAmount, setNewAmount] = useState('');
  const [isInitializing, setIsInitializing] = useState(false);
  const [validationError, setValidationError] = useState('');

  // Math allocation calculation
  const totalAllocated = renters.reduce((sum, r) => sum + Number(r.amount), 0);
  const allocatedPercentage = Math.round((totalAllocated / baseAmount) * 100);
  const isMatchExact = totalAllocated === baseAmount;

  // Track validation changes
  useEffect(() => {
    if (totalAllocated > baseAmount) {
      setValidationError(`Allocation error: Total exceeds base rental fee by ₱${(totalAllocated - baseAmount).toLocaleString()}`);
    } else if (totalAllocated < baseAmount) {
      setValidationError(`Pending allocation: Remaining amount left to allocate is ₱${(baseAmount - totalAllocated).toLocaleString()}`);
    } else {
      setValidationError('');
    }
  }, [totalAllocated, baseAmount]);

  // Simulate real-time payment settlement updates (e.g. from SignalR)
  useEffect(() => {
    const timer = setTimeout(() => {
      // Find the unpaid renter and simulate their settlement
      setRenters(prev => prev.map(r => {
        if (r.status === 'Unpaid' && r.email === 'allan.admin@driveandgo.com') {
          return { ...r, status: 'Paid', isNewFlash: true };
        }
        return r;
      }));

      // Clear the green flash state after animation
      setTimeout(() => {
        setRenters(prev => prev.map(r => ({ ...r, isNewFlash: false })));
      }, 2000);
    }, 5000);

    return () => clearTimeout(timer);
  }, []);

  const handleAddRenter = (e) => {
    e.preventDefault();
    if (!newEmail || !newAmount) return;

    const amt = Number(newAmount);
    if (totalAllocated + amt > baseAmount) {
      alert("Error: Total allocation cannot exceed the base rental amount.");
      return;
    }

    setRenters(prev => [...prev, {
      email: newEmail,
      amount: amt,
      status: 'Unpaid',
      isNewFlash: false
    }]);

    setNewEmail('');
    setNewAmount('');
  };

  const handleRemoveRenter = (index) => {
    setRenters(prev => prev.filter((_, i) => i !== index));
  };

  const handleInitializeSplits = () => {
    if (!isMatchExact) return;
    setIsInitializing(true);
    setTimeout(() => {
      setIsInitializing(false);
      alert("Barkada Mode Split Payments initialized. Settlement emails dispatched successfully.");
    }, 1500);
  };

  return (
    <div className="w-full flex flex-col gap-6 text-slate-100 bg-[#07070e] font-sans antialiased p-6 rounded-2xl border border-white/5 bg-slate-900/10">
      
      {/* Header bar */}
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-4 border-b border-white/5 pb-4">
        <div>
          <h3 className="text-base font-bold text-slate-200 flex items-center gap-2">
            <i className="fa-solid fa-users text-orange-500"></i>
            Barkada Mode: Interactive Split Payments
          </h3>
          <p className="text-xs text-slate-500 font-medium">
            Divide total rental costs dynamically. Tracks settlement notifications in real-time.
          </p>
        </div>
        
        {/* Cost tracker badge */}
        <div className="flex flex-col text-right">
          <span className="text-[10px] text-slate-500 font-bold uppercase">Total Rental Fee</span>
          <span className="text-lg font-black text-white">₱{baseAmount.toLocaleString()}</span>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left Control Panel Form */}
        <div className="lg:col-span-1 bg-slate-950/40 border border-white/5 p-5 rounded-2xl flex flex-col justify-between">
          <div>
            <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
              Add Co-Renter Share
            </span>

            <form onSubmit={handleAddRenter} className="flex flex-col gap-3">
              <div>
                <label className="text-[10px] text-slate-500 font-semibold uppercase block mb-1">Renter Email</label>
                <input 
                  type="email" 
                  value={newEmail} 
                  onChange={(e) => setNewEmail(e.target.value)} 
                  placeholder="co-renter@email.com" 
                  className="w-full bg-slate-900 border border-white/5 focus:border-orange-500/40 rounded-lg p-2 text-xs text-white placeholder-slate-600 transition-colors"
                  required
                />
              </div>

              <div>
                <label className="text-[10px] text-slate-500 font-semibold uppercase block mb-1">Share Amount (₱)</label>
                <input 
                  type="number" 
                  value={newAmount} 
                  onChange={(e) => setNewAmount(e.target.value)} 
                  placeholder="e.g. 2000" 
                  className="w-full bg-slate-900 border border-white/5 focus:border-orange-500/40 rounded-lg p-2 text-xs text-white placeholder-slate-600 transition-colors"
                  required
                />
              </div>

              <button 
                type="submit" 
                disabled={totalAllocated >= baseAmount}
                className="w-full mt-2 bg-orange-600 hover:bg-orange-700 disabled:bg-slate-800 disabled:text-slate-600 text-white font-bold p-2.5 rounded-lg text-xs transition-colors flex items-center justify-center gap-1.5"
              >
                <i className="fa-solid fa-plus text-xs"></i>
                <span>Add Renter Share</span>
              </button>
            </form>
          </div>

          {/* Allocation Math Summary */}
          <div className="mt-6 border-t border-white/5 pt-4 flex flex-col gap-3">
            <div>
              <p className="text-[10px] text-slate-500 font-semibold uppercase mb-1">Allocation Progress</p>
              <div className="flex justify-between text-xs font-bold mb-1">
                <span>₱{totalAllocated.toLocaleString()} / ₱{baseAmount.toLocaleString()}</span>
                <span className={isMatchExact ? 'text-emerald-400' : 'text-orange-400'}>{allocatedPercentage}%</span>
              </div>
              <div className="w-full h-2 bg-slate-900 rounded-full overflow-hidden border border-white/5">
                <div 
                  className={`h-full rounded-full transition-all duration-500 ${
                    isMatchExact ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]' : 'bg-orange-500'
                  }`}
                  style={{ width: `${Math.min(100, (totalAllocated / baseAmount) * 100)}%` }}
                />
              </div>
            </div>

            {validationError && (
              <p className="text-[10px] font-medium text-amber-400/90 leading-relaxed bg-amber-500/5 p-2.5 rounded-lg border border-amber-500/10">
                {validationError}
              </p>
            )}

            <button 
              onClick={handleInitializeSplits} 
              disabled={!isMatchExact || isInitializing}
              className="w-full bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-800 disabled:text-slate-600 text-white font-extrabold py-3 rounded-lg text-xs transition-all tracking-wider uppercase shadow-[0_4px_15px_rgba(16,185,129,0.1)]"
            >
              {isInitializing ? 'Dispatched...' : 'Initialize Split Payments'}
            </button>
          </div>
        </div>

        {/* Right Interactive Renter Grid Table */}
        <div className="lg:col-span-2 bg-slate-950/40 border border-white/5 p-5 rounded-2xl">
          <span className="text-[11px] font-bold text-slate-400 uppercase tracking-widest block mb-4">
            Renter Allocation & Settlement Status
          </span>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-white/5 text-[10px] text-slate-500 font-bold uppercase">
                  <th className="py-2.5">Co-Renter Email</th>
                  <th className="py-2.5">Allocated Share</th>
                  <th className="py-2.5">Settlement Status</th>
                  <th className="py-2.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {renters.map((renter, index) => (
                  <tr 
                    key={index} 
                    className={`border-b border-white/[0.02] text-xs font-semibold transition-all duration-700 ${
                      renter.isNewFlash ? 'bg-emerald-500/10 text-emerald-400' : 'hover:bg-white/[0.01]'
                    }`}
                  >
                    <td className="py-3.5 text-slate-200">{renter.email}</td>
                    <td className="py-3.5 text-white font-extrabold">₱{renter.amount.toLocaleString()}</td>
                    <td className="py-3.5">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[10px] font-extrabold uppercase ${
                        renter.status === 'Paid'
                          ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 shadow-[0_0_10px_rgba(16,185,129,0.15)]'
                          : 'bg-amber-500/10 text-amber-400 border border-amber-500/20 shadow-[0_0_10px_rgba(245,158,11,0.15)] animate-pulse'
                      }`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${
                          renter.status === 'Paid' ? 'bg-emerald-400' : 'bg-amber-400'
                        }`} />
                        {renter.status}
                      </span>
                    </td>
                    <td className="py-3.5 text-right">
                      <button 
                        onClick={() => handleRemoveRenter(index)}
                        className="text-slate-500 hover:text-rose-400 transition-colors p-1 rounded"
                      >
                        <i className="fa-solid fa-trash-can text-xs"></i>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="mt-4 p-3 bg-[#07070e] border border-white/5 rounded-xl text-[10px] text-slate-500 font-semibold flex items-center gap-2">
            <i className="fa-solid fa-circle-nodes text-orange-500 animate-pulse"></i>
            <span>Telemetry link active: Listening for SignalR remote co-renter settle transactions.</span>
          </div>
        </div>

      </div>

    </div>
  );
}
