#!/usr/bin/env python3
"""Rebuild the Manager's bundled typefaces from tools/fonts-src.

The console carries its own two faces and loads them by path at start-up
(Chaite.Manager/FontBook.cs, PrivateFontCollection.AddFontFile).  Both are
SIL Open Font License 1.1 and neither is original work:

    Chaite IBM Plex Sans   Latin letters, digits, punctuation
    Chaite Noto Sans SC    Simplified Chinese and CJK punctuation

Upstream, before this script runs, they are called "IBM Plex Sans" and
"Noto Sans SC".  The "Chaite" prefix is applied here, in the name table, and
FontBook.cs compares family names with StringComparison.Ordinal -- so the
names below are the contract and must not drift.  Nothing in src/ is modified
by this script; it only writes into src/Chaite.Manager/Fonts/.

What it does, in order:

  1. Scans the Manager's C# sources for string and character literals and
     collects every character the console can render.  Printable ASCII is
     always included whatever the scan finds.
  2. Subsets each upstream file to that character set.  The CJK face is also
     reached by mixed-script captions, so it keeps the Latin letters, digits
     and ASCII punctuation it is asked for -- laying out "F8 x2" inside a
     Chinese caption is ordinary, and a missing glyph there is a tofu box.
  3. Renames the family in the name table, and makes the heavier weight of
     each pair declare itself as the family's Bold in the RIBBI sense, so
     FontFamily.IsStyleAvailable(FontStyle.Bold) is true.
  4. Asserts cmap coverage.  Any character the interface can render that the
     subset does not carry is listed and the script exits 1.
  5. Writes Fonts/OFL.txt with both licences.

Three characters are known not to resolve in either upstream face and are
excluded on purpose: U+2190 LEFT ARROW, U+2192 RIGHT ARROW, U+2713 CHECK
MARK.  Their absence from the sources is asserted, because a tofu box in the
console is exactly what this assertion exists to prevent.

Usage:  python tools/build-fonts.py [--check]

    --check   generate nothing, only report what would change and whether
              the coverage assertion holds.
"""

import argparse
import datetime
import hashlib
import os
import re
import sys

try:
    from fontTools.ttLib import TTFont
    from fontTools import subset
except ImportError:  # pragma: no cover - environment guard
    sys.stderr.write(
        "fontTools is required: python -m pip install fonttools brotli\n")
    raise SystemExit(2)

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SOURCE = os.path.join(HERE, "fonts-src")
OUTPUT = os.path.join(ROOT, "src", "Chaite.Manager", "Fonts")
SOURCE_ROOT = os.path.join(ROOT, "src", "Chaite.Manager")

# The two families, as FontBook.cs spells them.
LATIN_FAMILY = "Chaite IBM Plex Sans"
CJK_FAMILY = "Chaite Noto Sans SC"

# Upstream files in tools/fonts-src, and what each becomes in Fonts/.
# (source, family, subfamily, writes family Bold bit)
PAIRS = (
    ("IBMPlexSans-Regular.ttf", LATIN_FAMILY, "Regular", False),
    ("IBMPlexSans-SemiBold.ttf", LATIN_FAMILY, "Bold", True),
    ("NotoSansSC-Regular.otf", CJK_FAMILY, "Regular", False),
    ("NotoSansSC-Bold.otf", CJK_FAMILY, "Bold", True),
)

# Character above this code point selects the CJK family at run time
# (FontBook.NeedsChinese).  It is the same threshold the subset split uses.
CJK_THRESHOLD = 0x2E7F

# Known unusable in both upstream faces.  They must not appear in any string
# the console renders; if one shows up the build stops rather than shipping a
# box glyph.
KNOWN_MISSING = {
    0x2190: "LEFT ARROW",
    0x2192: "RIGHT ARROW",
    0x2713: "CHECK MARK",
}

# Code points that the scanner sees inside a literal but that are never
# rendered, because the literal is a comparison constant rather than a
# caption.  FontBook.NeedsChinese tests `text[i] > '\u2E7F'`, so that
# character is a threshold, not a glyph the console draws; neither upstream
# face has it and nothing asks for it.  Anything listed here is reported, not
# subset, and not treated as a coverage failure -- but adding to this table is
# a claim that the character cannot reach a rendered string, so it is printed
# on every run.
NOT_RENDERED = {
    0x2E7F: "FontBook family-selection threshold",
}

