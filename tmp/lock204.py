"""Round 204: at the moment of each hit, how long ago was the lock, and had the
shield dash already been spent?  The demos show the dodge must run from lock+0.

Uses shield-events.jsonl (row per state change) and boss-observations.jsonl ai[].
"""
import io, json, os, glob

def jl(p):
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()] if os.path.exists(p) else []

TILE = 16.0
DASH_STATES = (1, 6, 11)

def analyse(tag):
    base = 'artifacts/game-probe-%s/' % tag
    obs = jl(base + 'boss-observations.jsonl')
    hurt = jl(base + 'hurt-observations.jsonl')
    shield = jl(base + 'shield-events.jsonl')
    if not obs or not hurt:
        return None
    # index observations by tick
    bytick = {}
    for o in obs:
        t = o.get('tick')
        if t is not None:
            bytick[t] = o
    # dash-start ticks from the shield events
    dashstarts = sorted(set(s['tick'] for s in shield if s.get('started')))
    out = []
    for h in hurt:
        tb = h.get('tickBefore')
        src = h.get('source', {})
        if tb is None:
            continue
        o = bytick.get(tb)
        if o is None:
            continue
        # find the most recent observation where the boss was in a dash state
        locktick = None
        state = None
        for back in range(0, 80):
            oo = bytick.get(tb - back)
            if oo is None:
                continue
            npcs = oo.get('npcs') or []
            if not npcs:
                continue
            ai = npcs[0].get('ai') or []
            st = ai[0] if ai else None
            if st in DASH_STATES:
                locktick = tb - back
                state = st
                break
        since = None if locktick is None else tb - locktick
        # last dash start at or before this hit
        prior = [d for d in dashstarts if d <= tb]
        lastdash = max(prior) if prior else None
        out.append(dict(tick=tb, type=src.get('type'), dmg=h.get('actualReturn'),
                        lock=locktick, since=since, state=state,
                        lastdash=lastdash,
                        dashago=None if lastdash is None else tb - lastdash))
    return out

for tag in ('kI4-s700', 'kI4-s800', 'kI4-s900', 'kJ4-s1100', 'kA3-s550', 'kA3-s300'):
    rows = analyse(tag)
    if rows is None:
        print('%-12s no data' % tag); continue
    print('=== %s ===' % tag)
    for r in rows:
        print('  hit t=%-6s type=%-4s dmg=%-4s | lock t=%-6s (%s ticks before, state=%s) | last shield dash t=%-6s (%s ticks before)' % (
            r['tick'], r['type'], r['dmg'], r['lock'], r['since'], r['state'], r['lastdash'], r['dashago']))
    print()
