# 重点 Boss 战中 F8 接管审计（2026-09-12）

## 范围与结论

本审计只静态阅读当前工作树的生产路径和离线夹具；没有启动
Terraria、没有读取或修改存档、没有执行发布，也没有改动生产源码。
本文中的“已覆盖”仅指源码/离线夹具存在，不是实机胜率、持续物理收敛
或“近乎必胜”的证据。

结论是：当前实现有一个清晰、保守的“从已开战状态加入并尝试回到闭环”
路径，但它**不是无条件的任意时刻接管保证**。合法快照才进入
`RecoverToPattern`；任何无法证明的原生状态、输出路线、场地/机动能力或
关键上下文都会中性地归还控制权。这比猜测阶段安全，但与“按 F8 后任何
当前状态都立即接管”的产品表述不同。

最具体的生产漏洞是骷髅王手臂的捕获半径：`TerrariaFacade` 的
`IsPriorityBossNativeNpc` 没有包含 type `35`/`36`。头部因 `NPC.boss`
仍会越过普通目标半径被收集，但非 Boss 标记的 type `36` 手臂只有在
`MaximumTargetDistancePixels`（默认 2800 px）内才进入快照。一个远处、仍在
执行俯冲的手臂会从策略输入中消失；控制器可能仍进入恢复，但已不是针对
真实当前攻击的闭环。

另一个需要优先处理的生产缺口在肉山：策略为了正确处理“同向、距离正在
扩大”的跑道，设置了 `RecoveryIgnoreHorizontalGeometry`。这会让
`ActivePatternRecovery.Measure` 完全不测量玩家位于 mouth 前方还是后方；只要
Y 车道、同向速度和场地刹车余量满足，已经被肉山越过的玩家也可能在三帧后被
误认作闭合跑道。现有 `WallLowLifeTakeoverDoesNotCloseWhileBeingCaught` 只覆盖
“仍在墙前、速度不足但可追上”的恢复，未覆盖“墙已越过玩家”。

## 当前 F8 路径及其边界

默认激活键确为 F8（`ChaiteConfig.ActivateKey`）。在已发现 Boss 时，
`Runtime.Tick` 不会消耗召唤物，而是：

```text
F8
  -> BuildObservation: 已有 Boss
  -> BuildCombatSnapshot
  -> CombatPlanner.PrepareForActiveEncounterDetailed
  -> 锁定输出/机动路线，并设置 _activePatternRecovery
  -> 下一帧 Plan: 当前原生 AI 状态 -> RecoverToPattern
  -> 连续 3 帧证明已闭合 -> EstablishPattern/StablePattern
```

`ActivePatternRecovery` 还具有无进展上限和 90--600 tick 的绝对恢复期限；
阶段标签变化不会无限延长期限。失败时
`UnsupportedPatternRecoveryPlan` 会清空移动、开火、药水、冲刺、钩爪等计划
并请求归还控制权。这部分的离线回归位于
`PriorityBossActiveRecoveryTests`。

以下情况在进入 Boss 策略前或策略首帧就不会进入 `RecoverToPattern`：

1. 输出、场地或锁定机动路线未通过准入；
2. 正在骑坐骑或抓钩：当前实现先走 native-release handoff，且任何仍为
   `MountActive`/`Grappling` 的 `Plan` 都会归还控制权；
3. 当前 Boss 原生 AI/计时器/身份/上下文无法通过对应的严格验证；
4. Boss 正在其原生离场/消失状态。

因此，“战斗中任意时刻”应至少限定为：本地玩家、合理场地和已支持输出/
机动路线、无正在持有的原生移动控制权、且当前帧可取得经验证的原生状态。
如果产品要把已骑乘的合格飞行坐骑也作为接管下限，现有的“先下坐骑”
架构本身需要另行扩展，不能只放宽一个检查。

## 按 Boss 审计

### 巨鹿（Deerclops，type 668 / AI_123）

**当前恢复路径。** `TerrariaFacade.ReadPriorityBossNpcContext` 无距离上限地
捕获 AI、localAI、原生方向和 `timeLeft`。`DeerclopsStrategy` 根据 AI state
`-1, 0, 1, 2, 3, 4, 5, 6, 7` 分别处理出生稳定、贴近、前方尖刺、碎石、
减速咆哮、双尖刺、暗影手、回家和回家传送；状态 `1/2/4` 使用带时钟的跳跃
闭环，状态 `3` 还具有 Buff 32（Slow）的专门机动合同。state `8` 是原生消失
并有意归还控制权。

