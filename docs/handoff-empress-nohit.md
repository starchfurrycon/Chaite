# 交接：光女 empress-broom 无伤训练

> **当前权威状态见下节「Current state (v3)」。**
> 本文档其余部分是第 25 轮写下的历史内容，其中「正在运行的东西」一节已经过期
> （那是指 `nohit-v1`），保留它是因为它记录了当时的口径与判断依据。
> 适应度定义、禁止事项、证据位置几节至今有效。

## Current state (v3) — 覆盖下文的运行状态

Written to be read by someone with no memory of the session that produced it.
Authority in force: **train toward 无伤** — a win with zero hits — and accept on
the same basis. The earlier ≥40-seed Wilson lower bound ≥90% criterion is
demoted to reference information by the round-25 directive above.

### What is running

| Thing | Value |
| --- | --- |
| Trainer | `artifacts/_run-train-nohit-v3.ps1`, WMI-detached (parent `WmiPrvSE.exe`) |
| Tag | `empress-broom-nohit-v3` |
| Scheme | rank-weighted population ES, mean initialised to zero, eta 0.30, sigma 0.10 |
| Size | population 16, 6 seeds per candidate, 6 in flight, 24 generations, validate every 4 |
| Build guard | `8BBFAC33E3DBE64C`, in `artifacts/training/empress-broom-nohit-v3/build.txt` |
| Watcher | `tools/watch-training.ps1`, detached, one line per 30 min to `.../watch.log` |
| Console | `artifacts/_train-console-nohit-v3.log` |

Both processes are WMI-detached on purpose: a harness-managed background job is
killed when the session that started it is torn down, which is how `nohit-v1`
died seven minutes after its session ended.

### Why zero weights are the starting point

`LearnedPolicy.Adjust` treats the scripted decision as the base and lets the
network only correct it, with class zero of each head meaning "leave it alone".
Zero weights therefore reproduce the fixed state machine **exactly**, and the
search starts at that machine's measured behaviour instead of at random weights
that score below it. Verified three ways:

* offline, `Adjust` returns the scripted values bit-for-bit under a zero policy;
* offline, the same call under a random policy returns different values, so the
  entry point is live and not merely accepted;
* in the engine on seed 1, a zero-weight policy is identical to no policy at
  all — 5985 ticks, 13 hits, 79874 damage.

`artifacts/_check/policy-zero.txt` (798 tokens, 794 weights, all zero) is the
definition of that control. Keep it: it is the reference every future candidate
is measured against.

### The constraint that decides everything

**One probe costs 250–273 s on this machine with nothing else running**,
measured three times, and the user's own game holds roughly five cores
throughout. That is about **fourteen probes per hour**.

A generation costs `1 + Population` waves of `SeedsPerCandidate` seeds, which at
the current size is about seventy minutes, so a day buys roughly twenty
generations.

The distance to the goal does not fit in that budget. Zero weights start at the
fixed machine's no-hit fitness of **−0.0264**; acceptance needs about **+0.94**.
A derivative-free search over **794 parameters** cannot cover that ground in a
few hundred rollouts, and no choice of population, seed count or learning rate
changes that arithmetic.

The v3 run is therefore sized to answer one question honestly — **does the
residual scheme climb, and how fast** — rather than to promise a finished
policy. If the curve is flat, the conclusion is that this representation and
this probe budget are wrong together, and the fix is a smaller parameterisation
or a cheaper probe, not another learning-rate sweep.

### The cheapest known lever, not yet used

Every doomed probe runs to its tick limit even though it is already a failure
the moment it takes a hit. A stop-on-first-hit flag in `tools/GameProbe.cs`
would end those runs early. Under a no-hit objective the full-length runs are
exactly the successes, so this is close to free speedup on everything that
fails. It costs one rebuild and a re-verification, and it is the first thing to
build if the curve is too shallow to read.

### Judgement criteria, fixed before the result is known

* **Climbing** — parent no-hit fitness rises across generations and the best
  candidate exceeds the parent by more than seed noise. Continue.
