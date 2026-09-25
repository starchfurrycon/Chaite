# 拆特工作流与状态机（2026-09-18 梳理）

本文把「拆特从安装到打完一场」的全链路、程序的状态机、音频触发点、以及前端结构
一次性写清楚。所有条目都对着当前源码写，不写计划中的东西。

---

## 一、拆特是什么，边界在哪

拆特是面向 **Windows Steam 原版 Terraria 1.4.5.8 x86** 的实验性 Boss 战斗接管器。
它不是 tModLoader 模组，不处理入侵、月亮事件或复合 Boss。

**生产白名单只有猪鲨公爵（NPC type 370）**。其他 Boss 一律拒绝接管。

---

## 二、进程与装配

三个可执行体，职责互不重叠：

| 组件 | 形态 | 职责 |
|---|---|---|
| `Chaite.Manager.exe` | WinForms | 前端控制台：定位、校验、安装、恢复、开槽位 |
| `Chaite.Patcher.exe` | 命令行 | 安装/恢复的实际文件操作，也可被 Manager 调用 |
| `Chaite.Plugin.dll` + `Chaite.Core.dll` | 注入到 Terraria.exe | 运行时：监视、接管、规划、音频 |

安装是**可恢复的 IL 注入**：先校验文件版本 + 完整 SHA-256，把原版 `Terraria.exe`
备份到 `Terraria/Chaite/`，再原子替换。恢复时只有当前文件仍与清单里的 patched hash
一致才覆盖，避免误抹 Steam 更新。

### 五个注入点

1. 本地玩家更新入口（`Tick`）——每帧唯一一次规划。
2. 原版输入复制后（`ApplyPendingInput`）——回放已算好的输入。
3. 快捷栏处理后（`ApplyPendingSelection`）——恢复选槽/瞄准。
4. `HorizontalMovement` 前（`ValidatePendingMobility`）——只复核已评分的机动边沿。
5. Boss 掉落结算处（`OnNpcKilled`）——记录被击破的 Boss 身份。

后三个入口**不重新扫描威胁、不重新规划**。战斗热路径离线：不截图、不联网、
不跑大模型、不直接读写 `.plr`/`.wld`/地图文件。

---

## 三、全链路工作流

### 阶段 0：安装（游戏必须已退出）

```
Manager 启动
  → TerrariaLocator 自动定位 Steam 目录（失败可手动浏览）
  → 防抖 550ms 后 GetStatus(path)：校验版本 + SHA-256
  → InstallState.CleanSupported 才允许「安装拆特」
  → 确认框（默认取消）→ InstallationService.Install
      · 校验载荷完整（Plugin.dll / Core.dll / Audio/README.txt）
      · 备份原版 + 哈希复核
      · 写临时程序集 → 校验注入点 → File.Replace 原子替换
      · CopyUserDataPayload：config.json 与 README 只在缺失时写，用户 WAV/梗图永不覆盖
  → 写 Chaite/install.xml
```

`Chaite/` 目录结构（安装后）：

```
Terraria/Chaite/
  config.json              热键、规划、提示行为（升级不覆盖）
  chaite.log               运行日志（含帧耗时统计）
  install.xml              安装清单
  Terraria.exe.<hash>.backup
  Audio/                   音频槽位 + README.txt（README 每次安装刷新）
  Memes/                   梗图槽位 + README.txt（README 每次安装刷新）
```

### 阶段 1：游戏内监视（F8）

**前提**：Boss 尚未出现。战斗中按 F8 拒绝接管。

```
F8
  → HotkeyPoller.Poll 得到 ActivatePressed
  → ArmBossMonitor(player)
      · BuildObservation：此刻若有 Boss → 拒绝（"请在 Boss 出现前按 F8"）
      · 玩家已死 → 拒绝（no_slime_ang.wav）
      · SelectMonitorRoute(snapshot, 370) 算一次
      · 结果为 None → 拒绝（never_tried_this_loadout.wav）
      · CaptureAuthorizedSessionIdentity：锁定世界 token / UniqueId / WorldId /
        NetMode / 本地玩家下标
      → EncounterController.ArmMonitoring → SessionState.Monitoring
      → 音频 man.wav，聊天提示"MAN！监视已开启"
```

