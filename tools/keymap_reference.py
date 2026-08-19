#!/usr/bin/env python3
"""
Reference implementation and verifier for the Vice Versa keyboard map.

This is the single source of truth for the Hebrew (Israel Standard) <-> US English
key-position mapping. Run it to validate the table and regenerate the C# and AHK
map literals so the three implementations can never drift apart.

    python3 tools/keymap_reference.py --check
    python3 tools/keymap_reference.py --emit csharp
    python3 tools/keymap_reference.py --emit ahk
"""

import argparse
import sys
import unicodedata

# Physical key positions on a US QWERTY keyboard, and what the standard
# Windows Hebrew (he-IL) layout produces from the same physical key.
#
# Verified against the Windows "Hebrew" keyboard layout (Israel Standard SI-1452).
EN = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`"
HE = "/'קראטוןםפ][שדגכעיחלךף,זסבהנמצתץ.;"

HEBREW_LETTERS = "אבגדהוזחטיכךלמםנןסעפףצץקרשת"

# Characters that are unambiguous evidence of each script, used by auto-detect.
LATIN_LETTERS = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"


def build_maps():
    if len(EN) != len(HE):
        raise SystemExit(f"length mismatch: EN={len(EN)} HE={len(HE)}")

    en_to_he = {}
    he_to_en = {}

    for e, h in zip(EN, HE):
        if e in en_to_he:
            raise SystemExit(f"duplicate EN key: {e!r}")
        if h in he_to_en:
            raise SystemExit(f"duplicate HE key: {h!r}")
        en_to_he[e] = h
        he_to_en[h] = e

    # Hebrew has no letter case. Shift+letter on a US layout still refers to the
    # same physical key, so uppercase Latin letters convert to the same Hebrew
    # letter. This is one-way: Hebrew -> English always produces lowercase.
    for e in EN:
        if e.isalpha():
            en_to_he[e.upper()] = en_to_he[e]

    return en_to_he, he_to_en


EN_TO_HE, HE_TO_EN = build_maps()


def has_hebrew(text):
    return any(c in HEBREW_LETTERS for c in text)


def convert_en_to_he(text):
    return "".join(EN_TO_HE.get(c, c) for c in text)


def convert_he_to_en(text):
    return "".join(HE_TO_EN.get(c, c) for c in text)


def is_latin(c):
    return ("a" <= c <= "z") or ("A" <= c <= "Z")


ACRONYM_MIN_LENGTH = 2


def is_all_upper_latin(word):
    """True when every Latin letter in the word is uppercase and there are at
    least ACRONYM_MIN_LENGTH of them."""
    letters = [c for c in word if is_latin(c)]
    return len(letters) >= ACRONYM_MIN_LENGTH and all(c.isupper() for c in letters)


def convert_hebrew_word(word, smart_case=True):
    """Convert a word that was typed on the Hebrew layout back to English.

    Latin letters inside such a word came from Caps Lock rather than from the
    Hebrew map, so they are left alone. The one exception is a run of a single
    letter, which is a stray Caps Lock capital and is folded to lowercase to
    match the rest of the word."""
    if not smart_case:
        return convert_he_to_en(word)

    out = []
    i = 0

    while i < len(word):
        if is_latin(word[i]):
            j = i
            while j < len(word) and is_latin(word[j]):
                j += 1
            run = word[i:j]
            out.append(run.lower() if len(run) == 1 else run)
            i = j
        else:
            out.append(HE_TO_EN.get(word[i], word[i]))
            i += 1

    return "".join(out)


def convert_english_word(word, smart_case=True, text_has_hebrew=False):
    """Convert a word that was typed on the English layout to Hebrew.

    An all-uppercase word is left alone, but only when the selection also
    contains Hebrew. Without that evidence the selection is more likely to be
    Hebrew typed on the English layout with Caps Lock on, where AKUO really
    does mean shalom and must still convert."""
    if smart_case and text_has_hebrew and is_all_upper_latin(word):
        return word

    return convert_en_to_he(word)


def convert_word(word, smart_case=True, text_has_hebrew=False):
    has_heb = has_hebrew(word)
    has_lat = any(is_latin(c) for c in word)

    # Digits, punctuation and symbols on their own carry no layout evidence.
    if not has_heb and not has_lat:
        return word

    if has_heb:
        return convert_hebrew_word(word, smart_case)

    return convert_english_word(word, smart_case, text_has_hebrew)


def convert_auto(text, smart_case=True):
    """Per-word auto-detect. A word containing Hebrew letters is converted
    Hebrew -> English; every other word is converted English -> Hebrew.
    Whitespace is preserved exactly."""
    out = []
    buf = []
    text_has_hebrew = has_hebrew(text)

    def flush():
        if not buf:
            return
        out.append(convert_word("".join(buf), smart_case, text_has_hebrew))
        buf.clear()

    for c in text:
        if c.isspace():
            flush()
            out.append(c)
        else:
            buf.append(c)
    flush()
    return "".join(out)


# --------------------------------------------------------------------------- checks

def check():
    failures = []

    def expect(name, got, want):
        if got != want:
            failures.append(f"{name}\n    got:  {got!r}\n    want: {want!r}")

    # Bijectivity
    expect("map sizes", len(HE_TO_EN), len(EN))
    expect("round trip en->he->en", convert_he_to_en(convert_en_to_he(EN)), EN)
    expect("round trip he->en->he", convert_en_to_he(convert_he_to_en(HE)), HE)

    # Real-world cases: what you get when you type the wrong layout.
    expect("shalom typed in English", convert_en_to_he("akuo"), "שלום")
    expect("shalom back to English", convert_he_to_en("שלום"), "akuo")
    expect("hello -> hebrew keys", convert_en_to_he("hello"), "יקךךם")
    expect("toda typed in English", convert_en_to_he(",usv"), "תודה")
    expect("toda back to English", convert_he_to_en("תודה"), ",usv")
    expect("boker tov", convert_en_to_he("cuer yuc"), "בוקר טוב")
    expect("yisrael", convert_en_to_he("hartk"), "ישראל")

    # Digits and unmapped characters pass through untouched
    expect("digits untouched", convert_en_to_he("12345"), "12345")
    expect("digits untouched reverse", convert_he_to_en("12345"), "12345")
    expect("newlines preserved", convert_auto("abc\ndef"), "שנב\nגקכ")

    # Uppercase collapses to the same Hebrew letter
    expect("uppercase", convert_en_to_he("ABC"), convert_en_to_he("abc"))

    # Auto-detect on mixed content
    expect("auto mixed", convert_auto("שלום hello"), "akuo יקךךם")
    expect("auto hebrew word", convert_auto("שלום"), "akuo")
    expect("auto punctuation only", convert_auto("123"), "123")

    # Whitespace preservation with tabs and multiple spaces
    expect("whitespace", convert_auto("a  b\tc"), "ש  נ\tב")

    # Smart case. Caps Lock on the Hebrew layout emits Latin uppercase, so an
    # ALL-CAPS run is already correct and a lone capital beside Hebrew is noise.
    heb_services = "\u05e7\u05e8\u05d4\u05df\u05d1\u05e7\u05d3"  # "ervices" on the Hebrew layout

    expect("acronym plus stray capital",
           convert_auto("API S" + heb_services), "API services")
    expect("acronym glued to hebrew",
           convert_auto("API" + heb_services), "APIervices")
    expect("acronym kept when hebrew is present elsewhere",
           convert_auto("API \u05e9\u05dc\u05d5\u05dd"), "API akuo")

    # Without Hebrew evidence an all-caps word is Hebrew typed with Caps Lock on,
    # so it must still convert. This is the mirror image of the case above.
    expect("all caps with no hebrew still converts", convert_auto("AKUO"), "\u05e9\u05dc\u05d5\u05dd")
    expect("all caps sentence with no hebrew", convert_auto("AKUO CUER YUC"),
           "\u05e9\u05dc\u05d5\u05dd \u05d1\u05d5\u05e7\u05e8 \u05d8\u05d5\u05d1")
    expect("acronym alone still converts", convert_auto("API"), "\u05e9\u05e4\u05df")

    # Only a single stray capital is folded. Longer runs keep their case, so
    # brand names glued to Hebrew survive.
    expect("brand name after a hebrew prefix",
           convert_auto("\u05d1-Zoom"), "c-Zoom")
    expect("brand name before hebrew",
           convert_auto("Gmail\u05e9\u05dc\u05d9"), "Gmailakh")

    expect("single capital is not an acronym", convert_auto("A"), "\u05e9")
    expect("title case still converts", convert_auto("Hello"), convert_auto("hello"))
    expect("symbols alone are left alone", convert_auto("..."), "...")
    expect("smart case off keeps the old behaviour",
           convert_auto("API S" + heb_services, smart_case=False),
           "\u05e9\u05e4\u05df Services")

    # Every Hebrew letter has an English key
    missing = [c for c in HEBREW_LETTERS if c not in HE_TO_EN]
    if missing:
        failures.append(f"Hebrew letters with no key position: {missing}")

    if failures:
        print("FAIL")
        for f in failures:
            print("  - " + f)
        return 1

    print(f"OK  {len(EN)} key positions, {len(HE_TO_EN)} reverse entries, all checks passed")
    print(f"    EN: {EN}")
    print(f"    HE: {HE}")
    for c in HE:
        name = unicodedata.name(c, "?")
        print(f"      {EN[HE.index(c)]!r} -> {c!r}  {name}")
    return 0


# --------------------------------------------------------------------------- emitters

def emit_csharp():
    print(f'    private const string EnglishKeys = "{EN.replace(chr(92), chr(92)*2).replace(chr(34), chr(92)+chr(34))}";')
    print(f'    private const string HebrewKeys  = "{HE}";')


def emit_ahk():
    print(f'    static EN := "{EN}"')
    print(f'    static HE := "{HE}"')


# ------------------------------------------------------------- source checking

import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SOURCES = [
    ("src/ViceVersa/TextConverter.cs", r'EnglishKeys = "(.*)";', r'HebrewKeys = "(.*)";', False),
    ("tests/ViceVersa.Tests/TextConverterTests.cs", r'EnglishKeys = "(.*)";', r'HebrewKeys = "(.*)";', False),
    ("ahk/ViceVersa.ahk", r'global EN_KEYS := "(.*)"', r'global HE_KEYS := "(.*)"', True),
]


def check_sources():
    """Every implementation must carry the exact same two strings."""
    failures = []

    for relative, en_pattern, he_pattern, ahk_escaped in SOURCES:
        path = os.path.join(ROOT, relative)

        if not os.path.exists(path):
            failures.append(f"{relative}: missing")
            continue

        with open(path, encoding="utf-8") as handle:
            text = handle.read()

        en_match = re.search(en_pattern, text)
        he_match = re.search(he_pattern, text)

        if not en_match or not he_match:
            failures.append(f"{relative}: could not find the key map literals")
            continue

        found_en = en_match.group(1)
        found_he = he_match.group(1)

        if ahk_escaped:
            # AutoHotkey escapes a literal backtick by doubling it.
            found_en = found_en.replace("``", "`")

        if found_en != EN:
            failures.append(f"{relative}: English key string differs\n    {found_en!r}")
        if found_he != HE:
            failures.append(f"{relative}: Hebrew key string differs\n    {found_he!r}")

    if failures:
        print("FAIL  implementations are out of step")
        for failure in failures:
            print("  - " + failure)
        return 1

    print(f"OK  {len(SOURCES)} implementations carry an identical key map")
    return 0


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--check", action="store_true")
    p.add_argument("--emit", choices=["csharp", "ahk"])
    a = p.parse_args()

    if a.emit == "csharp":
        emit_csharp()
    elif a.emit == "ahk":
        emit_ahk()
    else:
        sys.exit(check() or check_sources())


if __name__ == "__main__":
    main()
