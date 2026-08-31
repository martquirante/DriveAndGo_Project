#nullable disable
using System;
using System.Text;

namespace DriveAndGo_Admin.Panels
{
    /// <summary>
    /// Generates the enterprise HTML/CSS/JS for the Calendar panel with:
    /// - Zero emoji icons (clean SVG vector icons throughout)
    /// - Direct Year & Month dropdown selectors + instant client-side navigation
    /// - Real-time Search Box (filter by vehicle, plate, customer, driver, date, note)
    /// - 100% complete details for rentals (customer PFP, driver PFP, specs, blockchain seal)
    /// - Direct vehicle photo & API brand/payment logos
    /// - Company-wide events: Maintenance, Birthdays, and interactive Admin Notes
    /// </summary>
    internal static class CalendarHtmlGenerator
    {
        public static string Build(int year, int month, string eventsJson, string view, bool dark, string apiBase = "")
        {
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                apiBase = "http://localhost:5233/api";
            }
            apiBase = apiBase.TrimEnd('/');

            string bg   = dark ? "#0b1329" : "#f1f5f9";
            string card = dark ? "#131f37" : "#ffffff";
            string txt  = dark ? "#f8fafc" : "#0f172a";
            string sub  = dark ? "#94a3b8" : "#64748b";
            string bdr  = dark ? "#223150" : "#e2e8f0";

            string hBg   = dark ? "#0f172a" : "#ffffff";
            string hBdr  = dark ? "rgba(255,255,255,.08)" : "rgba(0,0,0,.08)";
            string btn   = dark ? "rgba(255,255,255,.06)" : "rgba(0,0,0,.04)";
            string btnBr = dark ? "rgba(255,255,255,.12)" : "rgba(0,0,0,.1)";
            string tab   = dark ? "rgba(255,255,255,.06)" : "rgba(0,0,0,.03)";
            string tabBr = dark ? "rgba(255,255,255,.1)"  : "rgba(0,0,0,.08)";

            var sb = new StringBuilder(40960);

            // ── HEAD & STYLES ─────────────────────────────────────────────
            sb.Append("<!DOCTYPE html><html lang='en'><head>");
            sb.Append("<meta charset='UTF-8'>");
            sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.Append("<title>DriveAndGo Master Calendar</title>");
            sb.Append("<style>");

            sb.Append("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendFormat("body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;background:{0};color:{1};height:100vh;display:flex;flex-direction:column;overflow:hidden;user-select:none}}", bg, txt);
            sb.Append("svg{width:14px;height:14px;min-width:14px;min-height:14px;max-width:24px;max-height:24px;flex-shrink:0;vertical-align:middle;display:inline-block}");
            sb.Append(".w-3{width:12px!important;height:12px!important;min-width:12px!important;min-height:12px!important}");
            sb.Append(".w-4{width:14px!important;height:14px!important;min-width:14px!important;min-height:14px!important}");
            sb.Append(".w-5{width:18px!important;height:18px!important;min-width:18px!important;min-height:18px!important}");
            sb.Append(".w-6{width:24px!important;height:24px!important;min-width:24px!important;min-height:24px!important}");

            // Topbar
            sb.AppendFormat(".topbar{{background:{0};padding:10px 20px;display:flex;align-items:center;gap:12px;border-bottom:1px solid {1};flex-shrink:0}}", hBg, hBdr);
            sb.Append(".brand{font-size:13px;font-weight:700;color:#f97316;display:flex;align-items:center;gap:6px}");
            sb.Append(".brand svg{width:16px;height:16px;stroke:#f97316}");
            sb.AppendFormat(".clock{{font-size:13px;color:{0};margin-left:auto;font-variant-numeric:tabular-nums}}", sub);
            sb.AppendFormat(".ddisp{{font-size:13px;font-weight:600;color:{0}}}", txt);

            // Navbar
            sb.AppendFormat(".navbar{{background:{0};padding:10px 20px;display:flex;align-items:center;gap:10px;border-bottom:1px solid {1};flex-shrink:0;flex-wrap:wrap}}", hBg, hBdr);
            sb.AppendFormat(".nb{{background:{0};border:1px solid {1};color:{2};width:34px;height:34px;border-radius:8px;cursor:pointer;display:flex;align-items:center;justify-content:center;transition:all .15s}}", btn, btnBr, txt);
            sb.Append(".nb:hover{background:rgba(249,115,22,.2);border-color:#f97316;color:#f97316}");
            sb.Append(".nb svg{width:16px;height:16px;stroke:currentColor}");

            sb.Append(".nav-selectors{display:flex;align-items:center;gap:6px}");
            sb.AppendFormat(".sel-control{{background:{0};border:1px solid {1};color:{2};font-size:13px;font-weight:700;padding:6px 10px;border-radius:8px;cursor:pointer;outline:none;transition:border-color .15s}}", card, bdr, txt);
            sb.Append(".sel-control:focus,.sel-control:hover{border-color:#f97316}");

            sb.Append(".tbtn{background:#f97316;border:none;color:#fff;font-size:11px;font-weight:700;padding:7px 14px;border-radius:8px;cursor:pointer;transition:filter .15s;display:flex;align-items:center;gap:5px}");
            sb.Append(".tbtn:hover{filter:brightness(1.15)}");

            sb.Append(".add-note-btn{background:rgba(14,165,233,.15);border:1px solid rgba(14,165,233,.35);color:#38bdf8;font-size:11px;font-weight:700;padding:7px 14px;border-radius:8px;cursor:pointer;transition:all .15s;display:flex;align-items:center;gap:5px}");
            sb.Append(".add-note-btn:hover{background:rgba(14,165,233,.25);border-color:#38bdf8}");
            sb.Append(".add-note-btn svg{width:14px;height:14px;stroke:currentColor}");

            // Search Box
            sb.AppendFormat(".cal-search-box{{display:flex;align-items:center;gap:8px;background:{0};border:1px solid {1};padding:6px 12px;border-radius:10px;min-width:240px;max-width:340px;transition:all .15s}}", card, bdr);
            sb.Append(".cal-search-box:focus-within{border-color:#f97316;box-shadow:0 0 0 2px rgba(249,115,22,.15)}");
            sb.AppendFormat(".cal-search-box svg{{width:14px;height:14px;stroke:{0};flex-shrink:0}}", sub);
            sb.AppendFormat(".cal-search-inp{{background:transparent;border:none;color:{0};font-size:12px;font-weight:600;outline:none;width:100%}}", txt);
            sb.AppendFormat(".cal-search-inp::placeholder{{color:{0};font-weight:500}}", sub);
            sb.AppendFormat(".cal-search-clear{{background:none;border:none;color:{0};cursor:pointer;padding:2px;display:none;align-items:center;justify-content:center}}", sub);
            sb.Append(".cal-search-clear:hover{color:#ef4444}");

            sb.Append(".vtabs{display:flex;gap:4px;margin-left:auto}");
            sb.AppendFormat(".vtab{{background:{0};border:1px solid {1};color:{2};font-size:11px;font-weight:600;padding:6px 14px;border-radius:8px;cursor:pointer;transition:all .15s}}", tab, tabBr, sub);
            sb.Append(".vtab.on,.vtab:hover{background:#f97316;border-color:#f97316;color:#fff}");

            // Filter & Category Bar
            sb.AppendFormat(".filbar{{padding:8px 20px;display:flex;align-items:center;gap:8px;background:{0};border-bottom:1px solid {1};flex-shrink:0;overflow-x:auto}}", hBg, hBdr);
            sb.AppendFormat(".flbl{{font-size:11px;font-weight:700;color:{0};text-transform:uppercase;letter-spacing:.8px;margin-right:4px}}", sub);
            sb.AppendFormat(".fpill{{padding:4px 12px;border-radius:20px;font-size:11px;font-weight:600;cursor:pointer;border:1px solid {0};background:transparent;color:{1};transition:all .15s;display:flex;align-items:center;gap:5px}}", bdr, sub);
            sb.Append(".fpill:hover{border-color:#f97316;color:#f97316}");
            sb.Append(".fpill.on{background:#f97316;border-color:#f97316;color:#fff}");
            sb.Append(".fpill svg{width:12px;height:12px;stroke:currentColor}");

            // Stats Bar
            sb.AppendFormat(".sbar{{display:flex;gap:10px;padding:8px 20px;background:{0};border-bottom:1px solid {1};flex-shrink:0;overflow-x:auto}}", hBg, hBdr);
            sb.AppendFormat(".sc{{background:{0};border:1px solid {1};border-radius:10px;padding:6px 14px;text-align:center;min-width:85px;cursor:pointer;transition:all .15s;user-select:none;position:relative}}", card, bdr);
            sb.Append(".sc:hover{border-color:#f97316;transform:translateY(-1px);box-shadow:0 4px 12px rgba(249,115,22,.15)}");
            sb.Append(".sc.on{border-color:#f97316;background:rgba(249,115,22,.12);box-shadow:0 0 0 2px rgba(249,115,22,.28)}");
            sb.Append(".sc.sc-info{cursor:default}");
            sb.Append(".sc.sc-info:hover{border-color:inherit;transform:none;box-shadow:none}");
            sb.Append(".sv{font-size:16px;font-weight:800}");
            sb.AppendFormat(".sl{{font-size:9px;color:{0};text-transform:uppercase;letter-spacing:.8px;margin-top:2px}}", sub);

            // Calendar Body
            sb.Append(".cbody{flex:1;overflow-y:auto;padding:12px 20px 20px}");

            // Month Grid
            sb.Append(".grid{display:grid;grid-template-columns:repeat(7,1fr);gap:6px}");
            sb.AppendFormat(".dh{{text-align:center;font-size:11px;font-weight:700;color:{0};padding:8px 0;text-transform:uppercase;letter-spacing:1px}}", sub);
            sb.AppendFormat(".cell{{background:{0};border:1px solid {1};border-radius:12px;min-height:105px;padding:6px;cursor:pointer;overflow:hidden;transition:all .15s;display:flex;flex-direction:column;gap:3px}}", card, bdr);
            sb.Append(".cell:hover{border-color:#f97316;transform:translateY(-1px);box-shadow:0 6px 20px rgba(249,115,22,.15)}");
            sb.Append(".cell.other{opacity:.3}");
            sb.Append(".cell.today{border:2px solid #3b82f6;box-shadow:0 0 0 3px rgba(59,130,246,.2)}");
            sb.Append(".cell.matched{border:2px solid #f97316;background:rgba(249,115,22,.08)}");
            sb.Append(".cell-top{display:flex;align-items:center;justify-content:space-between;margin-bottom:2px}");
            sb.AppendFormat(".dn{{font-size:11px;font-weight:700;color:{0}}}", txt);
            sb.Append(".tdot{width:22px;height:22px;border-radius:50%;background:#3b82f6;color:#fff;font-size:11px;font-weight:800;display:inline-flex;align-items:center;justify-content:center}");
            sb.Append(".add-day-btn{opacity:0;transition:opacity .15s;background:none;border:none;color:#94a3b8;cursor:pointer;padding:2px}");
            sb.Append(".cell:hover .add-day-btn{opacity:1}");
            sb.Append(".add-day-btn:hover{color:#f97316}");
            sb.Append(".add-day-btn svg{width:12px;height:12px;stroke:currentColor}");

