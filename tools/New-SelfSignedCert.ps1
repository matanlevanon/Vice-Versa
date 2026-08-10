<#
.SYNOPSIS
    Creates a self-signed code signing certificate for local testing.

.DESCRIPTION
    A self-signed signature is a valid signature, but Windows does not trust the
    root, so SmartScreen and Defender still warn unless the certificate is added
    to the machine's Trusted Root store. That is fine on your own machines and
    useless for anyone else's. Run this only for local testing, or as a stopgap
    until you buy a real certificate. See docs/SIGNING.md.

    Run this from an elevated PowerShell prompt if you want the -Trust switch to
    work, because writing to the machine root store needs administrator rights.

.PARAMETER Subject
    Certificate subject name. Use your own name or company name.

.PARAMETER Password
    Password to protect the exported .pfx.

.PARAMETER Trust
    Also install the certificate into the local machine's Trusted Root and
    Trusted Publisher stores so this PC stops warning about the signed builds.

.EXAMPLE
    ./tools/New-SelfSignedCert.ps1 -Subject 'Matan Levanon' -Password 'choose-one' -Trust
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Subject,
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$OutFile = 'vice-versa-dev.pfx',
    [int]$YearsValid = 3,
    [switch]$Trust
)

$ErrorActionPreference = 'Stop'

Write-Host "Creating a self-signed code signing certificate for '$Subject'"

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=$Subject" `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears($YearsValid)

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $OutFile -Password $securePassword | Out-Null

Write-Host "Wrote $OutFile"
Write-Host "Thumbprint: $($cert.Thumbprint)"

if ($Trust) {
    Write-Host 'Installing into the local machine Trusted Root and Trusted Publisher stores'

    $cerFile = [IO.Path]::ChangeExtension($OutFile, '.cer')
    Export-Certificate -Cert $cert -FilePath $cerFile | Out-Null

    Import-Certificate -FilePath $cerFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Import-Certificate -FilePath $cerFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null

    Write-Host "Installed. Also wrote $cerFile, which is what you copy to other machines you control."
}

Write-Host ''
Write-Host 'To use this in GitHub Actions, add these repository secrets:'
Write-Host '  SIGNING_PFX_BASE64   the command below prints the value'
Write-Host '  SIGNING_PFX_PASSWORD the password you just chose'
Write-Host ''
Write-Host "  [Convert]::ToBase64String([IO.File]::ReadAllBytes('$OutFile')) | Set-Clipboard"
Write-Host ''
Write-Host 'Keep the .pfx out of the repository. .gitignore already excludes it.'