* **Flat** — parent fitness stays near −0.0264 with no trend over ≥8
  generations. The scheme is not learning; change the representation or the
  probe, and do not tune the rate.
* **No-hit reached** — any row with `noHit` greater than zero on a training
  seed, then confirm on the disjoint `EvalSeeds` before any acceptance claim.

### One correction worth carrying forward

A control that reports "the numbers are identical, so the branch never ran" is
only valid if the control **cannot** be a no-op. The first version of the
verification used large random weights as its control and got a bit-identical
fight, which looked like a dead hook. It was not: offline, at the probed input
and at every input tried, that policy returned the scripted decision unchanged.
The standing rule is exactly right, and it cuts the other way too — when the
unmodified and modified runs agree, establish that the modification was capable
of disagreeing before concluding anything about the code.

### One trap in the code itself

`CombatPlanner` owns a route-specific script per route and dispatches to it by
`FormulaRoute`, so for example `empress-broom` goes to `_empressBroomScript`.
`FormulaScriptController.Tick` holds a **second, independent** implementation of
the empress and fishron decisions, and it is only reached by the final `else`
branch of that chain. An early grep in this session suggested the route scripts
were dead code; a direct grep disproved it, and the wrong version of that
conclusion briefly went into this document. The lesson is the same one as
above: establish the dispatch empirically before hooking or trusting a path, and
check the fix rather than the first plausible reading.

---

## 第 25 轮的历史记录（运行状态已过期，其余仍有效）

> 本文件是**当前权威状态的压缩版**，供上下文重置后直接接手，不必重新推导。

## 用户最新指示（覆盖旧验收口径）

> "关于验收和基线，直接以**无伤**为目标进行训练即可吧。"

因此：**训练目标与验收 = 无伤（零受击取胜）**。
原"40 条种子 95% Wilson 下界 ≥90%"与"5 条路线补基线"**降为参考信息**，不再作为主线工作量。

## 正在运行的东西

- **tag**：`empress-broom-nohit-v1`
- **场景/路线**：`empress-night` / `empress-broom` / PolicyRoute `EmpressBroom`
- **参数**：种群 16、每候选 4 种子、4 路并行、**120 代**、每 5 代在 40 条保留种子上验证
- **启动时刻**：2026-09-16 约 07:50（第 26 轮）
- **构建**：`artifacts/training/empress-broom-nohit-v1/build.txt` = `81D466CE66AB5CB0`
  （必须与 `src/Chaite.Core/bin/Release/net48/Chaite.Core.dll` 一致；不一致说明被污染）
- **旧训练 `empress-broom-v1` 已主动停止**（它优化的是旧适应度），其目录**保留不删**。

## 适应度（已改为无伤口径，并实测验证）

`tools/train-policy.ps1` 的 `Get-Fitness`：

| 结果 | 适应度 |
|---|---|
| **无伤胜利** | **1.0000**（唯一最大值） |
| 胜利，挨 h 下（h<25） | 0.5 + 0.5·(1 − h/25) |
| 胜利，挨 ≥25 下 | 0.5000（下限） |
| 失败 | 0.5·(damage/98000) − 0.5·(hits/14) |

性质：无伤唯一最大；胜利内挨打越多分越低（单调）；**任何胜利 ≥ 任何失败**
（唯一例外：胜利下限 0.5 与失败上限 0.5 在"0 受击且恰好 100% 伤害"角点上**相等**，
零测度、实测不出现）。

## 基线（新适应度，固定状态机，46 条唯一种子）

**均值 −0.0264**，中位 −0.0568，范围 −0.1278 … **0.7400**（种子 29，胜利但挨 13 下）。
**2 次胜利，0 次无伤。**

**这是最重要的一句话**：固定状态机在 46 条种子里**一次无伤都没拿到**，
所以无伤不是"再推一点"，而是要学出**它从未产生过的行为**。

## 证据在哪（已验证不会被覆盖）

- 训练曲线：`artifacts/training/empress-broom-nohit-v1/log.csv`
  （列：`gen,candidate,seeds,fitness,note`；note 含 `wins=`、`noHit=`、`hits=`）
