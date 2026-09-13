# Terraria 1.4.5.8 原版冲刺来源与能力门槛审计

## 证据边界

- 目标仅为 Windows Steam 原版 Terraria `1.4.5.8` x86。
- 静态读取的 `Terraria.exe` SHA-256 为
  `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。
- 原生逐 tick 语义来自 Mono.Cecil 对本机 IL 的只读检查。没有启动游戏、加载
  Terraria 程序集或修改游戏/存档。
- Wiki 仅交叉核对玩家可见来源和获取时期；运动常数、输入边沿和分支优先采用
  上述白名单程序集。配套脚本为 `tools/audit-native-dash-sources.ps1`。

这里的装备名只是某条运动实现的**来源证书**。Boss 下限仍应写成运动能力阈值，
不能写成指定配装；同样也不能把几个尚未闭环的来源拼成一个虚构的能力包。

## 穷举结果与来源身份

程序集内共有 9 个 `Player.dashType` 写点。写入值的闭集只有
`0/1/2/3/5/6`；`dashType = 4` 没有任何原版赋值来源。类型 4 在
`DashMovement` 中只剩活动阶段的粒子和衰减遗留分支，也没有启动分支，必须视为
不可达，而不是隐藏的可用冲刺。

| 原生来源证书 | 身份 | `dashType` | 可获得时期 | 当前能否满足生产门槛 |
|---|---|---:|---|---|
| Tabi | item `977` | 1 | 花后地牢 Bone Lee | 否；只有原生证据，没有完整预测器 |
| Master Ninja Gear | item `984` | 1 | Tabi 等材料合成，因此通常花后 | 否；与 Tabi 同运动，但身份仍须精确读取 |
| Shield of Cthulhu | item `3097` | 2 | 肉前；专家/大师克眼宝藏袋取得 | 是；目前唯一已有完整纯运动合同的水平冲刺证书 |
| Solar Flare 完整套装 | items `2763/2764/2765` | 3 | 月亮事件后段/终局 | 否；层数、碰撞消耗和回程尚未全接入 |
| Crystal Assassin 完整套装 | items `4982/4983/4984` | 5 | 肉后早期，史莱姆皇后 | 否；尚无完整预测器 |
| Chillet / Ignis | mount `62/63`，items `5665/5666` | 6 | 肉前，Huge Dragon Egg | 否；必须使用坐骑联合模型 |
| Trusty Chillet / Ignis | mount `64/65`，items `6150/6151` | 6 | 肉后合成升级 | 否；必须使用坐骑联合模型 |
| Ram Rune 下砸 | item `5465` | 不使用 `dashType` | 肉前骷髅王后，地牢上锁金箱/钓鱼锁盒 | 不能满足水平冲刺门槛；只可作为独立纵向候选 |

克盾的 `Item.SetDefaults` 只设置 `expert = true`，没有设置
`expertOnly = true`。因此它的**取得**受专家/大师限制，但被带进经典世界后仍能工作；
不能把“专家掉落”错误解释成“经典世界冲刺失效”。

`MountID.Sets.CanDash` 的集合为 `56..65`，但只有 `62..65` 会主动把
`dashType` 改为 6：

- mount `56`（Bat）、`57..60`（Roller Skates）和 `61`（Pixie）只允许
  已有饰品/套装冲刺在骑乘时继续执行，不是冲刺来源；
- mount `62..65` 会覆盖本帧其他来源并使用 Chillet 合同；
- `coldDash`、`sailDash`、`desertDash` 只是跑鞋视觉状态，不是可启动冲刺。

## 共用水平冲刺输入状态机

`DoCommonDashHandle` 对类型 `1/2/3/5/6` 共用下列边沿：

1. 只有设置允许双击时，左右首次按下才把 `dashTime` 置为 `+15/-15`；计时窗内
   第二次同向按下启动。
2. 专用 Dash 键要求 `controlDash && !CCed && releaseDash`。方向默认采用面向；
   只有水平输入恰好指向面向反方向时才改用该方向。无水平键、同向键或左右同时按
   都沿面向启动。
3. 成功启动会清空 `dashTime` 和 `timeSinceLastDashStarted`；由专用键启动时还会
   消耗 `releaseDash`。必须至少复制一帧 `Dash=false` 才能重新武装该边沿。
4. `dashDelay == 0` 时才把聚合的 `dashType` 复制进活动 `dash`。
   `dashDelay == -1` 表示活动冲刺，正数表示冷却。
5. 冷却从 1 降至 0 的那次 `DashMovement` 会立即返回，下一次更新才可重新启动。
6. 五种启动分支都使用相同的两个前方 `SolidOrSlopedTile` 探针；任一探针阻挡会
   将初始 X 速度减半。探针未知时不得猜测“净空”。

## 运动、接触与资源语义

| 类型 | 初始 X 速度 | 高速阈值与每 tick 衰减 | 跑速以上衰减 | 自然冷却 |
|---:|---:|---|---:|---:|
| 1 Tabi / MNG | 16.9 | `abs(vx) > 12` 时 `vx *= .992` | `vx *= .96` | 20 tick |
| 2 克盾 | 14.5 | `abs(vx) > 12` 时 `vx *= .985` | `vx *= .94` | 30 tick |
| 3 日耀 | 21.9 | `abs(vx) > 14` 时 `vx *= .985` | `vx *= .94` | 20 tick |
| 5 晶塔刺客 | 16.9 | `abs(vx) > 12` 时 `vx *= .992` | `vx *= .96` | 20 tick |
| 6 Chillet | 16 | `abs(vx) > 12` 时 `vx *= .992` | `vx *= .96` | 20 tick |

“跑速以上”的下界是 `max(accRunSpeed, maxRunSpeed)`。速度降到该值后，活动冲刺
结束并进入冷却；所以预测器必须读本帧真实跑速，不能只保存上表常量。

接触差异也不能合并：

- type 1/5 没有冲刺专用伤害或免疫；玩家仍按普通 NPC 接触规则受击。
- 克盾启动 `eocDash = 15`。有效命中造成 `30 * meleeDamage`，随后
  `eocDash = 10`、`dashDelay = 30`、`vx = -9 * hitDirection`、`vy = -4`，
  并给玩家 4 tick 碰撞免疫。`oldStyleParkour` 会改变多目标命中与反弹写入时序；
  它对应 Guide to Old World Parkour（active item `6190`，inactive item `6195`）
  的联合状态，不是一个可忽略的内部布尔。当前生产合同正确地对潜在或未知接触
  失败关闭。
- 日耀命中造成 `150 * meleeDamage`，目标 immunity 6、玩家碰撞免疫 4；不反弹。
  一次冲刺第一次有效命中才消耗一层盾，同次冲刺仍可撞击其他目标。
- Chillet 攻击框向前额外延伸 60 px；mount `62/63` 造成
  `32 * minionDamage`，`64/65` 造成 `150 * minionDamage`，目标 immunity 20、
  玩家碰撞免疫 10，不反弹。玩家骑乘高度为 46 px，不能用步行 20×42 hitbox
  做扫掠。

Chillet 另有不可移植到其他 dash 的状态：地面启动令 `vy = -4`，正在下落时把
`vy` 减半；活动时转向或丢失 `dashType == 6` 会立即令 `dashDelay = 30`、
`vx *= .3`、`dashTime = 0`。mount `62/63` 的 run/dash/jumpHeight/jumpSpeed 为
`3/6.5/6/8.01`，`64/65` 为 `3/9/11/8.1`。

日耀资源合同如下：

- `solarCounter` 每 tick 加一，每 180 tick 恢复一层，最多三层；
- 有盾层时才能开始新冲刺；已启动且 `solarDashing && dashDelay < 0` 时，即使资源
  随后变化，仍保持 type 3；
- 启动仅设置 `solarDashing=true` 并清“本次已消耗”标记，第一次有效 NPC 碰撞
  才消耗盾层；
- 作为能力证书必须同时读取精确套装、`solarShields/solarCounter`、活动冲刺和本次
  是否已经消耗，不能只读最终 `dashType`。

## Ram Rune 是独立的纵向转换

Ram Rune 的启动条件是：空中、未骑乘、当前未下砸、`controlDown`、
`velocity.Y != 0`，且 Jump 释放边沿可用。它由 Jump 输入启动，而不是 Dash 输入：

- 启动令 `isPerformingJump_DownDash = true`、`vy = 16 * gravDir`；倒置重力时因此
  向上冲向天花板。
- 活动状态禁用翅膀和火箭靴；召唤坐骑会取消。已附着钩爪进入
  `GrappleMovement -> RefreshMovementAbilities -> RefreshDoubleJumps`，也会取消。
  二段跳同样可取消。水平 dash 或击退可改变 X 轨迹，但不会取消下砸。
- 活动下砸绕过羽落药水的慢落分支；湿水时本帧重力和最大落速各乘 `.85`。
- 每个活动更新增加 `downDashTime`。着陆后的下一次更新先触发范围攻击，再由
  `RefreshDoubleJumps` 复位；普通半径 128 px、基础伤害 30、raw knockback 10，
  `32 * downDashTime > 300`（整数计时至少 10 tick）时半径 176 px、必暴击、raw
  knockback 20。当前 Wiki 将强化描述为“10 tiles / 最终 knockback 24”，与本机
  1.4.5.8 的 raw IL 常数并不完全等价，生产预测必须采用 IL 计时和最终伤害管线，
  不能直接抄可见文案。
- 普通重力方向的空中直接撞击框位于脚下，基础伤害 `40 * minionDamage`、
  knockback 5、目标 immunity 10、玩家碰撞免疫 6，且不会反弹。这个分支硬编码
  `velocity.Y > 0`，因此倒置重力向上的下砸没有对称的直接撞击伤害证据。
- 着陆范围攻击免疫坠落伤害；下砸额外执行一次 X 速度清零的纵向
  `DryCollision`，然后恢复 X 速度。它不能冒充水平
  `CertifiedDashWithBrakedReturn`。

## 来源优先级与不可拼接规则

原版每帧先重置并聚合功能饰品，再应用完整护甲套装，最后在
`DashMovement` 入口处理 Chillet：

1. 功能槽 `3..9` 按升序应用；同时装备 Tabi/MNG/克盾时，最后处理的来源覆盖
   单一 `dashType`。不能用槽内存在性猜实际活动类型。
2. Crystal Assassin 套装随后固定覆盖为 type 5。日耀只有在有盾层或已在活动冲刺
   时覆盖为 type 3；零层且非活动时，先前饰品来源仍可能保留。
3. 活动 mount `62..65` 在 `DashMovement` 入口最终覆盖为 type 6。
4. type `dash` 一旦在 `dashDelay == 0` 时锁定，整条活动/冷却轨迹必须跟随同一
   来源状态机；不得从另一装备借用速度、冷却、免疫或反弹。

抓钩和坐骑还会改变输出窗口：已附着抓钩走独立 `GrappleMovement`，普通
`DashMovement` 不执行，速度由锚点合力改写；释放/归还钩爪并重新观测稳定状态后
才能恢复正常瞄准闭环。Chillet 则改变玩家 hitbox、垂直速度和转向制动。任何救援
候选都必须包含“启动—完整扫掠—解除/制动—回到原 Boss 锚区—恢复武器视线”整段，
不能在普通射击计划末尾临时追加一个 Hook/Mount/Dash/Up 键。

羽落药水也是轨迹状态，而不是免费增益：未按 Down 时自然下落变慢，按 Down 才
绕过慢落；这会改变低配模板原本的落点和开火提前量。水平 dash 本身仍是水平的，
但 Chillet 的 `vy`、重力方向探针及回程必须连同羽落/重力状态一起预测。

## 能力门槛与可接入闭环

当前 Boss 能力枚举中，专家/大师猪鲨要求
`CertifiedDashWithBrakedReturn`；这是概念门槛，不等于“必须穿克盾”。一个来源只有
同时证明下列条件，才能签发该证书：

- 本帧精确身份、输入释放边沿和 ready 状态已知；
- 初速、衰减、冷却、前方两个物块探针和本帧玩家 hitbox 均可预测；
- 整段轨迹对所有已知 Boss/NPC/弹幕/物块安全，或接触结果已有来源专用模型；
- 冲刺后的制动距离、冷却期间移动及返回锚区路径成立；
- 结束时重新取得该武器所需的视线、射程和稳定瞄准窗口。

因此当前映射为：

- 克盾的**无接触完整轨迹**可满足该门槛，也是当前唯一生产实现；
- Tabi/MNG 和 Crystal Assassin 的纯运动参数足够成为未来候选，但在来源专用预测器
  和回程测试完成前不能自动取得证书；
- 日耀和 Chillet 理论上可由各自完整联合状态机取得同一概念证书，但不得复用克盾
  的碰撞/资源/hitbox 合同；
- Ram Rune 只可能成为未来的“快速受控下降并闭环”可选能力，不能替代水平 burst
  或持续可控飞行下限；
- 所有可选能力只在普通低配轨迹失败时扩展，不能抬高或暗中替换 Boss 开战底线。

## 最小实现与测试路线

不改 Boss/输出层即可先合入本审计及脚本。后续生产接入应按来源逐个完成：

1. 在装备、Buff、套装和坐骑聚合完成后的原生位置读取精确来源集合、最终
   `dashType/dash/dashDelay`、输入 release 边沿、资源与联合状态；未知字段失败关闭。
2. 给 type 1/5、日耀、Chillet、Ram Rune 分别增加纯状态预测器；共享的只能是已证明
   相同的输入边沿与物块探针，接触/资源/制动必须保留各自分支。
3. 每个预测器只输出完整路线证书，由现有 measured route selector 依据能力阈值
   选择；不得把 dormant flight、hook、mount、dash 的字段并集成一条路线。
4. 先加入逐来源的启动、衰减边界、冷却 1→0、阻挡减半、接触失败关闭、解除及
   回锚测试，再为 Boss 增加候选。随机/未知威胁、状态身份冲突和读取失败都应保留
   普通移动/瞄准/开火计划，而只剥离该可选输入。
5. 联合测试至少覆盖：羽落+自然下落/Down 绕过、重力反转+Ram Rune、
   CanDash mount+饰品、Chillet 覆盖饰品、抓钩附着期间禁普通 dash、
   Old World Parkour+克盾，以及各自解除后恢复稳定武器窗口。

## 外部交叉验证

- [Dash](https://terraria.wiki.gg/wiki/Dash)
- [Shield of Cthulhu](https://terraria.wiki.gg/wiki/Shield_of_Cthulhu)
- [Expert Mode](https://terraria.wiki.gg/wiki/Expert_Mode)
- [Tabi](https://terraria.wiki.gg/wiki/Tabi)
- [Master Ninja Gear](https://terraria.wiki.gg/wiki/Master_Ninja_Gear)
- [Crystal Assassin armor](https://terraria.wiki.gg/wiki/Crystal_Assassin_armor)
- [Solar Flare armor](https://terraria.wiki.gg/wiki/Solar_Flare_armor)
- [Chillet](https://terraria.wiki.gg/wiki/Chillet)
- [Ram Rune](https://terraria.wiki.gg/wiki/Ram_Rune)
- [Guide to Old World Parkour](https://terraria.wiki.gg/wiki/Guide_to_Old_World_Parkour)

## 仍缺证据

- type 1/3/5/6 与 Ram Rune 尚没有生产读取器、来源专用完整轨迹模型和 Boss 闭环接线。
- 日耀资源、Chillet hitbox/攻击接触、Ram Rune 最终伤害/击退管线尚未形成可执行的
  纯模型；Wiki 的可见数值不能替代该工作。
- 尚未为上述新来源做真实 Boss 回放或胜率声明。本审计只证明白名单版本的原生状态
  机，不证明任一尚未接入策略的 90% 胜率。
- 下一 Terraria 版本必须先更新白名单、重新静态审计并审阅差异；脚本会拒绝在未知
  二进制上沿用这些结论。