# Always subset in, whatever the scan turns up.  A caption is built from
# string literals, but a formatted number, a file name in a status line or an
# interpolated count can put any ASCII digit or punctuation on screen.
ALWAYS = set(range(0x20, 0x7F))

CONTROL = set(range(0x00, 0x20)) | {0x7F}

# Regex for a C# string or character literal.  Comments are stripped first, so
# the only remaining quotes are delimiters.  Handles "..." , @"..." and '...',
# with the doubled-quote escape inside verbatim strings.
LITERAL = re.compile(
    r'@?"(?P<v>(?:[^"\\]|\\.|"")*)"'
    r"|'(?P<c>(?:[^'\\]|\\.)*)'",
    re.DOTALL,
)
BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.DOTALL)
LINE_COMMENT = re.compile(r"//[^\n]*")

SIMPLE_ESCAPES = {
    "0": "\0", "a": "\a", "b": "\b", "f": "\f", "n": "\n",
    "r": "\r", "t": "\t", "v": "\v", "\\": "\\", "'": "'", '"': '"',
}


def strip_comments(text):
    """Remove // and /* */ comments without disturbing string contents."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == '"' or (ch == "@" and i + 1 < n and text[i + 1] == '"'):
            verbatim = ch == "@"
            if verbatim:
                i += 1
            i += 1
            out.append('"')
            while i < n:
                if verbatim:
                    if text[i] == '"':
                        if i + 1 < n and text[i + 1] == '"':
                            i += 2
                            continue
                        break
                    out.append(text[i])
                    i += 1
                else:
                    if text[i] == "\\" and i + 1 < n:
                        out.append(text[i])
                        out.append(text[i + 1])
                        i += 2
                        continue
                    if text[i] == '"':
                        break
                    out.append(text[i])
                    i += 1
            out.append('"')
            i += 1
            continue
        if ch == "'":
            out.append(ch)
            i += 1
            while i < n:
                if text[i] == "\\" and i + 1 < n:
                    out.append(text[i])
                    out.append(text[i + 1])
                    i += 2
                    continue
                out.append(text[i])
                if text[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "/":
            while i < n and text[i] != "\n":
                i += 1
            continue
        if ch == "/" and i + 1 < n and text[i + 1] == "*":
            i += 2
            while i + 1 < n and not (text[i] == "*" and text[i + 1] == "/"):
                i += 1
            i += 2
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def unescape(body, verbatim):
    """Turn a C# literal body into the text it denotes."""
    if verbatim:
        return body.replace('""', '"')
    out = []
    i = 0
    n = len(body)
    while i < n:
        ch = body[i]
        if ch != "\\" or i + 1 >= n:
            out.append(ch)
            i += 1
            continue
        nxt = body[i + 1]
        if nxt in SIMPLE_ESCAPES:
            out.append(SIMPLE_ESCAPES[nxt])
            i += 2
            continue
        if nxt == "u" and i + 5 < n:
            out.append(chr(int(body[i + 2:i + 6], 16)))
            i += 6
            continue
        if nxt == "U" and i + 9 < n:
            out.append(chr(int(body[i + 2:i + 10], 16)))
            i += 10
            continue
        if nxt == "x":
            j = i + 2
            while j < n and j < i + 6 and body[j] in "0123456789abcdefABCDEF":
                j += 1
            out.append(chr(int(body[i + 2:j], 16)))
            i = j
            continue
        out.append(nxt)
        i += 2
    return "".join(out)


def scan_sources(directory):
    """Every character that appears in a string or character literal."""
    found = {}
    for base, _dirs, files in os.walk(directory):
        for name in sorted(files):
            if not name.endswith(".cs"):
                continue
            path = os.path.join(base, name)
            with open(path, "r", encoding="utf-8-sig", newline="") as handle:
                text = strip_comments(handle.read())
            for match in LITERAL.finditer(text):
                value = match.group("v")
                if value is None:
                    value = match.group("c") or ""
                    text_value = unescape(value, False)
                else:
                    verbatim = text[match.start()] == "@"
                    text_value = unescape(value, verbatim)
                for character in text_value:
                    code = ord(character)
                    if code in CONTROL:
                        continue
                    found.setdefault(code, set()).add(name)
    return found


