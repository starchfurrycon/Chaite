using System;
using System.Collections.Generic;
using System.Linq;

namespace Chaite.Core
{
    public enum BossPattern
    {
        HorizontalKite,
        EllipseOrbit,
        CircleOrbit,
        Runway,
        StayCloseJump,
        PerpendicularDashDodge,
        ProjectileLanes,
        Composite
    }

    public enum BossLocomotionBaseline
    {
        Unspecified,
        OnFoot,
        FinitePlayerFlight,
        ActiveWitchBroom
    }

    public enum BossDashBaseline
    {
        Unspecified,
        None,
        ShieldOfCthulhu
    }

    /// <summary>
    /// The movement capability a Boss route must provide. This is deliberately
    /// independent from equipment identity: an on-foot controller, one exact
    /// mount controller, or another future controller may all qualify when
    /// they independently prove the requested kinematics and closure.
    /// </summary>
    public enum BossVerticalMobilityThreshold
    {
        Unspecified,
        GroundRoute,
        ControlledAirRoute
    }

    public enum BossBurstMobilityThreshold
    {
        Unspecified,
        None,
        CertifiedDashWithBrakedReturn
    }

    public struct BossMobilityCapabilityThreshold
    {
        public BossVerticalMobilityThreshold Vertical;
        public BossBurstMobilityThreshold Burst;
        public float MinimumHorizontalAcceleration;
        public float MinimumHorizontalBraking;
        public float MinimumControlledAscentSpeed;
        public int MinimumControlledAirTicks;
        public float MinimumBurstStartSpeed;
    }

    /// <summary>
    /// One concrete controller implementation capable of attempting a
    /// difficulty tier's mobility threshold. It is not a prescribed equipment
    /// loadout and not a bag of interchangeable capability flags: the player's
    /// live effective motion may come from any gear, while a mount or dash only
    /// qualifies through an independently modeled controller.
    /// </summary>
    public struct BossMobilityBaseline
    {
        public BossLocomotionBaseline Locomotion;
        public BossDashBaseline Dash;
    }

    /// <summary>
    /// One independently reviewed, named route for a Boss/difficulty pair.
    /// The locomotion and dash fields form one indivisible controller route:
    /// callers must never satisfy them from different entries in the
    /// alternatives. Multiple complete routes may meet the same capability
    /// threshold without prescribing one equipment set.
    /// </summary>
    public struct BossMobilityRouteProfile
    {
        public string Id;
        public BossMobilityBaseline Baseline;
    }

    public sealed class BossRequirements
    {
        public float MinimumHorizontalClearance;
        public float MinimumVerticalClearance;
        // Historical name was MinimumRunSpeed. This is the horizontal-speed
        // requirement of whichever complete implementation is selected: live
        // player movement, a certified mount, or another modeled source.
        public float MinimumEffectiveHorizontalSpeed;
        public float MinimumWeaponDps;
        public BossMobilityBaseline ClassicMobility;
        public BossMobilityBaseline ExpertMobility;
        public BossMobilityBaseline MasterMobility;
        public BossMobilityCapabilityThreshold ClassicMobilityThreshold;
        public BossMobilityCapabilityThreshold ExpertMobilityThreshold;
        public BossMobilityCapabilityThreshold MasterMobilityThreshold;
        // Optional alternatives. A non-empty set replaces the corresponding
        // legacy single profile. Selection prefers a qualifying player-owned
        // route over a mount and avoids optional burst tools; array order only
        // breaks ties and is never a capability-union priority list.
        public BossMobilityRouteProfile[] ClassicMobilityRoutes;
        public BossMobilityRouteProfile[] ExpertMobilityRoutes;
        public BossMobilityRouteProfile[] MasterMobilityRoutes;
        public bool RequiresNormalGravity;
        // Destroyer P1 is a deliberately narrow, native-state contract. The
        // exact same predicate is used before summoning and on every combat tick.
        public bool RequiresDestroyerP1Contract;
        // The first Twins closure covers one hash-reviewed classic,
        // single-player Demon-Wings/Lightning-Boots baseline. Additional
        // mobility profiles must add their own reviewed closure rather than be
        // silently treated as equivalent. Pre-spawn and live checks share this
        // baseline predicate.
        public bool RequiresTwinsClassicContract;
        // Deerclops' reviewed loop includes vanilla Slow (Buff 32). Only that
        // strategy may compare its exact 0.5-impaired live route against the
        // underlying equipment capability; actual trajectory prediction still
        // consumes the impaired per-frame motion values.
        public bool SupportsModeledSlowDebuff;
        // The Wall overwrites velocity.X from its complete native life/difficulty
        // ladder every tick.  Admission must therefore reserve the maximum
        // speed which can still occur later in this encounter, rather than the
        // generic per-difficulty offset used by ordinary strategies.  This is
        // only a necessary kinematic condition; it does not claim that matching
        // the speed alone is sufficient to win or recover every position.
        public bool RequiresWallOfFleshSpeedContract;
        // Plantera and Golem use separately reviewed ordinary-world contracts
        // which cover Classic, Expert and Master.  Their pre-spawn checks are
        // enabled only when the adapter has supplied an explicit native mode
        // (or an already observed component); live strategy evaluation always
        // invokes the contract directly.
        public bool RequiresPlanteraOrdinaryContract;
        public bool RequiresGolemOrdinaryContract;

        public bool IsMet(CombatSnapshot snapshot, out string reason)
        {
            BossMobilityRouteProfile route;
            return IsMet(snapshot, out route, out reason);
        }

        public bool IsMet(CombatSnapshot snapshot,
            out BossMobilityRouteProfile mobilityRoute, out string reason)
        {
            return IsMetCore(snapshot, false, 0f, out mobilityRoute,
                out reason);
        }

        /// <summary>
        /// Validates arena/mobility and the same Boss DPS threshold after a
        /// separate exact output controller has already been admitted.  This
        /// avoids pretending a two-slot summon/whip route is one WeaponSnapshot;
        /// callers must supply its independently proved conservative DPS.
        /// Existing IsMet overloads retain the original single-weapon checks.
        /// </summary>
        public bool IsMetWithAdmittedOutput(CombatSnapshot snapshot,
            float conservativeOutputDps,
            out BossMobilityRouteProfile mobilityRoute, out string reason)
        {
            return IsMetCore(snapshot, true, conservativeOutputDps,
                out mobilityRoute, out reason);
        }

        private bool IsMetCore(CombatSnapshot snapshot,
            bool outputAlreadyAdmitted, float conservativeOutputDps,
            out BossMobilityRouteProfile mobilityRoute, out string reason)
        {
            mobilityRoute = default(BossMobilityRouteProfile);
            if (snapshot == null || snapshot.Player == null || snapshot.Mobility == null ||
                snapshot.Arena == null || snapshot.Difficulty == null)
            {
                reason = "战斗快照、玩家、机动或场地信息不完整";
                return false;
            }
            if (!Finite(snapshot.Arena.ClearanceLeft) || !Finite(snapshot.Arena.ClearanceRight) ||
                !Finite(snapshot.Arena.ClearanceUp) || !Finite(snapshot.Arena.ClearanceDown) ||
                snapshot.Arena.ClearanceLeft < 0f || snapshot.Arena.ClearanceRight < 0f ||
                snapshot.Arena.ClearanceUp < 0f || snapshot.Arena.ClearanceDown < 0f)
            {
                reason = "场地净空或移动参数不是有效有限数值";
                return false;
            }
            if (RequiresTwinsClassicContract &&
                !TwinsClassicContract.IsSupported(snapshot, true, out reason))
                return false;
            TargetSnapshot destroyerHead;
            if (RequiresDestroyerP1Contract &&
                !DestroyerP1Contract.IsSupported(snapshot, true, out destroyerHead, out reason))
                return false;
            if (RequiresPlanteraOrdinaryContract &&
                ShouldRunOrdinarySecondaryContract(snapshot))
            {
                TargetSnapshot planteraRoot;
                bool planteraSecond;
                if (!PlanteraOrdinaryContract.IsSupported(snapshot, true,
                        out planteraRoot, out planteraSecond, out reason))
                    return false;
            }
            if (RequiresGolemOrdinaryContract &&
                ShouldRunOrdinarySecondaryContract(snapshot))
            {
                TargetSnapshot golemBody;
                bool golemDetached;
                TargetSnapshot golemFist;
                bool golemHasFist;
                if (!GolemOrdinaryContract.IsSupported(snapshot, true,
                        out golemBody, out golemDetached, out golemFist,
                        out golemHasFist, out reason))
                    return false;
            }
            var scale = snapshot.Difficulty.Zenith ? 1.20f : snapshot.Difficulty.ForTheWorthy ? 1.14f :
                snapshot.Difficulty.Master ? 1.08f : 1f;
            var horizontal = MinimumHorizontalClearance * scale;
            var vertical = MinimumVerticalClearance * scale;
            var speed = RequiredHorizontalSpeedFor(snapshot.Difficulty);
            if (snapshot.Arena.HorizontalClearance < horizontal)
            {
                reason = "横向有效场地不足 " + (int)horizontal + " 像素";
                return false;
            }
            if (snapshot.Arena.VerticalClearance < vertical)
            {
                reason = "纵向有效场地不足 " + (int)vertical + " 像素";
                return false;
            }
            if (snapshot.Mobility.Grappling)
            {
                reason = "detach the active grapple before selecting a certified mobility route";
                return false;
            }
            if (!TrySelectReadyMobilityRoute(snapshot, speed, out mobilityRoute,
                    out reason)) return false;
            if (outputAlreadyAdmitted)
            {
                if (!Finite(conservativeOutputDps) ||
                    conservativeOutputDps < MinimumWeaponDps)
                {
                    reason = "已录取输出路线的保守输出不足 " +
                        (int)MinimumWeaponDps + " DPS";
                    return false;
                }
            }
            else
            {
                if (snapshot.Weapon == null || !snapshot.Weapon.IsUsable ||
                    !snapshot.Weapon.HasAmmo ||
                    snapshot.Weapon.ApproximateDps < MinimumWeaponDps)
                {
                    reason = "快捷栏可用武器的基础输出不足 " +
                        (int)MinimumWeaponDps + " DPS";
                    return false;
                }
                if (!snapshot.Weapon.IsProjectile)
                {
                    reason = "当前风筝策略不支持纯挥砍武器，请在快捷栏准备远程武器";
                    return false;
                }
            }
            if (RequiresNormalGravity && snapshot.Mobility.GravityInverted)
            {
                reason = "当前策略只支持正常重力，不能在重力倒置时启动";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Returns the legacy single movement loadout for this exact
        /// difficulty. New strategies which have more than one independently
        /// reviewed lower-bound route should use MobilityRoutesFor and one of
        /// the TrySelect methods instead.
        /// </summary>
        public BossMobilityBaseline MobilityFor(DifficultySnapshot difficulty)
        {
            return difficulty.Master ? MasterMobility :
                difficulty.Expert ? ExpertMobility : ClassicMobility;
        }

        public BossMobilityCapabilityThreshold MobilityThresholdFor(
            DifficultySnapshot difficulty)
        {
            return difficulty.Master ? MasterMobilityThreshold :
                difficulty.Expert ? ExpertMobilityThreshold :
                ClassicMobilityThreshold;
        }

        public BossMobilityRouteProfile[] MobilityRoutesFor(
            DifficultySnapshot difficulty)
        {
            return difficulty.Master ? MasterMobilityRoutes :
                difficulty.Expert ? ExpertMobilityRoutes : ClassicMobilityRoutes;
        }

        public bool HasExplicitMobilityRoutesFor(DifficultySnapshot difficulty)
        {
            var routes = MobilityRoutesFor(difficulty);
            return routes != null && routes.Length != 0;
        }

        /// <summary>
        /// Selects the first complete reviewed route whose concrete identities
        /// are ready at admission time. A dash route therefore requires the
        /// exact dash to be rearmed, not merely equipped.
        /// </summary>
        public bool TrySelectReadyMobilityRoute(CombatSnapshot snapshot,
            out BossMobilityRouteProfile route, out string reason)
        {
            if (snapshot == null || snapshot.Difficulty == null)
            {
                route = default(BossMobilityRouteProfile);
                reason = "无法为不完整快照选择 Boss 机动路线";
                return false;
            }
            return TrySelectReadyMobilityRoute(snapshot,
                RequiredHorizontalSpeedFor(snapshot.Difficulty), out route,
                out reason);
        }

        /// <summary>
        /// Selects a complete route during live planning. Transient cooldown
        /// does not change the route identity, but the exact native equipment
        /// state must remain supported.
        /// </summary>
        public bool TrySelectLiveMobilityRoute(CombatSnapshot snapshot,
            out BossMobilityRouteProfile route, out string reason)
        {
            if (snapshot == null || snapshot.Difficulty == null)
            {
                route = default(BossMobilityRouteProfile);
                reason = "无法为不完整快照选择 Boss 机动路线";
                return false;
            }
            return TrySelectMobilityRoute(snapshot,
                RequiredHorizontalSpeedFor(snapshot.Difficulty), false, null,
                out route, out reason);
        }

        public bool TrySelectLiveMobilityRoute(CombatSnapshot snapshot,
            string requiredRouteId, out BossMobilityRouteProfile route,
            out string reason)
        {
            if (snapshot == null || snapshot.Difficulty == null)
            {
                route = default(BossMobilityRouteProfile);
                reason = "无法为不完整快照选择 Boss 机动路线";
                return false;
            }
            if (string.IsNullOrWhiteSpace(requiredRouteId))
            {
                route = default(BossMobilityRouteProfile);
                reason = "逐帧 Boss 机动路线缺少已锁定的审核 ID";
                return false;
            }
            return TrySelectMobilityRoute(snapshot,
                RequiredHorizontalSpeedFor(snapshot.Difficulty), false,
                requiredRouteId, out route, out reason);
        }

        private bool TrySelectReadyMobilityRoute(CombatSnapshot snapshot,
            float speed, out BossMobilityRouteProfile route, out string reason)
        {
            return TrySelectMobilityRoute(snapshot, speed, true, null,
                out route, out reason);
        }

        private bool TrySelectMobilityRoute(CombatSnapshot snapshot, float speed,
            bool requireDashReady, string requiredRouteId,
            out BossMobilityRouteProfile route, out string reason)
        {
            route = default(BossMobilityRouteProfile);
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Mobility == null || snapshot.Difficulty == null)
            {
                reason = "无法为不完整快照选择 Boss 机动路线";
                return false;
            }

            var routes = MobilityRoutesFor(snapshot.Difficulty);
            if (routes == null || routes.Length == 0)
            {
                route = new BossMobilityRouteProfile
                {
                    Id = snapshot.Difficulty.Master ? "legacy-master" :
                        snapshot.Difficulty.Expert ? "legacy-expert" :
                        "legacy-classic",
                    Baseline = MobilityFor(snapshot.Difficulty)
                };
                if (requiredRouteId != null && !string.Equals(route.Id,
                    requiredRouteId, StringComparison.Ordinal))
                {
                    reason = "已锁定的 Boss 机动路线不属于当前难度: " +
                        requiredRouteId;
                    return false;
                }
                var legacyThreshold = MobilityThresholdFor(snapshot.Difficulty);
                return MeetsMobilityBaseline(snapshot, in route.Baseline,
                    in legacyThreshold, speed, requireDashReady, out reason);
            }

            // Validate the complete set first. Silently skipping a malformed or
            // duplicate route would make its identity unsafe to latch later.
            for (var i = 0; i < routes.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(routes[i].Id))
                {
                    reason = "Boss 机动路线缺少稳定的审核 ID";
                    return false;
                }
                for (var j = 0; j < i; j++)
                {
                    if (!string.Equals(routes[i].Id, routes[j].Id,
                        StringComparison.Ordinal)) continue;
                    reason = "Boss 机动路线审核 ID 重复: " + routes[i].Id;
                    return false;
                }
            }

            string firstFailure = null;
            string firstFailureId = null;
            var requiredRouteFound = requiredRouteId == null;
            var selectedPreference = int.MaxValue;
            var selected = false;
            for (var i = 0; i < routes.Length; i++)
            {
                if (requiredRouteId != null && !string.Equals(routes[i].Id,
                    requiredRouteId, StringComparison.Ordinal)) continue;
                requiredRouteFound = true;
                string candidateFailure;
                var baseline = routes[i].Baseline;
                var threshold = MobilityThresholdFor(snapshot.Difficulty);
                if (MeetsMobilityBaseline(snapshot, in baseline, in threshold,
                    speed, requireDashReady, out candidateFailure))
                {
                    var preference = BossMobilityCapabilityEvaluator.Preference(
                        in baseline, in threshold);
                    if (!selected || preference < selectedPreference)
                    {
                        route = routes[i];
                        selectedPreference = preference;
                        selected = true;
                    }
                    // A locked route has exactly one admissible identity, so
                    // no preference comparison with another source is needed.
                    if (requiredRouteId != null)
                    {
                        reason = null;
                        return true;
                    }
                    continue;
                }
                if (firstFailure == null)
                {
                    firstFailureId = routes[i].Id;
                    firstFailure = candidateFailure;
                }
            }

            if (selected)
            {
                reason = null;
                return true;
            }

            if (!requiredRouteFound)
            {
                reason = "已锁定的 Boss 机动路线不属于当前难度: " +
                    requiredRouteId;
                return false;
            }
            reason = "当前状态不满足任何一条完整的已审核 Boss 机动路线" +
                (firstFailure == null ? string.Empty : "（" + firstFailureId +
                    ": " + firstFailure + "）");
            return false;
        }

        public float RequiredHorizontalSpeedFor(DifficultySnapshot difficulty)
        {
            if (RequiresWallOfFleshSpeedContract)
                return WallStrategy.MaximumNativeHorizontalSpeed(difficulty);
            return MinimumEffectiveHorizontalSpeed + (difficulty.Zenith ? .8f :
                difficulty.ForTheWorthy ? .5f : difficulty.Master ? .3f : 0f);
        }

        /// <summary>
        /// Revalidates the exact route and numeric capability contract captured
        /// at admission. It deliberately does not reselect by the current
        /// difficulty or by another now-available item.
        /// </summary>
        public bool TryValidateLiveMobilityRoute(CombatSnapshot snapshot,
            in BossMobilityRouteProfile route,
            in BossMobilityCapabilityThreshold threshold,
            float requiredHorizontalSpeed, out string reason)
        {
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Mobility == null ||
                string.IsNullOrWhiteSpace(route.Id))
            {
                reason = "无法复核不完整的已锁定 Boss 机动路线";
                return false;
            }
            return MeetsMobilityBaseline(snapshot, in route.Baseline,
                in threshold, requiredHorizontalSpeed, false, out reason);
        }

        private bool MeetsMobilityBaseline(CombatSnapshot snapshot,
            in BossMobilityBaseline baseline,
            in BossMobilityCapabilityThreshold threshold, float speed,
            bool requireDashReady, out string reason)
        {
            BossMobilityCapabilityEnvelope capability;
            if (!BossMobilityCapabilityEvaluator.TryMeasure(snapshot,
                    in baseline, requireDashReady, out capability, out reason))
                return false;
            return SupportsModeledSlowDebuff
                ? BossMobilityCapabilityEvaluator.MeetsWithModeledSlow(
                    in capability, snapshot.Player, in threshold, speed,
                    out reason)
                : BossMobilityCapabilityEvaluator.Meets(in capability,
                    in threshold, speed, out reason);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool ShouldRunOrdinarySecondaryContract(
            CombatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Difficulty == null)
                return false;
            // Legacy/synthetic adapters historically omitted the mode and
            // biome fields (including fixtures which pre-populate a dummy
            // Boss target). They retain the legacy generic admission path
            // until the adapter explicitly publishes the native game mode;
            // production TerrariaFacade always does so before this check.
            return snapshot.Difficulty.GameModeKnown;
        }
    }

    public struct BossDirective
    {
        public BossPattern Pattern;
        public string StrategyId;
        public string PhaseId;
        public float IdealDistance;
        public float VerticalOffset;
        // Optional minimum distance above a real, locally observed floor. The
        // planner's flight-recovery budget still takes precedence over ascent.
        public float FloorClearance;
        public int HorizontalIntent;
        public int VerticalIntent;
        // Zero is an intentional coast/no-jump command for source-specific
        // controllers, not an invitation to infer a generic orbit direction.
        public bool UseExplicitMovement;
        // A reviewed source-specific closure can reserve its route, posture and
        // flight budget. Generic safety may coast a nonzero horizontal command,
        // but must not reverse it, invent movement from zero, change posture, or
        // activate an unreviewed dash/mount fixture.
        public bool OwnsMovementClosure;
        // The source controller has closed the horizontal route, but leaves
        // calibrated vertical candidate scoring available. Generic arena,
        // flight-budget and candidate logic must preserve HorizontalIntent
        // exactly, including an intentional fail-closed zero.
        public bool OwnsHorizontalClosure;
        // Some source-specific pursuit/runway loops are stable at any safe
        // horizontal separation.  During active-encounter takeover, prove
        // those loops from their native minimum forward speed instead of
        // forcing the player toward an artificial target-relative radius.
        public float RecoveryMinimumHorizontalSpeed;
        public bool RecoveryIgnoreHorizontalGeometry;
        // A translating runway can have no fixed target-relative X radius,
        // but it still needs a signed safe lead over the moving source. The
        // sign is the reviewed horizontal route direction; zero keeps the
        // ordinary non-runway recovery contract unchanged.
        public float RecoveryMinimumForwardSeparation;
        // Bounded loss of a reviewed strategy contract is not a fatal plugin
        // error. It requests an orderly session cancellation before any input
        // from this otherwise-neutral directive can be applied.
        public bool RequestControlReturn;
        public string ControlReturnReason;
        // An unmodeled transient hazard is different from ending the takeover
        // contract.  This flag keeps the session alive while requiring both
        // the planner and native adapter to clear every control for this frame.
        public bool HoldNeutralControls;
        public string NeutralControlReason;
        public JumpAction JumpAction;
        public bool PreferDash;
        public bool AllowHook;
        public bool AllowGravityFlip;
        public bool ForceContinuousMovement;
        public bool Fire;
        public float ExtraContactMargin;
    }

    public sealed class BossMemory
    {
        public string StrategyId;
        public string PhaseId;
        public int PhaseTicks;
        public int OrbitDirection = 1;
        public int DashCounter;
        public bool DashActive;
        public int DirectionHoldTicks;
        public float PreviousSpeed;
        public int PreviousTargetKey = -1;
        // Queen Bee's stinger cadence is selected when AI_043 enters the
        // volley state.  The native life-band can change while that same
        // state is still running; retain the entry envelope instead of
        // rejecting a legitimate cross-threshold frame.
        public int QueenBeeStingerTargetKey = -1;
        public int QueenBeeStingerPeriod;
        public int QueenBeeStingerClockLimit;
        // Short-lived contact-escape latch for Queen Bee's non-charge
        // movement states.  Vanilla can reposition the body across the
        // player's runway between two Player.Update samples; retaining the
        // reviewed escape side for a few frames prevents the generic orbit
        // direction from steering straight back into that body.
        public int QueenBeeContactTargetKey = -1;
        public int QueenBeeContactDirection;
        public int QueenBeeContactTicks;
        public int QueenBeeProjectileDodgeTicks;
        public int QueenBeeProjectileDodgeDirection;
        // Queen Bee's jungle arena is often an open strip with no wall at
        // either end.  ReadArena can therefore report its bounded scan as
        // "open" after the player has already left the real support.  Keep a
        // conservative, read-only support span witnessed during the encounter
        // so a short projectile/contact escape cannot push the player into an
        // unrecoverable fall.
        public bool QueenBeeRunwayBoundsKnown;
        public bool QueenBeeRunwayInverted;
        public float QueenBeeRunwayLeft;
        public float QueenBeeRunwayRight;
        public float QueenBeeRunwaySurfaceY;
        public int QueenBeeRunwayBoundsAge;
        // Duke Fishron's hover and dash form one continuous orbit. Preserve
        // its rotation sense and the last hover tangent so a dash cannot flip
        // sides when the player crosses the instantaneous charge line.
        public int FishronTargetKey = -1;
        public int FishronOrbitDirection;
        public int FishronPreviousNativeState = int.MinValue;
        public int FishronPreviousNativeSequence = -1;
        public int FishronHoverHorizontal;
        public int FishronHoverVertical;
        public bool FishronDashDirectionLocked;
        public int FishronDashHorizontal;
        public int FishronDashVertical;

        public void Enter(string strategy, string phase)
        {
            if (StrategyId != strategy)
            {
                DashCounter = 0;
                DashActive = false;
                DirectionHoldTicks = 0;
                PreviousSpeed = 0f;
                PreviousTargetKey = -1;
                QueenBeeStingerTargetKey = -1;
                QueenBeeStingerPeriod = 0;
                QueenBeeStingerClockLimit = 0;
                QueenBeeContactTargetKey = -1;
                QueenBeeContactDirection = 0;
                QueenBeeContactTicks = 0;
                QueenBeeProjectileDodgeTicks = 0;
                QueenBeeProjectileDodgeDirection = 0;
                QueenBeeRunwayBoundsKnown = false;
                QueenBeeRunwayInverted = false;
                QueenBeeRunwayLeft = 0f;
                QueenBeeRunwayRight = 0f;
                QueenBeeRunwaySurfaceY = 0f;
                QueenBeeRunwayBoundsAge = 0;
                ResetFishron();
            }
            if (StrategyId != strategy || PhaseId != phase)
            {
                StrategyId = strategy;
                PhaseId = phase;
                PhaseTicks = 0;
            }
            else
            {
                PhaseTicks++;
            }
        }

        public void ResetFishron()
        {
            FishronTargetKey = -1;
            FishronOrbitDirection = 0;
            FishronPreviousNativeState = int.MinValue;
            FishronPreviousNativeSequence = -1;
            FishronHoverHorizontal = 0;
            FishronHoverVertical = 0;
            FishronDashDirectionLocked = false;
            FishronDashHorizontal = 0;
            FishronDashVertical = 0;
        }
    }

    public sealed class BossDecision
    {
        public TargetSnapshot Target;
        public TargetSnapshot PatternTarget;
        public BossDirective Directive;
        public BossRequirements Requirements;
    }

    public interface IBossStrategy
    {
        string Id { get; }
        BossRequirements Requirements { get; }
        bool Matches(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty);
        BossDecision Evaluate(CombatSnapshot snapshot, BossMemory memory);
    }

    public sealed class BossStrategyEngine
    {
        private readonly List<IBossStrategy> _strategies;
        private readonly BossMemory[] _memories;
        private readonly List<TargetSnapshot> _bosses = new List<TargetSnapshot>(200);
        private int _activeStrategyIndex = -1;

        public BossStrategyEngine()
        {
            _strategies = new List<IBossStrategy>
            {
                new MechdusaStrategy(), new MechanicalMayhemStrategy(), new TwinsStrategy(),
                new KingSlimeStrategy(), new EyeStrategy(), new EaterStrategy(), new BrainStrategy(),
                new QueenBeeStrategy(), new SkeletronStrategy(), new DeerclopsStrategy(), new WallStrategy(),
                new QueenSlimeStrategy(), new DestroyerStrategy(), new PrimeStrategy(), new PlanteraStrategy(),
                new GolemStrategy(), new FishronStrategy(), new CultistStrategy(),
                new MoonLordStrategy(), new CompositeBossStrategy()
            };
            _memories = new BossMemory[_strategies.Count];
            for (var i = 0; i < _memories.Length; i++) _memories[i] = new BossMemory();
        }

        public BossDecision Evaluate(CombatSnapshot snapshot)
        {
            var bosses = CollectBosses(snapshot);
            if (bosses.Count == 0)
            {
                _activeStrategyIndex = -1;
                return null;
            }
            var strategy = SelectStrategy(bosses, snapshot.Difficulty);
            if (strategy == null)
            {
                _activeStrategyIndex = -1;
                return null;
            }
            var strategyIndex = _strategies.IndexOf(strategy);
            var memory = _memories[strategyIndex];
            if (strategyIndex != _activeStrategyIndex)
            {
                // Strategies own compact private state. Mark a re-entry exactly
                // like a new target so it cannot resume a stale lift/turn cycle.
                memory.PreviousTargetKey = -1;
                _activeStrategyIndex = strategyIndex;
            }
            // Phase transitions must not erase the attack-cycle counter.
            if (memory.StrategyId == null) memory.StrategyId = strategy.Id;
            var decision = strategy.Evaluate(snapshot, memory);
            memory.Enter(decision.Directive.StrategyId, decision.Directive.PhaseId);
            memory.PreviousSpeed = decision.Target.Velocity.Length;
            memory.PreviousTargetKey = decision.Target.Key;
            return decision;
        }

        public BossRequirements RequirementsFor(CombatSnapshot snapshot)
        {
            if (snapshot == null)
                return null;
            var bosses = CollectBosses(snapshot);
            if (bosses.Count == 0)
                return null;
            var strategy = SelectStrategy(bosses, snapshot.Difficulty);
            return strategy?.Requirements;
        }

        public BossRequirements RequirementsForExpected(DifficultySnapshot difficulty, params int[] bossTypes)
        {
            if (bossTypes == null || bossTypes.Length == 0)
                return null;
            var bosses = bossTypes.Select((type, index) => new TargetSnapshot
            {
                Key = index,
                Type = type,
                Boss = true,
                Life = 1,
                LifeMax = 1,
                Chaseable = true
            }).ToList();
            var strategy = SelectStrategy(bosses, difficulty ?? new DifficultySnapshot());
            return strategy?.Requirements;
        }

        private IBossStrategy SelectStrategy(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty)
        {
            var family = BossFamily(bosses[0].Type);
            for (var i = 1; i < bosses.Count; i++)
                if (BossFamily(bosses[i].Type) != family)
                    return _strategies[_strategies.Count - 1];
            for (var i = 0; i < _strategies.Count - 1; i++)
                if (_strategies[i].Matches(bosses, difficulty)) return _strategies[i];
            return null;
        }

        private IList<TargetSnapshot> CollectBosses(CombatSnapshot snapshot)
        {
            _bosses.Clear();
            for (var i = 0; i < snapshot.Targets.Count; i++)
                // Eater of Worlds segments do not set NPC.boss during ordinary
                // life; vanilla only temporarily marks the final dying segment
                // for loot. Type 13 is nevertheless the authoritative live
                // head and must enter its dedicated controller.
                if ((snapshot.Targets[i].Boss || snapshot.Targets[i].Type == 13) &&
                    snapshot.Targets[i].Life > 0) _bosses.Add(snapshot.Targets[i]);
            return _bosses;
        }

        private static int BossFamily(int type)
        {
            if (MechanicalFamilies.IsTwin(type) || MechanicalFamilies.IsDestroyer(type) || MechanicalFamilies.IsPrime(type))
                return 1000;
            if (type >= 13 && type <= 15) return 13;
            if (type == 113 || type == 114) return 113;
            if (type >= 245 && type <= 249) return 245;
            // True Eye of Cthulhu (400) is a Moon Lord component even though
            // its numeric ID is not adjacent to the core/head/hands. Treating
            // a live True Eye as an unrelated boss routes the real encounter
            // into the generic composite controller precisely when an eye is
            // released.
            if (type >= 396 && type <= 398 || type == 400) return 396;
            return type;
        }

        public void Reset()
        {
            _bosses.Clear();
            _activeStrategyIndex = -1;
            for (var i = 0; i < _memories.Length; i++)
            {
                var memory = _memories[i];
                memory.StrategyId = null;
                memory.PhaseId = null;
                memory.PhaseTicks = 0;
                memory.OrbitDirection = 1;
                memory.DashCounter = 0;
                memory.DashActive = false;
                memory.DirectionHoldTicks = 0;
                memory.PreviousSpeed = 0f;
                memory.PreviousTargetKey = -1;
                memory.QueenBeeStingerTargetKey = -1;
                memory.QueenBeeStingerPeriod = 0;
                memory.QueenBeeStingerClockLimit = 0;
                memory.QueenBeeContactTargetKey = -1;
                memory.QueenBeeContactDirection = 0;
                memory.QueenBeeContactTicks = 0;
                memory.QueenBeeProjectileDodgeTicks = 0;
                memory.QueenBeeProjectileDodgeDirection = 0;
                memory.QueenBeeRunwayBoundsKnown = false;
                memory.QueenBeeRunwayInverted = false;
                memory.QueenBeeRunwayLeft = 0f;
                memory.QueenBeeRunwayRight = 0f;
                memory.QueenBeeRunwaySurfaceY = 0f;
                memory.QueenBeeRunwayBoundsAge = 0;
            }
        }
    }

    internal abstract class BossStrategyBase : IBossStrategy
    {
        private readonly Dictionary<string, string[]> _phaseNames = new Dictionary<string, string[]>(StringComparer.Ordinal);
        protected BossStrategyBase(string id, float horizontal, float vertical, float speed,
            bool finiteFlightBaseline = false)
        {
            Id = id;
            var mobility = new BossMobilityBaseline
            {
                Locomotion = finiteFlightBaseline
                    ? BossLocomotionBaseline.FinitePlayerFlight
                    : BossLocomotionBaseline.OnFoot,
                Dash = BossDashBaseline.None
            };
            var threshold = new BossMobilityCapabilityThreshold
            {
                Vertical = finiteFlightBaseline
                    ? BossVerticalMobilityThreshold.ControlledAirRoute
                    : BossVerticalMobilityThreshold.GroundRoute,
                Burst = BossBurstMobilityThreshold.None
            };
            Requirements = new BossRequirements
            {
                MinimumHorizontalClearance = horizontal,
                MinimumVerticalClearance = vertical,
                MinimumEffectiveHorizontalSpeed = speed,
                MinimumWeaponDps = MinimumDps(id),
                ClassicMobility = mobility,
                ExpertMobility = mobility,
                MasterMobility = mobility,
                ClassicMobilityThreshold = threshold,
                ExpertMobilityThreshold = threshold,
                MasterMobilityThreshold = threshold,
                ClassicMobilityRoutes = SingleMobilityRoute(id, "classic",
                    in mobility),
                ExpertMobilityRoutes = SingleMobilityRoute(id, "expert",
                    in mobility),
                MasterMobilityRoutes = SingleMobilityRoute(id, "master",
                    in mobility)
            };
        }

        protected static BossMobilityRouteProfile[] SingleMobilityRoute(
            string strategyId, string difficulty,
            in BossMobilityBaseline baseline)
        {
            return new[]
            {
                new BossMobilityRouteProfile
                {
                    Id = strategyId + "-" + difficulty + "-primary",
                    Baseline = baseline
                }
            };
        }

        public string Id { get; }
        public BossRequirements Requirements { get; }
        public abstract bool Matches(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty);
        public abstract BossDecision Evaluate(CombatSnapshot snapshot, BossMemory memory);

        protected BossDecision Decision(CombatSnapshot snapshot, TargetSnapshot target, string phase, BossPattern pattern,
            float distance, float vertical, int horizontalIntent = 0, int verticalIntent = 0, bool dash = false,
            bool hook = true, bool gravity = false, float margin = 28f, bool fire = true, TargetSnapshot? patternTarget = null)
        {
            return new BossDecision
            {
                Target = target,
                PatternTarget = patternTarget ?? target,
                Requirements = Requirements,
                Directive = new BossDirective
                {
                    StrategyId = Id,
                    PhaseId = VariantPhase(snapshot.Difficulty, phase),
                    Pattern = pattern,
                    IdealDistance = distance + VariantDistance(snapshot.Difficulty),
                    VerticalOffset = vertical,
                    HorizontalIntent = horizontalIntent,
                    VerticalIntent = verticalIntent,
                    PreferDash = dash,
                    AllowHook = hook,
                    AllowGravityFlip = gravity,
                    ForceContinuousMovement = true,
                    Fire = fire && !target.Invulnerable,
                    ExtraContactMargin = margin + VariantMargin(snapshot.Difficulty)
                }
            };
        }

        protected BossDecision NativeContractLost(CombatSnapshot snapshot,
            TargetSnapshot target, string reason,
            TargetSnapshot? patternTarget = null)
        {
            var result = Decision(snapshot, target, "native-contract-lost",
                BossPattern.HorizontalKite, 0f, 0f, 0, 0, false, false,
                false, 96f, false, patternTarget);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason = Id + ": " + reason;
            return result;
        }

        protected static TargetSnapshot Pick(CombatSnapshot snapshot, int type1, int type2 = -1, int type3 = -1, int type4 = -1)
        {
            var selected = default(TargetSnapshot);
            var bestScore = float.MaxValue;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < snapshot.Targets.Count; i++)
                {
                    var target = snapshot.Targets[i];
                    if (target.Life <= 0 || (pass == 0
                        ? target.Type != type1 && target.Type != type2 && target.Type != type3 && target.Type != type4
                        : !target.Boss)) continue;
                    var score = Vec2.DistanceSquared(target.Center, snapshot.Player.Center);
                    if (bestScore == float.MaxValue || selected.Invulnerable && !target.Invulnerable ||
                        target.Invulnerable == selected.Invulnerable && score < bestScore)
                    {
                        selected = target;
                        bestScore = score;
                    }
                }
                if (bestScore != float.MaxValue) break;
            }
            return selected;
        }

        protected static float Life(TargetSnapshot target) => target.LifeMax > 0 ? target.Life / (float)target.LifeMax : 1f;
        protected static int AwayX(PlayerSnapshot player, TargetSnapshot target) => player.Center.X >= target.Center.X ? 1 : -1;
        protected static int PerpendicularY(PlayerSnapshot player, TargetSnapshot target) => player.Center.Y >= target.Center.Y ? -1 : 1;
        protected static void ChargeEscape(CombatSnapshot snapshot,
            TargetSnapshot target, Vec2 chargeVelocity, out int horizontal,
            out int vertical)
        {
            horizontal = 0;
            vertical = 0;
            var speedSquared = chargeVelocity.X * chargeVelocity.X +
                chargeVelocity.Y * chargeVelocity.Y;
            if (float.IsNaN(speedSquared) || float.IsInfinity(speedSquared) ||
                speedSquared < .01f)
            {
                horizontal = AwayX(snapshot.Player, target);
                vertical = PerpendicularY(snapshot.Player, target);
                return;
            }

            // Keep moving away from the observed charge line rather than merely
            // opposing the Boss velocity. For a diagonal/vertical charge the
            // old X-only rule could steer along the attack and make a later hit
            // unavoidable. dot(relative, {-vy,vx}) is the signed side of line.
            var relative = snapshot.Player.Center - target.Center;
            var normalX = -chargeVelocity.Y;
            var normalY = chargeVelocity.X;
            var side = chargeVelocity.X * relative.Y -
                chargeVelocity.Y * relative.X;
            if (Math.Abs(side) <= .01f)
            {
                var positiveRoom = ProjectedArenaRoom(snapshot.Arena,
                    normalX, normalY);
                var negativeRoom = ProjectedArenaRoom(snapshot.Arena,
                    -normalX, -normalY);
                side = positiveRoom >= negativeRoom ? 1f : -1f;
            }
            else
                side = Math.Sign(side);
            normalX *= side;
            normalY *= side;

            var magnitude = (float)Math.Sqrt(speedSquared);
            var componentFloor = magnitude * .12f;
            if (Math.Abs(normalX) >= componentFloor)
                horizontal = Math.Sign(normalX);
            if (Math.Abs(normalY) >= componentFloor)
            {
                var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
                // VerticalIntent is positive Up in player-gravity coordinates,
                // while native world Y is positive Down.
                vertical = -Math.Sign(normalY) * gravitySign;
            }
        }

        private static float ProjectedArenaRoom(ArenaSnapshot arena,
            float worldX, float worldY)
        {
            var room = 0f;
            if (Math.Abs(worldX) > .01f)
                room += worldX > 0f ? arena.ClearanceRight :
                    arena.ClearanceLeft;
            if (Math.Abs(worldY) > .01f)
                room += worldY > 0f ? arena.ClearanceDown :
                    arena.ClearanceUp;
            return room;
        }
        protected static int NativeIntegerState(float value, int minimum,
            int maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return int.MinValue;
            var rounded = (int)Math.Round(value);
            return Math.Abs(value - rounded) <= .001f && rounded >= minimum &&
                rounded <= maximum ? rounded : int.MinValue;
        }

        protected static int NativeNonnegativeInteger(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f ||
                value > int.MaxValue)
                return int.MinValue;
            var rounded = (int)Math.Round(value);
            return Math.Abs(value - rounded) <= .001f ? rounded :
                int.MinValue;
        }
        protected static bool HasThreat(CombatSnapshot snapshot, int type1, int type2 = -1)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
                if (snapshot.Threats[i].Kind == ThreatKind.Projectile &&
                    (snapshot.Threats[i].Type == type1 || snapshot.Threats[i].Type == type2)) return true;
            return false;
        }

        protected static bool HasNpcThreat(CombatSnapshot snapshot, int type)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
                if (snapshot.Threats[i].Kind == ThreatKind.NpcContact && snapshot.Threats[i].Type == type) return true;
            return false;
        }

        protected static bool HasType(IList<TargetSnapshot> targets, int type, int lastType = -1)
        {
            for (var i = 0; i < targets.Count; i++)
                if (targets[i].Life > 0 && (lastType < 0 ? targets[i].Type == type :
                    targets[i].Type >= type && targets[i].Type <= lastType)) return true;
            return false;
        }

        protected static TargetSnapshot LowestLife(CombatSnapshot snapshot, int firstType, int lastType, out bool found)
        {
            var selected = default(TargetSnapshot);
            found = false;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Type < firstType || target.Type > lastType || target.Life <= 0 || target.Invulnerable) continue;
                if (!found || target.Life < selected.Life) { selected = target; found = true; }
            }
            return selected;
        }

        protected static TargetSnapshot MechanicalPriority(CombatSnapshot snapshot)
        {
            var selected = default(TargetSnapshot);
            var best = int.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Life <= 0) continue;
                var priority = target.Type == 126 ? 0 : target.Type == 127 ? 1 :
                    target.Type == 125 ? 2 : target.Type == 134 ? 3 : int.MaxValue;
                if (priority == int.MaxValue) continue;
                if (target.Invulnerable) priority += 10;
                if (priority < best) { selected = target; best = priority; }
            }
            return best == int.MaxValue ? Pick(snapshot, 125, 126, 127, 134) : selected;
        }

        protected static int CompositeEscape(CombatSnapshot snapshot, BossMemory memory)
        {
            var left = EscapeScore(snapshot, -1);
            var right = EscapeScore(snapshot, 1);
            var preferred = right >= left ? 1 : -1;
            var held = memory.OrbitDirection;
            var heldScore = held > 0 ? right : left;
            var nextScore = preferred > 0 ? right : left;
            // Keep the corridor unless a competing boss actually closes it; tiny
            // fluctuations in health/target selection must not reverse the whole train.
            if (memory.PhaseTicks == 0 || nextScore > heldScore * 1.25f + 18000f)
                memory.OrbitDirection = preferred;
            return memory.OrbitDirection;
        }

        private static float EscapeScore(CombatSnapshot snapshot, int direction)
        {
            var speed = Math.Max(snapshot.Player.MaxRunSpeed,
                snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f);
            var clearance = direction > 0 ? snapshot.Arena.ClearanceRight : snapshot.Arena.ClearanceLeft;
            var distance = Math.Min(speed * 72f, Math.Max(0f, clearance - 128f));
            var future = snapshot.Player.Center + new Vec2(direction * distance, 0);
            var worst = float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var boss = snapshot.Targets[i];
                if (!boss.Boss || boss.Life <= 0) continue;
                // Short threat extrapolation plus a longer player escape corridor.
                // Boss AI can turn: this is a risk envelope, not an exact future replay.
                var danger = boss.Center + boss.Velocity * 18f;
                worst = Math.Min(worst, Vec2.DistanceSquared(future, danger));
            }
            return Math.Min(worst, 4000000f) + Math.Min(clearance, 1400f) * 120f;
        }

        private static float MinimumDps(string id)
        {
            switch (id)
            {
                case "king-slime": return 12f;
                case "eye-of-cthulhu": return 18f;
                case "eater-of-worlds":
                case "brain-of-cthulhu": return 20f;
                case "queen-bee": return 25f;
                case "skeletron": return 30f;
                case "deerclops": return 22f;
                case "wall-of-flesh": return 55f;
                case "queen-slime": return 65f;
                case "twins":
                case "destroyer":
                case "skeletron-prime": return 90f;
                case "plantera": return 110f;
                case "golem": return 120f;
                case "duke-fishron": return 150f;
                case "lunatic-cultist": return 160f;
                case "moon-lord": return 220f;
                case "mechanical-mayhem": return 220f;
                case "mechdusa": return 260f;
                case "multi-boss-composite": return 200f;
                default: return 20f;
            }
        }

        private string VariantPhase(DifficultySnapshot difficulty, string phase)
        {
            string[] names;
            if (!_phaseNames.TryGetValue(phase, out names))
            {
                names = new[] { "classic-" + phase, "expert-" + phase, "master-" + phase,
                    "ftw-" + phase, "zenith-" + phase, "remix-" + phase };
                _phaseNames.Add(phase, names);
            }
            return names[difficulty.Zenith ? 4 : difficulty.ForTheWorthy ? 3 :
                difficulty.Master ? 2 : difficulty.Expert ? 1 : difficulty.Remix ? 5 : 0];
        }

        private static float VariantMargin(DifficultySnapshot difficulty)
        {
            if (difficulty.Zenith) return 30f;
            if (difficulty.ForTheWorthy) return 22f;
            if (difficulty.Master) return 14f;
            if (difficulty.Expert) return 8f;
            return 0f;
        }

        private static float VariantDistance(DifficultySnapshot difficulty)
        {
            if (difficulty.Zenith) return 100f;
            if (difficulty.ForTheWorthy) return 70f;
            if (difficulty.Master) return 45f;
            if (difficulty.Expert) return 25f;
            return 0f;
        }
    }

    internal sealed class KingSlimeStrategy : BossStrategyBase
    {
        private int _kingKey = -1;
        private int _runDirection;
        private int _crossDirection;
        private int _teleportDirection;
        private int _lastStage = -1;
        private bool _crossing;
        private bool _crossedThisJump;

        // Ground-runway policy, not the separate raised-platform/rope strategy.
        // These entry margins are conservative policy thresholds, not a victory proof.
        public KingSlimeStrategy() : base("king-slime", 1000, 280, 5.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 50);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var king = Pick(s, 50);
            var player = s.Player;
            if (m.PreviousTargetKey < 0 || _kingKey != king.Key)
            {
                _kingKey = king.Key;
                _runDirection = AwayX(player, king);
                _crossing = false;
                _crossedThisJump = false;
                _crossDirection = 0;
                _teleportDirection = 0;
                _lastStage = -1;
            }
            var stage = (int)king.Ai1;
            var dx = player.Center.X - king.Center.X;
            var contact = (player.Width + king.Width) * .5f + 32f;
            var gap = Math.Abs(dx);
            var ideal = 340f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(180f, s.Weapon.Profile.ConservativeRangePixels * .65f));
            var target = SelectPressureTarget(s, king);
            string phase;
            int horizontal;

            // Native ai[1], NOT stationary velocity or dontTakeDamage, identifies
            // the 60-tick shrink / 30-tick reform stages in singleplayer.
            if (stage == 5)
            {
                _crossing = false;
                if (_lastStage != 5)
                {
                    _teleportDirection = _runDirection;
                    if (king.LocalAiKnown)
                    {
                        var destinationDelta = player.Center.X - king.LocalAi1;
                        if (Math.Abs(destinationDelta) > 8f)
                            _teleportDirection = Math.Sign(destinationDelta);
                    }
                }
                phase = king.LocalAiKnown ? "teleport-leave-known-destination" : "teleport-destination-unobserved";
                horizontal = KeepInsideObservedLane(s, _teleportDirection);
                _runDirection = _teleportDirection;
            }
            else if (stage == 6)
            {
                _crossing = false;
                if (_lastStage != 6 || gap > contact)
                    _runDirection = AwayX(player, king);
                phase = "teleport-reform-regain-gap";
                horizontal = gap < ideal ? KeepInsideObservedLane(s, _runDirection) : 0;
            }
            else
            {
                var airborne = Math.Abs(king.Velocity.Y) > .1f;
                // ai[1] resets to zero at the HIGH launch, ai[0] to -200. A
                // newly spawned falling King with ai[1]==0 is not a high jump.
                var highJump = airborne && stage == 0 && king.Ai0 <= -150f;
                if (!highJump) _crossedThisJump = false;
                if (_crossing)
                {
                    if (dx * _crossDirection >= contact)
                    {
                        _runDirection = _crossDirection;
                        _crossing = false;
                    }
                    else if (Math.Abs(dx) > contact && !CanRunUnder(s, king, _crossDirection, false))
                    {
                        // Before entering the hitbox corridor, a lost opening is
                        // cancellable. Once underneath, keep the committed exit;
                        // the immediate collision layer can still override it.
                        _crossing = false;
                    }
                }
                if (!_crossing && gap > contact)
                    _runDirection = AwayX(player, king);
                var remainingLane = LaneRemaining(s, _runDirection);
                if (!_crossing && !_crossedThisJump && highJump && (remainingLane < 650f || gap < contact + 80f) &&
                    CanRunUnder(s, king, -_runDirection, true))
                {
                    _crossDirection = -_runDirection;
                    _crossing = true;
                    _crossedThisJump = true;
                }
                if (_crossing)
                {
                    phase = "high-jump-committed-run-under";
                    horizontal = _crossDirection;
                }
                else
                {
                    phase = NativeHopPhase(king, airborne, highJump);
                    var predictedGap = gap - Math.Max(0f, king.Velocity.X * _runDirection) * 18f;
                    var pressure = target.Type == 535;
                    // Coast/fire at a useful range, saving the finite runway for
                    // later cycles. Do not jump merely to match Boss center Y.
                    horizontal = predictedGap < ideal || pressure ? _runDirection : 0;
                    if (remainingLane < 48f && gap > contact + 48f) horizontal = 0;
                    horizontal = KeepInsideObservedLane(s, horizontal);
                }
            }
            _lastStage = stage;
            var visible = target.LineOfSightKnown ? target.HasLineOfSight : target.Key == king.Key && s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, player.Center) <=
                    s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            var decision = Decision(s, target, phase, BossPattern.Runway, ideal, 0f,
                horizontal, 0, false, false, false, 32f,
                visible && inRange && target.Life > 0 && target.Chaseable, king);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            // Ordinary classic/expert/master have the same native hop program.
            // Actual body dimensions and actual spawned 535s carry the differences;
            // don't inflate the useful weapon distance merely from a mode suffix.
            decision.Directive.IdealDistance = ideal;
            return decision;
        }

        private static string NativeHopPhase(TargetSnapshot king, bool airborne, bool highJump)
        {
            var stage = (int)king.Ai1;
            if (airborne)
                return highJump ? "high-jump-wait-for-clearance" : stage == 3 ? "short-hop-retreat" : "normal-hop-retreat";
            // Match native AI_015_KingSlime: conv.r4 life, conv.r4 lifeMax,
            // ldc.r4 threshold, mul, bge.un. Dividing life by lifeMax changes
            // exact boundary behavior on the supported x86/x87 runtime.
            var life = (float)king.Life;
            var maximum = (float)king.LifeMax;
            var countdown = 2f + (life < maximum * .8f ? 1f : 0f) + (life < maximum * .6f ? 1f : 0f) +
                (life < maximum * .4f ? 2f : 0f) + (life < maximum * .2f ? 3f : 0f) + (life < maximum * .1f ? 4f : 0f);
            var imminent = king.Ai0 >= -30f || -king.Ai0 <= countdown * 12f;
            if (stage == 3) return imminent ? "ground-high-windup" : "ground-high-wait";
            if (stage == 2) return imminent ? "ground-short-windup" : "ground-short-wait";
            return imminent ? "ground-normal-windup" : "ground-normal-wait";
        }

        private static TargetSnapshot SelectPressureTarget(CombatSnapshot s, TargetSnapshot king)
        {
            if (!s.Difficulty.Expert && !s.Difficulty.Master) return king;
            var result = king;
            var nearest = 240f * 240f;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var target = s.Targets[i];
                if (target.Type != 535 || target.Life <= 0 || target.Invulnerable || !target.Chaseable ||
                    !target.LineOfSightKnown || !target.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(s.Player.Center, target.Center);
                if (distance >= nearest) continue;
                nearest = distance;
                result = target;
            }
            return result;
        }

        private static float LaneRemaining(CombatSnapshot s, int direction)
        {
            var player = s.Player;
            var clearance = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            var support = s.Arena.FloorSupport;
            if (!s.Mobility.GravityInverted && support.ContainsBody(player.Position.X, player.Width))
                clearance = Math.Min(clearance, direction > 0 ? support.Right - player.Position.X - player.Width : player.Position.X - support.Left);
            return Math.Max(0f, clearance);
        }

        private static int KeepInsideObservedLane(CombatSnapshot s, int direction)
        {
            if (direction == 0) return 0;
            // A zero command brakes instead of reversing blindly through King.
            var speed = Math.Max(0f, s.Player.Velocity.X * direction);
            // Ice/slowing effects can make native slowdown far below .08. A
            // convenience floor would dangerously invent extra braking power.
            var braking = speed * speed / (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            return LaneRemaining(s, direction) < Math.Max(20f, braking + 12f) ? 0 : direction;
        }

        private static bool CanRunUnder(CombatSnapshot s, TargetSnapshot king, int direction, bool starting)
        {
            var player = s.Player;
            var floor = s.Arena.FloorSupport;
            if (!player.OnGround || s.Mobility.GravityInverted || s.Mobility.Grappling ||
                !floor.Valid || floor.Inverted || !floor.ContainsBody(player.Position.X, player.Width) ||
                Math.Abs(floor.SurfaceY - player.Position.Y - player.Height) > 6f ||
                s.Arena.HasCeiling && s.Arena.ClearanceUp < 320f ||
                s.Difficulty.ForTheWorthy || s.Difficulty.Zenith || s.Difficulty.Remix) return false;
            var bottom = king.Position.Y + king.Height;
            if (starting && (king.Velocity.Y >= -1f || bottom > player.Position.Y - 48f)) return false;
            var x = player.Position.X;
            var vx = player.Velocity.X;
            var kingX = king.Center.X;
            var vy = king.Velocity.Y;
            var halfContact = (player.Width + king.Width) * .5f + 32f;
            var mountedSpeed = s.Mobility.MountActive ? s.Mobility.MountRunSpeed : 0f;
            // Bounded scalar rollout only: no search tree, allocations, tiles or
            // RNG. Ordinary dry King gravity is <= .3, fall speed <= 10. Ceiling
            // collisions remain an explicit unverified-world limitation.
            for (var tick = 0; tick < 72; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(player, vx, direction, true, 1, out travel, mountedSpeed);
                x += travel;
                if (!floor.ContainsBody(x, player.Width)) return false;
                kingX += king.Velocity.X;
                vy = Math.Min(10f, vy + .3f);
                bottom += vy;
                var separation = x + player.Width * .5f - kingX;
                if (Math.Abs(separation) < halfContact && bottom > player.Position.Y - 32f) return false;
                if (separation * direction >= halfContact) return true;
            }
            return false;
        }
    }

    internal sealed class EyeStrategy : BossStrategyBase
    {
        private enum NativePhase
        {
            FirstTrack, FirstLaunch, FirstCharge, FirstBrake, TransformPending,
            TransformAccelerate, TransformDecelerate, SecondTrack, SecondLaunch,
            SecondCharge, SecondBrake, FastLaunch, FastCharge, FastBrake, FastReposition, Unknown
        }

        private int _eyeKey = -1;
        private int _runDirection;
        private bool _hasChargeVector;
        private bool _holdingHop;
        private Vec2 _chargeVector;
        private float _previousAi0 = -1f;
        private float _previousAi1 = -1f;
        private float _previousAi2;
        private float _previousAi3;

        // Ground runway with boots-class running. Raised platform switching,
        // slime-mount bouncing and secret-seed templates are separate policies.
        public EyeStrategy() : base("eye-of-cthulhu", 1200, 240, 5.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 4);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var eye = Pick(s, 4);
            if (_eyeKey != eye.Key || m.PreviousTargetKey < 0)
            {
                _eyeKey = eye.Key;
                _runDirection = AwayX(s.Player, eye);
                _hasChargeVector = false;
                _holdingHop = false;
                _previousAi0 = _previousAi1 = -1f;
                _previousAi2 = _previousAi3 = 0f;
            }
            var expert = s.Difficulty.Expert || s.Difficulty.Master;
            // Unlike the transform threshold, these exact native checks use r8.
            var frantic = expert && (double)eye.Life < (double)eye.LifeMax * .12;
            var desperate = expert && (double)eye.Life < (double)eye.LifeMax * .04;
            var phase = ObservePhase(eye, expert, frantic, desperate);
            var launch = phase == NativePhase.FirstLaunch || phase == NativePhase.SecondLaunch || phase == NativePhase.FastLaunch;
            var charge = phase == NativePhase.FirstCharge || phase == NativePhase.SecondCharge || phase == NativePhase.FastCharge;
            var brake = phase == NativePhase.FirstBrake || phase == NativePhase.SecondBrake || phase == NativePhase.FastBrake;
            var transformation = phase == NativePhase.TransformPending || phase == NativePhase.TransformAccelerate || phase == NativePhase.TransformDecelerate;
            var newCharge = charge && (!_hasChargeVector || eye.Ai0 != _previousAi0 || eye.Ai1 != _previousAi1 ||
                eye.Ai2 < _previousAi2 || eye.Ai3 != _previousAi3);
            if (newCharge)
            {
                // ai1==launch still contains the preceding motion. Capture only
                // the actual post-launch vector, never a guessed random dash.
                _chargeVector = eye.Velocity;
                _hasChargeVector = true;
            }
            else if (!charge) _hasChargeVector = false;

            var remainingLane = EyeLaneRemaining(s, _runDirection);
            var speed = Math.Abs(s.Player.Velocity.X);
            var brakingDistance = speed * speed / (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            var turnReserve = Math.Max(300f, brakingDistance + speed * 30f);
            var mayRecover = !launch && !charge && phase != NativePhase.Unknown;
            var turned = false;
            if (mayRecover && remainingLane < turnReserve && EyeLaneRemaining(s, -_runDirection) > remainingLane + 160f &&
                CanRecoverAcrossLane(s, eye, -_runDirection))
            {
                _runDirection = -_runDirection;
                remainingLane = EyeLaneRemaining(s, _runDirection);
                turned = true;
            }

            var ideal = 420f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(200f, s.Weapon.Profile.ConservativeRangePixels * .65f));
            var horizontal = _runDirection;
            var gap = Math.Abs(s.Player.Center.X - eye.Center.X);
            // Preserve running speed through bait and committed charges. Coast
            // only in a real recovery window when Boss is already far away, not
            // whenever generic distance tracking happens to cross one threshold.
            if (mayRecover && !turned && (gap > ideal * 1.7f || transformation && gap > ideal)) horizontal = 0;
            if (phase == NativePhase.Unknown || remainingLane < Math.Max(20f, brakingDistance + 12f)) horizontal = 0;

            var jump = JumpAction.Release;
            if (_holdingHop)
            {
                if (s.Player.Jump.Known && !s.Player.OnGround && s.Player.Jump.RemainingTicks > 0)
                    jump = JumpAction.Hold;
                else _holdingHop = false;
            }
            if (!_holdingHop && charge && _hasChargeVector && horizontal != 0 && CanStartGroundHop(s, eye, phase, expert, desperate))
            {
                _holdingHop = true;
                jump = JumpAction.Hold;
            }
            var visible = eye.LineOfSightKnown ? eye.HasLineOfSight : s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(eye.Center, s.Player.Center) <= s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            var decision = Decision(s, eye, PhaseName(phase), BossPattern.Runway, ideal, 0f,
                horizontal, jump == JumpAction.Hold ? 1 : 0, false, false, false, 30f,
                eye.Life > 0 && eye.Chaseable && visible && inRange);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.JumpAction = jump;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            decision.Directive.IdealDistance = ideal;
            _previousAi0 = eye.Ai0;
            _previousAi1 = eye.Ai1;
            _previousAi2 = eye.Ai2;
            _previousAi3 = eye.Ai3;
            return decision;
        }

        private static NativePhase ObservePhase(TargetSnapshot eye, bool expert, bool frantic, bool desperate)
        {
            if (eye.Ai0 == 1f) return NativePhase.TransformAccelerate;
            if (eye.Ai0 == 2f) return NativePhase.TransformDecelerate;
            if (eye.Ai0 == 0f)
            {
                // Native NPC.AI 11a4..11ca: r4 life < r4 lifeMax * r4 limit.
                // Do not replace this with a normalized ratio or expert .5.
                if (eye.Ai1 == 0f && (float)eye.Life < (float)eye.LifeMax * (expert ? .65f : .5f))
                    return NativePhase.TransformPending;
                if (eye.Ai1 == 0f) return NativePhase.FirstTrack;
                if (eye.Ai1 == 1f) return NativePhase.FirstLaunch;
                if (eye.Ai1 == 2f) return eye.Ai2 < 40f ? NativePhase.FirstCharge : NativePhase.FirstBrake;
                return NativePhase.Unknown;
            }
            if (eye.Ai0 != 3f) return NativePhase.Unknown;
            if (eye.Ai1 == 0f) return frantic ? NativePhase.FastReposition : NativePhase.SecondTrack;
            if (eye.Ai1 == 1f) return NativePhase.SecondLaunch;
            if (eye.Ai1 == 2f) return eye.Ai2 < (expert ? 50f : 40f) ? NativePhase.SecondCharge : NativePhase.SecondBrake;
            if (eye.Ai1 == 3f) return NativePhase.FastLaunch;
            if (eye.Ai1 == 4f)
                // Native ai2 holds at threshold-1 while within 200px; trusting
                // the observed timer automatically preserves that extra dash.
                return eye.Ai2 < (desperate ? 10f : 20f) ? NativePhase.FastCharge : NativePhase.FastBrake;
            if (eye.Ai1 == 5f) return NativePhase.FastReposition;
            return NativePhase.Unknown;
        }

        private static string PhaseName(NativePhase phase)
        {
            switch (phase)
            {
                case NativePhase.FirstTrack: return "first-hover-bait";
                case NativePhase.FirstLaunch: return "first-launch-prepare-exit";
                case NativePhase.FirstCharge: return "first-charge-committed-exit";
                case NativePhase.FirstBrake: return "first-brake-recover-runway";
                case NativePhase.TransformPending: return "transform-pending-recover-runway";
                case NativePhase.TransformAccelerate: return "transform-spin-up-recover";
                case NativePhase.TransformDecelerate: return "transform-spin-down-recover";
                case NativePhase.SecondTrack: return "second-track-bait";
                case NativePhase.SecondLaunch: return "second-launch-prepare-exit";
                case NativePhase.SecondCharge: return "second-charge-committed-exit";
                case NativePhase.SecondBrake: return "second-brake-recover-runway";
                case NativePhase.FastLaunch: return "fast-launch-observe-vector";
                case NativePhase.FastCharge: return "fast-charge-committed-exit";
                case NativePhase.FastBrake: return "fast-brake-recover-runway";
                case NativePhase.FastReposition: return "fast-reposition-bait";
                default: return "unrecognized-native-state";
            }
        }

        private static float EyeLaneRemaining(CombatSnapshot s, int direction)
        {
            var clearance = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            var support = s.Arena.FloorSupport;
            if (!s.Mobility.GravityInverted && support.ContainsBody(s.Player.Position.X, s.Player.Width))
                clearance = Math.Min(clearance, direction > 0 ? support.Right - s.Player.Position.X - s.Player.Width : s.Player.Position.X - support.Left);
            return Math.Max(0f, clearance);
        }

        private static bool CanRecoverAcrossLane(CombatSnapshot s, TargetSnapshot eye, int direction)
        {
            var p = s.Player;
            var floor = s.Arena.FloorSupport;
            if (!p.OnGround || s.Mobility.GravityInverted || s.Mobility.Grappling ||
                !floor.Valid || floor.Inverted || !floor.ContainsBody(p.Position.X, p.Width) ||
                Math.Abs(floor.SurfaceY - p.Position.Y - p.Height) > 6f) return false;
            var x = p.Position.X;
            var vx = p.Velocity.X;
            for (var tick = 1; tick <= 24; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, direction, true, 1, out travel,
                    s.Mobility.MountActive ? s.Mobility.MountRunSpeed : 0f);
                x += travel;
                if (!floor.ContainsBody(x, p.Width)) return false;
                var boss = eye.Position + eye.Velocity * tick;
                if (x + p.Width + 40f > boss.X && x - 40f < boss.X + eye.Width &&
                    p.Position.Y + p.Height + 40f > boss.Y && p.Position.Y - 40f < boss.Y + eye.Height) return false;
            }
            return true;
        }

        private bool CanStartGroundHop(CombatSnapshot s, TargetSnapshot eye, NativePhase phase, bool expert, bool desperate)
        {
            var p = s.Player;
            var jump = p.Jump;
            if (!jump.Known || !jump.ReleaseReady || jump.Speed <= 0f || jump.Height <= 0 || !p.OnGround ||
                s.Mobility.GravityInverted || s.Mobility.Grappling || s.Mobility.MountActive ||
                s.Difficulty.ForTheWorthy || s.Difficulty.Zenith || s.Difficulty.Remix ||
                Math.Abs(_chargeVector.Y) > Math.Abs(_chargeVector.X) * .65f ||
                eye.Center.Y < p.Center.Y - 48f) return false;
            var floor = s.Arena.FloorSupport;
            if (!floor.ContainsBody(p.Position.X, p.Width) || floor.Inverted ||
                Math.Abs(floor.SurfaceY - p.Position.Y - p.Height) > 6f) return false;
            var straightUntil = phase == NativePhase.FirstCharge ? 40f : phase == NativePhase.SecondCharge ?
                (expert ? 50f : 40f) : (desperate ? 10f : 20f);
            // Only rely on the still-observed straight segment. The native
            // near-player fast-dash timer extension is not assumed in advance.
            var horizon = GuaranteedStraightTicks(eye.Ai2, straightUntil);
            var x = p.Position.X;
            var vx = p.Velocity.X;
            var hopX = x;
            var hopY = p.Position.Y;
            var hopVx = vx;
            var hopVy = p.Velocity.Y;
            var hopState = jump;
            var groundThreat = false;
            for (var tick = 1; tick <= horizon; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, _runDirection, true, 1, out travel);
                x += travel;
                // Native horizontal movement precedes JumpMovement, so the
                // launch tick still earns grounded sprint acceleration.
                hopVx = HorizontalMotion.Advance(p, hopVx, _runDirection, tick == 1, 1, out travel);
                hopX += travel;
                var hold = JumpMotion.ResolveControl(true, JumpAction.Hold, in hopState, tick == 1, false);
                JumpMotion.ApplyJump(ref hopState, ref hopVy, hold, false);
                hopVy = JumpMotion.ApplyGravity(hopVy, Math.Abs(p.Gravity), p.MaxFallSpeed, false);
                hopY += hopVy;
                if (!floor.ContainsBody(x, p.Width) || !floor.ContainsBody(hopX, p.Width) ||
                    p.Position.Y - hopY + 48f > s.Arena.ClearanceUp) return false;
                var boss = eye.Position + _chargeVector * tick;
                if (hopX + p.Width + 24f > boss.X && hopX - 24f < boss.X + eye.Width &&
                    hopY + p.Height + 24f > boss.Y && hopY - 24f < boss.Y + eye.Height) return false;
                if (x + p.Width + 24f > boss.X && x - 24f < boss.X + eye.Width &&
                    p.Position.Y + p.Height + 24f > boss.Y && p.Position.Y - 24f < boss.Y + eye.Height)
                {
                    if (tick < 4) return false;
                    groundThreat = true;
                }
            }
            return groundThreat;
        }

        private static int GuaranteedStraightTicks(float observedTimer, float threshold)
        {
            // Snapshot precedes NPC.Update. Native increments BEFORE comparing
            // against the damping threshold, so future tick k must satisfy
            // observedTimer + k < threshold. Do not pre-spend a possible fast
            // dash extension: its exact next==T / target-position<200 condition
            // is evaluated only after the player has moved.
            if (observedTimer < 0f || float.IsNaN(observedTimer) || float.IsInfinity(observedTimer)) return 0;
            return (int)Math.Max(0d, Math.Min(24d, Math.Ceiling((double)threshold - observedTimer) - 1d));
        }
    }

    internal sealed class EaterStrategy : BossStrategyBase
    {
        public EaterStrategy() : base("eater-of-worlds", 720, 320, 3.8f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 13, 15);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var fallback = Pick(s, 13, 14, 15);
            string reason;
            TargetSnapshot head;
            if (!TryValidateNativeEncounter(s, out head, out reason))
                return NativeContractLost(s, fallback, reason, fallback);

            var target = SelectDamageSegment(s, m, head);
            var exposed = head.LocalAi0 == 0f;
            int horizontal;
            int vertical;
            if (exposed)
            {
                ChargeEscape(s, head, head.Velocity, out horizontal,
                    out vertical);
                if (horizontal == 0)
                    horizontal = ClassicSecondaryBossContract.StableHorizontal(
                        s, head, m, 220f);
            }
            else
            {
                ClassicSecondaryBossContract.OrbitIntent(s, head, m, 250f,
                    470f, out horizontal, out vertical);
                // A buried head's exact future turn depends on collision tiles.
                // Preserve a lateral corridor rather than chasing its current Y.
                vertical = 0;
                if (horizontal == 0)
                    horizontal = ClassicSecondaryBossContract.StableHorizontal(
                        s, head, m, 220f);
            }
            var phase = exposed ? "exposed-head-cross-line" :
                "buried-head-keep-crossing-corridor";
            var fire = target.Life > 0 && target.Chaseable &&
                !target.Invulnerable &&
                ClassicSecondaryBossContract.IsVisibleForNativeFinalRay(target) &&
                ClassicSecondaryBossContract.IsWithinReviewedWeaponRange(s,
                    target);
            var decision = Decision(s, target, phase,
                BossPattern.ProjectileLanes, exposed ? 340f : 290f, -80f,
                horizontal, vertical, false, false, false,
                exposed ? 74f : 54f, fire, head);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0 ||
                vertical != 0;
            return decision;
        }

        private static bool TryValidateNativeEncounter(CombatSnapshot s,
            out TargetSnapshot dangerousHead, out string reason)
        {
            dangerousHead = default(TargetSnapshot);
            if (!ClassicSecondaryBossContract.TryValidateFixture(s,
                    out reason)) return false;
            if (!s.Player.ZoneCorruptKnown ||
                !s.Player.ZoneCrimsonKnown ||
                !s.Player.ZoneCorrupt && !s.Player.ZoneCrimson)
            {
                reason = "Eater head is outside the observed corruption/crimson native pursuit predicate";
                return false;
            }

            var segments = new List<TargetSnapshot>(66);
            var heads = new List<TargetSnapshot>(8);
            var keys = new HashSet<int>();
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var segment = s.Targets[i];
                if (segment.Life <= 0 || segment.Type < 13 ||
                    segment.Type > 15) continue;
                if (segment.Key < 0 || !keys.Add(segment.Key) ||
                    !ClassicSecondaryBossContract.HasAllAi(segment) ||
                    !ClassicSecondaryBossContract.HasAllLocalAi(segment) ||
                    !ClassicSecondaryBossContract.IsExactInt(segment.Ai0) ||
                    !ClassicSecondaryBossContract.IsExactInt(segment.Ai1) ||
                    !ClassicSecondaryBossContract.IsExactInt(segment.Ai2, 0,
                        65) || segment.Ai3 != 0f)
                {
                    reason = "Eater segment has malformed native links or counters";
                    return false;
                }
                if (segment.Type == 13)
                {
                    if (!ClassicSecondaryBossContract.TargetsLocalPlayer(s,
                            segment) ||
                        segment.LocalAi0 != 0f && segment.LocalAi0 != 1f)
                    {
                        reason = "Eater head target or collision branch is unobservable";
                        return false;
                    }
                    heads.Add(segment);
                }
                else if (segment.LocalAi0 != 0f)
                {
                    reason = "Eater body/tail local state is not a Classic worm state";
                    return false;
                }
                segments.Add(segment);
            }
            if (heads.Count == 0 || segments.Count < 2 ||
                segments.Count > 66)
            {
                reason = "Eater stable chain is incomplete";
                return false;
            }

            var visited = new HashSet<int>();
            for (var i = 0; i < heads.Count; i++)
            {
                var current = heads[i];
                if (!visited.Add(current.Key))
                {
                    reason = "Eater chain reuses a head";
                    return false;
                }
                for (var depth = 0; depth < segments.Count; depth++)
                {
                    var nextKey = (int)current.Ai0;
                    if (nextKey <= 0)
                    {
                        reason = "Eater head/body has no observed successor";
                        return false;
                    }
                    TargetSnapshot next;
                    if (!ClassicSecondaryBossContract.TryFindByKey(s, nextKey,
                            out next) || next.Type < 14 || next.Type > 15 ||
                        next.Ai1 != current.Key || !visited.Add(next.Key))
                    {
                        reason = "Eater predecessor/successor links are not reciprocal";
                        return false;
                    }
                    if (next.Type == 15)
                    {
                        if (next.Ai0 != 0f)
                        {
                            reason = "Eater tail has an impossible successor";
                            return false;
                        }
                        break;
                    }
                    current = next;
                    if (depth == segments.Count - 1)
                    {
                        reason = "Eater chain does not terminate in a tail";
                        return false;
                    }
                }
            }
            if (visited.Count != segments.Count)
            {
                reason = "Eater snapshot contains an orphan or partially spawned segment";
                return false;
            }

            var best = float.MaxValue;
            for (var i = 0; i < heads.Count; i++)
            {
                var predicted = heads[i].Center + heads[i].Velocity * 14f;
                var score = Vec2.DistanceSquared(predicted, s.Player.Center);
                if (score >= best) continue;
                best = score;
                dangerousHead = heads[i];
            }
            reason = null;
            return true;
        }

        private static TargetSnapshot SelectDamageSegment(CombatSnapshot s,
            BossMemory memory, TargetSnapshot head)
        {
            var selected = head;
            var found = false;
            var best = float.MaxValue;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Type < 13 || candidate.Type > 15 ||
                    candidate.Life <= 0 || candidate.Invulnerable ||
                    !candidate.Chaseable || candidate.LineOfSightKnown &&
                    !candidate.HasLineOfSight) continue;
                var score = Vec2.DistanceSquared(candidate.Center,
                    s.Player.Center);
                if (candidate.Key == memory.PreviousTargetKey)
                    score -= 120f * 120f;
                if (found && score >= best) continue;
                found = true;
                best = score;
                selected = candidate;
            }
            return selected;
        }
    }

    internal sealed class BrainStrategy : BossStrategyBase
    {
        public BrainStrategy() : base("brain-of-cthulhu", 620, 320, 4f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 266);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var brain = Pick(s, 266);
            string reason;
            int state;
            if (!TryValidateNativeEncounter(s, brain, out state, out reason))
                return NativeContractLost(s, brain, reason, brain);

            var phaseOne = state >= 0;
            var target = phaseOne ? SelectCreeper(s, m, brain) : brain;
            var anchor = brain;
            if (state == 1 || state == 2 || state == -2 || state == -3)
            {
                anchor.Position = new Vec2(brain.Ai1 * 16f -
                    brain.Width * .5f, brain.Ai2 * 16f -
                    brain.Height * .5f);
            }
            int horizontal;
            int vertical;
            ClassicSecondaryBossContract.OrbitIntent(s, anchor, m,
                phaseOne ? 210f : 270f, phaseOne ? 390f : 470f,
                out horizontal, out vertical);
            if (horizontal == 0)
                horizontal = ClassicSecondaryBossContract.StableHorizontal(s,
                    anchor, m, 180f);
            string phase;
            switch (state)
            {
                case 0: phase = "phase-1-creeper-orbit"; break;
                case 1: phase = "phase-1-fade-from-known-teleport"; break;
                case 2: phase = "phase-1-emerge-at-known-teleport"; break;
                case -1: phase = "phase-2-chase-between-teleports"; break;
                case -2: phase = "phase-2-fade-from-known-teleport"; break;
                default: phase = "phase-2-emerge-at-known-teleport"; break;
            }
            var fire = target.Life > 0 && !target.Invulnerable &&
                target.Chaseable &&
                ClassicSecondaryBossContract.IsVisibleForNativeFinalRay(target) &&
                ClassicSecondaryBossContract.IsWithinReviewedWeaponRange(s,
                    target);
            var close = Vec2.DistanceSquared(brain.Center, s.Player.Center) <
                (phaseOne ? 150f * 150f : 210f * 210f);
            var decision = Decision(s, target, phase,
                phaseOne ? BossPattern.CircleOrbit :
                BossPattern.HorizontalKite, phaseOne ? 300f : 380f,
                phaseOne ? -30f : -60f, horizontal, vertical, close, false,
                false, phaseOne ? 48f : 68f, fire, brain);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0 ||
                vertical != 0;
            return decision;
        }

        private static bool TryValidateNativeEncounter(CombatSnapshot s,
            TargetSnapshot brain, out int state, out string reason)
        {
            state = int.MinValue;
            if (!ClassicSecondaryBossContract.TryValidateFixture(s,
                    out reason)) return false;
            if (!s.Player.ZoneCrimsonKnown || !s.Player.ZoneCrimson)
            {
                reason = "Brain target is outside the observed Crimson pursuit predicate";
                return false;
            }
            if (brain.Type != 266 || brain.Key < 0 ||
                !ClassicSecondaryBossContract.TargetsLocalPlayer(s, brain) ||
                !ClassicSecondaryBossContract.HasAllAi(brain) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(brain) ||
                brain.LocalAi0 != 1f || brain.LocalAi3 != 0f)
            {
                reason = "Brain native target or local state is incomplete";
                return false;
            }
            state = NativeIntegerState(brain.Ai0, -3, 2);
            if (state == int.MinValue ||
                !ClassicSecondaryBossContract.IsExactInt(brain.Ai1) ||
                !ClassicSecondaryBossContract.IsExactInt(brain.Ai2) ||
                !ClassicSecondaryBossContract.IsExactInt(brain.Ai3))
            {
                reason = "Brain state or teleport tuple is malformed";
                return false;
            }
            var phaseOne = state >= 0;
            var creepers = 0;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var creeper = s.Targets[i];
                if (creeper.Life <= 0 || creeper.Type != 267) continue;
                creepers++;
                if (creeper.Key < 0 ||
                    !ClassicSecondaryBossContract.HasAllAi(creeper) ||
                    !ClassicSecondaryBossContract.HasAllLocalAi(creeper) ||
                    !ClassicSecondaryBossContract.IsExactInt(creeper.Ai0, 0,
                        1) ||
                    !ClassicSecondaryBossContract.IsExactInt(creeper.Ai1, 0,
                        6) || creeper.Ai2 != 0f || creeper.Ai3 != 0f)
                {
                    reason = "Brain Creeper has a malformed orbit/charge state";
                    return false;
                }
            }
            if (phaseOne && (creepers == 0 || !brain.Invulnerable) ||
                !phaseOne && (creepers != 0 || brain.Invulnerable))
            {
                reason = "Brain phase does not agree with Creeper and damage state";
                return false;
            }
            if (state == 0 || state == 1 || state == 2)
            {
                if (brain.Ai3 != 0f ||
                    (state == 1 || state == 2) &&
                    !ValidTeleportTile(s, brain.Ai1, brain.Ai2))
                {
                    reason = "Brain phase-one teleport tuple is impossible";
                    return false;
                }
            }
            else if (state == -1)
            {
                if (brain.Ai3 != 0f)
                {
                    reason = "Brain pursuit state retains a fade clock";
                    return false;
                }
            }
            else if (!ValidTeleportTile(s, brain.Ai1, brain.Ai2) ||
                state == -2 && (brain.Ai3 < 0f || brain.Ai3 > 250f ||
                    ((int)brain.Ai3 % 25) != 0) ||
                state == -3 && (brain.Ai3 < 5f || brain.Ai3 > 255f ||
                    ((255 - (int)brain.Ai3) % 25) != 0))
            {
                reason = "Brain phase-two fade clock or destination is impossible in single-player";
                return false;
            }
            reason = null;
            return true;
        }

        private static bool ValidTeleportTile(CombatSnapshot s, float x,
            float y)
        {
            if (!ClassicSecondaryBossContract.IsExactInt(x) ||
                !ClassicSecondaryBossContract.IsExactInt(y)) return false;
            var maxX = s.Player.WorldRight > 0f ?
                (int)(s.Player.WorldRight / 16f) + 1 : int.MaxValue;
            var maxY = s.Player.WorldBottom > 0f ?
                (int)(s.Player.WorldBottom / 16f) + 1 : int.MaxValue;
            return x > 0f && y > 0f && x < maxX && y < maxY;
        }

        private static TargetSnapshot SelectCreeper(CombatSnapshot s,
            BossMemory memory, TargetSnapshot brain)
        {
            var selected = brain;
            var best = float.MaxValue;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Type != 267 || candidate.Life <= 0 ||
                    candidate.Invulnerable || !candidate.Chaseable ||
                    candidate.LineOfSightKnown && !candidate.HasLineOfSight)
                    continue;
                var score = Vec2.DistanceSquared(candidate.Center,
                    s.Player.Center);
                if (candidate.Ai0 == 1f) score -= 90f * 90f;
                if (candidate.Key == memory.PreviousTargetKey)
                    score -= 70f * 70f;
                if (score >= best) continue;
                best = score;
                selected = candidate;
            }
            return selected;
        }
    }

    internal sealed class QueenBeeStrategy : BossStrategyBase
    {
        public QueenBeeStrategy() : base("queen-bee", 820, 380, 4.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 222);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 222);
            var nativeEnrageFactor = 0f;
            if (s.NativeContextKnown)
            {
                QueenBeeNativeEnrageObservation nativeEnrage;
                if (!t.Ai0Known || !t.Ai1Known || !t.Ai2Known ||
                    !TryGetNativeEnrage(s, t.Key, out nativeEnrage))
                    return UnknownNativeState(s, t,
                        "queen-bee-missing-native-enrage-or-ai");
                nativeEnrageFactor = nativeEnrage.NativeEnrageFactor;
            }
            // AI_043 is already a complete attack-state certificate.  Speed is
            // not: an ordinary low-life Expert charge can exceed 13 px/tick,
            // while a fast reposition is not a charge.  Reading ai[] also lets
            // a controller join halfway through a cycle without inventing a
            // fresh opening state.
            var state = NativeIntegerState(t.Ai0, -1, 5);
            var sequence = t.Ai1;
            var brake = NativeNonnegativeInteger(t.Ai2);
            if (state == int.MinValue || float.IsNaN(sequence) ||
                float.IsInfinity(sequence) || sequence < 0f ||
                brake == int.MinValue)
                return UnknownNativeState(s, t,
                    "queen-bee-invalid-native-clock");
            var stingerClockLimit = 0;
            if (state == 3)
            {
                // AI_043 chooses the stinger period/volley count on entry to
                // this branch.  In particular, a hit which crosses 50%, 33%
                // or 10% life does not rewrite ai[1] or retroactively shorten
                // the already-running branch.  Keep the first observed
                // envelope until the native state leaves 3.
                var continuingStingers = m.QueenBeeStingerTargetKey == t.Key &&
                    m.QueenBeeStingerPeriod > 0 &&
                    m.QueenBeeStingerClockLimit > 0 &&
                    m.PhaseId != null && m.PhaseId.IndexOf("stinger",
                        StringComparison.Ordinal) >= 0;
                if (!continuingStingers)
                {
                    m.QueenBeeStingerTargetKey = t.Key;
                    m.QueenBeeStingerPeriod = QueenBeeStingerPeriod(s, t,
                        nativeEnrageFactor);
                    m.QueenBeeStingerClockLimit =
                        QueenBeeStingerClockLimit(s, m.QueenBeeStingerPeriod,
                            nativeEnrageFactor);
                }
                stingerClockLimit = m.QueenBeeStingerClockLimit;
            }
            else
            {
                m.QueenBeeStingerTargetKey = -1;
                m.QueenBeeStingerPeriod = 0;
                m.QueenBeeStingerClockLimit = 0;
            }
            if (!ValidNativePhase(s, t, state, sequence, brake,
                    nativeEnrageFactor, stingerClockLimit))
                return UnknownNativeState(s, t,
                    "queen-bee-impossible-native-phase-tuple");
            string phase;
            BossPattern pattern;
            var horizontal = 0;
            var vertical = 0;
            var preferDash = false;
            var ownsHorizontal = false;
            var ownsMovement = false;
            var jumpAction = JumpAction.Release;
            var distance = 440f;
            var margin = 44f;
            var runway = StableRunDirection(s, t, m);
            switch (state)
            {
                case -1:
                    phase = "choose-next-native-attack";
                    pattern = BossPattern.HorizontalKite;
                    horizontal = runway;
                    break;
                case 0:
                    var chargeSequence = NativeNonnegativeInteger(sequence);
                    var aligning = (chargeSequence & 1) == 0;
                    var braking = !aligning && brake != 0;
                    phase = aligning ? "charge-align-telegraph" :
                        braking ? "charge-braking-return" :
                        "horizontal-charge-committed";
                    pattern = BossPattern.PerpendicularDashDodge;
                    // The alignment half-cycle is deterministic too.  Begin
                    // opening the perpendicular lane before velocity spikes.
                    if (!aligning && !braking)
                    {
                        // AI_043 commits this branch to a horizontal line.
                        // Brake to rest during alignment and jump in place while
                        // the Queen is still approaching. Running with the dash
                        // lowers relative speed enough for an ordinary jump to
                        // land before contact. Once her center has crossed the
                        // player's center, release immediately so landing cannot
                        // start a second jump during the same committed pass.
                        horizontal = 0;
                        var approaching = ChargeStillApproaching(s.Player, t);
                        var impactTicks = HorizontalImpactTicks(s.Player, t);
                        jumpAction = approaching && impactTicks <= 26f ?
                            GroundChargeJumpAction(s.Player) :
                            JumpAction.Release;
                        vertical = RequestsJump(jumpAction) ? 1 : 0;
                        ownsHorizontal = true;
                        ownsMovement = true;
                    }
                    else
                    {
                        // AI_043 can advance an even ai[1] to the committed
                        // charge during this very NPC update when the vertical
                        // alignment band is already satisfied.  Waiting for the
                        // next odd snapshot is one tick too late at close range.
                        // Use the native target-directed speed and a swept
                        // rectangle test to reserve the same jump window as the
                        // committed branch; otherwise keep the deliberate
                        // braking/coast closure and do not spend a jump early.
                        horizontal = 0;
                        var predictedCharge = QueenBeeChargeVelocity(s, t);
                        var alignmentBand = 20f + 20f * nativeEnrageFactor;
                        var verticalGap = Math.Abs(s.Player.Center.Y -
                            t.Center.Y);
                        var alignmentLikely = verticalGap < alignmentBand ||
                            verticalGap < alignmentBand + 80f;
                        var predictedImpact = alignmentLikely ?
                            PredictedChargeImpactTicks(s.Player, t,
                                predictedCharge) : float.PositiveInfinity;
                if (alignmentLikely && predictedImpact <= 60f)
                        {
                            jumpAction = GroundChargeJumpAction(s.Player);
                            vertical = RequestsJump(jumpAction) ? 1 : 0;
                            // A nearly vertical/overlapping charge has no
                            // useful horizontal crossing line.  Leave the
                            // explicit jump closure in place for ordinary
                            // horizontal charges, but step to the observed
                            // perpendicular side when X separation is already
                            // consumed.
                            if (Math.Abs(predictedCharge.X) < 2f ||
                                HorizontalBoundsOverlap(s.Player, t))
                            {
                                int escapeHorizontal;
                                int escapeVertical;
                                ChargeEscape(s, t, predictedCharge,
                                    out escapeHorizontal, out escapeVertical);
                                if (escapeHorizontal != 0)
                                    horizontal = escapeHorizontal;
                                if (escapeVertical != 0)
                                    vertical = escapeVertical;
                            }
                        }
                        else vertical = 0;
                        ownsHorizontal = true;
                        ownsMovement = true;
                    }
                    preferDash = !aligning && !braking;
                    distance = 520f + nativeEnrageFactor * 60f;
                    margin = (preferDash ? 76f : 58f) +
                        nativeEnrageFactor * 24f;
                    break;
                case 1:
                    var beeWaveThreshold = Math.Max(0,
                        (int)(40f - 18f * nativeEnrageFactor));
                    // AI_043 resets ai[1] after each spawn; it is not a
                    // continuously wrapping clock.  In the ordinary
                    // single-target case the spawn happens on the first
                    // update which makes ai[1] greater than this threshold.
                    // Use the remaining raw clock here so the threshold edge
                    // stays telegraphed instead of wrapping back to "spacing".
                    var untilBeeWave = Math.Max(0, (int)Math.Ceiling(
                        beeWaveThreshold - sequence));
                    phase = untilBeeWave <= 4 ? "bee-wave-imminent" :
                        "bee-wave-spacing";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = runway;
                    distance = 500f + nativeEnrageFactor * 55f;
                    margin += nativeEnrageFactor * 22f;
                    break;
                case 2:
                    phase = "move-above-before-bee-waves";
                    pattern = BossPattern.HorizontalKite;
                    horizontal = runway;
                    break;
                case 3:
                    var stingerClock = NativeNonnegativeInteger(sequence);
                    if (stingerClock == int.MinValue)
                        return UnknownNativeState(s, t,
                            "queen-bee-invalid-stinger-clock");
                    var period = m.QueenBeeStingerPeriod > 0 ?
                        m.QueenBeeStingerPeriod : QueenBeeStingerPeriod(s, t,
                            nativeEnrageFactor);
                    var untilStinger = period <= 0 ? 0 :
                        (period - 1 - stingerClock % period + period) % period;
                    phase = untilStinger <= 4 ?
                        "stinger-imminent" : "stinger-volley-spacing";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = runway;
                    vertical = untilStinger <= 4 ? -PerpendicularY(s.Player, t) : 0;
                    distance = 520f;
                    margin = (untilStinger <= 4 ? 64f : 46f) +
                        nativeEnrageFactor * 24f;
                    break;
                case 4:
                    phase = "native-long-range-reacquire";
                    pattern = BossPattern.HorizontalKite;
                    horizontal = runway;
                    distance = 480f;
                    break;
                case 5:
                    return Departing(s, t);
                default:
                    return UnknownNativeState(s, t,
                        "queen-bee-unrecognized-native-state");
            }
            // States 1/2/3 (and the short chooser/reacquire state) can move
            // the Queen's AABB through the runway without presenting the
            // horizontal charge certificate used by state 0.  The generic
            // threat scorer only sees that transition after the native NPC
            // update, which is too late when the body is already overlapping
            // the player.  Reserve a small, bounds/TTC based escape lane here
            // so the planner keeps a stable side and can launch a jump only
            // when the horizontal corridor is unavailable.
            if (state != 0 && state != 5 &&
                ApplyQueenBeeContactEscape(s, t, m, ref horizontal,
                    ref vertical, ref jumpAction, ref ownsHorizontal,
                    ref ownsMovement))
                phase += "-contact-escape";
            bool projectileUrgent;
            int projectileHorizontal;
            bool projectileVertical;
            if (TryQueenBeeProjectileEscape(s, t, m, out projectileUrgent,
                out projectileHorizontal, out projectileVertical))
            {
                if (projectileHorizontal != 0)
                    horizontal = projectileHorizontal;
                var projectileJump = projectileVertical || projectileUrgent ?
                    GroundChargeJumpAction(s.Player) : JumpAction.Release;
                if (RequestsJump(projectileJump))
                {
                    jumpAction = projectileJump;
                    vertical = 1;
                    // Keep the signed runway/contact side, but reserve the
                    // vertical posture while a projectile is inside the
                    // short swept window.  An urgent overlap owns the whole
                    // candidate so the generic orbit scorer cannot trade the
                    // jump for a lower-distance horizontal path.
                    ownsMovement = projectileUrgent || projectileVertical;
                    ownsHorizontal = true;
                    phase += "-projectile-escape";
                }
                else if (projectileHorizontal != 0)
                {
                    // A lateral projectile which is still outside the short
                    // vertical crossing window should not consume a jump.
                    // The signed input is nevertheless owned for the small
                    // dodge latch so the runway controller cannot immediately
                    // steer back into the projectile's path.
                    ownsHorizontal = true;
                    phase += "-projectile-lateral-escape";
                }
                m.QueenBeeProjectileDodgeTicks = 8;
            }
            // During the even alignment half of AI_043 state 0 the Queen is
            // about to acquire the player's horizontal line.  A lateral
            // stinger dodge is not allowed to keep the player sprinting along
            // that line: the Queen's native charge speed is much higher and
            // will catch a same-direction runner before the jump has opened a
            // vertical lane.  Brake first, then launch the reviewed jump
            // window early enough for the committed odd frame.
            if (state == 0 &&
                (NativeNonnegativeInteger(sequence) & 1) == 0)
            {
                var alignmentBand = 42f + 24f * nativeEnrageFactor;
                var alignmentGap = Math.Abs(s.Player.Center.Y -
                    t.Center.Y);
                var horizontalGap = Math.Abs(s.Player.Center.X -
                    t.Center.X);
                var predictedCharge = QueenBeeChargeVelocity(s, t);
                var predictedImpact = alignmentGap <= alignmentBand + 150f ?
                    PredictedChargeImpactTicks(s.Player, t,
                        predictedCharge) : float.PositiveInfinity;
                if (alignmentGap <= alignmentBand + 150f &&
                    horizontalGap <= 900f && predictedImpact <= 48f)
                {
                    horizontal = 0;
                    ownsHorizontal = true;
                    ownsMovement = true;
                    phase += "-charge-alignment-priority";
                    var alignmentJump = s.Player.Jump.Known ?
                        GroundChargeJumpAction(s.Player) : JumpAction.Release;
                    if (s.Player.Jump.Known && RequestsJump(alignmentJump))
                    {
                        jumpAction = alignmentJump;
                        vertical = 1;
                    }
                }
            }
            // A committed native charge has priority over a merely lateral
            // projectile dodge.  The projectile branch can otherwise replace
            // the charge's zero/escape lane with the projectile travel
            // direction on the exact frame where Queen Bee is already only a
            // few ticks from the player's runway.  This is especially common
            // while the player is descending: GroundChargeJumpAction quite
            // correctly returns Release, so horizontal braking is the only
            // remaining safe degree of freedom.
            if (state == 0 && ChargeStillApproaching(s.Player, t))
            {
                var chargeImpact = HorizontalImpactTicks(s.Player, t);
                var chargeVerticalGap = Math.Abs(s.Player.Center.Y -
                    t.Center.Y);
                if (chargeImpact <= 24f && chargeVerticalGap <= 220f)
                {
                    // A jump/vertical lane is safer than matching the Queen's
                    // high-speed horizontal charge.  Keep the deliberate
                    // braking closure whenever a native jump edge is live;
                    // only an unavailable jump falls back to a signed side.
                    var chargeSide = s.Player.Center.X < t.Center.X ? -1 : 1;
                    horizontal = RequestsJump(jumpAction) ? 0 : chargeSide;
                    ownsHorizontal = true;
                    phase += "-charge-contact-priority";
                    // If a fresh ground/cloud edge is actually available, keep
                    // it; otherwise the signed horizontal reversal remains
                    // authoritative and does not invent a jump while airborne.
                    if (s.Player.Jump.Known && RequestsJump(jumpAction))
                        vertical = 1;
                }
            }
            // A Jungle arena can retain a hostile Man Eater (NPC 43) or a
            // related hive-pressure NPC on the runway.  Those contacts are
            // not part of Queen Bee's native AI clock and therefore cannot be
            // handled by the state-0/1/2/3 controller alone.  The generic
            // candidate scorer may also be unable to change the signed lane
            // while a projectile escape owns horizontal closure.  Reserve a
            // short, explicit escape for an observed pressure body before
            // selecting the firing target; this is bounded to the current
            // local contact horizon and never chases distant ambience.
            bool ambientPressureUrgent;
            if (TryQueenBeeAmbientPressureEscape(s, t, ref horizontal,
                ref vertical, ref jumpAction, ref ownsHorizontal,
                ref ownsMovement, out ambientPressureUrgent))
            {
                phase += ambientPressureUrgent ?
                    "-ambient-pressure-emergency" : "-ambient-pressure-escape";
            }
            // The Queen's body is the highest-priority native hazard.  A
            // projectile or ambient contact observed in the same frame may
            // otherwise replace the signed charge lane and make the player
            // steer back through the Queen during a reposition.  Re-apply a
            // bounded swept-body closure after those auxiliary branches.  It
            // does not invent a jump: GroundChargeJumpAction only returns a
            // real currently executable edge (ground/held jump/cloud).
            bool queenBodyUrgent;
            if (ApplyQueenBeeBodyClosure(s, t, ref horizontal, ref vertical,
                ref jumpAction, ref ownsHorizontal, ref ownsMovement,
                out queenBodyUrgent))
            {
                phase += queenBodyUrgent ?
                    "-queen-body-contact-priority" :
                    "-queen-body-charge-telegraph";
            }
            // Projectile/contact ownership is deliberately strong, but it
            // must not override a hard, previously observed runway edge.  In
            // an open Jungle strip the native arena scan has no wall to hit;
            // once the player leaves the support span, the scan can continue
            // to report its maximum clearance and a latched +1 dodge would
            // otherwise carry the player out of the arena forever.
            if (ApplyQueenBeeRunwayBoundary(s, m, ref horizontal, ref vertical,
                ref jumpAction, ref ownsHorizontal, ref ownsMovement))
                phase += "-runway-edge-reversal";
            var target = SelectPressureTarget(s, t, m.PreviousTargetKey);
            var visible = target.LineOfSightKnown ? target.HasLineOfSight :
                target.Key == t.Key && s.LineOfSightToPrimary;
            var inRange = s.Weapon == null ||
                !s.Weapon.NativeProfileRequired ||
                !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, s.Player.Center) <=
                    s.Weapon.Profile.ConservativeRangePixels *
                    s.Weapon.Profile.ConservativeRangePixels;
            var fire = target.Life > 0 && !target.Invulnerable &&
                target.Chaseable && visible && inRange;
            var decision = Decision(s, target, phase, pattern, distance, -100,
                horizontal, vertical, preferDash, true, false, margin, fire,
                t);
            // AI_043 already supplies a complete attack certificate.  Bypass
            // the generic direction-hold/distance orbit so a mid-cycle join
            // cannot turn back into a committed charge.  Only the exact charge
            // line owns vertical posture; projectile waves still allow the
            // hazard scorer to choose a safer jump/fall candidate.
            decision.Directive.UseExplicitMovement = true;
            // The committed native charge owns its horizontal crossing line,
            // but vertical posture remains scoreable when a stinger or another
            // live threat blocks the preferred jump. Other states keep runway
            // movement as a preference and may evade dangerous hive pressure.
            decision.Directive.OwnsHorizontalClosure = ownsHorizontal;
            decision.Directive.OwnsMovementClosure = ownsMovement;
            decision.Directive.JumpAction = jumpAction;
            return decision;
        }

        private static bool ApplyQueenBeeContactEscape(CombatSnapshot snapshot,
            TargetSnapshot queen, BossMemory memory, ref int horizontal,
            ref int vertical, ref JumpAction jumpAction,
            ref bool ownsHorizontal, ref bool ownsMovement)
        {
            var player = snapshot.Player;
            var playerBounds = new RectF(player.Position.X, player.Position.Y,
                Math.Max(1, player.Width), Math.Max(1, player.Height));
            var queenBounds = new RectF(queen.Position.X, queen.Position.Y,
                Math.Max(1, queen.Width), Math.Max(1, queen.Height));
            var verticalBand = playerBounds.Bottom > queenBounds.Top - 42f &&
                playerBounds.Top < queenBounds.Bottom + 42f;
            if (!verticalBand)
            {
                ClearQueenBeeContactLatch(memory, queen.Key);
                return false;
            }

            var horizontalGap = queenBounds.Left >= playerBounds.Right
                ? queenBounds.Left - playerBounds.Right
                : playerBounds.Left >= queenBounds.Right
                    ? playerBounds.Left - queenBounds.Right : 0f;
            var centerDelta = playerBounds.Center.X - queenBounds.Center.X;
            var relativeX = queen.Velocity.X - player.Velocity.X;
            var movingToward = Math.Abs(relativeX) > .05f &&
                centerDelta * relativeX > 0f;

            // A short swept-AABB test catches a fast Queen crossing the body
            // even when the current rectangles are still separated.  Keep the
            // window deliberately bounded; this is a contact guard, not a
            // replacement for the native projectile predictor.
            var predictedContact = false;
            var predictedTicks = 0;
            for (var tick = 0; tick <= 18; tick += 2)
            {
                var p = new RectF(playerBounds.X + player.Velocity.X * tick,
                    playerBounds.Y + player.Velocity.Y * tick,
                    playerBounds.Width, playerBounds.Height);
                var q = new RectF(queenBounds.X + queen.Velocity.X * tick,
                    queenBounds.Y + queen.Velocity.Y * tick,
                    queenBounds.Width, queenBounds.Height);
                if (!p.Intersects(q)) continue;
                predictedContact = true;
                predictedTicks = tick;
                break;
            }

            // Queen Bee's ordinary wave/stinger/vertical states commonly keep
            // the body one player-height above the runway.  Start escaping
            // before the exact overlap, with a hysteresis band large enough to
            // absorb one native teleport/update of latency.
            var active = horizontalGap <= 270f || movingToward &&
                horizontalGap <= 420f || predictedContact;
            if (!active)
            {
                ClearQueenBeeContactLatch(memory, queen.Key);
                return false;
            }

            var side = centerDelta > 0f ? 1 : centerDelta < 0f ? -1 :
                queen.Velocity.X > .05f ? -1 : queen.Velocity.X < -.05f ? 1 :
                memory.OrbitDirection == 0 ? 1 : memory.OrbitDirection;
            if (memory.QueenBeeContactTargetKey != queen.Key ||
                memory.QueenBeeContactDirection == 0)
            {
                memory.QueenBeeContactTargetKey = queen.Key;
                memory.QueenBeeContactDirection = side;
                memory.QueenBeeContactTicks = 0;
            }
            else
            {
                // If the Queen has actually crossed the player or is still
                // closing inside the near band, change sides immediately;
                // otherwise retain the previous side for a bounded interval
                // and avoid left/right oscillation on consecutive samples.
                var crossed = centerDelta * memory.QueenBeeContactDirection <
                    -48f;
                if (crossed || predictedContact && predictedTicks <= 4)
                    memory.QueenBeeContactDirection = side;
            }
            memory.QueenBeeContactTicks = Math.Min(24,
                memory.QueenBeeContactTicks + 1);
            side = memory.QueenBeeContactDirection;

            // Do not steer out of a verified runway edge.  Prefer the opposite
            // side only when it has materially more room; if neither side is
            // available, vertical escape is the last safe degree of freedom.
            var left = snapshot.Arena.ClearanceLeft;
            var right = snapshot.Arena.ClearanceRight;
            var selectedClearance = side < 0 ? left : right;
            if (selectedClearance < 112f)
            {
                var opposite = side < 0 ? right : left;
                if (opposite > selectedClearance + 96f)
                {
                    side = -side;
                    memory.QueenBeeContactDirection = side;
                    selectedClearance = opposite;
                }
            }

            horizontal = side;
            ownsHorizontal = true;

            // Jump only when the Queen is below/level with the player or when
            // both horizontal exits are effectively sealed.  Jumping directly
            // up into a hovering Queen is worse than maintaining the reviewed
            // horizontal crossing lane.
            var noHorizontalExit = left < 112f && right < 112f;
            var queenBelow = queenBounds.Center.Y >= playerBounds.Center.Y -
                12f;
            var urgent = predictedContact && predictedTicks <= 4 ||
                horizontalGap <= 24f;
            if ((noHorizontalExit || queenBelow && urgent) &&
                player.Jump.Known)
            {
                var requested = GroundChargeJumpAction(player);
                if (RequestsJump(requested))
                {
                    jumpAction = requested;
                    vertical = 1;
                    ownsMovement = true;
                }
            }
            return true;
        }

        private static void ClearQueenBeeContactLatch(BossMemory memory,
            int targetKey)
        {
            if (memory.QueenBeeContactTargetKey != targetKey) return;
            // Keep a tiny release hysteresis only while the target is still
            // known; the next near-band entry chooses a fresh side after the
            // body has genuinely cleared the player.
            if (memory.QueenBeeContactTicks > 0)
                memory.QueenBeeContactTicks--;
            if (memory.QueenBeeContactTicks == 0)
            {
                memory.QueenBeeContactTargetKey = -1;
                memory.QueenBeeContactDirection = 0;
            }
        }

        private static bool TryQueenBeeProjectileEscape(
            CombatSnapshot snapshot, TargetSnapshot queen, BossMemory memory,
            out bool urgent, out int escapeHorizontal, out bool escapeVertical)
        {
            urgent = false;
            escapeHorizontal = 0;
            escapeVertical = false;
            var player = snapshot.Player;
            var playerBounds = new RectF(player.Position.X, player.Position.Y,
                Math.Max(1, player.Width), Math.Max(1, player.Height));
            // These are the three ordinary Queen projectile families observed
            // in the native 1.4.5.8 encounter.  The type filter is deliberately
            // narrow: ambient projectiles must not turn every frame of a long
            // runway into a forced jump.
            var bestTick = int.MaxValue;
            ThreatSnapshot bestThreat = default(ThreatSnapshot);
            var hasThreat = false;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.Projectile ||
                    (threat.Type != 176 && threat.Type != 719 &&
                     threat.Type != 55) || threat.Damage <= 0)
                    continue;
                var firstHit = -1;
                for (var tick = 0; tick <= 18; tick += 2)
                {
                    var p = new RectF(playerBounds.X + player.Velocity.X * tick,
                        playerBounds.Y + player.Velocity.Y * tick,
                        playerBounds.Width, playerBounds.Height);
                    var q = threat.Trajectory == ThreatTrajectory.Linear ?
                        threat.BoundsAt(tick) : threat.BoundsAt(0);
                    if (!p.Inflated(14f).Intersects(q.Inflated(10f)))
                        continue;
                    firstHit = tick;
                    break;
                }
                if (firstHit < 0 || firstHit > bestTick) continue;
                bestTick = firstHit;
                bestThreat = threat;
                hasThreat = true;
            }
            if (!hasThreat)
            {
                // Keep the signed escape for a few frames after the projectile
                // leaves the sampled corridor.  Native projectile updates can
                // create/remove a slot between two planner snapshots; without
                // this hysteresis the stable runway can immediately reverse
                // into the same projectile lane.
                if (memory.QueenBeeProjectileDodgeTicks > 0 &&
                    memory.QueenBeeProjectileDodgeDirection != 0)
                {
                    memory.QueenBeeProjectileDodgeTicks--;
                    escapeHorizontal = memory.QueenBeeProjectileDodgeDirection;
                    return true;
                }
                memory.QueenBeeProjectileDodgeDirection = 0;
                return false;
            }

            urgent = bestTick <= 6;
            var projectileCenter = bestThreat.BoundsAt(0).Center;
            var horizontalVelocity = bestThreat.Velocity.X;
            if (Math.Abs(horizontalVelocity) >= 1.5f)
            {
                // For a crossing projectile, continuing in its travel
                // direction increases separation after the crossing and is
                // the stable vanilla-safe lane (right-to-left => left,
                // left-to-right => right).  Do not use the projectile's
                // current side: that reverses the lane for fast stingers.
                escapeHorizontal = Math.Sign(horizontalVelocity);
            }
            else if (Math.Abs(player.Center.X - projectileCenter.X) > 18f)
            {
                // Near-vertical projectile: leave the projectile's current
                // side.  A vertical dodge is also requested below.
                escapeHorizontal = player.Center.X >= projectileCenter.X ? 1 : -1;
            }
            else
            {
                // A newly spawned/centered stinger can have no useful lateral
                // separation yet. Returning zero here is especially unsafe
                // during Queen state 0, which deliberately coasts while
                // reserving the charge jump. Choose a verified runway side
                // instead of waiting for the next native slot update.
                escapeHorizontal = memory.OrbitDirection != 0 ?
                    memory.OrbitDirection :
                    snapshot.Arena.ClearanceRight >= snapshot.Arena.ClearanceLeft ?
                        1 : -1;
            }
            escapeVertical = Math.Abs(bestThreat.Velocity.Y) >= 2f || urgent;

            // Respect a verified arena edge.  If the travel-direction lane is
            // effectively sealed, use the opposite lane only when it has a
            // meaningful amount of additional clearance.
            if (escapeHorizontal < 0 && snapshot.Arena.ClearanceLeft < 112f &&
                snapshot.Arena.ClearanceRight > snapshot.Arena.ClearanceLeft + 96f)
                escapeHorizontal = 1;
            else if (escapeHorizontal > 0 && snapshot.Arena.ClearanceRight < 112f &&
                snapshot.Arena.ClearanceLeft > snapshot.Arena.ClearanceRight + 96f)
                escapeHorizontal = -1;

            if (escapeHorizontal != 0)
                memory.QueenBeeProjectileDodgeDirection = escapeHorizontal;
            memory.QueenBeeProjectileDodgeTicks = 8;
            return true;
        }

        private static bool TryQueenBeeAmbientPressureEscape(
            CombatSnapshot snapshot, TargetSnapshot queen,
            ref int horizontal, ref int vertical, ref JumpAction jumpAction,
            ref bool ownsHorizontal, ref bool ownsMovement,
            out bool urgent)
        {
            urgent = false;
            var player = snapshot.Player;
            var playerBounds = new RectF(player.Position.X, player.Position.Y,
                Math.Max(1, player.Width), Math.Max(1, player.Height));
            var bestTicks = int.MaxValue;
            ThreatSnapshot bestThreat = default(ThreatSnapshot);
            var found = false;

            // NPC contact snapshots are already broad-phase filtered by the
            // native facade.  Use a small fixed one-tick sweep here so a
            // stationary Man Eater whose vine/body enters between two planner
            // samples is handled before the next native collision update.
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.NpcContact ||
                    threat.Damage <= 0 || threat.Type == queen.Type ||
                    !IsHivePressure(threat.Type))
                    continue;
                var firstContact = -1;
                // Queen's Bee/BeeSmall minions are small, fast falling bodies.
                // In the focused native run a BeeSmall was still about 170 px
                // above a grounded player at the last ordinary 24-tick sample,
                // then crossed the player before the next planner observation.
                // Admit a bounded early-warning window for those two exact
                // native types; keep the shorter horizon for unrelated Jungle
                // contacts so distant ambience cannot own the route.
                var beeMinion = threat.Type == 210 || threat.Type == 211;
                var predictionHorizon = beeMinion ? 48 : 24;
                for (var tick = 0; tick <= predictionHorizon; tick++)
                {
                    var p = new RectF(playerBounds.X + player.Velocity.X * tick,
                        playerBounds.Y + player.Velocity.Y * tick,
                        playerBounds.Width, playerBounds.Height);
                    var q = threat.BoundsAt(tick);
                    if (!p.Inflated(10f).Intersects(q.Inflated(8f)))
                        continue;
                    firstContact = tick;
                    break;
                }
                if (firstContact < 0 || firstContact >= bestTicks)
                    continue;
                bestTicks = firstContact;
                bestThreat = threat;
                found = true;
            }
            if (!found) return false;

            var beeMinionThreat = bestThreat.Type == 210 ||
                bestThreat.Type == 211;
            urgent = bestTicks <= (beeMinionThreat ? 12 : 8);
            var threatCenter = bestThreat.BoundsAt(0).Center;
            var delta = player.Center.X - threatCenter.X;
            var side = delta > 4f ? 1 : delta < -4f ? -1 :
                bestThreat.Velocity.X > .05f ? -1 :
                bestThreat.Velocity.X < -.05f ? 1 :
                snapshot.Arena.ClearanceRight >= snapshot.Arena.ClearanceLeft ? 1 : -1;

            // Keep the escape inside the measured runway.  A pressure body at
            // an edge is safer to pass on the side with real clearance than to
            // reverse repeatedly on successive native frames.
            var selectedClearance = side < 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            if (selectedClearance < 112f)
            {
                var opposite = side < 0 ? snapshot.Arena.ClearanceRight :
                    snapshot.Arena.ClearanceLeft;
                if (opposite > selectedClearance + 96f)
                    side = -side;
            }
            horizontal = side;
            ownsHorizontal = true;

            // Man Eater and the small hive bodies are ground-level threats in
            // the reviewed pre-boss arena.  A native jump is the only reliable
            // vertical separation when the horizontal closure is already owned
            // by a Queen projectile.  Request it before exact overlap rather
            // than waiting for the post-Hurt snapshot.
            // A falling Queen minion needs the jump edge before it enters the
            // short contact band.  On-foot Cloud-in-a-Bottle routes otherwise
            // spend their only jump after the bee has already crossed the
            // player's vertical corridor.  The larger window is restricted to
            // the exact Bee/BeeSmall native IDs and remains bounded.
            var jumpWindow = beeMinionThreat ? 36 : 18;
            if (bestTicks <= jumpWindow && player.Jump.Known)
            {
                var requested = GroundChargeJumpAction(player);
                if (RequestsJump(requested))
                {
                    jumpAction = requested;
                    vertical = 1;
                    ownsMovement = true;
                }
            }
            return true;
        }

        private static bool ApplyQueenBeeBodyClosure(
            CombatSnapshot snapshot, TargetSnapshot queen,
            ref int horizontal, ref int vertical, ref JumpAction jumpAction,
            ref bool ownsHorizontal, ref bool ownsMovement, out bool urgent)
        {
            urgent = false;
            if (snapshot == null || snapshot.Player == null)
                return false;

            var player = snapshot.Player;
            var playerBounds = new RectF(player.Position.X, player.Position.Y,
                Math.Max(1, player.Width), Math.Max(1, player.Height));
            var queenBounds = new RectF(queen.Position.X, queen.Position.Y,
                Math.Max(1, queen.Width), Math.Max(1, queen.Height));

            // Use the actual native body velocity first.  The short swept
            // window covers the interval between two facade samples and is
            // deliberately capped; this helper is a contact closure, not a
            // long-horizon prediction of an arbitrary NPC.
            var bodyTicks = float.PositiveInfinity;
            for (var tick = 0; tick <= 36; tick += 2)
            {
                var p = new RectF(playerBounds.X + player.Velocity.X * tick,
                    playerBounds.Y + player.Velocity.Y * tick,
                    playerBounds.Width, playerBounds.Height);
                var q = new RectF(queenBounds.X + queen.Velocity.X * tick,
                    queenBounds.Y + queen.Velocity.Y * tick,
                    queenBounds.Width, queenBounds.Height);
                if (p.Inflated(10f).Intersects(q.Inflated(8f)))
                {
                    bodyTicks = tick;
                    break;
                }
            }

            var state = queen.Ai0Known ?
                NativeIntegerState(queen.Ai0, -1, 5) : int.MinValue;
            var sequence = queen.Ai1;
            var evenChargeTelegraph = state == 0 &&
                !float.IsNaN(sequence) && !float.IsInfinity(sequence) &&
                sequence >= 0f &&
                (NativeNonnegativeInteger(sequence) & 1) == 0;
            var centerDelta = playerBounds.Center.X - queenBounds.Center.X;
            var horizontalGap = queenBounds.Left >= playerBounds.Right
                ? queenBounds.Left - playerBounds.Right
                : playerBounds.Left >= queenBounds.Right
                    ? playerBounds.Left - queenBounds.Right : 0f;
            var verticalGap = Math.Abs(playerBounds.Center.Y -
                queenBounds.Center.Y);

            // During the even half of AI_043 state 0 the next native update
            // commits a charge toward the player's current centre.  Native
            // velocity is still zero at that point, so the actual-body sweep
            // alone cannot see the danger.  Reconstruct only this reviewed
            // charge vector and keep the admission bounded to the local lane.
            var chargeTicks = float.PositiveInfinity;
            if (evenChargeTelegraph && horizontalGap <= 760f &&
                verticalGap <= 220f)
            {
                var chargeVelocity = QueenBeeChargeVelocity(snapshot, queen);
                chargeTicks = PredictedChargeImpactTicks(player, queen,
                    chargeVelocity);
            }

            var chargeTelegraph = evenChargeTelegraph &&
                chargeTicks <= 60f;
            var bodyContact = bodyTicks <= 36f;
            if (!bodyContact && !chargeTelegraph) return false;

            urgent = bodyContact && bodyTicks <= 24f ||
                chargeTelegraph && chargeTicks <= 24f;

            // Move away from the body/charge line.  This is intentionally
            // based on the observed relative geometry rather than merely
            // negating the projectile direction: a Queen travelling left on
            // the player's left side is safely crossed by continuing left,
            // while a late projectile may be travelling the opposite way.
            int side;
            if (Math.Abs(centerDelta) > 12f)
                side = centerDelta > 0f ? 1 : -1;
            else if (Math.Abs(queen.Velocity.X) > .5f)
                side = Math.Sign(queen.Velocity.X);
            else
            {
                var charge = QueenBeeChargeVelocity(snapshot, queen);
                side = Math.Abs(charge.X) > .5f ? Math.Sign(charge.X) :
                    snapshot.Arena.ClearanceRight >= snapshot.Arena.ClearanceLeft ?
                    1 : -1;
            }

            var selectedClearance = side < 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            if (selectedClearance < 112f)
            {
                var opposite = side < 0 ? snapshot.Arena.ClearanceRight :
                    snapshot.Arena.ClearanceLeft;
                if (opposite > selectedClearance + 96f) side = -side;
            }
            horizontal = side;
            ownsHorizontal = true;

            // A jump is useful only when the body is at or near the player's
            // vertical lane.  GroundChargeJumpAction is fail-closed for an
            // airborne player with no remaining jump/cloud resource, so this
            // branch cannot fabricate a second jump in mid-air.
            var queenNotFarAbove = queenBounds.Center.Y >=
                playerBounds.Center.Y - 120f || verticalGap <= 220f;
            if ((urgent || chargeTelegraph && chargeTicks <= 48f) &&
                queenNotFarAbove && player.Jump.Known)
            {
                var requested = GroundChargeJumpAction(player);
                if (RequestsJump(requested))
                {
                    jumpAction = requested;
                    vertical = 1;
                    ownsMovement = true;
                }
            }
            return true;
        }

        private static bool ApplyQueenBeeRunwayBoundary(
            CombatSnapshot snapshot, BossMemory memory, ref int horizontal,
            ref int vertical, ref JumpAction jumpAction,
            ref bool ownsHorizontal, ref bool ownsMovement)
        {
            RememberQueenBeeRunway(snapshot, memory);
            if (!memory.QueenBeeRunwayBoundsKnown ||
                memory.QueenBeeRunwayRight <= memory.QueenBeeRunwayLeft)
                return false;

            var player = snapshot.Player;
            var left = player.Position.X;
            var right = player.Position.X + Math.Max(1, player.Width);
            var speed = Math.Abs(player.Velocity.X);
            var slowdown = Math.Max(.01f, player.RunSlowdown);
            // The margin includes the observed stopping distance and a small
            // input/observation cushion.  Cap it so a very fast native charge
            // does not collapse an otherwise usable platform into its center.
            var margin = 96f + speed * speed / (2f * slowdown) +
                speed * 4f;
            margin = Math.Min(420f, Math.Max(112f, margin));

            // The support span is the strongest evidence, but an open-air scan
            // can be wider than the actual platform (and can remain cached for
            // a few native frames).  Intersect it with the current local open
            // rectangle when that rectangle is a well-formed, body-sized
            // observation.  This keeps a source-owned projectile/contact lane
            // from using the scan's maximum width to run off a real ledge.
            var runwayLeft = memory.QueenBeeRunwayLeft;
            var runwayRight = memory.QueenBeeRunwayRight;
            var open = snapshot.Arena.LocalOpenBounds;
            var openBoundsKnown = Finite(open.Left) && Finite(open.Right) &&
                open.Right > open.Left && open.Width >= player.Width + 32f;
            if (openBoundsKnown)
            {
                var openLeft = open.Left + 8f;
                var openRight = open.Right - 8f;
                if (openRight - openLeft >= player.Width + 16f)
                {
                    runwayLeft = Math.Max(runwayLeft, openLeft);
                    runwayRight = Math.Min(runwayRight, openRight);
                }
            }
            // Never replace a witnessed support with an impossible intersection
            // caused by a stale/partial scan.  The latched support remains the
            // conservative fallback in that case.
            if (runwayRight <= runwayLeft + player.Width)
            {
                runwayLeft = memory.QueenBeeRunwayLeft;
                runwayRight = memory.QueenBeeRunwayRight;
            }

            var toward = horizontal != 0 ? horizontal :
                Math.Abs(player.Velocity.X) > .05f ?
                    Math.Sign(player.Velocity.X) : 0;
            var clearanceLeft = snapshot.Arena.ClearanceLeft;
            var clearanceRight = snapshot.Arena.ClearanceRight;
            var clearanceLeftKnown = Finite(clearanceLeft) && clearanceLeft >= 0f;
            var clearanceRightKnown = Finite(clearanceRight) && clearanceRight >= 0f;
            var atLeft = left <= runwayLeft + margin ||
                clearanceLeftKnown && clearanceLeft < margin;
            var atRight = right >= runwayRight - margin ||
                clearanceRightKnown && clearanceRight < margin;
            var outsideLeft = right < runwayLeft - 32f;
            var outsideRight = left > runwayRight + 32f;
            var reverse = 0;
            if ((atLeft && toward < 0) || outsideLeft)
                reverse = 1;
            else if ((atRight && toward > 0) || outsideRight)
                reverse = -1;
            // If the player is already airborne with no remaining finite
            // flight, use the projected body footprint to reacquire the
            // witnessed support even when the source escape is currently
            // neutral or already points inward.  Do not alter a healthy
            // mid-platform route merely because wings are present.
            if (reverse == 0 && !player.OnGround &&
                QueenBeeFlightExhausted(snapshot))
            {
                var projectedX = player.Position.X + player.Velocity.X * 8f;
                if (projectedX < runwayLeft + 32f)
                    reverse = 1;
                else if (projectedX + player.Width > runwayRight - 32f)
                    reverse = -1;
            }
            if (reverse == 0) return false;

            horizontal = reverse;
            ownsHorizontal = true;
            // An airborne player whose finite flight resource is exhausted
            // cannot turn an emergency edge reversal into a made-up flight
            // input. Keep the native jump state neutral while braking back over
            // known support. This covers both no-flight routes and a verified
            // zero wing/rocket budget; ordinary airborne players with reserve
            // flight retain the source controller's vertical dodge.
            if (!player.OnGround && QueenBeeFlightExhausted(snapshot))
            {
                vertical = 0;
                jumpAction = JumpAction.Release;
                ownsMovement = true;
            }
            return true;
        }

        private static bool QueenBeeFlightExhausted(CombatSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Mobility == null) return true;
            var mobility = snapshot.Mobility;
            if (!mobility.HasFiniteFlightResource) return true;
            if (Finite(mobility.FlightResourceFraction) &&
                mobility.FlightResourceFraction <= .02f) return true;
            var flight = snapshot.Player.Flight;
            return flight.Known && FlightMotion.RemainingWingTicks(in flight) <= .5f;
        }

        private static void RememberQueenBeeRunway(CombatSnapshot snapshot,
            BossMemory memory)
        {
            if (snapshot == null || snapshot.Arena == null ||
                snapshot.Mobility == null || memory == null) return;
            memory.QueenBeeRunwayBoundsAge = Math.Min(
                int.MaxValue, memory.QueenBeeRunwayBoundsAge + 1);

            var inverted = snapshot.Mobility.GravityInverted;
            var candidate = inverted ? snapshot.Arena.CeilingSupport :
                snapshot.Arena.FloorSupport;
            if (!candidate.Valid || candidate.Right - candidate.Left < 160f)
            {
                var recovery = snapshot.Arena.RecoverySupport;
                if (recovery.Valid && recovery.Inverted == inverted)
                    candidate = recovery;
            }
            if (!candidate.Valid || candidate.Right <= candidate.Left ||
                !Finite(candidate.Left) || !Finite(candidate.Right) ||
                !Finite(candidate.SurfaceY)) return;

            if (!memory.QueenBeeRunwayBoundsKnown ||
                memory.QueenBeeRunwayInverted != inverted ||
                memory.QueenBeeRunwayBoundsAge > 360 ||
                Math.Abs(candidate.SurfaceY - memory.QueenBeeRunwaySurfaceY) >
                    48f)
            {
                memory.QueenBeeRunwayBoundsKnown = true;
                memory.QueenBeeRunwayInverted = inverted;
                memory.QueenBeeRunwayLeft = candidate.Left;
                memory.QueenBeeRunwayRight = candidate.Right;
                memory.QueenBeeRunwaySurfaceY = candidate.SurfaceY;
                memory.QueenBeeRunwayBoundsAge = 0;
                return;
            }

            // Same support row: widen only through overlapping observations.
            // This accounts for the facade's bounded ±96-tile scan without
            // merging separate platforms or disconnected jungle ledges.
            var overlaps = candidate.Left <= memory.QueenBeeRunwayRight +
                32f && candidate.Right >= memory.QueenBeeRunwayLeft - 32f;
            if (overlaps)
            {
                memory.QueenBeeRunwayLeft = Math.Min(
                    memory.QueenBeeRunwayLeft, candidate.Left);
                memory.QueenBeeRunwayRight = Math.Max(
                    memory.QueenBeeRunwayRight, candidate.Right);
            }
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool RequestsJump(JumpAction action) =>
            action == JumpAction.Hold || action == JumpAction.Cloud;

        private static bool ChargeStillApproaching(PlayerSnapshot player,
            TargetSnapshot queen)
        {
            if (Math.Abs(queen.Velocity.X) < .01f) return false;
            return queen.Velocity.X *
                 (player.Center.X - queen.Center.X) >= 0f;
        }

        private static float HorizontalImpactTicks(PlayerSnapshot player,
            TargetSnapshot queen)
        {
            var direction = Math.Sign(queen.Velocity.X);
            if (direction == 0) return float.PositiveInfinity;
            var gap = direction > 0 ?
                player.Position.X - (queen.Position.X + queen.Width) :
                queen.Position.X - (player.Position.X + player.Width);
            if (gap <= 0f) return 0f;
            var closingSpeed = direction *
                (queen.Velocity.X - player.Velocity.X);
            return closingSpeed > .25f ? gap / closingSpeed :
                float.PositiveInfinity;
        }

        private static Vec2 QueenBeeChargeVelocity(CombatSnapshot snapshot,
            TargetSnapshot queen)
        {
            var delta = snapshot.Player.Center - queen.Center;
            var lengthSquared = delta.X * delta.X + delta.Y * delta.Y;
            if (lengthSquared < .0001f) return new Vec2(0f, 0f);
            var speed = snapshot.Difficulty.Expert ||
                snapshot.Difficulty.Master ? 16f : 12f;
            if (snapshot.Difficulty.Expert || snapshot.Difficulty.Master)
            {
                var life = Life(queen);
                if (life < .75f) speed += 2f;
                if (life < .5f) speed += 2f;
                if (life < .25f) speed += 2f;
                if (life < .1f) speed += 2f;
            }
            QueenBeeNativeEnrageObservation observation;
            if (TryGetNativeEnrage(snapshot, queen.Key, out observation))
                speed += 7f * observation.NativeEnrageFactor;
            var length = (float)Math.Sqrt(lengthSquared);
            return new Vec2(delta.X * speed / length,
                delta.Y * speed / length);
        }

        private static bool HorizontalBoundsOverlap(PlayerSnapshot player,
            TargetSnapshot queen)
        {
            return player.Position.X < queen.Position.X + queen.Width &&
                queen.Position.X < player.Position.X + player.Width;
        }

        private static float PredictedChargeImpactTicks(
            PlayerSnapshot player, TargetSnapshot queen, Vec2 chargeVelocity)
        {
            var relative = chargeVelocity - player.Velocity;
            var distanceX = queen.Center.X - player.Center.X;
            var distanceY = queen.Center.Y - player.Center.Y;
            var halfX = (player.Width + queen.Width) * .5f;
            var halfY = (player.Height + queen.Height) * .5f;
            float entryX;
            float exitX;
            float entryY;
            float exitY;
            SweptAxisInterval(distanceX, halfX, relative.X,
                out entryX, out exitX);
            SweptAxisInterval(distanceY, halfY, relative.Y,
                out entryY, out exitY);
            var entry = Math.Max(entryX, entryY);
            var exit = Math.Min(exitX, exitY);
            if (exit < 0f || entry > exit) return float.PositiveInfinity;
            return Math.Max(0f, entry);
        }

        private static void SweptAxisInterval(float distance, float half,
            float velocity, out float entry, out float exit)
        {
            if (Math.Abs(distance) <= half)
            {
                entry = 0f;
                exit = float.PositiveInfinity;
                return;
            }
            if (Math.Abs(velocity) < .001f)
            {
                entry = float.PositiveInfinity;
                exit = float.NegativeInfinity;
                return;
            }
            var first = (-half - distance) / velocity;
            var second = (half - distance) / velocity;
            entry = Math.Min(first, second);
            exit = Math.Max(first, second);
        }

        private static JumpAction GroundChargeJumpAction(
            PlayerSnapshot player)
        {
            var jump = player.Jump;
            if (!jump.Known) return JumpAction.Hold;
            if (player.OnGround || jump.RemainingTicks > 0)
                return JumpAction.Hold;
            if (jump.CloudAvailable)
                return jump.ReleaseReady ? JumpAction.Cloud :
                    JumpAction.Release;
            return JumpAction.Release;
        }

        private static TargetSnapshot SelectPressureTarget(
            CombatSnapshot snapshot, TargetSnapshot queen,
            int previousTargetKey)
        {
            const float acquisitionRadius = 320f;
            const float retentionRadius = 384f;
            if (previousTargetKey != queen.Key)
            {
                for (var i = 0; i < snapshot.Targets.Count; i++)
                {
                    var retained = snapshot.Targets[i];
                    if (retained.Key != previousTargetKey ||
                        !IsAvailablePressure(retained)) continue;
                    if (Vec2.DistanceSquared(snapshot.Player.Center,
                            retained.Center) <= retentionRadius *
                            retentionRadius)
                        return retained;
                }
            }

            var selected = queen;
            var closest = acquisitionRadius * acquisitionRadius;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (!IsAvailablePressure(candidate)) continue;
                var distance = Vec2.DistanceSquared(snapshot.Player.Center,
                    candidate.Center);
                if (distance >= closest) continue;
                closest = distance;
                selected = candidate;
            }
            return selected;
        }

        private static bool IsAvailablePressure(TargetSnapshot candidate) =>
            IsHivePressure(candidate.Type) && candidate.Life > 0 &&
            !candidate.Invulnerable && candidate.Chaseable &&
            candidate.LineOfSightKnown && candidate.HasLineOfSight;

        private static bool IsHivePressure(int type) =>
            type == 42 || type == 43 || type == 204 || type == 210 || type == 211 ||
            type >= 231 && type <= 235;

        private static bool ValidNativePhase(CombatSnapshot snapshot,
            TargetSnapshot target, int state, float sequence, int brake,
            float nativeEnrageFactor, int stingerClockLimit = 0)
        {
            if (state == 0)
            {
                var chargeSequence = NativeNonnegativeInteger(sequence);
                var charges = 2;
                if (snapshot.Difficulty.Expert || snapshot.Difficulty.Master)
                {
                    if (target.Life < target.LifeMax / 2) charges++;
                    if (target.Life < target.LifeMax / 3) charges++;
                    if (target.Life < target.LifeMax / 5) charges++;
                }
                charges += (int)nativeEnrageFactor;
                return chargeSequence != int.MinValue &&
                    // After the last charge brakes, AI_043 exposes the final
                    // even value for one complete update.  The following
                    // update observes > 2 * charges and returns to state -1.
                    chargeSequence <= charges * 2 + 2 && brake <= 1 &&
                    ((chargeSequence & 1) != 0 || brake == 0);
            }
            if (state == 1)
            {
                var threshold = (int)(40f - 18f * nativeEnrageFactor);
                // A non-positive threshold spawns immediately and resets the
                // raw clock to zero in the same native update.
                return brake <= 5 && (threshold <= 0 ?
                    Math.Abs(sequence) <= .001f :
                    sequence <= threshold + .001f);
            }
            if (state == 2)
            {
                // The chooser resets ai[1] before entering state 2, whose
                // movement target is player.Center.X / player.Y - 200 and
                // never writes ai[1].  Any retained offset is synthetic and
                // must fail closed rather than masquerade as a native phase.
                return brake == 0 && NativeNonnegativeInteger(sequence) == 0;
            }
            if (state == 3)
            {
                var clock = NativeNonnegativeInteger(sequence);
                if (clock == int.MinValue || brake != 0) return false;
                var period = stingerClockLimit > 0 ?
                    Math.Max(1, stingerClockLimit / 20) :
                    QueenBeeStingerPeriod(snapshot, target,
                        nativeEnrageFactor);
                var limit = stingerClockLimit > 0 ? stingerClockLimit :
                    QueenBeeStingerClockLimit(snapshot, period,
                        nativeEnrageFactor);
                return clock <= limit;
            }
            // Choice/reacquire/departure can retain clocks from the preceding
            // branch for one native update, so only their finite/nonnegative
            // property is asserted above.
            return true;
        }

        private static int QueenBeeStingerPeriod(CombatSnapshot snapshot,
            TargetSnapshot target, float nativeEnrageFactor)
        {
            var period = snapshot.Difficulty.Expert ||
                snapshot.Difficulty.Master ?
                (Life(target) < .1f ? 15 : Life(target) < 1f / 3f ?
                25 : Life(target) < .5f ? 30 : 35) : 40;
            return Math.Max(1, period - (int)(5f * nativeEnrageFactor));
        }

        private static int QueenBeeStingerClockLimit(
            CombatSnapshot snapshot, int period, float nativeEnrageFactor)
        {
            // The entry period is normally multiplied by this volley count;
            // the upper bound is deliberately integer and conservative for a
            // native frame observed exactly at a threshold transition.
            var volleys = Math.Max(1, (int)Math.Floor(
                20f - 5f * nativeEnrageFactor));
            return Math.Max(1, period) * volleys;
        }

        private static int StableRunDirection(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory)
        {
            if (memory.PreviousTargetKey != target.Key ||
                memory.OrbitDirection == 0)
                memory.OrbitDirection = snapshot.Arena.ClearanceRight >=
                    snapshot.Arena.ClearanceLeft ? 1 : -1;
            var direction = memory.OrbitDirection;
            var remaining = direction > 0 ? snapshot.Arena.ClearanceRight :
                snapshot.Arena.ClearanceLeft;
            var opposite = direction > 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = speed * speed /
                (2f * Math.Max(.01f, snapshot.Player.RunSlowdown));
            var reserve = Math.Max(150f, braking + 96f);
            if (remaining < reserve && opposite > remaining + 160f)
                memory.OrbitDirection = -direction;
            return memory.OrbitDirection;
        }

        private static bool TryGetNativeEnrage(CombatSnapshot snapshot,
            int npcKey, out QueenBeeNativeEnrageObservation observation)
        {
            observation = default(QueenBeeNativeEnrageObservation);
            if (snapshot?.PriorityBoss == null) return false;
            var values = snapshot.PriorityBoss.QueenBees;
            for (var i = 0; i < values.Count; i++)
            {
                var candidate = values[i];
                if (candidate.NpcKey != npcKey || !candidate.Known) continue;
                string ignored;
                if (!PriorityBossNativeContextContract.TryValidate(
                        in candidate, out ignored)) return false;
                observation = candidate;
                return true;
            }
            return false;
        }

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase)
        {
            var result = Decision(snapshot, target, phase,
                BossPattern.HorizontalKite, 520, -100, 0, 0, false,
                false, false, 80, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Queen Bee exposed an unreviewed native AI state";
            return result;
        }

        private BossDecision Departing(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            var result = Decision(snapshot, target, "native-departure",
                BossPattern.HorizontalKite, 520, -100, 0, 0, false,
                false, false, 80, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Queen Bee entered its native departure state";
            return result;
        }
    }

    internal sealed class SkeletronStrategy : BossStrategyBase
    {
        public SkeletronStrategy() : base("skeletron", 900, 430, 4.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 35);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = Pick(s, 35);
            var handCount = 0;
            var handAttack = default(HandAttack);
            if (s.NativeContextKnown && (!TryObserveHands(s, head,
                    out handCount, out handAttack) || !head.Ai1Known ||
                    !head.Ai2Known || !head.Ai3Known))
                return UnknownNativeState(s, head,
                    "missing-native-head-or-hand-clock");
            if (!s.NativeContextKnown)
            {
                if (!TryObserveHands(s, head, out handCount,
                        out handAttack))
                    return UnknownNativeState(s, head,
                        "invalid-hand-state");
            }
            var state = NativeIntegerState(head.Ai1, 0, 3);
            var clock = NativeNonnegativeInteger(head.Ai2);
            var redHat = NativeIntegerState(head.Ai3, 0, 1);
            if (state == int.MinValue || clock == int.MinValue ||
                redHat == int.MinValue || state == 0 && clock > 800 ||
                state == 1 && clock > 400)
                return UnknownNativeState(s, head,
                    "invalid-native-state-or-clock");
            // RedHat has different cadence, reflection, and movement constants.
            // Never pass it through the ordinary Skeletron proof.
            if (redHat == 1)
                return UnknownNativeState(s, head,
                    "red-hat-variant-unreviewed");
            if (state == 2)
                return UnknownNativeState(s, head,
                    "daytime-guardian-enrage");
            if (state == 3)
                return Departing(s, head);

            var target = BalancedSkeletronTarget(s, head, out handCount);
            string phase;
            BossPattern pattern;
            var horizontal = 0;
            var vertical = 0;
            var distance = 440f;
            var margin = 48f;
            var dash = false;
            var ownsMovement = false;
            var runway = StableRunDirection(s, head, m);
            // A hand which has already committed to a dive keeps executing
            // across a head-state transition. Its observed attack line is the
            // immediate movement contract even while the head is spinning.
            if (handAttack.Known &&
                (handAttack.State == 2 || handAttack.State == 5))
            {
                phase = handAttack.State == 2 ?
                    "hand-vertical-dive-committed" :
                    "hand-horizontal-dive-committed";
                pattern = BossPattern.PerpendicularDashDodge;
                ChargeEscape(s, handAttack.Target,
                    handAttack.Target.Velocity, out horizontal,
                    out vertical);
                distance = 560f;
                margin = 84f;
                ownsMovement = true;
            }
            else if (state == 1)
            {
                phase = clock < 40 ? "head-spin-entry" :
                    clock >= 360 ? "head-spin-exit-window" :
                    "head-spin-pursuit";
                pattern = BossPattern.Runway;
                horizontal = runway;
                distance = 590f;
                margin = 86f;
                dash = Vec2.DistanceSquared(s.Player.Center, head.Center) <
                    280f * 280f;
            }
            else
            {
                var untilSpin = Math.Max(0, 800 - clock);
                if (handAttack.Known)
                {
                    phase = handAttack.State == 1 ?
                        "hand-vertical-dive-locking" :
                        handAttack.State == 4 ?
                        "hand-horizontal-dive-locking" :
                        handAttack.State == 0 ?
                        "hand-vertical-dive-imminent" :
                        "hand-horizontal-dive-imminent";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = runway;
                    vertical = handAttack.State == 4 ||
                        handAttack.State == 3 ? 1 : 0;
                    margin = 70f;
                }
                else if (untilSpin <= 90)
                {
                    phase = untilSpin <= 20 ? "hover-spin-imminent" :
                        "hover-pre-spin";
                    pattern = BossPattern.Runway;
                    horizontal = runway;
                    distance = 520f;
                    margin = 64f;
                }
                else
                {
                    var skullPeriod = handCount == 0 ? 40 : 80;
                    if (s.Difficulty.ForTheWorthy)
                        skullPeriod = Math.Max(1,
                            (int)Math.Floor(skullPeriod * .8f));
                    var skullEnabled = (s.Difficulty.Expert ||
                        s.Difficulty.Master) &&
                        (handCount < 2 || Life(head) < .75f);
                    var untilSkull = skullEnabled ?
                        (skullPeriod - clock % skullPeriod) % skullPeriod :
                        int.MaxValue;
                    phase = skullEnabled && untilSkull <= 18 ?
                        "hover-skull-telegraph" :
                        handCount > 0 ? "balance-hands-hover" :
                        "head-hover";
                    pattern = skullEnabled ? BossPattern.ProjectileLanes :
                        BossPattern.Runway;
                    horizontal = runway;
                    vertical = skullEnabled && untilSkull <= 12 ? 1 : 0;
                }
            }
            var decision = Decision(s, target, phase, pattern, distance, -90,
                horizontal, vertical, dash, true, false, margin,
                patternTarget: head);
            // The head clock and both independent hand states are a finite
            // native schedule.  Keep the selected runway direction through a
            // spin/lock instead of allowing generic target-relative hysteresis
            // to reverse it. A committed hand vector additionally owns the
            // perpendicular posture for that short crossing window.
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.OwnsHorizontalClosure = true;
            decision.Directive.OwnsMovementClosure = ownsMovement;
            return decision;
        }

        private static bool TryObserveHands(CombatSnapshot snapshot,
            TargetSnapshot head, out int handCount, out HandAttack urgent)
        {
            handCount = 0;
            urgent = default(HandAttack);
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var hand = snapshot.Targets[i];
                if (hand.Type != 36 || hand.Life <= 0) continue;
                handCount++;
                if (!hand.Ai0Known || !hand.Ai1Known || !hand.Ai2Known ||
                    !hand.Ai3Known) return false;
                var side = NativeIntegerState(hand.Ai0, -1, 1);
                var parent = NativeNonnegativeInteger(hand.Ai1);
                var state = NativeIntegerState(hand.Ai2, 0, 5);
                if ((side != -1 && side != 1) || parent != head.Key ||
                    state == int.MinValue || float.IsNaN(hand.Ai3) ||
                    float.IsInfinity(hand.Ai3) || hand.Ai3 < 0f ||
                    hand.Ai3 >= 300f ||
                    state != 0 && state != 3 && Math.Abs(hand.Ai3) > .001f)
                    return false;

                var increment = snapshot.Difficulty.Expert ||
                    snapshot.Difficulty.Master ? 1.5f : 1f;
                var until = state == 0 || state == 3 ?
                    Math.Max(0, (int)Math.Ceiling(
                        (300f - hand.Ai3) / increment)) : 0;
                var priority = state == 2 ? 60 : state == 5 ? 55 :
                    state == 1 ? 45 : state == 4 ? 40 :
                    until <= 36 ? (state == 0 ? 25 : 22) : 0;
                if (priority == 0 || urgent.Known &&
                    (priority < urgent.Priority || priority == urgent.Priority &&
                     until >= urgent.Until)) continue;
                urgent = new HandAttack
                {
                    Known = true,
                    Target = hand,
                    State = state,
                    Until = until,
                    Priority = priority
                };
            }
            return true;
        }

        private struct HandAttack
        {
            public bool Known;
            public TargetSnapshot Target;
            public int State;
            public int Until;
            public int Priority;
        }

        private static TargetSnapshot BalancedSkeletronTarget(
            CombatSnapshot snapshot, TargetSnapshot head, out int handCount)
        {
            handCount = 0;
            var selected = default(TargetSnapshot);
            var found = false;
            var highestRatio = -1f;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var hand = snapshot.Targets[i];
                if (hand.Type != 36 || hand.Life <= 0) continue;
                handCount++;
                if (hand.Invulnerable) continue;
                var ratio = Life(hand);
                if (!found || ratio > highestRatio)
                {
                    selected = hand;
                    highestRatio = ratio;
                    found = true;
                }
            }
            return found ? selected : head;
        }

        private static int StableRunDirection(CombatSnapshot snapshot,
            TargetSnapshot head, BossMemory memory)
        {
            if (memory.PreviousTargetKey != head.Key ||
                memory.OrbitDirection == 0)
                memory.OrbitDirection = snapshot.Arena.ClearanceRight >=
                    snapshot.Arena.ClearanceLeft ? 1 : -1;
            var direction = memory.OrbitDirection;
            var remaining = direction > 0 ? snapshot.Arena.ClearanceRight :
                snapshot.Arena.ClearanceLeft;
            var opposite = direction > 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = speed * speed /
                (2f * Math.Max(.01f, snapshot.Player.RunSlowdown));
            var reserve = Math.Max(180f, braking + 112f);
            if (remaining < reserve && opposite > remaining + 180f)
                memory.OrbitDirection = -direction;
            return memory.OrbitDirection;
        }

        private BossDecision Departing(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            var result = Decision(snapshot, target, "native-departure",
                BossPattern.Runway, 600, -90, 0, 0, false, false, false,
                90, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Skeletron entered its native departure state";
            return result;
        }

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase)
        {
            var result = Decision(snapshot, target, phase,
                BossPattern.Runway, 600, -90, 0, 0, false, false, false,
                120, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Skeletron exposed an unreviewed native AI state";
            return result;
        }
    }

    internal sealed class DeerclopsStrategy : BossStrategyBase
    {
        private int _contactBossKey = -1;
        private bool _contactRetreat;

        public DeerclopsStrategy() : base("deerclops", 620, 220, 4f)
        {
            Requirements.SupportsModeledSlowDebuff = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 668);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 668);
            DeerclopsNativeTimerObservation nativeTimer;
            var hasNativeTimer = TryGetDeerclopsTimer(s, t.Key,
                out nativeTimer);
            if (s.NativeContextKnown && (!hasNativeTimer ||
                !t.Ai0Known || !t.Ai1Known ||
                nativeTimer.Ai0State != t.Ai0 ||
                nativeTimer.Ai1StateTimer != t.Ai1))
                return UnknownNativeState(s, t,
                    "missing-or-inconsistent-native-timers");
            var state = NativeIntegerState(hasNativeTimer ?
                nativeTimer.Ai0State : t.Ai0, -1, 8);
            var tick = NativeNonnegativeInteger(hasNativeTimer ?
                nativeTimer.Ai1StateTimer : t.Ai1);
            if (state == int.MinValue || tick == int.MinValue ||
                !ValidStateTimer(state, tick))
                return UnknownNativeState(s, t, "invalid-native-clock");
            string phase;
            BossPattern pattern = BossPattern.StayCloseJump;
            var horizontal = 0;
            var vertical = 0;
            var dash = false;
            var margin = 52f;
            var ownsHorizontal = false;
            var ownsMovement = false;
            var jumpAction = JumpAction.Release;
            var nativeDirection = hasNativeTimer &&
                nativeTimer.NativeDirectionKnown &&
                (nativeTimer.NativeDirection == -1 ||
                 nativeTimer.NativeDirection == 1) ?
                nativeTimer.NativeDirection : t.NativeDirectionKnown &&
                (t.NativeDirection == -1 || t.NativeDirection == 1) ?
                t.NativeDirection : 0;
            if ((state == 1 || state == 2) && nativeDirection == 0)
                return UnknownNativeState(s, t,
                    "directional-attack-missing-native-facing");
            var runway = StableRunDirection(s, t, m);
            var horizontalGap = Math.Abs(s.Player.Center.X - t.Center.X);
            var requiresFarReacquire = horizontalGap > 340f;
            switch (state)
            {
                case -1:
                    phase = "native-spawn-settle";
                    horizontal = 0;
                    break;
                case 0:
                    phase = "close-control";
                    horizontal = horizontalGap > 340f ?
                        -AwayX(s.Player, t) : runway;
                    ownsHorizontal = requiresFarReacquire;
                    if ((s.Difficulty.Expert || s.Difficulty.Master) &&
                        hasNativeTimer)
                    {
                        var handPeriod = Math.Max(1, (int)(40f +
                            40f * Life(t)));
                        var handClock = NativeNonnegativeInteger(
                            nativeTimer.LocalAi2ShadowHandTimer);
                        if (handClock != int.MinValue)
                        {
                            // AI_123 resets this counter only after every third
                            // spawn. At zero (and at each exact multiple) a hand
                            // has just spawned or is a full period away; modulo
                            // zero is therefore not an imminent future spawn.
                            var untilHand = handPeriod -
                                handClock % handPeriod;
                            if (untilHand <= 12)
                            {
                                phase = "passive-shadow-hand-imminent";
                                pattern = BossPattern.ProjectileLanes;
                                if (!requiresFarReacquire)
                                    horizontal = runway;
                                margin = 72f;
                                ownsHorizontal = true;
                            }
                        }
                    }
                    break;
                case 1:
                    phase = tick < 36 ? "forward-spikes-telegraph" :
                        "forward-spikes-wave";
                    // All twenty spikes remain ground-bound.  The exact facing
                    // side is native Entity.direction. A Slow player cannot
                    // outrun the 4-spikes/4-ticks front or cross the Boss after
                    // the lock; keep the attacked-side runway and execute the
                    // base/cloud jump cadence before the first relevant spike.
                    // The native wave advances by four tiles every four ticks.
                    // Running with it places the player under progressively taller
                    // 200 px spike lines. Hold the short, close-to-Deerclops lane
                    // and spend the Cloud jump only on the late half of the wave.
                    horizontal = horizontalGap > 220f ?
                        -AwayX(s.Player, t) : 0;
                    jumpAction = TimedSpikeJumpAction(s.Player, tick, 20, 48, 78);
                    vertical = RequestsJump(jumpAction) ? 1 : 0;
                    margin = 68f;
                    ownsMovement = true;
                    break;
                case 2:
                    phase = tick < 32 ? "rubble-telegraph" :
                        "rubble-arc";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = nativeDirection;
                    jumpAction = TimedJumpAction(s.Player, tick, 22, 58);
                    vertical = RequestsJump(jumpAction) ? 1 : 0;
                    margin = 66f;
                    ownsMovement = true;
                    break;
                case 3:
                    phase = tick < 28 ? "slow-roar-telegraph" :
                        "slow-roar-recovery-window";
                    // Deerclops is halted for all sixty frames. Bank every
                    // available pixel of separation while Slow is applied so
                    // the next close-range spike choice does not begin with a
                    // needless direction reversal.
                    horizontal = requiresFarReacquire ?
                        -AwayX(s.Player, t) : runway;
                    ownsHorizontal = true;
                    break;
                case 4:
                    phase = tick < 56 ? "double-spikes-telegraph" :
                        "double-spikes-wave";
                    // Both-sided spikes grow with their tile offset too. Staying
                    // in the short inner columns is safer than racing either arm.
                    horizontal = horizontalGap > 220f ?
                        -AwayX(s.Player, t) : 0;
                    jumpAction = TimedSpikeJumpAction(s.Player, tick, 36, 66, 88);
                    vertical = RequestsJump(jumpAction) ? 1 : 0;
                    margin = 76f;
                    ownsMovement = true;
                    break;
                case 5:
                    phase = tick < 30 ? "shadow-hands-telegraph" :
                        "shadow-hands-active";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = runway;
                    dash = tick >= 26 && tick <= 48;
                    margin = 78f;
                    ownsHorizontal = true;
                    break;
                case 6:
                    phase = "return-home";
                    horizontal = horizontalGap > 340f ?
                        -AwayX(s.Player, t) : runway;
                    ownsHorizontal = requiresFarReacquire;
                    break;
                case 7:
                    phase = tick < 40 ? "teleport-home-telegraph" :
                        "teleport-home-reappear";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = horizontalGap > 340f ?
                        -AwayX(s.Player, t) : runway;
                    ownsHorizontal = requiresFarReacquire;
                    margin = 74f;
                    break;
                case 8:
                    return Departing(s, t);
                default:
                    return UnknownNativeState(s, t,
                        "unreviewed-native-state-" + state);
            }
            // Projectile observations refine, but never replace, the native
            // pre-spawn clock used above.
            var shadowHandObserved = HasThreat(s, 965);
            int shadowHandEscape;
            var shadowHandImminent = TryShadowHandEscape(s,
                out shadowHandEscape);
            var spikeObserved = HasThreat(s, 961);
            var nearbySpikeObserved = HasNearbyGroundSpike(s);
            if (shadowHandObserved)
            {
                phase = "observed-shadow-hand";
                pattern = BossPattern.ProjectileLanes;
                dash = true;
                if (shadowHandImminent)
                {
                    horizontal = shadowHandEscape;
                    ownsHorizontal = true;
                }
            }
            if (spikeObserved)
            {
                var spikePhase = state == 4 ? "double-spikes-observed" :
                    "forward-spikes-observed";
                phase = shadowHandObserved ?
                    "observed-shadow-hand-and-" + spikePhase : spikePhase;
                // Spikes outlive Deerclops' native attack state. Keep the same
                // ground/cloud jump closure until the final observed spike is
                // gone instead of landing merely because ai[0] advanced.
                if (state != 1 && state != 4 && nearbySpikeObserved)
                {
                    jumpAction = GroundThreatJumpAction(s.Player);
                    vertical = RequestsJump(jumpAction) ? 1 : 0;
                    ownsMovement = true;
                }
                margin = Math.Max(margin, 72f);
            }
            else if (!shadowHandObserved && HasThreat(s, 962))
                phase = "rubble-observed";
            horizontal = ContactSafeHorizontal(s.Player, t, horizontalGap,
                horizontal, ref ownsHorizontal);
            var target = SelectAmbientPressureTarget(s, t);
            var visible = target.LineOfSightKnown ? target.HasLineOfSight :
                target.Key == t.Key && s.LineOfSightToPrimary;
            var inRange = s.Weapon == null ||
                !s.Weapon.NativeProfileRequired ||
                !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, s.Player.Center) <=
                    s.Weapon.Profile.ConservativeRangePixels *
                    s.Weapon.Profile.ConservativeRangePixels;
            var fire = target.Life > 0 && !target.Invulnerable &&
                target.Chaseable && visible && inRange;
            var decision = Decision(s, target, phase, pattern, 210, -95,
                horizontal, vertical, dash, true, false, margin, fire, t);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.OwnsHorizontalClosure = ownsHorizontal ||
                ownsMovement;
            decision.Directive.OwnsMovementClosure = ownsMovement;
            decision.Directive.JumpAction = jumpAction;
            return decision;
        }

        private static bool ValidStateTimer(int state, int tick)
        {
            switch (state)
            {
                case 1: return tick <= 80;
                case 2:
                case 3:
                case 5:
                case 7: return tick <= 60;
                case 4: return tick <= 90;
                case 8: return tick < 40;
                default: return true;
            }
        }

        private static bool RequestsJump(JumpAction action) =>
            action == JumpAction.Hold || action == JumpAction.Cloud;

        private static JumpAction TimedJumpAction(PlayerSnapshot player,
            int tick, int firstTick, int lastTick)
        {
            if (tick < firstTick || tick > lastTick)
                return JumpAction.Release;
            var jump = player.Jump;
            if (!jump.Known) return JumpAction.Hold;
            if (player.OnGround || jump.RemainingTicks > 0)
                return JumpAction.Hold;
            // Vanilla needs one actual key-up update before a Cloud jump can
            // consume its observed charge. Do not hold through the apex and
            // silently lose the only second-jump opportunity.
            if (jump.CloudAvailable)
                return jump.ReleaseReady ? JumpAction.Cloud :
                    JumpAction.Release;
            return JumpAction.Release;
        }

        private static JumpAction TimedSpikeJumpAction(PlayerSnapshot player,
            int tick, int firstTick, int cloudTick, int lastTick)
        {
            if (tick < firstTick || tick > lastTick)
                return JumpAction.Release;
            var jump = player.Jump;
            if (!jump.Known) return JumpAction.Hold;
            if (player.OnGround || jump.RemainingTicks > 0)
                return JumpAction.Hold;
            if (!jump.CloudAvailable) return JumpAction.Release;
            if (tick < cloudTick) return JumpAction.Release;
            return jump.ReleaseReady ? JumpAction.Cloud : JumpAction.Release;
        }

        private static JumpAction GroundThreatJumpAction(
            PlayerSnapshot player)
        {
            var jump = player.Jump;
            if (!jump.Known) return JumpAction.Hold;
            if (player.OnGround || jump.RemainingTicks > 0)
                return JumpAction.Hold;
            if (jump.CloudAvailable)
                return jump.ReleaseReady ? JumpAction.Cloud :
                    JumpAction.Release;
            return JumpAction.Release;
        }

        private static bool HasNearbyGroundSpike(CombatSnapshot snapshot)
        {
            var playerCenterX = snapshot.Player.Center.X;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.Projectile ||
                    threat.Type != 961 || threat.TimeLeft <= 0) continue;
                var threatCenterX = threat.Position.X + threat.Width * .5f;
                if (Math.Abs(threatCenterX - playerCenterX) <= 56f)
                    return true;
            }
            return false;
        }

        private static bool TryShadowHandEscape(CombatSnapshot snapshot,
            out int direction)
        {
            direction = 0;
            var player = snapshot.Player;
            var best = float.MaxValue;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.Projectile ||
                    threat.Type != 965 || threat.TimeLeft <= 0) continue;
                var center = new Vec2(threat.Position.X + threat.Width * .5f,
                    threat.Position.Y + threat.Height * .5f);
                var delta = center - player.Center;
                var distanceSquared = delta.X * delta.X + delta.Y * delta.Y;
                var relativeVelocity = threat.Velocity - player.Velocity;
                var approaching = delta.X * relativeVelocity.X +
                    delta.Y * relativeVelocity.Y < 0f;
                if (distanceSquared > 240f * 240f ||
                    !approaching && distanceSquared > 120f * 120f ||
                    distanceSquared >= best) continue;
                best = distanceSquared;
                direction = Math.Abs(delta.X) > 12f ?
                    (delta.X > 0f ? -1 : 1) :
                    relativeVelocity.X > 0f ? 1 : -1;
            }
            return direction != 0;
        }

        private int ContactSafeHorizontal(PlayerSnapshot player,
            TargetSnapshot deerclops, float horizontalGap, int proposed,
            ref bool ownsHorizontal)
        {
            if (_contactBossKey != deerclops.Key)
            {
                _contactBossKey = deerclops.Key;
                _contactRetreat = false;
            }
            if (horizontalGap < 190f) _contactRetreat = true;
            else if (horizontalGap > 260f) _contactRetreat = false;
            if (!_contactRetreat) return proposed;
            ownsHorizontal = true;
            return AwayX(player, deerclops);
        }

        private static TargetSnapshot SelectAmbientPressureTarget(
            CombatSnapshot snapshot, TargetSnapshot deerclops)
        {
            var selected = deerclops;
            var closest = 240f * 240f;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Boss || candidate.Damage <= 0 ||
                    candidate.Life <= 0 || candidate.Life > 500 ||
                    candidate.Invulnerable || !candidate.Chaseable ||
                    !candidate.LineOfSightKnown ||
                    !candidate.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(snapshot.Player.Center,
                    candidate.Center);
                if (distance >= closest) continue;
                closest = distance;
                selected = candidate;
            }
            return selected;
        }

        private static bool TryGetDeerclopsTimer(CombatSnapshot snapshot,
            int npcKey, out DeerclopsNativeTimerObservation timer)
        {
            timer = default(DeerclopsNativeTimerObservation);
            if (snapshot?.PriorityBoss == null) return false;
            var values = snapshot.PriorityBoss.Deerclopses;
            for (var i = 0; i < values.Count; i++)
            {
                var candidate = values[i];
                if (candidate.NpcKey != npcKey || !candidate.Known) continue;
                string ignored;
                if (!PriorityBossNativeContextContract.TryValidate(
                        in candidate, out ignored)) continue;
                timer = candidate;
                return true;
            }
            return false;
        }

        private static int StableRunDirection(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory)
        {
            if (memory.PreviousTargetKey != target.Key ||
                memory.OrbitDirection == 0)
            {
                var away = AwayX(snapshot.Player, target);
                var awayRoom = away > 0 ? snapshot.Arena.ClearanceRight :
                    snapshot.Arena.ClearanceLeft;
                var oppositeRoom = away > 0 ? snapshot.Arena.ClearanceLeft :
                    snapshot.Arena.ClearanceRight;
                memory.OrbitDirection = awayRoom + 96f >= oppositeRoom ?
                    away : -away;
            }
            var direction = memory.OrbitDirection;
            var remaining = LaneRemaining(snapshot, direction);
            var opposite = LaneRemaining(snapshot, -direction);
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = speed * speed /
                (2f * Math.Max(.01f, snapshot.Player.RunSlowdown));
            var reserve = Math.Max(120f, braking + 80f);
            if (remaining < reserve && opposite > remaining + 160f)
                memory.OrbitDirection = -direction;
            return memory.OrbitDirection;
        }

        private static float LaneRemaining(CombatSnapshot snapshot,
            int direction)
        {
            var player = snapshot.Player;
            var remaining = direction > 0 ? snapshot.Arena.ClearanceRight :
                snapshot.Arena.ClearanceLeft;
            var support = snapshot.Arena.FloorSupport;
            if (!support.Valid || support.Inverted ||
                !support.OverlapsBody(player.Position.X, player.Width))
                return remaining;
            var supportRemaining = direction > 0 ?
                support.Right - player.Position.X - player.Width :
                player.Position.X - support.Left;
            return Math.Min(remaining, Math.Max(0f, supportRemaining));
        }

        private BossDecision Departing(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            var result = Decision(snapshot, target, "native-disappear",
                BossPattern.StayCloseJump, 210, -95, 0, 0, false, false,
                false, 90, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Deerclops entered its native disappear state";
            return result;
        }

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase)
        {
            var result = Decision(snapshot, target, phase,
                BossPattern.StayCloseJump, 210, -95, 0, 0, false, false,
                false, 100, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Deerclops exposed an unreviewed native AI state";
            return result;
        }
    }

    internal sealed class WallStrategy : BossStrategyBase
    {
        public WallStrategy() : base("wall-of-flesh", 1800, 170, 3.25f)
        {
            Requirements.RequiresWallOfFleshSpeedContract = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 113, 114);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var mouth = Pick(s, 113);
            var target = BestWallTarget(s, mouth);
            string nativeFailure = null;
            if (!s.NativeContextKnown ||
                !ValidateMouthNativeState(mouth, out nativeFailure))
                return UnknownNativeState(s, mouth,
                    nativeFailure ?? "missing-native-mouth-state");
            var ratio = Life(mouth);
            var direction = AwayX(s.Player, mouth);
            var verticalOffset = 0f;
            WallOfFleshTunnelObservation tunnel;
            if (!TryGetTunnel(s, mouth.Key, out tunnel))
                return UnknownNativeState(s, mouth,
                    "missing-or-invalid-native-tunnel-bounds");
            direction = tunnel.NativeDirection;
            var tunnelCenter = (tunnel.DrawAreaTopPixels +
                tunnel.DrawAreaBottomPixels) * .5f;
            verticalOffset = tunnelCenter - mouth.Center.Y;
            var imminentLaser = false;
            var laserTicks = int.MaxValue;
            // localAI[1]/[2] are per-eye laser clocks.  Zero is meaningful;
            // every live eye must have one matching, independently validated
            // production observation. Unknown LOS is not silently treated as
            // a clear or blocked lane.
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var eye = s.Targets[i];
                if (eye.Type != 114 || eye.Life <= 0) continue;
                WallOfFleshEyeLaserObservation nativeEye;
                if (!TryGetEye(s, eye, mouth, out nativeEye))
                    return UnknownNativeState(s, mouth,
                        "missing-inconsistent-or-invalid-native-eye-laser-state");
                var burst = NativeNonnegativeInteger(
                    nativeEye.LocalAi2BurstStage);
                var rate = WallEyeLaserTimerRate(mouth, s.Difficulty);
                // localAI[2]==0 does not shoot at timer 600. It only opens
                // a burst and resets localAI[1], after which another >45
                // timer interval is required. During a burst, blocked LOS may
                // legally leave the timer above 45; the next visible tick fires.
                var remaining = burst == 0 ?
                    TicksUntilExceeded(nativeEye.LocalAi1ChargeTimer,
                        600f, rate) +
                        TicksUntilExceeded(0f, 45f, rate) :
                    TicksUntilExceeded(nativeEye.LocalAi1ChargeTimer,
                        45f, rate);
                if (remaining < laserTicks) laserTicks = remaining;
            }
            imminentLaser = laserTicks <= 24;
            var speedPhase = NativeSpeedPhase(mouth, s.Difficulty);
            var mouthBurst = NativeNonnegativeInteger(mouth.Ai2);
            var leechTicks = mouthBurst > 0 ? TicksUntilExceeded(mouth.Ai1,
                60f, 1f) : int.MaxValue;
            string phase;
            if (imminentLaser)
                phase = speedPhase + "-eye-laser-imminent-" +
                    Math.Max(0, laserTicks);
            else if (laserTicks <= 90)
                phase = speedPhase + "-eye-laser-charging-" +
                    Math.Max(0, laserTicks);
            else if (mouthBurst > 0 && leechTicks <= 16)
                phase = speedPhase + "-leech-burst-imminent-" +
                    leechTicks;
            else if (mouthBurst > 0)
                phase = speedPhase + "-leech-burst-" + mouthBurst;
            else
                phase = speedPhase;
            var distance = ratio < .1f ? 780f : ratio < .25f ? 700f : 580f;
            var nativeSpeed = NativeHorizontalSpeed(mouth.Life,
                mouth.LifeMax, s.Difficulty);
            var forwardPlayerSpeed = direction * s.Player.Velocity.X;
            if (float.IsNaN(forwardPlayerSpeed) ||
                float.IsInfinity(forwardPlayerSpeed))
                return UnknownNativeState(s, mouth,
                    "invalid-player-speed-during-native-wall-recovery");
            var forwardSeparation = direction * (s.Player.Center.X -
                mouth.Center.X);
            var contactSeparation = (Math.Max(1, s.Player.Width) +
                Math.Max(1, mouth.Width)) * .5f;
            if (float.IsNaN(forwardSeparation) ||
                float.IsInfinity(forwardSeparation))
                return UnknownNativeState(s, mouth,
                    "invalid-player-position-during-native-wall-recovery");
            // Once the mouth has reached or passed the player, a generic
            // runner cannot cross back through its hitbox safely. Do not take
            // a mid-fight F8 session merely because velocity happens to match
            // the Wall's native speed; give input back before any control is
            // latched. A player who is still genuinely ahead may recover the
            // wider braking margin below.
            if (forwardSeparation <= contactSeparation)
                return UnsafeTrailingRunway(s, mouth);
            var recoveryForwardSeparation = RecoveryForwardSeparation(
                s.Player, mouth, direction, nativeSpeed);
            if (float.IsNaN(recoveryForwardSeparation) ||
                float.IsInfinity(recoveryForwardSeparation) ||
                recoveryForwardSeparation < contactSeparation)
                return UnknownNativeState(s, mouth,
                    "invalid-wall-forward-recovery-margin");
            if (forwardPlayerSpeed < nativeSpeed)
            {
                phase += "-acquire-native-speed";
            }
            var vertical = imminentLaser ?
                PerpendicularY(s.Player, target) : 0;
            var result = Decision(s, target, phase, BossPattern.Runway, distance,
                verticalOffset, direction, vertical,
                ratio < .25f || imminentLaser,
                false, false, imminentLaser ? 92 : ratio < .25f ? 78 : 52,
                patternTarget: mouth);
            // Wall of Flesh owns a translating runway, not a ring around its
            // mouth.  Once the player matches its current native speed, is in
            // the observed tunnel lane, and retains braking room, increasing
            // separation is valid convergence rather than a recovery failure.
            // This is a source-owned direction: the ordinary generic runway
            // heuristic is allowed to coast/reverse a despawnable pursuer, but
            // must never do that to the translating Wall.
            result.Directive.OwnsHorizontalClosure = true;
            result.Directive.RecoveryMinimumHorizontalSpeed = nativeSpeed;
            result.Directive.RecoveryIgnoreHorizontalGeometry = true;
            result.Directive.RecoveryMinimumForwardSeparation =
                recoveryForwardSeparation;
            return result;
        }

        private static float RecoveryForwardSeparation(PlayerSnapshot player,
            TargetSnapshot mouth, int direction, float nativeSpeed)
        {
            var contact = (Math.Max(1, player.Width) +
                Math.Max(1, mouth.Width)) * .5f;
            var forwardSpeed = direction * player.Velocity.X;
            var overspeed = Math.Max(0f, forwardSpeed - nativeSpeed);
            var braking = player.RunSlowdown > .0001f ?
                player.RunSlowdown : Math.Max(.08f, player.RunAcceleration);
            if (float.IsNaN(overspeed) || float.IsInfinity(overspeed) ||
                float.IsNaN(braking) || float.IsInfinity(braking) ||
                braking <= 0f)
                return float.NaN;
            // This is only the distance needed to settle excess speed relative
            // to the Wall, plus one conservative reaction/collision buffer;
            // it intentionally does not demand a full-player stop distance
            // when player and Wall are already travelling together.
            var settleDistance = overspeed * overspeed / (2f * braking);
            return contact + Math.Max(48f, settleDistance + 32f);
        }

        private BossDecision UnsafeTrailingRunway(CombatSnapshot snapshot,
            TargetSnapshot mouth)
        {
            var result = Decision(snapshot, mouth,
                "wall-already-passed-player", BossPattern.Runway, 780f, 0f,
                0, 0, false, false, false, 120f, false, mouth);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Wall of Flesh is already at or ahead of the player; no safe runway re-entry exists";
            return result;
        }

        private static bool TryGetTunnel(CombatSnapshot snapshot, int npcKey,
            out WallOfFleshTunnelObservation tunnel)
        {
            tunnel = default(WallOfFleshTunnelObservation);
            if (snapshot?.PriorityBoss == null ||
                !snapshot.PriorityBoss.WallOfFleshDrawAreaKnown) return false;
            var values = snapshot.PriorityBoss.WallOfFleshTunnels;
            var found = false;
            for (var i = 0; i < values.Count; i++)
            {
                var candidate = values[i];
                if (candidate.NpcKey != npcKey) continue;
                if (found) return false;
                string ignored;
                if (!PriorityBossNativeContextContract.TryValidate(
                        in candidate, out ignored) ||
                    candidate.NativeDirection == 0) return false;
                tunnel = candidate;
                found = true;
            }
            return found;
        }

        private static bool TryGetEye(CombatSnapshot snapshot,
            TargetSnapshot target, TargetSnapshot mouth,
            out WallOfFleshEyeLaserObservation eye)
        {
            eye = default(WallOfFleshEyeLaserObservation);
            if (snapshot?.PriorityBoss == null || !target.Ai0Known ||
                !target.LocalAi1Known || !target.LocalAi2Known ||
                !target.LineOfSightKnown) return false;
            var values = snapshot.PriorityBoss.WallOfFleshEyes;
            var found = false;
            for (var i = 0; i < values.Count; i++)
            {
                var candidate = values[i];
                if (candidate.NpcKey != target.Key) continue;
                if (found) return false;
                string ignored;
                if (!PriorityBossNativeContextContract.TryValidate(
                        in candidate, out ignored)) return false;
                if (!candidate.LineOfSightKnown ||
                    candidate.Ai0EyeSide != target.Ai0 ||
                    candidate.LocalAi1ChargeTimer != target.LocalAi1 ||
                    candidate.LocalAi2BurstStage != target.LocalAi2 ||
                    candidate.LineOfSight != target.HasLineOfSight ||
                    !ValidEyeTimer(candidate.LocalAi1ChargeTimer,
                        snapshot.Difficulty)) return false;
                var burst = NativeNonnegativeInteger(
                    candidate.LocalAi2BurstStage);
                if (burst < 0 || burst >= WallEyeBurstCount(mouth,
                        snapshot.Difficulty) || burst == 0 &&
                    candidate.LocalAi1ChargeTimer > 600f) return false;
                eye = candidate;
                found = true;
            }
            return found;
        }

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase)
        {
            var result = Decision(snapshot, target, phase,
                BossPattern.Runway, 780, 0, 0, 0, false, false, false,
                120, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Wall of Flesh native mouth, tunnel, or eye state is unavailable or malformed";
            return result;
        }

        internal static float MaximumNativeHorizontalSpeed(
            DifficultySnapshot difficulty) => NativeHorizontalSpeed(0, 1,
                difficulty);

        internal static float NativeHorizontalSpeed(int life, int lifeMax,
            DifficultySnapshot difficulty)
        {
            var speed = 1.5f;
            if (Below(life, lifeMax, .75)) speed += .25f;
            if (Below(life, lifeMax, .5)) speed += .4f;
            if (Below(life, lifeMax, .25)) speed += .5f;
            if (Below(life, lifeMax, .1)) speed += .6f;
            if (difficulty.Expert || difficulty.Master)
            {
                if (Below(life, lifeMax, .66)) speed += .3f;
                if (Below(life, lifeMax, .33)) speed += .3f;
                if (Below(life, lifeMax, .05)) speed += .6f;
                if (Below(life, lifeMax, .035)) speed += .6f;
                if (Below(life, lifeMax, .025)) speed += .6f;
                speed *= 1.35f;
                speed += .35f;
            }
            // Preserve the native Single operation order exactly: getGoodWorld
            // scales the completed classic/expert value, then adds 0.2.
            if (difficulty.ForTheWorthy)
            {
                speed *= 1.1f;
                speed += .2f;
            }
            return speed;
        }

        private static string NativeSpeedPhase(TargetSnapshot mouth,
            DifficultySnapshot difficulty)
        {
            var expert = difficulty.Expert || difficulty.Master;
            if (expert && Below(mouth, .025)) return "speed-under-025";
            if (expert && Below(mouth, .035)) return "speed-035-to-025";
            if (expert && Below(mouth, .05)) return "speed-050-to-035";
            if (Below(mouth, .1)) return expert ?
                "speed-100-to-050" : "speed-under-100";
            if (Below(mouth, .25)) return "speed-250-to-100";
            if (expert && Below(mouth, .33)) return "speed-330-to-250";
            if (Below(mouth, .5)) return "speed-500-to-" +
                (expert ? "330" : "250");
            if (expert && Below(mouth, .66)) return "speed-660-to-500";
            if (Below(mouth, .75)) return "speed-750-to-" +
                (expert ? "660" : "500");
            return "speed-1000-to-750";
        }

        private static bool ValidateMouthNativeState(TargetSnapshot mouth,
            out string failure)
        {
            failure = null;
            if (mouth.Type != 113 || mouth.LifeMax <= 0 || mouth.Life <= 0 ||
                mouth.Life > mouth.LifeMax || !mouth.Ai1Known ||
                !mouth.Ai2Known)
            {
                failure = "missing-or-invalid-native-mouth-state";
                return false;
            }
            var clock = NativeNonnegativeInteger(mouth.Ai1);
            var burst = NativeNonnegativeInteger(mouth.Ai2);
            var maximumBurst = Below(mouth, .3) ? 4 : 3;
            // ai[2]=1 is assigned and consumed again inside the same AI_027
            // invocation; production snapshots taken at frame boundaries can
            // observe 0 or the emitted burst stages 2/3 (and low-life 4).
            if (clock == int.MinValue || burst < 0 || burst == 1 ||
                burst > maximumBurst ||
                burst == 0 && clock > 2700 || burst > 0 && clock > 60)
            {
                failure = "invalid-native-mouth-leech-clock";
                return false;
            }
            return true;
        }

        private static float WallEyeLaserTimerRate(TargetSnapshot mouth,
            DifficultySnapshot difficulty)
        {
            var rate = 1f;
            if (Below(mouth, .75)) rate += 1f;
            if (Below(mouth, .5)) rate += 1f;
            if (Below(mouth, .25)) rate += 1f;
            if (Below(mouth, .1)) rate += 2f;
            if (difficulty.Expert || difficulty.Master)
            {
                rate += .5f;
                if (Below(mouth, .1)) rate += 2f;
            }
            return rate;
        }

        private static int WallEyeBurstCount(TargetSnapshot mouth,
            DifficultySnapshot difficulty)
        {
            var count = 4;
            if (Below(mouth, .75)) count++;
            if (Below(mouth, .5)) count++;
            if (Below(mouth, .25)) count += 2;
            if (Below(mouth, .1)) count += 3;
            if (difficulty.Expert || difficulty.Master)
            {
                count++;
                if (Below(mouth, .1)) count += 3;
            }
            return count;
        }

        private static bool ValidEyeTimer(float timer,
            DifficultySnapshot difficulty)
        {
            if (float.IsNaN(timer) || float.IsInfinity(timer) || timer < 0f)
                return false;
            var lattice = (difficulty.Expert || difficulty.Master) ?
                timer * 2f : timer;
            return NativeNonnegativeInteger(lattice) != int.MinValue;
        }

        private static bool Below(TargetSnapshot target, double fraction) =>
            Below(target.Life, target.LifeMax, fraction);

        private static bool Below(int life, int lifeMax, double fraction) =>
            lifeMax > 0 && (double)life < (double)lifeMax * fraction;

        private static int TicksUntilExceeded(float current, float threshold,
            float rate)
        {
            if (current > threshold) return 1;
            return Math.Max(1, (int)Math.Floor(
                (threshold - current) / rate) + 1);
        }

        private static TargetSnapshot BestWallTarget(CombatSnapshot snapshot,
            TargetSnapshot mouth)
        {
            var selected = mouth;
            var found = !mouth.Invulnerable && mouth.Life > 0;
            var bestDistance = found ? Vec2.DistanceSquared(mouth.Center,
                snapshot.Player.Center) : float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Type != 114 || candidate.Life <= 0 ||
                    candidate.Invulnerable) continue;
                var distance = Vec2.DistanceSquared(candidate.Center,
                    snapshot.Player.Center);
                if (!found || candidate.HasLineOfSight &&
                        (!selected.HasLineOfSight || distance < bestDistance) ||
                    candidate.HasLineOfSight == selected.HasLineOfSight &&
                        distance < bestDistance)
                {
                    selected = candidate;
                    bestDistance = distance;
                    found = true;
                }
            }
            return selected;
        }
    }

    internal sealed class QueenSlimeStrategy : BossStrategyBase
    {
        // NPC.CheckActive in the hash-locked 1.4.5.8 build uses a fixed
        // 1920x1200 activity screen and activeTime=750. The native horizontal
        // refresh rectangle reaches sWidth/2 + npc.width from Queen.Center;
        // stay well inside that boundary instead of spending its hidden TTL.
        private const float NativeActivityHalfWidth = 960f;
        private const int ReturnGuardTicks = 24;
        private const float ActivityGuardTravel = 240f;
        private int _queenKey = -1;
        private int _runDirection;
        private int _escapeDirection;
        private float _previousAi0 = -1f;
        private bool _previousWave;
        private bool _spacingHold;
        private bool _spacingRecover;
        private int _spacingSide;

        // A low platform/runway template. Retain the existing air-capability
        // requirement until its actual native flight recovery is separately
        // verified; possessing wings is not evidence of a guaranteed victory.
        public QueenSlimeStrategy() : base("queen-slime", 900, 430, 5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 657);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var queen = Pick(s, 657);
            var p = s.Player;
            if (_queenKey != queen.Key || m.PreviousTargetKey < 0)
            {
                _queenKey = queen.Key;
                _runDirection = AwayX(p, queen);
                _escapeDirection = _runDirection;
                _previousAi0 = -1f;
                _previousWave = false;
                _spacingHold = _spacingRecover = false;
                _spacingSide = _runDirection;
            }
            // AI_121 0014..0027 uses INTEGER division and <=. Both ai0=4
            // (slam) and ai0=5 (gel) are also real first-form attacks.
            var second = queen.LifeMax > 0 && queen.Life <= queen.LifeMax / 2;
            var phase = NativePhase(queen, second, s.Difficulty.ForTheWorthy || s.Difficulty.Zenith);
            var slam = queen.Ai0 == 4f;
            var teleport = queen.Ai0 == 1f || queen.Ai0 == 2f;
            Vec2 waveCenter;
            var wave = TryNearbyWave(s, out waveCenter);
            var support = LowSupport(s);
            var ideal = second ? 420f : 360f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(200f, s.Weapon.Profile.ConservativeRangePixels * .65f));
            var horizontal = _runDirection;

            if (slam)
            {
                if (_previousAi0 != 4f)
                {
                    // Once dropping, future horizontal drift is bounded by
                    // sum(vx*.8^k,k>=1)=4*vx. Before the actual rise vector is
                    // observed do not invent a random/future landing position.
                    var landingX = queen.Center.X + (queen.Ai2 == 1f ? queen.Velocity.X * 4f : 0f);
                    _escapeDirection = EscapeSide(p, landingX, _runDirection);
                }
                // Start at the AI windup, not when the landing projectile first
                // becomes visible. A changing Boss side must not flip this exit.
                horizontal = _runDirection = _escapeDirection;
                _spacingRecover = false;
                if (CanCoastOutsideSlam(s, queen, ideal, _escapeDirection)) horizontal = 0;
            }
            else if (wave)
            {
                if (_previousAi0 != 4f && !_previousWave)
                    _escapeDirection = EscapeSide(p, waveCenter.X, _runDirection);
                horizontal = _runDirection = _escapeDirection;
                _spacingRecover = false;
                if (CanCoastOutsideDanger(p, waveCenter.X, waveCenter.X, ideal, _escapeDirection)) horizontal = 0;
                phase = second ? "second-smash-wave-horizontal-exit" : "first-smash-wave-horizontal-exit";
            }
            else if (queen.Ai0 == 2f)
            {
                if (_previousAi0 != 2f)
                    _escapeDirection = queen.LocalAiKnown ? EscapeSide(p, queen.LocalAi1, _runDirection) : _runDirection;
                horizontal = _runDirection = _escapeDirection;
                // Do not recover towards the old body while its destination is
                // unavailable or about to replace it. Reform re-observes the side.
                _spacingRecover = false;
            }
            else if (queen.Ai0 == 1f)
            {
                if (_previousAi0 != 1f) _runDirection = AwayX(p, queen);
                horizontal = Math.Abs(p.Center.X - queen.Center.X) < ideal ? _runDirection : 0;
            }
            else
            {
                var speed = Math.Abs(p.Velocity.X);
                var braking = speed * speed / (2f * Math.Max(.000001f, p.RunSlowdown));
                var lane = LaneRemaining(s, support, _runDirection);
                var reserve = Math.Max(320f, braking + speed * 48f);
                // Turn only while the upcoming 24 native ticks remain a known
                // idle/fly branch. Gel windup and slam are not generic recovery
                // windows, and old minions/projectiles can occupy the return.
                if (lane < reserve && LaneRemaining(s, support, -_runDirection) > lane + 160f &&
                    CanReturn(s, queen, second, support, -_runDirection))
                    horizontal = _runDirection = -_runDirection;
                else horizontal = _runDirection;

                // A run command throughout 3/4/5 accumulates separation even
                // while the Queen waits. Do not rely on an automatic teleport:
                // visible flat-ground pursuit can keep the teleport ai3 at zero.
                horizontal = SpacingMovement(s, queen, second, support, ideal, horizontal);
            }

            if (!p.OnGround && !slam && !wave && !teleport && support.Valid && phase != "unrecognized-native-state")
            {
                // Recover against a real platform, not Queen.Center.Y-120.
                // Zero vertical intent RELEASES jump without dropping through
                // one-way platforms. Exact flight/landing risk belongs to the
                // planner; this controller does not fabricate restored charge.
                var futureX = p.Position.X + p.Velocity.X * 8f;
                if (futureX < support.Left + 24f) horizontal = 1;
                else if (futureX + p.Width > support.Right - 24f) horizontal = -1;
                phase = second ? "second-low-air-return-to-platform" : "first-low-air-return-to-platform";
            }
            if (phase == "unrecognized-native-state") horizontal = 0;
            horizontal = KeepInsideLane(s, support, horizontal);
            var target = SelectPressureTarget(s, queen);
            var visible = target.LineOfSightKnown ? target.HasLineOfSight : target.Key == queen.Key && s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, p.Center) <= s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            var decision = Decision(s, target, phase, BossPattern.Runway, ideal, 0f,
                horizontal, 0, false, false, false, 36f, visible && inRange && target.Life > 0 && target.Chaseable, queen);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.JumpAction = JumpAction.Release;
            decision.Directive.IdealDistance = ideal;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            _previousAi0 = queen.Ai0;
            _previousWave = wave;
            return decision;
        }

        private int SpacingMovement(CombatSnapshot s, TargetSnapshot queen, bool second,
            SupportSpan floor, float ideal, int direction)
        {
            var p = s.Player;
            var side = AwayX(p, queen);
            var gap = Math.Abs(p.Center.X - queen.Center.X);
            var upper = Math.Max(480f, ideal + 180f);
            var lower = Math.Max(340f, ideal + 40f);
            // Player/Queen rectangles can overlap the native refresh rectangle
            // slightly beyond this center distance. Deliberately reserve 240px
            // for one guarded decision horizon and braking uncertainty.
            var activityLimit = NativeActivityHalfWidth + queen.Width + p.Width * .5f;
            var returnAt = Math.Min(upper + 120f, activityLimit - ActivityGuardTravel);
            returnAt = Math.Max(lower + 80f, returnAt);
            if (_spacingSide != side)
            {
                _spacingSide = side;
                _spacingHold = _spacingRecover = false;
            }
            // Recovery is a same-side approach, NOT permission to cross the
            // Boss. Brake before reaching the inner band, with the real drag.
            if (gap > returnAt) _spacingRecover = true;
            if (gap - StoppingTravel(p, -side) <= lower + 40f) _spacingRecover = false;
            if (_spacingRecover)
            {
                _spacingHold = true;
                // An approach is optional. It needs a phase which cannot create
                // an unobserved attack inside the next 24 ticks, real continuous
                // footing, and a corridor clear of observed bodies/projectiles.
                // Otherwise coast and let the Boss close instead of blind-running.
                return CanReturn(s, queen, second, floor, -side) ? -side : 0;
            }
            if (direction != side) return direction;
            if (gap + StoppingTravel(p, side) + Math.Abs(p.Velocity.X) >= upper) _spacingHold = true;
            else if (gap <= lower) _spacingHold = false;
            // Close contact takes priority over the spacing preference. P2's
            // late attack window still builds gap here, but does not run away
            // forever once already well outside its native 250px selection test.
            if (gap <= Math.Max(240f, queen.Width * .5f + p.Width * .5f + 96f)) return direction;
            return _spacingHold ? 0 : direction;
        }

        private bool CanCoastOutsideSlam(CombatSnapshot s, TargetSnapshot queen, float ideal, int direction)
        {
            var endX = queen.Center.X;
            if (queen.Ai2 == 1f) endX += queen.Velocity.X * 4f;
            else if (queen.Ai2 == 0f && queen.Ai1 >= 30f && queen.Velocity.Y != 0f)
            {
                // AI_121 1177..12c6: the observed rise vx is unchanged through
                // ++ai1>=60, including that transition tick; only vy damps.
                // The later drop contributes another geometric sum of 4*vx.
                endX += queen.Velocity.X * (Math.Max(0f, (float)Math.Ceiling(60f - queen.Ai1)) + 4f);
            }
            else return false; // Windup has not exposed its actual launch vector.
            return CanCoastOutsideDanger(s.Player, queen.Center.X, endX, ideal, direction);
        }

        private bool CanCoastOutsideDanger(PlayerSnapshot p, float startX, float endX, float ideal, int direction)
        {
            // Maximum wave half-width is 240px. Require the CURRENT body to be
            // outside the entire future landing segment before easing an exit;
            // a projected safe stop alone cannot justify staying inside a slam.
            var gap = Math.Min((p.Center.X - startX) * direction, (p.Center.X - endX) * direction);
            if (gap < Math.Max(ideal, 240f + p.Width * .5f + 64f) || p.Velocity.X * direction < 0f) return false;
            if (gap + StoppingTravel(p, direction) + Math.Abs(p.Velocity.X) >= Math.Max(480f, ideal + 180f))
                _spacingHold = true;
            return _spacingHold;
        }

        private static float StoppingTravel(PlayerSnapshot p, int direction)
        {
            var speed = Math.Max(0f, p.Velocity.X * direction);
            if (speed == 0f) return 0f;
            var drag = p.RunSlowdown * (p.OnGround ? 1f : .5f);
            // v^2/(2*d) is conservative for native decelerate-before-move.
            // Zero drag does not magically stop; fail conservatively to coast.
            return drag > 0f ? speed * speed / (2f * drag) : float.PositiveInfinity;
        }

        private static string NativePhase(TargetSnapshot q, bool second, bool worthy)
        {
            if (q.Ai0 == 0f)
                return second ? q.Ai1 >= 96f ? "second-attack-window-build-gap" : "second-low-runway-bait" : "first-ground-runway";
            if (q.Ai0 == 1f) return "teleport-reform-regain-gap";
            if (q.Ai0 == 2f) return q.LocalAiKnown ? "teleport-leave-known-destination" : "teleport-destination-unobserved";
            if (q.Ai0 == 3f) return second ? "second-observed-jump-sequence" : "first-native-jump-sequence";
            if (q.Ai0 == 4f)
            {
                if (q.Ai2 == 0f)
                    return second ? "second-observed-slam-rise" : q.Ai1 < 30f ? "first-slam-rise-windup" : "first-slam-rising";
                if (q.Ai2 != 1f) return "unrecognized-native-state";
                if (q.Velocity.Y == 0f) return second ? "second-slam-landing-pending" : "first-slam-landing-pending";
                var delay = worthy ? 0f : second ? 10f : 30f;
                if (second && q.Ai1 >= delay + 120f) return "second-slam-timeout-pending";
                return q.Ai1 < delay ? second ? "second-slam-drop-windup" : "first-slam-drop-windup" :
                    second ? "second-slam-descending" : "first-slam-descending";
            }
            if (q.Ai0 == 5f)
            {
                if (q.Ai2 == 0f) return second ? "second-gel-windup" : "first-gel-windup";
                if (q.Ai2 != 1f) return "unrecognized-native-state";
                return q.Ai1 >= 9f ? second ? "second-gel-fire-pending" : "first-gel-fire-pending" :
                    second ? "second-gel-countdown" : "first-gel-countdown";
            }
            return "unrecognized-native-state";
        }

        private static int EscapeSide(PlayerSnapshot p, float dangerX, int previous)
        {
            var delta = p.Center.X + p.Velocity.X * 8f - dangerX;
            return Math.Abs(delta) <= 8f ? previous : Math.Sign(delta);
        }

        private static SupportSpan LowSupport(CombatSnapshot s)
        {
            if (s.Mobility.GravityInverted) return default(SupportSpan);
            var foot = s.Player.Position.Y + s.Player.Height;
            var floor = s.Arena.FloorSupport;
            if (floor.Valid && !floor.Inverted && floor.SurfaceY >= foot - 6f) return floor;
            var recovery = s.Arena.RecoverySupport;
            return recovery.Valid && !recovery.Inverted && recovery.SurfaceY >= foot - 6f ? recovery : default(SupportSpan);
        }

        private static float LaneRemaining(CombatSnapshot s, SupportSpan floor, int direction)
        {
            var clearance = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            if (floor.ContainsBody(s.Player.Position.X, s.Player.Width))
                clearance = Math.Min(clearance, direction > 0 ? floor.Right - s.Player.Position.X - s.Player.Width : s.Player.Position.X - floor.Left);
            return Math.Max(0f, clearance);
        }

        private static int KeepInsideLane(CombatSnapshot s, SupportSpan floor, int direction)
        {
            if (direction == 0) return 0;
            if (s.Player.OnGround && !floor.ContainsBody(s.Player.Position.X, s.Player.Width)) return 0;
            var speed = Math.Max(0f, s.Player.Velocity.X * direction);
            var braking = speed * speed / (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            return LaneRemaining(s, floor, direction) < Math.Max(20f, braking + 12f) ? 0 : direction;
        }

        private static bool TryNearbyWave(CombatSnapshot s, out Vec2 center)
        {
            center = default(Vec2);
            var closest = float.MaxValue;
            for (var i = 0; i < s.Threats.Count; i++)
            {
                var wave = s.Threats[i];
                if (wave.Kind != ThreatKind.Projectile || wave.Type != 922 || wave.TimeLeft <= 0) continue;
                var c = new Vec2(wave.Position.X + wave.Width * .5f, wave.Position.Y + wave.Height * .5f);
                // AI_135 reaches 480x480; this is an intentionally conservative
                // current-presence exclusion, NOT an age-accurate damage model.
                if (Math.Abs(c.Y - s.Player.Center.Y) > 240f + s.Player.Height * .5f + 36f ||
                    Math.Abs(c.X - s.Player.Center.X) > 480f) continue;
                var distance = Vec2.DistanceSquared(c, s.Player.Center);
                if (distance >= closest) continue;
                closest = distance;
                center = c;
            }
            return closest < float.MaxValue;
        }

        private static TargetSnapshot SelectPressureTarget(CombatSnapshot s, TargetSnapshot queen)
        {
            var selected = queen;
            var closest = 300f * 300f;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var t = s.Targets[i];
                if (t.Type < 658 || t.Type > 660 || t.Life <= 0 || t.Invulnerable || !t.Chaseable ||
                    !t.LineOfSightKnown || !t.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(s.Player.Center, t.Center);
                if (distance >= closest) continue;
                selected = t;
                closest = distance;
            }
            return selected;
        }

        private static bool CanReturn(CombatSnapshot s, TargetSnapshot queen, bool second, SupportSpan floor, int direction)
        {
            var p = s.Player;
            if (!p.OnGround || s.Mobility.GravityInverted || s.Mobility.Grappling || s.Mobility.MountActive ||
                !floor.ContainsBody(p.Position.X, p.Width) || Math.Abs(floor.SurfaceY - p.Position.Y - p.Height) > 6f ||
                s.Difficulty.ForTheWorthy || s.Difficulty.Zenith || s.Difficulty.Remix ||
                !HasGuardedReturnWindow(queen, second)) return false;
            var x = p.Position.X;
            var vx = p.Velocity.X;
            var left = x;
            var right = x + p.Width;
            for (var tick = 1; tick <= ReturnGuardTicks; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, direction, true, 1, out travel);
                x += travel;
                if (!floor.ContainsBody(x, p.Width)) return false;
                left = Math.Min(left, x);
                right = Math.Max(right, x + p.Width);
            }
            var corridor = new RectF(left, p.Position.Y, right - left, p.Height).Inflated(32f);
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var t = s.Targets[i];
                if (t.Life <= 0) continue;
                var body = SweptBounds(t.Position, t.Velocity, t.Width, t.Height, ReturnGuardTicks);
                if (t.Key == queen.Key)
                {
                    // FlyMovement accel .085, doubled on a hard turn; native
                    // SimpleFlyMovement can apply it twice per axis. Integrate
                    // the .34/tick bound; do not pretend the last vector is fixed.
                    if (second) body = body.Inflated(.17f * ReturnGuardTicks * (ReturnGuardTicks + 1f));
                    // A zero observed vy proves current contact, not support
                    // beneath every future Boss footprint. Include downward
                    // gravity even if it could leave that support after a turn.
                    else body.Height += .15f * ReturnGuardTicks * (ReturnGuardTicks + 1f);
                    if (!second && queen.Ai0 == 3f)
                    {
                        // A grounded jump-sequence snapshot can launch on the
                        // next native update. Its largest jump impulse is -13y
                        // and its observed horizontal impulse is at most 4.5;
                        // bound that unseen launch rather than treating vy=0 as
                        // a stationary body for the whole approach horizon.
                        var jumpX = 4.5f * ReturnGuardTicks;
                        var jumpY = 13f * ReturnGuardTicks;
                        body = new RectF(body.X - jumpX, body.Y - jumpY,
                            body.Width + jumpX * 2f, body.Height + jumpY);
                    }
                }
                else if (t.Type >= 658 && t.Type <= 660)
                {
                    // A return is optional: refuse an occupied minion column
                    // rather than assuming its next jump/shot cannot occur.
                    body.Y -= 240f;
                    body.Height += 480f;
                }
                if (corridor.Intersects(body)) return false;
            }
            for (var i = 0; i < s.Threats.Count; i++)
            {
                var t = s.Threats[i];
                if (t.Kind != ThreatKind.Projectile || t.TimeLeft <= 0) continue;
                var ticks = Math.Min(ReturnGuardTicks, t.TimeLeft);
                if (t.Geometry != ThreatGeometry.Body)
                {
                    var beam = BeamGeometry.Sweep(t, 0f, ticks);
                    if (BeamGeometry.Intersects(corridor, beam)) return false;
                    continue;
                }
                RectF body;
                var nativeTrajectory = t.Trajectory != ThreatTrajectory.Linear;
                if (nativeTrajectory)
                {
                    ProjectileMotionSweep motion;
                    if (!HostileProjectileMotion.TrySweep(t, 0, ticks, out motion)) return false;
                    if (!motion.Active) continue;
                    body = motion.Bounds;
                }
                else body = SweptBounds(t.Position, t.Velocity, t.Width, t.Height, ticks);
                if (t.Type == 922)
                {
                    var c = new Vec2(t.Position.X + t.Width * .5f, t.Position.Y + t.Height * .5f);
                    body = new RectF(c.X - 240f, c.Y - 240f, 480f, 480f);
                }
                else if (t.Type == 920 || t.Type == 921 || t.Type == 926)
                {
                    // Unknown ai0 cannot establish the initial gravity delay.
                    // Envelope both no-gravity and .15/tick downward travel.
                    if (!nativeTrajectory)
                        body.Height += .075f * ticks * (ticks + 1f);
                    if (!floor.OneWay && body.Bottom >= floor.SurfaceY && body.Top <= floor.SurfaceY &&
                        body.Right > corridor.Left && body.Left < corridor.Right) return false;
                }
                if (corridor.Intersects(body)) return false;
            }
            return true;
        }

        private static bool HasGuardedReturnWindow(TargetSnapshot queen, bool second)
        {
            if (float.IsNaN(queen.Ai0) || float.IsInfinity(queen.Ai0) ||
                float.IsNaN(queen.Ai1) || float.IsInfinity(queen.Ai1) ||
                float.IsNaN(queen.Ai2) || float.IsInfinity(queen.Ai2)) return false;
            if (queen.Ai0 == 0f)
            {
                // Native increments first and selects only when >60 / >120.
                return queen.Ai1 >= 0f && queen.Ai1 + ReturnGuardTicks <= (second ? 120f : 60f);
            }
            if (queen.Ai0 == 3f)
            {
                // State 3 is the P1 jump sequence and creates no projectile.
                // Its possible within-horizon relaunch is covered geometrically.
                return !second && queen.Ai2 >= 0f && queen.Ai2 <= 3f;
            }
            if (queen.Ai0 == 5f)
            {
                // ai2=0 reaches 50, resets ai1, then needs ten more updates to
                // fire. With an entry value <36 the birth is strictly beyond
                // this 24-tick corridor. ai2=1 can always fire within ten ticks.
                return queen.Ai2 == 0f && queen.Ai1 >= 0f && queen.Ai1 < 36f;
            }
            // State 4 can land and create a previously unobserved 922 on any
            // update. Its committed horizontal emergency exit always wins.
            return false;
        }

        private static RectF SweptBounds(Vec2 position, Vec2 velocity, int width, int height, float ticks)
        {
            var end = position + velocity * ticks;
            return new RectF(Math.Min(position.X, end.X), Math.Min(position.Y, end.Y),
                width + Math.Abs(end.X - position.X), height + Math.Abs(end.Y - position.Y));
        }
    }

    internal static class MechanicalFamilies
    {
        public static bool IsTwin(int type) => type == 125 || type == 126;
        public static bool IsDestroyer(int type) => type >= 134 && type <= 136;
        public static bool IsPrime(int type) => type >= 127 && type <= 131;
        public static int Count(IList<TargetSnapshot> bosses)
        {
            var twin = false;
            var destroyer = false;
            var prime = false;
            for (var i = 0; i < bosses.Count; i++)
            {
                var type = bosses[i].Type;
                if (IsTwin(type)) twin = true;
                else if (IsDestroyer(type)) destroyer = true;
                else if (IsPrime(type)) prime = true;
            }
            return (twin ? 1 : 0) + (destroyer ? 1 : 0) + (prime ? 1 : 0);
        }
    }

    /// <summary>
    /// One source of truth for the hash-reviewed vanilla 1.4.5.8 classic Twins
    /// fixture. Pre-spawn checks additionally require a real runway and nonempty
    /// flight charge; a live fight may legitimately reach zero charge while the
    /// saved-runway controller is returning to ground.
    /// </summary>
    internal static class TwinsClassicContract
    {
        internal const float MinimumRunwayPixels = 1300f;
        internal const int DemonWingsItemType = 492;
        internal const int LightningBootsItemType = 898;

        public static bool IsSupported(CombatSnapshot snapshot, bool allowPreSpawn, out string reason)
        {
            if (snapshot == null || snapshot.Player == null || snapshot.Mobility == null ||
                snapshot.Arena == null || snapshot.Difficulty == null || snapshot.Targets == null)
            {
                reason = "双子魔眼原生快照不完整";
                return false;
            }
            if (!snapshot.NativeContextKnown || snapshot.LocalPlayerIndex < 0 || snapshot.LocalPlayerIndex >= 255)
            {
                reason = "无法确认双子魔眼的原生网络模式与本地玩家索引";
                return false;
            }
            if (snapshot.NetMode != 0)
            {
                reason = "当前双子魔眼策略只支持原版单机模式";
                return false;
            }
            var arena = snapshot.Arena;
            if (!Finite(arena.ClearanceLeft) || !Finite(arena.ClearanceRight) ||
                !Finite(arena.ClearanceUp) || !Finite(arena.ClearanceDown) ||
                arena.ClearanceLeft < 0f || arena.ClearanceRight < 0f ||
                arena.ClearanceUp < 0f || arena.ClearanceDown < 0f ||
                !Finite(arena.HorizontalClearance) || !Finite(arena.VerticalClearance))
            {
                reason = "双子魔眼场地净空不是可信的有限数值";
                return false;
            }
            if (!ClassicFixture(snapshot.Difficulty))
            {
                reason = "当前难度、世界种子或时间不在双子魔眼已复核范围内";
                return false;
            }
            if (!KnownDemonFlight(snapshot))
            {
                reason = "双子魔眼需要已复核的恶魔之翼与闪电靴原生机动配置";
                return false;
            }

            var retCount = 0;
            var spazCount = 0;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var eye = snapshot.Targets[i];
                if (eye.Life <= 0 || !MechanicalFamilies.IsTwin(eye.Type)) continue;
                if (eye.Type == 125) retCount++;
                else spazCount++;
                if (!ValidEye(eye))
                {
                    reason = "双子魔眼的原生 NPC 状态无效";
                    return false;
                }
                if (!eye.NativeTargetKnown)
                {
                    reason = "无法确认双子魔眼的原生目标玩家";
                    return false;
                }
                if (eye.NativeTargetPlayerIndex != snapshot.LocalPlayerIndex)
                {
                    reason = "双子魔眼当前未以本地玩家为目标";
                    return false;
                }
            }
            if (retCount > 1 || spazCount > 1)
            {
                reason = "无法确认唯一的双子魔眼实体";
                return false;
            }
            if (retCount + spazCount == 0)
            {
                if (!allowPreSpawn)
                {
                    reason = "未观察到仍存活的双子魔眼";
                    return false;
                }
                SupportSpan runway;
                if (!TryPreSpawnRunway(snapshot, out runway))
                {
                    reason = "召唤前必须真实站在至少 1300 像素的连续跑道上";
                    return false;
                }
                if (NativeResourceFraction(snapshot) <= 0f)
                {
                    reason = "召唤前有限飞行资源必须大于零";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private static bool ClassicFixture(DifficultySnapshot d)
        {
            return d.GameModeKnown && d.GameMode == 0 && !d.Journey && !d.Expert && !d.Master &&
                !d.Drunk && !d.NotTheBees && !d.ForTheWorthy && !d.Remix && !d.Zenith &&
                !d.Celebration && !d.Constant && !d.NoTraps && !d.Skyblock && !d.DayTime;
        }

        private static bool KnownDemonFlight(CombatSnapshot snapshot)
        {
            var p = snapshot.Player;
            var f = p.Flight;
            var m = snapshot.Mobility;
            if (!p.FunctionalEquipmentIdentityKnown || p.WingAccessoryItemType != DemonWingsItemType ||
                p.RocketBootAccessoryItemType != LightningBootsItemType ||
                !f.Known || !p.Jump.Known || f.WingsLogic != 1 || f.RocketBoots != 2 ||
                f.WingTimeMax != 100 || f.RocketTimeMax != 7 || f.RocketDelay != 0 ||
                !Finite(f.WingTime) || f.WingTime < 0f || f.WingTime > 142f ||
                f.WingTime != (int)f.WingTime || f.RocketTime < 0 || f.RocketTime > 7 ||
                !m.HasFiniteFlightResource || !Finite(m.FlightResourceFraction) ||
                m.FlightResourceFraction < 0f || m.FlightResourceFraction > 1f)
                return false;
            // Optional tools in inventory do not alter the reviewed low-gear
            // trajectory until activated. Feather Fall is admitted only when
            // the exact jump snapshot agrees with the mobility observation;
            // its native gravity branch is then scored by CombatPlanner. An
            // active mount, attached grapple or inverted gravity changes the
            // whole motion contract and remains fail-closed here.
            if (m.GravityInverted || m.MountActive || m.Grappling ||
                p.Jump.SlowFall != m.FeatherFall)
                return false;
            if (!Finite(p.Position.X) || !Finite(p.Position.Y) || !Finite(p.Velocity.X) ||
                !Finite(p.Velocity.Y) || p.Width <= 0 || p.Height <= 0 || !Finite(p.Gravity) ||
                p.Gravity <= 0f || !Finite(p.MaxFallSpeed) || p.MaxFallSpeed <= 0f ||
                !Finite(p.BaseRunSpeed) || p.BaseRunSpeed <= 0f || !Finite(p.MaxRunSpeed) ||
                p.MaxRunSpeed < p.BaseRunSpeed || !Finite(p.RunAcceleration) || p.RunAcceleration <= 0f ||
                !Finite(p.RunSlowdown) || p.RunSlowdown <= 0f || !Finite(p.Jump.Speed) ||
                p.Jump.Speed <= 0f || p.Jump.Height <= 0)
                return false;
            if (p.WingTime != f.WingTime || p.RocketTime != f.RocketTime) return false;
            return Math.Abs(m.FlightResourceFraction - FlightMotion.ResourceFraction(in f)) <= .001f;
        }

        private static bool ValidEye(TargetSnapshot eye)
        {
            return eye.Boss && eye.Key >= 0 && eye.Life > 0 && eye.LifeMax > 0 &&
                eye.Life <= eye.LifeMax && eye.Width > 0 && eye.Height > 0 &&
                Finite(eye.Position.X) && Finite(eye.Position.Y) && Finite(eye.Velocity.X) &&
                Finite(eye.Velocity.Y) && eye.Velocity.LengthSquared <= 96f * 96f &&
                Finite(eye.Ai0) && Finite(eye.Ai1) && Finite(eye.Ai2) && Finite(eye.Ai3);
        }

        private static bool TryPreSpawnRunway(CombatSnapshot snapshot, out SupportSpan runway)
        {
            runway = default(SupportSpan);
            if (!snapshot.Player.OnGround) return false;
            var foot = snapshot.Player.Position.Y + snapshot.Player.Height;
            if (AtFeet(snapshot.Arena.FloorSupport, snapshot.Player, foot))
                runway = snapshot.Arena.FloorSupport;
            else if (AtFeet(snapshot.Arena.RecoverySupport, snapshot.Player, foot))
                runway = snapshot.Arena.RecoverySupport;
            return runway.Valid && runway.Right - runway.Left >= MinimumRunwayPixels;
        }

        private static bool AtFeet(SupportSpan support, PlayerSnapshot player, float foot)
        {
            return support.Valid && !support.Inverted && Finite(support.Left) && Finite(support.Right) &&
                Finite(support.SurfaceY) && support.ContainsBody(player.Position.X, player.Width) &&
                Math.Abs(support.SurfaceY - foot) <= 4f;
        }

        internal static float NativeResourceFraction(CombatSnapshot snapshot)
        {
            return Math.Min(snapshot.Mobility.FlightResourceFraction,
                FlightMotion.ResourceFraction(in snapshot.Player.Flight));
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal sealed class TwinsStrategy : BossStrategyBase
    {
        private enum NativePhase
        {
            FirstHover,
            FirstLaunch,
            FirstCharge,
            FirstBrake,
            TransformPending,
            TransformSpinUp,
            TransformSpinDown,
            RetSecondOverheadLasers,
            RetSecondSideLasers,
            SpazSecondFlame,
            SpazSecondLaunch,
            SpazSecondCharge,
            SpazSecondBrake,
            Unknown
        }

        private enum MovementState
        {
            AcquireRunway,
            SupportedRun,
            CommittedEscape,
            RecoverRunway
        }

        private const int TurnHoldTicks = 60;
        private const int DefiniteContractLossTicks = 3;
        private const int ObservationGraceTicks = 12;
        private const int RouteClosedReturnTicks = 90;
        private const int RecoveryTimeoutTicks = 600;
        private const int RouteHorizonTicks = 24;
        private const float ResumeResourceFraction = .9f;
        private const float RouteReservePixels = 48f;

        private MovementState _state;
        private SupportSpan _anchor;
        private float _returnLeft;
        private float _returnRight;
        private bool _anchorValid;
        private int _runDirection;
        private int _committedDirection;
        private int _recoveryDirection;
        private int _ticksSinceTurn;
        private int _recoveryTicks;
        private int _contractLossTicks;
        private int _contractLossMode;
        private int _nativeUnknownTicks;
        private int _anchorMissingTicks;
        private int _acquireMissingTicks;
        private int _routeClosedTicks;
        private float _bestRecoveryProgress = float.MaxValue;

        public TwinsStrategy() : base("twins", 1300, 520, 6f, true)
        {
            Requirements.RequiresTwinsClassicContract = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 125, 126) && MechanicalFamilies.Count(b) == 1;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            TargetSnapshot ret;
            TargetSnapshot spaz;
            var hasRet = TryEye(s, 125, out ret);
            var hasSpaz = TryEye(s, 126, out spaz);
            var target = hasSpaz ? spaz : ret;
            var retPhase = hasRet ? ObservePhase(ret) : NativePhase.Unknown;
            var spazPhase = hasSpaz ? ObservePhase(spaz) : NativePhase.Unknown;
            var pressure = SelectPressure(hasRet, ret, retPhase, hasSpaz, spaz, spazPhase);
            var pressurePhase = pressure.Type == 126 ? spazPhase : retPhase;
            var ideal = pressurePhase == NativePhase.SpazSecondFlame ? 620f :
                pressurePhase == NativePhase.RetSecondOverheadLasers || pressurePhase == NativePhase.RetSecondSideLasers ? 540f : 560f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(240f, s.Weapon.Profile.ConservativeRangePixels * .68f));

            if (m.PreviousTargetKey < 0) ResetRoute();
            if (_ticksSinceTurn <= TurnHoldTicks) _ticksSinceTurn++;

            string contractReason;
            var contractSupported = TwinsClassicContract.IsSupported(s, false, out contractReason);
            var unknown = !hasRet && !hasSpaz || hasRet && retPhase == NativePhase.Unknown ||
                hasSpaz && spazPhase == NativePhase.Unknown;
            if (!contractSupported)
            {
                _nativeUnknownTicks = 0;
                _anchorMissingTicks = 0;
                _acquireMissingTicks = 0;
                _routeClosedTicks = 0;
                var definiteMismatch = DefiniteContractMismatch(s);
                var lossMode = definiteMismatch ? 2 : 1;
                if (_contractLossMode != lossMode) _contractLossTicks = 0;
                _contractLossMode = lossMode;
                _contractLossTicks = SaturatingIncrement(_contractLossTicks);
                var threshold = definiteMismatch ? DefiniteContractLossTicks : ObservationGraceTicks;
                var requestReturn = _contractLossTicks >= threshold;
                return BuildDecision(s, target, pressure, ideal,
                    "unsupported-twins-fixture", 0, false, true, requestReturn,
                    requestReturn ? definiteMismatch ?
                        "contract-lost: Twins fixture remained incompatible" :
                        "observation-lost: Twins native fixture remained unobservable" : null);
            }
            _contractLossTicks = 0;
            _contractLossMode = 0;

            if (unknown)
            {
                _nativeUnknownTicks = SaturatingIncrement(_nativeUnknownTicks);
                _anchorMissingTicks = 0;
                _acquireMissingTicks = 0;
                _routeClosedTicks = 0;
                var requestReturn = _nativeUnknownTicks >= ObservationGraceTicks;
                return BuildDecision(s, target, pressure, ideal,
                    "unsupported-twins-native-phase", 0, false, true, requestReturn,
                    requestReturn ? "phase-observation-lost: Twins native phase remained unknown" : null);
            }
            _nativeUnknownTicks = 0;

            if (!_anchorValid)
            {
                SupportSpan acquired;
                if (!TryAcquireRunway(s, out acquired))
                {
                    _state = MovementState.AcquireRunway;
                    _anchorMissingTicks = 0;
                    _routeClosedTicks = 0;
                    _acquireMissingTicks = SaturatingIncrement(_acquireMissingTicks);
                    var acquireReturn = _acquireMissingTicks >= ObservationGraceTicks;
                    return BuildDecision(s, target, pressure, ideal,
                        "acquire-runway-no-safe-support", 0, CanFire(s, target), true,
                        acquireReturn, acquireReturn ?
                            "runway-observation-lost: no safe Twins runway could be acquired" : null);
                }
                _acquireMissingTicks = 0;
                SaveRunway(s, acquired, pressure);
                var dangerousAtAcquire = IsCommittedDanger(retPhase) || IsCommittedDanger(spazPhase);
                _state = dangerousAtAcquire ? MovementState.CommittedEscape :
                    ResourcesRecovered(s) ? MovementState.SupportedRun : MovementState.RecoverRunway;
                if (dangerousAtAcquire) _committedDirection = _runDirection;
                else if (_state == MovementState.RecoverRunway) EnterRecovery(s);
                var acquiredHorizontal = RouteClear(s, _runDirection, true, true) ? _runDirection : 0;
                var routeReturn = TrackRouteClosed(acquiredHorizontal == 0);
                return BuildDecision(s, target, pressure, ideal,
                    acquiredHorizontal == 0 ? "acquire-runway-route-closed" : "acquire-runway",
                    acquiredHorizontal, CanFire(s, target), false, routeReturn,
                    routeReturn ? "route-closed: Twins runway had no safe exit for 90 ticks" : null);
            }

            if (!AnchorObserved(s))
            {
                _routeClosedTicks = 0;
                _anchorMissingTicks = SaturatingIncrement(_anchorMissingTicks);
                var requestReturn = _anchorMissingTicks >= ObservationGraceTicks;
                return BuildDecision(s, target, pressure, ideal,
                    "anchor-lost-closed", 0, CanFire(s, target), true, requestReturn,
                    requestReturn ? "runway-observation-lost: saved Twins runway remained unobserved" : null);
            }
            _anchorMissingTicks = 0;

            var dangerous = IsCommittedDanger(retPhase) || IsCommittedDanger(spazPhase);
            if (dangerous && _state != MovementState.CommittedEscape)
            {
                _state = MovementState.CommittedEscape;
                _committedDirection = _runDirection == 0 ? ChooseInitialDirection(s, pressure) : _runDirection;
                _recoveryTicks = 0;
            }

            if (_state == MovementState.CommittedEscape)
            {
                if (dangerous)
                    return EvaluateCommitted(s, target, pressure, pressurePhase, ideal);
                EnterRecovery(s);
            }

            if (_state == MovementState.SupportedRun && !IsOnAnchor(s)) EnterRecovery(s);
            if (_state == MovementState.RecoverRunway)
                return EvaluateRecovery(s, target, pressure, pressurePhase, ideal);
            return EvaluateSupported(s, target, pressure, pressurePhase, hasRet, ret, retPhase,
                hasSpaz, spaz, spazPhase, ideal);
        }

        private BossDecision EvaluateSupported(CombatSnapshot s, TargetSnapshot target,
            TargetSnapshot pressure, NativePhase pressurePhase, bool hasRet, TargetSnapshot ret,
            NativePhase retPhase, bool hasSpaz, TargetSnapshot spaz, NativePhase spazPhase, float ideal)
        {
            if (!IsOnAnchor(s) || !ResourcesRecovered(s))
            {
                EnterRecovery(s);
                return EvaluateRecovery(s, target, pressure, pressurePhase, ideal);
            }
            if (_runDirection == 0) _runDirection = ChooseInitialDirection(s, pressure);

            var player = s.Player;
            var lane = LaneToReturnEdge(s, _runDirection);
            var otherLane = LaneToReturnEdge(s, -_runDirection);
            var stopping = StoppingTravel(player, _runDirection);
            var turnThreshold = Math.Max(340f,
                stopping + Math.Abs(player.Velocity.X) * RouteHorizonTicks + RouteReservePixels);
            var needsTurn = lane <= turnThreshold;
            var horizontal = _runDirection;
            var phase = NativePhaseLabel(pressure, pressurePhase) + "-supported-run";
            if (needsTurn)
            {
                var reverse = -_runDirection;
                if (_ticksSinceTurn > TurnHoldTicks && otherLane > stopping + 160f &&
                    SafeTurnWindow(hasRet, ret, retPhase) && SafeTurnWindow(hasSpaz, spaz, spazPhase) &&
                    RouteClear(s, reverse, true, true))
                {
                    _runDirection = horizontal = reverse;
                    _ticksSinceTurn = 0;
                    phase += "-safe-turn";
                }
                else if (!RouteClear(s, horizontal, true, true))
                {
                    horizontal = 0;
                    phase += "-route-closed";
                }
            }
            else if (!RouteClear(s, horizontal, true, true))
            {
                horizontal = 0;
                phase += "-route-closed";
            }
            var requestReturn = TrackRouteClosed(horizontal == 0);
            return BuildDecision(s, target, pressure, ideal, phase, horizontal, CanFire(s, target), false,
                requestReturn, requestReturn ? "route-closed: Twins runway had no safe exit for 90 ticks" : null);
        }

        private BossDecision EvaluateCommitted(CombatSnapshot s, TargetSnapshot target,
            TargetSnapshot pressure, NativePhase pressurePhase, float ideal)
        {
            if (_committedDirection == 0)
                _committedDirection = _runDirection == 0 ? ChooseInitialDirection(s, pressure) : _runDirection;
            _runDirection = _committedDirection;
            var horizontal = RouteClear(s, _committedDirection, true, true) ? _committedDirection : 0;
            var phase = NativePhaseLabel(pressure, pressurePhase) + "-committed-escape";
            if (horizontal == 0) phase += "-route-closed";
            var requestReturn = TrackRouteClosed(horizontal == 0);
            return BuildDecision(s, target, pressure, ideal, phase, horizontal, CanFire(s, target), false,
                requestReturn, requestReturn ? "route-closed: Twins committed escape remained blocked for 90 ticks" : null);
        }

        private BossDecision EvaluateRecovery(CombatSnapshot s, TargetSnapshot target,
            TargetSnapshot pressure, NativePhase pressurePhase, float ideal)
        {
            if (IsOnAnchor(s) && InReturnZone(s.Player) && ResourcesRecovered(s))
            {
                _state = MovementState.SupportedRun;
                _recoveryDirection = 0;
                _recoveryTicks = 0;
                _bestRecoveryProgress = float.MaxValue;
                TargetSnapshot ret;
                TargetSnapshot spaz;
                var hasRet = TryEye(s, 125, out ret);
                var hasSpaz = TryEye(s, 126, out spaz);
                return EvaluateSupported(s, target, pressure, pressurePhase,
                    hasRet, ret, hasRet ? ObservePhase(ret) : NativePhase.Unknown,
                    hasSpaz, spaz, hasSpaz ? ObservePhase(spaz) : NativePhase.Unknown,
                    ideal);
            }
            var recoveryTimedOut = RecoveryTimedOut(s.Player);

            if (_recoveryDirection == 0) _recoveryDirection = SelectRecoveryDirection(s.Player);
            if (_recoveryDirection != 0) _runDirection = _recoveryDirection;
            var horizontal = RecoveryRouteClear(s, _recoveryDirection) ? _recoveryDirection : 0;
            var phase = NativePhaseLabel(pressure, pressurePhase) + "-recover-runway";
            if (TwinsClassicContract.NativeResourceFraction(s) <= 0f) phase += "-empty-flight";
            if (horizontal == 0) phase += "-route-closed";
            var routeReturn = TrackRouteClosed(horizontal == 0);
            if (recoveryTimedOut)
                return BuildDecision(s, target, pressure, ideal, "recover-runway-timeout-closed",
                    0, CanFire(s, target), true, true,
                    "watchdog: Twins runway recovery made no 16px progress for 600 ticks");
            return BuildDecision(s, target, pressure, ideal, phase, horizontal, CanFire(s, target), false,
                routeReturn, routeReturn ? "route-closed: Twins runway recovery remained blocked for 90 ticks" : null);
        }

        private BossDecision BuildDecision(CombatSnapshot s, TargetSnapshot target, TargetSnapshot pressure,
            float ideal, string phase, int horizontal, bool fire, bool fullClosure,
            bool requestReturn = false, string controlReturnReason = null)
        {
            if (requestReturn)
            {
                phase += "-control-return";
                horizontal = 0;
                fire = false;
                fullClosure = true;
            }
            var pressurePhase = ObservePhase(pressure);
            var margin = IsChargeBody(pressurePhase) ? 82f :
                pressurePhase == NativePhase.SpazSecondFlame ? 72f :
                pressurePhase == NativePhase.RetSecondSideLasers ? 58f : 48f;
            var decision = Decision(s, target, phase, BossPattern.Runway, ideal, 0f,
                horizontal, 0, false, false, false, margin, fire, pressure);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.OwnsHorizontalClosure = true;
            decision.Directive.OwnsMovementClosure = fullClosure;
            decision.Directive.JumpAction = JumpAction.Release;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            decision.Directive.IdealDistance = ideal;
            decision.Directive.VerticalOffset = 0f;
            decision.Directive.FloorClearance = 0f;
            decision.Directive.RequestControlReturn = requestReturn;
            decision.Directive.ControlReturnReason = controlReturnReason;
            return decision;
        }

        private static bool CanFire(CombatSnapshot s, TargetSnapshot target)
        {
            var visible = target.LineOfSightKnown ? target.HasLineOfSight : s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, s.Player.Center) <=
                s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            return target.Life > 0 && target.Chaseable && !target.Invulnerable && visible && inRange;
        }

        private void ResetRoute()
        {
            _state = MovementState.AcquireRunway;
            _anchor = default(SupportSpan);
            _returnLeft = _returnRight = 0f;
            _anchorValid = false;
            _runDirection = _committedDirection = _recoveryDirection = 0;
            _ticksSinceTurn = TurnHoldTicks + 1;
            _recoveryTicks = 0;
            _contractLossTicks = _nativeUnknownTicks = _anchorMissingTicks = _acquireMissingTicks = 0;
            _contractLossMode = 0;
            _routeClosedTicks = 0;
            _bestRecoveryProgress = float.MaxValue;
        }

        private static bool TryAcquireRunway(CombatSnapshot s, out SupportSpan support)
        {
            support = default(SupportSpan);
            if (!s.Player.OnGround) return false;
            var foot = s.Player.Position.Y + s.Player.Height;
            if (RunwayAtFeet(s.Arena.FloorSupport, s.Player, foot)) support = s.Arena.FloorSupport;
            else if (RunwayAtFeet(s.Arena.RecoverySupport, s.Player, foot)) support = s.Arena.RecoverySupport;
            return support.Valid;
        }

        private static bool RunwayAtFeet(SupportSpan support, PlayerSnapshot player, float foot)
        {
            return support.Valid && !support.Inverted && TwinsClassicContract.Finite(support.Left) &&
                TwinsClassicContract.Finite(support.Right) && TwinsClassicContract.Finite(support.SurfaceY) &&
                support.Right - support.Left >= TwinsClassicContract.MinimumRunwayPixels &&
                support.ContainsBody(player.Position.X, player.Width) && Math.Abs(support.SurfaceY - foot) <= 4f;
        }

        private void SaveRunway(CombatSnapshot s, SupportSpan support, TargetSnapshot pressure)
        {
            _anchor = support;
            var maximumStop = s.Player.MaxRunSpeed * s.Player.MaxRunSpeed /
                (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            var margin = Math.Min(240f, Math.Max(96f, maximumStop + RouteReservePixels));
            _returnLeft = support.Left + margin;
            _returnRight = support.Right - s.Player.Width - margin;
            if (_returnLeft > _returnRight)
                _returnLeft = _returnRight = (support.Left + support.Right - s.Player.Width) * .5f;
            _anchorValid = true;
            _runDirection = ChooseInitialDirection(s, pressure);
            _committedDirection = _recoveryDirection = 0;
            _ticksSinceTurn = TurnHoldTicks + 1;
            _recoveryTicks = 0;
            _anchorMissingTicks = _acquireMissingTicks = 0;
            _routeClosedTicks = 0;
            _bestRecoveryProgress = float.MaxValue;
        }

        private int ChooseInitialDirection(CombatSnapshot s, TargetSnapshot pressure)
        {
            // Spawn position determines both measured runway lanes; the pressure
            // eye supplies the preferred escape side. Keep that side only when
            // it has a complete braking/decision reserve, otherwise start toward
            // the longer observed boundary instead of scheduling an early turn.
            var away = AwayX(s.Player, pressure);
            var awayLane = LaneToReturnEdge(s, away);
            var otherLane = LaneToReturnEdge(s, -away);
            var required = Math.Max(340f, StoppingTravel(s.Player, away) +
                Math.Abs(s.Player.Velocity.X) * RouteHorizonTicks + RouteReservePixels);
            return awayLane >= required || awayLane >= otherLane ? away : -away;
        }

        private void EnterRecovery(CombatSnapshot s)
        {
            _state = MovementState.RecoverRunway;
            _committedDirection = 0;
            _recoveryTicks = 0;
            _recoveryDirection = SelectRecoveryDirection(s.Player);
            _bestRecoveryProgress = RecoveryProgress(s.Player);
            if (_recoveryDirection != 0 && _runDirection != _recoveryDirection)
            {
                _runDirection = _recoveryDirection;
                _ticksSinceTurn = 0;
            }
        }

        private int SelectRecoveryDirection(PlayerSnapshot player)
        {
            var projected = player.Position.X + player.Velocity.X * 8f;
            if (projected < _returnLeft) return 1;
            if (projected > _returnRight) return -1;
            var established = _runDirection == 0 ?
                (player.Position.X < (_returnLeft + _returnRight) * .5f ? 1 : -1) : _runDirection;
            var lane = established > 0 ? _returnRight - player.Position.X : player.Position.X - _returnLeft;
            return lane > StoppingTravel(player, established) + RouteReservePixels ? established : -established;
        }

        private bool AnchorObserved(CombatSnapshot s)
        {
            return SameAnchorWindow(s.Arena.FloorSupport) || SameAnchorWindow(s.Arena.RecoverySupport);
        }

        private bool SameAnchorWindow(SupportSpan observed)
        {
            return observed.Valid && !observed.Inverted && TwinsClassicContract.Finite(observed.Left) &&
                TwinsClassicContract.Finite(observed.Right) && TwinsClassicContract.Finite(observed.SurfaceY) &&
                Math.Abs(observed.SurfaceY - _anchor.SurfaceY) <= 2f &&
                observed.Left <= _returnRight && observed.Right >= _returnLeft;
        }

        private bool IsOnAnchor(CombatSnapshot s)
        {
            var p = s.Player;
            return p.OnGround && _anchor.ContainsBody(p.Position.X, p.Width) &&
                Math.Abs(p.Position.Y + p.Height - _anchor.SurfaceY) <= 4f;
        }

        private bool InReturnZone(PlayerSnapshot player)
        {
            return player.Position.X >= _returnLeft && player.Position.X <= _returnRight;
        }

        private bool ResourcesRecovered(CombatSnapshot s)
        {
            return IsOnAnchor(s) && TwinsClassicContract.NativeResourceFraction(s) >= ResumeResourceFraction;
        }

        private bool RecoveryTimedOut(PlayerSnapshot player)
        {
            var progress = RecoveryProgress(player);
            if (_bestRecoveryProgress == float.MaxValue)
            {
                _bestRecoveryProgress = progress;
                _recoveryTicks = 0;
            }
            else if (_bestRecoveryProgress - progress >= 16f)
            {
                // Progress is measured toward the saved landing zone rather
                // than as raw motion, so a small oscillation cannot feed the
                // watchdog forever. Sub-threshold gains accumulate against the
                // last checkpoint until they total one tile.
                _bestRecoveryProgress = progress;
                _recoveryTicks = 0;
            }
            else
            {
                _recoveryTicks = SaturatingIncrement(_recoveryTicks);
            }
            return _recoveryTicks >= RecoveryTimeoutTicks;
        }

        private float RecoveryProgress(PlayerSnapshot player)
        {
            return DistanceToReturnZone(player.Position.X) +
                Math.Abs(player.Position.Y + player.Height - _anchor.SurfaceY);
        }

        private bool TrackRouteClosed(bool closed)
        {
            if (!closed)
            {
                _routeClosedTicks = 0;
                return false;
            }
            _routeClosedTicks = SaturatingIncrement(_routeClosedTicks);
            return _routeClosedTicks >= RouteClosedReturnTicks;
        }

        private static bool DefiniteContractMismatch(CombatSnapshot s)
        {
            if (s == null || s.Player == null || s.Mobility == null || s.Difficulty == null)
                return false;
            var d = s.Difficulty;
            if (d.GameModeKnown && d.GameMode != 0 || d.Journey || d.Expert || d.Master ||
                d.Drunk || d.NotTheBees || d.ForTheWorthy || d.Remix || d.Zenith ||
                d.Celebration || d.Constant || d.NoTraps || d.Skyblock || d.DayTime)
                return true;
            if (s.NativeContextKnown && s.NetMode != 0) return true;

            var p = s.Player;
            var f = p.Flight;
            var m = s.Mobility;
            if (p.FunctionalEquipmentIdentityKnown &&
                (p.WingAccessoryItemType != TwinsClassicContract.DemonWingsItemType ||
                 p.RocketBootAccessoryItemType != TwinsClassicContract.LightningBootsItemType))
                return true;
            if (f.Known && (f.WingsLogic != 1 || f.RocketBoots != 2 ||
                f.WingTimeMax != 100 || f.RocketTimeMax != 7 || f.RocketDelay != 0))
                return true;
            return m.GravityInverted || m.MountActive || m.Grappling ||
                p.Jump.Known && p.Jump.SlowFall != m.FeatherFall;
        }

        private static int SaturatingIncrement(int value)
        {
            return value == int.MaxValue ? value : value + 1;
        }

        private float LaneToReturnEdge(CombatSnapshot s, int direction)
        {
            var player = s.Player;
            var anchorLane = direction > 0 ? _returnRight - player.Position.X : player.Position.X - _returnLeft;
            var observed = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            return Math.Max(0f, Math.Min(anchorLane, observed));
        }

        private bool RecoveryRouteClear(CombatSnapshot s, int direction)
        {
            if (direction == 0) return false;
            // A native reversal must first shed measured opposite momentum. That
            // bounded braking drift is not evidence that the selected return side
            // is wrong and must not trigger another A/-A oscillation.
            var maximumDistance = DistanceToReturnZone(s.Player.Position.X) +
                StoppingTravel(s.Player, -direction) + RouteReservePixels;
            return RouteClear(s, direction, false, true, maximumDistance);
        }

        private bool RouteClear(CombatSnapshot s, int direction, bool requireAnchor, bool includeEyes)
        {
            return RouteClear(s, direction, requireAnchor, includeEyes, float.MaxValue);
        }

        private bool RouteClear(CombatSnapshot s, int direction, bool requireAnchor, bool includeEyes,
            float maximumReturnDistance)
        {
            if (direction == 0) return false;
            var p = s.Player;
            var startX = p.Position.X;
            var x = startX;
            var vx = p.Velocity.X;
            for (var tick = 1; tick <= RouteHorizonTicks; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, direction, p.OnGround, 1, out travel);
                x += travel;
                if (requireAnchor && !_anchor.ContainsBody(x, p.Width)) return false;
                if (!requireAnchor && DistanceToReturnZone(x) > maximumReturnDistance + 2f) return false;
                var delta = x - startX;
                if (delta > s.Arena.ClearanceRight - 2f || -delta > s.Arena.ClearanceLeft - 2f) return false;
                var body = p.BoundsAt(new Vec2(x, p.Position.Y)).Inflated(20f);
                if (ObservedThreatIntersects(s, body, tick)) return false;
                if (includeEyes && EyeIntersects(s, body, tick)) return false;
            }
            return true;
        }

        private static bool ObservedThreatIntersects(CombatSnapshot s, RectF body, int tick)
        {
            for (var i = 0; i < s.Threats.Count; i++)
            {
                var threat = s.Threats[i];
                // A projectile with timeLeft == tick still moves and damages
                // on that final update before Update decrements it and Kill runs.
                if (threat.Damage <= 0 || threat.TimeLeft > 0 &&
                    threat.TimeLeft < tick) continue;
                if (threat.Geometry == ThreatGeometry.Body)
                {
                    var margin = threat.Kind == ThreatKind.NpcContact ? 48f : 24f;
                    if (threat.Trajectory != ThreatTrajectory.Linear)
                    {
                        ProjectileMotionSample sample;
                        if (!HostileProjectileMotion.TrySample(threat, tick,
                                out sample)) return true;
                        if (sample.Active && body.Intersects(
                                sample.Bounds.Inflated(margin))) return true;
                    }
                    else if (body.Intersects(threat.BoundsAt(tick)
                            .Inflated(margin))) return true;
                }
                else if (BeamGeometry.Intersects(body, BeamGeometry.Sweep(threat, 0, tick), 24f)) return true;
            }
            return false;
        }

        private static bool EyeIntersects(CombatSnapshot s, RectF body, int tick)
        {
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var eye = s.Targets[i];
                if (!MechanicalFamilies.IsTwin(eye.Type) || eye.Life <= 0) continue;
                var position = eye.Position + eye.Velocity * tick;
                if (body.Intersects(new RectF(position.X, position.Y, eye.Width, eye.Height).Inflated(56f)))
                    return true;
            }
            return false;
        }

        private float DistanceToReturnZone(float x)
        {
            return x < _returnLeft ? _returnLeft - x : x > _returnRight ? x - _returnRight : 0f;
        }

        private static bool IsCommittedDanger(NativePhase phase)
        {
            return IsChargeBody(phase) || phase == NativePhase.SpazSecondFlame;
        }

        private static string NativePhaseLabel(TargetSnapshot pressure, NativePhase phase)
        {
            return EyeName(pressure) + "-" + PhaseName(phase);
        }

        private static bool TryEye(CombatSnapshot s, int type, out TargetSnapshot eye)
        {
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Type != type || candidate.Life <= 0) continue;
                eye = candidate;
                return true;
            }
            eye = default(TargetSnapshot);
            return false;
        }

        private static NativePhase ObservePhase(TargetSnapshot eye)
        {
            if (eye.Ai0 == 0f)
            {
                // Both Twins test the independent 40% transform boundary before
                // entering their ai1 branch. Velocity is deliberately absent:
                // ordinary P1 Spazmatism hover already targets speed 12.
                // Native 1.4.5.8 IL uses conv.r8 + ldc.r8 0.4 here. Keeping
                // this comparison in Single precision makes some exact 40%
                // life totals (for example 4000/10000) transform one tick early.
                if ((double)eye.Life < (double)eye.LifeMax * .4) return NativePhase.TransformPending;
                if (eye.Ai1 == 0f) return NativePhase.FirstHover;
                if (eye.Ai1 == 1f) return NativePhase.FirstLaunch;
                if (eye.Ai1 == 2f)
                    return eye.Ai2 < (eye.Type == 126 ? 8f : 25f) ? NativePhase.FirstCharge : NativePhase.FirstBrake;
                return NativePhase.Unknown;
            }
            if (eye.Ai0 == 1f) return NativePhase.TransformSpinUp;
            if (eye.Ai0 == 2f) return NativePhase.TransformSpinDown;
            if (eye.Ai0 != 3f) return NativePhase.Unknown;
            if (eye.Type == 125)
            {
                if (eye.Ai1 == 0f) return NativePhase.RetSecondOverheadLasers;
                if (eye.Ai1 == 1f) return NativePhase.RetSecondSideLasers;
                return NativePhase.Unknown;
            }
            if (eye.Type != 126) return NativePhase.Unknown;
            if (eye.Ai1 == 0f) return NativePhase.SpazSecondFlame;
            if (eye.Ai1 == 1f) return NativePhase.SpazSecondLaunch;
            if (eye.Ai1 == 2f)
                return eye.Ai2 < 50f ? NativePhase.SpazSecondCharge : NativePhase.SpazSecondBrake;
            return NativePhase.Unknown;
        }

        private static TargetSnapshot SelectPressure(bool hasRet, TargetSnapshot ret, NativePhase retPhase,
            bool hasSpaz, TargetSnapshot spaz, NativePhase spazPhase)
        {
            if (!hasRet) return spaz;
            if (!hasSpaz) return ret;
            var retRank = PressureRank(retPhase);
            var spazRank = PressureRank(spazPhase);
            // Equal urgency favors Spazmatism: it is also the deliberate fire
            // target and its P2 flame is the encounter's limiting state.
            return spazRank >= retRank ? spaz : ret;
        }

        private static int PressureRank(NativePhase phase)
        {
            switch (phase)
            {
                case NativePhase.FirstLaunch:
                case NativePhase.SpazSecondLaunch: return 100;
                case NativePhase.FirstCharge:
                case NativePhase.SpazSecondCharge: return 95;
                case NativePhase.FirstBrake:
                case NativePhase.SpazSecondBrake: return 85;
                case NativePhase.SpazSecondFlame: return 80;
                case NativePhase.RetSecondSideLasers: return 65;
                case NativePhase.RetSecondOverheadLasers: return 60;
                case NativePhase.FirstHover: return 45;
                case NativePhase.TransformPending: return 35;
                case NativePhase.TransformSpinUp:
                case NativePhase.TransformSpinDown: return 25;
                default: return 110;
            }
        }

        private static bool IsLaunchOrCharge(NativePhase phase)
        {
            return phase == NativePhase.FirstLaunch || phase == NativePhase.FirstCharge ||
                   phase == NativePhase.SpazSecondLaunch || phase == NativePhase.SpazSecondCharge;
        }

        private static bool IsChargeBody(NativePhase phase)
        {
            return IsLaunchOrCharge(phase) || phase == NativePhase.FirstBrake || phase == NativePhase.SpazSecondBrake;
        }

        private static bool SafeTurnWindow(bool present, TargetSnapshot eye, NativePhase phase)
        {
            if (!present) return true;
            switch (phase)
            {
                case NativePhase.FirstHover:
                    // P1 switches at 600. Preserve eighty native ticks rather
                    // than turning through a dash whose launch vector is not yet
                    // in the snapshot.
                    return eye.Ai2 >= 0f && eye.Ai2 < 520f;
                case NativePhase.TransformPending:
                case NativePhase.TransformSpinUp:
                case NativePhase.TransformSpinDown:
                    return true;
                case NativePhase.RetSecondOverheadLasers:
                    return eye.Ai2 >= 0f && eye.Ai2 < 240f;
                case NativePhase.RetSecondSideLasers:
                    return eye.Ai2 >= 0f && eye.Ai2 < 120f;
                default:
                    return false;
            }
        }

        private static float StoppingTravel(PlayerSnapshot p, int direction)
        {
            var speed = Math.Max(0f, p.Velocity.X * direction);
            var slowdown = p.RunSlowdown * (p.OnGround ? 1f : .5f);
            return speed <= 0f ? 0f : speed * speed / (2f * Math.Max(.000001f, slowdown));
        }

        private static string EyeName(TargetSnapshot eye) => eye.Type == 126 ? "spaz" : "ret";

        private static string PhaseName(NativePhase phase)
        {
            switch (phase)
            {
                case NativePhase.FirstHover: return "first-hover-runway";
                case NativePhase.FirstLaunch: return "first-launch-committed-exit";
                case NativePhase.FirstCharge: return "first-charge-committed-exit";
                case NativePhase.FirstBrake: return "first-brake-hold-runway";
                case NativePhase.TransformPending: return "transform-pending-coast";
                case NativePhase.TransformSpinUp: return "transform-spin-up-coast";
                case NativePhase.TransformSpinDown: return "transform-spin-down-coast";
                case NativePhase.RetSecondOverheadLasers: return "second-overhead-laser-runway";
                case NativePhase.RetSecondSideLasers: return "second-side-laser-runway";
                case NativePhase.SpazSecondFlame: return "second-flame-spacing-runway";
                case NativePhase.SpazSecondLaunch: return "second-launch-committed-exit";
                case NativePhase.SpazSecondCharge: return "second-charge-committed-exit";
                case NativePhase.SpazSecondBrake: return "second-brake-hold-runway";
                default: return "unrecognized-native-state";
            }
        }
    }

    /// <summary>
    /// One source of truth for the currently reviewed Destroyer P1 fixture.
    /// A pre-spawn requirements check may omit the head; once any Destroyer part
    /// is observed, both requirements and the live controller demand the same
    /// unique, locally-targeting head with trusted native branch history.
    /// </summary>
    internal static class DestroyerP1Contract
    {
        public static bool IsSupported(CombatSnapshot snapshot, bool allowPreSpawn,
            out TargetSnapshot head, out string reason)
        {
            head = default(TargetSnapshot);
            if (snapshot == null || snapshot.Player == null || snapshot.Mobility == null ||
                snapshot.Arena == null || snapshot.Difficulty == null || snapshot.Targets == null)
            {
                reason = "毁灭者 P1 原生快照不完整";
                return false;
            }
            if (!ArenaObservationValid(snapshot.Arena))
            {
                reason = "毁灭者 P1 场地观测包含无效或非有限数值";
                return false;
            }
            if (!snapshot.NativeContextKnown || snapshot.LocalPlayerIndex < 0 || snapshot.LocalPlayerIndex >= 255)
            {
                reason = "无法确认原生网络模式与本地玩家索引";
                return false;
            }
            if (snapshot.NetMode != 0)
            {
                reason = "当前毁灭者策略只支持原版单机模式";
                return false;
            }
            if (!ClassicFixture(snapshot))
            {
                reason = "当前难度、世界种子或时间不在毁灭者 P1 已复核范围内";
                return false;
            }
            if (!KnownDemonFlight(snapshot))
            {
                reason = "毁灭者 P1 需要已复核的恶魔之翼与闪电靴原生机动配置";
                return false;
            }

            var partCount = 0;
            var headCount = 0;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                // Production capture keeps every valid active Boss root even
                // beyond the ordinary target radius. A second encounter must
                // never be hidden by distance or silently enter this single-
                // Destroyer baseline.
                if (candidate.Life > 0 && candidate.Boss &&
                    (candidate.Type < 134 || candidate.Type > 136))
                {
                    reason = "检测到其他 Boss；毁灭者 P1 只支持单 Boss 战";
                    return false;
                }
                if (candidate.Life <= 0 || candidate.Type < 134 || candidate.Type > 136) continue;
                partCount++;
                if (candidate.Type != 134) continue;
                headCount++;
                if (headCount == 1) head = candidate;
            }
            if (partCount == 0 && allowPreSpawn)
            {
                if (!CurrentContinuousSupport(snapshot))
                {
                    reason = "召唤毁灭者前必须站在真实、连续且足够宽的支撑面上";
                    return false;
                }
                reason = null;
                return true;
            }
            if (headCount != 1)
            {
                reason = "无法确认唯一的毁灭者头部及其原生目标";
                return false;
            }
            if (!ValidHead(head))
            {
                reason = "毁灭者头部原生状态无效";
                return false;
            }
            if (!head.NativeTargetKnown)
            {
                reason = "无法确认毁灭者头部的原生目标玩家";
                return false;
            }
            if (head.NativeTargetPlayerIndex != snapshot.LocalPlayerIndex)
            {
                reason = "毁灭者当前未以本地玩家为目标";
                return false;
            }
            if (!head.DestroyerBranchKnown)
            {
                reason = "毁灭者头部尚无连续可信的原生运动分支历史";
                return false;
            }
            reason = null;
            return true;
        }

        private static bool ClassicFixture(CombatSnapshot snapshot)
        {
            var d = snapshot.Difficulty;
            return d.GameModeKnown && d.GameMode == 0 && !d.Journey && !d.Expert && !d.Master &&
                !d.Drunk && !d.NotTheBees && !d.ForTheWorthy && !d.Remix && !d.Zenith &&
                !d.Celebration && !d.Constant && !d.NoTraps && !d.Skyblock && !d.DayTime;
        }

        private static bool KnownDemonFlight(CombatSnapshot snapshot)
        {
            var p = snapshot.Player;
            var f = p.Flight;
            var m = snapshot.Mobility;
            // This is the first reviewed low-gear baseline, not a ceiling on
            // future support. Faster wings, boots, mounts, hooks and dashes need
            // their own measured transition/return closures before admission.
            return p.FunctionalEquipmentIdentityKnown && p.WingAccessoryItemType == 492 &&
                p.RocketBootAccessoryItemType == 898 && f.Known && p.Jump.Known &&
                f.WingsLogic == 1 && f.RocketBoots == 2 &&
                f.WingTimeMax == 100 && f.RocketTimeMax == 7 && f.RocketDelay == 0 &&
                f.WingTime >= 0f && f.WingTime <= 142f && f.WingTime == (int)f.WingTime &&
                f.RocketTime >= 0 && f.RocketTime <= 7 && m.HasFiniteFlightResource &&
                // MountCanFly describes the best available mount even while no
                // mount is active. Possessing an optional escape tool must not
                // make the reviewed low-gear closure unusable; only an active
                // mount changes this native movement profile.
                !m.GravityInverted && !m.MountActive && !m.Grappling &&
                p.Jump.SlowFall == m.FeatherFall &&
                Finite(m.FlightResourceFraction) && m.FlightResourceFraction >= 0f &&
                m.FlightResourceFraction <= 1f && Finite(p.Gravity) && p.Gravity > 0f &&
                Finite(p.MaxFallSpeed) && p.MaxFallSpeed > 0f && Finite(p.BaseRunSpeed) &&
                p.BaseRunSpeed > 0f && Finite(p.MaxRunSpeed) && p.MaxRunSpeed >= p.BaseRunSpeed &&
                Finite(p.RunAcceleration) && p.RunAcceleration > 0f && Finite(p.RunSlowdown) &&
                p.RunSlowdown > 0f && Finite(p.Position.X) && Finite(p.Position.Y) &&
                Finite(p.Velocity.X) && Finite(p.Velocity.Y) && p.Width > 0 && p.Height > 0;
        }

        internal static bool ArenaObservationValid(ArenaSnapshot arena)
        {
            if (!Finite(arena.ClearanceLeft) || !Finite(arena.ClearanceRight) ||
                !Finite(arena.ClearanceUp) || !Finite(arena.ClearanceDown) ||
                arena.ClearanceLeft < 0f || arena.ClearanceRight < 0f ||
                arena.ClearanceUp < 0f || arena.ClearanceDown < 0f ||
                !Finite(arena.SafeCenter.X) || !Finite(arena.SafeCenter.Y) ||
                !Finite(arena.LocalOpenBounds.X) || !Finite(arena.LocalOpenBounds.Y) ||
                !Finite(arena.LocalOpenBounds.Width) || !Finite(arena.LocalOpenBounds.Height) ||
                arena.LocalOpenBounds.Width < 0f || arena.LocalOpenBounds.Height < 0f)
                return false;
            return ValidSupportObservation(arena.FloorSupport) &&
                ValidSupportObservation(arena.CeilingSupport) &&
                ValidSupportObservation(arena.RecoverySupport);
        }

        private static bool ValidSupportObservation(SupportSpan support)
        {
            return Finite(support.Left) && Finite(support.Right) && Finite(support.SurfaceY) &&
                (!support.Valid || support.Right > support.Left);
        }

        private static bool CurrentContinuousSupport(CombatSnapshot snapshot)
        {
            var p = snapshot.Player;
            var support = snapshot.Arena.FloorSupport;
            if (!p.OnGround || !support.Valid || support.Inverted ||
                !support.ContainsBody(p.Position.X, p.Width) ||
                Math.Abs(support.SurfaceY - (p.Position.Y + p.Height)) > 4f)
                return false;
            return SufficientContinuousSupport(support, p);
        }

        internal static bool SufficientContinuousSupport(SupportSpan support, PlayerSnapshot p)
        {
            if (p == null || !support.Valid || support.Inverted || !Finite(support.Left) ||
                !Finite(support.Right) || !Finite(support.SurfaceY) || support.Right <= support.Left)
                return false;
            var margin = AnchorMargin(p);
            return Finite(margin) && support.Right - support.Left >= p.Width + margin * 2f + 160f;
        }

        internal static float AnchorMargin(PlayerSnapshot p)
        {
            if (p == null || !Finite(p.MaxRunSpeed) || !Finite(p.RunSlowdown) ||
                p.MaxRunSpeed < 0f || p.RunSlowdown <= 0f) return float.PositiveInfinity;
            var maximum = p.MaxRunSpeed * p.MaxRunSpeed / (2f * p.RunSlowdown);
            return Math.Min(192f, Math.Max(64f, maximum + 48f));
        }

        private static bool ValidHead(TargetSnapshot head)
        {
            return head.Type == 134 && head.Key >= 0 && head.Life > 0 && head.LifeMax > 0 &&
                head.Life <= head.LifeMax && head.Width > 0 && head.Height > 0 &&
                Finite(head.Position.X) && Finite(head.Position.Y) && Finite(head.Velocity.X) &&
                Finite(head.Velocity.Y) && head.Velocity.LengthSquared <= 192f * 192f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal sealed class DestroyerStrategy : BossStrategyBase
    {
        private enum MovementState
        {
            AcquireAnchor,
            SupportedPressure,
            HeadExit,
            RecoverAnchor
        }

        private enum SafetyClosure
        {
            None,
            AnchorLost,
            RouteClosed
        }

        private const int ProbeClearTicks = 12;
        private const int ExitClearTicks = 8;
        private const int MinimumExitTicks = 12;
        private const int MaximumExitTicks = 48;
        private const int RouteHorizonTicks = 24;
        private const float LandingReserveTicks = 32f;
        private const float ResumeResourceFraction = .9f;
        private const float MinimumExitResourceFraction = .55f;
        private const float MaximumLiftPixels = 240f;
        private const int ObservationLossGraceTicks = 12;
        private const int IncompatibleContractConfirmTicks = 3;
        private const int ClosureReturnTicks = 90;
        private const int ProgressWatchdogTicks = 600;

        private MovementState _state;
        private SupportSpan _anchor;
        private float _returnLeft;
        private float _returnRight;
        private int _headKey = -1;
        private int _runDirection;
        private int _exitDirection;
        private int _exitTicks;
        private int _headClearTicks;
        private int _probeClearTicks;
        private bool _anchorValid;
        private bool _probePressure;
        private bool _mustGroundToReacquire;
        private int _contractLossTicks;
        private int _anchorLossTicks;
        private int _routeClosedTicks;
        private int _progressWatchdogTicks;
        private bool _watchdogPositionKnown;
        private Vec2 _watchdogPosition;

        public DestroyerStrategy() : base("destroyer", 1100, 500, 5f, true)
        {
            Requirements.RequiresDestroyerP1Contract = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 134, 136) && MechanicalFamilies.Count(b) == 1;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = default(TargetSnapshot);
            var headCount = 0;
            var probeCount = 0;
            var closeProbe = false;
            var player = s.Player.Center;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Life <= 0) continue;
                if (candidate.Type == 134)
                {
                    headCount++;
                    if (headCount == 1) head = candidate;
                }
                else if (candidate.Type == 139 && !candidate.Invulnerable && candidate.Chaseable)
                {
                    var distance = Vec2.DistanceSquared(candidate.Center, player);
                    if (distance < 900f * 900f) probeCount++;
                    if (distance < 320f * 320f) closeProbe = true;
                }
            }

            if (m.PreviousTargetKey < 0 || headCount == 1 && _headKey >= 0 && _headKey != head.Key)
                ResetRoute();
            if (headCount == 1) _headKey = head.Key;
            UpdateProbePressure(closeProbe || probeCount > 3);
            var target = SelectExposedTarget(s, m.PreviousTargetKey, _probePressure);
            TargetSnapshot contractHead;
            string contractReason;
            var contractSupported = DestroyerP1Contract.IsSupported(s, false, out contractHead, out contractReason);
            if (contractSupported) head = contractHead;

            // P1 is deliberately narrower than the strategy selector. Keeping the
            // strategy selected lets the controller expose an explicit stopped
            // state instead of falling through to a generic orbit for an unreviewed
            // difficulty, seed, mount, gravity or native movement profile.
            if (!contractSupported)
            {
                _mustGroundToReacquire = true;
                _anchorValid = false;
                _state = MovementState.AcquireAnchor;
                _contractLossTicks = SaturatingIncrement(_contractLossTicks);
                var transientObservation = headCount == 0 || headCount == 1 &&
                    (!head.NativeTargetKnown || !head.DestroyerBranchKnown);
                var decision = BuildDecision(s, target, headCount == 1 ? head : target,
                    headCount == 1 ? "unsupported-destroyer-fixture" : "unsupported-single-head",
                    0, 0, JumpAction.Release, false);
                var returnTicks = s.Arena == null || !DestroyerP1Contract.ArenaObservationValid(s.Arena) ? 1 :
                    transientObservation ? ObservationLossGraceTicks : IncompatibleContractConfirmTicks;
                if (_contractLossTicks >= returnTicks)
                    decision = RequestReturn(decision, "contract-lost: " + contractReason);
                return decision;
            }
            _contractLossTicks = 0;

            if (!_anchorValid)
            {
                var reacquiring = _mustGroundToReacquire;
                if (reacquiring && !s.Player.OnGround)
                {
                    _state = MovementState.AcquireAnchor;
                    return BuildDecision(s, target, head, "reacquire-anchor-airborne-closed",
                        0, 0, JumpAction.Release, CanFire(s, target), SafetyClosure.AnchorLost);
                }
                SupportSpan acquired;
                if (!TryAcquireAnchor(s, out acquired))
                {
                    _state = MovementState.AcquireAnchor;
                    return BuildDecision(s, target, head, reacquiring ?
                        "reacquire-anchor-no-safe-support" : "acquire-anchor-no-safe-support",
                        0, 0, JumpAction.Release, CanFire(s, target), SafetyClosure.AnchorLost);
                }

                SaveAnchor(s, acquired, head);
                if (IsOnAnchor(s) && InReturnZone(s.Player))
                    _state = ResourcesRecovered(s) ? MovementState.SupportedPressure : MovementState.RecoverAnchor;
                else
                    _state = MovementState.RecoverAnchor;
                return BuildDecision(s, target, head, reacquiring ? "reacquire-anchor" : "acquire-anchor",
                    0, 0, JumpAction.Release, CanFire(s, target));
            }

            if (!AnchorObserved(s))
            {
                // The old return surface is no longer observed. Discard it so
                // a later real landing can acquire a replacement, but never
                // reinterpret an airborne row as proof that this broken route
                // survived.
                _anchorValid = false;
                _mustGroundToReacquire = true;
                _state = MovementState.AcquireAnchor;
                if (!s.Player.OnGround)
                    return BuildDecision(s, target, head, "reacquire-anchor-airborne-closed",
                        0, 0, JumpAction.Release, CanFire(s, target), SafetyClosure.AnchorLost);
                SupportSpan replacement;
                if (!TryAcquireAnchor(s, out replacement))
                    return BuildDecision(s, target, head, "reacquire-anchor-no-safe-support",
                        0, 0, JumpAction.Release, CanFire(s, target), SafetyClosure.AnchorLost);
                SaveAnchor(s, replacement, head);
                _state = ResourcesRecovered(s) ? MovementState.SupportedPressure : MovementState.RecoverAnchor;
                return BuildDecision(s, target, head, "reacquire-anchor",
                    0, 0, JumpAction.Release, CanFire(s, target));
            }

            switch (_state)
            {
                case MovementState.SupportedPressure:
                    return EvaluateSupported(s, target, head);
                case MovementState.HeadExit:
                    return EvaluateHeadExit(s, target, head);
                case MovementState.RecoverAnchor:
                    return EvaluateRecovery(s, target, head);
                default:
                    _state = MovementState.RecoverAnchor;
                    return BuildDecision(s, target, head, "recover-anchor-invalid-state",
                        0, 0, JumpAction.Release, CanFire(s, target), SafetyClosure.AnchorLost);
            }
        }

        private BossDecision EvaluateSupported(CombatSnapshot s, TargetSnapshot target, TargetSnapshot head)
        {
            if (!IsOnAnchor(s) || !InReturnZone(s.Player))
            {
                _state = MovementState.RecoverAnchor;
                return EvaluateRecovery(s, target, head);
            }

            float crossingX;
            var pressure = HeadOrChainPressure(s, head, out crossingX);
            if (pressure)
            {
                _state = MovementState.HeadExit;
                _exitTicks = 0;
                _headClearTicks = 0;
                _exitDirection = SelectExitDirection(s, head, crossingX);
                return EvaluateHeadExit(s, target, head);
            }

            var horizontal = SupportedHorizontal(s, head);
            var phase = _probePressure ? "supported-probe-pressure" :
                horizontal == 0 ? "supported-pressure-brake" : "supported-pressure";
            return BuildDecision(s, target, head, phase, horizontal, 0,
                JumpAction.Release, CanFire(s, target), horizontal == 0 ?
                    SafetyClosure.RouteClosed : SafetyClosure.None);
        }

        private BossDecision EvaluateHeadExit(CombatSnapshot s, TargetSnapshot target, TargetSnapshot head)
        {
            _exitTicks++;
            float crossingX;
            if (HeadOrChainPressure(s, head, out crossingX)) _headClearTicks = 0;
            else _headClearTicks++;

            var remaining = FlightMotion.RemainingWingTicks(in s.Player.Flight);
            if (_exitTicks >= MaximumExitTicks || !s.Player.OnGround && remaining <= LandingReserveTicks ||
                _exitTicks >= MinimumExitTicks && _headClearTicks >= ExitClearTicks)
            {
                _state = MovementState.RecoverAnchor;
                return EvaluateRecovery(s, target, head);
            }

            if (_exitDirection == 0)
                _exitDirection = SelectExitDirection(s, head, crossingX);
            var horizontal = _exitDirection;
            // A newly observed obstacle may stop the committed side, but it must
            // never make the controller reverse through the head on this tick.
            if (horizontal != 0 && !RouteClear(s, head, horizontal, true, false)) horizontal = 0;

            var vertical = 0;
            var jump = JumpAction.Release;
            var resource = NativeResourceFraction(s);
            var enoughToLeave = resource >= MinimumExitResourceFraction &&
                remaining > LandingReserveTicks + 18f;
            if (horizontal != 0 && enoughToLeave && _headClearTicks == 0)
            {
                if (s.Player.OnGround)
                {
                    vertical = 1;
                    jump = JumpAction.Default;
                }
                else if (s.Player.Position.Y + s.Player.Height > _anchor.SurfaceY - MaximumLiftPixels)
                {
                    vertical = 1;
                    jump = JumpAction.Hold;
                }
            }

            return BuildDecision(s, target, head,
                horizontal == 0 ? "head-exit-committed-blocked" : "head-exit-committed",
                horizontal, vertical, jump, CanFire(s, target), horizontal == 0 ?
                    SafetyClosure.RouteClosed : SafetyClosure.None);
        }

        private BossDecision EvaluateRecovery(CombatSnapshot s, TargetSnapshot target, TargetSnapshot head)
        {
            if (IsOnAnchor(s) && InReturnZone(s.Player) && ResourcesRecovered(s))
            {
                _state = MovementState.SupportedPressure;
                _runDirection = ChooseRunDirection(s, head);
                return BuildDecision(s, target, head, "recover-anchor-complete",
                    0, 0, JumpAction.Release, CanFire(s, target));
            }

            var horizontal = RecoveryHorizontal(s.Player);
            if (horizontal != 0 && !RouteClear(s, head, horizontal, false, true))
                horizontal = 0;

            var vertical = 0;
            var jump = JumpAction.Release;
            if (!s.Player.OnGround && horizontal != 0 && ShouldHoldForReturn(s))
            {
                vertical = 1;
                jump = JumpAction.Hold;
            }
            var phase = horizontal == 0 && !InReturnZone(s.Player) ? "recover-anchor-route-blocked" :
                IsOnAnchor(s) ? "recover-anchor-recharge" : "recover-anchor";
            return BuildDecision(s, target, head, phase, horizontal, vertical, jump, CanFire(s, target),
                horizontal == 0 && !InReturnZone(s.Player) ? SafetyClosure.RouteClosed : SafetyClosure.None);
        }

        private BossDecision BuildDecision(CombatSnapshot s, TargetSnapshot target, TargetSnapshot head,
            string phase, int horizontal, int vertical, JumpAction jump, bool fire,
            SafetyClosure closure = SafetyClosure.None)
        {
            var decision = Decision(s, target, phase, BossPattern.Runway, 420f, 0f,
                horizontal, vertical, false, false, false, 72f, fire, head);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.OwnsMovementClosure = true;
            decision.Directive.JumpAction = jump;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            decision.Directive.VerticalOffset = 0f;
            decision.Directive.FloorClearance = 0f;
            if (closure == SafetyClosure.AnchorLost)
            {
                _anchorLossTicks = SaturatingIncrement(_anchorLossTicks);
                _routeClosedTicks = 0;
            }
            else if (closure == SafetyClosure.RouteClosed)
            {
                _routeClosedTicks = SaturatingIncrement(_routeClosedTicks);
                _anchorLossTicks = 0;
            }
            else
            {
                _anchorLossTicks = 0;
                _routeClosedTicks = 0;
            }

            UpdateProgressWatchdog(s.Player);
            if (_anchorLossTicks >= ClosureReturnTicks)
                return RequestReturn(decision, "anchor-lost: 连续支点无法在安全窗口内重新确认");
            if (_routeClosedTicks >= ClosureReturnTicks)
                return RequestReturn(decision, "route-closed: 已复核路线持续没有安全出口");
            if (_progressWatchdogTicks >= ProgressWatchdogTicks)
                return RequestReturn(decision, "watchdog: 毁灭者闭环长时间没有位置进展");
            return decision;
        }

        private void UpdateProgressWatchdog(PlayerSnapshot player)
        {
            if (!_watchdogPositionKnown)
            {
                _watchdogPosition = player.Position;
                _watchdogPositionKnown = true;
                _progressWatchdogTicks = 0;
                return;
            }
            if (Vec2.DistanceSquared(player.Position, _watchdogPosition) >= 16f * 16f)
            {
                _watchdogPosition = player.Position;
                _progressWatchdogTicks = 0;
            }
            else _progressWatchdogTicks = SaturatingIncrement(_progressWatchdogTicks);
        }

        private static BossDecision RequestReturn(BossDecision decision, string reason)
        {
            var directive = decision.Directive;
            directive.RequestControlReturn = true;
            directive.ControlReturnReason = reason;
            directive.HorizontalIntent = 0;
            directive.VerticalIntent = 0;
            directive.JumpAction = JumpAction.Release;
            directive.Fire = false;
            directive.ForceContinuousMovement = false;
            directive.OwnsMovementClosure = true;
            decision.Directive = directive;
            return decision;
        }

        private static int SaturatingIncrement(int value)
        {
            return value == int.MaxValue ? value : value + 1;
        }

        private void ResetRoute()
        {
            _state = MovementState.AcquireAnchor;
            _anchor = default(SupportSpan);
            _returnLeft = _returnRight = 0f;
            _headKey = -1;
            _runDirection = _exitDirection = 0;
            _exitTicks = _headClearTicks = _probeClearTicks = 0;
            _anchorValid = false;
            _probePressure = false;
            _mustGroundToReacquire = false;
            _contractLossTicks = _anchorLossTicks = _routeClosedTicks = _progressWatchdogTicks = 0;
            _watchdogPositionKnown = false;
            _watchdogPosition = default(Vec2);
        }

        private void UpdateProbePressure(bool immediate)
        {
            if (immediate)
            {
                _probePressure = true;
                _probeClearTicks = 0;
            }
            else if (_probePressure && ++_probeClearTicks >= ProbeClearTicks)
            {
                _probePressure = false;
                _probeClearTicks = 0;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool TryAcquireAnchor(CombatSnapshot s, out SupportSpan support)
        {
            support = default(SupportSpan);
            var p = s.Player;
            var foot = p.Position.Y + p.Height;
            var floor = s.Arena.FloorSupport;
            if (p.OnGround && AtFeet(floor, p, foot) && SufficientSupport(floor, p))
            {
                support = floor;
                return true;
            }
            var recovery = s.Arena.RecoverySupport;
            if (p.OnGround && AtFeet(recovery, p, foot) && SufficientSupport(recovery, p))
            {
                support = recovery;
                return true;
            }
            if (p.OnGround || !LandingCandidate(recovery, p) || !SufficientSupport(recovery, p)) return false;

            var margin = AnchorMargin(p);
            var left = recovery.Left + margin;
            var right = recovery.Right - p.Width - margin;
            if (left > right) return false;
            var horizontal = p.Position.X < left ? left - p.Position.X : p.Position.X > right ? p.Position.X - right : 0f;
            var available = Math.Max(0f, FlightMotion.RemainingWingTicks(in p.Flight) - LandingReserveTicks);
            if (horizontal > available * Math.Max(1f, p.MaxRunSpeed)) return false;
            support = recovery;
            return true;
        }

        private void SaveAnchor(CombatSnapshot s, SupportSpan support, TargetSnapshot head)
        {
            _anchor = support;
            var margin = AnchorMargin(s.Player);
            _returnLeft = support.Left + margin;
            _returnRight = support.Right - s.Player.Width - margin;
            if (_returnLeft > _returnRight)
                _returnLeft = _returnRight = (support.Left + support.Right - s.Player.Width) * .5f;
            _anchorValid = true;
            _mustGroundToReacquire = false;
            _runDirection = ChooseRunDirection(s, head);
            _exitDirection = 0;
            _exitTicks = _headClearTicks = 0;
        }

        private static float AnchorMargin(PlayerSnapshot p)
        {
            return DestroyerP1Contract.AnchorMargin(p);
        }

        private static bool SufficientSupport(SupportSpan support, PlayerSnapshot p)
        {
            return DestroyerP1Contract.SufficientContinuousSupport(support, p);
        }

        private static bool AtFeet(SupportSpan support, PlayerSnapshot p, float foot)
        {
            return support.Valid && !support.Inverted && support.ContainsBody(p.Position.X, p.Width) &&
                Math.Abs(support.SurfaceY - foot) <= 4f;
        }

        private static bool LandingCandidate(SupportSpan support, PlayerSnapshot p)
        {
            var foot = p.Position.Y + p.Height;
            return support.Valid && !support.Inverted && support.SurfaceY >= foot - 4f &&
                support.SurfaceY - foot <= 480f;
        }

        private bool AnchorObserved(CombatSnapshot s)
        {
            return SameAnchorRow(s.Arena.FloorSupport) || SameAnchorRow(s.Arena.RecoverySupport);
        }

        private bool SameAnchorRow(SupportSpan observed)
        {
            if (!observed.Valid || observed.Inverted || Math.Abs(observed.SurfaceY - _anchor.SurfaceY) > 2f)
                return false;
            // A long support row may be returned as overlapping bounded windows.
            // It still has to cover some of the saved landing zone; equal height
            // somewhere else is not evidence that the old anchor survived.
            return observed.Left <= _returnRight && observed.Right >= _returnLeft;
        }

        private bool IsOnAnchor(CombatSnapshot s)
        {
            var p = s.Player;
            return p.OnGround && _anchor.ContainsBody(p.Position.X, p.Width) &&
                Math.Abs(p.Position.Y + p.Height - _anchor.SurfaceY) <= 4f;
        }

        private bool InReturnZone(PlayerSnapshot p) => p.Position.X >= _returnLeft && p.Position.X <= _returnRight;

        private static float NativeResourceFraction(CombatSnapshot s)
        {
            return Math.Min(s.Mobility.FlightResourceFraction, FlightMotion.ResourceFraction(in s.Player.Flight));
        }

        private bool ResourcesRecovered(CombatSnapshot s)
        {
            return IsOnAnchor(s) && NativeResourceFraction(s) >= ResumeResourceFraction;
        }

        private int ChooseRunDirection(CombatSnapshot s, TargetSnapshot head)
        {
            var left = s.Player.Position.X - _returnLeft;
            var right = _returnRight - s.Player.Position.X;
            if (right > left + 24f) return 1;
            if (left > right + 24f) return -1;
            return s.Player.Center.X >= head.Center.X ? 1 : -1;
        }

        private int SupportedHorizontal(CombatSnapshot s, TargetSnapshot head)
        {
            if (_runDirection == 0) _runDirection = ChooseRunDirection(s, head);
            var p = s.Player;
            var lane = _runDirection > 0 ? _returnRight - p.Position.X : p.Position.X - _returnLeft;
            var other = _runDirection > 0 ? p.Position.X - _returnLeft : _returnRight - p.Position.X;
            var braking = StoppingTravel(p, _runDirection);
            if (lane <= braking + 32f)
            {
                var reverse = -_runDirection;
                if (other > lane + 24f && RouteClear(s, head, reverse, true, true))
                {
                    _runDirection = reverse;
                    return reverse;
                }
                return 0;
            }
            if (!RouteClear(s, head, _runDirection, true, true)) return 0;
            return _runDirection;
        }

        private static float StoppingTravel(PlayerSnapshot p, int direction)
        {
            var speed = Math.Max(0f, p.Velocity.X * direction);
            var slowdown = p.RunSlowdown * (p.OnGround ? 1f : .5f);
            return speed <= 0f ? 0f : speed * speed / (2f * Math.Max(.000001f, slowdown));
        }

        private int RecoveryHorizontal(PlayerSnapshot p)
        {
            var projected = p.Position.X + p.Velocity.X * 8f;
            if (projected < _returnLeft) return 1;
            if (projected > _returnRight) return -1;
            var moving = p.Velocity.X > 0f ? 1 : p.Velocity.X < 0f ? -1 : 0;
            if (moving != 0)
            {
                var lane = moving > 0 ? _returnRight - p.Position.X : p.Position.X - _returnLeft;
                if (lane < StoppingTravel(p, moving) + 24f) return -moving;
            }
            return 0;
        }

        private int SelectExitDirection(CombatSnapshot s, TargetSnapshot head, float crossingX)
        {
            var preferred = s.Player.Center.X >= crossingX ? 1 : -1;
            if (RouteClear(s, head, preferred, true, false)) return preferred;
            var other = -preferred;
            return RouteClear(s, head, other, true, false) ? other : 0;
        }

        private bool RouteClear(CombatSnapshot s, TargetSnapshot head, int direction,
            bool requireAnchorFootprint, bool includeHead)
        {
            if (direction == 0) return false;
            var p = s.Player;
            var x = p.Position.X;
            var vx = p.Velocity.X;
            var startDistance = DistanceToReturnZone(x);
            for (var tick = 1; tick <= RouteHorizonTicks; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, direction, p.OnGround, 1, out travel);
                x += travel;
                if (requireAnchorFootprint && !_anchor.ContainsBody(x, p.Width)) return false;
                if (!requireAnchorFootprint && DistanceToReturnZone(x) > startDistance + 2f) return false;
                var body = p.BoundsAt(new Vec2(x, p.Position.Y)).Inflated(20f);
                for (var i = 0; i < s.Threats.Count; i++)
                {
                    var threat = s.Threats[i];
                    // Preserve the final damaging update at timeLeft == tick.
                    if (threat.Damage <= 0 || threat.TimeLeft > 0 &&
                        threat.TimeLeft < tick) continue;
                    if (threat.Geometry != ThreatGeometry.Body)
                    {
                        if (BeamGeometry.Intersects(body, BeamGeometry.Sweep(threat, 0, tick), 20f)) return false;
                    }
                    else
                    {
                        var margin = threat.Kind == ThreatKind.NpcContact ? 44f : 20f;
                        if (threat.Trajectory != ThreatTrajectory.Linear)
                        {
                            ProjectileMotionSample sample;
                            if (!HostileProjectileMotion.TrySample(threat,
                                    tick, out sample)) return false;
                            if (sample.Active && body.Intersects(
                                    sample.Bounds.Inflated(margin)))
                                return false;
                        }
                        else if (body.Intersects(threat.BoundsAt(tick)
                                .Inflated(margin))) return false;
                    }
                }
                if (includeHead)
                {
                    var future = head.Position + head.Velocity * tick;
                    // This is a bounded uncertainty policy around the observed
                    // velocity, not an exact replay of AI_037. The remembered
                    // worm branch receives a materially wider allowance for its
                    // stronger per-axis turning; either branch can still change
                    // after this snapshot, so both retain a quadratic allowance.
                    var uncertainty = HeadBranchUncertainty(head, tick);
                    var headBody = new RectF(future.X, future.Y, head.Width, head.Height)
                        .Inflated(72f + uncertainty);
                    if (body.Intersects(headBody)) return false;
                }
            }
            return true;
        }

        private float DistanceToReturnZone(float x)
        {
            return x < _returnLeft ? _returnLeft - x : x > _returnRight ? x - _returnRight : 0f;
        }

        private static bool HeadOrChainPressure(CombatSnapshot s, TargetSnapshot head, out float crossingX)
        {
            var player = s.Player.Center;
            crossingX = head.Center.X;
            var separation = head.Center - player;
            var relative = head.Velocity - s.Player.Velocity;
            var speedSquared = relative.LengthSquared;
            if (speedSquared > .01f)
            {
                var ticks = Math.Max(0f, Math.Min(RouteHorizonTicks,
                    -Vec2.Dot(separation, relative) / speedSquared));
                var closest = separation + relative * ticks;
                crossingX = head.Center.X + head.Velocity.X * ticks;
                var uncertainty = HeadBranchUncertainty(head, ticks);
                if (ticks > 0f && Math.Abs(closest.X) < 300f + uncertainty &&
                    Math.Abs(closest.Y) < 260f + uncertainty)
                    return true;
            }
            var immediateUncertainty = HeadBranchUncertainty(head, 0f);
            if (Math.Abs(separation.X) < 240f + immediateUncertainty &&
                Math.Abs(separation.Y) < 220f + immediateUncertainty) return true;

            var guard = new RectF(s.Player.Position.X - 150f, s.Player.Position.Y - 150f,
                s.Player.Width + 300f, s.Player.Height + 300f);
            for (var i = 0; i < s.Threats.Count; i++)
            {
                var threat = s.Threats[i];
                if (threat.Damage <= 0 || threat.Geometry != ThreatGeometry.Body ||
                    threat.Type < 134 || threat.Type > 136) continue;
                for (var tick = 0; tick <= RouteHorizonTicks; tick += 4)
                {
                    if (threat.TimeLeft > 0 && threat.TimeLeft <= tick) break;
                    var bounds = threat.BoundsAt(tick).Inflated(56f);
                    if (!guard.Intersects(bounds)) continue;
                    crossingX = bounds.Center.X;
                    return true;
                }
            }
            return false;
        }

        private static float HeadBranchUncertainty(TargetSnapshot head, float ticks)
        {
            ticks = Math.Max(0f, Math.Min(RouteHorizonTicks, ticks));
            // Classic native air steering changes velocity by roughly 0.1/0.15
            // per axis. Worm movement can turn more aggressively. These larger
            // policy constants cover rounding and unobserved within-window branch
            // changes without claiming to reconstruct the native target/tile scan.
            // Use the larger reviewed per-axis policy bound for both: the next
            // native update can change branch after our snapshot. The remembered
            // worm branch adds an initial uncertainty reserve, but an observed air
            // branch is never granted the smaller native-air acceleration alone.
            const float acceleration = .40f;
            var branchAllowance = head.DestroyerUsesWormMovement ? 56f : 16f;
            return branchAllowance + acceleration * ticks * (ticks + 1f) * .5f;
        }

        private bool ShouldHoldForReturn(CombatSnapshot s)
        {
            var p = s.Player;
            var foot = p.Position.Y + p.Height;
            if (foot >= _anchor.SurfaceY - 4f || p.Velocity.Y < 0f) return false;
            var remaining = FlightMotion.RemainingWingTicks(in p.Flight);
            if (remaining <= LandingReserveTicks + 6f) return false;
            var distance = DistanceToReturnZone(p.Position.X);
            var horizontalTicks = distance / Math.Max(1f, p.MaxRunSpeed) +
                Math.Abs(p.Velocity.X) / Math.Max(.000001f, p.RunAcceleration + p.RunSlowdown) + 6f;
            return FallingTicks(p, _anchor.SurfaceY - foot) <= horizontalTicks;
        }

        private static int FallingTicks(PlayerSnapshot p, float distance)
        {
            if (distance <= 0f) return 0;
            var y = 0f;
            var velocity = p.Velocity.Y;
            for (var tick = 1; tick <= 120; tick++)
            {
                velocity = Math.Min(p.MaxFallSpeed, velocity + p.Gravity);
                y += velocity;
                if (y >= distance) return tick;
            }
            return 121;
        }

        private static TargetSnapshot SelectExposedTarget(CombatSnapshot snapshot, int previousTarget, bool probePressure)
        {
            var selected = default(TargetSnapshot);
            var bestRank = int.MaxValue;
            var bestScore = float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                var distance = Vec2.DistanceSquared(candidate.Center, snapshot.Player.Center);
                var destroyerTarget = candidate.Type == 134 || candidate.Type == 135 ||
                    candidate.Type == 136 || candidate.Type == 139;
                // Ordinary enemies are eligible only as an immediate, verified
                // contact-lane obstruction. This covers the observed Werewolf
                // failure without turning arbitrary off-screen enemies into a
                // replacement for Destroyer damage.
                var closeHostile = !candidate.Boss && !destroyerTarget && candidate.Life > 0 &&
                    candidate.Damage > 0 && candidate.Chaseable && !candidate.Invulnerable &&
                    candidate.LineOfSightKnown && candidate.HasLineOfSight && distance <= 320f * 320f;
                if (candidate.Life <= 0 || !destroyerTarget && !closeHostile) continue;
                if (candidate.Type == 139 && distance > 900f * 900f) continue;

                // Unknown is not blocked. The adapter supplies a bounded LOS cache,
                // and still performs its native final ray before actually firing.
                var rank = candidate.LineOfSightKnown ? candidate.HasLineOfSight ? 0 : 2 : 1;
                if (candidate.Invulnerable) rank += 6;
                if (!candidate.Chaseable) rank += 3;
                if (closeHostile) rank -= 2;
                var score = distance;
                if (candidate.Type == 139)
                    score += probePressure ? -600f * 600f : 300f * 300f;
                // Keep a viable target while neighboring worm segments exchange
                // nearest-distance order; never retain a blocked target over clear.
                if (candidate.Key == previousTarget) score -= 100f * 100f;
                if (rank < bestRank || rank == bestRank && score < bestScore)
                {
                    selected = candidate;
                    bestRank = rank;
                    bestScore = score;
                }
            }
            return bestRank == int.MaxValue ? Pick(snapshot, 134, 135, 136) : selected;
        }

        private static bool CanFire(CombatSnapshot s, TargetSnapshot target)
        {
            if (target.Life <= 0 || target.Invulnerable || !target.Chaseable ||
                !target.LineOfSightKnown || !target.HasLineOfSight) return false;
            if (s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported) return true;
            var range = s.Weapon.Profile.ConservativeRangePixels;
            return range > 0f && Vec2.DistanceSquared(target.Center, s.Player.Center) <= range * range;
        }
    }

    internal sealed class MechanicalMayhemStrategy : BossStrategyBase
    {
        public MechanicalMayhemStrategy() : base("mechanical-mayhem", 1800, 700, 7f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => MechanicalFamilies.Count(b) >= 2 && !d.Zenith;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = MechanicalPriority(s);
            return Decision(s, target, "three-boss-kite", BossPattern.Composite, 720, -180,
                CompositeEscape(s, m), 0, true, true, s.Mobility.CanFlipGravity, 95);
        }
    }

    internal sealed class MechdusaStrategy : BossStrategyBase
    {
        public MechdusaStrategy() : base("mechdusa", 1900, 760, 7f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => d.Zenith && MechanicalFamilies.Count(b) >= 2;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = MechanicalPriority(s);
            return Decision(s, target, "attached-stack-kite", BossPattern.Runway, 760, -220,
                AwayX(s.Player, target), 0, true, true, true, 110);
        }
    }

    internal sealed class PlanteraStrategy : BossStrategyBase
    {
        public PlanteraStrategy() : base("plantera", 850, 650, 5f, true)
        {
            Requirements.RequiresPlanteraOrdinaryContract = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 262);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var plantera = Pick(s, 262);
            string reason;
            bool second;
            if (!TryValidateNativeEncounter(s, plantera, out second,
                    out reason))
                return NativeContractLost(s, plantera, reason, plantera);

            var target = SelectDamageTarget(s, m, plantera, second);
            int horizontal;
            int vertical;
            ClassicSecondaryBossContract.OrbitIntent(s, plantera, m,
                second ? 260f : 350f, second ? 430f : 560f,
                out horizontal, out vertical);
            string phase;
            if (!second)
                phase = HasThreat(s, 275, 276) || HasThreat(s, 277) ?
                    "phase-1-seed-volley-live" :
                    plantera.LocalAi1 >= 68f ?
                    "phase-1-seed-volley-imminent" :
                    "phase-1-wide-orbit";
            else if (plantera.LocalAi0 == 1f)
                phase = "phase-2-tentacle-release-transition";
            else
                phase = plantera.LocalAi1 >= 315f ?
                    "phase-2-spore-release-imminent" :
                    "phase-2-tight-orbit";
            var close = Vec2.DistanceSquared(plantera.Center,
                s.Player.Center) < (second ? 260f * 260f : 210f * 210f);
            var fire = target.Life > 0 && !target.Invulnerable &&
                target.Chaseable &&
                ClassicSecondaryBossContract.IsVisibleForNativeFinalRay(target) &&
                ClassicSecondaryBossContract.IsWithinReviewedWeaponRange(s,
                    target);
            var decision = Decision(s, target, phase, BossPattern.CircleOrbit,
                second ? 350f : 450f, 0f, horizontal, vertical, close,
                false, false, second ? 88f : 58f, fire, plantera);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0 ||
                vertical != 0;
            return decision;
        }

        private static bool TryValidateNativeEncounter(CombatSnapshot s,
            TargetSnapshot plantera, out bool second, out string reason)
        {
            second = false;
            TargetSnapshot validatedRoot;
            if (!PlanteraOrdinaryContract.IsSupported(s, false,
                    out validatedRoot, out second, out reason)) return false;
            if (validatedRoot.Key != plantera.Key)
            {
                reason = "Plantera strategy target does not match the validated native root";
                return false;
            }
            return true;
        }

        private static TargetSnapshot SelectDamageTarget(CombatSnapshot s,
            BossMemory memory, TargetSnapshot plantera, bool second)
        {
            var selected = plantera;
            var bestRank = 3;
            var bestScore = Vec2.DistanceSquared(plantera.Center,
                s.Player.Center);
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Life <= 0 || candidate.Invulnerable ||
                    !candidate.Chaseable || candidate.LineOfSightKnown &&
                    !candidate.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(candidate.Center,
                    s.Player.Center);
                var rank = candidate.Type == 265 &&
                    distance < 260f * 260f ? 0 :
                    second && candidate.Type == 264 ? 1 :
                    candidate.Type == 262 ? 2 : int.MaxValue;
                if (rank == int.MaxValue) continue;
                if (candidate.Key == memory.PreviousTargetKey)
                    distance -= 80f * 80f;
                if (rank > bestRank || rank == bestRank &&
                    distance >= bestScore) continue;
                bestRank = rank;
                bestScore = distance;
                selected = candidate;
            }
            return selected;
        }
    }

    internal sealed class GolemStrategy : BossStrategyBase
    {
        public GolemStrategy() : base("golem", 680, 380, 4.5f)
        {
            Requirements.RequiresGolemOrdinaryContract = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 245, 249);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var body = Pick(s, 245);
            string reason;
            bool detached;
            TargetSnapshot threateningFist;
            bool hasThreateningFist;
            if (!TryValidateNativeEncounter(s, body, out detached,
                    out threateningFist, out hasThreateningFist, out reason))
                return NativeContractLost(s, body, reason, body);

            var target = SelectDamageTarget(s, m, body);
            var horizontal = ClassicSecondaryBossContract.StableHorizontal(s,
                body, m, 170f);
            var vertical = 0;
            var fistLaunch = hasThreateningFist && threateningFist.Ai0 == 2f;
            if (fistLaunch)
            {
                ChargeEscape(s, threateningFist, threateningFist.Velocity,
                    out horizontal, out vertical);
                if (horizontal == 0)
                    horizontal = ClassicSecondaryBossContract.StableHorizontal(
                        s, body, m, 170f);
            }
            else if (body.Ai0 == 1f)
                horizontal = AwayX(s.Player, body);
            string phase;
            if (fistLaunch)
                phase = "fist-launched-cross-charge-line";
            else if (hasThreateningFist && threateningFist.Ai0 == 1f)
                phase = "fist-30-tick-launch-telegraph";
            else if (body.Ai0 == 1f)
                phase = "body-airborne-jump";
            else if (HasThreat(s, 258, 259))
                phase = detached ? "detached-head-projectiles-live" :
                    "attached-head-projectiles-live";
            else
                phase = detached ? "detached-head-horizontal-cycle" :
                    "attached-head-and-fists-cycle";
            var fire = target.Life > 0 && !target.Invulnerable &&
                target.Chaseable &&
                ClassicSecondaryBossContract.IsVisibleForNativeFinalRay(target) &&
                ClassicSecondaryBossContract.IsWithinReviewedWeaponRange(s,
                    target);
            var decision = Decision(s, target, phase,
                fistLaunch ? BossPattern.PerpendicularDashDodge :
                BossPattern.HorizontalKite, detached ? 470f : 350f, -70f,
                horizontal, vertical, false, false, false,
                fistLaunch ? 92f : detached ? 66f : 50f, fire,
                fistLaunch ? threateningFist : body);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0 ||
                vertical != 0;
            return decision;
        }

        private static bool TryValidateNativeEncounter(CombatSnapshot s,
            TargetSnapshot body, out bool detached,
            out TargetSnapshot threateningFist,
            out bool hasThreateningFist, out string reason)
        {
            detached = false;
            threateningFist = default(TargetSnapshot);
            hasThreateningFist = false;
            TargetSnapshot validatedBody;
            if (!GolemOrdinaryContract.IsSupported(s, false, out validatedBody,
                    out detached, out threateningFist, out hasThreateningFist,
                    out reason)) return false;
            if (validatedBody.Key != body.Key)
            {
                reason = "Golem strategy target does not match the validated native body";
                return false;
            }
            return true;
        }

        private static TargetSnapshot SelectDamageTarget(CombatSnapshot s,
            BossMemory memory, TargetSnapshot body)
        {
            var selected = body;
            var found = !body.Invulnerable && body.Chaseable;
            var bestRank = found ? 2 : int.MaxValue;
            var bestScore = found ? Vec2.DistanceSquared(body.Center,
                s.Player.Center) : float.MaxValue;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Life <= 0 || candidate.Invulnerable ||
                    !candidate.Chaseable || candidate.LineOfSightKnown &&
                    !candidate.HasLineOfSight) continue;
                var rank = candidate.Type == 247 || candidate.Type == 248 ?
                    0 : candidate.Type == 246 ? 1 :
                    candidate.Type == 245 ? 2 : int.MaxValue;
                if (rank == int.MaxValue) continue;
                var score = Vec2.DistanceSquared(candidate.Center,
                    s.Player.Center);
                if (candidate.Key == memory.PreviousTargetKey)
                    score -= 80f * 80f;
                if (found && (rank > bestRank || rank == bestRank &&
                    score >= bestScore)) continue;
                found = true;
                bestRank = rank;
                bestScore = score;
                selected = candidate;
            }
            return selected;
        }
    }

    internal sealed class FishronStrategy : BossStrategyBase
    {
        public FishronStrategy() : base("duke-fishron", 1500, 700, 6.75f, true)
        {
            // Expert/master demand a certified burst + braking + return
            // capability. Shield of Cthulhu is currently the only implemented
            // source certificate; it is not the conceptual equipment goal.
            var expert = Requirements.ExpertMobility;
            expert.Dash = BossDashBaseline.ShieldOfCthulhu;
            Requirements.ExpertMobility = expert;
            var expertThreshold = Requirements.ExpertMobilityThreshold;
            expertThreshold.Burst =
                BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn;
            Requirements.ExpertMobilityThreshold = expertThreshold;
            Requirements.ExpertMobilityRoutes = SingleMobilityRoute(Id,
                "expert", in expert);
            var master = Requirements.MasterMobility;
            master.Dash = BossDashBaseline.ShieldOfCthulhu;
            Requirements.MasterMobility = master;
            var masterThreshold = Requirements.MasterMobilityThreshold;
            masterThreshold.Burst =
                BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn;
            Requirements.MasterMobilityThreshold = masterThreshold;
            Requirements.MasterMobilityRoutes = SingleMobilityRoute(Id,
                "master", in master);
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 370);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 370);
            var nativeEnraged = false;
            if (s.NativeContextKnown)
            {
                DukeFishronNativeEnrageObservation nativeEnrage;
                if (!t.Ai0Known || !t.Ai2Known || !t.Ai3Known ||
                    !TryGetNativeEnrage(s, t.Key, out nativeEnrage))
                    return UnknownNativeState(s, t,
                        "missing-native-enrage-or-ai");
                nativeEnraged = nativeEnrage.NativeEnraged;
            }
            var state = NativeIntegerState(t.Ai0, -1, 12);
            var tick = NativeNonnegativeInteger(t.Ai2);
            var sequence = NativeNonnegativeInteger(t.Ai3);
            if (state == int.MinValue || tick == int.MinValue ||
                sequence == int.MinValue || !ValidNativePhase(s, state,
                    tick, sequence, nativeEnraged))
                return UnknownNativeState(s, t,
                    "impossible-native-state-clock-sequence");
            if (m.FishronTargetKey != t.Key ||
                m.PreviousTargetKey != t.Key)
            {
                m.ResetFishron();
                m.FishronTargetKey = t.Key;
                m.FishronOrbitDirection = ChooseOrbitDirection(s, t);
            }
            string threatHoldReason;
            if (PriorityBossThreatGate.TryGetNeutralHoldReason(s,
                    PriorityBossThreatGate.DukeFishronType,
                    out threatHoldReason))
                return UnmodeledThreatSafetyHold(s, t, threatHoldReason);

            // AI_069 owns the phase.  Life merely schedules a transition from
            // hover; it never retroactively turns a still-observed state 0 into
            // phase three.  This distinction is essential for mid-fight join.
            var charge = state == 1 || state == 6 || state == 11;
            var phase3 = state >= 9;
            var phase2 = state >= 4 && state <= 8;
            string phase;
            BossPattern pattern;
            var horizontal = 0;
            var vertical = 0;
            var explicitMovement = false;
            var orbitMovement = false;
            var distance = (phase3 ? 520f : 640f) +
                (nativeEnraged ? 140f : 0f);
            var margin = (phase3 ? 112f : phase2 ? 86f : 66f) +
                (nativeEnraged ? 54f : 0f);
            var runway = StableFlightDirection(s, t, m);
            switch (state)
            {
                case -1:
                    phase = tick < 45 ? "spawn-fade" : "spawn-emerge";
                    pattern = BossPattern.EllipseOrbit;
                    orbitMovement = true;
                    break;
                case 0:
                    phase = "phase-1-hover-before-" +
                        PhaseOneNext(sequence);
                    pattern = BossPattern.EllipseOrbit;
                    orbitMovement = true;
                    break;
                case 1:
                    phase = "phase-1-dash-" + PhaseOneDashOrdinal(sequence);
                    pattern = BossPattern.PerpendicularDashDodge;
                    break;
                case 2:
                    phase = tick < 12 ? "phase-1-bubble-stream-entry" :
                        "phase-1-bubble-stream";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = runway;
                    explicitMovement = true;
                    break;
                case 3:
                    phase = tick < 48 ? "phase-1-sharknado-windup" :
                        tick < 60 ? "phase-1-sharknado-imminent" :
                        "phase-1-sharknado-deployed";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = TornadoLaneDirection(s, runway);
                    vertical = tick >= 48 && tick <= 64 ?
                        PerpendicularY(s.Player, t) : 0;
                    explicitMovement = true;
                    break;
                case 4:
                    phase = tick < 120 ? "phase-2-transform-fade" :
                        "phase-2-transform-emerge";
                    pattern = BossPattern.EllipseOrbit;
                    distance = 700f;
                    orbitMovement = true;
                    break;
                case 5:
                    phase = "phase-2-hover-before-" +
                        PhaseTwoNext(sequence);
                    pattern = BossPattern.EllipseOrbit;
                    orbitMovement = true;
                    break;
                case 6:
                    phase = "phase-2-dash-" + PhaseTwoDashOrdinal(sequence);
                    pattern = BossPattern.PerpendicularDashDodge;
                    break;
                case 7:
                    // State 7 has speed 20 but is the 120-tick circular bubble
                    // attack, never a dash.
                    phase = tick < 20 ? "phase-2-circular-bubbles-entry" :
                        "phase-2-circular-bubbles";
                    pattern = BossPattern.CircleOrbit;
                    horizontal = runway;
                    explicitMovement = true;
                    break;
                case 8:
                    phase = tick < 48 ? "phase-2-cthulhunado-windup" :
                        tick < 60 ? "phase-2-cthulhunado-imminent" :
                        "phase-2-cthulhunado-deployed";
                    pattern = BossPattern.ProjectileLanes;
                    horizontal = TornadoLaneDirection(s, runway);
                    vertical = tick >= 48 && tick <= 64 ?
                        PerpendicularY(s.Player, t) : 0;
                    explicitMovement = true;
                    break;
                case 9:
                    phase = tick < 120 ? "phase-3-transform-fade" :
                        "phase-3-transform-hidden";
                    pattern = BossPattern.EllipseOrbit;
                    distance = 720f;
                    orbitMovement = true;
                    break;
                case 10:
                    phase = "phase-3-reposition-before-" +
                        PhaseThreeNext(sequence);
                    pattern = BossPattern.EllipseOrbit;
                    orbitMovement = true;
                    break;
                case 11:
                    phase = "phase-3-dash-group-" +
                        PhaseThreeDashGroup(sequence);
                    pattern = BossPattern.PerpendicularDashDodge;
                    break;
                case 12:
                    phase = tick < 15 ? "phase-3-teleport-fade" :
                        "phase-3-teleport-relocated";
                    pattern = BossPattern.ProjectileLanes;
                    // The relocation commits on tick 15. Keep both axes free
                    // instead of treating residual velocity as a dash.
                    horizontal = runway;
                    vertical = 0;
                    explicitMovement = true;
                    break;
                default:
                    return UnknownNativeState(s, t);
            }
            if (orbitMovement)
            {
                OrbitIntent(s, t, m, distance, out horizontal, out vertical);
                m.FishronHoverHorizontal = horizontal;
                m.FishronHoverVertical = vertical;
                m.FishronDashDirectionLocked = false;
                explicitMovement = true;
            }
            if (charge)
            {
                LockedChargeEscape(s, t, m, state, sequence,
                    out horizontal, out vertical);
                explicitMovement = true;
            }
            else if (!orbitMovement)
                m.FishronDashDirectionLocked = false;
            m.FishronPreviousNativeState = state;
            m.FishronPreviousNativeSequence = sequence;
            if (nativeEnraged) phase = "native-enraged-" + phase;
            var decision = Decision(s, t, phase, pattern, distance, -160,
                horizontal, vertical, charge, true,
                s.Mobility.CanFlipGravity, margin);
            decision.Directive.UseExplicitMovement = explicitMovement;
            // Hover establishes the tangent before AI_069 commits a charge;
            // the following dash owns that same ordinary movement route. The
            // scorer may brake it to neutral, but cannot reverse either axis
            // into the attack line because of short-horizon hazard noise.
            decision.Directive.OwnsMovementClosure = orbitMovement || charge;
            return decision;
        }

        private static int ChooseOrbitDirection(CombatSnapshot snapshot,
            TargetSnapshot target)
        {
            var relative = snapshot.Player.Center - target.Center;
            var plusTangent = new Vec2(-relative.Y, relative.X);
            if (relative.LengthSquared < 1f)
                plusTangent = new Vec2(1f, -1f);

            // Enter a low runway by climbing, not by spending the first hover
            // segment descending into its platforms. The inverse applies at a
            // genuinely close ceiling.
            if (snapshot.Player.OnGround || snapshot.Arena.ClearanceDown < 240f)
                return plusTangent.Y <= 0f ? 1 : -1;
            if (snapshot.Arena.ClearanceUp < 180f)
                return plusTangent.Y >= 0f ? 1 : -1;

            var velocity = snapshot.Player.Velocity;
            var alignment = velocity.X * plusTangent.X +
                velocity.Y * plusTangent.Y;
            if (velocity.LengthSquared >= 4f && Math.Abs(alignment) > .01f)
                return alignment >= 0f ? 1 : -1;

            var plusRoom = DirectionalRoom(snapshot.Arena,
                plusTangent.X, plusTangent.Y);
            var minusRoom = DirectionalRoom(snapshot.Arena,
                -plusTangent.X, -plusTangent.Y);
            return plusRoom >= minusRoom ? 1 : -1;
        }

        private static float DirectionalRoom(ArenaSnapshot arena,
            float worldX, float worldY)
        {
            var length = (float)Math.Sqrt(worldX * worldX + worldY * worldY);
            if (length < .001f) return 0f;
            worldX /= length;
            worldY /= length;
            var horizontal = worldX >= 0f ? arena.ClearanceRight :
                arena.ClearanceLeft;
            var vertical = worldY >= 0f ? arena.ClearanceDown :
                arena.ClearanceUp;
            return Math.Abs(worldX) * horizontal +
                Math.Abs(worldY) * vertical;
        }

        private static void OrbitIntent(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory, float idealDistance,
            out int horizontal, out int vertical)
        {
            var relative = snapshot.Player.Center - target.Center;
            var distanceSquared = relative.LengthSquared;
            if (distanceSquared < 1f || float.IsNaN(distanceSquared) ||
                float.IsInfinity(distanceSquared))
            {
                horizontal = memory.FishronHoverHorizontal != 0 ?
                    memory.FishronHoverHorizontal : 1;
                vertical = memory.FishronHoverVertical != 0 ?
                    memory.FishronHoverVertical : 1;
                return;
            }

            var distance = (float)Math.Sqrt(distanceSquared);
            var unitX = relative.X / distance;
            var unitY = relative.Y / distance;
            var direction = memory.FishronOrbitDirection == -1 ? -1 : 1;
            var tangentX = -unitY * direction;
            var tangentY = unitX * direction;
            var radial = (idealDistance - distance) /
                Math.Max(80f, idealDistance * .35f);
            radial = Math.Max(-.85f, Math.Min(.85f, radial));
            var desiredX = tangentX + unitX * radial;
            var desiredY = tangentY + unitY * radial;
            horizontal = Math.Abs(desiredX) >= .12f ?
                Math.Sign(desiredX) : 0;
            var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
            vertical = Math.Abs(desiredY) >= .12f ?
                -Math.Sign(desiredY) * gravitySign : 0;
            if (horizontal == 0 && vertical == 0)
            {
                if (Math.Abs(tangentX) >= Math.Abs(tangentY))
                    horizontal = Math.Sign(tangentX);
                else
                    vertical = -Math.Sign(tangentY) * gravitySign;
            }
        }

        private static void LockedChargeEscape(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory, int state, int sequence,
            out int horizontal, out int vertical)
        {
            var enteringDash = !memory.FishronDashDirectionLocked ||
                memory.FishronPreviousNativeState != state ||
                memory.FishronPreviousNativeSequence != sequence;
            if (enteringDash)
            {
                horizontal = memory.FishronHoverHorizontal;
                vertical = memory.FishronHoverVertical;
                if (horizontal == 0 && vertical == 0)
                    ChargeEscape(snapshot, target, target.Velocity,
                        out horizontal, out vertical);
                if (horizontal == 0 && vertical == 0)
                    vertical = 1;
                memory.FishronDashHorizontal = horizontal;
                memory.FishronDashVertical = vertical;
                memory.FishronDashDirectionLocked = true;
            }
            horizontal = memory.FishronDashHorizontal;
            vertical = memory.FishronDashVertical;
        }

        private static bool ValidNativePhase(CombatSnapshot snapshot,
            int state, int tick, int sequence, bool nativeEnraged)
        {
            if (snapshot == null || snapshot.Difficulty == null ||
                state < -1 || state > 12 || tick < 0 || sequence < 0)
                return false;
            var expert = snapshot.Difficulty.Expert ||
                snapshot.Difficulty.Master;
            // The third form is gated by `expertMode && life <= 15%` in
            // AI_069. For the Worthy does not create that phase in a Classic
            // world, so an observed state 9..12 there is not a natural tuple.
            if (state >= 9 && !expert) return false;

            // AI_069 uses one shared ai[2] clock, but its limit is selected
            // from the branch's exact native constants.  In particular, an
            // enraged Fishron uses num2=10 for every hover/reposition branch;
            // the old fixed 60/30 limits accepted stale mid-fight clocks and
            // could make a takeover enter a phase which had already ended.
            int timerLimit = FishronTimerLimit(snapshot, state, sequence,
                nativeEnraged);
            if (timerLimit < 0) return false;
            if (tick > timerLimit &&
                !(nativeEnraged && IsFishronDashState(state) &&
                  tick <= timerLimit + 2))
                return false;

            switch (state)
            {
                case -1: return sequence == 0;
                case 0: return sequence <= 11;
                case 1: return sequence <= 9;
                case 2: return sequence == 1;
                // Sequence 10 in state 0 is rewritten to 1 before the
                // flag6 (native enrage) redirect selects state 3.  Sequence
                // 1 is therefore valid only while that same predicate is
                // observed; sequence 0 is the ordinary state-3 entry.
                case 3: return sequence == 0 || nativeEnraged && sequence == 1;
                case 4: return sequence <= 11;
                case 5: return sequence <= 7;
                case 6: return sequence <= 5;
                case 7: return sequence == 1;
                case 8: return sequence == 0;
                case 9: return sequence <= 7;
                // State 10's dispatch sends ai[3] == 9 to the spinning-shark
                // branch (state 13).  The third-phase sequence reaches 9 before
                // it resets, so 9 is an ordinary native observation here and
                // rejecting it reported a legitimate spin as an unknown state.
                case 10: return sequence <= 9;
                case 11:
                    return sequence == 0 || sequence == 2 ||
                        sequence == 3 || sequence == 5 ||
                        sequence == 6 || sequence == 7;
                case 12:
                    return sequence == 1 || sequence == 4 ||
                        sequence == 8;
                default: return false;
            }
        }

        private static int FishronTimerLimit(CombatSnapshot snapshot,
            int state, int sequence, bool nativeEnraged)
        {
            var expert = snapshot.Difficulty.Expert ||
                snapshot.Difficulty.Master;
            if (state == -1) return 75;
            if (state == 0)
            {
                // num2 is expert?40:60, is reduced to 30 while the dash
                // sequence is still 0..9, and is forced to 10 by flag6.
                // Sequence 10 and 11 are the attack markers that follow the
                // dash group: flag5 (ai[3] < num2*2) is then false, so the
                // native chain falls through to the base cadence.
                if (nativeEnraged) return 10;
                return sequence < 10 ? 30 : expert ? 40 : 60;
            }
            // The native enrage branch overwrites num6 for every dash family,
            // including the phase-one dash (state 1).  Do not keep the ordinary
            // 30/28-tick envelope after the exact predicate has become true.
            if (state == 1) return nativeEnraged ? 25 : expert ? 28 : 30;
            if (state == 2) return 80;
            // In 1.4.5.8 an already-enraged Fishron can enter the first
            // sharknado branch with ai[2] preset to num12 - 40 (140).  The
            // enrage flag may also become true while that branch is running,
            // so both the accelerated 140..179 window and a normal 0..89
            // window are legitimate observations.  Keep the larger native
            // upper bound whenever the current enrage predicate is true.
            if (state == 3) return nativeEnraged ? 180 : 90;
            if (state == 4) return 180;
            if (state == 5)
            {
                // State 5 is the second-phase hover.  In 1.4.5.8 flag5 is
                // selected from the phase-local sequence bound (ai[3] < 6),
                // rather than the phase-one bound of ten.  The final two
                // hover markers (6/7) therefore use the base 60/40 cadence;
                // treating them as the 20/40 cadence accepts a stale clock and
                // can enter the next attack one cycle late on Classic.
                if (nativeEnraged) return 10;
                return sequence < 6 ? (expert ? 40 : 20) :
                    (expert ? 40 : 60);
            }
            if (state == 6) return nativeEnraged ? 25 : expert ? 27 : 30;
            if (state == 7) return 120;
            if (state == 8) return 90;
            // State 9 is the phase-three transform, and its exit test uses
            // num14 (180), not num3.  The hover family's num3 must not be
            // applied here even though the state also lerps its velocity.
            if (state == 9) return 180;
            if (state == 10) return nativeEnraged ? 10 : 30;
            // State 11 uses the same native num6 dash timer as states 1 and 6;
            // 25 is the enrage value, while calm phase-three dashes retain the
            // ordinary expert/classic values.
            if (state == 11) return nativeEnraged ? 25 : expert ? 27 : 30;
            if (state == 12) return 30;
            return -1;
        }

        private static bool IsFishronDashState(int state) =>
            state == 1 || state == 6 || state == 11;

        private static int StableFlightDirection(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory)
        {
            if (memory.PreviousTargetKey != target.Key ||
                memory.OrbitDirection == 0)
                memory.OrbitDirection = snapshot.Arena.ClearanceRight >=
                    snapshot.Arena.ClearanceLeft ? 1 : -1;
            var direction = memory.OrbitDirection;
            var remaining = direction > 0 ? snapshot.Arena.ClearanceRight :
                snapshot.Arena.ClearanceLeft;
            var opposite = direction > 0 ? snapshot.Arena.ClearanceLeft :
                snapshot.Arena.ClearanceRight;
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = speed * speed /
                (2f * Math.Max(.01f, snapshot.Player.RunSlowdown));
            if (remaining < Math.Max(240f, braking + 150f) &&
                opposite > remaining + 240f)
                memory.OrbitDirection = -direction;
            return memory.OrbitDirection;
        }

        private static int TornadoLaneDirection(CombatSnapshot snapshot,
            int fallback)
        {
            var playerX = snapshot.Player.Center.X;
            var nearest = float.MaxValue;
            var result = fallback;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.Projectile ||
                    (threat.Type != 384 && threat.Type != 386)) continue;
                var centerX = threat.Position.X + threat.Width * .5f;
                var distance = Math.Abs(playerX - centerX);
                if (distance >= nearest) continue;
                nearest = distance;
                result = playerX >= centerX ? 1 : -1;
            }
            if (result > 0 && snapshot.Arena.ClearanceRight < 180f &&
                snapshot.Arena.ClearanceLeft > 360f) return -1;
            if (result < 0 && snapshot.Arena.ClearanceLeft < 180f &&
                snapshot.Arena.ClearanceRight > 360f) return 1;
            return result;
        }

        private static bool TryGetNativeEnrage(CombatSnapshot snapshot,
            int npcKey, out DukeFishronNativeEnrageObservation observation)
        {
            observation = default(DukeFishronNativeEnrageObservation);
            if (snapshot?.PriorityBoss == null) return false;
            var values = snapshot.PriorityBoss.DukeFishrons;
            for (var i = 0; i < values.Count; i++)
            {
                var candidate = values[i];
                if (candidate.NpcKey != npcKey || !candidate.Known) continue;
                string ignored;
                if (!PriorityBossNativeContextContract.TryValidate(
                        in candidate, out ignored)) return false;
                observation = candidate;
                return true;
            }
            return false;
        }

        private static string PhaseOneNext(int sequence)
        {
            if (sequence == 10) return "bubble-stream";
            if (sequence == 11) return "sharknado";
            return sequence >= 0 && sequence <= 9 ? "dash-" +
                PhaseOneDashOrdinal(sequence) : "unknown";
        }

        private static int PhaseOneDashOrdinal(int sequence) =>
            sequence >= 0 && sequence <= 9 ? sequence / 2 + 1 : 0;

        private static string PhaseTwoNext(int sequence)
        {
            if (sequence == 6) return "circular-bubbles";
            if (sequence == 7) return "cthulhunado";
            return sequence >= 0 && sequence <= 5 ? "dash-" +
                PhaseTwoDashOrdinal(sequence) : "unknown";
        }

        private static int PhaseTwoDashOrdinal(int sequence) =>
            sequence >= 0 && sequence <= 5 ? sequence / 2 + 1 : 0;

        private static string PhaseThreeNext(int sequence) =>
            sequence == 1 || sequence == 4 || sequence == 8 ?
                "teleport" : sequence >= 0 && sequence <= 8 ? "dash" :
                "unknown";

        private static int PhaseThreeDashGroup(int sequence) =>
            sequence <= 0 ? 1 : sequence <= 3 ? 2 : 3;

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase =
                "unrecognized-native-state")
        {
            var result = Decision(snapshot, target,
                phase, BossPattern.EllipseOrbit,
                700, -160, 0, 0, false, false, false, 120, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Duke Fishron exposed an unreviewed native AI state";
            return result;
        }

        private BossDecision UnmodeledThreatSafetyHold(CombatSnapshot snapshot,
            TargetSnapshot target, string reason)
        {
            var result = Decision(snapshot, target,
                "unmodeled-native-threat", BossPattern.ProjectileLanes,
                700, -160, 0, 0, false, false, false, 120, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.OwnsMovementClosure = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.HoldNeutralControls = true;
            result.Directive.NeutralControlReason = reason;
            return result;
        }
    }

    internal sealed class CultistStrategy : BossStrategyBase
    {
        public CultistStrategy() : base("lunatic-cultist", 1100, 620, 5.5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 439);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var real = Pick(s, 439);
            var ritual = HasThreat(s, 490) || real.Invulnerable;
            var doom = HasType(s.Targets, 523) || HasThreat(s, 593);
            var phase = ritual ? "ritual-hit-real" : doom ? "destroy-ancient-doom" : Life(real) < .5f ? "extended-seven-attack-cycle" : "six-attack-cycle";
            return Decision(s, real, phase, ritual ? BossPattern.ProjectileLanes : BossPattern.EllipseOrbit,
                520, -140, 0, 0, doom, true, false, doom ? 82 : 55, !real.Invulnerable);
        }
    }

    internal sealed class MoonLordStrategy : BossStrategyBase
    {
        private enum MoonAlertKind
        {
            None,
            Tongue,
            Bolts,
            SpinBarrage,
            SphereRelease,
            DeathrayTelegraph,
            ActiveDeathray
        }

        private struct MoonAlert
        {
            public MoonAlertKind Kind;
            public int Ticks;
            public string Source;
            public TargetSnapshot SourceTarget;
            public bool HasSourceTarget;
            public int ConcurrentCount;
            public float RayEscapeWorldX;
            public float RayEscapeWorldY;
            public float StrongestRayEscapeWorldX;
            public float StrongestRayEscapeWorldY;
            public float StrongestRayWeight;
        }

        private struct MoonNativeAttack
        {
            public int State;
            public int LocalTick;
            public int Duration;
        }

        private const float MoonDeathrayAngularVelocity =
            (float)(Math.PI * 2d / 540d);

        public MoonLordStrategy() : base("moon-lord", 2100, 900, 7f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 396, 398);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            string nativeFailure;
            if (s.NativeContextKnown && !ValidateNativeMoonState(s,
                    out nativeFailure))
                return UnknownNativeState(s, Pick(s, 398, 396, 397),
                    nativeFailure);
            TargetSnapshot nativeCore;
            int nativeCoreState;
            if (s.NativeContextKnown && TryReadMoonCore(s, out nativeCore,
                    out nativeCoreState) &&
                (nativeCoreState == 2 || nativeCoreState == 3))
                return TerminalCoreState(s, nativeCore, nativeCoreState);
            var target = BalancedMoonTarget(s);
            var alert = MoonClockAlert(s);
            string phase;
            BossPattern pattern;
            var horizontal = StableRunwayDirection(s, target, m);
            var vertical = 0;
            var distance = 860f;
            var margin = 106f;
            var dash = false;
            var strict = false;
            switch (alert.Kind)
            {
                case MoonAlertKind.ActiveDeathray:
                    phase = "active-deathray-" + alert.Source + "-concurrent-" +
                        alert.ConcurrentCount;
                    pattern = BossPattern.PerpendicularDashDodge;
                    RayEscapeIntent(s, in alert, horizontal, out horizontal,
                        out vertical);
                    distance = 1040f;
                    margin = 190f;
                    dash = true;
                    strict = true;
                    break;
                case MoonAlertKind.DeathrayTelegraph:
                    phase = "deathray-telegraph-" + alert.Source + "-" +
                        alert.Ticks + "-concurrent-" + alert.ConcurrentCount;
                    pattern = BossPattern.PerpendicularDashDodge;
                    // The pinned head/True-Eye schedule exposes the damaging
                    // ray at local tick 200. Enter its already-swept half-plane
                    // before then; waiting for projectile 455 forfeits this
                    // deterministic recovery window.
                    if (alert.Ticks <= 120)
                    {
                        RayEscapeIntent(s, in alert, horizontal,
                            out horizontal, out vertical);
                        strict = true;
                    }
                    distance = 980f;
                    margin = alert.Ticks <= 45 ? 184f : 154f;
                    dash = alert.Ticks <= 24;
                    break;
                case MoonAlertKind.SphereRelease:
                    phase = "sphere-release-telegraph-" + alert.Source +
                        "-" + alert.Ticks + "-concurrent-" +
                        alert.ConcurrentCount;
                    pattern = BossPattern.ProjectileLanes;
                    if (alert.Ticks <= 48)
                    {
                        vertical = alert.HasSourceTarget ?
                            PerpendicularY(s.Player, alert.SourceTarget) :
                            PerpendicularY(s.Player, target);
                        strict = true;
                    }
                    margin = 148f;
                    break;
                case MoonAlertKind.Tongue:
                    phase = "moon-bite-tongue-window";
                    pattern = BossPattern.HorizontalKite;
                    distance = 980f;
                    margin = 132f;
                    strict = true;
                    break;
                case MoonAlertKind.Bolts:
                    phase = "bolt-clock-" + alert.Source + "-" +
                        alert.Ticks + "-concurrent-" + alert.ConcurrentCount;
                    pattern = BossPattern.ProjectileLanes;
                    if (alert.Ticks <= 18)
                    {
                        vertical = alert.HasSourceTarget ?
                            PerpendicularY(s.Player, alert.SourceTarget) : 0;
                        strict = true;
                    }
                    margin = 124f;
                    break;
                case MoonAlertKind.SpinBarrage:
                    phase = "true-eye-spin-barrage-" + alert.Source + "-" +
                        alert.Ticks + "-concurrent-" + alert.ConcurrentCount;
                    pattern = BossPattern.CircleOrbit;
                    if (alert.HasSourceTarget)
                        OrbitIntents(s, alert.SourceTarget, horizontal,
                            out horizontal, out vertical);
                    margin = 138f;
                    strict = true;
                    break;
                default:
                    phase = target.Type == 398 && !target.Invulnerable ?
                        "core-finish" : "synchronize-open-eyes";
                    pattern = BossPattern.Runway;
                    break;
            }
            var result = Decision(s, target, phase, pattern, distance,
                alert.Kind == MoonAlertKind.ActiveDeathray ||
                    alert.Kind == MoonAlertKind.DeathrayTelegraph ? -310 :
                    -180,
                horizontal, vertical, dash, true, true, margin);
            // Moon Lord is a source-clock controller. Bypass stale generic
            // direction hysteresis so an arbitrary mid-fight takeover begins
            // converging on its selected runway/escape lane immediately.
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = true;
            if (!strict && alert.Kind == MoonAlertKind.None)
                result.Directive.VerticalIntent = 0;
            return result;
        }

        private static TargetSnapshot BalancedMoonTarget(
            CombatSnapshot snapshot)
        {
            TargetSnapshot core = default(TargetSnapshot);
            var coreFound = false;
            TargetSnapshot selected = default(TargetSnapshot);
            var found = false;
            var highestRatio = -1f;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Type == 398 && candidate.Life > 0)
                {
                    core = candidate;
                    coreFound = true;
                    if (!candidate.Invulnerable && candidate.Ai0 == 1f)
                        return candidate;
                }
                if ((candidate.Type != 396 && candidate.Type != 397) ||
                    candidate.Life <= 0 || candidate.Invulnerable)
                    continue;
                var ratio = Life(candidate);
                // Damage the HIGHEST remaining fraction.  Lowest-life targeting
                // releases an immortal True Eye early and makes the later
                // clocks overlap; ratio balancing keeps all three component
                // deaths close together without depending on their unequal HP.
                if (!found || ratio > highestRatio + .0001f ||
                    Math.Abs(ratio - highestRatio) <= .0001f &&
                        candidate.Type == 396 && selected.Type != 396)
                {
                    selected = candidate;
                    highestRatio = ratio;
                    found = true;
                }
            }
            if (found) return selected;
            if (coreFound) return core;
            return Pick(snapshot, 398, 396, 397);
        }

        private static MoonAlert MoonClockAlert(CombatSnapshot snapshot)
        {
            var best = new MoonAlert { Kind = MoonAlertKind.None,
                Ticks = int.MaxValue, Source = "none" };

            var observedRay = false;
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Kind != ThreatKind.Projectile ||
                    threat.Type != 455 ||
                    threat.Geometry != ThreatGeometry.MoonLordDeathray)
                    continue;
                observedRay = true;
                var warmup = Math.Max(0, 20 -
                    (int)Math.Floor(threat.BeamAge));
                Consider(ref best, warmup == 0 ?
                    MoonAlertKind.ActiveDeathray :
                    MoonAlertKind.DeathrayTelegraph, warmup,
                    "projectile-455-" + i, default(TargetSnapshot), false);
                AddRayEscape(ref best, snapshot.Player.Center,
                    threat.BeamOrigin, threat.BeamDirection,
                    threat.BeamAngularVelocity);
            }

            if (snapshot.NativeContextKnown)
            {
                var spheres = snapshot.PriorityBoss.MoonLordProjectiles454;
                for (var i = 0; i < spheres.Count; i++)
                {
                    var sphere = spheres[i];
                    if (sphere.DamageEnabled)
                        Consider(ref best, MoonAlertKind.SphereRelease, 0,
                            "native-sphere-" + sphere.ProjectileKey,
                            default(TargetSnapshot), false);
                    else if (sphere.Known && sphere.Ai0AgeOrMode >= 0f)
                        Consider(ref best, MoonAlertKind.SphereRelease,
                            Math.Max(0, 60 - (int)sphere.Ai0AgeOrMode),
                            "native-sphere-" + sphere.ProjectileKey,
                            default(TargetSnapshot), false);
                }
                var tongues = snapshot.PriorityBoss.MoonLordProjectiles456;
                for (var i = 0; i < tongues.Count; i++)
                {
                    var tongue = tongues[i];
                    if (tongue.Known && tongue.HasLiveMoonLordSource &&
                        tongue.TargetPlayerKey == snapshot.LocalPlayerIndex &&
                        !tongue.ContactLatched)
                        Consider(ref best, MoonAlertKind.Tongue, 0,
                            tongue.Returning ? "native-returning-tongue-" +
                                tongue.ProjectileKey :
                                "native-outbound-tongue-" +
                                tongue.ProjectileKey,
                            default(TargetSnapshot), false);
                }
            }

            if (HasThreat(snapshot, 452, 462))
                Consider(ref best, MoonAlertKind.Bolts, 0,
                    "live-phantasmal-projectile", default(TargetSnapshot),
                    false);

            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var source = snapshot.Targets[i];
                if (source.Life <= 0) continue;
                MoonNativeAttack attack;
                if (!TryDecodeMoonAttack(in source, out attack)) continue;
                var state = attack.State;
                var local = attack.LocalTick;
                var duration = attack.Duration;
                if (source.Type == 396)
                {
                    if (state == 1)
                    {
                        if (local < 360)
                        {
                            var untilDamage = Math.Max(0, 200 - local);
                            Consider(ref best, local >= 200 ?
                                MoonAlertKind.ActiveDeathray :
                                MoonAlertKind.DeathrayTelegraph, untilDamage,
                                "head", source, true);
                            if (!observedRay && untilDamage <= 120)
                                AddPredictedRayEscape(ref best, snapshot,
                                    in source);
                        }
                    }
                    else if (state == 2)
                        Consider(ref best, MoonAlertKind.Tongue, 0, "head",
                            source, true);
                    else if (state == 3)
                        Consider(ref best, MoonAlertKind.Bolts,
                            TicksToCadence(local, duration - 14, 7,
                                duration - 7), "head", source, true);
                }
                else if (source.Type == 397)
                {
                    var side = (int)source.Ai2;
                    var sideName = side == 0 ? "left-hand" : "right-hand";
                    if (state == 1)
                        Consider(ref best, MoonAlertKind.Bolts,
                            TicksToCadence(local, 28, 4, 52), sideName,
                            source, true);
                    else if (state == 2)
                    {
                        // The first held sphere starts damaging at local tick
                        // 90; the six-sphere release is at local tick 292.
                        var untilHazard = local < 90 ? 90 - local : 0;
                        Consider(ref best, MoonAlertKind.SphereRelease,
                            untilHazard, sideName, source, true);
                    }
                    else if (state == 3)
                        Consider(ref best, MoonAlertKind.Bolts,
                            TicksToCadence(local, duration - 14, 7,
                                duration - 7), sideName, source, true);
                }
                else if (source.Type == 400)
                {
                    if (state == 4)
                    {
                        if (local < 360)
                        {
                            var untilDamage = Math.Max(0, 200 - local);
                            Consider(ref best, local >= 200 ?
                                MoonAlertKind.ActiveDeathray :
                                MoonAlertKind.DeathrayTelegraph, untilDamage,
                                "true-eye-" + source.Key, source, true);
                            if (!observedRay && untilDamage <= 120)
                                AddPredictedRayEscape(ref best, snapshot,
                                    in source);
                        }
                    }
                    else if (state == 2)
                    {
                        // Both native state-2 segments share this local clock.
                        // The first held sphere can damage at 75; all six are
                        // released with the True Eye's short dash at 105.
                        Consider(ref best, MoonAlertKind.SphereRelease,
                            local < 75 ? 75 - local : 0,
                            "true-eye-" + source.Key, source, true);
                    }
                    else if (state == 1)
                        Consider(ref best, MoonAlertKind.Bolts,
                            TicksToCadence(local, duration - 14, 7,
                                duration - 7), "true-eye-" + source.Key,
                            source, true);
                    else if (state == 3)
                        Consider(ref best, MoonAlertKind.SpinBarrage,
                            Math.Max(0, 45 - local),
                            "true-eye-" + source.Key, source, true);
                }
            }
            // Legacy adapters have no independent projectile metadata.
            if (!snapshot.NativeContextKnown && HasThreat(snapshot, 454))
                Consider(ref best, MoonAlertKind.SphereRelease, 0,
                    "projectile-454", default(TargetSnapshot), false);
            return best;
        }

        private static bool ValidateNativeMoonState(CombatSnapshot snapshot,
            out string reason)
        {
            var coreKey = -1;
            var hasLiveSource = false;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Life <= 0) continue;
                if (target.Type == 398)
                {
                    if (coreKey >= 0 || !ValidMoonCore(in target))
                    {
                        reason = coreKey >= 0 ?
                            "duplicate-native-moon-lord-core" :
                            "malformed-native-moon-lord-core-state";
                        return false;
                    }
                    coreKey = target.Key;
                }
            }
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Life <= 0 || (target.Type != 396 &&
                    target.Type != 397 && target.Type != 400)) continue;
                hasLiveSource = true;
                MoonNativeAttack ignoredAttack;
                var parent = NativeNonnegativeInteger(target.Ai3);
                if (!target.Ai0Known || !target.Ai1Known ||
                    !target.Ai2Known || !target.Ai3Known ||
                    !target.NativeTargetKnown ||
                    target.NativeTargetPlayerIndex != snapshot.LocalPlayerIndex ||
                    parent == int.MinValue || coreKey >= 0 && parent != coreKey ||
                    !TryDecodeMoonAttack(in target, out ignoredAttack))
                {
                    reason = "malformed-or-inconsistent-native-moon-lord-source-clock";
                    return false;
                }
            }
            if (hasLiveSource && coreKey < 0)
            {
                reason = "missing-native-moon-lord-core";
                return false;
            }
            var context = snapshot.PriorityBoss;
            for (var i = 0; i < context.MoonLordProjectiles454.Count; i++)
            {
                string ignored;
                var value = context.MoonLordProjectiles454[i];
                if (!PriorityBossNativeContextContract.TryValidate(in value,
                        out ignored))
                {
                    reason = "malformed-native-moon-lord-sphere";
                    return false;
                }
            }
            for (var i = 0; i < context.MoonLordProjectiles456.Count; i++)
            {
                string ignored;
                var value = context.MoonLordProjectiles456[i];
                if (!PriorityBossNativeContextContract.TryValidate(in value,
                        out ignored))
                {
                    reason = "malformed-native-moon-lord-tongue";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private static bool ValidMoonCore(in TargetSnapshot core)
        {
            if (!core.Ai0Known || !core.Ai1Known || !core.Ai2Known ||
                !core.Ai3Known || core.Key < 0) return false;
            var state = NativeIntegerState(core.Ai0, -2, 3);
            var tick = NativeNonnegativeInteger(core.Ai1);
            if (state == int.MinValue || tick == int.MinValue) return false;
            switch (state)
            {
                case -2:
                case -1: return tick <= 60;
                case 0:
                case 1: return tick == 0;
                case 2: return tick <= 600;
                case 3: return tick <= 60;
                default: return false;
            }
        }

        private static bool TryReadMoonCore(CombatSnapshot snapshot,
            out TargetSnapshot core, out int state)
        {
            core = default(TargetSnapshot);
            state = int.MinValue;
            var found = false;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Type != 398 || candidate.Life <= 0) continue;
                if (found || !ValidMoonCore(in candidate)) return false;
                state = NativeIntegerState(candidate.Ai0, -2, 3);
                if (state == int.MinValue) return false;
                core = candidate;
                found = true;
            }
            return found;
        }

        private BossDecision TerminalCoreState(CombatSnapshot snapshot,
            TargetSnapshot core, int state)
        {
            // AI_077 state 2 is the 600-tick defeated death drama; state 3 is
            // the 60-tick no-live-player departure. Neither is a combat loop
            // an active F8 takeover can safely or usefully enter.
            var phase = state == 2 ? "native-defeat-sequence" :
                "native-departure";
            var result = Decision(snapshot, core, phase,
                BossPattern.Runway, 0f, 0f, 0, 0, false, false, false,
                180f, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason = state == 2 ?
                "Moon Lord is already in its native defeat sequence" :
                "Moon Lord entered its native departure state";
            return result;
        }

        private static bool TryDecodeMoonAttack(in TargetSnapshot source,
            out MoonNativeAttack attack)
        {
            attack = default(MoonNativeAttack);
            var state = NativeIntegerState(source.Ai0, -3, 4);
            var clock = NativeNonnegativeInteger(source.Ai1);
            if (state == int.MinValue || clock == int.MinValue) return false;
            int expected;
            int local;
            int duration;
            if (source.Type == 396)
            {
                if (clock >= 1200 || (state == -3 || state == -2))
                {
                    if (clock >= 1200 || (state != -3 && state != -2))
                        return false;
                    attack = new MoonNativeAttack { State = state,
                        LocalTick = clock, Duration = 1200 };
                    return true;
                }
                if (!DecodeHead(clock, out expected, out local,
                    out duration) || state != expected) return false;
            }
            else if (source.Type == 397)
            {
                var side = NativeIntegerState(source.Ai2, 0, 1);
                if (side == int.MinValue || clock >= 600 || state < -2 ||
                    state > 3) return false;
                if (state == -2)
                {
                    if (clock >= 32) return false;
                    attack = new MoonNativeAttack { State = state,
                        LocalTick = clock, Duration = 32 };
                    return true;
                }
                if (!DecodeHand(side, clock, out expected, out local,
                    out duration) || state != expected) return false;
            }
            else if (source.Type == 400)
            {
                if (clock >= 1200 || state < -2 || state > 4)
                    return false;
                if (!DecodeTrueEye(clock, out expected, out local,
                    out duration)) return false;
                // A newly spawned eye remains -2 until its offset schedule next
                // reaches idle state zero. Once idle is reached native changes
                // ai[0] to zero in the same update.
                if (state == -2)
                {
                    if (expected == 0) return false;
                    attack = new MoonNativeAttack { State = state,
                        LocalTick = local, Duration = duration };
                    return true;
                }
                if (state != expected) return false;
            }
            else return false;

            attack = new MoonNativeAttack { State = state,
                LocalTick = local, Duration = duration };
            return true;
        }

        private static bool DecodeHead(int clock, out int state,
            out int local, out int duration)
        {
            // InitializeMoonLordAttacks row 2: 3/180, 0/30, 2/435,
            // 3/180, 1/375. Boundaries use <= in native AI.
            return DecodeFive(clock, 3, 180, 0, 30, 2, 435, 3, 180,
                1, 375, out state, out local, out duration);
        }

        private static bool DecodeHand(int side, int clock, out int state,
            out int local, out int duration)
        {
            return side == 0
                ? DecodeFive(clock, 0, 50, 1, 70, 2, 330, 0, 60,
                    3, 90, out state, out local, out duration)
                : DecodeFive(clock, 1, 70, 0, 50, 3, 90, 0, 60,
                    2, 330, out state, out local, out duration);
        }

        private static bool DecodeTrueEye(int clock, out int state,
            out int local, out int duration)
        {
            // InitializeMoonLordAttacks2 has five 53-tick idle segments.
            var starts = 0;
            if (clock < starts + 53)
                return SetDecoded(0, clock - starts, 53, out state,
                    out local, out duration);
            starts += 53;
            if (clock < starts + 90)
                return SetDecoded(1, clock - starts, 90, out state,
                    out local, out duration);
            starts += 90;
            if (clock < starts + 53)
                return SetDecoded(0, clock - starts, 53, out state,
                    out local, out duration);
            starts += 53;
            if (clock < starts + 135)
                return SetDecoded(2, clock - starts, 135, out state,
                    out local, out duration);
            starts += 135;
            if (clock < starts + 53)
                return SetDecoded(0, clock - starts, 53, out state,
                    out local, out duration);
            starts += 53;
            if (clock < starts + 200)
                return SetDecoded(3, clock - starts, 200, out state,
                    out local, out duration);
            starts += 200;
            if (clock < starts + 53)
                return SetDecoded(0, clock - starts, 53, out state,
                    out local, out duration);
            starts += 53;
            if (clock < starts + 375)
                return SetDecoded(4, clock - starts, 375, out state,
                    out local, out duration);
            starts += 375;
            if (clock < starts + 53)
                return SetDecoded(0, clock - starts, 53, out state,
                    out local, out duration);
            starts += 53;
            if (clock < starts + 135)
                return SetDecoded(2, clock - starts, 135, out state,
                    out local, out duration);
            state = local = duration = 0;
            return false;
        }

        private static bool DecodeFive(int clock,
            int state0, int duration0, int state1, int duration1,
            int state2, int duration2, int state3, int duration3,
            int state4, int duration4, out int state, out int local,
            out int duration)
        {
            var start = 0;
            if (clock < start + duration0)
                return SetDecoded(state0, clock - start, duration0,
                    out state, out local, out duration);
            start += duration0;
            if (clock < start + duration1)
                return SetDecoded(state1, clock - start, duration1,
                    out state, out local, out duration);
            start += duration1;
            if (clock < start + duration2)
                return SetDecoded(state2, clock - start, duration2,
                    out state, out local, out duration);
            start += duration2;
            if (clock < start + duration3)
                return SetDecoded(state3, clock - start, duration3,
                    out state, out local, out duration);
            start += duration3;
            if (clock < start + duration4)
                return SetDecoded(state4, clock - start, duration4,
                    out state, out local, out duration);
            state = local = duration = 0;
            return false;
        }

        private static bool SetDecoded(int decodedState, int decodedLocal,
            int decodedDuration, out int state, out int local,
            out int duration)
        {
            state = decodedState;
            local = decodedLocal;
            duration = decodedDuration;
            return true;
        }

        private BossDecision UnknownNativeState(CombatSnapshot snapshot,
            TargetSnapshot target, string phase)
        {
            var result = Decision(snapshot, target, phase,
                BossPattern.Runway, 900, -180, 0, 0, false, false, false,
                180, false);
            result.Directive.UseExplicitMovement = true;
            result.Directive.ForceContinuousMovement = false;
            result.Directive.RequestControlReturn = true;
            result.Directive.ControlReturnReason =
                "Moon Lord native NPC or projectile state is unavailable";
            return result;
        }

        private static int StableRunwayDirection(CombatSnapshot snapshot,
            TargetSnapshot target, BossMemory memory)
        {
            if (memory.PreviousTargetKey != target.Key ||
                memory.OrbitDirection == 0)
            {
                var away = AwayX(snapshot.Player, target);
                var awayRoom = away > 0 ? snapshot.Arena.ClearanceRight :
                    snapshot.Arena.ClearanceLeft;
                var reverseRoom = away > 0 ? snapshot.Arena.ClearanceLeft :
                    snapshot.Arena.ClearanceRight;
                memory.OrbitDirection = awayRoom + 160f >= reverseRoom ?
                    away : -away;
            }
            var currentRoom = memory.OrbitDirection > 0 ?
                snapshot.Arena.ClearanceRight : snapshot.Arena.ClearanceLeft;
            var otherRoom = memory.OrbitDirection > 0 ?
                snapshot.Arena.ClearanceLeft : snapshot.Arena.ClearanceRight;
            if (currentRoom < 480f && otherRoom > currentRoom + 220f)
                memory.OrbitDirection *= -1;
            return memory.OrbitDirection;
        }

        private static void OrbitIntents(CombatSnapshot snapshot,
            TargetSnapshot source, int loop, out int horizontal,
            out int vertical)
        {
            horizontal = loop == 0 ? 1 : Math.Sign(loop);
            vertical = snapshot.Player.Center.X >= source.Center.X ?
                horizontal : -horizontal;
            if (vertical > 0 && snapshot.Arena.ClearanceUp < 220f)
                vertical = -1;
            else if (vertical < 0 && snapshot.Arena.ClearanceDown < 220f)
                vertical = 1;
        }

        private static void RayEscapeIntent(CombatSnapshot snapshot,
            in MoonAlert alert, int fallbackHorizontal, out int horizontal,
            out int vertical)
        {
            var worldX = alert.RayEscapeWorldX;
            var worldY = alert.RayEscapeWorldY;
            if (worldX * worldX + worldY * worldY < .0025f)
            {
                worldX = alert.StrongestRayEscapeWorldX;
                worldY = alert.StrongestRayEscapeWorldY;
            }
            var magnitude = (float)Math.Sqrt(worldX * worldX +
                worldY * worldY);
            horizontal = magnitude > .0001f &&
                Math.Abs(worldX) >= magnitude * .16f ? Math.Sign(worldX) :
                Math.Sign(fallbackHorizontal);
            var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
            vertical = magnitude > .0001f &&
                Math.Abs(worldY) >= magnitude * .16f ?
                -Math.Sign(worldY) * gravitySign : 0;
            if (horizontal == 0 && vertical == 0) horizontal = 1;
        }

        private static void AddPredictedRayEscape(ref MoonAlert alert,
            CombatSnapshot snapshot, in TargetSnapshot source)
        {
            var direction = snapshot.Player.Center - source.Center;
            if (direction.LengthSquared < .0001f) direction = new Vec2(0f, 1f);
            direction = direction.Normalized();
            var rotationSign = direction.X < 0f ? 1f : -1f;
            var angle = (float)Math.Atan2(direction.Y, direction.X) -
                rotationSign * (float)(Math.PI * 2d / 6d);
            var initial = new Vec2((float)Math.Cos(angle),
                (float)Math.Sin(angle));
            AddRayEscape(ref alert, snapshot.Player.Center, source.Center,
                initial, rotationSign * (float)(Math.PI * 2d / 540d));
        }

        private static void AddRayEscape(ref MoonAlert alert,
            Vec2 playerCenter, Vec2 origin, Vec2 beamDirection,
            float angularVelocity)
        {
            if (beamDirection.LengthSquared < .0001f ||
                float.IsNaN(angularVelocity) ||
                float.IsInfinity(angularVelocity) ||
                Math.Abs(angularVelocity) < .000001f) return;
            var direction = beamDirection.Normalized();
            // Native positive angular velocity sweeps toward the direction's
            // left normal. Crossing toward the already-swept half-plane is the
            // opposite normal; this remains valid for either ray rotation.
            var sign = Math.Sign(angularVelocity);
            var escapeX = sign * direction.Y;
            var escapeY = -sign * direction.X;
            var relative = playerCenter - origin;
            var across = Math.Abs(direction.X * relative.Y -
                direction.Y * relative.X);
            var along = Vec2.Dot(relative, direction);
            var weight = 1f / Math.Max(48f, across);
            if (along >= 0f) weight *= 2f;
            alert.RayEscapeWorldX += escapeX * weight;
            alert.RayEscapeWorldY += escapeY * weight;
            if (weight > alert.StrongestRayWeight)
            {
                alert.StrongestRayWeight = weight;
                alert.StrongestRayEscapeWorldX = escapeX;
                alert.StrongestRayEscapeWorldY = escapeY;
            }
        }

        private static int TicksToCadence(int local, int first, int cadence,
            int last)
        {
            if (local <= first) return first - local;
            if (local > last) return int.MaxValue;
            var elapsed = local - first;
            var remainder = elapsed % cadence;
            var result = remainder == 0 ? 0 : cadence - remainder;
            return local + result <= last ? result : int.MaxValue;
        }

        private static void Consider(ref MoonAlert best,
            MoonAlertKind kind, int ticks, string source,
            TargetSnapshot sourceTarget, bool hasSourceTarget)
        {
            if (ticks == int.MaxValue) return;
            ticks = Math.Max(0, ticks);
            best.ConcurrentCount++;
            var priority = AlertPriority(kind, ticks);
            var bestPriority = AlertPriority(best.Kind, best.Ticks);
            if (priority < bestPriority || priority == bestPriority &&
                ticks >= best.Ticks) return;
            best.Kind = kind;
            best.Ticks = ticks;
            best.Source = source;
            best.SourceTarget = sourceTarget;
            best.HasSourceTarget = hasSourceTarget;
        }

        private static int AlertPriority(MoonAlertKind kind, int ticks)
        {
            switch (kind)
            {
                case MoonAlertKind.ActiveDeathray: return 1000000;
                case MoonAlertKind.DeathrayTelegraph:
                    return ticks <= 45 ? 900000 - ticks : 500000 - ticks;
                case MoonAlertKind.SphereRelease:
                    return ticks <= 18 ? 850000 - ticks : 460000 - ticks;
                case MoonAlertKind.Bolts:
                    return ticks <= 10 ? 760000 - ticks : 300000 - ticks;
                case MoonAlertKind.SpinBarrage:
                    return ticks == 0 ? 720000 : 280000 - ticks;
                case MoonAlertKind.Tongue: return 600000;
                default: return 0;
            }
        }
    }

    internal sealed class CompositeBossStrategy : BossStrategyBase
    {
        public CompositeBossStrategy() : base("multi-boss-composite", 1800, 760, 7f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => true;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = default(TargetSnapshot);
            var found = false;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (!candidate.Boss || candidate.Life <= 0) continue;
                if (!found || target.Invulnerable && !candidate.Invulnerable ||
                    target.Invulnerable == candidate.Invulnerable && candidate.Life < target.Life)
                { target = candidate; found = true; }
            }
            return Decision(s, target, "maximin-composite", BossPattern.Composite, 720, -170,
                CompositeEscape(s, m), 0, true, true, s.Mobility.CanFlipGravity, 110);
        }
    }
}