**已有离线证据。** `PriorityBossActiveRecoveryTests` 对 `-1..7` 都有首帧
`PrepareForActiveEncounter`/`RecoverToPattern` 夹具；
`PriorityGroundFishronTrajectoryTests` 覆盖 Slow、尖刺的基础跳/云跳时序和
若干状态时钟。

**缺口。** 生产代码对 state `-1`、`0`、`6` 允许无上限的非负计时器；这可能
是正确的原生行为，但当前测试没有把“确实无上限”与“读取了陈旧状态”分开。
其余边界也没有完整的 F8 级回归：state `1` 的 `80/81`，state `2/3/5/7` 的
`60/61`，state `4` 的 `90/91`，以及 state `8` 的离场边界。现有夹具主要是
合成快照，未把 `TerrariaFacade` 的真实捕获、连续物理位置和策略切换串起来。

**最低风险建议。** 先只增加表驱动的离线边界测试：每个合法端点必须首帧
进入 `RecoverToPattern`，端点外必须产生完全中性的归还；随后对三个无上限
state 做一次版本锁定的 AI_123 源码/IL 审核。不要为“更容易接管”放宽未知
计时器。

### 骷髅王（Skeletron，头 type 35、手 type 36）

**当前恢复路径。** `SkeletronStrategy` 读取头部 `ai[1]` state `0/1` 和
`ai[2]` 时钟，读取每只手 `ai[0..3]` 的侧别、父头、攻击 state `0..5` 和
局部计时器。悬停/转头走稳定跑道；已提交的垂直或水平手臂俯冲改走垂直逃逸
线并拥有短时 movement closure。`redHat == 1`、白天守卫 state `2`、头的
state `3` 离场，以及非法手臂身份均明确归还控制权。

**已有离线证据。** `PriorityBossActiveRecoveryTests` 有头部悬停/转头和每个
手 state 的首帧恢复夹具；`PriorityGroundFishronTrajectoryTests` 在各难度、
两侧开局和同时两只已提交手臂下验证策略选择。

**生产缺口（优先级最高）。** 如开头所述，type `36` 不在
`IsPriorityBossNativeNpc` 中。离玩家超过默认目标半径的活手不被读取，离线
夹具手工把手臂塞入 `Targets`，因此没有发现这个问题。还有两个刻意不支持的
原生分支（red-hat、白天 guardian）不会恢复；它们应在界面/文档中被称为
“明确不接管”，而不能宣传为任意阶段。

**测试缺口。** 未完整覆盖头部时钟 `0/800/801`、`0/400/401`，手的
`ai[3]` `0/299/300`，以及“手臂目标不是本地玩家”的多人语义。

**最低风险建议。** 先将 `35` 和 `36` 纳入无距离上限的重点原生捕获（或做
等价的、已验证父头关联捕获），并新增一条 facade 级回归：手臂距离大于
`MaximumTargetDistancePixels` 时仍可被捕获、父头/侧别/AI 不变。之后才考虑
是否为 red-hat/guardian 单独建策略；不要把它们伪装为普通 Skeletron。

### 蜂后（Queen Bee，type 222 / AI_043）

**当前恢复路径。** 重点捕获包含蜂后本体；额外上下文从世界表面、丛林状态、
FTW 和本地目标重新得到三项原生暴走因子。策略覆盖 state `-1` 选招、`0`
冲刺的对齐/提交/刹车、`1` 蜂群、`2` 上移、`3` 毒刺、`4` 远距离重新获取；
state `5` 是离场。已提交冲刺使用垂直逃逸线，其他状态以跑道/弹幕车道收敛。

**已有离线证据。** `QueenBeeReachableTuplesSupportMidCycleTakeover` 对四种
难度/世界设置、两侧开局、可达冲刺序列、蜂群、毒刺和非法 tuple 都有策略级
覆盖；`PriorityBossActiveRecoveryTests` 对八个非离场类别有首帧恢复夹具。

**缺口。** 当 `npc.target` 不是本地玩家、丛林状态未知或暴走上下文不一致时，
生产策略会正确归还控制权而非恢复；这在多人/切换目标时是实质边界。所有可达
tuple 的全面测试目前多数只调用 `BossStrategyEngine.Evaluate`，不是完整的
`PrepareForActiveEncounter -> Plan` 收敛测试。蜂群阈值、毒刺周期和
`0/1/2/2.5` 暴走因子的端点也没有逐一经过该完整路径。

**最低风险建议。** 把已有可达 tuple 表复用为 active-admission 表：要求每个
合法 tuple 的首帧为 `RecoverToPattern`，每个非法 tuple 中性归还；再增加
“本地目标切走/切回”的 fail-closed 回归。不要在没有多人策略前删除
`npc.target == localPlayer` 约束。

