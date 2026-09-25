# 安全中止被当成超时罚分：13–26% 的战斗被截断且被反向激励

**日期**：2026-09-21
**结论**：插件在鱼鲨的原生 AI 离开"已审核公式表"时会**自动归还操作权**，
训练侧把这**当成 tick 上限超时**，于是每一次中止都扣了超时罚分。而这些中止
发生在 **BOSS 血量 60–100%（p90 = 100%）** 的时候——**策略正在赢的时候**。
这等于**惩罚策略走到终局**，是训练缺陷，不是战斗事实。

---

## 1. 怎么发现的

监视窗口报 `fsw121p` 有 26% 的局是 `Cancelled`，而 `fsw121d` 只有 12%。
两条臂奖励完全相同，只差观测宽度，**不该差一倍**。于是去查中止原因。

插件日志里一轮出现 25 次：

```
[拆特] 安全条件持续丢失，已自动归还操作权（猪鲨原生 AI 状态/时钟不在已审核公式表）。
```

对照死亡消息（31 次）：「死亡后战斗已无法继续，接管结束。」——**两者是不同的事件**。

## 2. 代码路径

`Chaite.Plugin/Runtime.cs:282-285`：当 `plan.RequestControlReturn` 为真且不是
"不支持的 BOSS"时，

```csharp
_encounter.Cancel();
Finish(player, "安全条件持续丢失，已自动归还操作权（" + detail + "）。", ...);
```

`detail` 来自 `Chaite.Core/CombatPlanner.cs:791`：

```csharp
if (!TryGetFishronEnrage(...) || !FishronFormulaStateContract.IsValid(in input, ...))
    return UnsupportedMobilityRoutePlan(plan, "猪鲨原生 AI 状态/时钟不在已审核公式表");
```

`FishronFormulaStateContract.IsValid` 校验 `(state, timer, sequence)` 三元组在
公式表内，其中计时器上限是 `TimerLimit(state, sequence, expert, enraged)`，而
enraged 冲刺只允许 `timer <= limit + 2`——**容差 2 tick**。

## 3. 训练侧为什么受害

探针的终止行只写 `done / win / dead`：

| 结局 | done | win | dead | 训练侧分支 |
|---|---|---|---|---|
| `SuccessNoDeath` | true | **true** | false | 胜利奖励 |
| `FailedAfterDeath` | true | false | **true** | 死亡奖励 |
| **`Cancelled`（安全中止）** | true | false | false | **超时罚分** ← 错 |
| `test-time-limit`（真超时） | true | false | false | 超时罚分 |

实测确认：`fsw121d` 的 92 场 `Cancelled`，**每一场的终止观测行都是
`win=false dead=false`**，即**每一场都被扣了超时罚分**。

### 被摧毁的胜场

`Cancelled` 局的 BOSS 伤害分布：

| 会话 | Cancelled 局数 | BOSS 伤害 p10 | p50 | p90 | ≥90% 的局 |
|---|---:|---:|---:|---:|---:|
| `fsw121d` | 91 | 0.29 | 0.79 | **1.00** | **45** |
| `fsw121p` | 203 | 0.35 | 0.60 | 0.76 | 3 |

`fsw121d` 有 **45 场**在 BOSS 伤害 ≥90% 时被中止——**这些本来会是胜利**。
按此计算，`fsw121d` 的真实胜率是 `(148+45)/737 = 26.2%`，而不是 148/737 = 20.1%。

### 这是反向激励，不只是漏计

中止发生时策略状态**很好**（受击少、BOSS 快死）。而它收到的是**罚分**。所以
训练在教策略：**不要走到终局**。这与"无伤"目标直接冲突。

## 4. 修复（只改训练缺陷，不动手写策略）

**探针侧**（`tools/GameProbe.cs`）：终止行新增 `st`（终止状态名）、`etl`
（本局已用 tick）、`tlim`（本局 tick 上限）。有了 `st`，训练侧才能把
"被引擎截断"和"真超时"分开。编译通过，只剩既有 `CS0169`。

**训练侧**（`training/chaite_env.py`）：新增 `is_safety_abort(row)`，识别
`st ∈ {Cancelled, EncounterInterrupted}`；`_reward` 增加该分支，**不给任何
终止奖励**（既不奖也不罚）。理由：这一局是被 harness 截断的，不是战斗分出
胜负，诚实的信号就是**已经累积的每次受击罚分**，到此为止。

**向后兼容**：老探针的行没有 `st`，`is_safety_abort` 返回 `False`，保持原
超时行为。这样这个修复**永远不会改变一个无法自证需要的流**。

逐项验证：

| 输入 | is_safety_abort |
|---|---|
| `st=Cancelled` | True |
| `st=EncounterInterrupted` | True |
| `st=test-time-limit`（真超时） | False |
| `st=SuccessNoDeath` | False |
| `st=FailedAfterDeath` | False |
| 无 `st`（老探针） | False |

`OBS_DIM` 仍是 121（本次不动观测）。

## 5. 什么时候生效

`run-session.ps1` 每一轮重新编译探针并重启训练进程，所以修复在**下一轮**
自动生效，不需要重启会话。当前两条臂在 round 6/12。

**风险**：奖励形状在中途变化会让价值函数暂时失准。但去掉的是一个**反向**
罚分，方向是修正而非扰动，所以让它生效比让它等下一轮更划算。

## 6. 这件事的类别

这已经是**同一类错误的第四次**：**度量的量不是想要的量**。

| # | 错误 | 度量的量 | 想要的量 |
|---|---|---|---|
| 1 | `HURT_FACTOR=40000` | 结束战斗 | 不被击中 |
| 2 | 局数被填充行放大 3.6× | 日志行数 | 真实战斗 |
| 3 | 旧奖励只推动存活时间 | 存活 | 闪避 |
| 4 | **安全中止算作超时** | **被截断** | **被打败** |

四次都是"奖励/计数没有度量目标量"。所以每次都要问：
**这个数字数的是我以为的那件事吗？**

---

## 7. 修复已生效（引擎内验证）

round 7 起探针重新编译，终止行开始带 `st`。`fsw121d` 的实测终止状态值：

```
SuccessNoDeath 21, FailedAfterDeath 3, Cancelled 9, 以及若干 EngagedDeadWaitingRespawn
```

**逐项核对 `_reward` 的分支顺序是对的**（这一条必须先证，否则会把死亡误判成中止）：

| 本局结局 | 终止行 `st` | `dead` | `win` | 命中分支 |
|---|---|---|---|---|
| `FailedAfterDeath` | `FailedAfterDeath` | **True** | False | 死亡奖励 ✓ |
| `SuccessNoDeath` | `SuccessNoDeath` | False | **True** | 胜利奖励 ✓ |
| `Cancelled` | `Cancelled` | False | False | **新的中性分支** ✓ |
| `test-time-limit` | `test-time-limit` | False | False | 真超时罚分 ✓ |

（流里那 35,563 行 `EngagedDeadWaitingRespawn` 是玩家已死但状态机还没走到末尾的
中间帧，它们**不是**终止行；终止行已验证为 `FailedAfterDeath`。）

## 8. 修正一个数：我此前把罚分说重了

第 3 节说"反向激励"，量级要修正：闪避目标下超时罚分只有 **−10**，而一次受击
是 **−160**。所以**超时罚分相对于受击成本可以忽略**，反向激励存在但很小。
真正有害的是**截断本身**：中止的局既不算胜也不算负，白跑。

## 9. 中止率随能力上升——这是现在最大的约束

八等份（按 `elapsedWallMs` 单调排序，最后一列最新）：

| 臂 | 每 1000 tick |
|---|---|
| `fsw121d` | 3.40 2.52 2.61 2.30 2.21 2.01 **1.23 1.32** |
| `fsw121p` | 4.04 3.05 2.66 2.56 1.91 1.71 **1.59 1.48** |
| `fsw121q` | 4.35 4.01 3.85 3.76 3.87 3.57 **3.28 3.31** |

| 臂 | 胜率 | 中止率 |
|---|---|---|
| `fsw121d` | 11.4 → **41.9 36.2** | 4.8 → **27.6 32.4** |
| `fsw121p` | 2.6 → **6.9 10.2** | 12.1 → **50.0 52.5** |
| `fsw121q` | 0.0 → 3.2 2.5 | 6.5 → 8.4 11.2（稳） |

**`fsw121p` 最新一档 52.5% 的局被中止**，胜率因此掉到 6.9–10.2%。它的受击率
（1.48）是三条里最好的，但**赢不下来**——一半的局被插件截断。

而中止**摧毁的正是胜场**（中止时 BOSS 血量）：

| 臂 | 中止局数 | 中止时 BOSS 伤害 p50 | ≥90% 的局 |
|---|---:|---:|---:|
| `fsw121d` | 128 | **1.00** | **68** |
| `fsw121p` | 276 | 0.60 | 5 |
| `fsw121q` | 89 | 0.32 | 0 |

`fsw121d` 的**一半中止发生在 BOSS 血量 100% 打光时**——那些本来是胜利。
所以它真实胜率是 `(193+68)/844 ≈ 30.9%`，不是 22.9%。

**因果**：策略越会躲，越能活到鱼鲨进入未建模状态的那一段，中止就越多。
`fsw121q`（不会躲）中止率稳定在 6–11%，因为它根本到不了那里。

## 10. 累计对比（本次三条臂 vs 上一代）

上一代的数字用**本项目已经确立的口径**（`docs/episode-count-error-2026-09-21.md`
第 38 节：9 个训练臂合计 34,692 场真实战斗、1,175 胜、2 次无伤），不另立分母。

| | 战斗 | 胜 | 胜率 | **无伤** | 每千场无伤 |
|---|---:|---:|---:|---:|---:|
| `fsw121d` | 857 | **270** | **31.5%** | **8** | **9.33** |
| `fsw121p` | 956 | 106 | 11.1% | 2 | 2.09 |
| `fsw121q` | 1,290 | 13 | 1.0% | **0** | 0 |
| **三条合计** | **3,103** | 389 | 12.5% | **10** | 3.22 |
| 上一代合计（9 臂，修正后） | 34,692 | 1,267 | 3.65% | 3 | 0.086 |
| 上一代 `obsb1`（同路线，修正后） | 2,592 | 397 | 15.32% | 2 | 0.77 |

（上表是第 11 节那条修正规则生效后的数；按探针**原样记录**的数是
`fsw121d` 199 胜 / 7 无伤，见第 11 节。）

**同路线对比最该看的一行**：`obsb1` 是上一代在**同一条鱼鲨强翼路线**上的最好
臂，0.77‰；`fsw121d` 是 9.33‰，**约 12 倍**。整代对比是 3.22‰ vs 0.086‰，
但那把女王史莱姆/光女等**几乎没有无伤**的路线也算进去了，所以**以同路线的
12 倍为准**。


**独立复核**：把 `artifacts/bridge-*/` 下**除本次 121 宽臂之外的全部 155 个标签**
（55,143 场）重数一遍，按探针**原样记录**的 `win` 是无伤 **2 次，且两次都在
`obsb1`**，其余 154 个标签一次都没有。所以"上一代只有 2 次无伤"这个数是可以
复现的。按第 11 节修正后的规则是 **3 次**（`train19` 多出一次，此前从未被发现）。

**注意 `fsw121q`**：它和 `fsw121p` 观测宽度完全相同，差别只在**放进去的是什么**。
1,290 场里**一次无伤都没有**——所以上面这些不是"多给 4 个输入"的效果。

**以及一个必须记住的偏差**：`fsw121d` 的 68 次中止发生在 BOSS 血量打光时，
那些本来是胜利。所以它的**真实胜率高于 22.8%**（约 30.9%），而
`hits/1000t` 也被这些截断局拉低了。

---

## 11. 第五个缺陷：BOSS 死了，却被记成"中止"

第 10 节那句"中止发生在 BOSS 血量打光时"追下去，发现比"被截断"更严重：
**那些局的 BOSS 已经死了，却被记成既不是胜也不是负。**

```
{"e":35,"outcome":"Cancelled","win":false,"hits":8,"ticks":4026,
 "bossDamage":77980,"bossLifeRemaining":0,"bubblesBroken":31}
```

`bossDamage` 等于鱼鲨满血 77,980，`bossLifeRemaining` 等于 **0**——**BOSS 死了，
战斗赢了**，而 `outcome` 是 `Cancelled`、`win` 是 `false`。

### 为什么会这样

`GameProbe.cs:4113` 用插件的会话状态当结局：

```csharp
if(state=="SuccessNoDeath" || ... || state=="Cancelled") Finish(state);
```

插件的安全中止和 BOSS 死亡**可以发生在同一 tick**，而中止先写入了
`SessionState.Cancelled`。**竞态由中止赢下**，于是胜利被记成中止。

### 数量

| 臂 | 中止局数 | 其中 **BOSS 已死** | 原记胜 | 修正后胜 |
|---|---:|---:|---:|---:|
| `fsw121d` | 130 | **70** | 199 (23.2%) | **270 (31.5%)** |
| `fsw121p` | 286 | 5 | 101 (10.6%) | 106 (11.1%) |
| `fsw121q` | 95 | 0 | 13 (1.0%) | 13 (1.0%) |

`fsw121d` **71 场胜利（占其全部胜利的 26%）被丢掉了**。奖励就是教策略的东西，
所以这不是计数问题，是**训练缺陷**：策略赢了却拿不到胜利奖励。

### 修复

`GameProbe.cs` 新增 `NormalizeTerminalState`：当会话状态是
`Cancelled`/`EncounterInterrupted` 而 **BOSS 已被击杀**时，把结局改写成
`SuccessNoDeath`（玩家活着）或 `SuccessAfterDeath`（玩家也死了）。

**安全性**：`lastBossLife` 在每次 `ResetEpisode()` 里连同
`lastBossLifeObservedTick=-1` 一起归零，所以判定要求
`lastBossLifeObservedTick>=0 && lastBossLifeExpectedRootCount>0`——
**没有观测过 BOSS 就不可能命中**，否则一局刚开始的第一次中止就会被判成胜利。

**同时改观测行**：终止观测行的 `win` 是训练侧真正读的字段，而它在
`Finish` 之前写入，所以归一化必须在**写观测之前**做，只改 episode 汇总不够。

### 基线也要用同一条规则

否则对比会偏袒新臂。上一代同样存在这个现象，只是轻得多：

| 臂 | 原记胜 | 修正后 | 原记无伤 | 修正后 |
|---|---:|---:|---:|---:|
| `obsb1` | 358 (13.81%) | 397 (15.32%) | 2 | 2 |
| `obsb2` | 152 (16.93%) | 169 (18.82%) | 0 | 0 |
| `train19` | 252 (9.02%) | 275 (9.85%) | 0 | **1** |
| `fw12` | 198 (5.48%) | 210 (5.82%) | 0 | 0 |
| `esd12` | 52 (0.99%) | 53 (1.01%) | 0 | 0 |
| `ebd11` | 0 | 0 | 0 | 0 |

**所以上一代的无伤总数是 3，不是 2**（`train19` 那一次此前从未被发现）。
新臂的相对优势**没有被夸大**：修正后 `fsw121d` 胜率 31.5% 对 `obsb1` 15.32%，
仍是约 2 倍；无伤 8 / 857 = 9.33‰ 对 `obsb1` 2 / 2,592 = 0.77‰，仍是约 12 倍。

### 计数

这已经是**第五个"数字没有度量它声称度量的东西"**的错误，而且和第四个是
**同一个根因的两半**：第四个是"中止被当成超时"，第五个是"中止把胜利吃掉了"。
两个都出在"用会话状态推断战斗结果"——而**战斗结果应该由 BOSS 的生死决定**。

---

## 12. 那个"未建模状态"到底是什么，以及它有多大影响

### 12.1 首先：BOSS 的 AI 是 100% 原生的

**我们从来没有改过鱼鲨的 AI。** 补丁只接管**玩家的输入**（方向/跳/下/冲刺），
BOSS 完全跑本体 Terraria 1.4.5.8 的原生代码。所以"未建模状态"**不是游戏缺了什么**。

### 12.2 "未建模状态"= **我们控制层的白名单**，不是游戏的状态

控制层里有一张**手写的审查表** `FishronFormulaStateContract`，枚举了它"审过的"
`(state, timer, sequence)` 三元组。这个三元组来自原生 AI 的 `ai[0]/ai[2]/ai[3]`：

```csharp
// CombatPlanner.cs:788
if (!TryGetFishronEnrage(...) ||
    !FishronFormulaStateContract.IsValid(in input, snapshot.Difficulty, enrage.NativeEnraged))
    return UnsupportedMobilityRoutePlan(plan, "猪鲨原生 AI 状态/时钟不在已审核公式表");
```

表里没有的三元组 → 插件**拒绝继续控制**，交还操作权 → 本局记 `Cancelled`。

这张表是**手写公式策略时代的审查产物**。现在驱动移动的是学习到的策略，表却还在
当门卫。

### 12.3 具体卡在哪个三元组（实测，expert 难度）

取 `fsw121d` 中止时的终止行自身三元组：

| `(state, timer, sequence)` | 次数 | 按合同判定 |
|---|---:|---|
| `(8, 1, 1)` | **11** | **state 8 只允许 sequence 0，实测是 1** |
| `(3, 88, 1)` | 1 | state 3 的 sequence 1 **只在 enraged 时**允许 |
| `(-1, -1, -1)` | 12 | 场上**没有 BOSS**——这些就是"BOSS 已死"的胜利局 |

合同原文：`case 8: return input.NativeSequence == 0;`

所以最主要的真实中止是 **state 8（Cthulhunado 那一段）的 sequence 1**：审查表只
写了 sequence 0。BOSS 能走到 sequence 1，说明**原生 AI 自己就是这么走的**——
是**我们的表不全**，不是游戏有未建模行为。

### 12.4 修正我自己一个错误假设

我一度以为第二个失效模式是"插件预测的 enrage 与游戏实际不一致"。**这是错的**，
查了赋值处：

```csharp
// TerrariaFacade.cs:2643
fishron.NativeEnraged = fishron.ExpectedEnraged;
```

`NativeEnraged` **直接由期望值赋值**，所以 `NativeEnraged != ExpectedEnraged`
这个判断**恒为假**，不可能触发。所以那些在合同内却被中止的局，原因不是 enrage
不一致。（记录在此，避免以后又把它当成一个假设去查。）

### 12.5 影响：真实截断率 7.2% / 30.5% / 7.7%

"真实截断"= 插件在**胜负未分**时结束了本局。BOSS 已死的 `Cancelled` 是胜利，
不算截断：

| 臂 | 战斗 | 中止 | 其中 BOSS 已死 | **真实截断** |
|---|---:|---:|---:|---:|
| `fsw121d` | 872 | 136 | 73 | **63 (7.2%)** |
| `fsw121p` | 972 | 301 | 5 | **296 (30.5%)** |
| `fsw121q` | 1,318 | 101 | 0 | **101 (7.7%)** |

**所以对主臂 `fsw121d` 影响是中等（7.2%），对 `fsw121p` 很大（30.5%）**——
而 `fsw121q`（不会躲的那条）也是 7.7%，说明 ~7% 是这条路线的基础水平，
`fsw121p` 的 30% 是它的策略**自己把 BOSS 推进了那张表没审过的分支**。

### 12.6 关键：这道门在训练里**什么都没保护**

训练时移动**完全来自策略**，不走公式脚本：

```csharp
// Runtime.cs:312-336
plan.Horizontal = replayDirection;
plan.Jump       = replayJump;
plan.Dash       = replayDash;
plan.Drop       = replayDown;
plan.FeatherFallUp = replayUp;
```

`RouteReplay` 从 `CHAITE_BRIDGE_FILE` 读策略写的动作，**覆盖了全部移动通道**。
公式脚本剩下的输出只有武器/瞄准/消耗品——而**伤害是探针模拟的**
（`simDps=1200`；实测一局 `bossDamage=35,684` 对 `simulatedDamage=35,540`，
真实武器贡献约 0.4%）。

**所以这张表在训练里守卫的是"已经被丢弃的输出"，代价是 7–30% 的局在终局被砍掉。**
策略永远学不到怎么收尾——而验收标准恰恰是"无伤赢下来"。

### 12.7 建议的修法（**需要改 Core，不能在本波进行中做**）

在**桥接/训练模式**下，让公式状态表的判定**不再结束本局**（保留日志）。理由：
移动是策略的、伤害是模拟的，表守卫的东西不影响训练结果。

这**不是**手写策略，是**训练环境的缺陷**。但它改的是 `Chaite.Core`，而项目铁律
是"波次运行中不得改/重建 Core"，所以必须**等本波结束**，并作为**新的测量组**
记录（`probe-manifest.json` 的 `InputCoreSha256` 必须与前一不同）。

**决定（2026-09-21，业主）**：**等本波结束再做，作为新测量组。**

### 12.8 为什么"等"是有依据的：两条路径的生效时机不同

查清了每轮的构建方式，这决定了哪些改动会漏进正在跑的波次：

```
run-session.ps1:195  -> runprobe.ps1 -> prepare-game-probe.ps1
   行 139  $probeSource = tools\GameProbe.cs        # 每轮**重新编译**
   行 135  $plugin = src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll   # 只**拷贝**
   行 136  $core   = src\Chaite.Core\bin\Release\net48\Chaite.Core.dll       # 只**拷贝**
```

所以：

| 改动 | 生效时机 |
|---|---|
| `tools/GameProbe.cs`（探针） | **下一轮自动生效**（每轮重编译） |
| `src/Chaite.Core`、`src/Chaite.Plugin`（源码） | **不生效**——除非手动 build 覆盖 `bin\Release\net48` |
| `training/*.py`、`*.ps1`（训练侧） | 下一轮自动生效（每轮重启 trainer） |

这解释了本轮观察到的现象：`st` 字段在 **round 7** 出现，而 `st` 是 round 7 之前
才加的——探针每轮重编译，所以第 7 轮就带上了。同理，第 11 节的"BOSS 已死算胜利"
会在 **round 9** 生效。

**因此铁律的具体含义是：波次运行中不要 build Core。** 只改源码而不 build 不会
泄漏进正在跑的波次（每轮只是拷贝现成 DLL）。

**审计性**：`prepare-game-probe.ps1` 每轮把 `GameProbeSourceSha256`、
`PreparedGameProbeSha256` 等写进 `probe-manifest.json`，所以"哪几轮用了哪个探针"
是可查的；Core 未重建，`InputCoreSha256` 在本波内保持不变——这正是新测量组要
打破的那一项。

### 12.9 本波结束后的执行清单

1. 在 `CombatPlanner.cs:785-793` 给公式状态表判定加**桥接模式旁路**：桥接开启时
   只记日志、不 `RequestControlReturn`；生产路径行为**逐字不变**。
2. build Core + Plugin 到 `bin\Release\net48`。
3. **必须有一个不可能是 no-op 的引擎内对照**证明旁路真的执行了（铁律）：
   预期 `Cancelled` 率从 7–30% 掉到接近 0，而 `SuccessNoDeath` 上升。
4. 记录新的 `InputCoreSha256`，作为新测量组。
5. 重训，并把 `fsw121p` 的 30.5% 截断当作它的主要失败模式重新审视。

---

## 13. 执行结果（2026-09-21 22:3x）

### 13.1 两处改动

**探针**（`tools/GameProbe.cs`）：第 11 节的 `NormalizeTerminalState` 原本用
`lastBossLife<=0 && observedTick>=0 && rootCount>0` 推断"BOSS 已死"。**实测它没有
生效**——rounds 9–12 仍有 34 场 `Cancelled` 带 `bossLifeRemaining==0`。

原因是 `lastBossLife` 归零有**两个来源**：观察到 BOSS 血量为 0，**以及每次
`ResetEpisode()`**。所以 `lastBossLife<=0` 无法区分"BOSS 死了"和"还没有 BOSS"。
改成**直接测量**：

```csharp
// CaptureActiveExpectedRootLife(): count>0 且 total<=0 时置位
if(total<=0) episodeBossKilled=true;
```

并给 episode 行加 `dbgBossKilled` / `dbgObsTick` / `dbgRootCount` 三个诊断字段——
**一个静默不生效的修复正是这一整轮反复出现的失败模式**，所以证据必须随行携带。

**插件**（`Runtime.cs`）：`plan.RequestControlReturn` 时，若 `_replay != null`
（即 `CHAITE_BRIDGE_FILE` 已设，训练桥接在驱动），**不再 `_encounter.Cancel()`**，
只每 600 tick 记一条日志。生产路径（无桥接）**逐字未变**。

### 13.2 引擎内对照（两次，都不可为 no-op）

| 证据 | 期望 | 实测 |
|---|---|---|
| 新字符串 `训练桥接：忽略公式表归还请求…` | 出现 | **4 次** |
| 旧字符串 `安全条件持续丢失` | 消失 | **0 次** |
| 新字段 `dbgBossKilled` | 出现 | 出现 |
| `Cancelled` 结局 | 趋近 0 | **0** |
| `SuccessNoDeath` 且 `dbgBossKilled=true` | 出现 | **15** |

