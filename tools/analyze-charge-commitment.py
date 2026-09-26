"""Measure whether the commanded horizontal flips during a locked charge.

Duke Fishron computes and freezes its charge velocity on the single tick it
enters state 1 (NPC.cs AI_069, num28==1). After that states 1/6/11 never
rewrite velocity. This script follows each locked charge and reports what the
player's applied horizontal input did while the boss was still travelling
toward the locked line, which is the window the owner's rule is about.

Run:  python tools/analyze-charge-commitment.py <artifacts/game-probe-X>
"""
import json
import math
import os
import sys
from collections import Counter

if len(sys.argv) < 2:
    print("usage: analyze-charge-commitment.py <run-dir>")
    raise SystemExit(2)
RUN = sys.argv[1]

rows = []
with open(os.path.join(RUN, "boss-observations.jsonl"), encoding="utf-8") as handle:
    for line in handle:
        line = line.strip()
        if line:
            rows.append(json.loads(line))
rows.sort(key=lambda r: r["tick"])

CHARGE_STATES = (1, 6, 11)


def boss_of(row):
    for npc in row.get("npcs", []):
        if npc.get("boss"):
            return npc
    return None


def center_of(entity):
    return (entity["position"]["x"] + entity["width"] / 2.0,
            entity["position"]["y"] + entity["height"] / 2.0)


# Group consecutive charge-state ticks into episodes and find the lock tick.
episodes = []
current = None
for row in rows:
    npc = boss_of(row)
    if npc is None:
        current = None
        continue
    state = int(npc["ai"][0])
    if state in CHARGE_STATES:
        if current is None:
            current = {"lock": row, "rows": []}
            episodes.append(current)
        current["rows"].append(row)
    else:
        current = None

print("run: %s" % RUN)
print("charge episodes: %d" % len(episodes))
if not episodes:
    raise SystemExit(0)

flip_histogram = Counter()
summary = []
for episode in episodes:
    lock = episode["lock"]
    lock_npc = boss_of(lock)
    lock_player = lock["player"]
    lock_center = center_of(lock_npc)
    player_center = center_of(lock_player)
    gap = (player_center[0] - lock_center[0], player_center[1] - lock_center[1])
    distance = math.hypot(*gap)
    aim = (gap[0] / distance, gap[1] / distance) if distance else (0.0, 0.0)

    # The perpendicular to the locked charge line, expressed as the sign of the
    # horizontal component the player should hold to increase clearance.
    # normal = (-aim_y, aim_x) or (aim_y, -aim_x); pick the one that moves the
    # player away from the boss axis.
    normal_a = (-aim[1], aim[0])
    normal_b = (aim[1], -aim[0])
    normal = normal_a if (normal_a[0] * gap[0] + normal_a[1] * gap[1]) >= 0 else normal_b
    want_horizontal = 0 if abs(normal[0]) < 0.2 else (1 if normal[0] > 0 else -1)

    applied = []
    for row in episode["rows"]:
        player = row["player"]
        horizontal = 0
        if player.get("controlLeft"):
            horizontal -= 1
        if player.get("controlRight"):
            horizontal += 1
        applied.append(horizontal)

    # Count sign changes while the boss is still closing on the locked line.
    changes = 0
    previous = None
    for value in applied:
        if value != 0 and previous is not None and value != previous:
            changes += 1
        if value != 0:
            previous = value
    flip_histogram[changes] += 1

    # Fraction of the episode spent holding the perpendicular direction.
    if want_horizontal == 0:
        held = sum(1 for value in applied if value == 0) / max(1, len(applied))
    else:
        held = sum(1 for value in applied if value == want_horizontal) / max(1, len(applied))
    summary.append({
        "tick": lock["tick"],
        "state": int(lock_npc["ai"][0]),
        "distance": distance,
        "aim": aim,
        "want_horizontal": want_horizontal,
        "applied": applied,
        "perp_held": held,
        "changes": changes,
        "length": len(applied),
    })

print()
print("  tick  st  dist   aim(dx,dy)      wantH  perpHeld  signChanges  len")
for item in summary[:24]:
    print("  %5d %3d %6.1f  (%5.2f,%5.2f)  %5d  %8.2f  %11d  %4d" % (
        item["tick"], item["state"], item["distance"], item["aim"][0],
        item["aim"][1], item["want_horizontal"], item["perp_held"],
        item["changes"], item["length"]))

print()
print("sign changes in applied horizontal during a charge: %s"
      % dict(sorted(flip_histogram.items())))
held_values = sorted(item["perp_held"] for item in summary)
print("perpendicular direction held: min %.2f  median %.2f  max %.2f" % (
    held_values[0], held_values[len(held_values) // 2], held_values[-1]))
print("episodes holding it for less than half the charge: %d of %d"
      % (sum(1 for item in summary if item["perp_held"] < 0.5), len(summary)))
print("episodes with at least one mid-charge sign change: %d of %d"
      % (sum(1 for item in summary if item["changes"] > 0), len(summary)))
