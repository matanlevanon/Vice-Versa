@echo off
rem Vice Versa - build the portable executable on this machine.
rem
rem Double-click this file, or run it from a terminal. It needs the .NET 8 SDK
rem and nothing else. The result is publish\portable\ViceVersa.exe, a single
rem file that runs with no install.
rem
rem   build.cmd            build
rem   build.cmd -Zip       build and pack dist\ViceVersa-1.0.0-portable-x64.zip
rem   build.cmd -Run       build and start the app

setlocal
cd /d "%~dp0"

where pwsh >nul 2>nul
if %errorlevel%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "tools\Build-Portable.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "tools\Build-Portable.ps1" %*
)

set BUILD_EXIT=%errorlevel%

echo.
if not %BUILD_EXIT%==0 (
    echo Build failed with exit code %BUILD_EXIT%.
)
pause
exit /b %BUILD_EXIT%
