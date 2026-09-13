using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>
        /// Small, deterministic regressions for the failure seen in the focused
        /// Queen Bee run: the native bee body crossed the player's runway while
        /// states 1/2/3 kept holding the old signed run direction.  These tests
        /// intentionally exercise the strategy/planner only; they do not launch
        /// Terraria or mutate a save.
        /// </summary>
        private static void RunQueenBeeContactRegressions()
        {
            Run(nameof(QueenBeeContactGuardHandlesBothRunwayDirections),
                QueenBeeContactGuardHandlesBothRunwayDirections);
            Run(nameof(QueenBeeContactLatchFlipsAfterBodyCrossing),
                QueenBeeContactLatchFlipsAfterBodyCrossing);
            Run(nameof(QueenBeeNoDashKeepsCommittedChargeEscape),
                QueenBeeNoDashKeepsCommittedChargeEscape);
            Run(nameof(QueenBeeProjectileOverlapLeavesRunway),
                QueenBeeProjectileOverlapLeavesRunway);
            Run(nameof(QueenBeeProjectileOnEscapeSideStillGetsVerticalDodge),
                QueenBeeProjectileOnEscapeSideStillGetsVerticalDodge);
            Run(nameof(QueenBeeProjectileDirectionLatchesAcrossNativeSlotGap),
                QueenBeeProjectileDirectionLatchesAcrossNativeSlotGap);
            Run(nameof(QueenBeeState0CenteredProjectileLeavesRunway),
                QueenBeeState0CenteredProjectileLeavesRunway);
            Run(nameof(QueenBeeAmbientManEaterContactGetsExplicitJump),
                QueenBeeAmbientManEaterContactGetsExplicitJump);
            Run(nameof(QueenBeeSmallBeeGetsEarlyJumpWarning),
                QueenBeeSmallBeeGetsEarlyJumpWarning);
            Run(nameof(QueenBeeOwnedEscapeReversesAtObservedRunwayEdge),
                QueenBeeOwnedEscapeReversesAtObservedRunwayEdge);
            Run(nameof(QueenBeeOwnedEscapeUsesLocalOpenBounds),
                QueenBeeOwnedEscapeUsesLocalOpenBounds);
            Run(nameof(QueenBeeOwnedEscapeUsesClearanceWhenSupportIsWide),
                QueenBeeOwnedEscapeUsesClearanceWhenSupportIsWide);
            Run(nameof(QueenBeeExhaustedFlightSuppressesEdgeJump),
                QueenBeeExhaustedFlightSuppressesEdgeJump);
            Run(nameof(QueenBeeChargeAlignmentBrakesBeforeProjectileLane),
                QueenBeeChargeAlignmentBrakesBeforeProjectileLane);
            Run(nameof(QueenBeeState0ChargeOutranksLateralProjectile),
                QueenBeeState0ChargeOutranksLateralProjectile);
        }

        private static void QueenBeeContactGuardHandlesBothRunwayDirections()
        {
            foreach (var direction in new[] { -1, 1 })
            foreach (var state in new[] { 1, 2, 3 })
            {
                var scene = CombatScenario(222);
                scene.Arena.ClearanceLeft = direction < 0 ? 2200 : 900;
                scene.Arena.ClearanceRight = direction > 0 ? 2200 : 900;
                var player = scene.Player;
                player.Position = new Vec2(1500f, 800f);
                player.Velocity = new Vec2(direction * 3f, 0f);
                player.OnGround = true;
                player.Jump = GroundJumpForRegression();
                scene.Player = player;

                var bee = scene.Targets[0];
                bee.Width = 90;
                bee.Height = 90;
                bee.Position = new Vec2(direction > 0 ? 1532f : 1408f, 790f);
                bee.Velocity = new Vec2(-direction * 1.5f, 0f);
                bee.Ai0 = state;
                bee.Ai1 = state == 1 ? 5f : state == 2 ? 0f : 10f;
                bee.Ai2 = 0f;
                scene.Targets[0] = bee;
                RefreshPriorityNativeContext(scene);

                var directive = new BossStrategyEngine().Evaluate(scene).Directive;
                False(directive.RequestControlReturn,
                    "state " + state + " direction " + direction +
                    " unexpectedly lost its native contract");
                // A runway controller may choose a vertical escape, but it
                // must not keep steering into the bee's overlapping body.
                True(direction > 0 ? directive.HorizontalIntent <= 0 :
                    directive.HorizontalIntent >= 0,
                    "state " + state + " direction " + direction +
                    " steered into Queen Bee: h=" + directive.HorizontalIntent +
                    " phase=" + directive.PhaseId);
            }
        }

        private static void QueenBeeNoDashKeepsCommittedChargeEscape()
        {
            var scene = CombatScenario(222);
            scene.Mobility.CanDash = false;
            scene.Mobility.DashReady = false;
            scene.Mobility.EyeShieldDash = default(EyeShieldDashState);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var bee = scene.Targets[0];
            bee.Ai0 = 0f;
            bee.Ai1 = 1f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(scene.Player.Position.X - 420f,
                scene.Player.Position.Y);
            bee.Velocity = new Vec2(16f, 0f);
            scene.Targets[0] = bee;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = scene.Player.Position + new Vec2(20f, 0f),
                Velocity = new Vec2(0f, 0f),
                Width = 46,
                Height = 46,
                Damage = 68,
                TimeLeft = int.MaxValue,
                Type = 43
            });
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason +
                " phase=" + plan.PhaseId);
            False(plan.Dash, "no-dash fixture emitted a dash input");
            True(plan.Jump, "no-dash committed charge lost its jump escape");
        }

        private static void QueenBeeContactLatchFlipsAfterBodyCrossing()
        {
            var scene = CombatScenario(222);
            scene.Arena.ClearanceLeft = 2200f;
            scene.Arena.ClearanceRight = 2200f;
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var bee = scene.Targets[0];
            bee.Ai0 = 1f;
            bee.Ai1 = 5f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(1560f, 790f);
            bee.Velocity = new Vec2(-16f, 0f);
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();
            var first = engine.Evaluate(scene).Directive;
            True(first.HorizontalIntent <= 0,
                "right-side crossing did not reserve the left escape lane");

            // Keep the same native slot/state but move the observed body to
            // the opposite side, as happens after a fast Queen crossing. The
            // contact latch must not keep holding the stale left direction.
            bee.Position = new Vec2(1400f, 790f);
            bee.Velocity = new Vec2(16f, 0f);
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);
            var second = engine.Evaluate(scene).Directive;
            True(second.HorizontalIntent >= 0,
                "post-crossing contact latch kept steering into stale side");
        }

        private static void QueenBeeProjectileOverlapLeavesRunway()
        {
            foreach (var projectileType in new[] { 176, 719, 55 })
            {
                var scene = CombatScenario(222);
                scene.Player.OnGround = true;
                scene.Player.Jump = GroundJumpForRegression();
                var bee = scene.Targets[0];
                bee.Ai0 = 1f;
                bee.Ai1 = 5f;
                bee.Ai2 = 0f;
                // Keep the Queen on the right runway; the projectile
                // approaches from that same side and crosses the player's
                // body in ~12 ticks.
                bee.Position = new Vec2(1840f, 790f);
                bee.Velocity = new Vec2(-1f, 0f);
                scene.Targets[0] = bee;
                scene.Threats.Add(new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Geometry = ThreatGeometry.Body,
                    Trajectory = ThreatTrajectory.Linear,
                    Position = new Vec2(scene.Player.Position.X + 270f,
                        scene.Player.Position.Y + 8f),
                    Velocity = new Vec2(-22f, 0f),
                    Width = 14,
                    Height = 14,
                    Damage = 28,
                    TimeLeft = 90,
                    Type = projectileType
                });
                RefreshPriorityNativeContext(scene);

                var planner = new CombatPlanner(new PlannerSettings());
                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn, plan.ControlReturnReason);
                True(planner.LastRelevantThreatCount > 0,
                    "approaching Queen projectile " + projectileType +
                    " was not admitted to the scorer");
                // The old stable runway (+1) is unsafe here.  The planner may
                // brake/reverse or jump, but it may not commit to an unbroken
                // rightward run through the incoming projectile.
                True(plan.Horizontal < 0 || plan.Jump,
                    "projectile " + projectileType +
                    " retained unsafe runway: h=" + plan.Horizontal +
                    " jump=" + plan.Jump + " phase=" + plan.PhaseId);
            }
        }

        private static void QueenBeeProjectileOnEscapeSideStillGetsVerticalDodge()
        {
            // The contact guard owns the horizontal escape side (left) while
            // an incoming stinger approaches from that same side.  Projectile
            // scoring must still retain a vertical candidate; otherwise the
            // guard itself would make the player run into the next hazard.
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var bee = scene.Targets[0];
            bee.Ai0 = 1f;
            bee.Ai1 = 5f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(1840f, 790f);
            bee.Velocity = new Vec2(-1f, 0f);
            scene.Targets[0] = bee;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(scene.Player.Position.X - 270f,
                    scene.Player.Position.Y + 8f),
                Velocity = new Vec2(22f, 0f),
                Width = 14,
                Height = 14,
                Damage = 28,
                TimeLeft = 90,
                Type = 719
            });
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            True(planner.LastRelevantThreatCount > 0,
                "escape-side projectile was not admitted to the scorer");
            True(plan.Jump || plan.Horizontal > 0,
                "contact-owned escape lane ignored projectile on its side: h=" +
                    plan.Horizontal + " jump=" + plan.Jump + " phase=" +
                    plan.PhaseId);
        }

        private static void QueenBeeProjectileDirectionLatchesAcrossNativeSlotGap()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var bee = scene.Targets[0];
            bee.Ai0 = 1f;
            bee.Ai1 = 5f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(1840f, 790f);
            bee.Velocity = new Vec2(-1f, 0f);
            scene.Targets[0] = bee;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(scene.Player.Position.X + 270f,
                    scene.Player.Position.Y + 8f),
                Velocity = new Vec2(-22f, 0f),
                Width = 14,
                Height = 14,
                Damage = 28,
                TimeLeft = 90,
                Type = 55
            });
            RefreshPriorityNativeContext(scene);
            var planner = new CombatPlanner(new PlannerSettings());
            var first = planner.Plan(scene);
            True(first.Horizontal < 0,
                "right-to-left projectile did not select travel-direction escape");

            // Simulate a native slot disappearing for one snapshot. The
            // short latch must retain the signed escape instead of handing
            // control straight back to the stable runway orbit.
            scene.Threats.Clear();
            var second = planner.Plan(scene);
            True(second.Horizontal < 0,
                "projectile escape direction was lost across a slot gap");
        }

        private static void QueenBeeState0CenteredProjectileLeavesRunway()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var bee = scene.Targets[0];
            bee.Ai0 = 0f;
            bee.Ai1 = 1f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(scene.Player.Position.X - 420f,
                scene.Player.Position.Y);
            bee.Velocity = new Vec2(16f, 0f);
            scene.Targets[0] = bee;
            // A centered, low-lateral-speed stinger can overlap before its
            // native slot acquires a useful side. State 0 must not answer with
            // horizontal=0 and a jump alone.
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(scene.Player.Position.X + 8f,
                    scene.Player.Position.Y + 12f),
                Velocity = new Vec2(0f, 0f),
                Width = 10,
                Height = 10,
                Damage = 13,
                TimeLeft = 90,
                Type = 176
            });
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            True(plan.Horizontal != 0,
                "state 0 centered stinger left the player stationary: h=" +
                plan.Horizontal + " jump=" + plan.Jump + " phase=" +
                plan.PhaseId);
        }

        private static void QueenBeeAmbientManEaterContactGetsExplicitJump()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 20f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2200f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            // NPC 43 is Man Eater.  It is a hostile ground-level contact,
            // not a Queen projectile, and must be handled even while the
            // Queen controller owns a signed runway direction.
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = scene.Player.Position + new Vec2(20f, 0f),
                Velocity = new Vec2(0f, 0f),
                Width = 46,
                Height = 46,
                Damage = 68,
                TimeLeft = int.MaxValue,
                Type = 43
            });
            RefreshPriorityNativeContext(scene);

            var direct = new BossStrategyEngine().Evaluate(scene).Directive;
            True(direct.PhaseId.Contains("ambient-pressure"),
                "strategy did not see Man Eater threat count=" +
                scene.Threats.Count + " phase=" + direct.PhaseId +
                " h=" + direct.HorizontalIntent + " v=" +
                direct.VerticalIntent + " owns=" + direct.OwnsMovementClosure);
            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            True(plan.Jump,
                "nearby Man Eater contact did not reserve a jump: h=" +
                plan.Horizontal + " phase=" + plan.PhaseId);
            True(plan.Horizontal < 0,
                "nearby right-side Man Eater did not reserve the left lane: h=" +
                plan.Horizontal + " phase=" + plan.PhaseId);
            True(plan.PhaseId.Contains("ambient-pressure"), plan.PhaseId);
        }

        private static void QueenBeeSmallBeeGetsEarlyJumpWarning()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Position = new Vec2(1500f, 858f);
            scene.Player.Jump = GroundJumpForRegression();
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 20f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2200f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;

            // BeeSmall (NPC 211) is a Queen-spawned minion.  At this distance
            // it crosses the player's vertical corridor in roughly 25 ticks:
            // outside the ordinary 24-tick ambient horizon, but inside the
            // reviewed early-warning window.  The grounded player must spend
            // the available jump before the minion reaches contact range.
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.NpcContact,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1506f, 686f),
                Velocity = new Vec2(1.1f, 6f),
                Width = 8,
                Height = 8,
                Damage = 18,
                TimeLeft = 749,
                Type = 211
            });
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            True(plan.PhaseId.Contains("ambient-pressure"),
                "BeeSmall contact did not enter ambient pressure closure: " +
                plan.PhaseId);
            True(plan.Jump,
                "BeeSmall early warning did not reserve a jump: h=" +
                plan.Horizontal + " phase=" + plan.PhaseId);
        }

        private static void QueenBeeState0ChargeOutranksLateralProjectile()
        {
            var scene = CombatScenario(222);
            // The odd ai[1]==7 tuple is only reachable after the additional
            // Expert charge bands have been admitted.  Keep the fixture's
            // difficulty explicit instead of accidentally testing the
            // Classic six-sequence contract.
            scene.Difficulty.Expert = true;
            var player = scene.Player;
            player.Position = new Vec2(1500f, 800f);
            player.Velocity = new Vec2(3.7f, 2.46f);
            player.OnGround = false;
            player.Jump = new JumpSnapshot
            {
                Known = true,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = false,
                CloudAvailable = true,
                RemainingTicks = 0
            };
            scene.Player = player;
            var queen = scene.Targets[0];
            queen.Position = new Vec2(1632f, 735f);
            queen.Velocity = new Vec2(-20f, -.5f);
            queen.Ai0 = 0f;
            queen.Ai1 = 7f;
            queen.Ai2 = 0f;
            queen.Life = 1000;
            scene.Targets[0] = queen;
            // A projectile travelling left-to-right would normally request
            // horizontal=+1.  That lane is the Queen's committed charge line
            // and must not override the charge escape while TTC is short.
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1230f, 808f),
                Velocity = new Vec2(22f, 0f),
                Width = 14,
                Height = 14,
                Damage = 28,
                TimeLeft = 90,
                Type = 55
            });
            RefreshPriorityNativeContext(scene);

            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            True(plan.Horizontal <= 0 || plan.Jump,
                "state 0 charge accepted lateral projectile lane: h=" +
                plan.Horizontal + " jump=" + plan.Jump + " phase=" +
                plan.PhaseId);
            True(plan.PhaseId.Contains("charge-contact-priority"),
                plan.PhaseId);
        }

        private static void QueenBeeOwnedEscapeReversesAtObservedRunwayEdge()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Velocity = new Vec2(3.4f, 0f);
            scene.Player.Jump = GroundJumpForRegression();
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true,
                Inverted = false,
                OneWay = false,
                Left = 1200f,
                Right = 1800f,
                SurfaceY = 900f
            };
            scene.Arena.RecoverySupport = scene.Arena.FloorSupport;
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 5f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2300f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            RefreshPriorityNativeContext(scene);

            var engine = new BossStrategyEngine();
            var initial = engine.Evaluate(scene).Directive;
            False(initial.RequestControlReturn,
                "initial return=" + initial.RequestControlReturn +
                " reason=" + initial.ControlReturnReason +
                " phase=" + initial.PhaseId +
                " h=" + initial.HorizontalIntent +
                " v=" + initial.VerticalIntent);

            // Simulate a projectile/contact escape arriving while the player
            // is already at the right edge.  The source strategy owns its
            // horizontal closure, yet the hard support boundary must still
            // reverse it instead of sending the player beyond the platform.
            var player = scene.Player;
            player.Position = new Vec2(1700f, 858f);
            player.Velocity = new Vec2(3.4f, 0f);
            player.OnGround = true;
            scene.Player = player;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1300f, 866f),
                Velocity = new Vec2(20f, 0f),
                Width = 14,
                Height = 14,
                Damage = 28,
                TimeLeft = 90,
                Type = 55
            });
            var edge = engine.Evaluate(scene).Directive;
            False(edge.RequestControlReturn,
                "return=" + edge.RequestControlReturn +
                " reason=" + edge.ControlReturnReason +
                " phase=" + edge.PhaseId +
                " h=" + edge.HorizontalIntent +
                " v=" + edge.VerticalIntent);
            True(edge.HorizontalIntent < 0,
                "owned Queen escape ignored observed support edge: h=" +
                    edge.HorizontalIntent + " v=" + edge.VerticalIntent +
                    " phase=" + edge.PhaseId);
            True(edge.PhaseId.Contains("runway-edge-reversal"), edge.PhaseId);
        }

        private static void QueenBeeOwnedEscapeUsesLocalOpenBounds()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, Inverted = false, OneWay = false,
                Left = 1000f, Right = 4000f, SurfaceY = 900f
            };
            scene.Arena.RecoverySupport = scene.Arena.FloorSupport;
            // Keep the measured support wide, but expose a locally verified
            // open rectangle whose right edge is the real obstruction for this
            // frame. Clearance alone is intentionally generous so this test
            // proves LocalOpenBounds participates in the closure.
            scene.Arena.LocalOpenBounds = new RectF(1000f, 0f, 650f, 1200f);
            scene.Arena.ClearanceLeft = 2000f;
            scene.Arena.ClearanceRight = 2000f;
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 5f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2300f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();
            engine.Evaluate(scene); // witness support before the escape frame

            var player = scene.Player;
            player.Position = new Vec2(1500f, 858f);
            player.Velocity = new Vec2(3.4f, 0f);
            scene.Player = player;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1300f, 866f),
                Velocity = new Vec2(20f, 0f),
                Width = 14, Height = 14, Damage = 28,
                TimeLeft = 90, Type = 55
            });
            var edge = engine.Evaluate(scene).Directive;
            True(edge.HorizontalIntent < 0,
                "LocalOpenBounds did not close owned escape: h=" +
                    edge.HorizontalIntent + " phase=" + edge.PhaseId);
            True(edge.PhaseId.Contains("runway-edge-reversal"), edge.PhaseId);
        }

        private static void QueenBeeOwnedEscapeUsesClearanceWhenSupportIsWide()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, Inverted = false, OneWay = false,
                Left = 1000f, Right = 4000f, SurfaceY = 900f
            };
            scene.Arena.RecoverySupport = scene.Arena.FloorSupport;
            scene.Arena.LocalOpenBounds = new RectF(0f, 0f, 5000f, 2400f);
            scene.Arena.ClearanceLeft = 2000f;
            // Keep the value above the projectile helper's fixed 112px lane
            // switch; the Queen boundary guard must still honor the larger
            // braking margin derived from the player's real slowdown.
            scene.Arena.ClearanceRight = 200f;
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 5f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2300f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();
            engine.Evaluate(scene);
            var player = scene.Player;
            player.Position = new Vec2(1500f, 858f);
            player.Velocity = new Vec2(3.4f, 0f);
            scene.Player = player;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1300f, 866f),
                Velocity = new Vec2(20f, 0f),
                Width = 14, Height = 14, Damage = 28,
                TimeLeft = 90, Type = 55
            });
            var edge = engine.Evaluate(scene).Directive;
            True(edge.HorizontalIntent < 0,
                "Clearance did not close owned escape: h=" +
                    edge.HorizontalIntent + " phase=" + edge.PhaseId);
            True(edge.PhaseId.Contains("runway-edge-reversal"), edge.PhaseId);
        }

        private static void QueenBeeExhaustedFlightSuppressesEdgeJump()
        {
            var scene = CombatScenario(222);
            scene.Player.OnGround = true;
            scene.Player.Jump = GroundJumpForRegression();
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true, Inverted = false, OneWay = false,
                Left = 1200f, Right = 1800f, SurfaceY = 900f
            };
            scene.Arena.RecoverySupport = scene.Arena.FloorSupport;
            var queen = scene.Targets[0];
            queen.Ai0 = 1f;
            queen.Ai1 = 5f;
            queen.Ai2 = 0f;
            queen.Position = new Vec2(2300f, 650f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();
            engine.Evaluate(scene);

            var player = scene.Player;
            player.Position = new Vec2(1700f, 858f);
            player.Velocity = new Vec2(3.4f, 1f);
            player.OnGround = false;
            player.Flight = new FlightSnapshot
            {
                Known = true, WingsLogic = 1, WingTime = 0f,
                WingTimeMax = 100, RocketBoots = 0
            };
            scene.Player = player;
            scene.Mobility.HasFiniteFlightResource = true;
            scene.Mobility.FlightResourceFraction = 0f;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1300f, 866f),
                Velocity = new Vec2(20f, 0f),
                Width = 14, Height = 14, Damage = 28,
                TimeLeft = 90, Type = 55
            });
            var edge = engine.Evaluate(scene).Directive;
            True(edge.HorizontalIntent < 0,
                "exhausted flight ignored support edge: h=" +
                    edge.HorizontalIntent + " phase=" + edge.PhaseId);
            Equal(0, edge.VerticalIntent);
            Equal(JumpAction.Release, edge.JumpAction);
            True(edge.OwnsMovementClosure, "exhausted flight did not close jump");
        }

        private static void QueenBeeChargeAlignmentBrakesBeforeProjectileLane()
        {
            var scene = CombatScenario(222);
            scene.Difficulty.Expert = true;
            var player = scene.Player;
            player.Position = new Vec2(1500f, 800f);
            player.Velocity = new Vec2(5f, 0f);
            player.OnGround = true;
            player.Jump = GroundJumpForRegression();
            scene.Player = player;
            var queen = scene.Targets[0];
            queen.Ai0 = 0f;
            queen.Ai1 = 0f; // native even alignment frame
            queen.Ai2 = 0f;
            queen.Position = new Vec2(1750f, 735f);
            queen.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = queen;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.Linear,
                Position = new Vec2(1200f, 808f),
                Velocity = new Vec2(22f, 0f),
                Width = 14,
                Height = 14,
                Damage = 28,
                TimeLeft = 90,
                Type = 55
            });
            RefreshPriorityNativeContext(scene);

            var directive = new BossStrategyEngine().Evaluate(scene).Directive;
            False(directive.RequestControlReturn, directive.ControlReturnReason);
            True(directive.PhaseId.Contains("charge-alignment-priority"),
                directive.PhaseId);
            // Alignment keeps the native brake lane unless the verified body
            // closure has to step away from an overlapping Queen.  In this
            // fixture the Queen is to the right, so a negative step is the
            // only permitted override; the projectile's +1 lane is unsafe.
            True(directive.HorizontalIntent == 0 ||
                directive.HorizontalIntent < 0,
                "alignment frame kept projectile lane: h=" +
                directive.HorizontalIntent + " phase=" + directive.PhaseId);
            True(directive.VerticalIntent != 0 ||
                directive.JumpAction == JumpAction.Hold ||
                directive.JumpAction == JumpAction.Cloud,
                "alignment frame did not reserve a vertical dodge: v=" +
                directive.VerticalIntent + " action=" +
                directive.JumpAction);
        }

        private static JumpSnapshot GroundJumpForRegression()
        {
            return new JumpSnapshot
            {
                Known = true,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = true,
                CloudAvailable = true
            };
        }
    }
}
