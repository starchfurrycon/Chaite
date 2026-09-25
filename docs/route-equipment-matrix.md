# 路线配装矩阵（用户 2026-09-16 指示）

> **2026-09-23 更新（当前口径，覆盖下文的历史记录）**：疾旋鼬路线
> （`fishron-trusty-chillet` 与 `fishron-trusty-chillet-ignis`，坐骑 64/65）
> **已整体删除**，理由见下面"舍弃"一节。猪鲨的已审路线现在是 **3 条**：
> `fishron-fairy-wing`、`fishron-strong-wing`、`fishron-lilith-wolf`。
> 下文各表按 2026-09-16 / 09-21 当时的事实保留，不再改写。

## 核心口径

**每套准入配装训练一个独立模型，因为对应的机动性不同。**

当前架构已经支持这一点，不需要改造：

* `FormulaRoute` 枚举每条路线一个值（`FormulaRouteCatalog.cs:3`）；
* `LearnedPolicy.ForRoute(route)` 按路线取各自的策略文件；
* 训练器用 `-PolicyRoute <route>` 指定本次训练哪一套，模型落在
  `artifacts/training/<tag>/params-best.txt`。

所以"一套配装一个模型"= 对每套配装各跑一次训练、各得一个参数文件。

## 猪鲨（Duke Fishron）— 4 套（第 4 套疾旋鼬已于 2026-09-23 删除，现为 3 套）

1. 莉莉丝项链坐骑 + 气球束 + 羽落药水增益
2. 仙灵翅膀及同级翅膀 + 蛙靴 + 克苏鲁之盾/闪避来源
3. 更高级的翅膀（如猪鲨翅膀）及同级翅膀 + 蛙靴 + 克苏鲁之盾/闪避来源
4. ~~可靠的疾旋鼬 + 多段跳饰品 + 羽落药水增益~~（**已删除**，见"舍弃"）

**猪鲨所有配装都要带狱火药水增益。**

## 场地限制

* 猪鲨：横向最大为**海洋群系宽度**的长平台

## 失败处理规则

若某套配装实在无法训练至极高胜率，**先检查是否为该配装漏写了某个影响机动的
因素**；若找不出原因则舍弃该套配装。

## 已确认口径（用户 2026-09-16 回答）

**最终要训练 4 个模型：猪鲨 4 套。**（2026-09-23 起为 **3 套**：疾旋鼬已删除。）

| # | Boss | 配装 | 路线 | 前置工作 |
| --- | --- | --- | --- | --- |
| 1 | 猪鲨 | 莉莉丝项链坐骑+气球束+羽落 | 待新建 | **需补齐坐骑准入 + 气球束/多段跳机动建模** |
| 2 | 猪鲨 | 仙灵翅膀及同级+蛙靴+盾 | `fishron-fairy-wing` | 无 |
| 3 | 猪鲨 | 高级翅膀（猪鲨翅膀）及同级+蛙靴+盾 | `fishron-strong-wing` | 无 |
| 4 | 猪鲨 | ~~疾旋鼬（64/65 合并）+多段跳饰品+羽落~~ | ~~`fishron-trusty-chillet`~~ | **已按用户 2026-09-23 指示删除**（机动性不足） |
| 5 | 猪鲨 | ~~史莱姆女士鞍~~ | ~~`fishron-queen-slime`~~ | **已按用户 2026-09-21 指示删除** |

**舍弃**：

* `fishron-queen-slime`（明胶女士鞍，坐骑 50）—— 使用者 2026-09-21 明确指示
  **直接从猪鲨路线中删去**，并要求同时删掉程序里的准入口径以免后续口径不一致。
  已从 `FormulaRoute` 枚举、`FormulaRouteCatalog`（`IsAcceptableMount` /
  `BelongsToBoss` / `Select`）、`FormulaMobilityContract.ExpectedMount`、
  `CombatPlanner` 分支、`FishronQueenSlimeScript.cs`（整文件删除）、
  探针的 `early-hardmode` 与 `post-plantera` 两个配装分支、探针的
  reviewed-route 白名单、`run-boss-validation.ps1` 的路线映射与 monitor 白名单、
  `start-isolated-test.ps1` 的参数校验、`RouteCatalogContractTests` 的钉死清单、
  `training/extend-sessions.ps1` 的 `qs13` 与 `training/sessions.json` 的
  `d12-qs1` **整体删除**。坐骑 50 现在选中 `None`，即该配装被拒绝而不是被驱动。
* `empress-rain-fishron`（雨天虾松露坐骑）—— 使用者 2026-09-18 明确**不在范围内**。
  已从 `FormulaRoute` 枚举、`FormulaRouteCatalog`、规划器分支、移动契约、
  三份工具门禁与契约测试中**整体删除**；坐骑 12 现在选中 `None`，
  即该配装被拒绝而不是被驱动。
