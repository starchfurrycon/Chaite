"""Round 202: why did dps 900 achieve a contact rate of 0.174 (1 hit in 5741 t)?

Compare the three strong-obsidian runs frame by frame:
  kI4-s900  rate 0.174  1 hit   KILL
  kI4-s800  rate 0.313  2 hits  KILL
  kI4-s700  rate 0.671  4 hits  DIED
  kI4-s750  rate 0.442  3 hits  KILL
The harness is deterministic, so any divergence is exactly reproducible.

Report: the altitude band each run holds, wingTime usage, dash cadence, and where the
first planned difference appears.
"""
import io, json, os

def rows(tag):
    p = 'artifacts/game-probe-%s/boss-observations.jsonl' % tag
    if not os.path.exists(p):
        return []
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def hurt(tag):
    p = 'artifacts/game-probe-%s/hurt-observations.jsonl' % tag
    if not os.path.exists(p):
        return []
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

def stats(tag):
    r = rows(tag)
    if not r:
        return None
    ys = [(x.get('player', {}).get('position') or {}).get('y') for x in r]
    ys = [y for y in ys if y is not None]
    ys.sort()
    def q(p):
        return ys[int(len(ys) * p)] if ys else 0
    wt = [((x.get('player') or {}).get('wingTime')) for x in r]
    wt0 = sum(1 for w in wt if w == 0)
    dashes = sum(1 for x in r if (x.get('plan') or {}).get('dash') is True)
    return dict(n=len(r), ymin=ys[0], y25=q(.25), y50=q(.50), y75=q(.75), ymax=ys[-1],
                wt0pct=100.0 * wt0 / len(wt), dashes=dashes)

print('%-12s %-6s %-9s %-9s %-9s %-9s %-9s %-8s %-7s' % (
    'run', 'rows', 'y_min', 'y_p25', 'y_p50', 'y_p75', 'y_max', 'wt0%', 'dashes'))
for t in ('kI4-s700', 'kI4-s750', 'kI4-s800', 'kI4-s900'):
    s = stats(t)
    if s is None:
        print('%-12s MISSING' % t)
        continue
    print('%-12s %-6d %-9.0f %-9.0f %-9.0f %-9.0f %-9.0f %-8.1f %-7d' % (
        t, s['n'], s['ymin'], s['y25'], s['y50'], s['y75'], s['ymax'],
        s['wt0pct'], s['dashes']))

print()
print('=== hits per run (tick, source, actual damage) ===')
for t in ('kI4-s700', 'kI4-s750', 'kI4-s800', 'kI4-s900'):
    h = hurt(t)
    print('%s: %d hits' % (t, len(h)))
    for x in h:
        src = x.get('source', {})
        print('    t=%-6s type=%-4s kind=%-10s actual=%-4s life %s->%s' % (
            x.get('tickBefore'), src.get('type'), src.get('kind'), x.get('actualReturn'),
            x.get('player', {}).get('lifeBefore'), x.get('player', {}).get('lifeAfter')))
