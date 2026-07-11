#nullable disable
using System;
using System.Text;

namespace DriveAndGo_Admin.Panels
{
    /// <summary>
    /// Generates the full HTML/CSS/JS for the Calendar panel.
    /// Kept in a separate file so the JavaScript content never conflicts
    /// with C# string-literal escaping rules.
    /// </summary>
    internal static class CalendarHtmlGenerator
    {
        public static string Build(int year, int month, string eventsJson, string view, bool dark)
        {
            string bg   = dark ? "#0f172a" : "#f1f5f9";
            string card = dark ? "#1e293b" : "#ffffff";
            string txt  = dark ? "#e2e8f0" : "#1e293b";
            string sub  = dark ? "#94a3b8" : "#64748b";
            string bdr  = dark ? "#334155" : "#e2e8f0";

            // Dynamic header/navbar theme variables
            string hBg   = dark ? "#0f172a" : "#ffffff";
            string hBdr  = dark ? "rgba(255,255,255,.07)" : "rgba(0,0,0,.06)";
            string hTxt  = dark ? "#ffffff" : "#1e293b";
            string clk   = dark ? "#94a3b8" : "#64748b";
            string dsp   = dark ? "#e2e8f0" : "#334155";
            string btn   = dark ? "rgba(255,255,255,.08)" : "rgba(0,0,0,.04)";
            string btnBr = dark ? "rgba(255,255,255,.12)" : "rgba(0,0,0,.08)";
            string tab   = dark ? "rgba(255,255,255,.07)" : "rgba(0,0,0,.03)";
            string tabBr = dark ? "rgba(255,255,255,.1)"  : "rgba(0,0,0,.06)";

            var sb = new StringBuilder(16384);

            // ── HEAD ─────────────────────────────────────────────────────
            sb.Append("<!DOCTYPE html><html lang='en'><head>");
            sb.Append("<meta charset='UTF-8'>");
            sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            sb.Append("<title>DriveAndGo Calendar</title>");
            sb.Append("<style>");

            sb.Append("*{margin:0;padding:0;box-sizing:border-box}");
            sb.AppendFormat("body{{font-family:'Segoe UI',sans-serif;background:{0};color:{1};height:100vh;display:flex;flex-direction:column;overflow:hidden;user-select:none}}", bg, txt);

            // topbar
            sb.AppendFormat(".topbar{{background:{0};padding:10px 20px;display:flex;align-items:center;gap:12px;border-bottom:1px solid {1}}}", hBg, hBdr);
            sb.Append(".brand{font-size:13px;font-weight:700;color:#f97316;margin-right:auto}");
            sb.AppendFormat(".clock{{font-size:13px;color:{0};font-variant-numeric:tabular-nums}}", clk);
            sb.AppendFormat(".ddisp{{font-size:13px;font-weight:600;color:{0}}}", dsp);

            // navbar
            sb.AppendFormat(".navbar{{background:{0};padding:10px 20px;display:flex;align-items:center;gap:10px;border-bottom:1px solid {1}}}", hBg, hBdr);
            sb.AppendFormat(".nb{{background:{0};border:1px solid {1};color:{2};font-size:18px;width:34px;height:34px;border-radius:8px;cursor:pointer;transition:all .15s;display:flex;align-items:center;justify-content:center}}", btn, btnBr, txt);
            sb.Append(".nb:hover{background:rgba(249,115,22,.25);border-color:#f97316}");
            sb.AppendFormat(".ntitle{{font-size:18px;font-weight:700;color:{0};min-width:230px;text-align:center}}", hTxt);
            sb.Append(".tbtn{background:#f97316;border:none;color:#fff;font-size:11px;font-weight:700;padding:6px 14px;border-radius:8px;cursor:pointer;transition:filter .15s}");
            sb.Append(".tbtn:hover{filter:brightness(1.15)}");
            sb.Append(".vtabs{display:flex;gap:4px;margin-left:auto}");
            sb.AppendFormat(".vtab{{background:{0};border:1px solid {1};color:{2};font-size:11px;font-weight:600;padding:5px 14px;border-radius:8px;cursor:pointer;transition:all .15s}}", tab, tabBr, sub);
            sb.Append(".vtab.on,.vtab:hover{background:#f97316;border-color:#f97316;color:#fff}");

            // legend / stats
            sb.AppendFormat(".legbar{{padding:7px 20px;display:flex;gap:14px;flex-wrap:wrap;background:{0};border-bottom:1px solid {1}}}", hBg, hBdr);
            sb.AppendFormat(".li{{display:flex;align-items:center;gap:5px;font-size:10px;color:{0}}}", sub);
            sb.Append(".ld{width:10px;height:10px;border-radius:3px}");
            sb.AppendFormat(".sbar{{display:flex;gap:8px;padding:8px 20px;background:{0};border-bottom:1px solid {1}}}", hBg, hBdr);
            sb.Append(".sc{background:rgba(255,255,255,.05);border:1px solid rgba(255,255,255,.08);border-radius:10px;padding:6px 14px;text-align:center;min-width:80px}");
            sb.Append(".sv{font-size:16px;font-weight:800}");
            sb.AppendFormat(".sl{{font-size:9px;color:{0};text-transform:uppercase;letter-spacing:1px;margin-top:1px}}", sub);

            // calendar body
            sb.Append(".cbody{flex:1;overflow-y:auto;padding:0 18px 16px}");

            // month grid
            sb.Append(".grid{display:grid;grid-template-columns:repeat(7,1fr);gap:4px;margin-top:10px}");
            sb.AppendFormat(".dh{{text-align:center;font-size:10px;font-weight:700;color:{0};padding:8px 0;text-transform:uppercase;letter-spacing:1px}}", sub);
            sb.AppendFormat(".cell{{background:{0};border:1px solid {1};border-radius:10px;min-height:90px;padding:5px 6px;cursor:pointer;overflow:hidden;transition:border-color .15s,transform .15s,box-shadow .15s}}", card, bdr);
            sb.Append(".cell:hover{border-color:#f97316;transform:translateY(-1px);box-shadow:0 6px 18px rgba(249,115,22,.15)}");
            sb.Append(".cell.other{opacity:.28}");
            sb.Append(".cell.today{border:2px solid #3b82f6;box-shadow:0 0 0 3px rgba(59,130,246,.2)}");
            sb.AppendFormat(".dn{{font-size:11px;font-weight:700;color:{0};margin-bottom:3px}}", txt);
            sb.Append(".tdot{width:22px;height:22px;border-radius:50%;background:#3b82f6;color:#fff;font-size:11px;font-weight:800;display:inline-flex;align-items:center;justify-content:center}");

            // event pills
            sb.Append(".ev{border-radius:4px;padding:2px 5px;font-size:9px;font-weight:600;margin-bottom:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;cursor:pointer;transition:filter .12s}");
            sb.Append(".ev:hover{filter:brightness(1.25)}");
            sb.Append(".ep{background:rgba(245,158,11,.18);color:#f59e0b;border-left:3px solid #f59e0b}");
            sb.Append(".ea{background:rgba(34,197,94,.18);color:#22c55e;border-left:3px solid #22c55e}");
            sb.Append(".ex{background:rgba(59,130,246,.18);color:#3b82f6;border-left:3px solid #3b82f6}");
            sb.Append(".ec{background:rgba(148,163,184,.18);color:#94a3b8;border-left:3px solid #94a3b8}");
            sb.Append(".ek{background:rgba(239,68,68,.18);color:#ef4444;border-left:3px solid #ef4444}");
            sb.AppendFormat(".more{{font-size:9px;color:{0};margin-top:1px}}", sub);

            // week/day time grid
            sb.AppendFormat(".tlbl{{font-size:9px;color:{0};text-align:right;padding-right:8px;height:54px;padding-top:4px}}", sub);
            sb.AppendFormat(".dhdr{{text-align:center;padding:6px;font-size:10px;font-weight:700;color:{0};border-bottom:1px solid {1}}}", sub, bdr);
            sb.Append(".dhdr.tod{color:#3b82f6}");
            sb.AppendFormat(".hrow{{height:54px;border-bottom:1px solid {0};border-left:1px solid {0};padding:2px 4px}}", bdr);
            sb.Append(".wev{border-radius:4px;padding:2px 5px;font-size:9px;font-weight:600;margin-bottom:2px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;cursor:pointer}");
            sb.Append(".wev:hover{filter:brightness(1.2)}");

            // modal
            sb.Append(".mbg{position:fixed;inset:0;background:rgba(0,0,0,.7);z-index:999;display:flex;align-items:center;justify-content:center;backdrop-filter:blur(4px);animation:fin .2s ease}");
            sb.Append("@keyframes fin{from{opacity:0}to{opacity:1}}");
            sb.AppendFormat(".modal{{background:{0};border:1px solid rgba(249,115,22,.35);border-radius:18px;width:430px;max-width:93vw;box-shadow:0 32px 80px rgba(0,0,0,.6);animation:ms .22s cubic-bezier(.34,1.56,.64,1)}}", card);
            sb.Append("@keyframes ms{from{opacity:0;transform:scale(.88)}to{opacity:1;transform:none}}");
            sb.Append(".mhdr{background:linear-gradient(135deg,#1e3a5f,#0f172a);border-radius:18px 18px 0 0;padding:18px 22px;display:flex;align-items:center;gap:12px}");
            sb.Append(".mico{width:42px;height:42px;border-radius:10px;display:flex;align-items:center;justify-content:center;font-size:20px;flex-shrink:0}");
            sb.Append(".mtitle{font-size:15px;font-weight:700;color:#fff}");
            sb.Append(".msub{font-size:11px;color:#94a3b8;margin-top:2px}");
            sb.Append(".mbody{padding:18px 22px}");
            sb.AppendFormat(".mrow{{display:flex;align-items:flex-start;gap:10px;margin-bottom:12px;padding-bottom:12px;border-bottom:1px solid {0}}}", bdr);
            sb.Append(".mrow:last-child{border-bottom:none;margin-bottom:0;padding-bottom:0}");
            sb.AppendFormat(".mk{{font-size:10px;color:{0};text-transform:uppercase;letter-spacing:.8px;margin-bottom:3px}}", sub);
            sb.AppendFormat(".mv{{font-size:13px;font-weight:600;color:{0}}}", txt);
            sb.Append(".badge{display:inline-flex;align-items:center;gap:5px;padding:4px 12px;border-radius:20px;font-size:11px;font-weight:700;letter-spacing:.3px}");
            sb.Append(".mclose{background:rgba(239,68,68,.1);border:1px solid rgba(239,68,68,.3);color:#ef4444;padding:10px;border-radius:10px;cursor:pointer;width:100%;font-size:13px;font-weight:600;margin-top:4px;transition:all .15s}");
            sb.Append(".mclose:hover{background:rgba(239,68,68,.25)}");

            sb.Append("</style></head><body>");

            // ── HTML STRUCTURE ───────────────────────────────────────────
            sb.Append("<div class='topbar'>");
            sb.Append("<span class='brand'>&#11042; DriveAndGo Admin</span>");
            sb.Append("<span class='ddisp' id='dd'></span>");
            sb.Append("<span class='clock' id='ck'></span>");
            sb.Append("</div>");

            sb.Append("<div class='navbar'>");
            sb.Append("<button class='nb' onclick='navP()'>&#8249;</button>");
            sb.Append("<div class='ntitle' id='nt'></div>");
            sb.Append("<button class='nb' onclick='navN()'>&#8250;</button>");
            sb.Append("<button class='tbtn' onclick='goToday()'>TODAY</button>");
            sb.Append("<div class='vtabs'>");
            sb.Append("<button class='vtab' id='vd' onclick='sv(\"day\")'>Day</button>");
            sb.Append("<button class='vtab' id='vw' onclick='sv(\"week\")'>Week</button>");
            sb.Append("<button class='vtab on' id='vm' onclick='sv(\"month\")'>Month</button>");
            sb.Append("</div></div>");

            sb.Append("<div class='legbar'>");
            sb.Append("<div class='li'><div class='ld' style='background:#f59e0b'></div>Pending</div>");
            sb.Append("<div class='li'><div class='ld' style='background:#22c55e'></div>Approved</div>");
            sb.Append("<div class='li'><div class='ld' style='background:#3b82f6'></div>Active</div>");
            sb.Append("<div class='li'><div class='ld' style='background:#94a3b8'></div>Completed</div>");
            sb.Append("<div class='li'><div class='ld' style='background:#ef4444'></div>Cancelled</div>");
            sb.Append("</div>");
            sb.Append("<div class='sbar' id='sb'></div>");
            sb.Append("<div class='cbody' id='cb'></div>");

            // ── JAVASCRIPT ───────────────────────────────────────────────
            sb.Append("<script>");

            // Inject C# values safely — only these two lines use AppendFormat
            sb.AppendFormat("var EVENTS={0};", eventsJson);
            sb.AppendFormat("var Y={0},M={1},VIEW='{2}';", year, month, view);

            // All remaining JS is pure string — zero C# interpolation
            sb.Append(JsBody());

            sb.Append("</script></body></html>");
            return sb.ToString();
        }

