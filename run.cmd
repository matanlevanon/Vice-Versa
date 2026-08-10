@echo off
rem Vice Versa - run it without installing anything.
rem
rem Starts publish\portable\ViceVersa.exe if it is already built. If it is not,
rem builds it first, which needs the .NET 8 SDK.
rem
rem If you only want to USE the app and would rather not install an SDK, grab
rem the prebuilt zip from the Releases page instead and run ViceVersa.exe from
rem inside it. Nothing gets installed either way.

setlocal
cd /d "%~dp0"

set "EXE=publish\portable\ViceVersa.exe"

if exist "%EXE%" goto :launch

echo Portable executable not built yet. Building it now.
echo.

where pwsh >nul 2>nul
if %errorlevel%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "tools\Build-Portable.ps1" -SkipTests
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "tools\Build-Portable.ps1" -SkipTests
)

if not exist "%EXE%" (
    echo.
    echo Build did not produce %EXE%.
    pause
    exit /b 1
)

:launch
echo Starting Vice Versa.
echo.
echo There is no window. Look for the tray icon next to the clock,
echo then select text anywhere in Windows and press Shift+F12.
start "" "%EXE%"
