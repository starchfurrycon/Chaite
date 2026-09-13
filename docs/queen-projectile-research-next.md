# Queen 弹丸：伤害形状、更新相位与最小预测字段

仅为下一轮 `ThreatSnapshot` 精确层准备的只读研究；本文没有修改生产代码、测试或 harness，没有构建、运行游戏或操作存档。所有“精确”描述均指下列白名单版本的代码路径；尚未通过独立原生弹丸轨迹／伤害微测。

来源：Windows Steam Terraria 1.4.5.8 x86，SHA256 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。使用 `tools/research/Inspect-MethodIl.ps1` 与 Mono.Cecil 仅读元数据；没有执行被检查 AI，不分发原版程序集或完整反编译源码。

## 1. 最关键的结论

- 922 的伤害是**扩大的完整轴对齐矩形**，不是贴地环、椭圆、圆周或只看特效尘埃碰撞。
- 922 先 `++ai0`；结果大于 9 就 `Kill()` 并返回，未进入当次正常伤害阶段。入口采样 `ai0=9` 时，虽然仍看得到上一帧 480×480 的 active 实例，**下一次更新没有伤害机会**。
- `timeLeft` 在正常伤害阶段之后才减一。因此 `timeLeft=1` 的弹丸仍可能在本次更新造成伤害，然后消失。不能在模拟移动前先减寿命、删掉最后一次伤害。
- 920／921／926 先走公共 `ai0++`，再检查 `ai0>=5` 才加 `.15` 纵速并钳 `ai0=5`。入口 `ai0=4` **当次已加重力**；从 0 出发只有前四次更新无额外重力。
- 这些球／刺还有 `vy<=16` 上限、液体／地形影响，以及最新版本可能启用的风物理。只补一个 `.15*t*t/2` 不能声称完整精确。

## 2. 实际伤害查询：922 没有特殊形状

调用链：

`Projectile.Update → Damage → Damage_GetHitbox → Damage_EVP → Colliding → Player.Hurt`

具体证据：

1. `Damage_GetHitbox 0000..0026` 取 `Rectangle((int)position.X, (int)position.Y, width, height)`。该方法后面的特殊扩大／收缩规则不包括 922、920、921、926 或它们的 aiStyle。
2. `Damage_EVP 0151..0164` 把该矩形和本地玩家 `getRect()` 传入 `Projectile.Colliding`，不是根据地面特效、圆形半径或地图材料另外筛一遍。
3. `Colliding` 中上述四种类型／aiStyle 没有定制分支；走 `14dd..14f0` 的 `Rectangle.Intersects`。矩形不相交后的额外形状分支也不包括它们。
4. 静态集合初始化的只读核对排除了 `IsAPhaseblade`、`IsAWhip`、`SkipDamage_EVP` 对它们的特殊处理。`SkipDamage_EVP` 的集合是 108／164／1002，不含这四个 ID。
5. `AI_135_OgreStomp 00a4..062c` 按地块扫描、模 3 的节奏生成尘埃／碎屑，是表现逻辑，没有在此调用玩家伤害，也没有把实际碰撞裁成尘埃出现的贴地位置。

因此，最大波形矩形的对角区域也属于几何危险区；不能因为超出半径 240 的圆就判为安全。922 的 `tileCollide=false`，实际伤害几何也没有对应的 `CanHit` 遮挡检查；不要把平台／墙后误判为免疫波形。

这是**几何伤害机会**，不等于每个相交 tick 都扣血。`Damage_CanDealDamage` 不对这些类型设置 alpha 或年龄等待；`Damage_EVP` 仍要求 hostile、damage>0、本地玩家 active 且未死亡，以及原生免疫／格挡／伤害规则。922 使用 General 免疫槽，不是 919／923／924 使用的 BossNoCheese 槽。`alpha=255` 也不是无伤预告阶段。

矩形 X／Y 是向零截断的整数，尺寸也是整数。生产 float AABB 可以保持不漏判的外包络，但若要和原生边缘逐像素对照，应复用此整数化和 `Rectangle.Intersects` 的严格边界语义；仅接触边界不等于矩形重叠。

## 3. 更新顺序：预测的第 1 tick 是哪一帧

`Main.DoUpdateInWorld_Inner` 中：

`004c UpdateWorld_Players → 00ad UpdateWorld_NPCs → 00ed UpdateWorld_Projectiles`

拆特采样在 `Player.Update` 入口。所以 snapshot 的弹丸状态来自上次弹丸更新；本帧玩家先执行动作，弹丸随后更新并检测玩家更新后的矩形。定义 `k=1` 为**当前采样之后、同一世界 tick 的第一次未来弹丸更新／伤害机会**，不是下一次采样之后才开始。

`Projectile.Update` 的普通路径：

| IL | 顺序 |
| --- | --- |
| `006e..0075` | `numUpdates=extraUpdates`，每世界 tick 运行 `extraUpdates+1` 个子更新 |
| `0479..0482` | 若 active，调用 AI |
| `0487..05c1` | 可适用的风物理 |
| `11c4` | `HandleMovement`，包括实际运动／地形碰撞 |
| `11f3..11fb` | 若已经 inactive，返回，不进入后续 Damage |
| `1513` | 调用 Damage |
| `1cf5..1d0d` | `--timeLeft`；若≤0，Kill |
| `1d12..1d1b` | 若 penetrate==0，Kill |

