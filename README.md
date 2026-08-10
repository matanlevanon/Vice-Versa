<div align="center">

# Vice Versa

**Fix text typed in the wrong keyboard language. One hotkey, anywhere in Windows.**

[![Build](https://github.com/matanlevanon/Vice-Versa/actions/workflows/build.yml/badge.svg)](https://github.com/matanlevanon/Vice-Versa/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

</div>

You meant to type `שלום` and got `akuo`. Select it, press `Shift+F12`, and it becomes `שלום`.

Vice Versa maps text between the US English and Israeli Hebrew keyboard layouts **by physical key position**. It is not a translator. It answers one question: what would this text have been if the other layout had been active.

---

## What it does

- **One hotkey, everywhere.** Works in any Windows application with a text field. Browsers, Office, Slack, WhatsApp Desktop, Notepad, IDEs.
- **No selection needed.** If nothing is highlighted, it selects the field for you first.
- **Auto direction.** Each word is judged on its own, so a line mixing Hebrew and English converts correctly in one pass.
- **Switches your keyboard language.** After converting, the Windows input language flips to match, so your next keystrokes land in the right language.
- **Portable or installed.** Unzip and run, or install with a Start-with-Windows option.
- **Your clipboard survives.** Whatever you had copied is put back afterwards.
- **Remappable hotkey.** Right-click the tray icon and pick your own combination.

## Get it

Download from the [Releases page](https://github.com/matanlevanon/Vice-Versa/releases/latest).

| Download | Use it when |
|---|---|
| `ViceVersa-x.y.z-portable-x64.zip` | You want zero install. Unzip anywhere, including a USB stick, and run `ViceVersa.exe`. |
| `ViceVersa-x.y.z-setup-x64.exe` | You want it installed, in the Start menu, and optionally starting with Windows. |
| `ViceVersa-AHK.exe` (inside the zip) | You prefer the AutoHotkey build. Same behaviour, much smaller, source in `ahk/ViceVersa.ahk`. |

Nothing else is required. The portable and installed builds carry their own .NET 8 runtime.

After downloading, right-click the file, open Properties, and tick **Unblock**. Windows marks every downloaded executable, signed or not.

## Use it

1. Type something in the wrong language.
2. Select it. Or do not, and let the app select the field for you.
3. Press `Shift+F12`.

Right-click the tray icon for settings, direction, and the hotkey.

### Settings

| Setting | Default | What it does |
|---|---|---|
| Hotkey | `Shift+F12` | The system-wide trigger. |
| Direction | Auto | Auto decides per word. Force one direction if you prefer. |
| Select whole field if nothing selected | On | Sends `Ctrl+A` when the first copy comes back empty. |
| Switch keyboard language after converting | On | Posts a language change request to the target window. |
| Restore clipboard afterwards | On | Puts your previous clipboard text back. |
| Start with Windows | Off | Adds a per-user entry under the `Run` registry key. |

Settings live in `%APPDATA%\ViceVersa\settings.json`, or next to the executable when `portable.txt` sits beside it. The portable zip ships with that marker file. Delete it to switch to AppData.

## The key map

Standard Windows Hebrew layout, Israel Standard SI-1452.

| | | | | | | | | | | | |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `q` | `w` | `e` | `r` | `t` | `y` | `u` | `i` | `o` | `p` | `[` | `]` |
| `/` | `'` | ק | ר | א | ט | ו | ן | ם | פ | `]` | `[` |
| `a` | `s` | `d` | `f` | `g` | `h` | `j` | `k` | `l` | `;` | `'` | |
| ש | ד | ג | כ | ע | י | ח | ל | ך | ף | `,` | |
| `z` | `x` | `c` | `v` | `b` | `n` | `m` | `,` | `.` | `/` | `` ` `` | |
| ז | ס | ב | ה | נ | מ | צ | ת | ץ | `.` | `;` | |

Digits and every unmapped character pass through untouched. Uppercase Latin letters map to the same Hebrew letter, because Hebrew has no case.

The map is defined once in [`tools/keymap_reference.py`](tools/keymap_reference.py), which doubles as the verifier. CI runs it on every build, alongside the C# unit tests, so the three implementations stay in step.

## Known limits

- **Elevated windows.** A non-elevated app cannot send keystrokes to an elevated one. If the target window runs as administrator, Windows blocks the hotkey. Running Vice Versa elevated fixes those windows and breaks the normal ones. Pick whichever set matters more to you.
- **Apps without Ctrl+C.** The app works through the clipboard. A control refusing `Ctrl+C` and `Ctrl+V` will not convert.
- **Ctrl+A scope.** In some editors `Ctrl+A` selects the whole document, not the current field. Turn off the auto-select option if it surprises you.
- **Hebrew layout only.** The map covers Hebrew and English. Other layouts are a pull request away.

## Two ways to run it

The same code ships in two forms, and neither needs .NET installed on the machine that runs it.

| Form | What you get | Where it comes from |
|---|---|---|
| **Portable** | One file, `ViceVersa.exe`. Unzip anywhere, double-click, done. Nothing is registered, nothing is written outside its own folder, no administrator rights. Runs from a USB stick. | `build.cmd`, or the portable zip on any release |
| **Installed** | A normal Windows app: Start menu entry, optional desktop shortcut, optional Start with Windows, clean uninstall from Settings. Per-user install, so no UAC prompt. | `build.cmd` with Inno Setup present, or the setup executable on any release |

Both carry their own .NET 8 runtime, so the machine that runs them needs nothing preinstalled.

## Build it

The SDK is only needed to **build**. To **use** the app, take a release artifact and skip this section.

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows. Double-click `build.cmd`, or:

```powershell
git clone https://github.com/matanlevanon/Vice-Versa.git
cd Vice-Versa

.\build.cmd            # test, then publish publish\portable\ViceVersa.exe
.\build.cmd -Zip       # also pack dist\ViceVersa-1.0.0-portable-x64.zip
.\build.cmd -Run       # build, then start it
.\run.cmd              # build only if needed, then start it
```

`build.cmd` also produces the installer when [Inno Setup 6](https://jrsoftware.org/isinfo.php) is installed, and says so plainly when it is not. The portable executable is complete either way.

The long form, if you would rather drive it by hand:

```powershell
dotnet test tests/ViceVersa.Tests/ViceVersa.Tests.csproj
python tools/keymap_reference.py --check

dotnet publish src/ViceVersa/ViceVersa.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true -o publish/portable

& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' `
  /DMyAppVersion=1.0.0 `
  "/DSourceExe=$((Resolve-Path publish/portable/ViceVersa.exe).Path)" `
  installer/ViceVersa.iss
```

## Push it

This folder is already a working copy of [`matanlevanon/Vice-Versa`](https://github.com/matanlevanon/Vice-Versa), with the first commit staged and made locally. To send it up:

```powershell
git push -u origin main
```

The push starts the workflow, which builds and tests both forms and leaves them on the Actions tab.

`push-to-github.ps1` and `push-to-github.cmd` are still here for setting this up from scratch somewhere else. They initialise a repository, rewrite the `OWNER` placeholder, commit, create the GitHub repository when the [GitHub CLI](https://cli.github.com/) is signed in, and push.

## Release it

Push a tag. The workflow builds, tests, signs, packages, and publishes.

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Signing

**Current state: unsigned.** Windows shows a SmartScreen warning on the download. Right-click the file, open Properties, tick **Unblock**, and it runs. Every release ships SHA256 checksums so you can verify what you got.

Only one route removes that warning on the first download, and it is not a certificate: publishing through the Microsoft Store, which Microsoft signs itself. Every purchasable certificate builds SmartScreen reputation gradually instead, EV included, since Microsoft removed EV's instant-reputation behaviour in 2024.

**Read [docs/SIGNING.md](docs/SIGNING.md)** for all five options, current prices, eligibility, and the exact secrets to add. The workflow already contains two signing paths. Add the secrets and the next build comes out signed with no code changes.

## Project layout

```
src/ViceVersa/          C# WinForms tray app
tests/ViceVersa.Tests/  xunit tests for the conversion logic
ahk/                    AutoHotkey v2 edition, single readable script
installer/              Inno Setup script
tools/                  key map reference and verifier, local build, signing helpers, icon generator
docs/                   signing guide
.github/workflows/      build, sign, package, release
build.cmd               build the portable executable here, no CI needed
run.cmd                 build if needed, then start the app
push-to-github.cmd      first push, one double-click
```

## Prior art

[Vice Versa (Chrome extension)](https://chromewebstore.google.com/detail/vice-versa-fixing-bilingu/cblggbpalaaljmmjlecbchppbpeaohaa), [Cables Hebrew converter](https://www.cables.org.il/hebrew.htm), and [LangOver](https://langover.com/) all solve the same problem. This one is open source, portable, and builds reproducibly from a public workflow.

## License

MIT. See [LICENSE](LICENSE).
