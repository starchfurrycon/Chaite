# 战斗中 F8 保留活动坐骑／抓钩的接管审计（2026-09-12）

## 范围与结论

本审计只静态阅读当前工作树；没有启动 Terraria、没有读取或修改正式游戏、用户存档、鼠标或桌面，也没有改动生产代码。

结论分两部分：

| 当前原生移动状态 | 能否在 F8 后“保留” | 能否作为 Boss 下限机动路线 | 当前实现状态 |
| --- | --- | --- | --- |
| 已激活女巫扫帚（item 4444 / mount 23） | **可以作为下一项受限实现的对象**，但只限完整的 type-23 逐 tick 闭环 | 可以；前提是为具体 Boss／难度完成完整扫帚控制、碰撞、威胁、输出和回归证据 | 尚未接线，当前不能宣称支持 |
| 其他原版飞行坐骑（UFO、Cute Fishron 等） | 否；只有身份目录或聚合飞行标志，不存在可安全继承的运动合同 | 否 | 必须保持拒绝 |
| 已附着的普通 Grappling Hook（item 84 / projectile 13） | 仅可作为**短暂、已证明的退出路径**保留到安全脱钩；不能当作持续 Boss 下限路线 | 否；锚点牵引是瞬态且会覆写速度 | 尚未接线，当前不能宣称支持 |
| 其他抓钩、在飞抓钩、多个抓钩、坐骑与抓钩并存 | 否 | 否 | 必须保持拒绝 |
| 玩家自身的已认证有限翅膀／火箭靴飞行 | 这不是活动坐骑问题；已有独立有限飞行路线 | 仅限已认证的具体翅膀／火箭靴合同 | 不应借此放宽坐骑规则 |

因此，用户所说“战斗中的任何时刻 F8 都应尝试接管”可以实现为：**每次都先尝试读取和准入当前原生状态**，而不是要求刚召唤或刚落地；但不能诚实地承诺任何未知活动控制器都能自动化。无法证明的状态应立即停止合成输入、保留玩家当前的原生状态并归还控制，而不是为了进入普通路线强制下坐骑／解钩。

当前白名单的 Windows Steam Terraria `1.4.5.8` x86 适合作为第一条此类路线的目标：程序集 SHA-256 为 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`，已有锁定的 type-23 和 item-84／projectile-13 只读证据。不过它只证明受限运动原语，**不证明 Boss 战胜率，也不自动授权生产输入**。

## 当前 F8 实际行为

现有代码不会保留活动控制器。路径如下：

```text
F8，发现已存在 Boss
  -> BuildCombatSnapshot
  -> PrepareForActiveEncounterDetailed
  -> 活动 MountActive 或 Grappling：AwaitingNativeMobilityRelease
  -> Runtime.BeginActiveNativeMobilityHandoff
  -> ActiveNativeMobilityHandoff
       抓钩：松开 Jump 一帧，再发一次 controlJump 脉冲以脱钩
       坐骑：松开 Mount 一帧，再发一次 controlMount 脉冲以下坐骑
  -> 下一帧确认二者均消失
  -> 再次 PrepareForActiveEncounterDetailed
  -> 普通 RecoverToPattern
