# 配装集与模拟完整性审计（2026-09-21，依据用户裁定）

> **本文的状态（2026-09-21 晚）**：本文列出的三个结构缺口（NPC 类威胁不可见、
> 弹幕窗口截断、明胶女士鞍口径）**都已在
> `verification-2026-09-21-training-structure.md` 里修完并做了引擎内验证**。
> 本文保留为缺口被发现时的原始审计记录。改动摘要：
>
> - §二 的准入集从 7 条变 4 条：明胶女士鞍已按用户 2026-09-21 指示**删除**，光女 2 条已随光女整体退出范围。
> - §3.1 的 NPC 威胁已并入观测（12 维），实测 34.5% 的受击来自这类威胁。
> - §3.2 的弹幕窗口已加 `CHAITE_PROJ_SORT=threat` 与 `CHAITE_PROJ_COLLAPSE`，
>   实测截断率从 40.55% 降到 0%。
> - §3.3 的观测宽度从 98 变 112（+12 NPC +2 BOSS 攻击时钟）。
>
> **2026-09-23 追加**：§二 的准入集再变为 **3 条** —— 可靠疾旋鼬/桃旋鼬
> （MountID 64/65）已按用户 2026-09-23 指示删除：三层平台竞技场的大师难度实测
> 只能爬升 38 格，莉莉丝狼能爬 70 格，而平台行距 60 格。删除范围与理由见
> `docs/route-equipment-matrix.md` 的"舍弃"一节与 `docs/current-focus.md` C125。

## 一、用户裁定（四条，均以此为准）

1. **雨天虾松露不准入** —— 历史遗留，直接清理。
2. **莉莉丝狼不是"可靠疾旋鼬"** —— 它是一条**被漏掉**的独立路线，应补入准入表。
3. **`queen-slime` 就是明胶女士鞍（MountID 50）** —— 名称应统一为明胶女士鞍。
   （后续：使用者 2026-09-21 指示该路线**整体删去**，见下。）
4. **仙灵之翼是"弱翼"的统称，不是强翼** —— 因此 `fairy-wing` 与 `strong-wing`
   是**两条不同的路线**，不能合并。

## 二、修正后的准入集（4 条）

| Boss | 路线 | 身份 |
|---|---|---|
| 猪鲨 | 强翼 + 冲刺 | 蛙腿、同级强翼、冲刺来源 |
| 猪鲨 | **弱翼（仙灵之翼）+ 冲刺** | 较弱的同级翅膀统称 |
| 猪鲨 | 可靠疾旋鼬 / 桃旋鼬 | MountID 64/65 |
| 猪鲨 | **莉莉丝狼** | 原先被漏掉，补入 |

**已删除**：明胶女士鞍（MountID 50）。使用者 2026-09-21 指示"可以直接从猪鲨中
删去，注意同时删掉程序中的准入口径，防止后续的口径不一致"。枚举、准入、
规划器分支、脚本文件、探针两个配装分支、两份工具门禁、契约测试与两份训练
会话清单中的对应条目已**全部删除**；坐骑 50 现在选中 `None`。

与训练侧对照：已训 8 条 = 上述 4 条 + 已删除的明胶女士鞍 + 重复的
`fishron-trusty-chillet-ignis`（用户已说明它与 `trusty-chillet` 是**完全一样的
换皮坐骑**，应合并）+ 光女 2 条（已随光女整体退出范围）。即本轮重训的路线集是
**4 条**，比上一代少四条。

## 三、模拟完整性审计（用户第二问）

### 3.1 猪鲨泡泡：**是 NPC，不是弹幕**（严重）

`tools/GameProbe.cs` L245-246 原文：

> The Fishron threats are **NPCs, not projectiles**: 371 Detonating Bubble,
> 372/373 the Sharknado-generating bubbles, 384 the Sharknado column.

后果：

* 它们**不在**弹幕窗口 `pr` 里，也**不计入** `pc`；
* 探针里确有 `ThreatCount`（L842：`if(d<400f) sample.ThreatCount++;`），
  它**会**统计这些 NPC 威胁，但**只写进状态采样**（L929 `threatsWithin400`），
  **没有进观测向量** —— 观测的 22 个基础特征（`chaite_env.py` L230-251）里没有它；
* ⇒ **策略对猪鲨泡泡完全不可见**。这可以单独解释猪鲨的卡点，且与我此前
  "受击时看得见威胁"的结论**不矛盾**：那次统计用的是 `p2/p4/p8`（弹幕），
  而泡泡根本不在这套统计里 —— **我此前的"威胁可见"结论对猪鲨是无效的**。

