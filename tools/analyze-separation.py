#!/usr/bin/env python3
"""Report the separation metrics that decide the Fishron wing rehearsal.

Why this file exists
--------------------
The 2026-09-25 v2 measurement showed that the win rate of a hand-written
wing script is explained by ONE number: how much distance the script keeps
from the Boss.  The 09-23 baseline kept a median centre distance of 479 px
and spent 2.05% of its combat ticks inside contact range; v2 kept 385 px and
spent 5.37% there, and scored 0/39 against the baseline's 8/39.  Nothing
else differed: wall-hugging, stuck ticks, ascent share and hover/charge tick
shares were all within a point of each other.

So every rehearsal now has to report this table, not just the win rate.
Run it next to tools/analyze-rehearsal.py.

Usage
-----
    python tools/analyze-separation.py <rehearsal-dir-or-obs-file> [...]
    python tools/analyze-separation.py --compare <dir-a> <dir-b>

The observation stream holds every episode of the run (e=0..N-1).  The
episodes file holds N-1 rows, because the probe only writes an episode when
the next one starts.  Pairing hit ticks against episodes is therefore done
here on the observation stream alone.
"""

import glob
import json
import os
import statistics as st
import sys

HOVER = (0, 5, 10)
CHARGE = (1, 6, 11)

# Contact geometry, from artifacts/fishron-ai-spec.md: the Boss body is
# 150x100 and the player is 20x42, so a centre distance under ~110 px is a
# body contact rather than a projectile.
CONTACT = 110.0

# Wing time ceilings are per loadout: FishronWings 180, FairyWings 130.
# tools/GameProbe.cs:2574/:2978/:3033.


def obs_files(target):
    if os.path.isfile(target):
        return [target]
    found = sorted(glob.glob(os.path.join(target, "*.obs.jsonl")))
    if not found:
        raise SystemExit("no *.obs.jsonl under %s" % target)
    return found


def num(row, key):
    try:
        return float(row[key])
    except (KeyError, TypeError, ValueError):
        return None


def load(target):
    rows = []
    for path in obs_files(target):
        with open(path, encoding="utf-8") as handle:
            for line in handle:
                line = line.strip()
                if line:
                    rows.append(json.loads(line))
    return rows


def quant(values, fraction):
    ordered = sorted(values)
    if not ordered:
        return float("nan")
    return ordered[int(fraction * (len(ordered) - 1))]


def hits_of(rows):
    """Indices where the cumulative hit counter rose inside one episode."""
    out = []
    for index in range(1, len(rows)):
        if int(rows[index]["e"]) != int(rows[index - 1]["e"]):
            continue
        if int(rows[index]["hits"]) > int(rows[index - 1]["hits"]):
            out.append(index)
    return out


