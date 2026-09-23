using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Reflection;

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
        private static bool _pendingInput;
        private static bool _frameApplied;
        private static object _pendingPlayer;
        private static long _timingTotal;
        private static long _timingMaximum;
        private static int _timingFrames;
        private static int _authorizedBossType;
        private static int _authorizedBossKey = -1;
        private static int _authorizedBossGeneration = -1;
        private static bool _authorizedBossObserved;
        private static bool _authorizedBossContinuityBroken;
        // Set when the authorized Boss's kill is drained; the live-scope check
        // must not reject the session for the missing root after a real kill.
        private static bool _authorizedBossKilled;
        private static string _pendingScopeRejection;
        private static bool _authorizedSessionIdentityKnown;
        // A composed route replayed through the real game. Null unless the
        // environment names a file, so the ordinary planner is untouched.
        private static readonly RouteReplay _replay =
            RouteReplay.LoadFromEnvironment();
        private static int _replayFrames;
        // Set while a frame of this tick has read the route. The index advances
        // once per tick at the entry below, not once per read: a tick that runs
        // the plan twice -- the handoff path does -- consumed two route entries
        // and put the replay permanently one entry ahead of the model, which no
        // constant skip can undo.
        private static bool _replayTickRead;
        private static object _authorizedWorldToken;
        private static object _authorizedPlayerToken;
        private static Guid _authorizedWorldUniqueId;
        private static int _authorizedWorldId;
        private static int _authorizedNetMode = -1;
        private static int _authorizedPlayerIndex = -1;
        // Throttles the training-bridge notice about ignoring a formula-table
        // control-return request, so a fight that hits it every tick cannot flood
        // chat. Only ever non-zero while CHAITE_BRIDGE_FILE names a route.
        private static int _bridgeGateCooldown;

        /// <summary>True when something other than the planner's formula script
        /// owns the movement channels this tick.
        ///
        /// Either the training bridge's replay or an exported policy overwrites
        /// Horizontal/Jump/Drop/Dash before ApplyPlan, so when one of them is
        /// loaded the planner's mobility contract no longer guards a real output:
        /// it only decides whether the episode -- or the real fight -- continues.
        /// The formula-state whitelist is a review artifact of the hand-written
        /// formula era and whitelists Fishron state 8 only for sequence 0, while
        /// the native AI also reaches sequence 1, so acting on it here would stop
        /// the policy exactly where it does most of its flying.</summary>
        private static bool MovementAuthorityIsExternal
        {
            get { return _replay != null || _exportedPolicy != null; }
        }
        private static FormulaRoute _monitorFishronRoute;
        // CombatWeaponSelectionHandoff used to live here, driving the hotbar
        // switch that served the planner's latched output route. The takeover is
        // movement-only now, so the plugin never selects a slot and the handoff
        // has no caller. The type itself stays in Chaite.Core: it is a standalone
        // contract with its own regression, and deleting it is a separate call.
        // Retained for the standalone contract regression. Runtime now owns a
        // short, explicit native-release handoff instead of asking the player
        // to manually leave a mount or grapple before an active Boss can join.
        private static readonly ActiveEncounterObservationWindow
            ActiveAdmissionObservation =
                new ActiveEncounterObservationWindow(300);
        private static readonly ActiveNativeMobilityHandoff
            ActiveNativeMobilityHandoff = new ActiveNativeMobilityHandoff();
        // The in-process driver for an exported policy. Null unless
        // CHAITE_POLICY_FORMAT=exported names one, so every existing
        // configuration runs the planner exactly as it did before this existed.
        private static ChaitePolicyDriver _exportedPolicy;

        /// <summary>The absolute game tick a tick-keyed route is indexed by.
        /// This is <c>Main.GameUpdateCount</c>, the same counter the probe
        /// publishes and the trainer writes into its action file, and it is what
        /// makes a tick-keyed replay reproducible: the applied-frame counter is
        /// not the game tick, because it only advances on ticks the plugin
        /// actually applied a plan for. Measured drift inside one session:
        /// 240 -> 96965.
        /// </summary>
        private static long CurrentGameTick()
        {
            try
            {
                return _game.GameTick();
            }
            catch (Exception)
            {
                // A route that cannot read the tick must not take the session
                // down; the positional path below still covers frame-indexed
                // files, and a tick-keyed file simply reports no coverage.
                return -1L;
            }
        }

        public static void Tick(object player, int playerIndex)
        {
            // Player.Update invokes the later replay hooks even when this entry
            // declines the current object. Invalidate the previous frame before
            // every early return so stale controls can never cross identities.
            _pendingInput = false;
            _frameApplied = false;
            _pendingPlayer = null;
            // The route advances here, at most once per tick, and only for a
            // tick in which the previous frame actually read it.
            if (_replayTickRead)
            {
                _replayFrames++;
                _replayTickRead = false;
            }
            if (_faulted || player == null)
                return;
            long tickStarted = 0;
            try
            {
                EnsureInitialized(player.GetType().Assembly, player.GetType());
                if (!_game.IsLocalPlayer(player, playerIndex))
                {
                    if (_encounter.IsSessionActive &&
                        playerIndex == _authorizedPlayerIndex)
                        AbandonChangedSession(player);
                    return;
                }
                // Validate the old authorization before polling cancellation or
                // restoring any saved item. A new Player/world object can keep
                // the same vanilla IDs after a reload.
                if (_encounter.IsSessionActive &&
                    !MatchesAuthorizedSessionIdentity(player))
                {
                    AbandonChangedSession(player);
                    return;
                }
                _game.BeginInputFrame();
                if (_bridgeGateCooldown > 0) _bridgeGateCooldown--;
                _pendingPlayer = player;
                tickStarted = Stopwatch.GetTimestamp();

                var keys = _hotkeys.Poll();
                if (keys.CancelPressed)
                {
                    if (_encounter.State == SessionState.Monitoring)
                    {
                        StopBossMonitor("已取消监视；操作权始终由你保留。", AudioCue.None);
                        return;
                    }
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

                if (_encounter.State == SessionState.Monitoring)
                {
                    if (!TryStartMonitoredEncounter(player))
                        return;
                }
                else if (!_encounter.IsControlling)
                {
                    if (keys.ActivatePressed)
                        ArmBossMonitor(player);
                    return;
                }

                var liveObservation = _game.BuildObservation(player,
                    DrainKilledBosses());
                string liveScopeReason;
                if (!TryValidateAuthorizedBossScope(liveObservation,
                        out liveScopeReason))
                {
                    // The Boss root disappears the moment it is killed, so a
                    // legitimate kill leaves this check with nothing to verify.
                    // Rejecting it cancelled the encounter instead of letting it
                    // complete: measured on an isolated probe whose simulated
                    // output killed Duke Fishron at tick 474, the run ended
                    // "FINISH Cancelled battlePassed=False bossLife=0
                    // win=false", and the plugin log read "Unsupported Boss
                    // rejected: no verifiable active Boss root". The training
                    // reward then charged a timeout for killing the Boss, so a
                    // win was unreachable. Once the authorized Boss has been
                    // killed, let the encounter controller observe the kill and
                    // finish the session itself.
                    if (!_authorizedBossKilled)
                    {
                        RejectUnsupportedBoss(player, liveScopeReason);
                        return;
                    }
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

                // The takeover is MOVEMENT ONLY, so the plugin no longer selects
                // a hotbar slot or clears the player's combat controls. It used
                // to do both, through EnsureCombatWeaponSelected, to serve the
                // planner's latched output route -- which also meant suppressing
                // whatever the user was attacking with, exactly what the
                // movement-only rule says to leave alone. The plan's weapon
                // fields are still cleared before ApplyPlan, so nothing the
                // planner decided about a shot can reach vanilla.
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
                    //
                    // EXCEPT when something else already owns the movement
                    // channels this tick: the training bridge's replay, or an
                    // exported policy. Both overwrite Horizontal/Jump/Drop/Dash
                    // below, so a contract ending then guards only outputs that
                    // are discarded -- while acting on it either destroys a
                    // training rollout or hands a real fight back to the user
                    // mid-flight.
                    //
                    // MEASURED 2026-09-21: under the bridge this ended 7.2% of
                    // fsw121d's fights and 30.5% of fsw121p's, essentially always
                    // at the endgame, because the reviewed formula table
                    // whitelists Fishron state 8 only for sequence 0 and the
                    // native AI also reaches sequence 1. In a real fight the same
                    // gate would block exactly the states the trained policy flies
                    // in, so the policy could not be deployed through it at all.
                    // See docs/safety-abort-misclassified-2026-09-21.md 12.7-12.9
                    // and 15.3.
                    if (MovementAuthorityIsExternal)
                    {
                        if (_bridgeGateCooldown == 0)
                        {
                            var ignored = string.IsNullOrEmpty(
                                plan.ControlReturnReason)
                                ? "策略安全条件持续丢失"
                                : plan.ControlReturnReason;
                            _game.Chat((_replay != null
                                ? "训练桥接：忽略公式表归还请求，移动仍由策略驱动（"
                                : "走位已由导出策略接管：忽略公式表归还请求（")
                                + ignored + "）。", 255, 220, 120);
                            _bridgeGateCooldown = 600;
                        }
                    }
                    else
                    {
                        _encounter.Cancel();
                        var detail = string.IsNullOrEmpty(plan.ControlReturnReason)
                            ? "策略安全条件持续丢失" : plan.ControlReturnReason;
                        Finish(player, "安全条件持续丢失，已自动归还操作权（" + detail + "）。", AudioCue.None);
                        return;
                    }
                }
                // The planner still reports plan.WeaponIssue, but the takeover no
                // longer fires, so "auto-fire paused" would be a claim about a
                // shot this plugin is not taking. The field is dropped with the
                // rest of the weapon commands below and its notice is gone.
                // The replay replaces the plan's movement controls for the
                // frames it covers. Everything else the planner decided -- the
                // weapon route, the consumables, the aim -- is left alone,
                // because the route is a claim about movement and forcing the
                // rest would be a different claim.
                // Published every frame, not only while a route covers it, so the
                // observation can tell "no route" apart from "route frame zero".
                plan.ReplayFrame = _replay != null ? _replayFrames : -1;
                if (_replay != null)
                {
                    int replayDirection;
                    bool replayJump;
                    bool replayUp;
                    bool replayDown;
                    bool replayDash;
                    if (_replay.TryRead(_replayFrames, CurrentGameTick(),
                            out replayDirection, out replayJump, out replayUp,
                            out replayDown, out replayDash))
                    {
                        plan.Horizontal = replayDirection;
                        plan.Jump = replayJump;
                        plan.Dash = replayDash;
                        // Holding DOWN is its own input in Terraria, not a
                        // consequence of the horizontal direction: it makes the
                        // broom descend fast, folds wings into a drop instead
                        // of a glide, changes the Featherfall descent and
                        // changes mount descent. Forcing it false here is what
                        // kept the trained action space horizontal-only, so the
                        // route's own down bit now owns controlDown.
                        plan.Drop = replayDown;
                        // UP drives BOTH channels, deliberately. Lift is carried
                        // by plan.Jump -> controlJump, which is also how mounts
                        // ascend, so broom/chillet/queen-slime/lilith keep their
                        // ascent; plan.FeatherFallUp carries the independent up
                        // key that feather fall reads (controlUp is separate
                        // from controlJump). controlUp is left subject to the
                        // facade's featherfall-premise gate
                        // (TerrariaFacade.cs:3941-3955) rather than bypassed:
                        // that gate is the project's fail-closed discipline, and
                        // the route channel may not claim a mobility premise the
                        // game does not actually satisfy. Lift is not gated.
                        // Gravity reversal stays out of scope, so GravityControl
                        // remains zero.
                        plan.FeatherFallUp = replayUp;
                        plan.GravityControl = 0;
                        plan.ToggleMount = false;
                        plan.Hook = false;
                        plan.HoldNeutralControls = false;
                        // The action has to follow the route, not be pinned to
                        // Hold. JumpAction.Hold forces the requested value true
                        // inside the resolver, so a route that releases the jump
                        // key still held it, and every enumerated candidate that
                        // differed in its jump channel produced the same run.
                        // Release is the action that actually yields false.
                        plan.JumpAction = replayJump
                            ? JumpAction.Hold
                            : JumpAction.Release;
                        // The takeover is a movement claim only. Weapon use,
                        // aiming and firing are deliberately out of scope, and
                        // the boss's health is driven by the probe's simulated
                        // player output instead, so nothing here may emit a
                        // shot: the plan's output certificate is dropped and
                        // the fire gate is closed explicitly.
                        plan.Fire = false;
                        plan.OutputRouteKind = OutputRouteKind.Unspecified;
                        plan.ExpectedWeaponId = 0;
                        plan.ExpectedAmmoId = 0;
                        plan.ExpectedProjectileId = 0;
                    }
                    // Marked, not counted. The counter moves at the tick entry
                    // so that a tick which reads the route more than once still
                    // consumes one entry, and a tick that never reads it consumes
                    // none. Counting here instead left the replay an entry ahead
                    // of the model from the very first tick.
                    _replayTickRead = true;
                }
                // The exported policy, when one is configured, owns the same
                // movement channels the replay above overwrites: it is applied
                // after the replay so that selecting the exported format is an
                // unambiguous statement about who is flying. Everything else
                // the planner decided is left alone, for the same reason the
                // replay leaves it alone.
                if (_exportedPolicy != null)
                {
                    var tick = CurrentGameTick();
                    var policyTarget = default(TargetSnapshot);
                    var policyTargetFound = false;
                    // The formula-table contract returns before the planner
                    // stamps its target (CombatPlanner.NewPlan leaves TargetKey at
                    // -1 and only the success paths assign it), so a plan that
                    // RequestControlReturn'd carries no target. Resolve the Boss
                    // here instead: without it the exported policy is handed
                    // nothing, its Observe/Apply pair is skipped, and the player
                    // would stand still in exactly the states we just decided to
                    // keep flying through. Only the two production Bosses qualify,
                    // which is the same admission scope the planner enforces.
                    var policyTargetKey = plan.TargetKey;
                    if (policyTargetKey < 0)
                    {
                        for (var index = 0; index < snapshot.Targets.Count;
                            index++)
                        {
                            var candidate = snapshot.Targets[index];
                            if (!SupportedBossPolicy.IsSupportedBossType(
                                    candidate.Type))
                                continue;
                            policyTargetKey = candidate.Key;
                            break;
                        }
                    }
                    if (policyTargetKey >= 0)
                    {
                        for (var index = 0; index < snapshot.Targets.Count;
                            index++)
                        {
                            if (snapshot.Targets[index].Key != policyTargetKey)
                                continue;
                            policyTarget = snapshot.Targets[index];
                            policyTargetFound = true;
                            break;
                        }
                    }
                    if (policyTargetFound)
                    {
                        // Apply BEFORE Observe, and the order is load-bearing.
                        // ChaitePolicyDriver.Apply only accepts a decision whose
                        // _pendingTick is exactly tick-1, and Observe stamps
                        // _pendingTick = tick. Called the other way round -- which
                        // is what this did -- Apply always saw _pendingTick == tick,
                        // incremented _waits, returned false and left the planner's
                        // own controls in place, so the exported policy loaded and
                        // then never flew a single frame. It was invisible because
                        // the formula script is competent: the run still won
                        // fights, it just won them without the policy.
                        //
                        // With this order the pairing is the trainer's: the row
                        // built from tick T-1's entry state chooses tick T's
                        // action, and the row built now becomes tick T+1's.
                        _exportedPolicy.Apply(tick, ref plan);
                        _exportedPolicy.Observe(player, in policyTarget, tick);
                    }
                }
                // The takeover is MOVEMENT ONLY. Attacking, aiming and
                // consumables stay the user's, so every non-movement command the
                // planner produced is dropped here instead of being handed to
                // vanilla. ChaitePolicyDriver.ApplyAction already clears the same
                // fields for the learned policy; doing it for the planner's own
                // plans too means the two controllers cannot disagree about what
                // the takeover owns.
                //
                // This runs LAST, after the replay and the exported policy have
                // written their movement, and it deliberately touches no movement
                // channel: Horizontal/Jump/Drop/Dash/Hook/ToggleMount/
                // GravityControl/FeatherFallUp are exactly what the takeover is
                // for. Dropping the aim here is also what makes plan.TargetKey
                // purely the policy's business rather than a claim about a shot.
                DropNonMovementCommands(ref plan);
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
                    _log?.Write("KILL_REJECT identityKnown=" + _authorizedSessionIdentityKnown +
                        " authorizedType=" + _authorizedBossType +
                        " authorizedKey=" + _authorizedBossKey +
                        " authorizedGeneration=" + _authorizedBossGeneration +
                        " continuityBroken=" + _authorizedBossContinuityBroken);
                    return;
                }
                if (_authorizedBossKey < 0)
                    _authorizedBossKey = key;
                if (_authorizedBossGeneration < 0)
                    _authorizedBossGeneration = generation;
                _authorizedBossObserved = true;
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
                // Same race as the main tick path: a killed Boss leaves no
                // active root to verify, and rejecting that here cancelled the
                // session on the deferred-admission path before the encounter
                // controller could record the kill. Measured with a forced
                // 20000 DPS: BOSS_KILL_REPORT fired and ARM authorized type=370
                // was set, no identity rejection was logged, yet the run still
                // ended "FINISH Cancelled win=false" from this call site.
                if (!_authorizedBossKilled)
                {
                    RejectUnsupportedBoss(player, scopeReason);
                    return false;
                }
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
            if (_encounter?.State == SessionState.Monitoring)
            {
                ResetSessionAutomation();
                _encounter.ReturnToIdle();
                return;
            }
            if (_encounter != null && _encounter.IsControlling)
                _encounter.Cancel();
            try { _game?.ClearCombatControls(player); }
            catch { }
            ResetSessionAutomation();
            _encounter?.ReturnToIdle();
        }

        private static bool RejectUnsupportedBoss(object player, string reason)
        {
            // A killed authorized Boss is gone from the world, so every
            // live-scope check downstream of the kill reports "no verifiable
            // active Boss root" -- an unsupported-Boss rejection that cancelled
            // the session before the encounter controller could record the kill
            // (measured: FINISH Cancelled win=false with bossLife=0, while
            // BOSS_KILL_REPORT fired and no identity rejection was logged).
            // Guarding each call site individually missed one, so the guard
            // lives here. The flag is only ever set after the authorized Boss's
            // kill is drained and is cleared when the session re-arms.
            if (_authorizedBossKilled)
                return false;
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
            _timingFrames = 0;
            _timingTotal = _timingMaximum = 0;
            _game.ResetBossStart();
            _game.Chat("接管开始；F9 可随时紧急终止。", 255, 210, 78);
            return true;
        }

        private static void ArmBossMonitor(object player)
        {
            var observation = _game.BuildObservation(player, Array.Empty<int>());
            if (observation.HasEncounter)
            {
                int key, generation, type;
                string reason;
                var supported = TryValidateActiveBossScope(observation, out key,
                    out generation, out type, out reason);
                StopBossMonitor(supported
                    ? "请在 Boss 出现前按 F8 开始监视；不接受中途接管。"
                    : SupportedBossPolicy.UnsupportedBossMessage,
                    supported ? AudioCue.UntestedLoadout : AudioCue.UnsupportedBoss);
                return;
            }
            if (observation.PlayerDead)
            {
                StopBossMonitor("请复活后再开始监视。", AudioCue.NoSlimeAng);
                return;
            }
            ResetSessionAutomation();
            var snapshot = _game.BuildCombatSnapshot(player, _config.AutoSwitchWeapon);
            _originalWeapon = -1;
            _terminalDelay = 0;
            _monitorFishronRoute = CombatPlanner.SelectMonitorRoute(snapshot, 370);
            if (_monitorFishronRoute == FormulaRoute.None)
            {
                StopBossMonitor(FormulaRouteCatalog.Refusal, AudioCue.UntestedLoadout);
                return;
            }
            string sessionReason;
            if (!CaptureAuthorizedSessionIdentity(player, out sessionReason))
            {
                StopBossMonitor("无法锁定当前世界与角色：" + sessionReason, AudioCue.NoSlimeAng);
                return;
            }
            observation.StartAuthorized = true;
            var update = _encounter.ArmMonitoring(observation);
            HandleCue(update.Cue);
            _game.Chat("MAN！监视已开启：请自行召唤猪鲨。出现后接管；F9 取消监视。", 255, 210, 78);
        }

        private static bool TryStartMonitoredEncounter(object player)
        {
            var observation = _game.BuildObservation(player, Array.Empty<int>());
            if (observation.PlayerDead)
            {
                StopBossMonitor("监视期间角色死亡，监视已取消；复活后可再次按 F8。", AudioCue.Dead);
                return false;
            }
            if (!observation.HasEncounter)
                return false;
            int key, generation, type;
            string reason;
            if (!TryValidateActiveBossScope(observation, out key, out generation, out type, out reason))
            {
                StopBossMonitor(SupportedBossPolicy.UnsupportedBossMessage, AudioCue.UnsupportedBoss);
                return false;
            }
            var armedRoute = _monitorFishronRoute;
            var snapshot = _game.BuildCombatSnapshot(player, _config.AutoSwitchWeapon);
            bool mobilityRefusal;
            if (!_planner.PrepareForMonitoredFormulaEncounter(snapshot, type,
                    armedRoute, out reason, out mobilityRefusal))
            {
                StopBossMonitor(mobilityRefusal
                        ? FormulaRouteCatalog.Refusal + "（" + reason + "）"
                        : "当前武器输出无法完成固定公式（" + reason + "）",
                    mobilityRefusal ? AudioCue.UntestedLoadout : AudioCue.None);
                return false;
            }
            _log?.Write("ARM authorized type=" + type + " key=" + key + " generation=" + generation);
            _authorizedBossType = type;
            _authorizedBossKey = key;
            _authorizedBossGeneration = generation;
            _authorizedBossObserved = true;
            _authorizedBossContinuityBroken = false;
            _authorizedBossKilled = false;
            PendingKilledBosses.Clear();
            observation.StartAuthorized = true;
            observation.RequirePreparation = false;
            // Activate records initial life at actual takeover, excluding
            // player-controlled monitoring damage from battle statistics.
            return ActivatePreparedSession(player, observation);
        }

        private static void StopBossMonitor(string message, AudioCue cue)
        {
            // No selection restoration, input clearing or native controls:
            // monitoring never owned any player input in the first place.
            ResetSessionAutomation();
            _originalWeapon = -1;
            _terminalDelay = 0;
            _encounter.ReturnToIdle();
            HandleCue(cue);
            _game.Chat(message, 255, 210, 78);
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
            // Resolved once, eagerly, so a configured-but-broken policy fails at
            // startup rather than on the first tick it would have owned. Left
            // null -- and inert -- unless the exported format is selected.
            _exportedPolicy = ChaitePolicyDriver.FromEnvironment(_game);
            if (_exportedPolicy != null)
                _log.Write("Exported policy loaded: " + _exportedPolicy.SourcePath +
                    " [" + _exportedPolicy.LayerShapes + "], observation " +
                    _exportedPolicy.ObservationCount + " wide");
        }

        private static IList<int> DrainKilledBosses()
        {
            if (PendingKilledBosses.Count == 0)
                return Array.Empty<int>();
            var result = PendingKilledBosses.ToArray();
            PendingKilledBosses.Clear();
            // A killed Boss is gone from the world on the very frame the kill
            // is reported, so from here on the live-scope check has no active
            // root to verify. Remember it: the rejection path below would
            // otherwise treat the successful kill as an unsupported Boss.
            _authorizedBossKilled = true;
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
                // An exported policy that loaded but never flew is silent, and
                // that is exactly how the Apply/Observe order bug survived: the
                // formula script kept winning fights, so nothing looked wrong.
                // WaitedTicks growing while AppliedActions stays at zero is the
                // signature, so print both every session end.
                if (_exportedPolicy != null)
                    _log.Write("Exported policy flight: applied=" +
                        _exportedPolicy.AppliedActions + " waited=" +
                        _exportedPolicy.WaitedTicks);
            }
            ResetSessionAutomation();
            _game.Chat(message);
            _terminalDelay = 1;
        }

        /// <summary>Drop every non-movement command from a plan.
        ///
        /// The takeover owns movement and nothing else. This is the same rule
        /// <see cref="ChaitePolicyDriver.ApplyAction"/> follows for the learned
        /// policy, applied to the planner's own plans, so the formula script and
        /// the learned policy cannot disagree about what the takeover owns.
        ///
        /// The movement channels are deliberately untouched -- they are the point
        /// of the takeover, and by the time this runs the replay and the exported
        /// policy have already written them. <c>TargetKey</c> is cleared as well:
        /// it is read by the weapon/aim path in <c>TerrariaFacade</c> (the
        /// line-of-sight and sight-cache bookkeeping around ApplyPlan), and with
        /// no shot to aim there is no target to claim. The exported policy
        /// resolves its own target separately, so clearing it here does not blind
        /// the policy.
        /// </summary>
        private static void DropNonMovementCommands(ref ControlPlan plan)
        {
            plan.Fire = false;
            plan.QuickHeal = false;
            plan.QuickMana = false;
            plan.QuickBuff = false;
            plan.PreferredWeaponSlot = -1;
            plan.WeaponIssue = null;
            plan.TargetKey = -1;
            plan.OutputRouteKind = OutputRouteKind.Unspecified;
            plan.ExpectedWeaponId = 0;
            plan.ExpectedAmmoId = 0;
            plan.ExpectedProjectileId = 0;
        }

        private static void ResetSessionAutomation()
        {
            _monitorFishronRoute = FormulaRoute.None;
            _pendingInput = _frameApplied = false;
            ActiveAdmissionObservation.Reset();
            ActiveNativeMobilityHandoff.Reset();
            PendingKilledBosses.Clear();
            _startPlan = null;
            _authorizedBossType = 0;
            _authorizedBossKey = -1;
            _authorizedBossGeneration = -1;
            _authorizedBossObserved = false;
            _authorizedBossContinuityBroken = false;
            _authorizedBossKilled = false;
            _pendingScopeRejection = null;
            _authorizedSessionIdentityKnown = false;
            _authorizedWorldToken = null;
            _authorizedPlayerToken = null;
            _authorizedWorldUniqueId = Guid.Empty;
            _authorizedWorldId = 0;
            _authorizedNetMode = -1;
            _authorizedPlayerIndex = -1;
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
