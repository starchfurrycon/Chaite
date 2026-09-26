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

## 27. Round 66: section 26's "wrong sign" is withdrawn -- the latch arithmetic is correct

Section 26 concluded that `LatchChargeNormal` computes a wrong normal vertical at the lock and
that one wrong sign was worth 4 hits. This round instrumented the latch directly to check that,
and **the conclusion does not hold**. The correction is recorded here in full, because a
plausible mechanism that survives into the document unchallenged is worse than no mechanism.

### 27.1 What the latch actually receives and computes

A temporary diagnostic (since reverted) logged every latch event with its own inputs and outputs.
The first rows of the current build, weak wing, policy off, guard 0:

```
tick  seq  px       py       bx       by      dx       dy      aimX     aimY     nX       nY      hor vert
 105    0  650.0   7835.7  1093.7   7625.8  -443.7  +209.9  -0.9039  +0.4276  +0.4276  +0.9039   1    1
 163    2  650.0   7936.7   632.6   7813.3   +17.4  +123.3  +0.1394  +0.9902  -0.9902  +0.1394  -1    0
 221    4  650.0   7698.0   761.0   8335.4  -111.0  -637.3  -0.1716  -0.9852  -0.9852  +0.1716  -1    0
 279    6  650.0   7616.4   793.0   7594.0  -143.0   +22.4  -0.9880  +0.1547  -0.1547  -0.9880   0   -1
 337    8  998.6   7415.4   272.1   7544.1  +726.6  -128.6  +0.9847  -0.1743  +0.1743  +0.9847   0    1
 515    1 2263.5   7817.3  1730.0   7686.2  +533.4  +131.1  +0.9711  +0.2386  +0.2386  -0.9711   1   -1
 573    3 2732.3   7523.6  2368.7   7645.3  +363.6  -121.7  +0.9483  -0.3173  -0.3173  -0.9483  -1   -1
 631    5 2263.5   7179.8  2856.8   7297.7  -593.4  -117.9  -0.9808  -0.1949  -0.1949  +0.9808   0    1
```

Every row satisfies the two identities the code intends: **`nX = ±aimY`** and **`nY = ∓aimX`**,
i.e. the normal is the unit perpendicular to the lock aim, quantised at 0.2. Checked against the
recorded `dx`, `dy`:

- row 105: `aim = (-443.7, +209.9)/491 = (-0.904, +0.428)`; `nX, nY = +0.428, +0.904` ✓
- row 279: `aim = (-143.0, +22.4)/144.7 = (-0.988, +0.155)`; `nX, nY = -0.155, -0.988` ✓
- row 631: `aim = (-593.4, -117.9)/605 = (-0.981, -0.195)`; `nX, nY = -0.195, +0.981` ✓

`dx`/`dy` themselves reproduce `player.Center - boss.Center` from the separately recorded
absolute centres, and `PlayerSnapshot.Center` and `TargetSnapshot.Center` are both
`Position + size * 0.5` (`Models.cs:188` and `Models.cs:487`), so there is no hidden centre
convention.

**The latch is arithmetically correct.** With the player above the boss (`aimY > 0`) it produces
`nY > 0`, which is descend: the player should move further away downward, and running from a boss
that is above means descending. That is the intended behaviour, not a bug.

### 27.2 How section 26 went wrong

Section 26 hand-computed a normal for "the lock at tick 2120" from `artifacts/game-probe-trace-script`
and compared it with a latch value read from a **different** run's diagnostic. The two numbers
came from different fights: the observation rows were the trace-script run (4 hits, boss damage
84) while the latched `(1, 1)` was read when interpreting that same file, but the coordinates used
for the hand calculation were re-read from a *third* listing whose `dy` sign was `+108` at the
boss centre offset while the latch's own log for that lock records `dy -110.9`. The sign of `dy`
alone flips the answer, and the two sources disagreed.

The lesson is the section-24 one applied to my own analysis: **a quantity derived from one run and
a quantity derived from another cannot be compared**, and a hand computation is a third source
that must be tied to the same run as the value it checks. The correct method -- used in 27.1 -- is
to have the instrument log its inputs *and* its outputs on the same line, so the identity can be
verified self-containedly.

### 27.3 What survives from section 26

Unchanged and still measured:

- The four hits are **boss body contact**, three of them on charge ticks (`state 1`, `ai1 0`) with
  no hostile projectile within 200 px. That is from one dense run and stands.
- At the failing lock the player was **above** the boss and the boss's locked velocity pointed
  **toward** the player, and the player then held `controlDown` for the whole episode with
  `wingTime` healthy (57). Both the direction of the latched normal and the branch's obedience to
  it are consistent with a **descend**, which is the *correct* response to a boss below.
- So the mechanism is **not** a wrong sign. It is that descending, by itself, fails to clear the
  charge -- which puts the failure back on the vertical cadence question of sections 19 and 22
  rather than on a single inverted branch.

### 27.4 Status

- **Withdrawn:** section 26's "the latch produced a normal the geometry does not support" and the
  "one wrong sign is worth 4 hits" conclusion.
- **Verified:** `LatchChargeNormal` computes the unit perpendicular to the lock aim correctly;
  the diagnostic is reverted.
- Weak wing, policy off, guard 0: **4-5 hits, alive at 2400-4000 ticks.**
- **No native zero over a full fight on either loadout.**

## 28. Round 67: floor-clamp and altitude experiments are refuted; the budget is the binding axis

Section 27 put the failure back on vertical cadence. This round tested three structural answers to
it on whole native fights. All three are refuted, and the measurements name the binding constraint.

### 28.1 The floor clamp is a real defect, but not the binding one

`ApplyArena` resolves a descend that would reach the floor by setting the vertical to 0:
`FishronWingScript.cs:1239`, `else if (y >= _floorY - FloorMargin && vertical > 0) vertical = 0;`.
That is neither floor-safe nor a dodge. 0 clears `controlJump`, and a released jump with the wings
out **holds altitude** rather than falling (`Player.cs:26992` refills only on `velocity.Y == 0 ||
sliding`), so the player neither falls nor climbs and stays on the locked line with
`controlDown` released.

Three resolutions were built and swept in one invocation (weak wing, policy off, guard 0):

| `CHAITE_FLOOR_ESCAPE` | behaviour | 3000 ticks | 8000 ticks |
|---|---|---|---|
| 0 | reviewed circuit (`vertical = 0`) | **4 hits** | **9 hits**, death at 5937 |
| 1 | climb instead | 6 hits | - |
| 2 | keep the descend, let the floor stop it | **2 hits** | **11 hits**, death at 5742 |
| 3 | room-aware flip (identical to 1 at the floor) | 6 hits | - |

Mode 2 leads by 2 hits at 3000 ticks and **loses by 2 at 8000**, with 210 boss damage against 126.
Mode 1/3 are worse at both horizons. The 3000-tick rank is therefore horizon-dependent and mode 2
is not an improvement; the clamp remains a defect worth fixing for clarity, but it is not what
costs the hits.

### 28.2 Minimum altitude has no headroom to reclaim

The hypothesis was that the circuit fights from a thin band near the floor (at lock: `py 7835.7`
against arena floor 7952, i.e. 116 px) and wastes the arena's 216 tiles of height. A
`CHAITE_MIN_ALTITUDE` floor was added that forces a climb whenever the branch wants to descend
below a given altitude while airborne with budget left:

| `CHAITE_MIN_ALTITUDE` | 4000 ticks |
|---|---|
| 0 (off) | **4 hits**, alive |
| 1500 | 7 hits, **death at 2193** |
| 2500 | run did not complete |

And the premise is wrong. Over the run, the player's centre y is **min 5899, max 7979, mean 6987**
-- a mean that sits exactly on the upper platform row (`arenaGroundY - 120` tiles = 6992). The
distribution is

```
 y 5500-5999 :   9   y 6500-6999 :  96   y 7500-7999 : 39
 y 6000-6499 :  25   y 7000-7499 :  48
```

so the circuit already uses most of the arena and spends its time near the top platform, not pinned
to the floor. Forcing it higher only removes the downward room it still sometimes needs, and
1500 px did exactly that.

### 28.3 The binding axis is the wing budget

In the same run, the player is airborne with an empty bar on **119 of 217 sampled rows (55%)**, and
holds `down=1, jump=0` on **112 of 217 (52%)**. Sections 23-24 measured the same thing from the
drain side. The platform rows sit one wing charge apart (60 tiles) *by design*, so an empty bar is
supposed to be answered by landing on the next row and refilling; the guard does release the jump
for that. What the two refuted experiments show is that neither the floor clamp nor the altitude
band is where the hits come from, which leaves the vertical **cadence** -- when to spend the charge
and when to land -- as the only lever left on this axis, and that is a scheduling problem over a
fixed 130-tick resource, not a clamp.

### 28.4 Status

- **Reverted:** `CHAITE_FLOOR_ESCAPE` and `CHAITE_MIN_ALTITUDE` and their uses
  (`git checkout -- src/Chaite.Core/FishronWingScript.cs`). The refill guard is kept.
- **Refuted:** floor-escape modes 1, 2, 3 as improvements; minimum altitude as an improvement.
- Weak wing, policy off, guard 0: **4 hits at 3000-4000 ticks**; **both refuted experiments die
  faster** than the baseline over 6000+ ticks.
- **No native zero over a full fight on either loadout.**

## 29. Round 68: the refill fall is already minimal; the dash is not the answer either

### 29.1 The refill cycle is physically minimal, not wasteful

Section 23 stopped at "wingTime is empty 15-18% of the time". Measured per episode over one dense
fight (`artifacts/game-probe-dense-guard0`, 3761 rows, weak wing, policy off, guard 0), there are
**7 empty-bar episodes**:

```
episode            length   dash-ticks   wingTime on landing
t  461-  518         58         0        130
t  976- 1005         30         1        130
t 1274- 1363         90         2        130
t 2247- 2362        116         0        130
t 2709- 2841        133         1        130
t 3022- 3097         76         1        130
t 3499- 3570         72         0        130
```

`wingTime` is **exactly 0 for every tick of every episode and snaps to the full 130 on the tick
after the last one** -- never a partial value, always full. That is the native refill at
`Player.cs:26992`, and it means the episode length *is* the time to return to a surface, not waste.
The fall itself is clean Newtonian motion at the measured `gravity 0.4`:

```
 t 2266  pcy 7436.0  pvy +3.44   dpvy +0.40
 t 2272  pcy 7465.1  pvy +5.84   dpvy +0.40
 t 2312  pcy 7615.6  pvy +10.01  dpvy +0.31   <- capped at maxFallSpeed
```

So an earlier estimate in this round (that ~70 ticks of each episode were avoidable) was my own
arithmetic error: starting from near-zero `vy`, falling the ~800 px from the hover altitude to the
arena floor at `0.4/tick` climbing to a `10.01` cap takes about 45 ticks, plus the landing. The
circuit descends to the **floor** rather than the intermediate platform rows because from its hover
altitude (~7423) the floor is nearer than the row above (~6992), so that choice is also correct.

**The refill cadence is not where the cost is.** It cannot be shortened by deciding differently.

### 29.2 The window has no climb and almost no dash -- and adding the dash still loses

The measured state during those windows: **575 empty-bar ticks**, on which the circuit holds
`controlDown` **91.8%** of the time and `controlJump` **1.2%** (it must not hold jump: that is what
lets the fall happen). Dash is active on **1.0%** of empty-bar ticks and **1.2%** of healthy ones,
i.e. about **46 of 3761 ticks** for the whole fight. So during every refill the player has no climb
authority and, in the four longest episodes, literally zero dash ticks.

That made the one horizontal tool which costs no `wingTime` look like the answer. A refill dash was
implemented -- when the bar is empty and the player is airborne, keep the dash issued -- and swept
in one invocation:

| `CHAITE_REFILL_DASH` | hits | boss damage | dash-active ticks |
|---|---|---|---|
| 0 (off, reviewed) | **4** | 66 | 32 |
| 1 (on) | **5** | 147 | 38 |

**Refuted.** It loses by one hit, and the boss-damage and dash-count columns move in opposite
directions, which is the signature of a different trajectory rather than a better one. The
mechanism is plausible but the outcome is not an improvement, so it is reverted.

### 29.3 Where this leaves the vertical axis

Three separate structural answers to the vertical problem have now been measured and refuted:
floor-escape modes 1/2/3 (section 28), minimum altitude (section 28), and the refill dash (29.2).
Together with sections 23-24 (guard thresholds are flat) this closes the whole family of
"tune one clamp or one flag" answers. The remaining axis is genuinely the **schedule**: which
charge a climb is spent on, and whether the player enters a refill window with enough horizontal
separation that the horizontal dodge alone can carry it, which is exactly the owner's rule that a
lock taken from far enough out may be answered by running straight away.

That is a different kind of change from the ones refuted above -- it is a decision over the whole
fight rather than a local clamp -- and it is the next thing to build.

### 29.4 Status

- **Reverted:** the refill dash and `CHAITE_REFILL_DASH`. The refill guard is kept.
- **Refuted:** refill dash as an improvement; the "refill fall is wasteful" premise.
- **Kept measurement:** the refill cycle is minimal (98-tick fall to the floor, full 130 on landing);
  dash is active on ~1% of all ticks.
- Weak wing, policy off, guard 0: **4 hits at 3000-4000 ticks.**
- **No native zero over a full fight on either loadout.**

## 30. Round 69: the pre-hit window is the missing instrument, and it shows the player pinned

### 30.1 The pre-hit window exists and is the best diagnostic built so far

`artifacts/game-probe-<run>/prehit-observations.jsonl` (`schema chaite-prehit-observation/v2`)
records, for **every damage event**, the 47 ticks leading up to it, with the full player and Boss
state on each: position, velocity, `wingTime`, `immuneTime`, `dashType`, all eight raw control
booleans, the plan phase, and the Boss's `x/y/vx/vy/ai0..ai3/state/timer/sequence`, plus
`nearestThreat`, `threatsWithin400`, `nearestProjectile`, `projectilesWithin400`. There is also
`hurt-observations.jsonl` (`chaite-hurt-observation/v1`), whose `source.kind` is `npc` with
`type 370` on every event.

That settles the damage question from section 25 for good: **every hit is Boss (NPC type 370)
contact.** No projectile channel is involved in any of the recorded damage, and the
`projectilesWithin400` field is available to prove it per hit.

### 30.2 The player is pinned at x = 650.0 with the horizontal commanded

Across the latch-trace run the player's centre x is pinned at exactly `650.0` in two contiguous
episodes, **163 ticks (2.7 s) and 110 ticks (1.8 s), 13% of all rows**. Both sit inside hit
windows. In the 47-tick window of one hit (`hurtSequence 2`), the picture is:

```
 off   px      bx      gap     vx     phase
 -47   650.0   738.8    +88.8  +0.00  precharge-jump
 -35   650.0   763.9   +113.9  +0.00  precharge-jump
 -20   650.0   720.2    +70.2  +0.00  charge-descend
  -5   650.0   677.5    +27.5  +0.00  personal-space
   0   650.0   678.1    +28.1  +0.00  personal-space   <- hit
```

The Boss stays **90-114 px to the player's right for the whole window**, so `gap > 0`
throughout, so `AwayFromBossAxis(gap)` returns `-1` throughout. The plan confirms it: in a
controlled run the recorded `plan.horizontal` is `-1` while `controlLeft` is 1 and
`controlRight` is 0 -- and `vx` is still exactly `0.00` with `px` frozen at `650.0`.

**So the player is not failing to choose a direction. It chooses left, holds left, and does not
move.** The horizontal is commanded and the position does not change. That is the defect, and it
is a different one from every mechanism proposed in sections 26-29: not a wrong sign, not a clamp,
not the budget. A dodge whose horizontal never executes cannot open vertical or horizontal
separation, and the charge simply arrives.

The player demonstrably *can* move horizontally in the same run -- later the same stream shows
`px 2200.0, vx +6.70` in `charge-ascend` -- so this is not a global movement failure and not an
input-plumbing failure. It is specific to the early `standoff`/`precharge`/`charge-descend`
episodes near the left of the arena, and it is unexplained.

### 30.3 What was tried and reverted

Two edits were made on the hypothesis that `AwayFromBossAxis` was returning 0 on the Boss axis and
so starving the horizontal. **That premise is false**: `AwayFromBossAxis(float gap)` is literally
`gap >= 0f ? -1 : 1` (`FishronWingScript.cs:1030`) and can never return 0, and the recorded `gap`
is +88.8 to +113.9 in the failing window anyway. Both edits measured **bit-identically** to the
baseline (4 hits, 66 Boss damage, 32 dash ticks), which is what an inert change looks like. Both
were reverted with `git checkout --`.

The lesson is the section-27 one again, and it cost two builds here: the mechanism has to be
checked against the recorded value **before** the edit is believed, not after.

### 30.4 Status

- **Reverted:** both horizontal-starving edits and their `PrechargeMinSpeed` constant.
- **New instrument:** `prehit-observations.jsonl` / `hurt-observations.jsonl` per-hit windows.
- **Settled:** all recorded hits are Boss NPC contact; `source.kind = npc`, `type 370`.
- **New defect, unexplained:** the horizontal is commanded (`plan.horizontal = -1`,
  `controlLeft = 1`) while `vx = 0.00` and `px` is frozen at `650.0` for up to 163 ticks; both
  pinned episodes contain a hit.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks.**
- **No native zero over a full fight on either loadout.** Note also that a 600- and a 900-tick run
  both reported `HITS 0` with the Boss at full life; those are run-length artefacts (the first
  charge lands later than that) and are **not** evidence of a no-hit solution.

## 31. Round 70: the stall is real, and it is not embedding

### 31.1 The instrument chain that produced this

A one-shot world scan was added to the probe's `ApplyPlan` observer (since reverted) and a
one-shot stall dump firing on the signature "the plan asks for a horizontal and the body does not
move". On a 700-tick weak-wing run the scan and dump report:

```
GEOM_SCAN cx=40 cy=498 top=497 bottom=500 centreX=650.0
          leftSolidTile=40 rightSolidTile=40 wet=False
          arenaLeft=1 arenaRightExclusive=400 worldSurface=500 rockLayer=750
          maxTilesX=4200 spawnTileX=200

STALL_FRAME ticks=240 px=650.0 py=7979.0 vx=0.000 vy=0.000
          planHor=-1 planJump=True planDrop=False
          ctlL=False ctlR=False ctlJ=False ctlD=False
          eocDash=0 dash=2 dashDelay=0 wingTime=130 wet=False immune=False
          grappled=False mount=False frozen=False webbed=False stoned=False
          dead=False CCed=False

STALL_AFTER ticks=240 ctlL=True ctlR=False ctlJ=True ctlD=False vx=0.000 px=650.0
```

`STALL_FRAME` fires **at `ApplyPlan` entry**, so its control fields are the pre-write state and
being false there means nothing. `STALL_AFTER` fires **after** the write and shows
`ctlL=True, ctlJ=True` with `vx` still exactly `0.000` and `px` still exactly `650.0`.

**So the plugin writes the controls, the engine's own fields read back True, and the body does not
move.** That rules out the two explanations this round was chasing: it is not a missing control
write, and it is not `eocDash` (`eocDash=0`, `dashDelay=0`; `dash=2` is simply the equipped
Shield), and it is not a movement-state lock (`frozen/webbed/stoned/dead/CCed/grappled/mount/wet`
are all false). The world scan also shows **no solid tile on the player's own rows within 200
tiles either side**, so it is not a wall.

### 31.2 The embed hypothesis was wrong

`player.position` was `new Vector2(playerStartTileX * 16, arenaStartY * 16 - player.height)`
(`GameProbe.cs:2577`), which puts the body's bottom edge on the first solid pixel of the floor
(the ground pass builds `arenaGroundY .. arenaGroundY+thickness`, so solid starts at
`arenaGroundY*16`). A spawn with one pixel of clearance is what native produces, so the line now
subtracts 1, and a one-shot spawn log confirmed `bottom=7999.0 groundTopPx=8000`.

**But this did not change the outcome: 4 hits and 66 Boss damage, the same as before, and the
stall still fires at tick 240 with `py 7979.0`.** The reason is that `py` in the stream is the body
**centre**, not the feet: centre 7979 with height 42 means the feet were at about 8000, i.e. the
body was at most **1 px** into the floor, not deeply embedded. My "the player is embedded in the
ground" reading was therefore wrong, and the stall has some other cause that is still unidentified.

The clearance fix is kept anyway because placing a body inside a solid pixel is wrong on its own
terms and one pixel is the native spawn clearance; it is recorded here as **not** a hit-count
improvement.

### 31.3 What the stall is and is not

Still true and unexplained: the plan commands a horizontal, the engine receives it
(`controlLeft` True), and `velocity.X` stays exactly 0 for up to 163 ticks while horizontal motion
works normally in other phases of the same run (`precharge-jump` mean |vx| 5.65, `charge-ascend`
9.44, `charge-horizontal-dash` 14.50, and the identical phases later in the same run move at
6.7-13.4). Both pinned episodes contain a hit. This is the live defect.

Ruled out this round: missing control write; `eocDash`; dash state; grappled/mount/frozen/CCed;
a wall on the player's rows.

### 31.4 Status

- **Kept:** one pixel of spawn clearance in `GameProbe.cs` (correctness, not a hit improvement).
- **Reverted:** the `GEOM_SCAN`, `STALL_FRAME` and `STALL_AFTER` diagnostics.
- **Tests:** 750 passed, 8 failed -- the same 8 pre-existing failures from the missing
  `tests/Chaite.Tests/fixtures/observation-conformance.jsonl`.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks.**
- **No native zero over a full fight on either loadout.**

## 32. Round 71: the charge-capsule criterion explains every recorded hit

### 32.1 The criterion

`AI_069` locks once (`ai[0]=1; ai[1]=0; ai[2]=0; velocity = Normalize(player.Center - center) * num7`)
and then travels a **fixed 476 px** along that locked line at 17 px/tick (expert). Contact needs the
centres within roughly `|dx| < 85` and `|dy| < 71`, so the threat is a **capsule**: the segment of
length 476 from the lock centre along the locked aim, inflated by 85 px laterally. A player is hit
exactly when its centre is inside that capsule.

**This explains all five recorded hit windows, with no exceptions:**

```
 hit  aim             lock dist   ticks inside the capsule
  1   (-0.90,+0.43)     490.8      0    (never inside)
  2   (-0.17,-0.99)     646.9      0    (never inside)
  3   (-0.74,+0.67)     254.8     19
  4   (+0.91,+0.41)     364.4     20
  5   (-0.94,+0.33)     303.3     18
```

Hits 1 and 2 never put the player inside the capsule at all, and they are the two that land on the
**pinned episodes** of section 30 -- a body frozen at `vx = 0` with no lateral motion cannot leave
the line, so it is caught by whatever arrives. Hits 3, 4 and 5 are the tunnel cases: the player sits
inside the capsule for 18-20 consecutive ticks with a small perpendicular offset (perp 0.0/0.0/3.4
at entry, still only 11.4/33.9/41.7 at impact) while the charge runs down the line straight into it.

The perpendicular distance is the whole story. At the moment of impact the players of hits 3-5 are
still **within 85 px of the line** -- they never got out of the tunnel. This is exactly the owner's
rule: the locked charge cannot be outrun along the line (17 px/tick against `maxRunSpeed` 4.71 and
`accRunSpeed` 6.75), so the escape must be along the **normal**, and it must exceed 85 px before the
charge arrives.

A counter-check confirms that raw separation is *not* the criterion: on every one of the five locks
the player's distance exceeded `CONTACT + dist * |pvel| / CHARGE`, the naive "does the player outrun
the tip" test, so a speed-based reading would have called all five safe. Distance from the lock
point says nothing; distance from the line says everything.

### 32.2 What this makes the target

The circuit already computes the correct escape axis: `LatchChargeNormal` returns the unit
perpendicular to the locked aim and is arithmetically verified (section 27). What it does not do is
**get far enough along that axis in time**. The requirement is concrete and checkable:

> during a charge, the player's perpendicular distance from the locked line must reach **85 px
> before the charge reaches the player's along-track position**, or the player must already be
> outside the 476 px range.

That is a two-number target rather than a heuristic, and it is measurable per charge from the same
`prehit`/dense streams. The next step is to instrument per-charge minimum perpendicular distance and
raise the vertical share of the escape until it clears 85, which is what sections 19 and 22 could
not quantify.

### 32.3 The stall is not the hit source

A stall breaker was built (detect `horizontal != 0` with `|vx| < 0.05` for 8 ticks while airborne,
then force `vertical = -1` and spend the dash) and swept in one invocation:

| `CHAITE_STALL_BREAK` | hits | ticks | boss damage | death |
|---|---|---|---|---|
| 0 (off) | **4** | 3000 | 66 | no |
| 1 (on) | **7** | 2818 | 86 | **yes** |

It is **clearly harmful** and is reverted. So although the stall is real (section 31), breaking it
makes the fight worse, and it is not where the hits come from. One plausible reading is that the
frozen frames are being spent near the arena floor where staying put happens to be safe for the
charges that occur there, and the forced jump moves the body into worse positions.

### 32.4 Correction: measured against the Boss's actual path

The 32.1 figures used a nominal 476 px charge range and the aim vector sampled at the lock, and two
of the five hits came out marginally outside that nominal capsule. Re-running the test with **no
assumed range** -- projecting each player position onto the segment the Boss actually flew, from its
position at the lock to its position at the hit -- removes the discrepancy and makes the picture
sharper:

```
 hit  boss travelled   min centre distance   min perp distance to the flown path
  1      425.0 px            82.0                    60.9
  2      563.5 px            56.7                    53.8
  3      306.0 px            40.0                     0.0
  4      323.0 px            27.0                     0.6
  5      289.0 px            35.2                     1.2
```

Two corrections to 32.1 fall out of this:

- The charge **does not travel a fixed 476 px** in practice: the recorded flights are 289-563 px,
  because the Boss's own speed and the player's displacement both change the geometry. The 476 px
  figure is the nominal travel at the locked speed and must not be used as a hard range.
- The **lateral clearance needed is about 85 px**, and it is a measured data-derived threshold taken
  from hitbox overlap rather than a derivation of the capsule: `boss 150x100` against
  `player 20x42` gives exactly `|dx| < (150+20)/2 = 85.0` and `|dy| < (100+42)/2 = 71.0`, and every
  one of the five hit frames satisfies both. Minimum centre distance at closest approach is 27-82 px,
  inside the contact box in every case.

**The criterion is the perpendicular distance to the flight path, and the circuit has never achieved
even 61 px of it.** `personal-space` fires at `separation < PersonalSpace` (about 200 px) and escapes
on `AwayFromBossAxis(gap)` -- a purely **horizontal** exit -- and the measured lateral result is
60.9 and 53.8 px on the two hits that used it, and essentially zero on the three that did not. A
purely horizontal exit from a charge that is itself aimed nearly horizontally is parallel to the
threat, not perpendicular to it, which is precisely the failure the owner's rule warns about: the
escape must be the **normal** to the locked aim, and for a near-horizontal charge the normal is
near-vertical.

This is the quantified target for the next round: the escape must be the perpendicular to the locked
aim (which `LatchChargeNormal` already computes correctly), and it must reach **>= 85 px of lateral
clearance** before the charge arrives, which requires a vertical share that the horizontal-only
`personal-space` exit cannot supply.

### 32.5 Status

- **Reverted:** the stall breaker and `CHAITE_STALL_BREAK`.
- **Kept:** one pixel of spawn clearance (section 31); the refill guard.
- **New criterion:** the charge capsule, which explains all five recorded hits and refutes the
  speed-based reading.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks.**
- **No native zero over a full fight on either loadout.**

## 33. Round 72: the perpendicular-clearance metric, and a third refuted axis change

### 33.1 The metric

Section 32 established that a charge is survived exactly when the player's **perpendicular distance
from the Boss's flown path** reaches 85 px (hitbox overlap: `boss 150x100`, `player 20x42`). That is
now measured per lock over one dense fight (45 locks; every lock registered as the state-1 edge with
`ai1 == 0`), sampling perpendicular distance at 0/5/10/15 ticks after the lock:

```
 lock  dist  aimY   perp@0  perp@5  perp@10 perp@15  maxPerp  need  reached85?
  344  490.8  +0.43      0.0    18.0    38.2    60.7     60.7  85   NO
  402  233.8  -0.96      0.0    54.5    91.7   112.2    112.2  85   yes
  460  344.7  -0.02      0.0    44.4    78.9   103.7    103.7  85   yes
  518  303.3  +0.60      0.0    40.1    85.9   131.9    131.9  85   yes
  754  506.4  +0.31      0.0    56.1   112.3   166.6    166.6  85   yes
  870  538.6  -0.42      0.0     1.3     3.4    11.3     11.3  85   NO
  928  370.0  +0.46      0.0    24.2    53.9    46.9     53.9  85   NO
  986  454.7  +0.05      0.0    16.7    43.8    81.6     81.6  85   NO
 1348  455.5  +0.81      0.0    26.9    47.7    64.1     64.1  85   NO
 1584  893.8  +0.45      0.0    24.1    53.7    85.9     85.9  85   yes
 1642  201.4  +0.94      0.0    70.9   135.7   188.2    188.2  85   yes
 1758  340.0  +0.55      0.0    31.2    66.7    59.7     66.7  85   NO
 1816  333.6  +0.08      0.0    13.9    17.2     9.4     17.2  85   NO
 2004 1358.8  +0.66      0.0    72.0   141.1   202.8    202.8  85   yes
 2062  858.1  +0.08      0.0    37.3    64.1    79.6     79.6  85   NO
```

**14 of 22 sampled locks reach 85 px within 15 ticks.** The failures are not correlated with the
lock distance: a lock taken at 201.4 px clears 188.2 px of perpendicular, while one at 538.6 px
manages only 11.3 and one at 1358.8 px is fine. The striking feature is that the failures are
**flat or non-monotonic** -- `perp` goes 24.2 -> 53.9 -> 46.9, or 13.9 -> 17.2 -> 9.4, or
31.2 -> 66.7 -> 59.7 -- whereas the successes are steadily rising (0 -> 50 -> 99 -> 140). A flat or
falling perpendicular means the escape is not being executed at all, and the two almost-zero cases
(11.3 and 17.2) are the **stall episodes** of sections 30-31.

So the circuit separates cleanly into two failure populations: locks where the escape runs (and
mostly succeeds) and locks where the body does not move laterally (and always loses). The stall,
which section 32.3 showed is not *by itself* the cause of the hits, is nevertheless exactly the
condition of every failed clearance.

### 33.2 The perpendicular-axis change is refuted

Section 32's target said the close-range escape should follow the latched normal rather than the
purely horizontal `AwayFromBossAxis(gap)`. That was implemented behind `CHAITE_PERP_ESCAPE` (when a
charge normal is latched, `personal-space` returns it instead of the horizontal exit) and swept in
one invocation:

| `CHAITE_PERP_ESCAPE` | hits | boss damage | dash ticks |
|---|---|---|---|
| 0 (off, reviewed) | **4** | 66 | 32 |
| 1 (on) | **7** | 150 | 32 |