def subset_font(source_path, characters):
    """Subset one face to `characters`, in memory.  Returns (font, missing)."""
    font = TTFont(source_path)

    covered = set(font.getBestCmap().keys())
    missing = sorted(c for c in characters if c not in covered)
    if missing:
        font.close()
        return None, missing

    options = subset.Options()
    options.drop_tables += ["DSIG", "BASE", "VORG", "vhea", "vmtx", "meta"]
    options.layout_features = []
    options.name_IDs = ["*"]
    options.name_legacy = True
    options.name_languages = ["*"]
    options.notdef_outline = True
    options.recalc_bounds = False
    options.recalc_timestamp = False
    options.desubroutinize = True
    options.glyph_names = False
    options.legacy_kern = False
    options.symbol_cmap = False
    options.ignore_missing_unicodes = False
    options.hinting = True
    options.ignore_missing_glyphs = False
    options.passthrough_tables = False
    options.canonical_order = True

    subsetter = subset.Subsetter(options=options)
    subsetter.populate(unicodes=sorted(characters))
    subsetter.subset(font)
    return font, []


def apply_names(font, family, subfamily, bold):
    """Apply the console's family name and the RIBBI bold bit, in memory.

    Every platform and locale record for the four identity name IDs is
    rewritten, and records with the same (platform, encoding, language) as one
    we are about to write are dropped first.  That is not tidiness: the Noto
    CJK sources carry a second Windows record under the zh-CN language ID
    (0x0804) and GDI+ picks that one on a Chinese-locale machine.  Writing only
    (3,1,0x0409) left the face resolving as "Noto Sans SC" -- measured, in
    FontProbe, as "Noto Sans SC" appearing in the private collection while the
    name table claimed otherwise.  FontBook compares names with Ordinal, so a
    single leftover record is the difference between the bundled face and a
    silent fallback to Microsoft Sans Serif.
    """
    name = font["name"]
    full = family if subfamily == "Regular" else family + " " + subfamily
    postscript = (family + "-" + subfamily).replace(" ", "")
    wanted = {1: family, 2: subfamily, 4: full, 6: postscript}

    # Where this font already keeps its identity records, plus the two records
    # the platform needs and that an OTF may not have.
    shipped = set()
    for record in name.names:
        if record.nameID in wanted or record.nameID in (16, 17, 21, 22):
            shipped.add((record.platformID, record.platEncID, record.langID))
    shipped.add((3, 1, 0x409))
    shipped.add((1, 0, 0))

    for name_id in (1, 2, 4, 6, 16, 17, 21, 22):
        for record in list(name.names):
            if record.nameID == name_id:
                name.names.remove(record)

    for platform, encoding, language in sorted(shipped):
        for name_id, value in sorted(wanted.items()):
            name.setName(value, name_id, platform, encoding, language)

    os2 = font["OS/2"]
    # Bit 5 REGULAR / bit 6 BOLD are mutually exclusive, bit 0 ITALIC must be
    # clear and must agree with head.macStyle bit 1.  The upstream sources are
    # inconsistent about this -- IBM Plex SemiBold ships macStyle=0 with
    # weight 600, Noto Sans SC Bold ships fsSelection with no BOLD bit -- so
    # the pair is normalised here rather than trusted.
    os2.fsSelection &= ~((1 << 5) | (1 << 6) | 0x01)
    os2.fsSelection |= (1 << 5) if bold else (1 << 6)
    head = font["head"]
    head.macStyle &= ~0x03
    if bold:
        head.macStyle |= 0x01


