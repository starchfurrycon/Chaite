"""Round 203c: what actually separates survival from death? Total damage vs rate vs timing."""
import io, json, os, glob

def load(tag):
    p = 'artifacts/game-probe-%s/hurt-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()] if os.path.exists(p) else []

print('%-11s %-6s %-5s %-7s %-8s %-9s %-9s %s' % (
    'run', 'ticks', 'hits', 'dmg', 'hits/1kt', 'dmg/tick', 'last_hit_t', 'verdict'))
K, D = [], []
for p in sorted(glob.glob('artifacts/game-probe-k*/result.json')):
    tag = os.path.basename(os.path.dirname(p)).replace('game-probe-', '')
    if not tag.startswith(('kA3', 'kB3', 'kE3', 'kF3', 'kI4', 'kJ4')):
        continue
    r = json.loads(io.open(p, encoding='utf-8').read())
    h = load(tag)
    t = r.get('ticks') or 0
    dmg = sum(x.get('actualReturn') or 0 for x in h)
    ticks = [int(x['tickBefore']) for x in h if x.get('tickBefore') is not None]
    rate = len(h) / t * 1000.0 if t else 0
    dpt = dmg / t if t else 0
    rec = dict(tag=tag, t=t, hits=len(h), dmg=dmg, rate=rate, dpt=dpt,
               last=max(ticks) if ticks else 0, left=r.get('bossLifeRemaining'))
    # outcome: a kill is boss life zero AND no death
    if (r.get('bossLifeRemaining') == 0) and not r.get('death'):
        K.append(rec); v = 'KILL'
    else:
        D.append(rec); v = 'DIED'
    print('%-11s %-6d %-5d %-7d %-8.3f %-9.4f %-9d %s' % (
        tag, t, len(h), dmg, rate, dpt, rec['last'], v))

def rng(name, xs, key):
    v = sorted(x[key] for x in xs)
    print('  %-8s n=%-3d min=%-9.4f median=%-9.4f max=%-9.4f' % (name, len(v), v[0], v[len(v)//2], v[-1]))

print()
for key in ('rate', 'dpt', 'hits', 'dmg'):
    print('=== %s ===' % key)
    rng('KILL', K, key); rng('DIED', D, key)
    kmin, kmax = min(x[key] for x in K), max(x[key] for x in K)
    dmin, dmax = min(x[key] for x in D), max(x[key] for x in D)
    sep = 'SEPARATES' if dmin > kmax else 'overlaps'
    print('  => KILL [%.4f, %.4f] vs DIED [%.4f, %.4f]  %s' % (kmin, kmax, dmin, dmax, sep))
    print()

print('=== when does the player die? (last hit tick / fight length) ===')
for x in sorted(D, key=lambda z: z['last']):
    print('  %-11s last_hit=%-6d of %-6d  (%.0f%% in)  boss_left=%-6s dmg=%d' % (
        x['tag'], x['last'], x['t'], 100.0 * x['last'] / x['t'], x['left'], x['dmg']))