### 肉山（Wall of Flesh，mouth 113 / eyes 114）

**当前恢复路径。** 策略不猜普通环绕，而读取 mouth 的原生生命速度阶梯、
`ai[1]/ai[2]` 吸血者时钟、两只眼各自的 `localAI[1]/[2]` 激光时钟、LOS 和
`Main.wofDrawAreaTop/Bottom`。它在观察到的 tunnel Y 车道内追上原生水平速度；
恢复度量故意忽略与 mouth 的固定 X 距离，避免把正确的同向跑道判成未收敛。

**已有离线证据。** 首帧恢复覆盖普通、加速、低血、临界和眼激光；另有 tunnel
与双眼独立上下文测试，及“阶段标签改变不得无限延长恢复期限”的测试。

**缺口。** 这不是一个单一整数 state 机，缺的是完整的边界矩阵：生命阈值
`.75/.66/.5/.33/.25/.1/.05/.035/.025`、mouth burst `0/1/2/3/4`、相应时钟
`0/60`、eye charge `600/601`、burst cadence `45/46` 和 LOS 切换，没有都走过
F8 的完整恢复路径。更重要的是，在 `wofDrawArea` 尚未初始化、任一眼 LOS
未能证明、或任一眼的原生时钟没有被捕获时，策略立即归还控制权；这对刚出现
或读帧预算不足的瞬间不满足“任意时刻”。

**生产缺口。** `RecoveryIgnoreHorizontalGeometry` 合理地取消了固定 X 距离，
但也取消了唯一必须保留的 X 不变量：玩家必须仍在肉山行进方向的前方。因而
玩家在 wall 后方、但速度恰好同向且 Y 合法时，可能在三帧内错误结束 active
recovery。

**最低风险建议。** 在 `WallStrategy`（而非泛化恢复量表）增加一个仅肉山使用
的前向车道合同：要求
`nativeDirection * (playerCenter.X - mouthCenter.X)` 大于双方半宽加上保守刹车
余量；不满足时中性归还控制权。为它增加“墙前、正在被追上仍可恢复”与“墙后、
同速不得闭合”的 active-recovery 回归。然后再添加上述时钟/LOS 表驱动夹具及
“所有活眼均已捕获”的 facade 级测试。若要减少 `wofDrawArea` 初始化的一帧拒绝，
只可增加很短、零输入、重新读取同一 mouth identity 的有界等待；不可复用旧
tunnel/LOS 数据。

### 猪鲨公爵（Duke Fishron，type 370 / AI_069）

**当前恢复路径。** 重点上下文重算三项原生暴走谓词；策略严格解析 state
`-1..12`、`ai[2]` 时钟、`ai[3]` 序列和难度。它覆盖出生、P1/P2 冲刺/泡泡/
龙卷、Expert/Master P3 的转场/重定位/冲刺/传送；冲刺走垂直逃逸，龙卷保留
车道。Classic 的 P3 和任何不可能 tuple 都归还控制权。

**已有离线证据。** `FishronAllReachableSequencesSupportMidCycleTakeover` 和
`FishronNativeTimerBoundariesFollowCurrentVersion` 对策略的序列、难度、暴走和
众多时钟端点覆盖较强；`PriorityBossActiveRecoveryTests` 对 17 个代表性阶段
执行首帧 active recovery。

**缺口。** 全部可达 tuple 的大部分仍仅经过策略级断言，不是完整
`PrepareForActiveEncounter -> Plan`；P3 传送只以一个中间 tick/理想场地取样，
没有覆盖传送提交前后（例如 `14/15/29/30`）及跑道两端的回归。暴走谓词或
本地目标身份无法证明时会归还控制权，这是正确的 fail-closed 行为，但也意味
不能宣称任意帧接管。Expert/Master 还要求已认证的 burst/brake 路线。

**最低风险建议。** 先把已有 tuple/时钟表参数化到 active recovery，并为 P3
传送加入两侧、临界 tick、已偏离跑道位置的有界收敛测试；在没有新物理证据前
不要放宽 P3 的 dash/mobility 准入。

### 光之女皇（Empress of Light，type 636 / AI_120；昼/夜）

**当前恢复路径。** facade 用只读方式重建 `ShouldEmpressBeEnraged`（含 Remix
语义），并把同帧 AI `0..3` 复制到独立上下文。策略覆盖 P1/P2 的合法攻击表：
`0,1,2,4,5,6,7,8/9,10,11,12`；昼夜/真正暴走和 phase latch 从 AI/context 而
非仅生命值判断。冲刺与昼战走显式闭环。state `3` 是版本锁定的未用分支，
state `13` 是离场，二者明确归还控制权。

