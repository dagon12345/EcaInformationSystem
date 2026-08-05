@echo off
REM ECA-InFORMS biometric local sync - Windows Service installer.
REM
REM Usage: copy this whole folder's contents (dotnet publish output plus
REM these scripts) to the target PC, then double-click this file. It will
REM ask for Administrator permission - click Yes on the prompt.
REM
REM Safe to re-run: if the service already exists, it stops/removes the old
REM one first, so this also works as the "update" procedure - just republish
REM over this folder (keeping the scripts) and re-run this .bat.
REM
REM All the real work happens in install-service.ps1 - this file only
REM handles requesting Administrator permission, since PowerShell is far
REM more reliable than raw batch for everything else here.
REM
REM The elevated relaunch goes through "cmd /k" (not straight to
REM powershell.exe) specifically so the window CANNOT close itself no
REM matter what happens inside - even a crash before any output is
REM printed still leaves an open, readable cmd window afterward.

net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo Requesting Administrator permission - a new window will open...
    echo If a User Account Control prompt appears, click Yes.
    powershell -NoProfile -Command "Start-Process -FilePath cmd.exe -ArgumentList '/k','%~f0' -Verb RunAs"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-service.ps1"
echo.
echo (Script finished - this window will stay open. Close it manually when done.)