第一轮验证（`fsw121d`，新构建）：

| | 战斗 | 胜 | 胜率 | 无伤 | `Cancelled` |
|---|---:|---:|---:|---:|---:|
| 新组（验证轮） | 18 | 15 | **83.3%** | 2 | **0%** |
| 旧组（同臂全波） | 1,037 | 374 | 36.1% | 18 | 17.9% |

**18 场太小，83.3% 的 2 倍标准误是 ±17.6%**，所以只能说方向明确、幅度待定。
三条臂在新组里 `abort` 全为 **0**。

### 13.3 新测量组

`Chaite.Core` 变了，所以这是新测量组，必须与旧的**分开统计**：

| | 旧组 | 新组 |
|---|---|---|
| `InputCoreSha256` | `CCEC6426…` | `66C9B1A0…` |
| `InputPluginSha256` | `2E902B93…` | `06F63B95…` |
| `ProbeSourceSha256` | `1D753375…` | `D04AEB32…` |

监视窗口 `GROUP_FIELD = "dbgBossKilled"` **只读新组**，避免把两种环境的数据混在一起。

---

## 14. 新组结果与一处口径更正（2026-09-21 08:5x）

### 14.1 结果

| 臂 | 战斗 | hits/1000t | hits/场 | 胜率 | 无伤率 | abort |
|---|---:|---:|---:|---:|---:|---:|
| `fsw121d` | 436 | **0.62** | 2.54 | **88.5%** | **12.2%**（53 场） | **0** |
| `fsw121p` | 447 | 1.07 | 4.23 | 68.9% | 2.2%（10 场） | **0** |
| `fsw121q`（宽度对照） | 783 | 2.34 | 5.77 | 4.0% | 0 | **0** |

三条臂 `abort` 全为 0——公式表再也不能结束一场训练。

### 14.2 更正我自己上一条消息里的口径

我上一条说"0.62 是历史最好 1.88 的 3 倍"，**这个比较不诚实**：1.88 是**上一代**
（obsb1 1.90、fsw121w 1.88）的数字，不是本波的。本波旧组打到**最后两轮**时已经是
0.74 了。按同一起点比较：

| | 上一代最好 | 旧组最后两轮 | 新组 |
|---|---:|---:|---:|
| hits/1000t | 1.88 | 0.74 | **0.62** |
| 胜率 | — | 55.1% | **88.5%** |
| 无伤率 | — | 4.7% | **12.2%** |
| abort | — | ~30% | **0** |

所以真正的收益**主要在胜率与无伤率**（55.1%→88.5%、4.7%→12.2%），命中率只是
0.74→0.62 的小幅改进（且仍在降：新组自身 0.70→0.58）。这符合机理——被截断的那些
场次本来就是**接近胜利**的场次，去掉截断后它们直接变成胜利，而不是让躲闪突然变好。

### 14.3 排除"数据构成变化"这一解释

旧组被截断的 186 场 `per1000t=0.87`，**低于**旧组总体 1.83。也就是说被截断的场次
比平均**更干净**，把它们拿掉只会**抬高**平均命中率。而新组命中率是**下降**的，所以
这个改进不是"换了统计口径"造成的。

### 14.4 胜利不是旁路伪造的

新组 1,663 场里，`SuccessNoDeath` 与"独立观测到 BOSS 死亡"（`dbgBossKilled`）不一致的
只有 **9 场（0.5%）**，全是死亡同一 tick 的竞态。`fsw121d` 的 386 场胜利里 380 场有
独立佐证。旁路只跳过 `_encounter.Cancel()`，不参与胜负判定。

### 14.5 监视窗口的第二个缺陷（已修）

`FLAT` 用**绝对** 1.00 hits/1000t 作为"无改进"阈值。新组到了 0.62 之后这个阈值
**算术上不可能达到**（命中率不能为负），于是它对一条**仍在下降**的臂（0.70→0.58）
每轮都报 FLAT。改成相对阈值 `FLAT_FRACTION = 0.15`（自身的 15%），并做了四例对照：
0.70→0.56 判"改进中"、0.60→0.59 判 FLAT、2.70→2.10 判"改进中"、2.70→2.60 判 FLAT，
全部符合预期。

---

## 15. 这些无伤胜利能不能直接当战斗策略用？（2026-09-21 09:1x）

结论：**躲闪这一半基本就绪，整套策略还不是。** 逐条给证据。

### 15.1 一场"无伤胜利"到底是什么

- 策略**只能移动**（24 个动作 = 方向 × 上 × 下 × 冲刺），**不能瞄准、不能攻击**。
- BOSS 的血是**模拟 DPS 直接扣的**（`GameProbe.cs:3950` `npc.life-=applied`，`simDps=1200`）。
  新组实测：**真实武器只贡献了 0.21%** 的 BOSS 伤害
  （`bossDamage=32,731,837` vs `simulatedDamage=32,664,479`）。
- 所以"无伤胜利" = **约 4,000 tick 内一次没被打到，同时模拟 DPS 把 BOSS 打死**。
- BOSS 的 AI、判定框、弹幕、以及**玩家受到的伤害全都是原生的**
  （`ARENA_READY ... no godmode`；`immune`/`immuneTime` 只被观测、从未被设置）。
  **所以躲闪本身是真实有效的**，这正是这条路线的价值。

### 15.2 已经就绪的部分

1. **生产端推理通路已存在**：`ChaitePolicyDriver.FromEnvironment` 载入导出的策略，
   写进**和 replay 相同的移动通道**，并刻意**不碰武器/瞄准**（留给公式脚本）。
2. **训练/部署的观测偏差是被测试钉住的**：插件用自己的 `BuildChaiteObservationRow`
   原生构造观测，`ChaiteObservationMatchesThePythonFixture` 用 **200 条真实录制行**
   比对，max |C# − Python| = **9.5e-07**；`ExportedPolicyDriverRefusesAMismatchedWindow`
   另外守住宽度不匹配。全部 **PASS**。
3. 策略是**反应式**的（2×128 MLP，无循环、无 frame-stack），所以跨 DPS 节奏迁移
   的可能性较高，而不是背下了 tick 级时序。

### 15.3 还挡在"可用"前面的四件事

1. **武器/攻击没有被训练。** 策略从不学打伤害。部署时靠手工武器逻辑补，而它在训练里
   只贡献了 0.21%。**"策略躲 + 公式打"这个组合能不能在非模拟的真实战斗里打死 BOSS，
   目前没有任何证据。**
2. **生产端的公式表闸门仍然会拦住策略实际飞行的那一段。** 我这次只对
   **桥接激活时**（`_replay != null`）绕过闸门。真实战斗没有桥接，所以
   `CombatPlanner.cs:791` 依然会在 BOSS 进入 state 8 / sequence 1 时**交还操作权**——
   而那正是终局的主导状态。**也就是说今天这个策略根本走不通已审核的那条路。**
3. **DPS 节奏塑造了战斗。** `simDps` 固定 1200，所以每场训练的血量阈值跨越时刻都一样。
   真实武器 DPS 不同，阶段时机会平移。反应式策略应该能适应，但**未经验证**。
4. **可靠性只有 12.2%。** 以"无伤"为目标，平均要约 8 次才有一次无伤。它确实在升
   （旧组最好 4.7%），但还不足以信任。

### 15.4 一句话

这 53 场无伤是"**躲闪可学、而且现在真的在学**"的强证据——这是最难的一半，也正是
截断修复起作用的地方。但它们是**干净的一次躲闪，不是一套完整战斗策略**。
补齐顺序应是：先**重审公式表**让它覆盖策略真正用到的状态，再**验证武器通路能在非模拟
条件下打死 BOSS**，最后把无伤率推到远高于 12%。

## 16. 落地阻塞的修复与"只接管走位"的落实（2026-09-22 09:xx–10:xx）

训练已按要求**暂停**（所有 Terraria / DesktopHost / python 进程已退出，三个 checkpoint
已备份并逐字节校验）。这一节记录停机期间改的东西。

### 16.1 一个此前没发现的部署阻塞：导出策略根本装不进插件

`src/Chaite.Core/ExportedPolicy.cs` 把 `ObservationCount = 98` /
`ActionCount = 12` 当成**编译期常量**在 `Parse` 里强制校验。而训练出来的
checkpoint 是 **121 宽 / 24 动作**，所以：

- `export_policy.py` 导出的文件，插件在 `Parse` 阶段就会拒绝；
- 也就是说 **15.2 里说的"生产推理通路已就绪"是错的**——通路在，但它只认 98/12。

同时 `export_policy.py` 自己也是坏的：`checkpoint_layout` 从来没搜过 `jump` 块
（`layout()` 支持但默认 0），`detect_blocks` 返回值从 2 个变 3 个之后没同步解包，
`column_map` 少传参数。**这个脚本在本次之前无法导出任何 121 宽的 checkpoint。**

修法（宽而不是窄）：

- `ExportedPolicy.Parse` 不再校验观测宽度，只要求 `outputs ∈ {12, 24}`；
  宽度校验移到 `ChaitePolicyDriver.RequireObservationCount`，由它对着
  **配置出来的** observation builder 比。
- `_logits` 从固定 12 改成按文件最后一层分配；`Forward`/`ChooseAction` 用
  `ObservationDimension`/`OutputCount`。
- 新增 `DecodeAction(actionCount, ...)`：12 → 无 down 键的旧语义；24 → 含 down 键；
  其它宽度抛异常。
- `ChaitePolicyDriver.ApplyAction` 从 `static` 改成实例方法，读 `_policy.OutputCount`，
  真正把 `plan.Drop = down`（此前硬编码 `false`）、`plan.Jump = up`。
- `export_policy.py` 的 `PLUGIN_ACTION_COUNT = 12` → `PLUGIN_ACTION_COUNTS = (12, 24)`，
  警告文案改成"必须用匹配的观测配置启动插件"。

### 16.2 引擎内验证：策略确实装进去了

第一次"部署彩排"我搞错了环境变量口径：探针和插件**共用** `CHAITE_BRIDGE_FILE`，
而 `RouteReplay.LoadFromEnvironment()` 只要这个变量非空就返回一个 replay
（**哪怕 `<base>.action` 根本不存在**）。所以我那次彩排实际上是在 replay 之下
测策略，不是生产配置。

改法：探针新增 `CHAITE_PROBE_OUT` 覆盖"写到哪"，与"插件认为谁掌舵"解耦；
不设时行为与训练完全一致。

用 `CHAITE_PROBE_OUT` + **不设** `CHAITE_BRIDGE_FILE` 重跑：

```
Chaite/chaite.log:
  Exported policy loaded: ...\policy-export\fsw121d.bin [128x121, 128x128, 24x128], observation 121 wide
```

**121/24 的策略在真实游戏里加载成功**——这是 16.1 那个阻塞被真正解除的证据。
同一轮 40 场：34 胜（85%）、5 负、1 超时。

导出本身也复核过：2000 条随机 + 2000 条真实观测行，与 SB3 greedy argmax
**2000/2000 全部一致**，判定 `EXPORT MATCHES SB3`。

### 16.3 公式表闸门：修好了，但**在这一轮彩排里没被触发**

`Runtime.cs` 的旁路原本只认 replay：

```csharp
if (_replay != null) { ...忽略归还请求... }
```

已改成

```csharp
private static bool MovementAuthorityIsExternal =>
    _replay != null || _exportedPolicy != null;
```

并补了一处**必要的**兜底：`NewPlan(snapshot)` 把 `TargetKey` 留在 `-1`，只有成功路径
才赋值；公式表闸门是**提前 return** 的，所以被拦下的 plan 没有目标，策略会拿到空目标
**原地不动**。现在在策略分支里按 `SupportedBossPolicy.IsSupportedBossType` 自己解一个
Boss 目标。

**诚实交代：**`_replay != null` 这一支此前已在引擎内验证过（新组验证轮里出现过
`训练桥接：忽略公式表归还请求`）。`|| _exportedPolicy != null` 这一支**在彩排里没有
触发过一次**——192 场（polA）+ 192 场（polB）里闸门一次都没开。

### 16.4 为什么彩排里闸门不开：我一开始把 DPS 设错了

训练里闸门确实会开（新组每轮 0–7 次）。对比发现差别在**战斗时长**：

| | 中位 ticks | 最大 ticks |
|---|---|---|
| 训练 `bridge-fsw121d` | 4,037 | 4,349（撞上限） |
| 彩排 polA/polB（`CHAITE_SIM_DPS=1200`） | 2,579 | 2,730 |

`CHAITE_SIM_DPS=1200` 时 BOSS 在 ~2,600 ticks 就被打死，**还没走到会触发闸门的
AI 状态**；训练里没设这个覆盖，实际是 600–1200 随机，BOSS 活过 4,349 的回合上限，
于是进得去那些状态。

所以 polA/polB 的 0 次触发**不能**用来证明旁路正确，只能说明我把场景设偏了。
已按训练口径（`CHAITE_SIM_DPS=600`、`-MaxTicks 4400`）重跑 polC（带策略）/
polD（不带策略的对照），目的就是让闸门真的开一次：polC 应打印
`走位已由导出策略接管：忽略公式表归还请求` 且战斗继续，polD 应走
`安全条件持续丢失，已自动归还操作权`。

### 16.5 "只接管走位"落实到代码

按"武器/攻击交给用户，我们只接管走位"的口径清理了历史遗留：

- 新增 `DropNonMovementCommands(ref plan)`，在 `ApplyPlan` **之前**清掉
  `Fire` / `QuickHeal` / `QuickMana` / `QuickBuff` / `PreferredWeaponSlot` /
  `WeaponIssue` / `TargetKey` / `OutputRouteKind` / `ExpectedWeaponId` /
  `ExpectedAmmoId` / `ExpectedProjectileId`。位置是刻意的：replay 和导出策略
  已经把**移动**通道写完了，这一步不碰任何移动通道。
  这也让 `plan.TargetKey` 彻底变成"策略自己解目标"的事，而不是"我要开这一枪"。
- 删掉 `EnsureCombatWeaponSelected` 整个调用与函数：它会给玩家**切快捷栏**，并在交接
  期间 `ClearCombatControls`——后者等于**压制用户自己的攻击输入**，与"攻击交给用户"
  直接冲突。连带删掉 `CombatWeaponSelection` 字段及其 Reset。
- 删掉 `plan.WeaponIssue` 的"自动射击暂停"提示：既然不开火，这句话是在描述一个
  我们没做的动作。
- `CombatWeaponSelectionHandoff` 类**保留**在 `Chaite.Core`：它是带独立回归的契约，
  删它是另一个决定，不该混在这次里。

`tools\build.ps1` 退出码 0，测试 **795 通过 / 0 失败**。

### 16.6 一次自伤：把 Runtime.cs 写坏了，已完整修复

清理 `_weaponIssueCooldown` 时我用了 `Get-Content -Raw` + `Set-Content`，
在 PS 5.1 下这组 cmdlet 默认走 ANSI(GBK)，把 **30 处中文字符串的结尾引号**
抹成了 `?`，文件变成非法 UTF-8。已逐点还原（文案逐行对照 `git show HEAD`），
重新构建 + 全测试通过。机制、复现、规则单独写在
`docs/powershell-encoding-incident-2026-09-22.md`，并已加进铁律。

受损原文件留在 `src/Chaite.Plugin/Runtime.cs.damaged-backup`，可复核。

### 16.7 当前状态

- 训练：**停止**。checkpoint `fsw121d` 2,324,720 步 / `fsw121p` 2,250,054 /
  `fsw121q` 2,301,880，均与 `artifacts/_ckpt-backup/` 一致。
- 停止前新测量组结果：`fsw121d` 436 场，0.62 次/千 tick，胜率 88.5%，无伤 12.2%；
  `fsw121p` 1.07 / 68.9% / 10 场；`fsw121q` 2.34 / 4.0% / 0 场。
- 验收线（猪鲨无伤 80% + 胜率 90%；光女无伤 80%）**远未达到**：胜率接近，无伤率差很远。
- 待办：polC/polD 的闸门对照结论；武器/攻击从未参与训练（真实武器只贡献 BOSS 伤害的
  0.21%）；`simDps` 调度会平移阶段时机；`pc`/`pr` 的 `hostile` vs `!friendly` 口径不一致。

## 17. 生产端的弹幕窗口喂给策略的是**训练分布的反集**（2026-09-22 11:xx）

### 17.1 口径

探针（训练数据的唯一来源）在 `tools/GameProbe.cs` 的 `CaptureHostileProjectiles` 里
是这么筛的：

```csharp
if(projectile==null || !projectile.active || !projectile.hostile ||
    projectile.friendly) continue;
```

即 **`active && hostile && !friendly`**。`pr` 窗口和 `pc` 计数都出自这一个函数，
所以 `pc` 和 `pr` 彼此是一致的。

而插件生产端 `TerrariaFacade.BuildChaiteObservationRow` 是：

```csharp
// The probe's own window keeps every active non-friendly projectile,
// including the ones that are not hostile ...
if (_projectileHostile(projectile)) continue;
```

**`hostile` 为真就跳过**——也就是只保留**非敌对**的射弹，把每一个敌对射弹都丢掉。
这正好是训练分布的反集。注释里那句"探针保留所有非友方、包括非敌对的射弹"是**错的**，
探针从来不保留非敌对射弹。

后果：生产环境里策略看到的弹幕窗口装的是布景，而**光女的弹幕一个都不在里面**。
离线训练得再好，上生产就瞎了。这比 16.3 那道公式表闸门严重得多——闸门只是偶尔挡一下，
这个是**每一个 tick 都在喂错输入**。

### 17.2 为什么一直没有测试发现

一致性测试 `ChaiteObservationMatchesThePythonFixture` 比的是**格式**：它拿探针
已经写好的 `pr` 数组（`ProjectilesFromBridgeRow`）重新填一遍，再看 C# 和 Python
的结果是否一致。**它从不自己从游戏里收集窗口**，所以"收集谓词"这一段根本没有被执行过。

`_projectileFriendly` 这个 getter 在生产代码里**压根不存在**，也就无从被断言。

### 17.3 修复与不可为 no-op 的对照

- `TerrariaFacade` 新增 `_projectileFriendly` 反射 getter（绑定 `Projectile.friendly`）。
- 筛选改成与探针逐字一致的 `!hostile || friendly → skip`。
- 新增回归 `ProjectileWindowFiltersHostileAndNotFriendly`，断言两件事：
  1. facade 绑定了 `_projectileFriendly` 字段；
  2. `BuildChaiteObservationRow` 里**同时**读了 `_projectileHostile` 和
     `_projectileFriendly`。

**对照（不是 no-op 的证明）**：把筛选临时改回旧的一行 `if (_projectileHostile(...)) continue;`
重新构建后，该测试**失败**（`通过 795，失败 1`）；恢复修复后 `通过 796，失败 0`。

（第一版测试我把方法名猜成 `ReadTargetsAndThreats`，它当场就失败了——说明断言是活的，
不是永远为真的空壳。实际收集在 `BuildChaiteObservationRow`。）

### 17.4 顺带查清、但**没改**的一件事：排序度量

探针按 `Vector2.DistanceSquared(center, playerCenter)`（欧氏）排序；
`ChaiteObservation.SelectProjectiles` 按**曼哈顿**距离排序。把两个 fixture 流里
1,872 条含 ≥2 个射弹的行拿出来核对记录顺序：

| 与哪种度量一致 | 行数 |
|---|---|
| 两者都一致 | 1,816 |
| 只与曼哈顿一致 | 4 |
| 只与欧氏一致 | 9 |
| 两者都不一致 | 43 |

那 43 行是**类型折叠**（`CHAITE_PROJ_COLLAPSE`）造成的，不是度量问题——折叠后窗口
本来就不是纯距离前缀。剩下 4 : 9 的差异（0.7%）方向还偏向欧氏，但样本太小，
不足以推翻现有注释和测试所依据的证据。

**结论：记录在案，暂不改。** 改排序度量会动 Core，且可能把已经对的 1,816 行弄坏；
要改应该先单独设计一个用**未折叠**流做判据的实验。

## 18. 真正的大问题：导出策略**从来没有飞过一次**（2026-09-22 11:xx）

### 18.1 症状：两条"不同"的臂给出完全一样的数字

彩排做 A/B 时，我反复拿到**逐条相同**的结果：

| 对比 | 带策略 | 不带策略（对照） |
|---|---|---|
| polA / polB | 192 场无伤 | 193 场无伤 |
| polG / polH | 18 场，全是 `FailedAfterDeath` | 18 场，全是 `FailedAfterDeath` |

两次独立实验、两组不同参数，带不带策略数字几乎一模一样。**这不是"策略没用"，这是
"策略没跑"。** 我当时把第一组当成"公式脚本也很强"，这是错的。

### 18.2 根因：`Apply` 和 `Observe` 的调用顺序

`ChaitePolicyDriver` 的契约写在代码里：

```csharp
public void Observe(object player, in TargetSnapshot target, long tick)
{
    ...
    _pending = next;
    _pendingTick = tick;          // 记下"这一 tick 的观测"
}

public bool Apply(long tick, ref ControlPlan plan)
{
    if (_pending == null || _pendingTick != tick - 1L)   // 只接受上一 tick 的观测
    { _waits++; return false; }
    ...
    _actions++;
    ApplyAction(action, ref plan);
    return true;
}
```

而 `Runtime.cs` 是这么调的：

```csharp
_exportedPolicy.Observe(player, in policyTarget, tick);   // _pendingTick = tick
_exportedPolicy.Apply(tick, ref plan);                    // 要求 _pendingTick == tick-1 -> 永远为假
```

**同一个 tick 里先 Observe 再 Apply，`Apply` 的条件永远不成立**：它每次都走
`_waits++` 并返回 `false`，plan 原封不动，公式脚本继续掌舵。策略文件加载成功、
日志打印成功、`Observe` 每 tick 都在建观测行——**只有"写回动作"这一步从来没发生**。

正确顺序是 **先 `Apply` 再 `Observe`**：

```csharp
_exportedPolicy.Apply(tick, ref plan);                     // 用 tick-1 的观测决定 tick 的动作
_exportedPolicy.Observe(player, in policyTarget, tick);    // 这一 tick 的观测留给 tick+1
```

这样配对才和训练一致：tick T-1 的行决定 tick T 的动作。

### 18.3 为什么它能藏这么久

因为它**不报错、不崩溃、不改变胜率**——公式脚本本身能打赢猪鲨，所以彩排照样出胜利，
日志照样干净。`ChaitePolicyDriver` 里其实有 `AppliedActions` / `WaitedTicks` 两个计数器，
**但没有任何地方把它们打出来**。一个"加载成功但从未生效"的组件在日志里是完全静音的。

**修法（两处）**：

1. `Runtime.cs` 交换调用顺序，并在注释里写明这个顺序是 load-bearing 的。
2. 会话结束时打印
   `Exported policy flight: applied=N waited=M`。
   **`waited` 一直涨而 `applied` 停在 0 就是这件事的签名**，以后一眼可见。

### 18.4 引擎内证据（修复后）

`Chaite/chaite.log`：

```
Exported policy flight: applied=46683 waited=6
Exported policy flight: applied=54479 waited=7
Exported policy flight: applied=62258 waited=8
...  applied=85277 waited=11
```

`applied` 在涨、`waited` 几乎不涨 —— 策略**真的在飞**。（修复前这里会是 `applied=0`。）

同一轮里还第一次看到了 16.3 想验证的那条消息：

```
CHAT [拆特] 走位已由导出策略接管：忽略公式表归还请求（猪鲨原生 AI 状态/时钟不在已审核公式表）。
```

**所以 16.3 的旁路在"导出策略"这一支上也被证实了**：闸门开了 8 次，战斗继续，没有
`安全条件持续丢失`。

### 18.5 A/B 结果（同一场景、同一 seed、`CHAITE_SIM_DPS=600`、`-MaxTicks 16000`）

| | 场次 | 中位 ticks | 结果 |
|---|---|---|---|
| **polI** 导出策略（已修顺序） | 25 | 8,234 | **19 未死亡胜** + 1 死后胜 + 5 负 |
| **polJ** 对照（无策略） | 35 | 5,785 | **35 负，0 胜** |

- 策略：**20/25 胜（80%）**。
- 公式脚本：**0/35 胜**。

> **更正（见 §20）**：上面"19 未死亡胜"当时被我写成了"19 无伤胜"，并把 76% 当成无伤率报出去。
> `SuccessNoDeath` 是"没**死**"，不是"没**挨打**"。真正的无伤率（`hits == 0`）是 **0%**。
> 胜率那部分（80% vs 0%）是对的，无伤率那部分是错的。

这是**第一次**证明导出的策略不仅会飞，而且**明显强于公式脚本**。此前所有"部署彩排"
的胜利数字（polA 的 34/40 等）都是公式脚本打出来的，与策略无关。

### 18.6 对既有结论的影响

- 15.3 列的四条阻塞里，第 2 条"生产端闸门拦住策略飞行的那一段"**现在才真正验证完毕**
  （旁路 + 顺序两处都修了）。
