import React, { useState, useEffect, useRef, useCallback, useMemo } from 'react';

/* ─────────────────────────────────────────────────────────────────────────────
   AnimatedNumber
   requestAnimationFrame counter with cubic-bezier ease-out.

   Props:
     value    – target number (or numeric string, e.g. 147850 or "147,850")
     duration – animation duration in ms (default 900)
     prefix   – string prepended to formatted number (e.g. "₱")
     suffix   – string appended to formatted number (e.g. "%")
     decimals – decimal places for toFixed (0 = integer toLocaleString)

   Algorithm: t ↦ 1 - (1-t)^4  (quartic ease-out — snappy but smooth)
─────────────────────────────────────────────────────────────────────────────── */
export function AnimatedNumber({ value, duration = 900, prefix = '', suffix = '', isCurrency = false, decimals = 0 }) {
  const parseNumeric = (v) => {
    if (v == null || v === '—') return null;
    if (typeof v === 'number') return v;
    const cleaned = String(v).replace(/[^0-9.\-]/g, '');
    return cleaned === '' ? null : parseFloat(cleaned);
  };

  const toNum = parseNumeric(value);
  const [displayVal, setDisplayVal] = useState('0');
  const rafRef = useRef(null);

  const formatVal = useCallback((num) => {
    if (num == null) return value ?? '—';
    if (isCurrency) {
      return new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(num);
    }
    const fmtNum = decimals > 0
      ? num.toFixed(decimals)
      : new Intl.NumberFormat('en-US').format(Math.round(num));
    return `${prefix}${fmtNum}${suffix}`;
  }, [isCurrency, decimals, prefix, suffix, value]);

  useEffect(() => {
    if (toNum == null) {
      setDisplayVal(value ?? '—');
      return;
    }

    cancelAnimationFrame(rafRef.current);
    const startTime = performance.now();
    const easeOut = (t) => 1 - Math.pow(1 - t, 4);

    const tick = (now) => {
      const elapsed  = now - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const current  = toNum * easeOut(progress);

      setDisplayVal(formatVal(current));

      if (progress < 1) {
        rafRef.current = requestAnimationFrame(tick);
      } else {
        setDisplayVal(formatVal(toNum));
      }
    };

    rafRef.current = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(rafRef.current);
  }, [toNum, duration, formatVal, value]);

  return <span>{displayVal}</span>;
}

