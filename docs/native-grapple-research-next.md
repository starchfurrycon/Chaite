# 普通抓钩可选救险层：Terraria 1.4.5.8 最小合同

范围：Windows Steam 原版 Terraria **1.4.5.8 x86**。本页只定义普通 `Grappling Hook` 的一个保守、可组合运动原语，不接入生产输入，不证明任何 Boss 胜率，也不把离线几何证书当作原生 Boss 实测。

## 证据锁定

- `Terraria.exe`：`D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe`
- SHA-256：`960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`
- 只读分析器：Mono.Cecil 0.11.6.0，当前本机构建副本 SHA-256 `C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B`
- 取证仅通过 Mono.Cecil 读取程序集元数据/IL；没有 CLR 加载或调用 Terraria 方法，没有启动游戏、读取存档、移动鼠标或生成原版资源副本。

以下指纹为 Mono.Cecil `Instruction.ToString()` 在 invariant culture 下按 IL 顺序、以 LF 连接后的 UTF-8 SHA-256；程序集总哈希是最终身份边界，方法指纹用于定位审查漂移。

| 方法 | 指令数 / CodeSize | IL 指纹 |
|---|---:|---|
| `Player.QuickGrapple()` | 553 / 1438 | `0B21B7FB02040FBC6CE644852B509F863E898C900EBB8DB5A972A5060B2B04E9` |
| `Player.QuickGrapple_GetItemToUse()` | 43 / 82 | `4DEBBE16514F4DB1DA4320617EE9A00CF02E4256393BE6ACC83896189E16B708` |
| `Player.GetGrapplingForces(...)` | 372 / 980 | `3AFEF2F013A6139B2B325B4E49F846D9DEB0E81CEF3AFCADE6DD91D0104DD47E` |
| `Player.GrappleMovement()` | 473 / 1295 | `D93767AAB0E3BBA24FE6F8F62623E9F560BD90C6F6FC4BCF99085E7B10CAEE1B` |
| `Projectile.AI_007_GrapplingHooks()` | 1128 / 3312 | `85C4D3447E6BCE2CF7250E25F37D1CBE2A65A18EA26F2D8B67176CC7507EE26F` |
| `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo(Tile)` | 47 / 124 | `2017051B0E26DAC2FFD856D9C9FC33747D65A1D31371985CFCBDCC831AE27249` |
| `Item.SetDefaults1(int)` | 33202 / 87746 | `3386DF0ADE1787078F979C8A641501AB4F0EB9656034407E206E6C479FEBBA13` |
| `Projectile.SetDefaults(int)` | 27110 / 75578 | `6D5C30D11359B9204A59ABDC347D92EFDE23F55BFE3CB27552D8F80D544CBA02` |

## 明确选定的原生 profile

`ItemID.GrapplingHook == 84`，其 `shoot == ProjectileID.Hook == 13`、`shootSpeed == 11.5f`、`useStyle == 5`、`useAnimation/useTime == 20`、`noUseGraphic/noMelee == true`。Projectile 13 是 18×18、`aiStyle == 7`、`netImportant == true`、`tileCollide == false`；`Main.Initialize` 会把所有 `aiStyle == 7` 的 projectile 标成 `Main.projHook`。

`QuickGrapple_GetItemToUse` 先看 `miscEquips[4]`，若其 `shoot` 被 `Main.projHook` 标记则直接使用；否则从 `inventory[0]` 到 `[57]` 选择第一个 hook。因而只看到“玩家有一个普通抓钩”还不够，适配器必须证明 **QuickGrapple 实际解析到的物品**完整匹配上述字段。当前纯函数用 `ResolvedByQuickGrapple` 表示这条证据，不能根据名称自行填 true。

## 发射不是瞬时附着

QuickGrapple 从玩家普通中心向鼠标世界坐标发射，速度归一化为 11.5 px/tick。普通 Hook 在 `ai[0] == 0` 时逐帧飞行并扫描附近物块；相对 `Player.MountedCenter` 的距离大于 300 px 后才切到 `ai[0] == 1`。返程速度对普通 Hook 是 11 px/tick，距离小于 24 px 后销毁。切换返程的同一 AI 调用仍会完成一次挂点扫描，因此 300 是用于候选证书的保守上限，不应据此伪造“301 px 必不可能挂中”的原生结论。

只有以下真实观测同时成立才进入已挂住状态：

- projectile 槽位已知且 `active`，`owner == Main.myPlayer`，`type == 13`，`aiStyle == 7`，`ai[0] == 2`；`Center` 与 AI 值均为有限数；
- `Player.grapCount == 1` 且 `Player.grappling[0]` 是同一个 projectile index；
- link 必须在 `GrappleMovement` 入口边界采集；Player 更新末尾会清空 `grappling[0]/grapCount`，Projectile AI 随后再为下一次 Player 更新登记，不能混用另一更新边界的槽位快照；
- projectile 的实际吸附中心与锚点 tile 的 visual hitbox 中心一致；
- tile 的 X/Y/type 已知，是 `nactive()`，且 `Main.tileSolid[type]` 或 tile 314（轨道），并且该 X/Y 不在玩家抓钩黑名单中。

预计锚点、射线路径或者单个 tile 坐标都不能替代这些字段。`BasicHookMotion.Decide` 明确覆盖：发射后短暂未观察到 projectile、`ai[0]==0` 出射等待、`ai[0]==1` 未命中返程、未见 projectile、超时、错误 owner/index/type、锚点改变等状态。等待 projectile/钩中期间并不冻结玩家；调用者仍执行原低配动作，只把经过评分的 hook pulse 叠入该 tick。