**待办**：把 NPC 类威胁（泡泡/龙卷风柱）并入观测。最小改动是把已有的
`ThreatCount`（400px 内 NPC 威胁数）加入观测尾部，与现有聚合特征同一手法。

> **已完成（2026-09-21）**：实际做的是 12 维而不是 1 维 —— 三个距离带计数
> （`nt2/nt4/nt8`）加上最近威胁的完整几何（相对位置、速度、距离、类型、血量、
> 尺寸、存在位）。引擎内实测该块非空（`nrt=371` 共 2140 行），并且**量化了
> 代价**：obsb1 的 2757 个掉血帧里有 **951 帧（34.5%）的 `pc==0`**，
> 即那一帧没有任何敌对射弹，伤害只能来自 NPC 类威胁。
> 详见 `verification-2026-09-21-training-structure.md` §1。

### 3.2 弹幕：**有上限，且会丢弃**

`CaptureHostileProjectiles`（L1160）里 `const int maximumHostileProjectiles=48;`，
超出即 `omitted++`（L1183），并把 `omittedHostileProjectiles` 写进状态（L1411）。

* 训练侧实际只用 `CHAITE_PROJ_SLOTS`（多数会话为 **12**）；
* 实测并发上限 44，所以 48 的**采样**够用，但 **12 槽的观测会丢掉大部分**；
* ⇒ "弹幕是否被完整模拟"：**采样不完整（有上限），观测更不完整（12/44）**。
  但这**不是**当前主要瓶颈 —— 因为受击时 400px 内弹幕数 p50 仅 0（猪鲨）。

> **已修正并量化（2026-09-21）**：真正的瓶颈不是"槽位少"，而是**槽位被无害的
> 柱状体占满**。实测 obsb1 有 **40.55%** 的行被截断，**90.13%** 的行 12 个槽位
> 全是 384/385/386（鲨鱼龙卷风柱），掉血帧里 78.8% 的槽位出现是 ty=384；
> 而真正会命中的弹（720、204/205、43、201-203）被挤出窗口。
> 已加 `CHAITE_PROJ_SORT=threat`（按预计接触时间排序）与
> `CHAITE_PROJ_COLLAPSE`（同类型只留最近一个，被折叠数写入 `pe`）。
> 引擎内实测：排序开关生效、折叠确实折叠了东西（`pe` 取 0/1/2），
> **截断率降到 0.00%**，槽位里出现的全是真正会命中的弹。
> 详见 `verification-2026-09-21-training-structure.md` §2。

### 3.3 泡泡打破：**有处理，但概率模型待核**

`bubblesBroken` 已在 episode schema 里（`chaite-env` 的 episodes 输出），
L1344 注释提到"The Inferno ring is the route's stated bubble-clearance"。
⇒ 泡泡**有**被打破的模拟。但**具体概率/条件需要读实现确认**（下一步）。

## 四、配装内部物品 —— **已逐条读出**（`src/Chaite.Manager/LoadoutCatalog.cs` L48 的 `Entries`）

| 路线 | 实际物品 | 蛙腿 | 气球束 | 羽落药水 | 冲刺来源 |
|---|---|---|---|---|---|
| 弱翼（仙灵之翼） | `FairyWings` + `FrogLeg` + `ShieldOfCthulhu` | **有** | 无 | **无** | 克苏鲁之盾 |
| 强翼（猪鲨翼） | `FishronWings` + `FrogLeg` + `ShieldOfCthulhu` | **有** | 无 | **无** | 克苏鲁之盾 |
| **莉莉丝狼** | `LilithNecklace` + `BundleOfBalloons` + `FeatherfallPotion` | — | **有** | **有** | 坐骑自带 |
| 可靠疾旋鼬/桃旋鼬 | `Chillet` + `ChilletIgnis` | — | 无 | 无 | 坐骑 |

结论（回答用户三问）：

1. **翅膀配装都带蛙腿**（两套 wing 路线都含 `FrogLeg`）；
2. **莉莉丝狼带多段跳气球束**（`BundleOfBalloons`），**并且它还额外带羽落药水**
   —— 它是**唯一**带羽落药水的配装；
3. **除莉莉丝外其余配装都没有羽落药水**（可靠疾旋鼬/桃旋鼬、弱翼/强翼都没有）。