- 但 15.2 说的"生产推理通路已就绪"要再降一级：通路**刚刚**才第一次跑通，之前是死的。
- 16.2 里"121/24 策略加载成功"仍然成立，但那只证明**加载**，不证明**生效**。
- **验收线（猪鲨无伤 80% + 胜率 90%）**：本次 76% / 80%，已经很接近，但样本只有 25 场，
  且 `CHAITE_SIM_DPS=600` 与训练的 600–1200 随机并不完全一致。要下结论得跑长。

## 19. 恢复训练：六套配装（2026-09-22 12:18 起）

### 19.1 范围（已与用户确认）

"两个BOSS四套配装"经确认取**全部六套**，逐套按原口径验收：

| 配装 | tag | 启动时步数 |
|---|---|---|
| 猪鲨 strong-wing | fsw121d | 2,324,720 |
| 猪鲨 strong-wing（候选） | fsw121p | 2,250,054 |
| 猪鲨 strong-wing（候选） | fsw121q | 2,301,880 |
| 猪鲨 fairy-wing | ffw121 | 444,951 |
| 猪鲨 trusty-chillet | fch121 | 415,141 |
| 猪鲨 lilith-wolf | flw121 | 408,786 |
| 光女 strong-wing | esw121 | 388,640 |
| 光女 broom | ebr121 | 410,928 |

六套配装全覆盖；strong-wing 多跑两个候选（p/q）只为在 80% 无伤这条最难的线上多一个选择，
不改变"六套配装"的口径。

### 19.2 把 dodge 目标推广到另外五套（**这是一处有意的口径改动**）

`fsw121d/p/q` 带 `dodgeObjective`，另外五套**没有**——它们是 2026-09-21 用"以胜利为中心"
的奖励启动的，只跑到约 40 万步。而验收线是**无伤率**，manifest 自己的测量已经写明
以胜利为中心的奖励不优化闪避：

> obsb1 的胜率在训练中从 2.32% 升到 32.05%，而每场被击中次数从 5.61 升到 6.69，
> 所以以胜利为中心的奖励优化的是存活时间，不是闪避。

让六套里的五套继续用"优化存活时间"的奖励，等于**照着和验收口径不同的目标训练它们**。
所以 `sessions.json` 里给 `ffw121/fch121/flw121/esw121/ebr121` 补上 `dodgeObjective: true`，
并在 `note` 里记了这段理由。这些臂从既有权重热启动，因此是"40 万步之上的奖励切换"，
不是从头开始。原 manifest 已备份到 `artifacts/_sessions-backup-*.json`。

### 19.3 自动续跑：**不能**用 `extend-sessions.ps1` / `night-watch.ps1`

这两个脚本会**悄悄用错误的参数重启训练**：

- `extend-sessions.ps1` 自带一份**硬编码的旧会话表**（旧 tag、`ObsWorldBound = 12.0`），
  并且**手工拼命令行**，因此丢掉 manifest 里所有奖励与观测参数——`hurtFactor`、
  `noHitBonus`、`dodgeObjective`、`projSlots`、`simDps` 一个都不带。
  用它续跑当前任何一个臂，都会让 checkpoint 在一个**不同的观测和奖励**下继续，
  直接毁掉这一轮的可比性。
- `night-watch.ps1` 调的正是 `extend-sessions.ps1`，所以同样不安全（它的 tag 表也是旧的）。

新增 `training/watch-campaign.ps1`：它**重新调用 `start-campaign.ps1`**，
而后者完全从 `training/sessions.json` 取参数、跳过正在跑的 tag、只在给了 `-Resume` 时
才续跑已有 checkpoint。这样**续跑路径就是启动路径本身**，参数没有第二个地方可以漂移。
脚本**纯 ASCII**（`powershell.exe -File` 按 ANSI 读 `.ps1`，含中文的脚本在运行前就已经坏了）。

### 19.4 新的测量组

探针与插件的改动（`CHAITE_PROBE_OUT` 解耦、锁步改由 `CHAITE_BRIDGE_FILE` 判定、
`Apply`/`Observe` 顺序、弹幕窗口谓词）都改变了哈希，所以本轮开启新组：

| | 新组（2026-09-22 12:18） | 旧组（2026-09-21 22:3x） |
|---|---|---|
| `InputCoreSha256` | `E787C9696EF4217F47FB898BCC8A9C269750DDC97B290F8D5E1CEB1F2D20B1AA` | `66C9B1A0…` |
| `InputPluginSha256` | `A6DFFC36B8198F9E0B2472F67243B23D1C5E9DA6B1EBD70285B07C026879251F` | `2E902B93…` |
| `ProbeSourceSha256` | `754A04F04704DAEE1B617216B5132617338F10AB53B669DF81A70E5D16772CB3` | `D04AEB32…` |

两个组的 `InputCoreSha256` 不同，符合铁律。**新旧组的数据不可混算。**

### 19.5 启动状态

- `start-campaign.ps1 -Tags <8 个> -Resume -Rounds 12`，8 个 tag 全部启动。
- 启动后实测：`run-session.ps1` 进程 8 个、`Terraria` 进程 8 个、`python` 12 个
  （8 个 trainer + 辅助）。32 逻辑核 / 14.7 GB 可用内存，资源充足。
- 各臂 episode 计数已在增长（fsw121d 6,849 / fsw121q 7,624 / ebr121 1,442 …）。
- `training/watch-campaign.ps1 -Hours 20 -Rounds 12` 已挂为监视窗口，每 10 分钟一轮：
  发现掉线的臂就按 manifest 参数续跑。

## 20. 更正：我把 `SuccessNoDeath` 当成了"无伤"（2026-09-22 15:xx）

### 20.1 错在哪

§18.5 与 §19 里我把彩排的 `SuccessNoDeath` 读成"无伤胜"，据此报出"无伤 74%／76%"。
**这是错的。** 这两个词在数据里是两件不同的事：

| 字段 | 含义 |
|---|---|
| `outcome == "SuccessNoDeath"` | 赢了，且**没死过** |
| `hits == 0` | 全程**一点血都没掉**，也就是验收口径的"无伤" |

`hits` 的定义在 `tools/GameProbe.cs:4079`，是**唯一**的递增点：

```csharp
bool lostLifeThisFrame=p.statLife<lastLife;
if(lostLifeThisFrame) { hits++; episodeHits++; }
```

所以 `hits == 0` 确实是"整场没有掉过血"，口径没问题；**是我读错了字段**。

### 20.2 更正后的真实数字

| | 场次 | **无伤%（`hits==0`）** | 胜率 | 每场挨打 | 每千 tick |
|---|---|---|---|---|---|
| 训练 fsw121d（新组全量） | 1,567 | **7.0%** | 46.5% | 4.65 | 1.407 |
| 部署 polI（策略，DPS 600） | 118 | **0.0%** | 81.4% | 4.17 | 0.517 |
| 部署 polJ（对照，无策略） | 164 | **0.0%** | 0.0% | 6.76 | 1.169 |

**结论完全反转**：无伤率不是"已经接近 80%"，而是 **0%（部署）/ 7%（训练）**。
我之前那句"验收线已经很接近"是错的，实际差距极大。

**仍然成立的部分**：策略在**胜率**上确实压倒公式脚本（81.4% vs 0%），
而且每千 tick 挨打次数只有对照的 44%（0.517 vs 1.169）——策略真的在闪避，
只是"整场一滴血不掉"这个标准目前还远远达不到。

### 20.3 教训

- **报告指标前先确认字段语义。** `SuccessNoDeath` 里的 "Death" 是"死亡"，
  而验收要的是"无伤"，两者在猪鲨这种接触伤害密集的 Boss 上差距是 7 倍量级。
- 这也解释了为什么这个项目的"clean"一直是个位数：`hits == 0` 要求整场
  **零掉血**，而上一代 34,692 场里只有 **3** 场做到。80% 无伤是一条非常高的线，
  不是"再训练一会儿就到"的线。

### 20.4 当前（15:xx）各臂状态

| arm | 胜率（近期轮） | 无伤% | 每千 tick 挨打 | 中位 tick |
|---|---|---|---|---|
| fsw121d | 85–92% | 11–18% | 0.47–0.70 | 4,340 |
| fsw121p | 68–89% | 0–17% | 0.87–1.04 | 4,340 |
| fsw121q | 0–38% | 0% | 1.31–2.35 | 3,100–4,065 |
| ffw121 | 0% | 0% | 4.2–4.9 | 1,600 |
| fch121 | 0% | 0% | 4.6–5.3 | 1,700 |
| flw121 | 0% | 0% | 5.1–5.5 | 1,600 |
| esw121 | 0% | 0% | 2.1–2.5 | 916 |
| ebr121 | 0% | 0% | 2.5–3.1 | 872 |

值得注意的两点：

1. **`fsw121q` 崩了**（0–38% 胜）而同配装的 d/p 是 85%+。q 是 `obsAgg`
   （把 4 个 Boss 邻近特征换成射弹密度聚合）的宽度对照臂，看起来那 4 个聚合特征
   不是中性的，而是**有害**。
2. **五套非 strong-wing 配装全是 0% 胜**，而且 BOSS 几乎没掉血就死了玩家
   （猪鲨 p50 剩 54,223，光女 p50 剩 88,700）。它们刚从"以胜利为中心"的奖励
   切到 dodge 目标约 2.5 小时，需要更长时间；但"BOSS 还剩九成血"比"还在学"更糟。
3. **光女两臂的 episode 长度几乎恒定**（p50 872 / 916 tick，min 303 max 3,831），
   说明玩家是在很固定的时间点被秒——像是固定吃了某一招，而不是被逐渐磨死。

---

## 21. 意外中断后的重启（2026-09-22 16:42–16:51）

### 21.1 中断取证

机器意外中断后清点：Terraria 0、DesktopHost 0、python 0、launcher 0。八个
checkpoint 全部完好、可读，没有半写坏的 zip：

| arm | 中断时步数 | checkpoint 时间 |
|---|---|---|
| fsw121d | 2,604,941 | 16:23 |
| fsw121p | 2,558,051 | 16:25 |
| fsw121q | 2,576,414 | 16:27 |
| ffw121 | 750,245 | 16:20 |
| fch121 | 718,622 | 16:26 |
| flw121 | 697,004 | 16:27 |
| esw121 | 679,407 | 16:22 |
| ebr121 | 699,359 | 16:17 |

### 21.2 重启路径

```
& training\start-campaign.ps1 -Tags @('fsw121d','fsw121p','fsw121q',
    'ffw121','fch121','flw121','esw121','ebr121') -Resume -Rounds 12
```

先用 `-WhatIfOnly` 核对过计划：八个臂全部 `resume=True`，且 `bound=18 slots=12
sort=threat collapse=True hurt=400 clean=1200000 dodge=True`、`simDps=1200` 与
中断前一致——重启路径与启动路径同为 `start-campaign.ps1`，参数只从
`training/sessions.json` 读，所以不存在第二处可以漂移的地方。

结果：8/8 started（16:42:05–16:45:11，20 s 错峰），随后核实
`Terraria=8`、`run-session=8`、`train.py=8`（每个 session 另有 1 个同命令行的
子进程，属正常）。八条 `bridge.obs.jsonl` 全部在增长，八个
`game-probe-rt-<tag>-*` 目录里的 `game-probe.log` / `boss-observations.jsonl` /
`prehit-observations.jsonl` 全部在写。

### 21.3 一个必须记住的取证陷阱：`dir` 会说 obs 流是空的

重启后第一次核查时，`Get-ChildItem` 与 `cmd /c dir` 都报
`bridge-fsw121d\bridge.obs.jsonl` **长度为 0、时间戳停在 round 开始的
16:42:06**，而同一时刻 `bridge.action` 与 `bridge.episodes.jsonl` 都在更新、
episode 还在赢（e=12 `SuccessNoDeath` ticks=4348）。差点被误判成"观测流断了"。

真相是**目录项缓存没刷新**：该文件在 round 开始时被
`run-session.ps1:140` 的 `New-Item -ItemType File -Force` 重新创建，之后由探针
以 `FileMode.Append` 打开，`Get-ChildItem` / `dir` 读到的仍是创建时的 0。

判据必须用 .NET：

```powershell
(New-Object System.IO.FileInfo $path).Length
```

同一文件 .NET 报 `Length=26750118`、`LastWriteTime=16:50:56`，即在增长。

**规则：判断 `*.obs.jsonl` 是否在写，只认 `System.IO.FileInfo`；`dir` /
`Get-ChildItem` 的 `Length` 在这个目录上不可信。**

### 21.4 监视窗口

以独立进程挂上 `training/watch-campaign.ps1`（不是 harness 后台作业，这样
harness 退出也不会带走它）：

```
powershell.exe -File training\watch-campaign.ps1 `
    -Tags fsw121d,fsw121p,fsw121q,ffw121,fch121,flw121,esw121,ebr121 `
    -Hours 20 -Rounds 12 -IntervalSeconds 600 -CooldownMinutes 20
```

pid 218260，输出重定向到 `artifacts/watch-campaign.log`，首行即
`[16:51:29] alive 8/8`。它会每 600 s 复查一次，发现某个 tag 的
`run-session.ps1` 消失（round 用尽、崩溃、被 OOM 杀掉）就再次调用
`start-campaign.ps1 -Resume`，所以 round 预算耗尽不会让campaign 停摆。

注意 `-File` 传带空格的脚本路径必须自己加引号：`Start-Process -ArgumentList`
不会替你转义，第一次尝试即因此报"文件没有 .ps1 扩展名"。

---

## 22. dodge 目标的奖励倒挂（2026-09-22 17:00）——本阶段最严重的训练缺陷

### 22.1 触发

重启后第一次基线（`training/campaign-acceptance.py --last 2`，按行位置分 round）
读到五个臂全部 0% 胜、而且死得极快：

| arm | 配装 | real | 胜率 | 无伤率 | hits/1kt | 中位 tick | BOSS 中位已掉血 |
|---|---|---|---|---|---|---|---|
| fsw121d | strong-wing | 202 | 66.8% | 6.9% | 0.690 | 4,219 | 77,980 / 78,000 |
| fsw121p | strong-wing+prox | 185 | 72.4% | 2.7% | 0.988 | 4,345 | 77,980 |
| fsw121q | strong-wing+agg | 43 | 0.0% | 0.0% | 1.360 | 3,221 | 55,381 |
| ffw121 | fairy-wing | 56 | 0.0% | 0.0% | 4.631 | 1,514 | 21,284 |
| fch121 | trusty-chillet | 157 | 0.0% | 0.0% | 5.244 | 1,690 | 25,224 |
| flw121 | lilith-wolf | 293 | 0.0% | 0.0% | 4.937 | 1,455 | 20,060 |
| esw121 | empress strong-wing | 40 | 0.0% | 0.0% | 0.978 | 1,022 | 11,400 |
| ebr121 | empress broom | 7 | 0.0% | 0.0% | 5.601 | 1,964 | 30,240 |

签名很明确：hits/1kt 4-5 而中位 episode 只有 1,500-1,800 tick。玩家不是在
"学不会闪避"，而是在**用最快速度去死**——BOSS 只掉了两三成血。

### 22.2 缺陷：dodge 目标付钱让策略去死

`chaite_env._reward` 的每帧命中项是

```python
reward -= (self._prev_life - life) * DODGE_HIT_PENALTY
```

注意乘的是 **life_lost**，不是 1。按实测每命中帧掉 64-96 点生命（取 80），
`DODGE_HIT_PENALTY=2000` 时**一次命中 = 160000**，而不是注释里对比的 2000。

而 dodge 目标的终局项是 win=6000 / clean=40000 / death=-300 / timeout=-1000
（都在 `REWARD_SCALE=1e-3` 之前）。于是（后乘 scale，单位"点"）：

| 结局 | 分数 |
|---|---|
| 无伤胜 | +46 |
| 1 次命中后胜 | **-154** |
| 5 次命中后胜 | **-794** |
| 9 次命中后胜 | **-1434** |
| 0 命中死亡 | **-0.3** |
| 1 次命中后死亡 | -160.3 |

**任何"至少命中 1 次"的胜利都比直接去死更差。** 更糟的是，同一段注释在表格
上方两行写着 "death -300 (still worse than any win, at every hit count)"——
这句话被它自己下一段的表格直接推翻，而表格打印出来后没有被核对。

同一段注释还写 "no-hit +40000 (the jackpot, and it must EXCEED one hit's cost)"。
实际 jackpot 是 40000，一次命中是 160000，**只有 1/4**，不是 20 倍。作者把
"per hit -2000" 当成了整次命中的代价，而代码乘的是 life_lost。

这解释了 22.1 的全部签名：命中密度 4-5/1kt 的策略，多活 1000 tick 要付
400-500 点，而赢只给 6 点；死掉只付 0.3 点。**梯度指向死亡。**

顺带暴露的第二个倒挂：timeout=-1000 比 death=-300 **更差**，而设计声明的顺序
是 `clean win >> win >> timeout >> death`（`chaite_env.py:125-126`）。

### 22.3 对照（control）：`training/check_reward_ordering.py`

不能靠读常量下结论——这次的错误恰恰就是"读了常量但没算乘法"。所以新增一个
**驱动真实 `_reward` 函数**的对照脚本：它造一个临时 obs 文件、实例化
`ChaiteBridgeEnv`、对每个结局调用同一个 `_reward`，然后断言五条不变量：

1. 任意胜利（≤40 次命中）都优于任意死亡（≤40 次命中）；
2. 无伤胜严格优于 1 次命中的胜利；
3. jackpot > 一次命中的代价；
4. 胜利阶梯在命中数上严格递减；
5. timeout 优于死亡；且安全中止不被罚到死亡以下。

它**不**从常量重算，所以不会和被测代码漂移。非空转证明：对修复前的常量运行
`EXIT=1`，报 2 条违规（"the worst win (40 hits) scores -6394000.02 but the best
death scores -300.02" 与 "a timeout (-1000.02) is not better than dying
(-300.02)"）；对修复后的常量 `EXIT=0`，六行全 OK。

### 22.4 修复

保持 dodge 目标的要害——每次命中 160 的收费——不动，把三个终局项抬到让顺序
真正成立（最坏情况取实测密度能产生的 40 次命中）：

| 常量 | 旧 | 新 |
|---|---|---|
| `DODGE_HIT_PENALTY` | 2000 | **2000**（不变，一次命中仍是 160） |
| `DODGE_WIN_REWARD` | 6000 | **6000000** |
| `DODGE_NO_HIT_BONUS` | 40000 | **4000000** |
| `DODGE_DEATH_REWARD` | -300 | **-2000000** |
| `DODGE_TIMEOUT_REWARD` | -1000 | **-1000000** |

修复后的表（`REWARD_SCALE` 之前）：

```
无伤胜      9,999,999     1 命中胜   5,840,000    9 命中胜  4,560,000
20 命中胜   2,800,000     40 命中胜   -400,000    timeout  -1,000,000
0 命中死亡 -2,000,000     40 命中死亡 -8,400,000
```

于是：**任何胜利（哪怕 40 次命中）都优于任何死亡**；timeout 优于死亡；无伤胜比
1 命中胜高 4,160,000 = **26 次命中的风险**；jackpot 是单次命中代价的 **25 倍**。
最后两条是"零命中可被找到"而不是抽奖的原因：一次命中在手时，去争取零命中必须
被付得比一次命中的风险更多，而修复前只付了四分之一个。

`chaite_env.py` 里那段自相矛盾的注释已整体重写，把错误表和它的推翻过程一起留在
原地——这类"注释宣称的不变量与表格不符"是本项目第三次栽在同一个坑里
（HURT_FACTOR=40000、2e6 罚分草案、以及这次），所以现在有不变量对照脚本兜底。

### 22.5 停机与重启（17:00–17:09）

1. 先杀监视窗口（pid 218260），否则它会在我停机途中把臂重新拉起来。
2. `training/stop-all.ps1 -Tags <8>`：按进程树逐 PID 杀（不用 `taskkill /T`），
   leftover check `none`，八个 checkpoint 全部通过 zip 完整性 + 加载校验，
   并各自留了 `ppo_stop_20260922-165954.zip` 快照。
3. `start-campaign.ps1 -Resume -Rounds 12` 重启，8/8 started。
4. 重新挂监视窗口 pid 200080，`[17:08:46] alive 8/8`。

**在引擎内证明改动被执行了**（不是空转）：八个 `bridge-<tag>\ppo.log` 的
`DODGE OBJECTIVE ON:` 行全部变成

```
DODGE OBJECTIVE ON: hit=2000 win=6000000 death=-2000000 clean=4000000 timeout=-1000000
```

### 22.6 一个 round 的粒度事实

停机时八个 checkpoint 的 mtime 全部停在中断前（16:17-16:27），即重启后的
round 1（16:44-16:59，15 分钟）**一步都没存**。原因不是 bug：`train.py` 的
`checkpoint_every=20480`，但 SB3 的 `learn()` 每轮收集 `n_steps=16384`，所以
实际要到第 2 个 rollout 结束（32,768 步）才会存盘；实测 ~37 步/s 意味着
**约 14.8 分钟才第一次存盘**。我们在 15 分钟处停机，正好差一分钟。

含义：一个 round 如果在 15 分钟内被杀，这个 round 的进度全部作废。停机时机要
么 < 1 分钟，要么 > 16 分钟。

---

## 23. 修复后的核查（2026-09-22 17:40–17:55）

### 23.1 奖励管道本身是对的（差点误判成第二个缺陷）

`chaite_env.step()` 把**观测行原样**交给 `_reward`，而 `_reward` 读 `win` /
`dead` / `hits` / `et`。我只读了 `WriteBridgeObservation` 里 row 字典的**尾部**
（5420 行往后），看到的是 `done/win/st/etl/tlim`，没看到 `dead` 和 `hits`，一度
以为：

- `row.get("hits", 1)` 永远取到默认值 1 → **无伤 jackpot 永远不可能触发**；
- `row.get("dead")` 永远为 None → **每次死亡都被当成 timeout 罚分**；
- `row.get("et", 0.0)` 永远为 0 → `_degenerate` 永不归零 → **5 个 episode 后
  就抛 `GameSessionEnded`**。

这三条任意一条成立都会让整个 campaign 无效，所以逐条查了 row 字典的**头部**
（5382-5388 行），三个字段都在：

```
{"e",episodeIndex},{"t",ticks},{"et",ticks-episodeStartTick},
{"hits",episodeHits},{"pl",p.statLife},{"plm",p.statLifeMax2},
...
{"dead",p.dead},
```

**教训：这个 row 字典有 100 多行，只读尾部会得出错误的"字段缺失"结论。**
反证也在数据里：如果 `et` 真的缺失，`_degenerate` 会在第 5 个 episode 抛异常，
而实际每个 round 跑了 190-400 个 episode。

### 23.2 五个停滞臂的真实状态：熵接近最大，不是坍缩

从各自 `ppo.log` 最后一次 round 的 `entropy_loss`（上限 ln 24 = 3.178）：

| arm | 熵 | 含义 |
|---|---|---|
| fsw121d | 1.72 | 已锐化，会赢 |
| fsw121p | 1.97 | 已锐化 |
| fsw121q | 2.29 | 半锐化 |
| ffw121 | 3.08 | **接近均匀随机** |
| fch121 | 2.87 | 接近均匀随机 |
| flw121 | 3.03 | 接近均匀随机 |
| esw121 | 2.71 | 接近均匀随机 |
| ebr121 | 3.04 | 接近均匀随机 |

这和 22.2 的推论一致：倒挂的奖励下"所有动作一样烂"，advantage ≈ 0，策略
收不到梯度，于是被 `entCoef=0.001` 的微弱熵奖励按在最大熵附近不动。**所以这
五个臂不是"学坏了"，而是"从没学过"**——这正好是奖励修复能直接作用的地方。

### 23.3 已排除的其它怀疑（都不成立）

- **动作没送到游戏**：ebr121 的 `controls=` 有 6 种组合，玩家 X 在 31,884–
  33,720 之间有 613 个不同取值 → 动作确实生效。ffw121 同样。
- **探针崩溃**：`game-probe.log` 里那段 `Terraria.Projectile.Update` 调用栈是
  `ChaiteGameProbe.PlayerDeath()` **故意打印**的诊断栈，不是异常。
- **安全中止很多**：本 campaign 最近 200 个 episode 里 `Cancelled` /
  `EncounterInterrupted` 几乎不出现（ebr121 200 个里 1 个，约 0.5%），所以把
  win 从 6 抬到 6000 不会因为中止而引入偏置。§20 里那个 13-26% 是上一代的数字。
- **声明观测盒 ±4.0 与数据不符**：`OBS_BOX_LOW/HIGH` 是文档性的，SB3 只用它
  校验 `PPO.load` 的 space 一致性；所有 checkpoint 都是 ±4.0，改动反而会让它们
  加载不了。保持不动。

### 23.4 监视工具

