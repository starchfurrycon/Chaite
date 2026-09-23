using Chaite.Core;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPriorityBossNativeCaptureRegressions()
        {
            Run(nameof(NativeTargetCapturePreservesEveryZeroSlot),
                NativeTargetCapturePreservesEveryZeroSlot);
            Run(nameof(NativeTargetCaptureClosesOnlyTheMalformedSlot),
                NativeTargetCaptureClosesOnlyTheMalformedSlot);
            Run(nameof(FishronBubbleTargetCaptureRequiresExactPlayerSlot),
                FishronBubbleTargetCaptureRequiresExactPlayerSlot);
            Run(nameof(EntityDirectionGetterAcceptsANativeNpcInstance),
                EntityDirectionGetterAcceptsANativeNpcInstance);
            Run(nameof(SkeletronHeadAndHandsAlwaysReceivePriorityCapture),
                SkeletronHeadAndHandsAlwaysReceivePriorityCapture);
            Run(nameof(PriorityWorldCaptureDistinguishesUninitializedWallBounds),
                PriorityWorldCaptureDistinguishesUninitializedWallBounds);
            Run(nameof(PriorityNpcCaptureKeepsBothWallEyesAndExactEnrageInputs),
                PriorityNpcCaptureKeepsBothWallEyesAndExactEnrageInputs);
            Run(nameof(PriorityProjectileCaptureKeepsEveryMoonLordEntity),
                PriorityProjectileCaptureKeepsEveryMoonLordEntity);
            Run(nameof(PriorityProjectileMalformedMetadataFailsClosed),
                PriorityProjectileMalformedMetadataFailsClosed);
            Run(nameof(PriorityThreatSourcesRequireCanonicalLiveBosses),
                PriorityThreatSourcesRequireCanonicalLiveBosses);
            Run(nameof(TargetThreatCaptureCallsEverySourceBoundGate),
                TargetThreatCaptureCallsEverySourceBoundGate);
            Run(nameof(NativeNeutralHoldDominatesEveryPlanAction),
                NativeNeutralHoldDominatesEveryPlanAction);
            Run(nameof(MoonLordMetadataIsCapturedBeforeHostileFiltering),
                MoonLordMetadataIsCapturedBeforeHostileFiltering);
            Run(nameof(ProjectileWindowFiltersHostileAndNotFriendly),
                ProjectileWindowFiltersHostileAndNotFriendly);
        }

        private static Type TerrariaFacadeType() =>
            typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);

        private static TargetSnapshot PopulateNativeTarget(int direction,
            int timeLeft, float[] ai, float[] localAi)
        {
            var type = TerrariaFacadeType();
            var method = type.GetMethod("PopulateNativeTargetFields",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[]
            {
                new TargetSnapshot(), direction, timeLeft, ai, localAi
            };
            method.Invoke(null, arguments);
            return (TargetSnapshot)arguments[0];
        }

        private static void NativeTargetCapturePreservesEveryZeroSlot()
        {
            var target = PopulateNativeTarget(0, 0,
                new[] { 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0f });
            True(target.NativeDirectionKnown);
            Equal(0, target.NativeDirection);
            True(target.NativeTimeLeftKnown);
            Equal(0, target.NativeTimeLeft);
            True(target.Ai0Known && target.Ai1Known && target.Ai2Known &&
                target.Ai3Known);
            True(target.LocalAi0Known && target.LocalAi1Known &&
                target.LocalAi2Known && target.LocalAi3Known);
            True(target.LocalAiKnown);
            Equal(0f, target.LocalAi0);
            Equal(0f, target.LocalAi1);
            Equal(0f, target.LocalAi2);
            Equal(0f, target.LocalAi3);
        }

        private static void NativeTargetCaptureClosesOnlyTheMalformedSlot()
        {
            var target = PopulateNativeTarget(2, -1,
                new[] { 1f, float.NaN, 0f },
                new[] { 0f, 2f, float.PositiveInfinity, 4f });
            False(target.NativeDirectionKnown);
            False(target.NativeTimeLeftKnown);
            True(target.Ai0Known);
            False(target.Ai1Known);
            True(target.Ai2Known);
            False(target.Ai3Known);
            True(target.LocalAi0Known && target.LocalAi1Known &&
                target.LocalAi3Known);
            False(target.LocalAi2Known);
            False(target.LocalAiKnown);
        }

        private static ThreatSnapshot PopulateProjectileNativeTarget(
            ThreatTrajectory trajectory, float[] ai)
        {
            var type = TerrariaFacadeType();
            var method = type.GetMethod("PopulateProjectileNativeTarget",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[]
            {
                new ThreatSnapshot { Trajectory = trajectory }, ai
            };
            method.Invoke(null, arguments);
            return (ThreatSnapshot)arguments[0];
        }

        private static void FishronBubbleTargetCaptureRequiresExactPlayerSlot()
        {
            // Fishron bubble AI_065 stores the target player slot one-based in
            // ai[1]; zero means the untargeted, outward-drifting phase.
            foreach (var slot in new[] { 0, 1, 254 })
            {
                var threat = PopulateProjectileNativeTarget(
                    ThreatTrajectory.UnmodeledDukeFishronHazard,
                    new[] { 0f, (float)slot + 1f });
                True(threat.NativeTargetPlayerKnown);
                Equal(slot, threat.NativeTargetPlayerIndex);
            }

            foreach (var malformed in new[]
            {
                (float[])null, Array.Empty<float>(), new[] { 0f, 0f },
                new[] { 0f, -1f }, new[] { 0f, 256f }, new[] { 0f, .5f },
                new[] { 0f, float.NaN }, new[] { 0f, float.PositiveInfinity }
            })
            {
                var threat = PopulateProjectileNativeTarget(
                    ThreatTrajectory.UnmodeledDukeFishronHazard, malformed);
                False(threat.NativeTargetPlayerKnown);
                Equal(-1, threat.NativeTargetPlayerIndex);
            }

            var ordinary = PopulateProjectileNativeTarget(
                ThreatTrajectory.Linear, new[] { 0f, 1f });
            False(ordinary.NativeTargetPlayerKnown);
            Equal(-1, ordinary.NativeTargetPlayerIndex);
        }

        private class NativeCaptureEntity
        {
            public int direction;
        }

        private sealed class NativeCaptureNpcEntity : NativeCaptureEntity
        {
        }

        private static void EntityDirectionGetterAcceptsANativeNpcInstance()
        {
            var getter = GenericAccessor<Func<object, int>>("Getter",
                typeof(int), typeof(NativeCaptureEntity), "direction");
            var npc = new NativeCaptureNpcEntity { direction = 0 };
            Equal(0, getter(npc));
            npc.direction = -1;
            Equal(-1, getter(npc));
        }

        private static void SkeletronHeadAndHandsAlwaysReceivePriorityCapture()
        {
            var method = TerrariaFacadeType().GetMethod(
                "IsPriorityBossNativeNpc",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            True((bool)method.Invoke(null, new object[] { 35 }),
                "Skeletron head must bypass the generic distance cull");
            True((bool)method.Invoke(null, new object[] { 36 }),
                "Skeletron hands must bypass the generic distance cull");
            False((bool)method.Invoke(null, new object[] { 37 }),
                "ordinary NPCs must not gain priority capture by adjacency");
        }

        private static void PopulatePriorityWorld(PriorityBossNativeContext value,
            double surface, int width, int top, int bottom)
        {
            var method = TerrariaFacadeType().GetMethod(
                "PopulatePriorityWorldContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            method.Invoke(null, new object[]
            {
                value, surface, width, top, bottom
            });
        }

        private static void PriorityWorldCaptureDistinguishesUninitializedWallBounds()
        {
            var value = new PriorityBossNativeContext();
            PopulatePriorityWorld(value, 250d, 8400, -1, -1);
            True(value.WorldGeometryKnown);
            False(value.WallOfFleshDrawAreaKnown);
            Equal(-1, value.WallOfFleshDrawAreaTopPixels);
            Equal(-1, value.WallOfFleshDrawAreaBottomPixels);

            PopulatePriorityWorld(value, 0d, 8400, 0, 160);
            False(value.WorldGeometryKnown);
            True(value.WallOfFleshDrawAreaKnown);

            PopulatePriorityWorld(value, double.NaN, 8400, 0, 160);
            False(value.WorldGeometryKnown);
            PopulatePriorityWorld(value, 250d, 0, 0, 160);
            False(value.WorldGeometryKnown);
        }

        private static TargetSnapshot NativeTarget(int key, int type)
        {
            var target = PopulateNativeTarget(0, 0,
                new[] { 0f, 0f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0f });
            target.Key = key;
            target.Type = type;
            target.Position = new Vec2(8000f, 500f);
            target.Width = 80;
            target.Height = 80;
            target.NativeTargetKnown = true;
            target.NativeTargetPlayerIndex = 0;
            target.LineOfSightKnown = true;
            target.HasLineOfSight = true;
            return target;
        }

        private static void ReadPriorityNpcs(CombatSnapshot snapshot)
        {
            var method = TerrariaFacadeType().GetMethod(
                "ReadPriorityBossNpcContext",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(method != null);
            method.Invoke(null, new object[] { snapshot });
        }

        private static void PriorityNpcCaptureKeepsBothWallEyesAndExactEnrageInputs()
        {
            var snapshot = new CombatSnapshot
            {
                NativeContextKnown = true,
                LocalPlayerIndex = 0
            };
            snapshot.Player.Position = new Vec2(8000f, 700f);
            snapshot.Player.ZoneJungleKnown = true;
            snapshot.Player.ZoneJungle = false;
            snapshot.Difficulty.ForTheWorthy = true;
            snapshot.Difficulty.DayTime = true;
            snapshot.Difficulty.Remix = true;
            PopulatePriorityWorld(snapshot.PriorityBoss, 100d, 8400,
                0, 160);

            snapshot.Targets.Add(NativeTarget(10, 222));
            snapshot.Targets.Add(NativeTarget(11, 113));
            var upperEye = NativeTarget(12, 114);
            upperEye.Ai0 = -1f;
            snapshot.Targets.Add(upperEye);
            var lowerEye = NativeTarget(13, 114);
            lowerEye.Ai0 = 1f;
            snapshot.Targets.Add(lowerEye);
            snapshot.Targets.Add(NativeTarget(14, 370));
            snapshot.Targets.Add(NativeTarget(16, 668));

            ReadPriorityNpcs(snapshot);
            Equal(1, snapshot.PriorityBoss.QueenBees.Count);
            var queen = snapshot.PriorityBoss.QueenBees[0];
            True(queen.Known);
            Equal(2.5f, queen.NativeEnrageFactor);
            Equal(1, snapshot.PriorityBoss.WallOfFleshTunnels.Count);
            True(snapshot.PriorityBoss.WallOfFleshTunnels[0].Known);
            Equal(2, snapshot.PriorityBoss.WallOfFleshEyes.Count);
            True(snapshot.PriorityBoss.WallOfFleshEyes[0].Known);
            True(snapshot.PriorityBoss.WallOfFleshEyes[1].Known);
            Equal(12, snapshot.PriorityBoss.WallOfFleshEyes[0].NpcKey);
            Equal(13, snapshot.PriorityBoss.WallOfFleshEyes[1].NpcKey);
            Equal(0f, snapshot.PriorityBoss.WallOfFleshEyes[0].
                LocalAi1ChargeTimer);
            Equal(0f, snapshot.PriorityBoss.WallOfFleshEyes[1].
                LocalAi2BurstStage);

            var fishron = snapshot.PriorityBoss.DukeFishrons[0];
            True(fishron.Known && fishron.NativeEnraged);
            True(fishron.PlayerAboveY800Band);
            True(fishron.PlayerInsideCentralHorizontalBand);
            var deer = snapshot.PriorityBoss.Deerclopses[0];
            True(deer.Known);
            Equal(0, deer.State);
            Equal(0f, deer.LocalAi3DistanceInvulnerabilityTimer);
        }

        private sealed class NativeCaptureNpc
        {
            public int Who;
            public int Type;
            public bool Active;
            public bool Boss;
            public bool Friendly;
            public int Life;
            public float PositionY;
            public int Height;
        }

        private sealed class NativeCaptureProjectile
        {
            public int Who;
            public float[] Ai;
            public float[] LocalAi;
            public int TimeLeft;
            public int Alpha;
            public int ExtraUpdates;
        }

        private sealed class PriorityProjectileFixture
        {
            private readonly Type _type = TerrariaFacadeType();
            private readonly object _facade;
            public readonly object[] Projectiles = new object[8];
            public readonly object[] Npcs = new object[8];

            public PriorityProjectileFixture()
            {
                _facade = FormatterServices.GetUninitializedObject(_type);
                Bind("_projectileAi", new Func<object, float[]>(value =>
                    ((NativeCaptureProjectile)value).Ai));
                Bind("_projectileLocalAi", new Func<object, float[]>(value =>
                    ((NativeCaptureProjectile)value).LocalAi));
                Bind("_projectileTimeLeft", new Func<object, int>(value =>
                    ((NativeCaptureProjectile)value).TimeLeft));
                Bind("_projectileAlpha", new Func<object, int>(value =>
                    ((NativeCaptureProjectile)value).Alpha));
                Bind("_projectileExtraUpdates", new Func<object, int>(value =>
                    ((NativeCaptureProjectile)value).ExtraUpdates));
                Bind("_whoAmI", new Func<object, int>(value =>
                    value is NativeCaptureProjectile
                        ? ((NativeCaptureProjectile)value).Who
                        : ((NativeCaptureNpc)value).Who));
                Bind("_npcActive", new Func<object, bool>(value =>
                    ((NativeCaptureNpc)value).Active));
                Bind("_npcTypeId", new Func<object, int>(value =>
                    ((NativeCaptureNpc)value).Type));
            }

            public void Read(int slot, int type,
                PriorityBossNativeContext context)
            {
                var method = _type.GetMethod(
                    "ReadPriorityMoonLordProjectile",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(method != null);
                method.Invoke(_facade, new object[]
                {
                    Projectiles, Npcs, slot, Projectiles[slot], type, context
                });
            }

            private void Bind(string name, object value)
            {
                var field = _type.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing projectile capture field " + name);
                field.SetValue(_facade, value);
            }
        }

        private static NativeCaptureProjectile MoonProjectile(int key,
            float ai0, float ai1, float local0 = 0f, float local1 = 0f)
        {
            return new NativeCaptureProjectile
            {
                Who = key,
                Ai = new[] { ai0, ai1 },
                LocalAi = new[] { local0, local1 },
                TimeLeft = 0,
                Alpha = 0,
                ExtraUpdates = 0
            };
        }

        private static void PriorityProjectileCaptureKeepsEveryMoonLordEntity()
        {
            var fixture = new PriorityProjectileFixture();
            fixture.Npcs[0] = new NativeCaptureNpc
            {
                Who = 0,
                Type = MoonLordProjectile456Observation.RequiredSourceNpcType,
                Active = true
            };
            fixture.Projectiles[0] = MoonProjectile(0, 0f, 0f);
            fixture.Projectiles[1] = MoonProjectile(1, -1f, -1f);
            fixture.Projectiles[2] = MoonProjectile(2, 1f, 0f);
            fixture.Projectiles[3] = MoonProjectile(3, -1f, 0f, 330f, 1f);
            var context = new PriorityBossNativeContext();
            fixture.Read(0, 454, context);
            fixture.Read(1, 454, context);
            fixture.Read(2, 456, context);
            fixture.Read(3, 456, context);

            Equal(2, context.MoonLordProjectiles454.Count);
            Equal(2, context.MoonLordProjectiles456.Count);
            var attached = context.MoonLordProjectiles454[0];
            True(attached.Known && attached.AttachedToSource);
            Equal(0, attached.ProjectileKey);
            Equal(0, attached.SourceNpcKey);
            False(attached.DamageEnabled);
            var detached = context.MoonLordProjectiles454[1];
            True(detached.Known && detached.Detached &&
                detached.DamageEnabled);

            var outbound = context.MoonLordProjectiles456[0];
            True(outbound.Known && outbound.HasLiveMoonLordSource);
            Equal(0, outbound.SourceNpcKey);
            Equal(0, outbound.TargetPlayerKey);
            False(outbound.Returning || outbound.ContactLatched);
            var returning = context.MoonLordProjectiles456[1];
            True(returning.Known && returning.Returning &&
                returning.ContactLatched &&
                returning.NativeReturnDeadlineReached);
        }

        private static void PriorityProjectileMalformedMetadataFailsClosed()
        {
            var fixture = new PriorityProjectileFixture();
            fixture.Projectiles[0] = MoonProjectile(0, 0f, 0f);
            fixture.Projectiles[1] = MoonProjectile(1, 1f, 0.5f);
            fixture.Projectiles[2] = MoonProjectile(99, 1f, 0f);
            fixture.Projectiles[3] = MoonProjectile(3, 1f, 0f);
            ((NativeCaptureProjectile)fixture.Projectiles[3]).LocalAi =
                new[] { 0f };
            var context = new PriorityBossNativeContext();
            fixture.Read(0, 456, context); // encoded source zero is malformed
            fixture.Read(1, 456, context); // fractional player key
            fixture.Read(2, 456, context); // whoAmI does not match slot
            fixture.Read(3, 456, context); // missing localAI[1]
            Equal(4, context.MoonLordProjectiles456.Count);
            for (var index = 0;
                index < context.MoonLordProjectiles456.Count; index++)
                False(context.MoonLordProjectiles456[index].Known);
        }

        private static bool ReadPriorityThreatSource(object[] npcs)
        {
            var facadeType = TerrariaFacadeType();
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            Action<string, object> bind = (name, value) =>
            {
                var field = facadeType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "missing threat source field " + name);
                field.SetValue(facade, value);
            };
            bind("_npcActive", new Func<object, bool>(value =>
                ((NativeCaptureNpc)value).Active));
            bind("_npcTypeId", new Func<object, int>(value =>
                ((NativeCaptureNpc)value).Type));
            bind("_whoAmI", new Func<object, int>(value =>
                ((NativeCaptureNpc)value).Who));
            bind("_npcBoss", new Func<object, bool>(value =>
                ((NativeCaptureNpc)value).Boss));
            bind("_npcFriendly", new Func<object, bool>(value =>
                ((NativeCaptureNpc)value).Friendly));
            bind("_npcLife", new Func<object, int>(value =>
                ((NativeCaptureNpc)value).Life));

            var method = facadeType.GetMethod("ReadSupportedThreatSources",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(method != null);
            var arguments = new object[] { npcs, false };
            method.Invoke(facade, arguments);
            return (bool)arguments[1];
        }

        private static NativeCaptureNpc ThreatSourceNpc(int who, int type)
        {
            return new NativeCaptureNpc
            {
                Who = who,
                Type = type,
                Active = true,
                Boss = true,
                Friendly = false,
                Life = 1
            };
        }

        private static void PriorityThreatSourcesRequireCanonicalLiveBosses()
        {
            var npcs = new object[4];
            npcs[1] = ThreatSourceNpc(1, 370);
            True(ReadPriorityThreatSource(npcs));

            var invalid = new[]
            {
                ThreatSourceNpc(1, 370),
                ThreatSourceNpc(1, 370),
                ThreatSourceNpc(1, 370),
                ThreatSourceNpc(2, 370),
                ThreatSourceNpc(1, 371),
                ThreatSourceNpc(1, 372),
                ThreatSourceNpc(1, 373),
                // The Empress of Light left the production scope, so her NPC
                // id must not authorize the Fishron threat family either.
                ThreatSourceNpc(1, 636)
            };
            invalid[0].Active = false;
            invalid[1].Friendly = true;
            invalid[2].Boss = false;
            invalid[3].Who = 2;
            for (var index = 0; index < invalid.Length; index++)
            {
                npcs = new object[4];
                npcs[1] = invalid[index];
                False(ReadPriorityThreatSource(npcs),
                    "invalid Fishron source " + index);
            }

            var dead = ThreatSourceNpc(1, 370);
            foreach (var life in new[] { 0, -1 })
            {
                dead.Life = life;
                npcs = new object[4];
                npcs[1] = dead;
                False(ReadPriorityThreatSource(npcs));
            }
            False(ReadPriorityThreatSource(null));
        }

        private static void TargetThreatCaptureCallsEverySourceBoundGate()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(
                typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var facade = FindCecilType(assembly,
                    "Chaite.Plugin.TerrariaFacade");
                var read = FindCecilMethod(facade,
                    "ReadTargetsAndThreats");
                var sourceScan = -1;
                var npcClassification = -1;
                var projectileClassification = -1;
                var projectileCapture = -1;
                for (var index = 0; index < read.Body.Instructions.Count;
                    index++)
                {
                    var call = read.Body.Instructions[index].Operand as
                        MethodReference;
                    if (call == null) continue;
                    if (call.Name == "ReadSupportedThreatSources")
                        sourceScan = index;
                    else if (call.Name == "SourceBoundNpcTrajectory")
                        npcClassification = index;
                    else if (call.Name == "SourceBoundProjectileTrajectory")
                        projectileClassification = index;
                    else if (call.Name == "ShouldCaptureProjectile")
                        projectileCapture = index;
                }
                True(sourceScan >= 0 && npcClassification > sourceScan,
                    "NPC threats were not classified after source discovery");
                True(projectileClassification > sourceScan &&
                    projectileCapture > projectileClassification,
                    "projectile source classification/capture ordering drifted");
            }
        }

        private static void NativeNeutralHoldDominatesEveryPlanAction()
        {
            var facadeType = TerrariaFacadeType();
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            var calls = 0;
            var sawTrue = false;
            var controls = new Dictionary<string, Action<object, bool>>
            {
                {
                    "sentinel",
                    (player, value) =>
                    {
                        calls++;
                        if (value) sawTrue = true;
                    }
                }
            };
            var controlsField = facadeType.GetField("_controls",
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(controlsField != null);
            controlsField.SetValue(facade, controls);
            var apply = facadeType.GetMethod("ApplyPlan",
                BindingFlags.Instance | BindingFlags.Public);
            True(apply != null);
            var plan = new ControlPlan
            {
                HoldNeutralControls = true,
                Horizontal = 1,
                Jump = true,
                Drop = true,
                Fire = true,
                QuickHeal = true,
                QuickMana = true,
                Dash = true,
                Hook = true,
                ToggleMount = true,
                GravityControl = 1,
                FeatherFallUp = true
            };
            var failure = apply.Invoke(facade, new object[] { new object(),
                plan });
            True(failure == null);
            Equal(1, calls);
            False(sawTrue,
                "a neutral hold applied a stale true control value");
        }

        private static void MoonLordMetadataIsCapturedBeforeHostileFiltering()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(
                typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var facade = FindCecilType(assembly,
                    "Chaite.Plugin.TerrariaFacade");

                var read = FindCecilMethod(facade,
                    "ReadTargetsAndThreats");
                var priorityCapture = -1;
                var hostileFilterAfterCapture = -1;
                for (var index = 0; index < read.Body.Instructions.Count;
                    index++)
                {
                    var call = read.Body.Instructions[index].Operand as
                        MethodReference;
                    if (call != null && call.Name ==
                        "ReadPriorityMoonLordProjectile")
                        priorityCapture = index;
                    var field = read.Body.Instructions[index].Operand as
                        FieldReference;
                    if (priorityCapture >= 0 && field != null &&
                        field.Name == "_projectileHostile")
                    {
                        hostileFilterAfterCapture = index;
                        break;
                    }
                }
                True(priorityCapture >= 0 &&
                    hostileFilterAfterCapture > priorityCapture,
                    "Moon Lord metadata must be captured before ordinary hostile/damage filtering");
            }
        }

        /// <summary>The projectile window's predicate is a PAIR of native flags.
        ///
        /// tools/GameProbe.cs CaptureHostileProjectiles keeps a projectile when
        ///     active &amp;&amp; hostile &amp;&amp; !friendly
        /// and the facade has to collect the same set, because the policy was
        /// trained on the probe's window. The facade instead tested `hostile`
        /// alone and skipped on true, which kept the NON-hostile projectiles and
        /// dropped every hostile one -- the exact complement of the training
        /// distribution, so in production the window was full of scenery and
        /// blind to the Boss's danmaku.
        ///
        /// Nothing caught it. The conformance test formats a window the probe
        /// hands it (ProjectilesFromBridgeRow) rather than collecting one from the
        /// game, so the collection predicate was never exercised; the field
        /// simply did not exist. This pins the half that was missing.
        /// </summary>
        private static void ProjectileWindowFiltersHostileAndNotFriendly()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(
                typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var facade = FindCecilType(assembly,
                    "Chaite.Plugin.TerrariaFacade");

                var bindsFriendly = false;
                foreach (var field in facade.Fields)
                    if (field.Name == "_projectileFriendly")
                        bindsFriendly = true;
                True(bindsFriendly,
                    "facade must bind a projectile `friendly` getter: the window " +
                    "predicate is `hostile && !friendly`, the same pair " +
                    "tools/GameProbe.cs CaptureHostileProjectiles uses");

                // The window is collected in BuildChaiteObservationRow, not in
                // ReadTargetsAndThreats: that one only records the nearest
                // projectile for the pre-hit diagnostic.
                var read = FindCecilMethod(facade, "BuildChaiteObservationRow");
                var seesHostile = false;
                var seesFriendly = false;
                foreach (var instruction in read.Body.Instructions)
                {
                    var field = instruction.Operand as FieldReference;
                    if (field == null) continue;
                    if (field.Name == "_projectileHostile") seesHostile = true;
                    if (field.Name == "_projectileFriendly") seesFriendly = true;
                }
                True(seesHostile && seesFriendly,
                    "the projectile window filter must consult BOTH flags; " +
                    "testing `hostile` alone keeps the complement of the " +
                    "training distribution");
            }
        }
    }
}
