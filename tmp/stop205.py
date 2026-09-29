"""Round 205b: the REAL sudden stop = |vx| collapses between two consecutive ticks while
the plan keeps a non-zero horizontal input.  The earlier "|vx|<0.5" metric also caught
legitimate direction reversals, which cross zero on the way through.
"""
import io, json, collections

def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def run(tag):
    obs = load(tag)
    rows = []
    for o in obs:
        pl = o.get('player') or {}
        v = (pl.get('velocity') or {}).get('x')
        rows.append((o.get('tick'), v, o.get('plan') or {}, pl.get('wingTime'),
                     pl.get('position') or {}))
    rows = [r for r in rows if r[2] and r[1] is not None]
    collapse = []
    for i in range(1, len(rows)):
        t0, v0, p0, w0, pos0 = rows[i - 1]
        t1, v1, p1, w1, pos1 = rows[i]
        if t1 != t0 + 1:
            continue
        h = p1.get('horizontal')
        if h in (None, 0):
            continue
        if abs(v0) >= 3.0 and abs(v1) < 0.5:
            collapse.append((t1, v0, v1, h, w0, w1, pos1.get('x'), pos1.get('y')))
    return rows, collapse

print('=== true sudden stops: |vx| >= 3 on one tick, < 0.5 on the next, input still non-zero ===')
allc = {}
for tag in ('kI4-s700', 'kI4-s750', 'kI4-s800', 'kI4-s900', 'kJ4-s1100',
            'kA3-s300', 'kA3-s550', 'kJ4-s1600', 'kJ4-s2000', 'kA3-s400'):
    rows, c = run(tag)
    allc[tag] = c
    span = rows[-1][0] if rows else 1
    late = [x for x in c if x[0] > span * 0.9]
    print('%-12s plan_rows=%-6d sudden_stops=%-4d  in_last_decile=%-4d' % (
        tag, len(rows), len(c), len(late)))

# when do they occur, as a fraction of the fight?
print()
print('=== every sudden stop, by fight position ===')
for tag in ('kI4-s900', 'kA3-s300'):
    rows, c = run(tag)
    span = rows[-1][0] or 1
    print('%s (span %d):' % (tag, span))
    for x in c:
        print('   t=%-6d (%.0f%%) vx %+7.2f -> %+5.2f  input=%+d  wingT %s->%s  pos=(%.0f,%.0f)' % (
            x[0], 100.0 * x[0] / span, x[1], x[2], x[3], x[4], x[5], x[6], x[7]))
