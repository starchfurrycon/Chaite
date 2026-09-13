# 武器与弹药适配范围

主弹道目录包含 **885 个生产候选精确输出组合**：22 把枪械 × 15 种实际子弹（330 组；Pew-matic Horn 显式固定输出 968 号弹体）、Snowball Cannon × Snowball（1 组）、2 把星星炮 × 陨星（2 组）、512 个弓箭组合、4 把飞镖武器 × 5 种实际飞镖（20 组），以及 20 件不使用弹药的法术武器（含 Diamond Staff）。另有独立的 **15 个非线性近战投射物实验画像**（均为无弹药路线），所以源码审查记录合计 **900 个**；这 15 个实验画像尚未取得 Boss 闭环命中范围与原生攻击节奏证书，生产输出准入主动拒绝，且自动选枪将其记为零分。Rocket Launcher＋Rocket I 使用目录外的精确生产路线，不纳入 885 个主目录组合。这里没有按 `shoot > 0`、伤害类型或单个 `shootSpeed` 猜测通用兼容层；武器 ID、实际弹药 ID、最终弹幕 ID、资源、射击时序或弹道证据有任一项不符，就会拒绝自动开火。

| 类别 | 已审查的原版 1.4.5.8 物品 ID |
|---|---|
| 枪械（22） | 95 Flintlock Pistol；96 Musket；98 Minishark；164 Handgun；219 Phoenix Blaster；434 Clockwork Assault Rifle；533 Megashark；534 Shotgun；679 Tactical Shotgun；800 The Undertaker；964 Boomstick；1254 Sniper Rifle；1255 Venus Magnum；1265 Uzi；1553 S.D.M.G.；1870 Red Ryder；1929 Chain Gun；2269 Revolver；2270 Gatligator；3788 Onyx Blaster；4703 Quad-Barrel Shotgun；5117 Pew-matic Horn（固定 968 弹体） |
| 雪弹枪械（1） | 1319 Snowball Cannon（949 Snowball，固定 166 弹体；仅纳入原生直线前缀） |
| 子弹（15） | 97 Musket Ball；234 Meteor Shot；278 Silver Bullet；515 Crystal Bullet；546 Cursed Bullet；1302 High Velocity Bullet；1335 Ichor Bullet；1342 Venom Bullet；1349 Party Bullet；1350 Nano Bullet；1351 Exploding Bullet；1352 Golden Bullet；3104 Endless Musket Pouch；3567 Luminite Bullet；4915 Tungsten Bullet |
| 弓/连弩（35） | 39/655/923/658/2515/2747/5282/661 木材系弓；3504/3498/99/3492/3510/3486/3516/3480 铜至铂金弓；4058 Skeleton Bow；44 Demon Bow；796 Tendon Bow；120 Molten Fury；2888 The Bee's Knees；3019 Hellwing Bow；435/1187/436/1194/481/1201/578 七种困难模式连弩；682 Marrow；725 Ice Bow；2223 Pulse Bow；3052 Shadowflame Bow；1229 Chlorophyte Shotbow；2624 Tsunami |
| 箭（15） | 40 Wooden；41 Flaming；47 Unholy；51 Jester's；265 Hellfire；516 Holy；545 Cursed；988 Frostburn；1235 Chlorophyte；1334 Ichor；1341 Venom；3003 Bone；3103 Endless Quiver；3568 Luminite；5348 Shimmer Arrow |
| 飞镖武器（4） | 281 Blowpipe；986 Blowgun；3007 Dart Pistol；3008 Dart Rifle |
| 飞镖（5） | 283 Seed；1310 Poison Dart；3009 Crystal Dart；3010 Cursed Dart；3011 Ichor Dart |
| 星星炮（2） | 197 Star Cannon；4060 Super Star Cannon（均使用 75 Fallen Star） |
| 法术武器（20） | 112 Flower of Fire；127 Space Gun；157 Aqua Scepter；165 Water Bolt；272 Demon Scythe；514 Laser Rifle；518 Crystal Storm；519 Cursed Flames；683 Unholy Trident；726 Frost Staff；744 Diamond Staff；1264 Flower of Frost；1295 Heat Ray；1308 Poison Staff；1336 Golden Shower；1444 Shadowbeam Staff；1445 Inferno Fork；1930 Razorpine；2188 Venom Staff；3209 Crystal Serpent |
| 实验性非线性近战投射物（15；生产禁用） | 55 Enchanted Boomerang（6）；119 Flamarang（19）；191 Thorn Chakram（33）；284 Wooden Boomerang（52）；277 Trident（47）；280 Spear（49）；670 Ice Boomerang（113）；561 Light Disc（106）；1324 Bananarang（272）；1918 Fruitcake Chakram（333）；756 Mushroom Spear（130）；1200 Titanium Trident（218）；4061 Thunder Spear（730）；5687 Slime Spear（1103）；3278 Wood Yoyo（541） |
| 独立生产路线（不计入上述画像统计） | 759 Rocket Launcher＋771 Rocket I（固定 134 弹体） |

