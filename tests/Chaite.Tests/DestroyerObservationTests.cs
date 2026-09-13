using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunDestroyerObservationRegressions()
        {
            Run("Destroyer body motion uses consecutive positions and swept bounds", DestroyerBodyMotionUsesPositionDelta);
            Run("Destroyer duplicate snapshots preserve the real frame delta", DestroyerDuplicateSnapshotPreservesDelta);
            Run("Destroyer history resets on inactive and broken chains", DestroyerHistoryResetsOnInactiveAndBrokenChain);
            Run("Destroyer facade validates the native predecessor chain", DestroyerFacadeValidatesPredecessorChain);
            Run("Destroyer predecessor keys require exact finite native slots", DestroyerPredecessorKeysRequireExactSlots);
            Run("Destroyer history rejects identity changes frame gaps and jumps", DestroyerHistoryRejectsDiscontinuities);
            Run("Destroyer same-slot life reset fails closed", DestroyerSameSlotLifeResetFailsClosed);
            Run("Destroyer head retains bounded native velocity on first sight", DestroyerHeadFirstSightUsesNativeVelocity);
            Run("Destroyer native snapshot contract is public and defaults unknown", DestroyerNativeSnapshotContractDefaultsUnknown);
            Run("Destroyer facade publishes the native Skyblock flag", DestroyerFacadePublishesNativeSkyblockFlag);
            Run("Destroyer facade publishes native game mode and excluded world flags", DestroyerFacadePublishesNativeWorldContract);
            Run("Destroyer facade reads exact effective wing and rocket-boot identities", DestroyerFacadeReadsEffectiveEquipmentIdentity);
            Run("Destroyer root capture is never clipped by the ordinary target radius", DestroyerRootCaptureBypassesOrdinaryRadius);
            Run("Destroyer head branch requires trusted identity history and localAI zero", DestroyerHeadBranchRequiresTrustedHistory);
            Run("Destroyer Runtime preflight rejects before boss-start execution", DestroyerRuntimePreflightDominatesBossStart);
            Run("Destroyer Runtime handles requested control return before applying input", DestroyerRuntimeControlReturnDominatesInput);
            Run("Destroyer LOS budget always reserves one Probe query", DestroyerLosBudgetReservesProbe);
            Run("Destroyer Probe pressure takes the next LOS query", DestroyerProbePressurePrioritizesUnknownProbe);
            Run("Destroyer LOS round robin cannot starve eighty moving slots", DestroyerLosRoundRobinDoesNotStarveMovingChain);
        }

        private static void DestroyerRootCaptureBypassesOrdinaryRadius()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.NativeTargetCapturePolicy", true);
            var method = type.GetMethod("ShouldInclude", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null);
            Func<float, float, bool, bool, bool> include = (distance, maximum, boss, validSlot) =>
                (bool)method.Invoke(null, new object[] { distance, maximum, boss, validSlot });

            True(include(100f, 400f, false, true));
            False(include(401f, 400f, false, true));
            True(include(4000f, 400f, true, true), "a live Boss root must remain observable outside the ordinary radius");
            False(include(100f, 400f, true, false), "a malformed native slot must never become a target");
        }

        private static void DestroyerBodyMotionUsesPositionDelta()
        {
            var history = new DestroyerMotionFixture();
            var first = history.Observe(7, 7, 135, 2, 6, true,
                new Vec2(100f, 200f), 40, 50, new Vec2(0f, 0f), 1);
            False(first.Trusted);
            Equal(0f, first.Velocity.X);
            Equal(0f, first.Velocity.Y);
            Equal(100f, first.SweepPosition.X);
            Equal(200f, first.SweepPosition.Y);
            Equal(40, first.SweepWidth);
            Equal(50, first.SweepHeight);

            var second = history.Observe(7, 7, 135, 2, 6, true,
                new Vec2(124f, 188f), 40, 50, new Vec2(0f, 0f), 2);
            True(second.Trusted);
            Equal(24f, second.Velocity.X);
            Equal(-12f, second.Velocity.Y);
            Equal(100f, second.SweepPosition.X);
            Equal(188f, second.SweepPosition.Y);
            Equal(64, second.SweepWidth);
            Equal(62, second.SweepHeight);
        }

        private static void DestroyerDuplicateSnapshotPreservesDelta()
        {
            var history = new DestroyerMotionFixture();
            history.Observe(8, 8, 136, 2, 7, true,
                new Vec2(100f, 200f), 40, 50, new Vec2(), 1);
            var firstRead = history.Observe(8, 8, 136, 2, 7, true,
                new Vec2(124f, 188f), 40, 50, new Vec2(), 2);
            var duplicate = history.Observe(8, 8, 136, 2, 7, true,
                new Vec2(124f, 188f), 40, 50, new Vec2(), 2);
            True(firstRead.Trusted);
            True(duplicate.Trusted);
            Equal(firstRead.Velocity.X, duplicate.Velocity.X);
            Equal(firstRead.Velocity.Y, duplicate.Velocity.Y);
            Equal(firstRead.SweepWidth, duplicate.SweepWidth);
            Equal(firstRead.SweepHeight, duplicate.SweepHeight);

            var next = history.Observe(8, 8, 136, 2, 7, true,
                new Vec2(130f, 180f), 40, 50, new Vec2(), 3);
            True(next.Trusted);
            Equal(6f, next.Velocity.X);
            Equal(-8f, next.Velocity.Y);
        }

        private static void DestroyerHistoryResetsOnInactiveAndBrokenChain()
        {
            var history = new DestroyerMotionFixture();
            history.Observe(9, 9, 135, 2, 8, true, new Vec2(100f, 100f), 40, 40, new Vec2(), 1);
            True(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(110f, 100f), 40, 40, new Vec2(), 2).Trusted);
            history.Invalidate(9);
            False(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(120f, 100f), 40, 40, new Vec2(), 3).Trusted);
            True(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(130f, 100f), 40, 40, new Vec2(), 4).Trusted);

            var disconnected = history.Observe(9, 9, 135, 2, 8, false,
                new Vec2(140f, 100f), 40, 40, new Vec2(), 5);
            False(disconnected.Trusted);
            Equal(0f, disconnected.Velocity.X);
            False(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(150f, 100f), 40, 40, new Vec2(), 6).Trusted,
                "a reconnected chain must start a fresh identity history");
            True(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(160f, 100f), 40, 40, new Vec2(), 7).Trusted);
            history.Clear();
            False(history.Observe(9, 9, 135, 2, 8, true,
                new Vec2(170f, 100f), 40, 40, new Vec2(), 8).Trusted,
                "session reset must discard prior Destroyer motion");
        }

        private static void DestroyerFacadeValidatesPredecessorChain()
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var facade = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            Action<string, object> set = (name, value) =>
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing Destroyer chain fixture field: " + name);
                field.SetValue(facade, value);
            };
            set("_npcActive", new Func<object, bool>(value => ((DestroyerLinkNpc)value).Active));
            set("_npcTypeId", new Func<object, int>(value => ((DestroyerLinkNpc)value).Type));
            set("_whoAmI", new Func<object, int>(value => ((DestroyerLinkNpc)value).Who));
            set("_npcRealLife", new Func<object, int>(value => ((DestroyerLinkNpc)value).Root));
            set("_npcLife", new Func<object, int>(value => ((DestroyerLinkNpc)value).Life));
            var method = type.GetMethod("DestroyerChainConnected", BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null);
            var npcs = new object[20];
            var head = new DestroyerLinkNpc { Active = true, Type = 134, Who = 2, Root = -1, Life = 100 };
            var body = new DestroyerLinkNpc { Active = true, Type = 135, Who = 6, Root = 2, Life = 100 };
            var current = new DestroyerLinkNpc { Active = true, Type = 135, Who = 7, Root = 2, Life = 100 };
            npcs[2] = head;
            npcs[6] = body;
            npcs[7] = current;
            Func<bool> connected = () => (bool)method.Invoke(facade, new object[] { npcs, 7, 2, 6 });
            True(connected());
            current.Who = 8;
            False(connected(), "the current part must identify the array slot it occupies");
            current.Who = 7;
            current.Root = 3;
            False(connected(), "the current part must identify the same root as its predecessor");
            current.Root = 2;
            current.Type = 134;
            False(connected(), "the chain-link validator accepts only body and tail current nodes");
            current.Type = 135;
            current.Life = 0;
            False(connected(), "a dead current part cannot preserve motion continuity");
            current.Life = 100;
            body.Active = false;
            False(connected());
            body.Active = true;
            body.Root = 3;
            False(connected());
            body.Root = 2;
            body.Who = 5;
            False(connected());
            body.Who = 6;
            body.Type = 136;
            False(connected(), "a tail cannot be the predecessor of another live part");
            body.Type = 135;
            body.Life = 0;
            False(connected(), "a dead predecessor cannot establish continuity");
            body.Life = 100;
            head.Active = false;
            False(connected());
            head.Active = true;
            head.Type = 135;
            False(connected());
            head.Type = 134;
            False((bool)method.Invoke(facade, new object[] { npcs, 7, 2, 7 }),
                "a segment cannot identify itself as its predecessor");
        }

        private static void DestroyerPredecessorKeysRequireExactSlots()
        {
            int slot;
            True(TryReadDestroyerSlot(0f, 20, out slot));
            Equal(0, slot);
            True(TryReadDestroyerSlot(19f, 20, out slot));
            Equal(19, slot);
            foreach (var invalid in new[] { -1f, 20f, 1.25f, float.NaN,
                float.PositiveInfinity, float.NegativeInfinity })
            {
                False(TryReadDestroyerSlot(invalid, 20, out slot));
                Equal(-1, slot);
            }
            False(TryReadDestroyerSlot(0f, 0, out slot));
        }

        private static void DestroyerHistoryRejectsDiscontinuities()
        {
            var history = new DestroyerMotionFixture();
            history.Observe(10, 10, 135, 2, 9, true, new Vec2(0f, 0f), 40, 40, new Vec2(), 1);
            var jump = history.Observe(10, 10, 135, 2, 9, true,
                new Vec2(1000f, 800f), 40, 40, new Vec2(), 2);
            False(jump.Trusted);
            Equal(1000f, jump.SweepPosition.X);
            Equal(800f, jump.SweepPosition.Y);
            Equal(40, jump.SweepWidth);
            Equal(40, jump.SweepHeight);
            True(history.Observe(10, 10, 135, 2, 9, true,
                new Vec2(1010f, 800f), 40, 40, new Vec2(), 3).Trusted);

            history.Observe(11, 11, 135, 2, 10, true, new Vec2(0f, 0f), 40, 40, new Vec2(), 1);
            False(history.Observe(11, 11, 135, 2, 10, true,
                new Vec2(10f, 0f), 40, 40, new Vec2(), 3).Trusted,
                "a missed observation frame cannot be treated as one tick of velocity");

            history.Observe(12, 12, 135, 2, 11, true, new Vec2(0f, 0f), 40, 40, new Vec2(), 1);
            False(history.Observe(12, 12, 135, 3, 11, true,
                new Vec2(10f, 0f), 40, 40, new Vec2(), 2).Trusted);
            False(history.Observe(12, 12, 136, 3, 11, true,
                new Vec2(20f, 0f), 40, 40, new Vec2(), 3).Trusted);
            False(history.Observe(12, 13, 136, 3, 11, true,
                new Vec2(30f, 0f), 40, 40, new Vec2(), 4).Trusted);
            False(history.Observe(12, 13, 136, 3, 10, true,
                new Vec2(40f, 0f), 40, 40, new Vec2(), 5).Trusted);

            var outsideNativeSlots = history.Observe(200, 200, 135, 2, 1, true,
                new Vec2(0f, 0f), 40, 40, new Vec2(), 1);
            False(outsideNativeSlots.Trusted);
            False(history.Observe(200, 200, 135, 2, 1, true,
                new Vec2(10f, 0f), 40, 40, new Vec2(), 2).Trusted);
        }

        private static void DestroyerSameSlotLifeResetFailsClosed()
        {
            var history = new DestroyerMotionFixture();
            history.Observe(12, 12, 135, 2, 11, true,
                new Vec2(100f, 100f), 40, 40, new Vec2(), 1, 1000, 1000);
            True(history.Observe(12, 12, 135, 2, 11, true,
                new Vec2(110f, 100f), 40, 40, new Vec2(), 2, 900, 1000).Trusted);

            // Native whoAmI remains the array slot across reuse. A replacement
            // which resets public life state must not inherit the old delta.
            var replacement = history.Observe(12, 12, 135, 2, 11, true,
                new Vec2(120f, 100f), 40, 40, new Vec2(), 3, 1000, 1000);
            False(replacement.Trusted);
            Equal(0f, replacement.Velocity.X);
            True(history.Observe(12, 12, 135, 2, 11, true,
                new Vec2(130f, 100f), 40, 40, new Vec2(), 4, 990, 1000).Trusted);

            False(history.Observe(12, 13, 135, 2, 11, true,
                new Vec2(140f, 100f), 40, 40, new Vec2(), 5, 980, 1000).Trusted,
                "the current whoAmI must equal its native slot");
        }

        private static void DestroyerHeadFirstSightUsesNativeVelocity()
        {
            var history = new DestroyerMotionFixture();
            var first = history.Observe(2, 2, 134, 2, -1, true,
                new Vec2(100f, 200f), 60, 60, new Vec2(12f, -4f), 1);
            False(first.Trusted);
            Equal(12f, first.Velocity.X);
            Equal(-4f, first.Velocity.Y);
            var second = history.Observe(2, 2, 134, 2, -1, true,
                new Vec2(110f, 197f), 60, 60, new Vec2(13f, -3.5f), 2);
            True(second.Trusted);
            Equal(13f, second.Velocity.X);
            Equal(-3.5f, second.Velocity.Y);

            var implausible = history.Observe(3, 3, 134, 3, -1, true,
                new Vec2(0f, 0f), 60, 60, new Vec2(10000f, 0f), 1);
            Equal(0f, implausible.Velocity.X);
        }

        private static void DestroyerNativeSnapshotContractDefaultsUnknown()
        {
            var combatFields = new[] { "NativeContextKnown", "NetMode", "LocalPlayerIndex" };
            for (var i = 0; i < combatFields.Length; i++)
                True(typeof(CombatSnapshot).GetField(combatFields[i], BindingFlags.Instance | BindingFlags.Public) != null,
                    "missing public CombatSnapshot field " + combatFields[i]);
            var targetFields = new[] { "NativeTargetKnown", "NativeTargetPlayerIndex",
                "DestroyerBranchKnown", "DestroyerUsesWormMovement" };
            for (var i = 0; i < targetFields.Length; i++)
                True(typeof(TargetSnapshot).GetField(targetFields[i], BindingFlags.Instance | BindingFlags.Public) != null,
                    "missing public TargetSnapshot field " + targetFields[i]);
            var facade = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var netMode = facade.GetField("_netMode", BindingFlags.Instance | BindingFlags.NonPublic);
            var npcTarget = facade.GetField("_npcTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            var skyblockWorld = facade.GetField("_skyblockWorld", BindingFlags.Instance | BindingFlags.NonPublic);
            True(netMode != null && netMode.FieldType == typeof(Func<int>),
                "facade must bind Main.netMode as an integer getter");
            True(npcTarget != null && npcTarget.FieldType == typeof(Func<object, int>),
                "facade must bind NPC.target as an integer getter");
            True(skyblockWorld != null && skyblockWorld.FieldType == typeof(Func<bool>),
                "facade must bind Main.skyblockWorld as a boolean getter");
            True(typeof(DifficultySnapshot).GetField("Skyblock", BindingFlags.Instance | BindingFlags.Public) != null,
                "DifficultySnapshot must expose the native Skyblock flag");
            True(typeof(BossRequirements).GetField("RequiresDestroyerP1Contract",
                BindingFlags.Instance | BindingFlags.Public) != null,
                "BossRequirements must expose the shared Destroyer P1 admission contract");

            var snapshot = new CombatSnapshot();
            False(snapshot.NativeContextKnown);
            Equal(0, snapshot.NetMode);
            Equal(0, snapshot.LocalPlayerIndex);
            False(snapshot.Difficulty.GameModeKnown);
            False(snapshot.Player.FunctionalEquipmentIdentityKnown);
            Equal(0, snapshot.Player.WingAccessoryItemType);
            Equal(0, snapshot.Player.RocketBootAccessoryItemType);
            var target = default(TargetSnapshot);
            False(target.NativeTargetKnown);
            False(target.DestroyerBranchKnown);
            False(target.DestroyerUsesWormMovement);
        }

        private static void DestroyerFacadePublishesNativeSkyblockFlag()
        {
            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var facade = FindCecilType(assembly, "Chaite.Plugin.TerrariaFacade");
                var method = FindCecilMethod(facade, "BuildCombatSnapshot");
                var read = -1;
                var write = -1;
                for (var i = 0; i < method.Body.Instructions.Count; i++)
                {
                    var field = method.Body.Instructions[i].Operand as Mono.Cecil.FieldReference;
                    if (field == null) continue;
                    if (field.Name == "_skyblockWorld") read = i;
                    if (field.DeclaringType.FullName == typeof(DifficultySnapshot).FullName && field.Name == "Skyblock") write = i;
                }
                True(read >= 0, "BuildCombatSnapshot does not read Main.skyblockWorld");
                True(write > read && write - read <= 4,
                    "BuildCombatSnapshot must directly publish the native Skyblock getter into DifficultySnapshot.Skyblock");
            }
        }

        private static void DestroyerFacadePublishesNativeWorldContract()
        {
            var facade = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            foreach (var binding in new[] { "_gameMode", "_drunkWorld", "_notTheBeesWorld" })
                True(facade.GetField(binding, BindingFlags.Instance | BindingFlags.NonPublic) != null,
                    "missing native world binding " + binding);
            foreach (var field in new[] { "GameModeKnown", "GameMode", "Journey", "Drunk", "NotTheBees" })
                True(typeof(DifficultySnapshot).GetField(field, BindingFlags.Instance | BindingFlags.Public) != null,
                    "missing DifficultySnapshot field " + field);

            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var method = FindCecilMethod(FindCecilType(assembly, "Chaite.Plugin.TerrariaFacade"), "BuildCombatSnapshot");
                foreach (var pair in new[]
                {
                    new[] { "_gameMode", "GameMode" }, new[] { "_drunkWorld", "Drunk" },
                    new[] { "_notTheBeesWorld", "NotTheBees" }
                })
                {
                    var read = -1;
                    var write = -1;
                    for (var i = 0; i < method.Body.Instructions.Count; i++)
                    {
                        var field = method.Body.Instructions[i].Operand as Mono.Cecil.FieldReference;
                        if (field == null) continue;
                        if (field.Name == pair[0]) read = i;
                        if (field.DeclaringType.FullName == typeof(DifficultySnapshot).FullName && field.Name == pair[1]) write = i;
                    }
                    True(read >= 0 && write > read, pair[0] + " is not published into " + pair[1]);
                }
            }
        }

        private static void DestroyerFacadeReadsEffectiveEquipmentIdentity()
        {
            var facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TerrariaFacade", true);
            var facade = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(facadeType);
            Action<string, object> set = (name, value) =>
            {
                var field = facadeType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing equipment identity binding " + name);
                field.SetValue(facade, value);
            };
            var player = new DestroyerEquipmentPlayer();
            player.Slots[3] = new DestroyerEquipmentItem { Type = 492, WingSlot = 1 };
            player.Slots[4] = new DestroyerEquipmentItem { Type = 898 };
            set("_getEffectiveArmor", new Func<object, int, object>((value, slot) => ((DestroyerEquipmentPlayer)value).Slots[slot]));
            set("_isItemSlotUnlockedAndUsable", new Func<object, int, bool>((value, slot) => slot >= 3 && slot <= 7));
            set("_itemTypeId", new Func<object, int>(value => ((DestroyerEquipmentItem)value).Type));
            set("_itemWingSlot", new Func<object, sbyte>(value => ((DestroyerEquipmentItem)value).WingSlot));
            set("_playerBuffType", new Func<object, int[]>(value => new int[22]));
            set("_playerBuffTime", new Func<object, int[]>(value => new int[22]));
            var read = facadeType.GetMethod("ReadFunctionalEquipmentIdentity", BindingFlags.Instance | BindingFlags.NonPublic);
            True(read != null);

            var state = new PlayerSnapshot();
            read.Invoke(facade, new object[] { player, state });
            True(state.FunctionalEquipmentIdentityKnown);
            Equal(492, state.WingAccessoryItemType);
            Equal(898, state.RocketBootAccessoryItemType);

            player.Slots[4] = new DestroyerEquipmentItem { Type = 405 };
            state = new PlayerSnapshot();
            read.Invoke(facade, new object[] { player, state });
            Equal(405, state.RocketBootAccessoryItemType);

            player.Slots[5] = new DestroyerEquipmentItem { Type = 493, WingSlot = 2 };
            state = new PlayerSnapshot();
            read.Invoke(facade, new object[] { player, state });
            Equal(-1, state.WingAccessoryItemType);
        }

        private static void DestroyerRuntimePreflightDominatesBossStart()
        {
            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var runtime = FindCecilType(assembly, "Chaite.Plugin.Runtime");
                var tick = FindCecilMethod(runtime, "Tick");
                var requirements = -1;
                var reset = -1;
                var bossStart = -1;
                for (var i = 0; i < tick.Body.Instructions.Count; i++)
                {
                    var call = tick.Body.Instructions[i].Operand as Mono.Cecil.MethodReference;
                    if (call == null) continue;
                    if (call.Name == "PrepareForSupportedExpectedEncounter" ||
                        call.Name == "PrepareForExpectedEncounter") requirements = i;
                    else if (call.Name == "ResetSessionAutomation" && requirements >= 0 && bossStart < 0) reset = i;
                    else if (call.Name == "ExecuteBossStart") bossStart = i;
                }
                True(requirements >= 0 && bossStart > requirements,
                    "Runtime must perform the supported preflight before ExecuteBossStart can consume a summon");
                var conditional = -1;
                for (var i = requirements + 1;
                    i < tick.Body.Instructions.Count && i <= requirements + 12;
                    i++)
                {
                    if (tick.Body.Instructions[i].OpCode.FlowControl ==
                        Mono.Cecil.Cil.FlowControl.Cond_Branch)
                    {
                        conditional = i;
                        break;
                    }
                }
                // Release branches directly. Debug materializes `!result`
                // through ldc/ceq and a temporary local before the same guard.
                True(conditional > requirements && (reset < 0 || conditional < reset),
                    "PrepareForExpectedEncounter must immediately guard the boss-start path");
            }
        }

        private static void DestroyerRuntimeControlReturnDominatesInput()
        {
            using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var tick = FindCecilMethod(FindCecilType(assembly, "Chaite.Plugin.Runtime"), "Tick");
                var requests = new List<int>();
                for (var i = 0; i < tick.Body.Instructions.Count; i++)
                {
                    var field = tick.Body.Instructions[i].Operand as Mono.Cecil.FieldReference;
                    if (field != null && field.DeclaringType.FullName == typeof(ControlPlan).FullName &&
                        field.Name == "RequestControlReturn") requests.Add(i);
                }
                True(requests.Count > 0,
                    "Runtime must inspect requested non-fatal control return");
                for (var requestIndex = 0; requestIndex < requests.Count;
                    requestIndex++)
                {
                    var request = requests[requestIndex];
                    var limit = requestIndex + 1 < requests.Count ?
                        requests[requestIndex + 1] :
                        tick.Body.Instructions.Count;
                    var cancel = -1;
                    var finish = -1;
                    var apply = -1;
                    for (var i = request + 1; i < limit; i++)
                    {
                        var call = tick.Body.Instructions[i].Operand as
                            Mono.Cecil.MethodReference;
                        if (call == null) continue;
                        if (cancel < 0 && call.DeclaringType.FullName ==
                                typeof(EncounterController).FullName &&
                            call.Name == "Cancel") cancel = i;
                        if (finish < 0 && call.DeclaringType.FullName ==
                                "Chaite.Plugin.Runtime" &&
                            call.Name == "Finish") finish = i;
                        if (apply < 0 && call.DeclaringType.FullName ==
                                "Chaite.Plugin.TerrariaFacade" &&
                            call.Name == "ApplyPlan") apply = i;
                    }
                    // ApplyPlan now has its own fail-closed return branch with
                    // a later Cancel/Finish. Compare the first calls belonging
                    // to each RequestControlReturn guard, not the final calls
                    // in the whole method.
                    True(cancel > request && finish > cancel &&
                        (apply < 0 || apply > finish),
                        "requested non-fatal control return must cancel and " +
                        "finish before any plan input is applied");
                }
            }
        }

        private static Mono.Cecil.TypeDefinition FindCecilType(Mono.Cecil.AssemblyDefinition assembly, string name)
        {
            for (var i = 0; i < assembly.MainModule.Types.Count; i++)
                if (assembly.MainModule.Types[i].FullName == name) return assembly.MainModule.Types[i];
            throw new InvalidOperationException("missing IL type " + name);
        }

        private static Mono.Cecil.MethodDefinition FindCecilMethod(Mono.Cecil.TypeDefinition type, string name)
        {
            Mono.Cecil.MethodDefinition found = null;
            for (var i = 0; i < type.Methods.Count; i++)
            {
                var candidate = type.Methods[i];
                if (candidate.Name != name || !candidate.HasBody) continue;
                if (found != null) throw new InvalidOperationException("ambiguous IL method " + type.FullName + "::" + name);
                found = candidate;
            }
            return found ?? throw new InvalidOperationException("missing IL method " + type.FullName + "::" + name);
        }

        private static void DestroyerHeadBranchRequiresTrustedHistory()
        {
            var history = new DestroyerMotionFixture();
            var first = history.Observe(2, 2, 134, 2, -1, true,
                new Vec2(100f, 200f), 60, 60, new Vec2(12f, -4f), 1);
            bool worm;
            False(TryReadDestroyerBranch(134, first.Trusted, new[] { 0f, 1f, 0f, 0f }, out worm));
            False(worm, "a first-frame zero must not become a known air branch");

            var second = history.Observe(2, 2, 134, 2, -1, true,
                new Vec2(110f, 197f), 60, 60, new Vec2(13f, -3.5f), 2);
            True(second.Trusted);
            True(TryReadDestroyerBranch(134, second.Trusted, new[] { 0f, 1f, 0f, 0f }, out worm));
            False(worm, "localAI[1]=1 must not replace the final localAI[0] branch");
            True(TryReadDestroyerBranch(134, second.Trusted, new[] { 1f, 1f, 0f, 0f }, out worm));
            True(worm, "localAI[0]=1 is the remembered worm-movement branch");

            False(TryReadDestroyerBranch(135, true, new[] { 1f, 1f, 0f, 0f }, out worm));
            False(worm, "body localAI[0] must never be interpreted as the head branch");
            False(TryReadDestroyerBranch(136, true, new[] { 1f, 1f, 0f, 0f }, out worm));
            False(TryReadDestroyerBranch(134, true, new[] { 2f, 1f, 0f, 0f }, out worm));
            False(TryReadDestroyerBranch(134, true, new[] { float.NaN, 1f, 0f, 0f }, out worm));
        }

        private static void DestroyerLosBudgetReservesProbe()
        {
            var targets = new List<TargetSnapshot>
            {
                SightTarget(135, 100f), SightTarget(136, 150f), SightTarget(135, 200f),
                SightTarget(139, 600f)
            };
            var center = new Vec2(0f, 0f);
            var sawProbe = false;
            for (var budget = 3; budget >= 1; budget--)
            {
                var selected = SelectSightTarget(targets, center, budget);
                True(selected >= 0);
                var target = targets[selected];
                if (target.Type == 139) sawProbe = true;
                target.LineOfSightKnown = true;
                targets[selected] = target;
            }
            True(sawProbe, "three nearer worm segments must not consume all three LOS rays");
            var distantProbe = new List<TargetSnapshot>
            {
                SightTarget(135, 50f), SightTarget(139, 1200f)
            };
            Equal(139, distantProbe[SelectSightTarget(distantProbe, center, 1)].Type);
        }

        private static void DestroyerProbePressurePrioritizesUnknownProbe()
        {
            var center = new Vec2(0f, 0f);
            var close = new List<TargetSnapshot>
            {
                SightTarget(135, 20f), SightTarget(139, 300f)
            };
            Equal(139, close[SelectSightTarget(close, center, 3)].Type);

            var crowded = new List<TargetSnapshot> { SightTarget(135, 20f) };
            crowded.Add(SightTarget(139, 500f));
            crowded.Add(SightTarget(139, 600f));
            crowded.Add(SightTarget(139, 700f));
            crowded.Add(SightTarget(139, 800f));
            Equal(139, crowded[SelectSightTarget(crowded, center, 3)].Type);

            var ignored = SightTarget(139, 100f);
            ignored.Invulnerable = true;
            var filtered = new List<TargetSnapshot> { SightTarget(135, 20f), ignored };
            Equal(135, filtered[SelectSightTarget(filtered, center, 3)].Type);
            ignored.Invulnerable = false;
            ignored.Chaseable = false;
            filtered[1] = ignored;
            Equal(135, filtered[SelectSightTarget(filtered, center, 1)].Type);
        }

        private static void DestroyerLosRoundRobinDoesNotStarveMovingChain()
        {
            var selector = new SightSelectorFixture();
            var targets = new List<TargetSnapshot>();
            for (var slot = 0; slot < 80; slot++)
            {
                var target = SightTarget(135, 40f + slot * 4f);
                target.Key = slot;
                targets.Add(target);
            }
            for (var probeIndex = 0; probeIndex < 4; probeIndex++)
            {
                var probe = SightTarget(139, 500f + probeIndex * 80f);
                probe.Key = 196 + probeIndex;
                targets.Add(probe);
            }
            var seen = new bool[80];
            var probeQueries = 0;

            // Expire every cached answer before every input frame. A nearest-only
            // selector would repeatedly query slots 0/1 and the reserved Probe.
            for (var frame = 0; frame < 100; frame++)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var expired = targets[i];
                    expired.LineOfSightKnown = false;
                    targets[i] = expired;
                }
                selector.BeginFrame();
                for (var budget = 3; budget >= 1; budget--)
                {
                    var selected = selector.Select(targets, new Vec2(), budget);
                    True(selected >= 0);
                    var target = targets[selected];
                    if (target.Type == 139) probeQueries++;
                    else seen[target.Key] = true;
                    target.LineOfSightKnown = true;
                    targets[selected] = target;
                }
            }

            for (var slot = 0; slot < seen.Length; slot++)
                True(seen[slot], "round-robin LOS never reached Destroyer slot " + slot);
            True(probeQueries >= 200,
                "Probe pressure must own the first query and retain the reserved final query");
        }

        private static TargetSnapshot SightTarget(int type, float centerX)
        {
            return new TargetSnapshot
            {
                Key = (int)centerX,
                Type = type,
                Position = new Vec2(centerX - 10f, -10f),
                Width = 20,
                Height = 20,
                Life = 100,
                Chaseable = true
            };
        }

        private static int SelectSightTarget(IList<TargetSnapshot> targets, Vec2 player, int budget)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.TargetSightQuerySelector", true);
            var method = type.GetMethod("Select", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(IList<TargetSnapshot>), typeof(Vec2), typeof(int) }, null);
            True(method != null);
            return (int)method.Invoke(null, new object[] { targets, player, budget });
        }

        private static bool TryReadDestroyerSlot(float value, int count, out int slot)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.DestroyerLinkIdentity", true);
            var method = type.GetMethod("TryReadSlot", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[] { value, count, -1 };
            var result = (bool)method.Invoke(null, arguments);
            slot = (int)arguments[2];
            return result;
        }

        private static bool TryReadDestroyerBranch(int typeId, bool trusted, float[] localAi, out bool worm)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.DestroyerBranchObservation", true);
            var method = type.GetMethod("TryReadHeadBranch", BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[] { typeId, trusted, localAi, false };
            var result = (bool)method.Invoke(null, arguments);
            worm = (bool)arguments[3];
            return result;
        }

        private sealed class SightSelectorFixture
        {
            private readonly Type _stateType;
            private readonly MethodInfo _beginFrame;
            private readonly MethodInfo _select;
            private object _state;

            public SightSelectorFixture()
            {
                var assembly = typeof(Chaite.Plugin.Runtime).Assembly;
                _stateType = assembly.GetType("Chaite.Plugin.TargetSightQueryState", true);
                _state = Activator.CreateInstance(_stateType);
                _beginFrame = _stateType.GetMethod("BeginFrame", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                var selector = assembly.GetType("Chaite.Plugin.TargetSightQuerySelector", true);
                _select = selector.GetMethod("Select", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(IList<TargetSnapshot>), typeof(Vec2), typeof(int), _stateType.MakeByRefType() }, null);
                True(_beginFrame != null && _select != null);
            }

            public void BeginFrame() => _beginFrame.Invoke(_state, null);

            public int Select(IList<TargetSnapshot> targets, Vec2 player, int budget)
            {
                var arguments = new object[] { targets, player, budget, _state };
                var selected = (int)_select.Invoke(null, arguments);
                _state = arguments[3];
                return selected;
            }
        }

        private struct DestroyerMotionResult
        {
            public bool Trusted;
            public Vec2 Velocity;
            public Vec2 SweepPosition;
            public int SweepWidth;
            public int SweepHeight;
        }

        private sealed class DestroyerMotionFixture
        {
            private readonly Type _type;
            private readonly Type _observationType;
            private readonly object _history;
            private readonly MethodInfo _observe;
            private readonly MethodInfo _invalidate;

            public DestroyerMotionFixture()
            {
                var assembly = typeof(Chaite.Plugin.Runtime).Assembly;
                _type = assembly.GetType("Chaite.Plugin.DestroyerMotionHistory", true);
                _observationType = assembly.GetType("Chaite.Plugin.DestroyerMotionObservation", true);
                _history = Activator.CreateInstance(_type, true);
                _observe = _type.GetMethod("Observe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _invalidate = _type.GetMethod("Invalidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                True(_observe != null && _invalidate != null);
            }

            public void Invalidate(int slot) => _invalidate.Invoke(_history, new object[] { slot });

            public void Clear() => _type.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic).Invoke(_history, null);

            public DestroyerMotionResult Observe(int slot, int entityKey, int type, int rootKey,
                int predecessorKey, bool connected, Vec2 position, int width, int height,
                Vec2 nativeVelocity, int frame, int life = 1000, int lifeMax = 1000)
            {
                var value = _observe.Invoke(_history, new object[] { slot, entityKey, type, rootKey,
                    predecessorKey, connected, position, width, height, nativeVelocity, frame, life, lifeMax });
                return new DestroyerMotionResult
                {
                    Trusted = Read<bool>(value, "TrustedMotion"),
                    Velocity = Read<Vec2>(value, "Velocity"),
                    SweepPosition = Read<Vec2>(value, "SweepPosition"),
                    SweepWidth = Read<int>(value, "SweepWidth"),
                    SweepHeight = Read<int>(value, "SweepHeight")
                };
            }

            private T Read<T>(object value, string name)
            {
                var field = _observationType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                True(field != null, "missing Destroyer observation field: " + name);
                return (T)field.GetValue(value);
            }
        }

        private sealed class DestroyerLinkNpc
        {
            public bool Active;
            public int Type;
            public int Who;
            public int Root;
            public int Life;
        }

        private sealed class DestroyerEquipmentPlayer
        {
            public readonly DestroyerEquipmentItem[] Slots = new DestroyerEquipmentItem[10];
        }

        private sealed class DestroyerEquipmentItem
        {
            public int Type;
            public sbyte WingSlot;
        }
    }
}
