@echo off
cd /d "%~dp0"
rem An instance left running locks the exe and the build fails with a wall of retries.
taskkill /F /IM SqlMarkdownRunner.exe >nul 2>&1
dotnet run --project SqlMarkdownRunner.csproj
pause
