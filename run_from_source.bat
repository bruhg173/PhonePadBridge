@echo off
cd /d "%~dp0"
dotnet restore
dotnet run
pause
