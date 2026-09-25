"""Report the dominant colours of a video frame so the player and the boss can
be detected by colour rather than by eye. The environment has no vision-capable
model, so the trajectory has to be recovered from pixels."""
import sys
import cv2
import numpy as np


def main(path):
    img = cv2.imread(path)
    if img is None:
        print("cannot read", path)
        return
    h, w = img.shape[:2]
    print(f"{path}: {w}x{h}")
    # Coarse palette: quantise and count.
    small = cv2.resize(img, (w // 4, h // 4), interpolation=cv2.INTER_NEAREST)
    q = (small // 24) * 24
    flat = q.reshape(-1, 3)
    uniq, counts = np.unique(flat, axis=0, return_counts=True)
    order = np.argsort(-counts)[:18]
    print("  dominant BGR (share):")
    total = flat.shape[0]
    for i in order:
        b, g, r = uniq[i]
        print(f"    BGR=({b:3d},{g:3d},{r:3d})  {100.0*counts[i]/total:5.2f}%")
    # Bright, saturated pixels are candidates for the player (the character is
    # usually the brightest saturated thing on screen).
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    sat = hsv[:, :, 1].astype(np.int32)
    val = hsv[:, :, 2].astype(np.int32)
    bright = (val > 200) & (sat > 60)
    print(f"  bright+saturated pixels: {bright.sum()}")
    if bright.sum():
        ys, xs = np.nonzero(bright)
        print(f"    bbox x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}]"
              f" centroid ({xs.mean():.0f},{ys.mean():.0f})")
    # Purple family, for the boss.
    hsv_h = hsv[:, :, 0].astype(np.int32)
    purple = (hsv_h > 125) & (hsv_h < 165) & (sat > 70) & (val > 70)
    print(f"  purple-ish pixels: {purple.sum()}")
    if purple.sum():
        ys, xs = np.nonzero(purple)
        print(f"    bbox x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}]"
              f" centroid ({xs.mean():.0f},{ys.mean():.0f})")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        main(p)