* `fishron-trusty-chillet` / `fishron-trusty-chillet-ignis`（可靠的疾旋鼬及其换皮，
  坐骑 64/65）—— 使用者 2026-09-23 明确指示**直接删去**：三层平台竞技场的大师难度
  挂载彩排实测疾旋鼬只能爬升 **38 格**，而莉莉丝狼能爬 **70 格**，平台行距 **60 格**，
  连第一层都上不去，机动性确实不足。2026-09-21 曾把换皮 65 合并进 64，现两者一起删除。
  已从 `FormulaRoute` 枚举、`FormulaRouteCatalog`（`IsAcceptableMount` / `BelongsToBoss` /
  `Select`）、`FormulaMobilityContract.ExpectedMount`、`CombatPlanner` 的脚本分支、
  `FishronChilletScript.cs`（整文件删除）、插件里坐骑冲刺的待校验分支与其字段、
  探针的 reviewed-route 白名单与 `early-hardmode` / `post-plantera` 两个配装分支、
  `run-boss-validation.ps1` 的路线映射与 monitor 白名单、`start-isolated-test.ps1`
  的参数校验、`RouteCatalogContractTests` 的钉死清单**整体删除**。坐骑 64/65 现在
  选中 `None`，即该配装被拒绝而不是被驱动。`VanillaMountCatalog` 的坐骑条目与
  `MobilitySnapshot` 的坐骑字段是游戏数据与位置式观测布局，**保留不动**。
* `fishron-trusty-chillet-ignis`（坐骑 65）—— 2026-09-21 用户明确说明 64 与 65
  **只是换皮变种，只需训练一套**，因此当时合并到 `fishron-trusty-chillet`；
  两者已于 2026-09-23 随该路线一并删除（见上一条）。

所有猪鲨配装均带狱火药水增益（狱火已建模，见上表）。

## 与代码现状的对照

| 用户配装 | 代码路线 | 状态 |
| --- | --- | --- |
| 猪鲨 2 仙灵翅膀+蛙靴+盾 | `fishron-fairy-wing` | 已支持 |
| 猪鲨 3 高级翅膀+蛙靴+盾 | `fishron-strong-wing` | 已支持 |
| 猪鲨 4 ~~疾旋鼬+多段跳+羽落~~ | ~~`fishron-trusty-chillet`(mount 64)~~ | **已删除**：使用者 2026-09-23 指示删去（爬升 38 格 vs 莉莉丝狼 70 格，平台行距 60 格） |
| ~~猪鲨 4（同上）~~ | ~~`fishron-trusty-chillet-ignis`(mount 65)~~ | **已删除**：与 64 一起删去 |
| 猪鲨 1 莉莉丝项链+气球束+羽落 | `fishron-lilith-wolf`(mount 52) | **已支持**（2026-09-21 补齐 `Select` 的 mount 52 准入） |
| ~~猪鲨 史莱姆女士鞍~~ | ~~`fishron-queen-slime`(mount 50)~~ | **已删除**：使用者 2026-09-21 指示删去，代码与门禁中已不存在 |
| ~~`empress-rain-fishron`(mount 12+雨)~~ | ~~同左~~ | **已删除**：使用者定案不在范围内，代码与门禁中已不存在 |

### 已建模 vs 未建模的机动/增益因素

在 `src/` 中实际检索的结果：

| 因素 | 代码中 | 说明 |
| --- | --- | --- |
| 克苏鲁之盾 3097 | 已建模 | `FormulaMobilityContract.ShieldOfCthulhuItem` |
| 忍者大师装备 984 | 已建模 | `FormulaMobilityContract.MasterNinjaGearItem` |
| 狱火药水 2348 | 已建模 | `FishronThreatCatalog`，用于摧毁泡泡，含储备量与刷新时钟 |
| 羽落药水 | 已建模 | `FlightMotion.cs` 只授权其 Up 分支 |
| 气球束 | **未建模** | 全仓库无 `Balloon` |
| 多段跳饰品（云瓶系列） | **未建模** | 全仓库无 `MultiJump`/`CloudInABottle` 等；只有抓钩的 `RefreshesDoubleJumps` |
| 莉莉丝项链坐骑 | **未建模** | 全仓库无 `Lilith`；`Select` 遇到未知 mount 直接判为不支持 |

这三项"未建模"正好落在用户说的"影响机动的因素"上，因此**猪鲨第 1 套当前
不是"训练不出来"，而是根本无法进入训练**（`Select` 返回 `None`，路线被拒）。