**监视期间玩家完全保留操作权**：拆特不切武器、不走位、不抛竿、不清怪。
玩家自己用松露虫钓猪鲨。

监视期间每 tick 走 `TryStartMonitoredEncounter`：

- 玩家死亡 → 取消监视（dead.wav）
- 无 Boss → 继续等
- Boss 身份不在白名单 → 取消监视（boss_too_hard_for_me.wav）
- 按 Boss 类型取监视时算好的路线，`PrepareForMonitoredFormulaEncounter` 复核配装
  - 机动不符 → 取消监视（never_tried_this_loadout.wav）
- 通过 → 记录授权 Boss 三元组（type/key/generation）→ `ActivatePreparedSession`

### 阶段 2：接管成立

```
ActivatePreparedSession
  → EncounterController.Activate
      · !StartAuthorized → RejectedNoEncounter（no_slime_ang.wav + 聊天"我没有史莱姆 ang 啊"）
      · RequirePreparation 或无 Boss → PreparingBoss
      · 否则 → EngagedAlive / EngagedDeadWaitingRespawn
      · 音频 try_minnie.wav（"来吧，试一下米妮"）
  → 重置 BossStart、计时统计
  → 聊天"接管开始；F9 可随时紧急终止"
```

**接管那一帧记录初始生命**，所以玩家在监视阶段自己打掉的伤害不计入战斗统计。

### 阶段 3：战斗循环（每 tick）

```
Tick(player, playerIndex)
  1. 帧前置空：_pendingInput / _frameApplied / _pendingPlayer
  2. EnsureInitialized（首次进入时建 log/config/facade/controller/planner/hotkeys/audio）
  3. 非本地玩家 → 若正接管同一 playerIndex 则 AbandonChangedSession
  4. 接管中且会话身份不匹配 → AbandonChangedSession（换世界/角色/连接）
  5. Poll 热键：F9 → 按当前状态走三条归还路径
  6. _pendingScopeRejection → RejectUnsupportedBoss
  7. IsInputBlocked（菜单/输入框）且未死 → 只校验授权链，不规划
  8. Monitoring → TryStartMonitoredEncounter；否则未接管 → 检查 F8
  9. BuildObservation（含 DrainKilledBosses）
 10. TryValidateAuthorizedBossScope：Boss 根消失/换根/换代 → 拒绝
 11. EncounterController.Update → HandleCue → 终态则 FinishTerminal
 12. !ApplyControls 或玩家已死 → ClearCombatControls 后返回
 13. BuildCombatSnapshot → CombatPlanner.PlanSupported
       · RequestControlReturn → 区分"白名单拒绝"与"安全条件丢失"两条归还路径
 14. ApplyPlan → 写原生输入
 15. finally：CapturePendingInput + 帧耗时统计
```

### 阶段 4：结束

| 终态 | 触发 | 音频 | 聊天 |
|---|---|---|---|
| `SuccessNoDeath` | 见过并击破全部 Boss，且未死 | mamba_out.wav | 无死亡完成战斗 |
| `SuccessAfterDeath` | 死过但最终击破 | failed_boss_design.wav | 经历死亡后完成战斗 |
| `FailedAfterDeath` | 死过且 Boss 已无法继续 | low_level_chaite.wav | 死亡后战斗已无法继续 |
| `EncounterInterrupted` | Boss 未确认击破即中断 | 无 | Boss 在未确认击破时中断 |
| `Cancelled` | F9 紧急终止 | 无 | 已紧急终止 |

`Finish` 统一做：清回放、恢复召唤物事务、写帧耗时、聊天、
`_terminalDelay = 1` 后回 `Idle`。

---

## 四、状态机全表

`SessionState`（`Chaite.Core.Models`）：

