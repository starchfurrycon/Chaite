# AI_008 魔法弹、风与液体审计（2026-09-12）

## 范围与证据

- 目标是本机白名单 Windows Steam Terraria 1.4.5.8 x86：`Terraria.exe`
  SHA-256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。
- 证据来自 `.analysis-tools/projectile-decomp/Terraria.Projectile.decompiled.cs`
  和 `tools/research/Inspect-MethodIl.ps1` 的只读元数据检查。没有启动
  Terraria，没有读取或写入任何玩家/世界存档，也没有修改正式安装。
- 文中“原版”只指上述哈希锁定的程序集。内存注入、DLL 替换或另一个补丁
  可以改变静态字段，不能继承本结论。

## 结论摘要

1. 这个白名单原版中，`Main::.cctor` 将 `Main.windPhysics` 初始化为
   `false`，全程序集只找到这一次对该布尔字段的写入。因此正常原版运行时
   下方的通用风代码实际上不会执行。
2. 不能只因为这一版本默认关闭就把无风当成永久前提。运行时若字段变成
   `true`，AI_008 的 `15`（Flower of Fire）、`95`（Cursed Flames）和
   `253`（Flower of Frost）都会进入通用风候选；其中很多箭、子弹和
   `aiStyle=2` 输出也会受影响。
3. AI_008 的“前 19 次更新直线”仅描述 AI 本身没有改变发射速度。它不是
   在表层、开风或穿过液体时的完整直线弹道证明。现有短前缀求解器在这三种
   环境条件没有被同帧门禁证明时不能把命中当作可靠事实。
4. 现在的生产输出有统一、常数开销的最终门禁：在真正设置
   `controlUseItem` 前读取一次 `Main.windPhysics`；读取异常、未知或为
   `true` 时，已指定的生产输出路线一律停火。官方原版的 `false` 正常
   放行。该门禁不扫描地图，不做 RNG，不改变原版状态。

## AI_008 的精确时序

`Projectile.SetDefaults` 中三个相关输出都为 16×16、`aiStyle=8`、
`friendly`、`magic`、默认 `tileCollide=true` 且默认 `ignoreWater=false`：

| 武器 | 弹幕 | 初速 | 前缀 | 20 次更新起的 AI 行为 |
| --- | ---: | ---: | --- | --- |
| Flower of Fire `112` | `15` | 7.5 | 1..19 | `velocity.Y += 0.2` |
| Cursed Flames `519` | `95` | 10 | 1..19 | `velocity.Y += 0.2` |
| Flower of Frost `1264` | `253` | 9 | 1..19 | `velocity.Y += 0.2` |

AI_008 对这三个类型都会先把 `ai[1]` 加一，再在 `ai[1] >= 20` 时增加
`Y` 速度，最后以 16 限制正向落速。因此第 1 到第 19 个**弹幕更新**里
AI 没有修改发射向量；第 20 个更新已经不是短直线合同的范围。它们都没有
`extraUpdates`，所以这里的“更新”也就是普通一帧的该弹幕更新。

但 `Projectile.Update` 的顺序是：`AI()`、通用风检查、液体检查、
`HandleMovement`、位置更新。也就是说，风发生在 AI_008 的“未加重力”
前缀之后、实际移动之前；它可以在第 1 次更新就改变 `X` 速度。

## Native 风何时真正作用

`Projectile.ShouldUseWindPhysics()` 先要求 `Main.windPhysics == true`。
若 `ProjectileID.Sets.WindPhysicsImmunity[type]` 有显式值则取其反值；否则
`aiStyle` 为下列之一才为真：

```text
1, 2, 8, 10, 14, 16, 17, 21, 24, 28, 29, 32, 33, 34, 35, 49,
72, 93, 96, 106
```

只读静态初始化检查确认 `15`、`95`、`253` 不在 WindPhysicsImmunity 的
显式豁免表中，因而三者在风开关开启时随 `aiStyle=8` 进入候选。

通用 `Projectile.Update` 分支还要求以下条件同时成立：

- 弹幕中心 `Y < Main.worldSurface * 16`；
- 中心所在 tile 非空且 `wall == 0`；
- `vx` 与风反向，或 `abs(vx) < abs(windSpeedCurrent * windPhysicsStrength) * 180`；
- `abs(vx) < 16`。

