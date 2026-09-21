@echo off
cd /d "%~dp0"
dotnet run --project SqlMarkdownRunner.csproj
pause
