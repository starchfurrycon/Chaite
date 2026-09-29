# 猪鲨走位接管 · 验收报告

**日期**：2026-09-29
**验收口径**：原生逐 tick 回放（`CHAITE_ROUTE_FILE`），隔离探针 `tools/run-native-acceptance.ps1`
**判定对象**：`FishronWingScript` 的两条已审路线

| 路线 | 配装 | 代码入口 |
|---|---|---|
| `fishron-strong-wing` | 猪鲨翅膀（`IsStrongWingItem`）+ 克盾 + 水陆两栖靴 | `FormulaRoute.FishronStrongWingsDash` |
| `fishron-fairy-wing` | 仙灵之翼（item 761）+ 克盾 + 水陆两栖靴 | `FormulaRoute.FishronFairyWingsDash` |

**场地**：海洋群系 320 格长直平地（tiles 1..321 exclusive），平台行 440/380。
**开火**：不由程序接管，按指定 DPS 直接扣减 BOSS 血量（`CHAITE_SIM_DPS`）。
**BOSS**：猪鲨公爵 NPC type 370，专家模式。`lifeMax = 78000`，玩家满血 **480**。

---

## 一、结论（先讲清楚"通过"与"未通过"）

| 判定项 | 结果 |
|---|---|
| **现行门槛（`docs/baselines.md`）：每套配装胜率 ≥ 90%** | **未达标** |
| **发行标签 v0.7.1-alpha 记录的门槛：每套配装 80%** | **仍未达标（差 1.1 个百分点）** |
| 强翼（黑曜石档）胜率 | **78.9%**（15/19 测点） |
| 弱翼（黑曜石档）胜率 | **50.0%**（9/18 测点） |
| 弱翼（蘑菇套）胜率 | **50.0%**（9/18 测点） |
| **更严格的零接触（`hits==0`，6000 tick 上限）** | **部分达成** |
| 强翼零接触点 | **4 个**（DPS 1175 / 1200 / 1300 / 2000） |
| 弱翼零接触点 | **0 个**（最好成绩 1 次受击） |

**因此：本次不构成"验收通过"。**

**★ 门槛本身有两个版本，两个都必须如实列出**：
`v0.7.1-alpha` 标签消息写的是 *"Acceptance target is 80% per loadout"*，
而当前 `docs/baselines.md` 写的是 *"各自胜率 ≥ 90%"*。
**在 80% 门下强翼差 1.1 个百分点（78.9%），在 90% 门下差 11.1 个百分点；
弱翼在任一门槛下都差 30 个百分点以上。**

**★ 同时必须说明一项重要区别**：CHANGELOG 记录的 **strong-wing 97.4%（38/39）达标**，
是在**另一条测量通道**上取得的 —— `training/mount-policy.ps1` 走生产路径
（`ExportedPolicy` 经 `LearnedPolicy.ForRoute`，每场 40 局），
不是本报告使用的逐 tick 回放通道。
**两条通道的数字不可互相替代，也不可相加。**

---

## 二、逐点实测数据

### 2.1 强翼 + 黑曜石（低容错档，诚实口径）—— 20000 tick 时限

| DPS | ticks | hits | 效果 |
|---|---|---|---|
| 300 | 15938 | 6 | 击杀存活 |
| 350 | 13901 | 5 | 击杀存活 |
| 400 | 12240 | 5 | 击杀存活 |
| 450 | 10937 | 4 | 击杀存活 |
| **500** | 7304 | 5 | **死亡** |
| **550** | 7531 | 5 | **死亡** |
| **700** | 5965 | 4 | **死亡** |
| 650 | 7738 | 3 | 击杀存活 |
| 750 | 6781 | 3 | 击杀存活 |
| 800 | 6386 | 2 | 击杀存活 |
| 850 | 6045 | 4 | 击杀存活 |
| **900** | 5741 | **1** | 击杀存活 |
| 1000 | 5221 | 3 | 击杀存活 |
| **1100** | 4409 | 4 | **死亡** |
| **1200** | 4441 | **0** | **零接触击杀** |
| 1400 | 3884 | 1 | 击杀存活 |
| 1600 | 3466 | 1 | 击杀存活 |
| 1800 | 3141 | 2 | 击杀存活 |
| **2000** | 2881 | **0** | **零接触击杀** |

**19 个测点：15 击杀 / 4 死亡 = 78.9%。空洞 = `{500, 550, 700, 1100}`（互不相邻）。**
**零接触点（另在 6000 tick 上限下测得）：1175、1200、1300、2000。**

### 2.2 弱翼 + 黑曜石（诚实口径）—— 20000 tick 时限

