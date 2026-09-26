"""Native lock-time geometry analysis for Duke Fishron charges.

Reads a native observation stream and, for every tick where the boss enters
charge state 1 (the one tick on which native code computes and freezes the
charge velocity), records the player/boss geometry at that instant and then
measures what the player did during the charge.

Run:  python tools/analyze-lock-geometry.py <artifacts/game-probe-X>
"""
import json
import math
import os
import sys
from collections import Counter

ARGS = sys.argv[1:]
if not ARGS:
    print("usage: analyze-lock-geometry.py <run-dir>")
    raise SystemExit(2)
RUN = ARGS[0]

rows = []
with open(os.path.join(RUN, "boss-observations.jsonl"), encoding="utf-8") as handle:
    for line in handle:
        line = line.strip()
        if line:
            rows.append(json.loads(line))
rows.sort(key=lambda r: r["tick"])

# Charge states that hold a locked velocity without ever rewriting it.
CHARGE_STATES = (1, 6, 11)


def boss_of(row):
    for npc in row.get("npcs", []):
        if npc.get("boss"):
            return npc
    return None


locks = []
previous_state = None
for index, row in enumerate(rows):
    npc = boss_of(row)
    if npc is None:
        continue
    state = int(npc["ai"][0])
    # The lock is the transition into a charge state: native computes
    # velocity = normalize(player.Center - center) * chargeSpeed on that tick.
    if state in CHARGE_STATES and previous_state != state:
        player = row["player"]
        boss_pos = npc["position"]
        boss_center = (boss_pos["x"] + npc["width"] / 2.0,
                       boss_pos["y"] + npc["height"] / 2.0)
        player_center = (player["position"]["x"] + player["width"] / 2.0,
                         player["position"]["y"] + player["height"] / 2.0)
        gap = (player_center[0] - boss_center[0],
               player_center[1] - boss_center[1])
        # The locked charge direction is exactly the normalized gap on this tick.
        norm = math.hypot(gap[0], gap[1])
        aim = (gap[0] / norm, gap[1] / norm) if norm else (0.0, 0.0)
        speed = math.hypot(npc["velocity"]["x"], npc["velocity"]["y"])
        locks.append({
            "index": index,
            "tick": row["tick"],
            "state": state,
            "distance": norm,
            "boss_above": -gap[1],
            "aim_x": aim[0],
            "aim_y": aim[1],
            "charge_speed": speed,
            "player_vx": player["velocity"]["x"],
            "player_vy": player["velocity"]["y"],
        })
    previous_state = state

print("run: %s" % RUN)
print("rows: %d   locks (charge entries): %d" % (len(rows), len(locks)))
if not locks:
    raise SystemExit(0)


def speed_needed(lock, margin):
    """Perpendicular speed needed to clear a 150x100 hitbox plus margin during
    the charge's approach, assuming the perpendicular offset stays constant."""
    return margin


print()
print("  tick  st  dist   bossAbove  aim(dx,dy)        chargeSpd  playerV")
for lock in locks[:26]:
    print("  %5d %3d %6.1f %9.1f  (%5.2f,%5.2f) %8.2f   (%5.1f,%5.1f)" % (
        lock["tick"], lock["state"], lock["distance"], lock["boss_above"],
        lock["aim_x"], lock["aim_y"], lock["charge_speed"],
        lock["player_vx"], lock["player_vy"]))

distances = sorted(lock["distance"] for lock in locks)
above = [lock["boss_above"] for lock in locks]
print()
print("distance at lock: min %.1f  median %.1f  max %.1f" % (
    distances[0], distances[len(distances) // 2], distances[-1]))
print("boss above player at lock: min %.1f  median %.1f  max %.1f" % (
    min(above), sorted(above)[len(above) // 2], max(above)))
print("locked charge speed values: %s" % dict(Counter(
    round(lock["charge_speed"], 1) for lock in locks).most_common(6)))
print("charge states seen at lock: %s" % dict(Counter(
    lock["state"] for lock in locks)))

# The owner's rule: dodge along the charge normal, upward when the boss is
# above and downward when below. The needed normal component is 17*|aim_y|
# horizontally and 17*|aim_x| vertically -- both can be far above the player's
# 4.71 run cap, which is why the dodge has to start at the lock and not after.
print()
print("required perpendicular component (owner's rule), strongest 12 locks:")
scored = []
for lock in locks:
    scored.append((lock["charge_speed"] * abs(lock["aim_y"]),
                   lock["charge_speed"] * abs(lock["aim_x"]), lock))
scored.sort(key=lambda item: -max(item[0], item[1]))
print("  tick  st  dist  needHoriz  needVert   (player caps 4.71 horiz)")
for horizontal, vertical, lock in scored[:12]:
    print("  %5d %3d %6.1f %10.2f %9.2f" % (
        lock["tick"], lock["state"], lock["distance"], horizontal, vertical))
