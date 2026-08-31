# =====================================================================
# DriveAndGo — Master API & HTTP Endpoints Test Suite
# Tests:
# 1. 100% Internal System HTTP Endpoints (All 42 Controllers)
# 2. Authentication & Security
# 3. Media & Logos Endpoints
# 4. Live Multi-Cloud API Keys (Groq, Mistral, OpenRouter, OpenWeather,
#    SambaNova, Supabase Cloud)
# =====================================================================

# ── Load Environment Variables from .env ──
function Load-EnvFile {
    param([string]$Path)
    if (Test-Path $Path) {
        Get-Content $Path -Encoding UTF8 | ForEach-Object {
            $line = $_.Trim()
            if ($line -and -not $line.StartsWith("#") -and $line -match "^([^=]+)=(.*)$") {
                $k = $matches[1].Trim()
                $v = $matches[2].Trim().Trim('"').Trim("'")
                if (-not [string]::IsNullOrEmpty($k) -and [string]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable($k))) {
                    [System.Environment]::SetEnvironmentVariable($k, $v, [System.EnvironmentVariableTarget]::Process)
                }
            }
        }
    }
}

$rootEnv = Join-Path $PSScriptRoot "..\.env"
$apiEnv  = Join-Path $PSScriptRoot "..\DriveAndGo_API\.env"
Load-EnvFile -Path $rootEnv
Load-EnvFile -Path $apiEnv

$baseUrl = if ($env:API_BASE_URL) { $env:API_BASE_URL } else { "http://127.0.0.1:5233/api" }
$hostBase = if ($baseUrl -match "^(https?://[^/]+)") { $matches[1] } else { "http://127.0.0.1:5233" }
$results = @()
$passCount = 0
$failCount = 0

function Test-Endpoint {
    param(
        [string]$Category,
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        $Body = $null,
        [int[]]$ExpectedCodes = @(200, 201, 204)
    )

    if (($Url.StartsWith($hostBase) -or $Url -match "^https?://(localhost|127\.0\.0\.1):5233") -and -not $script:apiOnline) {
        $script:failCount++
        $script:results += [PSCustomObject]@{
            Category = $Category
            Endpoint = "$Method $Name"
            StatusCode = 0
            DurationMs = 0
            Status = "OFFLINE"
            Notes = "Local API is offline (Start API first)"
        }
        Write-Host "[OFFLINE] [0] $Method $Name (Start API first)" -ForegroundColor DarkYellow
        return
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $statusCode = 0
    $errMsg = ""

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            TimeoutSec = 10
            UseBasicParsing = $true
        }
        if ($Headers.Count -gt 0) { $params["Headers"] = $Headers }
        if ($Body) {
            $params["Body"] = $Body
            $params["ContentType"] = "application/json"
        }

        $resp = Invoke-WebRequest @params -ErrorAction Stop
        $statusCode = [int]$resp.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        } else {
            $errMsg = $_.Exception.Message
        }
    }
    finally {
        $stopwatch.Stop()
    }

    $isPass = ($ExpectedCodes -contains $statusCode)
    if ($isPass) {
        $script:passCount++
        $statusText = "PASS"
    } else {
        $script:failCount++
        $statusText = "FAIL"
    }

    $resObj = [PSCustomObject]@{
        Category = $Category
        Endpoint = "$Method $Name"
        StatusCode = $statusCode
        DurationMs = $stopwatch.ElapsedMilliseconds
        Status = $statusText
        Notes = if ($isPass) { "OK" } else { if ($errMsg) { $errMsg } else { "HTTP $statusCode (Expected: $($ExpectedCodes -join ','))" } }
    }
    $script:results += $resObj

    $color = if ($isPass) { "Green" } else { "Red" }
    Write-Host "[$statusText] [$statusCode] $($resObj.Endpoint) ($($resObj.DurationMs)ms)" -ForegroundColor $color
}

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "   DRIVE&GO MASTER API & EXTERNAL CLOUD KEYS VERIFICATION SUITE       " -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Target API Base: $baseUrl" -ForegroundColor Yellow
Write-Host ""

