"""Summarise a tracked boss trajectory: velocity profile and charge bursts.

The guide's phase one is a fixed cycle, so the boss's screen velocity should show
clear bursts (a charge) separated by near-zero periods (hovering). Recovering that
rhythm from the video gives the timing the lab controller is missing, and it does
not depend on locating the player precisely.
"""
import csv
import math
import sys


def load(path):
    rows = []
    with open(path, encoding="utf-8") as f:
        for r in csv.DictReader(f):
            try:
                rows.append((r["frame"], float(r["boss_dx"]), float(r["boss_dy"]),
                             float(r["boss_area"])))
            except ValueError:
                continue
    return rows


def main(path):
    rows = load(path)
    if len(rows) < 5:
        print("not enough rows")
        return
    # Frame index is the numeric part of the filename; fps is 20, so one frame is
    # 50 ms. Smooth the position to suppress detection jitter.
    xs = [r[1] for r in rows]
    ys = [r[2] for r in rows]
    areas = [r[3] for r in rows]

    def smooth(v, k=3):
        out = []
        for i in range(len(v)):
            lo = max(0, i - k)
            hi = min(len(v), i + k + 1)
            out.append(sum(v[lo:hi]) / (hi - lo))
        return out

    sx, sy = smooth(xs), smooth(ys)
    speed = []
    for i in range(1, len(sx)):
        speed.append(math.hypot(sx[i] - sx[i - 1], sy[i] - sy[i - 1]))

    print(f"frames={len(rows)}  area median={sorted(areas)[len(areas)//2]:.0f}")
    print(f"speed px/frame: mean={sum(speed)/len(speed):.1f} "
          f"max={max(speed):.1f}")
    # Bursts: runs where speed exceeds a threshold, reported as frame ranges.
    thr = max(8.0, (sum(speed) / len(speed)) * 1.6)
    runs = []
    start = None
    for i, s in enumerate(speed):
        if s > thr and start is None:
            start = i
        elif s <= thr and start is not None:
            if i - start >= 2:
                runs.append((start, i, max(speed[start:i])))
            start = None
    if start is not None and len(speed) - start >= 2:
        runs.append((start, len(speed), max(speed[start:])))
    print(f"threshold={thr:.1f}  bursts={len(runs)}")
    print(" burst  frames      dur(s)  gap(s)  peak px/frame")
    for n, (a, b, pk) in enumerate(runs):
        gap = (a - runs[n - 1][1]) / 20.0 if n else 0.0
        print(f"  {n:3d}  {a:4d}-{b:<4d}  {(b-a)/20.0:5.2f}  {gap:6.2f}  {pk:6.1f}")


if __name__ == "__main__":
    main(sys.argv[1])