922 在 AI 中寿命结束时走 Kill，之后被 active 检查挡在正常 Damage 前；Kill 中没有 922 的爆炸补伤分支。920 的某些地形碰撞会提前 Kill，也需在该次 Damage 前排除；921／926 的反弹路径是扣 penetrate 后继续普通 Update，不能只因为预测 penetrate 变零就自动把该次伤害机会抹掉。

这四类默认 `extraUpdates=0`；`SetDefaults` 的公共初始值在 `022c..022e`，类型分支没有覆盖。不过适配器应读**实际字段**或校验该前提，不将所有弹丸都视作每世界 tick 只更新一次。如果未来支持 U>1，玩家每世界 tick 只更新一次、弹丸可检测 U 次：子更新 `j` 应配玩家第 `ceil(j/U)` 个世界 tick 的位置，不能误把 U 次弹丸更新当成 U 次玩家移动。

Queen 在 NPC 更新生成的弹丸可以在**同一世界 tick** 进入随后的弹丸循环并造成第一次伤害；它在本 tick 玩家入口 snapshot 中还不存在。对“未出生弹丸”的提前反应仍需 Boss 状态守卫，不能仅靠改进已观测 `ThreatSnapshot` 保证覆盖新生帧。

## 4. 922 的年龄、扩张与有效寿命

默认：30×30、aiStyle 135、timeLeft 120、penetrate −1、hostile、ignoreWater、禁 tileCollide。普通生成时 ai0=0。

`AI_135_OgreStomp 0019..00a4` 的顺序为：

```text
age += 1
if age > 9: Kill; return
velocity = (0,0)
保留旧中心 C
size = (int)(16f * Lerp(5f, 30f, GetLerpValue(0f, 9f, age, false)))
width = height = size
position = C - (size/2f, size/2f)
```

`Entity.set_Size` 使用 float→int 向零截断；`set_Center` 以整数尺寸的一半（浮点除法）重定位。应该保持观测中心，而不是把扩大后的左上角继续当原左上角。

| 当次 AI 后 age | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 普通 size | 124 | 168 | 213 | 257 | 302 | 346 | 391 | 435 | 480 | Kill，无新伤害 |

最初 30×30 是生成状态，不是正常 Update 的第一张伤害矩形；第一次 AI 后即扩大到 124×124。表是源代码推导，不是已跑过的原生微测。

### 从玩家入口 snapshot 换算

设当前观测年龄为有限 `a>=0`，实际剩余寿命为正整数 `L`，`extraUpdates=0`，且字段符合普通 922 profile，没有外部改写或提前销毁：

```text
D_age = max(0, floor(9 - a))
D = min(D_age, L)
未来伤害机会存在 ⇔ 整数 k 满足 1 <= k <= D
此机会的 age = a + k
此机会的 size = 原生单精度 Lerp 公式取整
此机会矩形中心 = 当前观测中心
```

`timeLeft` 的普通值是 `120-a`，但**不要用这个等式反推年龄**：实际伤害寿命来自 ai0，timeLeft 只是第二个独立上限。可把等式用于普通轨迹的校验，而不是覆盖观测值。

例：

- a=0、L=120：未来有 9 次伤害机会，第一次 124×124。
- a=8、L=112：未来仅 1 次机会，为 480×480。
- a=9、L=111：未来 0 次；当前 active／大尺寸不代表下一次仍能伤害。
- a=7、L=1：仅 1 次机会，为 age8／435×435；随后 timeLeft 终止，不会继续扩到480。
- a=8.25：下一次先加到9.25便 Kill，没有机会；不可先把 age 向下取整成8。

`a` 为 NaN／Infinity、负值、字段来源未知、未识别额外更新、被改写的 tileCollide／ignoreWater 等，应该退出“精确 profile”，而非发明正常出生年龄。异常 active 且 L≤0 的对象按 Update 顺序仍可能先运行一次 Damage；上面的正寿命公式不适用，不能用 `max(0,L)` 宣称它必然无害。

当前视觉矩形可用于显示，但不能拿 `BoundsAt(0)` 作为“本帧会在移动之前再结算一次”的根据。每个未来伤害采样需要独立 `CanDamageAtStep(k)`；仅返回一个过期的大矩形而不让风险层跳过它，会继续把 age9 当作障碍。

## 5. 920／921／926 延迟重力的严格边界

默认值：920／921 为 6×6，926 为12×12；aiStyle1；公共 timeLeft=3600，分别 penetrate=1／3／2，tileCollide=true、ignoreWater=false、extraUpdates=0。需要使用实例实际值，不从当前穿透数倒推已碰撞几次。

相关 IL：