枪械目录为上述 22 × 15 的笛卡尔积，但仍逐次核对玩家当前真正选中的弹药与最终弹幕。Pew-matic Horn 逐次核对 PickAmmo 的弹药伤害/速度贡献，再锁定 ItemCheck_Shoot 的 968 号最终弹体；其 ±1.125 分量散射计入保守包络。Sniper Rifle、Venus Magnum 和 Uzi 对原始 Musket Ball 类弹幕的原版转换也按武器分别处理，不会把转换规则外推给其他枪。

弓箭目录中除 Hellwing Bow 外为 34 × 15；Hellwing 只接受 Wooden Arrow 与 Endless Quiver，因此合计 512 组。Molten Fury、Marrow、Ice Bow、Pulse Bow、The Bee's Knees、Hellwing Bow 与 Shadowflame Bow 的转弹顺序分别按原生分支处理。Chlorophyte Shotbow 与 Tsunami 虽会产生多箭，但只给一枚确定存在、保持名义速度的主箭计分。

飞镖目录严格限制为上述 4 × 5 的 20 组，不与枪弹或箭的目录混用。Seed 在第 15 次 projectile subupdate 起于移动前加入 `+0.1 Y`；Poison/Cursed/Ichor Dart 在第 20 次起加入 `+0.075 Y`；Crystal Dart 每 tick 有两次更新且没有重力。Crystal 的反弹/分裂、Cursed 的死亡云以及各种 debuff 都不计入保证直伤。

Rocket Launcher＋Rocket I 锁定最终 134 号弹体、180 个 projectile subupdate 生命周期和原生逐次加速轨迹。只有这一组可以进入专用火箭求解器；其他火箭弹、发射器、榴弹和地雷不会借用它的合同。

“主目录画像存在”表示源码与程序集行为已经审查并有离线合同回归，不表示 885 个主目录组合或 Rocket Launcher＋Rocket I 都完成了真实客户端逐发试射；“近战实验画像存在”更不表示生产已接线。目前只有 Minishark＋Musket Ball 与 Clockwork Assault Rifle＋Crystal Bullet 留有此前无渲染原版运行观察；这项历史观察不会被冒充为新求解器、所有词缀、液体路径或完整 Boss 战验收。

## 原生依据与只读边界

