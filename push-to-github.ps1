<#
.SYNOPSIS
    Puts this folder on GitHub in one command.

.DESCRIPTION
    Creates the repository if the GitHub CLI is installed and signed in.
    Otherwise it prepares everything and pushes to a repository you created
    yourself. Also rewrites the OWNER placeholder in the docs to your username.

.PARAMETER Owner
    Your GitHub username or organisation. Optional when the GitHub CLI is
    installed and signed in: the username is read from it.

.PARAMETER Repo
    Repository name. Defaults to vice-versa.

.PARAMETER Private
    Create the repository private. Public is the default, since the free
    signing routes for open source need a public repository.

.EXAMPLE
    ./push-to-github.ps1

.EXAMPLE
    ./push-to-github.ps1 -Owner matanlevanon -Repo vice-versa -Private
#>
[CmdletBinding()]
param(
    [string]$Owner,
    [string]$Repo = 'vice-versa',
    [switch]$Private
)

$ErrorActionPreference = 'Stop'

# Native command exit codes are checked by hand below, so stop PowerShell 7 from
# turning a non-zero git exit code into a terminating error.
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

Set-Location $PSScriptRoot

function Write-Step($message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'git is not installed. Get it from https://git-scm.com/download/win'
}

# ------------------------------------------------------------------- who am I

if ([string]::IsNullOrWhiteSpace($Owner)) {
    Write-Step 'Working out your GitHub username'

    $ghCli = Get-Command gh -ErrorAction SilentlyContinue

    if ($ghCli) {
        $detected = & gh api user --jq .login
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($detected)) {
            $Owner = $detected.Trim()
            Write-Host "  signed in as $Owner"
        }
    }

    if ([string]::IsNullOrWhiteSpace($Owner)) {
        Write-Host '  could not detect it automatically'
        $Owner = (Read-Host 'GitHub username').Trim()
    }

    if ([string]::IsNullOrWhiteSpace($Owner)) {
        throw 'No GitHub username. Run again with -Owner <username>.'
    }
}

# ---------------------------------------------------------------- placeholders

Write-Step "Replacing the OWNER placeholder with '$Owner'"

$targets = @(
    'README.md',
    'docs/SIGNING.md',
    'installer/ViceVersa.iss',
    'src/ViceVersa/ViceVersa.csproj'
)

foreach ($file in $targets) {
    if (Test-Path $file) {
        $full = (Resolve-Path $file).Path
        $content = [IO.File]::ReadAllText($full)
        $updated = $content -replace 'github\.com/OWNER/vice-versa', "github.com/$Owner/$Repo"

        if ($updated -ne $content) {
            # Written without a BOM on purpose. Windows PowerShell 5.1 would add
            # one with -Encoding UTF8, pwsh would not, and the two would disagree.
            [IO.File]::WriteAllText($full, $updated, [Text.UTF8Encoding]::new($false))
            Write-Host "  updated $file"
        }
    }
}

# ------------------------------------------------------------------ local repo

if (-not (Test-Path .git)) {
    Write-Step 'Initialising the local repository'
    git init -b main | Out-Null
} else {
    Write-Step 'Local repository already exists'

    # Never force-move an existing main. Switch to it if it exists, create it otherwise.
    git rev-parse --verify main | Out-Null
    if ($LASTEXITCODE -eq 0) {
        git checkout main | Out-Null
    } else {
        git checkout -b main | Out-Null
    }
}

Write-Step 'Staging and committing'
git add -A

git diff --cached --quiet
$hasStagedChanges = ($LASTEXITCODE -ne 0)

if ($hasStagedChanges) {
    git commit -m "Vice Versa: Hebrew and English keyboard layout converter for Windows" | Out-Null
    Write-Host '  committed'
} else {
    Write-Host '  nothing new to commit'
}

# ------------------------------------------------------------------- the remote

$remoteUrl = "https://github.com/$Owner/$Repo.git"
$gh = Get-Command gh -ErrorAction SilentlyContinue

if ($gh) {
    Write-Step 'GitHub CLI found, creating the repository'

    # No 2>&1 here. On PowerShell 6.0 to 7.1 that turns a native command's stderr
    # into terminating errors, and stderr is the expected output of these calls.
    gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host '  not signed in, launching gh auth login'
        gh auth login
    }

    $visibility = if ($Private) { '--private' } else { '--public' }

    gh repo view "$Owner/$Repo" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        gh repo create "$Owner/$Repo" $visibility --source=. --remote=origin --push `
            --description 'Convert text between Hebrew and English by keyboard position, anywhere in Windows'
        Write-Host ''
        Write-Host "Done. https://github.com/$Owner/$Repo" -ForegroundColor Green
        exit 0
    }

    Write-Host '  repository already exists, pushing to it'
}

Write-Step "Pointing origin at $remoteUrl"

git remote remove origin | Out-Null
git remote add origin $remoteUrl

Write-Step 'Pushing'
Write-Host '  If this is your first push, GitHub will ask you to sign in.'
Write-Host '  Use a personal access token as the password, not your account password.'
Write-Host ''

git push -u origin main

Write-Host ''
Write-Host "Done. https://github.com/$Owner/$Repo" -ForegroundColor Green
Write-Host ''
Write-Host 'Next:'
Write-Host '  1. Open the Actions tab. The first build runs automatically.'
Write-Host '  2. Read docs/SIGNING.md and decide on a certificate.'
Write-Host '  3. Cut a release with:  git tag v1.0.0 ; git push origin v1.0.0'