```

证据位置：

- `src/Chaite.Core/ActiveNativeMobilityHandoff.cs` 的唯一已实现动作就是 `DetachGrapple` 和 `DismountMount`；它还故意限制在 12 帧内，避免重复触发原版的危险分支。
- `src/Chaite.Plugin/Runtime.cs` 的 `BeginActiveNativeMobilityHandoff`、`TryCompleteDeferredActiveAdmission` 和 `ApplyNativeMobilityHandoffPlan` 将上述一次性 release edge 写进当前游戏帧。
- `src/Chaite.Core/CombatPlanner.cs` 的 `PrepareForActiveEncounterDetailed` 在看见 `MountActive`／`Grappling` 时返回等待；`Plan` 和 `PlanSurvival` 又将任何仍活动的坐骑／抓钩转换为全中性、`RequestControlReturn` 的计划。

这条路径相对“猜测一个任意坐骑／抓钩”的物理语义是保守的，但不满足“保留合格活动坐骑”的目标。只删去这几项早期拒绝也不安全：普通候选模拟、`ActivePatternRecovery.Measure`、多个 Boss 专用分支和可选冲刺／重力／羽落分支都假设非坐骑、非抓钩的玩家物理。

## 已有证据与尚缺的生产闭环

### 女巫扫帚是唯一可优先考虑的活动飞行坐骑

`VanillaMountCatalog` 收录了全部 66 个 1.4.5.8 `MountID`，但除 type 23 外都只是 `IdentityOnly`。`WitchBroomMotion` 已对下列受限状态建立纯函数模型：

- 活动 `mount.Active == true`、`mount.Type == 23`，帧状态为 `0`、`1` 或 `2`；
- 正常重力、干燥、无 portal physics、无抓钩／飞行中的钩、无冲刺、无 CC／舌头／死亡、无滑轨／滑坡／风推／强制运动，且 `trackBoost == 0`；
- 20×42 hitbox、水平 `9 / 0.16 / 0.2`，以及 `Hover` 的上升、下降、中性纵向分支；
- 每一帧都重新证明干燥且开放的单步碰撞扫掠。

这些状态由 `NativeWitchBroomReader` 以只读字段／属性读取；`WitchBroomMotion.MatchesActiveBaseline` 也已明确说明“活动基线匹配不等于未来路径已经证明”。尤其值得注意的是，`TerrariaFacade.BuildCombatSnapshot` 现在以 `OpenDryPath = false` 调用读取器，正是为了避免把未经逐 tick 碰撞证明的扫帚误用于战斗输入。

`BossMobilityCapabilityEvaluator` 已能量化 `ActiveWitchBroom` 的理论速度和无期限 hover，但刻意将 `ProductionClosureCertified = false`。`CombatPlanner` 的普通候选还把 `flyingMount` 固定为 false，不能复用为扫帚飞行模拟。`WitchBroomRescueTrajectory` 已经覆盖“启用、观测、制动、安全下坐骑、回到底层闭环”的纯验证，但它不是长期 Boss 驾驶器。

故扫帚是**可以实现**的最小范围，而不是“已经支持”。

### 普通抓钩只适合作为有界退出动作

`NativeGrappleReader` 已能对普通单钩读取精确身份和附着事实：QuickGrapple 实际选中的 item 84、projectile 13、`aiStyle == 7`、`ai[0] == 2`、本地玩家 owner、`grapCount == 1`、`grappling[0]` 与 projectile index 一致，以及实际锚点 tile 和黑名单结果。`BasicHookMotion`／`BasicHookRescueController` 还对发射、命中、牵引、release/press 脱钩及回到低配置闭环有严格的纯路径合同。

但现有控制器只接受“由自己从无钩状态发起”的完整证书；它不会从用户已经附着的未知时刻接管。并且原版 `GrappleMovement` 会从锚点计算牵引并覆写玩家速度，普通 Boss 策略的水平／纵向闭环在此期间不成立。抓钩不应被伪装成持续飞行坐骑或下限机动配置。

## 最小可实施方案

建议新增一个独立的“活动原生机动接管协调器”，而不是扩大现有 `ActiveNativeMobilityHandoff` 的 release 逻辑。以下是实现顺序和边界；这些名称是建议，不是本次提交的代码变更。

```text
F8 during a supported active Boss
  -> InspectOnly（零合成输入）
  -> TryAdmitActiveBroom / TryAdoptAttachedBasicHook
  -> BroomRecoverToPattern | HookCertifiedExit | RejectAndReturn
