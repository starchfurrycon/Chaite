"""Round 205e: the decisive test of the owner's rule "keep horizontal speed".

Average |vx| is ~7, so the player is NOT stationary.  But if the controller reverses the
sign every ~30 ticks, the net displacement is tiny and no charge is ever outrun.
Measure: run-lengths of constant sign, and net vs gross horizontal displacement.
"""
import io, json

def load(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def analyse(tag):
    obs = load(tag)
    rows = []
    for o in obs:
        npcs = o.get('npcs') or []
        if npcs and (npcs[0].get('life') or 0) <= 0:
            break
        pl = o.get('player') or {}
        v = (pl.get('velocity') or {}).get('x')
        x = (pl.get('position') or {}).get('x')
        p = o.get('plan') or {}
        if not p or v is None or x is None:
            continue
        rows.append((o.get('tick'), v, x, p.get('horizontal')))
    if len(rows) < 50:
        return None
    # run lengths of constant sign of vx (ignore |vx|<0.05)
    runs = []
    cur = None
    n = 0
    for _, v, _, _ in rows:
        s = 1 if v > 0.05 else (-1 if v < -0.05 else 0)
        if s == 0:
            continue
        if s == cur:
            n += 1
        else:
            if cur is not None:
                runs.append(n)
            cur = s
            n = 1
    if cur is not None:
        runs.append(n)
    # net vs gross displacement
    xs = [r[2] for r in rows]
    net = abs(xs[-1] - xs[0])
    gross = sum(abs(xs[i] - xs[i - 1]) for i in range(1, len(xs)))
    avg = sum(abs(r[1]) for r in rows) / len(rows)
    runs_sorted = sorted(runs)
    med = runs_sorted[len(runs_sorted) // 2] if runs_sorted else 0
    p90 = runs_sorted[int(len(runs_sorted) * 0.9)] if runs_sorted else 0
    print('%-12s ticks=%-6d |vx|avg=%-6.2f  sign-runs=%-5d  median_run=%-4d  p90_run=%-4d  max_run=%-4d  net=%7.0f gross=%8.0f  net/gross=%.3f' % (
        tag, len(rows), avg, len(runs), med, p90, max(runs) if runs else 0, net, gross,
        net / gross if gross else 0))
    return runs

print('=== horizontal sign-run lengths and net/gross displacement (fighting only) ===')
allruns = {}
for tag in ('kI4-s700', 'kI4-s750', 'kI4-s800', 'kI4-s850', 'kI4-s900', 'kI4-s1000',
            'kJ4-s1100', 'kJ4-s1200', 'kJ4-s1400', 'kJ4-s1600', 'kJ4-s1800', 'kJ4-s2000',
            'kA3-s300', 'kA3-s400', 'kA3-s450', 'kA3-s550', 'kA3-s650'):
    try:
        r = analyse(tag)
        if r:
            allruns[tag] = r
    except IOError:
        print('%-12s no data' % tag)

print()
print('=== how often does the player hold one direction for >= 60 ticks (1 second)? ===')
for tag, runs in allruns.items():
    long_ = sum(1 for r in runs if r >= 60)
    vlong = sum(1 for r in runs if r >= 120)
    print('%-12s runs>=60t: %-4d   runs>=120t: %-4d' % (tag, long_, vlong))
