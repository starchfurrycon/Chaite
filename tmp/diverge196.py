"""Round 196: find the first divergence between the passing dps-450 and failing dps-500 runs.

The harness is deterministic, so any difference in the *player's* state at a given tick is a real
consequence of the boss's phase timing shifting the fight. Compare boss-observations frame by frame.
"""
import io, json, os

def rows(tag, name):
    p = 'artifacts/game-probe-%s/%s.jsonl' % (tag, name)
    if not os.path.exists(p):
        return []
    out = []
    for l in io.open(p, encoding='utf-8'):
        if l.strip():
            out.append(json.loads(l))
    return out

A, B = 'kB4-s450', 'kB4-s500'
for name in ('boss-observations', 'shield-events'):
    ra, rb = rows(A, name), rows(B, name)
    print('=== %s : %s=%d rows, %s=%d rows ===' % (name, A, len(ra), B, len(rb)))
    if not ra or not rb:
        continue
    # find the first row where the boss phase column differs
    n = min(len(ra), len(rb))
    first = None
    for i in range(n):
        pa = ra[i].get('bossPhase', ra[i].get('bossAi0'))
        pb = rb[i].get('bossPhase', rb[i].get('bossAi0'))
        if pa != pb:
            first = i
            break
    print('  first boss-phase divergence at row %s' % first)
    if first is not None:
        for i in range(max(0, first - 2), min(n, first + 3)):
            print('    row %-5d A phase=%-4s tick=%-6s | B phase=%-4s tick=%-6s' % (
                i, ra[i].get('bossPhase'), ra[i].get('tick'),
                rb[i].get('bossPhase'), rb[i].get('tick')))
    # also find first difference in player x
    for i in range(n):
        xa = (ra[i].get('player') or {}).get('position', {}).get('x')
        xb = (rb[i].get('player') or {}).get('position', {}).get('x')
        if xa is not None and xb is not None and abs(xa - xb) > 1e-6:
            print('  first player-x divergence at row %d: %.2f vs %.2f (d=%.2f)' % (i, xa, xb, xa - xb))
            break
    print()