- `training/campaign-acceptance.py`：**滚动窗口**（最近 300 / 100 个真实对局）
  的胜率与无伤率，加累计值。曾经按 `e` 重置分 round，但实测 `e` 不只在一个
  round 边界重置（当前 round 内在 e=22/33/105/43/72/17 都重置过），所以任何
  边界检测都不准；阈值型验收用滚动窗口最稳。
- `training/campaign-monitor.ps1`：每 20 分钟把上面这份报告 + 进程数 +
  各臂熵/`n_updates`/步数追加到 `artifacts/campaign-monitor.log`，跑 20 小时。
  pid 92616。这样"有没有在进步"不需要人工反复读日志。

### 23.5 光女臂的结构性难度（不是缺陷，但要记录）

`empress-day` 的 BOSS 有 98,000 血、模拟 DPS 1200，即每 tick 掉 20 血，
**BOSS 要 4,900 tick 才死**；而玩家在 ~872 tick 就被第一次接触秒掉（day 光女
伤害 9999）。也就是说"无伤胜"要求玩家在**不受任何一次接触**的前提下活满约
4,900 tick，比现在的 episode 长 5.6 倍，而 episode 只记录到 BOSS 掉 8,400–
30,240 血（8.6%–31%）就结束了。

同时光女臂的每 episode 墙钟开销很高：ebr121 一个 round 只产出约 190 个
episode，而 ffw121 约 1,270 个——**同样墙钟下光女臂的样本量少 6-7 倍**。
这两点共同决定了光女两套配装的验收会明显慢于猪鲨，需要更长的墙钟。

---

## 24. 孤儿探针：一个臂永久死亡而仍被计为"在跑"（2026-09-22 19:20-19:45）

### 24.1 症状

19:20 的监视快照显示 `run-session=8 Terraria=8`，一切正常。19:31 我去查
"引擎相对动作文件的超前量"时，在 **ffw121** 上量到：

```
engineTick=177540  actionTick=156307  LEAD=21233  ticks
```

`BridgeLagLimit = 8`，实测超前 **21,233 tick**（超标 2,654 倍）。而且
`bridge.action` 已经 71 秒没被写过，同时 obs 文件以 **188 KB/s** 增长，已经
**416 MB**。

进程普查给出了答案：

```
PID 154464  tag=fsw121d       PID 213476  tag=flw121
PID 190976  tag=ebr121        PID 156784  tag=ffw121    <-- 没有 trainer
PID 266572  tag=fch121        PID 171928  tag=fsw121p
PID 135764  tag=fsw121q       PID 230780  tag=esw121
```

- **ffw121**：launcher 和 trainer 都不存在，只剩一个 Terraria 在自由奔跑；
- **fsw121q**：trainer 和 Terraria 活着，但 **launcher 已经死了**（跑完这一
  round 就再没人接下一 round）；
- 其余 6 个臂正常。

### 24.2 为什么修不好：重启会瞬间死掉

`watch-campaign.log` 显示监视窗口在 19:30:30 确实发现并尝试重启了 ffw121：

```
[19:30:30] extending: ffw121
  ffw121   started pid=208012  duke-fishron/fishron-fairy-wing ...
[19:30:51] alive 7/8: fsw121d fsw121p fsw121q fch121 flw121 esw121 ebr121
```

20 秒后仍然是 7/8，而且 `session-ffw121.log` **是空的**（连
`observation world bound` 都没写出来）。原因在 `run-session.ps1:140`：

```powershell
New-Item -ItemType File -Force -Path "$base.obs.jsonl"
```

**孤儿 Terraria 仍然持有 `bridge.obs.jsonl` 的句柄**，这个 `New-Item` 抛
"文件正被另一进程使用"，而脚本是 `$ErrorActionPreference='Stop'`，于是
session 在写出任何一行日志之前就死了。监视窗口每 600 秒重试一次，每次都
以同样方式死掉——**这个臂进入了永久重启失败循环，却仍然被计为"campaign
的一个臂"**。`stop-all.ps1` 的注释里早就记录过这个失效模式（"the probe
survived the stop, kept bridge.obs.jsonl open, and the next resume died on
New-Item file is in use"），但 `start-campaign.ps1` 没有对应的清理。

### 24.3 两个根因

1. **round 结束时的清理被一个错误的守卫条件跳过。** 旧代码是

   ```powershell
   if (-not $game.HasExited) { & taskkill.exe /PID $game.Id /T /F }
   ```

   但 `$game` 只是包着 `runprobe.ps1` 的 powershell 壳，而 runprobe 在
   `start-isolated-test.ps1` 于 `WallSeconds + 60` 放弃时**立刻返回**——这正好
   就是"打满墙钟的 round"结束的时刻。所以**唯一真正需要这次 kill 的情形，
   守卫恰好为假**。

2. **`taskkill /T` 追不到探针。** `/T` 沿 `ParentProcessId` 递归，而
   DesktopHost 在 `start-isolated-test` 那个 shell 退出时被**重新挂到别的
   父进程**上，树链断了。这个项目已经记录过 `taskkill /T` 把 job runner 一起
   杀掉一次。

### 24.4 修复

不再用进程树，改用**运行目录名**定位：runprobe 把自己的运行目录命名为
`game-probe-rt-<tag>-<stamp>`，所以 tag 在命令行里是唯一的。两端都加了清理：

- `run-session.ps1`：每个 round 结束时，除了原来的 taskkill，再按
  `*game-probe-rt-$Tag-*` 扫一遍，杀掉 `Terraria.exe` /
  `Chaite.DesktopHost.exe` / `powershell.exe`（后者是持有私有桌面的
  `start-isolated-test.ps1` 壳）；
- `start-campaign.ps1`：新增 `Stop-OrphanProbe`，在启动某个 tag 之前先清掉它
  的孤儿。这个函数只在 `Test-Running` 已经判定该 tag 没有 launcher 之后才会
  被调用，所以**不可能**误杀正在运行的 session 的探针。调用点放在
  `-WhatIfOnly` 分支**之后**，保证 dry run 没有任何副作用。

**引擎内证明**（不是空转）：重启时清理函数逐条打印了它杀掉的进程，并成功启动：

```
  ffw121   reaping orphan Chaite.DesktopHost.exe pid=251028
  ffw121   reaping orphan Terraria.exe pid=156784
  ffw121   started pid=168144  duke-fishron/fishron-fairy-wing  bound=18 slots=12 sort=threat hurt=400 clean=1200000
  fsw121q  reaping orphan Chaite.DesktopHost.exe pid=262716
  fsw121q  reaping orphan Terraria.exe pid=135764
  fsw121q  started pid=253088  duke-fishron/fishron-strong-wing  bound=18 slots=12 sort=threat hurt=400 clean=1200000
```

4 分钟后复查，两个臂都真的在训练（这次 session log 有内容，action 文件
0.1 秒新）：ffw121 checkpoint 942,214 步、fsw121q 2,764,147 步，
`Terraria=8 DesktopHost=8 launchers=8 trainers=8`。

**顺带发现**：fsw121q 的 launcher 死了但 trainer 还活着，所以重启前必须先杀
掉那个 trainer——否则新 session 会起第二个 trainer，**两个 trainer 共用一个
bridge**（同一个 `bridge.action`、同一条 obs 流、同一个 checkpoint 目录）。

### 24.5 次要缺陷：round 步数预算被重复累加

```powershell
$target = $baseSteps + $StepsPerRound * $round
```

`$baseSteps` 每个 round 都从 `ppo.log` 重新读，本身已经在推进，再乘 `$round`
就是**重复计算**：第 N 个 round 会要求"N × 60,000"的额外步数。实测
fsw121q round 2 的目标是 2,761,950，而 checkpoint 停在 2,641,950——从 round 2
起目标在固定的墙钟内**不可能达到**，于是每个 round 都是被
`GameSessionEnded` 结束的，而不是被自己的预算结束的。改成
`$baseSteps + $StepsPerRound`。

### 24.6 量到但**故意不修**的缺陷：lockstep 关闭后不再恢复

```csharp
if(++bridgeWaitTimeouts >= 3) bridgeLockstepDisabled = true;
```

`bridgeLockstepDisabled` 一旦置位**在整轮内永不恢复**。round 开始时 trainer
还没接上（要启动游戏、`PPO.load` 一个 2.6M 步的 checkpoint，约 90 秒），于是
头 3 个 tick 各等满 30 秒超时 → 第 3 次就关掉 lockstep → **这一轮剩下的时间
里 `BridgeLagLimit = 8` 完全不被执行**。每个 round 的 `BRIDGE_LOCKSTEP` 日志
恰好 3 行，正好印证。

但实测**这不是当前的瓶颈**：在健康的臂上，动作文件是 0.0 秒新，引擎相对动作
的超前量是

| arm | engineTick | actionTick | lead |
|---|---|---|---|
| fsw121d | 147240 | 147244 | −4 |
| fsw121q | 117060 | 117076 | −16 |
| ebr121 | 213660 | 213716 | −56 |

负值意味着**trainer 在等引擎**——8 个 session 同时跑时引擎自身被 CPU 限在
~86 tick/s，正好等于 trainer 的消费能力（~43 步/s × 2）。所以超前量不会累积，
这个缺陷目前是**潜伏的**，只在 trainer 变成较慢一方时才会真的破坏
(observation, action) 对齐。

**不现在修的原因**：它在 `GameProbe.cs` 里，改它就要重建 Core，而测量组铁律
禁止在 wave 运行期间重建 Core（会改变 `InputCoreSha256` 并把正在跑的这一批
数据作废）。记在这里，等下一次停机窗口再修。

### 24.7 顺带量到的数字

- 孤儿引擎自由奔跑的速率：**188 obs 行/s ≈ 188 KB/s**（416 MB / 37 分钟）。
  一个孤儿一天能写 ~16 GB。
- 引擎在 8 session 满载下的实际速率：**85.6 tick/s**（60.3 秒 5,160 tick），
  即 **42.8 策略步/s**（frame_skip=2）。

---

## 25. 死亡的奖励是平的：唯一剩下的梯度是"更早去死"（2026-09-22 22:00）

这是到目前为止对"五个停滞臂为什么不学"最有解释力的缺陷。

### 25.1 算术

`_reward` 里命中罚分是

```python
reward -= (self._prev_life - life) * DODGE_HIT_PENALTY     # 2000
```

按**生命损失量**收费。而"死亡"的定义就是生命归零，所以**任何一个以死亡结束的
episode，其命中罚分总和恒等于 `lifeMax × 2000`**——光女 MaxLife=500 时是
1,000,000，猪鲨 78,000 时是 156,000,000。**死在 tick 1022 和死在 tick 4473
收一样多，中 1 下和中 10 下也收一样多。**

于是任何死亡的回报是

```
-0.01 × ticks  -  lifeMax × 2000  -  2,000,000
```

**唯一随死亡时点变化的项是每 tick 的 `-TIME_PENALTY`，而它对"活得久"收费更高。**
也就是说：**奖励在明确地付钱让策略早点死。** 对一个还没学会获胜的臂（= 全部五个
停滞臂），这就是它收到的全部梯度。

### 25.2 对照证明（`check_reward_ordering.py`，非空转）

把存活信用设为 0 复现旧行为，死亡阶梯**完全平**：

```
tick 500     survived   3.1%    -2160000.02
tick 1022    survived   6.4%    -2160000.02
tick 1964    survived  12.3%    -2160000.02
tick 3000    survived  18.8%    -2160000.02
tick 4473    survived  28.0%    -2160000.02
tick 8000    survived  50.0%    -2160000.02
tick 16000   survived 100.0%    -2160000.02
FAIL ... the objective pays the policy to die sooner     EXIT=1
```

修复后严格递增，且原有的六条不变量全部保持：

```
tick 500     -2144375.02
tick 1022    -2128062.52
tick 1964    -2098625.02
tick 4473    -2020218.77
tick 16000   -1660000.02
OK  a later death scores strictly higher        EXIT=0
```

### 25.3 训练日志里策略确实在服从它

- `esw121` 中位 episode：**1,362 → 1,022 tick**
- `ebr121` 中位 episode：**1,858 → 1,624 → 1,022 tick**
- `ebr121` 最近 67 场真实对局里 **41 场是完全相同的死亡**（ticks=1022, hits=1,
  dmg=11400），`esw121` 有 26 场
- 而**未训练的**光女对照（polL / polM / polP）的主导死法是 **ticks=1964**

**训练后的臂比什么都不做死得更早。** 死亡 tick 只落在少数离散值上（95 场里只有
15-16 个不同值：1022 / 1964 / 2274 / 2726 / 3571 / 3831 / 4192 / 4473），这是
"按固定时间表到达的攻击，躲不过就死"的特征；策略学到的是**挑最早的那一次死**。

### 25.4 修复

新增 `DODGE_SURVIVAL_CREDIT`（默认 500,000），在死亡与 timeout 两个分支按
`etl / tlim` 比例发放：

```python
reward += DODGE_SURVIVAL_CREDIT * survived
```

定标依据：必须压倒整局的每 tick 罚分（0.01 × 16,000 = 160），同时保持"任何胜利
都优于任何非胜利"：

| | 值 | 与最差胜利（40 命中 = -400,000）比较 |
|---|---|---|
| 最好的死亡 | -2,000,000 + 500,000 = -1,500,000 | 更低 ✓ |
| 最好的 timeout | -1,000,000 + 500,000 = -500,000 | 更低 ✓ |

500,000 是整局时间罚分总量的 **3,125 倍**，所以存活时间现在主导死亡之间的排序。

### 25.5 停机重起（因为运行中的 trainer 不会重新读常量）

奖励常量在模块导入时读取，所以已经在跑的 8 个 trainer **不会**拿到新值——而它们
每一分钟都在按旧奖励优化"更早去死"。因此必须停机重起：

- `stop-all.ps1` 干净停掉 8 个臂，**8 个 checkpoint 全部通过 `PPO.load` 校验**，
  并各留一份 `ppo_stop_20260922-213131.zip` 快照；
- 顺带又发现一个孤儿：`flw121 stop orphan pid=248780 (no launcher; holds
  bridge.obs.jsonl)`——说明 §24 的孤儿问题比先发现的 2 个臂更广；ffw121 还留下
  一个 `GameProbePatcher.exe`；
- `start-campaign.ps1 -Resume -Rounds 12` 重起全部 8 个臂。

**引擎内执行证明**：8 个 `ppo.log` 的最新横幅全部变成

```
DODGE OBJECTIVE ON: hit=2000 win=6000000 death=-2000000 clean=4000000
timeout=-1000000 survival=500000 (a hit costs 160.0, a win pays 480000.0)
```

`train.py` 的横幅已扩展打印 `survival=`，否则这次改动无法从日志回读（§22 的教训：
一次读不回自己奖励形状的运行，不能作为任何事情的证据）。

### 25.6 副作用与测量说明

- 这次改动只动 Python 训练代码，**`InputCoreSha256` / `InputPluginSha256` /
  `ProbeSourceSha256` 全部不变**，所以仍是同一个测量组，没有跨组混用。
- 但奖励形状变了，所以**改动前后的胜率/无伤率不可直接比较**；`campaign-acceptance.py`
  的滚动窗口会混入旧奖励下的对局，评估时必须用改动之后的 episode。
- 重起也让 8 个臂统一用上了 §24.5 修好的 round 预算（`base + StepsPerRound`），
  此前只有 19:40 重起的 ffw121 / fsw121q 是新公式，另外 6 个还在跑旧公式。

## 26. 验收之后的交付：把策略真正挂到程序本体上（2026-09-22 22:35）

用户的要求是"达到验收标准后挂载在程序本体的接管后战斗策略中，挂载后测试验证"。
训练通过桥接（`CHAITE_BRIDGE_FILE` + `<base>.action`，trainer 每 `frame_skip` 帧
选一个动作）驱动游戏，生产是插件在**进程内**每 tick 跑一遍 MLP——**两条不同的代码
路径**。所以"训练里达标"从来没有证明"挂载后能打"。新增
`Chaite/training/mount-policy.ps1` 就是补这一环。

### 26.1 生产口径：三个变量，其中一个必须**不设**

| 变量 | 值 | 作用 |
|---|---|---|
| `CHAITE_POLICY_FILE` | `<tag>.bin` | 插件加载的权重文件 |
| `CHAITE_POLICY_FORMAT` | `exported` | **选格式**。不设时 `ResolveFormat()` 返回 `Residual` |
| `CHAITE_PROBE_OUT` | 彩排用的 base | 探针写到哪 |
| `CHAITE_BRIDGE_FILE` | **必须不设** | `RouteReplay.LoadFromEnvironment()` 只要非空就返回 replay |

`CHAITE_PROBE_OUT` 是 §16.2 引入的解耦：探针写文件的位置与"插件认为谁在掌舵"分开。
`CHAITE_BRIDGE_FILE` 一旦被设上，彩排测的是 replay 而不是策略，而日志里看不出区别。

### 26.2 实测缺陷一：`CHAITE_POLICY_FILE` 单独设不够（22:33 崩在 tick 240）

第一次彩排直接崩掉：

```
status=harness-error ticks=240 hits=0 deaths=0
Fatal runtime error; automation disabled: System.InvalidOperationException:
  CHAITE_POLICY_FILE is set but CHAITE_POLICY_ROUTES is not; refusing to guess
  which routes the policy owns.
   at Chaite.Core.LearnedPolicy.EnsureConfigured()
   at Chaite.Core.LearnedPolicy.ForRoute(FormulaRoute route)
   at Chaite.Core.FishronWingScript.DecideMovement(...)
```

两种策略格式**共用** `CHAITE_POLICY_FILE`，由 `ExportedPolicy.ResolveFormat()`
（`ExportedPolicy.cs:207-221`）按 `CHAITE_POLICY_FORMAT` 选：不设/空 → `Residual`。
所以插件把 `.bin` 当文本残差去解析，**根本没走到 MLP 加载器**。加上
`CHAITE_POLICY_FORMAT=exported` 后立刻加载成功：

```
LOADED: 2026-09-22T22:34:31 Exported policy loaded:
  ...\policy-export\fsw121d.bin [128x121, 128x128, 24x128], observation 121 wide
```

注意这个失败是**失败关闭**的（抛异常、关自动化），不是静默走错——可以接受，
但必须在挂载脚本里写死，否则每次都要重新踩。

### 26.3 实测缺陷二：`CHAITE_RUN_MAX_TICKS` 与 `-MaxTicks` 是两个不同的上限

第二次彩排加载成功，但 19 个 episode 里只有 4 场真实对局，剩下 15 场是
`ticks=1` 的退化 episode：

```
e=0 ticks=4438 hits=5 win=True   e=4..18 ticks=1 hits=0 win=False
outcome=test-time-limit
```

原因是把 `CHAITE_RUN_MAX_TICKS` 当成了"每场上限"。实际语义：

- `runprobe.ps1 -MaxTicks` → 探针 `-maxticks` → **每场**的 `tickLimit`（训练 16000）；
- `CHAITE_RUN_MAX_TICKS` → **整轮总预算**（训练 380000）。

我传了 16000，于是 4438+4348+4346+2868 = 16000 正好用完，整轮结束，之后只剩
计数的退化 episode。**如果按 19 场统计，胜率会被 15 场 1-tick 拉成 15.8%**——
这是个会直接产出错误验收结论的坑。脚本现在把两者拆成
`-PerEpisodeTicks`（默认 16000）与 `-RunTickBudget`（默认 `Episodes * 20000`）。

### 26.4 观测配置必须从 `sessions.json` 复现，且 `fsw121p` 挂不了

插件用 C# 造观测、trainer 用 Python 造观测（`chaite_env.obs_vector`），两者只在
slots / sort / collapse / aggregates / world bound 全部一致时才等价。导出文件带
**观测宽度**，宽度不符会被 `ChaitePolicyDriver.RequireObservationCount` 拒掉，
但**同宽度的布局错位是静默的**。所以 `mount-policy.ps1` 直接读
`training/sessions.json` 的对应条目来设这些变量，不手抄。

顺带查清一件事：**引擎侧根本不读 `CHAITE_OBS_BOSS_PROX`**（它是 Python 侧的观测
开关）。所以带 `bossProximity` 的 `fsw121p` **无法挂载**——插件造不出匹配的观测。
八个臂里只有它用这个开关；strong-wing 是三选一，其余 7 个都可挂。脚本对这种情况
直接拒绝并说明原因，而不是挂上去跑一个静默错位的策略。

### 26.5 导出本身的证明

`export_policy.py` 的判定必须回读到 `EXPORT MATCHES SB3` 才允许挂载：

```
policy layers   : [(128, 121), (128, 128), (24, 128)]
obs dim         : 121 (from the first layer, not hardcoded)
verify random   : 2000/2000 agree with SB3 greedy argmax
verify real     : 2000/2000 agree with SB3 greedy argmax
real action hist: {3: 735, 7: 775, 14: 14, 23: 476}
VERDICT         : EXPORT MATCHES SB3
```

真实观测那一半是必要的：一个只在随机噪声上一致的导出不是证据，因为训练过的策略
动作集中在它真正访问过的状态上。脚本不看到这一行就 `exit 2`，不挂载。

### 26.6 首次挂载测量（4 场，样本不足，仅证明链路通）

| 指标 | 挂载彩排 | 训练滚动窗口（21:10，旧奖励） |
|---|---|---|
| 胜率 | 75.0% (3/4) | 77.7% (300 场) |
| 无伤率 | 0.0% (0/4) | 12.0% |
| hits/1kt | 0.875 | 0.494 |
| 中位 ticks | 4348 | 4344 |

胜率与中位时长和训练侧基本吻合，说明**挂载路径没有引入观测错位**——这是这一轮
唯一想确认的事。无伤率 0% 与验收线 50% 的差距是训练问题，不是挂载问题。4 场不足以
做任何判定，40 场重测在跑。

## 27. 弹幕按类型折叠：策略在挨打那一刻只看得见 32 个弹幕里的 1 个（2026-09-22 22:41）

这是本次长时段里找到的最严重的观测缺陷，也是"光女为什么一直 0%"最像结构原因的一条。

### 27.1 机制

`tools/GameProbe.cs:5317-5334`：

```csharp
// Membership. Twelve slots filled with twelve copies of one hitbox tell
// the policy nothing that one slot does not, ...
int collapsedOut=0;
if(projectileCollapseTypes)
{
    var seenType=new Dictionary<int,bool>();
    var kept=new List<Projectile>(nearest.Count);
    foreach(var shot in nearest)
    {
        if(seenType.ContainsKey(shot.type)) { collapsedOut++; continue; }
        seenType[shot.type]=true;
        kept.Add(shot);
    }
    nearest=kept;
}
```

先按威胁分排序取前 `projSlots` 个，然后**每种 `type` 只保留最近的一个**，其余全部
丢弃并计入 `pe`。所有 8 个臂都配了 `projSlots=12` 且 `projCollapse=true`。

### 27.2 实测

`training/_proj_slots.py` 读实时观测流（共享读，不干扰探针）：

```
tag      hostile pc(mean/max)  kept pr(mean/max)  collapsed pe(mean/max)
fch121      24.78 / 32            0.88 /  1           23.90 / 31
esw121       9.84 / 30            0.73 /  2            9.10 / 29
ebr121       8.38 / 25            1.20 /  2            7.19 / 23
fsw121d      0.24 /  2            0.12 /  1            0.12 /  1
```

场上最多 32 个敌对弹幕，`pr` 里**最多 1 个**，`pe` 均值 23.90——32 个里 31 个被折叠掉。

再看**挨打那一刻**（`training/_death_visibility.py`，取 `pl` 下降的帧）：

```
fch121  27 个受伤帧 | 平均 200px 内 2.19 个 vs 平均显示 0.63 个
        有近弹幕没被显示的帧：9/27 = 33%
        et=1255  pc=32 pe=31 slots=1 p2=10 p4=21 p8=32 pl=139
        et=1387  pc=32 pe=31 slots=1 p2=9  p4=22 p8=32 pl=78
        et=1510  pc=32 pe=31 slots=1 p2=9  p4=23 p8=32 pl=19
```

`p8=32` 说明**整批 32 个都在 800 px 内**，`p2=9~10` 说明 9~10 个在 200 px 内，
而策略看到的是 1 个。这三帧正是它把血从 139 掉到 19 的地方。

### 27.3 注释里的论断为什么是错的

注释说"12 个槽位装 12 份同一个 hitbox 不提供 1 个槽位之外的信息"。这对**同一位置**
的副本成立，对**散射**不成立：同 `type`、不同位置的弹幕，位置本身就是躲避所需的
全部信息。注释把"同类型"当成了"同位置"，所以这个去重删掉的正是弹幕战的信息。

### 27.4 修法：只对 fch121 关掉折叠，并在引擎内对照

`sessions.json` 的 `fch121` 设 `projCollapse: false`，`start-campaign.ps1:136` 因此
不再传 `-ProjCollapse`，`run-session.ps1:170` 于是移除 `CHAITE_PROJ_COLLAPSE`，探针
默认关闭折叠。**没有改一行源码，所以 `InputCoreSha256`/`ProbeSourceSha256` 不变，
仍是同一个测量组。**

引擎内对照（`pe` 是探针自己写的字段，不可能空转）：

