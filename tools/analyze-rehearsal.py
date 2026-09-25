# -*- coding: utf-8 -*-
"""彩排读数分析器（输入直读）。

读一个彩排目录里的 `<tag>.episodes.jsonl` 与 `<tag>.obs.jsonl`，输出：
  * 胜率 / hits<=2 比例 / hits 直方图 / 中位 tick / hits per 1k tick / 平均 bossDamage
  * 按受击时 Boss 状态（hover / charge / bubble / sharknado / transform / teleport）分桶
  * 每次受击前 8 tick 的玩家 vy / vx（用来验证"垂直轴无响应=无动力自由落体"那条结论）

用法：
  python analyze-rehearsal.py <彩排目录> [--tag <tag>]

**两个文件的行数口径不同，别误读：**
  * `<tag>.episodes.jsonl` 只有 **N-1** 行（探针在"下一场开始"时才落盘上一场，最后一场永不写入），
    它是**判分口径**（`-Episodes 40` → 39 行）。
  * `<tag>.obs.jsonl` 含**全部 N 场**的逐 tick 行。所以受击分桶的场数比判分行多一场，
    这是正常的，不是 bug。（冒烟实测：obs 里 e=0 有 5761 行 hits 0→3、e=1 有 1739 行 hits 0→4，
    而 episodes.jsonl 只有 e=0 那一行且 `hits=3` —— 逐字吻合。）

纪律：obs 流里的状态字段在部分行里是**字符串**，一律显式 int()；
      配对后先断言 malformed=0，自证不过就不许读数。
"""
import json, os, sys, glob
from collections import Counter, defaultdict

HOVER = {0, 5, 10}
CHARGE = {1, 6, 11}
BUBBLE = {2, 7}
NADO = {3, 8}
TRANSFORM = {4, 9}
TELEPORT = {12}

def bucket(bs):
    if bs in HOVER: return "hover"
    if bs in CHARGE: return "charge"
    if bs in BUBBLE: return "bubble"
    if bs in NADO: return "sharknado"
    if bs in TRANSFORM: return "transform"
    if bs in TELEPORT: return "teleport"
    return f"other({bs})"

def as_int(v, malformed):
    try:
        return int(v)
    except (TypeError, ValueError):
        malformed[0] += 1
        return None