            // Event Pills
            sb.Append(".ev{border-radius:6px;padding:3px 6px;font-size:9.5px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;cursor:pointer;display:flex;align-items:center;gap:5px;transition:filter .12s}");
            sb.Append(".ev:hover{filter:brightness(1.25)}");
            sb.Append(".ev svg{width:11px;height:11px;stroke:currentColor;flex-shrink:0}");
            sb.Append(".ev-brand-badge{display:inline-flex;align-items:center;justify-content:center;width:16px;height:16px;border-radius:4px;background:#ffffff;padding:1.5px;flex-shrink:0;box-shadow:0 1px 3px rgba(0,0,0,0.25);overflow:hidden}");
            sb.Append(".ev-brand-badge img{width:100%;height:100%;object-fit:contain}");
            sb.Append(".ev-brand-badge .brand-fallback-txt{font-size:8px;font-weight:900;color:#0f172a;line-height:1;display:none}");
            sb.Append(".ep{background:rgba(245,158,11,.15);color:#f59e0b;border-left:3px solid #f59e0b}");
            sb.Append(".ea{background:rgba(34,197,94,.15);color:#22c55e;border-left:3px solid #22c55e}");
            sb.Append(".ex{background:rgba(59,130,246,.15);color:#3b82f6;border-left:3px solid #3b82f6}");
            sb.Append(".ec{background:rgba(148,163,184,.15);color:#94a3b8;border-left:3px solid #94a3b8}");
            sb.Append(".ek{background:rgba(239,68,68,.15);color:#ef4444;border-left:3px solid #ef4444}");

            // Special Events: Maintenance, Birthdays, Notes
            sb.Append(".ev-maint{background:rgba(217,119,6,.15);color:#d97706;border-left:3px solid #d97706}");
            sb.Append(".ev-bday{background:rgba(236,72,153,.15);color:#ec4899;border-left:3px solid #ec4899}");
            sb.Append(".ev-note{background:rgba(14,165,233,.15);color:#38bdf8;border-left:3px solid #38bdf8}");
            sb.AppendFormat(".more{{font-size:9px;font-weight:600;color:{0};margin-top:1px;padding-left:2px}}", sub);

            // Week/Day Grid
            sb.AppendFormat(".tlbl{{font-size:9px;color:{0};text-align:right;padding-right:8px;height:54px;padding-top:4px}}", sub);
            sb.AppendFormat(".dhdr{{text-align:center;padding:6px;font-size:10px;font-weight:700;color:{0};border-bottom:1px solid {1}}}", sub, bdr);
            sb.Append(".dhdr.tod{color:#3b82f6}");
            sb.AppendFormat(".hrow{{height:54px;border-bottom:1px solid {0};border-left:1px solid {0};padding:2px 4px}}", bdr);

            // Modals
            sb.Append(".mbg{position:fixed;inset:0;background:rgba(0,0,0,.75);z-index:99999;display:flex;align-items:center;justify-content:center;backdrop-filter:blur(6px);animation:fin .18s ease}");
            sb.Append("@keyframes fin{from{opacity:0}to{opacity:1}}");
            sb.AppendFormat(".modal{{background:{0};border:1px solid rgba(249,115,22,.4);border-radius:20px;width:560px;max-width:94vw;max-height:92vh;display:flex;flex-direction:column;box-shadow:0 35px 90px rgba(0,0,0,.7);animation:ms .2s cubic-bezier(.34,1.56,.64,1);overflow:hidden}}", card);
            sb.Append("@keyframes ms{from{opacity:0;transform:scale(.92)}to{opacity:1;transform:none}}");

            // Modal Sections
            sb.Append(".mhdr{background:linear-gradient(135deg,#13223f,#0a1020);padding:18px 22px;display:flex;align-items:center;gap:14px;border-bottom:1px solid rgba(255,255,255,.08)}");
            sb.Append(".mphoto{width:76px;height:52px;border-radius:10px;object-fit:cover;background:#0f172a;border:1px solid rgba(255,255,255,.1);flex-shrink:0}");
            sb.Append(".mhdr-info{flex:1;min-width:0}");
            sb.Append(".mtitle{font-size:16px;font-weight:800;color:#fff;display:flex;align-items:center;gap:10px}");
            sb.Append(".msub{font-size:12px;color:#94a3b8;margin-top:2px}");
            sb.Append(".brand-badge-box{display:inline-flex;align-items:center;justify-content:center;height:28px;background:transparent;padding:0;box-shadow:none;border:none;flex-shrink:0;overflow:visible}");
            sb.Append(".brand-badge-box img{height:100%;max-height:28px;max-width:48px;object-fit:contain}");
            sb.Append(".brand-badge-box .brand-fallback-txt{font-size:11px;font-weight:900;color:#f97316;display:none}");
            sb.Append(".pay-badge-box{display:inline-flex;align-items:center;justify-content:center;height:24px;background:transparent;padding:0;border:none;box-shadow:none;flex-shrink:0;overflow:visible}");
            sb.Append(".pay-badge-box img{height:100%;max-height:24px;max-width:72px;object-fit:contain}");
            sb.Append(".pay-badge-box .pay-fallback-txt{font-size:10px;font-weight:900;color:currentColor;display:none}");

            sb.Append(".mbody{padding:18px 22px;overflow-y:auto;flex:1;display:flex;flex-direction:column;gap:14px}");
            sb.AppendFormat(".msec{{background:rgba(255,255,255,.02);border:1px solid {0};border-radius:12px;padding:12px 14px}}", bdr);
            sb.AppendFormat(".msec-title{{font-size:10.5px;font-weight:700;color:{0};text-transform:uppercase;letter-spacing:.8px;margin-bottom:8px;display:flex;align-items:center;gap:6px}}", sub);
            sb.Append(".msec-title svg{width:13px;height:13px;stroke:currentColor}");

            sb.Append(".mgrid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}");
            sb.AppendFormat(".mrow{{display:flex;flex-direction:column;gap:2px}}", bdr);
            sb.AppendFormat(".mk{{font-size:10px;color:{0};text-transform:uppercase;letter-spacing:.6px}}", sub);
            sb.AppendFormat(".mv{{font-size:12.5px;font-weight:600;color:{0};word-break:break-word}}", txt);
            sb.Append(".click-val{cursor:pointer;display:inline-flex;align-items:center;gap:5px;transition:color .15s}");
            sb.Append(".click-val:hover{color:#f97316}");
            sb.Append(".click-val svg{width:12px;height:12px;stroke:currentColor}");

            // User & Driver Profile Avatars (PFPs)
            sb.Append(".cust-pfp{width:46px;height:46px;border-radius:50%;object-fit:cover;border:2px solid #f97316;background:#1e293b;flex-shrink:0}");
            sb.Append(".cust-pfp-fallback{width:46px;height:46px;border-radius:50%;background:linear-gradient(135deg,#f97316,#ea580c);color:#fff;font-size:15px;font-weight:800;display:flex;align-items:center;justify-content:center;border:2px solid rgba(255,255,255,.2);flex-shrink:0;letter-spacing:.5px}");
            sb.Append(".drv-pfp{width:46px;height:46px;border-radius:50%;object-fit:cover;border:2px solid #10b981;background:#1e293b;flex-shrink:0}");
            sb.Append(".drv-pfp-fallback{width:46px;height:46px;border-radius:50%;background:linear-gradient(135deg,#10b981,#059669);color:#fff;font-size:15px;font-weight:800;display:flex;align-items:center;justify-content:center;border:2px solid rgba(255,255,255,.2);flex-shrink:0;letter-spacing:.5px}");

            // Blockchain Seal Card
            sb.Append(".bc-card{background:linear-gradient(135deg,rgba(16,185,129,.12),rgba(5,150,105,.05));border:1px solid rgba(16,185,129,.3);border-radius:12px;padding:12px 14px}");
            sb.Append(".bc-hdr{display:flex;align-items:center;justify-content:space-between;margin-bottom:6px}");
            sb.Append(".bc-title{font-size:11px;font-weight:800;color:#10b981;display:flex;align-items:center;gap:6px}");
            sb.Append(".bc-title svg{width:14px;height:14px;stroke:#10b981}");
            sb.Append(".bc-hash{font-family:monospace;font-size:10px;color:#cbd5e1;background:rgba(0,0,0,.35);padding:6px 10px;border-radius:6px;word-break:break-all;border:1px solid rgba(255,255,255,.05);display:flex;align-items:center;justify-content:space-between;gap:8px}");
            sb.Append(".copy-btn{background:rgba(255,255,255,.1);border:none;color:#fff;padding:4px 8px;border-radius:4px;cursor:pointer;font-size:10px;font-weight:600;display:flex;align-items:center;gap:4px;flex-shrink:0;transition:background .15s}");
            sb.Append(".copy-btn:hover{background:#10b981}");
            sb.Append(".copy-btn svg{width:11px;height:11px;stroke:currentColor}");

            // Modal Footer
            sb.AppendFormat(".mfoot{{padding:14px 22px;border-top:1px solid {0};display:flex;align-items:center;gap:10px;background:rgba(0,0,0,.15)}}", bdr);
            sb.Append(".mclose{background:rgba(239,68,68,.12);border:1px solid rgba(239,68,68,.3);color:#ef4444;padding:9px 18px;border-radius:10px;cursor:pointer;font-size:12px;font-weight:700;transition:all .15s;display:flex;align-items:center;gap:6px}");
            sb.Append(".mclose:hover{background:rgba(239,68,68,.25)}");
            sb.Append(".mclose svg{width:13px;height:13px;stroke:currentColor}");

            sb.Append(".mdelete{background:rgba(239,68,68,.1);border:1px solid rgba(239,68,68,.3);color:#ef4444;padding:9px 18px;border-radius:10px;cursor:pointer;font-size:12px;font-weight:700;margin-left:auto;transition:all .15s;display:flex;align-items:center;gap:6px}");
            sb.Append(".mdelete:hover{background:#ef4444;color:#fff}");
            sb.Append(".mdelete svg{width:13px;height:13px;stroke:currentColor}");

            // Add Note Form Controls
            sb.AppendFormat(".form-group{{display:flex;flex-direction:column;gap:5px;margin-bottom:10px}}", bdr);
            sb.AppendFormat(".form-lbl{{font-size:11px;font-weight:700;color:{0};text-transform:uppercase;letter-spacing:.6px}}", sub);
            sb.AppendFormat(".form-inp{{background:{0};border:1px solid {1};color:{2};padding:8px 12px;border-radius:8px;font-size:12px;outline:none;transition:border-color .15s}}", card, bdr, txt);
            sb.Append(".form-inp:focus{border-color:#f97316}");
            sb.Append(".form-submit{background:#f97316;border:none;color:#fff;padding:9px 20px;border-radius:10px;cursor:pointer;font-size:12px;font-weight:700;margin-left:auto;transition:filter .15s;display:flex;align-items:center;gap:6px}");
            sb.Append(".form-submit:hover{filter:brightness(1.15)}");