**Refuted -- it roughly doubles the damage taken.** The reason is visible in the `aimY` column: for a
charge locked from the hovering Boss the aim is often near-vertical (`+0.94`, `-0.96`, `-0.88`), and
for a near-**vertical** aim the perpendicular is near-**horizontal** -- which is what
`AwayFromBossAxis(gap)` already produced. So the change mostly replaced a working horizontal escape
with a latched normal that, at close range, points back along a nearly horizontal line into the
incoming charge. The section-32.1 reasoning ("the escape must be the normal") is correct as
geometry, but it does not follow that `personal-space` should abandon its own side choice, because
the two are not the same vector at close range.

This is the fourth structural axis change measured and refuted: floor-escape modes (28), minimum
altitude (28), the refill dash (29), the stall breaker (32), and now the perpendicular close-range
escape. Every one was a plausible reading of the geometry and every one lost to the reviewed circuit.

### 33.3 Status

- **Reverted:** `CHAITE_PERP_ESCAPE` and its use.
- **Kept:** one pixel of spawn clearance (31); the refill guard.
- **New metric:** per-lock perpendicular clearance against the 85 px need; **14 of 22 reach it**.
- **New reading:** every failed clearance is a flat-or-falling `perp` trace, and the two near-zero
  ones are the stall episodes -- so the escape runs correctly on most locks and does not run at all
  on the ones that fail.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks.**
- **No native zero over a full fight on either loadout.**

## 34. Round 73: the replay channel was starved, mis-keyed and mis-read -- all three fixed

The objective's acceptance口径 is native per-tick replay through `CHAITE_ROUTE_FILE`. It did not
work at all, for three independent reasons, each of which had to be fixed before a route could even
be built. All three are now fixed and the route demonstrably reaches the plan.

### 34.1 The probe starved the channel (`GameProbe.cs`)

`actualAtApplyReturn` was emitted only under `if(hasObservedPlanReturn)`, which is set on the tick an
apply returns. An apply returns once per charge sequence, so the snapshot was written on **4 of 221**
rows while `plan` was null on the other 217. `tools/harvest-native-route.py` prefers
`actualAtApplyReturn` and falls back to `plan`, so it read the entire fight as neutral. The snapshot
is now emitted on **every** row, and its `tick` falls back to the current tick, with a new `fresh`
flag so a reader can still distinguish a live apply from a held value. The underlying `observed*`
fields are persistent readbacks of the player's own controls, so the last written value *is* the
state that persists into every tick in between, which is exactly what a replay needs.

### 34.2 The harvester read the wrong key names (`tools/harvest-native-route.py`)

The reader looked for bare `left`/`right`/`jump`/`up`/`down`/`dash`, but the control snapshot uses the
native `Player` field names with a `control` prefix (`controlLeft`, `controlRight`, `controlJump`,
`controlUp`, `controlDown`, `controlDash`); the bare names exist only on the plan shape. Every row
therefore decoded as `(0,0,0,0,0)`: a harvest over a 1200 tick fight reported
`left=0 right=0 jump=0 dash=0` while the stream itself carried `L=True R=False J=True` on 17 rows.

It now prefers the **plan**, and that is the correct source rather than the facade's output. The
route channel writes `plan.Horizontal`, `plan.Jump`, `plan.Dash`, `plan.Drop`, `plan.FeatherFallUp`,
so the route carries a control **request** and `plan` is the record of that request. Harvesting the
facade's output does not round-trip, because `MovementActionGate.ResolveJump` is not idempotent:
putting the gate's own output back through the gate suppresses the jump.

### 34.3 The route was keyed on the wrong tick axis (`GameProbe.cs`)

`RouteReplay.TryReadTick` compares the route's absolute tick against
`Runtime.CurrentGameTick()`, which is `Main.GameUpdateCount`. The probe's own `ticks` counter does
**not** share that origin. A route harvested on `ticks` (1..1200) was therefore applied at a constant
offset. The boss row now publishes `gameUpdateCount` and the harvester keys on it, falling back to
`ticks` only for older streams with a note that such a route is offset. The harvested range moved
from `1..1200` to `2..1200`, confirming the offset is real.

### 34.4 What works now

With all three fixed, one dense 1200-tick run (weak wings, **1 hit**) harvests to a complete
**1200/1200 tick route with 0 neutral fillers** (`left=271 right=636 jump=432 dash=11`), and
replaying it gives `replayFrame=0` at tick 240 with `plan.jump=True plan.horizontal=-1`, **exactly
matching the live run**. The route is read, covers every frame, and lands on the plan.

### 34.5 The remaining gap: a control bit is not a faithful reproduction

The replay still does not reproduce the fight -- **6 hits and a death against the recorded 1 hit**.
The divergence is isolated to a single frame and is instructive: at tick 240 the live run and the
replay have **identical applied controls** `(L=True, R=False, J=True, D=False, Dash=False)` and
identically seeded state, yet the live player is at `y 7951.5` (it rose) and the replay player is
still at `y 7958` (it did not). The control bit is the same and the body behaves differently, so
`controlJump` alone does not determine whether a jump happens: `MovementActionGate.ResolveJump` and
the native wing/jump state machine carry memory across frames that the harvested per-tick bits do
not encode. Reproducing the recorded fight therefore needs the route to carry the *resolved* input
each frame, or the gate's cross-frame state, rather than only `plan`'s request bits.

This also means the earlier `standoff-dense` zero-hit artifact cannot be trusted as evidence either:
it came from an older build (different DLL hashes, `bossDamage` 18 over 4000 ticks, i.e. a barely
fought run) and the same configuration on the current build gives 4 hits / 84 damage. **No native
zero-hit full fight exists on either loadout.**

### 34.6 Status

- **Fixed:** the starved snapshot, the harvester's key names and source, and the tick axis. A
  complete non-neutral route can now be built and is read by the engine.
- **Open:** the replay's per-tick control bits do not reproduce the recorded jump, so the acceptance
  channel is not yet a faithful replayer.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**; dense 1200-tick run: 1 hit.
- **No native zero over a full fight on either loadout.**

## 35. Round 74: the replay reads the route and still does not move the body

### 35.1 The divergence is total, not vertical

Section 34.5 isolated the replay divergence to tick 240, where the live and replay runs have
identical applied controls. Probing the full player state at that tick shows the divergence is not
about the jump at all:

```
tick 240 dense3   pos=(640,7952) vel=(0.00,-6.48) wingTime=130
tick 240 replay3  pos=(640,7958) vel=(0.00, 0.00) wingTime=130

tick 420 dense3   pos=(845,7324) vel=(5.87,-9.28) wingTime=38
tick 420 replay3  pos=(640,7885) vel=(0.00, 1.47) wingTime=130
```

At tick 240 the replay's body has **velocity (0,0)**, and at tick 420 its **x is still 640** while the
live body has moved to 845 -- with `plan.horizontal = -1` in both. So the replay is not merely failing
to jump; **it is not moving horizontally either**, and `velocity` is exactly zero while `wingTime`
sits at its full 130. Over the whole run the live body spans y 5875.9..7958 (1082 px of motion) and
the replay only 7842.0..7958 (116 px), all of it near the spawn position.

### 35.2 The jump gate is not the cause

`MovementActionGate.ResolveJump` was the natural suspect, since it is the only thing between
`plan.Jump` and `controlJump` and it carries cross-frame state. It was bypassed behind
`CHAITE_JUMP_DIRECT` (setting `controlJump` straight from `plan.Jump`) and the replay was re-run:

| replay configuration | hits | boss damage | death | y range |
|---|---|---|---|---|
| normal | 6 | 54 | yes | 7842..7958 |
| gate bypassed | 6 | 54 | yes | 7842..7958 |

**Bit-identical.** The gate is therefore eliminated, and so is the section-34.5 reading that the
problem is jump-specific state. Since the horizontal channel fails the same way and it has no gate,
the fault is upstream of every individual control: in replay mode the plan is written and the body
ignores it.

### 35.3 What is established

- The route **is** read: `replayFrame=0` at tick 240 with `plan.jump=True plan.horizontal=-1`,
  matching the live run exactly.
- The controls **are** written: the observed `controlLeft`/`controlJump` read back True after
  `ApplyPlan` returns.
- The body **does not move**: `velocity` is exactly `(0,0)` and `x` does not change, on a channel
  (`horizontal`) that involves no gate.
- `wingTime` stays at 130 in the replay, i.e. the flight budget is never spent, which is consistent
  with a body that never leaves the ground.

So the plugin's plan reaches the player and the player behaves as though it were not being driven.
The next instrument is an A/B on the write itself: compare the player's control fields immediately
after `ApplyPlan` returns against the fields at the top of the following `Player.Update`, which
separates "the write did not persist" from "the write persisted and the native update ignored it".

### 35.4 Standing evidence

- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**; dense 1200-tick run: 1 hit.
- The dense route harvests to 1200/1200 ticks with 0 neutral fillers and replays to **6 hits and a
  death**, so the acceptance channel is still not a faithful replayer.
- `standoff-dense`'s zero-hit artifact remains disqualified: older build, 18 damage over 4000 ticks.
- **No native zero over a full fight on either loadout.**

## 36. Round 75: in replay mode the controls are cleared before `Player.Update`

### 36.1 The A/B §35 asked for

Section 35 left two possibilities: the plan write does not persist, or it persists and the native
update ignores it. A one-shot log at the **tick entry** (inside `BeforeUpdate`, which runs after the
previous tick's `Player.Update` returned and before this tick's native update) settles it. Same route,
same tick, two runs:

```
t=241 LIVE    L=True R=False J=True D=False Dash=False vx=0.00 vy=-6.48 px=640 py=7952
              lastWrite L=True J=True replayFrame=-1 hasReturn=True
              whoAmI=0 active=True dead=False CCed=False frozen=False webbed=False stoned=False
              gravDir=1 wingTime=130 wingsLogic=6 jump=15 releaseJump=False mapFull=False gameMenu=False
              velocity={X:0 Y:-6.476667} sameRef=True

t=241 REPLAY  L=False R=False J=False D=False Dash=False vx=0.00 vy=0.00 px=640 py=7958
              lastWrite L=True J=True replayFrame=0 hasReturn=True
              whoAmI=0 active=True dead=False CCed=False frozen=False webbed=False stoned=False
              gravDir=1 wingTime=130 wingsLogic=6 jump=0 releaseJump=True mapFull=False gameMenu=False
              velocity={X:0 Y:0} sameRef=True
```

Every state flag that could explain a refusal to move is **identical**: the same player object
(`sameRef=True`, `whoAmI=0`), `active=True`, and `dead`/`CCed`/`frozen`/`webbed`/`stoned` all false,
with the same `gravDir`, `wingTime` and `wingsLogic`, and `mapFullscreen`/`gameMenu` false. The one
difference that matters is at the top: **`lastWrite J=True` but the tick entry reads `J=False`**. In
the replay the control the plugin just wrote is **gone** by the time the tick begins, whereas in the
live run it survives and produces `vy=-6.48`.

The native jump state confirms which run actually executed the jump: live has `jump=15` (mid-jump,
`releaseJump=False`) while the replay has `jump=0` (`releaseJump=True`), i.e. **the replay never
entered the jump at all**.

### 36.2 What this rules out and what it establishes

- It is **not** the write: `lastWrite` is true in both runs, and §35 already showed the write reaches
  the player's own fields after `ApplyPlan` returns.
- It is **not** the jump gate: §35 bypassed `MovementActionGate.ResolveJump` bit-identically, and the
  same clearing would remove a gate-free `controlLeft` too.
- It is **not** a movement-state lock: every refusal flag is identical between the runs.
- It is **not** a terrain or embedding effect: the body is at the same `py` with `vy=0` in both.

**It is the persistence of the control fields across the native update.** In replay mode something
between the plugin's write and the next tick's `Player.Update` clears the controls, so the body is
never driven; the live path keeps them and the body moves. This also explains the §35 observation
that horizontal motion fails identically (`vx=0.00`, `x` frozen at 640 while live reached 845) and
that `wingTime` stays pinned at its full 130 -- a body that is never driven never spends flight.

### 36.3 The candidate

`RouteReplay` does not write player fields at all; it only rewrites the *plan*, which the facade then
applies exactly as in the live run. The difference in mode is therefore not the write path but the
per-frame input path that runs between frames. `Player.Update` contains a control-reset block
(`Player.cs:25446`) but it is gated on `CCed`, which is false in both runs, so it is not this. The
live run's control survives to the tick entry while the replay's does not, so the next step is to log
the controls at three points inside one replay frame -- immediately after `ApplyPlan` returns, at the
top of `Player.Update`, and immediately after it -- to name the exact instruction that clears them.

### 36.4 Status

- **Diagnostics reverted**; the §34 fixes (`actualAtApplyReturn` on every row, `fresh`, and
  `gameUpdateCount`) are intact and the tree is clean.
- **Established:** in replay mode the written controls do not survive into `Player.Update`, on a
  channel with no gate and with every refusal flag identical to the live run.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**; the same configuration at 600 ticks
  reports 0 hits with the Boss at full life, which is a run-length artefact and not a solution.
- The dense route replays to **6 hits and a death** against the recorded 1 hit, so the acceptance
  channel is still not a faithful replayer.
- **No native zero over a full fight on either loadout.**

## 37. Round 76: the native control reset, and the replay/plugin phase error

### 37.1 The mechanism is named

`Player.Update` calls a private `ResetControls()` **on entry** for the local player
(`Player.cs:24973-24975`):

```
if (i == Main.myPlayer && !isControlledByFilm)
{
    ResetControls();
    ...
```

and `ResetControls` (`Player.cs:29298`) clears `controlUp`, `controlLeft`, `controlDown`,
`controlRight`, `controlJump`, `controlUseItem` and the rest. That is the only place in the whole
decompile that clears the player's controls unconditionally for the local player -- the other reset
sites are gated on `Main.mapFullscreen` (25003), `spectating >= 0` (25021), a creative-menu branch
(25068), `CCed` (25450) and the film stage, and all of those are false in this fight. So **every frame
the controls are wiped at the top of `Player.Update`**, and any write that lands before that point is
discarded.

### 37.2 The measured A/B, on the same tick

`applyCalls` (the plugin's `ApplyPlan` count) was sampled at the tick entry across five consecutive
ticks in both modes. The two runs are identical up to t=240 and then separate:

```
LIVE    t=239 applyCalls=0 L=False J=False vx=0.00 vy=0.00  py=7958
        t=240 applyCalls=0 L=False J=False vx=0.00 vy=0.00  py=7958
        t=241 applyCalls=1 L=True  J=True  vx=0.00 vy=-6.48 py=7952
        t=242 applyCalls=2 L=True  J=False vx=0.00 vy=-6.34 py=7945
        t=243 applyCalls=3 L=True  J=False vx=0.00 vy=-6.21 py=7939

REPLAY  t=239 applyCalls=0 L=False J=False vx=0.00 vy=0.00  py=7958
        t=240 applyCalls=0 L=False J=False vx=0.00 vy=0.00  py=7958
        t=241 applyCalls=1 L=False J=False vx=0.00 vy=0.00  py=7958
        t=242 applyCalls=2 L=True  J=False vx=0.00 vy=0.00  py=7958
        t=243 applyCalls=3 L=True  J=False vx=0.00 vy=0.00  py=7958
```

`applyCalls` advances **identically** in both runs, and `isControlledByFilm` is `False` and
`myPlayer` is 0 in both, so the plugin is invoked the same number of times and the film branch is not
involved. The difference is what the controls read at the tick entry: in the live run t=241 shows
`L=True J=True` with the body already at `vy=-6.48` and rising, whereas in the replay t=241 shows
`L=False J=False` with `vy=0.00` and `py` frozen at the spawn value **for all five ticks**.

So in the replay the write is being issued and then erased before the update reads it, while in the
live run the same write survives. Combined with 37.1 this is a **phase error**: the plugin's tick
runs in a place where its write lands ahead of `ResetControls` in the replay, and after it in the
live path. `Runtime.Tick` is reached either from the native update or from a probe callback, and the
mode changes which.

### 37.3 The fix this points to

The write must land **after** `Player.Update`'s `ResetControls` and before the movement code reads
the controls. The probe already owns a hook at exactly that point -- `MotionAfterInput(player)`, which
fires after the native input phase -- so the replay path can be repointed there instead of running the
plugin earlier in the frame. Testing that is the next step.

### 37.4 Correction to §35's reading

§35 concluded that "the write does not persist" and left open whether the body ignores a persistent
write. 37.1 names the reason it does not persist, and §36's finding that every refusal flag was
identical (`CCed`, `frozen`, `webbed`, `stoned`, `dead`, `mapFullscreen`, `gameMenu`) is consistent:
this is not a state lock, it is the unconditional per-frame reset. §35's elimination of the jump gate
also still holds, since `ResetControls` would erase a gate-free `controlLeft` in exactly the same way
-- which is why the horizontal channel failed identically.

### 37.5 Status

- **Reverted:** the `ORDER` diagnostic; the §34 fixes (`actualAtApplyReturn` on every row, `fresh`,
  `gameUpdateCount`) are intact and the tree is clean.
- **Established:** `Player.Update` runs `ResetControls()` on entry for the local player, and in replay
  mode the plugin's control write is erased before the update reads it, while in the live run the same
  write survives. Same `applyCalls`, same player, `isControlledByFilm` false in both.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**; the 600-tick configurations that report 0
  hits are run-length artefacts (Boss at full life).
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 38. Round 77: the staged-input path, and why the replay deadlocks on the ground

### 38.1 How the plugin's controls actually reach the player

The plugin does not rely on its `ApplyPlan` write surviving on its own. It uses a **staged** path, and
the patcher shows exactly where each half sits (`GameProbePatcher.cs:38-53`):

- `Player.Update` is prefixed with `MotionBeforePlayerUpdate`, and has `ApplyPendingInput` injected
  after the native branch that copies raw controls (`:42-47`).
- `WingMovement` is prefixed with `FlightBeforeWing` (`:52`) and suffixed with `FlightAfterWing`
  (`:53`). **`Runtime.Tick` -- which is the only caller of the planner and of
  `_game.ApplyPlan` -- is reached from this site**, so the whole circuit runs inside `WingMovement`.
- `JumpMovement` gets `MotionBeforeJump`/`MotionAfterJump` (`:49-50`), and `DashMovement` gets
  `BeforeShieldDash`/`AfterShieldDash` (`:55-56`).

`Runtime.Tick`'s `finally` block then stages the frame (`Runtime.cs:579-591`): if the frame was
applied and the encounter is controlling it calls

```
_game.CapturePendingInput(player);
_pendingInput = true;
```

and `ApplyPendingInput` (`Runtime.cs:597-602`) replays it on a later frame, where the facade's
`ApplyPendingInput` (`TerrariaFacade.cs:3627`) does exactly

```
foreach (var pair in _capturedControls) _controls[pair.Key](player, pair.Value);
```

That is a **deliberate second write of the same controls, placed inside `Player.Update` so it lands
after `ResetControls`** -- which is precisely the requirement §37 derived independently. The design is
already correct.

### 38.2 Why the replay never gets off the ground

`Runtime.Tick` living inside `WingMovement` means the circuit only runs on a frame where the native
wing movement is invoked, and `WingMovement` is entered only when the player is already airborne with a
wing and the jump held (`wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 &&
velocity.Y != 0`). The staged replay then closes a cycle:

1. `ResetControls` (`Player.cs:24975`) clears `controlJump` at the top of `Player.Update`.
2. If the body is standing on the ground with `velocity.Y == 0`, `WingMovement` is not invoked, so
   `Runtime.Tick` does not run, so nothing stages this frame.
3. The buffered `_capturedControls` from the previous frame are re-applied -- but the buffered frame
   was itself computed on a frame where the body was not flying, so the buffer holds a state that
   cannot start the flight.

In the live run the circuit is already being driven from the ground frame whose buffer contains the
takeoff, and each frame's real flight keeps re-arming it; in the replay the first frames have no such
armed buffer, so `controlJump` is cleared and never re-supplied, `WingMovement` never runs, and the
body sits at the spawn point for the whole fight. That matches every measurement:

- `applyCalls` advances identically in both modes (§37.2), because `Runtime.Tick` runs on the same
  schedule in both;
- `py` is frozen at `7958` and `velocity` exactly `(0,0)` in the replay (§37.2), i.e. the body never
  becomes airborne;
- `wingTime` stays at its full 130 (§35.1), because flight is never spent;
- the **horizontal** channel fails identically (§35.1) even though it has no gate, because it is
  cleared by the same `ResetControls` and only restored by the same stalled stage.

### 38.3 What this changes about the fix

§37 proposed repointing the replay to `MotionAfterInput`. 38.1 shows that hook fires **before**
`ApplyPendingInput` in `Player.Update`, so it is the wrong side of the reset, and the correct anchor is
the existing injected `ApplyPendingInput` call itself. The condition to break is the cycle in 38.2:
the circuit must also run on a frame where `WingMovement` is **not** entered (a grounded frame), so
that the takeoff can be staged from the ground. `JumpMovement` already carries `MotionBeforeJump` and
runs whenever a jump is possible, which makes it the natural second driver -- it is entered from the
ground, which is exactly the state the cycle cannot leave.

### 38.4 Status

- **Reverted:** the `STAGE` diagnostic in `Runtime.cs` (it referenced `plan` outside its scope, so the
  plugin failed to build and one run was measured against a stale DLL -- that run's result is
  discarded). The tree builds clean and is otherwise unmodified.
- **Established:** `Runtime.Tick` is reached only from `WingMovement`; the plugin's own staged
  `ApplyPendingInput` is designed to land after `ResetControls`, and the replay's failure is a
  ground-state cycle in which `WingMovement` is never entered, so nothing is ever staged.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 39. Round 78: the JumpMovement driver is blocked by the JIT, not by the design

### 39.1 What was built

Section 38.3 named the fix: the circuit must also run on a frame where `WingMovement` is not entered,
because that is the only state from which a takeoff can be staged. `JumpMovement` is entered from the
ground and already carries the `MotionBeforeJump` observer, so it is the natural second driver. Three
changes were made:

1. `Runtime.Tick` gained a once-per-native-tick gate (`_lastTickFrame`), because it is now reached from
   two hooks and a second entry in the same tick would advance the route twice, re-plan and re-stage.
2. `GameProbePatcher` injected a `TickFromJump(player, 0)` call into `Player.JumpMovement`, placed
   **after** the `MotionBeforeJump` observer so the probe's own validation (`prepare-game-probe.ps1`,
   which requires `MotionBeforeJump` to remain at instruction 1) still passes.
3. `GameProbe` gained the `TickFromJump` hook, a no-op in motion cases, which calls
   `Chaite.Plugin.Runtime.Tick(player, 0)`.

### 39.2 The result

The first attempt failed the probe's own IL validation:

```
Motion preJump observer must precede the original JumpMovement body.
```

Inserting the call after the observer instead cleared that check, and the run then failed at the
runtime:

```
FAIL System.InvalidProgramException: JIT Compiler encountered an internal limitation.
   at Terraria.Player.JumpMovement()
   at Terraria.Player.Update(Int32 i)
   at ChaiteGameProbe.RunHeadless()
```

That is raised before a single tick runs (`ticks: 0`, `valid battle: False`, `boss seen: False`). The
injected call pushes two arguments, so `JumpMovement.Body.MaxStackSize` was raised to 2 as well -- the
method's computed stack depth predates the new instructions -- and the failure is **unchanged**. The
runtime is therefore not rejecting the IL for a stack-depth reason; `Player.JumpMovement` in 1.4.5.8
is JIT-fragile and adding instructions to it trips the JIT's internal limits.

This is a property of the target, not of the change: the same three-part design is sound (39.1) and the
route-advance concern is handled by the tick gate. What is needed is a **different insertion point that
is not inside a hot, JIT-sensitive `Player` method** -- for example driving the circuit from the probe's
existing `BeforeUpdate` prefix on `Main.DoUpdate`, which is a large method that the patcher already
successfully modifies, and staging the takeoff there instead of inside a movement method.

### 39.3 Reverted

All three edits are reverted; the tree builds clean and `git status` shows only the untracked `tmp/`.
The discarded runs (`jumpdrive2`, `jumpdrive3`) never reached a fight, so no measurement from them is
used.

### 39.4 Status

- **Blocked (not abandoned):** driving the circuit from `JumpMovement` fails with a JIT
  `InvalidProgramException` before the first tick, independent of `MaxStackSize`.
- **Next insertion point:** the `Main.DoUpdate` prefix (`BeforeUpdate`), which the patcher already
  modifies successfully and which runs every tick regardless of the movement state.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit, because the replay
  body still never leaves the ground.
- **No native zero over a full fight on either loadout.**

## 40. Round 79: correction -- `Runtime.Tick` runs at `Player.Update` entry

### 40.1 The error in section 38 and what it invalidates

Section 38.1 claimed that "`Runtime.Tick` -- which is the only caller of the planner and of
`_game.ApplyPlan` -- is reached from `WingMovement`", and built the whole ground-state-cycle
explanation in 38.2 on top of that claim. **That claim is wrong.** The probe's own comment records the
actual site (`GameProbe.cs:4256`):

```
// Runtime.Tick runs at Player.Update entry and its live-scope check
// rejects the session as soon as no active Boss root is left
```

and `NativeGrappleReader.cs:11` agrees: "called by `Runtime.Tick`'s hash-locked `Player.Update` entry
hook".

The mistake was reading the probe's own instrumentation as the production driver. The patcher lines
cited in 38.1 (`WingMovement` -> `FlightBeforeWing`/`FlightAfterWing`,
`JumpMovement` -> `MotionBeforeJump`/`MotionAfterJump`, `DashMovement` ->
`BeforeShieldDash`/`AfterShieldDash`, and `MotionBeforePlayerUpdate`/`MotionAfterInput`) are
**probe-only observers**; the probe deliberately keeps them as strict no-ops outside motion cases, and
a tree-wide search for `Runtime.Tick` finds **no caller in the patcher at all**. The plugin installs
its own hash-locked `Player.Update` entry hook to drive `Runtime.Tick`.

### 40.2 What this changes

- The 38.2 cycle ("the circuit only runs once already airborne, so a grounded frame never stages a
  takeoff") **does not hold**, because the circuit runs at `Player.Update` entry on **every** frame,
  airborne or grounded. Nothing about the replay's failure depends on `WingMovement` being entered.
- The 39 round's `JumpMovement` driver was therefore solving a problem that does not exist. Its JIT
  `InvalidProgramException` is still a real fact about patching that method, but the change was not
  needed for the reason I gave, and reverting it cost nothing.
- `ResetControls` (`Player.cs:24975`) is still the only unconditional local-player control reset, so
  the requirement that the write land **after** it stands. What is now open again is simply where
  `Runtime.Tick`'s write and `ApplyPendingInput`'s restore sit relative to it -- `Runtime.Tick` is at
  *entry*, which is the same place `ResetControls` runs, so the ordering between those two is the
  thing to pin down next, and it is a one-frame question rather than a state-machine question.

### 40.3 Why this is recorded rather than quietly fixed

Two consecutive rounds (38 and 39) reasoned from the wrong call site. The measurement trail was
self-consistent enough to make the wrong story look confirmed -- `applyCalls` advancing identically in
both modes, a frozen body, `wingTime` pinned at 130 -- because those observations are equally
consistent with "the circuit runs but its write is discarded every frame". The call site came from a
code comment, not from a measurement, and that is the specific discipline failure to avoid here: a
structural claim about native control flow must be measured, not inferred from adjacent patch code.

### 40.4 Status

- **Corrected:** 38.1 and 38.2 are withdrawn; `Runtime.Tick` runs at `Player.Update` entry.
- **Unaffected and still standing:** the capsule criterion (32), the per-lock clearance metric (33),
  the three replay-channel fixes (34 -- starved snapshot, harvester key names, tick axis), and the
  §37 `ResetControls` location and the §37.2 `applyCalls` A/B. Section 35's measurements (zero velocity,
  frozen `py`, `wingTime` 130, horizontal failing identically) are measurements and remain valid; only
  their explanation is reopened.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 41. Round 80: the write lands one frame late in replay, and the body never moves

### 41.1 The one-frame measurement

The plan write was bracketed: the controls are read back from the player's own fields immediately after
`ApplyPlan` returns (`WRITE`), and again at the tick entry (`ENTRY`), with a per-tick counter so
"cleared after the write" is distinguishable from "never written". Same route, same tick window, two
runs.

```
LIVE
ENTRY t=240 L=False J=False D=False Dash=False vx=0.00 vy=0.00  py=7958 writesLastTick=0
WRITE t=240 writes=1 L=True J=True D=False Dash=False py=7958 vy=0.00
ENTRY t=241 L=True  J=True  D=False Dash=False vx=0.00 vy=-6.48 py=7952 writesLastTick=1
WRITE t=241 writes=1 L=True J=False D=False Dash=False py=7952 vy=-6.48
ENTRY t=242 L=True  J=False D=False Dash=False vx=0.00 vy=-6.34 py=7945 writesLastTick=1

REPLAY
ENTRY t=240 L=False J=False D=False Dash=False vx=0.00 vy=0.00  py=7958 writesLastTick=0
WRITE t=240 writes=1 L=True J=True D=False Dash=False py=7958 vy=0.00
ENTRY t=241 L=False J=False D=False Dash=False vx=0.00 vy=0.00  py=7958 writesLastTick=1   <-- stale
WRITE t=241 writes=1 L=True J=False D=False Dash=False py=7958 vy=0.00
ENTRY t=242 L=True  J=False D=False Dash=False vx=0.00 vy=0.00  py=7958 writesLastTick=1   <-- one frame late
```

### 41.2 What the two runs prove

**Exactly one write per tick in both modes** (`writes=1` on every `WRITE` line), so the route is not
being double-advanced, and `ApplyPlan` is reached once per tick in the replay just as in the live run.

In the **live** run the write is visible at the very next entry and the body acts on it: `t=241`
shows `L=True J=True` with `vy=-6.48` and `py` already 7952 (it rose from 7958).

In the **replay** the same write at `t=240` is **not** visible at the `t=241` entry -- which still
reads the pre-write `L=False J=False` -- and only appears at the `t=242` entry, with
`vy=0.00` and `py` still exactly 7958. So the replay's controls reach the player's fields **one frame
later than the live run's**, and by then the frame that would have acted on them has already run.

**Combined with the invariant that `py` never moves in the replay, this is the sharpest statement of
the defect so far:** the write is correct and unique, and the player simply does not move in response
to it, on any of `L`, `J`, or `D`. Since §40 established the write lands after `ResetControls`, this is
not the reset discarding it either -- the field visibly *holds* `L=True` at the `t=242` entry while
`vx` stays exactly `0.00`.

### 41.3 What is now excluded

- Not a missing write, and not a double write (`writes=1` every tick, both modes).
- Not `ResetControls` discarding the write (§40, and the field holds `L=True` at the next entry).
- Not the route failing to reach the plan (§34.4: `replayFrame=0`, `plan.jump=True` at t=240).
- Not a state lock (§36: every refusal flag identical, including `CCed`, `frozen`, `mapFullscreen`).

What remains is that in replay mode **the native `Player.Update` does not act on the controls it can
see**. The two candidate mechanisms left are that the replay path drives the world through a different
update entry than `Main.DoUpdateInWorld`, or that `Player.Update` is invoked with a
`whoAmI`/`myPlayer` relationship that makes it skip the local-input branch, in which case the controls
would be read but the movement code would run on a body that the engine does not consider locally
controlled. Distinguishing those needs the `Player.Update` argument and `Main.myPlayer` logged inside
one replay frame, which is the next measurement.

