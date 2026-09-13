using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunQueenSlimeRegressions()
        {
            Run("Queen native form uses integer inclusive half life", QueenIntegerHalfLife);
            Run("Queen first-form slam and gel do not imply wings", QueenFirstFormAttacks);
            Run("Queen attack anticipation follows native timer", QueenAttackWindow);
            Run("Queen exits slam before a landing projectile exists", QueenSlamAnticipation);
            Run("Queen slam exit remains committed across Boss side changes", QueenCommittedSlamExit);
            Run("Queen landing is recognized before the timer increment", QueenLandingPending);
            Run("Queen slam timeout does not wait for an invented wave", QueenSlamTimeout);
            Run("Queen observed wave requests a horizontal exit not a jump", QueenWaveExit);
            Run("Queen old gel does not replace observed attack state", QueenHistoricalGel);
            Run("Queen low-air recovery is anchored to an observed platform", QueenLowAirRecovery);
            Run("Queen does not chase an ever-rising Boss height", QueenNoHeightFeedback);
            Run("Queen actual teleport destination controls its exit", QueenTeleportDestination);
            Run("Queen optional return rejects minions and old projectiles", QueenOccupiedReturn);
            Run("Queen malformed native projectile closes optional return", QueenMalformedNativeThreatClosesReturn);
            Run("Queen grounded body can leave an unverified future support", QueenGroundedBossGravityEnvelope);
            Run("Queen return is not authorized in attack selection window", QueenNoLateReturn);
            Run("Queen no observed floor does not invent ground runway", QueenUnknownFloor);
            Run("Queen native unknown state is fail-closed even airborne", QueenUnknownState);
            Run("Queen pressure targeting retains the Boss pattern target", QueenPressureTarget);
            Run("Queen fire respects visibility and invulnerability", QueenFireGuards);
            Run("Queen reset clears its committed escape direction", QueenResetDirection);
            Run("Queen repeated jump gel and drop do not accumulate runway escape", QueenRepeatedAttackSpacing);
            Run("Queen spacing hold persists across attack state changes", QueenSpacingHysteresis);
            Run("Queen braking anticipates actual low friction", QueenSpacingLowFriction);
            Run("Queen a close slam overrides an existing spacing hold", QueenCloseSlamOverridesHold);
            Run("Queen observed rise accounts for all remaining horizontal drift", QueenSlamRiseDrift);
            Run("Queen same-side recovery stops within a bounded band", QueenBoundedRecovery);
            Run("Queen far recovery crosses only guarded native windows", QueenGuardedFarRecovery);
            Run("Queen reset clears spacing hysteresis", QueenResetSpacing);
            Run("Queen native-state sequence stays inside inactivity refresh leash", QueenNativeTtlLeashSequence);
        }

        private static CombatSnapshot QueenSnapshot()
        {
            var s = EyeSnapshot();
            var queen = s.Targets[0];
            queen.Type = 657;
            queen.Key = 72;
            queen.Position = new Vec2(1500f, 900f);
            queen.Width = 114;
            queen.Height = 100;
            queen.Life = queen.LifeMax = 4000;
            s.Targets[0] = queen;
            return s;
        }

        private static void QueenIntegerHalfLife()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Life = 2001;
            s.Targets[0] = queen;
            Equal("classic-first-ground-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Life = 2000;
            s.Targets[0] = queen;
            Equal("classic-second-low-runway-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.LifeMax = 4001;
            queen.Life = 2001;
            s.Targets[0] = queen;
            Equal("classic-first-ground-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Life = 2000;
            s.Targets[0] = queen;
            Equal("classic-second-low-runway-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenFirstFormAttacks()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 4;
            s.Targets[0] = queen;
            Equal("classic-first-slam-rise-windup", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Ai1 = 30;
            s.Targets[0] = queen;
            Equal("classic-first-slam-rising", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Ai0 = 5;
            queen.Ai1 = 0;
            s.Targets[0] = queen;
            Equal("classic-first-gel-windup", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Ai2 = 1;
            queen.Ai1 = 9;
            s.Targets[0] = queen;
            Equal("classic-first-gel-fire-pending", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenAttackWindow()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Life = 2000;
            queen.Ai1 = 95;
            s.Targets[0] = queen;
            Equal("classic-second-low-runway-bait", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
            queen.Ai1 = 96;
            s.Targets[0] = queen;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-second-attack-window-build-gap", d.PhaseId);
            Equal(1, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            queen.Ai1 = 120;
            s.Targets[0] = queen;
            Equal("classic-second-attack-window-build-gap", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenSlamAnticipation()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Life = 2000;
            queen.Position = new Vec2(1720f, 600f);
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Velocity = new Vec2(3f, -1f);
            s.Targets[0] = queen;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-second-slam-drop-windup", d.PhaseId);
            Equal(1, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);
            True(d.UseExplicitMovement);
            Equal(BossPattern.Runway, d.Pattern);
        }

        private static void QueenCommittedSlamExit()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Velocity.Y = -1f;
            s.Targets[0] = queen;
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Position.X = 2300;
            queen.Ai1 = 31;
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenLandingPending()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Velocity.Y = 0;
            s.Targets[0] = queen;
            Equal("classic-first-slam-landing-pending", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenSlamTimeout()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Life = 2000;
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Ai1 = 130;
            queen.Velocity.Y = 14;
            s.Targets[0] = queen;
            var engine = new BossStrategyEngine();
            Equal("classic-second-slam-timeout-pending", engine.Evaluate(s).Directive.PhaseId);
            queen.Ai0 = queen.Ai1 = queen.Ai2 = 0;
            s.Targets[0] = queen;
            Equal("classic-second-low-runway-bait", engine.Evaluate(s).Directive.PhaseId);
        }

        private static void QueenWaveExit()
        {
            var s = QueenSnapshot();
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Type = 922, Position = new Vec2(1725, 985),
                Width = 30, Height = 30, TimeLeft = 120, Damage = 40
            });
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-first-smash-wave-horizontal-exit", d.PhaseId);
            Equal(1, d.HorizontalIntent);
            Equal(JumpAction.Release, d.JumpAction);
            Equal(0, d.VerticalIntent);
            var expired = s.Threats[0];
            expired.TimeLeft = 0;
            s.Threats[0] = expired;
            Equal("classic-first-ground-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenHistoricalGel()
        {
            var s = QueenSnapshot();
            s.Threats.Add(new ThreatSnapshot { Kind = ThreatKind.Projectile, Type = 926, TimeLeft = 100 });
            Equal("classic-first-ground-runway", new BossStrategyEngine().Evaluate(s).Directive.PhaseId);
        }

        private static void QueenLowAirRecovery()
        {
            var s = QueenSnapshot();
            s.Player.OnGround = false;
            s.Player.Position.Y = 740f;
            s.Player.WingTime = 12f;
            s.Arena.FloorSupport.Right = 1850f;
            s.Arena.FloorSupport.OneWay = true;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-first-low-air-return-to-platform", d.PhaseId);
            Equal(-1, d.HorizontalIntent);
            Equal(0, d.VerticalIntent);
            Equal(0f, d.VerticalOffset);
            Equal(0f, d.FloorClearance);
            Equal(JumpAction.Release, d.JumpAction);
            Equal(12f, s.Player.WingTime);
        }

        private static void QueenNoHeightFeedback()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Life = 2000;
            var engine = new BossStrategyEngine();
            for (var tick = 0; tick < 120; tick++)
            {
                queen.Position.Y = 600f - tick * 5f;
                s.Targets[0] = queen;
                var d = engine.Evaluate(s).Directive;
                Equal(0, d.VerticalIntent);
                Equal(0f, d.VerticalOffset);
                Equal(JumpAction.Release, d.JumpAction);
                True(d.UseExplicitMovement);
            }
        }

        private static void QueenTeleportDestination()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 2;
            queen.LocalAi1 = 2100;
            s.Targets[0] = queen;
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            Equal("classic-teleport-destination-unobserved", engine.Evaluate(s).Directive.PhaseId);
            queen.LocalAiKnown = true;
            s.Targets[0] = queen;
            engine.Reset();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Ai0 = 1;
            queen.Position.X = 2100;
            s.Targets[0] = queen;
            Equal("classic-teleport-reform-regain-gap", engine.Evaluate(s).Directive.PhaseId);
        }

        private static CombatSnapshot QueenReturnSnapshot()
        {
            var s = QueenSnapshot();
            s.Arena.ClearanceRight = 220f;
            s.Arena.FloorSupport.Right = 2040f;
            var queen = s.Targets[0];
            queen.Life = 2000;
            queen.Position.Y = 500f;
            s.Targets[0] = queen;
            return s;
        }

        private static void QueenOccupiedReturn()
        {
            var s = QueenReturnSnapshot();
            Equal(-1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            s.Targets.Add(new TargetSnapshot
            {
                Key = 82, Type = 659, Position = new Vec2(1790, 970), Width = 32, Height = 30,
                Life = 100, LifeMax = 100, Damage = 30, Chaseable = true
            });
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent >= 0);
            s.Targets.RemoveAt(1);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Type = 921, Position = new Vec2(1820, 970),
                Width = 12, Height = 12, Damage = 30, TimeLeft = 100
            });
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent >= 0);
        }

        private static void QueenMalformedNativeThreatClosesReturn()
        {
            var s = QueenReturnSnapshot();
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
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent >= 0);
        }

        private static void QueenNoLateReturn()
        {
            var s = QueenReturnSnapshot();
            var queen = s.Targets[0];
            queen.Ai1 = 120;
            s.Targets[0] = queen;
            Equal(1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenGroundedBossGravityEnvelope()
        {
            var s = QueenReturnSnapshot();
            var queen = s.Targets[0];
            queen.Life = queen.LifeMax;
            queen.Position = new Vec2(1700f, 780f);
            queen.Velocity = new Vec2(3f, 0f);
            s.Targets[0] = queen;
            // Current body ends above the return corridor. A future footprint
            // has no verified Boss support; the .3 gravity envelope reaches it.
            // Keep Queen left of the player: otherwise initial AwayX is already
            // left, so a left result says nothing about optional return safety.
            True(s.Player.Center.X > queen.Center.X);
            True(queen.Position.Y + queen.Height < s.Player.Position.Y - 32f);
            True(queen.Position.Y + queen.Height + .15f * 24f * 25f > s.Player.Position.Y - 32f);
            True(new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent >= 0);
        }

        private static void QueenUnknownFloor()
        {
            var s = QueenSnapshot();
            s.Arena.FloorSupport = default(SupportSpan);
            s.Arena.RecoverySupport = default(SupportSpan);
            s.Arena.HasFloor = true; // A broad hint is not a verified runway.
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenUnknownState()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 99;
            s.Targets[0] = queen;
            s.Player.OnGround = false;
            s.Player.Position.Y = 740;
            var d = new BossStrategyEngine().Evaluate(s).Directive;
            Equal("classic-unrecognized-native-state", d.PhaseId);
            Equal(0, d.HorizontalIntent);
            Equal(JumpAction.Release, d.JumpAction);
        }

        private static void QueenPressureTarget()
        {
            var s = QueenSnapshot();
            s.Targets.Add(new TargetSnapshot
            {
                Key = 82, Type = 660, Position = new Vec2(1840, 960), Width = 32, Height = 30,
                Life = 100, LifeMax = 100, Damage = 30, Chaseable = true,
                LineOfSightKnown = true, HasLineOfSight = true
            });
            var d = new BossStrategyEngine().Evaluate(s);
            Equal(660, d.Target.Type);
            Equal(657, d.PatternTarget.Type);
            True(d.Directive.Fire);
        }

        private static void QueenFireGuards()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.HasLineOfSight = false;
            s.Targets[0] = queen;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
            queen.HasLineOfSight = true;
            queen.Invulnerable = true;
            s.Targets[0] = queen;
            False(new BossStrategyEngine().Evaluate(s).Directive.Fire);
        }

        private static void QueenResetDirection()
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Velocity.Y = -1f;
            s.Targets[0] = queen;
            var engine = new BossStrategyEngine();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Position.X = 2300;
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            engine.Reset();
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static CombatSnapshot QueenSpacingSnapshot(int side = 1)
        {
            var s = QueenSnapshot();
            var queen = s.Targets[0];
            s.Player.Position.X = queen.Center.X + side * 253f - s.Player.Width * .5f;
            s.Player.Velocity.X = side * 6f;
            s.Arena.FloorSupport.Left = -10000f;
            s.Arena.FloorSupport.Right = 10000f;
            s.Arena.ClearanceLeft = s.Arena.ClearanceRight = 10000f;
            return s;
        }

        private static void AdvanceQueenPlayer(CombatSnapshot s, BossStrategyEngine engine)
        {
            var d = engine.Evaluate(s).Directive;
            float travel;
            s.Player.Velocity.X = HorizontalMotion.Advance(s.Player, s.Player.Velocity.X,
                d.HorizontalIntent, true, 1, out travel);
            s.Player.Position.X += travel;
            Equal(0, d.VerticalIntent);
            Equal(JumpAction.Release, d.JumpAction);
        }

        private static void QueenRepeatedAttackSpacing()
        {
            foreach (var side in new[] { -1, 1 })
            {
                var s = QueenSpacingSnapshot(side);
                var queen = s.Targets[0];
                var engine = new BossStrategyEngine();
                // A synthetic repeated state sequence, not a replay of native
                // Queen AI: it isolates the previous cumulative-run regression.
                for (var tick = 0; tick < 1200; tick++)
                {
                    var phaseTick = tick % 300;
                    queen.Ai0 = phaseTick < 160 ? 3 : phaseTick < 240 ? 5 : 4;
                    queen.Ai2 = queen.Ai0 == 4 ? 1 : 0;
                    queen.Ai1 = queen.Ai0 == 4 ? 35 : 0;
                    queen.Velocity.Y = queen.Ai0 == 4 ? 1 : 0;
                    s.Targets[0] = queen;
                    AdvanceQueenPlayer(s, engine);
                    var gap = (s.Player.Center.X - queen.Center.X) * side;
                    True(gap >= 250f && gap <= 550f);
                }
                True(System.Math.Abs(s.Player.Velocity.X) < .001f);
            }
        }

        private static void QueenSpacingHysteresis()
        {
            var s = QueenSpacingSnapshot();
            var queen = s.Targets[0];
            var engine = new BossStrategyEngine();
            s.Player.Position.X = queen.Center.X + 550f - s.Player.Width * .5f;
            s.Player.Velocity.X = 0;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            // Move the supplied Boss observation inside the upper bound without
            // reaching the separate resume bound. State changes must not clear it.
            foreach (var phase in new[] { 3f, 5f, 0f, 3f, 5f })
            {
                queen.Ai0 = phase;
                queen.Position.X = s.Player.Center.X - 470f - queen.Width * .5f;
                s.Targets[0] = queen;
                Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            }
            queen.Position.X = s.Player.Center.X - 395f - queen.Width * .5f;
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenSpacingLowFriction()
        {
            var s = QueenSpacingSnapshot();
            s.Player.RunSlowdown = .05f;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            Equal(.05f, s.Player.RunSlowdown);
            // Ordinary drag at the exact same measured gap/speed need not brake.
            s.Player.RunSlowdown = .2f;
            Equal(1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenCloseSlamOverridesHold()
        {
            var s = QueenSpacingSnapshot();
            var queen = s.Targets[0];
            s.Player.Position.X = queen.Center.X + 550f - s.Player.Width * .5f;
            s.Player.Velocity.X = 0;
            var engine = new BossStrategyEngine();
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Position.X = s.Player.Center.X - 180f - queen.Width * .5f;
            queen.Ai0 = 4;
            queen.Ai2 = 1;
            queen.Velocity = new Vec2(2, -1);
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Position.X = s.Player.Center.X + 80f - queen.Width * .5f;
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenSlamRiseDrift()
        {
            var s = QueenSpacingSnapshot();
            var queen = s.Targets[0];
            s.Player.Position.X = queen.Center.X + 700f - s.Player.Width * .5f;
            s.Player.Velocity.X = 0;
            queen.Ai0 = 4;
            queen.Ai2 = 0;
            queen.Ai1 = 30;
            queen.Velocity = new Vec2(14, -6);
            s.Targets[0] = queen;
            // Current body is far away, but 30 remaining rise movements plus
            // drop drift can invade the exit. A current-gap-only stop is unsafe.
            Equal(1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            queen.Velocity.X = 0;
            s.Targets[0] = queen;
            Equal(0, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
            queen.Ai1 = 0;
            queen.Velocity.Y = 0;
            s.Targets[0] = queen;
            Equal(1, new BossStrategyEngine().Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenBoundedRecovery()
        {
            foreach (var side in new[] { -1, 1 })
            {
                var s = QueenSpacingSnapshot(side);
                var queen = s.Targets[0];
                s.Player.Position.X = queen.Center.X + side * 900f - s.Player.Width * .5f;
                s.Player.Velocity.X = 0;
                var engine = new BossStrategyEngine();
                Equal(-side, engine.Evaluate(s).Directive.HorizontalIntent);
                for (var tick = 0; tick < 300; tick++)
                {
                    AdvanceQueenPlayer(s, engine);
                    True((s.Player.Center.X - queen.Center.X) * side > 330f);
                }
                var gap = (s.Player.Center.X - queen.Center.X) * side;
                True(gap >= 390f && gap <= 560f);
            }
        }

        private static void QueenGuardedFarRecovery()
        {
            var s = QueenSpacingSnapshot();
            var queen = s.Targets[0];
            s.Player.Position.X = queen.Center.X + 900f - s.Player.Width * .5f;
            s.Player.Velocity.X = 0;
            var engine = new BossStrategyEngine();
            s.Targets.Add(new TargetSnapshot
            {
                Key = 82, Type = 659, Position = s.Player.Position, Width = 32, Height = 30,
                Life = 100, LifeMax = 100, Damage = 30, Chaseable = true
            });
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            s.Targets.RemoveAt(1);

            // P1 state 3 creates no projectile. The 24-tick corridor includes
            // its possible next-tick -13/4.5 launch and is clear here.
            queen.Life = queen.LifeMax;
            queen.Ai0 = 3;
            queen.Ai1 = 0;
            queen.Ai2 = 0;
            s.Targets[0] = queen;
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);

            // Early state 5 cannot fire inside 24 ticks: 35 -> 50 takes
            // fifteen updates and the firing substate needs ten more.
            queen.Life = queen.LifeMax / 2;
            queen.Ai0 = 5;
            queen.Ai1 = 35;
            queen.Ai2 = 0;
            s.Targets[0] = queen;
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Ai1 = 36;
            s.Targets[0] = queen;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Ai2 = 1;
            queen.Ai1 = 0;
            s.Targets[0] = queen;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);

            // State 4 can produce a new 922 on any landing update. It must keep
            // the committed outward exit rather than turn inward for the leash.
            queen.Ai0 = 4;
            queen.Ai1 = 0;
            queen.Ai2 = 0;
            queen.Velocity = default(Vec2);
            s.Targets[0] = queen;
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);

            queen.Ai0 = 0;
            queen.Ai1 = 97;
            s.Targets[0] = queen;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Ai1 = 0;
            s.Targets[0] = queen;
            Equal(-1, engine.Evaluate(s).Directive.HorizontalIntent);
            s.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Type = 921, Position = s.Player.Position,
                Width = 12, Height = 12, TimeLeft = 100, Damage = 30
            });
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenResetSpacing()
        {
            var s = QueenSpacingSnapshot();
            var queen = s.Targets[0];
            s.Player.Position.X = queen.Center.X + 550f - s.Player.Width * .5f;
            s.Player.Velocity.X = 0;
            var engine = new BossStrategyEngine();
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            queen.Position.X += 80f;
            s.Targets[0] = queen;
            Equal(0, engine.Evaluate(s).Directive.HorizontalIntent);
            engine.Reset();
            Equal(1, engine.Evaluate(s).Directive.HorizontalIntent);
        }

        private static void QueenNativeTtlLeashSequence()
        {
            foreach (var side in new[] { -1, 1 })
            {
                var s = QueenSpacingSnapshot(side);
                var queen = s.Targets[0];
                s.Player.Position.X = queen.Center.X + side * 900f - s.Player.Width * .5f;
                s.Player.Velocity.X = 0;
                var engine = new BossStrategyEngine();
                var maximumGap = 0f;
                var maximumOutsideTicks = 0;
                var outsideTicks = 0;
                for (var tick = 0; tick < 1800; tick++)
                {
                    var phaseTick = tick % 400;
                    queen.Velocity = default(Vec2);
                    queen.Ai1 = queen.Ai2 = 0;
                    if (phaseTick < 100)
                    {
                        queen.Life = queen.LifeMax;
                        queen.Ai0 = 3;
                    }
                    else if (phaseTick < 200)
                    {
                        queen.Life = queen.LifeMax / 2;
                        queen.Ai0 = 5; // early, guarded gel windup
                    }
                    else if (phaseTick < 300)
                    {
                        queen.Life = queen.LifeMax / 2;
                        queen.Ai0 = 4;
                        queen.Ai2 = 1;
                        queen.Ai1 = 35;
                        queen.Velocity.Y = 1;
                    }
                    else
                    {
                        queen.Life = queen.LifeMax / 2;
                        queen.Ai0 = 0;
                    }
                    s.Targets[0] = queen;
                    AdvanceQueenPlayer(s, engine);

                    var gap = System.Math.Abs(s.Player.Center.X - queen.Center.X);
                    maximumGap = System.Math.Max(maximumGap, gap);
                    // Exact horizontal rectangle used by NPC.CheckActive for
                    // Queen: fixed sWidth/2 plus the NPC width on each side.
                    var activityLeft = queen.Center.X - 960f - queen.Width;
                    var activityRight = queen.Center.X + 960f + queen.Width;
                    var overlapsActivity = s.Player.Position.X + s.Player.Width > activityLeft &&
                        s.Player.Position.X < activityRight;
                    outsideTicks = overlapsActivity ? 0 : outsideTicks + 1;
                    maximumOutsideTicks = System.Math.Max(maximumOutsideTicks, outsideTicks);
                }
                Equal(0, maximumOutsideTicks);
                True(maximumGap < 960f + queen.Width + s.Player.Width * .5f);
            }
        }
    }
}
