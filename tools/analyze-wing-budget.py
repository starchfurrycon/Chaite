"""Measure the flight budget against native refill opportunities.

Native refills wingTime to wingTimeMax at Player.cs:26992 when
`(velocity.Y == 0 || sliding) && releaseJump` or `(autoJump && justJumped)`,
and justJumped is set at Player.cs:20867 on the landing tick. Since the probe
records autoJump, justJumped, sliding, wingTime and wingTimeMax, this script
measures how well the circuit lands -- which is the only way the budget comes
back -- and how much of the fight is flown on an empty budget.

Run:  python tools/analyze-wing-budget.py <artifacts/game-probe-X>
"""
import json
import os
import sys
from collections import Counter

if len(sys.argv) < 2:
    print("usage: analyze-wing-budget.py <run-dir>")
    raise SystemExit(2)
RUN = sys.argv[1]

rows = []
with open(os.path.join(RUN, "boss-observations.jsonl"), encoding="utf-8") as handle:
    for line in handle:
        line = line.strip()
        if line:
            rows.append(json.loads(line))
rows.sort(key=lambda r: r["tick"])

# Only look at the fight itself, not the pre-takeover idle.
rows = [r for r in rows if r.get("plan")]
if not rows:
    print("no planned rows in %s" % RUN)
    raise SystemExit(0)

print("run: %s" % RUN)
print("planned rows: %d  (ticks %d..%d)"
      % (len(rows), rows[0]["tick"], rows[-1]["tick"]))

wing_max = Counter(r["player"]["wingTimeMax"] for r in rows)
print("wingTimeMax values: %s" % dict(wing_max))

# A refill is the tick where wingTime returns to max after having been below it.
refills = 0
refill_ticks = []
previous = None
for row in rows:
    player = row["player"]
    current = player["wingTime"]
    if previous is not None and current == player["wingTimeMax"] and previous < current:
        refills += 1
        refill_ticks.append(row["tick"])
    previous = current

empty = sum(1 for r in rows if r["player"]["wingTime"] <= 0)
landing = sum(1 for r in rows if r["player"].get("justJumped"))
sliding = sum(1 for r in rows if r["player"].get("sliding"))
zero_vy = sum(1 for r in rows if r["player"]["velocity"]["y"] == 0)

print()
print("refills (wingTime returned to max): %d" % refills)
print("ticks with wingTime <= 0          : %d / %d  (%.1f%%)"
      % (empty, len(rows), 100.0 * empty / len(rows)))
print("ticks with justJumped (landing)   : %d  (%.1f%%)"
      % (landing, 100.0 * landing / len(rows)))
print("ticks with sliding                : %d" % sliding)
print("ticks with velocity.Y == 0        : %d" % zero_vy)

# Airtime: consecutive runs of rows that are neither a landing nor sliding nor
# velocity.Y == 0 -- i.e. genuinely airborne and not touching support.
def grounded(row):
    player = row["player"]
    return bool(player.get("justJumped") or player.get("sliding")
                or player["velocity"]["y"] == 0)

runs = []
length = 0
for row in rows:
    if grounded(row):
        if length:
            runs.append(length)
        length = 0
    else:
        length += 1
if length:
    runs.append(length)
runs.sort()
print()
if runs:
    print("airborne runs: %d   median %d   p90 %d   max %d"
          % (len(runs), runs[len(runs) // 2], runs[int(len(runs) * 0.9)], runs[-1]))
    budget = max(wing_max)
    over = sum(1 for r in runs if r > budget)
    print("airborne runs longer than one full budget (%d): %d  (%.1f%%)"
          % (budget, over, 100.0 * over / len(runs)))
    print("longest 12 airborne runs: %s" % runs[-12:])
else:
    print("no airborne runs recorded")

print()
print("wingTime histogram (top): %s" % dict(Counter(
    int(r["player"]["wingTime"] // 30 * 30) for r in rows).most_common(8)))
