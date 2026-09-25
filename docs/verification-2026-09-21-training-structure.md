# 2026-09-21 训练结构修正：实测、修改与 in-engine 验证

本文记录本轮对训练环境的五项结构修正。每一项都给出**修改前的实测数字**、
**改了什么**、以及**在引擎内证明改动真的执行了**的证据。没有任何一条以
"代码里写了" 作为证据。

被替换的配置与全部旧结果见 `status-2026-09-21.md` 与
`loadout-and-simulation-audit.md`。

---

## 0. 为什么必须动结构：旧配置的实测结论

上一代训练一共约 **8×10⁷ 步 / 约 3.47 万场真实战斗**，产出**2 次无伤胜**
（都在 obsb1：episode 12 @4889 tick、episode 77 @4341 tick，`SuccessNoDeath`，
`bossDamage` 77980/77983）。

获胜时的受击次数分布（obsb1）：

```
[(0,2),(1,7),(2,14),(3,31),(4,38),(5,58),(6,56),(7,45),(8,45),(9,31),(10,22),(11,7),(12,2)]
```

众数是 **5**，而且**从来没有从 5 走到 0**。所以问题不是"训练不够"，
而是环境里存在让策略**无法**走到 0 的障碍。本轮找到并修掉了三个。

---

## 1. 威胁盲区：34.5% 的受击来自策略看不见的东西

### 实测

对 `artifacts/bridge-obsb1/bridge.obs.jsonl`（1,207,918 行）逐帧比对
"掉血帧"与同帧的弹幕窗口、以及**截断前**的真实敌对弹幕数 `pc`：

| 项 | 数字 |
|---|---:|
| 掉血帧总数 | 2757 |
| 其中窗口为空 **且 `pc == 0`** | **951（34.5%）** |
| 其中窗口为空但 `pc > 0` | **0** |
| 满 12 槽的掉血帧 | 1159 |
| 12 槽里 ty=384（鲨鱼龙卷风柱）占比 | 78.8% |

`pc == 0` 是决定性的：它由探针在**截断之前**对全部敌对射弹统计，所以
"窗口空 + pc==0" 意味着**那一帧根本没有任何敌对射弹**。而场上除了猪鲨
本体与它的衍生 NPC 之外没有别的敌怪，因此这 951 帧的伤害**只能来自 NPC 类
威胁**——即 Detonating Bubble（371）与 Sharknado 生成泡（372/373）。

复现脚本：`artifacts/_hit-by-type.py`。

### 结论

旧观测里**没有任何 NPC 类威胁特征**，所以策略对猪鲨的主要威胁是盲的。
它不可能学会躲开看不见的东西——这正是"卡在 5 次受击"的第一个原因。

### 修改

观测新增 12 维 NPC 威胁块（`nt2/nt4/nt8` 计数 + 最近威胁的相对位置、速度、
距离、类型、血量、尺寸、存在位），三侧同步：

- 探针 `tools/GameProbe.cs`：`WriteBridgeObservation` 里扫描非友好/非城镇/非
  小动物 NPC，写 `nt2/nt4/nt8/nrx/nry/nrvx/nrvy/nrt/nrl/nrw/nrh`
- 训练 `training/chaite_env.py`：`obs_vector` 追加同一块
- C# `src/Chaite.Core/ChaiteObservation.cs`：`NpcThreatFeatureCount = 12`

### in-engine 验证

`artifacts/verify-2026-09-21-new-observation-keys.txt`：

```
nrt (nearest NPC type)  : LIVE  {371: 2140}
nt4 (count within 400)  : 0 x125994, 1 x2140
```

`nrt` 只有一个取值 371 是**正确**的：本次运行的场景里唯一会出现的 NPC 类威胁
就是 Detonating Bubble。它非零 2140 行，证明扫描分支确实执行了；如果分支没执行，
计数会恒为 0，而 0 与"没有威胁"无法区分。

---

## 2. 弹幕窗口：40% 的行被截断，而槽位被无害柱状体占满

### 实测

| 项 | obsb1 | train19 |
|---|---:|---:|
| `pc` 最大值 | 98 | 84 |
| 行被截断比例 | 40.55% | 40.77% |
| 12 槽全被 384/385/386 占满的行 | 90.13% | 90.98% |
| ty=384 的槽位出现次数 | 2,480,000+ | — |

