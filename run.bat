@echo off
cd /d "%~dp0"

rem Free port 5107 first: a leftover instance holds both the port and the build output,
rem which otherwise fails the build with ten copy retries.
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":5107" ^| findstr LISTENING') do taskkill /F /PID %%p >nul 2>&1

rem Open the browser once the server is actually listening, rather than on a fixed delay.
start "" powershell -NoProfile -WindowStyle Hidden -Command "1..120 | ForEach-Object { if (Get-NetTCPConnection -LocalPort 5107 -State Listen -ErrorAction SilentlyContinue) { Start-Process 'http://localhost:5107'; exit }; Start-Sleep -Milliseconds 500 }"

dotnet run --project SqlMarkdownRunner.csproj
pause