def font_characters(font):
    return set(font.getBestCmap().keys())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true",
                        help="report and assert, write nothing")
    arguments = parser.parse_args()

    for upstream, _family, _sub, _bold in PAIRS:
        path = os.path.join(SOURCE, upstream)
        if not os.path.exists(path):
            sys.stderr.write("missing upstream font: %s\n" % path)
            return 1

    found = scan_sources(SOURCE_ROOT)
    if not found:
        sys.stderr.write("no string literals found under %s\n" % SOURCE_ROOT)
        return 1

    absent = sorted(code for code in KNOWN_MISSING if code in found)
    if absent:
        sys.stderr.write("a character known to have no glyph is rendered:\n")
        for code in absent:
            sys.stderr.write("  U+%04X %s in %s\n" % (
                code, KNOWN_MISSING[code], ", ".join(sorted(found[code]))))
        return 1

    skipped = {}
    for code in sorted(NOT_RENDERED):
        if code in found:
            skipped[code] = found[code]
            del found[code]
    if skipped:
        for code, places in skipped.items():
            print("not subset, never rendered: U+%04X (%s) in %s"
                  % (code, NOT_RENDERED[code], ", ".join(sorted(places))))

    latin = {c for c in found if c <= CJK_THRESHOLD} | set(ALWAYS)
    cjk = set(found) | set(ALWAYS)
    # A mixed caption can carry Latin inside a CJK string, so the CJK subset
    # keeps the whole Latin range it was asked for; the reverse is not true
    # and the Latin face has no ideographs to offer.
    cjk |= {c for c in latin if c <= CJK_THRESHOLD}

    print("scanned %d distinct characters in string literals under %s"
          % (len(found), os.path.relpath(SOURCE_ROOT, ROOT)))
    print("  latin subset %d characters, cjk subset %d characters"
          % (len(latin), len(cjk)))
    print("  cjk-only (above U+%04X): %d"
          % (CJK_THRESHOLD, len([c for c in found if c > CJK_THRESHOLD])))

    wanted = {"IBMPlexSans-Regular.ttf": latin,
              "IBMPlexSans-SemiBold.ttf": latin,
              "NotoSansSC-Regular.otf": cjk,
              "NotoSansSC-Bold.otf": cjk}

    outputs = {}
    failures = []
    for upstream, family, subfamily, bold in PAIRS:
        source_path = os.path.join(SOURCE, upstream)
        stem = os.path.splitext(upstream)[0]
        destination = os.path.join(OUTPUT, stem + ".ttf")
        characters = wanted[upstream]

        font, missing = subset_font(source_path, characters)
        if missing:
            failures.append((upstream, missing))
            continue
        apply_names(font, family, subfamily, bold)

        names = {font["name"].getDebugName(1), font["name"].getDebugName(2)}
        if family not in names:
            sys.stderr.write("%s: family name did not stick\n" % destination)
            return 1
        # Assert the identity is clean everywhere, in every locale.  A single
        # surviving upstream record is enough for GDI+ to resolve the face as
        # the upstream name and for FontBook to fall back.
        stale = sorted({record.toUnicode() for record in font["name"].names
                        if record.nameID in (1, 2, 4, 6)
                        and record.toUnicode() not in
                        {family, subfamily,
                         family if subfamily == "Regular"
                         else family + " " + subfamily,
                         (family + "-" + subfamily).replace(" ", "")}})
        if stale:
            sys.stderr.write("%s: upstream names survive in the name table: %s\n"
                             % (destination, ", ".join(repr(s) for s in stale)))
            return 1
        carried = font_characters(font)
        absent = sorted(c for c in characters if c not in carried)
        if absent:
            failures.append((upstream, absent))
            font.close()
            continue

        if arguments.check:
            font.close()
            outputs[destination] = (len(carried), None)
            continue

        os.makedirs(OUTPUT, exist_ok=True)
        font.save(destination)
        font.close()
        outputs[destination] = (len(carried), destination)

    if failures:
        sys.stderr.write("\ncmap coverage assertion failed:\n")
        for upstream, missing in failures:
            printable = "".join(chr(c) if c > 0x20 else "\\x%02x" % c
                                for c in missing)
            sys.stderr.write("  %s is missing %d character(s): %s\n"
                             % (upstream, len(missing), printable))
            for code in missing[:40]:
                sys.stderr.write("    U+%04X %s\n" % (code, chr(code)))
        sys.stderr.write(
            "\nRe-add a glyph, change the caption, or record the character in\n"
            "KNOWN_MISSING with a reason.  Do not ship a tofu box.\n")
        return 1

    if arguments.check:
        print("coverage assertion holds; nothing written (--check)")
        return 0

    write_licence()
    print("\nwritten to %s:" % os.path.relpath(OUTPUT, ROOT))
    total = 0
    for path in sorted(outputs):
        size = os.path.getsize(path)
        total += size
        digest = hashlib.sha256(open(path, "rb").read()).hexdigest()[:16]
        print("  %-28s %8d bytes  sha256:%s"
              % (os.path.basename(path), size, digest.upper()))
    licence = os.path.join(OUTPUT, "OFL.txt")
    size = os.path.getsize(licence)
    total += size
    print("  %-28s %8d bytes" % ("OFL.txt", size))
    print("  total %d bytes" % total)
    print("\nOFL.txt is written too; it must travel with the files.")
    return 0


