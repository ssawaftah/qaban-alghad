@echo off
cd /d "%~dp0"
if not exist QabanAlGhad.exe (
 echo Extract the complete package before running.
 pause
 exit /b 1
)
start "" QabanAlGhad.exe