- 验证：`artifacts/training/<tag>/eval-<tag>-gen<g>.json`
  （line 306 用 `-Tag "$Tag-gen$gen"`，**每代独立、不覆盖**）
- 逐条原始证据：`artifacts/game-probe-rt-empress-broom-nohit-v1-g<g>-*<seed>-*/result.json`
  ——**`noHit` 可直接从这里算**（`win && hits==0`），无需改正在运行的训练器
- 输出策略：`artifacts/training/<tag>/params-genNNN.txt` / `params-best.txt`

## 判断标准（预先写死，避免事后找理由）

1. **`log.csv` 的 `noHit` 列出现非零**才是真进展；
2. 若适应度只在损失区爬、`noHit` 长期为 0 → 4 种子的比较噪声压住了信号，
   应把每候选种子数提到 8–12（配对取样下噪声约为 `sd·√(2(1−ρ))/√K`，
   sd=0.0603、ρ 未实测）再重启；
3. 120 代跑完仍无无伤 → 如实报告"该特征集 + 该搜索预算达不到无伤"及其证据，
   **不得用"平均伤害提高了"充数**。

## 现在能做什么 / 不能做什么

**不能**（会污染正在跑的实验）：

- 重新编译任何**包含 Core** 的工程——4 个脚本已改但未编译，msbuild 会重编 Core 并改变 DLL；
- 修改 `tools/train-policy.ps1`——验证步骤会 `& $MyInvocation.MyCommand.Path -Mode eval` 从磁盘重读；
- 并发跑额外 wave——训练已占 4 路并发，过载会危及整套测量所依赖的确定性。

**能**：只读分析现有证据、写文档、写不参与编译的新脚本。

## 进度窥视器（第 40 轮加入，**只读**，可随时启动）

```powershell
& 'tools\watch-training.ps1' -Tag empress-broom-nohit-v1 -IntervalMinutes 30
```

每 30 分钟往 `artifacts/training/<tag>/watch.log` 追加**一行**并同时打印，包含：
代数、已评分候选数、最好候选、最近父代、**`noHit` / `wins` 累计**、
已完成运行数、新增运行数、验证文件数、`params-gen001` 是否存在、构建守卫、探针进程数。

**判定值**：`OK`（有进展）/ `BASELINE`（第一轮，无参照）/
`SLOW`→`STALL`（连续无新运行）/ `DEAD`（连续无新运行**且无探针进程**）/
`GUARD-MISMATCH`（构建被污染，实验作废）/ `BREAKTHROUGH`（**出现无伤结果**）。

**它是只读的**：不编译、不编辑、不起 wave、不碰 Release，所以不可能干扰实验——
这正是它存在的理由，因为运行期间其它所有动作都不安全。

**注意**：它是无限循环的作业，**不会"完成"因而不会主动通知**；
要读它就用 `job_output`，或直接 `Get-Content watch.log`。
第 1 轮曾误报 `DEAD`（首轮无参照且赶上批次空隙），已修正为需要**连续两轮无新运行且无进程**才判 `DEAD`；
那行误报仍保留在 `watch.log` 里，没有删除。

## 已完成的其余工作（无需重做）

- 学习策略类型 `src/Chaite.Core/LearnedPolicy.cs`（38→32→10，参数从文件读入）
- **8/8 路线全部接入 hook**（猪鲨 5 条 + 光女 3 条），全部经过"不触碰训练的编译验证"
  （Roslyn `csc` 编译到 `artifacts/_check/`，从不写 Release）
- 启动前四项检查全部实测通过：固定路径逐位回归、随机策略使结果完全不同、
  缺文件 `harness-error`（响亮失败）、并发与串行逐位一致

## 已知无需再查的事

- `terraria=0` 有三种成因，已验证前两种为正常：**批间空隙**、**wave 内种子间空隙**；
  第三种（真死亡）尚未出现过。
- `result.json` **没有 `bossLife` 字段**，用 `bossDamage` / `bossLifeRemaining`。
- PS 5.1 里 `(单个对象).Count` 为 `$null`；`[math]::Max` 对小数会走 int 重载截断。