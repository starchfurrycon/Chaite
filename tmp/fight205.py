"""Round 205d: fighting-only statistics. Exclude ticks after the boss dies, then ask
whether the controller's horizontal direction reversal rate rises late in the fight.
"""
import io, json

def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def fighting_rows(tag):
    obs = load(tag)
    rows = []
    for o in obs:
        npcs = o.get('npcs') or []
        if npcs and (npcs[0].get('life') or 0) <= 0:
            break
        pl = o.get('player') or {}
        v = (pl.get('velocity') or {}).get('x')
        p = o.get('plan') or {}
        if not p or v is None:
            continue
        rows.append((o.get('tick'), v, p.get('horizontal')))
    return rows

print('=== fighting-only, by decile of the FIGHTING span ===')
for tag in ('kI4-s900', 'kA3-s300', 'kI4-s800', 'kJ4-s2000', 'kJ4-s1600'):
    rows = fighting_rows(tag)
    if len(rows) < 50:
        print('%-12s too few rows' % tag); continue
    span = rows[-1][0] or 1
    print('%-12s fighting_rows=%d span=%d' % (tag, len(rows), span))
    for d in range(10):
        lo, hi = span * d / 10.0, span * (d + 1) / 10.0
        seg = [r for r in rows if lo <= r[0] < hi]
        if len(seg) < 20:
            continue
        vs = [r[1] for r in seg]
        mean = sum(abs(v) for v in vs) / len(vs)
        flips = sum(1 for i in range(1, len(seg)) if vs[i] * vs[i - 1] < 0)
        slow = sum(1 for v in vs if abs(v) < 2.0)
        print('   %3d-%3d%%  n=%-5d |vx|mean=%-6.2f flips/100t=%-6.2f slow(<2)/100t=%-6.1f' % (
            d * 10, (d + 1) * 10, len(seg), mean, 100.0 * flips / len(seg),
            100.0 * slow / len(seg)))
    print()
