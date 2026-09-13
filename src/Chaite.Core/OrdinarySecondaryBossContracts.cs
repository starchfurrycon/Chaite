using System;

namespace Chaite.Core
{
    /// <summary>
    /// Native contracts for the ordinary (non-secret) Plantera and Golem
    /// branches.  The contracts intentionally keep the world/difficulty gate
    /// separate from the component scan so a missing source can never be
    /// mistaken for a native zero.  They are used by both the pre-spawn
    /// requirements path and the live strategy path.
    /// </summary>
    internal static class PlanteraOrdinaryContract
    {
        private const int RootType = 262;
        private const int HookType = 263;
        private const int TentacleType = 264;
        private const int SporeType = 265;

        public static bool IsSupported(CombatSnapshot snapshot,
            bool allowPreSpawn, out TargetSnapshot root, out bool second,
            out string reason)
        {
            root = default(TargetSnapshot);
            second = false;
            if (!ValidateWorld(snapshot, out reason)) return false;

            var rootCount = 0;
            var hooks = 0;
            var tentacles = 0;
            var spores = 0;
            var rootAttached = 0;
            var hookKeys = new int[3];
            for (var i = 0; i < hookKeys.Length; i++) hookKeys[i] = -1;

            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Life <= 0) continue;
                if (candidate.Boss && !IsPlanteraType(candidate.Type))
                    return Invalid("Plantera encounter contains another active Boss", out reason);
                if (candidate.Type == RootType)
                {
                    rootCount++;
                    if (rootCount == 1) root = candidate;
                    continue;
                }
                if (candidate.Type == HookType)
                {
                    hooks++;
                    if (hooks <= hookKeys.Length) hookKeys[hooks - 1] = candidate.Key;
                    continue;
                }
                if (candidate.Type == TentacleType) { tentacles++; continue; }
                if (candidate.Type == SporeType) { spores++; continue; }
            }

            if (rootCount == 0 && hooks == 0 && tentacles == 0 && spores == 0)
            {
                if (allowPreSpawn) { reason = null; return true; }
                return Invalid("Plantera root is not observed", out reason);
            }
            if (rootCount != 1)
                return Invalid("Plantera requires one unique live root", out reason);
            if (!ValidRoot(snapshot, root, out second, out reason)) return false;
            if (hooks != 3)
                return Invalid("Plantera requires exactly three live hook anchors", out reason);

            var maxTentacles = snapshot.Difficulty.Expert || snapshot.Difficulty.Master ? 25 : 8;
            if (tentacles > maxTentacles || rootAttached > 8)
                return Invalid("Plantera tentacle population exceeds the reviewed difficulty bound", out reason);