按曼哈顿距离排序时，鲨鱼龙卷风柱（体量大、移动慢、数量多）会把 12 个槽位
全部占掉，而真正会命中的快速接近弹（720、204/205、43、201/202/203）被挤出窗口。

### 修改（两个开关，默认关闭 = 旧行为）

- `CHAITE_PROJ_SORT=threat`：按**预计接触时间**排序，而不是原始距离。
  `score = gap/max(closing, 8) + 0.25*gap/8`，`gap = |相对位置| - 半个较大边长`。
- `CHAITE_PROJ_COLLAPSE=1`：同类型只保留最近的一个，被折叠掉的数量写入 `pe`，
  所以"省略"在数据流里是可见的，不再像旧的 12 槽上限那样无声。
- 两个开关都只改**顺序与成员**，不改窗口宽度，因此不可能错位特征向量。

### in-engine 验证

`artifacts/verify-2026-09-21-new-observation-keys.txt`：

```
ps (ordering in force)  : {'threat': 128134}      <- 排序开关生效
pe (types collapsed out): 0 x126522, 1 x1586, 2 x26   <- 折叠生效且确实折叠了东西
rows where pc > window  : 0/128134 = 0.0% 截断
slot type histogram     : ty=720 x11754, 205 x5591, 204 x4468, 203 x4168,
                          43 x3411, 202 x2642, 201 x1426
```

**对比**：改之前 90% 的槽位是柱状体、40% 的行被截断；改之后**截断率 0%**，
槽位里出现的全是猪鲨本体那些真正会命中的弹（720/201-205/43）。柱状体
（384/385/386）**一个都没有了**——因为每类只留一个，而它们本来就被排到了后面。

---

## 3. 下键：动作空间根本表达不出下降

### 根因

`src/Chaite.Plugin/Runtime.cs` 的桥接动作路径里，`plan.Drop` 被**硬编码为
`false`**。整条链路（RouteReplay → Runtime → ControlPlan.Drop →
TerrariaFacade.ApplyPlan → `player.controlDown`）下面每一层都是对的，所以
没有任何单元测试能发现它——这正是它存活至今的原因。

动作空间是 `3 方向 × up × down × dash = 24`，但旧的动作文件格式只有
`tick,direction,jump,dash` 四列，**没有 down 列**。

### 修改

- 动作文件改为五列 `tick,direction,up,down,dash`；四列旧格式仍然可读，
  缺失的两位按 `false` 处理，语义不变。
- `RouteReplay` 增加 `up`/`down` 两个位；`Runtime.cs` 里
  `plan.Drop = replayDown;`、`plan.FeatherFallUp = replayUp;`。
- `up` 位同时驱动跳跃通道（`_bridgeJump = _bridgeUp`）：Terraria 里 up 键与
  跳跃键都走 `controlJump`，否则翅膀与坐骑会完全失去上升输入。

### in-engine 验证（A/B，唯一变量是那一列）

`artifacts/verify-2026-09-21-down-key-in-engine.txt`。两臂场景、路线、种子、
难度、探针构建完全相同，唯一差别是动作文件 `999999999,0,0,<down>,0`：

| 臂 | 动作文件 | `player.controlDown` |
|---|---|---|
| dn0 | `999999999,0,0,0,0` | False ×2048 |
| dn1 | `999999999,0,0,1,0` | False ×443, **True ×1605** |

`RouteReplay` 自己的 trace 也记录了两臂接受的位：

```
dn0: 0 accepted tick=999999999 dir=0 jump=False up=False down=False dash=False
dn1: 0 accepted tick=999999999 dir=0 jump=False up=False down=True  dash=False
```

**上升位同样验证**（`up=1` 臂）：`controlJump` True ×1698，玩家高度从
y=7958（地面）升到 y=4945，即真的飞起来了。若某一层把它丢掉，两臂会得到
相同的分布。

---

## 4. BOSS 攻击时钟：策略看不到"现在打到哪一步"

### 为什么这是缺口

被审阅的公式脚本读的是 `ai[2]`（计时器）与 `ai[3]`（序号），不是 `ai[1]`：

`FormulaScriptController.TryReadInput`：

```csharp
var timer    = target.Type == 370 ? target.Ai2 : target.Ai1;
var sequence = target.Type == 370 ? target.Ai3 : target.Ai2;
```

