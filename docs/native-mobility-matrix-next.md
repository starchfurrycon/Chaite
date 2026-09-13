# 原版 1.4.5.8 可选机动能力矩阵（开发中）

范围：Windows Steam 原版 Terraria `1.4.5.8` x86，`Terraria.exe`
SHA-256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。
本页区分“原版确实存在的能力”和“拆特已经具备可发出输入的完整轨迹合同”。
读到一个聚合布尔值并不授权按键；没有相应合同的来源必须失败关闭，也不能让低于
Boss 基线的配置通过开战门槛。

## 当前身份边界

| 能力 | 原版精确来源 | 原版聚合状态 | 生产授权状态 |
|---|---|---|---|
| 克盾冲刺 | 功能槽物品 `3097` | `dashType == 2` | 仅精确克盾合同；需要 `dashDelay == 0`、专用 Dash 释放边沿、前方物块探针、全威胁轨迹和返回原闭环证明 |
| 忍者冲刺 | Tabi `977` 或 Master Ninja Gear `984` | `dashType == 1` | 尚未授权；不能套用克盾速度、冷却或接触反弹 |
| 晶塔刺客冲刺 | 头/身/腿 `4982/4983/4984` 完整套装 | `dashType == 5` | 尚未授权；身份来自 `ArmorSetBonuses.Benefits.CrystalAssassin`，不能只看最终 `dashType` |
| 日耀冲刺 | 头/身/腿 `2763/2764/2765` 完整套装及日耀充能 | `dashType == 3` | 尚未授权；必须另外跟踪三层资源及碰撞消耗 |
| Chillet 冲刺坐骑 | 物品 `5665/5666/6150/6151`，坐骑类型 `62..65` | `DashMovement` 临时令 `dashType == 6` | 尚未授权；四种坐骑、跃起、提前转向制动、接触伤害和 30 tick 冷却需独立合同 |
| 扫帚飞行坐骑 | Witch's Broom 物品 `4444`、坐骑 `23`、Buff `230` | 原版坐骑 hover | 纯运动与完整救援轨迹合同已存在，但没有生产 Boss 接线；当前不得发坐骑键，未来接线仍须逐 tick 20×42 扫掠、制动、卸载和返回闭环全部通过 |
| 普通抓钩 | Grappling Hook 物品 `84`、projectile `13`、`aiStyle == 7` | `grapCount/grappling[]` | 读取器、纯运动、路线证书与控制器已存在，但没有生产 Boss 接线；当前活动抓钩中性归还，计划锚点或“已发射”不能冒充真实附着 |
| 重力药水 | 活动 Buff `18`，且 `gravControl == true` | `gravDir`、`releaseUp` | 只在精确 Buff 身份、无强制重力/坐骑/钩爪冲突且完整翻转后回程轨迹成立时授权；上下两个方向都使用单帧 Up 边沿 |
| 重力球 | 功能槽物品 `1131`，`gravControl2 == true` | 与药水共享 Up 边沿 | 尚未授权为药水替代品；独立身份和分支 |
| 羽落药水 | 活动 Buff `8` | `slowFall == true` | 候选轨迹需同时验证精确 Buff 身份；无方向为 `gravity/3`，Up 为 `gravity/10`（保留 `/5` 触发和 `/10` 回落区间），Down 绕过羽落 |
| Djinn's Curse 羽落 | 装备物品 `3770` | 同样写 `slowFall` | 尚未建立装备身份合同，不能把它误报为药水 |

## 原生证据摘要

- `ApplyEquipFunctional` 把 `977/984` 写为 `dashType=1`、`3097` 写为
  `dashType=2`、`1131` 写为 `gravControl2=true`。
- `ArmorSetBonuses.Benefits.CrystalAssassin` 在完整套装回调中写
  `dashType=5`；`ApplySetBonus_Solar` 写 `dashType=3`。
- `DashMovement` 仅在坐骑类型 `62..65` 活动时临时写 `dashType=6`。
  这些分支的初速、衰减、接触结果和冷却不同，不能共用一个“冲刺”近似。
- `UpdateBuffs` 的 Buff `8` 写 `slowFall=true`，Buff `18` 写
  `gravControl=true`。但物品 `3770` 和 `CarpetMovement` 也会写
  `slowFall`，所以单看最终字段不能证明羽落药水身份。
- 原版 `Featherfall Potion` 为物品 `295`、Buff `8`、`buffTime=36000`；
  `Gravitation Potion` 为物品 `305`、Buff `18`、`buffTime=10800`。
- 官方社区 Wiki 的 `Dash`、`Crystal Assassin armor` 与 `Chillet` 页面用于核对
  玩家可见能力和 1.4.5 历史；所有逐 tick 常数与分支仍以本机白名单程序集为准。

## 接入规则

1. 普通低配候选先独立求解；仅当其预测不安全时才展开额外能力。
2. 能力候选必须包含启动、完整运动、全部威胁/场地碰撞、制动或解除，以及回到
   原 Boss 稳定闭环的数值轨迹；不能在评分结束后追加 Hook/Mount/Dash/Up。
3. 每个输入边沿都须在原版本帧装备/Buff 聚合后再次验证。验证失败只剥离该能力
   输入，不改变已选普通移动、跳跃、瞄准或射击。
4. 不能把可选能力计入低配开战门槛；低于对应 Boss 的场地、基础机动或输出底线时，
   必须在消耗召唤物前拒绝。更好装备只能提高已合格配置的安全余量。
5. 组合能力默认不闭包。扫帚+钩爪、坐骑+饰品冲刺、重力+羽落等组合只有建立新的
   联合状态机和原生微测后才可启用。
