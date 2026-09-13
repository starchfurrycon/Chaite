# 下一步：Demon Wings／火箭靴原生飞行模型研究

这是**下一轮实现建议**，不是已经交付的飞行适配。现有 production 文件未改动；没有构建、启动游戏或操作输入。

证据来源：Windows Steam 原版 Terraria 1.4.5.8 x86，SHA-256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。只用 Mono.Cecil 读取元数据/IL，没有 CLR 加载游戏或调用原生函数。原版函数名/偏移用于复核；本文不分发反编译源码。

## 为什么现有近似不足

当前基于 `max(wingTime, rocketTime)` 和固定竖向加速度的近似，混合了四种不同机制：翼推进、火箭批次、无动力滑翔、普通抛物线。真实机制还有额外跳与地面保持计数、资源转换和按键释放边沿。

尤其要避免以下误解：

- 翼推进当 tick **不再施加普通重力**。
- `rocketTime` 是剩余推进批次，不是剩余游戏 tick。
- 同时有翼和火箭靴时，火箭资源转换为翼时长，不是简单取较大值。
- 火箭批次开始后，松开跳跃不会立即取消该批次剩余推进。
- 翼时长归零后，按住跳仍可能滑翔；“没有飞行资源”不等于“必然普通自由下落”。

## 当前肉后测试装备的精确身份

`tools/GameProbe.cs` 的早期肉后 fixture 同时装备 Demon Wings 与 Lightning Boots；应先支持这一个明确组合，而不是默认它代表所有翅膀。

| 参数 | 只读证据 |
|---|---|
| Demon Wings 物品 ID **492**，翼槽/`wingsLogic` **1** | `ItemID.DemonWings`、`ArmorIDs.Wing.DemonWings` 常量。493 是 Angel Wings，不能混淆。 |
| Demon Wings 基础 `FlyTime=100` | `WingStatsInitializer.Load` `0049..004b` 与 `0097..00b1`。 |
| 翼水平速度覆盖值 6.25、加速度倍率 1、无下悬浮专用参数 | 同一初始化项；`WingAirLogicTweaks` 按已装备翼参数调整实例水平参数。 |
| Lightning Boots 物品 ID **898**，`rocketBoots=2`，基础 `accRunSpeed=6.75` | `Player.ApplyEquipFunctional` `1dd9..1e0e`；另给 `moveSpeed` 加 0.08。 |
| 默认 `rocketTimeMax=7` | `Player` 构造函数 `05d9..05db`；运行时仍应读实例值。 |
| `wingTimeMax` 来自实际物品 `wingSlot` 对应 `WingStats.FlyTime` | `ApplyEquipFunctional` `203a..2055`。 |

`GetWingStats(int)` 是只读表查询；`WingStats` 是值类型。但若通过通用 object 反射返回它，会装箱。低延迟适配宜使用初始化期编译的字段访问，或对已审查的翼 ID 使用显式默认资料，并核对实际实例参数。

## 每游戏 tick 的关键顺序

普通、干燥、无坐骑、无钩爪/绳索/异常状态的相关路径：

1. 装备重算与 `UpdateJumpHeight`；在空中且条件允许时执行 `WingAirLogicTweaks`（`Update` `4396..43c7`），然后才进入水平运动。不能用起跳后的 airborne 标志倒算起跳当帧的地面水平加速。
2. `JumpMovement`（`4d89`）：普通保持/释放、可用额外跳的优先选择。
3. 无翼/无火箭靴时清掉相应残余资源（`4d8e..4dab`）。
4. 翼资源恢复判定（`4e70..4eb9`）。
5. 根据**此时**的 `jump`、`wingTime`、输入和速度决定翼推进 flag（`4eda..4f13`）；若 true，调用 `WingMovement`（`5017`）。
6. 腾空且同时有翼与火箭靴时，把火箭资源转换成翼时间（`503d..50aa`）。该转换在 `WingMovement` **之后**；新增翼时间不回溯授权本帧原本未激活的翼推进。
7. 火箭资源恢复、火箭启动条件、当前批次施力（`518e..54f0`）。
8. 若本帧有火箭批次施力或翼推进，跳过普通重力；否则选择特殊坐骑/羽落/翼滑翔/普通重力分支。
9. 下落速度上限、原生碰撞与位移；后续钩爪等机制也会改变运动。

