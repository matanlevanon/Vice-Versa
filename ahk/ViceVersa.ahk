#Requires AutoHotkey v2.0
#SingleInstance Force
; ============================================================================
;  Vice Versa - Hebrew / English keyboard layout converter
;
;  Press the hotkey to convert the selected text between the Hebrew and English
;  keyboard layouts by key position. This is not translation.
;
;  Default hotkey: Shift+F12
;
;  Run it: install AutoHotkey v2 from https://www.autohotkey.com then
;  double-click this file. Or grab the compiled ViceVersa-AHK.exe from the
;  Releases page, which needs nothing installed.
;
;  The key map here is generated and verified by tools/keymap_reference.py.
; ============================================================================

; ---------------------------------------------------------------- key mapping

; Physical key positions on a US QWERTY keyboard.
global EN_KEYS := "qwertyuiop[]asdfghjkl;'zxcvbnm,./``"

; What the standard Windows Hebrew (he-IL) layout produces from those keys.
global HE_KEYS := "/'קראטוןםפ][שדגכעיחלךף,זסבהנמצתץ.;"

global EN_TO_HE := Map()
global HE_TO_EN := Map()

BuildMaps() {
    en := EN_KEYS
    he := HE_KEYS

    Loop StrLen(en) {
        e := SubStr(en, A_Index, 1)
        h := SubStr(he, A_Index, 1)
        EN_TO_HE[e] := h
        HE_TO_EN[h] := e
    }

    ; Hebrew has no case. Shift plus a letter is still the same physical key.
    Loop StrLen(en) {
        e := SubStr(en, A_Index, 1)
        if (e ~= "^[a-z]$")
            EN_TO_HE[StrUpper(e)] := EN_TO_HE[e]
    }
}

BuildMaps()

; --------------------------------------------------------------- conversion

HasHebrew(text) {
    Loop StrLen(text) {
        code := Ord(SubStr(text, A_Index, 1))
        if (code >= 0x05D0 && code <= 0x05EA)
            return true
    }
    return false
}

HasLatin(text) {
    return RegExMatch(text, "[A-Za-z]") > 0
}

MapText(text, table) {
    out := ""
    Loop StrLen(text) {
        c := SubStr(text, A_Index, 1)
        out .= table.Has(c) ? table[c] : c
    }
    return out
}

ToHebrew(text) => MapText(text, EN_TO_HE)
ToEnglish(text) => MapText(text, HE_TO_EN)

; Auto mode judges each whitespace-separated word on its own, so mixed text
; converts correctly in one pass. Whitespace is preserved exactly.
ConvertAuto(text) {
    out := ""
    word := ""

    Loop StrLen(text) {
        c := SubStr(text, A_Index, 1)

        if (c = " " || c = "`t" || c = "`n" || c = "`r") {
            if (word != "") {
                out .= HasHebrew(word) ? ToEnglish(word) : ToHebrew(word)
                word := ""
            }
            out .= c
        } else {
            word .= c
        }
    }

    if (word != "")
        out .= HasHebrew(word) ? ToEnglish(word) : ToHebrew(word)

    return out
}

ConvertText(text, direction) {
    if (direction = "EnglishToHebrew")
        return ToHebrew(text)
    if (direction = "HebrewToEnglish")
        return ToEnglish(text)
    return ConvertAuto(text)
}

; ------------------------------------------------------------------ settings

global CONFIG_FILE := ResolveConfigPath()

ResolveConfigPath() {
    candidate := A_ScriptDir . "\ViceVersa.ini"

    try {
        FileAppend("", candidate)
        return candidate
    } catch {
        DirCreate(A_AppData . "\ViceVersa")
        return A_AppData . "\ViceVersa\ViceVersa.ini"
    }
}

global CFG := {
    hotkey: IniRead(CONFIG_FILE, "General", "Hotkey", "+F12"),
    direction: IniRead(CONFIG_FILE, "General", "Direction", "Auto"),
    autoSelectAll: IniRead(CONFIG_FILE, "General", "AutoSelectAll", "1") = "1",
    switchLayout: IniRead(CONFIG_FILE, "General", "SwitchKeyboardLayout", "1") = "1",
    restoreClipboard: IniRead(CONFIG_FILE, "General", "RestoreClipboard", "1") = "1",
    showTips: IniRead(CONFIG_FILE, "General", "ShowNotifications", "1") = "1"
}

SaveConfig() {
    IniWrite(CFG.hotkey, CONFIG_FILE, "General", "Hotkey")
    IniWrite(CFG.direction, CONFIG_FILE, "General", "Direction")
    IniWrite(CFG.autoSelectAll ? "1" : "0", CONFIG_FILE, "General", "AutoSelectAll")
    IniWrite(CFG.switchLayout ? "1" : "0", CONFIG_FILE, "General", "SwitchKeyboardLayout")
    IniWrite(CFG.restoreClipboard ? "1" : "0", CONFIG_FILE, "General", "RestoreClipboard")
    IniWrite(CFG.showTips ? "1" : "0", CONFIG_FILE, "General", "ShowNotifications")
}

; --------------------------------------------------------------- main action

global BUSY := false

DoConvert(*) {
    global BUSY

    if (BUSY)
        return

    BUSY := true

    try {
        RunConversion()
    } catch as err {
        Tip("Vice Versa error: " . err.Message)
    } finally {
        BUSY := false
    }
}