### 41.4 Status

- **Diagnostics reverted**, tree builds clean, `git status` shows only the untracked `tmp/`.
- **Established:** one write per tick in both modes; in replay the written controls appear at the
  player's fields **one frame later** than in the live run, and the body never moves (`vx`/`vy` exactly
  `0.00`, `py` frozen at 7958) while holding `L=True`.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 42. Round 81: the replay body ignores controls the engine can see, and the difference is not identity

### 42.1 The identity measurement

Section 41 left two candidates: the replay drives the world through a different update entry, or
`Player.Update` runs on a body the engine does not consider locally controlled (so it skips the
`i == Main.myPlayer` branch that `ResetControls` and the local-input path live in). Both are settled by
logging the `Player.Update` prefix -- which is after `ResetControls` has had its chance and before the
movement code runs. Same route, same tick window, two runs:

```
LIVE
UPD ident t=239 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=False J=False vx=0.00 vy=0.00  py=7958
UPD ident t=240 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=False J=False vx=0.00 vy=0.00  py=7958
UPD ident t=241 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=True  J=True  vx=0.00 vy=-6.48 py=7952
UPD ident t=242 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=True  J=False vx=0.00 vy=-6.34 py=7945

REPLAY
UPD ident t=239 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=False J=False vx=0.00 vy=0.00  py=7958
UPD ident t=240 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=False J=False vx=0.00 vy=0.00  py=7958
UPD ident t=241 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=False J=False vx=0.00 vy=0.00  py=7958
UPD ident t=242 whoAmI=0 myPlayer=0 active=True dead=False isFilm=False netMode=0 L=True  J=False vx=0.00 vy=0.00  py=7958
```

**Every engine field is identical between the two runs** -- `whoAmI=0`, `myPlayer=0`, `active=True`,
`dead=False`, `isControlledByFilm=False`, `netMode=0` -- on every logged tick. So neither candidate
holds: the replay is not using a different update entry, and the body **is** the locally controlled
player by every field the engine checks. Yet at `t=241` the live body has `L=True J=True` and
`vy=-6.48` while the replay body has `L=False J=False` and `vx=vy=0.00`, and at `t=242` the replay
holds `L=True` with the velocity **still exactly zero**.

### 42.2 The defect, stated exactly

Combining 42.1 with 41.1, the replay node reads: the plan is reached and read every tick
(`replayFrame=0`), exactly one write per tick is issued, the written controls are visible on the
player's own fields, the local-player branch is the one that runs, and **the body does not move on any
channel**. Section 41's "one frame late" also turns out to be a *symptom* rather than the cause: at
`t=242` the write is no longer late at all (it was issued the previous tick and is visible now) and the
body still does not move. So there is no ordering left to repair -- the controls are readable by the
engine at the moment the movement code runs, and the movement code does not act on them.

That means the remaining difference is **not in the player object's input state at all**, but in the
world/update path the replay drives: the replay's `Player.Update` is executing, but whatever converts
`controlLeft`/`controlJump` into `velocity` is either not reached or is being undone inside the same
frame. The next measurement is accordingly not another identity probe but a position probe at the
**two ends of one `Player.Update`** in replay mode -- log `position`/`velocity` at the prefix and at
every `ret` of `Player.Update`. If the body moves at all inside the frame and is restored before the
next tick, the difference is a writer that runs after `Player.Update`; if it does not move at all
inside the frame, the movement code itself is not being reached and the probe's replay loop is driving
a different world update than the live one.

### 42.3 Honest position on the objective

The objective's acceptance口径 is the native per-tick replay, and it is still not a faithful replayer:
a route harvested from a 1-hit live run replays to **6 hits and a death**. The three probe/harvester
bugs fixed in §34 were real and necessary, and the route now reaches the plan, but this last gap is a
difference in how the *world* advances between live and replay that has resisted five rounds of
instrumentation (35, 36, 37, 41, 42). Until it is closed there is no way to accept **any** circuit on
the replay channel, and no native zero-hit fight exists for either loadout -- weak wing, policy off,
guard 0 measures **4 hits at 3000 ticks**.

### 42.4 Status

- **Diagnostics reverted**, tree builds clean, `git status` shows only the untracked `tmp/`.
- **Excluded:** a different update entry, and a non-local player identity -- all engine fields logged
  identical between live and replay on every tick.
- **Established:** the replay reads the route, writes once per tick, the write is visible on the
  player's fields, the local branch runs, and the body does not move on any channel.
- **Next measurement:** log `position`/`velocity` at the `Player.Update` prefix and at each `ret` of
  `Player.Update` in replay mode, to separate "moves inside the frame then restored" from "never moves
  inside the frame".
- **No native zero over a full fight on either loadout.**

## 43. Round 82: the replay never supplies `controlJump`, so it never becomes airborne

### 43.1 The two-ended frame probe

A paired observer was added to the end of `Player.Update` (`EndPlayerUpdate`) alongside the existing
entry observer, so one frame's position can be compared at both ends. Same route, same tick window, two
runs:

```
LIVE
FRAME_IN  t=239 L=False J=False px=640 py=7958 vx=0.00 vy=0.00  wingTime=130 wingsLogic=6 jump=0  onGroundZero=True
FRAME_OUT t=239 L=False J=False px=640 py=7958 vx=0.00 vy=0.00  wingTime=130 jump=0
FRAME_IN  t=240 L=False J=False px=640 py=7958 vx=0.00 vy=0.00  wingTime=130 wingsLogic=6 jump=0  onGroundZero=True
FRAME_OUT t=240 L=True  J=True  px=640 py=7952 vx=0.00 vy=-6.48 wingTime=130 jump=15
FRAME_IN  t=241 L=True  J=True  px=640 py=7952 vx=0.00 vy=-6.48 wingTime=130 wingsLogic=6 jump=15 onGroundZero=False
FRAME_OUT t=241 L=True  J=False px=640 py=7945 vx=0.00 vy=-6.34 wingTime=130 jump=0
FRAME_IN  t=242 L=True  J=False px=640 py=7945 vx=0.00 vy=-6.34 wingTime=130 wingsLogic=6 jump=0  onGroundZero=False

REPLAY
FRAME_IN  t=239 L=False J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 wingsLogic=6 jump=0 onGroundZero=True
FRAME_OUT t=239 L=False J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 jump=0
FRAME_IN  t=240 L=False J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 wingsLogic=6 jump=0 onGroundZero=True
FRAME_OUT t=240 L=False J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 jump=0
FRAME_IN  t=241 L=False J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 wingsLogic=6 jump=0 onGroundZero=True
FRAME_OUT t=241 L=True  J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 jump=0
FRAME_IN  t=242 L=True  J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 wingsLogic=6 jump=0 onGroundZero=True
FRAME_OUT t=242 L=True  J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 jump=0
FRAME_IN  t=243 L=True  J=False px=640 py=7958 vx=0.00 vy=0.00 wingTime=130 wingsLogic=6 jump=0 onGroundZero=True
```

### 43.2 What it proves

1. **The body never moves inside the frame.** In the replay `py` is `7958` at the entry **and** at the
   end of `Player.Update`, on every logged tick, and `vx`/`vy` stay exactly `0.00`. This is not a
   post-frame restore: the movement code runs and produces nothing. `onGroundZero=True` and
   `jump=0` throughout.
2. **The frame is processed normally.** `Player.Update` starts and returns every tick, and the write
   from the previous tick is visible at the entry (`FRAME_IN t=242 L=True`), so §42's "the engine can
   see the controls" is confirmed a second time, now from the other end of the frame.
3. **The live run moves on exactly the frame that first carries `J=True`.** In live, `t=240` enters
   grounded and returns **airborne** (`vy=-6.48`, `py` 7958->7952, `jump=15`), and the next entry sees
   `onGroundZero=False`.
4. **The replay never carries `J=True` into a frame.** At `t=240` the replay frame returns with
   `J=False`; at `t=241` the frame returns with `L=True J=False`. `controlJump` is **never** set before
   a frame's update begins, so no frame ever performs a jump.

### 43.3 Root cause and why every earlier symptom follows

`WingMovement` needs `wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 && velocity.Y != 0`;
`JumpMovement` needs `controlJump`. With `controlJump` never reaching a frame, the player can never
leave the ground, so:

- `velocity` stays exactly `(0,0)` and `py` is frozen -- the observed invariant since §35;
- `wingTime` stays pinned at its full `130`, because flight is never spent (§35);
- `onGroundZero` stays `True` forever;
- **the horizontal channel fails too, even though `L=True` is plainly set.** This is the same
  acceleration mechanic the owner described: horizontal speed only builds while a movement input is
  held across frames, and with the body pinned to the ground and no jump the frame-by-frame state never
  produces motion. It also means `L=True` at `FRAME_OUT` is **not** being cleared -- it persists into
  the next entry -- so the earlier "one frame late" reading (41.1) was an artefact of comparing the
  entry observer against a write that lands at the *end* of the same frame.

So the single defect is: **in replay mode the jump command is never present at the start of a frame.**
The route *does* ask for it (`plan.jump=True` at t=240, §34.4) and the write *is* issued, but it does not
survive to the point where the movement code consults it, while the horizontal write in the same
`ApplyPlan` call does survive. That asymmetry -- one control from a single write surviving and another
not -- is the concrete next thing to measure, and it points at the plan/route plumbing for the jump
channel rather than at `Player.Update` at all.

### 43.4 Status

- **Diagnostics reverted**, tree builds clean, `git status` shows only the untracked `tmp/`.
- **Established:** the replay body does not move inside `Player.Update`; `controlJump` is never set
  before a frame's update begins; the live run goes airborne on the first frame that carries `J=True`.
- **Refined:** §41's "one frame late" applies to `controlLeft` only and is an artefact of comparing the
  entry observer against a write issued at the end of the same frame; `controlLeft` does persist.
- **Next:** measure why `controlJump` from the same `ApplyPlan` write does not persist while
  `controlLeft` does -- inspect the jump channel of the route/plan plumbing.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 44. Round 83: the route does carry the jump, so the gate or the frame boundary drops it

### 44.1 The route is not the problem

Section 43 concluded that no frame ever begins with `controlJump` set, and proposed the jump channel of
the plan/route plumbing as the suspect. Reading the route directly removes half of that: the harvested
route **does** carry the jump, at exactly the tick the fight needs it.

```
236,0,0,0,0
237,0,0,0,0
238,0,0,0,0
239,0,0,0,0
240,0,0,0,0
241,-1,1,0,0      <-- direction -1, jump 1
242,-1,0,0,0
243,-1,0,0,0
```

Columns are direction, jump, up, down, dash. So `Runtime`'s replay block (`Runtime.cs:421-462`) reads
`replayDirection=-1, replayJump=true` at tick 241 and does set `plan.Jump = true` and
`plan.JumpAction = JumpAction.Hold` (`:426`, `:460-462`). The route read, the plan assignment and the
`Hold` action are all correct; the jump is lost **between the plan and the player's field**.

### 44.2 The only thing between them is the resolver

`TerrariaFacade.ApplyPlan` writes the channel through a gate rather than directly
(`TerrariaFacade.cs:3281-3284`):

```
var jumpState = _combatSnapshot.Player.Jump;
jumpState.ReleaseReady = _releaseJump(player);
SetControl(player, "controlJump", MovementActionGate.ResolveJump(plan.Jump, plan.JumpAction, in jumpState,
    _combatSnapshot.Player.OnGround, _combatSnapshot.Mobility.Grappling));
```

and that gate is a real conjunction (`MovementActionGate.cs:8-20`):

```
ShouldHoldJump(requested, grounded, releaseReady, grappling)
    => requested && (!grounded || releaseReady || grappling);
ResolveJump(requested, action, in state, grounded, grappling)
    => JumpMotion.ResolveControl(requested, action, in state, grounded, grappling);
```

So with `requested=true` the write still produces `false` whenever the resolver's snapshot says
otherwise. §35 recorded an ablation that bypassed this gate and found it "bit-identical", but that was
measured on the *live* path where the jump is supplied every tick anyway; the replay is the case where
the gate's inputs actually differ, so that ablation does not clear the resolver here.

### 44.3 The frame-boundary constraint, stated for the design

Independent of the gate, the measurement pins down a hard requirement that any fix must satisfy.
`controlJump` is never `true` at a frame entry in the replay (`FRAME_IN` at t=241, 242, 243 after the
route's jump at t=241), while the live run acts on `J=True` at the frame that carries it and returns
airborne. Since `WingMovement` requires `controlJump` and `velocity.Y != 0`, and `JumpMovement`
requires `controlJump`, the write **must be present before the movement code reads it in the frame that
is supposed to jump**. Reading the route at tick N and writing a value that a later point in the same
tick consumes does not achieve that; the value has to be in place at the frame boundary.

### 44.4 The next measurement, exactly

The plugin has no logger of its own, so the instrumentation goes in the probe's observer of the control
writes. Log, for ticks 239-246 and in both live and replay: `plan.Jump`, `plan.JumpAction`,
`jumpState.ReleaseReady`, `jumpState`'s own fields, `_combatSnapshot.Player.OnGround`,
`_combatSnapshot.Mobility.Grappling`, and the **return value** of `ResolveJump` -- the resolver's
verdict, not just its effect. If the verdict is `true` while the entry observer still sees `False`, the
remaining loss is the `_pendingInput` snapshot taken in `Runtime.Tick`'s `finally` (which stages the
frame for `ApplyPendingInput` to re-apply), and that snapshot is the next thing to log; if the verdict
is already `false`, the resolver's inputs are wrong and the fix is in what the replay feeds the
snapshot, not in the staging.

### 44.5 Status

- **Tree clean** apart from the untracked `tmp/`; no diagnostic code is currently in the tree.
- **Established:** the harvested route carries `jump=1` at tick 241 and `Runtime` sets `plan.Jump=true`
  with `JumpAction.Hold`; the jump is lost between the plan and the player's field, i.e. at the
  `ResolveJump` gate or in the `_pendingInput` staging.
- **Established:** no replay frame ever begins with `controlJump` set, so the body can never leave the
  ground, and the horizontal channel fails as a consequence (acceleration only builds across frames of
  held input).
- **Next:** log the resolver's verdict and inputs, then the `_pendingInput` snapshot, for ticks 239-246
  in both modes.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 45. Round 84: a minimal jump-only route rules out the resolver and the route alignment

### 45.1 The two measurements

**Post-write state.** The probe's `ObserveApplyPlanAfter` observer is injected at every `ApplyPlan`
return, so it reads the player *after* all the plan's writes and therefore exposes the resolver's
verdict. Replaying the harvested route:

```
POSTWRITE t=240 guc=241 J=True  L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00
POSTWRITE t=241 guc=242 J=False L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00
POSTWRITE t=242 guc=243 J=False L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00
```

`guc` is `Game.GameUpdateCount` and is consistently `ticks + 1`, so the replay's `CurrentGameTick()`
and the probe's counter are offset by one throughout.

**The minimal route.** The harvested route's jump is a single tick (`241,-1,1,0,0` followed by
`-1,0` rows), and the replay showed `J=True` only at `t=240`. That admits exactly two explanations -- the
resolver dropping the jump, or the route being consumed at the wrong rate. A synthetic route whose only
content is "hold jump from tick 241 to 250" decides between them:

```
POSTWRITE t=240 guc=241 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=241 guc=242 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=242 guc=243 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=243 guc=244 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=244 guc=245 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=245 guc=246 J=True L=True ... onGroundZero=True py=7958 vy=0.00
POSTWRITE t=246 guc=247 J=True L=True ... onGroundZero=True py=7958 vy=0.00
```

### 45.2 What this rules out, and what it leaves

Both candidate explanations from §44 are now dead:

- **Not the resolver.** With a sustained jump request the post-write `controlJump` is `True` on every
  single tick, so `MovementActionGate.ResolveJump` is returning `true` and the gate is not dropping
  anything.
- **Not the route alignment.** A sustained request is delivered as a sustained request, so the route is
  not being consumed one tick per N frames or otherwise rate-mismatched. (§44's single-tick jump reading
  was an artefact of the harvested route itself containing only one jump tick, not of the replay.)

**And the body still does not move.** Through all seven logged ticks: `py=7958` unchanged **and**
`vy=0.00` unchanged, with `onGroundZero=True` -- while `controlJump` is genuinely `True` on the
player's own field at the end of `ApplyPlan`. Combined with §43 (the body does not move inside
`Player.Update`, entry and exit positions identical) this is conclusive: **in replay mode the native
movement code in `Player.Update` is not being reached at all.** The controls are correct on the object
the engine sees; nothing consumes them.

### 45.3 The consequence for every earlier "fix"

This explains why the last several rounds of work moved nothing. §43's plan/route hypothesis and §44's
resolver hypothesis were both aimed at the *input* side, and the input side is now measured to be
correct end to end. The defect is on the *update* side, in whatever differs between the live path and
the replay path about how the player's own per-frame simulation is driven. That is a single, bounded
question -- and it is where the next measurement goes: log `velocity`/`position` at the entry **and at
every natural `ret` inside the movement region** of `Player.Update`, plus whether the enclosing
`Main.DoUpdateInWorld`-equivalent runs at all in replay, so "movement code skipped" is separated from
"movement code runs and is then reverted".

### 45.4 Status

- **Diagnostics reverted**, tree builds clean, `git status` shows only the untracked `tmp/`.
- **Established:** with a sustained route jump the post-write `controlJump` is `True` every tick, so the
  resolver is not gating and the route is not rate-mismatched. The body is nonetheless frozen
  (`py`/`vy` unchanged, `onGroundZero=True`), so the native movement code is not reached in replay mode.
- **Correction:** §44's "the resolver or the staging drops the jump" is withdrawn; both the resolver and
  the plan/route plumbing deliver a correct, sustained jump.
- **Next:** instrument the movement region of `Player.Update` in replay mode to separate "skipped" from
  "run then reverted".
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 46. Round 85: the gap is between `ApplyPlan` and the movement entry -- the frame is planned from the previous tick's controls

### 46.1 The three measured points, in order

Three observers now bracket the control's life inside one tick. Reading them together localises the loss
to a single interval.

```
(a) right after ApplyPlan (observer injected at every ApplyPlan return)
POSTWRITE t=240 guc=241 J=True  L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00
POSTWRITE t=241 guc=242 J=True  L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00
POSTWRITE t=245 guc=246 J=True  L=True D=False Dash=False jumpField=0 onGroundZero=True py=7958 vy=0.00

(b) at the movement region (Player.Update entry prefix)
GATE t=240 guc=241 J=False L=False frozen=False webbed=False stoned=False CCed=False pulley=False
     grap=False mount=False itemAnim=0 isFilm=False vy=0.00 py=7958
GATE t=241 guc=242 J=False L=False ... etc, identical on every tick
```

- **(a) is a route with a sustained jump** ("hold jump from tick 241 to 250"), so the plan genuinely asks
  for the jump and `MovementActionGate.ResolveJump` genuinely returns `true` -- the post-write
  `controlJump` is `True` on every tick. This finally closes §44's resolver question: the gate is not
  dropping anything.
- **(b) is the movement entry of the same run.** Every single native flag that makes
  `HorizontalMovement` or the input conversion bail out without moving the body is **false**
  (`frozen`, `webbed`, `stoned`, `CCed`, `pulley`, grap, mount, `itemAnim`, `isFilm`), so no state gate
  explains the freeze either -- and yet `J` and `L` are `False` here.

### 46.2 The interval that loses the value

`(a)` is `True` and `(b)` is `False` **in the same tick, on the same body**. So the controls are correct
when `ApplyPlan` writes them and are zero by the time the movement code reads them. The plugin's
`AwayFromBossAxis`-style state gates, the resolver, the route and the plan are all excluded, because
each was measured on the correct side of this line. The one mechanism that deliberately runs in exactly
that interval is the plugin's staged restore: `Runtime.Tick`'s `finally` snapshots the frame via
`_game.CapturePendingInput(player)` (setting `_pendingInput`), and `Runtime.ApplyPendingInput` -- injected
into `Player.Update` immediately after the native input copy -- writes those captured controls back over
the player's fields. In replay mode that snapshot is taken and re-applied every frame, and a stale or
empty snapshot will overwrite the resolve-written `True` with `False` a few instructions later, which is
precisely the observed `(a) True -> (b) False`.

This also explains why the whole replay channel has been unusable while every component tested
in isolation looked correct, and why **the live path is unaffected**: the live takeover reaches the same
`ApplyPlan` but the staged restore is staged from a frame that already carries the live controls.

### 46.3 Why this round closed without instrumenting that interval

The natural observer -- a probe hook injected right after the production `Runtime.ApplyPendingInput`
call -- collides with the runner's own IL validation, which asserts the exact instruction sequence
around that call site and the `Player.Update` hook set (`prepare-game-probe.ps1:598`):

```
Motion control replay must immediately follow the production input replay.
```

Two placements were tried and both are rejected, because the validator requires `MotionAfterInput` to be
`ApplyPendingInput`'s immediate next `ldarg.0`/`call` pair and then enumerates the remaining hooks
strictly. Rather than weaken a validator that is protecting the probe's contract, this round records the
localisation and parks the interval instrumentation. **The next attempt should log from
`RouteReplay`/`Runtime`'s own side (e.g. `CapturePendingInput`'s snapshot contents) rather than adding
instructions to `Player.Update`**, which avoids that contract entirely.

### 46.4 Status

- **Tree clean** apart from the untracked `tmp/`; every diagnostic from this round is reverted and the
  solution builds clean.
- **Established:** immediately after `ApplyPlan` the written `controlJump`/`controlLeft` are `True`
  (resolver verified to return `true` under a sustained route jump); at the movement entry of the *same
  tick* they are `False`; all native movement-suppressing flags are `False`. The loss is therefore inside
  the interval between the write and the movement entry, where the plugin's staged
  `CapturePendingInput`/`ApplyPendingInput` restore runs.
- **Excluded:** the resolver/gate (§44 hypothesis, now measured `true`), the route alignment and plan
  plumbing (§43/§45), and every native movement-suppression flag.
- **Next:** read `_capturedControls` at `CapturePendingInput`/`ApplyPendingInput` from the plugin's own
  side, without adding instructions to `Player.Update`.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 47. Round 86: the plugin's whole control pipeline is correct -- the controls are cleared between the restore and the next tick's entry

### 47.1 The plugin-side trace

The plugin has no logger, so a trace was added on its own side, writing to the path in `CHAITE_DIAG_FILE`
(pure `System.IO`, no new instructions in `Player.Update`, so the probe's IL validator is untouched). Four
points per tick: the entry state read before anything is written (`D_ENTRY`), the resolved plan and its
post-write fields (`A_APPLY`), the snapshot taken for staging (`B_CAPTURE`), and the staged restore's
written and post-write fields (`C_RESTORE`). Replaying the jump-only route:

```
D_ENTRY   t=241 inJ=False inL=False
A_APPLY   t=241 J=True JA=Hold L=-1 D=False Dash=False postJ=True postL=True
B_CAPTURE t=241 J=True L=True D=False Dash=False liveJ=True liveL=True
C_RESTORE t=241 wroteJ=True wroteL=True postJ=True postL=True
D_ENTRY   t=242 inJ=False inL=False
A_APPLY   t=242 J=True JA=Hold L=-1 D=False Dash=False postJ=True postL=True
B_CAPTURE t=242 J=True L=True D=False Dash=False liveJ=True liveL=True
C_RESTORE t=242 wroteJ=True wroteL=True postJ=True postL=True
D_ENTRY   t=243 inJ=False inL=False
... identical on every tick through 246
```

### 47.2 What this establishes

**The plugin's control path is correct end to end, and §44/§46's staging hypothesis is wrong.** On every
tick the plan resolves the jump (`J=True`, `JumpAction=Hold`), the write lands (`postJ=True`,
`postL=True`), the staging snapshot captures exactly that (`J=True`, `L=True`), and the restore writes it
back and reads it back as `True`. There is no stale or empty snapshot: the two-stage
`CapturePendingInput`/`ApplyPendingInput` round-trip is faithful.

**The loss is after the restore and before the next tick's entry.** `C_RESTORE` ends tick *N* with
`postJ=True postL=True`, and the very next line of the trace is `D_ENTRY` for tick *N+1* reading
`inJ=False inL=False`. Nothing in the plugin runs between those two points, so **the clear happens inside
the engine, between the end of one `Player.Update` and the entry of the next** -- which is precisely where
`ResetControls` sits (`Player.Update` calls it on entry for the local player, §37). And the same trace run
in the live takeover reaches the movement region carrying `controlJump=True`, so in live the value
survives that same window.

That is the whole defect, stated exactly: **in replay mode the controls written during tick N do not
survive into tick N+1, while in live mode they do.** Every earlier symptom follows from it -- the body
never becomes airborne, `velocity` stays exactly `(0,0)`, `py` is frozen, `wingTime` stays at 130, and the
horizontal channel fails too because acceleration only accumulates across frames of held input. §41's
"one frame late" and §43's "`controlJump` never reaches a frame" were both correct observations of this
same fact from different observers.

### 47.3 Where the live path differs

Since the plugin's writes and staging are provably identical in structure, the remaining difference is in
what the live mode does that replay does not: a mechanism that re-establishes the controls after the
engine's entry-time clear. In the live probe run, `Player.Update` reads its own inputs from the native
input layer before the entry clear, and the probe's live takeover is measured to work; in replay the
route's controls are supplied only through the plugin. The next measurement is therefore the **live**
run's same four-point trace: if `D_ENTRY` is `True` in live where it is `False` in replay, the live path
has a re-supply point the replay lacks, and that point is the thing the replay must be routed through.

### 47.4 Status

- **Tree clean** apart from the untracked `tmp/`; all instrumentation reverted, solution builds clean.
- **Established (measured, whole pipeline):** in replay the plan resolves, writes, snapshots and restores
  `controlJump`/`controlLeft` as `True` inside every tick, and the value is `False` again at the next
  tick's entry. The clear is in the engine between ticks, not in the plugin.
- **Correction:** §44's and §46's "the staged snapshot drops the jump" is withdrawn -- the snapshot is
  faithful (`B_CAPTURE J=True`, `C_RESTORE wroteJ=True postJ=True` every tick).
- **Next:** the same four-point trace on a **live** run, to find the live re-supply point that replay
  lacks.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 48. Round 87: live and replay have identical control state -- the divergence is inside the tick

### 48.1 The live trace

§47 predicted that live would show `D_ENTRY J=True` where replay shows `False`, i.e. that live has a
re-supply point the replay lacks. **That prediction is wrong.** The same four-point trace on a **live**
run (formula route, `fishron-fairy-wing`) gives:

```
D_ENTRY   t=241 myPlayer=0 film=False J=False L=False
C_RESTORE t=241 myPlayer=0 film=False J=True  L=True
D_ENTRY   t=242 myPlayer=0 film=False J=False L=False
C_RESTORE t=242 myPlayer=0 film=False J=False L=True
D_ENTRY   t=243 myPlayer=0 film=False J=False L=False
C_RESTORE t=243 myPlayer=0 film=False J=False L=True
D_ENTRY   t=244 myPlayer=0 film=False J=False L=False
C_RESTORE t=244 myPlayer=0 film=False J=False L=True
```

and the replay trace from §47:

```
D_ENTRY   t=241 inJ=False inL=False
C_RESTORE t=241 wroteJ=True wroteL=True postJ=True postL=True
D_ENTRY   t=242 inJ=False inL=False
C_RESTORE t=242 wroteJ=True wroteL=True postJ=True postL=True
```

**The two modes are the same on every logged field.** Entry is `J=False L=False` in both; the restore
leaves `J=True L=True` in both; `myPlayer=0` and `isControlledByFilm=False` in both. §47's "the clear is
between ticks and live survives it" is therefore also withdrawn: **live is cleared between ticks exactly
the same way.** The controls are re-established *inside* the tick by the plugin's staged restore, in both
modes, and that is by design.

### 48.2 The defect, now stated as a single sentence

Live and replay present the engine with the **same control values at the same points in the tick**, and
only live produces movement. So nothing about the controls, the resolver, the plan, the route, the
staging, the identity, the film flag, or the entry/exit clear distinguishes them. The divergence is
**after the restore and inside the same tick**: live's body acts on the restored controls and replay's
does not, while both have `controlJump=True` on the player's fields at that moment (measured from the
plugin's side, `C_RESTORE postJ=True`, and independently from the probe's side, `POSTWRITE J=True`).

That collapses the problem to one question, and it is not about input at all: **why does the native
movement code not act on a control value that is set on the player it is reading?** Two answers remain,
and they are cheaply separable:

1. the movement code is not invoked in replay (a different or reduced update path), or
2. the movement code is invoked but reads its input from somewhere other than the player's control
   fields (a cached input structure that live refreshes and replay does not).

Answer 2 is the more likely one and matches the owner's mechanic note that horizontal speed must be built
by *sustained* input: if the movement code consults a cached/edge-tracked input rather than the field, a
single-frame field write would never build speed, and the body would sit still exactly as observed while
`velocity` stays exactly `(0,0)`.

### 48.3 The measurement that separates them

`JumpMovement` and `WingMovement` already carry probe observers (`MotionBeforeJump`/`MotionAfterJump`,
`FlightBeforeWing`/`FlightAfterWing`) that are strict no-ops outside motion cases. Adding a counter to
those two hooks -- **no new instructions in `Player.Update`, so the IL validator is untouched** -- records
whether the movement methods are reached in a replay frame. If `JumpMovement` is reached with
`controlJump=True` and `velocity` still does not change, answer 2 is confirmed and the fix is to write the
input where the movement code actually reads it; if it is never reached, answer 1 is confirmed and the
replay's world-update path is the target.

### 48.4 Status

- **Tree clean** apart from the untracked `tmp/`; all instrumentation reverted, solution builds clean.
- **Established (measured on both modes):** live and replay are identical on entry controls, restored
  controls, `myPlayer` and `isControlledByFilm`; the entry clear happens in both.
- **Corrections this round:** §47's "live survives the between-tick clear" is withdrawn -- live is cleared
  identically and re-established inside the tick by the staged restore, in both modes. §47's prediction
  that live would show `D_ENTRY J=True` is refuted.
- **Next:** count `JumpMovement`/`WingMovement` invocations in a replay frame via their existing probe
  observers, to separate "movement code not reached" from "movement code reads a cached input".
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 49. Round 88: the movement code IS reached -- with `controlJump=False` while the plugin had just written it True

### 49.1 The two measurements

**(1) The movement methods are reached, and `JumpMovement` is the decisive one.** Adding a trace inside
the already-installed `MotionBeforeJump`/`FlightBeforeWing` observers (no new instructions in
`Player.Update`, so the IL validator is untouched), the same tick window, both modes:

