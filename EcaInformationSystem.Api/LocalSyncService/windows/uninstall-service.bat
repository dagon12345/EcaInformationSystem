@echo off
REM Removes the EcaLocalSync Windows Service. Doesn't touch any files -
REM safe to run before deleting the install folder, or before copying a
REM fresh republish over it (install-service.bat also does this
REM automatically, so you don't normally need to run this separately).
REM
REM The elevated relaunch goes through "cmd /k" so the window cannot
REM close itself no matter what happens inside.

net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo Requesting Administrator permission - a new window will open...
    echo If a User Account Control prompt appears, click Yes.
    powershell -NoProfile -Command "Start-Process -FilePath cmd.exe -ArgumentList '/k','%~f0' -Verb RunAs"
    exit /b
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall-service.ps1"
echo.
echo (Script finished - this window will stay open. Close it manually when done.)