满足时它把 `windSpeedCurrent * windPhysicsStrength` 加到 `vx`。本机 IL
还确认随后的 `MathHelper.Clamp(vx, -16, 16)` 返回值被 `pop` 丢弃；不应把
这次调用当成可依赖的速度上界。

此外 `aiStyle=2` 的 `Projectile.AI()` 在开关为真时会先直接对 `vx` 加同一
风量，不受世界表面、墙或高度限制。Pew-matic Horn 的输出 `968` 正是
`aiStyle=2` 且没有该豁免；这说明“全部改为地下再开火”不能作为统一生产
方案。普通箭/许多子弹的 `aiStyle=1`、Pew-matic 的 `2` 都由统一门禁覆盖。
Space Gun 的 `20` 在这个版本被显式 WindPhysicsImmunity 豁免，但统一门禁
宁可在非原版开风状态暂停整个已审查输出，也不会维护一份会漂移的例外清单。

### 是否可安全只限制地下路径？

对 **仅 AI_008** 的通用 `Update` 风分支，可以：若实际生成中心和直到命中
前每次原生采样中心都满足 `centerY >= worldSurface * 16`，通用分支因高度
条件失败；直线段的两个端点都在该界线以下时，线性前缀也不会穿回表层。
这种替代路线仍须以同帧的实际 `MountedCenter/RotatedRelativePoint` 发射点、
当前 `worldSurface` 和有限 19 更新路径核验，读取失败即拒绝。

它不适合现在的统一策略：它不能涵盖 `aiStyle=2` 的 AI 内风修改，也会引入
额外的坐标、路径和 tile 边界合同。当前白名单原版的真实状态是风关闭，
所以“同帧读取为 false，否则停火”既更低延迟也更严格。

## 液体：必须单独门禁

风门禁不解决液体。三个 AI_008 弹幕没有 `ignoreWater`：

- `ProjectileID.Sets.IsDestroyedByWater` 的静态表包含 `15`，不包含 `95`
  或 `253`。`15` 已处于非熔岩湿润状态时会走 `Kill()`；不能把其水中路径
  当作可命中直线。
- `95` 和 `253` 虽不在该销毁表，但 `HandleMovement` 在湿润状态下使用水的
  0.5、蜂蜜的 0.25、微光的 0.375 移动向量（并仍做碰撞处理）。未建模的
  位移已经足以否定无液体的拦截解。

因此短直线前缀若要作为真实发射合同，仍需要一个 **AI_008 专用**、同帧、
只读的干燥走廊门禁。最小安全设计是：

1. 只在求解器已经准备发射后执行；取真实原生发射中心、已选 aim、19 次
   更新上限和 16×16 弹幕尺寸。
2. 以不大于一个 tile 的固定间距采样这条有限线段，并给 16 px hitbox 与
   采样误差留保守边距；读到任意 `Tile.liquid > 0`、空 tile、越界或反射
   异常就拒绝。
3. 不扫描 NPC、不扫描全地图、不尝试区分水/蜂蜜/熔岩/微光的可行路径；
   这最多是短距离、固定上界的 tile 读取，并且只用于 `15/95/253`。

在该门禁落地前，当前短前缀的正确结论是：它在白名单原版、无液体且风标志
确为关闭时保留 AI 时序正确性；它不是跨液体的生产级命中证明。

## 已落实的统一风门禁

实现位于：

- `src/Chaite.Core/NativeWindEmissionGate.cs`：对所有已指定的
  `OutputRouteKind` 实施 `known && !enabled` 合同，未知枚举值失败关闭；
- `src/Chaite.Plugin/TerrariaFacade.cs`：在常规输出、minion/whip 输出和
  Lacewing 启动阶段的实际武器射击处，均在最终 `controlUseItem` 前读取
  `Main.windPhysics`；
- `tests/Chaite.Tests/NativeWindEmissionGateTests.cs`：覆盖全部已声明路线的
  原版关闭放行、开风拒绝、读取未知拒绝和未来枚举值拒绝。

该设计覆盖箭、子弹、Space Gun、Pew-matic Horn、AI_008 魔法和后续新增的
已声明输出，而无需逐武器全图扫描或依赖不稳定的 `aiStyle` 例外表。
