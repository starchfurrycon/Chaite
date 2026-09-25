# 当前重点（实时更新，跨上下文丢失）

> 本文件是**活文档**：每次发现新的铁律、缺口或踩过的坑，立刻写进来。
> 目的只有一个——上下文很长时，重点不能被忘掉。

最后更新：2026-09-23 · C125：疾旋鼬路线（坐骑 64/65）整体删除（C124 的 4 套配装现为 3 套）

---

## C125. **疾旋鼬路线（`fishron-trusty-chillet` / `-ignis`，坐骑 64/65）整体删除**

用户 2026-09-23 定案原话："疾旋鼬坐骑路线机动性确实不足，直接删去吧"。

**实测依据**（三层平台竞技场、大师难度、挂载彩排）：疾旋鼬只能爬升 **38 格**，
莉莉丝狼能爬 **70 格**，而平台行距是 **60 格** ⇒ 疾旋鼬连第一层平台都上不去，
两层平台上的占用率因此是 0.0%，胜率也停在 0%（`fch121`）。

**删除范围**：`FormulaRoute` 的两个枚举成员、`FormulaRouteCatalog` 的
`IsAcceptableMount` / `BelongsToBoss` / `Select` 准入、`FormulaMobilityContract.ExpectedMount`、
`CombatPlanner` 的脚本分支与两个脚本字段、`FishronChilletScript.cs`（整文件删除）、
插件 `TerrariaFacade` 里坐骑冲刺的待校验分支及其 `_validatePendingFormulaMountDash` /
`_pendingFormulaMountType` 字段、`GameProbe.cs` 的白名单与 `early-hardmode` /
`post-plantera` 两个配装分支、`run-boss-validation.ps1` 与 `start-isolated-test.ps1`
的路线门禁、四个测试文件里只钉该路线的用例、以及 `docs/` 里陈述路线集合的段落。
坐骑 64/65 现在选中 `None`，即该配装被**拒绝**而不是被驱动。

**保留不动**：`VanillaMountCatalog` 的坐骑 64/65 条目（游戏数据）、
`MobilitySnapshot` 的坐骑字段（121 维观测布局是位置式的，动字段会静默移位）、
`SpriteCatalog` 的疾旋鼬贴图（前端素材，取自本体游戏）。

**对旧结论的影响**：C120 第 1 条与 C122 测的是"疾旋鼬坐骑冲刺能否被动作空间表达"，
该路线既已删除，这两节的结论不再有行动价值；按当时事实保留，只是不必再围绕它
扩展动作空间。

---

## C124. **★★★ 验收口径变更：只针对猪鲨 4 套配装，看胜率 ≥ 90%，无伤率不再考核**

用户 2026-09-23 原话："现在程序只针对猪鲨！不再接管与准入光女……你只需要将猪鲨的四套
配装都训练到 90 胜率即可验收并挂载在程序策略中，并且注意更改程序前端与逻辑，以及清理
历史遗留，不再负责光女相关部分。"

**本文件里一切把"无伤"当作当前验收目标的表述（C115、C116 等）都已作废**，只作为历史
记录保留。当前口径：

| 配装 | 训练臂 | 要求 |
|---|---|---|
| 猪鲨 strong-wing | `fsw121d` / `fsw121p` / `fsw121q`（三选一） | **胜率 ≥ 90%** |
| 猪鲨 fairy-wing | `ffw121` | **胜率 ≥ 90%** |
| 猪鲨 trusty-chillet | ~~`fch121`~~ | ~~**胜率 ≥ 90%**~~ **路线已删除（见 C125）** |
| 猪鲨 lilith-wolf | `flw121` | **胜率 ≥ 90%** |

- 达标后**挂载进程序本体**的接管后战斗策略，并用挂载彩排验证（判据仍是
  `training/mount-policy.ps1 -Tag <tag>`，走生产代码路径）。
- 光女（NPC 636）退出范围：不再接管、不再准入、不再负责；前端与逻辑里的相关部分已清理。
- **不接管武器/攻击**，只接管走位。

> **测量组已换代**：C123 的"121 维重训战役"已演进为**新测量组**
> （`InputCoreSha256 = E9A4AD9A…FD6D`，竞技场加入两层水平平台，共三层地面）。
> 六个臂从各自 checkpoint **顺承续训、未重置权重**。旧的平场地测量组数据
> （归档在 `artifacts/session-<tag>-group1-flatarena.log`）**不得与新组混用**。

---

## C123. **★★★ 训练结构八处修正（均已引擎内验证）· 121 维重训战役进行中**

上一代 8×10⁷ 步 / 3.47 万场真实战斗只产出 **2 次无伤胜**，受击众数是 5 且
**从未走到 0**。本轮找到并修掉八处让策略**不可能**走到 0 的结构缺陷，
每一项都有"改动前实测数字 + 引擎内不可为 no-op 的对照"。

完整实测与证据：`docs/verification-2026-09-21-training-structure.md`；
战役清单：`docs/campaign-2026-09-21.md`。

| # | 缺陷 | 实测 | 修正 |
|---|---|---|---|
| 1 | **NPC 类威胁完全不可见** | obsb1 的 2757 个掉血帧里 **951 帧（34.5%）`pc==0`**，即那帧没有任何敌对射弹，伤害只能来自 NPC | 观测 +12 维（`nt2/nt4/nt8` + 最近威胁几何） |
| 2 | **弹幕窗口被无害柱状体占满** | **40.55%** 的行被截断；**90.13%** 的行 12 槽全是 384/385/386 | `CHAITE_PROJ_SORT=threat` + `CHAITE_PROJ_COLLAPSE`，截断 → **0.00%** |
| 3 | **动作空间表达不出下键** | `Runtime.cs` 把 `plan.Drop` **硬编码 false**，下面每层都对所以没有测试能发现 | 五列动作文件 + `plan.Drop = replayDown` |
| 4 | **BOSS 攻击时钟不可见** | 公式脚本读的是 `ai[2]`/`ai[3]`，桥接只给 `ai[0]`/`ai[1]` | 观测 +2 维 `bs2`/`bs3` |
| 5 | **多段跳充能不可见** | 审计点名的最后一个缺口；`jt` 不携带次数 | 观测 +9 维 `jc0..jc8` |
| 6 | **场地外是虚空** | **14.99%** 的帧在海洋带外，**3.23%** 的帧深坠落 | 安全地板（不改竞技场边界）→ **0.00%** |
| 7 | **一次受击只值 32** | 5 次受击胜 ≈598.0、4 次 ≈598.4，**差 0.4 等于没差** | 一度改为 40000，**实测证明是错的**（见下），已回到 400 |
| 8 | **世界坐标被裁成常数** | bound 4.0 下 **`py` 在 100% 的帧上是常数**、`by` 85.6% | bound → **18.0**（七个世界特征 0% 饱和） |

**第 5 条的适用范围（2026-09-24 补记，不改上表口径）**：`jc0..jc8` 这 9 维**不是**地形/着陆
信息，而是多段跳充能；且它们对验收范围内的两套配装（`strong-wing` / `fairy-wing`）**恒为 0、
不携带信息**——气球束（1164）只有已取消的 lilith-wolf 路线携带；`ChaiteObservationRow.JumpCharges`
在 `src\` 里**从未被赋值**（只有声明、计数常量、名字表与两处输出循环）。曾经把 9 维换成
"着陆图"（地形/平台信息）的实验在 Master 难度、走生产挂载路径下**失败**（强翼 82.1%（32/39）
→ 0.0%（0/38），弱翼 9.7% → 0.0%），已回滚。所以这 9 维的**语义不需要再改**。

第 8 条最难发现：宽度对、名字对、conformance 全绿，而策略读到的那个数是常量。

**维度**：98 → **121**（+12 NPC +2 BOSS 时钟 +9 多段跳充能，全部追加在尾部，
所以旧 98 个索引含义不变）。宽度变了，所以 `ExportedPolicyDriverRefusesAMismatchedWindow`
会主动拒绝旧导出——这是"必须重训"的机器判据，不是静默出错。

**验证手段（都不是"代码里写了"）**：下键/上键的**唯一变量 A/B**（只改动作文件
一列）、`nrt` 非零行数、`ps`/`pe` 字段、`bs2`/`bs3` 取值分布、`jc*` 取值分布、
推到场地外后是否还掉、以及 bound 18 下重生成的 conformance 夹具。
794 个测试全通过。

**战役中途作废**：前七条会话用 `HURT_FACTOR=40000` 启动，那是我的**算术错误**
（受击项加在 `REWARD_SCALE` 之前，一次受击在 400 时值 32 分、在 40000 时值
3200 分，而胜利只值 600 分）。从 4000 起**死掉比赢下来更划算**，策略于是正确地
学会了速死：全新会话 2460+ 局零胜、受击众数 9–10，而热启动那条在**完全相同的
观测**下赢 21/284。修正为 `HURT_FACTOR=400` + `NO_HIT_BONUS` 400000→1200000
（无伤彩金 3 倍，这才是"无伤"该用的杠杆），见
`reward-calibration-error-2026-09-21.md`。

**★ 更根本的一条：上一代从来没有在训练闪避。** 探针按模拟 DPS 自己打死 BOSS，
策略只负责活着，而受击与存活时间**正相关**。实测 `obsb1` 的胜率从 2.32% 涨到
32.05% 的同时，**每场受击从 5.61 涨到 6.69**——全程 1.92 次受击/1000 tick，
约每 520 tick 挨一下，就是站着不动。**1,175 场"胜"里没有一场来自闪避**，那
358 场胜只是存活时长的副产品，2 次无伤是分布的尾巴而不是技能的结果。
修正：新增 `CHAITE_DODGE_OBJECTIVE`——胜利从 600 降成结束条件 6，每次受击
变成 160，无伤彩金提到 40（**必须大于一次受击的 160**，否则停在 1 次受击的
策略没有动力去够 0）。见 `objective-mismatch-2026-09-21.md`。

**计数修正**：`bridge.episodes.jsonl` 有 72.5% 是墙钟耗尽后的填充行
（`ticks==1`、零伤害），上一代真实战斗是 **34,692 场 / 1,175 胜（3.39%）/
2 次无伤**，不是"约 79,000 局 / 1.5%"。见 `episode-count-error-2026-09-21.md`。

**当前能出结论的 A/B**（全部 `fishron-strong-wing`）：
`obsb1`（旧观测+旧奖励，基线）→ `fsw121c`（**新观测+旧奖励**，分离观测修正）
→ `fsw121r`（新观测+放大彩金）→ **`fsw121d`（新观测+闪避目标）**；
`fsw121w` 是热启动。**判据：`fsw121d` 的每场受击应下降，其余三条应持平。**
若 `fsw121d` 也不降，瓶颈就在动作空间表达力或观测，不在奖励。
`train.py` 现在把奖励常量打进每条会话日志。

**启动时被战役本身抓出的两个工具链 bug**（都已修并验证）：
① SB3 的 `PPO.load` **不检查动作空间**——一个 12 动作的检查点被干净地加载进
24 动作的环境，然后整回合不产出任何训练输出，而会话日志看起来是活的
（游戏仍在推观测流）；② 热启动写出的检查点用 `Box(-inf, inf)`，而环境声明
`Box(-4.0, 4.0)`，SB3 **会**比较边界，所以那种检查点根本无法被加载。
详见 `verification-2026-09-21-training-structure.md` §7b。

---

## C122. **更正 C120 第 1 条**：疾旋鼬坐骑冲刺**可达**，需要"按下—松开"边沿

> **2026-09-23 补注：疾旋鼬路线（坐骑 64/65）已整体删除，见 C125。本节只作历史记录，
> 不再有行动价值。**

C120 写"坐骑冲刺无法触发"是**错的** ✗。错因：我把 `dash` 位**一直按住**，而原生判据要求
`releaseDash == true` 与 `controlDash == true` 同时成立 —— 该判据就写在插件里
（`src/Chaite.Plugin/TerrariaFacade.cs:3879-3893`，`_validatePendingFormulaMountDash`）：

```
dashType == 6 && dashDelay == 0 && releaseDash && controlDash
&& !CC && !pulley && grapCount == 0 && !tongued && gravDir == 1
```

逐 tick 写动作的 A/B（`fishron-trusty-chillet`，mount 64 全程激活）：

| 动作 | \|vx\| 峰值 | `dashDelay` 被消耗 | 受击 |
|---|---|---|---|
| dash **一直按住** | 9.01 | **0 行** | 8 |
| dash **2-on/6-off 切换** | **16.00** | **475 行** | **1** |

⇒ 动作空间能表达该边沿（`dash` 位可逐 tick 翻转），疾旋鼬的 10 tick 无敌帧**是可训练资源** ✓；
`dd`（`dashDelay`）本就在观测里 ⇒ 冲刺是否就绪**可观测** ✓。**不需要扩展动作空间。**
唯一要留意：恒定 `dash=1` 的策略永远不触发冲刺（探索要跨过的局部最优，不是模型缺陷）。

对照：**饰品类冲刺（克苏鲁之盾 `dashType==2`）按住即可触发** ✓（|vx| 6.56→14.50、受击 8→4）。
两类冲刺输入语义不同，训练里各自都能表达。

### 多段跳实测（`fishron-lilith-wolf`，mount 52 全程激活、无飞行）

| 动作 | 升高 | 最长滞空 | `jt` 取值 | `wingTime` 消耗 |
|---|---|---|---|---|
| 按住跳跃 | **774.6 px** | **519 tick** | 0–5 | 0 |
| 2-on/14 轻按 | 692.1 px | 317 tick | 0 / 11–13 / 17–18 | **100** |

按住与轻按行为**确实不同** ✓，与矩阵 C5「按住不消耗附加跳直接进入飞行、轻按消耗一次」一致 ✓；
`jt`（`p.jump`）可观测且变化 ✓ ⇒ 多段跳在动作空间里可达、在观测里可见 ✓。

---

## C121. **第 0 回合没有坐骑（已修）· 长按跳跃已建模 · 桥接动作的生效时刻**

### 1. 坐骑配装飞行**已建模** ✓，但**第 0 回合根本没召唤坐骑** ✗

A/B（恒定动作 `jump` 按住 vs 不按）：

| 配装 | jump | 地面 Y | 峰值 Y | 最长连续滞空 |
|---|---|---|---|---|
| fishron-strong-wing | 0 | 7958.0 | 7940.9 | 13 |
| fishron-strong-wing | **1** | 7958.0 | **4945.5** | **659** |

但逐回合拆开后发现真问题 ✗：**第 0 回合 `mountRows=0`（899 tick 全程没有坐骑），
坐骑只出现在第 1 回合**；第 0 回合那 653 tick 的滞空是**翅膀**（`wingTime` 100→0），
不是坐骑。根因：`SummonLoadoutMount` 只有**一个调用点**（`ResetEpisode` 内），
启动路径只调 `EquipScenario` 就结束 —— 注释里"runs on every episode, not once at
boot"并不成立。

**影响**：导出/验收若落在第 0 回合，就是在**没有坐骑**的状态下判定无伤 ✗。

**修复**：启动路径 `EquipScenario(player)` 之后补 `SummonLoadoutMount(player)`。
**验证**（jump 按住）：第 0 回合 `mountRows=1599/1599`、`mountTypes={50:1599}`、
坐骑在飞（峰值 Y 6546.7，升高 1411 px，**坐骑状态连续滞空 557 tick**），
日志 `BRIDGE_MOUNT_SUMMONED type=50 active=True` ✓。

### 2. 长按跳跃/飞行**已建模** ✓（tap vs hold 逐 tick 对照）

用 `drive_route.py` 逐 tick 写动作（同配装同种子，跳跃放在 tick 380）：

| 动作 | 380 tick 后升高 | 最长滞空 | `wingTime` 消耗 |
|---|---|---|---|
| tap（按 **1** tick） | **0.0** | 0 | 0 |
| hold（按 **60** tick） | **543.1 px** | **169 tick** | **恰好 60** |

⇒ 翅膀飞行时长**严格等于按住时长** ✓，长按语义成立。多段跳：观测里带 `jt`
（`p.jump`，剩余额外跳数）✓，跳跃位是按住态、可逐 tick 翻转 ⇒ 策略能自行组织多段跳 ✓。

### 3. 桥接动作通道在 **tick≈239** 才生效（不是接管 tick 120）✗

`RouteReplay.TraceBridge` 的首次读取记录为 `accepted tick=239 dir=0 jump=True`；
把跳跃放在 tick 121–140 时**完全无效**（trace 里根本没有那段读取），
放到 380 才生效。原因：route/bridge 分支随战斗激活才接管。**影响**：接管 tick 120
到 ~239 之间的策略动作被忽略（约 120 tick 的中立期），任何"从接管就开始动作"的
假设都不成立。`TryReadBridge` 的过期守卫**已被移除**（"latest well-formed line
always wins"），所以不是 tick 新旧问题。

---

## C120. **难度差异 / 疾旋鼬坐骑冲刺——两项实测**

> **2026-09-23 补注：疾旋鼬路线（坐骑 64/65）已整体删除，见 C125。本节只作历史记录，
> 不再有行动价值。**

### 1. 疾旋鼬（trusty-chillet）坐骑的冲刺与无敌帧：**当前动作集触发不了** ✗

A/B（同配装、同种子、静止/直行的恒定动作，模拟 DPS 压到 1 以排除干扰）：

| 配装 | `dash` 位 | \|vx\| 峰值 | `dashDelay≠0` 行数 | `eocDash` 峰值 | 受击次数 |
|---|---|---|---|---|---|
| fishron-strong-wing（克苏鲁之盾） | 0 | 6.56 | 0 | 0 | 8 |
| fishron-strong-wing | **1** | **14.50** | **1121** | **15** | **4** |
| fishron-trusty-chillet（坐骑 64 已激活 ✓） | 0 | 9.06 | 0 | 0 | 9 |
| fishron-trusty-chillet | **1** | 6.26（**更慢**） | 0 | 0 | 9 |

- **饰品类冲刺是有效的**：`dash` 位 → `SetControl(player,"controlDash",plan.Dash)`
  → 强翼配装速度翻倍（6.56→14.50）、`dashDelay` 被消耗、`eocDash` 到 15、**受击 8→4**
  ⇒ 冲刺的无敌帧确实在起作用，且策略可用 ✓。
- **坐骑冲刺无效**：同样的 `dash` 位对疾旋鼬毫无作用（速度不升反降、`dashDelay` 全程 0、
  `eocDash` 恒 0、受击不变）⇒ **坐骑的冲刺与无敌帧完全没被建模** ✗。
  全仓库 `MountDash`/`DashAbility` 零匹配；`VanillaMountCatalog` 只登记
  `(id,itemId,buffType,name)`，没有冲刺/无敌帧字段。
- 坐骑本身是激活的 ✓（观测 `mountType=64`，且每集 `SummonLoadoutMount` →
  `BRIDGE_MOUNT_SUMMONED`）。**缺的是冲刺触发方式**：vanilla 里坐骑冲刺不是
  `controlDash`（而 `controlDash` 对饰品冲刺有效），所以要么补触发通道，
  要么把坐骑冲刺/无敌帧按引擎实测建模进动作空间。

### 2. 经典/专家/大师：**血量与伤害随难度放大；轨迹差异是伤害的下游效应**

同种子、同恒定动作、`CHAITE_SIM_DPS=1`：

| 难度 | 猪鲨满血 | 猪鲨每次受击掉血 |
|---|---|---|
| classic | 60000 | 54–67 |
| expert | 78000 | 79–98 |
| master | **99450**（=78000×1.275 ✓） | 109–143 |

- 血量是**原版数值** ✓（与 wiki 一致），大师 = 专家 ×1.275 ✓。
- 伤害约 ×1.5（专家）/ ×2.5–3（大师）✓。
- **BOSS 轨迹的差异是伤害的下游效应**，已用玩家侧对照证明：玩家 `pl`（血量）与
  `py`（受击退位移）在各难度下**本来就不同** ⇒ 轨迹分歧源于击退，而非另一套 AI ✓。
- **未定论（需更多重复）**：猪鲨 `ai[0]==3` 只在 expert/master 的单次运行中出现
  （classic 单次没有，但那次可比窗口只有 880 tick，可能只是没走到）。这只跑了一次，
  **不足以断言 AI 不同** ✗
  —— 探针自身的 `-phase p3-` 守卫要求 expert，与前者一致，但仍需长窗口重复验证。
- 训练当前跑的是 **expert** ✓（`runprobe.ps1` 默认 `-Difficulty expert`，runner 未覆盖）。

**新增工具** `training/drive_route.py`：用录制路由驱动在线桥接，既得到逐 tick 观测流、
又得到长存活（route 回放模式不写逐 tick 流，而静止动作只能活 ~1000 tick 且各难度回合
切分不同）。注意：Python 驱动存在写入时序抖动 ⇒ 玩家轨迹不保证逐 tick 相同，
所以**严格的 AI 对比仍应使用恒定动作**。

---

## C119. **根因：BOSS 被打死后被误判为"超囊波斯" ⇒ win 永不可达，训练被教成反面**

**现象**：7 路训练 3000+ 集**零胜利**，而 Fishron 各路的最高伤害精确停在
77981–77984 —— 恰好等于夹具里 Fishron 的满血（实测 `lifeMax=77982`，不是 78000）。

**定位链**（全部为实测，非推断）：

| 步骤 | 证据 |
|---|---|
| BOSS 确实被打死了 | 强制 `CHAITE_SIM_DPS=20000`（新增诊断开关，不改常量、不污染训练轮）：`bossDamage=77667`、`bossLife=0` |
| 结局却不是胜利 | `FINISH Cancelled battlePassed=False win=False` |
| 插件给出原因 | `chaite.log`: **`Unsupported Boss rejected: no verifiable active Boss root`** |
| 身份校验是过的 | 探针 `BOSS_KILL_REPORT slot=0 type=370 life=0`、`ARM authorized type=370` 均出现，且**无** `KILL_REJECT` |
| 第二条死代码 | `Runtime.OnNpcKilled` 是 `PendingKilledBosses` 的**唯一**写入者，而探针/补丁/插件里**没有任何调用点** ⇒ `KilledBossKeys` 恒空 ⇒ 即便不误判，`SuccessNoDeath` 也不可达 |

**机制**：模拟扣血把 `npc.life` 打到 0 后 NPC 立刻 inactive ⇒ 活性范围校验
`TryValidateActiveBossScope` 找不到活动 BOSS 根 ⇒ `RejectUnsupportedBoss` ⇒
`_encounter.Cancel()` ⇒ 结局 `Cancelled`。而 `Cancelled` 在奖励里按超时 **−10000** 计，
**打死 BOSS 反而被惩罚**，`win` 的 +600000 永远拿不到。

**修复**（`16ac5b2`）：
1. 探针 `ReportBossKills()`：在 `ApplySimulatedPlayerOutput()` **之后同帧**上报击杀
   （`Runtime.Tick` 跑在 `Player.Update` 入口，晚一帧就输掉竞争——先放在
   `AfterNativeUpdate` 顶部时仍被拒）。要求 `life<=0`，以此区分"击杀"与"夹具自己
   despawn 重置"（后者不掉血），每集清空上报集合。
2. 插件 `_authorizedBossKilled`：击杀被 drain 时置位，会话重新武装时清零。
3. 护栏放在 **`RejectUnsupportedBoss` 内部**——先按调用点逐个加护栏时漏掉了
   `TryCompleteDeferredActiveAdmission`（每 tick 开头、坐骑/抓钩交接路径），仍被拒。

**验证**：同一实验 `e=0/e=1 outcome=SuccessNoDeath win=True`，插件日志不再有 Unsupported
拒绝。**重启 7 路后 5 分钟内 train19 与 chl14 各出现 1 场胜利**（此前 3000+ 集零胜）。

**教训**：`hits==0` 与 `win` 是两件事；此前所有"无伤"目标都在追一个不可达的终点。
凡"某个结局从未出现过"时，先证明它可达，再谈收敛。

---

## C117 附：DPS 档收窄（使用者裁定）

模拟输出由 300–1000 收窄到 **600–1200**（实测生效 613–1194）。原因：名义 DPS 只有
60–97% 真正到账（扣血需 BOSS `active`，生成期与相位切换期不计），300 档一局要
17333 tick（4.3 分钟不挨一下），是整个训练集里最难、且唯一"策略完美也可能超时"的一档。
600–1200 让最长一局约 9500 tick，同时保留足够的击杀时钟变化。`MaxTicks`
22000 → **16000**（runner 与 `evaluate_policy.py` 同步）。

**交付链在新机制下端到端验证**：导出 `route-sw-r2.csv`（**从 tick 1 起**记录——锁步
让游戏等到训练器接入，旧版要 824）→ 按 tick 回放验收 **hits=5，与导出基线完全一致**、
同一次死亡（tick 差 3364 vs 2765 是死亡后等待复活的尾巴）。模拟输出由
`(seed, episode)` 决定 ⇒ 回放复现同一场战斗。

---

## C117. **接管只负责走位：输出改为模拟（300–1000 DPS + 泡泡高概率被打破）**

使用者裁定：程序接管**不再接管开火与瞄准**，只走位保证无伤；训练中取消输出训练，
改为模拟玩家输出量。落地：

### 1. 关掉开火接管（`src/Chaite.Plugin/Runtime.cs`）

路由/桥接分支里，计划在 `ApplyPlan` 之前被显式收窄为纯移动：
`plan.Fire=false`、`OutputRouteKind=Unspecified`、三个 output 证书清零。
`TerrariaFacade` 的 `WeaponActionGate.ShouldFire(plan.Fire,…)` 因此恒为 false ⇒
玩家一枪不发，战斗只由走位决定。

### 2. 模拟玩家输出（`tools/GameProbe.cs: ApplySimulatedPlayerOutput`）

- 每集从 `(seed, episode)` 派生一个确定性 DPS ∈ **[300,1000]**（`System.Random(seed*7919
  + episode*104729 + 17)`）——确定性是硬要求，否则验收无法复现同一场战斗。
- 每 tick 扣 `DPS/60`，小数进位保留，直接写 `npc.life`（不模拟任何射弹）。
  扣血发生在 `TrackExpectedBossDamage()` 之前，所以它照常被计入 `bossDamage`，
  BOSS 死亡走原本的状态机 ⇒ `SuccessNoDeath` 即胜利。
- **猪鲨泡泡**：Detonating Bubble 是 **NPC 371**（不是射弹），每 tick 以 **0.35**
  概率被打掉（模拟玩家开火）。372/373/384（Sharknado 系）保持为真实威胁。

隔离探针实测（中性动作、4 集）：`SIM_OUTPUT episode=0..3 dps=845.7/902.2/958.7/315.3`，
`simulatedDamage=10332` 对 `bossDamage=10463`（一致 ⇒ 扣血确实被计入），
泡泡每集打破 7/14/20 个。7 路重启后每会话 12–22 条 `SIM_OUTPUT`、泡泡 142–268 个。

### 3. 奖励与回合预算随之调整

- **去掉 BOSS 伤害奖励项**：伤害现在由脚本 DPS 产生，与策略无关，记账就是给每条
  回报加常数。剩下的目标恰好就是"活着、别挨打、让战斗结束"。
- `MaxTicks` 6000 → **18000**：300 DPS 下击杀 78000 血需要 260 s = 15600 tick，
  否则慢 DPS 的回合永远赢不了。`StepsPerRound` 350000 → **190000**（frame-skip=2）。

### 4. 顺带的吞吐结论（本机已达上限）

`Thread.Sleep(1)` 实测 **16.04 ms**（默认 15.6 ms 定时器精度），原"每 4 tick 睡一次"
把游戏硬压到 ~250 t/s；换成**按训练器动作 tick 锁步**后，自由运行上限
**196 → 644.7 t/s**（3.3×），冻结动作时锁步在第 9 tick 刹住并升级退出（已证）。
训练器侧修掉两处浪费：PyTorch 默认线程池（每进程 47 线程、7 路共 ~330 线程抢 32 核，
占 28.5% CPU，而 7 个游戏只占 2.5%）→ `torch.set_num_threads(1)`；动作文件写入
tmp+replace（1210 µs）→ 原地单次写（163 µs，实测 in-place 反而 7077 µs，故保留 open('w')）。
但**在线吞吐仍只有 ~180 tick/s/会话**：往返延迟中位 3.05 ms、p90 13.81 ms，而单步真实
工作量只有 ~1 ms ⇒ 同步往返是天花板，故引入 **frame-skip=2**（每 2 tick 决策一次、
动作保持），把延迟摊薄。CPU 已 88%、提交上限只剩 1.9 GB ⇒ 并行度 7 路即上限。

---

## C116. **回合按 tick 封顶（内存峰值确定化）＋ 稳定无伤的多种子验收**

### 1. 墙钟不是安全的回合边界

实测 tick 速率 **137–157/s**（`FRAME` 行按阶段统计），而 `RoundSeconds=5400`
在 150/s 下投影出 **74–84 万 tick —— 已越过 32 位地址空间约 66 万 tick 的天花板**。
这解释了 train7 的 `OutOfMemoryException`，以及 7 路在墙钟到点时同时暴毙。

改法：探针新增**运行级 tick 上限**（环境变量 `CHAITE_RUN_MAX_TICKS`），
`runTickLimit>0 && ticks>=runTickLimit` 即 `Finish("test-time-limit")`；
写入 CASE 日志与 `result.json` 的 `limits.runTicks`。分轮器设
`RunMaxTicks=380000`、`StepsPerRound=350000`、`RoundSeconds=4200`（仅作兜底）。
⇒ 每轮的内存峰值与运行时长都由 tick 决定，与机器负载无关。

资源实测（决定并行度上限）：CPU **88%**（32 逻辑核）、物理空闲 9 GB，
但 **提交上限只剩 1.9 GB** —— 所以**不能**再加游戏进程；7 路就是这台机器
（与使用者其它程序共存）的上限。

### 2. 稳定无伤 ≠ 单种子无伤：`training/evaluate_policy.py`

猪鲨的瞄准、弹幕掷点、生成时机都随种子变化，所以**一个种子的无伤不构成证据**。
验收工具对候选策略跑 **K 个种子**（默认 5 个），每个种子一个**独立探针进程**
（独立副本、独立存档路径、独立桥接文件），策略从观测流一出现就接入（实测接入
tick 16–36，即战斗一开始就接管），判据取**引擎自己写的 `result.json`**
（单集运行不写 `bridge.episodes.jsonl`，那要等下一集开始才落盘）。
只有当 **K 个种子全部 `win && hits==0`** 才判定 `stable_no_hit=true`。
单种子实测约 53–74 s，5 种子约 5 分钟。

### 3. 交付链三件套（全部已实测）

| 工具 | 作用 |
|---|---|
| `training/export_route.py` | 策略 → 按 tick 索引的路由（`--attach-current`，含中性头填充） |
| 路由回放验收 | 隔离探针按 tick 重放该路由，判据 `hits==0`（实测复现同一场战斗，差 1 次受击） |
| `training/evaluate_policy.py` | 同一策略在 K 个种子上跑，出 `stable_no_hit` 结论 |
| `training/watch_progress.py` | 后台驻守：任一会话出现无伤胜利即退出并通知 |

---

## C115. **奖励的最优解曾是"别打、拖到结束"，不是"干净地赢"**（已修）

### 实测（train10 自己的回合，按 obs 流逐帧累加掉血）

| 情形 | 旧奖励 |
|---|---|
| 3 次胜利（掉 549 / 669 / 753 HP） | **−119643**（最好的一次） |
| 1383 tick、掉 93 HP、**既没赢也没死**的截断回合 | **−47214** |

旧参数（`WIN=1e5`、`HURT=400`、`TIMEOUT=−1e4`）下，**胜利是负收益且比"拖到结束"
更差**——因为 Fishron 一局要吃掉 500–750 HP，`HURT` 罚分把 `WIN` 完全淹没。
这正好解释了"1.7M 步只有 3 次胜利、且没有一次低于 5 次受击"的平台期：
策略学到的最优行为就是别打。

### 改法（把目标函数按验收口径重排）

```
WIN_REWARD   = 600000      # 一局胜利要盖过一整管血的罚分
NO_HIT_BONUS = 400000      # 仅当 win 且 hits==0，使无伤成为唯一最优
DEATH_REWARD = -300000     # 死亡明确差于拖时间
TIMEOUT      = -10000      # 不变
HURT_FACTOR  = 400         # 不变（无伤的主要梯度来源）
gamma        = 0.9998      # 0.9995 在 4000 tick 处只剩 0.135，终端信号太弱
```

排序自检（4000 tick）：

| 情形 | 奖励 |
|---|---|
| 无伤胜利 | **+999960** |
| 胜利，掉 300 HP | +479960 |
| 胜利，掉 550 HP（train10 现状） | +379960 |
| 胜利，掉 700 HP | +319960 |
| 拖时间，0 掉血 | −10040 |
| 死亡，掉 550 HP | −520040 |

⇒ 胜利由 −119643 变成 +379960，拖时间由 −47214 变成 −10040，"打干净"成为
唯一最优。7 路已用新目标函数重启（检查点续训，策略权重保留，价值头会重新适配）。

---

## C114. **训练产物的交付链已打通：策略 → 按 tick 索引的路由 → 隔离探针验收**

### 1. 位置索引的回放**不可能**复现长回合（实测）

旧格式（`direction,jump,dash`，按行号索引）用的是插件的 `_replayFrames`，而它
**只在插件真正施加了计划的 tick 上自增**，不是游戏 tick。引擎内实测同一会话内
`tick - replayFrame` 从 **240 漂到 96965**（第 0 集内就已从 240 漂到 265、307…）。
⇒ 位置索引的路由跑不了 6000 tick 的战斗。

**改法**：路由文件支持四列 `tick,direction,jump,dash`，按**绝对游戏 tick** 查表、
保持最后动作（与桥接通道同语义），起点之前返回"未覆盖"交回规划器。
tick 来源是 `Main.GameUpdateCount`（与探针发布、训练器写入 action 文件的是同一个
计数器），经 `TerrariaFacade.GameTick()` 暴露（`StaticPropertyGetter<uint>`）。
三列文件仍按位置索引，旧路由不受影响。

### 2. 单集验收此前**根本起不来**

`ValidateLaunch` 里"Episode mode requires CHAITE_BRIDGE_FILE"把
`CHAITE_ROUTE_FILE` 驱动的单集回放直接拒了（`LAUNCH_REJECTED`）。已放开为二者
任一即可——这条路正是导出策略的验收路径。

### 3. 端到端实测（疾旋鼬策略，`ppo-chl2`）

| 阶段 | 结果 |
|---|---|
| 导出（策略实时驱动单集，`--attach-current`） | 590 tick 动作，写成 1414 行路由（tick 0..1413，含 824 行中性头填充） |
| 训练那一次（exp2） | `hits=10 deaths=1 ticks=1791` |
| 按 tick 回放验收（acc2） | `hits=9 deaths=1 ticks=1766` |

**回放忠实度**：差 1 次受击、25 tick。差异来源已定位：桥接写文件是**异步**的
（训练器按 tick T 写入时游戏可能已到 T+1），所以录下的是策略的**意图序列**，
而回放把该序列按 tick 精确施加。这不影响验收口径——验收的判据本来就是"隔离
探针按这条路由跑出 `hits==0`"，而不是"与训练日志逐帧相同"。

### 4. 导出必须接在**第 1 集**内

tick 是全局计数器：第 3 集导出的路由只覆盖第 3 集的 tick，而验收跑的是第 1 集。
所以 `ChaiteBridgeEnv(attach_current=True)` / `--attach-current` 直接接当前这一集
（`BridgeStream.latest_row()` 读文件尾最后一条完整行），并在导出时把
`0..首次动作-1` 填成中性——那正是桥接在训练器开口之前的真实状态。

### 5. 回合墙钟与僵死（旧代码的训练器会挂死）

游戏到墙钟上限就退出，**旧代码**的训练器会卡在 600 s 的流等待里（每步 10 分钟），
7 路全部僵死 >1300 s。新代码的 `GameSessionEnded` 正是治这个（C113）。
另外负载下游戏只产出 ~50-80 tick/s，所以 `RoundSeconds` 由 3000 提到 **5400**
（≈27-43 万 tick，仍远低于 66 万上限），`StepsPerRound=400000`。

---

## C113. **配装是"成套"的：两处成套缺口已修；长会话 OOM 用分轮制解决**

### 1. 使用者 2026-09-19 的口径更正（全部已落）

1. **疾旋鼬 64/65 只是换皮**，合并为一套训练（`fishron-trusty-chillet` 覆盖 ignis）。
   ⇒ 停止 ign2 独立会话，不再为换皮单独训一个模型。
4. **配装是成套的，不是单个物品**。逐套核对 `docs/route-equipment-matrix.md`
   后发现两处夹具缺口（都已修）：
   - **第 4 套**（疾旋鼬）缺**气球束 1164 + 羽落**：使用者确认多段跳件就是气球束。
     夹具补 `armor[3]=1164` + 原生 `AddBuff(Featherfall)`。
   - **坐骑套（第 4 套）多给了蛙靴**：使用者确认坐骑套不含蛙靴。夹具删掉
     `AmphibianBoots`（第 4 套被气球束取代）。

**成套改动的执行证据（引擎内实测，铁律口径）**：同一场景、同一配装，
`jt_max` 由 **11 → 33**（气球束的四段跳），`jt_nonzero` 39..150 → 204..741。

**羽落过期的实测结论（未改）**：羽落 buff 只在开机加一次，36000 tick 后按
vanilla 必然过期。但按回合统计 `vy_min` 在过期边界两侧**完全不变**
（-8.61 贯穿 67 回合、25 万 tick），且第 4 套"有羽落 vs 无羽落"的对照同样是
-7.7 ⇒ **坐骑状态下羽落对下落动力学无可测影响**（坐骑自身速度主导），
故不作为保真缺口处理，仅记录。

### 2. **长会话 OOM：Terraria 是 32 位进程，按 tick 撞地址空间上限**

`train7` 在 **661k tick** 前后 `System.OutOfMemoryException`
（栈顶都在 `Terraria.Lighting.AddLight`），随后 `fw1/chl2/lw1` 又在同一分钟
**并发死亡**（系统提交上限）。⇒ 单进程有硬天花板，与并发数无关；**分轮制**同时
解决两件事：每轮换新游戏进程（内存回到基线）＋训练器从检查点续训。

`training/run-session.ps1` 改为分轮：`RoundSeconds=3000`（≈45 万 tick，安全低于
66 万上限）、`EpisodesPerRound=400`、`StepsPerRound=500000`、`Rounds=12`。
两个坑（都已修）：
- `train.py` 的 `--timesteps` 是**绝对目标**（`while model.num_timesteps < timesteps`），
  续训的检查点已带步数 ⇒ 固定每轮预算会让每轮**一步不训就退出**。目标必须
  随轮次递增，并以检查点实际步数为基准（从 `ppo.log` 的
  `checkpoint saved at N steps` 与 `PPO.load(...).num_timesteps` 两处取）。
- 每轮开始时删掉遗留的 `bridge.action`，否则上一轮的动作会落到新进程头几帧。

### 3. 当前 4 路矩阵（= 4 套猪鲨，全部并行）

| 会话 | 场景 / 配装 | 备注 |
|---|---|---|
| train8 | 猪鲨 / 强翼（第 3 套） | 续 `ppo-fishron-sw-v2` |
| fw2 | 猪鲨 / 仙翼（第 2 套） | 续 `ppo-fw1` |
| chl4 | 猪鲨 / 疾旋鼬（第 4 套，覆盖 65） | 成套已修（气球束） |
| lw2 | 猪鲨 / 莉莉丝狼（第 1 套） | 续 `ppo-lw1` |

**重建不影响运行中会话的哈希证据**：7 路活动副本的 `Chaite.Core.dll` SHA256
在重建前后**全部逐字节不变**（`71315F74AF88…`）；每次 prepare 都是私有单次副本。

---

## C112. **★★★ 动作通道此前完全没生效（tick 守卫）；坐骑配装从未上马（两条根因，均已实证修复）**

### 1. 桥动作通道：`tick >= _bridgeTick` 单调守卫静默吞掉全部动作

**症状**：训练 40 万步曲线纹丝不动（逐回合恒 8 受击 / ~1200 tick / 伤害 ~13000）。

**隔离对照（同夹具、同种子、同配装，铁律口径）**：

| 对照 | 位移 | jt | vy_min | 结果 |
|---|---|---|---|---|
| 中性（不发动作） | 260 px | 0 | -3.5 | 1184 tick 死亡，伤害 ~13000 |
| `999999999,1,0,0` 冻结 | **3708 px** | 0 | -3.5 | 受击 8→4 |
| `999999999,0,1,0` 冻结 | 310 px | **15** | **-16.5** | timeout，伤害 45039 |

冻结 `dir=1` 时玩家**不动**（与中性逐项相同）= 分支未执行。根因：一旦某行携带
较大 tick（残留旧文件 / 两个训练器抢同一文件 / 哨兵值），`_bridgeTick` 被抬高，
**此后活动训练器写的每一个正常 tick 都被静默拒绝**，plan 永远中性；而
`catch (IOException)` 把读失败也吞掉，外部完全无声。

**修复**（`src/Chaite.Core/RouteReplay.cs`）：
1. 去掉单调守卫——最新一行合法内容永远生效（`os.replace` 原子，读到的必是完整一代；
   沿用上一代一拍 == 既有 hold-last 语义）。
2. 读取改 `FileStream(FileShare.ReadWrite|Delete)`——此前游戏以 `FileShare.Read`
   持有该文件，训练器 `os.replace` 偶发 `PermissionError`，实测在 398451 tick 处
   直接杀死训练器。
3. 训练器 `write_action` 加重试 + 原地写回退。
4. 新增有界诊断 `<base>.action.trace`（前 12 次 + 每 2000 次读结果）。**被吞掉的
   读失败与"训练器根本没写"在外部无法区分**——本次就是靠它定位的。

**修复后实况**：动作被接受且逐帧变化；回合长度 1200→1812..2635 tick，
单回合伤害 13000→29000..41764。

### 2. 坐骑配装从未上马（3/6 条猪鲨线）

`fishron-trusty-chillet` / `fishron-trusty-chillet-ignis` /
`fishron-lilith-wolf` 三条线的配装是 `miscEquips[3]` 的**坐骑物品**，而桥覆盖把
`plan.ToggleMount = false`（Runtime.cs L268）→ 永远没人按上马键。

**引擎内实证**：chl1 整场 `mountActive=False`、`mountType=-1`，单回合伤害卡在
**285**（同轮次翼类线 17156）。

**修复**：`ResetEpisode()` 里新增 `SummonLoadoutMount(p)`——按装备物品的
`Item.mountType` 调原生 `Mount.SetMount(type, player)`（与 vanilla 按上马键同一
路径）。上马属于**配装**，不是战斗要玩家做的决策，所以由夹具在每回合重置时召唤
（死亡会下马，故每回合都要做，不是开机一次）。观测行新增 `ma`/`mt`（`obs_vector`
按名取值，98 维不变）。

**对照**：修复前 chl1 平均伤害 285 → 修复后 chl2 **16934（59×）**；ign 230→19360、
lw 18670，全部与翼类同量级；日志 `BRIDGE_MOUNT_SUMMONED type=64/65/52`。

### 3. 并行训练布局（缩短总时长）

`training/run-session.ps1`：单会话 = 独立 Tag + 独立桥目录 + 独立检查点；先起游戏、
等观测真正在流再挂训练器；训练器退出后用 `taskkill /T` **只杀自己那棵进程树**
（裸杀全部 Terraria 会带倒其它并行会话）。

- `runprobe` 的运行目录名是 `game-probe-rt-<tag>-<stamp>` ⇒ **不同 Tag 永不撞名，
  并发 prepare 安全**（prepare 的输出全部落在各自的 run 目录里）。
- **重编译不影响运行中的会话**（实测证据）：重建前后 train7/fw1 的
  `Chaite.Core.dll` SHA256 逐字节不变（`71315F74AF88639F`），回合计数继续推进。
  每次 prepare 都是**私有单次副本**，故"训练期不得重编译"的实质（二进制同一性）
  由哈希对照保证。
- 实测资源：每会话 Terraria ~800 MB + 训练器 ~290 MB；8 路并行时 32 逻辑核、
  空闲内存 6.6 GB，训练器 ~150–200 fps。

**5 路矩阵（覆盖全部已审配装）**：猪鲨 × {fairy-wing, strong-wing,
trusty-chillet, trusty-chillet-ignis, lilith-wolf}。

---

## C111. 长会话世界事件墙（已修复，670a859）

训练会话要跨游戏日运行（213k+ tick），`UpdateTime` 的随机世界事件在专家难度
下无守卫（IL 实证：史莱姆雨在 !AnyPlayerReadyToFightKingSlime 时只有 classic
被挡，expert 放行）。事件激活天空特效 → 无头模式不注册特效 →
`MissingEffectException` → harness 死亡。修复：`RegisterNoopSkies()` 在
ContentReady 注册 NoopSky 到 SkyManager 的全部 11 个世界事件名（仅注册
缺失项；IsActive/IsVisible=false，无头不绘制，行为不可见）。

**回合循环三墙（全部 IL/引擎实证）**：
1. NPC 槽位保护计数 `spawnSlotProtected`（NewNPC 置 2，只清 active 不回收
   槽位 → rootIndex 爬升 → 第 10 回合 NewNPC 返回 200）。
2. `spawnSlotProtected` 比 `Main.npc` 短 → 按自身长度做边界。
3. 世界事件天空特效缺失（本条）。

---

## C110. 回合循环的两堵暗墙（均已实证修复）

1. **NPC 槽位保护计数（1.4.5 新机制，反汇编实证）**：`NPC.NewNPC` →
   `GetAvailableNPCSlot` 判占用用的是 `IsSpawnSlotInUse(i) =
   npc[i].active || NPC.spawnSlotProtected[i] > 0`，`NewNPC` 分配后置
   `spawnSlotProtected[slot]=2`。**只清 active 不清保护位 ⇒ 槽位永不回收**：
   rootIndex 每回合 +22（107,128,151,173,195→200），第 10 回合
   `NewNPC` 返回 200、harness 死于 "spawn did not create its expected root"。
   修复：`ResetEpisode` 清场时同步清零 `NPC.spawnSlotProtected[i]`。
   注意该数组**比 `Main.npc` 短**，必须按它自身长度做边界（否则
   IndexOutOfRangeException）。验证：rootIndex 在 0/1/2 循环复用，
   15 回合连跑通过。反汇编脚本：`tools/dump-newnpc.ps1`（Mono.Cecil）。
2. **SB3 兼容**：info 字典不能有 `episode` 键（Monitor 协议把它当 dict 塞进
   ep_info_buffer，裸 int 会让 dump_logs 崩 `len(int)`）；`use_sde` 只支持
   连续动作空间，Discrete(12) 直接被 SB3 拒绝。

**训练吞吐**：无头探针 ~150 tick/s，PPO ~182 fps。观测流 ~5KB/tick。
**交付路线**：`training/export_route.py` 把训练好的策略确定性 rollout 成
`direction,jump,dash` CSV → `CHAITE_ROUTE_FILE` 走与枚举路线完全相同的回放
通道 → 隔离探针 `hits==0` 验收。零新增 C# 代码。

---

## C109. t-agent 移植：回合模式 + 观测/动作桥（训练小模型取代穷举）

**背景**：穷举拼接口径已把拼接驱动内部的束搜索换成 `RouteEnumerator.Exhaustive`
（`CHAITE_COMPOSE_CEILING/KEEP/WEIGHTS` 可调，默认 120000/32/1,1,1,1）。fwd4 轨迹上：
51/51 闭环全部跨越、`composedHits=0`、独立回放 `replayedHits=0`、无截断、48 条候选
导出。但真机探针（`game-probe-rt-exh1-0919-082422`，猪鲨/强翼/seed2）：
**hits=9、死亡、1984 帧**。模型零受击 ≠ 引擎零受击——前瞻模型与引擎的分歧
（dash 晚一拍、accRunSpeed 陈旧）没有被解决，穷举出的安全余量在真机上不成立。

**用户裁决**：t-agent（https://github.com/jimmyjjz/t-agent，本地
`D:\personal tasks\modding\t-agent-main`）通过**训练产生小模型**，比穷举更有可行性，
用它填补战斗逻辑的空白。其架构：Gymnasium 环境 + SB3 PPO + 像素观测 +
伤害奖惩 + tModLoader 插件做进程内自动重置。

**我们的移植**（保留 t-agent 的架构，替换其实现面）：

| t-agent | Chaite 移植 |
|---|---|
| tModLoader 插件 | 原版 1.4.5.8 隔离探针（更干净：确定性、result.json、无 Steam） |
| 像素观测（dxcam） | 结构化观测（玩家/BOSS/最近12个敌弹），一行 JSON/tick |
| pydirectinput 按键 | **RouteReplay 桥模式**：动作文件经路线通道下发——训练时怎么动，拼接后就怎么动 |
| reward_denoter.txt 文件 | 观测行内的 life/bossLife（训练器自己算差分） |
| 'r' 键重置（ResetManager） | **Finish 拦截 + ResetEpisode**：进程内软重置 |
| PPO(MlpPolicy) | 同样 SB3 PPO，超参照抄其 settings.json（ent 0.3/clip 0.05/sde） |

**新增机制**（全部 `CHAITE_*` 环境变量，命令行参数不动）：
- `CHAITE_BRIDGE_FILE=<base>`：桥基路径。游戏侧 `RouteReplay` 桥模式每 tick 读
  `<base>.action`（`tick,dir,jump,dash` 单行，训练器原子替换写）；探针侧每 tick
  追加一行紧凑观测到 `<base>.obs.jsonl`（玩家/BOSS/12 弹 + e/t/done/win/hits）。
- `CHAITE_EPISODES=N`：回合模式。`Finish` 被拦截 → 写 `<base>.episodes.jsonl`
  → `ResetEpisode()`（清 NPC/弹/掉落、复活、回初始位、回满血、清 debuff、重上
  常规 buff、补消耗品、重置每回合计数）→ `episodeArmTick=ticks+30` 合成重臂沿 →
  `directSpawnTick=arm+120` 重生成 BOSS → 继续打。最后一回合走原 result.json 路径。
- 回合模式下 `-wallseconds` 上限 900→86400；`-maxticks` 按**每回合**计。
- `CHAITE_SIM_DPS=<dps>`：把注入的模拟伤害钉在固定 DPS（0/未设为既有的
  600–1200 每回合抽样）。
- `CHAITE_SIM_DPS_FULL_TILES=<格>` / `CHAITE_SIM_DPS_ZERO_TILES=<格>`：注入伤害的
  **距离衰减**曲线（单位格，1 格 = 16 px）。满 DPS 在 `FULL` 以内、`ZERO` 以外为 0，
  中间 smoothstep 平滑过渡；默认 30 / 80。曲线按玩家—BOSS 中心距逐 tick 计算，所以
  「跑远」不再是免费的。两个变量都只在启动时读一次。

**Python 侧**（`training/`，venv 已装 numpy/gymnasium/SB3/torch-cpu）：
- `chaite_env.py`：`ChaiteBridgeEnv`（gymnasium）：tail 观测流、98 维特征、
  12 元移动字母表（dir×jump×dash，与枚举字母表一致）、奖惩 = 对敌伤害×1 −
  掉血×400 − 时间×0.01 + 终局 ±100000。
- `train.py`：`--smoke`（随机策略验证闭环）/ `--train`（PPO）。

**验收路线**：训练收敛后导出 MLP 权重为 `LearnedPolicy` 文件格式（残差式，
neutral 脚本 = 直接策略），经 `CHAITE_POLICY_FILE` 拼接进程序作为完整战斗闭环，
再用隔离探针验证 `hits==0`。**训练期间不重编译**（权重是数据文件，引擎二进制不变）。

**冒烟命令**（已验证的部分见 C110+）：
```
$env:CHAITE_BRIDGE_FILE='...\bridge'; $env:CHAITE_EPISODES='3';
runprobe.ps1 -Scenario duke-fishron -Route fishron-strong-wing -Seed 2 -MaxTicks 1500 -WallSeconds 600 -TakeoverTick 120 -Tag epN
python training\train.py --smoke --base '...\bridge' --minutes 6
python training\train.py --train --base '...\bridge' --timesteps 1000000
```

---

## 0. 闭环切分与组合驱动已就位，并在**猪鲨**的真实轨迹上量了

`src/Chaite.Core/LoopDecomposition.cs`（新）：按 BOSS 状态序列切闭环，
再按「状态序列 + 起始格子」分组，输出枚举代价的缩减倍数。

**切分规则（三轮才定下来，每一轮都是被真实数据否掉的）**：

1. ~~按手写的"攻击/重定位"状态号切~~ → 猪鲨第三阶段用另一套状态号（`ai0=4..12`），
   **3 次受击全在切不开的 3258 帧里**。
3. ✅ **先游程编码状态序列，再在游程序列上找周期，并允许少数位置不匹配**（容差 0.75）。

**实测结果**：

| 轨迹 | 闭环 | 完整 | 无伤 | 分组 | reduction |
|---|---|---|---|---|---|
| 猪鲨/强翼 4394 帧 | **54** | 52 | 51 | 39 | **1.26** |

- 猪鲨切得很细（54 个约 81 帧的闭环）——这正是"固定重复脚本"。

**不变量（已写成测试）**：闭环必须**恰好铺满整条轨迹**（连续、覆盖 `[0, n)`），
且各闭环受击数**之和等于总受击数**。丢了帧的切分在汇总数字上看不出来，
而建立在它之上的搜索会去优化一场并不存在的战斗。

**切分过程中修掉的三个真 bug**（前两个同源：搜索空间把答案删掉）：

1. **bucket 参照错**：相对位置按 **tick 0 的 BOSS** 算，可 BOSS 一直在动
   ⇒ 真实轨迹上闭合率只有 4/25。必须用**同一 tick 的 BOSS**。
2. **恒定段假周期**：恒定段在任意"装得下"的周期上自匹配 ⇒ 悬停被切成碎片。
   （游程编码后此问题自然消失：悬停就是一段游程。）
3. **比较跨度太长**：用 3 个周期做证据会伸进轨迹尾部的非周期段，
   反而把前两个周期已经确立的周期投掉。改成 **2 个周期**。

**口径纠正**：`Closed`（回到起始格子）**不是切分依据**——切分依据是 BOSS 的脚本周期；
"玩家回到起点"是**搜索要达到的目标**（`RouteEnumerator.IsGoal`），不是切分前提。
把两者混在一起，会让切分丢掉搜索最需要修的那些闭环。

### 0.1 组合驱动已就位（`src/Chaite.Core/LoopComposition.cs`）

把「每闭环一条路线」接成整场，并**证明接得上、且整场仍无伤**。
`LoopComposition.Compose(segments, routes, request)` 返回
`Complete / Connected / RoutesMissing / RoutesDirty / TotalHits /
EnumerationTicks / ReductionFactor / Breaks[]`。
每个 `Break` 带**闭环下标、失败的那条边界、以及距离**，所以失败是**可定位**的，
不只是"组不起来"。

**关键：分组省下的枚举量 = 不同组数，不是闭环数。** 40 个同组闭环只付 1 条路线的 tick
（测试 `CompositionReusesOneRoutePerGroup` 断言 reduction≈40）。

**为什么要专门检查边界**：同组的两个闭环起点在**同一个 bucket**，而 bucket 是**故意粗糙**的。
一条"终点落在正确 bucket"的路线，可能离下一个闭环的真实起点几十像素远。
接起来就是一场**从不发生的战斗**，而且**全程看起来都是干净的**。
所以检查用的是**下一个闭环记录的真实起点**，不是 bucket
（测试 `CompositionUsesTheNextLoopsRealStartNotTheBucket` 专门证明这一点：
同一个链条在 16 px 容差下通过、在 4 px 容差下必须失败——bucket 比较永远看不到差别）。

### 0.2 真实轨迹上的两个实测（猪鲨/强翼 4394 帧）

**① 端点 tick 约定（真 bug，已修）**：`EndPosition` 原本取 `observations[end-1]`，
但闭环覆盖 `[start, end)`，它留下的状态是 **tick `end` 起点**采样的那个，也正是下一段的起点。
差一 tick ⇒ **边界间隙 12.73 px**，会让每条边界都看起来断开。改为 `observations[end]` 后
**`boundaryMaxGap=0`** ⇒ 组合的边界检查可以很紧。

**② 同组闭环**不是**精确平移**（方法层面的重要发现）：

| 组 | 闭环数 | 起点绝对分散 | **BOSS 相对分散** |
|---|---|---|---|
| 3 | 4 | 4995 px | **410 px** |
| 10 | 3 | 4563 px | 128 px |
| 1 | 2 | 1685 px | 61 px |

- 绝对分散高达 4995 px 本身**不说明问题**：场地是长直平地，只差一个平移的两个闭环
  是**同一个问题**，一条控制序列可以解决两个。
- **但 BOSS 相对分散是 59–410 px**，而 bucket 带宽是 **200 px** ⇒
  **同组闭环的相对几何并不相同**，`410 px` 甚至超出带宽。
- ⇒ **分组承诺的"一组一路线"缩减，几何上并不总是成立。**

### 0.3 修正：威胁签名加入「BOSS 相对格子」，并**量出了分组缩减其实是 0**

签名原本是 `StartBucket | BOSS状态序列`，而 bucket = 「200 px 距离带 × 4 方位扇区」——
**带内相差近 400 px 的闭环会被判为同组**。现在加入
`RelativeCellSize`（默认 32 px）量化的 **BOSS 相对偏移格**（`RelativeCell`）。

**在猪鲨的真实轨迹上扫描格子大小**：

| 轨迹 | 格子 | 组数 | 组内最大相对分散 | 分组缩减 |
|---|---|---|---|---|
| 猪鲨(51 闭环) | 0（旧） | 38 | **410 px** | 1.23（**假的**） |
| 猪鲨 | 200 | 46 | 170 px | 1.08 |
| 猪鲨 | **64 / 32 / 16 / 8** | **51** | **0** | **1.00** |

⇒ **凡是真的让分组变正确的格子（≤64 px），每个闭环都自成一組，reduction = 1.00。**
**"一组一路线"的复用缩减，在猪鲨的真实轨迹上是零。**
之前那个 1.23 是过粗分组凑出来的假象。

### 0.4 修正：真正的缩减来自「分解成独立短搜索」，不是「路线复用」

我一直在**量错东西**。`ReductionFactor = fightTicks / enumerationTicks` 量的是**复用**，
而复用为零。分解的真正贡献是**把一次长搜索变成 N 次独立短搜索**：

| 轨迹 | 闭环 | **最长闭环** | 战斗 tick | **搜索视野比** | 分组缩减 |
|---|---|---|---|---|---|
| 猪鲨 | 51 | 317 | 4155 | **13.11×** | 1.00 |

新增 `LongestLoop` 与 `SearchHorizonRatio`（`fightTicks / longestLoop`），
**这两个数字不得再混为一个**——把它们合成一个数，正是方法虚报缩减的来源。

**⚠️ 但 13× 本身远远不够**：12 动作的字母表下 `12^317` 仍然是天文数字。
⇒ **分解让搜索变短，但真正让它可行的是枚举器的支配剪枝 + beam + 启发式。**
分解是必要条件，不是充分条件。**这条必须记住，不得再把分解当成可行性本身。**

## A. 方法铁律：禁止手写策略权重

使用者已多次强调。**候选必须由枚举器产生**，我只负责优化枚举逻辑：
枚举空间的定义、剪枝、排序、去重、完备性与边界报告。

- 手工挑 `w[i]` / `hw[k]` / 任何策略参数 = **违规**。
- 已删除的违规产物：`hold-dash-probe.txt`、`hold-while-far.txt`、`hold-then-issue-3way.txt`。
- 「保证能枚举出无伤」= 枚举必须**有能力覆盖到解**，不是替它想一个解。

## B. 闭环切分是枚举可行性的来源（不是背景）

整场战斗切成闭环；每个闭环内只枚举「**无伤且回到循环初始状态**」的路线；闭环之间再组合。
搜索空间因此从几千 tick 降到几十 tick。

- 单闭环搜索器**已就位**：`src/Chaite.Core/RouteEnumerator.cs`
  （12 动作字母表 + 支配剪枝 + beam + 受击优先排序 + 边界全计数）。
- **缺的是闭环切分与组合的驱动**：如何切出闭环、如何在边界接续、
  组合后如何保证整场仍无伤。

## C. 评估器缺口（已实测，非推测）

`PlayerForwardModel` 对引擎实测密集轨迹（4393 对）复算：

| 版本 | 覆盖 | 干净 tick 平均误差 | 最差 clean tick | 拒绝分布 |
|---|---|---|---|---|
| 起点 | **0.5%** | — | 4.40 px | 翅膀1275 冲刺1220 跳跃1129 火箭985 |
| 原生水平剖面 + 冲刺延续 | 7.2% | — | 8.86 px | 翅膀1690 跳跃1397 火箭989 |
| **冲刺启动判定** | 7.2% | 0.028 px | **0.300 px** | 翅膀1690 跳跃1397 火箭989 |
| **接翅膀（FlightMotion）** | **73.7%** | **0.102 px** | **14.5 px** | 火箭靴989 跳跃166 |
| **接火箭批次** | **96.0%** | **0.083 px** | **14.5 px** | **跳跃176** |

**覆盖率 ×192（0.5% → 96.0%），干净 tick 平均误差 0.083 px，
地面接触 987 个仍保持 0.300 px 精度。**
冲刺、翅膀、火箭靴的拒绝**全部清零**。

### 剩余：跳跃 176（唯一拒绝）+ 贴墙 14.5 px（唯一精度缺口）

**结论：C 项（评估器缺口）已基本关闭。** 现在有了便宜且可信的评估器，
**枚举不再被评估器卡住**——这是本轮最重要的进展。

### 最差 clean tick 14.5 px = **贴墙**（缺失瓦片碰撞）

tick 344：玩家**被卡在墙边**（x 在 343–346 恒为 640），冲刺确实启动了
（`dashDelay` 0→-1）但引擎 vx=0、立刻进冷却；模型给出 -14.5。
**这不是公式错，是模型从未声称有瓦片碰撞。** 场地是长直平地，贴墙只发生在两端。
⇒ 下一个要解决的要么是瓦片碰撞，要么是在搜索里显式处理边界。

### 测量口径修正（重要，同一类"问错问题"）

**受击 tick 与被护盾撞击的 tick 必须从精度统计里剔除**，否则等于拿"BOSS 打了玩家"
去指责模型。实测：剔除前最差 16.9 px，其中
- 4 个 tick 是**受击**（生命下降），
- 45 个 tick 是**护盾撞到 BOSS 后的反弹**——**没有掉血**（`immune=4`、
  速度从 +8 反向到 -9），因为已审核的冲刺契约明确写着
  "护盾的命中/反弹/无敌路径不在本契约范围内"。

剔除后平均误差 0.124 → **0.102 px**。
⇒ **报告精度前必须先问：这个 tick 的误差，模型有没有可能避免？**

### 已修的真 bug（4 个）

1. **观测配对差一 tick（控制位）**：漏掉每一次起跳 → 地面误差 0.317 → 6.21 px。
2. **观测配对差一 tick（冲刺状态）**：每次冲刺启动被当成普通水平运动 → 8.86 px 过冲。
3. **`controlJump` 不足以判断起跳**：必须用引擎自己的 `justJumped`。
4. **资源计数器方向搞反**：`jump`/`wingTime`/`rocketTime`/`rocketDelay` 是模型**要递减**的，
   必须取**当前行**（tick 起点）；只有控制位与 `justJumped` 取**下一行**。

⇒ **配对规则（逐字段过一遍）：位置/速度/资源计数器 = 当前行；控制位/`justJumped` = 下一行。**

### 探针字段补齐记录

`dash`/`dashTime`/`timeSinceLastDashStarted`/`direction`/`releaseDash`/`runAcceleration`/
`runSlowdown`/`rocketBoots`/`rocketRelease`/`canJumpAgain_Cloud`/`hasJumpOption_Cloud`/
`isPerformingJump_Cloud`。
⚠️ `wingTimeMax` **原本已存在**，重复添加会让探针以"重复键"崩溃（`Dictionary.Add`）。

## C2. 滚动（rollout）能力：**128 tick 内逐位精确**

搜索需要**连续滚动**整场，而 `TryAdvance` 原本只返回位置与速度，
**丢掉了演化后的冲刺/跳跃/飞行子状态** ⇒ 无法滚动（只能做 1 tick 复算）。
已新增返回**整个下一帧**的重载 `TryAdvance(frame, controls, out PlayerMotionFrame next, out refusal)`，
并串接 `Dash` / `Jump` / `Flight` / `WingTime` / `Grounded` / `Dashing` / `JustJumped`。

**实测（猪鲨/强翼 4394 帧，用录制控制滚动）**：

```
ROLLOUT horizon=32   error=0
ROLLOUT horizon=64   error=0
ROLLOUT horizon=128  error=0
ROLLOUT horizon=256  error=21.48   ← 超阈值停止
```

- **误差在前 128 tick 内精确为 0** ⇒ 帧串接是**逐位正确**的，不是"近似能用"。
- 128 tick 已经**长于猪鲨的典型闭环（81 tick）**。
- 分歧**不是缓慢漂移**（那样 32/64/128 会非零），而是**一次离散事件**。

**分歧原因：贴墙（缺失瓦片碰撞）**。tick 255–259 玩家**完全静止在 x=640**
（贴左墙按住左键），而模型没有瓦片碰撞 ⇒ 预测继续向左加速，到 256 tick 累积 21.5 px。

⇒ **模型在"不贴墙"的闭环内是可信的评估器；贴墙是唯一已知缺口。**
场地是长直平地，墙只在两端，所以多数闭环不受影响。
⇒ 搜索要么**避开墙**，要么**补瓦片碰撞**。**这是下一个要解决的问题。**

**验证方式（按铁律）**：改动前后**复算结果逐位不变**
（96.0% / 4216 / 2269 / 14.5 / 0.0821…），说明重构没动复算路径；
新增的 `ROLLOUT` 测量则证明**新分支确实被执行**——旧 API 只能做 1 tick。


### D1. 观测配对差一 tick（第五轮，代价最大，**是一整类不是一处**）

密集观测是**原生更新之后**采样的 ⇒ 一行的控制位与 `justJumped` 描述的是
**进入该行**的那次更新。按当前行读，会**漏掉每一次起跳**——起跳那一 tick
只在**下一行**报告。后果：地面接触误差 0.317 → **6.21 px**。

**只修控制位还不够**：冲刺状态同样差一 tick ⇒ **每一次冲刺启动**
（`dashDelay` 0→-1）都被当成普通水平运动，vx 少算约 9 px/tick，
造成 **8.86 px 过冲**。按原生顺序重接后 → **0.300 px**。

⇒ **做法：逐字段过一遍"这个字段描述的是起点，还是刚刚那次更新？"**
（位置/速度 = 起点；控制位、`justJumped`、引擎写入的 `dashDelay`/`eocDash` = 刚发生的更新。）

### D2. `controlJump` 不足以判断起跳

自动跳、翅膀扇动、火箭都会在跳跃键为假时把玩家弹起。必须用引擎自己的
`justJumped`。只信控制位，会把一次弹射当成站立。

### D3. 枚举器：目标 bucket 被当成"已到达"

起始 bucket 若预置为"0 受击已到达"，而"闭合回路"正是回到该 bucket
⇒ **目标被支配剪枝删掉**，搜索永远返回空。

### D4. 枚举器：bucket 塌掉与目标有关的维度

目标是 `(位置, 时间)` 的区域；bucket 只按位置 ⇒ 目标在早一 tick 就被记为已到达，
同样被剪掉。**真实 bucket 必须保留一切与"是否满足目标"有关的维度。**

### D5. 切分：手写状态号 / 精确周期 / 比较跨度太长

见第 0 节。三次都是真实数据把规则否掉的。

⇒ **通用教训：在抱怨"搜不到"或"模型不准"之前，先问两件事**——
①我想搜/想预测的那个东西，当前空间/接口**能不能表达**？
②我是不是**在问一个错的问题**？
同类错误在本项目已出现多次（§52 冲刺时机不在搜索空间里、§D1 配对差一 tick）。

## C3. **评估器缺口彻底关闭：覆盖率 96.0% → 100.0%**

真因不是"缺物理常量"，而是**非翅膀分支在地面上拒绝一切跳跃动作**：
`if (!winged) { if (controls.Jump || frame.JustJumped) return Refuse(Jumping); }`
⇒ 枚举器 12 个动作里**有 4 个在地面 tick 上是死的**，**97% 的拒绝都是它**。
那句注释（"观测不携带多段跳充能状态"）**已过时**——现在携带
`canJumpAgain_Cloud` / `hasJumpOption_Cloud` / `isPerformingJump_Cloud`，且
`JumpMotion.ApplyJump` / `RefreshBeforeMovement` / `ResolveControl` 都在。

按 native 顺序补上（刷新充能 → 解析输入 → 冲量 → 重力）：

| 指标 | 改前 | 改后 |
|---|---|---|
| **覆盖率** | 96.0% (4216/4393) | **100.0% (4392/4393)** |
| 精确命中 | 2269 | 2284 |
| 干净 tick 平均误差 | 0.0821 px | **0.1025 px** |
| 地面接触最大误差 | 0.300 px | **0.300 px**（不变） |
| 最大位置误差 | 14.5 px | 14.5 px（贴墙） |
| **拒绝** | Jumping=176 | **0（无 REFUSED 行）** |

⇒ **这是"搜索空间把答案删掉了"的第三例**。一个**无法表达跳跃**的搜索空间，
其"找不到解"毫无信息量。**今后凡是搜索找不到解，先问：这个空间能表达解吗？**

## C4. `TraceLoopWorld`：枚举器第一次跑在真实闭环上

新增 `src/Chaite.Core/TraceLoopWorld.cs`（实现 `IRouteWorld`）：
玩家由 `PlayerForwardModel` 预测，威胁 = 录制的 BOSS 本体 + 敌对射弹；
目标 = **在闭环结束 tick 回到闭环起点**（**相对 BOSS** 而非绝对坐标，因为 BOSS 会移动）。

**关键设计**（直接对应 D 项的两类已踩错误）：
- `Bucket` **故意包含时间维度**：BOSS 脚本按 tick 索引，不同 tick 面对不同威胁，不可互换。
  同时含位置格(8px)与速度格(2px/tick)。用**规范化 id 表**而非打包哈希 ⇒ **两个不同状态
  不可能因哈希碰撞而同桶**。
- `DistanceToGoal` = **剩余 tick 数**（真实下界）+ 0.001×距离**仅作打破平局**，
  不可能声称某状态比剩余 tick 更少的更近。
- **拒绝而非猜测**：出闭环、威胁不完整、出场地边界，三种都返回 false。

**探针侧新增**：密集行现在记录 `hostileProjectiles` / `hostileProjectileCount` /
`omittedHostileProjectiles`（上限 48）。

## C5. ⚠️ **观测式威胁场在原理上无法判定命中**（本轮结论，已定案）

### 第一步：后置采样（原判定）——11 次命中，0 次判中

```
flagged=28  actualHits=11  matched=0  MISSED=11  falseAlarm=28
```

### 第二步：补录「更新前」射弹场 + 扫掠框 —— 只救回 5 次

探针已在 `BeforeUpdate()`（native update **之前**）记录
`hostileProjectilesBeforeUpdate`（最近优先、上限 48、截断可见）。
判定改用**更新前场 + 按速度展开的扫掠框**：

```
LOOP SEARCH SWEPT flagged=33 matched=5 MISSED=6 falseAlarm=28
ticksWithBeforeUpdateField=3688
```

### 第三步：剩下 6 次漏判的诊断——**决定性**

```
tick=2569 life=407 immuneTime=0 nearestBefore=277.4 nearestAfter=280.0 bossGap=245.0
tick=2609 life=337 immuneTime=0 nearestBefore=311.3 nearestAfter=313.2 bossGap=278.2
tick=2671 life=246 immuneTime=0 nearestBefore=351.1 nearestAfter=353.0 bossGap=318.0
tick=2711 life=329 immuneTime=0 nearestBefore=353.6 nearestAfter=355.5 bossGap=320.5
tick=2777 life=245 immuneTime=0 nearestBefore=375.0 nearestAfter=378.2 bossGap=343.2
tick=3519 life=27  immuneTime=0 nearestBefore=50.2  nearestAfter=55.8  bossGap=427.8
```

**最近的射弹在 50–375 px 外、BOSS 在 245–428 px 外、且 `immuneTime=0`。**
⇒ **造成伤害的东西，在两次采样里都根本不在射弹列表中。**

**原因：它在同一 tick 内生成并命中**，因此**从未出现在任何一次采样里**。
（`immuneTime=0` 也排除了"无敌帧内被记录为掉血"的解释。）

### ⇒ 定案结论

**观测式（从轨迹读取）威胁场在原理上无法判定命中**：造成命中的射弹
既不在后置列表（被命中移除），也可能不在前置列表（同 tick 内生成）。
**任何采样频率都救不了它——它存在于 tick 内部，而观测只在 tick 边界。**

⇒ **搜索的威胁模型必须是「生成式」而非「观测式」**：
从 BOSS 脚本状态**生成**射弹，再用 `HostileProjectileMotion`（已有）推进。
这正是目标书写的做法（"威胁模型（HostileProjectileMotion 已有）"），
而**不是**读轨迹。**在这条补上之前，搜索的"无伤"不可信。**

⇒ 副产品：28 次 falseAlarm 是"玩家在射弹框内但该 tick 未掉血"（无敌帧/宽限），
方向安全，可用于收紧判定。





## C7. **威胁模型的真正缺口：轨迹缺 `ThreatSnapshot` 的字段**（已定位到具体字段）

上一节说"必须用生成式威胁模型"。本轮把它落到了**具体字段清单**——
`HostileProjectileMotion` 消费的是 `ThreatSnapshot`（`src/Chaite.Core/Models.cs:590`），
而**轨迹并不携带它的字段**。

- **`ForProjectileType`**（`:102`）：920→`FallingHostileBolt`、921→`BouncingFallingHostileBolt`、384/385/386→猪鲨。

**已按 `ThreatSnapshot` 的字段清单补录**（探针 `CaptureHostileProjectiles`）：
`ai2` / `localAI0` / `localAI1` / `direction` / `scale`。

**踩坑记录**：`Projectile.ai` 只有 **3** 个元素（`ai[3]` 不存在，`NPC.ai` 才是 4 个）；
探针由 `prepare-game-probe.ps1` 用 csc 单独编译，**报错会被吞成 "Probe compile failed"**，
必须手动复现 csc 命令才能看到真正的 CS 错误。

⇒ **下一步**：用补录字段构造 `ThreatSnapshot` 驱动 `HostileProjectileMotion`，
替换 `TraceLoopWorld` 的 AABB 判定；跑校准，目标 `MISSED=0`。


## C8. 已审威胁模型已接入 `TraceLoopWorld`；剩余漏判的**真凶已定位**

### 做了什么

- `TraceProjectile` 增加 `Ai2`/`LocalAi0`/`LocalAi1`/`Direction`/`Scale`/`OldPos`。
- `TraceLoopWorld.TryBuildSnapshot`：按模型**自身的校验门**从录制字段重建 `ThreatSnapshot`。
- `TraceLoopWorld.OverlapsModeled`：用 `HostileProjectileMotion.TrySweep(snapshot,0,1)`
  的扫掠边界判定；**模型拒绝时回退到扫掠方盒**（不评分的射弹会被读成"干净 tick"，更糟）。
- 扫掠回退里补上 **`extraUpdates + 1`** 因子（native 每 tick 推进 `extraUpdates+1` 次，
  非光束；见 `TerrariaFacade:1824`）——此前我用的是裸速度。

### ★★★ 分支计数实测（**决定性** ✓✓）

给 `HorizontalMotion` 加了每分支计数器 + 进入速度记录 ✓（`BranchSummary()` ✓），审计打印：

```
ROUTE MODEL HBRANCH calls=962 brake=385 sprint=99 over=309 hold=16
                    coastOver=56 coastDecay=97
                    maxIncoming=9.0000 maxTop=8.0000
```

| 项 | 值 | 含义 |
|---|---|---|
| `calls` | 962 | 水平推进被调用 962 次 ✓ |
| `brake` | **385** | 40% 走"刹车"分支 ✓ |
| `over` | 309 | over-speed 分支**确实在跑** ✓（不是不可达 ✓）|
| **`maxIncoming`** | **9.0000** ✗ | **模型水平速度上限只有 9.0** ✓ |
| `maxTop` | 8.0000 | 上限（`max(baseRun, maxRun)` ✓）|

**⇒ 引擎在同一段达到 `12.8795`**（`OVERCAP tick=243` ✓），冲刺达 14.5 ✓
⇒ **模型从未达到引擎的速度** ✓✓
⇒ ⇒ **缺陷在 `HorizontalMotion` 的**上游** ✓✓ —— 实测确认，不再是推理 ✓

**⇒ 而 `over` 分支并非不可达** ✓ ⇒ 两次"无效果"的原因是：**上游先把速度压到 ≤9** ✓，所以改分支顺序自然无效 ✓✓（与实测完全一致 ✓）

### ⇒ 下一轮（顺藤摸瓜：找上游那个 9.0 的夹取）

**测量** ✓：在 `PlayerForwardModel` 的水平赋值处打印**每一步之后**的 `velocityX` ✓
⇒ 找出把速度压到 9.0 的那一行 ✓
⇒ 再按引擎实测修正 ✓

**判据**：`maxIncoming` 从 **9.0000** 升到 **≥ 12.88**（或引擎同段的值 ✓）；
`REGIME wing` 的 `mean` 从 **28.457** 降到 **< 2** ✓。

### ⇒ ★ 更正（**我上一轮的过度解读** ✓）

`ToSnapshot`（`PlayerForwardModel.cs:467`）**逐字段透传，无任何夹取** ✓
而 `maxIncoming` 是在 **`Advance` 入口**测量的 ✓
⇒ 而**冲刺路径**（`nextVelocityX = dash.VelocityX` ✓）**绕过 `Advance`** ✓
⇒ ⇒ **`maxIncoming = 9.0` 只是"普通路径"的最大值** ✓
⇒ ⇒ **它本身不能证明存在 9.0 的封顶** ✗ —— 这是我上一轮读错了 ✓

**⇒ 真正的待查项**（下一轮 ✓）：
1. **冲刺退出速度** ✓：模型 `dash.VelocityX` 结束时的值 vs 引擎 ✓
   （引擎冲刺 14.5 ✓，随后走 `Advance` 衰减 ✓ —— 模型这条链是否成立 ✓）
2. **重同步帧的路由** ✓：`BuildFrame` 带入引擎速度后 ✓，首帧若 `frame.Dashing` ✓
   ⇒ 走 dashing 分支 ✓ **不进 `Advance`** ✓ ⇒ 高速度被"绕开"✓

**判据**：普通路径的 `maxIncoming` 从 **9.0** 升到 **≥ 12.88** ✓；
`REGIME wing mean` 从 **29.002** 降到 **< 2** ✓。

### ⇒ 修正尝试 3（**"反向即只 bleed"**）**实测明显变差** ✗✗ ⇒ 已回退

| 指标 | 改前 | 改后 |
|---|---|---|
| `REGIME wing mean` | 29.002 | **48.848** ✗✗ |
| `REGIME rocket mean` | 78.271 | **158.352** ✗✗ |
| `REGIME dash mean` | 37.113 | **66.492** ✗ |
| `SINCE_RESYNC 80-more meanX` | 57.744 | **97.822** ✗✗ |

**⇒ 原因** ✓：该规则让模型**永远只 bleed、无法反向** ✗
⇒ 所以引擎**在反向时确实会加速**（某种条件下 ✓）
⇒ **该条件不是"输入的符号"** ✓✓

**⇒ 而测量本身仍然成立** ✓（`stepDelta = +0.1256 = runAccel` ✓）：
tick 252–257 那 6 帧引擎**只 bleed** ✓，而模型**多加了加速度** ✓
⇒ ⇒ **缺的是那个**阈值** ✓，必须**从轨迹里读出来** ✓
（不能从这几帧推理 ✓ —— 它们**全都在这条线之上** ✓）

### ⇒ 下一轮（**扫描轨迹找阈值** ✓）

**测量** ✓：遍历整条密集轨迹 ✓，对每一帧分类
—— 输入反向时，引擎是**加速**了还是**只 bleed** ✓
—— 同时记录当时的 `|vx|` / `maxRunSpeed` / `accRunSpeed` / `grounded` / `runAcceleration` ✓
⇒ 找出**分界速度** ✓（或发现区分量根本不是速度 ✓）

**判据**：打印出两类帧的**速度分布** ✓，能看出分界 ✓；
然后据此修正 ⇒ `HDELTA` 的 `stepDelta` → **0.0000** ✓；
`REGIME wing mean` → **< 2** ✓。

### ★★★ 阈值扫描（**分界正好在 `accRunSpeed = 8.0`** ✓✓）

遍历整条密集轨迹 ✓，对**输入反向**的每一帧 ✓，按 `|vx|` 分桶统计
—— 引擎是**只 bleed**（`step ≈ −0.4512` ✓）还是**别的** ✓：

| speed | frames | bleedOnly | other |
|---|---|---|---|
| 0–1 | 2 | **2** | 0 |
| 1–2 | 4 | **4** | 0 |
| 2–3 | 5 | **5** | 0 |
| 3–4 | 8 | 4 | 4 |
| 4–5 | 10 | 7 | 3 |
| 5–6 | 10 | 6 | 4 |
| 6–7 | 8 | **8** | 0 |
| 7–8 | 7 | 4 | 3 |
| **8–9** | 6 | **0** | **6** |
| **9–10** | 3 | **0** | **3** |
| 10–11 | 1 | **0** | 1 |
| 11–12 | 2 | **0** | 2 |
| 12–13 | 5 | **0** | 5 |
| 13–14 | 4 | **0** | 4 |

**`maxSpeed = 13.8381`** ✓

**⇒ 分界正好在 `accRunSpeed = 8.0`** ✓✓
- `|vx| ≤ 8.0` ⇒ 反向时**只 bleed** ✓（3–4/4–5/5–6/7–8 桶里的 `other` 是**反转到基础跑速以下**的帧 ✓）
- `|vx| > 8.0` ⇒ **永远走 over-speed 规则**（bleed + 乘性衰减 ✓）⇒ 与 C103 的逐位验证一致 ✓

### ⇒ 完整规则（**由轨迹实测得出** ✓）

```
|vx| > accRunSpeed   → over-speed：bleed + 乘性衰减（0.985/>12, 0.94/其余）   ← 不加速
|vx| ≤ accRunSpeed
    且 输入反向       → 只 bleed（无加速度）                                  ← 模型在此多加加速 ✗
    且 forward < baseSpeed → 刹车 + 加速（真正的反向）
    否则              → 冲刺 / 保持
```

**⇒ 模型的缺陷** ✓：在 `|vx| ≤ accRunSpeed` 且反向时**多加了 `+runAcceleration`** ✗
⇒ 即 **tick 252 的 `|vx| = 5.6744`**（≤8 ✓）⇒ 应只 bleed ✓ 而模型加了 0.1256 ✓✓

### ⇒ 下一轮（按这张表修正）

在"刹车"分支前**先判 `|vx| > accRunSpeed`** ✓，并在其内**先判 `forward < 0`（反向）⇒ 只 bleed** ✓
—— 但**必须保留** `|vx| ≤ baseSpeed` 时的刹车+加速 ✓（否则无法反向 ✓，这正是上次变差的原因 ✓）

**判据**：`HDELTA` 的 `stepDelta` → **0.0000** ✓；
`REGIME wing mean` 从 **29.002** → **< 2** ✓；
`SINCE_RESYNC 80-more meanX` 从 **57.744** 显著下降 ✓。

## C108. **★★★ 完整因果链（全部实测）：tick 248 的偏离是**冲刺晚结束 1 tick**，根因是模型里 `AccRunSpeed` 停在 6.0（引擎当刻 8.0）**

### 分支标记一次运行即定位 ✓

在 `HorizontalMotion` 每个分支写 `LastBranch` ✓，并在 `OPENLOOP` 行打印 ✓：

```
OPENLOOP tick=246 ... err=0.0001 branch=          ← Advance 根本没被调用 ✓
OPENLOOP tick=247 ... err=0.0001 branch=          ← 仍在冲刺路径 ✓
OPENLOOP tick=248 ... err=0.6225 branch=          ← 仍在冲刺路径 ✗
OPENLOOP tick=252 ... err=4.9528 branch=brake-oppose-nospeed accRun=0.0000
```

**⇒ tick 246–251 的 `branch` 是空的** ✓✓ ⇒ **值来自冲刺路径，不是 `HorizontalMotion`** ✓
⇒ 之前三轮都在改**错误的分支** ✗✓

### 冲刺状态逐 tick 对照 ✓

| tick | 引擎 | 模型 |
|---|---|---|
| 247 | `DashDelay=-1 Eoc=15 dashing=True` ✓ | 同 ✓ |
| **248** | **`DashDelay=30 Eoc=15 dashing=False`** ✓ | **`DashDelay=-1 dashing=True`** ✗ |
| 249 | `DashDelay=29 Eoc=14` ✓ | `DashDelay=-1` ✗ |

**⇒ 引擎在 248 进入 30 tick 冷却 ✓，模型还在冲刺** ✗ ⇒ **模型冲刺晚结束 1 tick** ✓✓

### ★ 根因（`GravityDashMotion.cs:479` ✓）

```csharp
var runSpeed = Math.Max(next.AccRunSpeed, next.MaxRunSpeed);
...
if (bled > runSpeed)  return ActiveLowSpeed;   // 冲刺继续 ✗
next.DashDelay = CooldownTicks;                // 冲刺结束 ✓
```

**tick 248 的算术** ✓：`|vx| = 8.2997` ✓，`bleed = 0.4512` ✓ ⇒ `bled = 7.8485` ✓
- `AccRunSpeed = 6.0`（模型 ✗）⇒ `runSpeed = max(6, 6) = 6` ⇒ `7.8485 > 6` **真** ⇒ 冲刺继续 ✗
- `AccRunSpeed = 8.0`（引擎 ✓）⇒ `runSpeed = max(8, 6) = 8` ⇒ `7.8485 > 8` **假** ⇒ **冲刺结束** ✓
  ⇒ `VelocityX = runSpeed = 8.0` ✓✓ **与引擎完全一致** ✓✓

**⇒ ⇒ 模型的 `AccRunSpeed` 停在**原点行的 6.0** ✗，而引擎的 `accRunSpeed` 是**实时**字段 ✓
（加速跑时升到 8.0 ✓）⇒ **前向模型必须跟踪它** ✓✓

### 状态

套件 **780 通过 0 失败** ✓（`HorizontalMotion` 的 `accRunSpeed > 0` 保护 ✓ + `BuildFrame` 赋值 ✓）
`AccRunSpeed` 已贯通 `PlayerMotionFrame` / `PlayerSnapshot` / `Models.PlayerSnapshot` ✓

### ⇒ 下一轮

**让模型跟踪实时 `accRunSpeed`** ✓ —— 它由引擎在"加速跑"时置 8.0 ✓
⇒ 需从轨迹确定其**派生规则**（何时 6.0、何时 8.0 ✓），并**实测验证** ✓

**判据**：`OPENLOOP tick=248` 的 `err` 从 **0.6225** → **≤ 0.0001** ✓；
首个 `err > 0.01` 的 tick 从 **248** 推迟到 **≥ 340** ✓（闭环长 58–110 ✓）。

## C107. **★★★ 开环对照成功：模型在 tick 239–247 **完全精确**（`err=0.0000` ✓），首次偏离**精确落在 tick 248** ⇒ 定位到"下限用错速度"**

### 开环模式（`CHAITE_AUDIT_OPENLOOP=1` ✓）

关掉审计的重同步 ✓，从**同一原点**逐 tick 对照引擎与模型 ✓：

```
OPENLOOP tick=239 engX=654.5000 modX=654.5000 engVX=14.5000 modVX=14.5000 err=0.0000
OPENLOOP tick=243 engX=707.3322 modX=707.3321 engVX=12.2419 modVX=12.2419 err=0.0001
OPENLOOP tick=247 engX=746.3199 modX=746.3198 engVX=8.2997  modVX=8.2997  err=0.0001
OPENLOOP tick=248 engX=754.3199 modX=753.6974 engVX=8.0000  modVX=7.3776  err=0.6225  ← 首次偏离
```

**⇒ 前 9 tick 位置误差 ≤ 0.0001** ✓✓ —— 冲刺、over-speed 衰减**全部正确** ✓
**⇒ 垂直方向全程精确** ✓（`engVY == modVY` ✓）

### ★ 首次偏离的算术

`modVX = 7.3776` ✓ 而 `(8.2997 − 0.4512) × 0.94 = 7.3776` ✓✓ **完全吻合** ✓
⇒ 模型做了 bleed + 0.94 衰减 ✓，**引擎把它夹到 `accRunSpeed = 8.0`** ✓✓
⇒ 且**下一帧**（从正好 8.0 出发）引擎给出 `7.5488 = 8.0 − 0.4512` ✓
⇒ ⇒ **阈值是严格大于** ✓：`|vx| > accRunSpeed` ⇒ 衰减 + 夹到 `accRunSpeed`；否则纯加性 bleed ✓✓

### ⇒ 本轮已落的修正

1. `AboveTopSpeed` 的夹取速度：`topSpeed`(6.0) → **`accRunSpeed`(8.0)** ✓（两处调用点 ✓）
2. 反向分支新增 `EyeShieldDashMotion.ApplyOpposingBrake` ✓（同上规则 ✓）
3. `PlayerMotionFrame` / `PlayerSnapshot` / `Models.PlayerSnapshot` 加 `AccRunSpeed` ✓
4. 测试 `BuildFrame` 两处赋 `AccRunSpeed = Player(row, "accRunSpeed")` ✓

**⇒ 实测效果**：tick 252+ 明显改善 ✓（`5.2159 → 5.5488` ✓，向 `6.5464` 靠近 ✓）
**⇒ 但 tick 248 仍是 `7.3776`** ✗ ⇒ 说明该帧的下限仍以 `accRunSpeed = 0` 施加 ✓

### ★ 保护（**必须** ✓）

`synthetic` 配置不携带 `accRunSpeed` ✓ ⇒ 若为 0 ✓，`|vx| > 0` **恒真** ✗ ⇒ 不该衰减处也衰减 ✗
（实测：`NativeMotionHonorsDebuffsAndReversalBraking` 期望 2.16 得 1.294 ✗，共 3 处失败 ✗）
⇒ ⇒ **`accRunSpeed <= 0` 视为"未知"** ✓ ⇒ 退回原行为 ✓（`ApplyOpposingBrake` 内 ✓ + 两处调用点 ✓）
⇒ 实测 **780 通过 0 失败** ✓

**⇒ 规则只在"轨迹实测场景"（`accRunSpeed > 0` ✓）生效** ✓，不影响合成配置 ✓

### ⇒ 下一轮（**一次运行即可定位** ✓）

在 `HorizontalMotion` 的每个分支写 `LastBranch` ✓，在 `OPENLOOP` 行里打印 ✓
⇒ 直接看 tick 248 走的是哪条分支 ✓、当时 `accRunSpeed` 是多少 ✓

**判据**：`OPENLOOP tick=248` 的 `err` 从 **0.6225** → **≤ 0.0001** ✓；
首个 `err > 0.01` 的 tick 从 **248** 推迟到 **≥ 340** ✓（闭环长度 58–110 ✓）。

### ★ 方法论收益（本轮最重要的成果）

**开环对照**是唯一能验证规则的测量 ✓✓ —— 它直接给出"前 9 tick 精确、第 10 tick 出错" ✓
⇒ 而轨迹行走审计只会给出被发散污染的聚合数 ✗（C106 ✓）
⇒ **后续所有模型改动都用这个模式判定** ✓

## C106. **★★★ 方法论结论：轨迹行走审计**无法验证**规则改动**（模型一发散，比较就被发散本身主导）⇒ 三条规则全部"更差"但都精确复现了轨迹里那几帧**

### 从轨迹里**精确解出**的完整规则（逐帧验算全中 ✓）

| tick | 前值 → 本值 | 规则 |
|---|---|---|
| 243 | 12.8795 → 12.2419 | (12.8795−0.4512)×**0.985** ✓ |
| 244 | 12.2419 → 11.0832 | (11.7907)×**0.94** ✓ |
| 247 | 9.2806 → 8.2997 | (8.8294)×**0.94** ✓ |
| **248** | 8.2997 → **8.0000** | 7.8485 ⇒ **夹到 `accRunSpeed`** ✓ |
| 249–257 | 7.5488 → 4.2904 | **纯加性 −0.4512，无衰减无夹取** ✓ |

**⇒ 分界正好在 `accRunSpeed = 8.000`** ✓，且两侧**所有其他字段完全相同** ✓
（`grounded=False` ✓、`jump` 时真时假 ✓、`wingAccRun=-1` ✓）

### ★ 但按这条规则改**实测全部更差** ✗✗

| 尝试 | `wing mean` |
|---|---|
| 基线 | **28.457** |
| 输入符号 ⇒ 只 bleed | **48.848** ✗✗ |
| `\|vx\| > baseSpeed`(4.71) ⇒ 只 bleed | **31.392** ✗ |
| `\|vx\| > accRunSpeed`(8.0) ⇒ 只 bleed | **50.833** ✗✗ |

**⇒ 三条规则都**精确复现**了轨迹里那几帧 ✓，却都让整体更差 ✗✓**

### ⇒ ⇒ 根因（**方法论** ✓✓）

审计是"**沿轨迹逐帧行走**并比较模型与引擎" ✓
⇒ 模型一旦发散 ✓，它生成的"反向"帧就是**它自己的状态** ✓
⇒ 引擎在那些状态怎么走，**轨迹里根本没有** ✓
⇒ ⇒ **比较被"发散"主导，而不是被规则正确性主导** ✗✓

**⇒ 所以三个不同的规则给出三个不同的数字 ✓，与规则对错**无关** ✓
⇒ ⇒ **从轨迹里读出的规则，不能用"再走一遍轨迹"来验证** ✓✓

**⇒ 这也解释了为什么这个缺陷反复"修不好"** ✓ —— 不是规则难，是**验证手段错了** ✓

### ⇒ 下一轮（**换验证手段** ✓）

**必须用隔离探针验证模型** ✓：
- 固定一串控制序列 ✓，让**引擎**和**模型**各跑一遍 ✓
- 比较**轨迹**（逐 tick 位置/速度 ✓），而不是比较聚合误差 ✓
- 这样才能在模型发散**之前**的窗口里判定规则对错 ✓

**并且**：实测显示模型只在**约 10 tick**内可信 ✓
（`SINCE_RESYNC 0-9 mean=2.245` ✓，而 `20-29 mean=19.695` ✗）
⇒ 闭环长 58–110 tick ✓ ⇒ **当前模型不足以支撑闭环内枚举** ✗
⇒ ⇒ 这是**必须先解决的前置条件** ✓

### 状态

基线已精确恢复 ✓（`wing mean=28.457 max=175.839` ✓、`rocket mean=89.213` ✓）
套件 **780 通过 0 失败** ✓
本轮的 `AccRunSpeed` 管线（`Models.cs` / `PlayerForwardModel.cs` ✓）与计数器**保留** ✓（中性 ✓、对下一轮有用 ✓）

## C105. **★★★ 根因（有算术证明）：输入反向且速度高于基础跑速时，引擎**只 bleed 不加速**；模型却每 tick 多加一次 `+runAcceleration`（0.1256）⇒ 355 帧累积 ≈ 28 px**

### 先排除"9.0 封顶"（**我的过度解读已彻底否证** ✓）

```
ROUTE MODEL HSPEED calls=1068 dashStart=11 dashContinue=95 ordinary=962
                    maxEntry=14.5000 maxExit=14.5000
```

**⇒ 模型**确实**携带引擎的冲刺速度 14.5** ✓✓ ⇒ **没有 9.0 封顶** ✓
⇒ `maxIncoming=9.0` 只是**冲刺衰减后交给普通路径**的值 ✓（冲刺路径绕过 `Advance` ✓）

### ★ 改用**逐 tick 步长对照**（此前比速度**水平**，把累积混了进来 ✗）

```
HDELTA tick=251 engineVX=6.9976 engineStep=-0.4512 modelVX=6.0000 modelStep=-0.0261 stepDelta=+0.4251
HDELTA tick=252 engineVX=6.5464 engineStep=-0.4512 modelVX=5.6744 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=253 engineVX=6.0952 engineStep=-0.4512 modelVX=5.3488 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=254 engineVX=5.6440 engineStep=-0.4512 modelVX=5.0232 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=255 engineVX=5.1928 engineStep=-0.4512 modelVX=4.6976 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=256 engineVX=4.7416 engineStep=-0.4512 modelVX=4.3720 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=257 engineVX=4.2904 engineStep=-0.4512 modelVX=4.0464 modelStep=-0.3256 stepDelta=+0.1256
HDELTA tick=258 engineVX=4.5416 engineStep=+0.2512 modelVX=4.1720 modelStep=+0.1256 stepDelta=-0.1256
```

**★ `stepDelta = +0.1256` 恰好等于 `runAccel = 0.1256`** ✓✓（表里同时打印了该字段 ✓）

**⇒ 算术证明**（tick 252 ✓，`velocity = 5.6744` ✓，`dir = -1` ✓，`left = True` ✓）：
```
forward = 5.6744 × (-1) = -5.6744  <  baseSpeed   ⇒ 走"刹车"分支 ✓
forward += slowdown      ( -5.6744 < -0.2 )       → -5.4744
forward += acceleration  (0.1256)                 → -5.3488   ← 引擎**不做**这一步 ✗
velocity = -5.3488 × (-1) = +5.3488               ⇒ step = -0.3256 ✓（实测一致 ✓）
```

**⇒ 引擎的 step 是 `-0.4512` = 纯 `OpposingSpeedBleed`** ✓✓

### ⇒ 根因（一句话）

**当输入反向、且速度已高于基础跑速时，引擎只 bleed、不施加加速度** ✓
⇒ 而模型**每 tick 都加一次 `+runAcceleration`** ✗
⇒ `+0.1256 / tick` × 355 帧 ≈ **28 px** ✓✓ = `REGIME wing mean = 28.457` ✓

**⇒ 这也解释了为什么前两个候选"无效果"** ✓：
两次改的都是这个分支 ✓，但当时**下限夹取还在** ✓
⇒ 夹取立刻把结果推回 `topSpeed` ✓ ⇒ 掩盖了真正的差异 ✓✓

### ⇒ 下一轮（修正 + 立刻对照）

**修正** ✓：输入反向且 `|velocity| > baseSpeed`（基础跑速 ✓）时 ⇒ **只 bleed，不加加速度** ✓
（即在 `forward < baseSpeed` 分支内，先判断"是否仍在基础跑速之上" ✓）

**判据**（铁律要求的对照 ✓）：
`HDELTA` 的 `stepDelta` 从 **+0.1256** 变为 **0.0000** ✓；
`REGIME wing mean` 从 **28.778** 降到 **< 2** ✓；
`SINCE_RESYNC 80-more` 的 `meanX` 从 **57.520** 显著下降 ✓。

## C104. **去掉了 `AboveTopSpeed` 的**下限夹取**（实测执行 ✓）；`max`/`rocket` 改善 ✓ 但 `wing mean` 略差 ✗ ⇒ **`maxIncoming` 仍是 9.0** ⇒ 上游另有一个 9.0 封顶**

### 从轨迹实测出的**正确规则** ✓✓（逐 tick 逐差验证 ✓）

| 转换 | `speed = \|vx\| − bleed` | 实测 delta | 规则 |
|---|---|---|---|
| 12.8795 → 12.2419 | 12.4283 **> 12** | ×0.985 | 12.2419 ✓ |
| 12.2419 → 11.0832 | 11.7907 **> 8** | ×0.94 | 11.0833 ✓ |
| 8.0000 → 7.5488 | 7.5488 **≤ 8** | ×**1.0** | 7.5488 ✓ |
| 7.5488 → 7.4488 | 7.3488 ≤ 8 | ×1.0 | 7.4488 ✓ |
| 7.4488 → 6.9976 | 6.9976 ≤ 8 | ×1.0 | 6.9976 ✓ |
| … → 4.2904 | 全部 ≤ 8 | ×1.0 | ✓ **全部吻合** |

⇒ **`decay = 0.985 (>12) / 0.94 (>accRunSpeed) / 1.0 (其余)`** ✓
⇒ **且没有 `accRunSpeed` 下限** ✓ —— 引擎从 8.0 **一路衰减到 4.29** ✓✓
⇒ 而模型 `AboveTopSpeed` 里写着 `if (magnitude < topSpeed) magnitude = topSpeed;` ✗
⇒ **这正是 `modelVX = 6.0000`（tick 251）的来源** ✓✓

### 修改与实测（`AboveTopSpeed` 去掉下限 ✓）

| 指标 | 改前 | 改后 | |
|---|---|---|---|
| `HBRANCH sprint` | 99 | **123** | ✓ 分支确实变了 ⇒ **改动执行** ✓ |
| `HBRANCH over` | 309 | **299** | ✓ |
| `HBRANCH hold` | 16 | **2** | ✓ |
| `REGIME wing mean` | 28.457 | **29.002** | ✗ 略差 |
| `REGIME wing max` | 175.839 | **158.059** | ✓ |
| `REGIME rocket mean` | 89.213 | **78.271** | ✓ |
| **`maxIncoming`** | 9.0000 | **9.0000** | ✗ **未变** |

**⇒ 保留此改动** ✓：它依据的是**轨迹实测的引擎事实**（无下限 ✓），且三项指标中两项改善 ✓
（铁律的判据是"完全相同即未执行" ✓ —— 此处计数器与指标**都变了** ✓ ⇒ 确实执行 ✓）

### ⇒ 剩余的主项：**上游那个 9.0 封顶** ✗

`maxIncoming` 在 `Advance` 入口测量 ✓ ⇒ `frame.Velocity.X` **从未超过 9.0** ✓
而引擎同段达 **12.88**（冲刺 14.5 ✓）
⇒ 封顶在 `PlayerForwardModel` 或 `ToSnapshot` 一侧 ✓，**不在** `HorizontalMotion` ✓

### ⇒ 下一轮

**测量** ✓：在 `PlayerForwardModel.TryAdvance` 的**每个**水平赋值点打印 `velocityX` ✓
（含 `dash.VelocityX` 两条路径 ✓ 与普通路径 ✓）
⇒ 找出把值压到 ≤ 9.0 的那一处 ✓

**判据**：`maxIncoming` 从 **9.0000** 升到 **≥ 12.88** ✓；
`REGIME wing mean` 从 **29.002** 降到 **< 2** ✓。

## C103. **★★★ 找到水平漂移的**根因**：模型把速度**钉在 `topSpeed` 上**，而引擎会**穿过它继续衰减**；且模型携带的 `MaxRunSpeed` 是**过期的 6.0**（引擎当刻是 8.0）**

### 逐 tick 实测（`ROUTE MODEL HORIZ`，原点附近 ✓）

```
HORIZ tick=248 engineVX=8.0000 modelVX=7.3776 delta=-0.6224 left=T dir=-1 grounded=F
              accRun=8.000 maxRun=6.000 runAccel=0.1256 sprintAccel=0.0502 runSlowdown=0.2000
HORIZ tick=249 engineVX=7.5488 modelVX=6.5108 delta=-1.0380 left=T
HORIZ tick=250 engineVX=7.4488 modelVX=6.0261 delta=-1.4227 left=F
HORIZ tick=251 engineVX=6.9976 modelVX=6.0000 delta=-0.9976 left=T   ← 模型**恰好钉在 6.0000** ✗
HORIZ tick=252 engineVX=6.5464 modelVX=5.6744
HORIZ tick=253 engineVX=6.0952 modelVX=5.3488
HORIZ tick=254 engineVX=5.6440 modelVX=5.0232
HORIZ tick=255 engineVX=5.1928 modelVX=4.6976
HORIZ tick=256 engineVX=4.7416 modelVX=4.3720
HORIZ tick=257 engineVX=4.2904 modelVX=4.0464
```

### ★ 两条实测结论

**① 引擎的衰减穿过 `topSpeed` 继续走** ✓✓

引擎 `8.0000 → 7.5488 → 7.4488 → 6.9976 → 6.5464 → 6.0952 → 5.6440 → 5.1928 → 4.7416 → 4.2904` ✓
逐差：**−0.4512**（输入反向 ✓）/ **−0.1000**（无输入 ✓）✓✓
⇒ **完全就是 `OpposingSpeedBleed = 0.4512` 与 `DashSpeedBleed = 0.1`** ✓
⇒ 且**没有任何 `topSpeed` 夹取** ✓ —— 引擎一路降到 4.29 ✓
⇒ **而模型在 6.0000 处被钉住** ✗（`topSpeed = max(baseSpeed, MaxRunSpeed)` ✓）

**② 模型携带的 `MaxRunSpeed` 是过期的** ✓✓

`maxRun=6.000`（模型 ✓）而 `accRun=8.000`（引擎当刻 ✓）✗
`BuildFrame` 里确实写了 `Math.Max(maxRunSpeed, accRunSpeed)` ✓（`ForwardModelTraceTests.cs:293` ✓）
⇒ 但**模型帧的 `MaxRunSpeed` 是从**重同步那一刻**继承的** ✓
⇒ 那一刻的 `accRunSpeed ≤ 6` ✓ ⇒ 之后**永不更新** ✗

### ⇒ 根因（一句话）

**模型把 `topSpeed` 当成了"速度上限"，而引擎只在**加速**时用它；已获得的速度靠 `bleed` 衰减，会穿过它** ✓✓

⇒ 这正是 `REGIME wing mean=28.457` 的来源 ✓
⇒ 也解释了为什么改 `HorizontalMotion` 的**分支顺序**无效 ✓（问题不在分支，在**夹取语义** ✓）

### ⇒ 下一轮（按实测修正，然后立刻用审计验证）

**修正** ✓：已获得的速度在输入下**不再被 `topSpeed` 夹住** ✓
—— 即 `forward ≥ baseSpeed` 且 `|velocity| > topSpeed` 时走 `AboveTopSpeed` ✓（含**衰减到 `topSpeed` 以下**的每一帧 ✓）

**判据**（铁律要求的对照 ✓）：
`HORIZ` 里 `modelVX` 在 tick 251 不再是 **6.0000** ✓；
`REGIME wing` 的 `mean` 从 **28.457** 降到 **< 2** ✓；
`maxIncoming` 从 **9.0000** 升到 **≥ 12.88** ✓。

## C102. **两个候选修正**都实测无效果**（28.457 → 28.421 / 28.422）⇒ 按铁律**两次都未执行** ✗ ⇒ 结论：**超速值根本没进到 `HorizontalMotion`** ⇒ 下一轮必须**实测哪条分支在执行**（不许再猜）**

### 候选 1：`else` 分支改为"保持"（不衰减）

理由 ✓：`HorizontalMotion.Advance` 第 36–58 行 ✓
`baseSpeed ≤ forward < topSpeed` 且 `sprintAcceleration == 0` 时落到 `else` ⇒ 往 0 衰减 ✗
而引擎注释说"空中输入**维持**已获得的速度" ✓

**实测** ✗：`REGIME wing` mean **28.457 → 28.421** ✓（基本相同 ✓）
⇒ **未执行 / 非原因** ✗

### 候选 2：把 over-speed 判据**提到最前**（按速度而非按方向相对量）

理由 ✓（看起来很强 ✓）：
`OVERCAP tick=243`：`velocity=+12.8795` ✓、`input=-1`（**反向** ✓）
⇒ 模型算 `forward = velocity × direction = −12.8795` ⇒ `forward < baseSpeed` ⇒ 走**刹车**分支 ✗
⇒ 而引擎走的是 **over-speed 衰减** ✓（12.4283 × 0.985 = 12.2419 ✓ 逐位吻合 ✓）

**实测** ✗：mean **28.421 → 28.422** ✓（完全相同 ✓）
⇒ **也未执行** ✗✓

### ⇒ 两次都无效果**是重要的实测信息** ✓

**⇒ 说明 `Math.Abs(velocity) > topSpeed` 在调用点几乎从不成立** ✓
⇒ 即 **`velocity` 在进入 `HorizontalMotion.Advance` 时已经 ≤ `topSpeed`** ✓
⇒ 而 `OVERCAP` 里的 12.88 是**引擎**的值 ✓
⇒ ⇒ **模型这一侧的速度根本没到过 12.88** ✗ ⇒ 缺陷在**上游**（`PlayerForwardModel` 的水平赋值 / `ToSnapshot` 的常量 ✓），**不在** `HorizontalMotion` 的分支顺序 ✓

### ★★ 过程教训：**增量构建会留下陈旧二进制** ⇒ **回退后必须干净重建** ✓✓

回退 `HorizontalMotion.cs` 后测试出现 `NativeMotionHonorsDebuffsAndReversalBraking:
expected 1.15, actual 1.2` ✗
但 `git diff 639361f -- src/Chaite.Core/HorizontalMotion.cs` **为空** ✓ ⇒ 源码与已知良好提交**逐字节相同** ✓
⇒ 结论：**`Chaite.Core.dll` 是陈旧的** ✗（增量构建没吃到回退 ✓）
⇒ **干净重建**（删 `obj`/`bin` ✓）后 ⇒ **780 通过 0 失败** ✓✓

**⇒ 这条必须记住** ✓：任何**回退**之后都要干净重建 ✓
否则后续测量可能对着**旧二进制**做 ⇒ 会得出**错误的"未执行"结论** ✓✓

### 干净重建后**基线精确复现** ✓（证明回退干净、两次候选测量真实 ✓）

```
REGIME wing   frames=355 over4=243 mean=28.457  max=175.839   ← 与改动前完全相同 ✓
REGIME rocket frames=13  over4=13  mean=89.213  max=145.203
REGIME dash   frames=49  over4=37  mean=36.817  max=107.669
SINCE_RESYNC 0-9   mean=2.245 meanX=2.245 meanY=0.000
SINCE_RESYNC 80-more mean=58.372 meanX=57.165 meanY=5.178
```

### ⇒ 两处改动**全部回退** ✓（不保留未经验证的物理改动 ✓）

### ⇒ 下一轮（**先测量分支执行情况**，这是铁律要求的）

在 `HorizontalMotion.Advance` 内加**每分支计数器** ✓，并**打印**
`velocity` / `topSpeed` / `baseSpeed` / `forward` / `sprintAcceleration` / `grounded` ✓
⇒ 得到"哪条分支被走了多少次" ✓ + "进入时速度是多少" ✓
⇒ 由此定位上游 ✓

**判据**：能打印出各分支的命中次数 ✓，且**进入时的 `velocity` 分布** ✓；
若 `velocity` 上限远低于 12.88 ⇒ 上游就是缺陷所在 ✓。

## C101. **定位到水平漂移的**具体代码路径**：`HorizontalMotion.Advance` 在 `baseSpeed ≤ forward < topSpeed` 且 `sprintAcceleration == 0` 时**错误地衰减**；而引擎的 over-speed 规则已被 `OVERCAP` 逐位证实精确**

### 引擎规则**逐位验证精确** ✓✓（审计已有的 `OVERCAP` 诊断 ✓）

```
OVERCAP tick=243 grounded=False accRunSpeed=8.00 engineVX=12.8795->12.2419 d=-0.6376 ratio=0.950493 input=-1
OVERCAP tick=244 grounded=False accRunSpeed=8.00 engineVX=12.2419->11.0832 d=-1.1586 ratio=0.905354 input=-1
OVERCAP tick=245 grounded=False accRunSpeed=8.00 engineVX=11.0832->10.3242 d=-0.7590 ratio=0.931519 input=0
```

**按文档规则手算对照** ✓：

| tick | vx | input | 规则：`bleed` 后 `speed`，`speed>12 ? ×0.985 : ×0.94` | 实测 |
|---|---|---|---|---|
| 243 | 12.8795 | −1（反向 ⇒ bleed=0.4512）| 12.4283 >12 ⇒ ×0.985 = **12.2419** | **12.2419** ✓✓ |
| 244 | 12.2419 | −1 | 11.7907 ≤12 ⇒ ×0.94 = **11.0833** | **11.0832** ✓✓ |

⇒ **规则本身精确** ✓ ⇒ 28 px 误差**不是规则错** ✓，而是**该分支没走到这条规则** ✗

### ★ 具体缺陷（`src/Chaite.Core/HorizontalMotion.cs` 第 36–58 行 ✓）

```csharp
if (direction != 0) {
    if (forward < baseSpeed)                                { ...加速... }
    else if (forward < topSpeed && sprintAcceleration > 0f) { ...冲刺... }
    else if (forward > topSpeed)                            { AboveTopSpeed(...) }
    else velocity = MoveTowards(velocity, 0f, drag);   // ← 衰减
}
```

**⇒ 当 `baseSpeed ≤ forward < topSpeed` 且 `sprintAcceleration == 0`** ✓
⇒ 落到最后一个 `else` ⇒ **把速度往 0 衰减** ✗
⇒ 而引擎在这个区间**保持速度** ✓（`OVERCAP` 那几帧正是 12.88 → 8.00 的**超速**段 ✓）

⇒ `topSpeed = max(baseSpeed, MaxRunSpeed)` ✓，而 `MaxRunSpeed` 在 `BuildFrame` 里已被设为
`max(maxRunSpeed, accRunSpeed) = max(4.71, 8.00) = 8.00` ✓ ⇒ 区间 `[4.71, 8.00)` 正是**空中展翼巡航**的常态 ✓✓

### ⇒ 下一轮（先测量，再改）

**测量** ✓：在该 `else` 分支加计数器 ✓，输出 `forward` / `topSpeed` / `sprintAcceleration` / `grounded` / `CanSprintInAir` ✓
⇒ 确认落进该分支的帧数与引擎的 `dvx` ✓
**然后**按实测修正（引擎在该区间保持 ✓，不是衰减 ✓）

**判据**：`REGIME wing` 的 `mean` 从 **28.457** 降到 **< 2** ✓；
`SINCE_RESYNC 80-more` 的 `meanX` 从 **57.165** 显著下降 ✓

## C100. **重同步已修好并**确实执行**（`resynced=4310`、路线 MD5 改变 ✓），但它**不是正确的修正** ✗；引擎 `hits=8` ✗；诊断定位到**水平漂移的主犯是 `wing` 分支**（mean 28.5 px）**

### 修好 C96 的门控（**原因已被读取+实测双重确认** ✓）

`segment.EndTick` 是**威胁索引** ✓（`loop-segments.csv` 的 `endTick` ✓）
`frame.Tick` 是**轨迹 tick** ✓
⇒ 两者**永不可能相等** ✓✓ ⇒ 门控从未触发 ✓

**实测打印** ✓：
```
LOOP COMPOSE TICK index=0 frameTick=344 sliceFirst=240 sliceEndTick=344 endIndex=104
```
⇒ 旧门控比的是 **344 vs 104** ✗✓ **确认** ✓

**修正后** ✓：门控改为 `frame.Tick == slice[last].Tick` ✓
```
LOOP COMPOSE segments=51 crossed=51 failedAt=-1 composedTicks=4155 composedHits=3
            replayRefusals=0 complete=True widest=48 routeTicks=4154 replayedHits=0
            resynced=4310
```
路线 MD5：`0E65063E...` → **`CE7D06351DD9D27F5F4E4089C99A5A32`** ✓
⇒ 按铁律，**改动确实执行了** ✓✓

### ⇒ 但引擎裁决：**远未无伤** ✗

```
RUN=game-probe-rt-rp201-0919-072532
status=loss ticks=1306 hits=8 deaths=1 bossLife=61605 damage=16395 shots=3
```

| | 模型自报 | 引擎实测 |
|---|---|---|
| 无重同步 | `composedHits=0` ✗**假** | `hits=9 deaths=1` |
| 有重同步 | `composedHits=3` | **`hits=8 deaths=1`** ✗ |

⇒ 重同步后模型自报从 0 变 3 ✓ ⇒ **原先的"零受击"是外推漂移造成的假象** ✓
⇒ `ticks=1306` 而路线 4154 tick ✓ ⇒ **在 1/3 处就死了** ✗

### ⇒ 重同步**不是正确的修正** ✗（方法论问题）

它注入的是**录制轨迹**的状态 ✓
而组合实际飞的是**另一条路线** ✗
⇒ 这是**让模型以为自己在录制的路径上** ✓，**不是**修正 ✓
⇒ 只有在路线与录制控制**一致**时重同步才合法 ✓

### ★ 诊断：水平漂移的**主犯是 `wing` 分支** ✓✓

审计（对照本轮新探针轨迹 ✓）：

| regime | frames | over4 | **mean 误差** | max |
|---|---|---|---|---|
| **wing** | **355** | **243** | **28.457** ✗ | 175.839 |
| rocket | 13 | 13 | 89.213 ✗ | 145.203 |
| dash | 49 | 37 | 36.817 ✗ | 107.669 |
| ground / air | 0 | 0 | 0.000 ✓ | 0.000 |

`firstOver4Tick=251 firstOver4Regime=wing` ✓ ⇒ 原点(240)后 **11 tick** 就超 4 px ✓
**`meanY ≈ 0`** ✓ ⇒ **垂直精确** ✓；误差**全在水平** ✓

### ⇒ 下一轮（唯一正确的方向：修模型，不是补丁）

**按项目方法实测 `wing` 分支的水平规则** ✓：
逐 tick 取引擎的 `dvx` ✓，与模型对比 ✓，**找出差异项** ✓
（物理常量必须引擎内实测，不得推算 ✓）

**判据**：`REGIME wing` 的 `mean` 从 **28.457** 降到 **< 2** ✓；
随后 `SINCE_RESYNC 80-more` 的 `meanX` 从 **57.165** 显著下降 ✓；
再谈组合与探针 ✓。

## C99. **★★★ 突破：闭环内零受伤枚举**跑通了**（`goals = keep`，`truncated=False`）；根因是目标判据用错 —— 闭环按 **BOSS 脚本周期**切，目标是 **`SurviveToEnd`** 而非"玩家回到原位置"**

### 加入带评分的逐层截断 ✓（使用者第 2、3 点 ✓）

`RouteEnumerator.Exhaustive(world, start, stepBudget, stateCeiling, keepPerDepth, score)` ✓

| 设计 | 理由 |
|---|---|
| **每层按评分保留前 N** ✓ | 丢弃**整个状态** ✓，**不合并** ✓ —— bucket 合并正是 D 类"把答案删掉"的来源 ✓ |
| **评分函数与权重由调用方注入** ✓ | Core **只枚举** ✓；"好路线长什么样"不在 Core 决定 ✓（符合铁律 ✓） |
| **`Dropped` 如实上报** ✓ | 被截断的扫描必须自认是 beam ✓ |
| **上限在每层内部检查** ✓ | 单层爆掉就管不住 ✓ |

**评分特征**（调用方构造 ✓）：`-DistanceToGoal` ✓ / 最近弹幕间距 ✓ / 最近 BOSS 间距 ✓ / 剩余翅膀 ✓
—— 后两者就是"**以及后续**" ✓（现在安全但翅膀耗尽的状态，价值低于同样安全且还能动的 ✓）

### ★ 根因：目标判据用错（**两处**）

1. **`state.Tick` 必须是轨迹 tick** ✓（世界用 `state.Tick - _firstTick` 索引切片 ✓）
   ⇒ 设为 0 使**根部 12 个动作全部 `PastLoopEnd`** ✗
2. **★ 目标是 `SurviveToEnd`** ✓✓ —— 闭环是按 **BOSS 攻击脚本周期**切的 ✓
   （方法原文："把 BOSS 每个阶段的循环状态各自视为一个闭环" ✓）
   ⇒ 而 `IsGoal` 默认要求玩家**回到 boss 相对的原位置** ✗
   ⇒ 实测：引擎自己认为干净的 loop 220，观测路线终点 `relX=371.9 relY=42.8` ✓ —— **差 372 px** ✗
   ⇒ 所以**没有任何状态能满足位置判据** ✓ ⇒ `goals=0` ✓
   ⇒ 打开 `SurviveToEnd`（第 597 行已存在 ✓）后 ⇒ **`reached=True`** ✓

### ⇒ 实测（**全部 `truncated=False`** ✓）

| 闭环 start | ticks | states | expansions | **goals** | dropped |
|---|---|---|---|---|---|
| 220 | 58 | 14418 | 169956 | **256** | 45926 |
| 162 | 58 | 14336 | 168972 | **256** | 45266 |
| 0 | 104 | 26045 | 309480 | **256** | 84353 |
| 404 | 110 | 55338 | 657924 | **512** | 139497 |

**⇒ `goals = keep`** ✓ —— 每个活到循环末尾的干净状态都是目标 ✓
⇒ **"一个循环内无伤"已经可以被枚举出来** ✓✓（方法的第②步 ✓）

**⇒ 且 `EXHAUSTIVE OBSERVED reached=True hits=0 refusals=0`** ✓ —— 观测路线也到达 ✓

### ⇒ 下一轮（使用者第 2、3 点的后半）

**目标不只是"活过本循环"，还要"为下一个循环留好局面"** ✓
⇒ 评分必须加权**后续** ✓ ⇒ 而**权重须由优化器给出** ✓（不得手写 ✓）
⇒ 然后把逐闭环选出的路线**接续**成整场 ✓

**判据**：整场组合后 `goals` 逐闭环非空 ✓；
引擎探针 `hits == 0` 且获胜 ✓。

## C98. **★★★ 决定性实测：闭环**不可穷尽**（前沿每 tick ×4.6，10 tick = 171 万状态；`refused=0`）⇒ 使用者的前提被数据修正，而使用者的**第二条指示**正是解法**

### 先看使用者放置的 JEV 源码（`TerraBlind-main.zip` ✓）

**⇒ "JEV" 不是学出来的策略** ✓ —— 它是**远程 LLM API** ✓
（`https://api.typesafe.ai/v1/systemone` ✓, `model = "jev-latest"` ✓）
回答**离散选择题** ✓：`Fight` / `Ignore` / `Flee` / `WallOff` / `Heal` ✓

**★ 它的架构才是可学的部分** ✓（`Jev\Fight\Dodge.cs` 开头原文 ✓）：

> `// Jev 给【意图】,不给按键。按键由下面那个每帧跑的反射层算`
> `// boss 战的走位。【两层】:Jev 每 200ms 说"该拉开还是该贴脸",反射层每帧算`
> `// "这一刻往左还是往右、跳不跳"。让 250ms 的判断直接当按键,就是站着挨撞`

| 层 | 周期 | 职责 |
|---|---|---|
| **慢层（意图）** | ~200 ms ✓ | `DodgeAct{Keep,Back,Close,Evade,Up,Float,Grapple,Orbit,Dive}` + **连续 `Danger`**（0=贴脸 4=远 ✓） |
| **快层（反射）** | **每帧** ✓ | 意图 → **实际按键** ✓；**每帧重算方向** ✓ |

**工程手法（直接可用 ✓）**：
- **绝不阻塞主线程** ✓：后台发请求 ✓，用**上一次**的结果 ✓；置信 **<0.6 退回手写 baseline** ✓
- **`FramesToHit(p, boss)`** ✓：每帧算"还有几帧撞上" ✓，`<= soon` 就跳 ✓（不等下一个意图 ✓）
- **弹幕也算"快被打中了"** ✓（`ThreatScan.SoonestHit` ✓）
- **撞墙就换边** ✓："哪边空是算得出来的,不用猜" ✓
- **`BossBook` 逐 boss 参数表** ✓（`WantCellsFor` / `IsBanned` ✓）—— 与"每套配装一个独立模型"同构 ✓
- **意图过期（1.5 s）退回保守** ✓；**被禁的意图退回 `Keep` 而非 `Back`** ✓

**⇒ 对拆特的映射** ✓：慢层 = **闭环/路线选择** ✓；快层 = **每帧选评分最高的候选并固定** ✓
—— **正是使用者本轮第 3 点** ✓✓

### 然后落实 C97 的阻塞点：廉价状态键 ✓

`StateKey` 从**格式化字符串**改为 **packed struct**（8 个 int + `IEquality`/`GetHashCode` ✓）
并把上限检查移到**每层内部** ✓（原来只在整层建完后检查 ⇒ 单层爆掉就管不住 ✓）

### ⇒ 实测结果（**决定性** ✓）

```
EXHAUSTIVE start=0 budget=20 states=3000002 expansions=10747404 refused=0 widest=1716274 truncated=True
EXHAUSTIVE FRONTIER 7,30,152,758,3816,17309,72070,272081,917505,1716274
EXHAUSTIVE REFUSAL (无)   goals=0
```

| tick | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| 前沿 | 7 | 30 | 152 | 758 | 3816 | 17309 | 72070 | 272081 | 917505 | **1716274** |

**⇒ 每 tick ×4.6** ✓（去重后，远小于 12 ✓）
⇒ **10 tick 就有 171 万个不同状态** ✗
⇒ **精确状态枚举下，闭环不可穷尽** ✗✓ —— 30 tick 是 4.6^30 ✓

**⇒ 且 `refused = 0`** ✓✓ —— 前向模型**一个动作都不拒绝** ✓
⇒ 所谓"机动性约束"**没有提供任何剪枝** ✓
⇒ 真正把 12 降到 4.6 的是**状态去重** ✓，而它仍然是**指数**的 ✓

### ⇒ 结论：使用者的前提被**数据**修正 ✓，而使用者的**第二条指示**正是解法 ✓

使用者说 ✓：
> "在一个'循环'内…回到闭环原始状态的路线是**可穷尽的**" ✓
> "**如果需要简化枚举手法**，即需要一个**优化了的评分**，加权各种影响此步与后续的因素，并在**每一帧选择评分最高的路线以固定**" ✓

⇒ 实测证明**第 1 句不成立**（对**精确**状态空间 ✓）
⇒ 而**第 2 句正是为这种情况准备的** ✓ —— 简化**必须**是**评分** ✓，**不能**是 bucket 支配剪枝 ✓（那是 D 类"把答案删掉"的来源 ✓）

### ⇒ 下一轮（按使用者第 2、3 点）

**带评分的截断枚举** ✓：每层按**评分**保留前 N 个 ✓（而非按 bucket 合并 ✓）
评分须加权**本步 + 后续** ✓（后续可用 `DistanceToGoal` / 威胁余量 ✓），且**权重须被优化**（不得手写 ✓）

**判据**：前沿在每层被截到 N 后**可完整跑完一个闭环** ✓；
`goals > 0` ✓（当前为 0 ✓）；
再由隔离探针验证 `hits` ✓。

## C97. **按使用者指示实现**穷尽枚举器**（无 beam/无支配剪枝 + 精确状态去重 + 每层前沿报告）；首次实测：40 tick 闭环**耗尽内存** ✗（键太贵，尚不能定论）**

### 使用者本轮指示（原文要点）

1. **闭环内穷尽枚举**：机动性约束下，"回到闭环原始状态"的路线**是可穷尽的** ✓
2. 需要简化时 ⇒ 用一个**优化过的评分** ✓，加权"本步与后续"因素 ✓
3. **每一帧选择评分最高的路线以固定** ✓
4. 参考：JEV 模型击败大师模式肉前全 BOSS 的视频 ✓
   （`https://www.bilibili.com/video/BV1Boe26JEh4/` ✓；同时检索到
   `Reisenbug/TerraBlind` ✓ —— GitHub 抓取被本机网络策略挡住 ✗，未深入 ✓）

### 实现（`src/Chaite.Core/RouteEnumerator.cs`）

**`RouteEnumerator.Exhaustive(world, start, stepBudget, stateCeiling)`** ✓ +
**`ExhaustiveReport`** ✓：

| 特性 | 说明 |
|---|---|
| **全量 BFS** ✓ | 按 tick 逐层展开，**不用** `PriorityQueue`/`NodeOrder` ✓ |
| **无 beam** ✓ | 不调 `Trim` ✓ |
| **无支配剪枝** ✓ | 不查 `world.Bucket` ✓ —— 这正是 D 类"把答案删掉"的来源 ✓ |
| **精确状态去重** ✓ | `HashSet<string>`，键 = 位置/速度 0.01 px + 翅膀/火箭/冲刺/着地 ✓ |
| **受击即丢** ✓ | 已受击的局部路线**永远不可能**变成无伤路线 ⇒ 这是**目标本身**，不是启发式 ✓ |
| **机动性约束** ✓ | 由 `world.TryStep` 的**拒绝**自然剪枝 ✓ |
| **如实报告** ✓ | 每层 `Frontier` ✓、`States` ✓、`Expansions` ✓、`Refused` ✓、**`RefusalsByReason`** ✓、`Truncated` ✓ |

**CLI**：`--exhaustive-loop <trace> [startTick] [budget] [stateCeiling]` ✓

### 过程中修掉的**测试台** bug（重要 ✓）

起点帧的 `Tick` 必须是**轨迹的 tick** ✓
（世界用 `state.Tick - _firstTick` 索引切片 ✓）
⇒ 我一度把它设为 0 ✗ ⇒ **根部 12 个动作全部 `PastLoopEnd`** ✗
⇒ "全部拒绝的扫描"测的是**测试台**，不是玩家的机动性 ✓✓

诊断输出：`ORIGIN stateTick=240 sliceFirst=240 sliceCount=41 threatRowsTick=240` ✓

### ⇒ 首次实测结果：**40 tick 就耗尽内存** ✗

```
EXHAUSTIVE THREW type=System.OutOfMemoryException
EXHAUSTIVE THREW stack= 在 System.Text.StringBuilder.AppendFormatHelper
                        在 Chaite.Core.RouteEnumerator.Exhaustive
```

⇒ 爆在 **`StateKey` 的字符串分配** ✓（每个状态一个字符串 ✓）
⇒ **以 0.01 px 字符串键的形式，闭环不可穷尽** ✗
⇒ **但键的实现太贵** ✗ ⇒ **尚不能对"是否可穷尽"下结论** ✓✓

### ⇒ 下一轮（两步，先测量后评分）

1. **把状态键换成廉价形式** ✓：`struct` + `GetHashCode`（或 64 位打包 ✓），
   并**打印每层前沿规模** ✓ ⇒ 得到真实的 `Frontier` 曲线 ✓
   ⇒ 由此**判定**可穷尽性 ✓（而不是继续假设 ✓）
2. 若前沿可控 ⇒ 按使用者第 2、3 点加**优化过的评分** ✓ 与**每帧固定** ✓

**判据**：`EXHAUSTIVE FRONTIER` 能完整打印 ✓；
若前沿在几十 tick 内收敛（或有界 ✓）⇒ 可穷尽 ✓ ⇒ 转向评分 ✓。

## C96. **闭环边界重同步：已实现但**门控从未触发**（`resynced=0`，路线逐字节未变）⇒ 必须先实测 tick 关系**

### 动机（C95 ✓）

模型在原点**完全精确** ✓，但水平误差随 tick 累积 ✓（1.355 → 10.847 → **60.192** ✓）
⇒ 而**组合把 51 个闭环串起来一路外推** ✓ ⇒ 引擎 `hits=9` ✗
⇒ 按方法第②步 ✓：**每个闭环边界用引擎轨迹重同步** ✓

### 实现 ✓（`LoopSearchTraceTests.cs`）

```csharp
if (frame.Tick == segment.EndTick &&
    segment.EndTick >= 0 && segment.EndTick < threatRows.Count)
{
    frame = BuildFrame(threatRows[segment.EndTick],
        threatRows[segment.EndTick], floor, false);
    resynced++;
}
```

### ⇒ 但**门控从未触发** ✗（两次尝试都失败 ✓）

| 门控 | 结果 |
|---|---|
| `GoalRoutes[goal].Count == segment.Length + 1` | **`resynced=0`** ✗ |
| `frame.Tick == segment.EndTick` | **`resynced=0`** ✗ |

**⇒ 两次组合输出的路线 MD5 完全相同** ✓：
`0E65063E90FBE03B4C17EC27CB355B06` ✓ = 无重同步版 ✓
⇒ **按铁律，这是"改动未执行"** ✓✓ —— 必须如实报告 ✓

### 其它结果（本轮组合 ✓）

```
LOOP COMPOSE segments=51 crossed=51 failedAt=-1 composedTicks=4155 composedHits=0
            replayRefusals=0 complete=True widest=48 routeTicks=4212
            replayedHits=0 resynced=0
```

⇒ 组合本身**仍然零受击** ✓（与 C91 一致 ✓）⇒ **无回归** ✓

### ⇒ 下一轮（必须先测量，不得再猜）

**打印 `frame.Tick` 与 `segment.EndTick`**（前 5 个闭环即可 ✓）
⇒ 得出它们之间的**实际关系** ✓，再据此写门控 ✓。

**依据**：C94 的教训 ✓ —— 常量必须**实测** ✓。
本轮两次门控都是**推算**出来的 ✗ ⇒ 两次都没触发 ✓。

**判据**：`resynced` 从 **0** 变为 **51** ✓（或实测所得的正确值 ✓）；
随后 `20-29` 桶的 `meanX` 从 **60.192** 下降 ✓；
引擎 `hits` 从 **9** 下降 ✓。

## C95. **★★★ 原点已实测化（工具自己找 `replayFrame == 0` 的行）；模型首帧**完全精确**，`0-9` 桶 mean=1.355**

### 修复：`startTick = index - 1`（差一）

测量**本来就有** ✓（找 `plan.replayFrame == 0` 的行 ✓）
但审计用 `tick = startTick + step + 1` ✓ ⇒ `startTick = index` 使整条路线**晚一帧** ✗✓
⇒ **症状正是 C93 看到的"引擎静止而模型冲刺"** ✓（那是路线第 2 个动作对上了引擎第 1 帧 ✓）

**已修**：`startTick = index - 1` ✓ ⇒ `tick = index` ✓

### 验证（**不传 `startTick` 参数**，工具自己测 ✓）

```
AUDIT actions=4212 rows=2150 startTick=239
FIRST dash tick=239 action=(0,1,1) dVX=0.0000 dVY=0.0000
      modelVX=14.5000 engineVX=14.5000 modelVY=-6.2100 engineVY=-6.2100
AUDIT walked=1911 refused=0 firstOver4Tick=251 firstOver4Regime=wing
SINCE_RESYNC 0-9   frames=94 mean=1.355  meanX=1.355 meanY=0.000
SINCE_RESYNC 10-19 frames=70 mean=10.847 meanX=10.847 meanY=0.000
SINCE_RESYNC 20-29 frames=70 mean=60.193 meanX=60.192 meanY=0.118
```

| 量 | 值 |
|---|---|
| **首帧 `modelVX` vs `engineVX`** | **14.5000 vs 14.5000** ✓✓✓ |
| **首帧 `modelVY` vs `engineVY`** | **−6.2100 vs −6.2100** ✓✓✓ |
| 首帧 `dVX` / `dVY` | **0.0000 / 0.0000** ✓✓✓ |
| `refused` | **0** ✓ |
| **`0-9` 桶 mean** | **1.355** ✓✓（源轨迹 1.753 ✓ 同级 ✓） |
| `meanY`（0-9 / 10-19） | **0.000 / 0.000** ✓✓✓ |

⇒ **模型在原点**完全精确** ✓，垂直在 20 tick 内**零误差** ✓✓
⇒ **垂直侧已经修好了** ✓✓（C89 + C94 的成果 ✓）

### ⇒ 剩余的唯一缺陷：**水平长程累积** ✗

`20-29` 桶 `meanX = 60.192` ✗ 而 `meanY = 0.118` ✓
⇒ 误差**几乎全在水平** ✓
⇒ 且随 tick 增长 ✓（1.355 → 10.847 → 60.192 ✓）
⇒ 而**引擎 `hits=9`** ✗ 正是这个累积把路线推出安全区 ✓

### ⇒ 下一轮（方向明确）

**针对水平累积** ✓ —— 而按方法的第②步 ✓，
组合时应在**每个闭环边界用引擎轨迹重同步状态** ✓，
而不是从原点一路外推 51 个闭环 ✓。

**判据**：`20-29` 桶的 `meanX` 从 **60.192** 显著下降 ✓；
引擎 `hits` 从 **9** 下降 ✓。

## C94. **★★★ 重大更正：正确原点是 `startTick = 239`；模型在原点**完全精确**（`dVX=dVY=0.0000`）⇒ C92/C93 的诊断是测量假象**

### 直接扫描探针轨迹，找引擎第一次被驱动的行 ✓

```
row=1   replayFrame=(无) vx=0     vy=0        <== 接管前的空转
row=240 replayFrame=0    vx=14.5  vy=-6.21    <== 引擎第一次被驱动
row=241 replayFrame=1    vx=14.184 vy=-6.21
row=242 replayFrame=2    vx=13.5268 vy=-6.21
```

**⇒ 引擎首帧 `vx=14.5 vy=-6.21`** ✓
⇒ 而模型预测正是 **`modelVX=14.5000 modelVY=-6.2100`** ✓✓✓ **完全一致**

**⇒ 所以审计的 `startTick` 应为 239** ✓
（审计用 `tick = startTick + step + 1` ✓ ⇒ `tick = 240` = row 240 ✓）

### 正确原点下的审计结果：**模型精确** ✓✓✓

```
AUDIT walked=1910 refused=0 firstOver4Tick=264 firstOver4Regime=wing
FIRST dash tick=240 action=(0,1,1) dVX=0.0000 dVY=0.0000
      modelVX=14.1840 engineVX=14.1840 modelVY=-6.2100 engineVY=-6.2100
FIRST wing tick=248 action=(-1,1,0) modelVX=8.0674 engineVX=8.0000 modelVY=-6.2100 engineVY=-6.2100
FIRST air  tick=762 action=(-1,0,0) modelVY=4.0333 engineVY=3.7000
```

| 量 | 值 |
|---|---|
| **首帧 `dVX` / `dVY`** | **0.0000 / 0.0000** ✓✓✓ |
| `firstOver4Tick` | **264** ✓（原点后 **24 tick** ✓） |
| `refused` | **0** ✓ |
| `walked` | 1910 ✓ |
| `SINCE_RESYNC 0-9` | **mean=2.924** ✓✓（原报 12.6 ✗）`meanY=1.255` ✓ |

### 各分支垂直误差（正确原点 ✓）

| regime | frames | 原报 `absVY` ✗ | **正确 `absVY`** ✓ |
|---|---|---|---|
| air | 184 | 2.4215 | **0.4376** ✓ |
| wing | 719 | 3.9212 | **0.5586** ✓ |
| dash | 284 | 4.0652 | **0.4312** ✓ |
| **rocket** | **7** ✓ | **8.0743** | （仅 7 帧 ✓，原报 245 帧 ✗ 是假象 ✓） |

⇒ **`rocket` 帧数从 245 ✗ 降到 7** ✓ ⇒ C92 的"rocket 暴增"**也是假象** ✓

### ⇒ **C92 与 C93 的诊断全部作废** ✗

- ~~"发散快约 10 倍"~~ ✗ —— 是 `startTick` 取错 ✓
- ~~"rocket 分支落在关键路径（28→125 帧）"~~ ✗ —— 是假象 ✓
- ~~"路线帧原点与插件重放原点不匹配"~~ ✗ —— 原点**本来就是对的** ✓

**⇒ 教训**：**审计的 `startTick` 是必须实测的常量** ✓，
不能从"接管 tick"或"轨迹 row 0"**推算** ✓
—— 这与项目铁律"物理常量必须引擎内实测不得推算"**同源** ✓✓

### ⇒ 现在的真实图景

- 模型在**原点精确** ✓，保持 <4 px 达 **24 tick** ✓ ⇒ **闭环长度内可信** ✓✓
- 误差在**长程**累积 ✓（`20-29` 桶 `meanX` 已达 **60.5** ✗）
- 引擎 `hits=9` ✗ ⇒ **不是原点误差造成的** ✓，
  而是**组合链起 51 个闭环后的累积误差** ✓ ⇒ 下一轮的主攻方向 ✓

### ⇒ 下一轮

1. **把 `startTick` 实测化** ✓：从轨迹里找 `replayFrame == 0` 的行 ✓，
   而不是靠参数推算 ✓（并写进工具 ✓）
2. **针对长程累积**：闭环组合时的**边界再同步** ✓
   —— 每跨一个闭环，用**引擎轨迹**重同步一次状态 ✓（而不是一路外推 51 个闭环 ✓）

**判据**：`20-29` 桶的 `meanX` 从 **60.5** 显著下降 ✓；引擎 `hits` 从 **9** 下降 ✓。

## C93. **★★★ 找到 9 次受击的直接原因：组合路线的**帧原点**与插件重放原点不匹配（首帧引擎静止 `vx=vy=0`，模型却冲刺 14.5）**

### 决定性现场（`ROCKETVERT` / `FIRST`，`startTick=0`）

```
FIRST rocket tick=1 action=(0,1,1) dVX=14.5000 dVY=-6.2100
      modelVX=14.5000 modelVY=-6.2100 engineVX=0.0000 engineVY=0.0000
      jump=0 wingTime=180 dashDelay=-1 eocDash=15 rocketTime=7
ROCKETVERT tick=1 engineVY=0.0000 modelVY=-6.2100 abs=6.2100
ROCKETVERT tick=2 engineVY=0.0000 modelVY=-6.2100 abs=6.2100
```

**⇒ 第 1 帧引擎的 `vx = vy = 0`** ✗ —— 玩家**静止站立** ✓
**⇒ 而路线首动作是 `(0,1,1)` = 跳跃 + 冲刺** ✗ ⇒ 模型 `vx = 14.5` ✗
⇒ **两者描述的不是同一时刻** ✓✓

### 对齐结论（两种假设都验过 ✓）

| 假设 | 结果 |
|---|---|
| `startTick = 120`（接管 tick ✗） | `engineVX=engineVY=0` ✗ ⇒ 错 ✗ |
| `startTick = 0`（轨迹 row 0 ✓） | **`firstOver4Tick=1 firstOver4Regime=rocket`** ✗ ⇒ 仍**立刻分歧** ✗ |

`plan.replayFrame` 在 **row 0 就是 0** ✓ ⇒ 轨迹 row 0 = 插件重放的第 0 帧 ✓
**⇒ 对齐本身是对的** ✓ ⇒ **分歧不是对齐造成的** ✗✓

### ⇒ 真正的问题：**路线的动作序列与引擎帧不对应**

- 组合路线由**源轨迹（fwd4-2）的闭环切片**拼成 ✓，
  其动作 0 对应源轨迹的 `slice[0].Tick` ≈ **241** ✓
- 插件把路线动作 0 应用在**它自己的重放帧 0** ✓
- 而探针轨迹第 1 帧的引擎状态是**静止站立** ✗ ⇒ **该帧不该有 `jump+dash`** ✗✓

⇒ **路线的 tick 原点与探针的 tick 原点相差一个偏移** ✓
⇒ 一次偏移使**整条路线错位** ✗ ⇒ 引擎 `hits=9` ✓✓

### 其它事实（同一审计）

```
AUDIT walked=2149 refused=0 firstOver4Tick=1 firstOver4Regime=rocket knockbackFrames=715
ROCKETVERT_SUMMARY frames=245 absVY=5.6429
```

- `refused = 0` ✓ ⇒ 模型能**完整走完**这条路线 ✓（无拒绝 ✓）
- **`knockbackFrames = 715`** ✗ ⇒ 715 帧处于受击后的无敌窗口 ✓ ⇒ **引擎很早就开始挨打** ✓
- `rocket` 帧数 **245** ✗（源轨迹 28 ✓）⇒ 火箭靴仍是关键路径 ✓

### ⇒ 下一轮（一步，且可判定）

**求出路线原点与探针原点的偏移** ✓：
从探针轨迹读**第一帧引擎被驱动**的 tick ✓
（`plan.replayFrame` 从 0 递增 ✓ 且引擎 `vx/vy` 开始非零 ✓）
⇒ 与源轨迹的 `slice[0].Tick` ✓ 相减 ✓ ⇒ 得到偏移 ✓
⇒ 用它**平移路线** ✓（或在探针里设 `CHAITE_ROUTE_SKIP` ✓）✓

**判据**：`FIRST` 行里 `engineVX/modelVX` 与 `engineVY/modelVY` **同时吻合** ✓；
引擎 `hits` 从 **9** 显著下降 ✓。

## C92. **引擎验证：模型的零受击**未迁移**（`loss hits=9 deaths=1 ticks=2149` ✗）；发散快 ~10 倍，且 rocket 分支落在关键路径上**

### 引擎结果（隔离探针 ✓）

```
RUN=game-probe-rt-r196-0919-032201
status=loss ticks=2149 hits=9 deaths=1 bossLife=42940 damage=35060 shots=7
wall=17.3s
```

| 运行 | 结果 |
|---|---|
| 模型组合（本轮 ✓） | **hits=0** ✓ |
| **引擎（本轮 ✗）** | **hits=9** ✗ |
| 引擎（上一轮，观测分支 ✗） | hits=5 ✗ |
| 引擎（无观测分支 ✗） | hits=8 ✗ |

⇒ **模型组合零受击，但引擎 9 次** ✗ ⇒ **未迁移** ✓
⇒ 也**没有比旧的更好** ✗（5 → 9 ✗）

### ⇒ 关键诊断：模型在这条轨迹上发散**快约 10 倍** ✗

对**本次探针的轨迹**跑同一个审计 ✓：

| 桶 | 源轨迹（fwd4-2 ✓） | **本次探针轨迹** ✗ |
|---|---|---|
| **0-9** | 1.753 ✓ | **13.920** ✗ |
| 10-19 | 7.007 ✓ | **63.094** ✗ |
| 20-29 | 10.615 ✓ | **123.454** ✗ |
| 30-39 | 34.124 | 201.232 ✗ |

⇒ 同一模型、同一路线 ✓，在**不同引擎轨迹**上发散速度差 ~10 倍 ✓
⇒ 说明**引擎的实际行为与模型假设不同** ✓ —— 不是模型"整体不准" ✗，
而是**这条轨迹进入了模型没覆盖的分支** ✓✓

### ⇒ regime 分布剧变，且**已知缺口落在关键路径上** ✓✓

| regime | 源轨迹 | **本次** |
|---|---|---|
| wing | 640 | 719 |
| **rocket** | **28** ✓ | **125** ✗ |
| dash | 314 | 285 |
| air | 305 | 184 |

**⇒ `rocket` 帧数从 28 暴增到 125** ✓
⇒ 而 `rocket` 的垂直误差 **4.5893** ✗（C89 记录的**唯一剩余缺口** ✓）
⇒ **组合路线大量使用火箭靴** ✓ ⇒ **那个缺口正好在关键路径上** ✓✓

### ⇒ 下一轮（优先级明确）

1. **修 `rocket` 的垂直缺口**（C89 已知 ✓，28→125 帧 ✗）✓
   —— 它是**唯一剩下的模型缺陷** ✓，且已被证明**在关键路径上** ✓
2. **核对 tick 对齐** ✓：组合路线的 tick 原点来自**源轨迹** ✓，
   而本次探针是**另一次运行** ✓ ⇒ 需用探针自己的 `plan.replayFrame` ✓ 对齐 ✓

**判据**：模型在这条探针轨迹上的 `0-9` 桶从 **13.920** 降到 **<2** ✓；
引擎 `hits` 从 **9** 降到 **0** ✓。

## C91. **★★★ 重大里程碑：组合**完整跑通**且**全流程零受击**（`composedHits=0` ✓ `replayedHits=0` ✓ `crossed=51/51` ✓）**

### 结果

```
LOOP COMPOSE segments=51 crossed=51 failedAt=-1 composedTicks=4155 composedHits=0
            replayRefusals=0 complete=True widest=48 routeTicks=4212 replayedHits=0
LOOP COMPOSE SEGMENTS loop-segments.csv loops=51 observedHits=179
```

| 指标 | 值 |
|---|---|
| 闭环总数 | **51** ✓ |
| 已跨越 | **51 / 51** ✓ |
| `failedAt` | **−1**（无失败 ✓） |
| **`composedHits`** | **0** ✓✓✓ |
| **`replayedHits`** | **0** ✓✓✓ |
| `replayRefusals` | **0** ✓ |
| `complete` | **True** ✓ |
| `routeTicks` | 4212 ✓ |
| 观测分支的受击数 | **179** ✗ |

**⇒ 观测路线有 179 次受击，而枚举组合给出 0 次** ✓✓✓
⇒ **枚举器在观测路线有受击的地方找到了零受击路径** ✓
⇒ **这正是方法本身在起作用** ✓✓（不是手写策略 ✓）

### 产物

- `artifacts\game-probe-rt-fwd4-2-0918-160519\composed-route-000.csv` ✓
  （4213 行 ✓，MD5 `0E65063E90FBE03C4C17EC27CB355B06` ✓）
- 已备份为 `composed-route-000.clean.csv` ✓ / `loop-segments.clean.csv` ✓

### 卡死的修复（属**枚举逻辑** ✓，非策略 ✓）

**病因**：`MaxExpansions = 2000000` ✗
旧模型下多数展开被**拒绝** ⇒ 提前终止 ✓
新模型下展开**成功** ⇒ **跑满 200 万次** ✗ × 48 分支 ⇒ 卡死 ✓

**修复**：上限收到 **200000** ✓，且**如实报告 `truncated`** ✓
（第五轮重点 A 的"边界报告" ✓）

### 诊断数据（`LOOP COMPOSE SEARCH`，共 4572 次搜索 ✓）

| 指标 | 值 |
|---|---|
| `clean=True` | **4296 / 4572**（94% ✓） |
| `truncated > 0` | **161** ✓ |
| `goals=0` | **276** ✓ |
| **`dominance` 量级** | **1.3M–2.0M** ✓（约 `expanded` 的 **10 倍** ✓） |
| `beam` | **0** ✓ |
| `refusals` | **1** ✓ |

⇒ **不是分支爆炸** ✓：支配剪枝**在正常工作** ✓（删掉的是展开的 10 倍 ✓）
⇒ 瓶颈是**支配检查本身的开销** ✓ —— 这是下一步可优化的枚举逻辑 ✓
⇒ 健康闭环很便宜 ✓（`loop=37`：`expanded=1186 goals=48` ✓）
⇒ 贵的闭环是**找不到干净目标**的那些 ✗（`goals=0` ✓，276 个 ✓）

### ⇒ 下一轮

**用隔离探针在引擎里验证这条零受击路线** ✓ —— 这是本方法第③步 ✓。

**判据**：探针 `hits` 与 `win` ✓；
若引擎 `hits=0` ⇒ **首次达到验收标准（无伤=获胜且零受击）** ✓✓✓

## C90. **组合用修复后的模型重跑：**几何已正常**（Y 落在 7700–7960 ✓），但在 **loop 25 卡死/发散** ✗**

### 已完成的 24 个闭环：几何**全部合理** ✓✓

```
LOOP COMPOSE THREAD index=12 x=5056.394 y=7311.359 vx=4.202399  vy=9.54
LOOP COMPOSE THREAD index=16 x=4982.069 y=7914.295 vx=-0.4888   vy=-0.585
LOOP COMPOSE THREAD index=20 x=2704.580 y=7745.898 vx=-6.0000   vy=4.840
LOOP COMPOSE THREAD index=24 x=1243.634 y=7788.157 vx=-9.5527   vy=-6.385
```

⇒ **Y 全部落在 7700–7960** ✓（地面附近 ✓）
⇒ 这是**地板修复已传播到组合**的直接证据 ✓✓
（修复前地板错误会让 Y 被夹取推飞 ✗）

### ⇒ 但 **loop 25 卡死** ✗

- 日志 **8 分钟无进展** ✗（`index=24` 之后不再输出 ✓）
- 进程**存活** ✓（`CPU = 1234 s` ✓）
- **工作集 401 MB 且持续增长** ✗ ⇒ **在累积分支** ✓
- 已终止 ✓（`job_kill` + `Stop-Process` ✓）

### ⇒ 病因推断（待验证，不预先断言）

模型修复后**可达状态变多** ✓：
1. **冲刺现在真的会执行** ✓（C80 冷却修复 ✓）
2. **位置不再被夹取推飞** ✓ ⇒ 高度真实 ✓ ⇒ 垂直可达范围**变大** ✓

⇒ 原来的**剪枝/束宽**在旧（错误）模型下"够用" ✗，
⇒ 在新（正确）模型下**不再能约束展开** ✗✓

**⇒ 这正是第五轮重点 A/D 的领域** ✓：
问题在**枚举逻辑**（剪枝、支配、bucket 定义、边界报告 ✓），
**不是**策略 ✓ —— 按铁律，必须修枚举器 ✓，不得手写策略 ✓。

### ⇒ 下一轮

**给 loop 25 的展开加计数** ✓：
- 每一深度的分支数 ✓
- 支配剪枝**删掉了多少** ✓ / bucket 命中多少 ✓
⇒ 判定是"分支爆炸" ✓ 还是"支配剪枝失效" ✗ 还是"bucket 塌掉导致无法闭合" ✗。

**判据**：组合能在有限时间内跑完 51 个闭环 ✓，
并输出 `composed-route-000.csv` ✓（旧产物已备份为 `.prev.csv` ✓）。

### 本轮附带事实

- `Chaite.Tests.exe`：**780 通过 0 失败** ✓
- 旧产物已备份：`composed-route-000.prev.csv` ✓、`loop-segments.prev.csv` ✓

## C89. **★★★ 里程碑：地板改为整条轨迹的最大底边 ⇒ 三条判据全部达成；前 ~30 tick 模型精确到 ~10 px ⇒ **闭环枚举可用了****

### 修复

审计的 `floor` 从"前 240 行的最大底边" ✗ 改为**整条轨迹的最大底边** ✓
（真正的地面 = 玩家底边到达过的**最低点** ✓）

### 判据 1：夹取不再误触发 ✓

```
VERTFLOOR tick=1509 floorY=8737.1880（原 8000.0000）
          advancedY=8001.0880（原 7958.0000）advancedVY=3.3367（原 -39.7520）
          engineY=8001.4890 engineVY=3.7367 grounded=False
```

⇒ `advancedVY` 从 **−39.7520 → +3.3367** ✓（引擎 3.7367 ✓，差一帧重力 0.4 ✓）
⇒ 位置不再被瞬移 ✓（`advancedY = 8001.088` ✓ vs 引擎 8001.489 ✓）

### 判据 2：`air` 的 `absVY` 从 7.7682 → **0.0045** ✓✓✓

```
AIRVERT_SUMMARY frames=305 engineDVY=0.0241 modelDVY=0.0285 absVY=0.0045
```

**1700 倍改善** ✓✓ —— 且 `engineDVY ≈ modelDVY` ✓（0.0241 / 0.0285 ✓）

### 判据 3：`SINCE_RESYNC` 曲线**塌陷** ✓✓✓

| 桶 | 修复前 `mean` | **修复后 `mean`** | `meanY` |
|---|---|---|---|
| **0-9** | 32.610 | **1.753** ✓✓ | **30.857 → 0.000** ✓✓✓ |
| 10-19 | 113.374 | **7.007** ✓✓ | 91.641 → **0.000** ✓✓✓ |
| 20-29 | 180.812 | **10.615** ✓✓ | 174.957 → **0.000** ✓✓✓ |
| 30-39 | — | **34.124** | 1.043 |
| 40-49 | — | **53.166** | 10.235 |
| 80+ | 413.782 | 413.782 | 381.466 ✗ |

### ⇒ **这是整个调查的目标：闭环长度内模型可信** ✓✓✓

**前 30 tick 的累积漂移 ≤ 10.6 px** ✓
⇒ 而**闭环长度就是几十 tick** ✓（§45 ✓）
⇒ **模型现在足以支撑"闭环内枚举无伤路线"** ✓✓✓
⇒ 长程漂移（`80+` 的 `meanY = 381` ✗）**在闭环之外** ✓ ⇒ 不影响本方法 ✓

### 剩余缺口（新暴露，量小）

```
VELOCITY rocket frames=28 absVY=4.5893 ✗     <== 新缺口
VELOCITY wing   frames=640 absVY=0.8928 ✓
VELOCITY dash   frames=314 absVY=1.0984 ✓
VELOCITY air    frames=305 absVY=0.0045 ✓✓
```

⇒ 水平侧 `absVX` 全部 ≤ **1.83** ✓
⇒ 只剩 `rocket` 的垂直（4.59 ✗，**28 帧** ✓）✓

### ⇒ 下一轮

1. **`rocket` 的垂直**（28 帧 ✓，`absVY = 4.59` ✗）✓ —— 小且局部 ✓
2. **回到主线**：用这个可信的模型**重跑闭环组合** ✓
   ⇒ 因为**控制序列变了** ✓（冲刺位 + 方向 ✓），旧路线必须**重新生成** ✓

## C88. **★★★ 根因完整锁定：审计用**单一常量地板**（`8000`），玩家低 1.5 px 即误触发夹取，瞬移 39.75 px**

### 现场（`VERTFLOOR`）

```
VERTFLOOR tick=1509 floorY=8000.0000 positionY=7997.7520 height=42.00
          floorMinusHeight=7958.0000 advancedY=7958.0000 advancedVY=-39.7520
          engineY=8001.4890 engineHeight=42.00 engineBottom=8043.4890
          modelBottom=8000.0000 engineVY=3.7367 grounded=False
```

| 量 | 值 |
|---|---|
| 审计的 `floorY`（常量 ✓） | **8000.0000** |
| 模型位置（1508 ✓） | 7997.7520 |
| **引擎位置（1509 ✓）** | **8001.4890** |
| 引擎底边 | **8043.4890** |
| 夹取后的 `advancedY` | **7958.0000** ✗ |
| 夹取后的 `advancedVY` | **−39.7520** ✗ |

### ⇒ 机制完整成立 ✓

1. 审计的 `FloorY` 由**前 240 行的 `position.y + height` 最大值**算出 ✓ = **8000** ✓
   ⇒ 那是**开战瞬间的地面** ✓，被当作**整条轨迹**的地板 ✓
2. 玩家在 1509 时 y = **8001.49** ✓ ⇒ **比 8000 低 1.5 px** ✗
3. 夹取条件 `nextY + Height >= FloorY` ✓ **成立** ✗
   ⇒ `nextY` 被钉到 `8000 − 42 = 7958` ✓ ⇒ **瞬移上移 39.75 px** ✗
   ⇒ `nextVelocityY = 7958 − 7997.752 = **−39.752**` ✗
4. 引擎同帧 `grounded=False` ✓、`vy=+3.74` ✓ ⇒ **引擎在正常下落** ✓

⇒ **一次误触发把 `vy` 钉成 −39.75** ✓，之后每帧 +0.4 重力**慢慢爬回** ✓
—— 与 C85 的 `-39.75 → -39.35 → -38.95` ✓ **逐帧吻合** ✓✓

### ⇒ 责任划分（重要，避免修错地方）

- **模型里的夹取逻辑本身是合理的** ✓（给定正确地板 ✓）：
  `nextVelocityY = nextY − frame.Position.Y` ✓ 是"报告速度为夹取产生的修正量" ✓，
  与已核验的原生垂直 fixture 形式一致 ✓
- **缺陷在调用方**：审计（以及 `BuildFrame` ✓）给模型喂了**单一常量地板** ✗
  ⇒ 模型无法知道"这一帧的地板在哪" ✗

### ⇒ 下一轮（一步）

**把 `FloorY` 从"整条轨迹的常量"改为"逐帧自洽"** ✓：
用**该帧引擎的 `position.y + height`** ✓（当引擎 `grounded` ✓ 时）
或**按 X 查地面高度** ✓ ⇒ 使夹取只在**真正贴地**时触发 ✓。

**判据**（三条同时成立才算执行）：
1. `tick=1509` 的夹取**不再触发** ✓，`modelVY` 恢复 ~**+3.7** ✓
2. `air` 的 `absVY` 从 **7.7682** 降到 **<1** ✓
3. `SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓

## C87. **★★★ 根因找到：−43.5 **不是冲量**，而是 `PlayerForwardModel` 的**地面夹取**误触发产生的位置修正量**

### 逐子步拆解（`VERTSTEP`）

```
VERTSTEP tick=1509 beforeVY=3.3367 gravityOnly=3.7367 flightOnly=3.3367
         flightPhase=Glide advancedVY=-39.7520 refusal=None engineVY=3.7367
         beforeWingTime=0.0 beforeWingMax=180.0 beforeRocket=False
         beforeJumpKnown=True beforeJumpSpeed=6.610 beforeJumpRemaining=0
         beforeGrounded=False beforeMaxFall=10.01
```

| 子步 | 结果 |
|---|---|
| `beforeVY`（1508 ✓） | **3.3367** ✓ 与引擎一致 ✓ |
| `gravityOnly` | **3.7367** ✓ 正确 ✓ |
| `flightOnly`（`ApplyAfterJump`） | **3.3367** ✓ 正确 ✓（Glide ✓） |
| **`advancedVY`（完整 `TryAdvance`）** | **−39.7520** ✗✗ |

⇒ **两个子步都正确，完整步错** ✓ ⇒ 错误在 `TryAdvance` **自己的组合代码**里 ✓

### 链条排查（逐一排除）

- `TryAdvance:347` → `FlightMotion.ApplyJump` ✓
- `FlightMotion.cs:85` → `JumpMotion.ApplyJump` ✓
- `JumpMotion.ApplyJump:68,77` ⇒ `velocityY = -state.Speed` = **−6.610** ✓
  ⇒ **不是 −43.5** ✗
- `ApplyAfterJump` 在 `wingTime=0` 下走 Glide ✓ ⇒ `3.3367` ✓ ⇒ **不是 −43.5** ✗

⇒ 两个可能的产生者**都排除了** ✓

### ⇒ 真正的产生者：**地面接触夹取**（`PlayerForwardModel.cs:419-423`）

```csharp
if (nextY + frame.Height >= frame.FloorY && nextVelocityY >= 0f)
{
    nextY = frame.FloorY - frame.Height;
    nextVelocityY = nextY - frame.Position.Y;   // <== 报告速度 = 位置修正量
}
```

⇒ `−39.7520 = (frame.FloorY − frame.Height) − frame.Position.Y` ✓✓
⇒ **所谓"−43.5 冲量"其实是位置修正量** ✓
—— 这也解释了它**为什么形状像位置而不像速度** ✓（C86 的怀疑正确 ✓）

### ⇒ 真正的缺陷：夹取**误触发**

条件 `nextY + frame.Height >= frame.FloorY` ✓ 成立
⇒ 说明模型认为玩家**在地板以下或贴地** ✗
⇒ 而引擎在同一帧 `grounded=False` ✓、`vy=+3.74` ✓（**在空中下落** ✓）

⇒ **模型的 `frame.FloorY` 与玩家实际 Y 不自洽** ✓✓
⇒ 一次误触发就把 `vy` 钉成 −39.75 ✓，
之后每帧 +0.4 的重力 ✓ 让它**慢慢爬回** ✓
—— 与 C85 观测的 `-39.75 → -39.35 → -38.95` ✓ **完全一致** ✓✓

### ⇒ 下一轮（一步）

**打印 `tick=1509` 的 `frame.FloorY`、`frame.Position.Y`、`frame.Height`、`nextY`** ✓
并与轨迹里引擎的 `position.y + height` ✓ 对照 ✓
⇒ 判定 `FloorY` 是**从哪一行/哪个约定**算出来的 ✓，以及为什么它高于玩家 ✓。

**判据**：夹取在 `grounded=False` 的帧上**不再触发** ✓；
`tick=1509` 的 `modelVY` 恢复为 ~+3.7 ✓；
`air` 的 `absVY` 从 7.77 降到 <1 ✓；
`SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓。

## C86. **−43.5 **不是**推力项产生的（`DemonThrust` 下限只有 −9.915）；分支是 wingTime=0 的滑翔分支**

### 现场字段（`VERTWIN`）

```
VERTWIN tick=1509 engineVY=3.7367 modelVY=-39.7520 grounded=False
        wingTimeMax=180.0 wingTime=0.0 rocketBoots=False
        jumpKnown=True jumpSpeed=6.610 maxFall=10.01 gravity=0.400 action=(-1,0,0)
```

- `wingTimeMax = 180` ✓ ⇒ `winged = !Grounded && WingTimeMax > 0` **为真** ✓ ⇒ 走**飞行分支** ✓
- `wingTime = 0` ✓ ⇒ **翅膀已耗尽** ⇒ 应走 **滑翔**分支 ✓
  （引擎正是 `+3.34/+3.74` ✓ = `maxFall/3` ✓ 的滑翔 ✓）
- `modelVY = -39.7520` ✗ ⇒ 模型在**快速上升** ✗，与滑翔完全相反 ✓

### ⇒ 关键否定结论：**没有任何推力常量能产生 −43.5** ✗

`DemonThrust`（`FlightMotion.cs:184`）✓：

```csharp
if (velocityY - .1f > 0f) velocityY -= .875f;
else if (velocityY > -jumpSpeed) velocityY -= .275f;
else velocityY -= .125f;
return Math.Max(velocityY, -jumpSpeed * 1.5f);   // = -9.915
```

⇒ **下限是 `-jumpSpeed * 1.5 = -9.915`** ✓
⇒ `RocketThrust` 同样夹在 `-9.915` ✓
⇒ **−43.5 不可能来自这两个推力项** ✓✓

### ⇒ 因此来源在**步内的其它环节**

`−43.5` 是**一帧内**的变化 ✓（1508 两侧吻合 ✓ → 1509 差 43.5 ✓）
⇒ 只能在：重力项 ✓、碰撞/位置解算 ✓、或某个**未被初始化的字段** ✓ 里产生 ✓

### ⇒ 下一轮（一步）

**在 `tick=1509` 把模型的垂直步**逐子步**打印** ✓：
`frame.Velocity.Y`（步前 ✓）→ 重力后 ✓ → 飞行后 ✓ → 碰撞后 ✓ → `next.Velocity.Y` ✓
⇒ 直接指出 −43.5 出现在哪一个子步 ✓。

**判据**：找到产生 −43.5 的子步 ✓；
修好后 `tick=1509` 的 `modelVY` 与引擎一致（~+3.7 ✓）；
`air` 的 `absVY` 从 7.77 降到 <1 ✓；
`SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓。

## C85. **★★ 偏移被定位到**一帧**：`tick=1509` 在普通 air 分支里 `vy` 跳变 −43.5（`modelVY = -39.75` vs 引擎 `+3.74`）**

### 证据（`AIRBIG`：`abs > 1` 的 air 帧 + 前一帧）

```
AIRBIG tick=1509 prevTick=1508 prevRegime=air regime=air
       engineVY= 3.7367 modelVY=-39.7520 abs=43.4886 prevAbs=0.0000
       jump=0 wingTime=0 dashDelay=0 rocketTime=0
AIRBIG tick=1510 prevTick=1509 prevRegime=air regime=air
       engineVY= 3.3367 modelVY=-39.3520 abs=42.6886 prevAbs=43.4886
       jump=1 wingTime=0 dashDelay=0 rocketTime=0
AIRBIG tick=1511 ... modelVY=-38.9520  (每帧 +0.4 = 重力 ✓)
AIRBIG tick=1515 ... modelVY=-37.3519
```

### 结论 1：偏移是**一帧内引入**的 ✓✓

`prevAbs = 0.0000` ✓ ⇒ 前一帧完全吻合 ✓
`abs = 43.4886` ✗ ⇒ **一帧内跳变约 43.5 px/tick** ✓

⇒ 这**彻底否定**了"误差是缓慢累积"的设想 ✓
⇒ 也解释了为什么 `absVY` 的均值是 7.77 而**前 10 帧都是 0** ✓
（偏移从 1509 起一直保持 ✓，把均值拉高 ✓）

### 结论 2：跳变发生在**普通 air 分支**里 ✓

`wingTime = 0` ✓、`dashDelay = 0` ✓、`rocketTime = 0` ✓、`jump = 0` ✓
⇒ **没有任何冲刺/翅膀/火箭靴/跳跃输入** ✓
⇒ 模型却在普通垂直步里产生了 **−43.5** ✗✓

### 结论 3：之后每帧 +0.4 ✓ = 重力 ✓

`-39.7520 → -39.3520 → -38.9520 → ...` ✓
⇒ 说明模型此后走的是**正确的重力** ✓，只是**起点错了 43.5** ✗
⇒ **再一次印证：逐帧动力学精确，错的是某个单帧的冲量** ✓

### ⇒ 下一轮（一步）

**在 `tick=1509` 打印模型垂直步的输入与输出** ✓：
步前的 `frame.Velocity.Y` ✓、分支（winged / 普通 ✓）、
`jump` 子状态 ✓、`FlightMotion` 的返回值 ✓、步后的 `next.Velocity.Y` ✓
⇒ 直接指出 **−43.5 是哪一项产生的** ✓。

**候选**（待验证，不预先断言）：
- `FlightMotion.ApplyJump` 的冲量取错 ✓
- `frame.Jump` 子状态未初始化 ⇒ 用到垃圾值 ✗
- `vy` 被某个 `MoveTowards` 越界 ✓

**判据**：`tick=1509` 的 `modelVY` 与引擎一致（~+3.7 ✓）；
随后 `air` 的 `absVY` 从 7.77 降到 <1 ✓；
`SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓。

## C84. **★ 找到计数器 bug（测错索引）并得出决定性结论：air 的逐帧垂直动力学**精确**（增量两侧完全相同），误差是**恒定偏移****

### bug：`names[0]` 是 ground，`names[1]` 才是 air

`names = { ground, air, wing, rocket, dash }` ✓
而 AIRVERT 块写的是 `regime == 0` ✗ ⇒ 它测的是**地面**（全轨迹只有 **1 帧** ✓）
⇒ `airVertCount = 1` 就是那唯一的**地面帧** ✓✓
⇒ **C82 的整个结论（"air 几乎从不相邻"）来自测错了索引** ✗
⇒ `ADJACENT air adjacentPairs=300` ✓ 才是真的 ✓ —— 自检计数器正是这样抓到它的 ✓

**已修**：`regime == 1` ✓

### 修正后的测量（`frames=305`，与 `ADJACENT` 自检一致 ✓）

```
AIRVERT tick=731 engineVY=-1.9150 modelVY=-1.9150 abs=0.0000
AIRVERT tick=732 engineVY=-1.5150 modelVY=-1.5150 abs=0.0000
AIRVERT tick=733 engineVY=-1.1150 modelVY=-1.1150 abs=0.0000
AIRVERT tick=750 engineVY= 3.3367 modelVY= 3.3367 abs=0.0000
AIRVERT tick=752 engineVY= 3.3367 modelVY= 3.3367 abs=0.0000
AIRVERT tick=754 engineVY= 4.1367 modelVY= 4.1367 abs=0.0000
AIRVERT_SUMMARY frames=305 engineDVY=0.0241 modelDVY=0.0241 absVY=7.7682
```

### ⇒ 决定性结论：**逐帧增量完全相同** ✓✓，误差是**恒定偏移**

| 量 | 引擎 | 模型 |
|---|---|---|
| 逐帧 `vy` 增量均值 | **0.0241** | **0.0241** ✓✓ |
| 绝对偏移 `absVY` | — | **7.7682** ✗ |

**⇒ air 分支的逐帧垂直动力学是**精确**的** ✓✓
（重力 `0.400` ✓、终端下落 `3.3367` ✓、起跳后 `3.7367/4.1367` ✓ 全部吻合 ✓）

**⇒ 误差**不是**分支内的加速度项** ✗ —— **与 C69 与 C82 的推断都相反** ✓
⇒ 它是**一个恒定偏移**，在**某处被引入并保持** ✓
⇒ 前 10 帧样本 `abs = 0.0000` ✓ ⇒ 偏移是**后来某帧**引入的 ✓

### ⇒ 下一轮（一步即可定位）

**打印 `abs > 1` 的 air 帧**（以及它的前一帧）✓
⇒ 直接指出**引入 7.77 偏移的那一帧** ✓，以及它属于哪个分支 ✓。

**判据**：找到偏移被引入的确切帧与分支 ✓；
修好后 `air` 的 `absVY` 从 7.77 降到 <1 ✓，
`SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓。

## C83. **更正 C82：air **是**相邻的（305 帧 / 300 相邻对）—— C82 的"从不相邻"是错的；`frames=1` 是计数器 bug**

### 每个 regime 的相邻对数量

```
ADJACENT ground frames=1   adjacentPairs=0
ADJACENT air    frames=305 adjacentPairs=300     <== 300/305 相邻！
ADJACENT wing   frames=640 adjacentPairs=620
ADJACENT rocket frames=28  adjacentPairs=24
ADJACENT dash   frames=314 adjacentPairs=293
```

**⇒ air 的 305 帧里有 300 对相邻** ✓✓
⇒ **C82 的结论"air 帧几乎从不相邻"是错的** ✗
⇒ C82 据此推出的"误差不是分支内累积"**也随之作废** ✗
⇒ C69 的"是加速度项"推断**可能仍成立** ✓（尚未定论 ✓）

### 矛盾未解：`airVertCount = 1` vs `adjacent[0] = 300`

两个计数用**同一个 `previousRegime`** ✓：
- `AIRVERT`：`if (regime == 0 && previousRegime >= 0)` ⇒ 1 次 ✗
- `ADJACENT`：`if (previousRegime == regime) adjacent[regime]++` ⇒ 300 次 ✓

且 `adjacent` 的条件**更严**（`== 0` 而 `>= 0`）✓
⇒ 却得到**更多**次数 ✗ ⇒ **矛盾** ✓ ⇒ 我的计数实现有 bug ✓
（**不是**物理结论 ✓ —— 这一点必须先分清 ✓）

### ⇒ 下一轮

**找出计数器 bug** ✓：核对 `previousRegime` 在两个计数之间的赋值时机 ✓、
以及 `AIRVERT` 块是否被**提前 `continue`** 跳过 ✓。

**判据**：`AIRVERT_SUMMARY frames` 与 `ADJACENT air adjacentPairs` **一致** ✓；
随后才能用逐帧增量定位 air 的垂直项 ✓。

### 方法学教训（本轮的真实收获）

**在同一个循环里加两个"应当一致"的计数器，是有价值的自检** ✓
—— 本轮正是靠它发现 C82 的结论不可靠 ✓
（C82 我只加了增量计数器、没有对照 ✓ ⇒ 误判 ✓）。

## C82. **`air` 的垂直误差不是"分支内累积"：air 帧几乎从不相邻（305 帧里只有 1 对相邻）**

### 测量：air 分支的逐帧 `vy` 增量（两侧）

```
AIRVERT tick=825 engineVY=8.6504->0.0000 d=-8.6504 modelVY=8.6504->0.0000 d=-8.6504
                jump=0 wingTime=0 maxFall=10.01 gravity=0.400 modelWing=0 modelGrav=0.400
AIRVERT_SUMMARY frames=1 engineDVY=-8.6504 modelDVY=-8.6504
```

**⇒ 唯一一对相邻的 air 帧两侧**完全吻合** ✓（`-8.6504` ✓，那是落地 ✓）**

### ⇒ 关键事实：`air frames=305` 但**相邻对只有 1** ✗

⇒ `air` 的 305 帧**几乎从不相邻** ✓
⇒ 那 7.77 的误差**不可能**是"在 air 分支内逐帧累积的加速度项" ✗✓
⇒ 它是**进入该帧时的速度偏移** ✓

**⇒ 这更正了 C69 的推断** ✓：
C69 说"每个 regime 首帧 `dVY = 0` ⇒ 是加速度项" ✗
—— 那只说明**首帧**没错 ✓，但 `air` 帧是**孤立**的 ✓
⇒ 误差是在**别的 regime**里积累出来的 ✓，在 air 帧上**显现** ✓。

### 方法学教训（记录）

**用"regime 首帧误差为 0"推断"误差是分支内累积的"是无效推理** ✓
—— 因为 regime 可能是**碎片化**的 ✓（本 case：305 帧里只有 1 对相邻 ✓）
⇒ 正确做法是**先报告每个 regime 的相邻对数量** ✓，
再决定能否用"分支内累积"解释 ✓。

### ⇒ 下一轮

1. **打印每个 regime 的相邻对数量** ✓（`air` 只有 1 ✓ 已证）
2. 对 `air` 帧打印**绝对** `vy`（模型 vs 引擎）✓，而不是增量 ✓
   ⇒ 直接看 7.77 是"恒定偏移"还是"随帧数增长" ✓

**判据**：确定 7.77 是**偏移**还是**累积** ✓，据此决定修 `air` 本身
还是修**产生该偏移的上游分支** ✓。

**注意**：本轮**没有**修好 7.77 ✓ —— 但**排除了一个错误的诊断方向** ✓，
并记录了导致该误判的推理缺陷 ✓。

## C81. **★★ 水平模型修好：处处准确到 2 px/tick 以内；垂直误差被干净隔离出来成为唯一主导缺陷**

### 实现

**冲刺方向重建** ✓（与 C77 的冲刺位同构）：
引擎在 `tick=T` 进入冲刺（`dashDelay` 变 −1 ✓）
⇒ 冲刺方向 = `sign(engine.velocityX[T])` ✓ ⇒ 路线第 `T-1` 帧设对应方向位 ✓
两处都改：`ObservedControls(row, nextRow)` ✓ 与 `tools/rebuild-observed-route.ps1` ✓

### 对照结果：**水平误差全线塌到 2 px/tick 以内** ✓✓✓

| regime | frames | 修复前 `absVX` | **修复后 `absVX`** |
|---|---|---|---|
| ground | 1 | 2.9501 | **0.2000** ✓✓ |
| air | 305 | 1.3596 | **0.7657** ✓ |
| wing | 640 | 2.6800 | **0.8018** ✓ |
| rocket | 28 | 1.6807 | **1.1526** ✓ |
| **dash** | 314 | **17.8245** ✗ | **1.8261** ✓✓ |

```
DASHWIN tick=734 action=(1,0,0) modelDash=-1 modelVX=14.1840 engineDash=-1 engineVX=14.5000
```

⇒ `modelVX` 现在是 **+14.1840** ✓（引擎 14.5000 ✓，差一帧 ✓ 符合"模型在 733 启动"✓）

`SINCE_RESYNC` 的水平分量同步改善 ✓：
`10-19` 桶 `meanX` 从 **22.607 → 7.007** ✓；`20-29` 桶从 — → **10.615** ✓

### ⇒ 垂直误差现在**唯一主导** ✓✓

| regime | `absVX` | **`absVY`** |
|---|---|---|
| ground | 0.2000 | **0.0000** ✓ |
| **air** | 0.7657 | **7.7682** ✗✗ |
| wing | 0.8018 | **0.6581** ✓ |
| rocket | 1.1526 | **0.0000** ✓ |
| dash | 1.8261 | **1.0636** ✓ |

**⇒ 只有 `air`（无翅膀空中）的垂直分量坏（7.77 ✗），其余四支都 <1.1 ✓**

⇒ 这**不是新问题** ✓ —— 正是 C68 报的 `air dVY = -7.7682` ✓
⇒ 但当时被水平的灾难（`dash 17.8` ✗）掩盖 ✓
⇒ **现在它被干净地隔离出来，成为唯一剩下的模型缺陷** ✓✓

### ⇒ 下一轮（目标明确且单一）

**修 `air` 的垂直** ✓：`absVY = 7.7682` ✓

线索（C69 已实测排除）：
- ~~终端下落速度 / `Down`~~ ✗（五帧的 `maxFall=10.01`、`down=0` 两侧一致 ✓）
- 每个 regime 的**首帧** `dVY = 0.0000` ✓ ⇒ 是**加速度项**在 regime 内累积 ✗

⇒ 下一步：在**连续的 air 帧**上对照 `vy` 的**逐帧增量** ✓
与引擎实测常量核对（重力 `+0.400`、翅膀下坠 `-0.875`、
上升带内 `-0.275`、带下 `-0.125`、`ctlJump && wingTime==0 && vy<0` ⇒ `+0.4`）✓

**判据**：`air` 的 `absVY` 从 7.77 降到 <1 ✓；
随后 `SINCE_RESYNC 0-9` 的 `meanY` 从 **30.857** 显著下降 ✓
—— 那将是**第一次**模型在闭环长度内可信 ✓。

## C80. **★ 冷却 bug 修复并证明已执行（模型终于会冲刺）✓；剩下的错在冲刺**方向**（同样未记录）**

### 先更正 C79 的误读

C79 说"审计读到的该位为 0" ✗ —— 那是**重建前的旧输出** ✓。
本轮 `PARSE` 打印证明**对齐是正确的** ✓：

```
ROUTE MODEL PARSE count=1799 first=-1,1,0 second=-1,1,0 at492=0,1,1
ROUTE MODEL DASHWIN tick=733 action=(0,1,1)      <== 冲刺位确实是 1
```

### ⇒ 真正的 bug：普通分支**从不递减 `DashDelay`** ✗✓

```
DASHWIN tick=730 modelDash=30 engineDash=0     <== 模型在 730 就已经卡在冷却 30
DASHWIN tick=733 modelDash=30 engineDash=0     <== 引擎 ready，模型仍 30
DASHWIN tick=734 modelDash=30 engineDash=-1    <== 引擎冲刺，模型还是 30
```

`PlayerForwardModel` 里只有 `else if (frame.Dashing)` 分支推进冲刺状态 ✓，
而 `frame.Dashing = DashDelay < 0` ✓ —— 那是**冲刺本身**，**不是冲刺之后的冷却** ✓
⇒ 一旦进入冷却就**永远出不来** ✗
⇒ 而冲刺候选只能从 ready 态创建 ✓ ⇒ **卡在冷却的模型根本无法冲刺** ✓✓

### 修复

普通分支里补上冷却推进 ✓：

```csharp
if (dash.DashDelay > 0)
    EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref dash);
```
（冷却路径**不改 `VelocityX`** ✓，所以普通运动步仍然拥有速度 ✓）

### 对照证明：**修复已执行** ✓✓

```
DASHWIN tick=734 action=(1,0,0) modelDash=-1 modelVX=-13.8381 engineDash=-1 engineVX=14.5000
DASHWIN tick=735 action=(0,1,0) modelDash=-1 modelVX=-13.5320 engineDash=-1 engineVX=14.1840
```

`modelDash` 从 **30 → -1** ✓ ⇒ **模型进入冲刺了** ✓
⇒ 按铁律，这是**已执行**的确凿证据 ✓。

### 但**方向反了** ✗

`modelVX = -13.8381` ✗ vs `engineVX = +14.5000` ✓ ⇒ 模型**向左**冲、引擎**向右**冲 ✓

**原因**：`tick=733` 的 `action=(0,1,1)` ⇒ **方向位是 0** ✗
⇒ 模型回退到 `FacingDirection` ✗
⇒ **引擎的双击冲刺方向同样没有记录在 `controlLeft/Right` 里** ✓
—— 与 C77 的冲刺位是**同一类缺口** ✓。

### ⇒ 下一轮（与 C77 同构的修复）

**重建冲刺方向** ✓：引擎在 `tick=T` 进入冲刺（`dashDelay` 变 −1 ✓）
⇒ 冲刺方向 = `sign(engine.velocityX[T])` ✓
⇒ 在路线第 `T-1` 帧把 `Direction` 设为该符号 ✓。

**判据**：
- `DASHWIN tick=734` 的 `modelVX` 变为 **+13.8 ~ +14.5** ✓
- `VELOCITY` 的 `dash` 的 `absVX` 从 **17.82** 大幅下降 ✓
- `air`/`ground`/`wing` 的 `absVX` 同步下降 ✓

**注意**：误差本轮**变大**了 ✓（`dash absVX` 7.01 → 17.82 ✓）
—— 这是**预期的** ✓：模型现在真的冲刺了，只是**冲错方向** ✓
⇒ 方向修好后应出现**第一次实质性下降** ✓。

## C79. **重建的 CSV 正确（含 tick 733 的冲刺位）✓，但审计读到的该位为 0 ⇒ 索引映射错位**

### 事实一：CSV 正确 ✓

```
dash ticks: 282, 331, 517, 584, 633, 683, 733, 782, 832, 882, 931, 981, 1030,
            1079, 1129, 1178, 1228, 1278, 1328, 1376, 1425
```

**21 个冲刺位 ✓，其中包含 733** ✓
（与引擎在 `tick=734` 进入冲刺一致 ✓：位在前一帧 ✓）

⇒ `rebuild-observed-route.ps1` 与 `ObservedControls(row, nextRow)` 的规则**都正确** ✓。

### 事实二：审计读到的却是 0 ✗

```
DASHWIN tick=733 action=(0,1,0)   <== 第三位（Dash）应为 1
DASHWIN tick=734 action=(1,0,0)   <== 也应为 1 的延续（冲刺继续）
```

⇒ 审计拿到的 `actions[step]` **不是** tick 733 那一行 ✗
⇒ **路线索引与 tick 的映射错位** ✓

### 索引推导（供下一轮核对）

- `rebuild-observed-route.ps1`：`$StartTick = 241` ✓
  ⇒ 第 1 行数据 = tick **241** ✓ ⇒ **1-based 行号 `i` = tick `240 + i`** ✓
  ⇒ tick 733 ⇒ 行号 **493** ⇒ 0-based 数据下标 **491** ✓
- 审计：`var tick = startTick + step + 1;` ✓，`startTick = 240` ✓
  ⇒ `actions[step]` 对应 tick `241 + step` ✓
  ⇒ tick 733 ⇒ `step = **492**` ✓

**⇒ 491 与 492 差 1** ✓ —— 这就是错位所在 ✓：
CSV 的第 0 个数据行是 tick 241 ✓，而审计认为 `actions[0]` 对应 tick 241 ✓
—— **两者本应一致** ✓，所以错位应出在**审计解析 CSV 时**（例如把表头当数据、
或多跳/少跳一行）✓。

### 下一轮（一步）

**打印审计解析后的 `actions.Count` 与前 3 行、以及 `actions[492]` 的值** ✓
⇒ 与 CSV 的 1799 行 / tick 733 那一行对照 ✓，定位错位的确切位置 ✓。

**判据**：`DASHWIN tick=733` 的 `action` 第三位变为 **1** ✓；
`tick=734` 的 `modelDash` 变为 **-1** 且 `modelVX` 达到 **14.5** ✓。

## C78. **冲刺位重建已实现（路线 0 → 21 个冲刺位）✓；但冲刺候选未给出 14.5 ⇒ 模型一帧内进出冷却**

### 实现

1. `ObservedControls(row, nextRow)` 重载 ✓：
   `Dash = row.dashDelay >= 0 && nextRow.dashDelay < 0` ✓
   （冲刺在 `dashDelay` 变负的**那一帧**开始 ⇒ 控制位属于**前一帧** ✓，
   这也正是模型需要的：候选由 ready 态创建、把 `dashDelay` 置为 −1 ✓）
2. 两个调用点（观测分支 walk ✓、`AuditObservedRoutes` ✓）改为传下一行 ✓
3. 新脚本 `tools/rebuild-observed-route.ps1` ✓：
   从密集轨迹重建观测路线，冲刺位取自**引擎状态**而非 `controlDash` ✓

### 对照证据

```
ROUTE REBUILD rows=2041 actions=1799 dashes=21        <== 原来 0
DASHWIN tick=734 action=(1,0,0) modelDash=30 modelVX=4.9518
                                engineDash=-1 engineVX=14.5000 engineEocDash=15
```

**⇒ 修复**部分执行** ✓**：路线确实带上了 21 个冲刺位 ✓
（改动前 `dashes = 0` ✓）⇒ 按铁律，这是**已执行**的证据 ✓。

### 但模型仍未进入冲刺 ✗

`tick=734` 时 `modelDash = 30` ✓ —— **30 是冷却值 `CooldownTicks`** ✓
⇒ 模型在 `tick=733` 启动了冲刺 ✓，但**一帧内就进入冷却** ✗
⇒ 且 `modelVX = 4.9518` ✗（引擎 14.5000 ✓）
⇒ **冲刺候选没有把速度置为 14.5** ✗✓

**机制推断**：`TryAdvanceCollisionFreeTick` 在
`bled = |vx| - bleed <= runSpeed(=8)` 时进入冷却 ✓
⇒ 若冲刺启动那一帧速度只有 ~4.9 ✗ ⇒ `bled ≈ 4.4 < 8` ✗
⇒ **立刻进入冷却** ✓ —— 与观测完全一致 ✓。

⇒ 所以问题在 `EyeShieldDashMotion.TryCreateDedicatedCandidate`
**是否返回 true 并给出 14.5** ✓。

### 其它数字（基本未变 ✗）

```
VELOCITY ground dVX=-2.9299  air dVX=-1.3257  wing dVX=-2.6336  dash dVX=-8.0443
SINCE_RESYNC 0-9 mean=32.610 meanX=1.753 meanY=30.857
```

⇒ 冲刺位虽已补上 ✓，但**模型的冲刺没有真正跑起来** ✗
⇒ 误差数字当然不会改善 ✓ —— 逻辑自洽 ✓。

### 下一轮（一步）

**打印 `tick=733` 那一帧**：`action`、`TryCreateDedicatedCandidate` 的返回值、
`state.Dash.DashDelay/EocDash`、`state.Velocity.X` ✓
⇒ 判定候选是**返回 false** ✗ 还是**返回 true 但速度没设** ✗。

**判据**：`modelVX` 在 `tick=734` 达到 **14.5** ✓；
随后 `VELOCITY` 的 `air`/`ground`/`dash` 的 `absVX` 显著下降 ✓。

## C77. **★ 决定性发现：审计用的"观测路线"**没有冲刺位**，因此审计整体无效 —— 引擎在 `controlDash = 0` 时也启动了冲刺**

### 冲刺启动窗口（`tick 728..744`）

```
DASHWIN tick=733 action=(0,1,0) modelDash= 0 modelVX= 4.6805 engineDash= 0 engineVX= 7.5067 engineEocDash= 0 engineControlDash=0
DASHWIN tick=734 action=(1,0,0) modelDash= 0 modelVX= 4.9317 engineDash=-1 engineVX=14.5000 engineEocDash=15 engineControlDash=0
DASHWIN tick=735 action=(0,1,0) modelDash= 0 modelVX= 4.8317 engineDash=-1 engineVX=14.1840 engineEocDash=15 engineControlDash=0
DASHWIN tick=736 action=(1,1,0) modelDash= 0 modelVX= 4.9322 engineDash=-1 engineVX=13.8727 engineEocDash=15 engineControlDash=0
DASHWIN tick=744 action=(1,1,0) modelDash= 0 modelVX= 4.9341 engineDash=-1 engineVX=11.0176 engineEocDash=15 engineControlDash=0
```

### 结论 1：**模型从未启动冲刺** ✗

`modelDash = 0` 在**每一帧** ✓，而引擎在 `tick=734` 进入冲刺 ✓
（`engineDash = -1`、`engineEocDash = 15`、`engineVX` 一步到 **14.5000** ✓）

### 结论 2：**`engineControlDash = 0` 每一帧** ✓✓✓

⇒ **引擎在没有 `controlDash` 的情况下启动了冲刺** ✓
（1.4.5 里护盾冲刺可由**双击方向键**触发 ✓，而 `controlDash` 观测到的仍是 0 ✓）

### ⇒ 结论 3：**审计整体无效** ✓✓✓

审计用的 `observed-route-*.csv` 是从 **`controlDash`** 逆映射出来的 ✓
（`ObservedControls`：`Dash = controlDash` ✓）
⇒ 那条路线**完全没有冲刺位** ✗
⇒ 模型**从不冲刺** ✗，而引擎**一直在冲刺** ✓
⇒ 两者从第一个冲刺帧（734）起就分道扬镳 ✓
⇒ **C67–C76 的所有误差数字（~1000–3000 px、"垂直偏差"、regime 分解）
都是"路线缺冲刺位"的产物** ✗，不是模型的缺陷 ✓✓

**⇒ 也解释了 `AuditObservedRoutes` 的 `maxError = 187`** ✓：
它只在**首次受击前**累计 ✓，而首个冲刺在 tick 734 ✓
⇒ 那个窗口里还没冲刺 ⇒ 误差自然小 ✓✓ **完全自洽** ✓。

### ⇒ 更正范围

以下结论**需要重新评估**（它们建立在无效的对照上）：
- C67 的"逐 tick 垂直偏差 ~3 px/tick" ✗
- C68 的"air 的 dVY=−7.77 / dash 的 dVX=−7.00" ✗
- C69 的"水平是入 regime 即偏移" ✗
- C70 的"速度天花板 4.71" ✓ **仍成立**（`frameMaxRun` 从 4.71→8.0 是实测 ✓）
- C71 的超速规则 ✓ **仍成立**（直接从引擎轨迹读出 ✓，与路线无关 ✓）
- C72–C76 的推理 ✓ 部分仍成立（常量实测 ✓），但"差距是历史性的"这一判断
  现在有了**确切原因**：路线没有冲刺位 ✓

### ⇒ 下一轮（关键修复）

**`ObservedControls` 必须从引擎的冲刺状态重建冲刺位** ✓，而不是读 `controlDash` ✗：
引擎在 `tick=734` 的 `dashDelay` 变为 **−1** ✓
⇒ 路线在 `tick=733` 的那一位应为 **`Dash = 1`** ✓（冲刺在下一帧生效 ✓）。

**判据（硬）**：
- 重建后的观测路线上，`modelDash` 在 `tick=734` 起为 **−1** ✓
- `modelVX` 在 `tick=734` 达到 **14.5** ✓
- `ROUTE MODEL VELOCITY` 的 `air`/`ground`/`dash` 的 `absVX` **显著下降** ✓

**注意**：这条修复会改变**审计**与**组合搜索**的输入 ✓ ⇒ 之后需**重跑组合** ✓。

## C76. **实测：引擎空中无翅膀的加速量恰为 `+0.1005` = 模型常量 `runAcceleration × 0.4` ⇒ 常量与分支都对；差距是**历史性**的（冲刺帧）**

### 引擎实测（`AIRACCEL` 帧：`!grounded && wingTime==0 && 输入同向 && |vx| < accRunSpeed`）

```
AIRACCEL tick=731 engineVX=7.6062->7.7067 d= 0.1005 accRun=8.00 runAccel=0.2512 wingTime=0 wingsLogic=26 input=1
AIRACCEL tick=734 engineVX=7.5067->14.5000 d= 6.9933 accRun=8.00 runAccel=0.2512 wingTime=0 wingsLogic=26 input=1
AIRACCEL tick=752 engineVX=7.9000->8.0005 d= 0.1005 accRun=8.00 runAccel=0.2512 wingTime=0 wingsLogic=26 input=1
AIRACCEL tick=754 engineVX=7.9005->8.0010 d= 0.1005 accRun=8.00 runAccel=0.2512 wingTime=0 wingsLogic=26 input=1
AIRACCEL tick=756 engineVX=7.9010->8.0014 d= 0.1005 accRun=8.00 runAccel=0.2512 wingTime=0 wingsLogic=26 input=1
AIRACCEL_SUMMARY frames=158 meanDelta=0.0991
```

### 结论 1：**常量完全一致** ✓✓

引擎实测加速量 **`+0.1005`** ✓
模型 `SprintAcceleration = runAcceleration × (wingsLogic > 0 ? .4 : .2)`
= `0.2512 × 0.4` = **`0.10048`** ✓✓ **精确吻合** ✓

⇒ **C75 的怀疑（`CanSprintInAir` 应包含靴子）被否定** ✗：
`wingsLogic = 26` **非零** ✓ ⇒ `CanSprintInAir` 本就为真 ✓
⇒ 加速分支**是会执行的** ✓

### 结论 2：`tick=734` 的 `d = +6.9933` 是**冲刺启动** ✓

`7.5067 → 14.5000` ✓ —— 一步到冲刺速度 **14.5** ✓
（与已实测的冲刺起始速度一致 ✓）

### ⇒ 差距是**历史性**的，不是常量错

模型在 `tick=731` 是 4.88 ✓，引擎是 7.71 ✓
⇒ 模型**正在以 +0.1005/tick 往上爬** ✓，只是**起点太低** ✗
⇒ 起点低的原因是**冲刺那一帧（734）没拿到 14.5** ✗✓

### 下一轮（一步）

**打印 `tick=730..742` 每帧的：模型 `dashDelay`/`velocityX`、引擎 `dashDelay`/`velocityX`、
以及该帧的 `action`** ✓ ⇒ 直接看出冲刺是**没启动**（`dashDelay` 不变 ✗）
还是**启动了但速度没给到 14.5** ✗。

**判据**：模型在 `tick=734` 的 `velocityX` 达到 **14.5** ✓；
随后 `air` 首帧 `modelVX` 升到 ~7.7 ✓。

**说明**：本轮的实测**否定了一个假设、确认了两个常量** ✓，
并把差距范围从"加速规则"缩小到"冲刺启动那一帧" ✓。

## C75. **上限修复**确实生效**（`frameMaxRun` 4.71 → 8.0000）✓；剩下的差距是"空中无翅膀不能积累速度"**

### 诊断证据（首个 air / ground 帧）

```
FIRST air    tick=731 action=(1,1,0) modelVX=4.8805 engineVX=7.7067 wingTime=0 dashDelay=0
             frameMaxRun=8.0000 frameBaseRun=4.7100 frameSprint=0.1005
             engineAccRun=8.00 engineMaxRun=4.71
FIRST ground tick=825 action=(0,0,0) modelVX=4.6552 engineVX=7.6053
             frameMaxRun=8.0000 frameBaseRun=4.7100 frameSprint=0.1005
```

**⇒ C74 的修复**生效了** ✓✓**：`frameMaxRun` 从 **4.71 → 8.0000** ✓
（与引擎的 `engineAccRun=8.00` 一致 ✓），`frameSprint = 0.1005` 非零 ✓
⇒ C74 里"对照无变化"的判断需要更正 ✓：
**修复改变了模型输入 ✓，只是没有改变这两帧的输出** ✓。

### ⇒ 剩余差距的原因（已定位到条件）

`HorizontalMotion.Advance` 的加速分支：

```csharp
else if (forward < topSpeed && sprintAcceleration > 0f)
{
    if (grounded || player.CanSprintInAir)
        velocity = (forward + sprintAcceleration) * direction;
}
```

而测试侧 `CanSprintInAir = wingsLogic > 0f` ✓
⇒ 首个 air 帧 `wingTime=0`（`wingsLogic` 为 0 ✓）⇒ **不满足** ✗
⇒ 既不加速（`forward < topSpeed` 成立但 `CanSprintInAir` 假 ✗）
⇒ 又不到超速分支（`forward > topSpeed` 假 ✗）
⇒ 落到 `else` 的**衰减** ✗
⇒ **模型在"空中无翅膀"时永远停在 `BaseRunSpeed`（4.71）附近** ✗✓
—— 正是观测到的 4.88 / 4.66 ✓。

### ⇒ 引擎的真实行为需要实测（不得推算）

引擎在**同一帧**是 **7.71** ✓ —— 它的速度是**earned** 的 ✓：
从冲刺 14.5 → 衰减到 8 → 钳在 8 → 之后按 0.1/tick 衰减到 7.7 ✓
（C71 已逐帧实测 ✓）。

⇒ 问题是**模型为什么没有保住那 8** ✗：
模型的冲刺退出钳位是 `runSpeed = max(AccRunSpeed, MaxRunSpeed)` = **8** ✓（C73 已核对 ✓）
⇒ 理论上应同样钳在 8 ✓ ⇒ 需要看**冲刺刚结束的那一帧**模型的实际 `velocityX` ✓。

### 下一轮（两步）

1. **打印模型在冲刺结束帧（`DashDelay` 由 −1 变 0）的 `velocityX`** ✓
   与引擎同帧对照 ✓ ⇒ 判定"8 是否被保住" ✓。
2. **实测引擎在"空中无翅膀但有靴子"时的加速规则** ✓
   （`CanSprintInAir` 是否应包含靴子 ✓），用密集轨迹里
   `wingTime==0 && !grounded && |vx| < accRunSpeed` 且输入同向的帧逐帧读出 ✓。

**判据**：`air` 首帧 `modelVX` 从 4.88 升到 ~7.7 ✓；
`VELOCITY` 的 `air`/`ground` 的 `absVX` 从 1.3/2.95 降到 <1 ✓。

## C74. **找到测试侧 `BuildFrame` 与生产侧不一致（`MaxRunSpeed` 取 `maxRunSpeed` 而非 `max(maxRunSpeed, accRunSpeed)`）；修复已施加但**未传播**（对照无变化）**

### 最后嫌疑核对：`SprintAcceleration` 与上限来源

`ForwardModelTraceTests.BuildFrame`（第 293–320 行）实测读取：

```csharp
BaseRunSpeed       = Player(row, "maxRunSpeed"),        // 4.71
RunAcceleration    = Player(row, "runAcceleration"),
SprintAcceleration = Player(row, "dashDelay") < 0f
                       ? 0f
                       : runAcceleration * (wingsLogic > 0f ? .4f : .2f),
RunSlowdown        = Player(row, "runSlowdown"),
CanSprintInAir     = wingsLogic > 0f,
```

### ⇒ **真正的根因** ✓✓：测试侧把上限取成了 `maxRunSpeed`

| | 测试侧 `BuildFrame`（原） | 生产侧 |
|---|---|---|
| `MaxRunSpeed` | `maxRunSpeed` = **4.71** ✗ | `max(maxRunSpeed, accRunSpeed)` = **8** ✓ |

⇒ `HorizontalMotion.topSpeed = Math.Max(baseSpeed, MaxRunSpeed) = 4.71` ✗
—— 而引擎的 `accRunSpeed = 8` ✓✓
⇒ **模型的水平天花板被钉在 4.71**，正是观测到的 ~4.7 ✓✓✓

**⇒ 这是测试侧与生产侧的实质性不一致** ✓：
所有审计数字、以及**整条组合路线的搜索**，
都是用**一个跑不到引擎速度的模型**做的 ✓✓
—— 这很可能是组合路线在引擎里总是死的一个重要原因 ✓。

### 修复已施加，但**对照无变化** ✗

在两处 `MaxRunSpeed`（第 293 行与第 397 行）改为
`Math.Max(Player(row, "maxRunSpeed"), Player(row, "accRunSpeed"))` ✓
⇒ 重新审计，**全部数字逐位相同** ✗：

```
VELOCITY ground dVX=-2.9501  air dVX=-1.3253  wing dVX=-2.1149  dash dVX=-7.0121
NEUTRAL_SUMMARY ground modelDVX=-0.1833  air modelDVX=-0.0871
SINCE_RESYNC 0-9 mean=32.610 meanX=1.753 meanY=30.857
```

**⇒ 按铁律：修复未被执行** ✓ —— `PlayerForwardModel` 里的 `ToSnapshot(frame)`
必定**另行推导** `MaxRunSpeed`，而不是直接取 `frame.MaxRunSpeed` ✓。

### 下一轮（一步即可收口）

**读 `PlayerForwardModel.ToSnapshot`** ✓，确认它把 `PlayerSnapshot.MaxRunSpeed`
设成了什么 ✓；把上限接上 `max(maxRunSpeed, accRunSpeed)` ✓。

**判据（硬）**：
- `OVERCAP` 帧数从 **454 → 非零的新分支命中**，且 `VELOCITY` 的
  `air`/`dash`/`ground` 的 `absVX` **显著下降** ✓
- `air` 首帧 `modelVX` 从 4.8987 升到 ~7.7 ✓

**注意**：这条修复一旦生效，会**改变组合路线的搜索输入** ✓
⇒ 之后需要**重跑组合**（不是重跑已有路线）✓。

## C73. **更正 C72：冲刺残余速度**没有**丢失；`BuildFrame` 完整填充了冲刺状态**

### 追查路径（含一次自己的错误推断，已更正）

**第一步（错的）** ✗：在 `LoopSearchTraceTests.cs` 里 grep `Dash =`，
只找到 3 处（都是控制位）⇒ 我推断 `BuildFrame` **没有**构造冲刺状态 ✗。
**这个推断是错的** ✗ —— `BuildFrame` 根本不在这个文件里 ✓。

**第二步（对）** ✓：`BuildFrame` 定义在 `ForwardModelTraceTests.cs` 第 277 行 ✓，
且第 385–402 行**完整填充**：

```csharp
EocDash          = (int)Player(row, "eocDash"),
EocHit           = (int)Player(row, "eocHit"),
ControlDash      = PlayerBoolean(row, "controlDash"),
ReleaseDash      = PlayerBoolean(row, "releaseDash"),
VelocityX        = Player(row, "velocity", "x"),
VelocityY        = Player(row, "velocity", "y"),
AccRunSpeed      = Player(row, "accRunSpeed"),     // 8
MaxRunSpeed      = Player(row, "maxRunSpeed"),     // 4.71
HostileContactKnown = true,
HostileContact      = false,
```

⇒ `runSpeed = Math.Max(AccRunSpeed, MaxRunSpeed) = Math.Max(8, 4.71) = 8` ✓✓

### ⇒ 冲刺退出钳位**与引擎一致** ✓

`GravityDashMotion.cs` 第 448–452 行在冲刺衰减到 `runSpeed` 时钳到该值并结束冲刺 ✓
引擎在 tick 248 也是 `8.2997 → 8.0000` ✓
⇒ **两侧同行为** ✓ ⇒ **C72 的"残余速度没被带出冲刺"假设被否定** ✗✓

### ⇒ 超速分支不可达的原因**仍未找到**（诚实的开口）

已排除：
- ~~`AccRunSpeed` 未填充~~ ✗（已填充 ✓）
- ~~冲刺退出丢速度~~ ✗（钳位一致 ✓）
- ~~终端下落速度 / `Down`~~ ✗（C69 已否定 ✓）
- ~~衰减常量~~ ✗（C70 已证实一致 ✓）

**剩下的嫌疑**：`HorizontalMotion.Advance` 的**加速分支**条件
`forward < topSpeed && sprintAcceleration > 0f` ✓
⇒ 若 `SprintAcceleration` 为 0 ✗（`BuildFrame` 是否填充 `SprintAcceleration` 尚未核对 ✓），
则速度**只会停在 `topSpeed` 之下**，`forward > topSpeed` 永不成立 ✓。

### 下一轮（一步即可判定）

**核对 `BuildFrame` 是否填充 `SprintAcceleration`** ✓（以及 `CanSprintInAir` ✓）；
并**直接打印**模型在冲刺刚结束那一帧的 `dashDelay/velocityX` ✓，
与引擎同帧对照 ✓。

**判据**：`OVERCAP` 帧数从 0 变为非零（新分支开始被执行）✓；
`air` 首帧 `modelVX` 接近 7.7 ✓。

## C72. **超速规则已实现，但对照证明它**不可达**（按铁律：改动前后完全相同 = 未执行）**

### 实现

在 `HorizontalMotion.Advance` 增加两支：
```csharp
else if (forward > topSpeed) velocity = AboveTopSpeed(velocity, true, topSpeed);
...
else if (Math.Abs(velocity) > topSpeed) velocity = AboveTopSpeed(velocity, false, topSpeed);
```
`AboveTopSpeed` 复用冲刺的 `EyeShieldDashMotion.ApplyDashBleed`
（已把 `ApplyDashBleed`/两个 bleed 常量由 `private` 改为 `internal` ✓，避免重复魔数 ✓），
并按实测加上**下限 = `topSpeed`** ✓。

### 对照结果：**几乎完全没变** ✗

| regime | 改动前 dVX | **改动后 dVX** |
|---|---|---|
| ground | -2.9318 | **-2.9501** |
| air | -1.3283 | **-1.3253** |
| wing | -2.0971 | **-2.1149** |
| rocket | -1.6825 | **-1.6678** |
| dash | -6.9958 | **-7.0121** |

`NEUTRAL_SUMMARY` 也逐位相同 ✓
⇒ **按铁律判定：新分支未被走到** ✓✓

### 原因（已定位）

`topSpeed = Math.Max(baseSpeed, player.MaxRunSpeed)` ✓
而**模型的水平速度从未超过 `topSpeed`** ✗
—— 因为第 47 行的加速分支条件是 `forward < topSpeed` ✓
⇒ **模型只会加速到 `topSpeed` 为止**，`forward > topSpeed` 永不成立 ✗
⇒ 新增分支**不可达** ✓。

### ⇒ 真正缺的是"冲刺残余速度"

引擎的速度轨迹是：
**冲刺到 14.5** ✓ ⇒ 之后按**乘性规则**衰减（14.18→13.87→13.57…）✓
而**模型在冲刺结束后掉到 ~4.7** ✗（C69 的 `air` 首帧 `modelVX=4.8987` vs `engineVX=7.7067` ✓）

⇒ 缺的不是"超速时怎么衰减"（已实测 ✓），而是
**"冲刺结束的那一帧，模型没有把 14.5 的残余速度带进普通运动路径"** ✗✓

### 下一轮

追 `PlayerForwardModel` 里**冲刺结束**（`DashDelay` 由 −1 变为 0/正）那一帧的处理：
冲刺的 `VelocityX` 是否被**传递**给普通 `HorizontalMotion` ✓。

**判据**：`air` 首帧 `modelVX` 与 `engineVX` 接近（7.7 量级而非 4.9）✓；
且 `OVERCAP` 帧数从 0 变为非零 ✓（证明新分支**开始被执行** ✓）。

**注意**：`AboveTopSpeed` 目前是**正确但不可达**的代码 ✓。
保留它并在注释里写明"当前不可达"✓，不假装它是本轮的成果 ✓。

## C71. **突破：引擎在 `accRunSpeed` 以上用的就是已实测的冲刺乘性规则 —— 8 帧逐帧精确吻合（1e-4）**

### 引擎实测（`OVERCAP` 帧）

```
OVERCAP tick=242 grounded=False accRunSpeed=8.00 engineVX=13.5268->12.8795 d=-0.6473 ratio=0.952144 input=-1
OVERCAP tick=243 grounded=False accRunSpeed=8.00 engineVX=12.8795->12.2419 d=-0.6376 ratio=0.950493 input=-1
OVERCAP tick=244 grounded=False accRunSpeed=8.00 engineVX=12.2419->11.0832 d=-1.1586 ratio=0.905354 input=-1
OVERCAP tick=245 grounded=False accRunSpeed=8.00 engineVX=11.0832->10.3242 d=-0.7590 ratio=0.931519 input= 0
OVERCAP tick=246 grounded=False accRunSpeed=8.00 engineVX=10.3242-> 9.2806 d=-1.0436 ratio=0.898919 input=-1
OVERCAP tick=247 grounded=False accRunSpeed=8.00 engineVX= 9.2806-> 8.2997 d=-0.9810 ratio=0.894300 input=-1
OVERCAP tick=248 grounded=False accRunSpeed=8.00 engineVX= 8.2997-> 8.0000 d=-0.2997 ratio=0.963893 input=-1
OVERCAP tick=284 grounded=False accRunSpeed=8.00 engineVX=14.5000->14.1840 d=-0.3160 ratio=0.978207 input= 0
OVERCAP tick=285 grounded=False accRunSpeed=8.00 engineVX=14.1840->13.8727 d=-0.3113 ratio=0.978056 input= 1
OVERCAP tick=286 grounded=False accRunSpeed=8.00 engineVX=13.8727->13.5662 d=-0.3066 ratio=0.977900 input= 0
OVERCAP_SUMMARY frames=454 meanDelta=-0.3545 meanRatio=0.963443
```

**注意 `accRunSpeed = 8.00`**（不是先前 DIVERGE 行里的 6 ✓）。

### ⇒ 用**已实测的冲刺规则**逐帧复算，**8/8 精确吻合**

规则（`Chaite.Core` 里已用于冲刺的那一条）：
```
bleed = 0.4512  若输入与运动方向相反；否则 0.1
speed = |vx| - bleed            (下限 0)
decay = 0.985   若 speed > 12；否则 0.94
vx_next = sign * speed * decay
结果下限为 accRunSpeed
```

| tick | 输入 | 引擎 | 复算 | 结果 |
|---|---|---|---|---|
| 242 | −1 反向 | 13.5268→12.8795 | `(13.5268−0.4512)×0.985` | **12.8795** ✓ |
| 243 | −1 | 12.8795→12.2419 | `(12.8795−0.4512)×0.985` | **12.2419** ✓ |
| 244 | −1 | 12.2419→11.0832 | `(12.2419−0.4512)×0.94`（<12 换档） | **11.0833** ✓ |
| 245 | 0 中立 | 11.0832→10.3242 | `(11.0832−0.1)×0.94` | **10.3242** ✓ |
| 246 | −1 | 10.3242→9.2806 | `(10.3242−0.4512)×0.94` | **9.2806** ✓ |
| 247 | −1 | 9.2806→8.2997 | `(9.2806−0.4512)×0.94` | **8.2996** ✓ |
| 248 | −1 | 8.2997→**8.0000** | `7.8485` < 8 ⇒ **落回上限** | **8.0000** ✓ |
| 284 | 1 同向 | 14.1840→13.8727 | `(14.1840−0.1)×0.985` | **13.8727** ✓ |
| 285 | 1 | 13.8727→13.5662 | `(13.8727−0.1)×0.985` | **13.5662** ✓ |

**⇒ 全部精确到 1e-4** ✓✓✓

### ⇒ 完整规格（引擎实测，非推算）

**`|vx| > accRunSpeed` 时**：走上面这条**乘性**规则，结果**下限为 `accRunSpeed`**；
**`|vx| ≤ accRunSpeed` 时**：走普通 `MoveTowards(0, drag)`（C70 已证实两侧一致 ✓）。

**⇒ 这就是水平 bug 的完整修法** ✓：
`HorizontalMotion.Advance` 现在把 `topSpeed` 当硬上限、一到就按 `drag` 线性衰减 ✗，
应改为**上限以上用乘性规则、下限夹在 `accRunSpeed`** ✓。

### 下一轮

**在 `HorizontalMotion` 里实现这条规则**，并用**同一份密集轨迹**回归：
`OVERCAP` 帧的 `modelVX` 应与 `engineVX` 逐帧吻合 ✓。

**判据**：`NEUTRAL_SUMMARY` 与 `OVERCAP` 两支的 `absVX` 都降到 <1 px/tick；
`ROUTE MODEL VELOCITY` 的 `air`/`dash`/`ground` 的 `dVX` 绝对值降到 <1 ✓。
**注意**：这属于**物理模型改动**，按硬约束必须**用隔离探针**验证分支确实被执行 ✓。

## C70. **水平 bug 定位到 `HorizontalMotion` 的速度上限：衰减常量两侧完全一致，但模型的速度天花板太低**

### 测量：中性连续帧的逐帧 `vx` 变化（两侧对照）

```
NEUTRAL tick=724 grounded=False engineVX=7.7048->7.6048 d=-0.1000 modelVX=4.7461->4.6461 d=-0.1000
NEUTRAL tick=803 grounded=False engineVX=7.9005->7.8005 d=-0.1000 modelVX=4.7672->4.6672 d=-0.1000
NEUTRAL tick=812 grounded=False engineVX=7.8024->7.7024 d=-0.1000 modelVX=4.8199->4.7199 d=-0.1000
NEUTRAL tick=830 grounded=True  engineVX=6.8053->6.6053 d=-0.2000 modelVX=4.6251->4.4251 d=-0.2000
NEUTRAL tick=866 grounded=False engineVX=5.3531->5.2531 d=-0.1000 modelVX=4.7415->4.6415 d=-0.1000
NEUTRAL tick=954 grounded=True  engineVX=7.9010->7.8010 d=-0.1000 modelVX=4.8152->4.7152 d=-0.1000
NEUTRAL tick=961 grounded=True  engineVX=6.9014->6.7014 d=-0.2000 modelVX=4.7181->4.5181 d=-0.2000
--- 引擎减速更强的帧 ---
NEUTRAL tick=785 grounded=False engineVX=14.1840->13.8727 d=-0.3113 modelVX=4.8127->4.7127 d=-0.1000
NEUTRAL tick=794 grounded=False engineVX=11.0176->10.2625 d=-0.7551 modelVX=4.7146->4.6146 d=-0.1000
NEUTRAL tick=839 grounded=True  engineVX=12.4888->12.1044 d=-0.3843 modelVX=4.6299->4.4299 d=-0.2000
NEUTRAL tick=847 grounded=True  engineVX= 6.7480-> 6.1551 d=-0.5929 modelVX=4.6840->4.4840 d=-0.2000
--- 汇总 ---
NEUTRAL_SUMMARY ground frames=6  engineDVX=-0.2721 modelDVX=-0.1833
NEUTRAL_SUMMARY air    frames=31 engineDVX=-0.2145 modelDVX=-0.0871
```

### 结论 1：**衰减常量两侧完全一致** ✓✓

空中 `-0.1000`、地面 `-0.2000`，与引擎实测常量吻合 ✓
（`runSlowdown = 0.2`，空中中立 `runSlowdown × 0.5 = 0.1` ✓）
⇒ **衰减项是对的**，C69 的怀疑被否定 ✓。

### 结论 2：**模型的速度天花板太低** ✗✓ —— 这是水平 bug 的实质

| | 模型 | 引擎 |
|---|---|---|
| 典型速度 | **4.6–4.8** | **5.0–14.2** |
| 超速时减速 | 永远 `-0.1/-0.2` | `-0.31` / `-0.76`（**更强**） |

⇒ 模型的速度**卡在 ~4.7**，引擎能维持 **7.6–14** ✓✓

### 结论 3：机制在 `HorizontalMotion.Advance`（已读代码确认）✓

```csharp
var topSpeed = Math.Max(baseSpeed, player.MaxRunSpeed);   // 4.71 或 6
...
else if (forward < topSpeed && sprintAcceleration > 0f) { ... 加速 ... }
else velocity = MoveTowards(velocity, 0f, drag);          // <== 到了/超过上限就衰减
```

`maxRunSpeed = 4.71`、`accRunSpeed = 6` ✓
⇒ 引擎在 **`accRunSpeed` 以上仍能维持速度**（冲刺动量）✓，
而模型把 `topSpeed` 当**硬上限**，一到就转入衰减 ✗✓。

**⇒ 这解释了 C69 里 `ground` 中性帧 `modelVX=4.67` 而 `engineVX=7.61`** ✓
——模型早就掉到天花板，引擎还在 7.6 ✓。

### 下一轮

**按引擎实测规则修 `HorizontalMotion` 的上限行为**：
需要**引擎内实测**"速度在 `accRunSpeed` 以上时的行为"——
是保持（`d = -0.1` 持续）、还是按某个比例衰减（`0.985`/`0.94` 的乘性规则，
与已实测的冲刺规则同源）✓。
**不得推算**：用密集轨迹里 `vx > accRunSpeed` 的帧逐帧读出 ✓。

**判据**：中性帧 `modelVX` 与 `engineVX` 的**绝对值**一致（不只是逐帧增量一致）；
`ground`/`air` 两支的 `absVX` 降到 <1 px/tick ✓。

## C69. **两个缺陷的签名完全不同：垂直是"逐帧累积"，水平是"入 regime 即偏移"**

### 首个 air / dash / wing / rocket / ground 帧的逐字段对照

```
FIRST dash   tick=241 action=(-1,1,0) dVX= 0.0000 dVY=0.0000 modelVX=13.5268 engineVX=13.5268 wingTime=180 dashDelay=-1 eocDash=15
FIRST wing   tick=248 action=(-1,1,0) dVX= 0.0000 dVY=0.0000 modelVX= 8.0000 engineVX= 8.0000 wingTime=180 dashDelay=30 eocDash=15
FIRST rocket tick=387 action=(-1,1,0) dVX=-0.0502 dVY=0.0000 modelVX=-5.7987 engineVX=-5.7485 wingTime=180 dashDelay=22 eocDash=7 rocketTime=7
FIRST air    tick=731 action=( 1,1,0) dVX=-2.8080 dVY=0.0000 modelVX= 4.8987 engineVX= 7.7067 wingTime=0   dashDelay=0  eocDash=0
FIRST ground tick=825 action=( 0,0,0) dVX=-2.9318 dVY=0.0000 modelVX= 4.6735 engineVX= 7.6053 wingTime=0   dashDelay=0  eocDash=0
```

（五帧的 `maxFall=10.01`、`gravity=0.400`、`down=0`、`up=0` **两侧完全一致** ✓
⇒ **不是终端下落速度、也不是 `Down` 的问题** ✗ —— C68 的两个猜测都被否定 ✓）

### ⇒ 两个缺陷的签名**完全不同**，指向不同的物理项

**① 垂直：每个 regime 的**首帧** `dVY = 0.0000`** ✓

⇒ 垂直误差**不是入 regime 时的偏移**，而是**在 regime 内逐帧累积** ✓
⇒ 是**加速度项**不符（`absVY` 在 air 上达 7.77 ✓），不是初速不符 ✓。

**② 水平：`air` 首帧即 `dVX = −2.81`** ✗

更关键的是 **`ground` 帧**：`action=(0,0,0)`（**中性输入**）时
`modelVX = 4.6735` 而 `engineVX = 7.6053` ✗
⇒ 模型在**无输入**时减速得比引擎**快 2.93 px/tick** ✓
⇒ 疑似**动量/减速项**：引擎保留了更高的速度（例如 `accRunSpeed` 的冲刺动量），
而模型一帧就掉到了较低值 ✓。

### 下一轮

**先修水平（签名更清楚）**：在**中性输入**的连续帧上对照 `vx` 的**逐帧衰减量**，
与引擎实测常量核对（`runSlowdown = 0.2`、`runAcceleration = 0.2512`、
空中中立 `runSlowdown × 0.5 = 0.1`）✓
⇒ 读出模型用的是哪个衰减量、引擎用的是哪个 ✓。

**再修垂直**：在**同一 regime 内连续帧**上对照 `vy` 的**逐帧增量**，
与引擎实测常量核对（重力 `+0.400`、翅膀下坠 `-0.875`、上升带内 `-0.275`、
带下 `-0.125`、`ctlJump && wingTime==0 && vy<0` ⇒ `+0.4`）✓

**判据**：中性帧的逐帧 `dvx` 衰减量两侧一致；
`air` 的 `absVY` 降到 <1 px/tick；随后 `SINCE_RESYNC 0-9` 的 `meanY` 显著下降 ✓。

## C68. **按分支定位成功：只有两支错（air 的 dVY=−7.77、dash 的 dVX=−7.00），wing/rocket/ground 几乎完美**

### 测量：速度误差按 regime 拆分

```
ROUTE MODEL VELOCITY ground frames=1   dVX=-2.9318 dVY= 0.0000 absVX=2.9318 absVY=0.0000
ROUTE MODEL VELOCITY air    frames=305 dVX=-1.3283 dVY=-7.7682 absVX=1.3283 absVY=7.7682
ROUTE MODEL VELOCITY wing   frames=640 dVX=-2.0971 dVY=-0.6581 absVX=2.0971 absVY=0.6581
ROUTE MODEL VELOCITY rocket frames=28  dVX=-1.6825 dVY= 0.0000 absVX=1.6825 absVY=0.0000
ROUTE MODEL VELOCITY dash   frames=314 dVX=-6.9958 dVY=-1.0636 absVX=6.9958 absVY=1.0636
```

### ⇒ 只有两支错，且错得干净利落

| regime | frames | 判定 |
|---|---|---|
| **air**（无翅膀空中） | 305 | **dVY = −7.77** ✗ 垂直速度偏低（落得**太快**） |
| **dash**（冲刺进行中） | 314 | **dVX = −7.00** ✗ 水平速度偏低（冲得**太慢**） |
| wing | 640 | absVY = 0.66 ✓ **几乎完美** |
| rocket | 28 | absVY = 0.00 ✓ **完美** |
| ground | 1 | absVY = 0.00 ✓ **完美** |

**⇒ 翅膀、火箭靴、地面三支的垂直物理是对的** ✓✓
（这否定了"§C 说翅膀 1275 帧未覆盖"的字面解读：**翅膀分支并不坏**）

### 两个缺陷的候选原因（下一轮逐一核对）

**① air 的 dVY = −7.77**

引擎实测的**终端下落速度**有两个值：
按 `Down` 时为 **10.01**，松开 `Down` 时为 **3.33**（见 `ObservedControls` 的注释）✓。
`10.01 − 3.33 = 6.68` 与 **7.77** 接近 ✓
⇒ 疑似**终端下落速度取错**，或**模型对 `Down` 的处理与引擎相反** ✓。
注意：`RouteAction` 字母表**没有 Up/Down**（已知缺口）✓
⇒ 若引擎当时按着 `Down`，模型无法表达 ⇒ 必然偏差 ✓。

**② dash 的 dVX = −7.00**

冲刺起始速度引擎实测 **14.5** ✓。
`14.5 − 7.0 = 7.5` ⇒ 模型实际给出的水平速度约为引擎的一半 ✓
⇒ 疑似**冲刺速度没有按 14.5 应用**，或冲刺该帧被走到了**普通加速分支** ✓。

### 下一轮

对**首个 air 帧**与**首个 dash 帧**打印逐字段对照
（`controlDown/controlUp`、`maxFallSpeed`、`dashDelay/eocDash`、模型与引擎的 vx/vy）
⇒ 直接读出是哪一个物理项取错 ✓。

**判据**：`air` 的 `absVY` 与 `dash` 的 `absVX` 降到与 wing 同量级（<1 px/tick）✓；
随后 `SINCE_RESYNC 0-9` 的 `meanY` 应显著下降 ✓。

## C67. **§C 的真实缺口被量化并定位：逐 tick 垂直速度偏差（~3 px/tick），且与路线无关**

### 测量：误差 = f(距上次重新同步的 tick 数)

```
ROUTE MODEL SINCE_RESYNC  0-9   mean=32.532  meanX=1.675   meanY=30.857
ROUTE MODEL SINCE_RESYNC 10-19  mean=101.135 meanX=10.368  meanY=91.641
ROUTE MODEL SINCE_RESYNC 20-29  mean=193.195 meanX=23.000  meanY=174.957
ROUTE MODEL SINCE_RESYNC 30-39  mean=294.045 meanX=50.110  meanY=259.289
ROUTE MODEL SINCE_RESYNC 40-49  mean=341.090 meanX=70.418  meanY=278.469
ROUTE MODEL SINCE_RESYNC 50-59  mean=420.853 meanX=100.358 meanY=320.495
ROUTE MODEL SINCE_RESYNC 60-69  mean=562.147 meanX=156.502 meanY=405.782
ROUTE MODEL SINCE_RESYNC 70-79  mean=768.533 meanX=205.317 meanY=563.217
ROUTE MODEL SINCE_RESYNC 80-more mean=1604.416 meanX=1273.299 meanY=349.277
```

### 结论 1：**近乎直线** ⇒ 逐 tick 系统性偏差（C66 的选项 1）✓

前 80 tick 每 10 tick 约 +100 px ⇒ **每 tick 约 10 px 的位置偏差** ✓
⇒ 不是"少数坏 regime"（那会是阶梯）✗。

### 结论 2：**偏差以垂直 Y 为主，且从最初 10 tick 就存在** ✓✓

`0-9` 桶：`meanY = 30.857` 而 `meanX = 1.675` ✓
⇒ **垂直速度从第 1 tick 起就有约 3 px/tick 的系统性偏差** ✓✓

这是 §C"评估器缺口"的**真实、可定位**的形态：
不是"某些 regime 未覆盖"，而是**垂直物理项有一个恒定的速度偏置** ✓。

### 结论 3：**漂移与路线无关** ✓

观测路线与组合路线的 `SINCE_RESYNC` 曲线**几乎逐桶相同**：

| 桶 | 观测 mean | 组合 mean |
|---|---|---|
| 0-9 | 32.532 | 33.522 |
| 20-29 | 193.195 | 170.914 |
| 40-49 | 341.090 | 325.558 |
| 70-79 | 768.533 | 773.469 |

⇒ 漂移是**模型自身的属性**，不是搜索路线造成的 ✓
⇒ 这也**最终解释**了为什么组合路线在引擎里总是死：
**模型只在前 10–20 tick 可信**，而闭环长度是 60–110 tick ✓✓。

### ⇒ 下一轮（目标明确）

**定位垂直偏置的来源**：逐 tick 对照 `velocityY`，按 regime 拆分
（`wingTime>0` / 自由落体 / 火箭靴 / 地面），找出**哪一个分支**产生 3 px/tick 的偏差 ✓。

已有的引擎实测常量（重力 `+0.400`、翅膀下坠 `-0.875`、上升带内 `-0.275`、
带下 `-0.125`、`ctlJump && wingTime==0 && vy<0` ⇒ `+0.4`）可逐一核对 ✓。

**判据**：`SINCE_RESYNC` 的 `0-9` 桶 `meanY` 从 30.9 降到个位数 ✓；
以及闭环长度内（约 60–110 tick）的误差降到可接受范围 ✓。

## C66. **分类器改用引擎语义（`dashDelay < 0`）；误差变成全 regime 均匀 ⇒ 漂移是"段内累积"而非 regime 特有**

### 从引擎实测实现里取出确切语义（不再猜字段）

`GravityDashMotion.cs` 里 `EyeShieldDashMotion` 的**不变式**：

| 状态 | 条件 |
|---|---|
| **冲刺进行中** | `DashDelay == -1` 且 `EocDash == 15` |
| 就绪（非冲刺） | `DashDelay == 0` 且 `EocDash == 0` |
| 冷却期 | `DashDelay > 0` 且 `EocDash == max(0, DashDelay − 15)` |

⇒ **"正在冲刺" = `dashDelay < 0`** ✓
—— 此前两版**正好反了**：先读 `dash` 字段（冲刺类型，常量 2 ✗），
再读冷却期当冲刺 ✗。

### 修正后帧数确实大变（证明改动执行）✓

| regime | 旧帧数 | **新帧数** |
|---|---|---|
| wing | 138 | **640** |
| dash | 953 | **314** |
| air | 194 | 305 |
| rocket | 3 | 28 |
| ground | 0 | 1 |

### 但误差变成**全 regime 均匀** ✗

| regime | frames | over4 | mean | max |
|---|---|---|---|---|
| ground | 1 | 1 | 987.608 | 987.608 |
| air | 305 | 305 | 1459.565 | 2369.308 |
| wing | 640 | 471 | 914.277 | 2860.624 |
| rocket | 28 | 27 | 1235.618 | 1484.357 |
| dash | 314 | 307 | 1140.961 | 2857.188 |

**⇒ 每个 regime 都是 ~900–1500 px** ⇒ 漂移**不是某个 regime 的问题** ✓，
而是在**两次受击之间的整段**里累积 ✓。

**⇒ 同时更正 C64 的"wing 吻合"**：那个 `186.96` 是在**被误分类的小子集**上得到的
（`wing` 当时只有 138 帧，且不含冷却期帧）⇒ **吻合是巧合**，不是验证 ✓。
真正的验证是：**同一份数据、同一个 regime 定义下两条独立路径给出同一个数**，
这一条**尚未做到** ✓。

### ⇒ 下一步：把误差表成"距上次重新同步的 tick 数"的函数

均匀误差有两种可能，必须分辨：

1. **每 tick 系统性偏差**（某个物理项错）⇒ 误差随段长**线性**增长 ✓；
2. **少数几个坏 regime** ⇒ 误差在特定帧**跳变** ✓。

做法：对每个未受击段，记录 `段内第 k tick` 的误差，按 `k` 聚合 ✓。
判据：误差-`k` 曲线是**直线**（⇒ 逐 tick 偏差，可定位到具体物理项）
还是**阶梯**（⇒ regime 相关）✓。

**在分辨清楚之前，不下"模型在搜索路线上失效"的结论** ✓。

## C65. **击退宽限期尝试失败并已回退；分类器仍有同类误判（`eocDash`）**

### 尝试：把重新同步从"无敌帧行"扩展到"无敌帧后 24 tick"

**结果：更糟** ✗

| regime | 无宽限期（C64） | **加 24 tick 宽限期** |
|---|---|---|
| air mean | 1912.002 | **8822.533** ✗ |
| air max | 2369.308 | **15872.380** ✗（**比场地还宽**） |
| wing mean | 186.963 | 234.789 |

**原因**：无敌帧结束时**击退速度仍在衰减** ✓ ⇒ 在那一刻重新同步，
等于把模型放到一个它**无法复现的速度**上 ⇒ 之后的漂移又被记成模型误差 ✓。

**⇒ 已回退**（`KnockbackGrace = 0`），并把这次失败与原因写进常量注释 ✓。
回退后 `wing mean = 186.963` 恢复，与独立实现的 `187.39` 重新吻合 ✓。

### 新发现：`eocDash` 与 `dash` 是同一类误判

obf 轨迹的实测行：

```
t=522 dash=2 eocDash=15 dashDelay=-1   <- 不在冲刺
t=525 dash=2 eocDash=9  dashDelay=29   <- 在冲刺
```

⇒ `eocDash` 是**护盾冲刺计数器**，在**非冲刺时段也长期为 15** ✗
⇒ 当前判据 `dashDelay > 0 || eocDash > 0 || dashTime > 0` 会把大量非冲刺帧判成 dash ✗
（`dash frames=953` 而真正在冲刺的只有一小部分）
—— **与 C63 里 `dash` 字段（冲刺类型，常量 2）是同一类错误** ✓。

### ⇒ 下一轮

**停止猜字段，改用引擎语义定义 regime**：
在 `TerrariaFacade` / 插件侧确认**哪个字段真正表示"正在冲刺"**
（`Player.dashTime > 0` 或 `Player.eocDash > 0` 的语义），
再重做分类 ✓。判据：`dash` 帧数与引擎轨迹里**实际处于冲刺**的帧数一致，
且 `air` 的 mean 降到与 `wing` 同量级（几十到一两百像素）。

在分类可信之前，这份 regime 清单**不能**用来定位 §C 的真实缺口 ✓。

## C64. **审计工具查错：三处 bug；wing regime 已与独立实现精确吻合（187 px）**

### 对照实验（C63 要求的"先排除测量问题"）

用**观测路线**跑同一审计：先由轨迹的控制序列导出 `observed-route-*.csv`
（逆映射 `controlRight/Left/Jump/Dash → direction,jump,dash`），
再用 `--route-model-audit <route> <trace> <startTick>` 审计。

**结果：同一工具在观测路线与组合路线上报出几乎相同的巨大误差**
⇒ **问题在工具，不在模型** ✓（C63 的怀疑成立）。

### 查出的三处工具 bug

**bug 1：`floor` 取错** ✗

本文件其它地方的 `floor` = **前 240 行**里 `position.y + height` 的**最大值**（地面高度）✓；
审计里写成了 `rows[0].position.y` ✗ ⇒ 模型永远不认为自己在站地 ⇒ 误差爆炸。

**bug 2：没有受击门禁** ✗

`AuditObservedRoutes` 只在**首次受击前**累计误差 ✓，审计**一直累计** ✗
⇒ 把击退算成模型误差（与 C62 同一个教训，在同一文件里又犯一次）。

**bug 3（真正的主因）：受击后模型**永久**偏离，必须重新同步** ✗

证据：误差**几乎恒定**（`mean=3219`、`max=3470`，只差 8%）✗
⇒ 不是物理误差，是**固定位移** ✓。
原因：击退把玩家**永久**挪走，模型再也追不上 ⇒ 受击之后每一帧都在测**那一次位移** ✓。

修法：在无敌帧行**从引擎状态重新同步**模型 ✓，而不是跳过 ✓
⇒ 变成"逐段（两次受击之间）测量" ✓，这才是模型真正要负责的范围。

### 修复后的结果

| regime | frames | over4 | mean | max |
|---|---|---|---|---|
| ground | 0 | 0 | 0.000 | 0.000 |
| air | 194 | 194 | 1912.002 | 2369.308 |
| **wing** | 138 | 38 | **186.963** | 2739.626 |
| rocket | 3 | 2 | 665.363 | 999.110 |
| dash | 953 | 877 | 1075.998 | 2860.624 |

**⇒ wing regime 的 `mean = 186.96 px` 与独立实现 `AuditObservedRoutes` 的
`maxError = 187.39 px` 精确吻合** ✓✓
—— 两条完全独立的路径给出同一个数 ⇒ **工具方向正确** ✓。

### 仍未解决（下一轮）

`air`(1912) 与 `dash`(1076) 仍大 ✗，且 `air` 的 mean/max 比值同样接近 1
⇒ 疑似**仍有击退泄漏**：无敌帧结束后击退**速度**仍在，
而重新同步只发生在无敌帧行上 ✓ ⇒ 需要在无敌帧**结束后的若干 tick**也重新同步，
或按"击退速度衰减完毕"判定段落边界。

**判据**：`air` 与 `dash` 的 mean 也降到与已知量级一致（几十到一两百像素），
之后这份 regime 清单才可用来定位真实缺口 ✓。

## C63. **新工具：在"搜索产出的路线"上按 regime 测模型偏离；首次测量量级可疑**

### 新增 CLI：`--route-model-audit <route.csv> <trace.jsonl>`

在**搜索产出的路线**（而非观测路线）上测前向模型 ✓：

- 运动**不依赖威胁场**（只依赖控制与玩家自身状态）
  ⇒ 可**完全不用威胁场**沿路线推进模型，再与**引擎跑同一路线**的轨迹逐 tick 对照 ✓；
- 起始 tick 由轨迹里的 `plan.replayFrame == 0` **直接读出**，不靠 tick 偏移推算 ✓；
- 按**引擎当时所处 regime** 分类（ground / air / wing / rocket / dash）✓
  ⇒ 把"一个误差数字"变成"一份可动手的清单" ✓。

### 首次测量（fwd4 组合路线 × obf 引擎轨迹）

```
ROUTE MODEL AUDIT actions=4212 rows=2041 startTick=240
ROUTE MODEL AUDIT walked=1801 refused=0 firstOver4Tick=264 firstOver4Regime=dash
ROUTE MODEL REGIME ground  frames=0    over4=0    mean=0.000    max=0.000
ROUTE MODEL REGIME air     frames=571  over4=571  mean=4923.127 max=5950.616
ROUTE MODEL REGIME wing    frames=199  over4=199  mean=605.290  max=4407.633
ROUTE MODEL REGIME rocket  frames=3    over4=3    mean=1607.103 max=2305.651
ROUTE MODEL REGIME dash    frames=1028 over4=1004 mean=2517.417 max=4861.192
```

### ⇒ 量级本身可疑，**先排除测量问题再下结论**

`mean ≈ 4900 px` 是**整个场地长度** ✗。
真实的物理误差应是**几十像素**（地面接触精度实测 0.37 px）✓。
⇒ 这个数字**不像物理误差，像对齐/初值错误** ✓（例如动作索引与 tick 差一格、
或初值取自错误的行）。

**因此本轮不下"模型在搜索路线上失效"的结论** ✓。
下一轮**先验证对齐**：

1. 用**观测路线**跑同一个审计（对照 `AuditObservedRoutes` 的 `maxError=0`）
   —— 如果同一个工具在观测路线上也报几千像素，就是工具的 bug ✓；
2. 若观测路线正常，再查组合路线的 `replayFrame` 对齐与初值行；
3. 对齐确认后才用这份 regime 清单定位真实缺口。

**判据**：同一工具在**观测路线**上必须复现 `maxError ≈ 0`（C62 的结论）。

### 本轮修掉的工具 bug（都有"改动前未执行/崩溃"的证据）

- `Object`/`Number` 对**缺失键抛异常** ⇒ 轨迹里只有接管后的行带 `plan.replayFrame`
  ⇒ 审计在**第一行**就崩，且异常自身 `ToString` 也失败（无法打印）✗
  ⇒ 改用 `TryGetValue` ✓；
- 引擎的 `dash` 字段是**冲刺类型（常量 2）**，每帧都 > 0
  ⇒ 用它分类会把**全部 1801 帧**判成 dash ✗ ⇒ 只用 `dashDelay/eocDash/dashTime` 计数 ✓；
- `dashDelay >= 0` 对**缺失键**（读作 0）恒真 ⇒ 改严格 `> 0` ✓。

## C62. **更正 C61：DIVERGE 行全部是击退，不是模型缺口；模型在未受击区间零偏离**

### C61 的"冲刺缺口"判断被证伪

逐 tick 打开引擎轨迹后：

**tick 407**（被 C61 当成"符号相反"的模型错误）：

```
t=406 life=318 dLife=0     vx=-7.56 vy=-6.56 imm=0
t=407 life=236 dLife=-82   vx=+4.50 vy=-3.50 imm=40   <- 受击 + 无敌帧
t=408 life=386 dLife=+150  vx=+4.05 vy=-3.10 imm=39
```

⇒ 速度翻转是**击退** ✓，模型的 `-7.66` **是对的**（延续受击前轨迹）✗ C61 判断错误。

**tick 525**（被 C61 当成"冲刺未发起"）：

```
t=524 life=392 dLife=0 vx=12.97 eoc=15 dashDelay=-1 imm=0
t=525 life=392 dLife=0 vx=-9.00 eoc=9  dashDelay=29 imm=4   <- 无生命下降，imm=4
```

⇒ `dLife=0` **不是受击**，但 `imm=4` ⇒ **仍在更早那次受击的击退/无敌窗口内** ✓
⇒ 同样不是冲刺缺口 ✗。

**并且模型本来就处理冲刺发起** ✓：
`PlayerForwardModel` 用 `EyeShieldDashMotion.TryCreateDedicatedCandidate` 起跳冲刺 ✓。

### 根因：`DIVERGE` 打印**漏了受击门禁**

`maxError` 有 `hitAt == 0` 门禁 ✓，但 `DIVERGE` 打印**没有** ✗
⇒ 击退被当成"模型偏离"报出来 ✓。

修法：给 `DIVERGE` 打印加同样的 `hitAt == 0` 门禁 ✓。

### 修正后的测量（fwd4 轨迹）

```
DIVERGE index= 行数：0
LOOP COMPOSE RESIDUAL firstDivergenceTick=240 worstGap=5680.69 walked=3917
```

⇒ **在 fwd4 上，模型在任何"未受击区间"内都没有超过 4 px 的偏离** ✓✓
⇒ **前向模型在观测路线上是精确的**（与 C53 的 `maxError=0` 一致，
之前所有"模型偏离"的报告都是击退污染）✓

（剩下那条 `DIVERGE tick=240` 是**组合级**对照：组合路线 vs 源轨迹在接管 tick，
即 C54 已记录的"第一 tick 就偏离"，与模型精度无关 ✓。）

### ⇒ 对 §C 的影响

§C 说"评估器缺口 = 翅膀/冲刺/跳跃/火箭靴未覆盖"。
本轮**排除了一条错误线索**（冲刺状态还原不是问题），
但**没有**证明模型已完备：这里只测了**观测路线**，
而搜索要走的路线远超观测路线。真正的缺口要靠**搜索路线上的偏离**来定位。

### 下一轮

在**搜索产出的路线**上测模型偏离（而不是观测路线）：
把组合路线的模型轨迹与引擎轨迹逐 tick 对照，按 regime 分类统计
（翅膀/冲刺/跳跃/火箭靴），得到**真实**的未覆盖清单。

## C61. **obf 轨迹存在"威胁不完整"的硬边界；DIVERGE 行给出前向模型的具体缺口**

### 观测分支的"回落"已实现，但对本轨迹无效

新增：搜索无干净分支时**回落到观测路线**并把受击计入 `composedHits`
（`LOOP COMPOSE FALLBACK index=<i> hits=<n>`），不再中断整场组合。

**但它在 obf 上没触发**，原因是**更硬的问题**：

```
LOOP COMPOSE FAIL index=26 ticks=350 branches=48 expanded=46007
LOOP COMPOSE FAIL REFUSAL ThreatIncomplete=22992
LOOP COMPOSE FAIL OBSERVED down=0 up=0 jump=7 horizontal=11 hitRows=340 rows=351
```

⇒ 该闭环窗口里**有被省略的射弹**（`threat.Omitted > 0`）⇒ `TryStep` 返回
`ThreatIncomplete` ⇒ **搜索和观测分支都无法推进** ✓
⇒ 组合在 loop 26 停止（`crossed=26 failedAt=26 complete=False`，不导出）。

**这是"轨迹数据完整性"边界，不是枚举逻辑问题** ✓：
`fwd4` 轨迹能跨完 51 个闭环，`obf` 轨迹不能跨第 26 个。

### DIVERGE 行：前向模型的**具体**缺口（可行动）

组合在偏差超阈值时打印模型与引擎的逐项对照。本轮捕获到的（**全是横向速度**）：

| 闭环 | tick | error | 引擎 VX | 模型 VX | 关键差异 |
|---|---|---|---|---|---|
| 0 | 252 | 4.08 | 6.9976 | 6 | `dashDelay=30` vs `engineDashDelay=27` |
| **2** | 407 | 12.61 | **4.5** | **−7.66** | **符号相反** ✗ |
| **4** | 525 | 21.85 | **−9** | **12.67** | **符号相反** ✗ |
| 5 | 586 | 8.79 | 14.5 | 5.71 | 引擎在冲刺速度 14.5，模型没跟上 |

⇒ **横向速度出现"符号相反"**，且都出现在**受击闭环**里；
伴随 `dashDelay/eocDash/dashTime` 的模型值与引擎值不一致
⇒ **冲刺状态的还原是前向模型的主要缺口**（与 §C 的"冲刺 1220 帧未覆盖"一致）。

### 下一轮

按 §C 的优先级**补前向模型的冲刺状态还原**：
用已实测的冲刺常量（`bleed = 0.4512/0.1`、`decay = 0.985/0.94`、起始速度 14.5、冷却 30）
把 `dashDelay / dashTime / eocDash` 纳入模型状态，
并以**这些 DIVERGE 行**作为改动是否执行的判据（error 是否下降）。

**判据**：DIVERGE 行的 `error` 下降；整场引擎 hits 从 5 继续下降。

## C60. **修掉越界 bug；观测分支把整场组合从 hits=8 降到 5，46/51 闭环干净**

### bug 3：观测分支在最后一个闭环越界

`LOOP SEARCH FAILED 索引超出范围` —— 观测分支代码直接索引 `threatRows[tick]`，
而**分解的最后一个闭环的 `EndTick` 可以超出轨迹实际行数** ✗
（其它所有遍历都带 `row < threatRows.Count` 保护，这里漏了）。
后果：组合跑到第 50 个闭环**整场崩掉**，白跑 11 分钟且**不导出任何东西**。

修法：加同样的边界保护，越界即视为"未到达" ✓。

### 修复后：整场组合完成，且路线真的变了

```
LOOP COMPOSE segments=51 crossed=51 failedAt=-1 complete=True routeTicks=4212
route000 lines=4213 hash=C8BD66ED1B525AEB7F4F83E7670F4440
```

- `routeTicks` **4153 → 4212**（多 59 个动作）✓
- 哈希 `394C1300…` → `C8BD66ED…` ⇒ **观测分支确实改变了路线** ✓
- 耗时 668 s（与改动前相同 ⇒ 不是变慢；之前是 `Select-String` 缓冲造成的误判）

### 引擎验证

| 路线 | 结果 |
|---|---|
| 脚本单独（无接管） | **win** hits=4 |
| 全组合（**无**观测分支） | loss hits=8 ticks=1634 deaths=1 |
| **全组合（含观测分支）** | **loss hits=5 ticks=2040 deaths=1** ✓ |
| 5 闭环（含观测分支） | **win** hits=6 |

⇒ 整场受击 **8 → 5**，是整场组合至今最好的结果 ✓。

### 逐闭环引擎 hits（用新轨迹统计，无需新探针）

```
loops with engine hits > 0 : 5 / 51
bad loops: loop 4=1, loop 6=1, loop 7=1, loop 21=1, loop 24=1
total engine hits in windows: 5   (engine reported hits=5)
```

- **46 / 51 闭环引擎实测 0 受击** ✓（C56 时是 33/40）
- 逐闭环合计 **精确等于**引擎报告（5 = 5）✓ ⇒ 统计方法自洽
- 只剩 **5 个坏闭环**：`4, 6, 7, 21, 24`

### 下一轮

只修这 5 个闭环：以**该闭环所在探针轨迹的窗口**为威胁场重新枚举，
逐闭环探针验证（判据：窗口内引擎 hits = 0），拼回整场。
**判据**：整场引擎 hits 从 5 降到 **< 4**（脚本基线），最终 0。

## C59. **观测路线纳入枚举空间 ⇒ 组合路线首次获胜（win hits=6）**

### 实现（属于"枚举空间的定义"，不是手写偏好）

在每个闭环的搜索**开始前**，把**轨迹自己的控制序列**（`ObservedControls` 的逆映射）
作为**额外分支**种入 frontier：

- 逆映射：`Direction = Right?1:(Left?-1:0)`、`Jump`、`Dash`；
- **Up/Down 不可表达**（字母表缺口，已知）⇒ 该分支继承此缺口，不绕过它；
- 逐 tick 走 `world.TryStep`，全部成功且无受击才作为分支加入；
- 加入后**照常**由 `perBranch` 轮转分配目标、由引擎裁决 ⇒ **不是偏好，是空间扩充** ✓。

输出：`LOOP COMPOSE OBSERVEDBRANCH loop=<i> added=<n>`
（`loop=0 added=1`、`loop=1 added=40`、`loop=2 added=47`、`loop=3 added=48` ⇒ **确实执行** ✓）

### 验证一：5 闭环组合 + 隔离探针

```
LOOP COMPOSE LIMITED loops=5
LOOP COMPOSE segments=51 crossed=5 composedTicks=336 routeTicks=336
route000 lines=337
```

探针 `game-probe-rt-ob5-0919-011111`：

```
status=win ticks=4813 hits=6 deaths=0 bossLife=0 damage=78000 shots=23
```

### ⇒ 这是组合路线**第一次获胜**

| 路线 | 结果 |
|---|---|
| 脚本单独（无接管） | **win** hits=4 |
| 全组合 48 候选（旧，丢弃观测路线） | **loss** hits=8 deaths=1 |
| **5 闭环 + 观测分支（本轮）** | **win** hits=6 deaths=0 ✓ |

⇒ 把观测路线放进搜索空间后，组合路线**不再是必死**，
且受击从 8 降到 6（脚本基线 4）✓。

### 待解决

**全 51 闭环 + 观测分支的组合明显变慢**（后台跑完但未导出 ⇒ `crossed < 51`），
需要查是**慢**还是**中途失败**。下一轮先查这一点，再逐步加长闭环数。

**判据**：`crossed=51 complete=True` 且整场引擎 hits **< 4**（脚本基线），最终 0。

## C58. **逐闭环迭代跑通；修掉两个真 bug；但迭代不收敛（组合路线比脚本差）**

### 修掉的两个真 bug（都用隔离探针证明"改动前未执行"）

**bug 1：`chain` 被计算并报告，却从未被使用** ✗

主循环写的是 `for (index = 0; index < segments.Count; ...)`，
而 `chain`（筛选/限制后的闭环表）根本没参与组合。
证据：设 `CHAITE_COMPOSE_LOOPS=2` 后 `SEGMENTS loops=2`、但 `crossed=51 routeTicks=4153`
⇒ 限制对路线**毫无影响**。改为 `chain` 后：`crossed=2 routeTicks=162` ✓。

**bug 2：导出被 `crossed == segments.Count` 卡住** ✗

限制组合时 `crossed=1 != 51` ⇒ **不导出**，而路线文件**只在导出时覆盖**
⇒ 探针读到的是**上一次遗留的整场路线**。
证据：一次单闭环组合报告 `crossed=1 routeTicks=104`，
但 `composed-route-000.csv` 仍是 **4154 行**。改为 `chain.Count` 后：**105 行** ✓。

> **这也修正了 C57 的部分叙述**：路线逐字节相同，**既**因为坏闭环没有输出，
> **也**因为限制根本没生效。两件事都真，现在都能分辨了。

### 逐闭环迭代（`_chaite-tools\iterate-loops.ps1`）

架构：组合前 N 个闭环 → 探针 → 用**新轨迹**当下一闭环的威胁场 → N+1。
因为到第 N 闭环为止的路线前缀**正是那条轨迹记录的**，威胁场在该处**有效** ✓。

| 迭代 | 组合闭环数 | 引擎 hits | ticks | bossLife | damage |
|---|---|---|---|---|---|
| 1 | 1 | 7 | 4462 | 4909 | 73091 |
| 2 | 2 | **13** | 4341 | 8267 | 69733 |
| 3 | 3 | 9 | 3684 | 12882 | 65118 |

**① 管道确认** ✓：迭代 1 与 C55 的单独 loop-0 探针**逐字段一致**
（`7/4462/4909/73091`）⇒ 修复后文件正确、探针确实在跑这条路线。

**② 但迭代不收敛** ✗：每多组合一个闭环，受击**增加**（7 → 13 → 9），
而**脚本单独跑是 hits=4**。⇒ 组合产出的逐闭环路线**比脚本自己的更差**。

### ⇒ 关键认识（下一轮的方向）

组合在闭环内**丢弃了轨迹自己的路线**，换成枚举出来的路线。
可是**轨迹自己的路线在干净闭环里本来就是引擎无伤的**（C56：33/40 ✓）。

⇒ **组合应当把"观测路线"当作一个候选并优先保留**，
只在**观测路线受击**的闭环里才去枚举替代 ✓。

**判据**：整场引擎 hits 从当前 8 降到 **4 以下**（脚本基线），最终 0；
以及路线哈希在"该保留的闭环"上**不再改变**。

## C57. **更精确的诊断：坏闭环上枚举是"空的"，不是"错的"**

### 实验一：自洽迭代（用候选自己的轨迹重新切分）——无改善

C53 用 `jf000` 重新组合出 15 个闭环（`routeTicks=1394`），本轮**首次探针验证**：

```
0 / 8，hits=7..8，全部 deaths=1
```

⇒ **一次自洽迭代没有降低受击**。原因见实验二。

### 实验二：把受击闭环也纳入链（原本被排除）

原代码只让**干净闭环**当锚点（受击闭环被 `else chain.Add` 跳过）。
但目标是 `SurviveToEnd`（**只判 tick、不判位置**），
"受击后被击退到干净路线到不了的位置"这条理由**不适用** ⇒ 改为**全部纳入**。

**报告确实变了**：

| | chain | SEGMENTS loops | crossed |
|---|---|---|---|
| 改动前 | `loops=51 clean=40` | 40 | 51 |
| **改动后** | `loops=51 clean=51` | **51** | 51 |

**但引擎结果与路线都没变**：

```
引擎：hits=7 / 7 / 8（与改动前逐字段相同）
路线：MD5 394C1300E50ECF89EBEAAF14E1BCFD45（与改动前记录值完全相同）
```

⇒ 按铁律判定：**该改动未反映到路线**。

### ⇒ 真正的原因（本轮的核心结论）

受击闭环内，搜索**找不到干净目标**（`report.Found == false`）⇒
该闭环贡献 **0 个动作** ⇒ 路线**逐字节不变**。

**所以那 11 个受击闭环上，枚举的结果是"空集"，不是"错的路线"。**
之前所有"模型说干净、引擎说死"的叙述要再修正一次：
坏闭环处**根本没有候选**，玩家在那里跑的是**脚本控制**（或上一动作的延续）⇒ 受击。

`loop-segments.csv` 里 11 个受击闭环（`observedHits>0`）：
`1(34), 2(7), 4(25), 5(16), 23(40), 25(4), 37(4), 38(19), 39(22), 46(4), 48(4)`

### 下一轮（目标明确）

**给这 11 个闭环一个有效的威胁场，让枚举在那里有输出**：

1. 对每个受击闭环，取其**所在探针轨迹的该 tick 窗口**作为威胁场
   （窗口只有约 60 tick ⇒ 偏离小 ⇒ 威胁场比整场有效得多）；
2. 在该窗口内枚举"无伤走到窗口末尾"的路线；
3. 用 `verify-route.ps1` 逐闭环验证（判据：该窗口内引擎 hits = 0）；
4. 把有输出的闭环动作**替换**进路线，再跑整场。

**判据**：路线哈希是否改变（改了就证明有输出），
以及整场引擎 hits 是否下降（当前 8，脚本基线 4，目标 0）。

## C56. **重大进展：组合路线在 40 个闭环中 33 个引擎实测 0 受击；只剩 7 个闭环要修**

### 新增：逐闭环清单（`loop-segments.csv`）

`ComposeLoops` 现在导出每个闭环的边界与动作区间：

```
index,startTick,endTick,ticks,actionStart,actionEnd,observedHits
```

（`actionStart/End = tick − firstTick`，与 C44 验证过的对齐一致）
输出行：`LOOP COMPOSE SEGMENTS <path> loops=40 observedHits=0`

### 测量：用**已有的**引擎轨迹按闭环窗口统计引擎 hits

不必再跑探针——`jf000` 就是组合路线的引擎运行，按清单的 tick 窗口统计生命下降帧：

```
loops with engine hits > 0 : 7 / 40
total engine hits in windows: 7
engine result: hits=8 deaths=1 ticks=1634
```

| 闭环 | 2 | 7 | 8 | 9 | 10 | 13 | 14 | **其余 33 个** |
|---|---|---|---|---|---|---|---|---|
| 模型判定 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **引擎实测** | 1 | 1 | 1 | 1 | 1 | 1 | 1 | **0** |

### ⇒ 三个结论

**① 架构验证通过（本轮最重要的结果）**

组合路线**在 40 个闭环中有 33 个引擎实测 0 受击**。
即"逐闭环接管 + 模型枚举"产出的路线**大部分是真的干净的**——
之前"96 条候选全死"是因为**7 个坏闭环**拖垮了整场，而不是路线整体无效。

**② 问题被缩到 7 个闭环**

从"整场 4393 tick 无从下手"缩到 **7 个各约 60 tick 的闭环**，
每个贡献恰好 1 次受击。这是可直接枚举的规模 ✓。

**③ 解释了 C55 的反常**

C55 里"只接管 loop 0"得到 hits=7，而这里 loop 0 的引擎 hits=**0**。
原因：单独接管 loop 0 后**脚本接管**，而玩家状态已不是脚本预期的那个
⇒ 脚本从错位状态继续 ⇒ 受击。**loop 0 本身是干净的** ✓。

### 下一轮

只针对**这 7 个闭环**做枚举修复：

1. 以**引擎的真实轨迹**（该闭环所在的探针轨迹）为威胁场，对这 7 个闭环重新枚举；
2. 每个闭环单独用探针验证（判据：该闭环窗口内引擎 hits = 0）；
3. 拼回整场，判据是**整场引擎 hits**（当前 8，脚本基线 4，目标 0）。

规模：7 个闭环 × 每闭环数次候选 ≈ 十几到几十次探针，可承受。

## C55. **逐闭环引擎反馈的管道验证通过；但组合出的 loop 0 比脚本更差**

### 实验：只接管第一个闭环，之后交回脚本

按 C44 验证过的对齐（`action index = engineTick − 240`）截出只含 loop 0 的路线
（loop 0 结束于 tick 345 ⇒ 动作 0..105，107 行），探针跑一场。

| 运行 | ticks | hits | deaths | bossLife | damage |
|---|---|---|---|---|---|
| 源脚本 fwd4（**无接管**） | 4393 | **4** | 0 | 0 | 78000（**win**） |
| 组合 48 条候选（全接管） | 1634 | 8 | 1 | — | 22922 |
| **只接管 loop 0** | **4462** | **7** | 1 | **4909** | **73091** |

### 两个结论

**① 管道可用** ✓

只接管 loop 0、之后路线耗尽即交回脚本：运行跑满 **4462 tick**、
BOSS 只剩 **4909 HP**（`shots=30`）。
⇒ "**逐闭环接管 + 脚本兜底**"的架构**可运行**，
路线文件短于战斗时 `TryRead` 失败、plan 保留脚本控制，这条路径**实测有效**。

**② 但组合出的 loop 0 路线比脚本自己更差**（hits 4 → 7）

模型说它干净，引擎给 7 次受击。这与 C54 完全一致：
组合路线**从 tick 240 就偏离轨迹** ⇒ 威胁场失效 ⇒ 模型的"干净"没有意义。

**③ 顺带确立基线**：**脚本单独跑是最好的已知结果（hits=4 且获胜）**，
优于至今**任何**枚举候选（最好 hits=7）。这给验收立了一个必须超过的标尺。

### ⇒ 架构结论

逐闭环引擎反馈是**正确架构**，但**选择判据必须是引擎的逐闭环 hits，不是模型的**。
`verify-route.ps1` 已经在整场层面做这件事（引擎当判据）；
扩展方向是**逐闭环验证**，只保留引擎判定为 0 受击的闭环。

### 下一轮

1. 给组合路线**标注逐闭环边界**（tick 区间 → 动作区间），导出为可分段验证的清单；
2. 用探针对**每个闭环单独**取引擎 `hits`（只接管该闭环、其余交回脚本）；
3. 只保留引擎 0 受击的闭环，并把它们的引擎轨迹作为**后续闭环的真实威胁场**。

判据：**逐闭环引擎 hits 是否保持 0**；标尺是脚本基线的 **hits=4**。

## C54. **完备性边界测出来了：可安全组合的闭环数 = 0（第一 tick 就偏离）**

### 测量（候选 jf000 的引擎轨迹 vs 源轨迹 fwd4，逐 tick 比玩家位置）

| 阈值 | 首次超过的 tick |
|---|---|
| \|Δ\| > 4 px | **240**（接管的第一 tick） |
| \|Δ\| > 32 px | **242** |
| \|Δ\| > 128 px | **249** |

| tick | 280 | 320 | 480 | 560 | 640 | 1062 |
|---|---|---|---|---|---|---|
| \|Δ\| | 376.8 | 703.2 | 780.2 | 1409.8 | 2126.5 | **5647.4**（最大） |

### ⇒ 结论

**组合路线从接管第一 tick 就偏离源轨迹**，40 tick 内偏离数百像素。

结合 C53（模型**沿轨迹走时 `maxError=0`**、偏离后失准）：
⇒ **轨迹威胁场的有效期 = 0 个闭环**。组合出来的每一条"干净"判决，
都是在**一个从 tick 240 起就已失效的威胁场**里做出的 ⇒ 引擎必然杀掉。

**这不是调参能修的**：`crossed=51 complete=True composedHits=0` 这套报告
在威胁场失效的前提下**没有意义**。

### ⇒ 三条路的代价（据此选下一步）

| 路线 | 代价 | 说明 |
|---|---|---|
| 生成式威胁场 | **项目级** | 需 BOSS 自己的状态机（AI_069）；两个运动学近似已证伪（C51/C52） |
| **逐闭环引擎反馈** | 中 | 每闭环跑一次探针取**真实**威胁场；1 闭环 ≈ 35 s，51 闭环 ≈ 30 min |
| 限制搜索贴轨迹 | 小但**几乎无用** | 自由度被压到近似"复刻脚本"，枚举失去意义 |

### 下一轮

选**逐闭环引擎反馈**：把"组合"改成**逐闭环推进 + 引擎确认**——

1. 从**当前真实状态**（引擎给的）出发，在**该闭环**内枚举；
2. 用探针跑该闭环，取**引擎的真实轨迹**作为**下一闭环**的威胁场；
3. 重复。

这样威胁场**每一步都是真的**，代价是每闭环一次探针。
判据：**逐闭环的引擎 `hits` 是否保持 0**（而不是组合报告的 `composedHits`）。

## C53. **重大更正：前向模型是精确的（干净闭环 maxError=0），且受击判定偏保守**

### 测量：在候选**自己的**轨迹上重新切分（自洽迭代的第一步）

把 `jf000`（候选 000 的引擎运行，实际 `loss hits=8 deaths=1 ticks=1634`）
当作威胁场来源重新切分，读出 `LOOP COMPOSE OBSERVED`（模型 vs 引擎，逐闭环）：

| loop | ticks | worldHits | engineHits | reachedGoal | **maxError** | hitAt |
|---|---|---|---|---|---|---|
| 0 | 104 | **0** | **1** | False | **187.39** | 311 |
| 1 | 58 | 0 | 0 | **True** | **0** | 345 |
| 2 | 58 | **7** | **1** | False | **0** | 407 |
| 3 | 58 | 0 | 0 | False | 5.10 | 0 |
| 5 | 68 | 0 | 0 | **True** | **0** | 0 |
| 6 | 110 | 0 | 0 | **True** | 1.20 | 0 |
| 7 | 58 | **5** | **1** | False | **0** | 776 |
| 8 | 58 | **7** | **1** | False | **0** | 813 |

### 三个发现（推翻 C50 的部分叙述）

1. **干净闭环上 `maxError = 0`** ⇒ 前向模型在该闭环内**逐像素精确** ✓✓
   —— 模型**不是**"不准"，之前"模型说干净、引擎说死"的锅不能全甩给它；
2. **受击闭环上 `worldHits` 远高于 `engineHits`**（7 vs 1、5 vs 1）⇒
   模型**过度预测受击**，即**偏保守**，不是"谎报干净" ✓；
3. **只有 loop 0 漏报**（`worldHits=0` 但 `engineHits=1`），
   而那里 `maxError=187.39`（tick 310）、受击在 311
   ⇒ **漏报只发生在模型自身位置已偏 187 px 时**。

### ⇒ 修正后的诊断

模型在**沿轨迹走**时精确；一旦**偏离轨迹**就失准（漏报）。
而组合做的事正是"把 47 个干净闭环拼接起来" ⇒ **拼接点必然偏离轨迹**
⇒ 从第一个偏离点起威胁场失效 ⇒ 引擎里死掉。

**所以瓶颈不是模型精度，而是"组合后威胁场不再对应当前状态"**（与 C50 同源，
但 C50 把它说成"模型谎报"，此处更正为"模型只在偏离后失准"）。

### 下一轮

**量化偏离**：在组合出的路线上，找出**第一个偏离轨迹超过阈值**的闭环，
并测该点之后模型与引擎的受击差。
这个"可安全组合的闭环数"就是当前方法的**完备性边界**，
也直接决定要不要走"逐闭环引擎反馈"的路线。

## C52. **第二个生成式 BOSS 模型也被证伪 ⇒ 运动学近似不够，需要 BOSS 自己的状态机**

### 实现（把 BOSS 状态纳入搜索状态，C51 的下一轮）

- `PlayerMotionFrame.Boss`（`BossChaseState`：`Active/Position/Velocity`）
  —— **随分支走**，不放在共享 world 里；
- `ThreatFor(index, player, out boss)`：BOSS **从自己的位置出发**，
  以轨迹里该 tick 的**位移大小**为速度、方向朝玩家推进；
- `TryStep` 把新 BOSS 状态写回 `next.Boss`；
- **bucket 也带上 BOSS 位置**（跟随模式用独立字符串键字典，默认路径零开销）
  —— 否则重犯 §D 的塌维度错误。

### 结果：改动**确实执行**，但仍卡在 loop 1

| 模型 | 扩展数 | 结果 |
|---|---|---|
| 固定相对偏移（C51） | 3 538 824 | `crossed=1 failedAt=1` |
| **自状态推进（本轮）** | **1 827 813** | `crossed=1 failedAt=1` |

扩展数变了 ⇒ 模型确实换了、分支确实执行；但**两种模型下 loop 1 内都不存在干净路线**。

### ⇒ 结论：运动学近似不够

轨迹里的 BOSS **速度**是"当时那个几何"的结果。几何一变，
照搬速度就会**过度驱动** BOSS——真实猪鲨有**攻击/悬停相位**，
不是每 tick 都朝玩家全速冲。

⇒ **忠实的生成式威胁场需要 BOSS 自己的状态机（AI_069）**，那是**项目级**工作量，不是补丁。
选项已默认关闭，两个失败模型与原因都写在注释里；管线恢复（`crossed=51 complete=True`）。

### 替代路线（下一步，代价小得多）

既然生成式威胁场是项目级工作，就**让轨迹威胁场自我一致**：

1. 跑一条候选 → 拿到**它自己的引擎轨迹**；
2. 用**那条轨迹**当威胁场重新切分/组合；
3. 重复，直到 `composedHits` 与引擎 `hits` 一致（不动点）。

这样威胁场**收敛到候选真实所处的战斗**，而**不需要重写 AI** ✓。
判据：`composedHits` 与引擎 `hits` 的**差**是否随迭代下降。

## C51. **生成式威胁场第一版：改动确实执行，但"固定相对偏移"模型是错的（loop 1 就卡死）**

### 实现（`TraceLoopWorldOptions.BossFollowsPlayer`）

- `TraceThreat.PlayerPosition`：轨迹里该 tick 的玩家位置；
- `ThreatFor(index, player)`：BOSS 位置 = 玩家当前位置 + **轨迹里的相对偏移**
  `(traceBoss − tracePlayer)`；
- 由 `TryStep` 在命中判定前套用。

**设计理由（当时）**：BOSS 追玩家 ⇒ 相对几何是可迁移的部分，且**无需拟合参数、无状态**，
可与搜索树共享的世界共存。

### 结果：改动**确实执行**，但组合在 loop 1 就失败

```
LOOP COMPOSE FAIL index=1 ticks=58 branches=48 expanded=4194149
LOOP COMPOSE FAIL REFUSAL PastLoopEnd=3538824
LOOP COMPOSE segments=51 crossed=1 failedAt=1 complete=False
```

⇒ **loop 1 内不存在任何"无伤走到循环末尾"的路线**。

### ⇒ 模型错在哪（重要）

**相对偏移不是不变量，而是追逐动力学的结果。**
轨迹里 BOSS 追玩家时偏移常常很小 ⇒ 把偏移钉住等于**把 BOSS 粘在玩家身上**
⇒ 每 tick 都在 BOSS 体内 ⇒ 全部路线受击 ⇒ 3 538 824 次拒绝。

**正确模型**必须用 BOSS **自己的位置与速度**推进，
而那份状态**必须随搜索分支走**——不能放在共享的 world 里（搜索树会串味）。

⇒ 这是**设计改动**（把 BOSS 状态纳入搜索状态），不是一行能补的。已把选项默认关闭、
代码与结论保留在注释里；管线恢复（`crossed=51 complete=True`）。

### 副产品：独立确认了 C48

`IsGoal` 在 `SurviveToEnd = true` 时（组合正是这么设的）**只判 tick、不判位置**：

```csharp
if (state.Tick < _goalTick) return false;
if (_options.SurviveToEnd) return true;      // ← 组合走这条
```

⇒ **目标根本不是位置**，所以 `GoalTolerance` 4→32 必然逐字节相同 ✓
——C48 的 A/B 结果由此得到**机制上的解释**（不再只是"测出来一样"）。

### 下一轮

把 BOSS 状态纳入搜索状态。可选实现（按代价排序）：

1. 在 `PlayerMotionFrame` 上挂一个"世界附加状态"（BOSS 位置/速度），
   `TryStep` 读写它，bucket 也必须带上它（否则重犯 §D 的塌维度错误）；
2. 或让 `TraceLoopWorld` 变成**每分支一个实例**（内存与改动都更大）。

**判据不变**：组合能否重新 `crossed=51`，且 `composedHits` 与引擎 `hits` 是否开始一致。

## C50. **"模型说干净、引擎说死"的根因测到了：威胁场取自源轨迹，而猪鲨追玩家**

### 测量（源轨迹 fwd4 vs 候选 jf000，逐 tick 比 BOSS 位置）

| tick | 源轨迹 BOSS | 候选 BOSS | 偏差 | 源玩家 X | 候选玩家 X |
|---|---|---|---|---|---|
| 240 | (1215,7619) | (1215,7619) | (0,0) | 640 | 655 |
| 400 | (542,7719) | (906,7778) | (364,58) | 640 | 1163 |
| 560 | (1909,6779) | (320,6935) | **(−1589,157)** | 2050 | **640** |
| 800 | (3426,6976) | (309,7158) | **(−3117,182)** | 4026 | **640** |
| 1200 | (6051,7758) | (577,7031) | **(−5474,−727)** | 5943 | **640** |

### ⇒ 两个结构性缺陷

**① 威胁场无效（根因）**

`TraceLoopWorld` 用的是**源轨迹**在该 tick 的 BOSS 位置与射弹。
但猪鲨**追玩家**：候选路线把玩家带到别处 ⇒ BOSS 位置偏差最大 **5668 px**
⇒ 源轨迹的威胁场对候选路线**完全不适用**。

**模型据此判"0 受击"没有意义**——它检查的是"在**别人的**战斗里会不会被打到"。
这解释了 C46–C49 的全部现象：**96 条候选全死，而模型说全干净**。

**② 枚举出的路线把玩家停在墙角**

候选玩家 X 从 tick 560 起恒为 **640** = 场地左边界（`ArenaLeft`）。
模型与引擎在此**一致**（都 640），所以不是模型误差；
而是**搜索偏好贴墙**——因为（无效的）威胁场告诉它墙边安全 ⇒ 被 BOSS 逼到角落 ⇒ 死。

### 这与使用者第 C 条的关系

第 C 条点名的"评估器缺口"是**覆盖率**（翅膀/冲刺/跳跃/火箭靴未覆盖）。
本测量给出的是**更上游**的缺口：**威胁场不是生成式的，而是从一条轨迹上抄的**。
即使把物理覆盖面补到 100%，只要威胁场仍是抄来的，判据依然是错的。

### 下一轮（生成式威胁模型——使用者已指明方向）

把威胁场从"抄源轨迹"改为"**按当前玩家位置推进 BOSS 脚本**"：

1. 从轨迹里取 BOSS 的**脚本状态**（`ai[]`、`localAI[]`、阶段、`targetPlayer`）；
2. 用玩家**当前**位置驱动 BOSS 的移动/瞄准，生成射弹；
3. 射弹用已有的 `HostileProjectileMotion` 推进；
4. 判据不变：**96 条候选的死亡是否减少 / 是否出现 `hits` 下降**。

**注意**：这仍是"枚举逻辑/模型定义"的修正，不是手写策略 ✓。

## C49. **切分变化确认拓宽空间（2→4 类）；但全部候选都死 ⇒ 约束在模型而非枚举空间**

### 实验：换一条轨迹当切分来源

用当前构建**新生成**一条密集轨迹（不同种子 ⇒ 不同 RNG ⇒ 不同威胁时序 ⇒ 不同闭环边界）：

```
game-probe-rt-fwd5-0918-232018   seed=5 win=True ticks=4532 hits=6 deaths=0
```

在它上面跑同一套组合（51 个闭环、`widest=48`、`complete=True`），再逐条引擎判定。

### 判据结果：**结局类别数 2 → 4**（肯定）

| 切分来源 | 结局类别 | 类别数 |
|---|---|---|
| fwd4（seed 2） | `7/1319/17307`、`8/1364/18031` | **2** |
| **fwd5（seed 5）** | `7/1320/15200`、`8/1628/24774`、`7/1274/14041`、`8/1643/22222` | **4** |

⇒ **C47 的假设得到确认**：有效多样性由**切分来源**决定，不由目标收集/容差决定。
⇒ 想继续拓宽，就**组合多条轨迹的切分**（多种子各自切、候选合并）。

### 但暴露更硬的约束

**96 次引擎判定（fwd4 48 条 + fwd5 48 条）全部 `deaths=1`**，
而**两条源轨迹本身都是 `win=True`**（fwd4 `hits=4`、fwd5 `hits=6`）。

⇒ **枚举出的路线比脚本基线更差**：模型判 `composedHits=0`（干净），
引擎却每条都死。**模型的受击预测是错的** ⇒ 这就是使用者第 C 条点名的**评估器缺口**。

**枚举空间已经不是瓶颈**（拓宽有效），**瓶颈是"模型说干净、引擎说死"**。

### 下一轮（按使用者第 C 条：补覆盖面优先于调参）

不再是枚举空间的事，而是**让模型别再谎报干净**：

1. 逐条候选比对**模型预测的受击 tick** 与**引擎实际的受击 tick**，
   找出模型漏报的第一帧（这是"评估器缺口"的可测量形式）；
2. 缺口优先补在**死亡前的那个闭环**——96 条候选都死，死亡点集中，
   比 §C 的"全轨迹 0.5% 覆盖"更好定位；
3. 补完再跑同一判据（候选是否开始出现 `hits` 下降 / 不再全死）。

## C48. **容差假设被证伪（A/B 逐字节相同）⇒ "48 条→2 类"的成因不在目标宽度**

### 假设

目标是"观测路线在下一闭环起点的位置"（单点，`GoalTolerance = 4f`）
⇒ 猜测：4 px 半径把每个分支都钉在"复现该轨迹"上。

### 实验（A/B，铁律要求）

把 `GoalTolerance` 提为命名常量 `BoundaryTolerance`，从 **4f → 32f**，重新组合并导出：

| | 结果 |
|---|---|
| 组合汇总 | `segments=51 crossed=51 complete=True widest=48 routeTicks=4153`（与 4f **完全相同**） |
| 48 条候选引擎判定 | 仍 **2 类**：`7/1319/17307`、`8/1364/18031`（与分散实验**逐字段相同**） |
| **A/B 路线文件比对** | **`identical=48 different=0`**（MD5 全同） |

### ⇒ 结论

**改动未被"执行"**（铁律判定的"未执行"）：容差 4 与 32 产生**逐字节相同**的路线。
即 **`GoalTolerance` 不是紧约束**——搜索本来返回的状态就已满足更紧的 4 px。

**假设被证伪**，这是本轮真正的产出：让 48 条互异路线塌成 2 类引擎结局的**不是目标的宽度**。

### 已排除的候选成因

| 候选成因 | 状态 |
|---|---|
| 前沿只由 branch 0 生成 | **已修**（真 bug，C43） |
| 回放跳跃通道失效 | **已修**（真 bug，C45） |
| 目标收集按启发式聚簇 | **已排除**（C47，分散后类别 3→2） |
| 目标容差太窄 | **已排除**（C48，A/B 逐字节相同） |

### 下一轮（枚举逻辑）

剩下的结构性约束：**所有候选都按同一顺序走同样的 51 个闭环**，
且每个闭环的**威胁场来自同一条 fwd4 轨迹** ⇒ 结局由该轨迹的威胁时序主导。

**下一实验：变化切分本身**——用另一条轨迹（不同种子/左起右起）的闭环边界，
或对同一轨迹平移相位切分，再跑同一判据（结局类别数）。

## C47. **判据给出否定结果：目标分散没有拓宽空间 ⇒ 瓶颈在闭环切分**

### 改动（纯枚举逻辑）

`RouteSearchRequest` 新增：

- `GoalCellSize`（0 = 旧行为）：目标状态按格子去重，**每格只收一个**；
  速度格取位置格的 1/4（8 / 2 px），保证**不比组合端去重更粗**，收集到的不被下游丢掉；
- `GoalSpreadSkips`：同一格连续跳过太多就停，避免"所有目标同格"时白烧预算。

组合端设 `GoalCellSize = 8f`。

**改动确实被执行**（首个差异行变了）：

| | 000 vs 001..016 的首个差异行 |
|---|---|
| 分散前 | 98, 102, 1575, … |
| **分散后** | **96, 806, 2415, 3378, 3492** |

### 判据结果：**结局类别数 3 → 2**（否定）

| | 结局类别 | 类别数 |
|---|---|---|
| 分散前（C46） | `8/1634/22922`、`7/1360/15627`、`9/1654/25890` | **3** |
| **分散后** | `7/1319/17307`、`8/1364/18031` | **2** |

两者均 **0/48 无伤**，全部 `deaths=1`。报告 `artifacts/route-verify-spread48.txt`。

### ⇒ 结论（这是本轮真正的产出）

**目标收集不是瓶颈。** 按格子分散后有效多样性**反而下降**，
说明 48 个分支的结局由**比"到达哪个格子"更上游的东西**决定。

⇒ **瓶颈在闭环切分本身**：51 个闭环是从**一条 fwd4 轨迹**上切出来的，
每个闭环的边界、起点、威胁相位都被这条轨迹钉死；
在固定边界内怎么换路线，结局都落回同 1–2 类。

### 下一轮（仍是枚举逻辑）

**让切分本身变化**，而不是在固定切分里换路线：

1. 用**多条不同来源的轨迹**（不同种子 / 左起右起）各自切闭环，混合成组合图；
2. 或对同一轨迹用**不同相位偏移**切（闭环起点平移 k tick），得到多个切分方案；
3. 判据不变：结局类别数是否上升。

## C46. **第一次真正的候选验证：48 条互异路线 → 只有 3 种引擎结局**

跳跃通道修好后（C45），48 条候选**第一次**被真正逐条验证
（`verify-route.ps1`，报告 `artifacts/route-verify-all48.txt`）。

### 结果

| 类别 | hits | ticks | bossDamage | 条数 |
|---|---|---|---|---|
| A | 8 | 1634 | 22922 | **29** |
| B | **7** | 1360 | 15627 | **12** |
| C | 9 | 1654 | 25890 | **7** |

**达成战斗级无伤：0 / 48**；**全部 `deaths=1`**（每条都死一次）。

### 这个测量的意义（枚举空间问题，正是我的职责）

- 48 条路线**互异**（C45 已证明控制序列差 566 帧）**却只产生 3 种引擎运行**
  ⇒ 前沿的 48 个分支**在结局上等价**，有效多样性 ≈ 3；
- 3 类结果分别对应 3 个**早期决策**，其余差异对结局无影响；
- 最好的一类是 **hits=7**（优于修复前的 8），但仍未接近 0。

### 下一轮（纯枚举逻辑）

**让收集到的目标在"结局"维度上分散**，而不是取 beam 里最先出队的 48 个
（它们彼此几乎相同）。做法：

1. 目标收集改为**按状态粗桶各取一个**（`CollectGoals` 当前是"前 N 个"，
   等于按启发式排序取，必然聚簇）；
2. 桶必须保留与目标有关的维度（§D 的教训）；
3. 重新导出候选并用 `verify-route.ps1` 判定，看结局类别数是否上升。

**判据**：结局类别数（现在是 3）上升 = 枚举空间真的变宽；
若仍为 3，说明多样性瓶颈在**闭环切分**而不在目标收集。

## C45. **真因修复：`JumpAction.Hold` 会强制覆盖 `plan.Jump`——枚举候选第一次真的互不相同**

### 真因（在 `JumpMotion.ResolveControl` 里）

```csharp
if (action == JumpAction.Release) return false;
if (action == JumpAction.Hold) requested = true;   // ← 强制覆盖
```

而回放块写的是 `plan.JumpAction = JumpAction.Hold;`（常量）
⇒ **路线里 `jump=0` 的项仍被解析成"按住"** ⇒ 跳跃通道恒为 True
⇒ 所有在跳跃上有差异的候选跑出**同一次运行**。

### 修法

```csharp
plan.JumpAction = replayJump ? JumpAction.Hold : JumpAction.Release;
```

`Release` 是唯一能真正产生 `false` 的动作。

### 验证（两条候选：`composed-route-000` / `-002`）

| | ticks | hits | deaths | bossLife | damage |
|---|---|---|---|---|---|
| 修复前 | 1655 | 8 | 1 | 52290 | 25710 |
| **jf000** | **1634** | 8 | 1 | **55078** | **22922** |
| **jf002** | **1360** | **7** | 1 | **62373** | **15627** |

**逐位核对（铁律）**：

| 检查 | 修复前 | 修复后 |
|---|---|---|
| 两候选控制序列差异数 | **0** | **566**（首个在 tick 341） |
| `plan.jump` vs `controlJump` | 恒 `True` ✗ | **逐一吻合** ✓（340-343 `False/False`，344 `True/True`） |

⇒ **枚举候选第一次在引擎里真的互不相同**，回放→引擎的闭环**真正闭合**。

### 意义

C42/C43/C44 追的"48 条候选同一次运行"到此结束：
不是模型不准，也不是前沿退化（那个也是真 bug，已修），
而是**回放的跳跃通道从未生效**。在这之前所有"候选验证"都是同一个运行的重复。

## C44. **回放自证上线——立刻定位真因：`plan.jump` 到了，`controlJump` 没到**

### 改动：把回放帧号写进观测（对齐从"推断"变"测量"）

- `ControlPlan.ReplayFrame`（`Models.cs`）：本次 plan 取自路线的第几帧，无回放时为 `-1`；
- `Runtime` 在 `ApplyPlan` 前**每帧**赋值 `plan.ReplayFrame = _replay != null ? _replayFrames : -1;`
  （每帧都写，才能把"没有回放"与"回放第 0 帧"区分开）；
- `GameProbe.cs` 观测里加 `{"replayFrame", observedPlan.ReplayFrame}`。

### 自证读出的三件事

```
tick=240 replayFrame=0   tick=241 replayFrame=1   tick=242 replayFrame=2  …
count=1417   min=0   max=1003
索引 1004 起 prev=1003 now=1003 …（连续 413 行停住）
```

1. **对齐是对的**：`action index = engineTick − 240` ✓（tick 240→0，241→1，…）
   —— C43 里"tick 256 脱钩"的推断**被推翻**，脱钩在更晚处；
2. **1656 tick 里只有 1417 有 plan**；
3. **回放走到第 1003 帧就停住**，之后 413 行一直停在 1003。

### ⇒ 真因（决定性）

查 tick 340 处（`replayFrame=100`）：

| tick | replayFrame | `plan.jump` | 引擎 `controlJump` | route002 该项 |
|---|---|---|---|---|
| 340 | 100 | **False** | **True** | `-1,0,0` |
| 341 | 101 | **False** | **True** | `-1,0,0` |
| 344 | 104 | **True** | True | `-1,1,0` |

⇒ **`plan.jump` 确实跟随路线**（回放已写进 plan），
**但引擎的 `controlJump` 恒为 True** ⇒ **plan 的跳跃通道没有到达原生控制**。

**这就是 48 条候选产生同一次运行的真因**：候选之间的差异主要落在**跳跃通道**上，
而该通道在引擎侧被覆盖/忽略 ⇒ 差异等于没施加（铁律判定的"未执行"）。

### 下一轮（纯回放/接线，不碰物理）

1. 查 `ApplyPlan` 里 `plan.Jump` → 原生 `controlJump` 的映射，
   以及 `JumpAction.Hold` 的语义（回放强制 `JumpAction.Hold`，可能压过 `Jump=False`）；
2. 查是否有 `autoJump` / 翅膀飞行逻辑在 `ApplyPlan` 之后把 `controlJump` 拉回 True；
3. 修好后重跑候选验证，看 48 条候选是否终于给出**不同**判决。

## C43. **修好前沿退化（真 bug）；但暴露回放在 tick 256 就与路线脱钩**（**"tick 256"这一推断已被 C44 推翻**）

### ① 修复：前沿只由第一条分支生成

```csharp
foreach (var branch in branches) {
    var report = Search(... CollectGoals = BoundaryWidth);
    for (goal...) { next.Add(...); if (next.Count >= BoundaryWidth) break; }
    if (next.Count >= BoundaryWidth) break;   // ← 元凶
}
```

**第一条分支自己就能产出 48 个目标**，所以外层 `branches` 循环永远停在 branch 0
⇒ 每个新前沿都只由一条分支生成 ⇒ **早期闭环没有任何多样性**。

**修法：轮转配额**。先把每条分支的目标各自收好（`perBranch`），
再按轮次逐条发放，保证**每条幸存分支都被代表之后**才给任何分支第二个状态。

### 验证：候选多样性提前（这是本次改动的直接证据）

| 对比 | 首个差异行 |
|---|---|
| 修复前（000 vs 001） | **4149**（最后一个闭环） |
| 修复后（000 vs 001） | **1575** |
| 修复后（000 vs 002） | **102** |
| 修复后（000 vs 003） | **98**（数据行 97，第一个闭环附近） |

`distinct files: 48`（48 条全部互异）✓

### ② 但引擎判定仍完全相同——且原因不是验证器

用 `verify-route.ps1` 跑 000/002/003：**三条 `loss hits=8 deaths=1 ticks=1655 bossDamage=25710`**。
手工直接跑 `runprobe.ps1` 两条（mr000 / mr002）结果同样完全相同。

**逐位核对**（铁律要求）：

| 检查 | 结果 |
|---|---|
| 两候选引擎轨迹的控制序列 | **differences=0**（逐行相同） |
| 两候选引擎轨迹的位置序列 | **differences=0** |
| 观测控制 vs 路线 000 | 1416 检查中 **954 不符**，首个不符在 **tick 256** |
| 观测控制 vs 路线 002 | 1416 检查中 **970 不符**，首个不符在 **tick 256** |
| 观测 `controlJump=False` 首次出现 | **tick 1244**（tick 340 处引擎仍是 `J=True`，与 000 一致、与 002 不符） |

⇒ **路线差异没有被施加**，而且回放**从 tick 256 起就与路线脱钩**（不只是最后）。
这解释了为什么 48 条候选得到同一个判决。

### 下一轮（仍是枚举/回放逻辑，不碰物理）

1. 定位 tick 256 处脱钩的原因：路线索引 `T−240` 的对齐假设可能不成立
   （`_replayFrames` 只在"插件正在操控"的 tick 前进，而中途可能中断/重启）；
2. 给回放加**自证**：插件把自己的 `_replayFrames` 写进观测，
   这样对齐不再靠推断——**这是让"差异是否被施加"可观测的最小改动**；
3. 对齐修好后重跑候选验证，看 48 条候选是否开始给出**不同**判决。

## C42. **枚举器改为导出整条前沿；引擎验证器打通——并暴露真正的瓶颈：前沿只在最后一个闭环分叉**

### 新增：枚举器导出 N 条候选，而不是 1 条

`ComposeLoops` 之前只写 `branches[0].Route`（**把整个前沿丢掉了**）。
现在导出**全部**前沿分支：

```
composed-route-000.csv … composed-route-047.csv     （48 条，MD5 全部不同 ✓）
candidates.csv                                      （可直接喂给验证器）
```

`candidates.csv` 的 scenario/seed/route 由轨迹旁的 `result.json` 自动读取
（`ReadTraceIdentity`），不需要人重打。

### 新增：`verify-route.ps1`（仓库外，`D:\personal tasks\_chaite-tools\`）

**引擎当判据**的路线批量验证器：每条候选跑一次全新隔离原版探针，
报告 `status/win/hits/deaths/bossDamage/ticks`；`hits=0 且 win` 即停。
责任划分写进脚本头：**枚举器决定"有哪些候选"，引擎决定"哪些候选是真的"**。

⚠️ **`.ps1` 含 CJK 必须带 UTF-8 BOM**（PowerShell 5.1 否则按 ANSI 读 ⇒ 乱码 ⇒ 解析失败）。
第一版忘了，已补 BOM。

### 实测：3 条候选引擎判定（各 ~35 s）

```
composed-000  status=loss win=False hits=8 deaths=1 bossDamage=25710 ticks=1655
composed-001  status=loss win=False hits=8 deaths=1 bossDamage=25710 ticks=1655
composed-002  status=loss win=False hits=8 deaths=1 bossDamage=25710 ticks=1655
达成战斗级无伤的候选: 0 / 3
```

### ⇒ 暴露的真正瓶颈（枚举空间问题，正是我的职责）

`composed-route-000` 与 `-001` **只在第 4149 行**（最后一个闭环）不同。
即 **48 条候选全部只差在最后一个闭环**，前面的闭环**收敛成同一条路径**
⇒ 三条候选引擎结果逐字段相同，因为引擎在 tick 1655 就死了，而分歧在 4148 之后。

**这是"搜索空间没有真正展开"**，与 §D 是同一类问题：
前沿虽然保住了 48 条，但**它们在早期闭环上没有多样性**，
所以枚举器实际上只探索了"一条主路径 + 末尾 48 种收尾"。

### 下一轮（纯枚举逻辑，不碰物理）

1. 查 `ComposeLoops` 里 `next` 的构造：前沿是否被**去重键**过早合并
   （`{x/8}|{y/8}|{vx/2}|{vy/2}` 会把不同历史折叠成一个 bucket）；
2. 若确认，改为**保留历史相关的维度**（§D 的第②条错误）；
3. 用 `verify-route.ps1` 批量判定，让引擎挑出真正无伤的那条。

## C41. **方法纠偏（使用者再次指示）：停止逐 tick 手推规则，回到枚举器**

### 使用者原话

> 我说过了，你不要对结果进行过于详细的分析后手写状态机，只要有合理正确的枚举后交给枚举就好了。

### 我的错误模式（记录以免重犯）

最近 8 轮我在做的是：**读密集轨迹 → 逐 tick 反解常数 → 手写分支规则**。
即使常数是实测的（符合"物理常量必须引擎内实测"），
**"逐 tick 分析 → 手推条件树"这个工作方式本身就是违规的**——
它是在**替枚举器想答案**，而不是**让枚举器有能力找到答案**。

### 职责边界（重申）

我负责：**枚举空间的定义、剪枝、排序、去重、完备性、边界报告**。
我不负责：手推物理分支、手挑常数、手写策略。

### 立即生效的三条

1. **不再**为单条轨迹的分歧做逐 tick 归因分析；
2. 物理常数只在**引擎内实测一次**并写入，之后**不再反复微调**；
3. 主攻方向改为**枚举器本身**：它能不能覆盖到解、边界有没有把解删掉。

### 当前枚举器的真实状态（必须诚实面对）

`ComposeLoops` 已经能跑出**模型内 0 受击**的 4153 tick 路线
（`composedHits=0 complete=True widest=48`），但**隔离探针给 8 次受击**
⇒ **枚举器给了答案，但它的评估器不可信**。

⇒ 关键问题不是"再修哪条物理分支"，而是
**枚举器凭什么判断一条候选无伤**——这个判据必须可靠。

### 下一轮

不再调物理常数。改为：
①把 `BoundaryWidth=48` 的**截断边界**变成显式报告
（每个闭环有多少干净目标被收集、是否被截断），确认解没有被剪掉；
②检查 `ComposeLoops` 的前沿是否真的在**跨闭环组合**，
而不是每条闭环只留一条路径。

## C40. **`0.4512` / `0.1` 被推导出来（不再是拟合）——但字段驱动接线会回归，暂留常数**

### 推导（观测里就有 `runAcceleration` / `runSlowdown`）

rp6 轨迹实测：**`runSlowdown = 0.2`**、**`runAcceleration = 0.2512`**（行 240 处 `runAcceleration=0.1256`，之后稳定 0.2512）

```
反向减速   = runSlowdown + runAcceleration = 0.2 + 0.2512 = 0.4512   ✓ 精确
中性减速   = runSlowdown × 0.5（空中）    = 0.2 × 0.5    = 0.1      ✓ 精确
```

⇒ **冲刺的 bleed 就是引擎的普通水平减速**，且**可由玩家字段推导**——这是比常数更本质的结论。

### 普通减速律的独立实测（fwd4 全轨迹，按输入统计 Δvx）

| 输入 | Δvx | 次数 |
|---|---|---|
| **OPP** | **0.4512** | **109**（另有 1 次在地面，同为 0.4512） |
| **NEUTRAL** | **0.1** | **104** |
| WITH（同向超速滑行） | 0.1 | 605 |
| WITH（加速中） | −0.1005 / −0.0502 | 750 / 149 |

⇒ **与是否触地无关**（地面样本同为 0.4512）。

### 但把它接到状态字段上会**回归**（已回退）

| 版本 | `firstDivergenceTick`（rp6 残差） |
|---|---|
| 常数版（0.1 / 0.4512） | **250** |
| 字段驱动版（`runSlowdown + runAcceleration`） | **245** ✗ |

推导是**精确的**，所以回归说明**前向模型没有把这些字段按引擎当时的取值喂进去**
（很可能是 `BuildFrame` 没从轨迹行读这两个字段，或 `Grounded` 判据不同）。

⇒ **已回退到常数版**，并把推导完整写进注释。
**回退一个正确的推导、因为它的输入是错的，是诚实的做法。**

### 下一轮

1. 查 `BuildFrame` 是否读取 `runAcceleration`/`runSlowdown`/`Grounded`；
2. 修好后重新接线，预期残差应越过 250；
3. 这也是**跨配装泛化**的前提——7 套配装的 `runSlowdown`/`runAcceleration` 不同，
   常数版只对测过的那一套成立。

## C39. **冲刺衰减的完整规则被解开**（统一式，精确到六位）；`0.4512` 就是引擎的普通反向减速

### 规则（一条式子，全部实测）

```
bleed  = 0.1    输入顺向/中性
         0.4512  输入与运动反向
speed  = |vx| − bleed          （下限 0）
decay  = 0.985  speed > 12
         0.94   否则
vx_next = sign × speed × decay
```

**关键点：制度判定用"减完 bleed 之后"的速度**，不是原始速度。

### `0.4512` 是钉出来的，不是拟合的

对同一条冲刺按制度分别反解 `(vx − c) × decay`：

| 制度 | 反解 `c` |
|---|---|
| HIGH 无反向 | **0.1** |
| HIGH 反向 | `0.1 + 0.3459312/0.985` = **0.451199** |
| LOW 反向 | `0.1 + 0.3301275/0.94` = **0.451199** |

**两种制度给出同一个 `c`，六位相同**，且在**第二条冲刺**（tick 2797）上复现。

### 逐位验证（两条冲刺，全部精确命中）

| 转变 | 输入 | 引擎 | 规则 |
|---|---|---|---|
| 2803→2804 | 无 | **12.385088** | `(12.6736937−0.1)×0.985` ✓ |
| 2804→2805 | **O** | **11.217855** | `(12.385088−0.4512)=11.933888 ≤12` ⇒ LOW ⇒ `×0.94` ✓ |
| 3296→3297 | **O** | **11.944389** | `(12.5774832−0.4512)=12.1262832 >12` ⇒ HIGH ⇒ `×0.985` ✓ |
| 3297→3298 | **O** | **10.803598** | `(11.9443893−0.4512)×0.94` ✓ |

### 这就解释了 C37 的"切换速度不一致"

原始切换速度：非反向 **11.82**；反向 **12.24 / 12.39 / 12.58**——**单一阈值不可能产生**。
减去 bleed 后：**11.72 / 11.93 / 12.00 / 12.13**——**全部落在 12 的同一侧** ✓✓

### 冲刺结束后引擎用的是同一对常数（rp5 tick 249–252）

```
249  vx=8.0000   dash 结束，被钳到 runSpeed=8
250  vx=7.5488   = 8 − 0.4512      （反向）
251  vx=7.4488   = 7.5488 − 0.1    （中性）
252  vx=6.9976   = 7.4488 − 0.4512 （反向）
```

⇒ **`0.4512` 不是冲刺专属常数，它就是引擎的普通"反向输入减速"**；
`0.1` 就是普通"无输入减速"。冲刺期只是**沿用同一对常数再乘一个衰减**。

### 残差

```
改前: firstDivergenceTick=246
改后: firstDivergenceTick=251  gap=2.15
```

**下一轮**：模型里"普通水平运动"路径也应使用同一对常数（0.1 / 0.4512），
当前模型在 tick 251 给 6.026 而引擎给 7.449，差别正在这里。

## C38. **两变量实验：反向项在同一条冲刺内被确认；LOW 段残差逐 tick 增大**

### 找到三条"输入中途换向"的冲刺（fwd4）

```
tick=2797  - - - - - - - O O O O O
tick=2864  - O O O O O O O O
tick=3292  - - O O O O O O O O
```

⇒ 可以在**同一条冲刺**里比较"有反向项"与"无反向项"，排除轨迹间混杂。

### tick 3292 逐位验证（引擎 vs 模型）

| 前值 → 引擎 | 输入 | 模型预测 | Δ |
|---|---|---|---|
| 14.5 → **14.18** | — | `(14.5−0.1)×0.985 = 14.184` | ✓ |
| 14.184 → **13.87** | — | `13.8727` | ✓ |
| 13.8727 → **13.22** | **O** | `13.5661 − 0.3465 = 13.2196` | **0.0004** |
| 13.2196 → **12.58** | **O** | `12.5762` | 0.0038 |
| 12.5762 → **11.94** | **O** | `11.9423`（**HIGH**） | 0.0023 |
| 11.9423 → **10.80** | **O** | `10.7855`（**LOW**） | **0.0145** |
| 10.7855 → **9.73** | **O** | `9.6978` | **0.0322** |
| 9.6978 → **8.72** | **O** | `8.6755` | **0.0445** |

**结论**：**反向项 `−0.3465` 在同一条冲刺内精确命中**（0.0004），
⇒ C34 的"反向输入"解释在**单条冲刺内**成立，不再是轨迹间相关。

### 但 LOW 段残差**逐 tick 增大**（0.0145 → 0.0322 → 0.0445）

⇒ **LOW 段的衰减式本身还有偏差**，不是常数噪声。
需要**用精确值**（不是两位小数）重新拟合 LOW 段：
`vx_next = (vx − b) × a − e`，解连续两对即可。

### 切换速度在**反向冲刺之间也不一致**

| 冲刺 | 切换到 LOW 时的速度 |
|---|---|
| 非反向（多数） | 11.8208 |
| tick 2864 | 12.2419 |
| tick 2797 | 12.3851 |
| tick 3292 | 12.5762 |

⇒ 阈值既不是固定值，也不只由"是否反向"决定。**下一轮**：
①用精确值重拟合 LOW 段（这是当前最大的确定性缺口）；
②把四次切换的**确切 tick 序号**与 `eocDash`/`dashTime` 对齐，找真正的判别量。

## C37. **切换速度不是固定值——它取决于输入方向**（C36 的区间结论需限定）

### 实测：fwd4 全部 35 次完整冲刺的 HIGH→LOW 切换点

| 冲刺类型 | 最后一次 HIGH | 首次 LOW | 次数 |
|---|---|---|---|
| **非反向**（顺向/中性） | **11.8208** | **11.0176** | 31 |
| **反向**（tick 2864 = rp5 那次） | **12.2419** | 11.0832 | 1 |

其余 3 次（2797 / 3158 / 3292）落在两者之间。

### 结论

**切换速度随输入方向变化**：反向冲刺在 **12.2419** 就切到 LOW，
而非反向冲刺到 **11.8208** 仍是 HIGH。
⇒ **不是"固定阈值"**。

### 对 C36 的限定

C36 说"引擎阈值落在 (12.2419, 12.8795)"——那是**只看了反向那一次**得出的，
**对非反向冲刺不成立**（非反向在 11.8208 还是 HIGH）。
正确表述：**阈值判定与反向项耦合**，两者必须在同一条规则里解释。

### 已排除的简单形式

- 固定阈值（12 或其他单一值）：**否**，见上表；
- `accRunSpeed + maxRunSpeed = 12.71`：**否**，非反向冲刺在 11.8208 仍 HIGH，
  12.71 会让它提前切换；
- 只按 `|vx|` 判定：**否**。

### 下一轮

需要在**同一条冲刺**里同时看到反向与非反向段（输入方向中途改变），
才能把"阈值"与"反向项"分离——那是一次干净的两变量实验。

## C36. **每 tick 幂等修复成功**：回放与模型逐位吻合；残差后移到 tick 246，瓶颈是**高低速阈值**

### 修法：计数从 `ApplyPlan` 移到 `Tick` 入口

```csharp
// Tick 入口（每游戏 tick 一次）
if (_replayTickRead) { _replayFrames++; _replayTickRead = false; }
// ApplyPlan：只读、只标记
if (_replay.TryRead(_replayFrames, ...)) { ...; _replayTickRead = true; }
```

⇒ 一个 tick 内读多次仍只消耗**一项**；不读则不消耗。

### 实测（rp5 的行控制 = **上一 tick** 的输入）

| 行 | 控制 | 对应路线项 |
|---|---|---|
| 240 | `L=F R=F J=T D=T` | route[0] `0,1,1` ✓ |
| 241 | `L=F R=F J=T D=F` | **route[1] `0,1,0` ✓（rp4 里被跳过，现已出现）** |
| 242 | `L=T R=F J=T D=F` | route[2] ✓ |

### 速度逐位吻合（冲刺衰减 + 反向项双双确认）

| 行 | 引擎 vx | 模型 | 差 | 制度 |
|---|---|---|---|---|
| 241 | **14.1840** | `(14.5−0.1)×0.985` | ✓ | BASE（route[1] 中性） |
| 242 | **13.5268** | `13.52624` | 0.0006 | HIGH + 反向 |
| 243 | **12.8795** | `12.87835` | 0.0006 | HIGH + 反向 |
| 244 | **12.2419** | `12.24128` | 0.0006 | HIGH + 反向 |
| 245 | **11.0832** | LOW 预测 `11.0668` | 0.0164 | **LOW + 反向** |
| 246 | **9.9941** | LOW 预测 `9.9777` | 0.0164 | LOW + 反向 |

⇒ **C32 的衰减形式、C34 的反向项，两者都在引擎内被逐位确认。**

### 残差

```
改前: firstDivergenceTick=243
改后: firstDivergenceTick=246  gap=1.01
```

### 剩下的唯一偏差：**高低速阈值的切换时机**

引擎在**输入速度 12.2419** 时已切到 LOW，而模型用 `speed > 12` 仍是 HIGH
⇒ **引擎阈值落在 (12.2419, 12.8795)**，记录的 `HighSpeedThreshold = 12f` **偏低**。

**候选**：`accRunSpeed + maxRunSpeed = 8 + 4.71 = 12.71`，落在界内。
**但仅凭一个界不能定值**——需要一条 `acc`/`max` 不同的轨迹来看切换点是否随之移动
（fwd4 tick 1175 的 `maxRunSpeed=2.205` 是候选样本）。

**注意**：在界内任取一个值（如 12.5）属于**拟合**，本轮不做。

## C35. **回放错位的新原因：引擎在同一 tick 内消耗了两个路线项**（常数 SKIP 无法对齐）

### 实测（rp4 密集轨迹的行控制 = **上一 tick** 的输入）

| 引擎行 | 控制 | 对应路线项 |
|---|---|---|
| 240 | `L=F R=F J=T D=T` | **route[0]** = `0,1,1` ✓ |
| 241 | `L=T R=F J=T D=F` | **route[2]** = `-1,1,0` |
| 242 | `L=T R=F J=T D=F` | route[3] |

**route[1] = `0,1,0`（中性）在引擎里从未出现** ⇒
引擎在**同一个游戏 tick 内消耗了两个路线项**（该帧 `ApplyPlan` 被调用两次，
很可能是交接帧的 handoff 路径与主路径各一次）。

### 后果

`CHAITE_ROUTE_SKIP` 是**常数偏移**，而这里丢的是**某一帧多消耗一项**，
所以**单靠 SKIP 无法对齐**。正确做法是让回放**对每个游戏 tick 幂等**
（同一 tick 内多次进入只消耗一项），而这需要插件有一个**每 tick 的标记**——
目前没有（观测里没有 tick 字段，`Runtime` 也只有"施加帧"计数）。

### 模型侧的一致证据

模型按路线逐项走，step 1 用 route[1]（中性）⇒ 无反向项 ⇒ `14.184`；
引擎行 241 = `13.8381`（含反向项）。**引擎行为等价于"从 tick 240 起每步都反向"**，
即引擎用的是 route[2]、route[3]…，与上面的"跳过一项"一致。

### 下一轮

1. 给回放加**每 tick 幂等**：需要一个每游戏 tick 递增一次的标记
   （可用 `Player.Update` 的进入点，或观测里新增 tick 字段）；
2. 对齐后重跑探针，再判 `hits`。

## C34. **更正 C33：三个"证伪"全部作废——是我读错了行**（index-vs-value 第 7 次）

### 错误

C32/C33 用"**冲刺起始那一行**"的 `controlLeft/controlRight` 去判断输入方向。
但观测约定是：**一行的控制描述"进入该行"的转变**，即该行的控制属于**上一 tick**。
⇒ tick T→T+1 的衰减由**第 T+1 行**的控制产生，**不是第 T 行**。

### 按正确行重测（fwd4 全 37 次冲刺起始）

| 制度 | 输入方向 | 次数 |
|---|---|---|
| **BASE** | **WITH**（顺向） | **35** |
| other（13.9697） | **OPPOSING**（反向） | 1 |
| **STRONG**（13.8381） | **OPPOSING**（反向） | 1 |

⇒ **完美分离 37/37**。**"反向输入"假说成立**，并非被证伪。

rp4 tick 240 的**第 241 行**是 `L=True` 且 `vx=+14.5` ⇒ **正是反向** ✓
（C33 里"起始帧 L=False R=False"读的是第 240 行，是上一 tick 的输入。）

⇒ **C33 的三条"证伪"（`controlDash`、`accRunSpeed`、`preVX`）全部作废**，
它们都建立在同一个读错行之上。

### 仍存的疑点

fwd4 tick 1175 是 OPPOSING 但只减 **0.3128**（而非 0.3465），
且它是唯一 `maxRunSpeed=2.205`（其余为 4.71）的一次
⇒ **该常数可能不普适**。已如实写进注释。

### 教训

**这是 index-vs-value 类的第 7 次**。前 6 次都在"取哪个数组/哪个索引"，
这一次在"**取哪一行的控制**"——同源：**把"行的值"当成了"转变的值"**。
**凡是要用控制判断"某 tick 做了什么"，必须取该 tick 的下一行。**

## C33. 第二个衰减制度的**三个候选判别因子全部被实测证伪**（**已被 C34 更正，勿引用**）

### 样本（fwd4 全 4393 tick + rp3/rp4 全轨迹，共约 55 次冲刺起始）

修正分类器（原先比了**带符号**值，把 16 个向左的 BASE 误记为 other）后的分布：

| `ctlD` | `acc` | 制度 | 次数 |
|---|---|---|---|
| True | 8 | **BASE** | 36 |
| True | 3 | **BASE** | 1 |
| True | 8 | **STRONG** | **1**（fwd4 tick 3158） |
| True | 8 | other（13.9697） | 1（fwd4 tick 1175） |
| True | 6 | **STRONG** | **1**（rp4 tick 240） |

⇒ **STRONG 极稀有：约 55 次里只有 2 次。**

### 证伪 1：`controlDash` 在起始帧是否按住

**否**。36 次 `ctlD=True, acc=8` 是 BASE；fwd4 唯一那次 STRONG **同样是** `ctlD=True, acc=8`。
（C32 里"起始帧按住冲刺键"的候选也作废。）

### 证伪 2：`accRunSpeed` 6 vs 8

**否**。fwd4 tick 403 是 `acc=8` 且 **BASE**，fwd4 tick 3158 是 `acc=8` 且 **STRONG**。

### 证伪 3：冲刺前水平速度 ≈ 0

**否**。fwd4 tick 403 `preVX=0.0000` 是 **BASE**，rp4 tick 240 `preVX=0.0000` 是 **STRONG**——
**同样的前置速度、不同制度**。

### 已逐字段比对的候选（均不能分离）

`dashType`(2)、`timeSinceLastDashStarted`(0)、`dashDelay`(−1)、`dashTime`(0)、`eocDash`(15)、
`eocHit`(−1)、`maxRunSpeed`(4.71)、`mountActive`、`wingsLogic`(26)、`rocketBoots`(0)、
`wet`、`pulley`、`grapCount` —— 在 403 与 3158 上**完全相同**。

唯一不同：`immuneTime`（403 为 **6**、3158 为 **0**）与 `preVY`（−8.45 vs −5.36）。

⇒ **判别因子不在已比对字段中**。`OpposingInputBleed = 0.3465f` 仍是**无解释的拟合**，
注释已如实标明"移动了分歧点不等于原因正确"。**下一轮应继续找判别因子，或删除该拟合。**

## C32. **冲刺衰减的"形式"被实测出来**（不是比率，是"先减 0.1 再乘"）

### 精确规则（rp4 密集轨迹逐 tick 实测，四位小数吻合）

```
基准:  vx = (vx − 0.1) × decay
高速 (>12): decay = 0.985
低速 (≤12): decay = 0.94
```

**逐位验证**：

| tick | 引擎 vx | 公式 |
|---|---|---|
| 283 | 14.5000 | 起点 |
| 284 | **14.1840** | `(14.5−0.1)×0.985` ✓ |
| 285 | **13.8727** | `(14.1840−0.1)×0.985` ✓ |
| 292 | **11.8208** | 连续 ✓ |
| 293 | **11.0176** | `(11.8208−0.1)×0.94` ✓ |
| 294 | **10.2625** | `(11.0176−0.1)×0.94` ✓ |

**原先记录的 0.9782 / 0.9304 是"表观比率"**，即该式在 14.5 与 ~9 处的取值
（`(9−0.1)×0.94/9 = 0.9296` ✓）。**比率随速度漂移，所以按比率读常数必然有残差。**

### 还存在**第二个衰减制度**，且原因**尚未确定**（不得编故事）

同一条轨迹里两次同为 14.5 起点的冲刺，衰减不同：

| 制度 | 起始 tick | 首步 vx | 拟合式 | 起始帧特征 |
|---|---|---|---|---|
| A（快） | 240 | **13.8381** | `vx×0.985 − 0.445` | `ctlD=True`、`acc=6` |
| C（慢） | 283 | **14.1840** | `(vx−0.1)×0.985` | `ctlD=False`、`acc=8` |

**第一个猜测（"水平输入与运动反向"）已被证伪**：制度 A 的起始帧 `L=False R=False`，
根本没有水平输入。两个候选判别因子是"**起始帧是否按住冲刺键**"与"**accRunSpeed 6 vs 8**"，
**哪个是真的还没测**。

⇒ 代码里的 `OpposingInputBleed = 0.3465f` 是**拟合值**，它把首个分歧从 tick 242 推到 243，
但**在判别因子找到之前不得当作解释**（注释已如实写明）。

### 结果

```
改前: firstDivergenceTick=242  gap=1.03
改后: firstDivergenceTick=243  gap=1.02
```

套件 780 通过 0 失败。

### 下一轮

1. **实测判别因子**：找一条 `ctlD=False` 且 `acc=6`（或 `ctlD=True` 且 `acc=8`）的冲刺，
   即可区分两个候选；
2. 判别因子确定后再改代码，否则 `0.3465` 只是拟合；
3. 残差继续后移后重跑探针。

## C31. **回放桥已验证正确**；真正的分歧在**冲刺衰减常数**（引擎 ≈0.954 vs 模型 0.978）

### ① 对齐实测：桥没问题，`SKIP=0` 才对

rp4 密集轨迹（`SKIP=0`）：

```
firstBossTick=240  firstMoveTick=240
tick=240 px=654.50 py=7951.79 vx=14.500 vy=-6.210 ctlJ=True ctlD=True
```

tick 240 正是路线首项 `0,1,1`，且 `vx=14.500` = 引擎实测冲刺速度 ✓。

**`CHAITE_ROUTE_SKIP` 必须为 0，不是 120**：`_replayFrames` 数的是**插件施加控制的帧**，
而插件要等 boss 接战才开始控制；接管 tick 120 与首个控制帧（≈tick 240）之间的
120 帧**根本不是控制帧**。`SKIP=120` 会把整条路线推迟 120 tick。

### ② 残差比较的**两次索引修正**（index-vs-value 第 6 次）

1. **比错轨迹**：拿 **fwd4（脚本轨迹）**的行去比**组合路线**的走位——两条不同路线，
   第一个 tick 就报 15.77 px"分歧"，其实只是路线不同。改为比 **rp4（回放轨迹）**：
   新增 `CHAITE_RESIDUAL_TRACE` 环境变量。
2. **差一个 tick**：模型走 k 步后的状态对应引擎 tick `threats[0].Tick + k`
   （种子是"进入首个 boss 行 tick"的状态，走一步后是"进入下一 tick"的状态）。

### ③ 真实残差（修正索引后）

```
LOOP COMPOSE DIVERGE tick=242 modelX=682.55 modelY=7939.37
                              engineX=681.52 engineY=7939.37 gap=1.03
                    modelVX=13.869 engineVX=13.186   （vy 完全相同 = -6.210）
LOOP COMPOSE RESIDUAL firstDivergenceTick=242 worstGap=2925.43 walked=1418
```

**模型到 tick 242 只差 1.03 px**，首个分歧在**冲刺期的 `vx` 衰减**：

| | 14.5 → 首步 | 比率 |
|---|---|---|
| 引擎 | 13.838 | **0.9543** |
| 模型 | 14.181 | 0.978 |

⇒ **引擎的冲刺衰减约 0.954，而非 `DashMotion` 记录的 0.978**。
这正是 §C 记的"冲刺 1220 帧未覆盖"缺口，且是**引擎内实测**得到的差异（非推算）。

### 下一轮

1. 从 rp4/rp3 密集轨迹**重新实测冲刺衰减**（多段、区分高低速阈值），修 `DashMotion`；
2. 修完再跑残差，看 `firstDivergenceTick` 是否后移；
3. 残差后移后重跑探针，看 `hits` 是否下降。

**当前如实状态**：桥已通、分支可证执行、模型到 tick 242 只差 1 px，
但整条路线在引擎内于 tick ≈1655 死亡（`hits=8`）。**验收无伤尚未达成。**

## C30. **组合路线接入探针回放通路**（模型 → 引擎的桥）

### 新增 `src/Chaite.Core/RouteReplay.cs`

从文件读逐 tick 控制序列（表头 + `direction,jump,dash`，direction ∈ −1..1，另两个 0/1）。

| 环境变量 | 作用 |
|---|---|
| `CHAITE_ROUTE_FILE` | 路线 CSV 路径；**未设置时整条通路完全惰性** |
| `CHAITE_ROUTE_SKIP` | 回放开始前跳过的**已施加帧数**（路线种子 tick ≠ 接管 tick，必须声明而非猜测） |

读取越过表尾 ⇒ 返回 false ⇒ 该帧交回常规 planner。

### 接入点 `Runtime.cs`（`ApplyPlan` 之前）

**只替换移动控制**——因为路线是对"移动"的断言，强制其余会变成另一个断言。

```csharp
plan.Horizontal = replayDirection;
plan.Jump = replayJump;  plan.Dash = replayDash;
plan.Drop = false;  plan.FeatherFallUp = false;  plan.GravityControl = 0;
plan.ToggleMount = false;  plan.Hook = false;  plan.HoldNeutralControls = false;
plan.JumpAction = JumpAction.Hold;
```

### 待验证（硬约束：须证明分支确实被执行）

轨迹 `game-probe-rt-fwd4-2-0918-160519` 基线：`seed=2 win=True hits=4 ticks=4393`
（`startSide=left`、`platformRows=[]` ⇒ 已是更正后场地）。

探针命令（`CHAITE_ROUTE_SKIP=120`，因接管 tick 120、路线种子 tick 240）：

```
$env:CHAITE_ROUTE_FILE='...\game-probe-rt-fwd4-2-0918-160519\composed-route.csv'
$env:CHAITE_ROUTE_SKIP='120'
runprobe.ps1 -Scenario duke-fishron -Route fishron-strong-wing -Seed 2 -TakeoverTick 120 -Tag rp1
```

**判定**：若 `hits/ticks/damage` 与基线 `hits=4 ticks=4393` **完全相同** ⇒ 分支**未执行**；
若不同 ⇒ 分支已执行，再看命中数是否达到 0。

### 探针实测（硬约束判定：改动前后完全相同即为未执行）

| 运行 | 结果 | 判定 |
|---|---|---|
| 基线（无回放） | `win ticks=4393 hits=4 deaths=0 shots=21` | 参照 |
| 回放 #1 | `win ticks=4393 hits=4 deaths=0` | **完全相同 ⇒ 分支未执行** |
| 回放 #2（修复后） | `loss ticks=1654 hits=7 deaths=1 shots=5` | **已变 ⇒ 分支已执行** |

### 回放 #1 抓到的 bug（铁律生效）

```csharp
if (_replay.TryRead(_replayFrames, out ...)) { ...; _replayFrames++; }   // 错
```

`_replayFrames++` 在 `TryRead` 为真的分支**里面** ⇒ 跳过前缀期间计数器**永不递增** ⇒
`TryRead(0)` 永远返回 false（`index = 0 - 120 < 0`）⇒ **回放永不启动**。

**修法**：计数器移到分支**外面**，每帧递增。

### 回放 #2 的结果：模型路线**未能迁移到引擎**

`hits=4 → 7`、`ticks=4393 → 1654`、`deaths=0 → 1`、**`shots=21 → 5`**。

`shots` 大幅下降说明**输出路线也受影响**（尽管只替换了移动控制，`plan.Jump` 常按
可能干扰射击动画）。**待查原因（按优先级）**：

1. **对齐偏移**：路线种子是观测帧 row 0 = 引擎 tick 240，接管 tick 120 ⇒ `SKIP=120`。
   但 `_replayFrames` 数的是"插件施加控制的帧"，首帧未必正好是 tick 120。
   **须用密集轨迹实测对齐**（比较路线起点处的玩家坐标与模型种子）。
2. **前向模型残差**：§C 的覆盖面缺口（翅膀/冲刺/跳跃/火箭靴）。
3. 输出路线被移动控制干扰（`shots` 下降）。

**结论：桥已通且分支可证执行，但"模型内零受击"尚未迁移到引擎。**
下一步必须先做**对齐实测**，否则无法区分"偏移错"与"模型错"。

## C29. **里程碑：模型内跑通整场猪鲨战零受击**（51/51 闭环、4153 tick、`replayedHits=0`）

### 结果

```
LOOP COMPOSE segments=51 crossed=51 failedAt=-1 composedTicks=4155
             composedHits=0 replayRefusals=0 complete=True widest=48
             routeTicks=4153 replayedHits=0
LOOP COMPOSE ROUTE ...\game-probe-rt-fwd4-2-0918-160519\composed-route.csv ticks=4153
```

**全部 51 个闭环串通，4153 tick 的完整战斗，零受击。**

### 关键：墙是**前沿宽度产物**，不是模型缺陷

`BoundaryWidth` 8 → **48**：

| BoundaryWidth | crossed | 结果 |
|---|---|---|
| 8 | 23 | 8 个分支在段 23 全部穷尽 |
| **48** | **51** | **complete=True** |

### 两项排除性实测（在加宽之前做的，用来定位）

**① 不是字母表造成的**：段 23 观测帧 `down=0 up=0 jump=69 horizontal=69`
⇒ 观测路线**从未使用 Up/Down**，而 12 动作字母表缺的正是这两个
⇒ **缺 Up/Down 不是这道墙的原因**（该结论已实测，勿再据推测加维度）。

**② 不是预算造成的**：`truncated=0`，且唯一的拒绝原因是 `PastLoopEnd=6120`
⇒ 搜索**确实穷尽了可达格子空间**（`expanded=10172` 与 69 tick 的可达格子数量级一致）。

**③ 模型与引擎在此处一致**：段 23 观测帧 `hitRows=40/69` ——
**引擎自己的玩家在该段有 40 帧处于无敌帧**，即观测战斗也飞不干净这一段。
模型说"无干净路线"与之**相符**，不是模型失真。

### 独立校验（不信搜索自己的记账）

回放时**独立计数** `TryStep` 返回的 `hit` ⇒ `replayedHits=0`。

### 尚未完成：**隔离探针逐位验证**（硬约束）

已导出 `composed-route.csv`（`direction,jump,dash`，4153 行）作为探针输入。
**模型内零受击 ≠ 验收无伤**，必须用隔离探针按 ticks/hits/damage 对照验证。

### 下一轮待办

1. 把 `composed-route.csv` 接进探针执行路径（现有 `CHAITE_POLICY_ROUTES` / 路线回放机制）；
2. 跑隔离探针，对照 ticks/hits/damage，确认分支确实被执行（改动前后完全相同即为未执行）；
3. 若探针命中数与模型不符，差异即前向模型在该段的残差 —— 回到 §C 的覆盖面清单。

## C28. **闭环边界携带一组状态**（beam over loops）+ 一个共享列表 bug ⇒ 枚举在 5 个受击闭环中**胜出 4 个**

### ① 枚举器支持返回**多个目标状态**

```csharp
public int CollectGoals = 1;                       // RouteSearchRequest
public readonly List<PlayerMotionFrame> GoalStates; // RouteSearchReport
public readonly List<List<RouteAction>> GoalRoutes; // 每个目标对应的路线
```

到达目标后**不再立即返回**，而是记录后 `continue`（不展开该节点：它已在段末，
后继属于下一闭环的问题）。

### ② **bug：`report.Route` 是累积的共享列表**

第二个目标时**没有清空**，于是 `GoalRoutes[1]` = "目标 0 的动作 + 目标 1 的动作"，
回放必然越过段末 ⇒ 被 `TryStep` 拒绝。

**症状（实测）**：`replayRefusals=149`、`branches=1`、`widest=1`。
**修法**：构建每个目标路线前 `report.Route.Clear();`。
**修后**：`branches=8`、`widest=8`、**`replayRefusals=0`**。

### ③ 组合改为携带**一组分支状态**（`BoundaryWidth = 8`）

每个闭环对前沿中**每个**状态各搜一次，收集其干净目标状态，按 8px/2px 分桶去重，
取前 8 个作为新前沿；前沿为空即该闭环失败。

### ④ 结果：`crossed=23` **未变**，但原因已定位

```
LOOP COMPOSE HITLOOPS 1,2,4,5,23,25,37,38,39,46,48
LOOP COMPOSE FAIL index=23 branches=8 expanded=10172 truncated=0 replayRefusals=0
LOOP COMPOSE segments=51 crossed=23 composedTicks=1576 composedHits=0 widest=8
```

**8 个分支全部穷尽**（`truncated=0` ⇒ 不是预算不足，是空间真的没有干净路线）。

**但真正重要的发现**：段 23 是**受击闭环**，而组合**成功穿过了 1、2、4、5 四个受击闭环**——
即**模型在观测战斗受击的地方找到了无受击路线**。已遇到的 5 个受击闭环中，
**枚举胜出 4 个**，只在 23 卡住。这说明枚举确实**有能力**产出比观测战斗更好的路线。

**下一步**：段 23 的墙是"模型内确实无解"还是"模型/威胁场在该处不准"——
需用隔离探针或对该段做逐 tick 对照来区分。

## C27. **闭环边界改为玩家自选 + 目标必须无受击** ⇒ `crossed` 1 → **23**，1576 tick **零受击**

### 两项枚举逻辑改动（符合铁律：只改枚举逻辑，不手写策略）

**① `TraceLoopWorldOptions.SurviveToEnd`** —— 目标改为"**走完本闭环的 tick 跨度**，
状态任意"，保留**时间界**（闭环切分的真正作用就是限定搜索范围），**放弃位置**：

```csharp
if (state.Tick < _goalTick) return false;
if (_options.SurviveToEnd) return true;     // 不再要求位置
```

依据（C26 实测）：相邻闭环不共享起点、受击闭环的边界只有击退能到达
⇒ **"复现观测边界"是在要求干净路线做它做不到的事**。

**② `RouteSearchRequest.RequireCleanGoal`** —— 目标状态**必须来自零受击路线**：

```csharp
if (node.HasAction && world.IsGoal(in node.State) &&
    (!request.RequireCleanGoal || node.Hits == 0))
```

**为什么必须加**：第一次运行时 `index=23` 返回 `found=True clean=False hits=27`，
而 `expanded` 只有 **271** —— 搜索**一到达段末就返回**，那条路线靠**反复受击**存活。
枚举器原本是"弹出首个目标即返回"，在 `SurviveToEnd` 下这不等于找到答案。

### 结果（铁律：输出**变了**且**变好**）

```
改前: LOOP COMPOSE segments=51 crossed=1  composedTicks=105   composedHits=0
改后: LOOP COMPOSE segments=51 crossed=23 composedTicks=1576  composedHits=0
```

⇒ **23/51 个闭环串起、1576 tick 全程零受击**（模型内）。

### 下一个瓶颈：贪心单状态链会走进死局

`index=23`：起点 `(640, 7810.677)`，`found=False`、`expanded=655`、`refused=396`
⇒ 从贪心链留下的**这一个**状态出发，**确实不存在无受击路线**。

**修法（下一步）**：闭环边界要携带**一组**存活状态（beam over loops），
而不是只传一个；某条分支走进死局时还有别的分支。当前枚举器每次只返回一条路线，
需要让它返回**多个目标状态**（或按 bucket 保留前 K 个）。

**如实记录**：这是模型内的无受击路线，**尚未经隔离探针验证**（硬约束要求逐位验证）。

## C26. **决定性实测：相邻闭环不共享起点** ⇒ 闭环条件**无法拼接**（撤回 C25 的 crossed=3）

### 实测（逐闭环起点，boss 相对几何 + 速度）

```
index=0 startTick=0   relX=-575.00 relY= 339.00 vx=0.000 vy=  0.000
index=1 startTick=104 relX=-381.89 relY= 233.45 vx=0.000 vy= -7.235
index=2 startTick=162 relX=  89.38 relY=-112.20 vx=0.000 vy= -8.450
index=3 startTick=220 relX= 448.52 relY=  74.22 vx=7.910 vy=-10.450
```

**四个相邻闭环的起点在相对几何与速度上全不相同** ⇒
方法原文的"回到循环初始状态"**在本轨迹上无法成立**，闭环之间**无法合法拼接**。

⇒ **撤回 C25 的 `crossed=3`**：那 3 个边界虽然可达，但**状态对不上下一个闭环**，
拼接等于瞬移。**C11 的结论（每个闭环落在自己的相对几何组）由直接实测确认。**

### 可拼接的条件只能是"结束在下一个闭环的观测起点"

```csharp
var goalSegment = index + 1 < segments.Count ? segments[index + 1] : segment;
GoalOffsets(slice, threatRows, goalSegment.StartTick, ...);   // 下一闭环的观测起点
```

**结果**：`crossed=1 composedTicks=105 composedHits=0`；
边界 0 落在目标 **1.5 px** 内（`y=7828.403` vs `goalY=7829.926`）。

### 组合的真正瓶颈：**受击闭环的边界是击退产物**

`index=1` 失败：起点 `(640, 7828.403)` → 目标 `(640, 7583.559)`，**需上升 245 px / 58 tick**。
而 `segments[1]` 的观测路线 `hitAt=369` ⇒ **该目标只有击退的免费上升速度能到达**，
干净路线穷尽 89274 次扩展也到不了（`refused=54288`，`truncated=0`）。

### 下一步的两条路（都需要实测支撑）

1. **闭环边界改为"玩家自选"**：只要求每段无受击且状态合法，不要求复现观测边界 ——
   观测边界在受击处本就不是干净路线能走的。
2. **先用无受击轨迹做参考**：现有轨迹有 11 个受击闭环，其边界不可用作参考；
   需要一条**无受击**的轨迹才能给出合法的闭环参考。

**当前状态如实记录**：`crossed=1`、`composedHits=0`，套件 780 通过 0 失败。

## C25. 组合驱动的**三个 bug**：列表混用、跳段后间隙、以及闭环目标的根本张力（crossed 1 → **3**）

### bug 1：遍历 `chain` 却从 `segments` 取目标（两个不同序）

```csharp
for (var index = 0; index < chain.Count; index++)      // 遍历"干净"列表
var goalSegment = index + 1 < segments.Count ? segments[index + 1] : segment;  // 取全量列表
```

⇒ 第一个边界的目标落在**行 162**，而路线从**行 344** 出发 —— **目标在过去**。

### bug 2：跳过受击闭环 ⇒ 链**不连续**，但 slice 与步数预算只覆盖一段

`chain[0].StartTick=0`（tick 240），目标行 **220**（tick 460）⇒ 需要 **220 tick**，
而预算只有 `slice.Count + 8 = 112` ⇒ 穷尽 416778 次扩展也找不到**观测轨迹自己走过**的路线。

**修法**：把 slice 与预算延伸到目标行。结果 `truncated=1`（撞 2M 扩展上限）——
⇒ 跨越间隙的长途搜索对枚举器不可行。

### 结论：链必须**连续**，用方法原文的闭环条件

```csharp
for (var index = 0; index < segments.Count; index++)   // 连续
slice = segment.StartTick .. segment.EndTick            // 本段自己的跨度
GoalOffsets(slice, threatRows, segment.StartTick, ...)  // 回到本闭环自身起点（相对 boss 表述）
```

**结果**：

```
LOOP COMPOSE THREAD index=0 endTick=344 x=640 y=7957.657 goalX=640 goalY=7958        (0.34 px)
LOOP COMPOSE THREAD index=1 endTick=403 x=640 y=7830.961 goalX=640 goalY=7829.926    (1.04 px)
LOOP COMPOSE THREAD index=2 endTick=461 x=640 y=7585.3   goalX=640 goalY=7583.559    (1.74 px)
LOOP COMPOSE segments=51 crossed=3 failedAt=3 composedTicks=222 composedHits=0
```

⇒ **crossed 1 → 3**，**`composedHits=0`**（3 个闭环、222 tick 全程无受击）。

### 但仍存在**根本张力**（C11 的重述，本轮量化）

闭环目标让每段回到**自己的起点**，而**相邻闭环的起点并不相同**：

```
段 3 自身起点 = (1161.082, 7013.45)   （行 220）
线程化状态   = (640, 7585.3)          （段 2 的终点 = 段 2 的起点）
```

⇒ **拼接并非合法轨迹**（两个闭环的起点不同，相当于瞬移）。`crossed=3` 的边界成功
但**状态对不上下一个闭环**。这正是 C11 记的"每个闭环落在自己的相对几何组"。

**下一步必须解决它**，两条候选：
1. 目标用**下一个闭环的观测起点**（拼接合法），受击闭环改用**搜索自解**的入口而非观测几何；
2. 或把闭环定义改为**相对 boss 的几何不变量**，使相邻闭环共享同一起点（需实测验证是否成立）。

**当前不宣称 `crossed=3` 为有效进展**，只记录边界可达性。套件 780 通过 0 失败。

## C24. **分支边界在 0.1 而非 0**（先减再比较）——段 3 从 13.576 → **0.145**，干净段全部精确

### 定位（同一次上升里的两侧）

```
t=498 vy=0.925 → t=499 vy=0.05     dvy=-0.875   ← 起始为正，走"下落"带
t=499 vy=0.05  → t=500 vy=-0.225   dvy=-0.275   ← 起始仍为正，却走"非下落"带
```

**同一次上升中，起始 vy 都为正却分属两带** ⇒ 判定不是 `vy > 0`，
而是**先减 0.1 再比较**（`vy - 0.1 > 0`，即 `vy > 0.1`）：0.05 < 0.1 走非下落带、
0.925 > 0.1 走下落带 ✓✓

**旧测试名 `DemonThrustUsesPostSubtractionBranch` 原本是对的**——我在 C22 里
把它改名为"实测常量"时，把这条正确的结构信息一并丢掉了。**这是本轮最重要的教训：
改名/重写测试时不要把原测试名里编码的引擎结构知识丢掉。**

### 实现

```csharp
if (velocityY - .1f > 0f) velocityY -= .875f;
else if (velocityY > -jumpSpeed) velocityY -= .275f;
else velocityY -= .125f;
return Math.Max(velocityY, -jumpSpeed * 1.5f);
```

### 逐段对照（铁律：输出**变了**且**变好**）

| 段 | C23 后 | C24 后 | reachedGoal |
|---|---|---|---|
| 3 | 13.576 | **0.1451** | **False → True** |
| 0/5 | 0 | 0 | True |
| 6 | 2.456 | 2.456 | True |
| 7/8/9 | 0.1450/0.1455/0.1455 | 不变 | True |

### 里程碑：干净段的前向模型已**基本精确**

- **10 段中 7 段到达目标**（0, 3, 5, 6, 7, 8, 9）
- **6 段在 0.146 px 内**（0=0、3=0.1451、5=0、7=0.1450、8=0.1455、9=0.1455）
- 未达标的 3 段（1, 2, 4）**全部 `hitAt != 0`**（击退污染）⇒ 属正常
- 段 6 剩 2.456 px

⇒ §C 的"评估器缺口"在**翅膀**一项上已基本补上；下一步转向**冲刺 / 跳跃 / 火箭靴**。

## C23. 翅膀推力的**分档规则**：阈值正是 `jumpSpeed`——段 3 再降到 13.576，段 5 到达目标

### 分桶实测（`jumpSpeed=6.61`，条件 `ctlJump && wingTime>0 && jump==0`）

| 起始 `vy` | 实测 `dvy` | 样本 |
|---|---|---|
| `> 0`（下落） | **-0.875** | 覆盖 0.385..9.135 |
| `(-6.61, 0)`（在 `-jumpSpeed` 之上） | **-0.275** | 337 |
| `≤ -6.61`（达到/低于 `-jumpSpeed`） | **-0.125** | 大量 |

**阈值就是 `-jumpSpeed`**：`[-7,-6)` 桶里两种值混合（-0.125 n=95、-0.275 n=23），
其余桶全部纯净 —— 这**只有阈值落在 -6.61（即该桶内部）才能解释**。

**C22 的规则过度泛化**：当时只有 `vy ≤ -6.7` 的数据，我把它推广到了全部上升段。

### 实现

```csharp
if (velocityY > 0f) velocityY -= .875f;
else if (velocityY > -jumpSpeed) velocityY -= .275f;
else velocityY -= .125f;
return Math.Max(velocityY, -jumpSpeed * 1.5f);
```

### 逐段对照（铁律：输出**变了**且**变好**）

| 段 | C22 后 | C23 后 | reachedGoal |
|---|---|---|---|
| 3 | 17.536 | **13.576** | False |
| 5 | 0 (False) | **0 (True)** | **False → True** |
| 0/7/8/9 | 0/0.145/0.145/0.145 | 不变 | True |
| 6 | 2.456 | 2.456 | True |

⇒ **10 段中 6 段到达目标**（0, 5, 6, 7, 8, 9），段 3 是唯一还大的真实偏差。

### 纠正 C22 的一个结论：下限**在受击段里确实生效**

段 2 的 DIVERGE（tick 422）：`engineVY=-10.95` vs `modelVY=-9.915` = **正是下限**。
C22 里"移除下限后 10 段逐字节相同 ⇒ 从未生效"只在**被审计的度量**内成立——
因为该段的 `hitAt=403`，度量在首次受击处就停止累计，把 tick 422 排除在外了。

**教训**：用"输出完全相同"判定"未执行"时，必须先确认**度量覆盖了被改动的代码路径**。

### 断言更新

`DemonThrustUsesMeasuredConstantDecrement`：`-2.45` 期望 -2.575→**-2.725**，
并**新增两条钉住阈值的断言**（`-6.5 → -6.775`、`-6.7 → -6.825`）；
`DemonLastFuelTickSkipsGravity`：-5.125→-5.275、-4.725→-4.875；
`DemonPlannerUsesPoweredTickWithoutGravity`：-5.125→-5.275。**套件 780 通过 0 失败。**

## C22. **把实测常量写进 `DemonThrust`**（覆盖缺口第 1 项：翅膀）——4 段改善、搜索快 36 倍

### 改动

```csharp
// 旧（被实测否证）
velocityY -= .1f;
if (velocityY > 0f) velocityY -= .5f;
else if (velocityY > -jumpSpeed * .5f) velocityY -= .1f;
// 新（引擎实测总量，含重力）
velocityY += velocityY > 0f ? -.875f : -.125f;
```

**否证依据**：实测转移 `0.385 → -0.49`（净 -0.875）。旧规则会算成
`0.385-0.1=0.285>0 → -0.5` ⇒ **-0.215**，与实测差 0.275 ⇒ 旧分支判定被否证。

### 逐段对照（铁律要求的证明：输出**变了**且**变好**）

| 段 | 修前 maxError | 修后 maxError | reachedGoal |
|---|---|---|---|
| 0 | 0.3745 | **0（精确）** | True |
| 3 | 25.818 | **17.536** | False |
| 6 | 29.378 | **2.456** | **False → True** |
| 9 | 0.4006 | **0.1455** | True |
| 7/8 | 0.1450/0.1455 | 不变 | True |

### 搜索效率暴涨

```
修前: LOOP SEARCH loop=0 expanded=29664 generated=355956
修后: LOOP SEARCH loop=0 expanded=816   generated=9780
```

⇒ **扩展数降 36 倍**。模型越准，支配剪枝越有效——这直接缓解 §C 的"枚举跑不动"。

### 同步更新 5 处断言（旧断言编码的是被否证的规则）

`DemonThrustUsesPostSubtractionBranch` → 更名 `DemonThrustUsesMeasuredConstantDecrement`；
`DemonFinalHeldTickStartsWingsImmediately` -5.11→-5.135；`DemonLastFuelTickSkipsGravity`
-5.1→-5.125 与 -4.7→-4.725；`DemonPoweredWingPrecedesFeatherFall` 1.4→1.125；
`DemonPlannerUsesPoweredTickWithoutGravity` -5.1→-5.125。**套件 780 通过 0 失败。**

### 负面结果（铁律）：下限**从未生效**

移除 `Math.Max(velocityY, -jumpSpeed*1.5f)` 后 **10 段数值逐字节完全相同**
⇒ 按"改动前后完全相同即为未执行"，**该下限在这些段里从未被触及**。
故保留，并记录：引擎那 86 行 `vy` 到 **-12.725** 的深上升，
来自**一条不经过本推力的路径**，而不是本下限有误。

**同时纠正 round 127 的错误归因**：当时"移除下限导致回退"实为 `GoalOffsets`
审计语义 bug 所致——下限的移除**本来就没有效果**。

## C21. 翅膀动力的**引擎实测三规则** + 一个"看似回退实为审计语义被改"的 bug

### 引擎实测（`jumpSpeed=6.61`, `maxFallSpeed=10.01`, `wingsLogic=26`）

| 条件 | 实测 `dvy` | 分布 |
|---|---|---|
| `ctlJump && wingTime>0 && jump==0 && vy<0`（上升） | **-0.125** 恒定 | -6.735→-6.86→-6.985→-7.11→-7.235→-7.36（步长严格 0.125） |
| `ctlJump && wingTime>0 && jump==0 && vy>0`（下落） | **-0.875** 恒定 | 9.135→8.26→…→0.385→-0.49（**跨零不变**） |
| `ctlJump && wingTime==0 && vy<0`（燃料耗尽） | **+0.4** | 纯重力 |

**`DemonThrust` 复现不了这两条**：下落给 -0.6（实测 -0.875），上升给 -0.1/-0.2（实测 -0.125）。
⇒ 这是**引擎内实测**的参考表，供后续精确修 `DemonThrust`（**不得推算**，故只记录不动手）。

### 引擎能达到的上升速度超过模型的下限

- 全场最负 `vy = **-12.725**`；其中 **86 行 `immuneTime<=0`（无击退）**的 `vy` 低于
  模型的 `-jumpSpeed*1.5 = -9.915`，从 -9.93 连续分布到 -12.73，**任何数值上都没有堆积**
  ⇒ 引擎在此分支**没有这个下限**，或者这些深上升来自**另一条直接赋值 `vy` 的分支**。

### 验证过的**负面结果**（按铁律记录）

尝试移除 `DemonThrust` 的下限 `Math.Max(velocityY, -jumpSpeed*1.5f)`：
段 0 0.3745→0.4995、段 7 0.1450→6.289、段 8 0.1455→6.288、段 9 0.4006→6.408，
四段 `reachedGoal` 全部 True→False，而真正偏差的段 3/6 **完全不变**。

**但复查源码后发现：夹紧的移除其实毫无影响**——`Math.Max` 与直接 `return` 的数值差异
来自**我改 `GoalOffsets` 签名时连带改了审计的语义**：

```csharp
// 审计原本要的是"段末行"（观测路线的实际终点）
GoalOffsets(slice, threatRows, baseIndex + slice.Count - 1, ...)   // 旧签名内部做加法
// 改成直接收行下标后，调用点仍传 segment.StartTick ⇒ 目标从"段末"变成"段首"
GoalOffsets(slice, threatRows, segment.StartTick, ...)             // ✗ 整差一个闭环
```

**修法**：审计调用点传 `segment.StartTick + slice.Count - 1`。

**验证**：段 0/7/8/9 恢复为 0.3745/0.1450/0.1455/0.4006 且 `reachedGoal=True`，
与 round 124 逐位一致；套件 **780 通过 0 失败**。

### 教训

**改一个被多处复用的辅助函数的签名时，必须逐个调用点确认语义。**
"改了却像没改"和"没改却像改了"都会发生——**唯一可靠的判据是逐段数值对照**。

## C20. 组合边界的**穷尽失败**诊断：目标锚定已精确，瓶颈是翅膀上升的模型缺口

### 先纠正两个我自己的误读（都已实测）

1. **`beam=0` 不是"beam 为空"**，它是 `PrunedByBeam`。段 2 的失败行里
   `beam=0` 但 `expanded=42441`、`truncated=0`、拒绝全是 `PastLoopEnd=19380`
   ⇒ 搜索**把整个空间穷尽了**，不是被 beam 剪空。
2. **`threatRows` 只收"有 boss 的行"**：本轨迹 4393 行中只有 **4155** 行有 boss，
   前 **238** 行（tick 1..239）没有 ⇒ `bossIndex = engineTick - 240`。
   我先前多次按**引擎 tick** 去量 `threatRows`，读到了无关行。
   **这是"下标 vs 取值"错误类的第 4 次**，务必先确认列表的构造方式再索引。

### 目标锚定**已精确**（独立复核）

```
代码打印:  goalX=1161.082 goalY=7013.45 goalRow=220
独立量测:  bossIndex=220 → engineTick=460 → 玩家 (1161.1, 7013.4)   ✓ 一致
```

⇒ `GoalOffsets` 的"下一段起点相对 boss"锚定是**对的**，不是锚定 bug。

### 真正的瓶颈：观测路线以 `vy=-10.45` 持续上升

```
bossIndex=117 tick=357  x=640    y=7772.3  vy=-2.03  vx=0     wingTime=175 ctlJump=False
bossIndex=220 tick=460  x=1161.1 y=7013.4  vy=-10.45 vx=7.91  wingTime=97  ctlJump=True
```

103 tick 内**右移 521 px、上升 759 px**，`vy` 达 **-10.45**（`wingTime` 175→97 在耗）。
观测路线**干净地做到了**（该闭环无受击），但枚举器**穷尽 401713 次扩展也找不到**
⇒ **前向模型的翅膀上升不足以复现 -10.45 的持续上升**。

这正是 §C 记的**评估器缺口**（翅膀 1275 帧未覆盖）在组合层的体现：
**不是目标锚错，是模型走不到那里。** 补翅膀上升覆盖面优先于继续调组合参数。

### 闭环清洁度统计（新）

`LOOP COMPOSE CHAIN loops=51 clean=40 hitLoops=11` —— 用"段内是否存在 `immuneTime>0`"
判定，**51 个闭环里 40 个干净**。受击闭环的终点是**击退产物**（击退给免费的向上速度，
前向模型不复现）⇒ 其终点与"下一段起点"都不可达。实测：某个此类目标比起点**高 250 px**，
搜索穷尽空间也到不了。**干净的 40 个闭环足够支撑组合**，受击闭环应改用搜索自解而非观测几何。

### 本轮组合状态

`crossed=1 composedTicks=105 composedHits=0`（**已串起部分全程无受击**）。
段 0 边界 `x=640 y=7590.512` vs 目标 `x=640 y=7592.009` ⇒ **1.5 px 内命中**。

## C19. **组合的两处 bug：重放绕过世界、目标没用"下一段起点"**（已修，crossed 1 → **2**）

### bug 1：线程化重放**绕过 `TraceLoopWorld.TryStep`**

```csharp
// 原代码：直接用前向模型，跳过竞技场夹紧与受击判定
if (!PlayerForwardModel.TryAdvance(in frame, in controls, out next, out refusal))
```

⇒ 线程化状态**穿墙**：段 0 走完后站在 **x=166**，而实测竞技场从 **640** 起
⇒ 段 1 从一个**合法路线无法占据**的位置开始，`beam=0` 由此而来。

**修法**：改走 `world.TryStep`（与搜索同一个世界）。

**验证**：

```
修前: LOOP COMPOSE FAIL index=1 ... startX=166.0671 startVX=-5.985284
修后: LOOP COMPOSE THREAD index=0 endTick=344 x=640 y=7958 vx=0 vy=0 goalX=640 goalY=7958
```

⇒ 段 0 的合成路线**精确落在目标点上**（x 与 y 都一致）。

### bug 2：目标瞄准的是**本段终点**，而 `goalSegment` 算了没用

```csharp
var goalSegment = index + 1 < segments.Count ? segments[index + 1] : segment;  // 算了
GoalOffsets(slice, threatRows, segment.StartTick, ...);                        // 用的是本段
```

413-423 行的注释已论证应瞄准**下一段的观测起点**，但代码从未这样做。
后果：目标是**观测路线的终点**；当该路线**受过击**时，终点是**击退把玩家推到的地方**，
而不是任何干净路线会去的位置（段 2 的 `hitAt=403` 就是首 tick 受击）。

**修法**：`GoalOffsets(slice, threatRows, goalSegment.StartTick + goalSegment.Length - 1, ...)`
（签名改为直接接收**玩家行下标**，避免调用方再做加法）。

### 结果

```
修前: LOOP COMPOSE segments=51 crossed=1 failedAt=1 composedTicks=104
修后: LOOP COMPOSE segments=51 crossed=2 failedAt=2 composedTicks=163 composedHits=0
```

- `crossed` **1 → 2**（多串一个闭环）
- 段 0 边界**精确命中**；段 1 边界落在目标 3.4 px 内（容差 4）
- **`composedHits=0`** —— 已串起的部分全程无受击

### 剩余

段 2 仍 `beam=0`（`expanded=42441 generated=509292 refused=19380`）——
段 2 的观测起点本身是击退状态，需查其可行性或跳过受击段。

## C18. **"有翅膀" ≠ "有翅膀燃料"：滑翔分支被 `WingTime>0` 错误门控**（已修，段 7/8 从 140/312 px → **0.145 px**）

### 定位过程

所有偏离点的 `engineVX` 与 `modelVX` **几乎完全一致**（7.905756=7.905756、
7.934886=7.934886、13.566/13.564）⇒ **水平（冲刺）模型已精确，偏离全在垂直方向**。

引擎有**两个终端速度**（实测）：

```
按住 Down:   vy = 10.01 = maxFallSpeed          （365 样本）
松开 Down:   vy = 3.3367 = maxFallSpeed / 3     （142 样本，wingTime=0）
```

`3.3367 = 10.01/3` 正是 `FlightMotion` 滑翔分支的上限：

```csharp
if (controlJump && velocityY > 0f) {
    velocityY += gravity / 3f;
    if (!controlDown) velocityY = Math.Min(maxFallSpeed / 3f, velocityY);
    ...
}
```

而 t=793 实测 `ctlJump=True, ctlDown=False, wingTime=0` ⇒ 引擎**在翅膀燃料耗尽后仍在滑翔**。

### 缺陷

```csharp
var winged = !frame.Grounded && (frame.WingTime > 0f || frame.RocketBoots);
```

`wingTime=0`（燃料耗尽）⇒ 模型判定为**无翅膀** ⇒ 走非翅膀分支、上限 `maxFallSpeed`=10.01，
而引擎仍以 `maxFallSpeed/3` 滑翔。**滑翔只看"是否装备翅膀"，`wingTime` 只管动力飞行。**

**修法**：`var winged = !frame.Grounded && (frame.Flight.WingTimeMax > 0f || frame.RocketBoots);`
（`wingTimeMax=180` 每行都有；燃料门控本来就在 `ApplyAfterJump` 内部的 `powered` 判定里。）

### 验证（输出改变 + 到达目标）

| 段 | 修前 maxError | 修后 maxError | reachedGoal |
|---|---|---|---|
| 6 | 62.75 | **29.38** | False |
| 7 | **140.28** | **0.1450** | **False → True** |
| 8 | **311.58** | **0.1455** | **False → True** |

⇒ **10 段中 9 段 `maxError ≤ 0.40 px`**，4 段到达目标。

### 剩余

①真正的模型偏差只剩 **段 3（25.82）** 与 **段 6（29.38）**；
②组合的线程化起点变为 `x=166.07`，**落在实测竞技场（x≥640）之外** ⇒
   组合的目标锚点/线程化需要复核（`beam=0` 即由此而来）。
③`loop=0` 搜索效率大幅提升（`expanded` 115544 → **29664**，`refused` 190056 → **39192**）。

## C17. **逐段偏差几乎全是"玩家受击击退"，不是模型缺陷**（度量已修正）

### 发现

`index=4 tick=552` 的"垂直反号"**不是翅膀分支**：引擎在该 tick 出现
`vy +8.025 → -3.5`、`vx 8.004 → 4.5` 的不连续，而：

```
t=552  immuneTime=40   ← 玩家刚受伤、获得无敌帧
       eocDash=0 eocHit=-1   ← 不是护盾接触
       位置 y≈6904（地面在 7958，是空中）
       本轨迹 npcs 只有 370（猪鲨本体），projectiles 为空
```

⇒ 这是 **Terraria 的受击击退**（`Player.Hurt` 反转速度）。

**全轨迹"下→上"速度反转只有 4 次：369、552、3178、3875** ——
**恰好就是前两个 DIVERGE 点（369、552）**。⇒ 之前报的"模型偏差"是击退。

### 度量修正：只统计**首次受击之前**的偏差

`LOOP COMPOSE OBSERVED` 新增 `hitAt`，且 `maxError` 只在 `hitAt == 0` 期间累积。

### 结果：**10 段中 6 段模型精确（0–0.40 px）**

| 段 | 旧 maxError | 新 maxError | hitAt |
|---|---|---|---|
| 0 | 0.3745 | **0.3745** | 0 |
| 1 | 165.20 | **0** | **369**（第 2 tick 受击） |
| 2 | 88.81 | **0** | **403** |
| 4 | 238.94 | **0.1453** | 552 |
| 5 | 173.18 | **0** | **577** |
| 9 | — | **0.4006** | 0，**reachedGoal=True** |

⇒ 所谓"62–347 px 偏差"**几乎全部是击退**。真正的模型偏差只剩
**段 3（25.82）、6（62.75）、7（140.28）、8（311.58）**，全部 `hitAt=0`。

### 对闭环方法的意义

① **只有 `hitAt == 0` 的段才能作为保真度基准**；受击段的观测轨迹是击退驱动的，
   自然到不了目标（段 1/2/4/5 的 `reachedGoal=False` 由此而来，不是目标锚点错）。
② 组合时受击段必须由搜索自求出路线，不能照抄观测轨迹。
③ **模型保真度已经达标**（6/10 段 ≤0.4 px），剩下的偏差集中在 4 段，可定点查。

⇒ **下一步**：查段 3/6/7/8 的真实偏差（均为无受击段），
再回到组合（当前 `crossed=1 failedAt=1`，段 1 的 `worldHits=30` vs `engineHits=1` 过度告警）。

## C16. **冲刺启动只取了速度、没采用候选状态 ⇒ 冲刺永远无法续接**（已修，验证通过）

### 缺陷（`PlayerForwardModel.TryAdvance`）

```csharp
if (TryCreateDedicatedCandidate(in dash, out started))
{
    nextVelocityX = started.AfterDashMovement.VelocityX;   // 只取速度！
}
...
next.Dash = dash;    // dash 仍是启动前的状态（DashDelay=0）
```

⇒ `next.Dashing = next.Dash.DashDelay < 0` 为 **false**，
于是**冲刺启动那一 tick 就不在冲刺态**，后续 tick 走普通分支、冲刺**永远续接不上**。
`modelVX` 恒为 **13.9**（一次摩擦后的定值）正是这个原因。

**修法**：采用**整个候选状态** `dash = started.AfterDashMovement`。

### 顺带修掉：`TimeSinceLastDashStarted` 从不推进

`PlayerForwardModel` 从不推进它（只有 `CombatPlanner` 推进），
⇒ 模型在整个 rollout 里冻结在起始行的值（实测：引擎读 24，模型读上限 300）。
已按引擎语义补上：**每 tick `Math.Min(300, x+1)`，冲刺启动那一 tick 重置为 0（不递增）**。

### 验证（铁律：输出必须改变 ⇒ 分支已执行）

`sinceStart` 现在与引擎**逐位吻合**：

```
index=1  模型 24  引擎 24      index=4  模型 33  引擎 33
index=2  模型 16  引擎 16      index=5  模型 9   引擎 9   ← 双方 dashDelay=-1, eoc=15
index=3  模型 41  引擎 41      index=6  模型 162 引擎 162
```

`index=5 tick=586`：模型 `modelDash=True dashDelay=-1 eoc=15 sinceStart=9`，
引擎 `dashDelay=-1 eoc=15 sinceStart=9` **完全一致**；
`modelVX=11.8691` vs `engineVX=11.8208`（**差 0.05**，此前 13.9 vs 12.67）。

**逐段 maxError 明显下降**：

| 段 | 修前 | 修后 |
|---|---|---|
| 2 | 178.20 | **88.81** |
| 3 | 156.63 | **25.82** |
| 4 | 312.88 | **238.94** |
| 5 | 234.29 | **173.18** |

⇒ **冲刺状态机现在被正确串起来了**，C15 实测的衰减常量也终于真正生效。

### 剩余偏差

`index=4 tick=552`：`engineVX=4.5` vs `modelVX=7.90`、`modelVY=+8.42` vs `engineVY=-3.5`
（垂直反号仍在，属翅膀/滑翔分支）；`index=7` `modelVY=10.01` vs `engineVY=3.34`。
⇒ **下一步**：查翅膀/滑翔分支的垂直选择（C13 已记的同一处）。

## C15. **冲刺衰减常量已实测校正，但改动前后输出逐字节相同 ⇒ 按铁律该分支未被执行**；真因是冲刺时间线错位

### 实测（全部 57 个冲刺 run，非目测）

```
冲刺活跃相 = dashDelay == -1，持续 16 tick（timeSinceLastDashStarted 0→15）
按下那一 tick：vx = 14.50（CertifiedStartSpeed ✓）
衰减：14.50 → 7.67（16 tick）
高速段（|vx| > 12）314 个样本：比率 0.9776（簇在 0.9782）
低速段（|vx| ≤ 12）203 个样本：比率 0.9286–0.9320（均值 0.9304）
阈值 12 ✓（12.10→11.82 走高速，11.82→11.02 走低速）
冷却：dashDelay 重置为 30 ✓（CooldownTicks=30 ✓）
冷却期 vx ≈ 8.00/7.90（翅膀速度，不是冲刺）
```

⇒ 原 `HighSpeedDecay=0.985`、`LowSpeedDecay=0.94` **都太慢**，已改为实测的
**0.978 / 0.930**（`GravityDashMotion.cs`），并同步测试断言。

### **但改动前后输出逐字节相同**

`OBSERVED` 全部 `maxError` 一位不差（164.5796 / 178.1951 / …）。
按使用者铁律：**"改动前后完全相同即为未执行"** ⇒ **该分支确实没被执行**。

### 真因：**模型的冲刺/冷却时间线与引擎错位约 7 tick**

加打印冲刺状态后（index=2 tick=409）：

```
引擎: engineDashDelay=-1  engineEoc=15  engineSinceStart=6   ← 正在冲刺
模型: dashDelay=0  eocDash=0  sinceStart=57                  ← 还在冷却
modelVX=13.9（= 14.5×0.978^2，即比引擎晚约 2–3 tick 的衰减）
```

`sinceStart=57` ⇒ 模型上次冲刺启动于 tick **352**，而引擎启动于 **403**。
⇒ **模型在引擎冲刺时处于冷却期**，所以根本走不到衰减分支。

**错位来源（待查）**：引擎在 tick 345 的按下只产生 **1 tick** 的冲刺
（`dashDelay` 345 为 -1、346 即为 30）——因为玩家贴墙 `speed=0 < runSpeed`，
立刻 `EnteredCooldown`。模型在 345 的行为与之不同，导致后续冷却窗口整体偏移。

⇒ **下一步**：对齐 345 那次"贴墙立即进冷却"的行为（`speed=0` 与 `runSpeed` 的比较、
以及 `TimeSinceLastDashStarted` 的重置时机），使 403 的按下能被模型接住；
之后衰减常量才会真正生效，再用 `OBSERVED` 对照验证。

## C14. **控制位 off-by-one（审计自身）**：段 0 偏差 7.48 → **0.3745 px**

### 错误

审计里用**起始行**的控制去推进。但轨迹里的控制位描述的是
**"进入该行"的转移**（早先记录的取样时序）⇒ 用起始行的控制**晚了一 tick**，
**每一个单 tick 事件都会被吞掉**。这正是上一轮"跳跃/冲刺缺失"的真身：

- 引擎刚起跳那一 tick，模型仍站在地上（`modelVY=0`）
- 引擎持续 31 tick 的冲刺，模型从未进入（按下事件读错了行）

**修法**：改为遍历**目标行**，用目标行的控制推进。
附带好处：目标行的列表下标**就是**引擎行，不必再做 tick 换算。

### 效果

| | 段 0 maxError | 冲刺段 VX 对照 |
|---|---|---|
| 修前 | **7.48 px** | `engine 14.5` vs `model 8.01`（未进入冲刺） |
| 修后 | **0.3745 px** | `engine 12.67` vs `model 13.9`（已进入冲刺） |

**段 0 现在 104 tick 内只差 0.37 px**——与已记录的地面接触精度 0.30 px 同级，
即**模型在该段已是精确的**。

### 剩余偏差（已缩小）

- 冲刺速度略差：引擎 `12.67`（**在衰减**）vs 模型 `13.9`（保持）⇒ 冲刺衰减曲线需实测标定。
- 垂直方向仍有个别反号：`index=1 tick=369` `engineVY=-3.5` vs `modelVY=+2.765`
  （翅膀/滑翔分支），需查该 tick 的翅膀与火箭靴状态。
- 累积后每段 62–347 px。

### 组合状态

`LOOP COMPOSE segments=51 crossed=1 failedAt=1`，边界 1 的观测路线
`worldHits=30` vs `engineHits=1`（**过度告警**）⇒ 保守威胁场可能把唯一可行走廊剪掉了。
`startVX=-14.3`（线程化状态正向左冲刺贴墙）。

⇒ **下一步**：①实测标定冲刺衰减曲线；②查 `index=1 tick=369` 的翅膀分支；
③查边界 1 的 30 次误报来源。

## C13. **逐段偏离定位：冲刺状态机缺失**（实测常量已取得）；跳跃启动也缺

修好墙与索引后，逐段量"首次超过 4 px 的 tick 及双方 regime"：

```
index=0 tick=325 error=6.21  modelGrounded=True  modelVY=0     engineJustJumped=True  engineVY=-6.21 engineRocket=7
index=1 tick=353 error=4.50  modelVY=-4.13                       engineVY=-3.63
index=2 tick=403 error=14.50 modelVX=0                           engineVX=14.5
index=3 tick=461 error=6.49  modelVX=8.010  modelDash=False      engineVX=14.5
index=4 tick=519 error=6.51  modelVX=8.010  modelDash=False      engineVX=14.5
index=5 tick=577 error=7.34  modelVX=7.163  modelDash=False      engineVX=14.5
index=7 tick=755 error=6.52  modelVX=7.977  modelDash=False      engineVX=14.5
index=8/9      error=6.44/6.43 modelVX=8.070                     engineVX=14.5
```

### 缺口一（主因）：**冲刺是状态机，模型按成"按键持续"处理**

`engineVX=14.5`（**冲刺速度**）而 `modelVX=8.01`（翅膀速度）或 **0**，且 `modelDash=False`。
但实测：

```
controlDash 按下次数 = 39，tick 345,403,461,519,577,755,813,871,929,...
     ← 间隔恰为 58 = 猪鲨闭环长度（每闭环按一次）
冲刺状态持续 run 长度 = 多为 31 tick（min=1 max=31 mean=23，共 57 段）
eocDash 峰值 = 15（15 出现 595 次）
dashType = 2（4393/4394 恒定）
```

⇒ **按一次 `Dash` 会启动一段最长 31 tick 的冲刺**（`eocDash` 从 15 递减两轮），
期间水平速度 **14.5 px/tick**。模型没有这个状态机，所以在冲刺段给出翅膀速度 8.01 或 0。
**10 段里有 6 段的首个偏离就是它。**

**实测常量**（引擎取得，非推算）：冲刺速度 **14.5 px/tick**、持续 **≤31 tick**、
`eocDash` 峰值 **15**、`dashType=2`、观测路线按下间隔 **58 tick**。

### 缺口二：**跳跃启动**（`justJumped` 那一 tick）

`index=0 tick=325`：`engineJustJumped=True`、`engineVY=-6.21`、`engineRocket=7`，
而模型 `modelGrounded=True, modelVY=0` —— **引擎刚起跳，模型还站在地上**。
与已知的控制/`justJumped` 取样时序（控制位描述的是"进入该行"的转移）有关。

⇒ **下一步**：按上述实测常量实现冲刺状态机（最高优先，影响 6/10 段），
再修跳跃启动；然后重跑逐段偏离，目标是 `maxError` 全面降到个位数。

## C12. **组合失败的真正根因：前向模型没有墙**（已修）；观测路线首次到达目标

### 本轮第三个"索引 vs 值"错误

`threatRows` 按**列表位置**索引，我却用**引擎 tick** 去索引它
（该轨迹跳过了 239 行 boss-less 行，两者不等）⇒ 量出的"偏差 1306–3028 px"是**伪影**。
**同类错误本会话第三次**（C6 已记两次）。修正后模型起步只差 **0.126 px**。

### 真正的根因：**模型穿墙**

修正索引后 DRIFT 显示：

```
tick=241 error=0.126  modelX=639.87 engineX=640  modelVX=-0.126 engineVX=0
tick=252 error=9.797  modelX=630.20 engineX=640  modelVX=-1.507 engineVX=0
```

⇒ 玩家被压在竞技场左墙，**引擎把他钉在 x=640 且 vx=0，模型却继续向左加速**。
到 tick 345 偏差 478 px。**这就是 C4/前几轮记的"瓦片碰撞缺口"**，
也是猪鲨轨迹上所有搜索失败的共同来源。

**实测场地边界**（非猜测）：`minPlayerX=640`（4394 tick 中 **690 个**恰为 640）、
`maxPlayerX=6315.8`、贴墙时地板 `y=7958`
⇒ `ArenaLeft=640`、`ArenaRight=6336`（6315.8+20）、场地长 ≈5676 px ≈ **355 格**
（与"猪鲨 300 余格长直平地"吻合）。

### 语义修正：`ArenaLeft/ArenaRight` 原本是**拒绝**，应该是**夹紧**

原实现越界即 `OutsideArena` **拒绝**（死路）。但引擎的行为是**夹紧位置 + 把撞墙方向的速度归零**。
拒绝会把"墙面"变成"死局"，任何贴墙的路线都变得不可达，搜索会报一个**看起来像穷尽失败、
实际是缺墙**的结果。⇒ 新增 `ClampToArena`（默认关，保持既有行为），
只把**撞墙的那个轴**归零（引擎贴墙时仍会下落/跳跃）。

### 结果：**观测路线首次到达目标**

```
（修前）OBSERVED index=0 worldHits=0 engineHits=0 reachedGoal=False maxError=478.7
（修后）OBSERVED index=0 worldHits=0 engineHits=0 reachedGoal=True  maxError=7.48
```

⇒ 段 0 的**观测路线到达目标**，模型 104 tick 内只差 **7.48 px**，受击判定与引擎**完全一致**。
组合从 `crossed=0`（错误目标）→ **`crossed=1`（正确目标 + 正确物理）**。

### 剩余问题（下一轮）

段 1 起 `maxError=52–346 px` 且 `worldHits=30` vs `engineHits=1`（**过度告警**）。
⇒ 需要：①逐段量偏差定位模型在哪一段开始偏离（可能是空中/火箭靴 regime）；
②查 30 次误报里哪些来自保守边界。

## C11. **闭环组合驱动已建成**；猪鲨 loop 0 找到无伤路线；组合卡在边界 1

### 目标 B 条点名的缺口已补上

`--loop-search-trace` 现在除单闭环搜索外还会跑 `ComposeLoops`：
逐段推进、**把上一段路线的终点接到下一段起点**（不是每段都从观测态重开），
逐段保留**自己的**威胁场（漂移只有几 tick，借用别循环的场就是描述另一场战斗），
并在第一段失败处停下并按**索引**报告，所以覆盖率是"实际跨过的段数"而非外推。

### ⚠️ 一个方法论修正：目标不能是"回到本闭环起点"

`TraceLoopWorld.IsGoal` 原定义是"回到**本闭环**起点"，这是"闭环完美"的字面条件。
但**实测两个真实轨迹上每个闭环都自成一族**（相对几何各不相同）⇒
闭环 i 的终点（= i 的起点）与闭环 i+1 的起点**不是同一状态**，无法接续。
⇒ 组合时目标改为**下一段的观测起点**（相对 BOSS 坐标）；
当两段起点重合时它自动退化为闭环条件。**这不需要改 Core**，只换传入的相对偏移。

### 结果（猪鲨 `game-probe-rt-fwd4-2`，51 闭环，longestLoop=317，clean=47）

```
LOOP SEARCH loop=0 ticks=104 found=True clean=True hits=0
    expanded=115544 generated=1386516 dominance=1080482 beam=239 refused=190056 truncated=0
LOOP COMPOSE segments=51 crossed=1 failedAt=1 composedTicks=105 composedHits=0 complete=False
LOOP COMPOSE FAIL index=1 ticks=58 found=False clean=False hits=0
    expanded=286458 generated=3437496 dominance=2808191 beam=128 refused=342720 truncated=0
LOOP COMPOSE FAIL REFUSAL PastLoopEnd=342720
```

**⇒ 枚举器在真实猪鲨闭环上找到了 104 tick、0 受击的路线。方法端到端跑通了。**

### 边界 1 失败的定性：**空间已穷尽，不是上限问题**

把 `MaxExpansions` 从 20 万提到 200 万后 `truncated=0` ⇒ **整个空间被穷尽仍无解**。
所有 refusal 都是 `PastLoopEnd`（无 `Jumping` 等regime refusal）⇒ 前向模型没拒绝任何 regime。

**已排除的原因**：观测轨迹**是可表达的**——实测 `mountActive=0`、`mountType=-1`（无坐骑），
控制组合只有 `L/R × J × D` 共 7 种，全在 12 动作字母表内。

**⇒ 剩下的两个嫌疑**：
1. **保守威胁场把真实路线剪成了"受击"**。
2. **支配剪枝把答案删掉了**——正是目标 **D 条**警告的失效模式
   （bucket 塌掉与"是否满足目标"有关的维度）。

⇒ **下一步**：①对边界 1 做"观测路线是否被误判为受击"的对照实验
（用观测控制序列喂进 `TraceLoopWorld`，看是否报 hit）；
②若被误判 ⇒ 修威胁场的保守度；若没被误判 ⇒ 查支配剪枝的 bucket 定义。

## C9. ⚠️ 性能基准失败是**环境性**的（使用者正在玩游戏）

本会话中 `PlannerRichWorkloadBenchmark` / `BeamRichWorkloadBenchmark` /
`CalibratedMotionWorkloadBenchmark` 间歇失败（p95 12–34 ms vs 12 ms 上限），
且**每次失败的项不同**。实测机器状态：

```
Palworld-Win64-Shipping: 507% of one core, 4.8 GB working set
system-wide: 70.7% of 32 cores
```

⇒ **不是回归**：同一构建在负载低时全绿（p95 2–5 ms）。
**使用者共用这台机器，不得关闭其程序。** 基准失败时先查 `Palworld-Win64-Shipping`，
不要追这个"回归"。


## C6. 本轮两次**同类**索引错误（"索引 vs 值"）

1. `start.Tick = segment.StartTick`（列表索引）而 `_firstTick` 是**引擎 tick** ⇒ 12 个动作全部
   `PastLoopEnd`。
2. `threats[i]` 与 `rows[i]` 按位置配对，但 239 行（战斗前）无 BOSS 被跳过 ⇒ **错位**，
   报出 minGap=2.88 px，而真实值**是 0**。
⇒ **教训：任何时候把两个列表按位置配对，先证明它们等长且同源。**
   `rawRows=3928` vs `threats=3689` 这个数字本该在第一眼就暴露问题。

## E. 进度锚点

| 项 | 值 |
|---|---|
| 测试 | `Chaite.Tests.exe` **762 项通过 0 失败** |
| 提交 | `f6fd844` |
| 7 套配装 | **全部未达无伤** |
| 历史最好 | `hits=3`（猪鲨/高级翅膀，右起，seed 2，0 死亡，minLife=214） |
| 已训练策略 | **全部作废**（场地更正 + 输入维度 38→40 + 冲刺头 2→3 路），需重训 |

## F. 不可动摇的硬约束

- 一次训练运行期间**不得重编译**（探针跨构建即作废）。
- 物理常量**必须引擎内实测，不得推算**。
- 任何改动须用**隔离探针 ticks/hits/damage 对照**证明分支确实被执行
  （改动前后完全相同 = 未执行）。
- 验收 = **获胜且零受击**（`hits==0`）。
- 与使用者对话**用中文**。
