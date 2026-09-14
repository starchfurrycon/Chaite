# 拆特固定公式路线

## 实现状态（2026-09-14）

下表是目标路线目录，不是已通过实战的支持清单。当前已完成战前身份接入和
固定脚本骨架：猪鲨翼类、史莱姆女士鞍、疾旋鼬/桃旋鼬，以及光女强翼、扫帚、
原生雨天虾松露均有独立入口；每条路线仍待隔离多种子实战验收。

当前脚本已进入独立 `PlanFormula` 分支，不再调用旧路线评分器。坐骑专用控制、
克盾/忍者大师/水晶刺客冲刺来源、蛙腿身份和原生雨天状态已接入路线选择。
固定配装并不自动证明脚本无伤，后续验收必须检查生产调用链和隔离实战证据。

猪鲨翼类原型现在使用有记忆的 `FishronWingScript`，锁定冲刺避让方向并尝试
升降／着陆循环，取消无方向的连续冲刺。干燥海边跑道专家 seed20260913 实测
仍死亡（9 次受击，Boss 剩余71445）；跑道端部折返和克盾反冲尚未完成。不得据此
宣称路线通关。详细证据与接续点见 [开发记忆](monitor-development-memory.md)。

原生时钟映射已加入回归：猪鲨使用 ai[2]/ai[3] 作为计时/序号，光女使用
ai[1]/ai[2]。不匹配 Boss 的路线、缺失或非整数观测必须拒绝。

生产接管只在 Boss 尚未出现、玩家准备召唤时进行一次路线选择。选择结果写入
`ControlPlan.FormulaRoute`，战斗中不重新选择、不拼接能力，也不接受中途 F8 接管。

| Boss | 路线 | 战前身份 |
|---|---|---|
| 猪鲨 | 仙灵/强翼 + 冲刺 | 蛙腿、翅膀、克盾/忍者大师/水晶刺客冲刺来源 |
| 猪鲨 | 史莱姆女士鞍 | MountID 50 |
| 猪鲨 | 可靠疾旋鼬/桃旋鼬 | MountID 64/65 |
| 光女 | 强翼 + 冲刺 | 蛙腿、猪鲨翼/同级强翼、冲刺来源 |
| 光女 | 扫帚 | MountID 23 |
| 光女 | 雨天虾松露 | MountID 12 且 `Main.raining` 原生为真 |

武器、弹药和非机动饰品不参与路线身份判断。路线身份通过后，脚本按原生 Boss
攻击状态执行固定输入；路线未通过时播放 `never_tried_this_loadout.wav`。

每条路线必须分别按难度和多个世界种子验证。没有 Terraria 原版二进制时，静态
构建和单元测试不能替代真实隔离战斗结果。

## 2026-09-14 隔离失败证据（未通过，不得宣称支持）

- 猪鲨 `FishronFairyWingsDash` / expert / seed 20260914：隔离 monitor 到达夹具，
  2400 tick 结束时仍存活但未击破，Boss 剩余 67509 / 78000，命中 6 次。
  证据目录：`artifacts/game-probe-formula-fishron-20260914-a/` 与
  `artifacts/formula-fishron-20260914-a-desktop/`。
- 光女 `EmpressStrongWingsDash` / night / expert / seed 20260914：死亡 1 次，
  Boss 剩余 73527 / 78000，命中 10 次。
  证据目录：`artifacts/game-probe-formula-empress-night-20260914-a/` 与
  `artifacts/formula-empress-night-20260914-a-desktop/`。
- 光女 `EmpressStrongWingsDash` / day / expert / seed 20260914：死亡 1 次，
  Boss 剩余 94130 / 78000，命中 1 次。
  证据目录：`artifacts/game-probe-formula-empress-day-20260914-a/` 与
  `artifacts/formula-empress-day-20260914-a-desktop/`。

这些失败按路线单独保留，不得用其他路线的总胜率抵销。

## 战前监视验收计划

按最新需求，F8 在 Boss 数组为空时进入独立 `Monitoring` 状态。监视中玩家自行
召唤，Runtime 不调用自动召唤或生存规划，不写入移动、瞄准、选槽或使用键。
监视开启复用 `man.wav`；确认单个目标 Boss 并通过当前配装检查后才播放
`try_minnie.wav` 并接管。监视死亡、切换世界／角色／连接或 F9 均取消监视。

- 猪鲨：玩家自行钓鱼；自动抛竿不再是产品要求或测试前置条件。
- 光女：玩家自行释放／击杀草蛉，可在非神圣区召唤；白天和夜间分开记录。
- `monitor` 隔离夹具在 F8 后 120 帧调用原版 NPC 生成入口，从初始 AI 开始运行，
  不设置 Boss 阶段或血量。结果标为 `native-monitor-arrival-fixture`，保留
  `directSpawn=true`、`readinessEligible=false`，不冒充玩家完整召唤或最终胜率验收。
- 2026-09-13 本地 `game-probe-monitor-arrival-20260913-c`：专家猪鲨，seed 20260913，
  第 120 帧监视，第 240 帧生成／接管；120 个等待帧无自动输入回放。1200 帧诊断
  中受击 7 次并死亡，Boss 剩余 75804 血。入口跑通，战斗失败，不能宣称无伤。

旧的 `p1-*` / `p2-*` 阶段夹具是在 Boss 已存在时注入原生字段，只能验证状态
解析和拒绝路径，不能计入公式路线胜率。每条路线的结果必须单独记录 seed、难度、
路线 ID、死亡数、完成 tick 和失败原因。
