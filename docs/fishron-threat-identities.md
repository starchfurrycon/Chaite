# 猪鲨非机动威胁的已核实身份（1.4.5.8）

本文记录需要**用武器清除、无法靠走位规避**的猪鲨威胁。数值直接取自本机白名单
原版程序集（SHA-256 `960A03BF…A2F3`）的 `Terraria.NPC.SetDefaults` 与
`Terraria.Projectile.SetDefaults`，并与 [Terraria Wiki](https://terraria.wiki.gg/wiki/Duke_Fishron)
交叉核对。代码侧对应 [FishronThreatCatalog.cs](../src/Chaite.Core/FishronThreatCatalog.cs)。

## 为什么不能靠走位

三类泡泡的 `aiStyle` 都带强跟踪，速度与转向能力足以追上玩家；`Detonating Bubble`
还会在接近后爆炸，爆炸判定范围比本体更大。之前把受击归因到"走位不够好"是错的：
这些目标必须在抵达前被打掉。

| NPC | 名称 | 尺寸 | 生命 | 防御 | aiStyle | 特长 |
|---|---|---:|---:|---:|---:|---|
| 371 | Detonating Bubble | 36×36 | **1** | 0 | 70 | `noTileCollide`、强跟踪、爆炸 |
| 372 | Sharknado 生成泡（大） | 120×24 | **100** | **100** | 71 | 落地生成 Sharknado |
| 373 | Sharknado 生成泡（小） | 100×24 | **100** | **100** | 71 | 落地生成 Sharknado |

| projectile | 名称 | 尺寸 | aiStyle | `timeLeft` |
|---|---|---:|---:|---:|
| 384 | Sharknado 本体 | 150×42 | 64 | **540** |
| 385 | Sharknado 弹 | 30×30 | 65 | 300 |

## 哪些需要打，哪些不需要

**只有 371 需要武器清理。** 372/373 是生成 Sharknado 的那两个泡，**不能靠打**——原因
在原生 `aiStyle 71` 里：下降阶段它设 `dontTakeDamage = true`，任何武器都打不动它；
只有锁定期之后才 `dontTakeDamage = false`。它们 100 血 100 防御的数值只用于说明
"不要去打它们"。

## 372/373 的真实行为（决定了怎么躲）

读 `Terraria.NPC` 的 `aiStyle 71` 分支：

1. **下降段**（`ai[0] == 0`）：`noTileCollide`、`noGravity`，`velocity.Y = ai[3]` 持续
   下落，期间 **`dontTakeDamage = true`**。373 额外带横向摆动。
2. **锁定**：到达阈值后调用 `TargetClosest()`，然后
   **`velocity = normalize(玩家 - 自身) * 16f`**——朝玩家**直线**射出，速度 16 px/tick。
3. **落地**：与实体方块碰撞时 `life = 0` 并就地生成 Sharknado。

**关键结论：它不是追踪弹，是一条 16 px/tick 的直线弹。** 因此它的正确应对与猪鲨冲刺
**完全同构**——按它的速度向量取垂直方向脱离即可；此前把它当成"必须打掉的强跟踪弹"
是错的，把它当成"靠站位自然解决"也不准确。

同时这也解释了为什么"把龙卷风控制在场地边缘"是可行的：直线弹的落点由**发射瞬间玩家
所在的位置**决定，走位可以预先选择那条线的终点。

这条修正把武器门槛从"要能穿透 100 防御"改回**覆盖范围与攻速**：目标是 1 血 0 防御，
**单发伤害多少无关紧要**，低伤但范围大、攻速高的武器同样合格。

## 战斗机循环与威胁的对应

- 一阶段 `ai[0]==2`：沿"自身→玩家"连线每 4 帧生成一只 371，共 20 只。
- 一阶段 `ai[0]==3`：向下偏外生成 2 枚 385，落地成 384。
- 二阶段 `ai[0]==7`：以自身为圆心绕圈，每 4 帧一只 371，形成一圈。
- 二阶段 `ai[0]==8`：生成 1 枚 385，追踪玩家并生成 384（Cthulhunado）。

因此**一阶段的清理压力最大**（20 只 371/次），二阶段则在角落落下 384 与 372/373。

## 准入要求（新增）

猪鲨战斗的准入条件除已声明的机动路线之外，**还要求背包中携带足够的地狱药水**
（item 2348，施加 Buff 116）。判定的是**库存数量**而不是当前增益是否激活：已审核的
打法是战前喝一瓶，之后在增益将尽时用**快捷增益键**从背包续药，所以战前需要成立的
是"备货够打完全程"。

代码侧对应：

- `FishronThreatCatalog.HasSufficientInfernoStock(potionCount)` —— 库存门槛，
  数量不足或读取失败一律不满足。
- `FishronThreatCatalog.NeedsInfernoRefresh(buffStateKnown, buffTicksLeft)` ——
  剩余时间低于 `InfernoRefreshTicks`（900 帧）时按一次快捷增益键，留足余量；
  增益状态未知时 fail-closed 按需要续药处理。

两个门槛都是 fail-closed 的。

**这条准入不是一个"泡泡必然全清"的承诺。** 高难度下泡泡数值可能变化，狱火不一定
全部拦住；届时仍需用武器补掉威胁最大的那几个。因此武器仍然留在允许范围内（见上表），
只是不再单独作为硬性准入项。

社区已审核的答案是 **Golden Shower（黄金雨，1336）**。Wiki 攻略原文：

> The Golden Shower spellbook is helpful as it reduces his defense and easily
> pops all the Detonating Bubbles.

你提供的教学视频置顶评论也是同一结论：**"吐泡泡时喷一下，刚好能续上减益并清理泡泡"**
——一次喷射同时续 Ichor 减益并清空泡泡。候选清单（物品 ID 取自原版程序集）：

| 武器 | ID | 为什么合格 |
|---|---:|---|
| Golden Shower 黄金雨 | 1336 | 宽幅穿透流，顺带续 Ichor |
| Razorblade Typhoon 剃刀台风 | 2622 | 自动追踪，无需瞄准 |
| Razorpine 剃刀松 | 1930 | 攻速高，射程较短 |
| Inferno Potion 地狱药水 | 2348 | **完全不需要武器输入**，被动销毁泡泡 |

哪一把最终入选仍需隔离实测；`CommonWeaponOutputCatalog` 中已有剃刀台风的精确弹道
合同（projectile 409，存活 300 帧），Golden Shower 的弹道合同尚未建立。

## 尚未实现

本文只确立身份与准入要求。**清泡武器的实际接入（战斗热路径中在泡泡阶段切换到
专用槽位、并把它列入战前准入检查）尚未实现**，因此猪鲨翼类路线仍然是
"已审核来源、未取得产品支持"。
