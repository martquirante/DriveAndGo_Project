// DriveAndGo Fleet Management Overview - Advanced React Application (FleetOps Pro Architecture)
const { useState, useEffect, useRef, useMemo, useCallback } = React;

const rawApi = (window.API_BASE_URL || (typeof window !== 'undefined' && window.location.hostname && window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1' && window.location.hostname !== 'appassets' ? `${window.location.protocol}//${window.location.hostname}:5233/api` : 'http://localhost:5233/api')).trim();
const API = rawApi.replace(/\/$/, '').replace(/\/api$/, '');
const HEADERS = {
    'Content-Type': 'application/json',
    ...(window.AUTH_TOKEN ? { 'Authorization': `Bearer ${window.AUTH_TOKEN}` } : {})
};

const formatPHP = (val) => new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(val || 0);

const statusColorMap = {
    'available': { text: 'text-emerald-400', border: 'border-emerald-400/30', bg: 'bg-emerald-400/10', hex: '#34d399' },
    'rented': { text: 'text-amber-400', border: 'border-amber-400/30', bg: 'bg-amber-400/10', hex: '#fbbf24' },
    'in-use': { text: 'text-amber-400', border: 'border-amber-400/30', bg: 'bg-amber-400/10', hex: '#fbbf24' },
    'maintenance': { text: 'text-purple-400', border: 'border-purple-400/30', bg: 'bg-purple-400/10', hex: '#c084fc' },
    'retired': { text: 'text-red-400', border: 'border-red-400/30', bg: 'bg-red-400/10', hex: '#f87171' },
    'default': { text: 'text-blue-400', border: 'border-blue-400/30', bg: 'bg-blue-400/10', hex: '#60a5fa' }
};

const getStatusStyle = (s) => statusColorMap[(s || '').toLowerCase()] || statusColorMap.default;

const detectVehicleCategory = (type = '', brand = '', model = '') => {
    const str = `${type || ''} ${brand || ''} ${model || ''}`.toLowerCase();
    if (str.includes('pickup') || str.includes('truck') || str.includes('hilux') || str.includes('navara') || str.includes('ranger') || str.includes('d-max') || str.includes('dmax') || str.includes('strada') || str.includes('triton')) {
        return 'pickup';
    }
    if (str.includes('suv') || str.includes('fortuner') || str.includes('montero') || str.includes('everest') || str.includes('cr-v') || str.includes('crv') || str.includes('rav4') || str.includes('terra') || str.includes('crossover') || str.includes('pajero') || str.includes('jimny') || str.includes('land cruiser')) {
        return 'suv';
    }
    if (str.includes('van') || str.includes('hiace') || str.includes('urvan') || str.includes('alphard') || str.includes('starex') || str.includes('staria') || str.includes('mpv') || str.includes('innova') || str.includes('ertiga') || str.includes('avanza') || str.includes('xpander') || str.includes('livina') || str.includes('carnival')) {
        return 'van';
    }
    if (str.includes('hatchback') || str.includes('wigo') || str.includes('yaris') || str.includes('mirage') || str.includes('swift') || str.includes('brio') || str.includes('compact') || str.includes('mini')) {
        return 'hatchback';
    }
    if (str.includes('motorcycle') || str.includes('scooter') || str.includes('bike') || str.includes('nmax') || str.includes('aerox') || str.includes('pcx') || str.includes('click') || str.includes('vespa') || str.includes('adv') || str.includes('sniper')) {
        return 'motorcycle';
    }
    if (str.includes('coupe') || str.includes('sports') || str.includes('mustang') || str.includes('supra') || str.includes('gt86') || str.includes('brz') || str.includes('miata') || str.includes('z4') || str.includes('porsche')) {
        return 'coupe';
    }
    return 'sedan';
};

const renderDefaultTopDownVehicleSvg = (v, hex = '#38bdf8') => {
    const category = detectVehicleCategory(v?.type, v?.brand, v?.model);
    const uid = (v?.id || v?.plateNumber || Math.random().toString(36).substring(2, 7)).toString().replace(/[^a-zA-Z0-9]/g, '_');

    if (category === 'pickup') {
        return `
            <svg viewBox="0 0 44 80" class="w-12 h-20 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <defs>
                    <linearGradient id="pBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                        <stop offset="0%" stop-color="#334155"/>
                        <stop offset="50%" stop-color="#1e293b"/>
                        <stop offset="100%" stop-color="#0f172a"/>
                    </linearGradient>
                    <linearGradient id="pBed_${uid}" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stop-color="#090d16"/>
                        <stop offset="100%" stop-color="#161e2e"/>
                    </linearGradient>
                </defs>
                <rect x="2" y="24" width="4" height="6" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <rect x="38" y="24" width="4" height="6" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <path d="M 8 16 Q 8 6 22 4 Q 36 6 36 16 L 36 70 Q 36 76 22 76 Q 8 76 8 70 Z" fill="url(#pBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
                <path d="M 12 10 L 14 20 M 32 10 L 30 20" stroke="#475569" stroke-width="1"/>
                <path d="M 11 23 Q 22 20 33 23 L 31 32 Q 22 30 13 32 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8" opacity="0.95"/>
                <rect x="13" y="32" width="18" height="15" rx="2" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
                <rect x="14" y="47" width="16" height="3" rx="1" fill="#0f172a" stroke="#38bdf8" stroke-width="0.6"/>
                <rect x="11" y="52" width="22" height="21" rx="2" fill="url(#pBed_${uid})" stroke="#334155" stroke-width="1"/>
                <line x1="15" y1="55" x2="15" y2="70" stroke="#1e293b" stroke-width="1"/>
                <line x1="22" y1="55" x2="22" y2="70" stroke="#1e293b" stroke-width="1"/>
                <line x1="29" y1="55" x2="29" y2="70" stroke="#1e293b" stroke-width="1"/>
                <rect x="18" y="74" width="8" height="1.5" rx="0.5" fill="#64748b"/>
                <rect x="9" y="5" width="5" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="30" y="5" width="5" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="8.5" y="72" width="3" height="4" rx="1" fill="#ef4444"/>
                <rect x="32.5" y="72" width="3" height="4" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    if (category === 'suv') {
        return `
            <svg viewBox="0 0 44 76" class="w-12 h-19 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <defs>
                    <linearGradient id="suvBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                        <stop offset="0%" stop-color="#334155"/>
                        <stop offset="50%" stop-color="#1e293b"/>
                        <stop offset="100%" stop-color="#0f172a"/>
                    </linearGradient>
                </defs>
                <rect x="2" y="22" width="4" height="6" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <rect x="38" y="22" width="4" height="6" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <path d="M 8 14 Q 8 4 22 3 Q 36 4 36 14 L 37 66 Q 37 72 22 73 Q 7 72 7 66 Z" fill="url(#suvBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
                <path d="M 14 6 L 16 18 M 30 6 L 28 18" stroke="#475569" stroke-width="1.2"/>
                <path d="M 10 21 Q 22 18 34 21 L 32 32 Q 22 30 12 32 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8"/>
                <rect x="12" y="32" width="20" height="26" rx="2" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
                <rect x="15" y="34" width="14" height="10" rx="1.5" fill="#0f172a" stroke="#38bdf8" stroke-width="0.6"/>
                <rect x="10.5" y="28" width="1.5" height="28" rx="0.5" fill="#94a3b8"/>
                <rect x="32" y="28" width="1.5" height="28" rx="0.5" fill="#94a3b8"/>
                <path d="M 12 59 Q 22 57 32 59 L 33 66 Q 22 65 11 66 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.7"/>
                <rect x="8.5" y="4" width="6" height="3.5" rx="1.5" fill="#38bdf8"/>
                <rect x="29.5" y="4" width="6" height="3.5" rx="1.5" fill="#38bdf8"/>
                <rect x="8" y="69" width="7" height="3" rx="1" fill="#ef4444"/>
                <line x1="15" y1="70.5" x2="29" y2="70.5" stroke="#ef4444" stroke-width="1.2"/>
                <rect x="29" y="69" width="7" height="3" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    if (category === 'van') {
        return `
            <svg viewBox="0 0 44 82" class="w-12 h-21 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <defs>
                    <linearGradient id="vanBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                        <stop offset="0%" stop-color="#334155"/>
                        <stop offset="50%" stop-color="#1e293b"/>
                        <stop offset="100%" stop-color="#0f172a"/>
                    </linearGradient>
                </defs>
                <rect x="1" y="20" width="4.5" height="6.5" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <rect x="38.5" y="20" width="4.5" height="6.5" rx="2" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <path d="M 8 12 Q 8 3 22 2 Q 36 3 36 12 L 36 74 Q 36 79 22 79 Q 8 79 8 74 Z" fill="url(#vanBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
                <path d="M 12 4 L 14 12 M 32 4 L 30 12" stroke="#475569" stroke-width="1"/>
                <path d="M 10 14 Q 22 11 34 14 L 33 24 Q 22 22 11 24 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8"/>
                <rect x="11" y="24" width="22" height="46" rx="2" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
                <line x1="16" y1="28" x2="16" y2="66" stroke="#0f172a" stroke-width="1.2"/>
                <line x1="22" y1="28" x2="22" y2="66" stroke="#0f172a" stroke-width="1.2"/>
                <line x1="28" y1="28" x2="28" y2="66" stroke="#0f172a" stroke-width="1.2"/>
                <rect x="12" y="70" width="20" height="5" rx="1.5" fill="#0f172a" stroke="#38bdf8" stroke-width="0.7"/>
                <rect x="9" y="3" width="6" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="29" y="3" width="6" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="8.5" y="72" width="2.5" height="5" rx="1" fill="#ef4444"/>
                <rect x="33" y="72" width="2.5" height="5" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    if (category === 'hatchback') {
        return `
            <svg viewBox="0 0 40 64" class="w-11 h-17 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <defs>
                    <linearGradient id="hbBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                        <stop offset="0%" stop-color="#334155"/>
                        <stop offset="50%" stop-color="#1e293b"/>
                        <stop offset="100%" stop-color="#0f172a"/>
                    </linearGradient>
                </defs>
                <rect x="1.5" y="19" width="3.5" height="5.5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <rect x="35" y="19" width="3.5" height="5.5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <path d="M 7 14 Q 7 5 20 4 Q 33 5 33 14 L 33 54 Q 33 60 20 60 Q 7 60 7 54 Z" fill="url(#hbBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
                <path d="M 9 18 Q 20 15 31 18 L 29 27 Q 20 25 11 27 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8"/>
                <rect x="11" y="27" width="18" height="18" rx="2" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
                <path d="M 10 46 Q 20 44 30 46 L 31 54 Q 20 53 9 54 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.7"/>
                <rect x="12" y="44" width="16" height="2" rx="1" fill="#475569"/>
                <rect x="8" y="5" width="5" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="27" y="5" width="5" height="3" rx="1.5" fill="#38bdf8"/>
                <rect x="7.5" y="54" width="4" height="3" rx="1" fill="#ef4444"/>
                <rect x="28.5" y="54" width="4" height="3" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    if (category === 'motorcycle') {
        return `
            <svg viewBox="0 0 36 68" class="w-10 h-18 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <rect x="15" y="3" width="6" height="12" rx="3" fill="#0f172a" stroke="#475569" stroke-width="1"/>
                <rect x="14" y="6" width="8" height="6" rx="2" fill="${hex}" opacity="0.9"/>
                <line x1="4" y1="18" x2="32" y2="18" stroke="#94a3b8" stroke-width="2.5" stroke-linecap="round"/>
                <circle cx="5" cy="18" r="2.5" fill="#1e293b" stroke="#64748b" stroke-width="1"/>
                <circle cx="31" cy="18" r="2.5" fill="#1e293b" stroke="#64748b" stroke-width="1"/>
                <polygon points="18,12 14,16 22,16" fill="#38bdf8"/>
                <path d="M 13 20 Q 18 19 23 20 L 22 34 Q 18 36 14 34 Z" fill="#1e293b" stroke="${hex}" stroke-width="1.5"/>
                <path d="M 14 34 Q 18 33 22 34 L 21 48 Q 18 49 15 48 Z" fill="#0f172a" stroke="#334155" stroke-width="1"/>
                <rect x="23" y="36" width="3" height="16" rx="1" fill="#94a3b8" stroke="#64748b" stroke-width="0.6"/>
                <rect x="14.5" y="48" width="7" height="14" rx="3.5" fill="#0f172a" stroke="#475569" stroke-width="1"/>
                <rect x="15.5" y="63" width="5" height="2.5" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    if (category === 'coupe') {
        return `
            <svg viewBox="0 0 42 72" class="w-12 h-19 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
                <defs>
                    <linearGradient id="cpBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                        <stop offset="0%" stop-color="#334155"/>
                        <stop offset="50%" stop-color="#1e293b"/>
                        <stop offset="100%" stop-color="#0f172a"/>
                    </linearGradient>
                </defs>
                <rect x="1" y="24" width="4" height="5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <rect x="37" y="24" width="4" height="5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
                <path d="M 10 15 Q 10 5 21 4 Q 32 5 32 15 L 30 38 Q 36 44 36 58 Q 36 67 21 67 Q 6 67 6 58 Q 6 44 12 38 Z" fill="url(#cpBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
                <rect x="14" y="11" width="3" height="7" rx="1" fill="#0f172a" stroke="#475569" stroke-width="0.6"/>
                <rect x="25" y="11" width="3" height="7" rx="1" fill="#0f172a" stroke="#475569" stroke-width="0.6"/>
                <path d="M 11 21 Q 21 18 31 21 L 28 32 Q 21 30 14 32 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8"/>
                <path d="M 14 32 L 28 32 L 26 48 L 16 48 Z" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
                <path d="M 16 48 L 26 48 L 27 57 Q 21 56 15 57 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.7"/>
                <rect x="10" y="66" width="3" height="2" rx="1" fill="#94a3b8"/>
                <rect x="29" y="66" width="3" height="2" rx="1" fill="#94a3b8"/>
                <polygon points="10,6 16,6 14,9 9,9" fill="#38bdf8"/>
                <polygon points="32,6 26,6 28,9 33,9" fill="#38bdf8"/>
                <rect x="8" y="61" width="7" height="2.5" rx="1" fill="#ef4444"/>
                <rect x="27" y="61" width="7" height="2.5" rx="1" fill="#ef4444"/>
            </svg>
        `;
    }

    // Default: Sedan
    return `
        <svg viewBox="0 0 40 70" class="w-12 h-19 filter drop-shadow-[0_8px_14px_rgba(0,0,0,0.7)]" style="display:block;">
            <defs>
                <linearGradient id="sedanBody_${uid}" x1="0" y1="0" x2="1" y2="1">
                    <stop offset="0%" stop-color="#334155"/>
                    <stop offset="50%" stop-color="#1e293b"/>
                    <stop offset="100%" stop-color="#0f172a"/>
                </linearGradient>
            </defs>
            <rect x="1.5" y="21" width="3.5" height="5.5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
            <rect x="35" y="21" width="3.5" height="5.5" rx="1.5" fill="#475569" stroke="#64748b" stroke-width="0.5"/>
            <path d="M 8 14 Q 8 4 20 3 Q 32 4 32 14 L 33 60 Q 33 66 20 66 Q 7 66 7 60 Z" fill="url(#sedanBody_${uid})" stroke="${hex}" stroke-width="1.5"/>
            <path d="M 12 7 L 14 18 M 28 7 L 26 18" stroke="#475569" stroke-width="1"/>
            <path d="M 10 20 Q 20 17 30 20 L 28 30 Q 20 28 12 30 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.8"/>
            <rect x="12" y="30" width="16" height="18" rx="2" fill="#1e293b" stroke="#334155" stroke-width="0.8"/>
            <path d="M 12 48 Q 20 46 28 48 L 29 55 Q 20 54 11 55 Z" fill="#0f172a" stroke="#38bdf8" stroke-width="0.7"/>
            <line x1="11" y1="58" x2="29" y2="58" stroke="#475569" stroke-width="0.8"/>
            <rect x="8.5" y="4" width="5.5" height="3" rx="1.5" fill="#38bdf8"/>
            <rect x="26" y="4" width="5.5" height="3" rx="1.5" fill="#38bdf8"/>
            <rect x="8" y="62" width="5" height="3" rx="1" fill="#ef4444"/>
            <rect x="27" y="62" width="5" height="3" rx="1" fill="#ef4444"/>
        </svg>
    `;
};

// Normalizes camelCase, PascalCase, and snake_case API responses
const formatDateSafe = (val, fallback) => {
    if (!val) return fallback;
    if (typeof val === 'string') {
        return val.includes('T') ? val.split('T')[0] : val;
    }
    try {
        const d = new Date(val);
        return isNaN(d.getTime()) ? fallback : d.toISOString().split('T')[0];
    } catch (e) {
        return fallback;
    }
};

const normVehicle = (v, idx = 0) => {
    let mainPhoto = v.photo_url || v.photoUrl || v.PhotoUrl || v.image || v.Image || '';
    if (typeof mainPhoto === 'string' && mainPhoto.trim().startsWith('[')) {
        try {
            const arr = JSON.parse(mainPhoto);
            if (Array.isArray(arr) && arr.length > 0) mainPhoto = arr[0];
        } catch(e) {}
    }

    let mapIcon = (v.map_icon_url || v.mapIconUrl || v.model_3d_url || v.model3dUrl || v.Model3dUrl || v.Model3DUrl || '').trim();
    if (mapIcon && mapIcon.includes('/uploads/vehicles/')) {
        mapIcon = '';
    }

    const HQ_LAT = 14.871116;
    const HQ_LNG = 121.048088;

    const rawLat = parseFloat(v.latitude || v.Latitude || 0);
    const rawLng = parseFloat(v.longitude || v.Longitude || 0);
    const st = (v.status || v.Status || 'available').toLowerCase();

    let finalLat = rawLat;
    let finalLng = rawLng;

    // Realtime Location Logic:
    // If available OR if GPS coordinates are missing/empty/default, place at HQ Rental Station parking lot grid
    if (st === 'available' || !rawLat || !rawLng || (Math.abs(rawLat - 14.8169) < 0.001 && Math.abs(rawLng - 121.0453) < 0.001)) {
        const row = Math.floor(idx / 5);
        const col = idx % 5;
        finalLat = HQ_LAT + (row * 0.00015 - 0.0003);
        finalLng = HQ_LNG + (col * 0.00018 - 0.00036);
    }

    let rawId = v.vehicle_id || v.vehicleId || v.VehicleId || v.id || v.Id || v.ID || v.Vehicle_Id || v._id;
    if ((rawId === undefined || rawId === null || rawId === '' || isNaN(parseInt(rawId)) || parseInt(rawId) <= 0) && typeof v === 'object' && v !== null) {
        for (const key of Object.keys(v)) {
            if (key.toLowerCase().includes('id') && !key.toLowerCase().includes('url') && !key.toLowerCase().includes('guid')) {
                const val = parseInt(v[key]);
                if (!isNaN(val) && val > 0) {
                    rawId = val;
                    break;
                }
            }
        }
    }
    const parsedId = (rawId !== undefined && rawId !== null && !isNaN(parseInt(rawId))) ? parseInt(rawId) : null;
    const dbId = (parsedId && parsedId > 0) ? parsedId : null;
    return {
        id: dbId,
        vehicle_id: dbId,
        vehicleId: dbId,
        VehicleId: dbId,
        plateNumber: v.plate_no || v.plateNo || v.plateNumber || v.PlateNumber || '',
        brand: v.brand || v.Brand || '',
        model: v.model || v.Model || '',
        type: v.type || v.Type || 'Car',
        year: v.year || v.Year || new Date().getFullYear(),
        status: st,
        dailyRatePHP: parseFloat(v.rate_per_day || v.ratePerDay || v.dailyRatePHP || v.DailyRatePHP || v.dailyRate || 0),
        rateWithDriverPHP: parseFloat(v.rate_with_driver || v.rateWithDriver || v.rateWithDriverPHP || 0),
        engineCc: v.cc || v.EngineCc || v.engineCc || 1500,
        transmission: v.transmission || v.Transmission || 'Automatic',
        fuelType: v.fuelType || v.FuelType || 'Gasoline',
        fuelPercentage: v.fuel_level_pct || v.fuelPercentage || v.FuelLevelPct || 85,
        odometerKm: v.odometer_km || v.odometerKm || v.OdometerKm || 0,
        currentSpeedKmh: v.current_speed || v.currentSpeed || v.speed || v.Speed || 0,
        healthScore: v.health_score || v.healthScore || v.HealthScore || 95,
        rfidBalancePHP: v.rfid_balance_autosweep || v.rfidBalancePHP || 500,
        autosweepTag: v.autosweepTag || 'AUTOSWEEP-' + (v.vehicle_id || v.id || 1),
        easytripTag: v.easytripTag || 'EASYTRIP-' + (v.vehicle_id || v.id || 1),
        image: mainPhoto,
        mapIconUrl: mapIcon,
        orCrUrl: v.or_cr_url || v.orCrUrl || v.OrCrUrl || '',
        insuranceUrl: v.insurance_url || v.insuranceUrl || v.InsuranceUrl || '',
        coordinates: [finalLat, finalLng],
        driverName: v.driverName || v.DriverName || '',
        driverPhone: v.driverPhone || v.DriverPhone || '',
        engineStatus: (v.current_speed || 0) > 0 ? 'RUNNING' : (v.engineStatus || 'OFF').toUpperCase(),
        maintenanceDueKm: v.maintenance_due_km || v.maintenanceDueKm || 5000,
        documents: {
            ltoRegistrationExpiry: formatDateSafe(v.lto_expiry_date || v.ltoExpiryDate || v.documents?.ltoRegistrationExpiry, '2026-10-15'),
            insuranceExpiry: formatDateSafe(v.insurance_expiry_date || v.insuranceExpiryDate || v.documents?.insuranceExpiry, '2026-11-20'),
            orCrUrl: v.or_cr_url || v.orCrUrl || v.OrCrUrl || '',
            insuranceUrl: v.insurance_url || v.insuranceUrl || v.InsuranceUrl || ''
        },
        hardwareOverride: {
            immobilizerActive: v.telematics_locked || v.hardwareOverride?.immobilizerActive || false,
            doorLocked: true,
        }
    };
};


// ─── ICONS (Lucide SVG, No Emojis) ──────────────────────────────────────────
const S = ({sz=16,c="",ch}) => <svg xmlns="http://www.w3.org/2000/svg" width={sz} height={sz} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={c}>{ch}</svg>;
const IconCar = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M19 17H5a2 2 0 0 1-2-2V9l2.7-5.4A2 2 0 0 1 7.5 3h9a2 2 0 0 1 1.8 1.1L21 9v6a2 2 0 0 1-2 2Z"/><circle cx="7" cy="17" r="2"/><circle cx="17" cy="17" r="2"/></>}/>;
const IconTruck = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M10 17h4V5H2v12h3"/><path d="M20 17h2v-9h-4V5h-4v12h3"/><circle cx="8.5" cy="17" r="1.5"/><circle cx="17.5" cy="17" r="1.5"/></>}/>;
const IconVan = ({sz,c}) => <S sz={sz} c={c} ch={<><rect x="1" y="6" width="16" height="11" rx="2"/><path d="M17 8.5l4 2.5v6h-4"/><circle cx="5.5" cy="17.5" r="2.5"/><circle cx="15.5" cy="17.5" r="2.5"/></>}/>;
const IconMotorcycle = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="5.5" cy="17.5" r="3.5"/><circle cx="18.5" cy="17.5" r="3.5"/><path d="M15 6h2.5l2 3.5-3 5.5H9.5L7 9.5 9 6h6z"/></>}/>;
const IconSearch = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></>}/>;
const IconFilter = ({sz,c}) => <S sz={sz} c={c} ch={<><polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/></>}/>;
const IconGrid = ({sz,c}) => <S sz={sz} c={c} ch={<><rect width="7" height="7" x="3" y="3" rx="1"/><rect width="7" height="7" x="14" y="3" rx="1"/><rect width="7" height="7" x="14" y="14" rx="1"/><rect width="7" height="7" x="3" y="14" rx="1"/></>}/>;
const IconList = ({sz,c}) => <S sz={sz} c={c} ch={<><line x1="8" x2="21" y1="6" y2="6"/><line x1="8" x2="21" y1="12" y2="12"/><line x1="8" x2="21" y1="18" y2="18"/><line x1="3" x2="3.01" y1="6" y2="6"/><line x1="3" x2="3.01" y1="12" y2="12"/><line x1="3" x2="3.01" y1="18" y2="18"/></>}/>;
const IconRefreshCw = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></>}/>;
const IconLock = ({sz,c}) => <S sz={sz} c={c} ch={<><rect width="18" height="11" x="3" y="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></>}/>;
const IconUnlock = ({sz,c}) => <S sz={sz} c={c} ch={<><rect width="18" height="11" x="3" y="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/></>}/>;
const IconMapPin = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/></>}/>;
const IconDroplets = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M7 16.3c2.2 0 4-1.83 4-4.05 0-1.16-.57-2.26-1.71-3.19S7 2.9 7 2.9s-2.15 5.16-2.29 6.16C3.57 10 3 11.1 3 12.25c0 2.22 1.8 4.05 4 4.05z"/><path d="M12.56 6.6A10.97 10.97 0 0 0 14 3.02c.5 2.5 2 4.9 4 6.5s3 3.5 3 5.5a6.98 6.98 0 0 1-11.91 4.97"/></>}/>;
const IconGauge = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="m12 14 4-4"/><path d="M3.34 16A10 10 0 1 1 20.66 16"/></>}/>;
const IconShield = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></>}/>;
const IconActivity = ({sz,c}) => <S sz={sz} c={c} ch={<polyline points="22 12 18 12 15 21 9 3 6 12 2 12"/>}/>;
const IconClock = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></>}/>;
const IconX = ({sz,c}) => <S sz={sz} c={c} ch={<><line x1="18" x2="6" y1="6" y2="18"/><line x1="6" x2="18" y1="6" y2="18"/></>}/>;
const IconCheck = ({sz,c}) => <S sz={sz} c={c} ch={<polyline points="20 6 9 17 4 12"/>}/>;
const IconEdit = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z"/></>}/>;
const IconEdit2 = IconEdit;
const IconTrash = ({sz,c}) => <S sz={sz} c={c} ch={<><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" x2="10" y1="11" y2="17"/><line x1="14" x2="14" y1="11" y2="17"/></>}/>;
const IconTrash2 = IconTrash;
const IconUpload = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="17 8 12 3 7 8"/><line x1="12" x2="12" y1="3" y2="15"/></>}/>;
const IconRotateCcw = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/></>}/>;
const IconRotateCw = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M21 12a9 9 0 1 1-9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/></>}/>;
const IconCompass = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="12" cy="12" r="10"/><polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76"/></>}/>;
const IconPlus = ({sz,c}) => <S sz={sz} c={c} ch={<><line x1="12" x2="12" y1="5" y2="19"/><line x1="5" x2="19" y1="12" y2="12"/></>}/>;
const IconAlertTriangle = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><line x1="12" x2="12" y1="9" y2="13"/><line x1="12" x2="12.01" y1="17" y2="17"/></>}/>;
const IconCreditCard = ({sz,c}) => <S sz={sz} c={c} ch={<><rect width="20" height="14" x="2" y="5" rx="2"/><line x1="2" x2="22" y1="10" y2="10"/></>}/>;
const IconSettings = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z"/><circle cx="12" cy="12" r="3"/></>}/>;
const IconFileText = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z"/><polyline points="14 2 14 8 20 8"/><line x1="16" x2="8" y1="13" y2="13"/><line x1="16" x2="8" y1="17" y2="17"/><line x1="10" x2="8" y1="9" y2="9"/></>}/>;
const IconPhone = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/></>}/>;
const IconPower = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M18.36 6.64a9 9 0 1 1-12.73 0"/><line x1="12" x2="12" y1="2" y2="12"/></>}/>;
const IconQrCode = ({sz,c}) => <S sz={sz} c={c} ch={<><rect width="5" height="5" x="3" y="3" rx="1"/><rect width="5" height="5" x="16" y="3" rx="1"/><rect width="5" height="5" x="3" y="16" rx="1"/><path d="M21 16h-3a2 2 0 0 0-2 2v3"/><path d="M21 21v.01"/><path d="M12 7v3a2 2 0 0 1-2 2H7"/><path d="M3 12h.01"/><path d="M12 3h.01"/><path d="M12 16v.01"/><path d="M16 12h1"/><path d="M21 12v.01"/><path d="M12 21v-1"/></>}/>;
const IconBell = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/></>}/>;
const IconCloudRain = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M4 14.899A7 7 0 1 1 15.71 8h1.79a4.5 4.5 0 0 1 2.5 8.242"/><path d="M16 14v6"/><path d="M8 14v6"/><path d="M12 16v6"/></>}/>;
const IconInfo = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/></>}/>;
const IconMoon = ({sz,c}) => <S sz={sz} c={c} ch={<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>}/>;
const IconSun = ({sz,c}) => <S sz={sz} c={c} ch={<><circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/></>}/>;
const IconWifi = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M5 12.55a11 11 0 0 1 14.08 0"/><path d="M1.42 9a16 16 0 0 1 21.16 0"/><path d="M8.53 16.11a6 6 0 0 1 6.95 0"/><line x1="12" y1="20" x2="12.01" y2="20"/></>}/>;
const IconChevronDown = ({sz,c}) => <S sz={sz} c={c} ch={<path d="m6 9 6 6 6-6"/>}/>;
const IconZap = ({sz,c}) => <S sz={sz} c={c} ch={<polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/>}/>;
const IconFolder = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 8 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2z"/></>}/>;
const IconPrinter = ({sz,c}) => <S sz={sz} c={c} ch={<><polyline points="6 9 6 2 18 2 18 9"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect width="12" height="8" x="6" y="14"/></>}/>;
const IconEye = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/></>}/>;
const IconDownload = ({sz,c}) => <S sz={sz} c={c} ch={<><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" x2="12" y1="15" y2="3"/></>}/>;

const getTypeIcon = (type) => {
    switch((type || '').toLowerCase()){
        case 'suv': return IconCar;
        case 'sedan': return IconCar;
        case 'van': return IconVan;
        case 'truck': return IconTruck;
        case 'pickup': return IconTruck;
        case 'motorcycle': return IconMotorcycle;
        default: return IconCar;
    }
};

// ─── COMPONENTS ─────────────────────────────────────────────────────────────

const StatCard = ({ title, value, icon: Icon, colorClass, subtitle }) => (
    <div className={`p-4 rounded-2xl bg-[var(--bg-card)] border border-[var(--border-color)] hover:border-[var(--border-highlight)] transition-colors shadow-[var(--shadow-card)]`}>
        <div className="flex items-center justify-between mb-2">
            <div className="text-[var(--text-secondary)] font-medium text-sm">{title}</div>
            <div className={`p-2 rounded-lg bg-opacity-10 ${colorClass.replace('text-', 'bg-')} ${colorClass}`}>
                <Icon sz={18} />
            </div>
        </div>
        <div className="text-2xl font-bold text-[var(--text-primary)]">{value}</div>
        {subtitle && <div className="text-xs text-[var(--text-muted)] mt-1">{subtitle}</div>}
    </div>
);



const WeatherBanner = ({ show, onHide }) => {
    if (!show) return null;
    return (
        <div className="mx-6 mt-4 p-3 rounded-xl bg-blue-500/10 border border-blue-500/30 flex items-center justify-between animate-fade-in">
            <div className="flex items-center gap-3 text-blue-400">
                <IconCloudRain sz={20} />
                <div>
                    <div className="font-semibold text-sm">WEATHER ADVISORY: Flood Risk Alert</div>
                    <div className="text-xs opacity-80">Heavy rainfall expected in Metro Manila. Low-lying zones flagged.</div>
                </div>
            </div>
            <button onClick={onHide} className="text-blue-400 hover:text-blue-300 p-1"><IconX sz={16} /></button>
        </div>
    );
};

const VehicleImageComponent = ({ src, alt, className, iconSize = 48 }) => {
    const [hasError, setHasError] = useState(false);

    useEffect(() => {
        setHasError(false);
    }, [src]);

    if (!src || hasError) {
        return (
            <div className="w-full h-full flex items-center justify-center text-[var(--text-muted)] opacity-30">
                <IconCar sz={iconSize} />
            </div>
        );
    }

    return (
        <img
            key={src}
            src={src}
            alt={alt || 'Vehicle Photo'}
            className={className || "w-full h-full object-cover"}
            onError={() => setHasError(true)}
        />
    );
};

const StatusCombobox = ({ value, onChange }) => {
    const [open, setOpen] = React.useState(false);
    const containerRef = React.useRef(null);

    const options = [
        { key: 'all', label: 'All Statuses' },
        { key: 'available', label: 'Available' },
        { key: 'rented', label: 'Rented' },
        { key: 'maintenance', label: 'Maintenance' },
    ];

    const currentOpt = options.find(o => o.key === value) || options[0];

    React.useEffect(() => {
        const handleClickOutside = (e) => {
            if (containerRef.current && !containerRef.current.contains(e.target)) {
                setOpen(false);
            }
        };
        document.addEventListener('mousedown', handleClickOutside);
        return () => document.removeEventListener('mousedown', handleClickOutside);
    }, []);

    return (
        <div className="relative shrink-0" ref={containerRef}>
            <button
                type="button"
                onClick={() => setOpen(!open)}
                className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition-all duration-200 border bg-[var(--bg-tertiary)] ${
                    open 
                    ? 'border-orange-500 text-orange-500 shadow-[0_0_15px_rgba(249,115,22,0.3)] ring-2 ring-orange-500/20' 
                    : 'border-[var(--border-color)] text-[var(--text-primary)] hover:border-orange-500/60 hover:text-orange-400'
                }`}
            >
                <span className={`w-2 h-2 rounded-full ${value === 'available' ? 'bg-emerald-400 animate-pulse' : value === 'rented' ? 'bg-amber-400 animate-pulse' : value === 'maintenance' ? 'bg-orange-400' : 'bg-orange-500'}`}></span>
                <span>{currentOpt.label}</span>
                <IconChevronDown sz={14} className={`transition-transform duration-200 ${open ? 'rotate-180 text-orange-500' : 'text-[var(--text-muted)]'}`} />
            </button>

            {open && (
                <div className="absolute right-0 mt-2 w-48 rounded-2xl bg-[var(--bg-card)] border border-orange-500/40 shadow-[0_12px_32px_rgba(0,0,0,0.6)] py-1.5 z-50 backdrop-blur-xl animate-in fade-in zoom-in-95 duration-150">
                    <div className="px-3 py-1.5 text-[10px] font-extrabold uppercase tracking-wider text-orange-500 border-b border-[var(--border-color)] mb-1">
                        Select Status
                    </div>
                    {options.map(opt => (
                        <button
                            key={opt.key}
                            onClick={() => {
                                onChange(opt.key);
                                setOpen(false);
                            }}
                            className={`w-full px-3.5 py-2 text-xs font-bold flex items-center justify-between transition-colors ${
                                value === opt.key 
                                ? 'bg-orange-500/15 text-orange-400 font-extrabold' 
                                : 'text-[var(--text-primary)] hover:bg-orange-500/10 hover:text-orange-400'
                            }`}
                        >
                            <span className="flex items-center gap-2">
                                <span className={`w-2 h-2 rounded-full ${opt.key === 'available' ? 'bg-emerald-400' : opt.key === 'rented' ? 'bg-amber-400' : opt.key === 'maintenance' ? 'bg-orange-400' : 'bg-orange-500'}`}></span>
                                {opt.label}
                            </span>
                            {value === opt.key && <IconCheck sz={14} className="text-orange-500" />}
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
};

// ─── 3D KPI METRIC CARD (Ported from DashboardOverview) ─────────────────────
const FleetKpiCard3D = ({ icon, title, value, tag, tagClass, glowColor, borderHoverColor, valueColor, onClick }) => {
    const cardRef = useRef(null);
    const rafRef = useRef(null);
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
            setTilt({ x: normY * -12, y: normX * 12 });
            setGlare({
                x: (relX / rect.width) * 100,
                y: (relY / rect.height) * 100,
                opacity: 0.28
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
            onClick={onClick}
            style={{
                perspective: '1000px',
                transformStyle: 'preserve-3d',
                transform: hovered
                    ? `perspective(1000px) rotateX(${tilt.x}deg) rotateY(${tilt.y}deg) scale(1.03) translateZ(8px)`
                    : 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale(1) translateZ(0px)',
                transition: hovered
                    ? 'transform 0.1s ease-out, box-shadow 0.25s ease, border-color 0.2s ease, background 0.2s ease'
                    : 'transform 0.5s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.35s ease, border-color 0.3s ease, background 0.3s ease',
                boxShadow: hovered ? `0 18px 36px ${glowColor || 'rgba(249, 115, 22, 0.25)'}, 0 0 0 1px rgba(255,255,255,0.08)` : 'var(--shadow-card)',
                willChange: 'transform',
                position: 'relative',
                overflow: 'hidden'
            }}
            className={`p-4 rounded-2xl bg-[var(--bg-card)] border border-[var(--border-color)] flex flex-col justify-between h-28 cursor-pointer group select-none ${hovered ? borderHoverColor : ''}`}
        >
            {/* Top Shine */}
            <div
                style={{
                    position: 'absolute', top: 0, left: 0, right: 0, height: '45%',
                    borderRadius: '16px 16px 0 0',
                    background: 'linear-gradient(180deg, rgba(255,255,255,0.06) 0%, transparent 100%)',
                    pointerEvents: 'none', zIndex: 1
                }}
            />
            {/* Radial Glare */}
            <div
                style={{
                    position: 'absolute', inset: 0, borderRadius: '16px', pointerEvents: 'none',
                    background: `radial-gradient(circle at ${glare.x}% ${glare.y}%, rgba(255,255,255,${glare.opacity}) 0%, rgba(255,255,255,0) 65%)`,
                    transition: hovered ? 'none' : 'opacity 0.4s ease',
                    zIndex: 2
                }}
            />
            {/* Content */}
            <div className="relative z-10 flex justify-between items-center">
                <div className="w-9 h-9 rounded-xl bg-orange-500/15 text-orange-500 flex items-center justify-center group-hover:scale-110 transition-transform">
                    {icon}
                </div>
                <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${tagClass}`}>{tag}</span>
            </div>
            <div className="relative z-10">
                <div className={`text-2xl font-black tracking-tight ${valueColor || 'text-[var(--text-primary)]'}`}>{value}</div>
                <div className="text-[10px] text-[var(--text-muted)] font-bold uppercase tracking-wider">{title}</div>
            </div>
        </div>
    );
};

const VehicleCard = ({ v, onClick, onMapFocus }) => {
    const cardRef = useRef(null);
    const rafRef = useRef(null);
    const [tilt, setTilt] = useState({ x: 0, y: 0 });
    const [glare, setGlare] = useState({ x: 50, y: 50, opacity: 0 });
    const [hovered, setHovered] = useState(false);

    const st = getStatusStyle(v.status);
    const isRented = (v.status || '').toLowerCase() === 'rented';

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
            setTilt({ x: normY * -8, y: normX * 8 });
            setGlare({
                x: (relX / rect.width) * 100,
                y: (relY / rect.height) * 100,
                opacity: 0.22
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
                    ? `perspective(1000px) rotateX(${tilt.x}deg) rotateY(${tilt.y}deg) scale(1.02) translateZ(6px)`
                    : 'perspective(1000px) rotateX(0deg) rotateY(0deg) scale(1) translateZ(0px)',
                transition: hovered
                    ? 'transform 0.1s ease-out, box-shadow 0.25s ease, border-color 0.2s ease, background 0.2s ease'
                    : 'transform 0.5s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.35s ease, border-color 0.3s ease, background 0.3s ease',
                boxShadow: hovered ? '0 20px 40px rgba(249, 115, 22, 0.22), 0 0 0 1px rgba(255,255,255,0.08)' : 'var(--shadow-card)',
                willChange: 'transform'
            }}
            className={`group flex flex-col rounded-3xl bg-[var(--bg-card)] border border-[var(--border-color)] hover:border-orange-500/60 cursor-pointer overflow-hidden relative justify-between p-0 pb-5.5 min-h-[360px]`}
            onClick={() => onClick(v)}
        >
            {/* Top Shine */}
            <div
                style={{
                    position: 'absolute', top: 0, left: 0, right: 0, height: '35%',
                    borderRadius: '24px 24px 0 0',
                    background: 'linear-gradient(180deg, rgba(255,255,255,0.07) 0%, transparent 100%)',
                    pointerEvents: 'none', zIndex: 3
                }}
            />
            {/* Radial Glare */}
            <div
                style={{
                    position: 'absolute', inset: 0, borderRadius: '24px', pointerEvents: 'none',
                    background: `radial-gradient(circle at ${glare.x}% ${glare.y}%, rgba(255,255,255,${glare.opacity}) 0%, rgba(255,255,255,0) 65%)`,
                    transition: hovered ? 'none' : 'opacity 0.4s ease',
                    zIndex: 4
                }}
            />
            {/* Ambient Soft Glow Overlay on Hover */}
            <div className="absolute -inset-1 rounded-3xl bg-gradient-to-r from-orange-500/10 via-amber-500/10 to-orange-500/10 opacity-0 group-hover:opacity-100 blur-xl transition-opacity duration-500 pointer-events-none"></div>

            {/* Top Section: FULL WIDTH Image Container (Edge-to-Edge with Card Borders) */}
            <div className="h-48 w-full relative overflow-hidden shrink-0 border-b border-[var(--border-color)] group-hover:border-orange-500/30 transition-colors">
                <VehicleImageComponent src={v.image} alt={v.plateNumber} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700 ease-out" iconSize={56} />
                
                <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent pointer-events-none"></div>
                
                {/* Status Badge */}
                <div className={`absolute top-3.5 right-3.5 px-3.5 py-1.5 rounded-full text-[11px] font-black uppercase tracking-wider flex items-center gap-1.5 backdrop-blur-md border border-white/20 shadow-lg ${st.bg} ${st.text}`}>
                    <span className={`w-2 h-2 rounded-full ${st.hex === '#34d399' ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'}`}></span>
                    {v.status}
                </div>

                {v.rfidBalancePHP < 200 && (
                    <div className="absolute top-3.5 left-3.5 px-2.5 py-1 rounded-full bg-red-500/90 text-white backdrop-blur shadow-lg text-[10px] font-bold flex items-center gap-1">
                        <IconAlertTriangle sz={12} /> Low RFID
                    </div>
                )}

                {/* Bottom Overlay inside Image Container: Type & Spec Badges */}
                <div className="absolute bottom-3.5 left-3.5 right-3.5 flex justify-between items-end pointer-events-none">
                    <span className="text-[10px] font-extrabold uppercase tracking-widest text-orange-400 bg-black/60 backdrop-blur px-2.5 py-0.5 rounded-md border border-white/10">
                        {v.type || 'Car'}
                    </span>
                    <span className="text-[10px] text-gray-300 font-semibold bg-black/60 backdrop-blur px-2 py-0.5 rounded-md border border-white/10">
                        {v.transmission} • {v.engineCc}cc
                    </span>
                </div>
            </div>
            
            {/* Body Section with 1.5cm Side Margins (px-6 Inset) */}
            <div className="pt-5 px-6 flex flex-col justify-between flex-1 gap-4 relative z-10">
                {/* Row 1: Brand & Model + Plate Number & Price */}
                <div className="flex flex-col gap-2">
                    <div className="text-xs font-extrabold text-orange-400 uppercase tracking-widest truncate">
                        {v.brand} {v.model}
                    </div>
                    
                    <div className="flex justify-between items-baseline gap-2 mb-2">
                        <div className="text-2xl font-black text-[var(--text-primary)] tracking-tight whitespace-nowrap overflow-hidden text-ellipsis drop-shadow">
                            {v.plateNumber}
                        </div>
                        <div className="text-right shrink-0">
                            <span className="text-base font-black text-emerald-400">₱{parseInt(v.dailyRatePHP || 0).toLocaleString()}</span>
                            <span className="text-[10px] text-[var(--text-muted)] font-medium">/day</span>
                        </div>
                    </div>
                </div>

                {/* Row 2: Symmetrical Dashboard-Style Stat Cards (Odometer & Health Score) */}
                <div className="grid grid-cols-2 gap-3 pt-4 border-t border-[var(--border-color)] shrink-0">
                    {/* Odometer Box with Live GPS Telematics Badge */}
                    <div className={`p-2.5 px-3 rounded-xl border transition-all duration-500 flex flex-col justify-center min-h-[52px] ${isRented ? 'border-emerald-500/50 bg-emerald-500/10 shadow-[0_0_12px_rgba(16,185,129,0.15)]' : 'bg-[var(--bg-tertiary)] border-[var(--border-color)]'}`}>
                        <div className="text-[9px] font-bold text-[var(--text-muted)] uppercase tracking-wider flex items-center justify-between gap-1">
                            <span className="flex items-center gap-1 shrink-0">
                                <IconGauge sz={11} className={isRented ? "text-emerald-400 shrink-0" : "text-orange-500 shrink-0"}/> Odometer
                            </span>
                            {isRented && (
                                <span className="flex items-center gap-1 text-[7.5px] text-emerald-400 font-extrabold tracking-tight bg-emerald-500/20 px-1.5 py-0.5 rounded-full border border-emerald-500/30 shrink-0 whitespace-nowrap">
                                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping"></span> LIVE
                                </span>
                            )}
                        </div>
                        <div className={`font-black text-xs mt-0.5 whitespace-nowrap truncate transition-all duration-300 ${isRented ? 'text-emerald-400 animate-pulse' : 'text-[var(--text-primary)]'}`}>
                            {parseInt(v.odometerKm || 0).toLocaleString()} <span className="text-[9px] text-[var(--text-muted)] font-normal">km</span>
                        </div>
                    </div>

                    {/* Health Score Box */}
                    <div className="bg-[var(--bg-tertiary)] p-2.5 px-3 rounded-xl border border-[var(--border-color)] flex flex-col justify-center min-h-[50px]">
                        <div className="text-[9px] font-bold text-[var(--text-muted)] uppercase tracking-wider flex items-center gap-1">
                            <IconShield sz={11} className="text-emerald-400 shrink-0"/> Health Score
                        </div>
                        <div className={`font-black text-xs mt-0.5 whitespace-nowrap ${v.healthScore > 80 ? 'text-emerald-400' : v.healthScore > 50 ? 'text-amber-400' : 'text-red-400'}`}>
                            {v.healthScore}/100
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

const VehicleTable = ({ vehicles, onSelect }) => (
    <div className="w-full overflow-x-auto bg-[var(--bg-card)] rounded-2xl border border-[var(--border-color)] shadow-[var(--shadow-card)]">
        <table className="w-full text-left text-sm border-collapse">
            <thead>
                <tr className="border-b border-[var(--border-color)] text-[var(--text-muted)] uppercase text-[10px] tracking-wider">
                    <th className="p-4 font-medium">Vehicle</th>
                    <th className="p-4 font-medium">Status</th>
                    <th className="p-4 font-medium">Fuel / Battery</th>
                    <th className="p-4 font-medium">Odometer</th>
                    <th className="p-4 font-medium">Health</th>
                    <th className="p-4 font-medium">RFID Balance</th>
                    <th className="p-4 font-medium text-right">Rate</th>
                </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border-color)]">
                {vehicles.map(v => {
                    const st = getStatusStyle(v.status);
                    return (
                        <tr key={v.id} onClick={() => onSelect(v)} className="hover:bg-[var(--border-highlight)] cursor-pointer transition-colors group">
                            <td className="p-4 flex items-center gap-3">
                                <div className="w-10 h-10 rounded-lg bg-[var(--bg-tertiary)] overflow-hidden shrink-0 border border-[var(--border-color)]">
                                    <VehicleImageComponent src={v.image} alt={v.plateNumber} className="w-full h-full object-cover" iconSize={20} />
                                </div>
                                <div>
                                    <div className="font-bold text-[var(--text-primary)]">{v.plateNumber}</div>
                                    <div className="text-xs text-[var(--text-muted)]">{v.brand} {v.model}</div>
                                </div>
                            </td>
                            <td className="p-4">
                                <span className={`px-2 py-1 rounded text-[10px] font-bold ${st.bg} ${st.text}`}>{v.status}</span>
                            </td>
                            <td className="p-4">
                                <div className="flex items-center gap-2">
                                    <div className="w-16 h-1.5 bg-[var(--border-color)] rounded-full overflow-hidden">
                                        <div className={`h-full rounded-full ${v.fuelPercentage > 50 ? 'bg-emerald-500' : v.fuelPercentage > 20 ? 'bg-amber-500' : 'bg-red-500'}`} style={{width: `${v.fuelPercentage}%`}}></div>
                                    </div>
                                    <span className="text-xs font-medium text-[var(--text-secondary)]">{v.fuelPercentage}%</span>
                                </div>
                            </td>
                            <td className="p-4 text-[var(--text-secondary)]">
                                {(v.odometerKm/1000).toFixed(1)}k km
                            </td>
                            <td className="p-4">
                                <span className={`font-medium ${v.healthScore > 80 ? 'text-emerald-500' : v.healthScore > 50 ? 'text-amber-500' : 'text-red-500'}`}>{v.healthScore}%</span>
                            </td>
                            <td className="p-4">
                                <span className={`font-medium ${v.rfidBalancePHP < 200 ? 'text-red-500 flex items-center gap-1' : 'text-[var(--text-secondary)]'}`}>
                                    {v.rfidBalancePHP < 200 && <IconAlertTriangle sz={12}/>}
                                    {formatPHP(v.rfidBalancePHP)}
                                </span>
                            </td>
                            <td className="p-4 text-right font-semibold text-[var(--text-primary)]">
                                {formatPHP(v.dailyRatePHP)}
                            </td>
                        </tr>
                    );
                })}
                {vehicles.length === 0 && (
                    <tr><td colSpan="7" className="p-8 text-center text-[var(--text-muted)]">No vehicles found.</td></tr>
                )}
            </tbody>
        </table>
    </div>
);

// ─── LEAFLET MAP PORTED FROM FleetMap.html ──────────────────────────────────
const LeafletMapComponent = ({ vehicles, selectedVehicleId, onSelectVehicle, isDark }) => {
    const mapRef = useRef(null);
    const mapInst = useRef(null);
    const markersRef = useRef({});
    const animFramesRef = useRef({});
    const layersRef = useRef({});
    
    const [mapType, setMapType] = useState('street');
    const [trafficOn, setTrafficOn] = useState(false);
    
    const garageMarkerRef = useRef(null);

    useEffect(() => {
        if (!mapRef.current || mapInst.current) return;
        
        // Init Map centered at Rental Station (14.871116, 121.048088)
        const map = L.map(mapRef.current, { zoomControl: false, attributionControl: false }).setView([14.871116, 121.048088], 14);
        L.control.zoom({ position: 'topright' }).addTo(map);
        mapInst.current = map;

        // Init Layers
        layersRef.current = {
            dark: L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=en&x={x}&y={y}&z={z}', { maxZoom: 22 }),
            light: L.tileLayer('https://mt1.google.com/vt/lyrs=m&hl=en&x={x}&y={y}&z={z}', { maxZoom: 22 }),
            sat: L.tileLayer('https://mt1.google.com/vt/lyrs=y&hl=en&x={x}&y={y}&z={z}', { maxZoom: 22 }),
            traffic: L.tileLayer('https://mt1.google.com/vt/lyrs=h,traffic&x={x}&y={y}&z={z}', { maxZoom: 22 })
        };
        
        layersRef.current.dark.addTo(map); // default

        // Add 3D Garage Hub Marker (using garage_3D.png from WebAssets)
        const garageIcon = L.divIcon({
            className: '',
            html: `<div id='garage-hub-container' class='flex flex-col items-center group cursor-pointer drop-shadow-[0_12px_24px_rgba(249,115,22,0.7)] hover:scale-105 transition-all duration-300 origin-bottom'>
                     <div class='w-24 h-24 relative flex items-center justify-center bg-transparent p-0 overflow-visible'>
                         <img src='garage_3D.png' class='w-full h-full object-contain filter drop-shadow-[0_10px_20px_rgba(249,115,22,0.8)]' alt='Garage Hub'/>
                     </div>
                     <div id='garage-hub-badge' class='mt-1 bg-orange-500/90 backdrop-blur-md text-white text-[10px] px-3 py-1 rounded-full font-black shadow-lg tracking-widest uppercase border border-white/30 flex items-center gap-1.5 whitespace-nowrap'>
                         <svg class='w-3 h-3 text-white' fill='none' stroke='currentColor' viewBox='0 0 24 24'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2.5' d='M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5m0 0h4m-4 0V11m0 0V5'/></svg>
                         RENTAL GARAGE HUB
                     </div>
                   </div>`,
            iconSize: [96, 110],
            iconAnchor: [48, 95]
        });
        const hqMarker = L.marker([14.871116, 121.048088], { icon: garageIcon, zIndexOffset: 1000 }).addTo(map);
        garageMarkerRef.current = hqMarker;

        // Dynamic zoom scaling handler to keep Garage Hub icon proportional when zooming out
        const updateGarageScale = () => {
            const z = map.getZoom();
            const el = document.getElementById('garage-hub-container');
            if (!el) return;
            if (z <= 7) {
                el.style.transform = 'scale(0.35)';
            } else if (z <= 9) {
                el.style.transform = 'scale(0.48)';
            } else if (z <= 11) {
                el.style.transform = 'scale(0.68)';
            } else if (z <= 12) {
                el.style.transform = 'scale(0.85)';
            } else {
                el.style.transform = 'scale(1.0)';
            }
        };

        map.on('zoom', updateGarageScale);
        map.on('zoomend', updateGarageScale);
        setTimeout(updateGarageScale, 100);

        return () => { map.remove(); mapInst.current = null; };
    }, []);

    // Handle Theme & Layer switching
    useEffect(() => {
        if (!mapInst.current) return;
        const m = mapInst.current;
        const l = layersRef.current;
        
        m.removeLayer(l.dark);
        m.removeLayer(l.light);
        m.removeLayer(l.sat);
        
        if (mapType === 'sat') {
            l.sat.addTo(m);
        } else {
            (isDark ? l.dark : l.light).addTo(m);
        }
        
        if (trafficOn) {
            l.traffic.addTo(m);
            l.traffic.bringToFront();
        } else {
            m.removeLayer(l.traffic);
        }
    }, [mapType, trafficOn, isDark]);

    // Handle Markers Update (Only show vehicles that left the garage on active trips!)
    useEffect(() => {
        if (!mapInst.current) return;
        const m = mapInst.current;
        
        const availVehicles = vehicles.filter(v => (v.status || '').toLowerCase() === 'available');
        const activeVehicles = vehicles.filter(v => (v.status || '').toLowerCase() !== 'available');

        // Update Garage Marker Popup & Badge with available count inside garage
        if (garageMarkerRef.current) {
            garageMarkerRef.current.bindPopup(`
                <div class="p-3.5 bg-[#0d0e17]/95 backdrop-blur-xl text-white rounded-2xl font-sans border border-orange-500/40 shadow-2xl min-w-[210px]">
                    <div class="text-xs font-extrabold text-orange-400 uppercase tracking-wider flex items-center gap-1.5 mb-1">
                        <svg class="w-3.5 h-3.5 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5m0 0h4m-4 0V11m0 0V5"/></svg>
                        Drive&Go Garage Hub
                    </div>
                    <div class="text-base font-black text-white">Central Rental Station</div>
                    <div class="mt-2.5 p-2 rounded-xl bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-between text-xs font-bold text-emerald-400">
                        <span>Vehicles Inside Garage:</span>
                        <span class="text-sm font-extrabold text-emerald-300">${availVehicles.length} units</span>
                    </div>
                    <div class="text-[10px] text-gray-400 mt-2 text-center font-medium flex items-center justify-center gap-1">
                        <svg class="w-3 h-3 text-orange-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
                        Coords: 14.871116, 121.048088
                    </div>
                </div>
            `);
        }

        const activeIds = new Set(activeVehicles.map(v => String(v.id)));
        
        // Remove markers for vehicles parked inside garage or removed
        Object.keys(markersRef.current).forEach(id => {
            if (!activeIds.has(String(id))) {
                if (animFramesRef.current[id]) {
                    cancelAnimationFrame(animFramesRef.current[id]);
                    delete animFramesRef.current[id];
                }
                m.removeLayer(markersRef.current[id]);
                delete markersRef.current[id];
            }
        });

        // Add/Update markers only for active vehicles out on the road
        activeVehicles.forEach(v => {
            const hex = statusColorMap[v.status.toLowerCase()]?.hex || '#60a5fa';
            const isSelected = v.id === selectedVehicleId;
            
            // Strictly use 3D Map Marker Icon only (NEVER use media/card photo)
            const rawIcon = (v.map3DIconUrl || v.mapIconUrl || v.map_icon_url || v.model_3d_url || v.model3dUrl || '').trim();
            const isCardPhoto = Boolean(rawIcon && (rawIcon.includes('/uploads/vehicles/') || rawIcon === (v.image || '').trim()));
            const hasCustomIcon = Boolean(
                rawIcon && 
                !isCardPhoto &&
                rawIcon !== 'null' && 
                rawIcon !== 'undefined' && 
                rawIcon.length > 10 && 
                (rawIcon.startsWith('data:image/') || rawIcon.startsWith('http://') || rawIcon.startsWith('https://') || rawIcon.startsWith('/uploads/3d/'))
            );
            const iconImage = hasCustomIcon ? rawIcon : '';
            const headingDeg = v.heading || v.bearing || v.direction || 0;

            const iconHtml = `
                <div class='relative flex flex-col items-center cursor-pointer group bg-transparent' style='perspective: 600px;'>
                    ${isSelected ? `<div class='absolute -inset-3 rounded-full border-2 animate-ping opacity-60 z-0' style='border-color: ${hex}'></div>` : ''}
                    
                    <!-- Forward Headlight Beam Cone (Google Maps Style Front Navigation Light) -->
                    <div class='absolute -top-7 left-1/2 -translate-x-1/2 pointer-events-none z-0' style='transform: rotate(${headingDeg}deg); transform-origin: center 48px; transition: transform 0.6s cubic-bezier(0.4, 0, 0.2, 1);'>
                        <div style='width: 32px; height: 40px; background: radial-gradient(ellipse at bottom, rgba(56, 189, 248, 0.45) 0%, rgba(56, 189, 248, 0.12) 55%, transparent 85%); clip-path: polygon(25% 100%, 75% 100%, 100% 0%, 0% 0%);'></div>
                    </div>

                    <!-- Isometric 3D Floor Shadow -->
                    <div class='absolute bottom-3 w-12 h-5 rounded-[100%] bg-black/40 blur-[3px] transform rotateX(65deg) scale-110 z-0 shadow-2xl border border-black/20 pointer-events-none'></div>
                    
                    <!-- Isometric 3D Vehicle Icon Wrapper with Smooth Rotation -->
                    <div class='relative z-10 bg-transparent transform ${isSelected ? 'scale-125 -translate-y-2' : 'group-hover:scale-110 group-hover:-translate-y-1'}' style='transition: transform 0.3s ease;'>
                        ${hasCustomIcon ? `
                            <div class='bg-transparent flex items-center justify-center' style='transform-style: preserve-3d; filter: drop-shadow(0px 10px 10px rgba(0,0,0,0.6));'>
                                <img src='${iconImage}' class='w-14 h-14 object-contain bg-transparent filter drop-shadow-[0_8px_12px_rgba(0,0,0,0.7)]' style='transform: rotate(${headingDeg}deg); transform-origin: center center; transition: transform 0.6s cubic-bezier(0.4, 0, 0.2, 1);' onerror="this.onerror=null; this.parentElement.innerHTML=\`${renderDefaultTopDownVehicleSvg(v, hex).replace(/\n/g, '').replace(/"/g, "'")}\`;"/>
                            </div>
                        ` : `
                            <div class='bg-transparent flex items-center justify-center' style='transform-style: preserve-3d; transform: rotate(${headingDeg}deg); transform-origin: center center; transition: transform 0.6s cubic-bezier(0.4, 0, 0.2, 1);'>
                                ${renderDefaultTopDownVehicleSvg(v, hex)}
                            </div>
                        `}
                    </div>
                    
                    <!-- Plate Label -->
                    <div class='mt-1 bg-[#0d0e17]/90 backdrop-blur-md border border-white/20 text-white px-2.5 py-0.5 rounded-full text-[9px] font-extrabold shadow-lg whitespace-nowrap z-10 tracking-wider flex items-center gap-1 border-b-2' style='border-bottom-color: ${hex}'>
                        <span class='w-1.5 h-1.5 rounded-full' style='background-color: ${hex}'></span>
                        ${v.plateNumber || ''}
                    </div>
                </div>
            `;

            const icon = L.divIcon({ className: '', html: iconHtml, iconSize: [50, 70], iconAnchor: [25, 35] });
            
            if (markersRef.current[v.id]) {
                const existingMarker = markersRef.current[v.id];
                existingMarker.setIcon(icon);

                // Smooth programmatic GPS coordinate interpolation over 1.5s
                const curLatLng = existingMarker.getLatLng();
                const targetLat = v.coordinates[0];
                const targetLng = v.coordinates[1];
                const dLat = Math.abs(curLatLng.lat - targetLat);
                const dLng = Math.abs(curLatLng.lng - targetLng);

                if (dLat > 0.000005 || dLng > 0.000005) {
                    if (animFramesRef.current[v.id]) {
                        cancelAnimationFrame(animFramesRef.current[v.id]);
                    }
                    const startLat = curLatLng.lat;
                    const startLng = curLatLng.lng;
                    const startTime = performance.now();
                    const duration = 1500;

                    const step = (now) => {
                        const elapsed = now - startTime;
                        const progress = Math.min(1, elapsed / duration);
                        const curL = startLat + (targetLat - startLat) * progress;
                        const curG = startLng + (targetLng - startLng) * progress;
                        existingMarker.setLatLng([curL, curG]);

                        if (progress < 1) {
                            animFramesRef.current[v.id] = requestAnimationFrame(step);
                        } else {
                            delete animFramesRef.current[v.id];
                        }
                    };
                    animFramesRef.current[v.id] = requestAnimationFrame(step);
                }
            } else {
                const marker = L.marker(v.coordinates, { icon }).addTo(m);
                marker.on('click', () => onSelectVehicle(v));
                markersRef.current[v.id] = marker;
            }
        });

        return () => {
            Object.keys(animFramesRef.current).forEach(id => {
                cancelAnimationFrame(animFramesRef.current[id]);
            });
            animFramesRef.current = {};
        };
    }, [vehicles, selectedVehicleId]);

    // Focus selected vehicle
    useEffect(() => {
        if (selectedVehicleId && mapInst.current && markersRef.current[selectedVehicleId]) {
            const latLng = markersRef.current[selectedVehicleId].getLatLng();
            mapInst.current.flyTo(latLng, 16, { duration: 1 });
        }
    }, [selectedVehicleId]);

    return (
        <div className="relative w-full h-full rounded-2xl overflow-hidden border border-[var(--border-color)] shadow-[var(--shadow-card)]">
            <div className="absolute top-4 left-4 z-[400] flex gap-2">
                <button onClick={() => setMapType('street')} className={`px-3 py-1.5 rounded-lg text-xs font-semibold backdrop-blur shadow ${mapType==='street' ? 'bg-orange-500 text-white' : 'bg-[var(--bg-card)] text-[var(--text-secondary)] border border-[var(--border-color)]'}`}>Street</button>
                <button onClick={() => setMapType('sat')} className={`px-3 py-1.5 rounded-lg text-xs font-semibold backdrop-blur shadow ${mapType==='sat' ? 'bg-orange-500 text-white' : 'bg-[var(--bg-card)] text-[var(--text-secondary)] border border-[var(--border-color)]'}`}>Satellite</button>
                <button onClick={() => setTrafficOn(!trafficOn)} className={`px-3 py-1.5 rounded-lg text-xs font-semibold backdrop-blur shadow ${trafficOn ? 'bg-red-500 text-white' : 'bg-[var(--bg-card)] text-[var(--text-secondary)] border border-[var(--border-color)]'}`}>Traffic</button>
                <button onClick={() => { if(mapInst.current && Object.keys(markersRef.current).length) mapInst.current.fitBounds(L.featureGroup(Object.values(markersRef.current)).getBounds().pad(0.2)); }} className="px-3 py-1.5 rounded-lg text-xs font-semibold bg-[var(--bg-card)] text-[var(--text-secondary)] border border-[var(--border-color)] backdrop-blur shadow hover:bg-[var(--border-highlight)]">Fit All</button>
            </div>
            <div ref={mapRef} className="w-full h-full bg-[var(--bg-tertiary)]" style={{ zIndex: 1 }}></div>
        </div>
    );
};


// ─── DOCUMENT PREVIEW GLASS MODAL ───────────────────────────────────────────
const DocumentPreviewModal = ({ title, url, onClose }) => {
    if (!url) return null;
    return (
        <div className="fixed inset-0 bg-black/85 backdrop-blur-md z-[999999] flex items-center justify-center p-4 animate-fade-in" onClick={onClose}>
            <div className="bg-[#0d0e17]/95 border border-white/20 rounded-3xl p-6 w-full max-w-2xl flex flex-col gap-4 shadow-[0_25px_60px_rgba(0,0,0,0.9)] animate-scale-up" onClick={e=>e.stopPropagation()}>
                <div className="flex justify-between items-center border-b border-white/10 pb-3">
                    <div className="text-sm font-black text-orange-400 uppercase tracking-wider flex items-center gap-2">
                        <IconFileText sz={18}/> {title}
                    </div>
                    <button onClick={onClose} className="p-1 rounded-full hover:bg-white/10 text-white"><IconX sz={18}/></button>
                </div>
                <div className="w-full max-h-[70vh] overflow-auto rounded-2xl border border-white/10 bg-black/40 flex items-center justify-center p-2">
                    {url.startsWith('data:application/pdf') || url.endsWith('.pdf') ? (
                        <iframe src={url} className="w-full h-[500px] rounded-xl"/>
                    ) : (
                        <img src={url} className="max-w-full max-h-[65vh] object-contain rounded-xl shadow-2xl"/>
                    )}
                </div>
                <div className="flex justify-between items-center pt-2">
                    <a href={url} download={`${title.replace(/\s+/g, '_')}_Document`} target="_blank" rel="noreferrer" className="px-4 py-2 bg-orange-500 hover:bg-orange-600 font-bold text-xs text-white rounded-xl flex items-center gap-2 shadow-lg shadow-orange-500/30 transition-all">
                        <IconDownload sz={14}/> Download Document
                    </a>
                    <button onClick={onClose} className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white text-xs font-bold rounded-xl transition-all">Close</button>
                </div>
            </div>
        </div>
    );
};

// ─── ADD / EDIT VEHICLE GLASS UI MODAL ──────────────────────────────────────
const AddVehicleModal = ({ onClose, onSave, vehicleToEdit }) => {
    let rawTargetId = vehicleToEdit?.vehicle_id || vehicleToEdit?.vehicleId || vehicleToEdit?.VehicleId || vehicleToEdit?.id || vehicleToEdit?.Id || vehicleToEdit?.ID;
    if ((rawTargetId === undefined || rawTargetId === null || rawTargetId === '' || isNaN(parseInt(rawTargetId)) || parseInt(rawTargetId) <= 0) && typeof vehicleToEdit === 'object' && vehicleToEdit !== null) {
        for (const key of Object.keys(vehicleToEdit)) {
            if (key.toLowerCase().includes('id') && !key.toLowerCase().includes('url')) {
                const val = parseInt(vehicleToEdit[key]);
                if (!isNaN(val) && val > 0) {
                    rawTargetId = val;
                    break;
                }
            }
        }
    }
    const parsedInit = (rawTargetId !== undefined && rawTargetId !== null && !isNaN(parseInt(rawTargetId))) ? parseInt(rawTargetId) : null;
    const initialId = (parsedInit && parsedInit > 0) ? parsedInit : null;

    const getInitialForm = (v) => ({
        vehicleId: v?.id || v?.vehicle_id || v?.vehicleId || v?.VehicleId || initialId,
        brand: v?.brand || v?.Brand || '',
        model: v?.model || v?.Model || '',
        plateNumber: v?.plateNumber || v?.plate_no || v?.plateNo || v?.PlateNumber || '',
        type: v?.type || v?.Type || 'Car',
        engineCc: v?.engineCc || v?.cc || v?.EngineCc || 1500,
        dailyRatePHP: v?.dailyRatePHP || v?.rate_per_day || v?.ratePerDay || v?.RatePerDay || 2500,
        rateWithDriverPHP: v?.rateWithDriverPHP || v?.rate_with_driver || v?.rateWithDriver || v?.RateWithDriver || 3500,
        seatCapacity: v?.seatCapacity || v?.seat_capacity || v?.SeatCapacity || 5,
        transmission: v?.transmission || v?.Transmission || 'Automatic',
        status: v?.status || v?.Status || 'available',
        description: v?.description || v?.Description || '',
        image: v?.image || v?.photo_url || v?.photoUrl || v?.PhotoUrl || '',
        mapIconUrl: v?.mapIconUrl || v?.map_icon_url || v?.map3DIconUrl || v?.map3dIconUrl || v?.model_3d_url || v?.model3dUrl || v?.Model3dUrl || v?.Model3DUrl || '',
        ltoExpiryDate: formatDateSafe(v?.lto_expiry_date || v?.ltoExpiryDate || v?.documents?.ltoRegistrationExpiry, '2026-10-15'),
        insuranceExpiryDate: formatDateSafe(v?.insurance_expiry_date || v?.insuranceExpiryDate || v?.documents?.insuranceExpiry, '2026-11-20'),
        orCrUrl: v?.orCrUrl || v?.or_cr_url || v?.OrCrUrl || v?.documents?.orCrUrl || v?.documents?.or_cr_url || '',
        insuranceUrl: v?.insuranceUrl || v?.insurance_url || v?.InsuranceUrl || v?.documents?.insuranceUrl || v?.documents?.insurance_url || '',
        color: v?.color || v?.Color || 'Pearl White'
    });

    const [form, setForm] = useState(() => getInitialForm(vehicleToEdit));

    useEffect(() => {
        if (vehicleToEdit) {
            setForm(getInitialForm(vehicleToEdit));
        }
    }, [vehicleToEdit]);

    const descRef = useRef(null);
    useEffect(() => {
        if (descRef.current) {
            descRef.current.style.height = 'auto';
            const minH = 76;
            const maxH = 220;
            const newH = Math.max(minH, Math.min(descRef.current.scrollHeight, maxH));
            descRef.current.style.height = `${newH}px`;
            descRef.current.style.overflowY = descRef.current.scrollHeight > maxH ? 'auto' : 'hidden';
        }
    }, [form.description]);

    const [isSaving, setIsSaving] = useState(false);
    const [errorMessage, setErrorMessage] = useState('');
    const [suggestData, setSuggestData] = useState(null);
    const mediaInputRef = useRef(null);
    const iconInputRef = useRef(null);
    const orCrInputRef = useRef(null);
    const insuranceInputRef = useRef(null);

    const handleOpenSuggest = () => {
        const typeLower = (form.type || '').toLowerCase();
        const cc = parseInt(form.engineCc) || 1500;
        let baseRate = 2500;
        if (typeLower.includes('suv')) baseRate = cc > 2000 ? 4200 : 3500;
        else if (typeLower.includes('van')) baseRate = 4500;
        else if (typeLower.includes('pickup')) baseRate = 3400;
        else if (typeLower.includes('hatchback')) baseRate = 1800;
        else if (typeLower.includes('motorcycle')) baseRate = 800;
        else baseRate = 2200;

        const demandFactor = Math.round(baseRate * 0.12);
        const inflationFactor = Math.round(baseRate * 0.05);
        const calculatedRate = baseRate + demandFactor + inflationFactor;
        const calculatedRateWithDriver = calculatedRate + 1200;

        setSuggestData({
            baseRate,
            demandFactor,
            inflationFactor,
            calculatedRate,
            calculatedRateWithDriver,
            reasoning: `Recommended pricing for ${form.brand || 'Vehicle'} ${form.model || ''} (${form.type || 'Car'}) with ${cc}cc engine based on current market demand & inflation indices.`
        });
    };

    const applySuggest = () => {
        if (suggestData) {
            setForm(f => ({
                ...f,
                dailyRatePHP: suggestData.calculatedRate,
                rateWithDriverPHP: suggestData.calculatedRateWithDriver
            }));
            setSuggestData(null);
        }
    };

    const processTransparentImage = (fileOrUrl, onComplete) => {
        if (!fileOrUrl) return;
        const img = new Image();
        img.crossOrigin = "Anonymous";
        
        const onLoad = () => {
            try {
                const canvas = document.createElement('canvas');
                let width = img.width || 400;
                let height = img.height || 400;
                const MAX_DIM = 800;
                if (width > MAX_DIM || height > MAX_DIM) {
                    if (width > height) {
                        height = Math.round((height * MAX_DIM) / width);
                        width = MAX_DIM;
                    } else {
                        width = Math.round((width * MAX_DIM) / height);
                        height = MAX_DIM;
                    }
                }
                canvas.width = width;
                canvas.height = height;
                const ctx = canvas.getContext('2d');
                ctx.drawImage(img, 0, 0, width, height);

                const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height);
                const data = imgData.data;

                const bgR = data[0], bgG = data[1], bgB = data[2];

                for (let i = 0; i < data.length; i += 4) {
                    const r = data[i], g = data[i + 1], b = data[i + 2];
                    const distBg = Math.sqrt((r - bgR) ** 2 + (g - bgG) ** 2 + (b - bgB) ** 2);
                    const isWhite = r > 235 && g > 235 && b > 235;

                    if (distBg < 45 || isWhite) {
                        data[i + 3] = 0; // Make background transparent
                    }
                }

                ctx.putImageData(imgData, 0, 0);
                onComplete(canvas.toDataURL('image/png'));
            } catch (e) {
                console.error("Canvas transparent processing failed:", e);
                if (typeof fileOrUrl === 'string') onComplete(fileOrUrl);
            }
        };

        if (typeof fileOrUrl === 'string') {
            img.src = fileOrUrl;
            if (img.complete) onLoad();
            else img.onload = onLoad;
        } else if (fileOrUrl instanceof File) {
            const reader = new FileReader();
            reader.onload = (e) => {
                img.src = e.target.result;
                img.onload = onLoad;
            };
            reader.readAsDataURL(fileOrUrl);
        }
    };

    const handleMediaUpload = (e) => {
        const file = e.target.files && e.target.files[0];
        if (!file) return;
        if (file.type.startsWith('image/')) {
            const reader = new FileReader();
            reader.onload = (ev) => {
                const img = new Image();
                img.onload = () => {
                    const canvas = document.createElement('canvas');
                    let width = img.width || 800;
                    let height = img.height || 800;
                    const MAX_DIM = 1200;
                    if (width > MAX_DIM || height > MAX_DIM) {
                        if (width > height) {
                            height = Math.round((height * MAX_DIM) / width);
                            width = MAX_DIM;
                        } else {
                            width = Math.round((width * MAX_DIM) / height);
                            height = MAX_DIM;
                        }
                    }
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);
                    const compressedDataUrl = canvas.toDataURL('image/jpeg', 0.85);
                    setForm(f => ({ ...f, image: compressedDataUrl }));
                };
                img.src = ev.target.result;
            };
            reader.readAsDataURL(file);
        } else {
            const reader = new FileReader();
            reader.onload = (ev) => {
                setForm(f => ({ ...f, image: ev.target.result }));
            };
            reader.readAsDataURL(file);
        }
    };

    const handleIconUpload = (e) => {
        const file = e.target.files && e.target.files[0];
        if (file) {
            processTransparentImage(file, (transparentDataUrl) => {
                setForm(f => ({ ...f, mapIconUrl: transparentDataUrl }));
            });
        }
    };

    const handleAuto3DTransparent = () => {
        const targetSrc = form.mapIconUrl || form.image;
        if (!targetSrc) {
            alert("Please upload a Vehicle Photo first or browse an image to convert.");
            return;
        }
        processTransparentImage(targetSrc, (transparentDataUrl) => {
            setForm(f => ({ ...f, mapIconUrl: transparentDataUrl }));
        });
    };

    const [isRotating, setIsRotating] = useState(false);

    const rotateMarkerImage = (angleDeltaDeg) => {
        const src = form.mapIconUrl;
        if (!src || isRotating) return;
        setIsRotating(true);
        const img = new Image();
        img.crossOrigin = 'Anonymous';
        img.onload = () => {
            try {
                const rad = (angleDeltaDeg * Math.PI) / 180;
                const sin = Math.abs(Math.sin(rad));
                const cos = Math.abs(Math.cos(rad));
                const origW = img.naturalWidth || img.width || 200;
                const origH = img.naturalHeight || img.height || 200;
                const newW = Math.round(origW * cos + origH * sin);
                const newH = Math.round(origW * sin + origH * cos);

                const canvas = document.createElement('canvas');
                canvas.width = Math.max(newW, 32);
                canvas.height = Math.max(newH, 32);
                const ctx = canvas.getContext('2d');
                ctx.imageSmoothingEnabled = true;
                ctx.imageSmoothingQuality = 'high';

                ctx.translate(canvas.width / 2, canvas.height / 2);
                ctx.rotate(rad);
                ctx.drawImage(img, -origW / 2, -origH / 2);

                const rotatedDataUrl = canvas.toDataURL('image/png');
                setForm(f => ({ ...f, mapIconUrl: rotatedDataUrl }));
            } catch (err) {
                console.error('Marker rotation error:', err);
            } finally {
                setIsRotating(false);
            }
        };
        img.onerror = () => setIsRotating(false);
        img.src = src;
    };

    const handleDocUpload = (e, fieldName) => {
        const file = e.target.files && e.target.files[0];
        if (!file) return;

        if (file.type.startsWith('image/')) {
            const reader = new FileReader();
            reader.onload = (ev) => {
                const img = new Image();
                img.onload = () => {
                    const canvas = document.createElement('canvas');
                    let width = img.width || 800;
                    let height = img.height || 800;
                    const MAX_DIM = 1200;

                    if (width > MAX_DIM || height > MAX_DIM) {
                        if (width > height) {
                            height = Math.round((height * MAX_DIM) / width);
                            width = MAX_DIM;
                        } else {
                            width = Math.round((width * MAX_DIM) / height);
                            height = MAX_DIM;
                        }
                    }

                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);
                    const compressedDataUrl = canvas.toDataURL('image/jpeg', 0.82);
                    setForm(f => ({ ...f, [fieldName]: compressedDataUrl }));
                };
                img.src = ev.target.result;
            };
            reader.readAsDataURL(file);
        } else {
            if (file.size > 4 * 1024 * 1024) {
                alert("Document file is too large. Please upload a file under 4MB.");
                return;
            }
            const reader = new FileReader();
            reader.onload = (ev) => {
                setForm(f => ({ ...f, [fieldName]: ev.target.result }));
            };
            reader.readAsDataURL(file);
        }
    };

    const submit = async (e) => {
        if (e) e.preventDefault();
        setErrorMessage('');
        if (!form.brand || !form.model || !form.plateNumber) {
            setErrorMessage("Please fill in Brand, Model, and Plate Number.");
            return;
        }

        try {
            setIsSaving(true);

            const pickId = (...sources) => {
                for (const s of sources) {
                    if (s !== undefined && s !== null && s !== '' && !isNaN(parseInt(s))) {
                        const val = parseInt(s);
                        if (val > 0) return val;
                    }
                }
                return null;
            };
            let targetId = pickId(
                initialId,
                vehicleToEdit?.vehicle_id,
                vehicleToEdit?.vehicleId,
                vehicleToEdit?.VehicleId,
                vehicleToEdit?.id,
                vehicleToEdit?.Id,
                vehicleToEdit?.ID
            );

            if (!targetId && typeof vehicleToEdit === 'object' && vehicleToEdit !== null) {
                for (const key of Object.keys(vehicleToEdit)) {
                    if (key.toLowerCase().includes('id') && !key.toLowerCase().includes('url')) {
                        const val = parseInt(vehicleToEdit[key]);
                        if (!isNaN(val) && val > 0) {
                            targetId = val;
                            break;
                        }
                    }
                }
            }
            if (!targetId && form.plateNumber && Array.isArray(vehicles)) {
                const cleanPlate = (form.plateNumber || '').trim().toLowerCase().replace(/-/g, '');
                const match = vehicles.find(v => (v.plateNumber || v.plate_no || v.plateNo || '').trim().toLowerCase().replace(/-/g, '') === cleanPlate);
                if (match) {
                    targetId = pickId(match.vehicle_id, match.vehicleId, match.VehicleId, match.id);
                }
            }

            const isEditMode = Boolean(vehicleToEdit);
            const shouldUpdate = (isEditMode || Boolean(targetId)) && targetId !== null && targetId > 0;

            const payload = {
                vehicleId: targetId || (form.vehicleId ? parseInt(form.vehicleId) : undefined),
                vehicle_id: targetId || (form.vehicleId ? parseInt(form.vehicleId) : undefined),
                brand: form.brand,
                model: form.model,
                plateNo: form.plateNumber,
                plate_no: form.plateNumber,
                type: form.type,
                cc: parseInt(form.engineCc) || 1500,
                ratePerDay: parseFloat(form.dailyRatePHP) || 0,
                rate_per_day: parseFloat(form.dailyRatePHP) || 0,
                rateWithDriver: parseFloat(form.rateWithDriverPHP) || 0,
                rate_with_driver: parseFloat(form.rateWithDriverPHP) || 0,
                seatCapacity: parseInt(form.seatCapacity) || 5,
                seat_capacity: parseInt(form.seatCapacity) || 5,
                transmission: form.transmission,
                status: form.status,
                description: form.description,
                photoUrl: form.image || '',
                photo_url: form.image || '',
                model3dUrl: form.mapIconUrl || '',
                model_3d_url: form.mapIconUrl || '',
                map_icon_url: form.mapIconUrl || '',
                ltoExpiryDate: form.ltoExpiryDate ? new Date(form.ltoExpiryDate).toISOString() : null,
                lto_expiry_date: form.ltoExpiryDate ? new Date(form.ltoExpiryDate).toISOString() : null,
                insuranceExpiryDate: form.insuranceExpiryDate ? new Date(form.insuranceExpiryDate).toISOString() : null,
                insurance_expiry_date: form.insuranceExpiryDate ? new Date(form.insuranceExpiryDate).toISOString() : null,
                orCrUrl: form.orCrUrl || '',
                or_cr_url: form.orCrUrl || '',
                insuranceUrl: form.insuranceUrl || '',
                insurance_url: form.insuranceUrl || ''
            };

            const url = shouldUpdate ? `${API}/api/vehicles/${targetId}` : `${API}/api/vehicles`;
            const method = shouldUpdate ? 'PUT' : 'POST';

            const res = await fetch(url, {
                method: method,
                headers: HEADERS,
                body: JSON.stringify(payload)
            });

            if (res.ok || res.status === 201 || res.status === 204) {
                const resData = await res.json().catch(() => ({}));
                const finalId = targetId || resData?.vehicleId || resData?.VehicleId || resData?.vehicle_id || form.vehicleId;
                const updatedVehicleObj = normVehicle({
                    ...(vehicleToEdit || {}),
                    ...form,
                    vehicle_id: finalId,
                    vehicleId: finalId,
                    VehicleId: finalId,
                    id: finalId,
                    plate_no: form.plateNumber,
                    plateNumber: form.plateNumber,
                    brand: form.brand,
                    model: form.model,
                    type: form.type,
                    rate_per_day: form.dailyRatePHP,
                    dailyRatePHP: form.dailyRatePHP,
                    rate_with_driver: form.rateWithDriverPHP,
                    rateWithDriverPHP: form.rateWithDriverPHP,
                    cc: form.engineCc,
                    engineCc: form.engineCc,
                    seat_capacity: form.seatCapacity,
                    seatCapacity: form.seatCapacity,
                    transmission: form.transmission,
                    status: form.status,
                    description: form.description,
                    photo_url: form.image || '',
                    image: form.image || '',
                    model_3d_url: form.mapIconUrl || '',
                    map_icon_url: form.mapIconUrl || '',
                    mapIconUrl: form.mapIconUrl || '',
                    lto_expiry_date: form.ltoExpiryDate,
                    insurance_expiry_date: form.insuranceExpiryDate,
                    or_cr_url: form.orCrUrl || '',
                    orCrUrl: form.orCrUrl || '',
                    insurance_url: form.insuranceUrl || '',
                    insuranceUrl: form.insuranceUrl || '',
                    documents: {
                        ltoRegistrationExpiry: formatDateSafe(form.ltoExpiryDate, '2026-10-15'),
                        insuranceExpiry: formatDateSafe(form.insuranceExpiryDate, '2026-11-20'),
                        orCrUrl: form.orCrUrl || '',
                        insuranceUrl: form.insuranceUrl || ''
                    }
                });
                onSave(resData?.message || (shouldUpdate ? "Vehicle updated successfully!" : "Vehicle added to fleet successfully!"), updatedVehicleObj);
            } else {
                const rawErrText = await res.text().catch(() => '');
                let parsedMsg = '';
                try {
                    const parsedJson = JSON.parse(rawErrText);
                    parsedMsg = parsedJson.message || parsedJson.Message || parsedJson.title || '';
                } catch (_) {}
                const displayMsg = parsedMsg || rawErrText || `Error saving vehicle data (HTTP ${res.status}).`;
                setErrorMessage(displayMsg);
            }
        } catch (err) {
            console.error("Save Vehicle Exception:", err);
            setErrorMessage(`Network or System Exception: ${err?.message || err}`);
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/75 backdrop-blur-xl p-4 sm:p-6 animate-fade-in" onClick={onClose}>
            <div className="bg-[#0d0e17]/90 backdrop-blur-2xl border border-white/20 rounded-3xl shadow-[0_25px_60px_rgba(0,0,0,0.8)] w-full max-w-4xl max-h-[90vh] flex flex-col overflow-hidden relative animate-scale-up" onClick={e => e.stopPropagation()}>
                
                {/* Visual Glass Saving Overlay Loading State */}
                {isSaving && (
                    <div className="absolute inset-0 z-50 bg-black/70 backdrop-blur-md flex flex-col items-center justify-center gap-3 animate-fade-in">
                        <div className="w-16 h-16 rounded-2xl bg-orange-500/20 border border-orange-500/40 flex items-center justify-center text-orange-500 shadow-2xl shadow-orange-500/30">
                            <IconRefreshCw sz={32} c="animate-spin" />
                        </div>
                        <div className="text-base font-black text-white tracking-wide">Saving Changes...</div>
                        <div className="text-xs text-orange-300/80 font-medium">Updating vehicle specs & documents in database</div>
                    </div>
                )}

                {/* Header */}
                <div className="p-5 border-b border-white/10 flex justify-between items-center bg-white/5 backdrop-blur-md shrink-0">
                    <h2 className="text-lg font-black text-white flex items-center gap-2">
                        <span className="text-orange-500">{vehicleToEdit ? <IconEdit sz={18} className="inline"/> : <IconPlus sz={18} className="inline"/>}</span>
                        <span>{vehicleToEdit ? 'Edit Vehicle Specs' : 'Add New Vehicle'}</span>
                    </h2>
                    <button onClick={onClose} className="p-1.5 rounded-full hover:bg-white/10 text-white transition-colors"><IconX sz={18}/></button>
                </div>

                {/* Inline Error Alert */}
                {errorMessage && (
                    <div className="mx-6 mt-4 p-3.5 rounded-2xl bg-red-500/10 border border-red-500/30 flex items-start gap-3 text-red-400 text-xs shrink-0 animate-fade-in">
                        <IconAlertTriangle sz={18} className="shrink-0 mt-0.5" />
                        <div className="flex-1">
                            <p className="font-bold">Failed to save vehicle specs</p>
                            <p className="text-[11px] text-red-300 mt-0.5 leading-relaxed">{errorMessage}</p>
                        </div>
                        <button type="button" onClick={() => setErrorMessage('')} className="text-red-400 hover:text-red-200"><IconX sz={14}/></button>
                    </div>
                )}

                {/* Form Body - 2 Columns */}
                <div className="p-6 overflow-y-auto flex-1 custom-scrollbar grid grid-cols-1 lg:grid-cols-12 gap-6">
                    
                    {/* LEFT COLUMN: Inputs (7 Cols) */}
                    <div className="lg:col-span-7 flex flex-col gap-4">
                        
                        {/* Brand & Model */}
                        <div className="grid grid-cols-2 gap-3">
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Brand *</label>
                                <input type="text" value={form.brand} onChange={e=>setForm({...form, brand: e.target.value})} className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all placeholder-gray-600" placeholder="e.g. Honda, Ford, Toyota" />
                            </div>
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Model *</label>
                                <input type="text" value={form.model} onChange={e=>setForm({...form, model: e.target.value})} className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all placeholder-gray-600" placeholder="e.g. Civic, Ranger, Vios" />
                            </div>
                        </div>

                        {/* Plate No & Vehicle Type */}
                        <div className="grid grid-cols-2 gap-3">
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Plate No. *</label>
                                <input type="text" value={form.plateNumber} onChange={e=>setForm({...form, plateNumber: e.target.value})} className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-bold uppercase text-white focus:border-orange-500 outline-none transition-all placeholder-gray-600" placeholder="e.g. ABC-1234" />
                            </div>
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Vehicle Type</label>
                                <select value={form.type} onChange={e=>setForm({...form, type: e.target.value})} className="w-full p-3 rounded-xl bg-[#141625] border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all">
                                    <option value="Car">Car</option>
                                    <option value="SUV">SUV</option>
                                    <option value="Van">Van</option>
                                    <option value="Pickup">Pickup</option>
                                    <option value="Hatchback">Hatchback</option>
                                    <option value="Motorcycle">Motorcycle</option>
                                </select>
                            </div>
                        </div>

                        {/* Engine CC & Daily Rate with Suggest */}
                        <div className="grid grid-cols-2 gap-3">
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Engine CC</label>
                                <input type="number" value={form.engineCc} onChange={e=>setForm({...form, engineCc: e.target.value})} className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all" placeholder="e.g. 1500" />
                            </div>
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Rate / Day (₱)</label>
                                <div className="flex gap-2">
                                    <div className="relative flex items-center w-full">
                                        <span className="absolute left-3.5 text-sm font-black text-orange-400 select-none">₱</span>
                                        <input type="number" value={form.dailyRatePHP} onChange={e=>setForm({...form, dailyRatePHP: e.target.value})} className="w-full pl-8 pr-3 py-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all" placeholder="2500" />
                                    </div>
                                    <button type="button" onClick={handleOpenSuggest} className="px-3 py-2 rounded-xl bg-orange-500 hover:bg-orange-600 text-white font-bold text-xs shadow-md transition-all shrink-0 flex items-center gap-1"><IconZap sz={14}/> Suggest</button>
                                </div>
                            </div>
                        </div>

                        {/* Rate + Driver & Seat Capacity */}
                        <div className="grid grid-cols-2 gap-3">
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Rate + Driver (₱)</label>
                                <div className="relative flex items-center w-full">
                                    <span className="absolute left-3.5 text-sm font-black text-orange-400 select-none">₱</span>
                                    <input type="number" value={form.rateWithDriverPHP} onChange={e=>setForm({...form, rateWithDriverPHP: e.target.value})} className="w-full pl-8 pr-3 py-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all" placeholder="3500" />
                                </div>
                            </div>
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Seat Capacity</label>
                                <input type="number" value={form.seatCapacity} onChange={e=>setForm({...form, seatCapacity: e.target.value})} className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all" placeholder="e.g. 5" />
                            </div>
                        </div>

                        {/* Transmission & Status */}
                        <div className="grid grid-cols-2 gap-3">
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Transmission</label>
                                <select value={form.transmission} onChange={e=>setForm({...form, transmission: e.target.value})} className="w-full p-3 rounded-xl bg-[#141625] border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all">
                                    <option value="Automatic">Automatic</option>
                                    <option value="Manual">Manual</option>
                                </select>
                            </div>
                            <div>
                                <label className="text-xs font-semibold text-gray-400 mb-1 block">Status</label>
                                {(form.status || '').toLowerCase() === 'rented' ? (
                                    <div className="w-full p-3 rounded-xl bg-amber-500/10 border border-amber-500/30 text-sm font-bold text-amber-400 flex items-center justify-between">
                                        <span className="flex items-center gap-1.5 capitalize">
                                            <span className="w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span> rented
                                        </span>
                                        <span className="text-[10px] text-amber-400/80 font-normal">🔒 Auto-managed via active rental</span>
                                    </div>
                                ) : (
                                    <select value={form.status} onChange={e=>setForm({...form, status: e.target.value})} className="w-full p-3 rounded-xl bg-[#141625] border border-white/10 text-sm font-semibold text-white focus:border-orange-500 outline-none transition-all capitalize">
                                        <option value="available">available</option>
                                        <option value="maintenance">maintenance</option>
                                    </select>
                                )}
                            </div>
                        </div>

                        {/* Description */}
                        <div>
                            <label className="text-xs font-semibold text-gray-400 mb-1 block">Description</label>
                            <textarea
                                ref={descRef}
                                value={form.description}
                                onChange={e => {
                                    setForm({ ...form, description: e.target.value });
                                    e.target.style.height = 'auto';
                                    const minH = 76;
                                    const maxH = 220;
                                    const newH = Math.max(minH, Math.min(e.target.scrollHeight, maxH));
                                    e.target.style.height = `${newH}px`;
                                    e.target.style.overflowY = e.target.scrollHeight > maxH ? 'auto' : 'hidden';
                                }}
                                rows="3"
                                className="w-full p-3 rounded-xl bg-white/5 border border-white/10 text-sm font-medium text-white focus:border-orange-500 outline-none transition-all resize-none placeholder-gray-600 min-h-[76px] max-h-[220px]"
                                placeholder="Vehicle notes, specs, condition..."
                            ></textarea>
                        </div>

                    </div>

                    {/* RIGHT COLUMN: 3D Preview & Media Uploaders (5 Cols) */}
                    <div className="lg:col-span-5 flex flex-col gap-5 border-t lg:border-t-0 lg:border-l border-white/10 lg:pl-6 pt-4 lg:pt-0">
                        
                        {/* Interactive 3D Digital Card Preview */}
                        <div>
                            <div className="text-[10px] font-bold text-gray-400 uppercase tracking-widest text-center mb-3">Interactive 3D Digital Preview</div>
                            <div className="p-4 rounded-3xl bg-gradient-to-br from-white/10 to-white/5 border border-white/15 shadow-2xl relative overflow-hidden group">
                                <div className="h-32 w-full rounded-2xl overflow-hidden bg-black/40 relative mb-3">
                                    {form.image ? (
                                        <img src={form.image} className="w-full h-full object-cover" />
                                    ) : (
                                        <div className="w-full h-full flex items-center justify-center text-gray-500 font-bold text-xs">No Photo Selected</div>
                                    )}
                                    <span className="absolute top-2 right-2 px-2.5 py-1 rounded-full text-[10px] font-black uppercase bg-emerald-500/80 text-white backdrop-blur">
                                        {form.status}
                                    </span>
                                </div>
                                <div className="text-xs font-bold text-orange-400 uppercase tracking-wider">{form.brand || 'BRAND'} {form.model || 'MODEL'}</div>
                                <div className="text-lg font-black text-white">{form.plateNumber || 'PLATE-123'}</div>
                                <div className="flex justify-between items-center mt-2 pt-2 border-t border-white/10 text-xs">
                                    <span className="text-gray-400 font-medium">Daily Rate:</span>
                                    <span className="font-bold text-emerald-400">₱{parseInt(form.dailyRatePHP || 0).toLocaleString()}/day</span>
                                </div>
                            </div>
                        </div>

                        {/* Vehicle Media Gallery Uploader */}
                        <div>
                            <div className="flex justify-between items-center mb-2">
                                <label className="text-xs font-bold text-gray-300">Vehicle Photo / Media</label>
                                <button type="button" onClick={() => mediaInputRef.current?.click()} className="px-3 py-1.5 rounded-xl bg-orange-500/20 text-orange-400 border border-orange-500/30 hover:bg-orange-500/30 font-bold text-xs transition-all flex items-center gap-1">
                                    + Add Media
                                </button>
                                <input type="file" ref={mediaInputRef} onChange={handleMediaUpload} accept="image/*" className="hidden" />
                            </div>
                            <input type="text" value={form.image} onChange={e=>setForm({...form, image: e.target.value})} className="w-full p-2.5 rounded-xl bg-white/5 border border-white/10 text-xs text-white placeholder-gray-600 outline-none" placeholder="Paste photo URL or click Add Media..." />
                        </div>

                        {/* Map / 3D Marker Icon Uploader & Interactive Orientation Studio */}
                        <div className="space-y-2">
                            <div className="flex justify-between items-center">
                                <label className="text-xs font-bold text-gray-300">Map / 3D Car Marker Icon</label>
                                <button type="button" onClick={handleAuto3DTransparent} className="px-2.5 py-1 rounded-lg bg-orange-500/20 hover:bg-orange-500/30 text-orange-400 border border-orange-500/30 font-bold text-[10px] transition-all flex items-center gap-1">
                                    <IconZap sz={12}/> AI 3D Transparent
                                </button>
                                <input type="file" ref={iconInputRef} onChange={handleIconUpload} accept="image/*" className="hidden" />
                            </div>

                            {form.mapIconUrl ? (
                                <div className="rounded-2xl border-2 border-orange-500/40 bg-slate-950/80 p-3 space-y-3 shadow-xl relative overflow-hidden animate-fade-in">
                                    {/* Compass Header */}
                                    <div className="flex items-center justify-between border-b border-white/10 pb-2">
                                        <div className="flex items-center gap-2">
                                            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                                            <span className="text-xs font-black tracking-wide text-white uppercase flex items-center gap-1.5">
                                                <IconCompass sz={14} className="text-orange-500" /> 3D Marker Orientation
                                            </span>
                                        </div>
                                        <div className="text-[10px] font-extrabold text-cyan-300 bg-cyan-500/10 px-2.5 py-0.5 rounded-full border border-cyan-500/30 flex items-center gap-1">
                                            <span>Front = Facing Up</span> <span className="text-xs font-black">↑</span>
                                        </div>
                                    </div>

                                    {/* Live Compass Target Area */}
                                    <div className="relative w-full h-40 rounded-xl bg-[#080d1a] border border-cyan-500/20 flex items-center justify-center overflow-hidden select-none">
                                        {/* Radar Concentric Rings & Crosshair Grid */}
                                        <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-20">
                                            <div className="w-32 h-32 rounded-full border border-cyan-400"></div>
                                            <div className="w-20 h-20 rounded-full border border-dashed border-cyan-400 absolute"></div>
                                            <div className="w-10 h-10 rounded-full border border-cyan-400 absolute"></div>
                                            <div className="w-full h-[1px] bg-cyan-400/40 absolute"></div>
                                            <div className="h-full w-[1px] bg-cyan-400/40 absolute"></div>
                                        </div>

                                        {/* Forward Headlight Beam Indicator */}
                                        <div className="absolute top-1 left-1/2 -translate-x-1/2 flex flex-col items-center pointer-events-none z-0">
                                            <div className="px-2 py-0.5 rounded-full bg-cyan-500/25 border border-cyan-400 text-cyan-200 text-[9px] font-black uppercase tracking-wider flex items-center gap-1 shadow-[0_0_12px_rgba(6,182,212,0.7)]">
                                                <span>▲ FRONT (Nose)</span>
                                            </div>
                                            <div style={{ width: '48px', height: '36px', background: 'radial-gradient(ellipse at bottom, rgba(56, 189, 248, 0.45) 0%, rgba(56, 189, 248, 0.08) 60%, transparent 85%)', clipPath: 'polygon(20% 100%, 80% 100%, 100% 0%, 0% 0%)' }}></div>
                                        </div>

                                        {/* Center 3D Vehicle Marker Image */}
                                        <div className="relative z-10 w-24 h-24 flex items-center justify-center p-1">
                                            <img
                                                src={form.mapIconUrl}
                                                alt="3D GPS Marker"
                                                className={`max-w-full max-h-full object-contain filter drop-shadow-[0_8px_16px_rgba(0,0,0,0.9)] transition-transform duration-200 ${isRotating ? 'scale-90 opacity-70' : 'scale-100 opacity-100'}`}
                                            />
                                        </div>

                                        {/* Rear Indicator */}
                                        <div className="absolute bottom-1 left-1/2 -translate-x-1/2 px-1.5 py-0.2 rounded bg-black/60 text-[8px] font-bold text-slate-400 border border-white/5 pointer-events-none">
                                            ▼ REAR (Tail)
                                        </div>
                                    </div>

                                    {/* Rotation Control Toolbar */}
                                    <div className="space-y-2">
                                        <div className="flex items-center justify-between text-[11px]">
                                            <span className="font-bold text-gray-300">Rotate until vehicle front points UP (▲):</span>
                                            <span className="text-[10px] text-emerald-400 font-bold flex items-center gap-1">
                                                <IconCheck sz={11} /> Auto-Saved
                                            </span>
                                        </div>

                                        {/* Quick Rotate Buttons */}
                                        <div className="grid grid-cols-5 gap-1.5">
                                            <button
                                                type="button"
                                                onClick={() => rotateMarkerImage(-90)}
                                                disabled={isRotating}
                                                className="py-1.5 px-1 rounded-xl bg-white/10 hover:bg-orange-500/20 text-white hover:text-orange-400 border border-white/10 hover:border-orange-500/40 text-xs font-bold transition-all flex items-center justify-center gap-1 cursor-pointer disabled:opacity-50"
                                                title="Rotate -90° Counter-Clockwise"
                                            >
                                                <IconRotateCcw sz={12} /> -90°
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => rotateMarkerImage(90)}
                                                disabled={isRotating}
                                                className="py-1.5 px-1 rounded-xl bg-white/10 hover:bg-orange-500/20 text-white hover:text-orange-400 border border-white/10 hover:border-orange-500/40 text-xs font-bold transition-all flex items-center justify-center gap-1 cursor-pointer disabled:opacity-50"
                                                title="Rotate +90° Clockwise"
                                            >
                                                <IconRotateCw sz={12} /> +90°
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => rotateMarkerImage(-15)}
                                                disabled={isRotating}
                                                className="py-1.5 px-1 rounded-xl bg-white/10 hover:bg-orange-500/20 text-white hover:text-orange-400 border border-white/10 hover:border-orange-500/40 text-xs font-bold transition-all flex items-center justify-center gap-0.5 cursor-pointer disabled:opacity-50"
                                                title="Fine-tune -15°"
                                            >
                                                <IconRotateCcw sz={10} /> -15°
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => rotateMarkerImage(15)}
                                                disabled={isRotating}
                                                className="py-1.5 px-1 rounded-xl bg-white/10 hover:bg-orange-500/20 text-white hover:text-orange-400 border border-white/10 hover:border-orange-500/40 text-xs font-bold transition-all flex items-center justify-center gap-0.5 cursor-pointer disabled:opacity-50"
                                                title="Fine-tune +15°"
                                            >
                                                <IconRotateCw sz={10} /> +15°
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => rotateMarkerImage(180)}
                                                disabled={isRotating}
                                                className="py-1.5 px-1 rounded-xl bg-white/10 hover:bg-orange-500/20 text-white hover:text-orange-400 border border-white/10 hover:border-orange-500/40 text-xs font-bold transition-all flex items-center justify-center gap-1 cursor-pointer disabled:opacity-50"
                                                title="Flip 180°"
                                            >
                                                ↕ 180°
                                            </button>
                                        </div>

                                        {/* Bottom Actions: Change, AI Re-extract, Remove */}
                                        <div className="flex items-center justify-between pt-1 border-t border-white/10">
                                            <div className="flex items-center gap-2">
                                                <button
                                                    type="button"
                                                    onClick={() => iconInputRef.current?.click()}
                                                    className="px-2.5 py-1 rounded-lg bg-orange-500/10 hover:bg-orange-500/20 text-orange-400 border border-orange-500/30 text-[11px] font-bold transition-all flex items-center gap-1 cursor-pointer"
                                                >
                                                    <IconEdit sz={12} /> Change File
                                                </button>
                                                <button
                                                    type="button"
                                                    onClick={handleAuto3DTransparent}
                                                    className="px-2.5 py-1 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-400 border border-amber-500/30 text-[11px] font-bold transition-all flex items-center gap-1 cursor-pointer"
                                                >
                                                    <IconZap sz={12} /> AI 3D Transparent
                                                </button>
                                            </div>
                                            <button
                                                type="button"
                                                onClick={() => setForm(f => ({ ...f, mapIconUrl: '' }))}
                                                className="px-2 py-1 rounded-lg bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/30 transition-all cursor-pointer flex items-center gap-1 text-[11px] font-bold"
                                                title="Remove 3D Icon"
                                            >
                                                <IconTrash sz={12} /> Remove
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            ) : (
                                <div
                                    onClick={() => iconInputRef.current?.click()}
                                    className="border-2 border-dashed border-white/10 hover:border-orange-500/60 p-3.5 rounded-2xl text-center cursor-pointer transition-all bg-white/5 hover:bg-orange-500/5 flex flex-col items-center justify-center gap-1 group select-none"
                                >
                                    <div className="w-9 h-9 rounded-xl bg-cyan-500/10 text-cyan-400 flex items-center justify-center group-hover:scale-110 transition-transform">
                                        <IconCompass sz={18} />
                                    </div>
                                    <div className="text-xs font-bold text-white">Drop 3D marker icon or click <span className="text-orange-500 font-black">AI 3D Transparent</span></div>
                                    <div className="text-[10px] text-gray-400">Automatic 3D car icon orientation on GPS Map</div>
                                </div>
                            )}
                        </div>

                        {/* Legal Expiry & OR/CR / Insurance Documents Upload */}
                        <div className="pt-4 border-t border-white/10 space-y-4">
                            <div className="text-xs font-black text-orange-400 uppercase tracking-widest flex items-center gap-1.5">
                                <IconFileText sz={14}/> Legal & Vehicle Registration Documents
                            </div>

                            {/* LTO Registration (OR/CR) Expiry & File */}
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 p-3.5 rounded-2xl bg-white/5 border border-white/10">
                                <div>
                                    <label className="text-[11px] font-bold text-gray-300 block mb-1">LTO Registration (OR/CR) Expiry</label>
                                    <input type="date" value={form.ltoExpiryDate} onChange={e=>setForm({...form, ltoExpiryDate: e.target.value})} className="w-full p-2.5 rounded-xl bg-white/5 border border-white/10 text-xs text-white outline-none" />
                                </div>
                                <div>
                                    <div className="flex justify-between items-center mb-1">
                                        <label className="text-[11px] font-bold text-gray-300">OR/CR Document File/URL</label>
                                        <button type="button" onClick={() => orCrInputRef.current?.click()} className="px-2.5 py-1 rounded-lg bg-orange-500/20 text-orange-400 border border-orange-500/30 text-[10px] font-bold hover:bg-orange-500/30 transition-all">
                                            Upload File
                                        </button>
                                        <input type="file" ref={orCrInputRef} onChange={(e) => handleDocUpload(e, 'orCrUrl')} accept="image/*,.pdf" className="hidden" />
                                    </div>
                                    <input type="text" value={form.orCrUrl} onChange={e=>setForm({...form, orCrUrl: e.target.value})} className="w-full p-2.5 rounded-xl bg-white/5 border border-white/10 text-xs text-white placeholder-gray-600 outline-none" placeholder="Paste OR/CR URL or Upload..." />
                                </div>
                            </div>

                            {/* CTPL Insurance Expiry & File */}
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 p-3.5 rounded-2xl bg-white/5 border border-white/10">
                                <div>
                                    <label className="text-[11px] font-bold text-gray-300 block mb-1">CTPL Insurance Expiry</label>
                                    <input type="date" value={form.insuranceExpiryDate} onChange={e=>setForm({...form, insuranceExpiryDate: e.target.value})} className="w-full p-2.5 rounded-xl bg-white/5 border border-white/10 text-xs text-white outline-none" />
                                </div>
                                <div>
                                    <div className="flex justify-between items-center mb-1">
                                        <label className="text-[11px] font-bold text-gray-300">Insurance Policy File/URL</label>
                                        <button type="button" onClick={() => insuranceInputRef.current?.click()} className="px-2.5 py-1 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 text-[10px] font-bold hover:bg-emerald-500/30 transition-all">
                                            Upload File
                                        </button>
                                        <input type="file" ref={insuranceInputRef} onChange={(e) => handleDocUpload(e, 'insuranceUrl')} accept="image/*,.pdf" className="hidden" />
                                    </div>
                                    <input type="text" value={form.insuranceUrl} onChange={e=>setForm({...form, insuranceUrl: e.target.value})} className="w-full p-2.5 rounded-xl bg-white/5 border border-white/10 text-xs text-white placeholder-gray-600 outline-none" placeholder="Paste Policy URL or Upload..." />
                                </div>
                            </div>
                        </div>

                    </div>

                </div>

                {/* Footer Buttons */}
                <div className="p-5 border-t border-white/10 bg-white/5 backdrop-blur-md flex justify-end gap-3 shrink-0">
                    <button type="button" onClick={onClose} disabled={isSaving} className="px-6 py-3 rounded-xl font-bold text-xs text-gray-300 hover:bg-white/10 transition-all disabled:opacity-50">Cancel</button>
                    <button 
                        type="button" 
                        onClick={submit} 
                        disabled={isSaving} 
                        className={`px-8 py-3 rounded-xl font-bold text-xs bg-orange-500 hover:bg-orange-600 text-white shadow-lg shadow-orange-500/30 transition-all flex items-center gap-2 ${isSaving ? 'opacity-70 cursor-not-allowed pointer-events-none' : ''}`}
                    >
                        {isSaving ? (
                            <>
                                <span className="animate-spin"><IconRefreshCw sz={16}/></span>
                                <span>Saving...</span>
                            </>
                        ) : (
                            <>
                                <IconCheck sz={16}/>
                                <span>{vehicleToEdit ? 'Save Changes' : '+ Add Vehicle'}</span>
                            </>
                        )}
                    </button>
                </div>

            </div>

            {/* AI DYNAMIC PRICING SUGGESTION MODAL POP-UP */}
            {suggestData && (
                <div className="fixed inset-0 z-[10000] flex items-center justify-center bg-black/80 backdrop-blur-md p-4 animate-fade-in" onClick={() => setSuggestData(null)}>
                    <div className="bg-[#0d0e17] border border-orange-500/50 rounded-3xl p-6 max-w-md w-full shadow-[0_25px_60px_rgba(249,115,22,0.35)] animate-scale-up" onClick={e => e.stopPropagation()}>
                        <div className="flex justify-between items-center pb-3 border-b border-white/10 mb-4">
                            <div className="flex items-center gap-2 text-orange-400 font-extrabold text-sm uppercase tracking-wider">
                                <IconZap sz={18}/> AI Dynamic Pricing Suggestion
                            </div>
                            <button onClick={() => setSuggestData(null)} className="p-1 rounded-full hover:bg-white/10 text-white"><IconX sz={16}/></button>
                        </div>
                        
                        <div className="text-xs text-gray-300 mb-4 leading-relaxed bg-orange-500/10 border border-orange-500/20 p-3 rounded-2xl">
                            {suggestData.reasoning}
                        </div>

                        <div className="space-y-2.5 text-xs mb-5">
                            <div className="flex justify-between text-gray-400">
                                <span>Base Vehicle Type Rate:</span>
                                <span className="font-bold text-white">₱{suggestData.baseRate.toLocaleString()}</span>
                            </div>
                            <div className="flex justify-between text-gray-400">
                                <span>Peak Demand Index (+12%):</span>
                                <span className="font-bold text-emerald-400">+₱{suggestData.demandFactor.toLocaleString()}</span>
                            </div>
                            <div className="flex justify-between text-gray-400">
                                <span>Inflation & Economy Adjustment (+5%):</span>
                                <span className="font-bold text-emerald-400">+₱{suggestData.inflationFactor.toLocaleString()}</span>
                            </div>
                            <div className="pt-2.5 border-t border-white/10 flex justify-between items-center">
                                <span className="font-extrabold text-white text-xs">Suggested Daily Rate:</span>
                                <span className="font-black text-orange-400 text-lg">₱{suggestData.calculatedRate.toLocaleString()} <span className="text-[10px] text-gray-400 font-normal">/ day</span></span>
                            </div>
                            <div className="flex justify-between items-center">
                                <span className="font-extrabold text-white text-xs">Suggested Rate + Driver:</span>
                                <span className="font-black text-emerald-400 text-base">₱{suggestData.calculatedRateWithDriver.toLocaleString()} <span className="text-[10px] text-gray-400 font-normal">/ day</span></span>
                            </div>
                        </div>

                        <div className="flex gap-3">
                            <button type="button" onClick={() => setSuggestData(null)} className="w-1/2 py-2.5 rounded-xl font-bold text-xs bg-white/10 hover:bg-white/20 text-gray-300 transition-all">Cancel</button>
                            <button type="button" onClick={applySuggest} className="w-1/2 py-2.5 rounded-xl font-bold text-xs bg-orange-500 hover:bg-orange-600 text-white shadow-lg shadow-orange-500/30 transition-all flex items-center justify-center gap-1.5">
                                <IconCheck sz={16}/> Apply Price
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

// ─── VEHICLE INSPECTOR DRAWER ───────────────────────────────────────────────
const VehicleDrawer = ({ v, onClose, onRefresh, onEdit, onDelete, onShowQr, onPreviewDoc }) => {
    if(!v) return null;
    const st = getStatusStyle(v.status);
    
    return (
        <div className="fixed inset-0 bg-black/70 backdrop-blur-md z-[9999] flex items-center justify-center p-4 sm:p-6 animate-fade-in" onClick={onClose}>
            <div className="w-full max-w-2xl max-h-[85vh] bg-[#0d0e17]/85 backdrop-blur-2xl border border-white/20 rounded-3xl shadow-[0_25px_60px_rgba(0,0,0,0.8)] flex flex-col overflow-hidden relative animate-scale-up" onClick={e => e.stopPropagation()}>
                
                {/* Header Image & Vehicle Details Banner */}
                <div className="h-44 relative shrink-0">
                    <VehicleImageComponent src={v.image} alt={v.plateNumber} className="w-full h-full object-cover" iconSize={64} />
                    <div className="absolute inset-0 bg-gradient-to-t from-[#0d0e17] via-black/40 to-transparent pointer-events-none"></div>
                    
                    {/* Close Button */}
                    <button onClick={onClose} className="absolute top-4 right-4 p-2 rounded-full bg-black/60 text-white hover:bg-black/90 backdrop-blur border border-white/20 transition-all hover:scale-110"><IconX sz={18}/></button>
                    
                    {/* Title Info */}
                    <div className="absolute bottom-4 left-6 right-6">
                        <div className="flex justify-between items-end">
                            <div>
                                <div className="text-orange-400 text-xs font-bold uppercase tracking-wider mb-1 flex items-center gap-1">
                                    <IconCar sz={14} /> {v.brand} {v.model} • {v.type}
                                </div>
                                <div className="text-3xl font-black text-white drop-shadow-lg tracking-tight">{v.plateNumber}</div>
                            </div>
                            <span className={`px-4 py-1.5 rounded-full text-xs font-bold uppercase backdrop-blur-md border border-white/20 shadow-lg ${st.bg} ${st.text}`}>
                                {v.status}
                            </span>
                        </div>
                    </div>
                </div>

                {/* Modal Body (Scrollable) */}
                <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6 custom-scrollbar">
                    
                    {/* Telemetry Cards Grid (Frosted Glass Style) */}
                    <div className="grid grid-cols-3 gap-3">
                        <div className="p-3.5 rounded-2xl bg-white/5 backdrop-blur-md border border-white/10 shadow-lg">
                            <div className="text-[10px] text-[var(--text-muted)] font-semibold uppercase tracking-wider flex items-center gap-1 mb-1.5"><IconGauge sz={12}/> Odometer</div>
                            <div className="text-lg font-bold text-white">{parseInt(v.odometerKm || 0).toLocaleString()} <span className="text-xs text-[var(--text-muted)] font-normal">km</span></div>
                        </div>
                        <div className="p-3.5 rounded-2xl bg-white/5 backdrop-blur-md border border-white/10 shadow-lg">
                            <div className="text-[10px] text-[var(--text-muted)] font-semibold uppercase tracking-wider flex items-center gap-1 mb-1.5"><IconActivity sz={12}/> Speed</div>
                            <div className="text-lg font-bold text-white">{v.currentSpeedKmh || 0} <span className="text-xs text-[var(--text-muted)] font-normal">km/h</span></div>
                        </div>
                        <div className="p-3.5 rounded-2xl bg-white/5 backdrop-blur-md border border-white/10 shadow-lg">
                            <div className="text-[10px] text-[var(--text-muted)] font-semibold uppercase tracking-wider flex items-center gap-1 mb-1.5"><IconShield sz={12}/> Health Score</div>
                            <div className={`text-lg font-bold ${(v.healthScore ?? 98) > 80 ? 'text-emerald-400' : (v.healthScore ?? 98) > 50 ? 'text-amber-400' : 'text-red-400'}`}>{v.healthScore ?? 98}/100</div>
                        </div>
                    </div>

                    {/* Toll RFID Wallets */}
                    <div>
                        <h3 className="text-xs font-bold text-[var(--text-secondary)] uppercase tracking-wider mb-3">Toll RFID Wallets</h3>
                        <div className="grid grid-cols-2 gap-3">
                            <div className={`p-4 rounded-2xl backdrop-blur-md border shadow-lg flex flex-col ${(v.rfidBalancePHP || 0) < 200 ? 'bg-red-500/10 border-red-500/30' : 'bg-white/5 border-white/10'}`}>
                                <span className="text-xs font-semibold text-[var(--text-muted)] mb-1">Autosweep</span>
                                <span className={`text-xl font-bold ${(v.rfidBalancePHP || 0) < 200 ? 'text-red-400' : 'text-blue-400'}`}>{formatPHP(v.rfidBalancePHP || 0)}</span>
                                {(v.rfidBalancePHP || 0) < 200 && <span className="text-[10px] text-red-400 mt-1 flex items-center gap-1"><IconAlertTriangle sz={10}/> Low Balance</span>}
                            </div>
                            <div className="p-4 rounded-2xl border border-white/5 bg-white/5 opacity-50 grayscale cursor-not-allowed">
                                <span className="text-xs font-semibold text-[var(--text-muted)] mb-1">Easytrip</span>
                                <span className="text-xl font-bold text-[var(--text-muted)]">--</span>
                                <span className="text-[10px] text-[var(--text-muted)] mt-1">Not Enrolled</span>
                            </div>
                        </div>
                    </div>

                    {/* Legal Expiry & Vehicle Registration Documents */}
                    <div className="p-4 rounded-2xl bg-white/5 backdrop-blur-md border border-white/10">
                        <h3 className="text-xs font-bold text-[var(--text-secondary)] uppercase tracking-wider mb-3 flex justify-between items-center">
                            Legal Expiry & Documents <IconFileText sz={14}/>
                        </h3>
                        <div className="space-y-3">
                            {/* LTO Registration (OR/CR) */}
                            <div className="flex justify-between items-center text-sm p-3 rounded-xl bg-white/5 border border-white/5">
                                <div>
                                    <div className="text-white font-semibold text-xs flex items-center gap-1.5">
                                        <IconFileText sz={14} className="text-orange-400"/> LTO Registration (OR/CR)
                                    </div>
                                    <div className="text-[10px] text-gray-400 mt-0.5">Expiry: <span className="text-white font-bold">{v.documents?.ltoRegistrationExpiry || 'N/A'}</span></div>
                                </div>
                                {(v.orCrUrl || v.or_cr_url || v.documents?.orCrUrl || v.documents?.or_cr_url) ? (
                                    <button onClick={() => onPreviewDoc && onPreviewDoc({ title: `${v.brand} ${v.model} (${v.plateNumber}) - LTO OR/CR Document`, url: v.orCrUrl || v.or_cr_url || v.documents?.orCrUrl || v.documents?.or_cr_url })} className="px-3.5 py-1.5 rounded-xl bg-orange-500/20 hover:bg-orange-500/30 text-orange-400 border border-orange-500/30 text-xs font-bold transition-all flex items-center gap-1.5 shadow-md">
                                        <IconEye sz={12}/> View OR/CR
                                    </button>
                                ) : (
                                    <span className="text-[10px] text-gray-500 italic bg-white/5 px-2.5 py-1 rounded-lg border border-white/5">No Document Uploaded</span>
                                )}
                            </div>

                            {/* CTPL Insurance */}
                            <div className="flex justify-between items-center text-sm p-3 rounded-xl bg-white/5 border border-white/5">
                                <div>
                                    <div className="text-white font-semibold text-xs flex items-center gap-1.5">
                                        <IconShield sz={14} className="text-emerald-400"/> CTPL Insurance Policy
                                    </div>
                                    <div className="text-[10px] text-gray-400 mt-0.5">Expiry: <span className="text-white font-bold">{v.documents?.insuranceExpiry || 'N/A'}</span></div>
                                </div>
                                {(v.insuranceUrl || v.insurance_url || v.documents?.insuranceUrl || v.documents?.insurance_url) ? (
                                    <button onClick={() => onPreviewDoc && onPreviewDoc({ title: `${v.brand} ${v.model} (${v.plateNumber}) - CTPL Insurance Policy`, url: v.insuranceUrl || v.insurance_url || v.documents?.insuranceUrl || v.documents?.insurance_url })} className="px-3.5 py-1.5 rounded-xl bg-emerald-500/20 hover:bg-emerald-500/30 text-emerald-400 border border-emerald-500/30 text-xs font-bold transition-all flex items-center gap-1.5 shadow-md">
                                        <IconEye sz={12}/> View Policy
                                    </button>
                                ) : (
                                    <span className="text-[10px] text-gray-500 italic bg-white/5 px-2.5 py-1 rounded-lg border border-white/5">No Policy Uploaded</span>
                                )}
                            </div>
                        </div>
                    </div>

                </div>
                
                {/* Footer Action Bar */}
                <div className="p-4 border-t border-white/10 bg-white/5 backdrop-blur-xl grid grid-cols-3 gap-3 shrink-0">
                    <button onClick={() => onEdit && onEdit(v)} className="py-2.5 rounded-xl font-bold text-xs bg-orange-500 hover:bg-orange-600 text-white shadow-lg shadow-orange-500/30 transition-all flex items-center justify-center gap-1.5"><IconEdit2 sz={14}/> Edit Specs</button>
                    <button onClick={() => onDelete && onDelete(v.id || v.vehicle_id)} className="py-2.5 rounded-xl font-bold text-xs bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/30 transition-all flex items-center justify-center gap-1.5"><IconTrash2 sz={14}/> Delete</button>
                    <button onClick={() => onShowQr && onShowQr(v)} className="py-2.5 rounded-xl font-bold text-xs bg-white/10 hover:bg-white/20 text-white border border-white/10 transition-all flex items-center justify-center gap-1.5"><IconQrCode sz={14}/> Handover QR</button>
                </div>

            </div>
        </div>
    );
};


// ─── MAIN APP COMPONENT ─────────────────────────────────────────────────────
const FleetOverview = () => {
    // Global State
    const [isDark, setIsDark] = useState(document.documentElement.getAttribute('data-theme') !== 'light');
    const [vehicles, setVehicles] = useState([]);
    const [loading, setLoading] = useState(true);
    
    // View State
    const [viewMode, setViewMode] = useState('grid'); // grid, table
    const [filterStatus, setFilterStatus] = useState('all'); // all, available, rented, maintenance
    const [searchTerm, setSearchTerm] = useState('');
    const [showWeatherBanner, setShowWeatherBanner] = useState(true);
    
    // Modals & Drawers
    const [selectedVehicle, setSelectedVehicle] = useState(null);
    const [showAddModal, setShowAddModal] = useState(false);
    const [editingVehicle, setEditingVehicle] = useState(null);
    const [qrVehicle, setQrVehicle] = useState(null);
    const [previewDoc, setPreviewDoc] = useState(null);
    const [toast, setToast] = useState(null);

    const showToast = (message, type = 'success') => {
        setToast({ message, type });
        setTimeout(() => setToast(null), 4000);
    };

    // Initial Fetch
    const fetchFleet = useCallback(async () => {
        try {
            const res = await fetch(`${API}/api/vehicles?_t=${Date.now()}`, { 
                headers: {
                    ...HEADERS,
                    'Cache-Control': 'no-cache, no-store, must-revalidate',
                    'Pragma': 'no-cache'
                },
                cache: 'no-store'
            });
            if (res.ok) {
                const data = await res.json();
                const list = data.map((v, i) => {
                    try {
                        return normVehicle(v, i);
                    } catch (err) {
                        console.error("Error normalizing vehicle:", v, err);
                        return {
                            id: v.vehicle_id || v.id || (i + 1),
                            vehicle_id: v.vehicle_id || v.id || (i + 1),
                            plateNumber: v.plate_no || v.plateNo || 'NO-PLATE',
                            brand: v.brand || 'Vehicle',
                            model: v.model || '',
                            status: 'available',
                            dailyRatePHP: v.rate_per_day || 2500,
                            odometerKm: v.odometer_km || 0,
                            documents: { ltoRegistrationExpiry: '2026-10-15', insuranceExpiry: '2026-11-20' },
                            coordinates: [14.5995, 120.9842]
                        };
                    }
                });
                window.fleetVehiclesCache = list;
                setVehicles(list);
                setSelectedVehicle(prev => {
                    if (!prev) return null;
                    const prevId = String(prev.vehicle_id || prev.id);
                    const fresh = list.find(item => String(item.vehicle_id || item.id) === prevId);
                    return fresh || prev;
                });
                return list;
            }
        } catch (e) {
            console.error("Fetch error:", e);
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        const hideSplash = () => {
            const splash = document.getElementById('boot-splash');
            if (splash) splash.classList.add('hide');
        };
        fetchFleet().finally(hideSplash);
        const splashTimer = setTimeout(hideSplash, 600);
        // Setup theme & bridge listeners if triggered from C# WinForms host
        window.setFleetTheme = (theme) => {
            setIsDark(theme === 'dark');
            document.documentElement.setAttribute('data-theme', theme);
        };
        window.refreshFleetData = fetchFleet;

        const calcBearing = (lat1, lng1, lat2, lng2) => {
            const toRad = Math.PI / 180;
            const dLng = (lng2 - lng1) * toRad;
            const phi1 = lat1 * toRad;
            const phi2 = lat2 * toRad;
            const y = Math.sin(dLng) * Math.cos(phi2);
            const x = Math.cos(phi1) * Math.sin(phi2) - Math.sin(phi1) * Math.cos(phi2) * Math.cos(dLng);
            const brng = Math.atan2(y, x) * (180 / Math.PI);
            return Math.round((brng + 360) % 360);
        };

        // Real API & Firebase Telematics GPS Live Update Listener
        // ONLY moves vehicle and rotates bearing when genuine coordinates arrive from API / GPS sensor
        window.liveUpdateGPS = (vid, lat, lng, speed, heading) => {
            setVehicles(prev => prev.map(v => {
                if (String(v.id) === String(vid) || String(v.vehicle_id) === String(vid)) {
                    const oldLat = v.coordinates ? v.coordinates[0] : lat;
                    const oldLng = v.coordinates ? v.coordinates[1] : lng;

                    // Calculate forward bearing angle only if location actually shifted
                    let newHeading = heading;
                    if (newHeading === undefined || newHeading === null) {
                        if (Math.abs(lat - oldLat) > 0.00001 || Math.abs(lng - oldLng) > 0.00001) {
                            newHeading = calcBearing(oldLat, oldLng, lat, lng);
                        } else {
                            newHeading = v.heading || 0;
                        }
                    }

                    const R = 6371; // Earth radius in km
                    const dLat = (lat - oldLat) * Math.PI / 180;
                    const dLng = (lng - oldLng) * Math.PI / 180;
                    const a = Math.sin(dLat/2) * Math.sin(dLat/2) +
                              Math.cos(oldLat * Math.PI / 180) * Math.cos(lat * Math.PI / 180) *
                              Math.sin(dLng/2) * Math.sin(dLng/2);
                    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));
                    const distKm = R * c;
                    
                    const addKm = (distKm > 0.005 && distKm < 10) ? distKm : 0;
                    const newOdo = Math.round(((v.odometerKm || 0) + addKm) * 10) / 10;

                    const updated = {
                        ...v,
                        coordinates: [lat, lng],
                        heading: newHeading,
                        bearing: newHeading,
                        currentSpeedKmh: speed !== undefined ? speed : (distKm > 0.001 ? 35 : 0),
                        odometerKm: newOdo,
                    };

                    setSelectedVehicle(curr => (curr && (String(curr.id) === String(vid) || String(curr.vehicle_id) === String(vid))) ? updated : curr);

                    return updated;
                }
                return v;
            }));
        };

        // Auto-refresh every 30s
        const int = setInterval(fetchFleet, 30000);

        return () => {
            clearTimeout(splashTimer);
            clearInterval(int);
        };
    }, [fetchFleet]);

    const handleAddUnit = () => {
        setEditingVehicle(null);
        setShowAddModal(true);
    };

    const handleEditUnit = async (vehicleOrId) => {
        let vObj = null;
        if (typeof vehicleOrId === 'object' && vehicleOrId !== null) {
            vObj = vehicleOrId;
        } else if (vehicleOrId !== undefined && vehicleOrId !== null) {
            vObj = vehicles.find(item => String(item.vehicle_id) === String(vehicleOrId) || String(item.vehicleId) === String(vehicleOrId) || String(item.VehicleId) === String(vehicleOrId) || String(item.id) === String(vehicleOrId));
        }

        let rawId = vObj?.vehicle_id || vObj?.vehicleId || vObj?.VehicleId || vObj?.id || vObj?.Id || vObj?.ID;
        if ((rawId === undefined || rawId === null || rawId === '' || isNaN(parseInt(rawId)) || parseInt(rawId) <= 0) && typeof vObj === 'object' && vObj !== null) {
            for (const key of Object.keys(vObj)) {
                if (key.toLowerCase().includes('id') && !key.toLowerCase().includes('url')) {
                    const val = parseInt(vObj[key]);
                    if (!isNaN(val) && val > 0) {
                        rawId = val;
                        break;
                    }
                }
            }
        }

        const parsedId = (rawId !== undefined && rawId !== null && !isNaN(parseInt(rawId))) ? parseInt(rawId) : null;
        const vId = (parsedId && parsedId > 0) ? parsedId : null;

        if (vId) {
            try {
                const res = await fetch(`${API}/api/vehicles/${vId}?_t=${Date.now()}`, { 
                    headers: {
                        ...HEADERS,
                        'Cache-Control': 'no-cache, no-store, must-revalidate',
                        'Pragma': 'no-cache'
                    },
                    cache: 'no-store'
                });
                if (res.ok) {
                    const fullData = await res.json();
                    vObj = { ...vObj, ...normVehicle(fullData, 0), vehicle_id: vId, vehicleId: vId, VehicleId: vId, id: vId };
                }
            } catch (e) {
                console.warn('[DriveAndGo] Failed to fetch full vehicle details for edit, using cached data:', e);
            }
        }

        setEditingVehicle(vObj || null);
        setShowAddModal(true);
    };

    const handleDeleteUnit = async (vOrId) => {
        const vId = typeof vOrId === 'object' 
            ? (vOrId?.vehicle_id || vOrId?.vehicleId || vOrId?.VehicleId || vOrId?.id || vOrId?.Id || vOrId?.ID) 
            : vOrId;
        if (!vId) {
            alert("Cannot delete vehicle: Vehicle ID could not be resolved.");
            return;
        }
        if (!confirm("Are you sure you want to delete this vehicle?")) return;
        try {
            const res = await fetch(`${API}/api/vehicles/${vId}`, { method: 'DELETE', headers: HEADERS });
            if (res.ok || res.status === 204) {
                setSelectedVehicle(null);
                showToast("Vehicle deleted successfully!", 'success');
                fetchFleet();
            } else {
                const errData = await res.json().catch(() => ({}));
                alert(errData.message || errData.Message || "Failed to delete vehicle.");
            }
        } catch (e) {
            console.error(e);
            alert("Connection error while deleting vehicle.");
        }
    };

    const toggleTheme = () => {
        const nu = !isDark;
        setIsDark(nu);
        document.documentElement.setAttribute('data-theme', nu ? 'dark' : 'light');
    };

    // Derived Data
    const filteredVehicles = useMemo(() => {
        return vehicles.filter(v => {
            const st = (v.status || '').toLowerCase();
            const mStat = filterStatus === 'all' ||
                (filterStatus === 'rented' ? (st === 'rented' || st === 'in-use') : st === filterStatus);
            const term = (searchTerm || '').toLowerCase();
            const mSearch = !term ||
                (v.plateNumber || '').toLowerCase().includes(term) ||
                (v.brand || '').toLowerCase().includes(term) ||
                (v.model || '').toLowerCase().includes(term);
            return mStat && mSearch;
        });
    }, [vehicles, filterStatus, searchTerm]);

    const stats = useMemo(() => {
        let avail=0, rent=0, maint=0, sumHealth=0, lowRfid=0;
        vehicles.forEach(v => {
            const st = (v.status || '').toLowerCase();
            if (st === 'available') avail++;
            else if (st === 'rented' || st === 'in-use') rent++;
            else if (st === 'maintenance') maint++;
            
            sumHealth += (v.healthScore || 100);
            if ((v.rfidBalancePHP || 0) < 200) lowRfid++;
        });
        const avgHealth = vehicles.length ? Math.round(sumHealth / vehicles.length) : 0;
        return { total: vehicles.length, avail, rent, maint, avgHealth, lowRfid };
    }, [vehicles]);


    return (
        <div className="flex h-screen w-screen overflow-hidden bg-[var(--bg-primary)] text-[var(--text-primary)]">
            
            {/* LEFT COLUMN: KPI Stats, Search & Filters, 3-Col Vehicle Cards (~58% width) */}
            <div className="flex-1 lg:max-w-[58%] h-full overflow-y-auto p-5 flex flex-col gap-5 custom-scrollbar">
                
                {/* KPI STAT CARDS BENTO GRID (Clean 4-Col Layout + Status Bar) */}
                <div className="flex flex-col gap-3">
                    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                        {/* 1. Total Fleet */}
                        <FleetKpiCard3D
                            icon={<IconCar sz={18} />}
                            title="Total Fleet"
                            value={stats.total.toLocaleString()}
                            tag="Live"
                            tagClass="text-emerald-400 bg-emerald-500/10 border-emerald-500/20"
                            glowColor="rgba(249, 115, 22, 0.28)"
                            borderHoverColor="border-orange-500/50"
                            valueColor="text-[var(--text-primary)]"
                            onClick={() => setFilterStatus('all')}
                        />

                        {/* 2. Available */}
                        <FleetKpiCard3D
                            icon={<IconCheck sz={18} />}
                            title="Available"
                            value={stats.avail.toLocaleString()}
                            tag="Ready"
                            tagClass="text-emerald-400 bg-emerald-500/10 border-emerald-500/20"
                            glowColor="rgba(52, 211, 153, 0.28)"
                            borderHoverColor="border-emerald-500/50"
                            valueColor="text-emerald-400"
                            onClick={() => setFilterStatus('available')}
                        />

                        {/* 3. Rented */}
                        <FleetKpiCard3D
                            icon={<IconZap sz={18} />}
                            title="On Rent"
                            value={stats.rent.toLocaleString()}
                            tag="Active"
                            tagClass="text-amber-400 bg-amber-500/10 border-amber-500/20"
                            glowColor="rgba(251, 191, 36, 0.28)"
                            borderHoverColor="border-amber-500/50"
                            valueColor="text-amber-400"
                            onClick={() => setFilterStatus('rented')}
                        />

                        {/* 4. Maintenance */}
                        <FleetKpiCard3D
                            icon={<IconSettings sz={18} />}
                            title="Maintenance"
                            value={stats.maint.toLocaleString()}
                            tag="In Shop"
                            tagClass="text-gray-400 bg-white/5 border-white/10"
                            glowColor="rgba(192, 132, 252, 0.25)"
                            borderHoverColor="border-purple-500/50"
                            valueColor="text-[var(--text-primary)]"
                            onClick={() => setFilterStatus('maintenance')}
                        />
                    </div>

                    {/* Fleet Health & RFID Status Bar */}
                    <div className="grid grid-cols-2 gap-3">
                        <div className="p-3 px-4 rounded-xl bg-[var(--bg-card)] border border-[var(--border-color)] flex items-center justify-between shadow-sm hover:border-emerald-500/30 transition-colors">
                            <div className="flex items-center gap-2">
                                <div className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></div>
                                <span className="text-xs text-[var(--text-muted)] font-bold uppercase tracking-wider">Avg Fleet Health</span>
                            </div>
                            <span className="text-sm font-black text-emerald-400">{stats.avgHealth}%</span>
                        </div>
                        <div className="p-3 px-4 rounded-xl bg-orange-500/15 border border-orange-500/40 flex items-center justify-between shadow-sm hover:bg-orange-500/25 transition-colors">
                            <div className="flex items-center gap-2 text-orange-400">
                                <IconAlertTriangle sz={14} />
                                <span className="text-xs font-bold uppercase tracking-wider">Low RFID Balance</span>
                            </div>
                            <span className="text-sm font-black text-orange-500">{stats.lowRfid} Vehicles</span>
                        </div>
                    </div>
                </div>

                {/* CONTROL & FILTER BAR (Prominent Search + Status Combobox) */}
                <div className="flex flex-col sm:flex-row justify-between items-center gap-3 bg-[var(--bg-card)] p-2.5 px-4 rounded-2xl border border-[var(--border-color)] shadow-[var(--shadow-card)]">
                    {/* PROMINENT EXPANDED SEARCH BAR */}
                    <div className="flex items-center gap-2.5 flex-1 w-full bg-[var(--bg-tertiary)] px-3.5 py-2 rounded-xl border border-[var(--border-color)] focus-within:border-orange-500/60 transition-colors">
                        <IconSearch sz={18} c="text-orange-500 shrink-0" />
                        <input 
                            type="text" 
                            placeholder="Search in fleet..." 
                            value={searchTerm}
                            onChange={e=>setSearchTerm(e.target.value)}
                            className="bg-transparent border-none outline-none text-sm w-full text-[var(--text-primary)] placeholder-[var(--text-muted)] font-medium"
                        />
                        {searchTerm && (
                            <button onClick={() => setSearchTerm('')} className="text-xs text-[var(--text-muted)] hover:text-white px-1 font-bold">✕</button>
                        )}
                    </div>

                    {/* RIGHT CONTROLS: STATUS COMBOBOX + VIEW TOGGLE + ADD UNIT */}
                    <div className="flex items-center gap-2.5 w-full sm:w-auto shrink-0 justify-between sm:justify-end">
                        {/* Interactive Brand Orange Status Combobox */}
                        <StatusCombobox value={filterStatus} onChange={setFilterStatus} />

                        <div className="h-6 w-px bg-[var(--border-color)] hidden sm:block"></div>

                        {/* View Mode Icons */}
                        <div className="flex gap-1 shrink-0 bg-[var(--bg-tertiary)] p-1 rounded-xl border border-[var(--border-color)]">
                            <button onClick={()=>setViewMode('grid')} className={`p-1.5 rounded-lg transition-all ${viewMode==='grid'?'bg-orange-500 text-white shadow-md shadow-orange-500/30':'text-[var(--text-muted)] hover:text-[var(--text-primary)]'}`} title="Grid View"><IconGrid sz={16}/></button>
                            <button onClick={()=>setViewMode('table')} className={`p-1.5 rounded-lg transition-all ${viewMode==='table'?'bg-orange-500 text-white shadow-md shadow-orange-500/30':'text-[var(--text-muted)] hover:text-[var(--text-primary)]'}`} title="List View"><IconList sz={16}/></button>
                        </div>

                        {/* Add Unit Button */}
                        <button onClick={() => setShowAddModal(true)} className="bg-orange-500 hover:bg-orange-600 text-white px-4 py-2 rounded-xl text-xs font-bold shadow-lg shadow-orange-500/20 flex items-center gap-2 shrink-0 transition-colors">
                            <IconPlus sz={16} /> Add Unit
                        </button>
                    </div>
                </div>

                {/* VEHICLE GRID (LARGER DASHBOARD CARDS) */}
                {loading && vehicles.length === 0 ? (
                    <div className="flex justify-center items-center py-20 text-[var(--text-muted)]"><IconRefreshCw sz={32} c="animate-spin" /></div>
                ) : viewMode === 'grid' ? (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 pb-6">
                        {filteredVehicles.map(v => (
                            <VehicleCard key={v.id} v={v} onClick={setSelectedVehicle} onMapFocus={setSelectedVehicle} />
                        ))}
                        {filteredVehicles.length === 0 && <div className="col-span-full py-16 text-center text-[var(--text-muted)]">No vehicles match filters.</div>}
                    </div>
                ) : (
                    <VehicleTable vehicles={filteredVehicles} onSelect={setSelectedVehicle} />
                )}

            </div>

            {/* RIGHT COLUMN: FULL-HEIGHT DARK LEAFLET MAP (~42% width) */}
            <div className="hidden lg:flex flex-col flex-1 h-full p-5 pl-0 relative gap-3">
                {showWeatherBanner && (
                    <div className="p-3 rounded-2xl bg-amber-950/90 border border-amber-500/40 backdrop-blur-md flex items-center justify-between text-amber-300 text-xs font-semibold shadow-xl shrink-0 animate-fade-in">
                        <div className="flex items-center gap-2">
                            <IconAlertTriangle sz={16} c="text-amber-400 shrink-0" />
                            <span>Active Weather / Flood Risk Alert in Metro Manila.</span>
                        </div>
                        <button onClick={() => setShowWeatherBanner(false)} className="p-1 rounded-lg hover:bg-white/10 text-amber-200"><IconX sz={14}/></button>
                    </div>
                )}
                
                <div className="w-full flex-1 rounded-3xl overflow-hidden border border-[var(--border-color)] shadow-2xl relative">
                    <LeafletMapComponent vehicles={vehicles} selectedVehicleId={selectedVehicle?.id} onSelectVehicle={setSelectedVehicle} isDark={isDark} />
                </div>
            </div>

            {/* VEHICLE DETAILS OVERLAY DRAWER */}
            <VehicleDrawer v={selectedVehicle} onClose={()=>setSelectedVehicle(null)} onRefresh={fetchFleet} onEdit={handleEditUnit} onDelete={handleDeleteUnit} onShowQr={setQrVehicle} onPreviewDoc={setPreviewDoc} />

            {/* MODALS */}
            {showAddModal && (
                <AddVehicleModal 
                    vehicleToEdit={editingVehicle} 
                    onClose={() => { setShowAddModal(false); setEditingVehicle(null); }} 
                    onSave={async (successMsg, updatedVehicle) => { 
                        setShowAddModal(false); 
                        setEditingVehicle(null); 
                        showToast(successMsg || "Vehicle updated successfully!", 'success');

                        // Instant Optimistic Local State Update (0ms immediate refresh!)
                        if (updatedVehicle) {
                            const uId = String(updatedVehicle.id || updatedVehicle.vehicle_id || updatedVehicle.vehicleId || '');
                            const uPlate = (updatedVehicle.plateNumber || updatedVehicle.plate_no || '').trim().toLowerCase();

                            setVehicles(prev => {
                                const isMatch = v => (uId && String(v.id || v.vehicle_id || v.vehicleId || '') === uId) || 
                                                     (uPlate && (v.plateNumber || v.plate_no || '').trim().toLowerCase() === uPlate);
                                const exists = prev.some(isMatch);
                                if (exists) {
                                    return prev.map(v => isMatch(v) ? { ...v, ...updatedVehicle } : v);
                                }
                                return [updatedVehicle, ...prev];
                            });

                            setSelectedVehicle(prev => {
                                if (!prev) return null;
                                const isMatch = (uId && String(prev.id || prev.vehicle_id || prev.vehicleId || '') === uId) || 
                                                (uPlate && (prev.plateNumber || prev.plate_no || '').trim().toLowerCase() === uPlate);
                                return isMatch ? { ...prev, ...updatedVehicle } : prev;
                            });
                        }

                        // Re-fetch fresh live data from database with no-cache
                        await fetchFleet(); 
                    }} 
                />
            )}
            {qrVehicle && <QrCodeModal vehicle={qrVehicle} onClose={() => setQrVehicle(null)} />}
            {previewDoc && <DocumentPreviewModal title={previewDoc.title} url={previewDoc.url} onClose={() => setPreviewDoc(null)} />}

            {/* FLOATING GLASS TOAST NOTIFICATION */}
            {toast && (
                <div className={`fixed top-6 right-6 z-[100000] px-5 py-3.5 rounded-2xl backdrop-blur-xl border shadow-2xl flex items-center gap-3 animate-slide-in text-xs font-bold ${
                    toast.type === 'error' 
                        ? 'bg-red-950/90 border-red-500/40 text-red-200 shadow-red-500/20' 
                        : 'bg-emerald-950/90 border-emerald-500/40 text-emerald-200 shadow-emerald-500/20'
                }`}>
                    {toast.type === 'error' ? <IconAlertTriangle sz={18} className="text-red-400 shrink-0"/> : <IconCheck sz={18} className="text-emerald-400 shrink-0"/>}
                    <span>{toast.message}</span>
                    <button onClick={() => setToast(null)} className="ml-2 hover:opacity-75 text-gray-400 hover:text-white"><IconX sz={14}/></button>
                </div>
            )}

        </div>
    );
};

class ErrorBoundary extends React.Component {
    constructor(props) {
        super(props);
        this.state = { hasError: false, error: null };
    }
    static getDerivedStateFromError(error) {
        return { hasError: true, error };
    }
    componentDidCatch(error, errorInfo) {
        console.error("FleetOverview ErrorBoundary Caught:", error, errorInfo);
        window.__lastReactError = { error: String(error), info: errorInfo };
    }
    render() {
        if (this.state.hasError) {
            return (
                <div className="min-h-screen bg-[#07070e] text-white flex flex-col items-center justify-center p-8 text-center">
                    <div className="p-8 rounded-3xl bg-red-950/40 border border-red-500/30 backdrop-blur-2xl max-w-lg shadow-2xl flex flex-col items-center gap-4">
                        <div className="w-14 h-14 rounded-2xl bg-red-500/20 border border-red-500/40 flex items-center justify-center text-red-400 text-2xl font-black">!</div>
                        <h2 className="text-xl font-black text-white">Something went wrong</h2>
                        <p className="text-xs text-gray-400 font-mono bg-black/50 p-3 rounded-xl border border-white/10 text-left w-full overflow-auto max-h-40">
                            {String(this.state.error?.message || this.state.error)}
                        </p>
                        <button onClick={() => { this.setState({ hasError: false, error: null }); window.location.reload(); }} className="px-6 py-3 rounded-xl bg-orange-500 hover:bg-orange-600 font-bold text-xs text-white shadow-lg shadow-orange-500/30 transition-all">
                            Reload Operations Center
                        </button>
                    </div>
                </div>
            );
        }
        return this.props.children;
    }
}

// Mount
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
    <ErrorBoundary>
        <FleetOverview />
    </ErrorBoundary>
);