**已有离线证据。** `PriorityEmpressMoonStrategyTests` 覆盖所有自然攻击族、
非法 state/table pair、昼夜交替和 P1 双方向冲刺的三个窗口；
`PriorityBossActiveRecoveryTests` 对昼/夜各十个代表性 phase 做首帧 recovery。

**缺口。** 十个 active fixture 没有覆盖 P1 state `0` 初始重定位、P1 state
`4` 长矛，也没有覆盖 P2 的 bolts、lances、rainbow、sun dance、两方向 dash。
这些状态有策略级证据，但没有完整 active-admission/候选收敛证据。昼夜切换、
AI3 `2/3` 的真正暴走、Remix 空间谓词也主要停留在策略/捕获单元测试。

**最低风险建议。** 直接复用 `EmpressAcceptsEveryNaturalAttackFamily` 的 tuple
生成器，令每个案例都经过 `PlanFirstPriorityRecovery`；额外加入昼夜切换的
transition tick `89/90`、两种 dash 方向和一个 Remix case。保持 state `3/13`
的中性归还，直到各自有版本锁定的原生策略。

### 月亮领主（Moon Lord，core 398 / head 396 / hands 397 / true eye 400）

**当前恢复路径。** 所有组件都在重点捕获列表中。策略验证 core state、头/手/
真眼的完整时钟表、父 core key、目标玩家身份，以及 454/456 弹幕的来源身份；
随后按死亡射线、球体、舌头、螺旋弹和 bolt 时钟选择最紧急的逃逸/跑道闭环。
454/456 在普通 hostile 过滤前读取。core 合法但暂时没有 source 时仍可进入
“synchronize-open-eyes”恢复；元数据不完整时则归还控制权。

**已有离线证据。** core 合法 state/clock 的 11 个端点已有完整首帧 active
recovery 测试。`PriorityEmpressMoonStrategyTests` 对头、左右手和真眼每个时钟
段的首尾、非法父/目标/时钟、并发 deathray/sphere 有策略级覆盖。

**缺口。** 完整 planner recovery 目前只覆盖 core 和少量代表 source（head
bolts/tongue/deathray、左右手 sphere）；真眼的所有时钟段、closed/dying
head/hand、454/456 的真实 metadata 分支、实际 455 射线和多源并发，大多仍只
经过策略级断言。任何一个必需 source/core/projectile identity 读不到都会中性
归还，因此合法但未被完全捕获的瞬间不保证接管。

**最低风险建议。** 将现有 `MoonSourceCase` 首/末端点表升级为 active planner
回归，并附加真实 454/456 metadata、两条相反旋转射线及多源并发案例。先证明
每例都进入 `RecoverToPattern`/有界中性归还，再讨论扩大实时容错。

## 离线测试与实机证据的界线

`test-priority-phase-fixture-contract.ps1` 只检查 GameProbe、runner 和 launcher
的阶段名称/tuple 是否同步；它本身不运行 Terraria。当前阶段目录也只列代表性
phase，例如光女每个昼夜十项、月总七项，并非所有上述原生 state 的闭环测试。
GameProbe 的直接阶段注入夹具被明确标记为
`staged-native-phase-regression`，不能计入 readiness 胜率。

`docs/boss-readiness-campaign.md` 的正式门槛是每个重点 Boss/难度/变种至少
40 个预注册独立 seed，并按 Wilson 下界评估；本审计没有看到可以把当前
离线夹具升级为这些胜率结论的实机证据。因此现在只能说“存在严格状态识别和
局部恢复测试”，不能说重点 Boss 已达到 90% 或接近 100% 胜率。

## 建议的修复顺序

1. **先修骷髅王 type 36 的无距离上限捕获，并加 facade 级回归。** 这是会让
   真正当前攻击从输入中消失的明确生产漏洞。
2. **修肉山的前向跑道不变量。** 不能把“墙已越过玩家、同速前进”当作闭环。
3. **明确产品契约。** 若 F8 必须支持已骑坐骑/抓钩，先设计/验证对应的活动
   原生移动路线；否则 UI/文档应写明“安全解除后接管”。
4. **把已有合法 tuple 表升级为 `PrepareForActiveEncounter -> Plan` 回归。**
   优先光女遗漏 phase、月总真眼/弹幕、猪鲨传送、肉山的生命/眼时钟边界。
5. **仅在静态和离线物理回归全绿后，使用隔离的 headless 新世界做实机连续
   轨迹验证。** 阶段注入和首帧候选不是胜率证据；不得据此发布或宣称近乎必胜。

本次审计只新增了本文件。
