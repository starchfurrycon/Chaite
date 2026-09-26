"""Measure charge episodes as telegraph + travel, and what the player did.

Duke Fishron locks its charge velocity on the single tick it enters state 1
(NPC.cs AI_069, num28==1: velocity = normalize(player.Center - center) * speed).
State 1 is a short wind-up (ai[2] >= num6, 28 ticks in expert); the travel
itself happens in the block that follows, which steers velocity toward the
player each tick. This script groups the whole episode from the lock until the
boss leaves the charge-and-travel sequence, and reports the player's applied
horizontal against the charge normal.

Run:  python tools/analyze-charge-episode.py <artifacts/game-probe-X>
"""
import json
import math
import os
import sys
from collections import Counter

if len(sys.argv) < 2:
    print("usage: analyze-charge-episode.py <run-dir>")
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


# An episode opens on a charge-state tick and stays open through the travel that
# follows. Travel is identified by the boss actually moving fast toward the
# player, so the episode closes when speed falls back to cruise.
episodes = []
current = None
for index, row in enumerate(rows[::-1]):
    pass
previous_state = None
for row in rows:
    npc = boss_of(row)
    if npc is None:
        current = None
        previous_state = None
        continue
    state = int(npc["ai"][0])
    if state in CHARGE_STATES and previous_state not in CHARGE_STATES:
        current = {"lock": row, "rows": [], "travel": []}
        episodes.append(current)
    if current is not None:
        current["rows"].append(row)
        speed = math.hypot(npc["velocity"]["x"], npc["velocity"]["y"])
        if state not in CHARGE_STATES and speed > 12.0:
            current["travel"].append(row)
        elif state not in CHARGE_STATES and current["travel"]:
            current = None
    previous_state = state

print("run: %s" % RUN)
print("charge episodes: %d" % len(episodes))
if not episodes:
    raise SystemExit(0)

held_fractions = []
details = []
for episode in episodes:
    lock = episode["lock"]
    lock_npc = boss_of(lock)
    lock_center = center_of(lock_npc)
    player_center = center_of(lock["player"])
    gap = (player_center[0] - lock_center[0], player_center[1] - lock_center[1])
    distance = math.hypot(*gap)
    aim = (gap[0] / distance, gap[1] / distance) if distance else (0.0, 0.0)

    # The dodge normal: perpendicular to the locked charge line, chosen so it
    # increases the player's clearance from that line.
    normal_a = (-aim[1], aim[0])
    normal_b = (aim[1], -aim[0])
    normal = normal_a if (normal_a[0] * gap[0] + normal_a[1] * gap[1]) >= 0 else normal_b
    want_horizontal = 0 if abs(normal[0]) < 0.2 else (1 if normal[0] > 0 else -1)
    # The owner's rule also wants vertical motion along the normal.
    want_vertical = 0 if abs(normal[1]) < 0.2 else (-1 if normal[1] < 0 else 1)

    applied = []
    applied_vertical = []
    for row in episode["rows"]:
        player = row["player"]
        horizontal = (1 if player.get("controlRight") else 0) - \
                     (1 if player.get("controlLeft") else 0)
        vertical = -1 if player.get("controlJump") else (
            1 if player.get("controlDown") else 0)
        applied.append(horizontal)
        applied_vertical.append(vertical)

    if want_horizontal == 0:
        held = sum(1 for value in applied if value == 0) / max(1, len(applied))
    else:
        held = sum(1 for value in applied if value == want_horizontal) / max(1, len(applied))
    opposite = sum(1 for value in applied
                   if value != 0 and want_horizontal != 0
                   and value == -want_horizontal) / max(1, len(applied))
    vertical_ok = sum(1 for value in applied_vertical
                      if value == want_vertical) / max(1, len(applied_vertical))
    held_fractions.append(held)
    details.append({
        "tick": lock["tick"], "distance": distance, "aim": aim,
        "want_horizontal": want_horizontal, "want_vertical": want_vertical,
        "held": held, "opposite": opposite, "vertical_ok": vertical_ok,
        "telegraph": sum(1 for r in episode["rows"]
                         if int(boss_of(r)["ai"][0]) in CHARGE_STATES),
        "travel": len(episode["travel"]), "length": len(episode["rows"]),
    })

print()
print("  tick  dist   aim(dx,dy)      wantH wantV  perpHeld  opposite  vertOK  tele  trav")
for item in details[:26]:
    print("  %5d %6.1f  (%5.2f,%5.2f)  %5d %5d  %8.2f  %8.2f  %6.2f  %4d  %4d" % (
        item["tick"], item["distance"], item["aim"][0], item["aim"][1],
        item["want_horizontal"], item["want_vertical"], item["held"],
        item["opposite"], item["vertical_ok"], item["telegraph"], item["travel"]))

print()
ordered = sorted(held_fractions)
print("perpendicular held: min %.2f  median %.2f  max %.2f" % (
    ordered[0], ordered[len(ordered) // 2], ordered[-1]))
print("episodes holding the normal for <50%%: %d of %d" % (
    sum(1 for value in held_fractions if value < 0.5), len(held_fractions)))
print("median fraction spent moving OPPOSITE the normal: %.2f" % (
    sorted(item["opposite"] for item in details)[len(details) // 2]))
print("median fraction with correct vertical: %.2f" % (
    sorted(item["vertical_ok"] for item in details)[len(details) // 2]))
print("telegraph lengths: %s" % dict(Counter(
    item["telegraph"] for item in details).most_common(6)))
print("travel lengths: %s" % dict(Counter(
    item["travel"] for item in details).most_common(6)))