| 状态 | 含义 | 进入条件 | 离开 |
|---|---|---|---|
| `Idle` | 未做任何事 | 初始 / `ReturnToIdle` | F8 → `Monitoring` |
| `Monitoring` | 已武装，玩家保留操作 | `ArmMonitoring` 成功 | Boss 出现→接管；死亡/身份不符→`Idle`；F9→`Idle` |
| `Validating` | `Activate` 内的瞬时态 | `Activate` 开头 | 立即转下面之一 |
| `RejectedNoEncounter` | 终止：没有合法开战条件 | 武装失败 / `!StartAuthorized` | 终态 |
| `PreparingBoss` | 接管中但 Boss 还没到 | `RequirePreparation` 或无 Boss | `MarkSummonIssued`→`AwaitingBossSpawn`；Boss 到→`EngagedAlive` |
| `AwaitingBossSpawn` | 召唤已发出，等 Boss | `MarkSummonIssued` | 同 `PreparingBoss` 的到达分支 |
| `EngagedAlive` | 交战中且玩家存活 | Boss 在场且玩家存活 | 玩家死→`EngagedDeadWaitingRespawn`；Boss 消失→终态 |
| `EngagedDeadWaitingRespawn` | 玩家死了，等复活 | 玩家死亡 | 复活且 Boss 仍在→`EngagedAlive`；Boss 消失→终态 |
| `SuccessNoDeath` | 终止：零死亡击破 | Boss 全灭且未死 | 终态 |
| `SuccessAfterDeath` | 终止：死过但击破 | Boss 全灭且死过 | 终态 |
| `FailedAfterDeath` | 终止：死过且打不下去 | Boss 消失且死过且未全灭 | 终态 |
| `Cancelled` | 终止：F9 | `Cancel()` | 终态 |
| `EncounterInterrupted` | 终止：未确认击破 | Boss 消失且未全灭且未死 | 终态 |

`IsControlling` = `PreparingBoss | AwaitingBossSpawn | EngagedAlive | EngagedDeadWaitingRespawn`。
`IsSessionActive` = `IsControlling | Monitoring`。

**关键设计**：`Monitoring` 不拥有输入。取消监视只回 `Idle`，不改玩家当前武器。

### 死亡与复活

- 单机死亡通常使普通 Boss 立即消失 → 走 `FailedAfterDeath`。
- 多人下 Boss 仍在 → 复活后 `EngagedAlive`。
- 复活那一帧被**刻意保留为中性帧**：`Player.Update` 在 vanilla 重建装备派生的运动字段
  之前就看到 dead/life，这一帧不接受准入判断，避免用陈旧默认值误拒合法配装。

### 会话身份链

接管期间持续校验：世界对象引用、`UniqueId`、`WorldId`、`NetMode`、本地玩家下标。
任一变化即 `AbandonChangedSession`。Boss 侧另有一组：type / key / generation，
Boss 根消失后再出现的根**不能复用**会话（`_authorizedBossContinuityBroken`）。

---

## 五、音频触发表（完整）

槽位目录固定为 `Terraria/Chaite/Audio`，**不由 config.json 改写**。文件名固定：

| 触发点 | 槽位 | 台词 | 源码位置 |
|---|---|---|---|
| F8 武装成功 | `man.wav` | 科比「MAN」 | `AudioCue.MonitorArmed` |
| 接管成立 | `try_minnie.wav` | EZ「来吧，试一下米妮」 | `EncounterController.Activate` |
| 接管中受伤 | `man.wav` | 科比「MAN」 | `Update`，冷却 45 tick |
| 死亡 | `dead.wav` | EZ「亡了亡了」 | `Update` 死亡边沿 / 监视中死亡 |
| 零死亡完成 | `mamba_out.wav` | 科比「MANBA OUT」 | `SuccessNoDeath` |
| 死过但完成 | `failed_boss_design.wav` | EZ「设计失败的波斯」 | `SuccessAfterDeath` |
| 死亡后打不下去 | `low_level_chaite.wav` | EZ「低级的拆特」 | `FailedAfterDeath` |
| 无合法开战条件 | `no_slime_ang.wav` | EZ「我没有史莱姆 ang 啊」 | `RejectedNoEncounter` |
| Boss 不在白名单 | `boss_too_hard_for_me.wav` | 「这个波斯可是超囊的对我来说」 | `RejectUnsupportedBoss` |
| 配装不匹配 | `never_tried_this_loadout.wav` | 「从来没试过哦」 | 路线/机动复核失败 |