            // Toast Alert (Top-Right Stacking)
            sb.Append(".toast-container{position:fixed;top:20px;right:20px;z-index:99999;display:flex;flex-direction:column;gap:8px;pointer-events:none;max-width:340px;width:100%}");
            sb.Append(".toast{pointer-events:auto;background:rgba(15,23,42,0.95);border:1px solid #22c55e;color:#fff;padding:10px 16px;border-radius:12px;font-size:12px;font-weight:600;display:flex;align-items:center;gap:10px;box-shadow:0 12px 35px rgba(0,0,0,0.6);backdrop-filter:blur(8px);animation:slideInRight .2s ease;transition:opacity .25s ease}");
            sb.Append(".toast svg{width:14px;height:14px;stroke:#22c55e;flex-shrink:0}");
            sb.Append("@keyframes slideInRight{from{opacity:0;transform:translateX(40px)}to{opacity:1;transform:translateX(0)}}");

            // Day View Responsive Cards
            sb.AppendFormat(".day-banner{{background:{0};border:1px solid {1};border-radius:14px;padding:14px 20px;display:flex;align-items:center;justify-content:space-between;box-shadow:0 4px 20px rgba(0,0,0,.15)}}", dark ? "linear-gradient(135deg,#1e293b,#0f172a)" : "linear-gradient(135deg,#ffffff,#f8fafc)", dark ? "rgba(255,255,255,.08)" : "rgba(0,0,0,.08)");
            sb.AppendFormat(".day-title{{font-size:16px;font-weight:800;color:{0}}}", txt);
            sb.AppendFormat(".day-card-title{{font-size:13.5px;font-weight:800;color:{0};display:flex;align-items:center;gap:8px}}", txt);
            sb.AppendFormat(".day-sub{{font-size:11.5px;color:{0};margin-top:2px;display:flex;align-items:center;gap:6px}}", sub);
            sb.AppendFormat(".day-empty{{background:{0};border:1px dashed {1};border-radius:12px;padding:32px 20px;text-align:center;color:{2}}}", dark ? "rgba(255,255,255,.02)" : "rgba(0,0,0,.02)", dark ? "rgba(255,255,255,.1)" : "rgba(0,0,0,.1)", sub);

            sb.Append("</style></head><body>");

            // ── TOPBAR ────────────────────────────────────────────────────
            sb.Append("<div class='topbar'>");
            sb.Append("<span class='brand'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/></svg>");
            sb.Append("DriveAndGo Master Calendar");
            sb.Append("</span>");
            sb.Append("<span class='ddisp' id='dd'></span>");
            sb.Append("<span class='clock' id='ck'></span>");
            sb.Append("</div>");

            // ── NAVBAR WITH YEAR/MONTH DROPDOWNS & REAL-TIME SEARCH ────────
            sb.Append("<div class='navbar'>");
            sb.Append("<button class='nb' onclick='navP()' title='Previous Month'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><polyline points='15 18 9 12 15 6'/></svg>");
            sb.Append("</button>");

            // Month & Year Selectors
            sb.Append("<div class='nav-selectors'>");
            sb.Append("<select class='sel-control' id='selMonth' onchange='onMonthSel(this.value)'>");
            string[] months = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            for (int mi = 1; mi <= 12; mi++)
            {
                sb.AppendFormat("<option value='{0}'{1}>{2}</option>", mi, mi == month ? " selected" : "", months[mi - 1]);
            }
            sb.Append("</select>");

            sb.Append("<select class='sel-control' id='selYear' onchange='onYearSel(this.value)'>");
            for (int yi = 2020; yi <= 2035; yi++)
            {
                sb.AppendFormat("<option value='{0}'{1}>{0}</option>", yi, yi == year ? " selected" : "");
            }
            sb.Append("</select>");
            sb.Append("</div>");

            sb.Append("<button class='nb' onclick='navN()' title='Next Month'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><polyline points='9 18 15 12 9 6'/></svg>");
            sb.Append("</button>");

            sb.Append("<button class='tbtn' onclick='goToday()'>");
            sb.Append("<svg width='12' height='12' fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><circle cx='12' cy='12' r='10'/><polyline points='12 6 12 12 16 14'/></svg>");
            sb.Append("TODAY");
            sb.Append("</button>");

            sb.Append("<button class='add-note-btn' onclick='openAddNoteModal()'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><line x1='12' x2='12' y1='5' y2='19'/><line x1='5' x2='19' y1='12' y2='12'/></svg>");
            sb.Append("+ Add Note / Reminder");
            sb.Append("</button>");

            // Search Bar Input
            sb.Append("<div class='cal-search-box'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/></svg>");
            sb.Append("<input type='text' id='calSearch' class='cal-search-inp' placeholder='Search plate, vehicle, customer, driver, date...' oninput='onSearchInput(this.value)' />");
            sb.Append("<button id='calClearBtn' class='cal-search-clear' onclick='clearSearch()' title='Clear Search'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><line x1='18' y1='6' x2='6' y2='18'/><line x1='6' y1='6' x2='18' y2='18'/></svg>");
            sb.Append("</button>");
            sb.Append("</div>");

            sb.Append("<div class='vtabs'>");
            sb.Append("<button class='vtab' id='vd' onclick='sv(\"day\")'>Day</button>");
            sb.Append("<button class='vtab' id='vw' onclick='sv(\"week\")'>Week</button>");
            sb.Append("<button class='vtab on' id='vm' onclick='sv(\"month\")'>Month</button>");
            sb.Append("</div></div>");

            // ── CATEGORY FILTER PILLS ─────────────────────────────────────
            sb.Append("<div class='filbar'>");
            sb.Append("<span class='flbl'>Filter:</span>");
            sb.Append("<button class='fpill on' id='fil-all' onclick='setFilter(\"all\")'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><circle cx='12' cy='12' r='10'/></svg> All Operations");
            sb.Append("</button>");
            sb.Append("<button class='fpill' id='fil-rentals' onclick='setFilter(\"rentals\")'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><path d='M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2'/><circle cx='7' cy='17' r='2'/><circle cx='17' cy='17' r='2'/></svg> Rentals");
            sb.Append("</button>");
            sb.Append("<button class='fpill' id='fil-maintenance' onclick='setFilter(\"maintenance\")'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><path d='M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z'/></svg> Maintenance");
            sb.Append("</button>");
            sb.Append("<button class='fpill' id='fil-birthdays' onclick='setFilter(\"birthdays\")'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><path d='M20 21v-8a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8M4 16s.5-1 2-1 2.5 2 4 2 2.5-2 4-2 2.5 2 4 2 2-1 2-1M2 21h20M7 8v2M12 8v2M17 8v2M7 4h.01M12 4h.01M17 4h.01'/></svg> Birthdays");
            sb.Append("</button>");
            sb.Append("<button class='fpill' id='fil-notes' onclick='setFilter(\"notes\")'>");
            sb.Append("<svg fill='none' stroke='currentColor' stroke-width='2' viewBox='0 0 24 24'><path d='M16 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8Z'/><path d='M15 3v5h5'/></svg> Notes &amp; Reminders");
            sb.Append("</button>");
            sb.Append("</div>");

            // ── STATS BAR & BODY ──────────────────────────────────────────
            sb.Append("<div class='sbar' id='sb'></div>");
            sb.Append("<div class='cbody' id='cb'></div>");

            // ── SCRIPT INJECTION ─────────────────────────────────────────
            sb.Append("<script>");
            sb.AppendFormat("var RAW_EVENTS={0};", eventsJson);
            sb.AppendFormat("var Y={0},M={1},VIEW='{2}';", year, month, view);
            sb.AppendFormat("var API_BASE='{0}';", apiBase);
            sb.Append(JsBody());
            sb.Append("</script></body></html>");