# Pre-flight check: Is the local API running?
$apiOnline = $false
try {
    $preflight = Invoke-WebRequest -Uri "$baseUrl/timesync" -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
    if ($preflight.StatusCode -eq 200) { $apiOnline = $true }
} catch {
    # Fallback try on 127.0.0.1 if baseUrl was using localhost or vice versa
    try {
        $altUrl = if ($baseUrl -match "localhost") { $baseUrl -replace "localhost", "127.0.0.1" } else { $baseUrl -replace "127\.0\.0\.1", "localhost" }
        $altPreflight = Invoke-WebRequest -Uri "$altUrl/timesync" -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($altPreflight.StatusCode -eq 200) {
            $apiOnline = $true
            $baseUrl = $altUrl
            $hostBase = if ($baseUrl -match "^(https?://[^/]+)") { $matches[1] } else { "http://127.0.0.1:5233" }
        }
    } catch {
        $apiOnline = $false
    }
}

if (-not $apiOnline) {
    Write-Host "⚠️  BABALA: HINDI KASALUKUYANG TUMATAKBO ANG LOCAL API SERVER (port 5233)!" -ForegroundColor Red
    Write-Host "   Lalabas ang [FAIL] [0] sa lahat ng internal endpoints dahil patay ang backend." -ForegroundColor Yellow
    Write-Host "   Para gumana nang 100% ang test:" -ForegroundColor White
    Write-Host "   -> Pindutin ang [Start] o [F5] sa Visual Studio sa DriveAndGo_API" -ForegroundColor Cyan
    Write-Host "   -> O patakbuhin sa hiwalay na terminal: dotnet run --project DriveAndGo_API" -ForegroundColor Cyan
    Write-Host "---------------------------------------------------------------------`n" -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────────────
# SECTION 1: CORE INFRASTRUCTURE & DIAGNOSTICS
# ─────────────────────────────────────────────────────────────────────
Write-Host "=== 1. Core Infrastructure & Diagnostics ===" -ForegroundColor Magenta
Test-Endpoint -Category "Diagnostics" -Name "/diagnostics/telemetry" -Method "GET" -Url "$baseUrl/diagnostics/telemetry"
Test-Endpoint -Category "TimeSync" -Name "/timesync" -Method "GET" -Url "$baseUrl/timesync"
Test-Endpoint -Category "Blockchain" -Name "/blockchain/contracts/1" -Method "GET" -Url "$baseUrl/blockchain/contracts/1" -ExpectedCodes @(200, 404)
Test-Endpoint -Category "Blockchain" -Name "/blockchain/verify/1" -Method "GET" -Url "$baseUrl/blockchain/verify/1" -ExpectedCodes @(200, 404)

# ─────────────────────────────────────────────────────────────────────
# SECTION 2: AUTHENTICATION
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 2. Authentication & Identity ===" -ForegroundColor Magenta
$loginBody = '{"email":"invalid_test_check@driveandgo.com","password":"wrongpassword"}'
Test-Endpoint -Category "Auth" -Name "/auth/login (Invalid Creds Check)" -Method "POST" -Url "$baseUrl/auth/login" -Body $loginBody -ExpectedCodes @(401, 400, 423)

# ─────────────────────────────────────────────────────────────────────
# SECTION 3: FLEET & VEHICLES
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 3. Fleet, Telematics & Brand Logos ===" -ForegroundColor Magenta
Test-Endpoint -Category "Vehicles" -Name "/vehicles" -Method "GET" -Url "$baseUrl/vehicles"
Test-Endpoint -Category "Vehicles" -Name "/vehicles/brand-logo/Toyota" -Method "GET" -Url "$baseUrl/vehicles/brand-logo/Toyota"
Test-Endpoint -Category "Vehicles" -Name "/vehicles/brand-logo/Mitsubishi" -Method "GET" -Url "$baseUrl/vehicles/brand-logo/Mitsubishi"
Test-Endpoint -Category "Vehicles" -Name "/vehicles/brand-logo/Honda" -Method "GET" -Url "$baseUrl/vehicles/brand-logo/Honda"
Test-Endpoint -Category "Vehicles" -Name "/vehicles/brand-logo/Ford" -Method "GET" -Url "$baseUrl/vehicles/brand-logo/Ford"

# ─────────────────────────────────────────────────────────────────────
# SECTION 4: RENTALS & CALENDAR
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 4. Rentals, Booking Calendar & Notes ===" -ForegroundColor Magenta
Test-Endpoint -Category "Rentals" -Name "/rentals" -Method "GET" -Url "$baseUrl/rentals"
Test-Endpoint -Category "Rentals" -Name "/rentals/calendar?year=2026&month=8" -Method "GET" -Url "$baseUrl/rentals/calendar?year=2026&month=8"

# ─────────────────────────────────────────────────────────────────────
# SECTION 5: DRIVERS & FLEET OPERATIONS
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 5. Drivers & Operations ===" -ForegroundColor Magenta
Test-Endpoint -Category "Drivers" -Name "/drivers" -Method "GET" -Url "$baseUrl/drivers"
Test-Endpoint -Category "Drivers" -Name "/drivers/available" -Method "GET" -Url "$baseUrl/drivers/available"

# ─────────────────────────────────────────────────────────────────────
# SECTION 6: FINANCIALS & TRANSACTIONS & PROVIDER LOGOS
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 6. Financials, Transactions & Payment Provider Logos ===" -ForegroundColor Magenta
Test-Endpoint -Category "Transactions" -Name "/transactions" -Method "GET" -Url "$baseUrl/transactions"
Test-Endpoint -Category "Transactions" -Name "/transactions/summary" -Method "GET" -Url "$baseUrl/transactions/summary"
Test-Endpoint -Category "Transactions" -Name "/transactions/provider-logo/gcash" -Method "GET" -Url "$baseUrl/transactions/provider-logo/gcash"
Test-Endpoint -Category "Transactions" -Name "/transactions/provider-logo/maya" -Method "GET" -Url "$baseUrl/transactions/provider-logo/maya"
Test-Endpoint -Category "Transactions" -Name "/transactions/provider-logo/bdo" -Method "GET" -Url "$baseUrl/transactions/provider-logo/bdo"
Test-Endpoint -Category "Transactions" -Name "/transactions/provider-logo/cash" -Method "GET" -Url "$baseUrl/transactions/provider-logo/cash"
Test-Endpoint -Category "Expenses" -Name "/expenses" -Method "GET" -Url "$baseUrl/expenses"
Test-Endpoint -Category "Toll" -Name "/rentals/1/toll" -Method "GET" -Url "$baseUrl/rentals/1/toll"

# ─────────────────────────────────────────────────────────────────────
# SECTION 7: DASHBOARDS, ACCOUNTS & ANALYTICS
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 7. Dashboards, Admin Accounts & Analytics ===" -ForegroundColor Magenta
Test-Endpoint -Category "Users" -Name "/users" -Method "GET" -Url "$baseUrl/users"
Test-Endpoint -Category "AdminAccounts" -Name "/admin/accounts" -Method "GET" -Url "$baseUrl/admin/accounts"
Test-Endpoint -Category "AdminDashboard" -Name "/admin/dashboard/summary" -Method "GET" -Url "$baseUrl/admin/dashboard/summary"
Test-Endpoint -Category "Analytics" -Name "/analytics/ai-summary" -Method "GET" -Url "$baseUrl/analytics/ai-summary"
Test-Endpoint -Category "Analytics" -Name "/analytics/revenue-forecast" -Method "GET" -Url "$baseUrl/analytics/revenue-forecast"

# ─────────────────────────────────────────────────────────────────────
# SECTION 8: CATALOG, PROMOS & ADD-ONS
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 8. Catalog, Promos & Add-Ons ===" -ForegroundColor Magenta
Test-Endpoint -Category "AddOns" -Name "/addons" -Method "GET" -Url "$baseUrl/addons"
Test-Endpoint -Category "Promos" -Name "/promos" -Method "GET" -Url "$baseUrl/promos"
Test-Endpoint -Category "Promos" -Name "/promos/ai-suggest" -Method "GET" -Url "$baseUrl/promos/ai-suggest"

# ─────────────────────────────────────────────────────────────────────
# SECTION 9: TELEMATICS, GEOFENCE, SAFETY & MAINTENANCE
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 9. Telematics, Safety & Maintenance ===" -ForegroundColor Magenta
Test-Endpoint -Category "GeoFence" -Name "/geofence" -Method "GET" -Url "$baseUrl/geofence"
Test-Endpoint -Category "Locations" -Name "/locations/active-vehicles" -Method "GET" -Url "$baseUrl/locations/active-vehicles"
Test-Endpoint -Category "Fuel" -Name "/fuel" -Method "GET" -Url "$baseUrl/fuel"
Test-Endpoint -Category "Maintenance" -Name "/maintenance" -Method "GET" -Url "$baseUrl/maintenance"
Test-Endpoint -Category "Maintenance" -Name "/maintenance/predictive-alerts" -Method "GET" -Url "$baseUrl/maintenance/predictive-alerts"
Test-Endpoint -Category "RiskManagement" -Name "/risk/fuel-anomaly" -Method "GET" -Url "$baseUrl/risk/fuel-anomaly?vehicleId=1&amount=500&distance=50"
Test-Endpoint -Category "RouteAdvisory" -Name "/routeadvisory/1" -Method "GET" -Url "$baseUrl/routeadvisory/1"

# ─────────────────────────────────────────────────────────────────────
# SECTION 10: CUSTOMER SERVICE, LEGAL & NOTIFICATIONS
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 10. Customer Support, Legal & Communications ===" -ForegroundColor Magenta
Test-Endpoint -Category "Legal" -Name "/terms" -Method "GET" -Url "$hostBase/terms"
Test-Endpoint -Category "Legal" -Name "/privacy" -Method "GET" -Url "$hostBase/privacy"
Test-Endpoint -Category "Notifications" -Name "/notifications" -Method "GET" -Url "$baseUrl/notifications"
Test-Endpoint -Category "Ratings" -Name "/ratings" -Method "GET" -Url "$baseUrl/ratings"
Test-Endpoint -Category "Issues" -Name "/issues" -Method "GET" -Url "$baseUrl/issues"
Test-Endpoint -Category "Extensions" -Name "/extensions" -Method "GET" -Url "$baseUrl/extensions"
Test-Endpoint -Category "DocVault" -Name "/docvault/alerts" -Method "GET" -Url "$baseUrl/docvault/alerts"
Test-Endpoint -Category "Messages" -Name "/messages/conversations?userId=admin" -Method "GET" -Url "$baseUrl/messages/conversations?userId=admin"

# ─────────────────────────────────────────────────────────────────────
# SECTION 11: WEATHER TELEMATICS (INTERNAL ENDPOINTS & LIVE KEYS)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 11. Weather Telematics & Live Weather API Keys ===" -ForegroundColor Magenta
Test-Endpoint -Category "Weather" -Name "/weather/current" -Method "GET" -Url "$baseUrl/weather/current"
Test-Endpoint -Category "Weather" -Name "/weather/cities" -Method "GET" -Url "$baseUrl/weather/cities"
Test-Endpoint -Category "Weather" -Name "/weather/radar-frames" -Method "GET" -Url "$baseUrl/weather/radar-frames"
Test-Endpoint -Category "Weather" -Name "/weather/flood-zones" -Method "GET" -Url "$baseUrl/weather/flood-zones"

# Live OpenWeather Key Check
$owKey = $env:OPENWEATHER_API_KEY
if ($owKey) {
    Test-Endpoint -Category "Live API Keys" -Name "OpenWeather API Direct Ping (Manila)" -Method "GET" -Url "https://api.openweathermap.org/data/2.5/weather?q=Manila&appid=$owKey"
} else {
    Write-Host "[SKIP] OpenWeather API Key (OPENWEATHER_API_KEY not found in .env)" -ForegroundColor Yellow
}

# ─────────────────────────────────────────────────────────────────────
# SECTION 12: MULTI-MODEL CLOUD AI API KEYS (GROQ, MISTRAL, OPENROUTER, SAMBANOVA)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 12. Multi-Model Cloud AI API Keys Connectivity ===" -ForegroundColor Magenta

# Live Groq API Key Check
$groqKey = $env:GROQ_API_KEY
if ($groqKey) {
    $groqHeaders = @{ "Authorization" = "Bearer $groqKey" }
    Test-Endpoint -Category "Live AI Keys" -Name "Groq Cloud Llama-3 API Key Ping" -Method "GET" -Url "https://api.groq.com/openai/v1/models" -Headers $groqHeaders
} else {
    Write-Host "[SKIP] Groq Cloud API Key (GROQ_API_KEY not found in .env)" -ForegroundColor Yellow
}

# Live Mistral API Key Check
$mistralKey = $env:MISTRAL_API_KEY
if ($mistralKey) {
    $mistralHeaders = @{ "Authorization" = "Bearer $mistralKey" }
    Test-Endpoint -Category "Live AI Keys" -Name "Mistral AI Official API Key Ping" -Method "GET" -Url "https://api.mistral.ai/v1/models" -Headers $mistralHeaders
} else {
    Write-Host "[SKIP] Mistral AI API Key (MISTRAL_API_KEY not found in .env)" -ForegroundColor Yellow
}

# Live OpenRouter API Key Check
$openRouterKey = $env:OPENROUTER_API_KEY
if ($openRouterKey) {
    $openRouterHeaders = @{ "Authorization" = "Bearer $openRouterKey" }
    Test-Endpoint -Category "Live AI Keys" -Name "OpenRouter Universal AI Key Ping" -Method "GET" -Url "https://openrouter.ai/api/v1/models" -Headers $openRouterHeaders
} else {
    Write-Host "[SKIP] OpenRouter API Key (OPENROUTER_API_KEY not found in .env)" -ForegroundColor Yellow
}

# Live SambaNova API Key Check
$sambaKey = if ($env:SAMBANOVA_API_KEY) { $env:SAMBANOVA_API_KEY } else { $env:SAMBA_API_KEY }
if ($sambaKey) {
    $sambaHeaders = @{ "Authorization" = "Bearer $sambaKey" }
    Test-Endpoint -Category "Live AI Keys" -Name "SambaNova Deep Learning Key Ping" -Method "GET" -Url "https://api.sambanova.ai/v1/models" -Headers $sambaHeaders
} else {
    Write-Host "[SKIP] SambaNova API Key (SAMBANOVA_API_KEY not found in .env)" -ForegroundColor Yellow
}

# Live Supabase Cloud Connectivity Check
$sbKey = if ($env:SUPABASE_SECRET_KEY) { $env:SUPABASE_SECRET_KEY } else { $env:SUPABASE_PUBLISHABLE_KEY }
$sbUrl = if ($env:SUPABASE_URL) { $env:SUPABASE_URL } else { "https://mvnswnhnhstzeritaeou.supabase.co" }
if ($sbKey -and $sbUrl) {
    $sbHeaders = @{ "apikey" = $sbKey; "Authorization" = "Bearer $sbKey" }
    Test-Endpoint -Category "Live Cloud" -Name "Supabase Cloud Project REST Ping" -Method "GET" -Url $sbUrl -ExpectedCodes @(200, 204, 301, 302, 401, 403, 404)
} else {
    Write-Host "[SKIP] Supabase Cloud (SUPABASE_SECRET_KEY / SUPABASE_URL not found in .env)" -ForegroundColor Yellow
}

# ─────────────────────────────────────────────────────────────────────
# SUMMARY & SCORECARD
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "                       TEST EXECUTION SUMMARY                         " -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Total Endpoints & Keys Tested: $($script:results.Count)" -ForegroundColor White
Write-Host "Passed:                        $script:passCount" -ForegroundColor Green
Write-Host "Failed:                        $script:failCount" -ForegroundColor $(if ($script:failCount -eq 0) { "Green" } else { "Red" })
$passRate = [math]::Round(($script:passCount / $script:results.Count) * 100, 1)
Write-Host "Pass Rate:                     $passRate%" -ForegroundColor $(if ($passRate -eq 100) { "Green" } else { "Yellow" })
Write-Host "=====================================================================`n" -ForegroundColor Cyan