        // ── All JavaScript in one method ──────────────────────────────────
        // Using string concatenation at compile time (no runtime C# interpolation)
        // so curly braces, single quotes, etc. are all safe.
        private static string JsBody()
        {
            return
"var TODAY=new Date();" +
"var MN=['January','February','March','April','May','June','July','August','September','October','November','December'];" +
"var DN=['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];" +
"var DS=['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];" +

// Live clock
"function tick(){" +
"  var n=new Date(),h=n.getHours()%12||12,mi=n.getMinutes(),s=n.getSeconds(),ap=n.getHours()<12?'AM':'PM';" +
"  document.getElementById('ck').textContent=h+':'+(mi<10?'0':'')+mi+':'+(s<10?'0':'')+s+' '+ap;" +
"  document.getElementById('dd').textContent=DN[n.getDay()]+', '+MN[n.getMonth()]+' '+n.getDate()+', '+n.getFullYear();" +
"}" +
"setInterval(tick,1000);tick();" +

// Status helpers
"function sc(s){" +
"  var m=(s||'').toLowerCase().replace(/[-_ ]/g,'');" +
"  if(m.indexOf('pending')>=0)return 'ep';" +
"  if(m.indexOf('approved')>=0)return 'ea';" +
"  if(m.indexOf('active')>=0||m.indexOf('inuse')>=0)return 'ex';" +
"  if(m.indexOf('completed')>=0||m.indexOf('returned')>=0)return 'ec';" +
"  return 'ek';" +
"}" +
"function scol(s){" +
"  var m=(s||'').toLowerCase();" +
"  if(m.indexOf('pending')>=0)return '#f59e0b';" +
"  if(m.indexOf('approved')>=0)return '#22c55e';" +
"  if(m.indexOf('active')>=0||m.indexOf('in-use')>=0)return '#3b82f6';" +
"  if(m.indexOf('completed')>=0||m.indexOf('returned')>=0)return '#94a3b8';" +
"  return '#ef4444';" +
"}" +
"function money(n){return '\u20b1'+parseFloat(n||0).toLocaleString('en-PH',{minimumFractionDigits:2});}" +
"function fmt(d){" +
"  if(!d)return '--';" +
"  var dt=new Date(d+'T00:00:00');" +
"  return MN[dt.getMonth()].slice(0,3)+' '+dt.getDate()+', '+dt.getFullYear();" +
"}" +
"function same(a,b){return a.getFullYear()===b.getFullYear()&&a.getMonth()===b.getMonth()&&a.getDate()===b.getDate();}" +
"function ico(s){" +
"  var m=(s||'').toLowerCase();" +
"  if(m.indexOf('active')>=0||m.indexOf('in-use')>=0)return '&#128663;';" +
"  if(m.indexOf('pending')>=0)return '&#9203;';" +
"  if(m.indexOf('approved')>=0)return '&#9989;';" +
"  if(m.indexOf('completed')>=0)return '&#127937;';" +
"  return '&#10060;';" +
"}" +

// Stats bar
"function stats(){" +
"  var p=0,a=0,x=0,c=0,k=0,rev=0;" +
"  EVENTS.forEach(function(e){" +
"    var s=(e.status||'').toLowerCase();" +
"    if(s.indexOf('pending')>=0)p++;" +
"    else if(s.indexOf('approved')>=0)a++;" +
"    else if(s.indexOf('active')>=0||s.indexOf('in-use')>=0)x++;" +
"    else if(s.indexOf('completed')>=0||s.indexOf('returned')>=0)c++;" +
"    else if(s.indexOf('cancelled')>=0)k++;" +
"    if((e.paymentStatus||'').toLowerCase()==='paid')rev+=parseFloat(e.totalAmount||0);" +
"  });" +
"  document.getElementById('sb').innerHTML=" +
"    \"<div class='sc'><div class='sv' style='color:#f59e0b'>\"+p+\"</div><div class='sl'>Pending</div></div>\"+" +
"    \"<div class='sc'><div class='sv' style='color:#22c55e'>\"+a+\"</div><div class='sl'>Approved</div></div>\"+" +
"    \"<div class='sc'><div class='sv' style='color:#3b82f6'>\"+x+\"</div><div class='sl'>Active</div></div>\"+" +
"    \"<div class='sc'><div class='sv' style='color:#94a3b8'>\"+c+\"</div><div class='sl'>Completed</div></div>\"+" +
"    \"<div class='sc'><div class='sv' style='color:#ef4444'>\"+k+\"</div><div class='sl'>Cancelled</div></div>\"+" +
"    \"<div class='sc' style='margin-left:auto'><div class='sv' style='color:#10b981'>\"+money(rev)+\"</div><div class='sl'>Revenue</div></div>\";" +
"}" +

// Month render
"function renderMonth(){" +
"  document.getElementById('nt').textContent=MN[M-1]+' '+Y;" +
"  var dim=new Date(Y,M,0).getDate();" +
"  var fd=new Date(Y,M-1,1).getDay();" +
"  var ld=new Date(Y,M-1,dim).getDay();" +
"  var prev=fd,next=ld===6?0:6-ld;" +
"  var pmd=new Date(Y,M-1,0).getDate();" +
"  var total=prev+dim+next;" +
"  var h=\"<div class='grid'>\";" +
"  DS.forEach(function(d){h+=\"<div class='dh'>\"+d+\"</div>\";});" +
"  for(var i=0;i<total;i++){" +
"    var dn,other=false,date;" +
"    if(i<prev){dn=pmd-prev+i+1;other=true;var pm=M===1?12:M-1;var py=M===1?Y-1:Y;date=new Date(py,pm-1,dn);}" +
"    else if(i<prev+dim){dn=i-prev+1;date=new Date(Y,M-1,dn);}" +
"    else{dn=i-prev-dim+1;other=true;var nm=M===12?1:M+1;var ny=M===12?Y+1:Y;date=new Date(ny,nm-1,dn);}" +
"    var isT=!other&&same(date,TODAY);" +
"    var evs=EVENTS.filter(function(e){" +
"      var s=new Date(e.startDate+'T00:00:00');" +
"      var en=e.endDate?new Date(e.endDate+'T23:59:59'):new Date(e.startDate+'T23:59:59');" +
"      return date>=s&&date<=en;" +
"    });" +
"    var shown=evs.slice(0,3),extra=evs.length-3;" +
"    var eH='';" +
"    shown.forEach(function(e){" +
"      eH+=\"<div class='ev \"+sc(e.status)+\"' onclick='om(\"+e.rentalId+\", event)'>\"+"+
"           (e.plateNo||'')+' '+(e.vehicleName||'').split(' ').pop()+\"</div>\";" +
"    });" +
"    if(extra>0)eH+=\"<div class='more'>+\"+extra+\" more</div>\";" +
"    var dateStr = date.getFullYear() + '-' + String(date.getMonth()+1).padStart(2,'0') + '-' + String(date.getDate()).padStart(2,'0');" +
"    h+=\"<div class='cell\"+(other?' other':'')+(isT?' today':'')+\"' onclick='cc(\\\"\"+dateStr+\"\\\", event)'>\"+" +
"       \"<div class='dn'>\"+(isT?\"<span class='tdot'>\"+dn+\"</span>\":dn)+\"</div>\"+eH+\"</div>\";" +
"  }" +
"  h+='</div>';" +
"  document.getElementById('cb').innerHTML=h;" +
"}" +

// Week render
"function renderWeek(){" +
"  var ref=new Date(Y,M-1,TODAY.getDate());" +
"  var dow=ref.getDay(),ws=new Date(ref);ws.setDate(ref.getDate()-dow);" +
"  var days=[];" +
"  for(var i=0;i<7;i++){var d=new Date(ws);d.setDate(ws.getDate()+i);days.push(d);}" +
"  var e6=days[6];" +
"  document.getElementById('nt').textContent=MN[ws.getMonth()].slice(0,3)+' '+ws.getDate()+'  \u2013  '+MN[e6.getMonth()].slice(0,3)+' '+e6.getDate()+', '+e6.getFullYear();" +
"  var h=\"<div style='display:grid;grid-template-columns:54px repeat(7,1fr);margin-top:10px'><div></div>\";" +
"  days.forEach(function(d){" +
"    var iT=same(d,TODAY);" +
"    h+=\"<div class='dhdr\"+(iT?' tod':'')+\"'>\"+DS[d.getDay()]+\"<br>\"+" +
"       \"<span style='font-size:15px;font-weight:800;color:\"+(iT?'#3b82f6':'inherit')+\"'>\"+d.getDate()+\"</span></div>\";" +
"  });" +
"  for(var hr=0;hr<24;hr++){" +
"    var lbl=hr===0?'12 AM':(hr<12?hr+' AM':(hr===12?'12 PM':(hr-12)+' PM'));" +
"    h+=\"<div class='tlbl'>\"+lbl+\"</div>\";" +
"    days.forEach(function(d){" +
"      var evs=EVENTS.filter(function(e){return same(d,new Date(e.startDate+'T00:00:00'));});" +
"      var inner='';" +
"      if(hr===9)evs.forEach(function(e){" +
"        inner+=\"<div class='wev \"+sc(e.status)+\"' onclick='om(\"+e.rentalId+\", event)'>\"+"+
"               (e.plateNo||'')+' '+(e.customerName||'').split(' ')[0]+\"</div>\";" +
"      });" +
"      h+=\"<div class='hrow'>\"+inner+\"</div>\";" +
"    });" +
"  }" +
"  h+='</div>';" +
"  document.getElementById('cb').innerHTML=h;" +
"}" +

// Day render
"function renderDay(){" +
"  var d=new Date(Y,M-1,TODAY.getDate());" +
"  document.getElementById('nt').textContent=DN[d.getDay()]+', '+MN[d.getMonth()]+' '+d.getDate()+', '+Y;" +
"  var evs=EVENTS.filter(function(e){" +
"    var s=new Date(e.startDate+'T00:00:00');" +
"    var en=e.endDate?new Date(e.endDate+'T23:59:59'):new Date(e.startDate+'T23:59:59');" +
"    return d>=s&&d<=en;" +
"  });" +
"  var h=\"<div style='display:grid;grid-template-columns:54px 1fr;margin-top:10px'>\";" +
"  for(var hr=0;hr<24;hr++){" +
"    var lbl=hr===0?'12 AM':(hr<12?hr+' AM':(hr===12?'12 PM':(hr-12)+' PM'));" +
"    h+=\"<div class='tlbl'>\"+lbl+\"</div>\";" +
"    var inner='';" +
"    if(hr===9&&evs.length)evs.forEach(function(e){" +
"      var c=scol(e.status);" +
"      inner+=\"<div onclick='om(\"+e.rentalId+\", event)' style='background:\"+c+\"22;border-left:3px solid \"+c+\";border-radius:6px;padding:8px 10px;margin-bottom:4px;cursor:pointer'>\";" +
"      inner+=\"<div style='font-size:11px;font-weight:700;color:\"+c+\"'>\"+e.vehicleName+\"</div>\";" +
"      inner+=\"<div style='font-size:10px;color:#94a3b8'>\"+e.customerName+\"</div>\";" +
"      inner+=\"<div style='font-size:9px;color:#94a3b8;margin-top:2px'>\"+fmt(e.startDate)+' \u2192 '+fmt(e.endDate)+\"</div></div>\";" +
"    });" +
"    h+=\"<div class='hrow'>\"+inner+\"</div>\";" +
"  }" +
"  h+='</div>';" +
"  if(!evs.length)h+=\"<div style='text-align:center;padding:40px;color:#94a3b8;font-size:13px'>No rentals for this day</div>\";" +
"  document.getElementById('cb').innerHTML=h;" +
"}" +

// Modal
"function om(id, ev){" +
"  if(ev)ev.stopPropagation();" +
"  var evInfo=null;" +
"  for(var i=0;i<EVENTS.length;i++){if(EVENTS[i].rentalId===id){evInfo=EVENTS[i];break;}}" +
"  if(!evInfo)return;" +
"  var c=scol(evInfo.status),ic=ico(evInfo.status);" +
"  var h=\"<div class='mbg' id='mbg' onclick=\\\"if(event.target.id==='mbg')cm()\\\">\";" +
"  h+=\"<div class='modal'>\";" +
"  h+=\"<div class='mhdr'>\";" +
"  h+=\"<div class='mico' style='background:\"+c+\"22;border:1px solid \"+c+\"44'>\"+ic+\"</div>\";" +
"  h+=\"<div><div class='mtitle'>\"+(evInfo.vehicleName||'Vehicle')+\"</div>\";" +
"  h+=\"<div class='msub'>Rental #\"+evInfo.rentalId+\" &middot; \"+(evInfo.plateNo||'')+\"</div></div></div>\";" +
"  h+=\"<div class='mbody'>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#128100;</span><div><div class='mk'>Customer</div><div class='mv'>\"+(evInfo.customerName||'&mdash;')+\"</div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#128663;</span><div><div class='mk'>Vehicle &amp; Plate</div><div class='mv'>\"+(evInfo.vehicleName||'&mdash;')+\" <span style='color:#94a3b8;font-size:11px'>[\"+(evInfo.plateNo||'')+\"]</span></div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#129333;</span><div><div class='mk'>Driver</div><div class='mv'>\"+(evInfo.driverName&&evInfo.driverName!='Self-Drive'?evInfo.driverName:'Self-Drive')+\"</div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#128197;</span><div><div class='mk'>Pickup Date</div><div class='mv'>\"+fmt(evInfo.startDate)+\"</div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#127937;</span><div><div class='mk'>Return Date</div><div class='mv'>\"+fmt(evInfo.endDate)+\"</div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#128176;</span><div><div class='mk'>Amount / Payment</div><div class='mv'>\"+money(evInfo.totalAmount)+\" <span style='color:#94a3b8;font-size:10px'>(\"+(evInfo.paymentStatus||'Unpaid')+\")</span></div></div></div>\";" +
"  h+=\"<div class='mrow'><span style='font-size:16px'>&#127919;</span><div><div class='mk'>Status</div>\";" +
"  h+=\"<div><span class='badge' style='background:\"+c+\"22;color:\"+c+\";border:1px solid \"+c+\"44'>\"+ic+' '+(evInfo.status||'')+\"</span></div></div></div>\";" +
"  h+=\"<button class='mclose' onclick='cm()'>&#10005; &nbsp;Close</button>\";" +
"  h+=\"</div></div></div>\";" +
"  document.body.insertAdjacentHTML('beforeend',h);" +
"}" +
"function cm(){var m=document.getElementById('mbg');if(m)m.remove();}" +

// Cell click navigation
"function cc(dStr, ev){" +
"  if(ev && (ev.target.classList.contains('ev') || ev.target.closest('.ev') || ev.target.classList.contains('more'))) return;" +
"  var cd=new Date(dStr+'T00:00:00');" +
"  TODAY=cd;" +
"  var nY=cd.getFullYear(), nM=cd.getMonth()+1;" +
"  if(nY!==Y || nM!==M){" +
"    Y=nY; M=nM;" +
"    window.chrome.webview.postMessage(JSON.stringify({action:'viewChanged', view:'day'}));" +
"    window.chrome.webview.postMessage(JSON.stringify({action:'navigate', year:Y, month:M}));" +
"  } else {" +
"    sv('day');" +
"  }" +
"}" +

// Navigation
"function navP(){" +
"  if(VIEW==='month'){" +
"    M--;if(M<1){M=12;Y--;}window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"  } else if(VIEW==='week'){" +
"    var prevWeek=new Date(TODAY);prevWeek.setDate(TODAY.getDate()-7);" +
"    var oldM=M,oldY=Y;TODAY=prevWeek;Y=TODAY.getFullYear();M=TODAY.getMonth()+1;" +
"    if(Y!==oldY||M!==oldM) window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"    else render();" +
"  } else {" +
"    var prevDay=new Date(TODAY);prevDay.setDate(TODAY.getDate()-1);" +
"    var oldM=M,oldY=Y;TODAY=prevDay;Y=TODAY.getFullYear();M=TODAY.getMonth()+1;" +
"    if(Y!==oldY||M!==oldM) window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"    else render();" +
"  }" +
"}" +
"function navN(){" +
"  if(VIEW==='month'){" +
"    M++;if(M>12){M=1;Y++;}window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"  } else if(VIEW==='week'){" +
"    var nextWeek=new Date(TODAY);nextWeek.setDate(TODAY.getDate()+7);" +
"    var oldM=M,oldY=Y;TODAY=nextWeek;Y=TODAY.getFullYear();M=TODAY.getMonth()+1;" +
"    if(Y!==oldY||M!==oldM) window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"    else render();" +
"  } else {" +
"    var nextDay=new Date(TODAY);nextDay.setDate(TODAY.getDate()+1);" +
"    var oldM=M,oldY=Y;TODAY=nextDay;Y=TODAY.getFullYear();M=TODAY.getMonth()+1;" +
"    if(Y!==oldY||M!==oldM) window.chrome.webview.postMessage(JSON.stringify({action:'navigate',year:Y,month:M}));" +
"    else render();" +
"  }" +
"}" +
"function goToday(){" +
"  var n=new Date();" +
"  var oldM=M,oldY=Y;TODAY=n;Y=n.getFullYear();M=n.getMonth()+1;" +
"  if(Y!==oldY||M!==oldM) {" +
"    window.chrome.webview.postMessage(JSON.stringify({action:'viewChanged', view:'day'}));" +
"    window.chrome.webview.postMessage(JSON.stringify({action:'navigate', year:Y, month:M}));" +
"  } else {" +
"    sv('day');" +
"  }" +
"}" +

// View switcher
"function sv(v){" +
"  VIEW=v;" +
"  document.querySelectorAll('.vtab').forEach(function(b){b.classList.remove('on');});" +
"  var mp={day:'vd',week:'vw',month:'vm'};" +
"  document.getElementById(mp[v]).classList.add('on');" +
"  window.chrome.webview.postMessage(JSON.stringify({action:'viewChanged', view:v}));" +
"  render();" +
"}" +
"function render(){stats();if(VIEW==='month')renderMonth();else if(VIEW==='week')renderWeek();else renderDay();}" +
"sv(VIEW);";
        }
    }
}
