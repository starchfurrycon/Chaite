# 当前开发接续（2026-09-13）

最新用户要求覆盖旧的自动召唤目标：F8 在 Boss 出现前进入被动监视，玩家自行召唤，
单个猪鲨出现后才接管；F9 取消监视或战斗。自动钓鱼失败不再构成开发阻塞。
不能继续复述旧钓鱼失败；应推进原版 Boss 到来检测与公式战斗。

本次发现并修复：旧“监视”仍落入 PlanSurvival 自动切枪／走位；F8 仍依赖
FindBossStartPlan；仙灵翅膀被 ReadFunctionalEquipmentIdentity 误报为公式外物品；
公式控制先运行旧评分器，再覆盖输入。现在 Runtime 无自动召唤／生存规划调用，
独立 Monitoring 状态不拥有输入；PlanFormula 在旧评分器之前返回。

开始监视复用 man.wav，接管使用 try_minnie.wav；隔离副本没有梗音频文件，只验证
触发与缺失提示，不宣称音频实播成功。F9／死亡／世界或角色切换取消监视。
开启监视时记录各 Boss 可用路线，Boss 出现后重验配置；仍有许多路线未接通。

验证：730 个回归通过；界面六种尺寸／缩放烟测 Failures: 0，前台未改变。
原版监视诊断夹具 tools/GameProbe.cs 的 phase=monitor：F8 第 120 帧，原版 NPC
生成入口第 240 帧，无 AI 阶段和血量修改。结果明确标记 directSpawn=true、
evidenceKind=native-monitor-arrival-fixture、readinessEligible=false，不能冒充
完整客户端召唤和最终胜率验收。用 start-isolated-test.ps1 启动，绝不改为用户桌面。

猪鲨专家 seed 20260913：artifacts/game-probe-monitor-arrival-20260913-c。
monitorArmedTick=120，monitorCombatTick=240，monitorPassiveFrames=120。
1200 帧诊断 deaths=1，hits=7，剩余 Boss 血 75804：入口已跑通，战斗明显失败。
早前 a、b 的拒绝日志完整保留。失败不能删掉、改为胜利或作为环境外部阻塞。

下一步：按原生 AI 和教程重做各路线的真正闭环，补齐坐骑驾驶、天气、冲刺来源；
当前脚本仍非常粗糙，缺少场地边界／飞行恢复闭环。SelectMonitorRoute 仍只读取
克盾身份且天气传 false；忍者大师、水晶刺客未完整接通。不要把路线枚举存在称为
路线已实现。
保留所有 Chaite 源码与失败证据，尚不可清理项目产品或宣布完成。

## 翼类脚本迭代（2026-09-13 后续）

新增 FishronWingScript：仅用于猪鲨两条翼类路线，记忆入场跑道、横向绕行方向、
升降段和冲刺开始时的垂直方向。Boss 越过玩家时不逐帧翻转；不再在 Horizontal=0
时盲按克盾。它仍是实验原型，不是已审核的无伤路线；尚缺精确折返和克盾反冲时机。
现在主 PlanFormula 委托给该状态机，非翼类路线仍有待独立实现。

测试 731 项通过。两个新原版失败记录（expert，seed 20260913）：

- artifacts/game-probe-fishron-wing-circuit-a：仍是旧水中夹具，首次伤害 tick476，
  tick1416 FailedAfterDeath，7 次受击，剩余血75679。此前同水中旧脚本首次受击359。
- artifacts/game-probe-fishron-wing-circuit-dry-a：监视夹具改为干燥海边跑道，
  首次伤害 tick480，tick3033 FailedAfterDeath，9 次受击，Boss 剩余71445，伤害6555。
  不得把两种环境差异归功于脚本或报告胜率。两个桌面日志均证明未抢前台。

明确下一步：首次 dry 伤害发生在端部折返时，被 Boss 斜向冲刺撞到；应读第430..490
帧位置和 ai 而不是无变化重跑。脚本没有完成可靠克盾反冲。还需重做平台着陆补翼，
复活后重新建立跑道、所有坐骑路线。当前固定路径未通过任何胜率门禁。

GameProbe 的 monitor 现在不填旧钓鱼水池；旧 summon 测试仍保留水池。结果报告
的海岸地面边界已修正为1..400，并添加 oceanBasinFilled。dry-a 已准备后才改报告
字段，其旧 result.json 中80..1900的地面边界字段错误，实际场地按源代码1..400，
不要回写历史结果；下一次准备新夹具时会用修正的报告。

## 折返／克盾实验（本次继续）

四份独立测试目录全部保留；均为专家、干燥 monitor 夹具，先120帧F8监视，再240帧
原版生成入口出现猪鲨，测试上限3600帧。它们不是完整玩家召唤或胜率验收：

| 目录（artifacts/） | seed | 结束tick | 受击帧 | 死亡 | Boss剩余血 |
|---|---:|---:|---:|---:|---:|
| game-probe-fishron-turn-pass-a | 20260913 | 2680 | 8 | 1 | 70705 |
| game-probe-fishron-turn-brake-a | 20260913 | 1804 | 7 | 1 | 74064 |
| game-probe-fishron-shield-counter-a | 20260913 | 2497 | 8 | 1 | 72456 |
| game-probe-fishron-shield-counter-b | 20260914 | 2731 | 9 | 1 | 71012 |

等待Boss越过才折返的实验让首次受击从480推迟到725，但跑得太远；减速爬升实验
耗光翼时间并失去高度约束。两者未保留在当前默认脚本，原始夹具与日志仍可复查。
当前仅增加克盾反冲原型：迎面接近、预测4帧内接触且纵向对齐时发一次专用冲刺边沿，
同次Boss冲刺不重复。仍没解决首次480帧的斜撞，不能声称改善了胜率。

counter-b 增加只读 dashType/dashDelay/eocDash/eocHit/immuneTime/controlDash
观测。tick1725 请求冲刺，原生 eocDash=15、vx=14.5，证明原生启动，但不是命中免疫
证明。tick828抽样eocHit=0且immuneTime=0，抽样不足以证明命中猪鲨或连续无伤。
应补精确反弹事件／NPC身份观测及Boss斜冲对齐；不要把克盾起动当作公式反冲成功。
counter-b生产循环2129帧平均0.058ms、最大16.725ms（含首用/JIT，非端到端延迟）。
shield-diagonal-a 新原版测试（seed20260914）：克盾事件完整记录了 476/479、586/590、
875/876、1315/1318、1409/1413 等“启动 eocDash=15 -> 实际命中 NPC -> eocDash=9、
dashDelay=29、immuneTime=4”的序列，证明单次冲刺请求和原版反冲链路确实生效；
但该次仍死亡（2101帧，7次受击，Boss剩余73439，伤害4561）。这不是胜率证据。
斜向重叠条件已补入 FishronWingScript；下一步应减少泡泡／Sharkron 危险区，改善
跑道端部后的回程和输出路线，不能继续只重复同一种子。
回归732项通过；四次隔离桌面均无前台抢占。所有试验仍死亡，不可宣布目标完成。
