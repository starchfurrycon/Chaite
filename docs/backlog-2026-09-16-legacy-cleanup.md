# 历史遗留清理清单（2026-09-16）

本文件只记录**已核实**的遗留项与前置条件，不记录猜测。每条都给出证据位置。
按「主体程序收尾后清理」的约定，这里的内容**不阻塞**当前训练工作。

---

## C1. 召唤机器是遗留代码，但已被 IL 级不变量隔离（不是需要重架构）

**用户提出的正确顺序**：启动 → 确认准入 → 监视；玩家自行召唤 → 监视到 BOSS 出现 → 立即接管。
**不允许**：启动后直接接管并让程序召唤 BOSS。

**核实结论：正确顺序已经是当前架构，并且是被强制保证的。**

`tests/Chaite.Tests/BossMonitoringTests.cs:698`
`BossMonitoringProductionHasNoSummonOrSurvivalPath` 用
`Mono.Cecil` 读取 `Chaite.Plugin.Runtime` 的 IL 并断言：

- 生产 Runtime 的任何方法体内**不得出现**对
  `ExecuteBossStart`、`FindBossStartPlan`、`PlanSurvival` 的调用
  （失败信息逐字为 `Production monitor must not retain auto-summon/survival calls`）；
- `ArmBossMonitor` / `StopBossMonitor` **不得**调用
  `ApplyPlan`、`SetSelectedItem`、`ClearCombatControls`
  （`Passive monitor must not write native controls/selection`）。

所以**遗留项只是死代码的删除**，不是重新设计。仍留在树里、但生产 Runtime 不得调用的部分：

| 位置 | 性质 |
|---|---|
| `src/Chaite.Core/BossStartPlanner.cs`（221 行） | 纯选择器；`TerrariaFacade.cs:838` 注释自述「Runtime admission gate, not this pure selector, owns the live allowlist」 |
| `TerrariaFacade.cs:2891-3204` `ExecuteBossStart` / `ExecuteFishronStart` / `ExecuteSummonPulse` / `ExecuteLacewingStart` / `ExecuteWallStart` / `RestoreVoodooRemainder` / `RestorePendingBossStart` / `EnsureTruffleWormIsFirstBait` / `RestoreFishingBaitOrder` | 召唤／钓鱼／巫毒娃娃执行体 |
| `Runtime.cs:24` `private static BossStartPlan _startPlan;` | 被 `RestorePendingBossStart` 在 110/820/921 行读取、在 888 行置空，但 Runtime 被禁止调用 `FindBossStartPlan`，**赋值来源需要单独确认**；疑似恒为 null 的残留字段 |

**待确认（下次清理时第一步）**：`_startPlan` 的唯一赋值点。若确实无来源，
则 `RestorePendingBossStart` 是空操作，这一整条链可以安全删除。

**注意**：`tools/GameProbe.cs` 的 `StageEmpress` 等是**探测台架**在布置 boss，
这是隔离探针的正确做法（玩家/台架负责召唤，插件只负责接管），**不属于遗留项**。

---

## C2. Tabi 的冲刺剖面未建模，因此不能准入