| tag | `pe` mean/max（折叠掉的数量） | `pr` max（显示槽位） |
|---|---|---|
| fch121（已关） | **0.00 / 0** | **12** |
| fsw121d（未动） | 23.43 / 24 | 1 |

`OBS_DIM` 不变（槽位列表本来就会补齐到 `projSlots`，见 `GameProbe.cs:5335-5344`），
所以 warm start 权重兼容；但槽位特征的分布变了，所以 **fch121 改动前后的胜率/无伤率
不可直接比较**。

### 27.5 为什么其余七个臂不同时改

- `fsw121d`/`ffw121`/`flw121` 每 tick 平均只有 0.00~0.24 个敌对弹幕，折叠对它们是
  空操作；不动它们，才能让 2026-09-22 22:00 的奖励改动**单独可归因**。
- `esw121`/`ebr121` 确实每 tick 面对 8~10 个弹幕，是下一批候选，但它们的受伤帧里
  近弹幕**仍然可见**（esw121 0%、ebr121 9% 有近弹幕被隐藏），证据强度不如 fch121
  的 33%，所以没有和奖励改动放在同一步做。

## 28. 验收必须看挂载彩排，不能看训练窗口（2026-09-22 22:38）

### 28.1 挂载彩排的结果

`training/mount-policy.ps1 -Tag fsw121d -Episodes 40`（生产口径挂载，进程内 MLP）：

```
=== mounted rehearsal: 39 real fights (of 39 episodes) ===
  win         89.7%  (35/39)
  no-hit       0.0%  (0/39)
  no-hit win   0.0%  (0/39)
  hits/1kt    0.713   median ticks 4348
wall=928.9s
```

**39 场全部是真实对局（没有一场退化），929 秒跑完，约 24 秒/场**——比训练快约 4 倍，
因为挂载路径没有文件握手往返（策略在进程内决策）。

### 28.2 训练窗口与挂载彩排**不一致**，而且方向相反

| fsw121d | 胜率 | 无伤率 |
|---|---|---|
| 训练窗口（当轮全部 episode） | 53.5% | 7.7% |
| 训练窗口（滚动 300 场，21:10） | 77.7% | 12.0% |
| **挂载彩排 39 场** | **89.7%** | **0.0%** |

胜率被**低估**、无伤率被**高估**，两者同向解释：每轮开头 trainer 挂上之前有一段
约 2 分钟的窗口，那段时间的 episode 由**原生 AI**（公式表路线）飞完。原生 AI
**不会赢**（把胜率拉低），但**也不会挨打**（把无伤率拉高）。

这段数据**不进训练**（`ChaiteBridgeEnv.__init__` 在实时边缘 `seek(0, END)`，
`reset()` 又用 `next_episode_start` 等到下一个 episode 边界才开始），但它**进
`bridge.episodes.jsonl`**，而 `campaign-acceptance.py` 和所有验收统计都读这个文件。

结论：**验收判定必须用挂载彩排的数字**，训练窗口只能用来观察趋势。这正好也是用户
要求的"挂载后测试验证"，两者是同一件事。

### 28.3 首次挂载彩排对验收的判定

`fsw121d` 挂载后 **89.7% 胜 / 0% 无伤**：胜率已过 80% 线，**无伤率离 50% 线还差
整整 50 个点**。39 场 0 无伤，即使真实无伤率是训练窗口显示的 12%，出现 0/39 的概率
也只有约 0.7%——所以真实无伤率显著低于 12%。

**当前状态：猪鲨 strong-wing 未达标（差无伤率），光女两套未达标（历史 3011 场 0 无伤胜）。**

## 29. 光女：致命伤是弹幕，且历史 3011 场里 0 次无伤胜（2026-09-22 22:53）

### 29.1 致命帧的几何：不是接触

`training/_empress_kill_gap.py` 在 esw121 的 40 MB 流里找**单帧掉血 ≥90% 血条**的帧
（`pl 600 → 0`，`hits=1`，真正的秒杀帧），共 22 帧：

```
玩家-BOSS 距离  p10=293  p50=388  p90=544  max=617 px
其中 <200 px 的：1/22 (5%)
同帧 p2（200px 内弹幕数）= 7..16   而 slots（显示槽位）= 2
```

**玩家被秒时离 BOSS 中位 388 px，所以致命伤不是接触/冲刺。** 同帧有 7~16 个弹幕在
200 px 内，而策略只被显示 2 个——**它被那 14 个看不见的弹幕之一打死**。这正是 §27 的
折叠缺陷在光女身上的后果。

（顺带更正我当晚早些时候的一个读错：`_death_visibility.py` 曾报 esw121
"0% 有近弹幕被隐藏"，那是在只有 2 个受伤帧的 4 MB 窗口上算的，而且没有按"≥90% 血条"
筛选，被消耗伤摊平了。只看秒杀帧，折叠正在隐藏凶手。）

### 29.2 历史校准：光女从未被无伤击败

`training/_empress_history.py` 扫描 `artifacts/` 里所有光女相关的 episode 文件：

| 来源 | 真实对局 | 胜 | 无伤 | 无伤胜 |
|---|---|---|---|---|
| esw121 | 1432 | 0 | 0 | 0 |
| ebr121 | 1359 | 0 | 0 | 0 |
| polL | 95 | 1 | 0 | 0 |
| polM | 96 | 0 | 2 | 0 |
| polP | 29 | 0 | 0 | 0 |
| **合计** | **3011** | **1** | **2** | **0** |

**3011 场里 1 胜、2 次无伤、0 次无伤胜。** 所以"80% 无伤胜率"不是把已有策略再挤出
几个点，而是要从 0/3011 起步。

### 29.3 可行性算术

从 episode 汇总行（`bossLifeRemaining`，注意不是 obs 行的 `blm`）：

```
e=0  ticks=962   hits=1  bossDamage=8400   bossLifeRemaining=89580
e=1  ticks=916   hits=1  bossDamage=9280   bossLifeRemaining=88700
e=41 ticks=1022  hits=1  bossDamage=11400  bossLifeRemaining=86580
```

玩家在 872~1362 tick 就死，此时只打掉 BOSS **约 8.6%**（8,400/98,000）。
按 `simDps 1200`（=20/tick）BOSS 需要约 **4,900 tick** 才会死；实测 `bossDamage/ticks`
约 8.7~11.2/tick，需要 **8,700~11,300 tick**。

**也就是说策略要把存活时间拉长到现在的 7~9 倍，而且一次都不能被打到。**
`hits==0` 是硬要求：满血 600 被单帧打空确实存在，所以任何一次命中都可能直接结束；
而 65~196 的消耗伤虽然不致命，也一样会让该场失去"无伤"资格。

### 29.4 处置与引擎内验证

对 `esw121` / `ebr121` 关掉 `projCollapse`（与 fch121 同一处改动、同一种验证）。
引擎内对照（`pe` 是探针自己写的字段）：

| tag | `pe` mean/max | `pr` mean/max | `p2` mean/max | 槽位饱和 |
|---|---|---|---|---|
| fch121（已关） | 0.00 / 0 | 8.41 / 12 | 4.57 / 14 | 61% |
| esw121（已关） | 0.00 / 0 | 0.42 / 1 | 0.00 / 0 | 0% |
| ebr121（已关） | 0.00 / 0 | 0.36 / 9 | 0.00 / 0 | 0% |
| fsw121d（未动） | 0.45 / 1 | 1.05 / 2 | 0.00 / 0 | 0% |

（esw121/ebr121 的两列取自刚开轮的前 600 行，战斗还没进到弹幕阶段，所以数字偏低；
`pe` 恒为 0 才是这一步要证明的事。）

## 30. 换掉 checkpoint 会让一个臂**永久空转**（2026-09-23 00:45）

### 30.1 迁移的动机：同一奖励下三对三完全分化

`campaign-acceptance.py` 在 00:40 的 last-100：

| 臂 | 配装 | 胜率 | 无伤率 | hits/1kt | 熵 |
|---|---|---|---|---|---|
| fsw121d | strong-wing | **84.0%** | 15.0% | 0.46 | −1.67 |
| fsw121p | strong-wing | 73.0% | 2.0% | 0.92 | −2.31 |
| fsw121q | strong-wing | 61.0% | 1.0% | 1.24 | −2.10 |
| ffw121 | fairy-wing | **0%** | 0% | 4.11 | −2.84 |
| fch121 | trusty-chillet | **0%** | 0% | 4.97 | −2.55 |
| flw121 | lilith-wolf | **0%** | 0% | 2.24 | −2.43 |

熵接近 `ln 24 = 3.18` 说明这三个**不是收敛到坏策略，而是还没找到任何能赢的行为**。
checkpoint 普查显示混淆项：`fsw121d` 已 3,526,409 步，`ffw121` 1,630,342、
`fch121` 1,662,738、`flw121` 1,692,333——赢的那三个同时也多练了约一倍。

**迁移一次性覆盖两个原因，而且是区分"训练量不足"与"配装不行"最便宜的实验**：
如果 fairy-wing / lilith-wolf 本身能躲这套战斗，一个已经会用 strong-wing 躲的策略
会在一两轮内表现出来；如果它低水平平台化，那就是配装的上限，这件事越早知道越好。

兼容性是**验证过而不是假定**的：五个 checkpoint 全部加载为 `obs (121,) act 24
pi0 (128,121) vf0 (128,121) action (24,128)`，且 ffw121/flw121 的观测配置与 fsw121d
逐项相同（bound 18 / slots 12 / sort threat / collapse ON / frameSkip 2 / simDps 1200
/ dodgeObjective）。原权重保留为各臂目录下的
`ppo_pre-transfer-20260923-004319.zip`，另有 `stop-all.ps1` 的
`ppo_stop_20260923-004246.zip` 快照。

`fch121` **故意不迁移**：它是唯一同时关了 `projCollapse` 的臂，同时动两件事会让那个
修复的读数作废。

### 30.2 症状：迁移后一轮 2 分 18 秒就"成功退出"，一步没训

```
[ffw121] round 1/12 00:43:41 launching duke-fishron/fishron-fairy-wing
[ffw121] round 1: trainer exited 0 (base 1630342 + target 1690342 steps)
[ffw121] round 2/12 00:45:59 launching ...
```

`base 1630342` 是**迁移前的旧值**，而 checkpoint 已经是 3,526,409 步。目标
1,690,342 落在 checkpoint 实际步数**以下**，`train.py` 的
`while model.num_timesteps < timesteps` 一开始就为假，于是**训练 0 步、退出码 0**，
然后一轮接一轮地空转，每轮还照常拉起一个游戏进程烧掉墙钟。

### 30.3 根因

`run-session.ps1` 在循环**之前**（L153-160）正确地从
`PPO.load($latest).num_timesteps` 取基线，但循环**内部**（L231-235）每一轮又用
`ppo.log` 里最后一条 `checkpoint saved at (\d+) steps` **无条件覆盖**它：

```powershell
$saved = Select-String -Path $logPath -Pattern 'checkpoint saved at (\d+) steps' | Select-Object -Last 1
if ($saved) { $baseSteps = [int]$saved.Matches[0].Groups[1].Value }   # 旧写法
```

`ppo.log` 是**追加**日志，替换 checkpoint 文件不会改它，所以基线回退。

### 30.4 修法：只许前进

```powershell
if ($logged -gt $baseSteps) { $baseSteps = $logged }
```

对正常臂行为不变（日志值与 checkpoint 相等或更小，取大者仍是日志值，基线照常在
会话内推进）；对替换过 checkpoint 的臂则不会再把目标压到实际步数以下。

引擎内验证：

```
[ffw121] resumed checkpoint already at 3526409 steps
[flw121] resumed checkpoint already at 3526409 steps
ffw121  actionTick +4028 ticks / 30s = 134.3 ticks/s
flw121  actionTick +4173 ticks / 30s = 139.1 ticks/s
```

两个迁移臂都在真实推进（30 秒内 action tick 各走了 4,000 左右），`ppo.log` 也回读到
`DODGE OBJECTIVE ON: ... survival=500000`。

## 31. 训练/生产的决策节奏不匹配：frameSkip 2 → 1（2026-09-23 02:55）

### 31.1 怎么发现的：挂载彩排与训练窗口的分歧本身就是证据

02:29-02:41 的挂载彩排（生产代码路径，29 / 28 场）：

| | 训练窗口 (frameSkip=2) | 挂载彩排 |
|---|---|---|
| fsw121d 胜率 | 79.5% | **89.7%** (26/29) |
| esw121 胜率 | 0.3% | **17.9%** (5/28) |
| esw121 中位 tick | 1362 | **3571** |
| esw121 hits/1kt | 2.314 | **0.810** |

**60 倍的胜率差既不是污染也不是采样噪声**，所以分歧本身指向了配置。

### 31.2 根因

- `chaite_env.py:648` 读 `CHAITE_FRAME_SKIP`，`:784` 把它花在
  `for _ in range(self.frame_skip): row = self.stream.next_row(...)` 上——**写一次动作后
  连吞 N 行观测**，即"动作保持 N 个游戏 tick"。
- 生产端不是这样。`LearnedPolicy.cs:209-210` 的注释直接写着
  "the scripts call ForRoute unconditionally on **every tick**"；
  `FishronWingScript.Tick` → `DecideMovement` → `LearnedPolicy.ForRoute` → `Adjust`
  每 tick 都调用，`ExportedPolicy.cs` 里没有任何节流。

所以这批 checkpoint **全部是按一半的决策频率训练的，却按全频率部署**。挂载彩排因为
不经过 trainer（策略在进程内），天然就是"每 tick 决策"，于是它就是 frameSkip=1 的评测——
**这个修复不需要新证据，它已经被跑过了**。

### 31.3 修复与代价

`frameSkip` 全部改为 **1**（12 个 session 条目）。`stepsPerRound` 故意保持 60000：
每轮的游戏时间覆盖减半，但"每小时决策数"两种设法则相同（引擎是瓶颈，trainer 从来不是），
而短轮次只多付一次游戏启动。

实测代价与收益：

- fsw121d 的 trainer 步速 **68.3 步/秒**（240 秒内正好完成 1 个 16384 步 rollout；
  frameSkip=2 会预测 0 个），确认生效。
- 改后 tick 速率 88.2 / 106.4 ticks/s，**与改前持平甚至更好**——证实引擎而非 trainer
  是瓶颈，这个修复不花吞吐。
- 轮次时长从约 26 分降到 **13 分 15 秒**（60000 步 = 60000 tick）。
- 权重兼容（观测仍 121 维），且彩排已证明 frameSkip=2 的权重在 frameSkip=1 下表现**更好**，
  所以双向迁移都安全。

### 31.4 同时更正一个归因

此前把训练窗口与挂载彩排的差距归因于"每轮开头 trainer 挂上之前由原生 AI 飞的那段
episode"。读 `run-session.ps1:200-207` 后修正：那约 2 分钟是**游戏自身启动**
（等 `bridge.obs.jsonl` 增长超过 2000 字节），trainer 在流开始后约 10 tick 就启动了。
真正的次要污染是 trainer 挂上后要等**下一个 episode 边界**，所以每轮约 1 场由原生 AI
飞完（不赢也不挨打）。两个成因都在，但**主因是节奏不匹配**。

这给了一个可检验的预测：frameSkip=1 之后，训练窗口的胜率应当向上收敛到挂载彩排的水平
（fsw121d 从 79.5% 趋向 89.7%）。下一次监视窗口验证。

## 32. 未决：fsw121d 的训练窗口与挂载彩排相差 50 个点（2026-09-23 05:15）

### 32.1 现象

05:00 的挂载彩排（生产代码路径，29 场）与同一时刻的训练窗口：

| fsw121d | 训练窗口 last 200 | 挂载彩排 |
|---|---|---|
| 胜率 | 43.5% | **93.1%** (27/29) |
| 无伤率 | 2.5% | 0.0% |
| hits/1kt | 1.484 | **0.484** |
| 中位 tick | 4000 | 4347 |

而 **ffw121 两个口径一致**（21.0% 对 17.2%），所以不是全局的口径问题，是 fsw121d 特别敏感。

### 32.2 为什么敏感：它正卡在刀锋上

逐条读 episode 行可以看到，fsw121d 的输局大量是**死在 BOSS 剩 0.05%~5% 血的时候**：

```
ticks=4152  win=False  hits=6  bossLife=3917
ticks=4346  win=False  hits=7  bossLife=41
ticks=4229  win=False  hits=9  bossLife=2371
```

BOSS 在约 4347 tick 死，所以**几百 tick 的存活差就能把胜率从 43% 翻到 93%**。
训练窗口的中位存活是 4000，挂载是 4347——差 8.7%，正好跨过那条线。

### 32.3 四个假设都被否证

1. **节奏不匹配**——已修为 frameSkip=1，但训练窗口反而从 79.5% 掉到 43.5%，
   而挂载从 89.7% 升到 93.1%。**方向相反**，所以节奏不是这个分歧的成因。
2. **锁步延迟**——`training/_lockstep_lag.py` 直接测"最新观测 tick − 动作文件 tick"：
   fsw121d `min=-1 p50=0 max=0`，ffw121 同样。**延迟是 0 tick**，握手完全跟得上。
3. **动作文件撕裂读**——不是缺陷，是设计：`chaite_env.write_action` 的注释明确
   "the reader now shares read/write/delete and tolerates a torn or empty line by keeping
   the previous action"，探针也按 `BridgeLagLimit = 8` 等新动作（`GameProbe.cs:2298`）。
4. **导出格式只是微调手写公式路线**——不是：`LearnedPolicy.cs:437` 说明非残差入口
   "class zero means a concrete action"，即导出策略**取代**脚本决策。

### 32.4 为什么不再深挖

**因为它不改变任何验收判定。** 两个口径都同意无伤率在 0~2.5%，而门槛是 50%；
挂载口径的胜率（93.1%）反而已经过线。**约束项是无伤率，不是胜率**，两个口径在这一项上
没有分歧。所以这个分歧被记为未决项而不是阻塞项。

### 32.5 无伤率为什么是真正的墙（量化）

- fsw121d 胜局的 hits 分布是 0→10 的平滑分布，均值 **2.46**；掉血 71~150/480
  **全是 BOSS 伤害**（≤5 血的消耗伤 0%），所以每次 hit 是一次独立失误。
- 要 50% 无伤，需要把失误率降到约 1/3.5（按泊松，P(0)=0.5 对应均值约 0.7）。
- 奖励算术上策略**已经有**转向无伤的正激励：无伤胜 = 6000+4000 = 10000，
  2.5 次命中的胜 = 6000−450 ≈ 5550，比值 1.8:1；若为变干净牺牲 10 个点胜率，
  代价约 600 而收益约 (0.50−0.14)×4000 = 1440，净为正。**所以激励不是瓶颈，能力是。**
- 按铁律不改奖励权重（那是偏好不是缺陷，且排序本来就正确）。

## 33. 需求变更：光女退出范围，猪鲨改为 90% 胜率；四套配装的难度阶梯（2026-09-23 08:30）

### 33.1 用户指令

用户 2026-09-23 原文："现在程序只针对猪鲨！不再接管与准入光女……你只需要将猪鲨的
四套配装都训练到 90 胜率即可验收并挂载在程序策略中，并且注意更改程序前端与逻辑，
以及清理历史遗留，不再负责光女相关部分。"

所以：**无伤率不再考核**，验收 = 四套配装各 ≥90% 胜率，然后挂载并验证。光女
（NPC 636）的接管、准入、逻辑、前端一律清理。此前的"无伤率是约束项"结论作废，
§31/§32 里关于无伤率的分析只作为历史证据保留。

### 33.2 难度阶梯：胜率与"翼时上限"单调相关

`training/_mobility_census.py` 从各臂 obs 流的尾部采样（`vy<0` 即上升）：

| 臂 | 上升帧占比 | 水平移动帧占比 | 翼时上限 `wm` | 训练窗口胜率 |
|---|---|---|---|---|
| fsw121d（strong-wing） | 49.5% | 42.4% | **180** | 54.6%（挂载 93.1%） |
| ffw121（fairy-wing） | 52.5% | 98.7% | **130** | 7.1%（挂载 17.2%） |
| flw121（lilith-wolf） | 54.7% | 89.7% | **100** | 2.2% |
| fch121（trusty-chillet） | 53.0% | 89.6% | **100** | 0.5% |

**四套配装都能飞**，所以低胜率不是"坐骑不能飞"。差异集中在**翼时上限 180/130/100**，
胜率与之单调相关。**弱臂确实赢过**（fch121 全量约 20 胜 / 4035 场、flw121 2.2%），
所以这是学习难度，不是配装不可行。

### 33.3 动作分布：`fch121` 塌缩成"不按跳跃键"

`training/_action_census.py` 反复采样 `bridge.action`（该文件只有一行）：

| 臂 | up | down | dash | 不同动作数 |
|---|---|---|---|---|
| fsw121d | 80% | 90% | 75% | 15 |
| ffw121 | 68% | 83% | 64% | 14 |
| **fch121** | **25%** | 58% | 55% | 16 |
| flw121 | 89% | 89% | 88% | 16 |

三个能打的臂都大量同时按 up+down+dash；`fch121` 的 up 只有 25%，并且最近 8 场**全部**
在 `ticks=1101~1184`、`hits=6~8`、BOSS 还剩 6.3 万血时死亡——它是被 BOSS 本体撞死的，
不是打不动。**注意 up% 与上升帧占比不成正比**（fsw121d up=80% 但只有 49.5% 的帧在上升），
说明 up 与 down 同时按下会互相抵消，所以 up% 不能单独当作"会不会飞"的指标。

### 33.4 与 §32 的关系

`fsw121d` 的"训练窗口 43.5% 对挂载 93.1%"分歧仍未解释（§32 的四个假设都已否证）。
但**它不影响现在的验收**：验收只看挂载口径的胜率，而 strong-wing 挂载已 93.1% 过线。

## 34. 竞技场改为三层平台：弱翼与坐骑摔死的真正原因（2026-09-23 09:00）

### 34.1 用户给出的原因

用户 2026-09-23 原文："两套坐骑路线与弱翼路线都需要在水平地面上每隔 60 格有一行水平
平台，一共需要三层（包括地面）。至于强翼，新增平台应该不会影响其现有的单平地策略。"

### 34.2 实测完全吻合

- 训练用的 fixture 是 `-phase monitor`（`runprobe.ps1` 的默认值），所以
  `IsMonitorFixture = true`，海洋盆地**不灌水**，是一条干燥跑道。
- 而这条跑道**只有一层地面**：`GameProbe.cs` 的 `arena` 块发布
  `platformRows: new int[0]`、`arenaShape: "one long straight flat ground..."`。
- 于是翼时只有 100（chillet / lilith-wolf）与 130（fairy-wing）的配装，在空中把翼时用尽
  之后**无处落脚**，只能摔下去——这正是 §33 里它们死在约 1100 tick、BOSS 还剩 80% 血
  的原因。它们不是学不会躲，而是**根本没有可以落脚的垂直面**。

### 34.3 改动

`tools\GameProbe.cs`：海洋 + monitor fixture 下，除地面外再建 **2 行木质平台**
（`TileID.Platforms`，`frameY = 0`），位于 `arenaGroundY - 60` 与 `arenaGroundY - 120`，
x 跨度为竞技场自身的 `ArenaGroundLeft..ArenaGroundRightExclusive`（即 1..399），
**不含安全地板**。60 格约等于一次满翼时所能爬升的高度，所以从下一层够得着上一层。

### 34.4 为什么这次不会再犯"几何漂移"

早期版本有过两行平台，被删除的理由是"那套多层竞技场是凭空发明的、不是测出来的"，而且
**发布错误**：`arena` 块声称一个范围、平台却建在 1..399，读证据的人无法发现。

这次：
- `platformRows` **直接发布建行时用的同一个数组** `arenaPlatformRows`；
- 新增 `platformRowSpacingTiles`；
- 新增 **`platformTileCount`**——建完**立刻从世界回读**的平台瓦片数（不是结果写出时统计，
  因为那时世界可能已被重置，控制会永远读 0 而毫无意义）。这是铁律要求的
  "在引擎内执行、且不可能成为空转的对照"。
- `arenaShape` 同步改写，说明强翼面对同一个分层竞技场、其平地行为不变。

### 34.5 影响范围

竞技场变了，所以**此前所有数据与之后的数据不是同一测量组**，必须重启整批训练。
各臂从现有 checkpoint 继续（不重置权重），让策略在分层竞技场里适应。
`GameProbe.cs` 不属于 `Chaite.sln`，由 `tools\prepare-game-probe.ps1` 用 Roslyn `csc.exe`
单独编译，所以 `build.ps1` 校验不到它，必须另外编译一次。

## 35. 三层平台已按引擎内读数确证，新测量组已开跑（2026-09-23 10:20）

§34.4 说 `platformTileCount` 是"不可能成为空转的对照"。本节就是把那个对照**读出来**，
而不是声称它存在。

### 35.1 从世界回读的平台瓦片数

六个已完成运行的 `result.json` 里 `arena` 块逐项一致：

