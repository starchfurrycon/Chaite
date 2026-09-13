using Chaite.Core;
using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunTwinsRegressions()
        {
            Run("Twins every reviewed classic native phase keeps final horizontal control", TwinsEveryNativePhaseKeepsPlannerHorizontal);
            Run("Twins commits launch charge brake and flame across crossings and pressure changes", TwinsCommittedSequenceKeepsDirection);
            Run("Twins continues after one eye dies without reversing", TwinsSingleEyeDeathKeepsDirection);
            Run("Twins closes a blocked committed route instead of crossing the eye", TwinsCommittedRouteClosesWithoutReverse);
            Run("Twins malformed native projectile closes the reviewed route", TwinsMalformedNativeThreatClosesRoute);
            Run("Twins empty-flight recovery returns to the saved runway with release", TwinsEmptyFlightReturnsWithRelease);
            Run("Twins resumes supported running only after landing and native resource recovery", TwinsRecoveryRequiresLandingAndResource);
            Run("Twins recovery has a finite fail-closed timeout", TwinsRecoveryTimeoutIsFinite);
            Run("Twins permits a second safe turn only after sixty full ticks", TwinsTurnHysteresisBoundary);
            Run("Twins initial side uses spawn position and measured runway bounds", TwinsInitialDirectionUsesSpawnAndBounds);
            Run("Twins keeps its original anchor when scan windows move", TwinsAnchorDoesNotDriftWithScanWindow);
            Run("Twins fails closed outside its reviewed native fixture", TwinsUnsupportedFixturesFailClosed);
            Run("Twins summon requirements reject unsupported fixtures before item use", TwinsPreSpawnRequirementsUseSameContract);
            Run("Twins reset and strategy re-entry discard the saved runway", TwinsResetAndReentryClearRunway);
            Run("Twins exact horizontal closure survives generic flight and candidate rewrites", TwinsPlannerCannotRewriteHorizontal);
            Run("Twins unsupported active grapple returns control without guessed inputs", TwinsRejectedPlanStaysNeutral);
            Run("Twins transient native and runway observations preserve its saved closure", TwinsTransientObservationsPreserveClosure);
            Run("Twins returns control after bounded contract and observation losses", TwinsBoundedObservationLossReturnsControl);
            Run("Twins returns control after ninety continuously closed route ticks", TwinsRouteClosedReturnsControl);
            Run("Twins recovery watchdog requires six hundred ticks without real progress", TwinsRecoveryWatchdogTracksProgress);
            Run("Twins planner emits a fully neutral control-return plan", TwinsPlannerControlReturnIsNeutral);
            Run("Twins fire honors the selected eye visibility and invulnerability", TwinsFireGuards);
            Run("Twins state machine hot path constructs no collections and calls no LINQ", TwinsHotPathHasNoCollectionsOrLinq);
        }

        private static CombatSnapshot TwinsSnapshot(bool ret = true, bool spaz = true)
        {
            var s = CombatScenario();
            s.Targets.Clear();
            s.Player.Position = new Vec2(1800f, 958f);
            s.Player.Velocity = new Vec2(-3f, 0f);
            s.Player.Width = 20;
            s.Player.Height = 42;
            s.Player.OnGround = true;
            s.Player.OnOneWaySupport = true;
            s.Player.Gravity = .4f;
            s.Player.MaxFallSpeed = 10f;
            s.Player.BaseRunSpeed = 3f;
            s.Player.MaxRunSpeed = 8f;
            s.Player.RunAcceleration = .2f;
            s.Player.SprintAcceleration = .08f;
            s.Player.RunSlowdown = .2f;
            s.Player.CanSprintInAir = true;
            s.Player.Jump = new JumpSnapshot
            {
                Known = true, Speed = 7f, Height = 15, ReleaseReady = true
            };
            s.Player.Flight = new FlightSnapshot
            {
                Known = true,
                WingsLogic = 1,
                RocketBoots = 2,
                WingTime = 100f,
                WingTimeMax = 100,
                RocketTime = 7,
                RocketTimeMax = 7,
                RocketDelay = 0,
                CanRocket = true,
                RocketRelease = true
            };
            s.Player.WingTime = 100f;
            s.Player.RocketTime = 7f;
            s.Player.FunctionalEquipmentIdentityKnown = true;
            s.Player.WingAccessoryItemType = 492;
            s.Player.RocketBootAccessoryItemType = 898;
            s.Mobility = new MobilitySnapshot
            {
                HasFiniteFlightResource = true,
                FlightResourceFraction = FlightMotion.ResourceFraction(in s.Player.Flight),
                // Native facade reports dashDelay<=0 even when dashType==0.
                DashReady = true
            };
            s.Arena = new ArenaSnapshot
            {
                LocalOpenBounds = new RectF(800f, 200f, 3600f, 1600f),
                SafeCenter = new Vec2(2400f, 1000f),
                ClearanceLeft = 600f,
                ClearanceRight = 1800f,
                ClearanceUp = 800f,
                ClearanceDown = 500f,
                HasFloor = true,
                FloorSupport = new SupportSpan
                {
                    Valid = true, OneWay = true, Left = 1200f, Right = 3620f, SurfaceY = 1000f
                },
                RecoverySupport = new SupportSpan
                {
                    Valid = true, OneWay = true, Left = 1200f, Right = 3620f, SurfaceY = 1000f
                }
            };
            s.NativeContextKnown = true;
            s.NetMode = 0;
            s.LocalPlayerIndex = 0;
            s.Difficulty = new DifficultySnapshot { GameModeKnown = true, GameMode = 0, DayTime = false };
            if (ret) s.Targets.Add(Twin(125, 51, 2300f));
            if (spaz) s.Targets.Add(Twin(126, 52, 2050f));
            return s;
        }

        private static TargetSnapshot Twin(int type, int key, float x)
        {
            return new TargetSnapshot
            {
                Key = key,
                Type = type,
                Position = new Vec2(x, 760f),
                Width = 100,
                Height = 100,
                Life = 10000,
                LifeMax = 10000,
                Damage = 70,
                Boss = true,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0
            };
        }

        private static void SetTwinsFlight(CombatSnapshot s, float wing, int rocket)
        {
            var flight = s.Player.Flight;
            flight.WingTime = wing;
            flight.RocketTime = rocket;
            s.Player.Flight = flight;
            s.Player.WingTime = wing;
            s.Player.RocketTime = rocket;
            s.Mobility.FlightResourceFraction = FlightMotion.ResourceFraction(in flight);
        }

        private static BossStrategyEngine SupportedTwins(CombatSnapshot s)
        {
            var engine = new BossStrategyEngine();
            var acquire = engine.Evaluate(s).Directive;
            True(acquire.PhaseId.Contains("acquire-runway"), acquire.PhaseId);
            True(acquire.HorizontalIntent != 0, "acquisition must not introduce a valid-phase H=0 tick");
            var supported = engine.Evaluate(s).Directive;
            True(supported.PhaseId.Contains("supported-run"), supported.PhaseId);
            True(supported.HorizontalIntent != 0);
            return engine;
        }

        private static void AssertTwinsClosed(BossDirective directive)
        {
            True(directive.PhaseId.Contains("unsupported") || directive.PhaseId.Contains("closed") ||
                 directive.PhaseId.Contains("no-safe-support"), directive.PhaseId);
            Equal(0, directive.HorizontalIntent);
            Equal(0, directive.VerticalIntent);
            Equal(JumpAction.Release, directive.JumpAction);
            True(directive.UseExplicitMovement);
        }

        private static void ConfigureNativePhase(CombatSnapshot s, int type, int phaseIndex)
        {
            var eye = s.Targets[0];
            eye.Ai0 = eye.Ai1 = eye.Ai2 = eye.Ai3 = 0f;
            eye.Life = eye.LifeMax = 10000;
            switch (phaseIndex)
            {
                case 0: break;
                case 1: eye.Ai1 = 1f; break;
                case 2: eye.Ai1 = 2f; eye.Ai2 = type == 126 ? 7f : 24f; break;
                case 3: eye.Ai1 = 2f; eye.Ai2 = type == 126 ? 8f : 25f; break;
                case 4: eye.Life = 3999; break;
                case 5: eye.Ai0 = 1f; break;
                case 6: eye.Ai0 = 2f; break;
                case 7: eye.Ai0 = 3f; eye.Ai1 = 0f; break;
                case 8: eye.Ai0 = 3f; eye.Ai1 = 1f; break;
                case 9: eye.Ai0 = 3f; eye.Ai1 = 2f; eye.Ai2 = 49f; break;
                case 10: eye.Ai0 = 3f; eye.Ai1 = 2f; eye.Ai2 = 50f; break;
                default: throw new InvalidOperationException("invalid phase fixture");
            }
            s.Targets[0] = eye;
        }

        private static void TwinsEveryNativePhaseKeepsPlannerHorizontal()
        {
            for (var typeIndex = 0; typeIndex < 2; typeIndex++)
            {
                var type = typeIndex == 0 ? 125 : 126;
                var lastPhase = type == 125 ? 8 : 10;
                for (var phase = 0; phase <= lastPhase; phase++)
                {
                    var s = TwinsSnapshot(type == 125, type == 126);
                    ConfigureNativePhase(s, type, phase);
                    var planner = new CombatPlanner(new PlannerSettings());
                    var first = planner.Plan(s);
                    True(first.Horizontal != 0, type + "/" + phase + " acquire: " + first.PhaseId);
                    var plan = planner.Plan(s);
                    True(plan.Horizontal != 0, type + "/" + phase + ": " + plan.PhaseId);
                    False(plan.PhaseId.Contains("unsupported"), plan.PhaseId);
                    False(plan.Dash);
                    False(plan.Hook);
                    False(plan.ToggleMount);
                    Equal(0, plan.GravityControl);
                }
            }
        }

        private static void TwinsCommittedSequenceKeepsDirection()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var initial = engine.Evaluate(s).Directive.HorizontalIntent;

            var spaz = s.Targets[1];
            spaz.Ai1 = 1f;
            s.Targets[1] = spaz;
            var launch = engine.Evaluate(s).Directive;
            True(launch.PhaseId.Contains("committed-escape"), launch.PhaseId);
            Equal(initial, launch.HorizontalIntent);

            spaz.Position.X = 1100f;
            spaz.Ai1 = 2f;
            spaz.Ai2 = 3f;
            s.Targets[1] = spaz;
            Equal(initial, engine.Evaluate(s).Directive.HorizontalIntent);

            var ret = s.Targets[0];
            ret.Ai1 = 1f;
            s.Targets[0] = ret;
            spaz.Ai2 = 8f;
            s.Targets[1] = spaz;
            var switched = engine.Evaluate(s).Directive;
            True(switched.PhaseId.Contains("ret"), switched.PhaseId);
            Equal(initial, switched.HorizontalIntent);

            ret.Ai1 = 0f;
            ret.Ai2 = 0f;
            s.Targets[0] = ret;
            spaz.Ai0 = 3f;
            spaz.Ai1 = 0f;
            spaz.Ai2 = 0f;
            s.Targets[1] = spaz;
            var flame = engine.Evaluate(s).Directive;
            True(flame.PhaseId.Contains("committed-escape"), flame.PhaseId);
            Equal(initial, flame.HorizontalIntent);
        }

        private static void TwinsSingleEyeDeathKeepsDirection()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var spaz = s.Targets[1];
            spaz.Ai1 = 1f;
            s.Targets[1] = spaz;
            var committed = engine.Evaluate(s).Directive.HorizontalIntent;
            s.Targets.RemoveAt(1);
            var survivor = engine.Evaluate(s).Directive;
            True(survivor.PhaseId.Contains("ret"), survivor.PhaseId);
            False(survivor.PhaseId.Contains("unsupported"), survivor.PhaseId);
            Equal(committed, survivor.HorizontalIntent);
            True(survivor.Fire);
        }

        private static void TwinsCommittedRouteClosesWithoutReverse()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var spaz = s.Targets[1];
            spaz.Ai1 = 1f;
            s.Targets[1] = spaz;
            var direction = engine.Evaluate(s).Directive.HorizontalIntent;
            True(direction < 0);

            s.Player.Position.X = 1204f;
            s.Player.Velocity.X = -7f;
            s.Arena.ClearanceLeft = 4f;
            spaz.Position.X = 900f;
            spaz.Ai1 = 2f;
            spaz.Ai2 = 3f;
            s.Targets[1] = spaz;
            var closed = engine.Evaluate(s).Directive;
            Equal(0, closed.HorizontalIntent);
            True(closed.PhaseId.Contains("route-closed"), closed.PhaseId);
            False(closed.HorizontalIntent == -direction, "a closed committed route reversed through the eye");
        }

        private static void TwinsMalformedNativeThreatClosesRoute()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
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
            True(closed.PhaseId.Contains("route-closed"), closed.PhaseId);
        }

        private static void TwinsEmptyFlightReturnsWithRelease()
        {
            var s = TwinsSnapshot();
            var planner = new CombatPlanner(new PlannerSettings { PatternSafeRiskThreshold = 0f });
            planner.Plan(s);
            planner.Plan(s);
            s.Player.OnGround = false;
            s.Player.OnOneWaySupport = false;
            s.Player.Position = new Vec2(1000f, 800f);
            s.Player.Velocity = new Vec2(-4f, 1f);
            SetTwinsFlight(s, 0f, 0);
            string reason;
            False(planner.RequirementsMet(s, out reason));
            True(reason.Contains("耗尽"), reason);

            for (var tick = 0; tick < 8; tick++)
            {
                var plan = planner.Plan(s);
                True(plan.PhaseId.Contains("recover-runway"), plan.PhaseId);
                Equal(1, plan.Horizontal);
                False(plan.Jump);
                Equal(JumpAction.Release, plan.JumpAction);
                False(plan.Drop);
            }
        }

        private static void TwinsRecoveryRequiresLandingAndResource()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            s.Player.OnGround = false;
            s.Player.Position.Y = 800f;
            SetTwinsFlight(s, 20f, 0);
            True(engine.Evaluate(s).Directive.PhaseId.Contains("recover-runway"));

            s.Player.OnGround = true;
            s.Player.Position.Y = 958f;
            SetTwinsFlight(s, 40f, 0);
            var landedLow = engine.Evaluate(s).Directive;
            True(landedLow.PhaseId.Contains("recover-runway"), landedLow.PhaseId);
            False(landedLow.PhaseId.Contains("supported-run"), landedLow.PhaseId);

            SetTwinsFlight(s, 100f, 7);
            var recovered = engine.Evaluate(s).Directive;
            True(recovered.PhaseId.Contains("supported-run"), recovered.PhaseId);
            True(recovered.HorizontalIntent != 0);
        }

        private static void TwinsRecoveryTimeoutIsFinite()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            s.Player.OnGround = false;
            s.Player.Position = new Vec2(1000f, 700f);
            s.Player.Velocity = new Vec2(0f, 1f);
            SetTwinsFlight(s, 0f, 0);
            BossDirective directive = default(BossDirective);
            for (var tick = 0; tick <= 600; tick++) directive = engine.Evaluate(s).Directive;
            True(directive.PhaseId.Contains("timeout-closed"), directive.PhaseId);
            Equal(0, directive.HorizontalIntent);
            Equal(JumpAction.Release, directive.JumpAction);
        }

        private static void TwinsTurnHysteresisBoundary()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            s.Player.Position.X = 1450f;
            s.Player.Velocity.X = -6f;
            s.Arena.ClearanceLeft = 250f;
            var firstTurn = engine.Evaluate(s).Directive;
            Equal(1, firstTurn.HorizontalIntent);
            True(firstTurn.PhaseId.Contains("safe-turn"), firstTurn.PhaseId);

            s.Player.Position.X = 3340f;
            s.Player.Velocity.X = 6f;
            s.Arena.ClearanceLeft = 1800f;
            s.Arena.ClearanceRight = 260f;
            for (var tick = 1; tick <= 60; tick++)
            {
                var held = engine.Evaluate(s).Directive;
                False(held.HorizontalIntent < 0, "second reversal happened at held tick " + tick);
            }
            var secondTurn = engine.Evaluate(s).Directive;
            Equal(-1, secondTurn.HorizontalIntent);
            True(secondTurn.PhaseId.Contains("safe-turn"), secondTurn.PhaseId);
        }

        private static void TwinsAnchorDoesNotDriftWithScanWindow()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var expected = engine.Evaluate(s).Directive.HorizontalIntent;
            s.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 1650f, Right = 2100f, SurfaceY = 1000f
            };
            s.Arena.RecoverySupport = s.Arena.FloorSupport;
            s.Player.Position.X = 3000f;
            var decision = engine.Evaluate(s).Directive;
            False(decision.PhaseId.Contains("acquire"), decision.PhaseId);
            False(decision.PhaseId.Contains("anchor-lost"), decision.PhaseId);
            Equal(expected, decision.HorizontalIntent);
        }

        private static void TwinsInitialDirectionUsesSpawnAndBounds()
        {
            var leftSpawn = TwinsSnapshot();
            leftSpawn.Player.Position.X = 1450f;
            leftSpawn.Player.Velocity.X = -3f;
            Equal(1, new BossStrategyEngine().Evaluate(leftSpawn).Directive.HorizontalIntent);

            var rightSpawn = TwinsSnapshot();
            rightSpawn.Player.Position.X = 3350f;
            rightSpawn.Player.Velocity.X = 3f;
            for (var i = 0; i < rightSpawn.Targets.Count; i++)
            {
                var eye = rightSpawn.Targets[i];
                eye.Position.X = 2600f - i * 100f;
                rightSpawn.Targets[i] = eye;
            }
            Equal(-1, new BossStrategyEngine().Evaluate(rightSpawn).Directive.HorizontalIntent);
        }

        private static void TwinsUnsupportedFixturesFailClosed()
        {
            var noSupport = TwinsSnapshot();
            noSupport.Arena.FloorSupport = default(SupportSpan);
            noSupport.Arena.RecoverySupport = default(SupportSpan);
            noSupport.Arena.HasFloor = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(noSupport).Directive);

            var expert = TwinsSnapshot();
            expert.Difficulty.Expert = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(expert).Directive);

            var seed = TwinsSnapshot();
            seed.Difficulty.Zenith = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(seed).Directive);

            var drunk = TwinsSnapshot();
            drunk.Difficulty.Drunk = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(drunk).Directive);

            var wings = TwinsSnapshot();
            var flight = wings.Player.Flight;
            flight.WingsLogic = 2;
            wings.Player.Flight = flight;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(wings).Directive);

            var identity = TwinsSnapshot();
            identity.Player.WingAccessoryItemType = 493;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(identity).Directive);

            var activeMount = TwinsSnapshot();
            activeMount.Mobility.MountActive = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(activeMount).Directive);

            var attachedGrapple = TwinsSnapshot();
            attachedGrapple.Mobility.Grappling = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(attachedGrapple).Directive);

            var inverted = TwinsSnapshot();
            inverted.Mobility.GravityInverted = true;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(inverted).Directive);

            var multiplayer = TwinsSnapshot();
            multiplayer.NetMode = 1;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(multiplayer).Directive);

            var target = TwinsSnapshot();
            var eye = target.Targets[0];
            eye.NativeTargetKnown = false;
            target.Targets[0] = eye;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(target).Directive);

            var unknown = TwinsSnapshot();
            eye = unknown.Targets[1];
            eye.Ai1 = 99f;
            unknown.Targets[1] = eye;
            AssertTwinsClosed(new BossStrategyEngine().Evaluate(unknown).Directive);
        }

        private static void TwinsPreSpawnRequirementsUseSameContract()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var supported = TwinsSnapshot();
            supported.Targets.Clear();
            True(planner.RequirementsMetForExpected(supported, "twins", 125, out reason), reason);

            var empty = TwinsSnapshot();
            empty.Targets.Clear();
            SetTwinsFlight(empty, 0f, 0);
            False(planner.RequirementsMetForExpected(empty, "twins", 125, out reason));

            var floor = TwinsSnapshot();
            floor.Targets.Clear();
            floor.Arena.FloorSupport = default(SupportSpan);
            floor.Arena.RecoverySupport = default(SupportSpan);
            False(planner.RequirementsMetForExpected(floor, "twins", 125, out reason));

            var expert = TwinsSnapshot();
            expert.Targets.Clear();
            expert.Difficulty.Expert = true;
            False(planner.RequirementsMetForExpected(expert, "twins", 125, out reason));

            var wings = TwinsSnapshot();
            wings.Targets.Clear();
            var flight = wings.Player.Flight;
            flight.WingsLogic = 2;
            wings.Player.Flight = flight;
            False(planner.RequirementsMetForExpected(wings, "twins", 125, out reason));

            var identity = TwinsSnapshot();
            identity.Targets.Clear();
            identity.Player.RocketBootAccessoryItemType = 1862;
            False(planner.RequirementsMetForExpected(identity, "twins", 125, out reason));

            var optional = TwinsSnapshot();
            optional.Targets.Clear();
            optional.Mobility.CanDash = true;
            optional.Mobility.DashReady = true;
            optional.Mobility.DashType = 2;
            optional.Mobility.HasUsableMount = true;
            optional.Mobility.MountCanFly = true;
            optional.Mobility.HasGrapple = true;
            optional.Mobility.CanFlipGravity = true;
            True(planner.RequirementsMetForExpected(optional, "twins", 125, out reason), reason);

            var feather = TwinsSnapshot();
            feather.Targets.Clear();
            feather.Mobility.FeatherFall = true;
            var featherJump = feather.Player.Jump;
            featherJump.SlowFall = true;
            feather.Player.Jump = featherJump;
            True(planner.RequirementsMetForExpected(feather, "twins", 125, out reason), reason);

            var target = TwinsSnapshot();
            var eye = target.Targets[0];
            eye.NativeTargetPlayerIndex = 1;
            target.Targets[0] = eye;
            False(planner.RequirementsMet(target, out reason));
        }

        private static void TwinsResetAndReentryClearRunway()
        {
            var first = TwinsSnapshot();
            var engine = SupportedTwins(first);
            Equal(-1, engine.Evaluate(first).Directive.HorizontalIntent);

            engine.Reset();
            var second = TwinsSnapshot();
            second.Player.Position.X = 3600f;
            second.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 2800f, Right = 5200f, SurfaceY = 1000f
            };
            second.Arena.RecoverySupport = second.Arena.FloorSupport;
            for (var i = 0; i < second.Targets.Count; i++)
            {
                var eye = second.Targets[i];
                eye.Position.X = 3000f - i * 100f;
                second.Targets[i] = eye;
            }
            var reset = engine.Evaluate(second).Directive;
            True(reset.PhaseId.Contains("acquire-runway"), reset.PhaseId);
            Equal(1, reset.HorizontalIntent);

            engine.Evaluate(second);
            engine.Evaluate(EyeSnapshot());
            var third = TwinsSnapshot();
            third.Player.Position.X = 5200f;
            third.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 4400f, Right = 6800f, SurfaceY = 1000f
            };
            third.Arena.RecoverySupport = third.Arena.FloorSupport;
            for (var i = 0; i < third.Targets.Count; i++)
            {
                var eye = third.Targets[i];
                eye.Position.X = 4700f - i * 100f;
                third.Targets[i] = eye;
            }
            var reentry = engine.Evaluate(third).Directive;
            True(reentry.PhaseId.Contains("acquire-runway"), reentry.PhaseId);
            Equal(1, reentry.HorizontalIntent);
        }

        private static void TwinsPlannerCannotRewriteHorizontal()
        {
            var s = TwinsSnapshot();
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var eye = s.Targets[i];
                eye.Position.X = 1200f - i * 100f;
                s.Targets[i] = eye;
            }
            var planner = new CombatPlanner(new PlannerSettings { PatternSafeRiskThreshold = 0f });
            Equal(1, planner.Plan(s).Horizontal);
            Equal(1, planner.Plan(s).Horizontal);

            s.Player.OnGround = false;
            s.Player.Position = new Vec2(2700f, 800f);
            s.Player.Velocity = new Vec2(2f, 1f);
            SetTwinsFlight(s, 12f, 0);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 1200f, Right = 1900f, SurfaceY = 1000f
            };
            // Damage zero makes this an inert strategy-side observation while
            // still forcing the generic predictor to compare vertical evasion
            // candidates. Its horizontal alternatives remain forbidden.
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Position = s.Player.Position,
                Width = 16,
                Height = 16,
                Damage = 0,
                TimeLeft = 60
            });
            typeof(CombatPlanner).GetField("_stuckTicks", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(planner, 1000);
            var plan = planner.Plan(s);
            Equal(1, plan.Horizontal);
            True(planner.LastCandidateCount <= 3, "horizontal closure expanded " + planner.LastCandidateCount + " candidates");
        }

        private static void TwinsRejectedPlanStaysNeutral()
        {
            var s = TwinsSnapshot();
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 1f });
            planner.Plan(s);
            s.Mobility.CanDash = true;
            s.Mobility.DashReady = true;
            s.Mobility.HasUsableMount = true;
            s.Mobility.MountRunSpeed = 40f;
            s.Mobility.HasGrapple = true;
            s.Mobility.Grappling = true;
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Type = 126,
                Position = s.Player.Position,
                Width = s.Player.Width,
                Height = s.Player.Height,
                Damage = 999,
                TimeLeft = int.MaxValue
            });
            var plan = planner.Plan(s);
            True(plan.RequestControlReturn);
            Equal("unsupported-active-grapple", plan.StrategyId);
            True(plan.PhaseId.Contains("guessed-detach"), plan.PhaseId);
            Equal(0, plan.Horizontal);
            False(plan.Jump);
            Equal(JumpAction.Release, plan.JumpAction);
            False(plan.Drop);
            False(plan.Dash);
            False(plan.Hook);
            False(plan.ToggleMount);
            Equal(0, plan.GravityControl);
        }

        private static void TwinsTransientObservationsPreserveClosure()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var expected = engine.Evaluate(s).Directive.HorizontalIntent;

            var flight = s.Player.Flight;
            flight.Known = false;
            s.Player.Flight = flight;
            var missingFlight = engine.Evaluate(s).Directive;
            AssertTwinsClosed(missingFlight);
            False(missingFlight.RequestControlReturn);
            flight.Known = true;
            s.Player.Flight = flight;
            var afterFlight = engine.Evaluate(s).Directive;
            Equal(expected, afterFlight.HorizontalIntent);
            False(afterFlight.PhaseId.Contains("acquire"), afterFlight.PhaseId);

            var ret = s.Targets[0];
            ret.NativeTargetKnown = false;
            s.Targets[0] = ret;
            var missingTarget = engine.Evaluate(s).Directive;
            AssertTwinsClosed(missingTarget);
            False(missingTarget.RequestControlReturn);
            ret.NativeTargetKnown = true;
            s.Targets[0] = ret;
            Equal(expected, engine.Evaluate(s).Directive.HorizontalIntent);

            var floor = s.Arena.FloorSupport;
            var recovery = s.Arena.RecoverySupport;
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = default(SupportSpan);
            var missingAnchor = engine.Evaluate(s).Directive;
            AssertTwinsClosed(missingAnchor);
            False(missingAnchor.RequestControlReturn);
            s.Arena.FloorSupport = floor;
            s.Arena.RecoverySupport = recovery;
            var resumed = engine.Evaluate(s).Directive;
            Equal(expected, resumed.HorizontalIntent);
            False(resumed.PhaseId.Contains("acquire"), resumed.PhaseId);
        }

        private static void TwinsBoundedObservationLossReturnsControl()
        {
            var incompatible = TwinsSnapshot();
            var incompatibleEngine = SupportedTwins(incompatible);
            incompatible.Player.WingAccessoryItemType = 493;
            for (var tick = 1; tick < 3; tick++)
                False(incompatibleEngine.Evaluate(incompatible).Directive.RequestControlReturn,
                    "definite mismatch returned on tick " + tick);
            AssertTwinsControlReturn(incompatibleEngine.Evaluate(incompatible).Directive, "contract");

            var native = TwinsSnapshot();
            var nativeEngine = SupportedTwins(native);
            var eye = native.Targets[0];
            eye.NativeTargetKnown = false;
            native.Targets[0] = eye;
            for (var tick = 1; tick < 12; tick++)
                False(nativeEngine.Evaluate(native).Directive.RequestControlReturn,
                    "native observation returned on tick " + tick);
            AssertTwinsControlReturn(nativeEngine.Evaluate(native).Directive, "observation");

            var phase = TwinsSnapshot();
            var phaseEngine = SupportedTwins(phase);
            eye = phase.Targets[1];
            eye.Ai1 = 99f;
            phase.Targets[1] = eye;
            for (var tick = 1; tick < 12; tick++)
                False(phaseEngine.Evaluate(phase).Directive.RequestControlReturn,
                    "native phase returned on tick " + tick);
            AssertTwinsControlReturn(phaseEngine.Evaluate(phase).Directive, "phase");

            var anchor = TwinsSnapshot();
            var anchorEngine = SupportedTwins(anchor);
            anchor.Arena.FloorSupport = default(SupportSpan);
            anchor.Arena.RecoverySupport = default(SupportSpan);
            for (var tick = 1; tick < 12; tick++)
                False(anchorEngine.Evaluate(anchor).Directive.RequestControlReturn,
                    "saved runway observation returned on tick " + tick);
            AssertTwinsControlReturn(anchorEngine.Evaluate(anchor).Directive, "runway");

            var acquire = TwinsSnapshot();
            acquire.Arena.FloorSupport = default(SupportSpan);
            acquire.Arena.RecoverySupport = default(SupportSpan);
            var acquireEngine = new BossStrategyEngine();
            for (var tick = 1; tick < 12; tick++)
                False(acquireEngine.Evaluate(acquire).Directive.RequestControlReturn,
                    "runway acquisition returned on tick " + tick);
            AssertTwinsControlReturn(acquireEngine.Evaluate(acquire).Directive, "runway");
        }

        private static void TwinsRouteClosedReturnsControl()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var spaz = s.Targets[1];
            spaz.Ai1 = 1f;
            s.Targets[1] = spaz;
            engine.Evaluate(s);
            s.Player.Position.X = 1204f;
            s.Player.Velocity.X = -7f;
            s.Arena.ClearanceLeft = 4f;
            spaz.Position.X = 900f;
            spaz.Ai1 = 2f;
            spaz.Ai2 = 3f;
            s.Targets[1] = spaz;

            for (var tick = 1; tick < 90; tick++)
            {
                var waiting = engine.Evaluate(s).Directive;
                True(waiting.PhaseId.Contains("route-closed"), waiting.PhaseId);
                False(waiting.RequestControlReturn, "closed route returned on tick " + tick);
            }
            AssertTwinsControlReturn(engine.Evaluate(s).Directive, "route");
        }

        private static void TwinsRecoveryWatchdogTracksProgress()
        {
            var noProgress = TwinsSnapshot();
            var noProgressEngine = SupportedTwins(noProgress);
            noProgress.Player.OnGround = false;
            noProgress.Player.OnOneWaySupport = false;
            noProgress.Player.Position = new Vec2(1000f, 700f);
            noProgress.Player.Velocity = new Vec2(0f, 1f);
            SetTwinsFlight(noProgress, 0f, 0);
            for (var tick = 1; tick < 600; tick++)
                False(noProgressEngine.Evaluate(noProgress).Directive.RequestControlReturn,
                    "no-progress recovery returned on tick " + tick);
            AssertTwinsControlReturn(noProgressEngine.Evaluate(noProgress).Directive, "watchdog");

            var progressing = TwinsSnapshot();
            var progressingEngine = SupportedTwins(progressing);
            progressing.Player.OnGround = false;
            progressing.Player.OnOneWaySupport = false;
            progressing.Player.Position = new Vec2(1000f, 700f);
            progressing.Player.Velocity = new Vec2(0f, 1f);
            SetTwinsFlight(progressing, 0f, 0);
            for (var tick = 1; tick <= 590; tick++)
                False(progressingEngine.Evaluate(progressing).Directive.RequestControlReturn);
            progressing.Player.Position.Y += 16f;
            False(progressingEngine.Evaluate(progressing).Directive.RequestControlReturn,
                "sixteen pixels of recovery progress did not reset the watchdog");
            for (var tick = 1; tick <= 20; tick++)
                False(progressingEngine.Evaluate(progressing).Directive.RequestControlReturn,
                    "watchdog ignored recent recovery progress on tick " + tick);
        }

        private static void TwinsPlannerControlReturnIsNeutral()
        {
            var s = TwinsSnapshot();
            var planner = new CombatPlanner(new PlannerSettings { EmergencyRiskThreshold = 0f });
            planner.Plan(s);
            planner.Plan(s);
            s.Player.WingAccessoryItemType = 493;
            s.Mobility.CanDash = true;
            s.Mobility.DashReady = true;
            s.Mobility.HasUsableMount = true;
            s.Mobility.MountRunSpeed = 40f;
            s.Mobility.HasGrapple = true;
            False(planner.Plan(s).RequestControlReturn);
            False(planner.Plan(s).RequestControlReturn);
            var plan = planner.Plan(s);
            True(plan.RequestControlReturn);
            True(plan.PhaseId.Contains("control-return"), plan.PhaseId);
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

        private static void AssertTwinsControlReturn(BossDirective directive, string reasonFragment)
        {
            True(directive.RequestControlReturn);
            True(directive.PhaseId.Contains("control-return"), directive.PhaseId);
            True(!string.IsNullOrEmpty(directive.ControlReturnReason), "missing return reason");
            True(directive.ControlReturnReason.IndexOf(reasonFragment, StringComparison.OrdinalIgnoreCase) >= 0,
                directive.ControlReturnReason);
            Equal(0, directive.HorizontalIntent);
            Equal(0, directive.VerticalIntent);
            Equal(JumpAction.Release, directive.JumpAction);
            False(directive.Fire);
            True(directive.OwnsMovementClosure);
        }

        private static void TwinsFireGuards()
        {
            var s = TwinsSnapshot();
            var engine = SupportedTwins(s);
            var spaz = s.Targets[1];
            spaz.HasLineOfSight = false;
            s.Targets[1] = spaz;
            False(engine.Evaluate(s).Directive.Fire);
            spaz.HasLineOfSight = true;
            spaz.Invulnerable = true;
            s.Targets[1] = spaz;
            False(engine.Evaluate(s).Directive.Fire);
            spaz.Invulnerable = false;
            s.Targets[1] = spaz;
            True(engine.Evaluate(s).Directive.Fire);
        }

        private static void TwinsHotPathHasNoCollectionsOrLinq()
        {
            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(BossStrategyEngine).Assembly.Location))
            {
                Mono.Cecil.TypeDefinition strategy = null;
                for (var i = 0; i < assembly.MainModule.Types.Count; i++)
                {
                    var candidate = assembly.MainModule.Types[i];
                    if (candidate.FullName == "Chaite.Core.TwinsStrategy") { strategy = candidate; break; }
                }
                True(strategy != null, "TwinsStrategy IL type missing");
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
