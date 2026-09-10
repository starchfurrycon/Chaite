# 原版普通跳跃与云跳：只读机制审查

范围：Windows Steam Terraria **1.4.5.8 x86**，程序集 SHA-256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。以下来自 Mono.Cecil 只读元数据/IL 审查；没有 CLR 加载游戏、调用游戏函数、运行游戏、读取用户存档或模拟消耗随机数。本文记录机制与自有模型，不分发原版反编译源码。

## 关键时序

| 原版位置 | 含义 |
|---|---|
| `Player.Update` `0037..0059` | 每次玩家更新重设 `maxFallSpeed=10`、`gravity=defaultGravity`、**static** `jumpHeight=15`、**static** `jumpSpeed=5.01`。随后 `0253..0260` 给最大下落速度加 `0.01`。 |
| `Update` `0153..023b` | 水、蜂蜜、微光、三叉戟等改变基础重力/跳跃参数；不能把所有状态都视为干燥普通跳。 |
| `Update` `092a..09ec` | 按世界宽度、当前位置 Y 与 `worldSurface` 缩放重力；普通世界倍率下限 `0.25`，remix 下限 `0.1`。默认重力 `0.4` 的 remix 高空可以得到 `0.04`，不能强制最小重力 `0.1`。 |
| `Update` `2006 → 2281 → 248f → 335c` | 主要路径依次 `ResetEffects`、`UpdateBuffs`、`UpdateEquips`、`UpdateJumpHeight`。 |
| `Update` `3472..357f` | 阻止额外跳的坐骑清空次数；否则 `velocity.Y==0` 或贴墙滑行时刷新额外跳，空中移除已失去装备支持的次数。 |
| `Update` `4b5e → 4d7c → 4d89` | 水平运动、`UpdateControlHolds`、`JumpMovement`。`UpdateControlHolds` 处理方向键保持状态，不负责重置 `releaseJump`。 |
| `Update` `4d8e..4df6` | 清理无翼/无火箭的剩余资源；随后冲刺、墙面、飞毯与额外跳特效等分支。 |
| `Update` `4eda..5017` | 普通翅膀推进判定和 `WingMovement`，在 `JumpMovement` **之后**。 |
| `Update` `536c..54f0` | 火箭延迟仍可改变竖向速度；仅检查 `rocketTime` 是否为零不足以排除此分支。 |
| `Update` `6801..681c` | 普通支路给速度加 `gravity * gravDir`。随后 `684b..69bf` 沿下落方向钳制最大下落速度；不是对上升/下落同时做对称钳制。 |
| `Update` `7264..726a`、`8b25..8c6b` | 后续钩爪、黏附与实际碰撞/位移仍可能改变结果。局部跳跃模型不等于完整物理模拟。 |

当前插件入口位于 `Player.Update` 开头，因此不能直接读 static `jumpSpeed/jumpHeight` 作为本玩家本帧最终参数：它们可能仍属于上一位完成更新的玩家。实例状态也尚未经过本帧装备/Buff 重算，必须明确它们是**本次观察状态**，不能宣称预知同帧变化。

## `UpdateJumpHeight` 不是只读辅助函数

`UpdateJumpHeight` 的 `0000..01d3` 修改 static 和实例字段，不能为计算预览而调用。

- `jumpBoost`：高度至少 20，速度至少 6.51。
- `empressBrooch`、`frogLegJumpBoost`、`moonLordLegs`：分别向实例 `jumpSpeedBoost` 加 1.8、2.4、1.8；部分还修改 `extraFall`。月亮领主腿另给高度加 1。
- `wereWolf`：高度加 2，速度加 0.2。正在使用便携凳时高度加 5。
- `00e2..00ee`：将**已聚合** `jumpSpeedBoost` 加到速度。
- 坐骑按类型叠加或覆盖自身的速度/高度，且可能依赖当前水平速度。
- `sticky`：高度整数除以 10，速度除以 5；`dazed`：高度整数除以 5，速度除以 2。
- `UpdateBuffs` `4359..43da` 的蛛网禁锢另把高度设为 0、改变速度与重力。

因此从上帧实例读取 `jumpSpeedBoost` 后，不能再重复追加蛙腿、胸针、月亮领主腿贡献。当前实现不重建这个完整流程，只在明确排除了参数修正分支后采用已审查的 5.01/15 基础值。

## 普通跳与云跳的局部状态转移

`JumpMovement` 的普通/云跳子集：