```
REPLAY
JUMP_ENTER t=239 guc=240 J=False jump=0  vy=0.00  py=7958
JUMP_ENTER t=240 guc=241 J=False jump=0  vy=0.00  py=7958
JUMP_ENTER t=241 guc=242 J=False jump=0  vy=0.00  py=7958
JUMP_ENTER t=242 guc=243 J=False jump=0  vy=0.00  py=7958
... identical through t=246, and no WING_ENTER line at all

LIVE
JUMP_ENTER t=239 guc=240 J=False jump=0  vy=0.00  py=7958
JUMP_ENTER t=240 guc=241 J=True  jump=0  vy=0.00  py=7958   <-- reaches the movement code with the jump held
JUMP_ENTER t=241 guc=242 J=False jump=15 vy=-6.48 py=7952   <-- airborne now
JUMP_ENTER t=242 guc=243 J=False jump=0  vy=-6.34 py=7945
```

So §48's "different update path" answer is excluded: **`JumpMovement` runs every tick in replay too.**
What differs is the value it sees. Live hands it `controlJump=True` and the body jumps (`jump=15`,
`vy=-6.48`); replay hands it `controlJump=False` and the body never leaves the ground.

**(2) The order inside one live tick, and that the staged restore is not the cause.** Observing the
plugin's own `Runtime.Tick` call site (`TICK_OUT`) gives the order:

```
TICK_OUT   t=240 J=True  L=True  jump=0     <-- Runtime.Tick / ApplyPlan has written the controls
JUMP_ENTER t=240 guc=241 J=True  jump=0     <-- movement code still sees True here
TICK_OUT   t=241 J=False L=True  jump=15
JUMP_ENTER t=241 guc=242 J=False jump=15
```

Note the two observers use different counters: `TICK_OUT t=N` and `JUMP_ENTER ... guc=N+1` are the
**same frame**. So in live, the resolve-written `J=True` is still present when `JumpMovement` runs, and
the jump fires. §48's conclusion that the staged restore overwrites the fresh write was this round's
working hypothesis, so it was tested directly by disabling the restore body in
`Runtime.ApplyPendingInput` and re-running the replay:

```
REPLAY with the staged restore disabled
   guc=241 pos={'x': 640, 'y': 7958} vel={'x': 0, 'y': 0} ctlJump=False planJump=True
LIVE (formula route) for comparison
   guc=241 pos={'x': 640, 'y': 7951.52344} vel={'x': 0, 'y': -6.476667} ctlJump=True planJump=True
```

**Disabling the restore changes nothing** -- replay still measures `controlJump=False` at the movement
code, the plan still asks for `planJump=True`, and `pos`/`vel` are still exactly frozen. The restore is
not the overwriter, and that hypothesis is withdrawn.

### 49.2 Where the defect now sits

Reading (1) and (2) together, in replay mode the plan **is** resolved (`planJump=True`), the plugin's
`ApplyPlan` **does** write `controlJump` (`§47 C_RESTORE postJ=True`), and `JumpMovement` **is** reached
-- but it is reached with `controlJump=False`. So in replay the write is undone **between `ApplyPlan`
returning and the movement code reading it, inside the same `Player.Update`**, and the only actors in
that window are the engine's own control handling and the plugin's `ValidatePendingMobility` hook. The
engine's entry-time `ResetControls` runs *before* `ApplyPlan` (which is why `D_ENTRY` is False in both
modes), so what remains is a clear that happens after the plan's write and before `JumpMovement`.
Locating it is a bounded, one-frame question and is the next measurement: log the control value at the
existing `MotionAfterInput` site (immediately after the production `ApplyPendingInput` and therefore
after the plan write) in replay and compare it with `JUMP_ENTER` of the same frame. If it is already
`False` there, the clear is between `ApplyPlan` and that point.

### 49.3 Status

- **Tree clean** apart from the untracked `tmp/`; all diagnostics and the restore experiment reverted,
  solution builds clean.
- **Established:** `JumpMovement` is reached every tick in replay (with `controlJump=False`) and in live
  (with `controlJump=True` on the frame that jumps); the plan asks for the jump in replay
  (`planJump=True`); disabling the staged restore does not change replay at all.
- **Corrected:** §48's candidate "the movement code is not invoked in replay" is refuted, and this
  round's own "the staged restore overwrites the fresh write" hypothesis is refuted by the restore-off
  experiment.
- **Next:** read the control value at `MotionAfterInput` (after the plan write, same frame) in replay, to
  bound the clear between `ApplyPlan` and the movement code.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 50. Round 89: the clear sits between `MotionAfterInput` and `JumpMovement`, inside one `Player.Update`

### 50.1 The measurement

Reading the player's control fields at the probe's existing `MotionAfterInput` hook -- which sits
immediately after the production `Runtime.ApplyPendingInput` inside `Player.Update`, and therefore
**after** the plan has written the controls and **before** any movement method runs -- in the same run
where `JumpMovement` was measured to see `False`:

```
REPLAY
AFTERINPUT t=239 guc=240 J=False L=False jump=0 vy=0.00
AFTERINPUT t=240 guc=241 J=True  L=True  jump=0 vy=0.00
AFTERINPUT t=241 guc=242 J=True  L=True  jump=0 vy=0.00
AFTERINPUT t=242 guc=243 J=True  L=True  jump=0 vy=0.00
... J=True L=True on every tick through t=246
```

against §49's `JUMP_ENTER` for the same run:

```
JUMP_ENTER t=240 guc=241 J=False jump=0 vy=0.00 py=7958
JUMP_ENTER t=241 guc=242 J=False jump=0 vy=0.00 py=7958
```

**Same frames, same counters: `J=True` at `MotionAfterInput` and `J=False` at `MotionBeforeJump`.** So
within a single `Player.Update`, the control is correct after the plan write and gone by the time
`JumpMovement` is entered. This finally puts the clear unambiguously *inside* `Player.Update`, between
two probe hooks that are a few instructions apart, and it excludes everything upstream of that point: the
resolver (writes `True`), the plan and route (`planJump=True`), the staged
`CapturePendingInput`/`ApplyPendingInput` round-trip (§47: faithful; §49: disabling it changes nothing),
the engine's entry-time `ResetControls` (it runs *before* `ApplyPlan`, which is why both modes read
`False` at entry), and all the movement-suppressing native flags (§46: all `False`).

### 50.2 Why this round stopped here

The obvious next probe -- a hook adjacent to `MotionAfterInput` to narrow the remaining few instructions
-- was attempted and **failed on my own IL edit**: the insertion placed the new observer *before*
`MotionAfterInput` in the instruction stream, which the runner's validator rejects:

```
The property 'Name' cannot be found on this object.
```

(That is `prepare-game-probe.ps1:598` dereferencing `$inputReplay[0].Next.Next.Operand.Name` under
`Set-StrictMode -Version Latest` when the instruction at that slot is my `ldarg.0` pair rather than
`MotionAfterInput`.) Since three of this round's edits were spent on patch placement rather than on the
fight, the correct move is to stop adding instructions to `Player.Update` and record where the boundary
now is. The instrumentation is fully reverted, the tree builds clean, and no measurement from the failed
run is used.

### 50.3 What the next measurement must be

The clear is between "just after the production input replay" and "`JumpMovement` entry", inside one
`Player.Update`, with no plugin hook in between other than `ValidatePendingMobility` (`Runtime.cs:607`,
which only validates optional edges and calls `_game.ValidatePendingMobility`). Two candidate
mechanisms remain, and the next probe should distinguish them **without adding instructions to
`Player.Update`**:

1. The engine's own input handling runs a second clear later in the method (the patcher's own comment at
   `GameProbePatcher.cs:40-41` notes "Branches skipping native input retain the pre-frame test
   controls", which implies at least one native path that resets controls mid-update).
2. `ValidatePendingMobility` -> `TerrariaFacade.ValidatePendingMobility` (`TerrariaFacade.cs:3746`) is
   rewriting or clearing controls while validating optional mobility.

Because the plugin has a working file trace (§47, `CHAITE_DIAG_FILE`), the clean way is to log inside
`TerrariaFacade.ValidatePendingMobility` -- entirely on the plugin's own side, with no IL changes at all --
and compare its post-state with `JUMP_ENTER` of the same frame.

### 50.4 Status

- **Tree clean** apart from the untracked `tmp/`; all instrumentation reverted, solution builds clean.
- **Established:** in replay, `controlJump`/`controlLeft` are `True` at `MotionAfterInput` (after the plan
  write) and `False` at `MotionBeforeJump` in the **same frame**; the clear is therefore inside
  `Player.Update` between those two hooks.
- **Excluded by this measurement:** everything upstream -- resolver, plan/route, staged round-trip,
  entry-time `ResetControls`, and every movement-suppressing flag.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**.
- The dense route replays to **6 hits and a death** against the recorded 1 hit.
- **No native zero over a full fight on either loadout.**

## 51. Round 90: the harvest writer dropped the dash channel -- the replay body now moves

### 51.1 The bug that froze every replay

The route writer built each row with **five** format placeholders for **six** controls
(`tools/harvest-native-route.py:205`):

```
lines.append("{},{},{},{},{}".format(tick, *controls))
```

`controls` is `(direction, jump, up, down, dash)`
(`harvest-native-route.py:78-82`), so `.format(tick, *controls)` supplied six arguments to five slots and
**the trailing `dash` value was silently discarded from every harvested route**. The documented format is
`tick,direction,jump,up,down,dash` (`:17`), and the dash count was even computed and printed by the
harvester (`controls ... dash=11`) -- while never being written. Measured: the old
`tmp/route-tickaxis.txt` has **5 columns and no dash field at all**.

That is why no replay ever moved. In the plugin, `plan.Dash` feeds
`_validatePendingDash` (`TerrariaFacade.cs:3744-3756`), and when a dash candidate cannot be certified
`ValidatePendingMobility` rejects and calls `ResolveRejectedPendingMobility` -> `NeutralizePendingInput`
(`TerrariaFacade.cs:3939-3949`), which **zeroes every control and every captured control**:

```
E_VALIDATE_IN  t=241 J=True  L=True
F_VALIDATE_OUT t=241 J=False L=False
```

That single clear, inside the interval §50 bounded, is what §49 measured as
`MotionAfterInput J=True` -> `MotionBeforeJump J=False`, and it is why the body sat at `(640, 7958)` with
`velocity` exactly `(0,0)` in every replay while the live run flew.

### 51.2 The fix and its measured effect

With the sixth placeholder restored:

```
controls        : left=271 right=636 jump=432 dash=11
route file      : tmp\route-fixed.txt   (6 columns, 11 nonzero dash rows)
```

Replaying that route (1200 ticks, weak wing):

```
ticks     : 1200
HITS      : 6
death     : True
player x range 640 .. 2666.11     (was frozen at 640)
player y range 7841.96 .. 7958    (was frozen at 7958)
```

**The replay body moves for the first time.** The frozen-body defect -- open since §35 and investigated
through §36, §37, §41, §43, §45, §47, §49 and §50 -- is explained and removed. The validator no longer
neutralizes after the first frame (`E_VALIDATE_IN t=242 J=False L=True` -> `F_VALIDATE_OUT t=242 J=False
L=True`, controls preserved), whereas with the old 5-column route it neutralized on every tick.

### 51.3 What remains

The replay is now *a* moving replay, not yet the *recorded* one: the source live run recorded **1 hit**
and this replay of its own route gives **6 hits and a death**. So the acceptance channel still diverges
from the recorded fight. The next step is the one §34 planned and never completed: re-harvest from a
fresh dense live run **with the fixed writer** and compare the replay against the live run tick by tick
(positions, controls, dash episodes) to find what differs now that the body actually moves. The dash
channel is the prime suspect, because the recorded run had 11 dash ticks and the dash interacts with the
same validator that was previously zeroing everything.

### 51.4 Status

- **Fixed and kept:** `tools/harvest-native-route.py:205` (sixth placeholder). All plugin diagnostics from
  this round are reverted; solution builds clean; `git status` shows only the harvest fix and untracked
  `tmp/`.
- **Established:** the harvested route format was missing the dash channel, which armed the plugin's
  pending-dash validation in replay, which in turn neutralized every control and captured control on
  every tick. With the dash channel restored the replay body moves.
- **Not yet achieved:** the replay does not yet reproduce the recorded fight (1 hit live vs 6 hits and a
  death replayed). Weak wing, policy off, guard 0 still measures **4 hits at 3000 ticks**.
- **Next:** re-harvest from a fresh dense live run with the fixed writer and diff replay vs live per tick.
- **No native zero over a full fight on either loadout.**

## 52. Round 91: the replay diverges at the takeoff frame -- the feather-fall validator neutralizes EVERY control

### 52.1 The differential measurement

A fresh dense live run with the weak wing (`game-probe-densefix`, 1200 ticks) records **1 hit / 9 boss
damage / 10 dash-active ticks**. Harvesting that same run with the **fixed** writer and replaying its own
route:

```
harvest : rows 1200, tick range 2..1200, 0 neutral fillers
          controls: left=271 right=636 jump=432 dash=11
replay  : ticks 1200, HITS 6, boss damage 54, death True
          shield rows 17, dash started 13, dash-active ticks 13, npc contact 4
```

So `§51`'s dash fix was necessary but not sufficient: the body now moves, but the replay still does not
reproduce its own source run. Reading both observation streams by `gameUpdateCount`, the **first
divergence is at `guc=241`**, the takeoff frame:

```
guc=241   live:  y=7951.52  vy=-6.476667  ctlJump=True   ctlLeft=True   jump=15
          replay: y=7958     vy=0          ctlJump=False  ctlLeft=False  jump=0
46 of the 50 common sampled ticks differ.
```

### 52.2 The mechanism, read directly

Instrumenting the plugin's own side (no IL change, `CHAITE_DIAG_FILE`) at the end of `ApplyPlan`'s control
writes and after `ValidatePendingMobility`, in the replay:

```
H_AFTER_WRITES t=241 J=True  L=True  Dash=False planJump=True  planUp=True
F_OUT          t=241 J=False L=False Dash=False
G_REJECT mobility validation rejected at pre-frame=1, post-frame=1:
         feather-fall=up-input-changed; resolution=all-controls-neutral
H_AFTER_WRITES t=242 J=False L=True  Dash=False planJump=False planUp=False
F_OUT          t=242 J=False L=True  Dash=False
```

and in the live run at the same ticks:

```
F_OUT t=241 J=True  L=True  Dash=False     <-- no rejection at all
F_OUT t=242 J=False L=True  Dash=False
... no rejection on any sampled tick
```

This is decisive and it corrects §51's working theory about *why* the neutralization happened:

1. At `t=241` the plan legitimately asks for **both** the jump and feather-fall
   (`planJump=True`, `planUp=True`), and `ApplyPlan` does write `J=True L=True`.
2. `_validatePendingFeatherFall` is armed (`TerrariaFacade.cs:3699-3703`, armed whenever
   `mobility.FeatherFall` is true), with `_pendingFeatherFallRequiresPotionUp = true` (`:3714`).
3. The validator then computes `upConflict = _pendingFeatherFallRequiresPotionUp && ... &&
   !_controlReaders["controlUp"](player)` (`TerrariaFacade.cs:3852-3856`). `controlUp` is false on this
   frame, so the frame is rejected as `feather-fall=up-input-changed`.
4. `ResolveRejectedPendingMobility` (`:3939-3945`) computes
   `usedFallback = optionalEdgeRejected && !featherPhysicsRejected && ApplyLateMobilityFallback(player)`.
   Here `featherPhysicsRejected` is **true**, so `usedFallback` is false and
   **`NeutralizePendingInput(player)` runs, zeroing every control AND every captured control**
   (`:3947-3954`).

**A feather-fall validation failure therefore discards the jump, the horizontal direction and the dash
together.** That single coarse clear is what makes the takeoff frame differ and the whole trajectory
diverge from `t=241` onward. The instrumented replay reproduced exactly the §51 numbers (HITS 6), so the
observation is not perturbing the run.

### 52.3 What this means and what the fix must be

`all-controls-neutral` is wrong as a response to a **feather-fall** rejection. The failed certificate
concerns one optional edge (the potion-up slow-fall input). The correct response is to neutralize **only
the feather-fall control** (`controlUp` / the `FeatherFallUp` intent) and keep the plan's jump, horizontal
and dash, which were never in question. That is also why live is unaffected while replay is not: the
recorded live run never hit this rejection on the sampled ticks, so its `ApplyPlan` output reached
`JumpMovement` intact and it jumped.

Two candidate fixes, in order of preference:

1. **Make the rejection granular.** In `ResolveRejectedPendingMobility`, when the rejection is
   feather-physics-only, clear just the feather-fall control instead of calling
   `NeutralizePendingInput`. (`NeutralizePendingInput` remains correct for a gravity/dash edge rejection,
   where the whole manoeuvre is unsafe.)
2. **Make `_pendingFeatherFallRequiresPotionUp` non-sticky.** It is armed from the previous frame's plan
   and then demanded against the current frame's `controlUp`, so a frame whose plan does **not** request
   the potion-up (`planUp=False`, as at `t=242`) can still be rejected for not holding it. Deriving the
   requirement from the frame's own `plan.FeatherFallUp` removes that contradiction.

### 52.4 Status

- **Tree clean**; all plugin diagnostics reverted; solution builds clean. Retained from this round:
  `tools/harvest-native-route.py:205` (sixth format placeholder, now confirmed present).
- **Established:** the replay's first divergence from its own live source is `guc=241`; live writes
  `ctlJump=True` and jumps (`vy=-6.48`), replay writes `ctlJump=True` and then the feather-fall validator
  rejects and `NeutralizePendingInput` zeroes **every** control, so replay reaches `JumpMovement` with
  `ctlJump=False` and never leaves the ground.
- **Corrected:** §51 attributed the neutralization to the missing dash channel. The dash channel was a
  real and separate bug (now fixed) that caused a per-tick neutralization; the residual `t=241` divergence
  is a **feather-fall** rejection with the same coarse "all controls neutral" response.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**. Live weak-wing formula route, 1200 ticks:
  **1 hit**.
- **No native zero over a full fight on either loadout.**

## 53. Round 92: the granular feather-fall fix cuts the replay from 6 hits and a death to 2 hits

### 53.1 The fix, and its measured effect

§52 established that the replay's takeoff frame was lost because a **feather-fall** rejection produced
`resolution=all-controls-neutral`, zeroing the jump, the horizontal direction and the dash together.
Applying §52's preferred fix -- make the rejection granular -- in `ResolveRejectedPendingMobility`
(`TerrariaFacade.cs:3917-3937`): a feather-physics-only rejection now clears **only** the feather-fall
input (`SetPendingControl(player, "controlUp", false)`) and the coarse `NeutralizePendingInput` is reserved
for a gravity/dash edge rejection, where the whole manoeuvre really is unsafe.

Measured on the **same** route (`tmp/route-densefix.txt`, 1200 ticks, weak wing):

```
before:  ticks 1200  HITS 6  boss damage 54  death True   (shield rows 17, dash-active 13, npc contact 4)
after :  ticks 1200  HITS 2  boss damage 18  death False  (shield rows 19, dash-active 17, npc contact 2)
```

**Three of the six hits and the death are gone**, and the takeoff now happens: the per-tick diff against
the live source run shows the two runs agreeing exactly through `guc=253` (`pos (640.00, 7889.08)`,
`vel (0.000, -5.010)`, `jump=0`, `wingTime=130`, identical plans), where before they diverged at the
takeoff frame `guc=241`. Divergence now begins at `guc=254`.

### 53.2 The next divergence, and the corrected source theory

