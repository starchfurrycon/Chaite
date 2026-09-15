# 交接：光女 empress-broom 无伤训练（第 25 轮起的当前状态）

> 本文件是**当前权威状态的压缩版**，供上下文重置后直接接手，不必重新推导。

## 用户最新指示（覆盖旧验收口径）

> "关于验收和基线，直接以**无伤**为目标进行训练即可吧。"

因此：**训练目标与验收 = 无伤（零受击取胜）**。
原"40 条种子 95% Wilson 下界 ≥90%"与"5 条路线补基线"**降为参考信息**，不再作为主线工作量。

## 正在运行的东西

- **tag**：`empress-broom-nohit-v1`
- **场景/路线**：`empress-night` / `empress-broom` / PolicyRoute `EmpressBroom`
- **参数**：种群 16、每候选 4 种子、4 路并行、**120 代**、每 5 代在 40 条保留种子上验证
- **启动时刻**：2026-09-16 约 07:50（第 26 轮）
- **构建**：`artifacts/training/empress-broom-nohit-v1/build.txt` = `81D466CE66AB5CB0`
  （必须与 `src/Chaite.Core/bin/Release/net48/Chaite.Core.dll` 一致；不一致说明被污染）
- **旧训练 `empress-broom-v1` 已主动停止**（它优化的是旧适应度），其目录**保留不删**。

## 适应度（已改为无伤口径，并实测验证）

`tools/train-policy.ps1` 的 `Get-Fitness`：

| 结果 | 适应度 |
|---|---|
| **无伤胜利** | **1.0000**（唯一最大值） |
| 胜利，挨 h 下（h<25） | 0.5 + 0.5·(1 − h/25) |
| 胜利，挨 ≥25 下 | 0.5000（下限） |
| 失败 | 0.5·(damage/98000) − 0.5·(hits/14) |

性质：无伤唯一最大；胜利内挨打越多分越低（单调）；**任何胜利 ≥ 任何失败**
（唯一例外：胜利下限 0.5 与失败上限 0.5 在"0 受击且恰好 100% 伤害"角点上**相等**，
零测度、实测不出现）。

## 基线（新适应度，固定状态机，46 条唯一种子）

**均值 −0.0264**，中位 −0.0568，范围 −0.1278 … **0.7400**（种子 29，胜利但挨 13 下）。
**2 次胜利，0 次无伤。**

**这是最重要的一句话**：固定状态机在 46 条种子里**一次无伤都没拿到**，
所以无伤不是"再推一点"，而是要学出**它从未产生过的行为**。

## 证据在哪（已验证不会被覆盖）

- 训练曲线：`artifacts/training/empress-broom-nohit-v1/log.csv`
  （列：`gen,candidate,seeds,fitness,note`；note 含 `wins=`、`noHit=`、`hits=`）
- 验证：`artifacts/training/<tag>/eval-<tag>-gen<g>.json`
  （line 306 用 `-Tag "$Tag-gen$gen"`，**每代独立、不覆盖**）
- 逐条原始证据：`artifacts/game-probe-rt-empress-broom-nohit-v1-g<g>-*<seed>-*/result.json`
  ——**`noHit` 可直接从这里算**（`win && hits==0`），无需改正在运行的训练器
- 输出策略：`artifacts/training/<tag>/params-genNNN.txt` / `params-best.txt`

## 判断标准（预先写死，避免事后找理由）

1. **`log.csv` 的 `noHit` 列出现非零**才是真进展；
2. 若适应度只在损失区爬、`noHit` 长期为 0 → 4 种子的比较噪声压住了信号，
   应把每候选种子数提到 8–12（配对取样下噪声约为 `sd·√(2(1−ρ))/√K`，
   sd=0.0603、ρ 未实测）再重启；
3. 120 代跑完仍无无伤 → 如实报告"该特征集 + 该搜索预算达不到无伤"及其证据，
   **不得用"平均伤害提高了"充数**。

## 现在能做什么 / 不能做什么

**不能**（会污染正在跑的实验）：

- 重新编译任何**包含 Core** 的工程——4 个脚本已改但未编译，msbuild 会重编 Core 并改变 DLL；
- 修改 `tools/train-policy.ps1`——验证步骤会 `& $MyInvocation.MyCommand.Path -Mode eval` 从磁盘重读；
- 并发跑额外 wave——训练已占 4 路并发，过载会危及整套测量所依赖的确定性。

**能**：只读分析现有证据、写文档、写不参与编译的新脚本。

## 已完成的其余工作（无需重做）

- 学习策略类型 `src/Chaite.Core/LearnedPolicy.cs`（38→32→10，参数从文件读入）
- **8/8 路线全部接入 hook**（猪鲨 5 条 + 光女 3 条），全部经过"不触碰训练的编译验证"
  （Roslyn `csc` 编译到 `artifacts/_check/`，从不写 Release）
- 启动前四项检查全部实测通过：固定路径逐位回归、随机策略使结果完全不同、
  缺文件 `harness-error`（响亮失败）、并发与串行逐位一致

## 已知无需再查的事

- `terraria=0` 有三种成因，已验证前两种为正常：**批间空隙**、**wave 内种子间空隙**；
  第三种（真死亡）尚未出现过。
- `result.json` **没有 `bossLife` 字段**，用 `bossDamage` / `bossLifeRemaining`。
- PS 5.1 里 `(单个对象).Count` 为 `$null`；`[math]::Max` 对小数会走 int 重载截断。