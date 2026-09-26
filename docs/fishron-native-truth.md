# 猪鲨 — 原生验收事实表（权威）

> **本文件记录的每一条都来自原生引擎实测，不是建模、不是 wiki 换算。
> 凡与实验台（`tests/Chaite.Tests/FishronNoHitLab.cs`）冲突的，以本文件为准。**

验收路径：`tools/run-native-acceptance.ps1`（原生跑一场猪鲨，读 `result.json` 的 `hits`）。

---

## 0. 为什么改成原生验收

实验台是**手写的替代模型**，已产出**三处与真游戏的偏离**：

| 偏离 | 后果 |
|---|---|
| 无敌帧规则写成"任何冲刺都白给 15 tick" | 门限法的 speed 12 零接触**是假的，已撤回** |
| 阶段转换是血量驱动，而实验台从不扣 boss 血 | **boss 永远停在一阶段**，二三阶段结论作废 |
| `frame.Dash` 未初始化 + `ControlDash` 未置位 | **冲刺放不出来**，所有冲刺结论未验证过 |

而项目里本来就有原生路径（`tools/GameProbe.cs` 5726 行 + `prepare-game-probe.ps1`
+ `start-isolated-test.ps1`），**验收一度被搬到替代模型上，这是根本性失误**。

---

## 1. 原生验收链（已跑通）

```
tools/run-native-acceptance.ps1 -RunName <标签> -Phase <阶段>
```

它依次做三件事：

1. **`prepare-game-probe.ps1 -Headless`**——用 Roslyn 从源码编译
   `Chaite.GameProbe.dll` / `GameProbePatcher.exe` / `Chaite.DesktopHost.exe`，
   校验原版游戏与四个依赖的 SHA256，**私有副本**打补丁让
   `Program.RunGame` 直接调 `ChaiteGameProbe.RunHeadless()`，
   然后用 **Mono.Cecil 读 IL** 静态验证补丁；
2. **`start-isolated-test.ps1`**——生成 pin plan 与 launch binding，
   在**私有桌面 `ChaiteTest_*`** 上、kill-on-close **job object** 内启动子进程，
   子进程结束后**重新校验每个 pin 的哈希**；
3. **读 `result.json` 判定**，并断言场地与配装。

每次运行落到独立目录 `artifacts\game-probe-<标签>\`，保留 pin plan、
launch binding、`game-probe.log` 与全部观测流。

---

## 2. 原生实测事实

### 2.1 首次成功原生战斗（`game-probe-20260926-070737`）

- **场景** `duke-fishron`，**专家难度**，seed `20260910`；
- **6000 tick / 5999 原生帧**，`validBattle=true`，`bossSeen=true`；
- **hits = 3**（原生 `Player.Hurt` 计数）。

### 2.2 场地：确实是海洋群系的长直平地

```
NATIVE_BIOME hallow=False jungle=False snow=False beach=True
groundLeft=1  groundRightExclusive=400      ⇒ 399 格
platformRows=[440,380]  spacing=60          ⇒ 地面 + 两层平台
startSide=left  playerStartTileX=21
```

**399 格 ≥ 要求的"300 余格"**，且 `beach=True` 证明海洋群系成立。

### 2.3 配装：`Fishron formula fixture: fishron-fairy-wing`

`armorAndAccessories = [1547,1549,1550,3990,761,0,860,491,3097,0]`
——**含 761（仙灵之翼）与 3097（克苏鲁之盾）**，与准入配装一致。

### 2.4 ★ 实际水平速度：**翼飞巡航 ~13.87，冲刺 14.50**

`accRunSpeed` 这个**状态字**在原生里读 **6.75**：

```
DIRECT_PLAYER_UPDATE completed defense=48 accRun=6.75
```

**但 `accRunSpeed` 不是实际速度函数。** 从 `game-probe.log` 的 124 个
`FRAME` 采样直接统计 `|vel.X|`：

| 统计量 | \|vx\| |
|---|---:|
| **最大** | **14.50**（= 克盾冲刺） |
| p99 | 14.50 |
| **p95** | **13.87**（= 翅膀巡航峰值） |
| 中位数 | 6.65 |

`shield-events.jsonl` 1024 行的 `max|vx|` 也是 **14.50**，
与原生克盾起步速度一致。

> **所以要分清两个数**：
> **`accRunSpeed = 6.75` 是引擎暴露的加速度/速度状态字，
> 而玩家在翼飞中真正达到的水平速度是 ~13.87，冲刺是 14.50。**
>
> **对建模而言该用的是后者。** 我在此前一轮里把 6.75 说成"翼速"是不准确的，
> 特此更正：**实测函数是 13.87 / 14.50。**
>
> 这也意味着实验台的 "12.0 零接触窗口"**低于真实速度**（14.50），
> 所以那个零**在真实配装下不可达**这一判断**依然成立**，
> 但"真实速度是多少"这一条现在有了原生实测值：
> **13.87 巡航 / 14.50 冲刺**，而不是 wiki 换算的 15.82 / 16.4。

### 2.5 ★ 克苏鲁之盾冲刺**工作正常**

`shield-events.jsonl` 原始前两行：

```json
{"tick":317,"requested":true,"started":true,"contact":false,"npcSlot":-1,
 "eocDash":15,"dashDelay":-1,"immuneTime":0,"vx":-14.5,"vy":-9.915,
 "lifeBefore":416,"lifeAfterDash":416,"lifeAfterFrame":416}
{"tick":318,"requested":true,"started":false,"contact":false,
 "eocDash":15,"dashDelay":30,"vx":0,"vy":-9.915,...}
```

**与原生 `Player.cs:21752` 完全一致**：
`controlDash && !CCed && releaseDash` → `dashing=true; eocDash=15; dashDelay=-1`，
下一 tick 进入 `dashDelay=30` 冷却。

> **所以实验台"冲刺永远放不出来"是实验台自己的缺陷，不是生产代码的问题。**
> **此前怀疑 `GravityDashMotion` 有 bug 的说法已撤回。**

### 2.6 伤害来源是原生的

`hurt-observations.jsonl`（3 条）：

| tick | 来源 | 请求伤害 | 实际 | 玩家 480→ |
|---|---|---|---|---|
| 4182 | **npc type 370**（猪鲨本体） | 136 | 79 | 401 |
| 5702 | **projectile 384**（鲨鱼龙卷） | 104 | 51 | 429 |
| 5937 | **npc type 370** | 119 | 64 | 376 |

**每条都带真实成因**，玩家 48 防御下约减半。

---

## 3. 逐 tick 回放接口（状态机的交付通道）— **已实测跑通**

`RouteReplay` 从 `CHAITE_ROUTE_FILE` 读控制序列，两种格式：

- **帧索引**：每行 `direction,jump,dash`（三列）或 `direction,jump,up,down,dash`（五列）；
- **绝对 tick 索引**：每行 `tick,direction,jump,up,down,dash`，
  判定见 `RouteReplay.cs:161-210`。

`CHAITE_ROUTE_SKIP` 声明"接管 tick"与"路线起点 tick"之差。

### 3.1 回放确实在驱动玩家（`game-probe-route-right`）

用一条**恒定向右**的 8000 行路线（`tick,1,0,0,0`）跑 `monitor` 阶段：

| 项目 | 实测 |
|---|---|
| 路线文件 | 已加载（`Route replay:` 打印） |
| **有效战斗** | **`validBattle=True`，`bossSeen=True`** |
| tick | 3000 |
| **hits** | **5** |
| 场地 | 399 格，`beach=True` |
| **克盾冲刺** | **`dash started: 23`**，`dash-active ticks: 370` |
| **冲刺撞到 NPC** | **`npc contact: 3`** |
| 玩家 x 范围 | **640 .. 65,343**（确实在被驱动） |
| **max \|vx\|** | **14.50** |

> **这条路打通了原生验收的最后一环**：
> **路线文件 → 真实引擎逐 tick 驱动 → 读 `hits`。**
> 而且 `npc contact: 3` 证明**"冲刺撞到本体"这一事件在原生里可观测**，
> 正是三阶段克盾反冲机制所需要的信号。

### 3.2 验收判据

> **`validBattle=True` 且 `hits == 0`** ⇒ 原生无伤。
> 这是唯一可称为无伤的结果。

---

## 7. 本轮原生改动与实测结果

### 7.1 已生效并**原生验证有效**：地面逃跑者必须起跳

`FishronWingScript` 的三个逃跑分支写的是
`vertical = player.OnGround ? 0 : 1`，而 `output.Jump = vertical < 0`
（`FishronWingScript.cs:419`）。**结果是：站在地面上的玩家永远不会起跳。**
翅膀在 1.4.5.8 里**没有任何地面机动**，所以"地面 + 不起跳"等于**原地不动**。

原生实测（`-FormulaRoute fishron-fairy-wing`，其余配置完全相同）：

| | ticks | hits | boss damage |
|---|---:|---:|---:|
| 改前 | 10255 | **12** | 87 |
| 改后（`? -1 : 1`） | 8684 | **8** | 37 |

**受击 12 → 8。** 死因构成也变了：改前 8 次本体 + 4 次鲨鱼龙卷；
改后 **6 次本体 + 2 次鲨鱼龙卷**（那 2 次已是终局）。

### 7.2 已定位但**尚未生效**的两处（下一轮目标）

三次运行 `Chaite.Core.dll` 哈希各不相同，证明改动确实进了运行；
"没生效"是因为**当前轨迹没有走进那两个分支**，不是改动本身错误：

1. **`AwayFromBoss` 用的是"垂直于 boss 向量"**（`FishronWingScript.cs:889`）。
   垂直向量在 boss 位于正上方时**退化为纯竖直**，水平间隙完全没被拉开。
   AI_069 悬停在玩家上方 200 px 再冲锋，所以这是**最常见的几何**。
   已改为沿"圆心连线反向"逃跑（同时拉开两个轴）。
2. **`personal-space` 锁存把竖直方向定成"迎向 boss"**
   （原 `_personalSpaceVertical = boss.Center.Y >= player.Center.Y ? -1 : 1`）。
   boss 在上方时给 `-1` = **起跳撞向正在下冲的本体**。已改为
   `? 1 : -1`（原生 Y 向下增长，boss 在上方就该**下坠**拉开）。

### 7.3 受击的精确几何（tick 3019）

```
 tick |    plX    plY   plvx  plvy  wing |    boX    boY   bovx  bovy  bs  bs2 | L R U D J dash | phase
 3003 |    640   5953    0.0  10.0     0 |    609   5686   -6.2  15.8   1   15 | 0 0 0 1 1    1 | charge-horizontal
 3013 |    640   6038    0.0   0.0     0 |    547   5844   -6.2  15.8   1   25 | 0 0 0 1 1    1 | charge-horizontal
 3014 |    640   6032    0.0  -6.2   130 |    541   5860   -6.2  15.8   1   26 | 0 0 0 1 1    1 | charge-horizontal
 3018 |    640   6007    0.0  -6.2   130 |    515   5920   -7.3  13.6   0    2 | 0 0 0 1 1    0 | personal-space
 3019 |    644   6003    4.5  -3.5   130 |    508   5932   -6.8  12.5   0    3 | 0 1 0 1 1    1 | personal-space  <== 受击