而 `FishronFormulaStateContract.TimerLimit(state, sequence, expert, enraged)`
用这两个值算出**当前状态在第几 tick 结束**；`sequence` 决定**下一个攻击是什么**
（state 2 吐 20 只 Detonating Bubble、state 3 投鲨鱼龙卷风弹、state 7 绕圈、
state 8 投 Cthulhunado）。

桥接观测**只有 `bs`(ai[0]) 与 `bi`(ai[1])**，所以策略读不到计时器与序号，
只能从经过时间猜攻击进度——而躲闪恰恰需要这个信息。

### 修改

桥接行新增 `bs2`(ai[2]) 与 `bs3`(ai[3])，无 BOSS 时写 `-1`（与 `bs` 一致）。
三侧同步追加 2 维：

```python
features.append(_clip((float(row.get("bs2", -1.0)) + 1.0) / 181.0))
features.append(_clip((float(row.get("bs3", -1.0)) + 1.0) / 16.0))
```

**为什么是 `(value+1)/divisor` 而不是 `value/divisor`**：探针用 `-1` 表示
"没有 BOSS"。直接除会把 `-1` 映射到 0，而计时器真的是 0 的 BOSS 也产生 0——
"没有 BOSS" 与 "攻击刚开始" 需要完全相反的行为，策略却分不出来。加 1 之后
哨兵值落在 0，真实值落在 `1/divisor..1`。

### in-engine 验证

`artifacts/verify-2026-09-21-new-observation-keys.txt`：

```
bs2 (boss ai[2] timer)   : 81 个不同取值, 最高 -1.0 x48338, 0.0 x10679, ...
bs3 (boss ai[3] sequence): 11 个不同取值, -1.0 x48338, 1.0 x14766, 0.0 x14124,
                            5.0 x10757, 3.0 x6356, 2.0 x6206
```

`bs3` 的 11 个取值（0..10）与 `FishronFormulaStateContract` 里 state 0 允许的
`sequence <= 11` 完全对上；`bs2` 的 81 个取值覆盖了计时器范围。

---

## 5. 场地：15% 的时间在场地外，3.2% 的帧在深坠落

### 实测（obsb1，1,207,918 行）

| 项 | 数字 |
|---|---:|
| 玩家 x 范围 | 640 .. 14751 px（882 格） |
| 已建地面范围 | x 16 .. 6400 px（399 格，即 tile 1..399） |
| x ≤ 6400（海洋带内） | 85.01% |
| **x > 6400（跑到海滩上）** | **14.99%** |
| 低于地面线 y=8000 | 6.91% |
| **低于地面线 2000 px 以上** | **3.23%** |
| 低于地面线的最深处 | 地面线以下约 9.5k px |

`WorldGen.clearWorld()` 之后**只有手搭的竞技场有地面**，所以 x > 6400 的
地方是**空的**：玩家飞出去就会掉进虚空。按内外拆分：

| | 行数 | 地面线以下 | 其中 >2000 px 深 |
|---|---:|---:|---:|
| 场地内（x ≤ 6400） | 1,026,852 | 33,435 | ~4,700 |
| 场地外（x > 6400） | 181,066 | 50,089 | ~34,000 |

两次无伤胜都在 x ≤ 8811（511 格）内，最大只落到地面线以下 221/518 px，
也就是说**深坠落全部发生在跑出场地之后**。

### 修改

把海洋场景的地面**多修到 tile 1200**（`SafetyFloorRightExclusive = 1200`，
实测最大 x 为 922 格，留有余量）。

这是一块**安全地板，不是把竞技场变大**：`ArenaGroundLeft`（1）、
`ArenaGroundRightExclusive`（400）、出生点（tile 21）、enrage 带、以及
`result.json` 里公布的竞技场边界**全部没有动**。它只保证"跑出海洋带"
这件事的代价是**位置**而不是**命**——真实海岸线也是这样，海滩不是陷阱。

`result.json` 的 arena 块新增 `safetyFloorRightExclusive` 一起公布，避免
"公布的范围"与"实际修了砖的范围"再次不一致（历史上平台行就是这样漂移的）。

### in-engine 验证

用 `999999999,1,1,0,0`（持续向右 + 上升）把玩家推到场地外：

