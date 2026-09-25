"""Recover the boss and player screen positions from a Bilibili Terraria no-hit
video so the movement pattern can be read as numbers instead of by eye.

The environment has no vision-capable model, so nothing here may depend on
looking at a picture. Two robust cues are used instead:

  * The boss (Duke Fishron) is a large purple/violet sprite. Selecting the purple
    hue band and taking the LARGEST connected component gives its blob without
    any per-frame tuning.
  * The player is the brightest, most saturated small object near the middle of
    the screen, and the Terraria camera keeps the player near the centre, so the
    player is found as the largest bright+saturated component close to centre.

Everything is reported relative to the screen centre, which is the quantity that
matters: the pattern is about where the boss is relative to the player.
"""
import sys
import cv2
import numpy as np


def largest_component(mask, min_area):
    n, labels, stats, cents = cv2.connectedComponentsWithStats(mask, 8)
    best = None
    for i in range(1, n):
        area = stats[i, cv2.CC_STAT_AREA]
        if area < min_area:
            continue
        if best is None or area > best[0]:
            best = (area, stats[i], cents[i])
    return best


def analyse(path):
    img = cv2.imread(path)
    if img is None:
        return None
    h, w = img.shape[:2]
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    hue = hsv[:, :, 0].astype(np.int32)
    sat = hsv[:, :, 1].astype(np.int32)
    val = hsv[:, :, 2].astype(np.int32)

    # Boss: violet band. Duke Fishron is drawn in lilac/violet tones.
    boss_mask = ((hue >= 128) & (hue <= 168) & (sat >= 60) & (val >= 80))
    boss_mask = boss_mask.astype(np.uint8)
    boss_mask = cv2.morphologyEx(boss_mask, cv2.MORPH_CLOSE,
                                 np.ones((9, 9), np.uint8))
    boss = largest_component(boss_mask, 900)

    # Player: bright and saturated, restricted to the middle third of the
    # screen where the camera holds the character.
    player_mask = ((val >= 215) & (sat >= 70)).astype(np.uint8)
    band = np.zeros_like(player_mask)
    band[h // 4: h, w // 4: 3 * w // 4] = 1
    player_mask &= band
    player = largest_component(player_mask, 120)

    def rel(c):
        if c is None:
            return None
        return (c[1][0] + c[1][2] / 2.0 - w / 2.0,
                c[1][1] + c[1][3] / 2.0 - h / 2.0, c[0])

    return {"path": path, "size": (w, h), "boss": rel(boss),
            "player": rel(player)}


def main(glob_dir, stride):
    import glob
    import os
    files = sorted(glob.glob(os.path.join(glob_dir, "*.png")))
    if stride > 1:
        files = files[::stride]
    print("file,boss_dx,boss_dy,boss_area,player_dx,player_dy,player_area")
    for f in files:
        r = analyse(f)
        if r is None:
            continue
        b = r["boss"] or (float("nan"),) * 3
        p = r["player"] or (float("nan"),) * 3
        print(f"{os.path.basename(f)},{b[0]:.1f},{b[1]:.1f},{b[2]},"
              f"{p[0]:.1f},{p[1]:.1f},{p[2]}")


if __name__ == "__main__":
    main(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 1)
