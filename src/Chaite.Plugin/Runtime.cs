using Chaite.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace Chaite.Plugin
{
    /// <summary>Entry points injected into vanilla Terraria 1.4.5.8.</summary>
    public static class Runtime
    {
        private static readonly List<int> PendingKilledBosses = new List<int>(8);
        private static bool _initialized;
        private static bool _faulted;
        private static TerrariaFacade _game;
        private static ChaiteConfig _config;
        private static EncounterController _encounter;
        private static CombatPlanner _planner;
        private static HotkeyPoller _hotkeys;
        private static AudioCuePlayer _audio;
        private static Log _log;
        private static int _originalWeapon = -1;
        private static int _terminalDelay;
        private static BossStartPlan _startPlan;
        private static int _startTicks;
        private static bool _summonIssued;
        private static bool _pendingInput;
        private static bool _frameApplied;
        private static object _pendingPlayer;
        private static long _timingTotal;
        private static long _timingMaximum;
        private static int _timingFrames;
        private static bool _expectedBossKilled;

        public static void Tick(object player, int playerIndex)
        {
            if (_faulted || player == null)
                return;
            long tickStarted = 0;
            try
            {
                EnsureInitialized(player.GetType().Assembly, player.GetType());
                if (!_game.IsLocalPlayer(player, playerIndex))
                    return;
                _game.BeginInputFrame();
                _pendingInput = false;
                _frameApplied = false;
                _pendingPlayer = player;
                tickStarted = Stopwatch.GetTimestamp();

                var keys = _hotkeys.Poll();
                if (keys.CancelPressed)
                {
                    if (!_encounter.IsControlling) return;
                    _encounter.Cancel();
                    _game.RestorePendingBossStart(player, _startPlan);
                    _game.ClearCombatControls(player);
                    Finish(player, "已紧急终止，操作权已归还。", AudioCue.None);
                    return;
                }
                if (_game.IsInputBlocked && !_game.IsDead(player))
                {
                    if (_encounter.IsControlling) _game.ClearCombatControls(player);
                    return;
                }

                if (_terminalDelay > 0)
                {
                    _terminalDelay--;
                    if (_terminalDelay == 0)
                        _encounter.ReturnToIdle();
                }

                if (!_encounter.IsControlling)
                {
                    if (!keys.ActivatePressed)
                        return;
                    // Idle kills are unrelated to this session and must never
                    // satisfy completion for a future boss reusing the NPC slot.
                    PendingKilledBosses.Clear();
                    var observation = _game.BuildObservation(player, Array.Empty<int>());
                    _startPlan = _game.IsDead(player) ? null : _game.FindBossStartPlan(player);
                    observation.StartAuthorized = _startPlan != null;
                    observation.RequirePreparation = true;
                    if (_startPlan != null)
                    {
                        var preflight = _game.BuildCombatSnapshot(player, true);
                        string reason;
                        var ready = _planner.RequirementsMetForExpected(preflight, _startPlan.Id, _startPlan.ExpectedBossType, out reason);
                        if (!ready)
                        {
                            HandleCue(AudioCue.NoSlimeAng);
                            _game.Chat("未通过该 Boss 的基础开战检查：" + reason, 255, 155, 110);
                            ResetSessionAutomation();
                            return;
                        }
                    }
                    var activation = _encounter.Activate(observation);
                    HandleCue(activation.Cue);
                    if (activation.Current == SessionState.RejectedNoEncounter)
                    {
                        _game.Chat("我没有史莱姆 ang 啊：需要当前可用的快捷栏召唤物，或已排定的自然 Boss；仅有 Boss 在场不构成启动条件。", 255, 155, 110);
                        _terminalDelay = 1;
                        ResetSessionAutomation();
                        return;
                    }
                    _originalWeapon = _game.GetSelectedItem(player);
                    _startTicks = 0;
                    _summonIssued = false;
                    _expectedBossKilled = false;
                    _timingFrames = 0;
                    _timingTotal = _timingMaximum = 0;
                    _planner.Reset();
                    _game.ResetBossStart();
                    _game.Chat("接管开始；F9 可随时紧急终止。", 255, 210, 78);
                }

                if (_encounter.State == SessionState.PreparingBoss || _encounter.State == SessionState.AwaitingBossSpawn)
                {
                    _startTicks++;
                    var start = _game.IsDead(player)
                        ? new BossStartTick { Issued = _summonIssued, StillValid = _startPlan != null && _startTicks <= _startPlan.TimeoutTicks }
                        : _game.ExecuteBossStart(player, _startPlan, _startTicks, _summonIssued);
                    _summonIssued |= start.Issued;
                    _frameApplied = start.ControlsApplied;
                    if (start.Issued)
                        _encounter.MarkSummonIssued();
                    var waitingObservation = _game.BuildObservation(player, DrainKilledBosses(), true, start.StillValid);
                    waitingObservation.ExpectedBossArrived = _startPlan != null &&
                        (_expectedBossKilled || _game.HasBossType(_startPlan.ExpectedBossType));
                    var waitingUpdate = _encounter.Update(waitingObservation);
                    HandleCue(waitingUpdate.Cue);
                    if (waitingUpdate.BecameTerminal)
                    {
                        if (!string.IsNullOrEmpty(start.FailureReason))
                            _game.Chat(start.FailureReason, 255, 155, 110);
                        FinishTerminal(player, waitingUpdate.Current);
                        return;
                    }
                    if (waitingObservation.ExpectedBossArrived)
                    {
                        _game.RestorePendingBossStart(player, _startPlan);
                        _startPlan = null;
                        _planner.Reset();
                    }
                    else
                    {
                        if (waitingUpdate.ApplyControls && !_game.IsDead(player) && !start.ControlsApplied)
                        {
                            SelectCombatWeapon(player);
                            _game.ApplyPlan(player, _planner.PlanSurvival(_game.BuildCombatSnapshot(player)));
                            _frameApplied = true;
                        }
                        return;
                    }
                }

                var update = _encounter.Update(_game.BuildObservation(player, DrainKilledBosses()));
                HandleCue(update.Cue);
                if (update.BecameTerminal)
                {
                    FinishTerminal(player, update.Current);
                    return;
                }
                if (!update.ApplyControls || _game.IsDead(player))
                {
                    _game.ClearCombatControls(player);
                    return;
                }

                SelectCombatWeapon(player);
                var snapshot = _game.BuildCombatSnapshot(player);
                var plan = _planner.Plan(snapshot);
                _game.ApplyPlan(player, plan);
                _frameApplied = true;
            }
            catch (Exception ex)
            {
                FailClosed(player, ex);
            }
            finally
            {
                if (tickStarted != 0 && _frameApplied && !_faulted && _encounter != null && _encounter.IsControlling)
                {
                    try
                    {
                        _game.CapturePendingInput(player);
                        _pendingInput = true;
                        long elapsed = Stopwatch.GetTimestamp() - tickStarted;
                        _timingTotal += elapsed;
                        if (elapsed > _timingMaximum) _timingMaximum = elapsed;
                        _timingFrames++;
                    }
                    catch (Exception ex) { FailClosed(player, ex); }
                }
            }
        }

        // Replays one already-computed frame after vanilla copies raw controls.
        // These hooks must never plan, scan the world, or throw into Terraria.
        public static void ApplyPendingInput(object player)
        {
            if (!_pendingInput || _faulted || !ReferenceEquals(player, _pendingPlayer)) return;
            try { _game.ApplyPendingInput(player); }
            catch (Exception ex) { FailClosed(player, ex); }
        }

        public static void ApplyPendingSelection(object player)
        {
            if (!_pendingInput || _faulted || !ReferenceEquals(player, _pendingPlayer)) return;
            try { _game.ApplyPendingSelection(player); }
            catch (Exception ex) { FailClosed(player, ex); }
        }

        public static void OnNpcKilled(object npc)
        {
            if (_faulted || npc == null || !_initialized || !_encounter.IsControlling)
                return;
            try
            {
                EnsureInitialized(npc.GetType().Assembly, null);
                if (_game.IsBoss(npc))
                {
                    if (_startPlan != null && _game.GetNpcType(npc) == _startPlan.ExpectedBossType)
                        _expectedBossKilled = true;
                    int key = _game.BossKey(npc);
                    if (!PendingKilledBosses.Contains(key)) PendingKilledBosses.Add(key);
                }
            }
            catch (Exception ex)
            {
                _log?.Write("OnNpcKilled disabled: " + ex);
            }
        }

        private static void EnsureInitialized(System.Reflection.Assembly gameAssembly, Type playerType)
        {
            if (_initialized)
                return;
            if (playerType == null)
                playerType = gameAssembly.GetType("Terraria.Player", true);
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chaite");
            Directory.CreateDirectory(dataDirectory);
            _log = new Log(Path.Combine(dataDirectory, "chaite.log"));
            _config = ChaiteConfig.LoadOrCreate(Path.Combine(dataDirectory, "config.json"));
            _game = new TerrariaFacade(gameAssembly, playerType, _config);
            _encounter = new EncounterController(_config.ClearGraceTicks, _config.HitSoundCooldownTicks);
            _planner = new CombatPlanner(_config.Planner);
            _hotkeys = new HotkeyPoller(_config.ActivateKey, _config.EmergencyStopKey);
            _audio = new AudioCuePlayer(Path.Combine(dataDirectory, "Audio"));
            _initialized = true;
            _log.Write("Initialized against Terraria " + gameAssembly.GetName().Version);
        }

        private static IList<int> DrainKilledBosses()
        {
            if (PendingKilledBosses.Count == 0)
                return Array.Empty<int>();
            var result = PendingKilledBosses.ToArray();
            PendingKilledBosses.Clear();
            return result;
        }

        private static void HandleCue(AudioCue cue)
        {
            if (cue == AudioCue.None)
                return;
            var warning = _audio.Play(cue);
            if (!string.IsNullOrEmpty(warning))
            {
                _log.Write(warning);
                _game.Chat(warning, 255, 170, 120);
            }
        }

        private static void FinishTerminal(object player, SessionState state)
        {
            string message;
            switch (state)
            {
                case SessionState.SuccessNoDeath: message = "无死亡完成战斗，接管结束。"; break;
                case SessionState.SuccessAfterDeath: message = "经历死亡后完成战斗，接管结束。"; break;
                case SessionState.FailedAfterDeath: message = "死亡后战斗已无法继续，接管结束。"; break;
                case SessionState.EncounterInterrupted: message = "Boss 在未确认击破时中断，或召唤流程失效，接管结束。"; break;
                default: message = "接管结束。"; break;
            }
            Finish(player, message, AudioCue.None);
        }

        private static void Finish(object player, string message, AudioCue cue)
        {
            _pendingInput = _frameApplied = false;
            HandleCue(cue);
            _game.RestorePendingBossStart(player, _startPlan);
            _game.ClearCombatControls(player);
            if (_config.RestoreOriginalWeapon && _originalWeapon >= 0 && _originalWeapon < 10)
                _game.SetSelectedItem(player, _originalWeapon);
            _originalWeapon = -1;
            if (_timingFrames > 0)
            {
                double milliseconds = 1000d / Stopwatch.Frequency;
                _log.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Frame timing (snapshot+plan+capture, excluding vanilla): frames={0}, mean={1:F3} ms, max={2:F3} ms",
                    _timingFrames, _timingTotal * milliseconds / _timingFrames, _timingMaximum * milliseconds));
            }
            ResetSessionAutomation();
            _game.Chat(message);
            _terminalDelay = 1;
        }

        private static void SelectCombatWeapon(object player)
        {
            if (!_config.AutoSwitchWeapon)
                return;
            var best = _game.FindBestWeaponSlot(player);
            if (best != _game.GetSelectedItem(player))
                _game.SetSelectedItem(player, best);
        }

        private static void ResetSessionAutomation()
        {
            _pendingInput = _frameApplied = false;
            PendingKilledBosses.Clear();
            _startPlan = null;
            _startTicks = 0;
            _summonIssued = false;
            _expectedBossKilled = false;
            _planner?.Reset();
            _game?.ResetBossStart();
        }

        private static void FailClosed(object player, Exception error)
        {
            _faulted = true;
            _pendingInput = _frameApplied = false;
            try
            {
                _log?.Write("Fatal runtime error; automation disabled: " + error);
                if (_game != null && player != null)
                {
                    _game.ClearCombatControls(player);
                    if (_config != null && _config.RestoreOriginalWeapon && _originalWeapon >= 0 && _originalWeapon < 10)
                        _game.SetSelectedItem(player, _originalWeapon);
                    _originalWeapon = -1;
                    _game.RestorePendingBossStart(player, _startPlan);
                    ResetSessionAutomation();
                    _game.Chat("运行时发生错误，已自动关闭接管。详情见 Chaite/chaite.log。", 255, 90, 90);
                }
            }
            catch
            {
                // The injected entry point must never throw back into the game loop.
            }
        }
    }
}