| | 修改前 obsb1 | 修改后 bridge-floor1 |
|---|---:|---:|
| 行数 | 1,207,918 | 237,101 |
| x > 6400 的行 | 14.99% | **60.1%** |
| 低于地面线 | 6.91% | **0.00%** |
| 低于地面线 2000 px 以上 | 3.23% | **0.00%** |
| 玩家 y 最大值 | 17534 | **7958（正好是地面）** |

60% 的帧都在场地外、而**一帧都没有掉下去**：地板确实在那里。

证据：`artifacts/verify-2026-09-21-arena-safety-floor.txt`。

---

## 5b. 多段跳充能：审计点名的最后一个不可观测资源

### 缺口

`docs/loadout-and-simulation-audit.md` §5 原文：`PlayerForwardModel.cs` L31-34
"The recorded observation does not carry the multi-jump charge state"。
莉莉丝狼是**唯一**带气球束（1164）的配装，它的核心机动就是多段跳，所以
"还剩几次"直接决定"要不要花掉一次"。

`jt`（`p.jump`）**不携带**充能次数。`current-focus.md` C122 的实测：
按住跳跃 `jt` 0–5、`wingTime` 不动；轻按 `jt` 11–18、`wingTime` 被消耗。
两者能靠**后果**区分，但次数读不出来。

### 修改

从程序集里取出 `Player` 的九个充能字段（`canJumpAgain_Cloud/_Sandstorm/
_Blizzard/_Fart/_Sail/_Basilisk/_Santank/_Unicorn/_WallOfFleshGoat`），
写为 `jc0..jc8`，三侧同步追加 9 维。

### in-engine 验证，以及一个改变设计的发现

`artifacts/verify-2026-09-21-jump-charges.txt`。在 `fishron-lilith-wolf`、
种子 2、接管 120 的记录流上（254,957 行，全程装备气球束）：

```
jc0 (cloud)      True x254957
jc1 (sandstorm)  True x254957
jc2 (blizzard)   True x254957
jc3..jc8         False x254957（各自）
```

**气球束给的是三种充能，不是一种。** 一个"求和成一个计数"的设计会报 3，
而策略仍然不知道它将要花掉的是哪一种跳跃——这恰恰是这个特征存在的目的。
这就是"九个布尔而不是一个计数"的**实测**理由，不是"以后可能有别的配装"的猜测。

---

## 6. 奖励：5 次受击与 4 次受击在数值上无法区分

`HURT_FACTOR` 默认 400，即一次受击值 `400 × 掉血 / 血量上限 × 1e-3`。
按实测每次掉血 64–96、血量约 500 计算，一次受击约 **0.4**，而一次胜利值
**600**、无伤额外 **+400**。所以：

- 5 次受击的胜利 ≈ 598.0
- 4 次受击的胜利 ≈ 598.4

**两者差 0.4，等于没有差别。** 策略只能靠"撞到 +400 的无伤大奖"这一条稀疏
路径走到 0，而没有任何密集梯度把它从 5 拉向 4、3、2、1。这与观测到的平台期
形状完全一致。

修改：默认提到 **40000**（一次受击约 5.1–7.7），使 5→0 这段带约 30 点梯度，
同时 15 次受击的胜利仍然低于一次无伤胜利。`training/chaite_env.py` 与
`training/run-session.ps1` 的默认值都改了，并保留环境变量覆盖以便做配对对照。

---

## 6b. 世界坐标裁剪：已有特征被裁成了常数

这一条不是"缺特征"，而是**已有特征在旧默认值下退化成常数**，所以比缺特征更难发现：
宽度对、名字对、conformance 全绿，而策略读到的那个数是常量。

`_clip_world` 先除以 1000 再裁剪，所以默认的 bound 4.0 是 **4000 px** 的上限，
而场地是 4200×1200 格。在 `artifacts/bridge-obsb1/bridge.obs.jsonl`
（1,207,918 行）上逐特征统计被钉死的比例：

| 特征 | bound 4.0 | bound 12.0 | bound 18.0 |
|---|---:|---:|---:|
| `px` 玩家 x | 47.76% | 0.61% | **0.00%** |
| `py` 玩家 y | **100.00%** | 1.58% | **0.00%** |
| `bx` BOSS x | 37.71% | 0.33% | **0.00%** |
| `by` BOSS y | 85.61% | 0.85% | **0.00%** |
| `px-bx` | 8.44% | 0.25% | **0.00%** |
| `py-by` | 14.39% | 0.56% | **0.00%** |
| `dist/1500` | 14.19% | 0.18% | **0.00%** |

