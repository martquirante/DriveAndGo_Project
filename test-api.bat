@echo off
setlocal
echo ===================================================
echo  DriveAndGo Master API & Cloud Verification Suite
echo  (Safe & Non-Destructive: Does NOT erase any data)
echo ===================================================

netstat -ano | findstr /R /C:":5233 .*LISTENING" >nul
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [PAALALA] Hindi pa tumatakbo ang DriveAndGo_API sa port 5233!
    echo Upang ma-test ang local endpoints:
    echo  1. Buksan at i-run ang DriveAndGo_API sa Visual Studio (F5 / Play)
    echo     o patakbuhin: dotnet run --project DriveAndGo_API
    echo  2. Kapag bukas na ang server, patakbuhin muli ang test-api o npm test.
    echo.
)

powershell -ExecutionPolicy Bypass -File "%~dp0scripts\test_all_api_endpoints.ps1"