补充：`FormulaRouteCatalog.Select(..., bool frogLeg, ...)`（L71）把**蛙腿作为路线身份的一部分**，
即"带没带蛙腿"不是可选项，而是判定路线时就要核对的输入。
莉莉丝狼的身份是 **MountID 52**（L43、L84-85：项链 item 5130 召唤 `MountID.Wolf` = 52），
与可靠疾旋鼬/桃旋鼬的 64/65 **确实是不同路线**，与用户裁定一致。

## 五、这些增益是否被模拟 —— **是，且走原生 API**

| 增益 | 探针证据 |
|---|---|
| 蛙腿 | `p.autoJump`（L1364）；`FrogLegItem = 2423`（`FormulaMobilityContract.cs` L9） |
| 气球多段跳 | `p.canJumpAgain_Cloud`（L1393）；`player.armor[3].SetDefaults(1164)`（L2621） |
| 羽落药水 | `player.AddBuff(BuffID.Featherfall, 36000)`（L2612/L2626）；`ItemID.FeatherfallPotion`（L2657） |
| 原版派生 | L2610 注释："Never assign slowFall directly: UpdateBuffs must derive it on…" |

⇒ 模拟完整且通过原生调用（`AddBuff` / `SetDefaults`），行为由原版引擎推导，不是手写近似。

### 但发现一个真实缺口：多段跳的"剩余次数"不在观测里

`src/Chaite.Core/PlayerForwardModel.cs` L31-34 原文：

> The jump control is down. **The recorded observation does not carry the multi-jump
> charge state**, and inventing it would be a…