## 拉动与解除的逐 tick 语义

普通单钩附着后，`GetGrapplingForces` 使用真实 projectile 中心减 `Player.Center`，长度大于 11 时归一化到 11，否则直接使用剩余差值。`GrappleMovement` 每 tick 用该结果覆盖玩家 X/Y 速度，还会刷新移动能力、清除本 tick 的 `canRocket/rocketRelease`，并把向下拉动标记为 `GoingDownWithGrapple`。

解除不是“正在按跳就解除”，而是 `controlJump && releaseJump` 边沿：

- 未按跳的一帧只会把 `releaseJump` 重新置 true，并继续被钩拉动；
- 按跳但 `releaseJump == false` 仍继续拉动；
- 边沿成立时销毁玩家的全部 grappling hook 并刷新二段跳；
- 解除前速度长度小于 2（或在水中且旧 Y 严格位于 -0.02..0.02）时通常以 `-jumpSpeed` 跳离；否则保留拉动 X，并令拉动 Y 加 `0.01f`；
- `controlDown` 禁止完整跳离；若从零 Y 被钩向下拉且没有 `controlUp`，也禁止完整跳离。

纯 API `TryApplyAttachedTick` 保留“解除前旧速度”和“钩拉速度”的区分，并回报 `GoingDownWithGrapple`、release 状态、跳跃 tick、火箭状态清除和二段跳刷新；它不改玩家或 projectile。

## 平台、重力、坐骑及可选装备

- 平台在 1.4.5.8 同时是 `tileSolid` 和 `tileSolidTop`，原生 Hook 可以挂平台。但向下钩拉会置 `GoingDownWithGrapple`，随后玩家碰撞将 `fallThrough=true`；平台/单向碰撞必须有额外逐 tile 模型。首版安全 profile 因此拒绝平台、solid-top、轨道锚点，并要求整个拉动走廊已经证明无平台。
- QuickGrapple 会视坐骑能力尝试卸载，GrappleMovement 也会对不能用钩的坐骑再次尝试卸载。卸载、坐骑 hitbox 和资源切换尚未纳入本纯模型，因此 `MountActive` 一律 fail-closed。Boss 层应在“坐骑救险”和“抓钩救险”间择一，不把两者叠成未经验证的新轨迹。
- 抓钩拉速使用世界 X/Y；解除的完整跳离直接写 `velocity.Y=-jumpSpeed`。倒置重力及 gravity-control 切换另有输入/碰撞语义，本 profile 只接受正常重力且没有重力控制。
- 羽落可存在，但发起前的 `ReturnTrajectoryCertified` 必须明确用同一个 `SlowFall` profile 计算；状态不一致会拒绝。也就是说，羽落不会让无羽落低配闭环静默变形。
- pulley、液体以及 grapple/interact 共键模式也暂不属于这个最小合同。

## 保守候选与回到原 Boss 闭环

开始发射前必须有 `BasicHookRouteCertificate`：

1. 0..300 px 的逐 tick hook 扫掠已知，且第一个可挂 tile 被证明就是目标锚点；
2. 锚点、拉动走廊与平台集合完整已知，走廊无阻挡且无平台；
3. 解除点位于起点到锚点的直线拉动段上；
4. 从解除点返回原低配闭环的轨迹已经按当前羽落 profile 验证；
5. 原闭环的 epoch、位置/速度包络和 latch/pull/reentry deadline 都已声明。

同一个证书还必须有完整救险分数，并分别声明已覆盖 **出射等待、未生成或未钩中的回退窗口、拉拽、主动解除、回到闭环** 五段；最坏风险不得超过声明阈值。运行中的每一 tick 还要提供新的 threat-field 结果，证明证书仍安全且实时最坏风险没有越界。缺一段、缺实时结果或新弹幕使旧路线失效都会 fail-closed。

运行时只有到达解除半径后才做“先松跳一帧，再按跳一帧”的建议。解除后只有同时回到原闭环的位置与速度包络、Boss 策略 epoch 未变化，才返回 `ReenteredLowConfigLoop`；否则最多返回 `FollowCertifiedReturn`。未生成 projectile 或原生进入 `ai[0]==1` 时走预先评分的 `FollowCertifiedMissRecovery`，并保留明确 contingency；锚点变化、Boss 阶段变化、实时轨迹失效或任一 deadline 超时才是 Abort。这样可选抓钩失败也不会把无抓钩基线留在未定义状态。

## 验证边界

`GrappleMotionContractTests`、`GrappleRouteFactoryTests`、`BasicHookRescueControllerTests` 和 `NativeGrappleReaderTests` 已接入共享 csproj／Program，合计覆盖 31 个确定性子检查：精确身份、11.5 发射、原生／安全锚点差异、真实 projectile/link 及采样边界绑定、11 拉速、解除边沿、平台／坐骑／重力拒绝、羽落 profile 匹配、未命中与返回状态机、逐 tile 扫掠证书、实时威胁重评分和 NaN／Infinity。`audit-native-grapple-contract.ps1` 另以固定哈希只读核对九个原生方法；这些都不启动 Terraria。

仍需完成后才能用于生产：把 `BasicHookRescueController` 接入某个具体 Boss 的候选生成和稳定闭环；为该生产路径提供私有桌面的原生 hook 发射／未命中／附着／解除 trace及逐 Boss 验收。当前 `CombatPlanner` 没有调用该控制器，遇到活动抓钩会发出全中性控制归还；因此现有离线模型不能计入任何 Boss 的 40 种子胜率矩阵。