播放语义：

- WAV 在初始化时由**后台线程预载**；战斗帧不读文件、不解码。
- 预载完成前同一槽位最多保留**一次**待播请求，所以第一条提示不会因为抢跑而丢。
- 缺失文件只在聊天里提示一次，自动战斗继续。
- 项目**不生成、不下载、不变声、不分发**这些片段。

---

## 六、前端（Chaite.Manager）结构

```
MainForm
  Header     拆特 + 骷髅王徽章 + 星星炮梗语（随状态轮换）
  Body (滚动)
    01 连接游戏    路径输入 / 浏览 / 检查 / 状态 / 版本+SHA
    02 游戏内操作  F8 / F9 热键卡
    03 战斗准备    猪鲨召唤 / 已审核路线 / 战前准入 / 验收状态
    04 拆特语音席  音频槽位状态 + 打开音频 + 打开配置
    05 梗图槽位    槽位状态 + 打开梗图 + 缩略图预览
  Footer     F9 安全线 + 状态提示 + 梗语
```

设计约束（由 `UiSmokeTest` 强制）：

- 六种尺寸/缩放组合下无兄弟控件重叠、无标签截断、无水平滚动条。
- 文本对比度 ≥ 4.5。
- 主操作按钮不被裁切。
- 预览窗口用 `WS_EX_NOACTIVATE|TOOLWINDOW` 放在所有显示器之外，**绝不抢前台**。

梗语轮换**只在状态变化时推进**，不用计时器：冒烟测试会连续做两次布局并比对，
计时器在两次布局之间改文本会让测试变得不稳定。

---

## 七、闭环训练与验收的关系（这一节最容易搞错）

这是本项目最容易被误读的一处，必须写死：

- **验收要求的量**：整场战斗 `status=win` 且 `hits=0`。**这才是目标。**
- **训练器现在优化的量**（2026-09-18 改，见下）：排序为
  `noHitWins 高 → wins 高 → deaths 低 → hits 低 → cleanLoopShare 高
  → playerDamagePerK 容差内 → damage 高`。
  第一项就是验收口径本身，逐种子计数。
- **训练器过去优化的量**（已作废）：`Clean`，即单个**玩家**闭环
  （回到初始距离带 + 方位扇区）内一次都没被打到，且这个闭环的**数量**。

**这两者不是同一个量。** 一个策略可以拥有很多个干净闭环，
却在进入或退出时挨一下，甚至在闭环之间被打死。

### 为什么换掉旧目标（实测，不是口味）

旧排序是 `Clean 数量 → Hits → PlayerDamagePerK → Damage`，
**`win` 在整个排序里根本不存在**，只在最后的 damage tie-break 里间接体现。
把已记录的 9 个组合喂给旧排序，它把

- **`nh03`（猪鲨/高级翅膀）排第 5**：那是 9 个组合里**唯一赢过**的一条（胜 2）。

新排序把 `nh03` 排第 1。改动前后的排序不同，
这就是"分支确实被执行"的对照证据；`tools/test-training-objective.ps1`
把这条排序连同文件头的声明一起钉死，并已验证它在改动前的版本上会失败。

同时日志现在**先打验收口径再打闭环代理**：

```
fight verdict: no-hit wins 0/5 (wins 2, deaths 4, hits 38)
loop verdict: STABLE clean closed loops on 5 of 5 gate seeds ...
```

因为 `loop verdict` 那一行单独看会被读成战斗结论——这正是本项目
**三次**在闭环口径上出错的原因（`09376f2`、`185ccbf`，以及把 9 条
"STABLE clean closed loops" 报成"9 组合无伤"）。

当前实测结论（`CHAITE-NOHIT-VERIFY.txt`，45 场）：整场无伤 **0/9 组合、0/45 场**。
但该结论是在**错误的场地**上得到的，见 §七之二。

### 训练流水线（现状）

