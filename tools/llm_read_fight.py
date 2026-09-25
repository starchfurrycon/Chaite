"""Read the player/boss relationship tick by tick from gameplay frames.

Absolute percentage coordinates came back unreliable, so the relative geometry is
requested instead: where the boss sits relative to the player, which way the
player is moving, and whether the player is airborne. That is exactly what the
lab controller needs to act on, and it is a much easier question for a vision
model than pixel estimation.

Frames are sent concurrently because each call is a network round trip.
"""
import argparse
import concurrent.futures as cf
import os
import sys

from llm_describe import ask

PROMPT = """One frame of a Terraria Duke Fishron fight. Answer with exactly these \
lines and nothing else:

BOSS_REL: <choose one: above|below|left|right|above-left|above-right|below-left|\
below-right|overlapping|absent>
BOSS_DIST: <choose one: touching|near|medium|far>
BOSS_HEADING: <which way the boss body points: left|right|up|down|upleft|\
upright|downleft|downright|unclear>
PLAYER_STATE: <choose one: flying|standing|dashing|falling|rising>
PLAYER_MOVE: <choose one: up|down|left|right|upleft|upright|downleft|downright|\
still>
DASH_TRAIL: <yes|no>
PLATFORM_BELOW: <yes|no>
NOTE: <at most 10 words>
"""


def read_one(path):
    try:
        text = ask(path, PROMPT, "GLM-4.6V", max_tokens=700)
    except Exception as e:                                    # noqa: BLE001
        text = "ERROR: " + str(e)[:80]
    return path, text


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dir")
    ap.add_argument("--workers", type=int, default=6)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()

    files = sorted(f for f in os.listdir(args.dir) if f.endswith(".png"))
    paths = [os.path.join(args.dir, f) for f in files]
    lines = []
    with cf.ThreadPoolExecutor(max_workers=args.workers) as ex:
        for path, text in ex.map(read_one, paths):
            flat = " | ".join(
                ln.strip() for ln in text.splitlines() if ln.strip())
            lines.append(f"{os.path.basename(path)}\t{flat}")
            print(f"{os.path.basename(path)}\t{flat}", flush=True)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write("\n".join(lines))


if __name__ == "__main__":
    main()