RunConversion() {
    targetHwnd := WinExist("A")

    saved := ClipboardAll()
    A_Clipboard := ""

    Send "^c"
    copied := ClipWait(0.6, 0)

    if (!copied && CFG.autoSelectAll) {
        Send "^a"
        Sleep 60
        A_Clipboard := ""
        Send "^c"
        copied := ClipWait(0.6, 0)
    }

    text := copied ? A_Clipboard : ""

    if (text = "") {
        RestoreClip(saved)
        Tip("Nothing to convert. Select some text and try again.")
        return
    }

    converted := ConvertText(text, CFG.direction)

    if (converted = text) {
        RestoreClip(saved)
        Tip("That text has nothing to convert.")
        return
    }

    A_Clipboard := converted
    ClipWait(1, 1)
    Send "^v"
    Sleep 200

    RestoreClip(saved)

    if (CFG.switchLayout)
        SwitchLayout(targetHwnd, converted)
}

RestoreClip(saved) {
    if (CFG.restoreClipboard)
        A_Clipboard := saved
}

Tip(message) {
    if (CFG.showTips)
        TrayTip message, "Vice Versa", 0x1
}

; ---------------------------------------------------------- layout switching

SwitchLayout(hwnd, text) {
    klid := HasHebrew(text) ? "0000040D" : "00000409"
    hkl := DllCall("LoadKeyboardLayout", "Str", klid, "UInt", 1, "Ptr")

    if (!hkl || !hwnd)
        return

    ; WM_INPUTLANGCHANGEREQUEST
    try PostMessage 0x0050, 0, hkl, , "ahk_id " hwnd
}

; ------------------------------------------------------------------ tray menu

BuildTray() {
    A_IconTip := "Vice Versa  (" . CFG.hotkey . ")"

    tray := A_TrayMenu
    tray.Delete()
    tray.Add("Convert selection now", DoConvert)
    tray.Add()
    tray.Add("Change hotkey...", ChangeHotkey)
    tray.Add("Direction: " . CFG.direction, CycleDirection)
    tray.Add("Select whole field if nothing selected", ToggleAutoSelect)
    tray.Add("Switch keyboard language after converting", ToggleSwitchLayout)
    tray.Add("Restore clipboard afterwards", ToggleRestoreClipboard)
    tray.Add()
    tray.Add("Start with Windows", ToggleStartup)
    tray.Add("Open settings file", OpenSettingsFile)
    tray.Add()
    tray.Add("About", ShowAbout)
    tray.Add("Exit", (*) => ExitApp())

    if (CFG.autoSelectAll)
        tray.Check("Select whole field if nothing selected")
    if (CFG.switchLayout)
        tray.Check("Switch keyboard language after converting")
    if (CFG.restoreClipboard)
        tray.Check("Restore clipboard afterwards")
    if (StartupEnabled())
        tray.Check("Start with Windows")

    tray.Default := "Convert selection now"
}

OpenSettingsFile(*) {
    Run('notepad.exe "' . CONFIG_FILE . '"')
}

CycleDirection(*) {
    order := ["Auto", "EnglishToHebrew", "HebrewToEnglish"]
    index := 1

    for i, value in order {
        if (value = CFG.direction) {
            index := Mod(i, order.Length) + 1
            break
        }
    }

    CFG.direction := order[index]
    SaveConfig()
    BuildTray()
    Tip("Direction: " . CFG.direction)
}

ToggleAutoSelect(*) {
    CFG.autoSelectAll := !CFG.autoSelectAll
    SaveConfig()
    BuildTray()
}

ToggleSwitchLayout(*) {
    CFG.switchLayout := !CFG.switchLayout
    SaveConfig()
    BuildTray()
}

ToggleRestoreClipboard(*) {
    CFG.restoreClipboard := !CFG.restoreClipboard
    SaveConfig()
    BuildTray()
}

ChangeHotkey(*) {
    prompt := "Enter a hotkey using AutoHotkey notation.`n`n"
        . "^ = Ctrl    ! = Alt    + = Shift    # = Windows`n`n"
        . "Examples:  +F12   ^!h   ^+x"

    result := InputBox(prompt, "Vice Versa hotkey", "w380 h200", CFG.hotkey)

    if (result.Result != "OK" || result.Value = "")
        return

    old := CFG.hotkey

    try Hotkey(old, , "Off")

    try {
        Hotkey(result.Value, DoConvert, "On")
        CFG.hotkey := result.Value
        SaveConfig()
        BuildTray()
        Tip("Hotkey set to " . CFG.hotkey)
    } catch as err {
        try Hotkey(old, DoConvert, "On")
        MsgBox("That hotkey could not be registered.`n`n" . err.Message, "Vice Versa", "Icon!")
    }
}

; -------------------------------------------------------------------- startup

StartupPath() => A_Startup "\ViceVersa.lnk"

StartupEnabled() => FileExist(StartupPath()) != ""

ToggleStartup(*) {
    if (StartupEnabled()) {
        FileDelete(StartupPath())
    } else {
        target := A_IsCompiled ? A_ScriptFullPath : A_AhkPath
        arguments := A_IsCompiled ? "" : ('"' . A_ScriptFullPath . '"')
        FileCreateShortcut(target, StartupPath(), A_ScriptDir, arguments)
    }

    BuildTray()
}

ShowAbout(*) {
    message := "Vice Versa (AutoHotkey edition)`n`n"
        . "Converts text between Hebrew and English by keyboard position.`n"
        . "Hotkey: " . CFG.hotkey . "`n`n"
        . "Settings file:`n" . CONFIG_FILE

    MsgBox(message, "About Vice Versa", "Iconi")
}

; ------------------------------------------------------------------ bootstrap

BuildTray()

try {
    Hotkey(CFG.hotkey, DoConvert, "On")
} catch as err {
    warning := "Could not register " . CFG.hotkey . ".`n"
        . "Another application is using it.`n`n"
        . "Right-click the tray icon and pick a different hotkey."

    MsgBox(warning, "Vice Versa", "Icon!")
}
