import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';

const API_BASE = window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233/api` : 'http://localhost:5233/api');

/* ─────────────────────────────────────────────────────────────────────────────
   Vector SVG Icons (Strictly No Emojis)
───────────────────────────────────────────────────────────────────────────── */
const IconCar = ({ size = 20, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2"/>
    <circle cx="7" cy="17" r="2"/><path d="M9 17h6"/><circle cx="17" cy="17" r="2"/>
  </svg>
);
const IconGauge = ({ size = 20, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <path d="m12 14 4-4"/><path d="M3.34 19a10 10 0 1 1 17.32 0"/>
  </svg>
);
const IconAlertCircle = ({ size = 20, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
  </svg>
);
const IconWrench = ({ size = 20, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>
  </svg>
);
const IconDownload = ({ size = 16, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/>
  </svg>
);
const IconSearch = ({ size = 16, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/>
  </svg>
);
const IconRefresh = ({ size = 14, className = '' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={className}>
    <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2"/>
  </svg>
);

/* ─────────────────────────────────────────────────────────────────────────────
   3D Bento Metric Card with Cursor Glare Tracking
───────────────────────────────────────────────────────────────────────────── */
export function RentalBentoCard3D({ icon, value, title, subtext, tag, tagClass, glowColor, strokeColor, pathD }) {
  const cardRef = useRef(null);
  const rafRef  = useRef(null);
  const [tilt, setTilt] = useState({ x: 0, y: 0 });
  const [glare, setGlare] = useState({ x: 50, y: 50, opacity: 0 });
  const [hovered, setHovered] = useState(false);

  const handleMouseMove = useCallback((e) => {
    if (!cardRef.current) return;
    cancelAnimationFrame(rafRef.current);
    rafRef.current = requestAnimationFrame(() => {
      if (!cardRef.current) return;
      const rect = cardRef.current.getBoundingClientRect();
      const relX = e.clientX - rect.left;
      const relY = e.clientY - rect.top;
      const centerX = rect.width / 2;
      const centerY = rect.height / 2;
      const normX = (relX - centerX) / centerX;
      const normY = (relY - centerY) / centerY;
      setTilt({ x: normY * -10, y: normX * 10 });
      setGlare({
        x: (relX / rect.width) * 100,
        y: (relY / rect.height) * 100,
        opacity: 0.25
      });
    });
  }, []);

  const handleMouseEnter = useCallback(() => setHovered(true), []);
  const handleMouseLeave = useCallback(() => {
    cancelAnimationFrame(rafRef.current);
    setHovered(false);
    setTilt({ x: 0, y: 0 });
    setGlare(g => ({ ...g, opacity: 0 }));
  }, []);

  return (
    <div
      ref={cardRef}
      onMouseMove={handleMouseMove}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      style={{
        perspective: '1000px',
        transformStyle: 'preserve-3d',
        transform: hovered
          ? `perspective(1000px) rotateX(${tilt.x}deg) rotateY(${tilt.y}deg) scale(1.03) translateZ(8px)`
          : 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale(1) translateZ(0px)',
        transition: hovered
          ? 'transform 0.1s ease-out, box-shadow 0.25s ease, border-color 0.2s ease, background 0.2s ease'
          : 'transform 0.5s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.35s ease, border-color 0.3s ease, background 0.3s ease',
        boxShadow: hovered ? `0 20px 40px ${glowColor || 'rgba(255, 107, 0, 0.25)'}, 0 0 0 1px rgba(255,255,255,0.08)` : '0 4px 16px rgba(0,0,0,0.25)',
        willChange: 'transform',
        position: 'relative',
        overflow: 'hidden'
      }}
      className="glass-card rounded-2xl p-5 relative overflow-hidden select-none cursor-pointer border border-slate-200 dark:border-slate-800 bg-white dark:bg-[#0f172a]/90 backdrop-blur-xl"
    >
      {/* Top Glass Shine */}
      <div
        style={{
          position: 'absolute', top: 0, left: 0, right: 0, height: '45%',
          borderRadius: '16px 16px 0 0',
          background: 'linear-gradient(180deg, rgba(255,255,255,0.08) 0%, transparent 100%)',
          pointerEvents: 'none', zIndex: 1
        }}
      />
      {/* Radial Glare Overlay */}
      <div
        style={{
          position: 'absolute', inset: 0, borderRadius: '16px', pointerEvents: 'none',
          background: `radial-gradient(circle at ${glare.x}% ${glare.y}%, rgba(255,255,255,${glare.opacity}) 0%, rgba(255,255,255,0) 65%)`,
          transition: hovered ? 'none' : 'opacity 0.4s ease',
          zIndex: 2
        }}
      />
      {/* Card Content */}
      <div className="relative z-10">
        <div className="flex items-center justify-between mb-3">
          <div className="w-10 h-10 rounded-xl bg-orange-500/10 flex items-center justify-center text-orange-500">
            {icon}
          </div>
          <span className={`flex items-center gap-1 text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full border ${tagClass}`}>
            {tag}
          </span>
        </div>
        <div className="text-3xl font-black text-slate-900 dark:text-white tracking-tight">{value}</div>
        <div className="text-xs font-bold text-slate-800 dark:text-slate-200 mt-1">{title}</div>
        <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">{subtext}</div>
        {pathD && (
          <div className="mt-4 opacity-80">
            <svg className={`w-full h-5 ${strokeColor || 'text-orange-500'}`} fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 100 20">
              <path d={pathD} strokeLinecap="round"/>
            </svg>
          </div>
        )}
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────────
   Main Rentals Management React Component
───────────────────────────────────────────────────────────────────────────── */
export default function RentalsOverview() {
  const [rentals, setRentals] = useState([]);
  const [stats, setStats] = useState({
    totalVehicles: 0, availableVehicles: 0, onRentVehicles: 0, maintenanceVehicles: 0,
    totalBookings: 0, pendingBookings: 0, activeBookings: 0, overdueBookings: 0, completedBookings: 0
  });
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(10);

  // Fetch Live Data
  const fetchData = useCallback(async () => {
    try {
      setLoading(true);
      const [statsRes, rentalsRes] = await Promise.all([
        fetch(`${API_BASE}/Rentals/stats`).then(r => r.ok ? r.json() : null).catch(() => null),
        fetch(`${API_BASE}/Rentals`).then(r => r.ok ? r.json() : []).catch(() => [])
      ]);

      if (statsRes) setStats(statsRes);
      if (Array.isArray(rentalsRes)) setRentals(rentalsRes);
    } catch (err) {
      console.error('Rentals API Error:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Filtered & Paginated Rentals
  const filteredRentals = useMemo(() => {
    const q = searchTerm.toLowerCase().trim();
    return rentals.filter(r => {
      const st = (r.status || '').toLowerCase();
      const matchStatus = statusFilter === 'all' || st === statusFilter;
      const matchSearch = !q || (
        (r.customerName || '').toLowerCase().includes(q) ||
        (r.customerPhone || '').toLowerCase().includes(q) ||
        (r.vehicleName || '').toLowerCase().includes(q) ||
        (r.vehiclePlateNo || '').toLowerCase().includes(q) ||
        String(r.rentalId || '').includes(q) ||
        (r.rentalCode || '').toLowerCase().includes(q)
      );
      return matchStatus && matchSearch;
    });
  }, [rentals, searchTerm, statusFilter]);

  const totalPages = Math.ceil(filteredRentals.length / itemsPerPage) || 1;
  const paginatedRentals = useMemo(() => {
    const start = (currentPage - 1) * itemsPerPage;
    return filteredRentals.slice(start, start + itemsPerPage);
  }, [filteredRentals, currentPage, itemsPerPage]);

  const getStatusBadge = (status) => {
    const st = (status || '').toLowerCase();
    switch (st) {
      case 'active':
      case 'in-use':
        return { label: 'Active', bg: 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30' };
      case 'pending':
        return { label: 'Pending', bg: 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/30' };
      case 'overdue':
        return { label: 'Overdue', bg: 'bg-red-500/15 text-red-600 dark:text-red-400 border border-red-500/30 animate-pulse' };
      case 'completed':
        return { label: 'Completed', bg: 'bg-blue-500/15 text-blue-600 dark:text-blue-400 border border-blue-500/30' };
      default:
        return { label: status || 'Pending', bg: 'bg-slate-500/15 text-slate-400 border border-slate-500/30' };
    }
  };

  return (
    <div className="min-h-screen bg-[#090D16] text-[#F8FAFC] p-4 lg:p-6 font-sans">
      
      {/* Header Bar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-6">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl lg:text-2xl font-black text-white tracking-tight">Rental Agreements & Bookings</h1>
            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase bg-orange-500/15 text-orange-400 border border-orange-500/30">
              Live DB Feed
            </span>
          </div>
          <p className="text-xs text-slate-400 mt-0.5">
            Enterprise fleet dispatch, A4 agreements with QR security, and automated email processing.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button 
            onClick={() => {
              if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage({ action: 'openPromoCodes' });
              }
            }} 
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl border border-orange-500/30 bg-orange-500/10 text-orange-400 hover:bg-orange-500/20 text-xs font-bold transition-all shadow-sm cursor-pointer" 
            title="Manage Promo Codes & Discounts"
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"/>
              <path d="M13 5v2"/><path d="M13 17v2"/><path d="M13 11v2"/>
            </svg>
            <span>Promo Codes</span>
          </button>
          <button onClick={fetchData} className="p-2 rounded-xl border border-slate-800 bg-slate-900 text-slate-300 hover:text-white hover:border-orange-500/40 transition-all" title="Refresh Live Data">
            <IconRefresh size={16} className={loading ? 'animate-spin' : ''} />
          </button>
        </div>
      </div>

      {/* ── 4 Bento Metric Cards with 3D Hover & Cursor Glare ── */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <RentalBentoCard3D
          icon={<IconCar size={20} />}
          value={stats.availableVehicles ?? '0'}
          title="Available Fleet"
          subtext={`of ${stats.totalVehicles ?? '0'} total fleet`}
          tag="Ready"
          tagClass="text-emerald-500 bg-emerald-500/10 border-emerald-500/20"
          glowColor="rgba(16, 185, 129, 0.35)"
          strokeColor="text-emerald-500"
          pathD="M0,18 Q25,16 50,8 T100,2"
        />
        <RentalBentoCard3D
          icon={<IconGauge size={20} />}
          value={stats.onRentVehicles ?? '0'}
          title="On-Rent"
          subtext={`${stats.onRentVehicles ?? 0} active dispatched`}
          tag="Active"
          tagClass="text-orange-500 bg-orange-500/10 border-orange-500/20"
          glowColor="rgba(255, 107, 0, 0.35)"
          strokeColor="text-orange-500"
          pathD="M0,15 Q30,12 60,6 T100,3"
        />
        <RentalBentoCard3D
          icon={<IconAlertCircle size={20} />}
          value={stats.overdueBookings ?? '0'}
          title="Overdue Alerts"
          subtext={`${stats.overdueBookings ?? 0} require action`}
          tag="Attention"
          tagClass="text-red-500 bg-red-500/10 border-red-500/20"
          glowColor="rgba(239, 68, 68, 0.45)"
          strokeColor="text-red-500"
          pathD="M0,5 Q35,8 65,15 T100,18"
        />
        <RentalBentoCard3D
          icon={<IconWrench size={20} />}
          value={stats.maintenanceVehicles ?? '0'}
          title="In Maintenance"
          subtext={`${stats.maintenanceVehicles ?? 0} in repair`}
          tag="Service"
          tagClass="text-slate-400 bg-slate-500/10 border-slate-500/20"
          glowColor="rgba(148, 163, 184, 0.25)"
          strokeColor="text-slate-400"
          pathD="M0,10 Q30,12 60,10 T100,11"
        />
      </div>

      {/* ── Dense Data Table ── */}
      <div className="glass-card rounded-2xl overflow-hidden shadow-xl mb-6 bg-slate-900/80 border border-slate-800">
        
        {/* Table Toolbar */}
        <div className="p-4 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="font-extrabold text-sm text-white">All Rentals</span>
            <span className="text-xs font-bold px-2 py-0.5 rounded-full bg-orange-500/10 text-orange-400 border border-orange-500/20">
              {filteredRentals.length}
            </span>
          </div>

          <div className="flex items-center gap-3">
            {/* Search */}
            <div className="relative flex items-center">
              <IconSearch size={14} className="text-slate-400 absolute left-3 pointer-events-none" />
              <input
                type="text"
                placeholder="Search rentals..."
                value={searchTerm}
                onChange={e => setSearchTerm(e.target.value)}
                className="pl-9 pr-3 py-1.5 rounded-xl text-xs bg-slate-950 border border-slate-800 text-white placeholder-slate-500 focus:outline-none focus:border-orange-500 w-52 transition-all"
              />
            </div>

            {/* Filter */}
            <select
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
              className="px-3 py-1.5 rounded-xl text-xs bg-slate-950 border border-slate-800 text-slate-200 font-bold focus:outline-none focus:border-orange-500"
            >
              <option value="all">All Status</option>
              <option value="pending">Pending</option>
              <option value="active">Active</option>
              <option value="overdue">Overdue</option>
              <option value="completed">Completed</option>
            </select>
          </div>
        </div>

        {/* Table View */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-300">
            <thead className="bg-slate-950/80 text-[11px] font-bold text-slate-400 uppercase tracking-wider border-b border-slate-800">
              <tr>
                <th className="px-4 py-3">Rental ID & Code</th>
                <th className="px-4 py-3">Customer</th>
                <th className="px-4 py-3">Vehicle Details</th>
                <th className="px-4 py-3">Schedule</th>
                <th className="px-4 py-3">Total (PHP)</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-medium">
              {paginatedRentals.map(r => {
                const badge = getStatusBadge(r.status);
                return (
                  <tr key={r.rentalId} className="hover:bg-slate-800/40 transition-colors">
                    <td className="px-4 py-3 font-mono font-bold text-white">
                      <div>#{r.rentalId}</div>
                      <div className="text-[10px] text-orange-400">{r.rentalCode || 'RN-PENDING'}</div>
                    </td>
                    <td className="px-4 py-3 font-semibold text-white">
                      <div>{r.customerName || 'Walk-in Client'}</div>
                      <div className="text-[10px] text-slate-400">{r.customerPhone || 'No Phone'}</div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="font-bold text-white">{r.vehicleName || 'Toyota Vios'}</div>
                      <div className="text-[10px] font-mono text-slate-400">{r.vehiclePlateNo || 'N/A'}</div>
                    </td>
                    <td className="px-4 py-3 text-[11px] text-slate-300">
                      <div>{new Date(r.startDate).toLocaleDateString()}</div>
                      <div className="text-[10px] text-slate-500">to {new Date(r.endDate).toLocaleDateString()}</div>
                    </td>
                    <td className="px-4 py-3 font-mono font-black text-emerald-400 text-sm">
                      ₱{Number(r.totalAmount || 0).toLocaleString('en-PH', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="px-4 py-3">
                      <span className={`px-2.5 py-1 rounded-full text-[10px] font-black uppercase ${badge.bg}`}>
                        {badge.label}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <a
                          href={`${API_BASE}/Rentals/${r.rentalId}/pdf`}
                          target="_blank"
                          rel="noreferrer"
                          className="p-1.5 rounded-lg bg-orange-500/10 text-orange-400 hover:bg-orange-500/20 border border-orange-500/30 transition-all"
                          title="Download Official PDF"
                        >
                          <IconDownload size={14} />
                        </a>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {paginatedRentals.length === 0 && (
                <tr>
                  <td colSpan="7" className="px-4 py-12 text-center text-slate-500">
                    No rental bookings found matching criteria.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination Footer */}
        <div className="p-4 pr-24 border-t border-slate-200 dark:border-slate-800 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
          <div>
            Showing <span className="font-bold text-slate-900 dark:text-white">{filteredRentals.length === 0 ? 0 : (currentPage - 1) * itemsPerPage + 1}</span> to <span className="font-bold text-slate-900 dark:text-white">{Math.min(currentPage * itemsPerPage, filteredRentals.length)}</span> of <span className="font-bold text-slate-900 dark:text-white">{filteredRentals.length}</span> entries
          </div>
          <div className="flex items-center gap-1.5">
            <button
              onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
              disabled={currentPage <= 1}
              className="p-1.5 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900 text-slate-600 dark:text-slate-400 disabled:opacity-40 disabled:cursor-not-allowed hover:border-brand hover:text-brand transition-all flex items-center justify-center"
            >
              <LucideIcon name="chevron-left" className="w-3.5 h-3.5" />
            </button>
            {(() => {
              const pages = [];
              if (totalPages <= 5) {
                for (let i = 1; i <= totalPages; i++) pages.push(i);
              } else {
                if (currentPage <= 3) {
                  pages.push(1, 2, 3, '...', totalPages);
                } else if (currentPage >= totalPages - 2) {
                  pages.push(1, '...', totalPages - 2, totalPages - 1, totalPages);
                } else {
                  pages.push(1, '...', currentPage - 1, currentPage, currentPage + 1, '...', totalPages);
                }
              }
              return pages.map((p, idx) => p === '...' ? (
                <span key={`el-${idx}`} className="px-1 text-slate-400 text-xs select-none">...</span>
              ) : (
                <button
                  key={p}
                  onClick={() => setCurrentPage(p)}
                  className={`w-7 h-7 rounded-lg border text-xs font-bold transition-all ${
                    p === currentPage
                      ? 'bg-[#FF6B00] text-white border-[#FF6B00] shadow-md shadow-[#FF6B00]/30'
                      : 'border-slate-200 dark:border-slate-800 text-slate-700 dark:text-slate-300 hover:border-brand hover:text-brand bg-slate-50/50 dark:bg-slate-900/50'
                  }`}
                >
                  {p}
                </button>
              ));
            })()}
            <button
              onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
              disabled={currentPage >= totalPages}
              className="p-1.5 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-900 text-slate-600 dark:text-slate-400 disabled:opacity-40 disabled:cursor-not-allowed hover:border-brand hover:text-brand transition-all flex items-center justify-center"
            >
              <LucideIcon name="chevron-right" className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

      </div>

    </div>
  );
}
