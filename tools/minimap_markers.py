"""Read the player and boss markers from the Terraria minimap, correctly bounded.

The previous attempt took the whole top-right corner and so picked up the buff row
and the minimap frame, which are large and static. Two corrections:

  * The minimap proper is the lower-left part of that corner, so the search area is
    inset to exclude the buff icons above it and the frame border.
  * Buff icons never move, while the player and boss markers do. Selecting only
    blobs whose centroid changes between frames removes every static icon.

Player and boss are then separated by marker size: the boss marker is the larger.
"""
import os
import sys

import cv2
import numpy as np


def marker_blobs(img, bounds):
    x0, y0, x1, y1 = bounds
    roi = img[y0:y1, x0:x1]
    if roi.size == 0:
        return []
    hsv = cv2.cvtColor(roi, cv2.COLOR_BGR2HSV)
    sat = hsv[:, :, 1].astype(np.int32)
    val = hsv[:, :, 2].astype(np.int32)
    mask = ((val > 190) & (sat > 80)).astype(np.uint8) * 255
    n, labels, stats, cents = cv2.connectedComponentsWithStats(mask, 8)
    out = []
    for i in range(1, n):
        area = stats[i, cv2.CC_STAT_AREA]
        if area < 4 or area > 400:
            continue
        out.append((area, float(cents[i][0]), float(cents[i][1])))
    return out


def main(d):
    files = sorted(f for f in os.listdir(d) if f.endswith(".png"))
    first = cv2.imread(os.path.join(d, files[0]))
    h, w = first.shape[:2]
    # The minimap sits in the top-right; inset to drop the buff icons above it and
    # the frame border around it.
    bounds = (int(w * 0.755), int(h * 0.055), int(w * 0.995), int(h * 0.30))
    print(f"minimap bounds={bounds}")

    prev = None
    rows = []
    for f in files:
        img = cv2.imread(os.path.join(d, f))
        if img is None:
            continue
        blobs = marker_blobs(img, bounds)
        moved = []
        if prev is not None:
            for area, x, y in blobs:
                best = min((((x - px) ** 2 + (y - py) ** 2) ** 0.5
                            for _, px, py in prev), default=999)
                if best > 0.8:
                    moved.append((area, x, y))
        else:
            moved = blobs
        moved.sort(reverse=True)
        prev = blobs
        if moved:
            big = moved[0]
            small = moved[-1]
            rows.append((f, big[1], big[2], big[0], small[1], small[2], small[0],
                         len(moved)))
        else:
            n = float("nan")
            rows.append((f, n, n, 0, n, n, 0, 0))

    print("frame,bossx,bossy,bossarea,playerx,playery,playerarea,nmoved")
    for r in rows:
        print(f"{r[0]},{r[1]:.1f},{r[2]:.1f},{r[3]},"
              f"{r[4]:.1f},{r[5]:.1f},{r[6]},{r[7]}")

    xs = [r[1] for r in rows if r[1] == r[1]]
    ys = [r[2] for r in rows if r[2] == r[2]]
    if len(xs) > 20:
        print(f"boss marker x range [{min(xs):.0f},{max(xs):.0f}] "
              f"y range [{min(ys):.0f},{max(ys):.0f}] over {len(xs)} frames")


if __name__ == "__main__":
    main(sys.argv[1])