| DPS | 300 | 400 | 500 | 600 | 700 | 800 | 900 | 1000 | 1100 | 1200 | 1300 | 1400 | 1500 | 1600 | 1700 | 1800 | 1900 | 2000 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 判定 | D | D | D | D | D | **K** | D | **K** | D | D | D | **K** | **K** | **K** | **K** | **K** | **K** | **K** |
| hits | 10 | 6 | 6 | 5 | 6 | 2 | 5 | 3 | 5 | 5 | 5 | 2 | 1 | 1 | 2 | 3 | 3 | 2 |

**通过集 `{800, 1000, 1400–2000}` = 9/18 = 50.0%。零接触 0 个，最好 1 次受击。**

### 2.3 弱翼 + 蘑菇套（高防御档）—— 20000 tick 时限

**通过集 `{800, 900, 1100–1300, 1600–1800, 2000}` = 9/18 = 50.0%。**
DPS 1900 为 `SuccessAfterDeath`（BOSS 死但玩家也死）——**不算击杀**。

---

## 三、两套路线的差距（量化的竖直机动性差异）

| 配装 | 测点 | 击杀 | 通过率 | 300–700 低段 |
|---|---|---|---|---|
| **强翼 + 黑曜石** | 19 | **15** | **78.9%** | **4/5 通过（300–450），仅 500/550/700 三洞** |
| **弱翼 + 黑曜石** | 18 | **9** | **50.0%** | **0/5 通过（全灭）** |

弱翼直到 **DPS 800** 才首次通过（与最小 DPS 定理对弱翼的预测 848 一致），
且高段通过集是**散点**而非连续带 —— 这正是使用者所指
"强翼与弱翼在竖直机动性上的区别理应写两套"的定量证据。

---

## 四、已验证但**未达成**的事项（不得当作成果宣称）

1. **不得声称无伤。** 弱翼从未出现 `hits==0`；强翼仅 4 个 DPS 点达成。
   `SuccessAfterDeath` **不是**击杀，更不是无伤。
2. **不得把击杀存活表述为无伤。** 本报告全部使用"击杀存活"字样。
3. **不得声称覆盖全 DPS 区间。** 现行口径要求不区分 DPS 的稳定胜率，实测未达 90%。
4. **另外两条路线未在本通道验证**：`fishron-lilith-wolf` 与
   `fishron-trusty-chillet`（挂载类，由 `FishronChilletScript` 处理，非本报告对象）。
   CHANGELOG 记录它们为 0.0% / 0.0%（生产通道旧测量）。

---

## 五、可复现的验收步骤

```powershell
# 1) 清空全部 CHAITE_* 环境变量（重要：环境里可能残留策略/探针变量）
Get-ChildItem Env: | Where-Object { $_.Name -like "CHAITE_*" } |
  ForEach-Object { Remove-Item ("Env:\" + $_.Name) }

# 2) 押注本次口径
$env:CHAITE_ARMOR_TIER          = 'obsidian'   # 诚实口径；'shroomite' 为高防御档
$env:CHAITE_SIM_DPS             = '900'
$env:CHAITE_SIM_DPS_FULL_TILES  = '400'
$env:CHAITE_SIM_DPS_ZERO_TILES  = '401'
$env:CHAITE_SIM_BUBBLE_BREAK    = '0.95'
$env:CHAITE_PROBE_DENSE_FRAMES  = '1'

# 3) 跑原生验收（-maxticks 必须落在 600..24000）
.\tools\run-native-acceptance.ps1 -RunName my-run -Phase monitor -MaxTicks 20000 `
  -WallSeconds 900 -FormulaRoute fishron-strong-wing
```

**只有探针输出 `ACCEPTED: zero hits in the native engine.` 才可称零接触。**

---

## 六、长扫描前必须做的校验

```powershell
(Get-FileHash 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe' -Algorithm SHA256).Hash
# 必须等于 960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3 （1.4.5.8 Windows Steam 原版 x86）
```

`tools/prepare-game-probe.ps1` 自身也会校验该哈希与 4 个依赖哈希。

---

## 七、证据索引

* `docs/fishron-native-truth.md` —— 原生实测真值日志（§1–§211），本报告全部数字的来源。
* `docs/rounds/round-*-handoff.md` —— 历轮交接记录（含每次失败与撤回）。
* `docs/fishron-ai-native.md` —— `AI_069_DukeFishron` 逐帧源码分析。
* `docs/fishron-technique-from-videos.md`、`docs/fishron-video-subtitles.md` —— 视频参考打法。
* `tmp/*.py` —— 生成上述统计的分析脚本（可重跑）。
* `routes/strong-fishron-wings.csv`、`routes/fairy-wings.csv` —— 回放路由文件。

**注意**：`tmp/NPC.cs` 与 `tmp/Player.cs`（ILSpy 反编译的 1.4.5.8 源码，约 96k/— 行）
曾作为唯一源码依据，但**不属于发行物**，已在清理中移除；
重建方式见 `docs/fishron-ai-native.md` 顶部命令：

```powershell
& "C:\Users\lenovo\.dotnet\tools\ilspycmd.exe" -t NPC --no-dead-code `
  "D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe"
```