“普通保持计数从 1 减到 0”的**同一帧**，可以紧接着进入翼推进，因为翼 flag 检查发生在 `JumpMovement` 之后。不能多等待一个 tick。类似地，最后一单位翼资源在本帧消耗完时，flag 已经成立，本帧仍然不补普通重力。

## Demon Wings 的竖向推进

`WingMovement` 的默认参数（`0159..0176`）适用于翼 ID 1：基础推进 0.1，下落时附加制动 0.5，慢速上升时附加推进 0.1，慢速阈值为 `jumpSpeed*0.5`，上升极限 `jumpSpeed*1.5`。其他翼 ID 有不同参数或专用悬浮，不能沿用 ID 1 的规则。

正重力、正常参数下的自有伪代码：

```text
vy -= 0.1
if vy > 0:
    vy -= 0.5
else if vy > -jumpSpeed * 0.5:
    vy -= 0.1
vy = max(vy, -jumpSpeed * 1.5)
wingTime -= 1
本帧跳过普通 gravity
```

分支判断使用**已减去第一项 0.1 后**的速度。不能提前分类成“本帧开始时下落/上升”再合并常量，否则过零附近会不同。源码位置：`05ba..06c4`；消耗位置：`072c..0739`；跳过重力位置：`Update` `54f0..54f2 → 682d`。

未增益的 `jumpSpeed=5.01` 时，上升极限约为 7.515 像素/tick；快上升区通常每 tick 只额外推进 0.1，不是固定 0.35。该数值是源码推导，不是新原生行为验收。

普通翼推进成立条件包括 `wingsLogic>0 && controlJump && wingTime>0 && jump==0 && velocity.Y!=0`。本模型首版还应明确排除坐骑、鱼人、下砸、控制异常与特殊悬浮分支。

## 翼、火箭资源不能混成一个计时器

### 腾空转换

`Update` `503d..50aa`：同时有翼、火箭靴，且 `velocity.Y!=0`、`rocketTime!=0` 时：

```text
bonus = rocketTime * 6
wingTime = min(wingTime + bonus, wingTimeMax + bonus)
rocketTime = 0
```

此条件不要求当前正在翼推进，所以普通地面跳保持期间就可能发生。当前 fixture 在一次正常充分恢复后，通常得到 `100 + 7*6 = 142` 个翼推进 tick 的总资源；其中部分可能已经消耗。不能把它固定当成每次采样都拥有 142，也不能把 `wingTime>wingTimeMax` 当成非法状态。

现有 `wingTime/wingTimeMax` 的归一化也不是“装备总资源比例”：转换后分母仍是 100，而剩余值可能超过它。恢复策略应依据真实剩余 tick 与落地时间预算，不能仅依赖被钳制为 1 的百分比。

### 地面恢复

翼资源在 `JumpMovement` 后满足以下条件时恢复为 `wingTimeMax`（`4e70..4eb9`）：

```text
((velocity.Y == 0 || sliding) && releaseJump)
|| (autoJump && justJumped)
```

因此着地但一直按住、又没有满足自动跳跃分支，不等于本帧立刻补满。放开一帧有实际意义。火箭资源的对应恢复（`518e..51bf`）不要求同一个释放条件；不能复用完全相同的恢复 predicate。

绳索路径显式补充资源并跳过普通运动（`4323..4378`）。已经成立的钩爪运动调用 `RefreshMovementAbilities(true)`（`GrappleMovement` `00f0..00f2`），恢复翼、火箭、额外跳并清火箭延迟。**射出钩爪本身不等于已经抓住可刷新资源的实体表面**，不能预支恢复。

### 无翼时的火箭批次

火箭启动检查（`51d8..524f`）要求没有可用翼推进资源组合、火箭靴、按跳、`rocketDelay==0`、`canRocket`、`rocketRelease`、允许使用火箭能力，并有 `rocketTime>0`。成功消耗 1 单位 `rocketTime`，设 `rocketDelay=10`。