1. 本帧没有按跳：`jump=0`、`releaseJump=true`，并重置火箭释放状态（`1ebe..1ece`）。**没有额外把上升速度砍半。**
2. 按跳且 `jump>0`：若竖向速度恰好为 0，则仅清掉计数；否则强制 `velocity.Y=-jumpSpeed*gravDir` 并将 `jump` 减 1（`057b..0607`）。这不是起跳后一直做自由抛物线运动。
3. 没有保持计数时，需有 `velocity.Y==0`、滑墙或可用额外跳等入口（`060c..06cc`）；并且需 `releaseJump=true`，或地面/滑墙支持的 `autoJump`（`06cc..06fc`）。云跳不能靠一直按住跳跃自动接续。
4. 普通地面跳设速度为负向 `jumpSpeed`，`jump=jumpHeight`（`0950..096e`）。启动这帧不减计数；标准高度 15 意味着启动帧加最多 15 个保持帧，不是总共 15 帧腾空。
5. 云跳消耗 `canJumpAgain_Cloud`，设 `isPerformingJump_Cloud=true`、速度为负向 `jumpSpeed`、`jump=(int)((double)jumpHeight*0.75)`（`088b..088d`、`1aa2..1b16`）。高度 15 对应计数 11。
6. 任意按跳分支结束都令 `releaseJump=false`（`1eb6..1eb8`）；释放一帧后才可再次建立边沿。
7. `RefreshDoubleJumps` 根据 `hasJumpOption_*` 恢复次数，普通地面起跳前/当下均有相关刷新；预测器必须在落地释放期间也恢复云跳，而不只在真正起跳时恢复。原版依据精确 `velocity.Y==0`，不是宽泛的“接近零”。

普通无翼/火箭支路的每 tick 顺序为：**跳跃转移 → 加重力 → 下落速度单向钳制 → 碰撞和位移**。默认重力 0.4 下，保持帧施加重力后的速度为 `-5.01+0.4=-4.61`。这是源码推导值，不是新增实机验收证据。

### 云跳与翅膀、火箭的关系

云跳入口**不要求** `rocketTime==0` 或 `wingTime==0`。它先于翅膀/火箭处理，并在选择额外跳时清 `canRocket/rocketRelease`（`0892..089b`）；普通翅膀推进另外要求 `jump==0`。不要把飞毯的资源前置条件误当成云跳条件：`CarpetMovement` 确实检查额外跳次数、火箭和翅膀资源，但它是另一个机制。

额外跳选择有严格优先级（`0792..0892`）：Basilisk、WallOfFleshGoat、Santank、Unicorn 等坐骑跳分支优先，其后可能下砸，再依次沙暴、暴雪、屁、海啸，最后才是云跳。当前模型只允许云跳，其他额外跳无论“有装备”“有次数”或“正在执行”均标为未知。

## 当前 `NativeJumpReader` 的接口与拒绝范围

`NativeJumpReader(Type playerType).Read(object player, bool mountActive)` 返回值类型 `JumpSnapshot`：`Known / RemainingTicks / Speed / Height / ReleaseReady / CloudAvailable / CloudEnabled / AutoJump`。

所有普通字段读取绑定都是源码中的显式类型/字面字段名。构造时编译只读委托；热路径遍历固定数组，不运行原版更新方法。便携凳的嵌套 struct 通过编译表达式直接读取，避免每帧反射或装箱。

`Known=true` 仅表示观察到的状态属于 5.01/15 普通/云跳子集。排除项：

- 环境/参数：`wet`、`shimmerWet`、`shimmering`、坐骑激活、非零 `jumpSpeedBoost`、`jumpBoost`、`wereWolf`、`moonLordLegs`、`empressBrooch`、`frogLegJumpBoost`、`portableStoolInfo.IsInUse`、`sticky`、`dazed`。
- 改写运动或控制：死亡/幽灵状态，`frozen`、`webbed`、`stoned`、`sliding`、`pulley`、`carpet`、`slowFall`、`vortexDebuff`、`tongued`、`onTrack`、非零 `grapCount/cartRampTime/rocketDelay`，下砸装备或正在下砸。
- 飞行：任何非零 `wingsLogic/rocketBoots`。**即使资源耗尽，也不把这类装备冒充为已精确建模**；翅膀仍有滑翔等不同运动。
- 其他额外跳：Sandstorm、Blizzard、Fart、Sail、Unicorn、Santank、WallOfFleshGoat、Basilisk 的 `hasJumpOption / canJumpAgain / isPerformingJump` 三种状态。
- 无效数据：跳跃计数不在 0..15，云跳可用却无对应能力、重力为负或非有限值、最大下落速度不为有限正数、重力方向不是 ±1。

未知时仍保留实例跳跃计数/释放/云跳状态，但 `Known=false`，速度与高度不填入伪造“精确值”。调用方可以保留另行标明的旧近似模型，不能把未知状态改名成已验证。

### 验证边界

新增离线回归检查共享 static 污染不影响读取、云跳读取不消耗次数、每个排除布尔分支、资源/异常数值、嵌套凳状态与暖态 10,000 次零分配。元数据单独核对了最初 60 个字面 Getter 字段绑定，以及随后追加的 `dead/ghost` 两个布尔字段；这不等于原生运动行为验收。

尚未支持/尚未精确验证：本帧装备或 Buff 变化、完整坐骑/翅膀/火箭/飞毯模型、其他额外跳、羽落与重力翻转操作、水与微光、绳索/钩爪、墙面/斜坡/半砖碰撞、瞬移/击退，以及完整客户端时序。预测过程中仍须由调用方处理支撑碰撞与每帧重采样，不能宣称任意机动装备都已精确适配。