```

**三个致命特征同时出现：**

1. **玩家 `plX` 恒为 640、`plvx` 恒为 0.0 共 16 tick** ——
   `_bandLeft = worldLeft + BandEdgeMargin = 640`，
   `ApplyArena`（`:924`）在左边界把水平方向强制反向；
   于是在边界处**水平输入被锁死**，玩家被钉在墙角；
2. **boss 从正上方以 `bovy 15.8` 垂直下冲**，而玩家在**上升**（`plvy -6.2`）
   ⇒ **迎面相撞**；
3. 受击瞬间 **x 重叠 14 px、y 重叠 29 px**（boss 宽 150 高 100）。

### 7.4 结论与下一步

**核心矛盾：玩家要把水平速度提上去，但地面速度只有 ~3.5 px/tick，
而冲锋是 14.7–17.0 px/tick。唯一够快的机动是翅膀（实测巡航 13.87）。
所以"地面 + 贴着边界"是必死的组合。**

下一轮按顺序做：

1. **修 `ApplyArena` 的墙角死锁**：当水平方向与一堵墙冲突时不要把它变成 0，
   而要**沿墙切向逃跑**（boss 竖直冲过来时，水平方向是唯一的活路）；
2. **让玩家在冲锋前就处于飞行状态**（`wingTime` 满却站在地上是浪费），
   而不是在冲锋边缘才起跳；
3. 复测 §7.2 的两处改动是否在新轨迹中生效。

---

## 4. 观测流（可用于逐帧诊断）

| 文件 | 内容 |
|---|---|
| `boss-observations.jsonl` | boss 周期/转移快照（本场 333 行） |
| `charge-observations.jsonl` | **冲撞**观测（70 行） |
| `prehit-observations.jsonl` | 受击前 48 tick 窗口（144 行，命中 3） |
| `hurt-observations.jsonl` | 每次 `Player.Hurt` 的完整成因 |
| `shield-events.jsonl` | **克盾冲刺事件**（本场 1024 行，丢弃 3710） |
| `game-probe.log` | 每 60 tick 一行 `FRAME`，含 `pos/vel/accRun/wingTime/wings` |

---

## 5. 待办

1. **写路线生成器**：用一条可复现的离线状态机生成
   `tick,direction,up,down,dash` 路线文件，交给原生回放。
   这是下一步的主要工作，也是"状态机"这一交付物的载体。
   - 起点：`tests/Chaite.Tests/FishronNoHitLab.cs` 里已有的控制器
     （`PredictiveDodge` / `CorridorEscape`）可以复用为**假设生成器**，
     但**必须知道它们的结论建立在错误的速度与错误的无敌帧上**（见 §0）；
   - 更好的是**重写一套面向原生观测的状态机**，输入直接取
     `charge-observations.jsonl` / `prehit-observations.jsonl` 的字段；
2. **按 13.87 / 14.50 重新评估全部旧结论**——实验台用 12.0，
   **低于真实速度**；
3. **两套配装各写一套**：弱翼 761 已确认；
   **强翼套（猪鲨翅膀）的原生 `accRunSpeed` 与实测峰值尚未测**，
   需一次原生运行补齐；
4. **三阶段克盾反冲**：现在 `npc contact` 可观测，
   且原生链路证明冲刺可用，**这一机制终于可以在原生里真正验证**；
5. **场地与环境的鲁棒性**：用户明确警告"场地或环境问题会导致状态机失效"，
   所以每一条被接受的路线都应**至少在两种开局长边（left/right）各跑一次**。


---

## 6. 原生诊断：为什么现有公式电路会死

用 `tools/run-native-acceptance.ps1 -FormulaRoute fishron-fairy-wing` 跑
**现有已审电路**（`src/Chaite.Core/FishronWingScript.cs`），原生结果：

```
ticks=10253  boss damage=87  death=True  hits=12
```

**12 次受击 = 8 次本体（npc 370）+ 4 次鲨鱼龙卷（projectile 384）。**
`npc contact=0`，即电路**从不主动撞本体**——所以三阶段的克盾反冲
在这套电路里根本没被使用。

### 6.1 逐 tick 证据（`CHAITE_PROBE_DENSE_FRAMES=1`）

开启密集帧后 `boss-observations.jsonl` 是**每 tick 一行、整场 10253 行**，
含玩家 `position/velocity/wingTime/dashDelay/eocDash`、boss
`position/velocity/ai[0..3]`、以及**实际生效的 `control*` 位**。

第一次本体受击（tick 4182）前 24 tick：

```
 tick |    plX    plY   plvx  plvy  wing  dd   eo |    boX    boY   bovx  bovy  bs  bs2 bs3 | L R U D J dash | plan phase
 4158 |   2879   6012    5.1   6.1     0   25   10 |   2492   5780   14.7   8.5   1    8   5 | 1 0 0 1 1    0 | charge-descend
 4162 |   2896   6038    3.8   6.7     0   21    6 |   2551   5814   14.7   8.5   1   12   5 | 1 0 0 1 1    0 | charge-descend
 4163 |   2900   6038    3.5   0.4     0   20    5 |   2566   5822   14.7   8.5   1   13   5 | 1 0 0 1 1    0 | charge-descend
 4170 |   2915   6045    1.2   1.4     0   13    0 |   2669   5882   14.7   8.5   1   20   5 | 1 0 0 1 1    0 | charge-descend
 4178 |   2915   6061   -0.6   2.4     0    5    0 |   2786   5950   14.7   8.5   0    0   7 | 1 0 0 1 1    0 | charge-descend
 4180 |   2914   6066   -0.8   2.7     0    3    0 |   2812   5964   12.5   6.3   0    2   7 | 1 0 0 1 1    0 | personal-space
 4181 |   2913   6069   -0.5   2.8     0    2    0 |   2824   5969   11.4   5.2   0    3   7 | 0 1 0 0 0    1 | personal-space
 4182 |   2918   6065    4.5  -3.5     0    1    0 |   2834   5973   10.3   4.1   0    4   7 | 0 1 0 0 0    1 | personal-space  <== 受击
 4184 |   2937   6059   14.5  -3.0     0   -1   15 |   2851   5978    8.1   1.9   0    6   7 | 0 1 0 1 1    1 | personal-space
```

**读出来的事实：**

1. **boss 冲刺 (`bs=1`) 期间速度恒为 `(14.7, 8.5)`，模长 17.0** ——
   与原生 `ChargeSpeed=17` 一致；**冲刺方向一次锁定，中途不改**；
2. **tick 4178 起 `bs` 由 1 → 0，`bvx` 从 14.7 递减到 8.1** ——
   这是**减速追击段**，boss 仍在逼近；
3. **受击瞬间水平间隙只有约 25 px**（`2913+10` 对 `2834+75`），
   **垂直间隙 96 px**，而双方半高之和仅 71 px ——
   **不是"撞上"，是玩家直接落在 boss 横向覆盖范围内被包住**；
4. **电路在受击前 24 tick 里让玩家几乎静止**（`|plvx| ≤ 5`，tick 4163 起
   还只有 2.8 → 0.2），`wingTime=0` 说明**在地面上**；
5. **tick 4184 才发出冲刺**（`dd` 由 1 变 -1、`eo=15`）——
   **冲刺比受击晚 2 tick**，即"知道要躲但来不及"。

### 6.2 结论

**现有电路的失败模式不是"躲错方向"，而是"在冲锋到达前把速度掉到零"。
`FishronWingScript` 有 `StandoffPixels = 720` 这个常量，但从轨迹看
它并没有转化为持续的速度。**

对一个**方向锁死、固定 476 px 直线行程**的冲锋，真正的解法是
**保持垂直于冲锋轴的速度，而不是保持距离**：

- 用户在任务说明里也强调过"**只要横向移动速度一直保持**"；
- 电路在 tick 4163 把 `plvy` 从 6.7 压到 0.4、`plvx` 压到 3.5，
  **等于把自己钉在冲锋路径上**。

而 `dist` 与是否受击**不相关**（受击的最小 dist=197、最大的 dist=1020；
未受击的 dist 里有 233、197 这类小值），所以**"保持距离"这个判据是错的**，
**"保持速度"才是对的**。

### 6.3 下一步

1. 按 §6.2 重做电流：**冲锋临近（`bs∈{1,6,11}` 或 `bs2` 接近结尾）时
   必须保持满速，且优先沿冲锋轴的垂直方向**；
2. 鲨鱼龙卷（384）的 4 次受击要先读 `hostileProjectiles` 的
   `beforeUpdate` 快照定位，再用"记住龙卷列并绕开"处理；
3. 三阶段克盾反冲改成**主动撞本体拿无敌帧**——`npc contact` 目前恒为 0，
   这一机制**未被使用**，而它是三阶段的关键。

---

## 8. Two corrections from rounds 47-48 (owner-verified)

### 8.1 The boss starts on the player's side, not across the arena

`tools/GameProbe.cs:3255` spawns the direct boss at

    spawnX = player.Center.X + 640
    spawnY = player.Center.Y - 260

and the player starts at `PlayerStartTileX = ArenaGroundLeft + RunwayStartInsetTiles` =
tile 21. So the boss begins 640 px to the side and 260 px above the player: **the same
side of the arena.** A charge travels a fixed 476 px, and the arena is 399 tiles wide
(6384 px), so a charge cannot cross the arena at all -- the boss must close the gap first.

Recorded player position range over a full fight: **640 .. 6723** (span 6083 px). The
player roams the arena; it is not trapped at an edge.

Consequence: the round-47 claim "away from the boss points into the wall because the boss
is on the far side" was **wrong**, and the `WallApproachMargin` reversal built on it was
**reverted**. Reversal engages exactly at the band edge again.

### 8.2 `wingTime == 0` is the landing frame, not an exhausted budget

`Player.cs:26996` refills the flight budget under

    if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump && justJumped))
        wingTime = wingTimeMax;

`autoJump` is **true on all 8685 measured rows**, and `Player.cs:20864` sets `justJumped`
on landing whenever `autoJump` is set. The second clause therefore applies, so the budget
refills normally and `releaseJump` is not required.

Supporting measurement: of the 4896 rows with `wingTime == 0`, only **19** have
`velocity.Y` exactly 0 -- which is what landing looks like. `wingTime == 0` is the last
flight tick before the refill lands, not a budget that stays empty.

Consequence: the round-47 "empty budget for 56% of the fight" reading was **wrong**, and
the apex tap built on it (`ApexVelocityTolerance` / `_apexTapped`) was **reverted**.

### 8.3 What actually survives

| fact | value |
|---|---|
| player speed at body contact | ~**4.5** px/tick |
| boss speed at body contact | **6.4 .. 22.9** px/tick |
| `maxRunSpeed` (foot speed) | **4.71** |
| wing cruise (measured) | **13.87** p95, max 14.50 |
| contacts with a FULL wing budget | **2 of 8** (wingTime 130 @3019, 119 @3807) |

At contact the player is moving at roughly foot speed (`maxRunSpeed 4.71`) while the boss
closes three to four times faster, and two contacts happen with a full budget in hand.
So **neither direction choice nor wing exhaustion is the binding constraint** -- the open
question is why the player is on foot (4.5 px/tick) rather than flying (13.87) at the
moment of contact.

## 9. Round 48: the controls the player actually receives

The prehit stream (`prehit-observations.jsonl`, 48 rows per hit) is the first artifact that
records the **applied** controls alongside the plan. Reading the window before hit 1
(hurtTick 3019) is decisive:

```
 off  tick   vx     vy   wing | L R U D J Dash | phase
 -44  2975  0.00   8.62     0 | 0 0 0 1 1    1 | fishron-wing-precharge-jump
 -32  2987  0.00  10.01     0 | 0 0 0 1 1    1 | fishron-wing-precharge-jump
 -24  2995  0.00  10.01     0 | 0 0 0 1 1    1 | fishron-wing-charge-horizontal-dash
 -12  3007  0.00  10.01     0 | 0 0 0 1 1    1 | fishron-wing-charge-horizontal
  -4  3015  0.00  -6.21   130 | 0 0 0 1 1    1 | fishron-wing-charge-horizontal
   0  3019  4.50  -3.50   130 | 0 1 0 1 1    1 | fishron-wing-personal-space
```

For **44 consecutive ticks** of an incoming charge the player receives **no horizontal
input at all** (`L 0`, `R 0`), holds `Down`, and falls at `vy 10.01` (= `maxFallSpeed`).
Horizontal input appears only on the frame of the hit itself. So the player is not
"choosing a bad direction" -- it is receiving **no direction**.

### 9.1 The script is not the source of the zero

Instrumenting `FishronWingScript`'s return (gated on `CHAITE_SCRIPT_TRACE`, since removed)
over 3160 ticks shows it **never returns a neutral horizontal**:

```
 phase / hor        count        phase / hor / vert     count
 precharge-jump  +1   400        precharge-jump  +1 -1    400
 precharge-jump  -1   390        precharge-jump  -1 -1    390
 charge-horizontal -1 222        tornado-clear   -1 +1    256
 charge-horizontal +1 199        bubble-line     +1 +1    240
```

and instrumenting the last write to `plan.Horizontal` in `PlanFormula` (gated on
`CHAITE_GUARD_TRACE`, since removed) gives

```
 rows where scriptHor != 0 but planHor == 0 : 0
```

so the planner's pledge is `+/-1` on every tick.

### 9.2 Ruled out: the neutral-hold safety gate

`BossStrategyCatalog` line 7240 calls `PriorityBossThreatGate.TryGetNeutralHoldReason`,
and `UnmodeledThreatSafetyHold` (line 7731) sets `HoldNeutralControls = true`, which
`TerrariaFacade.ApplyPlan` honours by clearing controls -- exactly the observed `L 0 R 0`.
The gate fires for a Fishron threat of type 384..386 (bubble / shark / tornado) whose
native trajectory has no proven envelope (`HostileProjectileMotion.cs:779-796`).

This is **not** the cause on the current path, because `CombatPlanner.Plan` dispatches the
formula route first and returns:

```
 CombatPlanner.cs:536  if (_formulaRoute != FormulaRoute.None)
 CombatPlanner.cs:537      return PlanFormula(snapshot);
 CombatPlanner.cs:566  if (directive.HoldNeutralControls)   // unreachable on that route
```

So `PlanFormula` runs and the hold branch is never reached.

### 9.3 Open, and stated as open

The planner pledges `+/-1`, the script pledges `+/-1`, and the game receives `0`. The
44-tick window above says the controls are cleared rather than steered, but **the code
that clears them has not been identified.** Plausible remaining sites are the
non-`PlanFormula` control path in `TerrariaFacade` (line 3720 and the pending-control
writes around 3910) and any consumer that runs after `PlanFormula` returns. This has not
been measured, and it should be measured by instrumenting `ApplyPlan` from the probe
(`GameProbe.ObserveApplyPlanBefore` already receives the `ControlPlan` by value) rather
than by inferring it.

### 9.4 The neutral-hold gate is still a real defect for this fight

Independently of 9.2, the gate as written would neutral-hold the player whenever a
Fishron bubble is live without a proven trajectory sample, and the owner's own reading is
that **bubbles are a one-hit non-threat** whose only requirement is that horizontal speed
be maintained. A hold that zeroes horizontal speed is therefore the opposite of the
correct response to a bubble. This should be narrowed or exempted for the formula route,
but it was **not** changed this round because it is not on the measured failing path and
changing it could not be validated against this failure.

## 10. Round 49: horizontal input continuity, and the acceleration constraint

The owner supplied the missing mechanism: Terraria has **acceleration**, so horizontal
speed must be **maintained continuously**. A player that taps a direction and then stops
loses the accumulated speed and is effectively stationary, and because vertical
acceleration is plentiful while horizontal is not, the horizontal axis is the one that has
to be held. The dash exists to bring the speed up quickly when it has been lost.

The measured player state agrees with this reading:

```
 maxRunSpeed        4.71     runAcceleration  0.1256
 moveSpeedDebuffFactor 1     runSlowdown      0.2