已有 `rocketDelay>0` 时，无论玩家这帧是否还按跳，都继续减少 delay 并施力（`536c..54eb`）。默认 7 单位可形成 7 批、每批 10 tick 的推进窗口，**不是 7 tick**；但必须满足各次启动条件，不能无条件预算成 70 tick。

这一普通火箭竖向加速度/限速形式与上述默认翼近似相同，但拥有不同的资源、边沿与持续时间语义。火箭支路直接跳至 `682d`，同样不再加普通重力。

`canRocket` 与 `rocketRelease` 都必须采样：普通/额外跳会清掉它们；松开跳可重置 `rocketRelease`，而速度进入允许区间后才允许 `canRocket`。不能只看 `rocketTime>0` 就虚构一次立即向上加速。

## 释放、额外跳、无资源滑翔

- 翼推进没有要求新的 `releaseJump` 边沿；普通跳结束后一直按住可以自然转翼推进。
- 额外跳需要相应释放/重按条件。按住时即使仍有云跳次数，也可能在计数结束后开始翼推进；不能误称“必须先耗完所有额外跳才允许翅膀”。
- 在空中松开再按、且云跳可用时，`JumpMovement` 先消费云跳并重建保持计数，延后翼推进。多种额外跳仍按原生优先级处理，不能强行指定云跳。
- 松开翼推进只是不再施加推力，不会清掉已有向上速度；必须保留惯性与重力。
- 正重力、非推进时，装备翼并按跳且正在下落，可走翼滑翔分支（`57e2..5810 → 674d..67f6`）。通常加 `gravity/3`，并限制到 `maxFallSpeed/3`；向下控制可绕过该较小的速度上限。是否耗尽 `wingTime` 不是这条滑翔入口条件。
- 松开跳通常恢复普通下落重力；“按住跳但翼空了”和“松开跳”轨迹不同。
- **倒重力滑翔不可直接按符号翻转套用。** 本版本入口可见字面的世界坐标 `velocity.Y>0` 条件，后面又存在倒重力分支；应单独进行原生轨迹对照后才标为支持。

## 建议下一轮实现边界

先实现正重力、干燥、无坐骑/钩爪/绳索/羽落/下砸/异常状态的 **Demon Wings ID 1**，分别覆盖无火箭与 Lightning Boots `rocketBoots=2` 的明确组合。普通跳参数仍限定到已审查的 5.01/15 无增益子集；不要把现有 `NativeJumpReader` 的“无翼已知”标志直接扩展成“所有飞行已知”。

建议值类型状态：

```text
FlightSnapshot:
  Profile / Known
  WingTime / WingTimeMax
  RocketTime / RocketTimeMax / RocketDelay
  CanRocket / RocketRelease
  JustJumped
  JumpSnapshot（独立的保持计数与释放/云跳状态）

FlightPhase:
  Ground / JumpHold / WingPowered / RocketBatch / Glide / Ballistic / Unsupported
```

以固定有界的一 tick 转移函数更新资源与速度，返回是否跳过普通重力；不用遍历搜索、不调用原生 `WingMovement`，不改任何原版资源。常驻 profile 与值类型保持低分配。横向、跳跃、翼、资源转换、火箭、滑翔、碰撞必须按原生顺序组合，不能仅把新竖向速度补丁塞进旧“统一重力”尾部。

优先新增独立无 Boss 原生轨迹对照：普通跳持续持有接翼、提前释放再起翼、100/142 资源耗尽、空翼持续按跳滑翔、落地未松与松开恢复、火箭转换发生当帧、无翼火箭批次中途松开、云跳后接翼。每帧至少记录 `prePlayer/preJump/postJump/preWing/postWing/postPlayer` 及资源/控制字段，校验本机 adapter 采样与完整一 tick 组合，不只验证复制原生参数的局部子函数。

这些测试是运动机制证据，不是 Boss 胜率或无伤保证。确认轨迹后，才能重新衡量肉后策略的垂直安全窗、剩余资源落地预算、恢复闭环与计算耗时。
