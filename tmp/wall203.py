"""Round 203: are the type-386 tornado hits only lethal near the arena walls?

If the player is cornered when a tornado cluster arrives, the fix is positional (turn earlier),
which is a lever the horizontal logic can actually act on. Check the player's x at every
370/386/384 hit across every strong-obsidian run, against the 320-tile arena [0, 320*16].
"""
import io, json, os, glob

TILE = 16.0
ARENA_LEFT, ARENA_RIGHT = 0.0, 320 * TILE   # 0 .. 5120

def load(tag):
    p = 'artifacts/game-probe-%s/hurt-observations.jsonl' % tag
    if not os.path.exists(p):
        return []
    return [json.loads(l) for l in io.open(p, encoding='utf-8') if l.strip()]

tags = sorted(os.path.basename(os.path.dirname(p)).replace('game-probe-', '')
              for p in glob.glob('artifacts/game-probe-k*-s*/hurt-observations.jsonl'))
tags = [t for t in tags if t.startswith(('kA3', 'kB3', 'kE3', 'kF3', 'kI4', 'kJ4'))]

print('%-12s %-8s %-6s %-9s %-9s %-8s %s' % ('run', 'tick', 'type', 'player_x', 'tile', 'edge_dist', 'verdict'))
agg = {}
for t in tags:
    h = load(t)
    p = 'artifacts/game-probe-%s/result.json' % t
    if not os.path.exists(p):
        continue
    r = json.loads(io.open(p, encoding='utf-8').read())
    kill = (r.get('bossLifeRemaining') == 0) and not r.get('death')
    for x in h:
        src = x.get('source', {})
        typ = src.get('type')
        pos = src.get('position') or {}
        px = pos.get('x')
        if px is None:
            continue
        tile = px / TILE
        edge = min(tile, 320 - tile)
        agg.setdefault(typ, []).append(edge)
        print('%-12s %-8s %-6s %-9.0f %-9.1f %-8.1f %s' % (
            t, x.get('tickBefore'), typ, px, tile, edge, 'K' if kill else 'D'))

print()
print('=== distance from the nearer arena wall, in tiles, at the moment of each hit ===')
for typ in sorted(agg):
    v = sorted(agg[typ])
    n = len(v)
    print('type %-4s n=%-3d min=%-7.1f p25=%-7.1f median=%-7.1f p75=%-7.1f max=%-7.1f' % (
        typ, n, v[0], v[n // 4], v[n // 2], v[3 * n // 4], v[-1]))

print()
print('=== fraction of hits taken within N tiles of a wall ===')
for typ in sorted(agg):
    v = agg[typ]
    for N in (20, 40, 60, 80):
        c = sum(1 for e in v if e < N)
        print('  type %-4s within %-3d tiles: %d/%d = %.0f%%' % (typ, N, c, len(v), 100.0 * c / len(v)))
    print()