```

`runAcceleration` 0.1256 means reaching the 4.71 cap from rest takes roughly 38 ticks, and
losing it takes far less. So a gap of even a few ticks without horizontal input leaves the
player far below the speed needed to clear a charge closing at 14.7 to 17.0.

### 10.1 Input continuity, measured

Across 8685 dense ticks of a full fight:

| metric | value |
|---|---|
| ticks with horizontal input held | **5822 / 8685 (67%)** |
| number of separate held runs | **93** |
| longest held runs | **368, 353, 337, 320, 288, 267, 220, 213, 210, 202, 192, 186** |
| median \|vx\| | 6.70 |
| p90 \|vx\| | 12.67 |
| max \|vx\| | 14.50 |
| ticks with \|vx\| < 0.5 | **1128 (13%)** |

So the input is *mostly* continuous -- long runs of 200 to 368 ticks are the norm -- but
there are **93 breaks**, and 13% of the fight is spent effectively stationary. The breaks
are not the general case; they are the specifically fatal case, and they cluster at the
body contacts.

### 10.2 The canonical break

The window before hit 1 reproduces it exactly (from the prehit stream):

```
 off  tick   vx     vy   wing | L R U D J Dash | phase
 -44  2975  0.00   8.62     0 | 0 0 0 1 1    1 | precharge-jump
 -32  2987  0.00  10.01     0 | 0 0 0 1 1    1 | precharge-jump
 -24  2995  0.00  10.01     0 | 0 0 0 1 1    1 | charge-horizontal-dash
 -12  3007  0.00  10.01     0 | 0 0 0 1 1    1 | charge-horizontal
  -4  3015  0.00  -6.21   130 | 0 0 0 1 1    1 | charge-horizontal
   0  3019  4.50  -3.50   130 | 0 1 0 1 1    1 | personal-space
```

44 ticks with `L 0 R 0`, `Down` held, falling at `vy 10.01` (= `maxFallSpeed`), at
`plX` 640. The horizontal input appears only on the hit frame, where the plan finally
switches to `personal-space`, which is far too late to build speed.

### 10.3 What was ruled out, and what is still open

Instrumenting every stage of the pipeline shows the decision layer is *not* asking for a
stop:

| probe point | result |
|---|---|
| `FishronWingScript` return, 3160 ticks | never returns a neutral horizontal (only `+/-1`) |
| last write to `plan.Horizontal` in `PlanFormula` | `horAfter == 0` on **0** rows |
| value leaving `PlanFormula` (`returnHor`) | `0` on **0** rows |
| `ApplyPlannedOutput` delta (`horBefore` vs `horAfter`) | mismatches on **0** rows |

But the probe's own hook at the first instruction of `ApplyPlan` reports
`hor == 0` on **1000 of 3160** rows, all of them `route=FishronFairyWingsDash` and
`strategy=formula-fishron`, including **196** `precharge-jump` rows and **177**
`charge-horizontal` rows.

That is a direct contradiction with the layer-by-layer trace and it is **not resolved**.
Two candidate explanations remain, and both are testable:

1. The probe's `ObserveApplyPlanBefore` reaches the game through a path where the plan is
   a different instance or a stale copy, so its `hor == 0` rows are not the plan
   `PlanFormula` returned.
2. `ApplyPlan` is invoked more than once per tick on some ticks, and the applied call is
   one whose plan was produced by a different route.

Until one of these is settled by measurement, the cause of the 44-tick dead window is
**open**, and no change should be made on the assumption that the planner is at fault.

### 10.4 Direction for the next round

The actionable statement does not depend on 10.3. Whatever clears or omits the horizontal
input, the requirement is the owner's: **never let the horizontal axis drop to zero**, and
use the dash to restore speed quickly rather than as an attack timing aid. Concretely, a
guard belongs at the point the input is finally applied, not in the decision layer, and it
should assert "horizontal input is held unless the arena edge makes that direction
impossible". That is checkable with the same continuity statistic used in 10.1: the 93
breaks and the 1128 near-zero ticks are the target, and a correct fix drives those to near
zero without needing the plan-level contradiction to be resolved first.

## 11. Round 50: the continuity guard was tested and refuted

Round 49 left two things: the owner's requirement that horizontal input must never be
released, and an unresolved contradiction about where the neutral horizontal comes from.
This round tested the requirement directly rather than continuing to chase the trace.

### 11.1 `ApplyPlan` runs exactly once per tick

The first candidate explanation for the contradiction is now dead. Instrumenting the probe's
`ApplyPlan` entry hook with the tick number gives

```
 ApplyPlan calls per tick -> how many ticks: {1: 3160}
 ticks with >1 call     : 0
 zero-hor rows          : 1000
 zero-hor rows on ticks with more than one call : 0
```

So the applied call and the traced call are the same call, and the contradiction in 10.3
stands unresolved rather than being explained by double invocation.

### 11.2 The guard, and why it is reverted

I implemented the owner's requirement at the point the controls are actually written --
`TerrariaFacade.ApplyPlan`, immediately after the normal `controlLeft` / `controlRight`
writes, so it cannot be overridden by any upstream layer:

- if the plan's horizontal is 0 while a Fishron is in charge state 1, 6 or 11, hold a
  horizontal direction instead of releasing, and request a dash when the native dash is
  ready, because a dash writes `velocity.X` in the facing direction and is the only fast
  way to restore lost speed;
- the direction is the perpendicular to the charge line, resolved to the side that opens
  the gap to the boss.

It **improved continuity exactly as intended and still lost**:

| run | horizontal input held | \|vx\| < 0.5 | ticks | hits | boss damage |
|---|---|---|---|---|---|
| baseline (no guard) | 5822 / 8685 (67%) | 1128 (13%) | 8684 | **8** | 37 |
| guard, raw perpendicular sign | 6458 / 8243 (78%) | 822 (10%) | 8242 | 12 | 153 |
| guard, perpendicular resolved away from boss | -- | -- | 7564 | 11 | 105 |

The first version took the raw cross product, which picks whichever perpendicular has
positive orientation; half of those fly the player *into* the incoming boss. Correcting the
sign to always open the gap changed the result from 12 hits to 11, so the sign mattered, but
**both signs are worse than doing nothing**, and the corrected version also produced the
first `npc contact 1` seen in any run.

The guard is therefore **reverted** and `TerrariaFacade` and `GameProbe` are back at their
previous revisions. The pinned native result returns to `validBattle True`, ticks 8684,
hits 8, boss damage 37.

### 11.3 What the refutation establishes

This is a real result, not just a failed edit. Continuity is **necessary but not
sufficient**, and the guard's mechanism of improving continuity while overriding the
script's direction made the fight *worse*. That is direct evidence that:

1. the reviewed circuit's **direction choice is better than a geometric perpendicular**
   computed at the facade, and
2. the 13% of ticks at `|vx| < 0.5` are **not** by themselves the binding constraint --
   otherwise holding the axis continuously would have helped.

So the open question is not "why is the horizontal zero" but "**when** the player should
spend its horizontal speed and when it should keep it", which is a scheduling question
about the circuit, not a missing-input bug to be patched at the application layer.

### 11.4 Consequence for the next round

The next round should not add another facade-level guard. It should test the scheduling
hypothesis inside the reviewed circuit, where the state machine already knows the phase:
for instance, keeping the horizontal axis under the circuit's control across a phase
transition rather than letting it fall to neutral, and measuring with the same continuity
statistic plus the hit count. No native zero has been observed on either loadout.

## 12. Round 51: two facade overrides tested, both refuted

Rounds 50 and 51 tested the two most plausible input-level explanations of the 44-tick dead
window. Both made the fight worse, and the pattern of results is itself the finding.

### 12.1 Run-length structure

Before testing, the input structure was measured over 8685 dense ticks:

| metric | value |
|---|---|
| direction reversals while moving | **21** |
| held runs | 93 |
| runs of 1-3 ticks | 20 |
| runs of 4-10 ticks | 19 |
| runs of 11-30 ticks | 21 |
| runs of 31-100 ticks | 13 |
| runs of 100+ ticks | 20 |
| ticks with \|vx\| < 0.5 | 1128 (13%) |

So the input is **not** chattering: only 21 reversals in the whole fight, and 33 runs are
longer than 30 ticks. The problem is not that the circuit flip-flops. It is that **39 of 93
runs are 10 ticks or shorter**, and 1128 ticks end up near stationary.

### 12.2 Down is the dominant control

| group | ticks | median \|vx\| | \|vx\| < 0.5 | median vy |
|---|---|---|---|---|
| `Down` held | 7144 | 6.70 | 12% | -0.21 |
| `Down` released | 1541 | 4.50 | 19% | 2.75 |

`Down` is held on **82%** of the fight, and **28% of those ticks (1995) carry no horizontal
input at all**. The window before the first contact is exactly that state for 44 ticks.

### 12.3 Override A - hold the horizontal axis (round 50)

Enforced in `TerrariaFacade.ApplyPlan`, after the normal left/right writes, so no upstream
layer could undo it. Direction was the perpendicular to the charge line, corrected to the
sign that opens the gap to the boss. Also requested a dash when the native dash was ready.

| run | input held | \|vx\| < 0.5 | ticks | hits | damage |
|---|---|---|---|---|---|
| baseline | 67% | 1128 (13%) | 8684 | **8** | 37 |
| raw perpendicular sign | 78% | 822 (10%) | 8242 | 12 | 153 |
| perpendicular away from boss | -- | -- | 7564 | 11 | 105 |

Continuity improved exactly as designed and the fight still got worse. **Reverted.**

### 12.4 Override B - release `Down` during a charge (round 51)

Narrower: keep the circuit's own horizontal direction, and only stop feeding the descend
while a charge is live, on the reasoning that the charge is escaped on the horizontal axis
and a descend only walks the player towards the floor where no speed can be built.

| run | ticks | hits | damage | npc contact |
|---|---|---|---|---|
| baseline | 8684 | 8 | 37 | 0 |
| `Down` released during charge | **4229** | 8 | **90** | **4** |

Death arrives at **half the tick count**, damage more than doubles, and body contact goes
from 0 to 4. **Reverted.**

### 12.5 What this establishes

Both overrides replaced one of the circuit's own decisions with a locally reasonable rule,
and both lost. Taken with round 50 that is a consistent picture:

1. **The circuit's per-tick input is not the defect.** Its decisions are already better
   than the local rules tested at the facade, in both the horizontal axis and the vertical.
2. **The 13% of ticks at `|vx| < 0.5` are a symptom, not the cause.** Making them continuous
   by overriding direction, or removing the paired descend, both made the outcome worse.
3. Therefore the eight remaining body hits come from the circuit's **own choice of position
   and timing**, and the fix has to change what the state machine decides -- not enforce a
   property on its output.

This closes the line of work opened in round 49. No further facade-level input guard should
be attempted without a measured reason to believe the circuit's own decision is wrong at a
specific, identified tick.

### 12.6 Where the effort should go

The objective needs the strong-wing loadout measured natively at all, which has never been
done, and needs the state machine to be delivered through `CHAITE_ROUTE_FILE` rather than
only through the formula route. Both are concrete and unblocked, and both are prerequisites
for the acceptance the objective asks for regardless of how the weak-wing hits are reduced.

## 13. Round 52: the strong-wing loadout measured, and the real contact signature

Two results this round: the strong-wing loadout's first native measurement, and a
correction to how every previous hit was being read.

### 13.1 Strong wing (Fishron Wings) measured natively for the first time

`-FormulaRoute fishron-strong-wing` runs the same fight with `armor[4] = 2609`
(Fishron Wings) instead of 761 (Fairy Wings), asserted in the run header.

| property | weak (Fairy) | strong (Fishron) |
|---|---|---|
| `wingTimeMax` | 130 | **180** |
| `wingsLogic` | 6 | **26** |
| fastest measured climb | `minVy` -9.9 | **-16.5** |
| median \|vx\| | 6.70 | **7.90** |
| p90 \|vx\| | 12.67 | 12.97 |
| max \|vx\| | 14.50 | 14.50 |
| ticks at `wingTime == 0` | 56% | **63%** |
| held runs / short (<=10) | 93 / 39 | 116 / 41 |
| longest held run | 368 | **1109** |
| **hits** | **8** (death, 8684 ticks) | **11** (alive at the 11000 cap) |
| boss damage taken | 37 | 76 |
| npc contact | 0 | 3 |

The strong wing has strictly better mobility on every axis -- 38% more flight budget, more
than double the wings logic tier, 67% faster climb, and a higher median horizontal speed --
and it takes **more** hits, not fewer. Hit phases differ too: the weak loadout's hits are
concentrated in `personal-space` (3) and `charge-descend` (3), while the strong loadout's
spread across `charge-descend` (3), `charge-horizontal` (2), `charge-ascend` (2) and
`bubble-line` (2).

That is a direct measurement that **vertical mobility is not the binding constraint**, and
it contradicts the "strong and weak wings need two different state machines because their
vertical mobility differs" premise in an important way: the difference does not show up as
a vertical-mobility advantage, because the failure is not vertical.

### 13.2 Correction: 4.50 / -3.50 is knockback, not the player's motion

Every hit in both loadouts records `|vx| = 4.50` and `vy = -3.50` at the hit tick. I had
been reading that as the player's own speed and concluding "the player is always slower than
the charge". Comparing the tick before each hit to the hit tick disproves it:

```
 hitTick  vx(before) vy(before) | vx(hit) vy(hit)
    3019     0.00     -6.21   |    4.50   -3.50
    3807    -0.24     -7.21   |    4.50   -3.50
    8282    12.97      7.20   |   -4.50   -3.50
    8322    12.39      2.23   |   -4.50   -3.50
