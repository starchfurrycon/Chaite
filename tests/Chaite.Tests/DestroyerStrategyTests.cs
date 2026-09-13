using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunDestroyerStrategyRegressions()
        {
            Run("Destroyer P1 acquires a real bounded support", DestroyerAcquiresRealAnchor);
            Run("Destroyer P1 fails closed without a support", DestroyerRejectsMissingAnchor);
            Run("Destroyer P1 can acquire a reachable airborne landing support", DestroyerAcquiresReachableLandingAnchor);
            Run("Destroyer P1 never follows the head height", DestroyerDoesNotFollowHeadHeight);
            Run("Destroyer P1 turns using measured stopping travel", DestroyerTurnsBeforeStoppingBoundary);
            Run("Destroyer P1 locks the head-exit side across a crossing", DestroyerHeadExitDirectionIsCommitted);
            Run("Destroyer P1 does not leave support on low wing reserve", DestroyerLowReserveDoesNotLaunch);
            Run("Destroyer P1 returns toward its saved anchor while airborne", DestroyerAirborneReturnUsesSavedAnchor);
            Run("Destroyer P1 does not accept equal-height footing outside the anchor", DestroyerEqualHeightOutsideAnchorStillRecovers);
            Run("Destroyer P1 requires landing and resource restoration", DestroyerLandingAloneDoesNotCompleteRecovery);
            Run("Destroyer P1 fails closed when a body sweep seals the return", DestroyerBodySweepBlocksRecovery);
            Run("Destroyer P1 malformed native projectile closes the reviewed route", DestroyerMalformedNativeThreatClosesRoute);
            Run("Destroyer P1 blocked head exit stays on its support", DestroyerBlockedHeadExitDoesNotLaunch);
            Run("Destroyer P1 ceiling guard never becomes a platform drop", DestroyerCeilingGuardStopsInsteadOfDropping);
            Run("Destroyer P1 rejects unreviewed runtime fixtures", DestroyerUnsupportedFixturesFailClosed);
            Run("Destroyer P1 fails closed outside its native single-player target scope", DestroyerNativeScopeFailsClosed);
            Run("Destroyer P1 requires a trusted head branch and grounded reacquisition", DestroyerBranchTrustRequiresGroundedReacquisition);
            Run("Destroyer P1 worm movement expands the conservative head envelope", DestroyerWormBranchExpandsHeadEnvelope);
            Run("Destroyer P1 target changes discard the anchor until a new landing", DestroyerTargetChangeRequiresGroundedReacquisition);
            Run("Destroyer summon requirements enforce the native scope before use", DestroyerRequirementsEnforceNativeScope);
            Run("Destroyer summon requirements enforce the complete P1 fixture before use", DestroyerRequirementsEnforceCompleteFixture);
            Run("Destroyer summon requirements need a current continuous runway and no other Boss", DestroyerRequirementsNeedRunwayAndSoloEncounter);
            Run("Destroyer P1 distinguishes exact Demon Wings and Lightning Boots identities", DestroyerRequiresExactEquipmentIdentity);
            Run("Destroyer P1 rejects unreviewed game modes and special worlds", DestroyerRejectsUnreviewedNativeWorlds);
            Run("Destroyer P1 rejects null and non-finite arena observations", DestroyerRejectsInvalidArenaObservations);
            Run("Destroyer rejected plans cannot be overridden by generic movement tools", DestroyerRejectedPlanStaysNeutral);
            Run("Destroyer transient unknown branch recovers but persistent loss returns control", DestroyerPersistentContractLossReturnsControl);
            Run("Destroyer persistent blocked recovery returns control", DestroyerPersistentBlockedRecoveryReturnsControl);
            Run("Destroyer no-progress watchdog eventually returns control", DestroyerNoProgressWatchdogReturnsControl);
            Run("Destroyer control return plan is neutral and bypasses optional mobility", DestroyerControlReturnPlanIsNeutral);
            Run("Destroyer P1 reset and strategy re-entry discard the old anchor", DestroyerResetAndReentryClearAnchor);
            Run("Destroyer P1 Probe pressure has a bounded clear hysteresis", DestroyerProbePressureHasHysteresis);
            Run("Destroyer P1 selects a near visible Werewolf without losing the head pattern", DestroyerSelectsNearVisibleWerewolf);
            Run("Destroyer P1 does not select a distant ordinary hostile", DestroyerDoesNotSelectFarOrdinaryHostile);
            Run("Destroyer P1 does not select blocked or unknown ordinary hostiles", DestroyerDoesNotSelectUnverifiedOrdinaryHostile);
            Run("Destroyer P1 safely reacquires after grounded anchor loss", DestroyerReacquiresGroundedReplacementAnchor);
            Run("Destroyer P1 does not invent an airborne replacement anchor", DestroyerDoesNotReacquireAnchorInAir);
            Run("Destroyer P1 explicit recovery scoring ignores head altitude", DestroyerExplicitScoringIgnoresHeadAltitude);
            Run("Destroyer P1 explicit controller does not auto-dash or mount", DestroyerExplicitControllerOwnsMobilityTools);
            Run("Destroyer P1 strategy hot path constructs no collections and calls no LINQ", DestroyerHotPathHasNoCollectionsOrLinq);
        }

        private static CombatSnapshot DestroyerP1Snapshot()
        {
            var s = CombatScenario(134);
            s.Targets.Clear();
            s.Player.Position = new Vec2(790f, 958f);
            s.Player.Velocity = new Vec2(0f, 0f);
            s.Player.Width = 20;
            s.Player.Height = 42;
            s.Player.OnGround = true;
            s.Player.Gravity = .4f;
            s.Player.MaxFallSpeed = 10f;
            s.Player.BaseRunSpeed = 3f;
            s.Player.MaxRunSpeed = 8f;
            s.Player.RunAcceleration = .2f;
            s.Player.SprintAcceleration = .08f;
            s.Player.RunSlowdown = .3f;
            s.Player.CanSprintInAir = true;
            s.Player.Jump = new JumpSnapshot
            {
                Known = true, Speed = 7f, Height = 15, ReleaseReady = true
            };
            s.Player.Flight = new FlightSnapshot
            {
                Known = true, WingsLogic = 1, RocketBoots = 2,
                WingTime = 100f, WingTimeMax = 100,
                RocketTime = 7, RocketTimeMax = 7
            };
            s.Player.WingTime = 100f;
            s.Player.RocketTime = 7f;
            s.Player.FunctionalEquipmentIdentityKnown = true;
            s.Player.WingAccessoryItemType = 492;
            s.Player.RocketBootAccessoryItemType = 898;
            s.Mobility.HasFiniteFlightResource = true;
            s.Mobility.FlightResourceFraction = 1f;
            s.Mobility.GravityInverted = false;
            s.Mobility.MountActive = false;
            s.Mobility.MountCanFly = false;
            s.Mobility.Grappling = false;
            s.Arena.ClearanceLeft = 1200f;
            s.Arena.ClearanceRight = 1200f;
            s.Arena.ClearanceUp = 800f;
            s.Arena.ClearanceDown = 500f;
            s.Arena.HasFloor = true;
            s.Arena.LocalOpenBounds = new RectF(-400f, 0f, 2600f, 1000f);
            s.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 0f, Right = 1800f, SurfaceY = 1000f
            };
            s.Arena.RecoverySupport = s.Arena.FloorSupport;
            s.Difficulty.GameModeKnown = true;
            s.Difficulty.GameMode = 0;
            s.Targets.Add(DestroyerP1Target(10, 134, 1050f, 300f, true));
            return s;
        }

        private static TargetSnapshot DestroyerP1Target(int key, int type, float centerX, float centerY, bool boss = false)
        {
            return new TargetSnapshot
            {
                Key = key,
                Type = type,
                Position = new Vec2(centerX - 40f, centerY - 40f),
                Velocity = new Vec2(0f, 0f),
                Width = 80,
                Height = 80,
                Life = 80000,
                LifeMax = 80000,
                Damage = 100,
                Boss = boss,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0,
                DestroyerBranchKnown = type == 134,
                DestroyerUsesWormMovement = false
            };
        }

        private static void SetDestroyerHead(CombatSnapshot s, float centerX, float centerY, float vx, float vy)
        {
            var head = s.Targets[0];
            head.Position = new Vec2(centerX - head.Width * .5f, centerY - head.Height * .5f);
            head.Velocity = new Vec2(vx, vy);
            s.Targets[0] = head;
        }

        private static void SetDestroyerResource(CombatSnapshot s, float wing, int rocket)
        {
            var flight = s.Player.Flight;
            flight.WingTime = wing;
            flight.RocketTime = rocket;
            s.Player.Flight = flight;
            s.Player.WingTime = wing;
            s.Player.RocketTime = rocket;
            s.Mobility.FlightResourceFraction = FlightMotion.ResourceFraction(in flight);
        }

        private static BossStrategyEngine SupportedDestroyer(CombatSnapshot s)
        {
            var engine = new BossStrategyEngine();
            True(engine.Evaluate(s).Directive.PhaseId.Contains("acquire-anchor"));
            True(engine.Evaluate(s).Directive.PhaseId.Contains("supported-pressure"));
            return engine;
        }

        private static ControlPlanDirectiveView View(BossDecision decision)
        {
            return new ControlPlanDirectiveView
            {
                Phase = decision.Directive.PhaseId,
                Horizontal = decision.Directive.HorizontalIntent,
                Vertical = decision.Directive.VerticalIntent,
                Jump = decision.Directive.JumpAction
            };
        }

        private struct ControlPlanDirectiveView
        {
            public string Phase;
            public int Horizontal;
            public int Vertical;
            public JumpAction Jump;
        }

        private static ControlPlanDirectiveView EnterDestroyerRecovery(CombatSnapshot s, BossStrategyEngine engine)
        {
            SetDestroyerHead(s, 500f, 1200f, 8f, -12f);
            var exit = View(engine.Evaluate(s));
            True(exit.Phase.Contains("head-exit-committed"));
            Equal(1, exit.Horizontal);

            s.Player.OnGround = false;
            s.Player.Position = new Vec2(100f, 700f);
            s.Player.Velocity = new Vec2(0f, 0f);
            SetDestroyerHead(s, 2600f, 200f, 0f, 0f);
            var result = default(ControlPlanDirectiveView);
            for (var tick = 0; tick < 11; tick++) result = View(engine.Evaluate(s));
            True(result.Phase.Contains("recover-anchor"), result.Phase);
            return result;
        }

        private static void DestroyerAcquiresRealAnchor()
        {
            var s = DestroyerP1Snapshot();
            var engine = new BossStrategyEngine();
            var acquire = engine.Evaluate(s).Directive;
            Equal("classic-acquire-anchor", acquire.PhaseId);
            Equal(0, acquire.HorizontalIntent);
            Equal(0, acquire.VerticalIntent);
            Equal(JumpAction.Release, acquire.JumpAction);
            True(acquire.UseExplicitMovement);
            Equal(0f, acquire.VerticalOffset);
            Equal(0f, acquire.FloorClearance);
            True(engine.Evaluate(s).Directive.PhaseId.Contains("supported-pressure"));
        }

        private static void DestroyerRejectsMissingAnchor()
        {
            var s = DestroyerP1Snapshot();
            s.Arena.FloorSupport = s.Arena.RecoverySupport = default(SupportSpan);
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            True(d.PhaseId.Contains("acquire-anchor-no-safe-support"));
            Equal(0, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);
        }

        private static void DestroyerAcquiresReachableLandingAnchor()
        {
            var s = DestroyerP1Snapshot();
            s.Player.OnGround = false;
            s.Player.Position = new Vec2(790f, 700f);
            s.Arena.FloorSupport = default(SupportSpan);
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-acquire-anchor", d.PhaseId);
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);
        }

        private static void DestroyerDoesNotFollowHeadHeight()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            for (var tick = 0; tick < 80; tick++)
            {
                SetDestroyerHead(s, 2600f, -800f + tick * 42f, 0f, 0f);
                var d = engine.Evaluate(s).Directive;
                Equal(0, d.VerticalIntent);
                Equal(0f, d.VerticalOffset);
                Equal(JumpAction.Release, d.JumpAction);
            }
        }

        private static void DestroyerTurnsBeforeStoppingBoundary()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            s.Player.Position.X = 1490f;
            s.Player.Velocity = new Vec2(8f, 0f);
            SetDestroyerHead(s, 100f, 200f, 0f, 0f);
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void DestroyerHeadExitDirectionIsCommitted()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            SetDestroyerHead(s, 500f, 1200f, 8f, -12f);
            var first = engine.Evaluate(s).Directive;
            Equal(1, first.HorizontalIntent);
            True(first.PhaseId.Contains("head-exit-committed"));
            Equal(1, first.VerticalIntent);

            SetDestroyerHead(s, 1100f, 1200f, -8f, -12f);
            var crossed = engine.Evaluate(s).Directive;
            Equal(1, crossed.HorizontalIntent);
            True(crossed.PhaseId.Contains("head-exit-committed"));
        }

        private static void DestroyerLowReserveDoesNotLaunch()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            SetDestroyerResource(s, 16f, 0);
            SetDestroyerHead(s, 500f, 1200f, 8f, -12f);
            var d = engine.Evaluate(s).Directive;
            // Grounded lateral evasion remains available, but protected landing
            // reserve can never be spent on a speculative takeoff.
            True(d.PhaseId.Contains("head-exit"), d.PhaseId);
            True(d.HorizontalIntent != 0);
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);
        }

        private static void DestroyerAirborneReturnUsesSavedAnchor()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            var recovery = EnterDestroyerRecovery(s, engine);
            Equal(1, recovery.Horizontal);
        }

        private static void DestroyerEqualHeightOutsideAnchorStillRecovers()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            EnterDestroyerRecovery(s, engine);
            s.Player.OnGround = true;
            s.Player.Position = new Vec2(2200f, 958f);
            s.Player.Velocity = new Vec2(0f, 0f);
            s.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 1900f, Right = 3200f, SurfaceY = 1000f
            };
            // The adapter retains the original row separately for bounded return.
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 0f, Right = 1800f, SurfaceY = 1000f
            };
            var d = engine.Evaluate(s).Directive;
            True(d.PhaseId.Contains("recover-anchor"), d.PhaseId);
            False(d.PhaseId.Contains("complete"));
            Equal(-1, d.HorizontalIntent);
        }

        private static void DestroyerLandingAloneDoesNotCompleteRecovery()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            EnterDestroyerRecovery(s, engine);
            s.Player.OnGround = true;
            s.Player.Position = new Vec2(790f, 958f);
            s.Player.Velocity = new Vec2(0f, 0f);
            SetDestroyerResource(s, 20f, 0);
            var landed = engine.Evaluate(s).Directive;
            True(landed.PhaseId.Contains("recover-anchor-recharge"), landed.PhaseId);
            False(landed.PhaseId.Contains("complete"));

            SetDestroyerResource(s, 100f, 7);
            Equal("classic-recover-anchor-complete", engine.Evaluate(s).Directive.PhaseId);
            True(engine.Evaluate(s).Directive.PhaseId.Contains("supported-pressure"));
        }

        private static void DestroyerBodySweepBlocksRecovery()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            EnterDestroyerRecovery(s, engine);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Type = 135,
                Position = new Vec2(120f, 630f),
                Velocity = new Vec2(0f, 0f),
                Width = 320,
                Height = 180,
                Damage = 100,
                TimeLeft = int.MaxValue
            });
            var d = engine.Evaluate(s).Directive;
            Equal(0, d.HorizontalIntent);
            True(d.PhaseId.Contains("route-blocked"), d.PhaseId);
        }

        private static void DestroyerMalformedNativeThreatClosesRoute()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.BouncingFallingHostileBolt,
                Type = 921,
                Position = new Vec2(6000f, 0f),
                Width = 12,
                Height = 12,
                Damage = 30,
                // The final native update is still damaging before TTL Kill.
                TimeLeft = 1,
                TrajectoryAi0 = float.NaN
            });

            var closed = engine.Evaluate(s).Directive;
            Equal(0, closed.HorizontalIntent);
            True(closed.PhaseId.Contains("supported-pressure-brake"),
                closed.PhaseId);
        }

        private static void DestroyerBlockedHeadExitDoesNotLaunch()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Type = 104,
                Position = new Vec2(690f, 930f),
                Width = 40,
                Height = 80,
                Damage = 70,
                TimeLeft = int.MaxValue
            });
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Type = 104,
                Position = new Vec2(850f, 930f),
                Width = 40,
                Height = 80,
                Damage = 70,
                TimeLeft = int.MaxValue
            });
            SetDestroyerHead(s, 500f, 1200f, 8f, -12f);

            var d = engine.Evaluate(s).Directive;
            True(d.PhaseId.Contains("head-exit-committed-blocked"), d.PhaseId);
            Equal(0, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);

            var planSnapshot = DestroyerP1Snapshot();
            var planner = new CombatPlanner(new PlannerSettings());
            planner.Plan(planSnapshot);
            planSnapshot.Threats.Add(s.Threats[0]);
            planSnapshot.Threats.Add(s.Threats[1]);
            SetDestroyerHead(planSnapshot, 500f, 1200f, 8f, -12f);
            var plan = planner.Plan(planSnapshot);
            True(plan.PhaseId.Contains("head-exit-committed-blocked"), plan.PhaseId);
            Equal(0, plan.Horizontal);
            False(plan.Jump);
            False(plan.Drop);
        }

        private static void DestroyerCeilingGuardStopsInsteadOfDropping()
        {
            var s = DestroyerP1Snapshot();
            var planner = new CombatPlanner(new PlannerSettings());
            planner.Plan(s);
            s.Arena.ClearanceUp = 20f;
            SetDestroyerHead(s, 500f, 1200f, 8f, -12f);

            var plan = planner.Plan(s);
            True(plan.PhaseId.Contains("head-exit-committed"), plan.PhaseId);
            False(plan.Jump);
            False(plan.Drop, "a blocked ascent was inverted into a one-way-platform drop");
        }

        private static void DestroyerUnsupportedFixturesFailClosed()
        {
            var s = DestroyerP1Snapshot();
            s.Difficulty.Expert = true;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            s = DestroyerP1Snapshot();
            s.Mobility.GravityInverted = true;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            s = DestroyerP1Snapshot();
            var flight = s.Player.Flight; flight.Known = false; s.Player.Flight = flight;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            s = DestroyerP1Snapshot();
            flight = s.Player.Flight; flight.WingsLogic = 2; s.Player.Flight = flight;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            // NativeFlightReader can describe bare Demon Wings, but this P1
            // combat controller has only been bounded for the recorded Demon
            // Wings + Lightning Boots fixture.
            s = DestroyerP1Snapshot();
            flight = s.Player.Flight; flight.RocketBoots = 0; s.Player.Flight = flight;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            s = DestroyerP1Snapshot();
            s.Targets.Add(DestroyerP1Target(11, 134, 2600f, 300f, true));
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));
        }

        private static void DestroyerNativeScopeFailsClosed()
        {
            var s = DestroyerP1Snapshot();
            s.NativeContextKnown = false;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            foreach (var mode in new[] { 1, 2 })
            {
                s = DestroyerP1Snapshot();
                s.NetMode = mode;
                AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));
            }

            s = DestroyerP1Snapshot();
            var head = s.Targets[0];
            head.NativeTargetKnown = false;
            s.Targets[0] = head;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            foreach (var targetPlayer in new[] { 1, 255, -1 })
            {
                s = DestroyerP1Snapshot();
                head = s.Targets[0];
                head.NativeTargetPlayerIndex = targetPlayer;
                s.Targets[0] = head;
                AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));
            }
        }

        private static void DestroyerBranchTrustRequiresGroundedReacquisition()
        {
            var s = DestroyerP1Snapshot();
            var head = s.Targets[0];
            head.DestroyerBranchKnown = false;
            s.Targets[0] = head;
            var engine = new BossStrategyEngine();
            AssertDestroyerStopped(engine.Evaluate(s));

            head.DestroyerBranchKnown = true;
            s.Targets[0] = head;
            s.Player.OnGround = false;
            var airborne = engine.Evaluate(s).Directive;
            True(airborne.PhaseId.Contains("reacquire-anchor-airborne-closed"), airborne.PhaseId);
            Equal(0, airborne.HorizontalIntent);
            Equal(0, airborne.VerticalIntent);

            s.Player.OnGround = true;
            Equal("classic-reacquire-anchor", engine.Evaluate(s).Directive.PhaseId);
            True(engine.Evaluate(s).Directive.PhaseId.Contains("supported-pressure"));

            // A later history discontinuity must discard the established route,
            // not coast for one frame on the old anchor.
            head = s.Targets[0];
            head.DestroyerBranchKnown = false;
            s.Targets[0] = head;
            AssertDestroyerStopped(engine.Evaluate(s));
            head.DestroyerBranchKnown = true;
            s.Targets[0] = head;
            s.Player.OnGround = false;
            True(engine.Evaluate(s).Directive.PhaseId.Contains("reacquire-anchor-airborne-closed"));
        }

        private static void DestroyerWormBranchExpandsHeadEnvelope()
        {
            var air = DestroyerP1Snapshot();
            SetDestroyerHead(air, air.Player.Center.X + 560f, air.Player.Center.Y, -5f, 0f);
            var airEngine = new BossStrategyEngine();
            airEngine.Evaluate(air);
            var airDecision = airEngine.Evaluate(air).Directive;
            False(airDecision.PhaseId.Contains("head-exit"), airDecision.PhaseId);

            var worm = DestroyerP1Snapshot();
            var head = worm.Targets[0];
            head.DestroyerUsesWormMovement = true;
            worm.Targets[0] = head;
            SetDestroyerHead(worm, worm.Player.Center.X + 560f, worm.Player.Center.Y, -5f, 0f);
            var wormEngine = new BossStrategyEngine();
            wormEngine.Evaluate(worm);
            var wormDecision = wormEngine.Evaluate(worm).Directive;
            True(wormDecision.PhaseId.Contains("head-exit"), wormDecision.PhaseId);
        }

        private static void DestroyerTargetChangeRequiresGroundedReacquisition()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            var head = s.Targets[0];
            head.NativeTargetPlayerIndex = 1;
            s.Targets[0] = head;
            AssertDestroyerStopped(engine.Evaluate(s));

            head.NativeTargetPlayerIndex = s.LocalPlayerIndex;
            s.Targets[0] = head;
            s.Player.OnGround = false;
            var airborne = engine.Evaluate(s).Directive;
            True(airborne.PhaseId.Contains("reacquire-anchor-airborne-closed"), airborne.PhaseId);
            Equal(0, airborne.HorizontalIntent);
            Equal(0, airborne.VerticalIntent);
            Equal(JumpAction.Release, airborne.JumpAction);

            s.Player.OnGround = true;
            var landed = engine.Evaluate(s).Directive;
            Equal("classic-reacquire-anchor", landed.PhaseId);
            Equal(0, landed.HorizontalIntent);
            Equal(JumpAction.Release, landed.JumpAction);
        }

        private static void DestroyerRequirementsEnforceNativeScope()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var s = DestroyerP1Snapshot();
            True(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason), reason);

            // A real pre-spawn snapshot contains no Destroyer yet. Native single-
            // player context is still mandatory, while NPC.target is deferred
            // until the head actually exists.
            s.Targets.Clear();
            True(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason), reason);
            s.NativeContextKnown = false;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("网络模式"), reason);

            s = DestroyerP1Snapshot();
            s.NetMode = 1;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("单机"), reason);

            s = DestroyerP1Snapshot();
            var head = s.Targets[0];
            head.NativeTargetKnown = false;
            s.Targets[0] = head;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("目标玩家"), reason);
            False(planner.RequirementsMet(s, out reason));

            head.NativeTargetKnown = true;
            head.NativeTargetPlayerIndex = 1;
            s.Targets[0] = head;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("本地玩家"), reason);
            False(planner.RequirementsMet(s, out reason));
        }

        private static void DestroyerRequirementsEnforceCompleteFixture()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var supported = DestroyerP1Snapshot();
            supported.Targets.Clear();
            True(planner.RequirementsMetForExpected(supported, "mechanical-worm", 134, out reason), reason);

            var expert = DestroyerP1Snapshot();
            expert.Targets.Clear();
            expert.Difficulty.Expert = true;
            False(planner.RequirementsMetForExpected(expert, "mechanical-worm", 134, out reason));

            var skyblock = DestroyerP1Snapshot();
            skyblock.Targets.Clear();
            skyblock.Difficulty.Skyblock = true;
            False(planner.RequirementsMetForExpected(skyblock, "mechanical-worm", 134, out reason));

            var wrongWings = DestroyerP1Snapshot();
            wrongWings.Targets.Clear();
            var flight = wrongWings.Player.Flight;
            flight.WingsLogic = 2;
            wrongWings.Player.Flight = flight;
            False(planner.RequirementsMetForExpected(wrongWings, "mechanical-worm", 134, out reason));

            var mount = DestroyerP1Snapshot();
            mount.Targets.Clear();
            mount.Mobility.MountActive = true;
            False(planner.RequirementsMetForExpected(mount, "mechanical-worm", 134, out reason));

            // These describe dormant, optional escape tools. The native facade
            // reports MountCanFly for the best available inventory mount even
            // while MountActive is false; better optional gear must not make the
            // Demon-Wings/Lightning-Boots baseline fail admission.
            var optional = DestroyerP1Snapshot();
            optional.Targets.Clear();
            optional.Mobility.HasUsableMount = true;
            optional.Mobility.MountCanFly = true;
            optional.Mobility.MountRunSpeed = 20f;
            optional.Mobility.CanDash = true;
            optional.Mobility.DashReady = true;
            optional.Mobility.DashType = 2;
            optional.Mobility.HasGrapple = true;
            optional.Mobility.CanFlipGravity = true;
            True(planner.RequirementsMetForExpected(optional, "mechanical-worm", 134, out reason), reason);

            var attachedHook = DestroyerP1Snapshot();
            attachedHook.Targets.Clear();
            attachedHook.Mobility.HasGrapple = true;
            attachedHook.Mobility.Grappling = true;
            False(planner.RequirementsMetForExpected(attachedHook, "mechanical-worm", 134, out reason));

            var invertedGravity = DestroyerP1Snapshot();
            invertedGravity.Targets.Clear();
            invertedGravity.Mobility.CanFlipGravity = true;
            invertedGravity.Mobility.GravityInverted = true;
            False(planner.RequirementsMetForExpected(invertedGravity, "mechanical-worm", 134, out reason));

            var observedUnknownBranch = DestroyerP1Snapshot();
            var head = observedUnknownBranch.Targets[0];
            head.DestroyerBranchKnown = false;
            observedUnknownBranch.Targets[0] = head;
            False(planner.RequirementsMetForExpected(observedUnknownBranch, "mechanical-worm", 134, out reason));
        }

        private static void DestroyerRequirementsNeedRunwayAndSoloEncounter()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var s = DestroyerP1Snapshot();
            s.Targets.Clear();
            True(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason), reason);

            s.Arena.FloorSupport = default(SupportSpan);
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("支撑"), reason);

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Player.OnGround = false;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            var floor = s.Arena.FloorSupport;
            floor.Left = s.Player.Position.X + s.Player.Width + 8f;
            floor.Right = floor.Left + 1800f;
            s.Arena.FloorSupport = floor;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Targets.Add(new TargetSnapshot
            {
                Key = 44, Type = 4, Boss = true, Life = 2800, LifeMax = 2800,
                Width = 100, Height = 100, Position = new Vec2(8000f, 500f)
            });
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            True(reason.Contains("其他 Boss"), reason);
        }

        private static void DestroyerRequiresExactEquipmentIdentity()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var s = DestroyerP1Snapshot();
            s.Targets.Clear();
            True(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason), reason);

            s.Player.FunctionalEquipmentIdentityKnown = false;
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Player.WingAccessoryItemType = 493; // Angel Wings also report wingsLogic 1.
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Player.RocketBootAccessoryItemType = 405; // Spectre Boots also report rocketBoots 2.
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));

            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Player.RocketBootAccessoryItemType = -1; // Multiple effect sources are not the baseline.
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
        }

        private static void DestroyerRejectsUnreviewedNativeWorlds()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            foreach (var mutate in new Action<DifficultySnapshot>[]
            {
                d => d.GameModeKnown = false,
                d => d.GameMode = 1,
                d => { d.GameMode = 3; d.Journey = true; },
                d => d.Drunk = true,
                d => d.NotTheBees = true
            })
            {
                var s = DestroyerP1Snapshot();
                s.Targets.Clear();
                mutate(s.Difficulty);
                False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
            }
        }

        private static void DestroyerRejectsInvalidArenaObservations()
        {
            var engine = new BossStrategyEngine();
            var s = DestroyerP1Snapshot();
            s.Arena = null;
            AssertDestroyerStopped(engine.Evaluate(s));
            var invalidPlan = new CombatPlanner(new PlannerSettings()).Plan(s);
            True(invalidPlan.RequestControlReturn);
            Equal(0, invalidPlan.Horizontal);
            False(invalidPlan.Jump || invalidPlan.Fire || invalidPlan.Dash || invalidPlan.Hook ||
                invalidPlan.ToggleMount || invalidPlan.GravityControl != 0);

            s = DestroyerP1Snapshot();
            s.Arena.SafeCenter = new Vec2(float.NaN, 500f);
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            s = DestroyerP1Snapshot();
            var floor = s.Arena.FloorSupport;
            floor.SurfaceY = float.PositiveInfinity;
            s.Arena.FloorSupport = floor;
            AssertDestroyerStopped(new BossStrategyEngine().Evaluate(s));

            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            s = DestroyerP1Snapshot();
            s.Targets.Clear();
            s.Arena.LocalOpenBounds = new RectF(0f, 0f, float.NaN, 1000f);
            False(planner.RequirementsMetForExpected(s, "mechanical-worm", 134, out reason));
        }

        private static void DestroyerPersistentContractLossReturnsControl()
        {
            var s = DestroyerP1Snapshot();
            var head = s.Targets[0];
            head.DestroyerBranchKnown = false;
            s.Targets[0] = head;
            var engine = new BossStrategyEngine();
            var first = engine.Evaluate(s).Directive;
            False(first.RequestControlReturn, "one untrusted first observation must receive a bounded grace window");

            head.DestroyerBranchKnown = true;
            s.Targets[0] = head;
            False(engine.Evaluate(s).Directive.RequestControlReturn,
                "a continuous second observation must recover without ending the session");

            head.DestroyerBranchKnown = false;
            s.Targets[0] = head;
            for (var tick = 1; tick < 12; tick++)
            {
                var directive = engine.Evaluate(s).Directive;
                False(directive.RequestControlReturn, "branch observation grace ended before 12 consecutive ticks");
            }
            var requested = engine.Evaluate(s).Directive;
            True(requested.RequestControlReturn, "persistent unknown native branch retained control indefinitely");
            Equal(0, requested.HorizontalIntent);
            Equal(0, requested.VerticalIntent);
            Equal(JumpAction.Release, requested.JumpAction);
            True(requested.ControlReturnReason.StartsWith("contract-lost:"), requested.ControlReturnReason);

            // A concrete incompatible profile gets a short confirmation window,
            // rather than the longer allowance reserved for a missing native row.
            s = DestroyerP1Snapshot();
            engine = SupportedDestroyer(s);
            s.Player.WingAccessoryItemType = 493;
            False(engine.Evaluate(s).Directive.RequestControlReturn);
            False(engine.Evaluate(s).Directive.RequestControlReturn);
            requested = engine.Evaluate(s).Directive;
            True(requested.RequestControlReturn);
            True(requested.ControlReturnReason.StartsWith("contract-lost:"), requested.ControlReturnReason);
        }

        private static void DestroyerPersistentBlockedRecoveryReturnsControl()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            EnterDestroyerRecovery(s, engine);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact, Geometry = ThreatGeometry.Body, Type = 135,
                Position = new Vec2(120f, 630f), Velocity = new Vec2(), Width = 320,
                Height = 180, Damage = 100, TimeLeft = int.MaxValue
            });
            for (var tick = 1; tick < 90; tick++)
            {
                var directive = engine.Evaluate(s).Directive;
                False(directive.RequestControlReturn, "route closure ended before its 90-tick recovery window");
            }
            var requested = engine.Evaluate(s).Directive;
            True(requested.RequestControlReturn, "a sealed return route retained control indefinitely");
            True(requested.PhaseId.Contains("route-blocked"), requested.PhaseId);
            True(requested.ControlReturnReason.StartsWith("route-closed:"), requested.ControlReturnReason);

            s = DestroyerP1Snapshot();
            engine = SupportedDestroyer(s);
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = default(SupportSpan);
            for (var tick = 1; tick < 90; tick++)
                False(engine.Evaluate(s).Directive.RequestControlReturn,
                    "anchor loss ended before its 90-tick reacquisition window");
            requested = engine.Evaluate(s).Directive;
            True(requested.RequestControlReturn);
            True(requested.ControlReturnReason.StartsWith("anchor-lost:"), requested.ControlReturnReason);
        }

        private static void DestroyerControlReturnPlanIsNeutral()
        {
            var s = DestroyerP1Snapshot();
            var head = s.Targets[0];
            head.DestroyerBranchKnown = false;
            s.Targets[0] = head;
            s.Mobility.CanDash = true;
            s.Mobility.DashReady = true;
            s.Mobility.HasGrapple = true;
            s.Mobility.HasUsableMount = true;
            s.Mobility.MountCanFly = true;
            s.Mobility.MountRunSpeed = 99f;
            s.Mobility.CanFlipGravity = true;
            var planner = new CombatPlanner(new PlannerSettings());
            var plan = default(ControlPlan);
            for (var tick = 0; tick < 240 && !plan.RequestControlReturn; tick++) plan = planner.Plan(s);
            True(plan.RequestControlReturn);
            Equal(0, plan.Horizontal);
            False(plan.Jump);
            Equal(JumpAction.Release, plan.JumpAction);
            False(plan.Drop);
            False(plan.Fire);
            False(plan.Dash);
            False(plan.Hook);
            False(plan.ToggleMount);
            Equal(0, plan.GravityControl);
        }

        private static void DestroyerNoProgressWatchdogReturnsControl()
        {
            var s = DestroyerP1Snapshot();
            SetDestroyerHead(s, 2600f, 200f, 0f, 0f);
            var engine = SupportedDestroyer(s);
            BossDirective requested = default(BossDirective);
            var tick = 0;
            for (; tick < 700; tick++)
            {
                requested = engine.Evaluate(s).Directive;
                if (requested.RequestControlReturn) break;
            }
            True(requested.RequestControlReturn, "a commanded but physically stationary loop retained control indefinitely");
            True(tick >= 590, "watchdog fired before the long no-progress window");
            True(requested.ControlReturnReason.StartsWith("watchdog:"), requested.ControlReturnReason);
        }

        private static void DestroyerRejectedPlanStaysNeutral()
        {
            var s = DestroyerP1Snapshot();
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 1f });
            planner.Plan(s);
            planner.Plan(s);
            var head = s.Targets[0];
            head.NativeTargetPlayerIndex = 1;
            s.Targets[0] = head;
            s.Mobility.CanDash = true;
            s.Mobility.DashReady = true;
            s.Mobility.HasUsableMount = true;
            s.Mobility.MountRunSpeed = 40f;
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Type = 104,
                Position = s.Player.Position,
                Width = s.Player.Width,
                Height = s.Player.Height,
                Damage = 999,
                TimeLeft = int.MaxValue
            });

            var plan = planner.Plan(s);
            True(plan.PhaseId.Contains("unsupported"), plan.PhaseId);
            Equal(1, planner.LastCandidateCount);
            Equal(0, plan.Horizontal);
            False(plan.Jump);
            Equal(JumpAction.Release, plan.JumpAction);
            False(plan.Drop);
            False(plan.Fire);
            False(plan.Dash);
            False(plan.Hook);
            False(plan.ToggleMount);
            Equal(0, plan.GravityControl);
        }

        private static void AssertDestroyerStopped(BossDecision decision)
        {
            True(decision != null);
            True(decision.Directive.PhaseId.Contains("unsupported"), decision.Directive.PhaseId);
            Equal(0, decision.Directive.HorizontalIntent);
            Equal(0, decision.Directive.VerticalIntent);
            Equal(JumpAction.Release, decision.Directive.JumpAction);
            True(decision.Directive.UseExplicitMovement);
            True(decision.Directive.OwnsMovementClosure);
        }

        private static void DestroyerResetAndReentryClearAnchor()
        {
            var first = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(first);
            engine.Reset();
            var second = DestroyerP1Snapshot();
            second.Player.Position.X = 3600f;
            second.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 2800f, Right = 4600f, SurfaceY = 1000f
            };
            second.Arena.RecoverySupport = second.Arena.FloorSupport;
            SetDestroyerHead(second, 4800f, 300f, 0f, 0f);
            Equal("classic-acquire-anchor", engine.Evaluate(second).Directive.PhaseId);

            True(engine.Evaluate(second).Directive.PhaseId.Contains("supported-pressure"));
            engine.Evaluate(EyeSnapshot());
            second.Player.Position.X = 6000f;
            second.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 5200f, Right = 7000f, SurfaceY = 1000f
            };
            second.Arena.RecoverySupport = second.Arena.FloorSupport;
            SetDestroyerHead(second, 7400f, 300f, 0f, 0f);
            Equal("classic-acquire-anchor", engine.Evaluate(second).Directive.PhaseId);
        }

        private static void DestroyerProbePressureHasHysteresis()
        {
            var s = DestroyerP1Snapshot();
            SetDestroyerHead(s, 1000f, 300f, 0f, 0f);
            for (var i = 0; i < 4; i++)
                s.Targets.Add(DestroyerP1Target(20 + i, 139, 1450f + i * 4f, 700f));
            var engine = new BossStrategyEngine();
            engine.Evaluate(s);
            var pressure = engine.Evaluate(s);
            Equal(139, pressure.Target.Type);
            True(pressure.Directive.PhaseId.Contains("probe-pressure"));

            s.Targets.RemoveRange(2, 3);
            for (var tick = 0; tick < 11; tick++)
            {
                pressure = engine.Evaluate(s);
                Equal(139, pressure.Target.Type);
                True(pressure.Directive.PhaseId.Contains("probe-pressure"));
            }
            var cleared = engine.Evaluate(s);
            Equal(134, cleared.Target.Type);
            False(cleared.Directive.PhaseId.Contains("probe-pressure"));
        }

        private static void DestroyerSelectsNearVisibleWerewolf()
        {
            var s = DestroyerP1Snapshot();
            var wolf = DestroyerP1Target(40, 104, s.Player.Center.X + 120f, s.Player.Center.Y);
            wolf.Boss = false;
            wolf.Damage = 70;
            s.Targets.Add(wolf);
            var decision = new BossStrategyEngine().Evaluate(s);
            Equal(104, decision.Target.Type);
            Equal(40, decision.Target.Key);
            Equal(134, decision.PatternTarget.Type);
            Equal(10, decision.PatternTarget.Key);
        }

        private static void DestroyerDoesNotSelectFarOrdinaryHostile()
        {
            var s = DestroyerP1Snapshot();
            var wolf = DestroyerP1Target(40, 104, s.Player.Center.X + 321f, s.Player.Center.Y);
            wolf.Boss = false;
            wolf.Damage = 70;
            s.Targets.Add(wolf);
            var decision = new BossStrategyEngine().Evaluate(s);
            Equal(134, decision.Target.Type);
            Equal(10, decision.PatternTarget.Key);
        }

        private static void DestroyerDoesNotSelectUnverifiedOrdinaryHostile()
        {
            var s = DestroyerP1Snapshot();
            var wolf = DestroyerP1Target(40, 104, s.Player.Center.X + 100f, s.Player.Center.Y);
            wolf.Boss = false;
            wolf.Damage = 70;
            wolf.HasLineOfSight = false;
            s.Targets.Add(wolf);
            Equal(134, new BossStrategyEngine().Evaluate(s).Target.Type);

            wolf.LineOfSightKnown = false;
            s.Targets[1] = wolf;
            Equal(134, new BossStrategyEngine().Evaluate(s).Target.Type);
        }

        private static void DestroyerReacquiresGroundedReplacementAnchor()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            s.Player.Position = new Vec2(3600f, 1158f);
            s.Player.Velocity = new Vec2(0f, 0f);
            s.Player.OnGround = true;
            s.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 2800f, Right = 4600f, SurfaceY = 1200f
            };
            s.Arena.RecoverySupport = s.Arena.FloorSupport;
            SetDestroyerHead(s, 5000f, 300f, 0f, 0f);
            var reacquire = engine.Evaluate(s).Directive;
            Equal("classic-reacquire-anchor", reacquire.PhaseId);
            Equal(0, reacquire.HorizontalIntent);
            Equal(JumpAction.Release, reacquire.JumpAction);
            True(engine.Evaluate(s).Directive.PhaseId.Contains("supported-pressure"));
        }

        private static void DestroyerDoesNotReacquireAnchorInAir()
        {
            var s = DestroyerP1Snapshot();
            var engine = SupportedDestroyer(s);
            s.Player.Position = new Vec2(3600f, 800f);
            s.Player.Velocity = new Vec2(0f, 0f);
            s.Player.OnGround = false;
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 2800f, Right = 4600f, SurfaceY = 1200f
            };
            SetDestroyerHead(s, 5000f, 300f, 0f, 0f);
            for (var tick = 0; tick < 2; tick++)
            {
                var stopped = engine.Evaluate(s).Directive;
                True(stopped.PhaseId.Contains("reacquire-anchor-airborne-closed"), stopped.PhaseId);
                Equal(0, stopped.HorizontalIntent);
                Equal(0, stopped.VerticalIntent);
                Equal(JumpAction.Release, stopped.JumpAction);
            }
        }

        private static void DestroyerExplicitScoringIgnoresHeadAltitude()
        {
            var s = DestroyerP1Snapshot();
            SetDestroyerHead(s, 2600f, -6000f, 0f, 0f);
            var planner = new CombatPlanner(new PlannerSettings());

            // The first evaluation records the real support anchor. Force the
            // bounded candidate search on the following tick without adding a
            // hazard: an explicit Boss controller must remain the source of
            // pattern geometry even while the generic planner is recovering.
            planner.Plan(s);
            typeof(CombatPlanner).GetField("_stuckTicks",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(planner, 1000);

            var plan = planner.Plan(s);
            Equal(2, planner.LastCandidateCount);
            True(plan.PhaseId.Contains("supported-pressure"), plan.PhaseId);
            True(plan.Horizontal >= 0, "generic scoring reversed the committed Destroyer runway");
            False(plan.Jump, "generic scoring followed the Destroyer head altitude");
            False(plan.Drop, "generic scoring inverted the explicit level route");
        }

        private static void DestroyerExplicitControllerOwnsMobilityTools()
        {
            var s = DestroyerP1Snapshot();
            var planner = new CombatPlanner(new PlannerSettings());
            planner.Plan(s); // Acquire the real support.
            s.Mobility.CanDash = true;
            s.Mobility.DashReady = true;
            s.Mobility.HasUsableMount = true;
            s.Mobility.MountCanFly = true;
            s.Mobility.MountRunSpeed = 20f;
            s.Mobility.HasGrapple = true;
            s.Mobility.CanFlipGravity = true;
            s.Mobility.FeatherFall = true;
            var featherJump = s.Player.Jump;
            featherJump.SlowFall = true;
            s.Player.Jump = featherJump;
            typeof(CombatPlanner).GetField("_emergencyHoldTicks",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(planner, 8);

            var plan = planner.Plan(s);
            True(plan.PhaseId.Contains("supported-pressure"), plan.PhaseId);
            True(plan.Horizontal != 0);
            False(plan.Dash, "generic emergency mode bypassed the explicit dash policy");
            False(plan.ToggleMount, "generic mobility changed the reviewed Destroyer fixture");
        }

        private static void DestroyerHotPathHasNoCollectionsOrLinq()
        {
            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(BossStrategyEngine).Assembly.Location))
            {
                Mono.Cecil.TypeDefinition strategy = null;
                for (var i = 0; i < assembly.MainModule.Types.Count; i++)
                {
                    var candidate = assembly.MainModule.Types[i];
                    if (candidate.FullName == "Chaite.Core.DestroyerStrategy") { strategy = candidate; break; }
                }
                True(strategy != null, "DestroyerStrategy IL type missing");
                for (var methodIndex = 0; methodIndex < strategy.Methods.Count; methodIndex++)
                {
                    var method = strategy.Methods[methodIndex];
                    if (!method.HasBody) continue;
                    for (var instructionIndex = 0; instructionIndex < method.Body.Instructions.Count; instructionIndex++)
                    {
                        var instruction = method.Body.Instructions[instructionIndex];
                        var called = instruction.Operand as Mono.Cecil.MethodReference;
                        if (called == null) continue;
                        False(called.DeclaringType.FullName.StartsWith("System.Linq.Enumerable", StringComparison.Ordinal),
                            method.Name + " calls LINQ: " + called.FullName);
                        if (instruction.OpCode.Code == Mono.Cecil.Cil.Code.Newobj)
                            False(called.DeclaringType.FullName.StartsWith("System.Collections", StringComparison.Ordinal),
                                method.Name + " constructs a collection: " + called.FullName);
                    }
                }
            }
        }
    }
}
