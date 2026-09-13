using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Result of attempting to admit an already active Boss. A native mount or
    /// grapple is a transient ownership state: the planner must not fabricate
    /// a detach input, but it also must not turn one observation into a
    /// permanent rejection. The plugin waits briefly while vanilla/player
    /// input resolves that state, then retries the normal admission.
    /// </summary>
    public enum ActiveEncounterPreparationResult
    {
        Ready,
        AwaitingNativeMobilityRelease,
        Rejected
    }

    /// <summary>
    /// Small, allocation-free window used by the runtime while an active native
    /// movement action still owns the player. It deliberately has no Terraria
    /// knowledge; callers decide what counts as a busy native action.
    /// </summary>
    public enum ActiveEncounterObservationResult
    {
        Waiting,
        ReadyToRetry,
        EncounterEnded,
        TimedOut
    }

    public sealed class ActiveEncounterObservationWindow
    {
        private readonly int _maximumTicks;
        private int _observedTicks;

        public ActiveEncounterObservationWindow(int maximumTicks)
        {
            _maximumTicks = Math.Max(1, maximumTicks);
        }

        public bool Active { get; private set; }
        public int ObservedTicks => _observedTicks;
        public int MaximumTicks => _maximumTicks;

        public void Begin()
        {
            Active = true;
            _observedTicks = 0;
        }

        public ActiveEncounterObservationResult Observe(
            bool encounterStillActive, bool nativeMobilityBusy)
        {
            if (!Active)
                return encounterStillActive && !nativeMobilityBusy ?
                    ActiveEncounterObservationResult.ReadyToRetry :
                    ActiveEncounterObservationResult.Waiting;
            if (!encounterStillActive)
            {
                Reset();
                return ActiveEncounterObservationResult.EncounterEnded;
            }
            if (!nativeMobilityBusy)
                return ActiveEncounterObservationResult.ReadyToRetry;
            _observedTicks++;
            if (_observedTicks >= _maximumTicks)
            {
                Reset();
                return ActiveEncounterObservationResult.TimedOut;
            }
            return ActiveEncounterObservationResult.Waiting;
        }

        public void Reset()
        {
            Active = false;
            _observedTicks = 0;
        }
    }

    /// <summary>
    /// Bounded-horizon combat planner. Boss code chooses a durable movement pattern;
    /// the nine-candidate predictor is only the inner collision-avoidance loop.
    /// </summary>
    public sealed class CombatPlanner
    {
        private readonly PlannerSettings _settings;
        private readonly BossStrategyEngine _strategies = new BossStrategyEngine();
        private int _lastHorizontal = 1;
        private int _patternDirection = 1;
        private int _directionHoldTicks;
        private int _stuckTicks;
        private int _recoveryTicks;
        private bool _activePatternRecovery;
        private int _activeRecoveryTotalElapsedTicks;
        private int _activeRecoveryAbsoluteDeadlineTicks;
        private int _activeRecoveryElapsedTicks;
        private int _activeRecoveryNoProgressTicks;
        private int _activeRecoveryClosedTicks;
        private int _activeRecoveryDeadlineTicks;
        private float _activeRecoveryPreviousDistance = float.MaxValue;
        private ActivePatternRecoveryRoute _activeRecoveryRoute;
        private bool _activeRecoveryRouteKnown;
        private bool _scoreRecoveryRequired;
        private ActivePatternRecoveryMeasure _scoreRecoveryCurrent;
        private int _emergencyHoldTicks;
        private int _stableTicks;
        private bool _hasPreviousPosition;
        private Vec2 _previousPosition;
        private readonly List<ThreatSnapshot> _relevantThreats = new List<ThreatSnapshot>(1000);
        private readonly List<ThreatSnapshot> _relevantBeams = new List<ThreatSnapshot>(48);
        private readonly int _horizonTicks;
        private readonly int _stepTicks;
        private int _gravityCooldown;
        private bool _gravityReleasePending;
        private bool _gravityReturnPending;
        private bool _gravityReturnInverted;
        private bool _gravityReturnWasGrounded;
        private Vec2 _gravityReturnAnchor;
        private SupportSpan _gravityReturnSupport;
        private bool _dashReleasePending;
        private Candidate _lateMobilityFallback;
        private BossRequirements _latchedMobilityRouteRequirements;
        private string _latchedMobilityRouteId;
        private BossMobilityRouteProfile _latchedMobilityRoute;
        private BossMobilityCapabilityThreshold _latchedMobilityThreshold;
        private float _latchedMobilityRequiredHorizontalSpeed;
        private int _latchedMobilityDifficultyFlags;
        private int _latchedMobilityGameMode;
        private bool _hasLatchedOutputRoute;
        private OutputRouteProfile _latchedOutputRoute;
        private SummonWhipOutputController _summonWhipOutputController;
        private float _latchedMinimumOutputDps;
        private BossLocomotionBaseline _activeLocomotion =
            BossLocomotionBaseline.Unspecified;
        private bool _restoringFlight;
        private SupportSpan _landingSupport;
        private ThreatStep[] _threatSteps = Array.Empty<ThreatStep>();
        private TargetedProjectileMotionState[] _targetedThreatStates =
            Array.Empty<TargetedProjectileMotionState>();
        // NPC 636's state-8/9 dash is candidate-coupled: its destination is
        // built from the player's centre every native tick. Keep a distinct
        // state array so the Projectile 873 homing predictor retains its own
        // semantics and both paths stay allocation-free on the rollout hot
        // loop.
        private EmpressDashMotionState[] _empressDashThreatStates =
            Array.Empty<EmpressDashMotionState>();
        private BeamStep[] _beamSteps = Array.Empty<BeamStep>();

        public int LastCandidateCount { get; private set; }
        public int LastRelevantThreatCount => _relevantThreats.Count + _relevantBeams.Count;
        public int LatchedOutputSlot => _hasLatchedOutputRoute ?
            _latchedOutputRoute.WeaponSlot : -1;
        public int LatchedOutputWeaponId => _hasLatchedOutputRoute ?
            _latchedOutputRoute.WeaponId : 0;
        public bool UsesSummonWhipOutput =>
            _summonWhipOutputController != null;

        public CombatPlanner(PlannerSettings settings)
        {
            _settings = settings ?? new PlannerSettings();
            _horizonTicks = Math.Max(6, Math.Min(90, _settings.HorizonTicks));
            _stepTicks = Math.Max(1, Math.Min(6, _settings.SimulationStepTicks));
        }

        public bool RequirementsMet(CombatSnapshot snapshot, out string reason)
        {
            var requirements = _strategies.RequirementsFor(snapshot);
            if (requirements == null)
            {
                reason = "未识别到受支持的 Boss";
                return false;
            }
            return RequirementsMetWithAvailableOutput(snapshot, requirements,
                out reason);
        }

        /// <summary>
        /// Production-only expected-Boss admission. The broad strategy API is
        /// intentionally left available for offline fixtures; the native
        /// runtime should call this wrapper so an old strategy cannot become a
        /// live takeover merely by matching the catalog.
        /// </summary>
        public bool ProductionRequirementsMetForExpected(
            CombatSnapshot snapshot, string planId, int expectedBossType,
            out string reason)
        {
            if (!SupportedBossPolicy.TryValidateExpectedType(expectedBossType,
                    out reason))
                return false;
            return RequirementsMetForExpected(snapshot, planId,
                expectedBossType, out reason);
        }

        /// <summary>
        /// Production-only pre-summon admission wrapper. It verifies the
        /// exact reviewed summon route before delegating to the existing
        /// mobility/output certificate checks.
        /// </summary>
        public bool PrepareForSupportedExpectedEncounter(
            CombatSnapshot snapshot, BossStartPlan startPlan,
            out string reason)
        {
            if (!SupportedBossPolicy.TryValidateStartPlan(startPlan,
                    out reason))
                return false;
            return PrepareForExpectedEncounter(snapshot, startPlan.Id,
                startPlan.ExpectedBossType, out reason);
        }

        /// <summary>
        /// Production-only active encounter admission when the caller has a
        /// Core snapshot but no separate native type-list observation. The
        /// facade should additionally validate its authoritative
        /// EncounterObservation.ActiveBossTypes before calling this method.
        /// </summary>
        public ActiveEncounterPreparationResult
            PrepareForSupportedActiveEncounterDetailed(
                CombatSnapshot snapshot, out string reason)
        {
            if (!SupportedBossPolicy.IsSupportedNativeSnapshot(snapshot,
                    out reason))
                return ActiveEncounterPreparationResult.Rejected;
            return PrepareForActiveEncounterDetailed(snapshot, out reason);
        }

        /// <summary>
        /// Production frame gate. Unsupported/ambiguous snapshots return a
        /// neutral plan requesting control return; they never reach weapon or
        /// movement candidate scoring.
        /// </summary>
        public ControlPlan PlanSupported(CombatSnapshot snapshot,
            out string reason)
        {
            if (!SupportedBossPolicy.IsSupportedNativeSnapshot(snapshot,
                    out reason))
            {
                var plan = NewPlan(snapshot);
                plan.StrategyId = "unsupported-boss-allowlist";
                plan.PhaseId = "unsupported-production-boss";
                plan.RequestControlReturn = true;
                plan.ControlReturnReason = reason;
                return plan;
            }
            reason = null;
            return Plan(snapshot);
        }

        public bool RequirementsMetForExpected(CombatSnapshot snapshot, string planId, int expectedBossType, out string reason)
        {
            BossRequirements requirements;
            if (!string.IsNullOrEmpty(planId) && planId.IndexOf("mechdusa", StringComparison.OrdinalIgnoreCase) >= 0)
                requirements = _strategies.RequirementsForExpected(snapshot.Difficulty, 125, 127, 134);
            else
                requirements = _strategies.RequirementsForExpected(snapshot.Difficulty, expectedBossType);
            if (requirements == null)
            {
                reason = "没有对应的 Boss 策略";
                return false;
            }
            if (!RequirementsMetWithAvailableOutput(snapshot, requirements,
                    out reason))
                return false;
            if (IsLacewingStart(planId) &&
                OrdinaryOutputUnavailableAtThreshold(snapshot,
                    requirements.MinimumWeaponDps))
            {
                reason = "prismatic lacewing start requires an admitted " +
                    "single-slot projectile route";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Performs a fresh pre-summon admission and reserves the one complete
        /// controller route selected for the encounter. This
        /// keeps an equipment change during the spawn wait from silently
        /// replacing the movement model which was actually admitted.
        /// </summary>
        public bool PrepareForExpectedEncounter(CombatSnapshot snapshot,
            string planId, int expectedBossType, out string reason)
        {
            Reset();
            BossRequirements requirements;
            if (!string.IsNullOrEmpty(planId) && planId.IndexOf("mechdusa",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                requirements = _strategies.RequirementsForExpected(
                    snapshot.Difficulty, 125, 127, 134);
            else
                requirements = _strategies.RequirementsForExpected(
                    snapshot.Difficulty, expectedBossType);
            if (requirements == null)
            {
                reason = "没有对应的 Boss 策略";
                return false;
            }
            OutputRouteProfile outputRoute;
            SummonWhipOutputController summonWhip;
            float admittedDps;
            if (!TryCreateOutputAdmission(snapshot,
                    requirements.MinimumWeaponDps, out outputRoute,
                    out summonWhip, out admittedDps, out reason))
                return false;
            if (summonWhip != null && IsLacewingStart(planId))
            {
                reason = "prismatic lacewing start requires an admitted " +
                    "single-slot projectile route";
                return false;
            }
            BossMobilityRouteProfile selected;
            if (!RequirementsMetWithAdmission(snapshot, requirements,
                    admittedDps, out selected, out reason))
                return false;
            LatchMobilityRoute(requirements, snapshot, in selected);
            if (snapshot?.Weapon?.NativeProfileRequired == true)
            {
                if (summonWhip != null)
                    LatchSummonWhipOutput(summonWhip,
                        requirements.MinimumWeaponDps);
                else
                LatchOutputRoute(in outputRoute,
                    requirements.MinimumWeaponDps);
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Admits a Boss which is already active. The current native phase is
        /// authoritative; no summon-start assumptions are fabricated. Generic
        /// movement begins in recovery mode so it first returns to the reviewed
        /// loop instead of treating an arbitrary mid-fight position as stable.
        /// </summary>
        public bool PrepareForActiveEncounter(CombatSnapshot snapshot,
            out string reason)
        {
            return PrepareForActiveEncounterDetailed(snapshot, out reason) ==
                ActiveEncounterPreparationResult.Ready;
        }

        /// <summary>
        /// Attempts active-Boss admission without treating a currently held
        /// mount/grapple as a permanent failure. The returned waiting state is
        /// intentionally side-effect free after Reset(): no route is latched,
        /// so a later retry observes the native state afresh.
        /// </summary>
        public ActiveEncounterPreparationResult PrepareForActiveEncounterDetailed(
            CombatSnapshot snapshot, out string reason)
        {
            Reset();
            var requirements = _strategies.RequirementsFor(snapshot);
            if (requirements == null)
            {
                reason = "no reviewed strategy matches the active Boss set";
                return ActiveEncounterPreparationResult.Rejected;
            }
            if (snapshot.Mobility != null &&
                (snapshot.Mobility.MountActive || snapshot.Mobility.Grappling))
            {
                reason = snapshot.Mobility.MountActive &&
                    snapshot.Mobility.Grappling ?
                    "active mount and grapple are still native-owned; waiting for release" :
                    snapshot.Mobility.MountActive ?
                    "active mount is still native-owned; waiting for release" :
                    "active grapple is still native-owned; waiting for release";
                return ActiveEncounterPreparationResult.AwaitingNativeMobilityRelease;
            }

            // RequirementsFor intentionally selects by the live Boss family so
            // it can reserve the appropriate lower-bound mobility contract.
            // That alone is not enough for an in-progress encounter: a
            // priority strategy may have just observed an impossible native
            // timer, a mismatched independent source, or a departure state.
            // Validate the exact source-driven directive before latching any
            // output/mobility route or reporting active-F8 admission success.
            // Use an isolated engine: preflight must not advance the live
            // planner's attack/movement memory before its first controlled
            // frame. Plan() still repeats this check every frame, because a
            // valid snapshot can change before the first controlled input.
            var activeDecision = new BossStrategyEngine().Evaluate(snapshot);
            if (activeDecision == null || activeDecision.Requirements == null)
            {
                reason = "no reviewed native strategy matches the active Boss state";
                return ActiveEncounterPreparationResult.Rejected;
            }
            if (activeDecision.Directive.RequestControlReturn)
            {
                reason = string.IsNullOrEmpty(
                    activeDecision.Directive.ControlReturnReason) ?
                    "active Boss native state is not admitted" :
                    activeDecision.Directive.ControlReturnReason;
                return ActiveEncounterPreparationResult.Rejected;
            }
            OutputRouteProfile outputRoute;
            SummonWhipOutputController summonWhip;
            float admittedDps;
            if (!TryCreateOutputAdmission(snapshot,
                    requirements.MinimumWeaponDps, out outputRoute,
                    out summonWhip, out admittedDps, out reason))
                return ActiveEncounterPreparationResult.Rejected;
            BossMobilityRouteProfile selected;
            if (!RequirementsMetWithAdmission(snapshot, requirements,
                    admittedDps, out selected, out reason))
                return ActiveEncounterPreparationResult.Rejected;
            LatchMobilityRoute(requirements, snapshot, in selected);
            if (snapshot?.Weapon?.NativeProfileRequired == true)
            {
                if (summonWhip != null)
                    LatchSummonWhipOutput(summonWhip,
                        requirements.MinimumWeaponDps);
                else
                LatchOutputRoute(in outputRoute,
                    requirements.MinimumWeaponDps);
            }
            _activePatternRecovery = true;
            _activeRecoveryPreviousDistance = float.MaxValue;
            _activeRecoveryTotalElapsedTicks = 0;
            _activeRecoveryAbsoluteDeadlineTicks =
                ActivePatternRecovery.AbsoluteRecoveryDeadlineTicks;
            _activeRecoveryElapsedTicks = 0;
            _activeRecoveryNoProgressTicks = 0;
            _activeRecoveryClosedTicks = 0;
            _activeRecoveryDeadlineTicks = 0;
            _activeRecoveryRoute = default(ActivePatternRecoveryRoute);
            _activeRecoveryRouteKnown = false;
            _recoveryTicks = Math.Max(1, _settings.RecoveryTicks);
            reason = null;
            return ActiveEncounterPreparationResult.Ready;
        }

        public ControlPlan Plan(CombatSnapshot snapshot)
        {
            var plan = NewPlan(snapshot);
            if (snapshot?.Player == null || snapshot.Player.Dead)
                return plan;
            if (snapshot.Mobility != null && snapshot.Mobility.MountActive)
                return UnsupportedActiveMountPlan(plan,
                    TacticalMode.EmergencyEvade);
            if (snapshot.Mobility != null && snapshot.Mobility.Grappling)
                return UnsupportedActiveGrapplePlan(plan, TacticalMode.EmergencyEvade);

            var decision = _strategies.Evaluate(snapshot);
            if (decision == null)
                return PlanSurvival(snapshot);
            UpdateMotionMemory(snapshot);

            var directive = decision.Directive;
            if (directive.RequestControlReturn)
            {
                // This is a normal strategy-level relinquish, not an emergency
                // candidate. Keep the plan fully neutral so optional dash/hook/
                // mount/gravity or weapon layers cannot act before Runtime
                // closes the session.
                LastCandidateCount = 0;
                _relevantThreats.Clear();
                _relevantBeams.Clear();
                plan.JumpAction = JumpAction.Release;
                plan.StrategyId = directive.StrategyId;
                plan.PhaseId = directive.PhaseId;
                plan.RequestControlReturn = true;
                plan.ControlReturnReason = directive.ControlReturnReason;
                return plan;
            }
            if (directive.HoldNeutralControls)
            {
                // Keep the takeover alive, but do not turn an unknown native
                // trajectory into a guessed escape, output pulse, potion, or
                // optional mobility edge. The adapter independently honors the
                // marker after clearing controls, so future plan fields cannot
                // accidentally weaken this current-frame hold.
                LastCandidateCount = 0;
                _relevantThreats.Clear();
                _relevantBeams.Clear();
                _emergencyHoldTicks = _settings.EmergencyHysteresisTicks;
                _recoveryTicks = _settings.RecoveryTicks;
                _stableTicks = 0;
                plan.JumpAction = JumpAction.Release;
                plan.TacticalMode = TacticalMode.EmergencyEvade;
                plan.StrategyId = directive.StrategyId;
                plan.PhaseId = directive.PhaseId;
                plan.HoldNeutralControls = true;
                plan.NeutralControlReason = directive.NeutralControlReason;
                RememberPlan(plan);
                return plan;
            }
            if (_latchedMobilityRouteId != null &&
                !ReferenceEquals(_latchedMobilityRouteRequirements,
                    decision.Requirements))
                return UnsupportedMobilityRoutePlan(plan,
                    "Boss 策略已变化，不能在同一接管会话中切换已锁定的机动路线");
            if (snapshot.Weapon.NativeProfileRequired)
            {
                string outputFailure;
                if (!EnsureLiveOutput(snapshot,
                        decision.Requirements.MinimumWeaponDps,
                        out outputFailure))
                    return UnsupportedOutputRoutePlan(plan, outputFailure);
                if (_hasLatchedOutputRoute) StampOutputRoute(ref plan);
            }
            BossMobilityRouteProfile mobilityRoute;
            string mobilityFailure;
            bool matched;
            if (_latchedMobilityRouteId == null)
            {
                matched = decision.Requirements.TrySelectReadyMobilityRoute(
                    snapshot, out mobilityRoute, out mobilityFailure);
            }
            else if (!MatchesLatchedMobilityDifficulty(snapshot.Difficulty))
            {
                mobilityRoute = default(BossMobilityRouteProfile);
                mobilityFailure =
                    "Boss 难度或世界变种已变化，不能替换已锁定的机动能力合同";
                matched = false;
            }
            else
            {
                mobilityRoute = _latchedMobilityRoute;
                matched = decision.Requirements.TryValidateLiveMobilityRoute(
                    snapshot, in _latchedMobilityRoute,
                    in _latchedMobilityThreshold,
                    _latchedMobilityRequiredHorizontalSpeed,
                    out mobilityFailure);
            }
            if (!matched)
                return UnsupportedMobilityRoutePlan(plan, mobilityFailure);
            if (_latchedMobilityRouteId == null)
                LatchMobilityRoute(decision.Requirements, snapshot,
                    in mobilityRoute);
            var mobilityBaseline = mobilityRoute.Baseline;
            _activeLocomotion = mobilityBaseline.Locomotion;
            var target = decision.Target;
            var patternTarget = decision.PatternTarget;
            PrepareThreats(snapshot, directive);
            var patternHorizontal = PatternHorizontal(snapshot, patternTarget, directive);
            var patternVertical = PatternVertical(snapshot, patternTarget, directive);
            RespectArenaEdges(snapshot, ref patternHorizontal, ref patternVertical,
                directive.UseExplicitMovement, directive.OwnsMovementClosure,
                directive.OwnsHorizontalClosure);
            if (directive.OwnsMovementClosure)
            {
                _restoringFlight = false;
                _landingSupport = default(SupportSpan);
            }
            else
            {
                var closedHorizontal = patternHorizontal;
                BudgetFlight(snapshot, ref patternHorizontal, ref patternVertical);
                if (directive.OwnsHorizontalClosure) patternHorizontal = closedHorizontal;
            }

            var currentRecovery = ActivePatternRecovery.Measure(snapshot,
                patternTarget, directive, snapshot.Player.Position,
                snapshot.Player.Velocity, patternHorizontal, patternVertical, 0f);
            var best = FindBestCandidate(snapshot, patternTarget, directive,
                patternHorizontal, patternVertical, mobilityBaseline.Dash,
                _activePatternRecovery, currentRecovery);
            var immediateRisk = ImmediateRisk(snapshot, directive);
            var emergency = immediateRisk >= _settings.EmergencyRiskThreshold || best.Hazard >= _settings.EmergencyRiskThreshold;
            var stuck = _stuckTicks >= _settings.StuckTicksBeforeRecovery;

            string recoveryFailure;
            if (_activePatternRecovery && !UpdateActivePatternRecovery(
                    currentRecovery, best, directive, patternTarget.Key,
                    patternHorizontal, patternVertical, emergency, stuck,
                    out recoveryFailure))
                return UnsupportedPatternRecoveryPlan(plan, directive,
                    recoveryFailure);

            TacticalMode mode;
            if (emergency)
            {
                _emergencyHoldTicks = _settings.EmergencyHysteresisTicks;
                _recoveryTicks = _settings.RecoveryTicks;
                _stableTicks = 0;
                mode = TacticalMode.EmergencyEvade;
            }
            else if (_emergencyHoldTicks > 0)
            {
                _emergencyHoldTicks--;
                mode = TacticalMode.EmergencyEvade;
            }
            else if (_activePatternRecovery || stuck || _recoveryTicks > 0 || _restoringFlight)
            {
                if (!_activePatternRecovery && _recoveryTicks > 0)
                    _recoveryTicks--;
                mode = TacticalMode.RecoverToPattern;
            }
            else if (_stableTicks < _settings.StablePatternTicks)
            {
                _stableTicks++;
                mode = TacticalMode.EstablishPattern;
            }
            else
            {
                mode = TacticalMode.StablePattern;
            }

            plan.Horizontal = best.Horizontal;
            plan.Jump = best.Posture == JumpPosture;
            plan.JumpAction = best.JumpAction;
            plan.Drop = best.Posture < 0;
            plan.FeatherFallUp = best.Posture == FeatherFallUpPosture;
            plan.TargetKey = target.Key;
            plan.RiskScore = best.Score;
            plan.TacticalMode = mode;
            plan.StrategyId = directive.StrategyId;
            plan.PhaseId = directive.PhaseId;

            // Source-specific controllers (e.g. a committed King run-under)
            // must keep the scored escape, not receive an unscored reversal/jump
            // afterwards. FindBestCandidate already expands for stuck states.
            var preservedScoredControls = true;
            if (mode == TacticalMode.RecoverToPattern && stuck &&
                !_activePatternRecovery && !directive.UseExplicitMovement)
            {
                plan.Horizontal = _lastHorizontal == 0 ? _patternDirection : -_lastHorizontal;
                plan.Jump = true;
                plan.JumpAction = JumpAction.Default;
                preservedScoredControls = false;
            }

            if (preservedScoredControls) ApplyScoredMobility(snapshot, best, ref plan);

            string plannedOutputFailure;
            if (!ApplyPlannedOutput(snapshot, target, directive.Fire,
                    ref plan, out plannedOutputFailure))
                return UnsupportedOutputRoutePlan(plan,
                    plannedOutputFailure);
            ApplyConsumables(snapshot, ref plan);
            RememberPlan(plan);
            return plan;
        }

        /// <summary>Low-cost defensive loop used while a scheduled or issued summon is pending.</summary>
        public ControlPlan PlanSurvival(CombatSnapshot snapshot)
        {
            var plan = NewPlan(snapshot);
            if (snapshot?.Player == null || snapshot.Player.Dead)
                return plan;
            if (snapshot.Mobility != null && snapshot.Mobility.MountActive)
                return UnsupportedActiveMountPlan(plan,
                    TacticalMode.AwaitingBoss);
            if (snapshot.Mobility != null && snapshot.Mobility.Grappling)
                return UnsupportedActiveGrapplePlan(plan, TacticalMode.AwaitingBoss);

            var survivalBaseline = new BossMobilityBaseline
            {
                Locomotion = BossLocomotionBaseline.OnFoot,
                Dash = BossDashBaseline.None
            };
            if (_latchedMobilityRouteRequirements != null &&
                _latchedMobilityRouteId != null)
            {
                string mobilityFailure = null;
                if (!MatchesLatchedMobilityDifficulty(snapshot.Difficulty))
                    mobilityFailure =
                        "Boss 难度或世界变种已变化，不能替换已锁定的机动能力合同";
                else if (!_latchedMobilityRouteRequirements.
                    TryValidateLiveMobilityRoute(snapshot,
                        in _latchedMobilityRoute,
                        in _latchedMobilityThreshold,
                        _latchedMobilityRequiredHorizontalSpeed,
                        out mobilityFailure))
                {
                    // The selected source or its live recoverability changed.
                }
                if (mobilityFailure != null)
                    return UnsupportedMobilityRoutePlan(plan, mobilityFailure);
                survivalBaseline = _latchedMobilityRoute.Baseline;
            }
            if (_hasLatchedOutputRoute || _summonWhipOutputController != null)
            {
                string outputFailure;
                if (!EnsureLiveOutput(snapshot, _latchedMinimumOutputDps,
                        out outputFailure))
                    return UnsupportedOutputRoutePlan(plan, outputFailure);
                if (_hasLatchedOutputRoute) StampOutputRoute(ref plan);
            }
            _activeLocomotion = survivalBaseline.Locomotion;

            UpdateMotionMemory(snapshot);
            LastCandidateCount = 0;
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            if (snapshot.Targets.Count == 0 && snapshot.Threats.Count == 0)
            {
                plan.Horizontal = ArenaCenterDirection(snapshot);
                plan.TacticalMode = TacticalMode.AwaitingBoss;
                string idleOutputFailure;
                if (!ApplyPlannedOutput(snapshot, default(TargetSnapshot),
                        false, ref plan, out idleOutputFailure))
                    return UnsupportedOutputRoutePlan(plan,
                        idleOutputFailure);
                ApplyConsumables(snapshot, ref plan);
                RememberPlan(plan);
                return plan;
            }

            var hasTarget = snapshot.Targets.Count > 0;
            var target = hasTarget ? SelectPrimaryTarget(snapshot) : new TargetSnapshot
            {
                Key = -1, Position = snapshot.Player.Center, Width = 0, Height = 0, Invulnerable = true
            };
            var directive = new BossDirective
            {
                StrategyId = "awaiting-boss-survival",
                PhaseId = "clear-hostiles",
                Pattern = BossPattern.HorizontalKite,
                IdealDistance = hasTarget ? 440f : 0f,
                VerticalOffset = hasTarget ? -70f : 0f,
                HorizontalIntent = hasTarget ? AwayX(snapshot.Player.Center, target.Center) : 0,
                ForceContinuousMovement = hasTarget,
                Fire = true,
                ExtraContactMargin = 48f,
                AllowHook = true
            };
            PrepareThreats(snapshot, directive);
            var h = hasTarget ? PatternHorizontal(snapshot, target, directive) : ArenaCenterDirection(snapshot);
            var v = hasTarget ? PatternVertical(snapshot, target, directive) : 0;
            RespectArenaEdges(snapshot, ref h, ref v);
            BudgetFlight(snapshot, ref h, ref v);
            var best = FindBestCandidate(snapshot, target, directive, h, v);
            plan.Horizontal = best.Horizontal;
            plan.Jump = best.Posture == JumpPosture;
            plan.JumpAction = best.JumpAction;
            plan.Drop = best.Posture < 0;
            plan.FeatherFallUp = best.Posture == FeatherFallUpPosture;
            plan.TargetKey = target.Key;
            plan.RiskScore = best.Score;
            plan.TacticalMode = TacticalMode.AwaitingBoss;
            plan.StrategyId = directive.StrategyId;
            plan.PhaseId = directive.PhaseId;
            string plannedOutputFailure;
            if (!ApplyPlannedOutput(snapshot, target, hasTarget, ref plan,
                    out plannedOutputFailure))
                return UnsupportedOutputRoutePlan(plan,
                    plannedOutputFailure);
            ApplyScoredMobility(snapshot, best, ref plan);
            ApplyConsumables(snapshot, ref plan);
            RememberPlan(plan);
            return plan;
        }

        public void Reset()
        {
            ResetCore(false);
        }

        /// <summary>
        /// Clears waiting-phase motion and Boss memory while preserving the
        /// pre-summon mobility and output-route reservations.
        /// </summary>
        public void ResetForBossArrival()
        {
            ResetCore(true);
        }

        private void ResetCore(bool preserveMobilityRoute)
        {
            var reservedRequirements = preserveMobilityRoute
                ? _latchedMobilityRouteRequirements : null;
            var reservedRouteId = preserveMobilityRoute
                ? _latchedMobilityRouteId : null;
            var reservedRoute = _latchedMobilityRoute;
            var reservedThreshold = _latchedMobilityThreshold;
            var reservedSpeed = _latchedMobilityRequiredHorizontalSpeed;
            var reservedDifficultyFlags = _latchedMobilityDifficultyFlags;
            var reservedGameMode = _latchedMobilityGameMode;
            var reservedHasOutputRoute = preserveMobilityRoute &&
                _hasLatchedOutputRoute;
            var reservedOutputRoute = _latchedOutputRoute;
            var reservedSummonWhipOutput = preserveMobilityRoute
                ? _summonWhipOutputController : null;
            var reservedMinimumOutputDps = _latchedMinimumOutputDps;
            _strategies.Reset();
            _lastHorizontal = 1;
            _patternDirection = 1;
            _directionHoldTicks = 0;
            _stuckTicks = 0;
            _recoveryTicks = 0;
            _activePatternRecovery = false;
            _activeRecoveryTotalElapsedTicks = 0;
            _activeRecoveryAbsoluteDeadlineTicks = 0;
            _activeRecoveryElapsedTicks = 0;
            _activeRecoveryNoProgressTicks = 0;
            _activeRecoveryClosedTicks = 0;
            _activeRecoveryDeadlineTicks = 0;
            _activeRecoveryPreviousDistance = float.MaxValue;
            _activeRecoveryRoute = default(ActivePatternRecoveryRoute);
            _activeRecoveryRouteKnown = false;
            _scoreRecoveryRequired = false;
            _scoreRecoveryCurrent = default(ActivePatternRecoveryMeasure);
            _emergencyHoldTicks = 0;
            _stableTicks = 0;
            _hasPreviousPosition = false;
            _restoringFlight = false;
            _landingSupport = default(SupportSpan);
            _gravityCooldown = 0;
            _gravityReleasePending = _dashReleasePending = false;
            _gravityReturnPending = false;
            _gravityReturnInverted = _gravityReturnWasGrounded = false;
            _gravityReturnAnchor = default(Vec2);
            _gravityReturnSupport = default(SupportSpan);
            _latchedMobilityRouteRequirements = null;
            _latchedMobilityRouteId = null;
            _latchedMobilityRoute = default(BossMobilityRouteProfile);
            _latchedMobilityThreshold =
                default(BossMobilityCapabilityThreshold);
            _latchedMobilityRequiredHorizontalSpeed = 0f;
            _latchedMobilityDifficultyFlags = 0;
            _latchedMobilityGameMode = 0;
            _hasLatchedOutputRoute = false;
            _latchedOutputRoute = default(OutputRouteProfile);
            _summonWhipOutputController = null;
            _latchedMinimumOutputDps = 0f;
            _activeLocomotion = BossLocomotionBaseline.Unspecified;
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            LastCandidateCount = 0;
            if (preserveMobilityRoute)
            {
                _latchedMobilityRouteRequirements = reservedRequirements;
                _latchedMobilityRouteId = reservedRouteId;
                _latchedMobilityRoute = reservedRoute;
                _latchedMobilityThreshold = reservedThreshold;
                _latchedMobilityRequiredHorizontalSpeed = reservedSpeed;
                _latchedMobilityDifficultyFlags = reservedDifficultyFlags;
                _latchedMobilityGameMode = reservedGameMode;
                _hasLatchedOutputRoute = reservedHasOutputRoute;
                _latchedOutputRoute = reservedOutputRoute;
                _summonWhipOutputController = reservedSummonWhipOutput;
                _latchedMinimumOutputDps = reservedMinimumOutputDps;
            }
        }

        private void LatchOutputRoute(in OutputRouteProfile route,
            float minimumEffectiveDps)
        {
            _hasLatchedOutputRoute = true;
            _latchedOutputRoute = route;
            _summonWhipOutputController = null;
            _latchedMinimumOutputDps = minimumEffectiveDps;
        }

        private void LatchSummonWhipOutput(
            SummonWhipOutputController controller,
            float minimumEffectiveDps)
        {
            _hasLatchedOutputRoute = false;
            _latchedOutputRoute = default(OutputRouteProfile);
            _summonWhipOutputController = controller;
            _latchedMinimumOutputDps = minimumEffectiveDps;
        }

        private bool RequirementsMetWithAvailableOutput(
            CombatSnapshot snapshot, BossRequirements requirements,
            out string reason)
        {
            if (snapshot?.Weapon?.NativeProfileRequired != true)
                return requirements.IsMet(snapshot, out reason);

            OutputRouteProfile ordinary;
            SummonWhipOutputController summonWhip;
            float admittedDps;
            if (!TryCreateOutputAdmission(snapshot,
                    requirements.MinimumWeaponDps, out ordinary,
                    out summonWhip, out admittedDps, out reason))
                return false;
            BossMobilityRouteProfile ignored;
            return requirements.IsMetWithAdmittedOutput(snapshot,
                admittedDps, out ignored, out reason);
        }

        private static bool RequirementsMetWithAdmission(
            CombatSnapshot snapshot, BossRequirements requirements,
            float admittedDps, out BossMobilityRouteProfile mobilityRoute,
            out string reason)
        {
            if (snapshot?.Weapon?.NativeProfileRequired == true)
                return requirements.IsMetWithAdmittedOutput(snapshot,
                    admittedDps, out mobilityRoute, out reason);
            return requirements.IsMet(snapshot, out mobilityRoute, out reason);
        }

        private static bool TryCreateOutputAdmission(CombatSnapshot snapshot,
            float minimumDps, out OutputRouteProfile ordinary,
            out SummonWhipOutputController summonWhip,
            out float admittedDps, out string reason)
        {
            ordinary = default(OutputRouteProfile);
            summonWhip = null;
            admittedDps = 0f;
            if (snapshot?.Weapon?.NativeProfileRequired != true)
            {
                reason = null;
                return true;
            }
            if (!FiniteNonnegative(minimumDps))
            {
                reason = "Boss output threshold is invalid";
                return false;
            }

            string ordinaryReason;
            if (OutputRouteContract.TryCreateReady(snapshot, out ordinary,
                    out ordinaryReason))
            {
                if (ordinary.Kind == OutputRouteKind.MeleeProjectile)
                {
                    ordinary = default(OutputRouteProfile);
                    ordinaryReason = "melee projectile route lacks a certified " +
                        "Boss-pattern hit range and native cadence";
                }
                else
                {
                admittedDps = snapshot.Weapon.ApproximateDps;
                if (FiniteNonnegative(admittedDps) &&
                    admittedDps >= minimumDps)
                {
                    reason = null;
                    return true;
                }
                ordinary = default(OutputRouteProfile);
                ordinaryReason = "single-slot conservative output is below " +
                    "the Boss threshold";
                }
            }

            string summonReason;
            if (SummonWhipOutputController.TryCreate(
                    in snapshot.SummonWhipOutput, out summonWhip,
                    out summonReason))
            {
                admittedDps = snapshot.SummonWhipOutput.
                    ConservativeWhipDps;
                if (FiniteNonnegative(admittedDps) &&
                    admittedDps >= minimumDps)
                {
                    reason = null;
                    return true;
                }
                summonWhip = null;
                summonReason = "summon/whip conservative output is below " +
                    "the Boss threshold";
            }

            reason = "no exact production output route is ready (single: " +
                (ordinaryReason ?? "unavailable") + "; summon/whip: " +
                (summonReason ?? "unavailable") + ")";
            admittedDps = 0f;
            return false;
        }

        private bool EnsureLiveOutput(CombatSnapshot snapshot,
            float minimumDps, out string reason)
        {
            if (snapshot?.Weapon?.NativeProfileRequired != true)
            {
                reason = null;
                return true;
            }
            if (_hasLatchedOutputRoute)
                return OutputRouteContract.ValidateLive(snapshot,
                    in _latchedOutputRoute, _latchedMinimumOutputDps,
                    out reason);
            if (_summonWhipOutputController != null)
            {
                if (_summonWhipOutputController.Failed)
                {
                    reason = "summon/whip output controller already failed closed";
                    return false;
                }
                var route = _summonWhipOutputController.Route;
                if (!SummonWhipOutputRouteContract.ValidateLive(
                        in snapshot.SummonWhipOutput, in route, out reason))
                    return false;
                var dps = snapshot.SummonWhipOutput.ConservativeWhipDps;
                if (!FiniteNonnegative(dps) ||
                    dps < _latchedMinimumOutputDps)
                {
                    reason = "live summon/whip output fell below the " +
                        "admitted Boss threshold";
                    return false;
                }
                reason = null;
                return true;
            }

            OutputRouteProfile ordinary;
            SummonWhipOutputController summonWhip;
            float admittedDps;
            if (!TryCreateOutputAdmission(snapshot, minimumDps, out ordinary,
                    out summonWhip, out admittedDps, out reason))
                return false;
            if (summonWhip != null)
                LatchSummonWhipOutput(summonWhip, minimumDps);
            else
                LatchOutputRoute(in ordinary, minimumDps);
            return true;
        }

        private static bool OrdinaryOutputUnavailableAtThreshold(
            CombatSnapshot snapshot, float minimumDps)
        {
            if (snapshot?.Weapon?.NativeProfileRequired != true)
                return false;
            OutputRouteProfile route;
            string reason;
            return !OutputRouteContract.TryCreateReady(snapshot, out route,
                       out reason) ||
                !FiniteNonnegative(snapshot.Weapon.ApproximateDps) ||
                snapshot.Weapon.ApproximateDps < minimumDps;
        }

        private static bool IsLacewingStart(string planId) =>
            string.Equals(planId, "prismatic-lacewing",
                StringComparison.OrdinalIgnoreCase);

        private bool UpdateActivePatternRecovery(
            ActivePatternRecoveryMeasure current, Candidate selected,
            BossDirective directive, int patternTargetKey,
            int desiredHorizontal, int desiredVertical,
            bool emergency, bool stuck,
            out string failure)
        {
            failure = null;
            if (!current.Valid)
            {
                failure = "active Boss recovery has no finite kinematic state";
                return false;
            }

            _activeRecoveryTotalElapsedTicks++;
            var route = ActivePatternRecoveryRoute.Create(in directive,
                patternTargetKey, desiredHorizontal, desiredVertical);
            var routeChanged = !_activeRecoveryRouteKnown ||
                !_activeRecoveryRoute.SameAs(in route);
            if (routeChanged)
            {
                _activeRecoveryRoute = route;
                _activeRecoveryRouteKnown = true;
                // Each stable native route gets its own finite convergence
                // interval. A legitimate attack transition must not inherit
                // the nearly exhausted interval of the route it replaced.
                // The independent total interval below still prevents route
                // churn from retaining input indefinitely.
                _activeRecoveryElapsedTicks = 0;
                _activeRecoveryDeadlineTicks = 0;
                _activeRecoveryPreviousDistance = float.MaxValue;
                _activeRecoveryNoProgressTicks = 0;
                _activeRecoveryClosedTicks = 0;
            }
            if (_activeRecoveryDeadlineTicks <= 0)
            {
                _activeRecoveryDeadlineTicks =
                    ActivePatternRecovery.RecoveryDeadline(current.Distance,
                        Math.Max(2f,
                            _latchedMobilityRequiredHorizontalSpeed),
                        _settings.RecoveryTicks);
                if (_activeRecoveryDeadlineTicks <= 0)
                {
                    failure = "active Boss recovery deadline could not be proved";
                    return false;
                }
            }
            _activeRecoveryElapsedTicks++;

            var observedProgress = ActivePatternRecovery.
                MakesObservedProgress(_activeRecoveryPreviousDistance,
                    in current);
            _activeRecoveryPreviousDistance = current.Distance;
            // A closed velocity/geometry snapshot is not enough when the
            // native player position has stopped responding to the route. A
            // blocked wall, platform edge, or stale adapter can preserve an
            // old velocity while every issued movement input has no effect.
            // Do not let three such frames promote active recovery into a
            // stable loop; the normal no-progress watchdog must hand control
            // back within its finite bound instead.
            if (current.Closed && !stuck)
            {
                _activeRecoveryClosedTicks++;
                _activeRecoveryNoProgressTicks = 0;
            }
            else
            {
                _activeRecoveryClosedTicks = 0;
                var projectedProgress = selected.RecoveryValid &&
                    (selected.RecoveryClosed || selected.RecoveryProgress);
                // A predicted route is useful only while native observation
                // confirms that controls still move the player. Once the
                // ordinary stuck detector fires, forecast motion can no longer
                // mask a blocked platform, wall, or stale adapter snapshot.
                if (observedProgress || projectedProgress && !stuck)
                    // Count uninterrupted intervals with no convergence. A
                    // necessary braking arc can briefly increase geometric
                    // error; once native observation or the bounded rollout
                    // proves that the input is closing the loop again, an old
                    // interval must not remain one frame from failure. The
                    // ordinary stuck detector still disables projected
                    // progress when the real player does not move.
                    _activeRecoveryNoProgressTicks = 0;
                else if (!emergency)
                    _activeRecoveryNoProgressTicks++;
            }

            if (_activeRecoveryClosedTicks >= 3)
            {
                _activePatternRecovery = false;
                _recoveryTicks = 0;
                _stableTicks = 0;
                _activeRecoveryNoProgressTicks = 0;
                _activeRecoveryPreviousDistance = float.MaxValue;
                failure = null;
                return true;
            }

            var noProgressLimit = Math.Max(24,
                _settings.RecoveryTicks * 2);
            if (_activeRecoveryNoProgressTicks >= noProgressLimit)
            {
                failure = "active Boss recovery made no observed progress " +
                    "toward the reviewed movement loop";
                return false;
            }
            if (_activeRecoveryTotalElapsedTicks >=
                _activeRecoveryAbsoluteDeadlineTicks)
            {
                failure = "active Boss recovery exceeded its absolute finite " +
                    "takeover deadline";
                return false;
            }
            if (_activeRecoveryElapsedTicks >=
                _activeRecoveryDeadlineTicks)
            {
                failure = "active Boss recovery did not reach the current " +
                    "reviewed movement route before its finite deadline";
                return false;
            }
            return true;
        }

        private void StampOutputRoute(ref ControlPlan plan)
        {
            if (!_hasLatchedOutputRoute) return;
            plan.PreferredWeaponSlot = _latchedOutputRoute.WeaponSlot;
            plan.OutputRouteKind = _latchedOutputRoute.Kind;
            plan.ExpectedWeaponId = _latchedOutputRoute.WeaponId;
            plan.ExpectedAmmoId = _latchedOutputRoute.AmmoId;
            plan.ExpectedProjectileId = _latchedOutputRoute.ProjectileId;
        }

        private bool ApplyPlannedOutput(CombatSnapshot snapshot,
            TargetSnapshot target, bool attackRequested,
            ref ControlPlan plan, out string reason)
        {
            if (_summonWhipOutputController == null)
            {
                if (target.Key >= 0)
                    ApplyWeaponAim(snapshot, target, attackRequested, ref plan);
                else
                    plan.Fire = false;
                reason = null;
                return true;
            }

            var hasTarget = target.Key >= 0 && target.Life > 0 &&
                !target.Invulnerable;
            var visible = hasTarget && (target.LineOfSightKnown ?
                target.HasLineOfSight : snapshot.LineOfSightToPrimary);
            var decision = _summonWhipOutputController.Tick(
                in snapshot.SummonWhipOutput,
                attackRequested && visible, !plan.Hook);
            if (decision.Failed)
            {
                reason = decision.FailureReason;
                return false;
            }

            var route = _summonWhipOutputController.Route;
            plan.OutputRouteKind = OutputRouteKind.MinionAndWhip;
            plan.SummonWhipOutputRoute = route;
            plan.SummonWhipOutputPhase = decision.Phase;
            plan.SummonWhipOutputAction = decision.Action;
            plan.Fire = decision.UseItem;
            plan.QuickMana = false;
            plan.AimWorld = hasTarget ? target.Center :
                new Vec2(snapshot.Player.Center.X + 160f,
                    snapshot.Player.Center.Y);

            var staffPhase = decision.Phase ==
                    SummonWhipOutputPhase.SelectStaff ||
                decision.Phase == SummonWhipOutputPhase.DeployOnce ||
                decision.Phase == SummonWhipOutputPhase.ConfirmDeployment;
            if (decision.SelectSlot >= 0)
                plan.PreferredWeaponSlot = decision.SelectSlot;
            else
                plan.PreferredWeaponSlot = staffPhase ? route.StaffSlot :
                    route.WhipSlot;
            if (staffPhase)
            {
                plan.ExpectedWeaponId = route.StaffProfile.ItemId;
                plan.ExpectedAmmoId = route.StaffProfile.BuffId;
                plan.ExpectedProjectileId = route.StaffProfile.ProjectileId;
            }
            else
            {
                plan.ExpectedWeaponId = route.WhipProfile.ItemId;
                plan.ExpectedAmmoId = 0;
                plan.ExpectedProjectileId = route.WhipProfile.ProjectileId;
            }
            reason = null;
            return true;
        }

        private void LatchMobilityRoute(BossRequirements requirements,
            CombatSnapshot snapshot, in BossMobilityRouteProfile route)
        {
            _latchedMobilityRouteRequirements = requirements;
            _latchedMobilityRouteId = route.Id;
            _latchedMobilityRoute = route;
            _latchedMobilityThreshold = requirements.MobilityThresholdFor(
                snapshot.Difficulty);
            _latchedMobilityRequiredHorizontalSpeed =
                requirements.RequiredHorizontalSpeedFor(snapshot.Difficulty);
            _latchedMobilityDifficultyFlags = MobilityDifficultyFlags(
                snapshot.Difficulty);
            _latchedMobilityGameMode = snapshot.Difficulty.GameMode;
        }

        private bool MatchesLatchedMobilityDifficulty(
            DifficultySnapshot difficulty)
        {
            return difficulty != null &&
                _latchedMobilityDifficultyFlags ==
                    MobilityDifficultyFlags(difficulty) &&
                _latchedMobilityGameMode == difficulty.GameMode;
        }

        private static int MobilityDifficultyFlags(DifficultySnapshot value)
        {
            if (value == null) return -1;
            var flags = value.GameModeKnown ? 1 : 0;
            if (value.Journey) flags |= 1 << 1;
            if (value.Expert) flags |= 1 << 2;
            if (value.Master) flags |= 1 << 3;
            if (value.Drunk) flags |= 1 << 4;
            if (value.NotTheBees) flags |= 1 << 5;
            if (value.ForTheWorthy) flags |= 1 << 6;
            if (value.Remix) flags |= 1 << 7;
            if (value.Zenith) flags |= 1 << 8;
            if (value.Celebration) flags |= 1 << 9;
            if (value.Constant) flags |= 1 << 10;
            if (value.NoTraps) flags |= 1 << 11;
            if (value.Skyblock) flags |= 1 << 12;
            return flags;
        }

        private static ControlPlan NewPlan(CombatSnapshot snapshot)
        {
            return new ControlPlan
            {
                PreferredWeaponSlot = snapshot?.Weapon?.Slot ?? 0,
                TargetKey = -1,
                TacticalMode = TacticalMode.EstablishPattern
            };
        }

        private void UpdateMotionMemory(CombatSnapshot snapshot)
        {
            var player = snapshot.Player;
            if (_hasPreviousPosition)
            {
                var moved = Vec2.DistanceSquared(player.Position, _previousPosition);
                if (Math.Abs(_lastHorizontal) > 0 && moved < _settings.StuckDistancePixels * _settings.StuckDistancePixels)
                    _stuckTicks++;
                else
                    _stuckTicks = Math.Max(0, _stuckTicks - 2);
            }
            _previousPosition = player.Position;
            _hasPreviousPosition = true;
            if (_directionHoldTicks > 0)
                _directionHoldTicks--;
            if (_gravityCooldown > 0) _gravityCooldown--;
            // Vanilla consumes both edges itself. A scored pulse is followed by
            // a real false-input frame; only a later observed release flag may
            // rearm the optional action.
            if (_gravityReleasePending && snapshot.Mobility.GravityFlip.Known &&
                snapshot.Mobility.GravityFlip.ReleaseUp) _gravityReleasePending = false;
            if (_gravityReturnPending && snapshot.Mobility.GravityFlip.Known &&
                snapshot.Mobility.GravityInverted == _gravityReturnInverted)
            {
                // Either the reviewed return edge completed, or a late runtime
                // validation correctly stripped the original edge. In both cases
                // vanilla is already in the gravity state that Chaite must preserve.
                _gravityReturnPending = false;
                _gravityReturnSupport = default(SupportSpan);
            }
            if (_dashReleasePending && snapshot.Mobility.EyeShieldDash.Known &&
                snapshot.Mobility.EyeShieldDash.ReleaseDash) _dashReleasePending = false;
        }

        private int PatternHorizontal(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive)
        {
            if (directive.UseExplicitMovement)
                return ClampIntent(directive.HorizontalIntent);
            if (_directionHoldTicks > 0 && directive.Pattern != BossPattern.PerpendicularDashDodge)
                return _patternDirection;

            var player = snapshot.Player.Center;
            var delta = player - target.Center;
            var absX = Math.Abs(delta.X);
            var ideal = Math.Max(80f, directive.IdealDistance);
            if (directive.HorizontalIntent != 0)
            {
                var intent = ClampIntent(directive.HorizontalIntent);
                if (!directive.OwnsHorizontalClosure &&
                    (directive.Pattern == BossPattern.Runway ||
                     directive.Pattern == BossPattern.HorizontalKite ||
                     directive.Pattern == BossPattern.ProjectileLanes) &&
                    intent == AwayX(player, target.Center))
                {
                    // Running away forever eventually despawns a slower Boss.
                    // Coast once a safe firing gap is established; recover toward
                    // a distant Boss only outside a wider hysteresis band. The
                    // collision predictor can still override this in an emergency.
                    if (absX > ideal * 1.8f) return -intent;
                    if (absX > ideal * 1.15f) return 0;
                }
                return intent;
            }
            int wanted;
            switch (directive.Pattern)
            {
                case BossPattern.CircleOrbit:
                case BossPattern.EllipseOrbit:
                    wanted = delta.Y * _patternDirection > directive.VerticalOffset ? -_patternDirection : _patternDirection;
                    break;
                case BossPattern.StayCloseJump:
                    wanted = absX > ideal * 1.15f ? -AwayX(player, target.Center) : AwayX(player, target.Center);
                    break;
                case BossPattern.PerpendicularDashDodge:
                    wanted = target.Velocity.X == 0f ? AwayX(player, target.Center) : -Math.Sign(target.Velocity.X);
                    break;
                default:
                    if (absX < ideal * .72f)
                        wanted = AwayX(player, target.Center);
                    else if (absX > ideal * 1.28f)
                        wanted = -AwayX(player, target.Center);
                    else
                        wanted = _lastHorizontal == 0 ? _patternDirection : _lastHorizontal;
                    break;
            }
            return ClampIntent(wanted);
        }

        private int PatternVertical(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive)
        {
            var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
            if (directive.UseExplicitMovement)
                return ClampIntent(directive.VerticalIntent) * gravitySign;
            if (directive.VerticalIntent != 0)
                return ClampIntent(directive.VerticalIntent) * gravitySign;

            var wantedY = target.Center.Y + directive.VerticalOffset;
            if (directive.FloorClearance > 0f && snapshot.Arena.HasFloor && snapshot.Arena.LocalOpenBounds.Height > 0f)
                wantedY = Math.Min(wantedY, snapshot.Arena.LocalOpenBounds.Bottom - directive.FloorClearance);
            var error = wantedY - snapshot.Player.Center.Y;
            if (error < -42f)
                return gravitySign;
            if (error > 70f)
                return -gravitySign;
            if (directive.Pattern == BossPattern.CircleOrbit || directive.Pattern == BossPattern.EllipseOrbit)
                return _patternDirection * (snapshot.Player.Center.X >= target.Center.X ? 1 : -1) * gravitySign;
            return 0;
        }

        private void RespectArenaEdges(CombatSnapshot snapshot, ref int horizontal, ref int vertical,
            bool explicitMovement = false, bool ownsMovementClosure = false,
            bool ownsHorizontalClosure = false)
        {
            var arena = snapshot.Arena;
            // Start a turn early enough to brake, instead of steering into a dead end
            // and hoping the immediate evasion layer can undo accumulated momentum.
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = explicitMovement ? Math.Max(.000001f, snapshot.Player.RunSlowdown) :
                Math.Max(.08f, snapshot.Player.RunAcceleration);
            var turnMargin = _settings.ArenaEdgeMarginPixels + speed * speed /
                (2f * braking) + speed * _settings.DirectionHysteresisTicks;
            if (!explicitMovement)
                turnMargin = Math.Min(turnMargin, Math.Max(_settings.ArenaEdgeMarginPixels, arena.HorizontalClearance * .42f));
            if (!ownsHorizontalClosure && arena.ClearanceLeft < turnMargin && horizontal < 0)
            {
                if (explicitMovement) horizontal = 0;
                else ReversePattern(1, ref horizontal);
            }
            else if (!ownsHorizontalClosure && arena.ClearanceRight < turnMargin && horizontal > 0)
            {
                if (explicitMovement) horizontal = 0;
                else ReversePattern(-1, ref horizontal);
            }

            var worldVertical = vertical * (snapshot.Mobility.GravityInverted ? -1 : 1);
            if (arena.ClearanceUp < _settings.ArenaVerticalMarginPixels && worldVertical > 0)
                vertical = ownsMovementClosure ? 0 : snapshot.Mobility.GravityInverted ? 1 : -1;
            // A floor is a replenishment destination, not an obstacle to flee forever.
            else if (arena.ClearanceDown < _settings.ArenaVerticalMarginPixels && worldVertical < 0 &&
                     !arena.HasFloor && !snapshot.Player.OnGround)
                vertical = ownsMovementClosure ? 0 : snapshot.Mobility.GravityInverted ? -1 : 1;
        }

        private void ReversePattern(int newDirection, ref int horizontal)
        {
            _patternDirection = newDirection;
            horizontal = newDirection;
            _directionHoldTicks = _settings.DirectionHysteresisTicks;
        }

        private void BudgetFlight(CombatSnapshot snapshot, ref int horizontal, ref int vertical)
        {
            if (_activeLocomotion !=
                BossLocomotionBaseline.FinitePlayerFlight)
            {
                _restoringFlight = false;
                _landingSupport = default(SupportSpan);
                return;
            }
            var inverted = snapshot.Mobility.GravityInverted;
            _landingSupport = snapshot.Arena.RecoverySupport;
            if (!_landingSupport.Valid || _landingSupport.Inverted != inverted)
                _landingSupport = inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
            if (!snapshot.Mobility.HasFiniteFlightResource ||
                snapshot.Mobility.MountActive && snapshot.Mobility.MountCanFly ||
                HasExactGravityEdge(snapshot))
            {
                // A normal ground/double jump is not an exhausted flight cycle.
                // Preserve the Boss strategy's horizontal and vertical intents.
                _restoringFlight = false;
                return;
            }
            if (snapshot.Player.OnGround || snapshot.Mobility.FlightResourceFraction >= _settings.FlightResumeFraction)
                _restoringFlight = false;
            else if (snapshot.Mobility.FlightResourceFraction < _settings.FlightReserveFraction)
                _restoringFlight = true;
            if (_landingSupport.Valid && !snapshot.Player.OnGround && _landingSupport.Inverted == inverted &&
                !(_landingSupport.OneWay && inverted))
            {
                var player = snapshot.Player;
                var halfWidth = player.Width * .5f;
                var speed = Math.Abs(player.Velocity.X);
                var brakeTicks = speed / Math.Max(.08f, player.RunAcceleration + player.RunSlowdown);
                var margin = Math.Min(Math.Max(0f, (_landingSupport.Right - _landingSupport.Left - player.Width) * .25f),
                    32f + speed * (brakeTicks * .5f + 8f));
                var left = _landingSupport.Left + halfWidth + margin;
                var right = _landingSupport.Right - halfWidth - margin;
                if (left > right) left = right = (_landingSupport.Left + _landingSupport.Right) * .5f;
                var projectedX = player.Center.X + player.Velocity.X * 8f;
                var outside = Math.Max(0f, Math.Max(left - projectedX, projectedX - right));
                var returnTicks = outside / Math.Max(1f, player.MaxRunSpeed) + brakeTicks;
                var remainingFlight = player.Flight.Known ? FlightMotion.RemainingWingTicks(in player.Flight) :
                    Math.Max(player.WingTime, player.RocketTime);
                if (outside > 0f && (snapshot.Mobility.FlightResourceFraction < Math.Min(.5f, _settings.FlightReserveFraction + .25f) ||
                    remainingFlight < returnTicks + 24f)) _restoringFlight = true;
                if (_restoringFlight)
                {
                    // This is the desired landing route, BEFORE the emergency
                    // candidate search. Never overwrite its chosen dodge afterward.
                    horizontal = player.Center.X < left ? 1 : player.Center.X > right ? -1 : 0;
                    if (_landingSupport.OneWay && vertical < 0) vertical = 0;
                }
            }
            if (_restoringFlight && vertical > 0) vertical = 0;
        }

        private Candidate FindBestCandidate(CombatSnapshot snapshot, TargetSnapshot target,
            BossDirective directive, int desiredHorizontal, int desiredVertical,
            BossDashBaseline requiredDash = BossDashBaseline.None,
            bool requireRecovery = false,
            ActivePatternRecoveryMeasure currentRecovery =
                default(ActivePatternRecoveryMeasure))
        {
            _scoreRecoveryRequired = requireRecovery;
            _scoreRecoveryCurrent = currentRecovery;
            LastCandidateCount = 1;
            _lateMobilityFallback = InvalidCandidate(desiredHorizontal,
                desiredVertical, MobilityCandidateAction.None);
            var best = ScoreCandidate(snapshot, target, directive, desiredHorizontal, desiredVertical, desiredHorizontal, desiredVertical);
            var edgeFreeBest = best;
            CaptureLateMobilityFallback(edgeFreeBest);
            // The daytime Empress horizontal-dash phase is the one reviewed
            // phase whose lower-bound formula names the shield dash even in
            // Classic.  Other Classic charges keep the dash optional, exactly
            // as their baseline declares it.
            var empressDayDash = directive.PreferDash &&
                directive.PhaseId != null &&
                directive.PhaseId.Contains("day-rage-") &&
                directive.PhaseId.Contains("horizontal-dash");
            var empressPreDashWithStreak = directive.PhaseId != null &&
                directive.PhaseId.Contains("day-rage-") &&
                directive.PhaseId.Contains("reposition-before-horizontal-dash") &&
                HasRainbowStreakThreat(snapshot);
            var preferRequiredShieldDash = directive.PreferDash &&
                (requiredDash == BossDashBaseline.ShieldOfCthulhu ||
                 empressDayDash) &&
                CanScoreEyeShieldDash(snapshot);
            Candidate gravityReturn = default(Candidate);
            var hasGravityReturn = false;
            if (_gravityReturnPending && snapshot.Mobility.GravityInverted != _gravityReturnInverted &&
                !directive.OwnsMovementClosure && !directive.OwnsHorizontalClosure &&
                CanScoreGravityReturn(snapshot))
            {
                hasGravityReturn = TryScoreGravityReturn(snapshot, target, directive,
                    desiredHorizontal, desiredVertical, best.Posture, out gravityReturn);
                // Restoration is part of the already-authorized escape contract.
                // Once its complete route is below the ordinary safe threshold,
                // take it even though the optional-edge tie-break cost is higher.
                if (hasGravityReturn && gravityReturn.Hazard < _settings.PatternSafeRiskThreshold &&
                    (!requireRecovery || gravityReturn.RecoveryClosed ||
                     gravityReturn.RecoveryProgress))
                    return gravityReturn;
            }
            // Stable-pattern controller owns the route; alternative controls are only
            // considered when its predicted trajectory is unsafe or we are recovering.
            if (best.Hazard < _settings.PatternSafeRiskThreshold &&
                _stuckTicks < _settings.StuckTicksBeforeRecovery &&
                !preferRequiredShieldDash &&
                (!requireRecovery || best.RecoveryClosed))
                return best;
            for (var horizontal = -1; horizontal <= 1; horizontal++)
            {
                if (directive.OwnsHorizontalClosure && horizontal != desiredHorizontal) continue;
                if (directive.OwnsMovementClosure && horizontal != desiredHorizontal &&
                    (desiredHorizontal == 0 || horizontal != 0)) continue;
                for (var posture = -1; posture <= 1; posture++)
                {
                    if (directive.OwnsMovementClosure && posture != desiredVertical) continue;
                    if (_activeLocomotion ==
                            BossLocomotionBaseline.FinitePlayerFlight &&
                        directive.OwnsHorizontalClosure &&
                        FlightMotion.RemainingWingTicks(in snapshot.Player.Flight) <= 0f &&
                        posture != desiredVertical) continue;
                    if (horizontal == desiredHorizontal && posture == desiredVertical) continue;
                    LastCandidateCount++;
                    var candidate = ScoreCandidate(snapshot, target, directive, horizontal, posture, desiredHorizontal, desiredVertical, best.Score);
                    if (IsBetterCandidate(candidate, best)) best = candidate;
                }
            }
            edgeFreeBest = best;
            CaptureLateMobilityFallback(edgeFreeBest);
            if (hasGravityReturn && IsBetterCandidate(gravityReturn, best))
                best = gravityReturn;
            // Feather Fall's Up branch is a distinct vanilla input, not a jump.
            // Only introduce it as an emergency/recovery alternative after the
            // ordinary low-gear candidate was found unsafe. Possessing a
            // gravity-control item would make the same key edge flip gravity;
            // mounts and grapples also reinterpret Up, so those combinations
            // remain fail-closed until their joint transition models exist.
            if (!directive.OwnsMovementClosure && !directive.OwnsHorizontalClosure &&
                FlightMotion.CanRequestFeatherFallPotionUp(snapshot.Player,
                    snapshot.Mobility) &&
                !_gravityReleasePending)
            {
                for (var horizontal = -1; horizontal <= 1; horizontal++)
                {
                    LastCandidateCount++;
                    var candidate = ScoreCandidate(snapshot, target, directive, horizontal,
                        FeatherFallUpPosture, desiredHorizontal, desiredVertical, best.Score);
                    if (IsBetterCandidate(candidate, best)) best = candidate;
                }
            }

            // High-mobility tools are an additional trajectory dimension, not
            // a post-plan button press. They are considered only after the
            // ordinary route was proved unsafe, and normally never bypass a
            // Boss-owned movement closure.  A reviewed *required* dash is the
            // one exception: Fishron's expert/master charge contract names
            // Shield of Cthulhu as part of the route, so score that edge with
            // ownership temporarily released while retaining the explicit
            // controller's pattern and late-fallback certificate.
            if (!directive.OwnsMovementClosure && !directive.OwnsHorizontalClosure ||
                preferRequiredShieldDash || empressPreDashWithStreak)
            {
                if (CanScoreEyeShieldDash(snapshot))
                {
                    if (preferRequiredShieldDash)
                    {
                        // A dash named by the current Boss/difficulty baseline
                        // is a real part of that phase's route. Prefer it on a
                        // hazard tie only after the complete native dash,
                        // braking and return closure has passed; an unsafe or
                        // incomplete dash still loses to ordinary movement.
                        var dashDirective = directive;
                        dashDirective.OwnsMovementClosure = false;
                        dashDirective.OwnsHorizontalClosure = false;
                        var preferredDash = InvalidCandidate(desiredHorizontal,
                            desiredVertical, MobilityCandidateAction.EyeShieldDash);
                        ScoreMobilityPostures(snapshot, target, dashDirective,
                            desiredHorizontal, desiredVertical,
                            MobilityCandidateAction.EyeShieldDash,
                            ref preferredDash);
                        // Required means required when the complete native
                        // dash/brake/return rollout is valid.  Do not let the
                        // ordinary candidate's lower cosmetic score suppress
                        // a declared baseline; an unsafe/incomplete rollout
                        // remains InvalidCandidate and therefore keeps the
                        // safe ordinary route.
                        if (preferredDash.Score < float.MaxValue &&
                            preferredDash.Hazard < _settings.EmergencyRiskThreshold)
                            best = preferredDash;
                    }
                    else
                    {
                        ScoreMobilityPostures(snapshot, target, directive,
                            desiredHorizontal, desiredVertical,
                            MobilityCandidateAction.EyeShieldDash, ref best);
                    }
                }
                if (CanScoreGravityFlip(snapshot, directive))
                    ScoreMobilityPostures(snapshot, target, directive, desiredHorizontal,
                        desiredVertical, MobilityCandidateAction.GravityFlip, ref best);
            }
            return best;
        }

        private void ScoreMobilityPostures(CombatSnapshot snapshot, TargetSnapshot target,
            BossDirective directive, int desiredHorizontal, int desiredVertical,
            MobilityCandidateAction action, ref Candidate best)
        {
            var alternatePosture = best.Posture;
            for (var posturePass = 0; posturePass < 2; posturePass++)
            {
                var posture = posturePass == 0 ? desiredVertical : alternatePosture;
                if (posture < -1 || posture > 1 || posturePass == 1 && posture == desiredVertical)
                    continue;
                for (var horizontal = -1; horizontal <= 1; horizontal++)
                {
                    LastCandidateCount++;
                    var candidate = ScoreCandidateWithMobility(snapshot, target, directive,
                        horizontal, posture, desiredHorizontal, desiredVertical, best.Score, action);
                    if (IsBetterCandidate(candidate, best)) best = candidate;
                }
            }
        }

        private bool CanScoreGravityFlip(CombatSnapshot snapshot, BossDirective directive)
        {
            if (!directive.AllowGravityFlip || !snapshot.Mobility.CanFlipGravity ||
                snapshot.Mobility.MountActive || snapshot.Mobility.Grappling ||
                _gravityReturnPending || _gravityCooldown != 0 || _gravityReleasePending ||
                snapshot.Player.Flight.Known)
                return false;
            var clearance = snapshot.Mobility.GravityInverted
                ? snapshot.Arena.ClearanceDown : snapshot.Arena.ClearanceUp;
            if (clearance <= _settings.ArenaVerticalMarginPixels * 2f) return false;
            GravityFlipCandidate candidate;
            return GravityFlipMotion.TryCreatePotionCandidate(snapshot.Mobility.GravityFlip,
                snapshot.Mobility.GravityInverted ? 1f : -1f, out candidate);
        }

        private static bool HasExactGravityEdge(CombatSnapshot snapshot)
        {
            if (!snapshot.Mobility.CanFlipGravity || snapshot.Mobility.MountActive ||
                snapshot.Mobility.Grappling) return false;
            GravityFlipCandidate candidate;
            return GravityFlipMotion.TryCreatePotionCandidate(snapshot.Mobility.GravityFlip,
                snapshot.Mobility.GravityInverted ? 1f : -1f, out candidate);
        }

        private bool CanScoreEyeShieldDash(CombatSnapshot snapshot)
        {
            if (!snapshot.Mobility.CanDash || !snapshot.Mobility.DashReady ||
                snapshot.Mobility.MountActive || snapshot.Mobility.Grappling ||
                _gravityReturnPending || _dashReleasePending || snapshot.Player.Flight.Known &&
                snapshot.Mobility.GravityInverted) return false;
            // At least one planned direction must have an exact native probe.
            return snapshot.Mobility.DashLeftProbeKnown || snapshot.Mobility.DashRightProbeKnown;
        }

        private bool CanScoreGravityReturn(CombatSnapshot snapshot)
        {
            if (!_gravityReturnPending || !snapshot.Mobility.CanFlipGravity ||
                snapshot.Mobility.MountActive ||
                snapshot.Mobility.Grappling || _gravityReleasePending ||
                snapshot.Player.Flight.Known) return false;
            GravityFlipCandidate candidate;
            return GravityFlipMotion.TryCreatePotionCandidate(snapshot.Mobility.GravityFlip,
                _gravityReturnInverted ? -1f : 1f, out candidate);
        }

        private bool TryScoreGravityReturn(CombatSnapshot snapshot, TargetSnapshot target,
            BossDirective directive, int desiredHorizontal, int desiredVertical,
            int alternatePosture, out Candidate best)
        {
            best = InvalidCandidate(desiredHorizontal, desiredVertical,
                MobilityCandidateAction.GravityRestore);
            for (var posturePass = 0; posturePass < 3; posturePass++)
            {
                var posture = posturePass == 0 ? desiredVertical :
                    posturePass == 1 ? alternatePosture : 0;
                if (posture < -1 || posture > 1 ||
                    posturePass == 1 && posture == desiredVertical ||
                    posturePass == 2 && (posture == desiredVertical ||
                        posture == alternatePosture)) continue;
                for (var horizontal = -1; horizontal <= 1; horizontal++)
                {
                    LastCandidateCount++;
                    var candidate = ScoreCandidateWithMobility(snapshot, target, directive,
                        horizontal, posture, desiredHorizontal, desiredVertical,
                        float.MaxValue, MobilityCandidateAction.GravityRestore);
                    if (IsBetterCandidate(candidate, best)) best = candidate;
                }
            }
            return best.Score < float.MaxValue;
        }

        private bool IsBetterCandidate(Candidate candidate, Candidate incumbent)
        {
            if (!_scoreRecoveryRequired)
                return candidate.Score < incumbent.Score;
            if (candidate.Score == float.MaxValue) return false;
            if (incumbent.Score == float.MaxValue) return true;

            // During active-encounter acquisition, first stay inside the same
            // ordinary safe envelope used by the stable planner. Within that
            // envelope a candidate which reaches or advances toward the loop
            // beats a safe but stationary local optimum. If every alternative
            // is hazardous, retain the ordinary threat score so emergency
            // avoidance remains authoritative.
            var candidateSafe = candidate.Hazard <
                _settings.PatternSafeRiskThreshold;
            var incumbentSafe = incumbent.Hazard <
                _settings.PatternSafeRiskThreshold;
            if (candidateSafe != incumbentSafe) return candidateSafe;
            if (candidateSafe)
            {
                var candidateAdvances = candidate.RecoveryClosed ||
                    candidate.RecoveryProgress;
                var incumbentAdvances = incumbent.RecoveryClosed ||
                    incumbent.RecoveryProgress;
                if (candidateAdvances != incumbentAdvances)
                    return candidateAdvances;
                if (candidateAdvances && incumbentAdvances &&
                    Math.Abs(candidate.RecoveryDistance -
                        incumbent.RecoveryDistance) > .5f)
                    return candidate.RecoveryDistance <
                        incumbent.RecoveryDistance;
            }
            return candidate.Score < incumbent.Score;
        }

        private Candidate ScoreCandidate(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive,
            int horizontal, int posture, int desiredHorizontal, int desiredVertical,
            float incumbentScore = float.MaxValue)
        {
            return ScoreCandidateWithMobility(snapshot, target, directive, horizontal,
                posture, desiredHorizontal, desiredVertical, incumbentScore,
                MobilityCandidateAction.None);
        }

        private Candidate ScoreCandidateWithMobility(CombatSnapshot snapshot,
            TargetSnapshot target, BossDirective directive, int horizontal, int posture,
            int desiredHorizontal, int desiredVertical, float incumbentScore,
            MobilityCandidateAction mobilityAction)
        {
            var player = snapshot.Player;
            var position = player.Position;
            var velocity = player.Velocity;
            var risk = 0f;
            var step = _stepTicks;
            var mountedSpeed = snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f;
            var inverted = snapshot.Mobility.GravityInverted;
            var useFinitePlayerFlight = _activeLocomotion ==
                BossLocomotionBaseline.FinitePlayerFlight;
            var flyingMount = false;
            var flightTicks = useFinitePlayerFlight
                ? Math.Max(player.WingTime, player.RocketTime) : 0f;
            var canInitialJump = player.OnGround || flightTicks > 0f || flyingMount;
            var nativeJump = player.Jump.Known;
            var jumpState = player.Jump;
            var flightState = useFinitePlayerFlight
                ? player.Flight : default(FlightSnapshot);
            var dashState = snapshot.Mobility.EyeShieldDash;
            var gravityState = snapshot.Mobility.GravityFlip;
            if (IsGravityAction(mobilityAction))
            {
                GravityFlipCandidate gravityCandidate;
                if (!GravityFlipMotion.TryCreatePotionCandidate(gravityState,
                    mobilityAction == MobilityCandidateAction.GravityRestore
                        ? (_gravityReturnInverted ? -1f : 1f)
                        : (inverted ? 1f : -1f), out gravityCandidate))
                    return InvalidCandidate(horizontal, posture, mobilityAction);
            }
            else if (mobilityAction == MobilityCandidateAction.EyeShieldDash)
            {
                EyeShieldDashCandidate dashCandidate;
                if (!TryPrepareDashCandidate(snapshot, horizontal, ref dashState, out dashCandidate))
                    return InvalidCandidate(horizontal, posture, mobilityAction);
            }
            var gravityDirection = inverted ? -1f : 1f;
            var jumpRequested = posture == JumpPosture;
            var featherFallUp = posture == FeatherFallUpPosture;
            var jumpAction = jumpRequested ? (directive.JumpAction == JumpAction.Release ? JumpAction.Default : directive.JumpAction) :
                directive.JumpAction == JumpAction.Release ? JumpAction.Release : JumpAction.Default;
            var grounded = player.OnGround && (nativeJump || posture <= 0);
            var standingY = player.Position.Y;
            var support = inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
            var actualFoot = inverted ? player.Position.Y : player.Position.Y + player.Height;
            if (player.OnGround && (!support.OverlapsBody(player.Position.X, player.Width) ||
                 support.Inverted != inverted || Math.Abs(actualFoot - support.SurfaceY) > 2f))
            {
                // Native OnGround is evidence of current contact, even on shapes
                // the flat-row scanner deliberately excludes. Preserve only this
                // actual footprint, never extrapolate it across open arena space.
                support = new SupportSpan { Valid = true, Inverted = inverted,
                    OneWay = player.OnOneWaySupport,
                    Left = player.Position.X, Right = player.Position.X + player.Width, SurfaceY = actualFoot };
            }
            var originalSupport = support;
            var recoveryAnchor = mobilityAction == MobilityCandidateAction.GravityRestore
                ? _gravityReturnAnchor : position;
            var recoveryInverted = mobilityAction == MobilityCandidateAction.GravityRestore
                ? _gravityReturnInverted : inverted;
            var recoveryWasGrounded = mobilityAction == MobilityCandidateAction.GravityRestore
                ? _gravityReturnWasGrounded : player.OnGround;
            var recoverySupport = mobilityAction == MobilityCandidateAction.GravityRestore
                ? _gravityReturnSupport : originalSupport;
            if (grounded && !SupportGeometry.RetainsFooting(support, position.X, player.Width,
                inverted, posture < 0)) grounded = false;
            var previousBounds = player.BoundsAt(position);
            InitializeTargetedThreatStates(snapshot);
            InitializeEmpressDashThreatStates();

            if (!nativeJump && mobilityAction == MobilityCandidateAction.None &&
                jumpRequested && canInitialJump)
            {
                if (player.OnGround)
                    velocity.Y = -(5.01f + Math.Max(0f, player.JumpSpeedBoost)) * gravityDirection;
                else
                    velocity.Y -= .55f * step * gravityDirection;
            }

            for (var tick = step; tick <= _horizonTicks; tick += step)
            {
                var beforeStep = position;
                // Native hold/release/extra-jump transitions have one-tick
                // semantics even when threat scoring uses a coarser step.
                if (nativeJump)
                {
                    for (var substep = 0; substep < step; substep++)
                        if (!AdvanceNativeJumpWithMobility(snapshot, horizontal, posture, jumpAction,
                            mountedSpeed, ref originalSupport, ref inverted,
                            mobilityAction, tick - step + substep + 1,
                            ref gravityState, ref dashState, ref jumpState,
                            ref flightState, ref position, ref velocity, ref grounded,
                            ref support, ref standingY))
                            return InvalidCandidate(horizontal, posture, mobilityAction);
                }
                else if (mobilityAction != MobilityCandidateAction.None)
                {
                    for (var substep = 0; substep < step; substep++)
                        if (!AdvanceOptionalGenericTick(snapshot, horizontal, posture,
                            mountedSpeed, ref originalSupport, ref inverted, jumpRequested,
                            flyingMount, flightTicks, canInitialJump,
                            tick - step + substep + 1, mobilityAction,
                            ref gravityState, ref dashState,
                            ref position, ref velocity, ref grounded, ref support, ref standingY))
                            return InvalidCandidate(horizontal, posture, mobilityAction);
                }
                else
                {
                float horizontalTravel;
                var verticalTravel = 0f;
                velocity.X = HorizontalMotion.Advance(player, velocity.X, horizontal, grounded, step, out horizontalTravel, mountedSpeed);
                position.X += horizontalTravel;
                if (grounded && !SupportGeometry.RetainsFooting(support, position.X, player.Width,
                    inverted, posture < 0)) grounded = false;
                if (grounded) velocity.Y = 0f;
                else
                {
                    var gravity = Math.Max(.1f, Math.Abs(player.Gravity));
                    if (jumpRequested && (flyingMount || tick <= flightTicks))
                    {
                        velocity.Y += gravity * gravityDirection * step;
                        velocity.Y -= (gravity + .35f) * gravityDirection * step;
                        verticalTravel = velocity.Y * step;
                    }
                    else
                    {
                        // Even an otherwise unsupported jump profile can retain
                        // vanilla's self-contained feather-fall gravity branch.
                        // Integrate each native tick so its /5 -> /10 Up snap is
                        // not flattened by the coarser threat-sampling step.
                        for (var substep = 0; substep < step; substep++)
                        {
                            velocity.Y = JumpMotion.ApplyGravity(velocity.Y, gravity,
                                player.MaxFallSpeed, inverted,
                                snapshot.Mobility.FeatherFall, featherFallUp, posture < 0);
                            verticalTravel += velocity.Y;
                        }
                    }
                }
                position.Y += verticalTravel;
                if (grounded) position.Y = standingY;

                if (!grounded)
                {
                    var first = originalSupport;
                    var second = snapshot.Arena.RecoverySupport;
                    if (second.Valid && second.Inverted == inverted &&
                        (!first.Valid || (second.SurfaceY - first.SurfaceY) * gravityDirection < 0f))
                    {
                        first = second;
                        second = originalSupport;
                    }
                    var landed = SupportGeometry.TryLand(first, beforeStep, ref position, ref velocity,
                        player.Width, player.Height, inverted, posture < 0);
                    if (landed) support = first;
                    else if (SupportGeometry.TryLand(second, beforeStep, ref position, ref velocity,
                        player.Width, player.Height, inverted, posture < 0))
                    {
                        landed = true;
                        support = second;
                    }
                    if (landed)
                    {
                        standingY = position.Y;
                        grounded = true;
                    }
                }
                }

                var bounds = player.BoundsAt(position);
                if (OutsideWorldOrArena(snapshot, bounds))
                    risk += _settings.CollisionPenalty;

                var timeWeight = 1f / (1f + tick * .035f);
                var cacheOffset = (tick / step - 1) * _relevantThreats.Count;
                for (var i = 0; i < _relevantThreats.Count; i++)
                {
                    var cacheIndex = cacheOffset + i;
                    ThreatStep predicted;
                    if (_empressDashThreatStates[i].Valid)
                    {
                        predicted = PredictEmpressDashCandidateThreat(
                            _relevantThreats[i], i, previousBounds.Center,
                            bounds.Center, tick, directive, cacheIndex);
                    }
                    else if (_targetedThreatStates[i].Valid)
                    {
                        predicted = PredictTargetedCandidateThreat(
                            _relevantThreats[i], i, previousBounds.Center,
                            bounds.Center, tick, directive, cacheIndex);
                    }
                    else
                    {
                        if (!_settings.CacheThreatPrediction)
                            _threatSteps[cacheIndex] = PredictThreat(
                                _relevantThreats[i], tick, directive);
                        predicted = _threatSteps[cacheIndex];
                    }
                    if (!predicted.Active) continue;
                    var liveBounds = bounds;
                    if (predicted.LifeFraction < 1f)
                    {
                        var fraction = predicted.LifeFraction;
                        liveBounds = new RectF(previousBounds.X + (bounds.X - previousBounds.X) * fraction,
                            previousBounds.Y + (bounds.Y - previousBounds.Y) * fraction, bounds.Width, bounds.Height);
                    }
                    if (SweptIntersects(in previousBounds, in liveBounds, in predicted.Before, in predicted.After))
                        risk += predicted.DamageRisk;
                    else
                    {
                        var separation = liveBounds.SeparationSquared(predicted.After);
                        if (separation < 14400f)
                            risk += NearMissPenalty(_relevantThreats[i]) *
                                (1f - separation / 14400f) * timeWeight;
                    }
                }
                var beamOffset = (tick / step - 1) * _relevantBeams.Count;
                var playerSweep = BeamGeometry.Union(previousBounds, bounds);
                for (var i = 0; i < _relevantBeams.Count; i++)
                {
                    var beamIndex = beamOffset + i;
                    if (!_settings.CacheThreatPrediction) _beamSteps[beamIndex] = PredictBeam(_relevantBeams[i], tick);
                    ref var beam = ref _beamSteps[beamIndex];
                    if (!beam.Shape.Active) continue;
                    // All three tapered Sun Dance lobes belong to one projectile;
                    // overlapping lobes never triple-count its damage.
                    if (BeamGeometry.Intersects(playerSweep, beam.Shape,
                            BeamSafetyMargin(_relevantBeams[i])))
                        risk += beam.DamageRisk;
                    else
                    {
                        var separation = BeamGeometry.SeparationSquared(playerSweep, beam.Shape);
                        if (separation < 14400f)
                            risk += NearMissPenalty(_relevantBeams[i]) *
                                (1f - separation / 14400f) * timeWeight;
                    }
                }
                previousBounds = bounds;
                // All remaining default costs are nonnegative. A losing partial path
                // cannot become the winner later, so do not simulate its unused tail.
                if (!_scoreRecoveryRequired &&
                    mobilityAction == MobilityCandidateAction.None &&
                    _settings.EnableScorePruning && risk >= incumbentScore && _settings.CollisionPenalty >= 0f && _settings.DamagePenalty >= 0f &&
                    _settings.NearMissPenalty >= 0f && _settings.PatternDeviationPenalty >= 0f &&
                    _settings.VerticalPatternDeviationPenalty >= 0f && _settings.MovementChangePenalty >= 0f)
                    return new Candidate { Horizontal = horizontal, Posture = posture,
                        JumpAction = jumpAction, MobilityAction = mobilityAction,
                        Score = risk, Hazard = risk };
            }

            if (mobilityAction != MobilityCandidateAction.None &&
                !TryRolloutMobilityRecovery(snapshot, directive, mobilityAction,
                    recoveryAnchor, recoveryInverted, recoveryWasGrounded, recoverySupport,
                    ref gravityState, ref dashState, ref position, ref velocity,
                    ref inverted, ref grounded, ref originalSupport, ref support,
                    ref jumpState, ref flightState, ref standingY, ref risk))
                return InvalidCandidate(horizontal, posture, mobilityAction);

            var hazard = risk;
            var futureCenter = new Vec2(position.X + player.Width * .5f, position.Y + player.Height * .5f);
            var predictedTarget = target.Center + target.Velocity * _horizonTicks;
            var delta = futureCenter - predictedTarget;
            var distanceError = Math.Abs(delta.Length - directive.IdealDistance);
            if (!directive.UseExplicitMovement)
            {
                // Explicit Boss controllers already own their long-lived route,
                // anchor and vertical goal. Target-relative geometry here would
                // silently turn the emergency search back into a greedy orbit
                // (notably making Destroyer recovery chase the head altitude).
                risk += distanceError * .8f;
                risk += Math.Abs(delta.Y - directive.VerticalOffset) * .18f;
            }
            if (horizontal != desiredHorizontal)
                risk += _settings.PatternDeviationPenalty;
            if (posture != desiredVertical)
                risk += _settings.VerticalPatternDeviationPenalty;
            if (directive.ForceContinuousMovement && horizontal == 0)
                risk += 22f;
            if (horizontal != _lastHorizontal)
                risk += _settings.MovementChangePenalty;
            if (!directive.UseExplicitMovement && !snapshot.LineOfSightToPrimary)
                risk += distanceError * .1f;
            // Landing/restoration is a deliberate sub-loop, not an accidental failure
            // to follow an airborne preferred path. Emergency candidates may override it.
            if (_restoringFlight && posture > 0) risk += _settings.PatternDeviationPenalty * 2f;
            if (_restoringFlight && _landingSupport.Valid)
            {
                var outside = Math.Max(0f, Math.Max(_landingSupport.Left - position.X,
                    position.X + player.Width - _landingSupport.Right));
                risk += Math.Min(120f, outside * .5f);
            }
            // Optional edges carry a small recovery cost so a nearly tied path
            // keeps the ordinary low-gear loop. A genuinely avoided hit dwarfs
            // this bounded cost.
            if (mobilityAction == MobilityCandidateAction.EyeShieldDash) risk += 6f;
            else if (IsGravityAction(mobilityAction)) risk += 10f;
            var recovery = _scoreRecoveryRequired ?
                ActivePatternRecovery.Measure(snapshot, target, directive,
                    position, velocity, desiredHorizontal, desiredVertical,
                    _horizonTicks) :
                default(ActivePatternRecoveryMeasure);
            return new Candidate { Horizontal = horizontal, Posture = posture,
                JumpAction = jumpAction, MobilityAction = mobilityAction,
                Score = risk, Hazard = hazard,
                RecoveryValid = recovery.Valid,
                RecoveryClosed = recovery.Closed,
                RecoveryDistance = recovery.Distance,
                RecoveryProgress = _scoreRecoveryRequired &&
                    ActivePatternRecovery.MakesProjectedProgress(
                        in _scoreRecoveryCurrent, in recovery) };
        }

        // Retain the original exact one-tick helper contract used by native
        // jump/flight trace regressions. Optional mobility uses the extended
        // helper below and cannot change this baseline path.
        private static void AdvanceNativeJump(CombatSnapshot snapshot, int horizontal,
            int posture, JumpAction action, float mountedSpeed, SupportSpan originalSupport,
            ref JumpSnapshot jump, ref FlightSnapshot flight, ref Vec2 position,
            ref Vec2 velocity, ref bool grounded, ref SupportSpan support,
            ref float standingY)
        {
            var inverted = snapshot.Mobility.GravityInverted;
            var gravity = snapshot.Mobility.GravityFlip;
            var dash = snapshot.Mobility.EyeShieldDash;
            AdvanceNativeJumpWithMobility(snapshot, horizontal, posture, action,
                mountedSpeed, ref originalSupport, ref inverted,
                MobilityCandidateAction.None, 1, ref gravity, ref dash,
                ref jump, ref flight, ref position, ref velocity, ref grounded,
                ref support, ref standingY);
        }

        private static bool AdvanceNativeJumpWithMobility(CombatSnapshot snapshot, int horizontal,
            int posture, JumpAction action, float mountedSpeed, ref SupportSpan originalSupport,
            ref bool inverted, MobilityCandidateAction mobilityAction, int elapsedTick,
            ref GravityFlipState gravity, ref EyeShieldDashState dash,
            ref JumpSnapshot jump, ref FlightSnapshot flight,
            ref Vec2 position, ref Vec2 velocity, ref bool grounded,
            ref SupportSpan support, ref float standingY)
        {
            var player = snapshot.Player;
            var before = position;
            if (flight.Known) FlightMotion.RefreshBeforeMovement(ref flight, ref jump, velocity.Y);
            else JumpMotion.RefreshBeforeMovement(ref jump, velocity.Y);
            var jumpRequested = posture == JumpPosture;
            var featherFallUp = posture == FeatherFallUpPosture;
            var control = JumpMotion.ResolveControl(jumpRequested, action, in jump, grounded, snapshot.Mobility.Grappling);
            float travel;
            // Native HorizontalMovement runs before JumpMovement: the launch
            // frame still receives grounded boot acceleration. Losing support
            // is decided only after this frame's jump opportunity.
            velocity.X = HorizontalMotion.Advance(player, velocity.X, horizontal, grounded, 1, out travel, mountedSpeed);
            if (IsGravityAction(mobilityAction) && elapsedTick == 1)
            {
                if (GravityFlipMotion.TryAdvanceNativeBranch(ref gravity, true) !=
                    GravityFlipPhase.Flipped) return false;
                inverted = gravity.GravityDirection < 0f;
                jump.RemainingTicks = gravity.JumpTicks;
                grounded = false;
                support = inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
                originalSupport = support;
            }
            else if (IsGravityAction(mobilityAction) &&
                GravityFlipMotion.TryAdvanceNativeBranch(ref gravity, false) ==
                    GravityFlipPhase.Unsupported) return false;
            if (flight.Known) FlightMotion.ApplyJump(ref flight, ref jump, ref velocity.Y, control);
            else JumpMotion.ApplyJump(ref jump, ref velocity.Y, control, inverted);
            if (mobilityAction == MobilityCandidateAction.EyeShieldDash &&
                !TryAdvancePlannedDash(snapshot, horizontal, elapsedTick,
                    position, ref velocity, ref dash)) return false;
            if (velocity.Y != 0f) grounded = false;
            position.X += mobilityAction == MobilityCandidateAction.EyeShieldDash
                ? velocity.X : travel;
            if (grounded && !SupportGeometry.RetainsFooting(support, position.X, player.Width, inverted, posture < 0))
                grounded = false;
            if (flight.Known) FlightMotion.ApplyAfterJump(ref flight, in jump, ref velocity.Y,
                control, featherFallUp, posture < 0, Math.Abs(player.Gravity), player.MaxFallSpeed);
            else velocity.Y = JumpMotion.ApplyGravity(velocity.Y, Math.Abs(player.Gravity), player.MaxFallSpeed,
                inverted, jump.SlowFall, featherFallUp, posture < 0);
            position.Y += velocity.Y;
            if (grounded)
            {
                velocity.Y = 0f;
                position.Y = standingY;
                return true;
            }
            var first = originalSupport;
            var second = snapshot.Arena.RecoverySupport;
            if (second.Valid && second.Inverted == inverted &&
                (!first.Valid || (second.SurfaceY - first.SurfaceY) * (inverted ? -1f : 1f) < 0f))
            {
                first = second;
                second = originalSupport;
            }
            var landed = SupportGeometry.TryLand(first, before, ref position, ref velocity,
                player.Width, player.Height, inverted, posture < 0);
            if (landed) support = first;
            else if (SupportGeometry.TryLand(second, before, ref position, ref velocity,
                player.Width, player.Height, inverted, posture < 0))
            {
                landed = true;
                support = second;
            }
            if (landed)
            {
                // Native TileCollision returns this frame's allowed vertical
                // displacement, not zero on the first contact. The next tick
                // settles to zero. Otherwise release/rejump is predicted early.
                velocity.Y = position.Y - before.Y;
                grounded = velocity.Y == 0f;
                standingY = position.Y;
            }
            return true;
        }

        private static bool AdvanceOptionalGenericTick(CombatSnapshot snapshot,
            int horizontal, int posture, float mountedSpeed, ref SupportSpan originalSupport,
            ref bool inverted, bool jumpRequested, bool flyingMount, float flightTicks,
            bool canInitialJump, int elapsedTick, MobilityCandidateAction mobilityAction,
            ref GravityFlipState gravity, ref EyeShieldDashState dash,
            ref Vec2 position, ref Vec2 velocity,
            ref bool grounded, ref SupportSpan support, ref float standingY)
        {
            var player = snapshot.Player;
            var before = position;
            float horizontalTravel;
            velocity.X = HorizontalMotion.Advance(player, velocity.X, horizontal,
                grounded, 1, out horizontalTravel, mountedSpeed);

            if (IsGravityAction(mobilityAction) && elapsedTick == 1)
            {
                if (GravityFlipMotion.TryAdvanceNativeBranch(ref gravity, true) !=
                    GravityFlipPhase.Flipped) return false;
                inverted = gravity.GravityDirection < 0f;
                grounded = false;
                support = inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
                originalSupport = support;
            }
            else if (IsGravityAction(mobilityAction) &&
                GravityFlipMotion.TryAdvanceNativeBranch(ref gravity, false) ==
                    GravityFlipPhase.Unsupported) return false;

            if (elapsedTick == 1 && jumpRequested && canInitialJump)
            {
                if (player.OnGround)
                    velocity.Y = -(5.01f + Math.Max(0f, player.JumpSpeedBoost)) *
                        (inverted ? -1f : 1f);
                else velocity.Y -= .55f * (inverted ? -1f : 1f);
            }
            if (mobilityAction == MobilityCandidateAction.EyeShieldDash &&
                !TryAdvancePlannedDash(snapshot, horizontal, elapsedTick,
                    position, ref velocity, ref dash)) return false;

            position.X += mobilityAction == MobilityCandidateAction.EyeShieldDash
                ? velocity.X : horizontalTravel;
            if (grounded && !SupportGeometry.RetainsFooting(support, position.X,
                player.Width, inverted, posture < 0)) grounded = false;
            if (grounded) velocity.Y = 0f;
            else
            {
                var gravityAmount = Math.Max(.1f, Math.Abs(player.Gravity));
                if (jumpRequested && (flyingMount || elapsedTick <= flightTicks))
                {
                    var direction = inverted ? -1f : 1f;
                    velocity.Y += gravityAmount * direction;
                    velocity.Y -= (gravityAmount + .35f) * direction;
                }
                else velocity.Y = JumpMotion.ApplyGravity(velocity.Y, gravityAmount,
                    player.MaxFallSpeed, inverted, snapshot.Mobility.FeatherFall,
                    false, posture < 0);
            }
            position.Y += velocity.Y;
            if (grounded)
            {
                position.Y = standingY;
                return true;
            }

            var first = originalSupport;
            var second = snapshot.Arena.RecoverySupport;
            var gravityDirection = inverted ? -1f : 1f;
            if (second.Valid && second.Inverted == inverted &&
                (!first.Valid || (second.SurfaceY - first.SurfaceY) * gravityDirection < 0f))
            {
                first = second;
                second = originalSupport;
            }
            var landed = SupportGeometry.TryLand(first, before, ref position, ref velocity,
                player.Width, player.Height, inverted, posture < 0);
            if (landed) support = first;
            else if (SupportGeometry.TryLand(second, before, ref position, ref velocity,
                player.Width, player.Height, inverted, posture < 0))
            {
                landed = true;
                support = second;
            }
            if (landed)
            {
                standingY = position.Y;
                grounded = true;
            }
            return true;
        }

        private static bool TryPrepareDashCandidate(CombatSnapshot snapshot,
            int horizontal, ref EyeShieldDashState state,
            out EyeShieldDashCandidate candidate)
        {
            state.ControlLeft = horizontal < 0;
            state.ControlRight = horizontal > 0;
            state.ControlDash = true;
            var direction = horizontal == -state.FacingDirection
                ? horizontal : state.FacingDirection;
            if (direction < 0)
            {
                state.ForwardSolidProbeKnown = snapshot.Mobility.DashLeftProbeKnown;
                state.ForwardSolidProbeBlocked = snapshot.Mobility.DashLeftProbeBlocked;
            }
            else
            {
                state.ForwardSolidProbeKnown = snapshot.Mobility.DashRightProbeKnown;
                state.ForwardSolidProbeBlocked = snapshot.Mobility.DashRightProbeBlocked;
            }
            return EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate);
        }

        private static bool TryAdvancePlannedDash(CombatSnapshot snapshot,
            int horizontal, int elapsedTick, Vec2 position, ref Vec2 velocity,
            ref EyeShieldDashState state)
        {
            state.ControlLeft = horizontal < 0;
            state.ControlRight = horizontal > 0;
            state.ControlDash = elapsedTick == 1;
            state.VelocityX = velocity.X;
            state.VelocityY = velocity.Y;
            state.TimeSinceLastDashStarted = Math.Min(300,
                state.TimeSinceLastDashStarted + 1);

            if (elapsedTick == 1)
            {
                EyeShieldDashCandidate candidate;
                if (!TryPrepareDashCandidate(snapshot, horizontal, ref state,
                    out candidate)) return false;
                state = candidate.AfterDashMovement;
            }

            if (!state.HostileContactKnown || state.HostileContact ||
                HasPredictedDashContact(snapshot, position,
                    new Vec2(state.VelocityX, velocity.Y), elapsedTick)) return false;
            state.HostileContactKnown = true;
            state.HostileContact = false;

            if (elapsedTick > 1)
            {
                var phase = EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref state);
                if (phase == EyeShieldDashPhase.Unsupported) return false;
            }
            velocity.X = state.VelocityX;
            return true;
        }

        private static bool HasPredictedDashContact(CombatSnapshot snapshot,
            Vec2 position, Vec2 velocity, int elapsedTick)
        {
            var before = snapshot.Player.BoundsAt(position);
            var after = snapshot.Player.BoundsAt(position + velocity);
            var beforeTick = Math.Max(0, elapsedTick - 1);
            for (var index = 0; index < snapshot.Threats.Count; index++)
            {
                var threat = snapshot.Threats[index];
                if (threat.Kind != ThreatKind.NpcContact) continue;
                if (threat.Trajectory == ThreatTrajectory.EmpressDashContact)
                {
                    EmpressDashMotionState dash;
                    ProjectileMotionSweep sweep;
                    if (!HostileProjectileMotion.TryCreateEmpressDashState(
                            threat, out dash) ||
                        !HostileProjectileMotion.TryAdvanceEmpressDashSweep(
                            ref dash, before.Center, after.Center, 1,
                            out sweep)) return true;
                    if (sweep.Active && SweptIntersects(before, after,
                            sweep.Bounds, sweep.Bounds)) return true;
                    continue;
                }
                if (threat.Trajectory != ThreatTrajectory.Linear)
                    return true;
                if (SweptIntersects(before, after, threat.BoundsAt(beforeTick),
                    threat.BoundsAt(elapsedTick))) return true;
            }
            return false;
        }

        /// <summary>
        /// Proves that a one-frame optional movement edge has a complete route
        /// back into the ordinary low-mobility loop. Avoiding the first hit is
        /// insufficient: every braking and return tick must remain inside the
        /// observed world/arena, clear every body/beam threat, restore gravity,
        /// and (when the player began supported) reacquire that same support.
        /// </summary>
        private bool TryRolloutMobilityRecovery(CombatSnapshot snapshot,
            BossDirective directive, MobilityCandidateAction mobilityAction,
            Vec2 recoveryAnchor, bool recoveryInverted, bool recoveryWasGrounded,
            SupportSpan recoverySupport, ref GravityFlipState gravity,
            ref EyeShieldDashState dash, ref Vec2 position, ref Vec2 velocity,
            ref bool inverted, ref bool grounded, ref SupportSpan originalSupport,
            ref SupportSpan support, ref JumpSnapshot jump, ref FlightSnapshot flight,
            ref float standingY, ref float risk)
        {
            if (directive.OwnsMovementClosure || directive.OwnsHorizontalClosure)
                return false;
            if (recoveryWasGrounded && (!recoverySupport.Valid ||
                recoverySupport.Inverted != recoveryInverted)) return false;
            if (!HasRecoveryBrakingRoom(snapshot, mobilityAction, position,
                velocity, grounded, recoverySupport, recoveryWasGrounded, dash))
                return false;

            var elapsedTick = _horizonTicks;
            if (mobilityAction == MobilityCandidateAction.GravityFlip)
            {
                // Even though the avoidance horizon already contains released
                // Up frames, model one explicit release update immediately before
                // the return edge. This is the native releaseUp rearm contract.
                var horizontal = RecoveryHorizontal(snapshot.Player, recoveryAnchor.X,
                    position.X, velocity.X, grounded);
                var posture = RecoveryPosture(snapshot, recoverySupport, inverted,
                    position, velocity);
                elapsedTick++;
                if (!TryAdvanceRecoveryTick(snapshot, directive, mobilityAction,
                    horizontal, posture, elapsedTick, ref gravity, ref dash,
                    ref jump, ref flight, ref position, ref velocity, ref inverted,
                    ref grounded, ref originalSupport, ref support, ref standingY,
                    ref risk)) return false;

                gravity.PositionY = position.Y;
                gravity.VelocityY = velocity.Y;
                GravityFlipCandidate returnCandidate;
                var desiredDirection = recoveryInverted ? -1f : 1f;
                if (!gravity.ReleaseUp ||
                    !GravityFlipMotion.TryCreatePotionCandidate(gravity,
                        desiredDirection, out returnCandidate) ||
                    GravityFlipMotion.TryAdvanceNativeBranch(ref gravity, true) !=
                        GravityFlipPhase.Flipped) return false;
                inverted = gravity.GravityDirection < 0f;
                if (inverted != recoveryInverted) return false;
                jump.RemainingTicks = gravity.JumpTicks;
                grounded = false;
                originalSupport = recoverySupport.Valid ? recoverySupport :
                    (inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport);
                support = originalSupport;
            }
            else if (mobilityAction == MobilityCandidateAction.GravityRestore)
            {
                // The first scored frame is the return edge itself. The ordinary
                // horizon must therefore already end in the remembered direction.
                if (inverted != recoveryInverted) return false;
            }

            var maxRecoveryTicks = IsGravityAction(mobilityAction) ? 300 : 210;
            for (var recoveryTick = 1; recoveryTick <= maxRecoveryTicks; recoveryTick++)
            {
                if (RecoveryClosureReached(snapshot.Player, mobilityAction,
                    recoveryAnchor, recoveryInverted, recoveryWasGrounded,
                    recoverySupport, position, velocity, inverted, grounded,
                    support, dash)) return true;

                var horizontal = RecoveryHorizontal(snapshot.Player, recoveryAnchor.X,
                    position.X, velocity.X, grounded);
                var posture = RecoveryPosture(snapshot, recoverySupport, inverted,
                    position, velocity);
                elapsedTick++;
                var activeAction = mobilityAction == MobilityCandidateAction.EyeShieldDash
                    ? MobilityCandidateAction.EyeShieldDash
                    : MobilityCandidateAction.None;
                if (!TryAdvanceRecoveryTick(snapshot, directive, activeAction,
                    horizontal, posture, elapsedTick, ref gravity, ref dash,
                    ref jump, ref flight, ref position, ref velocity, ref inverted,
                    ref grounded, ref originalSupport, ref support, ref standingY,
                    ref risk)) return false;
            }
            return RecoveryClosureReached(snapshot.Player, mobilityAction,
                recoveryAnchor, recoveryInverted, recoveryWasGrounded,
                recoverySupport, position, velocity, inverted, grounded,
                support, dash);
        }

        private bool TryAdvanceRecoveryTick(CombatSnapshot snapshot,
            BossDirective directive, MobilityCandidateAction activeAction,
            int horizontal, int posture, int elapsedTick,
            ref GravityFlipState gravity, ref EyeShieldDashState dash,
            ref JumpSnapshot jump, ref FlightSnapshot flight,
            ref Vec2 position, ref Vec2 velocity, ref bool inverted,
            ref bool grounded, ref SupportSpan originalSupport,
            ref SupportSpan support, ref float standingY, ref float risk)
        {
            var before = position;
            gravity.PositionY = position.Y;
            gravity.VelocityY = velocity.Y;
            bool advanced;
            if (jump.Known)
            {
                advanced = AdvanceNativeJumpWithMobility(snapshot, horizontal,
                    posture, JumpAction.Default, 0f, ref originalSupport,
                    ref inverted, activeAction, elapsedTick, ref gravity, ref dash,
                    ref jump, ref flight, ref position, ref velocity, ref grounded,
                    ref support, ref standingY);
            }
            else
            {
                advanced = AdvanceOptionalGenericTick(snapshot, horizontal, posture,
                    0f, ref originalSupport, ref inverted, false, false, 0f,
                    false, elapsedTick, activeAction, ref gravity, ref dash,
                    ref position, ref velocity, ref grounded, ref support,
                    ref standingY);
            }
            if (!advanced) return false;
            var bounds = snapshot.Player.BoundsAt(position);
            if (OutsideWorldOrArena(snapshot, bounds)) return false;
            return RecoveryThreatStepSafe(snapshot, directive, before, position,
                elapsedTick, ref risk);
        }

        private bool RecoveryThreatStepSafe(CombatSnapshot snapshot,
            BossDirective directive, Vec2 beforePosition, Vec2 afterPosition,
            int elapsedTick, ref float risk)
        {
            var playerBefore = snapshot.Player.BoundsAt(beforePosition);
            var playerAfter = snapshot.Player.BoundsAt(afterPosition);
            var beforeTick = Math.Max(0, elapsedTick - 1);
            for (var index = 0; index < snapshot.Threats.Count; index++)
            {
                var threat = snapshot.Threats[index];
                if (threat.Geometry != ThreatGeometry.Body)
                {
                    var beam = BeamGeometry.Sweep(threat, beforeTick, elapsedTick);
                    var sweep = BeamGeometry.Union(playerBefore, playerAfter);
                    if (beam.Active && BeamGeometry.Intersects(sweep, beam,
                        BeamSafetyMargin(threat))) return false;
                    continue;
                }
                if (threat.TimeLeft > 0 && threat.TimeLeft <= beforeTick) continue;
                var aliveUntil = threat.TimeLeft > 0
                    ? Math.Min(elapsedTick, threat.TimeLeft) : elapsedTick;
                var lifeFraction = aliveUntil - beforeTick;
                var livePlayerAfter = lifeFraction >= 1f ? playerAfter :
                    snapshot.Player.BoundsAt(beforePosition +
                        (afterPosition - beforePosition) * lifeFraction);
                var margin = threat.Kind == ThreatKind.Projectile
                    ? _settings.ProjectileSafetyMargin
                    : _settings.ContactSafetyMargin + directive.ExtraContactMargin;
                if (threat.Trajectory == ThreatTrajectory.EmpressDashContact)
                {
                    EmpressDashMotionState dashState;
                    ProjectileMotionSweep dashSweep;
                    if (!HostileProjectileMotion.TryCreateEmpressDashState(
                            threat, out dashState) ||
                        !HostileProjectileMotion.TryAdvanceEmpressDashSweep(
                            ref dashState, playerBefore.Center,
                            livePlayerAfter.Center, 1, out dashSweep))
                        return false;
                    if (!dashSweep.Active) continue;
                    var dashBounds = dashSweep.Bounds.Inflated(margin);
                    if (SweptIntersects(playerBefore, livePlayerAfter,
                            dashBounds, dashBounds)) return false;
                    var dashSeparation = livePlayerAfter.SeparationSquared(
                        dashBounds);
                    if (dashSeparation < 14400f)
                    {
                        var dashTimeWeight = 1f /
                            (1f + elapsedTick * .035f);
                        risk += NearMissPenalty(threat) *
                            (1f - dashSeparation / 14400f) * dashTimeWeight;
                    }
                    continue;
                }
                if (threat.Trajectory != ThreatTrajectory.Linear)
                {
                    ProjectileMotionSweep motion =
                        default(ProjectileMotionSweep);
                    if (!HostileProjectileMotion.TrySweep(threat, beforeTick,
                            aliveUntil, out motion)) return false;
                    if (!motion.Active) continue;
                    var threatSweep = motion.Bounds.Inflated(margin);
                    if (SweptIntersects(playerBefore, livePlayerAfter,
                            threatSweep, threatSweep)) return false;
                    var curvedSeparation = livePlayerAfter.SeparationSquared(
                        threatSweep);
                    if (curvedSeparation < 14400f)
                    {
                        var curvedTimeWeight = 1f /
                            (1f + elapsedTick * .035f);
                        risk += NearMissPenalty(threat) *
                            (1f - curvedSeparation / 14400f) *
                            curvedTimeWeight;
                    }
                    continue;
                }
                var threatBefore = threat.BoundsAt(beforeTick).Inflated(margin);
                var threatAfter = threat.BoundsAt(aliveUntil).Inflated(margin);
                if (SweptIntersects(playerBefore, livePlayerAfter,
                    threatBefore, threatAfter)) return false;
                var separation = livePlayerAfter.SeparationSquared(threatAfter);
                if (separation < 14400f)
                {
                    var timeWeight = 1f / (1f + elapsedTick * .035f);
                    risk += NearMissPenalty(threat) *
                        (1f - separation / 14400f) * timeWeight;
                }
            }
            return true;
        }

        private bool HasRecoveryBrakingRoom(CombatSnapshot snapshot,
            MobilityCandidateAction action, Vec2 position, Vec2 velocity,
            bool grounded, SupportSpan recoverySupport, bool recoveryWasGrounded,
            EyeShieldDashState dash)
        {
            var direction = Math.Sign(velocity.X);
            if (direction == 0) return true;
            var distance = action == MobilityCandidateAction.EyeShieldDash
                ? EstimateDashBrakingDistance(snapshot.Player, dash, velocity.X,
                    direction, grounded)
                : EstimateOrdinaryBrakingDistance(snapshot.Player, velocity.X,
                    direction, grounded);
            if (float.IsInfinity(distance) || float.IsNaN(distance)) return false;
            var bounds = snapshot.Player.BoundsAt(position);
            var left = snapshot.Player.WorldLeft;
            var right = snapshot.Player.WorldRight;
            var arena = snapshot.Arena.LocalOpenBounds;
            if (arena.Width > 0f)
            {
                left = Math.Max(left, arena.Left);
                right = Math.Min(right, arena.Right);
            }
            if (recoveryWasGrounded && recoverySupport.Valid &&
                recoverySupport.Inverted == snapshot.Mobility.GravityInverted &&
                recoverySupport.OverlapsBody(position.X, snapshot.Player.Width))
            {
                left = Math.Max(left, recoverySupport.Left);
                right = Math.Min(right, recoverySupport.Right);
            }
            var available = direction < 0 ? bounds.Left - left : right - bounds.Right;
            return available >= distance + 8f;
        }

        private static float EstimateOrdinaryBrakingDistance(PlayerSnapshot player,
            float velocity, int direction, bool grounded)
        {
            var distance = 0f;
            for (var tick = 0; tick < 240 && velocity * direction > 0f; tick++)
            {
                float travel;
                velocity = HorizontalMotion.Advance(player, velocity, -direction,
                    grounded, 1, out travel);
                distance += Math.Max(0f, travel * direction);
            }
            return velocity * direction > 0f ? float.PositiveInfinity : distance;
        }

        private static float EstimateDashBrakingDistance(PlayerSnapshot player,
            EyeShieldDashState dash, float velocity, int direction, bool grounded)
        {
            var distance = 0f;
            for (var tick = 0; tick < 240 && velocity * direction > 0f; tick++)
            {
                float travel;
                velocity = HorizontalMotion.Advance(player, velocity, -direction,
                    grounded, 1, out travel);
                if (dash.DashDelay != 0)
                {
                    dash.ControlLeft = direction > 0;
                    dash.ControlRight = direction < 0;
                    dash.ControlDash = false;
                    dash.VelocityX = velocity;
                    dash.HostileContactKnown = true;
                    dash.HostileContact = false;
                    if (EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref dash) ==
                        EyeShieldDashPhase.Unsupported) return float.PositiveInfinity;
                    velocity = dash.VelocityX;
                    travel = velocity;
                }
                distance += Math.Max(0f, travel * direction);
            }
            return velocity * direction > 0f ? float.PositiveInfinity : distance;
        }

        private static int RecoveryHorizontal(PlayerSnapshot player, float anchorX,
            float positionX, float velocityX, bool grounded)
        {
            var delta = anchorX - positionX;
            if (Math.Abs(delta) <= Math.Max(24f, player.Width * 1.5f))
                return Math.Abs(velocityX) < .25f ? 0 : -Math.Sign(velocityX);
            var toward = Math.Sign(delta);
            if (velocityX * toward <= 0f) return toward;
            var braking = EstimateOrdinaryBrakingDistance(player, velocityX,
                toward, grounded);
            return braking + player.Width * .5f >= Math.Abs(delta)
                ? -toward : toward;
        }

        private static int RecoveryPosture(CombatSnapshot snapshot,
            SupportSpan recoverySupport, bool inverted, Vec2 position, Vec2 velocity)
        {
            if (!snapshot.Player.Jump.SlowFall && !snapshot.Mobility.FeatherFall)
                return 0;
            if (!recoverySupport.Valid || recoverySupport.Inverted != inverted)
                return -1;
            var foot = inverted ? position.Y : position.Y + snapshot.Player.Height;
            var direction = inverted ? -1f : 1f;
            var distance = (recoverySupport.SurfaceY - foot) * direction;
            // Down disables Feather Fall's 1/3 cap. Release it before a one-way
            // platform so the predicted player does not intentionally drop through.
            return distance > (recoverySupport.OneWay ? 80f : 40f) &&
                velocity.Y * direction >= 0f ? -1 : 0;
        }

        private static bool RecoveryClosureReached(PlayerSnapshot player,
            MobilityCandidateAction action, Vec2 anchor, bool recoveryInverted,
            bool recoveryWasGrounded, SupportSpan recoverySupport,
            Vec2 position, Vec2 velocity, bool inverted, bool grounded,
            SupportSpan support, EyeShieldDashState dash)
        {
            if (IsGravityAction(action) && inverted != recoveryInverted) return false;
            if (action == MobilityCandidateAction.EyeShieldDash && dash.DashDelay < 0)
                return false;
            var runSpeed = Math.Max(2f, player.MaxRunSpeed);
            if (Math.Abs(position.X - anchor.X) > Math.Max(48f, player.Width * 2f) ||
                Math.Abs(velocity.X) > runSpeed + .5f) return false;
            if (!recoveryWasGrounded)
                return !IsGravityAction(action) || Math.Abs(position.Y - anchor.Y) <= 96f;
            if (!grounded || !recoverySupport.Valid ||
                support.Inverted != recoverySupport.Inverted ||
                Math.Abs(support.SurfaceY - recoverySupport.SurfaceY) > 2f ||
                !recoverySupport.OverlapsBody(position.X, player.Width)) return false;
            var foot = inverted ? position.Y : position.Y + player.Height;
            return Math.Abs(foot - recoverySupport.SurfaceY) <= 2f;
        }

        private static bool IsGravityAction(MobilityCandidateAction action) =>
            action == MobilityCandidateAction.GravityFlip ||
            action == MobilityCandidateAction.GravityRestore;

        private void PrepareThreats(CombatSnapshot snapshot, BossDirective directive)
        {
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            var player = snapshot.Player;
            var speedX = Math.Max(Math.Abs(player.Velocity.X), Math.Max(player.MaxRunSpeed,
                snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f));
            if (snapshot.Mobility.CanDash && snapshot.Mobility.DashReady)
            {
                // Aggregate flags cannot enlarge the safety broadphase unless
                // the exact hash-locked dash state and its directional tile
                // probe can actually authorize that speed this update.
                if (EyeShieldDashMotion.IsReady(snapshot.Mobility.EyeShieldDash) &&
                    (snapshot.Mobility.DashLeftProbeKnown ||
                     snapshot.Mobility.DashRightProbeKnown))
                    speedX = Math.Max(speedX, 14.5f);
            }
            var speedY = Math.Max(Math.Abs(player.Velocity.Y), Math.Max(12f, player.MaxFallSpeed));
            var reach = new RectF(player.Position.X - speedX * _horizonTicks - 160f,
                player.Position.Y - speedY * _horizonTicks - 160f,
                player.Width + 2f * (speedX * _horizonTicks + 160f),
                player.Height + 2f * (speedY * _horizonTicks + 160f));
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Geometry != ThreatGeometry.Body)
                {
                    var beamBounds = BeamGeometry.ConservativeBounds(threat,
                        _horizonTicks);
                    if (beamBounds.Width > 0f && beamBounds.Height > 0f &&
                        reach.Intersects(beamBounds)) _relevantBeams.Add(threat);
                    continue;
                }
                if (threat.Trajectory != ThreatTrajectory.Linear)
                {
                    ProjectileMotionSweep motion =
                        default(ProjectileMotionSweep);
                    if (!HostileProjectileMotion.TrySweep(threat, 0,
                            _horizonTicks, out motion))
                    {
                        // A snapshot explicitly marked as a reviewed native
                        // trajectory must not fall back to linear motion when
                        // its required state is malformed.
                        _relevantThreats.Add(threat);
                        continue;
                    }
                    if (motion.Active && reach.Intersects(motion.Bounds))
                        _relevantThreats.Add(threat);
                    continue;
                }
                var ticks = threat.TimeLeft > 0 ? Math.Min(_horizonTicks, threat.TimeLeft) : _horizonTicks;
                var travel = threat.Velocity * ticks;
                var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                    _settings.ContactSafetyMargin + directive.ExtraContactMargin;
                // Swept broadphase includes incoming fast projectiles whose present
                // position is far away; no fixed-radius culling at the current tick.
                var swept = new RectF(threat.Position.X + Math.Min(0f, travel.X),
                    threat.Position.Y + Math.Min(0f, travel.Y), threat.Width + Math.Abs(travel.X),
                    threat.Height + Math.Abs(travel.Y)).Inflated(margin + _horizonTicks * .08f);
                if (reach.Intersects(swept)) _relevantThreats.Add(threat);
            }
            var sampleCount = _horizonTicks / _stepTicks;
            var required = sampleCount * _relevantThreats.Count;
            if (_threatSteps.Length < required)
                _threatSteps = new ThreatStep[((required + 1023) / 1024) * 1024];
            if (_targetedThreatStates.Length < _relevantThreats.Count)
                _targetedThreatStates = new TargetedProjectileMotionState[
                    ((_relevantThreats.Count + 255) / 256) * 256];
            if (_empressDashThreatStates.Length < _relevantThreats.Count)
                _empressDashThreatStates = new EmpressDashMotionState[
                    ((_relevantThreats.Count + 255) / 256) * 256];
            if (_settings.CacheThreatPrediction)
                for (var sample = 0; sample < sampleCount; sample++)
                    for (var threat = 0; threat < _relevantThreats.Count; threat++)
                        _threatSteps[sample * _relevantThreats.Count + threat] =
                            PredictThreat(_relevantThreats[threat], (sample + 1) * _stepTicks, directive);
            var beamRequired = sampleCount * _relevantBeams.Count;
            if (_beamSteps.Length < beamRequired) _beamSteps = new BeamStep[((beamRequired + 63) / 64) * 64];
            if (_settings.CacheThreatPrediction)
                for (var sample = 0; sample < sampleCount; sample++)
                    for (var beam = 0; beam < _relevantBeams.Count; beam++)
                        _beamSteps[sample * _relevantBeams.Count + beam] = PredictBeam(_relevantBeams[beam], (sample + 1) * _stepTicks);
        }

        private BeamStep PredictBeam(in ThreatSnapshot threat, int tick)
        {
            return new BeamStep
            {
                Shape = BeamGeometry.Sweep(threat, tick - _stepTicks, tick),
                DamageRisk = (_settings.DamagePenalty + Math.Max(1, threat.Damage) * 90f) * (1f / (1f + tick * .035f))
            };
        }

        private void InitializeTargetedThreatStates(CombatSnapshot snapshot)
        {
            var localPlayerIndex = snapshot.NativeContextKnown
                ? snapshot.LocalPlayerIndex : -1;
            for (var index = 0; index < _relevantThreats.Count; index++)
            {
                TargetedProjectileMotionState state;
                HostileProjectileMotion.TryCreateTargetedState(
                    _relevantThreats[index], localPlayerIndex, out state);
                _targetedThreatStates[index] = state;
            }
        }

        private void InitializeEmpressDashThreatStates()
        {
            for (var index = 0; index < _relevantThreats.Count; index++)
            {
                EmpressDashMotionState state;
                HostileProjectileMotion.TryCreateEmpressDashState(
                    _relevantThreats[index], out state);
                _empressDashThreatStates[index] = state;
            }
        }

        private ThreatStep PredictTargetedCandidateThreat(
            in ThreatSnapshot threat, int threatIndex,
            Vec2 playerCenterBefore, Vec2 playerCenterAfter, int tick,
            BossDirective directive, int cacheIndex)
        {
            ProjectileMotionSweep motion;
            if (threat.Type == 873)
            {
                // The 873 homing branch is coupled to the candidate player
                // centre. Advance it once per native tick over the coarse
                // planner step so the homing turn cannot slip between two
                // interpolated endpoints and graze the player body.
                var substepTargetBefore = playerCenterBefore;
                motion = default(ProjectileMotionSweep);
                for (var substep = 1; substep <= _stepTicks; substep++)
                {
                    var substepTargetAfter = playerCenterBefore +
                        (playerCenterAfter - playerCenterBefore) *
                        (substep / (float)_stepTicks);
                    ProjectileMotionSweep substepSweep;
                    if (!HostileProjectileMotion.TryAdvanceTargetedSweep(
                            ref _targetedThreatStates[threatIndex],
                            substepTargetBefore, substepTargetAfter, 1,
                            out substepSweep) || !substepSweep.Active)
                        break;
                    if (!motion.Active)
                    {
                        motion.Active = true;
                        motion.Bounds = substepSweep.Bounds;
                    }
                    else
                    {
                        var left = Math.Min(motion.Bounds.X,
                            substepSweep.Bounds.X);
                        var top = Math.Min(motion.Bounds.Y,
                            substepSweep.Bounds.Y);
                        var right = Math.Max(motion.Bounds.Right,
                            substepSweep.Bounds.Right);
                        var bottom = Math.Max(motion.Bounds.Bottom,
                            substepSweep.Bounds.Bottom);
                        motion.Bounds = new RectF(left, top,
                            right - left, bottom - top);
                    }
                    substepTargetBefore = substepTargetAfter;
                }
            }
            else if (!HostileProjectileMotion.TryAdvanceTargetedSweep(
                    ref _targetedThreatStates[threatIndex],
                    playerCenterBefore, playerCenterAfter, _stepTicks,
                    out motion))
            {
                // A malformed or degenerate candidate input never turns a
                // reviewed trajectory into linear motion. Discard its coupled
                // state and retain the original all-directions envelope.
                _targetedThreatStates[threatIndex] =
                    default(TargetedProjectileMotionState);
                if (!_settings.CacheThreatPrediction)
                    _threatSteps[cacheIndex] = PredictThreat(threat, tick,
                        directive);
                return _threatSteps[cacheIndex];
            }
            if (!motion.Active) return default(ThreatStep);

            var beforeTick = tick - _stepTicks;
            var aliveUntil = threat.TimeLeft > 0
                ? Math.Min(tick, threat.TimeLeft) : tick;
            var margin = ProjectileSafetyMargin(in threat);
            var bounds = motion.Bounds.Inflated(margin + tick * .08f);
            return new ThreatStep
            {
                Active = true,
                LifeFraction = (aliveUntil - beforeTick) /
                    (float)_stepTicks,
                Before = bounds,
                After = bounds,
                DamageRisk = (_settings.DamagePenalty +
                    Math.Max(1, threat.Damage) * 90f) *
                    (1f / (1f + tick * .035f))
            };
        }

        private ThreatStep PredictEmpressDashCandidateThreat(
            in ThreatSnapshot threat, int threatIndex,
            Vec2 playerCenterBefore, Vec2 playerCenterAfter, int tick,
            BossDirective directive, int cacheIndex)
        {
            ProjectileMotionSweep motion;
            if (!HostileProjectileMotion.TryAdvanceEmpressDashSweep(
                    ref _empressDashThreatStates[threatIndex],
                    playerCenterBefore, playerCenterAfter, _stepTicks,
                    out motion))
            {
                // A reviewed dash must never silently degrade to linear motion.
                // PredictThreat writes the fail-closed full-world bounds for the
                // malformed state; preserve that cache convention here.
                _empressDashThreatStates[threatIndex] =
                    default(EmpressDashMotionState);
                if (!_settings.CacheThreatPrediction)
                    _threatSteps[cacheIndex] = PredictThreat(threat, tick,
                        directive);
                return _threatSteps[cacheIndex];
            }
            if (!motion.Active) return default(ThreatStep);

            var beforeTick = tick - _stepTicks;
            var aliveUntil = threat.TimeLeft > 0
                ? Math.Min(tick, threat.TimeLeft) : tick;
            var margin = _settings.ContactSafetyMargin +
                directive.ExtraContactMargin;
            var bounds = motion.Bounds.Inflated(margin + tick * .08f);
            return new ThreatStep
            {
                Active = true,
                LifeFraction = (aliveUntil - beforeTick) /
                    (float)_stepTicks,
                Before = bounds,
                After = bounds,
                DamageRisk = (_settings.DamagePenalty +
                    Math.Max(1, threat.Damage) * 90f) *
                    (1f / (1f + tick * .035f))
            };
        }

        private ThreatStep PredictThreat(ThreatSnapshot threat, int tick, BossDirective directive)
        {
            var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                _settings.ContactSafetyMargin + directive.ExtraContactMargin;
            var beforeTick = tick - _stepTicks;
            var aliveUntil = threat.TimeLeft > 0 ? Math.Min(tick, threat.TimeLeft) : tick;
            if (threat.TimeLeft > 0 && threat.TimeLeft <= beforeTick)
                return default(ThreatStep);
            if (threat.Trajectory != ThreatTrajectory.Linear)
            {
                ProjectileMotionSweep motion;
                if (!HostileProjectileMotion.TrySweep(threat, beforeTick,
                        aliveUntil, out motion))
                {
                    var invalid = InvalidThreatBounds();
                    return new ThreatStep
                    {
                        Active = true,
                        LifeFraction = 1f,
                        Before = invalid,
                        After = invalid,
                        DamageRisk = (_settings.DamagePenalty +
                            Math.Max(1, threat.Damage) * 90f) *
                            (1f / (1f + tick * .035f))
                    };
                }
                if (!motion.Active) return default(ThreatStep);
                var bounds = motion.Bounds.Inflated(margin + tick * .08f);
                return new ThreatStep
                {
                    Active = true,
                    LifeFraction = (aliveUntil - beforeTick) /
                        (float)_stepTicks,
                    // The motion helper already encloses every native update in
                    // this scoring interval. A stationary swept box preserves
                    // that whole curved/uncertain set for relative CCD.
                    Before = bounds,
                    After = bounds,
                    DamageRisk = (_settings.DamagePenalty +
                        Math.Max(1, threat.Damage) * 90f) *
                        (1f / (1f + tick * .035f))
                };
            }
            return new ThreatStep
            {
                Active = true,
                LifeFraction = (aliveUntil - beforeTick) / (float)_stepTicks,
                Before = threat.BoundsAt(beforeTick).Inflated(margin + tick * .08f),
                After = threat.BoundsAt(aliveUntil).Inflated(margin + tick * .08f),
                DamageRisk = (_settings.DamagePenalty + Math.Max(1, threat.Damage) * 90f) * (1f / (1f + tick * .035f))
            };
        }

        private static bool SweptIntersects(in RectF playerBefore, in RectF playerAfter, in RectF threatBefore, in RectF threatAfter)
        {
            if (playerBefore.Intersects(threatBefore) || playerAfter.Intersects(threatAfter)) return true;
            var x = playerBefore.Center.X - threatBefore.Center.X;
            var y = playerBefore.Center.Y - threatBefore.Center.Y;
            var dx = playerAfter.X - playerBefore.X - (threatAfter.X - threatBefore.X);
            var dy = playerAfter.Y - playerBefore.Y - (threatAfter.Y - threatBefore.Y);
            var entry = 0f;
            var exit = 1f;
            return ClipSweep(x, dx, (playerBefore.Width + threatBefore.Width) * .5f, ref entry, ref exit) &&
                   ClipSweep(y, dy, (playerBefore.Height + threatBefore.Height) * .5f, ref entry, ref exit);
        }

        private static bool ClipSweep(float origin, float delta, float radius, ref float entry, ref float exit)
        {
            if (Math.Abs(delta) < .00001f) return Math.Abs(origin) <= radius;
            var first = (-radius - origin) / delta;
            var second = (radius - origin) / delta;
            if (first > second) { var swap = first; first = second; second = swap; }
            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return entry <= exit;
        }

        private bool OutsideWorldOrArena(CombatSnapshot snapshot, RectF bounds)
        {
            var player = snapshot.Player;
            if (bounds.Left < player.WorldLeft || bounds.Right > player.WorldRight ||
                bounds.Top < player.WorldTop || bounds.Bottom > player.WorldBottom)
                return true;
            var arena = snapshot.Arena.LocalOpenBounds;
            if (arena.Width > 0f && (bounds.Left < arena.Left || bounds.Right > arena.Right)) return true;
            // Up/down scan extents are not horizontal collision planes. Only the
            // actually verified flat solid tile rows can obstruct this footprint.
            return IntersectsSolidSupport(snapshot.Arena.FloorSupport, bounds) ||
                IntersectsSolidSupport(snapshot.Arena.CeilingSupport, bounds) ||
                IntersectsSolidSupport(snapshot.Arena.RecoverySupport, bounds);
        }

        private static bool IntersectsSolidSupport(SupportSpan support, RectF bounds)
        {
            if (!support.Valid || support.OneWay) return false;
            var row = new RectF(support.Left, support.SurfaceY - (support.Inverted ? 16f : 0f),
                support.Right - support.Left, 16f);
            return row.Intersects(bounds);
        }

        private float ImmediateRisk(CombatSnapshot snapshot, BossDirective directive)
        {
            var bounds = snapshot.Player.BoundsAt(snapshot.Player.Position);
            var ticks = Math.Max(1, Math.Min(_horizonTicks, _settings.ImmediateThreatTicks));
            var playerFuture = snapshot.Player.BoundsAt(snapshot.Player.Position + snapshot.Player.Velocity * ticks);
            var risk = 0f;
            for (var i = 0; i < _relevantThreats.Count; i++)
            {
                var threat = _relevantThreats[i];
                var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                    _settings.ContactSafetyMargin + directive.ExtraContactMargin;
                var aliveUntil = threat.TimeLeft > 0 ? Math.Min(ticks, threat.TimeLeft) : ticks;
                var livePlayerFuture = aliveUntil == ticks ? playerFuture :
                    snapshot.Player.BoundsAt(snapshot.Player.Position + snapshot.Player.Velocity * aliveUntil);
                if (threat.Trajectory != ThreatTrajectory.Linear)
                {
                    ProjectileMotionSweep motion =
                        default(ProjectileMotionSweep);
                    if (threat.Trajectory == ThreatTrajectory.EmpressDashContact)
                    {
                        EmpressDashMotionState dashState;
                        var dashPredicted =
                            HostileProjectileMotion.TryCreateEmpressDashState(
                                threat, out dashState) &&
                            HostileProjectileMotion.TryAdvanceEmpressDashSweep(
                                ref dashState, bounds.Center,
                                livePlayerFuture.Center, aliveUntil,
                                out motion);
                        if (!dashPredicted)
                        {
                            risk += _settings.DamagePenalty +
                                Math.Max(1, threat.Damage) * 90f;
                            continue;
                        }
                        if (!motion.Active) continue;
                        var dashMotionBounds = motion.Bounds.Inflated(margin);
                        if (SweptIntersects(bounds, livePlayerFuture,
                                dashMotionBounds, dashMotionBounds))
                            risk += _settings.DamagePenalty +
                                Math.Max(1, threat.Damage) * 90f;
                        continue;
                    }
                    var targeted =
                        default(TargetedProjectileMotionState);
                    var hasTargetedState = snapshot.NativeContextKnown &&
                        HostileProjectileMotion.TryCreateTargetedState(
                            threat, snapshot.LocalPlayerIndex, out targeted);
                    var predicted = hasTargetedState
                        ? HostileProjectileMotion.TryAdvanceTargetedSweep(
                            ref targeted, bounds.Center,
                            livePlayerFuture.Center, aliveUntil, out motion)
                        : HostileProjectileMotion.TrySweep(threat, 0,
                            aliveUntil, out motion);
                    if (!predicted)
                    {
                        risk += _settings.DamagePenalty +
                            Math.Max(1, threat.Damage) * 90f;
                        continue;
                    }
                    if (!motion.Active) continue;
                    var motionBounds = motion.Bounds.Inflated(margin);
                    if (SweptIntersects(bounds, livePlayerFuture,
                            motionBounds, motionBounds))
                        risk += _settings.DamagePenalty +
                            Math.Max(1, threat.Damage) * 90f;
                    continue;
                }
                var future = threat.BoundsAt(aliveUntil).Inflated(margin);
                if (SweptIntersects(bounds, livePlayerFuture, threat.BoundsAt(0).Inflated(margin), future))
                    risk += _settings.DamagePenalty + threat.Damage * 90f;
            }
            var playerSweep = BeamGeometry.Union(bounds, playerFuture);
            for (var i = 0; i < _relevantBeams.Count; i++)
            {
                var threat = _relevantBeams[i];
                var beam = BeamGeometry.Sweep(threat, 0, ticks);
                if (BeamGeometry.Intersects(playerSweep, beam,
                        BeamSafetyMargin(threat)))
                    risk += _settings.DamagePenalty + threat.Damage * 90f;
            }
            return risk;
        }

        private static RectF InvalidThreatBounds() =>
            new RectF(-1000000000f, -1000000000f,
                2000000000f, 2000000000f);

        private float BeamSafetyMargin(in ThreatSnapshot threat)
        {
            // The Empress sun-dance beam sweeps three tapered lobes through a
            // wide rotating arc; its native scale/age transition leaves less
            // room for interpolation error than a stationary line. Add a
            // conservative extra cushion so a grazing rotation cannot clip the
            // player body between two sampled steps.
            return threat.Geometry == ThreatGeometry.EmpressSunDance
                ? _settings.ProjectileSafetyMargin + 40f
                : _settings.ProjectileSafetyMargin;
        }

        private float ProjectileSafetyMargin(in ThreatSnapshot threat)
        {
            // The 873 rainbow streak homes on the candidate player, so its
            // near-miss envelope is narrower than the all-directions broadphase.
            // Keep a small extra cushion for interpolation between the coarse
            // rollout steps without inflating unrelated projectiles.
            return threat.Trajectory == ThreatTrajectory.EmpressRainbowStreak
                ? _settings.ProjectileSafetyMargin + 28f
                : _settings.ProjectileSafetyMargin;
        }

        private float NearMissPenalty(in ThreatSnapshot threat)
        {
            // The homing 873 streak tracks the candidate body, so a small
            // separation now can become a hit one native tick later. Price
            // that near miss more heavily than an ordinary projectile graze.
            return threat.Trajectory == ThreatTrajectory.EmpressRainbowStreak
                ? _settings.NearMissPenalty * 1.5f
                : _settings.NearMissPenalty;
        }

        private static bool HasRainbowStreakThreat(CombatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Threats == null)
                return false;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind == ThreatKind.Projectile &&
                    threat.Trajectory == ThreatTrajectory.EmpressRainbowStreak)
                    return true;
            }
            return false;
        }

        private void ApplyScoredMobility(CombatSnapshot snapshot, Candidate candidate,
            ref ControlPlan plan)
        {
            if (candidate.MobilityAction == MobilityCandidateAction.EyeShieldDash)
            {
                StampLateMobilityFallback(ref plan);
                plan.Dash = true;
                _dashReleasePending = true;
            }
            else if (IsGravityAction(candidate.MobilityAction))
            {
                StampLateMobilityFallback(ref plan);
                // Both directions use the same releaseUp-gated Up edge.
                plan.GravityControl = 1;
                _gravityReleasePending = true;
                _gravityCooldown = Math.Max(18, _settings.MobilityActionCooldownTicks);
                if (candidate.MobilityAction == MobilityCandidateAction.GravityFlip)
                {
                    _gravityReturnPending = true;
                    _gravityReturnInverted = snapshot.Mobility.GravityInverted;
                    _gravityReturnWasGrounded = snapshot.Player.OnGround;
                    _gravityReturnAnchor = snapshot.Player.Position;
                    _gravityReturnSupport = CurrentObservedSupport(snapshot);
                }
            }
        }

        private void CaptureLateMobilityFallback(Candidate candidate)
        {
            if (candidate.MobilityAction == MobilityCandidateAction.None &&
                candidate.Posture >= -1 && candidate.Posture <= 1 &&
                candidate.Hazard < _settings.PatternSafeRiskThreshold &&
                (!_scoreRecoveryRequired || candidate.RecoveryClosed ||
                 candidate.RecoveryProgress) &&
                !float.IsNaN(candidate.Hazard) &&
                !float.IsInfinity(candidate.Hazard))
                _lateMobilityFallback = candidate;
        }

        private void StampLateMobilityFallback(ref ControlPlan plan)
        {
            var candidate = _lateMobilityFallback;
            if (candidate.MobilityAction != MobilityCandidateAction.None ||
                candidate.Posture < -1 || candidate.Posture > 1 ||
                !(candidate.Hazard < _settings.PatternSafeRiskThreshold) ||
                float.IsNaN(candidate.Hazard) ||
                float.IsInfinity(candidate.Hazard))
                return;
            plan.LateMobilityFallback = new LateMobilityFallback
            {
                Known = true,
                Horizontal = candidate.Horizontal,
                Jump = candidate.Posture == JumpPosture,
                JumpAction = candidate.JumpAction,
                Drop = candidate.Posture < 0,
                Hazard = candidate.Hazard
            };
        }

        private static SupportSpan CurrentObservedSupport(CombatSnapshot snapshot)
        {
            var inverted = snapshot.Mobility.GravityInverted;
            var support = inverted ? snapshot.Arena.CeilingSupport :
                snapshot.Arena.FloorSupport;
            var foot = inverted ? snapshot.Player.Position.Y :
                snapshot.Player.Position.Y + snapshot.Player.Height;
            if (snapshot.Player.OnGround && (!support.OverlapsBody(
                snapshot.Player.Position.X, snapshot.Player.Width) ||
                support.Inverted != inverted ||
                Math.Abs(foot - support.SurfaceY) > 2f))
            {
                support = new SupportSpan
                {
                    Valid = true,
                    Inverted = inverted,
                    OneWay = snapshot.Player.OnOneWaySupport,
                    Left = snapshot.Player.Position.X,
                    Right = snapshot.Player.Position.X + snapshot.Player.Width,
                    SurfaceY = foot
                };
            }
            return support;
        }

        private static ControlPlan UnsupportedActiveGrapplePlan(ControlPlan plan,
            TacticalMode mode)
        {
            // Hook families differ in range, projectile count, pull velocity,
            // teleport/static behavior and detach semantics. Never press a
            // guessed jump/hook edge for an attached identity. The exact
            // item-84/projectile-13 controller is admitted separately only when
            // its complete route certificate is present.
            plan.Horizontal = 0;
            plan.Jump = plan.Drop = plan.Fire = plan.Dash = plan.Hook = false;
            plan.ToggleMount = plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.JumpAction = JumpAction.Release;
            plan.TacticalMode = mode;
            plan.StrategyId = "unsupported-active-grapple";
            plan.PhaseId = "return-control-without-guessed-detach";
            plan.RequestControlReturn = true;
            plan.ControlReturnReason = "active grapple has no exact live route contract";
            return plan;
        }

        private static ControlPlan UnsupportedActiveMountPlan(ControlPlan plan,
            TacticalMode mode)
        {
            // Catalog identity and even an exact dry motion model are not a
            // production combat controller. Until a Boss strategy explicitly
            // owns a mount-specific collision/threat/return closure, never use
            // generic MountCanFly or MountRunSpeed flags to emit guessed input.
            plan.Horizontal = 0;
            plan.Jump = plan.Drop = plan.Fire = plan.Dash = plan.Hook = false;
            plan.ToggleMount = plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.JumpAction = JumpAction.Release;
            plan.TacticalMode = mode;
            plan.StrategyId = "unsupported-active-mount";
            plan.PhaseId = "return-control-without-generic-mount-model";
            plan.RequestControlReturn = true;
            plan.ControlReturnReason =
                "active mount has no strategy-specific live route contract";
            return plan;
        }

        private static ControlPlan UnsupportedMobilityRoutePlan(ControlPlan plan,
            string reason)
        {
            // An explicit alternatives set is a list of independently closed
            // routes. If none still matches, do not assemble a synthetic route
            // from whatever capabilities remain observable this tick.
            plan.Horizontal = 0;
            plan.Jump = plan.Drop = plan.Fire = plan.Dash = plan.Hook = false;
            plan.ToggleMount = plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.JumpAction = JumpAction.Release;
            plan.TacticalMode = TacticalMode.EmergencyEvade;
            plan.StrategyId = "unsupported-mobility-route";
            plan.PhaseId = "return-control-without-capability-union";
            plan.RequestControlReturn = true;
            plan.ControlReturnReason = reason;
            return plan;
        }

        private static ControlPlan UnsupportedOutputRoutePlan(ControlPlan plan,
            string reason)
        {
            plan.Horizontal = 0;
            plan.Jump = plan.Drop = plan.Fire = plan.Dash = plan.Hook = false;
            plan.QuickMana = false;
            plan.ToggleMount = plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.JumpAction = JumpAction.Release;
            plan.TacticalMode = TacticalMode.EmergencyEvade;
            plan.StrategyId = "unsupported-output-route";
            plan.PhaseId = "return-control-without-output-route-swap";
            plan.WeaponIssue = reason;
            plan.RequestControlReturn = true;
            plan.ControlReturnReason = reason;
            return plan;
        }

        private static ControlPlan UnsupportedPatternRecoveryPlan(
            ControlPlan plan, BossDirective directive, string reason)
        {
            // Mid-fight admission is allowed only while the selected ordinary
            // trajectory continues to close on the strategy's stable loop.
            // A failed convergence proof is a normal bounded hand-back: never
            // leak the rejected candidate, an optional movement edge, or an
            // output pulse into the final native input frame.
            plan.Horizontal = 0;
            plan.Jump = plan.Drop = plan.Fire = plan.QuickHeal = false;
            plan.QuickMana = plan.Dash = plan.Hook = false;
            plan.ToggleMount = plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.JumpAction = JumpAction.Release;
            plan.LateMobilityFallback = default(LateMobilityFallback);
            plan.TacticalMode = TacticalMode.RecoverToPattern;
            plan.StrategyId = string.IsNullOrEmpty(directive.StrategyId) ?
                "active-pattern-recovery" : directive.StrategyId;
            plan.PhaseId = "return-control-after-unproved-recovery";
            plan.RequestControlReturn = true;
            plan.ControlReturnReason = reason;
            return plan;
        }

        private void ApplyConsumables(CombatSnapshot snapshot, ref ControlPlan plan)
        {
            plan.QuickHeal = snapshot.Player.MaxLife > 0 && snapshot.Player.Life <= snapshot.Player.MaxLife * _settings.HealAtLifeFraction;
            // QuickMana belongs to the selected magic route's resource FSM.
            // A health/resource convenience layer must never consume mana
            // potions while a gun, bow, whip, or legacy test weapon is active.
            if (!_hasLatchedOutputRoute ||
                _latchedOutputRoute.Resource != OutputResourceKind.Mana)
            {
                plan.QuickMana = false;
                return;
            }
            var mana = ManaOutputController.Decide(in snapshot.Weapon.Mana,
                plan.Fire);
            plan.Fire = mana.Fire;
            plan.QuickMana = mana.QuickMana;
        }

        private void RememberPlan(ControlPlan plan)
        {
            _lastHorizontal = plan.Horizontal;
        }

        private static TargetSnapshot SelectPrimaryTarget(CombatSnapshot snapshot)
        {
            var player = snapshot.Player.Center;
            var best = snapshot.Targets[0];
            var score = TargetScore(best, player);
            for (var i = 1; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                var nextScore = TargetScore(candidate, player);
                if (nextScore < score) { score = nextScore; best = candidate; }
            }
            return best;
        }

        private static bool CanFireAt(CombatSnapshot snapshot, TargetSnapshot target)
        {
            return target.Life > 0 && !target.Invulnerable && snapshot.Weapon.IsUsable && snapshot.Weapon.HasAmmo &&
                (target.LineOfSightKnown ? target.HasLineOfSight : snapshot.LineOfSightToPrimary);
        }

        private static bool FiniteNonnegative(float value) => value >= 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ApplyWeaponAim(CombatSnapshot snapshot, TargetSnapshot target, bool requested, ref ControlPlan plan)
        {
            if (snapshot.Weapon.NativeProfileRequired)
            {
                var origin = snapshot.Player.Center;
                var profile = snapshot.Weapon.Profile.Profile;
                if (profile != null && profile.Ballistics ==
                    WeaponBallisticKind.RazorbladeTyphoonHoming)
                {
                    // AI_071 chooses a nearby native NPC independent of this
                    // planner's preferred Boss. Never fire a new Typhoon when
                    // the same-frame candidate scan, shared immunity state,
                    // or exact muzzle observation cannot admit that Boss.
                    // This remains a pre-fire observation rather than an
                    // assertion about the later spawned projectile's lock.
                    if (!snapshot.Weapon.RazorbladeTyphoon.PermitsTarget(
                            target.Key))
                    {
                        plan.AimWorld = target.Center;
                        plan.Fire = false;
                        return;
                    }
                    origin = snapshot.Weapon.RazorbladeTyphoon.SpawnCenter;
                }
                var shot = WeaponAimSolver.Solve(snapshot.Weapon.Profile,
                    origin, target.Center, target.Velocity,
                    90f, target.Width, target.Height);
                plan.AimWorld = shot.AimWorld;
                plan.Fire = requested && shot.CanFire && CanFireAt(snapshot, target);
                // Report unsupported equipment, not ordinary momentary LOS/range
                // misses. Never emit a message every frame or interrupt a Boss.
                if (snapshot.Weapon.Profile.Status != WeaponProfileStatus.Supported)
                    plan.WeaponIssue = snapshot.Weapon.Profile.Reason;
            }
            else
            {
                plan.Fire = requested && CanFireAt(snapshot, target);
                plan.AimWorld = InterceptSolver.PredictAim(snapshot.Player.Center, target.Center, target.Velocity,
                    snapshot.Weapon.IsProjectile ? snapshot.Weapon.ShootSpeed : 0f);
            }
        }

        private static float TargetScore(TargetSnapshot target, Vec2 player)
        {
            var score = Vec2.DistanceSquared(target.Center, player);
            if (target.Boss) score -= 1000000f;
            if (!target.Chaseable || target.Invulnerable) score += 3000000f;
            if (target.LifeMax > 0) score += target.Life / (float)target.LifeMax * 1500f;
            return score;
        }

        private static int ArenaCenterDirection(CombatSnapshot snapshot)
        {
            var center = snapshot.Arena.SafeCenter;
            if (center.X <= 0f)
                return 0;
            var delta = center.X - snapshot.Player.Center.X;
            return Math.Abs(delta) < 48f ? 0 : Math.Sign(delta);
        }

        private static int AwayX(Vec2 player, Vec2 target) => player.X >= target.X ? 1 : -1;
        private static int ClampIntent(int value) => value < 0 ? -1 : value > 0 ? 1 : 0;

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static Candidate InvalidCandidate(int horizontal, int posture,
            MobilityCandidateAction action)
        {
            return new Candidate
            {
                Horizontal = horizontal,
                Posture = posture,
                MobilityAction = action,
                Score = float.MaxValue,
                Hazard = float.MaxValue,
                RecoveryDistance = float.MaxValue
            };
        }

        private enum MobilityCandidateAction
        {
            None = 0,
            EyeShieldDash = 1,
            GravityFlip = 2,
            GravityRestore = 3
        }

        private struct Candidate
        {
            public JumpAction JumpAction;
            public int Horizontal;
            public int Posture;
            public MobilityCandidateAction MobilityAction;
            public float Score;
            public float Hazard;
            public bool RecoveryValid;
            public bool RecoveryClosed;
            public bool RecoveryProgress;
            public float RecoveryDistance;
        }

        // Internal posture values otherwise mirror -1/0/+1 vertical intent.
        // Keep the extra feather-fall input out of BossDirective so a strategy
        // cannot request it without going through scored candidates.
        private const int JumpPosture = 1;
        private const int FeatherFallUpPosture = 2;

        private struct ThreatStep
        {
            public RectF Before;
            public RectF After;
            public float LifeFraction;
            public float DamageRisk;
            public bool Active;
        }

        private struct BeamStep
        {
            public BeamSample Shape;
            public float DamageRisk;
        }
    }
}
