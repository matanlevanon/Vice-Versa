; Vice Versa installer
;
; Built by the GitHub Actions workflow with:
;   ISCC /DMyAppVersion=1.0.0 /DSourceExe=...\ViceVersa.exe /O<outdir> installer\ViceVersa.iss
;
; Installs per user by default, so no UAC prompt and no administrator rights.

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef SourceExe
  #define SourceExe "..\publish\portable\ViceVersa.exe"
#endif

#define MyAppName    "Vice Versa"
#define MyAppExeName "ViceVersa.exe"
#define MyAppPublisher "Matan Levanon"
#define MyAppURL     "https://github.com/matanlevanon/Vice-Versa"

[Setup]
AppId={{7C4E1B2A-9D3F-4A61-BB58-1E9C2F0A77D4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} setup

DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

; Per-user install, no UAC prompt, no administrator rights.
;
; An all-users install is deliberately not offered. The autostart entry lives
; under HKCU and the app itself writes HKCU, so a machine-wide install would put
; "Start with Windows" on the installing administrator's account only.
PrivilegesRequired=lowest

; Refuses to overwrite files while the app is running, and offers to close it.
AppMutex=Global\ViceVersa.SingleInstance.4F2A1C
CloseApplications=yes
RestartApplications=no

; x64compatible arrived in Inno Setup 6.3. Older compilers still understand x64.
#if VER >= EncodeVer(6,3,0)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#else
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
#endif
MinVersion=10.0

OutputBaseFilename=ViceVersa-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\ViceVersa\Resources\app.ico
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup";     Description: "Start {#MyAppName} when Windows starts"; GroupDescription: "Additional options:"
Name: "desktopicon"; Description: "Create a desktop shortcut";              GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "{#SourceExe}";        DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "..\README.md";        DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";          DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\ahk\ViceVersa.ahk"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";     Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ViceVersa"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Start {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\ViceVersa"