            return sb.ToString();
        }

        private static string JsBody()
        {
            return """
var TODAY = new Date();
var MN = ['January','February','March','April','May','June','July','August','September','October','November','December'];
var DN = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
var DS = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
var FILTER = 'all';
var SEARCH_QUERY = '';

var RENTALS = Array.isArray(RAW_EVENTS) ? RAW_EVENTS : (RAW_EVENTS.rentals || []);
var MAINTENANCE = Array.isArray(RAW_EVENTS) ? [] : (RAW_EVENTS.maintenance || []);
var BIRTHDAYS = Array.isArray(RAW_EVENTS) ? [] : (RAW_EVENTS.birthdays || []);
var NOTES = Array.isArray(RAW_EVENTS) ? [] : (RAW_EVENTS.notes || []);

function svg(n, cls, col) {
  cls = cls || 'w-4';
  var s = 'width="14" height="14" style="width:14px;height:14px;min-width:14px;min-height:14px;max-width:16px;max-height:16px;vertical-align:middle;display:inline-block;flex-shrink:0;" class="' + cls + '" fill="none" stroke="' + (col || 'currentColor') + '" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" viewBox="0 0 24 24"';
  if(n==='user') return '<svg ' + s + '><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>';
  if(n==='car') return '<svg ' + s + '><path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2"/><circle cx="7" cy="17" r="2"/><circle cx="17" cy="17" r="2"/></svg>';
  if(n==='driver') return '<svg ' + s + '><circle cx="12" cy="12" r="10"/><path d="m4.93 4.93 4.24 4.24M14.83 9.17l4.24-4.24M12 12v9"/></svg>';
  if(n==='calendar') return '<svg ' + s + '><rect width="18" height="18" x="3" y="4" rx="2"/><path d="M3 10h18M8 2v4M16 2v4"/></svg>';
  if(n==='clock') return '<svg ' + s + '><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>';
  if(n==='peso'||n==='dollar') return '<svg ' + s + '><line x1="12" x2="12" y1="2" y2="22"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>';
  if(n==='flag') return '<svg ' + s + '><path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/><line x1="4" x2="4" y1="22" y2="15"/></svg>';
  if(n==='shield') return '<svg ' + s + '><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="m9 12 2 2 4-4"/></svg>';
  if(n==='tool'||n==='wrench') return '<svg ' + s + '><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>';
  if(n==='cake'||n==='birthday') return '<svg ' + s + '><path d="M20 21v-8a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8M4 16s.5-1 2-1 2.5 2 4 2 2.5-2 4-2 2.5 2 4 2 2.5-2 4-2 2-1 2-1M2 21h20M7 8v2M12 8v2M17 8v2M7 4h.01M12 4h.01M17 4h.01"/></svg>';
  if(n==='note'||n==='sticky') return '<svg ' + s + '><path d="M16 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8Z"/><path d="M15 3v5h5"/></svg>';
  if(n==='copy') return '<svg ' + s + '><rect width="14" height="14" x="8" y="8" rx="2" ry="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/></svg>';
  if(n==='phone') return '<svg ' + s + '><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/></svg>';
  if(n==='mail') return '<svg ' + s + '><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>';
  if(n==='close'||n==='x') return '<svg ' + s + '><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';
  if(n==='trash') return '<svg ' + s + '><path d="M3 6h18M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/></svg>';
  if(n==='plus') return '<svg ' + s + '><line x1="12" x2="12" y1="5" y2="19"/><line x1="5" x2="19" y1="12" y2="12"/></svg>';
  if(n==='check') return '<svg ' + s + '><polyline points="20 6 9 17 4 12"/></svg>';
  return '<svg ' + s + '><circle cx="12" cy="12" r="10"/></svg>';
}

function tick(){
  var n=new Date(),h=n.getHours()%12||12,mi=n.getMinutes(),s=n.getSeconds(),ap=n.getHours()<12?'AM':'PM';
  document.getElementById('ck').textContent=h+':'+(mi<10?'0':'')+mi+':'+(s<10?'0':'')+s+' '+ap;
  document.getElementById('dd').textContent=DN[n.getDay()]+', '+MN[n.getMonth()]+' '+n.getDate()+', '+n.getFullYear();
}
setInterval(tick,1000);tick();

function money(n){return '₱'+parseFloat(n||0).toLocaleString('en-PH',{minimumFractionDigits:2});}
function fmt(d){
  if(!d)return '--';
  var dt=new Date(d+'T00:00:00');
  return MN[dt.getMonth()].slice(0,3)+' '+dt.getDate()+', '+dt.getFullYear();
}
function same(a,b){return a.getFullYear()===b.getFullYear()&&a.getMonth()===b.getMonth()&&a.getDate()===b.getDate();}

function sc(s){
  var m=(s||'').toLowerCase().replace(/[-_ ]/g,'');
  if(m.indexOf('pending')>=0)return 'ep';
  if(m.indexOf('approved')>=0)return 'ea';
  if(m.indexOf('active')>=0||m.indexOf('inuse')>=0||m.indexOf('rented')>=0)return 'ex';
  if(m.indexOf('completed')>=0||m.indexOf('returned')>=0)return 'ec';
  return 'ek';
}
function scol(s){
  var m=(s||'').toLowerCase();
  if(m.indexOf('pending')>=0)return '#f59e0b';
  if(m.indexOf('approved')>=0)return '#22c55e';
  if(m.indexOf('active')>=0||m.indexOf('in-use')>=0||m.indexOf('rented')>=0)return '#3b82f6';
  if(m.indexOf('completed')>=0||m.indexOf('returned')>=0)return '#94a3b8';
  return '#ef4444';
}

function copyText(val, lbl){
  if(!val)return;
  navigator.clipboard.writeText(val).then(function(){
    showToast('Copied ' + (lbl||'item') + ' to clipboard!');
  });
}
function showToast(msg){
  var c = document.getElementById('c-toast-container');
  if(!c){
    c = document.createElement('div');
    c.id = 'c-toast-container';
    c.className = 'toast-container';
    document.body.appendChild(c);
  }
  var t = document.createElement('div');
  t.className = 'toast';
  t.innerHTML = svg('check') + '<span style="flex:1;min-width:0">' + msg + '</span>';
  c.appendChild(t);
  setTimeout(function(){
    if(t.parentElement){
      t.style.opacity = '0';
      setTimeout(function(){ if(t.parentElement) t.remove(); }, 250);
    }
  }, 3200);
}

function getBrandLogoHtml(brand, name, isModal){
  var b = (brand && brand.length > 1) ? brand : (name ? name.split(' ')[0] : 'Car');
  var raw = b.toLowerCase().trim().replace(/[\/\\_]+/g, ' ');
  var slug = raw;
  if(raw.indexOf('toyota')!==-1) slug='toyota';
  else if(raw.indexOf('ford')!==-1) slug='ford';
  else if(raw.indexOf('mitsubishi')!==-1) slug='mitsubishi';
  else if(raw.indexOf('honda')!==-1) slug='honda';
  else if(raw.indexOf('hyundai')!==-1) slug='hyundai';
  else if(raw.indexOf('nissan')!==-1) slug='nissan';
  else if(raw.indexOf('isuzu')!==-1) slug='isuzu';
  else if(raw.indexOf('suzuki')!==-1) slug='suzuki';
  else if(raw.indexOf('bmw')!==-1) slug='bmw';
  else if(raw.indexOf('chev')!==-1) slug='chevrolet';
  else if(raw.indexOf('kia')!==-1) slug='kia';
  else if(raw.indexOf('mazda')!==-1) slug='mazda';
  else if(raw.indexOf('benz')!==-1||raw.indexOf('mercedes')!==-1) slug='mercedes-benz';
  else if(raw.indexOf('rover')!==-1) slug='land-rover';
  else if(raw.indexOf('volks')!==-1||raw==='vw') slug='volkswagen';
  else slug=raw.split(' ')[0].replace(/[^a-z0-9-]/g,'');
  var initials = b.length >= 2 ? b.substring(0,2).toUpperCase() : b.toUpperCase();
  var cdnSrc = 'https://cdn.jsdelivr.net/gh/filippofilip95/car-logos-dataset@master/logos/original/' + slug + '.png';
  var apiSrc = API_BASE + '/vehicles/brand-logo/' + encodeURIComponent(slug);
  var boxCls = isModal ? 'brand-badge-box' : 'ev-brand-badge';
  return "<span class='" + boxCls + "'>" +
         "<img src='" + cdnSrc + "' alt='" + b + "' onerror=\"if(this.src!=='" + apiSrc + "'){this.src='" + apiSrc + "';}else{this.style.display='none';this.nextElementSibling.style.display='inline-flex';}\" />" +
         "<span class='brand-fallback-txt'>" + initials + "</span>" +
         "</span>";
}

function getPaymentLogoHtml(method, status){
  var m = (method || 'cash').toLowerCase().trim();
  var slug = 'cash';
  var domain = '';
  if(m.indexOf('gcash')!==-1) { slug='gcash'; domain='gcash.com'; }
  else if(m.indexOf('maya')!==-1) { slug='maya'; domain='maya.ph'; }
  else if(m.indexOf('bdo')!==-1) { slug='bdo'; domain='bdo.com.ph'; }
  else if(m.indexOf('bpi')!==-1) { slug='bpi'; domain='bpi.com.ph'; }
  else if(m.indexOf('metro')!==-1) { slug='metrobank'; domain='metrobank.com.ph'; }
  else if(m.indexOf('union')!==-1||m==='ubp') { slug='unionbank'; domain='unionbankph.com'; }
  else if(m.indexOf('landbank')!==-1) { slug='landbank'; domain='landbank.com'; }
  else if(m.indexOf('china')!==-1) { slug='chinabank'; domain='chinabank.ph'; }
  else if(m.indexOf('security')!==-1) { slug='securitybank'; domain='securitybank.com'; }
  else if(m.indexOf('gotyme')!==-1) { slug='gotyme'; domain='gotyme.com.ph'; }
  else if(m.indexOf('seabank')!==-1) { slug='seabank'; domain='seabank.com.ph'; }
  else if(m.indexOf('tonik')!==-1) { slug='tonik'; domain='tonikbank.com'; }
  else if(m.indexOf('cimb')!==-1) { slug='cimb'; domain='cimbbank.com.ph'; }
  else if(m.indexOf('shopee')!==-1) { slug='shopeepay'; domain='shopee.ph'; }
  else if(m.indexOf('grab')!==-1) { slug='grabpay'; domain='grab.com'; }
  else if(m.indexOf('palawan')!==-1) { slug='palawanpay'; domain='palawanpay.com'; }
  else if(m.indexOf('psbank')!==-1) { slug='psbank'; domain='psbank.com.ph'; }
  else if(m.indexOf('aub')!==-1) { slug='aub'; domain='aub.com.ph'; }
  var cdnSrc = domain ? ('https://www.google.com/s2/favicons?domain=' + domain + '&sz=256') : '';
  var apiSrc = API_BASE + '/transactions/provider-logo/' + encodeURIComponent(slug);
  var primarySrc = cdnSrc || apiSrc;
  var initials = (method || 'Cash').substring(0, 2).toUpperCase();
  return "<span class='pay-badge-box'>" +
         "<img src='" + primarySrc + "' alt='" + m + "' onerror=\"if(this.src!=='" + apiSrc + "'){this.src='" + apiSrc + "';}else{this.style.display='none';this.nextElementSibling.style.display='inline-flex';}\" />" +
         "<span class='pay-fallback-txt'>" + initials + "</span>" +
         "</span>";
}

// Search filtering logic
function onSearchInput(val){
  SEARCH_QUERY = (val || '').toLowerCase().trim();
  var clr = document.getElementById('calClearBtn');
  if(clr) clr.style.display = SEARCH_QUERY ? 'flex' : 'none';
  render();
}

function clearSearch(){
  SEARCH_QUERY = '';
  var inp = document.getElementById('calSearch');
  if(inp) inp.value = '';
  var clr = document.getElementById('calClearBtn');
  if(clr) clr.style.display = 'none';
  render();
}

function matchesSearch(item, type, dateStr){
  if(!SEARCH_QUERY) return true;
  var q = SEARCH_QUERY;
  if(dateStr && (dateStr.indexOf(q)>=0 || fmt(dateStr).toLowerCase().indexOf(q)>=0)) return true;
  if(type === 'rental' && item){
    var veh = (item.vehicleName || '').toLowerCase();
    var plate = (item.plateNo || '').toLowerCase();
    var cust = (item.customerName || '').toLowerCase();
    var phone = (item.customerPhone || '').toLowerCase();
    var email = (item.customerEmail || '').toLowerCase();
    var drv = (item.driverName || '').toLowerCase();
    var id = String(item.rentalId || '').toLowerCase();
    var st = (item.status || '').toLowerCase();
    var dest = (item.destination || '').toLowerCase();
    var code = ('rn-' + String(item.rentalId).padStart(6,'0')).toLowerCase();
    return veh.indexOf(q)>=0 || plate.indexOf(q)>=0 || cust.indexOf(q)>=0 || phone.indexOf(q)>=0 || email.indexOf(q)>=0 || drv.indexOf(q)>=0 || id.indexOf(q)>=0 || st.indexOf(q)>=0 || dest.indexOf(q)>=0 || code.indexOf(q)>=0;
  }
  if(type === 'maint' && item){
    var mVeh = (item.vehicleName || '').toLowerCase();
    var mPlate = (item.plateNo || '').toLowerCase();
    var mType = (item.type || '').toLowerCase();
    var mDesc = (item.description || '').toLowerCase();
    return mVeh.indexOf(q)>=0 || mPlate.indexOf(q)>=0 || mType.indexOf(q)>=0 || mDesc.indexOf(q)>=0;
  }
  if(type === 'bday' && item){
    var bName = (item.name || item.fullName || '').toLowerCase();
    var bRole = (item.role || '').toLowerCase();
    return bName.indexOf(q)>=0 || bRole.indexOf(q)>=0;
  }
  if(type === 'note' && item){
    var nTitle = (item.title || '').toLowerCase();
    var nCat = (item.category || '').toLowerCase();
    var nCont = (item.content || '').toLowerCase();
    return nTitle.indexOf(q)>=0 || nCat.indexOf(q)>=0 || nCont.indexOf(q)>=0;
  }
  return false;
}

// Client-side Async Calendar Loader
async function fetchCalendarData(targetY, targetM){
  try {
    var res = await fetch(API_BASE + '/rentals/calendar?year=' + targetY + '&month=' + targetM);
    if(res.ok){
      var data = await res.json();
      RAW_EVENTS = data;
      RENTALS = Array.isArray(data) ? data : (data.rentals || []);
      MAINTENANCE = Array.isArray(data) ? [] : (data.maintenance || []);
      BIRTHDAYS = Array.isArray(data) ? [] : (data.birthdays || []);
      NOTES = Array.isArray(data) ? [] : (data.notes || []);
    }
  } catch(e) {
    console.warn('Calendar fetch error:', e);
  }
  Y = targetY;
  M = targetM;
  render();
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage(JSON.stringify({action:'navigate', year:Y, month:M, clientHandled:true}));
  }
}

function matchesRentalFilter(e, f){
  if(!f || f==='all' || f==='rentals') return true;
  var s = (e.status||'').toLowerCase();
  if(f==='pending') return s.indexOf('pending')>=0;
  if(f==='approved') return s.indexOf('approved')>=0;
  if(f==='active') return s.indexOf('active')>=0 || s.indexOf('in-use')>=0 || s.indexOf('rented')>=0;
  if(f==='completed') return s.indexOf('completed')>=0 || s.indexOf('returned')>=0;
  if(f==='cancelled') return s.indexOf('cancelled')>=0;
  return false;
}

function setFilter(f){
  if(FILTER === f){
    FILTER = 'all';
  } else {
    FILTER = f;
  }
  updateFilterUI();
  render();
}

function updateFilterUI(){
  document.querySelectorAll('.fpill').forEach(function(b){b.classList.remove('on');});
  document.querySelectorAll('.sc').forEach(function(c){c.classList.remove('on');});

  var pillId = 'fil-' + FILTER;
  var pill = document.getElementById(pillId);
  if(pill) {
    pill.classList.add('on');
  } else if(['pending','approved','active','completed','cancelled'].indexOf(FILTER) >= 0) {
    var rentPill = document.getElementById('fil-rentals');
    if(rentPill) rentPill.classList.add('on');
  } else if(FILTER==='all') {
    var allPill = document.getElementById('fil-all');
    if(allPill) allPill.classList.add('on');
  }

  var card = document.getElementById('sc-' + FILTER);
  if(card) card.classList.add('on');
}

function stats(){
  var p=0,a=0,x=0,c=0,k=0,rev=0;
  RENTALS.forEach(function(e){
    var s=(e.status||'').toLowerCase();
    if(s.indexOf('pending')>=0)p++;
    else if(s.indexOf('approved')>=0)a++;
    else if(s.indexOf('active')>=0||s.indexOf('in-use')>=0||s.indexOf('rented')>=0)x++;
    else if(s.indexOf('completed')>=0||s.indexOf('returned')>=0)c++;
    else if(s.indexOf('cancelled')>=0)k++;
    if((e.paymentStatus||'').toLowerCase()==='paid')rev+=parseFloat(e.totalAmount||0);
  });
  document.getElementById('sb').innerHTML=
    "<div class='sc' id='sc-pending' onclick='setFilter(\"pending\")' title='Click to filter Pending rentals (" + p + ")'><div class='sv' style='color:#f59e0b'>" + p + "</div><div class='sl'>Pending</div></div>" +
    "<div class='sc' id='sc-approved' onclick='setFilter(\"approved\")' title='Click to filter Approved rentals (" + a + ")'><div class='sv' style='color:#22c55e'>" + a + "</div><div class='sl'>Approved</div></div>" +
    "<div class='sc' id='sc-active' onclick='setFilter(\"active\")' title='Click to filter Active rentals (" + x + ")'><div class='sv' style='color:#3b82f6'>" + x + "</div><div class='sl'>Active</div></div>" +
    "<div class='sc' id='sc-completed' onclick='setFilter(\"completed\")' title='Click to filter Completed rentals (" + c + ")'><div class='sv' style='color:#94a3b8'>" + c + "</div><div class='sl'>Completed</div></div>" +
    "<div class='sc' id='sc-cancelled' onclick='setFilter(\"cancelled\")' title='Click to filter Cancelled rentals (" + k + ")'><div class='sv' style='color:#ef4444'>" + k + "</div><div class='sl'>Cancelled</div></div>" +
    "<div class='sc' id='sc-maintenance' onclick='setFilter(\"maintenance\")' title='Click to filter Maintenance events (" + MAINTENANCE.length + ")'><div class='sv' style='color:#d97706'>" + MAINTENANCE.length + "</div><div class='sl'>Maintenance</div></div>" +
    "<div class='sc' id='sc-birthdays' onclick='setFilter(\"birthdays\")' title='Click to filter Birthdays (" + BIRTHDAYS.length + ")'><div class='sv' style='color:#ec4899'>" + BIRTHDAYS.length + "</div><div class='sl'>Birthdays</div></div>" +
    "<div class='sc' id='sc-notes' onclick='setFilter(\"notes\")' title='Click to filter Notes & Reminders (" + NOTES.length + ")'><div class='sv' style='color:#38bdf8'>" + NOTES.length + "</div><div class='sl'>Notes</div></div>" +
    "<div class='sc sc-info' style='margin-left:auto' title='Settled Revenue for this month'><div class='sv' style='color:#10b981'>" + money(rev) + "</div><div class='sl'>Settled Inflow</div></div>";
  updateFilterUI();
}

function renderMonth(){
  var dim=new Date(Y,M,0).getDate();
  var fd=new Date(Y,M-1,1).getDay();
  var ld=new Date(Y,M-1,dim).getDay();
  var prev=fd,next=ld===6?0:6-ld;
  var pmd=new Date(Y,M-1,0).getDate();
  var total=prev+dim+next;
  var h="<div class='grid'>";
  DS.forEach(function(d){h+="<div class='dh'>" + d + "</div>";});
  for(var i=0;i<total;i++){
    var dn,other=false,date;
    if(i<prev){dn=pmd-prev+i+1;other=true;var pm=M===1?12:M-1;var py=M===1?Y-1:Y;date=new Date(py,pm-1,dn);}
    else if(i<prev+dim){dn=i-prev+1;date=new Date(Y,M-1,dn);}
    else{dn=i-prev-dim+1;other=true;var nm=M===12?1:M+1;var ny=M===12?Y+1:Y;date=new Date(ny,nm-1,dn);}
    var isT=!other&&same(date,TODAY);
    var dateStr = date.getFullYear() + '-' + String(date.getMonth()+1).padStart(2,'0') + '-' + String(date.getDate()).padStart(2,'0');
    var dayEvs = [];
    var hasMatch = false;

    if(FILTER==='all'||FILTER==='rentals'||['pending','approved','active','completed','cancelled'].indexOf(FILTER)>=0){
      RENTALS.forEach(function(e){
        var s=new Date(e.startDate+'T00:00:00');
        var en=e.endDate?new Date(e.endDate+'T23:59:59'):new Date(e.startDate+'T23:59:59');
        if(date>=s&&date<=en && matchesRentalFilter(e, FILTER)){
          var isMatch = matchesSearch(e, 'rental', dateStr);
          if(isMatch) hasMatch = true;
          if(!SEARCH_QUERY || isMatch){
            dayEvs.push({type:'rental', data:e, html:"<div class='ev " + sc(e.status) + "' onclick='om(" + e.rentalId + ", event)' title='" + (e.vehicleName||'') + " - " + (e.customerName||'') + "'>" +
                         getBrandLogoHtml(e.vehicleBrand, e.vehicleName, false) + "<span>" + (e.plateNo||'') + " " + (e.vehicleName||'').split(' ').pop() + "</span></div>"});
          }
        }
      });
    }

    if(FILTER==='all'||FILTER==='maintenance'){
      MAINTENANCE.forEach(function(m){
        if(m.scheduledDate===dateStr){
          var isMatch = matchesSearch(m, 'maint', dateStr);
          if(isMatch) hasMatch = true;
          if(!SEARCH_QUERY || isMatch){
            dayEvs.push({type:'maint', data:m, html:"<div class='ev ev-maint' onclick='openMaintModal(" + m.maintenanceId + ", event)' title='Maintenance: " + (m.type||'') + "'>" +
                         svg('wrench') + "<span>" + (m.plateNo||'') + " " + (m.type||'PMS') + "</span></div>"});
          }
        }
      });
    }

    if(FILTER==='all'||FILTER==='birthdays'){
      if(!other){
        BIRTHDAYS.forEach(function(b){
          if(b.birthDay===dn){
            var isMatch = matchesSearch(b, 'bday', dateStr);
            if(isMatch) hasMatch = true;
            if(!SEARCH_QUERY || isMatch){
              dayEvs.push({type:'bday', data:b, html:"<div class='ev ev-bday' onclick='openBdayModal(" + b.id + ", event)' title='Birthday: " + (b.name||b.fullName||'') + "'>" +
                           svg('cake') + "<span>" + (b.name||b.fullName||'').split(' ')[0] + " Birthday</span></div>"});
            }
          }
        });
      }
    }

    if(FILTER==='all'||FILTER==='notes'){
      NOTES.forEach(function(nt){
        var nDate = (nt.noteDate || '').split('T')[0];
        if(nDate===dateStr){
          var isMatch = matchesSearch(nt, 'note', dateStr);
          if(isMatch) hasMatch = true;
          if(!SEARCH_QUERY || isMatch){
            dayEvs.push({type:'note', data:nt, html:"<div class='ev ev-note' onclick='openNoteModal(" + nt.noteId + ", event)' title='Note: " + (nt.title||'') + "'>" +
                         svg('sticky') + "<span>" + (nt.title||'Note') + "</span></div>"});
          }
        }
      });
    }

    var isDateMatch = SEARCH_QUERY && matchesSearch(null, null, dateStr);
    var isMatchedCell = isDateMatch || (SEARCH_QUERY && hasMatch);

    var shown = dayEvs.slice(0,3), extra = dayEvs.length - 3;
    var evHtml = '';
    shown.forEach(function(evObj){ evHtml += evObj.html; });
    if(extra > 0) evHtml += "<div class='more'>+" + extra + " more</div>";

    h += "<div class='cell" + (other?' other':'') + (isT?' today':'') + (isMatchedCell?' matched':'') + "' onclick='onCellClick(\"" + dateStr + "\", event)'>" +
         "<div class='cell-top'>" +
         "<div class='dn'>" + (isT ? "<span class='tdot'>" + dn + "</span>" : dn) + "</div>" +
         "<button class='add-day-btn' onclick='openAddNoteForDate(\"" + dateStr + "\", event)' title='Add note for " + dateStr + "'>" + svg('plus') + "</button>" +
         "</div>" + evHtml + "</div>";
  }
  h += '</div>';
  document.getElementById('cb').innerHTML = h;
}

function renderWeek(){
  var ref=new Date(Y,M-1,TODAY.getDate());
  var dow=ref.getDay(),ws=new Date(ref);ws.setDate(ref.getDate()-dow);
  var days=[];
  for(var i=0;i<7;i++){var d=new Date(ws);d.setDate(ws.getDate()+i);days.push(d);}
  var h="<div style='display:grid;grid-template-columns:54px repeat(7,1fr);gap:2px;margin-top:10px'><div></div>";
  days.forEach(function(d){
    var iT=same(d,TODAY);
    h+="<div class='dhdr" + (iT?' tod':'') + "'>" + DS[d.getDay()] + "<br>" +
       "<span style='font-size:15px;font-weight:800;color:" + (iT?'#3b82f6':'inherit') + ";'>" + d.getDate() + "</span></div>";
  });
  for(var hr=0;hr<24;hr++){
    var lbl=hr===0?'12 AM':(hr<12?hr+' AM':(hr===12?'12 PM':(hr-12)+' PM'));
    h+="<div class='tlbl'>" + lbl + "</div>";
    days.forEach(function(d){
      var dStr=d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0');
      var inner='';
      if(hr===9){
        var showRentals = (FILTER==='all'||FILTER==='rentals'||['pending','approved','active','completed','cancelled'].indexOf(FILTER)>=0);
        if(showRentals){
          RENTALS.filter(function(e){return same(d,new Date(e.startDate+'T00:00:00')) && matchesRentalFilter(e, FILTER) && matchesSearch(e,'rental',dStr);}).forEach(function(e){
            inner+="<div class='ev " + sc(e.status) + "' onclick='om(" + e.rentalId + ", event)'>" + svg('car') + "<span>" + (e.plateNo||'') + " " + (e.customerName||'').split(' ')[0] + "</span></div>";
          });
        }
      }
      h+="<div class='hrow' onclick='onCellClick(\"" + dStr + "\", event)'>" + inner + "</div>";
    });
  }
  h+='</div>';
  document.getElementById('cb').innerHTML=h;
}

function renderDay(){
  var d=new Date(Y,M-1,TODAY.getDate());
  var dStr=d.getFullYear()+'-'+String(d.getMonth()+1).padStart(2,'0')+'-'+String(d.getDate()).padStart(2,'0');
  var showRentals = (FILTER==='all'||FILTER==='rentals'||['pending','approved','active','completed','cancelled'].indexOf(FILTER)>=0);
  var dayRentals = !showRentals ? [] : RENTALS.filter(function(e){
    var s=new Date(e.startDate+'T00:00:00');
    var en=e.endDate?new Date(e.endDate+'T23:59:59'):new Date(e.startDate+'T23:59:59');
    return d>=s&&d<=en && matchesRentalFilter(e, FILTER) && matchesSearch(e, 'rental', dStr);
  });
  var showMaint = (FILTER==='all'||FILTER==='maintenance');
  var dayMaint = !showMaint ? [] : MAINTENANCE.filter(function(m){ return (m.scheduledDate===dStr||m.serviceDate===dStr) && matchesSearch(m, 'maint', dStr); });
  var showBday = (FILTER==='all'||FILTER==='birthdays');
  var dayBday = !showBday ? [] : BIRTHDAYS.filter(function(b){ return b.birthDay===d.getDate() && matchesSearch(b, 'bday', dStr); });
  var showNotes = (FILTER==='all'||FILTER==='notes');
  var dayNotes = !showNotes ? [] : NOTES.filter(function(n){ var nDate = (n.noteDate || '').split('T')[0]; return nDate===dStr && matchesSearch(n, 'note', dStr); });
  var h='<div style="max-width:1040px;margin:0 auto;display:flex;flex-direction:column;gap:14px;padding-top:4px">';
  h += '<div class="day-banner">' +
       '<div style="display:flex;align-items:center;gap:14px">' +
       '<div style="width:48px;height:48px;border-radius:10px;background:#f97316;display:flex;flex-direction:column;align-items:center;justify-content:center;color:#fff;flex-shrink:0">' +
       '<span style="font-size:9.5px;font-weight:700;text-transform:uppercase;letter-spacing:1px;line-height:1">' + DS[d.getDay()] + '</span>' +
       '<span style="font-size:20px;font-weight:900;line-height:1.1">' + d.getDate() + '</span></div>' +
       '<div><div class="day-title">' + MN[d.getMonth()] + ' ' + d.getDate() + ', ' + d.getFullYear() + '</div>' +
       '<div class="day-sub">' + svg('clock') + ' Daily Fleet Dispatch &amp; Operations Schedule</div></div></div>' +
       '<div style="display:flex;align-items:center;gap:10px">' +
       '<button class="add-note-btn" onclick="openAddNoteForDate(\'' + dStr + '\', event)">' + svg('plus') + ' Add Note / Reminder</button>' +
       '</div></div>';
  h += '<div style="display:flex;flex-direction:column;gap:8px">';
  h += '<div style="font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:.8px;display:flex;align-items:center;gap:6px">' +
       svg('car') + ' Active Bookings &amp; Dispatches (' + dayRentals.length + ')</div>';
  if(!dayRentals.length){
    h += '<div class="day-empty">' +
         '<div style="font-size:14px;font-weight:600">No Active Vehicle Bookings for this Day</div>' +
         '<div style="font-size:12px;margin-top:4px">Vehicles are available for dispatch or booking in the garage.</div></div>';
  } else {
    dayRentals.forEach(function(e){
      var c=scol(e.status);
      var brandBadge = getBrandLogoHtml(e.vehicleBrand, e.vehicleName, false);
      var payBadge = getPaymentLogoHtml(e.paymentMethod, e.paymentStatus);
      h += '<div onclick="om(' + e.rentalId + ', event)" style="background:' + c + '12;border:1px solid ' + c + '33;border-left:4px solid ' + c + ';border-radius:12px;padding:12px 16px;cursor:pointer;display:flex;align-items:center;justify-content:space-between;transition:all .15s;gap:16px">' +
           '<div style="display:flex;align-items:center;gap:12px;min-width:0">' +
           brandBadge +
           '<div style="min-width:0">' +
           '<div class="day-card-title">' +
           (e.vehicleName||'Vehicle') +
           '<span style="font-size:10.5px;font-weight:700;color:#94a3b8;background:rgba(255,255,255,.08);padding:2px 8px;border-radius:6px">[' + (e.plateNo||'--') + ']</span>' +
           '</div>' +
           '<div style="font-size:11.5px;color:#94a3b8;margin-top:4px;display:flex;align-items:center;gap:10px;flex-wrap:wrap">' +
           '<span style="display:inline-flex;align-items:center;gap:4px">' + svg('user') + ' ' + (e.customerName||'Customer') + '</span>' +
           '<span>&bull;</span>' +
           '<span style="display:inline-flex;align-items:center;gap:4px">' + svg('driver') + ' ' + (e.driverName||'Self-Drive') + '</span>' +
           '<span>&bull;</span>' +
           '<span style="display:inline-flex;align-items:center;gap:4px">' + svg('calendar') + ' ' + fmt(e.startDate) + ' &rarr; ' + fmt(e.endDate) + '</span>' +
           '</div></div></div>' +
           '<div style="display:flex;align-items:center;gap:14px;flex-shrink:0">' +
           payBadge +
           '<div style="text-align:right">' +
           '<div style="font-size:14px;font-weight:800;color:#10b981">' + money(e.totalAmount) + '</div>' +
           '<div style="font-size:9.5px;color:' + c + ';font-weight:800;text-transform:uppercase;letter-spacing:.5px">' + (e.status||'pending') + '</div>' +
           '</div></div></div>';
    });
  }
  h += '</div>';
  if(dayMaint.length || dayBday.length || dayNotes.length){
    h += '<div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:12px;margin-top:6px">';
    if(dayMaint.length){
      dayMaint.forEach(function(m){
        h += '<div onclick="openMaintModal(' + m.maintenanceId + ', event)" style="background:rgba(217,119,6,.1);border:1px solid rgba(217,119,6,.3);border-left:4px solid #d97706;border-radius:10px;padding:12px 14px;cursor:pointer">' +
             '<div style="font-size:10.5px;font-weight:700;color:#d97706;text-transform:uppercase;display:flex;align-items:center;gap:6px">' + svg('tool') + ' Maintenance Due</div>' +
             '<div class="day-card-title" style="margin-top:3px">' + (m.vehicleName||'Vehicle') + ' [' + (m.plateNo||'') + ']</div>' +
             '<div style="font-size:11px;color:#94a3b8;margin-top:2px">' + (m.description||'Scheduled Inspection') + ' &bull; ' + money(m.cost) + '</div></div>';
      });
    }
    if(dayBday.length){
      dayBday.forEach(function(b){
        h += '<div onclick="openBdayModal(' + b.id + ', event)" style="background:rgba(236,72,153,.1);border:1px solid rgba(236,72,153,.3);border-left:4px solid #ec4899;border-radius:10px;padding:12px 14px;cursor:pointer">' +
             '<div style="font-size:10.5px;font-weight:700;color:#ec4899;text-transform:uppercase;display:flex;align-items:center;gap:6px">' + svg('cake') + ' Team Birthday</div>' +
             '<div class="day-card-title" style="margin-top:3px">' + (b.name||b.fullName||'Celebrant') + '</div>' +
             '<div style="font-size:11px;color:#94a3b8;margin-top:2px">' + (b.role||'Team Member') + ' &bull; ' + (b.phone||'') + '</div></div>';
      });
    }
    if(dayNotes.length){
      dayNotes.forEach(function(n){
        h += '<div onclick="openNoteModal(' + n.noteId + ', event)" style="background:rgba(14,165,233,.1);border:1px solid rgba(14,165,233,.3);border-left:4px solid #38bdf8;border-radius:10px;padding:12px 14px;cursor:pointer">' +
             '<div style="font-size:10.5px;font-weight:700;color:#38bdf8;text-transform:uppercase;display:flex;align-items:center;gap:6px">' + svg('note') + ' ' + (n.category||'Note') + '</div>' +
             '<div class="day-card-title" style="margin-top:3px">' + (n.title||'Note') + '</div>' +
             '<div style="font-size:11px;color:#94a3b8;margin-top:2px">' + (n.content||'') + '</div></div>';
      });
    }
    h += '</div>';
  }
  h += '</div>';
  document.getElementById('cb').innerHTML = h;
}

function om(id, ev){
  if(ev)ev.stopPropagation();
  var evInfo=null;
  for(var i=0;i<RENTALS.length;i++){if(RENTALS[i].rentalId===id){evInfo=RENTALS[i];break;}}
  if(!evInfo)return;
  var c=scol(evInfo.status);
  var photoSrc = evInfo.photoUrl || '/WebAssets/garage_3D.png';

  var h = "<div class='mbg' id='mbg' onclick=\"if(event.target.id==='mbg')cm()\">";
  h += "<div class='modal'>";

  h += "<div class='mhdr'>";
  h += "<img src='" + photoSrc + "' class='mphoto' onerror=\"this.src='/WebAssets/garage_3D.png'\" alt='Vehicle' />";
  h += "<div class='mhdr-info'>";
  h += "<div class='mtitle'>" + (evInfo.vehicleName||'Vehicle') +
       " " + getBrandLogoHtml(evInfo.vehicleBrand, evInfo.vehicleName, true) +
       "</div>";
  h += "<div class='msub'>Rental #" + evInfo.rentalId + " &bull; Plate <span class='click-val' onclick='copyText(\"" + (evInfo.plateNo||'') + "\", \"Plate Number\")'>" + (evInfo.plateNo||'') + " " + svg('copy') + "</span></div>";
  h += "</div></div>";

  h += "<div class='mbody'>";

  // 1. Customer Profile Card with PFP Avatar
  var custInitials = ((evInfo.customerName || 'Customer').split(' ').map(function(s){return s[0];}).slice(0,2).join('')).toUpperCase();
  var custPhoto = (evInfo.customerPhoto && evInfo.customerPhoto.length > 5) ? evInfo.customerPhoto : '';
  h += "<div class='msec'><div class='msec-title'>" + svg('user') + " Customer Profile &amp; Contact</div>";
  h += "<div style='display:flex;align-items:center;gap:12px;margin-bottom:12px;padding-bottom:10px;border-bottom:1px solid rgba(255,255,255,.06)'>";
  if(custPhoto){
    h += "<img src='" + custPhoto + "' class='cust-pfp' onerror='this.style.display=\"none\";if(this.nextElementSibling)this.nextElementSibling.style.display=\"flex\"' alt='Customer Avatar' />";
    h += "<div class='cust-pfp-fallback' style='display:none'>" + custInitials + "</div>";
  } else {
    h += "<div class='cust-pfp-fallback'>" + custInitials + "</div>";
  }
  h += "<div><div style='font-size:14px;font-weight:800;color:#fff'>" + (evInfo.customerName||'--') + "</div>";
  h += "<div style='font-size:11px;color:#10b981;font-weight:600;display:flex;align-items:center;gap:4px;margin-top:2px'>" + svg('shield') + " Verified Customer Account</div></div>";
  h += "</div>";
  h += "<div class='mgrid'>";
  h += "<div class='mrow'><div class='mk'>Phone Number</div><div class='mv'><span class='click-val' onclick='copyText(\"" + (evInfo.customerPhone||'') + "\", \"Customer Phone\")'>" + (evInfo.customerPhone||'--') + " " + svg('phone') + "</span></div></div>";
  h += "<div class='mrow'><div class='mk'>Email Address</div><div class='mv'><span class='click-val' onclick='copyText(\"" + (evInfo.customerEmail||'') + "\", \"Customer Email\")'>" + (evInfo.customerEmail||'--') + " " + svg('mail') + "</span></div></div>";
  h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Trip Destination</div><div class='mv'>" + (evInfo.destination||'City / Local Drive') + "</div></div>";
  h += "</div></div>";

  // 2. Vehicle Specs & Telematics Card
  h += "<div class='msec'><div class='msec-title'>" + svg('car') + " Fleet Vehicle Telematics</div><div class='mgrid'>";
  h += "<div class='mrow'><div class='mk'>Transmission &amp; Color</div><div class='mv'>" + (evInfo.transmission||'Automatic') + " &bull; " + (evInfo.color||'Pearl White') + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Daily Rate</div><div class='mv'>" + money(evInfo.ratePerDay) + " / day</div></div>";
  h += "<div class='mrow'><div class='mk'>Current Odometer</div><div class='mv'>" + (evInfo.odometerKm||0).toLocaleString() + " km</div></div>";
  h += "<div class='mrow'><div class='mk'>Fuel Tank Level</div><div class='mv'>" + (evInfo.fuelLevelPct||100) + "% Available</div></div>";
  h += "</div></div>";

  // 3. Driver Profile Card with Driver PFP Avatar
  var drvInitials = ((evInfo.driverName || 'Driver').split(' ').map(function(s){return s[0];}).slice(0,2).join('')).toUpperCase();
  var drvPhoto = (evInfo.driverPhoto && evInfo.driverPhoto.length > 5) ? evInfo.driverPhoto : '';
  h += "<div class='msec'><div class='msec-title'>" + svg('driver') + " Assigned Driver Profile</div>";
  if(evInfo.driverName && evInfo.driverName !== 'Self-Drive'){
    h += "<div style='display:flex;align-items:center;gap:12px;margin-bottom:12px;padding-bottom:10px;border-bottom:1px solid rgba(255,255,255,.06)'>";
    if(drvPhoto){
      h += "<img src='" + drvPhoto + "' class='drv-pfp' onerror='this.style.display=\"none\";if(this.nextElementSibling)this.nextElementSibling.style.display=\"flex\"' alt='Driver Photo' />";
      h += "<div class='drv-pfp-fallback' style='display:none'>" + drvInitials + "</div>";
    } else {
      h += "<div class='drv-pfp-fallback'>" + drvInitials + "</div>";
    }
    h += "<div><div style='font-size:14px;font-weight:800;color:#fff'>" + (evInfo.driverName||'--') + "</div>";
    h += "<div style='font-size:11px;color:#10b981;font-weight:600;display:flex;align-items:center;gap:4px;margin-top:2px'>" + svg('shield') + " Official Company Chauffeur</div></div>";
    h += "</div>";
    h += "<div class='mgrid'>";
    h += "<div class='mrow'><div class='mk'>License No.</div><div class='mv'>" + (evInfo.driverLicense||'Verified') + "</div></div>";
    h += "<div class='mrow'><div class='mk'>Driver Phone</div><div class='mv'><span class='click-val' onclick='copyText(\"" + (evInfo.driverPhone||'') + "\", \"Driver Phone\")'>" + (evInfo.driverPhone||'--') + " " + svg('phone') + "</span></div></div>";
    h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Shift Schedule</div><div class='mv'>" + (evInfo.driverShift||'Flexible') + "</div></div>";
    h += "</div>";
  } else {
    h += "<div class='mv' style='font-weight:600;color:#94a3b8'>Customer Self-Drive Rental Package (No assigned chauffeur)</div>";
  }
  h += "</div>";

  // 4. Schedule & Return Inspection Card
  h += "<div class='msec'><div class='msec-title'>" + svg('calendar') + " Schedule &amp; Return Inspection</div><div class='mgrid'>";
  h += "<div class='mrow'><div class='mk'>Pickup Date</div><div class='mv'>" + fmt(evInfo.startDate) + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Scheduled Return</div><div class='mv'>" + fmt(evInfo.endDate) + "</div></div>";
  if(evInfo.returnDate){
    h += "<div class='mrow'><div class='mk'>Actual Return Date</div><div class='mv' style='color:#10b981'>" + evInfo.returnDate + "</div></div>";
    h += "<div class='mrow'><div class='mk'>Return Odometer / Fuel</div><div class='mv'>" + (evInfo.returnOdometer?evInfo.returnOdometer.toLocaleString()+' km':'--') + " &bull; " + (evInfo.returnFuelLevel||'--') + "</div></div>";
    if(evInfo.returnNotes){
      h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Return Inspection Notes</div><div class='mv' style='font-size:11px;color:#cbd5e1'>" + evInfo.returnNotes + "</div></div>";
    }
  }
  h += "</div></div>";

  // 5. Financials & Payment Method Card
  h += "<div class='msec'><div class='msec-title'>" + svg('peso') + " Financial Breakdown</div><div class='mgrid'>";
  h += "<div class='mrow'><div class='mk'>Total Rental Amount</div><div class='mv' style='color:#10b981;font-size:14px'>" + money(evInfo.totalAmount) + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Payment Channel</div><div class='mv' style='display:flex;align-items:center;gap:8px'>" + getPaymentLogoHtml(evInfo.paymentMethod, evInfo.paymentStatus) + " <span style='font-weight:700'>" + (evInfo.paymentMethod||'Cash').toUpperCase() + " <span style='font-size:11px;opacity:.7'>(" + (evInfo.paymentStatus||'Unpaid') + ")</span></span></div></div>";
  if(evInfo.penaltyFee > 0){
    h += "<div class='mrow'><div class='mk'>Overdue Penalty Fee</div><div class='mv' style='color:#ef4444'>" + money(evInfo.penaltyFee) + "</div></div>";
  }
  if(evInfo.damageFee > 0){
    h += "<div class='mrow'><div class='mk'>Damage Assessment Fee</div><div class='mv' style='color:#ef4444'>" + money(evInfo.damageFee) + "</div></div>";
  }
  h += "<div class='mrow'><div class='mk'>Booking Status</div><div class='mv'><span class='fpill' style='border-color:" + c + ";color:" + c + ";background:" + c + "18;display:inline-flex;padding:3px 10px'>" + (evInfo.status||'pending').toUpperCase() + "</span></div></div>";
  h += "</div></div>";

  // 6. Cryptographic Blockchain Seal
  if(evInfo.blockchainHash){
    h += "<div class='bc-card'>";
    h += "<div class='bc-hdr'><div class='bc-title'>" + svg('shield') + " Cryptographic Blockchain Seal</div><span style='font-size:10px;font-weight:700;color:#10b981'>VERIFIED IMMUTABLE</span></div>";
    h += "<div class='bc-hash'><span>" + evInfo.blockchainHash + "</span><button class='copy-btn' onclick='copyText(\"" + evInfo.blockchainHash + "\", \"Blockchain Hash\")'>" + svg('copy') + " Copy</button></div>";
    h += "</div>";
  }

  h += "</div>";

  h += "<div class='mfoot'>";
  h += "<button class='mclose' onclick='cm()'>" + svg('close') + " Close</button>";
  h += "</div></div></div>";
  document.body.insertAdjacentHTML('beforeend', h);
}

function cm(){var m=document.getElementById('mbg');if(m)m.remove();}

function openMaintModal(id, ev){
  if(ev)ev.stopPropagation();
  var m = null;
  for(var i=0;i<MAINTENANCE.length;i++){if(MAINTENANCE[i].maintenanceId===id){m=MAINTENANCE[i];break;}}
  if(!m)return;
  var h = "<div class='mbg' id='mbg' onclick=\"if(event.target.id==='mbg')cm()\">";
  h += "<div class='modal' style='width:450px'>";
  h += "<div class='mhdr'><div class='mtitle'>" + svg('tool') + " Maintenance Schedule</div></div>";
  h += "<div class='mbody'>";
  h += "<div class='msec'><div class='mgrid'>";
  h += "<div class='mrow'><div class='mk'>Vehicle</div><div class='mv'>" + (m.vehicleName||'Fleet') + " [" + (m.plateNo||'') + "]</div></div>";
  h += "<div class='mrow'><div class='mk'>Service Type</div><div class='mv' style='color:#d97706'>" + (m.type||'General PMS') + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Scheduled Date</div><div class='mv'>" + fmt(m.scheduledDate) + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Estimated Cost</div><div class='mv' style='color:#10b981'>" + money(m.cost) + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Status</div><div class='mv' style='text-transform:uppercase'>" + (m.status||'scheduled') + "</div></div>";
  h += "</div></div></div>";
  h += "<div class='mfoot'><button class='mclose' onclick='cm()'>" + svg('close') + " Close</button></div>";
  h += "</div></div>";
  document.body.insertAdjacentHTML('beforeend', h);
}

function openBdayModal(id, ev){
  if(ev)ev.stopPropagation();
  var b = null;
  for(var i=0;i<BIRTHDAYS.length;i++){if(BIRTHDAYS[i].id===id){b=BIRTHDAYS[i];break;}}
  if(!b)return;
  var h = "<div class='mbg' id='mbg' onclick=\"if(event.target.id==='mbg')cm()\">";
  h += "<div class='modal' style='width:420px'>";
  h += "<div class='mhdr' style='background:linear-gradient(135deg,#831843,#4c0519)'><div class='mtitle'>" + svg('cake') + " Company Birthday Greeting</div></div>";
  h += "<div class='mbody'>";
  h += "<div class='msec'><div class='mgrid'>";
  h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Celebrant</div><div class='mv' style='font-size:15px;color:#ec4899'>" + (b.name||b.fullName||'Celebrant') + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Company Role</div><div class='mv'>" + (b.role||'Team Member') + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Day of Month</div><div class='mv'>Day " + b.birthDay + "</div></div>";
  if(b.phone){
    h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Contact Phone</div><div class='mv'><span class='click-val' onclick='copyText(\"" + b.phone + "\", \"Phone\")'>" + b.phone + " " + svg('phone') + "</span></div></div>";
  }
  h += "</div></div></div>";
  h += "<div class='mfoot'><button class='mclose' onclick='cm()'>" + svg('close') + " Close</button></div>";
  h += "</div></div>";
  document.body.insertAdjacentHTML('beforeend', h);
}

function openNoteModal(id, ev){
  if(ev)ev.stopPropagation();
  var nt = null;
  for(var i=0;i<NOTES.length;i++){if(NOTES[i].noteId===id){nt=NOTES[i];break;}}
  if(!nt)return;
  var h = "<div class='mbg' id='mbg' onclick=\"if(event.target.id==='mbg')cm()\">";
  h += "<div class='modal' style='width:460px'>";
  h += "<div class='mhdr' style='background:linear-gradient(135deg,#0c4a6e,#082f49)'><div class='mtitle'>" + svg('note') + " Company Note / Reminder</div></div>";
  h += "<div class='mbody'>";
  h += "<div class='msec'><div class='mgrid'>";
  h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Subject</div><div class='mv' style='font-size:14px;color:#38bdf8'>" + (nt.title||'Note') + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Target Date</div><div class='mv'>" + fmt(nt.noteDate) + "</div></div>";
  h += "<div class='mrow'><div class='mk'>Category</div><div class='mv' style='text-transform:uppercase'>" + (nt.category||'Reminder') + "</div></div>";
  h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Details / Content</div><div class='mv' style='white-space:pre-wrap;font-weight:400;color:#cbd5e1'>" + (nt.content||'No content') + "</div></div>";
  h += "<div class='mrow' style='grid-column:span 2'><div class='mk'>Posted By</div><div class='mv' style='font-size:11px;color:#94a3b8'>" + (nt.createdBy||'Admin') + " &bull; " + (nt.createdAt||'') + "</div></div>";
  h += "</div></div></div>";
  h += "<div class='mfoot'>";
  h += "<button class='mclose' onclick='cm()'>" + svg('close') + " Close</button>";
  h += "<button class='mdelete' onclick='deleteCalendarNote(" + nt.noteId + ")'>" + svg('trash') + " Delete Note</button>";
  h += "</div></div></div>";
  document.body.insertAdjacentHTML('beforeend', h);
}

function openAddNoteModal(targetDate){
  var defaultDate = targetDate || (Y + '-' + String(M).padStart(2,'0') + '-' + String(TODAY.getDate()).padStart(2,'0'));
  var h = "<div class='mbg' id='mbg' onclick=\"if(event.target.id==='mbg')cm()\">";
  h += "<div class='modal' style='width:460px'>";
  h += "<div class='mhdr'><div class='mtitle'>" + svg('plus') + " Add Company Note / Reminder</div></div>";
  h += "<div class='mbody'>";
  h += "<div class='form-group'><label class='form-lbl'>Note Date</label><input type='date' id='inpDate' class='form-inp' value='" + defaultDate + "' required /></div>";
  h += "<div class='form-group'><label class='form-lbl'>Title / Subject</label><input type='text' id='inpTitle' class='form-inp' placeholder='e.g. VIP Booking Arrival, Garage Inspection...' required /></div>";
  h += "<div class='form-group'><label class='form-lbl'>Category</label><select id='inpCat' class='form-inp'><option value='reminder'>Reminder</option><option value='meeting'>Admin Meeting</option><option value='announcement'>Company Announcement</option><option value='inspection'>Fleet Inspection</option></select></div>";
  h += "<div class='form-group'><label class='form-lbl'>Details / Instructions</label><textarea id='inpContent' class='form-inp' rows='3' placeholder='Enter notes or instructions for the team...'></textarea></div>";
  h += "</div>";
  h += "<div class='mfoot'>";
  h += "<button class='mclose' onclick='cm()'>" + svg('close') + " Cancel</button>";
  h += "<button class='form-submit' onclick='submitNewNote()'>" + svg('check') + " Save Note</button>";
  h += "</div></div></div>";
  document.body.insertAdjacentHTML('beforeend', h);
}
function openAddNoteForDate(dStr, ev){
  if(ev)ev.stopPropagation();
  openAddNoteModal(dStr);
}
async function submitNewNote(){
  var d = document.getElementById('inpDate').value;
  var t = document.getElementById('inpTitle').value;
  var c = document.getElementById('inpCat').value;
  var cont = document.getElementById('inpContent').value;
  if(!d || !t){ showToast('Date and Title are required!'); return; }
  
  var payload = {
    noteDate: d,
    title: t,
    category: c || 'reminder',
    content: cont || '',
    createdBy: 'Admin'
  };

  try {
    var res = await fetch(API_BASE + '/rentals/calendar/notes', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if(res.ok){
      showToast('Note saved successfully!');
      cm();
      await fetchCalendarData(Y, M);
      return;
    }
  } catch(e) {}

  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage(JSON.stringify({
      action: 'saveNote',
      noteDate: d,
      title: t,
      category: c,
      content: cont
    }));
  }
  cm();
  showToast('Saving note to calendar...');
}

async function deleteCalendarNote(id){
  try {
    var res = await fetch(API_BASE + '/rentals/calendar/notes/' + id, {
      method: 'DELETE'
    });
    if(res.ok){
      showToast('Note deleted successfully.');
      cm();
      await fetchCalendarData(Y, M);
      return;
    }
  } catch(e) {}

  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage(JSON.stringify({
      action: 'deleteNote',
      noteId: id
    }));
  }
  cm();
  showToast('Note deleted.');
}

function onCellClick(dStr, ev){
  if(ev && (ev.target.classList.contains('ev') || ev.target.closest('.ev') || ev.target.classList.contains('more') || ev.target.classList.contains('add-day-btn') || ev.target.closest('.add-day-btn'))) return;
  var cd = new Date(dStr + 'T00:00:00');
  TODAY = cd;
  var nY = cd.getFullYear(), nM = cd.getMonth() + 1;
  if(nY !== Y || nM !== M){
    fetchCalendarData(nY, nM).then(function(){ sv('day'); });
  } else {
    sv('day');
  }
}

function onMonthSel(mVal){
  var nm = parseInt(mVal, 10);
  fetchCalendarData(Y, nm);
}
function onYearSel(yVal){
  var ny = parseInt(yVal, 10);
  fetchCalendarData(ny, M);
}

function navP(){
  if(VIEW==='month'){
    var nm = M - 1, ny = Y;
    if(nm < 1){ nm = 12; ny--; }
    fetchCalendarData(ny, nm);
  } else if(VIEW==='week'){
    var prevWeek = new Date(TODAY);
    prevWeek.setDate(TODAY.getDate() - 7);
    TODAY = prevWeek;
    if(TODAY.getFullYear() !== Y || (TODAY.getMonth() + 1) !== M){
      fetchCalendarData(TODAY.getFullYear(), TODAY.getMonth() + 1);
    } else {
      render();
    }
  } else {
    var prevDay = new Date(TODAY);
    prevDay.setDate(TODAY.getDate() - 1);
    TODAY = prevDay;
    if(TODAY.getFullYear() !== Y || (TODAY.getMonth() + 1) !== M){
      fetchCalendarData(TODAY.getFullYear(), TODAY.getMonth() + 1);
    } else {
      render();
    }
  }
}

function navN(){
  if(VIEW==='month'){
    var nm = M + 1, ny = Y;
    if(nm > 12){ nm = 1; ny++; }
    fetchCalendarData(ny, nm);
  } else if(VIEW==='week'){
    var nextWeek = new Date(TODAY);
    nextWeek.setDate(TODAY.getDate() + 7);
    TODAY = nextWeek;
    if(TODAY.getFullYear() !== Y || (TODAY.getMonth() + 1) !== M){
      fetchCalendarData(TODAY.getFullYear(), TODAY.getMonth() + 1);
    } else {
      render();
    }
  } else {
    var nextDay = new Date(TODAY);
    nextDay.setDate(TODAY.getDate() + 1);
    TODAY = nextDay;
    if(TODAY.getFullYear() !== Y || (TODAY.getMonth() + 1) !== M){
      fetchCalendarData(TODAY.getFullYear(), TODAY.getMonth() + 1);
    } else {
      render();
    }
  }
}

function goToday(){
  var n = new Date();
  TODAY = n;
  var ny = n.getFullYear(), nm = n.getMonth() + 1;
  if(ny !== Y || nm !== M){
    fetchCalendarData(ny, nm).then(function(){ sv('day'); });
  } else {
    sv('day');
  }
}

function sv(v){
  VIEW = v;
  document.querySelectorAll('.vtab').forEach(function(b){b.classList.remove('on');});
  var mp = {day:'vd', week:'vw', month:'vm'};
  if(document.getElementById(mp[v])) document.getElementById(mp[v]).classList.add('on');
  if(window.chrome && window.chrome.webview){
    window.chrome.webview.postMessage(JSON.stringify({action:'viewChanged', view:v}));
  }
  render();
}

function render(){
  stats();
  var sm = document.getElementById('selMonth'); if(sm) sm.value = M;
  var sy = document.getElementById('selYear'); if(sy) sy.value = Y;
  if(VIEW==='month') renderMonth();
  else if(VIEW==='week') renderWeek();
  else renderDay();
}

render();
""";
        }
    }
}