| 字段 | 值 |
|---|---|
| `arenaShape` | `three horizontal layers including the ground, 60 tiles apart…` |
| `platformRows` | `[440, 380]` |
| `platformRowSpacingTiles` | `60` |
| **`platformTileCount`** | **`798`** |
| `groundTop` / `groundThickness` / `groundTile` | `500` / `6` / `GrayBrick` |
| `groundLeft..groundRightExclusive` | `1..400` |

**算术自洽**：海洋竞技场地面是 1..399 共 **399** 格，两行平台即 399 × 2 = **798**，
与从世界回读的数字**逐位相同**。所以"两行平台真的建出来了"是**数出来的**，不是推断的。
地面 y=500、平台 y=440/380，间距正好 60 格，与用户给的"每隔 60 格一层、共三层"一致。

### 35.2 新测量组的身份，同样是读出来的

| 项 | 值 |
|---|---|
| `InputCoreSha256` | `E9A4AD9A…FD6D` |
| `GameProbeSourceSha256`（即 `ProbeSourceSha256`） | `E88A7A70…A8190` |

两项都在**运行目录内实测**：`game-probe-rt-ffw121-0923-100421\Chaite.Core.dll` 的
SHA256 与权威值逐位一致；同目录 `probe-static-evidence.json` 的
`GameProbeSourceSha256` 也逐位一致。所以"这一波跑的是新二进制"同样不是声称。

六个臂从各自 checkpoint **顺承续训**（未重置权重）：

| 臂 | 续训起点（步） |
|---|---|
| `fsw121d` | 5,689,097 |
| `fsw121p` | 5,776,535 |
| `fsw121q` | 5,746,035 |
| `ffw121` | 5,984,009 |
| `fch121` | 5,852,937 |
| `flw121` | 5,328,649 |

旧平场地测量组的会话日志归档为 `artifacts\session-<tag>-group1-flatarena.log`，
**不得与新组数据混用**。

### 35.3 顺带记一个操作教训

`Get-Content` 在本机（PS 5.1）按 ANSI/GBK 读无 BOM 的 UTF-8 文件，会把本文件的中文显示成
乱码。**核验非 ASCII 文件必须用能按 UTF-8 读取的方式**，不要据 `Get-Content` 的显示判断
文件是否被写坏——这次差点据此误判 §34 被写坏了，实际文件完好。

## 36. 平台建出来了，但策略几乎不站上去（2026-09-23 10:30）

§35 证明了 798 块平台砖确实在世界里。本节回答下一个问题：**策略用它们吗？**
答案是**基本不用**。这也是从观测流里数出来的。

### 36.1 判据与样本

观测行是完整 JSON，单位是像素。玩家站在某层上时 `player.position.y` = 该层顶面像素
− 玩家高度 42，即地面 500 格 → **7958**、平台 440 格 → **6998**、平台 380 格 → **6038**。
（7958 与实测首行 `"y":7958` 逐位吻合，换算由此得到验证。）判据取
"y 落在 ±2 px 内 **且** |vy| < 0.5"。

先确认这些帧是**策略在飞**而不是接管前的原生 AI：`sessionState` 分布为
`Idle 11 / Monitoring 24 / EngagedAlive 1223 / EngagedDeadWaitingRespawn 57`，
`plan` 非空 1311/1315。

| 臂 | 帧数 | 地面 | 平台 440 | 平台 380 | 任一落地 |
|---|---|---|---|---|---|
| `fsw121d` | 1315 | 15.6% | **0.3%** | **0.2%** | 16.2% |
| `ffw121` | 461 | 36.0% | **1.3%** | **0.0%** | 37.3% |
| `fch121` | 682 | **0.0%** | **0.0%** | **0.0%** | **0.0%** |
| `flw121` | 601 | 52.9% | **0.2%** | **0.0%** | 53.1% |

已完成轮次的运行目录给出同样结论：`ffw121` 地面 8.7%、`flw121` 地面 64.0%，
而**两行平台上都是 0.0%**。

样本量说明：以上是**单轮的部分帧**（461–1315 帧），足以看出量级（平台使用率在 1% 量级
或以下），不足以给出精确到小数点的比例。

### 36.2 这意味着什么

- 用户给的原因（"没有可落脚的垂直面"）**在竞技场层面已经修好**：798 块平台砖在那儿。
- 但**策略没有学会用它们**：平台上站立帧占比 0.0–1.3%。
- 所以弱翼臂的剩余差距**不是场地问题，是学习问题**。这正落在"用模拟训练、不手写状态机"
  的范围内——我的职责是修链路缺陷，不是替它想一个落地策略。
- 观测里**没有地形特征**（只有 `py`/`vy`/`wm` 等）。平台在固定的 y 上，所以"落到
  y≈6998 / 6038"原则上可以从 `py` 学出来，但它是个信用分配难题。
  **不能靠加观测特征来解决**——121 维布局一改，全部 checkpoint 作废。

### 36.3 顺带确认的事

弱翼臂在**地面上**的落地比例差异极大（`flw121` 52.9–64% 对 `fch121` 0%），说明"落地"
这个行为本身在不同配装间差异很大，不是所有弱臂都统一缺失。

## 37. 前端两处真缺陷：梗被改写、梗行换字体会让正文跳 4px（2026-09-23 11:05）

前端重做定稿后，所有者认可观感（"合格，就这样"），但指出一条：**梗被加工过，要求用梗的
原文**。查证后发现这不只是措辞问题，而是两个可测量的缺陷。

### 37.1 梗被改写，并破坏了登记表的"逐字一致"契约

`docs/meme-register.md` 是梗的单一事实来源。它第五节明确写"与
`src/Chaite.Manager/MemeVoice.cs` 逐字一致"，而重做时 `MemeMoment.Scope` 两条被改成：

| 登记表原文 | 被改写成 |
|---|---|
| `固定表只认一条公式：猪鲨` | `只认猪鲨，别的不拆` |
| `不搜路、不评分、不中途换装` | `只走位，不碰武器` |

其余 7 个时段的池子里也有大量**自己编的句子**（`正在数你有没有 300 颗`、
`先别动，我看一眼`、`拆到一半不能停`、`打不过就跑，跑得掉就是赢` 等），
并把原文长句压缩成了 `打这个 BOSS 要 300 颗`、`星星炮已经架好了`。

**修法**：`MemeVoice` 每一行现在都**逐字来自登记表**，不再有登记表里没有的句子。

| 时段 | 台词（全部逐字取自登记表 §一 / §二 / §五） |
|---|---|
| `Boot` | 来吧，试一下米妮 / 星星炮大战骷髅王 / 桑百颗 |
| `Inspecting` | 正在看这个 BOSS 是不是设计失败的 / 打这个设计失败的 boss 必须要有 300 颗 / 桑百颗 |
| `Ready` | 来吧，试一下米妮 / 桑百颗 / 星星炮大战骷髅王 |
| `Installed` | 来吧，试一下米妮 / MAN / MANBA OUT |
| `Blocked` | 这个波斯可是超囊的对我来说 / 从来没试过哦 / 我没有史莱姆 ang 啊 |
| `Busy` | 桑百颗 / 星星炮大战骷髅王 / 来吧，试一下米妮 |
| `Footer` | MANBA OUT / MAN / 亡了亡了 |
| `Scope` | 只拆猪鲨一个；别的波斯都超囊 / 固定表只认一条公式：猪鲨 / 不搜路、不评分、不中途换装 |

池子在时段间重复是**故意的**：登记表把这一组称作一个"前端头部梗语池"，某个时段没有
自己的登记台词时就从登记表取，而不是新编一句。

### 37.2 梗行换字体 ⇒ 正文整体跳 4px（真缺陷）

`FontBook` 按字符串是否含 > U+2E7F 的字符**逐字符串选族**：纯拉丁用 IBM Plex Sans，
含中文用 Noto Sans SC。两族 ascent/descent 不同，于是**行盒高度不同**。

标题区是"标题 + 梗行"的单列表格，高度由两行之和决定 ⇒ 梗行一族一变，标题区高度就变
⇒ 它下面**每一条横线都跟着移动**。实测：

| 梗行 | 标题区以下各横线的 y |
|---|---|
| 混排（`来吧，试一下米妮`） | 113 / 264 / 334 / 402 / 470 / 538 |
| 纯拉丁（`MAN`） | **109 / 260 / 330 / 398 / 466 / 534** |
| 页脚线 | 1079（不动） |

即**整版上移 4px**。梗行每轮换一次就可能触发一次，界面会持续轻微抖动。

**修法**：给梗行加**行高下限**，取两族中较高者
（`FontBook.LineHeight()` = `max(Plex.Height, Noto.Height)`），
于是无论当前句子落到哪一族盒高都相同。字号抽成 `UiTheme.ParagraphPoints` 常量，
避免下限与字号漂移。

**负对照**（证明这个检查不是空转）：临时去掉下限后 `--ui-smoke` 立刻失败并报出真实差值
——`Meme line changes height with its face: 24px all-Latin vs 28px mixed`，
六种尺寸各一条，`Failures: 6`、`exit 1`。恢复后 `Failures: 0`。

**修复后实测**：`default` / `default-installed` / `default-blocked` 三种状态的横线位置
完全一致（都是 113 / 264 / 334 / 402 / 470 / 538）。该断言已固化进
`UiSmokeTest.ValidateMemeElements`，以后任何人再让字体选择影响梗行盒高，构建就会红。

### 37.3 顺带清掉的

`src\Chaite.Manager\Fonts\` 里有 3 个上一轮遗留、**零引用且未进 csproj** 的
`Rajdhani-*.ttf`（共 1.08 MB），已删除。字体产物现在只有 4 个子集 ttf + `OFL.txt`。

### 37.4 过程记录

`Start-Process -ArgumentList '--ui-smoke', $dir` 在路径含空格时**不加引号**，
exe 收到残缺路径后 `exit 0` 且一张 PNG 都不写。冒烟测试必须给含空格路径加引号，
并且**必须核对产物数量**才能判断它真的跑了——只看退出码会被骗过去。

## 38. `policies/` 里的光女遗留会随发行一起发出去（2026-09-23 11:20）

清点光女残留时发现 270 处引用，绝大多数在**历史文档**里（按纪律不改写历史），
但 `policies/` 是一处**会随发行一起发布**的真实遗留。

### 38.1 事实

- `policies/` 下有 4 个 `empress-strong-wing-*.policy.txt`（各约 140 B）。
- **没有任何代码加载它们**：`git grep` 在 `src/`、`tools/`、`training/` 里找不到
  枚举 `policies/` 目录或 `*.policy.txt` 的代码（命中的 `policy_net` 是 SB3 的、
  `--policy` 是 argparse 的，都无关）。
- 它们引用的 `FormulaRoute.EmpressStrongWingsDash` **已不存在**
  （现存成员：`None` / `FishronFairyWingsDash` / `FishronStrongWingsDash` /
  `FishronTrustyChillet` / `FishronTrustyChilletIgnis` / `FishronLilithWolf`），
  所以即使有人手动指定也**加载不了**。
- `tools/publish-github.ps1` 的排除规则是
  `artifacts|research|bin|obj|packages|MEMORY|DEVELOPMENT_NOTES|exe|dll|wav|…`，
  **不含 `policies`** ⇒ 这 4 个文件会**进入 GitHub 发行**。

### 38.2 处理：移进 `artifacts/`，而不是删除

`policies/README.md` 明确写 `empress-strong-wing-nudge-left` 是
"the first policy artifact of this project"，属 **provenance**。所以**不删**，
移到 `artifacts/retired-policies-empress-20260923/`：
`artifacts/` 按纪律永不删除，且**正好被发行排除**，两头都满足。

同时修掉 `policies/README.md` 里一处会误导人的**过期示例**：
`CHAITE_POLICY_ROUTES=EmpressStrongWingsDash`（这个枚举成员已经不存在了）
改为 `FishronStrongWingsDash`，并列出当前全部合法成员。文件表里那 4 行改为划线历史行，
注明去向。**测量数据一行未动**——README 里的 script/nudge 对照表、
`dmgPerK` 排名、`-MergeSideStates` 那段分析全部保留。

### 38.3 顺带确认：测试里的光女引用是**反向断言**，保留

`tests/Chaite.Tests/SupportedBossPolicyTests.cs:41` 等处的写法是
"The Empress of Light left the production scope, so her NPC id is…"，
即断言**她的 NPC id 必须被拒绝**。这是有价值的守卫，不是遗留，不动。

### 38.4 验证与一个观察

- 测试不读 `policies/`（`git grep` 在 `tests/` 里只有一条注释命中），
  移动后 `Chaite.Tests.exe` 连跑 4 次都是 **756 通过 / 0 失败**。
- **观察**：5 次运行中有 1 次报 `755 通过 / 1 失败`，重跑不复现。当时训练 wave
  满载（6 个游戏进程 + 12 个 python），最可能是负载下的时序敏感项。
  **尚未定位到具体用例**，记在这里不当作阻塞。

## 39. 训练读数口径错误、一个真实台阶，以及挂载脚本的验收门槛过期（2026-09-23 13:40）

### 39.1 先说我自己犯的错：按"每 400 场一块"切轮是错的

我先用"把 episodes 从末尾按 400 场切块、块≈一轮"来分析逐轮胜率，据此报告
"fsw121d 从 58.2% 单调下滑到 40.0%"。**这个结论是错的。**

真实口径：`training/run-session.ps1` 的 `stepsPerRound = 60000`，而
`ep_len_mean ≈ 3050`，所以**一轮只有约 20–30 场**。`bridge.episodes.jsonl` 的
`e` 字段**每轮重置为 0**，全文件共 87–93 次重置——用它才能切出真正的轮边界。

按 400 场切块会横跨约 14 轮，把不同阶段混在一起，于是造出"单调下滑"的假象。
**正确做法：以 `e` 回绕到 0 作为轮边界。** 这是以后分析逐轮读数的唯一口径。

按真实轮边界重算，近 14 轮是**平的**，不是下滑的：

| 臂 | 近 14 轮胜率 | 水平 |
|---|---|---|
| fsw121d | 35.5 77.8 50.0 31.4 42.9 33.3 41.7 26.7 36.0 40.0 38.5 41.4 33.3 25.0 | 约 37% |
| fsw121p | 35.7 33.3 37.5 — 42.3 30.0 24.1 40.0 44.0 39.1 35.7 25.9 10.7 20.0 | 约 35% |
| fsw121q | 30.8 25.0 28.6 — 36.0 18.2 27.6 14.5 15.2 19.4 26.7 17.2 34.8 40.0 | 约 25% |
| ffw121 | 13.8 14.3 19.4 28.6 22.2 17.6 26.7 24.0 20.0 25.9 18.5 20.0 20.7 9.1 | 约 20% |
| fch121 | 7.9 7.9 10.5 13.2 4.5 2.3 4.0 5.0 3.9 5.7 8.6 12.2 2.3 14.3 | 约 7% |
| flw121 | 6.9 13.8 9.1 — 13.3 11.6 16.7 13.9 11.4 16.1 5.7 5.6 17.9 18.2 | 约 12% |

### 39.2 但确实有一个真实台阶：第 49 轮

把 fsw121d 的**全部 93 轮**按 `e` 回绕切开，胜率序列里有一个干净的台阶：

```
#34-36:  68 53 18
#37-48:  80 83 88 83 85 79 82 69 76 68 65 67   <- 80-88%，就是 AGENTS.md 记的 89.7% 时期
#49-59:  35 37 46 30 52 50 52 35 57 48 50      <- 掉到约 45%，此后 40 多轮再没回去
```

同时 `mean ticks` 从 #37-48 的 3500–4130 掉到 #49 之后的 2400–3300。

**时间定位（估算）**：当前 wave 的第 10–11 轮在 12:07，即**一轮 ≈ 12.3 分钟**。
据此第 49 轮 ≈ 09-23 **03:00–03:20**。而 AGENTS.md 记载 `frameSkip` 由 2 改为 1
正是在 **2026-09-23 03:00**。

**假设（尚未证实）**：`frameSkip` 2→1 让训练窗口从 80% 掉到 45%。这不是"训练坏了"，
而是**任务变难了**——每 tick 都决策意味着每 tick 都有机会走错。而生产端本来就每 tick
决策（`LearnedPolicy.cs:209-210`），所以训练窗口的下滑**可能同样发生在生产端**。
反过来，旧时代的 89.7% 挂载成绩是在"按 2 tick 训练、按 1 tick 部署"的**不匹配**下取得的，
说明那个不匹配对 fsw121d 反而无害。

这条只能用**挂载彩排**证实或推翻，所以下面的彩排同时挂了当前 checkpoint。
`mount-policy.ps1` 新增 `-Checkpoint` 参数以便对比同一臂历史上两个点。

### 39.3 另一个真实发现：挂载脚本的验收门槛是过期的

`training/mount-policy.ps1` 的 `Get-Bar` 写死：

```powershell
if ($Scenario -like 'empress*') { return @{ Win = 0.80; Clean = 0.80; CleanIsWin = $true } }
return @{ Win = 0.80; Clean = 0.50; CleanIsWin = $false }
```

即猪鲨的判定是 **80% 胜率 且 50% 无伤**。但用户 2026-09-23 已把判据改为
**只要胜率 ≥ 90%、不再考核无伤率**。后果：一个 **95% 胜率 / 30% 无伤**的彩排会被
判成 `not yet`，而按现行判据它应当 **PASS**。这是"验收口径已变、脚本没跟上"的缺陷。

已改：

- `Get-Bar` 只返回 `@{ Win = 0.90; Clean = 0.00; CleanIsWin = $false }`，
  并删掉**光女分支**（她已退出范围，分支不可达）。
- 判定改为 `$pass = ($winRate -ge $bar.Win)`。无伤率仍然测量并打印，
  但**不再是闸门**——它是有用上下文，不是判据。
- 顺带删掉未使用的 `$scored` 变量。

### 39.4 本轮还确认的两件事

- `mount-policy.ps1` **不重建 Core**（无 build/MSBuild/dotnet 调用，只调
  `export_policy.py` 与 `runprobe.ps1`），所以彩排与训练用的是同一份 DLL。
  已核对：`src\Chaite.Core\bin\Release\net48\Chaite.Core.dll` 的 SHA256 前缀
  `E9A4AD9AA823CCF1F21C89C4` 与 wave 探针目录里的副本**逐字节一致**。
- 历史里有一批**异常轮**：399 场、平均仅 500–950 tick、胜率 0–17%
  （fsw121d 的 #4-10、#15-22、#30-32）。正常轮是 20–30 场、约 3000 tick。
  这批是训练器/探针没正常挂上的轮，**读逐轮数据时必须把它们排除**，
  否则会把它们误当成"策略很差"。

## 40. 挂载彩排的实测结果：strong-wing 达标，另外三套暴露同一个链路缺陷（2026-09-23 14:05）

### 40.1 结果

停掉 wave（`stop-all.ps1`，6 个 checkpoint 全部快照并验证可加载）后，用
`mount-policy.ps1` 挂了 5 个可挂候选，每场 40 局、`runTickBudget=800000`：

| 配装 | arm | 真实场次 | **挂载胜率** | 无伤 | hits/1kt | 中位 tick | 判定 |
|---|---|---|---|---|---|---|---|
| **strong-wing** | **fsw121d** | 39 | **97.4%** (38) | 41.0% | 0.437 | 4350 | **PASS** |
| strong-wing | fsw121q | 39 | 89.7% (35) | 0.0% | 0.842 | 4349 | 差 0.3% |
| fairy-wing | ffw121 | 39 | 43.6% (17) | 0.0% | 1.583 | 4215 | 未达标 |
| trusty-chillet | fch121 | 20 | **0.0%** | 0.0% | 4.607 | 1613 | 未达标 |
| lilith-wolf | flw121 | 39 | **0.0%** | 0.0% | 4.467 | 2210 | 未达标 |

**strong-wing 达标**（三选一，fsw121d 97.4%）。另外三点结论：

1. **"训练窗口严重低估挂载表现"是真的，而且量级很大**：fsw121d 训练窗口约 37%
   而挂载 97.4%（约 2.6 倍）。ffw121 20%→43.6%、fsw121q 25%→89.7% 是同一方向。
   因此 §39.2 里"frameSkip 改动导致回退"的假设**不成立**——训练窗口从 80% 掉到 45%
   并没有让挂载表现跟着掉。
2. **两套坐骑路线不是"没召唤坐骑"**：obs 里 `ma`/`mt` 实测 fch121 73.5% 帧坐骑激活
   （type 64）、flw121 84.5%（type 52），训练期分别是 83.4% / 82.4%，一致。
   坐骑在，但**照样 0%**。
3. **mobility 校验的驳回次数与胜率强相关**，见下。

### 40.2 根因（已定位到代码）：公式路线的后置条件在驳回导出策略的 dash

插件日志里 `mobility validation rejected` 的分布：

| arm | 驳回数 | 原因 | 挂载胜率 |
|---|---|---|---|
| fsw121d | **1** | `dash-trajectory-input-changed` | 97.4% |
| fsw121q | 2 | 同上 | 89.7% |
| ffw121 | **236** | 229 次 `dash-edge-or-direction-changed` + 7 次 `dash-trajectory-input-changed` | 43.6% |
| fch121 | **511** | `formula-mount-dash=trusty-chillet-native-dash-state-changed` | 0% |
| flw121 | 0 | — | 0% |

`src/Chaite.Plugin/TerrariaFacade.cs:3973-3976`：

```csharp
if (!current.ControlDash || current.ReleaseDash != expected.ReleaseDash ||
    current.ControlLeft != expected.ControlLeft ||
    current.ControlRight != expected.ControlRight)
    return Reject("dash-edge-or-direction-changed", out reason);
