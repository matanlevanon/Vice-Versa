<#
.SYNOPSIS
    Authenticode-signs every .exe under a folder with a PFX certificate.

.DESCRIPTION
    Used by the GitHub Actions workflow and usable locally. The certificate is
    read from the PFX_BASE64 environment variable, written to a temporary file,
    imported into the current user's certificate store, and signed by thumbprint
    so the password never lands on a command line. Both the temporary file and
    the imported key are removed afterwards.

    Environment variables:
      PFX_BASE64     base64 of the .pfx file (required)
      PFX_PASSWORD   password for the .pfx (optional)
      TIMESTAMP_URL  RFC 3161 timestamp server (optional)

.EXAMPLE
    $env:PFX_BASE64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx'))
    $env:PFX_PASSWORD = 'secret'
    ./tools/Sign-Files.ps1 -Path publish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [string]$Filter = '*.exe'
)

$ErrorActionPreference = 'Stop'

if (-not $env:PFX_BASE64) {
    throw 'PFX_BASE64 is not set. Nothing to sign with.'
}

$timestampUrl = if ($env:TIMESTAMP_URL) { $env:TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }

# Sort by real version, not by string. Ordinal sort puts 10.0.9.0 above
# 10.0.26100.0 and would silently pick a stale signtool.
$signtool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' -ErrorAction SilentlyContinue |
            Sort-Object { try { [version](Split-Path (Split-Path $_.FullName -Parent) -Leaf) } catch { [version]'0.0' } } -Descending |
            Select-Object -First 1

if (-not $signtool) {
    throw 'signtool.exe was not found. Install the Windows SDK signing tools.'
}

$temp = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$pfx = Join-Path $temp ('vv-signing-' + [Guid]::NewGuid().ToString('N') + '.pfx')
$imported = $null

try {
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:PFX_BASE64))

    $targets = @(Get-ChildItem -Path $Path -Recurse -Filter $Filter -File)

    if ($targets.Count -eq 0) {
        Write-Warning "No files matching $Filter under $Path"
        return
    }

    $importArguments = @{
        FilePath          = $pfx
        CertStoreLocation = 'Cert:\CurrentUser\My'
    }

    if ($env:PFX_PASSWORD) {
        $importArguments['Password'] = ConvertTo-SecureString $env:PFX_PASSWORD -AsPlainText -Force
    }

    $imported = Import-PfxCertificate @importArguments
    Write-Host "Imported certificate $($imported.Thumbprint)  $($imported.Subject)"

    foreach ($target in $targets) {
        Write-Host "Signing $($target.FullName)"

        & $signtool.FullName sign /sha1 $imported.Thumbprint /fd SHA256 `
            /tr $timestampUrl /td SHA256 /v $target.FullName

        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE for $($target.Name)"
        }
    }

    Write-Host "Signed $($targets.Count) file(s)."
}
finally {
    if (Test-Path $pfx) {
        Remove-Item $pfx -Force
    }

    if ($imported) {
        Remove-Item "Cert:\CurrentUser\My\$($imported.Thumbprint)" -Force -ErrorAction SilentlyContinue
    }
}