```

### 1. InspectOnly：先读、后写，且不触碰原生释放边沿

F8 的第一帧只能收集一个完整快照并验证 Boss／输出基础门槛；不得调用 `AimAt`、不得按 `controlMount`、`controlJump`、`controlHook`、`controlDash`、`controlUp`，也不得选择物品或消费药水。此阶段应绕过旧的“先释放”状态机，而不是与它并行运行。

通过时把来源和身份锁存为不可更换的 route token，例如：

```text
active-broom-23 / mountType=23 / source sequence=N
attached-basic-hook-84-13 / projectileIndex=i / anchor=(x,y) / source sequence=N
```

锁存后每帧仍必须重新读取同一身份；不能因为背包里另有坐骑、抓钩、翅膀或冲刺饰品，就拼成一个新的能力集合。

### 2. `BroomRecoverToPattern`：唯一可成为下限路线的首期对象

此路径只接受 type 23，且只在下列条件全部成立时保留活动扫帚：

1. `WitchBroomMotion.MatchesActiveBaseline` 为真，活动 mount identity 和锁存 token 一致；
2. 当前 Boss 及其原生 phase/context 已通过普通的活动接管解析；输出准入、场地和威胁输入也完整；
3. 冷路径建立一个**扫帚专用**的、有限候选集合。每个候选只含 `left/right/up/down`，禁止 `Jump/Hook/Dash/ToggleMount/FlipGravity`；
4. 每个候选都通过 `TryAdvanceOpenDryTick`，对真实 20×42 sweep 重新取证，并验证当前和预测窗口内的所有已纳入威胁；
5. 用扫帚自身的预期位置、速度和停距衡量回归，而不是让普通 `ActivePatternRecovery.Measure` 假定地面跳跃／有限翅膀。连续至少三帧闭合后才离开 `RecoverToPattern`；无进展和总时限仍须有界；
6. 武器输出仅在同一帧的武器／弹药／弹幕合同、准星和扫帚状态均能证明时开启。若武器后坐、持有式物理或切换动画会改变这条扫帚轨迹，首期应停火而不是假装其不影响运动。

这要求一个明确的 `ActiveWitchBroom` Boss 控制器或候选模拟器，并让仅声明支持它的 Boss route 使用它。它不能通过将 `MountCanFly`、`MountRunSpeed` 传给普通地面候选器来实现。每个目标 Boss／难度的下限路线仍须各自声明、锁存和验证；扫帚存在不应自动令所有 Boss 的最低配置合格。

成功的第一帧可直接标为 `TacticalMode.RecoverToPattern`。这满足“从战斗中途进入时逐步回到闭环”，但不把当前位置误判为已经稳定。F9、Boss 结束、身份漂移或任何证据缺失时都只清除拆特写入的 controls，**不按 Mount 键**；玩家仍保持原本骑乘状态并立即拿回操作权。

### 3. `HookCertifiedExit`：保留到已证明的脱钩，不把抓钩当长期路线

首次接入可只支持已附着、静态、普通单钩，且必须同时具备：

1. 精确 item 84／projectile 13 identity；projectile 活动、owner 为本地玩家、`aiStyle == 7`、`ai[0] == 2`；
2. `grapCount == 1`、`grappling[0] == projectileIndex`，并且快照采集点确为 `GrappleMovement` 的同一原生更新边界；
3. 活动锚点是已知、实心、非平台／轨道／形状、非黑名单 tile，中心与 projectile 中心一致；
4. 正常重力、无坐骑、无液体／滑轮／CC／舌头／重力控制；没有第二钩、飞行中的钩、未知 projectile 或玩家身份变化；
5. 冷路径从**当前附着状态**构造新的 continuation certificate：当前牵引、可接受的脱钩位置、release 一帧、一次 press 边沿、脱钩后的返回轨迹、完整 tile sweep、威胁上界及 deadline 都必须可证明。

这需要一个新的“从 attached 状态开始”的 controller；不能直接调用当前 `BasicHookRescueController.Apply`，因为该控制器的起始状态要求它自己已经发射并追踪 projectile。该新路径在脱钩和明确 re-entry 后才调用普通 `PrepareForActiveEncounterDetailed`，使后续首帧进入普通 `RecoverToPattern`。

如果连续路径无法建立，不能自动按 Jump 来试探性解钩，也不能把 `grapCount > 0` 当作可忽略的小状态。应立即归还控制，保留用户的钩。抓钩最多是一次有界的“从当前位置安全退出”动作，永远不参与 Boss 下限机动能力的比较。

## 所需的原生状态与读写边界

| 类别 | 需要的只读状态 | 不能用聚合值替代的原因 |
| --- | --- | --- |
| 通用 | 本地玩家／网络模式、活动／死亡、Boss 和当前 native phase、位置、速度、hitbox、场地边界、已纳入威胁、当前输入 release edge、武器输出合同 | 默认值、旧帧或其他玩家的值会伪造可接管性 |
| 扫帚身份 | `mount.Active`、`mount.Type`、`mount._frameState`、type-23 item/buff 映射 | `MountCanFly` 与 `MountRunSpeed` 不能区分飞行坐骑物理 |
| 扫帚运动 | `position`、`velocity`、`gravity`、`gravDir`、`releaseUp`、`slowFall`、湿润状态、portal、track/dash/grapple/hook-in-flight、pulley/sliding/wind/forced motion、CC/tongue/dead | Hover 的分支与普通跳跃、翅膀、冲刺互不等价 |
| 扫帚碰撞 | 每一帧的 20×42 swept body tile 证据、干燥／开放路径、世界边界和停距 | 上一帧的“开阔”不证明下一帧仍可运动 |
| 抓钩身份 | QuickGrapple 解析出的 item 84、实际 projectile 13、owner、`aiStyle`、`ai[0]`、index | 有抓钩物品或 `grapCount > 0` 不足以说明是普通单钩 |
| 抓钩附着 | `grapCount`、`grappling[0]`、anchor tile、anchor/projectile center、黑名单、同一 `GrappleMovement` 边界 | 混用不同 Player.Update 时点会把已清空的 link 当成活动牵引 |

上述读取可继续采用已有版本锁定的编译访问器和只读 tile／projectile 枚举。不要用反射写 `Mount`、`Projectile`、速度、buff、资源或 `Player` 状态来“修正”不匹配；唯一允许的生产写入应是已经完成逐 tick 证书的原生 control 字段，且每帧都重新验证。

## Fail-closed 行为

| 发现的情况 | 必须做什么 | 禁止做什么 |
| --- | --- | --- |
| 非 type-23 坐骑、未知 mount type 或身份漂移 | 中性归还控制，保持玩家原样 | 依据 `MountCanFly`／速度猜测驾驶或按 Mount 键 |
| 扫帚碰撞、液体、威胁、帧状态、速度或状态证据不完整 | 停止该帧及后续合成输入，归还控制 | 沿用旧的 open-path／风险结果，强制下坐骑 |
| 抓钩不是精确且已附着的普通单钩 | 归还控制 | 按 Jump 试探解钩，删除 projectile，忽略多个钩 |
| 扫帚与抓钩并存、冲刺／重力／滑轮等混合物理 | 归还控制 | 组合两个独立合同来“补齐”能力 |
| Boss 离场、死亡、当前 phase 无法解析、路线不再收敛或到 deadline | 中性归还控制 | 延长接管时间、继续开火、把失败计为胜利 |
| F9 或输入被 UI 阻塞 | 立即清除拆特控制并取消 route token | 翻译成下坐骑／解钩脉冲，写入玩家存档或桌面输入 |

旧的 `ActiveNativeMobilityHandoff` 可以保留给以后明确选择“安全解除后接管”的独立模式，但不应作为新 preserve-first 路径的 fallback。否则一个本应被拒绝的活动控制器仍会遭到强制状态改变，违背本审计的安全边界。

## 低延迟与“不抢鼠标”要求

`HotkeyPoller` 仅查询 F8/F9 和前台进程；静态搜索未发现 `SendInput`、`mouse_event`、`SetCursorPos`、`keybd_event` 或桌面切换调用。新的接管检查也应维持这一性质：不发系统键鼠事件、不改变前台窗口、不启动游戏副本。

生产战斗路线现有的瞄准会写游戏进程内部的 `Main.mouseX/Y`，这与移动操作系统鼠标不同；但 preserve admission 的 InspectOnly 帧不应调用该逻辑。扫帚／抓钩接管的冷路径应只对本地 20×42 swept area、当前有限威胁表和已缓存的 Boss context 做有界读取，不能为此扫描整张地图或进行全 NPC 搜索。热路径应复用预分配候选和 tile 缓冲；若预算不足，拒绝而非降低验证精度。

## 实施前必须新增的验证

以下是门槛，不是已经通过的测试：

1. 为 type-23 的“已激活进入”新增 facade／reader 回归：活动身份、frame 0/1/2、每种拒绝状态、旧帧／身份漂移、无输入 InspectOnly，以及确保从未发出 `controlMount`。
2. 为扫帚新增完整的候选—真实帧一致性回归：每个发出的 `left/right/up/down` 都有单独 sweep／threat 证据；三帧收敛、无进展、deadline、F9 和输出冲突均有断言。普通 `ActivePatternRecovery` 的夹具不能替代这套物理夹具。
3. 为普通单钩新增“adopt attached”夹具：合法附着、wrong index/owner/type、多个钩、平台／黑名单锚点、预测脱钩、脱钩确认、miss/identity drift、F9。必须断言未认证时从不发 Jump 脉冲。
4. 在隔离 headless 新世界中进行版本锁定的真实 trace：不启动正式游戏、不读写用户存档、不抢鼠标。先验证一条扫帚中途接管和一条抓钩退出的原生逐帧状态，再扩至声明支持的每个 Boss、难度、变种、武器／机动路线。
5. 任何“扫帚可作为某 Boss 下限”或“重点 Boss 高可靠”的说法，仍需独立的预注册多种子验收；不得用其他 Boss、其他难度、抓钩退出成功或离线状态机回归抵消失败格。

## 建议的优先级

1. 先实现并验证 **type-23 扫帚的 preserve-first `RecoverToPattern`**，从一个明确声明支持的 Boss／难度路线开始；它是现有证据中唯一有机会成为完整下限机动路线的活动飞行坐骑。
2. 再实现普通单钩的 **certified exit**，把它限制为从中途状态安全回到普通闭环，而不是持续驾驶抓钩。
3. 每种其他坐骑单独补齐原生运动、碰撞、威胁、制动／解除和 Boss 真实验证后，才允许加入候选列表。不得按“也是飞行坐骑”复用扫帚合同。

本次只新增该审计文件；没有改变当前运行时的强制 release 行为，也没有把任何坐骑或抓钩标记为生产支持。