```

The pre-hit value ranges from -12.4 to +13.0 and bears no relation to the post-hit value,
which is a fixed magnitude in the direction away from the boss. So `4.50 / -3.50` is the
**knockback the hit applies**, not the player's state. The earlier claim that the player is
"consistently three to four times too slow at contact" was an artifact of reading a
post-collision value, and is withdrawn.

### 13.3 The real pre-hit picture

Measured on the tick before each hit, across both loadouts (19 hits):

| metric | value |
|---|---|
| pre-hit \|vx\| median | **0.87** |
| pre-hit \|vx\| < 1.0 | **10 / 19** |
| pre-hit \|vx\| >= 6.0 | 5 / 19 |
| pre-hit `wingTime == 0` | **14 / 19** |

```
 tag      tick  |vx|   vy   wing
 WEAK     3019  0.00  -6.21   130
 WEAK     7204  0.00   1.55     0
 WEAK     7884  0.00   3.34     0
 STRONG   4569  0.00   3.34     0
 STRONG   2144  0.10   3.34     0
 STRONG   9470  0.16   2.13     0
 WEAK     3807  0.24  -7.21   120
 ...
```

**Ten of nineteen hits are taken from a near standstill, and fourteen of nineteen are taken
with an empty wing budget, falling at `maxFallSpeed` (+3.34).** So the two failure modes are
the same one: the player is caught with no horizontal speed and no flight left, in the air
and descending. The five hits taken at speed (6.9 to 12.97) show the circuit *can* be
travelling fast; it just is not, at the moments that decide the outcome.

### 13.4 What this changes

The earlier framing -- "the player arrives too slow, so pick a better direction" -- is
retired, and with it the reason the two facade overrides in section 12 failed: they changed
the direction of an input that was already absent or nearly absent. The measurement now
points at **arriving at the contact with the horizontal axis already moving and the flight
budget not empty**, which is a scheduling property of the circuit rather than a steering
property.

Note also that `wingTime == 0` on 56-63% of the fight is not by itself the bug: section 8.2
established that the budget refills on landing. What matters is that it is empty
*at the contact*, which means the circuit is spending the budget earlier in the cycle than
the contact needs it.

## 14. Round 53: the script asks to move and the applied control is cleared

This round finally caught both sides of the same tick in one run, with the script's own
output and the per-tick applied controls recorded together. The result is unambiguous.

### 14.1 The script never returns a neutral horizontal

Over 3360 planner calls:

```
 hor distribution : {-1: 1627, 1: 1733}      <- zero occurrences of 0
```

On the rows where the player is stationary (`|vx| < 0.5`, 163 rows), the script is still
commanding a direction, and overwhelmingly the same one:

```
 rows with |vx| < 0.5 : 163
   their hor distribution : {-1: 153, 1: 10}
   their state distribution: {0: 51, -1: 74, 1: 33, 3: 5}
   their phase counts      : standoff 85, precharge-jump 37,
                             charge-horizontal 33, sharknado-exit 5,
                             personal-space 3
```

### 14.2 The applied control disagrees with the script, per tick

Aligning the script trace against the per-tick observation stream (which records the
controls the player actually received):

```
  idx  bossTick  L R   player vx | scriptHor
 1213     1214  1 0      -6.65   |    -1
 1214     1215  0 0      -6.55   |    -1
 1215     1216  0 0      -6.45   |    -1
 1216     1217  0 0      -6.35   |    -1
 ...
 1227     1228  0 0      -5.25   |    -1
 1228     1229  1 0      -5.30   |    -1
 1229     1230  0 0      -5.20   |    -1
 1230     1231  1 0      -5.25   |    -1
```

Across the whole run, **1439 of 3600 ticks have no horizontal control applied at all**,
while the script is asking for `-1` on essentially every one of them.

### 14.3 This is exactly the owner's acceleration effect, and it explains every earlier failure

The player's velocity keeps drifting left (`-6.65` down to `-5.20`) while `L` is 0, because
Terraria decelerates a moving player gradually (`runSlowdown` 0.2) rather than stopping it.
When the input returns it only briefly (`L` 1 for one tick, then 0 again), the speed never
rebuilds. Held against `runAcceleration` 0.1256 and `maxRunSpeed` 4.71, this is precisely
the owner's description: **intermittent input leaves the player nearly stationary, and speed
has to be held continuously to accumulate.**

It also explains, after the fact, why both facade overrides in section 12 failed. They
changed the *direction* requested, but the defect is that the request is being **discarded**
on a large fraction of ticks. Overriding the direction of a value that is then thrown away
cannot help; and the one override that also forced `controlLeft`/`controlRight` after the
clear (override A) did improve continuity as measured, yet still lost -- which means the
clear is not the only thing wrong, only the first thing wrong.

### 14.4 Also ruled out this round

- **Direction reversals are not the cause.** Of the 19 known hits across both loadouts only
  two have a preceding direction flip, and 5 of the 19 are taken while moving fast
  (`|vx|` 5.10 to 12.97), which shows the circuit can travel quickly when the input sticks.
- **Stale controls are not the cause.** `controlsFresh` is `True` on all 528 prehit rows.
- **The 4.50 / -3.50 post-hit value is knockback**, already established in section 13.2.

### 14.5 The precise defect, for the next round

The applied control is cleared on ~40% of ticks while the planner has asked for a direction.
`TerrariaFacade.ApplyPlan` opens with `ClearCombatControls(player)` and then writes
`controlLeft`/`controlRight` from `plan.Horizontal`; the per-tick stream shows both false on
ticks where the script asks for `-1`. So either `ApplyPlan` is not reached on those ticks,
or it is reached with a plan whose horizontal is 0. The probe's `ApplyPlan`-entry hook
reported `hor == 0` on 1000 of 3160 rows, which points at the second possibility, but the
same run's `PlanFormula` return trace reported `hor == 0` on **0** rows. Those two cannot
both be true, and reconciling them is the single highest-value next step: instrument
`ApplyPlan` to write the plan's `PhaseId` **and** the value of `plan.Horizontal` **and** a
per-tick counter to one file, so the applied plan can be matched to the script invocation
that produced it. Whatever the answer, the fix belongs where the input is written, and it
must make the horizontal persistent rather than per-tick.

## 15. Round 54: the session gate is ruled out, and the contradiction is isolated

Section 14.5 proposed two explanations for the 1439 ticks that receive no horizontal control
while the planner asks for a direction. This round eliminated the first one.

### 15.1 The session gate is not the cause

`Runtime.cs:309` is the one place that clears controls without applying a plan:

```csharp
if (!update.ApplyControls || _game.IsDead(player))
{
    _game.ClearCombatControls(player);
    ...
    return;
}
```

Instrumenting that branch (gated on `CHAITE_GATE_TRACE`, since removed) produced **no trace
file at all** over a 3600-tick run, which means the branch never executed during the battle:
`update.ApplyControls` was true and the player was not dead on every tick. The plugin did
apply a plan on every tick of the fight.

That eliminates the "the plugin deliberately applies nothing" explanation, and it removes the
`EncounterController` session-state machine (`ApplyControls` is derived at
`EncounterController.cs:102/112/137-139` from `HasEncounter`, `PlayerDead` and the
respawn settle frame) from suspicion.

### 15.2 The contradiction, stated precisely

After this round the following are all measured, on the same builds, and cannot all be true
together:

| observation | value | how measured |
|---|---|---|
| script returns a neutral horizontal | **never** (0 of 3360) | `FishronWingScript` return trace |
| last write to `plan.Horizontal` in `PlanFormula` | 0 rows at 0 | `PlanFormula` trace |
| value leaving `PlanFormula` | 0 rows at 0 | `PlanFormula` return trace |
| `ApplyPlan` entry sees `hor == 0` | **1000 of 3160** | probe `ObserveApplyPlanBefore` |
| `ApplyPlan` runs per tick | exactly 1 | probe hook with tick counter |
| the runtime gate that skips `ApplyPlan` | never fires | `Runtime` gate trace |
| applied `L`/`R` both false | **1439 of 3600** | per-tick observation stream |

The two rows in bold conflict: the probe's `ApplyPlan`-entry hook and the per-tick
observation stream are both probe-side reads, and they disagree about whether the plan
carried a direction.

### 15.3 What is nonetheless established, and is enough to act on

Independent of the contradiction, the following are solid and already explain the observed
failure mode:

- the player is **near-stationary before 10 of 19 hits** (median pre-hit `|vx|` 0.87) and has
  **`wingTime == 0` before 14 of 19** (section 13.3);
- the input is **intermittent**, with 1439 of 3600 ticks receiving no horizontal control, and
  the player is seen **coasting on momentum** (velocity drifting -6.65 to -5.20 with `L` 0)
  rather than stopping, which is exactly the acceleration behaviour the owner described
  (section 14.3);
- the circuit **can** travel fast -- 5 of 19 hits are taken at `|vx|` 5.10 to 12.97 -- so the
  mobility is available and the problem is that it is not being sustained at the contact;
- the strong wing, which is better on every mobility axis, takes **more** hits (section 13.1),
  so neither vertical mobility nor wing budget size is the constraint.

### 15.4 Next step

The contradiction in 15.2 has to be settled before any further code change, because every
plausible fix depends on knowing whether the plan carries the direction. The decisive
instrument is a single file written from `ApplyPlan` that records, per call, a monotonically
increasing call counter **and** the plan's `PhaseId`, `Horizontal`, `Jump` and `Drop`, paired
against the script trace by call index rather than by tick. That pairing is what the two
existing traces lack, and it is the only reason they cannot be reconciled.

No code change should be made on the strength of the contradiction alone, and none was made
this round. The pinned native result is unchanged: weak wing `validBattle True`, ticks 8684,
hits 8, boss damage 37; strong wing 11 hits, alive at the cap. No native zero on either
loadout.

## 16. Round 55: the zero is introduced inside `Plan`, between `PlanFormula` and the return

Section 15.4 asked for a call-keyed pairing of the script trace and the applied plan. This
round built it, and it resolves most of the contradiction -- while leaving one precise
question open.

### 16.1 The pairing

Both traces were given a monotonically increasing call counter, and a third trace was added
around `ApplyPlannedOutput`. Aligning by call index gives clean 1:1:1:1 counts:

```
 script calls        3360
 PlanFormula returns 3360
 ApplyPlan calls     3360        (each tick has exactly one)
 Plan entries        3360
 dispatch rows       3360        route = FishronFairyWingsDash on every one
 extra Plan entries     0
