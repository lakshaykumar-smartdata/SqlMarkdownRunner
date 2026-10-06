@echo off
cd /d "%~dp0"

rem Stop a previous run of this app by name. Deliberately not a netstat/taskkill-by-PID
rem loop: endpoint security flags that pattern, and it can hit unrelated processes.
taskkill /F /IM SqlMarkdownRunner.exe >nul 2>&1

rem dotnet run opens the browser itself once the server is listening
rem (launchBrowser in Properties/launchSettings.json).
dotnet run --project SqlMarkdownRunner.csproj

pause
