@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -File scripts\build.ps1
if errorlevel 1 pause