```

### 16.2 Where the values diverge

| probe point | horizontal distribution |
|---|---|
| `FishronWingScript` return | **{-1: 1627, 1: 1733}** -- never 0 |
| `PlanFormula` return | **{-1: 1627, 1: 1733}** -- never 0 |
| `ApplyPlannedOutput` entry | -1 wherever the script says -1 |
| `ApplyPlannedOutput` exit | **never differs from its entry** (0 rows) |
| `ApplyPlan` entry | **{-1: 1047, 0: 1199, 1: 1114}** |

So the script returns a direction, `PlanFormula` returns that same direction, and
`ApplyPlannedOutput` does not modify it -- yet the plan that reaches `ApplyPlan` carries 0 on
1199 calls. Since `Plan` is entered exactly 3360 times, once per tick, and calls
`PlanFormula` exactly 3360 times, the zero is introduced **inside `Plan`, after `PlanFormula`
has returned and before `Plan` returns**.

### 16.3 The call-index alignment also revises section 14

With the call counters, the earlier per-tick comparison was misaligned by one call and by the
first tick, and the "1387 mismatches" of section 14.5 should be read through this correction:
the reliable figure is that `ApplyPlan` sees 0 on **1199 of 3360** calls, not 1439 of 3600
ticks. The qualitative conclusion is unchanged -- a large fraction of ticks receive no
horizontal input -- but the earlier number mixed in the pre-takeover idle window.

### 16.4 What this rules out, finally

- **Not the session gate**: `!update.ApplyControls` never fires (section 15.1).
- **Not a second `Plan` invocation**: entries equal applications exactly.
- **Not the evaluate path serving the fight**: the formula route is set on all 3360 entries
  and `PlanFormula` runs on all 3360.
- **Not `ApplyPlannedOutput` or `ApplyConsumables`**: `ApplyPlannedOutput`'s exit never
  differs from its entry.
- **Not the probe hook reading a stale value**: script, `PlanFormula`, and `ApplyPlan` all
  report exactly 3360 calls with the same route.

The remaining candidate is a write to `plan.Horizontal` on the return path of `Plan` that is
not on the `PlanFormula` path -- that is, after `PlanFormula` returns, inside `Plan` itself.
`Plan`'s `_formulaRoute != None` branch returns `PlanFormula(snapshot)` directly, so the write
must be reachable on that branch, which means it is in code that runs after the call returns
but before `Plan` returns, or in a `finally`-equivalent path. **This has not been found yet**,
and the next step is to read `Plan` and `PlanFormula` with this specific question rather than
adding more traces: find every statement that can execute after `PlanFormula` returns within
`Plan`.

### 16.5 No code change

No change was made this round; all instrumentation was removed and the tree is at its previous
revision. The pinned native result is unchanged: weak wing `validBattle True`, ticks 8684,
hits 8, boss damage 37; strong wing 11 hits alive at the cap, both loadouts
`FishronFairyWingsDash` / `FishronStrongWingsDash` respectively. **No native zero on either
loadout.**

## 17. Round 56: the native charge lock, and the owner's perpendicular rule

The owner supplied the mechanic that this document had been missing, and it is now confirmed
in the native code.

### 17.1 The lock, read from the decompile

`NPC.cs AI_069_DukeFishron`, in the `num28 == 1` case:

```csharp
ai[0] = 1f;  ai[1] = 0f;  ai[2] = 0f;
velocity = Vector2.Normalize(player.Center - center) * num7;
rotation = (float)Math.Atan2(velocity.Y, velocity.X);
```

`num7` is 17 in expert and 23 when enraged (`flag4`, below 15 percent life). The charge
velocity is computed **once, on the single tick the boss enters a charge state**, from the
player's position on that tick. States 1, 6 and 11 never rewrite `velocity`, and the state-1
wind-up (`ai[2] >= num6`, `num6` = 28 in expert) runs while the boss is already travelling at
that speed. The whole approach is therefore downhill from one decision made on one tick.

Measured over 40 locked charges in a native run: locked speed was 17.0 on 39 of them and 23.0
on one (the enraged case), and the boss entered the charge through state 1 on all 40.

### 17.2 Why the dodge must be perpendicular

`maxRunSpeed` is a measured 4.71 and the locked charge closes at 17. The charge line
therefore cannot be outrun, which is exactly the owner's point: pulling away along the charge
direction does not work because the player is too slow. The only axis that works is normal to
the locked line, and the owner's rule is the practical form of that geometry -- dodge
diagonally up when the boss is above, diagonally down when it is below, and only run straight
away when the lock was taken from far enough out.

The measured lock geometry supports it. Over the 40 charges, lock distance ranged from 241.6
to 1183.0 px (median 405.5) and the boss was above the player at the lock on most of them.
For the near-horizontal locks (`|aim_y|` about 0.02) the required perpendicular is almost
entirely vertical, which is the diagonal; for the diagonal locks (`|aim_y|` about 0.5 to 0.7)
it has a large horizontal component, which is the run.

### 17.3 What the old circuit did, measured

The flee branch recomputed the horizontal every tick from the boss's current side. AI_069
crosses the player during a charge, so the sign was re-derived from a value that flips at the
crossing. Measured on the native stream, before the change: of 40 locked charges, **27 held
the correct normal for less than half the episode**, and the diagonal episodes spent **67 to
100 percent** of their length moving *opposite* the normal, i.e. back across the locked line.

### 17.4 The change

`FishronWingScript` now latches the perpendicular once per charge. `LatchChargeNormal` runs on
the transition into a charge state, computes the locked aim from the same geometry native
used, picks the perpendicular that increases the player's clearance from the locked line, and
stores its horizontal and vertical signs in `_chargeNormalHorizontal` /
`_chargeNormalVertical`, keyed by `_chargeNormalSequence`. `ChargeEscape` uses the latched
horizontal instead of the per-tick `away`, and the vertical beats follow the latched vertical
so that a normal pointing down is never overridden by an ascend beat.

### 17.5 The result is a null result, and the reason matters

Both loadouts measured **exactly** their previous figures after the change:

| loadout | before | after |
|---|---|---|
| weak (Fairy Wings) | ticks 8684, hits 8, damage 37, death | ticks 8684, hits 8, damage 37, death |
| strong (Fishron Wings) | 11000 ticks, hits 11, damage 76, npc contact 3 | 11000 ticks, hits 11, damage 76, npc contact 3 |

The latch is the correct implementation of the owner's rule and is kept, but it did not move
the outcome, so the locked-charge dodge is **not the binding constraint**. The dense stream
says what is.

### 17.6 What the dense stream says the hits actually are

Eight life drops, and the same signature on every one:

```
 tick   dx     dy    |vx|   vy   wingTime  bossState  L R J D
 3019   71.8   42.0   4.50  -3.50    130        0      0 1 1 1
 3807   46.9  -61.9   4.50  -3.50    119        1      1 0 1 1
 5801   12.2  -57.9   4.50  -3.50      0        1      1 0 1 1
 7204  -51.0  -65.8   0.00  -3.50      0        0      1 0 1 1
 7538   -1.4  -62.6   4.50  -3.50      0        0      1 0 1 1
 7884   16.3   51.4   4.50  -3.50      0        1      0 0 0 0
 8282  393.5  198.2   4.50  -3.50      0        1      0 1 1 1
 8322 -105.8  -15.0   4.50  -3.50      0        0      0 0 1 1
```

Three things follow, and they redirect the next round:

1. **`|vx| 4.50, vy -3.50` is identical on all eight**, which is the knockback signature
   already established in section 13.2, not the player's own motion. The last column pair is
   therefore not evidence about the approach.
2. **`wingTime` is 0 on six of the eight.** The player is out of flight budget at the contact.
3. **Contact is diagonal, not head-on.** Against a 150x100 boss and a 20x42 player, vertical
   contact needs `|dy| < 71` and horizontal needs `|dx| < 85`. Five of the eight sit inside
   both, but the pattern is a player with no flight budget being caught at 12 to 72 px on the
   diagonal, and two more are caught by Detonating Bubbles (type 384, `vx` exactly 0) at 440
   and 107 px rather than by the body at all.

So the remaining constraint is **flight budget plus the diagonal escape**, not the choice of
charge direction. Section 13.3 reached the same conclusion from the other end (`wingTime == 0`
before 14 of 19 hits) and this round confirms it on the post-change build.

### 17.7 Housekeeping

- `python tools/analyze-lock-geometry.py <run>` reports the lock geometry and the required
  perpendicular component per charge.
- `python tools/analyze-charge-episode.py <run>` reports the held fraction of the normal, the
  fraction spent opposite it, and the vertical correctness per episode.
- `python tools/analyze-damage-source.py <run>` reports every life drop with the boss state,
  boss distance and nearby hostile projectile types, which is how the bubbles were separated
  from body contacts.
- The test suite is at **750 passed, 8 failed**, and all 8 failures are a pre-existing missing
  fixture (`tests/Chaite.Tests/fixtures/observation-conformance.jsonl`), unrelated to this
  change.

**No native zero on either loadout.** The pinned figures stand at weak 8 hits / 37 damage /
death, strong 11 hits / 76 damage / alive at the cap.

## 18. Round 57: the flight budget is the real constraint, and it is a refill problem

Section 17.6 named flight budget as the remaining constraint. This round measured it directly
and then tested the obvious repair, which failed. Both the measurement and the failure are
worth recording.

### 18.1 The native refill, read exactly

`Player.cs:26992`:

```csharp
if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump && justJumped))
{
    wingTime = wingTimeMax;
    mount.ResetFlightTime(this);
}
```

`justJumped` is set at `Player.cs:20867` only when `sliding || velocity.Y == 0f`, i.e. on the
tick the player touches support. Refilling therefore requires a **landing**, or a
zero-vertical-velocity frame with the jump released. The arena places its platform rows 60
tiles apart (`GameProbe` `PlatformRowSpacingTiles`), which the probe's own comment calls
"about one wing charge of climb between layers".

### 18.2 Measured on the dense weak-wing stream (8446 planned ticks)

```
refills (wingTime returned to max): 23
ticks with wingTime <= 0          : 4896 / 8446  (58.0%)
ticks with justJumped (landing)   : 17  (0.2%)
airborne rows                     : 8351 / 8446  (98.9%)
airborne runs: 24   median 350   p90 559   max 759
airborne runs longer than one full budget (130): 22 of 24 (91.7%)
```

The player is airborne for **98.9 percent** of the fight and out of flight budget for **58
percent** of it. Only **23 refills** happen in 8446 ticks, and **22 of the 24 airborne runs are
longer than the entire 130-tick budget** -- median 350, longest 759.

Every one of the 17 landings refilled to the maximum, so the refill itself works perfectly.
The problem is purely that landings are rare: gravity only reaches the next platform row after
the budget is already spent, so the player oscillates between a full budget and an empty one
(`wingTime == 0` on 5076 of 8446 ticks, with the rest spread almost uniformly from 10 to 130)
instead of holding a comfortable reserve.

### 18.3 The repair that was tried, and why it did nothing

The first hypothesis was that `Down` was preventing landings: platforms are one-way, and in
Terraria holding `Down` makes the player fall through them. `Down` is indeed held on 7061 of
the 8351 airborne ticks (84.5 percent), so the theory was plausible.

A guard was added to `FishronWingScript` that released `Down` (`Vertical = 0`, `Jump = false`)
once `wingTime` fell to 35 percent of maximum, tagged `-refill` in the phase. It was written
twice:

1. **First form**, only on ticks where a descent was already commanded
   (`output.Vertical > 0`). Instrumented with `CHAITE_REFILL_TRACE`, it was reached **1453
   times in a 3000-tick run**, but `wingTime` was already **0 on 701** of those -- far too late
   to matter -- and the full-run result was **bit-identical** to the baseline.
2. **Second form**, acting on the budget alone regardless of phase, standing down only while an
   ascent was commanded. This fires on **5829 of 8446 ticks** (`wingTime <= 45`), and the
   result was **bit-identical again**: weak wing 8684 ticks, 8 hits, 37 damage, death; strong
   wing 11000 ticks, 11 hits, 76 damage, npc contact 3.

A follow-up check explains the null: of the 5829 guarded rows, `Down` was actually held on only
4540, so the guard was already changing far less than it appeared to, and the rows where it did
change something did not alter where the player ended up. **The guard was reverted**, because it
is an unverified change of my own that produced no measured effect; only the section 17 work,
which implements the owner's stated rule, remains.

The deeper reason a descent guard cannot fix this on its own is that the circuit is only
airborne-and-empty because it spends the entire budget before looking for a surface. Making the
descent start earlier does not create a landing that the fight's own geometry does not offer
within reach; the budget has to be **budgeted**, with landings scheduled across the fight
instead of discovered at the end of each run.

### 18.4 A false trail worth recording

The long airborne runs first looked like a falling-through-platforms bug, because a run showed
`Y 6026` to `6998` and the platform rows sit 960 px apart. That reading was wrong: `Y 6026` is
level flight, not a fall. The airborne `vy` histogram is bimodal at **-10 (1653 ticks)** and
**+10 (1255 ticks)** with only 252 rows near `|vy| <= 1`, so the player is climbing to the wing
ceiling or falling at terminal velocity almost all of the time. There is no hovering phase to
blame and no missing platform.

### 18.5 Also recorded

- A 3000-tick run reported `HITS 0` and `ACCEPTED: zero hits in the native engine`. This is
  **not** a zero-hit result and must not be read as one: the same run reported
  `boss damage: 0` and `boss life left: 78000`, so the fight had barely begun. Native
  acceptance is `hits == 0` over a run that actually fights, and section 12a's rule stands.
- New tool `tools/analyze-wing-budget.py` reports refills, empty-budget share, landing count and
  the airborne-run distribution against the budget.
- The pinned figures are unchanged: weak wing 8684 ticks / 8 hits / 37 damage / death; strong
  wing 11000 ticks / 11 hits / 76 damage / alive at the cap. **No native zero on either
  loadout.**

## 19. Round 58: the budget cycle, measured end to end

Section 18 left one thing unclear: whether the player ever lands once the boss is engaged, or
whether the 23 refills all belong to the opening. This round settled it, and the answer
reframes the problem.

### 19.1 Landings do happen during the fight

All 17 landings, with the boss's own state and life at that tick:

```
 tick   bossState  bossLife  phase
  240       -1       78000   standoff
  758        1       78000   charge-horizontal
  983        0       78000   precharge-jump
 1117        3       78000   sharknado-exit
 1486        2       78000   bubble-line
 1930        3       78000   tornado-clear
 3014        1       78000   charge-horizontal
 3781        1       78000   charge-descend
 4216        0       77999   charge-horizontal
 4568        1       77999   charge-descend
 5012        0       77999   tornado-clear
 5619        2       77999   bubble-line
 5896        0       77981   tornado-clear
 6318        0       77981   precharge-jump
 6680        1       77981   charge-horizontal
 7365        0       77981   standoff
 7926        0       77963   precharge-jump
