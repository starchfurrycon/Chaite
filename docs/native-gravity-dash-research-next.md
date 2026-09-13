# Terraria 1.4.5.8 重力翻转与克盾冲刺合同

## 证据边界

- 只读取正式安装的 `Terraria.exe`，SHA-256 为 `960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3`。
- 证据来自 Mono.Cecil 对本机 IL 的静态检查；没有启动游戏、没有修改存档，也没有把纯函数回归称作 Boss 胜率。
- 本合同的采样点是装备、Buff 与水平运动更新完成后、`Player.Update` 的重力分支或 `DashMovement` 入口前。入口帧旧值不能直接冒充这个采样点。

## 重力药水

原版常量与字段身份：

- `ItemID.GravitationPotion = 305`，实际运动资格由 `BuffID.Gravitation = 18` 建立。
- `Player.UpdateBuffs` 的 `0531..053f` 在发现 Buff 18 时令 `gravControl=true`。
- `GravityGlobe = 1131` 是另一身份；`ApplyEquipFunctional 14ad..14bc` 令 `gravControl2=true`，不能用它替代药水身份取证。

`Player.Update`（token `0x06000990`）正常路径的顺序和语义：

1. `4b5e` 先调用 `HorizontalMovement`，`4b63..4b71` 随后记录 `!mount.Active`。
2. `4b73..4b87` 的 `forcedGravity>0` 优先把 `gravDir` 设为 `-1`，绕过可控翻转。
3. `4b8c..4c4c` 的药水支路只在未骑乘时进入。触发条件对两个方向完全相同，都是 `controlUp && releaseUp`；`gravDir` 为 `+1` 时改为 `-1`，否则改为 `+1`。
4. 真正翻转时把 `fallStart` 设为 `(int)(position.Y / 16)`、把 `jump` 清零；这一分支不反转或清零现有 `velocity.Y`。
5. `4c51..4d0e` 的 Gravity Globe 使用同一个 Up 边沿。未进入两类控制支路时，`4d10..4d16` 恢复 `gravDir=+1`；因此活动坐骑不是“暂缓切换”，而会在这条正常路径上恢复普通重力。
6. `4d7c` 紧接着调用 `UpdateControlHolds`。该方法 `0052..0069` 在 Up 按住时令 `releaseUp=false`，Up 松开时才令其为 `true`。要再次翻转必须完整经过一帧松开，然后再次按 Up；按 Down 不能从倒置恢复。
7. 后续依次是 `JumpMovement 4d89`、`DashMovement 4dc0`、可能的 `WingMovement 5017`，普通重力在 `6801..681c` 才施加，碰撞更晚发生。

因此集成时应把“目标重力方向”翻译成同一个单帧 Up 候选，而不是 `+1 -> Up / -1 -> Down`。候选仅在 Buff 18 身份、字段对应、`forcedGravity==0`、未骑乘、有限位置/速度、`releaseUp==true` 时成立。翻转后至少输出一帧 `controlUp=false`，直到重新观察到 `releaseUp=true`。

## 专用 Dash 键与克苏鲁之盾

身份与入口：

- `ItemID.EoCShield = 3097`；`ApplyEquipFunctional 1463..1472` 把它映射为 `dashType=2`。
- 只见到 `dashType=2` 仍不够，读取器必须同时证明 3097 位于有效功能装备槽；未知类型或身份冲突应失败关闭。
- `DashMovement`（token `0x0600095e`）在 `0050..005f` 仅当 `dashDelay==0` 才把 `dashType` 复制到 `dash`。`dashDelay=-1` 是正在冲刺，不是 ready；现有的 `<=0` ready 判断不符合原版。
- 普通活动坐骑在 `145f..147d` 阻止冲刺；少量 `MountID.Sets.CanDash` 例外和 62–65 号坐骑属于独立合同。克盾合同全部拒绝活动坐骑，避免把坐骑冲刺混为克盾。

输入边沿来自两处：

- `TriggersSet.CopyInto 013b..0158` 先复制 `controlDash`，再执行 `releaseDash = releaseDash || !controlDash`。原版输入复制中的一帧 Dash=false 才会重新武装专用键。
- `DoCommonDashHandle`（token `0x06000960`）`003f..0083` 要求 `controlDash && !CCed && releaseDash`。方向默认取面向；只有水平输入恰好指向面向的反方向时才改用该反方向。无方向、同向或左右同时按都沿面向冲刺。
- 专用键不依赖双击计时或 `Settings.DashControl` 的双击偏好；成功后 `00aa..00c4` 清 `dashTime` 与 `timeSinceLastDashStarted`，`0124..0137` 在 Dash 仍按住时清 `releaseDash`。

克盾起步及衰减：

- `DashMovement 17f9..1826` 在成功边沿把 `velocity.X` 设为 `14.5 * direction`。
- `182b..18db` 检查冲刺方向上的两个 `SolidOrSlopedTile` 探针；任一阻挡就把 X 速度减半。预测器不知道这两个探针结果时不得猜测。
- `18dc..18e6` 令 `dashDelay=-1`、`eocDash=15`，Y 速度不由该支路修改。
- 无 NPC 接触的活动阶段，克盾参数由 `0b07..0c70` 选择：高于 12 的绝对 X 速度每 tick 乘 `.985`；随后高于 `max(accRunSpeed,maxRunSpeed)` 时每 tick 乘 `.94`；降至跑速后，公共支路 `1367..13d3` 设置 30 tick 冷却并把 X 速度归到有符号跑速。
- 正冷却支路 `08fd..093a` 每 tick 把 `dashDelay` 减一并返回；从 1 变 0 的那一 tick 仍不能重新起步，要等下一次 `DashMovement`。`eocDash` 只在这段正冷却的前 15 tick 递减。
- `eocDash>0` 时 NPC 接触会走伤害、免疫、可能反弹等另一合同。当前纯运动合同要求目标轨迹证明无接触；未知接触结果直接失败关闭。

## 与当前注入顺序的集成建议

安装器把 `Runtime.Tick` 注入 `Player.Update` 入口，把 `ApplyPendingInput` 注入原版 `TriggersSet.CopyInto` 之后。这意味着：

- `releaseDash` 的重新武装由原版输入复制完成；后置重放只改 `controlDash`，不应直接写 `releaseDash`。请求冲刺应是一次有 `releaseDash=true` 证据的单帧 true，之后至少重放一帧 false，并等再次观察到 true。
- `releaseUp` 不在 `CopyInto` 更新，而由重力分支后的 `UpdateControlHolds` 更新。翻转帧之后必须给正常更新路径一帧 Up=false，下一帧确认 `releaseUp=true` 才可再次构造候选。
- 入口 `Tick` 早于本帧 `ResetEffects/UpdateBuffs/UpdateEquips`，不能凭一个入口快照声称本帧的 Buff/装备合同。可靠方案是在对应原版分支前增加只读采样，或者要求入口身份连续稳定并对本帧装备/Buff 列表做精确证明；任何不确定性都应停止产生候选。
- `GravityFlipCandidate` 与 `EyeShieldDashCandidate` 都只是可送入轨迹评分的状态转移，不授权 `TerrariaFacade` 写键。只有完整候选轨迹确认能避险且能回归 Boss 闭环后，生产层才应发出那个单帧边沿。
