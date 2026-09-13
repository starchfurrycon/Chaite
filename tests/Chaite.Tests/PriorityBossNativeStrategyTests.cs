using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPriorityBossNativeStrategyRegressions()
        {
            Run(nameof(QueenBeeUsesNativeStateInsteadOfVelocity),
                QueenBeeUsesNativeStateInsteadOfVelocity);
            Run(nameof(QueenBeeCommittedChargeUsesGroundExecutableJump),
                QueenBeeCommittedChargeUsesGroundExecutableJump);
            Run(nameof(QueenBeeRealChargeFramesKeepClosedJumpRoute),
                QueenBeeRealChargeFramesKeepClosedJumpRoute);
            Run(nameof(QueenBeeChargeUsesBoundedCollisionWindow),
                QueenBeeChargeUsesBoundedCollisionWindow);
            Run(nameof(QueenBeeStingerRetainsEntryEnvelopeAcrossLifeBand),
                QueenBeeStingerRetainsEntryEnvelopeAcrossLifeBand);
            Run(nameof(QueenBeeSelectsNearbyHivePressure),
                QueenBeeSelectsNearbyHivePressure);
            Run(nameof(QueenBeeRetainsCurrentHivePressure),
                QueenBeeRetainsCurrentHivePressure);
            Run(nameof(FishronStateSevenAndTwelveAreNeverCharges),
                FishronStateSevenAndTwelveAreNeverCharges);
            Run(nameof(FishronPhaseComesFromNativeStateNotLifeGuess),
                FishronPhaseComesFromNativeStateNotLifeGuess);
            Run(nameof(ChargeControllersLeaveVerticalAndDiagonalAttackLines),
                ChargeControllersLeaveVerticalAndDiagonalAttackLines);
            Run(nameof(EmpressUsesNativeAttackTableAndRageLatch),
                EmpressUsesNativeAttackTableAndRageLatch);
            Run(nameof(EmpressDayPreDashReservesPerpendicularClearance),
                EmpressDayPreDashReservesPerpendicularClearance);
            Run(nameof(MoonLordBalancesEyesAndReadsPreSpawnClocks),
                MoonLordBalancesEyesAndReadsPreSpawnClocks);
            Run(nameof(WallDistinguishesChargeClockFromActiveLaserBurst),
                WallDistinguishesChargeClockFromActiveLaserBurst);
            Run(nameof(SkeletronUsesHeadAndHandNativeClocks),
                SkeletronUsesHeadAndHandNativeClocks);
            Run(nameof(DeerclopsTelegraphsBeforeProjectilesExist),
                DeerclopsTelegraphsBeforeProjectilesExist);
            Run(nameof(DeerclopsKeepsJumpingWhileObservedSpikesSurvive),
                DeerclopsKeepsJumpingWhileObservedSpikesSurvive);
            Run(nameof(DeerclopsPreservesCloudForLateSpikeWave),
                DeerclopsPreservesCloudForLateSpikeWave);
            Run(nameof(DeerclopsEscapesContactAndImminentShadowHand),
                DeerclopsEscapesContactAndImminentShadowHand);
            Run(nameof(DeerclopsReturnHomeAllowsAmbientThreatEscape),
                DeerclopsReturnHomeAllowsAmbientThreatEscape);
            Run(nameof(DeerclopsSelectsNearbyAmbientPressure),
                DeerclopsSelectsNearbyAmbientPressure);
            Run(nameof(PriorityStrategiesRequireMatchingProductionContext),
                PriorityStrategiesRequireMatchingProductionContext);
            Run(nameof(NativeEnrageChangesQueenBeeAndFishronSafetyEnvelope),
                NativeEnrageChangesQueenBeeAndFishronSafetyEnvelope);
            Run(nameof(MoonLordUsesValidatedSphereAndTongueMetadata),
                MoonLordUsesValidatedSphereAndTongueMetadata);
        }

        private static void QueenBeeUsesNativeStateInsteadOfVelocity()
        {
            var scene = CombatScenario(222);
            var bee = scene.Targets[0];
            bee.Ai0 = 0f;
            bee.Ai1 = 0f;
            bee.Ai2 = 0f;
            bee.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = bee;
            var align = new BossStrategyEngine().Evaluate(scene).Directive;
            True(align.PhaseId.Contains("charge-align-telegraph"));

            bee.Ai1 = 1f;
            scene.Targets[0] = bee;
            var charge = new BossStrategyEngine().Evaluate(scene).Directive;
            True(charge.PhaseId.Contains("horizontal-charge-committed"));
            True(charge.PreferDash);

            bee.Ai0 = 1f;
            bee.Ai1 = 10f;
            bee.Ai2 = 2f;
            bee.Velocity = new Vec2(20f, 0f);
            scene.Targets[0] = bee;
            var wave = new BossStrategyEngine().Evaluate(scene).Directive;
            True(wave.PhaseId.Contains("bee-wave-spacing"));
            False(wave.PreferDash);

            // Expert AI_043 deliberately advances ai[1] by quarter ticks while
            // spawning bees below life thresholds. Fractional here is native,
            // not malformed input and must remain joinable mid-attack.
            scene.Difficulty.Expert = true;
            bee.Ai1 = 10.25f;
            scene.Targets[0] = bee;
            var fractionalWave = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(fractionalWave.PhaseId.Contains("bee-wave-spacing"));
            False(fractionalWave.RequestControlReturn);
        }

        private static void QueenBeeCommittedChargeUsesGroundExecutableJump()
        {
            var scene = CombatScenario(222);
            var bee = scene.Targets[0];
            bee.Ai0 = 0f;
            bee.Ai1 = 1f;
            bee.Ai2 = 0f;
            bee.Position.X = scene.Player.Position.X - 420f;
            bee.Velocity = new Vec2(16f, 0f);
            scene.Targets[0] = bee;
            scene.Player.OnGround = true;
            var committed = new BossStrategyEngine().Evaluate(scene).
                Directive;
            Equal(1, committed.VerticalIntent);
            Equal(0, committed.HorizontalIntent);
            Equal(JumpAction.Hold, committed.JumpAction);
            True(committed.OwnsHorizontalClosure);
            True(committed.OwnsMovementClosure);

            var plan = new CombatPlanner(new PlannerSettings()).Plan(scene);
            True(plan.Jump);
            False(plan.Drop);

            scene.Player.OnGround = false;
            var airborne = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(airborne.OwnsHorizontalClosure);
            True(airborne.OwnsMovementClosure);

            bee.Position.X = scene.Player.Center.X + 20f;
            scene.Targets[0] = bee;
            var crossed = new BossStrategyEngine().Evaluate(scene).
                Directive;
            Equal(0, crossed.VerticalIntent);
            Equal(JumpAction.Release, crossed.JumpAction);
            True(crossed.OwnsMovementClosure);

            bee.Ai1 = 0f;
            scene.Targets[0] = bee;
            var aligning = new BossStrategyEngine().Evaluate(scene).
                Directive;
            Equal(0, aligning.VerticalIntent);
            Equal(JumpAction.Release, aligning.JumpAction);
            True(aligning.OwnsHorizontalClosure);
            True(aligning.OwnsMovementClosure);

            bee.Ai1 = 1f;
            bee.Ai2 = 1f;
            scene.Targets[0] = bee;
            var braking = new BossStrategyEngine().Evaluate(scene).
                Directive;
            Equal(0, braking.VerticalIntent);
            Equal(JumpAction.Release, braking.JumpAction);
            True(braking.OwnsHorizontalClosure);
            True(braking.OwnsMovementClosure);
        }

        private static void QueenBeeRealChargeFramesKeepClosedJumpRoute()
        {
            AssertQueenBeeChargeFrame(35356.6875f, 11158f, 5.75758457f,
                34914.5625f, 11129.5859f, 15.987401f, .634823561f,
                false, false);
            AssertQueenBeeChargeFrame(36906.8672f, 11158f, 3.96879935f,
                36531.0977f, 11131.7832f, 17.9850655f, .7330894f,
                true, true);
        }

        private static void AssertQueenBeeChargeFrame(float playerX,
            float playerY, float playerVelocityX, float queenX, float queenY,
            float queenVelocityX, float queenVelocityY, bool master,
            bool expectedJump)
        {
            var scene = CombatScenario(222);
            scene.Difficulty.Expert = !master;
            scene.Difficulty.Master = master;
            scene.Player.Position = new Vec2(playerX, playerY);
            scene.Player.Velocity = new Vec2(playerVelocityX, 0f);
            scene.Player.OnGround = true;
            scene.Player.Jump = new JumpSnapshot
            {
                Known = true,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = true
            };
            var bee = scene.Targets[0];
            bee.Position = new Vec2(queenX, queenY);
            bee.Velocity = new Vec2(queenVelocityX, queenVelocityY);
            bee.Width = 66;
            bee.Height = 66;
            bee.Ai0 = 0f;
            bee.Ai1 = 1f;
            bee.Ai2 = 0f;
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);

            var directive = new BossStrategyEngine().Evaluate(scene).
                Directive;
            // The native charge owns the primary horizontal closure, but a
            // verified body-contact guard may add one signed escape step.  A
            // player to the right of the Queen may therefore safely keep
            // running right; it must never be forced back through her body.
            var safeChargeSide = scene.Player.Center.X < bee.Center.X ? -1 : 1;
            True(directive.HorizontalIntent == 0 ||
                directive.HorizontalIntent == safeChargeSide,
                "charge horizontal closure entered the Queen: h=" +
                directive.HorizontalIntent + " safe=" + safeChargeSide +
                " phase=" + directive.PhaseId);
            Equal(expectedJump ? 1 : 0, directive.VerticalIntent);
            Equal(expectedJump ? JumpAction.Hold : JumpAction.Release,
                directive.JumpAction);
            True(directive.OwnsMovementClosure);

            var plan = new CombatPlanner(new PlannerSettings()).Plan(scene);
            True(plan.Horizontal == 0 || plan.Horizontal == safeChargeSide,
                "planned charge closure entered the Queen: h=" +
                plan.Horizontal + " safe=" + safeChargeSide +
                " phase=" + plan.PhaseId);
            Equal(expectedJump, plan.Jump);
            False(plan.Drop);

            scene.Player.OnGround = false;
            scene.Player.Position.Y -= 20f;
            scene.Player.Velocity.Y = -3.5f;
            scene.Player.Jump.RemainingTicks = 8;
            scene.Player.Jump.ReleaseReady = false;
            directive = new BossStrategyEngine().Evaluate(scene).Directive;
            Equal(expectedJump ? JumpAction.Hold : JumpAction.Release,
                directive.JumpAction);
            True(directive.OwnsMovementClosure);
            plan = new CombatPlanner(new PlannerSettings()).Plan(scene);
            Equal(expectedJump, plan.Jump);
            False(plan.Drop);
        }

        private static void QueenBeeChargeUsesBoundedCollisionWindow()
        {
            var scene = CombatScenario(222);
            scene.Player.Position = new Vec2(1500f, 800f);
            scene.Player.Velocity = new Vec2(0f, 0f);
            scene.Player.OnGround = true;
            scene.Player.Jump = new JumpSnapshot
            {
                Known = true,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = true
            };
            var bee = scene.Targets[0];
            bee.Width = 66;
            bee.Height = 66;
            bee.Ai0 = 0f;
            bee.Ai1 = 1f;
            bee.Ai2 = 0f;
            bee.Position = new Vec2(900f, 800f);
            bee.Velocity = new Vec2(16f, 0f);
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);

            var far = new BossStrategyEngine().Evaluate(scene).Directive;
            Equal(JumpAction.Release, far.JumpAction);
            Equal(0, far.VerticalIntent);

            bee.Position.X = 1100f;
            scene.Targets[0] = bee;
            var window = new BossStrategyEngine().Evaluate(scene).Directive;
            Equal(JumpAction.Hold, window.JumpAction);
            Equal(1, window.VerticalIntent);

            scene.Player.OnGround = false;
            scene.Player.Jump.RemainingTicks = 5;
            scene.Targets[0] = bee;
            Equal(JumpAction.Hold, new BossStrategyEngine().Evaluate(scene).
                Directive.JumpAction);

            scene.Player.Jump.RemainingTicks = 0;
            scene.Player.Jump.CloudAvailable = false;
            Equal(JumpAction.Release, new BossStrategyEngine().Evaluate(scene).
                Directive.JumpAction);

            bee.Position.X = 1510f;
            scene.Targets[0] = bee;
            Equal(JumpAction.Release, new BossStrategyEngine().Evaluate(scene).
                Directive.JumpAction);
        }

        private static void QueenBeeStingerRetainsEntryEnvelopeAcrossLifeBand()
        {
            var scene = CombatScenario(222);
            scene.Difficulty.Expert = true;
            var bee = scene.Targets[0];
            bee.LifeMax = 4760;
            bee.Life = 2400;
            bee.Ai0 = 3f;
            bee.Ai1 = 680f;
            bee.Ai2 = 0f;
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();
            var first = engine.Evaluate(scene).Directive;
            False(first.RequestControlReturn, first.ControlReturnReason);
            True(first.PhaseId.Contains("stinger"), first.PhaseId);

            // The native state remains state 3 while the hit which crosses
            // the exact half-life threshold is observed.  Its original
            // 35*20 envelope is still valid; selecting the new 30-tick band
            // here would incorrectly return control on the next frame.
            bee.Life = 2378;
            bee.Ai1 = 682f;
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);
            var crossing = engine.Evaluate(scene).Directive;
            False(crossing.RequestControlReturn,
                "stinger state was rejected at a life-band crossing");
            True(crossing.PhaseId.Contains("stinger"), crossing.PhaseId);

            // Once AI_043 leaves the volley state, the retained envelope must
            // not authorize a synthetic continuation of a later branch.
            bee.Ai0 = -1f;
            bee.Ai1 = 3f;
            scene.Targets[0] = bee;
            RefreshPriorityNativeContext(scene);
            var next = engine.Evaluate(scene).Directive;
            False(next.RequestControlReturn, next.ControlReturnReason);
            True(next.PhaseId.Contains("choose-next-native-attack"),
                next.PhaseId);
        }

        private static void QueenBeeSelectsNearbyHivePressure()
        {
            foreach (var type in new[] { 42, 43, 210, 211, 231, 232, 233,
                         234, 235 })
            {
                var scene = CombatScenario(222);
                scene.Targets.Add(new TargetSnapshot
                {
                    Key = 80 + type,
                    Type = type,
                    Position = scene.Player.Position + new Vec2(100f, 0f),
                    Width = 30,
                    Height = 30,
                    Life = 80,
                    LifeMax = 80,
                    Damage = 40,
                    Chaseable = true,
                    LineOfSightKnown = true,
                    HasLineOfSight = true
                });
                var decision = new BossStrategyEngine().Evaluate(scene);
                Equal(type, decision.Target.Type);
                Equal(222, decision.PatternTarget.Type);
                True(decision.Directive.Fire);
            }

            var ambient = CombatScenario(222);
            ambient.Targets.Add(new TargetSnapshot
            {
                Key = 204,
                Type = 204,
                Position = ambient.Player.Position + new Vec2(120f, 0f),
                Width = 32,
                Height = 28,
                Life = 110,
                LifeMax = 110,
                Damage = 40,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true
            });
            var ambientDecision = new BossStrategyEngine().Evaluate(ambient);
            Equal(204, ambientDecision.Target.Type);
            Equal(222, ambientDecision.PatternTarget.Type);
        }

        private static void QueenBeeRetainsCurrentHivePressure()
        {
            var scene = CombatScenario(222);
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 80,
                Type = 204,
                Position = scene.Player.Position + new Vec2(180f, 0f),
                Width = 32,
                Height = 28,
                Life = 110,
                LifeMax = 110,
                Damage = 40,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true
            });
            var engine = new BossStrategyEngine();
            Equal(80, engine.Evaluate(scene).Target.Key);

            scene.Targets.Add(new TargetSnapshot
            {
                Key = 81,
                Type = 42,
                Position = scene.Player.Position + new Vec2(80f, 0f),
                Width = 28,
                Height = 27,
                Life = 120,
                LifeMax = 120,
                Damage = 40,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true
            });
            Equal(80, engine.Evaluate(scene).Target.Key);

            var retained = scene.Targets[1];
            retained.Life = 0;
            scene.Targets[1] = retained;
            Equal(81, engine.Evaluate(scene).Target.Key);
        }

        private static void FishronStateSevenAndTwelveAreNeverCharges()
        {
            var scene = CombatScenario(370);
            var fishron = scene.Targets[0];
            fishron.Ai0 = 7f;
            fishron.Ai2 = 45f;
            // AI_069 enters the circular bubble branch only from sequence 6,
            // rewriting ai[3] to the native branch marker 1.
            fishron.Ai3 = 1f;
            fishron.Velocity = new Vec2(20f, 0f);
            scene.Targets[0] = fishron;
            var bubbles = new BossStrategyEngine().Evaluate(scene).Directive;
            True(bubbles.PhaseId.Contains("phase-2-circular-bubbles"));
            False(bubbles.PreferDash);
            Equal(BossPattern.CircleOrbit, bubbles.Pattern);

            scene.Difficulty.Expert = true;
            fishron.Ai0 = 12f;
            fishron.Ai2 = 8f;
            fishron.Ai3 = 4f;
            fishron.Velocity = new Vec2(25f, 0f); // legal residual speed
            scene.Targets[0] = fishron;
            var teleport = new BossStrategyEngine().Evaluate(scene).Directive;
            True(teleport.PhaseId.Contains("phase-3-teleport"));
            False(teleport.PreferDash);
        }

        private static void FishronPhaseComesFromNativeStateNotLifeGuess()
        {
            var scene = CombatScenario(370);
            scene.Difficulty.Expert = true;
            var fishron = scene.Targets[0];
            fishron.LifeMax = 10000;
            fishron.Life = 1000;
            fishron.Ai0 = 0f;
            fishron.Ai2 = 10f;
            fishron.Ai3 = 2f;
            scene.Targets[0] = fishron;
            False(new BossStrategyEngine().Evaluate(scene).Directive.PhaseId.
                Contains("phase-3"));

            fishron.Life = 9000;
            fishron.Ai0 = 10f;
            fishron.Ai2 = 4f;
            fishron.Ai3 = 5f;
            scene.Targets[0] = fishron;
            True(new BossStrategyEngine().Evaluate(scene).Directive.PhaseId.
                Contains("phase-3-reposition-before-dash"));
        }

        private static void ChargeControllersLeaveVerticalAndDiagonalAttackLines()
        {
            var scene = CombatScenario(370);
            var fishron = scene.Targets[0];
            fishron.Ai0 = 1f;
            fishron.Ai2 = 10f;
            fishron.Ai3 = 1f;
            fishron.Velocity = new Vec2(0f, 16f);
            scene.Targets[0] = fishron;
            var vertical = new BossStrategyEngine().Evaluate(scene).Directive;
            True(vertical.HorizontalIntent != 0,
                "vertical charge did not leave its attack line horizontally");
            Equal(0, vertical.VerticalIntent);

            fishron.Velocity = new Vec2(12f, 12f);
            scene.Targets[0] = fishron;
            var diagonal = new BossStrategyEngine().Evaluate(scene).Directive;
            True(diagonal.HorizontalIntent != 0 &&
                 diagonal.VerticalIntent != 0,
                "diagonal charge did not reserve both perpendicular axes");
            // Convert player-gravity Up/Down back to native world Y and prove
            // that the discrete intent is perpendicular to this 45-degree dash.
            var worldY = -diagonal.VerticalIntent;
            Equal(0, diagonal.HorizontalIntent + worldY);
        }

        private static void EmpressUsesNativeAttackTableAndRageLatch()
        {
            var scene = CombatScenario(636);
            var empress = scene.Targets[0];
            empress.Ai0 = 1f;
            empress.Ai1 = 4f;
            empress.Ai2 = 2f; // P1 table entry 2 => Sun Dance
            empress.Ai3 = 0f;
            scene.Difficulty.DayTime = true;
            scene.Targets[0] = empress;
            RefreshPriorityNativeContext(scene);
            var lethal = new BossStrategyEngine().Evaluate(scene).Directive;
            True(lethal.PhaseId.Contains(
                "day-lethal-p1-reposition-before-sun-dance"),
                lethal.PhaseId);

            // In Remix the native predicate is spatial. Being underground
            // keeps the encounter non-lethal even while Main.dayTime is true.
            scene.Difficulty.Remix = true;
            RefreshPriorityNativeContext(scene);
            var night = new BossStrategyEngine().Evaluate(scene).Directive;
            True(night.PhaseId.Contains(
                "night-p1-reposition-before-sun-dance"), night.PhaseId);

            empress.Ai3 = 2f;
            scene.Difficulty.DayTime = false;
            scene.Difficulty.Remix = false;
            scene.Targets[0] = empress;
            RefreshPriorityNativeContext(scene);
            var rage = new BossStrategyEngine().Evaluate(scene).Directive;
            True(rage.PhaseId.Contains("day-rage-p1"));

            empress.Ai0 = 8f;
            empress.Ai1 = 8f;
            empress.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = empress;
            RefreshPriorityNativeContext(scene);
            var dash = new BossStrategyEngine().Evaluate(scene).Directive;
            True(dash.PhaseId.Contains("horizontal-dash-telegraph"));
            Equal(BossPattern.PerpendicularDashDodge, dash.Pattern);
        }

        private static void EmpressDayPreDashReservesPerpendicularClearance()
        {
            // State 1 with the next attack selected as a horizontal 8/9 body
            // charge. In the daytime contract the Empress contact is lethal,
            // so the inter-attack reposition must step off the horizontal
            // charge lane rather than run along it into the native state 8/9
            // transition. The night contract keeps its historical horizontal
            // reposition behaviour.
            var day = CombatScenario(636);
            day.Difficulty.DayTime = true;
            var target = day.Targets[0];
            target.Ai0 = 1f;
            target.Ai1 = 4f;
            target.Ai2 = 1f; // PhaseOneAttacks[1] == 8, horizontal dash.
            target.Ai3 = 2f; // Phase one plus genuinely enraged.
            day.Targets[0] = target;
            RefreshPriorityNativeContext(day);

            var dayDecision = new BossStrategyEngine().Evaluate(day).Directive;
            True(dayDecision.PhaseId.Contains(
                "day-rage-p1-reposition-before-horizontal-dash"),
                dayDecision.PhaseId);
            Equal(0, dayDecision.HorizontalIntent);
            False(dayDecision.VerticalIntent == 0,
                "daytime pre-dash did not choose a perpendicular escape");
            True(dayDecision.ForceContinuousMovement,
                "daytime pre-dash did not reserve continuous clearance");
            True(dayDecision.OwnsMovementClosure,
                "daytime pre-dash let the generic scorer re-enter the charge lane");

            var withStreak = CombatScenario(636);
            withStreak.Difficulty.DayTime = true;
            var streakTarget = withStreak.Targets[0];
            streakTarget.Ai0 = 1f;
            streakTarget.Ai1 = 4f;
            streakTarget.Ai2 = 1f;
            streakTarget.Ai3 = 2f;
            withStreak.Targets[0] = streakTarget;
            withStreak.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.EmpressRainbowStreak,
                Type = 873,
                NativeIdentity = 17,
                TrajectoryAi0Known = true,
                TrajectoryAi0 = 0f,
                NativeTargetPlayerKnown = true,
                NativeTargetPlayerIndex = 0,
                Position = new Vec2(34000f, 7600f),
                Velocity = new Vec2(0f, -5f),
                Width = 30,
                Height = 30,
                TimeLeft = 190,
                Damage = 120
            });
            RefreshPriorityNativeContext(withStreak);
            var withStreakDecision =
                new BossStrategyEngine().Evaluate(withStreak).Directive;
            False(withStreakDecision.OwnsMovementClosure,
                "daytime pre-dash locked the vertical lane despite a homing rainbow streak");

            var night = CombatScenario(636);
            night.Difficulty.DayTime = false;
            var nightTarget = night.Targets[0];
            nightTarget.Ai0 = 1f;
            nightTarget.Ai1 = 4f;
            nightTarget.Ai2 = 1f;
            nightTarget.Ai3 = 0f;
            night.Targets[0] = nightTarget;
            RefreshPriorityNativeContext(night);
            var nightDecision = new BossStrategyEngine().Evaluate(night).Directive;
            True(dayDecision.ExtraContactMargin >
                nightDecision.ExtraContactMargin + 120f,
                "daytime pre-dash did not widen its lethal contact margin");
            True(nightDecision.OwnsMovementClosure,
                "nighttime pre-dash did not reserve its perpendicular charge lane");
        }

        private static void MoonLordBalancesEyesAndReadsPreSpawnClocks()
        {
            var scene = CombatScenario(398);
            var core = scene.Targets[0];
            core.Invulnerable = true;
            scene.Targets[0] = core;
            scene.Targets.Add(MoonPart(31, 396, .2f, 1f, 900f, 0f,
                core.Key));
            scene.Targets.Add(MoonPart(32, 397, .8f, 1f, 100f, 0f,
                core.Key));
            scene.Targets.Add(MoonPart(33, 397, .5f, 2f, 400f, 1f,
                core.Key));

            var decision = new BossStrategyEngine().Evaluate(scene);
            Equal(32, decision.Target.Key);
            True(decision.Directive.PhaseId.Contains(
                "sphere-release-telegraph-right-hand-0"),
                decision.Directive.PhaseId);

            var head = scene.Targets[1];
            head.Ai0 = 2f;
            head.Ai1 = 300f;
            scene.Targets[1] = head;
            var left = scene.Targets[2];
            left.Ai0 = 2f;
            left.Ai1 = 400f;
            left.Ai2 = 0f;
            scene.Targets[2] = left;
            decision = new BossStrategyEngine().Evaluate(scene);
            True(decision.Directive.PhaseId.Contains(
                "sphere-release-telegraph-left-hand-0"),
                decision.Directive.PhaseId);
        }

        private static TargetSnapshot MoonPart(int key, int type,
            float lifeFraction, float ai0, float ai1, float ai2,
            int parentCoreKey)
        {
            return new TargetSnapshot
            {
                Key = key,
                Type = type,
                Position = new Vec2(1700f + key, 600f),
                Width = 80,
                Height = 80,
                LifeMax = 10000,
                Life = (int)(10000 * lifeFraction),
                Boss = true,
                Chaseable = true,
                Ai0Known = true,
                Ai1Known = true,
                Ai2Known = true,
                Ai3Known = true,
                Ai0 = ai0,
                Ai1 = ai1,
                Ai2 = ai2,
                Ai3 = parentCoreKey,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0
            };
        }

        private static void WallDistinguishesChargeClockFromActiveLaserBurst()
        {
            var scene = CombatScenario(113);
            scene.NativeContextKnown = true;
            var mouth = scene.Targets[0];
            mouth.LifeMax = 10000;
            mouth.Life = 8000;
            scene.Targets[0] = mouth;
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 44,
                Type = 114,
                Position = new Vec2(1700f, 650f),
                Width = 60,
                Height = 60,
                Life = 8000,
                LifeMax = 10000,
                Boss = true,
                Chaseable = true,
                Ai0 = 1f,
                Ai0Known = true,
                LocalAi1 = 590f,
                LocalAi2 = 0f,
                LocalAi1Known = true,
                LocalAi2Known = true,
                HasLineOfSight = true,
                LineOfSightKnown = true
            });
            RefreshPriorityNativeContext(scene);
            var decision = new BossStrategyEngine().Evaluate(scene);
            True(decision.Directive.PhaseId.Contains("eye-laser-charging-57"),
                decision.Directive.PhaseId);
            Equal(44, decision.Target.Key);

            scene.PriorityBoss.WallOfFleshTunnels[0] =
                new WallOfFleshTunnelObservation
                {
                    Known = true,
                    NpcKey = mouth.Key,
                    NativeDirectionKnown = true,
                    NativeDirection = 1,
                    DrawAreaTopPixels = 400,
                    DrawAreaBottomPixels = 1600
                };
            // The second half deliberately flips the native Wall direction.
            // Keep the local player in front of that translating direction so
            // this remains a valid strategy-state assertion rather than the
            // separate fail-closed "Wall already passed player" admission.
            scene.Player.Position = new Vec2(mouth.Center.X + 360f -
                scene.Player.Width * .5f, scene.Player.Position.Y);
            scene.Player.Velocity = new Vec2(2f, 0f);
            decision = new BossStrategyEngine().Evaluate(scene);
            Equal(1, decision.Directive.HorizontalIntent);
            True(decision.Directive.VerticalOffset > 0f,
                "Wall strategy did not target the observed tunnel center");

            var eye = scene.Targets[1];
            eye.LocalAi1 = 35f;
            eye.LocalAi2 = 1f;
            scene.Targets[1] = eye;
            RefreshPriorityNativeContext(scene);
            var updatedTunnel = scene.PriorityBoss.WallOfFleshTunnels[0];
            updatedTunnel.NativeDirection = 1;
            scene.PriorityBoss.WallOfFleshTunnels[0] = updatedTunnel;
            decision = new BossStrategyEngine().Evaluate(scene);
            True(decision.Directive.PhaseId.Contains("eye-laser-imminent-11"),
                decision.Directive.PhaseId);
        }

        private static void SkeletronUsesHeadAndHandNativeClocks()
        {
            var scene = CombatScenario(35);
            var head = scene.Targets[0];
            head.Ai1 = 0f;
            head.Ai2 = 790f;
            head.Ai3 = 0f;
            scene.Targets[0] = head;
            var preSpin = new BossStrategyEngine().Evaluate(scene).Directive;
            True(preSpin.PhaseId.Contains("hover-spin-imminent"));

            head.Ai1 = 1f;
            // AI_035 resets the shared phase clock on entry to the spin
            // branch; retaining the hover value 790 would be unreachable.
            head.Ai2 = 200f;
            head.Velocity = new Vec2(0f, 0f);
            scene.Targets[0] = head;
            var spin = new BossStrategyEngine().Evaluate(scene).Directive;
            True(spin.PhaseId.Contains("head-spin-pursuit"), spin.PhaseId);

            head.Ai1 = 0f;
            head.Ai2 = 100f;
            scene.Targets[0] = head;
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 55,
                Type = 36,
                Position = new Vec2(1700f, 800f),
                Width = 40,
                Height = 40,
                Life = 900,
                LifeMax = 1000,
                Chaseable = true,
                Ai0Known = true,
                Ai0 = -1f,
                Ai1Known = true,
                Ai1 = head.Key,
                Ai2Known = true,
                Ai2 = 4f,
                Ai3Known = true,
                Ai3 = 0f
            });
            var hand = new BossStrategyEngine().Evaluate(scene);
            Equal(55, hand.Target.Key);
            True(hand.Directive.PhaseId.Contains(
                "hand-horizontal-dive-locking"));
        }

        private static void DeerclopsTelegraphsBeforeProjectilesExist()
        {
            var scene = CombatScenario(668);
            var deer = scene.Targets[0];
            deer.NativeDirectionKnown = true;
            deer.NativeDirection = 1;
            deer.Ai0 = -1f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var spawn = new BossStrategyEngine().Evaluate(scene).Directive;
            True(spawn.PhaseId.Contains("native-spawn-settle"));
            False(spawn.RequestControlReturn);

            deer.Ai0 = 1f;
            deer.Ai1 = 20f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var spikes = new BossStrategyEngine().Evaluate(scene).Directive;
            True(spikes.PhaseId.Contains("forward-spikes-telegraph"));
            Equal(1, spikes.HorizontalIntent);

            deer.Ai0 = 2f;
            deer.Ai1 = 30f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var rubble = new BossStrategyEngine().Evaluate(scene).Directive;
            True(rubble.PhaseId.Contains("rubble-telegraph"));

            deer.Ai0 = 5f;
            deer.Ai1 = 29f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var hands = new BossStrategyEngine().Evaluate(scene).Directive;
            True(hands.PhaseId.Contains("shadow"));
        }

        private static void DeerclopsKeepsJumpingWhileObservedSpikesSurvive()
        {
            var scene = CombatScenario(668);
            var deer = scene.Targets[0];
            deer.Ai0 = 0f;
            deer.Ai1 = 0f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            scene.Player.OnGround = true;
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 961,
                Position = scene.Player.Position + new Vec2(20f, 0f),
                Velocity = new Vec2(-4f, 0f),
                Width = 48,
                Height = 48,
                Damage = 40,
                TimeLeft = 90
            });
            var directive = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(directive.PhaseId.Contains("spikes-observed"));
            Equal(1, directive.VerticalIntent);
            Equal(JumpAction.Hold, directive.JumpAction);
            True(directive.OwnsMovementClosure);

            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 965,
                Position = scene.Player.Position + new Vec2(-120f, -40f),
                Velocity = new Vec2(5f, 0f),
                Width = 36,
                Height = 36,
                Damage = 40,
                TimeLeft = 90
            });
            var concurrent = new BossStrategyEngine().Evaluate(scene).
                Directive;
            True(concurrent.PhaseId.Contains("shadow-hand-and-forward-spikes"));
            Equal(1, concurrent.VerticalIntent);
            Equal(JumpAction.Hold, concurrent.JumpAction);
            True(concurrent.OwnsMovementClosure);
        }

        private static void DeerclopsPreservesCloudForLateSpikeWave()
        {
            var scene = CombatScenario(668);
            var deer = scene.Targets[0];
            deer.Position.X = scene.Player.Center.X + 200f -
                deer.Width * .5f;
            deer.Ai0 = 1f;
            deer.Ai1 = 37f;
            deer.NativeDirection = -1;
            scene.Targets[0] = deer;
            scene.Player.OnGround = false;
            scene.Player.Jump = new JumpSnapshot
            {
                Known = true,
                RemainingTicks = 0,
                CloudAvailable = true,
                ReleaseReady = true
            };
            RefreshPriorityNativeContext(scene);

            var early = new BossStrategyEngine().Evaluate(scene).Directive;
            Equal(0, early.HorizontalIntent);
            Equal(JumpAction.Release, early.JumpAction);

            deer.Ai1 = 48f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var late = new BossStrategyEngine().Evaluate(scene).Directive;
            Equal(JumpAction.Cloud, late.JumpAction);
            Equal(1, late.VerticalIntent);
        }

        private static void DeerclopsEscapesContactAndImminentShadowHand()
        {
            var scene = CombatScenario(668);
            var deer = scene.Targets[0];
            deer.Ai0 = 0f;
            deer.Ai1 = 0f;
            deer.Position.X = scene.Player.Center.X + 170f -
                deer.Width * .5f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var engine = new BossStrategyEngine();

            var close = engine.Evaluate(scene).Directive;
            Equal(-1, close.HorizontalIntent);
            True(close.OwnsHorizontalClosure);

            deer.Position.X = scene.Player.Center.X + 220f -
                deer.Width * .5f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var retained = engine.Evaluate(scene).Directive;
            Equal(-1, retained.HorizontalIntent);
            True(retained.OwnsHorizontalClosure);

            deer.Position.X = scene.Player.Center.X + 300f -
                deer.Width * .5f;
            scene.Targets[0] = deer;
            RefreshPriorityNativeContext(scene);
            var released = engine.Evaluate(scene).Directive;
            False(released.OwnsHorizontalClosure);

            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 965,
                Position = scene.Player.Position + new Vec2(100f, 0f),
                Velocity = new Vec2(-5f, 0f),
                Width = 40,
                Height = 40,
                Damage = 40,
                TimeLeft = 90
            });
            scene.Threats.Add(new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 961,
                Position = scene.Player.Position + new Vec2(10f, 0f),
                Velocity = new Vec2(0f, -1f),
                Width = 32,
                Height = 32,
                Damage = 40,
                TimeLeft = 15
            });
            scene.Player.OnGround = true;
            var concurrent = engine.Evaluate(scene).Directive;
            Equal(-1, concurrent.HorizontalIntent);
            Equal(JumpAction.Hold, concurrent.JumpAction);
            True(concurrent.OwnsHorizontalClosure);
            True(concurrent.OwnsMovementClosure);
        }

        private static void DeerclopsReturnHomeAllowsAmbientThreatEscape()
        {
            foreach (var state in new[] { 0f, 6f, 7f })
            {
                var scene = CombatScenario(668);
                scene.Difficulty.Expert = true;
                var deer = scene.Targets[0];
                deer.Position = scene.Player.Position + new Vec2(300f, 0f);
                deer.Ai0 = state;
                deer.Ai1 = state == 0f ? 0f : state == 7f ? 45f : 10f;
                scene.Targets[0] = deer;
                RefreshPriorityNativeContext(scene);
                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                False(directive.OwnsHorizontalClosure);
                False(directive.OwnsMovementClosure);

                deer.Position = scene.Player.Position + new Vec2(500f, 0f);
                scene.Targets[0] = deer;
                RefreshPriorityNativeContext(scene);
                var far = new BossStrategyEngine().Evaluate(scene).Directive;
                Equal(1, far.HorizontalIntent);
                True(far.OwnsHorizontalClosure,
                    "far state " + state + " did not own horizontal recovery");
                False(far.OwnsMovementClosure);

                if (state == 0f)
                {
                    deer.LocalAi2 = 60f;
                    scene.Targets[0] = deer;
                    RefreshPriorityNativeContext(scene);
                    var imminent = new BossStrategyEngine().Evaluate(scene).
                        Directive;
                    True(imminent.PhaseId.Contains("shadow-hand-imminent"),
                        "far passive hand was not identified: " +
                        imminent.PhaseId);
                    Equal(1, imminent.HorizontalIntent);
                    True(imminent.OwnsHorizontalClosure,
                        "far passive hand lost horizontal recovery ownership");
                }
            }

            var edge = CombatScenario(668);
            edge.Player.Position = new Vec2(900f, edge.Player.Position.Y);
            edge.Player.Velocity = new Vec2(6f, 0f);
            edge.Arena.ClearanceLeft = 900f;
            edge.Arena.ClearanceRight = 3000f;
            edge.Arena.FloorSupport = new SupportSpan
            {
                Valid = true,
                Left = 0f,
                Right = edge.Player.Position.X + edge.Player.Width + 30f,
                SurfaceY = edge.Player.Position.Y + edge.Player.Height
            };
            var edgeDeer = edge.Targets[0];
            edgeDeer.Position = edge.Player.Position + new Vec2(-250f, 0f);
            edgeDeer.Ai0 = 3f;
            edgeDeer.Ai1 = 30f;
            edge.Targets[0] = edgeDeer;
            RefreshPriorityNativeContext(edge);
            var turned = new BossStrategyEngine().Evaluate(edge).Directive;
            Equal(-1, turned.HorizontalIntent);
            True(turned.OwnsHorizontalClosure,
                "floor-edge slow phase lost horizontal ownership");
        }

        private static void DeerclopsSelectsNearbyAmbientPressure()
        {
            var scene = CombatScenario(668);
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 431,
                Type = 431,
                Position = scene.Player.Position + new Vec2(100f, 0f),
                Width = 20,
                Height = 40,
                Life = 120,
                LifeMax = 120,
                Damage = 59,
                Chaseable = true,
                LineOfSightKnown = true,
                HasLineOfSight = true
            });
            var decision = new BossStrategyEngine().Evaluate(scene);
            Equal(431, decision.Target.Type);
            Equal(668, decision.PatternTarget.Type);
            True(decision.Directive.Fire);
        }

        private static void PriorityStrategiesRequireMatchingProductionContext()
        {
            var queen = CombatScenario(222);
            queen.PriorityBoss.QueenBees.Clear();
            True(new BossStrategyEngine().Evaluate(queen).Directive.
                RequestControlReturn,
                "production Queen Bee accepted a missing enrage observation");

            var fishron = CombatScenario(370);
            fishron.PriorityBoss.DukeFishrons[0] =
                new DukeFishronNativeEnrageObservation
                {
                    Known = true,
                    NpcKey = fishron.Targets[0].Key,
                    PlayerAboveY800Band = true,
                    NativeEnraged = false
                };
            True(new BossStrategyEngine().Evaluate(fishron).Directive.
                RequestControlReturn,
                "production Fishron accepted a contradictory enrage result");

            var empress = CombatScenario(636);
            var target = empress.Targets[0];
            target.Ai0 = 8f;
            empress.Targets[0] = target;
            // The duplicate source must match the NPC array from the same tick.
            True(new BossStrategyEngine().Evaluate(empress).Directive.
                RequestControlReturn,
                "production Empress accepted stale independent AI metadata");

            var wall = CombatScenario(113, 114);
            wall.PriorityBoss.WallOfFleshEyes.Clear();
            True(new BossStrategyEngine().Evaluate(wall).Directive.
                RequestControlReturn,
                "production Wall accepted a missing per-eye laser clock");
        }

        private static void NativeEnrageChangesQueenBeeAndFishronSafetyEnvelope()
        {
            var queen = CombatScenario(222);
            var bee = queen.Targets[0];
            bee.Ai0 = 3f;
            bee.Ai1 = 10f;
            queen.Targets[0] = bee;
            var calm = new BossStrategyEngine().Evaluate(queen).Directive;
            queen.PriorityBoss.QueenBees[0] =
                new QueenBeeNativeEnrageObservation
                {
                    Known = true,
                    NpcKey = bee.Key,
                    BossAboveWorldSurface = true,
                    TargetOutsideJungle = true,
                    NativeEnrageFactor = 2f
                };
            var enragedBee = new BossStrategyEngine().Evaluate(queen).
                Directive;
            True(enragedBee.ExtraContactMargin > calm.ExtraContactMargin);
            True(enragedBee.PhaseId.Contains("stinger"));

            var fishron = CombatScenario(370);
            var calmFishron = new BossStrategyEngine().Evaluate(fishron).
                Directive;
            fishron.PriorityBoss.DukeFishrons[0] =
                new DukeFishronNativeEnrageObservation
                {
                    Known = true,
                    NpcKey = fishron.Targets[0].Key,
                    PlayerInsideCentralHorizontalBand = true,
                    NativeEnraged = true
                };
            var enragedFishron = new BossStrategyEngine().Evaluate(fishron).
                Directive;
            True(enragedFishron.PhaseId.Contains("native-enraged"));
            True(enragedFishron.IdealDistance > calmFishron.IdealDistance);
            True(enragedFishron.ExtraContactMargin >
                calmFishron.ExtraContactMargin);
        }

        private static void MoonLordUsesValidatedSphereAndTongueMetadata()
        {
            var scene = CombatScenario(398);
            scene.PriorityBoss.MoonLordProjectiles454.Add(
                new MoonLordProjectile454Observation
                {
                    Known = true,
                    ProjectileKey = 8,
                    Ai0AgeOrMode = 59f,
                    SourceNpcAi1 = 4f,
                    LocalAi0 = 0f,
                    LocalAi1 = 0f,
                    TimeLeft = 120,
                    Alpha = 0,
                    ExtraUpdates = 0,
                    SourceNpcIdentityKnown = true,
                    SourceNpcActive = true,
                    SourceNpcType = 397
                });
            var sphere = new BossStrategyEngine().Evaluate(scene).Directive;
            True(sphere.PhaseId.Contains("sphere-release-telegraph-native-sphere-8-1"),
                sphere.PhaseId);

            scene.PriorityBoss.MoonLordProjectiles454.Clear();
            scene.PriorityBoss.MoonLordProjectiles456.Add(
                new MoonLordProjectile456Observation
                {
                    Known = true,
                    ProjectileKey = 9,
                    EncodedSourceAi0 = 4f,
                    TargetPlayerAi1 = 0f,
                    AgeTicks = 20f,
                    ContactLatchAi = 0f,
                    TimeLeft = 200,
                    SourceNpcIdentityKnown = true,
                    SourceNpcActive = true,
                    SourceNpcType = 396
                });
            var tongue = new BossStrategyEngine().Evaluate(scene).Directive;
            True(tongue.PhaseId.Contains("moon-bite-tongue-window"));

            var malformed = scene.PriorityBoss.MoonLordProjectiles456[0];
            malformed.EncodedSourceAi0 = 0f;
            scene.PriorityBoss.MoonLordProjectiles456[0] = malformed;
            True(new BossStrategyEngine().Evaluate(scene).Directive.
                RequestControlReturn,
                "malformed Moon Leech metadata did not fail closed");
        }
    }
}