```

Only tick 240 is pre-engagement (`bossState -1`). All 16 others happen with the boss alive and
fighting, and every one refilled `wingTime` to 130. Landing works throughout the fight.

### 19.2 The landings are 940 px apart, and the budget is 121 percent of that

The touchdown heights are exactly the arena's three surfaces, 960 px apart:
`Y 7952` (ground), `6992` (platform row 60), `6032` (platform row 120). Consecutive landings
are about **350 ticks** apart, and the pattern is fully regular:

- all 17 landings touch down at the same three heights,
- `wingTime` is 130 on every landing and reaches 0 about **130 ticks** later,
- the remaining ~220 ticks of each cycle are spent descending from one surface to the next,
- every airborne run starts at `wingTime 130` and ends at `wingTime 0`.

So the cycle is: **land, refill to 130, spend all 130 in the air, fall, land.** Measured
per-tick drain while both `controlJump` and `controlDown` are held is **0.48**, and those two
are held together on **5886 of 8351 airborne ticks (70.5 percent)** -- the wings stay deployed
while the player descends, which is what halves the drain rate.

The decisive number is the ratio: descents average **350 ticks** and one full budget is **130
ticks**, so the budget covers only **121 percent** of the usable cycle. It is exhausted just
before every landing rather than just after, which is why `wingTime == 0` on 58 percent of the
fight. The circuit is not wasting the budget and it is not failing to land; **the arena's
vertical spacing is marginally larger than one wing charge, and the descent is flown rather
than fallen.**

### 19.3 Two candidate fixes were tested and both were null

Both were reverted, because neither changed any measured value by even one bit:

1. **Free-fall descent.** All five "jump on takeoff, descend once airborne" sites
   (`vertical = player.OnGround ? -1 : 1`) were changed so the airborne half is a true descent
   (`0`). Instrumented with `CHAITE_FALL_TRACE`, the sharknado-exit branch was reached 66 times
   in 3000 ticks, 61 of them airborne -- but every one of those had `wingTime` 90 or 120, so
   that branch is not where the budget is spent. The full run was byte-identical: 8684 ticks,
   8 hits, 37 damage, death.
2. **Proactive refill descent** (section 18.3) had already been null.

The reason the descent is not the lever is now visible in the numbers: `controlDown` is held on
7061 of 8351 airborne ticks, so the player is already descending; the descent is simply
**slow**, and the budget is spent on keeping the wings open during it.

### 19.4 What this means for the next round

The binding quantity is not "land more often" -- the player already lands every 350 ticks and
refills fully. It is either

- **descend faster than the wings make it**, so the ~220-tick descent shrinks toward the 130
  that remain after the budget runs out, or
- **spend less than 130 in the air**, so a reserve survives to the next landing and the cycle
  stops oscillating between full and empty.

Both are one-parameter changes to the vertical policy during descent, and both need the actual
`controlJump` state during a measured descent to be pinned first, because the two null results
above show that changing `output.Vertical` in this circuit does not necessarily change what the
player receives. **That discrepancy -- `output.Vertical` versus applied `controlJump` -- is the
next thing to measure**, exactly as section 16 did for the horizontal axis.

### 19.5 Standing result

Unchanged and not to be overstated: weak wing 8684 ticks / **8 hits** / 37 damage / death;
strong wing 11000 ticks / **11 hits** / 76 damage / alive at the cap. A separate 3000-tick run
again reported `HITS 0` while also reporting `boss damage 0` and the boss at full life, so it is
not a zero-hit result. **No native zero on either loadout.**

## 20. Round 59: the footage, measured by pixels (the vision API cannot see)

The owner asked me to use the two tutorial videos with their full no-hit fight recordings instead
of reasoning from the decompile alone. The videos are on disk
(`tmp/video/v1.mp4`, `tmp/video/v2.mp4`, both 1920x1080 at 30 fps, 599 s and 523 s), and the
subtitle OCR in `docs/fishron-video-subtitles.md` already gives the spoken formula. This round
tried the vision route and then did the measurement directly, because the vision route does not
work.

### 20.1 The vision API returns confident hallucinations

`tools/vision-read-sheet.py` posts a contact sheet to the configured vision endpoint with a
base64 data URL. It returns HTTP 200 and plausible-looking markdown, but the content is
fabricated. Two outputs, both clearly impossible:

- A 48-frame sheet of the phase-1 fight came back with **every tile identical** on boss
  position ("above (1)"), on player horizontal action ("still"), and with a metronomic
  "rising, rising, falling, falling, rising, rising" pattern repeating every five tiles. Its
  answers then asserted the player is ~90 percent vertical, ~2-3 tiles of travel, and never runs
  horizontally.
- A 12-frame sheet at 12 fps, meant to isolate a single charge, came back with the player's
  x-position advancing by exactly **0.02 per tile in a perfect linear ramp**, **zero vertical
  travel**, and **zero vertical reversals**. A sprite in a Terraria fight cannot move at a
  constant linear velocity for twelve consecutive frames while the boss charges.

Subagents cannot substitute for this either: `read_image` is unavailable to the child route
whatever model is named, so all four vision subagents returned "cannot read images".

**Conclusion: the video's pixel content must be measured, not described.** The wording below is
therefore restricted to what `tools/track-video-sprites.py` computes.

### 20.2 The measurement, phase-1 fight (165 s, 80 s at 10 fps, 800 frames)

The player is located as the tightest cluster of the player's cyan tint (mask: blue and green
above 190, red below 170, both differences above 50). The mask finds a candidate in **every
frame**, 262 to 650 pixels.

```
total |dx| = 11946.0 px   total |dy| = 6646.0 px
vertical share of travel = 35.7%
vertical direction reversals: 318
x range 108.5..826.0   y range 57.0..326.0
```

Read against the current circuit, this is informative:

- **The player really does move vertically, a lot.** 35.7 percent of all travel is vertical and
  the sprite changes vertical direction **318 times in 80 seconds** -- roughly four reversals per
  second. A circuit that spends 58 percent of the fight in free fall with `wingTime == 0` is not
  reproducing this.
- **The player also moves horizontally**, over a wide span (108 to 826 px of a 960 px crop). So
  the vision model's "horizontal action: still" was wrong in the opposite direction, and the
  owner's rule is about the *dodge axis on a locked charge*, not about never running.
- The y range of 269 px against an x range of 718 px is consistent with the arena in the video
  being much shallower vertically than the probe's, which is worth noting because the probe
  builds 216 tiles of vertical space with rows 60 tiles apart.

### 20.3 Surface-contact cadence, and why the number is not yet trustworthy

A first attempt to extract the landing cadence, counting frames where vertical motion stops and
reverses upward, gives 130 contacts in 80 s with a median gap of 4 frames (24 native ticks) and
a maximum of 56 frames (336 ticks). The distribution is **bimodal** -- a dense cluster of 2-5
frame gaps plus isolated gaps of 42 and 56 frames -- which means the rule is detecting
mid-flight bounces as well as true landings, and the median is therefore not yet a landing
cadence.

What it does establish is the shape of the answer: the video player touches a surface far more
often than the circuit's 350 ticks per landing, with occasional long airborne stretches of the
same order as the circuit's. Making this number trustworthy needs a rule that distinguishes a
real touchdown (sustained contact, vertical velocity zero for more than one frame) from a
one-frame bounce, and that is the next step for this tool.

### 20.4 What the videos have already settled without pixels

The subtitle extract in `docs/fishron-video-subtitles.md` is itself authoritative written
evidence, and it contradicts the circuit in two specific places:

1. **Phase 1, 190 s: "open up vertical distance" (拉开竖直距离), and 200 s: "we do not need to
   take any action"** (我们不需要采取任何措施). The dodge is vertical and then *passive*. The
   circuit instead keeps issuing horizontal flee input through the whole charge.
2. **Phase 1, 185 s and 195 s: "it will charge at your CURRENT position"** (它会向你当前位置再次
   冲撞). This is the same lock that section 17.1 found in `AI_069`, from the other direction,
   and it is why acting before the lock is pointless.
3. **Phase 2, 330-355 s: the formula is "jump, then run once, then three dashes"**
   (起跳 / 一跑 / 三冲刺), with the goal of getting the sharknado and the bubbles released on the
   same side at the platform edge.

So the two structural changes the footage argues for are: stop fleeing horizontally during a
committed charge, and give the phase-2 cycle an explicit jump-run-dash cadence.

### 20.5 Status

Nothing was changed in the circuit this round -- the measurement did not yet support a specific
edit, and the previous two rounds showed that a plausible edit here can be bit-identical. The
pinned result is unchanged: weak wing 8684 ticks / **8 hits** / 37 damage / death; strong wing
11000 ticks / **11 hits** / 76 damage / alive. **No native zero on either loadout.**

## 21. Round 60: a real sign bug in the personal-space escape

This round went after the hits that the geometry says should be avoidable, and found an
actual inverted sign. It is a correctness fix; it does not yet reduce the hit count, and both
facts are recorded.

### 21.1 Where the hits actually are

All 8 hits from the dense weak-wing stream, with the boss state, the vertical offset
(`player.y - boss.y`, native Y growing downward) and the player's phase at the hit tick:

```
 tick   state  dy       wingTime  phase
  3019     0   +71.0       130     personal-space
  3807     1   -32.9       119     charge-horizontal
  5801     1   -28.9         0     charge-descend
  7204     0   -36.8         0     personal-space
  7538     0   -33.6         0     personal-space
  7884     1   +80.4         0     charge-descend
  8282     1  +227.2         0     charge-descend
  8322     0   +14.0         0     tornado-clear
```

Against a 150x100 boss and a 20x42 player, contact needs `|dx| < 85` and `|dy| < 71`. Five of
the eight sit inside that box. Three are in `personal-space`, which is the branch that runs when
the boss body is closest, and one of those three still had `wingTime 130` -- so for that hit the
flight budget was not the reason.

### 21.2 The inverted sign

`FishronWingScript`, in the personal-space latch:

```csharp
_personalSpaceVertical = boss.Center.Y >= player.Center.Y ? 1 : -1;
```

Native Y grows downward, so `boss.Center.Y >= player.Center.Y` means the boss is **below** the
player, and `1` means **descend**. The latch therefore drove the player **down into a boss that
was already underneath it**, and **up into a boss that was above** -- both branches closed the
vertical gap. Increasing the vertical gap requires moving toward +Y exactly when the boss is at
smaller Y:

```csharp
_personalSpaceVertical = boss.Center.Y <= player.Center.Y ? 1 : -1;
```

The comment above the line already stated the intended rule ("running away from a Boss above
means descending: vy positive") and the code implemented its opposite. This is the same class of
error the horizontal latch was fixed for in section 17, in the same three lines.

### 21.3 Effect: correct, but not yet sufficient

The fix changes which hits happen without changing how many:

| | before (rounded) | after |
|---|---|---|
| weak wing | 8684 ticks, **8 hits**, 37 damage, death | 8684 ticks, **8 hits**, 37 damage, death |
| strong wing | 11000 ticks, **11 hits**, 76 damage, npc contact 3 | 11000 ticks, **11 hits**, 76 damage, npc contact 3 |

The hit *set* did change, and in the intended direction:

```
 before:  3019(ps) 3807(ch) 5801(cd) 7204(ps) 7538(ps) 7884(cd) 8282(cd) 8322(tc)
 after :  3032(pj) 3810(ch) 5806(cd) 7229(bl) 7545(ps) 7899(ps) 8295(cd) 8340(tc)