---

## 更正（2026-09-16，来自外部核实）

本节不修改上面的原始记录，只追加按来源核实后的更正。凡与上文冲突处，以本节为准。

### C1. 「忍者大师装备拥有更好的无敌帧机制，因此不可替换」——不成立

来源：[Invincibility frame](https://terraria.wiki.gg/wiki/Invincibility_frame) /
[Dodge](https://terraria.wiki.gg/wiki/Dodge) / [Shield of Cthulhu](https://terraria.wiki.gg/wiki/Shield_of_Cthulhu)（wiki 引用 1.4.4.9 / 1.4.5.5 / 1.4.5.8 源码）。

- 无敌帧分两类：**dodge 类**走 `SetImmuneTimeForAllTypes()`，同时设置 `immuneTime`
  与每一个 `hurtCooldowns[i]`，因此覆盖 **Group 2（月总）**；**冲刺命中类**走
  `GiveImmuneTimeForCollisionAttack()`，**只设 `immuneTime`（= Group 5）**，
  挡不住 Group 2。
- **MNG / Tabi 的冲刺属于 `dashType == 1`，源码分支里没有任何 i-frame 调用**
  ⇒「用 MNG 冲刺吃无敌帧」这个操作**不存在**。能吃冲刺无敌帧的只有克苏鲁之盾（4 ticks）、
  Chillet 系（10 ticks）、日耀（4 ticks），而**这三者都只给 Group 5**。
- MNG 的无敌帧来自**随机 1/10 dodge**，时长 **80 ticks**（配十字项链 120），且无冷却。
  但 **黑腰带 / 大脑of混乱 / 神圣保护 / 暗影躲避的时长完全相同（80 ticks）**，
  差别只在触发条件与锁：大脑of混乱 **1/6 更高**但有 4 秒锁；神圣保护 100% 但消耗 buff。
  **不存在严格支配关系。**
- 1.4.5.7 新增 **Mystic Arts Sash**：同为 **1/10**，**优先级高于 MNG**，
  且可与 MNG 同时装备（19%）。⇒ MNG 甚至已不是最优 dodge 件。

**对 Chaite 的结论**：MNG 是**单槽综合价值最高**的 dodge 型饰品，但它的无敌帧是随机 proc、
与冲刺无关、且非严格最优。**不应让任何 dodge 类成为承重部件**——目标是零命中，
随机 proc 在"零命中"目标下价值为零。

> **使用者 2026-09-18 定案（更正本节旧结论）**：旧文写"无伤社区亦明确把'靠 dodge 吃掉一次伤害'
> 算作被打中"，并据此把所有无敌帧都视为违规。使用者已明确：**冲刺的无敌帧是合法资源**。
> 本项目的验收口径是 `hits`（生命下降帧），因此**无敌帧生效的那一帧不算受击**，
> 靠它穿过射弹或本体是合法操作，不得再被标记为违规。
> 上面"MNG 不适合做承重部件"的理由随之改为**只保留"随机 proc 不可依赖"这一条**，
> 与"无敌帧是否合法"无关。

### C3. 疾旋鼬坐骑不是飞行坐骑

**Chillet / Chillet Ignis / Trusty Chillet / Trusty Chillet Ignis = 坐骑 ID 62/63/64/65**，
**1.4.5.0 Palworld 联动**内容，来自 Cavern 层 Huge Dragon Egg。
它们是**地面坐骑 + 冲刺**：**没有飞行、没有悬停**；骑乘时伤害 +10%、坠落伤害 -50%。
**1.4.5.7 刚被削弱**：速度 46 -> 33 mph、jump reach 8.81 -> 7.85。
⇒ `fishron-trusty-chillet` 路线的机动性是**地面**的，与翅膀路线不可类比。
（该路线已于 2026-09-23 按使用者指示删除；坐骑 62–65 本身仍是游戏数据。）
wiki 从未把它与翅膀做机动性比较。

### C4. Lilith's Necklace 的准确定位

- **不是** Don't Starve Together / Deerclops 联动内容。**1.4.4 新增**，
  由 **Wolf 敌人掉落**（1/30，专家 1/25），名称来自 Redigit 与 Cenx 的女儿。
- **物品 5130 / 坐骑 52 / 增益 342**，与 `VanillaMountCatalog` 的 `E(52, 5130, 342, "wolf")` 一致。
- 狼形态：3 格高、48 mph、加速快、**坠落伤害 -90%**；**不能飞、不能二段跳**；
  **不能使用翅膀 / 靴子 / 飞毯，但可以使用全部附加跳饰品**。
- ⇒ 这正是「莉莉丝项链 + 气球束 + 羽落药水」这套配装的机动来源：**多段跳 + 全局重力修改**。

### C5. 多段跳的真实叠加规则（建模必须按这个）

- **按类型划分，同类型不叠加、不同类型叠加。** 五种类型：
  Cloud / Blizzard / Sandstorm / Fart / Tsunami（另有 4 个坐骑自带跳）。
  粒子顺序：坐骑跳 -> Sandstorm -> Blizzard -> Fart -> Tsunami -> Cloud。
- **Shiny Red Balloon 本身不提供附加跳**，只给 +33% 跳跃时长、+30% 跳跃速度。
- **Bundle of Balloons（物品 1164）= 四段跳 = 基础跳 + 3 次附加跳**，
  顺序 **Sandstorm -> Blizzard -> Cloud**；配方为
  Cloud in a Balloon + Blizzard in a Balloon + Sandstorm in a Balloon。
  **不含 Fart（正确名称是 Fart in a Jar），Fart 也不是材料**；
  **不与自身材料叠加**；**不免疫摔落伤害**（Bundle of Horseshoe Balloons 才免疫）。
- 最大叠加可达 **七段跳**：Bundle(3) + Fart in a Jar(1) + Tsunami(1) + 坐骑(1)。
- **与翅膀的交互**：按住跳跃键会**不消耗附加跳**直接进入飞行；轻按则消耗一次附加跳。

### C6. 羽落药水建模是错的（代码缺陷）

来源：[Featherfall Potion](https://terraria.wiki.gg/wiki/Featherfall_Potion)。

真实机制是**三档**，而不是「可选的上键分支」：

| 输入 | 重力 | 最大下落速度 |
|---|---|---|
| **默认（不按键）** | **1/3** | **1/3** |
| 按住 Down | 100%（等于无效果） | 100% |
| 按住 Up | **10%** | **10%** |

- **buff 生效时"1/3 重力"是常态基线**，Up 只是压到 10%，Down 是取消。
  `src/Chaite.Core/FlightMotion.cs:28` 只授权了 Up 分支，
  **等于把绝大多数时间的常态排除在模型之外**。
- 它修改的是**全局重力**，因此**同时改变跳跃高度**：重力 0.4 -> reach 6.27 tiles，
  重力 0.133（羽落）-> reach 约 21.2 tiles（约 3.4 倍）。
  所以"羽落只改下落速度"也是错的。
- 基线常量：默认重力 **0.4 tiles/tick^2**、最大下落速度 **10 tiles/tick**。
- 与坐骑和矿车同时有效（唯一例外：Honeyed Goggles / Bee Mount）；与 Djinn's Curse 完全等价，
  **两者不能叠加**。
- 1.4.4 / 1.4.5.x 对羽落机制**零改动**。

### C7. 其它影响脚本设计的核实结果

- **猪鲨 enrage 是几何边界盒判定，不是生物群系判定**：距真实世界边缘 <= 400 tiles
  **且** 距世界顶部 > 50 tiles **且** 位于 underground 层之上。
  ⇒「飞太高」和「往下钻到 underground」都会 enrage，"飞高躲"被禁。
  「1.4.4 改了 enrage 触发条件」是误传——触发自 1.3.0.1 未变，1.4.4 只强化了 enraged 形态。
- **猪鲨 P3 传送规律**：传送去的那一侧**永远是上一次冲刺方向的相反侧**；
  进入 P3 后的**第一次冲刺不带传送**。P3 本体隐形，只能背板数冲刺。
- **1.4.5.7 收窄了 Everlasting Rainbow 的判定范围**
  （"can no longer hit players on most faded out sections"）
  ⇒ 任何基于 1.4.4 帧数的弹幕 hitbox 表都需要在 1.4.5.8 上重测。
- **tModLoader 目前仍在 1.4.4**（[issue #5070](https://github.com/tModLoader/tModLoader/issues/5070)）
  ⇒ 1.4.5.8 上**没有任何 tModLoader 系工具可用**。
  所以"不用 tModLoader mod"在 1.4.5.8 上不是取舍，而是唯一可能。

### C8. 可参照的社区需求规格

[Nycro's Nohit Efficiency Mod](https://www.terraria-game.com/en/mody/nycros-nohit-efficiency-mod-dlya-terrarii/)
（Steam Workshop 2819498996，<=1.4.4，跑不了 1.4.5.8）是社区 hitless 练习的事实标准，
其功能清单可直接当作 Chaite 的 episode 重置 / 统计需求规格：
受伤即死、关闭 boss 与弹幕生成、关闭天气与事件、自动清理掉落物、
**每次尝试统计（第几次尝试 / 战斗时长 / 结束时 boss 血量）**、玩家头顶尝试计数。