> **已完成（2026-09-21）**：九个充能字段（`canJumpAgain_Cloud/_Sandstorm/
> _Blizzard/_Fart/_Sail/_Basilisk/_Santank/_Unicorn/_WallOfFleshGoat`）已作为
> `jc0..jc8` 写入桥接观测并在三侧同步。引擎内实测（`fishron-lilith-wolf`，
> 254,957 行）：气球束把 **jc0/jc1/jc2 三种**都置为 True，jc3–jc8 全 False
> ——即气球束给的是三种充能而不是一种，这就是"九个布尔而不是一个计数"的实测理由。
> 详见 `verification-2026-09-21-training-structure.md` §5b。
>
> **适用范围补记（2026-09-24，不改上面这段测量）**：这条读数来自已取消的
> `fishron-lilith-wolf` 路线。就验收范围内的两套配装（`strong-wing` / `fairy-wing`）而言，
> `jc0..jc8` **恒为 0、不携带信息**：气球束（1164）只有 lilith-wolf 携带，且
> `ChaiteObservationRow.JumpCharges` 在 `src\` 里**从未被赋值**（只有声明、计数常量、
> 名字表与两处输出循环）。把 9 维换成"着陆图"（地形/平台信息）的实验已回滚——Master 难度、
> 走生产挂载路径下强翼 **82.1%（32/39）→ 0.0%（0/38）**、弱翼 **9.7% → 0.0%**，
> **不要再盲目重试**。这 9 维的语义不需要再改。

## 六、最严重的问题：训练路径用的是一份**未修正**的装备代码

`tools/GameProbe.cs` L2579 `EquipScenario(Player)`（L2433 调用）是装备施加点。
Fishron formula 路线有**两份**互斥的装备代码，由 `EquipmentTier` 选择：

| 块 | 守卫 | 内容 |
|---|---|---|
| **L2758** | `EquipmentTier == "early-hardmode"`（L2739） | 坐骑路线**只有坐骑**；莉莉丝狼**掉进 `else` 拿到翅膀** |
| **L2818** | `EquipmentTier == "post-plantera"`（L2787） | 莉莉丝狼 = 项链(5130) + **气球束(1164)** + **羽落**；疾旋鼬 = 坐骑 + **气球束** + **羽落** |

L2818 块的注释原文：

> A reviewed loadout is a whole SET, not one item… **The user confirmed on 2026-09-19**
> that the mount sets carry no boots, and that set 4 is the mount plus the Bundle of
> Balloons (1164) and featherfall. **The fixture previously gave every Fishron route the
> boots and gave set 4 neither the balloons nor the potion, so both mount sets were
> training against gear nobody wears.**

也就是说：**2026-09-19 那次修正被写进了 `post-plantera` 块，而训练路径走的是
`early-hardmode`，从未得到这次修正。**

依据 L2030 原文：

```csharp
scenario.EquipmentTier = IsMonitorFixture ? "post-plantera" : "early-hardmode";
```

⇒ **训练（非监控）用 `early-hardmode`**，因此：

1. **气球束与羽落药水在训练战斗里不存在**（只有监控路径有）；
2. **`fishron-lilith-wolf` 在 `early-hardmode` 块里掉进 `else`**，训练时骑的是**翅膀**
   而不是莉莉丝狼坐骑 ⇒ 该配装此前所有训练都是无效的。

（更正：我先前说"莉莉丝狼从未被 `EquipScenario` 处理"是**读漏了第二块**的误判；
它**被处理了**，只是在另一个 tier。结论方向不变，但表述必须准确。）

**修复清单**：

* 把 L2818-2864 块的内容**移植进 `early-hardmode` 块（L2758-2784）**，即让训练路径
  与已审阅的配装一致：莉莉丝狼加项链+气球+羽落、疾旋鼬加气球+羽落、
  三套翅膀加羽落（明胶女士鞍已按 2026-09-21 的指示整体删除）；
* 改完必须用**隔离探针**验证分支确实被执行（项目纪律）：比较改动前后同一场景的
  `equipmentReport`（`bundleOfBalloonsItemType`、`featherfallActive`、
  `featherfallSetupViaNativeAddBuff`）与 ticks/hits/damage —— **完全相同即未执行**；
* 修复后**此前的检查点全部作废**（训练环境变了），必须重训。

> **已完成（2026-09-21）**：移植已落地，并且本轮又在 `early-hardmode` 块里
> 补齐了莉莉丝狼的 `miscEquips[3] = 5130`（此前该分支只设了气球束与羽落，
> **漏了项链本身**，所以"莉莉丝狼"在训练里骑不上狼）。
> 引擎内验证：`chaite_env.py` 与探针的 `bs2/bs3` 观测、`ps/pe` 诊断字段都已在
> 真实运行里确认非空（见 `verification-2026-09-21-training-structure.md`）。

## 八、动作空间缺下键的**根因**（2026-09-21 定位，用户的诊断得到证实）

用户的判断完全正确：动作空间只有水平方向 `{-1,0,1}`，**下键根本无法表达**。
根因不在探针里，而在**插件**里：

| 环节 | 位置 | 作用 |
|---|---|---|
| 解析动作行 | `src/Chaite.Core/RouteReplay.cs:227` `TryReadBridge(...)` | 读 `tick,dir,jump,dash` |
| **应用动作** | `src/Chaite.Plugin/Runtime.cs:301-359` | **L312-313 是缺陷所在** |
| 推向原生玩家 | `src/Chaite.Plugin/TerrariaFacade.cs:3487` | `SetControl(player,"controlDown",plan.Drop)` |

**缺陷**（`Runtime.cs:312-313` 原文）：

```csharp
plan.Drop = false;
plan.FeatherFallUp = false;
```

即：无论训练器发什么，`Drop` 与 `FeatherFallUp` **恒为 false** ⇒ 策略**永远无法**让角色按下下键
⇒ 翅膀的收翅下坠、缓降药水的下落改变、坐骑的下降**全都表达不出来**。
这可以单独解释"看得见威胁、有判断、却躲不掉"。

> **已修复并做引擎内 A/B 验证（2026-09-21）**：动作文件改为五列
> `tick,direction,up,down,dash`（四列旧格式仍可读），`RouteReplay` 增加两个位，
> `Runtime.cs` 改为 `plan.Drop = replayDown; plan.FeatherFallUp = replayUp;`。
> 验证方式是**唯一变量对照**：两臂只差动作文件的第 4 列，
> `down=0` 臂 `player.controlDown` 全 False，`down=1` 臂 2048 帧里 1605 帧 True；
> `up=1` 臂 `controlJump` 1698 帧 True、玩家高度 7958 → 4945。
> 详见 `verification-2026-09-21-training-structure.md` §3。

**为什么之前找不到**：`GameProbe.cs` 里唯一的动作文件读取者是
`ReadBridgeActionTick()`（L5059），它**只解析 tick**（L5063-5076），且在 L2193 只用于**锁步等待**。
探针通过 `GameProbe.cs:161` 托管 `Chaite.Plugin.Runtime`，真正的动作施加在**插件**里。
`GameProbe.cs:4161-4170`（`controlLeft/Right=false`、`controlJump=MotionJumpRequested()`、
`controlUp=FlightUpRequested()`）是 **Motion/Flight 夹具专用**，与战斗无关 —— 我先前在那里
反复找是找错了地方。

**修复方向**：动作格式改为 `tick,direction,up,down,dash`（5 字段，Python 侧已提交 `b9cf071`），
`RouteReplay` 解析并把 up/down 透传，`Runtime` 置 `plan.Drop=down`、`plan.FeatherFallUp=up`，
并**由 up 位驱动 `plan.Jump`**（泰拉瑞亚中上键与跳跃键都驱动 `controlJump`，单独的 jump 位冗余）。
4 字段旧行按 legacy `tick,direction,jump,dash` 处理（up/down 为 false），保证旧动作文件兼容。

（注：第八节引用的 `RouteReplay.cs:227` 在本次改动后位于 `RouteReplay.cs:291` `TryReadBridge(...)`。）

## 九、下键管线打通后的两个遗留问题（2026-09-21 隔离探针实测，均**未修**）

### 9.1 up=1 会被整体中和（较紧急）

隔离探针（`artifacts\game-probe-rt-dshup-0921-083013`，冻结动作行 `999999999,0,1,0,0`）：

* trace：`accepted tick=999999999 dir=0 jump=True up=True down=False dash=False`
* 36 行 `EngagedAlive` 全部 `plan.jump=true`、`plan.featherFallUp=true`、
  `actualAtApplyReturn.controlJump=true` ✓ —— **但** `actualAtApplyReturn.controlUp=false`，
  且帧末 `player.controlJump=false` ✗
* `Chaite\chaite.log` 反复出现：
  `mobility validation rejected at pre-frame=N, post-frame=N: feather-fall=up-input-changed; resolution=all-controls-neutral`

机制（`TerrariaFacade.cs:3941-3955`，其中 `:3954` 强制 `controlUp=false`；`:3936-3939` 已用
`_pendingFeatherFallRequiresPotionUp=true` 武装帧内复核）：`plan.FeatherFallUp` 为真时，若缓落
前提不成立就**强制 `controlUp=false`**，随后 `ValidatePendingMobility`（`:4006`）判定该帧无效，
`ResolveRejectedPendingMobility`（`:4177-4184`）→ `NeutralizePendingInput`（`:4186`）
**清空整个待处理帧** —— 于是**同一帧里的 down 位也会被一起丢掉**。

**后果（重要）**：用户 2026-09-21 裁定所有配装都带羽落药水，
所以这道门在**几乎每条配装**上都被武装 ⇒ 24 动作空间里的 `up=1` 动作**可能整帧失效**，
并连带吞掉同帧的下降意图。这不是外观问题，是动作空间的实质缺陷。

**对照**（证明问题特定于该门，而非管线本身）：

* `down=1`（`999999999,0,0,1,0`，运行目录 `artifacts\game-probe-rt-dshdown-0921-082403`）：
  `accepted … down=True`；39 行战斗行中 35 行
  `plan.drop=true` 且 `actualAtApplyReturn.controlDown=true`；pre-hit 窗口 **336/336** 行
  `player.playerDown=true`；存活期间**没有**任何 `down=1` 而 `controlDown=false` 的帧
  （只有接管前与死亡后两类，均非 down 专属门控）。`mountActive` 全程 false。
* 4 字段 legacy（`999999999,0,1,0`，运行目录 `artifacts\game-probe-rt-dshlegacy-0921-083040`
  —— 不是 `…-083013`，那是 up=1 的运行）：trace `accepted-legacy … jump=True up=False down=False`；
  36/36 行 `plan.jump=true`、`controlJump=true`（施加时与帧末**都是** true）、
  `controlUp=false`、`controlDown=false` ⇒ **legacy 路径仍能起跳**，且不设 up/down。

**待决策**（不要擅自改）：是否让回放通道的 up 绕过这道缓落前提门。保留该门是本项目的
fail-closed 纪律（不谎报机动前提）；但若保留，就需要另想办法让 `up=1` 不至于整帧作废。

### 9.2 被拒帧会飞规划器的兜底输入，而非路线的意图

`plan.LateMobilityFallback` 由 `CombatPlanner.cs:3432/3475` 打戳，而 `Runtime.cs` 的回放块
**没有清空它** ⇒ 一旦某帧被拒，该帧执行的是**规划器自己的**无边缘兜底（含它自己的
left/right/jump/down），**不是路线声明的那一组输入** ⇒ 路线保真度在那些帧上被静默破坏。
这也解释了 9.1 里"整帧被中和"之外的另一条隐性路径。**未修**。





