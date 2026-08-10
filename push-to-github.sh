#!/usr/bin/env bash
# Puts this folder on GitHub in one command. Bash version of push-to-github.ps1.
#
#   ./push-to-github.sh <github-username> [repo-name]
#
# Uses the GitHub CLI to create the repository when gh is installed and signed
# in. Otherwise it pushes to a repository you created yourself.

set -euo pipefail

OWNER="${1:-}"
REPO="${2:-vice-versa}"

if [ -z "$OWNER" ]; then
    echo "usage: $0 <github-username> [repo-name]" >&2
    exit 1
fi

cd "$(dirname "$0")"

step() { printf '\n==> %s\n' "$1"; }

command -v git >/dev/null || { echo "git is not installed" >&2; exit 1; }

step "Replacing the OWNER placeholder with '$OWNER'"
for file in README.md docs/SIGNING.md installer/ViceVersa.iss src/ViceVersa/ViceVersa.csproj; do
    [ -f "$file" ] || continue

    tmp="$(mktemp)"
    sed "s|github\.com/OWNER/vice-versa|github.com/$OWNER/$REPO|g" "$file" > "$tmp"

    if cmp -s "$tmp" "$file"; then
        rm -f "$tmp"
    else
        cat "$tmp" > "$file"
        rm -f "$tmp"
        echo "  updated $file"
    fi
done

if [ ! -d .git ]; then
    step "Initialising the local repository"
    git init -b main >/dev/null
else
    step "Local repository already exists"
    git checkout -B main >/dev/null
fi

step "Staging and committing"
git add -A
if git diff --cached --quiet; then
    echo "  nothing new to commit"
else
    git commit -m "Vice Versa: Hebrew and English keyboard layout converter for Windows" >/dev/null
    echo "  committed"
fi

if command -v gh >/dev/null; then
    if ! gh auth status >/dev/null 2>&1; then
        step "GitHub CLI is installed but not signed in"
        gh auth login
    fi

    if gh auth status >/dev/null 2>&1 && ! gh repo view "$OWNER/$REPO" >/dev/null 2>&1; then
        step "Creating the repository with the GitHub CLI"
        gh repo create "$OWNER/$REPO" --public --source=. --remote=origin --push \
            --description 'Convert text between Hebrew and English by keyboard position, anywhere in Windows'
        echo
        echo "Done. https://github.com/$OWNER/$REPO"
        exit 0
    fi
fi

step "Pointing origin at https://github.com/$OWNER/$REPO.git"
git remote remove origin 2>/dev/null || true
git remote add origin "https://github.com/$OWNER/$REPO.git"

step "Pushing"
echo "  Use a personal access token as the password, not your account password."
echo
git push -u origin main

echo
echo "Done. https://github.com/$OWNER/$REPO"
echo
echo "Next:"
echo "  1. Open the Actions tab. The first build runs automatically."
echo "  2. Read docs/SIGNING.md and decide on a certificate."
echo "  3. Cut a release with:  git tag v1.0.0 && git push origin v1.0.0"