```
FormulaRouteCatalog.Select(370, 翅膀, 冲刺, 忍装, 蛙腿, 坐骑, 雨天)
  → FormulaRoute（坐骑 52/64/65 之外一律 None；坐骑 12 已于 2026-09-18 移出范围）
  → train-policy.ps1
      · 先量基准（同一 tag 通道，dense frames 一致）
      · 每轮 N 个扰动候选，逐个跑 wave
      · Test-Better 排序：noHitWins 高 → wins 高 → deaths 低 → hits 低
        → cleanLoopShare 高 → PlayerDamagePerK 容差内 → Damage
      · 全是 no-op 说明扰动幅度不对，按 ScaleGrowth 放大 scale
      · 结束总是对最终最优跑一次稳定性门，并**先**打 fight verdict
  → record-clean-loops.ps1：按 hurt-observations 统计干净闭环，出 STABLE/UNSTABLE
  → verify-nohit.ps1：按 result.json 统计整场 win + hits=0
```

### 七之二、场地更正（2026-09-18，实测）

使用者指出真实战斗场地是**一条长直平地**，并给出：猪鲨约 300 余格、
**起点贴近左端或右端**（战斗时识别后镜像）。

夹具当时与之不符，两处都错：

| 项 | 真实 | 旧夹具 | 现夹具 |
| --- | --- | --- | --- |
| 猪鲨起点 | 贴近一端 | **tile 200（正中）** | tile 21 或 379，`-startside left/right` |
| 平地上方 | 长直平地 | **y=460/420 两条贯穿全场的木平台** | 已删除 |

而且旧夹具**公布值与实际不符**：`platformLeft/platformRightExclusive` 写成 50/550，
实际造在 1–399。现在两边都从 `ArenaGroundLeft` / `ArenaGroundRightExclusive`
这**同一个表达式**取，不可能再分家；场地块还公布了 `startSide` 与 `playerStartTileX`。

**同参数对照（seed 2、expert、`fishron-strong-wing`、takeoverTick 120、
ticks 12000 / wallSeconds 600；两边同 config 哈希、都不加载策略文件）：**

| | 旧夹具 | 新夹具 |
| --- | --- | --- |
| 结局 | loss | **win** |
| 受击 | 8 | **4** |
| 死亡 | 1 | **0** |
| BOSS 伤害 | 35,593 | **78,000（满血击杀）** |
| ticks | 2,226 | 4,393 |

⇒ 场地一改，同一套基线从"打到 45.6% 就死"变成"满血击杀零死亡"。
**此前所有基于旧夹具的路线结论都要在新夹具上重测。**

### 七之三、冲刺时机原本不在搜索空间里（2026-09-18，代码 + 探针）

两个带冲刺的脚本（`FishronWingScript`、`FishronChilletScript`）
（2026-09-23 补注：`FishronChilletScript` 已随疾旋鼬路线整体删除；本节按当时事实保留）
原本都在**冲刺一就绪的瞬间**无条件置位"本回合已用掉冲刺"，而残差
`LearnedPolicy.Adjust` 在其**之后**才运行：

```
ChargeEscape(...)                      // 脚本：就绪 → dash = true; 已用掉 = true;
DecideMovement(...) → Adjust(...)      // 策略：只能把 dash 取反，但"已用掉"已经置位
```

⇒ 策略**只能取消**冲刺，**不能推迟**：一取消，本回合的冲刺就没了。
可达集合只有「一就绪就放」与「本回合不放」两点，"晚几 tick 再放"不在其中。
而实测的剩余受击正是需要后者——猪鲨二阶段那次本体接触，冲刺在距离 **270 px**
就按下，无敌帧在最近距离（26 px）之前耗尽。

**已改**：置位改到**真正发出**时，并加守卫，使策略在"未就绪"tick 上强行打开的冲刺位
（引擎会忽略）**不烧掉**本回合的合法冲刺。无策略与零权重策略下发出时机逐位不变。

**但只改这个还不够**，两个手工策略都没能改善战斗（`hits` 3 → 7 / 12）。残差结构本身是瓶颈：