**物品 ID：977**，依据 [官方 wiki Tabi 页](https://terraria.wiki.gg/wiki/Tabi)（Internal Item ID: **977**）。
同页两条决定性细节：

1. **"Performing a dash will _not_ make the player invulnerable to enemy attacks."**
   ⇒ 与源码级结论一致：Tabi 冲刺**完全不给无敌帧**。
2. **"The Tabi's dash achieves slightly lower speed than the Shield of Cthulhu's dash."**
   ⇒ 与圣盾**不是同一条冲刺**。

而 `src/Chaite.Core/GravityDashMotion.cs:237` `CertifiedStartSpeed = 14.5f`，
整个 `EyeShieldDashMotion` 自述为
`Closed vanilla 1.4.5.8 Shield of Cthulhu dash profile`。

**因此放宽光女准入需要三件套，缺一不可**：

1. `TerrariaFacade.cs:1304` 已读取 `tabiSources`，但在 1306-1314 的三元链里
   **落进 `Unknown`** ⇒ 需要新增 `DashEquipmentIdentity` 取值并接线（item 977）；
2. `ReviewedDashIdentity.IsSupported`（`GravityDashMotion.cs:169`）需要接纳新取值；
3. **必须在引擎内实测 Tabi 的冲刺剖面**（起步速度／衰减／冷却），否则是让预测
   **变错**而不是**变缺**——`EyeShieldDashMotion` 的校验会 fail-closed，但一旦
   放行 `IsSupported` 就等于声称能预测它。

---

## C3. MNG 是否被用圣盾剖面建模，需要实测确认（影响猪鲨配装 2、3）

`Master_Ninja_Gear` 的配方是 **Tiger Climbing Gear + Tabi + Black Belt**
（同上 wiki 页 `Used in` 表），即 **MNG 的冲刺就是 Tabi 的冲刺**。
而 C2 已证 Tabi 速度**低于**圣盾，且 `ReviewedDashIdentity.IsSupported`
把 MNG(984) 与圣盾(3097) 并列接受，`EyeShieldDashMotion` 只有单一圣盾剖面常量。

⇒ **存在 MNG 被按圣盾剖面建模的可能**。这直接影响猪鲨翅膀配装 2、3 的机动预测。
**这是测量项，不是推断项**：需要在引擎内分别记录 MNG 与圣盾冲刺的
起步速度／衰减／冷却，再决定是加分支还是拆模型。

---

## C4. 猪鲨（5/7 个模型）当前卡在准入与建模，不卡在训练

已准入：坐骑 50（史莱姆女士鞍）→ `FishronQueenSlime`；
坐骑 64/65（可靠的疾旋鼬及其换皮）→ `FishronTrustyChillet`（本轮已合并）；
翅膀路线 761（仙灵）／强翼 + 冲刺 + 蛙靴 → `FishronFairyWingsDash` / `FishronStrongWingsDash`。

**猪鲨全部配装强制携带狱火药水**，已由
`FormulaMobilityContract.TryValidateBubbleClearance` 在准入点校验，
`FishronThreatCatalog.RequiredInfernoPotionStock` 为下限。

**仍缺**：

| 缺口 | 说明 |
|---|---|
| **莉莉丝项链（坐骑 52）准入** | `VanillaMountCatalog` 有 `E(52, 5130, 342, "wolf")`（item 5130 / buff 342）；`FormulaRouteCatalog.Select` 目前对 370 只有 50/64/65，其余 `mountType >= 0` 一律 `None`；`FormulaMobilityContract.ExpectedMount` 也需相应分支 |
| **气球束与多段跳建模** | `src/` 下**不存在**任何名为 `Balloon` / `MultiJump` / `CloudInABottle` 的符号 ⇒ 完全未建模。机制见 C4a |

### C4a. 气球束（配装 1 的核心）——已从 wiki 核实

**物品 ID：1164**，依据 [Bundle of Balloons 页](https://terraria.wiki.gg/wiki/Bundle_of_Balloons)。

- **四段跳**（基础 + 3 段），并 **+33% 跳跃持续时间、+30% 跳跃速度**；
- 三段顺序**固定为 沙尘暴 → 暴雪 → 云**（`Sandstorm in a Bottle` → `Blizzard in a Bottle` → `Cloud in a Bottle`）；
- **不与**任何用于合成它的饰品、或其前置气球叠加；
- **不提供摔落伤害免疫**（wiki 明确警告配合火箭靴可能落地 400+ 伤害）；
- **与翅膀的交互**：按住跳跃键会**立即**进入翅膀飞行且**不消耗**附加跳；**按下**跳跃键则消耗下一段附加跳而不是启动飞行；若持续按住，附加跳结束后立即接飞行；飞行时间耗尽仍未松手则进入缓降，此后剩余附加跳每段结束后立即接缓降；
- **青蛙腿（蛙靴系）会显著增强其跳跃高度与速度**；
- **七段跳组合**：气球束 + （屁瓶 或 屁气球）+ （海啸瓶 或 鲨鱼龙气球）+ 祝福苹果，
  顺序为 `Jump > 独角兽(祝福苹果) > 沙尘暴 > 暴雪 > 屁 > 海啸 > 云`；
- wiki 逐字写着 **"When using a non-flying mount, like a Unicorn or Lilith's Necklace|Wolf, the extra jumps can be more useful than wings."**
  ⇒ **直接印证配装 1（莉莉丝项链 + 气球束 + 羽落）的机动来源就是附加跳，不是翅膀**。

**准入的最小充分改动**：`IsMobilityAccessory` 加入 **1164** 即可覆盖用户所述的配装 1。
其余瓶子（云／暴雪／沙尘暴／屁／海啸）与闪光红气球的 ID **尚未核实**，
仅在需要准入它们的变体配装时才需要——**不在本轮猜测性填入**。

### C4b. 多段跳的 oracle 测量计划（占用构建，尚未执行）

**为什么必须实测而不是推算**：wiki 给了定性规则但**没有给任何数值**
（+33% 跳跃持续时间、+30% 跳跃速度是相对量，未说明与蛙靴如何复合；
每段附加跳的竖直冲量完全未给出）。按本项目规矩，物理常量必须引擎内实测。

必须在引擎内记录的、wiki 未回答的量：

1. 每段附加跳是否**重置** `velocity.Y` 为固定值，还是相对当前速度叠加；
2. 基础跳／沙尘暴／暴雪／云 四段的竖直冲量与滞空 ticks 各自是多少；
3. `+33% 持续时间` 与 `+30% 速度` 在**有蛙靴**（`FrogLegItem` 2423 /
   `AmphibianBootsItem` 3990）时如何复合——是相乘还是取较大者；
4. **与羽落（缓降）的交互**：wiki 说"飞行时间耗尽仍未松手则进入缓降，此后
   每段附加跳结束后立即接缓降"。需要确认缓降状态下**按下**跳跃是消耗一段
   附加跳、还是被缓降状态吞掉；
5. **与坐骑的交互**：莉莉丝项链不能二段跳，那么骑乘时附加跳是被抑制、
   还是照常可用（这决定配装 1 的机动模型是"坐骑+跳"还是"仅坐骑"）；
6. 附加跳的**剩余次数**如何暴露（`Player` 上的哪个字段／`jumpOption` 家族），
   以及落地／踩平台／进液体时是否重置。

**测量方法**（沿用既有 native-trace 模式，见 `test-native-flight-evidence.ps1`
与 `NativeFlightTraceTests.cs`）：在 `tools/GameProbe.cs` 增加一个跳跃 oracle
场景，喂入脚本化输入轨迹（按住跳跃 N ticks → 松开 → 在固定 tick 按下跳跃），
逐 tick 记录 `position.Y`、`velocity.Y`、剩余附加跳数，输出为可被测试消费的
轨迹文件。然后写 `ExtraJumpMotion` 模型，用同一套
`Native*TraceTests` 的对照方式验证建模轨迹与原生轨迹**逐帧一致**。

#### 侦察结果（2026-09-16）：**这套 oracle 基础设施已经存在，只需扩展**

我原先以为要从零新建"跳跃 oracle 场景 + 脚本化输入 + 逐帧轨迹"。
**实际上探针里已经全都有了**，C4b 只是**扩展**它们：

| 已有机制 | 位置 | 说明 |
|---|---|---|
| 运动场景 | `GameProbe.cs:890-891` | `motion-jump`（配 `-motioncase`）与 `motion-flight`（配 `-flightcase`） |
| 参数注册 | `:824` | `-motioncase`、`-flightcase` **已在**合法键列表内 |
| 布置 | `:1549-1566` | 裸装 + 可选恶魔翼 / 闪电靴 / 云瓶；**羽落用原生 `player.AddBuff(BuffID.Featherfall,36000)`**，注释明写"永不直接赋值 `slowFall`：`UpdateBuffs` 必须每帧像真实药水那样推导" |
| **逐帧轨迹** | `:2543` | 已写出 `chaite-native-motion-frame/v1` 与 `chaite-native-flight-frame/v1` |
| **脚本化输入** | `:2773-2775` | 按 case 命名约定：`ticks<=20 \|\| ticks>80` 时不按；`-tap` 后缀在第 21 tick 按下；`-release-press` 后缀除第 26 tick 外都按 |
| 帧数常量 | `:69` | `MotionWarmupFrames=20, MotionTotalFrames=180` |
| 装备报告 | `:1572-1590` + `:3075`/`:3121` | `equipmentReport` 报告 `wingItemType` / `featherfallActive` / `featherfallBuffType` / `featherfallSourceItemType` / `bootsItemType`，**写入 `result.json` 的 `equipment` 字段** |

**关键点**：`:1570` 的标签形如 `"motion: naked, no accessories"`，并声明
`noDirectJumpStateOverrides` 与 `noWeaponsAmmoConsumablesOrMount`——
**这个装置的设计意图正是"不加任何直接状态覆盖地测量跳跃/飞行物理"**，
完全对应 C4b"必须引擎内实测、不得推算"的要求。

**因此实际工作量比原估计小一个量级**，只需三处扩展：

1. **布置分支**（`:1549-1566`）：增加坐骑 52（`player.mount.SetMount(52, player)`）
   + 气球束 1164 + 可选羽落。
   **注意 `:1576` 当前声明 `noWeaponsAmmoConsumablesOrMount`，该断言需相应调整**，
   否则报告会与实际布置矛盾。
2. **新 motion case**：在 `:927-931` 的 case 白名单与 `:2773-2785` 的输入调度里
   加一个（例如 `lilith-balloon-tap`），实现"按住跳 → 松开 → 再按"以触发第 2/3/4 段跳。
3. **轨迹字段**：确认逐帧轨迹是否包含**剩余跳跃次数**（这正是未知项之一），
   若没有则在该 case 里把候选字段一并写出以便判别。

**尚未核实（不得当成已结论）**：我**没有**确认 `motion-jump` / `motion-flight`
是否已被接线到 `run-boss-validation.ps1` / `start-isolated-test.ps1` 的调用路径。
若未接线，扩展后还需补调用侧的允许列表与场景映射——与 C4c 第 6 步同型，代价已知且很小。

**下次继续 (b) 时从第 1 步（布置分支）开始**：它是其余两步的前提，
且能立刻用 `result.json` 的 `equipment` 字段验证布置是否正确。

**准入之间的关系**：在 `ExtraJumpMotion` 存在并逐帧对照通过之前，
`FishronLilithNecklace` 路由不得声称能预测多段跳轨迹——这与 Tabi 的理由
相同（见 C2）：缺少剖面时预测会**变错**而不是**变缺**。

### C4c. 配装 1 的完整前置条件（准入已撤回，必须与脚本同时加入）

**结论：只加路由准入是不完整且有害的，已撤回。**

一个"通过准入但没有脚本"的路由比直接拒绝更糟：它对玩家**声称支持**，
然后拿错误的脚本去打。

#### 为什么最初没发现

第一次排查用的是 `grep 'case FormulaRoute\.'`，只找到两个文件，看起来齐全。
但**路由→脚本的分派是 `==` 比较链而非 `case`**，写在
`CombatPlanner.cs:808-836`，所以那次 grep **结构性地看不到它**。这是本次的真正教训：
枚举分派可能以 `==` 链、字典或工厂形式存在，只搜 `case` 会漏。

未分派的路由会落到 **`CombatPlanner.cs:835-836` 的通用兜底**
`script = FormulaScriptController.Tick(in input)`，即被**通用脚本**驱动。
（若脚本自身明确拒绝，`837-838` 行会安全地返回
`UnsupportedMobilityRoutePlan(plan, "未识别的公式 Boss 阶段")`。）

#### 两处必须同时登记的缺口

1. **需要一个新脚本 `FishronLilithNecklaceScript`。** 两个既有的猪鲨坐骑脚本
   **都无法复用**，因为它们对路由**和**坐骑都做了硬门禁：
   - `FishronQueenSlimeScript.cs:38` 要求 `input.Route == FishronQueenSlime`，
     `:44`/`:56` 要求 `Selected/ActiveMountType == 50`；
   - `FishronChilletScript.cs:36-37` 要求路由是 `FishronTrustyChillet` 或
     `...Ignis`，`:59`/`:70` 要求对应坐骑。
   莉莉丝（坐骑 52，狼，不能飞也不能二段跳）与这两者机动性都不同，
   所以这是**一个独立脚本**，不是复用。
2. **需要一个探针场景 `fishron-lilith-necklace`。**
   `tools/run-boss-validation.ps1:547-549` 维护**场景名 ↔ 路由名**映射
   （如 `'fishron-queen-slime'='FishronQueenSlime'`）。训练用 `-Route`
   指定场景、`-PolicyRoute` 指定枚举名，**两者都必须存在**。该场景需布置
   坐骑 52 + 气球束(1164) + 羽落 + **狱火药水**（猪鲨全配装强制）。

#### 加入顺序（不可颠倒）

1. 用 C4b 的 oracle 实测多段跳物理（纵向冲量、与蛙靴的复合、与缓降及坐骑的交互）；
2. 按实测值写 `ExtraJumpMotion`，以 `Native*TraceTests` 的方式逐帧对照通过；
3. 写 `FishronLilithNecklaceScript`（机动模型就位后才可能"预测正确"）；
4. 在 `CombatPlanner` 分派处登记新路由；
5. 加 `IsMobilityAccessory(1164)` **并**在 `TerrariaFacade.cs` 的功能装备读取处
   把它加入允许清单（只加前者会让它从"被静默忽略"变成"被判为意外物品"而拒绝）；
6. 加探针场景与 `run-boss-validation.ps1` 映射；
7. 最后才恢复路由准入（枚举、`Select`、`BelongsToBoss`、
   `IsAcceptableMount`、`ExpectedMount`）与测试。

#### 一并撤回的次要决定

气球束识别（1164 加入 `IsMobilityAccessory` 与 facade 允许清单）随准入一起撤回。
理由：在没有使用气球束的路由时，它**只会放宽一道安全检查**而无收益，
应在第 5 步与脚本一起加入。

### C4d. C4 表剩余的两项未建模缺口

| 缺口 | 说明 |
|---|---|
| **疾旋鼬的多段跳与羽落** | 坐骑状态下多段跳与羽落的交互未建模 |
| **羽落三档在坐骑路径上的行为** | 见 C5 |

---

## C5. 羽落「BUG」无法重现——已建模且有测试（撤销该项）

外部分析曾提出「1/3 常态档未建模」。**在本代码库中不成立**：

- `src/Chaite.Core/JumpMotion.cs:82` `ApplyGravityChecked` 已按
  `controlDown` / `controlUp` 选择除数 **1 / 3 / 10**，并实现
  `/3` 上限与 `/5 → /10` 触发带；
- `tests/Chaite.Tests/JumpMotionTests.cs` 按名字固定三档：
  `FeatherFallUsesThirdGravityAndFallCap`(160)、
  `FeatherFallUpUsesTenthGravityAndFallCap`(172)、
  `FeatherFallDownRestoresOrdinaryGravity`(192)；
- `tests/Chaite.Tests/NativeFlightTraceTests.cs` 拿建模轨迹与原生轨迹对照，
  含 `slowFall` 逐帧比较与 `featherFrames` 计数。

⇒ 按本项目「先测量再改」的规矩，**无可证缺陷，故未改动**。
若日后观察到具体 ticks/hits/damage 差异，再按同样标准复现。

`src/Chaite.Core/FlightMotion.cs:28` 的注释（"Authorizes only the optional Up
branch"）描述的是 `CanRequestFeatherFallPotionUp` **只放行 Up 请求**这件事，
不是说 1/3 档未建模——这两件事此前被混为一谈。

---

## C6. 其他

- `artifacts/training/<tag>/log.csv` 是**追加**语义，同一 tag 重启会叠加两份
  `1,parent` 行。已用「每种子取最新目录」缓解读取端，但写入端仍未修。
- `tools/train-policy.ps1` 无断点续训能力。
- `docs/route-equipment-matrix.md` 的更正段（C1–C8）与
  `docs/diagnosis-2026-09-16-search-has-no-gradient.md` 是当前有效的结论来源。

---

## C7. 七套配装的当前实测基线（2026-09-16，构建 `7FFDBD04EB6DB848`）

**先修正一处我先前的错误判断**：我曾说"7 套里有 4 套连探针都无法运行"。
**事实是 7 套里有 6 套现在就能探测，只有配装 1（莉莉丝）缺 fixture。**
逐套核对如下：

| 配装 | fixture | 现在能否探测 |
|---|---|---|
| 1 莉莉丝项链 | **无** | ✗ 需构建 |
| 2 仙灵翅膀 | `fishron-fairy-wing` | ✓ |
| 3 猪鲨翅膀 | `fishron-strong-wing` | ✓ |
| 4 疾旋鼬 | `fishron-trusty-chillet` / `-ignis` | ✓ |
| 5 史莱姆女士鞍 | `fishron-queen-slime` | ✓ |
| 6 光女扫帚 | `empress-broom` | ✓ |
| 7 光女强翼 | `empress-strong-wing` | ✓ |

**因此构建不是 6 套配装的瓶颈，"训练能否真正爬升"才是。**
配装 1（莉莉丝）是唯一必须动构建才能开始的一套。

**固定脚本、每种 6 条种子、不使用 `-StopOnHit`（让整场打完，以便观察胜负与受击数）。**
命令形式：`runwave.ps1 -Scenario duke-fishron -Route <fixture> -Seeds (1..6) -Tag <tag>`。

| 配装 | fixture / 路由 | 胜场 | 获胜时受击 | 失败时受击 | 伤害范围 |
|---|---|---|---|---|---|
| 2 仙灵翅膀 | `fishron-fairy-wing` / `FishronFairyWingsDash` | **3/6** | 3, 8, 7 | 9 | 57k–78k |
| 5 史莱姆女士鞍 | `fishron-queen-slime` / `FishronQueenSlime` | **1/6** | 9 | 8–10 | 29k–77k |
| 3 更高级翅膀 | `fishron-strong-wing` / `FishronStrongWingsDash` | **0/6** | — | 7–9 | 40k–62k |
| 4 疾旋鼬 | `fishron-trusty-chillet` / `FishronTrustyChillet` | **0/6** | — | 8–10 | **7k–17k** |
| 6 光女扫帚 | `empress-broom` / `EmpressBroom`（训练三代后） | 0 | — | 1（`-StopOnHit`） | 存活 1876→3908 ticks |
| 7 光女强翼·**夜** | `empress-strong-wing` / `EmpressStrongWingsDash` | **0/6** | — | 11–15 | 41k–61k |
| 7 光女强翼·**昼** | 同上，`-Scenario empress-day` | **0/6** | — | **1（即致死）** | **存活恒为 962 ticks** |
| 1 莉莉丝项链 | 无 fixture | 无法探测 | — | — | — |

### C7-0. 决定性战略事实：7 套里只有 2 套用手写脚本能赢

**只有配装 2（3/6）与配装 5（1/6）能取胜。其余 5 套一场都赢不了。**
所以残差训练要解决的问题是"把**不会赢**的脚本变成会赢"，而不是打磨一个会赢的脚本。
这正是光女扫帚那套的状况——三代训练后仍 0 胜。

**因此优先级 (a)「让训练真正爬升」确实是唯一的瓶颈**，不是 fixture、不是构建。
补测 4/7 的基线也确认了这一点：它们的脚本比配装 3 还弱。

### C7-1. 配装 7 白天光女：一击即死，且 962 ticks 跨种子恒定

`result.json` 证实路由身份正确（`observed=EmpressStrongWingsDash`，`mismatches=0`），
日志里没有"未识别"记录。但六条种子的 `ticks` **恰好全部是 962**、`hits=1`、`deaths=1`。

**跨种子逐位相同**说明种子没有影响结果，这是"确定性开场 + 首个不可避免的攻击"的指纹。
结合已知事实（**白天光女的接触/冲刺与 ≥9999 伤害弹幕属无敌帧 Group 2，无法闪避**）
与低防御的游侠 fixture，**一次命中即致死**。

**夜间同一配装存活 3609–5084 ticks、11–15 次受击**，说明白天不是"差一点"，
而是**结构性不可行**：翅膀配装对白天光女需要**纯几何规避**（或用混沌杖）。
**待用户确认**：配装 7 是否应以夜间光女为训练目标。

### C7-2. 配装 2 的受击来源（最好那条种子：win, 3 次受击）

`hurt-observations.jsonl` 给出三次受击的完整来源：

| 次序 | tick | 来源 | `dodgeable` |
|---|---|---|---|
| 1 | 682 | NPC **371**，`life 1/1` → 猪鲨的**爆裂气泡** | **true** |
| 2 | 1643 | NPC **370**，`life 51929/78000` → **猪鲨本体接触** | **true** |
| 3 | 3752 | NPC **373**，`life 94/150` → **鲨鱼龙 Sharkron** | **true** |

**三次全部 `dodgeable: true`**，即都不是无法闪避的攻击，而是普通闪避失败——
**可以用几何/时序修正解决**。脚本对三类威胁都已有处理分支
（`FishronWingScript.cs:501-511` 爆裂气泡、`:512-522` 鲨卷风、`:421/441` 鲨鱼龙），
所以问题是**处理质量**而非缺失处理。

同时 `shield-events.jsonl` 证明圣盾冲刺在工作
（tick 345/403/461，`eocDash:15`，`requested`/`started` 均为 true，
且 `lifeBefore`/`lifeAfterDash`/`lifeAfterFrame` 三者相等 = 冲刺期间未受击）。

**这给出一个高价值线索**：圣盾冲刺**在接触时有短暂无敌帧**，
而第 2 次受击是"本体接触"——**冲刺时序**正是规避单次接触命中的杠杆。
且冲刺头只有 2 个 logits（`$HeadRows.dash = @(8,9)`），
把它单独开放（`-ActiveHead dash`）是一种**小而连贯**的行为改动，
正好符合 C7c 测出的"该残差空间是刀锋型最优、扰动必须小而连贯"。

### C7-3. 冲刺头实验（`fishron-fairy-wing-nohit-v2-dash`）：杠杆失败，但否定了一条假设

父代 `0.34958 / wins=3 / meanTicks=4402`，与 v1 那轮**逐位相同**——
这是有用的确认：**零权重在任何头集合下都精确复现固定脚本**
（掩码只影响候选的可训练槽位，不影响零权重时的行为）。

第 1 代 16 个候选（6 种子/候选）结果：

| 头集合 | 惰性候选（与脚本逐位相同） | 有实质改变且优于父代 |
|---|---|---|
| `all`（C7c 的 v1） | 1 / 16 | **0 / 16** |
| `dash`（本轮 v2） | **7 / 16** | **0 / 16** |

**判读惰性时必须小心**：中途只看前 2 条种子会把 c10 误判为"改进"
（其 2 种子均值 `0.43714` 貌似远高于父代 `0.34958`），
但父代在**同两条种子**上的均值是 `(0.8125+0.0618)/2 = 0.43715`，**完全相同**，
且 hits 也同为 `3/9`。**所以 c10 是惰性的，不是改进。**
比较候选必须先确认种子集合与父代一致（配对评分的设计初衷），
否则会把自己的噪声当成发现。

**结论一：缩小头集合这条杠杆无效。** 冲刺头带来的惰性候选**更多**（7/16 对 1/16），
因为冲刺是二值动作、小扰动很少翻转它。**有实质改变的候选里没有一个优于父代。**

**结论二（更有价值）：冲刺头无法规避这三次受击。**
C7-2 提出的假设是"用圣盾冲刺的无敌帧躲开本体接触"——
**该假设不成立**：把冲刺头单独开放后，没有任何候选能减少受击。
所以修正这三次受击需要**水平/纵向**的改变，
而 v1 已经证明任何那种规模的改变都会毁掉胜利。

**综合 C7c 与 C7-3**：在手写脚本已经会赢的配装上，
**残差 ES 在"全部头"与"冲刺头"两个子空间里都找不到改进**。
这说明该瓶颈不是头集合、不是 σ、不是维度，
而是**残差机制本身的表达能力**：它只能做"逐 tick 偏离脚本"，
而这里需要的修正（气泡线穿越、本体间距、鲨鱼龙规避）是**几何/相位级**的，
必须落到脚本里。

### C7-4. 三次受击的几何还原与"脚本看不见什么"

从 `boss-observations.jsonl`（257 行，按状态转换采样）取最近可观测行：

| 受击 | 玩家 | 威胁 | 几何判读 |
|---|---|---|---|
| 682 气泡 | (653, 7000) vel(−2.5,−3.5) | 爆裂气泡 (649.6, **7046.5**) vel(−5.7,**−13.4**) | 玩家在 Boss 上方 279 px，气泡从下方**上升**撞上；脚本在气泡阶段是**下潜到地面** |
| 1643 本体 | (6304, 6467) **`eocDash=15`、`immuneTime=40`** | Boss (6226, 6485) vel(3.4,**−16.6**) | 水平仅隔 **78 px**，远小于 `StandoffPixels=720`；且**脚本自己的冲刺正在生效** |
| 3752 鲨鱼龙 | (4568, 6678) vel(**−1.0, 0.0**) | 鲨鱼龙 (4484, 6671) vel(**−15.5**, 10) | 玩家**几乎静止**被穿过；巡逻分支不针对鲨鱼龙 |

#### 关键限制一：气泡处理规则**已经试过两次并实测更差**

`FishronWingScript.cs:148-154` 的注释原文记录了这件事：

> 气泡快照被发布但**故意不据此行动**。两条候选规则都已实现并实测，
> 结果都在种子间波动之内或更差：**垂直逃逸（7–9 次受击）**与
> **朝较近边缘的引导布位（8–10 次）**，对照是**不加任何规则时的 7–9 次**。

**所以"改进气泡处理"这条路已经被走过并否定了**，重做没有意义。

#### 关键限制二：脚本对"鲨鱼龙"与"爆裂气泡"没有逐实体感知

脚本能看到的威胁通道只有三个：

1. `TargetSnapshot boss`——**只有 Boss 本体**；
2. `_tornadoX`——**一个记忆的 X 坐标**（`FishronWingScript.cs:178-185`
   明写"one remembered X is the whole record the circuit keeps，
   not a scan of live projectiles"）；
3. `SharknadoBubbleSnapshot`（`Models.cs:374`）——**单个**实体，
   而且按 `:370-373` 的文档注释指的是**生成鲨卷风的那个气泡**，
   **不是**在 tick 682 命中玩家的爆裂气泡（NPC 371）。

**结论：脚本拿不到任何鲨鱼龙或爆裂气泡的位置。**
第 3 次受击（站着不动被鲨鱼龙穿过）**在现有输入下无法被脚本察觉**。

#### 因此下一步有两条路，代价差别很大

- **便宜且不新增通道**：鲨鱼龙只在 Boss 生命低于一定比例后出现，
  而 Boss 生命/阶段**已经**在输入里。规则可以是
  "该阶段内不允许在地面静止"——直接针对第 3 次受击，且无需改接口。
- **昂贵但更彻底**：给 `FormulaScriptInput` 增加一个**邻近威胁列表**通道
  （仿照 `SharknadoBubbleSnapshot` 的形状但改为列表），
  由 facade 填充。这是本轮诊断出的**真正结构性缺口**，
  且对猪鲨全部 5 套配装都有效。

**注意第 2 次受击的证据含义**：命中时 `eocDash=15`（脚本自己的冲刺正在生效）
且 `immuneTime=40`，却仍然受到 67 点伤害。
**这说明圣盾冲刺的无敌帧并没有保住这次接触**，
与 C7-3 中"冲刺头无法规避这三次受击"的实测**互相印证**。

#### 第 3 次受击的轨迹（把范围收窄到一个具体分支）

```
tick 3300  p=(6174,6167) v=( 6.8, 2.8) wing= 81  bossLife=21848  gapY= 50
tick 3420  p=(5794,6678) v=(-5.8, 0.0) wing=130  bossLife=20711  gapY=542
tick 3480  p=(5439,6678) v=(-5.9, 0.0) wing=130  bossLife=20576  gapY=503
tick 3600  p=(4497,6378) v=(-4.8,-9.2) wing=104  bossLife=17967  gapY=-218
tick 3700  p=(4554,6621) v=( 3.7, 9.4) wing=100  bossLife=15841  gapY= 926
tick 3715  p=(4572,6678) v=(-0.4, 0.0) wing=130  bossLife=15548  gapY= 870  <- 落地
tick 3720  p=(4568,6678) v=(-1.0, 0.0) wing=130  bossLife=15466  gapY= 841
tick 3752  <- 被鲨鱼龙命中
```

玩家**落到 `y=6678` 的地面后几乎停住**，`wingTime` 回满 130（= 已接触地面，
按 `:25` 的注释"circuit is ground-anchored"，触地回满翼力是**设计意图**）。
此时 Boss 在其**下方 870 px**，血量 **15466/78000 ≈ 20%**——即 **P3 狂暴期、鲨鱼龙活跃**。

**"停住"不是地形限制**：同一地面高度在 tick 3420–3480 时的速度是
**−5.8/−5.9 px/tick**（正常地面速度）。

**未解分歧点（需要新证据，不可推算）**：`_patrol` 应给出 ±1 的输入、
在地面约合 5.9 px/tick，而 tick 3720 实测只有 `−1.0`。
所以要么 `_patrol` 在该 tick 为 0，要么玩家被挡住。**两者指向完全不同的修法。**
**分辨它们需要脚本当时输出的 `phase`，而观测行的 `plan` 字段是 `null`。**

#### 分歧点已解决——而且探针**不需要改**（又一次自我更正）

**我先前的两句话都是错的**："`plan` 字段每行都是 `null`"与"`game-probe.log` 里没有 phase"。
**真相：257 行里有 253 行的 `plan` 是有值的**，且包含
`phase / strategy / formulaRoute / horizontal / jump / jumpAction / drop / dash /
fire / hook / target / tacticalMode`（`GameProbe.cs:614-639`）。

**我犯错的经过**：我只打印了**第一行（tick 14）**就断定"每行都是 null"。
tick 14 在接管（tick 120+）之前，那时确实还没有 plan。
**这是同一类错误的第三次**（前两次：从源码赋值点反推 `dashType`；
把 2 种子的子集均值当作候选改进）。
**教训：`head -1` 不是样本。任何"全部/每行/都没有"的断言都必须先统计整个文件。**

读到的实际 phase（`boss-observations.jsonl`）：

| 受击 | 脚本 phase | 脚本指令 | 判读 |
|---|---|---|---|
| 682 气泡 | `fishron-wing-bubble-line` | `horizontal=-1, drop=True, jump=False` | 按设计下潜——而这条路已试过并否定 |
| 1643 本体 | `fishron-wing-charge-ascend-dash` | `horizontal=-1, **jump=True, dash=True**` | **脚本自己的"上冲冲刺"节拍**把玩家带到距本体 78 px |
| 3752 鲨鱼龙 | `fishron-wing-tornado-clear` | `horizontal=-1`（**全速左移**） | **脚本在指挥移动，玩家却没有移动 → 被挡住** |

**结论一：脚本在第 3 次受击时并非静止，而是全速指挥 `horizontal=-1`。**
所以"让脚本别站着不动"这类改法会是**空操作**——
按用户那条判定标准（改动前后 `ticks/hits/damage` 相同即分支未被执行），
这种改动注定无效。**这次实测避免了一次无用的脚本改动。**

**结论二：三次受击中只有第 2 次是"脚本可控制的几何失误"。**

- 第 1 次：气泡规则已试过并否定，**死路**；
- 第 3 次：玩家被地形/液体挡住，**不是脚本可控的**（需要 tile 层面的证据才能定性）；
- 第 2 次：脚本的 `charge-ascend-dash` 节拍把玩家送进本体 78 px 内——
  这是唯一由脚本几何决定、且尚无实测否定记录的一次。

**因此下一个（也是唯一有依据的）脚本改动目标**是 `charge-ascend-dash` 节拍的间距，
而不是气泡、也不是"静止"。改动前必须先想清楚：
`FishronWingScript.cs:188-194` 记录了这个节拍是**每次冲锋只选一次**
（"a beat that changes mid-charge walks the player straight through the Boss body"），
所以可动的自由度是**节拍的选择**，不是逐 tick 调整。

### C7-5. 第三次脚本规则实测：**更差，已回退**

`ChargeEscape`（`:386-389`）的逃逸方向规则是：

```
away = 附近有记忆的鲨卷风列 ? 远离鲨卷风 : 远离 Boss
```

`:383-385` 的注释明写这是**有意的取舍**："鲨卷风列优先于 Boss——
列不会移动，所以一次把玩家带进列里的冲锋比带向 Boss 更糟。"
**tick 1643 正是这个取舍的实测代价**：Boss 在玩家左侧（6226 < 6304），
而 `away=-1`（远离右侧的鲨卷风列）**正好指向 Boss**，
随后 `dash=true` 以 14.5 px/tick 冲过去，最终距本体 78 px。

**改动**：当逃逸方向指向本体时（`escapeIntoBoss`）**不发起冲刺**。
理由是走路 5.9 px/tick、冲刺 14.5 px/tick，差距正是"穿过本体"与"保持间距"之别。
构建 `2EB3C98AFAC0DE70`，测试 748/0。

**实测（同 6 条种子，基线 `fw-base` → 改动 `fw-esc1`）**：

| 种子 | 基线 ticks/hits/damage | 改动后 | 判定 |
|---|---|---|---|
| 1 | 4932 / 3 / 78000 | **4853** / 3 / 78000 | 分支**被执行** |
| 2 | 4030 / 9 / 59781 | 4030 / 9 / 59781 | 相同 |
| 3 | 3640 / 9 / 57322 | **2933 / 10 / 44079** | **更差** |
| 4 | 4605 / 8 / 78000 | 4605 / 8 / 78000 | 相同 |
| 5 | 4453 / 9 / 68037 | **4451** / 9 / 68267 | 略好 |
| 6 | 4755 / 7 / 78000 | 4755 / 7 / 78000 | 相同 |

胜场仍是 3/6（种子 1、4、6）。种子 3 的适应度从 `0.0461` 掉到 **`−0.0746`**，
净变化 **≈ −0.0199**。

**结论：改动确实被执行（不是空操作，符合用户那条判定标准的要求），但结果更差，已回退。**

**这构成了一个重要的模式**：至今已有**三条**针对该脚本的单规则改动被实现并实测，
**全部不优于原状**——两条气泡规则（`:148-154` 记录）与本条。
配合 C7c/C7-3（残差 ES 在两个子空间都找不到改进），
可以合理推断：**这个手写脚本已经处在一个对"单规则微调"稳定的局部最优上**。
**因此"改进脚本"这条路的期望值也比它表面看起来低**，
任何新规则都必须按本节的流程实测，且有相当概率被否。
下一轮若要继续，应先从**测量**找出一个尚未被否定过的具体失败模式，
而不是从代码里找"看起来不对的地方"。

#### 附带发现：测试套件存在一种状态敏感的偶发失败

回退后重建，Core 哈希**逐字节回到 `7FFDBD04EB6DB848`**，
但**紧接着的测试运行报 747 通过 / 1 失败**；**不改任何东西重跑即回到 748 / 0**。

哈希相同意味着产物完全一致，所以**这不可能是一次代码回归**。
最可能的原因是**某个用例读到了刚跑完的探针波次留下的 `artifacts/` 状态或未沉降的进程**。

**记录此事的用途**：以后在探针波次之后立刻看到 747/1 时，
**应先在无改动的情况下重跑一次**再判断，避免把偶发失败误当成回归而回退正确的改动。
（本次未记录到具体的失败用例名——只捕获了汇总行。）

### C7-6. 配装 2 全部受击的分类：失败是**弥散的**，没有主导模式

对 6 条种子的 `hurt-observations.jsonl` 全部 41 次 NPC 受击，
按"来源 NPC 类型 × 命中时的脚本 phase"归类（phase 取该 tick 最近的观测行）。

**逐种子受击数**：s1=**3**、s2=8、s3=8、s4=8、s5=7、s6=7（s1 是最好的一条）。

| 类型 × phase | 次数 | 占比 |
|---|---|---|
| **371 爆裂气泡 × bubble-line** | **11** | **27%** ← **已被试过并否定**（`:148-154`） |
| 370 本体 × charge-horizontal | 5 | 12% |
| 371 气泡 × standoff | 4 | 10% |
| 370 本体 × tornado-clear | 3 | 7% |
| 370 本体 × charge-ascend | 3 | 7% |
| 370 本体 × personal-space | 3 | 7% |
| 其余 10 个格子 | 各 1–2 | 各 ≤5% |

**按威胁类型**：气泡 **17 (41%)**、本体 **15 (37%)**、鲨鱼龙 **6 (15%)**。
**按 phase 归并**：`charge-*` 共 13 (32%)、`bubble-line` 12 (29%)、
`standoff` 5、`tornado-clear` 5、`personal-space` 4。

#### 结论（这是本节的要点）：**41 次受击散布在 16 个格子里**

- 最大的单格是 `371|bubble-line`（27%），**而它正是唯一已被实测否定的那条路**；
- 最大的**未试过**的单格是 `370|charge-horizontal`（12%，5 次）；
- 即使把整个 `charge-*` 家族（13 次，32%）**全部消除**，仍会剩下 28 次受击。

**无伤要求把 41 次全部消除到 0。** 在一个最大单格仅占 27%、
且该格已被否定的分布上，**单规则改动不可能达到零**——
这与"三条规则全部被否"（C7-5）的记录完全一致，也解释了残差 ES 为何失败：
**不存在一个主导错误可供修正。**

**方法上的收获**：上一轮我说"从测量找出主导失败模式"，本轮照做后发现
**主导模式并不存在**（分布弥散）。这本身就是决定性信息，
它把"再试几条规则"这条路的期望值降到接近零。

**同时注意一个统计陷阱**：初次统计时目录 glob 匹配到 **7** 个而种子只有 **6** 条
（某条种子有两次运行），导致 `s1@682` 等被**重复计数**、总数虚高为 44。
**按种子去重（取最新目录）后为 41。任何"按种子聚合"的统计都必须先去重**，
否则会把重复运行当成额外数据。

### 结论一：三套配装现在就能训练，且目标不是"取胜"而是"消除受击"

配装 2/3/5 的脚本已经能打出胜利，所以对它们而言目标是**把受击数降到 0**。
这与光女扫帚（三代训练仍 0 胜、存活时间不足半场）是完全不同的难度结构。

### 结论二：`-WinHitBudget` 默认 25 使胜利区间的信号被压缩到 0.06

`Get-Fitness`（`tools/train-policy.ps1:230-233`）对胜利给
`0.5 + 0.5×(1 − hits/WinHitBudget)`。取默认 25 时：

- 3 次受击获胜 = **0.94**，7 次受击获胜 = **0.86**，无伤获胜 = **1.0**；
- 而任何失败 ≤ 0.5。

**胜利区间内只有 0.06 的跨度，失败/胜利之间却有 0.44。**搜索几乎没有梯度
去区分"受击 3 次"和"受击 0 次"，而 0 次正是验收标准。

把 `-WinHitBudget` 降到 **3** 可得：3 次受击 = 0.5、2 次 = 0.667、1 次 = 0.833、
0 次 = 1.0。**且"任何胜利 ≥ 任何失败"这一设计性质对任意 WinHitBudget 都成立**
（满伤害失败 = `0.5 − penalty < 0.5`），所以调小它不破坏原有的序关系。
这是**纯参数改动，不需要改代码**。

配套要点：对已能获胜的配装应**不使用 `-StopOnHit`**。`-StopOnHit` 在首次受击
即结束，`win` 永远为假，于是永远走失败分支、`hits` 恒为 1，
观察不到"获胜且零受击"这一目标本身。

### 结论三：配装 3 用更好的翅膀反而更差，且原因不是实现差异

`GameProbe.cs:1708`/`:1765` 显示 strong-wing 固化装置用的正是
**`ItemID.FishronWings`（猪鲨翅膀）**，所以**场景是对的**。

而 `CombatPlanner.cs:139` 显示 strong-wing 用的是**同一个 `FishronWingScript` 实例化**
（不存在 `FishronStrongWingScript` 类），且该类除 `FishronWingScript.cs:157-158`
的守卫外**没有其他按路由的分支**。

**固化装置也已逐行比对**（`GameProbe.cs:1707-1714`）：两个翅膀路由设置的是
完全相同的配件——`armor[6]=FrogLeg`、`armor[7]=EoCShield`、
`armor[8]=RangerEmblem`（`difficultyCode>0` 时），**唯一差别就是 `armor[4]` 的翅膀**。

所以**唯一差别就是翅膀物品**，而结果反转：

> **在脚本逻辑与配件完全相同的前提下，猪鲨翅膀（3/6→0/6）比仙灵翅膀更差。**

这是引擎内实测，不是推断。

**已否定的假设（更正）**：我曾推测脚本的时序常量是围绕仙灵翅膀的飞行时间调的。
**这一假设不成立。** `FishronWingScript` 的悬停时长常量
（`FishronWingScript.cs:113` `_hoverLimit = {30,30,80,90,180,...}`）是
**`AI_069`（猪鲨本体）的悬停时长**，且脚本会**从观测中学习**它们
（`:196-200` `_hoverLimit[_previousState] = _previousTimer + 1`）。
它们与玩家翅膀无关，所以"时序绑定了仙灵翅膀"没有代码依据。

**仍未解释**，需要逐帧测量（不可推算）。候选方向：猪鲨翅膀更高的上升/水平速度
可能让脚本的固定间距（`StandoffPixels`、`PersonalSpace` 等）出现超调。
按本项目规矩，这只是待测方向，不是结论。

### 结论四：猪鲨固化装置的冲刺状态**已核实为开放**（一次自我更正）

排查过程中我曾据 `GameProbe.cs` 推断：`player.dashType` 只在第 1739 行的
`post-plantera` 分支设置，而猪鲨固化装置只给了 `armor[7]=ItemID.EoCShield` 物品、
没设 `dashType`，加上第 2597 行有 `player.dashType != 2` 的守卫，
所以怀疑配装 2/3 是"带圣盾物品但冲刺不可用"在训练。

**该推断被已有探针产物直接否定。** `boss-observations.jsonl` 首行含
`"dashType":2`（`game-probe-rt-fw-base-1-*` 与 `-2-*` 均为 2），
且同目录存在 `shield-events.jsonl`（13.5 KB）在记录冲刺事件。

**结论：冲刺状态是开放的**，由游戏自身的装备更新（`UpdateEquips` 路径）设置，
不依赖探针显式赋值；`post-plantera` 分支的显式 `dashType=2` 与注释只对那条路径成立。
**配装 2/3 的基线（3/6 胜）是在冲刺可用的前提下取得的。**

**方法记录**：这条更正完全依靠**既有产物**完成，无需重构、无需新探针。
排查"某能力是否真的生效"时，应先查 `boss-observations.jsonl` 里已记录的
`dashType`／`eocDash` 等字段，而不是从源码的赋值点反推——
源码里看不到游戏自身装备更新的效果。**差一点就把一个未经验证的缺陷写进文档。**

### C7a. 猪鲨满血是 78000，不是训练器的默认 98000

`tools/train-policy.ps1:21` 的 `-BossMaxLife` 默认 **98000**（光女的值）。
猪鲨在本 fixture/difficulty 下满血 **78000**，由探针数据直接得出：
seed 2 失败时 `damage=59781` 且 `bossLife=18219`，两者之和恰为 78000。

`Get-Fitness` 的失败分支用 `$row.damage / $BossMaxLife`，**若沿用默认值会把
每一场失败按 78000/98000 缩小，静默地给所有失败局排错序**。
训练猪鲨时必须显式传 `-BossMaxLife 78000`。

**验证方式（可复算）**：配装 2 的父代（零权重＝固定脚本）在
`-BossMaxLife 78000 -WinHitBudget 8` 下测得 **0.34958**。
逐种子手算为 3 次受击胜 0.8125、8 次胜 0.5、7 次胜 0.5625，三个失败局
分别为 0.0618 / 0.0461 / 0.1147，均值 **0.3496** —— 与实测吻合到四位小数。
这一次性证明了 `-BossMaxLife` 与 `-WinHitBudget` 都确实生效。

### C7b. 配装 2 的训练起点（`fishron-fairy-wing-nohit-v1`）

父代 `fitness=0.34958, wins=3/6, noHit=0, meanTicks=4402, head=all, trainable=206`。
**注意 `trainable=206/206`**：与光女那轮（`horizontal` → 171/206）不同，
本轮全部头都可训练，因为该配装在空中作战，只允许一个轴修正无法修好纵向闪避误差。

**启动器脚本位于 `artifacts/` 下，而 `artifacts/` 是 gitignored，不会被提交。**
因此参数理由必须写进受管理的文档（即本节），否则会随会话丢失。

### C7c. 配装 2 第 1 代：一个清晰且重要的负面结果

16 个候选全部测完（6 种子/候选），按镜像对尺度列出**原始**适应度
（父代 = **0.34958**）：

| 候选 | 尺度 | 适应度 | 胜场 |
|---|---|---|---|
| c1 / c2 | 0.111 | 0.34958 / 0.35048 | 3 / 3 |
| c3 / c4 | 0.140 | −0.127 / −0.052 | 0 / 0 |
| c5 / c6 | 0.177 | −0.064 / −0.158 | 0 / 0 |
| c7 / c8 | 0.223 | −0.152 / −0.071 | 0 / 0 |
| c9 / c10 | 0.281 | −0.063 / −0.087 | 0 / 0 |
| c11 / c12 | 0.354 | 0.025 / 0.014 | 0 / 0 |
| c13 / c14 | 0.446 | −0.147 / −0.029 | 0 / 0 |
| c15 / c16 | 0.563 | −0.151 / −0.041 | 0 / 0 |

**结论一：手写脚本在这个残差空间里是一个"刀锋型"最优。**

**任何尺度 ≥ 0.14 的扰动都让 3 场胜利全部消失。** 这不是渐变的性能下降，
而是胜负的**阶跃**：偏差稍大就一场都赢不了。

**结论二：8 个随机方向里有 7 个会毁掉胜利；唯一无害的是最小尺度那一对。**

最小尺度 0.111 的**整对**落在 margin 死区里——c1 的数值
（seed1 `win/4932/3/78000`、seed2 `4030/9/59781`、seed3 `3640/9/57322`）
与父代基线**逐位相同**，说明它一个动作都没改。c2（镜像的另一半）略超出边界，
得 0.35048，仅比父代高 **+0.0009**，可以忽略。

**必须标注的方法缺陷（自我更正）**：我起初把上表读成"可用扰动窗口 = [0.111, 0.14]"。
**这个推断被方向混淆了，不成立。** 8 个尺度各自对应**不同的随机方向**，
所以"pair 1 在尺度 0.14 被破坏"**无法区分**是尺度太大还是那个方向本身就坏。
要干净地分离尺度与方向，必须**用同一方向、多个尺度**各测一遍，
而当前数据是 8 方向 × 8 尺度、每格只有一对。
因此可以下的结论只有下面这条更弱的：

> **在 8 个随机方向中，只有尺度最小的那一对没有毁掉胜利；
> 其余 7 个方向（尺度 0.14–0.56）全部把 3 场胜利清零。**

这已经足以说明该残差空间的搜索极其困难，但**不足以**给出一个可用的尺度区间，
也**不足以**据此断言"把 σ 调到 0.12 就能找到东西"。

**这不是配置错误**，而是该配装的客观性质。
`σ` 的自适应会逐代收缩（0.25 → 0.2125 → …），使更细的对子逐步进入窗口，
但每一代约 20 分钟，收敛会很慢。

**结构性含义**：由于 logits 依赖特征、而扰动是**在所有 tick 上均匀施加**的，
随机扰动无法表达"只在特定相位偏离脚本"。要在这个配装上取得进展，
需要**结构性**手段而非继续随机搜索，方向有二：

1. 把残差**按相位/时间门控**（早前分析里的第 2 号杠杆）；
2. 直接改进手写脚本（早前分析里的第 3 号杠杆）——它目前 3/6 胜、3–9 次受击。

### C7d. 吞吐实测（无异常，可复算）

6 个候选并行、每个候选的 6 条种子**串行**（`wave-*.log` 时间戳显示同一种子流
间隔约 45 秒）。因此一批 6 个候选约 4.5–6 分钟，16 个候选（3 批）约 **15–20 分钟/代**，
折合 **5–6.5 次探针/分钟**，与既有记录一致。

**日志行的写出时机**：候选日志行**不是**随候选完成即写，而是等**全部**候选结束后
统一写出。因此中途看 `log.csv` 会一直只有 `1,parent` 一行——
要中途评估必须直接读 `result.json`（本节的表就是这样得到的）。