/* ─────────────────────────────────────────────────────────────────────────────
   MetricCard3D
   World-Class B2B SaaS 3D Metric Card

   • Dynamic 3D tilt: perspective(1000px) rotateX/Y ±12deg on mouseMove via rAF
   • Radial gradient glare tracking cursor exact X/Y position
   • Staggered fadeInUp entrance: opacity 0→1 + translateY 24px→0
   • AnimatedNumber on numeric values (counts up/down smoothly)
   • Hardware-accelerated via willChange: transform
   • Reset to flat on mouseLeave with spring ease-back (0.5s cubic-bezier)
   • CSS variables for dual dark/light theme
─────────────────────────────────────────────────────────────────────────────── */
function MetricCard3D({ metric, delay, mounted, onCardClick, syncId }) {
  const cardRef = useRef(null);
  const rafRef  = useRef(null);

  const [tilt,    setTilt]    = useState({ x: 0, y: 0 });
  const [glare,   setGlare]   = useState({ x: 50, y: 50, opacity: 0 });
  const [hovered, setHovered] = useState(false);
  const [clicked, setClicked] = useState(false);

  // ── rAF-throttled 3D math ─────────────────────────────────────────────────
  const handleMouseMove = useCallback((e) => {
    if (!cardRef.current) return;
    cancelAnimationFrame(rafRef.current);
    rafRef.current = requestAnimationFrame(() => {
      if (!cardRef.current) return;
      const rect    = cardRef.current.getBoundingClientRect();
      const relX    = e.clientX - rect.left;
      const relY    = e.clientY - rect.top;
      const centerX = rect.width  / 2;
      const centerY = rect.height / 2;

      // Normalize position to [-1, +1] and map to ±12deg rotation
      const normX = (relX - centerX) / centerX;
      const normY = (relY - centerY) / centerY;

      setTilt({ x: normY * -12, y: normX * 12 });
      setGlare({
        x:       (relX / rect.width)  * 100,
        y:       (relY / rect.height) * 100,
        opacity: 0.25,
      });
    });
  }, []);

  const handleMouseEnter = useCallback(() => setHovered(true), []);

  const handleMouseLeave = useCallback(() => {
    cancelAnimationFrame(rafRef.current);
    setHovered(false);
    setTilt({ x: 0, y: 0 });             // spring-back handled by CSS transition
    setGlare(g => ({ ...g, opacity: 0 }));
  }, []);

  const handleClick = () => {
    setClicked(true);
    setTimeout(() => setClicked(false), 200);
    if (onCardClick) onCardClick(metric);
  };

  useEffect(() => () => cancelAnimationFrame(rafRef.current), []);

  // ── Staggered entrance ─────────────────────────────────────────────────────
  const staggerStyle = {
    opacity:    mounted ? 1 : 0,
    transform:  mounted ? 'translateY(0)' : 'translateY(24px)',
    transition: `opacity 0.5s cubic-bezier(0.16,1,0.3,1) ${delay}ms,
                 transform 0.5s cubic-bezier(0.16,1,0.3,1) ${delay}ms`,
    willChange: 'opacity, transform',
  };

  // ── 3D card surface ────────────────────────────────────────────────────────
  const cardStyle = {
    width:          '100%',
    height:         '100%',
    position:       'relative',
    background:     hovered ? 'var(--bg-card-h, rgba(255,255,255,0.048))' : 'var(--bg-card, rgba(255,255,255,0.030))',
    border:         `1px solid ${hovered ? 'var(--border-h, rgba(255,255,255,0.16))' : 'var(--border, rgba(255,255,255,0.07))'}`,
    borderRadius:   16,
    padding:        '20px 20px 18px',
    cursor:         'pointer',
    overflow:       'hidden',
    boxSizing:      'border-box',
    transform:      hovered
      ? `perspective(1000px) rotateX(${tilt.x}deg) rotateY(${tilt.y}deg) scale(1.02) translateZ(8px)`
      : `perspective(1000px) rotateX(0deg) rotateY(0deg) scale(${clicked ? 0.97 : 1}) translateZ(0px)`,
    transition:     hovered
      ? 'transform 0.1s ease-out, border-color 0.2s ease, box-shadow 0.25s ease, background 0.2s ease'
      : 'transform 0.5s cubic-bezier(0.16, 1, 0.3, 1), border-color 0.3s ease, box-shadow 0.35s ease, background 0.3s ease',
    boxShadow:      hovered
      ? `0 20px 40px ${metric.glow}, 0 0 0 1px rgba(255,255,255,0.05)`
      : '0 4px 16px rgba(0,0,0,0.25)',
    willChange:     'transform',
    transformStyle: 'preserve-3d',
  };

  // ── Radial glare overlay ───────────────────────────────────────────────────
  const glareStyle = {
    position:      'absolute',
    inset:          0,
    borderRadius:   16,
    pointerEvents: 'none',
    background:    `radial-gradient(circle at ${glare.x}% ${glare.y}%, rgba(255,255,255,${glare.opacity}) 0%, rgba(255,255,255,0) 65%)`,
    transition:    hovered ? 'none' : 'opacity 0.4s ease',
    zIndex:         2,
  };

  // Robust numeric parser — handles number, numeric string, or ₱-prefixed string from API.
  // Using Number(String(v).replace()) ensures AnimatedNumber always fires regardless of
  // whether the JSON serializer sends 147850 or "147850" or "₱147,850.00".
  const isCurrency = Boolean(metric.isCurrency);
  const numRaw = (metric.value != null && metric.value !== '—')
    ? Number(String(metric.value).replace(/[^0-9.-]+/g, ''))
    : null;

  return (
    <div style={staggerStyle}>
      <div
        ref={cardRef}
        style={cardStyle}
        onMouseMove={handleMouseMove}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        onClick={handleClick}
      >
        {/* Radial glare overlay */}
        <div style={glareStyle} />

        {/* Top shine */}
        <div style={{
          position: 'absolute', top: 0, left: 0, right: 0, height: '50%',
          borderRadius: '16px 16px 0 0',
          background: 'linear-gradient(180deg, var(--card-shine, rgba(255,255,255,0.04)) 0%, transparent 100%)',
          pointerEvents: 'none', zIndex: 1,
        }} />

        {/* Content */}
        <div style={{ position: 'relative', zIndex: 3 }}>

          {/* Icon badge */}
          <div style={{
            width: 38, height: 38, borderRadius: 10,
            background: metric.glow,
            border: `1px solid ${metric.color}35`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 18, marginBottom: 16,
            transform: hovered ? 'translateZ(10px)' : 'translateZ(0)',
            transition: 'transform 0.2s ease',
          }}>
            {metric.icon}
          </div>

          {/* Animated value — skeleton on null (first load), AnimatedNumber otherwise */}
          <div style={{
            fontSize: 28, fontWeight: 800,
            color: 'var(--text-primary, #fff)',
            lineHeight: 1, marginBottom: 4, letterSpacing: '-0.025em',
            transform: hovered ? 'translateZ(8px)' : 'translateZ(0)',
            transition: 'transform 0.2s ease',
          }}>
            {metric.value == null
              ? <span className="skeleton" style={{ display: 'inline-block', width: 64, height: 26 }} />
              : numRaw != null
                ? <AnimatedNumber
                    key={`num-${metric.label}-${syncId}`}
                    value={numRaw}
                    isCurrency={isCurrency}
                    duration={1000}
                  />
                : metric.value
            }
          </div>

          {/* Label */}
          <div style={{
            fontSize: 10, fontWeight: 800, color: metric.color,
            letterSpacing: '0.07em', textTransform: 'uppercase', marginBottom: 5,
          }}>
            {metric.label}
          </div>

          {/* Sub-label */}
          <div style={{
            fontSize: 10,
            color: 'var(--text-sub, rgba(255,255,255,0.32))',
            fontWeight: 500, marginBottom: 12,
          }}>
            {metric.sub}
          </div>

          <div style={{ height: 1, background: 'var(--border, rgba(255,255,255,0.06))', marginBottom: 10 }} />

          {/* Trend indicator */}
          <div style={{
            fontSize: 10, fontWeight: 700,
            color: metric.trendUp
              ? 'var(--green, #34d399)'
              : 'var(--red, #f87171)',
            display: 'flex', alignItems: 'center', gap: 4,
          }}>
            <span style={{ fontSize: 11 }}>{metric.trendUp ? '↑' : '↓'}</span>
            {metric.trend}
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────────────────────────────────────
   DashboardOverview Component

   Features:
   • Fetches live data from /api/admin/dashboard/summary on mount & on demand
   • window.forceDashboardRefresh() global for C# WinForms interop
   • AnimatedNumber counters (quartic ease-out rAF) on all 8 metric cards
   • Staggered fadeInUp entrance: Card 1 @ 0ms, Card 2 @ 45ms, …
   • 3D tilt + glare on MetricCard3D components
   • Live recentBookings from API response (graceful empty-state fallback)
   • CSS variable theming — window.setDashboardTheme('dark'|'light')
   • Refresh button with spinning icon state
─────────────────────────────────────────────────────────────────────────────── */
export default function DashboardOverview({
  onRefresh,
  onTriggerAiInsights,
  parentLoadingState = false,
  stats = {},
}) {
  const [isRefreshing,   setIsRefreshing]   = useState(false);
  const [isAiProcessing, setIsAiProcessing] = useState(false);
  const [mounted,        setMounted]        = useState(false);
  const [fetchedStats,   setFetchedStats]   = useState(null);   // null = not yet fetched
  const [lastSyncTime,   setLastSyncTime]   = useState(new Date());
  const [recentBookings, setRecentBookings] = useState([]);
  const [fetchStatus,    setFetchStatus]    = useState('idle'); // 'idle' | 'loading' | 'success' | 'error'
  const [apiError,       setApiError]       = useState(null);
  const [syncId,         setSyncId]         = useState(() => Date.now());

  // ── DATA FETCHING & ERROR MAPPER ───────────────────────────────────────────
  const fetchDashboardData = useCallback(async () => {
    setIsRefreshing(true);
    setFetchStatus('loading');
    setApiError(null);
    try {
      if (onRefresh) await onRefresh();

      let apiBase = (window.API_BASE_URL || 'http://localhost:5233').replace(/\/$/, '');
      if (apiBase.toLowerCase().endsWith('/api')) {
        apiBase = apiBase.substring(0, apiBase.length - 4);
      }

      const token   = window.AUTH_TOKEN || '';
      const headers = {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {})
      };

      // Endpoint fallback chain
      const endpoints = [
        `${apiBase}/api/admin/dashboard/summary`,
        `${apiBase}/api/dashboard/stats`,
        `${apiBase}/api/dashboard/summary`,
      ];

      let successData = null;
      let lastStatusCode = null;
      let lastStatusText = null;

      for (const url of endpoints) {
        try {
          const res = await fetch(url, { headers });
          if (res.ok) {
            successData = await res.json();
            break;
          } else {
            lastStatusCode = res.status;
            lastStatusText = res.statusText;
          }
        } catch (e) {
          // Keep attempting fallback endpoints
        }
      }

      if (successData) {
        setFetchedStats({
          totalVehicles:  successData.totalVehicles    ?? successData.fleetSize      ?? 0,
          activeRentals:  successData.activeRentals                                  ?? 0,
          pendingBookings:successData.pendingRentals   ?? successData.pendingBookings ?? 0,
          totalRevenue:   successData.totalRevenueAllTime ?? successData.totalRevenue ?? 0,
          revenueThisMonth:successData.revenueThisMonth?? successData.monthlyRevenue ?? 0,
          totalDrivers:   successData.totalDrivers     ?? successData.totalUsers      ?? 0,
          maintenanceDue: successData.overdue          ?? successData.maintenanceDue  ?? 0,
          openIssues:     successData.openIssues       ?? successData.incidents        ?? 0,

          fleetUtilization:    successData.fleetUtilization    ?? 0,
          onTimeReturns:       successData.onTimeReturns       ?? 0,
          driverRatingPercent: successData.driverRatingPercent ?? 0,
          revenueTargetPct:    successData.revenueTargetPct    ?? 0,
          customerSatPct:      successData.customerSatPct      ?? 0,
          healthStatus:        successData.healthStatus        || 'operational',
          daysToMaintenance:   successData.daysToMaintenance   || 3,
        });

        const bookingsRaw = successData.recentBookings || successData.bookings;
        if (Array.isArray(bookingsRaw)) {
          setRecentBookings(bookingsRaw.slice(0, 10));
        }
        setSyncId(Date.now());
        setFetchStatus('success');
        setLastSyncTime(new Date());
      } else {
        // Clean, concise error message mapping (no 'API Error: API Error' duplication)
        let errorMsg = '';
        if (lastStatusCode === 401 || lastStatusCode === 403) {
          errorMsg = '401 Unauthorized: Invalid or expired Auth Token.';
        } else if (lastStatusCode === 404) {
          errorMsg = '404 Not Found: The backend API endpoint is missing or unreachable.';
        } else if (lastStatusCode === 500) {
          errorMsg = '500 Internal Server Error: The C# backend encountered an exception.';
        } else if (lastStatusCode === 504) {
          errorMsg = '504 Gateway Timeout: The database is unresponsive.';
        } else if (lastStatusCode) {
          errorMsg = `${lastStatusCode} Server Error: ${lastStatusText || 'The backend server returned an error'}.`;
        } else {
          errorMsg = `Connection Failed: Unable to connect to backend API server at ${apiBase}.`;
        }

        setApiError(errorMsg);
        setFetchStatus('error');
      }
    } catch (err) {
      console.error('[Dashboard] Fetch failed:', err);
      setApiError(err.message || 'Network Connection Error');
      setFetchStatus('error');
    } finally {
      setTimeout(() => setIsRefreshing(false), 500);
    }
  }, [onRefresh]);

  // ── GLOBAL C# WINFORMS INTEROP TRIGGER ────────────────────────────────────
  // C# WinForms calls:
  //   await _dashWebView.CoreWebView2.ExecuteScriptAsync(
  //     "if(window.forceDashboardRefresh) window.forceDashboardRefresh();"
  //   );
  useEffect(() => {
    window.forceDashboardRefresh = () => fetchDashboardData();
    window.refreshDashboardData  = () => fetchDashboardData();
    return () => {
      delete window.forceDashboardRefresh;
      delete window.refreshDashboardData;
    };
  }, [fetchDashboardData]);

  // ── AUTO-FETCH ON MOUNT ────────────────────────────────────────────────────
  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  // ── DOUBLE-rAF STAGGER TRIGGER ─────────────────────────────────────────────
  // Ensures CSS transitions fire after first paint (avoids invisible initial flash)
  useEffect(() => {
    const id = requestAnimationFrame(() =>
      requestAnimationFrame(() => setMounted(true))
    );
    return () => cancelAnimationFrame(id);
  }, []);

  const handleAiInsights = async () => {
    if (isAiProcessing) return;  // guard against double-click
    setIsAiProcessing(true);
    try { if (onTriggerAiInsights) await onTriggerAiInsights(); }
    catch (err) { console.error('[Dashboard] AI Insights failed:', err); }
    finally {
      // Always release — never permanently stuck
      setIsAiProcessing(false);
    }
  };

  // Merge prop stats & fetched stats (live API data takes priority)
  const activeData = fetchedStats || stats || {};

  // Is data still being fetched for the first time?
  const isFirstLoad = fetchStatus === 'idle' || (fetchStatus === 'loading' && !fetchedStats);

  const fmt = (n) => n != null ? Number(n).toLocaleString() : null;

  // ── Metric Cards Data Matrix ───────────────────────────────────────────────
  // When isFirstLoad=true, value=null triggers the skeleton pulse; no 0s shown.
  const metrics = useMemo(() => [
    {
      label: 'Total Fleet',
      value: isFirstLoad ? null : (activeData.totalVehicles != null ? Number(activeData.totalVehicles) : null),
      icon: '🚗', color: '#f97316', glow: 'rgba(249,115,22,0.22)',
      sub: 'Registered vehicles', trend: '+2 this month', trendUp: true,
    },
    {
      label: 'Active Rentals',
      value: isFirstLoad ? null : (activeData.activeRentals != null ? Number(activeData.activeRentals) : null),
      icon: '🔑', color: '#22d3ee', glow: 'rgba(34,211,238,0.18)',
      sub: 'Ongoing trips', trend: 'Live tracking', trendUp: true,
    },
    {
      label: 'Pending Bookings',
      value: isFirstLoad ? null : (activeData.pendingBookings != null ? Number(activeData.pendingBookings) : null),
      icon: '📋', color: '#a78bfa', glow: 'rgba(167,139,250,0.18)',
      sub: 'Awaiting confirmation', trend: 'Action required', trendUp: false,
    },
    {
      // Pass raw numeric value + isCurrency flag — AnimatedNumber handles ₱ formatting
      label: 'Total Revenue',
      value: isFirstLoad ? null
        : activeData.totalRevenue != null
          ? Number(activeData.totalRevenue)
          : activeData.revenueThisMonth != null
            ? Number(activeData.revenueThisMonth)
            : null,
      isCurrency: true,
      icon: '💰', color: '#34d399', glow: 'rgba(52,211,153,0.18)',
      sub: 'All-time earnings', trend: '+14% vs last mo.', trendUp: true,
    },
    {
      label: 'Total Drivers',
      value: isFirstLoad ? null : (activeData.totalDrivers != null ? Number(activeData.totalDrivers) : null),
      icon: '👤', color: '#fb923c', glow: 'rgba(251,146,60,0.18)',
      sub: 'Registered drivers', trend: 'Active workforce', trendUp: true,
    },
    {
      label: 'Maintenance Due',
      value: isFirstLoad ? null : (activeData.maintenanceDue != null ? Number(activeData.maintenanceDue) : null),
      icon: '🔧', color: '#f43f5e', glow: 'rgba(244,63,94,0.18)',
      sub: 'Vehicles overdue', trend: 'Schedule service', trendUp: false,
    },
    {
      // Pass raw numeric value + isCurrency flag — AnimatedNumber handles ₱ formatting
      label: 'Monthly Revenue',
      value: isFirstLoad ? null
        : activeData.revenueThisMonth != null
          ? Number(activeData.revenueThisMonth)
          : null,
      isCurrency: true,
      icon: '📈', color: '#facc15', glow: 'rgba(250,204,21,0.18)',
      sub: 'This month', trend: 'On target', trendUp: true,
    },
    {
      label: 'Open Incidents',
      value: isFirstLoad ? null : (activeData.openIssues != null ? Number(activeData.openIssues) : null),
      icon: '⚠️', color: '#fb7185', glow: 'rgba(251,113,133,0.18)',
      sub: 'Pending reports', trend: 'All clear', trendUp: true,
    },
  ], [activeData, isFirstLoad]);

  // ── Quick Stats KPIs — 100% Dynamic DB Telemetry (0%/dash when offline) ─────
  const isOffline = fetchStatus === 'error' || Boolean(apiError) || !fetchedStats;

  const quickStats = useMemo(() => [
    {
      label: 'Fleet Utilization',
      value: isOffline ? 0 : Number(fetchedStats?.fleetUtilization ?? fetchedStats?.occupancyRate ?? 0),
      displayVal: isOffline ? '—' : `${Math.round(fetchedStats?.fleetUtilization ?? fetchedStats?.occupancyRate ?? 0)}%`,
      color: '#f97316'
    },
    {
      label: 'On-Time Returns',
      value: isOffline ? 0 : Number(fetchedStats?.onTimeReturns ?? 0),
      displayVal: isOffline ? '—' : `${Math.round(fetchedStats?.onTimeReturns ?? 0)}%`,
      color: '#22d3ee'
    },
    {
      label: 'Driver Rating Avg',
      value: isOffline ? 0 : Number(fetchedStats?.driverRatingPercent ?? fetchedStats?.driverRatingAvg ?? 0),
      displayVal: isOffline ? '—' : `${Math.round(fetchedStats?.driverRatingPercent ?? fetchedStats?.driverRatingAvg ?? 0)}%`,
      color: '#a78bfa'
    },
    {
      label: 'Revenue Target',
      value: isOffline ? 0 : Number(fetchedStats?.revenueTargetPct ?? fetchedStats?.revenueTarget ?? 0),
      displayVal: isOffline ? '—' : `${Math.round(fetchedStats?.revenueTargetPct ?? fetchedStats?.revenueTarget ?? 0)}%`,
      color: '#34d399'
    },
    {
      label: 'Customer Satisfaction',
      value: isOffline ? 0 : Number(fetchedStats?.customerSatPct ?? fetchedStats?.customerSatisfaction ?? 0),
      displayVal: isOffline ? '—' : `${Math.round(fetchedStats?.customerSatPct ?? fetchedStats?.customerSatisfaction ?? 0)}%`,
      color: '#facc15'
    },
  ], [fetchedStats, isOffline]);

  const statusMap = {
    Active:    { bg: 'rgba(34,197,94,0.12)',  color: '#4ade80', dot: '#22c55e' },
    Pending:   { bg: 'rgba(250,204,21,0.12)', color: '#fbbf24', dot: '#f59e0b' },
    Completed: { bg: 'rgba(34,211,238,0.12)', color: '#67e8f9', dot: '#22d3ee' },
    Cancelled: { bg: 'rgba(244,63,94,0.12)',  color: '#fb7185', dot: '#f43f5e' },
  };

  // Stagger helper
  const fadeUp = (delay) => ({
    opacity:    mounted ? 1 : 0,
    transform:  mounted ? 'translateY(0)' : 'translateY(24px)',
    transition: `opacity 0.5s cubic-bezier(0.16,1,0.3,1) ${delay}ms,
                 transform 0.5s cubic-bezier(0.16,1,0.3,1) ${delay}ms`,
    willChange: 'opacity, transform',
  });

  const hasLiveBookings = recentBookings.length > 0;

  return (
    <>
      <style>{`
        @keyframes fadeInUp {
          from { opacity: 0; transform: translateY(24px); }
          to   { opacity: 1; transform: translateY(0); }
        }

        /* ── Dual-theme CSS variables ─────────────────────────────────── */
        :root,
        [data-theme="dark"] {
          --bg-main:      #07070e;
          --bg-card:      rgba(255,255,255,0.030);
          --bg-card-h:    rgba(255,255,255,0.048);
          --bg-input:     rgba(255,255,255,0.06);
          --text-primary: #e2e8f0;
          --text-sub:     rgba(255,255,255,0.30);
          --text-muted:   rgba(255,255,255,0.18);
          --border:       rgba(255,255,255,0.07);
          --border-h:     rgba(255,255,255,0.16);
          --scrollbar:    rgba(255,255,255,0.09);
          --accent:       #f97316;
          --accent-glow:  rgba(249,115,22,0.22);
          --card-shine:   rgba(255,255,255,0.04);
          --green:        #34d399;
          --red:          #f87171;
        }

        [data-theme="light"] {
          --bg-main:      #f1f5f9;
          --bg-card:      rgba(255,255,255,0.80);
          --bg-card-h:    rgba(255,255,255,0.95);
          --bg-input:     rgba(0,0,0,0.04);
          --text-primary: #0f172a;
          --text-sub:     rgba(15,23,42,0.48);
          --text-muted:   rgba(15,23,42,0.28);
          --border:       rgba(15,23,42,0.10);
          --border-h:     rgba(15,23,42,0.22);
          --scrollbar:    rgba(15,23,42,0.12);
          --accent:       #ea580c;
          --accent-glow:  rgba(234,88,12,0.18);
          --card-shine:   rgba(0,0,0,0.02);
          --green:        #16a34a;
          --red:          #dc2626;
        }

        ::-webkit-scrollbar { width: 5px; height: 5px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb {
          background: var(--scrollbar);
          border-radius: 4px;
        }
        ::-webkit-scrollbar-thumb:hover { background: var(--accent-glow); }

        .dash-action-btn {
          display: flex; align-items: center; gap: 7px;
          background: var(--bg-input);
          border: 1px solid var(--border);
          border-radius: 10px;
          color: var(--text-primary);
          padding: 8px 16px;
          font-size: 12px; font-weight: 600; font-family: inherit;
          cursor: pointer;
          transition: background 0.2s ease, border-color 0.2s ease, transform 0.15s ease;
          user-select: none;
        }
        .dash-action-btn:hover  { background: var(--bg-card-h); border-color: var(--border-h); }
        .dash-action-btn:active { transform: scale(0.97); }
        .dash-action-btn:disabled { opacity: 0.5; cursor: not-allowed; transform: none; }

        .dash-ai-btn {
          display: flex; align-items: center; gap: 7px;
          background: linear-gradient(135deg, #ea580c 0%, #d97706 100%);
          border: 1px solid rgba(249,115,22,0.45);
          border-radius: 10px;
          color: #fff;
          padding: 8px 18px;
          font-size: 12px; font-weight: 700; font-family: inherit;
          cursor: pointer;
          box-shadow: 0 0 20px var(--accent-glow);
          transition: box-shadow 0.2s ease, transform 0.15s ease;
          user-select: none;
        }
        .dash-ai-btn:hover  { box-shadow: 0 0 36px rgba(234,88,12,0.55); transform: translateY(-1px); }
        .dash-ai-btn:active { transform: scale(0.97) translateY(0); }
        .dash-ai-btn:disabled { opacity: 0.55; cursor: not-allowed; transform: none; box-shadow: none; }

        .tbl-row { transition: background 0.15s ease; }
        .tbl-row:hover { background: var(--bg-card-h) !important; }

        .progress-track { background: var(--border); border-radius: 999px; height: 5px; overflow: hidden; }
        .progress-fill  { height: 100%; border-radius: 999px; transition: width 1.3s cubic-bezier(0.16,1,0.3,1); }

        @keyframes pulse-ring {
          0%   { box-shadow: 0 0 0 0   rgba(249,115,22,0.7); }
          70%  { box-shadow: 0 0 0 8px rgba(249,115,22,0);   }
          100% { box-shadow: 0 0 0 0   rgba(249,115,22,0);   }
        }
        .live-dot {
          display: inline-block; width: 8px; height: 8px; border-radius: 50%;
          background: var(--accent);
          animation: pulse-ring 2s ease-out infinite;
          flex-shrink: 0;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        .spinning { animation: spin 0.8s linear infinite; display: inline-block; }
        /* ── Skeleton pulse ──────────────────────────────────────────── */
        @keyframes skeletonPulse {
          0%, 100% { opacity: 0.4; }
          50%       { opacity: 0.9; }
        }
        .skeleton {
          background: linear-gradient(90deg,
            rgba(255,255,255,0.06) 25%,
            rgba(255,255,255,0.12) 50%,
            rgba(255,255,255,0.06) 75%);
          background-size: 200% 100%;
          animation: skeletonPulse 1.4s ease-in-out infinite;
          border-radius: 6px;
        }
      `}</style>

      <div style={{
        background: 'var(--bg-main)',
        minHeight: '100%',
        padding: '26px 28px 32px',
        fontFamily: "'Inter', 'Segoe UI', system-ui, sans-serif",
        color: 'var(--text-primary)',
        boxSizing: 'border-box',
        overflowY: 'auto',
        overflowX: 'hidden',
        transition: 'background 0.35s ease, color 0.35s ease',
      }}>

        {/* ── Header — Stagger Delay 0ms ──────────────────────────────── */}
        <div style={{
          display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start',
          marginBottom: 28, flexWrap: 'wrap', gap: 14,
          ...fadeUp(0),
        }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 5 }}>
              <span className="live-dot" />
              <h1 style={{
                margin: 0, fontSize: 20, fontWeight: 800,
                color: 'var(--text-primary)', letterSpacing: '-0.025em',
              }}>
                Dashboard Overview
              </h1>
            </div>
            <p style={{
              margin: 0, fontSize: 12,
              color: 'var(--text-sub)', fontWeight: 500, paddingLeft: 18,
            }}>
              Real-time fleet activity, operational metrics &amp; revenue tracking
            </p>
          </div>

          <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
            <span style={{ fontSize: 10, color: 'var(--text-muted)', fontWeight: 600, paddingRight: 4 }}>
              {fetchStatus === 'error'
                ? <span style={{ color: '#f87171' }}>⚠ Error: {apiError}</span>
                : <>Synced {lastSyncTime.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })}</>
              }
            </span>
            <button
              className="dash-action-btn"
              onClick={fetchDashboardData}
              disabled={isRefreshing || parentLoadingState}
            >
              <span className={isRefreshing || parentLoadingState ? 'spinning' : ''} style={{ fontSize: 14 }}>↻</span>
              Refresh Data
            </button>
            <button
              className="dash-ai-btn"
              onClick={handleAiInsights}
              disabled={isAiProcessing}
              style={{
                pointerEvents: 'auto',
                zIndex: 10,
                position: 'relative',
                cursor: isAiProcessing ? 'wait' : 'pointer',
              }}
            >
              <span style={{ fontSize: 12 }}>{isAiProcessing ? '⟳' : '✦'}</span>
              {isAiProcessing ? 'Analyzing...' : 'AI Business Insights'}
            </button>
          </div>
        </div>

        {/* ── Error Banner — Conditional Rendering ──── */}
        {apiError && (
          <div style={{
            background: 'rgba(239, 68, 68, 0.12)',
            border: '1px solid rgba(239, 68, 68, 0.35)',
            borderRadius: 12,
            padding: '14px 18px',
            marginBottom: 24,
            color: '#f87171',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            fontSize: 13,
            fontWeight: 600,
            boxShadow: '0 4px 16px rgba(239, 68, 68, 0.1)',
            ...fadeUp(0),
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <span style={{ fontSize: 18 }}>⚠️</span>
              <span>API Error: {apiError}</span>
            </div>
            <button
              className="dash-action-btn"
              onClick={fetchDashboardData}
              disabled={isRefreshing}
              style={{
                background: 'rgba(239, 68, 68, 0.15)',
                borderColor: 'rgba(239, 68, 68, 0.4)',
                color: '#f87171',
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              <span className={isRefreshing ? 'spinning' : ''} style={{ fontSize: 14 }}>↻</span>
              {isRefreshing ? 'Retrying...' : 'Retry Connection'}
            </button>
          </div>
        )}

        {/* ── Metric Cards — 4×2 Grid, Stagger Card 1@0ms … Card 8@315ms ─── */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gap: 13,
          marginBottom: 20,
        }}>
          {metrics.map((m, i) => (
            <MetricCard3D
              key={m.label}
              metric={m}
              delay={i * 45}
              mounted={mounted}
              syncId={syncId}
            />
          ))}
        </div>

        {/* ── Bottom split: Bookings table + Quick Stats ────────────────── */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 320px', gap: 13 }}>

          {/* Recent Bookings Table */}
          <div style={{
            background: 'var(--bg-card)',
            border: '1px solid var(--border)',
            borderRadius: 16,
            overflow: 'hidden',
            ...fadeUp(200),
          }}>
            <div style={{
              padding: '16px 20px',
              borderBottom: '1px solid var(--border)',
              display: 'flex', justifyContent: 'space-between', alignItems: 'center',
            }}>
              <div>
                <div style={{ fontSize: 13, fontWeight: 800, color: 'var(--text-primary)', marginBottom: 2 }}>
                  Recent Bookings
                </div>
                <div style={{ fontSize: 10, color: 'var(--text-sub)' }}>
                  {hasLiveBookings ? 'Live rental transactions' : 'Latest rental transactions'}
                </div>
              </div>
              <span style={{
                background: 'var(--accent-glow)', color: 'var(--accent)',
                padding: '3px 10px', borderRadius: 999,
                fontSize: 10, fontWeight: 700,
              }}>
                {hasLiveBookings ? `${recentBookings.length} Live` : 'No Data'}
              </span>
            </div>

            {hasLiveBookings ? (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11 }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}>
                    {['Booking ID', 'Customer', 'Vehicle', 'Date', 'Status', 'Amount'].map(h => (
                      <th key={h} style={{
                        padding: '9px 18px', textAlign: 'left',
                        color: 'var(--text-muted)', fontWeight: 700,
                        fontSize: 9, letterSpacing: '0.08em', textTransform: 'uppercase',
                      }}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {recentBookings.map((b, i) => {
                    const rawStatus = b.status || b.rentalStatus || 'Pending';
                    const status    = rawStatus.charAt(0).toUpperCase() + rawStatus.slice(1).toLowerCase();
                    const s         = statusMap[status] || statusMap.Pending;
                    const amount = (b.amount ?? b.totalAmount) != null
                      ? new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(b.amount ?? b.totalAmount)
                      : '—';
                    const dateStr   = b.date || b.startDate || b.createdAt || '';
                    const displayDate = dateStr
                      ? (String(dateStr).includes(',') ? dateStr : new Date(dateStr).toLocaleDateString('en-PH', { month: 'short', day: 'numeric', year: 'numeric' }))
                      : '—';
                    const bookingId = b.bookingId || (b.id ? `BK-${b.id}` : `BK-${1000 + i}`);
                    const customer  = b.customerName || b.customer || b.fullName || 'Customer';
                    const vehicle   = b.vehicleInfo || b.vehicleName || b.vehicle || 'Vehicle';

                    return (
                      <tr
                        key={bookingId}
                        className="tbl-row"
                        style={{
                          borderBottom: '1px solid var(--border)',
                          ...fadeUp(220 + i * 25),
                        }}
                      >
                        <td style={{ padding: '11px 18px', color: 'var(--accent)', fontFamily: 'monospace', fontSize: 10, fontWeight: 700 }}>
                          {String(bookingId).startsWith('BK') ? bookingId : `BK-${bookingId}`}
                        </td>
                        <td style={{ padding: '11px 18px', color: 'var(--text-primary)', fontWeight: 600 }}>{customer}</td>
                        <td style={{ padding: '11px 18px', color: 'var(--text-sub)', fontWeight: 500 }}>{vehicle}</td>
                        <td style={{ padding: '11px 18px', color: 'var(--text-sub)', fontWeight: 500 }}>{displayDate}</td>
                        <td style={{ padding: '11px 18px' }}>
                          <span style={{
                            background: s.bg, color: s.color,
                            padding: '3px 9px', borderRadius: 999,
                            fontSize: 9, fontWeight: 700, letterSpacing: '0.05em',
                            display: 'inline-flex', alignItems: 'center', gap: 5,
                          }}>
                            <span style={{ width: 4, height: 4, borderRadius: '50%', background: s.dot }} />
                            {status}
                          </span>
                        </td>
                        <td style={{ padding: '11px 18px', color: 'var(--green)', fontWeight: 700 }}>{amount}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            ) : (
              <div style={{
                display: 'flex', flexDirection: 'column',
                alignItems: 'center', justifyContent: 'center',
                padding: '40px 20px', gap: 10,
              }}>
                {isRefreshing ? (
                  <>
                    <span className="spinning" style={{ fontSize: 24, color: 'var(--accent)' }}>↻</span>
                    <span style={{ fontSize: 12, color: 'var(--text-sub)' }}>Loading bookings…</span>
                  </>
                ) : (
                  <>
                    <span style={{ fontSize: 28 }}>📋</span>
                    <span style={{ fontSize: 12, color: 'var(--text-sub)' }}>No recent bookings yet</span>
                    <span style={{ fontSize: 10, color: 'var(--text-muted)' }}>
                      Bookings appear here once the API returns data
                    </span>
                  </>
                )}
              </div>
            )}
          </div>

          {/* Quick Stats Panel */}
          <div style={{
            background: 'var(--bg-card)',
            border: '1px solid var(--border)',
            borderRadius: 16,
            padding: '20px',
            ...fadeUp(200),
          }}>
            <div style={{ marginBottom: 20 }}>
              <div style={{ fontSize: 13, fontWeight: 800, color: 'var(--text-primary)', marginBottom: 2 }}>
                Quick Stats
              </div>
              <div style={{ fontSize: 10, color: 'var(--text-sub)' }}>
                Operational KPIs at a glance
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {quickStats.map((qs, i) => (
                <div key={qs.label} style={fadeUp(220 + i * 35)}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 7 }}>
                    <span style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-sub)' }}>
                      {qs.label}
                    </span>
                    <span style={{ fontSize: 11, fontWeight: 800, color: isOffline ? 'var(--text-muted)' : qs.color }}>
                      {isOffline ? qs.displayVal : (
                        <AnimatedNumber
                          key={`qs-${qs.label}-${syncId}`}
                          value={qs.value}
                          suffix="%"
                          duration={1100}
                        />
                      )}
                    </span>
                  </div>
                  <div className="progress-track">
                    <div
                      className="progress-fill"
                      style={{
                        width: mounted ? `${qs.value}%` : '0%',
                        background: isOffline ? 'rgba(255,255,255,0.1)' : `linear-gradient(90deg, ${qs.color}66, ${qs.color})`,
                        transitionDelay: `${300 + i * 80}ms`,
                      }}
                    />
                  </div>
                </div>
              ))}
            </div>

            {/* Fleet Health Summary */}
            <div style={{
              marginTop: 22, padding: '14px 15px',
              background: 'var(--accent-glow)',
              border: '1px solid rgba(249,115,22,0.2)',
              borderRadius: 12,
              ...fadeUp(440),
            }}>
              <div style={{
                fontSize: 9, fontWeight: 800, color: 'var(--accent)',
                letterSpacing: '0.07em', textTransform: 'uppercase', marginBottom: 6,
              }}>
                Fleet Health Summary
              </div>
              <div style={{ fontSize: 11, color: 'var(--text-sub)', lineHeight: 1.65 }}>
                {isOffline ? (
                  <span>Awaiting system telemetry...</span>
                ) : (
                  <>
                    Overall system is{' '}
                    <span style={{ color: 'var(--green)', fontWeight: 700 }}>
                      {fetchedStats?.healthStatus || 'operational'}
                    </span>.
                    {fetchedStats?.maintenanceDue > 0 ? (
                      <> <span style={{ color: '#fbbf24', fontWeight: 700 }}>
                            {fetchedStats.maintenanceDue} vehicle(s) overdue for service.
                          </span></>
                    ) : (
                      <> Next maintenance check in <span style={{ color: '#fbbf24', fontWeight: 700 }}>{fetchedStats?.daysToMaintenance || 3} days</span>.</>
                    )}
                  </>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
