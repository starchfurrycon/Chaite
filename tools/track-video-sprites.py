"""Track the player and boss in the Duke Fishron video by pixel analysis.

The vision-model route produced visually impossible traces (a perfectly linear
horizontal ramp with zero vertical motion and zero reversals), so this measures
the footage directly instead. The point of the exercise is the dodge geometry:
the owner's rule and the video's own subtitles both say the escape from a locked
charge is vertical, while the current circuit runs horizontally, and that claim
is worth checking against pixels rather than against a model's expectation.

Frames are read with ffmpeg into a numpy buffer, the player is located as the
tightest cluster of the player's cyan tint, and the result is printed as a
per-frame trace plus aggregate vertical-versus-horizontal travel.

Usage:
  python tools/track-video-sprites.py <video> <start-seconds> <duration> [fps]
"""
import json
import os
import subprocess
import sys

import numpy as np

if len(sys.argv) < 4:
    print(__doc__)
    raise SystemExit(2)

VIDEO = sys.argv[1]
START = float(sys.argv[2])
DURATION = float(sys.argv[3])
FPS = float(sys.argv[4]) if len(sys.argv) > 4 else 30.0

WIDTH, HEIGHT = 960, 540


def read_frames():
    command = [
        "ffmpeg", "-v", "error",
        "-ss", str(START), "-t", str(DURATION), "-i", VIDEO,
        "-vf", "fps=%s,scale=%d:%d" % (FPS, WIDTH, HEIGHT),
        "-f", "rawvideo", "-pix_fmt", "bgr24", "-",
    ]
    raw = subprocess.run(command, capture_output=True, check=True).stdout
    frame_bytes = WIDTH * HEIGHT * 3
    count = len(raw) // frame_bytes
    frames = np.frombuffer(raw[:count * frame_bytes], dtype=np.uint8)
    return frames.reshape(count, HEIGHT, WIDTH, 3)


print("decoding %s  t=%.1f..%.1f  fps=%s" % (VIDEO, START, START + DURATION, FPS))
frames = read_frames()
print("frames: %d" % len(frames))
if len(frames) == 0:
    raise SystemExit(1)

# The player sprite carries a cyan/teal tint that the ocean background does not
# reach: the background water sits well below these thresholds in red and blue
# difference. Tune once against the first frames and keep it fixed.
b = frames[:, :, :, 0].astype(np.int16)
g = frames[:, :, :, 1].astype(np.int16)
r = frames[:, :, :, 2].astype(np.int16)
mask = (b > 190) & (g > 190) & (r < 170) & ((b - r) > 50) & ((g - r) > 50)

print("mask pixels in first frame: %d" % mask[0].sum())
if mask[0].sum() == 0:
    print("no player-coloured pixels found; thresholds need tuning")
    raise SystemExit(1)

positions = []
for index in range(len(frames)):
    ys, xs = np.nonzero(mask[index])
    if len(xs) == 0:
        positions.append(None)
        continue
    # The player is one compact cluster; take the median, which is robust to a
    # few stray pixels of the same tint elsewhere in the frame.
    positions.append((float(np.median(xs)), float(np.median(ys)), int(len(xs))))

valid = [(i, p) for i, p in enumerate(positions) if p is not None]
print("frames with a player candidate: %d / %d" % (len(valid), len(frames)))

print()
print("  frame  t(s)     x      y    pixels  dx     dy")
previous = None
for index, position in enumerate(positions):
    t = START + index / FPS
    if position is None:
        print("  %5d  %6.2f      --     --       -      -      -" % (index, t))
        continue
    x, y, count = position
    if previous is None:
        dx = dy = 0.0
    else:
        dx, dy = x - previous[0], y - previous[1]
    previous = (x, y)
    print("  %5d  %6.2f  %6.1f %6.1f  %6d  %+6.1f %+6.1f"
          % (index, t, x, y, count, dx, dy))

coordinates = np.array([[p[0], p[1]] for _, p in valid])
if len(coordinates) > 1:
    deltas = np.diff(coordinates, axis=0)
    vertical = float(np.abs(deltas[:, 1]).sum())
    horizontal = float(np.abs(deltas[:, 0]).sum())
    reversals = int(np.sum(np.diff(np.sign(deltas[:, 1])) != 0))
    print()
    print("total |dx| = %.1f px   total |dy| = %.1f px" % (horizontal, vertical))
    if horizontal + vertical > 0:
        print("vertical share of travel = %.1f%%"
              % (100.0 * vertical / (horizontal + vertical)))
    print("vertical direction reversals: %d" % reversals)
    print("x range %.1f..%.1f   y range %.1f..%.1f"
          % (coordinates[:, 0].min(), coordinates[:, 0].max(),
             coordinates[:, 1].min(), coordinates[:, 1].max()))

    # Landing cadence. Terraria runs at 60 ticks per second, so one frame at the
    # usual 30 fps is two ticks. A surface is a frame where the player's vertical
    # motion stops and then reverses upward, which is what a touchdown looks like
    # once the sprite is on a platform.
    print()
    bottoms = []
    for index in range(1, len(coordinates) - 1):
        before = coordinates[index][1] - coordinates[index - 1][1]
        after = coordinates[index + 1][1] - coordinates[index][1]
        # Falling (or level) into the frame, then rising out of it: a floor hit.
        if before >= 0 and after < 0:
            bottoms.append(index)
    if bottoms:
        gaps = np.diff(bottoms)
        ticks = gaps * (60.0 / FPS)
        print("surface contacts detected: %d" % len(bottoms))
        print("gaps between contacts (video frames): median %.0f  max %.0f"
              % (np.median(gaps), gaps.max()))
        print("gaps in native ticks (60/s): median %.0f  max %.0f"
              % (np.median(ticks), ticks.max()))
        print("frames-per-contact sequence: %s" % gaps[:30].tolist())
    else:
        print("no surface contacts detected with this rule")

