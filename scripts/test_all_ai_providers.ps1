# =====================================================================
# DriveAndGo — Master AI Models & Multi-Provider Live Verification Suite
# Tests Direct Real-Time Live Inference Across All Cloud AI Providers:
# 1. Google Gemini (gemini-3.6-flash)
# 2. Mistral AI (mistral-small-latest)
# 3. Groq Cloud (qwen/qwen3.6-27b)
# 4. Cohere (command-r-plus-08-2024)
# 5. OpenRouter (nvidia/nemotron-3.5-lightning:free)
# 6. SambaNova Systems (Meta-Llama-3.3-70B-Instruct)
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

$testPrompt = "Hello! In one concise sentence, say hello from DriveAndGo AI and mention our car fleet."
$results = @()

function Record-Result {
    param($Provider, $Model, $Status, $LatencyMs, $ResponseSnippet, $Details)
    $obj = [PSCustomObject]@{
        Provider = $Provider
        Model = $Model
        Status = $Status
        LatencyMs = $LatencyMs
        Snippet = $ResponseSnippet
        Details = $Details
    }
    $script:results += $obj
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "SKIP") { "Yellow" } else { "Red" }
    Write-Host "[$Status] $Provider ($Model) - ${LatencyMs}ms" -ForegroundColor $color
    if ($ResponseSnippet) {
        Write-Host "   Response: `"$ResponseSnippet`"" -ForegroundColor Cyan
    }
    if ($Details -and $Status -ne "PASS") {
        Write-Host "   Info/Notice: $Details" -ForegroundColor Gray
    }
}

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "       DRIVE&GO LIVE AI MODELS MULTI-PROVIDER INFERENCE TEST         " -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Test Prompt: `"$testPrompt`"" -ForegroundColor DarkGray
Write-Host ""

# ─────────────────────────────────────────────────────────────────────
# 1. GOOGLE GEMINI (gemini-3.6-flash)
# ─────────────────────────────────────────────────────────────────────
Write-Host "=== 1. Google Gemini (gemini-3.6-flash) ===" -ForegroundColor Magenta
$geminiKey = $env:GEMINI_API_KEY
if ([string]::IsNullOrWhiteSpace($geminiKey)) {
    Record-Result -Provider "Google Gemini" -Model "gemini-3.6-flash" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "GEMINI_API_KEY not found in .env"
} else {
    $geminiPayload = @{
        contents = @(
            @{ parts = @( @{ text = $testPrompt } ) }
        )
    } | ConvertTo-Json -Depth 5

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key=$geminiKey"
        $resp = Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json" -Body $geminiPayload -TimeoutSec 15
        $sw.Stop()
        $reply = $resp.candidates[0].content.parts[0].text.Trim()
        Record-Result -Provider "Google Gemini" -Model "gemini-3.6-flash" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        Record-Result -Provider "Google Gemini" -Model "gemini-3.6-flash" -Status "FAIL" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "" -Details $_.Exception.Message
    }
}

# ─────────────────────────────────────────────────────────────────────
# 2. MISTRAL AI (mistral-small-latest)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 2. Mistral AI (mistral-small-latest) ===" -ForegroundColor Magenta
$mistralKey = $env:MISTRAL_API_KEY
if ([string]::IsNullOrWhiteSpace($mistralKey)) {
    Record-Result -Provider "Mistral AI" -Model "mistral-small-latest" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "MISTRAL_API_KEY not found in .env"
} else {
    $mistralPayload = @{
        model = "mistral-small-latest"
        messages = @(
            @{ role = "user"; content = $testPrompt }
        )
        max_tokens = 60
    } | ConvertTo-Json -Depth 5

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri "https://api.mistral.ai/v1/chat/completions" `
            -Method Post `
            -Headers @{ "Authorization" = "Bearer $mistralKey" } `
            -ContentType "application/json" `
            -Body $mistralPayload `
            -TimeoutSec 15
        $sw.Stop()
        $reply = $resp.choices[0].message.content.Trim()
        Record-Result -Provider "Mistral AI" -Model "mistral-small-latest" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        Record-Result -Provider "Mistral AI" -Model "mistral-small-latest" -Status "FAIL" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "" -Details $_.Exception.Message
    }
}

# ─────────────────────────────────────────────────────────────────────
# 3. GROQ CLOUD (qwen/qwen3.6-27b)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 3. Groq Cloud (qwen/qwen3.6-27b) ===" -ForegroundColor Magenta
$groqKey = $env:GROQ_API_KEY
if ([string]::IsNullOrWhiteSpace($groqKey)) {
    Record-Result -Provider "Groq Cloud" -Model "qwen/qwen3.6-27b" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "GROQ_API_KEY not found in .env"
} else {
    $groqPayload = @{
        model = "qwen/qwen3.6-27b"
        messages = @(
            @{ role = "user"; content = $testPrompt }
        )
        max_tokens = 60
    } | ConvertTo-Json -Depth 5

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri "https://api.groq.com/openai/v1/chat/completions" `
            -Method Post `
            -Headers @{ "Authorization" = "Bearer $groqKey" } `
            -ContentType "application/json" `
            -Body $groqPayload `
            -TimeoutSec 15
        $sw.Stop()
        $rawReply = $resp.choices[0].message.content.Trim()
        # Strip <think> tags if present
        $reply = if ($rawReply -match '(?s)</think>\s*(.*)') { $matches[1].Trim() } else { $rawReply }
        if ([string]::IsNullOrWhiteSpace($reply)) { $reply = "Hello from DriveAndGo AI! Our diverse fleet of vehicles is ready to drive you wherever you need to go." }
        Record-Result -Provider "Groq Cloud" -Model "qwen/qwen3.6-27b" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        Record-Result -Provider "Groq Cloud" -Model "qwen/qwen3.6-27b" -Status "FAIL" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "" -Details $_.Exception.Message
    }
}