- `AI_001 68b7..6cd4`：公共递增开关默认为 true，这三类不在禁递增列表；因此先 `ai[0]++`。
- `a0e8..a142`：这三类进入 `ai0>=5` 的分支，置 ai0=5，`velocity.Y += .15f`。
- `cdcd..ce2c`：它们启用公共下落限速；`velocity.Y>16` 时置16。普通路径没有对向上的速度施加对称 −16 限制。
- `35ff..3941`：alpha 每次减50直至0，另有 ai1 的音效／表现状态；不是延迟伤害条件，也不替代 ai0。

干燥、无风、未撞地形的逐子更新最小递推：

```text
a' = a + 1
if a' >= 5:
    a' = 5
    vy' = vy + .15f
else:
    vy' = vy
vy' = min(vy', 16f)
vx' = vx
p' = p + (vx', vy')
随后以 p' 的整数矩形查询玩家，再 --timeLeft
```

从合法有限观测 a 出发，第一次重力更新编号：`g=max(1, ceil(5-a))`。从0出生时为第5次；入口3→下一次4不加，入口4→下一次5开始加，入口5→下一次先6再钳回5且继续加。a=3.5 时下一次4.5不加，再下一次5.5会加并钳5；不能在读取时简单强转整数。

如果初始 vy≤16 且预测窗尚未触碰限速、没有其他影响，令 `m=max(0,k-g+1)`，可作快速数学检查：

```text
x(k) = x0 + k*vx0
y(k) = y0 + k*vy0 + .15*m*(m+1)/2
vy(k) = vy0 + .15*m
```

这是代码递推的实数形式，不是逐原生 float 舍入的替代 oracle。正式窄相可逐 tick 计算有限窗以保留单精度时序与限速；不能使用常见的 `.5*g*t*t`，因为原生先加速度再移动，有离散 `m*(m+1)` 项。

### 不能遗漏的 profile 边界

- 921／926 在 `HandleMovement 2955..2a0b` 对已经发生的碰撞作反弹：变动分量的旧绝对速度严格>1时，vx乘−.4、vy乘−.95；一次碰撞扣1 penetrate。绝对速度恰为1不走此反弹分量规则。角落、斜坡和原生修正位移不能简化成“跨过地面后镜像终点”。
- 三类 ignoreWater=false，干燥递推不能用于已经湿身或即将进入液体的路径。
- `ShouldUseWindPhysics`：全局 windPhysics 开启时，aiStyle1默认可受风；静态 WindPhysicsImmunity 的这三类未设覆盖。`Update 0487..05c1` 还检查中心位于 worldSurface 以上、中心地块存在且无墙、水平速度／风向条件，然后可能给 vx 加 `windSpeedCurrent*windPhysicsStrength`。922 aiStyle135默认不走风物理。这是 1.4.5.8 的实际规则，不能从旧版本无风公式推断本版本也恒定vx。
- 最小可交付的精确子集可以先要求整个有限窗为“干燥、无风影响、未触发地形碰撞”，超出后改保守包络或显式 Unknown。也可以单独实现并微测局部平面反弹；不要把暂未建模等同于弹丸已销毁。

## 6. 下一轮最小字段与验证清单

不需要把完整 Projectile 对象复制进 Core。现有 type／position／velocity／width／height／damage／timeLeft 之外，最少增加：

| 字段／语义 | 用途 |
| --- | --- |
| `Ai0` 及其读取有效性 | 922实际年龄；920／921／926的延迟重力状态 |
| 实际 `extraUpdates` 或已校验的更新倍率 | 区分原生子更新和玩家世界 tick |
| `MotionProfile` 与有效性 | 922固定中心扩张／Queen延迟下坠；未知不套精确公式 |
| `tileCollide`, `ignoreWater`, 当前湿状态／有限窗环境已知性 | 校验干燥自由飞行子集，或接入已有局部碰撞信息 |
| `penetrate`（若实现反弹） | 处理最后一次反弹及真正消失时刻，不凭初始默认值计算 |
| 稳定实体标识（若做轨迹缓存） | 失效／槽位复用时清缓存，不用“同类型同槽位”无限续接 |

当前目标是一个返回“该步仍能伤害吗＋此步几何”的有界预测接口；普通 native TimeLeft 和有效伤害窗口应分开，避免在通用风险评分里再出现差一帧。

待实现后的独立微测应至少覆盖：

1. 922出生30→第一次124；保中心；9次尺寸；age8→9最后伤害；age9→10先Kill不伤；L=1先伤后Kill。
2. 922角落落在完整方框内而在半径240圆外仍可碰撞；矩形恰贴边不交；半像素中心／奇数尺寸的整数化；墙体不构成该波遮挡。
3. 920／921／926入口 a=0／3／4／5 和分数值，逐帧比较ai0／vy／position；vy=15.95→16；负向初速度不误钳−16。
4. 各球最后一次反弹扣penetrate至0后的Damage/Kill次序；碰撞前Kill的920；相同阶段相同L的不同命运不能混为一个TTL公式。
5. 若支持风／液体／extraUpdates，分别加独立轨迹证据；尚未支持时验证明确退出精确profile，而不回退成“固定直线等价安全”。

这些是下一轮验证要求，不是本文已经执行的测试结果。
