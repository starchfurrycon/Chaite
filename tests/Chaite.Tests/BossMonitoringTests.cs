using System;
using System.Linq;
using Chaite.Core;
using Mono.Cecil;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>The threats a wing circuit cannot answer by moving. The
        /// split that matters is the removal cost, not the homing, so the
        /// one-life bubble and the two armoured ones must stay distinct.</summary>
        private static void FishronThreatIdentitiesKeepTheWeaponBarWide()
        {
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.DetonatingBubbleType));
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.LargeSharknadoBubbleType));
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SmallSharknadoBubbleType));
            False(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SharknadoType));
            False(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SharknadoBoltType));
            // Only the one-life bubble has to be shot down. The two the
            // Sharknado emits are answered by where the circuit lets the
            // tornado land, which is what keeps the weapon bar wide.
            True(FishronThreatCatalog.RequiresWeaponClearance(
                FishronThreatCatalog.DetonatingBubbleType));
            False(FishronThreatCatalog.RequiresWeaponClearance(
                FishronThreatCatalog.LargeSharknadoBubbleType));
            False(FishronThreatCatalog.RequiresWeaponClearance(
                FishronThreatCatalog.SmallSharknadoBubbleType));
            True(FishronThreatCatalog.IsSharknadoSpawnedBubble(
                FishronThreatCatalog.LargeSharknadoBubbleType));
            True(FishronThreatCatalog.IsSharknadoSpawnedBubble(
                FishronThreatCatalog.SmallSharknadoBubbleType));
            False(FishronThreatCatalog.IsSharknadoSpawnedBubble(
                FishronThreatCatalog.DetonatingBubbleType));
            True(FishronThreatCatalog.IsReviewedBubbleClearer(
                FishronThreatCatalog.GoldenShowerItem));
            True(FishronThreatCatalog.IsReviewedBubbleClearer(
                FishronThreatCatalog.RazorbladeTyphoonItem));
            False(FishronThreatCatalog.IsReviewedBubbleClearer(757));
            Equal(116, FishronThreatCatalog.InfernoBuff);
            Equal(2348, FishronThreatCatalog.InfernoPotionItem);
            // Admission is a stock count, and it fails closed on a short or
            // unreadable inventory.
            True(FishronThreatCatalog.HasSufficientInfernoStock(3));
            False(FishronThreatCatalog.HasSufficientInfernoStock(2));
            False(FishronThreatCatalog.HasSufficientInfernoStock(0));
            // The refresh is fail-closed on unknown buff state and fires with
            // margin before the ring actually lapses.
            True(FishronThreatCatalog.NeedsInfernoRefresh(false, 99999));
            True(FishronThreatCatalog.NeedsInfernoRefresh(true, 900));
            True(FishronThreatCatalog.NeedsInfernoRefresh(true, 10));
            False(FishronThreatCatalog.NeedsInfernoRefresh(true, 3600));
            Equal(1, FishronThreatCatalog.DetonatingBubbleLife);
            Equal(100, FishronThreatCatalog.SharknadoBubbleLife);
            Equal(100, FishronThreatCatalog.SharknadoBubbleDefense);
            Equal(540, FishronThreatCatalog.SharknadoLifetimeTicks);
        }

        /// <summary>The reviewed W cycle: horizontal, ascend, descend,
        /// repeating, with one Shield-of-Cthulhu edge spent per charge.</summary>
        private static void FishronWingFollowsReviewedChargeCycle()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 1 };
            var horizontal = new int[4];
            var vertical = new int[4];
            var dashed = new bool[4];
            for (var beat = 0; beat < 4; beat++)
            {
                input.NativeState = 0;
                input.NativeTimer = 1;
                controller.Tick(in input, player, in boss, arena, mobility);
                input.NativeState = 1;
                input.NativeTimer = 1;
                var escape = controller.Tick(in input, player, in boss, arena,
                    mobility);
                horizontal[beat] = escape.Horizontal;
                vertical[beat] = escape.Vertical;
                dashed[beat] = escape.Dash;
            }
            // Away from the Boss on the horizontal axis of every beat, because
            // the shield dash follows the facing direction.
            for (var beat = 0; beat < 4; beat++)
                Equal(-1, horizontal[beat]);
            Equal(0, vertical[0]);
            Equal(-1, vertical[1]);
            Equal(1, vertical[2]);
            Equal(0, vertical[3]);
            for (var beat = 0; beat < 4; beat++)
                True(dashed[beat], "beat " + beat + " must spend the dash edge");
        }

        /// <summary>A charge aimed from closer than the standoff cannot be
        /// cleared by any available speed, so the circuit flees whenever the
        /// hover puts the Boss inside it.</summary>
        private static void FishronWingKeepsStandoffGap()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130, OnGround = true };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2300, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 2 };
            var close = new FishronWingScript().Tick(in input, player, in boss,
                arena);
            Equal(-1, close.Horizontal);
            // Past the standoff the circuit patrols instead of fleeing.
            boss.Position = new Vec2(4400, 5000);
            var far = new FishronWingScript().Tick(in input, player, in boss,
                arena);
            True(far.Phase.StartsWith("fishron-wing-cruise"),
                "phase=" + far.Phase);
        }

        /// <summary>The body escape is one decision per episode. AI_069 crosses
        /// the player on its way through a charge, so re-deriving the side every
        /// frame makes the sign follow whatever sub-pixel difference happens to
        /// exist while the body is on top of the player.</summary>
        private static void FishronWingLatchesTheBodyEscapeSide()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130, OnGround = true };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2120, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 1 };
            var controller = new FishronWingScript();
            var first = controller.Tick(in input, player, in boss, arena);
            Equal("fishron-wing-personal-space", first.Phase);
            Equal(-1, first.Horizontal);
            // The Boss crosses to the other side without ever leaving the
            // personal-space radius.
            boss.Position = new Vec2(1890, 5000);
            var second = controller.Tick(in input, player, in boss, arena);
            Equal("fishron-wing-personal-space", second.Phase);
            Equal(-1, second.Horizontal);
            // Once the body is clear the next episode chooses its own side.
            boss.Position = new Vec2(2600, 5000);
            var clear = controller.Tick(in input, player, in boss, arena);
            Equal("fishron-wing-standoff", clear.Phase);
            boss.Position = new Vec2(1890, 5000);
            var fresh = controller.Tick(in input, player, in boss, arena);
            Equal("fishron-wing-personal-space", fresh.Phase);
            Equal(1, fresh.Horizontal);
        }

        /// <summary>A projectile attack restarts the charge group, so the cycle
        /// must begin again from its horizontal beat rather than continuing.</summary>
        private static void FishronWingRestartsCycleAfterProjectileAttack()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var mobility = new MobilitySnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 1,
                NativeTimer = 1 };
            Equal(0, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
            input.NativeState = 0;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            Equal(-1, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
            // A Bubble phase ends the group.
            input.NativeState = 2;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            Equal(0, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
        }

        private static void FishronWingScriptIgnoresThreatListContents()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40, WorldRight = 5000 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 900), Width = 150, Height = 100,
                Velocity = new Vec2(-17, 4) };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 2, NativeTimer = 4 };
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var emptyArena = new ArenaSnapshot();
            var populatedArena = new ArenaSnapshot
            {
                LocalOpenBounds = new RectF(100, 100, 40, 40),
                ClearanceLeft = 1,
                ClearanceRight = 9999
            };
            var first = new FishronWingScript().Tick(in input, player,
                in boss, emptyArena, mobility);
            var second = new FishronWingScript().Tick(in input, player,
                in boss, populatedArena, mobility);
            Equal(first.Horizontal, second.Horizontal);
            Equal(first.Vertical, second.Vertical);
            Equal(first.Dash, second.Dash);
            Equal("fishron-wing-bubble-line", first.Phase);
        }

        private static void FishronFormulaRejectsImpossibleNativeTuples()
        {
            var difficulty = new DifficultySnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 11, NativeTimer = 1, NativeSequence = 0 };
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            difficulty.Expert = true;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeSequence = 1;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeState = 1;
            input.NativeSequence = 0;
            input.NativeTimer = 29;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeTimer = 27;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeTimer = 27;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, true));
            input.NativeTimer = 28;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, true));
        }

        private static void FishronChilletUsesReviewedNativeDashCadence()
        {
            var controller = new FishronChilletScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 980), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 800, Right = 3600 } };
            var mobility = new MobilitySnapshot
            {
                SelectedMountIdentityKnown = true,
                SelectedMountType = 64,
                ActiveMountReleaseReady = true
            };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronTrustyChillet,
                NativeState = 0, NativeSequence = 0 };
            var mount = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(mount.Accepted); True(mount.ToggleMount); False(mount.Fire);

            mobility.MountActive = true;
            mobility.ActiveMountIdentityKnown = true;
            mobility.ActiveMountType = 64;
            mobility.DashType = 6;
            mobility.DashReady = true;
            var dashState = mobility.EyeShieldDash;
            dashState.ReleaseDash = true;
            mobility.EyeShieldDash = dashState;

            input.NativeState = 1; input.NativeSequence = 0;
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "the first P1 charge is deliberately passed");
            input.NativeState = 0; input.NativeSequence = 2;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            var second = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(second.Dash); Equal(1, second.Horizontal);
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "one native charge cannot emit two dash edges");

            input.NativeState = 5; input.NativeSequence = 4;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 6;
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "the third P2 charge keeps running");
            input.NativeState = 12; input.NativeSequence = 4;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 11; input.NativeSequence = 5;
            True(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "every P3 charge is a reviewed counter edge");
        }

        private static void FishronChilletRejectsWrongMountAndShortRunway()
        {
            var player = new PlayerSnapshot { Position = new Vec2(1000, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(1400, 1000), Width = 150, Height = 100 };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronTrustyChillet };
            var shortArena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 500, Right = 1500 } };
            False(new FishronChilletScript().Tick(in input, player, in boss,
                shortArena, new MobilitySnapshot()).Accepted);

            var arena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 0, Right = 3000 } };
            var wrong = new MobilitySnapshot { InfernoPotionStockKnown = true,
                InfernoPotionStock = 3, MountActive = true,
                ActiveMountIdentityKnown = true, ActiveMountType = 63 };
            False(new FishronChilletScript().Tick(in input, player, in boss,
                arena, wrong).Accepted);
        }

        /// <summary>The reviewed bubble clearance is part of Fishron admission,
        /// and it fails closed: a short or unreadable potion stock must refuse
        /// the fight rather than let a route start without a way to clear the
        /// homing bubbles.</summary>
        private static void FishronAdmissionRequiresInfernoStock()
        {
            var scene = CombatScenario(370);
            scene.Player.FunctionalEquipmentIdentityKnown = true;
            scene.Player.WingAccessoryItemType = 761;
            scene.Mobility.FormulaAccessoryScanKnown = true;
            scene.Mobility.FrogLegAccessoryKnown = true;
            scene.Mobility.FrogLegAccessoryPresent = true;
            var dash = scene.Mobility.EyeShieldDash;
            dash.EquipmentIdentity =
                DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            scene.Mobility.EyeShieldDash = dash;
            FormulaRoute route;
            string reason;
            True(FormulaMobilityContract.TrySelectRoute(scene, 370, out route,
                out reason), reason);

            scene.Mobility.InfernoPotionStock = 2;
            False(FormulaMobilityContract.TrySelectRoute(scene, 370, out route,
                out reason));
            True(reason.Contains("地狱药水"), reason);

            scene.Mobility.InfernoPotionStock = 0;
            False(FormulaMobilityContract.TrySelectRoute(scene, 370, out route,
                out reason));

            scene.Mobility.InfernoPotionStockKnown = false;
            scene.Mobility.InfernoPotionStock = 0;
            False(FormulaMobilityContract.TrySelectRoute(scene, 370, out route,
                out reason));

            // A locked route is re-validated at takeover under the same rule.
            scene.Mobility.InfernoPotionStockKnown = true;
            scene.Mobility.InfernoPotionStock = 3;
            True(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                FormulaRoute.FishronFairyWingsDash, out reason), reason);
            // The stock is an admission condition, not a standing one: the
            // circuit drinks from it, so a count that has fallen mid-fight must
            // not cancel the run it was provisioned for.
            scene.Mobility.InfernoPotionStock = 1;
            True(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                FormulaRoute.FishronFairyWingsDash, out reason), reason);
            // Readability is still re-asserted every tick.
            scene.Mobility.InfernoPotionStockKnown = false;
            False(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                FormulaRoute.FishronFairyWingsDash, out reason));
        }

        private static void FishronChilletLockedRouteAcceptsBothReskinMounts()
        {
            var scene = CombatScenario(370);
            scene.Player.FunctionalEquipmentIdentityKnown = true;
            scene.Mobility.FormulaAccessoryScanKnown = true;
            scene.Mobility.SelectedMountIdentityKnown = true;
            scene.Mobility.SelectedMountType = 64;
            FormulaRoute route;
            string reason;
            True(FormulaMobilityContract.TrySelectRoute(scene, 370,
                out route, out reason), reason);
            Equal(FormulaRoute.FishronTrustyChillet, route);

            scene.Mobility.MountActive = true;
            scene.Mobility.ActiveMountIdentityKnown = true;
            scene.Mobility.ActiveMountType = 64;
            True(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                route, out reason), reason);
            // Mount 65 is the reskin of mount 64 and is the same route, so it
            // must satisfy the locked-route check rather than fail it. Before
            // the merge this comparison was an equality against a single
            // expected id, which is exactly why merging Select alone would
            // have locked out every mount-65 player.
            scene.Mobility.ActiveMountType = 65;
            True(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                route, out reason), reason);
            // A genuinely different mount is still rejected, so the merge
            // widened membership to the reskin and nothing else.
            scene.Mobility.ActiveMountType = 50;
            False(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                route, out reason));
        }

        private static void FormulaAdmissionSeparatesMobilityAndOutputRefusal()
        {
            var scene = CombatScenario(370);
            scene.Player.FunctionalEquipmentIdentityKnown = true;
            scene.Mobility.FormulaAccessoryScanKnown = true;
            scene.Mobility.SelectedMountIdentityKnown = true;
            scene.Mobility.SelectedMountType = 64;
            scene.Weapon.NativeProfileRequired = true;
            scene.Weapon.WeaponId = -1;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            bool mobilityRefusal;
            False(planner.PrepareForMonitoredFormulaEncounter(scene, 370,
                FormulaRoute.FishronTrustyChillet, out reason,
                out mobilityRefusal));
            False(mobilityRefusal);
            True(reason.Contains("output route"), reason);

            // The Queen Slime saddle (mount 50) was withdrawn from the Fishron
            // admission set on 2026-09-21, so asking for the Lilith's Wolf route
            // while riding mount 50 is now a MOBILITY refusal: the route still
            // exists, the saddle simply does not satisfy it. The old test asked
            // for a route that no longer exists at all.
            False(planner.PrepareForMonitoredFormulaEncounter(scene, 370,
                FormulaRoute.FishronLilithWolf, out reason,
                out mobilityRefusal));
            True(mobilityRefusal);
        }

        /// <summary>A policy must not erase the label of the branch it adjusted.
        /// The probe marks a battle observation edge when the phase string
        /// changes, so pinning it to one constant for the whole run stops that
        /// reason from firing; and the observation channel is the only place
        /// that can show which branch pressed a key, so losing the label makes
        /// per-branch attribution impossible after the fact. Section 25 needed
        /// exactly that and found every state-6 row reading one constant.</summary>
        private static void PolicyKeepsTheBranchLabelItAdjusted()
        {
            var player = new PlayerSnapshot { Position = new Vec2(1800, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2200, 800), Width = 80, Height = 80 };
            var arena = new ArenaSnapshot { ClearanceLeft = 800,
                ClearanceRight = 1200 };
            var mobility = new MobilitySnapshot { CanDash = true,
                DashReady = true };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronStrongWingsDash,
                NativeState = 6, NativeTimer = 30, NativeSequence = 3,
                NativeFormKnown = true };

            var previousFile = Environment.GetEnvironmentVariable("CHAITE_POLICY_FILE");
            var previousRoutes = Environment.GetEnvironmentVariable("CHAITE_POLICY_ROUTES");
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-branch-label-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                LearnedPolicy.ResetCache();
                var plain = FormulaScriptController.Tick(in input, player,
                    in boss, arena, mobility).Phase;
                True(!string.IsNullOrEmpty(plain), "the script names its branch");
                False(plain.EndsWith(LearnedPolicy.LearnedSuffix),
                    "no suffix without a policy");

                // The declared format is the header token, the version, the
                // input count, the hidden width, then the weight groups. All
                // weights zero is the identity policy, so it changes no control
                // and the branch label must come out identical.
                WriteTestPolicy(path, -1, null);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", path);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES",
                    nameof(FormulaRoute.FishronStrongWingsDash));
                LearnedPolicy.ResetCache();

                var adjusted = FormulaScriptController.Tick(in input, player,
                    in boss, arena, mobility).Phase;
                True(adjusted.EndsWith(LearnedPolicy.LearnedSuffix),
                    "the marker records that a policy acted");
                Equal(plain + LearnedPolicy.LearnedSuffix, adjusted);
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", previousFile);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES", previousRoutes);
                LearnedPolicy.ResetCache();
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        /// <summary>A charge's single dash is spent when the dash is
        /// <em>issued</em>, not when the script proposes it.
        ///
        /// The latch used to be set inside the script, before the trained
        /// residual ran, which made a hold indistinguishable from a burn: the
        /// search could pick "dash on the first ready tick" or "not at all this
        /// charge" and nothing in between. The timing the remaining hits need
        /// was therefore outside the reachable set, so no amount of search could
        /// have found it. Measured on the corrected arena, the phase-two body
        /// contact happens because the dash fires while the Boss is still 270 px
        /// out and its i-frames are spent before the closest approach.
        ///
        /// This is a control-contract test, not a physics claim: it asserts only
        /// that the hold costs nothing, which is what makes the timing
        /// expressible at all.</summary>
        private static void HeldDashIsNotSpentUntilItIsIssued()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var mobility = new MobilitySnapshot { CanDash = true,
                DashReady = true };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 1 };

            var previousFile = Environment.GetEnvironmentVariable("CHAITE_POLICY_FILE");
            var previousRoutes = Environment.GetEnvironmentVariable("CHAITE_POLICY_ROUTES");
            var hold = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-hold-dash-" + Guid.NewGuid().ToString("N") + ".txt");
            var identity = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-identity-dash-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                // Logit 9 is the dash head's hold class, so a bias on it alone
                // makes the residual hold every proposed dash. Logit 8 keeps its
                // zero bias and the margin, so the other three heads still choose
                // class zero and no other control moves.
                WriteTestPolicy(hold, 9, "10");
                WriteTestPolicy(identity, -1, null);

                // Baseline: with no policy the dash goes out on the first ready
                // tick, which is the shipped fixed circuit...
                var plain = new FishronWingScript();
                plain.Tick(in input, player, in boss, arena, mobility);
                // The charge is a state edge, and the edge is what re-arms the
                // charge's single dash.
                input.NativeState = 1;
                var firstReady = plain.Tick(in input, player, in boss, arena, mobility);
                True(firstReady.Dash,
                    "without a policy the dash is issued as soon as it is ready");
                True(firstReady.Phase.EndsWith("-dash", StringComparison.Ordinal),
                    "the issued dash is what labels the branch");
                // ...and it is still single-shot: the charge does not dash twice.
                var secondReady = plain.Tick(in input, player, in boss, arena, mobility);
                False(secondReady.Dash, "a charge spends exactly one dash");

                // Held: the hold class suppresses the proposal. Note that a hold
                // is unconditional, so a false here does NOT by itself prove the
                // script proposed — the decisive tick is the identity switch
                // below, which can only come out true if the charge is still
                // armed and still proposing.
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", hold);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES",
                    nameof(FormulaRoute.FishronFairyWingsDash));
                LearnedPolicy.ResetCache();
                var holding = new FishronWingScript();
                False(holding.Tick(in input, player, in boss, arena, mobility).Dash,
                    "the residual can hold a proposed dash");
                False(holding.Tick(in input, player, in boss, arena, mobility).Dash,
                    "a hold stays held across ticks");

                // The decisive assertion. Switching to the identity policy asks
                // the script again with nothing adjusted: it answers true only if
                // the earlier hold left the dash armed AND the script is still
                // proposing. Under the old latch the first hold spent the
                // charge, so this tick would propose nothing and read false.
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", identity);
                LearnedPolicy.ResetCache();
                var issued = holding.Tick(in input, player, in boss, arena, mobility);
                True(issued.Dash,
                    "a held dash is still armed and issued once the policy allows it");
                True(issued.Phase.EndsWith("-dash", StringComparison.Ordinal),
                    "the late issue is labelled too");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", previousFile);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES", previousRoutes);
                LearnedPolicy.ResetCache();
                if (System.IO.File.Exists(hold)) System.IO.File.Delete(hold);
                if (System.IO.File.Exists(identity)) System.IO.File.Delete(identity);
            }
        }

        /// <summary>A dash the residual forced on a tick where nothing was ready
        /// must not spend the charge's single dash.
        ///
        /// The residual's dash head can turn the bit <em>on</em> on a tick the
        /// script never proposed one — the force class exists precisely so that
        /// is a separate action from holding. The engine ignores such an input,
        /// because the dash is still on cooldown, but latching on it would
        /// quietly delete the legitimate dash later in the same charge, a
        /// strictly worse circuit than either the script or the policy asked
        /// for. Found by running the isolated probe with a dash policy: every
        /// phase came back labelled, and the run lost the fight it wins without
        /// a policy.</summary>
        private static void ForcedDashDoesNotBurnTheChargesDash()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            // Not ready yet, so the script proposes nothing at all.
            var mobility = new MobilitySnapshot { CanDash = true,
                DashReady = false };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 1,
                NativeTimer = 1 };

            var previousFile = Environment.GetEnvironmentVariable("CHAITE_POLICY_FILE");
            var previousRoutes = Environment.GetEnvironmentVariable("CHAITE_POLICY_ROUTES");
            var flip = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-force-dash-" + Guid.NewGuid().ToString("N") + ".txt");
            var identity = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-force-identity-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                // Logit 10 is the dash head's force class. It is a separate
                // class from the hold, so asking for a dash the script never
                // proposed no longer doubles as the way to hold one.
                WriteTestPolicy(flip, 10, "10");
                WriteTestPolicy(identity, -1, null);

                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", flip);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES",
                    nameof(FormulaRoute.FishronFairyWingsDash));
                LearnedPolicy.ResetCache();

                var script = new FishronWingScript();
                // Nothing is ready, so the script proposes nothing and the
                // force class is what turns the bit on.
                True(script.Tick(in input, player, in boss, arena, mobility).Dash,
                    "the residual can force the bit on");

                // The dash is now ready. The charge must still own it.
                mobility.DashReady = true;
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", identity);
                LearnedPolicy.ResetCache();
                True(script.Tick(in input, player, in boss, arena, mobility).Dash,
                    "a forced dash on an unready tick does not spend the charge");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", previousFile);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES", previousRoutes);
                LearnedPolicy.ResetCache();
                if (System.IO.File.Exists(flip)) System.IO.File.Delete(flip);
                if (System.IO.File.Exists(identity)) System.IO.File.Delete(identity);
            }
        }

        /// <summary>Writes a one-hidden-unit policy file whose only nonzero
        /// weight is one logit's bias. Pass -1 for the all-zero identity policy.
        ///
        /// The layout is four header tokens, one weight per input, one hidden
        /// bias, one weight per head and one bias per head. It is derived from
        /// the loader's own constants rather than written out, so widening the
        /// input or a head cannot leave these tests quietly building files the
        /// loader rejects — which is exactly what happened when the dash
        /// readiness features landed and all three tests failed closed with
        /// "declares 38 inputs but this build expects 40".</summary>
        private static void WriteTestPolicy(string path, int biasLogit, string bias)
        {
            var tokens = new System.Collections.Generic.List<string>
            {
                "chaite-policy", "1",
                LearnedPolicy.InputCount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                "1"
            };
            var headBiasBase = 4 + LearnedPolicy.InputCount + 1
                + LearnedPolicy.HeadCount;
            var total = headBiasBase + LearnedPolicy.HeadCount;
            for (var i = 4; i < total; i++) tokens.Add("0");
            if (biasLogit >= 0) tokens[headBiasBase + biasLogit] = bias;
            System.IO.File.WriteAllLines(path, tokens);
        }

        /// <summary>The feature vector and the head layout are written down twice:
        /// once in the loader and once in the tool that writes policy files. The
        /// loader fails closed on a header that disagrees, so a drift shows up as
        /// every trained policy being rejected — discovered only after a training
        /// run has already been paid for. Pin both ends here.</summary>
        private static void PolicyLayoutMatchesTheToolThatWritesIt()
        {
            True(LearnedPolicy.InputCount == 40,
                "the input count changed: new-policy.ps1, the layout docs and " +
                "every existing policy file must move with it");
            True(LearnedPolicy.HeadCount == 11,
                "the head count changed: new-policy.ps1 and the layout docs " +
                "must move with it");

            var tool = System.IO.Path.Combine(FindRepositoryRoot(), "tools",
                "new-policy.ps1");
            True(System.IO.File.Exists(tool),
                "new-policy.ps1 is missing: " + tool);
            var text = System.IO.File.ReadAllText(tool);
            True(text.Contains("$input_ = " + LearnedPolicy.InputCount),
                "new-policy.ps1 does not declare the input count the loader " +
                "expects, so every policy it writes would be rejected");
            True(text.Contains("$heads = " + LearnedPolicy.HeadCount),
                "new-policy.ps1 does not declare the head count the loader " +
                "expects, so every policy it writes would be rejected");

            // The vector itself: the two dash-state features are the last two
            // entries, and they are what let a policy condition on whether a
            // dash would even be accepted. If they move, the whole vector's
            // meaning moves with them, because the weights are positional.
            var features = new float[LearnedPolicy.InputCount];
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            LearnedPolicy.FillFeatures(
                new FormulaScriptInput { BossType = 370 }, player, in boss,
                new ArenaSnapshot(),
                new MobilitySnapshot { CanDash = true, DashReady = true },
                features);
            True(features[LearnedPolicy.InputCount - 1] == 1f,
                "the last feature is not the dash-available flag");
            True(features[LearnedPolicy.InputCount - 2] == 1f,
                "the second-to-last feature is not the dash-ready flag");

            // Fail closed: a snapshot that does not say a dash is ready must not
            // read as ready, or a policy would be told it can dash when it cannot.
            var unknown = new float[LearnedPolicy.InputCount];
            LearnedPolicy.FillFeatures(
                new FormulaScriptInput { BossType = 370 }, player, in boss,
                new ArenaSnapshot(), null, unknown);
            True(unknown[LearnedPolicy.InputCount - 1] == 0f,
                "a missing mobility snapshot reads as dash-available");
            True(unknown[LearnedPolicy.InputCount - 2] == 0f,
                "a missing mobility snapshot reads as dash-ready");
        }

        /// <summary>The dash head has three classes on purpose: keep, hold and
        /// force. Hold must suppress without ever turning the bit on, and force
        /// must turn it on without that also being how a hold is expressed.
        /// Under the old two-way flip the two were the same action, and the probe
        /// showed the cost — the policy spent the dash during cruise and left the
        /// charge itself on cooldown.</summary>
        private static void DashHeadSeparatesHoldingFromForcing()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 1 };

            var hold = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-threeway-hold-" + Guid.NewGuid().ToString("N") + ".txt");
            var force = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-threeway-force-" + Guid.NewGuid().ToString("N") + ".txt");
            var identity = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "chaite-threeway-identity-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                WriteTestPolicy(hold, 9, "10");
                WriteTestPolicy(force, 10, "10");
                WriteTestPolicy(identity, -1, null);

                // Nothing is ready, so the script proposes nothing.
                var cold = new MobilitySnapshot { CanDash = true,
                    DashReady = false };
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", identity);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES",
                    nameof(FormulaRoute.FishronFairyWingsDash));
                LearnedPolicy.ResetCache();
                False(new FishronWingScript().Tick(in input, player, in boss,
                    arena, cold).Dash,
                    "an unready dash is not proposed in the first place");

                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", hold);
                LearnedPolicy.ResetCache();
                False(new FishronWingScript().Tick(in input, player, in boss,
                    arena, cold).Dash,
                    "the hold class must not turn a dash on where the script " +
                    "proposed none: that is what spent the charge during cruise");

                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", force);
                LearnedPolicy.ResetCache();
                True(new FishronWingScript().Tick(in input, player, in boss,
                    arena, cold).Dash,
                    "the force class can ask for a dash the script did not propose");

                // Now with a dash actually ready, so the script does propose.
                var warm = new MobilitySnapshot { CanDash = true,
                    DashReady = true };
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", hold);
                LearnedPolicy.ResetCache();
                var held = new FishronWingScript();
                held.Tick(in input, player, in boss, arena, warm);
                input.NativeState = 1;
                False(held.Tick(in input, player, in boss, arena, warm).Dash,
                    "the hold class suppresses a proposed dash");

                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", force);
                LearnedPolicy.ResetCache();
                var forced = new FishronWingScript();
                forced.Tick(in input, player, in boss, arena, warm);
                input.NativeState = 1;
                True(forced.Tick(in input, player, in boss, arena, warm).Dash,
                    "the force class also issues a proposed dash");
            }
            finally
            {
                Environment.SetEnvironmentVariable("CHAITE_POLICY_FILE", null);
                Environment.SetEnvironmentVariable("CHAITE_POLICY_ROUTES", null);
                LearnedPolicy.ResetCache();
                foreach (var path in new[] { hold, force, identity })
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        /// <summary>The four Fishron scripts cover the four admitted routes.
        /// An unprobed route is also untested at the tick level, and a control
        /// outside the allowed set or an accepted tick with no phase label would
        /// waste a whole probe session rather than produce a result, so this
        /// sweeps every script over every native state and a grid of player
        /// offsets and mount states.
        ///
        /// This is deliberately not a behaviour test. It cannot say whether the
        /// movement is any good, only that no state and no geometry produces a
        /// malformed output. Each sweep also asserts that it accepted at least
        /// one tick, so a script whose gate rejects the whole grid fails here
        /// instead of quietly passing while proving nothing.</summary>
        private static void EveryScriptStaysWellFormedAcrossStatesAndGeometry()
        {
            var offsets = new[] { -1500f, -600f, -100f, 0f, 100f, 600f, 1500f };
            var states = new[] { 0, 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
            const float bossX = 2400f;
            const float bossY = 900f;

            Action<string, FormulaRoute[], int, int,
                Func<FormulaScriptInput, PlayerSnapshot, TargetSnapshot,
                    ArenaSnapshot, MobilitySnapshot, DifficultySnapshot,
                    FormulaScriptOutput>> sweep =
                (name, routes, bossType, mountType, call) =>
                {
                    var accepted = 0;
                    foreach (var route in routes)
                    foreach (var state in states)
                    foreach (var dx in offsets)
                    foreach (var dy in offsets)
                    foreach (var mounted in new[] { false, true })
                    {
                        var where = name + " state " + state + " dx " + dx +
                            " dy " + dy + " mounted " + mounted;
                        var player = new PlayerSnapshot
                        {
                            Position = new Vec2(bossX + dx, bossY + dy),
                            Width = 20, Height = 40
                        };
                        var boss = new TargetSnapshot
                        {
                            Type = bossType,
                            Position = new Vec2(bossX, bossY),
                            Width = 150, Height = 100
                        };
                        var arena = new ArenaSnapshot
                        {
                            ClearanceLeft = 1500, ClearanceRight = 1800,
                            ClearanceUp = 700, ClearanceDown = 900,
                            FloorSupport = new SupportSpan
                            {
                                Valid = true, Left = 400, Right = 4400
                            }
                        };
                        var mobility = new MobilitySnapshot
                        {
                            SelectedMountIdentityKnown = true,
                            SelectedMountType = mountType,
                            ActiveMountReleaseReady = true,
                            MountActive = mounted,
                            ActiveMountIdentityKnown = true,
                            ActiveMountType = mountType
                        };
                        // No reviewed formula route is gated on the weather any
                        // more: the rain-gated Shrimpy Truffle route was
                        // withdrawn as out of scope, so the sweep reports dry
                        // weather and no route depends on it.
                        var difficulty = new DifficultySnapshot
                        {
                            Rain = false, RainKnown = true
                        };
                        var input = new FormulaScriptInput
                        {
                            BossType = bossType, Route = route,
                            NativeState = state, NativeTimer = 20,
                            NativeSequence = 3, NativeFormKnown = true
                        };

                        FormulaScriptOutput output;
                        try
                        {
                            output = call(input, player, boss, arena, mobility,
                                difficulty);
                        }
                        catch (Exception ex)
                        {
                            True(false, where + " threw " + ex.GetType().Name +
                                ": " + ex.Message);
                            return;
                        }

                        True(output.Horizontal >= -1 && output.Horizontal <= 1,
                            where + " horizontal " + output.Horizontal);
                        True(output.Vertical >= -1 && output.Vertical <= 1,
                            where + " vertical " + output.Vertical);
                        if (!output.Accepted) continue;
                        accepted++;
                        True(!string.IsNullOrEmpty(output.Phase),
                            where + " accepted with no phase label");
                    }
                    True(accepted > 0, name + " accepted no tick, so the sweep " +
                        "proved nothing");
                };

            var fairyWing = new FishronWingScript();
            var strongWing = new FishronWingScript();
            var chillet = new FishronChilletScript();
            var chilletIgnis = new FishronChilletScript();

            sweep("fishron-fairy-wing",
                new[] { FormulaRoute.FishronFairyWingsDash }, 370, -1,
                (i, p, b, a, m, d) => fairyWing.Tick(in i, p, in b, a, m));
            sweep("fishron-strong-wing",
                new[] { FormulaRoute.FishronStrongWingsDash }, 370, -1,
                (i, p, b, a, m, d) => strongWing.Tick(in i, p, in b, a, m));
            sweep("fishron-trusty-chillet",
                new[] { FormulaRoute.FishronTrustyChillet }, 370, 64,
                (i, p, b, a, m, d) => chillet.Tick(in i, p, in b, a, m));
            sweep("fishron-trusty-chillet-ignis",
                new[] { FormulaRoute.FishronTrustyChilletIgnis }, 370, 65,
                (i, p, b, a, m, d) => chilletIgnis.Tick(in i, p, in b, a, m));
        }

        private static void BossMonitoringDoesNotOwnControls()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, PlayerLife = 400 };
            var armed = controller.ArmMonitoring(observation);
            Equal(SessionState.Monitoring, armed.Current);
            Equal(AudioCue.MonitorArmed, armed.Cue);
            False(armed.ApplyControls);
            False(controller.IsControlling);
            True(controller.IsSessionActive);
            observation.PlayerLife = 250;
            for (var i = 0; i < 2000; i++)
            {
                var update = controller.Update(observation);
                False(update.ApplyControls);
                Equal(AudioCue.None, update.Cue);
                Equal(SessionState.Monitoring, update.Current);
            }
            Equal(AudioCue.None, controller.ArmMonitoring(observation).Cue);
            controller.Cancel();
            False(controller.IsSessionActive);
            controller.ReturnToIdle();
            Equal(AudioCue.MonitorArmed, controller.ArmMonitoring(observation).Cue);
        }

        private static void BossMonitoringRejectsMidFightAndDeadArming()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss };
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
            observation.Flags = EncounterFlags.None;
            observation.PlayerDead = true;
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
        }

        private static void BossMonitoringTransitionsOnceAndResetsLifeAccounting()
        {
            var controller = new EncounterController(3);
            controller.ArmMonitoring(new EncounterObservation { StartAuthorized = true, PlayerLife = 400 });
            var arrived = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss,
                PlayerLife = 250, ActiveBossKeys = new[] { 4 }, ActiveBossTypes = new[] { 370 } };
            var activation = controller.Activate(arrived);
            Equal(SessionState.EngagedAlive, activation.Current);
            Equal(AudioCue.TryMinnie, activation.Cue);
            True(activation.ApplyControls);
            Equal(AudioCue.None, controller.Update(arrived).Cue);
            Equal(AudioCue.None, controller.Activate(arrived).Cue);
            arrived.PlayerLife = 249;
            Equal(AudioCue.Man, controller.Update(arrived).Cue);
        }

        private static void BossMonitoringProductionHasNoSummonOrSurvivalPath()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var runtime = FindCecilType(assembly, "Chaite.Plugin.Runtime");
                foreach (var method in runtime.Methods.Where(m => m.HasBody))
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var call = instruction.Operand as MethodReference;
                        if (call == null) continue;
                        False(call.Name == "ExecuteBossStart" || call.Name == "FindBossStartPlan" || call.Name == "PlanSurvival",
                            "Production monitor must not retain auto-summon/survival calls: " + method.Name);
                        if (method.Name == "ArmBossMonitor" || method.Name == "StopBossMonitor")
                            False(call.Name == "ApplyPlan" || call.Name == "SetSelectedItem" || call.Name == "ClearCombatControls",
                                "Passive monitor must not write native controls/selection: " + method.Name);
                    }
            }
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(CombatPlanner).Assembly.Location))
            {
                var planner = FindCecilType(assembly, "Chaite.Core.CombatPlanner");
                var formula = FindCecilMethod(planner, "PlanFormula");
                foreach (var instruction in formula.Body.Instructions)
                {
                    var call = instruction.Operand as MethodReference;
                    if (call == null) continue;
                    False(call.Name == "FindBestCandidate" || call.Name == "Evaluate" || call.Name == "PlanSurvival" ||
                        call.Name == "TrySelectReadyMobilityRoute", "Formula path must not run old scoring or mobility selection");
                }
            }
        }
    }
}