def write_licence():
    """The two licences, verbatim, plus the provenance of what was subset."""
    import textwrap

    plex = read_text(os.path.join(SOURCE, "IBM-Plex-LICENSE.txt"))
    noto = read_text(os.path.join(SOURCE, "Noto-CJK-LICENSE.txt"))
    stamp = datetime.datetime.now().strftime("%Y-%m-%d")

    body = []
    body.append(
        "Fonts bundled with the Chaite console\n"
        "=====================================\n"
        "\n"
        "This folder holds four subset font files.  They are derived works of\n"
        "two upstream typefaces, both released under the SIL Open Font\n"
        "License, Version 1.1.  Neither typeface is original to this project\n"
        "and neither was drawn here; they were subset to the characters the\n"
        "console actually renders and renamed so that the two families cannot\n"
        "collide with a copy of the same face installed on the machine.\n"
        "\n"
        "  IBMPlexSans-Regular.ttf    Chaite IBM Plex Sans Regular\n"
        "  IBMPlexSans-SemiBold.ttf   Chaite IBM Plex Sans Bold\n"
        "  NotoSansSC-Regular.ttf     Chaite Noto Sans SC Regular\n"
        "  NotoSansSC-Bold.ttf        Chaite Noto Sans SC Bold\n"
        "\n"
        "  Chaite IBM Plex Sans  subset of IBM Plex Sans\n"
        "                        (c) 2017 IBM Corp., SIL OFL 1.1\n"
        "                        upstream: https://github.com/IBM/plex\n"
        "  Chaite Noto Sans SC   subset of Noto Sans SC, from Noto CJK\n"
        "                        (c) 2014-2021 Adobe (http://www.adobe.com/),\n"
        "                        with Reserved Font Name 'Source'\n"
        "                        (c) 2015-2021 Google LLC\n"
        "                        SIL OFL 1.1\n"
        "                        upstream: https://github.com/notofonts/noto-cjk\n"
        "\n"
        "The Reserved Font Names of the upstream licences are 'Plex' and\n"
        "'Source'.  The modified families here are named 'Chaite IBM Plex\n"
        "Sans' and 'Chaite Noto Sans SC'; no upstream reserved name is used on\n"
        "its own, and no upstream file is redistributed unmodified.\n"
        "\n"
        "Regenerated by tools/build-fonts.py on %s.\n" % stamp)
    body.append(
        "\n"
        "------------------------------------------------------------------------\n"
        "IBM Plex Sans -- SIL Open Font License 1.1\n"
        "------------------------------------------------------------------------\n"
        "\n" + plex.strip() + "\n")
    body.append(
        "\n"
        "------------------------------------------------------------------------\n"
        "Noto Sans SC (Noto CJK) -- SIL Open Font License 1.1\n"
        "------------------------------------------------------------------------\n"
        "\n" + noto.strip() + "\n")
    os.makedirs(OUTPUT, exist_ok=True)
    with open(os.path.join(OUTPUT, "OFL.txt"), "w",
              encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(body))


def read_text(path):
    with open(path, "r", encoding="utf-8-sig", newline="") as handle:
        return handle.read()


if __name__ == "__main__":
    raise SystemExit(main())
