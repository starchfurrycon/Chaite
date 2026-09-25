"""Track the player via the Terraria minimap, which the game draws for us.

Screen-space detection failed because the camera follows the player and the HUD
swamps naive colour masks. The minimap sidesteps both: it plots the player as a
small bright marker and the boss as a large one, in WORLD coordinates, independent
of camera scroll. Reading the markers out of the minimap corner therefore gives the
relative geometry directly, which is what the movement pattern is made of.

The minimap is in the top-right. Player and boss markers are high-value, saturated
pixels; they are separated by blob size, since the boss marker is much larger.
"""
import os
import sys

import cv2
import numpy as np


def frame_report(path):
    img = cv2.imread(path)
    if img is None:
        return None
    h, w = img.shape[:2]
    # Minimap region: top-right corner, generously bounded.
    x0, y0 = int(w * 0.72), 0
    x1, y1 = w, int(h * 0.34)
    roi = img[y0:y1, x0:x1]
    hsv = cv2.cvtColor(roi, cv2.COLOR_BGR2HSV)
    sat = hsv[:, :, 1].astype(np.int32)
    val = hsv[:, :, 2].astype(np.int32)
    # Bright markers on the dark translucent minimap.
    mask = ((val > 175) & (sat > 60)).astype(np.uint8) * 255
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN,
                            np.ones((3, 3), np.uint8))
    n, labels, stats, cents = cv2.connectedComponentsWithStats(mask, 8)
    blobs = []
    for i in range(1, n):
        area = stats[i, cv2.CC_STAT_AREA]
        if area < 4:
            continue
        blobs.append((area, cents[i][0], cents[i][1]))
    blobs.sort(reverse=True)
    return blobs, (x0, y0, x1, y1)


if __name__ == "__main__":
    d = sys.argv[1]
    files = sorted(f for f in os.listdir(d) if f.endswith(".png"))[:6]
    for f in files:
        r = frame_report(os.path.join(d, f))
        if r is None:
            print(f, "unreadable")
            continue
        blobs, box = r
        top = " ".join(f"[a={int(a)} x={x:.0f} y={y:.0f}]"
                       for a, x, y in blobs[:5])
        print(f"{f} roi={box} blobs={len(blobs)} {top}")