            // Scan and validate each component after the inexpensive count pass.
            var seenHooks = 0;
            var seenTentacles = 0;
            var seenSpores = 0;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Life <= 0) continue;
                if (candidate.Type == HookType)
                {
                    seenHooks++;
                    if (!ValidateHook(snapshot, root, candidate, hookKeys,
                            seenHooks, out reason)) return false;
                }
                else if (candidate.Type == TentacleType)
                {
                    seenTentacles++;
                    if (!ValidateTentacle(snapshot, root, candidate, second,
                            hookKeys, out reason)) return false;
                    if (NativeInteger(candidate.Ai3, 0, int.MaxValue) == 0)
                        rootAttached++;
                }
                else if (candidate.Type == SporeType)
                {
                    seenSpores++;
                    if (!ValidateSpore(candidate, out reason)) return false;
                }
            }
            if (seenHooks != hooks || seenTentacles != tentacles ||
                seenSpores != spores || rootAttached > 8)
                return Invalid("Plantera component population changed during validation", out reason);
            if (!second && tentacles != 0)
                return Invalid("Plantera tentacles are only valid after the phase transition", out reason);
            if (!ValidateProjectiles(snapshot, out reason)) return false;
            reason = null;
            return true;
        }

        private static bool ValidateWorld(CombatSnapshot snapshot,
            out string reason)
        {
            if (!ClassicSecondaryBossContract.TryValidateOrdinaryDifficultyFixture(
                    snapshot, out reason)) return false;
            if (snapshot.PriorityBoss == null ||
                !snapshot.PriorityBoss.WorldGeometryKnown ||
                !snapshot.PriorityBoss.UnderworldLayerKnown ||
                double.IsNaN(snapshot.PriorityBoss.WorldSurfaceTiles) ||
                double.IsInfinity(snapshot.PriorityBoss.WorldSurfaceTiles) ||
                snapshot.PriorityBoss.WorldSurfaceTiles <= 0d ||
                snapshot.PriorityBoss.WorldWidthTiles <= 0 ||
                snapshot.PriorityBoss.UnderworldLayerTiles <=
                    snapshot.PriorityBoss.WorldSurfaceTiles)
                return Invalid("Plantera world geometry is unavailable", out reason);
            var p = snapshot.Player;
            if (!p.ZoneJungleKnown || !p.ZoneJungle ||
                !Finite(p.Position) || p.Width <= 0 || p.Height <= 0 ||
                p.Position.Y < snapshot.PriorityBoss.WorldSurfaceTiles * 16d ||
                p.Position.Y > snapshot.PriorityBoss.UnderworldLayerTiles * 16d)
                return Invalid("Plantera target is outside the native Jungle depth band", out reason);
            reason = null;
            return true;
        }

        private static bool ValidRoot(CombatSnapshot snapshot,
            TargetSnapshot root, out bool second, out string reason)
        {
            second = false;
            if (!ValidEntity(root) || root.Type != RootType ||
                !ClassicSecondaryBossContract.TargetsLocalPlayer(snapshot, root) ||
                !ClassicSecondaryBossContract.HasAllAi(root) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(root) ||
                root.Ai0 != 0f || root.Ai1 != 0f || root.Ai2 != 0f ||
                root.Ai3 != 0f || root.LocalAi3 != 0f || root.Invulnerable)
                return Invalid("Plantera root native state is malformed", out reason);
            second = root.LifeMax > 0 && root.Life <= root.LifeMax / 2;
            if (NativeInteger(root.LocalAi0, 1, 2) == int.MinValue ||
                NativeInteger(root.LocalAi1, -120, 350) == int.MinValue ||
                root.LocalAi2 != 0f && root.LocalAi2 != 1f)
                return Invalid("Plantera phase and local timers are malformed", out reason);
            if (!second && root.LocalAi0 != 1f)
                return Invalid("Plantera phase one has an invalid local phase", out reason);
            reason = null;
            return true;
        }

        private static bool ValidateHook(CombatSnapshot snapshot,
            TargetSnapshot root, TargetSnapshot hook, int[] hookKeys,
            int ordinal, out string reason)
        {
            if (!ValidEntity(hook) || hook.Type != HookType || !hook.Invulnerable ||
                !ClassicSecondaryBossContract.HasAllAi(hook) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(hook) ||
                !NativeTile(hook.Ai0, hook.Ai1, snapshot) ||
                hook.Ai2 != 0f || hook.Ai3 != 0f || DuplicateKey(hook.Key,
                    hookKeys, ordinal - 1))
                return Invalid("Plantera hook anchor is incomplete or malformed", out reason);
            if (hook.NativeRealLifeKnown && hook.NativeRealLife >= 0 &&
                hook.NativeRealLife != root.Key)
                return Invalid("Plantera hook anchor has the wrong native parent", out reason);
            reason = null;
            return true;
        }

        private static bool ValidateTentacle(CombatSnapshot snapshot,
            TargetSnapshot root, TargetSnapshot tentacle, bool second,
            int[] hookKeys, out string reason)
        {
            if (!second || !ValidEntity(tentacle) || tentacle.Type != TentacleType ||
                !ClassicSecondaryBossContract.HasAllAi(tentacle) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(tentacle) ||
                NativeInteger(tentacle.Ai0, -100, 100) == int.MinValue ||
                NativeInteger(tentacle.Ai1, -100, 100) == int.MinValue || tentacle.Ai2 != 0f ||
                NativeInteger(tentacle.Ai3, 0, int.MaxValue) == int.MinValue ||
                !Finite(tentacle.LocalAi0) || tentacle.LocalAi0 < -8f ||
                tentacle.LocalAi0 > 600f || tentacle.LocalAi1 != 0f ||
                tentacle.LocalAi2 != 0f || tentacle.LocalAi3 != 0f)
                return Invalid("Plantera tentacle native state is malformed", out reason);
            var parent = NativeInteger(tentacle.Ai3, 0, int.MaxValue);
            if (parent > 0 && !ContainsKey(hookKeys, parent - 1))
                return Invalid("Plantera tentacle hook parent is not live", out reason);
            if (tentacle.NativeRealLifeKnown && tentacle.NativeRealLife >= 0 &&
                tentacle.NativeRealLife != root.Key)
                return Invalid("Plantera tentacle has the wrong native parent", out reason);
            reason = null;
            return true;
        }

        private static bool ValidateSpore(TargetSnapshot spore, out string reason)
        {
            if (!ValidEntity(spore) || spore.Type != SporeType ||
                !ClassicSecondaryBossContract.HasAllAi(spore) ||
                spore.Ai0 != 0f || spore.Ai1 != 0f || spore.Ai2 != 0f ||
                spore.Ai3 != 0f || !Finite(spore.Velocity) ||
                spore.Velocity.LengthSquared > 256f * 256f)
                return Invalid("Plantera spore NPC state is malformed", out reason);
            reason = null;
            return true;
        }

        private static bool ValidateProjectiles(CombatSnapshot snapshot,
            out string reason)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Type != 275 && threat.Type != 276 && threat.Type != 277)
                    continue;
                if (threat.Kind != ThreatKind.Projectile || !Finite(threat.Position) ||
                    !Finite(threat.Velocity) || threat.Width < 0 ||
                    threat.Height < 0 || threat.TimeLeft < 0)
                    return Invalid("Plantera seed projectile observation is malformed", out reason);
            }
            reason = null;
            return true;
        }

        private static bool IsPlanteraType(int type) => type >= 262 && type <= 265;

        private static bool ValidEntity(TargetSnapshot value)
        {
            return value.Key >= 0 && value.Life > 0 && value.LifeMax >= value.Life &&
                value.Width > 0 && value.Height > 0 && Finite(value.Position) &&
                Finite(value.Velocity);
        }

        private static bool NativeTile(float x, float y, CombatSnapshot snapshot)
        {
            return NativeInteger(x, 1, snapshot.PriorityBoss.WorldWidthTiles - 1) != int.MinValue &&
                NativeInteger(y, 1, (int)Math.Max(1d, snapshot.Player.WorldBottom / 16f)) != int.MinValue;
        }

        private static bool DuplicateKey(int key, int[] values, int count)
        {
            for (var i = 0; i < count; i++) if (values[i] == key) return true;
            return false;
        }

        private static bool ContainsKey(int[] values, int key)
        {
            for (var i = 0; i < values.Length; i++) if (values[i] == key) return true;
            return false;
        }

        private static int NativeInteger(float value, int minimum, int maximum)
        {
            return ClassicSecondaryBossContract.IsExactInt(value, minimum, maximum)
                ? (int)Math.Round(value) : int.MinValue;
        }

        private static bool Finite(float value) => ClassicSecondaryBossContract.Finite(value);
        private static bool Finite(Vec2 value) => ClassicSecondaryBossContract.Finite(value);
        private static bool Invalid(string value, out string reason)
        {
            reason = value;
            return false;
        }
    }

    internal static class GolemOrdinaryContract
    {
        private const int BodyType = 245;
        private const int AttachedHeadType = 246;
        private const int LeftFistType = 247;
        private const int RightFistType = 248;
        private const int DetachedHeadType = 249;

        public static bool IsSupported(CombatSnapshot snapshot,
            bool allowPreSpawn, out TargetSnapshot body, out bool detached,
            out TargetSnapshot threateningFist, out bool hasThreateningFist,
            out string reason)
        {
            body = default(TargetSnapshot);
            detached = false;
            threateningFist = default(TargetSnapshot);
            hasThreateningFist = false;
            if (!ValidateWorld(snapshot, out reason)) return false;

            var bodyCount = 0;
            var attached = 0;
            var detachedCount = 0;
            var left = 0;
            var right = 0;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Life <= 0) continue;
                if (candidate.Boss && !IsGolemType(candidate.Type))
                    return Invalid("Golem encounter contains another active Boss", out reason);
                switch (candidate.Type)
                {
                    case BodyType: bodyCount++; if (bodyCount == 1) body = candidate; break;
                    case AttachedHeadType: attached++; break;
                    case DetachedHeadType: detachedCount++; break;
                    case LeftFistType: left++; break;
                    case RightFistType: right++; break;
                }
            }
            if (bodyCount == 0 && attached == 0 && detachedCount == 0 &&
                left == 0 && right == 0)
            {
                if (allowPreSpawn) { reason = null; return true; }
                return Invalid("Golem body is not observed", out reason);
            }
            if (bodyCount != 1 || left != 1 || right != 1 ||
                attached + detachedCount != 1)
                return Invalid("Golem requires one body, two fists, and one mutually-exclusive head", out reason);
            if (!ValidBody(snapshot, body, attached == 1, out reason)) return false;
            detached = detachedCount == 1;

            var bestScore = float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var part = snapshot.Targets[i];
                if (part.Life <= 0 || part.Type < AttachedHeadType ||
                    part.Type > DetachedHeadType) continue;
                if (!ValidatePart(snapshot, body, part, detached, out reason)) return false;
                if (part.Type == LeftFistType || part.Type == RightFistType)
                {
                    var state = NativeInteger(part.Ai0, 0, 2);
                    if (state != 0)
                    {
                        var horizon = state == 2 ? 8f : 1f;
                        var predicted = part.Center + part.Velocity * horizon;
                        var score = Vec2.DistanceSquared(predicted,
                            snapshot.Player.Center) - (state == 2 ? 32400f : 0f);
                        if (!hasThreateningFist || score < bestScore)
                        {
                            bestScore = score;
                            threateningFist = part;
                            hasThreateningFist = true;
                        }
                    }
                }
            }
            if (!ValidateProjectiles(snapshot, out reason)) return false;
            reason = null;
            return true;
        }

        private static bool ValidateWorld(CombatSnapshot snapshot,
            out string reason)
        {
            if (!ClassicSecondaryBossContract.TryValidateOrdinaryDifficultyFixture(
                    snapshot, out reason)) return false;
            if (snapshot.PriorityBoss == null ||
                !snapshot.PriorityBoss.WorldGeometryKnown ||
                double.IsNaN(snapshot.PriorityBoss.WorldSurfaceTiles) ||
                double.IsInfinity(snapshot.PriorityBoss.WorldSurfaceTiles) ||
                snapshot.PriorityBoss.WorldSurfaceTiles <= 0d)
                return Invalid("Golem world geometry is unavailable", out reason);
            var p = snapshot.Player;
            var inJungle = p.ZoneJungleKnown && p.ZoneJungle;
            var inTemple = p.ZoneLihzhardTempleKnown && p.ZoneLihzhardTemple;
            if (!inJungle && !inTemple || !Finite(p.Center) ||
                p.Center.Y < snapshot.PriorityBoss.WorldSurfaceTiles * 16d)
                return Invalid("Golem target is outside the native temple/Jungle depth band", out reason);
            if (!TempleArenaEvidence(snapshot.Arena, p))
                return Invalid("Golem requires observed multi-platform temple-room support", out reason);
            reason = null;
            return true;
        }

        private static bool ValidBody(CombatSnapshot snapshot, TargetSnapshot body,
            bool attached, out string reason)
        {
            if (!ValidEntity(body) || body.Type != BodyType ||
                !ClassicSecondaryBossContract.TargetsLocalPlayer(snapshot, body) ||
                !ClassicSecondaryBossContract.HasAllAi(body) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(body) ||
                body.LocalAi0 != 1f || !Finite(body.LocalAi1) ||
                !Finite(body.LocalAi2) || !Finite(body.LocalAi3) ||
                body.Ai2 != 0f || body.Ai3 != 0f)
                return Invalid("Golem body native state is malformed", out reason);
            var state = NativeInteger(body.Ai0, 0, 1);
            if (state == int.MinValue ||
                state == 1 && body.Ai1 != 0f ||
                state == 0 && NativeInteger(body.Ai1, -20, 299) ==
                    int.MinValue)
                return Invalid("Golem body phase/timer is malformed", out reason);
            if (body.Invulnerable != attached)
                return Invalid("Golem body vulnerability does not match its active head", out reason);
            reason = null;
            return true;
        }

        private static bool ValidatePart(CombatSnapshot snapshot,
            TargetSnapshot body, TargetSnapshot part, bool detached,
            out string reason)
        {
            if (!ValidEntity(part) || !ClassicSecondaryBossContract.HasAllAi(part) ||
                !ClassicSecondaryBossContract.HasAllLocalAi(part))
                return Invalid("Golem component native state is unavailable", out reason);
            if (!ClassicSecondaryBossContract.TargetsLocalPlayer(snapshot, part))
                return Invalid("Golem component targets a different player", out reason);
            if (part.NativeRealLifeKnown && part.NativeRealLife >= 0 &&
                part.NativeRealLife != body.Key)
                return Invalid("Golem component has the wrong native parent", out reason);
            if (part.Type == AttachedHeadType)
            {
                var phase = NativeInteger(part.Ai0, 0, 1);
                var expected = part.LifeMax > 0 && part.Life < part.LifeMax / 2 ? 1 : 0;
                if (phase == int.MinValue || phase != expected || NativeInteger(part.Ai1, 0, 299) == int.MinValue ||
                    NativeInteger(part.Ai2, 0, 900) == int.MinValue || part.Ai3 != 0f ||
                    !Binary(part.LocalAi0) || NativeInteger(part.LocalAi1, -1, 1) == int.MinValue ||
                    part.LocalAi2 != 0f || part.LocalAi3 != 0f || part.Invulnerable ||
                    !Near(part.Center, body.Center + new Vec2(0f, -57f), 220f))
                    return Invalid("Golem attached head phase/timers are malformed", out reason);
            }
            else if (part.Type == DetachedHeadType)
            {
                if (part.Ai0 != 0f || NativeInteger(part.Ai1, 0, 299) == int.MinValue ||
                    !Finite(part.Ai2) || part.Ai2 < 0f || part.Ai2 > 10000f ||
                    part.Ai3 != 0f || !Binary(part.LocalAi0) ||
                    part.LocalAi1 != 0f || part.LocalAi2 != 0f || part.LocalAi3 != 0f ||
                    !part.Invulnerable ||
                    !Near(part.Center, body.Center + new Vec2(0f, -300f), 220f))
                    return Invalid("Golem detached head timers or anchor are malformed", out reason);
            }
            else
            {
                var state = NativeInteger(part.Ai0, 0, 2);
                var clock = state == 0 ? NativeInteger(part.Ai1, 0, 60) :
                    state == 1 ? NativeInteger(part.Ai1, 0, 29) :
                    NativeInteger(part.Ai1, 0, 1200);
                if (state == int.MinValue || clock == int.MinValue ||
                    part.Ai2 != 0f || part.Ai3 != 0f ||
                    part.LocalAi0 != 0f || part.LocalAi1 != 0f ||
                    part.LocalAi2 != 0f || part.LocalAi3 != 0f || part.Invulnerable ||
                    !Finite(part.Velocity) || part.Velocity.LengthSquared > 64f * 64f ||
                    !ValidFistSide(body, part, state))
                    return Invalid("Golem fist state/timer or side anchor is malformed", out reason);
            }
            reason = null;
            return true;
        }

        private static bool ValidFistSide(TargetSnapshot body,
            TargetSnapshot fist, int state)
        {
            if (state == 2) return true;
            var delta = fist.Center.X - body.Center.X;
            if (fist.Type == LeftFistType ? delta >= 40f : delta <= -40f)
                return false;
            // In states 0/1 vanilla continually homes the fist to this
            // body-relative anchor. State 1 is snapped exactly to it; state 0
            // may lag while travelling, so retain a wider bounded tolerance.
            var anchor = body.Center + body.Velocity +
                new Vec2(fist.Type == LeftFistType ? -84f : 78f, -9f);
            return Near(fist.Center, anchor, state == 1 ? 96f : 220f);
        }

        private static bool TempleArenaEvidence(ArenaSnapshot arena,
            PlayerSnapshot player)
        {
            if (arena == null || player == null) return false;
            var floor = ValidSupport(arena.FloorSupport, player, false);
            var recovery = ValidSupport(arena.RecoverySupport, player, false);
            var ceiling = ValidSupport(arena.CeilingSupport, player, true);
            // The scanner exposes at most one current and one retained row. A
            // pair of independently valid rows is the minimum evidence that
            // this is a room/platform arena rather than open sky or Hell.
            return floor && (recovery || ceiling) || recovery && ceiling;
        }

        private static bool ValidSupport(SupportSpan support,
            PlayerSnapshot player, bool inverted)
        {
            return support.Valid && support.Inverted == inverted &&
                Finite(support.Left) && Finite(support.Right) &&
                Finite(support.SurfaceY) && support.Right - support.Left >=
                player.Width + 64f;
        }

        private static bool ValidateProjectiles(CombatSnapshot snapshot,
            out string reason)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Type != 258 && threat.Type != 259) continue;
                if (threat.Kind != ThreatKind.Projectile || !Finite(threat.Position) ||
                    !Finite(threat.Velocity) || threat.Width < 0 ||
                    threat.Height < 0 || threat.TimeLeft < 0)
                    return Invalid("Golem projectile observation is malformed", out reason);
            }
            reason = null;
            return true;
        }

        private static bool IsGolemType(int type) => type >= BodyType && type <= DetachedHeadType;

        private static bool ValidEntity(TargetSnapshot value)
        {
            return value.Key >= 0 && value.Life > 0 && value.LifeMax >= value.Life &&
                value.Width > 0 && value.Height > 0 && Finite(value.Position) &&
                Finite(value.Velocity);
        }

        private static bool Binary(float value) => value == 0f || value == 1f;
        private static bool Near(Vec2 value, Vec2 expected, float radius)
        {
            return Finite(value) && Vec2.DistanceSquared(value, expected) <= radius * radius;
        }
        private static int NativeInteger(float value, int minimum, int maximum)
        {
            return ClassicSecondaryBossContract.IsExactInt(value, minimum, maximum)
                ? (int)Math.Round(value) : int.MinValue;
        }
        private static bool Finite(float value) => ClassicSecondaryBossContract.Finite(value);
        private static bool Finite(Vec2 value) => ClassicSecondaryBossContract.Finite(value);
        private static bool Invalid(string value, out string reason)
        {
            reason = value;
            return false;
        }
    }
}
