using System;
using System.Text;

namespace DriveAndGo_Admin.Panels
{
    internal static class AccountsHtmlGenerator
    {
        public static string Build(string apiBaseUrl, bool dark)
        {
            var sb = new StringBuilder(65536);

            sb.Append("<!DOCTYPE html>");
            sb.Append("<html lang='en'>");
            sb.Append("<head>");
            sb.Append("<meta charset='UTF-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<title>DriveAndGo Accounts Management</title>");
            
            // Fonts & Tailwind
            sb.Append("<link rel='preconnect' href='https://fonts.googleapis.com'>");
            sb.Append("<link rel='preconnect' href='https://fonts.gstatic.com' crossorigin>");
            sb.Append("<link href='https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700;800&display=swap' rel='stylesheet'>");
            sb.Append("<script src='https://cdn.tailwindcss.com'></script>");
            
            // React, SignalR, & Babel
            sb.Append("<script src='https://unpkg.com/react@18/umd/react.production.min.js'></script>");
            sb.Append("<script src='https://unpkg.com/react-dom@18/umd/react-dom.production.min.js'></script>");
            sb.Append("<script src='https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/6.0.1/signalr.min.js'></script>");
            sb.Append("<script src='https://unpkg.com/@babel/standalone/babel.min.js'></script>");
            
            // FontAwesome Icons
            sb.Append("<link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css'>");

            // Tailwind config for brand identity
            sb.Append("<script>");
            sb.Append("tailwind.config = {");
            sb.Append("  theme: {");
            sb.Append("    extend: {");
            sb.Append("      fontFamily: {");
            sb.Append("        sans: ['Outfit', 'sans-serif'],");
            sb.Append("      },");
            sb.Append("      colors: {");
            sb.Append("        brand: '#ea580c',");
            sb.Append("      }");
            sb.Append("    }");
            sb.Append("  }");
            sb.Append("};");
            sb.Append("</script>");

            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Outfit', sans-serif;");
            sb.Append("  overflow-x: hidden;");
            sb.AppendFormat("  background-color: {0};", dark ? "#020617" : "#f9fafb");
            sb.Append("}");
            // Custom scrollbar
            sb.Append("::-webkit-scrollbar { width: 6px; }");
            sb.AppendFormat("::-webkit-scrollbar-track {{ background: {0}; }}", dark ? "rgba(15, 23, 42, 0.3)" : "rgba(0, 0, 0, 0.05)");
            sb.Append("::-webkit-scrollbar-thumb { background: rgba(234, 88, 12, 0.4); border-radius: 4px; }");
            sb.Append("::-webkit-scrollbar-thumb:hover { background: rgba(234, 88, 12, 0.6); }");
            
            // Glassmorphism card styles
            sb.Append(".glass-card {");
            sb.AppendFormat("  background: {0};", dark ? "rgba(15, 23, 42, 0.45)" : "rgba(255, 255, 255, 0.7)");
            sb.Append("  backdrop-filter: blur(16px);");
            sb.Append("  -webkit-backdrop-filter: blur(16px);");
            sb.AppendFormat("  border: 1px solid {0};", dark ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.06)");
            sb.AppendFormat("  box-shadow: {0};", dark ? "0 8px 32px 0 rgba(0, 0, 0, 0.37)" : "0 8px 32px 0 rgba(0, 0, 0, 0.06)");
            sb.Append("  transition: all 0.5s cubic-bezier(0.23, 1, 0.32, 1);");
            sb.Append("}");
            sb.Append(".glass-card:hover {");
            sb.Append("  border-color: rgba(234, 88, 12, 0.45);");
            sb.AppendFormat("  box-shadow: {0};", dark ? "0 0 30px rgba(234, 88, 12, 0.18), inset 0 0 12px rgba(255, 255, 255, 0.02)" : "0 0 25px rgba(234, 88, 12, 0.12)");
            sb.Append("}");

            // 3D Card tilt properties
            sb.Append(".tilt-container {");
            sb.Append("  perspective: 1000px;");
            sb.Append("}");
            sb.Append(".tilt-card {");
            sb.Append("  transform-style: preserve-3d;");
            sb.Append("  transition: transform 0.15s ease-out, box-shadow 0.3s ease;");
            sb.Append("}");
            
            // Shimmer and neon glows
            sb.Append(".neon-glow-pfp {");
            sb.Append("  box-shadow: 0 0 15px rgba(234, 88, 12, 0.5);");
            sb.Append("}");
            sb.Append(".neon-glow-pfp-blue {");
            sb.Append("  box-shadow: 0 0 15px rgba(59, 130, 246, 0.5);");
            sb.Append("}");
            sb.Append(".neon-glow-pfp-emerald {");
            sb.Append("  box-shadow: 0 0 15px rgba(16, 185, 129, 0.5);");
            sb.Append("}");
            sb.Append(".gloss-highlight {");
            sb.AppendFormat("  background: {0};", dark ? "linear-gradient(135deg, rgba(255, 255, 255, 0.12) 0%, rgba(255, 255, 255, 0) 60%)" : "linear-gradient(135deg, rgba(255, 255, 255, 0.5) 0%, rgba(255, 255, 255, 0) 60%)");
            sb.Append("}");
            // Modal entrance animation (scale-95 to scale-100 over 300ms cubic-bezier(0.16, 1, 0.3, 1))
            sb.Append("@keyframes modalScaleIn {");
            sb.Append("  from { opacity: 0; transform: scale(0.95); }");
            sb.Append("  to   { opacity: 1; transform: scale(1); }");
            sb.Append("}");
            sb.Append(".modal-enter {");
            sb.Append("  animation: modalScaleIn 300ms cubic-bezier(0.16, 1, 0.3, 1) forwards;");
            sb.Append("}");
            // Staggered card entrance
            sb.Append("@keyframes cardStaggerIn {");
            sb.Append("  from { opacity: 0; transform: translateY(20px); }");
            sb.Append("  to   { opacity: 1; transform: translateY(0); }");
            sb.Append("}");
            sb.Append(".card-stagger {");
            sb.Append("  animation: cardStaggerIn 0.5s cubic-bezier(0.16, 1, 0.3, 1) both;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head>");
            sb.AppendFormat("<body class='{0} min-h-screen p-6 relative'>", dark ? "text-slate-100" : "text-gray-900");

            // Ambient background glows
            if (dark)
            {
                sb.Append("<div class='fixed top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-orange-600/10 blur-[120px] pointer-events-none z-0'></div>");
                sb.Append("<div class='fixed bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-blue-600/10 blur-[120px] pointer-events-none z-0'></div>");
            }
            else
            {
                sb.Append("<div class='fixed top-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-orange-500/5 blur-[120px] pointer-events-none z-0'></div>");
                sb.Append("<div class='fixed bottom-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-blue-500/5 blur-[120px] pointer-events-none z-0'></div>");
            }

            sb.Append("<div id='root' class='relative z-10'></div>");

            // React code compiling on-the-fly via Babel
            sb.Append("<script type='text/babel'>");
            sb.Append("const { useState, useEffect, useRef } = React;");
            
            // Injected variables
            sb.AppendFormat("const API_BASE_URL = '{0}';", apiBaseUrl);
            sb.AppendFormat("const IS_DARK_THEME = {0};", dark ? "true" : "false");

            sb.Append(@"
            // Helper to resolve and format image URLs and raw Base64 strings safely
            function resolvePhotoUrl(accountOrUrl) {
                if (!accountOrUrl) return '';
                let val = '';
                if (typeof accountOrUrl === 'object') {
                    val = accountOrUrl.idPhotoUrl || accountOrUrl.avatarBase64 || accountOrUrl.photoUrl || '';
                } else {
                    val = accountOrUrl;
                }
                if (!val || typeof val !== 'string') return '';
                val = val.trim();
                if (!val) return '';
                if (val.startsWith('http://') || val.startsWith('https://') || val.startsWith('data:') || val.startsWith('blob:')) {
                    return val;
                }
                return `data:image/png;base64,${val}`;
            }

            // 3D Card Hover Component (Dashboard grid listing view)
            function AccountCard({ account, onEdit, onDelete }) {
                const cardRef = useRef(null);
                const [coords, setCoords] = useState({ x: 0, y: 0 });
                const [isHovered, setIsHovered] = useState(false);

                const handleMouseMove = (e) => {
                    if (!cardRef.current) return;
                    const card = cardRef.current;
                    const rect = card.getBoundingClientRect();
                    
                    const x = e.clientX - rect.left - rect.width / 2;
                    const y = e.clientY - rect.top - rect.height / 2;
                    
                    const tiltX = (y / (rect.height / 2)) * -8;
                    const tiltY = (x / (rect.width / 2)) * 8;

                    setCoords({ x: tiltX, y: tiltY });
                };

                const handleMouseEnter = () => setIsHovered(true);
                const handleMouseLeave = () => {
                    setIsHovered(false);
                    setCoords({ x: 0, y: 0 });
                };

                const getCardStyle = () => {
                    if (!isHovered) return { transform: 'rotateX(0deg) rotateY(0deg) translateZ(0)' };
                    return {
                        transform: `rotateX(${coords.x}deg) rotateY(${coords.y}deg) translateY(-5px) translateZ(10px)`
                    };
                };

                const getRoleColor = (role) => {
                    switch(role.toLowerCase()) {
                        case 'admin': 
                            return IS_DARK_THEME 
                                ? 'bg-purple-500/20 text-purple-400 border border-purple-500/30' 
                                : 'bg-purple-100 text-purple-800 border border-purple-200';
                        case 'driver': 
                            return IS_DARK_THEME 
                                ? 'bg-blue-500/20 text-blue-400 border border-blue-500/30' 
                                : 'bg-blue-100 text-blue-800 border border-blue-200';
                        default: 
                            return IS_DARK_THEME 
                                ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' 
                                : 'bg-emerald-100 text-emerald-800 border border-emerald-200';
                    }
                };

                const getDriverStatusColor = (status) => {
                    switch(status?.toLowerCase()) {
                        case 'available': return 'bg-emerald-500 text-emerald-50';
                        case 'busy': return 'bg-amber-500 text-amber-50';
                        default: return 'bg-slate-500 text-slate-50';
                    }
                };

                const initials = account.fullName
                    .split(' ')
                    .map(n => n[0])
                    .join('')
                    .substring(0, 2)
                    .toUpperCase();

                const photoSrc = resolvePhotoUrl(account);

                return (
                    <div 
                        className='tilt-container'
                        onMouseMove={handleMouseMove}
                        onMouseEnter={handleMouseEnter}
                        onMouseLeave={handleMouseLeave}
                    >
                        <div 
                            ref={cardRef}
                            style={getCardStyle()}
                            className='tilt-card glass-card rounded-2xl p-6 relative overflow-hidden flex flex-col justify-between h-[310px] select-none cursor-default'
                        >
                            <div className='absolute inset-0 gloss-highlight pointer-events-none'></div>

                            {isHovered && IS_DARK_THEME && (
                                <div className='absolute -top-[30%] -right-[30%] w-[60%] h-[60%] rounded-full bg-orange-600/10 blur-[40px] pointer-events-none'></div>
                            )}

                            <div className='flex justify-between items-start z-10'>
                                <div className='flex items-center gap-4'>
                                    {/* Circular Glowing border around profile pic */}
                                    <div className='relative w-16 h-16 rounded-full flex items-center justify-center p-[2px] bg-gradient-to-tr from-orange-500 to-amber-400 neon-glow-pfp overflow-hidden shrink-0'>
                                        {photoSrc ? (
                                            <img 
                                                src={photoSrc} 
                                                alt={account.fullName} 
                                                className={`w-full h-full rounded-full object-cover ${IS_DARK_THEME ? 'bg-slate-800' : 'bg-slate-200'}`}
                                                onError={(e) => { e.target.style.display = 'none'; if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex'; }}
                                            />
                                        ) : null}
                                        <div className={`w-full h-full rounded-full flex items-center justify-center font-bold text-lg text-orange-400 select-none ${IS_DARK_THEME ? 'bg-slate-800' : 'bg-slate-100'}`} style={{ display: photoSrc ? 'none' : 'flex' }}>
                                            {initials}
                                        </div>
                                    </div>
                                    <div className='max-w-[150px]'>
                                        <h3 className={`font-bold text-base tracking-wide line-clamp-1 ${IS_DARK_THEME ? 'text-slate-100' : 'text-slate-900'}`}>{account.fullName}</h3>
                                        <span className={`px-2 py-0.5 rounded-full text-[9px] font-semibold uppercase tracking-wider block mt-1 w-max ${getRoleColor(account.role)}`}>
                                            {account.role}
                                        </span>
                                    </div>
                                </div>
                                <div className='flex items-center gap-2'>
                                    <button 
                                        onClick={(e) => { e.stopPropagation(); onEdit(account); }}
                                        className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all duration-300 ${IS_DARK_THEME ? 'bg-slate-800/80 hover:bg-orange-500 text-slate-400 hover:text-white border border-slate-700/50' : 'bg-slate-100 hover:bg-orange-500 text-slate-600 hover:text-white border border-slate-200'}`}
                                        title='Edit Account'
                                    >
                                        <i className='fa-solid fa-pen-to-square text-xs'></i>
                                    </button>
                                    <button 
                                        onClick={(e) => { e.stopPropagation(); onDelete(account.userId); }}
                                        className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all duration-300 ${IS_DARK_THEME ? 'bg-slate-800/80 hover:bg-red-500 text-slate-400 hover:text-white border border-slate-700/50' : 'bg-slate-100 hover:bg-red-500 text-slate-600 hover:text-white border border-slate-200'}`}
                                        title='Delete Account'
                                    >
                                        <i className='fa-solid fa-trash text-xs'></i>
                                    </button>
                                </div>
                            </div>

                            <div className={`my-4 space-y-2 text-sm z-10 flex-1 flex flex-col justify-center ${IS_DARK_THEME ? 'text-slate-300' : 'text-slate-700'}`}>
                                <div className='flex items-center gap-2.5'>
                                    <i className='fa-solid fa-envelope text-orange-500/85 w-4 text-center'></i>
                                    <span className='truncate break-all'>{account.email}</span>
                                </div>
                                <div className='flex items-center gap-2.5'>
                                    <i className='fa-solid fa-phone text-orange-500/85 w-4 text-center'></i>
                                    <span>{account.phone || 'No phone number'}</span>
                                </div>
                                <div className={`flex items-center gap-2.5 text-xs mt-1 ${IS_DARK_THEME ? 'text-slate-450' : 'text-slate-500'}`}>
                                    <i className='fa-solid fa-calendar-days text-slate-500 w-4 text-center'></i>
                                    <span>Registered {new Date(account.createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' })}</span>
                                </div>
                            </div>

                            {account.role.toLowerCase() === 'driver' && (
                                <div className={`pt-3 border-t flex justify-between items-center z-10 text-xs p-2.5 rounded-xl border ${IS_DARK_THEME ? 'border-slate-800/50 bg-slate-900/40 text-slate-300' : 'border-slate-200 bg-slate-100/50 text-slate-700'}`}>
                                    <div>
                                        <p className={`text-[9px] uppercase font-semibold ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}>License Number</p>
                                        <p className={`font-mono font-semibold ${IS_DARK_THEME ? 'text-slate-200' : 'text-slate-800'}`}>{account.licenseNo || 'N/A'}</p>
                                    </div>
                                    <div className='text-right'>
                                        <p className={`text-[9px] uppercase font-semibold mb-0.5 ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}>Duty Status</p>
                                        <span className={`px-2 py-0.5 rounded text-[9px] font-bold uppercase ${getDriverStatusColor(account.driverStatus)}`}>
                                            {account.driverStatus || 'inactive'}
                                        </span>
                                    </div>
                                </div>
                            )}

                            {account.role.toLowerCase() !== 'driver' && (
                                <div className={`pt-3 border-t flex justify-between items-center z-10 text-xs ${IS_DARK_THEME ? 'border-slate-800/60 text-slate-400' : 'border-slate-200 text-slate-650'}`}>
                                    <span>Identity Authentication</span>
                                    <span className='flex items-center gap-1 text-emerald-500 font-bold'>
                                        <i className='fa-solid fa-circle-check'></i> Verified
                                    </span>
                                </div>
                            )}
                        </div>
                    </div>
                );
            }

            // Interactive 3D Digital Employee ID Card Component
            function EmployeeIDCard({ fullName, role, idPhotoUrl, signatureBase64, createdAt }) {
                const cardRef = useRef(null);
                const [coords, setCoords] = useState({ x: 0, y: 0 });
                const [glare, setGlare] = useState({ x: 50, y: 50 });
                const [isHovered, setIsHovered] = useState(false);

                const handleMouseMove = (e) => {
                    if (!cardRef.current) return;
                    const rect = cardRef.current.getBoundingClientRect();
                    const x = e.clientX - rect.left - rect.width / 2;
                    const y = e.clientY - rect.top - rect.height / 2;
                    const tiltX = (y / (rect.height / 2)) * -14;
                    const tiltY = (x / (rect.width / 2)) * 14;
                    setCoords({ x: tiltX, y: tiltY });
                    setGlare({
                        x: ((e.clientX - rect.left) / rect.width) * 100,
                        y: ((e.clientY - rect.top) / rect.height) * 100
                    });
                };

                const handleMouseLeave = () => {
                    setIsHovered(false);
                    setCoords({ x: 0, y: 0 });
                };

                const initials = fullName
                    ? fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase()
                    : 'DG';

                const issueDate = createdAt 
                    ? new Date(createdAt).toLocaleDateString(undefined, { year: 'numeric', month: '2-digit' }) 
                    : new Date().toLocaleDateString(undefined, { year: 'numeric', month: '2-digit' });

                return (
                    <div 
                        ref={cardRef}
                        onMouseMove={handleMouseMove}
                        onMouseEnter={() => setIsHovered(true)}
                        onMouseLeave={handleMouseLeave}
                        style={{
                            transform: isHovered 
                                ? `rotateX(${coords.x}deg) rotateY(${coords.y}deg) scale(1.02) translateZ(10px)` 
                                : 'rotateX(0deg) rotateY(0deg) scale(1)',
                            transition: isHovered ? 'transform 0.1s ease-out' : 'transform 0.5s cubic-bezier(0.23, 1, 0.32, 1)'
                        }}
                        className={`w-[320px] h-[200px] rounded-2xl p-5 relative overflow-hidden select-none cursor-default border glass-card shadow-2xl flex flex-col justify-between tilt-card ${
                            IS_DARK_THEME 
                                ? 'border-white/10 bg-gradient-to-br from-slate-900/90 to-slate-950/90 text-white' 
                                : 'border-slate-200 bg-gradient-to-br from-white/90 to-slate-100/90 text-slate-900'
                        }`}
                    >
                        {/* Dynamic Radial Glare overlay tracking cursor */}
                        <div 
                            className='absolute inset-0 pointer-events-none rounded-2xl z-20 transition-opacity duration-200'
                            style={{
                                background: `radial-gradient(circle at ${glare.x}% ${glare.y}%, rgba(255, 255, 255, ${isHovered ? 0.22 : 0}) 0%, transparent 65%)`
                            }}
                        />

                        {/* Grid line overlay */}
                        <div className='absolute inset-0 bg-[linear-gradient(rgba(234,88,12,0.03)_1px,transparent_1px),linear-gradient(90deg,rgba(234,88,12,0.03)_1px,transparent_1px)] bg-[size:16px_16px] pointer-events-none'></div>
                        <div className='absolute inset-0 gloss-highlight pointer-events-none'></div>
                        <div className='absolute -right-16 -top-16 w-32 h-32 rounded-full bg-orange-500/10 blur-[30px] pointer-events-none'></div>

                        {/* Top layout */}
                        <div className='flex justify-between items-center z-10'>
                            <div className='flex items-center gap-1.5'>
                                <i className='fa-solid fa-car-side text-orange-500 text-lg'></i>
                                <span className='font-extrabold tracking-wider text-xs uppercase bg-gradient-to-r from-orange-500 to-amber-500 bg-clip-text text-transparent'>Drive&Go</span>
                            </div>
                            <span className={`text-[8px] tracking-widest font-extrabold px-2 py-0.5 rounded-full ${
                                IS_DARK_THEME ? 'bg-orange-500/15 text-orange-400' : 'bg-orange-100 text-orange-850'
                            }`}>STAFF PORTAL</span>
                        </div>

                        {/* Profile layout */}
                        <div className='flex items-center gap-4 my-2 z-10'>
                            {/* Photo with neon glow */}
                            <div className='w-16 h-16 rounded-full p-[2px] bg-gradient-to-tr from-orange-500 to-amber-400 neon-glow-pfp shrink-0 overflow-hidden relative'>
                                {resolvePhotoUrl(idPhotoUrl) ? (
                                    <img src={resolvePhotoUrl(idPhotoUrl)} alt='Employee' className='w-full h-full rounded-full object-cover bg-slate-800 transition-opacity duration-500 ease-in-out' onError={(e) => { e.target.style.display = 'none'; if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex'; }} />
                                ) : null}
                                <div className={`w-full h-full rounded-full flex items-center justify-center font-bold text-orange-500 text-lg ${
                                    IS_DARK_THEME ? 'bg-slate-800' : 'bg-slate-100'
                                }`} style={{ display: resolvePhotoUrl(idPhotoUrl) ? 'none' : 'flex' }}>
                                    {initials}
                                </div>
                            </div>

                            <div className='flex-1 min-w-0'>
                                <h3 className={`font-extrabold text-sm tracking-wide truncate ${
                                    IS_DARK_THEME ? 'text-white' : 'text-slate-900'
                                }}`}>{fullName || 'Full Name'}</h3>
                                
                                <span className={`text-[9px] font-bold tracking-widest uppercase mt-0.5 block ${
                                    role.toLowerCase() === 'admin' ? 'text-purple-400 font-bold' : 'text-blue-400 font-bold'
                                }`}>
                                    {role.toUpperCase()}
                                </span>
                                
                                <div className='mt-2 grid grid-cols-2 gap-1 text-[8px] text-slate-400 font-semibold'>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>ID Code</p>
                                        <p className={`font-mono ${IS_DARK_THEME ? 'text-slate-300' : 'text-slate-800'}`}>DG-EMP-{(fullName ? fullName.length * 11 : 777)}</p>
                                    </div>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>Joined</p>
                                        <p className={IS_DARK_THEME ? 'text-slate-300' : 'text-slate-850'}>{issueDate}</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Barcode & Signature footer */}
                        <div className={`pt-2 border-t flex justify-between items-center text-[7px] ${
                            IS_DARK_THEME ? 'border-white/5 text-slate-500' : 'border-slate-200 text-slate-400'
                        }`}>
                            <div className='flex items-center gap-2'>
                                <span className='font-mono tracking-widest'>*DRIVEANDGO-STAFF*</span>
                                {signatureBase64 && (
                                    <div className='h-4 max-w-[65px] flex items-center px-1 rounded bg-black/20'>
                                        <img src={signatureBase64} alt='Sign' className='max-h-full max-w-full object-contain' />
                                    </div>
                                )}
                            </div>
                            <span className='font-semibold uppercase tracking-wider text-orange-500'>SYSTEM SECURE</span>
                        </div>
                    </div>
                );
            }

            // Interactive 3D Digital Driver's License Component
            function DriversLicenseCard({ fullName, idPhotoUrl, signatureBase64, licenseNo, driverStatus }) {
                const cardRef = useRef(null);
                const [coords, setCoords] = useState({ x: 0, y: 0 });
                const [isHovered, setIsHovered] = useState(false);

                const handleMouseMove = (e) => {
                    if (!cardRef.current) return;
                    const rect = cardRef.current.getBoundingClientRect();
                    const x = e.clientX - rect.left - rect.width / 2;
                    const y = e.clientY - rect.top - rect.height / 2;
                    const tiltX = (y / (rect.height / 2)) * -14;
                    const tiltY = (x / (rect.width / 2)) * 14;
                    setCoords({ x: tiltX, y: tiltY });
                };

                const handleMouseLeave = () => {
                    setIsHovered(false);
                    setCoords({ x: 0, y: 0 });
                };

                const initials = fullName
                    ? fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase()
                    : 'DR';

                return (
                    <div 
                        ref={cardRef}
                        onMouseMove={handleMouseMove}
                        onMouseEnter={() => setIsHovered(true)}
                        onMouseLeave={handleMouseLeave}
                        style={{
                            transform: isHovered 
                                ? `rotateX(${coords.x}deg) rotateY(${coords.y}deg) scale(1.02) translateZ(10px)` 
                                : 'rotateX(0deg) rotateY(0deg) scale(1)',
                            transition: isHovered ? 'transform 0.1s ease-out' : 'transform 0.5s cubic-bezier(0.23, 1, 0.32, 1)'
                        }}
                        className={`w-[320px] h-[200px] rounded-2xl p-5 relative overflow-hidden select-none cursor-default border glass-card shadow-2xl flex flex-col justify-between tilt-card ${
                            IS_DARK_THEME 
                                ? 'border-white/10 bg-gradient-to-br from-slate-900/90 to-slate-950/90 text-white' 
                                : 'border-slate-200 bg-gradient-to-br from-white/90 to-slate-100/90 text-slate-900'
                        }`}
                    >
                        <div className='absolute inset-0 bg-[linear-gradient(rgba(59,130,246,0.03)_1px,transparent_1px),linear-gradient(90deg,rgba(59,130,246,0.03)_1px,transparent_1px)] bg-[size:16px_16px] pointer-events-none'></div>
                        <div className='absolute inset-0 gloss-highlight pointer-events-none'></div>
                        <div className='absolute -left-16 -bottom-16 w-32 h-32 rounded-full bg-blue-600/10 blur-[30px] pointer-events-none'></div>

                        {/* Seal */}
                        <div className='absolute right-4 bottom-4 w-12 h-12 rounded-full border border-blue-500/15 flex items-center justify-center opacity-30 select-none'>
                            <i className='fa-solid fa-certificate text-2xl text-blue-500/80 animate-pulse'></i>
                        </div>

                        {/* Top layout */}
                        <div className='flex justify-between items-start z-10'>
                            <div className='flex items-center gap-1.5'>
                                <i className='fa-solid fa-car-side text-blue-500 text-lg'></i>
                                <span className='font-extrabold tracking-wider text-xs uppercase bg-gradient-to-r from-blue-500 to-indigo-500 bg-clip-text text-transparent'>Drive&Go</span>
                            </div>
                            <span className={`text-[8px] tracking-widest font-extrabold px-2 py-0.5 rounded-full ${
                                IS_DARK_THEME ? 'bg-blue-500/15 text-blue-400' : 'bg-blue-100 text-blue-850'
                            }`}>DRIVER LIC.</span>
                        </div>

                        {/* Driver details */}
                        <div className='flex items-center gap-4 my-2 z-10'>
                            {/* Photo with blue neon glow */}
                            <div className='w-16 h-16 rounded-xl p-[2px] bg-gradient-to-tr from-blue-500 to-indigo-400 neon-glow-pfp-blue shrink-0 overflow-hidden relative'>
                                {resolvePhotoUrl(idPhotoUrl) ? (
                                    <img src={resolvePhotoUrl(idPhotoUrl)} alt='Driver' className='w-full h-full rounded-xl object-cover bg-slate-800 transition-opacity duration-500 ease-in-out' onError={(e) => { e.target.style.display = 'none'; if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex'; }} />
                                ) : null}
                                <div className={`w-full h-full rounded-xl flex items-center justify-center font-bold text-blue-500 text-lg ${
                                    IS_DARK_THEME ? 'bg-slate-800' : 'bg-slate-100'
                                }`} style={{ display: resolvePhotoUrl(idPhotoUrl) ? 'none' : 'flex' }}>
                                    {initials}
                                </div>
                            </div>

                            <div className='flex-1 min-w-0'>
                                <h3 className={`font-extrabold text-sm tracking-wide truncate ${
                                    IS_DARK_THEME ? 'text-white' : 'text-slate-900'
                                }`}>{fullName || 'Full Legal Name'}</h3>
                                
                                <span className='text-[9px] font-bold tracking-widest uppercase mt-0.5 block text-slate-400'>
                                    AUTHORIZED OPERATOR
                                </span>
                                
                                <div className='mt-2 grid grid-cols-2 gap-1 text-[8px] text-slate-400 font-semibold'>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>Lic. Number</p>
                                        <p className={`font-mono ${IS_DARK_THEME ? 'text-slate-300' : 'text-slate-800'}`}>{licenseNo || 'N/A'}</p>
                                    </div>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>Duty Status</p>
                                        <span className={`font-bold capitalize ${
                                            driverStatus === 'available' ? 'text-emerald-500' : 'text-amber-500'
                                        }`}>{driverStatus || 'available'}</span>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Card footer with signature */}
                        <div className={`pt-2 border-t flex justify-between items-center text-[7px] ${
                            IS_DARK_THEME ? 'border-white/5 text-slate-500' : 'border-slate-200 text-slate-400'
                        }`}>
                            <div className='flex items-center gap-2'>
                                <span className='font-mono tracking-widest'>*DRIVEANDGO-LICENSE*</span>
                                {signatureBase64 && (
                                    <div className='h-4 max-w-[65px] flex items-center px-1 rounded bg-black/20'>
                                        <img src={signatureBase64} alt='Sign' className='max-h-full max-w-full object-contain' />
                                    </div>
                                )}
                            </div>
                            <span className='font-semibold uppercase tracking-wider text-blue-500'>CLASS A CERTIFIED</span>
                        </div>
                    </div>
                );
            }

            // Interactive 3D Digital Customer Card Component
            function MembershipCard({ fullName, idPhotoUrl, signatureBase64, createdAt }) {
                const cardRef = useRef(null);
                const [coords, setCoords] = useState({ x: 0, y: 0 });
                const [isHovered, setIsHovered] = useState(false);

                const handleMouseMove = (e) => {
                    if (!cardRef.current) return;
                    const rect = cardRef.current.getBoundingClientRect();
                    const x = e.clientX - rect.left - rect.width / 2;
                    const y = e.clientY - rect.top - rect.height / 2;
                    const tiltX = (y / (rect.height / 2)) * -14;
                    const tiltY = (x / (rect.width / 2)) * 14;
                    setCoords({ x: tiltX, y: tiltY });
                };

                const handleMouseLeave = () => {
                    setIsHovered(false);
                    setCoords({ x: 0, y: 0 });
                };

                const initials = fullName
                    ? fullName.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase()
                    : 'DG';

                const issueDate = createdAt 
                    ? new Date(createdAt).toLocaleDateString(undefined, { year: 'numeric', month: '2-digit' }) 
                    : new Date().toLocaleDateString(undefined, { year: 'numeric', month: '2-digit' });

                return (
                    <div 
                        ref={cardRef}
                        onMouseMove={handleMouseMove}
                        onMouseEnter={() => setIsHovered(true)}
                        onMouseLeave={handleMouseLeave}
                        style={{
                            transform: isHovered 
                                ? `rotateX(${coords.x}deg) rotateY(${coords.y}deg) scale(1.02) translateZ(10px)` 
                                : 'rotateX(0deg) rotateY(0deg) scale(1)',
                            transition: isHovered ? 'transform 0.1s ease-out' : 'transform 0.5s cubic-bezier(0.23, 1, 0.32, 1)'
                        }}
                        className={`w-[320px] h-[200px] rounded-2xl p-5 relative overflow-hidden select-none cursor-default border glass-card shadow-2xl flex flex-col justify-between tilt-card ${
                            IS_DARK_THEME 
                                ? 'border-white/10 bg-gradient-to-br from-slate-900/90 to-slate-950/90 text-white' 
                                : 'border-slate-200 bg-gradient-to-br from-white/90 to-slate-100/90 text-slate-900'
                        }`}
                    >
                        <div className='absolute inset-0 bg-[linear-gradient(rgba(16,185,129,0.03)_1px,transparent_1px),linear-gradient(90deg,rgba(16,185,129,0.03)_1px,transparent_1px)] bg-[size:16px_16px] pointer-events-none'></div>
                        <div className='absolute inset-0 gloss-highlight pointer-events-none'></div>
                        <div className='absolute -left-16 -top-16 w-32 h-32 rounded-full bg-emerald-500/10 blur-[30px] pointer-events-none'></div>

                        {/* Top layout */}
                        <div className='flex justify-between items-center z-10'>
                            <div className='flex items-center gap-1.5'>
                                <i className='fa-solid fa-car-side text-emerald-500 text-lg'></i>
                                <span className='font-extrabold tracking-wider text-xs uppercase bg-gradient-to-r from-emerald-500 to-teal-500 bg-clip-text text-transparent'>Drive&Go</span>
                            </div>
                            <span className={`text-[8px] tracking-widest font-extrabold px-2 py-0.5 rounded-full ${
                                IS_DARK_THEME ? 'bg-emerald-500/15 text-emerald-450' : 'bg-emerald-100 text-emerald-800'
                            }`}>MEMBER CARD</span>
                        </div>

                        {/* Customer Details */}
                        <div className='flex items-center gap-4 my-2 z-10'>
                            {/* Photo with emerald neon glow */}
                            <div className='w-16 h-16 rounded-full p-[2px] bg-gradient-to-tr from-emerald-500 to-teal-400 neon-glow-pfp-emerald shrink-0 overflow-hidden relative'>
                                {resolvePhotoUrl(idPhotoUrl) ? (
                                    <img src={resolvePhotoUrl(idPhotoUrl)} alt='Customer' className='w-full h-full rounded-full object-cover bg-slate-800 transition-opacity duration-500 ease-in-out' onError={(e) => { e.target.style.display = 'none'; if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex'; }} />
                                ) : null}
                                <div className={`w-full h-full rounded-full flex items-center justify-center font-bold text-emerald-500 text-lg ${
                                    IS_DARK_THEME ? 'bg-slate-800' : 'bg-slate-100'
                                }`} style={{ display: resolvePhotoUrl(idPhotoUrl) ? 'none' : 'flex' }}>
                                    {initials}
                                </div>
                            </div>

                            <div className='flex-1 min-w-0'>
                                <h3 className={`font-extrabold text-sm tracking-wide truncate ${
                                    IS_DARK_THEME ? 'text-white' : 'text-slate-900'
                                }}`}>{fullName || 'Customer Name'}</h3>
                                
                                <span className='text-[9px] font-bold tracking-widest uppercase mt-0.5 block text-slate-450'>
                                    LOYAL CUSTOMER
                                </span>
                                
                                <div className='mt-2 grid grid-cols-2 gap-1 text-[8px] text-slate-400 font-semibold'>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>Member ID</p>
                                        <p className={`font-mono ${IS_DARK_THEME ? 'text-slate-300' : 'text-slate-800'}`}>DG-MEM-{(fullName ? fullName.length * 19 : 999)}</p>
                                    </div>
                                    <div>
                                        <p className='uppercase text-[7px] text-slate-500'>Joined</p>
                                        <p className={IS_DARK_THEME ? 'text-slate-300' : 'text-slate-850'}>{issueDate}</p>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Barcode & Signature footer */}
                        <div className={`pt-2 border-t flex justify-between items-center text-[7px] ${
                            IS_DARK_THEME ? 'border-white/5 text-slate-500' : 'border-slate-200 text-slate-400'
                        }`}>
                            <div className='flex items-center gap-2'>
                                <span className='font-mono tracking-widest'>*DRIVEANDGO-MEMBER*</span>
                                {signatureBase64 && (
                                    <div className='h-4 max-w-[65px] flex items-center px-1 rounded bg-black/20'>
                                        <img src={signatureBase64} alt='Sign' className='max-h-full max-w-full object-contain' />
                                    </div>
                                )}
                            </div>
                            <span className='font-semibold uppercase tracking-wider text-emerald-500'>VERIFIED GUEST</span>
                        </div>
                    </div>
                );
            }

            // Custom File Uploader Component with 0-100% Progress Tracking
            function FileUploader({ label, value, onChange, folderName, isUploading, setIsUploading, onNotify }) {
                const fileInputRef = useRef(null);
                const [uploadProgress, setUploadProgress] = useState(0);
                const [uploadStatus, setUploadStatus] = useState('');
                const [uploadError, setUploadError] = useState('');

                const handleFileChange = async (e) => {
                    const file = e.target.files[0];
                    if (!file) return;

                    if (!file.type.startsWith('image/')) {
                        const err = 'Please select a valid image file (PNG, JPG, WEBP).';
                        setUploadError(err);
                        if (onNotify) onNotify(err, 'error');
                        return;
                    }

                    setUploadError('');
                    setIsUploading(true);
                    setUploadProgress(20);
                    setUploadStatus('Reading local photo...');

                    // Read local image preview immediately
                    const reader = new FileReader();
                    reader.onload = (uploadEvt) => {
                        onChange(uploadEvt.target.result);
                        setUploadProgress(50);
                        setUploadStatus('Optimizing image...');
                    };
                    reader.readAsDataURL(file);

                    const formData = new FormData();
                    formData.append('file', file);
                    formData.append('folderName', folderName);

                    try {
                        setUploadProgress(75);
                        setUploadStatus('Transferring to storage...');
                        const response = await fetch(`${API_BASE_URL}/media/upload`, {
                            method: 'POST',
                            body: formData
                        });

                        if (response.ok) {
                            const data = await response.json();
                            if (data.url) onChange(data.url);
                            setUploadProgress(100);
                            setUploadStatus('Photo uploaded!');
                            if (onNotify) onNotify('Photo uploaded successfully!', 'success');
                        } else {
                            setUploadProgress(100);
                            setUploadStatus('Preview stored locally');
                        }
                    } catch (err) {
                        setUploadProgress(100);
                        setUploadStatus('Preview stored locally');
                    } finally {
                        setTimeout(() => {
                            setIsUploading(false);
                            setUploadProgress(0);
                            setUploadStatus('');
                        }, 500);
                    }
                };

                const triggerFileSelect = () => {
                    fileInputRef.current.click();
                };

                return (
                    <div className='flex flex-col gap-1.5'>
                        <label className={`block font-semibold uppercase tracking-wider ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>{label}</label>
                        <div 
                            onClick={triggerFileSelect}
                            className={`relative h-28 border-2 border-dashed rounded-xl cursor-pointer flex flex-col items-center justify-center transition-all group overflow-hidden ${
                                IS_DARK_THEME 
                                    ? 'border-slate-700 bg-slate-800/30 hover:border-orange-500 hover:bg-slate-800/50' 
                                    : 'border-gray-300 bg-gray-50/50 hover:border-orange-500 hover:bg-gray-100/50'
                            }`}
                        >
                            {isUploading ? (
                                <div className='flex flex-col items-center justify-center p-3 w-full h-full space-y-2 relative z-10'>
                                    <div className='flex items-center justify-between w-full max-w-[260px] text-[10px] font-bold'>
                                        <span className='text-orange-500 uppercase tracking-wider flex items-center gap-1.5 truncate'>
                                            <i className='fa-solid fa-spinner animate-spin text-xs'></i> {uploadStatus || 'Uploading...'}
                                        </span>
                                        <span className='text-orange-400 font-mono text-xs'>{uploadProgress}%</span>
                                    </div>
                                    <div className='w-full max-w-[260px] h-2 bg-slate-800 rounded-full overflow-hidden border border-orange-500/30'>
                                        <div 
                                            className='h-full bg-gradient-to-r from-orange-500 via-amber-400 to-emerald-400 transition-all duration-300 ease-out rounded-full' 
                                            style={{ width: `${uploadProgress}%` }}
                                        ></div>
                                    </div>
                                </div>
                            ) : uploadError ? (
                                <div className='flex items-center justify-between gap-3 p-3 w-full h-full bg-red-500/10 border border-red-500/30 rounded-xl relative z-10'>
                                    <div className='flex items-center gap-2.5 min-w-0'>
                                        <i className='fa-solid fa-triangle-exclamation text-red-400 text-lg shrink-0'></i>
                                        <div className='min-w-0'>
                                            <p className='text-[10px] font-bold text-red-400 uppercase tracking-wider'>Upload Failed</p>
                                            <p className='text-[9px] text-red-300 truncate'>{uploadError}</p>
                                        </div>
                                    </div>
                                    <button 
                                        type='button' 
                                        onClick={(e) => { e.stopPropagation(); setUploadError(''); triggerFileSelect(); }}
                                        className='px-2.5 py-1 rounded bg-red-500/20 hover:bg-red-500/30 text-red-300 text-[9px] font-bold uppercase tracking-wider shrink-0'
                                    >
                                        Retry
                                    </button>
                                </div>
                            ) : resolvePhotoUrl(value) ? (
                                <div className='flex items-center gap-3.5 p-3 w-full h-full relative z-10'>
                                    <div className='w-16 h-16 rounded-full overflow-hidden border border-orange-500/50 neon-glow-pfp shrink-0'>
                                        <img src={resolvePhotoUrl(value)} alt='Preview' className='w-full h-full object-cover' />
                                    </div>
                                    <div className='flex flex-col items-start'>
                                        <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded ${IS_DARK_THEME ? 'bg-orange-500/20 text-orange-400' : 'bg-orange-100 text-orange-850'}`}>Photo Selected</span>
                                        <button 
                                            type='button' 
                                            onClick={(e) => { e.stopPropagation(); triggerFileSelect(); }}
                                            className='text-[10px] text-orange-500 hover:text-orange-400 font-bold underline mt-1.5'
                                        >
                                            Change Photo
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div className='flex flex-col items-center justify-center p-4 text-center'>
                                    <i className={`fa-solid fa-cloud-arrow-up text-xl mb-1.5 transition-transform group-hover:-translate-y-1 ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}></i>
                                    <span className={`text-[10px] font-semibold ${IS_DARK_THEME ? 'text-slate-355' : 'text-gray-700'}`}>Click or drag image to upload</span>
                                    <span className={`text-[9px] mt-0.5 ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}>PNG, JPG, WEBP up to 5MB</span>
                                </div>
                            )}
                            <input 
                                type='file' 
                                ref={fileInputRef} 
                                onChange={handleFileChange} 
                                accept='image/*' 
                                className='hidden' 
                            />
                        </div>
                    </div>
                );
            }

            // Custom Signature Uploader with Real-time 0-100% Progress and Automatic Background Removal
            function SignatureUploader({ value, onChange, onNotify }) {
                const fileInputRef = useRef(null);
                const [isProcessing, setIsProcessing] = useState(false);
                const [processProgress, setProcessProgress] = useState(0);
                const [processStatus, setProcessStatus] = useState('');
                const [processError, setProcessError] = useState('');

                const processSignature = (file) => {
                    if (!file.type.startsWith('image/')) {
                        const err = 'Please select a valid image file (PNG, JPG, WEBP).';
                        setProcessError(err);
                        if (onNotify) onNotify(err, 'error');
                        return;
                    }

                    setProcessError('');
                    setIsProcessing(true);
                    setProcessProgress(15);
                    setProcessStatus('Reading image photo...');

                    const reader = new FileReader();
                    reader.onload = (e) => {
                        setProcessProgress(35);
                        setProcessStatus('Analyzing paper image...');

                        const img = new Image();
                        img.onload = () => {
                            setProcessProgress(55);
                            setProcessStatus('Scanning white background & ink strokes...');

                            try {
                                const canvas = document.createElement('canvas');
                                const ctx = canvas.getContext('2d');

                                // Scale to optimal signature size (max 750px)
                                const maxDim = 750;
                                let width = img.width;
                                let height = img.height;
                                if (width > maxDim || height > maxDim) {
                                    if (width > height) {
                                        height = Math.round((height * maxDim) / width);
                                        width = maxDim;
                                    } else {
                                        width = Math.round((width * maxDim) / height);
                                        height = maxDim;
                                    }
                                }

                                canvas.width = width;
                                canvas.height = height;
                                ctx.drawImage(img, 0, 0, width, height);

                                setProcessProgress(75);
                                setProcessStatus('Removing background, paper noise & enhancing ink...');

                                const imgData = ctx.getImageData(0, 0, width, height);
                                const data = imgData.data;

                                // Step 1: Collect luminance distribution of opaque pixels
                                const opaqueLums = [];
                                for (let i = 0; i < data.length; i += 4) {
                                    if (data[i + 3] > 40) {
                                        const lum = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
                                        opaqueLums.push(lum);
                                    }
                                }

                                opaqueLums.sort((a, b) => a - b);
                                const paperWhite = opaqueLums.length > 0 ? opaqueLums[Math.floor(opaqueLums.length * 0.90)] : 255;
                                const inkDarkness = opaqueLums.length > 0 ? opaqueLums[Math.floor(opaqueLums.length * 0.10)] : 0;
                                const contrastRange = Math.max(30, paperWhite - inkDarkness);

                                // Step 2: Binary ink detection
                                const isInk = new Uint8Array(width * height);
                                let hasInk = false;

                                for (let y = 0; y < height; y++) {
                                    for (let x = 0; x < width; x++) {
                                        const idx = (y * width + x) * 4;
                                        const a = data[idx + 3];
                                        if (a < 15) continue;

                                        const r = data[idx];
                                        const g = data[idx + 1];
                                        const b = data[idx + 2];
                                        const lum = 0.299 * r + 0.587 * g + 0.114 * b;

                                        const diffFromPaper = paperWhite - lum;
                                        const contrastRatio = diffFromPaper / contrastRange;

                                        // Paper vs Ink detection threshold
                                        if (diffFromPaper >= 12 && contrastRatio >= 0.08 && lum <= 235) {
                                            isInk[y * width + x] = 1;
                                            hasInk = true;
                                        }
                                    }
                                }

                                // Step 3: Stroke dilation (1px thickening) + deep bold ink coloring
                                let minX = width, minY = height, maxX = 0, maxY = 0;

                                for (let y = 0; y < height; y++) {
                                    for (let x = 0; x < width; x++) {
                                        const pIdx = y * width + x;
                                        const idx = pIdx * 4;

                                        let inkVal = isInk[pIdx];
                                        if (!inkVal) {
                                            if ((x > 0 && isInk[pIdx - 1]) ||
                                                (x < width - 1 && isInk[pIdx + 1]) ||
                                                (y > 0 && isInk[pIdx - width]) ||
                                                (y < height - 1 && isInk[pIdx + width])) {
                                                inkVal = 1;
                                            }
                                        }

                                        if (inkVal) {
                                            data[idx] = 10;      // Pitch dark navy / black ink
                                            data[idx + 1] = 15;
                                            data[idx + 2] = 30;
                                            data[idx + 3] = 255; // 100% solid opacity
                                            if (x < minX) minX = x;
                                            if (x > maxX) maxX = x;
                                            if (y < minY) minY = y;
                                            if (y > maxY) maxY = y;
                                        } else {
                                            data[idx + 3] = 0;   // 100% transparent paper
                                        }
                                    }
                                }

                                ctx.putImageData(imgData, 0, 0);

                                // Step 3: Auto-crop tightly to bounding box if ink is detected
                                let finalCanvas = canvas;
                                if (hasInk && maxX > minX && maxY > minY) {
                                    const cropPadding = 14;
                                    const cropW = Math.min(width, (maxX - minX) + cropPadding * 2);
                                    const cropH = Math.min(height, (maxY - minY) + cropPadding * 2);
                                    const cropX = Math.max(0, minX - cropPadding);
                                    const cropY = Math.max(0, minY - cropPadding);

                                    const croppedCanvas = document.createElement('canvas');
                                    croppedCanvas.width = cropW;
                                    croppedCanvas.height = cropH;
                                    const cropCtx = croppedCanvas.getContext('2d');
                                    cropCtx.drawImage(canvas, cropX, cropY, cropW, cropH, 0, 0, cropW, cropH);
                                    finalCanvas = croppedCanvas;
                                }

                                setProcessProgress(95);
                                setProcessStatus('Encoding transparent PNG signature...');

                                setTimeout(() => {
                                    const transparentPng = finalCanvas.toDataURL('image/png');
                                    onChange(transparentPng);
                                    setProcessProgress(100);
                                    setProcessStatus('Complete!');
                                    setIsProcessing(false);
                                    if (onNotify) onNotify('Signature background removed successfully!', 'success');
                                }, 250);
                            } catch (canvasErr) {
                                setIsProcessing(false);
                                const errMsg = canvasErr.message || 'Error removing background from image.';
                                setProcessError(errMsg);
                                if (onNotify) onNotify(errMsg, 'error');
                            }
                        };
                        img.onerror = () => {
                            setIsProcessing(false);
                            const errMsg = 'Failed to load signature image file.';
                            setProcessError(errMsg);
                            if (onNotify) onNotify(errMsg, 'error');
                        };
                        img.src = e.target.result;
                    };
                    reader.onerror = () => {
                        setIsProcessing(false);
                        const errMsg = 'Failed to read image file.';
                        setProcessError(errMsg);
                        if (onNotify) onNotify(errMsg, 'error');
                    };
                    reader.readAsDataURL(file);
                };

                const handleFileChange = (e) => {
                    const file = e.target.files[0];
                    if (!file) return;
                    processSignature(file);
                };

                return (
                    <div className='flex flex-col gap-2'>
                        <div className='flex items-center justify-between'>
                            <label className={`block font-semibold uppercase tracking-wider ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>
                                Digital Signature (E-Signature)
                            </label>
                            {value && !isProcessing && (
                                <span className='text-[10px] text-emerald-400 font-bold flex items-center gap-1'>
                                    <i className='fa-solid fa-wand-magic-sparkles'></i> BG Auto-Removed
                                </span>
                            )}
                        </div>

                        {/* Note & Guidelines before uploading */}
                        <div className={`p-3 rounded-xl border text-[10.5px] leading-relaxed flex items-start gap-2.5 ${
                            IS_DARK_THEME ? 'bg-amber-500/10 border-amber-500/20 text-amber-300' : 'bg-amber-50 border-amber-200 text-amber-800'
                        }`}>
                            <i className='fa-solid fa-circle-info text-amber-500 mt-0.5 shrink-0 text-sm'></i>
                            <div>
                                <strong className='font-bold block mb-0.5 uppercase text-[9.5px] tracking-wider'>Important Signature Guidelines:</strong>
                                <ul className='list-disc list-inside space-y-0.5 text-[10px] opacity-90'>
                                    <li>Please sign on a <strong>clean, plain white paper</strong> using <strong>black or dark blue ink</strong>.</li>
                                    <li>Take a clear, bright photo directly above your signature without heavy shadows or camera glare.</li>
                                    <li>The system will <strong>automatically remove the white background</strong> with real-time 0-100% processing for rental agreements.</li>
                                </ul>
                            </div>
                        </div>

                        {/* Upload Area / Progress Indicator / Transparent Preview */}
                        <div 
                            onClick={() => fileInputRef.current.click()}
                            className={`relative h-28 border-2 border-dashed rounded-xl cursor-pointer flex flex-col items-center justify-center transition-all group overflow-hidden ${
                                IS_DARK_THEME 
                                    ? 'border-slate-700 bg-slate-800/30 hover:border-orange-500 hover:bg-slate-800/50' 
                                    : 'border-gray-300 bg-gray-50/50 hover:border-orange-500 hover:bg-gray-100/50'
                            }`}
                        >
                            {isProcessing ? (
                                <div className='flex flex-col items-center justify-center p-3 w-full h-full space-y-2 relative z-10'>
                                    <div className='flex items-center justify-between w-full max-w-[280px] text-[10px] font-bold'>
                                        <span className='text-orange-500 uppercase tracking-wider flex items-center gap-1.5 truncate'>
                                            <i className='fa-solid fa-wand-magic-sparkles animate-spin text-xs'></i> {processStatus}
                                        </span>
                                        <span className='text-orange-400 font-mono text-xs'>{processProgress}%</span>
                                    </div>
                                    <div className='w-full max-w-[280px] h-2 bg-slate-800 rounded-full overflow-hidden border border-orange-500/30'>
                                        <div 
                                            className='h-full bg-gradient-to-r from-orange-500 via-amber-400 to-emerald-400 transition-all duration-300 ease-out rounded-full' 
                                            style={{ width: `${processProgress}%` }}
                                        ></div>
                                    </div>
                                    <span className='text-[8.5px] text-slate-400'>Processing high-resolution transparent alpha mask...</span>
                                </div>
                            ) : processError ? (
                                <div className='flex items-center justify-between gap-3 p-3 w-full h-full bg-red-500/10 border border-red-500/30 rounded-xl relative z-10'>
                                    <div className='flex items-center gap-2.5 min-w-0'>
                                        <i className='fa-solid fa-triangle-exclamation text-red-400 text-lg shrink-0'></i>
                                        <div className='min-w-0'>
                                            <p className='text-[10px] font-bold text-red-400 uppercase tracking-wider'>Processing Failed</p>
                                            <p className='text-[9px] text-red-300 truncate'>{processError}</p>
                                        </div>
                                    </div>
                                    <button 
                                        type='button' 
                                        onClick={(e) => { e.stopPropagation(); setProcessError(''); fileInputRef.current.click(); }}
                                        className='px-2.5 py-1 rounded bg-red-500/20 hover:bg-red-500/30 text-red-300 text-[9px] font-bold uppercase tracking-wider shrink-0'
                                    >
                                        Retry
                                    </button>
                                </div>
                            ) : value ? (
                                <div className='flex items-center justify-between gap-4 p-3 w-full h-full relative z-10'>
                                    {/* Checkerboard container to showcase transparency */}
                                    <div className='w-36 h-20 rounded-lg p-2 bg-[linear-gradient(45deg,#1e293b_25%,transparent_25%),linear-gradient(-45deg,#1e293b_25%,transparent_25%),linear-gradient(45deg,transparent_75%,#1e293b_75%),linear-gradient(-45deg,transparent_75%,#1e293b_75%)] bg-[size:10px_10px] bg-slate-900 border border-slate-700 flex items-center justify-center shrink-0 overflow-hidden shadow-inner'>
                                        <img src={value} alt='Processed Signature' className='max-h-full max-w-full object-contain filter drop-shadow' />
                                    </div>
                                    <div className='flex-1 flex flex-col items-start min-w-0'>
                                        <span className='text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center gap-1 truncate'>
                                            <i className='fa-solid fa-check'></i> Transparent Signature Ready
                                        </span>
                                        <span className={`text-[9px] mt-1 line-clamp-1 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Signature will appear on rental agreements and handover documents.</span>
                                        <div className='flex items-center gap-3 mt-2'>
                                            <button 
                                                type='button' 
                                                onClick={(e) => { e.stopPropagation(); fileInputRef.current.click(); }}
                                                className='text-[10px] text-orange-500 hover:text-orange-400 font-bold underline'
                                            >
                                                Upload Another
                                            </button>
                                            <button 
                                                type='button' 
                                                onClick={(e) => { e.stopPropagation(); onChange(''); }}
                                                className='text-[10px] text-red-400 hover:text-red-300 font-bold underline'
                                            >
                                                Remove
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            ) : (
                                <div className='flex flex-col items-center justify-center p-4 text-center'>
                                    <i className={`fa-solid fa-signature text-xl mb-1.5 transition-transform group-hover:-translate-y-1 ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}></i>
                                    <span className={`text-[10px] font-semibold ${IS_DARK_THEME ? 'text-slate-355' : 'text-gray-700'}`}>Upload Signature Photo (Auto BG Removal)</span>
                                    <span className={`text-[9px] mt-0.5 ${IS_DARK_THEME ? 'text-slate-500' : 'text-slate-400'}`}>PNG, JPG, WEBP — Any photo on plain white paper</span>
                                </div>
                            )}
                            <input 
                                type='file' 
                                ref={fileInputRef} 
                                onChange={handleFileChange} 
                                accept='image/*' 
                                className='hidden' 
                            />
                        </div>
                    </div>
                );
            }

            // Custom Dropdown Component
            function RoleFilterDropdown({ activeFilter, onChange }) {
                const [isOpen, setIsOpen] = useState(false);
                const dropdownRef = useRef(null);

                const options = [
                    { value: 'all', label: 'All Accounts', icon: 'fa-users' },
                    { value: 'admin', label: 'Admins Only', icon: 'fa-shield-halved' },
                    { value: 'driver', label: 'Drivers Only', icon: 'fa-id-card' },
                    { value: 'customer', label: 'Customers Only', icon: 'fa-user' }
                ];

                useEffect(() => {
                    const handleOutsideClick = (e) => {
                        if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
                            setIsOpen(false);
                        }
                    };
                    document.addEventListener('mousedown', handleOutsideClick);
                    return () => document.removeEventListener('mousedown', handleOutsideClick);
                }, []);

                const selectedOption = options.find(o => o.value === activeFilter) || options[0];

                return (
                    <div ref={dropdownRef} className='relative w-60 z-50'>
                        <button 
                            onClick={() => setIsOpen(!isOpen)}
                            className='w-full px-5 py-3 glass-card rounded-xl flex items-center justify-between hover:border-orange-500 transition-all duration-300 text-slate-100 font-medium'
                        >
                            <div className='flex items-center gap-2.5'>
                                <i className={`fa-solid ${selectedOption.icon} text-orange-500`}></i>
                                <span className={IS_DARK_THEME ? 'text-slate-200' : 'text-slate-800'}>{selectedOption.label}</span>
                            </div>
                            <i className={`fa-solid fa-chevron-down transition-transform duration-300 text-slate-400 ${isOpen ? 'rotate-180 text-orange-500' : ''}`}></i>
                        </button>

                        {isOpen && (
                            <div className={`absolute left-0 right-0 mt-2 glass-card border rounded-xl overflow-hidden shadow-2xl z-[100] ${IS_DARK_THEME ? 'border-slate-800 bg-slate-900' : 'border-slate-200 bg-white'}`}>
                                {options.map((option) => (
                                    <button
                                        key={option.value}
                                        onClick={() => {
                                            onChange(option.value);
                                            setIsOpen(false);
                                        }}
                                        className={`w-full px-5 py-3 text-left hover:bg-orange-500 hover:text-white flex items-center gap-2.5 transition-colors duration-200 ${
                                            activeFilter === option.value 
                                                ? 'bg-orange-600/20 text-orange-500 font-semibold' 
                                                : IS_DARK_THEME ? 'text-slate-300' : 'text-slate-700'
                                        }`}
                                    >
                                        <i className={`fa-solid ${option.icon} text-xs w-4 text-center`}></i>
                                        <span>{option.label}</span>
                                    </button>
                                ))}
                            </div>
                        )}
                    </div>
                );
            }

            // Main Application Component
            function App() {
                const [accounts, setAccounts] = useState([]);
                const [loading, setLoading] = useState(true);
                const [filter, setFilter] = useState('all');
                const [modalOpen, setModalOpen] = useState(false);
                const [editingAccount, setEditingAccount] = useState(null);
                
                // Form fields
                const [fullName, setFullName] = useState('');
                const [email, setEmail] = useState('');
                const [password, setPassword] = useState('');
                const [phone, setPhone] = useState('');
                const [role, setRole] = useState('customer');
                const [idPhotoUrl, setIdPhotoUrl] = useState('');
                const [signatureBase64, setSignatureBase64] = useState('');
                const [licenseNo, setLicenseNo] = useState('');
                const [licensePhotoUrl, setLicensePhotoUrl] = useState('');
                const [driverStatus, setDriverStatus] = useState('available');

                // Image Upload Progress States
                const [isUploadingPfp, setIsUploadingPfp] = useState(false);
                const [isUploadingLicense, setIsUploadingLicense] = useState(false);

                // Saving / Progress States (0-100%)
                const [isSaving, setIsSaving] = useState(false);
                const [saveProgress, setSaveProgress] = useState(0);
                const [saveStatus, setSaveStatus] = useState('');
                const [formError, setFormError] = useState('');

                // Password generator & clipboard states
                const [showPassword, setShowPassword] = useState(false);
                const [copied, setCopied] = useState(false);

                const generateSecurePassword = () => {
                    const length = 12;
                    const charset = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+~`|}{[]:;?><,./-=';
                    let retVal = '';
                    const uppercase = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
                    const lowercase = 'abcdefghijklmnopqrstuvwxyz';
                    const numbers = '0123456789';
                    const symbols = '!@#$%^&*()_+=-';
                    
                    retVal += uppercase[Math.floor(Math.random() * uppercase.length)];
                    retVal += lowercase[Math.floor(Math.random() * lowercase.length)];
                    retVal += numbers[Math.floor(Math.random() * numbers.length)];
                    retVal += symbols[Math.floor(Math.random() * symbols.length)];
                    
                    for (let i = 4; i < length; i++) {
                        retVal += charset[Math.floor(Math.random() * charset.length)];
                    }
                    return retVal.split('').sort(() => 0.5 - Math.random()).join('');
                };

                const handleSuggestPassword = () => {
                    const securePass = generateSecurePassword();
                    setPassword(securePass);
                    setShowPassword(true);
                };

                const handleCopyToClipboard = () => {
                    if (!password) return;
                    navigator.clipboard.writeText(password);
                    setCopied(true);
                    setTimeout(() => setCopied(false), 2000);
                };

                const [toast, setToast] = useState(null);

                const showToast = (message, type = 'success') => {
                    setToast({ message, type });
                    setTimeout(() => setToast(null), 3500);
                };

                // Fetch Accounts
                const fetchAccounts = async (roleFilter = filter) => {
                    setLoading(true);
                    try {
                        const url = `${API_BASE_URL}/admin/accounts?role=${roleFilter}`;
                        const response = await fetch(url);
                        if (!response.ok) throw new Error('API Request Failed');
                        const data = await response.json();
                        setAccounts(data);
                    } catch (error) {
                        showToast('Error retrieving records: ' + error.message, 'error');
                    } finally {
                        setLoading(false);
                    }
                };

                useEffect(() => {
                    fetchAccounts(filter);
                }, [filter]);

                // SignalR Connection
                useEffect(() => {
                    let connection = null;
                    try {
                        const hubUrl = API_BASE_URL.replace('/api', '') + '/hubs/admin';
                        connection = new signalR.HubConnectionBuilder()
                            .withUrl(hubUrl)
                            .withAutomaticReconnect()
                            .build();

                        connection.on('ReceiveAccountsUpdate', () => {
                            fetchAccounts(filter);
                        });

                        connection.start()
                            .then(() => console.log('SignalR connected to Accounts Panel'))
                            .catch(err => console.error('SignalR failed starting: ', err));
                    } catch (e) {
                        console.error('SignalR failed setup: ', e);
                    }

                    return () => {
                        if (connection) connection.stop();
                    };
                }, [filter]);

                const openCreateModal = () => {
                    setEditingAccount(null);
                    setFullName('');
                    setEmail('');
                    setPassword('');
                    setPhone('');
                    setRole('customer');
                    setIdPhotoUrl('');
                    setSignatureBase64('');
                    setLicenseNo('');
                    setLicensePhotoUrl('');
                    setDriverStatus('available');
                    setIsUploadingPfp(false);
                    setIsUploadingLicense(false);
                    setShowPassword(false);
                    setCopied(false);
                    setFormError('');
                    setIsSaving(false);
                    setSaveProgress(0);
                    setSaveStatus('');
                    setModalOpen(true);
                };

                const openEditModal = (account) => {
                    setEditingAccount(account);
                    setFullName(account.fullName);
                    setEmail(account.email);
                    setPassword('');
                    setPhone(account.phone);
                    setRole(account.role);
                    setIdPhotoUrl(resolvePhotoUrl(account.idPhotoUrl || account.avatarBase64 || account.photoUrl || ''));
                    setSignatureBase64(resolvePhotoUrl(account.signatureBase64 || account.signatureUrl || ''));
                    setLicenseNo(account.licenseNo || '');
                    setLicensePhotoUrl(resolvePhotoUrl(account.licensePhotoUrl || ''));
                    setDriverStatus(account.driverStatus || 'available');
                    setIsUploadingPfp(false);
                    setIsUploadingLicense(false);
                    setShowPassword(false);
                    setCopied(false);
                    setFormError('');
                    setIsSaving(false);
                    setSaveProgress(0);
                    setSaveStatus('');
                    setModalOpen(true);
                };

                const handleSubmit = async (e) => {
                    e.preventDefault();

                    // Prevent click if image files are transferring to Firebase/Supabase
                    if (isUploadingPfp || isUploadingLicense) {
                        showToast('Please wait for file transfers to complete.', 'error');
                        return;
                    }

                    setFormError('');
                    setIsSaving(true);
                    setSaveProgress(15);
                    setSaveStatus('Validating form details...');

                    const payload = {
                        fullName,
                        email,
                        password,
                        phone,
                        role,
                        idPhotoUrl: idPhotoUrl || null,
                        avatarBase64: idPhotoUrl || null,
                        signatureBase64: signatureBase64 || null,
                        licenseNo: role.toLowerCase() === 'driver' ? licenseNo : null,
                        licensePhotoUrl: role.toLowerCase() === 'driver' ? licensePhotoUrl : null,
                        driverStatus: role.toLowerCase() === 'driver' ? driverStatus : null
                    };

                    try {
                        const isEdit = !!editingAccount;
                        const url = isEdit 
                            ? `${API_BASE_URL}/admin/accounts/${editingAccount.userId}` 
                            : `${API_BASE_URL}/admin/accounts`;
                        
                        setSaveProgress(45);
                        setSaveStatus(isEdit ? 'Saving profile and digital signature...' : 'Registering new account in database...');

                        const response = await fetch(url, {
                            method: isEdit ? 'PUT' : 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        });

                        if (!response.ok) {
                            const errBody = await response.json().catch(() => ({}));
                            throw new Error(errBody.message || 'API request failed');
                        }

                        setSaveProgress(85);
                        setSaveStatus('Synchronizing database records...');

                        setTimeout(() => {
                            setSaveProgress(100);
                            setSaveStatus('Saved!');
                            setTimeout(() => {
                                setIsSaving(false);
                                setModalOpen(false);
                                showToast(isEdit ? 'Account updated successfully!' : 'Account registered successfully!', 'success');
                                fetchAccounts(filter);
                            }, 350);
                        }, 250);
                    } catch (error) {
                        setIsSaving(false);
                        const msg = error.message || 'Failed to save account.';
                        setFormError(msg);
                        showToast(msg, 'error');
                    }
                };

                const handleDelete = async (userId) => {
                    if (!confirm('Are you sure you want to permanently delete this user account?')) return;

                    try {
                        const response = await fetch(`${API_BASE_URL}/admin/accounts/${userId}`, {
                            method: 'DELETE'
                        });

                        if (!response.ok) throw new Error('API request failed');

                        showToast('Account successfully removed!');
                        fetchAccounts(filter);
                    } catch (error) {
                        showToast(error.message, 'error');
                    }
                };

                // Render Live 3D Digital Card Preview
                const renderLivePreviewCard = () => {
                    const cleanName = fullName || 'Your Name';
                    const activeRole = role || 'customer';
                    
                    if (activeRole.toLowerCase() === 'admin') {
                        return (
                            <EmployeeIDCard 
                                fullName={cleanName}
                                role='SYSTEM ADMIN'
                                idPhotoUrl={idPhotoUrl}
                                signatureBase64={signatureBase64}
                                createdAt={editingAccount?.createdAt}
                            />
                        );
                    } else if (activeRole.toLowerCase() === 'driver') {
                        return (
                            <DriversLicenseCard 
                                fullName={cleanName}
                                idPhotoUrl={idPhotoUrl}
                                signatureBase64={signatureBase64}
                                licenseNo={licenseNo}
                                driverStatus={driverStatus}
                            />
                        );
                    } else {
                        return (
                            <MembershipCard 
                                fullName={cleanName}
                                idPhotoUrl={idPhotoUrl}
                                signatureBase64={signatureBase64}
                                createdAt={editingAccount?.createdAt}
                            />
                        );
                    }
                };

                return (
                    <div className='w-full max-w-7xl mx-auto'>
                        {/* Title Section */}
                        <div className={`flex flex-col md:flex-row md:items-center justify-between gap-4 mb-8 p-6 rounded-2xl border backdrop-blur-md relative z-[100] ${IS_DARK_THEME ? 'bg-slate-900/30 border-slate-800/40' : 'bg-white/50 border-slate-200'}`}>
                            <div>
                                <h1 className='text-2xl font-extrabold tracking-tight bg-gradient-to-r from-orange-500 to-amber-500 bg-clip-text text-transparent'>Accounts Control Panel</h1>
                                <p className={`mt-1 text-xs ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Audit, configure, register, and update system profiles dynamically.</p>
                            </div>
                            <div className='flex items-center gap-4'>
                                <RoleFilterDropdown activeFilter={filter} onChange={setFilter} />
                                <button 
                                    onClick={openCreateModal}
                                    className='px-5 py-3 rounded-xl bg-orange-600 hover:bg-orange-500 font-semibold flex items-center gap-2 shadow-[0_0_20px_rgba(234,88,12,0.3)] hover:-translate-y-[2px] transition-all duration-300 text-white border border-orange-500/50'
                                >
                                    <i className='fa-solid fa-plus text-sm'></i>
                                    <span className='text-sm'>New Account</span>
                                </button>
                            </div>
                        </div>

                        {/* List grid cards */}
                        {loading ? (
                            <div className='flex flex-col justify-center items-center py-24 gap-4'>
                                <div className='w-10 h-10 rounded-full border-4 border-orange-500 border-t-transparent animate-spin'></div>
                                <p className='text-orange-500 font-semibold tracking-wider text-xs'>Querying Server Accounts...</p>
                            </div>
                        ) : accounts.length === 0 ? (
                            <div className={`glass-card rounded-2xl py-24 text-center border max-w-xl mx-auto ${IS_DARK_THEME ? 'border-slate-800/40' : 'border-slate-200/50'}`}>
                                <i className='fa-regular fa-folder-open text-4xl text-slate-500 mb-4'></i>
                                <h3 className={`font-bold text-base ${IS_DARK_THEME ? 'text-slate-350' : 'text-slate-800'}`}>No accounts matched your search</h3>
                                <p className='text-slate-500 mt-1 text-xs'>Try switching filters or register a new identity profile.</p>
                            </div>
                        ) : (
                            <div className='grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6'>
                                {accounts.map((acc, idx) => (
                                    <div key={acc.userId} className='card-stagger' style={{ animationDelay: `${idx * 60}ms` }}>
                                        <AccountCard 
                                            account={acc} 
                                            onEdit={openEditModal}
                                            onDelete={handleDelete}
                                        />
                                    </div>
                                ))}
                            </div>
                        )}

                        {/* Modal Box with 2-column layout (Inputs + Live 3D Card Preview) */}
                        {modalOpen && (
                            <div className='fixed inset-0 bg-black/60 backdrop-blur-md flex items-center justify-center z-[999] p-4 overflow-y-auto'>
                            <div className={`modal-enter glass-card border rounded-2xl w-full max-w-4xl p-8 relative shadow-2xl flex flex-col justify-between max-h-[90vh] overflow-y-auto ${IS_DARK_THEME ? 'border-slate-800 bg-slate-900' : 'border-slate-200 bg-white'}`}>
                                    <div>
                                        <button 
                                            onClick={() => setModalOpen(false)}
                                            className='absolute top-6 right-6 text-slate-500 hover:text-orange-500 hover:scale-110 transition-all font-bold text-lg'
                                        >
                                            <i className='fa-solid fa-xmark'></i>
                                        </button>
                                        
                                        <h2 className='text-xl font-bold bg-gradient-to-r from-orange-500 to-amber-500 bg-clip-text text-transparent mb-6'>
                                            {editingAccount ? 'Edit Profile Details' : 'Register Profile'}
                                        </h2>

                                        <div className='flex flex-col lg:flex-row gap-8 items-stretch'>
                                            {/* Left side: Inputs */}
                                            <form onSubmit={handleSubmit} className='flex-1 space-y-4 text-xs'>
                                                {/* Form Error Banner */}
                                                {formError && (
                                                    <div className='p-3.5 rounded-xl bg-red-500/15 border border-red-500/30 text-red-400 flex items-start gap-2.5 text-xs animate-shake'>
                                                        <i className='fa-solid fa-circle-exclamation text-base mt-0.5 shrink-0'></i>
                                                        <div>
                                                            <strong className='font-bold block uppercase text-[10px] tracking-wider'>Save Error</strong>
                                                            <span>{formError}</span>
                                                        </div>
                                                    </div>
                                                )}

                                                <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
                                                    <div>
                                                        <label className={`block font-semibold uppercase tracking-wider mb-1.5 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Full Name</label>
                                                        <input 
                                                            type='text' 
                                                            required 
                                                            value={fullName}
                                                            onChange={e => setFullName(e.target.value)}
                                                            className={`w-full px-4 py-2.5 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors ${IS_DARK_THEME ? 'bg-slate-900 border-slate-800 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                        />
                                                    </div>
                                                    <div>
                                                        <label className={`block font-semibold uppercase tracking-wider mb-1.5 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Email Address</label>
                                                        <input 
                                                            type='email' 
                                                            required 
                                                            value={email}
                                                            onChange={e => setEmail(e.target.value)}
                                                            className={`w-full px-4 py-2.5 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors ${IS_DARK_THEME ? 'bg-slate-900 border-slate-800 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                        />
                                                    </div>
                                                </div>

                                                <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
                                                    <div>
                                                        <label className={`block font-semibold uppercase tracking-wider mb-1.5 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Password {editingAccount && <span className='text-slate-500 text-[10px]'>(Skip to keep)</span>}</label>
                                                        <div className='relative flex items-center'>
                                                            <input 
                                                                type={showPassword ? 'text' : 'password'} 
                                                                required={!editingAccount}
                                                                value={password}
                                                                onChange={e => setPassword(e.target.value)}
                                                                className={`w-full pl-4 pr-20 py-2.5 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors ${IS_DARK_THEME ? 'bg-slate-900 border-slate-800 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                            />
                                                            <div className='absolute right-2 flex items-center gap-1.5'>
                                                                {password && (
                                                                    <button 
                                                                        type='button'
                                                                        onClick={handleCopyToClipboard}
                                                                        className={`w-7 h-7 rounded-lg flex items-center justify-center transition-all ${
                                                                            IS_DARK_THEME ? 'text-slate-400 hover:text-orange-500 hover:bg-slate-800' : 'text-slate-600 hover:text-orange-500 hover:bg-slate-200'
                                                                        }`}
                                                                        title={copied ? 'Copied!' : 'Copy to Clipboard'}
                                                                    >
                                                                        <i className={`fa-solid ${copied ? 'fa-check text-emerald-500' : 'fa-copy'}`}></i>
                                                                    </button>
                                                                )}
                                                                <button 
                                                                    type='button'
                                                                    onClick={() => setShowPassword(!showPassword)}
                                                                    className={`w-7 h-7 rounded-lg flex items-center justify-center transition-all ${
                                                                        IS_DARK_THEME ? 'text-slate-400 hover:text-orange-500 hover:bg-slate-800' : 'text-slate-600 hover:text-orange-500 hover:bg-slate-200'
                                                                    }`}
                                                                    title={showPassword ? 'Hide Password' : 'Show Password'}
                                                                >
                                                                    <i className={`fa-solid ${showPassword ? 'fa-eye-slash' : 'fa-eye'}`}></i>
                                                                </button>
                                                            </div>
                                                        </div>
                                                        <div className='mt-1.5 flex items-center justify-between min-h-[16px]'>
                                                            <button 
                                                                type='button' 
                                                                onClick={handleSuggestPassword}
                                                                className='text-[10px] text-orange-500 hover:text-orange-400 font-bold flex items-center gap-1.5 hover:underline transition-all'
                                                            >
                                                                <i className='fa-solid fa-wand-magic-sparkles'></i>
                                                                Suggest Secure Password
                                                            </button>
                                                            {copied && (
                                                                <span className='text-[9px] text-emerald-500 font-bold uppercase tracking-wider flex items-center gap-1 animate-pulse'>
                                                                    <i className='fa-solid fa-circle-check'></i> Copied!
                                                                </span>
                                                            )}
                                                        </div>
                                                    </div>
                                                    <div>
                                                        <label className={`block font-semibold uppercase tracking-wider mb-1.5 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Phone</label>
                                                        <input 
                                                            type='text' 
                                                            value={phone}
                                                            onChange={e => setPhone(e.target.value)}
                                                            className={`w-full px-4 py-2.5 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors ${IS_DARK_THEME ? 'bg-slate-900 border-slate-800 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                        />
                                                    </div>
                                                </div>

                                                <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
                                                    <div>
                                                        <label className={`block font-semibold uppercase tracking-wider mb-1.5 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Account Role</label>
                                                        <select 
                                                            value={role} 
                                                            onChange={e => setRole(e.target.value)}
                                                            className={`w-full px-4 py-2.5 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors appearance-none ${IS_DARK_THEME ? 'bg-slate-900 border-slate-800 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                        >
                                                            <option value='customer'>Customer</option>
                                                            <option value='driver'>Driver</option>
                                                            <option value='admin'>Admin</option>
                                                        </select>
                                                    </div>
                                                    
                                                    {/* File picker replacement of plaintext URL */}
                                                    <FileUploader 
                                                        label='Profile Photo'
                                                        value={idPhotoUrl}
                                                        onChange={setIdPhotoUrl}
                                                        folderName='pfp'
                                                        isUploading={isUploadingPfp}
                                                        setIsUploading={setIsUploadingPfp}
                                                        onNotify={showToast}
                                                    />
                                                </div>

                                                {/* E-Signature Uploader with Automatic Background Removal */}
                                                <div className='pt-1'>
                                                    <SignatureUploader 
                                                        value={signatureBase64}
                                                        onChange={setSignatureBase64}
                                                        onNotify={showToast}
                                                    />
                                                </div>

                                                {/* Driver Specs layout */}
                                                {role === 'driver' && (
                                                    <div className={`p-4 rounded-2xl border space-y-4 ${IS_DARK_THEME ? 'bg-slate-950/50 border-slate-800' : 'bg-slate-100/50 border-slate-200'}`}>
                                                        <h4 className='text-xs font-bold text-orange-500 uppercase tracking-wide border-b border-slate-800 pb-2 mb-2'>Driver License Specifications</h4>
                                                        <div className='grid grid-cols-1 sm:grid-cols-2 gap-4'>
                                                            <div>
                                                                <label className={`block font-semibold mb-1 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>License No.</label>
                                                                <input 
                                                                    type='text' 
                                                                    required={role === 'driver'}
                                                                    value={licenseNo}
                                                                    onChange={e => setLicenseNo(e.target.value)}
                                                                    placeholder='e.g. N01-12-345678'
                                                                    className={`w-full px-4 py-2 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors ${IS_DARK_THEME ? 'bg-slate-900 border-slate-850 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                                />
                                                            </div>
                                                            
                                                            {/* File picker for Driver License Image */}
                                                            <FileUploader 
                                                                label='License Photo'
                                                                value={licensePhotoUrl}
                                                                onChange={setLicensePhotoUrl}
                                                                folderName='licenses'
                                                                isUploading={isUploadingLicense}
                                                                setIsUploading={setIsUploadingLicense}
                                                                onNotify={showToast}
                                                            />
                                                        </div>
                                                        <div>
                                                            <label className={`block font-semibold mb-1 ${IS_DARK_THEME ? 'text-slate-400' : 'text-slate-500'}`}>Driver Availability Status</label>
                                                            <select 
                                                                value={driverStatus} 
                                                                onChange={e => setDriverStatus(e.target.value)}
                                                                className={`w-full px-4 py-2 rounded-xl border focus:border-orange-500 focus:outline-none transition-colors appearance-none ${IS_DARK_THEME ? 'bg-slate-900 border-slate-850 text-slate-250' : 'bg-slate-100 border-slate-200 text-slate-900'}`}
                                                            >
                                                                <option value='available'>Available</option>
                                                                <option value='busy'>Busy / Assigned</option>
                                                                <option value='inactive'>Inactive / On break</option>
                                                            </select>
                                                        </div>
                                                    </div>
                                                )}

                                                <div className='pt-6 flex flex-col gap-3'>
                                                    {/* Real-time Saving 0-100% Progress Bar */}
                                                    {isSaving && (
                                                        <div className='w-full p-3 rounded-xl bg-orange-500/10 border border-orange-500/20 space-y-2'>
                                                            <div className='flex items-center justify-between text-[10px] font-bold text-orange-400'>
                                                                <span className='flex items-center gap-1.5 truncate'>
                                                                    <i className='fa-solid fa-spinner animate-spin'></i> {saveStatus}
                                                                </span>
                                                                <span className='font-mono text-xs'>{saveProgress}%</span>
                                                            </div>
                                                            <div className='w-full h-2 bg-slate-800 rounded-full overflow-hidden border border-orange-500/30'>
                                                                <div 
                                                                    className='h-full bg-gradient-to-r from-orange-500 via-amber-400 to-emerald-400 transition-all duration-300 ease-out rounded-full' 
                                                                    style={{ width: `${saveProgress}%` }}
                                                                ></div>
                                                            </div>
                                                        </div>
                                                    )}

                                                    <div className='flex justify-end gap-3'>
                                                        <button 
                                                            type='button'
                                                            disabled={isSaving}
                                                            onClick={() => setModalOpen(false)}
                                                            className={`px-5 py-2.5 rounded-xl border font-semibold transition-all ${
                                                                isSaving ? 'opacity-50 cursor-not-allowed border-slate-800 text-slate-500' : (IS_DARK_THEME ? 'border-slate-800 hover:bg-slate-900 text-slate-400 hover:text-slate-250' : 'border-slate-200 hover:bg-slate-100 text-slate-650 hover:text-slate-850')
                                                            }`}
                                                        >
                                                            Cancel
                                                        </button>
                                                        <button 
                                                            type='submit'
                                                            disabled={isSaving || isUploadingPfp || isUploadingLicense}
                                                            className={`px-6 py-2.5 rounded-xl font-semibold shadow-lg text-white transition-all flex items-center gap-2 ${
                                                                (isSaving || isUploadingPfp || isUploadingLicense) 
                                                                    ? 'bg-slate-700/50 cursor-not-allowed border border-slate-800 text-slate-400' 
                                                                    : 'bg-orange-600 hover:bg-orange-500 hover:scale-[1.02]'
                                                            }`}
                                                        >
                                                            {isSaving ? (
                                                                <>
                                                                    <i className='fa-solid fa-spinner animate-spin'></i>
                                                                    <span>Saving ({saveProgress}%)...</span>
                                                                </>
                                                            ) : (
                                                                <span>{editingAccount ? 'Save Changes' : 'Register Account'}</span>
                                                            )}
                                                        </button>
                                                    </div>
                                                </div>
                                            </form>

                                            {/* Right side: 3D Holographic Digital Card Live Preview */}
                                            <div className={`w-full lg:w-[360px] flex flex-col justify-center items-center p-6 rounded-2xl border border-dashed select-none relative overflow-hidden ${
                                                IS_DARK_THEME 
                                                    ? 'bg-slate-950/20 border-slate-800/80' 
                                                    : 'bg-slate-100/30 border-slate-200'
                                            }`}>
                                                <span className={`text-[9px] font-bold uppercase tracking-widest mb-4 ${
                                                    IS_DARK_THEME ? 'text-slate-500' : 'text-slate-450'
                                                }`}>Interactive 3D Digital Card</span>
                                                
                                                <div className='perspective-1000 flex items-center justify-center min-h-[220px] w-full'>
                                                    {renderLivePreviewCard()}
                                                </div>
                                                
                                                <p className={`text-[8px] text-center mt-4 ${
                                                    IS_DARK_THEME ? 'text-slate-550' : 'text-slate-400'
                                                }`}>Move your mouse over the card preview to experience premium 3D tilt shading.</p>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* Toast notifications (Top-Right) */}
                        {toast && (
                            <div className={`fixed top-8 right-8 z-[99999] px-6 py-4 rounded-xl shadow-2xl border flex items-center gap-3 transition-all duration-300 scale-100 ${
                                toast.type === 'error' 
                                    ? 'bg-red-500/20 text-red-400 border-red-500/30' 
                                    : 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30'
                            }`}>
                                <i className={`fa-solid ${toast.type === 'error' ? 'fa-triangle-exclamation' : 'fa-circle-check'} text-lg`}></i>
                                <span className='font-semibold text-sm'>{toast.message}</span>
                            </div>
                        )}
                    </div>
                );
            }

            const rootContainer = document.getElementById('root');
            const reactRoot = ReactDOM.createRoot(rootContainer);
            reactRoot.render(<App />);
            </script>");
            
            sb.Append("</body>");
            sb.Append("</html>");

            return sb.ToString();
        }
    }
}
