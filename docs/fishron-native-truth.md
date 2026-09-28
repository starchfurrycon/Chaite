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

## 89. Round 128: the floor-clearance rule is REVERTED -- altitude is load-bearing

§88.2 concluded that both remaining hits share a cause, altitude, and that a floor-clearance rule was the one
avenue those frames argued for which had not been tested against the current machine. It was implemented:

```csharp
if (input.Route == FormulaRoute.FishronStrongWingsDash &&
    dash && vertical >= 0 && !player.OnGround &&
    player.Center.Y > _floorY - FloorClearanceBand &&          // 160 px
    Math.Abs(player.Center.X - boss.Center.X) < FloorClearanceRange &&  // 520 px
    player.WingTime > _refillGuardBudget)
{
    vertical = -1;
    phase = "fishron-wing-floor-clearance";
}
```

**It regresses sharply:**

```
                        §88 best        + floor clearance
strong  ticks/hits/death/contacts   6000/2/FALSE/3    6000/7/FALSE/11
```

Hits go from **2 to 7** and body contacts from **3 to 11**. This is the **fourth time** the same shape has
appeared (§70's unconditional ascend lift, §78's minimum altitude, §86's margin 900, and now this): **any rule
that raises the player's altitude during the fight moves the W cycle and the charges redistribute.** The two
floor-level hits are the *cost* of a configuration that avoids eleven others, not an independent defect.

**Altitude is load-bearing in this fight, not incidental.** The rule is reverted; the committed source and its
DLL hash `D38B8A23E6162308` are byte-identical to the state that measured the best results.

### 89.1 Where this leaves the objective

Twelve of fourteen interventions have failed, and the two that worked (§83 co-location lift, §86 turnaround
margin) both won by **improving separation within the existing cycle** rather than by adding a new vertical
commitment. The remaining 2 strong-wing hits are a dash toward a locked charge at a 14.9 px vertical gap
(§88.1) and a floor track at the Boss's altitude (§88.2); both would need a **timing** change, not a
**threshold** change, and every threshold change tried so far has cost more than it bought.

- **Reverted**; tree clean, no mojibake, source valid UTF-8, DLL hash `D38B8A23E6162308`.
- **Measured:** floor clearance gives `6000 / 7 hits / FALSE / 11 contacts` against §88's `6000 / 2 / FALSE / 3`.
- **Established:** altitude-raising rules redistribute hits rather than removing them -- the fourth such result.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`, both
  surviving the cap.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 90. Round 129: dropping the lateral command is REVERTED -- the escape geometry is not the lever

§88.1 measured that at the lock preceding hit 2487 the perpendicular escape is `(11.8, 60.7)` -- **97% vertical**
-- while the script kept commanding `horizontal = -1` and burned a dash to `vx -14.50`. The natural inference
is that the lateral command is wasted motion: the dash closes 154 px of horizontal gap in six ticks and lets the
Boss's own `+17` px/tick close the remaining 88 px in the two frames the reversal costs.

So the co-location lift was made purely vertical by zeroing `horizontal` with it. **It regresses:**

```
                              §88/section 89 best    + lateral drop
strong  ticks/hits/death/contacts   6000/2/FALSE/3      6000/7/FALSE/8
```

Hits go from **2 to 7**. This is the **fifth** rule of this shape to fail (§70, §78, §86 margin 900, §89, this),
and it settles the pattern: **in this fight every command that changes the player's trajectory during a charge
redistributes the hits rather than removing them.** The lateral motion at the lock is not wasted -- it is part
of the configuration that keeps the other eleven charges from connecting.

A useful consequence for how to read §88: the trace shows the escape is *geometrically* misdirected, but that
is a **local** observation. The configuration as a whole is better than any locally-corrected variant, because
the fight is a single deterministic clock (§78) and a local correction resamples which charges land.

### 90.1 Status

- **Reverted**; tree clean, DLL hash `D38B8A23E6162308`, byte-identical to the best-measured state.
- **Measured:** the lateral drop gives `6000 / 7 hits / FALSE / 8 contacts` against `6000 / 2 / FALSE / 3`.
- **Established:** five trajectory-altering rules have now each cost more than they bought; local geometric
  correctness is not a valid objective function for this fight.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`, both
  surviving the 6000-tick cap.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 91. Round 130: the startup phase offset is REVERTED -- the probe cancels a held opening

### 91.1 The idea, which was the right shape

§90 concluded that every rule altering the trajectory **during** a charge only resamples which charges land,
and that the remaining two hits need a **cycle-level** change. The natural cycle-level lever is a one-time
**phase offset**: hold the player near its start for N ticks so the whole engagement shifts against the Boss's
attack clock.

It was implemented as an env-var-switchable hold (`CHAITE_STARTUP_HOLD`, default `0`, capped at 2400), so the
offset could be swept without rebuilding. **`hold=0` reproduced the best state exactly** --
`6000 / 2 hits / FALSE / 3 contacts` -- so the switch itself is faithful.

### 91.2 It cannot be measured: the probe cancels the run

Every non-zero hold, from **20 to 240**, ended identically:

```
valid battle : False
ticks        : 240
death        : False   outcome: Cancelled
HITS         : 0
```

`ticks 240` is the probe's takeover tick **plus 120**, and it is the same for a 40-tick hold and a 120-tick
one, so the cutoff is **time-based, not hold-based**. Two variants were tried:

1. **Idle hold** (no horizontal input at all) -- cancelled at 240.
2. **Station-keeping hold** (alternate `+1`/`-1` each tick) -- cancelled at 240.

with the episode bookkeeping (`_previousState`, `_previousSequence`, `_previousTimer`) kept current in the
early return, in case a stale `_previousState` was re-latching the charge normal late. That changed nothing.
The probe's `Cancelled` path is not the death path (§4333 in `GameProbe.cs` lists it among the terminal
states), so the run is being abandoned rather than losing -- most likely because the held opening does not
satisfy whatever the driver requires to certify a valid battle.

**Conclusion: this lever is not testable through the current native harness.** The phase offset may be the
right idea, but the probe will not carry a run through it, so it cannot produce native evidence. Reverted.

### 91.3 Where the objective stands

Sixteen interventions have now been attempted. Two improved the fight (§83 co-location lift, §86 turnaround
margin), five were reverted as regressions (§70, §78, §86 margin 900, §89, §90), and this one could not be
measured at all.

```
strong wing   6000 / 2 hits / death FALSE / 3 contacts    (from 11 hits, 8 contacts)
weak wing     6000 / 4 hits / death FALSE / 5 contacts    (from 4598-tick death)
```

Both loadouts survive the cap and neither is at zero. The two remaining strong-wing hits are a dash toward a
locked charge at a 14.9 px vertical gap and a floor track at the Boss's altitude; every threshold-shaped fix has
cost more than it bought, and the one cycle-level lever cannot be driven through the probe.

- **Reverted**; tree clean, valid UTF-8, DLL hash `D38B8A23E6162308` -- byte-identical to the best state.
- **Measured:** `hold=0` reproduces `6000 / 2 / FALSE / 3` exactly, so the switch was faithful.
- **Measured:** every hold from 20 to 240 gives `ticks 240`, `valid battle False`, `outcome Cancelled`,
  independent of the hold length.
- **Established:** the startup phase offset cannot be evaluated with this probe; `Cancelled` is not a death.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 92. Round 131: the phase offset is confirmed untestable -- `Cancelled` is the plugin's own safety abort

### 92.1 What `Cancelled` actually is

§91 guessed the probe was abandoning the run. That is wrong. `Cancelled` is set by
`EncounterController.Cancel()` (`src/Chaite.Core/EncounterController.cs:167`), and it is called from the
plugin's runtime when a takeover cannot be certified safe -- for example at `Runtime.cs:766`, where
`ActiveNativeMobilityHandoff.Advance` returns `Rejected`, and at `Runtime.cs:779`, where
`PrepareForSupportedActiveEncounterDetailed` does not return `Ready`. The probe merely *reports* that terminal
state (`tools/GameProbe.cs:4333`).

So the phase offset was not being abandoned by the harness; it was being **vetoed by the production safety
interlock**, which is a much stronger reason to stop. Two further variants were tried this round to rule out the
harness:

```
h=0   dir=-1   6000 / 2 hits / FALSE   valid battle TRUE    (baseline, unchanged)
h=40  dir=-1   240 ticks, valid battle FALSE, outcome Cancelled
h=40  dir=+1   240 ticks, valid battle FALSE, outcome Cancelled
```

A **constant** direction (so the player genuinely translates rather than oscillating in place, which §91.2
suspected was the cause) behaves exactly like the alternating one, and both directions behave the same. The
`ticks 240` is the plugin's certification window closing, not a probe limit.

**The startup phase offset is therefore closed as an avenue for this harness.** It cannot produce native
evidence, and the interlock that blocks it is a safety contract rather than a tunable.

### 92.2 A false alarm from the tooling, recorded so it is not chased again

While reading `Runtime.cs` the Chinese diagnostic strings appeared as mojibake (`鏃犳硶瀹夊叏...` for `无法安全...`),
which looked like repository-wide encoding corruption. **It is not.** `git grep -l -P "\x{E9}\x{8F}\x{83}"`
over every tracked `.cs` returns nothing, so the bytes on disk are correct UTF-8. The cause is that Windows
PowerShell 5.1 `Get-Content` decodes UTF-8 as ANSI, so the console rendering is wrong while the file is fine.

**Use `git grep`, the `read` tool, or `[System.IO.File]::ReadAllText` for UTF-8 content; `Get-Content` without
`-Encoding UTF8` is not authoritative.** This is the second encoding trap of the session (§87.2 was the first,
in the other direction) and it costs a full investigation each time if not remembered.

### 92.3 Status

- **Reverted**; tree clean, valid UTF-8, DLL hash `D38B8A23E6162308`, byte-identical to the best state.
- **Measured:** `h=0` gives `6000 / 2 / FALSE`; `h=40` gives `240 ticks / valid battle FALSE / Cancelled` for
  **both** hold directions.
- **Established:** `Cancelled` is `EncounterController.Cancel()` driving the plugin's safety abort, not a probe
  policy; the phase offset is blocked by a production safety contract and cannot be measured natively.
- **Established (tooling):** `Get-Content` without `-Encoding UTF8` mis-decodes UTF-8 as ANSI in PS 5.1; the
  repository has no encoding corruption.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 94. Round 133: §93 IS RETRACTED -- the player does land and the bar does refill

### 94.1 The error, and its cause

§93 claimed the player "never lands" and that "the bar is filled exactly once, at the start, and is then drained
permanently". **Both claims are false, and §93.1's whole conclusion is withdrawn.**

The mistake was treating the observation's `onGround` field as the ground truth for contact. The probe derives
it (`TerrariaFacade.cs:986`):

```csharp
state.OnGround = Math.Abs(vy) < .01f && hasFooting;
```

That is an **alias**, not the engine's `Player.onGround`, and it additionally reads `None` in these rows. The
authoritative evidence is the flight bar itself. Counting `wingTime` jumps of more than 20 over a full fight:

```
strong wing   12 refills      weak wing   20 refills
altitude at refill: 6031.5 / 6991.5 / 7951.5   (the platform and ground surfaces)
```

12 to 20 refills in 6000 ticks is roughly one every 580 ticks, which is ample. The tick-level context is
unambiguous -- the player descends onto a platform and the next tick is standing:

```
t=616  y=6038.0  vy= +8.13   wt=  0.0   cmd jump=False
t=617  y=6038.0  vy= +0.00   wt=  0.0   <- contact; velocity zeroed by the floor
t=618  y=6031.5  vy= -6.48   wt=180.0   <- full bar, via the `velocity.Y == 0` clause
```

So the native refill clause is broader than "landing":

```csharp
// Player.cs:26992
if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump && justJumped))
    wingTime = wingTimeMax;
```

It fires when the velocity is zero and the jump key is in its release transition -- and in the observed runs
`releaseJump` is already true on the contact tick, so the standing case covers it. **No change is needed to the
refill path; the circuit already lands and refills correctly.**

### 94.2 Consequences

- `CHAITE_LANDING_MARGIN`, added in §93.2 to *allow* a landing that was never being prevented, was **removed**.
  It was solving a non-problem, and §93.2's measurements (weak contacts 5 -> 2, hits 4 -> 5) were measuring a
  change made for a false reason. **Do not treat those numbers as evidence about landing.**
- §89's regression is **no longer explained**; that explanation is withdrawn along with §93.1.
- "The flight bar is running out" is **not** the cause of the residual hits.

### 94.3 The surviving measured fact

The native refill is reachable **in mid-air**: a frame with `velocity.Y == 0` and a jump-release restores the
whole bar without touching the ground. `CHAITE_APEX_REFILL` arms the drained-bar refill to drop the jump command
at the apex so that clause can fire. Measured:

```
                        apex refill off        apex refill armed
strong   6000/2/FALSE/3 contacts          6000/4/FALSE/6 contacts
weak     6000/4/FALSE/5 contacts          3237/7/TRUE (DEAD)/6 contacts
```

It **regresses both arms** -- the seventh trajectory-altering rule to do so. It is kept, **off by default**,
because the reachability of the clause is a real engine fact worth having a switch for; it is not a fix.

### 94.4 Status

- **Retracted:** §93.1 in full (the player does land; the bar refills 12-20 times per fight). §93.2's numbers
  are measuring a change made for a false reason.
- **Removed:** `CHAITE_LANDING_MARGIN`, which was added on the retracted premise.
- **Measured:** 12 refills (strong) / 20 (weak) per 6000 ticks, at the platform and ground surfaces.
- **Measured:** apex refill regresses both arms (strong 2 -> 4, weak 4 -> death at 3237). Kept off by default.
- **Verified:** with no `CHAITE_*` variables set, the strong wing still measures
  `6000 / 2 hits / FALSE / 3 contacts`.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 93. Round 132: the player never lands, so the flight bar never refills

> **RETRACTED by §94.** The premise is false: the player does land on the platforms and the bar refills 12-20
> times per fight. `onGround` is a probe-derived alias (`|vy| < 0.01 && hasFooting`), not the engine's contact
> flag, and the refill was being read from it. Kept only so the retraction has something to point at.

### 93.1 The measurement

Across a full 6000-tick fight, on **both** loadouts:

```
                          maxWingTime   wingTime == 0        onGround
strong (Fishron wing)         180       2009 ticks (33.5%)     0.0%
weak   (Fairy wing)           130       1485 ticks (24.8%)     0.0%
```

**The player never touches the ground in 6000 ticks.** The native refill is the landing tick
(`Player.cs:26992` restores `wingTime = wingTimeMax`), so the bar is filled **exactly once, at the start**, and
is then drained permanently. The strong wing therefore spends **a third of the fight with no flight at all**,
and the weak wing a quarter.

This also **explains §89's failure** from the other direction. The floor-clearance rule ordered a climb whenever
the ground was within 160 px, which forbids the very landing the bar needs; its regression was not mysterious.

The mechanism is `ApplyArena`'s floor clamp: `y >= _floorY - FloorMargin` (90 px) zeroes a downward command, so
the player hovers about 90 px up with an empty bar rather than descending the last stretch.

### 93.2 Making it testable, and measuring it

`CHAITE_LANDING_MARGIN` was added: when `wingTime <= 30`, the floor standoff drops to the given value
(default off, so the fixed circuit is unchanged). Measured:

```
                        baseline        landing enabled (margin 0 or 20)
strong   ticks/hits/death/contacts   6000/2/FALSE/3     6000/2/FALSE/3     (identical)
weak     ticks/hits/death/contacts   6000/4/FALSE/5     6000/5/FALSE/2     (contacts down, hits up)
```

The weak wing's **body contacts fall from 5 to 2** while its **total hits rise from 4 to 5** -- the shark
contact replaces a body contact. So landing genuinely changes the fight, but once again **redistributes** rather
than removes (the sixth instance of this pattern). The hook is kept **off by default** because it does not
improve either arm, and because the "never lands" fact is itself a finding worth not having to rediscover.

### 93.3 Status

- **Added, off by default, verified inert:** `CHAITE_LANDING_MARGIN` and `CHAITE_COLOCATION_ROUTES`, both
  defaulting to the fixed circuit; a run with no `CHAITE_*` variables set still measures
  `6000 / 2 hits / FALSE / 3 contacts` for the strong wing.
- **Measured:** `onGround` is **0.0%** for both loadouts over 6000 ticks; `wingTime == 0` for 33.5% (strong) and
  24.8% (weak).
- **Measured:** drained-bar landing leaves the strong wing identical and moves the weak wing's contacts 5 -> 2
  while its hits go 4 -> 5.
- **Established:** the flight bar is refilled once, at the start, and never again; §89's regression is explained
  by this rather than by altitude alone.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 96. Round 135: the dash-suppression rule was never wired, and §95.2 is retracted

### 96.1 The call site was dead code

The dash-suppression block added in §95 was placed inside `DecideMovement`. That method is the **trained-policy
hook**, and its second statement is:

```csharp
var learned = LearnedPolicy.ForRoute(input.Route);
if (learned == null) return;          // <-- no policy configured => returns here
```

Every native run in this session is made **without `CHAITE_POLICY_FILE`**, so `learned` is always null and
`DecideMovement` returns before reaching the block. **The rule never executed in any §95 measurement.** The
active charge path is `ChargeEscape`, which is where the dash proposal is actually produced.

Wiring it there is not possible as written: `ChargeEscape` is an instance method and takes no
`FormulaInput`, so it has no route. A `_dashSuppressRoute` field latched in `Tick` was added to bridge that.

### 96.2 With the rule actually wired, the parameter does nothing measurable

Once the block was in the live path, the threshold stopped discriminating:

```
weak wing, CHAITE_DASH_SUPPRESS = 0    6000 / 3 hits / FALSE / 3 contacts
weak wing, CHAITE_DASH_SUPPRESS = 50   6000 / 3 hits / FALSE / 3 contacts
weak wing, CHAITE_DASH_SUPPRESS = 90   6000 / 3 hits / FALSE / 3 contacts
strong wing, CHAITE_DASH_SUPPRESS = 0  6000 / 2 hits / FALSE / 4 contacts
strong wing, CHAITE_DASH_SUPPRESS = 90 6000 / 2 hits / FALSE / 4 contacts
```

Identical at 0 and at 50/90, while **30 kills the weak wing** (3065 ticks, 8 hits, death). A parameter whose
value is irrelevant except at one setting is not being applied where it is believed to be. So:

- **§95.2 is RETRACTED.** The claim that "`CHAITE_DASH_SUPPRESS` at 90 removes the t=2486 hit" is **not
  established** -- the rule was not running. The sweep numbers in §95.2 (2 / 8 / 2 / 8 / 7) are **not a
  measurement of dash suppression** and must not be cited.
- Any apparent weak-wing movement (4 contacts -> 3) across those runs is **not attributable** to this
  parameter, and the difference is not confirmed.

### 96.3 What survives, and the fix

§95.1's **hit anatomy is sound** and is the round's real result: the two strong-wing hits were located by direct
native trace, the contact test `|dx| < 85 && |dy| < 71` was verified against all three, and the t=2486 escape
provably runs along the charge normal. The trace work does not depend on the rule.

The dash-suppression code is **removed**, not left dormant: a rule that is believed to be active and is not is
worse than no rule, because it corrupts every later comparison -- which is exactly what §95.2 did.

### 96.4 Baseline, re-measured after full revert

```
strong wing   6000 / 2 hits / death FALSE / 3 npc contacts   boss damage 33
weak wing     6000 / 4 hits / death FALSE / 5 npc contacts   boss damage 87
```

The same binary produces the same fight on repeat, so these runs are deterministic; the ~30 HP damage
variations seen in §95.2 were produced by the partially-wired variant, not by noise.

### 96.5 Status

- **Retracted:** §95.2 in full (the suppression was never executing; its sweep is not evidence).
- **Removed:** the dash-suppression block, `DashSuppressGap`, `_dashSuppressRoute`, and
  `CHAITE_DASH_SUPPRESS`.
- **Corrected:** `DecideMovement` is a **policy-only hook that returns immediately with no policy configured**;
  engine-behaviour rules must live in `ChargeEscape` or `Cruise`.
- **Verified:** reverted tree reproduces `strong 6000/2/FALSE/3` and `weak 6000/4/FALSE/5`, and the runs are
  deterministic across repeats.
## 97. Round 136: the dash suppression, WIRED CORRECTLY, is real -- and improves the weak wing

### 97.1 §96's correction put the rule where it runs

§96 established that the §95 rule sat in `DecideMovement`, the policy-only hook that returns immediately with no
`CHAITE_POLICY_FILE` configured. The rule was re-implemented **inside `ChargeEscape`** (the instance method that
actually produces the dash proposal). `ChargeEscape` sees no `FormulaScriptInput`, so the loadout is latched
into a `_dashSuppressRoute` field in `Tick` before the call.

### 97.2 CONTROLLED sweep -- the parameter now discriminates

With the rule provably live (verified by reading the enclosing method, not by assumption):

```
STRONG wing   gap = 0 (baseline)  2 hits / 3 contacts
              gap =  40           6 hits / 3
              gap =  60           8 hits / 4
              gap =  90           2 hits / 4   <- optimum
              gap = 120           8 hits / 5 + DEATH
              gap = 150           7 hits / 6

WEAK wing     gap = 0 (baseline)  4 hits / 5 contacts
              gap =  30           8 hits / 6 + DEATH
              gap =  50           3 hits / 3   <- optimum
              gap =  75           7 hits / 4 + DEATH
```

Three things follow. First, **the rule is real**: unlike §95.2, the value now changes the outcome, and 0 is
distinct from every positive value. Second, **the optima are loadout-dependent** -- strong needs `>= 90` and is
killed by nothing in its useful range, while weak is *killed by 90* and wants 50; a single value cannot serve
both, the same §79/§83 pattern. Third, the optima are again **sharp**: 50 gives 3 hits while 30 and 75 both
kill.

### 97.3 The defaults, and the new best state

Both routes now take their own measured optimum (`FishronFairyWingsDash => 50`, otherwise `90`), with
`CHAITE_DASH_SUPPRESS` overriding both (`0` disables the rule and reproduces the pre-§97 circuit).

```
strong wing   6000 / 2 hits / death FALSE / 4 npc contacts   boss damage 42   (was 2 / 3, unchanged in hits)
weak wing     6000 / 3 hits / death FALSE / 3 npc contacts   boss damage 48   (was 4 / 5)  <-- NEW BEST
```

Both reproduce on repeat, so these are deterministic. **The weak wing improves on both axes at once -- one
fewer hit AND two fewer body contacts -- which is the first change in this fight that has done that.**

### 97.4 Status

- **Established:** the dash-suppression rule is real when placed in `ChargeEscape`; §95.2's *conclusion* is
  reinstated on correct evidence, though its *reasoning* (that the rule had been running) was wrong.
- **Measured:** controlled sweeps for both loadouts; optima 90 (strong) and 50 (weak), both sharp.
- **Changed default behaviour:** the weak wing is now `6000 / 3 hits / FALSE / 3 contacts`, improved from
  `6000 / 4 / FALSE / 5`, and reproducible.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 3 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 98. Round 137: the dash is 5 ticks early -- the timing is real, and fixing it is a NET LOSS

### 98.1 The timing defect is real, and it is the same on BOTH loadouts

Traced every remaining hit on the current default circuit. Four of the five hits across the two loadouts share
one anatomy: **the charge's dash is spent 2-5 ticks after the lock, so its 15 i-frames expire about 5 ticks
BEFORE the Boss arrives.**

```
weak   t=1425  lock 1406 -> dash 1408 -> i-frames die 1420 -> CONTACT 1424
weak   t=1605  lock 1584 -> dash 1586 -> i-frames die 1600 -> CONTACT 1604
weak   t=1782  lock 1758 -> dash 1760 -> i-frames die ~1774 -> CONTACT ~1778
strong t=4271  lock 4250 -> dash 4250 -> i-frames die 4265 -> CONTACT 4271
```

This is the first defect found that is **shared by both loadouts**, which is why the two arms fail at
structurally the same kind of tick. The Boss needs ~29 ticks to cross; the shield grants 15. Firing on the first
ready tick puts the window in the wrong half of the approach.

### 98.2 A stamina hypothesis, tested and REFUTED

> **CORRECTED BY §103.2.** The refutation below is **too broad**. Stamina is not the cause of *every* hit, but it
> **is** the cause of `strong t=3259`: the bar is empty on that frame and the climb has no authority. What
> follows correctly refutes the claim that stamina explains *all* five hits; it does not refute stamina as the
> cause of that one. The two hits also have different causes, so no single explanation covers them.

At two hits (`strong t=3259`, `weak t=1782`) `wingTime == 0`, and the player's `vy` is pinned at `+3.34` -- the
bar is empty so the escape has no vertical authority at all. That suggested the hits were a flight-budget
problem. It is not: `wingTime==0` occupies 35.2% (strong) and 26.5% (weak) of all ticks, and three of the five
hits -- `weak t=1425` (130), `weak t=1605` (114), and `strong t=4271` (146) -- happen with the bar **nearly
full**. Stamina depletion is present at some hits but is not the cause.

### 98.3 Delaying the dash DOES cover the contacts -- and still loses

`CHAITE_DASH_DELAY` holds the charge's dash proposal for N ticks after the lock. It works exactly as designed:
`npc contact` **falls from 3-4 to 0** at delays 12/16/24, so the collisions land inside the i-frames. And it
makes the fight worse at **every** nonzero value:

```
delay   0 -> 2 hits / 3-4 contacts   <- BEST, and the committed default
        2 -> 4          4 -> 4          6 -> 8          8 -> 10
       12 -> 5 (0 contacts)   16 -> 9 (0)   20 -> 8     24 -> 8 (0)
```

The reason is that the dash is a **once-per-charge budget**. Spending it late fixes that charge's arrival but
removes it from the rest of the cycle, and the cycle damage exceeds the single-contact saving. `strong delay=24`
shows the trade directly: **0 npc contacts and yet 8 hits** -- the contact counter is clean while the fight is
much worse, which also proves `npc contact` is not a sufficient proxy for `hits`.

**The default is 0 and must stay 0.** This closes the timing line: the defect is real, it is measurable, and it
is not the binding constraint.

### 98.4 A counter trap, recorded so it is not repeated

The first implementation of the delay used `NativeSequence`, i.e. the Boss's `ai[2]`. **MEASURED: `ai[2]`
counts up through the wind-up and RESETS TO ZERO exactly at the lock** (`ai2: 29 -> 0` as `ai0` goes `0 -> 1`),
so differencing it across the lock yields garbage. The symptom was five different delays (10/14/18/22/28)
producing **one identical result** (7 hits, death at tick 2406, boss damage 0) because the dash was held for the
entire charge. `NativeTimer` restarts at the state entry, so its value is the true ticks-since-lock. Any future
countdown from a lock must use the timer, not the sequence.

### 98.5 Status

- **Established:** the dash is systematically ~5 ticks early and this is shared by both loadouts; a locked
  charge dash gives 15 i-frames against a ~29-tick approach.
- **Measured:** the delay sweep for the strong wing; `npc contact` reaches 0 at delays 12/16/24.
- **Refuted:** (a) the stamina-depletion explanation -- 3 of 5 hits occur with a nearly full bar; (b) dash timing
  as the fix -- every nonzero delay is worse, and 24 gives 0 contacts with 8 hits.
- **Unchanged defaults:** `CHAITE_DASH_DELAY` defaults to 0; the committed behavioural state is unchanged.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE / 4 contacts`; weak wing
  `6000 / 3 hits / death FALSE / 3 contacts`. Both re-verified after the revert.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 99. Round 138: the dash runs INTO the charge -- measured, refuted, and the search closed

### 99.1 The defect is real: the dash is perpendicular to the locked charge

§98.3 showed the dash is needed for DISPLACEMENT, not merely for i-frames. Checking the direction found a
systematic defect present in **all four hits that share the §98.1 anatomy**:

```
strong t=4271  player x=4673.4  boss x=4293.0  boss charges RIGHT (+15.33)  player dashes vx=-14.50
weak   t=1425  player x=5569.2  boss x=5265.7  boss charges RIGHT (+14.97)  player dashes vx=-14.50
weak   t=1605  player x=5348.3  boss x=5606.4  boss charges LEFT  (-14.16)  player dashes vx=+14.50
weak   t=1782  player x=5558.8  boss x=5147.2  boss charges RIGHT (+15.30)  player dashes vx=-14.50
```

The player is consistently on the side the charge is coming from, and dashes **straight into it**. The
mechanism is precise: `ChargeEscape` sets `horizontal = _chargeNormalHorizontal`, and for a horizontal charge
the latched normal is **vertical**, so the horizontal input is 0. `Player.DoCommonDashHandle` then computes the
dash direction from the **facing**, which is stale, so the dash fires along whatever direction the player last
faced. At strong t=4271 the pair close at 29.83 px/tick (14.50 + 15.33) and the Boss arrives in 14 ticks;
dashing *along* the charge would close at 6.83 (15.33 - 8.50 wing cruise) and take **63 ticks**, four times the
15-tick i-frame window.

### 99.2 Forcing the dash along the charge makes BOTH arms worse

```
off: strong 6000 / 2 hits / 4 contacts    weak 6000 / 3 hits / 3 contacts
on:  strong 6000 / 7 hits / 3 contacts    weak 6000 / 4 hits / 3 contacts
```

So the 14-tick head-on arrival is **not the binding constraint**, and "the dash closes the distance faster" is a
superficial reading. Opposing the charge and crossing its path is what the fixed circuit wants. The edit was
reverted; nothing about the shipped behaviour changed.

### 99.3 A dash-into-the-Boss counter was considered and dismissed on engine evidence

Dashing into a charging Boss is a real technique, but the engine does not support it here. `NPC` contact during
a `dashType == 2` dash calls `Player.GiveImmuneTimeForCollisionAttack(4)`, which grants only **4** invulnerability
ticks -- not the 15 the dash state carries -- and it is additionally capped by `_immuneStrikes < 3`, i.e. three
grants inside any 20-tick window and the fourth is refused outright. A shield-dash exchange therefore buys 4
ticks and cannot be leaned on.

### 99.4 The search is closed on evidence, not on preference

Every principled intervention tried against this circuit has been measured and rejected:

| lever | result |
|---|---|
| dash suppression by `\|dy\|` (§97) | **WORKS AND STAYS**: strong 90, weak 50; weak improved 4 hits/5 contacts -> 3/3 |
| dash timing / delay (§98) | every nonzero delay worse; 24 gives 0 contacts yet 8 hits |
| stamina / refill scheduling (§98.2) | refuted; 3 of 5 hits occur with a nearly full bar |
| dash along the charge (§99) | both arms worse |
| co-location lift ungated (§83) | kills the weak wing (2829 / death / 9 hits) |
| floor-escape modes, minimum altitude, refill dash, stall breaker, phase offset, knob-b2 (§55-§91) | all refuted |

**The remaining hits are knife-edge coincidences, not a correctable policy error**, and the two defects found
in §98 and §99 are provably not the binding constraint. Roughly twenty controlled interventions have now been
tried against this fight; exactly one (§97 suppression) improved it, and the current pure fixed circuit is
`strong 6000 / 2 hits / death FALSE / 4 contacts` and `weak 6000 / 3 hits / death FALSE / 3 contacts`, both
deterministic across repeats. Reaching `hits == 0` is not reachable by the incremental route this session has
exhausted; it would need a different approach to the charge escape than tuning the existing state machine.

### 99.5 Status

- **Established:** the charge dash fires perpendicular to (usually directly into) the locked charge, because
  the latched normal is vertical for a horizontal charge and the engine then falls back to stale facing.
- **Measured:** `CHAITE_DASH_ALONG_CHARGE` on/off for both arms.
- **Refuted:** running and dashing along a locked horizontal charge; and a shield-dash exchange as a counter
  (`GiveImmuneTimeForCollisionAttack(4)`, capped at three grants per 20 ticks).
- **Unchanged defaults:** the experiment is reverted; the shipped behaviour is identical to §97.
- **Best achieved (unchanged, re-verified after the revert):** strong wing `6000 / 2 hits / death FALSE /
  4 contacts`; weak wing `6000 / 3 hits / death FALSE / 3 contacts`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.

## 100. Round 139: single-hit forensics -- the wind-up dive is real and still not the lever

### 100.1 The t=4271 hit, frame by frame

Attacked one hit as a single event rather than as a policy class. The Boss hovers at a **fixed y 4077.8** with
`bovy ~0` through ticks 4219-4248 (that is the wind-up; the lock is at 4249), while the player:

```
t=4219-4229  standoff      vy +6.00 -> +10.00   dy 257.5 -> 246.2   DIVING at the Boss
t=4230-4241  precharge     vy +3.34 -> -2.09    dy 243.2 -> 205.8   the jump reverses the dive
t=4242-4248  tornado-clear vy -1.69 -> +0.71    dy 204.4 -> 202.8   the climb flattens to zero
t=4249       LOCK                            dy +196.5  the player is 196 BELOW the Boss
t=4249-4271  charge        vy -0.64 -> -6.48    dy +196.5 -> -39.0  only ~22 px of net climb in 23 ticks
```

Against a body box that needs 71, contact at dy -39.0 is 32 short. The circuit spent the standoff **closing**
the vertical gap at up to 10 px/tick, and had only ~23 ticks to rebuild it.

### 100.2 Suppressing the dive is refuted too

`CHAITE_ALTITUDE_HOLD` suppressed the standoff's descent when the Boss hovers above (`|bovy| < 1.5`) and the
vertical gap exceeds 120 px, holding level so the precharge jump starts from a level attitude rather than a dive
to undo. Both arms got worse:

```
off: strong 6000 / 2 hits / 4 contacts    weak 6000 / 3 hits / 3 contacts
on:  strong 6000 / 4 hits / 5 contacts    weak 2946 / DEATH / 8 hits
```

Reverted. The wind-up dive costs the circuit more elsewhere than the extra separation buys at the lock.

### 100.3 The fixed circuit is a sharp local optimum, and this is now the central measured fact

**Twenty-one controlled interventions** have been tried against this fight. Exactly **one** improved it (§97
dash suppression, whose switch is `FishronFairyWingsDash ? 50 : 90`). Every other one -- dash timing (§98),
stamina scheduling (§98.2), dash along the charge (§99), altitude hold through the wind-up (§100), the ungated
co-location lift (§83), and the whole §55-§91 list -- made the result **worse, often with a death**. Three of
the last four produced a death that the untouched circuit does not.

Every defect this session *found* was real, reproduced frame by frame, and explained mechanically: the dash is
~5 ticks early, it fires perpendicular to the charge, the dash grants only 4 collision i-frames, the player
dives at the Boss through the wind-up. **Fixing any of them loses the fight.** The conclusion is not that the
diagnoses are wrong but that the circuit already sits on a knife edge where these local defects are being
traded against each other, and no single-axis correction is a net gain. `hits == 0` is **not reachable by the
incremental route**, which is now a measured statement rather than a guess: with ~20 interventions and one
success, and with the surviving hits being single-frame coincidences at 32 px inside a 71 px box, exhaustive
incremental search has been tried and has failed.

### 100.4 Status

- **Established:** the t=4271 contact geometry in full; the Boss hovers at a fixed y through the wind-up while
  the circuit dives at it, leaving only ~23 ticks and ~22 px of climb before contact.
- **Refuted:** holding altitude through the wind-up instead of diving -- both arms worse, weak wing dies.
- **Best achieved (unchanged, re-verified after the revert):** strong wing `6000 / 2 hits / death FALSE /
  4 contacts`, boss damage 42; weak wing `6000 / 3 hits / death FALSE / 3 contacts`, boss damage 48. Both
  deterministic across repeats; both run the full 6000-tick cap without dying, `validBattle == True`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed. Any claim of 无伤 for this circuit would be false under this document's own standard.

## 101. Round 140: the last zero-hit artifact, fully explained -- the circuit already does it, more

### 101.1 The artifact is real, and its mechanic is not exotic

The only zero-hit full-length record in `artifacts/` is `game-probe-standoff-dense`: **fishron-fairy-wing (weak
wing), 4000 ticks, `hits: 0`, `death: false`, `validBattle: true`**. §34.5 already disqualified it as acceptance
evidence (older build, and the same configuration on the current build gives 4 hits / 84 damage), but it was never
explained *mechanically*. It is now, and the explanation removes it as a lead rather than restoring it.

It is not a passive run. The pair genuinely came into contact -- at tick 3868 the centres were **7.9 px apart**
-- and the run survived on the Shield of Cthulhu dash **hitting the Boss**. The native signature is explicit:

```
t=3448  eocDash 15->9  eocHit 0  immuneTime 4  dashDelay 30  vx +9.00  vy -4.10
t=3866  eocDash 14->9  eocHit 0  immuneTime 4  dashDelay 29  vx +9.00  vy -3.60
```

That is `Player.cs` doing exactly what it does: the dash body collides with the NPC, `eocDash` is set to 10
(presented as 9 after the decrement), `dashDelay` is set to 30, `eocHit` records the NPC index,
`GiveImmuneTimeForCollisionAttack(4)` grants 4 i-frames, and the recoil at `velocity.X = -num4 * 9;
velocity.Y = -4f` throws the player clear. This is a *crossing* hit, not a charge into the Boss: the player
dashes left at -14.50 while the Boss charges right at 16.8, and they cross.

### 101.2 The current circuit already uses that mechanic -- about twice as often

Counting frames with `eocHit == 0` (the dash connected with the Boss body):

```
standoff-dense  (zero-hit artifact, OLD build, 4000 ticks)   18 eocHit-frames   hits 0
fn-strong       (current, 6000 ticks)                        36 eocHit-frames   hits 2
fn-weak         (current, 6000 ticks)                        27 eocHit-frames   hits 3
rv-strong / rv-weak (reproduced)                             36 / 27            hits 2 / 3
```

So there is **no missing technique**. The shipped circuit performs the dash-body-hit more than the zero-hit
artifact did, per tick and in total, and still takes 2-3 hits. The artifact's zero is a consequence of **fought
far less**: `bossDamage` 18 over 4000 ticks against the current circuit's 42-48 over 6000 -- roughly 4.5x the
damage rate -- so it simply faced fewer charge cycles and fewer crossings. A zero bought by barely fighting is
the exact thing the acceptance rule exists to reject, and it stays rejected.

### 101.3 Verdict

This closes the last open lead. Every structural hypothesis for the surviving hits has now been measured and
refuted: dash timing (§98), stamina (§98.2), dash direction (§99), wind-up altitude (§100), and "the earlier
zero used a special dash-contact trick" (§101) -- which is not a trick, and is already in use. **Twenty-two
controlled interventions; one improvement (§97).** The best achievable state on this harness and arena is
`strong 6000 / 2 hits / death FALSE / 4 contacts` and `weak 6000 / 3 hits / death FALSE / 3 contacts`, both
deterministic and both reproducible on demand.

**`hits == 0` was not achieved on either loadout, and no native zero is claimed.** The objective is unmet.

### 101.4 Status

- **Established:** the zero-hit artifact's mechanic in full (`eocHit == 0`, `immuneTime 4`, `dashDelay 30`,
  recoil to `vx ±9 / vy -4`), and that it is a crossing dash-body-hit.
- **Measured:** `eocHit` frame counts for every relevant run; the current circuit exceeds the artifact's rate.
- **Refuted:** "the earlier zero-hit run used a dash-contact technique the current circuit lacks" -- it does not
  lack it; it uses it more, and the artifact's zero comes from a ~4.5x lower damage rate over a shorter fight.
- **Best achieved (verified):** strong wing `6000 / 2 hits / death FALSE / 4 contacts`, boss damage 42; weak wing
  `6000 / 3 hits / death FALSE / 3 contacts`, boss damage 48.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**. Any claim of
  无伤 for this circuit would be false under this document's own standard.

## 102. Round 141: the 8-tick rear-end -- and the dash-body-hit is load-bearing

### 102.1 Every hit is the same event, 8 ticks after a dash-body-hit

One new measurement axis: the collision-immunity strike counter. `GiveImmuneTimeForCollisionAttack` refreshes
only while `_immuneStrikes < 3`, so three grants inside any 20-tick window leave the fourth refused. Checking
the cadence before each hit:

```
fn-weak   t=1425  grant t=1417  8 ticks before  depth 1  granted
fn-weak   t=1605  grant t=1597  8 ticks before  depth 1  granted
fn-weak   t=1782  grant t=1774  8 ticks before  depth 1  granted
fn-strong t=4271  grant t=4263  8 ticks before  depth 1  granted
fn-strong t=3259  (no grant within 45 ticks)
```

Immunity is **never refused** -- depth is always 1. So the failure is not the strike cap. It is that the 4
collision i-frames expire 3-4 ticks short of the contact, and the reason is a **REAR-END**, not a head-on:

```
t=4263  dx +62.4  dy +63.9  DASH-BODY-HIT  engine recoil REVERSES vx: -7.10 -> +9.00
t=4263-4271  pvx +9.00 -> +8.20 while the Boss runs bvx +15.33
        the Boss overtakes from behind; |dy| falls through 0 to -25.2 / -39.0
t=4267  the 4 i-frames expire
t=4270-4271  HIT -- |dy| 25.2 and 39.0, both well inside the 71 the body box needs
```

The player was ahead of the charge and the impact threw it **backwards into it**.

### 102.2 Steering the facing along the charge is refuted

`CHAITE_CHARGE_FACING` made the circuit face along a charging Boss **before** the lock -- which is the one place
§99's post-lock attempt could not reach, since the engine derives the dash direction from the facing and the
facing is stale by then. Both arms got much worse:

```
off: strong 6000 / 2 hits / 4 contacts    weak 6000 / 3 hits / 3 contacts
on:  strong 6000 / 6 hits / 1 contact     weak 5317 / DEATH / 10 hits
```

and strong `bossDamage` collapsed from 42 to **9**, i.e. the circuit largely stopped engaging. Reverted.

### 102.3 Removing the charge dash entirely is the most decisive negative of the session

`CHAITE_NO_CHARGE_DASH` withholds the charge's dash, so no dash-body-hit and therefore no recoil into the
charge. That hypothesis -- that the impact is a liability worth avoiding -- is **wrong in the strongest way**:

```
off: strong 6000 / 2 hits / no death        weak 6000 / 3 / 3
on:  strong 2406 / 7 hits / DEATH           weak 1636 / 7 / DEATH
     bossDamage 0 on BOTH -- the circuit never damaged the Boss at all before dying
```

**The dash-body-hit is load-bearing.** The same impact that costs the hit at t=4271 is what keeps the other
~5990 ticks alive, and the damage the circuit deals travels through that contact path -- removing it starves the
fight as well as killing the player. §102.1's rear-end reading, taken alone, is therefore **incomplete**: it
identifies the mechanism of the hit but not its role in the circuit.

### 102.4 Verdict

**Twenty-four controlled interventions; one improvement (§97).** Every part of this circuit is now measured to
be load-bearing, and every single-axis change is a net loss -- five of the last six produced a death the
untouched circuit does not. The best achievable state on this harness and arena is:

```
strong wing  6000 ticks / 2 hits / death FALSE / 4 npc contacts   boss damage 42
weak   wing  6000 ticks / 3 hits / death FALSE / 3 npc contacts   boss damage 48
```

both deterministic and reproducible on demand, both running the full `-maxticks 6000` cap alive with
`validBattle == True`. **`hits == 0` was not achieved on either loadout, and no native zero is claimed.**

### 102.5 Status

- **Established:** every surviving hit is a rear-end occurring exactly 8 ticks after a dash-body-hit at strike
  depth 1, with immunity never refused; the collision i-frames expire 3-4 ticks before the overtake.
- **Refuted:** steering the facing along the charge before the lock (both arms worse, boss damage 42 -> 9); and
  withholding the charge dash (both arms die with boss damage 0) -- which establishes that the dash-body-hit is
  load-bearing rather than a liability.
- **Best achieved (verified):** as quoted in §102.4.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**. Any claim of
  无伤 for this circuit would be false under this document's own standard.

## 103. Round 142: the damage and the hits do not scale with time -- both are localised

### 103.1 The strong wing passes native acceptance outright at 3000 ticks

Running the same committed circuit at five different `-maxticks` ceilings, everything else identical:

```
STRONG wing   t=2000  0 hits  no death  bossDamage 18   ACCEPTED
              t=3000  0 hits  no death  bossDamage 18   ACCEPTED
              t=4000  1 hit   no death  bossDamage 18   hits [3259]
              t=5000  2 hits  no death  bossDamage 27
              t=6000  2 hits  no death  bossDamage 42   hits [3259, 4271]

WEAK wing     t=2000  3 hits  no death  bossDamage 48
              t=3000  3 hits  no death  bossDamage 48
              t=4000  3 hits  no death  bossDamage 48
```

Two things follow, and neither was known before. **The strong wing records a native `hits == 0` at 3000 ticks** --
the runner prints `ACCEPTED: zero hits in the native engine`. And **boss damage does not scale with time**: 18
flat out to t=4000, then 27 at 5000 and 42 at 6000. The circuit's damage is **back-loaded**, not spread across the
fight.

Against the objective's own terms this is a *provisional* pass, not acceptance: the objective says acceptance
must run to the `-maxticks 6000` cap and that a 3000-tick `ACCEPTED` is only tentative. At 6000 the strong wing
records 2 hits, so **the objective is still unmet** and nothing here is claimed as 无伤.

### 103.2 Both hits sit in one window, and they have DIFFERENT causes

```
strong wing hits  [3259, 4271]
  t=3259   wingTime == 0    vy pinned at +3.34    <- STAMINA: the bar is empty, no vertical authority
  t=4271   wingTime 146     dash-body-hit t=4263  <- REAR-END (see §102.1)
```

`t=4271` is the rear-end already characterised. **`t=3259` is a stamina hit after all, and §98.2's refutation was
too broad.** That check counted hits at which the bar was *nearly full* and concluded stamina was not the cause;
it is not the cause of *all* the hits, but it is the cause of *this* one. The strong wing is airborne with
`wingTime == 0` for 35.2% of the fight and its first hit lands exactly in such a frame, where the measured `vy`
is a constant +3.34 -- pure gravity and no wing authority.

### 103.3 The existing refill guard cannot address it

The budget guard is

```csharp
if (player.WingTime <= _refillGuardBudget && !player.OnGround && vertical < 0)
```

and `ReadRefillGuard()` returns **0f** unless `CHAITE_REFILL_GUARD` is set. So the guard can only fire on the
exact zero frame, in which case the player is already out of authority -- it is a last-resort trigger, not
scheduling. Combined with §94's finding that the bar does refill (12x strong / 20x weak per 6000 ticks) but
35.2% of frames are still at zero, the picture is that the circuit spends the bar faster than it reliably
recharges, and the t=3259 hit is where that lands. Not changed here: raising the guard threshold is exactly the
kind of single-axis knob §98-§102 measured to be a net loss (five of the last six produced a death).

### 103.4 Status

- **Established:** the strong wing records `hits == 0` at `-maxticks` 2000 and 3000 with `validBattle == True`
  and no death; boss damage is back-loaded (18 flat to t=4000, then 27, then 42) rather than proportional to
  time; the two hits are localised to t=3259 and t=4271 and have **different** causes.
- **Corrected:** §98.2's dismissal of stamina was too broad. Stamina is not the cause of every hit, but it is the
  cause of `t=3259` (bar empty, `vy` a constant +3.34).
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2, weak records 3. The
  3000-tick `ACCEPTED` is provisional only, per the objective's own terms. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 104. Round 143: refill scheduling is refuted on the current circuit -- guard 0 is the optimum

### 104.1 The old sweep never covered this circuit, and the "hold" artifacts were empty

§24.3 swept `CHAITE_REFILL_GUARD` at a 6000-tick cap and found every row ended in `FailedAfterDeath`, with guard
0 dying at tick 5937. The **current** weak wing on guard 0 survives all 6000 ticks, so that sweep predates this
session's changes. The four artifacts named `hold20/40/60/90-strong` look like a guard sweep but are
**`ticks=240, validBattle=False`** -- the Boss spawns at tick 240, so those runs never fought. The threshold had
therefore never been measured against the circuit as it now stands, which mattered because §103.2 identified
`strong t=3259` as a genuine **stamina** hit (bar empty, `vy` pinned at +3.34), i.e. a case where more refill
runway should help.

### 104.2 Measured on the current circuit: every nonzero guard is worse

Strong wing, 6000-tick cap, only `CHAITE_REFILL_GUARD` changed:

```
guard    0 (default)   2 hits   no death   boss damage 42   contacts 4   <- OPTIMUM
        30             6 hits   no death   boss damage 54   contacts 6
        60             3 hits   no death   boss damage 27   contacts 2
        90             5 hits   no death   boss damage 48   contacts 4
       120            11 hits   DEATH      boss damage 45   contacts 5
```

The trend is monotone-worse at the extremes and strictly worse everywhere: **2 -> 6 -> 3 -> 5 -> 11**, ending in
the only death in the set. So §103.2's stamina finding is real, and scheduling the refill earlier is still not
the fix -- the guard can only act by **converting a climb into a descend**, which spends the very vertical
authority the charge escape needs. `CHAITE_REFILL_GUARD` stays at its default **0**, which this sweep confirms is
the optimum rather than an untested placeholder.

### 104.3 The lead set out in §103 is closed

Round 142 proposed this as the strongest remaining lead because it was "mechanism located, only 3 ticks short".
It is now measured and refuted on the current circuit. That makes **twenty-five controlled interventions, one
improvement** (§97). Notably the sweep is *not* flat -- it discriminates cleanly, with guard 0 the unique best --
so this is a real negative result and not an inert knob.

### 104.4 Status

- **Established:** on the current circuit, `CHAITE_REFILL_GUARD` at 0 is the optimum; every nonzero value is
  worse and 120 produces a death. The four historical `hold*` artifacts are `validBattle=False` at 240 ticks and
  are not measurements of anything.
- **Refuted:** raising the refill threshold to give the circuit landing runway before the t=3259 stamina hit --
  it spends the vertical authority the escape needs.
- **Unchanged:** the default remains 0; no code change and the committed behaviour is identical.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2, weak records 3. The
  objective remains **active and incomplete**, and no native zero is claimed.

## 105. Round 144: the refill window is not separable -- but the stamina hits ARE removable

### 105.1 The bar drains to empty and only then starts down

Measured on the committed default (`game-probe-rg-strong`), the refill events and the empty stretches:

```
refill events (wingTime jumps)     14 in 6000 ticks
  618 708 | 1221 | 1777 1971 | 2449 | 2930 | 3515 3612 | 4205 | 4637 | 5102 5300 | 5814
  gaps between refills: 513 556 194 478 481 585 97 593 432 465 198 514

zero-bar runs (len >= 8)
  t  535.. 617   83      t 2284..2448  165      t 3881..4204  324
  t  945..1220  276      t 2701..2929  229      t 4551..4636   86
  t 1455..1776  322      t 3231..3514  284      t 4949..5101  153
                                               t 5621..5813  193
```

A full bar is 180 and drains at about 1 per tick, so a ~280-tick empty stretch is **the descent-to-landing
time**. The circuit flies the bar to empty and only then begins descending, which is why `strong t=3259` -- the
§103.2 stamina hit -- lands **28 ticks into** the `t=3231..3514` stretch.

### 105.2 Scheduling the refill earlier REMOVES both original hits

This is the first time in the session that a change has eliminated a target hit. Same circuit, only
`CHAITE_REFILL_GUARD` changed, reading the actual hit ticks rather than the totals:

```
guard  0 (default) -> hits [3259, 4271]         the stamina hit and the rear-end
guard 60           -> hits [2256, 2484, 4564]   BOTH original hits GONE
guard 90           -> hits [896, 2949, 3314, 3514, 3554]
guard 30           -> hits [1614, 1771, 3261, 3910, 5333, 5825]
guard 120          -> hits [1627, 1870, 1910, 2433, 4431, 4471, 4707, 4924, 4964, 5504, 5912]
```

`guard 60` retires `t=3259` **and** `t=4271` outright -- neither a hit near 3259 nor near 4271 remains. So the
stamina failure mode is genuinely fixable by refilling earlier. The problem is that every nonzero guard also
**introduces its own hits** at unrelated ticks (2256/2484 for guard 60), which is why the totals in §104.2 were
worse even though the target hits were gone.

### 105.3 The two effects are NOT separable

The obvious next move is to refill early for the stamina hits but only where descending is free, i.e. **outside
the charge states (1/6/11)**, whose escape is the climb. That was implemented as `CHAITE_NO_CHARGE_REFILL` and it
is **refuted**, decisively:

```
guard 60, ungated -> 3 hits      guard 60, gated -> 9 hits
guard 90, ungated -> 5 hits      guard 90, gated -> 8 hits
```

Gating makes every threshold *far* worse. So the descend-to-land **during** a charge is itself load-bearing --
it is what actually returns the bar -- and there is no window in which the refill is free. Reverted; the knob was
deleted. The committed default of 0 remains the optimum, now for a *measured* reason rather than as an untested
placeholder.

### 105.4 Status

- **Established:** the bar drains to empty in 153-324 tick stretches whose length is the descent-to-landing time,
  and `strong t=3259` sits 28 ticks into one of them; raising the refill threshold **removes both** of the
  committed circuit's hits (guard 60 retires t=3259 and t=4271).
- **Refuted:** making that refill free by withholding it from the charge states -- gating 6-9x the hits at every
  threshold, so the descend-during-charge is load-bearing and the two effects cannot be split.
- **Reverted:** `CHAITE_NO_CHARGE_REFILL` removed; the default remains 0 and the committed behaviour is verified
  identical (strong 6000/2/no death/4 contacts, weak 6000/3/no death/3 contacts).
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. Twenty-six controlled interventions, one
  improvement (§97). The objective remains **active and incomplete**, and no native zero is claimed.

## 106. Round 145: the refill threshold is chaotic -- guard 0 is the unique optimum, axis closed

### 106.1 The full grid, with hit ticks rather than totals

§105 showed guard 0 and guard 60 have **disjoint** hit sets, and the original hits were still present at guard
30 (`3261`), so the transition had to lie in (30, 60). A fine grid resolves it. Eleven two-point steps:

```
guard  ticks  hits  death  dmg    hit ticks
    0  6000    2    False   42    [3259, 4271]                                    <- OPTIMUM
   30  6000    6    False   54    [1614, 1771, 3261, 3910, 5333, 5825]
   60  6000    3    False   27    [2256, 2484, 4564]
   90  6000    5    False   48    [896, 2949, 3314, 3514, 3554]
  120  6000   11    True    45    [1627, 1870, 1910, 2433, 4431, 4471, 4707, 4924, 4964, 5504, 5912]
   34  5934   13    True    69    [2477, 2517, 4565, 4605, 4674, 4721, 4761, 4801, 4971, ...]
   38  5307    8    True    51    [546, 3854, 4139, 4257, 4331, 4588, 4673, 4913]
   42  2971    7    True    93    [545, 1395, 2131, 2197, 2483, 2555, 2597]
   46  5509    8    True    69    [544, 762, 2432, 2621, 4104, 4148, 5095, 5156]
   50  6000    3    False   36    [543, 1785, 2486]
   56  6000    7    False   72    [2421, 2473, 2800, 3317, 4101, 4141, 5925]
```

**Four of the six fine-grid points die**, and pairs four apart disagree completely: 34 -> 13 hits/death,
38 -> 8/death, 42 -> 7/death, 46 -> 8/death. The hit *sets* share almost nothing between adjacent values. This is
**noisier than the §97 dash-suppression sweep** (which was at least smooth across `0/40/60/90`), so there is no
tunable region here -- the threshold is a chaotic parameter.

### 106.2 The optimum is isolated and already committed

`guard 0` is the **unique** no-death run at 2 hits. Every other no-death value is strictly worse (50 and 60 give
3 hits, 56 gives 7). So the committed default of 0 is not a placeholder that happens to be untested -- it is the
measured global optimum of this axis, by a wide margin.

That also disposes of §105's lead. Guard 60 does retire `t=3259` and `t=4271`, but it buys that with
`2256/2484/4564`, and no value in (30, 60) avoids both sets: the fine grid's points there either die or carry 7+
hits. The stamina hits are removable **in principle** — and removing them always costs more than it saves on this
circuit, because the refill descend and the charge escape are the same mechanism (§105.3).

### 106.3 Status

- **Established:** the refill threshold is a **chaotic** parameter; `guard 0` is the unique no-death run at 2
  hits and the measured global optimum; four of six fine-grid points (34/38/42/46) die.
- **Refuted, and the axis closed:** no value of `CHAITE_REFILL_GUARD` retires the committed circuit's two hits
  without introducing a worse set. Guard 60's removal of both is real but costs 3 unrelated hits.
- **Unchanged:** default 0, no code change; committed behaviour re-verified identical.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. Twenty-seven controlled interventions, one
  improvement (§97). The objective remains **active and incomplete**, and no native zero is claimed.

## 107. Round 145b: the two remaining knobs are inert, not unexplored

An inventory of every `CHAITE_*` knob `FishronWingScript.cs` reads leaves exactly two that this session had not
swept: `CHAITE_LOOP_LEG_CHARGES` and `CHAITE_LOOP_BEAT_PATTERN`. Both look like the "formulaic play" the objective
asks for, so they were the last plausible new axis. **Neither can act on the current circuit**, which is why no
run was spent on them.

- **`CHAITE_LOOP_LEG_CHARGES`** defaults to **0** (`ReadLegCharges` returns 0 for empty or `< 1`) and its branch,
  `else if (_legCharges > 0 && _legDirection != 0)`, sits **below** `else if (_chargeNormalSequence >= 0)` in
  `ChargeEscape`. On a locked charge the perpendicular latched normal wins and the last two branch bodies agree,
  so the leg schedule never reaches the output. Contrast the refill guard in §104, which *was* live at its default
  of 0 -- being a default is not the same as being inert, and the two had to be checked separately.
- **`CHAITE_LOOP_BEAT_PATTERN`** does run (`_patternActive` branch, line 995), but it is **shadowed twice**: first
  by the anti-stall floor at line 962 (`OnGround || |vx| < EscapeSpeedFloor` forces `away`), and then by the
  same `_chargeNormalSequence` branch. The floor is not incidental -- it is the fix the owner's own speed mandate
  required ("横向的速度需要一直保持 ... 几乎是静止状态"), and its interaction with a neutral beat is already
  measured in place: the comment at line 968 records the hit at **t=3019** where a neutral pattern charge left
  `horizontal = 0` for a whole approach while the Boss descended, and the player sat at `plX 640` with `vx 0.00`.

So the beat pattern *was* explored on this circuit and is documented as failing; the leg schedule is structurally
unreachable. **There is no un-swept axis left.** The search space this session has covered:

```
horizontal geometry   pin 640 (measured optimum; 2400 is not a wall)      §78
dash suppression      route-dependent 90/50, sharp optimum                §97  KEPT
dash timing           every nonzero delay worse                           §98
stamina scheduling    guard 0 unique optimum; grid chaotic; gated worse   §104-106
escape direction      facing-along post-lock and pre-lock both worse      §99, §102
dash presence         removing it kills both arms, boss damage 0          §102.3
altitude              hold/lift/min-altitude all refuted                  §83 ungated, §100, §55-57
co-location lift      strong-wing only; ungated kills the weak wing       §83  KEPT
beat pattern          shadowed; neutral beat measured to fail (t=3019)     line 968
leg schedule          _legCharges 0, branch shadowed                       §107
```

### 107.1 Status

- **Established:** the last two un-swept knobs cannot affect the current circuit -- one is disabled and branch-
  shadowed, the other is shadowed by the anti-stall floor and by the latched charge normal, with its failure
  already measured at t=3019.
- **Unchanged:** no code change; committed behaviour identical.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 108. Round 146: the t=4271 mechanism is corrected -- no rear-end, and the uniform i-frame economy

### 108.1 §102.1's "rear-end" reading was wrong

Every prior account of the `t=4271` hit -- including §102.1, and the code comment it produced -- held that the
dash-body-hit **reversed the player's travel**, that the Boss then **overtook from behind**, and that the hit was a
rear-end. Reading the dense trace tick-by-tick refutes all three. The player is **above** the Boss for the entire
approach:

```
  tick      dy      dx     pvx     pvy    bvy   wing  eocD  eocH  imm
  4238   +213.7  +415.9   +7.91  -1.26  +1.91  171     0    -1     0
  4249   +196.5  +410.2   +4.40  +1.11  +7.34  168     0    -1     0   <- LOCK
  4250   +189.4  +380.4  -14.50  +0.24  +7.34  167    15    -1     0   <- dash begins
  4262    +75.5   +68.8   -7.10  -3.66  +7.34  155    13    -1     0
  4263    +63.9   +62.4   +9.00  -4.28  +7.34  154     9     0     4   <- dash-body-hit
  4267    +14.6   +36.1   +8.60  -5.38  +7.34  150     5     0     0   <- i-frames END
  4270    -25.2   +15.3   +8.30  -6.20  +7.34  147     2     0     0
  4271    -39.0    +8.2   +8.20  -6.48  +7.34  146     1     0     0
  4272    -49.9    -2.7   +4.50  -3.50  +7.34  145     0    -1    40   <- DAMAGE, life 461 -> 394
```

The facts, in order:

1. **No rear-end.** `dy` runs +213.7 → +63.9 → −39.0. The player never goes behind the Boss; it passes *through*
   the Boss's vertical level from above. The Boss is not overtaking from behind, it is **rising into the player
   from below**.
2. **The dash did not reverse the travel.** `pvx` is +7.91 at t=4238 moving *away*, and the visible `−7.10 →
   +9.00` at 4263 is the **dash itself** (`dashType 2` writes `velocity.X = 14.5 * dir`), not a collision recoil.
   The player was fleeing left, then dashed right into the Boss's path.
3. **The dash-body-hit was a re-collision on a contact already true.** `dy` +63.9 is *inside* the 71 the two boxes
   need (`boss 150x100` + `player 20x42`), so at t=4263 the dash struck a Boss the player was already overlapping.
   Its reward is `GiveImmuneTimeForCollisionAttack(4)` -- **4 ticks**, spent at 4263-4266.
4. **Damage is the uniform economy, not a burst.** `imm` counts 4,3,2,1,0 across 4263-4267; contact damage then
   lands at 4272 with `life 461 → 394` and `immuneTime 40`. `eocHit` goes 0 → −1 and `eocDash` 1 → 0 across the
   damage tick. There is no special mechanism: the i-frames simply ran out approximately 5 ticks before a contact
   that was continuously available from 4268 onward.
5. **The closing is kinematic and unavoidable from 4268.** After the lock the Boss holds `bvy +7.34` while the
   wing tops out at `pvy ≈ −6.2`, so `dy` shrinks ~1.1/tick until the boxes meet. From `dy +196.5` at the lock the
   collision boundary arrives at t≈4268 and the trace shows exactly that.

### 108.2 The obvious fix is structurally unreachable

The tempting repair -- withhold the dash when the boxes already overlap, so the 4 i-frames are not spent on an
already-true contact -- was implemented as `CHAITE_OVERLAP_SUPPRESS` (gap `< 71`) and is **inert**:

```
overlapSuppress 1  strong: hits [3259, 4271]  <-- BYTE-IDENTICAL to the default
overlapSuppress 1  weak:   4520 / DEATH / 9 hits
```

The strong result is identical because the new branch is **strictly narrower than the one above it**: §97's
suppression already fires at gap `< 90`, and `71 < 90`, so any tick my branch could catch is already caught by the
90 px gate. That also means **§97's branch does not fire at t=4262** (where `dy` is +75.5): if it had, no dash
would have occurred at 4263 at all. So the dash at 4263 came from the branch structure with `dy` +63.9 *after* the
move that tick -- the gate is evaluated on the pre-move geometry, which is why a 90 px threshold never sees a
63.9 px gap. Reverted and the knob deleted.

### 108.3 Consequence for the whole session

This is the **third** time a mechanism-level conclusion has had to be retracted on re-reading the raw trace (§98.2
stamina, §105 refill, now §102.1 rear-end), and each retraction has *widened* rather than narrowed the set of
plausible levers. The measured position is therefore:

- the t=4271 hit is a **plain i-frame shortfall against a continuously-available contact**, with the dash spending
  its 4 ticks early on a contact that was already true;
- from the lock, the Boss's `bvy +7.34` against a wing ceiling of `pvy ≈ −6.2` closes the gap deterministically,
  so **no horizontal decision can affect it** -- the only in-principle lever is the vertical gap at the lock, and
  §100 (altitude hold) plus §83 (co-location lift, ungated) already establish that changing it costs more than it
  saves.

### 108.4 Status

- **Corrected:** §102.1's rear-end account is **wrong** and is retracted -- there is no overtake and no recoil
  reversal; the player is above the Boss throughout and passes through its level from above. The dash-body-hit is a
  re-collision on an already-true contact, and the damage is the ordinary i-frame expiry.
- **Refuted:** `CHAITE_OVERLAP_SUPPRESS`, a 71 px overlap gate -- inert on the strong wing (byte-identical hits)
  because it is strictly narrower than §97's existing 90 px gate, and fatal on the weak wing (4520 / death).
  Reverted and deleted.
- **Unchanged:** no behavioural change; committed state re-verified (strong 6000/2/no death/4 contacts, weak
  6000/3/no death/3 contacts).
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. Twenty-eight controlled interventions, one
  improvement (§97). The objective remains **active and incomplete**, and no native zero is claimed.

## 109. Round 146b: the `CHAITE_ROUTE_FILE` acceptance channel is now real, and both loadouts round-trip

### 109.1 The objective's named criterion had no artifact behind it

The objective names native tick-by-tick replay through `CHAITE_ROUTE_FILE` as the **sole acceptance channel**.
Every route-replay artifact in the tree predates this session's circuit and is weak:

```
fixedroute     1200 / 6 hits / DEATH
replay1..3     1200 / 6 hits / DEATH
identreplay     600 / 3 hits          ord2replay 600 / 3    ordreplay 600 / 3    postreplay 600 / 3
route-right    3000 / 5 hits
```

Meanwhile the script route reached 6000/2 and 6000/3. So the channel named as the criterion had **never carried
the circuit that actually performs**, and the two strongest results existed only as live script runs. That gap is
now closed: the committed circuit was harvested with `tools/harvest-native-route.py` into per-tick routes, and both
were replayed natively.

### 109.2 Both routes reproduce the script run exactly

The harvester reads `plan` (not the facade output; its docstring records that `actualAtApplyReturn` does not
round-trip because `MovementActionGate.ResolveJump` is not idempotent). `plan` coverage in the dense stream is
5761/6000 rows with full density from tick 500 onward, so no neutral fillers were needed (`0 neutral fillers`).

```
route                      ticks  hits  death  valid  dmg  contacts   vs. live script run
routes/strong-fishron-wings 6000   2     False  True   42   4          IDENTICAL (6000/2/False/42/4)
routes/fairy-wings          6000   3     False  True   48   3          IDENTICAL (6000/3/False/48/3)
```

Both replayed routes ran the full `-maxticks 6000` cap, stayed alive, and returned `validBattle == True`. Both
route files are committed (5999 tick-keyed rows each). **The acceptance channel the objective asks for now carries
the delivered circuit**, and a reviewer can reproduce either result with:

```
tools/run-native-acceptance.ps1 -RunName <fresh> -Phase monitor -MaxTicks 6000 `
  -RouteFile routes/strong-fishron-wings.csv -FormulaRoute fishron-strong-wing
```

### 109.3 A route is only half the configuration, and that is measurable

The first replay was launched with `-RouteFile` but **without** `-FormulaRoute`. It silently ran the default
(fairy/weak) loadout, and the same input stream then produced a completely different fight:

```
same route, strong-wing loadout : 6000 / 2 hits / no death / 42 dmg / 4 contacts
same route, NO -FormulaRoute    : 2032 / 6 hits / DEATH    / 99 dmg / 8 contacts   (fairy-wing)
```

This is the objective's own point -- 弱翼与强翼竖直机动性不同，理应写两套 -- expressed as a measurement: per-tick
directional input is **not** sufficient to specify the fight, because the wing decides how that input becomes
motion. A route file therefore has to be paired with its loadout, and the pair is what round-trips. Any future
acceptance run must pass both.

### 109.4 Status

- **Established:** the `CHAITE_ROUTE_FILE` channel, named by the objective as the sole acceptance criterion, now
  exists for the committed circuit; both loadouts round-trip **exactly** to their live script results at the
  6000-tick cap, alive, with `validBattle == True`; both route files are committed.
- **Established:** a route alone does not determine the fight -- the same 5999-tick input stream gives
  6000/2/no-death on the strong wing and 2032/6/DEATH on the weak wing -- so route and loadout must be passed
  together.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2 and weak records 3
  through **both** channels. Thirty controlled interventions, one improvement (§97). The objective remains
  **active and incomplete**, and no native zero is claimed.

## 110. Round 147: the charge is deterministic 16 px/tick -- and the lock ratio is the real variable

### 110.1 The engine commits the charge from the lock geometry

Decompiling `AI_069_DukeFishron` gives the mechanism behind every hit in this fight. At the lock
(`NPC.cs:35341-35343`):

```csharp
Vector2 vector124 = Main.player[target].Center - base.Center;
vector124.Normalize();
velocity = vector124 * 16f;          // exactly 16 px/tick, no randomness
```

So the charge velocity is **exactly 16 px/tick along the vector from the Boss to the player at the lock**, and
there is no RNG to exploit -- the owner's "固定 AI 且没有任何随机" is literally true here. `TargetClosest()` is
re-run at that instant, so the direction is fixed by the geometry at the lock and never updated during the charge.
This confirms the measured `bvx +15.33 / bvy +7.34` (`15.33² + 7.34² = 289 = 17²`, close to 16 plus the frame's
own integration) and, more importantly, makes the escape a **two-axis race with known closing rates**.

### 110.2 The load-bearing variable is dy/dx at the lock, and it is 0.479 against a 0.42 threshold

With `velocity = 16 * unit(player - boss)`, the vertical component is `bvy = 16 * dy / hypot(dx, dy)`. The wing's
climb ceiling is `pvy ≈ −6.2`, so the vertical race is winnable only while

```
bvy < 6.2   <=>   16 * sin(theta) < 6.2   <=>   sin(theta) < 0.388   <=>   dy/dx < 0.42
```

Measured at the lock:

```
t=4249   dx +410.2   dy +196.5   pvx +4.40  pvy +1.11   bvx +15.33  bvy +7.34
         dy/dx = 0.479        (threshold 0.42)
         allowed shrink before |dy| < 71  : 125.5
         time to vertical boundary        : 125.5 / (7.34 + 6.20) = 9.3 ticks
```

**0.479 is just above 0.42**, so the Boss gets 7.34 of vertical closing against a 6.2 ceiling and wins by about
1.1/tick. That single number is the whole loss: it is why the boxes meet at t=4268-4272 (§108.1) no matter what the
horizontal decision is. Every earlier attempt in this session steered the escape **after** the lock, by which point
`dy/dx` is already frozen. This is the first axis that acts **before** the commit.

### 110.3 Acting before the commit: implemented, confirmed to fire, and worse

`CHAITE_BAND_TARGET` kept the dive while a locked-charge stack was developing, so the lock lands at a smaller
`dy/dx`. Unlike the previous round's gate, this one was **verified to fire** (31 times in the strong run, phase
string present in the observation stream), so the negative result is about the rule, not about deployability:

```
off: strong 6000 / 2 hits / no death     weak 6000 / 3 hits / no death
on:  strong 6000 / 3 hits                weak 3543 / 10 hits / DEATH
```

Both arms worse, and the weak wing dies. So the ratio is controllable in principle but steering it costs more than
the vertical race it buys -- the same pattern as §100 and §83-ungated. Reverted and deleted.

### 110.4 A scoping trap that cost a round, recorded so it is not repeated

The first version of this rule was placed in `ChargeEscape` and **never fired once in 6000 ticks** (the phase string
appeared 0 times), even though every clause of its condition was satisfied at t=4230. The cause: `Tick` calls
`ChargeEscape` **only when `dash` is true**, i.e. only for Boss states 1/6/11. At t=4230 the Boss is still in its
pre-charge state, so `Cruise` is the branch that runs and `ChargeEscape` is never entered. The refill guard sits in
`Tick` for exactly this reason. **Any rule that must act during the pre-charge window has to live in `Tick`** (or
`Cruise`), not in `ChargeEscape`. The dead copy was removed and the working one placed in `Tick` after
`ApplyArena`.

### 110.5 Status

- **Established:** `AI_069` commits the charge at the lock as `Normalize(player - boss) * 16f` -- exactly
  16 px/tick, no randomness; therefore `bvy = 16*sin(theta)` and the vertical race is winnable only while
  `dy/dx < 0.42`; the measured lock is `dy/dx = 0.479`, which is why the Boss wins by ~1.1 px/tick and the boxes
  meet at t=4268-4272.
- **Refuted:** steering `dy/dx` before the lock (`CHAITE_BAND_TARGET`) -- confirmed to fire 31 times and worse on
  both arms, with the weak wing dying at 3543. Reverted and deleted.
- **Recorded:** a rule that must act before the lock cannot live in `ChargeEscape`, which runs only for states
  1/6/11; it must live in `Tick` or `Cruise`.
- **Unchanged:** committed behaviour re-verified (strong 6000/2/no death/4 contacts, weak 6000/3/no death/3).
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. Thirty-one controlled interventions, one
  improvement (§97). The objective remains **active and incomplete**, and no native zero is claimed.

## 111. Round 147b: the 70-lock census, and the width axis is closed

### 111.1 The §110 threshold, measured across every charge in the fight

§110 derived the condition from a single lock. Detecting all 70 locks in the committed 6000-tick run (a lock is a
frame where the Boss's speed jumps above 14 having been at or below 14) gives the full distribution:

```
locks detected: 70
locks with |bvy| > 6.2 (the vertical race is LOST): 49 of 70
dy/dx over all locks: min -2.223   max 7.239   threshold 0.42
```

So the vertical race is lost at **49 of 70 locks** while the circuit takes only **2 hits in 6000 ticks**. The race
is therefore **necessary but not sufficient** for a hit -- the other 47 lost races are survived by the dash's
i-frames, the beat pattern, and the co-location lift. That is an important bound on the theory: §110's threshold
explains *why* a charge is dangerous, not *that* it will hit.

Note the sign convention: `bvy` negative means the Boss charges **upward** at the player, which is the mirror case
and is just as unwinnable (the wing descends at `maxFallSpeed 10.01`, so the asymmetry runs the other way but the
race is still a race). The cleanest statement is on `|bvy|`.

### 111.2 The affordable axis is dx, and overriding `horizontal` to hold it is fatal

The natural repair is the one §110 pointed at: `dx` is the cheap axis, because raising it lowers `dy/dx` without
spending wing time (whereas every vertical intervention, §100 and §83-ungated, has cost more than it bought). At
the t=4249 lock `dx` is 410.2 against the 468 needed -- about 58 px -- and `pvx` is only **+4.40** while the wing
cruises at roughly **13.9**, so the circuit is visibly braking into the lock.

`CHAITE_WIDTH_HOLD` forced `horizontal = away` through the pre-charge window (range 720 px, charge states only,
`vertical` deliberately untouched). It is **fatal on both loadouts**:

```
off: strong 6000 / 2 hits / no death     weak 6000 / 3 hits / no death
on:  strong 3265 / 7 hits / DEATH        weak 3413 / 7 hits / DEATH
```

Pinning the horizontal direction destroys the horizontal **beat pattern**, and the pattern is what desynchronises
the circuit from AI_069's attack clock. This is the same failure as §78: a rule that overrides `horizontal` for a
whole window cannot be afforded no matter what it buys locally. **The width axis is closed.** Reverted and deleted.

### 111.3 Status

- **Established:** across all 70 locks in the committed run, the §110 vertical race is lost at **49 of 70**, yet
  only 2 hits occur -- so the race is necessary but not sufficient, and the other 47 losses are absorbed by the
  dash i-frames, the beat pattern and the co-location lift.
- **Refuted and axis closed:** holding `dx` open through the pre-charge window (`CHAITE_WIDTH_HOLD`) -- fatal on
  both arms (3265/7/DEATH and 3413/7/DEATH) because overriding `horizontal` desynchronises the beat pattern.
  Reverted and deleted.
- **Unchanged:** committed behaviour re-verified (strong 6000/2/no death/4 contacts, weak 6000/3/no death/3).
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout. Thirty-two controlled interventions, one
  improvement (§97). The objective remains **active and incomplete**, and no native zero is claimed.

## 112. Round 148: a route-replay acceptance trap -- dense frames changes the fight

### 112.1 The one-command check, and the bug it immediately exposed

`tools/verify-fishron-routes.ps1` was added so the objective's named criterion can be exercised in a single
command: it replays `routes/strong-fishron-wings.csv` and `routes/fairy-wings.csv`, each paired with its
`-FormulaRoute` loadout, and compares `result.json` against the recorded expectation. The first version did **not**
set `CHAITE_PROBE_DENSE_FRAMES`, and it failed immediately:

```
ROUTE-REPLAY VERIFICATION
loadout  formula                ticks  hits  death  valid  dmg   vs expected
strong   fishron-strong-wing    6000   8     False  True   57    DRIFT
weak     fishron-fairy-wing     5764   10    True   True   90    DRIFT
```

The same two route files had replayed to `6000/2/no death` and `6000/3/no death` minutes earlier. Setting the
variable made them match again. The comparison is controlled -- identical route files, identical loadouts,
identical tick cap, **only the probe's instrumentation level differs**:

```
same route, CHAITE_PROBE_DENSE_FRAMES unset : strong 6000/8 hits/57 dmg    weak 5764/10 hits/DEATH/90 dmg   shield rows 1024
same route, CHAITE_PROBE_DENSE_FRAMES = 1   : strong 6000/2 hits/42 dmg    weak 6000/3  hits/no death/48  shield rows 73
```

### 112.2 Why this matters more than a missing flag

The replay's shield cadence depends on how often the probe samples: the unset run records **1024 shield rows**
against **73**, and `dash started` 26 against 69. So a route-replay acceptance run that omits the variable is not
"a less detailed measurement of the same fight" -- it drives a **different** fight and reports a different result,
with no error. Under the objective's own standard (`CHAITE_ROUTE_FILE` as the sole acceptance channel) that is
exactly the kind of silent divergence the standard exists to prevent: **a route-replay number is meaningless
without its instrumentation level recorded alongside it.**

The variable is now set inside the verification script, and every `CHAITE_*` knob is cleared there too, so a
leftover experiment cannot change the circuit under test.

### 112.3 The verification result

With both fixes in place:

```
loadout  formula                ticks  hits  death  valid  dmg   vs expected
strong   fishron-strong-wing    6000   2     False  True   42    MATCH
weak     fishron-fairy-wing     6000   3     False  True   48    MATCH

REPRODUCED: both routes replay to their recorded native result.
ZERO-HIT NOT ACHIEVED: hits are 2 / 3. The objective requires hits == 0 at the
6000-tick cap and that remains UNMET.
```

The script reports `ZERO-HIT` only if a run genuinely records `hits == 0`, so the unmet requirement is stated by
the tool itself rather than left to prose.

### 112.4 Status

- **Established:** route replay through `CHAITE_ROUTE_FILE` **requires** `CHAITE_PROBE_DENSE_FRAMES=1`; with it
  unset the same route file yields `6000/8 hits/57 dmg` and `5764/10 hits/DEATH/90 dmg` instead of
  `6000/2/42` and `6000/3/48`, with 1024 shield rows instead of 73. A route-replay result is therefore
  uninterpretable without its instrumentation level.
- **Established:** `tools/verify-fishron-routes.ps1` exercises the named channel end to end and confirms both
  loadouts reproduce their recorded native result, while reporting the `hits == 0` requirement as **UNMET**.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2 and weak records 3
  through **both** channels. The objective remains **active and incomplete**, and no native zero is claimed.

## 113. Round 149: both vulnerabilities are BOUNDED windows -- and the weak wing's deficit is 3x the strong's

### 113.1 The two arms fail in disjoint time windows

Reading the hit ticks against the tick cap rather than the totals shows something the totals hid:

```
strong wing: hits [3259, 4271]        ZERO hits in the first 3258 ticks
weak   wing: hits [1425, 1605, 1782]  ZERO hits after 1782

weak wing boss damage: 48 at -maxticks 2000, 3000, 4000 AND 6000  -> literally nothing after 1782
```

So neither arm degrades with time. Each has **one bounded vulnerable window** -- the strong wing's is the
mid-fight (3259-4271), the weak wing's is the opening 1800 ticks -- and is hit-free outside it. That is a stronger
and more useful statement than "2 hits" and "3 hits", and it means the remaining work is narrow in both cases.

### 113.2 The weak wing's three hits are one mechanism, and its climb ceiling is the cause

All three have an identical anatomy. Taking `t=1425`:

```
  tick      dy      dx     pvy     bvy    pvx  wing eocD eocH  phase
  1415   +58.3  +107.7  -7.41  +8.06  -11.19  122   15   -1   charge-ascend
  1417   +30.6   +76.5  -4.10  +8.06   +9.00  120    9    0   <- dash-body-hit
  1420    -6.5   +58.0  -4.40  +8.06   +8.70  117    6    0
  1421   -19.0   +51.7  -4.50  +8.06   +8.60  116    5    0   <- i-frames END
  1425   -70.3   +23.3  -4.90  +8.06   +6.98  112    1    0   <- HIT
```

The same shape at `t=1605` (dash 1597, arm 1601) and `t=1782` (dash 1774, arm 1778). In every one:

- `bvy +8.06` against a weak-wing climb ceiling of **`pvy −4.90`** -- a **3.16/tick deficit**;
- the dash's **4** collision i-frames are spent immediately, so they expire **4 ticks before** the boxes can meet;
- `dx` collapses 76 -> 23 over the same eight ticks, so the dodge is running out of horizontal room as well.

The weak wing's deficit is **three times the strong wing's** (`§110`: bvy 7.34 against `pvy −6.2`, ~1.1/tick). That
is the quantitative form of the owner's own point that the two loadouts have different vertical mobility and
therefore need two state machines: the strong wing escapes by about 22 px per charge and the weak wing by about
**−13 px**, i.e. it loses ground on every single charge and only survives because the i-frames and the boss's own
hover timing absorb the losses.

### 113.3 Consequence: the failure is structural to the i-frame budget

On all three weak-wing hits the dash-body-hit happens at `dy` between +30 and +39, which is **already inside the
71 the boxes need**. So the dash does not prevent a contact -- it is a re-collision on an already-true overlap, and
its only product is 4 i-frames that then expire 4 ticks before the boxes meet. Since the dash-body-hit fires at the
moment the boxes are already touching, and contact damage resolves 8 ticks later, **no choice of dash timing can
place 4 i-frames over a contact that resolves 8 ticks out**. This is the same conclusion §110 reached
geometrically, now confirmed on the second loadout by direct measurement, and it is why §98's `CHAITE_DASH_DELAY`
was strictly worse at every nonzero value.

### 113.4 Status

- **Established:** both arms have a **single bounded vulnerable window** and are hit-free outside it -- strong
  3259-4271 with zero hits before 3258, weak 1425-1782 with zero hits after 1782 and boss damage flat at 48 across
  every cap from 2000 to 6000.
- **Established:** the weak wing's three hits are one mechanism: the dash-body-hit lands at `dy +30..+39` (already
  inside the 71 the boxes need), its 4 i-frames expire 4 ticks before contact, and the closing rate is
  `bvy +8.06` against a weak-wing ceiling of `pvy −4.90` -- a **3.16/tick deficit, about 3x the strong wing's**.
- **Established:** no dash timing can cover a contact resolving 8 ticks after a dash-body-hit that grants 4
  i-frames, on either loadout.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2 and weak records 3
  through both the script and route channels. The objective remains **active and incomplete**, and no native zero
  is claimed.

## 114. Round 150: the dash-body-hit deferral is exactly 10 ticks, and the last width variant is closed

### 114.1 The deferral is a constant: 10 ticks

Pairing every dash-body-hit (`contact: true` in `shield-events.jsonl`) with the next life decrease gives the
deferral between the collision and the damage it causes:

```
strong: dash@4262 -> hit@4272   defer 10
weak  : dash@1416 -> hit@1426   defer 10
        dash@1596 -> hit@1606   defer 10
        dash@1773 -> hit@1783   defer 10
```

**Exactly 10, in every case that produced damage, on both loadouts, at every closing geometry and both launch
directions.** This is the missing piece of §113.3: `GiveImmuneTimeForCollisionAttack(4)` (`Player.cs:21284`) grants
**4** i-frames over a **10**-tick deferral, so ticks 5 through 10 are unprotected by construction. The pairing also
shows two contacts that were *not* followed by damage (strong `t=353`, `t=1831`, and `t=5153`; the next hit after
each is thousands of ticks away), confirming that a dash-body-hit is a necessary but not sufficient antecedent of a
hit.

### 114.2 The circuit spends exactly those 6 ticks turning around

At the weak wing's `t=1773` hit the launch carries the player away at `vx +9.00`, and over the next 10 ticks that
decays only to `+8.20` -- yet `dx` **collapses from 67.9 to 13.8** because the circuit begins pre-positioning for
the next charge. So the ten ticks in which the player is most exposed are the ten the circuit uses to reverse.

### 114.3 Narrowest form of the repair, still refuted -- but with a diagnostic tell

`CHAITE_BODY_WINDOW` held `away` for ticks 5..10 only: six ticks, on an event-triggered window, at the one moment
the circuit should not be turning around. That is the narrowest version of the width repair, and it is deliberately
ten times shorter than the fatal whole-window `CHAITE_WIDTH_HOLD` (§111.2). It still fails:

```
off: strong 6000 / 2 hits / 42 dmg     weak 6000 / 3 hits / 48 dmg
on:  strong 6000 / 6 hits / 27 dmg     weak 6000 / 3 hits / 48 dmg
```

The tell is worth recording: **damage falls 42 -> 27 while hits rise 2 -> 6.** The rule is not being ignored; it
converts two hard hits into six soft ones, i.e. it moves *which* contact lands rather than preventing one. That is
the signature of every `horizontal` override tried in this session, and it closes the last variant of the width
axis. Reverted and deleted.

Incidentally this establishes that `EyeShieldDashState` already carries `EocHit` and `EocDash` (raised in
`GravityDashMotion.cs`), so a collision can be detected in-script with no probe change -- useful even though this
particular rule failed.

### 114.4 Status

- **Established:** the dash-body-hit's deferred damage lands **exactly 10 ticks** after the collision, on both
  loadouts and at all measured geometries, against only **4** granted i-frames -- so ticks 5..10 are unprotected
  by construction. A dash-body-hit is necessary but not sufficient for a hit.
- **Established:** `EyeShieldDashState.EocHit` / `.EocDash` are already available to the script, so native
  collision detection needs no probe change.
- **Refuted and closed:** holding `away` across the uncovered tail (`CHAITE_BODY_WINDOW`, 6 ticks, the narrowest
  form of the width repair) -- strong 2 -> 6 hits, with damage falling 42 -> 27, the tell that it relocates a
  contact instead of preventing one. Weak unchanged. Reverted and deleted.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2 and weak records 3,
  re-verified through `tools/verify-fishron-routes.ps1` after the revert. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 115. Round 151: the hits are close passes, not edge traps -- and the weak wing's side bias is the one behavioural difference measured

### 115.1 Neither loadout is trapped against an arena edge

Fitting the hypothesis that the hits come from being cornered, absolute player X over each fight:

```
weak   wing: player X min 650.0  max 5730.2  (span 5080)   boss X 738.6 .. 5898.2
strong wing: player X min 650.0  max 5648.0  (span 4998)   boss X 576.9 .. 5878.8

weak   hits at playerX 5543.5 / 5380.8 / 5497.7  (dx +23.3 / +43.5 / +13.8)
strong hits at playerX 5527.9 / 4623.1           (dx +79.9 / +8.2)
```

The arena's left edge is X 650 and its right edge is around X 6400, so the furthest-right hit (X 5543) still has roughly **850 px of room to its right**. **No hit is an edge trap.** Every hit sits at a small `dx` (8.2 to 79.9), so all five are the same mechanism: the boss's body box meeting the player's during a close pass, exactly as §113.2 described for the weak wing.

### 115.2 The one measured behavioural difference between the loadouts

```
weak   wing: time in left third 10%   middle 29%   right third 61%
strong wing: time in left third 46%   middle 29%   right third 25%
```

Both start at the left (tile 21). The strong wing turns around often enough to stay roughly **balanced** (46 / 29 / 25); the weak wing **drifts right and stays there** (10 / 29 / 61), spending six times as long on its right third as on its left. Since the weak wing's three hits are all in that right region, the side bias is the only behavioural difference this session has been able to measure between the two loadouts' horizontal handling.

It is a **consequence**, not a cause that can be edited directly: the same script drives both, and the divergence comes from the weak wing's smaller `wingTimeMax` (130 against 180), which changes how often the refill and turnaround branches win. A rule cannot simply "turn around more" -- §111.2 measured that pinning `horizontal` in the pre-charge window is fatal on both arms, and §78 measured that desynchronising the turnaround costs more than the separation buys. Recorded as a characterisation, not a lever.

### 115.3 The weak wing's vulnerable window opens when it finishes its opening crossing

Sampling player X every 200 ticks locates the window in space and time:

```
  tick   playerX
   200     650.0      <- start, left edge
   600    2427.6
   1000   4555.3
   1200   5378.7      <- has crossed to the right
   1400   5592.1
   1600   5422.8      <- hit window open
   1800   5537.1      <- t=1782 hit, the last one
   2000   4711.5      <- already leaving
   2400   3491.5
   3200   5593.6      <- back to the right, and NOT hit
   5400   5495.2      <- still there, and NOT hit
   5800   5595.3
```

The weak wing covers 650 -> 5379 in the first 1200 ticks and all three hits fall in the following 600, while it dwells in the band X 5280-5600. So the window is not merely "early": it opens **when the opening crossing completes**, and closes 1782 ticks in even though the player returns to the same band at 3200 and stays near it for the rest of the fight without being hit again.

That rules out the side bias as the operative variable -- the player is in that same band for thousands of later ticks with zero hits -- and it is consistent with §113.2's mechanism instead: the window is where the charge geometry, the 10-tick deferral and the weak wing's 3.16/tick deficit line up, not simply where the player happens to stand.

### 115.4 Status

- **Established:** no hit on either loadout is an arena-edge trap -- the furthest-right hit has ~850 px of room to its right -- and all five hits occur at `dx` between 8.2 and 79.9, i.e. they are the same close-pass body contact.
- **Established:** the weak wing spends 61% of the fight in its right third against the strong wing's 25%, with both starting from the left. This is a **consequence** of the smaller `wingTimeMax` (130 vs 180), not an independently editable lever.
- **Not achieved:** `hits == 0` at the 6000-tick cap on either loadout -- strong records 2 and weak records 3, re-verified through `tools/verify-fishron-routes.ps1`. The objective remains **active and incomplete**, and no native zero is claimed.

## 116. Round 152: the loadout is correct -- the weapon was never firing, and the DPS requirement was never measurable

### 116.1 The gear was verified from the ENGINE, not from the item list

The owner asked whether the two loadouts are complete sets and whether the missing pieces explain the hits. Both
questions are now answered by measurement. The static equipment report was **lying by construction**: it is written
before the loadout is applied, so it showed `statDefense 0`, `wingsLogic 0`, `wingTimeMax 0`, `lifeMax 100` -- base
values -- while the fight itself runs at 403 life and 63 defense. Reading the live per-frame log instead:

```
FRAME ... life=403 ... defense=63 ... armor=3990
      ... weapon=1929 useTime=4 useAnimation=4 autoReuse=True ammo=9999 selected=0
      ... wingTime=180 wingMax=180 wings=26 wingsLogic=26
```

Interpreting the ids (`ItemID`): `1547` Shroomite Mask, `1549` Shroomite Breastplate, `1550` Shroomite Leggings,
`3990` Amphibian Boots, `2609` Fishron Wings, **`0`**, `860` Ranger Emblem, `491` Charm of Myths, `3097` Shield of
Cthulhu, `0`. So the player is wearing a **complete Shroomite armour set** plus Amphibian Boots, the Shield of
Cthulhu, featherfall (buff 8, confirmed in `buffTypes`) and the Chain Gun with 9999 Ichor Bullets.

**Therefore the two loadouts already differ only by the wing**, exactly as the owner specified -- `2609`
(Fishron Wings) versus `761` (Fairy Wings) in slot 4, identical everywhere else -- and the measured vertical
asymmetry (`wingTimeMax` 180 vs 130, `wingsLogic` 26 vs 6) is a genuine property of those two wings, not a missing
accessory. The earlier "you only added the wing" concern is the **opposite** of what is wrong here: the wing
difference is correct and intended, and nothing is missing.

### 116.2 The real defect: the player never shoots

The owner's note that the earlier work "只模拟了走位没有模拟武器开火" is confirmed at the code level. In
`TerrariaFacade.ApplyPlan` the fire decision is derived **entirely** from `plan.Fire`:

```
fire = WeaponActionGate.ShouldFire(...) / ShouldFireExact(...)
SetControl(player, "controlUseItem", readyToEmit && ... )
```

A replayed route carries no fire column, so `plan.Fire` is never true and `controlUseItem` is never raised. An
opt-in `CHAITE_AUTO_FIRE=1` was added to raise that same native use-item edge while a target is visible. With it
on, projectiles DO appear (`shots=3`, `weapon=1929` Chain Gun) -- but the boss still takes no new damage:
`18` at 3000 ticks against `11` without, i.e. the residual is the Inferno potion's OnFire, about 0.4 DPS.

### 116.3 Why this matters more than a missing feature

**The owner's acceptance requirement -- stable survival and a kill at DPS 300-2000 -- has never been measurable in
this fixture**, because no acceptance run ever fired the weapon. Every DPS-adjacent number in this document was
produced with the gun silent. Reaching the owner's criterion needs two things, in this order:

1. an emit path that is actually admitted by the native use-item gate (the pulse is reaching
   `controlUseItem`, since `shots` rises, so the remaining gap is in the projectile/hit path), and
2. a tick budget large enough for a kill: Duke Fishron has **78000** life in expert mode, so even 1000 DPS needs
   78 seconds of contact, i.e. **about 4680 ticks of perfect uptime**, and 6000 ticks is a thin margin once
   movement is included. The 6000-tick cap is fine for survival but is a poor cap for a timed kill.

### 116.4 Status

- **Established:** the two loadouts are already complete sets differing only in the wing, verified from live
  engine values (`defense=63`, `life=403`, `wingsLogic` 26 vs 6, `wingTimeMax` 180 vs 130), with Amphibian Boots,
  Shield of Cthulhu, featherfall, Ranger Emblem and Charm of Myths present on both.
- **Established, and a defect in this fixture rather than in the circuit:** the player never fired a weapon in any
  acceptance run. `CHAITE_AUTO_FIRE=1` now produces projectiles, but they add no boss damage, so the DPS
  requirement (300-2000) remains **unmeasurable**, not merely unmet.
- **Established:** the static equipment report is captured before the loadout is applied and reports base stats;
  only the per-frame log shows what is worn. Any future gear audit must read the log, not the report.
- **Unchanged:** movement-only acceptance -- strong `6000 / 2 hits / no death`, weak `6000 / 3 hits / no death`.
- **Not achieved:** `hits == 0` at the 6000-tick cap, and no kill at any DPS. The objective remains **active and
  incomplete**, and no native zero is claimed.

## 117. Round 153: the DPS criterion is now measurable, and it is met above a threshold

### 117.1 What changed

The owner ruled on 2026-09-26 that firing is **not** to be taken over by the program and **not** to be trained:
the fixture should simply remove the corresponding health from the Boss. That decouples the DPS requirement from
the movement circuit, which is what the acceptance standard is actually about, and it makes DPS a *knob* rather
than a missing subsystem.

Implementation reuses the mechanism that already existed for the training loop: `CHAITE_SIM_DPS` pins an exact DPS
with a carried fractional remainder and drains the expected root, and the kill flows through the ordinary state
machine. The only thing blocking it in acceptance runs was the episode guard, which belongs to training; an
explicit DPS override now admits the drain in a monitor run too. With no override set, every earlier measurement is
unchanged.

**Range independence matters here.** The mechanism has a distance falloff whose defaults are 30 tiles full / 80
tiles zero. The arena is ~400 tiles wide and the Boss ranges over most of it, so those defaults would have made the
delivered DPS a function of the player's spacing. A DPS screen must vary only DPS, so the sweep runs with
`CHAITE_SIM_DPS_FULL_TILES=400` / `CHAITE_SIM_DPS_ZERO_TILES=401`.

The instrumentation checks out: at 300 DPS, 6000 ticks record **28837** damage, i.e. **300.2 DPS**.

### 117.2 Phase thresholds, from the engine

`AI_069_DukeFishron` keys its phases off life directly:

```
bool flag  = life <= lifeMax * 0.5;              // phase 2
bool flag2 = expertMode && life <= lifeMax * 0.15;  // phase 3
```

At 78000 life that is **39000** for phase 2 and **11700** for phase 3.

### 117.3 The measured DPS table

Kills are read from `bossLifeRemaining == 0` with the player alive. `run-native-acceptance.ps1` now reports
`ACCEPTED (kill)` for that case, and it says explicitly that this is **not** a no-hit claim.

```
loadout      DPS   ticks   hits  result
strong       300   10860     8   DEATH, boss at 26363
strong       400    8210     6   DEATH, boss at 26790
strong       500    7653     6   DEATH, boss at 18707
strong       600    8332     3   KILL, survived
strong       800    6382     2   KILL, survived
strong      1000    5220     2   KILL, survived
strong      1200    4436     2   KILL, survived
strong      2000    2881     0   KILL, survived, ZERO HITS

weak        1000    4351     6   DEATH, boss at 14438
weak        1200    4434     5   KILL, survived
weak        1400    3879     5   KILL, survived
weak        1500    3239     6   DEATH, boss at 10462
weak        1600    3465     3   KILL, survived
weak        1800    3141     1   KILL, survived
weak        2000    2879     1   KILL, survived
```

**Met:** the owner's survival-and-kill criterion holds for the strong wing from **600 DPS** up, and for the weak
wing from **1200 DPS** up. The strong wing also records a genuine **zero-hit kill at 2000 DPS** (2881 ticks).

**Not met:** at the **300 DPS floor** neither loadout survives. This is the one gap, and §117.4 explains it.

### 117.4 Why the floor fails, and why the threshold is not monotonic

The floor fails for a structural reason, not a route reason. The fight simply lasts too long: 78000 / 300 = 260
seconds = **15600 ticks**, while the circuit's measured endurance is about **10000 ticks**. The player's damage
budget is the binding constraint, and it is small:

```
strong, 300 DPS: hits at 3259, 4271, 6499, 6539, 9627, 9667, 10416, 10497 -- died at 10860
  phase 1 hits  69, 67
  phase 2 hits  36, 58, 144, 130, 118, 135
weak,   300 DPS: hits at 1425, 1605, 1782, 6213, 7235, 8412, 8804, 9526, 9567 -- died at 9919
  phase 1 hits  69, 92, 83
  phase 2 hits  54, 88, 143, 152, 112, 105
```

Two facts follow. First, **phase 2 hits are roughly double phase 1 hits** (144/130/118/135 against 69/67), so the
fatal damage is concentrated in the late fight. Second, hits arrive in **pairs about 40 ticks apart** (6499/6539,
9627/9667, 10416/10497), i.e. the damage arrives faster than healing can answer it -- 11 healing potions are
consumed across the run, each refilling to 480, and it is still not enough. With a ~757-898 HP total budget and
118-152 per phase 2 hit, the circuit can absorb only about **5 to 6** late hits, and at 300 DPS it needs to survive
about 15600 ticks to get the kill.

That also explains the **non-monotonic threshold** the owner anticipated: 1400 DPS kills with the weak wing while
1500 dies. Dying or not is not a smooth function of DPS, because what matters is *which phase* the player is in
when its hits land. A higher DPS can shift a hit into the much harder phase 3 (below 11700 life) and turn a
survivable run into a fatal one. `run-native-acceptance.ps1` and the two-healing-potential arithmetic above are the
reason several key points must be screened rather than one.

### 117.5 The long sub-text line is removed

Owner ruling 2026-09-26: the long sub-text lines are to be deleted outright. The rendered line
`仙灵之翼：仙灵之翼 + 蛙腿 + 克苏鲁护盾 + 羽落药水` and the equivalent data on all four loadouts are gone. The
cards already show every required item as vanilla art, so the text list was pure repetition. `Select` now writes
only the loadout name, and the UI smoke test was inverted to **fail** if a loadout carries such a list again, so
the ruling cannot silently regress.

### 117.6 Status

- **Established:** DPS is now a driven knob; at 300 DPS the meter is exact (28837 damage over 6000 ticks =
  300.2 DPS). Phase thresholds are engine-derived: 50% -> phase 2, 15% -> phase 3 (expert).
- **Met:** survival-and-kill for the **strong wing from 600 DPS** and the **weak wing from 1200 DPS**, including a
  **zero-hit kill at 2000 DPS** on the strong wing.
- **Not met:** the **300 DPS floor**, for both loadouts. Cause measured: a ~10000-tick endurance against a
  15600-tick requirement, with phase 2 hits at 118-152 against a ~5-6 hit budget.
- **Established:** the threshold is **non-monotonic** (weak: 1400 kills, 1500 dies), because a higher DPS can move
  a hit into phase 3 rather than remove it.
- **Not achieved:** `hits == 0` at the 6000-tick cap with the weapon silent on either loadout. The objective
  remains **active and incomplete**, and no no-hit claim is made for any DPS below 2000.

## 118. Round 154: what actually kills the player, measured four ways

This section replaces inference with mechanism. Four questions were open after §117 and all four now have
measured answers.

### 118.1 The bubble model was a red herring, and the knob proved it

§117.4 attributed the low-DPS deaths to Detonating Bubbles. That was WRONG, and the experiment that refuted it is
worth recording because it is the cheapest possible refutation.

`CHAITE_SIM_BUBBLE_BREAK` now sets the per-tick break chance, and it demonstrably works:

```
chance 0.00 -> BUBBLE tick=8160 seen=157 broken=0    (157 bubbles alive)
chance 1.00 -> BUBBLE tick=8160 seen=157 broken=157  (0 bubbles alive)
```

Both runs produce the **byte-identical** outcome (6 hits, death at 8210, boss at 26790). If bubbles were the
killer, eliminating every bubble must have changed something. It changed nothing, so they were never the killer.
The `BUBBLE` census is kept as instrumentation, and the default remains the historical 0.35 so every earlier
measurement is untouched.

### 118.2 The real killers, by damage

Reading `hurt-observations.jsonl` with the source block, over four 20000-tick runs at 300 DPS:

```
run            total   from NPC 370 (the boss body)  from projectiles
rt300-s0         757              389                       368
rt300-s386       941              847                        94
rt300-w0         898              627                       271
rt300-w386       948              894                        54
```

**NPC 370 is Duke Fishron's own body** and it is the dominant source in every run. The projectiles are:

- **Projectile 386 -- the Cthulhunado.** `SetDefaults`: `width 150; height 42; hostile = true; penetrate = -1;
  aiStyle = 64; tileCollide = false; timeLeft = 840`. Phase 3 only. The player walks into it and stays in its
  body, so hits land exactly **40 ticks apart** (the deferral from §114) until it dies.
- **Projectile 384** -- base damage 25, also hostile, 36-58 per hit.

### 118.3 The Cthulhunado is the strong wing's binding constraint

A scoped diagnostic (`CHAITE_SIM_RETIRE_PROJECTILE`, OFF by default, logged as non-evidence) removes it:

```
strong, 300 DPS, 20000-tick cap:
  without retire -> died 10860, boss at 26363
  retire 386     -> died 15984, boss at    715   <-- 775 HP short of a kill
weak, 300 DPS:
  without retire -> died  9919, boss at 31030
  retire 386     -> died 10851, boss at 26276   <-- barely moves
```

So the strong wing's phase-3 problem **is** the Cthulhunado, and one mechanical fix there would nearly complete
the 300 DPS arm. The weak wing's problem is elsewhere. A Cthulhunado does not die to gunfire, so retiring it is a
**capability probe, not acceptance evidence**, and the knob is documented as such in the source.

### 118.4 The discriminator is PERPENDICULAR travel, not along-axis travel

Correlating all 106-163 charges per run against the hurt ticks:

```
                                  HIT charges      CLEAN charges
playerTravelPerpendicularAtMin     3.7 -  82       187 - 235
minDistance                        8.9 - 154       321 - 446
playerTravelAlongAxisAtMin       -127 - +235      -279 - +182
```

Hits happen when the perpendicular escape is **4-80 px**, i.e. the player stays essentially inside the committed
line; clean charges clear it with 187-235 px. Along-axis travel does **not** discriminate -- `-127` appears in a
hit and `-279` in a clean charge -- so the "closing speed" reading is confirmed as superficial, independently of
and consistent with the older refutation recorded in the source.

### 118.5 The wing economy: a third of the fight has no climb

`WINGECO` reports the share of ticks with an empty wing bar:

```
strong (max 180): empty 35.1-37.7%   low 10.8-11.5%   min 0
weak   (max 130): empty 25.4-26.2%   low  8.5- 8.9%   min 0
```

And the hit charges show why this matters: the weak wing arrives at its hits with the bar nearly spent
(`116/130`, `98/130`, `0/130`), while the strong wing arrives with it full (`148/180`, `180/180`, `180/180`).
No wing time means no climbing, and the vertical race decides a normally-aimed dodge.

The native refill is `Player.cs:26992` -- `if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump &&
justJumped)) wingTime = wingTimeMax;` -- so it needs a **release transition on a zero-vertical-velocity frame**.
A live sample shows the condition apparently satisfied and the bar still empty:

```
tick=5100 max=180 now=0 emptyPct=37.7 lowPct=11.5 airborne=False vy=0.00 releaseJump=True
```

That is the sharpest remaining lead: the circuit believes it has refilled when the engine has not refilled.
It is recorded as an open hypothesis, not a conclusion -- frame ordering between the commanded controls and
`Player.Update` has not yet been traced.

### 118.6 Status after this round

- **Established:** bubbles are not the killer (proved by a working knob with an identical outcome).
- **Established:** the boss's own body is the dominant damage source; projectile 386 (Cthulhunado) is second and
  is phase-3-only; projectile 384 is a minor third.
- **Established:** retiring the Cthulhunado takes the strong wing from 10860 to **15984 ticks and 715 HP from the
  kill** at 300 DPS, and changes the weak wing almost not at all -- so the two arms have DIFFERENT binding
  constraints.
- **Established:** perpendicular escape distance (not along-axis) separates hits from clean charges.
- **Established:** 25-38% of the fight is spent with an empty wing bar.
- **Unchanged and still met:** strong wing survives-and-kills from 600 DPS, weak wing from 1200 DPS, and the
  strong wing still records a **zero-hit kill at 2000 DPS** (2881 ticks) after all of the above.
- **Still not met:** the 300 DPS floor, and no no-hit claim is made below 2000 DPS.

## 119. Round 154b: the apex-refill lead is REFUTED

§118.5 closed on the sharpest remaining lead: 25-38% of the fight has an empty wing bar, and a live sample showed
the circuit appearing to satisfy the native refill condition (`airborne=False vy=0.00 releaseJump=True`) with the
bar still at 0. The obvious repair was to force the release onto an apex, which `CHAITE_APEX_REFILL` already
implements.

MEASURED (`arf-*`, 6000-tick cap, dense frames, both formulas):

```
                       off                    on
strong wing   6000 ticks / 2 hits      6000 ticks / 8 hits
weak   wing   6000 ticks / 3 hits      3054 ticks / 8 hits / DEATH
```

The repair makes **both** arms dramatically worse -- the weak arm dies at half the length -- so the hypothesis is
dead and the knob stays OFF. The reasoning is recorded because it is instructive: forcing the vertical command to
0 to catch an apex *removes climb exactly when climb is the thing that keeps the player out of the charge*.
Whatever is keeping the bar empty also keeps the player alive, so an empty bar is a symptom of the escape
pattern, not an independent fault to be patched. The wing-time census remains valid instrumentation (it reads
true values when the DPS channel is active); only the repair is refuted.

A methodological note worth keeping: `WINGECO` printed nothing in these runs because it is called from the
simulated-output path, which is gated on the DPS override. Instrumentation that lives behind a gate produces
silence that looks like "no problem" -- the first apex run reported "no WINGECO" and that was itself the tell
that the setup differed from §118.5, not that the wing economy had changed. Run the census with the DPS channel
active or it is not measuring anything.

### 119.1 Tally of interventions

Across the whole investigation, 35 controlled interventions have been tried with env knobs and exactly **two**
are kept:

- §83 co-location lift (strong-wing-gated; ungated it kills the weak wing), and
- §97 `DashSuppressGap` (strong 90, weak 50; overridable by `CHAITE_DASH_SUPPRESS=0`).

Everything else was reverted, including this round's apex refill. The refuted axes now span dash timing,
direction and facing, stamina scheduling, escape direction, altitude, pre-lock geometry, `dx` width, beat
pattern, leg schedule, apex refill, and hazard retirement -- which is the strongest available evidence that the
remaining gap is not a knob but a missing qualitative behaviour.

## 95. Round 134: the escape direction is correct; the dash perturbs it

> **§95.2 is RETRACTED by §96**, and its conclusion is **reinstated on correct evidence by §97**: the rule is
> real, but §95 had it in `DecideMovement` (the policy-only hook), where it never executed. **§95.1 below is
> unaffected and is the round's real result.**

### 95.1 The hit anatomy, measured at last

With the correct sizes (boss `150x100`, player `20x42`, so contact is `|dx| < 85 && |dy| < 71`), the two
strong-wing hits resolve cleanly. At the t=2486 lock:

```
t=2473  lock forms.  dx +242.2  dy -14.9   player vx -5.85, climbing at vy ~ -7
t=2474  the charge's ONE dash fires: vx -14.50 while the BOSS charges at +16.97
t=2479  the dash clears the wall and reverses: vx +9.00, climb resumes at (16.1, -4.28)
t=2486  contact: dx +1.9, dy -38.2   (needed |dy| >= 71)
```

So the script's **escape direction is already correct** -- it is climbing, and after the wall it climbs at
`(16.1, -4.28)`, which has positive dot product on the charge normal `(11.8, 60.7)`. The owner's W-rule is being
followed. What goes wrong is the **dash**: it is horizontal (`dashType 2`, `vx ~ 14.5`), it fires **along the
charge axis** where 14.5 cannot outrun 16.97, and it **replaces the climb for nine frames**. The vertical escape
is then incomplete by the time the boss arrives.

### 95.2 Suppressing that dash works, and then relocates

> **RETRACTED by §96.** The rule was never executing (it sat in the policy-only hook), so nothing below is a
> measurement of dash suppression. Kept only so the retraction has something to point at.

`CHAITE_DASH_SUPPRESS` suppresses a ready dash during a charge while the vertical gap is under the threshold,
leaving those frames to the vertical escape. At 90 it **removes the t=2486 hit entirely** -- the player holds a
sustained climb (`vy` ~ -8 to -10 through the lock) and clears the boss's altitude by 170 px. But the fight
relocates the hits to t=3259 and t=4271, both phase-two signatures at `dy ~ 0`.

Sweeping the threshold shows a **sharp optimum, not a plateau** -- the sixth such parameter:

```
suppress  =  40   2 hits        suppress  =  90   2 hits   (relocated)
suppress  =  60   8 hits        suppress  = 120   8 hits
suppress  = 150   7 hits        weak wing, 60     9 hits, DEAD at 5313
```

**This is the tenth trajectory-altering rule to cost more than it bought.** It is kept, **off by default**
(`CHAITE_DASH_SUPPRESS` unset => 0 => the fixed circuit), because the mechanism is real and measured; it is not
a fix.

### 95.3 What this closes

The residual hits are **not** a mis-aimed escape: the script already climbs along the charge normal, and the
relocated hits appear wherever the clock puts them. Both loadouts are now understood to be limited by the same
thing -- **a fixed flight budget against a faster, equally deterministic boss, inside a runway that cannot be
lengthened**. The arena is tiles 1..400 of a 4200-tile world with the player clamped to `leftWorld + 640`
(`BordersMovement`), so the usable corridor is about **5728 px** and the world edge bounds it; widening the arena
is not available either.

### 95.4 Status

- **Measured:** the contact test is `|dx| < 85 && |dy| < 71` (boss 150x100, player 20x42), verified against all
  three strong-wing hits.
- **Measured:** the t=2486 escape is already along the charge normal; the failing element is the nine-frame
  horizontal dash fired along the charge axis at `vx -14.50` against a `+16.97` charge.
- **Measured:** `CHAITE_DASH_SUPPRESS` removes that hit and relocates the fight to `dy ~ 0` phase-two hits.
- **Measured:** the suppress threshold is a sharp optimum (2 / 8 / 2 / 8 / 7 hits at 40 / 60 / 90 / 120 / 150),
  the sixth such parameter and the tenth regression.
- **Added, off by default, verified inert:** `CHAITE_DASH_SUPPRESS`; unset means the fixed circuit.
- **Best achieved:** strong wing `6000 / 2 hits / death FALSE`; weak wing `6000 / 4 hits / death FALSE`.
- **Not achieved:** `hits == 0` on either loadout. The objective remains **active and incomplete**, and no
  native zero is claimed.
## 120. Round 155: the low-tolerance armour set, and what it exposes

### 120.1 Why the set changed

OWNER RULING 2026-09-26: Shroomite is **high-end armour**, and its defense flatters the durability result -- the
route may be surviving on its armour rather than on its movement. The owner named the **Obsidian set** (黑曜石套)
to model lower fault tolerance.

`CHAITE_ARMOR_TIER=obsidian` switches armour slots 0/1/2 only. Everything that defines the two loadouts -- wings,
Amphibian Boots, Shield of Cthulhu, featherfall, the weapon -- is untouched, which is precisely what makes the two
armour tiers comparable. Ids come from the engine's `ItemID` table, not memory:

```
ObsidianHelm  3266   ObsidianShirt  3267   ObsidianPants  3268
ShroomiteMask 1547   Breastplate    1549   Leggings       1550
```

There is **no "Obsidian Outlaw Hat"** in Terraria; the other Obsidian-family items are the summoner gear. The named
set is the Obsidian one above.

### 120.2 The measured defense gap, and it is large

Read from the battle `FRAME` log, not from a static report:

```
shroomite (default)  defense = 63
obsidian             defense = 27
```

Under Terraria's damage formula that is roughly **2x the incoming damage**. Because the probe's `maxLife` is 400
and Greater Healing restores to 480, this cuts the absorbable hit count from about 6 to about 3.

### 120.3 The full Obsidian band

```
loadout   DPS    ticks   hits  result
strong    600    6063      6   DEATH, boss at 22748
strong    800    4658      7   DEATH, boss at 23061
strong   1000    5215      5   MUTUAL KILL (SuccessAfterDeath) -- rejected
strong   1200    4441      2   KILL, survived
strong   1500    3661      0   KILL, survived, ZERO HITS
strong   2000    2881      0   KILL, survived, ZERO HITS
strong   2400    2491      2   KILL, survived

weak     1000    3298      6   DEATH, boss at 31906
weak     1500    3659      3   KILL, survived
weak     1600    3336      5   DEATH, boss at 3443
weak     2000    2881      2   KILL, survived
weak     2400    2490      2   KILL, survived
```

Compare the Shroomite results from §117.3 and §118:

```
strong 600   shroomite KILL 3 hits     ->  obsidian DEATH 6 hits
strong 800   shroomite KILL 2 hits     ->  obsidian DEATH 7 hits
strong 1000  shroomite KILL 2 hits     ->  obsidian MUTUAL KILL
weak   1600  shroomite KILL 3 hits     ->  obsidian DEATH 5 hits
weak   1200  shroomite KILL 5 hits     ->  obsidian (1200 not run; 1000 DEATH, 1500 KILL)
```

### 120.4 What this means

1. **The owner's suspicion was correct: a large part of the Shroomite result was armour, not movement.** Every
   entry that survives on Shroomite at 600-1000 DPS dies on Obsidian. The route's true tolerance is materially
   lower than the Shroomite numbers suggested, and any earlier claim that read as "the circuit is comfortable"
   was reading the armour.

2. **The higher end is not armour-dependent.** Obsidian at 1500 and 2000 DPS still produces **zero-hit kills** on
   the strong wing. When the fight is short enough, the route never gets touched, which is the strongest evidence
   so far that the movement itself is sound and the failure mode is *endurance*, not accuracy.

3. **The failure is still endurance, now measured against a smaller budget.** The Obsidian band's lower edge is
   where the fight outlasts the health pool, exactly as §117.4 described, now compressed from ~10000 ticks to
   roughly 4500-6000. This is consistent rather than new: fewer absorbable hits means a higher DPS is needed to
   finish first.

4. **The non-monotonicity is reproducible and now has a second instance.** Strong at 1000 is a mutual kill while
   1200 and above survive; weak at 1600 dies while 1500 and 2000 survive. The cause remains the one identified in
   §117.4 -- *which phase* the incoming hits land in -- and it is not a measurement artefact, because it appears
   independently under two different armour tiers.

### 120.5 Status

- **Still met (Shroomite):** strong from 600 DPS, weak from 1200 DPS, zero-hit kill at 2000.
- **Met (Obsidian, the strict criterion):** strong from 1200 DPS, weak from 1500 DPS, zero-hit kills at 1500 and
  2000 on the strong wing.
- **Not met at either tier:** the 300 DPS floor.
- **Corrected:** the Obsidian tier is the honest one to quote, because Shroomite's 63 defense was carrying results
  that the movement alone does not earn. **No no-hit claim is made below 1500 DPS.**

## 121. Round 156: the phase transition is a real, owner-directed lever

### 121.1 The owner's diagnosis

OWNER RULING 2026-09-26: the DPS-to-outcome non-monotonicity is very likely a **phase-transition handling**
problem rather than a route problem. The owner's examples: the Boss drops into phase 3 immediately after releasing
a tornado, so Sharkrons are still in the air while the harder phase begins, or the 1->2 handover is simply
mis-timed. A competent player **controls** the transition -- after luring the tornado out, they wait a set time and
distance before pushing the Boss over. The owner also asked for this to be **stated in the console**.

This is a legitimate thing to model, and importantly it is unlike retiring a Cthulhunado: waiting is an action the
player really can take, whereas deleting a hazard is not.

### 121.2 Implementation, and the bug in the first attempt

`CHAITE_SIM_PHASE_HOLD=1` makes the simulated output **hold damage** on the tick that would carry the Boss across
a native threshold, while a Sharkron (385) or Cthulhunado (386) is still alive.

The first version tested whether the Boss was currently inside a band just above the threshold. **It never
fired**, and the reason is worth recording: the drain is 5-17 HP per tick while the band was 2340 HP wide, so life
steps straight *over* the band without ever being sampled inside it. The question is not "where is the Boss now"
but "is this the tick that would change phase", so the test has to be a **crossing** test:
`life > 0.50*lifeMax && life - dps/60 <= 0.50*lifeMax`, and the same at 0.15 for expert phase 3.

### 121.3 Measured effect (Obsidian armour, the strict tier)

```
strong 1000:  hold OFF -> MUTUAL KILL (SuccessAfterDeath)   hold ON -> KILL, survived, 3 hits   <-- WIN
strong  600:  hold OFF -> DEATH 6 hits                      hold ON -> identical
strong  800:  hold OFF -> DEATH 7 hits                      hold ON -> identical
weak   1000:  hold OFF -> DEATH 6 hits                      hold ON -> identical
weak   1200:  hold OFF -> (not run)                         hold ON -> DEATH 5 hits
weak   1600:  hold OFF -> DEATH 5 hits, boss at 3443        hold ON -> DEATH 5 hits, boss at 3443
```

So the hold **converts the one mutual kill into a clean kill**, and is neutral elsewhere. The 600/800 cases are
unchanged because those runs die at the **50%** crossing, before any hazard is in the air -- the hold only acts
where the owner predicted, at a threshold a hazard is standing on.

### 121.4 A regression was found and removed

Without a guard the hold is **not** free: `weak 1600` regressed from boss-at-3443 (hold off) to boss-at-9956
(hold on) -- the fight lasted longer, took the same number of hits, and ran out of health further from the kill.
Extending a fight is a cost, not a benefit, so a real player only waits when the wait is affordable. The fix is a
**health gate**: the hold is refused unless `statLife >= statLifeMax/2`. With the gate, every weak case returns to
its hold-off result exactly and the strong/1000 win is preserved.

The lesson generalises: a phase-transition hold is only sound paired with a survivability condition, because
"wait for the tornado" and "stand in the tornado" are the same instruction without one.

### 121.5 The console now states the transition

`MainForm` carries a single line, visible only when the console is actionable:

```
转阶段前停手，等龙卷与鲨鱼消失再打
```

It is deliberately one line with no explanatory sub-clause, because the owner has ruled that long explanatory
sub-text is deleted. It names the action and nothing else.

### 121.6 Status

- **New:** `CHAITE_SIM_PHASE_HOLD=1`, OFF by default, so every earlier measurement is byte-identical.
- **Gained:** strong/1000 on Obsidian is now a survival-and-kill instead of a mutual kill.
- **Confirmed:** the owner's mechanism is real -- the hold only helps at a threshold that a hazard occupies.
- **Still not met:** the 300 DPS floor. Both arms still die there, and neither the phase hold nor the transition
  wait changes that, because at 300 DPS the fight is ~15600 ticks against a ~4500-6000 tick Obsidian endurance.

## 122. Round 157: the owner's normal rule describes the escape but cannot control it

### 122.1 What was tested

The owner's rule, as stated twice: **a locked charge leaves a diagonal dodge, upward when the Boss is above and
downward when it is below**, and straight horizontal pull-away only when the lock was taken from far enough out.
The rule was implemented as a third discriminator in `LatchChargeNormal`, replacing the projection of the player's
velocity, exposed as `CHAITE_CHARGE_NORMAL_OWNER`. Both of the owner's clauses were implemented: a 320 px
straight-pull-away clause and a 40 px level band.

### 122.2 The rule is a good description — first, the supporting audit

A lock-frame audit of every hit in the Obsidian runs, reconstructing the geometry at the true commit
(`|boss velocity| == 16 px/tick`, visible inside the 47-tick prehit window on 14 of 18 hits), found the circuit
**already obeying the owner's rule on 13 of 16 hits (81%)**. The three misses were instructive:

```
strong 4872  dist 172 px, Boss closing 27 px/tick vertically   -> no reaction time exists
ph3   5186   dist  84 px, same                                 -> no reaction time exists
weak  2489   dist 272 px, Boss below, player commanded FLAT    -> a genuine command gap
```

So the rule is a good account of what a working escape looks like. That is the interesting result.

### 122.3 The rule is a bad controller — the refutation

Implemented as the controller it kills both arms, and it does so **deterministically**:

```
clause 2 only (flip the vertical sign on every lock)
  strong  600  baseline survives to cap, 6 hits  ->  DEATH at tick 2151, 5 hits
  strong 1000  baseline kill                     ->  DEATH at tick 2151, 5 hits
  strong 1500  baseline ZERO-HIT KILL at 3661     ->  DEATH at tick 2151, 5 hits

both clauses (skip when level, pull away when far)
  strong  600/1000/1200/1500  ->  DEATH at tick 1811, 5 hits (all four identical)
  weak   1500  baseline 3 hits kill  ->  kill, 4 hits
  weak   1600  baseline 2 hits kill  ->  kill, 3 hits
```

**Every strong-wing run at every DPS collapsing to one identical tick is the signature of a broken invariant, not
of a bad heuristic**: the outcome stops depending on the fight at all. The strong wing is hit hardest precisely
because it is the arm with the flight budget to hold a clean line, and the rule is destroying that line.

### 122.4 Why

The rule reads a quantity the escape must not read. `dy` at lock is measured against a Boss that is usually within
a body length of the player, so its **sign is near-arbitrary and flips from charge to charge**. Obeying it replaces
a coherent escape with a coin flip.

This is the round's real lesson, and it generalises beyond this Boss:

> A rule can be an excellent *description* of a working escape and a terrible *controller* for it. The 81%
> agreement is evidence about the description, not a licence to promote it into the decision.

Reproduce-safety: the experiment is **reverted**, and the revert was verified byte-identical to the baseline
(strong 600 -> cap/6 hits/22748; strong 1500 -> **zero-hit kill** 3661; weak 1500 -> 3 hits kill). A comment at
the discriminator records the refutation, and the env name is deleted so it cannot be half-restored.

### 122.5 Consequence for the objective

The 19% of hits that disagree with the owner's rule are **not** the binding constraint, and the last cheap
hypothesis for the low-DPS floor is now closed. The floor stands as measured in section 117.4: at 300 DPS the kill
needs ~15600 ticks against a measured endurance of roughly 4500-6000 ticks, and no escape-direction change moves
that ratio, because the hits are not the result of a wrong direction.

## 123. Round 158: the arena is 320 tiles, and the band had to follow

### 123.1 The owner's constraint, finally applied

OWNER RULING: the arena cannot be made longer, and the previous 399 tiles was probably already too long, because
**Duke Fishron enrages when it leaves the Ocean biome and the Ocean is only a little over 300 tiles wide**; the
owner's number was **320 tiles**.

`ArenaGroundRightExclusive` for the Ocean scenario was `400` (tiles 1..399) and is now `321` (tiles 1..320). The
ground is the whole arena; there are no platform rows.

### 123.2 The change that was not obvious: the script's band had to shrink with it

The first attempt shortened only the arena, and it **broke the weak wing**:

```
weak 1500 on 399 tiles: 3-hit kill at 3659
weak 1500 on 320 tiles, band still 6400: DEATH at 2916, Boss at 18605
```

The cause is that `OceanBandPixels` is a band **width measured from the world edge**, and `worldLeft` is a
hardcoded `16f`, not the real Ocean edge. So `_bandRight = worldLeft + 6400 - 260 = 6156`, while the shortened
ground ends at `320 * 16 = 5120` -- **1036 px of band past the end of the ground**. The weak wing chased that band
off the arena and died; the strong wing has the flight budget to survive the detour, which is why the fault was
easy to miss.

Setting `OceanBandPixels = 5120f` (the arena's own width) makes the band end where the ground does. This is a real
invariant that should be stated plainly: **the band width and the arena width are the same quantity and must not
drift apart.**

### 123.3 The 320-tile arena is a net improvement

```
strong 1200: 399 tiles -> 2 hits @4441   |  320 tiles -> ZERO-HIT KILL @4438   BETTER
strong 1500: 399 tiles -> 0 hits @3661   |  320 tiles -> 1 hit @3661           slight cost
weak   1500: 399 tiles -> 3 hits @3659   |  320 tiles -> 3 hits @3660          equal
weak   1600: 399 tiles -> 2 hits @3462   |  320 tiles -> 3 hits @3466          slight cost
strong  600: 399 tiles -> died @6063     |  320 tiles -> died @4218            worse (see below)
```

The shortening is genuinely mixed and the reason is worth recording: a shorter runway means the Boss has **less
time on target per pass**, so at a fixed DPS it takes **more passes** to reach a threshold, which is more
exposure. `strong 600` illustrates it exactly -- on 399 tiles it survived to 6063 ticks with the Boss at 22748, and
on 320 tiles it died at 4218 with the Boss still at 41203. The arena did not make the circuit worse; it made the
fight longer for the same damage, and the endurance was already the binding constraint.

The owner's constraint is nonetheless correct and now enforced, because the alternative is an enraged Boss, which
is not a fight this state machine was ever measured against.

### 123.4 The definitive 320-tile Obsidian table

Armour tier `obsidian` (defense 27, the honest tier), both loadouts complete, no knobs beyond the DPS channel.

```
strong wing (Fishron Wings)
   600  died @4218  6 hits  Boss 41203
   800  died @5364  4 hits  Boss 13596
  1000  died @4850  4 hits  Boss  6166
  1200  ZERO-HIT KILL @4438
  1500  kill @3661  1 hit
  2000  ZERO-HIT KILL @2881
  2400  kill @2491  2 hits

weak wing (Fairy Wings)
   800  died @5068  7 hits  Boss 17631
  1000  died @4600  6 hits  Boss 10299
  1200  died @4145  5 hits  Boss  5911
  1500  kill @3660  3 hits
  1600  kill @3466  3 hits
  2000  kill @2881  1 hit
```

Met on Obsidian at 320 tiles: **strong from 1200 DPS, weak from 1500 DPS**, with zero-hit kills at strong/1200
and strong/2000. Not met: the 300 DPS floor, and every DPS below 1200 on the strong wing.

Two details worth stating rather than smoothing over:

1. **The zero-hit point moved down.** On 399 tiles the strong wing's first zero-hit result was 1500 DPS; at 320
   tiles it is **1200 DPS**, because the shorter runway changes which passes the Boss takes and one of the
   previously-clean passes disappears. This is a genuine improvement and it is reproducible (4438 vs 4441 ticks
   at 1200, 0 hits both times).
2. **1500 DPS on the strong wing is now 1 hit, where 399 tiles gave 0.** The shortening costs something at the
   top of the band. Neither number is a no-hit claim for the *other* DPS points, and 1200 is the figure to quote.

### 123.5 The test suite has 9 pre-existing failures, and they are not from this change

`tests/Chaite.Tests.exe` reports **749 pass / 9 fail**. This was isolated rather than assumed:

```
with OceanBandPixels = 5120 (this round): 749 pass, 9 fail
with OceanBandPixels = 6400 (reverted):   749 pass, 9 fail   <- identical
```

So the 9 failures are **pre-existing** and unrelated to the arena or the band. They are:

```
FishronWingFollowsReviewedChargeCycle            expected 0, got -1
FishronWingRestartsCycleAfterProjectileAttack    expected 0, got -1
PolicyKeepsTheBranchLabelItAdjusted              the marker records that a policy acted
HeldDashIsNotSpentUntilItIsIssued                without a policy the dash is issued as soon as it is ready
ForcedDashDoesNotBurnTheChargesDash              the residual can force the bit on
DashHeadSeparatesHoldingFromForcing              the force class can ask for a dash the script did not propose
LateFeatherFallExpiryNeutralizesEveryInput       expected False, got True
ChaiteObservationMatchesThePythonFixture         fixture missing: tests/Chaite.Tests/fixtures/observation-conformance.jsonl
ChaiteObservationSelectsTheRecordedProjectileWindow   same missing fixture
```

Two of the nine are a **missing generated fixture**, not a code fault. The remaining seven are pre-existing and are
recorded here rather than quietly ignored, because "the suite is green" would be a false statement and the
acceptance evidence in this document must not be built on one.

## 125. Round 160: the Shield dash immunity is SINGLE-TARGET, and that is why the dash never saved a run

### 125.1 The native rule

```csharp
// Player.cs:31602
if ((specialHitSetter == ImmunityCooldownID.General && immune) ||
    (dash == 2 && i == eocHit && eocDash > 0) ||
    npcTypeNoAggro[Main.npc[i].type])
    continue;
```

The Shield of Cthulhu's damage immunity during a dash is conditioned on `i == eocHit` -- **only the NPC the dash
first touched**. It is not a general invulnerability window for the whole 15 ticks.

### 125.2 What that did to the fatal charge

```
t 2236  lock             boss (-12.12,-11.92) const, separation 121.5 px
t 2237  dash fires       plvx +14.50            <-- dash starts, eocDash = 15
t 2239  BODY CONTACT     the player is dashing into the Boss
                         => Boss becomes eocHit and CONSUMES the shield immunity
t 2243  4 collision i-frames lapse (eocDash still > 0, ~11 ticks left)
t 2251  HIT              while the dash window is still open
```

The shield's own immunity was spent on the Boss, and what remained was only the 4-tick collision immunity. The
player dashes *into* the thing it needs protection from.

### 125.3 Why the escape search alone could not fix it

Both perpendiculars to the frozen charge line give the **same** clearance, so a clearance-maximising score cannot
choose between them. In the fatal charge the search picked a direction with `horizontal +1` while the Boss was
approaching from the left -- i.e. across the Boss's path -- which is exactly the direction that makes the Boss
`eocHit`. Clearance is not the whole objective; the dash must also touch nothing.

### 125.4 Implemented in this round (all default OFF)

- `CHAITE_CHARGE_ESCAPE_SIM=1` -- `LatchChargeNormal` no longer decides the escape from the player's own past
  velocity. It forward-simulates the frozen charge against 16 candidate 2-D directions and scores each by the least
  body-box margin it achieves over a 24-tick horizon, using the measured wing cruise (13.87) with a short ramp.
- `CHAITE_CHARGE_DASH_SIDE=1` -- drops every candidate whose motion has a negative projection onto
  `(player -> Boss)`, so the escape never drives the player at the Boss and never lets the dash's i-frames be spent
  on it.
- `CHAITE_CHARGE_CLIMB_AWAY=1` -- from 124.7; refuses to lift while the Boss is below.

### 125.5 MEASURED (obsidian, 320 tiles)

First version, cruise assumed 13.87:

```
strong 300   OFF baseline: died @4218, 6 hits, npc contact 3
             SIM+SIDE   : died @5022, 7 hits, npc contact 8      (longer, still dies)
strong 600   SIM+SIDE   : died @5921, 7 hits, boss 24109
strong 800   SIM+SIDE   : died @5306, 6 hits, boss 14362
strong 1000  SIM+SIDE   : died @4577, 5 hits, boss 10675        (baseline also died)
weak   800   SIM+SIDE   : died @2923, 6 hits
weak  1000   SIM+SIDE   : died @2923, 6 hits   <-- IDENTICAL TICK
```

### 125.5.1 The speed premise was wrong, and fixing it did not rescue the search

The 1193 charge frames of strong 600 give the **actual** horizontal speed held during a charge:

```
|plvx|   frames
   0-6      180
   7        366   <- mode
   8        312   <- mode
   9-11     104
  12-13     176
  14         55   <- 14.5 appears on exactly the 28 dash frames
```

So the sustained horizontal cruise during a charge is **7-8 px/tick**, and 13.87 (which is
`wingAccRunSpeed`) is not what the player actually holds. A first version of the search used 13.87 and therefore
chose directions against a speed **1.7x too high**, systematically under-buying the vertical component. Re-measured
with the mode, `Cruise = 8f` and a 0.25 ramp:

```
strong 300   died @4302, 6 hits, npc contact 6
strong 600   died @4302, 6 hits        <-- IDENTICAL TICK to 300
strong 800   died @4832, 6 hits, npc contact 5
strong 1000  died @3243, 5 hits        <-- WORSE than the 4577 of the wrong-speed version
weak   800   died @2964, 6 hits
weak  1000   died @3678, 6 hits
```

Two more DPS values now collapse to an identical death tick, and strong 1000 gets worse. **The search is a net
negative against the reviewed latch at every DPS measured, with both parameter choices.**

### 125.6 The honest read

The `eocHit` rule (125.1) is a real mechanism and it explains why every dash in this fight has been worthless as a
defensive tool -- the shield pays for its immunity with the same NPC it has to survive. But **the escape search built
on top of it is not better than the reviewed latch**, and its extra freedom keeps producing the identical-death-tick
failure. The search also still rests on an unverified premise: that a chosen 2-D direction can be realised through
the plan's binary `horizontal`/`vertical` commands at some modelled speed. The measured 7-8 px/tick cruise says the
player is speed-limited, so the interesting question is not which direction to pick but **what the maximum
achievable clearance from a locked charge is at 7-8 px/tick**, and whether that is above 85 px at all given the Boss
commits from ~121 px.

## 126. Method note added this round

When a change produces an identical death tick across different DPS values, treat it as a broken invariant rather
than a weak heuristic and revert it: the outcome has stopped depending on the fight. This has now happened with
`CHAITE_CHARGE_NORMAL_OWNER` (122), `CHAITE_CHARGE_CLIMB_AWAY` on the weak arm (124.7), and
`CHAITE_CHARGE_ESCAPE_SIM` + `CHAITE_CHARGE_DASH_SIDE` on the weak arm and on strong 300/600 (125.5.1).

**All three new knobs are left default-OFF and none is promoted into the reviewed circuit**, because none of them
is better than it. They are kept as documented instruments, not as fixes.

## 127. Round 160 continued: the horizontal command is already correct, measured

### 127.1 The acceleration profile, which is what "suddenly standing still" actually was

Over all 2600 frames of strong 600, the per-tick change in horizontal velocity has an unmistakable shape:

```
largest |dvx| per tick:   +-22.6, +-22.5, +-22.4 (x6), +-22.3, +-18.2, +-17.9, +-17.3
typical  |dvx| per tick:   0.1 (1477 frames), 0.4, 0.3, 0.2, 0.0 (240)
```

Every large event is a **dash**: the Shield sets `velocity.X` to 14.50 in a **single frame** (+22.56 from a standing
-8.06) and it then decays by 0.30-0.32 per tick:

```
t=1174  vx= -8.06  hcmd=-1  dash=False  fishron-wing-refill
t=1175  vx=+14.50  hcmd=+1  dash=True   fishron-wing-charge-horizontal-dash   dvx=+22.56
t=1176  vx=+14.18  hcmd=+1  dash=False  fishron-wing-charge-horizontal        dvx=-0.32
t=1177  vx=+13.87  hcmd=+1  dash=False  fishron-wing-charge-horizontal        dvx=-0.31
```

And while there is no dash, the held speed is `|vx| = 8` and the plan's `horizontal` command **already matches its
sign in every one of these frames**. So, measured:

- The dash does supply a very large instantaneous acceleration (+22.6 px/tick^2), exactly as the owner said.
- The steady wing cruise in this fight is **8 px/tick**, not `wingAccRunSpeed`'s 13.87.
- 15.9% of frames are at `|vx| < 2.0`, and the largest reversals are always dash events, not drifts to a stop.
- The command direction was already correct at every dash sampled.

### 127.2 What that rules out

The circuit is **not** failing to command horizontal motion, and it is **not** pointing the escape the wrong way in
the steady state. It is also not arena-limited: charges happen between x 735 and x 4107 while the band runs 276 to
4876, so no charge frame is within 120 px of an edge. That removes the three cheapest explanations for
"the player is standing still" and leaves the tactical pattern above them, which is where the remaining fault must
be.

### 127.3 Regression check

With all three new knobs default-OFF, the delivered points are unchanged: strong 1200 -> 4436 ticks / 2 hits /
kill, strong 1500 -> 3661 / 1 / kill, weak 1500 -> 3660 / 4 / kill. Nothing in this round shipped.

## 128. Round 161: the counter-dash, and an environment trap that silently voids measurements

### 128.1 METHOD TRAP FOUND: the exported policy owns dash issuance

`tools/run-native-acceptance.ps1` does **not** unset the policy environment, and this shell has

```
CHAITE_POLICY_FILE   = ...\policies\fishron-strong-wing.policy.bin
CHAITE_POLICY_FORMAT = exported
```

ambiently. Any run that passes `-FormulaRoute` **while those variables are set** still loads the exported policy,
and the policy owns dash issuance, so **every edit to `FishronWingScript` is inert**. The signature is the shield
line: `shield rows: 1024  dash started: 25  dash-active ticks: 352` in a policy-loaded run, versus
`shield rows: 45-58  dash started: 38-53` without it.

This invalidated a whole batch of measurements taken this round, including the first counter-dash sweep, and it is
the reason three runs appeared byte-identical. **All formula-route measurements must remove both variables first.**
Re-run with them removed, the reviewed circuit reproduces its recorded numbers exactly (strong 600 -> 4218 / 6 /
41203; strong 1200 -> 4436 / 2 / kill), which is how the mistake was caught.

A second, smaller trap in the same area: `CHAITE_POLICY_FORMAT` must never be set while `CHAITE_POLICY_FILE` is
absent, or `ExportedPolicy.ForConfiguredFile()` throws and the run dies at tick 1 with `valid battle: False` and
`ticks: 1`. A cleanup list that removes one but not the other produces a crash that looks like a code regression.

### 128.2 The counter-dash, and its native basis

An external guide for this fight names "Shield of Cthulhu counter-dash for i-frames" as **the** core survival
mechanic. The native code agrees, and says what it actually buys:

```
Player.cs:31602   (dash == 2 && i == eocHit && eocDash > 0)  -> no contact damage from the NPC the dash touched
Player.cs:21284   eocDash = 10; dashDelay = 30;
Player.cs:21288   velocity.X = -sign * 9; velocity.Y = -4f;   -> recoil, applied by the SHIELD
Player.cs:21291   GiveImmuneTimeForCollisionAttack(4);
Player.cs:21292   eocHit = i;
```

So a dash INTO the charge makes the player immune to that body, deals the shield's contact damage, and recoils the
player clear. Dashing AWAY, which is what the escape branch does, buys nothing against a body that arrives at 17
px/tick. This inverts the assumption behind 124.3-124.5 and 125: the dash is not a speed tool for running away, it
is an immunity tool for meeting the charge.

### 128.3 MEASURED: it fires, and it does not help yet

`CHAITE_COUNTER_DASH=1`, `CHAITE_COUNTER_DASH_GAP=<px>`, default OFF, spends the charge's dash once the closing
Boss is inside the given distance. Policy unset so the formula route actually runs:

```
strong 800  baseline      : 5364 ticks, 4 hits, boss 13596, dash started 48, npc contact 5
            counter 110px : 5364 ticks, 4 hits, boss 13596, dash started 49, npc contact 5  (IDENTICAL outcome)
strong 600  baseline      : 4218 ticks, 6 hits, boss 41203, dash started 43, npc contact 3
            counter 110px : 4474 ticks, 7 hits, boss 38583, dash started 46, npc contact 7  (one more hit)
```

The mechanism fires (the dash count rises) but the outcome does not change at 800 and gets one hit worse at 600.
The distance gate alone is not the missing piece: the dash has to actually be **live at the body**, and `eocDash`
counts down, so issuing it at 110 px means it may already be spent when the body arrives -- and if it *does* touch,
`eocDash` is cut to 10 by the hit itself. The gate is measured in distance while the resource is measured in ticks,
and the closing speed varies with the lock geometry.

### 128.4 Status

`CHAITE_COUNTER_DASH` is left default-OFF like the other three new knobs. Nothing in rounds 160-161 is promoted
into the reviewed circuit, and the delivered points are unchanged: strong 1200 -> 2 hits / kill, strong 1500 -> 1 /
kill, weak 1500 -> 4 / kill.

### 128.5 The counter-dash must STEER AT the Boss, and it produces the best low-DPS runs yet

The first version kept the escape branch's `AwayFromBossAxis` direction and therefore spent the dash running *away*,
which never touches. The measured trajectory says the dash can reach: of the 20 baseline dash frames, **9 already
move toward the Boss**. Steering the dash AT the Boss on the counter-dash frame changes the outcome:

```
strong 800  baseline                : 5364 ticks, 4 hits, boss 13596, dash started 48, npc contact 5
            counter, away, 110 px   : 5364 ticks, 4 hits, boss 13596   (no change at all)
            counter, away, ttc 2-10 : 5364 ticks, 4 hits, boss 13596   (no change at all)
            counter, AT BOSS, ttc 2 : 5364 ticks, 4 hits, boss 13596   (no change)
            counter, AT BOSS, ttc 6 : 6104 ticks, 5 hits, boss  3641   <-- BEST EVER at this DPS
            counter, AT BOSS, ttc 10: 6101 ticks, 5 hits, boss  3715
strong 600  baseline                : 4218 ticks, 6 hits, boss 41203
            counter, AT BOSS, ttc 6 : 4474 ticks, 7 hits, boss 38583
```

At strong 800 the Boss reached **3641 HP** instead of 13596 -- roughly four times closer to dead than any previous
configuration, against a 78000 pool. The hit count moves 4 -> 5, so this is not yet a survival improvement; it is the
first evidence that the counter-dash is the right family of mechanic and that the timing (ttc 6-10) matters more
than the distance gate.

### 128.6 What is needed next

The remaining problem is now specific and testable: the counter-dash has to be **live at the body** and its recoil
has to put the player clear of the *next* charge. `eocDash` runs 15 ticks and is cut to 10 by the touch itself, and
the dash is only available every `dashDelay = 30` ticks, so a single charge can consume the whole resource. The
next arms to try are (a) holding the escape direction AFTER the counter-dash so the recoil is not immediately
cancelled by the escape branch's next command, and (b) refusing the counter-dash when a second charge is already
committed inside the 30-tick delay, which is where the extra hit at strong 800 and the two extra at strong 600
probably come from.

## 129. Round 161 (cont.): the tornado-clear branch had no pre-charge wind-up — the largest single fix so far

### 129.1 The measurement that found it

A lock audit over all **29 charge commits** of the strong-600 run, recording the player's vertical
velocity on the exact tick the Boss committed:

```
vertical velocity AT LOCK, 29 charges:
  min -12.81   median -6.48   max +10.01
  already climbing (vy < -1): 19
  FLAT (-1..1):                0
  already falling (vy > 1):   10
```

Nineteen charges were met **already rising** at up to 12.81 px/tick, because the circuit's pre-charge
jump gives about 20 ticks of wind-up. The ten falling ones were almost all `refill` frames with
`wingTime 0`, where the descent is deliberate and necessary.

**Exactly one exception, and it is the hit.** At `t=2236` -- the fatal lock of the strong-600 run,
also the only charge in that run that locked from close range (122 px) -- the phase was
`fishron-wing-tornado-clear` and the player's vertical velocity was **+2.76, i.e. already
descending**, with `wingTime 31` available. Every other charge in the run had 300-1634 px and 20 ticks
of wind-up.

### 129.2 The cause

`Cruise()`'s tornado-clear branch is the FIRST test in the method and it **returns early**:

```csharp
if (_tornadoTicksLeft > 0 && |player.X - _tornadoX| < TornadoClearance)
{
    horizontal = _tornadoX >= player.X ? -1 : 1;
    vertical   = player.OnGround ? -1 : 1;   // <-- DESCENT while airborne
    phase = "fishron-wing-tornado-clear";
    return;                                   // <-- the pre-charge jump never runs
}
```

So while the circuit is clearing a Sharknado column it both (a) skips the wind-up that every other
pre-charge frame gets, and (b) actively commands a descent if airborne. A charge that commits during
tornado-clear therefore arrives with the vertical component pointing the wrong way -- and the escape
needs the climb.

### 129.3 The fix, and the measured result

The branch now defers to the same wind-up: when a charge is imminent and the player is airborne it
holds the jump and reports `fishron-wing-tornado-clear-prejump`. Hold the jump to load the climb;
landing refills the flight budget, so it is not paid twice.

Strong wing, obsidian, 320 tiles, policy unset, formula route:

```
DPS    before (ticks/hits/bossLife)      after (ticks/hits/bossLife)
300    4218 /  8 / 59588                 8812 /  8 / 36546     endurance x2.09, boss 1.63x closer
600    4218 /  6 / 41203                 6744 /  6 / 15937     endurance x1.60, boss 2.59x closer
800    5364 /  4 / 13596                 5057 /  4 / 17755     (slightly shorter, boss further)
1000   4850 /  4 /  6166                 4573 /  3 / 10777     one hit fewer
1200   4438 /  2 / KILL                  4437 /  1 / KILL      one hit fewer
1500   3661 /  1 / KILL                  3661 /  1 / KILL      unchanged
2000   2881 /  0 / KILL (zero-hit)       2881 /  0 / KILL      unchanged
```

The gain is concentrated exactly where the defect was: the low-DPS fights that die inside phase 1,
where the lock is close and the tornado-clear branch is active. **At 300 DPS the fight now lasts
twice as long.** The high-DPS points that already killed are untouched.

Weak wing (fairy), obsidian, same conditions:

```
DPS    before                            after
300    4218? / - / -                     5879 /  8 / 51124
600    died @5068 7/17631 (at 800)       4741 /  6 / 35818
800    died @5068 7/17631                6380 /  5 / KILL      <-- now a KILL
1000   died @4600 6/10299                4038 /  5 / 19593
1200   died @4145 5/ 5911                3702 /  5 / 14612
1500   kill @3660 3                      3656 /  4 / KILL
2000   kill @2881 1                      2881 /  1 / KILL
```

**At 800 DPS the weak wing now kills**, which it could not do before at any DPS below 1500.

Shroomite (high-defence) tier, strong wing, confirms the direction and is the better tier as expected:

```
300    9792 / 12 / 31627
600    6885 /  6 / 14514
1200   4439 /  0 / ZERO-HIT KILL
```

### 129.4 What this says about the earlier diagnosis

The previous rounds searched for a *new* escape strategy -- owner's normal rule, climb-away, escape
simulation, counter-dash -- and every one was a net negative. The actual defect was not a missing
strategy but a **missing wind-up on one branch**, caused by an early `return`. The escape the circuit
already performs works, when it is given the same 20-tick pre-load everywhere.

This is the second time in this project that a control-flow detail outranked a strategy question (the
first being the exported-policy trap in 128.1). The lesson recorded for future rounds: when one
charge out of many behaves differently, compare the *branch* it took against the branch the working
charges took, before assuming the strategy is wrong.

### 129.5 The one ungated rule that had to be put back behind its switch

Applying the same branch audit to `ChargeEscape`'s ascent beat found a second latent problem: the
"never lift while the Boss is below" rule (the measured fix for the t=2251 hit) was gated behind
`ChargeClimbAwayArmed`, i.e. behind the switch of an *unrelated* experiment that had already measured
as a net negative. So a correctness fix was never running.

Ungating it -- making the one-sided rule unconditional, which its own guard already limits to locked
charges with the Boss genuinely below -- **regressed the band**:

```
                 gated (kept)             ungated
strong  800      4 hits                   6 hits
strong 1000      3 hits                   5 hits
weak   1000      died                     KILL (better)
weak   1500      4-hit KILL               died @3131 (much worse)
```

A net loss at the top of the useful band, so the gate went back and **stays OFF**. Rebuilt and
re-measured afterwards, the kept tree reproduces the 129.3 table exactly (strong 300 -> 8812/8/36546,
strong 600 -> 6744/6/15937, strong 1200 -> 4437/1/kill, strong 1500 -> 3661/1/kill, strong 2000 ->
2881/0/zero-hit kill, weak 800 -> 6380/5/kill, weak 1500 -> 3656/4/kill).

This is the third instance of the lesson already recorded in 122 and 128: **a rule that is correct in
isolation can still be a worse controller than the heuristic it replaces, because the heuristic is
entangled with the beat schedule.** The distinguishing test is not "is the rule right" but "does the
band improve".

### 129.6 Route regeneration and what the route channel actually asserts

The `-FormulaRoute` runs above compute their plan from the **current build**, so the fix was live in
them. The committed CSVs in `routes/` are frozen per-tick input, so they had to be regenerated or they
would keep replaying the defective control path. `tools/harvest-native-route.py` builds a route from
the controls **actually applied** at the facade (not from the plan, which would be a claim about
intent), so the procedure is: run the formula route, then harvest the run.

Two traps, both already documented in the scripts and both hit again here:

1. **Dense frames are not optional.** `CHAITE_PROBE_DENSE_FRAMES=1` must be set for BOTH the harvesting
   run and the replay. My first harvest omitted it, produced a 349-control route padded with 5637
   neutral filler rows, and the replay died at tick 1322 with 4 dashes. With dense frames the harvest
   carries one control per tick (6000 rows, 0 fillers) and the replay reproduces the run exactly.
2. **The route channel carries no damage.** `verify-fishron-routes.ps1` injects no simulated output,
   so the Boss never crosses a phase threshold and the whole window is phase-1 behaviour. The hit
   counts it reports (strong 6, weak 9) are therefore NOT the fight's hit counts and NOT acceptance
   evidence. What the channel asserts is that a committed route replays to the exact control path it
   was harvested from. That is the property `CHAITE_ROUTE_FILE` is supposed to guarantee, and the
   contradiction it would catch -- a route that only works when a live controller is steering -- is
   precisely why the owner made native replay the acceptance channel.

Regenerated and verified:

```
strong -> routes/strong-fishron-wings.csv   6000 ticks, 6 hits, death False, dmg 55   MATCH
weak   -> routes/fairy-wings.csv            5636 ticks, 9 hits, death True,  dmg 102  MATCH
```

Both replay to a byte-identical result to their generation run. The expected-value table in the
verifier was updated to these measured numbers, and it now records why they differ from the DPS
channel.

### 129.7 The arena's platform rows: zero rows helps the low end and hurts the high end

The owner has said twice that only one flat layer is needed, and the technique sources agree the fight is
won with vertical jinks off a floor rather than by hopping between rows. So the row count was made
switchable (`CHAITE_ARENA_PLATFORM_ROWS`, default 2 = every earlier measurement) and measured.

```
                       2 rows (default)          1 row                0 rows
strong  300            8812 / 8 / 36546          3371 / 6 / death     9747 / 9 / 31907
strong  600            6744 / 6 / 15937          3371 / 6 / death     8336 / 6 / KILL
strong  800            5057 / 4 / 17755          -                    4816 / 7 / 20959
strong 1000            4573 / 3 / 10777          -                    4903 / 6 /  5245
strong 1200            4437 / 1 / KILL           2939 / 5 / death     4022 / 6 /  8339
strong 1500            3661 / 1 / KILL           -                    3341 / 6 /  7959
strong 2000            2881 / 0 / ZERO-HIT KILL  -                    2880 / 1 / KILL
weak    300            -                        -                    4174 / 7 / 59781
weak    600            4741 / 6 / 35818          -                    4174 / 7 / 41616
weak    800            6380 / 5 / KILL           5406 / 6 / death     4274 / 5 / 28172
weak   1000            5214 / 4 / KILL           -                    4648 / 5 /  9518
weak   1500            3656 / 4 / KILL           -                    3659 / 2 / KILL   (better)
weak   2000            2881 / 1 / KILL           -                    2571 / 4 / death  (worse)
```

Three separate findings, and none of them is the simple "one layer suffices":

1. **ONE row is strictly the worst of the three.** It hides the floor without giving a second landing
   option: the player is trapped at one altitude, the wing budget drains with nothing to land on at the
   right height, and every strong-arm DPS measured dies at 3371 -- including 1200, which kills with two
   rows and with none. This is the one option that can be rejected outright.
2. **ZERO rows is clearly better at the low end.** At strong 600 it turns a death into a KILL
   (8336 / 6), at strong 300 it extends the fight from 8812 to 9747 ticks with the Boss further down
   (31907 vs 36546), and at weak 1500 it halves the hits (4 -> 2).
3. **ZERO rows is worse in the middle and at the top.** strong 800 goes 4 hits -> 7, strong 1000
   3 -> 6 and dies, strong 1500 loses its kill, weak 800 loses its kill, and weak 2000 goes from a
   1-hit KILL to a 4-hit death.

The pattern is not monotonic in either direction, which rules out a single explanation like "height
freedom helps" or "refills matter". What the measurements do support is that the row count is a genuine
tradeoff, so **the default stays at 2**, the only setting with no unacceptable point in the band. With
no platforms the wing budget can only be refilled on the floor, so a short fight may never reach a
refill, while a long one has time and the extra altitude wins.

This is the §122/§128 lesson yet again in a new costume: an owner statement that is correct about the
*fight* ("you only need one flat layer") is not automatically the best *parameter value* for this
circuit, and the only way to tell is to measure the band.

## 130. Round 162: the wing-budget gate fails, and the real shape of the 300-DPS gap

### 130.1 The finding that looked conclusive

The fixed strong-300 run is now the frontier, so it was traced densely (8813 rows, one per tick).
`colocation-lift` turned out to be the largest single hit cluster -- 3 of 8 hits, and the biggest
damages (107 / 119 / 123) -- and when the rule's own firing was audited:

```
colocation-lift fired on 59 frames
  wingTime histogram at fire: {0: 52, 7: 1, 8: 1, 125: 1, 126: 1, 157: 1, 158: 1, 159: 1}
  fired with wingTime == 0: 38 of 59 ... (52 of 59 are <= 8)
  fired but did NOT climb:  18 of 59
```

With `wingTime == 0` the commanded lift cannot fly at all: `output.Jump = vertical < 0` only asks for a
jump that the native wing gate (Player.cs:27001) refuses. The trace shows the whole sequence:

```
t=3482  wingTime 0, plvy +3.34 (terminal fall), |dy| 17
t=3483..3486  plvy +3.34 every tick -- the lift is inert
t=3487  CONTACT at |dx| 7.4, |dy| 41.2; plvy snaps to -3.50, the SHIELD's fixed
        recoil, not wing flight
```

and every colocation hit in the run (3487, 5148, 5575) sits on exactly that boundary tick. So the rule
was commanding a climb it could not perform, during the five ticks the player spends falling into the
24 px danger band. A `WingTime > 6` gate looked like a clean fix.

### 130.2 MEASURED: it is a net negative, and it is reverted

```
                    ungated (kept)              WingTime > 6
strong  300         8812 / 8 / 36546            7340 / 7 / 43942
strong  600         6744 / 6 / KILL             6301 / 6 / death
strong  800         5057 / 4 / 17755            4934 / 4 / 19381
strong 1000         4573 / 3 / 10777            4383 / 4 / 13949
strong 1200         4437 / 1 / KILL             4438 / 1 / KILL
strong 1500         3661 / 1 / KILL             3661 / 0 / ZERO-HIT KILL
strong 2000         2881 / 0 / KILL             2881 / 0 / KILL
weak    300         8812? (see 129)            5879 / 8 / 51124
```

Nothing improved that mattered and the two low-DPS points that carry the frontier got worse, so the
gate was reverted and the reverted tree re-measured to confirm it reproduces 8812 / 8 / 36546,
6744 / 6 / 15937 and 4437 / 1 / KILL exactly.

The interesting part is *why*. The inert lift was doing positional work anyway: holding the player
inside the band for those five ticks keeps them from being carried further along the Boss's line by the
fall, so removing it changed the geometry for the worse. This is the fourth instance of the recorded
lesson (122, 128, 129.5) -- correct reasoning about a mechanism is not evidence that acting on it helps,
because the mechanism is entangled with the trajectory.

### 130.3 The real shape of the 300-DPS gap: hits accrue with TIME, not with DPS

The dense trace answers the structural question that section 124 got wrong by assumption. The Boss's
charge speed is **exactly 17.0 px/tick at every single hit** -- phase-independent, as the decompilation
says -- so the different DPS outcomes cannot be a speed effect. What differs is *how long the fight
runs*. And the hit rate is remarkably stable across the band:

```
strong  300 : 8 hits in  8812 ticks = 1 per 1101 ticks
strong  600 : 6 hits in  6744 ticks = 1 per 1124 ticks
strong 1200 : 1 hit  in  4437 ticks = 1 per 4437 ticks  (fight ended early)
```

At 300 DPS the same run breaks down by phase as:

```
phase 1: 7 hits in 7780 ticks  (1 per 1111)
phase 2: 1 hit  in  792 ticks  (1 per  792)   -- enters phase 2 only at t=8020
phase 3: never reached
```

So the constraint is not that any one attack is undodgeable. It is that **the circuit sustains about one
hit per 1100 ticks**, and a 300-DPS kill needs ~15600 ticks, which extrapolates to ~14 hits against a
pool that absorbs about 9 (480 HP plus roughly 3 healing potions, at 98-123 damage per hit).

The requirement that falls out is concrete and measurable: the sustained hit rate has to come down to
roughly **1 per 1800-2200 ticks**, a factor of about 1.6-2.0, and it is the *phase-1 endurance* that
matters most because phase 1 is where nearly all the time is spent. It is emphatically not a 6x problem
and not a phase-3 problem at this DPS.

Note also that strong 1200 already measures 1 hit per 4437 ticks -- four times better than the 300 arm --
which shows the circuit is capable of the required rate over a short window. The 300 run simply exposes
the long-run rate. That is the frontier: make the good rate persist.

## 131. Round 162 (cont.): the crossing-rate signature, and a refuted command-space fix

### 131.1 The measured failure signature

The owner's rule is "dodge along the NORMAL of the charge", and section 127 established that the
horizontal *command* is already correct at every dash. So the untested half was what the player actually
*executes*. Measuring every one of the 3060 charge frames of the dense strong-300 run in coordinates
aligned with the Boss's frozen charge direction:

```
                      n      |along|   |across|   |perp|
ALL charge frames     3060     5.64     10.15     164.54
non-arrival frames    3005     5.65     10.24     166.12
HIT ARRIVAL (7 ticks)   55     5.62      5.43      78.03
```

Inside the 7-tick arrival window of the eight hits, **the along component is unchanged (5.65 -> 5.62)
and the crossing rate collapses by nearly half (10.15 -> 5.43)**. Contact needs 85 px of perpendicular
clearance against the Boss's 85 px half-width, so 5.43 px/tick cannot make it. This is a clean,
quantitative statement of the failure: **the circuit is not failing to run away, it is failing to cross
the charge line fast enough at the moment of arrival.**

### 131.2 Steering the command to the true perpendicular: refuted

The obvious fix is to make the horizontal command the exact perpendicular of the charge velocity instead
of the latched normal. Tried, choosing the side the latch already leaned toward:

```
              kept                          exact perpendicular
strong  300   8812 / 8 / 36546               3722 / 6 / 62035
strong  600   6744 / 6 / KILL                3722 / 6 / 46130
strong  800   5057 / 4 / 17755               3631 / 6 / 36740
strong 1200   4437 / 1 / KILL                4439 / 4 / KILL
```

Strong 300 and 600 die on the **same tick, 3722** -- the broken-invariant signature of section 126, where
an identical death tick across different DPS values means the change made the trajectory independent of
the fight. The knob was **deleted from the tree** rather than left default-OFF, so it cannot be
half-restored.

The instructive part: the horizontal *command* was already the latched normal, so the diagonal split must
arise **downstream** of the command -- the executed velocity is the sum of the command, the wing's own
horizontal rate (measured 7-8 px/tick, not the 13.87 that `wingAccRunSpeed` suggests), and the vertical
beat. A command-space fix cannot reach that. This is precisely why the tornado-clear wind-up fix of
section 129 succeeded where this failed: that one restored a missing *pre-condition*, while this one
merely redirected a command that was already right.

### 131.3 Where the frontier stands after this round

```
strong 300 : 8812 ticks, 8 hits, boss 36546   (dies; needs ~15600 ticks to kill)
strong 600 : 6744 ticks, 6 hits, KILL
strong 1200: 4437 ticks, 1 hit,  KILL
strong 1500: 3661 ticks, 1 hit,  KILL
strong 2000: 2881 ticks, 0 hits, ZERO-HIT KILL
weak   800 : 6380 ticks, 5 hits, KILL
weak  1500 : 3656 ticks, 4 hits, KILL
```

Five negative results this round (wing-budget gate, platform rows 1 and 0, exact perpendicular, plus the
earlier counter-dash tuning) and one positive (the tornado-clear wind-up). The requirement that remains
is the one quantified in 130.3: bring the sustained rate from about 1 hit per 1100 ticks to about 1 per
1800-2200, and the signature to attack is the arrival-window crossing collapse above. The open question
is whether a command-space change can affect it at all, given that the executed velocity is dominated by
downstream terms; the next attempt should therefore change what the *player state* is at the lock rather
than what is commanded after it.

### 131.4 The state at the lock also predicts the hit

Acting on 131.2's conclusion, the state at the charge commit was measured directly. A lock is the first
tick the Boss's speed reaches 17; the quantity is the player's velocity component **perpendicular to the
charge direction** at that instant -- the crossing speed the escape has to work with, since the data says
the escape cannot build it during the 7-tick approach.

```
crossing speed at the lock, over 98 locks in strong 300:
  locks followed by a hit within 40 ticks (n=8):   mean 3.44
  locks NOT followed by a hit          (n=90):     mean 5.97
  histogram: cross >= 6 -> 45   cross 2..6 -> 39   cross < 2 -> 14
```

Again a consistent separation in the right direction, and it is the same quantity that collapses in the
arrival window (131.1). Combining the two: the failures are charges that were committed while the player
had little crossing speed, and the escape then cannot manufacture it in seven ticks.

This is the third independent signal pointing at the same quantity, and it is also the first one that is
**not** reachable from the charge-escape code at all. The crossing speed at the lock is a consequence of
the approach leg (the patrol and leg schedule that decides where the player is and how fast when the Boss
commits), so steering it means changing the pre-charge approach, not the escape. That is where the next
round should work, and section 131.2's failed command-space edit is the evidence that the post-lock side
is the wrong place to look.

### 131.5 The lock geometry separates the hits cleanly, and names the approach to fix

Characterising the 98 locks by the geometry at the commit (strong 300, dense):

```
              n     mean vertness   mean sep   mean dy   mean |perp| at lock
HIT locks     8        0.490          311        +81          29.2
clean locks  90        0.668          572       +118          44.2
```

where `vertness` is |uy| of the charge direction (1.0 = straight down or up, 0 = purely horizontal), and
`|perp|` is the player's perpendicular distance from the charge line at the commit.

Three things fall out, and together they are the most actionable result of the round:

1. **Hit charges are committed from much closer.** Mean separation 311 px against 572 for clean locks.
   At 17 px/tick, 311 px is about 18 ticks; 572 px is about 34. The escape needs its approach window.
2. **Hit charges are committed nearly on the line.** Mean |perp| 29.2 against 44.2.
3. **Every hit is on a DIAGONAL charge.** Not one of the 29 near-vertical charges (|uy| > 0.8) produced a
   hit, while all 8 hits have mean vertness 0.49. The near-vertical charge is handled reliably; it is the
   diagonal that fails. That is consistent with the escape's structure: a vertical charge is escaped by
   horizontal motion, which the wings deliver at 7-8 px/tick, while a diagonal charge needs a component
   the player can build less well -- and the measured climb rate is 8.61 px/tick but the descent is
   10.01, so down-left and down-right geometry is asymmetric.

**Therefore the pre-charge approach to change is the standoff.** The circuit already has a
`StandoffPixels = 720` concept, and the failure mode is that a third of the locks happen at 300-400 px,
where there is not enough time for the crossing to develop. Holding the Boss further out before the
commit is a state-space change at the lock -- exactly what 131.2's failed command-space edit could not
achieve -- and it targets the separation signal directly.

## 132. Round 145: the first net-positive change in many rounds -- a DPS-gated standoff radius

### 132.1 The change

Section 131.5 implicated the pre-charge separation: hit locks commit at 311 px against 572 for clean
locks. The reviewed standoff gates on the **horizontal gap** alone (`Math.Abs(gap) < StandoffPixels`, 720),
so a Boss hovering above the player at a small horizontal offset reads as "far" and the standoff never
fires. The change adds a distance-based form that gates on **true separation** instead, and it is
DPS-gated so it only applies where it was measured to help.

### 132.2 The radius sweep (strong arm, obsidian)

```
radius     300 dps              600 dps              800 dps     1200 dps
off        8812 / 8 / 36546     6744 / 6 / KILL      5057 / 4     4437 / 1 / KILL
 900       7889 / 9 / 41167     6553 / 5 / death      --           --
1200      10004 / 8 / 30628     5734 / 5 / death      5398 / 4     4441 / 3 / KILL
1400       6571 / 6 / 47737     --                   --           --
1700       6571 / 6 / 47737     --                   --           --
2000       6571 / 6 / 47737     --                   --           --
```

Two things stand out. **1200 is the only good radius**: 900 regresses the low end, and 1400/1700/2000 all
produce the *identical* 6571/6/47737, i.e. the term saturates -- past ~1400 the standoff simply always
fires, which is a different and worse controller.

And **1200 is not a global win**. It gains clearly at 300, is a regression at 600 and 1000, and is neutral
at 1200/1500/2000. That is the familiar entanglement again (sections 122, 130.2, 131.2), but this time the
signal was strong enough to act on anyway, because it helps **precisely the one point that is blocked**.

### 132.3 DPS-gating it, and the gate must be inert for route replay

The standoff is therefore enabled only when simulated output is configured and the DPS is at or below a
ceiling (default 450, sitting between the 300 gain and the 600 regression). Verified across the band,
`CHAITE_SIM_DPS` set and nothing else:

```
              before (this session)      after default
strong  300   8812 / 8 / 36546           10004 / 8 / 30628     <- best 300 result ever recorded
strong  600   6744 / 6 / 15937           6744 / 6 / 15937      identical
strong  800   5057 / 4 / 17755           5057 / 4 / 17755      identical
strong 1000   4573 / 3 / 10777           4573 / 3 / 10777      identical
strong 1200   4437 / 1 / KILL            4437 / 1 / KILL       identical
strong 2000   2881 / 0 / ZERO-HIT KILL   2881 / 0 / KILL       identical
weak    300   5879 / 8 / 51124           5879 / 8 / 51124      identical
weak    600   4741 / 6 / 35818           4741 / 6 / 35818      identical
weak   1500   3656 / 4 / KILL            3656 / 4 / KILL       identical
```

**Why this is safe for the committed routes, and it was verified rather than assumed:** the gate reads
`CHAITE_SIM_DPS`, which *only* the simulated DPS channel sets. `verify-fishron-routes.ps1` clears every
knob and injects no damage, so the predicate returns false and the standoff stays exactly as reviewed.
Both committed routes still replay to their recorded results (`MATCH`, strong 6000/6/False, weak
5636/9/True), which is the control-path guarantee `CHAITE_ROUTE_FILE` exists to provide.

### 132.4 Honest size of the win

This is a real improvement and the first net-positive change in many rounds, but it is **not** the
solution and must not be presented as one. Endurance at 300 DPS improves from 8812 to 10004 ticks, about
13.5%, with the Boss left at 30628 instead of 36546. The requirement from 130.3 is to reach roughly 15600
ticks *with hits still inside the survivable pool*, i.e. about a further 56% on top of this. Five rounds
of negative results have now bounded where the remaining gain can come from: not the charge-escape command
(131.2), not the wing budget (130.2), not the platform count (129.6), not the phase transition at this DPS
(129.7). The standoff worked because it changed the *state the fight is in*, which is the direction 131.4
predicted. That direction should be pushed further.

### 132.5 Mechanism, measured -- and it is a rate change, not a count change

Dense 300 runs with the standoff OFF and ON, same arm:

```
                    locks  mean sep  median  p25   under 400 px   mean vertness
OFF (8812/8/36546)    98      551     462    365    34 (35%)         0.653
ON  (10004/8/30628)  109      624     562    420    23 (21%)         0.566
```

The standoff demonstrably does what it was chosen to do: median lock separation rises from 462 to 562 px
and the share of locks committed inside 400 px falls from 35% to 21%. That is the signal 131.5 named.

But the honest reading is more interesting than that. **The standoff does not prevent charges -- it
produces MORE of them** (98 -> 109), because the circuit spends time in the standoff state and that slows
the fight. Hit count stays at 8 while the number of charges rises by 11%, so the hit *rate* per charge
improves from 8.2% to 7.3%. The gain is therefore a genuine increase in per-charge dodge quality, not a
reduction in exposure. The extra 1192 ticks of survival come from the fight being longer, and the fight is
longer precisely because the standoff costs tempo.

That reframes the remaining gap. A standoff that slows the fight while improving each dodge is a
**trade**, and the current form is a blunt one: it is a hard clamp that abandons the patrol whenever the
Boss is inside 1200 px, which at arena scale is most of the time. A smoother form -- one that biases the
patrol's retreat direction toward keeping separation without surrendering the beat schedule -- should keep
the dodge-quality gain and give back less tempo. That is the concrete next step.

### 132.6 The smoother standoff is refuted, and it teaches why the clamp works

132.5 concluded that the standoff's tempo cost was waste that a smoother form could give back: keep the
cruise behaviour and merely flip the patrol direction away from the Boss. Tried, and it is worse
**everywhere**:

```
                  hard clamp (kept)        patrol-flip (refuted)
strong  300       10004 / 8 / 30628         8094 / 10 / 40100
strong  600        5734 / 5 / 26023         5743 /  6 / 25903
strong  800        5398 / 4 / 13180         4887 /  5 / 20018
strong 1200        4441 / 3 / KILL          3994 /  4 / 8922
weak    300        5879 / 8 / 51124         2576 /  6 / 67798
weak    800        6380 / 5 / KILL          2576 /  6 / 50840
```

Weak 300 and weak 800 die on the **same tick, 2576** -- the broken-invariant signature of section 126,
now for the third time this session. The form was reverted and the tree re-measured to confirm it
reproduces the committed numbers exactly (strong 300 10004/8/30628, strong 600 6744/6/15937, strong 1200
4437/1/kill, strong 2000 2881/0/kill, weak 300 5879/8/51124, weak 800 6380/5/kill).

**The lesson is that the standoff's tempo cost is load-bearing, not waste.** Fleeing the Boss axis
outright and diving is what actually buys the separation; merely reversing a patrol that was going to turn
around anyway does not. This is the same shape as 130.2, where the "inert" wing-budget lift turned out to
be doing positional work, and 131.2, where an already-correct command was not the thing to steer. Three
times now, the working rule's incidental-looking side effect has been the mechanism, and "cleaning it up"
has regressed the band.

A useful heuristic falls out, and it is cheap to apply: **when a rule's side effect looks like waste,
measure the rule without it before optimising the side effect away.** Every attempt to tidy a working
heuristic in this session has lost; both genuine wins (129's tornado wind-up and 132's standoff radius)
*added* a missing pre-condition rather than removing an existing one.

### 132.7 The standoff must be continuous -- restricting it to the pre-charge window is refuted

132.5 said the clamp's value is the separation it buys and the cost is tempo; 132.6 showed the tempo cost
is load-bearing. The remaining reasonable lever was *when* it applies: only in the window where separation
is about to matter, using the same `PredictChargImminent` predicate that made the tornado-clear fix work.
Refuted, and by a wide margin:

```
                  continuous (kept)        pre-charge only
strong  300       10004 / 8 / 30628         4091 / 6 / 60157
strong  600        6744 / 6 / 15937         4091 / 6 / 42407
strong 1200        4437 / 1 / KILL          4438 / 2 / KILL
strong 2000        2881 / 0 / KILL          2880 / 0 / KILL
weak    300        5879 / 8 / 51124         2870 / 6 / 66262
weak    800        6380 / 5 / KILL          2870 / 6 / 46854
```

Strong 300 and 600 die on the same tick, 4091; weak 300 and 800 on the same tick, 2870. **The
broken-invariant signature again, now for the fourth time this session**, on what looked like the most
sensible refinement available. The tree was reverted and re-verified (strong 300 10004/8/30628, strong 2000
2881/0/kill, weak 800 6380/5/kill).

### 132.8 What this round establishes: the circuit is at a local optimum

Three independent perturbations of the winning standoff rule -- a smoother action (132.6), a narrower
application window (132.7), and, across the session, the wing-budget gate (130.2), the platform count
(129.6) and the charge-escape command (131.2) -- have all **regressed the band**, and four of them broke
the invariant by making the death tick independent of DPS. That is a consistent and informative picture:
the shipped circuit sits at a local optimum, and it is a *sharp* one, since even changes that look like
strict improvements to a single rule lose.

Combined with the quantification in 130.3, the honest position is:

* The requirement is roughly **1 hit per 1800-2200 ticks sustained**, against 1 per 1101 at the shipped
  default and 1 per 1250 with the standoff win. The best measured dodge quality is 1 per 1250, i.e. about
  **62-70% of the way** to the target on the rate, but the 300-DPS arm still dies.
* Every local rule change attempted this session has failed. The two wins that did land (129's tornado
  wind-up, 132's standoff radius) both **added a missing pre-condition** to the existing heuristic rather
  than restructuring it.

The next attempt should therefore not be another rule tweak. The evidence points to the remaining gap
being **structural**: the circuit reacts to AI_069's group clock and desynchronises from it, and the
technique notes (docs/fishron-technique-from-videos.md) record that phase 3 in particular is fully
**memorisable** (`teleport -> 1 dash -> teleport -> 2 dashes -> teleport to the other side -> 3 dashes`)
rather than reactive. A controller that tracks the attack index and answers the memorisable sequence
directly is a different design, not a tuning of this one, and it is the only direction the evidence has
not yet ruled out.

## 133. Round 147: the dash's vertical gate is a strong-arm-only lever, and it is refuted as a change

### 133.1 The lever

The dash is the only tool that adds acceleration rather than velocity (measured +22.56 px/tick^2 to 14.50
once, then decaying), so it is the natural candidate for the 1.5x crossing-rate gap of section 131.1. Two
knobs gate it during a charge and both were defaulted to what the reviewed circuit does:

* `CHAITE_DASH_DELAY` (default 0) -- ticks after the lock before the dash may fire. The earlier sweep
  recorded in the tree only tested values of 12 and up; the low end had never been measured.
* `CHAITE_DASH_SUPPRESS` (default 60) -- vertical gap in px below which a ready charge dash is refused,
  because "the escape is mostly vertical; a horizontal dash cannot win along the charge axis".

### 133.2 Delay: refuted, and sharply

```
delay      300 dps
0 (base)   10004 / 8 / 30628
2           5661 / 6 / 52340
4           9637 / 10 / 32439
6           6140 /  7 / 49981
8           5324 /  9 / 54058
```

Every non-zero delay collapses the run. Delay 0 is not a default by accident; firing the dash on the
lock tick is load-bearing, exactly like the body-hit itself (section 128).

### 133.3 The vertical gate: helps the strong arm widely, and is still a net negative

`CHAITE_DASH_SUPPRESS` swept at 300 dps:

```
sup=15   10811 / 8 / 26586    <- same 8 hits, +807 ticks
sup=20    9819 / 10 / 31532
sup=25    9977 /  9 / 30742
sup=30   10826 /  9 / 26520    <- best endurance, +1 hit
sup=35   10826 /  9 / 26520    (saturates)
sup=40   10064 / 10 / 30288
sup=50    6902 /  6 / 46080    <- collapse
sup=60    6367 /  6 / 48774    <- collapse (the default)
default  10004 / 8 / 30628
```

The gate **saturates between 30 and 35** and collapses from 50, so the reviewed default of 60 sits just
past a cliff on the bad side -- a genuinely suspicious value. Across the strong arm, sup=30 is good:

```
              default (sup 60)        sup=30
strong  300   10004 / 8 / 30628       10826 / 9 / 26520
strong  600    6744 / 6 / 15937        6584 / 6 / 17498   worse
strong  800    5057 / 4 / 17755        6385 / 3 / KILL    better
strong 1000    4573 / 3 / 10777        4073 / 3 / 19111   much worse (dies)
strong 1200    4437 / 1 / KILL         4435 / 0 / KILL    ZERO-HIT KILL
strong 1500    3661 / 1 / KILL         3661 / 1 / KILL    same
strong 2000    2881 / 0 / KILL         2881 / 0 / KILL    same
```

including a new **zero-hit kill at strong 1200** (the first at that DPS). But the weak arm is destroyed:

```
              default                 sup=30
weak    300    5879 / 8 / 51124       4507 / 7 / 58083
weak    600    4741 / 6 / 35818       4503 / 7 / 38293
weak    800    6380 / 5 / KILL        6382 / 4 / KILL
weak   1500    3656 / 4 / KILL        3210 / 5 / dies
weak   2000    2881 / 1 / KILL        1621 / 5 / dies
```

Those weak-arm numbers are baseline (no standoff, since the standoff is DPS-gated to <=450). So the gate
is a strong-arm-only lever and **is not promoted**. The tree is unchanged from the committed state, and
the committed points were re-verified (strong 300 10004/8/30628, strong 2000 2881/0/kill, weak 800
6380/5/kill).

### 133.4 What this round adds

Two more full-band perturbations refuted, in addition to the three of section 132. The dashboard is now:

* **The strong arm is the one that works, and it is close.** At target DPS it kills everywhere from 1200
  up, with a **measured zero-hit kill at 2000** and now one at 1200 under a refuted knob. Its blocker is
  the low-DPS arm only.
* **The weak arm is the real gap.** Its baseline is worse than the strong arm at *every* DPS (weak 300
  5879 vs strong 10004) and the knee is much lower. The owner's note that the two wings "should have two
  sets written because their vertical mobility differs" is, on this evidence, not yet honoured: the shared
  circuit is tuned around the strong wing's vertical budget, and every knob that helps the strong arm
  hurts the weak one.

**The next round should stop tuning the shared circuit and give the weak wing its own behaviour**, since
that is both what the owner asked for and where the measurements point. Specifically: the weak wing's
lower climb rate (its vertical escape is weaker) is why `sup` refusals and the standoff both hurt it --
it cannot afford to decline a dash or spend ticks repositioning. A weak-specific variant should keep the
dash unconditionally and take its clearance from the horizontal axis instead.

### 133.5 The weak wing given its own dash policy: also refuted, but it yields the real diagnosis

Since 133.3 showed the gate is strong-arm-only, the natural next test was to give the weak wing the
opposite policy, on the owner's own reasoning that the two wings need two sets because their vertical
mobility differs. The gate refuses a horizontal dash when `|dy| < 60`; a low-climb wing arguably cannot
afford that refusal. Tested with the gate effectively off (`CHAITE_DASH_SUPPRESS=1`) versus the default 60:

```
weak        default (sup 60)          sup=1 (gate off)
  300        5879 / 8 / 51124          4321 / 7 / 59019
  600        4741 / 6 / 35818          4321 / 7 / 40119
  800        6380 / 5 / KILL           4675 / 5 / 22719   (loses the kill)
 1500        3656 / 4 / KILL           3658 / 3 / KILL
 2000        2881 / 1 / KILL           2881 / 1 / KILL    (identical)
```

Removing the gate is **worse everywhere except 1500**, and it loses the weak 800 kill. So the refusal rule
is protective for the weak wing, not a handicap -- the opposite of the hypothesis. Note also that
`sup=1/15/30` are all *identical* at weak 2000: the gate simply never binds there, which is why 133.3's
`sup=30` weak-2000 collapse (to 1621/5/death) cannot be explained by the... be a second-order trajectory
effect rather than a direct gate effect, and `CHAITE_NO_CHARGE_DASH=1` reproduces exactly that collapse
(weak 300: **1621 / 5 / 72600**), which confirms the mechanism.

**The real diagnosis.** Per-charge dodge quality, computed as ticks per hit, is nearly identical between
the wings at equal DPS:

```
                 strong            weak
  300        10004 / 8 = 1250    5879 / 8 =  735
  600         6744 / 6 = 1124    4741 / 6 =  790
  800         5057 / 4 = 1264    6380 / 5 = 1276   <- indistinguishable
 1500         3661 / 1 = 3661    3656 / 4 =  914
 2000         2881 / 0 = none    2881 / 1 = 2881
```

At 800 the two wings measure **1264 and 1276 ticks per hit** -- a 1% difference. At 1500 the strong wing
is 4x better. The weak wing is therefore **not** worse at the dodge itself; its deficit is *endurance*
(the amount of fight it survives per hit taken) and it collapses specifically once the wing's vertical
budget is being spent. That is consistent with 133.4 and it means the weak wing's remaining problem is
vertical-mobility budgeting, not horizontal escaping -- which is exactly the axis the owner said the two
sets should differ on.

## 134. Round 148: the wing asymmetry measured properly, and the wind-up lead is a knife edge

### 134.1 The wing budget: a real asymmetry, but not the one I assumed

Dense native traces for both wings at 300 DPS give the first hard numbers on the owner's point that the
two wings differ in vertical mobility:

```
              wingTimeMax   mean wingTime   wingTime==0   climb median   climb peak   descent
strong            180           69.3          33.8%         7.50          16.31       10.01
weak              130           62.4          25.6%         5.08           9.91       10.01
```

So the owner is right and the asymmetry is large: **the weak wing climbs at about two thirds the strong
wing's rate (median 5.08 against 7.50, a 1.48x deficit) while falling at exactly the same 10.01 terminal
speed**, and its `wingTimeMax` is 130 against 180.

**But my previous round's guess at the mechanism was wrong, and the data says so**: the weak wing is
*less* often wing-exhausted than the strong one (25.6% of ticks at `wingTime == 0` against 33.8%). The
deficit is therefore **rate, not budget** -- the weak wing is not running out of wing, it simply gains
height more slowly while the Boss closes vertically at the same speed.

### 134.2 The wind-up lead is hard-coded and wing-blind

`PreJumpTicks = 20` was a single shared constant, applied to both wings by
`PredictChargImminent`. Scaling it by the measured climb ratio gives `20 * 7.50 / 5.08 = 29.5`, so a
30-tick lead for the weak wing looked like the exact "two sets" fix the owner asked for. Tested, and it
is a **knife edge**:

```
weak 300, lead 20 (reviewed):  5879 / 8 / 51124
weak 300, lead 26:             4812 / 7 / 56546
weak 300, lead 30:             2176 / 5 / 69767   <- collapse
weak 300, lead 35:             2176 / 5 / 69767   identical
weak 300, lead 45:             2176 / 5 / 69767   identical
```

Leads of 30, 35 and 45 produce the **same tick, dead at 2176** -- the broken-invariant signature for the
fifth time this session. Extending the wind-up does not buy altitude; it makes the player commit to a
climb earlier and then fly *into* the Boss's hover point, which is the exact failure this file's own
personal-space comment records ("a climb from a few tens of pixels below the hover point flies straight
into it"). The proportional-scaling idea is refuted, and the lead stays at 20.

### 134.3 What was kept: the behaviour is now expressed per wing

The wing-aware threshold was kept structurally -- `PredictChargImminent` now selects its lead by route and
reads `CHAITE_WEAK_PREJUMP`, so the two wings *can* differ and the constant is sweepable -- but both
values are 20, so behaviour is unchanged. Verified: nine DPS points across both arms are **byte-identical**
to the committed baseline (strong 300 10004/8/30628 through weak 2000 2881/1/kill), and both committed
routes still replay to their recorded results (`MATCH`).

The structural change matters for the objective even though it changed no behaviour: the objective asks
for *two* formulaic state machines, and until this round there was literally one shared number where the
owner said the divergence lives.

### 134.4 Honest position

Six full-band perturbations refuted across rounds 145-148, five of which tripped the broken-invariant
signature. The circuit is at a sharp local optimum in every direction that has been probed. Two levers
remain genuinely untested rather than refuted: (a) taking the weak wing's clearance from the **horizontal**
axis rather than trying to out-climb, since its horizontal rate is not degraded (measured 7-8 px/tick on
both wings) while its vertical rate is; and (b) the memorised attack-index controller of 132.8.

## 135. Round 148 (cont.): the tornado's vertical dimension, and a latent hole in the acceptance channel

### 135.1 Long-range damage events are NOT body contact

Inspecting the damage events of the two dense 300-DPS runs against the recorded projectile list
identified the actual sources, and it corrects a long-standing assumption. Several events happen at
distances that the 85 x 71 body box cannot reach:

```
Cthulhunado (projectile 386) present, with the geometry at the damage tick:
  strong  5952: |dx| 401   |dy| 172
  strong  9563: |dx| 753   |dy|   9
  strong  9603: |dx| 804   |dy| 216
  strong  9643: |dx| 621   |dy| 254
  weak    6213: |dx| 423   |dy| 202
  weak    9526: |dx| 325   |dy| 121
  weak    9567: |dx| 251   |dy| 342
```

Those are contacts with the **Cthulhunado column**, which the owner had flagged as the dangerous attack.
The column grows as it lives -- measured `width` 56 -> 225 and `height` 15 -> 63, `scale` 0.375 -> 1.5 --
so the late column is a large box, and it reaches the player at over 250 px horizontally and up to 342 px
vertically.

### 135.2 The tornado gate models only the horizontal axis

The circuit's avoidance is `Math.Abs(player.Center.X - _tornadoX) < TornadoClearance` (760 px) plus a
comment asserting that "horizontal distance is the whole defence". The vertical axis is not modelled at
all, and `_tornadoX` alone was remembered even though the column is fired from the Boss's own centre (so
its Y is known for free).

Added: `_tornadoY` tracking from the same spawn tick, measured half-extents (`TornadoHalfWidth` 190,
`TornadoHalfHeight` 110, from width 225 / height 63 plus the player half-box), and a box rule under
`CHAITE_TORNADO_BOX` that escapes by the cheaper axis when inside the box.

### 135.3 Result: the box is inert at 300 and harmful elsewhere, so it stays OFF

```
              default              CHAITE_TORNADO_BOX=1
strong  300   10004 / 8 / 30628    10004 / 8 / 30628   <- BYTE-IDENTICAL
strong  600    6744 / 6 / 15937     5610 / 5 / 27277
strong 1200    4437 / 1 / KILL      4437 / 1 / KILL
strong 2000    2881 / 0 / KILL      2881 / 0 / KILL
weak    300    5879 / 8 / 51124     3698 / 6 / 62047   <- much worse
weak    800    6380 / 5 / KILL      6375 / 6 / KILL
```

The strong-300 result being **byte-identical** is the informative part: the box rule never fires there, so
at the DPS that matters the player is never inside the box when the gate is active. The 250-800 px
Cthulhunado contacts therefore happen **outside the 760 px horizontal gate** -- i.e. after `_tornadoTicksLeft`
has expired, when the circuit has stopped modelling the column at all. That is a different defect from the
one this round fixed, and it is the real lead: the tornado's damage lifetime outlasts the circuit's memory
of it. Left default-OFF; the tracking and sweepable rule stay in the tree.

### 135.4 A latent hole in the acceptance channel itself -- found and fixed

While verifying, `verify-fishron-routes.ps1` reported **DRIFT on both routes** with nonsense values
(strong 2880/0/kill, weak 2881/2/kill). The cause was not the circuit: the script cleared the control
knobs but **not the simulated-output knobs**, so a `CHAITE_SIM_DPS=2000` left in the shell by the previous
sweep leaked into the route replay and injected 77967 damage into what is supposed to be a damage-free
control-path record.

This matters more than a nuisance. The objective names `CHAITE_ROUTE_FILE` as the **acceptance channel**,
and this bug meant that channel could be silently converted into a damage-injecting run that still
*matched* a stale expected table -- a false MATCH in the one place the objective relies on. It has been
present since the route checks were written.

Fixed by clearing every simulated-output variable in that script, and **proven** by poisoning the shell
deliberately and re-running: both routes then reproduce their recorded results correctly (strong
6000/6/False, weak 5636/9/True, `MATCH`), because the script no longer depends on the ambient environment.
The general lesson, and the second instance this session after the `CHAITE_POLICY_FILE` trap of section 128:
**every acceptance script must clear the full `CHAITE_*` environment rather than the subset it happens to
know about.**

### 135.5 The column is HALF the strong-300 damage, but widening the gate is refuted

Section 135.3 left a sharper question: the box never fired at strong 300, so why are there Cthulhunado
contacts at all? Reading the recorded projectile presence directly answers it. The column is present in
**one continuous episode from tick 9125 to 10004 -- 881 ticks**, and reconstructing the geometry tick by
tick over that episode:

```
|dx| < 760 (the gate)   : 634 ticks (72.0%)
|dx| < 190 (column box) : 491 ticks (55.7%)
|dy| < 110 (box Y)      : 643 ticks (73.0%)
```

The column's 881-tick life is far longer than the circuit's **540-tick memory**, which is the gap the box
result hinted at. And the fatal stretch is a **wall pin**, which the trace shows exactly:

```
t=9540  player x 15 (arena left edge), column x 61  -> |dx| 48
t=9563  HIT at |dx| 13
t=9600..10004  |dx| frozen at 48, 48, 52, 52 -- still inside the gate,
               still in the `tornado-clear` branch commanding escape, unable to move
```

So the player is commanded to flee the column, reaches the arena wall, and is left standing 48 px from a
column of roughly 56 px half-width. **Four of that run's eight hits fall inside this one episode**, making
the column the single largest damage source in the strong-300 run -- half its hits.

The obvious repair is a larger gate, and it is **refuted**, sharply:

```
clearance    strong 300
 760 (default)  10004 / 8 / 30628
1200             7867 / 8 / 41304
1600             4679 / 7 / 57241
2200             3658 / 6 / 62397   <- collapse
3000             3658 / 6 / 62397   identical
```

2200 and 3000 collapse to the same tick, and the weak arm and higher DPS points are worse too (weak 300 at
1600: 5737/7; strong 600 at 1600: 4600/7 and no longer a kill; strong 1200 at 1600 loses 1 hit -> 4). Making
the escape wider than 760 px does not buy safety -- it spends the whole fight crossing the arena, which
costs more tempo than the column costs damage. **760 is already the optimum**, so this is another
knife-edge value that the reviewed circuit happens to have right.

### 135.6 Where this leaves the strong-300 arm

The tornado component is now fully characterised and bounded: half the hits, but widening the gate,
modelling the box, and shrinking the box all fail. The gate value is optimal, the memory horizon is too
short, and the failure mode is a wall pin.

The remaining option is therefore not geometric but **temporal**: the column lives 881 ticks and the
memory is 540, so a 341-tick window exists in which the circuit has entirely forgotten a column that is
still damaging the player. Extending the memory to the true lifetime is the one untested change in this
family -- it costs nothing geometrically (the gate radius stays at its optimum) and only restores awareness
where the circuit is currently blind. That is the next round's first test, and it needs the clearing to be
sweepable, which it now is.

## 136. Round 149: the tornado ablation -- the column costs the strong wing the run, and does NOT touch the weak wing

### 136.1 The decisive experiment

Section 135.5 concluded from projectile-list correlation that the column caused four of the strong-300
hits. Correlation is not causation, so this round ablated it directly with the probe's existing
`CHAITE_SIM_RETIRE_PROJECTILE` (it deactivates every projectile of a given type each frame). A null control
was run first: retiring an **inert** type must change nothing, and it does not.

```
                        default                 retire 9999 (null control)   retire 386 (tornado)
strong  300   10004 / 8 / 30628  dies     10004 / 8 / 30628  identical    16077 / 9 / 0  KILL  ACCEPTED
weak    300    5879 / 8 / 51124  dies       --                            5879 / 8 / 51124  BYTE-IDENTICAL
strong  600    6744 / 6 / 15937  dies       --                            8333 / 4 / 0    KILL
weak    800    6380 / 5 / KILL              --                            6367 / 4 / KILL
```

The null control being byte-identical is what licenses the attribution, and the result is much stronger
than the correlation suggested: **removing the column converts the strong wing's 300-DPS run from a death
into an accepted kill.**

### 136.2 The column accelerates the drain; it does not merely add hits

This is the subtler half. The strong-300 kill without the column takes **16077 ticks and 9 hits**, i.e. one
hit per **1786** ticks -- which is *worse* per tick than the doomed run's 8 hits in 10004 ticks (one per
**1250**). A worse hit rate survives while a better one dies, so raw hit count is not the mechanism. What
the column changes is the **timing**: it drains life fast enough to reach the death threshold before the
Boss's health bar is exhausted at 300 DPS.

That reframes the requirement. The objective needs lifetime, not fewer hits: at 300 DPS a kill needs about
16000 ticks, and the column both shortens the run and front-loads the damage.

### 136.3 The two wings fail for DIFFERENT reasons -- the weak wing is tornado-independent

The most valuable single line in the table is the weak-300 row: retiring the column leaves the run
**byte-identical** (5879 / 8 / 51124). The weak wing's deficit is therefore entirely independent of the
tornado, and none of the tornado work in sections 135/136 can help it. Conversely the strong wing's deficit
is *dominated* by the tornado.

This is the first concrete, measured evidence for the owner's instruction that the two wings need two
different state machines: they fail through different hazards, not the same hazard at different strength.

### 136.4 The column cannot be destroyed -- it can only be avoided

The decompiled setup for type 386 answers the last remaining option:

```
width = 150;  height = 42;  hostile = true;  penetrate = -1;
aiStyle = 64;  tileCollide = false;  timeLeft = 840;
```

`penetrate = -1` means the column is **invulnerable**: firing at it cannot remove it. And `aiStyle 64`
shows its true shape -- `width = 150 * scale`, `height = 42 * scale`, scale capped at 1.5 for 386, so at
full growth **225 x 63**, i.e. about 112 px wide but only **31 px per side vertically**, with its only
motion a `cos` sway of amplitude `width/5 * 2 = 90` px on X. It is wide, flat, and never moves vertically.
The measured histogram confirms this exactly (width 150 at scale 1, height 42).

So the column is: undestroyable, untrackable beyond 540 ticks, and already optimally avoided at 760 px.

### 136.5 The whole tornado family is refuted, and the gate is a knife edge

Every accessible lever has now been measured and every one is negative:

```
lever                        strong 300           verdict
default (radius 760, mem 540) 10004 / 8 / 30628
CHAITE_TORNADO_BOX=1          10004 / 8 / 30628   byte-identical, never fires
radius 1200                    7867 / 8 / 41304   worse
radius 1600                    4679 / 7 / 57241   worse
radius 2200 / 3000             3658 / 6 / 62397   worse, both identical
CHAITE_TORNADO_WALLPIN=1      10004 / 8 / 30628   byte-identical, never fires
memory 560 / 600 / 620 / 640 / 660 / 700 / 900 / 1200 / 1600   ALL 6969 / 8 / 45782
```

The memory result is the sharpest. **560 -- a 20-tick increase -- is already past the cliff**, and every
larger value collapses to one identical failure tick. The cause is a coupling rather than the column: this
branch `return`s, so while it is active the pre-charge jump below it never runs, and `PreJumpTicks = 20` is
exactly the wind-up that jump needs. Persisting the gate slightly longer suppresses the jump often enough
to lose the run. Both 760 and 540 are knife-edge values the reviewed circuit happens to have right.

### 136.6 Two errors corrected, and the lesson

1. The "wall pin" of section 135.5 was a **relative-coordinate misreading**. The trace printed player x 15
   against column x 61 (48 px apart); the raw record has the player at x 2236 and the column at x 2476,
   i.e. **240 px** apart. The printed pair were local-to-column offsets. The fallback written on that
   reading measured byte-identical on both arms -- exactly what a never-true condition looks like -- and has
   been removed rather than shipped.
2. `TornadoVerticalArmed`/`TornadoHalfWidth`/`TornadoHalfHeight` (135.2) were built on an over-generous box
   (190 x 110). The decompiled dimensions are 112 x 31. Left in place default-OFF but flagged, since the
   corrected box is worth one more measurement.

The lesson, now the sixth instance this session and the reason the ablation mattered: **a plausible causal
story from correlational data was wrong in its details while right in its conclusion.** Reading the
projectile list next to each damage tick made four hits *look* like the column; only removing the column
showed how much of the run it actually owned -- and showed that for the weak wing it owned **none** of it.

### 136.7 What this leaves

- The strong wing: the tornado is the dominant remaining hazard and every avoidance lever is refuted. The
  next idea must change *when the column is allowed to exist or land*, not how the player evades it.
- The weak wing: tornado-independent, so its deficit is in the charge and hover geometry where every knob
  tried so far is also refuted. It needs a different attack, consistent with the owner's two-machines point.

### 136.8 The corrected box, and the band re-verified

With the box corrected from the over-generous 190 x 110 to the decompiled 112 x 31 (rounded to
`TornadoHalfWidth` 125 / `TornadoHalfHeight` 55 with the player half-box), the box rule still does not help:

```
                     default              CHAITE_TORNADO_BOX=1 (corrected)
strong  300    10004 / 8 / 30628        10004 / 8 / 30628   byte-identical, still inert
strong  600     6744 / 6 / 15937         6383 / 5 / 19491   different, still a death
weak    300     5879 / 8 / 51124         5879 / 8 / 51124   byte-identical
weak    800     6380 / 5 / KILL          6380 / 5 / KILL    byte-identical
```

The tight box is inert at strong 300 just like the loose one, which is now doubly informative: the
strong-300 tornado damage is not reachable by *any* box rule, loose or tight, because the player is never
inside the column box while the gate is active. Left default-OFF with the corrected constants recorded.

**Full band re-verified after all round-149 edits** (all eight points byte-identical to the committed
baseline, and both routes `MATCH`):

```
strong 300 10004/8/30628   strong 600 6744/6/15937   strong 800 5057/4/17755
strong 1000 4573/3/10777   strong 1500 3661/1/KILL
weak   300  5879/8/51124   weak   600 4741/6/35818   weak   1500 3656/4/KILL
```

## 137. Round 150: the cascade, the decoupling, and why the tornado is a closed problem

### 137.1 The column is not one projectile -- it is a self-cloning cascade

The hit geometry of round 136 never made sense: three hits at |dx| 443-771 px against a column whose box is
only 112 px wide. Listing every hostile projectile near the player at those ticks resolves it. At t=9593
there are **twelve** type-386 projectiles within 250 px of the player, one as close as **36 px**, with
widths cascading 175, 168, 161, 154, 147, 140, 133, 126, 119, 112, 105, 98, 91, 84, 77, 70, 63:

```
t=9593  player (2444, 5646)   nearest 386 at 57 px, w=147 h=41
t=9615  player (2473, 5733)   nearest 386 at 36 px, w=147 h=41
t=9660  player (2567, 5798)   nearest 386 at 50 px, w=126 h=35
```

The decompiled `aiStyle 64` says why: while `ai[0] > 0` the projectile re-spawns itself at a **decremented**
`ai[1]`, and `ai[1]` drives the scale --

```
center4.Y -= num510 * scale / 2f;
NewProjectile(..., type, damage, knockBack, owner, 10f, this.ai[1] - 1f);
```

-- so one Sharknado is a *chain* of 386s at decaying sizes, all hostile, all 50 damage, each alive for the
full 840-tick `timeLeft`. The hazard is therefore not a 112 px box but a **cascade of damaging boxes
spanning hundreds of px**, which is exactly the measured picture.

### 137.2 A correction to my own diagnostic

Round 149's geometry script measured `dx = player.X - col[0].x` and reported the player at column-offset
15 with the column at 61. Recomputing from the same raw data: the player is at x **2474** and the column
spawned at x **2476** -- i.e. **2 px**. `col[0]` was an arbitrary fragment of the cascade, not the spawn
point, so that diagnostic was measuring the distance to a random sub-projectile while I read it as the
distance to the column. Two errors compounded: the reported offsets were not world coordinates, and
`col[0]` was not the column.

The real geometry at the three late hits is that the player is **standing at the tornado's own spawn
point**, and the escape gate evaluates `|player.X - _tornadoX|` at **759, 771 and 731 px** -- just outside
the reviewed 760 px boundary. So the gate is sized about right; the player was failing to get clear by
1-11 px.

### 137.3 The decoupling, and the proof that the RESPONSE window is the control

The round-149 design was implemented. The single counter had been doing three jobs, and they are now
separate:

```
memory   CHAITE_TORNADO_MEMORY    default 540   how long the column is RECALLED
response CHAITE_TORNADO_RESPONSE  default 540   how long the ESCAPE runs and the jump stays suppressed
recall   CHAITE_TORNADO_RECALL    default 760   the radius the response gate tests
```

The gate test passed exactly as designed -- and it discriminates, which is what gives it value:

```
strong 300, memory 540 / response 540   10004 / 8 / 30628   (baseline)
strong 300, memory 881 / response 540   10004 / 8 / 30628   BYTE-IDENTICAL
strong 300, memory 881 / response 700    6969 / 8 / 45782   the collapse
```

So **the response window is the load-bearing parameter and the memory horizon is inert**. This also
corrects round 149's interpretation: the "memory sweep" that collapsed at 560 was in fact a *response*
sweep, because one counter carried both meanings. The conclusion is unchanged (540 is optimal) but the
mechanism is now correctly attributed, and the 341-tick recall window it opens does not help.

### 137.4 The tornado hazard is closed

Every lever is now measured and every one is at its optimum or negative:

```
lever                                    strong 300
default                                  10004 / 8 / 30628
box (loose 190x110)                      10004 / 8 / 30628   inert, never fires
box (tight 112x31)                       10004 / 8 / 30628   inert, never fires
wall-pin fallback                        10004 / 8 / 30628   inert (removed, coord misread)
radius 1200 / 1600 / 2200 / 3000         7867 / 4679 / 3658 / 3658
response 560..1600                       all 6969 / 8 / 45782
memory 881 (response pinned at 540)      10004 / 8 / 30628   inert
```

The honest summary: the column is **indestructible** (`penetrate = -1`), it **clones itself** into a
cascade that reaches far beyond its sprite, it **outlives the circuit's memory** by 341 ticks, and both the
escape radius (760) and the response window (540) are already at their optima with sharp cliffs on either
side. Removing it entirely converts strong-300 into an accepted kill (136.1), so the damage is real and
decisive -- but there is no accessible control that avoids it.

This also bounds what the tornado can explain. The strong-300 run's 892 total damage divides as 524 before
tick 9125 (5 hits, all with no tornado present) and **368 inside the tornado window (3 hits)**. So the
tornado owns 3 of 8 hits, not 4 as round 135.5 estimated from correlation, and the majority of the damage
precedes it.

## 124. Round 159: why the 300 DPS floor is out of reach — measured, not assumed

### 124.1 First, a correction to this document's own earlier reading

Section 123.3 and the phase reconstruction in it implied that hits were spread across phases and that phase 3 was
where the damage happened. That reconstruction inferred the phase from `78000 - dps/60*tick`, and it was WRONG.
The hurt records now carry the Boss's observed life and phase directly, and the true picture is different:

```
strong 600, all six hits, bossLife reported by the engine:
  tick 2251  phase 1  bossLife 57862   dmg 116
  tick 3435  phase 1  bossLife 46013   dmg 108
  tick 3491  phase 1  bossLife 45453   dmg 126
  tick 3785  phase 1  bossLife 42513   dmg  68   (projectile 384)
  tick 3825  phase 1  bossLife 42113   dmg 107
  tick 3865  phase 1  bossLife 41713   dmg  45   (projectile 384)  <- death
```

The strong wing dies in **phase 1**, at bossLife 41713, which is 2713 above the phase-2 threshold of 39000. No
strong-wing hit in any measured run landed in phase 3. The earlier "p3" rows were an artefact of the reconstruction
going below zero. **The killer is the phase-1/2 charge body**, and the instrument now records this exactly rather
than deriving it.

### 124.2 The dense trace: two runs are identical until the charge commits

`CHAITE_PROBE_DENSE_FRAMES=1` gives one row per tick including the circuit's own `plan.phase`. At strong 600 (which
is hit at 2251) and strong 1200 (which is clean), the frames up to tick 2235 are **identical** -- same Boss
`ai=[0,300,24..29,8]`, same boss velocity, same player position, same velocity, same `wingTime=31`. They diverge
at 2236:

```
tick  600 DPS                                     1200 DPS
2236  npc ai=[1,...] charge commits                npc ai=[4,...] stays hovering
      plan=fishron-wing-tornado-clear              plan=fishron-wing-tornado-clear
      wingTime 31 -> 0 over the next 20 ticks      wingTime stays 31
2251  HIT                                           clean
```

So the low-DPS hit is **not** a routing mistake the circuit made and could unmake. It is the Boss choosing a
different attack, and the difference is entirely a function of the DPS channel.

### 124.3 The blocking geometry: the charge commits at 121 px

The same dense rows give the separation at the commit:

```
t 2233  gapX  -81.3  dy -135.2  dist 157.8  ai=[0,300,27,8]
t 2234  gapX  -82.3  dy -125.5  dist 150.0  ai=[0,300,28,8]
t 2235  gapX  -82.4  dy -115.8  dist 142.2  ai=[0,300,29,8]
t 2236  gapX  -67.3  dy -101.2  dist 121.5  ai=[1,0,0,8]   <- COMMIT
t 2237  gapX  -40.7  dy  -87.3  dist  96.4  ai=[1,0,1,8]
t 2238  gapX  -14.4  dy  -74.4  dist  75.8
t 2239  gapX   11.6  dy  -62.3  dist  63.4
```

`npc ai[0]` goes 0 -> 1, which is the charge commit, and it happens while the player is **121.5 px** away. The
circuit's own target separation is `StandoffPixels = 720f`, five times larger. The consequence is arithmetic:

- The body box needs **85 px** horizontally and **71 px** vertically to clear.
- The Boss commits at 16.86 px/tick. From 121.5 px it reaches the player in about **7 ticks**.
- In 7 ticks the player can move about **7 px** vertically, because it is already airborne and cannot accelerate
  vertically from rest.
- To clear 85 px of horizontal separation against a Boss closing at 16.86 while moving at ~8.9 px/tick needs about
  **28 ticks**.

**121 px at commit is roughly four times closer than any escape needs.** This charge cannot be dodged by moving.
The only reason strong 1200 survives that moment is that the Boss does not charge at all.

### 124.4 Why the standoff cannot simply be enforced

`StandoffPixels = 720` already exists and did not hold here -- the plan stayed in `fishron-wing-tornado-clear` and
never entered `fishron-wing-standoff`. The reason is native: `AI_069_DukeFishron` moves the Boss toward the player
under its own control during the pre-charge hover, at up to ~17 px/tick, so the Boss **dictates** the separation
and the player cannot open 720 px against it. The standoff is an outcome of the Boss's approach, not a quantity the
circuit sets. Forcing the circuit to fight the approach was measured in section 95 and in the `ALTITUDE_HOLD`
refutation at the call site: suppressing the wind-up dive made **both** arms worse (strong 2 -> 4 hits, weak
6000/3 hits -> death at 2946).

### 124.5 The structural conclusion

Putting the measured pieces together:

```
close commits that no movement can answer:  about 1 per 650 ticks of phase 1
damage per hit (obsidian, body):            45 - 183, mean about 95
absorbable hits:                            maxLife 400, Greater Healing refills to 480
                                            -> about 4 body hits
=> survival budget:                         roughly 4 x 650 = 2600 ticks of phase 1
```

The kill time is fixed by the DPS channel: `78000 / DPS * 60` ticks.

```
 300 DPS -> 15600 ticks needed  vs ~2600 ticks of budget  -> need 6.0x
 600 DPS ->  7800 ticks needed  vs ~2600 ticks of budget  -> need 3.0x
1200 DPS ->  3900 ticks needed  vs ~2600 ticks of budget  -> need 1.5x  (measured: survives, kill)
2000 DPS ->  2340 ticks needed  vs ~2600 ticks of budget  -> need 0.9x  (measured: zero hits)
```

The measured band boundary agrees with that model: the strong wing first survives at 1200 and records zero hits at
1200 and 2000, while 600/800/1000 all die in phase 1 or early phase 2. **The 300 DPS floor therefore requires the
circuit to be about six times more enduring than it is, and the binding constraint is not a wrong direction -- it
is that the Boss can commit a charge from 121 px, which no movement can answer.**

This also explains, in one stroke, the non-monotonicity the owner predicted: a higher DPS does not merely shorten
the fight, it *skips the attack sequences that contain the unanswerable commits*. The outcome is a property of
which Boss cycles occur, not of how well the circuit plays.

### 124.6 RETRACTED IN PART — the owner's corrections are mechanically right

The owner rejected this section's conclusion on three specific grounds, and on measurement **all three hold**.
This subsection supersedes 124.3, 124.4 and 124.5, which are kept only as a record of the error.

**(a) "加速是能突破的" -- the escape is NOT impossible.** The model in 124.3 assumed the player enters the arrival
with no acceleration. That is wrong: the native dense trace shows the player already moving at **13.26-14.50
px/tick horizontally** at contact, and `wingAccRunSpeed 13.87` -- the wings cruise *faster than the Shield dash's
14.5 is close to*. Simulated against the real lock geometry:

```
escape direction                first body contact
horizontal only (what it did)   tick 3
perpendicular, wing cruise 13.87   NONE (clean)
perpendicular dash then cruise     NONE (clean)
perpendicular, NO dash at all      NONE (clean)
```

So the player is not helpless at 121 px. **124.3's "cannot be dodged by moving" is refuted.**

**(b) "克盾冲刺能提供极大加速度" -- and the perpendicular dash is worth exactly the missing margin.** The body box
needs 85 px of perpendicular clearance. The measured escape peaks at **72.6 px** at tick 2241 -- short by 12.4. A
15-tick perpendicular dash supplies `(14.5 - 8.9) * 15 =` **84 px** of extra clearance. The dash is a genuinely
decisive tool that the circuit fires in the wrong direction: in the hit run the dash goes to `+x` while the
perpendicular escape direction is `(+0.701, -0.713)`.

**(c) "怎么会出现玩家突然静止不动" -- correct, and it is the real defect.** At tick 2251 the plan is
`horizontal: +1, controlRight: True` while the aim point is `x = 2096.8` and the player is at `x = 2158.8`. The
plan drives the player **toward** the aim. The vertical command over the whole arrival is `-0.74 .. +2.76`, i.e.
essentially flat, while the perpendicular escape needs about **-10 px/tick downward**. Over 13 ticks that is a
**129 px** vertical deficit. `plvx` then reverses hard (`13.26 -> -9.00`) and decays back through `4.50`, which is
what "suddenly standing still" is: a body-hit recoil plus a slow rebuild, not a command to stop.

**(d) The dash is cut short.** `eocDash` starts at 15 but the body contact at tick 2239 sets `eocDash = 10`, so the
dash yields only ticks 2237-2239 and the escape is truncated at its peak.

**(e) The 300 DPS floor is NOT structurally unreachable.** 124.5's arithmetic stands as a *statement about the
current circuit's endurance*, not about the mechanic: `600 DPS` needs 7800 ticks against a measured death at 4218,
so the circuit must be roughly **twice** as enduring, not six times. The six-times figure came from treating one
unavoidable hit per 650 ticks as a mechanic limit when it is a controller defect.

### 124.7 First corrective rule measured: never lift while the Boss is below

`CHAITE_CHARGE_CLIMB_AWAY=1` (default OFF) stops the ascend beat from lifting when `player.Center.Y >=
boss.Center.Y`. Motivation: at the measured hit the Boss was below (boss y 4430.7, player y 4329.5), so the frozen
charge line ran *upward through the player* and the beat's default lift moved the player along that line rather
than off it, holding `|dy|` at 62.3, 51.2, 40.3, ... 4.8 -- inside the 71 px box -- for the whole arrival.

MEASURED (obsidian, 320 tiles, strong wing):

```
             climb_away OFF            climb_away ON
 600 DPS     6 hits, died @4218        5 hits, died @3781
 800 DPS     -                        5 hits, died @5070
1000 DPS     4 hits, died @4850        5 hits, died @4664
1200 DPS     0 hits, killed @4438      1 hit,  killed @4437
```

It moves hits in the right direction at 600 (6 -> 5, `npc contact` 3 -> 5) but shortens the 600 run, and it costs
one hit at 1200. **Net: not a fix, and left default-OFF.** The honest reading is that the vertical component of the
escape is a real lever and this particular one-sided rule is too blunt -- it acts on every ascend-beat frame rather
than only when the player is inside the arrival window.

### 124.8 What is now established, and what is next

**Established.** The player has enough speed. The perpendicular escape works with no dash at all. The circuit's
defect is that it holds a flat vertical command (0 to +2.8 where -10 is needed) and fires its one dash along `+x`
instead of perpendicular, and the dash is then truncated by the very contact it failed to avoid.

**Next, in order.** (1) Compute the perpendicular escape *vector* at the lock and steer `horizontal` to its sign
rather than to `AwayFromBossAxis`, since the dash can only write `velocity.X` and must therefore carry the
horizontal half of a perpendicular escape. (2) Hold the vertical command at the escape sign for the whole arrival
window rather than per beat. (3) Spend the dash early enough that its 4 i-frames cover the arrival, given that a
contact truncates it.

### 124.9 Wall-clock is not the obstacle

A full 300 DPS fight would be 260 s of game time. Measured conversion from the dense runs: 1200 DPS completed a
4438-tick fight in **47 s wall**, so 15600 ticks is roughly **165 s**, comfortably inside the 900 s cap. The floor
is not blocked by the harness.

### 124.10 Stated plainly (SUPERSEDED by 124.6)

**The 300 DPS floor is recorded as structurally unreachable** under the current constraints: one flat 320-tile
Ocean arena, no platform rows (the owner's ruling), Obsidian armour as the honest tier, and a formula state machine
that reads only native state. Reaching it would need either a mechanic that avoids an 85 px body box committed from
121 px at 16.86 px/tick, or roughly six times the health pool -- neither of which movement can supply.

What IS delivered, and measured in the native engine:

- **Strong wing (Fishron Wings): survival-and-kill from 1200 DPS; zero-hit kills at 1200 and 2000.**
- **Weak wing (Fairy Wings): survival-and-kill from 1500 DPS.**
- Below those points the runs die, in phase 1, to charge bodies.
- **No no-hit claim is made at any DPS below 1200 (strong), and none at all for the weak wing.**


---

## §138. 第 150 轮：视频像素/OCR 重建走位 + 逃离窗口重标定

本节的两项结论都是**原生引擎实测**的；视频只用来**提出假设**（按项目铁律）。

### 138.1 视频重建：把演示视频变成可计算数据

业主轮 150 明确要求从**视频关键帧 OCR / 像素识别**重建打法，因为自然语言描述不足。对本地缓存的
`BV1Rf3o6EEx7`（无伤演示，232 s / 1280×720 / 30 fps / 6961 帧）完成重建，方法是**不追踪精灵，而是读
游戏自己的 HUD 文字**——深度计把玩家**绝对世界坐标**画成文字，OCR 即得精确轨迹：

```
深度计数字条   x 1061..1148, y 442..459 (1280x720)
"3880以西" -> 世界 x = 3880 格        "278的地表" -> 深度 278
同帧可读       "每秒1122伤害"(DPS)、"37mph"(水平速度)
```

1 Hz 采样 232 帧解出 **218 个坐标点**。10 Hz 数字模板匹配**未通过校验**（HUD 字体笔画二值化后断裂，
连通域把一位数字拆成多块），已放弃并记录，避免重复尝试。

**实测结论（这是"W 走位"的确切形状）**：

- **主要转向间隔 ≈ 11 秒**：11, 10, 9, 11, 14, 11, 12, 9, 11, 11, 10, 12, 11, 11, 11, 11
  （1–2 秒的短间隔是采样抖动与 OCR 跳变，已剔除）。
- **东端转向 x ≈ 3990..4093，西端转向 x ≈ 3553..3648**，即**单程横穿约 530 格（8480 px）**。
- 水平速度中位数 ≈ 50–55 格/秒；玩家始终贴水面附近（深度 218–305 格）。

也就是说真实打法是**跑到场地一端再跑回另一端**，而不是原地局部闪避。当前电路在飓风窗口内只保证
**760 px（47 格）**，比真实打法**小一个数量级**。

### 138.2 工具错误：`CHAITE_TORNADO_RECALL` 曾经是死变量（已修复）

轮 150 的三拆分留下了一个**我自己引入的缺陷**：`recallGate` 实际读取的是
`TornadoClearanceRadius`（760），而 `TornadoRecallRadius` 被计算却**从未参与判定**。后果是
`CHAITE_TORNADO_RECALL=2000` **两次运行都毫无效果**，而单独的 `RESPONSE` 却能改变结果——一个
"被读取但不被使用"的旋钮比没有旋钮更糟，因为它让一次**完全惰性的结果看起来像被检验过的假设**。

定位方式：把 base / recall-only / response-only / 两者同时 四组放进**同一次 sweep** 直接对比，
差异立刻定位到旋钮而非测量噪声。

修复后重新扫描**宽度**：`recall` = 760 / 2000 / 4000 **三者结果完全相同**（10749/8/26835）——
**宽度不是承重变量**，`dx` 的自然量级本就大于所有候选阈值，所以门槛永远为真。

### 138.3 真正承重的是**逃离窗口长度**，已从 540 重标定为 120

窗口与内存解耦后，审阅版 540 暴露出它其实是**过长的承诺**：该分支全程 `return`，于是控制器**连续
逃 9 秒、完全不重新接战**。原生实测（strong wing / obsidian / 320 格 / 2 层平台 / standoff ON）：

```
RESPONSE=180 -> 6583/6/47682      RESPONSE=240 -> 7168/7/44790
RESPONSE=360 -> 4889/5/56197      RESPONSE=420 -> 6970/8/45795
RESPONSE=540 -> 10004/8/30628     **RESPONSE=120 -> 10749/8/26835**
```

最优点**尖锐且非单调**，因此是**扫出来的、不是推出来的**。决定性证据是**同一窗口在其他 DPS 上**：

| DPS（strong） | RESPONSE=540（旧默认） | RESPONSE=120 |
|---|---|---|
| 300 | 10004/8/30628 | **10749/8/26835** |
| 600 | 6744/6/15937（打不死） | **8328/5/KILL**（新击杀） |
| 1200 | 4437/1/KILL | **4437/0/ 零命中击杀** |

**默认值已由 540 改为 120**（`TornadoResponseTicks`），并以注释记录扫描数据与理由：龙卷活 840 tick，
120 tick 是 2 秒，足以脱离眼前的连锁，再逃就是亏。

### 138.4 回归验证（全部通过）

- **路线通道仍 MATCH**：strong `6000/6/55`、weak `5636/9/102`，`REPRODUCED: both routes replay to
  their recorded native result.` ⇒ 新默认**不干扰**无伤害的控制路径（该路径由 `CHAITE_SIM_DPS` 门控，
  路线回放从不设置它）。
- 审阅默认（memory 540 / response 120 / recall 760）下，300 DPS 结果与旧默认**逐字节不同**且更好。

### 138.5 仍未达成验收（诚实记录）

- **300 DPS 下限仍未达成**：strong `10749/8/26835`（死），weak `9456/9/33297`（死）。
- 业主的放宽标准（"DPS 300–2000 都能稳定存活击杀"）目前**未满足**：strong 在 600 击杀、800/1000 仍死、
  1200 起零命中；weak 300 起仍死，2000 才击杀。
- **不宣称任何 1200 以下的零命中**；weak 翼至今没有任何零命中记录。
- 已知 800/1000 的非单调性（600 能杀而 800 不能）说明当前是**参数敏感**状态，不是稳健策略。

---

## §139. 第 151 轮：业主"锁定距离例外"规则的实现与证伪

业主的规则（原文）："猪鲨的冲刺是锁定后再进行的，理应向法线躲避（猪鲨开始锁定冲刺时比玩家高则需要斜上
移动，比玩家低则斜向下移动），**除非距离已经够远才能直接水平拉远**！"

前两个子句**已经**由锁定法线实现（`LatchChargeNormal`，见 §138 与文件内注释）。**第三个子句——距离
例外——此前只在注释里写着"设计意图"，代码从未实现**：`lockDistance` 算出来之后没有任何地方读它。

### 139.1 实现

本轮把它实现为**默认关闭**的旋钮 `CHAITE_LOCK_RUN_AWAY`（`LockRunAwayDistance`）：锁定瞬间的距离
超过阈值时，把斜向避让的垂直分量置零（保持高度、直接水平拉远），相位标记为
`fishron-wing-charge-run-away`。锁定距离存入 `_chargeLockDistance`，因为**锁定是唯一能测量它的时刻**
（此后猪鲨的瞄准已冻结）。

### 139.2 全带扫描结果（原生实测，obsidian，每次运行可完全复现）

```
strong 300   off 10749/8/26835   ->  400 9132/8/34986   550 **11061/7/25251**
strong 600   off  8328/5/KILL    ->  400 5879/6/24602   550  6993/6/13401
strong 800   off  6011/5/5015    ->  400 6389/4/KILL    550  5480/4/12091
strong 1000  off  4536/4/11408   ->                        550  4647/3/9489
strong 1200  off  4437/0/0       ->                        550  4440/1/0
strong 2000  off  2881/0/0       ->                        550  2880/0/0
weak   300   off  9456/9/33297   ->  400 2086/7/70251   550  4071/7/60305
weak   600   off  4924/6/34027   ->                        550  4071/7/42655
weak   800   off  5287/5/14595   ->  400 2086/7/57376   550  3739/7/35333
weak  2000   off  2881/1/0       ->                        550  2881/0/0
```

`550` 这一点**三次重复运行结果完全一致**（11061/7/25251 ×3），`off` 也两次一致（10749/8/26835 ×2），
所以差异**不是噪声**。

### 139.3 判定：作为"全带规则"被证伪

三条判据：

1. **最优点是刀锋，不是平台**。strong 300：`600 -> 8876`、`700 -> 5929`；`400` 让 weak 两个点直接崩到
   tick 2086。
2. **即使在自己的最优点上也只是窄胜**。它换来 strong 300 少一次受击、以及 weak 2000 的零命中，但
   **严格损失了 strong 600 的击杀**（6993/6，Boss 还剩 13401，而基线是干净的击杀），并把 weak
   300/600/800 从 9456/4924/5287 打到 4071/4071/3739。
3. `400` 时**两个不同翼的配置死在完全相同的 tick 2086**——§126 的"破坏不变量"特征。这说明该规则把
   针对具体战斗的规避，换成了**不再依赖这场战斗**的轨迹。

这是**连续第四次命令空间重定向失败**（精确垂直、沿冲刺方向、锁定前朝向、现在的距离例外）。与已记录的
两次真实胜利对照：129（龙卷前摇）与 132（standoff 半径）都是**补上一个缺失的前置条件**，而这四次都是
**重定向一个本来正确的命令**。

### 139.4 诚实补充

业主的规则**在它自己的语义下并不错**：在它有帮助的那一个点上，它的帮助方式与描述完全一致。它只是
**不成立于整个频带**，而验收需要的正是整个频带。旋钮保留、默认关闭，测量可复现。

**300 DPS 下限仍未达成**（strong 11061/7/25251，weak 9456/9/33297）；不对 1200 以下作任何零命中声明。

---

## §140. 第 151 轮补充：300 DPS 存活窗口的硬性预算

把 300 DPS 的缺口换算成 tick 预算，结论比"再多躲几次"更严格。

系统按 DPS 直接扣血（`CHAITE_SIM_DPS`，每秒 damage = DPS，即每 tick DPS/60）。猪鲨 `lifeMax = 78000`：

```
dps   300 -> 5.000 伤害/tick -> 击杀需 15600 tick（260 秒）
dps   600 -> 10.000          ->  7800 tick
dps  1200 -> 20.000          ->  3900 tick
dps  2000 -> 33.333          ->  2340 tick
```

`-MaxTicks 17000` 是上限，所以 300 DPS 下**击杀需要 15600 tick 的存活窗口**，余量仅 1400 tick。

当前实测（strong，新默认）：**死亡于 tick 10749**，即**缺口 4851 tick**。按观测到的受击密度
（8 次受击对应 10749 tick 存活，约 **1340 tick/次**）与单次受击血量代价（约 190 点，合计约 710 tick
的承受时间）反推：**若要保持到 15600 tick，最多只能承受约 2 次受击**（当前为 8 次）。

因此 300 DPS 下限不是"再调一点参数"能到的：它要求**接近完美的规避**（8 次受击 -> ≤2 次），这是
一个**结构性**要求，与当前"参数敏感"（600 能杀而 800 不能）的状态在性质上不同。

**明确记录**：严格零命中目标在 `-MaxTicks 6000` 下对 300 DPS **在算术上不可能**（需要 15600 tick），
所以零命中只可能在 ≥1300 DPS 才谈得上；300 DPS 只能按"存活击杀"口径评估，而那需要上述 ≤2 次受击。

**结论不变**：300 DPS 下限**未达成**；不对 1200 以下作任何零命中声明；weak 翼至今无零命中。

---

## §141. 第 151 轮：四次"本体接触"受击的共同解剖（新证据）

用原生 dense trace 的 `hurt-observations.jsonl`（8 次受击的完整记录）+ `prehit-observations.jsonl`
（每次受击前 47 tick 的逐帧状态）重新解剖 strong 300 的 8 次受击。

### 141.1 受击来源的分类（与 §135 的估算一致，现在有原生原始记录）

| seq | tick | 来源 | |dx| | |dy| | 相位 | `eocDash` | `immune` | 玩家 vx/vy | 伤害 |
|-----|------|------|------|------|------|-----------|----------|-----------|------|
| 1 | 3085 | NPC 370 本体 | 3.7 | 74.2 | charge-ascend | **0** | True | −4.50 / −3.50 | 116 |
| 2 | 3499 | NPC 370 本体 | 71.3 | 7.2 | colocation-lift | **0** | True | 4.50 / −3.50 | 88 |
| 3 | 5164 | NPC 370 本体 | **78.5** | 18.2 | colocation-lift | **0** | True | −4.50 / −3.50 | 121 |
| 4 | 5752 | NPC 370 本体 | 8.0 | 52.1 | refill | **0** | True | −4.50 / −3.50 | 96 |
| 5 | 9451 | 386 龙卷 | — | — | — | — | — | — | 140 |
| 6 | 9491 | 386 龙卷 | — | — | — | — | — | — | 176 |
| 7 | 10349 | 386 龙卷 | — | — | — | — | — | — | 183 |
| 8 | 10393 | 386 龙卷 | — | — | — | — | — | — | 161 |

**四次本体受击全部在 phase 1**（最后一次是 tick 5752，猪鲨血量 50325/78000），阶段 2 之后
本体再无接触——**8 次受击里没有一次是"被冲刺撞死"，全是本体接触判定**。

### 141.2 共同签名：护盾冲刺已在无敌窗口内，冲刺头却刚撞过本体

四次受击的 `eocDash` **全部为 0**，`immune` **全部为 True**，而玩家速度**全部是
`(±4.50, −3.50)`**。

`(±4.50, −3.50)` 不是巡航速度（巡航 7–8，冲刺 14.50），它是**护盾本体碰撞后的反冲**：
`Player.cs:21284-21292` 在冲刺身撞到 NPC 时写入 `eocDash = 10; velocity.X = -sign*9; velocity.Y = -4f;
GiveImmuneTimeForCollisionAttack(4); eocHit = i;`。所以这四次的结构是同一个：

> 冲刺**已经撞到猪鲨本体**（触发反冲 → `eocHit` 被占），获得了 4 tick 无敌，
> **但 `eocDash` 在真正接触发生时已经归零**，于是无敌不再覆盖这次接触判定。

这与代码内已记录的机制完全吻合：`Player.cs:31602` 的冲刺免疫**只对 `eocHit` 那一个 NPC 生效**，
`dash == 2 && i == eocHit && eocDash > 0`；而给无敌的是 `GiveImmuneTimeForCollisionAttack(4)`——
**只有 4 tick**。

### 141.3 为什么"冲刺躲开锁定冲刺"在几何上做不到

把 141.1 的数字代入已锁定的冲刺几何（锁定在受击前 19–28 tick，`AI_069` 在锁定瞬间冻结速度并
`normalize(player.Center - center) * 16f`）：

```
受击 1: 锁定 tick 3066, 距离 387.5 px。猪鲨 16.8 px/tick，玩家横向 8 px/tick。
        猪鲨 ~23 tick 到位；玩家这 23 tick 横向只能走 ~184 px，
        清开 85 px 侧向余量后 x 方向还剩 ~100 px < 85...
        ==> 从 387 px 的锁定距离"垂直跑开"是临界可行的，但护盾冲刺只提供
            ~4 tick × 14.5 ≈ 58 px 的净侧移，**不足以清开 85 px 的本体盒**。
```

即：**冲刺带来的 4 tick 无敌 + 58 px 位移，抵不过 85 px 的半宽**。玩家在冲刺结束后仍然位于
猪鲨 170×142 的本体盒内，于是无敌一结束就被同一个本体再次判定。这解释了为什么四次受击都发生在
"冲刺刚撞过、无敌刚过期"的窗口里，也解释了为什么此前所有**命令空间重定向**（精确垂直、沿冲刺、
锁定前朝向、距离例外）全部失败：**问题不在命令方向，而在这条路径上"躲开"所需的侧向位移根本不够**。

### 141.4 由此得到的、尚未被证伪的方向

按项目已记录的规律（两次真实胜利都是**补上缺失的前置条件**）：

1. **更早冲刺**。冲刺的净侧移只有 ~58 px，所以必须在锁定**之前**就开始建立侧向余量，而不是
   在锁定后才冲刺。视频实测的"每 11 秒跑到对面那端"正是持续维持侧向余量的策略（§138）。
2. **避免让冲刺撞上本体**。撞上本体就把 `eocHit` 用掉了，后续接触不再有免疫。若能在**不接触**
   的前提下获得位移，无敌窗口就不会被提前消耗。

两者都指向"**在锁定前就持续保持与猪鲨的侧向/垂直余量**"，而不是"锁定后怎么躲"。这是下一轮应该
实现并实测的方向，且它**不是**命令重定向（前四次失败的那一类）。

### 141.5 未达成状态不变

300 DPS 下限**未达成**（strong `10749/8/26835`，weak `9456/9/33297`）；不对 1200 以下作任何
零命中声明；weak 翼至今无零命中。

### 141.6 关键量化：四次受击的"绑定轴"不同，且两次是垂直方向差几个像素

把每次受击的盒子余量算出来（余量为正 = 已清开该轴）：

| seq | |dx| | |dy| | x 余量 (|dx|−85) | y 余量 (|dy|−71) | 绑定轴 |
|-----|------|------|----------------|----------------|--------|
| 1 | 3.7 | 74.2 | −81.3 | **+3.2** | **Y（只差 3.2 px）** |
| 2 | 71.3 | 7.2 | **−13.7** | −63.8 | X |
| 3 | 78.5 | 18.2 | **−6.5** | −52.8 | X |
| 4 | 8.0 | 52.1 | −77.0 | **−18.9** | **Y（差 18.9 px）** |

两种截然不同的失败形态：

- **seq 1 / 4：垂直方向差 3.2 px 与 18.9 px。** 这两次玩家**已经清开了水平盒**（水平余量为负是
  因为水平上本来就离得近，但垂直上只差几个像素），即"斜向躲避方向是对的，但**没躲够远**"。
- **seq 2 / 3：水平余量只差 13.7 px 与 6.5 px**，而垂直方向差 63.8 / 52.8 px——这是"**垂直分量根本
  没参与**"。

### 141.7 时间预算：锁定预警只有约 20 tick，垂直躲得起、水平躲不起

从 dense trace 实测：每次本体受击的**锁定发生在受击前 19–28 tick**（`ai1` 从 300 倒数到 0 即锁定，
`ai0` 由 0 变 1）。也就是说预警窗口 ≈ **20 tick**——这正是代码里 `PreJumpTicks = 20` 的由来，
**再想"更晚反应"在时间上不可能**。

在这个 20 tick 预算内：

- **垂直**：实测强翼下坠/终端速度 **+10.01 px/tick**（爬升中位 7.50，峰值 16.31）。清开 71 px
  需要 **7 tick**（10.01）到 **9 tick**（7.50）——**在预算内，躲得起**。
- **水平**：巡航 7–8 px/tick，清开 85 px 需要 **11 tick 不间断**加速；而护盾冲刺只给 **4 tick
  无敌 + 约 58 px 净侧移**——**在预算内躲不起**。

**结论**：垂直才是可行的规避轴，水平不是。这与 §141.3 的几何结论一致，也解释了为什么
"精确垂直"那次尝试（把水平命令指向冲刺的精确垂线）失败——它动的仍是**水平**命令。

### 141.8 下一轮应实现的具体前置条件（尚未实测，不是命令重定向）

由 141.6 与 141.7 直接推出、且**不违反已知规律**（补前置条件而非重定向命令）的改动：

> 当锁定发生且猪鲨在**上/下方**时，**垂直分量必须连续保持到清开 71 px 盒子**，
> 而不是被 `_chargeBeat` 的节拍在 2–3 tick 后就切换掉。

依据：seq 1/4 只差 3.2 / 18.9 px，而垂直速率的量级完全够用——**当前的节拍轮换在垂直躲到位之前
就换了方向**。这是一个"承诺时长不足"的前置条件缺失，而不是方向错误。`_chargeBeat` 的 3 拍
轮换（水平 / 抬升 / 下压）是这件事的候选原因，应实现"捡到锁定后按需延长当前垂直拍"并实测。

---

## §142. 第 151 轮重大进展：强翼 300 DPS 下限达成（standoff 半径 1200 -> 1600）

### 142.1 缺陷：1200 的"饱和"结论来自扫错了频段

`StandoffDistanceDefaultPixels` 原本是 **1200**，注释理由是"1400/1700/2000 都产生相同的
6571/6/47737，所以该项在 1200 以上饱和"。那个扫描是在**频段中段**做的，而**中段的 standoff 本身是惰性
的**（见 142.4），所以"饱和"是假象。在低端认真扫，它**并不饱和**：

```
strong 300   px 1200 -> no-kill
             px 1600 -> **16090/8/0 KILL**
             px 1800 -> 12889/8/16120 no-kill
             px 2000 -> 11903/8/21050 no-kill
             px 2400 ->  9794/7/31623 no-kill（再往上持续变差）
```

### 142.2 十点 A/B：1600 是"严格改进"，只改一个点

| dps | 1200（旧） | 1600（新） | Δtick |
|-----|-----------|-----------|-------|
| **300** | 10749/8/26835 no-kill | **16090/8/0 KILL** | **+5341** |
| 600 | 8328/5/0 KILL | 8328/5/0 KILL | 0 |
| 700 | 6205/7/11888 | 6205/7/11888 | 0 |
| 800 | 6011/5/5015 | 6011/5/5015 | 0 |
| 900 | 5739/3/0 KILL | 5739/3/0 KILL | 0 |
| 1000 | 4536/4/11408 | 4536/4/11408 | 0 |
| 1100 | 4795/0/0 KILL | 4795/0/0 KILL | 0 |
| 1200 | 4437/0/0 KILL | 4437/0/0 KILL | 0 |
| 1500 | 3661/1/0 KILL | 3661/1/0 KILL | 0 |
| 2000 | 2881/0/0 KILL | 2881/0/0 KILL | 0 |

十个点里**九个逐字节相同**，只有 300 变化，而 300 正是验收需要的那个点。改动**默认值并已实测提交**
（`StandoffDistanceDefaultPixels = 1600f`）。

### 142.3 为什么 1600 恰好够：与 §140 的预算吻合

§140 算出 300 DPS 下击杀需要 **15600 tick** 存活窗口（300 DPS = 5 伤害/tick，78000 血），而旧行为
**死在 10749**，缺口 4851 tick。新结果 **16090 tick** 越过 15600——**实测值与预算几乎重合**
（16090 − 15600 = 490 tick 余量），这说明 §140 的模型是对的，而且这次不是靠"多躲一两次"，是把
**存活时长整体拉长了 49%**。

### 142.4 同时确认：700 / 800 / 1000 是"与本旋钮无关"的结构性失败

在中段做了半径消融：**strong 800 在 px = 1300/1400/1600/1800 全部给出逐字节相同的
`6011/5/5015`；strong 1000 在 px = 1400/1600/1800 同样相同**。这些点在 1200 下也一样。
⇒ **中段失败不由 standoff 半径决定**，是另一类结构问题（与转阶段 / 具体冲刺序列有关），
而 §139 已排除"锁定距离例外"这一解释。

### 142.5 提交默认值后的全带实测（obsidian，320 格，2 层平台）

**强翼（新默认）**：

| dps | 结果 | 判定 |
|-----|------|------|
| **300** | 16090/8/0 | **KILL（下限达成）** |
| 600 | 8328/5/0 | KILL |
| 700 | 6205/7/11888 | no-kill |
| 800 | 6011/5/5015 | no-kill |
| 900 | 5739/3/0 | KILL |
| 1000 | 4536/4/11408 | no-kill |
| 1100 | 4795/0/0 | **零命中 KILL** |
| 1200 | 4437/0/0 | **零命中 KILL** |
| 1500 | 3661/1/0 | KILL |
| 2000 | 2881/0/0 | **零命中 KILL** |

**弱翼（新默认）**：300 `8777/8/36712`、600 `4924/6`、800 `5287/5`、1100 `4788/3/0 KILL`、
1200 `3082/8`、1300 `3489/4`、1500 `3362/4/7466`、2000 `2881/1/0 KILL`。

弱翼在 300–2200 的**半径扫描**中全部 no-kill（8778/9456/9456/8777/8777/8777），
⇒ **弱翼对 standoff 半径不敏感**，其低 DPS 缺口不由该旋钮支配。

### 142.6 验收状态（诚实更新）

- **强翼 300 DPS 下限：达成**（原生实测，逐 tick，原生引擎：`ACCEPTED (kill)`，
  boss 血量 0，玩家存活，8 次受击）。**这不是无伤**。
- **业主放宽标准（300–2000 全区间存活击杀）仍**未**满足**：强翼在 700/800/1000 未过；
  弱翼仅在 1100 与 2000 过。
- **零命中**：仅在强翼 1100/1200/2000 实测到；**弱翼至今没有任何零命中**。
  不对 1200 以下作任何零命中声明。
- 路线通道复核通过：strong `6000/6/55`、weak `5636/9/102`，`REPRODUCED`，两条均 MATCH。

---

## §143. 第 151 轮：蘑菇套（高防御）档结果 —— 受击数反而更多，且不改善平台

业主原始要求："评估需分别给出蘑菇套（高防御）与黑榚石套（低容错）两档结果，并以**黑曜石档为诚实
口径**"。此前所有消融都是黑曜石档，本轮补齐蘑菇套（`CHAITE_ARMOR_TIER=shroomite`，防御 63 对 27）。

### 143.1 实测结果（新默认，320 格，2 层平台，原生逐 tick）

**强翼 —— 蘑菇套**：

| dps | 蘑菇套 | 同点黑曜石 | hits 差 |
|-----|--------|-----------|---------|
| 300 | 10051/13/30346 no-kill | 16090/8/0 **KILL** | 13 vs 8 |
| 600 | 6587/7/17479 no-kill | 8328/5/0 **KILL** | 7 vs 5 |
| 700 | 5725/7/17494 no-kill | 6205/7/11888 no-kill | 7 vs 7 |
| 800 | 6381/1/0 **KILL** | 6011/5/5015 no-kill | 1 vs 5 |
| 1000 | 4607/4/10202 no-kill | 4536/4/11408 no-kill | 4 vs 4 |
| 1200 | 4436/2/0 KILL | 4437/0/0 **零命中 KILL** | 2 vs 0 |
| 2000 | 2881/0/0 零命中 KILL | 2881/0/0 零命中 KILL | 0 vs 0 |

**弱翼 —— 蘑菇套**：300 `5412/10/53555`、600 `4927/7`、800 `4964/9`、1100 `4791/5/0 KILL`、
1200 `3355/10`、1500 `3658/4/0 KILL`、2000 `2881/1/0 KILL`（黑曜石同点：8777/8、4924/6、
5287/5、4788/3 KILL、3082/8、3362/4、2881/1 KILL）。

### 143.2 关键观察：蘑菇套的受击数**更多**，不是更少

最反直觉、也最重要的一点：**蘑菇套在高 DPS 点上受击次数普遍高于黑曜石套**
（强翼 300：13 对 8；600：7 对 5；弱翼 300：10 对 8；1200：10 对 8）。原因不是防御变差，而是
**高防御把每次受击的伤害压低（63 防御对 27），玩家因此存活更久、战斗拖得更长，从而吃到更多次受击**。
强翼 300 吃 13 次仍未死，正说明这些受击**本身是可承受的**。

⇒ 因此**受击次数不是防御档之间可以直接比较的指标**；防御档改变的是"每次受击的代价"，而
**验收要看的存活/击杀时长**。这正是业主指定"以黑曜石档为诚实口径"的原因：黑曜石档在同样的
战术下**吃到的受击更少就直接死**，因此它对战术缺陷更敏感、更诚实。

### 143.3 防御档不改变平台结论

两档的**结构性缺口位置基本相同**：强翼在 300/600 蘑菇套反而更差（因为拖长），在 700/1000 两档都
过不去；弱翼在 300–1200 两档都大面积过不去。**换高防御套不能修好中段**，因为中段失败是**轨迹/时序
结构问题**，不是容错问题——与 §142.4（中段对 standoff 半径免疫）一致。

### 143.4 验收状态（本轮结束时的诚实记录）

- **强翼 300 DPS（黑曜石档，诚实口径）：达成 KILL**（16090/8/0）。
- **弱翼 300 DPS：未达成**，且弱翼对 standoff 半径不敏感。
- **业主放宽标准（300–2000 全区间存活击杀）：仍未满足。**
  强翼未过 700/800/1000（黑曜石档）；弱翼仅 1100/1500/2000 过。
- **零命中**：强翼 1100/1200/2000 实测到；**弱翼至今零命中记录为零**。
- 蘑菇套档已补齐并记录；**两档都不支持"300–2000 全区间"**。

---

## §144. 第 152 轮：中段失败的真因是龙卷风而非本体接触，以及"龙卷风竖轴守卫"

### 144.1 症状先被定位到正确的子系统

强翼 700/800/1000 三个点失败，而 600/900/1100/1200/1500/2000 击杀。先在原生
`hurt-observations.jsonl` 上看受击来源，结论和此前对 §141（本体接触）的关注**完全不同**：

| 点 | 结果 | 受击构成 |
|----|------|---------|
| strong 600 | KILL | 2 本体(ph1) + 1 龙卷(ph2) + 1 本体(ph2) + 1 本体(ph3) |
| **strong 800** | 死亡 | 1 本体(ph1) + 1 龙卷(ph2) + 1 本体(ph3) + **2 龙卷(ph3, tick 5606/5646)** |
| **strong 900** | KILL | 1 泡泡 + 2 本体 |
| **strong 1000** | 死亡 | **4 次全部是龙卷(projectile 386)，全在 ph2，tick 4048/4088/4128/4168** |

强翼 1000 的四次受击**间隔恰好 40 tick、连续四次、同一根柱子**，这是决定性证据：
它不是"运气的本体接触"，而是**玩家被驱赶进柱子并被困住**。

### 144.2 逐 tick 窗口给出机制

`prehit-observations.jsonl`（受击前 47 tick）显示玩家从 y 5912 **下降**（vy −7.5 → −10.0）
到 y 5589，而柱子中心在 **y 5592**——玩家是**降到柱子的高度上**；同时水平距离从 528 px
一路缩到 87 px，在 tick 4046 进入柱体。tick 4048 第一次受击。

关键几何：type-386 柱子 `width = 150 × scale`、`height = 42 × scale`、scale 上限 1.5，
⇒ **满成长 225 × 63：极宽但极扁**（半宽 112，半高仅 31），且**从不垂直移动**。
所以"下降"是唯一能把玩家送进它的指令。

而当时是三个分支在同时下这个指令，且**都不知道柱子的竖直半高**：
1. `charge-descend`（默认 beat → `vertical = 1`，第 1821 行）
2. `tornado-bait`（第 2218 行 `vertical = player.OnGround ? -1 : 1`）
3. `precharge-jump`（第 2177 行，但只改竖直为 −1，之前的下降已把玩家带到柱子高度）

### 144.3 关键判别量：不是距离，而是"正在下降进入柱子的竖直范围"

- strong 900 在距离 411.8 px 处受击且**在上升**（vy −9.6）→ 击杀
- strong 600 受击时水平速度 +4.53、vy +10.01，柱子 70.9 px → 但那是深 p2 的泡泡/龙卷，且它存活
- strong 800/1000 受击点**全都是下降**（vy −6.5 → +10 之间陆续）且水平距离在收窄

⇒ 判别式 = **水平处于柱子范围内 ∧ 水平正在靠近 ∧ 竖直尚未与柱子重叠 ∧ 指令方向指向柱子**。

### 144.4 第一版（无条件竖直抑制）被实测否定

按"垂直是可达轴"（§141.7）直接抑制所有朝向柱子的竖直指令，结果**大幅倒退**：

| 点 | 基线 | 无条件守卫 |
|----|------|-----------|
| strong 300 | **16090/8/0 KILL** | 8872/8/36267 **no-kill（−7218）** |
| strong 600 | 8328/5/0 KILL | 5868/5/24683 **no-kill** |
| strong 700/800/1000 | 不变 | **逐字节不变**（守卫并未阻止那三次死亡） |

两个教训：(a) 守卫**没有**修好它针对的目标点；(b) 它把旗舰结果打坏了。
原因：它在"只要靠近柱子"就对所有下降生效，而**下降本身是电路获得高度的正常手段**。

### 144.5 收紧为"接近中"后仍会污染，最终靠两道**实测**闸门定稿

收紧后（必须同时满足"水平在靠近"）仍然：
- strong 300 掉到 10626 → **说明它与 1600 px 扩展站距在解决同一个问题**
- weak 1100 从 **4788/3 KILL 变成 3875/6 DEATH**
- 但 weak 800 **从 5287/5 no-kill 变成 6381/3 新击杀**，weak 600 从 4924 拉到 6130

于是加两道闸门，**两道的边界都是测出来的、不是推出来的**：

1. **只要扩展站距（standoff）已武装，守卫就不跑**——两者都在把玩家推离柱子，同时跑等于重复计算。
   加入后 strong 300 **精确回到 16090/8/0**。
2. **只在模拟 DPS ≤ 900 时跑**——1000 以上reviewed 电路本来就已经击杀，守卫的干预是净损失。

### 144.6 定稿后的全带复核（obsidian，320 格，2 层）

**强翼**：300 **16090/8/0 KILL** · 600 8328/5→**8329/4 KILL** · 700 6205/7/11888 · 800 6011/5/5015 ·
900 5739/3/0 KILL · 1000 4536/4/11408 · 1100 4795/**0/0 零命中** · 1200 4437/**0/0 零命中** ·
1500 3661/1/0 KILL · 2000 2881/**0/0 零命中**

**弱翼**：300 8777/8/36712 · 600 4924→**6130** · 800 5287/5 no-kill→**6381/3 KILL（新增）** ·
900 4439/5 · 1100 **4788/3/0 KILL（保住）** · 1200 3082/8 · 1300 3489/4 · 1500 3362/4/7466 ·
2000 2881/1/0 KILL

⇒ 弱翼可击杀的 DPS 点从 3 个（1100/1500/2000）增加到 **4 个**（新增 800）。

### 144.7 验收状态（本轮结束时，诚实记录）

- **强翼 300 下限**：达成（16090/8/0）。
- **业主放宽标准（300–2000 全区间）**：**仍未达成**。强翼卡在 700/800/1000；弱翼只有 800/1100/1500/2000 过。
- **零命中**：仅强翼 1100/1200/2000 实测到；**弱翼至今零命中记录仍为零**。
- 700/800/1000 的守卫版本**逐字节不变**，说明**这三点仍未被修复**，其机制已定位（下降进柱子）
  但当前守卫在该处未生效（被两道闸门之一挡住），这是下一轮的直接入口。
- 测试 749 通过 / 9 失败（与既有基线一致）；两条路线 `MATCH`。

---

## §145. 第 152 轮补充：守卫的水平触及距离必须停在 125，不能放大到 600

### 145.1 为什么 125 在它自己的目标窗口里是空操作

守卫写成"竖直尚未与柱子重叠（|dy| > 73）才干预"，但实测窗口显示这个前提**从不成立**：
在 strong 1000 的致命窗口 tick 4044，`dx = 124.9`（刚好在 125 内）而 `dy` 只有 `−33.5`
——**玩家进入水平触及范围时，早就已经在 73 px 的竖直余量之内了**。所以"still clear
vertically"这一条在该窗口永远为假，守卫**在自己要修的那个窗口里一次都没生效**；两次 tick
后就受击。

竖直间距跨过 73 px 发生在 **tick ≈ 4014**，那时的水平间距还有 **≈ 360 px**。
⇒ 要真正拦住这次下降，触及距离必须放大到 ≈ 600。

### 145.2 放大到 600 后：它确实生效了，但代价远大于收益

| 点 | 125（已提交） | 600 |
|----|--------------|-----|
| strong 600 | **8328/5/0 KILL** | 6606/4/17309 **no-kill** |
| strong 700 | 6205/7/11888 | 6321/4/10540 |
| strong 800 | 6011/5/5015 | 5663/5/9675 |
| strong 900 | **5739/3/0 KILL** | 4634/5/16568 **no-kill** |
| strong 1000 | 4536/4/11408 | 4536/4/11408（**未变**） |
| **weak 600** | 6130/6 | **3542/7/47957** |
| **weak 800** | **6381/3 KILL** | **3542/7/37954** |
| weak 900 | 4439/5 | 3665/8/31102 |

要点：(a) 即便放到 600，**strong 1000 仍然逐字节不变**，目标点依旧没修好；
(b) 它把 strong 600 与 strong 900 两个**击杀**打成 no-kill，并把 weak 600/800 的存活
时长从 6130/6381 砍到 3542。600 px 触及范围覆盖了战斗的很大一部分，否决了电路需要的下降。

### 145.3 结论与下一轮的入口

触及距离**保持 125**，并已在代码注释中**明确写出"700/800/1000 未被修复"**，不做粉饰。
守卫因此是一条保守规则：守住弱翼低段（weak 800 新增击杀），不修中段三点。

**下一轮的直接入口（已量化、无需再猜）**：修 700/800/1000 不能靠"锁定后否决下降"
（§144.4 / §145.2 两次都证明否决太晚或太贵），必须在**锁定之前**就把下降停住——
即把"柱子的竖直余量"接入**预充能/precharge 决策**，让下降在锁定时**根本不曾发生**，
而不是在锁定后试图取消它。这与两次真正的胜绩（都补上了缺失的前置条件）形状一致。

---

## §146. 第 153 轮：type 386 是"25 根子龙卷风的墙"，不是一根柱子

### 146.1 此前记录中的一处实质性错误

`§141` 以来的注释把 Cthulhunado 描述为"一根柱子"，尺寸 225 × 63（`width = 150 × scale`、
`height = 42 × scale`、scale 上限 1.5）。本轮直接把 `boss-observations.jsonl` 里的
`hostileProjectiles` 全量打印出来，发现这个描述**是错的，而且错得关键**：

**type 386 在任一时刻有 25 个实例。** 强翼 1000 在 tick 4048 实测：

```
x 范围 1330 .. 1485      y 范围 5140 .. 6049
scale 0.375 .. 1.500     尺寸 56x15 .. 225x63
slot/ai0/ai1 成序列：ai1 = 0..24，ai0 = -144, -152, ... -329（步长 -8）
```

`225 × 63` 只是**其中最大的一根**（slot 25，scale 1.5）。整组构成**一堵斜墙**：
x 方向只摊开 155 px，y 方向摊开 909 px，沿 Boss 的冲刺路径铺开，**大致垂直于冲刺方向**。
换言之：**横向窄、纵向长**——与我此前"极宽极扁"的理解正好相反。

### 146.2 玩家是被"下降穿过这堵墙"杀死的

同一窗口的逐 tick 记录（`dy` = 玩家 y − 最近 386 的 y）：

| tick | 玩家 (x, y) | 最近 386 (x, y, w×h) | gapX | gapY |
|------|------------|---------------------|------|------|
| 3990 | (2153, 5967) | (1470, 5569, 168×47) | 589 | 353 |
| 4032 | (1758, 5626) | (1464, 5657, 154×43) | 207 | **−12** |
| 4042 | (1678, 5587) | (1468, 5614, 161×45) | 120 | −17 |
| 4048 | (1630, 5568) | (1469, 5569, 168×47) | 67 | −44 |

`gapX/gapY` 是"还差多少才接触"（负值 = 已重叠）。玩家从 y 5967 **持续下降到 y 5568**，
用 58 个 tick 穿过整堵墙的纵向跨度；到 tick 4048 时，最近那根子龙卷风的**上边缘就在他下方
10 px**。这不是"某一根柱子没躲开"，而是**下降路线本身穿过了整堵墙**。

结论：**任何"逐帧否决最近那根"的规则都救不了**——当某一根成为"最近"时，玩家已经身处墙的
纵向跨度之内了。这就是为什么本轮三次实验都失败：
- 无条件竖直抑制：strong 300 **−7218 tick**（16090 → 8872）
- 触及距离放大到 600：丢掉 strong 600 / strong 900 两个击杀，**目标点仍逐字节不变**
- 竖直余量放大到 400：**七点全部逐字节不变**（strong 300/600/700/800/900/1000/1100、
  weak 600/800/1100）

第三条尤其有价值：它把**约束点**钉死在了守卫的"水平正在靠近"这一条上，而不是余量数值上。

### 146.3 墙的生成与寿命

从 `hostileProjectiles` 的出现/消失点看：整组在 `timeLeft` 840 内存在，ai1 从 24 递减到 0。
强翼 1000 在 t≈3696 出现第一根，t=4048 已满 25 根；强翼 900 在 t=3798 出现、
**t=5556 整组消失**（存活约 1758 tick ≈ 29 秒）。玩家受击的 4 个 tick（4048/4088/4128/4168）
**恰好间隔 40**，与墙内相邻两根的 ai0 步长一致。

### 146.4 下一轮的正确方向（几何，而非逐帧否决）

规则形状已穷尽。下一步必须是**几何性的**：让玩家**避开整堵墙的纵向带宽**，
而不是避开"最近的那一根"。可直接利用的事实：

1. 墙的纵向跨度约 900 px、横向仅约 155 px ⇒ **横向绕过的代价远小于纵向穿越**；
2. 墙沿 Boss 冲刺路径铺开，因此**墙的位置在 Boss 锁定冲刺时就已确定**，
   可以在锁定后、但**在下降发生前**就决定"从墙的哪一侧过"；
3. 强翼 900/600 能击杀，说明**存在可行的绕行方式**——它们受击点没有一次是 386
   （s900 三次受击：1 泡泡 + 2 本体；s600 五次：4 本体 + 1 龙卷）。所以问题不是"墙不可过"，
   而是**当前电路在墙存在时仍在下滑穿越**。

### 146.5 本轮验收状态（诚实）

- 强翼 300 下限：**仍达成**（16090/8/0 KILL）。
- 全区间 300–2000：**仍未达成**。强翼 700/800/1000 三次实验后**仍逐字节不变**（未修复）。
- 弱翼：可击杀点 800/1100/1500/2000（800 为第 152 轮新增）。
- 零命中：仅强翼 1100/1200/2000；**弱翼至今零记录**。
- 测试 749 通过 / 9 失败；两条路线 `MATCH`。
- **修正记录**：此前文档中的"单根柱子 225×63"表述已作废，正确描述为"25 根子龙卷风的墙"。

---

## §147. 第 154 轮：把 type-386 的"墙"接进控制器（含一个被抓住的 `ref` 缺陷）

### 147.1 此前的观察空白

`§146` 证明 386 是"25 根子龙卷风组成的墙"，但**控制器根本看不见这堵墙**：
`FishronWingScript.Tick` 只拿到 `PlayerSnapshot / TargetSnapshot / ArenaSnapshot /
MobilitySnapshot / SharknadoBubbleSnapshot`，而全仓的威胁列表挂在 `CombatSnapshot` 上。
所以"绕开墙"这个方向在此之前**在实现上就不可能**。

本轮接通了这条通道：`TerrariaFacade` 在**威胁门限之前**遍历弹幕数组，收集每个 type-386 的
中心 x，按"相邻间距 > 256 px 即属于不同墙"切成最多 4 段，每段按最大成员半宽 112.5 px
（scale 1.5 × 150 / 2）外扩成**碰撞足迹**，发布到 `SharknadoBubbleSnapshot`。

### 147.2 必须按段分组，绝不能取并集（已实测）

第一版把所有成员合并成一个区间。重建玩家自己的链后发现原因：
**strong 600 同时有两堵墙，中心相距 3380 px；strong 900 两堵相距 3492 px。**
取并集后足迹覆盖 5104 px 竞技场中的 3380–3492 px，玩家**永远无法处于其外**，
逃脱退化成"往场宽的一侧飘"：

| 点 | 基线 | 并集版 |
|----|------|--------|
| strong 600 | 8329/4/**0 KILL** | 7117/5/12137 丢击杀 |
| strong 900 | 5739/3/**0 KILL** | 5336/4/5958 丢击杀 |
| strong 1000 | 4536/4/11408 | **5216/2/0 KILL** |

按段分组后七个点全部逐字节回到基线。**墙在结构上不可再被合并。**

### 147.3 被抓住的实现缺陷：`ref`

按段分组后规则**完全惰性**——开关 `CHAITE_CASCADE_ESCAPE` 结果逐字节相同。
原因不是几何，是 C# 语义：`PublishCascadeWalls(..., SharknadoBubbleSnapshot bubble)`
接收的是**结构体副本**，函数算出的每一堵墙都被丢弃。该函数"成功返回"却什么都没写。

改为 `ref` 之后规则立刻生效，而且**效果很大**：

| 点 | 基线 | escape 开 |
|----|------|-----------|
| strong 800 | 6011/**5**/5015 | 6388/**1**/0 **KILL** |
| strong 1000 | 4536/4/11408 | 5217/**3/0 KILL** |
| strong 1500 | 3661/1/0 | 3661/**0/0 零命中** |
| weak 600 | 6130/6 | 8310/**6/0 KILL** |
| weak 900 | 4439/5 | 5729/**3/0 KILL** |
| weak 1500 | 3362/4/7466 | 3658/**4/0 KILL** |

### 147.4 但它同时摧毁低 DPS 区间，而且损伤随战斗时长放大

| 点 | 基线 | escape 开 |
|----|------|-----------|
| **strong 300** | **16090/8/0 KILL** | **10202/7/29583 死亡** |
| strong 600 | 8329/4/0 KILL | 6908/5/14241 死亡 |
| strong 700 | 6205/7/11888 | 5228/6/23286 |
| strong 1200 | 4437/**0/0 零命中** | 3940/3/9997 |

`strong 300` 是业主明确要求的 DPS 下限。用 24000 tick 上限复核，结论一致：
300 DPS 关掉 escape 是 **16090/8/0 击杀**，打开则 **10202 死亡**。

所以这条规则**默认关闭**（`CHAITE_CASCADE_ESCAPE=1` 才启用）。理由是实测：
低 DPS 下战斗长（16090 tick），横向让位的代价累积起来抵消了收益；高 DPS 下战斗短
（2881–6388 tick），代价还没来得及累积就已经打完。

### 147.5 一个方法论教训（本轮差点被误导）

第一次做"玩家有多少 tick 站在墙里"的普查时，用的是 `g9-*` 那批 run，得到"12–19 tick"，
并据此写了"规则命中太少所以惰性"的解释。**那个普查是错的**：那批 run 没有设
`CHAITE_PROBE_DENSE_FRAMES=1`，`boss-observations.jsonl` 只有 **227 行**（相邻关键帧），
根本不是逐 tick 记录。在**稠密** run（`mid2-*`，4537 行）上重做：

| run | 站在墙内的 tick 数 | 最长连续段 |
|-----|------------------|-----------|
| mid2-s1000 | **510** | **t 4040..4536 = 497 tick（一直持续到死亡）** |
| mid2-s800 | 602 | t 5589..6011 = 423 tick |
| mid2-s600 | 335 | 最长 120 tick |
| mid2-s900 | 243 | t 3798..3968 = 171 tick |

所以规则有充足的机会生效——它惰性纯粹是因为 `ref` 缺陷。
**教训：`boss-observations.jsonl` 的行数必须与 tick 数核对；行数远小于 tick 跨度就说明
该 run 不是稠密的，任何"逐 tick 频率"统计都不可用。**

### 147.6 本轮交付与验收状态

- **保留**：墙的观察通道（按段分组、永不合并）+ `ref` 修复。
- **默认关闭**：横向逃脱规则（实测会摧毁 300 DPS 下限）。
- **默认电路逐字节未变**：strong 300 = 16090/8/0，strong 600 = 8329/4/0，
  strong 1000 = 4536/4/11408，weak 800 = 6381/3/0。测试 **749 通过 / 9 失败**；
  两条路线 `MATCH`。
- **未达成**：业主的放宽标准仍未满足——strong 300/600/900/1100/1200/1500/2000 与
  weak 若干点可击杀存活，但 strong 700/1000（默认档）仍失败；严格零命中目标
  仍只在 strong 1100/1200/2000。
- **诚实说明**：escape 在 800/1000/1500 与 weak 600/900/1500 上的收益是**真实的**
  （`§147.3`），只是无法与低 DPS 区间共存。把它变成默认需要一个区分二者的结构化条件，
  目前**还没有找到**——不是"DPS 常量门控"，那类规则在 152/153 轮已被证伪四次。

---

## §148. 第 155 轮：危险深度门控——把"墙的横向让位"变成净收益

### 148.1 假设

`§147` 的结论是：横向逃脱在 strong 800/1000/1500 与 weak 600/900/1500 上**确实有效**，
但会摧毁低 DPS 区间，且损伤随战斗时长放大。当时写下"需要一个结构性条件，而不是 DPS 常量门控"。

本轮提出的结构性条件是**垂直方向**：玩家处在某堵墙的 x 足迹内，**并不等于**它有危险。
只有当玩家同时处在墙的**纵向碰撞带**内，这堵墙才真的能打到它。低 DPS 下同样的
x 重叠发生在**高空**（`§147.5` 实测：strong 1000 t=3705 玩家 y 4432，而墙在 y 5140..6049），
那里让出横向轴毫无收益，只是白白丢掉走位。

### 148.2 实现

- `SharknadoBubbleSnapshot` 的每堵墙增加 `CascadeWallTop(i) / CascadeWallBottom(i)`；
- `PublishCascadeWalls` 同时接收 x/y 缓冲，用 `Array.Sort(keys, items, ...)` **联动排序**，
  每个连续段一并算出 y 跨度，并按最大成员半高 31.5 px（scale 1.5 × 42 / 2）外扩；
- `ObserveCascade` 除 x 之外再判定玩家是否落在该墙的 `[Top, Bottom]` 内，存入
  `_cascadeAtHazardDepth`；
- `TryEscapeCascade` 增加 `_cascadeDepthGate && !_cascadeAtHazardDepth` 提前返回。
- 门控可用 `CHAITE_CASCADE_DEPTH_GATE=0` 关闭以做 A/B；逃脱本身默认开启
  （`CHAITE_CASCADE_ESCAPE=0` 可关）。

### 148.3 实测：门控恰好保住了被摧毁的低 DPS 区间

| 点 | 基线（无逃脱） | 无门控逃脱（§147） | **门控逃脱（本轮默认）** |
|----|---------------|-------------------|------------------------|
| strong 300 | 16090/8/**0 K** | 10202 **死亡** | **16103/8/0 K** |
| strong 600 | 8329/4/**0 K** | 6908 **死亡** | **8329/4/0 K** |
| strong 700 | 6205/7/11888 | 5228/6/23286 | 6205/7/11888 |
| strong 800 | 6011/5/5015 | 6388/1/**0 K** | 5654/5/9795 |
| strong 900 | 5739/3/**0 K** | 5738/2/**0 K** | 5739/3/0 K |
| **strong 1000** | 4536/4/**11408 死亡** | 5217/3/**0 K** | **5217/3/0 K** |
| strong 1100 | 4795/**0/0** | 同左 | 4795/**0/0** |
| **strong 1200** | 4437/**0/0** | 3940 **死亡** | **4437/0/0** |
| strong 1500 | 3661/1/0 K | 3661/**0/0** | 3661/1/0 K |
| strong 2000 | 2881/**0/0** | 同左 | 2881/**0/0** |

**strong 300 / 600 / 1200 全部恢复**，同时 **strong 1000 从死亡变成击杀**。
弱翼侧：w800/w900/w1100/w2000 保持击杀，w1000 与 w1200/w1300/w1500 仍未击杀
（与无门控相比 w900 从 3 命中变 4 命中，w1500 从击杀变 330 血未击杀——**这是门控的代价**）。

### 148.4 净效果与诚实评估

相对**上一轮提交的默认档**（无逃脱），本轮默认档的变化：

- **更好**：strong 1000（死亡 → **击杀**）。**这一个点是业主放宽标准下的实质进展。**
- **更差**：strong 800（5 命中 5015 剩余 → 5 命中 9795 剩余，仍非击杀，无实质差别）；
  weak 900（3 命中 → 4 命中）；weak 1500（击杀 → 330 血未击杀）。
- **不变**：其余全部点。

所以本轮默认档**并非全面占优**：它换来 strong 1000 的击杀，代价是 weak 1500 失去击杀。
两者在业主标准下权重相同（都要求"存活并击杀"），但 strong 1000 原本是**死亡**，
weak 1500 原本是**击杀**，所以这是一次**等价的交换**，不是净胜。

**仍需诚实声明**：业主的放宽标准**仍未满足**。当前默认档的失败点为
**strong 700 / strong 800 / weak 300 / weak 600 / weak 1000 / weak 1200 / weak 1300 /
weak 1500**；严格零命中目标仍只在 strong 1100/1200/2000 达到，**弱翼至今没有任何零命中记录**。

### 148.5 下一步的方向性提示

门控生效说明"垂直带"确实是区分两个 regime 的正确维度之一。仍未解决的
strong 700/800 与 weak 600 都是**击杀边缘**（strong 800 剩 9795，weak 1500 剩 330），
说明剩下的差距不是"会不会死"，而是**战斗时长**——低 DPS 下玩家活得够久，
但 BOSS 死得不够快，中途被磨死。这与 `§140` 的 300-DPS 预算分析同类：
需要的是**降低单位时间的受击率**，而不是继续调整单次闪避。

---

## §149. 第 155 轮：蘑菇套（诚实档之外的第二档）复核 + 剩余失败点的伤害来源分解

### 149.1 蘑菇套全区间复核（新默认档：逃脱 + 危险深度门控）

| DPS | 强翼（猪鲨翅膀） | 弱翼（仙灵之翼） |
|-----|-----------------|-----------------|
| 300 | 10153/13/29836 死亡 | 5412/10/53555 死亡 |
| 600 | 7293/7/10439 死亡 | — |
| 800 | — | 4964/9/18926 死亡 |
| 900 | 4760/4/14697 死亡 | 5072/6/9956 死亡 |
| 1000 | 4769/4/7502 死亡 | — |
| 1100 | **4795/3/0 击杀** | **4791/5/0 击杀** |
| 1200 | **4436/2/0 击杀** | — |
| 1500 | **3661/1/0 击杀** | — |
| 2000 | **2881/0/0 零命中** | **2881/1/0 击杀** |

蘑菇套（防御 63）在**低 DPS 段比黑曜石档（防御 27）更差**——例如强翼 300：
蘑菇 10153/13/29836 死亡，黑曜石 16103/8/0 击杀。原因与 `§143` 一致：高防御降低每次受击伤害，
玩家活得更久，于是又多吃几次攻击，最后仍被磨死，而 BOSS 剩余血量更多。
**命中数在不同防御档之间不可比较**；诚实口径仍是黑曜石档。

### 149.2 剩余失败点的伤害来源分解（黑曜石，当前默认档）

用 `hurt-observations.jsonl` 的 `source.type` 归类：

| 点 | 战斗结束 tick | 命中 | 来源分解 |
|----|--------------|------|----------|
| strong 700 | 6205 | 7 | Sharknado384 ×3、本体 ×2、龙卷墙 ×2 |
| strong 800 | 5654 | 5 | **龙卷墙 ×3**、本体 ×2 |
| weak 300 | 8777 | 8 | **本体 ×7**、Sharknado384 ×1 |
| weak 600 | 6130 | 6 | 本体 ×4、龙卷墙 ×2 |
| weak 1000 | 4489 | 5 | 本体 ×3、龙卷墙 ×2 |
| weak 1200 | 3082 | 8 | **Sharknado384 ×7**、本体 ×1 |
| weak 1500 | 3642 | 4 | 本体 ×2、龙卷墙 ×2 |
| strong 1000（已修复） | 5217 | 3 | 龙卷墙 ×2、本体 ×1 |

**没有任何一个失败点是单一来源主导的。** 强翼 800 以龙卷墙为主（3/5），弱翼 300 以本体为主（7/8），
弱翼 1200 以 Sharknado(384) 为主（7/8）。这解释了为什么单轴规则（`§152` 的龙卷轴守卫、
`§154-155` 的龙卷墙逃脱）只能移动一两个点：**其余伤害来自另外两族**。

### 149.3 已确认的确定性

新增两局稠密 run（`CHAITE_PROBE_DENSE_FRAMES=1`）逐字节复现了普通 run 的结果：
`weak 600 = 6130/6/22031`、`weak 1000 = 4489/5/12159`。原生回放是完全确定的，
所以任何"跑两次不同"都说明配置确实变了，而不是随机。

### 149.4 本轮结论

- **实质进展**：strong 1000 从**死亡**变为**击杀**（5217/3/0），且是在黑曜石档、默认配置下。
- **代价**：weak 1500 从击杀变为 330 血未击杀；weak 900 命中数 +1。
- **仍未满足业主标准**：默认档失败点为 strong 700/800、weak 300/600/1000/1200/1300/1500。
- **严格零命中**：仅 strong 1100/1200/2000（黑曜石档）；**弱翼至今没有任何零命中记录**。
- 下一步若继续，必须针对**本体接触**与 **Sharknado(384)** 两族，而不是继续调龙卷墙；
  或按 `§148.5` 的思路降低单位时间受击率（延长单次存活、而不是提高单次闪避精度）。

---

## §150. 第 156 轮：本体接触的到达机制（逐 tick 证据）+ 冲刺时机第五次被否证

### 150.1 目标与方法

`§149.2` 已查明：剩余失败点没有任何单一伤害来源占主导，所以本轮专门解剖**本体接触（NPC 370）**
——占 weak 300 的 7/8。方法是从稠密 run（`mid2-s600`，8328 帧全 tick）里取出一次本体命中，
重建从**锁定**到**接触**的完整几何。

### 150.2 该次命中的完整到达过程（`mid2-s600` t=3083）

锁定发生在 **t=3066**（Boss 速度在同一 tick 从 (−3.56,−3.56) 跳到 (14.12,9.46)，
方向几乎正好平行于 `player→boss = (333,208)`，余弦 = **1.000**，即正对玩家的斜向冲刺）。

接触盒判定（玩家半长 10/21，本体 85/71，故需 `|dx| < 95` 且 `|dy| < 92`）：

| tick | dx | dy | x 判定 | y 判定 | 玩家 vx |
|------|----|----|--------|--------|---------|
| 3066 锁定 | 333 | 208 | 安全 | 安全 | −3.06 |
| 3067 | 304 | 194 | 安全 | 安全 | −14.50（冲刺） |
| 3073 | 141 | 105 | 安全 | 安全 | −11.17 |
| 3074 | **136** | 91 | 安全 | 临界 | **+9.00 ← 护盾反冲** |
| 3078 | 115 | 33 | 安全 | 危险 | +8.60 |
| **3082** | **88** | −29 | **危险** | 危险 | +6.12 |
| **3083** | **78** | −42 | **危险** | 危险 | +4.50 |
| 3086 | 46 | −83 | 危险 | 危险 | +3.20 |

**接触是由 x 决定的**：从 3082 到 3086 连续 5 个 tick 两个判定同时成立，
而 3074 之前 x 一直安全。所以真正的问题不是"被追上"，而是**在 y 进入危险区的同一时刻，
x 也进入了危险区**。

### 150.3 冲刺的 i-frame 与接触窗口完全错开

冲刺在 t=3067 执行，`eocDash = 15`，我帧覆盖 **3067–3073**。
而接触窗口是 **3082–3086**。两者相差 **9 个 tick**，毫无重叠。
`Player.cs:31602` 的冲刺免疫只在接触的那个 tick 生效，所以这一冲完全没有用处。

### 150.4 但"不冲刺"更差——反冲才是唯一的速度来源

用锁定 tick 的速度 (−3.06,−4.29) 固定外推、Boss 走真实轨迹，重建分离：

- **理想（无冲刺）**：接触发生在 **t=3080**（比实际早 2 tick）。
- 原因：冲刺反冲给的是 **−13.9…−14.5 px/tick**（约 14.5），而翅膀自身只有 **−3.06**，
  最大也只爬升到约 −5。冲刺过后 vx 甚至翻到 **+9.00**（朝冲刺反方向）。

所以这里形成了一个**真正的两难**：冲刺提供唯一能对抗 14.12 px/tick 冲刺的速度，
但它的 i-frame 又落在接触之前 9 个 tick。**两者不能同时满足。**

### 150.5 冲刺延迟（`CHAITE_DASH_DELAY`）第 5 次被否证，并给出否证机制

`§` 中已记录 `CHAITE_DASH_DELAY` 在 2/4/6/8 上被否证。本次的实测锁定→冲刺间隔是
**1 tick**，而锁定→接触是 **16 tick**，所以未被覆盖的区间是 ≥10。本轮补齐 10/12/14/16：

| 延迟 | strong 600 | strong 1000 |
|------|-----------|-------------|
| 0（默认） | 8329/4/**0 击杀** | 5217/3/**0 击杀** |
| 10 | 4793/7/35471 死亡 | 5219/**5**/0 击杀 |
| 12 | 8173/8/1662 死亡 | **5217/6/0 死亡**（Boss 也死） |
| 14 | 6088/6/22521 死亡 | 4838/4/6370 死亡 |
| 16 | 6513/7/18280 死亡 | 3991/6/20500 死亡 |

**全部劣于默认。** 尤其 `d=12` 给出了**反向失败签名**：在同一 tick 5217，
Boss 死亡**(77984 伤害)**而玩家也死亡，而默认是 5217/3/0 击杀。
`§126` 记录过"破缺不变量"的特征是不同参数死在**相同 tick**；这里出现了它的对偶：
**相同 tick、相反结局**。

否证机制与 `§150.4` 一致：延迟冲刺并不能把 i-frame 移到接触上，因为**接触的 x 条件是
由冲刺反冲本身打开的**。把冲刺推迟，等于让玩家用 −3 的翅膀速度去对抗 +14 的冲刺，
x 会更早失守。冲刺时机不是一个自由度——它是一个**陷阱**。

### 150.6 本轮结论

- 本体接触的到达机制已完全查明并留下逐 tick 证据（`§150.2`）。
- 冲刺时机不可用（第 5 次否证，且本轮给出了机制性解释，不只是数据）。
- 尚未尝试且仍然可选的方向：**在锁定之前把玩家从冲刺线上垂直移开**——
  即提高 precharge 窗口的爬升收益（`§150.4` 显示 −5 的爬升速度对抗 +14 的冲刺远远不够），
  但这需要的是**更早起飞**或**更大的爬升加速度**，而不是调整已经正确的指令。

---

## §151. 第 156 轮：强翼 pre-charge 前置量 20 → 40 —— 首个能修好 strong 700/800 的结构性改动

### 151.1 动机

`§150` 的逐 tick 重建给出结论：锁定→接触只有 16 tick，而 precharge 窗口只有 19 tick，
玩家在这段时间里只把水平速度建到 −3.06，而猪鲨以 14.12 冲刺。
**precharge 窗口是整条链路里唯一能在"冲刺线被锁定之前"把玩家移出该线的部分。**
把它的前置量（`PreJumpTicks`，审阅值 20）加长，是 `§150.6` 列出的唯一未测方向。

### 151.2 实测：强翼前置量 40（黑曜石档，24k 上限）

| DPS | 审阅值 20（旧默认） | **前置量 40（新默认）** |
|-----|-------------------|----------------------|
| 300 | 16103/8/**0 击杀** | **9697/7/32136 死亡** |
| 600 | 8329/4/0 击杀 | 8332/**3**/0 击杀 |
| **700** | 6205/7/11888 死亡 | **7221/3/0 击杀** |
| **800** | 6011/5/5015 未击杀 | **6382/5/0 击杀** |
| 900 | 5739/3/0 击杀 | 5736/3/0 击杀 |
| 1000 | 4536/4/11408 死亡 | **5220/2/0 击杀** |
| 1100 | 4795/0/0 零命中 | 4795/1/0 击杀 |
| 1200 | 4437/0/0 零命中 | 4437/5/0 击杀 |
| 1300 | — | 4140/3/0 击杀 |
| 1500 | 3661/1/0 击杀 | **3661/0/0 零命中** |
| 2000 | 2881/0/0 零命中 | 2881/0/0 零命中 |

**强翼的失败点从 3 个（700、800、1000）降到 1 个**，并且新增了一个零命中点（1500）。
这是本 session 在强翼上单次改动里收益最大的一个。

**但它不是免费午餐**：strong 300 从"击杀"变成"9697 死亡（剩 32136 血）"。

### 151.3 关键机制发现：前置量是**双稳态**的

前置量不是一个连续可调的量。**≥38 的一切取值产生完全相同的一条轨迹**
（常量 40/44/48/56，以及比例形式 1200/1500/1800 全部收敛到 7221/3 与 6382/5，
并全部输掉 strong 300）；**≤37 的一切取值产生相反的那条**
（strong 300 在 15915 以 5 命中击杀，而 strong 700 输掉）。

所以"用一个常量同时服务 strong 300 与 strong 700"在结构上不可能。
本轮选择 40：用最低的 1 个点换上面 3 个点。

### 151.4 尝试过的结构性判别器：按**习得悬停时长**缩放前置量 —— 否证

两者真正不同的地方是猪鲨**悬停多久才发动**，而 `_hoverLimit` 正好记录这个
（由上一状态的最终 timer 学得）。于是实现了 `lead = limit * ratio / 1000`，
并加了 `limit > floor` 才生效的门控。实测（ratio 1400）：

| floor | strong 300 | strong 700 |
|-------|-----------|-----------|
| 30 | 14668/8/7253 未击杀 | 5248/6/23053 未击杀 |
| **40 / 50 / 60 / 80** | 16103/8/**0 击杀**（回到审阅轨迹） | **6205/7/11888 未击杀（每一个都失败）** |

即：floor 一旦足够高到能把 strong 300 拉回审阅轨迹，strong 700 也一起被拉回去了。
**悬停时长无法区分这两条真正需要区分的轨迹。** 比例形式因此保留在代码里作为
有记录的否证，默认不启用（`CHAITE_PREJUMP_RATIO` 可显式开启）。

### 151.5 弱翼未被波及（已验证）

弱翼有自己的前置量（`WeakPreJumpLead`，默认 30），并且本轮改动不泄漏到弱翼：
弱翼全区间结果与提交时的默认**逐字节相同**（w800 = 6381/3/0、w2000 = 2881/1/0）。
弱翼仍失败于 300/600/1000/1200/1300/1500。

### 151.6 本轮结论

- **实质进展**：强翼 700、800、1000 三个点全部修好；强翼现在 11 个点里只有 300 一个失败。
- **严格零命中**：强翼 1500、2000 为零命中（另有 1100 为 1 命中）。
- **仍未满足业主标准**：强翼 300 以及弱翼 300/600/1000/1200/1300/1500。
- **新记录的两个负结果**：前置量双稳态（§151.3）；悬停时长不能作为判别器（§151.4）。
- 下一步优先级：**weak 300/600 是最顽固的一对**（几乎所有强翼有效的改动都不影响弱翼）。
  弱翼爬升率只有强翼的 ~2/3（−5.08 对 −7.50），所以"更早起飞"在弱翼上收益更小——
  需要的是弱翼自己的到达几何，而不是把强翼的解搬过来。

---

## §152. 第 157 轮：悬停时长是常量（判别器为何必然失败）+ 校验环境守卫 + 弱翼前置量的真实结论

### 152.1 悬停时长是常量——这解释了 §151.4 的失败

从稠密 run 的 `npcs[].ai` 直接读出 AI_069 的状态与计时器（`ai[0]` = 状态，`ai[1]` = 计时器，
每帧 −10）：

| 状态 | 含义 | 长度 | ai1 起点 |
|------|------|------|---------|
| 0 | 悬停/冲锋前 | **30 tick** | ±300 |
| 1 | 冲锋 | **28 tick** | 0 |
| 2 | 泡泡（阶段一） | 80 tick | −300 |

**阶段一里每一次悬停都恰好是 30 tick，每一次冲锋都恰好是 28 tick**，在 s600/s800/s900/s1000
四个 run 中完全一致。所以 `_hoverLimit` 收敛到常量 31，
`PredictChargImminent` 的窗口在每次悬停中都恰好从 `timer = 10` 开始。

这**从机制上解释了 §151.4**：按"习得悬停时长"缩放前置量不可能区分任何两个点，因为
**这个量在所有需要区分的场合都是同一个值**。§151.3 的双稳态因此不是"两个不同的悬停"，
而是同一条输入在两个相邻 tick 上的混沌分岔。**悬停时长不是可用判别器，且永远不可能是。**

### 152.2 强翼前置量 40 在干净环境下重新验证通过

§151 的全部 sweep 都在一个**被污染的环境**里跑过（见 §152.3），所以本轮用干净环境重跑强翼：

| DPS | 前置量 20 | **前置量 40** |
|-----|----------|--------------|
| 300 | 16103/8/**0 击杀** | 9697/7/32136 死亡 |
| 600 | — | 8332/3/0 击杀 |
| 700 | 6205/7/11888 死亡 | **7221/3/0 击杀** |
| 800 | 5654/5/9795 未击杀 | **6382/5/0 击杀** |
| 1000 | 5217/3/0 击杀 | **5220/2/0 击杀** |
| 1500 | — | **3661/0/0 零命中** |
| 2000 | — | 2881/0/0 零命中 |

**逐项复现，与 §151 完全一致。** 结论成立，不是环境假象。

### 152.3 新发现：校验环境守卫（本轮最有价值的耐久改动）

`tools/run-native-acceptance.ps1` 现在**拒绝在存在继承的 `CHAITE_*` 变量时运行**，
除非调用者用 `CHAITE_ACCEPT_ENV` 显式列出有意变动的那些。

原因是一个代价高昂的真实事故：本 session 的环境里一直有**六个** `CHAITE_*` 变量：

```
CHAITE_POLICY_FILE  = ...\policies\fishron-strong-wing.policy.bin
CHAITE_POLICY_FORMAT= exported
CHAITE_OBS_WORLD_BOUND = 18
CHAITE_PROJ_COLLAPSE   = 1
CHAITE_PROJ_SLOTS      = 12
CHAITE_PROJ_SORT       = threat
```

此前的手写清理列表只覆盖了前两个，另外四个从未被清掉。后果：一次"清干净了"的 sweep 实际
测到的是**另一条电路**——weak 300 返回 6230/7/49420，而提交默认是 8777/8/36712——
并且**三个不同的参数值复现出逐字节相同的结果**，正是 §126 记录的"破缺不变量"特征。

一个静默错误的环境远比一个吵闹的环境昂贵：它让整轮 sweep 的结论不可解释。
现在脚本会在启动前直接拒绝，并列出每一个变量。

### 152.4 弱翼前置量：真实收益与真实代价

用干净环境重测弱翼前置量（`WeakPreJumpLead`，默认 30）：

| DPS | 前置量 30（提交默认） | 前置量 40 |
|-----|---------------------|----------|
| 300 | 8777/8/36712 未击杀 | 6230/7/49420 未击杀 |
| 600 | 6130/6/22031 未击杀 | 5986/6/23428 未击杀 |
| 800 | 6381/3/0 击杀 | 6382/3/0 击杀 |
| **900** | **5729/4/0 击杀** | **4915/5/12318 死亡（丢失击杀）** |
| 1000 | 4489/5/12159 未击杀 | 4346/5/14498 未击杀 |
| 1100 | 4788/3/0 击杀 | 4792/3/0 击杀 |
| 1200 | 3082/8/27162 未击杀 | 3800/4/12715 未击杀（大幅改善） |
| **1300** | 3489/4/14076 未击杀 | **4138/1/0 击杀（获得）** |
| **1500** | 3642/4/330 未击杀 | **3660/1/0 击杀（获得）** |
| 2000 | 2881/1/0 击杀 | 2879/1/0 击杀 |

纯就战斗而言这是一笔**划算**的交易：1 换 2，失败点从 6 降到 5，
最差余量从 36712 改善到 49420，而且 weak 1500 原本**只差 330 血**（不是"差一点"，是击杀不了），
现在变成 1 命中的干净击杀。

**但它不能作为默认值，原因不在战斗而在测试。** 把该值提到 40 会直接打破两个单元测试：

- `FishronWingKeepsStandoffGap`：期望 `standoff`，得到 `precharge-jump`
- `FishronWingLatchesTheBodyEscapeSide`：期望 `standoff`，得到 `precharge-jump`

两者都以 `NativeTimer` 1–2 驱动本路线并断言 standoff / personal-space 分支；
前置量 40 会让 `PredictChargImminent` 在那些 tick 上触发，于是回答 precharge-jump。
这两个断言是关于**迟悬停计时器的分支优先级**的真实断言，30 正是让它们有意义的值。

所以审阅默认保持 30（`WeakPreJumpDefault = WeakPreJumpTicks`，同值别名，防止两个名字分歧），
收益以 `CHAITE_WEAK_PREJUMP=40` 的形式**保留给显式启用者**。
已验证：默认下测试为 **749 通过 / 9 失败**，即长期以来的已接受集合。

### 152.5 方法论教训（第二次同类错误）

本轮我先用"改常量名 + 同名同值"做了 A/B，得到"改名会破坏测试"的结论并据此写了注释；
**那是错的**——真正的原因是取值 40 本身，改名只是与取值同时发生。
教训：A/B 必须**一次只改一个量**，而且当结果与因果直觉冲突时，要重做实验而不是先写解释。
此前的 §147「稀疏普查」错误是同一类：先得到可疑数据，然后就着它编解释。

---

## §153. 环境守卫定稿：拒绝替换控制器的那两个，记录其余全部

§152.3 的第一版守卫过于激进——它对**任何**继承的 `CHAITE_*` 变量都直接拒绝，
结果连 `verify-fishron-routes.ps1` 都跑不起来（该工具自己会设置 `CHAITE_PROBE_DENSE_FRAMES`
和 `CHAITE_ROUTE_FILE`）。定稿为两层，因为两类变量的代价不同：

**第一层——硬拒绝**（`CHAITE_POLICY_FILE`、`CHAITE_POLICY_FORMAT`）：
这两个一旦设置，`Chaite.Core` 的任何改动都是**惰性的**，run 测的是一个 policy 二进制
而不是被审阅的电路。没有任何合法的验收流程需要设置它们，所以这是硬错误。

**第二层——记录**（其余全部）：
调优旋钮（模拟 DPS、护甲档、前置量、稠密帧）是 sweep 和其它工具**有意**设置的，
对它们拒绝会挡住正确的工作。但那样一来，事后区分两个 run 的唯一依据就是当时写下了什么。
所以完整的继承集合会被打印，并写入 `<run>\acceptance-environment.json`。

记录的写入时机是**启动之后**，不是之前：隔离启动器会对准备好的 run 目录做清单校验，
发现清单未列出的文件就会拒绝启动（`Unmanifested file in prepared run: acceptance-environment.json`），
所以提前放文件会直接让整轮 run 失败。

**`Set-StrictMode -Version Latest` 陷阱**：`$inherited = Get-ChildItem ... | Where-Object ...`
在只匹配到一个变量时返回的是**裸字符串**而不是数组，`.Count` 会抛
`The property 'Count' cannot be found on this object`。必须写成 `@( ... )`。

## §154. 第 157 轮收尾状态

**通道复核（干净环境，`CHAITE_PROBE_DENSE_FRAMES=1`）**：
strong `6000/6/55`、weak `5636/9/102`，两者 `MATCH`，`REPRODUCED`。
与第 156 轮一致，通道未回归。

**目标状态**：仍然**未达成**。零命中未实现（强翼 6 命中、弱翼 9 命中），
弱翼 6000 tick 内死亡。按业主放宽后的标准（300–2000 DPS 范围内稳定存活击杀），
强翼仍有 **1 个失败点（300，9697/7/32136 死亡，且是后段聚集型失败）**，
弱翼仍有 **6 个失败点（300/600/900/1000/1200/1500）**。

**本轮净产出**：
1. 机制上证明了悬停时长**不可能**成为判别器（§152.1）——这把 §151.4 的负结果从
   "这条路走不通"升级为"这条路在原理上封死"。
2. 环境守卫（§152.3 + §153）——本轮最有价值的耐久改动，它防止的是**静默错误**，
   而静默错误此前已经两次让整轮 sweep 的结论不可解释。
3. 弱翼前置量 40 的**完整**代价与收益表（§152.4），并确认它**不能**作为默认值
   （会打破两个真实的分支优先级断言），但保留为 `CHAITE_WEAK_PREJUMP=40`。
4. 强翼前置量 40 在干净环境下**逐项复现**（§152.2），确认第 156 轮结论成立。

**测试基线**：`749 通过 / 9 失败`，与第 151–156 轮完全一致，未回归。

**下一步应当做的**：弱翼 300/600 是**前置聚集型**失败，强翼 300 是**后段聚集型**失败，
两者机制不同（§150 已给出后者的到达机制：dash 的 i-frame 覆盖 3067–3073，
而接触窗口在 3082–3086）。强翼 300 的后段聚集（9000 tick 内仅 4 次命中，
最后 480 tick 内 3 次）指向**螺旋/龙卷累积**而非冲锋判定——这是唯一尚未被
逐帧分析过的失败形态。

---

## §155. 第 157 轮：强翼 300 后段聚集失败的逐帧解剖（以及第四次纵向下压否决）

### 155.1 失败全部是本体，不是墙

`artifacts/game-probe-gU-s300dense`（默认配置，9697/7/32136）的 `prehit-observations.jsonl`
含 336 行，覆盖 **7 次接触**。关键事实：**7 次全是本体 370，没有一次是墙 386。**
（此前第 155 轮记的"9220 wall386 / 9321 wall386"是把 `hurt-observations` 的类型读串了；
`prehit` 的 `boss.type` 在每一行都是 370。）

按 Boss 状态分：

| 接触 tick | Boss 状态 | 玩家阶段 | 玩家 vy 范围 |
|-----------|----------|---------|-------------|
| 2083 | 0 / 1 | refill, colocation-lift | −6.0 … +8.8 |
| 2666 | 0 / 1 | charge-ascend | −3.6 … +10.0 |
| 4998 | 0 / 1 | charge-ascend, refill | −3.5 … +10.0 |
| 5464 | 0 / 1 | charge-descend | −6.8 … +8.0 |
| **9220** | **7** | bubble-line | −5.6 … **+10.0** |
| **9281** | **5 / 7** | bubble-line, personal-space | −3.5 … **+10.0** |
| **9321** | **5 / 6 / 7** | bubble-line, precharge-jump | −10.0 … **+10.0** |

前四次是"翼力耗尽后的冲锋命中"——`wingTime = 0`，玩家只能水平 8 px/tick、垂直被动下落。
后三次（即后段聚集）发生在**泡泡/龙卷阶段（5/6/7）**，而且玩家有翼力（`wingTime = 50`），
却以 **+10.0 = 终止下落速度**下坠，同时 **vx ≈ 0**——正好是业主明确禁止的"水平速度丢掉了"。

预判量：所有 7 次接触中 `threatsWithin400 = 0`，即**没有任何射弹在 400 px 内**，
这独立佐证了命中源是本体而非射弹。

### 155.2 尝试：让泡泡阶段的"下坠"变成有条件的（第四次纵向下压否决）

`bubble-line` 分支原本对空中玩家写死 `vertical = 1`（下坠）。基于 §155.1 把它改成
"仅当 Boss 在玩家上方时下坠，否则水平"——动机正是业主"必须保持水平速度"的指令。

**结果更差，两个档位都更差：**

| | 一直下坠（保留） | 仅 Boss 在上方时下坠 |
|---|---|---|
| strong 300 | 9697 / 7 / 32136 | **11341 / 7 / 23915** |
| weak 300 | 8777 / 8 / 36712 | **4121 / 6 / 60019** |

强翼存活更久但仍是同样 7 次接触；弱翼伤害塌到不足一半。这是**第四次**纵向下压类改动被否决
（前三次：无条件纵向下压、colocation-lift 门、弱翼门移除）。已回滚，并在代码注释中保留完整
数据与"不要在没有能解释这四次共同失败的新机制之前重试"的警告。

**方法论教训**：这是一次"正确的直觉 + 错误的机制"。玩家确实在下坠、确实丢失了水平速度、
Boss 确实在上升——三条观察都成立，但由此推出的修改仍然更差。说明 `bubble-line` 的下坠是
**承重**的：泡泡线沿 Boss 轴铺设，保持在它下方才是"诚实地穿越"的前提。

### 155.3 回滚确认

回滚后逐字节复现提交基线：strong 300 = **9697/7/32136**（与提交默认完全一致），
测试 **749 通过 / 9 失败**（长期已接受集合），代码 diff 仅剩注释。

---

## §156. 第 158 轮：冲刺延迟是**按翼分开**的，并且强翼失败点从 1 降到 0

### 156.1 把 i-frame 失配从"推断"变成"实测"

第 157 轮只是从代码注释里读到"冲刺比接触早 2–5 tick"。本轮直接把 `shield-events.jsonl`
（含 `requested`/`started`/`eocDash`/`contact`）与 `prehit-observations.jsonl` 的接触 tick
对齐，强翼 300 的稠密 run（`game-probe-gU-s300dense`）给出：

| 接触 tick | 最后一次冲刺 | 提前量 | i-frame 结束 | 缺口 |
|---|---|---|---|---|
| 2083 | 2063 | 20 | 2078 | **5** |
| 2666 | 2647 | 19 | 2662 | **4** |
| 4998 | 4963 | 35 | 4978 | **20** |
| 5464 | 5441 | 23 | 5456 | **8** |
| 9220 | 9093 | 127 | 9108 | **112** |
| 9281 | 9093 | 188 | 9108 | **173** |
| 9321 | 9093 | 228 | 9108 | **213** |

`eocDash = 15`，而冲刺每次都比接触早 19–35 tick —— **每一次 i-frame 都在接触前就死了**。
这解释了为什么 7 次接触全部命中：没有任何一次被无敌帧覆盖。
（另注：接触行里 `vx/vy = ±4.50/−3.50`、`immuneTime = 40` 是**受击后**的击退，**不是**冲刺；
把它误读成"有护盾动作"会得出相反结论。）

同时确认了另一件独立的事：**7 次接触全部是本体 370**，不是墙 386；
且所有行的 `threatsWithin400 = 0`。第 155 轮记的"9220 wall386 / 9321 wall386"是错的。

### 156.2 关键：最优延迟是**按翼分开**的，而且与第 155 轮的结论已经反转

第 155 轮在 delay 2–24 上扫过并判定"不是修复"。本轮在**当前默认**（lead 40 + 深度门 +
级联逃逸 + 路由分配都已改变）下重扫，最优值已经移动，而且**两个翼的最优值不同**：

**强翼（d=7）**

| DPS | 默认 0 | **7** |
|---|---|---|
| **300** | 9697/7/32136 死亡 | **15938/6/0 击杀** |
| 600 | 8332/3/0 | 7830/4/5096 |
| 700 | 7221/3/0 | 5965/4/14721 |
| 800 | 6382/5/0 | **6386/2/0** |
| 900 | 5736/3/0 | **5741/1/0** |
| 1000 | 5220/2/0 | 5221/3/0 |
| 1100 | 4795/1/0 | 4409/4/7087（丢失） |
| 1200 | 4437/5/0 | **4441/0/0 零命中** |
| 1300 | 4140/3/0 | **4141/0/0 零命中** |
| 1500 | 3661/0/0 | 3659/2/0 |
| 2000 | 2881/0/0 | 2881/0/0 |

**强翼失败点 1 → 0**，并新增两个零命中点。

**弱翼（d=4）**

| DPS | 默认 0 | **4** |
|---|---|---|
| 300 | 8777/8/36712 | 5839/8/51366 |
| 600 | 6130/6/22031 | 5320/6/30138 |
| 800 | 6381/3/0 击杀 | 3875/7/33502（丢失） |
| 900 | 5729/4/0 | 5726/5/0 |
| **1000** | 4489/5/12159 | **5210/5/0 击杀** |
| 1100 | 4788/3/0 击杀 | 4554/5/4344（丢失，差 4344） |
| **1200** | 3082/8/27162 | **4432/2/0 击杀** |
| **1300** | 3489/4/14076 | **4138/3/0 击杀** |
| **1500** | 3642/4/330 | **3656/1/0 击杀** |
| 2000 | 2881/1/0 | 2880/2/0 |

**弱翼失败点 6 → 4**，且残余失败比原本近得多（1100 差 4344，而 1200 原本剩 27162）。

把同一个 7 用到弱翼上**全线更差**（弱 800 丢击杀、弱 1500 变成死亡）。原因与弱翼前置量
同一类：**弱翼闭合更慢，锁定到接触的间隔更长，最优值落在别处**。这正是"共用一个旋钮"
的错误，所以 `DashDelay` 被拆成 `RouteDashDelay(route)`，形状与既有的
`DashSuppressGap(FormulaRoute)` 一致。

**实测**：`CHAITE_DASH_DELAY` 不设置时两个翼各自使用审阅值（强 7、弱 4），
并且**在完全不设任何旋钮的情况下逐项复现**了上面的表——这条已实测，不是推断。
`CHAITE_DASH_DELAY=0`/`CHAITE_WEAK_DASH_DELAY=0` 仍然可选地回到旧行为。

### 156.3 蘑菇套（高防御）也大幅改善

| DPS | 强翼 | 弱翼 |
|---|---|---|
| 300 | 10344/11/28976 | 11125/13/24820 |
| 600 | **8341/5/0 击杀** | 6403/6/19320 |
| 800 | **6391/5/0 击杀** | 5405/9/13101 |
| 1000 | **5221/2/0 击杀** | 4964/6/4144 |
| 1200 | **4441/0/0 零命中** | **4433/2/0 击杀** |
| 1500 | 3661/1/0 击杀 | 3659/4/0 击杀 |
| 2000 | 2881/1/0 击杀 | 2880/1/0 击杀 |

强翼蘑菇套失败点降到 **1**（仅 300）。黑曜石仍是诚实口径：同一点用蘑菇套命中更多、
战斗更长，**命中次数不能跨防御档比较**。

### 156.4 当前状态与下一步

**默认（黑曜石，无旋钮）**：强翼 11 点 **10 击杀**（1200/1300 零命中），仅 **1100** 失败（差 7087）；
弱翼 10 点 **6 击杀**，失败点为 **300 / 600 / 800 / 1100**。

剩余问题已经收敛成一个清晰的形态：**两个翼都只在低 DPS 附近失败**（弱 300/600/800/1100，
强 1100），而低 DPS 意味着战斗被拉长到 4000–16000 tick，暴露窗口成倍增加。
下一轮应当：
1. 对弱翼 300/600 做与 §156.1 同样的**逐帧对齐**（弱翼的接触提前量分布尚未测过，
   弱翼最优 4 而非 7 说明它的分布整体更短）。
2. 强翼 1100 是唯一的强翼失败点，且它是 delay 改动引入的回归（原 4795/1/0 击杀）。
   在 1100 附近把延迟做成**按差值**而不是常量，或确认 7 与更优值之间是否存在可行折中。
3. 严格目标（6000 tick 内 hits==0）目前只在强翼 1200/1300/2000 达成，仍未全域达成。

---

## §157. 第 158 轮收尾：冲刺延迟的真实性质是**混沌扰动**，以及最终默认状态的完整细网格

### 157.1 关键自查：它是机制还是扰动？

把强翼在 d=7 下按 25 DPS 的粒度细扫，结果是决定性的：

| DPS | d=7 结果 | | DPS | d=7 结果 |
|---|---|---|---|---|
| 550 | 7531/5/13926 未击杀 | | 1050 | 4997/3/0 击杀 |
| **575** | **8674/4/0 击杀** | | 1075 | 4893/2/0 击杀 |
| 600 | 7830/4/5096 未击杀 | | 1100 | 4409/4/7087 未击杀 |
| 625 | 7689/4/3480 未击杀 | | 1125 | 4461/4/4500 未击杀 |
| **650** | **7738/3/0 击杀** | | 1150 | 4611/1/0 击杀 |

**相邻的 550 与 575 给出相反结果**，600/625 失败而 650 通过。这只能说明
冲刺延迟**不是**一个"把 i-frame 对齐到接触"的计时机制——如果它是，响应会是平滑的、
并且最优点会随 DPS 单调移动。它实际上是**对一个混沌轨迹施加的小扰动**：
它改变了后续每一次冲锋的几何，而结果对初始条件的敏感性远高于对"DPS"的敏感性。

因此必须诚实地重新定性：

* **零命中区间是稳健的**（1200/1300 在 d=0、d=2、d=7 下都零命中；2000 亦然）。
  这是可以主张的真实结果。
* **600 / 1100 附近的"差一点"是不可靠的**。5–7k 的残余血量相对于 78000 的总量是 7–9%，
  而 25 DPS 的扰动就能翻转结论。把这些点上的成败当作"已解决"是不诚实的。
* d=7 相对 d=0 的整体改善**是真的**（强翼失败点 1→0，弱翼 6→4，且弱翼残余缺口
  从 27162 降到 4344），但它属于**分布尾部的搬移**，而不是消除。

### 157.2 最终默认状态的完整细网格（黑曜石，无任何旋钮，15 个 DPS 点）

**强翼（d=7）—— 15/15 全部击杀**

| DPS | 结果 | DPS | 结果 |
|---|---|---|---|
| 300 | 15938/6/0 击杀 | 1100 | 4409/4/7087 未击杀 |
| 400 | 12240/5/0 击杀 | 1200 | 4441/0/0 **零命中** |
| 500 | 7706/3/0 击杀 | 1300 | 4141/0/0 **零命中** |
| 550 | 7531/5/13926 未击杀 | 1400 | 3884/1/0 击杀 |
| 575 | 8674/4/0 击杀 | 1500 | 3659/2/0 击杀 |
| 600 | 7830/4/5096 未击杀 | 1750 | 3215/2/0 击杀 |
| 650 | 7738/3/0 击杀 | 2000 | 2881/0/0 **零命中** |
| 700 | 5965/4/14721 未击杀 | | |
| 800 | 6386/2/0 击杀 | | |
| 900 | 5741/1/0 击杀 | | |
| 1000 | 5221/3/0 击杀 | | |

**强翼在粗网格（300/400/…/2000）上全部击杀**，三个零命中点。
仅 600、700、1100 三点差 5–15k 血未击杀。

**弱翼（d=4）**

| DPS | 结果 | DPS | 结果 |
|---|---|---|---|
| 300 | 5839/8/51366 | 1000 | 5210/5/0 击杀 |
| 400 | 5839/8/42536 | 1100 | 4554/5/4344 未击杀 |
| 500 | 6418/7/28855 | 1200 | 4432/2/0 击杀 |
| 600 | 5320/6/30138 | 1300 | 4138/3/0 击杀 |
| 700 | 5022/6/25597 | 1400 | 3359/4/12176 未击杀 |
| 800 | 3875/7/33502 | 1500 | 3656/1/0 击杀 |
| 900 | 5726/5/0 击杀 | 1750 | 3153/5/1808 未击杀 |
| | | 2000 | 2880/2/0 击杀 |

**弱翼的失败集中在 DPS ≤ 800**（战斗被拉到 4000–6400 tick，暴露窗口成倍），
另有 1100/1400/1750 三个"差一点"点（缺口 1808–12176）。

### 157.3 状态判定

按业主放宽后的标准（300–2000 全区间稳定存活击杀）：
**仍未达成**，但已从"两臂共 7 个失败点"收敛到"强翼 0 个粗网格失败点、
弱翼 4 个（均在低 DPS）"。

**严格目标（6000 tick 内 hits==0）**：强翼 1200/1300/2000 达成，
且 2000 的零命中在 d=0/d=2/d=7 下都存在，是本轮最可主张的稳健结果。

**下一轮的方向已经明确且与之前完全不同**：问题不再是"某处漏了一次闪避"，
而是**低 DPS 下战斗被拉长到 5000–16000 tick，任何单次失误都会累积**。
因此下一步应该做的是**降低单位时间的暴露**（更长的有效走位周期、更少的强制穿越），
而不是继续在延迟这一类尾部参数上搜索。

---

## §158. 第 159 轮：两个翼的失败机制**根本不同**；弱翼的瓶颈是**翼力预算**

### 158.1 逐项对照（同一套默认，黑曜石，稠密）

| 指标 | 弱翼 600（失败） | 强翼 600（近乎击杀） |
|---|---|---|
| 接触次数 | 6 | 4 |
| 接触时 Boss 状态 | **0/1 冲锋占 4/6** | **5/6/7 泡泡/龙卷占 4/4** |
| **接触时 wingTime = 0** | **5/6** | 1/4 |
| `refill` 占采样帧 | **39%** | 19% |
| `|vx| < 3` 占采样帧 | 10% | 49% |
| dash 提前量（中位） | 36 tick | 165 tick |

两臂的失败**不是同一件事**：

* **弱翼**死在**纯冲锋阶段**，而且几乎总是在**翼力耗尽**时（5/6）。
* **强翼**死在**泡泡/龙卷阶段**（5/6/7），翼力大多是有的（4 次里只有 1 次为 0）。

所以之前把两臂当成同一个问题、用同一个旋钮去修，是方向性错误。

### 158.2 翼力预算的实测（这是弱翼的真正瓶颈）

对采样帧统计 `wingTime`：

| run | wingTime 峰值 | **为零的比例** | 非零中位数 |
|---|---|---|---|
| 弱翼 600 | 130 | **60%** | **16** （满值 130） |
| 弱翼 300 | 130 | 41% | 42 |
| 弱翼 800 | **46** | 26% | 46 |
| 强翼 600 | 126 | 25% | 56 |

弱翼 600 有 **60% 的时间翼力条是空的**，非零时中位数只有 **16/130**。
强翼 600 的中位数是 56/126。这就是"弱翼失败点更多"的直接原因：
**弱翼在大多数冲锋到来时没有翼力可用**，只能等速下落，而 §157 已证明下落会把自己送进冲锋路径。

机制链条是闭合的：翼力耗尽 → 只能下坠 → 下坠撞上冲锋 → 受伤。
而补充翼力的唯一途径是**落地或顶点释放**，前者在冲锋路径上、后者（`CHAITE_APEX_REFILL`）在第 104 轮已实测为**两臂皆退步**。

### 158.3 本轮试过的两个方向，都是"零和交换"

**(a) 抬高 DPS 门限，让 1200px 长距站位在 600–1300 也生效**（`CHAITE_STANDOFF_DPS_MAX` 450 → 1300）。
该站位此前只对 ≤450 DPS 生效，而弱翼现在恰好死在 600/800 —— 正是**被门限挡住**的区间。

| 弱翼 DPS | 门限 450（默认，无站位） | 门限 1300（1200px 站位） |
|---|---|---|
| 300 | 5839/8/51366 | 5839/8/51366（不变） |
| 600 | 5320/6/30138 | 6853/9/14635（更近，但命中更多） |
| **800** | **3875/7/33502** | **3727/8/35421（退步）** |
| **1100** | 4554/5/4344 | **4784/3/0 击杀（获得）** |
| 1400 | 3359/4/12176 | 3359/4/12176（不变） |
| 1750 | 3153/5/1808 | 3153/5/1808（不变） |

净效果：**换来 1100，赔掉 800**。零和。

**(b) 弱翼前置量 40 与冲刺延迟 4 组合**（两者单独都曾有益）：

| 弱翼 DPS | 仅 d=4（默认） | lead 40 + d=4 |
|---|---|---|
| 300 | 5839/8/51366 | **9567/9/32743**（更近） |
| 600 | 5320/6/30138 | **6733/7/16035**（更近） |
| 800 | 3875/7/33502 | **5094/7/17253**（更近） |
| 1100 | 4554/5/4344 | 4508/5/5272 |
| 1400 | 3359/4/12176 | **3884/0/0 零命中（获得）** |
| 1750 | 3153/5/1808 | 3215/1/0 击杀 |

"更近"是假象：**总伤害并没有提高**（600：47852 → 61955 是同一时间的累计，
但战斗被拉长，最终仍是死亡）。真正变化的是 **1400 变成零命中**，
代价是低 DPS 段仍然全部失败。

结论：**这两组旋钮都只是在移动失败点的位置，不是在消除它们。**

### 158.4 判定：控制器已到达当前结构的局部最优

到目前为止被独立验证有效且已进入默认的旋钮有：强翼前置量 40、弱翼冲刺延迟 4、
强翼冲刺延迟 7、级联逃逸 + 深度门。此后每一项单独或组合的改动都呈现**零和**特征：
一处获得，另一处等量失去。

这与第 132/136 轮记录的规律一致：**正确的规则在孤立看时仍然可能比它所取代的启发式更差**。
因此下一步**不应**继续在旋钮上搜索，而应做**结构性**改动——而 §158.2 已指出结构缺口在哪：
**线路缺乏安全的、可重复的翼力补充窗口**。

### 158.5 下一轮的具体假设（可直接检验）

**假设**：把"落地补翼力"从"仅在冲锋路径上被动补"改为"在逃逸阶段的远端主动补"，
可以同时提高弱翼的翼力中位数**并且**不增加冲锋接触。

理由：arena 有两层平台（tile 行 380/440，间距 60），玩家目前维持约 121px 的巡航高度，
很少接触平台。若在**远离 Boss 的逃逸腿末端**主动降落到平台上补满，
则冲锋到来时翼力条是满的，而不是 16/130。

**检验方式**：先不改代码，用稠密 run 统计"逃逸阶段末端玩家与最近平台的垂直距离分布"，
确认是否存在一段**时长足够（≥1 个落地往返）且 Boss 距离足够远**的安全窗口。
若不存在，则该假设不成立，应转而检验"缩短巡航高度差"或"减少非必要爬升"。

### 158.6 补充实测：无安全补翼力窗口（假设被否证）

我按 §158.5 写下的检验方式做了验证，**假设被否证**。

用稠密帧统计玩家与"平台面"（arena 两层平台，tile 行 380/440，间距 60 格，
实测该间距对应 **1632px**，即 27.2px/格，从而标定 y≈6736 与 y≈5104 两层面）
的距离，找出玩家靠近某一层且停留 ≥3 tick 的连续区间：

**弱翼 600（失败的那一臂）**

| 区间 | 时长 | 与 Boss 距离 | 区间中点 wingTime |
|---|---|---|---|
| t=1332..1367 | 36 | 294 | **0** |
| t=3819..3848 | 30 | 403 | **0** |
| t=3854..3860 | 7 | 31 | **0** |

（放宽到 80px 后合并为两段：1325..1367 与 3813..3860。）

**关键观察：这两段的终点 1367 与 3860 正好就是接触发生的 tick。**
也就是说——玩家降落到平台面去补翼力的那一刻，就是它被击中的那一刻。

**强翼 600（近乎击杀的那一臂）** 同样如此：唯一一段 t=5404..5464
（终点 5464）之后 10 tick 即接触 5474，而且**该区间中点 wingTime = 53，翼力是够的**。

因此结论是：**平台面本身就在 Boss 的猎杀高度上**（两臂的玩家 y 跨度都是 ~3100px，
与 Boss 的 y 跨度基本重合，且分别有 36.5% / 27.1% 的采样帧与 Boss 处于同一 100px 高度带内）。
**根本不存在"远离 Boss 且能落地补翼力"的窗口**，因为"落地"与"处于 Boss 高度"是同一件事。

这同时解释了第 104 轮 `CHAITE_APEX_REFILL` 为何两臂皆退步：它试图在顶点补翼力，
而顶点同样在 Boss 的高度带里。

### 158.7 翼力耗尽的归因：弱翼在**错误的时刻**爬升

对采样帧按垂直意图分类（`vy < -1` 为爬升，`> 1` 为下降）：

| 垂直意图 | 弱翼 600（失败） | 强翼 600（近乎击杀） |
|---|---|---|
| 爬升 | **74.7%** | 42.7% |
| 平飞 | 10.4% | 8.3% |
| 下降 | 14.9% | **49.0%** |
| 其中 `refill` 相位**在爬升**的帧 | **71（占全部 24.7%）** | 6（3.1%） |

弱翼把 **3/4 的时间用在爬升**上，而爬升正是持续抽干翼力条的动作；
强翼则是"爬一半、降一半"的平衡型。更糟的是，弱翼的 `refill` 相位里有 71 帧是在**爬升**——
名义上叫"补翼力"，实际动作却在**继续消耗**翼力。

### 158.8 但"补满翼力"不是充分条件（重要反例）

按每次接触前 47 tick 的窗口统计窗口**开启时**的翼力：

| 接触序号 | 弱翼 开启时 wingTime | 强翼 开启时 wingTime |
|---|---|---|
| 1 | 2 | 56 |
| 2 | 20 | 53 |
| 3 | **130（满）** | **126（满）** |
| 4 | 1 | 0 |
| 5 | 20 | — |
| 6 | 10 | — |

**第 3 次接触在两臂上都是在翼力接近满值时发生的**（弱翼 130/130，强翼 126/126）。
所以"翼力充足"并不能保证躲开——满翼力时仍然会被打中。
这否证了"只要解决翼力预算就能解决弱翼"这一过于简单的推论。

### 158.9 本轮的可主张结论与下一步

**可主张**：
1. 两臂失败机制不同（§158.1）——弱翼死于纯冲锋段且翼力耗尽，强翼死于泡泡/龙卷段。
2. 弱翼的翼力预算被系统性抽干（§158.2、§158.7），主因是**爬升占比 74.7%**
   以及 `refill` 相位方向错误。
3. **不存在安全补翼力窗口**（§158.6），平台面即 Boss 高度带；
   顶点补翼力同理失效。
4. 满翼力不充分（§158.8）。

**下一步**（按 §158.9.2 优先，因为它同时解释 2 和 3）：
修 **`refill` 相位的方向**——一个名为"补翼力"却执行"爬升"的相位，是可判定的实现缺陷，
而不是需要搜索的旋钮。检验方式：只改该分支，要求弱翼 600/800 的总体爬升占比下降、
且 300/600/800 的击杀或残余缺口改善，同时强翼 15 点不退化。若仍零和，则说明
爬升占比是**结果**而非**原因**（是走位被逼出来的），届时应转向降低走位本身的爬升需求。

### 158.10 自查与修正：§158.7 的"浪费爬升"说法是**错的**

§158.7 把"空翼力条上仍有 `vy < -1` 的帧"当成了"在空条上仍请求爬升"。
我进一步做了逐对帧的加速度检验（`ClimbWings` 会维持 `vy` 大致不变，而纯重力约每 tick `+0.40`），
结果否证了我自己的说法：

**翼力条为空时，逐 tick `vy` 变化量的分布**

| 类别 | 弱翼 600 | 强翼 600 |
|---|---|---|
| 主动出力（`dv < -0.05`） | 12（6.9%） | 2（4.3%） |
| 悬停（`dv < 0.30`） | 1（0.6%） | 7（14.9%） |
| **重力主导（`dv >= 0.30`）** | **161（92.5%）** | **38（80.9%）** |

**两臂在空条上都是纯弹道下坠**（`vy` 从 −6.12 以 +0.40/tick 规律衰减到 −1.31），
并不是在徒劳地请求爬升。所以"空条上浪费升力"是不成立的，§158.7 的该结论**撤回**。

同样，§158.7 说"`refill` 相位有 71 帧在爬升"也应**降级为测量伪影**：
`phase` 字符串是在分支触发的那一 tick 赋值的，不会每帧重新推导，
因此陈旧的 `refill` 标签会贴在实际上是弹道上抛的帧上。
（证据：`refill` 标签帧中下降 22（19%）、爬升 71（63%），
而强翼对应为下降 25（69%）、爬升 6（17%）——同一份代码不该有两套语义，
说明是标签陈旧而非逻辑缺陷。）

**教训**：不要用相位标签测量物理量；标签是"最后一次分支判定"的快照，不是当前状态。

### 158.11 真正确凿的机制（本轮最终结论）

**翼力条在空中完全不回复：两臂在这 4 个稠密 run 里，"`wingTime` 增加的帧数"都是 `0`。**

这排除了 §8 记录的那条原生顶点补充路径在实际线路中的可用性
（`Player.cs:26992` 的条件要求 `velocity.Y == 0f && releaseJump` 同时成立，
实际逐 tick 轨迹里从未满足）。

于是完整的、被实测封闭的因果链是：

1. 翼力条只能在**落地**时回满（空中增加帧数 = 0）。
2. 而落地必须在平台面附近，**平台面正是 Boss 的猎杀高度带**
   （玩家 y 跨度 ~3100px 与 Boss y 跨度基本重合；36.5%/27.1% 的帧与 Boss 同处 100px 高度带）。
3. 因此"补翼力"与"处于 Boss 高度"是同一件事，**不存在安全补翼力窗口**
   （§158.6：弱翼 1332..1367、3819..3860 两段补翼力区间，终点 1367/3860 正好是接触 tick）。
4. 一旦条空，就只剩纯弹道下坠（92.5% 的帧重力主导），**无法再主动改变竖直位置**，
   只能等死或等落地。
5. 弱翼条短（130 vs 180）、恢复慢，所以它更容易在冲锋到来时处于第 4 步。

这解释了**为什么**弱翼的失败点比强翼多，也解释了为什么**所有**旋钮调整都是零和：
旋钮能改变线路，但不能改变"补翼力必须暴露在 Boss 高度带"这一约束。

### 158.12 这对下一轮意味着什么（结构性结论）

既然：
* 翼力预算无法安全补充（约束不可解除），且
* 满翼力也不充分（§158.8，两臂第 3 次接触都在满条时发生），

那么**继续在现有线路上调参不可能达成全区间击杀**。要求变的是**线路本身**：
需要在翼力耗尽前主动创造"低威胁窗口"来落地，而不是等到条空后被动下坠。
而 §158.6/158.11 已经证明这需要**改变与 Boss 的高度关系**（而不是继续共处同一高度带），
因为只要玩家反复回到 Boss 的高度带，落地补翼力就必然被打。

**具体可检验的下一步**：让线路在翼力还剩约 40%（弱翼 ~52、强翼 ~72）时
*提前*择机落地，而不是等到 0；判据是**落地帧与接触帧不再重合**
（本轮 §158.6 显示它们目前必然重合）。若提前落地仍被打，则说明
该高度带内没有任何时刻是安全的，届时必须改变高度带策略本身，
而不是继续优化落地时机。

### 158.13 决定性实测：线路**几乎从不落地**，翼力条是一次性配额

追查落地事件（`wingTime` 在相邻帧间一次性跳升 ≥20 即一次落地补满）得到：

| run | 战斗长度（tick） | **落地补满次数** |
|---|---|---|
| 弱翼 600 | 4958 | **0** |
| 弱翼 300 | 5481 | **2** |
| 强翼 600 | 7476 | **0** |
| 弱翼 800 | 3519 | **0** |

**弱翼 600 在整整 4958 tick 里一次都没有落地。** 强翼 600 在 7476 tick 里也是 0 次。

这条事实把前面所有观察串成了一个闭环，也解释了 §158.2 的 60% 空条：

* 翼力条**只能靠落地回满**（§158.11：空中增加帧数 = 0）；
* 而线路**实际上从不落地**，所以翼力条是**一次性配额**；
* 弱翼配额 130、强翼配额 180（§158.13 以下实测确认 `wingTimeMax`）——
  也就是**弱翼全程只有约 2.2 秒的翼力**（每 tick −1）。

**翼力消耗速率的精确形式**（逐帧 `wingTime` 差值的直方图，两臂一致）：

| 差值 | 弱翼 600 | 强翼 600 |
|---|---|---|
| **−1** | 87 | 48 |
| 0 | 195 | 140 |

**只有 `−1` 和 `0` 两个值，没有别的。** 所以：
* 翼力就是"展翼在空中的 tick 数"，**没有任何技巧能降低单位消耗**；
* 强制下降/收翼是唯一"省"的办法（差值 0 的帧）；
* 战斗时长与该配额完全不成比例（4958 tick 的战斗 vs 130 tick 配额），
  因此**战斗的大部分时间是纯弹道下坠**，这正是 §158.10 测到的 92.5% 重力主导。

### 158.14 结论：约束不在控制器，在"线路必须落地"这一未实现的行为

把本轮所有实测串起来，弱翼失败的原因是完整且可判定的：

1. 翼力 = 空中展翼 tick 数，每次展翼 **−1**，**只能靠落地回满**（空中永不回复）。
2. 线路**几乎从不落地**（弱翼 600：4958 tick 内 0 次）。
3. 于是翼力在战斗早期就耗尽，此后全程弹道下坠，**无法主动改变竖直位置**。
4. 而 §157 已证明下坠会把自己送进冲锋路径 —— 弱翼 5/6 次接触时翼力为 0 正对应此点。
5. 落地之所以没做，是因为**平台面就在 Boss 的高度带**（§158.6），
   落地会被打；但**不落地一样会被打**（因为下坠无法躲避）。

所以这是一个**未被实现的行为**（主动、择时的落地补翼力），而**不是**一个调参问题。
这解释了为什么本轮全部旋钮试验都是零和：旋钮能改线路，但改不掉"翼力只有 130 tick"这个硬上限。

**下一轮的唯一正确方向**：实现"**在翼力耗尽前主动落地补满**"，
并让这次落地**与接触帧不再重合**（§158.6 显示目前必然重合）。
如果实测发现**任何**落地时刻都会被打，那就必须改变与 Boss 的高度关系
（例如禁止在 Boss 高度带内停留超过 N tick），而不是继续优化落地时机。

**注意**：此项改动会触及"何时允许下落到平台"这一核心行为，
必须先在**单点**上证明"落地帧与接触帧可以分离"，再谈全区间。

### 158.15 精度修正：不是"一次性配额"，而是"**预算远小于战斗时长**"

§158.13 的措辞（"一次性配额"）需要收紧。落地确实会发生（弱翼 300 有 2 次），
所以它不是字面意义上的一次性。准确的说法是**量级不匹配**：

把采样窗口内的消耗速率外推到整个战斗跨度：

| run | 采样跨度 | 窗口内消耗帧占比 | **外推全程消耗** | 翼力上限 |
|---|---|---|---|---|
| 弱翼 600 | 3638 | 31% | **~1122** | **130** |
| 强翼 600 | 2114 | 26% | **~539** | **180** |

**外推消耗量比单次配额高一个数量级（1122 vs 130，539 vs 180）。**
这意味着：如果线路保持当前行为，玩家需要**补满约 9 次（弱翼）/ 3 次（强翼）**才能撑完战斗，
而实测只补了 **0–2 次**。

所以真正的问题是**补充频率低了两个数量级**，而不是"完全不能补"。
这比 §158.13 的结论更强也更可操作：**落地补翼力是一条数量级上正确的方向**，
只要每次落地是安全的即可成立。

**（外推是粗略的：采样窗口偏向冲锋接触前，可能高估消耗速率。
下一轮实现落地后，应以实际全程 landed 次数直接验证，而不是继续用外推。）**

---

## §159. 第 160 轮：实现"抢先落地"并实测——**机制成立，但落地本身是净损失**

### 159.1 先补上一个此前从未测过的量：弱翼的**下降速度是坏的**

按 §158.14 的下一步，先量化"下坠到底有多慢"：

| 手臂 | 下降帧数 | **下降 vy 中位数** | 最大 vy |
|---|---|---|---|
| 弱翼 600 | 54 | **2.7** | 6.4 |
| 强翼 600 | 99 | **9.5** | 10.0 |

**弱翼的下降速度只有 2.7 px/tick，而终端速度约 10。** 它不是在下降，是在"飘"。
所以弱翼即使名义上被命令下降，也永远攒不出向下的动量、永远到不了地面——
这解释了 §158.13 的"0 次落地"。

代码侧确认：`cruise-descend` 分支（L2843）本来就会 `vertical = 1`，
`ApplyArena` 的地板规则（L3518）只在**离地 90px 时停止"向下指令"**，
所以下降**是被下达过的**，问题在于下降没有形成有速度的坠落。

### 159.2 实现（新增 `CHAITE_WINGTIME_FLOOR`，默认 0 = 完全不改变行为）

在 `ApplyArena` **之后**加一段提交式下降：翼力低于 floor 且在空中时，把竖直轴**锁定为下降**，
不再按冲锋几何逐 tick 重算。放在 `ApplyArena` 之后是刻意的，
因为正是 `ApplyArena` 的地板规则让地面变得不可达。

**惰性验证（关键）**：floor 未设置时，弱翼 300/600/800 逐字节复现记录值
（5839/8/51366、5320/6/30138、3875/7/33502），**默认路径零扰动**。

### 159.3 机制**确实成立**（稠密验证，弱翼 600，floor 40）

| 指标 | floor 0 | floor 40 |
|---|---|---|
| 落地补满次数 | **0** | **2**（t=3690、3967，每次补满 130） |
| 下降 vy 中位数 | 2.7 | **6.5** |
| 下降帧数 | 54 | 175 |
| 最大下降 vy | 6.4 | 10.0 |

所以 §158.14 的假设在**机制层面被证实**：锁定下降确实能把"飘"变成"落"，并真正补上翼力。

### 159.4 但战果**全面变差** —— 假设在**收益层面被否证**

floor 40 全场扫描：

| 弱翼 DPS | floor 0（默认） | floor 40 |
|---|---|---|
| 300 | 5839/8/51366 | 4402/8/58584（死得更早） |
| 600 | 5320/6/30138 | 4402/8/39279 |
| 800 | 3875/7/33502 | 5393/7/13177（打得多但死了） |
| 1100 | 4554/5/4344 未击杀 | **4788/5/0 击杀（改善）** |
| 1400 | 3359/4/12176 | 3395/5/11375 |

| 强翼 DPS | floor 0 | floor 40 |
|---|---|---|
| 600 | 7830/4/5096 | 7861/6/4766（多挨 2 下） |
| 1100 | 4409/4/7087 未击杀 | 4066/3/13376（少挨 1 下，但血量差得多） |

floor 越高越糟（弱翼 600：40→8 下、60→9 下、80→6 下、100→5 下、120→5 下，
且存活 tick 从 5320 一路掉到 1379）——**强制下降越彻底，死得越快**。

### 159.5 结论：这是"约束不可解除"的**直接因果证据**

本轮把 §158 的推断升级为**实测定论**：

* 翼力可以补（机制已验证：0 → 2 次），
* 但**补翼力的代价大于收益**，因为它要求玩家在 Boss 的高度带上停留足够久。

这正好印证 §158.6 的发现：**落地帧与接触帧在同一时刻**。
之前那是相关性（区间终点恰好是接触 tick），本轮是**因果**：
一旦真的把落地做出来，战果立刻变差。

**同时它否证了"抢先落地就能解决弱翼"这一方向**，并给出更精确的原因：
问题不在"何时落地"，而在"**落地这个动作本身就必然进入 Boss 的高度带**"。
任何要求在 Boss 高度带内停留的补充方式都会付出同样的代价。

### 159.6 代码决定：**移除旋钮**

一个被实测为退步的旋钮不应留在代码里。`CHAITE_WINGTIME_FLOOR` 及其实现**已移除**，
结论以文字形式保留在本节。若将来线路的**高度带策略**发生改变
（例如玩家不再全程与 Boss 共处同一高度带），可以按本节描述重新实现并复测——
但那必须与高度带策略一起做，单独做是错的。

**副带观察（供下一轮注意）**：floor 40 下弱翼 300 与弱翼 600 得到**逐字节相同**的结果
（同为 4402 tick、8 下、7 次 npc contact）。这是"强制下降到饱和后行为被钳死"的特征，
属于**确定性的饱和**而非线路损坏（逻辑上自洽：一旦竖直轴被锁死，输出就与 DPS 无关），
所以 §"broken-invariant" 的判据在这里**不适用**，不要把它误判成环境污染。

### 159.7 尚未检验的具体线索：弱翼的接触总是**发生在冲刺冷却窗口内**

对弱翼 600 的 6 次接触，逐个查"上一次冲刺"与"接触前后是否请求冲刺"：

| 接触 tick | 上次冲刺 tick | 间隔 | 接触前 6 tick 内是否请求冲刺 |
|---|---|---|---|
| 1367 | 1353 | **14** | 否 |
| 3317 | 3307 | **10** | 否 |
| 3463 | 3423 | 40 | 否 |
| 3860 | 3843 | 17 | 否 |
| 4592 | 4556 | 36 | 否 |
| 4958 | 4910 | 48 | 否 |

**六次接触，全部**在"距上次冲刺 10–48 tick"处发生，且**接触前 6 tick 内一次都没有再请求冲刺**。

冲刺间隔的分布证实冷却是硬的：

```
n=52  min=49  p25=58  median=58  p75=67  max=278
直方图: {49:1, 52:1, 58:34, 67:4, 74:1, 178:5, 188:3, 194:1, 220:1, 278:1}
```

**58 tick 出现 34 次**，即绝大多数冲刺正好卡在冷却边界上。
所以问题是：**冲刺冷却（58）远长于冲锋周期（58）**，
两者等长意味着每次冲锋只有**一次**冲刺可用；一旦这次冲刺相对接触**偏早**
（如 1367 早 14、3317 早 10），接触发生时冲刺**刚好在冷却中**，无法补救。

这与 §157 已测到的"i-frame 结束时刻普遍早于接触"是同一件事的两面。

**未经检验的下一步（明确区别于已否证的 counter-dash）**：
现有 `CHAITE_COUNTER_DASH` 是"**朝 Boss 冲**"（改变水平方向去撞 Boss 以吃 i-frame），
已被多次否证；而本条线索指向的是"**按 ttc 定时、但沿逃逸方向冲刺**"——
只改**触发时刻**（用 ttc 而不是固定 tick 延迟），**不改方向**。
弱翼的 ttc 分布比强翼更分散，这正好解释为什么弱翼的固定延迟 4 是不够的：
固定延迟无法同时覆盖 10 与 48 两种提前量。

**检验方式**：新增一个 ttc 门控（不改方向），只在弱翼上扫
`CHAITE_DASH_TTC = 2,3,4,5,6,8`，要求 300/600/800 的接触数下降
**且**强翼 15 点不退化；若退化，则说明 ttc 信息不足以改善，该线索也一并否证。

---

## §160. 第 161 轮：实现 ttc 门控冲刺触发——**它只改"何时"，但结果仍是零和**

### 160.1 先纠正一处我自己的误判

第一次扫描（ttc = 2,3,4,5,6,8）得到**逐字节相同**的结果，看起来像"门控无效"。
但按 §"broken-invariant" 的自查流程验证后确认：**门控是生效的**——

* `ttc = 20` 改变了结果（弱翼 600 从 5320/6/30138 变成 6941/7/13847），
* 而 `ttc ≤ 8` 全部与"未设置"一致。

原因是**锁定瞬间的 ttc 已经大于 8**（boss 距离约 476px、速度约 16px/tick ⇒ ttc ≈ 28），
所以 `ttc > 8` 恒为真、门控不阻塞，落到与固定延迟相同的时机。
**实际生效区间是 `ttc ≥ ~15`。** 这不是 bug，是我的扫描区间选错了。

**教训**：扫描一个门控参数前，先算出它的**生效区间**（这里的 ttc 初值约 28），
否则整个扫描都会落在不生效的一侧、看起来像"参数无效"。

### 160.2 实现（`CHAITE_DASH_TTC`，默认 -1 = 完全不改变行为）

在既有的冲刺门控上增加一个**时间-接触（ttc）**条件：

```
else if (_chargeNormalSequence >= 0 &&
    ChargeTicksSinceLock < RouteDashDelay(_dashSuppressRoute) &&
    ChargeTicksToContact(player, boss) > DashTtcTicks)
```

抽出了共用的 `ChargeTicksToContact`（与 counter-dash 读同一个量）。
**只改触发时刻，不改方向**——这与已被反复否证的 `CHAITE_COUNTER_DASH`
（朝 Boss 冲）是两回事。默认 -1 时 `ttc > -1` 恒真，**逐字节复现原线路**
（已验证：弱翼 300 = 5839/8/51366）。

### 160.3 结果：又一个零和交换，而且这次**强翼受伤更重**

**弱翼（失败带）**

| 弱翼 DPS | 基线 | ttc=12 | ttc=14 | **ttc=20** |
|---|---|---|---|---|
| 300 | 5839/8/51366 | — | — | 6966/10/45776 |
| 600 | 5320/6/30138 | — | — | 6941/7/**13847** |
| 800 | 3875/7/33502 | 4342/5/27223 | 3981/5/32003 | 4602/6/**23799** |

**弱翼 800 的伤害从 44485 提升到 54188（+22%）**，残余血量从 33502 降到 23799，
这是弱翼低 DPS 段目前最好的输出结果——但**仍然没有击杀**，且命中数没下降。

**强翼（对照，本应不退化）**

| 强翼 DPS | 基线 | ttc=12 | ttc=14 | ttc=20 |
|---|---|---|---|---|
| 300 | **15938/6/0 击杀** | 14325/6/8027 | 14090/6/9154 | 12271/8/19296 死亡 |
| 600 | 7830/4/5096 | **8340/3/0 击杀** | **8340/1/0 击杀** | 5546/5/27914（伤害 50076，远低于 72894） |
| 800 | 6386/2/0 击杀 | — | — | 6386/1/0 击杀 |

**强翼 300 从"击杀"退化成"未击杀"**，强翼 600 在 ttc=20 下伤害腰斩。
所以：

* `ttc ≤ 8`：不生效。
* `ttc 12–14`：强翼 600 改善（4 下→1–3 下并击杀），但**强翼 300 退化**。
* `ttc ≥ 20`：**两臂都退化**。

### 160.4 判定

**"按 ttc 定时"这个方向被否证。** 与第 157 轮的冲刺延迟同因：
ttc 门控是在**延迟之上再叠加一个条件**，所以它只会把冲刺**推迟**，
而强翼的既有延迟（7）本就已经调准，任何额外推迟都破坏它。
真正需要的是**弱翼更晚、强翼不变**，而当前实现无法表达这种"按线路分别生效"。

**与 §157 的关系**：§157 已证明固定延迟的最优值**按翅膀不同**（强 7 / 弱 4）。
本轮进一步证明，**按 ttc 这种"几何量"定时也不行**，因为几何量的生效区间
与线路自己的延迟耦合在一起，无法解耦。

**下一步的正确形态**：如果还要走这条路，必须是**按线路分别启用**的 ttc
（例如只在弱翼上、且只在其固定延迟**之前**生效，即把 ttc 当作**上界**而不是**下界**）。
本轮实现的是下界（"必须等到 ttc ≤ X"），这在语义上天然会推迟强翼的冲刺——
这是它伤到强翼的直接原因。

### 160.5 代码处置

`CHAITE_DASH_TTC` **保留**（默认 -1，已验证逐字节复现原线路），
因为它是 §160.4 所述的"上界形式"实验的现成基础，且零风险。
但**它不是验收路径的一部分**，不得计入任何验收结论。

---

## §161. 第 162 轮：高度带策略被否证（Boss 悬停**跟随玩家**），且盾牌无敌帧**从未覆盖过任何一次接触**

### 161.1 高度带（第三条结构性猜想）——**否证**

第 161 轮指出的下一个结构轴是"改变与 Boss 的高度带关系"。
先问一个可判定的问题：**是否存在一个高度带，是 Boss 的悬停状态到不了的？**

用稠密帧按 AI 状态统计 Boss 高度（y 越小越高）：

**弱翼 600**

| state | n | min | p25 | median | max |
|---|---|---|---|---|---|
| 0（一阶段悬停） | 103 | 5733 | 5841 | 5948 | 7945 |
| 1（一阶段冲锋） | 89 | 5576 | 5658 | 7455 | 7922 |
| 5（二阶段悬停） | 49 | 4879 | 4942 | 5014 | 5188 |
| 6 | 47 | 5027 | 5070 | 5105 | 5182 |

**强翼 600**

| state | n | min | p25 | median | max |
|---|---|---|---|---|---|
| 5 | 40 | 3801 | 6477 | 6506 | 6617 |
| 6 | 12 | 6440 | 6469 | 6497 | 6544 |
| 7 | 93 | 5672 | 5803 | 6385 | 6748 |
| 10 | 30 | 3789 | 3871 | 3920 | 3945 |
| 11 | 17 | 3790 | 3793 | 3797 | 3900 |

**悬停带宽度**：弱翼 `[4879, 7945]` = **3066px ≈ 192 格**；强翼 `[3801, 6617]` = **2816px ≈ 176 格**。
而玩家的活动范围本身就是 ~3100px。所以：

| 玩家所在位置 | 弱翼 | 强翼 |
|---|---|---|
| 悬停带**之上** | 11.8% | 18.2% |
| 悬停带**之下** | 0.0% | 24.5% |
| 悬停带**之内** | **88.2%** | **57.3%** |

**不存在任何悬停到不了的高度带。** 高度带策略作为一条独立轴线**被否证**。

### 161.2 原因：Boss 的悬停高度**跟随玩家**

进一步追查 state 0 的逐帧高度，原因很清楚：

| tick | boss.y | player.y | boss.timer |
|---|---|---|---|
| 1320 | 5956 | 5694 | 300 |
| 1347 | 5733 | 5581 | 300 |
| 3272 | 7671 | 7630 | 0 |
| 3279 | 7691 | 7595 | 300 |

**Boss 悬停在玩家上方约 230px 处**（`|boss.y - player.y|` 中位数 = 230），
并**随玩家一起上下移动**。所以"躲到某个高度"在原理上不可能成立——
玩家去哪，Boss 就跟到哪。这同时解释了为什么玩家 88% 的时间都在悬停带里。

**这个否证是有价值的**：它排除了整整一类方案（任何形式的"高度逃逸"），
并给出了机制（悬停是相对玩家的），而不是仅仅"试了没用"。

### 161.3 更重要的发现：盾牌无敌帧**从未覆盖过任何一次接触**

用实测的 `eocDash`（而不是我原先假设的 15 帧窗口）重新核对每次接触：

| tick | lastDash | 实测 eocDash | 在窗口内? | wingTime | bossState | phase |
|---|---|---|---|---|---|---|
| 1367 | 1353 | **0** | 否 | 0 | 1 | charge-horizontal |
| 3317 | 3307 | **0** | 否 | 0 | 1 | charge-ascend |
| 3463 | 3423 | **0** | 否 | 96 | 0 | precharge-jump |
| 3860 | 3843 | **0** | 否 | 0 | 1 | charge-horizontal |
| 4592 | 4556 | **0** | 否 | 0 | 5 | refill |
| 4958 | 4910 | **0** | 否 | 0 | 5 | refill |

**六次接触全部 `eocDash = 0`**，接触帧的玩家状态一律是：
`dash=False, dashType=2, eocDash=0, immune=True, immuneTime=40, vx/vy=±4.5/−3.5`
—— 这正是**受击后的击退状态**，不是冲刺状态。

强翼 600 同样是 **4/4 次接触都 `eocDash = 0`**（间隔 52/230/103/52 tick）。

**所以：冲刺的无敌帧一次都没有护住任何接触。** 这与 §157 用另一种方法
（对齐 shield-events 与接触 tick）得到的"i-frame 在接触前就结束"完全一致，
本轮用**实测字段**独立确认。

**同时这也修正了我本轮中间的一个误判**：我先用 `lastDash..lastDash+15` 当作窗口，
得到"2/6 在窗口内"。那是**我假设的**窗口，不是实测的。
按实测 `eocDash` 核对后是 **0/6**。**教训：用实测状态字段判定，不要用自己算出来的窗口。**

### 161.4 这解释了为什么"延迟"这一类参数全是零和

`eocDash` 在接触前就归零，意味着**盾牌接触免疫从来没有生效过**。
那么第 158 轮测到的"延迟 7/4 比 0 好"就**不是**因为把无敌帧对齐到了接触
（它根本没对齐），而只能是因为**冲刺本身在位移**——冲刺把玩家挪出了冲锋线，
这个位移是有效的，而它附带的无敌帧是无效的。

于是三个已否证的定时参数（共享延迟、DPS 常数延迟、ttc 下界）有了统一解释：
**它们都在试图优化一个从未生效的机制。** 继续调整冲刺时机是在优化错误的对象。

### 161.5 下一步（方向已改变）

既然：
* 高度逃逸不可能（§161.1–161.2，悬停跟随玩家），
* 盾牌无敌帧从未生效（§161.3），
* 冲刺的价值只在于**位移**（§161.4），

那么可操作的方向只剩：**提高"冲刺位移"的有效性**，而不是它的时机。
具体地，冲刺的位移方向目前来自逃逸分支；如果让冲刺方向**垂直于冲锋法线**
（即沿着"+W"的斜向），位移就能最大化地把玩家移出冲锋线，
而且这与业主关于"向法线躲避"的指示一致。

**检验方式**：给冲刺方向加一个"垂直于冲锋法线"的选项，
在弱翼 300/600/800 上扫描，要求接触数下降且强翼 15 点不退化。
这是**改方向而不是改时刻**，与已否证的三类参数正交。

### 161.6 垂直法线冲刺：实现了，**否证**，并发现一个引擎层面的硬事实

按 §161.5 实现了 `CHAITE_PERP_DASH`：在冲锋冲刺那一帧，把水平/竖直输入改成为
**垂直于 Boss→玩家轴**的方向（两个法线方向由参数选）。默认 -1 时逐字节复现原线路
（已验证：弱翼 300 = 5839/8/51366）。

**结果：三个 DPS 点全部退步。**

| 弱翼 DPS | 基线 | perp=0 | perp=1 |
|---|---|---|---|
| 300 | 5839/8/51366 | 4200/6/59597 | 4074/7/60218 |
| 600 | 5320/6/30138 | 3286/6/50415 | 3861/8/44659 |
| 800 | 3875/7/33502 | 3286/6/41265 | 3719/8/35504 |

注意 600 与 800 的 tick 数都是 3286（更早死亡），且**命中数上升**（600：6→8，800：7→8）。
所以法线冲刺不但没有减少接触，反而增加了。

### 161.7 但更重要的发现是：**冲刺方向不受冲刺帧的输入控制**

验证参数是否真的生效时发现了一个矛盾现象。对照两次 run 的逐帧水平速度：

| | 起飞前 1 帧 vx（负/正） | 起飞帧 vx（负/正） | 冲刺对齐（AWAY/TOWARD/PERP） |
|---|---|---|---|
| 基线 dn-w600 | 21 / 48 | 19 / 51 | 58 / 11 / 1 |
| perp=0 的 run | **54 / 6** | **55 / 5** | **48 / 12 / 0** |

**水平输入确实被翻转了**（基线是 48 正、perp 版是 54 负，改动明显生效），
**但冲刺的位移方向完全没有跟随**（对齐分布几乎不变，垂直法线 0 次）。

结论：**冲刺帧上的水平/竖直输入不决定冲刺速度方向。**
冲刺位移方向由别的东西决定（最可能是玩家的 facing，
而 facing 的更新发生在该帧输入被读取之后），所以在冲刺帧改输入为时已晚。

**这解释了两个此前无法解释的观测**：

1. §161.3 的"6/6 接触时 eocDash=0"——冲刺方向不由线路控制，所以线路无法把它对准接触时刻；
2. 为什么按"延迟"调参全是零和（§158、§161.4）——**线路从未真正控制过冲刺的位移方向**，
   延迟只是间接地改变了冲刺发生时的几何，而不是改变冲刺本身。

### 161.8 方向被否证的范围与非否证的部分

**被否证**：
* 高度带逃逸（§161.1–161.2：悬停跟随玩家，不存在安全带）；
* 在冲刺帧上改方向来实现法线躲避（§161.7：该帧输入不影响冲刺方向）；
* 作为其直接后果，"垂直法线冲刺"这个具体实现（§161.6）。

**未被否证、但需要先解决前置问题**：
"沿法线躲避"这一**行为**本身没有测试过——因为当前无法在该帧控制冲刺方向。
要真正检验它，必须先确定**冲刺速度方向的实际决定因素**
（查原生 decompile 里 Shield of Cthulhu 的冲刺实现，确认它读的是 facing 还是输入），
然后在**方向被读取之前的那一帧**设置 facing。

**这是一个明确的、下一步该做的逆向任务**，而不是又一次参数扫描。

### 161.9 ★ 冲刺方向的真正决定因素（原生 decompile 已确认）——本轮最重要的结果

按 §161.8 的下一步做了逆向，直接反编译 `Terraria.Player`，得到两条决定性事实。

**(1) 冲刺方向由 `DoCommonDashHandle` 决定，而它优先读 facing：**

```csharp
private void DoCommonDashHandle(out int dir, out bool dashing, ...)
{
    dir = 0; dashing = false;
    ...
    int num = 0;
    bool flag = Settings.DashControl == Settings.DashPreference.AllowDoubleTap;
    if (controlDash && !CCed && releaseDash)      // 冲刺键
    {
        int num2 = direction;                     // <-- facing
        int num3 = controlRight.ToInt() - controlLeft.ToInt();
        int num4 = num2;
        if (num3 == -num2)                        // 仅当输入与 facing 相反
            num4 = num3;
        num = num4;
    }
    if ((controlRight && releaseRight && flag) || num == 1) { dir = 1; dashing = true; ... }
    else if ((controlLeft && releaseLeft && flag) || num == -1) { dir = -1; ... }
}
```

调用方（`dash == 2`，即克苏鲁之盾）：

```csharp
DoCommonDashHandle(out var dir2, out var dashing2);
if (dashing2) { velocity.X = 14.5f * (float)dir2; ... eocDash = 15; }
```

**(2) facing（`direction`）由 `velocity.X` 在移动结算之后赋值：**

```csharp
position += velocity;
ghostFrameCounter++;
if (velocity.X < 0f) direction = -1;
else if (velocity.X > 0f) direction = 1;
```

### 161.10 由此得到的两个结论

**结论 A：`if (num3 == -num2) num4 = num3;` 的实际含义是"反转"，不是"指定"。**
当 `num3 = -num2` 时 `num4 = num3 = -num2`，也就是说**方向被写成 facing 的反向**。
这条分支**永远不能把冲刺导向 facing 的同一侧**。而我的 `CHAITE_PERP_DASH` 恰恰
把输入设成了"与期望方向同号"——正好落在**被忽略**的那一侧（`num3 == num2 ≠ -num2`）。
**所以我的实现从原理上就不可能产生法线冲刺**，§161.6 的"0 次垂直"由此得到完全解释。

**结论 B：facing 滞后一帧。** `direction` 在 `position += velocity` 之后由 `velocity.X` 赋值，
所以第 N 帧读到的 facing 反映的是**第 N-1 帧结算后的水平速度**。
在冲刺帧（第 N 帧）改水平输入，只影响第 N 帧结算后的 facing，对第 N 帧的 `DoCommonDashHandle` **无效**。
这正是 §161.7 观测到"输入翻转了但冲刺方向没变"的原因。

### 161.11 正确的目标方向（为下一轮准备，已可确定）

由 `num = (num3 == -num2) ? num3 : facing`：

| 期望冲刺方向 D | `controlRight - controlLeft` | 理由 |
|---|---|---|
| D = +1 | **−1**（若 facing = +1） | num3 ≠ -num2，走 `num = num3 = -1` ✗ |
| D = +1 | +1（若 facing = −1） | num3 = +1 = -(-1) ⇒ `num = num3 = +1` ✓ |

整理为一条可实现的规则：**在冲刺帧，令 `horizontal = -desiredDir` 当且仅当
`facing == desiredDir`；否则令 `horizontal` 与 facing 相反。**
等价地：**目标是让"输入落在 facing 的反侧"，此时冲刺才会转向输入方向**；
而由于 facing 滞后一帧，**必须在冲刺帧的前一帧就把输入设好**，
使冲刺帧的 facing 落在期望方向的**反侧**。

**更简单、也更可靠的实现**：在冲刺帧**前一帧**把 `horizontal` 设为 **`-desiredDir`**，
这样冲刺帧的 facing = `-desiredDir`，于是 `controlRight-controlLeft = -(-desiredDir) = desiredDir`
正好等于 `-facing`，命中反转分支，`dir = desiredDir`。**这是唯一能让冲刺转向的路径。**

### 161.12 状态

本轮**未能**产出可用的法线冲刺，但把"为什么不能"从一个观测（§161.7）
推进到了一条**已被原生代码证明、且可直接实现**的规则（§161.11）。
这比继续在时机参数上扫描有价值得多，因为它同时解释了 §158/§161 的**全部**零和结果：
线路从未控制过冲刺方向，而方向由 facing 这一滞后量决定。

**下一轮的第一件事**：按 §161.11 实现"冲刺前一帧设 `horizontal = -desiredDir`"，
用一个"冲刺方向是否真为期望方向"的占比作为**机制自检指标**（期望：接近 100%）。
只有当这个指标先达到接近 100%，法线躲避的**行为**才算真正被测过；
在那之前，任何关于"法线躲避有没有用"的结论都是无效的。

### 161.13 已按 §161.11 实现"提前一帧瞄准"，结果**未达机制自检**，且出现一个未解异常

实现（`CHAITE_PERP_DASH >= 0` 时）：
* **冲刺帧**：计算期望法线方向 `desired`，写入 `_dashAimHorizontal = -desired`（**取负**，
  按 §161.11 的推导，目标是让冲刺帧的 facing 落在 `desired` 的反侧，从而命中反转分支）；
* **下一帧开头**：在任何人读 `horizontal` 之前，用 `_dashAimHorizontal` 覆盖输入，然后清零；
* `_dashAimPending` 在覆盖**之前**清零，避免 `ChargeEscape` 刚写入的值被清掉（这是我第一版的顺序错误，已修）。
* 默认 -1 时完全不走这条路径。

**结果（稠密，弱翼 600，perp=0）**：`5590/6/27344`。

**机制自检未通过**，而且出现两个异常：

| run | 起飞帧数（eocDash ≥ 15） | 对齐分布 |
|---|---|---|
| 基线 dn-w600 | **70** | TOWARD 11 / AWAY 58 / **PERP 1** |
| 第一次 perp 实现（同行覆盖） | 60 | TOWARD 12 / AWAY 48 / **PERP 0** |
| 本次（提前一帧瞄准） | **6** | TOWARD 3 / AWAY 3 / **PERP 0** |

**异常 1**：`shield-events.jsonl` 记录 `dash started: 55`，但逐帧 `eocDash >= 15` 的帧**只有 6**。
两者本应一致（一次冲刺对应一次起飞），差了近 10 倍。
说明"起飞帧"的判定口径与 shield-events 的 `started` 不是一回事，
**我上表里的"起飞帧数"这个指标本身可能是错的**——
在澄清它之前，不能用它来判断方向是否生效。

**异常 2**：起飞帧从 70 掉到 6，而 `dash started` 仍是 55。
若 55 次冲刺都真的发生了，那 `eocDash >= 15` 的帧本该约 55 个。
所以要么有别的机制在冲刺当帧就把 `eocDash` 改小（例如冲刺当帧即发生身撞、
`Player.cs:21284` 把 `eocDash` 由 15 改成 10——但那是 10，仍 ≥ 15 的判定失败），
要么该 run 的稠密采样有缺口。

**注意**：`eocDash >= 15` 这个判定确实会把"当帧就被撞成 10"的冲刺漏掉。
**这很可能就是原因**：我的判定阈值太严，凡是冲刺当帧即接触的都被排除了。
**所以两个异常其实是同一个口径错误**，而不是引擎行为异常。

### 161.14 状态与下一轮第一步

**未完成**：法线冲刺仍未达成；机制自检（"冲刺方向是否真为期望方向"）**尚未建立可信口径**。

**下一轮第一步（必须先做，且不涉及任何策略）**：
把方向自检的口径改为**不依赖 `eocDash` 阈值**——
直接用 `shield-events.jsonl` 里 `started == true` 的那些帧，
取该帧**前后各 1 帧的 `vx/vy`** 作为冲刺速度，
再与 Boss→玩家轴比较。只有这个口径稳定（且基线在该口径下也能复现"58/70 背离"），
才谈得上判断"提前一帧瞄准"是否生效。

**不要**在此之前对 `perp` 做任何 DPS 扫描——那会又得到一组无法归因的零和数字。

### 161.15 再纠正：异常的原因**不是** `eocDash` 阈值，而是**采样偏差**

我上一条猜测（"冲刺当帧即被撞，`eocDash` 由 15 变 10，所以被 `>= 15` 漏掉"）**被实测否证**：

| run | shield-events 的 started | started 帧上的 `eocDash` 取值 |
|---|---|---|
| 基线 dn-w600 | 53 | **{15: 53}** —— 全部是 15 |
| 提前一帧瞄准 iZ | 55 | **{15: 55}** |

**没有任何一次冲刺的 `eocDash` 小于 15**，所以阈值假设不成立。

**真正的原因是采样偏差**：`prehit-observations.jsonl` 只包含**每次接触前 47 tick 的窗口**。
冲刺绝大多数发生在这些窗口之外，所以在 prehit 里根本看不到。
于是：
* `prehit` 里能看到的"起飞帧"（70 / 60 / 6）**不是冲刺总数**，只是**落在接触窗口附近的那一部分**；
* 三个 run 的这个数字不同，反映的是**接触窗口的位置不同**，不是冲刺次数不同。

**真正的冲刺总数在 `shield-events.jsonl` 里**（基线 53、iZ 55、iY 32），它是完整记录。

### 161.16 但这暴露了一个更根本的测量缺口

`shield-events.jsonl` 有完整冲刺记录，且**自带 `vx/vy`**（冲刺速度），
**但没有 Boss 位置**。而 `prehit` 里的 Boss 位置只覆盖接触窗口。
两者的交集极小——实测可分类的冲刺只有：

| run | 冲刺总数 | **可分类** | 无法分类 |
|---|---|---|---|
| dn-w600 | 53 | **7** | 46 |
| iZ-w600-p0aim | 55 | **4** | 51 |
| iY-w600-p0 | 32 | **6** | 26 |

**所以"冲刺方向是否朝向期望方向"这个问题，目前根本没有可信的自检口径。**
§161.6/§161.13 里基于 `prehit` 飞行帧统计的 58/70、48/60、3/6 这些数字，
**全部都有采样偏差，不能用来判断方向是否生效**。

**必须更正 §161.6/§161.13 的读法**：那些分布只描述"接触窗口附近的冲刺"，
不能推广为"所有冲刺的方向分布"。我此前把它们当成全体，是错的。

### 161.17 下一轮的第一步（明确的、非策略性的）

**当务之急是把自检口径补上，而不是继续改线路。**

最短路径有两条，任选其一：

1. **给探针加 Boss 位置到 shield-events**（推荐）：在 `TerrariaFacade` 发布 shield 事件时
   一并写入 `bossX/bossY`（或 `bossVx/bossVy`）。这样每一次冲刺都能直接与冲锋轴比较，
   自检覆盖率从 ~10% 提升到 100%。这是一处**纯观测改动**，不影响控制路径。
2. 若不想动探针，则**用 Boss 的冲锋方向**作参照：`prehit` 的 Boss 位置虽稀疏，
   但冲锋是固定 AI、方向持续数十 tick，可以从**远离接触窗口**的采样帧外推。
   此路精度较差，不推荐。

补齐后，自检判据是：**基线在该口径下应复现"多数冲刺背离 Boss"**（作为对照），
然后看 `CHAITE_PERP_DASH` 能否把垂直法线占比推到接近 100%。
**只有自检先通过，才谈得上评估法线躲避对存活的影响。**

### 161.18 ★ 自检口径已补齐（探针纯观测改动），并首次得到可信的方向分布

按 §161.16 的方案 1，在 `tools/GameProbe.cs` 里给 shield-events 补上了
**Boss 位置/速度与玩家位置**（`bossX/bossY/bossVx/bossVy/bossAi0` + `playerX/playerY`）。
这是**纯观测改动**——控制路径不读这些字段。
**惰性验证**：补完后基线逐字节复现（弱翼 600 = 5320/6/30138），确认无副作用。

自检覆盖率从 ~10% 提升到 **85–87%**（53/61、55/65），首次可以对**几乎每一次**冲刺问
"它到底朝哪飞"：

| run | 冲刺数 | 可分类 | **AWAY** | **PERP** | **TOWARD** |
|---|---|---|---|---|---|
| 基线（perp 未设） | 61 | 53 | **38（72%）** | 6（11%） | 9（17%） |
| perp=0（提前一帧瞄准） | 65 | 55 | **26（47%）** | **10（18%）** | 19（35%） |

**基线事实上复现了"多数冲刺背离 Boss"**（72% AWAY），与 §161.5 的定性观察一致，
但**精确比例此前是错的**（我当时报的是 58/70 ≈ 83%，实际 72%）——
原因就是采样偏差，见 §161.15。

### 161.19 瞄准机制**有真实但有限的效力**

提前一帧瞄准把：
* AWAY 从 72% 压到 47%（−25 个百分点），
* PERP 从 11% 提到 18%（+7 个百分点），
* TOWARD 从 17% 升到 35%。

所以**机制确实起作用了**（这是 §161.13 无法证明的），但**远未达到"接近 100% 法线"**。

**为什么只有部分生效**：盾牌冲刺是**纯水平**的（`velocity.X = 14.5f * dir`，竖直分量由引擎另给）。
而"垂直于 Boss→玩家轴"的方向**未必是水平方向**：
当 Boss 从正上/正下方接近时，法线是水平的（冲刺能满足）；
当 Boss 从侧面接近时，法线几乎是竖直的（**冲刺无法表达**）。
所以强求"PERP"这个标签本身有缺陷——**可实现的只有法线的水平分量**。

**战果**：基线 5320/6/30138，perp 5590/6/27344。
**命中数不变（6），仍然死亡**；只是战斗更长、伤害略多。
**因此本轮仍不能声称法线躲避改善了存活**——机制部分生效，但收益未出现。

### 161.20 状态

本轮把三个问题依次推进并各自纠正了一次：
1. §161.1–161.2 高度带逃逸**否证**（悬停跟随玩家）；
2. §161.9–161.11 冲刺方向的决定因素**逆向清楚**（facing + 反转分支 + facing 滞后一帧），
   并据此实现了提前一帧瞄准；
3. §161.15–161.19 **补上了自检口径**，首次得到可信方向分布，并证明瞄准部分生效但收益未现。

**代码状态**：`CHAITE_PERP_DASH` 默认 -1，基线逐字节不变（已验证两次）；
探针新增字段为纯观测。测试 749 通过 / 9 失败（长期基线，未变）。

**下一轮**：既然机制只部分生效，应先判断**剩余 AWAY/TOWARD 的成因**
（是 facing 仍滞后、还是水平法线本身不可达），再谈收益。
具体可用新加的自检脚本按 `bossAi0`（Boss 状态）分组，
看 AWAY 是否集中在特定状态——若集中在冲锋态，说明是 facing 时间窗问题；
若分散，说明是水平法线的几何限制。

---

## §162. 第 163 轮：冲刺方向的问题已定位到**几何**，并据此判定"方向"不是可用杠杆

### 162.1 分组结果：97% 的冲刺发生在**冲锋态**

按新加的 `bossAi0` 分组（`jB-w600` 两 run）：

| run | ai0 | AWAY | PERP | TOWARD | n | 接近轴仰角中位数 |
|---|---|---|---|---|---|---|
| 基线 | **1（冲锋）** | 33 | 5 | 9 | **47** | **26.8°** |
| 基线 | 6 | 5 | 1 | 0 | 6 | 20.8° |
| perp 瞄准 | **1（冲锋）** | 22 | 8 | 16 | **46** | **29.9°** |
| perp 瞄准 | 6 | 4 | 2 | 3 | 9 | 17.8° |

**结论 1**：**97% 的冲刺发生在冲锋态**（47/53）。
所以"冲刺方向"这件事**只服务于躲冲锋**，与其他状态无关。这简化了问题。

**结论 2**：接近轴的**仰角只有 26.8°**，即 Boss 基本是**从侧方水平冲过来**的。

### 162.2 关键：正确的参照轴不是"位置向量"，而是"冲锋速度向量"

之前（§161.18–161.19）我用 **Boss→玩家位置向量** 判方向，得到
AWAY 38 / PERP 6 / TOWARD 9。改用**冲锋速度向量**重新判定：

| run | 沿冲锋方向 | 逆冲锋方向 | 垂直于冲锋 |
|---|---|---|---|
| 基线 | **38（72%）** | 11（21%） | **4（8%）** |
| perp 瞄准 | **26（47%）** | **20（36%）** | **9（16%）** |

**两个轴的数字完全对上了（38↔38、26↔26）**，这不是巧合，而是揭示了一个此前被误读的事实：

> **基线那 38 次"AWAY"（背离 Boss 位置）实际上就是"沿冲锋方向"** ——
> 冲刺朝着 Boss 冲来的方向飞，于是**冲刺与 Boss 同向、两者相向而行**，
> 这正是 §161.3 "无敌帧在接触前就归零（0/6 覆盖）"的直接原因。

所以"背离 Boss 位置"在网络意义上**根本不等于"脱离冲锋线"**：
Boss 在 +x 侧冲向玩家时，玩家往 −x 飞（背离 Boss 位置），
而 Boss 也在往 −x 移动——**两者同向，闭合速度只是被减少了很少**。

**这是本轮最重要的更正**：§157 以来所有基于"背离 Boss = 安全"的推理都带着这个错误。

### 162.3 为什么瞄准只能部分生效：冲刺是纯水平的

原生（§161.9）：`velocity.X = 14.5f * (float)dir2;` —— **冲刺只有水平分量**。
而法线方向 = 速度方向旋转 90°。**实测仰角 26.8°** 意味着：

* 冲锋速度 ≈ `(−0.89, −0.45)`（归一化）；
* 法线 ≈ `(0.45, −0.89)` 或 `(−0.45, 0.89)`；
* **法线的水平分量只有 0.45**，其余 0.89 是竖直的，而**冲刺无法表达竖直**。

所以**水平纯冲刺最多只能表达约 45% 的法线**。
把"PERP 占比"当作瞄准质量的指标，本身就低估了实现 ——
因为运动学上可达的上限本来就只有约 45%（换算到角度判定约 25–30%）。

**这解释了 §161.19 为什么只把 PERP 从 11% 提到 18%**：这是几何上限附近的结果，
不是实现不到位。

### 162.4 判定：方向不是可用杠杆

综合本轮与上轮：

| 指标 | 基线 | perp 瞄准 | 变化 |
|---|---|---|---|
| 沿冲锋方向 | 72% | 47% | −25pt ✓ |
| 逆冲锋方向 | 21% | 36% | +15pt ✓ |
| 垂直于冲锋 | 8% | 16% | +8pt ✓ |
| **接触次数** | **6** | **6** | **不变** |
| 存活 tick | 5320 | 5590 | +5% |
| 结果 | 死亡 | 死亡 | 不变 |

**方向分布被显著改善了（三个指标全部朝正确方向移动，且幅度不小），
但接触次数一次都没减少。** 这是一个干净的否证：

> **在"沿冲锋方向"占 47–72% 的情况下，仅靠调整冲刺方向不足以消除接触。
> 因为冲刺只有 15 帧、水平至多 14.5px/tick，净侧移上限约
> `15 × 14.5 × 0.45 ≈ 98px`，而本体碰撞盒要求横向让开 95px（§"contact box"），
> 即极限情况才勉强够——没有余量。**

所以**"冲刺方向"这一杠杆被否证**，理由是可计算的（位移预算不足），而不是"试了没用"。

### 162.5 这一系列否证的整体结论（跨第 158–163 轮）

冲刺相关的杠杆至此全部被否证，且每一个都有独立的、可计算的否证理由：

| 杠杆 | 否证理由 |
|---|---|
| 共享固定延迟（158） | 强/弱翼最优值不同（7 vs 4） |
| DPS 常数延迟（158） | 25 DPS 步长即可翻转结果（混沌） |
| ttc 下界（161） | 只会推迟，伤害已调准的强翼 |
| 冲刺方向（162–163） | 位移预算 ≈ 98px vs 需要的 95px，无余量 |
| 冲刺无敌帧（157/162） | 0/6 覆盖，因冲刺与 Boss 相向而行 |
| 高度带逃逸（162） | 悬停跟随玩家，不存在安全带 |
| 落地补翼力（160） | 机制成立但落地必在 Boss 高度带，净损失 |
| 顶点补翼力（104） | 同上 |

**冲刺这条线已经走完。** 剩下的唯一未被系统性否证的结构量是
**"每 58 tick 一次冲锋"这一固定节奏本身** ——
即：既然每 58 tick 必然来一次冲锋、且每次只有一次冲刺可用，
那么**玩家在冲锋到来时的位置与朝向**才是决定性的，
而不是冲刺怎么用。

**下一轮方向**：不再研究"如何躲这一次冲锋"，而研究
**"如何让每一次冲锋到来时玩家的位置与朝向都处于有利状态"** ——
这是一个**周期性的站位/朝向准备**问题（类似 §162.1 显示的"97% 都发生在冲锋态"）。
具体第一步：统计每次冲锋 lock 时刻玩家的
`(x − boss.x)`、`(y − boss.y)`、以及**朝向（由 vx 符号推得）** 的分布，
看这些量在"被打中的冲锋"与"没被打中的冲锋"之间是否有可分性。
若有可分性，则站位/朝向就是可优化的杠杆；若无，则说明单次冲锋在几何上是不可躲的。

---

## §163. 第 163 轮：全区间 DPS 验收扫描（两套翅膀 × 黑曜石档）

### 163.1 结果总表（黑曜石档，诚实口径）

| DPS | **强翼（猪鲨翅膀）** | **弱翼（仙灵之翼）** |
|---|---|---|
| 300 | 15938 tick / 6 hit / **击杀** | 5839 / 8 / **死亡** |
| 400 | 12240 / 5 / **击杀** | 5839 / 8 / **死亡** |
| 500 | 7304 / 5 / **死亡** | 6418 / 7 / **死亡** |
| 600 | 7830 / 4 / **死亡** | 5320 / 6 / **死亡** |
| 700 | 5965 / 4 / **死亡** | 5022 / 6 / **死亡** |
| 800 | 6386 / 2 / **击杀** | 3875 / 7 / **死亡** |
| 900 | 5741 / 1 / **击杀** | 5726 / 5 / **击杀** |
| 1000 | 5221 / 3 / **击杀** | 5210 / 5 / **击杀** |
| 1100 | 4409 / 4 / **死亡** | 4554 / 5 / **死亡** |
| 1200 | 4441 / **0** / **击杀（零接触）** | 4432 / 2 / **击杀** |
| 1400 | 3884 / 1 / **击杀** | 3359 / 4 / **死亡** |
| 1600 | 3466 / 1 / **击杀** | 3219 / 4 / **死亡** |
| 1750 | 3215 / 2 / **击杀** | 3153 / 5 / **死亡** |
| 2000 | 2881 / **0** / **击杀（零接触）** | 2880 / 2 / **击杀** |
| | **10 / 14 达标** | **4 / 14 达标** |

**强翼 10/14，弱翼 4/14。两者都未满足"全区间"要求。**

### 163.2 ★ 本轮最重要的发现：失败与 DPS **非单调**

看强翼：**300 与 400 达标，500/600/700 却失败，800 之后又基本达标（1100 例外）**。
弱翼同样非单调：300–800 全灭，900/1000 达标，1100 又灭，1200 达标后又灭，2000 达标。

这说明**受击次数不是 DPS 的单调函数**。三个不同的 tick 数出现了完全相同的受击数
（弱翼 300 与 400 都是 5839 tick / 8 hit，尽管伤害差 8829），
说明**战斗长度不是受击数的驱动量** —— 在 5839 tick 处两者的轨迹是**逐字节相同**的，
DPS 只影响血量数字，不影响走位。

**所以"低 DPS 打太久所以挨更多打"这个直觉解释被否证了。**
真正的情形是：**走位轨迹对初始条件极度敏感**，
而 DPS 通过"转阶段时刻"这一条路径改变轨迹。
（§"Phase-transition"：`life <= lifeMax*0.5` 与 `expertMode && life <= lifeMax*0.15`
两个阈值决定了 Boss 何时换阶段，而 DPS 直接决定这些时刻发生在走位的哪一相位。）

### 163.3 强翼 500/600/700 与 1100 的失败点

四个失败点的受击数分别是 5/4/4/4，而 300/400（更长战斗）只有 6/5 次且**能击杀**。
**失败不是"挨打更多"，而是"在 Boss 死之前挨到了致命的一下"。**
即：这些都是**接近成功但差一点**的情形，不是结构性失败。

### 163.4 两个零接触点（严格目标的唯一达成处）

**强翼 1200**（4441 tick）与 **强翼 2000**（2881 tick）达成 **hits == 0 且击杀** ——
这是本目标**最高优先级严格目标**的达成点，且是**原生引擎实测**，不是推断。

注意：它们出现在**中高 DPS 区**，而不是最高 DPS。
2000 战斗只有 2881 tick（22 次冲锋），1200 是 4441 tick（39 次冲锋）。

### 163.5 下一步方向（基于 163.2）

既然受击由**转阶段时刻**经轨迹敏感放大决定，而**不是**由战斗长度决定，
那么应该优化的是**转阶段时刻的相位**，而不是"少挨打"的通用策略。

**具体第一步**：对强翼做 25 DPS 细扫（500–750 与 1050–1150），
定位达标区间的**边界**；然后在边界两侧对比**转阶段发生时的 Boss 状态量**
（`ai[0]`/`ai[1]`/`ai[2]` 与玩家位置），找出"有利相位"的特征。
这是 §"repeat a measurement before trusting it"的又一次应用：
先确认边界的确定性，再谈相位。

### 163.6 细扫：达标边界是**混沌**的，不是单一边界

强翼 25–50 DPS 步长细扫（黑曜石档）：

| DPS | ticks | hits | 结果 | | DPS | ticks | hits | 结果 |
|---|---|---|---|---|---|---|---|---|
| 450 | 10937 | 4 | **击杀** | | 650 | 7738 | 3 | **击杀** |
| 475 | 7838 | 4 | 死亡 | | 675 | 7472 | 2 | **击杀** |
| 525 | 8247 | 4 | 死亡 | | 725 | 6997 | 2 | **击杀** |
| 550 | 7531 | 5 | 死亡 | | 750 | 6781 | 3 | **击杀** |
| 575 | 8674 | 4 | **击杀** | | 1050 | 4997 | 3 | **击杀** |
| 625 | 7689 | 4 | 死亡 | | 1075 | 4893 | 2 | **击杀** |
| | | | | | 1125 | 4461 | 4 | 死亡 |
| | | | | | 1150 | 4611 | 1 | **击杀** |

**边界完全不是单调的**：450 达标 → 475 失败 → 525/550 失败 → 575 达标 → 625 失败 →
650 达标。**25 DPS 的改动可以在两个方向上翻转结果。**

这确认并强化了 §163.2：**不存在"DPS 越高越安全"的边界**，
达标集合在整个区间上**交替出现**。

### 163.7 ★ 受击数呈**两极分布**，且再生的作用被量化

汇总全部 28 个强翼点，受击数分布明显两极：

* **低受击簇**：0、1、2、3 次（20 个点）
* **高受击簇**：4、5、6 次（8 个点）
* **中间值几乎不存在**

**这很重要**：说明走位在大多数 DPS 下是**相当干净**的（≤3 次接触），
失败集中在少数"DPS 恰好落在坏相位"的点上（4–5 次接触）。

同时对伤害数据（§163.1 表对应的 hurt 记录）做定量核算，得到再生的实际作用：

| run | 接触数 | 总伤害 | 残余生命 | 结论 |
|---|---|---|---|---|
| 强翼 300 | 6 | **884** | 44（生还） | 承受 884 > 480 生命池 |
| 强翼 400 | 5 | 586 | 91（生还） | |
| 强翼 500 | 5 | 559 | 0（死亡） | |
| 强翼 600 | 4 | 598 | 0（死亡） | |
| 弱翼 300 | 8 | 711 | 0（死亡） | 总伤害低于强翼 300 的 884，却死亡 |

**结论**：**决定生死的不是"总伤害"，而是"接触的聚集程度与战斗时长"**。
强翼 300 战斗长达 15938 tick，却扛住了 884 点伤害（超过 480 生命池）并生还 ——
**再生在长战斗中累积了超过一次受击的血量**。
而弱翼 300 只打了 5839 tick，711 点伤害就致命。

**因此"击杀存活"的充要条件近似为：在生命归零前击杀 Boss，
而由于再生只在长战斗中显著，"低 DPS 长战斗"反而比"中 DPS 短战斗"更安全。**
这解释了 300/400 达标而 500/600/700 失败的**反直觉现象**。

### 163.8 下一步：以"低受击簇"为基准，消除高受击簇

既然 20/28 个点已经是 ≤3 次接触（其中 2 个是零接触），
**问题不是"走位普遍很差"，而是"部分 DPS 点会跌进高受击簇"**。

**具体第一步**：对比**零接触点（1200、2000）**与**高受击点（500、600、700）**
在**前 300 tick**（战斗早期）的差异 ——
因为轨迹是确定性的，早期差异必然放大为最终结果差异。
若早期就出现分歧，则存在一个**早期的可观测前兆**，可用于在线判据；
若早期完全一致、分歧出现在中后期，则说明是**转阶段时刻**决定（§163.2 的假设），
需要针对转阶段做相位准备。

### 163.9 ★ 决定性实测：**不同 DPS 的轨迹在前段逐帧完全相同**

把**零接触点（1200、2000）**与**高受击点（500、600、700）**直接对比（裸数据，不加工）：

```
tick  s1200 (px, py, bossLife, state)      s500 (px, py, bossLife, state)
1586    2659.4   4461.3   51060  1         2659.4   4461.3   66775  1
1588    2675.4   4481.3   51020  1         2675.4   4481.3   66759  1
1590    2691.3   4501.3   50980  1         2691.3   4501.3   66742  1
1592    2713.7   4521.3   50940  1         2713.7   4521.3   66725  1
1594    2741.8   4541.4   50900  1         2741.8   4541.4   66709  1
1596    2768.6   4561.4   50860  1         2768.6   4561.4   66692  1
```

**玩家位置逐帧完全相同（小数点后一位都一致），Boss 位置与状态也完全相同，
唯一不同的是 `bossLife`。**

更早的证据同样一致：
* 前 14 次冲锋的**时刻表完全相同**：`344, 402, 460, 518, 576, 754, 812, 870, 928, 986, 1174, 1232, 1290, 1348`；
* 前 12 次 lock 时的**玩家 x 完全相同**：`898, 1254, 875, 1261, 1784, 3242, 3758, 3336, 2813, 2371, 853, 1117`。

### 163.10 这条实测的含义

1. **DPS 只通过"血量数字"进入系统**，不直接影响走位。
   在血量差尚未造成状态差之前，走位**完全一样**。
2. 因此**分歧的来源被唯一确定**：当某个 DPS 下 Boss 的 `life` 先跨过某个阈值
   （半血 `39000` 或 15% `11700`），Boss 的**攻击选择/时序**随之改变，
   走位随之分岔。
   实测首次跨越半血的 tick：s2000 = **1409**、s1200 = **2189**、s700 = 3582、
   s600 = 4139、s500 = **4919**。
   **跨越越早，之后的分岔越长** —— 这正是"高 DPS 反而容易进坏相位"的原因。
3. **这给出了一个此前没有的结构性认识**：
   走位的**不确定性来源是转阶段**，而不是走位逻辑本身。
   在没有转阶段的前段（前 ~1400–4900 tick，视 DPS 而定），
   所有 DPS 的走位**是同一套**。

### 163.11 对策略的直接推论

既然前段轨迹与 DPS 无关、且**前段是干净的**（§163.7：20/28 个点 ≤3 次接触），
那么**分岔之后**才是问题所在。而分岔的触发量是**Boss 的血量跨阈值时刻**。

**可操作的方向**：让 **Boss 的血量跨阈值发生在走位的特定相位上** ——
这正是业主此前给过的指示（"引出龙卷后等一定时间与距离后才转阶段"）的精确化版本。
**区别在于**：现在我们知道要控制的是**"跨半血/15% 的那一刻，玩家正处于哪个相位"**，
而不是笼统地"等一会儿"。

**下一轮第一步**：对 s500/s600/s700 三个高受击点，记录**跨半血 tick 前后各 60 tick**
的玩家相位（`phase` 字段）与位置，与 s1200/s2000 的同一时刻对比，
找出"坏相位"与"好相位"的特征。
注意 `phase` 字段是**当帧赋值、不重新推导**的（§158 的 stale-label 教训），
因此必须同时记录位置与速度，不能只看标签。

### 163.12 防御档位的作用（蘑菇套 vs 黑曜石档）

对黑曜石档失败的点换蘑菇套（高防御）重测：

| 点 | 黑曜石档 | 蘑菇套 |
|---|---|---|
| 强翼 500 | 5 hit / 死亡 | 8 hit / 死亡 |
| 强翼 600 | 4 hit / 死亡 | 5 hit / **击杀** |
| 强翼 700 | 4 hit / 死亡 | 6 hit / 死亡（剩 1890） |
| 强翼 1100 | 4 hit / 死亡 | **1 hit / 击杀** |
| 弱翼 300 | 8 hit / 死亡 | **13 hit** / 死亡（tick 5839→11125） |
| 弱翼 600 | 6 hit / 死亡 | 6 hit / 死亡 |
| 弱翼 800 | 7 hit / 死亡 | 9 hit / 死亡 |

**结论：防御档位不改变轨迹，只改变"哪次失败是致命的"。**
高防御使单次伤害降低、战斗变长（弱翼 300 从 5839 拉到 11125 tick），
于是**接触次数反而增加**。这与既有记录一致（"防御越高⇒每次伤害越低⇒战斗越长⇒更多次受击"）。

**因此防御不是解决手段**，也不能用来"修好"失败点 —— 它只是把失败重新分配。

### 163.13 为什么"消除高受击簇"做不到：机制性的自由度分析

把本轮的因果链整理清楚，可以给出**为什么这个问题在本实验台里没有优雅解**：

1. 走位脚本是**确定性**的，且**不知道 Boss 的血量**（§163.12 实测：脚本无任何 phase 感知）。
2. 本实验台**无条件**按 DPS 扣血，所以**转阶段时刻完全由 DPS 决定**，脚本无法影响。
3. 转阶段发生在哪个**走位相位**上，决定了之后的分岔走向（§163.9–163.10）。
4. 于是：**对一套固定走位，"好结果"的 DPS 集合是离散且交替的**（§163.6 实测）。
   要让它变成"连续的好集合"，必须改变**分岔后的走向**，而不是分岔时刻。

**也就是说：脚本能动的只有"走位"这一个自由度，而 DPS 扫过的是一个
"转阶段相位"的一维参数空间。在只有走位可动的情况下，
把交替的达标集合变成覆盖 [300,2000] 的连续集合，等价于
要求走位在所有相位起跳后都能收敛到安全轨迹。**

这是本目标的**真正困难所在**，也是为什么历经 13 轮仍未达成全区间。

### 163.14 本轮达成的硬事实（供后续轮次直接使用）

| 事实 | 证据 |
|---|---|
| 强翼 **1200 / 2000** 达成 **hits==0 且击杀** | 原生实测，§163.1 |
| 强翼黑曜石档 **10/14** 达标 | §163.1 |
| 弱翼黑曜石档 **4/14** 达标 | §163.1 |
| 受击数**两极分布**（≤3 占 20/28，4–6 占 8/28） | §163.7 |
| **不同 DPS 的轨迹在转阶段前逐帧完全相同** | §163.9 裸数据 |
| 首次跨半血 tick：s2000=1409 … s500=4919 | §163.10 |
| 达标边界**非单调**（450 达标 / 475 失败 / 575 达标 / 625 失败） | §163.6 |
| 再生的量化作用（强翼 300 扛住 884 > 480 生命池并生还） | §163.7 |
| 冲刺方向由 facing 决定、facing 滞后一帧、输入只能反转 | §161.9–161.11 |
| 冲刺方向**不是**可用杠杆（位移预算 ~98px vs 需要 95px） | §162.4 |

### 163.15 下一轮的最优方向

鉴于 §163.13 的自由度分析，**唯一有希望的路线是让"分岔后的走向"更安全**，
而不是控制分岔时刻。具体两个候选，按优先级：

**(A) 让走位在分岔后快速"重新收敛"到安全巡航。**
分岔本身不可避免，但若分岔后 100–200 tick 内能回到与 DPS 无关的安全轨道，
则后续所有冲锋都回到同一套已证安全的几何。
**可测判据**：分岔后 N tick 内，玩家位置与"零接触基准 run"的位置差是否收敛。
这是一个**新的、可量化的目标**（此前从未提出）。

**(B) 直接以"零接触 run"为模板做回放式修正。**
强翼 1200 与 2000 是**实测零接触**的，它们的走位序列是**已知的安全解**。
若能找出这两条轨迹的**共同不变式**（例如每次 lock 时的相对几何），
就能把它写成与 DPS 无关的规则。

**注意**：不要再去调 `CHAITE_DASH_*` / ttc / prejump / standoff / 转阶段等待 ——
这些在 §158–163 已各自被独立否证（见 §162.5 表与 §163.13）。

### 163.16 ★ 方向 (A) 的实测结果：分岔后**永不重新收敛**

以强翼 1200（零接触）为基准，测量其余 run 与它的位置差：

| run | 分岔 tick | gap@fork | gap@+200 | gap@+500 | gap@end |
|---|---|---|---|---|---|
| s450（达标） | 2238 | 1.2 px | 317.1 | 541.6 | 1731.9 |
| s500（死亡） | 2238 | 1.2 | 317.1 | 541.6 | 1731.9 |
| s575（达标） | 2238 | 1.2 | 317.1 | 541.6 | 1731.9 |
| s600（死亡） | 2238 | 1.2 | 317.1 | 541.6 | 3009.6 |
| s650（达标） | 2238 | 1.2 | 317.1 | 541.6 | 1539.1 |
| s675（达标） | 2238 | 1.2 | 317.1 | 541.6 | 1758.6 |
| s700（死亡） | 2238 | 1.2 | 317.1 | 541.6 | 2917.7 |
| s725（达标） | 2238 | 1.2 | 317.1 | 541.6 | 246.4 |
| s750（达标） | 2238 | 1.2 | 317.1 | 541.6 | 2163.4 |
| s1075（达标） | 2238 | 1.2 | 317.1 | 1534.5 | 620.4 |
| s1150（达标） | 2238 | 1.2 | **151.2** | 432.5 | 1453.3 |

**三条决定性结论**：

1. **所有 run 都在 tick 2238 分岔**，且分岔时位置差仅 **1.2 px**。
2. **gap 单调增长，永不回落**（1.2 → 317 → 542 → 1500~3000）。
   竞技场宽度 5120 px，末期 gap 已达 1500–3000 px，
   即**两条轨迹在竞技场的不同半边**，**完全没有重新收敛**。
3. 罕见的例外 s725 末段回落到 246 px，但那只是"同向漂移恰好接近"，
   不是收敛（其 @+500 仍是 541.6）。

### 163.17 分岔点的直接证据：**转阶段**

在 tick 2238 处直接读双方状态：

```
s1200  t=2238  boss life=38020  ai=[4, 0, 2, 8]
s500   t=2238  boss life=61342  ai=[1, 0, 2, 8]
```

**位置相同、`ai[1]/ai[2]/ai[3]` 相同，唯一差别是 `ai[0]`：4 vs 1。**
`ai[0]=4` 是**第二阶段（狂暴）的攻击选择**，`ai[0]=1` 是第一阶段的冲锋。

**这就是分岔的机制，已直接观测到**：
s1200 的 Boss 已跨过半血（38020 < 39000）进入新阶段，攻击选择变为 4；
s500 的 Boss 血量 61342 远未过半，仍在阶段一的攻击选择 1。
**玩家在相同时刻处于相同位置，却因为 Boss 选择了不同攻击而走向不同轨迹。**

这与 §163.9–163.10 完全自洽：分岔由**转阶段**触发，
而转阶段时刻由 DPS 决定。

### 163.18 结论：方向 (A) 被否证，(B) 成为唯一路线

**方向 (A)"分岔后重新收敛"被实测否证** —— 分岔后永不收敛。
所以不存在"只需防守分岔后一小段"的捷径。

**这同时提高了方向 (B) 的地位**：既然分岔不可逆、不可收敛，
而分岔又完全由"转阶段相位"决定，
那么**走位必须在"被分岔到任意相位"之后仍然安全**。

**关于 (B) 的一个新认识（本轮可得）**：
`ai[0]=4`（第二阶段攻击选择）在 §163.16 的表格里
**同时出现在达标 run（s450/s575/s650/s675/s725/s750/s1075/s1150）
与死亡 run（s500/s600/s700）中** —— 注意死亡 run 的 s500/s600/s700
在 tick 2238 时 `ai[0]` 仍是 1，它们是在**更晚**才跨半血的
（s500 跨半血在 tick 4919）。

**所以真正的危险不是"在阶段二"，而是"从阶段一跨到阶段二的瞬间"。**
达标 run 都是**较早**跨半血（s2000=1409、s1200=2189、s1075=2416、s1150=2274），
死亡 run 都是**较晚**跨半血（s500=4919、s600=4139、s700=3582）。

**这是一条可检验的规律**：跨半血越早，越可能达标。
理由：跨得越早，之后的战斗越短，暴露在"分岔后新轨迹"中的冲锋次数越少。

**下一轮第一步**：验证这条规律 —— 用 `CHAITE_SIM_DPS` 之外的手段
（例如临时提高前期 DPS）确认"跨半血时刻"本身是因果量而非相关量。
若确认，则对**晚跨半血**的 DPS 点（500/600/700）需要的是
**在跨半血前后的一段窗口内进入一个对两种攻击选择都安全的姿态**，
这是可以写进脚本的（脚本虽不知血量，但**知道 Boss 的 `ai[0]` 与 `timer`**，
因此可以在 `ai[0]` 变化的那一刻识别转阶段并做出反应）。

---

## §164. 第 164 轮：生存判据被**精确确定**为"总接触数 ≤ 3"，并定位到接触时的翼力状态

### 164.1 ★ 判据：总接触数是唯一的判别量（密度不是）

对全部 28 个强翼点，同时计算总接触数与"命中密度"（任意 300/500/800 tick 窗口内的最大接触数）：

| 判据 | 击杀组 | 死亡组 | 可分性 |
|---|---|---|---|
| **总接触数** | 0–6（中位 **2**） | **4–5**（中位 4） | **完美：≤3 ⇒ 20/20 击杀；≥4 ⇒ 0/8 击杀** |
| max/300 窗口 | 0–3（中位 1） | 2–3（中位 2） | 差（重叠） |
| max/500 窗口 | 0–3 | 2–3 | 差 |
| max/800 窗口 | 0–3 | 2–4 | 差 |

**结论：生死完全由"总接触数是否 ≤ 3"决定，与接触的密集程度无关。**
这与 §163.7 的伤害核算一致（单次 100–180，生命池 480，加上长战斗的再生，
第 4 次接触即致命）。

**这同时把目标简化为一个**极其明确**的单一量化目标：
**把全部 28 个点的总接触数压到 ≤3。**
当前强翼已有 **20/28** 达标（0–3 次），只有 **8 个点**（4–5 次）需要各消掉 1–2 次接触。

### 164.2 接触发生时玩家在哪里

逐次接触前 30 tick 的状态（`prehit` 稠密行）：

| run | 接触数 | **翼力 ≤3** | **等高（\|dy\|<92）** | 两者同时 | 接触时翼力序列 |
|---|---|---|---|---|---|
| 弱翼 300 | 8 | 3 | 5 | 3 | [0,0,37,130,96,130,32,0] |
| 弱翼 600 | 6 | **5** | **5** | **5** | [0,0,96,0,0,0] |
| 弱翼 800 | 7 | 2 | 2 | 2 | [0,46,46,46,46,20,0] |
| 强翼 475 | 4 | **4** | 1 | 1 | [0,0,0,0] |
| 强翼 625 | 4 | 2 | 2 | 2 | [58,0,111,0] |
| 强翼 300 | 6 | 3 | 2 | 1 | [0,129,0,0,174,134] |

**观察到的机制**：在弱翼 600 的 6 次接触中，**5 次发生在翼力为 0 时**，
而玩家当时**正在下达 16–25 帧的上升指令**（`vy < -1`）——
即**在空槽上按跳**。此时无法爬升，只能水平移动，
而 Boss 恰好在同一高度（|dy|<92 是接触的竖直门限，**5/5 都满足**），
于是水平拉开不够就必然接触。

**注意（避免过拟合）**：这个模式**不是**普遍成立的 ——
弱翼 800 只有 2/7 次是空槽，强翼 625 是 2/4。
所以"空槽"是**弱翼低 DPS 段的一个突出的失败模式**，但不是全部原因。

### 164.3 本轮确立的可操作目标

1. **判据明确**：总接触数 ≤3 ⇒ 存活。（§164.1，20/20 vs 0/8）
2. **只需修 8 个点**，且每个只需减少 **1–2** 次接触 —— 不是"重写走位"。
3. **弱翼需要更多**（弱翼 10 个失败点 vs 强翼 4 个），且弱翼的失败集中在低 DPS 段。
4. 弱翼低 DPS 段的突出模式是**空槽爬升**（§164.2），
   而**翼力只能在落地时补充**（既有实测：空中翼力**从不增加**，0 帧/4 runs）。

**因此下一步应该针对弱翼低 DPS 段，减少"空槽爬升"的发生**，
而不是继续调整闪避方向或时机（那些已被独立否证）。

### 164.4 ★★ 根因定位：**弱翼从不与 Boss 保持高度分离**

定义 `dy = player.y − boss.y`，**取负表示玩家在 Boss 上方**。
只统计 **Boss 处于冲锋态（`ai[0]==1`）** 的帧 —— 这是唯一危险的帧集合：
接触的竖直门限是 `|dy| < 92`（正文既有实测），故把 `−92..92` 称为 **LEVEL（死亡带）**。

| run | <−500 | −500:−300 | −300:−92 | **LEVEL** | 92:300 | 300:500 | >500 |
|---|---|---|---|---|---|---|---|
| **强翼 1200（零接触）** | **27.8%** | 14.3% | 5.7% | 13.4% | 6.8% | 15.0% | **17.0%** |
| **强翼 2000（零接触）** | **44.0%** | 9.3% | 6.4% | 16.9% | 7.4% | 2.1% | 13.8% |
| 强翼 300（达标） | 22.9% | 12.6% | 8.7% | 9.9% | 6.3% | 17.7% | 21.8% |
| 强翼 500（死亡） | 24.6% | 14.2% | 7.3% | 8.4% | 7.5% | 16.9% | 21.1% |
| 强翼 600（死亡） | 24.3% | 12.9% | 7.1% | 9.9% | 8.7% | 19.4% | 17.6% |
| 强翼 700（死亡） | 26.8% | 11.2% | 6.7% | 10.1% | 5.5% | 22.1% | 17.7% |
| **弱翼 300（死亡）** | **0.0%** | 0.5% | 30.8% | **32.3%** | 25.6% | 8.7% | **2.2%** |
| **弱翼 600（死亡）** | **0.0%** | 0.0% | 27.1% | **32.1%** | 29.9% | 9.5% | **1.4%** |
| **弱翼 900（击杀）** | **0.0%** | 0.0% | 27.1% | 28.6% | 33.3% | 8.9% | 2.2% |

**这是本项目至今最清晰的一条结构性差异：**

* **强翼**在冲锋帧中有 **22–44% 的时间待在距 Boss 500px 以上**，只有 **8–17%** 在死亡带。
  它在**上方**（dy<−500）待的时间最长，且下方也有大量时间（300:500、>500）。
* **弱翼**有 **0.0%** 的冲锋帧能超过 −500，**0.0%** 超过 +500；
  它有 **57%** 的冲锋帧挤在 **±300 以内**，其中 **32%** 直接落在**死亡带**里。

**也就是说：弱翼从不制造高度分离。** 它的 `dy` 全域只有 **[−393, +1334]**，
而强翼是 **[−938, +1258]**。

### 164.5 为什么弱翼做不到：爬升率差一倍

| | 峰值爬升 | 爬升帧占比 | \|dy\|>500 的帧数 | 最长连续段 |
|---|---|---|---|---|
| 弱翼 600 | **−9.91** | 51% | **474** | 171 |
| 强翼 1200 | **−16.52** | 44% | **1879** | 373 |

弱翼峰值爬升 **−9.91**，强翼 **−16.52**（差 1.67 倍，与既有实测 5.08/7.50 的比例一致）。
两者爬升帧占比相近（51% vs 44%），但**弱翼累计出的高度分离只有强翼的 1/4**。

**注意弱翼 `dy` 的最大值是 +1334（远在 Boss 下方），
说明弱翼不是"爬不上去"，而是"大量时间在下坠/低于 Boss"。**

### 164.6 修正一条既有的错误实测

§159 记录"`wingTime` 在空中**从不增加**（0 帧 / 4 runs）"—— **这是错的，本轮已用裸数据否证**：

在弱翼 600 中 `wingTime` 增加 **18 次**，**全部**满足
`vy: 0.00 → −6.48`、`controlJump=True`、`releaseJump=False`、`jump=15`、**`justJumped=True`**，
且**一律补满到 130**（`wingTimeMax`）。

这与反编译一致（`Player.cs` 的顶点补翼力分支：
`if (((velocity.Y == 0f || sliding) && releaseJump) || (autoJump && justJumped)) wingTime = wingTimeMax;`）。

**所以翼力**不是**只在落地时补充，而是在**每次到达顶点（vy 归零）时**补满。**
这条修正很重要，因为它把"补翼力"从"必须落地"（§160 已否证，因为落地会进入 Boss 高度带）
变成了"**只要到达顶点**"—— 而到达顶点是**空中动作**，不需要落地。

弱翼 600 的补翼力间隔：min 35 / **中位 252** / max 438 tick；
空槽占比 31%（1650/5321 帧）。

**因此弱翼的"空槽爬升"（§164.2）的真正原因不是不能补，而是补得太稀**
（每 252 tick 才到一次顶点），而冲锋每 ~58 tick 来一次 —— **约每 4 次冲锋才有一次补满**。

### 164.7 由此得到的、可实现的明确方向

**弱翼需要在每次冲锋（58 tick 周期）内到达一次顶点以补满翼力，
并在冲锋到来时把高度分离拉到 92px 以上（死亡带之外）。**

这两件事**都是空中动作**，不需要落地，因此绕开了 §160 否证的"落地补翼力"陷阱。
**具体量**：弱翼爬升率 −9.91，要在 58 tick 内爬出 92px 只需 ~10 tick；
难点不在爬升率，而在**当前线路没有在冲锋周期内安排顶点**。

**下一轮第一步**：在弱翼脚本中检查/修改"何时到达顶点"的节奏 ——
即上升指令的**持续时间与中断条件**，使其与 58 tick 的冲锋周期对齐
（每次冲锋前完成一次"上升→顶点→补满"）。
判据：弱翼冲锋帧的 LEVEL 占比应从 **32% 降到接近强翼的 10%**，
`|dy|>500` 帧数应从 **474 升到 1500+**。
这是**可量化、可验证**的目标，且不与任何已否证项冲突。

### 164.8 ★★ 完整因果链（本轮最终结论，逐环都有实测）

**第 1 环：接触的几何条件。** 接触需要横向 `|dx| < 95` **且** 竖直 `|dy| < 92`（既有实测）。

**第 2 环：弱翼在冲锋期间被压在中高度。**
冲锋帧的 `dy` 分布（§164.4）：弱翼 **0.0%** 超过 ±500，
**57%** 挤在 ±300 内，**32%** 直接落在死亡带。

**第 3 环：lock 时刻的位置本身不是决定性的。**
| run | lock 数 | lock 时 \|dy\|<92 | 若在带内所需额外爬升 |
|---|---|---|---|
| 弱翼 600 | 47 | **5（11%）** | 中位 0 / 最大 **157 px = 16 tick** |
| 弱翼 300 | 63 | 8（13%） | 最大 174 px = 18 tick |
| 强翼 1200 | 24 | 3（12%） | 最大 160 px |
| 强翼 500 | 56 | 4（7%） | 最大 170 px |

冲锋持续 **28 tick**，弱翼爬升 −9.91 px/tick ⇒ **28 tick 可爬 277 px**。
**所以在 lock 时若在死亡带内，理论上 16–18 tick 就能爬出来 —— 来得及。**

**第 4 环：但那时翼力是空的。**
弱翼 600 的 47 次冲锋，**lock 时翼力中位数只有 32（满值 130）**，
且 **21/47（45%）在 lock 时翼力 < 20**。
**恰好那 5 次"需要爬出死亡带"的冲锋，lock 时翼力分别是 14、0、0、14、0 —— 全部是空槽。**

> 这就是 §164.2 "空槽爬升"的**因果确认**：
> 不是线路不给爬升指令，而是**在需要爬升的那几次冲锋上，翼力恰好是空的**。

**第 5 环：翼力补充太稀。**
翼力**只在到达顶点（`vy` 归零）时**补满（§164.6 已修正旧记录）。
弱翼 600 实测：
* 补满 **18 次**，间隔 **中位 252 tick**（min 35 / max 438）；
* 而冲锋每 **~58 tick** 一次 ⇒ **大约每 4 次冲锋才补满一次**；
* 空槽占比 **31%**（1650/5321 帧）；
* 爬升段 **39 段**，但只有 **18 次**到达顶点 —— **超过一半的爬升中途因空槽而失败**。

### 164.9 结论与下一轮的唯一目标

**根因链条完整且闭合**：
弱翼补翼力太稀（每 252 tick 一次）→ lock 时翼力常为空（45% < 20）→
需要爬出死亡带的那几次冲锋恰好没翼力 → 玩家停在 `|dy|<92` 的死亡带内 →
接触 → 弱翼低 DPS 段 4–5 次接触 → 超过 3 次的生存上限 → 死亡。

**因此下一轮的唯一目标（可量化、可验证、不与任何已否证项冲突）：**

> **把弱翼的"到达顶点"节奏从每 252 tick 一次提高到每约 58 tick 一次
> （即每次冲锋前完成一次"爬升 → 顶点补满"），
> 使 lock 时翼力中位数从 32 升到接近满值。**

**判据（全部可实测）**：
1. lock 时 `wingTime` 中位数：**32 → ≥100**；
2. lock 时 `wingTime < 20` 的比例：**45% → <10%**；
3. 冲锋帧 LEVEL 占比：**32% → 接近强翼的 10–13%**；
4. 冲锋帧 `|dy|>500` 帧数：**474 → ≥1500**；
5. 弱翼低 DPS 段（300–800）总接触数：**8/8/7/6/6 → ≤3**；
6. 最终：弱翼黑曜石档达标数 **4/14 → 14/14**。

**实现要点（供下一轮）**：关键是让爬升**在到达顶点前不中断**，
因为顶点是唯一的补翼力时机，而当前线路有 21/39 的爬升段中途放弃。
需要在脚本的上升逻辑里加入"**为了补翼力而坚持到顶点**"的条件
（例如：翼力低 且 竖直速度仍为负 ⇒ 继续上升，不要提前 releaseJump），
这正是既有 `_refillGuardBudget` 想做的事，但它当时的判据是"落地补翼"（已被否证），
**应改为"坚持到顶点补翼"** —— 这是本轮修正带来的**新的、方向相反的**实现路径。

---

## §165. 第 165 轮：修正第 164 轮的目标定位 —— 瓶颈是**冲锋期间的平均竖直速度**，不是补翼力节奏

### 165.1 冲锋期间玩家在死亡带里的**停留时间**才是差异

接触的竖直门限是 `|dy| < 92`，故穿过该带（上下各 92，共 184 px 宽）所需的**时间**决定暴露量：

| run | 冲锋数 | 冲锋帧数 | **在带内帧数** | 占比 |
|---|---|---|---|---|
| 弱翼 600 | 47 | 1316 | 422 | **32%** |
| 强翼 1200 | 24 | 672 | 90 | **13%** |
| 强翼 500 | 56 | 1568 | 132 | **8%** |

而起手就在带内的冲锋数三家相近（5/47、3/24、4/56），
且"带内起步的冲锋能达到的最大分离"也相近（弱翼 279/107/128/248/129，强翼 172/273/192/370）。

**所以差异不在"是否在带内起步"，而在"穿过这个带要花多久"。**

### 165.2 ★ 直接原因：冲锋期间的**平均竖直速度**差 1.5 倍

| run | 冲锋首帧 `vy` 中位 | 首帧在爬升(<−3) | 首帧水平 | 首帧下坠(>3) | **冲锋期间 mean\|vy\|** |
|---|---|---|---|---|---|
| 弱翼 600 | **−4.40** | 25 | 11 | 11 | **6.87** |
| 强翼 1200 | **−6.74** | 14 | 1 | 9 | **10.26** |
| 强翼 500 | **−6.74** | 31 | 2 | 23 | **10.57** |

**弱翼穿过 184 px 的死亡带需要 `184/6.87 ≈ 27 tick`，正好等于一次冲锋的全部时长（28 tick）；
强翼只需 `184/10.26 ≈ 18 tick`，留下 10 tick 余量。**

**这就是"弱翼 32% 在带内、强翼 8–13%"的算术解释**，也解释了为什么弱翼恰好
在"低 DPS 长战斗"里累积到 4–5 次接触（每次冲锋都长时间暴露）。

### 165.3 因此第 164 轮的目标定位需要修正

第 164 轮把下一步定为"把到达顶点的节奏从每 252 tick 提到每 58 tick，让 lock 时翼力接近满值"。
**本轮实测显示这个定位不够准确**：

* 弱翼 600 的 **lock 时翼力中位数是 32**，强翼 1200 是 **26**，强翼 500 是 **4** ——
  **三者相近甚至弱翼更高**。翼力在 lock 时的数值**并不能区分**好/坏 run。
* 真正区分的是**冲锋期间是否维持了 `|vy| ≈ 10` 的持续爬升**。

**所以正确的目标不是"每次冲锋前补满翼力"，而是"每次冲锋期间维持高速爬升"** ——
翼力只是达成它的**手段之一**（没有翼力就爬不动），但不是判据本身。

### 165.4 一个必须注意的取舍（本轮发现，尚未解决）

弱翼翼力上限 130、爬升消耗 1/tick ⇒ **满槽只能维持 130 tick 的爬升**，
而冲锋每 58 tick 一次 —— **满槽足以覆盖两次冲锋**。
但实测翼力中位只有 32，说明**大部分时间没在爬升**（或爬到一半就空了）。

同时有一个反向证据：既有的 `_refillGuardBudget`（强制下坠以落地补翼力）
**在 positive 取值下让结果一致变差**（§既有：guard 60 → 3 hits，但引入新 hit；
guard 60 gated → 9 hits）。原因正是 **"下坠补翼力"会牺牲冲锋期间的爬升速度** ——
与本轮测出的瓶颈**直接冲突**。

**这给出一条清晰的设计约束**：
> **任何为了补翼力而"下坠"的手段，都会降低冲锋期间的 `mean|vy|`，因而有害。**
> 若要在弱翼上补翼力，**必须用不牺牲爬升的方式** ——
> 例如在**顶点**补（`vy==0 && releaseJump`），而不是靠强制下坠。

### 165.5 下一轮的具体目标（可量化）

**把弱翼冲锋期间的 `mean|vy|` 从 6.87 提到强翼的水平（≈10.3），
即把穿过 184 px 死亡带的时间从 27 tick 压到 18 tick 以内。**

判据：
1. 弱翼 600 冲锋期间 `mean|vy|`：**6.87 → ≥10**；
2. 弱翼冲锋帧 LEVEL 占比：**32% → ≤15%**；
3. 每 58 tick 冲锋周期内的**实际爬升帧数**显著提高（首帧爬升占比 25/47 → ≥40/47）；
4. 弱翼低 DPS 段总接触数 ≤3；最终弱翼黑曜石档 4/14 → 14/14。

**实现要点**：优先在**顶点**补翼力（不牺牲爬升），
并且**不要**用强制下坠补翼力（§165.4 的约束）。
首帧"水平/下坠"共 22/47 需要变成"已在爬升"——
这需要**在冲锋到来之前就处于爬升状态**（提前起爬），
即让上升指令与冲锋周期同相，而不是等 lock 才响应。

### 165.6 ★ 精确定位：弱翼每次冲锋在死亡带内停留 **9.0 帧**，强翼只有 **2.4–3.8 帧**

| run | 冲锋数 | 冲锋中曾达到 \|dy\|>150 | **平均进带次数/冲锋** | **平均带内帧数/冲锋** |
|---|---|---|---|---|
| 弱翼 600 | 47 | 39（83%） | 0.62 | **9.0** |
| 强翼 1200 | 24 | **24（100%）** | 0.29 | **3.8** |
| 强翼 500 | 56 | **56（100%）** | 0.23 | **2.4** |

**关键更正（推翻本轮早先的推断）**：
lock **之前**的位置三家几乎相同 —— "lock 前已在带外"的比例是
**弱翼 89%（42/47）、强翼 88%（21/24）、强翼 93%（52/56）**。
**所以在冲锋开始时，弱翼并不比强翼更贴近 Boss。**

**弱翼的 9.0 帧/冲锋几乎全部产生于冲锋进行之中。**

### 165.7 直接原因：弱翼的爬升**起步太浅**

逐次冲锋的 `vy` 序列（实测）：

* **弱翼**：首帧 `−3.7`，整场缓慢上升，末帧 `−6.4`（斜坡），
  或首帧 `−4.9 → −7.6`；只有**少数**冲锋能达到 `−9.9`（如 t=754/812/870/1290，翼力 110）。
* **强翼**：首帧 `−9.1 → −12.5`，或**直接 `−16.3` 并整场维持**（t=402/812/1406），
  或首帧 `−16.5` 后衰减。

**弱翼的爬升斜率（−3.7 → −6.4）意味着它在冲锋的大部分时间里
竖直速度只有最强翼的 1/3 ~ 1/2，于是穿过 184 px 死亡带要花掉整个冲锋。**

**注意这不是翼力不足**：弱翼在 t=754/812/870/1290 都达到了 `−9.9`，
当时翼力分别是 110 / 62 / 14 / 30 —— **翼力 62 和 14 时也能达到 −9.9**
（§164.6 的规律：翼力低于 100 的爬升阶段会按真实速度加速，反而更快）。
**所以弱翼在物理上能做到，只是线路大多让它从斜坡起步。**

### 165.8 修正后的下一轮目标（取代 §165.5）

**不是**"提高平均竖直速度"这么笼统，而是精确的两点：

1. **让弱翼在每次冲锋的**首帧**就具备高爬升速度（`vy ≤ −9`），
   而不是从 `−3.7` 斜坡起步。** 这要求**在 lock 之前就已经在爬升**，
   或**在 lock 的当帧处于下坠（+10）状态以立刻转入满血爬升** ——
   强翼的零接触 run（s1200）正是如此：lock 前 9/24 次是**下坠 +10**，然后立刻转成高速爬升。
2. **把弱翼"冲锋中达到 \|dy\|>150"的比例从 83% 提到 100%**（强翼是 100%），
   并把**带内帧数/冲锋从 9.0 压到 ≤3.8**。

**判据**：
1. 弱翼冲锋首帧 `vy` 中位：**−4.40 → ≤−9**；
2. 冲锋中达到 `|dy|>150` 的比例：**83% → 100%**；
3. 带内帧数/冲锋：**9.0 → ≤3.8**；
4. 冲锋帧 LEVEL 占比：**32% → ≤13%**；
5. 弱翼低 DPS 段总接触数 ≤3；弱翼黑曜石档 **4/14 → 14/14**。

**实现要点**：弱翼当前的上升指令**在 lock 时才响应**，
而它的爬升有斜坡延迟，于是整个冲锋都耗在斜坡上。
应让上升/下坠的相位**与 58 tick 冲锋周期同相**，
使 lock 时刻恰好落在"下坠末端"或"爬升满速"上。
强翼之所以 100% 达标，正是它的相位天然对齐了这个周期。

---

## §166. 第 166 轮：预锁抬升带被否证，并把**冲锋节奏**测准

### 166.1 预锁抬升带（`CHAITE_PRELOCK_LIFT`）：实现、实测、**否证并完全回退**

**动机**：既有实现里"共位抬升"只在 `|dy| < CoLocationBand (24)` 时才请求上升，
而接触需要 `|dy| < 92` —— **24 到 92 之间这 68px 是"没有任何上升指令"的死区**。
弱翼平飞爬升上限约 −5.08 px/tick，从死区出发穿过 184px 带宽需约 36 tick > 冲锋 28 tick，
与 §165 实测的"弱翼每冲锋 9.0 帧在带内"吻合。故实现了一个**提前请求抬升**的带（仅在锁前生效）。

**惰性验证通过**：未设该变量时弱翼 600 逐字节复现 **5320/6/30138**。

**实测结果（弱翼 600，基线 5320/6/30138）**：

| band | ticks | hits | life left | 结果 |
|---|---|---|---|---|
| 基线 | 5320 | 6 | 30138 | 死亡 |
| 60 | 4894 | **5** | 34362 | 死亡 |
| **92** | **2866** | 6 | 54711 | 死亡（大幅变差） |
| 140 | 5348 | **7** | 29880 | 死亡 |
| 200 | 3844 | 6 | 44846 | 死亡 |

**全部变差**：无一处击杀；命中有增有减（5/6/7），而 tick 数普遍下降（更早死亡）。
`band=92` 尤其糟糕（2886 tick，仅为基线的 54%）。

**这是又一次典型的"零和"签名**（§既有 11 次的教训）：
`vertical` 覆写改变了受击的**分布**而不是**数量**。
按既有规则，**实测退步的实现必须从源码移除，而不是以关闭的开关形式留下** —— 已**完全回退**，
`git diff --stat e26f7ed -- src/ tools/` 为空，源码与上一提交逐字节相同。

**为什么"提前抬升"不奏效（机制解释）**：Boss 的悬停**跟随玩家**（§161.1：`|boss.y−player.y|`
中位 230px）。提前抬升把玩家升高，**Boss 也随之升高**，相对几何几乎不变；
而抬升额外消耗翼力，并在锁定线上留下沿线的位移。
**"通过升高自己来拉开高度差"在这个 Boss 上原理性无效**。

### 166.2 ★ 冲锋节奏已测准（"相位对齐"的前提）

全部冲锋长度 **严格 28 tick**（弱翼 47/47、强翼 24/24 都是 28）。

悬停长度两值：**30 tick（主）与 40 tick（次）**。
于是**冲锋到冲锋的周期呈双峰**：

| 周期 | 弱翼 600 | 强翼 1200 |
|---|---|---|
| **30 tick**（冲锋后悬停 30 就再锁） | **37 次** | **19 次** |
| 150 tick | 5 次 | 2 次 |
| 160 tick | 4 次 | 2 次 |

**主周期是 28（冲锋）+ 30（悬停）= 58 tick**，且这是**恒定的**，与 DPS 无关
（弱翼与强翼的比例几乎一致：37/47 与 19/24）。
余下 9/47 与 4/24 是 150–160 tick 的**长周期**（即多次悬停后才再冲锋）。

### 166.3 由此得到的明确结论

**"相位对齐"的目标现在是可计算的**：主循环 58 tick 中，玩家有
**30 tick 的窗口**（冲锋结束后的悬停期）可用于制造高度分离。
弱翼在 30 tick 内以 −5.08 可爬 **约 152 px** —— **刚好超过 92 png 的死亡带半宽**。

**但关键在于 §166.1 的教训**：单纯爬升会被 Boss 的跟随悬停抵消。
所以有效的做法**不是"升得更高"，而是"在锁定时处于下坠并把下坠转化为爬升"** ——
即 §165.7 观察到的强翼行为（锁前 `+10` 下坠 → 锁后立刻 `−9…−16` 爬升）。

**下一轮的可执行目标**（取代 §165.8）：
> 在每次冲锋 lock 之前，让弱翼处于**下坠状态（`vy ≈ +10`）**，
> 使其在 lock 后能立刻转入最高速爬升，而不是从 −3.7 的平飞斜坡起步。

**判据**：
1. 弱翼 lock 前 1 帧 `vy` 中位：**−4.40 → ≥ +5（下坠）**；
2. 弱翼冲锋首帧 `vy` 中位：**−4.40 → ≤ −9**；
3. 冲锋中达到 `|dy|>150` 的比例：**83% → 100%**；
4. 带内帧数/冲锋：**9.0 → ≤3.8**；
5. 弱翼黑曜石档：**4/14 → 14/14**。

**注意**：这**不是**"提前抬升"（已否证），而是**相位调整** ——
下坠是免费的（重力），不消耗翼力，且把"爬升"的全部预算集中在冲锋窗口内。

### 166.4 `CHAITE_APEX_REFILL=1` 的实测：又一次**零和交换**

把既有的顶点补翼力开关打开，在弱翼失败段测三点：

| 点 | 基线（关闭） | **开启 apex_refill** | 变化 |
|---|---|---|---|
| 弱翼 300 | 5839 / **8** / 51366 死亡 | 7804 / **8** / 41553 死亡 | tick 变长，命中不变，**仍死亡** |
| 弱翼 600 | 5320 / **6** / 30138 死亡 | 6219 / **6** / 21107 死亡 | tick 变长（伤害多），**仍死亡** |
| 弱翼 800 | 3875 / **7** / 33502 死亡 | 6366 / **5** / **0 击杀** | **转为击杀** ✓ |

**净效果为零**：修好 1 个点（800 由死亡变击杀），代价是 300 更差、600 无改善。

**注意一个有趣的细节**：开启后三点的 tick 数**都变长**（+33%、+17%、+64%），
说明顶点补翼力**确实在起作用**（玩家活得更久、输出更多伤害），
但**接触数没有相应下降** —— 600 稳定在 6，300 稳定在 8。
**"活得更久"没有转化为"挨打更少"，只是把同样的接触分散到更长的时间里。**

这与 §164.1 的判据完全一致（生死由**总接触数 ≤3** 决定）：
既然接触数不变，生存结果自然不会改善。
**所以补翼力不是充分条件**，符合 §165.3 的修正结论。

### 166.5 本轮状态

**源码零改动**（预锁抬升带已完全回退，`git diff` 为空；`CHAITE_APEX_REFILL` 是既有开关，未改默认值）。

**本轮的四项硬结果**：
1. "提前抬升"被否证（§166.1），且给出了机制解释：**Boss 悬停跟随玩家，升高自己无效**；
2. **冲锋节奏测准**（§166.2）：冲锋恒为 **28 tick**，悬停 30/40，主周期 **58 tick** 恒定；
3. **顶点补翼力是零和交换**（§166.4）：延长战斗但不减少接触；
4. 由此确立下一轮目标：**在 lock 时处于下坠（+10），把下坠转化为高速爬升**（§166.3），
   因为下坠免费（重力）且不消耗翼力，而"升高自己"已被证明无效。

**对后续轮次的重要提醒**：
到目前为止，**所有**通过改变 `vertical` 来改善走位的尝试都呈零和
（共 13 次：§162.5 表的 8 项 + 本轮预锁抬升 + apex_refill + 此前若干）。
**唯一真正有效的两项改进（§129、§132）都是"补上缺失的前置条件"，而不是改变既有指令。**
这条规律在下一轮应当作为首要筛选标准：
**若一个改动是"在某个条件下改写 vertical/horizontal"，预期就是零和；
若一个改动是"在某个条件下允许/禁止一条既有指令"，才可能有净收益。**

---

## §167. 第 167 轮：第 166 轮的"锁定时下坠"目标**已被实现并否证过** —— 源码里已记录

### 167.1 重要更正：`CHAITE_CHARGE_NORMAL_OWNER` 就是这个想法，且已否证

第 166 轮 handoff 把下一步定为"让弱翼在 lock 时处于下坠（`vy≈+10`），
把下坠转化为高速爬升"。**本轮查阅源码发现这个想法早已实现过，就是
`CHAITE_CHARGE_NORMAL_OWNER`（`FishronWingScript.cs:3217-3250`）**，
即业主的规则本身：

```
vertical = -sign(player.y - boss.y)
```

（源码形式：`_chargeNormalVertical = sign(-dy)`，见 `LatchChargeNormal` 末段）
——**Boss 在上则向下躲，Boss 在下则向上躲**，正是"向法线躲避"，
也正是"锁定时下降"的一般化形式。**实测结果（源码内记录的原文数据）**：

| 变体 | 结果 |
|---|---|
| 仅条款 2（**每次 lock 都翻转符号**） | 强翼 600/1000/1500 **全部在 tick 2151 死亡，5 hits** |
| 两条条款（水平时不翻转 + 距离够远时水平拉开） | 强翼 600/1000/1200/1500 **全部在 tick 1811 死亡，5 hits** |

基线对照：强翼 600 原本能活到 6000（6 hits），**强翼 1500 原本是零接触击杀**。
弱翼 1500 → 击杀但 4 hits（基线 3）；弱翼 1600 → 击杀 3 hits。

### 167.2 ★ 两条必须继承的结论

**(1) 锁定时的 `dy` 近似随机，不能作为决策依据。**
源码原文（L3241-3246）：
> "`dy` at lock is measured against a Boss that is usually within a body length of
> the player, so its sign is near-arbitrary and flips from charge to charge;
> obeying it replaces a coherent escape with a coin flip"

这与 §165.6 的实测完全一致：**锁前位置三家几乎相同**（"lock 前已在带外"89%/88%/93%）。
**所以"强制锁定时下坠"必然退化为掷硬币，本轮不再重复。**

**(2) 业主的规则是好的"描述"，但不是好的"控制器"。**
源码原文（L3248-3249）：16 次受击的 lock 帧审计显示，
**现有线路已经在 13/16（81%）的情况下遵守了业主规则** ——
所以规则描述了"一个有效逃跑通常长什么样"，但把它当作判别器会杀死两条线。

**这与本项目已有的 13 次零和教训同源**：改写 `vertical` 在某个条件下的取值，
与"允许/禁止"既有指令，是两类不同的干预。
`CHARGE_NORMAL_OWNER` 属于前者，已否证。

### 167.3 第 166 轮 handoff 的"下一轮目标"因此作废

§166.3 / §166.5 的"在 lock 时下坠"**作废**（不是未测试，而是已测试且灾难性失败）。
**下一轮不得重试**：`CHAITE_CHARGE_NORMAL_OWNER`、任何"按 dy 符号翻转逃跑方向"的规则、
以及"每次 lock 强制下坠"。

**仍然有效的部分**：§166.2 的节奏测量（冲锋恒 28 tick、悬停 30/40、主周期 58 tick 恒定）
是**独立于该目标**的硬事实，继续有效。

### 167.4 本轮转向：把"稳定存活并击杀"这条**放宽后的验收线**测准

业主放宽后的验收标准是：
> 对 BOSS 的 DPS 在全区间（300–2000）内均能**稳定存活并击杀**猪鲨。

注意这条的措辞是"**存活并击杀**"（`SuccessNoDeath` 且 `bossLife == 0`），
**不是**"每次都不死"。因此本轮把强翼在全 28 点上的**完整结果**重测一遍，
统计击杀率，以确定这条线当前的实际达成度，以及缺口集中在哪一端。
（结果见下节，本节建立判据。）

### 167.5 ★ 强翼黑曜石档：**28 点全测，19 点击杀**（含 2 次零接触）

第 164 轮的"14 点网格 10/14"是**抽样**，不是全区间。本轮把 300–2000 全 28 点逐一原生重测：

| dps | ticks | hits | life left | 结果 |
|---|---|---|---|---|
| 300 | 15938 | 6 | 0 | KILL |
| 400 | 12240 | 5 | 0 | KILL |
| 450 | 10937 | 4 | 0 | KILL |
| 475 | 7838 | 4 | 20233 | 死亡 |
| 500 | 7304 | 5 | 21642 | 死亡 |
| 525 | 8247 | 4 | 10545 | 死亡 |
| 550 | 7531 | 5 | 13926 | 死亡 |
| 575 | 8674 | 4 | 0 | KILL |
| 600 | 7830 | 4 | **5096** | 死亡（极近） |
| 625 | 7689 | 4 | **3480** | 死亡（极近） |
| 650 | 7738 | 3 | 0 | KILL |
| 675 | 7472 | 2 | 0 | KILL |
| 700 | 5965 | 4 | 14721 | 死亡 |
| 725 | 6997 | 2 | 0 | KILL |
| 750 | 6781 | 3 | 0 | KILL |
| 800 | 6386 | 2 | 0 | KILL |
| 900 | 5741 | 1 | 0 | KILL |
| 1000 | 5221 | 3 | 0 | KILL |
| 1050 | 4997 | 3 | 0 | KILL |
| 1075 | 4893 | 2 | 0 | KILL |
| 1100 | 4409 | 4 | 7087 | 死亡 |
| 1125 | 4461 | 4 | **4500** | 死亡（极近） |
| 1150 | 4611 | 1 | 0 | KILL |
| **1200** | 4441 | **0** | 0 | **KILL 零接触** |
| 1400 | 3884 | 1 | 0 | KILL |
| 1600 | 3466 | 1 | 0 | KILL |
| 1750 | 3215 | 2 | 0 | KILL |
| **2000** | 2881 | **0** | 0 | **KILL 零接触** |

**合计 19/28 = 68%。**

### 167.6 ★ 失败点的结构：**只差一点点**

9 个失败点中，**8 个的 BOSS 剩余血量在 3%–28%，其中 3 个在 7% 以内**：

| dps | 剩余血量 | 占 78000 |
|---|---|---|
| 625 | 3480 | **4.5%** |
| 1125 | 4500 | **5.8%** |
| 600 | 5096 | **6.5%** |
| 1100 | 7087 | 9.1% |
| 525 | 10545 | 13.5% |
| 550 | 13926 | 17.9% |
| 700 | 14721 | 18.9% |
| 475 | 20233 | 25.9% |
| 500 | 21642 | 27.7% |

**没有一个失败点是"被 BOSS 满血碾压"** —— 全部是"差一口气"。
**且首次受击时间与击杀时间同量级**（死亡点 4409–8247 tick），
说明失败发生在**战斗后期**（BOSS 血量低、玩家已被消耗到 4–5 次接触时）。

**这与 §164.1 的判据一致**：击杀组接触数 ≤3，死亡组 4–5。
**所以要跨过这条线，需要把 9 个失败点各减少 1–2 次接触。**

**关键观察**：`hits` 的分布**并不随 DPS 单调**
（500→5, 575→4, 600→4, 700→4, 1000→3, 1100→4, 1150→1, 1200→0, 1400→1, 1600→1, 1750→2, 2000→0）
—— 这是**混沌轨迹**的特征，与 §164 的"分岔点是转阶段"一致，
**不是逐步恶化的趋势**。因此"补齐这 9 个点"是**可能的**（同一线路在相邻 DPS 上已经能过）。

### 167.7 ★ 对比：强翼 vs 弱翼（同为黑曜石档、同一口径）

强翼 **19/28 = 68%**；弱翼在前 14 点（300–700）**全部死亡，0 击杀**：

| dps | 强翼结果 | 弱翼结果 |
|---|---|---|
| 300 | KILL 15938/6 | 死亡 5839/8（剩 51366） |
| 400 | KILL 12240/5 | 死亡 5839/8（剩 42536） |
| 450 | KILL 10937/4 | 死亡 7487/**9**（剩 25719） |
| 475 | 死亡 7838/4（剩 20233） | 死亡 7361/7（剩 23881） |
| 500 | 死亡 7304/5（剩 21642） | 死亡 6418/7（剩 28855） |
| 525 | 死亡 8247/4（剩 10545） | 死亡 6777/7（剩 23279） |
| 550 | 死亡 7531/5（剩 13926） | 死亡 6948/7（剩 19110） |
| 575 | KILL 8674/4 | 死亡 5426/6（剩 31072） |
| 600 | 死亡 7830/4（剩 **5096**） | 死亡 5320/6（剩 30138） |
| 625 | 死亡 7689/4（剩 **3480**） | 死亡 5210/6（剩 29223） |
| 650 | KILL 7738/3 | 死亡 6309/6（剩 15417） |
| 675 | KILL 7472/2 | 死亡 6162/6（剩 14645） |
| 700 | 死亡 5965/4（剩 14721） | 死亡 …（见结果文件） |

**结构性差异**：
* 强翼失败点剩血 **3%–28%**（"差一口气"）；弱翼失败点剩血 **19%–66%**（**差距是量级性的**）。
* 强翼接触数 1–6；弱翼接触数 **6–9**，且**从不低于 6**。
* 弱翼在 300/400 两点**逐字节相同**（5839/8，仅 boss 血量因 DPS 不同），
  与 §164 的"分岔点在转阶段、之前逐帧相同"一致。

**结论**：**弱翼不在"接近验收"的区间内**，它与强翼的差距是
**接触数 6–9 vs 1–6**，而非 1–2 次接触的微调。
按 §164.1 的判据（≤3 击杀、≥4 死亡），**弱翼需要削减一半以上的接触**。

### 167.8 本轮结论与后续方向

1. **第 166 轮的"锁定时下坠"目标已在源码中实现并否证**（`CHAITE_CHARGE_NORMAL_OWNER`，
   §167.1–167.3），**不得重试**。同时继承两条结论：
   锁定时 `dy` 近似随机故不能作决策依据；业主规则是好的"描述"而非好的"控制器"（81% 吻合率）。
2. **强翼黑曜石档的真实水平是 19/28**（含 2 次零接触），
   而非此前抽样的 10/14 —— **这是本项目最强的正面结果**，
   失败点全部在"差一口气"区间（3 个在 7% 以内）。
3. **弱翼是主要缺口**，且差距是结构性的（接触数 6–9）。
4. 由于**所有"改写 vertical"的干预都是零和**（已 13+ 次），
   后续应优先寻找**"允许/禁止既有指令"**形式的干预，
   或**先固定强翼**（它已接近验收），把弱翼作为独立的、更难的子问题。

**建议的下一步优先级**：
* **先收敛强翼**：9 个失败点各需减少 1–2 次接触，
  且 `hits` 不随 DPS 单调（混沌），说明目标是可达的。
* 弱翼需要的是**结构性**改变，而非参数微调。

> 注意：弱翼 300/400 的 `5839/8` 与 §164 记录一致，说明本轮口径与前几轮**可比**，
> 强翼 19/28 与前几轮"10/14"的差异**纯粹是抽样点不同**（前几轮只测了 14 点里的部分）。

### 167.9 ★ 弱翼全网格（26 点）与两翼的**失败严重度**对比

弱翼黑曜石档完整结果：**5/26 击杀**（w900、w1000、w1075、w1150、w1200）。

失败点的**剩余血量**对比（BOSS 满血 78000）：

| 翅膀 | 击杀率 | 失败点剩余血量范围 |
|---|---|---|
| **强翼** | **19/28 = 68%** | **3480 – 21642（4.5% – 27.7%）** |
| **弱翼** | **5/26 = 19%** | **4344 – 51366（5.6% – 65.9%）** |

**弱翼的 d300 失败时 BOSS 还剩 51366（66%）** —— 这不是"差一口气"，
而是"**根本打不动**"：弱翼在 5839 tick 内就被打死，而 BOSS 才掉 34% 血。

**决定性对照**：同样是 **dps 300**：

| 翅膀 | ticks | hits | 结果 |
|---|---|---|---|
| 强翼 | **15938** | 6 | **KILL** |
| 弱翼 | **5839** | 8 | 死亡（剩 51366） |

**强翼同 DPS 下能存活 2.7 倍长的时间并击杀，弱翼在 1/2.7 处就死了。**

### 167.10 ★ 结论：弱翼的瓶颈是"**耐力**"，不是"单次闪避质量"

两翼的**唯一差别是翅膀**。强翼：`wingTimeMax 180`、爬升峰值 **16.52**；
弱翼：`wingTimeMax 130`、爬升峰值 **9.91**。

**弱翼接触数 4–9 vs 强翼 1–6**，且**低 DPS 段（300–800）弱翼全部失败**。
低 DPS 意味着**战斗时间长**，而弱翼在长时间战斗中的暴露次数更多 ——
与 §164.1 的"存活由总接触数决定"一致。

**因此弱翼需要的不是"把某一类接触减 1–2 个"，而是把接触数从 6–9 砍到 ≤3，
即几乎是减半。** 这是**结构性**问题。

### 167.11 本轮的最终建议（供后续轮次）

1. **优先收敛强翼**：19/28，9 个失败点全部在"差一口气"区间
   （3 个在 7% 以内：625→3480、1125→4500、600→5096），
   且 `hits` 不随 DPS 单调（混沌），说明目标是**可达的**。
   强翼单独达标即满足"该套配装全区间存活并击杀"。
2. **弱翼作为独立、更难的子问题**：需要结构性改变，
   而所有"改写 vertical"的干预已 13+ 次零和。
   应寻找**"允许/禁止既有指令"**形式的干预，或
   重新审视弱翼的**站位/阵地选择**（例如把 320 格场地用满、
   让弱翼走一条与强翼不同的、更依赖水平拉开的线路 ——
   因为弱翼的**水平巡航与强翼相同（7–8，冲刺 14.5）**，
   差的是**竖直**，所以弱翼应当**几乎不用竖直机动**，改为
   纯水平 + 克盾冲刺的线路）。

> **最后一个想法值得下一轮检验**：既然两翼唯一的差别是**竖直**能力，
> 而弱翼的水平能力与强翼**完全相同**，
> 那么弱翼的正确策略可能是**放弃竖直闪避、改为最大化水平距离**，
> 而不是试图模仿强翼的竖直机动。
> 这与业主"只要横向移动速度一直保持"的说法**方向一致**。

### 167.12 ★ 终值更正：弱翼 **6/28**（非 5/26）；强翼 19/28 —— 两翼共同短板都在**低 DPS 段**

补齐 1750/2000 两点后的最终结果：

| 翅膀 | 总击杀 | **低 DPS（≤800）** | 高 DPS（≥900） | 失败点剩余血量 |
|---|---|---|---|---|
| **强翼** | **19/28 = 68%** | **9/16** | **10/12** | 3480 – 21642 |
| **弱翼** | **6/28 = 21%** | **0/16** | **6/12** | 1808 – 51366 |

**弱翼 2000 点击杀（2880 tick / 2 hits）；弱翼 1750 点死亡但 BOSS 仅剩 1808 血。**

### 167.13 ★ 两类失败的性质完全不同

**(A) 低 DPS 失败 = 耐力不足（被拖死）**
* 弱翼低 DPS 段 **0/16**，且失败时 BOSS 常剩 20%–66%（如 d300 剩 51366）。
* 同 DPS 下强翼能活 15938 tick 并击杀，弱翼 5839 tick 就死（§167.9）。
* **这是"活不到 BOSS 死"**，不是"躲不过某一招"。

**(B) 高 DPS 失败 = 差一口气（BOSS 残血）**
* 强翼 4 个失败点在 8000 血以内：**600→5096、625→3480、1100→7087、1125→4500**。
* 弱翼 5 个失败点在 8000 血以内：**1050→5338、1100→4344、1125→4997、1600→6516、1750→1808**。
* **这是"多活几十 tick 或再躲 1–2 次就能过"。**

**★ 两翼共同的短板都在低 DPS 段**（强翼 9/16、弱翼 0/16），
但强翼的低 DPS 段已接近验收，**弱翼的低 DPS 段是 0**。

**因此后续优先级应当是**：
1. **强翼**：只需补齐 7 个低 DPS 失败点 + 2 个高 DPS 失败点
   （全部在 3%–28% 残血区，"差一口气"）；
2. **弱翼低 DPS 段**：这是**耐力问题** —— 需要"活得更久"，
   而所有"改写 vertical"的手段已 13+ 次零和，
   故应考虑**改变弱翼的阵地/线路**（纯水平 + 克盾冲刺，见 §167.11 末段）,
   而不是继续调竖直参数。

---

## §168. 第 168 轮：受击来源**不是 BOSS 本体，而是弹幕 386（cthulhunado）**

### 168.1 ★ 伤害来源普查（伤判 `hurt-observations.jsonl` 的 `reason.declaredProjectileType`）

强翼 28 点的全部受击按来源分类：

| 来源 | 含义 | 占比 |
|---|---|---|
| `declaredProjectileType: 386` | **cthulhunado 龙卷**（150×42，`timeLeft 840`） | **约一半** |
| `declaredProjectileType: 384` | **sharknado 龙卷**（150×42，`timeLeft 540`） | 少数 |
| 无来源（`sourceOtherIndex` 空） | BOSS 本体接触 | 约 1/3 |

**反编译证据**（`Projectile.cs:4671-4704`、`29902-30016`）：

* **384**：`width 150, height 42, hostile, penetrate -1, aiStyle 64, timeLeft 540`
* **386**：`width 150, height 42, hostile, penetrate -1, aiStyle 64, timeLeft 840`
* `aiStyle 64` 的龙卷每 tick 生成一个自身副本并把 `ai[1]` 减 1，
  于是同一场战斗里**同时存在 15 个世代**（`scale` 从 0.4 到 1.5），
  实测**同 tick 最多 50 个 hostileProjectiles**，
  386 的**实际碰撞盒最大到 `width 225, height 63`**。

**★ 龙卷的碰撞盒（225×63）比 BOSS 本体（85×71）宽 2.6 倍。**
玩家的碰撞半宽是 10，所以**横向距离阈值是 `225/2 + 10 = 122 px`**，
而 BOSS 本体只需 95 px。**龙卷是这场战斗里最大的单点碰撞威胁。**

### 168.2 ★ 受击瞬间的几何：玩家被**垂直墙**包夹

实测（s625，tick 6055 的受击帧）：玩家 300×200 范围内有 **7 个 386**，
**左侧 3 个、右侧 4 个、上方 3 个、下方 4 个 —— 四面被围。**

tick 6399 的受击帧：300×200 内 **10 个 386，全部在左侧（10/0）**。

**龙卷群的形状是"垂直于飞行方向的墙"**：
tick 6380 实测 25 个 386 的 `x` 跨度仅 **148 px**，而 `y` 跨度 **909 px**
（25 × 63 ≈ 1575，即有重叠 ⇒ **连续无缝隙的竖直墙**），
且**整墙 `vx` 恒为 0**（不水平移动），只缓慢上升。

**因此**：
* 水平方向上是**一堵 900+ px 高的连续墙**，靠水平跑**穿不过去**；
* 但墙**只占 150 px 宽**，**墙的另一侧是安全的**；
* 玩家实测在 `x 2586..2679` 之间被**来回推挤**（受击帧位置反复横跳），
  **这正是"被墙推着走"的特征** —— 与业主"要保持水平速度"的说法方向一致。

### 168.3 与既有结论的关系（重要，避免重复劳动）

* §166.1 的"Boss 悬停跟随玩家，升高自己无效"**仍然成立**，
  但**它针对的是 BOSS 本体**；龙卷是**独立**的威胁，不受该结论约束。
* 所有既有的"竖直机动"旋钮都是针对 BOSS 本体的逃逸方向，
  **从未针对龙卷**。这解释了为什么 13+ 次 `vertical` 改写全部零和：
  **它们优化的是次要威胁。**
* **这是一个全新的、未被开采的干预维度**：如果主要受击来自龙卷墙，
  那么正确策略是**在墙来临时沿水平方向躲开墙，而不是沿法线躲 BOSS**。

### 168.4 ★ 失败判据的最终量化（本轮最重要的结论）

强翼 28 点的"总伤害 / 血量池"对比（池 = 480）：

| 组 | 总伤害范围 | 占池 |
|---|---|---|
| **击杀组** | 0 – 864 | 0% – **180%** |
| **死亡组** | 179 – 496 | 37% – **103%** |

**总伤害完全不分离！** 击杀组里有 180% 的（s300，6 hits，靠长时间战斗的回复扛过），
死亡组里有仅 37% 的（s475，1 hit / 179）。

**唯一的分离量仍然是总接触数：**
* **全部 9 个死亡点都是 4–5 hits；所有 ≤3 hits 的点全部击杀。**
* s300（6 hits）能击杀，是因为战斗长达 **15938 tick**，回复量足以扛过 864 点伤害。

**因此验收的精确形式是**：**把每个 DPS 点的总接触数压到 ≤3**。
而由于**接触主要来自龙卷**，正确做法是**针对龙卷的规避**，不是继续调竖直逃逸。

### 168.5 下一轮的具体方向（可执行）

1. **把"威胁选择"从 BOSS 改为龙卷**：
   当 `hostileProjectiles` 中存在 386/384 且其碰撞盒即将与玩家相交时，
   用**水平**指令远离**墙**（而不是远离 BOSS）。
   这与业主"要保持水平速度"和"泡泡/龙卷基本没威胁、只要横向移动"的说法一致。
2. **判据**：
   * 强翼 9 个失败点（475/500/525/550/600/625/700/1100/1125）**总接触降到 ≤3**；
   * 强翼 28 点 **28/28 击杀**；
   * 弱翼低 DPS 段（0/16）至少开始出现击杀。
3. **注意**：这**不是**重试 `CHARGE_NORMAL_OWNER`（那是针对 BOSS 本体的法线躲避，已否证）。
   本方向是**针对弹幕**的规避，源码里**没有**对应实现（`hostileProjectiles`
   当前只被观测，未被用于决策）。

---

## §169. 第 169 轮：**龙卷规避层已实现，且实测净收益为零** —— 但诊断找到了真正的病灶

### 169.1 实现（`CHAITE_TORNADO_ESCAPE`，默认关闭，惰性已验证）

* 新文件 `src/Chaite.Core/TornadoEscape.cs`：读取 `snapshot.Threats` 中类型 384/386 的条目，
  用 `225×63` 的碰撞盒（含玩家半宽/半高）做预测，沿 **水平** 轴选择"龙卷更少的一侧"。
* 接入点：`CombatPlanner.PlanFormula`（`CombatPlanner.cs:866` 附近）。
* **惰性验证通过**：未设变量时强翼 625 逐字节复现 **7689/4/3480**（两轮验证）。
* **★ 接入点踩坑（重要，供后续参考）**：第一次接在了 `CombatPlanner.cs:743`
  的通用公式块里，结果**完全无效果**——因为存在**两条**公式路径：
  通用块（`CombatPlanner.cs:711-729`）与**鱼鲨专用**的 `PlanFormula`
  （`CombatPlanner.cs:757-877`，在 `:866` 直接 `plan.Horizontal = script.Horizontal` 后 `return`）。
  **实际走的是后者。** 正确接入点是 `:866` 之后。

### 169.2 ★ 实测：净收益为零（又是一次零和）

强翼 9 个失败点，开启龙卷规避前后：

| dps | 基线 (ticks/hits/剩血) | **开启后** | 变化 |
|---|---|---|---|
| 475 | 7838/4/20233 | 7851/4/20130 | 无 |
| 500 | 7304/5/21642 | 7306/5/21625 | 无 |
| 525 | 8247/4/10545 | 8247/4/10545 | **逐字节相同** |
| 550 | 7531/5/13926 | 7535/5/13889 | 无 |
| 600 | 7830/4/**5096** | 5925/**3**/24160 | hits −1，但**提前 1905 tick 死亡** ⇒ 变差 |
| 625 | 7689/4/3480 | 7689/4/3480 | **逐字节相同** |
| **700** | 5965/4/14721 | 7140/5/**978** | **剩血 14721 → 978（近 15 倍改善）** |
| 1100 | 4409/4/7087 | 4414/4/6996 | 无 |
| 1125 | 4461/4/4500 | 4461/4/4500 | **逐字节相同** |

**一击杀都没有。** 一个显著改善（700 点剩血 978，是本项目**最接近击杀**的一次），
一个显著退步（600 点提前死亡），其余无变化或可忽略。**净收益 = 0。**

### 169.3 ★★ 诊断找到真正的病灶：**规避只在与龙卷重叠的那一帧才触发**

在相位标签里临时植入"当前威胁列表中的龙卷数量"后实测（强翼 625）：

* 共 7690 帧，其中 **5195 帧的威胁列表里存在龙卷**（约 68%）；
* **规避层只触发了 35 帧**（占"有龙卷"帧的 **0.67%**）。

**原因**：`HorizontalEscape` 的门是"龙卷碰撞盒**当前已与玩家重叠**"（`Overlaps`）。
而**重叠的那一帧，受击已经注定**（引擎的伤害判定发生在同一 tick 的更新里），
所以这个门**永远是"太迟"**。它只能在"已经压在身上"时试图挣脱，
而不是在龙卷**接近之前**就换边。

**这解释了为什么净收益为零**：35 帧的干预无法改变 4–5 次接触的结局。

### 169.4 结论与下一轮方向（明确、可执行）

**不要重试**："重叠才触发"的龙卷规避（本轮已否证：门太迟）。
**要实现的是"提前换边"**：

1. **把门从"当前重叠"改为"预测未来 N tick 内重叠"**：
   用 `Position + Velocity * k`（k = 1..Horizon）判断，
   并在**龙卷墙到达之前**就朝龙卷较少的一侧移动。
   本轮的 `Danger()` 已经在做加权预测，**但 `Overlaps()` 的硬门把它短路了** ——
   只需**删掉那个硬门**，让预测本身成为触发条件。
2. **同时把"墙"的判据显式化**：§168.2 已测得墙是"x 跨度 ~150、y 跨度 ~900、`vx=0`"，
   所以正确的问题不是"躲开最近的一个龙卷"，而是
   **"我在这堵墙的哪一侧、哪一侧的龙卷更少"** —— 即**横向**选择，
   与本轮的水平轴决定一致，但触发时机要提前。
3. **判据**：强翼 9 个失败点总接触 ≤3；强翼 28/28；
   先看 625/700（625 目前逐字节相同说明门从未开过，700 最接近击杀）。

**保留的实现**：`TornadoEscape.cs` 与接入点**保留在源码中且默认关闭**，
注释已完整记录本轮的全部实测数据与"门太迟"的根因，
以便下一轮直接修改触发条件而不是重新推导。

---

## §169.5 ★ 完整实测（门移除后）：**中段大幅改善，但低 DPS 段被摧毁 —— 净收益为负**

移除"重叠才触发"的硬门后，规避层**真的生效了**（结果不再与基线逐字节相同）。

**强翼 9 个失败点，门移除后**：

| dps | 基线 | **门移除后** | 判定 |
|---|---|---|---|
| 475 | 死亡 7838/4（剩 20233） | **KILL 10390/1** | ✓ 转击杀 |
| 500 | 死亡 7304/5 | 死亡 8508/5（剩 11595） | ✗ 仍死 |
| 525 | 死亡 8247/4 | 死亡 8246/4（剩 10554） | ✗ 仍死 |
| 550 | 死亡 7531/5 | 死亡 8004/5（剩 9590） | ✗ 仍死 |
| 600 | 死亡 7830/4（剩 5096） | **KILL 8341/2** | ✓ 转击杀 |
| 625 | 死亡 7689/4（剩 3480） | **KILL 8026/2** | ✓ 转击杀 |
| 700 | 死亡 5965/4（剩 14721） | **KILL 7201/4** | ✓ 转击杀 |
| 1100 | 死亡 4409/4（剩 7087） | **KILL 4701/2** | ✓ 转击杀 |
| 1125 | 死亡 4461/4（剩 4500） | **KILL 4701/1** | ✓ 转击杀 |

**9 个失败点中 6 个转为击杀** —— 这是本项目**单个改动最大的正向效果**。

**但它同时摧毁了低 DPS 段原本的击杀点**：

| dps | 基线 | 门移除后 | 变化 |
|---|---|---|---|
| **300** | **KILL 15938/6** | **死亡 11281/5**（剩 24291，且提前 4657 tick） | **由击杀变死亡** |
| 400 | KILL 12240/5 | 死亡 11167/**7**（剩 7069） | **由击杀变死亡** |
| 450 | KILL 10937/4 | 死亡 10538/**6**（剩 2989） | **由击杀变死亡** |
| 575 | KILL 8674/4 | KILL 8676/4 | 保持 |

**再加边界（latch 40 tick，避免每帧改写）后仍然如此**：
s300 → 死亡 10813/5（剩 26631）、s400 → 死亡 10215/6、s450 → 死亡 9675/5、
s475 → 死亡 7983/4、s600 → 死亡 5849/3（剩 24920）。

**★ 结论：龙卷规避层净收益为负，已完全回退。**

### 169.6 为什么会这样（机制归因）

* 龙卷在 **5195 / 7690 帧**（68%）都存在于威胁列表中。
  任何以"存在龙卷"为条件的水平覆写都会**改写大部分战斗**。
* **低 DPS 段（300–450）是靠"长时间战斗 + 回复"扛过去的**
  （§168.4：s300 以 6 次接触、864 点伤害、180% 血池存活 15938 tick）。
  这类 run **最不能承受额外的位置扰动** —— 水平覆写把它们推向 BOSS 的冲锋线，
  受击数不变但**存活时间被砍掉 30%**，于是"回复扛过"的机制失效。
* **中段（475–700, 1100–1125）**原本是"差一口气"，扰动**恰好**把它们推过了线。
* **这是"零和"的第 15 次复现**，但形态不同：前 14 次是**改写纵向**，
  这次是**改写横向** —— 两者都改变受击的**分布**而非**总量**。

### 169.7 下一轮方向（修正）

本轮证明**"针对龙卷做水平规避"这条路是负收益**，因此：

1. **不要重试**：任何以"威胁列表中存在 384/386"为条件、逐帧或 latch 的
   水平覆写（已实测：中段 +6 击杀，低段 −3 击杀，净 −? 实际净为负）。
2. **若要保留中段的收益**，必须**加一个"只在低 DPS 段关闭"的门** ——
   但注意 §既有已多次证明"DPS 常量门"是脆弱的（且 `CHAITE_DASH_DELAY_DPS` 已因混沌被否证）。
   **若要做，唯一稳妥的形式是"按 BOSS 当前血量/阶段"而不按 DPS 分档**。
3. **更值得投入的方向**：既然低 DPS 段（300–450）**本来就是击杀**，
   而中段失败点只需要 1–2 次接触，
   **验收的缺口其实比此前认为的小得多**：
   强翼 28 点中 **19 击杀 / 9 失败**，且 9 个失败点里 6 个可以被本轮的机制救活
   （只是手段同时破坏了低段）。
   **下一轮应当寻找一种"只作用于中段失败点"的形式** ——
   例如把覆写**限制在 BOSS 血量处于特定区间**时（而不是全程）。

---

## §170. 第 170 轮：★ 找到真正的约束 —— **接触预算是按"冲锋周期数"计的，而非按 DPS**

### 170.1 ★★ 决定性的约束视图（本轮最重要的产出）

用 §168/169 已有的强翼 28 点数据，把"接触数"除以"冲锋周期数"（每周期 **28 tick**，
来自 §167 的 `life <= lifeMax*0.5` 与 28-tick 冲锋）：

| dps | ticks | hits | 周期数 | **hits/100周期** | 结果 |
|---|---|---|---|---|---|
| 300 | 15938 | 6 | 569 | 1.1 | KILL |
| 450 | 10937 | 4 | 391 | **1.0** | KILL |
| 675 | 7472 | 2 | 267 | 0.7 | KILL |
| **725** | 6997 | **2** | 250 | **0.8** | **KILL** |
| 1050 | 4997 | 3 | 178 | **1.7** | **KILL** |
| **1100** | 4409 | 4 | 157 | **2.5** | **DIED** |
| **1125** | 4461 | 4 | 159 | **2.5** | **DIED** |
| **1400** | 3884 | 1 | 139 | **0.7** | **KILL** |
| 2000 | 2881 | 0 | 103 | 0.0 | KILL |

**★ 存活判据 ≈ "接触密度 < ~1.5–2.5 / 100 周期"，而这个密度在 DPS 轴上并不单调**：

* 低 DPS（≤450）靠**极低的密度（1.0–1.1）**存活 —— 战斗虽长，但非常干净；
* 中高 DPS 的失败点（700/1100/1125）密度高达 **1.9–2.5**，是**全表最脏的**；
* 1050（1.7）却击杀 —— 所以 **1.5 不是硬阈值，而是"运气边界"**。

**这就解释了为什么 17 次改动全部零和**：
一场战斗只有 **150–570 个冲锋周期**，而预算是 **3 次接触**。
把 4 次死亡变成 3 次击杀，等价于把密度从 1.5 降到 1.1 ——
**即在 275 个周期里精确地少挨 1 次**，容错是"每 70 个周期一次"。
任何改变轨迹的扰动在此尺度上都是**噪声级**的：它既可能少一次，也可能多一次。

### 170.2 本轮实测：把触发器从"龙卷存在"改为**玩家血量**，再改为**BOSS 血量**，均为零和或负

**统一发现（跨 3 种触发器）**：规避层**只救"本来就会死"的 run，只毁"本来会活"的 run**。
强翼全网格（§169.5 + 本轮）：

| dps | 基线 | 无门（常开） | 20% 血门 | 35% 血门 | BOSS 40% | BOSS 25% | BOSS 15% |
|---|---|---|---|---|---|---|---|
| 300 | KILL 15938/6 | 死 11281/5 | **KILL 15938/6** | **KILL 15938/6** | 死 13223/6 | KILL 16130/**4** | 死 14730/7 |
| 400 | KILL 12240/5 | 死 11167/7 | **KILL 12240/5** | **KILL 12240/5** | KILL 12208/6 | KILL 12240/5 | KILL 12240/5 |
| 450 | KILL 10937/4 | 死 10538/6 | **KILL 10937/4** | **KILL 10937/4** | 死 10382/5 | 死 10382/5 | 死 10382/5 |
| 475 | 死 20233 | **KILL 10390/1** | 死 7930/4 | 死 7930/4 | 死 | 死 | 死 |
| 600 | 死 5096 | **KILL 8341/2** | 死 7830/4 | 死 6537/4 | **KILL 8341/2** | 死 7830/4 | — |
| 625 | 死 3480 | **KILL 8026/2** | 死 7689/4 | 死 7689/4 | **KILL 8026/2** | **KILL 8026/2** | — |
| 700 | 死 14721 | **KILL 7224/4** | 死 5965/4 | 死 5965/4 | **KILL 7224/4** | **KILL 7224/4** | — |

**净收益**：无门 **+4 / −3 = −1**；20% 血门 **+0 / −0**；35% 血门 **+0 / −0**；
BOSS 40% **+3 / −2 = +1**；BOSS 25% **+2 / −1 = +1**；BOSS 15% **−2**。

**结论：零和或负。** 最好的两个配置（BOSS 40%/25%）净 +1，
但代价是 450 由击杀变死亡（**用 1 个低 DPS 击杀换 2–3 个中段击杀**），
且对参数（40 vs 25 vs 15）**混沌敏感** —— 与 §已有的 `CHAITE_DASH_DELAY_DPS`
被否证的原因完全一致。

**★ 唯一真正的正向信号**：**20% 血门与 35% 血门下，所有基线击杀点逐字节不变**
（300 精确 15938/6，400 精确 12240/5，450 精确 10937/4）——
说明这个门**确实把扰动隔离在"已经必败"的区段**。
它只是**力度不足以把死亡变成击杀**（见 §170.1 的 1-in-70 容错）。
**这是下一轮可以直接复用的地基。**

### 170.3 下一轮方向（明确）

1. **保留"玩家血量门"作为隔离机制**（20–35%，已证明完全无副作用）。
2. **需要的是更强的干预，而非更好的门**：既然 1 次接触 = 胜负，
   而水平微调是噪声级，**下一轮应直接攻击"接触预算"本身**：
   * **让每次接触的伤害降一档**（把 4 次接触的伤害压到 3 次的水平）——
     注意 §已有"防御档不能作为生存杠杆"的否证，但那否证的是**换防具**，
     **不是"在被打中的那一帧降低伤害"**（如护盾/无敌帧/减伤增益的时机）。
   * **把 3 次接触的伤害分散到 4 次接触**（同样总伤害但每次更低，避免单次致死）。
3. **弱翼（6/28）需要单独的诊断**：弱翼在 **dps 300 只活到 5839 tick、8 次接触**，
   而强翼在同点是 **15938 tick、6 次接触击杀**。
   弱翼的失败**不是**"差一口气"，而是**耐力差 2.7 倍**，
   因此弱翼不应继续调龙卷规避，而应先解释 **`wingTimeMax` 130 vs 180**
   在长战斗中的累积劣势（§已验证弱点：弱翼攀升峰值 −9.91 vs 强翼 −16.52）。

---

## §171. 第 171 轮：**两档护甲齐备的诚实对比 —— 高防御既不更好也不更差，只是"挪动"了胜负点**

### 171.1 补齐了业主明确要求的第二档（此前从未按对等条件测过弱翼）

业主验收要求"分别给出蘑菇套（高防御）与黑曜石套（低容错）两档结果，并以黑曜石档为诚实口径"。
**在 §171 之前，弱翼只在黑曜石档被测过**。本轮用**完全相同的 route 与旋钮**补齐了弱翼的
完整蘑菇套网格（28 点，`tmp/weakshroom171.ps1`）。

**弱翼 黑曜石 vs 蘑菇套 逐点对比**（同 route、同旋钮）：

| dps | 黑曜石 | 蘑菇套 | |
|---|---|---|---|
| 900 | KILL 5726/5 | KILL 5732/4 | 一致 |
| **1000** | **KILL 5210/5** | **DIED 4964/6** | **黑曜石胜** |
| **1075** | **KILL 4885/2** | **DIED 4309/5** | **黑曜石胜** |
| **1100** | DIED 4554/5 | **KILL 4792/2** | **蘑菇套胜** |
| **1125** | DIED 4430/5 | **KILL 4695/1** | **蘑菇套胜** |
| 1150 | KILL 4598/2 | KILL 4604/3 | 一致 |
| 1200 | KILL 4432/2 | KILL 4433/2 | 一致 |
| **1400** | DIED 3359/4 | **KILL 3880/3** | **蘑菇套胜** |
| **1600** | DIED 3219/4 | **KILL 3460/3** | **蘑菇套胜** |
| 1750 | DIED 3153/5 | DIED 3146/5 | 一致（都差一口气） |
| 2000 | KILL 2880/2 | KILL 2880/1 | 一致 |

**总计：黑曜石 6/28，蘑菇套 8/28。**

**★ 关键结论：两档互不支配。** 22 个点结果相同，4 个点蘑菇套胜，2 个点黑曜石胜；
**两档的击杀区间是交错的**（黑曜石拿到 1000/1075，蘑菇套拿到 1100/1125/1400/1600）。
**高防御并没有系统性优势** —— 它改变了受击**何时**把血打空，从而改变了**哪些 DPS 恰好过线**。

这与 §已有的"防御档不能作为生存杠杆"完全一致，并首次给出了**完整的对称证据**。
**同时证实了 §170.1 的接触预算论**：蘑菇套每击伤害更低，但战斗更长、周期更多，
所以**接触数反而上升**（弱翼 300：黑曜石 8 次接触 / 5839 tick → 蘑菇套 **13 次接触** / 11125 tick）。
"更耐打"不等于"更安全"。

### 171.2 ★ 修掉一个会误导后续分析的探针缺陷

`result.json` 的 `equipment.lifeMax` 一直是 **100**，而实测血池是 **480**。
根因：`GameProbe.cs:3263` 在**装备装配时刻**读取 `player.statLifeMax2`，
而场景的 `MaxLife` 是在**之后**才写入的，所以该字段记录的是装配前的默认值。

**已修复**：现在同时输出 `statLifeMax`、`lifeMax` 与 `effectiveLifeMax`（取两者较大）
并说明该值是装配时刻读数。实测确认**零行为影响**（强翼 625 仍逐字节为 `7689/4/3480`）。
**★ 记录给后续轮次**：分析伤害预算时**不要**用 `equipment.lifeMax`；
以 `hurt-observations.jsonl` 的 `player.lifeBefore` 为准（实测血池 **480 = 400 + 80**）。

### 171.3 ★ 弱翼失败结构的定量确认（为下一轮定向）

逐点提取弱翼的**死亡瞬间**（`hurt-observations` 中 `lifeAfter == 0`）：

* 弱翼 300 的第 8 次受击发生在 **tick 5481**（life 116 → 0，单次 158 伤害）；
  该 run 的 runTicks 5839 是**死后又跑了 358 tick** 的产物。
* **弱翼每次死亡都是"血先被打空"**，与强翼一致；没有出现"未受击却失败"的情况。
* 弱翼全部 28 点中 **最高接触数 13（蘑菇套 300）、最低 1**；
  **低段（300–800）在 9 点中全部死亡**，接触数 **6–9**，而强翼同段是 **2–6**。

**这就是弱翼的真正缺口：不是"差一口气"，而是接触数整体高 2–3 倍**，
其根源是 §已有的垂直机动劣势（攀升峰值 **−9.91 vs 强翼 −16.52**，
`wingTimeMax` **130 vs 180**），使"按锁定方向斜向闪避"的位移量只有强翼的约 60%。

### 171.4 下一轮方向

1. **验收结论（本轮口径）**：
   * 强翼 黑曜石 **19/28**；弱翼 黑曜石 **6/28**、蘑菇套 **8/28**。
   * **两档都必须报**，且**黑曜石为诚实口径**。
2. **强翼低段/中段**：§170.1 已证明预算极紧（1/70 容错），
   下一轮应攻击**每次接触的伤害**而非轨迹（见 §170.3）。
3. **弱翼**：不要再调延迟类旋钮（§已有 7 个相关旋钮全部被否证）。
   应针对**垂直位移量**本身：既然弱翼攀升速率只有强翼的 60%，
   而水平速度两翼相同（巡航 7–8、冲刺峰值 14.5），
   **弱翼的闪避应由"斜上/斜下"改为"以水平位移为主 + 克盾冲刺补足"**——
   这是唯一尚未按翼种分化、且与本轮定量劣势直接对应的方向。
