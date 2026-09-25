"""Recover the fight geometry from video frames using difference imaging.

Colour segmentation failed earlier because the HUD and the world palette both
produce large blobs. Two structural facts are much more reliable:

  * The Terraria camera locks the PLAYER near a fixed screen position, so the
    player is nearly stationary on screen while the world scrolls.
  * The BOSS moves fast across the screen and is large, so it dominates
    frame-to-frame change away from the HUD.

So the boss is the largest strongly-changing blob outside the HUD bands, and the
player is the persistent bright small object near the screen centre. Reporting the
boss relative to the player then gives the geometry the controller needs.
"""
import os
import sys

import cv2
import numpy as np

# Terraria HUD bands: hearts/mana top-left, hotbar bottom-centre, minimap
# top-right, buffs top-right. Excluded from all detection.
def hud_mask(h, w):
    m = np.zeros((h, w), np.uint8)
    m[:170, :700] = 1
    m[:220, w - 420:] = 1
    m[h - 140:, w // 2 - 460: w // 2 + 460] = 1
    m[h - 120:, :260] = 1
    return m


def biggest(mask, min_area, exclude):
    mask = mask.copy()
    mask[exclude > 0] = 0
    n, labels, stats, cents = cv2.connectedComponentsWithStats(mask, 8)
    best = None
    for i in range(1, n):
        area = stats[i, cv2.CC_STAT_AREA]
        if area < min_area:
            continue
        if best is None or area > best[0]:
            best = (area, stats[i], cents[i])
    return best


def track(frames_dir, out_csv):
    files = sorted(f for f in os.listdir(frames_dir) if f.endswith(".png"))
    prev = None
    rows = ["frame,player_dx,player_dy,boss_dx,boss_dy,boss_area,changed"]
    for f in files:
        img = cv2.imread(os.path.join(frames_dir, f))
        if img is None:
            continue
        h, w = img.shape[:2]
        ex = hud_mask(h, w)
        hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
        sat = hsv[:, :, 1].astype(np.int32)
        val = hsv[:, :, 2].astype(np.int32)

        if prev is not None:
            diff = cv2.absdiff(img, prev).max(axis=2)
            changed = (diff > 36).astype(np.uint8) * 255
            changed = cv2.morphologyEx(changed, cv2.MORPH_CLOSE,
                                       np.ones((7, 7), np.uint8))
            boss = biggest(changed, 2500, ex)
        else:
            changed = np.zeros((h, w), np.uint8)
            boss = None

        # Player: bright and saturated, central region only.
        pm = ((val >= 210) & (sat >= 70)).astype(np.uint8) * 255
        band = np.ones((h, w), np.uint8) * 255
        band[: h // 5] = 0
        band[h * 4 // 5:] = 0
        band[:, : w // 4] = 0
        band[:, w * 3 // 4:] = 0
        pm = cv2.bitwise_and(pm, band)
        player = biggest(pm, 100, ex)

        def rel(b):
            if b is None:
                return (float("nan"),) * 2
            x = b[1][0] + b[1][2] / 2.0 - w / 2.0
            y = b[1][1] + b[1][3] / 2.0 - h / 2.0
            return (x, y)

        px, py = rel(player)
        bx, by = rel(boss)
        rows.append(f"{f},{px:.1f},{py:.1f},{bx:.1f},{by:.1f},"
                    f"{0 if boss is None else boss[0]},{int((changed > 0).sum())}")
        prev = img

    with open(out_csv, "w", encoding="utf-8") as f:
        f.write("\n".join(rows))
    print("\n".join(rows[:6]))
    print(f"... {len(rows) - 1} frames -> {out_csv}")


if __name__ == "__main__":
    track(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "track.csv")
