"""Round 203b: is a death decided by the SHORTEST hit-to-hit interval?

The wall hypothesis is dead (0% of hits within 40 tiles of a wall; median 67-77 tiles).
Test instead: for every strong-obsidian run, the minimum gap in ticks between consecutive
hits, and whether any pair falls inside an immune/invulnerability window.
"""
import io, json, os, glob

def load(tag):
    p = 'artifacts/game-probe-%s/hurt-observations.jsonl' % tag
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()] if os.path.exists(p) else []

rows = []
for p in sorted(glob.glob('artifacts/game-probe-k*/result.json')):
    tag = os.path.basename(os.path.dirname(p)).replace('game-probe-', '')
    if not tag.startswith(('kA3', 'kB3', 'kE3', 'kF3', 'kI4', 'kJ4')):
        continue
    r = json.loads(io.open(p, encoding='utf-8').read())
    kill = (r.get('bossLifeRemaining') == 0) and not r.get('death')
    h = load(tag)
    ticks = sorted(int(x['tickBefore']) for x in h if x.get('tickBefore') is not None)
    gaps = [ticks[i + 1] - ticks[i] for i in range(len(ticks) - 1)]
    dps = tag.split('-s')[-1]
    rows.append((dps, tag, r.get('ticks'), len(ticks), min(gaps) if gaps else None,
                 sorted(gaps)[:3], kill, r.get('bossLifeRemaining')))

rows.sort(key=lambda x: (int(x[0]) if x[0].isdigit() else 0))
print('%-6s %-11s %-8s %-5s %-9s %-18s %s' % ('dps', 'run', 'ticks', 'hits', 'min_gap', '3 smallest gaps', 'verdict'))
for dps, tag, t, n, mg, sg, kill, left in rows:
    print('%-6s %-11s %-8s %-5d %-9s %-18s %s' % (
        dps, tag, t, n, mg if mg is not None else '-', str(sg), 'KILL' if kill else 'DIED'))

print()
K = [r for r in rows if r[7]]
D = [r for r in rows if not r[7]]
km = [r[4] for r in K if r[4] is not None]
dm = [r[4] for r in D if r[4] is not None]
if km and dm:
    print('min_gap: KILL runs  min=%-5d max=%-5d  (n=%d)' % (min(km), max(km), len(km)))
    print('min_gap: DIED runs  min=%-5d max=%-5d  (n=%d)' % (min(dm), max(dm), len(dm)))
    print()
    print('=> the LONGEST minimum-gap among DIED runs is %d' % max(dm))
    print('=> the SHORTEST minimum-gap among KILL runs is %d' % min(km))
    print('   overlap: %s' % ('YES - not a criterion' if min(km) <= max(dm) else 'NO - perfect separator'))
    print()
    print('DIED runs sorted by min_gap: %s' % sorted(dm))
    print('KILL runs sorted by min_gap: %s' % sorted(km))