```

`EquivalentDashState` 是一组**后置条件**校验：它假定"计划是在一个已知快照上算出来的，
所以施加后左/右键状态、dash 相位、时钟都必须与预期一致"。这对**手写公式路线**成立，
但**导出策略是每 tick 现决策的直接控制指令**，这个前提不成立，于是被驳回，
`ResolveRejectedPendingMobility`（`:3843`）把控制回落成 `all-controls-neutral`，
**策略学到的 dash 被丢掉**。

关键证据：`TerrariaFacade.cs` 里 `grep _exportedPolicy` **零命中**——
**没有任何针对导出策略的绕过**。校验对公式脚本和导出策略一视同仁，
但它描述的是公式脚本的契约。

注：`flw121` 驳回为 0 却也是 0%，说明**驳回不是唯一原因**；它另有问题——
`fch121` 的 y 只在 7490..7954 的贴地窄带里活动，而两层平台对应 y 约 6998 / 6038，
即它从没到过第一层平台，与 §36 的"平台占用 0.0%"一致。

### 40.3 结论

- 这一轮确定的**交付物是 fsw121d（strong-wing）**：97.4% ≥ 90%，`MOUNT-EXIT=0`。
- 另外三套的失败**不是训练量不够**（训练窗口的排名与挂载排名不一致），
  而是**接管链路把策略的动作丢掉了**，属于"修链路缺陷"的范围。
- 下一步的判据是一个**不可能成为空转的引擎内对照**：让导出策略的 dash 不被驳回，
  重挂 ffw121 / fch121 / flw121，看 `mobility validation rejected` 计数是否降到 0、
  胜率是否随之上升。计数变化本身就是改动生效的证据。

## 41. 修复：导出策略的 dash 不再被公式路线的后置条件驳回（2026-09-23 14:17）

用户 2026-09-23 决策："修这个链路缺陷，重挂三套"。

### 41.1 改动（三处，最小面）

根因见 §40.2：`ApplyPlan` 先按 `plan.Dash` 设好 `controlDash`，随后
`PreparePendingMobilityValidation` 拿这个 plan 去跑**公式路线**的 dash 前置条件，
下一帧 `ValidatePendingMobility` 再跑后置条件。公式路线的契约是"计划基于已知快照，
施加后左/右键状态、dash 相位与时钟必须与预期一致"，而导出策略**每 tick 现决策**，
这个前提不成立，于是被驳回并回落成 `all-controls-neutral`。

1. `src/Chaite.Core/Models.cs`：`ControlPlan` 增加 `public bool PolicyOwnsMobility;`
   字段，注释记录实测数字与出处。
2. `src/Chaite.Plugin/ChaitePolicyDriver.cs`：`ApplyAction` 里设
   `plan.PolicyOwnsMobility = true;`——策略写 plan 的地方就是"这个 plan 的移动
   由策略拥有"的唯一事实来源。
3. `src/Chaite.Plugin/TerrariaFacade.cs`：`PreparePendingMobilityValidation` 在
   `ClearPendingMobilityValidation()` 与 `_pendingMobilityFrame = _sightFrame;`
   之后**提前返回**，不再 arm 任何公式路线的校验。

提前返回的位置是刻意的：`ClearPendingMobilityValidation()` 必须仍然执行，
否则上一帧公式路线留下的 pending 状态会残留；而 `PrepareLateMobilityFallback`
不该执行，因为那个 fallback 是给"被驳回的公式 dash"用的，策略不需要。

安全性论证：`ApplyPlan:3289` 已经按 `plan.Dash` 设好了 `controlDash`，所以提前返回
等于**按策略写的原样执行**；而 vanilla 对未就绪的 dash 会直接忽略，这正是这个校验
对"每 tick 决策的策略"唯一还能提供的保护。

### 41.2 验证

- 构建 exit 0；`Chaite.Tests.exe` **756 通过 / 0 失败**（`PolicyOwnsMobility`
  默认为 false，所以所有走公式路线与合成 plan 的测试行为不变）。
- 新测量组：`InputCoreSha256 = 99A6D71274E1532FC88614383CC2A5C0A70DBFA98F7D6198E50E49891B1E9009`
  （旧 `E9A4AD9AA823CCF1F21C89C43214F55E…`）、
  `InputPluginSha256 = 75D443A41D4D0D6AFFC747836743275172F95C637A5E923C00D01E10D11639A7`。
  `ProbeSourceSha256` 不变（未动 `tools/GameProbe.cs`）。
- **引擎内对照（不可能成为空转）**：改动前 ffw121 的插件日志有 236 行
  `mobility validation rejected`、fch121 有 511 行；改动后新起的四场彩排
  （ffw121 / fch121 / flw121 / fsw121d）该计数**全部为 0**。
  计数从三位数掉到零，本身就是改动生效的证据，与胜率是否提高无关。

胜率结果见后续章节。

## 42. 修复后的重挂结果：假设被部分推翻，三套仍是策略能力不足（2026-09-23 14:30）

### 42.1 结果

| arm | 改动前胜率 | 改动后胜率 | 驳回数（前→后） | 其他变化 |
|---|---|---|---|---|
| **fsw121d** | 97.4% | **97.4%** | 1 → 0 | 无回归（回归对照通过） |
| ffw121 | 43.6% | **43.6%** | 236 → **0** | hits/1kt 1.583 → 1.569（基本不变） |
| fch121 | 0% | **0%** | 511 → **0** | **hits/1kt 4.607 → 3.278、中位存活 1613 → 2776** |
| flw121 | 0% | 0% | 0 → 0 | 逐位相同（ticks=96231, hits=431, deaths=40, bossLife=38300） |

### 42.2 结论：驳回是症状，不是根因

§40.2 把"mobility 校验驳回 dash"判为 ffw121/fch121 低胜率的**原因**，这个判断
**被数据部分推翻**：

- 驳回计数确实从 236/511 掉到 **0**，改动在引擎内**确实生效**（这半边成立）。
- `fch121` 的行为也**确实变了**：存活时间 +72%、hits/1kt 降 29%——说明策略学到的
  dash 现在真的执行了。
- 但**胜率没有跟着起来**：ffw121 一动不动（43.6%），fch121 仍是 0%。
- `fsw121d` 作为回归对照保持 97.4%，说明改动没有副作用。

所以驳回是"策略下达了它本来就不该下达的 dash"的**症状**，而不是它赢不了的原因。
`flw121` 尤其能说明问题：它改动前就是 **0 次驳回**，重挂后逐位相同——那条路线
从来就没有被这个校验碰过。

### 42.3 至此已排除的链路缺陷清单

这一轮把能查的链路缺陷全部查过并修掉了，**没有一个是这三套失败的原因**：

| 候选原因 | 处置 | 结论 |
|---|---|---|
| 场地是单层平地，坐骑路线无处落脚 | 加两层平台（§35） | 平台占用仍为 0.0%（§36），不是原因 |
| `frameSkip` 训练/生产不一致 | 统一为 1 | 训练窗口下滑但挂载不受影响（§40.1），不是原因 |
| 挂载脚本验收门槛过期（80%/50% 无伤） | 改为只卡胜率 90% | 是缺陷，但不是失败原因 |
| 挂载闸门查错的日志字符串（假阴性） | 接受 `flight: applied>0` | 是缺陷，但不是失败原因 |
| 公式路线后置条件驳回策略 dash | `PolicyOwnsMobility`（§41） | **驳回归零，胜率不动** |

### 42.4 剩下的解释

三套的真实瓶颈是**策略能力**，不是接管链路：

- 训练窗口长期平台在 7–20%（§39.1 的真实轮口径），且挂载后没有出现
  fsw121d 那种 2.6 倍的放大（fsw121d 37%→97.4%，而 ffw121 20%→43.6%、
  fch121 7%→0%、flw121 12%→0%）。
- §36 已量过：两套坐骑路线在两层平台上的占用率都是 **0.0%**，而
  **观测里没有任何地形特征**，所以策略**无法学会使用平台**。
  这是"坐骑路线赢不了"最可能的解释，但补地形特征会改变 121 维观测布局，
  需要**全部重训**（并可能危及已经达标的 strong-wing）。

因此按验收标准：**strong-wing 达标（fsw121d 97.4% ≥ 90%），另外三套未达标**。
按用户 2026-09-23 的授权（"如果实在不行，就这样验收，挂载并更新发行 github 吧"），
进入"照现状验收 + 挂载 + 更新发行"的路径。

## 43. 挂载与发行的落地情况：挂载已完成，发行被仓库/清单漂移挡住（2026-09-23 14:50）

用户 2026-09-23 决策："照现状验收：挂载 strong-wing + 更新发行"。

### 43.1 挂载（已完成的部分）

生产代码里策略**只**从环境变量加载：`ExportedPolicy.ForConfiguredFile` 读
`CHAITE_POLICY_FILE` + `CHAITE_POLICY_FORMAT`；`config.json` 里**没有**策略字段
（`ChaiteConfig` 无相关成员），`src/Chaite.Manager` 里 `grep -i policy` **零命中**。
所以"挂载"的机制就是环境变量，没有配置文件可改。

已做：

- 把经过验证的导出策略落到仓库：`policies/fishron-strong-wing.policy.bin`，
  140941 B，sha256 `40C41D7542DD544AC3221B826FFC8BEC27429014B72FEFA4A03DEEDC1A853287`
  ——与彩排日志打印的哈希逐位一致。
- `policies/README.md` 补全了挂载说明：原文只覆盖 residual 文本格式（并且声称
  `CHAITE_POLICY_ROUTES` 必需），缺了实际要发布的导出格式。现在两种格式分开写，
  导出格式明确列出 `CHAITE_POLICY_FORMAT=exported`、观测构建变量，以及
  **`CHAITE_POLICY_ROUTES` 不需要**（`LearnedPolicy.cs:216` 在导出格式下主动让位）。
- `CHANGELOG.md` 记录了验收结果、测量组哈希、挂载变量与"另外三套未达标"的原因。

**发现一个挂载风险（尚未处置）**：`CHAITE_POLICY_FILE` 只有 `train-policy.ps1`
会在结尾 `Remove-Item`（L696）；PPO wave 用的 `run-session.ps1` 完全不碰它。
所以如果把策略设成**用户级**环境变量，它会漏进后续训练 wave，
和 `CHAITE_BRIDGE_FILE` 抢控制权。挂载的投递方式因此需要用户定：

| 方式 | 代价 |
|---|---|
| 用户级环境变量 | 会漏进训练 wave（需同时给 `run-session.ps1` 加清理） |
| 仓库内启动器脚本 | 需要新增并发布一个工具；用户要改启动习惯 |
| 只写文档、由用户自行设置 | 不改本机任何全局状态，但"挂载"只到文件+文档 |

### 43.2 发行（被挡住，未完成）

`tools/publish-github.ps1` 的设计前提是**工作目录本身就是发布仓库、且 git 索引恰好
等于发行清单**：

- `:163-165`：`ls-files` 里任何文件不在清单内就 `throw`；
- `:166-167`：已 stage 的文件不在清单内也 `throw`；
- `:152-156`：只有 `.git` 不存在时才 `git init`，远端已有内容时不许初始化无关历史。

实测三组数字：

| 集合 | 数量 |
|---|---|
| 当前发行清单 | 230 |
| 本地工作仓库 `git ls-files` | **331**（其中 **103** 项不在清单内） |
| 远端 `origin/main` | 232 |

本地领先远端 **295** 个提交。按当前清单发布，公开仓库会**少掉 34 个文件**
（docs 26、src 4、tools 3、tests 1，主要是光女与研究笔记），同时**新增 32 个文件**
（本轮新代码、测试夹具、`policies/` 三个文件）。这 34 个的移除与用户"清理历史遗留"
的指令方向一致，但它会**改写公开仓库的内容**，属于需要用户确认的动作。

另外两处**早已存在**的发行缺陷已修掉（否则连审计都过不了）：

- `tools/train-policy.ps1` 内嵌三处本机绝对路径（`D:\personal tasks\_chaite-tools\…`）。
  它自己的注释就写着"默认值是某台机器的布局，而这个文件是发布的"，所以按该意图改为
  环境变量默认：`CHAITE_SEARCH_POLICY_DIR`、`CHAITE_SEARCH_LOG`（空则落到
  仓库内 `artifacts/search-log.jsonl`）、`CHAITE_RUNWAVE`，并在缺失时给出明确报错。
- `tests/Chaite.Tests/fixtures/obsb1.json` 的 `checkpoint` 字段内嵌本机绝对路径，
  改为仓库相对路径 `artifacts/ppo-obsb1/ppo_latest.zip`。测试只加载同目录的
  `obsb1.bin`，没有任何断言读这个字段，所以改动无行为影响。
  （生成它的 `training/export_policy.py` 不在发行清单内，但它会重新写入绝对路径，
  将来重新生成夹具时需要一并处理。）

`policies/` 此前**根本不在**发行白名单里，所以策略文件和 `policies/README.md`
从来不会随发行出去；现已显式加入三个文件。修改后干跑通过：
`PUBLIC SOURCE AUDIT: 230 allowlisted files`，exit 0；
`tools/test-training-objective.ps1` 仍 exit 0（19 项检查，它会 parse `train-policy.ps1`）。

## 44. 挂载与发行完成（2026-09-23 15:05）

用户 2026-09-23 决策：发行"按现清单发布"，挂载"用户级环境变量 + 给 run-session.ps1 加清理"。

### 44.1 挂载

**用户级环境变量已写入并读回验证**：

| 变量 | 值 |
|---|---|
| `CHAITE_POLICY_FILE` | `D:\personal tasks\modding\Chaite\policies\fishron-strong-wing.policy.bin` |
| `CHAITE_POLICY_FORMAT` | `exported` |
| `CHAITE_PROJ_SLOTS` | `12` |
| `CHAITE_PROJ_SORT` | `threat` |
| `CHAITE_PROJ_COLLAPSE` | `1` |
| `CHAITE_OBS_WORLD_BOUND` | `18` |

策略文件 sha256 复核为 `40C41D7542DD544AC3221B826FFC8BEC27429014B72FEFA4A03DEEDC1A853287`，
与彩排日志打印的一致。

**`training/run-session.ps1` 增加了清理**（在 `$env:CHAITE_BRIDGE_FILE = $base` 之后）：
`Remove-Item Env:\CHAITE_POLICY_FILE / CHAITE_POLICY_FORMAT / CHAITE_POLICY_ROUTES`。
理由是挂载变成了用户级变量，训练会话会继承它，而训练期必须由 bridge 独占玩家；
`train-policy.ps1` 自己会在结尾清除（它的搜索本来就会设这个变量），但 PPO wave 从不碰它，
所以不加这段的话，挂载的策略会漏进之后每一轮训练。

注意：环境变量的继承只对**新启动**的进程生效。已经开着的终端/游戏进程不会拿到新值，
下次启动 Terraria 才会用上策略。

顺带核实了一个疑点：`run-session.ps1` 的 `$FrameSkip` 默认值仍是 2、
`$EpisodesPerRound` 是 400、`$StepsPerRound` 是 190000，看起来像 frameSkip 缺陷复发。
实测 `training/sessions.json` 里**所有**会话都是 `frameSkip = 1`、`stepsPerRound = 60000`，
而 `resume-all.ps1:132-133` 会显式传入这两个值。所以那只是过时的默认值，没有被使用，
不是缺陷。

### 44.2 发行

发行脚本的前提是"工作目录本身就是发布仓库、git 索引恰好等于清单"，而本地工作仓库
跟踪 331 个文件（含全部 research/training，其中 103 项不在清单内），所以**必须从一个
干净 clone 发布**。执行过程：

1. `git clone --branch main https://github.com/starchfurrycon/Chaite.git` 到
   `artifacts/publish-clone-20260923-150134`（232 个文件，HEAD = `46cd48d3`）。
2. 移除 clone 跟踪但清单不含的 **34** 项（其中 `src/Chaite.Core/Empress*.cs`、
   `tests/…/PriorityEmpressMoonStrategyTests.cs`、`docs/handoff-empress-nohit.md`、
   `docs/native-witch-broom-policy.md` 等在本地已删除，只是还留在远端）。
3. 从工作仓库复制清单内 **230** 项，`git add -A` 后核对：索引 == 清单（230/230）。
4. 提交 `805243e8`（父提交 `46cd48d3`）。
5. 在 clone 内跑 `tools/publish-github.ps1 -Publish`，exit 0，
   `46cd48d..805243e  main -> main`，
   `VERIFIED: https://github.com/starchfurrycon/Chaite/tree/805243e83a111fd429b808a034b5ad847b6d739d`。

**远端复核**：`main` 现在跟踪 **230** 个文件；
`policies/README.md`、`policies/fishron-strong-wing.policy.bin`、`policies/zero.policy.txt`
都在；按文件名搜 `Empress|empress|witch-broom|handoff-empress` **零命中**，
光女遗留已从公开仓库消失。

（正文里仍有光女字样是**有意保留**的：测试把光女当反向断言，`empressBrooch` 是原版
`Player` 字段。文件名层面的清理才是本次的目标。）


## §45 2026-09-23 疾旋鼬路线删除、前端卡片删除、以及平坦场地 A/B 第一次作废的根因

### 45.1 疾旋鼬（mount 64/65）路线完整删除

用户决定原文：“疾旋鼬坐骑路线机动性确实不足，直接删去吧”。
删除范围按“移除明胶女士鞍路线，含其准入路径”的先例：
枚举成员 `FishronTrustyChillet` / `FishronTrustyChilletIgnis`、
`IsTrustyChillet`、`TrustyChilletMountType`、`Select` 里的准入、
`IsAcceptableMount` / `BelongsToBoss` 分支、`FishronChilletScript.cs` 整文、
csproj 项、`CombatPlanner` 两个字段与两个分支、
`FormulaMobilityContract.ExpectedMount` 两个 case、
`TerrariaFacade` 的坐骑冲刺校验块与 `trusty-chillet-native-dash-state-changed` 理由串、
探针与工具的路线白名单、4 个测试文件、以及文档。

**实测依据**：大师难度挂载彩排（三层场地）中，疾旋鼬最高攀爬 **38 格**，
而莉莉丝狼是 **70 格**；平台行间距是 **60 格**，所以疾旋鼬连第一行都上不去（平台占有率 0.00%）。

**不得动的部分**：`MobilitySnapshot` 的字段与
`VanillaMountCatalog` 的 64/65 条目保留——那是游戏数据不是路线，
且 121 维观测布局是按位置排的，增删字段会静默地移它。

**验收**：`tools\build.ps1 -Configuration Release` exit 0；
`Chaite.Tests.exe` **752 通过 / 0 失败**（756 减去 4 个只钉疾旋鼬的用例）。
接受范围相应从 4 套变 **3 套**（strong-wing / fairy-wing / lilith-wolf）。

### 45.2 前端疾旋鼬配装卡删除

用户选择“删掉这张卡，改成 3 张”。
`src\Chaite.Manager\LoadoutCatalog.cs` 删除 `fishron-chillet` 条目；
`UiSmokeTest.ValidateLoadouts` 的“Fishron 必须 4 张”改为 3 张，并记下删除原因。
`SpriteCatalog` 里的疾旋鼬贴图保留（本体游戏素材，
且 `SpriteCatalog.All` 是运行时解析用的）。

**验收**：`Chaite.Manager.exe --ui-smoke <目录>` 产出 **37 张 PNG**、exit 0，
用户看图后确认合格。首次跑出 **0 张 PNG 但 exit 0**，
正是 AGENTS.md 记的陷阱（`Start-Process -ArgumentList` 对含空格路径不加引号）；
修正后重跑并**核对了产物数量**。

### 45.3 平坦场地 A/B 第一次作废，及其根因（一个既有的潜在缺陷）

为回答“平台是否影响强翼既有胜率”，给探针加了 `-arenaflat` 开关
（`GameProbe.cs` 静态字段 + 参数白名单 + 读取 + 建造条件），
并在 `runprobe.ps1` / `mount-policy.ps1` / `start-isolated-test.ps1` 逐层接上。

**第一次两场都 exit 2**：`start-isolated-test.ps1` 的目标参数是 fail-closed 白名单，
`-arenaflat` 被拒（`Unreviewed target argument: -arenaflat`）。已加入白名单，
并补了 `$arenaFlat` 声明（该脚本开了 `Set-StrictMode -Latest`，不声明会抛异常）。

**第二次两场都 exit 1，但数据作废**：
`ProbeSourceSha256` 确实从 `E88A7A70…` 变为 `E226636A…`（说明改动确实编进了探针），
但平坦场地那场的 `result.json` 仍是
`platformRows:[440,380]`、`platformTileCount:798`——**平台照样建了**，
两场数字逐字节相同是因为它们跑的是**同一个场地**。

**根因**：`runprobe.ps1` 把开关拼在 `-skipbeam` **之后**：

```
... '-wallseconds', $WallSeconds, '-skipbeam', '-arenaflat'
```

Terraria 的 `LaunchParameters` 解析器把一个 key 后面那个 token 当作它的**值**并跳过，
所以 `-arenaflat` 被当成 `-skipbeam` 的值吞掉、**根本不是键**，
`ContainsKey("-arenaflat")` 读到 false，开关静默失效。
同理，**`-stoponhit` 一直也被静默吞掉**（它也拼在 `-skipbeam` 后）。
因为 `-skipbeam` 自身仍是个键（它就是那个 `-` token），
它的功能一直正常，所以这个缺陷没有被发现。

**修法**：开关一律放在 `-skipbeam` **之前**，`-skipbeam` 保持最后。

**验证方法**（不可能成为空转的对照）：
平坦场地那场的 `result.json → arena.platformTileCount` 必须从 **798 变为 0**。

**数据保留**：作废的两批都改名保留、未删除——
`bridge-mount-*-master-flat-failedrun1`（0 行 episodes）与
`bridge-mount-*-master-flat-MISLABELED-three-layer`（实际跑的是三层场地）。
旧测量组的彩排数据改名为 `bridge-mount-*-master-groupA-expert-trained`。

### 45.4 新测量组下的强翼与莉莉丝狼基线（三层场地）

删除疾旋鼬改了 Core，`Chaite.Core.dll` 从 `EE648059…` 变为 `4B60B67A…`，
**`InputCoreSha256` 换代**，所以旧组的胜率数字不能直接引用，必须在新组上重测。

| 配装 | 场地 | 胜率 | hits/1kt | 中位 tick |
|---|---|---|---|---|
| fsw121d（strong-wing） | 三层 | **82.1% (32/39)** | 0.462 | 5422 |
| flw121（lilith-wolf） | 三层 | **0.0% (0/39)** | 2.119 | 2603 |

强翼在新组上仍是 82.1%、hits/1kt 0.462、中位 5422，与旧组**逐位一致**——
说明删路线没动强翼路径，也再次证明彩排是确定性的。
莉莉丝狼在新组上仍是 0.0%，与旧组一致。

平坦场地对照修好后重跑，结果待补。


### 45.5 更正：45.3 里的根因推断是错的，真正原因在参数表重建

45.3 里我写的“Terraria 解析器把紧跟在 key 后面的开关吞掉”
**是错的**，而且我由此得出的“`-stoponhit` 一直也被静默吞掉”**同样是错的**。
本节保留错误推断的记录，并写下经引擎内日志确认的真因。

**推翻假设的证据**：`-skipbeam` 在新旧两种顺序下都被识别（日志里有
`BEAM_COMPARE_SKIPPED diagnostic combat iteration only`，该分支只在
`ContainsKey("-skipbeam")` 为真时才走），所以解析器**不吞键**。

**真因**：`tools/start-isolated-test.ps1` 在校验循环之后**从头重建**了参数表：

```powershell
$TargetArguments = @('-savedirectory', $save)          # 旧的全丢
if ($skipBeam)  { $TargetArguments += '-skipbeam'  }
if ($stopOnHit) { $TargetArguments += '-stoponhit' }
foreach ($key in $fixtureArguments.Keys) { $TargetArguments += @($key, $fixtureArguments[$key]) }
```

任何只在**校验循环**里被接受、却没在这里被重建的开关，都会在游戏看到它之前被丢掉。
`-arenaflat` 正是如此：它能通过白名单校验（所以不报错），然后被丢。
而 `-stoponhit` **没有**这个问题，它在 L533 被重建了。

**定调手段**（这一次是它结束了循环）：在 `GameProbe.cs` 读取处直接打印标志值与**全部参数键**。
因为“开关从未成为键”与“读取从未执行”从外面看一模一样：

```
ARENA_FLAT_READ arenaFlat=False launchKeys= -savedirectory -skipbeam -scenario -phase
  -formularoute -takeovertick -seed -difficulty -maxticks -wallseconds      # 修之前
ARENA_FLAT_READ arenaFlat=True  launchKeys= -savedirectory -skipbeam -arenaflat -scenario ...  # 修之后
ARENA_FLAT_BUILD ocean=True monitor=True flat=True
```

**修法**：在 L533 后补 `if ($arenaFlat) { $TargetArguments += '-arenaflat' }`。
后续任何新开关都必须**同时**加进校验循环与这个重建段，否则会重踩这个坑。

**操作教训**：前台命令里长时间 `Start-Sleep` 会在命令被中断时**连带终止后台任务**
（job object 语义）。本次就因此把两个正在跑的彩排一起杀了（部分数据已保留为
`bridge-mount-*-master-flat-killedrun1`）。**等待一律靠任务完成通知，不在前台睡。**

**本次诊断也改了 `ProbeSourceSha256`**（加了两行日志），
属于新的探针源身份；两行日志是常驻的证据，不再移除。

## §46 2026-09-24 生产端从不填 `BossAi2`/`BossAi3`，以及由此牵出的四个链路缺陷

起点是一个观测字段缺口，查下去发现同一类"训练侧活着、生产侧死掉"的缺陷还有一处，
再加上三个把测量和验收本身弄脏的问题。全部按"引擎内可执行、且不可能是空转"的口径取证。

### 46.1 观测第 25/26 维在生产端是恒定的死值（本次主缺陷）

`ChaiteObservation` 的 Boss-clock 块是两维：`bs2` = `ai[2]` 攻击计时器、`bs3` = `ai[3]`
攻击序号。训练侧 `GameProbe.cs` 一直在写这两个键（`{"bs2", boss.ai[2]}`、
`{"bs3", boss.ai[3]}`），而生产端 `TerrariaFacade` 的行构造器**从未给它们赋值**，
于是 `BossAi2`/`BossAi3` 保持默认 0，策略读到的是 `(0+1)/181` 与 `(0+1)/16` 两个常数。

`ChaiteObservation.cs` 的文档注释自己就写着"调用方若保留默认值 0，就会报告'攻击刚开始'"，
契约是对的，是调用方没遵守。修法是按探针同样的哨兵填：

```csharp
BossAi2 = target.Ai2Known ? target.Ai2 : -1f,
BossAi3 = target.Ai3Known ? target.Ai3 : -1f
```

**引擎内证据**：`TerrariaFacade` 加了静态诊断钩子（`Runtime` 在建好驱动后接到
`chaite.log`），把最初 8 行的攻击时钟打出来。三个臂一致：

```
ROW_DIAG n=1 ai0=0  ai2=0 ai3=0
ROW_DIAG n=2 ai0=-1 ai2=1 ai3=0
...
ROW_DIAG n=8 ai0=-1 ai2=7 ai3=0
```

`ai2` 逐 tick 递增，修复前恒为 0。这不可能由空转产生。`ai0` 的 `-1` 用训练侧观测交叉
验证过：`bs` 在 `fsw121d` 取 {0,1,3}、`flw121` 取 {0,1,2,3}，而 `ffw121` 全部是 `-1`，
所以那是 `ai[0]` 的真实取值而非未知哨兵，插件端与训练侧一致，无新缺陷。

**修复效果（大师难度挂载彩排，三层场地）**：

| 臂 | 胜率 | hits/1kt | 中位 tick |
|---|---|---|---|
| fsw121d（strong-wing） | 82.1% (32/39) | 0.536 | 5421 |
| ffw121（fairy-wing） | 0.0% (0/39) | 2.176 | 1929 |
| flw121（lilith-wolf） | 0.0% (0/39) | 1.874 | 3358 |

`fsw121d` 的 82.1% / 0.536 与修复前**逐位相同**，即本修复对 strong-wing 是中性的、
无退步；两个失败臂也没被它改善。所以攻击时钟修复是**正确但不够**，
剩下要靠观测加宽（见 46.4）。