```

`personal-space` hits fell from three to two, and the tick-3019 hit -- the specific one the code
comment cited as evidence -- is gone, replaced by a `precharge-jump` hit at 3032 where the
player had `wingTime 126` and therefore full mobility. The remaining personal-space hit at 7545
is now at `dy = -0.3`, i.e. the player and the boss centre are level with each other, which is
the geometry where a perpendicular escape has no vertical component to work with.

So the sign is right, the branch is doing what it says, and 8 hits remain. This is the fourth
consecutive round in which a geometrically justified change left the count unchanged, which says
the count is governed by something these local corrections do not touch.

### 21.4 What the evidence now points at

Three independent measurements agree on the same coarse fact: the fight is lost while the player
is out of flight budget.

- `wingTime == 0` before **14 of 19** hits (section 13.3), and **5 of 8** here.
- `wingTime == 0` on **58 percent** of the whole fight, with only 23 refills in 8446 ticks
  (section 18.2).
- The budget covers **121 percent** of the arena's landing cycle, so it always expires just
  before a landing (section 19.2).

The video measurement in section 20.2 adds the other half: the human player changes vertical
direction **318 times in 80 seconds**, roughly four per second, on a much shallower arena. The
circuit holds `Down` on 84.5 percent of airborne ticks and reverses vertical direction rarely.

That combination -- a sparse, mis-timed vertical rhythm against a budget that runs out just
before each landing -- is the mechanism, and none of the four fixes tried so far (locked
perpendicular, refill descent, free-fall descent, personal-space sign) changes either side of
it. A fix has to change the **vertical cadence**, not the direction of any single branch.

### 21.5 Status

- Correctness fix kept: the personal-space vertical sign, verified against a full native run and
  the unit suite (**750 passed, 8 failed**, all 8 the pre-existing missing
  `tests/Chaite.Tests/fixtures/observation-conformance.jsonl`).
- Pinned result unchanged and not to be overstated: weak 8684 ticks / **8 hits** / 37 damage /
  death; strong 11000 ticks / **11 hits** / 76 damage / alive. **No native zero on either
  loadout.**

## 22. Round 61: the learned policy has been masking the hand-written circuit

This round set out to fix the vertical cadence and instead found that every "native" result in
this session has been produced with a learned policy active, and that the policy is doing most of
the work. Two measurements are recorded here: the drain is caused by holding `controlJump` while
descending, and with the policy removed the hand-written circuit loses in a quarter of the time.

### 22.1 Session environment: `CHAITE_POLICY_FILE` was set

`Get-ChildItem env:` in this session shows:

```
CHAITE_POLICY_FILE    ...\policies\fishron-strong-wing.policy.bin
CHAITE_POLICY_FORMAT  exported
```

`LearnedPolicy.EnsureConfigured()` reads exactly these, and `ForRoute` then hands the script an
active policy. `FishronWingScript.DecideMovement` calls `learned.Adjust(...)` and, when it
returns true, **replaces** `output.Horizontal`, `output.Vertical`, `output.Jump` and
`output.Dash` wholesale (section 22.4 of this document; `FishronWingScript.cs` lines 509-529).

So the pinned figures (weak 8684 ticks / 8 hits; strong 11000 / 11) are **policy-assisted**, not
the hand-written state machine's own result.

### 22.2 The drain is `controlJump` held while descending

Per-tick dense native rows (3761 contiguous ticks, boss present, `wingTime > 0`), grouped by the
applied controls, with the measured `wingTime` drop per tick:

| controlJump | controlDown | ticks | drain/tick | total drain |
|---|---|---|---|---|
| 0 | 0 | 25 | 0.080 | 2 |
| 0 | 1 | 478 | **0.031** | 15 |
| 1 | 0 | 1 | 0.000 | 0 |
| **1** | **1** | **1467** | **0.920** | **1350** |

This matches the decompile exactly. `Player.WingMovement()` (line 22430) does `wingTime -= 1f`,
and it is only reached when `flag19` is true, which requires
`wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 && velocity.Y != 0`
(`Player.cs:27001`). Holding jump while descending therefore costs a full tick of budget per
tick, and holding down without jump costs almost nothing (0.031).

Attributed by phase, the 1406 draining ticks split as `precharge-jump` 404, `charge-horizontal`
214, `tornado-clear` 201, **`standoff` 194**, `charge-ascend` 145, `charge-descend` 82,
`bubble-line` 78, `sharknado-exit` 34. Reading the raw rows shows the pattern directly:

```
 tick plan.Jump p.controlJump p.vy    p.wingTime  next  phase
  254   True       True      -6.21     130.0      +1.00  fishron-wing-standoff
  259   True       True      -7.11     125.0      +1.00  fishron-wing-standoff
  265   True       True      -7.71     119.0      +1.00  fishron-wing-standoff
```

### 22.3 On every draining tick, `plan.Jump` and `plan.Drop` are both true

`PlanFormula` makes those mutually exclusive by construction:

```csharp
plan.Jump = script.Jump && script.Vertical < 0;
plan.Drop = script.Vertical > 0;
```

so `(True, True)` cannot come from the script. On the measured draining ticks the plan flags are
`(plan.jump, plan.drop)` = `{(True, True): 1406}` -- exactly the draining count. The only writer
that can produce that pair is `LearnedPolicy.Adjust`, which emits `jump` as an independent output
alongside `vertical` and is applied *after* the script, at `FishronWingScript.cs:525`.

**The drain is a learned-policy behaviour, not a script behaviour.**

### 22.4 With the policy off, the script never holds jump while descending

Same dense measurement, `CHAITE_POLICY_FILE` unset:

| `(controlJump, controlDown)` | ticks | drain |
|---|---|---|
| (0,0) | 423 | 0 |
| (0,1) | 759 | 26 |
| (1,0) | 632 | 558 |

`(1,1)` is **zero ticks**. The hand-written circuit's `output.Jump = vertical < 0` is sufficient
to keep jump off during descents, and its total budget spend is the legitimate 558 of the 632
ascend ticks. `wingTime == 0` occurs on **18.3 percent** of rows against the policy's **47.5
percent**.

### 22.5 Which refutes the budget hypothesis as the cause

The script-only run is **better** on the budget and **far worse** on the fight:

```
script only : 2466 ticks   6 hits   108 damage   death=True   npc contact 7
with policy : 11000 ticks 11 hits    76 damage   death=False  npc contact 3
```

At the identical 4000-tick cap the policy run reached **hits 0** with 18 boss damage while the
script-only run was already dead at 2466 with 6 hits. So the script spends less wing budget, has
the budget available more of the time, and still loses roughly four times sooner. Sections 18, 19
and 21 all concluded the binding constraint was the flight budget; **that conclusion does not
survive this measurement.** The budget exhaustion is real but it is not what kills the script --
the script dies with budget in hand, which means its failure is positional, not economic.

### 22.6 A standoff edit that was tried and reverted

Both the `standoff` and `tornado-bait` branches ended in `vertical = player.OnGround ? -1 : 1`,
so airborne in standoff meant *climb*, spending 1.00 wingTime per tick on a branch that is by
definition the case where the boss is already within 720 px. Changing the airborne half to
`vertical = 1` (descend) built and ran, and produced **bit-identical budget telemetry** (1467
jump&down ticks, 1350 drain) because the policy overwrites `output.Vertical` immediately
afterwards. It was reverted with `git checkout --`. It is worth retrying **after** the policy is
disabled, when the script's own output actually reaches the engine.

### 22.7 Consequences for the objective

1. Any native acceptance number quoted from this session before now was policy-assisted. The
   hand-written state machines the objective asks for have been measured at **6 hits / death at
   2466 ticks** (weak wing) and **7 npc contacts** — much worse than the pinned figure suggested.
2. The 4000-tick `HITS 0` is a genuine native zero-hit window with the boss engaged (18 damage
   dealt), but it is the **policy's** result and it is a window, not a fight: nothing was verified
   to 9000 ticks, and the boss was at 77982 of 78000 life.
3. The next round must decide the intended relationship between the learned policy and the
   deliverable. If the deliverable is a hand-written formulaic state machine, the policy must be
   off for every acceptance run, and the script has to be fixed against a much worse baseline.

### 22.8 Status

- No circuit change kept. The standoff edit was reverted as a measured null.
- Weak wing, script only: **6 hits, death at 2466 ticks.** Weak wing, policy: 8684 ticks / 8 hits
  / death. Strong wing, policy: 11000 / 11 / alive. **No native zero over a full fight on either
  loadout.**

## 23. Round 62: the kill mechanism, a refill guard, and an environment trap

This round found the actual mechanism that ends the script-only fight, fixed it, and in the
process found why several of this session's measurements disagreed with each other.

### 23.1 The kill mechanism: airborne with an empty bar, never landing

Trace of the hit at tick 533, script only, around the moment the charge locks (the boss enters
state 1 at tick 518 with `ai[1]=0, ai[2]=0`, which is the native lock of section 17.1):

```
 tick  st  ai2  dy      pvx      pvy    wingTime  phase
  517   0   29  -74.3   +6.71   +0.22      0      precharge-jump
  518   1    0  -67.1   +6.76   +0.35      0      precharge-jump
  519   1    1  -59.8  -14.50   +0.48      0      charge-horizontal
  523   1    5  -29.2  -13.26   +1.02      0      charge-horizontal
  533   1   15   +5.5   -4.50   -3.50      0      charge-horizontal   <- hit
```

Two things are visible and they are the whole failure:

1. **`wingTime` is 0 on every row**, from tick 508 through 533. The player is airborne
   (`sliding` false) the entire time, so it never lands and never refills.
2. **`pvy` is essentially zero** (-0.98 to +1.02 while climbing, then gravity). With an empty bar
   the player has no vertical authority at all, so the latched normal -- which wanted to climb --
   could not be executed. `dy` drifts from -74 to +5 under gravity alone while the boss closes.

The player is not being out-positioned; it is **out of fuel and unable to refuel**. Holding
`controlJump` with an empty bar keeps `vy` near zero (the wings stay deployed), so it hovers
instead of falling to a platform that would restore the bar.

### 23.2 The refill guard

Added at the end of `Tick`, after the branch has chosen its vertical:

```csharp
if (player.WingTime <= 0f && !player.OnGround && vertical < 0)
{
    vertical = 1;
    phase = "fishron-wing-refill";
}
```

Climbing is suppressed only while the bar is empty and the player is airborne. The descend
branches are untouched, and a grounded player is untouched, so the takeoff that raises
`output.Jump` and the landing that refills the bar both still happen. Releasing jump is what lets
the fall occur; `Player.cs:26992` restores `wingTime` on the landing tick.

Measured effect, dense, policy explicitly off, same 4000-tick cap:

| | rows | `wingTime==0` | refill rows | `jump&down` ticks |
|---|---|---|---|---|
| before | 2228 (died at 2466) | 18.3% | 0 | 0 |
| after | 3761 (alive at 4000) | **15.3%** | **277** | 0 |

Zero `jump&down` confirms the guard did not introduce the held-jump-while-descending drain, and
the guard firing 277 times confirms it is actually reached. Survival improved from a death at
2466 to a live 4000, life drops fell from 6 to 4, and boss damage rose from 108 to 84 (lower is
better here because the player survived longer and kept hitting).

### 23.3 The environment trap that corrupted this session's measurements

`CHAITE_POLICY_FILE` and `CHAITE_POLICY_FORMAT` were set in this session's environment. Worse,
several attempts to clear them used:

```powershell
Remove-Item Env:\CHAITE_POLICY_FILE,Env:\CHAITE_POLICY_ROUTES,Env:\CHAITE_POLICY_FORMAT -ErrorAction SilentlyContinue
```

`CHAITE_POLICY_ROUTES` is **never set**, and `Remove-Item` on a missing item is a terminating
error for the whole command even with `-ErrorAction SilentlyContinue` on the cmdlet, so the
remaining names were often **not** removed and the policy stayed active. That is why
`refillguard-weak` reported the policy-on signature (8684 ticks, 8 hits, `npc contact` 0) while
`scriptonly-dense` (policy genuinely off) reported 2466 ticks, 6 hits, `npc contact` 7 -- two
runs of the same build disagreeing completely.

**Every acceptance run from here must set the policy environment explicitly, one variable per
statement**, and the probe should be run with `CHAITE_POLICY_ROUTES` set explicitly when a policy
is intended. A missing `CHAITE_POLICY_ROUTES` with `CHAITE_POLICY_FILE` set makes
`LearnedPolicy.EnsureConfigured` throw *after* assigning `_file` and `_configured`, so the file
stays set for the process.

### 23.4 Reliable A/B, policy on versus off (weak wing, dense, 4000 ticks)

| | hits | boss damage | `npc contact` |
|---|---|---|---|
| policy admitted for `fishron-fairy-wing` | **2** | 1 | 0 |
| policy fully off | **4** | 84 | 8 |

So the policy genuinely helps the weak route and was not a no-op. It is not, however, the
deliverable: the objective asks for a hand-written formulaic state machine per loadout.

### 23.5 The four remaining hits are mostly not body contact

Under the guard, 4 life drops totalling 351 damage:

```
 tick  state  dmg   dy      dx      wingTime  phase
   950    1    98   +2.5  +169.0      10      charge-horizontal
  2146    1    77  +35.3  +103.3      57      charge-descend
  2278    0    77  -36.0  +146.4       0      tornado-clear
  2668    1    99  -32.9    +5.0      32      charge-ascend
