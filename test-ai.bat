@echo off
echo ===================================================
echo Running DriveAndGo AI Models Verification Suite...
echo (Tests live inferences on Mistral, Gemini, Groq, Cohere, OpenRouter)
echo ===================================================
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\test_all_ai_providers.ps1"
