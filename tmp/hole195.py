"""Round 195: why do strong-obsidian dps 500/550 fail while 450/650 pass?

Harness is deterministic, so the trajectories are exactly reproducible. Characterise the
contacts in each run: tick, source kind/type, boss phase, and the geometric relation.
"""
import io, json, os

RUNS = [
    ('kB4-s450', 'PASS', 450),
    ('kB4-s500', 'FAIL', 500),
    ('kB3-s550-rb4f9', 'FAIL', 550),
    ('kA3-s650', 'PASS', 650),
    ('kA3-s400', 'PASS', 400),
    ('kA3-s300', 'PASS', 300),
]


def load(tag):
    hp = 'artifacts/game-probe-%s/hurt-observations.jsonl' % tag
    if not os.path.exists(hp):
        return None
    out = []
    for l in io.open(hp, encoding='utf-8'):
        if l.strip():
            out.append(json.loads(l))
    return out


print('%-20s %-5s %-6s %-7s %-8s  contacts' % ('run', 'kind', 'dps', 'hits', 'rate'))
print('-' * 100)
for tag, kind, dps in RUNS:
    rows = load(tag)
    if rows is None:
        print('%-20s %-5s %-6d  (no hurt-observations)' % (tag, kind, dps))
        continue
    rp = 'artifacts/game-probe-%s/result.json' % tag
    j = json.load(io.open(rp, encoding='utf-8-sig'))
    rate = j['hits'] / j['ticks'] * 1000.0
    print('%-20s %-5s %-6d %-7d %-8.3f' % (tag, kind, dps, j['hits'], rate))
    for r in rows:
        src = r.get('source', {})
        req = r.get('request', {})
        pos = src.get('position') or {}
        vel = src.get('velocity') or {}
        print('      t=%-6s %-10s type=%-5s dmg=%-5s life %s->%s' % (
            r.get('tickBefore'), src.get('kind'), src.get('type'),
            req.get('damage'), r.get('player', {}).get('lifeBefore'),
            r.get('player', {}).get('lifeAfter')))
