using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPrimeRegressions()
        {
            Run("Prime fast native hover is not spin", PrimeFastHover);
            Run("Prime slow native spin remains spin", PrimeSlowSpin);
            Run("Prime day enrage and departure are explicit", PrimeExceptionalPhases);
            Run("Prime unknown native phase does not invent an orbit", PrimeUnknownPhase);
            Run("Prime runway never follows a rising head", PrimeNoHeightFeedback);
            Run("Prime bounded lift does not raise goal at a new platform", PrimeFixedLiftOrigin);
            Run("Prime lift aborts at reserve without spending nonexistent charge", PrimeLiftReserve);
            Run("Prime blocked lift has a finite timeout", PrimeBlockedLift);
            Run("Prime platform landing releases jump and does not drop", PrimePlatformRelease);
            Run("Prime solid footing emits a real jump launch", PrimeSolidFootingLaunch);
            Run("Prime unknown ground does not invent an ascent destination", PrimeNoUnknownLift);
            Run("Prime detached recovery support is not current footing", PrimeDetachedRecoveryIsNotFooting);
            Run("Prime distant recovery support is landing-only", PrimeDistantRecoveryIsLandingOnly);
            Run("Prime airborne correction can use recovery support", PrimeAirborneLandingSupport);
            Run("Prime visible Laser precedes high-defense melee arms", PrimeLaserPriority);
            Run("Prime blocked Laser does not monopolize the fire target", PrimeBlockedLaser);
            Run("Prime known-visible head precedes unknown Laser", PrimeKnownVisibleBeforeUnknownLaser);
            Run("Prime unknown Laser remains a non-firing probe", PrimeUnknownLaserProbe);
            Run("Prime head can be finished without destroying all arms", PrimeHeadBeforeOtherArms);
            Run("Prime stopping margin applies to hover and spin", PrimeBrakingGap);
            Run("Prime far return is restricted to safe hover", PrimeFarReturn);
            Run("Prime return rejects an occupied support corridor", PrimeOccupiedReturn);
            Run("Prime malformed native projectile closes optional return", PrimeMalformedNativeThreatClosesReturn);
            Run("Prime returning brakes before the inner band", PrimeReturnStopsBeforeInnerBand);
            Run("Prime returning revalidates new threats", PrimeReturnRevalidatesThreats);
            Run("Prime returning never follows a head across sides", PrimeReturnDoesNotCrossHead);
            Run("Prime hook permission honors the quarter-resource boundary", PrimeHookBoundary);
            Run("Prime strategy reset clears the bounded ascent", PrimeResetLift);
            Run("Prime strategy re-entry clears private movement state", PrimeStrategyReentry);
            Run("Prime selected route demands known finite normal-gravity flight", PrimeFlightRequirements);
        }

        private static CombatSnapshot PrimeSnapshot()
        {
            var s = EyeSnapshot();
            var head = s.Targets[0];
            head.Type = 127;
            head.Position = new Vec2(1500f, 580f);
            head.Life = head.LifeMax = 28000;
            s.Targets[0] = head;
            var floor = s.Arena.FloorSupport;
            floor.OneWay = true;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            ConfigurePrimeFlight(s);
            return s;
        }

        private static void ConfigurePrimeFlight(CombatSnapshot s)
        {
            s.Player.Flight = new FlightSnapshot
            {
                Known = true,
                WingsLogic = 1,
                WingTime = 100f,
                WingTimeMax = 100
            };
            s.Player.WingTime = 100f;
            s.Mobility.HasFiniteFlightResource = true;
            s.Mobility.FlightResourceFraction = 1f;
            s.Mobility.GravityInverted = false;
        }

        private static void PrimeFastHover()
        {
            var s = PrimeSnapshot();
            var head = s.Targets[0]; head.Velocity = new Vec2(9.5f, 0); s.Targets[0] = head;
            Equal("classic-hover-low-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void PrimeSlowSpin()
        {
            var s = PrimeSnapshot(); var head = s.Targets[0];
            head.Ai1 = 1; head.Velocity = new Vec2(2, 0); s.Targets[0] = head;
            Equal("classic-spin-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            head.Ai1 = 0; head.Ai2 = 540; s.Targets[0] = head;
            Equal("classic-hover-prepare-spin", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void PrimeExceptionalPhases()
        {
            var s = PrimeSnapshot(); var h = s.Targets[0];
            h.Ai1 = 2; s.Targets[0] = h;
            Equal("classic-day-enrage-escape", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            h.Ai1 = 3; s.Targets[0] = h;
            Equal("classic-native-departure", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void PrimeUnknownPhase()
        {
            var s = PrimeSnapshot(); var h = s.Targets[0]; h.Ai1 = 19; s.Targets[0] = h;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-unrecognized-native-state", d.PhaseId);
            Equal(0, d.HorizontalIntent); Equal(0, d.VerticalIntent); False(d.Fire); True(d.UseExplicitMovement);
        }

        private static void PrimeNoHeightFeedback()
        {
            var s = PrimeSnapshot(); var engine = new BossStrategyEngine();
            for (var i = 0; i < 100; i++)
            {
                var h = s.Targets[0]; h.Position.Y -= 10; s.Targets[0] = h;
                var d = engine.Evaluate(s).Directive;
                Equal(0, d.VerticalIntent); Equal(JumpAction.Release, d.JumpAction);
            }
        }

        private static void PrimeFixedLiftOrigin()
        {
            var s = PrimeSnapshot(); var engine = new BossStrategyEngine();
            var f = s.Arena.FloorSupport; f.OneWay = false; s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            s.Player.OnGround = false; s.Player.Position.Y = 500;
            f.SurfaceY = 600; f.OneWay = true; s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            s.Player.Position.Y = 238; // Foot280 = launch1000 -720; must stop even with a newly seen row600.
            var d = engine.Evaluate(s).Directive;
            Equal(0, d.VerticalIntent); Equal(JumpAction.Release, d.JumpAction);
        }

        private static void PrimeLiftReserve()
        {
            var s = PrimeSnapshot(); var f = s.Arena.FloorSupport; f.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            var engine = new BossStrategyEngine(); Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            s.Player.OnGround = false; s.Mobility.FlightResourceFraction = .28f;
            Equal(0, engine.Evaluate(s).Directive.VerticalIntent);
            s.Mobility.FlightResourceFraction = 0;
            for (var i = 0; i < 10; i++) Equal(0, engine.Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimeBlockedLift()
        {
            var s = PrimeSnapshot(); var f = s.Arena.FloorSupport; f.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            var engine = new BossStrategyEngine();
            for (var i = 0; i < 180; i++) Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            Equal(0, engine.Evaluate(s).Directive.VerticalIntent);
            for (var i = 0; i < 100; i++) Equal(0, engine.Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimePlatformRelease()
        {
            var s = PrimeSnapshot(); var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal(0, d.VerticalIntent); Equal(JumpAction.Release, d.JumpAction);
            False(d.AllowHook); True(d.UseExplicitMovement);
            var plan = new CombatPlanner(new PlannerSettings()).Plan(s);
            False(plan.Jump); Equal(JumpAction.Release, plan.JumpAction); False(plan.Drop);
        }

        private static void PrimeSolidFootingLaunch()
        {
            var s = PrimeSnapshot(); var floor = s.Arena.FloorSupport; floor.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            var plan = new CombatPlanner(new PlannerSettings()).Plan(s);
            True(plan.Jump); Equal(JumpAction.Default, plan.JumpAction); False(plan.Drop);
        }

        private static void PrimeNoUnknownLift()
        {
            var s = PrimeSnapshot();
            s.Arena.FloorSupport = s.Arena.RecoverySupport = default(SupportSpan);
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimeDetachedRecoveryIsNotFooting()
        {
            var s = PrimeSnapshot();
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, Left = 0f, Right = 1000f, SurfaceY = 1000f
            };
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimeDistantRecoveryIsLandingOnly()
        {
            var s = PrimeSnapshot();
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, Left = 1200f, Right = 3620f, SurfaceY = 1400f
            };
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.VerticalIntent);

            s = PrimeReturnSnapshot();
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 1200f, Right = 3620f, SurfaceY = 1400f
            };
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeAirborneLandingSupport()
        {
            var s = PrimeSnapshot();
            s.Player.OnGround = false;
            s.Player.Position = new Vec2(2400f, 800f);
            s.Player.Velocity = default(Vec2);
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = new SupportSpan
            {
                Valid = true, OneWay = true, Left = 1600f, Right = 2300f, SurfaceY = 1000f
            };
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal(-1, d.HorizontalIntent); Equal(JumpAction.Release, d.JumpAction);
        }

        private static void AddPrimeArm(CombatSnapshot s, int type, int life, bool? visible = true)
        {
            var arm = s.Targets[0]; arm.Type = type; arm.Key = type; arm.Life = arm.LifeMax = life;
            arm.Boss = false; arm.LineOfSightKnown = visible.HasValue;
            arm.HasLineOfSight = visible.GetValueOrDefault(); arm.Position.X += 80;
            s.Targets.Add(arm);
        }

        private static void PrimeLaserPriority()
        {
            var s = PrimeSnapshot(); AddPrimeArm(s, 129, 1); AddPrimeArm(s, 130, 2); AddPrimeArm(s, 131, 6000);
            var d = new BossStrategyEngine().Evaluate(s);
            Equal(131, d.Target.Type); Equal(127, d.PatternTarget.Type);
        }

        private static void PrimeBlockedLaser()
        {
            var s = PrimeSnapshot(); AddPrimeArm(s, 131, 6000, false);
            Equal(127, new BossStrategyEngine().Evaluate(s).Target.Type);
            var h = s.Targets[0]; h.HasLineOfSight = false; s.Targets[0] = h;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
        }

        private static void PrimeKnownVisibleBeforeUnknownLaser()
        {
            var s = PrimeSnapshot(); AddPrimeArm(s, 131, 6000, null);
            var d = new BossStrategyEngine().Evaluate(s);
            Equal(127, d.Target.Type); True(d.Directive.Fire);
        }

        private static void PrimeUnknownLaserProbe()
        {
            var s = PrimeSnapshot(); var head = s.Targets[0];
            head.HasLineOfSight = false; s.Targets[0] = head;
            AddPrimeArm(s, 131, 6000, null);
            var d = new BossStrategyEngine().Evaluate(s);
            Equal(131, d.Target.Type); False(d.Directive.Fire);
        }

        private static void PrimeHeadBeforeOtherArms()
        {
            var s = PrimeSnapshot(); AddPrimeArm(s, 129, 9000); AddPrimeArm(s, 128, 7000);
            Equal(127, new BossStrategyEngine().Evaluate(s).Target.Type);
        }

        private static void PrimeBrakingGap()
        {
            var s = PrimeSnapshot(); var h = s.Targets[0]; h.Position.X = 1200; s.Targets[0] = h;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            h.Ai1 = 1; s.Targets[0] = h;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static CombatSnapshot PrimeReturnSnapshot()
        {
            var s = PrimeSnapshot(); var h = s.Targets[0]; h.Position.X = 900; s.Targets[0] = h;
            s.Player.Velocity = default(Vec2);
            return s;
        }

        private static void PrimeFarReturn()
        {
            var s = PrimeReturnSnapshot();
            Equal(-1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            var h = s.Targets[0]; h.Ai1 = 1; s.Targets[0] = h;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeOccupiedReturn()
        {
            var s = PrimeReturnSnapshot();
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent < 0);
            s.Threats.Add(new ThreatSnapshot { Kind = ThreatKind.Projectile, Damage = 30,
                Position = s.Player.Position - new Vec2(24, 0), Width = 24, Height = 50 });
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeMalformedNativeThreatClosesReturn()
        {
            var s = PrimeReturnSnapshot();
            Equal(-1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
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
                TimeLeft = 100,
                TrajectoryAi0 = float.NaN
            });
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeReturnStopsBeforeInnerBand()
        {
            var s = PrimeReturnSnapshot(); var engine = new BossStrategyEngine();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            s.Player.Position.X = 1450f;
            s.Player.Velocity = new Vec2(-6f, 0f);
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeReturnRevalidatesThreats()
        {
            var s = PrimeReturnSnapshot(); var engine = new BossStrategyEngine();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Damage = 30,
                Position = s.Player.Position - new Vec2(24f, 0f),
                Width = 24,
                Height = 50
            });
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeReturnDoesNotCrossHead()
        {
            var s = PrimeReturnSnapshot(); var engine = new BossStrategyEngine();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            var head = s.Targets[0]; head.Position.X = 2400f; s.Targets[0] = head;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void PrimeHookBoundary()
        {
            var s = PrimeSnapshot();
            False(new BossStrategyEngine().Evaluate(s).Directive.AllowHook);
            s.Player.OnGround = false; s.Mobility.FlightResourceFraction = .24f;
            True(new BossStrategyEngine().Evaluate(s).Directive.AllowHook);
            s.Mobility.FlightResourceFraction = .25f;
            False(new BossStrategyEngine().Evaluate(s).Directive.AllowHook);
        }

        private static void PrimeResetLift()
        {
            var s = PrimeSnapshot(); var f = s.Arena.FloorSupport; f.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            var engine = new BossStrategyEngine(); Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            engine.Reset(); f.OneWay = true; s.Arena.FloorSupport = s.Arena.RecoverySupport = f;
            Equal(0, engine.Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimeStrategyReentry()
        {
            var s = PrimeSnapshot(); var floor = s.Arena.FloorSupport; floor.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.VerticalIntent);

            var twin = s.Targets[0]; twin.Key = 125; twin.Type = 125; twin.Life = twin.LifeMax = 10000;
            s.Targets.Add(twin);
            Equal("mechanical-mayhem", engine.Evaluate(s).Directive.StrategyId);
            twin.Life = 0; s.Targets[1] = twin;
            floor.OneWay = true; s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            Equal(0, engine.Evaluate(s).Directive.VerticalIntent);

            s = PrimeSnapshot(); floor = s.Arena.FloorSupport; floor.OneWay = false;
            s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
            twin = s.Targets[0]; twin.Key = 125; twin.Type = 125; s.Targets.Add(twin);
            True(engine.RequirementsFor(s) != null);
            twin.Life = 0; s.Targets[1] = twin;
            floor.OneWay = true; s.Arena.FloorSupport = s.Arena.RecoverySupport = floor;
            Equal(1, engine.Evaluate(s).Directive.VerticalIntent);
        }

        private static void PrimeFlightRequirements()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            var s = PrimeSnapshot();
            var met = planner.RequirementsMet(s, out reason);
            True(met, reason ?? "Prime requirements returned false without a reason");

            s = PrimeSnapshot(); s.Player.Flight = default(FlightSnapshot);
            False(planner.RequirementsMet(s, out reason));
            True(reason.Contains("有限飞行") || reason.Contains("空中路线"), reason);

            s = PrimeSnapshot(); s.Mobility.HasFiniteFlightResource = false;
            s.Mobility.MountCanFly = true; s.Mobility.CanFlipGravity = true;
            False(planner.RequirementsMet(s, out reason));
            True(reason.Contains("有限飞行") || reason.Contains("空中路线"), reason);

            s = PrimeSnapshot();
            var depleted = s.Player.Flight;
            depleted.WingTime = 0f;
            depleted.RocketTime = 0;
            s.Player.Flight = depleted;
            s.Player.WingTime = s.Player.RocketTime = 0f;
            s.Mobility.FlightResourceFraction = 0f;
            s.Mobility.MountCanFly = true; s.Mobility.CanFlipGravity = true;
            False(planner.RequirementsMet(s, out reason));
            True(reason.Contains("耗尽"), reason);

            s = PrimeSnapshot(); s.Mobility.GravityInverted = true;
            False(planner.RequirementsMet(s, out reason)); True(reason.Contains("正常重力"));
        }
    }
}
