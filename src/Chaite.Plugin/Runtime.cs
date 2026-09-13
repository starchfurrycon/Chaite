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
        private static int _authorizedBossType;
        private static int _authorizedBossKey = -1;
        private static int _authorizedBossGeneration = -1;
        private static bool _authorizedBossObserved;
        private static bool _authorizedBossContinuityBroken;
        private static string _pendingScopeRejection;
        private static bool _authorizedSessionIdentityKnown;
        private static object _authorizedWorldToken;
        private static object _authorizedPlayerToken;
        private static Guid _authorizedWorldUniqueId;
        private static int _authorizedWorldId;
        private static int _authorizedNetMode = -1;
        private static int _authorizedPlayerIndex = -1;
        private static int _weaponIssueCooldown;
        private static readonly CombatWeaponSelectionHandoff
            CombatWeaponSelection = new CombatWeaponSelectionHandoff();
        // Retained for the standalone contract regression. Runtime now owns a
        // short, explicit native-release handoff instead of asking the player
        // to manually leave a mount or grapple before an active Boss can join.
        private static readonly ActiveEncounterObservationWindow
            ActiveAdmissionObservation =
                new ActiveEncounterObservationWindow(300);
        private static readonly ActiveNativeMobilityHandoff
            ActiveNativeMobilityHandoff = new ActiveNativeMobilityHandoff();

        public static void Tick(object player, int playerIndex)
        {
            // Player.Update invokes the later replay hooks even when this entry
            // declines the current object. Invalidate the previous frame before
            // every early return so stale controls can never cross identities.
            _pendingInput = false;
            _frameApplied = false;
            _pendingPlayer = null;
            if (_faulted || player == null)
                return;
            long tickStarted = 0;
            try
            {
                EnsureInitialized(player.GetType().Assembly, player.GetType());
                if (!_game.IsLocalPlayer(player, playerIndex))
                {
                    if (_encounter.IsControlling &&
                        playerIndex == _authorizedPlayerIndex)
                        AbandonChangedSession(player);
                    return;
                }
                // Validate the old authorization before polling cancellation or
                // restoring any saved item. A new Player/world object can keep
                // the same vanilla IDs after a reload.
                if (_encounter.IsControlling &&
                    !MatchesAuthorizedSessionIdentity(player))
                {
                    AbandonChangedSession(player);
                    return;
                }
                _game.BeginInputFrame();
                if (_weaponIssueCooldown > 0) _weaponIssueCooldown--;
                _pendingPlayer = player;
                tickStarted = Stopwatch.GetTimestamp();

                var keys = _hotkeys.Poll();
                if (keys.CancelPressed)
                {
                    if (ActiveNativeMobilityHandoff.Active)
                    {
                        _encounter.Cancel();
                        Finish(player, "已取消原生移动交接；操作权已归还。",
                            AudioCue.None);
                        return;
                    }
                    if (!_encounter.IsControlling) return;
                    _encounter.Cancel();
                    _game.RestorePendingBossStart(player, _startPlan);
                    _game.ClearCombatControls(player);
                    Finish(player, "已紧急终止，操作权已归还。", AudioCue.None);
                    return;
                }
                if (!string.IsNullOrEmpty(_pendingScopeRejection))
                {
                    var pendingReason = _pendingScopeRejection;
                    _pendingScopeRejection = null;
                    RejectUnsupportedBoss(player, pendingReason);
                    return;
                }
                if (_game.IsInputBlocked && !_game.IsDead(player))
                {
                    if (_encounter.IsControlling)
                    {
                        string blockedScopeReason;
                        if (!TryValidateBlockedControlSession(player,
                                out blockedScopeReason))
                        {
                            RejectUnsupportedBoss(player,
                                blockedScopeReason);
                            return;
                        }
                        _game.ClearCombatControls(player);
                    }
                    return;
                }

                if (_terminalDelay > 0)
                {
                    _terminalDelay--;
                    if (_terminalDelay == 0)
                        _encounter.ReturnToIdle();
                }

                if (ActiveNativeMobilityHandoff.Active &&
                    !TryCompleteDeferredActiveAdmission(player))
                    return;

                if (!_encounter.IsControlling)
                {
                    if (!keys.ActivatePressed)
                        return;
                    // Idle kills are unrelated to this session and must never
                    // satisfy completion for a future boss reusing the NPC slot.
                    PendingKilledBosses.Clear();
                    var observation = _game.BuildObservation(player, Array.Empty<int>());
                    // An already active Boss takes priority over every summon in
                    // the hotbar. Admission uses its observed current phase and
                    // starts from a recovery state; it never consumes another
                    // summon item just because F9 was pressed mid-fight.
                    var joiningActiveBoss = observation.HasEncounter;
                    if (joiningActiveBoss)
                    {
                        HandleCue(AudioCue.UntestedLoadout);
                        _game.Chat("这个波斯，用这个武器来打，从来没试过哦。请在召唤前启动，拆特不再中途接管。", 255, 155, 110);
                        ResetSessionAutomation();
                        return;
                    }
                    // The production surface is deliberately narrower than
                    // the offline strategy catalog. Reject an active,
                    // unreviewed Boss before preflight can latch a route or
                    // mutate any summon transaction.
                    string scopeReason;
                    int observedBossKey = -1;
                    int observedBossGeneration = -1;
                    int observedBossType = 0;
                    if (joiningActiveBoss &&
                        !TryValidateActiveBossScope(observation,
                            out observedBossKey, out observedBossGeneration,
                            out observedBossType,
                            out scopeReason))
                    {
                        RejectUnsupportedBoss(player, scopeReason);
                        return;
                    }
                    _startPlan = joiningActiveBoss || _game.IsDead(player) ?
                        null : _game.FindBossStartPlan(player);
                    if (!joiningActiveBoss && _startPlan != null &&
                        !SupportedBossPolicy.TryValidateStartPlan(_startPlan,
                            out scopeReason))
                    {
                        RejectUnsupportedBoss(player, scopeReason);
                        return;
                    }
                    _authorizedBossType = joiningActiveBoss
                        ? observedBossType
                        : _startPlan?.ExpectedBossType ?? 0;
                    _authorizedBossKey = joiningActiveBoss
                        ? observedBossKey : -1;
                    _authorizedBossGeneration = joiningActiveBoss
                        ? observedBossGeneration : -1;
                    _authorizedBossObserved = joiningActiveBoss;
                    _authorizedBossContinuityBroken = false;
                    observation.StartAuthorized = joiningActiveBoss ||
                        _startPlan != null;
                    observation.RequirePreparation = !joiningActiveBoss;
                    if (joiningActiveBoss || _startPlan != null)
                    {
                        string sessionReason;
                        if (!CaptureAuthorizedSessionIdentity(player,
                                out sessionReason))
                        {
                            HandleCue(AudioCue.NoSlimeAng);
                            _game.Chat("无法锁定当前世界与连接身份：" +
                                sessionReason, 255, 155, 110);
                            ResetSessionAutomation();
                            return;
                        }
                        var preflight = _game.BuildCombatSnapshot(player, _config.AutoSwitchWeapon);
                        string reason = null;
                        var activePreparation = joiningActiveBoss ?
                            _planner.PrepareForSupportedActiveEncounterDetailed(
                                preflight, out reason) :
                            ActiveEncounterPreparationResult.Ready;
                        if (joiningActiveBoss && activePreparation ==
                            ActiveEncounterPreparationResult.
                                AwaitingNativeMobilityRelease)
                        {
                            if (!BeginActiveNativeMobilityHandoff(player,
                                    observation))
                                return;
                            // The helper installed one neutral/release frame
                            // and captured it for post-input replay. Planner
                            // admission follows only when the next native
                            // snapshot proves the old controller is gone.
                            return;
                        }
                        if (joiningActiveBoss && activePreparation !=
                            ActiveEncounterPreparationResult.Ready)
                        {
                            HandleCue(AudioCue.NoSlimeAng);
                            _game.Chat("未通过该 Boss 的基础开战检查：" + reason, 255, 155, 110);
                            ResetSessionAutomation();
                            return;
                        }
                        if (!joiningActiveBoss &&
                            (SupportedBossPolicy.IsSupportedBossType(
                                _startPlan.ExpectedBossType)
                                ? !_planner.PrepareForSupportedFormulaEncounter(
                                    preflight, _startPlan, out reason)
                                : !_planner.PrepareForSupportedExpectedEncounter(
                                    preflight, _startPlan, out reason)))
                        {
                            HandleCue(AudioCue.NoSlimeAng);
                            _game.Chat("未通过该 Boss 的基础开战检查：" + reason, 255, 155, 110);
                            ResetSessionAutomation();
                            return;
                        }
                        if (_startPlan != null &&
                            !_startPlan.TrySetAdmittedCombatWeaponSlot(
                                _planner.LatchedOutputSlot))
                        {
                            RejectUnsupportedBoss(player,
                                SupportedBossPolicy.UnsupportedBossMessage +
                                " (admitted output slot could not be sealed)");
                            return;
                        }
                    }
                    if (!ActivatePreparedSession(player, observation))
                        return;
                }

                if (_encounter.State == SessionState.PreparingBoss || _encounter.State == SessionState.AwaitingBossSpawn)
                {
                    _startTicks++;
                    // Recheck native roots before issuing another item,
                    // fishing, movement, or attack edge. A newly arrived Boss
                    // must be rejected without one frame of summon side effects.
                    var beforeStart = _game.BuildObservation(player,
                        Array.Empty<int>(), true, true);
                    string beforeStartReason;
                    if (!TryValidateAuthorizedBossScope(beforeStart,
                            out beforeStartReason))
                    {
                        RejectUnsupportedBoss(player, beforeStartReason);
                        return;
                    }
                    string startPlanReason;
                    if (!beforeStart.HasEncounter &&
                        !SupportedBossPolicy.TryValidateStartPlan(_startPlan,
                            out startPlanReason))
                    {
                        RejectUnsupportedBoss(player, startPlanReason);
                        return;
                    }
                    var start = beforeStart.HasEncounter
                        ? new BossStartTick
                        {
                            Issued = _summonIssued,
                            StillValid = true
                        }
                        : _game.IsDead(player)
                            ? new BossStartTick
                            {
                                Issued = _summonIssued,
                                StillValid = _startPlan != null &&
                                    _startTicks <= _startPlan.TimeoutTicks
                            }
                            : _game.ExecuteBossStart(player, _startPlan,
                                _startTicks, _summonIssued);
                    _summonIssued |= start.Issued;
                    _frameApplied = start.ControlsApplied;
                    if (start.Issued)
                        _encounter.MarkSummonIssued();
                    var waitingObservation = _game.BuildObservation(player, DrainKilledBosses(), true, start.StillValid);
                    string waitingScopeReason;
                    if (!TryValidateAuthorizedBossScope(waitingObservation,
                            out waitingScopeReason))
                    {
                        RejectUnsupportedBoss(player, waitingScopeReason);
                        return;
                    }
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
                        _planner.ResetForBossArrival();
                    }
                    else
                    {
                        if (waitingUpdate.ApplyControls && !_game.IsDead(player) && !start.ControlsApplied)
                        {
                            if (!EnsureCombatWeaponSelected(player))
                                return;
                            var survivalPlan = _planner.PlanSurvival(
                                _game.BuildCombatSnapshot(player));
                            if (survivalPlan.RequestControlReturn)
                            {
                                _encounter.Cancel();
                                _game.RestorePendingBossStart(player, _startPlan);
                                var reason = string.IsNullOrEmpty(
                                    survivalPlan.ControlReturnReason)
                                    ? "等待 Boss 时机动状态超出精确模型"
                                    : survivalPlan.ControlReturnReason;
                                Finish(player, "安全条件丢失，已归还操作权（" +
                                    reason + "）。", AudioCue.None);
                                return;
                            }
                            var outputFailure = _game.ApplyPlan(player,
                                survivalPlan);
                            if (!string.IsNullOrEmpty(outputFailure))
                            {
                                _encounter.Cancel();
                                _game.RestorePendingBossStart(player,
                                    _startPlan);
                                Finish(player,
                                    "输出路线失去原生证明，已归还操作权（" +
                                    outputFailure + "）。", AudioCue.None);
                                return;
                            }
                            _frameApplied = true;
                        }
                        return;
                    }
                }

                var liveObservation = _game.BuildObservation(player,
                    DrainKilledBosses());
                string liveScopeReason;
                if (!TryValidateAuthorizedBossScope(liveObservation,
                        out liveScopeReason))
                {
                    RejectUnsupportedBoss(player, liveScopeReason);
                    return;
                }
                var update = _encounter.Update(liveObservation);
                HandleCue(update.Cue);
                if (update.BecameTerminal)
                {
                    FinishTerminal(player, update.Current);
                    return;
                }
                if (!update.ApplyControls || _game.IsDead(player))
                {
                    _game.ClearCombatControls(player);
                    // Tick runs at Player.Update entry, before vanilla refreshes
                    // equipment movement on the first live respawn frame. Replay
                    // the neutral controls after CopyInto so raw input cannot leak
                    // into the deliberately deferred admission frame.
                    if (update.Previous ==
                            SessionState.EngagedDeadWaitingRespawn &&
                        update.Current == SessionState.EngagedAlive &&
                        !_game.IsDead(player))
                        _frameApplied = true;
                    return;
                }

                if (!EnsureCombatWeaponSelected(player))
                    return;
                var snapshot = _game.BuildCombatSnapshot(player);
                string planScopeReason;
                var plan = _planner.PlanSupported(snapshot,
                    out planScopeReason);
                if (plan.RequestControlReturn)
                {
                    if (string.Equals(plan.StrategyId,
                            "unsupported-boss-allowlist",
                            StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(planScopeReason) &&
                         planScopeReason.IndexOf(
                             SupportedBossPolicy.UnsupportedBossMessage,
                             StringComparison.Ordinal) >= 0))
                    {
                        RejectUnsupportedBoss(player, planScopeReason);
                        return;
                    }
                    // A bounded strategy contract ended normally. Cancel before
                    // ApplyPlan so no neutral/stale input reaches vanilla, then
                    // use the ordinary finish path to clear pending replay,
                    // restore the original weapon and restore any summon item
                    // transaction. This must not globally fault the plugin.
                    _encounter.Cancel();
                    var detail = string.IsNullOrEmpty(plan.ControlReturnReason)
                        ? "策略安全条件持续丢失" : plan.ControlReturnReason;
                    Finish(player, "安全条件持续丢失，已自动归还操作权（" + detail + "）。", AudioCue.None);
                    return;
                }
                if (!string.IsNullOrEmpty(plan.WeaponIssue) && _weaponIssueCooldown == 0)
                {
                    _game.Chat("自动射击暂停：" + plan.WeaponIssue + "；仍在避险，F9 可归还操作。", 255, 155, 110);
                    _weaponIssueCooldown = 600;
                }
                var nativeOutputFailure = _game.ApplyPlan(player, plan);
                if (!string.IsNullOrEmpty(nativeOutputFailure))
                {
                    _encounter.Cancel();
                    Finish(player, "输出路线失去原生证明，已归还操作权（" +
                        nativeOutputFailure + "）。", AudioCue.None);
                    return;
                }
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

        // Runs at the hash-locked late hook immediately before vanilla
        // HorizontalMovement. It validates only optional edges already scored
        // by Tick; it never scans threats or asks the planner for a new action.
        public static void ValidatePendingMobility(object player)
        {
            if (!_pendingInput || _faulted || !ReferenceEquals(player, _pendingPlayer)) return;
            try
            {
                var rejection = _game.ValidatePendingMobility(player);
                if (!string.IsNullOrEmpty(rejection)) _log?.Write(rejection);
            }
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
                var observedType = _game.GetNpcType(npc);
                if (!SupportedBossPolicy.IsEncounterBossRoot(observedType,
                        _game.IsBoss(npc)))
                    return;
                int key;
                int generation;
                int type;
                if (!MatchesAuthorizedSessionIdentity(null) ||
                    !_game.TryGetSupportedBossIdentity(npc, out key,
                        out generation, out type) ||
                    type != _authorizedBossType ||
                    (_authorizedBossKey >= 0 && key != _authorizedBossKey) ||
                    (_authorizedBossGeneration >= 0 &&
                     generation != _authorizedBossGeneration) ||
                    _authorizedBossContinuityBroken)
                {
                    QueueKilledBossScopeRejection(
                        " (a killed Boss did not match the authorized encounter)");
                    return;
                }
                if (_authorizedBossKey < 0)
                    _authorizedBossKey = key;
                if (_authorizedBossGeneration < 0)
                    _authorizedBossGeneration = generation;
                _authorizedBossObserved = true;
                if (_startPlan != null &&
                    type == _startPlan.ExpectedBossType)
                    _expectedBossKilled = true;
                if (!PendingKilledBosses.Contains(key))
                    PendingKilledBosses.Add(key);
            }
            catch (Exception ex)
            {
                QueueKilledBossScopeRejection(
                    " (Boss kill identity validation failed)");
                try { _log?.Write("OnNpcKilled disabled: " + ex); }
                catch { }
            }
        }

        private static void QueueKilledBossScopeRejection(string detail)
        {
            // The loot hook can run between player updates. Invalidate any
            // captured frame immediately so an unsupported, replaced, or
            // unreadable Boss cannot inherit one last replayed input.
            _pendingInput = false;
            _frameApplied = false;
            _pendingPlayer = null;
            _pendingScopeRejection =
                SupportedBossPolicy.UnsupportedBossMessage + detail;
        }

        private static bool BeginActiveNativeMobilityHandoff(object player,
            EncounterObservation observation)
        {
            // Activate the encounter before installing any release edge. This
            // lets the normal post-CopyInto replay preserve the handoff input
            // while keeping death/encounter completion accounting continuous.
            observation.StartAuthorized = true;
            observation.RequirePreparation = false;
            ActiveAdmissionObservation.Reset();
            ActiveNativeMobilityHandoff.Begin();
            if (!ActivatePreparedSession(player, observation))
            {
                ActiveNativeMobilityHandoff.Reset();
                return false;
            }
            _game.Chat("检测到原生坐骑或抓钩：正在安全解除后接入战斗闭环；F9 可立即取消。",
                255, 210, 78);
            TryCompleteDeferredActiveAdmission(player);
            return true;
        }

        private static bool TryCompleteDeferredActiveAdmission(object player)
        {
            // The native release protocol is intentionally short and has no
            // movement/weapon actions beyond a single rearmed jump or mount
            // edge. See ActiveNativeMobilityHandoff for the source-locked
            // QuickMount/GrappleMovement reasons behind the one-pulse rule.
            var observation = _game.BuildObservation(player,
                DrainKilledBosses());
            string scopeReason;
            if (!TryValidateAuthorizedBossScope(observation,
                    out scopeReason))
            {
                RejectUnsupportedBoss(player, scopeReason);
                return false;
            }
            var update = _encounter.Update(observation);
            HandleCue(update.Cue);
            if (update.BecameTerminal)
            {
                ActiveNativeMobilityHandoff.Reset();
                FinishTerminal(player, update.Current);
                return false;
            }

            var preflight = _game.BuildCombatSnapshot(player,
                _config.AutoSwitchWeapon);
            var mobility = preflight == null ? null : preflight.Mobility;
            var handoffSnapshot = new ActiveNativeMobilityHandoffSnapshot
            {
                Known = preflight != null && preflight.Player != null &&
                    mobility != null,
                EncounterActive = observation.HasEncounter,
                PlayerDead = preflight != null && preflight.Player != null &&
                    preflight.Player.Dead,
                Grappling = mobility != null && mobility.Grappling,
                ReleaseJump = preflight != null && preflight.Player != null &&
                    preflight.Player.Jump.ReleaseReady,
                MountActive = mobility != null && mobility.MountActive,
                ReleaseMount = mobility != null &&
                    mobility.ActiveMountReleaseReady,
                MountDismountProbeKnown = mobility != null &&
                    mobility.ActiveMountDismountProbeKnown,
                MountCanDismount = mobility != null &&
                    mobility.ActiveMountCanDismount
            };
            var decision = ActiveNativeMobilityHandoff.Advance(
                in handoffSnapshot);
            if (decision.Rejected)
            {
                _encounter.Cancel();
                Finish(player, "无法安全交接原生移动状态，已归还操作权（" +
                    decision.Reason + "）。", AudioCue.None);
                return false;
            }
            if (decision.Complete)
            {
                string reason;
                var preparation =
                    _planner.PrepareForSupportedActiveEncounterDetailed(
                        preflight, out reason);
                if (preparation != ActiveEncounterPreparationResult.Ready)
                {
                    _encounter.Cancel();
                    HandleCue(AudioCue.NoSlimeAng);
                    Finish(player, "原生移动已解除，但未通过该 Boss 的基础开战检查：" +
                        reason, AudioCue.None);
                    return false;
                }
                ActiveAdmissionObservation.Reset();
                // The following neutral replay closes the one-tick gap between
                // observed release and planner admission. The recovery planner
                // starts on the next fresh native frame.
                return ApplyNativeMobilityHandoffPlan(player, false, false);
            }
            if (handoffSnapshot.PlayerDead)
                return false;
            return ApplyNativeMobilityHandoffPlan(player, decision.HoldJump,
                decision.ToggleMount);
        }

        private static bool ApplyNativeMobilityHandoffPlan(object player,
            bool holdJump, bool toggleMount)
        {
            var handoffPlan = new ControlPlan
            {
                TargetKey = -1,
                Jump = holdJump,
                JumpAction = holdJump ? JumpAction.Hold : JumpAction.Release,
                ToggleMount = toggleMount
            };
            var failure = _game.ApplyPlan(player, handoffPlan);
            if (!string.IsNullOrEmpty(failure))
            {
                _encounter.Cancel();
                Finish(player, "原生移动交接的输入验证失败，已归还操作权（" +
                    failure + "）。", AudioCue.None);
                return false;
            }
            _frameApplied = true;
            return false;
        }

        private static bool TryValidateActiveBossScope(
            EncounterObservation observation, out int supportedKey,
            out int supportedGeneration, out int supportedType,
            out string reason)
        {
            supportedKey = -1;
            supportedGeneration = -1;
            supportedType = 0;
            reason = null;
            if (observation == null || !observation.HasEncounter)
                return true;
            if (!SupportedBossPolicy.TryValidateActiveBossIdentities(
                    observation.ActiveBossKeys,
                    observation.ActiveBossGenerations,
                    observation.ActiveBossTypes,
                    out supportedType, out reason))
                return false;
            supportedKey = observation.ActiveBossKeys[0];
            supportedGeneration = observation.ActiveBossGenerations[0];
            if (supportedKey < 0)
            {
                reason = "active Boss root identity is invalid";
                return false;
            }
            return true;
        }

        private static bool TryValidateAuthorizedBossScope(
            EncounterObservation observation, out string reason)
        {
            reason = null;
            if (observation == null)
            {
                reason = "active Boss observation is unavailable";
                return false;
            }
            if (!observation.HasEncounter)
            {
                if (_authorizedBossObserved)
                    _authorizedBossContinuityBroken = true;
                return true;
            }

            int supportedKey;
            int supportedGeneration;
            int supportedType;
            if (!TryValidateActiveBossScope(observation, out supportedKey,
                    out supportedGeneration, out supportedType, out reason))
                return false;
            if (_authorizedBossContinuityBroken)
            {
                reason = SupportedBossPolicy.UnsupportedBossMessage +
                    " (the authorized Boss root disappeared and a later root cannot reuse the session)";
                return false;
            }
            if (_authorizedBossType <= 0 ||
                supportedType != _authorizedBossType)
            {
                reason = SupportedBossPolicy.UnsupportedBossMessage +
                    " (active Boss does not match the authorized encounter)";
                return false;
            }
            if (_authorizedBossKey < 0)
                _authorizedBossKey = supportedKey;
            else if (supportedKey != _authorizedBossKey)
            {
                reason = SupportedBossPolicy.UnsupportedBossMessage +
                    " (active Boss root does not match the authorized encounter)";
                return false;
            }
            if (_authorizedBossGeneration < 0)
                _authorizedBossGeneration = supportedGeneration;
            else if (supportedGeneration != _authorizedBossGeneration)
            {
                reason = SupportedBossPolicy.UnsupportedBossMessage +
                    " (active Boss generation does not match the authorized encounter)";
                return false;
            }
            _authorizedBossObserved = true;
            return true;
        }

        private static bool TryValidateBlockedControlSession(object player,
            out string reason)
        {
            reason = null;
            if (_encounter == null || !_encounter.IsControlling)
                return true;
            // Menus and text entry suppress combat input, but native NPCs keep
            // updating in several of those states. Keep the authorization
            // chain live without running the planner or draining kill events.
            var observation = _game.BuildObservation(player,
                Array.Empty<int>());
            return TryValidateAuthorizedBossScope(observation, out reason);
        }

        private static bool CaptureAuthorizedSessionIdentity(object player,
            out string reason)
        {
            reason = null;
            object worldToken;
            Guid uniqueId;
            int worldId;
            int netMode;
            int localPlayerIndex;
            if (player == null ||
                !_game.TryGetSessionIdentity(out worldToken, out uniqueId,
                    out worldId, out netMode, out localPlayerIndex))
            {
                reason = "native session identity is unavailable";
                return false;
            }
            _authorizedWorldToken = worldToken;
            _authorizedPlayerToken = player;
            _authorizedWorldUniqueId = uniqueId;
            _authorizedWorldId = worldId;
            _authorizedNetMode = netMode;
            _authorizedPlayerIndex = localPlayerIndex;
            _authorizedSessionIdentityKnown = true;
            return true;
        }

        private static bool MatchesAuthorizedSessionIdentity(object player)
        {
            if (!_authorizedSessionIdentityKnown)
                return false;
            object worldToken;
            Guid uniqueId;
            int worldId;
            int netMode;
            int localPlayerIndex;
            return (player == null ||
                    ReferenceEquals(player, _authorizedPlayerToken)) &&
                   _game.TryGetSessionIdentity(out worldToken, out uniqueId,
                       out worldId, out netMode, out localPlayerIndex) &&
                   ReferenceEquals(worldToken, _authorizedWorldToken) &&
                   uniqueId == _authorizedWorldUniqueId &&
                   worldId == _authorizedWorldId &&
                   netMode == _authorizedNetMode &&
                   localPlayerIndex == _authorizedPlayerIndex;
        }

        private static void AbandonChangedSession(object player)
        {
            if (_encounter != null && _encounter.IsControlling)
                _encounter.Cancel();
            try { _game?.ClearCombatControls(player); }
            catch { }
            ResetSessionAutomation();
            _encounter?.ReturnToIdle();
        }

        private static bool RejectUnsupportedBoss(object player, string reason)
        {
            if (_encounter != null && _encounter.IsControlling)
                _encounter.Cancel();

            var detail = string.IsNullOrEmpty(reason) ?
                SupportedBossPolicy.UnsupportedReason(0) : reason;
            var technicalStart = detail.IndexOf('(');
            if (technicalStart >= 0)
                detail = detail.Substring(technicalStart);
            if (string.IsNullOrWhiteSpace(detail))
                detail = "outside the production Boss scope";
            _log?.Write("Unsupported Boss rejected: " + detail);
            Finish(player, "这个波斯可是超囊的对我来说",
                AudioCue.UnsupportedBoss);
            return false;
        }

        private static bool ActivatePreparedSession(object player,
            EncounterObservation observation)
        {
            var activation = _encounter.Activate(observation);
            HandleCue(activation.Cue);
            if (activation.Current == SessionState.RejectedNoEncounter)
            {
                _game.Chat("我没有史莱姆 ang 啊", 255, 155, 110);
                _terminalDelay = 1;
                ResetSessionAutomation();
                return false;
            }
            _originalWeapon = _game.GetSelectedItem(player);
            _startTicks = 0;
            _summonIssued = false;
            _expectedBossKilled = false;
            _timingFrames = 0;
            _timingTotal = _timingMaximum = 0;
            _game.ResetBossStart();
            _game.Chat("接管开始；F9 可随时紧急终止。", 255, 210, 78);
            return true;
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

        private static bool EnsureCombatWeaponSelected(object player)
        {
            // Native SelectedItemState applies a requested hotbar change later
            // in Player.Update. In particular, the first frame after a direct
            // item summon still observes the consumed summon slot here. Keep
            // that transition neutral instead of validating the stale slot as
            // though it were the already-admitted combat output route.
            if (_planner?.UsesSummonWhipOutput == true)
            {
                CombatWeaponSelection.Reset();
                return true;
            }
            var desired = _planner?.LatchedOutputSlot ?? -1;
            if (desired < 0 || desired >= 10)
                desired = !_config.AutoSwitchWeapon && _originalWeapon >= 0 &&
                    _originalWeapon < 10 ? _originalWeapon : -1;
            if (desired < 0)
            {
                CombatWeaponSelection.Reset();
                return true;
            }
            var decision = CombatWeaponSelection.Advance(
                _game.GetSelectedItem(player) == desired,
                _game.CanChangeSelectedItemImmediately(player));
            if (decision.Complete)
            {
                return true;
            }
            if (decision.Rejected)
            {
                _encounter.Cancel();
                Finish(player,
                    "战斗武器槽位无法在原版物品动画结束后切回，已自动归还操作权（" +
                    decision.Reason + "）。",
                    AudioCue.None);
                return false;
            }
            if (decision.RequestSelection)
                _game.SetSelectedItem(player, desired);
            _game.ClearCombatControls(player);
            _frameApplied = true;
            return false;
        }

        private static void ResetSessionAutomation()
        {
            _pendingInput = _frameApplied = false;
            ActiveAdmissionObservation.Reset();
            ActiveNativeMobilityHandoff.Reset();
            PendingKilledBosses.Clear();
            _startPlan = null;
            _startTicks = 0;
            _summonIssued = false;
            _expectedBossKilled = false;
            _authorizedBossType = 0;
            _authorizedBossKey = -1;
            _authorizedBossGeneration = -1;
            _authorizedBossObserved = false;
            _authorizedBossContinuityBroken = false;
            _pendingScopeRejection = null;
            _authorizedSessionIdentityKnown = false;
            _authorizedWorldToken = null;
            _authorizedPlayerToken = null;
            _authorizedWorldUniqueId = Guid.Empty;
            _authorizedWorldId = 0;
            _authorizedNetMode = -1;
            _authorizedPlayerIndex = -1;
            _weaponIssueCooldown = 0;
            CombatWeaponSelection.Reset();
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
