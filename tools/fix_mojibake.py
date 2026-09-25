"""Repair UTF-8 Chinese that was decoded as a single-byte codepage.

The vision model returns valid UTF-8, but it passes through a PowerShell pipeline
that decodes it as GBK, so the bytes survive while the characters do not. Encoding
back to GBK and decoding as UTF-8 recovers the original text, which is where the
formulaic method is actually written down.
"""
import sys


def fix(s):
    for enc in ("gbk", "cp936", "latin-1"):
        try:
            return s.encode(enc).decode("utf-8")
        except (UnicodeDecodeError, UnicodeEncodeError):
            continue
    return s


if __name__ == "__main__":
    path = sys.argv[1]
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            print(fix(line.rstrip()))