At `guc=254` the replay has `planDash=True` and `vy=-4.74` while live has `planDash=False` and `vy=-4.48`.
Instrumenting the replay block itself (`R_READ`, on the plugin's log) reads out the values the route
supplies:

```
R_READ frames=0  tick=241 dir=-1 jump=True  up=True  down=False dash=False preDash=False
R_READ frames=13 tick=254 dir=-1 jump=False up=False down=False dash=True  preDash=False
```

So at tick 254 the route itself carries `dash=True` and the planner had `preDash=False`: **the route is the
source, and the desync is on the tick axis, not in the applied-vs-plan channel.**

The harvester's own docstring records the decisive earlier measurement on this axis: a route harvested
entirely from the applied controls replayed the *jump* wrongly (`MovementActionGate.ResolveJump` /
`JumpMotion.ResolveControl` are **not idempotent**), producing 6 hits and a death with the first divergence
at tick 240 where the original applied `jump=True` and the replay produced `jump=False`. So harvesting the
applied controls wholesale is not the answer either.

`tools/harvest-native-route.py` now therefore chooses **per channel**: `jump` from the plan (it must be
able to win the gate), and `up`/`down`/`dash` from what was actually applied. That change is kept, but on
its own it does not fix tick 254, because the route already carries the stray `dash=True` for that tick.

### 53.3 The remaining blocker: the jump channel needs a release frame

The takeoff is still suppressed for one frame. `JumpMotion.ResolveControl` (`JumpMotion.cs:38-46`) returns
`requested && (!grounded || state.ReleaseReady || state.AutoJump || grappling)`, and the frame is grounded,
so the jump requires **`state.ReleaseReady`**. `ReleaseReady` is only set by `ApplyJump` on a frame where
`controlJump` is false (`JumpMotion.cs:55-59`). The route's first route-driven frame is `tick=241` with
`jump=True` (`R_READ frames=0 tick=241 jump=True`), so the replay reaches the resolver with no preceding
released-input frame in the route-driven region, `ReleaseReady` is false, and the takeoff is refused. Once
the replay misses that jump its whole trajectory is offset and it takes its 2 hits.

This also explains the docstring's earlier "not idempotent" observation precisely: it is the same
`ReleaseReady` requirement. The fix is to guarantee a released-input frame **before** the first requested
jump in the replayed region -- either by extending the harvested route to cover the takeoff approach
(the recorded run's frames before 241, which the current harvest starts at tick 2 with all-neutral rows),
or by having the replayer prime `ReleaseReady` when the route's first nonzero jump follows only neutral
frames.

### 53.4 Status

- **Kept and verified:** the granular feather-fall rejection (`TerrariaFacade.cs:3917-3937`) and the
  per-channel harvest source (`tools/harvest-native-route.py:72-112`). All diagnostics reverted, solution
  builds clean, `git status` shows only those two files plus untracked `tmp/`.
- **Measured:** replay of the fresh dense live route, weak wing, 1200 ticks: **2 hits / 18 damage / no
  death** (was 6 hits / 54 damage / death), agreeing with live through `guc=253`.
- **Identified but not fixed:** the route's `dash=True` at tick 254 (tick-axis desync), and the takeoff
  frame refused for want of `ReleaseReady` in the route-driven region.
- Weak wing, policy off, guard 0: **4 hits at 3000 ticks**. Live weak-wing formula route, 1200 ticks:
  **1 hit**.
- **No native zero over a full fight on either loadout.**

## 54. Round 93: the route reader mis-mapped every channel from `up` onward -- replay is now tick-perfect

### 54.1 The bug

`RouteReplay.Load` accepted any row with **five or more** columns and decoded it as
`tick,direction,up,down,dash` (`RouteReplay.cs:163-182`), reading
`jump.Add(parts[2])`-by-proxy via `upBit = parts[2]`, `down.Add(parts[3])`,
`dash.Add(parts[4])`. The harvest writer emits **six** columns,
`tick,direction,jump,up,down,dash` (`harvest-native-route.py:215`). So every six-column route was
decoded with **every channel from `up` onward shifted one position**:

```
route row (written)      254 , -1 , 0 , 0 , 1 , 0
meaning                 tick  dir  jump up down dash
reader took             tick  dir  up  down dash  --
so it replayed          up=0  down=0  dash=1   <-- a shield dash that never happened
```

This is exactly the §53 mystery: `R_READ frames=13 tick=254 ... dash=True preDash=False` while the file
plainly said `dash=0`. The route's dash ticks are `346, 404, 462, 520, 578, 756, 814, 872, 930, 988,
1176` and the recorded run's applied `controlDash` ticks are `346, 404, 462, 520, 578, 756, 814, 872, 930,
988, 1176` -- **they agree exactly**, so the file was right and the reader was wrong. The mis-mapped
`down` bit became `dash`, which drove the player into a dash **92 ticks before the recorded run's first
dash**, and that was the first divergence at `guc=254`.

### 54.2 The fix

`RouteReplay.Load` now dispatches on the column count: **six or more** columns are decoded as
`tick,direction,jump,up,down,dash` (the current format), and the previous five-column
`tick,direction,up,down,dash` reading is kept verbatim as the legacy branch, where the up bit still drives
the jump channel. Historical five-column files therefore keep their old meaning.

### 54.3 Measured: the replay is now tick-perfect

Replaying the weak-wing live route (`tmp/route-hybrid.txt`, 1200 ticks) against its own source run:

```
live  source : ticks 1200  HITS 1  boss damage 9  death False  shield rows 11  dash-active 10  npc contact 1
replay       : ticks 1200  HITS 1  boss damage 9  death False  shield rows 11  dash-active 10  npc contact 1

per-tick diff over 1199 common gameUpdateCounts, comparing rounded x/y/vx/vy:
   differing ticks: 0 / 1199
   IDENTICAL on every common tick
```

**`CHAITE_ROUTE_FILE` is now a faithful replayer** -- the objective's required acceptance channel. The
progression of this defect, and the total effect of the round's fixes on the same route:

```
start of round : HITS 6  boss damage 54  death True
after §53 fix  : HITS 2  boss damage 18  death False
after §54 fix  : HITS 1  boss damage  9  death False   <-- identical to the live source run
```

### 54.4 What now remains for the objective

The acceptance channel is trustworthy; the **fight itself is not yet won**. The live weak-wing formula
route still records **1 hit in 1200 ticks and does not kill the Boss** (`boss life left 77991` of 78000,
i.e. 9 damage). So exactly one contact remains to be eliminated on the weak wing, and neither loadout has
a native zero-hit **full** fight. The next work is therefore on the route/state machine rather than on the
harness: find the single remaining weak-wing contact by replaying the route and reading the recorded
`npc contact` tick, then adjust the formula route at that tick and re-verify. Because the replay is now
tick-perfect, any such change can be validated by a single replay without re-running the live fight.

### 54.5 Status

- **Kept and verified:** `RouteReplay.cs` six-column branch (`:163-186`) plus the legacy five-column
  branch (`:187-207`); the §53 granular feather-fall rejection and per-channel harvest source; the §51
  sixth-placeholder writer fix. Tree has no diagnostics; solution builds clean.
- **Established and measured:** the replay of the weak-wing live route is **tick-identical to its source
  run over all 1199 common ticks**, with identical hits (1), damage (9), death flag, shield rows, dash
  ticks and npc contacts.
- **Not achieved:** zero hits -- weak wing still takes exactly 1 contact in 1200 ticks, the Boss is not
  killed, and the strong wing has no equivalent verified full fight.
- **No native zero over a full fight on either loadout.**

## 55. Round 94: a 1200-tick zero that did not hold -- the charge-beat priority was a regression

### 55.1 The hypothesis

§54 localized the single remaining weak-wing contact to **tick 950**, and the prehit trace showed why the
perpendicular dodge did not save it. At the lock (about tick 928) the player was 370 px out with 329 px of
that horizontal, so the escape rule correctly classified it as "far enough to run flat". The
personal-space branch then took over at tick 934 (separation 193 < `PersonalSpace` 200) and latched its own
escape, and the horizontal gap collapsed 329 -> 45 px by tick 944 -- far inside the 85 px contact
threshold. So the hypothesis was: **a locked charge must keep its perpendicular dodge, and the
personal-space latch must not override it.**

The change gated the personal-space branch on `!inBeat`, where `inBeat` is
`state == 1 || state == 6 || state == 11 || _chargeNormalSequence >= 0`
(`FishronWingScript.cs:885-900`).

### 55.2 The measurement, and the trap it exposed

```
1200 ticks, live, weak wing, fishron-fairy-wing, WITH the change:
   HITS 0   boss damage 0   death False   shield rows 11   dash-active 11   npc contact 0
   "ACCEPTED: zero hits in the native engine."

3000 ticks, live, weak wing, fishron-fairy-wing, WITH the change:
   HITS 6   boss damage 99  death False   shield rows 42   dash-active 33   npc contact 9

3000 ticks, live, weak wing, fishron-fairy-wing, change REVERTED (baseline):
   HITS 4   boss damage 66  death False   shield rows 38   dash-active 32   npc contact 6
```

Two conclusions, both important:

1. **The 1200-tick zero was a window artefact, not a solution.** The run was genuinely engaged (11 dash
   starts, 0 npc contacts, `validBattle=True`, `boss seen=True`) and would have been reported as
   `ACCEPTED` by `run-native-acceptance.ps1`, which is exactly the hazard the project's own warning about
   short runs describes. A zero is only evidence once it survives a full fight; a 1200-tick zero followed
   by 6 hits at 3000 ticks is evidence of a quiet window.
2. **The change is a regression and has been reverted.** At the comparable 3000-tick length it is strictly
   worse than the baseline (`HITS 6 / damage 99` against `HITS 4 / damage 66`), and it also costs more
   contacts (9 against 6). Giving the charge beat priority over the personal-space escape removes a
   defence that was doing real work in the frames where the body is genuinely on top of the player.

So §54's reading of tick 950 was correct about *that* contact but wrong about the remedy: the
personal-space latch is not simply fighting the charge normal, it is the fallback that catches the cases
the charge normal alone does not.

### 55.3 Status

- **Reverted:** the `inBeat` gate. `git status` shows no tracked modification; `src/Chaite.Core/FishronWingScript.cs`
  is back to its committed state.
- **Measured baselines (live, weak wing, 3000 ticks):** **4 hits / 66 damage / no death / 6 npc contacts**
  with the committed state machine; 6 hits / 99 damage / 9 contacts with the reverted experiment.
- **Measured:** replay of a live weak-wing route is tick-identical to its source run over all 1199 common
  ticks, and the live weak-wing formula route records 1 hit in 1200 ticks.
- **Not achieved:** zero hits. Neither loadout has a native zero-hit full fight, and per §55.2 a short-run
  zero cannot be used as acceptance.
- **No native zero over a full fight on either loadout.**

## 56. Round 95: the charge-normal sign is decided by floating-point noise on an exact tie

### 56.1 The defect

`LatchChargeNormal` (`FishronWingScript.cs:1165-1178`) tries to choose which of the two perpendiculars to
the locked aim takes the player further off the locked line by comparing two dot products:

```csharp
var aimX = dx / lockDistance;          // dx = player.Center.X - boss.Center.X
var aimY = dy / lockDistance;
var normalAX = -aimY; var normalAY =  aimX;
var normalBX =  aimY; var normalBY = -aimX;
var dotA = normalAX * dx + normalAY * dy;
var dotB = normalBX * dx + normalBY * dy;
var normalX = dotA >= dotB ? normalAX : normalBX;
```

But `(aimX, aimY)` is by construction **parallel** to `(dx, dy)`, so both perpendiculars are perpendicular
to `(dx, dy)` and **both dot products are identically zero**. Measured for the actual lock geometries:

```
dx= -339.0 dy= -179.0  dotA=0.000000000000 dotB=-0.000000000000  -> picks A
dx=  339.0 dy= -179.0  dotA=-0.000000000000 dotB=0.000000000000  -> picks B
dx=  156.0 dy= -113.0  dotA=0.000000000000 dotB=-0.000000000000  -> picks A
dx=   47.0 dy=  -84.0  dotA=0.000000000000 dotB=0.000000000000  -> picks A
```

So the sign is decided by **floating-point noise on an exact tie**. For the tick-928 lock of the tk 950
contact (`dx = 339, dy = -179`) the tie broke to **B**, giving a normal of `(0.219, -0.976)`; the
charge-horizontal branch then drives `vertical = -1` -- **upward**, toward a Boss that is above and
charging down. The player needs to go **down** there (normal A, `(-0.219, 0.976)`). The observed trace
confirms the consequence: on approach the player descends at only `vy` +2.9, and gravity takes it to about
+9.8 only after the lock, by which time the Boss's 15.1 px/tick charge has closed a 75 px perpendicular
offset that needed 85 px.

### 56.2 Two experiments, both regressions, both reverted

Building on §55's method -- measure at 3000 ticks, not 1200 -- two candidate changes were tried and both
were strictly worse than the committed baseline:

```
baseline (committed state machine), live weak wing, 3000 ticks:
    HITS 4   boss damage 66   death False   dash-active 32   npc contact 6

(A) personal-space branch gated on !inBeat  (round 94, §55):
    HITS 6   boss damage 99   death False   dash-active 33   npc contact 9

(B) charge-normal deadband removed (0.2 -> exact zero):
    HITS 6   boss damage 123  death False   dash-active 31   npc contact 9
```

Both are reverted; `src/Chaite.Core/FishronWingScript.cs` is back to its committed state and the solution
builds clean. Note that (B) is **not** evidence that the deadband is load-bearing: it exposed the broken
sign from §56.1 more often, which is exactly why it made things worse. The two defects are coupled and must
be fixed together.

### 56.3 The correct rule

The perpendicular to select is the one that **increases the perpendicular clearance** while the Boss
closes, which is a relative-velocity question, not a position question:

```
choose n in {(normalAX,normalAY), (normalBX,normalBY)} maximising
    (n.x - chargeDir.x) * n.x + (n.y - chargeDir.y) * n.y
```

equivalently `1 - (n . chargeDir)`, i.e. maximise the component of the player's escape that is
anti-parallel to the locked charge. At the hover tick the Boss's charge velocity is not yet known, so this
cannot be evaluated at latch time; the honest options are to latch the *aim* at the lock tick and choose
the sign on the first tick where the Boss's charge velocity is observable, or to keep the
position-perpendicular but break the tie with the sign of the normal that points away from the Boss's own
future motion. Either way the discarded `0.2` deadband (see the note in (B)) should be revisited **only
after** the sign is correct.

This is directly testable with the instrument the round just validated: harvest a live route, apply the
change, and replay -- any change that helps will show up as fewer hits in a tick-perfect replay of the same
route. But note the important caveat §55 established: a live-route replay reproduces the *recorded*
controls, so a state-machine change can only be evaluated by a fresh **live** run, with the replay used to
inspect the failing frames.

### 56.4 Status

- **Reverted:** both round-94 and round-95 experiments. Tree clean apart from untracked `tmp/`; builds clean.
- **Defect established by algebra plus measurement:** `LatchChargeNormal`'s perpendicular choice is an
  exact tie broken by floating-point noise, and for the tk 950 lock it points the escape **into** the
  Boss's charge.
- **Measured baselines (live, weak wing, 3000 ticks):** committed state machine **4 hits / 66 damage /
  no death / 6 npc contacts**.
- **Also measured this round:** the replay of a live weak-wing route is tick-identical to its source over
  all 1199 common ticks.
- **Not achieved:** zero hits on either loadout over a full fight.
- **No native zero over a full fight on either loadout.**

## 57. Round 96: the charge-normal tie-break is a measured NO-OP -- the direction is not the constraint

### 57.1 The experiment

§56 proved algebraically that `LatchChargeNormal`'s perpendicular choice compares two dot products that are
**identically zero**, so the sign is set by floating-point noise on an exact tie. The obvious next question
was whether that arbitrary sign is what the remaining contacts depend on. The tie-break was flipped from
`dotA >= dotB` to `dotA > dotB` (`FishronWingScript.cs:1173-1176`), which reverses the arbitrary choice
wherever the tie is exact.

### 57.2 The result: bit-identical outcome

```
baseline (committed), live weak wing, 3000 ticks:
    HITS 4   boss damage 66   death False   shield rows 38   dash-active 32   npc contact 6
    hits at ticks [950, 2146, 2278, 2668]

flipped tie-break, live weak wing, 3000 ticks:
    HITS 4   boss damage 66   death False   shield rows 38   dash-active 32   npc contact 6
    hits at ticks [950, 2146, 2278, 2668]
```

**Every observable is identical, down to the tick of each of the four contacts.** So the arbitrary
tie-break is a **no-op on this run**: whatever the sign does, it does not change which charges connect.

### 57.3 What that rules out, and where the constraint actually is

This is a valuable negative result. It rules out "the escape points the wrong way" as the binding
constraint, and with it §56.3's proposed relative-velocity sign rule as a fix for these four contacts --
selecting a better perpendicular cannot help if the current one is already irrelevant.

The measurement points instead at **clearance versus closing speed**. Across all four contacts the player
is a consistent ~10 px short of the 85 px needed:

```
contact  perp offset at the lock   required   player lateral speed   boss charge speed
 950           75 px                  85 px        13.87 px/tick        15.1 / 17.5 px/tick
```

The player's wing cruise is about **13.87 px/tick** while the locked charge travels **15.1-17.5 px/tick**,
so the player cannot out-run the charge along its own line, and the perpendicular offset available at the
lock is not large enough to survive the closing geometry. That is a **budget/geometry** problem, not a
direction problem: it needs the player to hold a larger perpendicular offset **at the lock tick** (i.e. be
further off the Boss's approach line when the charge freezes) or to preserve flight budget for the dodge
rather than spending it on the pre-charge jump -- and §52's trace showed the pre-charge jump spends the
weak wing's entire 30-tick budget before the lock, leaving `wingTime 10` when the dodge begins.

### 57.4 Status

- **Reverted:** the flipped tie-break. Tree clean apart from untracked `tmp/`; builds clean.
- **Established by measurement:** the charge-normal tie-break direction is a **no-op** -- flipping it
  reproduces the baseline exactly (4 hits, 66 damage, identical hit ticks 950/2146/2278/2668).
- **Ruled out:** the escape-direction sign as the cause of the four remaining weak-wing contacts, and with
  it §56.3's sign rule as their remedy.
- **Now indicated:** the constraint is perpendicular clearance at the lock versus the charge's closing
  speed (player cruise 13.87 px/tick against a 15.1-17.5 px/tick charge), coupled with the weak wing's
  flight budget being spent by the pre-charge jump before the dodge begins.
- **Baselines:** committed state machine, live weak wing, 3000 ticks: **4 hits / 66 damage / no death /
  6 npc contacts**. Live weak-wing route replay is tick-identical to its source over 1199 common ticks.
- **Not achieved:** zero hits on either loadout over a full fight.
- **No native zero over a full fight on either loadout.**

## 58. Round 97: the lock aims exactly at the player, so escape must be created during the charge

### 58.1 The corrected geometric model

§57 concluded the constraint was "perpendicular clearance at the lock versus closing speed", quoting a
75 px offset at the lock. Measuring the offset properly **refutes that framing**: at the lock tick the
perpendicular offset is **exactly zero for all four contacts**.

```
seq  lockTick  lockDist  wingTime at lock  |dy| at lock  chargeSpeed  perpOffset
 1      928       370           10              169          17.0          0.0
 2     2120       537           57              108          17.0          0.0
 3     2236       351           11              180          17.0          0.0
 4     2646       417           54              130          17.0          0.0
```

That is not a coincidence in the data, it is the mechanism: `AI_069_DukeFishron` computes the locked
charge velocity as `Vector2.Normalize(player.Center - center) * num7`, so the charge line **passes through
the player at the lock by construction** (already established for the plugin's own latch in §"NATIVE
CHARGE LOCK"). The 75 px figure in §57 was the offset measured on the *following* frame, after the player
had begun moving off the line -- it is a **consequence** of the escape, not a budget available at the lock.

So the real constraint is a **race**: from a zero-perpendicular start, the player must create ~85 px of
perpendicular separation before the Boss's body covers the remaining **along-line** distance. With the
charge at 17.0 px/tick and the player needing ~85 px of offset over a ~20-tick charge, the perpendicular
escape must sustain roughly

```
85 px / 20 ticks  ~  4.25 px/tick   perpendicular to the charge line
```

### 58.2 Wing budget is NOT the differentiator

§57 speculated the weak wing's 30-tick budget was being spent by the pre-charge jump and that this was the
binding constraint. The measurement above refutes that too: **two of the four contacts happen with a nearly
full budget** (`wingTime` 57 and 54) and two with a nearly empty one (10 and 11). A constraint that is
absent in half the failures is not the constraint.

What the failing frames show instead (§52's trace) is that the perpendicular escape is **not being
committed**. During `fishron-wing-charge-horizontal` the player's perpendicular speed is only about
2.0-2.9 px/tick (the vertical component is largely rounded away and the horizontal component is aimed
along the line), well short of the 4.25 px/tick the race needs. The escape direction is not wrong (§57
proved the sign is a no-op); the escape **magnitude** is.

### 58.3 What this means for the fix

The remaining weak-wing work is therefore to **maximise perpendicular speed during the charge**, not to
change which way it points and not to save flight budget. Concretely the `charge-horizontal` branch should
push the normal's dominant component as a sustained input (the normal for a near-horizontal charge is
near-vertical, so that means committing the vertical input for the whole charge), and the `0.2` deadband
that quantises a real component to zero should be removed **only after** the sign is made deliberate rather
than tie-broken -- the two are coupled, which is why §"56.2/B" (deadband removed, sign still arbitrary) and
§"55" (charge beat prioritised) both made things worse rather than better.

Given the project's own discipline that a state-machine change can only be judged by a **fresh live run**
at full-fight length (a live-route replay reproduces the recorded controls, §55), any such change must be
validated at 3000 ticks against the committed baseline, not at 1200.

### 58.4 Status

- **Tree clean** apart from untracked `tmp/`; builds clean. No experiment left in the tree this round.
- **Corrected:** the perpendicular offset at the lock is **exactly zero for all four remaining contacts**,
  because the native lock aims the charge at the player by construction. §57's "75 px at the lock" was a
  post-lock consequence and is withdrawn as a budget statement.
- **Ruled out:** flight budget as the binding constraint -- two of four contacts occur with `wingTime` 57
  and 54.
- **Now indicated:** the escape must create ~85 px of perpendicular separation during a ~20-tick charge,
  i.e. sustain ~**4.25 px/tick** perpendicular, against the ~2.0-2.9 px/tick actually measured. The defect
  is escape **magnitude**, not direction.
- **Baselines:** committed state machine, live weak wing, 3000 ticks: **4 hits / 66 damage / no death /
  6 npc contacts**. Live weak-wing route replay is tick-identical to its source over 1199 common ticks.
- **Not achieved:** zero hits on either loadout over a full fight.
- **No native zero over a full fight on either loadout.**

## 59. Round 98: the latched normal is already near-optimal -- the shortfall is along-line, not perpendicular

### 59.1 The measurement

§58 reasoned that the escape's *magnitude* was the problem and proposed committing the perpendicular input
harder. Reconstructing `LatchChargeNormal` exactly (including its `>=` tie-break) and evaluating the
perpendicular it produces at each lock frame against the player's actual velocity gives the decisive
answer:

```
seq  lockTick   dx       dy     dotA     dotB     picksA  normal              h  v   clearance rate
 1     928    -329.4   168.6   0.0e+00  0.0e+00   True   (-0.456,-0.890)    -1 -1      +5.63
 2    2120    -526.4   108.3   1.4e-14 -1.4e-14   True   (-0.202,-0.979)    -1 -1      +4.17
 3    2236     301.1   179.9   0.0e+00  0.0e+00   True   (-0.513,+0.858)    -1 +1      -5.92
 4    2646     395.8  -130.3   0.0e+00  0.0e+00   True   (+0.313,+0.950)    +1 +1      +2.18
```

("clearance rate" is the projection of the player's velocity onto the latched normal: how fast the
perpendicular separation is actually growing.)

Three things follow, and they close off the last two hypotheses:

1. **The perpendicular escape is not the shortfall.** The latched normal already yields **+5.63, +4.17 and
   +2.18 px/tick** of clearance on sequences 1, 2 and 4 -- comfortably at or above the ~4.25 px/tick §58
   estimated was needed for the race. The player *is* leaving the locked line fast enough.
2. **The tie-break picks the better perpendicular in 3 of the 4 cases.** Only sequence 3 gets the worse
   side (-5.92 against a possible +5.92). So the floating-point tie-break from §56 is not the systemic
   cause either, which is consistent with §57's finding that flipping it changed nothing measurable.
3. **The two contacts with the best clearance rates (1 and 2: +5.63, +4.17) still connect.** If leaving the
   line quickly were sufficient, those would be the survivors. They are not. So the failure is in the
   **along-line** component: the charge closes the along-line distance at 17.0 px/tick while the player's
   own velocity has a large component *along* the line (e.g. sequence 1: player velocity `(-6.8, -2.9)`
   projected onto the charge direction `(-0.890, 0.456)` is `+6.05 - 1.32 = +4.7` along the line), so the
   body arrives while the player is still translating down the corridor.

### 59.2 Where the remaining weak-wing work actually is

Every geometric hypothesis about the *direction* or *rate* of the perpendicular escape is now excluded by
measurement, as are the flight-budget hypothesis (§58) and the tie-break hypothesis (§56/§57). What is left
is the **along-line** term: to survive, the player must not merely leave the line, it must also **not travel
down it** toward the incoming body. That is a different control problem from the one the
`charge-horizontal` beat solves, and it explains why three separate attempts aimed at the perpendicular
(§55 prioritising the beat, §56.2/B removing the deadband, §57 flipping the sign) each failed to help or
made things worse: none of them touched the along-line component.

This is the state of knowledge at the point the session's round budget is nearly exhausted, and it is
recorded as the concrete next target rather than as a claim: **the along-line component of the player's
velocity during a locked charge must be driven toward zero (or reversed to match the charge) while the
perpendicular escape runs**, and that change must be validated by a fresh **live** 3000-tick run against
the committed baseline, because a live-route replay reproduces the recorded controls and cannot judge a
state-machine change (§55).

### 59.3 Status

- **Tree clean** apart from untracked `tmp/`; builds clean; no experiment left in the tree.
- **Established by measurement:** the latched perpendicular gives +5.63 / +4.17 / +2.18 px/tick clearance on
  three of the four remaining contacts, and the tie-break selects the better perpendicular in 3 of 4 cases.
- **Ruled out:** perpendicular escape direction (§56/§57), perpendicular escape magnitude (§59.1 item 1),
  flight budget (§58.2), and the arbitrary tie-break (§57) as causes of the four remaining weak-wing
  contacts.
- **Now indicated:** the **along-line** component -- the player still translates down the charge corridor
  while the body closes at 17.0 px/tick.
- **Baselines:** committed state machine, live weak wing, 3000 ticks: **4 hits / 66 damage / no death /
  6 npc contacts**; hits at ticks 950, 2146, 2278, 2668. Live weak-wing route replay is tick-identical to
  its source over all 1199 common ticks.
- **Not achieved:** zero hits on either loadout over a full fight.
- **No native zero over a full fight on either loadout.**

## 60. Round 99: consolidated session state, so the next session starts from measured facts

### 60.1 What is done and verified

Before this session `CHAITE_ROUTE_FILE` did not replay at all. Four defects were found and fixed, each with
its own measured effect, all committed:

1. **The harvest writer dropped the dash channel** -- five format placeholders for six controls
   (`harvest-native-route.py:215`), so the trailing `dash` was silently discarded from every route. That
   armed the plugin's pending-dash validation, which called `NeutralizePendingInput` and **zeroed every
   control and every captured control on every tick**, so the replayed body never moved. Fixing it is what
   first made the replay body move at all.
2. **The feather-fall rejection resolved to `all-controls-neutral`**, discarding the jump, the horizontal
   direction and the dash together, which lost the takeoff frame (`TerrariaFacade.cs:3917-3937`).
3. **The route reader decoded six-column files as five-column**, shifting every channel from `up` onward,
   so the `down` bit replayed as a **dash 92 ticks early** (`RouteReplay.cs:163-207`).

Combined effect on one route, replaying the weak-wing live route:

```
HITS 6 / damage 54 / death True   ->  HITS 2 / 18 / False  ->  HITS 1 / 9 / False
per-tick diff over 1199 common gameUpdateCounts: 0 differing ticks
```

**The acceptance channel the objective requires is demonstrated**: native per-tick replay reproduces its
recorded run exactly (identical hits, damage, death flag, shield rows, dash ticks, npc contacts).

### 60.2 Baseline used throughout

Committed state machine, live weak wing, `fishron-fairy-wing`, 3000 ticks: **4 hits / 66 boss damage /
no death / 6 npc contacts**, hits at ticks 950, 2146, 2278, 2668.

### 60.3 Not done

**No native zero-hit full fight exists on either loadout.** The objective is not met and must not be
reported as met.

### 60.4 Excluded, each by direct measurement -- do not repeat

- **Perpendicular escape direction**: flipping the charge-normal tie-break reproduced the baseline exactly,
  with identical hit ticks.
- **Perpendicular escape magnitude**: the latched normal already yields +5.63, +4.17 and +2.18 px/tick of
  clearance on three of the four contacts, at or above the ~4.25 px/tick the race needs.
- **Flight budget**: two of four contacts occur with `wingTime` 57 and 54, not with a spent wing.
- **The floating-point tie-break itself**: it selects the better perpendicular in three of four cases.
- **Prioritising the charge beat over the personal-space escape**: measured worse (6 hits / 99 damage).

### 60.5 Next target

**The along-line component.** The two contacts with the *best* clearance rates (+5.63 and +4.17 px/tick)
still connect, and the charge closes the along-line distance at 17.0 px/tick while the player's velocity
retains a large component along the line (+4.7 on sequence 1). Surviving therefore requires the along-line
component of the player's velocity during a locked charge to be driven toward zero -- or reversed to match
the charge -- while the perpendicular escape continues. The three failed attempts all aimed at the
perpendicular and none touched this term.

### 60.6 Method constraints for whoever continues

- A live-route replay reproduces the **recorded** controls: it can inspect failing frames but **cannot judge
  a state-machine change**. State-machine changes require a fresh **live** run.
- **Short runs are not evidence.** A 1200-tick zero was measured and then showed **6 hits at 3000 ticks** in
  the same configuration. Acceptance needs full-fight length (3000+ ticks) against the committed baseline.
  `run-native-acceptance.ps1` will print `ACCEPTED` for a short quiet window.
- Harvest with the fixed writer; a live-route replay is then tick-exact (0 differing ticks over 1199).

### 60.7 Status

Tree clean apart from untracked `tmp/`; solution builds clean. Objective remains **active and incomplete**.

## 61. Round 100: the wing's capability is not the bound -- the commanded escape is

### 61.1 The capability measurement

Read from the committed-baseline live run (`game-probe-weak-3000-base`), over the whole 3000-tick fight:

```
player |vx| max       = 14.50 px/tick
player vy max (down)  = 10.01 px/tick
player vy min (up)    = -9.91 px/tick
```

So the weak wing (Fairy Wings) can command **up to ~10 px/tick vertically** and **~14.5 px/tick
horizontally**. The perpendicular escape a locked, near-horizontal charge needs is about **4.25 px/tick**
(§58.1: ~85 px of clearance to create over a ~20-tick charge), and §59.1 measured the latched normal
already yielding +5.63 / +4.17 / +2.18 px/tick of clearance.

**Conclusion: the constraint is not the wing's capability.** The airframe can deliver more than twice the
required perpendicular rate. What is missing is the *commanded* escape, and §59.1's item 3 says which part:
the two contacts with the **best** clearance rates still connect, because the player keeps translating
**along** the locked line while the charge closes it at 17.0 px/tick.

### 61.2 The arithmetic of the remaining gap

For sequence 1 (`prehit`, lock at tick 928):

```
charge direction (unit)          (-0.890, +0.456)
player velocity at lock          (-6.8, -2.9)
  -> component along the line    (-6.8)(-0.890) + (-2.9)(0.456) = +4.7 px/tick
  -> clearance rate onto normal  +5.63 px/tick
needed clearance rate            ~4.25 px/tick   (already met)
needed along-line component      ~0              (measured +4.7)
```

The perpendicular term is satisfied; the along-line term is not. Driving the along-line component to zero
while holding the perpendicular rate is the single concrete change that the measurements support, and it is
**not** what any of the three failed attempts did (§60.4). With `|vx|` up to 14.5 and `|vy|` up to 10
available, such a split is well inside the airframe's envelope, so this is a solvable control problem
rather than a capability wall.

### 61.3 What a next session should do

1. Compute the charge direction at the lock (`Normalize(player - boss)` frozen at the lock tick, which is
   exactly what the native code uses -- §"NATIVE CHARGE LOCK") and decompose the commanded input into
   along-line and perpendicular parts.
2. Command the perpendicular part at full strength (the normal, as today) and **cancel the along-line
   part** -- do not keep running down the corridor the charge is travelling.
3. Validate with a fresh **live** 3000-tick run against the committed baseline (4 hits / 66 damage), because
   a live-route replay reproduces recorded controls and cannot judge a state-machine change.
4. Only after the sign is deliberate, revisit the `0.2` deadband, since §56.2/B showed that removing it
   while the sign is still tie-broken makes things worse.

### 61.4 Status

- Tree clean apart from untracked `tmp/`; solution builds clean.
- **Measured capability bound:** weak wing commands up to ~14.5 px/tick horizontal and ~10 px/tick
  vertical, against a ~4.25 px/tick requirement -- capability is not the constraint.
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 62. Round 101: the commanded escape is already the full normal -- so the gap is in execution, not command

### 62.1 The finding

§61 concluded the constraint is the *commanded* escape rather than wing capability. Reading the committed
`charge-horizontal` branch refutes the "command" half of that:

```csharp
// FishronWingScript.cs
horizontal = _chargeNormalHorizontal;                       // :780  (locked normal)
...
vertical = _chargeNormalSequence >= 0
    ? _chargeNormalVertical                                 // :817-818 (locked normal)
    : (player.OnGround ? -1 : 0);
phase = "fishron-wing-charge-horizontal";                    // :820
```

So during a locked charge the code **already commands the full latched normal on both axes** -- there is no
uncommanded component left to switch on. Yet the observed velocity on the tick-950 charge is
`(14.18, -2.06)`: the horizontal is at cruise (~14.2 of a ~14.5 maximum) but the vertical is only
**-2.06 px/tick against the ~9.9 px/tick the airframe can command** (§61.1).

So the deficit is between the **command** and the **executed motion**, not in the command itself. That
reframes the problem and explains the three failures in §60.4: every attempt (prioritising the beat,
removing the deadband, flipping the sign) changed *which* normal was commanded, and the normal was never the
binding term.

### 62.2 The candidate mechanism, and why it is not yet established

The obvious candidate is wing application. `Player.WingMovement` is gated on
`wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 && velocity.Y != 0`, so sustained vertical
authority requires **`controlJump` to be held** -- the jump channel, not `controlUp`. A `vertical` input of
`-1` in the plan's terms does not by itself hold the jump key.

**This is a hypothesis, not a measurement.** The baseline run used for §61/§62 was harvested **without**
`CHAITE_PROBE_DENSE_FRAMES`, so its observation stream is sparse (163 rows over 3000 ticks, one row every
~46 ticks) and the `jump` field is a jump-duration counter, not a boolean. It is therefore not possible from
this stream to say whether `controlJump` was held during the charge. Settling it needs a **dense** live run
(one row per tick) with the wing fields recorded across a charge window -- which is exactly the instrument
§54 validated.

### 62.3 Recommended next step (do this before any state-machine edit)

1. Run one **dense** live weak-wing fight (`CHAITE_PROBE_DENSE_FRAMES=1`, 3000 ticks).
2. Over the charge windows, tabulate `controlJump`, `wingTime`, `velocity.Y` and the commanded
   `plan` vertical/horizontal per tick.
3. Only if `controlJump` is false while `vertical = -1` is commanded does the "hold the jump to apply the
   wing" hypothesis become a measured fact -- at which point the fix is to hold the jump through the locked
   charge, and it must be validated by a fresh live 3000-tick run against the committed baseline
   (4 hits / 66 damage).

Doing the edit before step 3 would be the same mistake as §55, §56.2/B and §57, each of which changed the
commanded normal without first establishing that the normal was the binding term.

### 62.4 Status

- Tree clean apart from untracked `tmp/`; solution builds clean. No experiment left in the tree.
- **Established by reading the committed code plus the measured velocity:** the locked-charge branch already
  commands the full latched normal on both axes, and the executed velocity is ~14.2 px/tick horizontal
  against ~14.5 available but only ~2.06 px/tick vertical against ~9.9 available.
- **Reframed:** the deficit is command-to-execution, not command selection -- consistent with the three
  measured failures in §60.4.
- **Not established:** whether `controlJump` is held during the charge. The baseline stream is sparse and
  cannot answer it; a dense run is required (step 62.3).
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 63. Round 102: dense charge window -- the vertical escape is ballistic, and the jump channel is DOWN

### 63.1 The dense measurement (§62.3 steps 1-2, carried out)

One dense live weak-wing run (`CHAITE_PROBE_DENSE_FRAMES=1`, 1200 ticks, **1 hit**, `npc contact 1`) gives one
row per tick. Tabulating the tick-950 contact's locked charge:

```
guc | plan(h,up,drop,jump) | player ctl(J,U,D) | vy      wingTime
 929 | (-1,0,0,True)       | (True, False,False) | -2.86   10
 930 | (+1,0,1,False)      | (False,False,True ) | -2.46   10
 931 | (+1,0,1,False)      | (False,False,True ) | -2.06   10
 932 | (+1,0,1,False)      | (False,False,True ) | -1.66   10
 933 | (+1,0,1,False)      | (False,False,True ) | -1.26   10
 934 | (+1,0,1,False)      | (False,False,True ) | -0.86   10
 935 | (+1,0,1,False)      | (False,False,True ) | -0.46   10
 936 | (+1,0,1,False)      | (False,False,True ) | -0.06   10
```

Two facts, both new and both measured:

1. **`controlJump` is FALSE and `controlDown` is TRUE through the entire locked charge.** `Player.WingMovement`
   is gated on `wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 && velocity.Y != 0`, so the wing
   is gated **off**: the vertical motion is pure ballistic decay.
2. **The decay rate proves it**: `vy` goes `-2.46, -2.06, -1.66, -1.26, -0.86, -0.46, -0.06` -- exactly
   `+0.40` per tick, i.e. `gravity(0.1333) x 3`, with **no thrust term at all**. `wingTime` stays pinned at
   10 the whole time, confirming the wing never ran.

That is the missing escape magnitude from §61/§62: the perpendicular rate is ~2.2 px/tick where the race
needs ~4.25, because the vertical escape is falling under gravity rather than being flown.

### 63.2 A correction to §62, and why no edit was made this round

§62's hypothesis was that the jump channel needed holding. **That was wrong**, and it is corrected here:
`output.Jump = vertical < 0` (`FishronWingScript.cs:526`) already raises the jump whenever the charge branch
commands a negative vertical, so the jump is *not* uncommanded by construction. The draft edit that added an
explicit `jump = true` failed to compile (`CS0103: name 'jump' does not exist in the current context` --
`ChargeEscape`'s outputs are `horizontal, vertical, phase, dash`), and the 3000-tick run launched after that
failed build **used the stale DLL**, so its numbers are void. The edit is reverted; the tree is clean.

The unresolved question is therefore sharper and different: at `guc 930` the plan reads
`(h=+1, up=0, drop=1, jump=False)` while the observed phase is `fishron-wing-charge-horizontal`, whose branch
sets `vertical = _chargeNormalVertical` and `horizontal = _chargeNormalHorizontal`. If `_chargeNormalSequence
>= 0` the branch would light `jump` via `:526`; it does not, so on these ticks the branch's
`_chargeNormalSequence < 0` arm is what runs -- meaning **the normal was not latched for this charge even
though the charge is locked**, and the `+1`/`-9.00` horizontal reversal at `guc 942` is the later
`_chargeNormalHorizontal`. That is the next thing to measure, and it is a `_chargeNormalSequence` question,
not a wing-application question.

### 63.3 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; solution builds clean.
- **Measured:** through the whole locked charge, `controlJump=False`, `controlDown=True`, `wingTime` pinned
  at 10, and `vy` decaying at exactly `+0.40` per tick (ballistic, zero wing thrust).
- **Corrected:** §62's "the jump needs holding" is withdrawn -- `:526` already derives the jump from a
  negative vertical.
- **Void:** the 3000-tick numbers from the run launched after the failed build (stale DLL).
- **Next target:** why `_chargeNormalSequence < 0` (so `vertical`/`horizontal` do not take their latched
  normal values) on charge ticks whose phase is `fishron-wing-charge-horizontal`.
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 64. Round 103: KEPT FIX -- the charge normal is chosen by the player's velocity, not by a zero tie-break

### 64.1 The defect, now fully resolved

§63 left the question "why does the charge tick command `drop=1, jump=False`?" Resolving it needs the
mapping, which is:

```
CombatPlanner.cs:846   plan.Jump = script.Jump && script.Vertical < 0;
CombatPlanner.cs:848   plan.Drop = script.Vertical > 0;
```

So the dense `plan(h=+1, up=0, drop=1, jump=False)` means `script.Vertical = +1`: **the script was telling
the player to DESCEND for the whole charge.** The tick-950 charge needed the opposite -- the player had to
leave the locked line *upward*, and `vy` was already `-2.46` and rising. The script reversed it.

The cause is §56's tie-break, and it is not merely "arbitrary": because the offset `(dx,dy)` is parallel to
the aim, `dotA` and `dotB` are **identically zero**, so `dotA >= dotB` is `0 >= 0` and the code **always
picks normal A**, whatever the geometry. Flipping the comparison (§57) was a no-op because `0 > 0` is also
false and the fallback is the same `normalB` only when the noise says so -- which is why it reproduced the
baseline exactly.

### 64.2 The fix

`LatchChargeNormal` now projects the **player's velocity** onto the two perpendiculars and picks the one the
player is already travelling along -- i.e. the direction that actually increases clearance:

```csharp
var rateA = normalAX * player.Velocity.X + normalAY * player.Velocity.Y;
var rateB = normalBX * player.Velocity.X + normalBY * player.Velocity.Y;
var normalX = rateA >= rateB ? normalAX : normalBX;
var normalY = rateA >= rateB ? normalAY : normalBY;
```

This asks the question the old code was *trying* to ask ("which perpendicular takes the player further off
the locked line?") in the only well-posed form, and it is **idempotent** -- it reads state instead of
comparing two constants -- so floating-point noise cannot flip it. It also finally implements §56.3's
relative-velocity idea in the correct place: §57 ruled out flipping the *sign*, but the sign was never the
lever; the **selection** was.

### 64.3 Measured verification (live, weak wing, 3000 ticks, fresh DLL confirmed)

DLL `Chaite.Core.dll` mtime `15:47:05` is later than `FishronWingScript.cs` `15:47:01`, and `rateA`/`rateB`
are present at `:1196-1199`, so the run used the new build (the stale-DLL trap of §63.2 is guarded:

```
                     baseline (§60.2)      velocity-normal (§64)
HITS                       4                       2
boss damage               66                       9
death                   False                   False
shield rows               38                      33
dash-active ticks         32                      32
npc contact                6                       1
```

**Both remaining hits are Boss body contact** (`hurt-observations`: `kind: npc, type: 370`, damage 140,
`life` 78000 -> 77991, i.e. 9 damage after defence) -- so the two surviving hits are exactly the charge
contacts the change targets, and **no projectile hit remains**.

This is the first change this session that improved the weak-wing result rather than matching or worsening
it, and it is **kept**.

### 64.4 Status

- **Change kept** in `src/Chaite.Core/FishronWingScript.cs`; solution builds clean.
- **Verified against the committed baseline: 4 hits / 66 damage -> 2 hits / 9 damage, npc contact 6 -> 1**,
  at the same 3000 ticks, with a confirmed-fresh DLL.
- **Not yet a zero**, and not claimed as one: **2 body contacts remain** over 3000 ticks.
- The 3000-tick length matches the length §60.6/§63 require for evidence, but a longer run remains the
  stronger test.
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 65. Round 104: the velocity-normal fix is duration-dependent -- it wins at 3000 and dies at 4598

### 65.1 The longer run

§64.4 flagged that 3000 ticks was the evidence length but that a longer run was the stronger test. Running
the same kept build at `-maxticks 6000` (`game-probe-velnormal-6000b`, `-wallseconds` caps at 900, so the
fight itself ended at 4598):

```
                     baseline (§60.2)   velnormal 3000   velnormal 6000
ticks                      3000              3000             4598 (died)
HITS                         4                 2                8
boss damage                 66                 9               31
death                     False             False             TRUE (FailedAfterDeath)
shield rows                 38                33               48
dash-active ticks           32                32               45
npc contact                  6                 1                3
```

**All eight hits are Boss body contact** (`hurt-observations`: every row `kind: npc, type: 370,
damage 140`; boss life 78000 -> 77969, i.e. 31 damage after defence). **No projectile hit at any length.**
So the velocity-normal change fixes the charge-contact geometry in the window it was tuned against, and then
the same geometry fails repeatedly later in the fight.

### 65.2 What this means, stated plainly

This is the **third** time this project has seen a short-window win that does not survive a longer fight
(the 1200-tick zero of §60.6, and now 3000 -> 4598). The conclusion is methodological and it applies to both
the remaining work and to acceptance:

- **3000 ticks is not sufficient evidence for the weak-wing fight.** The same build goes from 2 hits to a
  death between 3000 and 4598 ticks. Any verdict taken at 3000 -- *including the `ACCEPTED` verdict of
  `run-native-acceptance.ps1`* -- must be treated as provisional.
- Because §60.6 already requires "full-fight length" and 6000 is the harness maximum, the practical rule is:
  **accept only a run that survives to the `-maxticks` cap without death, and re-run at the cap after every
  change.**

### 65.3 Decision on the fix

The change is **kept**, because it is a genuine measured improvement on the charge-contact mechanism the
whole investigation has been about (npc contact 6 -> 1 at 3000 ticks, 4 hits/66 damage -> 2 hits/9 damage),
and because reverting it would restore a defect that is now understood rather than mysterious. It is
recorded here as **partial and not an acceptance**: it does not reach zero at any tested length and it dies
at 4598.

### 65.4 Status

- Change kept in `src/Chaite.Core/FishronWingScript.cs`; builds clean.
- **Measured:** velnormal at 3000 ticks = 2 hits / 9 damage / no death; at 4598 ticks = 8 hits / 31 damage /
  **death**. Baseline = 4 hits / 66 damage at 3000 ticks.
- **All hits at both lengths are Boss body contact** (`type 370`); no projectile hits.
- **Method constraint tightened:** a 3000-tick verdict, including `ACCEPTED`, is **provisional**; accept only
  a death-free run at the `-maxticks` cap (6000).
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 66. Round 105 (session close): both loadouts measured; the objective is NOT met

### 66.1 The strong-wing set, measured for the first time this session

§60-§65 evaluated only the weak wing. The strong wing (Fishron Wings, `wingsLogic` 26, `wingTimeMax` 180)
was run once with the kept velocity-normal build:

```
tools/run-native-acceptance.ps1 -RunName strongwing-3000 -Phase monitor ^
  -MaxTicks 3000 -WallSeconds 900 -FormulaRoute fishron-strong-wing

valid battle : True     boss seen : True
ticks        : 3000     HITS      : 2      boss damage : 18
death        : False    npc contact : 2    shield rows : 35   dash-active : 33
armor+accessory : 1547,1549,1550,3990,2609,0,860,491,3097,0   (accessory 2609 = Fishron Wings)
```

No pre-fix strong-wing baseline was taken this session, so the improvement is **not** measured for this
loadout -- only the current absolute result is known. Do not compare it to the weak-wing baseline.

### 66.2 The complete measured picture at session close

```
loadout                 ticks   HITS   damage   death   npc contact
weak  (fairy-wing)       3000     2       9     False       1        <- after §64 fix
weak  (fairy-wing)       4598     8      31     TRUE        3        <- same build, longer
weak  (fairy-wing)       3000     4      66     False       6        <- pre-fix baseline §60.2
strong(fishron-wing)     3000     2      18     False       2        <- no pre-fix baseline
```

**Every hit recorded at every length and on both loadouts is Boss body contact** (`kind: npc, type: 370`);
**no projectile hit was ever recorded**, which is consistent with the owner's correction that the one-HP
bubble projectiles need no special handling provided lateral speed is maintained.

### 66.3 Objective status at session close -- stated without softening

The objective requires, for **both** loadouts, a formulaic positioning state machine measured at native
`hits == 0`. That is **not met**:

- No loadout reaches zero at any tested length.
- The weak wing reaches its best result (2 hits) at 3000 ticks and then **dies at 4598** with the same build.
- The strong wing has been measured once (2 hits at 3000) and its longer-run behaviour is **unknown**.
- Therefore **no no-hit claim is made**, and none may be inferred from these numbers.

What **is** delivered and verified this session:

1. `CHAITE_ROUTE_FILE` is a faithful tick-perfect native replayer -- the acceptance channel the objective
   names. Verified by a per-tick diff: **0 differing ticks over 1199 common ticks**, with identical hits,
   damage, death flag, shield rows, dash ticks and npc contacts (§60.1).
2. Four defects in that channel were found, fixed and measured (harvest writer dropping the dash channel;
   feather-fall rejection neutralising all controls; six-column routes decoded as five-column).
3. The charge-contact defect was root-caused: the perpendicular was selected by two **identically zero**
   dot products, so the code always took normal A and could command the player to **descend out of a charge
   it had to climb out of** (§63.1, §64.1).
4. A principled, idempotent fix -- select the perpendicular by projecting the **player's velocity** (§64.2) --
   which at 3000 ticks cut the weak wing from **4 hits / 66 damage / 6 contacts** to
   **2 hits / 9 damage / 1 contact**, and left **zero projectile hits**.
5. A tightened acceptance rule: a 3000-tick verdict, **including the harness's own `ACCEPTED`**, is
   **provisional**; only a death-free run at the `-maxticks` cap counts (§65.2).

### 66.4 Exact reproduction

```
# build
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    Chaite.sln -p:Configuration=Release -v:minimal -nologo

# weak wing, 3000 ticks (current best: 2 hits / 9 damage)
& .\tools\run-native-acceptance.ps1 -RunName weak-3000 -Phase monitor `
    -MaxTicks 3000 -WallSeconds 900 -FormulaRoute fishron-fairy-wing

# weak wing at the cap (currently dies at 4598)
& .\tools\run-native-acceptance.ps1 -RunName weak-6000 -Phase monitor `
    -MaxTicks 6000 -WallSeconds 900 -FormulaRoute fishron-fairy-wing

# strong wing
& .\tools\run-native-acceptance.ps1 -RunName strong-3000 -Phase monitor `
    -MaxTicks 3000 -WallSeconds 900 -FormulaRoute fishron-strong-wing
```

`-wallseconds` must be in `15..900`; `-maxticks` in `600..24000`. Always confirm the DLL is newer than the
source before trusting a run (§63.2), and clear `CHAITE_*` environment variables between runs.

### 66.5 Status

- Tree clean apart from untracked `tmp/`; solution builds clean; the §64 fix is committed (`a46bca3`).
- **Objective NOT met.** No native zero on either loadout. Goal left **active** for the next session.
- **No no-hit claim is made anywhere in this document without a measured zero.**

## 67. Round 106: closing reproduction of the committed build

The committed revision was re-verified from a clean environment (all `CHAITE_*` variables cleared) to confirm
the headline result is reproducible rather than a one-off:

```
dll  Chaite.Core.dll 2026/9/26 15:47:05
src  FishronWingScript.cs 2026/9/26 15:47:01     -> OK: DLL is newer than source (not stale)

tools/run-native-acceptance.ps1 -RunName closeverify-3000 -Phase monitor ^
  -MaxTicks 3000 -WallSeconds 900 -FormulaRoute fishron-fairy-wing

valid battle : True     boss seen   : True
ticks        : 3000     HITS        : 2        boss damage : 9
death        : False    outcome     : test-time-limit
npc contact  : 1        shield rows : 33       dash-active : 32
boss life left : 77991
NOT ACCEPTED: 2 hit(s).
```

**This reproduces §64.3 exactly** -- 2 hits, 9 boss damage, no death, 1 npc contact, 33 shield rows,
32 dash-active ticks -- and the `npc contact : 1` / `HITS : 2` pair again shows that one of the two hits is
recorded as a body contact while the other is not counted as an `npc contact` event. Both are nonetheless
`kind: npc, type: 370` body hits (§65.1), so **still no projectile hit**.

### 67.1 Final position

- Committed fix (`a46bca3`) is **reproducible**: 2 hits / 9 damage / no death at 3000 ticks, down from the
  4 hits / 66 damage / 6 contacts pre-fix baseline (§60.2).
- **The objective is not achieved.** `hits == 0` is not reached on either loadout at any tested length; the
  same build dies at 4598 ticks (§65.1); and the strong wing has only a single 3000-tick measurement (§66.1)
  with its longer-run behaviour unknown.
- **The round budget for this goal is exhausted** (round 100 of `maxGoalRounds 100`). The goal is left
  **active** so a following session can continue from §66.4's reproduction commands; it is **not** marked
  complete, because marking it complete would assert a native zero that was never measured.
- Tree clean apart from untracked `tmp/`; local HEAD == `origin/main` == `1b4e255`.

## 68. Round 107: the fatal late hits follow a PIN at the native world-border clamp

### 68.1 The measurement

One dense live weak-wing run at the cap (`CHAITE_PROBE_DENSE_FRAMES=1`, `-maxticks 6000`,
`game-probe-dense6k-weak`) reproduced the §65.1 result **exactly** -- `ticks 4598`, `HITS 8`,
`boss damage 31`, `death True`, `outcome FailedAfterDeath`, `npc contact 3`, `shield rows 48`,
`dash-active 45` -- so the failure is deterministic and its per-tick data is available.

Locating every hurt tick by the drop in `player.life`:

```
tick   lifeDelta  phase                              wingTime  controlJump  controlDown
 905       69     fishron-wing-refill                    0        False        True
2433       89     fishron-wing-charge-horizontal         95        True         False
3264       73     fishron-wing-charge-horizontal         91        True         False
3792       83     fishron-wing-charge-descend           120        True         False
3918       99     fishron-wing-charge-ascend            130        False        False
4096       92     fishron-wing-charge-horizontal        130        False        False
4151       86     fishron-wing-charge-ascend            130        False        False
4209       41     fishron-wing-charge-descend           130        False        False
```

The last four hits are a **cluster**, and they share a signature: `wingTime` at maximum (130) -- the wing
is **fully charged** -- while `controlJump` is **False**, so the wing is *available but not applied*
(`Player.WingMovement` is gated on `controlJump`), and `vy` is frozen at `3.34` while `vx` is `0.00`.

### 68.2 The pin

Tracing the frames immediately before that cluster:

```
guc   pos x        pos y      plan.h  cL cR cU cD   vx      vy     wingTime
4060  640.0000     7867.55      -1    1  0  0  1   0.00    2.72      130
4064  640.0000     7882.44      -1    1  0  0  1   0.00    4.32      130
4068  640.0000     7898.96      -1    1  0  0  1   0.00    3.34      130
4072  640.0000     7912.31      -1    1  0  0  1   0.00    3.34      130
4076  640.0000     7925.65      -1    1  0  0  1   0.00    3.34      130   (charge-horizontal-dash)
4080  640.0000     7938.99      -1    1  0  0  0   0.00    2.32      130   (charge-horizontal)
```

**`x` is exactly `640.0000` and does not move for 30+ ticks while `plan.horizontal = -1` and
`controlLeft = True`** -- the plan is pressing *into* the boundary, so the engine pins the position and
zeroes the axis velocity. Across the whole 4599-tick run the player's `x` range is
**`640.0000 .. 5978.2360`**, and `min x` is exactly `640.0000`, which is precisely
`leftWorld + 640` -- the value `Player.BordersMovement` clamps to, exactly as documented at
`FishronWingScript.cs:620-624`: *"the engine does NOT turn the player around at that clamp: it pins the
position and zeroes that axis of the velocity, while the circuit goes on holding the outward input."*

Two further pins of the same shape were located earlier in the fight by a grounded-segment scan
(`wingTime == 0` and `vy >= 9.9` for >= 8 ticks): **ticks 3007..3065 (59 ticks)** and **ticks 3414..3489
(76 ticks)**. §"the hit at tick 3019" already records the x=640 pin, and §65.1 now shows the same pin
preceding the lethal cluster.

### 68.3 The open question, stated precisely

`ApplyArena` is supposed to prevent exactly this:

```csharp
var atLeftWall  = x <= _bandLeft;                              // :1257
var atRightWall = x >= _bandRight;                             // :1258
if (atLeftWall && horizontal <= 0) horizontal = 1;             // :1259
else if (atRightWall && horizontal >= 0) horizontal = -1;      // :1260
```

It is called unconditionally at `:515`, and it is the **last** writer before `output.Horizontal = horizontal`
at `:524` (only `DecideMovement` at `:535` runs after, and it does not move the axis when no policy is
configured). So at `x = 640` with `horizontal = -1` it **should** have produced `horizontal = +1` -- yet the
observed `plan.horizontal` is `-1`.

Therefore one of these is false, and it is testable:

1. `_bandLeft` is **not** 640. Its value is `worldLeft + BandEdgeMargin` with `worldLeft = 16f`
   (`TerrariaFacade.cs:989`) and `BandEdgeMargin = 260f`, which gives **276** -- so `atLeftWall` would be
   `640 <= 276` = **false**, and the guard never fires. That is the leading hypothesis and it is exactly the
   case the comment at `:1230-1235` claims is handled ("`_bandLeft` is `worldLeft + BandEdgeMargin` = 640"),
   i.e. **a stale constant in a comment that the code no longer satisfies**.
2. Or `PlayerSnapshot.Position.X` is not the same quantity as the clamped native `position.X`.

Note the band logic at `:651-665`: with `leftDistance <= rightDistance` it takes
`_bandLeft = worldLeft + 260`, and the `< 400f` fallback at `:661` can additionally reset the band to the
full `worldLeft..worldRight`. Either way `_bandLeft` sits far from 640, which is consistent with hypothesis 1.

### 68.4 Next step, and why no edit was made

The next step is a **single dense run that records `_bandLeft`/`_bandRight`** (or an equivalent
`FishronWingScript` diagnostic) alongside `player.Position.X`, so the guard's actual arithmetic is observed
rather than inferred. If hypothesis 1 is confirmed, the fix is to compare against the **native clamp**
(`worldLeft + 640`) rather than `worldLeft + BandEdgeMargin` -- but that must be **measured**, because
`FishronWingScript.cs:635-648` records that moving this edge inward was tried twice and both attempts scored
**far worse** (0 wins in 20 against a 10-in-20 baseline; hit rate raised from ~1 per 470 to ~1 per 380
ticks, shortening runs from 4000 to 3100), with the stated reason that removing the station desynchronises
the W cycle from the Boss's attack clock. **No edit was made this round**, so the tree is clean.

### 68.5 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean. The
  §64 velocity-normal fix (`a46bca3`) is untouched.
- **Measured:** 8 hits / 31 damage / death at 4598, bit-identical to §65.1; the last four hits cluster with
  `wingTime 130` (wing charged) and `controlJump False` (wing not applied); and `x` is pinned at exactly
  `640.0000` -- `leftWorld + 640`, the native border clamp -- for 30+ ticks before that cluster while the
  plan commands outward (`horizontal = -1`, `controlLeft = True`). `min x` over the run is `640.0000`.
- **Leading hypothesis (not yet confirmed):** `ApplyArena`'s `atLeftWall` test uses
  `_bandLeft = worldLeft + BandEdgeMargin = 276`, so it never fires at the 640 clamp, and the comment at
  `:1230-1235` asserting `= 640` is stale.
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 69. Round 108: the pinned-axis detector is REVERTED -- it made the fight worse

### 69.1 What was tried

§68.3 established that `_bandLeft` is `worldLeft + BandEdgeMargin` = `16 + 260` = **276**, while the engine
clamps the player at `leftWorld + 640` ≈ **640**, so `ApplyArena`'s `atLeftWall` test is `false` throughout
the **364 px** between them and the measured pin is never caught. The attempt was to detect the pin from its
**symptom** rather than from a predicted edge -- an axis that is commanded but does not move is blocked:

```csharp
var commanded = horizontal != 0;
var blocked = Math.Abs(player.Velocity.X) < 0.05f;
if (commanded && blocked) _pinnedHorizontalTicks++;
else _pinnedHorizontalTicks = 0;
if (_pinnedHorizontalTicks >= 2)
{
    horizontal = -horizontal;
    _pinnedHorizontalTicks = 0;
}
```

Two consecutive ticks were required because the first tick of a legitimate reversal has `vx ~ 0` while the
velocity crosses zero, and the flip itself clears the counter so the cost is bounded to one input flip per
episode.

### 69.2 The measurement: worse on every axis

```
                        §64 baseline (dense6k)   pinned-axis detector
ticks                          4598                  3760  (died 838 ticks EARLIER)
HITS                              8                     8
boss damage                      31                    90
death                          TRUE                  TRUE
npc contact                       3                    10
shield rows                      48                    47
dash-active ticks                45                    37
```

**Every axis is worse**: death 838 ticks earlier, damage nearly tripled (31 -> 90), and npc contacts more
than tripled (3 -> 10). Reverted; the tree is clean and the §64 fix is intact.

### 69.3 Why it failed, and what it does not tell us

`Math.Abs(player.Velocity.X) < 0.05f` does **not** isolate the pinned state. The player legitimately passes
through `vx ~ 0` at every direction reversal, and on the ground `|vx|` is small for long stretches under the
move-speed debuff (`FishronWingScript.cs:792-794` measures ground acceleration at about `0.08 px/tick^2`, so
a standstill persists for many ticks). So the detector fired in states that were **not** pins and reversed
the input while the player was merely slow -- which is exactly the class of timing disturbance that
`FishronWingScript.cs:635-648` warns about, where removing the station desynchronises the W cycle from the
Boss's attack clock.

Two conclusions, and the second is the important one:

1. **A velocity-magnitude test is the wrong instrument** for this pin. The usable discriminator is that the
   position itself does not change (`x` held at exactly `640.0000`) while input is commanded -- i.e. compare
   the **position** across ticks, not the velocity against a threshold.
2. **The pin is not established as the cause of the late death.** Detecting and breaking it made the fight
   decisively worse, which is evidence *against* "the pin causes the late hits" as a simple causal story.
   §68.1's correlation (the pin precedes the lethal cluster) may be a **symptom**: a charge pattern that
   forces the player to the edge is what produces both the pin and the hits, in which case breaking the pin
   merely moves the failure elsewhere -- which is what the numbers show.

This is the **fourth** perpendicular/pinning intervention this session to measure worse or neutral (§55,
§56.2/B, §57, and now §69). The pattern across all four is that the late-game failure is not caused by any
single local input decision.

### 69.4 Status

- **Edit reverted.** Tree clean apart from untracked `tmp/`; builds clean; the §64 velocity-normal fix
  (`a46bca3`) is intact and verified present (`rateA >= rateB` at `:1198-1199`).
- **Measured:** the pinned-axis detector gives `ticks 3760 / HITS 8 / damage 90 / death TRUE / npc contact 10`
  against the §64 baseline's `4598 / 8 / 31 / TRUE / 3` -- worse on every axis.
- **Refuted:** that breaking the horizontal pin improves the weak-wing fight, and that a velocity-magnitude
  threshold identifies the pin.
- **Next instrument (recorded, not yet built):** detect the pin by an **unchanged position** across
  consecutive ticks while input is commanded, which is what the measurement actually shows, and treat §68.1's
  pin-hit correlation as unproven causality.
- **Still not achieved:** zero hits on either loadout over a full fight. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 70. Round 109: unconditional lift on the ascend beat is REVERTED -- fifth local intervention to lose

### 70.1 The mechanism, confirmed

§69's frames showed the late hits happening with `wingTime` at its 130 maximum, which is only possible if the
wing is never *applied*. The gate is `controlJump`, and `Tick` publishes `output.Jump = vertical < 0`
(`FishronWingScript.cs:526`). So a beat that commands `vertical = +1` does not merely descend -- it **gates the
wing off entirely**.

The frames around the ascend hits prove which arm runs:

```
guc   phase                        plan(h, up, drop, jump)   ctl(J,U,D)          vx      vy     wingTime
3908  fishron-wing-charge-ascend   (-1, 0, 0, False)         (False,False,False)  0.00   +3.34   130
3918  fishron-wing-charge-ascend   (-1, 0, 0, False)         (False,False,False)  0.00   -3.50   130  <- hit
4145  fishron-wing-charge-ascend   (-1, 0, 0, False)         (False,False,False)  6.86   -3.47   130
4151  fishron-wing-charge-ascend   (-1, 0, 0, False)         (False,False,False) -4.50   -3.50   130  <- hit
```

`script.Vertical = +1` (from `plan.Drop = script.Vertical > 0`), which per the old `case 1` at `:826-828`
means `_chargeNormalVertical < 0` -- the normal pointed **down** while the beat was **ascend**, so the
fallback took `vertical = +1`, `controlJump` stayed false, the wing never ran, and `vy` sat frozen at `+3.34`
turning to `-3.50` only as the Boss overlapped. `vx = 0.00` throughout. **The wing was fully charged and
never applied.**

### 70.2 What was tried, and the measurement

The ascend beat was changed to take lift unconditionally (`vertical = -1`), on the reasoning that the worst
case is gaining altitude when the normal wanted to descend -- recoverable -- against a guaranteed loss of all
vertical mobility, which is not.

```
                        §64 baseline (dense6k)   unconditional ascend lift
ticks                          4598                     4488  (died 110 ticks EARLIER)
HITS                              8                        9
boss damage                      31                      123
death                          TRUE                    TRUE
npc contact                       3                       10
shield rows                      48                       55
dash-active ticks                45                       45
```

Again **worse on every axis** -- damage nearly quadrupled (31 -> 123) and npc contacts more than tripled
(3 -> 10). Reverted.

### 70.3 The accumulating negative result, and what it implies

This is the **fifth** local movement intervention this session to measure neutral or worse:

```
§55   personal-space branch gated on !inBeat          6 hits / 99 damage
§56.2 charge-normal deadband 0.2 -> exact zero        6 hits / 123 damage
§57   flip the charge-normal tie-break                identical (no-op)
§69   pinned-axis detector                            8 hits / 90 damage / death at 3760
§70   unconditional lift on the ascend beat           9 hits / 123 damage / death at 4488
                                                      baseline: 8 hits / 31 damage / death at 4598
```

Evaluated at the cap, **every single local change to the movement decision has made the outcome worse.** No
local input rule is the binding constraint. That is now a strong, repeatedly measured conclusion, and it
reframes the remaining work: the weak-wing failure is **structural** -- a timing/scheduling property of the
whole W cycle against the Boss's attack clock -- not a per-tick choice. It is exactly what
`FishronWingScript.cs:635-648` already warns about for the band edge ("removing the station desynchronises the
W cycle from the Boss's attack clock and costs more than the pinned frames ever did"), and §70.1's wing-gate
mechanism shows *why* the cost is so steep: any change to the beat schedule also changes when `controlJump`
is true, and `controlJump` is simultaneously the wing gate and the escape's only vertical authority.

### 70.4 Methodological consequence for acceptance

**Even the best current build is not an accepted run**: the §64 baseline dies at 4598 at the 6000-tick cap.
So under the acceptance rule this goal now carries ("survive to `-maxticks` without death"), **neither loadout
has any accepted run at all**, at any length. The 3000-tick figure (2 hits / 9 damage / no death) is
**provisional only** and must never be reported as acceptance.

### 70.5 Status

- **Edit reverted.** Tree clean apart from untracked `tmp/`; builds clean; the §64 velocity-normal fix
  (`a46bca3`) is intact (`rateA >= rateB` at `:1198-1199`).
- **Confirmed mechanism:** an ascend beat whose normal points down commands `vertical = +1`, which sets
  `output.Jump = false`, which gates the wing off, producing the observed `wingTime 130` with `vy` frozen.
- **Measured:** unconditional ascend lift gives `4488 / 9 / 123 / TRUE / 10` against the baseline's
  `4598 / 8 / 31 / TRUE / 3`.
- **Established by five independent measurements:** no local movement-rule change improves the weak-wing
  fight. The binding constraint is **structural** (W-cycle timing vs. the Boss's attack clock), not per-tick.
- **Still not achieved:** zero hits on either loadout over a full fight -- and no run currently *survives*
  the cap, so there is no accepted run to speak of. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 71. Round 110: the wing is gated off for 58% of every charge -- the structural measurement

### 71.1 The measurement

§70.3 concluded the weak-wing failure is structural rather than a per-tick choice. This quantifies exactly
how structural, over the whole 4599-tick dense baseline run:

```
total ticks                                 4599
charge ticks (phase starts fishron-wing-charge)  1625
  controlJump FALSE (= wing gated off)           944    58.1% of all charge ticks
  of those, wingTime > 0                         721    wing CHARGED but never applied

gated-off share, by charge beat:
  charge-descend       591 ticks   gated off 408   (69%)
  charge-horizontal    499 ticks   gated off 297   (60%)
  charge-ascend        487 ticks   gated off 216   (44%)
  charge-*-dash         48 ticks   gated off  23   (48%)

total wing time charged but never spent:  86 544
```

(Note the labels: `charge-horizontal` in this dump is the asc/desc variant reached when
`_chargeNormalVertical < 0`, which is why its count nearly matches `charge-ascend`.)

### 71.2 What it means

**The player's single most important defensive resource -- the wing -- is switched off for more than half of
every charge, and 86 544 units of flight time are charged but never spent.** The mechanism is §70.1:
`Tick` publishes

```csharp
output.Jump = vertical < 0;        // FishronWingScript.cs:526
```

and `Player.WingMovement` is gated on `controlJump`. So **`vertical = +1` does not mean "descend", it means
"descend with no wing"** -- the script cannot dive while retaining vertical authority. Because the charge
normal frequently points downward (it is selected from the player's own velocity at the lock, §64.2, and a
descending player selects a downward normal), and because the descend beat forces `vertical = +1` by
construction, the gate is shut through the longest charge phase (69%) and, of all things, through **44% of the
ascend beat itself** -- the very beat whose purpose is to climb.

This explains, in one fact, why **five independent local interventions all failed** (§70.3). Each of them
edited *which* vertical value a beat commands, but the value is doubly constrained: it sets the escape
direction **and** it is the wing's enable line. Any local edit therefore trades a correct escape direction for
a dead wing, or a live wing for a wrong escape direction, and the measurements show the trade is always net
negative. **The defect is that two independent concerns share one channel.**

### 71.3 What a real fix requires (recorded, not attempted)

The fix must **decouple the wing gate from the direction command** -- e.g. keep descent expressed through a
dedicated fast-fall control while `output.Jump` remains available to the wing, instead of deriving
`output.Jump` from the sign of `vertical` at `:526`. That is a change to the decision *encoding*, not to any
beat's choice, so it is not another local intervention and it is not contradicted by the five failures above.
It must still be validated live at the `-maxticks` cap; and `:635-648`'s warning stands, because changing when
`controlJump` is true still changes the W cycle's interaction with the Boss's attack clock.

### 71.4 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean; the
  §64 velocity-normal fix (`a46bca3`) is intact.
- **Measured:** the wing is gated off on **944 of 1625 charge ticks (58.1%)**, and on **721** of those the
  wing was charged (`wingTime > 0`) -- 86 544 units of flight time charged and never spent. The gate is shut
  on 69% of the descend beat and **44% of the ascend beat**.
- **Root cause, stated structurally:** `output.Jump` is derived from the sign of `vertical` (`:526`), so the
  direction command and the wing's enable line are the same channel. A downward escape choice necessarily
  disables the wing.
- **Explains:** all five failed local interventions (§70.3) -- each traded escape direction against wing
  availability on a shared channel.
- **Not attempted:** the decoupling, which is recorded at §71.3 as the next real step.
- **Still not achieved:** zero hits on either loadout over a full fight; no run survives the cap. The
  objective remains **active and incomplete**, and no native zero is claimed.

## 72. Round 111: keeping the wing on charge beats is REVERTED -- the sixth failure, and the pattern is now conclusive

### 72.1 The gate analysis, sharpened

Before the attempt, the wing gate
(`Player.cs:27001`: `wingsLogic > 0 && controlJump && wingTime > 0 && jump == 0 && velocity.Y != 0`) was
evaluated directly against every dense frame:

```
                       gate SATISFIED
all ticks         1377 / 4599  (29.9%)
charge ticks       646 / 1625  (39.8%)

why it fails on charge ticks:
  no controlJump                        669
  OK (gate satisfied)                   646
  no controlJump + wingTime 0           221
  no controlJump + vy 0                  52
  wingTime 0                              5
  other                                  32

`jump` counter on charge ticks: 0 on 1595 of 1625   (only the 15..1 ramp, 2 ticks each)
```

**This isolates the cause exactly: `controlJump` is the sole meaningful blocker** (944 ticks). Neither the
`jump` counter nor `wingTime` is the constraint -- the wing is charged and ready, and the script simply is not
raising `controlJump`.

### 72.2 What was tried, and the measurement

`Tick` publishes `output.Jump = vertical < 0` (`:526`), so the fix was to stop a charge beat from ever
commanding descent while airborne, at one point in `Tick`:

```csharp
if (vertical > 0 && !player.OnGround) vertical = -1;
```

Grounded frames are exempt so the takeoff that refills the bar still happens.

```
                        §64 baseline (dense6k)   keep-the-wing
ticks                          4598                  3646  (died 952 ticks EARLIER)
HITS                              8                     7
boss damage                      31                   137
death                          TRUE                  TRUE
npc contact                       3                     9
shield rows                      48                    44
dash-active ticks                45                    35
```

**Worse again.** It did reduce the hit count (8 -> 7) but more than quadrupled the damage (31 -> 137) and
killed the player 952 ticks earlier. Reverted.

### 72.3 The conclusive pattern

Six local interventions, all evaluated live at the `-maxticks` cap against the same baseline:

```
                                              HITS   damage   death tick
§64 baseline (committed, best)                   8       31       4598
§55   personal-space branch gated on !inBeat     6       99         --
§56.2 charge-normal deadband 0.2 -> zero         6      123         --
§57   flip charge-normal tie-break               8       31       4598   (bit-identical no-op)
§69   pinned-axis detector                       8       90       3760
§70   unconditional lift on ascend beat          9      123       4488
§71   keep the wing on charge beats              7      137       3646
```

**Not one of the six improves the fight, and the two that reduced the hit count (`§55`, `§71`) increased the
damage by 3-4x and killed the player sooner.** That inversion is the real signal: fewer contacts with far more
damage means the interventions are trading *many cheap grazes* for *few lethal connections* -- i.e. they are
changing **which** charges connect, not how many, and the baseline's configuration happens to pick the
survivable ones. This is a timing/scheduling property of the whole cycle, exactly as §71.2 argued, and it is
now confirmed by six independent live measurements rather than by one.

### 72.4 The honest conclusion for this goal

The remaining work is **not** a sequence of local rule fixes. Six of them have been tried and every one lost.
What the measurements support instead is a **scheduling** change -- the beat cycle and the lock-time normal
selection need to be derived together from the Boss's attack clock rather than patched per branch -- and that
is a redesign which cannot be validated within the remaining budget of this session at the
`-maxticks`-cap standard the objective now requires.

Under the goal's own acceptance rule (survive to the 6000-tick cap without death, `hits == 0` for **both**
loadouts), the state is:

- **weak wing:** no accepted run. Best is 8 hits / 31 damage **with death at 4598**.
- **strong wing:** one measurement only (2 hits / 18 damage at 3000 ticks); never run to the cap.
- **replay channel:** verified tick-perfect (0 differing ticks over 1199) -- the acceptance *instrument*
  works, which is a genuine deliverable of this session.
- **`hits == 0`:** not reached anywhere, at any length, on either loadout.

**No no-hit claim is made, and none may be inferred.**

### 72.5 Status

- **Edit reverted.** Tree clean apart from untracked `tmp/`; builds clean; the §64 velocity-normal fix
  (`a46bca3`) is intact (`rateA >= rateB` at `:1198-1199`).
- **Measured:** keep-the-wing gives `3646 / 7 / 137 / TRUE / 9` against the baseline's `4598 / 8 / 31 / TRUE / 3`.
- **Isolated:** `controlJump` is the sole blocker of the wing gate on charge ticks (944 of 1625); the `jump`
  counter is 0 on 1595 of 1625 and `wingTime` is available.
- **Established by six live measurements:** no local movement-rule change improves the weak-wing fight, and
  the two that reduce hit count trade cheap grazes for lethal connections.
- **Still not achieved:** zero hits on either loadout; no accepted run at the cap. The objective remains
  **active and incomplete**, and no native zero is claimed.

## 73. Round 112: what acceptance actually requires, and the fight's real shape

### 73.1 Acceptance does NOT require killing the boss

This matters because it had been implicitly treated as a two-sided objective. The harness verdict is:

```
tools/run-native-acceptance.ps1:221-225
if ($hits -eq 0 -and -not $death) { 'ACCEPTED: zero hits in the native engine.' }
else { "NOT ACCEPTED: {0} hit(s)." -f $hits }
```

and `docs/approach-2026-09-16-closed-loop-training.md:885` states the rule directly:
**"「无伤」验收从此只认 `hits == 0`，不区分「靠无敌帧穿过」与「完全没有接触」。"**

So acceptance is **`hits == 0` and alive**, evaluated at the `-maxticks` cap. Killing Duke Fishron is **not
part of it**, and it is in fact out of reach: from `result.json` the boss has **`lifeMax 78000`**, and across
4599 ticks of the baseline the player dealt **`bossDamage: 31`**. `outcome: test-time-limit` is therefore the
normal, expected end of a passing run -- not a failure. **The task is a survival-and-evasion problem, not a
damage race.** Every prior framing of the remaining work should be read that way.

### 73.2 The fight's real shape (measured)

```
boss width / height across the whole run: 150 x 100, unchanged  -> the Boss NEVER ENRAGES
player wet flag: False on all 4599 ticks                       -> the player is NEVER in water
arena: 399 tiles (groundLeft 1, groundRightExclusive 400), world 4200x1200 tiles
       three layers: ground 7958 plus wooden platforms at y ~7040 and y ~6080
player x range over the run: 640 .. 5978  (span 5338 px = 334 tiles)
player y range over the run: 6108 .. 7958

where the hits happen -- ai[0] is the Boss state, all 8 hits:
  hit@ 905  ai=[0,-300,  6,7]  vel=(  7.9, -6.7)   <- hover/setup
  hit@2433  ai=[1,  0, 18,1]   vel=( 14.7,  8.6)   <- charging
  hit@3264  ai=[1,  0, 19,1]   vel=(-15.1,  7.8)   <- charging
  hit@3792  ai=[1,  0, 11,4]   vel=( 17.0, -0.2)   <- charging
  hit@3918  ai=[1,  0, 21,8]   vel=( 14.9,  8.1)   <- charging
  hit@4096  ai=[1,  0, 21,1]   vel=(-12.8, 11.2)   <- charging
  hit@4151  ai=[1,  0, 18,3]   vel=( 16.9, -1.4)   <- charging
  hit@4209  ai=[0,  0,  0,5]   vel=(-14.9,  7.8)   <- hover/setup
```

**Seven of the eight hits occur in `ai[0] == 1`: the locked charge.** The eighth (tick 905) is a hover
contact. So the entire problem is the charge, and the geometry is fixed by the native hitboxes: Boss body
150x100, player 20x42, so a body connection needs `|dx| < 85` **and** `|dy| < 71`. The boss's own collision
half-height is 50, so clearing its body vertically needs on the order of `50 + 21 = 71 px` from its centre --
close to the ~85 px figure the earlier sections used for the horizontal clearance. Both are of the same order
and both exceed what the player creates before a 17 px/tick charge arrives.

Two things this rules out, which had been open questions:

- **Enrage is not a factor.** The boss stays 150x100 for the whole fight, so no late-fight size or speed
  change explains the lethal cluster. It also means the ocean-biome restriction (the concern behind the
  300-tile arena) is not being violated in a way that triggers enrage here.
- **Water is not a factor.** The monitor fixture runs with `oceanBasinFilled = false`
  (`tools/GameProbe.cs:5734`), and the player is never wet, so no liquid physics enters the problem.

### 73.3 What this leaves

The problem is now stated with nothing extraneous: **keep a 20x42 body more than 85 px horizontally or 71 px
vertically from a 150x100 body that charges in a straight line at 12.8-17.0 px/tick, eight times, while never
dying and never needing to win.** The boss is fully deterministic, the arena is flat with two platform rows,
and the replay channel is validated tick-perfect (§60.1), so this is a solvable evasion problem -- but §72.3
shows it is **not** solved by any of the six local input-rule changes tried, all of which were evaluated at
the cap and all of which lost. The remaining work is the cycle-level rescheduling described in §72.4.

### 73.4 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean; the
  §64 velocity-normal fix (`a46bca3`) is intact.
- **Clarified:** acceptance is `hits == 0` **and alive** at the cap; **killing the boss is not required** and
  is out of reach (78000 HP versus 31 damage dealt). `test-time-limit` is a normal passing end state.
- **Measured:** the boss never enrages (150x100 throughout); the player is never wet; **7 of 8 hits are during
  the `ai[0]==1` charge**; contact needs `|dx| < 85 && |dy| < 71`.
- **Ruled out:** enrage and water physics as contributors.
- **Still not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 74. Round 113: the charge aims at the player's centre, so the defect is the PRE-CHARGE position

### 74.1 The measurement

Vertical separation between the player's centre and the Boss's centre, on every charge tick (`ai[0] == 1`,
1334 sampled):

```
|dy|  min 0   p25 75   median 128   p75 211   max 936

charge ticks with |dy| <  71   (inside the vertical contact band)      313  (23%)
charge ticks with |dy| < 114                                          561  (42%)

closest approaches:
  guc=1302  |dy|=0  dy=  -0   wingTime=119
  guc=2195  |dy|=0  dy=  +0   wingTime=  0
  guc=4145  |dy|=0  dy=  +0   wingTime=130
  guc=3923  |dy|=1  dy=  +1   wingTime=130
  guc=2064  |dy|=1  dy=  -1   wingTime= 76
  guc=1659  |dy|=1  dy=  +1   wingTime= 94
  guc=3259  |dy|=2  dy=  +2   wingTime= 96
```

**`|dy| = 0` on charge ticks is not a coincidence, it is the lock.** `AI_069_DukeFishron` sets the charge
velocity to `Normalize(player.Center - center) * num7` (the NATIVE CHARGE LOCK), so at the lock the Boss's
centre, the player's centre and the charge direction are **collinear**: the *perpendicular* offset is
**exactly zero**, and the entire dodge is a pure race in which the player must manufacture 71 px of vertical
(or 85 px of horizontal) clearance before a body closing at 12.8-17.0 px/tick arrives. With 23% of charge
ticks sitting inside the 71 px vertical band, the player is regularly locked into a geometry where the escape
must be created from zero.

Player altitude profile (centre y): min 6129, p25 6775, median 7055, p75 7909, max 7979 against platforms at
~7040 and ~6080 and ground at ~7958 -- so the player largely works the platform/ground layers rather than
holding altitude.

### 74.2 What this pins down

Combined with §72.3 (six local interventions, all lost) and §73.2 (7 of 8 hits in the charge), the defect is
now located **before** the charge rather than during it:

- During a charge the player starts from **zero** perpendicular offset, so the escape needs the maximum rate
  the airframe can give, immediately, in the correct direction.
- The wing is gated on `controlJump` (944 of 1625 charge ticks, §72.1), and the direction command is the same
  channel as the gate (§71.2), so the required maximum-rate escape is routinely unavailable exactly when the
  race starts.
- Therefore no *during-charge* rule can fix it -- which is precisely what the six failures show -- because the
  race is already lost by the geometry at lock time.

**The fix must act during the hover, before the lock**: hold a position from which the eventual charge line
leaves the player already far enough off it, and already moving perpendicular to it. The Boss's pattern is
fully deterministic (fixed AI, no randomness, §"fixed AI"), and the hover states place it at height
`-300` (see the `ai` samples: `[0,-300,...]` before charges), so the safe pre-charge state is a known
function of the attack sequence rather than something to be discovered online.

### 74.3 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean; the
  §64 velocity-normal fix (`a46bca3`) is intact.
- **Measured:** on charge ticks `|dy|` is 0 at the closest approaches and below 71 on **313 of 1334 charge
  ticks (23%)**; below 114 on 561 (42%). Median `|dy|` 128.
- **Established:** the charge locks collinear with the player's centre, so the perpendicular escape starts
  from zero by construction; the dodge is a race the player usually enters with a gated wing.
- **Concluded:** the defect is in the **pre-charge (hover) positioning**, not in any during-charge rule --
  consistent with all six local during-charge interventions failing.
- **Still not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 75. Round 114: a hard, measured separation threshold separates every hit from every miss

### 75.1 The measurement

All 48 charge episodes of the dense baseline run were extracted (contiguous runs of `ai[0] == 1`), and for
each one the Boss-player centre distance was tracked for 45 ticks from the lock, recording the **minimum**
reached. The perpendicular offset from the charge line was also tracked.

```
perpendicular offset from the charge line, minimum over each episode:
   misses: min 0  median 0  max 0   (n=40)
   hits  : min 0  median 0  max 0   (n=8)
```

**Every single episode has a minimum perpendicular offset of exactly 0.** The locked charge line passes
through the player's centre in all 48 cases, confirming §74.1 -- there is no such thing as being "off the
line" when a charge locks. **The only variable is how far the player gets, along the line's normal, before the
Boss body arrives.**

Minimum centre distance per episode:

```
MISSES (40)                       HITS (8)
  lock 345   minDist 112            lock 871   minDist  74
  lock 929   minDist 151            lock 3781  minDist  86
  lock 403   minDist 179            lock 4191  minDist   7
  lock 3665  minDist 224            lock 4075  minDist  20
  lock 2237  minDist 237            lock 4133  minDist   7
  lock 1643  minDist 257            lock 3897  minDist  56
  lock 461   minDist 286            lock 3245  minDist  91
  lock 3009  minDist 292            lock 2415  minDist  38
  lock 1759  minDist 297
  lock 3419  minDist 332
  lock 755   minDist 349
  ... (all remaining misses >= 112)

            worst miss   = 112        worst hit = 91
```

**There is a clean gap: every hit has `minDist <= 91`, and every miss has `minDist >= 112`.** No charge above
~100 px of minimum centre separation ever connects, and none below ~91 px ever misses. That is a
**hard, measured evasion threshold** -- the sharpest result of this investigation, and it is consistent with
the native hitboxes (Boss half-width 85 plus player half-width 10 gives 95).

### 75.2 Why this reframes the remaining work

The objective reduces to a single quantified requirement:

> **Drive the minimum Boss-player centre distance above ~100 px on every one of the ~48 charges of a full
> fight.**

Nothing else matters -- not hit count, not boss damage (the boss is not killed and need not be, §73.1), not
which phase. And because the threshold is a property of the **trajectory**, not of any input, it also explains
why six local input-rule changes all failed and why two of them traded fewer hits for far more damage (§72.3):
each changed *which* charges fell below 100 px rather than lifting all of them above it.

The pre-charge positioning conclusion of §74.2 now has a concrete target: at the lock the separation is
**0**, so the player must generate >= 100 px of normal-direction travel inside a ~28-tick charge (episode
lengths measured at 28 ticks). At the wing's ~9.9 px/tick vertical and ~14.5 px/tick horizontal authority
(§61.1) that is **reachable but requires the escape to run at near-maximum rate from the first tick of the
charge** -- which is exactly when the wing is gated off (§72.1). The two measurements together identify the
mechanism of failure with no remaining ambiguity:

- separation must be built from 0 to >= 100 px in ~28 ticks (~3.6 px/tick sustained), and
- the wing is unavailable on 58% of charge ticks (§71.2), so the sustained rate is not delivered.

### 75.3 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean; the
  §64 velocity-normal fix (`a46bca3`) is intact.
- **Measured:** all 48 charge episodes have minimum perpendicular offset exactly 0 -- the locked line always
  passes through the player's centre. **Every hit has minimum centre distance <= 91 px and every miss
  >= 112 px**: a clean separation with no overlap.
- **Reduced the objective to one quantified requirement:** hold minimum Boss-player centre distance above
  ~100 px on every charge of a full fight.
- **Explains:** all six failed local interventions, and why two of them traded hit count for damage.
- **Still not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 76. Round 115: KEPT (mixed) -- the escape normal is chosen by the player's OFFSET, not its velocity

### 76.1 The defect this fixes

§75's three near-miss charges were inspected frame by frame. At the charge locked at tick **3781** the plan
commanded `h = -1` for the charge's whole length **while the Boss closed from the left**, so the script ran the
player straight down the approach axis:

```
t=3781 dist=280 dx=+280 dy=  -4  P.v=(-3.01,-6.48) J=True w=130  plan(h=-1,dr=0,j=True)
t=3786 dist=182 dx=+179 dy= -35  P.v=(-3.41,-6.74) J=True w=126  plan(h=-1,dr=0,j=True)
t=3791 dist=102 dx= +75 dy= -69  P.v=(-4.04,-7.24) J=True w=121  plan(h=-1,dr=0,j=True)
t=3794 dist= 87 dx= +36 dy= -80  P.v=( 3.85,-3.70) J=True w=118  plan(h=-1,dr=0,j=True)
```

`|dx|` fell 280 -> 36 and the centre distance bottomed at **86 px**, against the **112 px** that every miss
in that run achieved (§75.1). At the charge locked at tick **3245** the input produced **`vx = 0.00` for the
entire charge** -- no horizontal escape at all, and `minDist` 91.

The cause was §64's own rule. Choosing the normal by projecting the **player's velocity** carries the history
of the *previous* decision: a bad earlier choice becomes the reason to keep making it. The offset `(dx, dy)`
has no such feedback, and it is what actually decides whether the nearest approach clears the body.

### 76.2 The change

`LatchChargeNormal` now projects the **offset** instead:

```csharp
var rateA = normalAX * dx + normalAY * dy;   // was: normalAX * player.Velocity.X + ...
var rateB = normalBX * dx + normalBY * dy;
var normalX = rateA >= rateB ? normalAX : normalBX;
var normalY = rateA >= rateB ? normalAY : normalBY;
```

i.e. take the perpendicular that carries the player **further to the side it is already on**. **Kept.**

### 76.3 Measured result: better survival, more hits

```
                     velocity normal (§64)   offset normal (§76)
ticks                       4598                  5937      (+1339, +29%)
HITS                           8                     9
player damage taken          632                   694
death                       TRUE                  TRUE
npc contact                    3                    12
shield rows                   48                    75
dash-active ticks             45                    63      (+40%)
```

Per-hit detail:

```
velocity normal  hits at [905, 2433, 3264, 3792, 3918, 4096, 4151, 4209]   died at 4598
offset normal    hits at [958, 2150, 2281, 2676, 4141, 4201, 4964, 5399, 5573]   died at 5937
```

The late lethal cluster moved from **4598** out to **4964 / 5399 / 5573**: the run now survives 29% longer and
engages the dash 40% more often. But the hit count rose 8 -> 9, so **this is not an acceptance and it is not a
clean improvement** -- it trades one hit for a third more fight.

### 76.4 Honest status of this change

It is **kept** because it is the longest-surviving weak-wing configuration measured so far (5937 vs 4598) and
because it removes a defect that is now understood (an escape that ran down the approach axis), not because it
approaches `hits == 0`. Under the objective's own rule -- `hits == 0` **and** alive at the cap -- it **fails**,
as does every configuration tried. The weak-wing fight still needs the cycle-level rescheduling of §72.4, and
the separation threshold of §75.1 (>= ~112 px minimum centre distance on **every** charge) remains the
quantified target.

### 76.5 Status

- **Change kept** in `src/Chaite.Core/FishronWingScript.cs`; builds clean; verified fresh DLL
  (16:17:54 vs source 16:17:46).
- **Measured:** offset normal = `ticks 5937 / HITS 9 / 694 player damage / death TRUE / npc contact 12 /
  dash-active 63`, against velocity normal's `4598 / 8 / 632 / TRUE / 3 / 45`.
- **Improved:** survival +29%, dash engagement +40%, and the late lethal cluster pushed from 4598 to 5573.
- **Worsened:** hit count 8 -> 9.
- **Not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and incomplete**,
  and no native zero is claimed.

## 77. Round 116: the contact test is a RECTANGLE, two hits were projectiles, and the true rate improves

### 77.1 Correcting §75

§75 compared hits against misses using the **centre distance**. That is the wrong metric, and it produced a
false result: the offset run appeared to have a body hit at 260 px and a miss at 29 px, which "refuted" the
threshold. Both were artefacts of the same two mistakes:

1. **The native contact test is a rectangle, not a circle**: contact needs `|dx| < (150+20)/2 = 85` **and**
   `|dy| < (100+42)/2 = 71`. A centre distance of 239 px can still be a contact if `|dy|` is what is small.
2. **Two of the offset run's nine hits are PROJECTILES, not body contact.** `hurt-observations.jsonl` gives
   the source for every event, and it reports `kind: projectile, type: 384` at ticks 4137 and 4197
   (Sharknado sharks, base damage 25, actual return 58 and 51). They are **not** bubble projectiles and are
   **not** ignorable -- they cost 109 HP in that run, more than any single body hit. The previous "all hits
   are body contact" belief came from the baseline run, which genuinely had 8/8 body hits; the offset run
   does not.

Re-measured with the exact rectangle test, **every** body hit in **both** runs is inside the contact
rectangle, with no exceptions:

```
BASELINE       tick  dmg   |dx| (<85)      |dy| (<71)
                904   69   61.9 INSIDE     65.1 INSIDE
               2432   89   22.1 INSIDE     42.4 INSIDE
               3263   73   79.7 INSIDE     53.2 INSIDE
               3791   83   74.8 INSIDE     69.5 INSIDE
               3917   99    2.7 INSIDE     68.4 INSIDE
               4095   92   32.5 INSIDE     64.1 INSIDE
               4150   86   13.0 INSIDE      8.0 INSIDE
               4208   41   82.3 INSIDE     45.2 INSIDE

CHARGE TICKS (ai[0]==1): 1334   inside the contact rectangle: 48 (3.6%)
  closest charge tick: maxNorm 0.093 (|dx| 0.0, |dy| 6.6)
  5th-percentile charge tick: maxNorm 1.166

OFFSET         tick  dmg   |dx| (<85)      |dy| (<71)
                950   98   84.4 INSIDE     15.2 INSIDE
               2146   77   17.2 INSIDE     13.2 INSIDE
               2278   77   76.6 INSIDE     64.9 INSIDE
               2668   99   39.3 INSIDE     63.7 INSIDE
               4961   77   65.6 INSIDE     69.1 INSIDE
               5393   80   76.7 INSIDE      4.7 INSIDE
               5572   85   14.4 INSIDE      2.2 INSIDE
        (plus 2 projectile hits, not body contact)

CHARGE TICKS: 1808   inside the contact rectangle: 97 (5.4%)
  5th-percentile charge tick: maxNorm 0.968
```

Two useful consequences:

- The dodges are mostly **wide**: a hit needs the player to be inside a 170x142 rectangle around the Boss
  centre, and only **3.6% (baseline) / 5.4% (offset)** of charge ticks are. The failures are a small tail, not
  a systemic collapse -- so the fight is much closer to solvable than the "8 hits" headline suggests.
- §75.1's "clean 91/112 gap" was an artefact of the circle metric on a single run. The **correct** statement
  is that a hit occurs exactly when the rectangle test passes, which requires near-perfect precision on
  **both** axes simultaneously (baseline hit at tick 3917 had `|dx|` of just 2.7 px).

### 77.2 The offset normal does improve the hit RATE (dense confirmation)

The earlier offset run was launched **without `CHAITE_PROBE_DENSE_FRAMES=1`** and therefore recorded only 317
rows, too sparse to measure anything; the hurt ticks were not even present in the stream. It was re-run dense
(`game-probe-offsetdense-6000`) and reproduced the sparse run **exactly** -- `ticks 5937 / HITS 9 / boss
damage 126` -- which independently confirms the engine is deterministic run-to-run.

```
                              velocity normal (§64)   offset normal (§76/§77)
ticks                               4598                   5937
HITS                                   8                      9
body hits                              8                      7
projectile hits                        0                      2
NPC contacts                          12                     12
engagement (charge ticks)           1334                   1808
charge ticks inside contact rect    48 (3.6%)              97 (5.4%)
HITS PER 1000 TICKS                 1.74                   1.52
```

**Per tick of engagement the offset normal is safer (1.52 vs 1.74 hits per 1000), and it survives 29% longer,
but it accumulates one more hit in absolute terms and exposes the player to Sharknado projectiles that the
baseline never met.** It remains the best available weak-wing configuration and stays kept, but it is still
**not** an acceptance.

### 77.3 Status

- Tree clean apart from untracked `tmp/`; the `docs/` update in this entry is committed; builds clean.
- **Corrected:** §75.1's centre-distance threshold was an artefact (circle metric, single run). The native
  contact test is the rectangle `|dx| < 85 && |dy| < 71`, and **every** body hit in both runs satisfies it.
- **Discovered:** two of the offset run's nine hits are **projectile type 384 (Sharknado sharks)**, costing
  109 HP -- not bubble projectiles and not ignorable.
- **Measured:** only **3.6% / 5.4%** of charge ticks are inside the contact rectangle, so the failures are a
  small tail; the offset normal reduces hits **per tick of engagement** (1.52 vs 1.74 per 1000).
- **Confirmed deterministic:** the dense offset re-run reproduced the sparse run exactly (`5937 / 9 / 126`).
- **Not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and incomplete**,
  and no native zero is claimed.

## 78. Round 117: the shark band is REVERTED -- the eighth failure, and the ratchet is the whole problem

### 78.1 The shark finding, made precise

The two type-384 hits were taken at player **centres (3115.1, 7289.1)** (moving right) and
**(3309.0, 7273.3)** (moving left) -- both at platform altitude, in the same stretch of arena. The Boss was in
`ai[0] == 1` (charging) on **both** ticks, not in the Sharknado state, so these are **lingering static hazards
left behind by the `ai[0] == 3` phase** (which occupies 540 ticks of the run). The earlier `(3130, 7247)` and
`(3159, 7287)` figures were projectile origins I had inferred rather than verified: the stream carries no
projectile data, and those origins do not in fact agree with the contact frames. **The band was therefore
rebuilt from the measured player centres**, x 3020-3410 and y 7180-7400, and the rule was: while airborne
inside that band, climb out of the top (the arena floor is ~680 px further down, so climbing needs far less
room than dropping; the refill guard takes precedence because an empty bar cannot climb).

### 78.2 The measurement -- it worked exactly as designed, and it still lost

```
                     offset normal (§76)   + shark band (§78)
ticks                      5937                 5543
HITS                          9                   10
body hits (npc 370)           7                    6
projectile hits (384)         2                    4
death                      TRUE                 TRUE
npc contact                  12                   14
dash-active ticks            63                   59

projectile hits, offset normal : ticks 4137 (58 HP), 4197 (51 HP)   = 109 HP
projectile hits, + shark band  : ticks 4883 (47), 5043 (58), 5145 (36), 5185 (38) = 179 HP
```

**The rule did precisely what it was built to do: the two original shark hits at 4137 and 4197 are gone, and
no hit occurs anywhere near that corridor.** But **four new shark hits appeared at 4883-5185**, costing more
than the two it removed (179 vs 109 HP), and body contacts rose 12 -> 14. Reverted.

**This is the eighth consecutive local intervention to lose**, and the mechanism is now unmistakable.

### 78.3 The ratchet, stated plainly

```
                                              HITS   damage   death tick
§64 baseline (velocity normal)                   8       31       4598
§55   personal-space gated on !inBeat            6       99         --
§56.2 charge-normal deadband 0.2 -> zero         6      123         --
§57   flip charge-normal tie-break               8       31       4598   (bit-identical no-op)
§69   pinned-axis detector                       8       90       3760
§70   unconditional lift on ascend beat          9      123       4488
§71   keep the wing on charge beats              7      137       3646
§76   offset-based charge normal (KEPT)          9      694*      5937   (*player HP, not boss)
§78   shark-band avoidance                      10        ?       5543
```

Every intervention displaces the failure rather than removing it. Moving the escape timing moves *which*
charge connects; avoiding the shark corridor moves *where* the sharks connect; forcing the wing open changes
*grazes into lethal connections* (§72.3). The trajectory has roughly a fixed budget of exposure, and a local
rule can only re-spend it. That is the signature of a system whose failure is **scheduled**, not
**positional** -- exactly the conclusion §72.4 reached, now confirmed by eight measurements instead of one.

### 78.4 Status

- **Edit reverted.** Tree clean apart from untracked `tmp/`; builds clean; the kept §76 offset normal is
  intact (`normalAX * dx` at `:1210`).
- **Measured:** the shark band removed the two original type-384 hits and introduced four new ones
  (179 HP vs 109 HP), with body contacts 12 -> 14 and total hits 9 -> 10.
- **Established by eight live cap-length measurements:** no local movement rule improves the weak-wing fight;
  each displaces the failure instead of eliminating it.
- **Still not achieved:** `hits == 0` on either loadout at the cap. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 79. Round 118: the offset normal REGRESSES the strong wing -- reverted; the strong wing now survives the cap

### 79.1 The regression, isolated to one variable

Round 116 kept the offset-based charge normal (§76) on the strength of a weak-wing measurement alone. The
strong wing had never been re-measured against it. It was, and **it regresses badly**:

```
                                        offset normal (§76)   velocity normal (§64)
strong wing, maxticks 6000   ticks           5341                  6000
                             HITS               9                    11
                             death            TRUE                  FALSE
                             outcome   FailedAfterDeath        test-time-limit
strong wing, maxticks 11000  ticks           5341                 11000
                             HITS               9                    12
                             death            TRUE                  FALSE
                             npc contact        9                     9

weak wing (for contrast)     ticks           5937                  4598
                             death            TRUE                  TRUE
```

The strong wing dies at **5341 with the offset normal at both tick limits**, and **survives the full 11000
with the velocity normal**. This is a single-variable comparison -- the only edit reverted was the two
projection lines in `LatchChargeNormal` -- so the offset projection is the cause.

### 79.2 The cause

The two loadouts respond to the same rule in **opposite directions**, and the reason is their vertical
mobility. The offset normal sends the escape to the side the player already is, which adds **vertical**
travel; the strong wing (`wingsLogic 26`, `wingTimeMax 180`) converts that into much more altitude than the
weak wing (`wingsLogic 6`, `wingTimeMax 130`) can. So the rule that rescues the weak wing by removing the
"escape down the approach axis" defect (§76.1) **over-commits the strong wing vertically** and kills it. The
weak wing improves (4598 -> 5937, still dying); the strong wing goes from surviving to dead.

**§76 is reverted.** The velocity normal (`§64`, `a46bca3`) is restored, and it is the configuration under
which the **strong wing satisfies the survival half of the objective**.

### 79.3 What this changes about the objective's status

```
objective acceptance = hits == 0 AND alive at the -maxticks cap

strong wing, velocity normal, cap 6000:
   ticks 6000   death FALSE   outcome test-time-limit   HITS 11   npc contact 8
   -> SURVIVES the cap.  Fails only the hits == 0 half.

weak wing:
   best alive-at-cap run: none.  Every configuration measured dies before 6000.
```

So the strong wing is **half-accepted**: it meets the survival criterion at the cap and needs only the hit
count driven to zero. The weak wing fails both halves with every configuration tried. No run on either
loadout has reached `hits == 0`, and **no no-hit claim is made**.

### 79.4 Status

- **§76 reverted** (offset normal -> velocity normal); builds clean; verified fresh DLL; the restored line is
  `normalAX * player.Velocity.X + ...` at `:1210`.
- **Measured (single variable):** strong wing at cap 6000 = `6000 / 11 hits / death FALSE / test-time-limit`
  with the velocity normal, versus `5341 / 9 / TRUE / FailedAfterDeath` with the offset normal. At cap 11000:
  `11000 / 12 / FALSE` versus `5341 / 9 / TRUE`.
- **Established:** the two loadouts respond to the charge-normal rule in opposite directions because of their
  vertical mobility, so the rule must be **loadout-aware** -- a single global projection cannot serve both.
- **Achieved:** the strong wing now survives the 6000-tick cap without death.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 80. Round 119: a UTF-16 source corruption invalidated four runs, and the controlled experiment that settles §79

### 80.1 The tooling trap

While attempting a loadout-aware charge normal, the source file
`src/Chaite.Core/FishronWingScript.cs` was overwritten in **UTF-16** (leading bytes `FF FE`) by a PowerShell
`>` redirect. MSBuild compiled it without complaint, so **every run launched from that file was testing
garbage** -- the file grew from 76124 to 152130 bytes and its content was no longer the reviewed source. Four
runs were affected (`aware-weak-6k`, `aware-weak-6k-b`, `aware-strong-6k`, `forceoff-weak-6k`).

The tell was that two runs of **the same build hash** gave wildly different results (`2666` versus the
expected `5937`), which is impossible for a deterministic engine. **A hash mismatch between runs that should
be identical is a build-integrity alarm, not a behavioural finding.** The file was restored with
`git checkout` and verified as valid UTF-8 (`75 73 69 6E`, 76124 bytes) with a clean `git status` before any
further measurement.

### 80.2 The controlled experiment

The §79 question -- does the charge-normal projection help or hurt each loadout -- was then settled with a
**single build and a single variable**, rebuilding commit `e484e3b` (the offset projection) exactly and
running both routes from it:

```
                    weak wing (Fairy)      strong wing (Fishron)
e484e3b (OFFSET)    5937 / 9 hits / TRUE    5341 / 9 hits / TRUE
9f6a984 (VELOCITY)  4598 / 8 hits / TRUE    6000 / 11 / FALSE  <- survives the cap
```

Both numbers for `e484e3b` reproduce the earlier §76 and §79 records exactly, so §79's conclusion stands and
is now confirmed by a clean controlled run: **the offset projection helps the weak wing survive longer
(4598 -> 5937) but kills the strong wing (survives -> dead at 5341).** The committed state `9f6a984`
(velocity projection for both) is the only configuration under which the **strong wing survives the 6000-tick
cap**, so it is kept.

### 80.3 Why the projections differ at all

The two projections are **not** equivalent, contrary to an assumption made while investigating. At a real
lock (`game-probe-offsetdense-6000`):

```
 t=  345  dx= -443.7 dy= +209.9  vel=( 0.00,-3.68)  offset(0.00,0.00)->B   vel(+3.32,-3.32)->A   DIFFERENT
 t=  403  dx=  +61.7 dy= -225.5  vel=(+3.01,-7.48)  offset(0.00,0.00)->A   vel(+0.93,-0.93)->A   SAME
 t=  461  dx= +344.7 dy=   -5.8  vel=(+6.76,-9.91)  offset(0.00,0.00)->A   vel(-9.80,+9.80)->B   DIFFERENT
 t=  519  dx= +243.4 dy= +181.0  vel=(+6.76, 0.00)  offset(0.00,0.00)->A   vel(-4.03,+4.03)->B   DIFFERENT
```

**Both offset dot products are identically zero at every lock** -- because the offset is parallel to the aim
by construction, the very fact recorded in §64. So the offset projection does **not** discriminate at all: it
always selects normal A, and the `>=` is deciding on floating-point noise. The **velocity** projection is the
one that actually discriminates. This also means §64's original claim ("the offset is parallel to the aim, so
the dot products are identically zero, therefore project the velocity") is exactly right, and §76's
"offset projection" was a mischaracterisation: it reverted the choice to a constant.

### 80.4 Status

- **Source restored** to the committed `9f6a984` state: valid UTF-8, 76124 bytes, clean `git status`, rebuilt,
  and re-verified (strong wing `6000 / 11 / death FALSE / test-time-limit`).
- **Invalidated:** four runs launched from the UTF-16 file (`aware-weak-6k`, `aware-weak-6k-b`,
  `aware-strong-6k`, `forceoff-weak-6k`) -- their results must not be cited.
- **Established by controlled experiment (one build, one variable):** offset projection = weak `5937/9/TRUE`,
  strong `5341/9/TRUE`; velocity projection = weak `4598/8/TRUE`, strong `6000/11/FALSE`.
- **Established:** the offset projection is a **constant** (normal A), because both offset dot products are
  identically zero at every lock; only the velocity projection discriminates.
- **Achieved:** the strong wing survives the 6000-tick cap without death (committed state).
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 81. Round 120: the contact mechanism, read frame by frame on the loadout that survives

### 81.1 The comparison is complete

```
                         weak (Fairy)          strong (Fishron)
velocity projection      4598 / 8 / TRUE      6000 / 11 / FALSE   <- survives the cap
offset projection        5937 / 9 / TRUE      5341 / 9  / TRUE
```

Both were re-verified this round on the committed build (`velnorm-weak-6k` = `4598 / 8 / TRUE`), so the table
is complete and consistent. The velocity projection is kept: it is the only one in which the strong wing
survives, and §80.3 showed the offset projection is a constant that does not discriminate at all.

The strong wing is the tractable target -- it already satisfies survival, so its remaining work is purely the
hit count.

### 81.2 Its hits are not grazes, they are centre crossings

The strong wing's four body hits, with the native margins (`|dx| < 85`, `|dy| < 71`):

```
  tick   dmg   |dx|   |dy|
   2963   64    25.1   41.7
   3439   76    12.9   32.4
   4745   92    51.1   29.5
   5396   77    25.6    3.6
```

and frame by frame around tick 5396 (the shallowest of the four):

```
 t=5392  P=(6213.6,7607.2) v=(+5.74,-2.00)  dx= +23.7  dy= -11.0
 t=5393  P=(6218.7,7605.6) v=(+5.02,-1.60)  dx= +12.4  dy=  -7.9
 t=5394  P=(6223.0,7604.4) v=(+4.34,-1.20)  dx=  +0.4  dy=  -4.5   <- |dx| 0.4, |dy| 4.5
 t=5395  P=(6226.7,7603.6) v=(+3.70,-0.80)  dx= -12.3  dy=  -0.6
 t=5396  P=(6229.8,7603.2) v=(+3.09,-0.40)  dx= -25.6  dy=  +3.6   <- the hit frame
 t=5397  P=(6225.3,7599.7) v=(-4.50,-3.50)                            (knockback)
```

At the contact the two centres are **4.5 px apart vertically and 0.4 px horizontally** -- the player passes
essentially *through* the Boss's centre. The same pattern holds at tick 2963 (`dx -25.1, dy -41.7` after
crossing `dx +0.7` two frames earlier) and at 3439 and 4745. The escape is working before and after the
crossing -- `dy` is accumulating at roughly 6 px/tick at 5394 and reaches 40+ px two frames later -- but the
**perpendicular separation passes through zero at the crossing instant**, and the contact test needs only
`|dy| < 71` *while* `|dx| < 85`. The player is not caught by a bad direction or a stalled escape; it is caught
because the two bodies are **co-located exactly when the Boss passes through the player's line**.

This is the geometric consequence of §74/§75 taken to its end: the lock aims at the player's centre, the
perpendicular offset starts at zero, and a straight charge necessarily brings the Boss's centre back through
the player's position unless the player has already left the corridor by more than 71 px vertically **before**
the crossing. The measured gap to safety is small at these frames (`|dy|` of 4.5 and 3.6 px at the crossing)
but it is not closable by tuning the escape rate, because the crossing instant is set by the Boss's
deterministic path and the required displacement is perpendicular to a motion the player is already making at
near maximum rate.

### 81.3 Status

- **Measured:** `velnorm-weak-6k` = `4598 / 8 / TRUE`, completing the 2x2 comparison; the strong wing's body
  hits are `2963`, `3439`, `4745`, `5396`, all deep inside the contact rectangle.
- **Established (frame-level):** each body hit occurs at the instant the Boss's centre crosses the player's
  axis, with `|dy|` at the crossing between 3.6 and 4.5 px -- the bodies are co-located, so the escape's
  accumulated separation is momentarily irrelevant.
- **Consequence:** the remaining strong-wing work is not an escape-rate or direction tuning problem; it needs
  the perpendicular separation to be non-zero **before** the crossing, i.e. a positional commitment made
  during the hover, as §74.2 and §75.2 concluded.
- **Achieved:** the strong wing survives the 6000-tick cap without death.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 82. Round 121: the co-location lift is REVERTED -- the ninth failure, and a rule that never fired

### 82.1 The hypothesis and the change

§81.2 established that every strong-wing body contact happens at the instant the Boss's centre crosses the
player's, with the two centres 3.6-4.5 px apart. The pre-hit traces then showed the crossing is **slow and
spendable**: `|dy|` sits under 12 px for 8-12 ticks before each contact while the charge is in flight for ~28,
and the measured escape rate through that window is only 0.4-2.4 px/tick against a wing that climbs at ~9.9.
So a rule was added to `Tick` after `ApplyArena`: **while charging, airborne, with `|dy| < 24` and the bar able
to pay, command a climb** (`CoLocationBand = 24f`).

### 82.2 It never fired -- and the counter proves it

The rule was built and the DLL hash was **verified to match the tested run**
(`EAE7B19A9FDF20E0` in both `artifacts/game-probe-coloc-strong-6k/Chaite.Core.dll` and the build output), so
the tested binary really contained the rule. The result was nevertheless **bit-identical to the baseline**:

```
                          baseline (§81)   co-location lift
ticks                          6000              6000
HITS                             11                11
boss damage                      72                72
death                         FALSE             FALSE
npc contact                       8                 8
```

A diagnostic counter (`DiagnosticCoLocationLiftCount`) was then added and read back, because the phase name
`fishron-wing-colocation-lift` never appears anywhere in the 5761 recorded plans (16 distinct phases, none of
them this one). The counter reads **0**, confirming the branch is **dead in practice**, even though a
post-hoc count over the same stream showed 67 charge ticks where the conditions appeared to hold
(`|dy| < 24` on 148, plus `wingTime > 20` on 124, plus `plan vertical >= 0` on 85).

The discrepancy is a **pre-update versus post-update sampling artefact**: the rule evaluates against the
snapshot the engine hands the planner *before* the frame's player update, while the probe's
`boss-observations.jsonl` records state *after* that update. At the separations in question (a few pixels) that
difference is larger than the band being tested. **Any future rule targeting near-contact geometry must be
validated with an in-script counter, not with a post-hoc recomputation over the probe stream** -- this round
spent most of its budget learning that.

### 82.3 Status

- **Edit reverted.** Tree clean apart from untracked `tmp/`; source valid UTF-8 (`75 73 69 6E`); builds clean;
  the committed velocity projection is restored (`normalAX * player.Velocity.X` at `:1210`).
- **Measured:** the co-location lift produced `6000 / 11 / FALSE / 8 contacts`, identical to the baseline, and
  its diagnostic counter read **0** -- the branch never executed.
- **Achieved:** the strong wing survives the 6000-tick cap without death.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 83. Round 122: KEPT -- the co-location lift works, and it must be gated to the strong wing

### 83.1 The blocker was the latch, not the sampling

§82 concluded that a post-update sampling offset explained why the co-location lift never fired.
**That was wrong.** Re-testing the same condition against the stream at shifts of 0, -1 and -2 ticks all gave
**67** matching ticks, so the pairing was not the problem. The real blocker was the clause
`_chargeNormalVertical <= 0`: **the latched charge normal was positive throughout the window**, so the rule
was suppressed on every tick it was meant to act on.

Removing that clause alone made the rule fire **63 times** in the strong-wing run -- which matches the 67
post-hoc ticks almost exactly -- and produced a real improvement. **The lesson stands in a corrected form:
validate near-contact rules with an in-script counter, and be suspicious of a latch whose value was never
measured.**

### 83.2 The mechanism is now validated at the cap

The four strong-wing body hits §81.2 identified are **all eliminated**:

```
  previously  2963, 3439, 4745, 5396   ->  ELIMINATED
  now         5099, 5518, 5755, 5874
```

and the strong wing's numbers improve on every measure while still surviving:

```
                          baseline (§81)   co-location lift
ticks                          6000              6000
HITS                             11                 8
npc contact (body hits)           8                 5
player damage taken             638               523
death                         FALSE             FALSE
outcome                 test-time-limit   test-time-limit
```

### 83.3 It must be gated to the strong wing

Applied to both loadouts, the rule **regresses the weak wing**:

```
gated to strong only:
  strong   6000 / 8 hits / death FALSE / 5 contacts     (improved from 11 / FALSE / 8)
  weak     4598 / 8 hits / death TRUE / 3 contacts      (bit-identical to baseline)

ungated (both loadouts):
  strong   6000 / 8 / FALSE / 5                          (same improvement)
  weak     2224 / 7 / TRUE, firing 76 times in a 1986-tick life   (regression)
```

This is the **§79 pattern a third time**: the same vertical-commitment rule that helps the strong wing costs
the weak wing more than the separation buys, because the Fairy wing's budget is 130 ticks against the Fishron
wing's 180. The committed rule is therefore conditioned on
`input.Route == FormulaRoute.FishronStrongWingsDash`.

### 83.4 Status

- **KEPT** in `src/Chaite.Core/FishronWingScript.cs`; source valid UTF-8; builds clean; verified fresh DLL.
- **Measured (strong, cap 6000):** `6000 / 8 hits / death FALSE / 5 contacts`, against the baseline's
  `6000 / 11 / FALSE / 8`; all four of §81.2's body-hit ticks eliminated; the phase fires 63 times.
- **Measured (weak, cap 6000):** `4598 / 8 / TRUE / 3 contacts` -- bit-identical to baseline, confirming the
  gate is inert for the Fairy wing.
- **First intervention in nine to improve the fight.** It is still **not** an acceptance: 8 hits remain
  (4 body + 4 projectile).
- **Corrected:** §82.2's sampling-offset explanation was wrong; the blocker was the unmeasured
  `_chargeNormalVertical` latch.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 84. Round 123: the remaining hits are classified, and the wall-unpin fix is REVERTED

### 84.1 The eight remaining strong-wing hits have two distinct causes

With §83's co-location lift in place the strong wing's eight hits split cleanly:

```
  4 PROJECTILE, type 384 (shark), damage 61/33/43/43, at ticks 1852, 2032, 2378, 2418
  4 BODY, type 370, damage 95/73/83/92, at ticks 5099, 5518, 5755, 5874
```

**The projectile hits are incidental.** At each one the player is at full cruise (`vx` 7.9-8.0) and simply
runs into a lingering shark; they cost 180 HP total and are not worth a dedicated rule.

**The body hits are wall traps.** Three of the four (5518, 5755, and 5874 one tick later) catch the player at
`x = 640.0` with `vx = 0.00` for six or more consecutive ticks while `plan.horizontal` is still `-1`, with the
boss `ai` timer at 19-23 -- mid-charge. The player is motionless and the charge connects for free at 73-83
damage. The fourth (5099) is a centre crossing at altitude 4589, the §81.2 mechanism again.

### 84.2 Why the obvious fix does not work

`ApplyArena`'s band-edge test cannot fire at `x = 640`: `_bandLeft` is
`worldLeft + BandEdgeMargin = 16 + 260 = 276`, which is **364 px inside** the position the player is pinned
at. (The comment at the head of `ApplyArena` claims `_bandLeft` *is* 640; that is stale -- 640 is the arena's
constructed left edge, not this constant.)

So a wall-unpin rule was added using the engine's own stuck signal plus `arena.ClearanceLeft/Right` to locate
the real obstruction: while `|vx| < 0.5`, if the command is into a wall within 56 px, reverse it. It is
**logically sound and measured no effect at all**:

```
                        §83 (lift only)      + wall unpin
strong    ticks/hits/death/contacts   6000 / 8 / FALSE / 5    6000 / 8 / FALSE / 5
weak      ticks/hits/death/contacts   4598 / 8 / TRUE  / 3    4598 / 8 / TRUE  / 3
```

**The branch never fired, and the frames show why.** The motionless player at `x = 640` with `vx = 0.00` is
not pressing into the wall -- it is in **knockback**. In every recorded case the velocity arrives at
`+4.50, -3.50`, which is exactly **53% of 8.5**, the native knockback fraction, and the plan's horizontal has
already flipped to `+1`. The command is not being absorbed; the player was simply still moving left when the
wall stopped it, and then the hit converted the motion into knockback. There is no pinned-input state to
detect, so the rule has nothing to act on.

### 84.3 Status

- **Reverted.** The rebuilt DLL is **hash-identical** to the §83 build (`B4DA86A04A015A66`), so §83's
  verified numbers stand unchanged and the wall-unpin code is gone.
- **Measured:** the wall unpin produced `6000 / 8 / FALSE / 5` (strong) and `4598 / 8 / TRUE / 3` (weak),
  identical to §83 on both loadouts, and its branch never fired.
- **Established (frame-level):** the strong wing's 8 hits are 4 incidental shark contacts at cruise speed
  (180 HP total) and 4 body hits, three of which are wall traps at `x = 640` where the velocity is
  `+4.50, -3.50` knockback rather than a pinned command.
- **Established:** `_bandLeft = 276` while the player pins at `x = 640`, so the band-edge guard in
  `ApplyArena` is inert for this arena.
- **Ten interventions attempted; one (§83) improves the fight.** The best achieved is the strong wing at
  `6000 / 8 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 85. Round 124: the wall-approach turnaround is REVERTED -- `ClearanceLeft` does not describe this wall

### 85.1 The rule, and the proof that it is dead

§84 established that the strong wing's three remaining wall traps catch the player at `x = 640.0` with
`vx = 0.00` for six or more ticks, and that `ApplyArena`'s band guard is inert there because
`_bandLeft = 276`. §84's own note says "Do not reinstate it without a native trace that shows the player
pinned at the edge while a charge arrives"; **that trace now exists**, so the early turnaround was reinstated:

```csharp
var wallLeftX  = player.Center.X - arena.ClearanceLeft;
var wallRightX = player.Center.X + arena.ClearanceRight;
if (player.Center.X - wallLeftX <= WallApproachMargin && horizontal < 0) horizontal = 1;
else if (wallRightX - player.Center.X <= WallApproachMargin && horizontal > 0) horizontal = -1;
```

with `WallApproachMargin = 220f`. **It never fired.** The proof is stronger than a counter this time: the
plan trajectory is **bit-identical to §83 across all 5999 common ticks**, with zero differing positions or
commands.

```
common ticks: 5999
ticks where position/command differ: 0
```

So `arena.ClearanceLeft` is not the distance to the `x = 640` boundary. Whatever the probe populates those
fields with for this fixture, it is not the obstruction the player actually collides with -- and because the
branch was gated on it, the branch was unreachable. The observed left limit (min `x` over the fight is
exactly `640.0`, against a maximum of `6085.3`) has to come from the arena's construction, not from the
snapshot's clearance fields.

### 85.2 Why the physical bound cannot simply be hardcoded here

The obvious substitute -- reverse on `x < _arenaLeft + margin` using the measured `640` -- was **not**
attempted, and the reason is §78. An earlier revision reversed 120 px early from the *band* and measured no
difference; the note that removed it argues the player is not trapped at the edge. **That argument is now
disproved** (§84 shows three traps at the wall), so an early turn is worth re-testing -- but it must be
re-tested as a **cycle-level** change, because moving where the turnarounds happen moves the whole W cycle
relative to the Boss's attack clock, which is exactly the timing §78 records as expensive. A single build is
not enough evidence either way, and this round's budget went to establishing that the clearance-based form is
dead rather than to that search.

### 85.3 Status

- **Reverted**; the rebuilt DLL is again hash `B4DA86A04A015A66`, identical to the §83 build, so §83's
  verified numbers stand and the dead code is gone.
- **Measured:** the wall-approach turnaround produced `6000 / 8 / FALSE / 5` (strong) and `4598 / 8 / TRUE / 3`
  (weak), identical to §83 on both loadouts.
- **Established (bit-exact):** the new branch changed **nothing** -- 0 of 5999 common ticks differ in position
  or command -- so `arena.ClearanceLeft/Right` do not locate the `x = 640` obstruction for this fixture.
- **Disproved:** the claim recorded in `ApplyArena` that the player "is not trapped at the edge either". The
  player reaches exactly `x = 640.0` and is charged there three times in the §83 run. An early turnaround is
  therefore still an open avenue, but only as a cycle-level change (§78).
- **Eleven interventions attempted; one (§83) improves the fight.** The best achieved is the strong wing at
  `6000 / 8 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 86. Round 125: KEPT -- widening the turnaround margin takes the strong wing to 2 hits and the weak wing to survival

### 86.1 Why the clearance-based form could never work

`ReadArena` computes `ClearanceLeft = ScanHorizontal(...) * 16f` with `maxHorizontal = 150`
(`TerrariaFacade.cs:2657-2665`), and `ScanHorizontal` (`:2780-2791`) returns a tile count **capped at 150**, so
`ClearanceLeft` **saturates at 2400.0** everywhere in the arena interior. §85's `<= 220` test could therefore
only ever fire within 220 px of a tile the scan recognizes as *full solid*.

That is not the obstruction. `min x` over the fight is exactly **640.0** while the world's own left edge is
tile 1 (`x = 16`), so the scan reports that position as open air. Whatever stops the player at 640 is
invisible to the tile scan, which is why §85's branch was bit-exactly dead.

### 86.2 The fix: move the turnaround, do not detect the wall

`ApplyArena`'s band guard is kept, but the turnaround is now placed `PinnedWallMargin = 640f` **inside** the
band edge:

```csharp
var atLeftWall  = x <= _bandLeft + PinnedWallMargin;    // 276 + 640 = 916
var atRightWall = x >= _bandRight - PinnedWallMargin;
```

This moves the turn to `x = 916`, clear of the 640 trap, and leaves a **4920 px** corridor. No clearance
measurement is involved, so the saturation problem cannot recur.

### 86.3 Result: both loadouts now survive the cap

```
                          §83 baseline        margin 640          margin 900
strong  ticks/hits/death/contacts   6000/8/FALSE/5    6000/2/FALSE/3     6000/8/FALSE/9
weak    ticks/hits/death/contacts   4598/8/TRUE/3     6000/4/FALSE/5      (not run)
```

**This is the largest single improvement of the session.** The strong wing goes from 8 hits to **2**, and the
weak wing -- which previously **died at tick 4598 and never reached the cap** -- now **survives all 6000 ticks**
with 4 hits. All four projectile hits in the strong wing are gone as well.

`margin 900` is a clear **regression** (2 → 8 hits, contacts 3 → 9): a larger margin moves the turnaround
further and thereby shifts the whole W cycle against the Boss's attack clock, exactly the timing §78 records as
expensive. The optimum is therefore local and near 640, and larger values must not be assumed better.

### 86.4 The two remaining strong-wing hits

Both are at the floor level (y ≈ 7822 and 7939) and are **co-location failures with the escape starting too
late**:

```
  hit at 2487   dy -11.2 -> -14.6 -> -21.9 -> -29.6 -> -35.7 -> -40.2 (hit)
  hit at 2562   dy +39.6 -> +23.9 -> + 8.1 ->  -7.7 -> -28.9 -> -51.7 (hit)
```

At 2487 the player is moving at `vx -13.57` (dashing) with `dy` only -11.2 when the charge locks, and it takes
the entire charge for `dy` to reach only 40.2 against the 71 threshold. At 2562 the player is on the floor
(y = 7958) with `vy = 0.00` and moves at only `vx -2.7 .. -0.8` while `dy` sweeps from +39.6 through -7.7. In
both cases the escape is **directionally right and rate-limited**, not mis-aimed.

### 86.5 Status

- **KEPT** in `src/Chaite.Core/FishronWingScript.cs` (`PinnedWallMargin = 640f`); source valid UTF-8; builds
  clean; verified fresh DLL.
- **Measured (strong, cap 6000):** `6000 / 2 hits / death FALSE / 3 contacts`, from `6000 / 8 / FALSE / 5`.
- **Measured (weak, cap 6000):** `6000 / 4 hits / death FALSE / 5 contacts`, from `4598 / 8 / TRUE / 3` --
  **the weak wing now survives the cap for the first time**, which §65 requires of any acceptance.
- **Measured:** margin 900 regresses the strong wing to `6000 / 8 / FALSE / 9`.
- **Established:** `ClearanceLeft` saturates at 2400.0 (`maxHorizontal = 150`), which is why §85's branch was
  dead; the `x = 640` obstruction is invisible to the tile scan.
- **Twelve interventions attempted; two (§83, this) improve the fight.** The best achieved is the strong wing
  at `6000 / 2 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 87. Round 126: the margin optimum is sharp and narrow, and a source-encoding repair

### 87.1 Parameter sweeps: the optimum is a knife edge, so stop tuning

`PinnedWallMargin` was swept one variable at a time from the promising `640`:

```
margin   strong (ticks/hits/death/contacts)   weak (ticks/hits/death/contacts)
 540     6000 / 9 / FALSE / 5                 2696 / 8 / TRUE / 9
 640     6000 / 2 / FALSE / 3                 6000 / 4 / FALSE / 5     <- best
 740     3670 / 8 / TRUE  / 7                 (not run)
 900     6000 / 8 / FALSE / 9                 (not run)
```

`CoLocationBand` was swept the same way with the margin held at 640:

```
band     strong
 24      6000 / 2 / FALSE / 3     <- best
 80      6000 / 8 / FALSE / 4
```

**Both optima are sharp local maxima, not plateaus.** A 100 px change in the margin, or a 56 px change in the
band, costs 6-7 hits and can cost the run its survival. This is the same phenomenon §78 recorded: the fight is
a deterministic clock, so any parameter that moves the W cycle shifts *which* charges connect rather than
removing them. **The parameters are therefore not to be tuned further** -- the search would be fitting the
particular sampled cycle, not improving the machine. `640f` and `24f` are kept as measured, and any future
change must be justified by a mechanism, not by a sweep.

### 87.2 A source-encoding defect, found and repaired

The sweeps were applied with PowerShell `Set-Content -Encoding UTF8`, which re-encoded the file and turned the
three `§` characters in comments into mojibake. The damage was **comments only** -- the DLL hash was
unchanged from the good build -- but it was committed, so the file is repaired against `HEAD~1`, which still
held the correct text. All three lines are restored:

```
/// still leaving a 4920 px corridor (§78 requires the W-cycle timing
// THE MECHANISM IS VALIDATED. §81.2 showed every strong-wing body
// half as far in. That is the §79 pattern again: spending wing time
```

`git grep -c` for the mojibake range now returns nothing, the file starts `75 73 69 6E` (no BOM), and the
rebuilt DLL is again `D38B8A23E6162308` -- the exact binary that produced the 2-hit strong wing. **Use the
`edit` tool for source changes; `Set-Content` rewrites whole files in the console's encoding.**

### 87.3 Status

- **KEPT and repaired** in `src/Chaite.Core/FishronWingScript.cs`: `PinnedWallMargin = 640f`,
  `CoLocationBand = 24f`, no mojibake, valid UTF-8, builds clean, DLL hash `D38B8A23E6162308`.
- **Measured:** the margin sweep is `540 -> 9 hits`, `640 -> 2`, `740 -> 8 + death`, `900 -> 8`; the band sweep
  is `24 -> 2`, `80 -> 8`. Both optima are sharp.
- **Established:** parameters that move the W cycle redistribute hits rather than removing them (§78), so
  further sweeping is overfitting and is stopped.
- **Verified by hash:** the committed binary is byte-identical to the one that measured
  `6000 / 2 hits / death FALSE` (strong) and `6000 / 4 hits / death FALSE` (weak).
- **Twelve interventions attempted; two (§83, §86) improve the fight.** The best achieved is the strong wing at
  `6000 / 2 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 88. Round 127: the last two strong-wing hits, frame by frame

Both survivors of §86 are on the floor and are **specific geometric failures**, not general ones.

### 88.1 Hit at 2487 -- the dash carries the player into the charge

The Boss locks at t=2473 (`ai[0]` 0 -> 1) with the player at `(1000.9, 7883.5)` and the Boss at
`(758.7, 7898.4)`: `dx +242`, `dy -14.9`. The player is 242 px **to the right** of the Boss. The script's
command is `horizontal = -1` -- **it runs left, straight at the Boss** -- and on the next frame a dash fires and
takes `vx` to **-14.50**:

```
t=2473  P=(1000.9,7883.5) v=( -5.85,+1.92)  dx=+242.2  dy=-14.9  ai=[1,0,0,3]
t=2474  P=( 986.4,7884.5) v=(-14.50,+1.05)  dx=+210.7  dy=-12.8  ai=[1,0,1,3]  <- dash, -14.5
t=2477  P=( 944.8,7883.0) v=(-13.57,-0.98)  dx=+118.2  dy=-11.2  ai=[1,0,4,3]
t=2478  P=( 931.5,7881.8) v=(-13.26,-1.25)  dx= +88.0  dy=-11.4  ai=[1,0,5,3]
t=2479  P=( 940.5,7877.5) v=( +9.00,-4.28)  dx= +80.0  dy=-14.6  ai=[1,0,6,3]  <- reversal
t=2487  P=( 984.3,7843.5) v=( +3.09,-3.10)  dx= -11.9  dy=-40.2  ai=[1,0,14,3]  <- HIT
```

At the lock the vertical gap is only **14.9 px**, so the co-location lift fires and reverses the dash to
`+9.00, -4.28`, and `dy` does grow all the way to `-40.2`. **It is not enough**: the escape needed 71 px and had
eight ticks to find it while covering the 88 px of remaining horizontal gap, and the reversal cost the two
frames that the closing 88 px took. The `dy` at the hit, 40.2, is the largest separation either hit reaches.

### 88.2 Hit at 2562 -- running the floor at the Boss's own altitude

The Boss descends diagonally while the player tracks along the ground at `y = 7979` with `vy = 0.00` for the
entire approach:

```
t=2540  P=( 832.7,7979.0) v=( -7.98,+0.00)  dx=-321.8  dy=+134.4
t=2548  P=( 786.6,7979.0) v=( -4.70,+0.00)  dx=-247.6  dy= +71.2   <- threshold crossed
t=2557  P=( 759.0,7979.0) v=( -1.77,+0.00)  dx=-139.7  dy=  +0.2   <- Boss at player altitude
t=2562  P=( 755.4,7960.0) v=( +0.11,-6.21)  dx= -74.6  dy= -51.7   <- HIT
```

The Boss closes at about **10 px/tick horizontally and 7.9 px/tick vertically**, while the player's horizontal
speed **decays from -7.98 to -0.11** -- the wall-approach turnaround is fighting it -- and `vy` stays exactly
`0.00` for the first 20 ticks. `dy` crosses the 71 px threshold at t=2548 and passes through zero at t=2557
with **no vertical separation at all**. Only in the last three ticks does the player climb, at -6.2 px/tick,
which is too late: it converts a 74.6 px horizontal gap into a hit because the horizontal gap was already
inside the 85 px body half-width.

**The common cause is altitude.** Both hits happen at the floor (y 7843-7979, ground is 7958) at the Boss's own
elevation, where the only escape is horizontal and the horizontal escape is what the wall turnaround is
suppressing. §70's unconditional ascend lift and §78's minimum-altitude rules were both measured harmful in
their earlier forms, but they were tested **before** §83 and §86 changed the cycle; a floor-clearance rule is
the one avenue these frames argue for and it has not been tested against the current machine.

### 88.3 Status

- **Unchanged** source: `PinnedWallMargin = 640f`, `CoLocationBand = 24f`, DLL hash `D38B8A23E6162308`.
- **Established (frame-level):** hit 2487 is a dash *toward* the locked charge (`vx -5.85` -> `-14.50`) from a
  242 px horizontal gap at only 14.9 px vertical separation; the §83 lift reverses it but reaches only 40.2 px
  of the 71 needed. Hit 2562 is a floor track at the Boss's altitude with `vy = 0.00` for 20 ticks while the
  Boss closes diagonally at 10 px/tick horizontal.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.