def summarize(label, target):
    rows = load(target)
    battle = [r for r in rows if int(r["bs"]) >= 0]
    if not battle:
        raise SystemExit("no battle ticks in %s" % target)

    gaps, ascending, stuck, wall = [], 0, 0, 0
    wing = []
    phases = {"hover": 0, "charge": 0, "slow": 0, "other": 0}
    contact = 0
    for row in battle:
        px, py = num(row, "px"), num(row, "py")
        bx, by = num(row, "bx"), num(row, "by")
        if None in (px, py, bx, by):
            continue
        distance = ((px - bx) ** 2 + (py - by) ** 2) ** 0.5
        gaps.append(distance)
        if distance < CONTACT:
            contact += 1
        vy = num(row, "vy")
        if vy is not None and vy < -0.5:
            ascending += 1
        vx = num(row, "vx")
        if vx is not None and abs(vx) < 0.01:
            stuck += 1
        if px <= 700.0:
            wall += 1
        wt = num(row, "wt")
        if wt is not None:
            wing.append(wt)
        state = int(row["bs"])
        if state in HOVER:
            phases["hover"] += 1
        elif state in CHARGE:
            phases["charge"] += 1
        elif state in (2, 3, 7, 8):
            phases["slow"] += 1
        else:
            phases["other"] += 1

    total = len(battle)
    print("%s  (%d battle ticks, %d episodes)" % (label, total, len({int(r["e"]) for r in rows})))
    print("  centre distance to Boss : median %6.0f   p25 %6.0f   p75 %6.0f"
          % (st.median(gaps), quant(gaps, 0.25), quant(gaps, 0.75)))
    print("  contact share (<%d px)  : %6.2f%%" % (CONTACT, 100.0 * contact / total))
    print("  wing time               : median %6.1f   share <50 %5.1f%%   share full %5.1f%%"
          % (st.median(wing) if wing else float("nan"),
             100.0 * sum(1 for w in wing if w < 50) / max(1, len(wing)),
             100.0 * sum(1 for w in wing if w >= 180) / max(1, len(wing))))
    print("  ascent / stuck / wall   : %5.1f%%   %5.2f%%   %5.2f%%"
          % (100.0 * ascending / total, 100.0 * stuck / total, 100.0 * wall / total))
    print("  phase tick share        : hover %5.1f%%  charge %5.1f%%  slow %5.1f%%  other %5.1f%%"
          % tuple(100.0 * phases[k] / total for k in ("hover", "charge", "slow", "other")))

    hits = hits_of(rows)
    if hits:
        per_k = 1000.0 * len(hits) / total
        print("  hits                    : %d  (%.3f per 1k battle ticks)" % (len(hits), per_k))
        charge_hits = [i for i in hits if int(rows[i]["bs"]) in CHARGE]
        at_wall = sum(1 for i in charge_hits if num(rows[i], "px") <= 700.0)
        print("    charge-phase hits     : %d  (%s%% of them pinned at the left wall)"
              % (len(charge_hits), ("%.1f" % (100.0 * at_wall / len(charge_hits))) if charge_hits else "n/a"))
    return {"label": label, "total": total, "gap_median": st.median(gaps),
            "contact_share": 100.0 * contact / total, "hits": len(hits),
            "hits_per_k": 1000.0 * len(hits) / total}


def episodes_summary(target):
    base = target
    if os.path.isfile(target):
        base = os.path.dirname(target)
        name = os.path.basename(target)[:-len(".obs.jsonl")]
    else:
        name = os.path.basename(os.path.normpath(target))
    path = os.path.join(base, name + ".episodes.jsonl")
    if not os.path.isfile(path):
        candidate = sorted(glob.glob(os.path.join(base, "*.episodes.jsonl")))
        if not candidate:
            return None
        path = candidate[0]
    with open(path, encoding="utf-8") as handle:
        rows = [json.loads(line) for line in handle if line.strip()]
    if not rows:
        return None
    wins = sum(1 for r in rows if r.get("win"))
    ticks = [int(r["ticks"]) for r in rows]
    print("  %-24s : win %6.2f%% (%d/%d)   median ticks %6.0f   mean damage %.0f"
          % ("episodes.jsonl", 100.0 * wins / len(rows), wins, len(rows),
             st.median(ticks), st.mean(float(r.get("bossDamage", 0)) for r in rows)))
    return {"win_rate": 100.0 * wins / len(rows), "median_ticks": st.median(ticks)}


def main(argv):
    if len(argv) >= 4 and argv[1] == "--compare":
        result = []
        for target in argv[2:4]:
            result.append(summarize(os.path.basename(os.path.normpath(target)), target))
            episodes_summary(target)
            print()
        a, b = result
        print("delta (second minus first): distance %+.0f px   contact %+.2f pt   hits/1k %+.3f"
              % (b["gap_median"] - a["gap_median"],
                 b["contact_share"] - a["contact_share"],
                 b["hits_per_k"] - a["hits_per_k"]))
        return 0
    if len(argv) < 2:
        print(__doc__)
        return 2
    for target in argv[1:]:
        summarize(os.path.basename(os.path.normpath(target)), target)
        episodes_summary(target)
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
