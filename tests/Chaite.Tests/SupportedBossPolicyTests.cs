using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSupportedBossPolicyRegressions()
        {
            Run(nameof(SupportedBossAllowlistIsExact),
                SupportedBossAllowlistIsExact);
            Run(nameof(SupportedBossActiveRootsFailClosed),
                SupportedBossActiveRootsFailClosed);
            Run(nameof(SupportedBossRootIdentitiesAreStrict),
                SupportedBossRootIdentitiesAreStrict);
            Run(nameof(RuntimeAuthorizationBindsExactBossRoot),
                RuntimeAuthorizationBindsExactBossRoot);
            Run(nameof(RuntimeInvalidatesPendingReplayBeforeEarlyReturn),
                RuntimeInvalidatesPendingReplayBeforeEarlyReturn);
            Run(nameof(RuntimeAuthorizationBindsWorldAndConnection),
                RuntimeAuthorizationBindsWorldAndConnection);
            Run(nameof(RuntimeKillHookRejectsForeignBossIdentity),
                RuntimeKillHookRejectsForeignBossIdentity);
            Run(nameof(FacadeBossObservationPreservesEveryRoot),
                FacadeBossObservationPreservesEveryRoot);
            Run(nameof(SupportedBossSummonRoutesAreExact),
                SupportedBossSummonRoutesAreExact);
            Run(nameof(SupportedBossNativeSnapshotGate),
                SupportedBossNativeSnapshotGate);
            Run(nameof(SupportedPlannerWrappersRequireNativeContext),
                SupportedPlannerWrappersRequireNativeContext);
        }

        private static void SupportedBossAllowlistIsExact()
        {
            True(SupportedBossPolicy.IsSupportedBossType(
                SupportedBossPolicy.DukeFishronType));
            // The Empress of Light left the production scope, so her NPC id is
            // refused exactly like every other unreviewed Boss.
            False(SupportedBossPolicy.IsSupportedBossType(636));
            False(SupportedBossPolicy.IsKnownBossSummonItem(4961));
            False(SupportedBossPolicy.IsSupportedBossType(222));
            False(SupportedBossPolicy.IsSupportedBossType(4));
            False(SupportedBossPolicy.IsSupportedBossType(113));
            True(SupportedBossPolicy.IsEncounterBossRoot(13, false));
            True(SupportedBossPolicy.IsEncounterBossRoot(370, false));
            False(SupportedBossPolicy.IsEncounterBossRoot(222, false));
            Equal("这个波斯可是超囊的对我来说",
                SupportedBossPolicy.UnsupportedBossMessage);
        }

        private static void SupportedBossActiveRootsFailClosed()
        {
            int type;
            string reason;
            True(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int> { 370 }, out type, out reason));
            Equal(370, type);
            False(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int> { 636 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int>(), out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int> { 222 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int> { 370, 636 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossTypes(
                new List<int> { 370, 370 }, out type, out reason));
            True(!string.IsNullOrEmpty(reason));
        }

        private static void SupportedBossRootIdentitiesAreStrict()
        {
            int type;
            string reason;
            True(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int> { 1 },
                new List<int> { 370 }, out type,
                out reason));
            Equal(370, type);
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4, 9 }, new List<int> { 1, 1 },
                new List<int> { 370, 370 },
                out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int> { 1 },
                new List<int> { 370, 636 }, out type,
                out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int>(), new List<int>(), new List<int>(),
                out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int>(),
                new List<int> { 370 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int> { -1 },
                new List<int> { 370 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int> { 0 },
                new List<int> { 370 }, out type, out reason));
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                new List<int> { 4 }, new List<int> { 256 },
                new List<int> { 370 }, out type, out reason));
        }

        private static void RuntimeAuthorizationBindsExactBossRoot()
        {
            var runtime = typeof(Chaite.Plugin.Runtime);
            var typeField = runtime.GetField("_authorizedBossType",
                BindingFlags.Static | BindingFlags.NonPublic);
            var keyField = runtime.GetField("_authorizedBossKey",
                BindingFlags.Static | BindingFlags.NonPublic);
            var generationField = runtime.GetField(
                "_authorizedBossGeneration",
                BindingFlags.Static | BindingFlags.NonPublic);
            var observedField = runtime.GetField("_authorizedBossObserved",
                BindingFlags.Static | BindingFlags.NonPublic);
            var continuityField = runtime.GetField(
                "_authorizedBossContinuityBroken",
                BindingFlags.Static | BindingFlags.NonPublic);
            var validate = runtime.GetMethod("TryValidateAuthorizedBossScope",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(typeField != null && keyField != null &&
                generationField != null &&
                observedField != null && continuityField != null &&
                validate != null);

            var oldType = (int)typeField.GetValue(null);
            var oldKey = (int)keyField.GetValue(null);
            var oldGeneration = (int)generationField.GetValue(null);
            var oldObserved = (bool)observedField.GetValue(null);
            var oldContinuity = (bool)continuityField.GetValue(null);
            try
            {
                typeField.SetValue(null, 370);
                keyField.SetValue(null, -1);
                generationField.SetValue(null, -1);
                observedField.SetValue(null, false);
                continuityField.SetValue(null, false);
                var first = new EncounterObservation
                {
                    ActiveBossKeys = new[] { 0 },
                    ActiveBossGenerations = new[] { 7 },
                    ActiveBossTypes = new[] { 370 }
                };
                var arguments = new object[] { first, null };
                True((bool)validate.Invoke(null, arguments));
                Equal(0, (int)keyField.GetValue(null));

                arguments = new object[] { first, null };
                True((bool)validate.Invoke(null, arguments));
                var replacement = new EncounterObservation
                {
                    ActiveBossKeys = new[] { 1 },
                    ActiveBossGenerations = new[] { 7 },
                    ActiveBossTypes = new[] { 370 }
                };
                arguments = new object[] { replacement, null };
                False((bool)validate.Invoke(null, arguments));
                True(((string)arguments[1]).Contains(
                    SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, (int)keyField.GetValue(null));

                typeField.SetValue(null, 370);
                keyField.SetValue(null, -1);
                generationField.SetValue(null, -1);
                observedField.SetValue(null, false);
                continuityField.SetValue(null, false);
                arguments = new object[] { first, null };
                True((bool)validate.Invoke(null, arguments));
                var absent = new EncounterObservation();
                arguments = new object[] { absent, null };
                True((bool)validate.Invoke(null, arguments));
                True((bool)continuityField.GetValue(null));
                arguments = new object[] { first, null };
                False((bool)validate.Invoke(null, arguments));
                True(((string)arguments[1]).Contains(
                    SupportedBossPolicy.UnsupportedBossMessage));

                typeField.SetValue(null, 370);
                keyField.SetValue(null, -1);
                generationField.SetValue(null, -1);
                observedField.SetValue(null, false);
                continuityField.SetValue(null, false);
                arguments = new object[] { first, null };
                True((bool)validate.Invoke(null, arguments));
                var sameSlotReplacement = new EncounterObservation
                {
                    ActiveBossKeys = new[] { 0 },
                    ActiveBossGenerations = new[] { 8 },
                    ActiveBossTypes = new[] { 370 }
                };
                arguments = new object[] { sameSlotReplacement, null };
                False((bool)validate.Invoke(null, arguments));
                True(((string)arguments[1]).Contains(
                    SupportedBossPolicy.UnsupportedBossMessage));
            }
            finally
            {
                typeField.SetValue(null, oldType);
                keyField.SetValue(null, oldKey);
                generationField.SetValue(null, oldGeneration);
                observedField.SetValue(null, oldObserved);
                continuityField.SetValue(null, oldContinuity);
            }
        }

        private static void RuntimeInvalidatesPendingReplayBeforeEarlyReturn()
        {
            var runtime = typeof(Chaite.Plugin.Runtime);
            var pending = runtime.GetField("_pendingInput",
                BindingFlags.Static | BindingFlags.NonPublic);
            var applied = runtime.GetField("_frameApplied",
                BindingFlags.Static | BindingFlags.NonPublic);
            var player = runtime.GetField("_pendingPlayer",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(pending != null && applied != null && player != null);
            var oldPending = (bool)pending.GetValue(null);
            var oldApplied = (bool)applied.GetValue(null);
            var oldPlayer = player.GetValue(null);
            try
            {
                pending.SetValue(null, true);
                applied.SetValue(null, true);
                player.SetValue(null, new object());
                Chaite.Plugin.Runtime.Tick(null, -1);
                False((bool)pending.GetValue(null));
                False((bool)applied.GetValue(null));
                True(player.GetValue(null) == null);
            }
            finally
            {
                pending.SetValue(null, oldPending);
                applied.SetValue(null, oldApplied);
                player.SetValue(null, oldPlayer);
            }
        }

        private static void RuntimeAuthorizationBindsWorldAndConnection()
        {
            var runtime = typeof(Chaite.Plugin.Runtime);
            var facadeType = runtime.Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            var world = new ScopeWorld
            {
                UniqueId = Guid.NewGuid(),
                WorldId = 7123
            };
            var authorizedPlayer = new object();
            var netMode = 0;
            var localPlayer = 0;
            Action<string, object> bindFacade = (name, value) =>
            {
                var field = facadeType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "session fixture binding missing: " + name);
                field.SetValue(facade, value);
            };
            bindFacade("_activeWorldFileData",
                new Func<object>(() => world));
            bindFacade("_worldUniqueId",
                new Func<object, Guid>(value =>
                    ((ScopeWorld)value).UniqueId));
            bindFacade("_worldFileId",
                new Func<object, int>(value =>
                    ((ScopeWorld)value).WorldId));
            bindFacade("_netMode", new Func<int>(() => netMode));
            bindFacade("_myPlayer", new Func<int>(() => localPlayer));

            var gameField = runtime.GetField("_game",
                BindingFlags.Static | BindingFlags.NonPublic);
            var knownField = runtime.GetField(
                "_authorizedSessionIdentityKnown",
                BindingFlags.Static | BindingFlags.NonPublic);
            var worldTokenField = runtime.GetField("_authorizedWorldToken",
                BindingFlags.Static | BindingFlags.NonPublic);
            var playerTokenField = runtime.GetField("_authorizedPlayerToken",
                BindingFlags.Static | BindingFlags.NonPublic);
            var uniqueField = runtime.GetField("_authorizedWorldUniqueId",
                BindingFlags.Static | BindingFlags.NonPublic);
            var worldIdField = runtime.GetField("_authorizedWorldId",
                BindingFlags.Static | BindingFlags.NonPublic);
            var netModeField = runtime.GetField("_authorizedNetMode",
                BindingFlags.Static | BindingFlags.NonPublic);
            var playerField = runtime.GetField("_authorizedPlayerIndex",
                BindingFlags.Static | BindingFlags.NonPublic);
            var capture = runtime.GetMethod(
                "CaptureAuthorizedSessionIdentity",
                BindingFlags.Static | BindingFlags.NonPublic);
            var matches = runtime.GetMethod(
                "MatchesAuthorizedSessionIdentity",
                BindingFlags.Static | BindingFlags.NonPublic);
            True(gameField != null && knownField != null &&
                worldTokenField != null && playerTokenField != null &&
                uniqueField != null && worldIdField != null &&
                netModeField != null && playerField != null &&
                capture != null && matches != null);

            var oldGame = gameField.GetValue(null);
            var oldKnown = (bool)knownField.GetValue(null);
            var oldWorldToken = worldTokenField.GetValue(null);
            var oldPlayerToken = playerTokenField.GetValue(null);
            var oldUnique = (Guid)uniqueField.GetValue(null);
            var oldWorldId = (int)worldIdField.GetValue(null);
            var oldNetMode = (int)netModeField.GetValue(null);
            var oldPlayer = (int)playerField.GetValue(null);
            try
            {
                gameField.SetValue(null, facade);
                knownField.SetValue(null, false);
                var args = new object[] { authorizedPlayer, null };
                True((bool)capture.Invoke(null, args),
                    args[1] as string);
                True((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                True((bool)matches.Invoke(null, new object[] { null }));
                False((bool)matches.Invoke(null,
                    new object[] { new object() }));

                var originalUniqueId = world.UniqueId;
                world.UniqueId = Guid.NewGuid();
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                world.UniqueId = originalUniqueId;
                world.WorldId++;
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                world.WorldId--;
                netMode = 1;
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                netMode = 0;
                localPlayer = 1;
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                localPlayer = 0;
                localPlayer = 255;
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
                localPlayer = 0;
                world = new ScopeWorld
                {
                    UniqueId = originalUniqueId,
                    WorldId = 7123
                };
                False((bool)matches.Invoke(null,
                    new object[] { authorizedPlayer }));
            }
            finally
            {
                gameField.SetValue(null, oldGame);
                knownField.SetValue(null, oldKnown);
                worldTokenField.SetValue(null, oldWorldToken);
                playerTokenField.SetValue(null, oldPlayerToken);
                uniqueField.SetValue(null, oldUnique);
                worldIdField.SetValue(null, oldWorldId);
                netModeField.SetValue(null, oldNetMode);
                playerField.SetValue(null, oldPlayer);
            }
        }

        private static void FacadeBossObservationPreservesEveryRoot()
        {
            var facadeType = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            var npcs = new object[16];
            npcs[4] = new ScopeNpc { Active = true, Boss = true, Type = 370,
                Who = 4, RealLife = -1, Generation = 2 };
            npcs[9] = new ScopeNpc { Active = true, Boss = true, Type = 370,
                Who = 9, RealLife = -1, Generation = 3 };
            // Native Eater heads are Boss roots despite NPC.boss=false.
            npcs[12] = new ScopeNpc { Active = true, Boss = false, Type = 13,
                Who = 12, RealLife = -1, Generation = 4 };
            npcs[13] = new ScopeNpc { Active = true, Boss = true, Type = 439,
                Who = 13, RealLife = 13, Generation = 5 };
            npcs[14] = new ScopeNpc { Active = true, Boss = true, Type = 439,
                Who = 4, RealLife = -1, Generation = 6 };
            // A supported single-root type with a forwarded realLife identity
            // must remain visible as an invalid extra root, not collapse into 4.
            npcs[15] = new ScopeNpc { Active = true, Boss = true, Type = 370,
                Who = 15, RealLife = 4, Generation = 7 };
            Action<string, object> bind = (name, value) =>
            {
                var field = facadeType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "scope fixture binding missing: " + name);
                field.SetValue(facade, value);
            };
            bind("_npcs", new Func<object[]>(() => npcs));
            bind("_npcActive", new Func<object, bool>(value =>
                ((ScopeNpc)value).Active));
            bind("_npcBoss", new Func<object, bool>(value =>
                ((ScopeNpc)value).Boss));
            bind("_npcTypeId", new Func<object, int>(value =>
                ((ScopeNpc)value).Type));
            bind("_whoAmI", new Func<object, int>(value =>
                ((ScopeNpc)value).Who));
            bind("_npcRealLife", new Func<object, int>(value =>
                ((ScopeNpc)value).RealLife));
            bind("_npcGeneration", new Func<object, byte>(value =>
                ((ScopeNpc)value).Generation));
            bind("_playerDead", new Func<object, bool>(value => false));
            bind("_playerLife", new Func<object, int>(value => 400));
            bind("_observation", new EncounterObservation());
            bind("_activeBossKeys", new List<int>(4));
            bind("_activeBossGenerations", new List<int>(4));
            bind("_activeBossTypes", new List<int>(4));

            var method = facadeType.GetMethod("BuildObservation",
                BindingFlags.Instance | BindingFlags.Public);
            True(method != null);
            var observation = (EncounterObservation)method.Invoke(facade,
                new object[] { new object(), Array.Empty<int>(), false, true });
            Equal(6, observation.ActiveBossKeys.Count);
            Equal(6, observation.ActiveBossGenerations.Count);
            Equal(6, observation.ActiveBossTypes.Count);
            Equal(2, observation.ActiveBossGenerations[0]);
            Equal(3, observation.ActiveBossGenerations[1]);
            Equal(4, observation.ActiveBossGenerations[2]);
            Equal(370, observation.ActiveBossTypes[0]);
            Equal(370, observation.ActiveBossTypes[1]);
            Equal(13, observation.ActiveBossTypes[2]);
            Equal(13, observation.ActiveBossKeys[3]);
            // An unsupported root in its own array slot keeps its own
            // generation: the canonical-identity test only demands the
            // realLife/whoAmI agreement for supported types, precisely so that
            // an unsupported multi-part family is never collapsed.
            Equal(5, observation.ActiveBossGenerations[3]);
            Equal(439, observation.ActiveBossTypes[3]);
            Equal(14, observation.ActiveBossKeys[4]);
            Equal(-1, observation.ActiveBossGenerations[4]);
            Equal(439, observation.ActiveBossTypes[4]);
            Equal(15, observation.ActiveBossKeys[5]);
            Equal(-1, observation.ActiveBossGenerations[5]);
            Equal(370, observation.ActiveBossTypes[5]);
            int supportedType;
            string reason;
            False(SupportedBossPolicy.TryValidateActiveBossIdentities(
                observation.ActiveBossKeys,
                observation.ActiveBossGenerations,
                observation.ActiveBossTypes,
                out supportedType, out reason));
        }

        private static void RuntimeKillHookRejectsForeignBossIdentity()
        {
            var runtime = typeof(Chaite.Plugin.Runtime);
            var facadeType = runtime.Assembly.GetType(
                "Chaite.Plugin.TerrariaFacade", true);
            var facade = FormatterServices.GetUninitializedObject(facadeType);
            var world = new ScopeWorld
            {
                UniqueId = Guid.NewGuid(),
                WorldId = 9127
            };
            var npcs = new object[16];
            Action<string, object> bindFacade = (name, value) =>
            {
                var field = facadeType.GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null, "kill fixture binding missing: " + name);
                field.SetValue(facade, value);
            };
            bindFacade("_npcTypeId", new Func<object, int>(value =>
                ((ScopeNpc)value).Type));
            bindFacade("_npcBoss", new Func<object, bool>(value =>
                ((ScopeNpc)value).Boss));
            bindFacade("_npcRealLife", new Func<object, int>(value =>
                ((ScopeNpc)value).RealLife));
            bindFacade("_whoAmI", new Func<object, int>(value =>
                ((ScopeNpc)value).Who));
            bindFacade("_npcGeneration", new Func<object, byte>(value =>
                ((ScopeNpc)value).Generation));
            bindFacade("_npcs", new Func<object[]>(() => npcs));
            bindFacade("_activeWorldFileData", new Func<object>(() => world));
            bindFacade("_worldUniqueId", new Func<object, Guid>(value =>
                ((ScopeWorld)value).UniqueId));
            bindFacade("_worldFileId", new Func<object, int>(value =>
                ((ScopeWorld)value).WorldId));
            bindFacade("_netMode", new Func<int>(() => 0));
            bindFacade("_myPlayer", new Func<int>(() => 0));

            Func<string, FieldInfo> runtimeField = name =>
            {
                var field = runtime.GetField(name,
                    BindingFlags.Static | BindingFlags.NonPublic);
                True(field != null, "kill fixture Runtime field missing: " +
                    name);
                return field;
            };
            var fields = new[]
            {
                runtimeField("_initialized"), runtimeField("_faulted"),
                runtimeField("_game"), runtimeField("_encounter"),
                runtimeField("_pendingInput"), runtimeField("_frameApplied"),
                runtimeField("_pendingPlayer"),
                runtimeField("_pendingScopeRejection"),
                runtimeField("_authorizedSessionIdentityKnown"),
                runtimeField("_authorizedWorldToken"),
                runtimeField("_authorizedPlayerToken"),
                runtimeField("_authorizedWorldUniqueId"),
                runtimeField("_authorizedWorldId"),
                runtimeField("_authorizedNetMode"),
                runtimeField("_authorizedPlayerIndex"),
                runtimeField("_authorizedBossType"),
                runtimeField("_authorizedBossKey"),
                runtimeField("_authorizedBossGeneration"),
                runtimeField("_authorizedBossObserved"),
                runtimeField("_authorizedBossContinuityBroken"),
                runtimeField("_startPlan")
            };
            var oldValues = new Dictionary<FieldInfo, object>();
            foreach (var field in fields)
                oldValues.Add(field, field.GetValue(null));
            var killedField = runtimeField("PendingKilledBosses");
            var killed = (List<int>)killedField.GetValue(null);
            var oldKilled = killed.ToArray();
            var pendingMarker = new object();
            try
            {
                var encounter = new EncounterController(5);
                encounter.Activate(new EncounterObservation
                {
                    Flags = EncounterFlags.Boss,
                    PlayerLife = 400,
                    StartAuthorized = true,
                    ActiveBossKeys = new[] { 4 }
                });
                True(encounter.IsControlling);
                runtimeField("_initialized").SetValue(null, true);
                runtimeField("_faulted").SetValue(null, false);
                runtimeField("_game").SetValue(null, facade);
                runtimeField("_encounter").SetValue(null, encounter);
                runtimeField("_authorizedSessionIdentityKnown")
                    .SetValue(null, true);
                runtimeField("_authorizedWorldToken").SetValue(null, world);
                runtimeField("_authorizedPlayerToken")
                    .SetValue(null, new object());
                runtimeField("_authorizedWorldUniqueId")
                    .SetValue(null, world.UniqueId);
                runtimeField("_authorizedWorldId")
                    .SetValue(null, world.WorldId);
                runtimeField("_authorizedNetMode").SetValue(null, 0);
                runtimeField("_authorizedPlayerIndex").SetValue(null, 0);
                runtimeField("_authorizedBossType").SetValue(null, 370);
                runtimeField("_authorizedBossKey").SetValue(null, 4);
                runtimeField("_authorizedBossGeneration").SetValue(null, 2);
                runtimeField("_authorizedBossObserved").SetValue(null, true);
                runtimeField("_authorizedBossContinuityBroken")
                    .SetValue(null, false);
                runtimeField("_startPlan").SetValue(null, null);
                killed.Clear();

                Action armPendingReplay = () =>
                {
                    runtimeField("_pendingInput").SetValue(null, true);
                    runtimeField("_frameApplied").SetValue(null, true);
                    runtimeField("_pendingPlayer")
                        .SetValue(null, pendingMarker);
                    runtimeField("_pendingScopeRejection")
                        .SetValue(null, null);
                };

                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(new ScopeNpc
                {
                    Type = 1, Boss = false, Who = 3, RealLife = -1,
                    Generation = 1
                });
                True((bool)runtimeField("_pendingInput").GetValue(null));
                True(runtimeField("_pendingScopeRejection")
                    .GetValue(null) == null);

                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(new ScopeNpc
                {
                    Type = 13, Boss = false, Who = 5, RealLife = -1,
                    Generation = 1
                });
                False((bool)runtimeField("_pendingInput").GetValue(null));
                False((bool)runtimeField("_frameApplied").GetValue(null));
                True(runtimeField("_pendingPlayer").GetValue(null) == null);
                True(((string)runtimeField("_pendingScopeRejection")
                    .GetValue(null)).Contains(
                        SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, killed.Count);

                var fishron = new ScopeNpc
                {
                    Type = 370, Boss = true, Who = 4, RealLife = -1,
                    Generation = 2
                };
                npcs[4] = fishron;
                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(fishron);
                True((bool)runtimeField("_pendingInput").GetValue(null));
                True(runtimeField("_pendingScopeRejection")
                    .GetValue(null) == null);
                Equal(1, killed.Count);
                Equal(4, killed[0]);

                killed.Clear();
                runtimeField("_authorizedBossKey").SetValue(null, 5);
                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(fishron);
                False((bool)runtimeField("_pendingInput").GetValue(null));
                True(((string)runtimeField("_pendingScopeRejection")
                    .GetValue(null)).Contains(
                        SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, killed.Count);
                runtimeField("_authorizedBossKey").SetValue(null, 4);

                runtimeField("_authorizedBossGeneration").SetValue(null, 3);
                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(fishron);
                False((bool)runtimeField("_pendingInput").GetValue(null));
                True(((string)runtimeField("_pendingScopeRejection")
                    .GetValue(null)).Contains(
                        SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, killed.Count);
                runtimeField("_authorizedBossGeneration").SetValue(null, 2);

                runtimeField("_authorizedWorldToken")
                    .SetValue(null, new object());
                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(fishron);
                False((bool)runtimeField("_pendingInput").GetValue(null));
                True(((string)runtimeField("_pendingScopeRejection")
                    .GetValue(null)).Contains(
                        SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, killed.Count);
                runtimeField("_authorizedWorldToken").SetValue(null, world);

                bindFacade("_npcTypeId", new Func<object, int>(value =>
                    { throw new InvalidOperationException("fixture failure"); }));
                armPendingReplay();
                Chaite.Plugin.Runtime.OnNpcKilled(fishron);
                False((bool)runtimeField("_pendingInput").GetValue(null));
                False((bool)runtimeField("_frameApplied").GetValue(null));
                True(runtimeField("_pendingPlayer").GetValue(null) == null);
                True(((string)runtimeField("_pendingScopeRejection")
                    .GetValue(null)).Contains(
                        SupportedBossPolicy.UnsupportedBossMessage));
                Equal(0, killed.Count);
            }
            finally
            {
                foreach (var entry in oldValues)
                    entry.Key.SetValue(null, entry.Value);
                killed.Clear();
                killed.AddRange(oldKilled);
            }
        }

        private static void SupportedBossSummonRoutesAreExact()
        {
            string reason;
            var fishron = ProductionFishronStartPlan();
            True(fishron != null);
            True(SupportedBossPolicy.TryValidateStartPlan(fishron,
                out reason), reason);

            True(fishron.TrySetAdmittedCombatWeaponSlot(0));
            True(SupportedBossPolicy.TryValidateStartPlan(fishron,
                out reason), reason);
            fishron.CombatWeaponSlot = 1;
            RejectedStartPlan(fishron);

            var forged = ProductionFishronStartPlan();
            forged.SummonSlot = 2;
            RejectedStartPlan(forged);
            forged = ProductionFishronStartPlan();
            forged.ActionSlot = 5;
            RejectedStartPlan(forged);
            forged = ProductionFishronStartPlan();
            forged.TimeoutTicks = 901;
            RejectedStartPlan(forged);
            forged = ProductionFishronStartPlan();
            forged.InteractionWorld = new Vec2(120f, 220f);
            RejectedStartPlan(forged);
            forged = ProductionFishronStartPlan();
            forged.CombatWeaponSlot = 2;
            RejectedStartPlan(forged);

            False(SupportedBossPolicy.TryValidateStartPlan(
                new BossStartPlan
                {
                    Kind = BossSummonKind.DirectItem,
                    ItemType = 222,
                    ExpectedBossType = 222,
                    Id = "abeemination"
                }, out reason));
            False(SupportedBossPolicy.TryValidateStartPlan(
                new BossStartPlan
                {
                    Kind = BossSummonKind.DirectItem,
                    ItemType = 2673,
                    ExpectedBossType = 370,
                    Id = "forged-fishron"
                }, out reason));
        }

        private static BossStartPlan ProductionFishronStartPlan()
        {
            var context = new BossStartContext
            {
                ZoneBeach = true,
                OceanWater = true,
                FishingRodHotbarSlot = 4,
                OceanWaterWorld = new Vec2(100f, 200f)
            };
            context.Hotbar.Add(new HotbarItemSnapshot
                { Slot = 1, Type = 2673, Stack = 1 });
            context.Hotbar.Add(new HotbarItemSnapshot
                { Slot = 4, Type = 2291, Stack = 1 });
            return BossStartPlanner.SelectProduction(context);
        }

        private static void RejectedStartPlan(BossStartPlan plan)
        {
            string reason;
            False(SupportedBossPolicy.TryValidateStartPlan(plan, out reason));
            True(!string.IsNullOrEmpty(reason));
        }

        private static void SupportedBossNativeSnapshotGate()
        {
            var fishron = new CombatSnapshot { NativeContextKnown = true };
            fishron.Targets.Add(new TargetSnapshot
            {
                Type = 370,
                Boss = true,
                Life = 100,
                LifeMax = 100
            });
            string reason;
            True(SupportedBossPolicy.IsSupportedNativeSnapshot(
                fishron, out reason));

            var unknownContext = new CombatSnapshot();
            unknownContext.Targets.Add(new TargetSnapshot
            {
                Type = 370,
                Boss = true,
                Life = 100,
                LifeMax = 100
            });
            False(SupportedBossPolicy.IsSupportedNativeSnapshot(
                unknownContext, out reason));

            var unsupported = new CombatSnapshot { NativeContextKnown = true };
            unsupported.Targets.Add(new TargetSnapshot
            {
                Type = 222,
                Boss = true,
                Life = 100,
                LifeMax = 100
            });
            False(SupportedBossPolicy.IsSupportedNativeSnapshot(
                unsupported, out reason));
            True(!string.IsNullOrEmpty(reason));

            var ordinaryOnly = new CombatSnapshot { NativeContextKnown = true };
            ordinaryOnly.Targets.Add(new TargetSnapshot
            {
                Type = 222,
                Boss = false,
                Life = 100,
                LifeMax = 100
            });
            False(SupportedBossPolicy.IsSupportedNativeSnapshot(
                ordinaryOnly, out reason));

            var mixedEater = new CombatSnapshot { NativeContextKnown = true };
            mixedEater.Targets.Add(new TargetSnapshot
            {
                Type = 370, Boss = true, Life = 100, LifeMax = 100
            });
            mixedEater.Targets.Add(new TargetSnapshot
            {
                Type = 13, Boss = false, Life = 100, LifeMax = 100
            });
            False(SupportedBossPolicy.IsSupportedNativeSnapshot(
                mixedEater, out reason));

            var duplicateFishron = new CombatSnapshot
                { NativeContextKnown = true };
            duplicateFishron.Targets.Add(new TargetSnapshot
            {
                Key = 1, Type = 370, Boss = true, Life = 100, LifeMax = 100
            });
            duplicateFishron.Targets.Add(new TargetSnapshot
            {
                Key = 2, Type = 370, Boss = true, Life = 100, LifeMax = 100
            });
            False(SupportedBossPolicy.IsSupportedNativeSnapshot(
                duplicateFishron, out reason));
        }

        private static void SupportedPlannerWrappersRequireNativeContext()
        {
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            Equal(ActiveEncounterPreparationResult.Rejected,
                planner.PrepareForSupportedActiveEncounterDetailed(
                    new CombatSnapshot(), out reason));
            True(!string.IsNullOrEmpty(reason));

            var plan = planner.PlanSupported(new CombatSnapshot(), out reason);
            True(plan.RequestControlReturn);
            Equal("unsupported-boss-allowlist", plan.StrategyId);
            True(!string.IsNullOrEmpty(reason));
        }

        private sealed class ScopeNpc
        {
            public bool Active;
            public bool Boss;
            public int Type;
            public int Who;
            public int RealLife;
            public byte Generation;
        }

        private sealed class ScopeWorld
        {
            public Guid UniqueId;
            public int WorldId;
        }
    }
}