```

Body contact needs roughly `|dx| < 85`, so only tick 2668 (`dx +5.0`) is unambiguously the boss
body; the other three at `dx` 103, 146 and 169 are something else -- bubbles, shark projectiles or
the sharknado. The probe reports `npc contact 8` for this run, so the npc-contact counter is not
the same quantity as the life-drop count and should not be quoted as if it were.

### 23.6 Status

- **Kept:** the empty-bar refill guard (`FishronWingScript.cs`), verified by a clean A/B.
- Unit suite: **750 passed, 8 failed** (all 8 the pre-existing missing
  `tests/Chaite.Tests/fixtures/observation-conformance.jsonl`).
- Clean-environment baselines, policy off: **weak 4000 ticks / 4 hits / death not reached**;
  **strong 5341 ticks / 9 hits / death / `npc contact` 9**. With the policy on, weak is 8684 / 8 /
  death and strong 11000 / 11 / alive, but those are policy results, not the state machine's.
- **No native zero over a full fight on either loadout.**

## 24. Round 63: the refill guard made sweepable, and an environment-drift trap

Section 23.2 added the empty-bar refill guard with a hard 0 threshold. This round made the
threshold sweepable to ask whether *reserving* budget beats *reacting at empty*, and in doing so
found that the session's environment drifts between tool invocations, which invalidated part of
section 23's evidence.

### 24.1 The knob

`CHAITE_REFILL_GUARD` (wingTime units, default 0) is read once per script instance:

```csharp
if (player.WingTime <= _refillGuardBudget && !player.OnGround && vertical < 0)
{
    vertical = 1;
    phase = "fishron-wing-refill";
}
```

A threshold of 0 is the reviewed circuit plus the guard of section 23.2; unset, malformed or
negative values fall back to 0, so a probe run without the variable reproduces the previous build.
Setting the variable to a value above `wingTimeMax` disables the guard entirely, which is what
makes a clean on/off A/B possible.

### 24.2 The guard is real: on/off A/B inside one invocation

Both runs below were launched from the **same** shell invocation with only
`CHAITE_REFILL_GUARD` changed, weak wing, policy explicitly off, 3000-tick cap:

| guard | hits | ticks | boss damage | death | `npc contact` |
|---|---|---|---|---|---|
| 100000 (disabled) | 5 | **1692** | 57 | **True** | 5 |
| 0 (enabled) | **4** | **3000** | 66 | **False** | 6 |

With the guard disabled the script dies at tick 1692; with it enabled the same script survives the
full 3000 ticks. This is the same build, the same seed and the same parameters, so the guard is
confirmed to change the outcome rather than merely the telemetry.

### 24.3 The threshold sweep (weak wing, policy off, 6000-tick cap)

| guard | hits | ticks | boss damage | `npc contact` |
|---|---|---|---|---|
| 0 | 9 | 5937 † | 126 | 12 |
| 10 | 8 | 5015 † | 162 | 13 |
| 20 | 8 | 4653 † | 210 | 14 |
| 30 | 8 | 2983 † | 128 | 10 |
| 45 | 8 | 4477 † | 144 | 11 |
| 60 | **7** | 3698 † | 111 | 9 |

† every row ended in `FailedAfterDeath`.

The hits column is nearly flat (9, 8, 8, 8, 8, 7) while survival **degrades** as the threshold
rises: 5937 ticks at guard 0 down to 3698 at guard 60, with a non-monotonic wobble at guard 30.
Reserving budget by landing earlier buys at most one fewer hit and costs about forty percent of
the fight's duration, so it is a trade and not a fix. Guard is kept at its default of 0.

### 24.4 The environment drifts between tool invocations

The sweep above reports 9 hits at guard 0, while the A/B in 24.2 reports 4 hits at guard 0, and
an earlier dense run reported 4 hits with the guard unset. The runs cannot all be the same
configuration. Two candidate explanations were tested:

- **The dense probe flag.** Controlled A/B, dense off versus dense on, otherwise identical, both
  in one invocation: **4 hits / 84 boss damage / `npc contact` 8 in both, digit for digit.**
  Dense mode changes only the observation row limit (`GameProbe.cs:200`), so this is refuted --
  and it also means the 4000-tick dense artifact and the 4000-tick sampled artifact are the same
  fight.
- **Environment drift.** The session environment is re-created between tool invocations and is
  not reliably what the previous call left behind. Earlier in this session it carried
  `CHAITE_OBS_WORLD_BOUND=18`, `CHAITE_PROJECTILE_SLOTS=12`, `CHAITE_PROJ_SORT=threat` and
  `CHAITE_POLICY_FILE`; those are planner-visible knobs, not just probe settings. The guard
  sweep ran in a later invocation from the A/B, and the two differ by more than the guard value.

**Consequence: a comparison is only trustworthy when both arms run inside a single invocation
with the difference explicit.** Section 23.4's policy on/off table (2 hits versus 4) was taken
from two separate invocations and is therefore **not** established; it is withdrawn here. The
same applies to section 23.5's four-hit breakdown, which came from an invocation whose
environment is no longer reconstructible.

### 24.5 Status

- **Kept:** the refill guard, parameterised by `CHAITE_REFILL_GUARD`, default 0, confirmed by a
  single-invocation A/B (death at 1692 -> alive at 3000).
- **Withdrawn:** section 23.4's policy on/off comparison and section 23.5's hit attribution.
- **Best current single-invocation baseline, weak wing, policy off, guard 0:** 4 hits, alive at
  3000 ticks, 66 boss damage.
- **No native zero over a full fight on either loadout.**

## 25. Round 64: a single-invocation sweep harness, and where the four hits sit

Section 24 established that the session environment drifts between tool invocations, so no
comparison taken across invocations is trustworthy. This round built the harness that removes
that failure mode and used it to confirm the guard's default.

### 25.1 `tools/sweep-native.ps1`

Runs a list of parameter points for one environment variable, each arm launched from the same
process, with the policy environment pinned off **one variable per statement** -- clearing them
as one comma-separated `Remove-Item` list is the trap from section 23.3, because
`CHAITE_POLICY_ROUTES` is normally unset and `Remove-Item` terminates on the first missing name.

```
.\tools\sweep-native.ps1 -FormulaRoute fishron-fairy-wing `
    -Variable CHAITE_REFILL_GUARD -Values 0,25,60 -MaxTicks 3000
```

Confirmed on the weak wing at a 3000-tick cap, all three arms inside one invocation:

| guard | hits | ticks | boss damage | death | `npc contact` |
|---|---|---|---|---|---|
| **0** | **4** | 3000 | 66 | False | 6 |
| 25 | 6 | 3000 | 87 | False | 7 |
| 60 | 5 | 3000 | 102 | False | 8 |

Guard 0 is the best of the three, and the threshold is not monotone in either direction. This
agrees with section 24.3's conclusion from the earlier sweep: the guard belongs at its default and
the threshold is not the lever.

### 25.2 The four hits, with the boss's own state

The sampled trace for the guard-0 arm records 159 rows over ticks 240..3000, with 4 life drops.
Absolute offsets at the sampled tick of each drop:

```
 tick   dmg  bossState   dx       dy      wingTime  phase
  957    97      0     +308.3   -63.9       10     tornado-bait
 2149    76      0     +166.9   +17.4       56     personal-space
 2280    77      0     +156.6   -33.4        0     tornado-clear
 2675    98      0     -141.3   -22.4       25     personal-space
```

Two properties stand out:

1. **`bossState` is 0 at every drop.** State 0 is a hover state, not a charge state. Sections 21
   and 23 found the hits concentrated in charge states and in `personal-space`; on this build and
   environment **not one hit is in a charge state**.
2. **No hostile projectile is recorded within 140 px on any drop.** The sampled channel does not
   carry a projectile list on these rows, so this is an absence of evidence rather than evidence
   of absence, but it does rule out the "the bubbles are hitting us" reading for these four.

The offsets of 140-310 px are far outside the ~85 px body-contact box, and at 17 px/tick a charge
closes roughly 17 px between a sample and the tick it represents, so these offsets cannot be
corrected into a body contact either. With `bossState` 0 and no projectiles recorded, the damage
source for these four is **not yet identified**. That is the honest state of it, and it is the
thing to measure next: a dense capture of the tick of each drop with the projectile channel
present, so the source is read rather than inferred.

### 25.3 What is established at this point

- The empty-bar refill guard is a real fix (section 24.2, single invocation: death at 1692 without
  it, alive at 3000 with it) and its default of 0 is optimal among the thresholds tried.
- The weak-wing hand-written circuit, policy off, guard 0, currently reaches **4 hits and is
  alive at 3000 ticks** with 66 boss damage.
- The remaining hits are in boss hover state 0, not in charge states, and their source is
  unidentified.
- Baseline hit counts on this build range over 4-9 depending on the invocation's environment, so
  only single-invocation comparisons carry weight.
- **No native zero over a full fight on either loadout**, and no claim of one.

### 25.4 Status

- **Added:** `tools/sweep-native.ps1`, the single-invocation comparison harness.
- **Kept:** the refill guard at default 0.
- Unit suite: **750 passed, 8 failed** (all 8 the pre-existing missing
  `tests/Chaite.Tests/fixtures/observation-conformance.jsonl`).
- Best single-invocation baseline, weak wing, policy off, guard 0: **4 hits, alive at 3000 ticks,
  66 boss damage.**

## 26. Round 65: the dodge goes the wrong way — a read of the script's own output

Section 25 left the four hits unexplained: they were in boss hover state 0 in the sampled
channel with no projectile nearby. A dense capture resolves what they are, and a temporary
diagnostic that logs the script's own per-tick output narrows the failure to one decision.

### 26.1 The four hits are body contact during a charge, not bubbles

Dense capture (`artifacts/game-probe-dense-guard0`, 3761 contiguous rows, weak wing, policy off,
guard 0, 4000-tick cap, 4 hits):

```
 tick   dmg  bossState  ai1    dx      dy      wingTime  immune  projectiles<200
  950    98      1        0   +104.0  -26.5      10        40    none
 2146    77      1        0    +38.3   +6.3      57        40    none
 2278    77      0     -300    +81.4  -65.0       0        40    none
 2668    99      1        0    -60.0  -61.9      32        40    none
```

Three of the four are charge ticks (`bossState 1`, `ai1 0`) with offsets inside the contact box
(`|dx| < 85`, `|dy| < 71`), and **no hostile projectile is recorded within 200 px on any of
them**. The fourth (2278) is `bossState 0` with `ai1 -300`, which is the hover that immediately
follows a charge; the boss is still moving at charge speed through it. So this build's four hits
are boss body contact, and the section-25 reading of "hover state 0, source unidentified" was an
artefact of the sampled channel being too sparse to catch the charge state.

### 26.2 The failure is a wrong-direction dodge, with the budget available

Full trace of the hit at tick 2146 (the boss locks at tick 2120 when its state goes 0 -> 1):

```
 tick  st  dx       dy     pvx     pvy    wt  d  phase
 2120   1  -526.4  +108.3  (lock)          58     precharge-jump
 2121   1  -495.2  +102.4  +?            57  1  charge-descend
 2130   1  -228.5   +67.4  +11.82  +1.14  57  1  charge-descend
 2136   1   -73.0   +62.1   +7.67  +3.54  57  1  charge-descend
 2137   1   -65.3   +55.1   -9.00  -3.60  57  1  charge-descend   <- knockback
 2146   1   +38.3    +6.3   +4.50  -3.50  57  1  charge-descend   <- hit
```

At the lock the player is **above** the boss (`dy +108`) and the boss's locked velocity is
`(-16.94, +1.44)` -- it travels left and slightly **upward**, i.e. toward the player. The owner's
rule says this is the case for a **diagonal climb**: the boss is below, so the escape must have an
upward component. The player instead held `controlDown` for the whole 25-tick episode and never
climbed. `pvy` never exceeds +3.54 before the hit, so the vertical axis contributed nothing to the
dodge, and the horizontal separation alone decayed from 526 px to 38 px while the charge closed.

**`wingTime` is 57 for the entire episode** -- the bar was healthy. This is not the empty-bar
failure of section 23; it is a dodge aimed along the wrong axis with full budget in hand.

### 26.3 The latch produced a normal that the geometry does not support

A temporary diagnostic (since reverted) logged the script's own output per tick. At the lock:

```
 tick  st  dx       dy      script phase                  hor vert jump dash  nh  nv  nseq  wt
 2120   1  -526.4  +108.3  fishron-wing-precharge-jump    -1   -1    1    0    0   1    2   58.0
 2121   1  -495.2  +102.4  fishron-wing-charge-descend     1    1    0    1    1   1    4   57.0
```

`nseq` moved from 2 to 4, so `LatchChargeNormal` did re-fire on the charge edge, and it latched
`(nh, nv) = (1, 1)`.

Working the same geometry by hand from the recorded centres:

```
player.Center - boss.Center = (-526.4, +108.3)
aim      = (-0.979, +0.201)
normalA  = (-aimY, +aimX) = (-0.201, -0.979)   dot with (dx,dy) = +125   <- larger, so chosen
normalB  = (+aimY, -aimX) = (+0.201, +0.979)   dot = -125
```

which quantises to `(nh, nv) = (0, -1)` -- a climb, which is the correct answer and the one the
owner's rule gives. The latch instead produced **(1, 1)**, whose vertical sign is the opposite.
Every tick of the episode then followed `_chargeNormalVertical = +1` into `charge-descend`.

So the chain is: the latch computes the wrong normal vertical at the lock, the charge branch
faithfully obeys it, and the player descends into an upward-travelling charge. **One wrong sign in
one latch is worth 4 hits over 4000 ticks.**

The discrepancy between the hand computation and the latched value is not yet explained. The
candidate is that the `TargetSnapshot` centre the script reads is not the NPC centre the
observation channel records, which would also explain `nh = 1` where the geometry gives `0.201`
(close to the 0.2 quantisation threshold -- a different boss centre would move it across). Pinning
that is the immediate next step, and it needs the diagnostic kept during one run rather than
inferred.

### 26.4 Status

- **Reverted:** the temporary `CHAITE_SCRIPT_TRACE` diagnostic (`git checkout --`).
- **Kept:** the refill guard and `CHAITE_REFILL_GUARD` from sections 23-24.
- Weak wing, policy off, guard 0: **4 hits, alive at 3000-4000 ticks**, 66-84 boss damage.
- **No native zero over a full fight on either loadout.**