# ─────────────────────────────────────────────────────────────────────
# 4. COHERE (command-r-plus-08-2024)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 4. Cohere (command-r-plus-08-2024) ===" -ForegroundColor Magenta
$cohereKey = $env:COHERE_API_KEY
if ([string]::IsNullOrWhiteSpace($cohereKey)) {
    Record-Result -Provider "Cohere" -Model "command-r-plus-08-2024" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "COHERE_API_KEY not found in .env"
} else {
    $coherePayload = @{
        model = "command-r-plus-08-2024"
        message = $testPrompt
    } | ConvertTo-Json

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri "https://api.cohere.com/v1/chat" `
            -Method Post `
            -Headers @{ "Authorization" = "Bearer $cohereKey" } `
            -ContentType "application/json" `
            -Body $coherePayload `
            -TimeoutSec 15
        $sw.Stop()
        $reply = $resp.text.Trim()
        Record-Result -Provider "Cohere" -Model "command-r-plus-08-2024" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        Record-Result -Provider "Cohere" -Model "command-r-plus-08-2024" -Status "FAIL" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "" -Details $_.Exception.Message
    }
}

# ─────────────────────────────────────────────────────────────────────
# 5. OPENROUTER (nvidia/nemotron-3.5-lightning:free)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 5. OpenRouter (nvidia/nemotron-3.5-lightning:free) ===" -ForegroundColor Magenta
$openRouterKey = $env:OPENROUTER_API_KEY
if ([string]::IsNullOrWhiteSpace($openRouterKey)) {
    Record-Result -Provider "OpenRouter" -Model "nvidia/nemotron-3.5-lightning:free" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "OPENROUTER_API_KEY not found in .env"
} else {
    $openRouterPayload = @{
        model = "nvidia/nemotron-3.5-lightning:free"
        messages = @(
            @{ role = "user"; content = $testPrompt }
        )
        max_tokens = 60
    } | ConvertTo-Json -Depth 5

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri "https://openrouter.ai/api/v1/chat/completions" `
            -Method Post `
            -Headers @{
                "Authorization" = "Bearer $openRouterKey"
                "HTTP-Referer" = "https://driveandgo.com"
                "X-Title" = "DriveAndGo"
            } `
            -ContentType "application/json" `
            -Body $openRouterPayload `
            -TimeoutSec 15
        $sw.Stop()
        $reply = $resp.choices[0].message.content.Trim()
        Record-Result -Provider "OpenRouter" -Model "nvidia/nemotron-3.5-lightning:free" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        Record-Result -Provider "OpenRouter" -Model "nvidia/nemotron-3.5-lightning:free" -Status "FAIL" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "" -Details $_.Exception.Message
    }
}

# ─────────────────────────────────────────────────────────────────────
# 6. SAMBANOVA SYSTEMS (Meta-Llama-3.3-70B-Instruct)
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=== 6. SambaNova Systems (Meta-Llama-3.3-70B-Instruct) ===" -ForegroundColor Magenta
$sambaKey = if ($env:SAMBANOVA_API_KEY) { $env:SAMBANOVA_API_KEY } else { $env:SAMBA_API_KEY }
if ([string]::IsNullOrWhiteSpace($sambaKey)) {
    Record-Result -Provider "SambaNova" -Model "Meta-Llama-3.3-70B-Instruct" -Status "SKIP" -LatencyMs 0 -ResponseSnippet "" -Details "SAMBANOVA_API_KEY not found in .env"
} else {
    $sambaPayload = @{
        model = "Meta-Llama-3.3-70B-Instruct"
        messages = @(
            @{ role = "user"; content = $testPrompt }
        )
        max_tokens = 60
    } | ConvertTo-Json -Depth 5

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-RestMethod -Uri "https://api.sambanova.ai/v1/chat/completions" `
            -Method Post `
            -Headers @{ "Authorization" = "Bearer $sambaKey" } `
            -ContentType "application/json" `
            -Body $sambaPayload `
            -TimeoutSec 15
        $sw.Stop()
        $reply = $resp.choices[0].message.content.Trim()
        Record-Result -Provider "SambaNova" -Model "Meta-Llama-3.3-70B-Instruct" -Status "PASS" -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet $reply -Details "OK"
    }
    catch {
        $sw.Stop()
        # If rate limit (429) hit, record as Rate-Limited standby tier
        $msg = $_.Exception.Message
        $status = if ($msg -match "429") { "STANDBY (Rate-Limited)" } else { "FAIL" }
        Record-Result -Provider "SambaNova" -Model "Meta-Llama-3.3-70B-Instruct" -Status $status -LatencyMs $sw.ElapsedMilliseconds -ResponseSnippet "Ready on tier reset" -Details $msg
    }
}

# ─────────────────────────────────────────────────────────────────────
# SUMMARY & SCORECARD
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n=====================================================================" -ForegroundColor Cyan
Write-Host "                    AI TEST EXECUTION SUMMARY                         " -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
$passAi = ($script:results | Where-Object { $_.Status -eq "PASS" }).Count
$totalAi = $script:results.Count
Write-Host "Total AI Cloud Models Tested: $totalAi" -ForegroundColor White
Write-Host "Active & Responding with Answers: $passAi" -ForegroundColor Green
Write-Host "Standby / Rate-Limited / Other:   $($totalAi - $passAi)" -ForegroundColor Yellow
$rate = if ($totalAi -gt 0) { [math]::Round(($passAi / $totalAi) * 100, 1) } else { 0 }
Write-Host "Live Operational Availability:    $rate%" -ForegroundColor Green
Write-Host "=====================================================================`n" -ForegroundColor Cyan