### 46.2 单元测试套件不是自洽的：User 作用域的 `CHAITE_POLICY_FORMAT` 会打红 4 个测试

`LearnedPolicy.EnsureConfigured` 在 `ExportedPolicy.IsExportedFormatSelected` 为真时
主动让位（`_file = null`），其注释明写"格式变量未设置时——这是默认值，也是所有既有调用方
与测试唯一使用的配置"。但生产挂载变量是设在 **User 作用域**的持久变量：

```
CHAITE_POLICY_FORMAT   User=exported
CHAITE_POLICY_FILE     User=...\policies\fishron-strong-wing.policy.bin
```

测试进程会继承它们。`BossMonitoringTests.cs` 的四个 dash 测试只保存/恢复了
`CHAITE_POLICY_FILE` 与 `CHAITE_POLICY_ROUTES`，漏了格式变量，于是
`LearnedPolicy` 让位、`ForRoute` 返回 null、`FormulaScriptController` 跳过 learned
residual，断言 `adjusted.EndsWith(LearnedSuffix)` 失败。

**证据**：清空这三个变量后同一二进制 **752 通过 / 0 失败、exit 0**；
在继承 User 作用域变量的 shell 里是 **748 / 4 失败**。

**修法**：在 `tests/Chaite.Tests/Program.cs` 的 `Main` 入口一次性清空这三个变量，
让套件与机器状态无关。测试要用的自己设、自己恢复。**测试必须自洽**，
否则任何真正在跑 Chaite 的机器上套件都是红的。

### 46.3 挂载脚本的验收门槛停在 90%

`mount-policy.ps1` 的 `Get-Bar` 返回 `Win = 0.90`，而用户 2026-09-23 已把门槛降到
**80%** 并取消无伤要求。结果是实测 82.1% 的 strong-wing 被报成"未达标"并 `exit 1`。
已改为 0.80。**门槛是用户口径，不是脚本的历史常量**。

### 46.4 `CHAITE_OBS_BOSS_PROX` 从未被引擎读取

训练侧 `chaite_env.py` 有 `OBS_BOSS_PROXIMITY`，在 aggregates **之后**追加 4 维
（`clip(gap/300)`、`gap<=32`、`closing>0 && dd!=0`、`dd==0 && eo==0`），
动机写在代码里：基础块唯一的距离特征是 `dist/1500`（第 16 维），把整个 0–200 px
危险带压进 0.000–0.133，粗了 15 倍；两代策略都在 1.9 hits/1000 ticks 处平台化。

而 `src/` 里 `CHAITE_OBS_BOSS_PROX` 命中数为 **0**，所以任何用它训练的臂都不可挂载。
本次把它接进生产端：`ChaiteObservation` 新增 `BossProximityFeatureCount = 4`、
`BossProximityVariable`、`BossHalfExtentFallback = 50f`、`BossProximity` 属性与构造参数、
`ObservationCount` 项、`FromEnvironment` 解析、`RequireObservationCount` 报错信息与特征名，
`Fill` 里**紧跟 aggregates 之后**追加同样四值；行里补 `BossWidth`/`BossHeight`
（`TerrariaFacade` 从 `target.Width/Height` 填）。

启动链路本来就预置好了：`run-session.ps1` 有 `[switch]$BossProximity` 并设置环境变量，
`resume-all.ps1`/`start-campaign.ps1` 从 `sessions.json` 读取传递。只有 `mount-policy.ps1`
因为"引擎不读"而拒绝挂载这类臂——已解除并改为传递开关。

**训练侧实测**（`ppo.log`，不是推断）：

```
observation world bound = 18.0 (OBS_DIM=125)
observation blocks: projectile_slots=12 aggregates=off boss_proximity=on
```

**warm-start 加宽**：`warm_start_obs.py` 新增 `PROX` 块，121 维 checkpoint → 125 维目标。
独立校验证明前 121 列**逐位落在同一索引**（`max |dst[:, :121] - src| = 0.000e+00`）、
末尾 4 列严格为 0，并用"平移一列"的对照给出 `1.226e+01` 的非零差，证明该判定不是空转。
原 121 维检查点按纪律改名为 `ppo_pre-prox-20260924-081910.zip` 保留，未删除。

**新测量组**：`Chaite.Core.dll` 哈希从 `4B60B67A…` 变为
`D4185F3D299C5DD7208740A239551F36E109FD4B3EA3DFB65DD64091E0F000EB`。
旧的 82.1% 属于旧组，strong-wing 需在新组复测。构建后 752 通过 / 0 失败。

### 46.5 `EXPORT MATCHES SB3` 是必要而非充分条件

`export_policy.py` 的 `checkpoint_layout` 只按 `(npc,boss,jump,agg)` 搜索，125 宽会被
判成 `agg=4`。已修为返回 5 元组、由 `CHAITE_OBS_BOSS_PROX` 决定**搜索顺序**
（121 宽即使开关打开仍解析为无 prox），并加一行 NOTE 把"环境决定的歧义"显式打出来。

**但真正必须记住的是**：`EXPORT MATCHES SB3` **无法区分两种 4 宽尾块**。
反向对照（同一个 125 文件、开关不设 → 判成 `agg=1`）导出的 `.bin` 与正确路径
**逐字节相同**（`2895FA47…`，142989 字节），因为 verify 把**同一个 obs 向量**分别喂给
SB3 与导出策略，布局根本不参与判定。

所以该判定是**必要而非充分**的。挂载正确性由另外两处独立保证：插件侧
`RequireObservationCount` 是宽度闸门，`mount-policy.ps1` 依据 `sessions.json` 的
`bossProximity` 传 `CHAITE_OBS_BOSS_PROX=1`。**不要把 "EXPORT MATCHES SB3" 当成
"布局正确"的证明。** 无回归的硬证据是另一条：从 121 检查点新导出的 `.bin` 与改动前
07:56 导出的 `ffw121.bin` 逐字节相同（`B5D2E397…`，140941 字节）。

## §47 2026-09-24 夹具的距离衰减修正与 tickLimit 16000 → 40000

### 47.1 缺陷：注入伤害与距离无关，夹具奖励"逃跑"

`tools\GameProbe.cs` 的模拟玩家伤害（`simulatedDps`）此前**与玩家到 Boss 的距离无关**，
所以"跑远"在夹具里零成本，而真实游戏里跑远等于没有输出。**夹具一直在奖励一个真实游戏
会惩罚的策略**，此前所有胜率数字都是在错误目标上取得的。

已修：smoothstep 距离衰减，**30 格（480 px）内满 DPS、80 格（1280 px）归零**，由
`CHAITE_SIM_DPS_FULL_TILES` / `CHAITE_SIM_DPS_ZERO_TILES` 覆盖；非法组合响亮拒绝并 clamp；
**读不到距离时 fail closed 判 0 伤害**并打 `SIM_FALLOFF_UNREADABLE`，绝不回退满 DPS。

**引擎内证明**：逐 tick 伤害对距离的 pearson 从 **−0.0170（平的）** 变为 **−0.8997**，
对曲线预测因子为 **+0.9989**；逐 10 格分桶与 smoothstep 逐桶吻合。

**后果**：同一策略 `policies\fishron-strong-wing.policy.bin`（sha `40C41D75…`）在新夹具下
大师胜率 **82.1%（32/39）→ 5.1%（2/39）**，hits/1kt 0.536 → 0.666，中位 tick 5421 →
**9149**（最长 11172）。原因是旧 82.1% 那次有 **76.1% 的战斗 tick 在 30 格外、25.7% 在
80 格外**，新口径下零输出。旧证据改名保留在
`artifacts\bridge-mount-fsw121d-master-oldfixture-pre-distance-falloff-20260924\`。

### 47.2 缺陷：撞上 `tickLimit` 会终止整个 run，不只是那一场

`GameProbe.cs` episode 模式里 `ticks - episodeStartTick >= tickLimit` 会调用
`Finish("test-time-limit")`，而 `Finish` 在 episode 模式之外**结束整个进程**——所以一场慢
战斗会丢掉该轮**剩余的全部行**。战斗时长翻倍后（中位 5421 → 9149，最长 11172；弱翼更慢）
旧值已经逼近上限，这不是"少一场"，而是"少一轮"。

### 47.3 修复：所有设置点一致抬到 40000

| 位置 | 旧值 | 新值 |
|---|---|---|
| `tools\GameProbe.cs` `tickLimit` 默认 | 24000 | **40000** |
| `tools\GameProbe.cs` `-maxticks` 上界 | 24000 | **40000** |
| `tools\GameProbe.cs` `-takeovertick` 上界（= tickLimit − 120） | 23880 | **39880** |
| `training\run-session.ps1` `-MaxTicks` 默认 | 16000 | **40000** |
| `training\mount-policy.ps1` `-PerEpisodeTicks` 默认 | 16000 | **40000** |
| `training\train-policy.ps1` `-MaxTicks` 默认 | 12000 | **40000** |
| `training\evaluate_policy.py` `--max-ticks` 默认 | 16000 | **40000** |
| `_chaite-tools\runprobe.ps1` `-MaxTicks` 默认 | 12000 | **40000** |
| `tools\start-isolated-test.ps1` `-maxticks` 校验上界 | 24000 | **40000** |
| `tools\start-isolated-test.ps1` 缺省 `$maxTicks` | 24000 | **40000** |

`tools\GameProbe.cs` 里 `runtimeSamples`/`engineSamples` 的容量提示同步 24000 → 40000。
`CHAITE_RUN_MAX_TICKS`（380000，run 级）与 32 位地址空间 ~661k tick 上限都远在 40000 之上，
所以这次抬高不会碰到内存上限；**run 级预算与每场预算是两个不同的量，仍然不得混用**
（§26.3）。

### 47.4 测量组身份

`GameProbe.cs` 变更后 `ProbeSourceSha256`：`DBF27AA6…` → **`58418BD4…`**。
`src\Chaite.Core` 未改动，`InputCoreSha256` 仍为 **`D4185F3D…`**。引擎内对照：obs 行的
`tlim` 字段必须回读 **40000**（探针把 `tickLimit` 逐行写出），这是"改动真的到达引擎"的
证明，而不是"改完就算"。

## §48 2026-09-24 晚 公式表闸门在生产挂载路径上无条件 Cancel，截断 48.7% 的场次

### 48.1 缺陷

`src\Chaite.Core\FishronFormulaStateContract.cs` 的 `case 8` 只承认 `NativeSequence == 0`，
而本体 AI 在**阶段 2 的第 6 次攻击**进入 state 8 时 `ai[3]` **已经是 1**。于是
`CombatPlanner` 判定"公式表不覆盖这一段"，把操作权归还给本体 AI，`Runtime.cs:337` 紧接着
**无条件 `Cancel()`** —— 生产挂载路径上 **39 场里有 19 场（48.7%）** 被这条闸门截断。

截断是**确定性**的：这 19 场的 `ticks=3402`、`bossDamage=55560`、`bossLifeRemaining=43874`
**字段完全一致**，是同一个截断点，不是随机波动。

### 48.2 为什么长期没被发现

v1 手写状态机**从没活到那一步**：951 次冲刺里 **949 次发生在阶段 1**、阶段 3 为 **0**，
所以在它身上这条闸门永远不触发，被"早死"掩盖了（v1 原始数据
`artifacts\fishron-state-machine-zero-residual-20260924\`）。

### 48.3 修法（依据用户既有指令）

用户既有指令："公式表闸门不得拦住导出策略实际飞行的那一段，归还请求应被忽略而不是生效"
（`AGENTS.md`「职责边界」，对应 `CombatPlanner.cs:785-793` 的
`UnsupportedMobilityRoutePlan`）。据此两处修改：

1. **契约放宽到承认本体可达集。** 全表审计逐条比对引擎实际出现过的 `(ai[0], ai[3])` 对，
   证明**只有 `case 8` 是错的**，其余逐条吻合；`TimerLimit` **无需改** —— 每个出现过的
   元组的 `max(ai[2])` 恰好等于上限减一。
2. **`IsValid` 失败不再归还操作权。** 改为记 `LastFormulaTableMiss*` 后**继续走脚本**；
   `Runtime` 通过只读口 `FormulaTableMiss` 把命中写进 `chaite.log`，让"公式表漏了"变成
   可观测读数，而不是一次静默的接管取消。

### 48.4 引擎内对照

修前 `安全条件持续丢失` **19** 行、修后 **0** 行；`Cancelled` **19 → 0**；彩排分母从被截断的
20 恢复到 **39**。逐条元组审计与引擎内证据见 `artifacts\fishron-formula-tuple-audit.md`。

## §49 2026-09-24 晚 `mount-policy.ps1` 默认 run tick 预算高于 32 位进程崩溃线

### 49.1 缺陷

`training\mount-policy.ps1` 的默认 run 级预算是 `$RunTickBudget = $Episodes * 20000`，
40 场即 **800,000** tick，而 32 位进程实测在 **~661k tick** 前后崩溃：`train7` 与 `esd1`
两个臂都死在 661,000 ± 20 tick 内。这个默认值与 `-PerEpisodeTicks` **无关**（每场 40000 时
它既不对应 1 场也不对应 N 场的预算），并且一旦用完 run 预算，`Finish("test-time-limit")`
会**终止整个 run**（§47.2），后续场次退化成 `ticks=1` 的退化行 —— 表面看"跑完了"，
实际丢掉整轮剩余数据。

### 49.2 修法

改为**从每场上限派生**并**显式 clamp**：

- `$RunTickMargin = 1.5`、`$RunTickCeiling = 650000`；
- `$RunTickBudget = min(ceil($Episodes * $PerEpisodeTicks * $RunTickMargin), $RunTickCeiling)`，
  触发 clamp 时**大声打印**原因；
- **显式传入的 `-RunTickBudget` 一律尊重**，只在上限之上给警告、**绝不静默 clamp**；
- 超过上限时提示：**接受场数要从结果里读、不要信 `-Episodes`**。

650,000 的依据是实测崩溃线 ~661k 留出约一场中位战斗的余量，让 run 干净收尾并把最后一行
写下来，而不是在 661k 处硬崩。

### 49.3 逐字抽取实测（`artifacts\_verify-mount-budget.ps1`）

脚本从 `mount-policy.ps1` 里**逐字抽出**派生块执行（不是重写逻辑），6 组参数：

| 输入 | 结果 |
|---|---|
| `40 × 40000 × 1.5 = 2400000` | **CLAMPED 650000**（打印 clamp 原因） |
| `39 × 40000 × 1.5 = 2340000` | 650000 |
| `4 × 40000 × 1.5 = 240000` | 240000（不 clamp） |
| `40 × 10000 × 1.5 = 600000` | 600000（跟随每场上限变化） |
| 显式 `380000` | 380000（无提示） |
| 显式 `900000` | 900000 + ABOVE-ceiling 警告（不 clamp） |

`-UseZeroResidual` 逐字保留：参数表中仍为 `SwitchParameter`，文件内 8 处引用与三个分支
（`Test-Path $zeroResidualPath`、`-not $SkipExport -and -not $UseZeroResidual`、
`if ($UseZeroResidual) {`）原样存在。

### 49.4 行为变化

默认值 **800,000 → 650,000**。全仓库没有任何调用者显式传 `-RunTickBudget`，所以彩排会吃到
这个新默认；实际影响为零：闸门修好后的 39 场彩排中位 **5,353** tick，即使按夹具修正后最慢的
**11,172** tick 计，40 场也只有约 45 万 tick，两个值都够用。要恢复旧值就在命令行显式传
`-RunTickBudget 800000`（代码会尊重它）。

### 49.5 已过期的旧描述（保留原位，以本节为准）

以下两处**保留原位、不再修改**，但内容已过期：

- §26.3（本文件 `:1703`）："`-PerEpisodeTicks`（默认 16000）与 `-RunTickBudget`
  （默认 `Episodes * 20000`）" —— 每场上限已在 §47.3 抬到 **40000**，`-RunTickBudget`
  已改为**派生 + clamp 650000**。
- §40.1（本文件 `:2539`）："每场 40 局、`runTickBudget=800000`" —— 那是 2026-09-23
  当天那次彩排的**实际参数记录**，不是当前默认值。

---

## §50 2026-09-25 手写状态机收束：伤害等于时长、两套 `wt` 上限不同、收束读数与差距、五个被否方案的一句话版

**节点**：手写走位状态机（`src\Chaite.Core\FishronWingScript.cs`）在同一版 Core
`InputCoreSha256 = A6912B16D6B60B615DA00D63E59EC1C56BF58A06788370A757E9F95D869F6487` 下的最终读数，
以及围绕它的四条结构性结论。收尾摘要在
`artifacts\fishron-wing-round-summary-20260925.md`。

### 50.1 本夹具的伤害只与存活时长成正比 —— 不要再用距离/DPS 占比解释胜负

**实测**：弱翼 **39/39 场** `bossDamage / simulatedDamage = 1.000`（逐场 0.999–1.002，
合计 0.9998）；强翼 1.001。`bossDamage/ticks` 分别 15.763（强翼）与 15.052（弱翼）。

**含义**：彩排夹具（`mount-policy.ps1` 走的生产挂载路径）的伤害**等于时长**。
训练夹具那边的 smoothstep 距离衰减（§47）已经修好，但**彩排夹具的伤害本身仍等于时长**。
**所以 "满 DPS tick 占比 / 中位距离" 在彩排夹具里不是胜负的解释变量**；
任何从距离推胜率的推理在本夹具里都无效。
反过来的事实更能说明问题：弱翼其实**比强翼更近**
（30 格内 **62.7%** 对 50.0%、中位距离 **389** 对 480、smoothstep 期望输出系数 0.920 对 0.870），
却因为**活得太短**而打不死。

### 50.2 两套配装的 `wt`（wingTime）上限不同 —— 量的时候必须按配装取值

| 配装 | 翅膀 | `wingTimeMax` |
|---|---|---|
| strong-wing | `ItemID.FishronWings` | **180** |
| fairy-wing | `ItemID.FairyWings` | **130** |

依据：`tools\GameProbe.cs:2574`（注释明写 100 / 130）、`:2978` 与 `:3033`
（`player.armor[4].SetDefaults(formulaRoute=="fishron-strong-wing" ? ItemID.FishronWings : ItemID.FairyWings)`）。

**本轮曾因此得到一个假缺陷**：“弱翼 `wt==180` 占 **0.0%**、从不补满”。
实际是弱翼流里 `wt` 上升**只有一种幅度**：219 次上升中 **201 次正好 `+130`**，
且上升后 `wt` **恒为 130**（0% 达到 170）—— 每次都是**补满到该配装的上限**。
**拿强翼的上限去量弱翼 = 误判。** 另外 `gon`（grounded）**不在观测流里**，
落地只能靠 `wt` 回升推断。

### 50.3 手写状态机的收束读数与差距

| 配装 | 臂 | 胜率 | `hits ≤ 2` | 直方图 | `hits/1kt` | 中位 tick | 距 80% |
|---|---|---|---|---|---|---|---|
| strong-wing | `fsw121d` | **46.2%（18/39）** | **59.0%（23/39）** | `{2:23, 3:16}` | 0.504 | 6,088 | −33.8 pt |
| fairy-wing | `ffw121e` | **0.0%（0/39）** | **48.7%（19/39）** | `{2:19, 3:4, 4:10, 5:6}` | 1.238 | 1,694 | −80.0 pt |

- `hits ≥ 3` **两套配装都 0 胜**（3 击必死：大师 210 伤害、玩家 480 血）。
- `hits=2` 时的存活时长：强翼 **5,433 tick** 对弱翼 **1,581 tick**（强翼 `hits=2` 胜率 78%、弱翼 0%）。
- 弱翼击杀需 **≈6,607 tick**，而中位场次 **1,694 tick**（最长 4,150 仍不足）→ **3.9× 存活缺口**。
  对照强翼需 ≈6,309 tick，**18 场胜局全部落在 6,222–6,226 tick**，恰好跨过阈值。
- 弱翼不是“挨打更多”：`hits/1kt` 1.238 对 0.504，换算下来**每 1,000 tick 只多挨 0.73 次**。
  **真正的差距是 `hits=2` 时的存活时长。**
- **该量级没有证据支撑的战术改动可以弥合**，所以本轮不提可执行项，收束于此。

### 50.4 被否方案的一句话版

1. **沿冲刺方向逃** —— 维度错了：决定生死的是径向分量 `vrad` 的**数值**
   （`vrad<2` 受击 60.9%、`vrad 2–6` → 1.9%），二元符号 `sign(vx)==sign(bvx)` 无信息。
2. **在慢窗口（泡泡/鲨卷风）里落地回充** —— 46.2% → **0/39**。
   回充与“赚距离”同源：那两个窗口之后的冲刺 onset 中位 **901 px**，纯 hover-charge 链只有 **488 px**。
3. **给垂直轴补接近速度前瞻** —— 46.2% → **2.6%**。
   **向上闪避 = 把高度送给 Boss**：它悬停上限 8.5/10/12，而玩家上升未被激发
   （命中前 8 tick `vy` 恰为重力 +0.40/tick），**爬升比对方下压更慢**。
4. **按 `bs2` 时钟预排克盾** —— **实现前**就被数据否掉。保护带是 `bs2` **24..39**，
   而 **21 次冲刺期命中全在 `bs2=5`**（发盾前 2 tick），且 `_dashIssued` 每次冲刺重置 → **结构上不可覆盖**；
   悬停 76 次命中里 57 次是身体接触、其命中前 8 tick `closing` 全是 0 或负
   —— **不是撞击事件，是持续接触状态**，前瞻无物可预测。
5. **用站位/边界 clamp 让悬停目标点落在接触盒外** —— **机制不存在，且数据方向相反**。
   IL 实读：悬停是“向目标加速 ±`num4`、上限 `num5`”，`noTileCollide = true`，**没有任何边界 clamp**。
   `sign(d0)` 与冲刺 onset 玩家侧别的一致率虽 **86.4%**，但**翻转组命中率 7.9% 反高于 SAME 组 3.5%**，
   “站 Boss 左侧”又是玩家默认（55.5%）且 86.8% 产出最危险入口格 —— **偏好某侧的规则会推向更危险一侧**。

详细机制与全部读数：本文件 § 2 清单与 `artifacts\fishron-wing-round-summary-20260925.md`；
常驻约定同步在 `Chaite\AGENTS.md`。

### 50.5 两条工装纪律（都踩过坑）

1. **空间常量必须先读实测值再推理。** 本轮先推 `clamp 阈值 6000 / band 216..6300`
   再往下推，结果 IL 里根本没有 clamp，而 band 实测是 **650..~7470**
   （玩家中心 x 的 `min = p25 = 650`，**至少 25% 的 tick 被压在左边界 650 上**，仅 **3.48%** 超过 6300）。
   凡以 216/6300 为界的结论已作废（含“左墙侧从不 clamp”、“B 带 137 段 0 命中”）。
2. **观测流的状态字段一律显式 `int()`，状态判定必须带自证断言。**
   `bs` 在部分行里是**字符串**，`'0' in (0,5,10)` 为 `False` → 状态判定**静默全灭**
   （表现为 1862 个冲刺 onset 一个都配不上悬停），为此绕了 5 轮错路。
   正确做法：配对后先断言 `malformed pairs = 0`，并打印首对的 `gap`
   （阶段 1 应为 **28 tick**）；后向配对（从 hover 入口回找 charge）比正向扫描稳健。

### 50.6 `Program.cs` 事故的重建口径（不可逆计数不确定性）

一个工作流用 `Get-Content -replace | Set-Content` 修改 `tests\Chaite.Tests\Program.cs`
—— 该文件**非 ASCII**（违反铁律）且**不在其白名单内**（违反边界），
结果吃掉约 **65 行**、**3 个测试静默消失**；随后它写的“自动删掉编译不过的方法”循环
开始**整段删除方法体**，滚到 64 个合成错误才停手。

**重建口径**：从 `git HEAD`（**清理前的旧版**）重建 → 去残（Chillet / Empress / 武器交接引用）
→ 补回环境隔离块（`ExportedPolicy.FormatVariable` / `LearnedPolicy.FileVariable` /
`LearnedPolicy.RoutesVariable` 置 `null`）→ 重新注册 4 条测试
（`FishronWingRetreatsFromAClosingBody`、`FishronWingDoesNotRiseFromADivingBody`、
`FishronHoverReplayReadsTheRequestedInput`、`FishronWingNeverRechargesThroughACharge`）。

**现状**：构建 0 错误、`Chaite.Tests` **752 通过 / 0 失败 / exit 0**。
**该文件长期为 `M`、HEAD 是旧版，所以无法证明工作树原版是 752**
（此前为 754，差的 2 条是过期的 Empress / `CombatWeaponSelectionHandoff` 注册）。
**这是不可逆的计数不确定性，不要声称“752 就是原值”。**
缓解：当前版本另存于 `artifacts\_programcs-baseline-20260925\`
（`Program.git-HEAD.cs`、`Program.rebuilt-20260925.cs`、`Chaite.Tests.rebuilt-20260925.exe`）。
