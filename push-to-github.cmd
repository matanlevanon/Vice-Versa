@echo off
rem Vice Versa - put this folder on GitHub.
rem
rem Double-click this file. It initialises the git repository, makes the first
rem commit, creates the GitHub repository if the GitHub CLI is signed in, and
rem pushes. Pushing starts the build workflow, which produces the installer and
rem the portable executable as release artifacts.
rem
rem   push-to-github.cmd                     detect your username automatically
rem   push-to-github.cmd -Owner yourname     say it explicitly

setlocal
cd /d "%~dp0"

where pwsh >nul 2>nul
if %errorlevel%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "push-to-github.ps1" %*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "push-to-github.ps1" %*
)

echo.
pause
