@echo off
cd /d "%~dp0"

rem Stop a previous run of this app by name. Deliberately not a netstat/taskkill-by-PID
rem loop: endpoint security flags that pattern, and it can hit unrelated processes.
taskkill /F /IM SqlMarkdownRunner.exe >nul 2>&1

rem The server gets its own window so this script stays free to wait for it.
rem dotnet run ignores launchBrowser - that is a Visual Studio setting - so we open it here.
start "SqlMarkdownRunner" cmd /k dotnet run --project SqlMarkdownRunner.csproj

echo Waiting for http://localhost:5107 ...
curl -s -o nul --max-time 5 --retry 60 --retry-delay 1 --retry-connrefused http://localhost:5107

if errorlevel 1 (
    echo Server did not answer - check the SqlMarkdownRunner window.
    pause
    exit /b 1
)

start "" http://localhost:5107