- 冲刺头是**无记忆的翻转位**（`ArgMax(8,2)` 把脚本的位取反），表达不了"按住 N tick 再放"；
  它要么按住每一次提议，要么在脚本没提议的 tick 上强行打开——后者会在**巡航期**浪费掉冲刺，
  于是真正的充能回合反而在冷却。
- 38 个特征里**没有冲刺就绪／冷却状态**，策略无法知道"现在能不能放"。

⇒ 这两条已按下面的方式补上；补完之后"择时"才第一次可被搜索到。

### 七之三之二、把「择时」变成可搜索的（同轮实现）

两个改动，都只扩大可达集合、不动固定状态机：

1. **冲刺就绪度进特征**。向量由 38 维扩到 **40 维**，最后两项是
   `MobilitySnapshot.DashReady` 与 `CanDash`。这是策略能表达"等到就绪再放"的前提：
   在此之前它无法区分"冲刺被按住了"和"冲刺已经花掉了"。
   缺失的 mobility 快照读作"不可用"（fail-closed），不会谎报"可以放"。
2. **冲刺头由 2 路改为 3 路**（`HeadCount` 10 → 11）：
   class 0 = 不动、class 1 = **只按住**（脚本为 true 时压掉，为 false 时保持 false）、
   class 2 = **只强行打开**。旧的两路翻转把这两件事合成同一个动作，
   所以"想推迟一次冲刺"必然在巡航期先浪费掉它。

**代价与门禁**：维度变了 ⇒ 旧策略文件一律被加载器拒绝（这是 fail-closed，不是回归）。
`InputCount`/`HeadCount` 同时写在 C# 与 `tools/new-policy.ps1` 两处，属于本仓库反复踩到的
漂移类，所以新增契约测试 `PolicyLayoutMatchesTheToolThatWritesIt` 同时钉住两端，
并断言最后两项特征确实是冲刺标志、缺失快照不谎报可用。

**变异验证**（每个变异都编译通过，且对应测试确实失败）：把 latch 还原到"提议时置位"
⇒ `HeldDashIsNotSpentUntilItIsIssued` 失败；删掉 `&& dashProposed` 守卫
⇒ `ForcedDashDoesNotBurnTheChargesDash` 失败；把冲刺头改回 2 路
⇒ `DashHeadSeparatesHoldingFromForcing` 失败；把工具回退到 38/10
⇒ `PolicyLayoutMatchesTheToolThatWritesIt` 失败。

### 七之四、一个名字与含义不符的探针字段

`charge-observations.jsonl` 的 `dashUsedDuringCharge` 读的是 `p.controlDash`（输入位），
不是"本回合是否真的用掉冲刺"。距离门控那一场它对全部 34 个回合报 `false`，
而同一场的逐 tick 抽样行显示 `eocDash=15`（冲刺确实发出过）。
**在查清语义前，这个字段不得再当作"是否用了冲刺"的证据。**

策略文件格式：`chaite-policy` / 版本 / `40` 输入 / hidden 数 / 权重。
`LearnedPolicy` 是残差 MLP（40→16→11），只在 `CHAITE_POLICY_ROUTES` 列出的路线上生效。
冲刺头占 logit **8..10**（三路），其余三个头仍是 `0..2` 水平、`3..5` 垂直、`6..7` 跳跃。
环境变量：`CHAITE_POLICY_FILE`、`CHAITE_POLICY_ROUTES`、`CHAITE_TRACE_FILE`、
`CHAITE_DEFAULT_ACTION_MARGIN`、`CHAITE_PROBE_DENSE_FRAMES`。

---

## 八、两条硬性纪律

1. **一次训练/实测运行期间不得重编译**。探针副本是按当时的 DLL 准备的，
   中途换构建会让前后种子跑在不同代码上，整批数据作废。
2. **物理常量必须引擎内实测，不得推算**。已实测：重力 `dvy = +0.400 px/tick²`、
   松开跳跃后翅膀垂直减速度 ≈ 0.4、水平减速度 ≈ 0.37、引擎吞吐 ≈ 186 tick/s。