依据是 Windows Steam 原版 Terraria **1.4.5.8 x86** 程序集，SHA-256：`960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。审查使用只读反编译/元数据；没有启动游戏、调用会消耗弹药的 `PickAmmo`、读取或改写玩家存档，也没有消耗游戏随机数。

- 实际弹药由独立的只读适配器按原版选择结果采样，不能用“背包里任意一种兼容弹药”代替。
- 直伤保留原版独立舍入顺序：`GetWeaponDamage(武器) + floor(弹药.damage × GetWeaponDamageMultiplier(弹药))`。法术武器读取已经应用折扣的实时耗魔。
- 速度、弹幕额外更新数、寿命、使用时间、动画、复用延迟与自动连用状态都取实时值并做边界检查；最终弹幕身份不符时立即停火。
- 弓额外只读 `archery`、`magicQuiver`、`hasMoltenQuiver`、`accSharpBarb` 与 `accHarpyCharm`。速度顺序严格为箭速相加、魔法箭袋 ×1.1、箭术药水 ×1.2（上限 20）；箭伤先加入熔火箭袋/锋利倒钩，再按弹药伤害倍率舍入。这组状态不能同时确认时只拒绝弓，不影响枪械。
- 目录只读取状态并计算瞄准，不修改武器冷却、弹药、魔力、伤害、射速或随机数。

## 弹道与保守计分

普通直线主弹按真实初速度、额外更新和寿命求正交会。箭、飞镖与 Golden Shower 使用可复用的“延迟若干 projectile subupdate 后，每 subupdate 在移动前施加竖直加速度”模型；普通箭和 Seed 第 15 次更新开始 `+0.1`，Poison/Cursed/Ichor Dart 第 20 次更新开始 `+0.075`，Golden Shower 第 5 次更新开始 `+0.075`，Crystal Dart 及 Holy/Frostburn/Marrow/Ice/Shadowflame/Luminite 与反重力 Shimmer Arrow 均保留自己的原生更新数、阈值和增量。求解器在每个离散更新区间解精确二次式，不逐帧暴力模拟。

魔法箭袋对原本没有额外更新且仍带原生 `arrow` 标记的弹幕，在第一 tick 末才把 `extraUpdates` 设为 1；所以第一 tick 是 1 次更新，此后才是每 tick 2 次。寿命和交会映射均保留这个边界，不把整段轨迹错误地按双倍更新计算。Crystal Storm 另行使用其每次 AI 更新先乘 `0.985`、再移动的指数减速模型，并把原生有效伤害更新上限纳入射程。

散射按原版分支建立严格包络：

- Clockwork Assault Rifle 依据**射击入口处剩余动画**区分三发，而不是按固定模数猜测。
- Gatligator 同时计入归一化前后的两层扰动及三分之一独立缩放分支。
- Onyx Blaster 使用模长不超过 2 的径向包络，不误写成 X/Y 各 ±2 的方形包络。
- Shotgun、Tactical Shotgun、Boomstick、Quad-Barrel Shotgun 与 Onyx Blaster 只计一枚保证存在的主弹；额外随机弹丸不计入准入 DPS。
- Chlorophyte Shotbow 与 Tsunami 只计一枚保证主箭；Holy Arrow 落星、The Bee's Knees 蜜蜂、反弹/穿透、Debuff 与死亡衍生弹幕均不计入保证 DPS。
- Hellwing 的木箭弹幕用原生 ±π/8 最大角包络；其他箭会把随机向量写入各自 `ai[]` 并改变重力计时，故不冒充支持。
- 1.4.5 Harpy Charm（含 Seraph Necklace/Phoenix Quiver 升级）会在飞行中重新选择目标并旋转箭矢；目前装备该效果时弓路线失败关闭，不把外部追踪 FSM 当作普通抛物线。
- Poison Staff 与 Venom Staff 只计随机系数必为零的第 0 枚弹丸；Golden Shower 保留三发原生射击周期但不计 Ichor debuff；Inferno Fork 与 Crystal Serpent 的死亡衍生爆炸／碎片不计。

反弹、穿透、爆炸、Debuff、Silver Bullet 特攻、水晶碎片及额外弹丸均记录为副作用，但不计入保证直伤。`ApproximateDirectDps` 也不包含敌方防御、暴击、伤害浮动与实际命中率。

`ConservativeRangePixels` 是主弹在干燥、无遮挡路径上的保守长度，不是推荐风筝距离。墙体、液体、目标变速与原版弹幕生命周期仍会缩短实际命中距离。不存在正的物理解、超过弹幕寿命、超过预测时域，或散射包络装不进当前目标命中框时，`WeaponAimSolver` 都会停火并等待下一帧事实，而非降级成盲射。

## 生产接线与未覆盖范围

- 开火门控要求规划槽位、实际选中槽位、武器/弹药/弹幕身份和资源合同连续一致；换枪或换弹期间不会沿用上一把武器的解。
- `MeleeProjectileCatalog` 的 15 个画像只用于实验性身份、范围和求解回归。即使底层 `OutputRouteContract` 能构造该无弹药路线，召唤前与战中 Boss 准入都会拒绝 `MeleeProjectile`；自动选枪只保证它不会压过任一正分生产武器。若快捷栏没有正分候选，选槽函数可能保留当前近战槽，但随后生产准入仍会失败。玩家另有合格的召唤／鞭双槽路线时，可以改走后者，不等于近战画像获准。
- 热路径使用常驻目录和值类型，离线回归覆盖零分配合同；这不等同于宣称完整游戏端到端延迟为零。
- 召唤杖与鞭不套用本弹道目录。生产代码另有严格的双槽部署/挥鞭 FSM，目前接受 28 根已审查普通 minion staff 与原版 18 条鞭的精确身份；它不推测召唤物 AI、鞭范围或标签收益。
- 弓仍明确拒绝：Daedalus Stormbow 与 Blood Rain Bow（随机高空出生点、箭数和速度扰动）；Phantasm 与 Phantom Phoenix（先生成持续型 held projectile，之后重新选弹/发射）；Eventide（五发序列含双伤双速转弹）；Aerial Bane（固定六箭扇形且无名义中心箭）。这些重点武器不会默默落入通用弓模型。
- 其他仍明确拒绝：Xenopopper、Vortex Beater、Coin Gun、喷火器等多阶段或特殊发射器；Chlorophyte Bullet 等追踪弹；除 Rocket Launcher＋Rocket I 外的火箭组合；未列明的弓或飞镖组合；以及未列入上述 15 个画像的悠悠球、回旋镖、长矛、连枷和其他近战接触模型。Star Cannon 与 Super Star Cannon 仅接受上述两个独立的陨星组合；Super Star Slash（弹幕 729）属于附加效果，不计入保证直伤或 DPS。
- 追踪、持续控制、放置型、区域型或多阶段法器仍拒绝。Diamond Staff 的 126 号弹体在自身基础状态下只有已建模的大命中框特征，但仍须成功读取有效身体装备，且装备任何 Gem Robe（1282..1287 或 4256）时失败关闭。Amethyst、Topaz、Sapphire、Emerald、Ruby 与 Amber Staff 的基础弹体分别自带追踪、范围爆炸、双弹旋转、先快后慢、穿甲散射或反弹等 `GemStaffFeatures`；它们不是 Diamond Staff 的换色直线版本，当前均未接入。

后续扩展必须继续以“精确物品/弹药组合＋原生分支证据＋失败关闭回归”为单位，不能因为两个弹幕看起来相似就共享未经证明的输出合同。
