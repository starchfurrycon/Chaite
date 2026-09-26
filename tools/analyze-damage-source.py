"""Classify what actually damages the player in a native run.

The charge-dodge work assumes body contacts, but npc_contact has measured zero
on the weak-wing route. This script reads the native observation stream and
reports, for each tick where the player's life drops, what hostile bodies were
near the player and what state the boss was in, so the damage source can be
named rather than assumed.

Run:  python tools/analyze-damage-source.py <artifacts/game-probe-X>
"""
import json
import math
import os
import sys
from collections import Counter

if len(sys.argv) < 2:
    print("usage: analyze-damage-source.py <run-dir>")
    raise SystemExit(2)
RUN = sys.argv[1]

rows = []
with open(os.path.join(RUN, "boss-observations.jsonl"), encoding="utf-8") as handle:
    for line in handle:
        line = line.strip()
        if line:
            rows.append(json.loads(line))
rows.sort(key=lambda r: r["tick"])

print("run: %s" % RUN)
print("rows: %d" % len(rows))
if not rows:
    raise SystemExit(0)

sample = rows[0]
print("hostile projectile keys: %s" % (
    sorted(sample["hostileProjectiles"][0].keys())
    if sample.get("hostileProjectiles") else "none in first row"))


def center_of(entity):
    """NPCs carry a nested position; projectiles carry flat x/y."""
    if "position" in entity:
        return (entity["position"]["x"] + entity["width"] / 2.0,
                entity["position"]["y"] + entity["height"] / 2.0)
    return (entity["x"] + entity["width"] / 2.0,
            entity["y"] + entity["height"] / 2.0)


def boss_of(row):
    for npc in row.get("npcs", []):
        if npc.get("boss"):
            return npc
    return None


events = []
previous_life = None
for row in rows:
    player = row["player"]
    life = player["life"]
    if previous_life is not None and life < previous_life:
        events.append((row, previous_life - life))
    previous_life = life

print("life-drop events: %d   total damage: %d"
      % (len(events), sum(delta for _, delta in events)))
print()

# What is near the player at each drop, and what the boss was doing.
sources = Counter()
print("  tick  dmg  bossState  bossDist  immuneTime  nearbyProjectiles (type, dist, vel)")
for row, delta in events[:20]:
    player = row["player"]
    player_center = center_of(player)
    boss = boss_of(row)
    boss_state = int(boss["ai"][0]) if boss else -1
    boss_distance = (math.hypot(player_center[0] - center_of(boss)[0],
                               player_center[1] - center_of(boss)[1])
                     if boss else -1.0)
    near = []
    for projectile in row.get("hostileProjectiles", []):
        distance = math.hypot(player_center[0] - center_of(projectile)[0],
                              player_center[1] - center_of(projectile)[1])
        if distance < 120:
            velocity = projectile.get("velocity") or {
                "x": projectile.get("vx"), "y": projectile.get("vy")}
            near.append((projectile.get("type"), round(distance, 1),
                         (round(velocity["x"], 1), round(velocity["y"], 1))))
            sources[projectile.get("type")] += 1
    print("  %5d %4d  %9d  %8.1f  %10s  %s" % (
        row["tick"], delta, boss_state, boss_distance,
        player.get("immuneTime"), near if near else "-- none within 120 --"))

print()
print("nearby projectile types at damage ticks: %s" % dict(sources))
print("hostile projectile count at those ticks: %s" % dict(Counter(
    row.get("hostileProjectileCount") for row, _ in events)))
print("total hostile projectile count over the run: %s" % dict(Counter(
    row.get("hostileProjectileCount") for row in rows).most_common(8)))
