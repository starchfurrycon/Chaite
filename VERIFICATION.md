# v0.3.0-alpha 验证记录

日期：2026-09-10。目标 Windows Steam Terraria 1.4.5.8 x86。

## 本轮结果

- Release 构建成功；107 项离线回归通过。
- 原版程序集只读 API 契约：181 项检查，其中 136 个绑定直接提取自当前 Facade；四个生产钩子的数量、位置与顺序通过，正式游戏源文件哈希未改变。
- 工作区副本执行原版 AI、角色物理、选槽、物品使用、弹幕与伤害/掉落流程。经典克苏鲁之眼自动召唤、输出、击破、结束接管全部通过：5011 个原生 tick，9 次生命下降，零死亡。**不是无伤，也不是完整图形客户端测试。**
- 月总死亡射线与光女日舞：348 个配置、55,680 次原版碰撞对照，0 漏报、445 个保守额外命中。已打印的额外样例为零面积贴边；不据此推断全部额外样例同因或完全等价。
- 管理器采用非激活、屏幕外多尺寸/缩放/长文本检查。打包必须再次通过 UI smoke，证据在对应 package-check 目录。

## 后台战斗的准确范围

测试使用本机拥有的原版程序集，仅复制到工作区：`Main.dedServ=true`、`netMode=0`，运行 `Main.DoUpdateInWorld`，不是自写 Boss 模拟器。

夹具是 4200×1200 内存世界，其中 2600 格长平坦灰砖地面；经典难度、夜间、400 HP，熔岩套、幽灵靴、云瓶、克盾、钩爪、迷你鲨、9999 发火枪子弹，第 2 个快捷栏槽放一个可疑眼球。只在初始化时配置装备，战斗中不改生命、无敌、攻击力、NPC AI 或物品消耗。不能据此证明最低机动性或最简场地。

F8/F9 由测试副本的确定性热键采样器提供，不使用操作系统输入。保留四个生产入口及真实快捷栏时序；生产 DLL 仅在测试副本中替换热键轮询，其他控制逻辑不改。必须观察到召唤消耗、角色移动、发射与 Boss 扣血；只有真实击杀钩子使状态机确认成功，且完成断言全部成立，退出码才为 0。拒绝激活、超时、异常、取消或失败都不是成功。

专用服务器初始化缺少客户端数据系统，因此探针补齐本地成就、CPU 光照与摄像机矩阵。测试副本禁止 Player/World/Map 存盘，省略 Main.NewText 的绘制、CombatText 浮动字和碎块更新，并关闭物品弹出文字。原版更新中捕获的异常被显式报告，不能吞掉异常后判成功。测试 IL 短跳转统一展开，防止探针插入导致偏移溢出。

输出强制放入工作区隔离目录；SocialMode.None，不载入用户角色、世界或地图，不向 Steam 连接。启动前校验 manifest/全部文件哈希、准确 Save 路径、空存储、参数白名单，拒绝链接目录和重复运行。独立桌面不切换为输入桌面；宿主确认测试进程从未抢前台，超时只结束自己的 Job。这不是通用网络/文件系统沙箱；隔离依据是上述显式限制。

完整 XNA 图形客户端在非活动独立桌面上因 Direct3D Reach 设备初始化失败，未换用用户桌面。dedServ 分支的近战判定尺寸与客户端贴图分支不同，因此本次枪械测试不能验证精确客户端近战；手柄锁定、真实显示器 DPI/缩放、特殊召唤和其他 Boss/变种尚未实战验收。

## 离线覆盖与延迟

覆盖 324 个 Boss/难度/阶段组合的有限输出、288 个固定场景缓存/剪枝等价性；新增旋转光束连续扫掠、96 个原版分段运动场景、解钩恢复、真实 Facade 延迟选槽和倒置瞄准检查。另有状态机、多 Boss、死亡/复活、紧急取消、特殊召唤、单物品事务和安装回滚保护。

本机 x86 Release，384 次暖态计时；仅 Core，不包含游戏采样、渲染或操作系统输入：

| 场景 | p50 | p95 | p99 |
|---|---:|---:|---:|
| 克眼 + 200 弹幕 | 1.216 ms | 1.660 ms | 1.906 ms |
| 猪鲨 + 200 弹幕 | 1.214 ms | 1.740 ms | 1.998 ms |
| 机械组合 + 200 弹幕 | 1.211 ms | 1.693 ms | 1.806 ms |
| 混合 Boss + 200 弹幕 | 1.189 ms | 1.689 ms | 1.919 ms |
| 200 弹幕 + 18 条长光束 | 2.588 ms | 3.994 ms | 4.948 ms |
| 原版分段跑速 + 200 弹幕 | 1.702 ms | 2.360 ms | 2.928 ms |

约 188–195 B/次，计时窗口无 GC；冷构造与首次密集规划 17.850 ms（包括 JIT）。机器负载影响明显，数字不是延迟保证。原版克眼探针中 snapshot+plan+capture 共 4591 次，平均 0.091 ms，最慢 24.295 ms；最慢值包括初次规划/JIT，不能只报告平均值。探针使用节流且不渲染，墙钟加速比不是帧率或端到端延迟。

## 证据与复现

```powershell
.\tools\build.ps1 -Configuration Release
.\tests\Chaite.Tests\bin\Release\net48\Chaite.Tests.exe
.\tools\verify-api-contract.ps1 -OutputDirectory "$PWD\artifacts\verification-local01" -PatchCopy
.\tools\prepare-game-probe.ps1 -RunName game-probe-local01 -Headless
.\tools\start-isolated-test.ps1 -TargetExe "$PWD\artifacts\game-probe-local01\Terraria.exe" -OutputDirectory "$PWD\artifacts\game-probe-local01-desktop"
.\tools\build.ps1 -Configuration Release -Package
```

本机证据：`artifacts/verification-v0.3.0/`、`artifacts/game-probe-final02/` 与其 `-desktop` 目录。旧调试探针包含失败记录，不能代替最终证据；旧 v0.1/v0.2 包保留回退。原版 SHA-256：`960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。

GitHub 只发布自有源码、工具与文档。CI 只执行离线构建/回归；不提供、不下载、不运行 Terraria，不包含测试副本、存档或梗音频。
