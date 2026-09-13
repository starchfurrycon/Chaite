# 当前开发接续（2026-09-13）

最新用户要求覆盖旧的自动召唤目标：F8 在 Boss 出现前进入被动监视，玩家自行召唤，
单个猪鲨／光女出现后才接管；F9 取消监视或战斗。自动钓鱼失败不再构成开发阻塞。
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

夜间光女专家 seed 20260913：artifacts/game-probe-monitor-empress-20260913-a。
同样 120 帧监视、240 帧接管、120 个被动等待帧；1200 帧诊断受击 1 次，未击杀，
Boss 剩余 85535 血。两条入口都已取得实际原版证据，不能再说光女因钓鱼无法测试。

下一步：按原生 AI 和教程重做各路线的真正闭环，补齐坐骑驾驶、天气、冲刺来源；
当前脚本仍非常粗糙，缺少场地边界／飞行恢复闭环。SelectMonitorRoute 仍只读取
克盾身份且天气传 false；雨天虾松露、忍者大师、水晶刺客未完整接通。不要把路线
枚举存在称为路线已实现。武器支持仍受输出目录限制，0 DPS 阈值不等于所有武器适配。
保留所有 Chaite 源码与失败证据，尚不可清理项目产品或宣布完成。
