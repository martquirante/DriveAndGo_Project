@echo off
title DriveAndGo Cloudflare Tunnel (Port 5233)
echo ======================================================================
echo   DriveAndGo API - Cloudflare Public Tunnel Launcher
echo   Port: 5233 (http://localhost:5233)
echo ======================================================================
echo.
echo Connecting to Cloudflare network...
echo Kopyahin ang "https://xxxx.trycloudflare.com" na lalabas sa ibaba para sa mobile testing!
echo.
cloudflared tunnel --url http://localhost:5233
pause
