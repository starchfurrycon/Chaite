# 动作空间改造：加入"下"方向（四方向机动）

## 为什么必须改（用户指正，2026-09-21）

我此前把动作空间描述为"12 个动作与键盘同构，但没有模拟量"。**这个描述是错的。**

真正的缺失是**方向不完整**：现在的 `direction ∈ {-1, 0, 1}` **只有水平**，
而"按住下键"在泰拉瑞亚里是一个**独立的、改变机动模式**的输入：

| 载具/增益 | 按住下键的行为 |
|---|---|
| 翅膀（各 `*-wing` 配装） | 收翅下坠，而非滑翔 |
| 缓落药水 | 下落速度被改变 |
| 信任辣椒 | 坐骑的下落与悬停行为不同 |

这些配装里既有坐骑也有翅膀配装 —— 也就是说**大部分配装的核心机动都依赖下键**，
而策略**根本无法表达它**。这足以单独解释"看得见威胁、有判断、却躲不掉"。

## 另外一个必须纠正的判断（用户指正）

我曾把"高 DPS 少受击"当成突破。**这是错的**：DPS 高只是 BOSS 存在时间短、曝光时间短，
不是策略学会了规避。一个真正无伤的模型**无论战斗持续多久都不会被击中**。
因此：

* 固定高 DPS **不是**解，只是把问题缩小；
* 验收必须**在完整 DPS 区间（600–1200）上成立**，而不是在高 DPS 端成立；
* 今后任何"少受击"的结论都必须**同时报告战斗时长**，否则无法区分"躲得好"与"打得快"。

## 改动清单

### 1. 动作编码（`training/chaite_env.py`）

现状：`ACTION_COUNT = 12`，解码为

```python
direction = (-1, 0, 1)[action // 4]
jump      = bool((action % 4) // 2)
dash      = bool(action % 2)
```

改为 24 个动作（3 方向 × 上 × 下 × 冲刺）：

```python
ACTION_COUNT = 24
direction = (-1, 0, 1)[action // 8]
up        = bool((action % 8) // 4)
down      = bool((action % 4) // 2)
dash      = bool(action % 2)
```

动作文件格式由 `tick,direction,jump,dash` 改为 `tick,direction,up,down,dash`
（`write_action(tick, direction, up, down, dash)`，L204）。

> 注意：`up` 与 `down` 要**允许同时为真**还是**互斥**，取决于原版行为。
> 建议先允许独立（原版里 `controlUp` 与 `controlDown` 是两个独立字段，
> L267 已确认探针侧两者都存在），由策略自行学习。

### 2. 探针侧（`tools/GameProbe.cs`）

* 探针已经把 `controlUp` / `controlDown` 作为**独立字段**存在（L267、L275、L686-687、
  L807、L811），并且已在状态采样里回传 `"up"` / `"down"`（L908）—— 即**读取侧已具备**。
* **已定位（2026-09-21）**：动作行的读取只有一处 `ReadBridgeActionTick()`（L4987），
  而它**只解析逗号前的 tick**（L5000-5004），direction/jump/dash 并不在这里读。
  它在 tick 循环里被调用一次（L2193），用途是**锁步等待**（L2197 的
  `ticks - actionTick <= BridgeLagLimit`）。
  ⇒ 所以"动作如何变成玩家输入"的应用点**不在这两处**，下一步应查
  `BeforeUpdate()`（L3656）与 L4089-4094 一带（那里有
  `player.controlLeft=player.controlRight=player.controlUp=player.controlDown=false;`
  与 `player.controlJump=MotionJumpRequested();`，是典型的"每 tick 重建输入"位置）。
* 需要改的是**动作应用侧**：新增的 `down` 字段要赋给 `player.controlDown`。
* 已有 `observedControlDown` / `PlayerControlDown` 可用于验证新输入确实生效
  （这是"改动须用隔离探针证明分支被执行"的现成抓手）。

### 3. 检查点不兼容（重要）

动作头形状变了（12 → 24），旧检查点**无法直接续训**。两条路：

* **推荐**：写一个 `warm_start_action.py`，把旧策略头的前 12 个输出列原样搬到新头，
  新增的 12 列置零（与 `warm_start_obs.py` 同一手法，已被证明可做到**逐位保真**）。
  这样既保留 6–7M 步的既有能力，又让新动作从"从不使用"开始被探索。
* 备选：从零重训（约 20 小时/配装，代价高）。

### 4. 验证要求（沿用项目纪律）

* 动作分支是否真的被执行：用 `observedControlDown` 回传值对照，改动前后必须不同；
* 动作空间改动的效果：**必须在同一 DPS 分布下**比较受击数，并**同时报告战斗时长**，
  避免重犯"把曝光时间当成规避能力"的错误；
* 观测侧无需改动，`OBS_DIM` 不变（`dd`/`eo`/`jt` 已在基础特征里）。

## 优先级

1. 精确定位并改探针的动作解析与应用（`controlDown`）
2. 改 `chaite_env.py` 的动作编码与 `write_action`
3. 写 `warm_start_action.py`（12 → 24，逐位保真验证）
4. 全程在**完整 600–1200 DPS 区间**上评估，不用固定高 DPS