def median(xs):
    if not xs: return float("nan")
    s = sorted(xs); n = len(s)
    return s[n // 2] if n % 2 else (s[n // 2 - 1] + s[n // 2]) / 2

def main():
    d = sys.argv[1]
    tag = None
    if "--tag" in sys.argv:
        tag = sys.argv[sys.argv.index("--tag") + 1]
    eps_files = glob.glob(os.path.join(d, "*.episodes.jsonl"))
    obs_files = glob.glob(os.path.join(d, "*.obs.jsonl"))
    if not eps_files:
        print(f"FAIL 找不到 episodes 文件于 {d}"); sys.exit(2)
    if tag:
        eps_files = [p for p in eps_files if tag in os.path.basename(p)]
        obs_files = [p for p in obs_files if tag in os.path.basename(p)]
    eps_path, obs_path = eps_files[0], (obs_files[0] if obs_files else None)
    print(f"episodes : {os.path.basename(eps_path)}")
    print(f"obs      : {os.path.basename(obs_path) if obs_path else '（缺失）'}")

    eps = [json.loads(l) for l in open(eps_path, encoding="utf-8") if l.strip()]
    wins = [e for e in eps if e.get("win")]
    hits = [int(e.get("hits", 0)) for e in eps]
    ticks = [int(e.get("ticks", 0)) for e in eps]
    dmg = [float(e.get("bossDamage", 0)) for e in eps]
    n = len(eps)
    print(f"\n=== 汇总（{n} 场）===")
    print(f"  胜率          {len(wins)}/{n} = {100.0*len(wins)/n:.1f}%")
    print(f"  hits<=2 比例  {sum(1 for h in hits if h <= 2)}/{n} = {100.0*sum(1 for h in hits if h<=2)/n:.1f}%")
    print(f"  hits 直方图   {dict(sorted(Counter(hits).items()))}")
    print(f"  中位 tick     {median(ticks):.0f}   (min {min(ticks)} / max {max(ticks)})")
    print(f"  hits/1k tick  {1000.0*sum(hits)/max(1,sum(ticks)):.3f}")
    print(f"  平均 bossDamage {sum(dmg)/n:.0f}   (max {max(dmg):.0f})")
    print(f"  结局分布      {dict(Counter(e.get('outcome') for e in eps))}")

    if not obs_path:
        print("\n（没有 obs 流，跳过受击分桶）"); return

    malformed = [0]
    cur_e = -1
    prev = None
    hit_rows = []          # (episode, tick, bs, gap, distance)
    prehit_traces = []     # 每次受击前 8 tick 的 (vy, vx)
    ring = []
    rows = 0
    bs_seen = Counter()
    vy_fall = 0; vy_powered = 0
    with open(obs_path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line: continue
            r = json.loads(line); rows += 1
            e = as_int(r.get("e"), malformed)
            bs = as_int(r.get("bs"), malformed)
            if bs is not None: bs_seen[bs] += 1
            if e != cur_e:
                cur_e = e; ring = []
                continue
            if prev is not None:
                hp = as_int(prev.get("hits"), malformed); hc = as_int(r.get("hits"), malformed)
                if hp is not None and hc is not None and hc > hp:
                    px = float(r["px"]); py = float(r["py"])
                    bx = float(r["bx"]); by = float(r["by"])
                    gap = ((px - bx) ** 2 + (py - by) ** 2) ** 0.5
                    hit_rows.append((e, as_int(r.get("t"), malformed), bs, gap))
                    prehit_traces.append(list(ring[-8:]))
            vy = r.get("vy")
            if vy is not None:
                try:
                    vy = float(vy)
                    if abs(vy - 0.40 * len(ring[-8:])) < 0.25: pass
                except (TypeError, ValueError):
                    pass
            ring.append((r.get("vy"), r.get("vx"), bs))
            prev = r

    print(f"\n=== 自证断言 ===")
    print(f"  obs 行数            {rows}")
    print(f"  malformed 状态字段  {malformed[0]}   （必须为 0，否则读数作废）")
    assert malformed[0] == 0, "状态字段出现非整数，读数作废"
    print(f"  出现过的 bs 取值    {sorted(bs_seen)}")

    print(f"\n=== 受击按 Boss 状态分桶（{len(hit_rows)} 次）===")
    by = defaultdict(list)
    for e, t, bs, gap in hit_rows:
        by[bucket(bs)].append(gap)
    tot = max(1, len(hit_rows))
    for k, v in sorted(by.items(), key=lambda kv: -len(kv[1])):
        print(f"  {k:<12} {len(v):>4}  ({100.0*len(v)/tot:5.1f}%)   gap 中位 {median(v):7.1f}")

    print(f"\n=== 受击前 8 tick 的玩家 vy（验证'无动力自由落体'）===")
    fall = 0; powered = 0
    for tr in prehit_traces:
        vys = []
        for vy, vx, bs in tr:
            try: vys.append(float(vy))
            except (TypeError, ValueError): pass
        if len(vys) >= 2:
            # 逐 tick 增量是否恒为 +0.40（= Player.gravity），即完全没有动力
            diffs = [round(vys[i+1] - vys[i], 3) for i in range(len(vys)-1)]
            if diffs and all(abs(d - 0.40) < 0.02 for d in diffs): fall += 1
            else: powered += 1
    print(f"  命中前 8 tick 逐 tick 增量恒为 +0.40（纯重力）：{fall}/{fall+powered}"
          f"  ({100.0*fall/max(1,fall+powered):.1f}%)")
    print(f"  有动力（上升/翅膀）：{powered}/{fall+powered}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(2)
    main()