**在旧默认值下，玩家的竖直位置在每一帧上都是同一个常数**，BOSS 的竖直位置在
85.6% 的帧上是常数。策略无法表达"我在场地的哪里"。

与上一代结果一致：7 条主会话用 4.0，胜率 0.16%–2.50%；两条用 12.0 的对照臂
`obsb1`/`obsb2` 是 3.79%/5.04%，且**仅有的两次无伤胜都在 obsb1**。配装不同，
所以不能把差异全部归给 bound，但方向与机制相符。

修改：本战役用 **18.0**——实测**最小**的、七个特征全部零饱和的界。12.0 仍有
1.8% 的行有特征被裁；18.0 一个都没有，所以这个裁剪在 18.0 下是**诚实地失效**
（不再改变任何观测），而不是"看起来放宽了但仍在悄悄丢信息"。20.0 同样失效且
没有更好。

只有那 7 个世界特征消费这个常数（其余基础特征走 `_clip(4.0)`，参数已各自除以
自己的量纲除数），所以抬高它不可能移动别的特征。这一点由 conformance 夹具在
bound 18.0 下**重生成并通过**确认：200 行真实数据，max |C#-Py| = 9.5e-7，
C# 侧读同一个 `CHAITE_OBS_WORLD_BOUND`（`ChaiteObservation.DefaultWorldBound`
= 4f，可由环境变量覆盖），所以两侧不可能不一致。

---

## 7b. 启动战役时发现的两个热启动 bug（都已修）

这两个都不是"缺特征"，而是**工具链的静默失效**，而且它们只在真的去跑热启动
的时候才暴露出来——所以它们是被战役本身发现的，不是被审阅发现的。

### bug 1：SB3 的加载器**不检查动作空间**

`PPO.load(path, env=env)` 只比较**观测空间**。实测：一个 **121 维观测、12 个动作**
的检查点被干净地加载进这个 **24 个动作**的环境，然后整个回合**没有产出任何训练
输出**——策略头是按 12 个动作建的，而环境从 24 个动作里采样，进程在没有
flush 任何一行的情况下死掉。

**为什么这是最坏的一种**：会话日志看起来是活的，因为游戏一直在推观测流。
`session-fsw121w.log` 有正常的 `round 1/12 launching ...`，`bridge.obs.jsonl`
长到 59 MB，而 `ppo.log` 只有 94 字节。

修复：`train.py` 在 `PPO.load` **之前**从检查点自己的 `data` 里读出动作数并比对。
SB3 v2.9 把 `data` 写成 JSON、把空间 base64-pickle 在里面，所以读法是
`json.loads(zip.read("data"))` 再 `pickle.loads(base64.b64decode(...))`。
不匹配就带着自己的句子拒绝，而不是在 rollout 里变成形状错误。

**验证**：故意用一个 121 维 / 12 动作的检查点去 resume，得到

```
checkpoint artifacts\_guardtest\ppo_latest was trained over 12 actions but the
environment has 24; refuse to resume. SB3's own loader does NOT check this --
it compares observation spaces only -- so without this line the round dies
silently. Widen the checkpoint with training/warm_start_action.py, or start
this arm from scratch.
```

### bug 2：热启动写出的检查点**观测空间边界与环境不一致**

`warm_start_obs.build_target` 用 gymnasium 的默认 `Box(-inf, inf)` 建目标环境，
而 `chaite_env` 声明的是 `Box(-4.0, 4.0)`。SB3 的 `check_for_correct_spaces`
**会**比较边界，所以热启动写出的检查点**根本无法被 `train.py` 加载**：

```
ValueError: Observation spaces do not match:
  Box(-inf, inf, (121,), float32) != Box(-4.0, 4.0, (121,), float32)
```

这条与 bug 1 方向相反，也解释了为什么 bug 1 之前没有被发现：SB3 检查的那一半
先炸了，没检查的那一半就没机会暴露。

修复：`chaite_env` 把边界命名成 `OBS_BOX_LOW`/`OBS_BOX_HIGH` 并说明它们是
**声明而不是钳制**（世界特征故意超过 4.0，这正是 `OBS_WORLD_BOUND` 的用途），
`warm_start_obs` 从该模块取值而不是硬编码。

