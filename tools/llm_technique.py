"""Ask a vision model what TECHNIQUE the video is demonstrating.

Asking these models for pixel coordinates produces long, unreliable reasoning, but
asking what the on-screen annotation says works well. The overlay text is where
the formulaic method is actually stated, so that is what gets read.
"""
import concurrent.futures as cf
import os
import sys

from llm_describe import ask

PROMPT = """This is a frame from a Chinese-language Terraria guide video about \
beating Duke Fishron without taking damage. Do two things and nothing else.

1. Transcribe every Chinese annotation, caption and subtitle you can see, exactly.
2. In at most 25 English words, state the movement TECHNIQUE the frame is \
illustrating (for example: which way the player moves relative to the boss).

Reply in exactly this form:
TEXT: <the Chinese text>
TECHNIQUE: <the English summary>
"""


def one(path):
    try:
        return path, ask(path, PROMPT, "GLM-4.6V", max_tokens=1200)
    except Exception as e:                                    # noqa: BLE001
        return path, "ERROR: " + str(e)[:80]


def main():
    d = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else None
    files = sorted(f for f in os.listdir(d) if f.endswith(".png"))
    paths = [os.path.join(d, f) for f in files]
    lines = []
    with cf.ThreadPoolExecutor(max_workers=6) as ex:
        for path, text in ex.map(one, paths):
            body = "\n".join(ln.rstrip() for ln in text.splitlines()
                             if ln.strip())
            # Keep the useful tail: these models emit reasoning first and the
            # requested answer last, and the content field is empty when the
            # budget runs out, in which case the tail is all we have.
            keep = [ln for ln in body.splitlines()
                    if ln.strip().startswith(("TEXT:", "TECHNIQUE:"))]
            joined = " || ".join(keep) if keep else body[-600:]
            lines.append(f"{os.path.basename(path)}\t{joined}")
            print(f"{os.path.basename(path)}\t{joined}", flush=True)
    if out:
        with open(out, "w", encoding="utf-8") as f:
            f.write("\n".join(lines))


if __name__ == "__main__":
    main()
