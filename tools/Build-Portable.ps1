<#
.SYNOPSIS
    Builds Vice Versa on this machine, without CI.

.DESCRIPTION
    Produces the two shipping forms of the app:

      publish\portable\ViceVersa.exe       one file, no install, carries its own
                                           .NET runtime, settings live beside it
      publish\installer\ViceVersa-setup.exe the normal Windows installer, built
                                           only when Inno Setup 6 is present

    Only the .NET 8 SDK is required. Everything else is optional and is skipped
    with a message rather than failing the build.

.PARAMETER Version
    Version stamped into the executable. Defaults to 1.0.0.

.PARAMETER SkipTests
    Skip the unit tests and the key map verification.

.PARAMETER Zip
    Also produce dist\ViceVersa-<version>-portable-x64.zip, the same layout the
    release workflow publishes.

.PARAMETER Run
    Launch the portable executable when the build succeeds.

.EXAMPLE
    ./tools/Build-Portable.ps1

.EXAMPLE
    ./tools/Build-Portable.ps1 -Version 1.2.0 -Zip
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [switch]$SkipTests,
    [switch]$Zip,
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

# Repository root is one level above this script.
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Write-Step($message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Assert-LastExit($what) {
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be MAJOR.MINOR.PATCH, got '$Version'"
}

# ------------------------------------------------------------------ toolchain

Write-Step 'Checking the toolchain'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host ''
    Write-Host 'The .NET 8 SDK is not installed.' -ForegroundColor Yellow
    Write-Host 'Get it from https://dotnet.microsoft.com/download/dotnet/8.0 (SDK, x64),'
    Write-Host 'then open a new terminal and run this script again.'
    Write-Host ''
    Write-Host 'You do not need it to USE the app. Every release on GitHub'
    Write-Host 'already carries a prebuilt ViceVersa.exe.'
    throw 'dotnet was not found on PATH'
}

$sdks = @(& dotnet --list-sdks)
Write-Host "  dotnet $(& dotnet --version)"

if (-not ($sdks | Where-Object { $_ -match '^8\.' -or $_ -match '^9\.' -or $_ -match '^\d\d\.' })) {
    Write-Host 'Warning: no .NET 8 or newer SDK found. Expect the build to fail.' -ForegroundColor Yellow
}

# ---------------------------------------------------------------- verification

if (-not $SkipTests) {
    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { $python = Get-Command python3 -ErrorAction SilentlyContinue }

    if ($python) {
        Write-Step 'Verifying the key map across the C#, AutoHotkey and Python copies'
        & $python.Source tools/keymap_reference.py --check | Select-Object -Last 1
        Assert-LastExit 'keymap_reference.py --check'
    } else {
        Write-Host '  python not found, skipping the key map cross-check'
    }

    Write-Step 'Running the unit tests'
    & dotnet test tests/ViceVersa.Tests/ViceVersa.Tests.csproj -c Release --nologo --verbosity quiet
    Assert-LastExit 'dotnet test'
}

# --------------------------------------------------------------- portable exe

Write-Step "Publishing the portable executable ($Version)"

Remove-Item publish/portable -Recurse -Force -ErrorAction SilentlyContinue

& dotnet publish src/ViceVersa/ViceVersa.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -p:InformationalVersion=$Version `
    --nologo `
    -o publish/portable
Assert-LastExit 'dotnet publish'

$exe = Join-Path $root 'publish/portable/ViceVersa.exe'
if (-not (Test-Path $exe)) { throw 'The publish step produced no ViceVersa.exe' }

# The marker file is what puts the app into portable mode: settings are written
# next to the executable instead of into AppData. Delete it to switch back.
'Vice Versa portable mode. Delete this file to store settings in AppData instead.' |
    Out-File (Join-Path $root 'publish/portable/portable.txt') -Encoding utf8

Copy-Item README.md      publish/portable/README.md    -Force
Copy-Item LICENSE        publish/portable/LICENSE.txt  -Force
Copy-Item ahk/ViceVersa.ahk publish/portable/          -Force

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "  publish\portable\ViceVersa.exe  ($sizeMb MB, self-contained)" -ForegroundColor Green

# ------------------------------------------------------------------ installer

Write-Step 'Looking for Inno Setup, for the installer'

$iscc = Get-ChildItem 'C:\Program Files (x86)\Inno Setup *\ISCC.exe', 'C:\Program Files\Inno Setup *\ISCC.exe' -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ($iscc) {
    New-Item -ItemType Directory -Force -Path publish/installer | Out-Null
    $outDir = (Resolve-Path publish/installer).Path

    & $iscc "/DMyAppVersion=$Version" "/DSourceExe=$exe" "/O$outDir" 'installer/ViceVersa.iss' | Out-Null
    Assert-LastExit 'Inno Setup'

    $setup = Get-ChildItem $outDir -Filter *.exe | Select-Object -First 1
    if ($setup) {
        Write-Host "  publish\installer\$($setup.Name)" -ForegroundColor Green
    }
} else {
    Write-Host '  Inno Setup 6 is not installed, so no installer was built.'
    Write-Host '  The portable executable above is complete and needs no installer.'
    Write-Host '  Installer optional, from https://jrsoftware.org/isdl.php'
}

# ------------------------------------------------------------------- zip

if ($Zip) {
    Write-Step 'Packing the portable zip'
    New-Item -ItemType Directory -Force -Path dist | Out-Null

    $zipPath = Join-Path $root "dist/ViceVersa-$Version-portable-x64.zip"
    Compress-Archive -Path publish/portable/* -DestinationPath $zipPath -Force

    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash
    Write-Host "  dist\ViceVersa-$Version-portable-x64.zip" -ForegroundColor Green
    Write-Host "  SHA256 $hash"
}

# ------------------------------------------------------------------- summary

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host ''
Write-Host 'Run it without installing anything:'
Write-Host "  $exe"
Write-Host ''
Write-Host 'It has no window. Look for the tray icon next to the clock,'
Write-Host 'then select some text anywhere and press Shift+F12.'

if ($Run) {
    Write-Step 'Starting Vice Versa'
    Start-Process $exe
}