**验证**：重建后的检查点 `obs=Box(-4.0, 4.0, (121,)) actions=24 steps=8242173`，
并且用真实的 `ChaiteBridgeEnv` 加载成功（`LOAD OK: obs=121 actions=24 steps=8242173`）。

### 两个 widen 的次序与验证

```
98 维 / 12 动作
  --warm_start_obs-->    121 维 / 12 动作   98 列逐位不变（max diff 0.000e+00）
                                            200/200 行动作完全一致，logit 差 9.5e-7
  --warm_start_action--> 121 维 / 24 动作   12 个原动作 logit 逐位不变（0.000e+00）
                                            12 个新 DOWN 动作 logit 全 0
                                            200/200 行在 24 路 argmax 下仍是原动作
```

即：热启动后的策略**行为与旧的完全一样**，下键是"接好了但还没被使用"，
训练才有机会去用它——这是热启动应有的起点。

---

## 8. 本轮的验证清单

| 改动 | 不可为 no-op 的引擎内对照 | 结果 |
|---|---|---|
| NPC 威胁块 | 流里出现 `nrt=371` 非零行 | 2140 行，LIVE |
| 弹幕排序 | 流里 `ps` 字段 | 全部 `threat` |
| 同类型折叠 | 流里 `pe` 字段 | 0/1/2 三种取值 |
| 下键 | 只改第 4 列的 A/B | dn0 全 False / dn1 1605 True |
| 上升位 | 只改第 3 列的 A/B | 高度 7958 → 4945 |
| BOSS 时钟 | `bs2`/`bs3` 取值分布 | 81 / 11 个取值 |
| 多段跳充能 | `jc0`..`jc8` 取值分布 | jc0/jc1/jc2 全 True，jc3–jc8 全 False |
| 安全地板 | 推到场地外后是否还掉 | 60% 在场外，0% 坠落 |
| 世界坐标裁剪 | 逐特征饱和比例 | 4.0 → py 100%；18.0 → 全部 0.00% |
| 观测布局 | conformance 夹具（121 维，bound 18） | 794 测试全通过，max |C#-Py| = 9.5e-7 |

### 观测维度变化

```
旧: 10 + 12 + PROJECTILE_SLOTS*6 + 4            = 98  (12 槽)
                                                  + 12 NPC 威胁
                                                  +  2 BOSS 攻击时钟
                                                  +  9 多段跳充能
新: 10 + 12 + PROJECTILE_SLOTS*6 + 4 + 12 + 2 + 9 = 121 (12 槽)
```

三个新块都是**追加**在尾部，所以旧检查点训练过的那 98 个索引含义不变；
但宽度变了，所以**所有旧检查点都失效**，必须重训。这一点由
`ExportedPolicyDriverRefusesAMismatchedWindow` 明确拦住（它拒绝把 121 维喂给
读 98 维的第一层），而不是静默出错。

---

## 8. 尚未完成 / 下一步

**本轮已全部落地并启动**：7 条会话正在跑（6 配装 + 1 热启动对照），
权威清单见 `campaign-2026-09-21.md`。剩下的都是**读数**而不是改动：

1. **阶段 1 核对（已做）**：七条的 `OBS_DIM` 全为 121、bound 全为 18，
   `fsw121w` 打印 `resumed checkpoint already at 8242173 steps`；
   实跑流里 `jc*`/`nt*`/`bs2`/`bs3`/`ps`/`pe` 出现率 **100%** 且取值非平凡。
2. **阶段 2**：受击次数分布是否开始向下走（上一代众数 5，从未到 0）。
3. **阶段 3**：无伤率。
4. **配对读数**：`fsw121` 对 `fsw121w`，判断热启动是加速手段还是有害先验。
5. 清泡武器仍未接入战斗热路径（见 `fishron-threat-identities.md` 末节）；
   模拟输出仍以 `DetonatingBubbleBreakChance = 0.35` 每 tick 的概率模拟泡泡被
   打破，所以策略不需要自己开火。

**归因限制**：本轮同时上线 8 处改动，任何单一结果都不能归给其中某一处。
可分离的只有 `fsw121` vs `fsw121w` 这一对。若结果仍差，下一轮应当用
`CHAITE_PROJ_SORT=manhattan`、`CHAITE_OBS_AGG` 等"旧行为"开关做逐项回退对照，
而不是再叠新改动。
