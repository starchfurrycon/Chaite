using System;
using System.Collections.Generic;
using System.Globalization;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// Offline laboratory for the Duke Fishron no-hit problem.
    ///
    /// Everything here is deterministic. AI_069 has no randomness that touches
    /// the fight: the pinned IL shows `Main.rand` only in dust and in the
    /// phase-two ring-bubble *size*, and the dash cadence, dash direction and
    /// dash count are constants. A charge commits its velocity once, on the
    /// state-entry tick, to `normalize(player.Center - Center) * num7` and then
    /// travels a straight line, so the whole fight is a function of the player's
    /// input sequence alone.
    ///
    /// The lab exists to answer one question with numbers instead of argument:
    /// given the engine's own player physics, is there an input sequence that
    /// takes zero hits, and what does it look like? It is a research harness,
    /// not evidence about the real client -- see docs/fishron-no-hit-lab.md for
    /// the measured results and the boundary.
    ///
    /// Two known limits of this harness, both load-bearing when reading its
    /// numbers:
    ///
    ///   * The Shield dash cannot be evaluated here. `PlayerMotionFrame` does
    ///     not carry the dash-readiness state, so `PlayerControlFrame.Dash` has
    ///     no effect and every dash sweep returns identical figures.
    ///   * Peak perpendicular clearance is not the quantity that decides a hit.
    ///     A hit depends on the clearance at the moment of closest approach, and
    ///     a strategy can post a large peak while still being hit.
    /// </summary>
    internal static partial class Program
    {
        // ---------------------------------------------------------------- AI_069
        // Constants below are the pinned 1.4.5.8 expert-mode values, transcribed
        // from docs/fishron-ai-spec.md (which was produced from the IL, not from
        // the wiki). Difficulty is expert throughout, because that is the
        // acceptance target.
        private const int FishronType = 370;
        private const float ChargeSpeed = 17f;
        private const int ChargeTicks = 28;
        private const float EnragedChargeSpeed = 23f;
        private const float HoverAccel = 0.55f;
        private const float HoverMaxSpeed = 8.5f;
        private const float HoverAccelPhase2 = 0.6f;
        private const float HoverMaxSpeedPhase2 = 10f;
        private const float HoverAccelPhase3 = 0.7f;
        private const float HoverMaxSpeedPhase3 = 12f;
        private const int EnragedHoverTicks = 10;
        private const float OceanBandPixels = 6400f;
        private const float SkyEnrageCeiling = 800f;
        private const int BossWidth = 150;
        private const int BossHeight = 100;

        private enum FightPhase { One, Two, Three }

        /// <summary>One Detonating Bubble (NPC 371). Its AI bends towards the
        /// player, and it grows from 36x36 to 100x100 when it dies.</summary>
        private sealed class Bubble
        {
            public Vec2 Position;
            public Vec2 Velocity;
            public int Life = 150;
            public bool Detonated;
            public int DetonateTicks;
        }

        /// <summary>A Sharknado / Cthulhunado column. It never moves and never
        /// tracks; its threat is its width plus the Sharkrons it emits.</summary>
        private sealed class Tornado
        {
            public float X;
            public float Top;
            public float Bottom;
            public float HalfWidth;
            public int Life;
            public int SpawnClock;
            public int SharkronEvery;
            public int SharkronType;
        }

        private sealed class Sharkron
        {
            public Vec2 Position;
            public Vec2 Velocity;
            public int Wait;
            public bool Launched;
            public int Width;
            public int Height;
        }

        private sealed class FightWorld
        {
            public FightPhase Phase = FightPhase.One;
            public float BossX, BossY, BossVx, BossVy;
            public int State = 0;
            public int StateTimer;
            public int AttackCounter;
            public float HoverOffset;
            public float BossLife = 78000f;
            public float BossLifeMax = 78000f;
            public float FloorY;
            public float BandLeft, BandRight;
            public bool Enraged;
            public readonly List<Bubble> Bubbles = new List<Bubble>();
            public readonly List<Tornado> Tornadoes = new List<Tornado>();
            public readonly List<Sharkron> Sharkrons = new List<Sharkron>();
            public int Hits;
            public readonly List<string> HitLog = new List<string>();
            public int ImmuneTicks;
            public int ChargeCount;
            public int Tick;
            public int TicksSinceBubbleDamage;
            // Research switches. Turning a threat class off is how the lab
            // attributes a remaining hit to a mechanism instead of to "the
            // boss" in general.
            public bool BossContactEnabled = true;
            public bool BubblesEnabled = true;
            public bool TornadoesEnabled = true;

            public Vec2 BossCenter => new Vec2(BossX + BossWidth * 0.5f,
                BossY + BossHeight * 0.5f);
        }

        // ------------------------------------------------------------- the player
        // The acceptance loadout is the "strong wing" route: Fishron Wings plus a
        // Shield of Cthulhu dash source. Fishron Wings are wingsLogic 45 in the
        // pinned build, whose measured constants are num2=.95 num5=.15 num4=1
        // num3=4.5 with an up-hover of 0.4. Horizontal run speed with boots is
        // accRunSpeed 6; the base sprint ceiling is 6.75.
        private static PlayerMotionFrame FishronPlayerStart(float x, float floorY)
        {
            var jump = new JumpSnapshot
            {
                Known = true,
                RemainingTicks = 15,
                Speed = 5.01f,
                Height = 15,
                ReleaseReady = true,
                CloudAvailable = false,
                CloudEnabled = false,
                AutoJump = false,
            };
            var flight = new FlightSnapshot
            {
                Known = true,
                WingsLogic = 45,
                RocketBoots = 0,
                WingTime = 150f,
                WingTimeMax = 150,
                RocketTime = 0,
                RocketTimeMax = 0,
                RocketDelay = 0,
                CanRocket = false,
                RocketRelease = true,
            };
            return new PlayerMotionFrame
            {
                Position = new Vec2(x, floorY - 42f),
                Velocity = new Vec2(0f, 0f),
                Tick = 0,
                Width = 20,
                Height = 42,
                Gravity = 0.4f,
                MaxFallSpeed = 10f,
                GravityDirection = 1,
                BaseRunSpeed = 3f,
                MaxRunSpeed = 6.75f,
                AccRunSpeed = 6f,
                RunAcceleration = 0.08f,
                SprintAcceleration = 0.08f,
                RunSlowdown = 0.2f,
                CanSprintInAir = true,
                Grounded = true,
                JustJumped = false,
                FloorY = floorY,
                WingTime = 150f,
                Jump = jump,
                Flight = flight,
                // The acceptance loadout wears a Shield of Cthulhu, and the
                // rollout begins between dashes. Both facts have to be declared
                // on the frame: without them PlayerForwardModel takes the
                // ordinary step for a tick that asked to dash, which is why
                // every dash sweep in this lab used to return one set of
                // figures regardless of timing.
                DashIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097,
                DashReady = true,
            };
        }

        private static PlayerSnapshot PlayerView(in PlayerMotionFrame frame,
            FightWorld world)
        {
            var snapshot = new PlayerSnapshot
            {
                Position = frame.Position,
                Velocity = frame.Velocity,
                Width = frame.Width,
                Height = frame.Height,
                Gravity = frame.Gravity,
                MaxFallSpeed = frame.MaxFallSpeed,
                MaxRunSpeed = frame.MaxRunSpeed,
                AccRunSpeed = frame.AccRunSpeed,
                RunAcceleration = frame.RunAcceleration,
                BaseRunSpeed = frame.BaseRunSpeed,
                SprintAcceleration = frame.SprintAcceleration,
                RunSlowdown = frame.RunSlowdown,
                CanSprintInAir = frame.CanSprintInAir,
                OnGround = frame.Grounded,
                Jump = frame.Jump,
                Flight = frame.Flight,
                WingTime = frame.WingTime,
                WorldLeft = 0f,
                WorldRight = world.BandRight + OceanBandPixels,
            };
            return snapshot;
        }

        private static TargetSnapshot BossView(FightWorld world)
        {
            return new TargetSnapshot
            {
                Key = 0,
                Type = FishronType,
                Position = new Vec2(world.BossX, world.BossY),
                Velocity = new Vec2(world.BossVx, world.BossVy),
                Width = BossWidth,
                Height = BossHeight,
                Life = (int)world.BossLife,
                LifeMax = (int)world.BossLifeMax,
                Boss = true,
                Ai0 = world.State,
                Ai1 = world.HoverOffset,
                Ai2 = world.StateTimer,
                Ai3 = world.AttackCounter,
                Ai0Known = true,
                Ai1Known = true,
                Ai2Known = true,
                Ai3Known = true,
            };
        }

        // -------------------------------------------------------------- collisions
        private static bool Overlaps(Vec2 aPos, int aW, int aH, Vec2 bPos, int bW,
            int bH)
        {
            return aPos.X < bPos.X + bW && aPos.X + aW > bPos.X &&
                aPos.Y < bPos.Y + bH && aPos.Y + aH > bPos.Y;
        }

        private static void RegisterHit(FightWorld world, string source,
            Vec2 playerPos, int playerW, int playerH)
        {
            world.Hits++;
            if (world.HitLog.Count < 200)
                world.HitLog.Add(string.Format(CultureInfo.InvariantCulture,
                    "tick {0} charge#{1} state {2} seq {3} timer {4} phase {5} " +
                    "src {6} boss ({7:F0},{8:F0}) player ({9:F0},{10:F0})",
                    world.Tick, world.ChargeCount, world.State,
                    world.AttackCounter, world.StateTimer, world.Phase, source,
                    world.BossCenter.X, world.BossCenter.Y,
                    playerPos.X + playerW * 0.5f, playerPos.Y + playerH * 0.5f));
        }

        // ------------------------------------------------------------- AI_069 tick
        private static bool IsDashState(int state) =>
            state == 1 || state == 6 || state == 11;

        private static int HoverTicks(FightWorld world)
        {
            if (world.Enraged) return EnragedHoverTicks;
            switch (world.State)
            {
                case 0: return world.AttackCounter < 10 ? 30 : 30;
                case 5: return world.AttackCounter < 6 ? 40 : 40;
                case 10: return 30;
                default: return 30;
            }
        }

        private static void AdvanceBoss(FightWorld world, PlayerMotionFrame player)
        {
            var pc = new Vec2(player.Position.X + player.Width * 0.5f,
                player.Position.Y + player.Height * 0.5f);
            var bc = world.BossCenter;

            // Enrage: the pinned three-term predicate.
            var flag6 = player.Position.Y < SkyEnrageCeiling ||
                player.Position.X < world.BandLeft ||
                player.Position.X > world.BandRight;
            world.Enraged = flag6;

            if (IsDashState(world.State))
            {
                world.BossX += world.BossVx;
                world.BossY += world.BossVy;
                world.StateTimer++;
                if (world.StateTimer >= ChargeTicks)
                {
                    world.AttackCounter += world.State == 11 ? 1 : 2;
                    world.State = world.State == 1 ? 0 : world.State == 6 ? 5 : 10;
                    world.StateTimer = 0;
                    world.HoverOffset = 0f;
                }
                return;
            }

            switch (world.State)
            {
                case 0:
                case 5:
                case 10:
                {
                    if (world.HoverOffset == 0f)
                        world.HoverOffset = 300f * Math.Sign(bc.X - pc.X);
                    var target = new Vec2(pc.X + world.HoverOffset, pc.Y - 200f);
                    var toTarget = new Vec2(target.X - bc.X, target.Y - bc.Y);
                    var length = (float)Math.Sqrt(toTarget.X * toTarget.X +
                        toTarget.Y * toTarget.Y);
                    var maxSpeed = world.State == 10 ? HoverMaxSpeedPhase3 :
                        world.State == 5 ? HoverMaxSpeedPhase2 : HoverMaxSpeed;
                    var accel = world.State == 10 ? HoverAccelPhase3 :
                        world.State == 5 ? HoverAccelPhase2 : HoverAccel;
                    if (length > maxSpeed)
                    {
                        var wantX = toTarget.X / length * maxSpeed;
                        var wantY = toTarget.Y / length * maxSpeed;
                        world.BossVx = Approach(world.BossVx, wantX, accel);
                        world.BossVy = Approach(world.BossVy, wantY, accel);
                    }
                    else
                    {
                        world.BossVx = toTarget.X;
                        world.BossVy = toTarget.Y;
                    }
                    world.BossX += world.BossVx;
                    world.BossY += world.BossVy;
                    world.StateTimer++;
                    if (world.StateTimer >= HoverTicks(world))
                        HoverDecision(world, pc);
                    return;
                }
                case 2:
                    if (world.StateTimer % 4 == 0) SpawnBubbleSpray(world, pc, false);
                    world.StateTimer++;
                    if (world.StateTimer >= 80)
                    {
                        world.State = 0;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                case 3:
                    if (world.StateTimer == 60) SpawnSharknadoBolts(world, pc);
                    world.StateTimer++;
                    if (world.StateTimer >= 90)
                    {
                        world.State = 0;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                case 7:
                    if (world.StateTimer % 4 == 0) SpawnBubbleSpray(world, pc, true);
                    world.StateTimer++;
                    if (world.StateTimer >= 120)
                    {
                        world.State = 5;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                case 8:
                    if (world.StateTimer == 60) SpawnCthulhunadoBolt(world, pc);
                    world.StateTimer++;
                    if (world.StateTimer >= 90)
                    {
                        world.State = 5;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                case 4:
                case 9:
                    world.StateTimer++;
                    if (world.StateTimer >= 180)
                    {
                        world.Phase = world.State == 4 ? FightPhase.Two : FightPhase.Three;
                        world.State = world.State == 4 ? 5 : 10;
                        world.StateTimer = 0;
                        world.AttackCounter = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                case 12:
                    if (world.StateTimer == 15)
                    {
                        if (world.HoverOffset == 0f)
                            world.HoverOffset = 300f * Math.Sign(bc.X - pc.X);
                        var landing = new Vec2(pc.X - world.HoverOffset, pc.Y - 200f);
                        world.BossX = landing.X - BossWidth * 0.5f;
                        world.BossY = landing.Y - BossHeight * 0.5f;
                        world.BossVx = 0f;
                        world.BossVy = 0f;
                    }
                    world.StateTimer++;
                    if (world.StateTimer >= 30)
                    {
                        world.State = 10;
                        world.StateTimer = 0;
                        world.AttackCounter++;
                        if (world.AttackCounter >= 9) world.AttackCounter = 0;
                        world.HoverOffset = 0f;
                    }
                    return;
                default:
                    world.StateTimer++;
                    return;
            }
        }

        private static float Approach(float current, float want, float step)
        {
            if (current < want) return Math.Min(want, current + step);
            if (current > want) return Math.Max(want, current - step);
            return current;
        }

        private static void HoverDecision(FightWorld world, Vec2 pc)
        {
            var seq = world.AttackCounter;
            var speed = world.Enraged ? EnragedChargeSpeed : ChargeSpeed;
            switch (world.State)
            {
                case 0:
                    if (world.Phase == FightPhase.One && world.BossLife <=
                        world.BossLifeMax * 0.5f)
                    {
                        world.State = 4;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    if (seq == 10)
                    {
                        world.State = 2;
                        world.AttackCounter = 1;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    if (seq == 11)
                    {
                        world.State = 3;
                        world.AttackCounter = 0;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    StartCharge(world, pc, speed);
                    return;
                case 5:
                    if (world.Phase == FightPhase.Two && world.BossLife <=
                        world.BossLifeMax * 0.15f)
                    {
                        world.State = 9;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    if (seq == 6)
                    {
                        world.State = 7;
                        world.AttackCounter = 1;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    if (seq == 7)
                    {
                        world.State = 8;
                        world.AttackCounter = 0;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    StartCharge(world, pc, speed);
                    return;
                case 10:
                {
                    var teleport = seq == 1 || seq == 4 || seq == 8;
                    if (teleport)
                    {
                        world.State = 12;
                        world.StateTimer = 0;
                        world.HoverOffset = 0f;
                        return;
                    }
                    StartCharge(world, pc, world.Enraged ? 33f : 27f);
                    return;
                }
            }
        }

        private static void StartCharge(FightWorld world, Vec2 pc, float speed)
        {
            var bc = world.BossCenter;
            var dx = pc.X - bc.X;
            var dy = pc.Y - bc.Y;
            var length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.0001f) { dx = 1f; dy = 0f; length = 1f; }
            world.BossVx = dx / length * speed;
            world.BossVy = dy / length * speed;
            world.State = world.State == 0 ? 1 : world.State == 5 ? 6 : 11;
            world.StateTimer = 0;
            world.ChargeCount++;
        }

        private static void SpawnBubbleSpray(FightWorld world, Vec2 pc, bool ring)
        {
            var bc = world.BossCenter;
            float dx, dy;
            if (ring)
            {
                var vx = world.BossVx;
                var vy = world.BossVy;
                var l = (float)Math.Sqrt(vx * vx + vy * vy);
                if (l < 0.0001f) { vx = 1f; vy = 0f; l = 1f; }
                dx = vx / l; dy = vy / l;
            }
            else
            {
                dx = pc.X - bc.X;
                dy = pc.Y - bc.Y;
                var l = (float)Math.Sqrt(dx * dx + dy * dy);
                if (l < 0.0001f) { dx = 1f; dy = 0f; l = 1f; }
                dx /= l; dy /= l;
            }
            var px = bc.X + dx * 85f;
            var py = bc.Y + dy * 85f + 45f;
            var speed = 13.2f;
            var aimX = pc.X + 0f - px;
            var aimY = pc.Y + 0f - py;
            var al = (float)Math.Sqrt(aimX * aimX + aimY * aimY);
            if (al < 0.0001f) { aimX = 1f; aimY = 0f; al = 1f; }
            world.Bubbles.Add(new Bubble
            {
                Position = new Vec2(px, py),
                Velocity = new Vec2(aimX / al * speed, aimY / al * speed),
            });
        }

        private static void SpawnSharknadoBolts(FightWorld world, Vec2 pc)
        {
            // Both bolts come from the same point and are not homing; they fall
            // until they hit liquid, then Kill() places a Sharknado at the bolt.
            // The runway is dry in the fixture, so the columns land where the
            // bolts are when their lifetime runs out.
            var bc = world.BossCenter;
            var direction = Math.Sign(pc.X - bc.X);
            if (direction == 0) direction = 1;
            var px = bc.X + direction * 85f;
            var py = bc.Y;
            for (var side = -1; side <= 1; side += 2)
            {
                world.Tornadoes.Add(new Tornado
                {
                    X = px + side * 30f,
                    Top = world.FloorY - 452f,
                    Bottom = world.FloorY,
                    HalfWidth = 75f,
                    Life = 540,
                    SpawnClock = 0,
                    SharkronEvery = 4,
                    SharkronType = 372,
                });
            }
        }

        private static void SpawnCthulhunadoBolt(FightWorld world, Vec2 pc)
        {
            // The homing bolt self-destructs within 50 px of the player and drops
            // the Cthulhunado at the player's own column. This is the one attack
            // the player fully controls the landing position of.
            world.Tornadoes.Add(new Tornado
            {
                X = pc.X,
                Top = world.FloorY - 959f,
                Bottom = world.FloorY,
                HalfWidth = 112f,
                Life = 840,
                SpawnClock = 0,
                SharkronEvery = 2,
                SharkronType = 373,
            });
        }

        private static void AdvanceThreats(FightWorld world, PlayerMotionFrame player)
        {
            var pc = new Vec2(player.Position.X + player.Width * 0.5f,
                player.Position.Y + player.Height * 0.5f);

            for (var i = world.Bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = world.Bubbles[i];
                if (bubble.Detonated)
                {
                    bubble.DetonateTicks--;
                    if (bubble.DetonateTicks <= 0) world.Bubbles.RemoveAt(i);
                    continue;
                }
                var dx = pc.X - bubble.Position.X;
                var dy = pc.Y - bubble.Position.Y;
                var l = (float)Math.Sqrt(dx * dx + dy * dy);
                if (l < 0.0001f) { dx = 1f; dy = 0f; l = 1f; }
                bubble.Velocity = new Vec2(
                    (bubble.Velocity.X * 40f + dx / l * 20f) / 41f,
                    (bubble.Velocity.Y * 40f + dy / l * 20f) / 41f);
                bubble.Position = new Vec2(bubble.Position.X + bubble.Velocity.X,
                    bubble.Position.Y + bubble.Velocity.Y);
                bubble.Life--;
                var inflated = new RectF(bubble.Position.X - 20f,
                    bubble.Position.Y - 20f, 40f, 40f);
                var near = inflated.X < pc.X + 20f && inflated.X + 40f > pc.X - 20f &&
                    inflated.Y < pc.Y + 21f && inflated.Y + 40f > pc.Y - 21f;
                if (near || bubble.Life <= 0)
                {
                    bubble.Detonated = true;
                    bubble.DetonateTicks = 4;
                    if (Overlaps(player.Position, player.Width, player.Height,
                            new Vec2(bubble.Position.X - 50f,
                                bubble.Position.Y - 50f), 100, 100))
                        RegisterHit(world, "bubble", player.Position,
                            player.Width, player.Height);
                }
            }

            for (var i = world.Tornadoes.Count - 1; i >= 0; i--)
            {
                var tornado = world.Tornadoes[i];
                tornado.Life--;
                tornado.SpawnClock++;
                if (tornado.Life <= 0) { world.Tornadoes.RemoveAt(i); continue; }
                if (!world.TornadoesEnabled) continue;
                if (tornado.SpawnClock % (tornado.SharkronEvery * 10) == 0)
                {
                    world.Sharkrons.Add(new Sharkron
                    {
                        Position = new Vec2(tornado.X,
                            tornado.Top + (world.FloorY - tornado.Top) *
                            (tornado.SpawnClock / (float)tornado.Life)),
                        Velocity = new Vec2(0f, 0f),
                        Wait = 90,
                        Width = tornado.SharkronType == 372 ? 120 : 100,
                        Height = 24,
                    });
                }
                var left = tornado.X - tornado.HalfWidth;
                var right = tornado.X + tornado.HalfWidth;
                var top = tornado.Top;
                if (Overlaps(player.Position, player.Width, player.Height,
                        new Vec2(left, top), (int)(right - left),
                        (int)(world.FloorY - top)))
                    RegisterHit(world, "tornado", player.Position, player.Width,
                        player.Height);
            }

            for (var i = world.Sharkrons.Count - 1; i >= 0; i--)
            {
                var shark = world.Sharkrons[i];
                if (!shark.Launched)
                {
                    shark.Wait--;
                    if (shark.Wait <= 0)
                    {
                        var dx = pc.X - shark.Position.X;
                        var dy = pc.Y - shark.Position.Y;
                        var l = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (l < 0.0001f) { dx = 1f; dy = 0f; l = 1f; }
                        shark.Velocity = new Vec2(dx / l * 16f, dy / l * 16f);
                        shark.Launched = true;
                    }
                }
                else
                {
                    shark.Position = new Vec2(
                        shark.Position.X + shark.Velocity.X,
                        shark.Position.Y + shark.Velocity.Y);
                    if (shark.Position.Y > world.FloorY + 200f)
                    {
                        world.Sharkrons.RemoveAt(i);
                        continue;
                    }
                }
                if (Overlaps(player.Position, player.Width, player.Height,
                        shark.Position, shark.Width, shark.Height))
                    RegisterHit(world, "sharkron", player.Position, player.Width,
                        player.Height);
            }
        }

        /// <summary>Runs one deterministic fight with a supplied controller and
        /// reports the outcome. No randomness anywhere.</summary>
        private static FightResult RunFight(IFishronController controller,
            int maxTicks, bool verbose = false, bool trace = false,
            int traceTicks = 70, int maxHits = 1, bool bossOnly = false,
            bool bubbles = false)
        {
            const float floorY = 6000f;
            var bandLeft = 1000f;
            var bandRight = 6000f;
            var world = new FightWorld
            {
                FloorY = floorY,
                BandLeft = bandLeft,
                BandRight = bandRight,
                BossX = 2600f,
                BossY = floorY - 42f - 400f,
                BubblesEnabled = !bossOnly || bubbles,
                TornadoesEnabled = !bossOnly,
                BossContactEnabled = true,
            };
            var frame = FishronPlayerStart(3300f, floorY);
            controller.Reset();
            var chargeLine = new Vec2(0f, 0f);
            var chargeOrigin = new Vec2(0f, 0f);
            var chargePlayer = new Vec2(0f, 0f);
            var hadCharge = false;
            var maxPerpendicular = 0f;
            var closestPerpendicular = float.MaxValue;
            var chargeOrdinal = 1;
            var chargeStartTick = 0;
            var chargeMaxPerpendicular = 0f;
            var chargeClosestDistance = float.MaxValue;
            var chargeClosestPerpendicular = 0f;
            var chargeHit = false;
            var chargeEvents = new List<string>();

            while (world.Tick < maxTicks &&
                (maxHits < 0 || world.Hits < maxHits))
            {
                if (world.ImmuneTicks > 0) world.ImmuneTicks--;
                var playerView = PlayerView(in frame, world);
                var bossView = BossView(world);
                var controls = controller.Decide(world.Tick, in frame,
                    playerView, bossView, world);
                var arena = new ArenaSnapshot();
                PlayerMotionFrame next;
                ForwardModelRefusal refusal;
                if (!PlayerForwardModel.TryAdvance(in frame, in controls,
                        out next, out refusal))
                {
                    return new FightResult
                    {
                        Ticks = world.Tick,
                        Hits = world.Hits,
                        Refusal = refusal.ToString(),
                        HitLog = world.HitLog,
                    };
                }
                // Arena clamp: Player.BordersMovement pins the player inside the
                // world border. The fixture runway is narrower than the world.
                if (next.Position.X < bandLeft)
                {
                    next.Position = new Vec2(bandLeft, next.Position.Y);
                    next.Velocity = new Vec2(0f, next.Velocity.Y);
                }
                else if (next.Position.X > bandRight - next.Width)
                {
                    next.Position = new Vec2(bandRight - next.Width,
                        next.Position.Y);
                    next.Velocity = new Vec2(0f, next.Velocity.Y);
                }
                var perpendicular = 0f;
                var numerator = 0f;
                frame = next;

                var wasDash = IsDashState(world.State);
                // A new charge commits its line on this tick, and the line is
                // what the measurement below is taken against, so it is
                // established before anything is measured. Doing it the other
                // way -- measuring with the new direction but the previous
                // origin -- reported a perpendicular of zero at the closest
                // approach for every strategy, which is not a measurement at
                // all.
                AdvanceBoss(world, frame);
                var isDash = IsDashState(world.State);
                if (isDash && !wasDash)
                {
                    if (chargeOrdinal > 1)
                        chargeEvents.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "charge {0,3} start {1,5} angle {2,5:F1}deg " +
                            "maxPerp {3,6:F1} perpAtClosest {4,6:F1} hit={5}",
                            chargeOrdinal - 1, chargeStartTick,
                            Math.Atan2(Math.Abs(chargeLine.Y),
                                Math.Abs(chargeLine.X)) * 180.0 / Math.PI,
                            chargeMaxPerpendicular, chargeClosestPerpendicular,
                            chargeHit));
                    chargeOrdinal++;
                    chargeLine = new Vec2(world.BossVx, world.BossVy);
                    chargeOrigin = world.BossCenter;
                    chargeStartTick = world.Tick;
                    chargePlayer = new Vec2(frame.Position.X + frame.Width * 0.5f,
                        frame.Position.Y + frame.Height * 0.5f);
                    chargeMaxPerpendicular = 0f;
                    chargeClosestDistance = float.MaxValue;
                    chargeClosestPerpendicular = 0f;
                    chargeHit = false;
                    hadCharge = true;
                }
                if (hadCharge && isDash)
                {
                    var playerX = frame.Position.X + frame.Width * 0.5f;
                    var playerY = frame.Position.Y + frame.Height * 0.5f;
                    var l = (float)Math.Sqrt(chargeLine.X * chargeLine.X +
                        chargeLine.Y * chargeLine.Y);
                    if (l > 0.0001f)
                    {
                        var ux = chargeLine.X / l;
                        var uy = chargeLine.Y / l;
                        // Signed projection of the player onto the line, and
                        // the perpendicular distance, both measured from the
                        // line's own origin.
                        var px = playerX - chargeOrigin.X;
                        var py = playerY - chargeOrigin.Y;
                        numerator = px * ux + py * uy;
                        perpendicular = Math.Abs(px * -uy + py * ux);
                    }
                    if (perpendicular > chargeMaxPerpendicular)
                        chargeMaxPerpendicular = perpendicular;
                    if (perpendicular > maxPerpendicular)
                        maxPerpendicular = perpendicular;
                    // The boss's projection onto the player's path. The closest
                    // approach is where the two projections meet, not where the
                    // two bodies happen to be nearest each other in general, so
                    // this is the figure a dodge has to win.
                    if (l > 0.0001f)
                    {
                        var bossX = world.BossX + BossWidth * 0.5f;
                        var bossY = world.BossY + BossHeight * 0.5f;
                        var bossS = ((bossX - chargeOrigin.X) * chargeLine.X +
                            (bossY - chargeOrigin.Y) * chargeLine.Y) / l;
                        var approach = Math.Abs(bossS - numerator);
                        if (approach < chargeClosestDistance)
                        {
                            chargeClosestDistance = approach;
                            chargeClosestPerpendicular = perpendicular;
                        }
                    }
                    if (chargeClosestPerpendicular < closestPerpendicular)
                        closestPerpendicular = chargeClosestPerpendicular;
                }
                if (isDash && !wasDash && verbose)
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "  charge {0,3} at tick {1,5} from ({2:F0},{3:F0}) " +
                        "vec ({4:F1},{5:F1}) player ({6:F0},{7:F0})",
                        chargeOrdinal++, world.Tick, chargeOrigin.X,
                        chargeOrigin.Y, chargeLine.X, chargeLine.Y,
                        frame.Position.X + frame.Width * 0.5f,
                        frame.Position.Y + frame.Height * 0.5f));
                if (trace && world.Tick < traceTicks)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  T{0,3} st={1,2} tm={2,2} boss=({3,7:F1},{4,7:F1}) " +
                        "bv=({5,6:F1},{6,6:F1}) ply=({7,7:F1},{8,7:F1}) " +
                        "pv=({9,6:F2},{10,6:F2}) ctrl={11}{12}{13}{14} " +
                        "perp={15,6:F1}",
                        world.Tick, world.State, world.StateTimer,
                        world.BossCenter.X, world.BossCenter.Y, world.BossVx,
                        world.BossVy, frame.Position.X + frame.Width * 0.5f,
                        frame.Position.Y + frame.Height * 0.5f, frame.Velocity.X,
                        frame.Velocity.Y, controls.Left ? "L" : "-",
                        controls.Right ? "R" : "-", controls.Jump ? "J" : "-",
                        controls.Dash ? "D" : "-", perpendicular));
                AdvanceThreats(world, frame);

                if (world.BossContactEnabled && world.ImmuneTicks == 0 &&
                    Overlaps(frame.Position, frame.Width, frame.Height,
                        new Vec2(world.BossX, world.BossY), BossWidth,
                        BossHeight))
                {
                    chargeHit = true;
                    RegisterHit(world, "boss", frame.Position, frame.Width,
                        frame.Height);
                }
                if (verbose && world.Tick % 3 == 0)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  t={0} state={1} seq={2} timer={3} boss=({4:F0},{5:F0}) " +
                        "player=({6:F0},{7:F0}) v=({8:F1},{9:F1}) enraged={10} " +
                        "bubbles={11} nados={12} sharks={13}",
                        world.Tick, world.State, world.AttackCounter,
                        world.StateTimer, world.BossCenter.X, world.BossCenter.Y,
                        frame.Position.X + 10f, frame.Position.Y + 21f,
                        frame.Velocity.X, frame.Velocity.Y, world.Enraged,
                        world.Bubbles.Count, world.Tornadoes.Count,
                        world.Sharkrons.Count));
                world.Tick++;
            }
            return new FightResult
            {
                Ticks = world.Tick,
                Hits = world.Hits,
                Refusal = "None",
                HitLog = world.HitLog,
                Charges = world.ChargeCount,
                MaxPerpendicular = maxPerpendicular,
                ClosestPerpendicular = closestPerpendicular,
                ChargeLog = chargeEvents,
            };
        }

        private sealed class FightResult
        {
            public int Ticks;
            public int Hits;
            public string Refusal;
            public List<string> HitLog = new List<string>();
            public int Charges;
            public float MaxPerpendicular;
            /// <summary>
            /// The smallest perpendicular clearance measured at the moment of
            /// closest approach, over every charge. This is the figure that
            /// decides a hit; the peak clearance over a whole charge does not,
            /// and reading the peak instead was the error that made an earlier
            /// round of this lab look far healthier than it was.
            /// </summary>
            public float ClosestPerpendicular;
            public List<string> ChargeLog = new List<string>();
        }

        private interface IFishronController
        {
            void Reset();
            PlayerControlFrame Decide(int tick, in PlayerMotionFrame frame,
                PlayerSnapshot player, TargetSnapshot boss, FightWorld world);
        }

        /// <summary>How the dash is aimed on a vertical beat.
        ///
        /// There is no choice here, and that is the point. `PlayerControlFrame`
        /// derives `Direction` from Left/Right, so the only dash this frame can
        /// ask for is one along its own horizontal input. The engine's own rule
        /// agrees: `DoCommonDashHandle` writes the dash direction as the player's
        /// facing, flipped only when the input opposes it, and the dash velocity
        /// itself is `velocity.X` -- horizontal, never vertical.
        ///
        /// So "dash up" and "dash down" are not dashes in a vertical direction.
        /// They are a horizontal dash held together with a vertical input, and
        /// the compound motion is the diagonal escape. That is exactly what makes
        /// a vertical beat able to clear a charge line: the vertical input alone
        /// leaves parallel to the line, and the dash is what adds the
        /// perpendicular component.
        /// </summary>
        private enum DashAim
        {
            /// <summary>Dash along the horizontal run input, away from the Boss,
            /// while the vertical input supplies the perpendicular component.
            /// This is the only aim the engine can express.</summary>
            Flee,
        }

        /// <summary>The reviewed three-beat cycle, as an explicit script.
        ///
        /// The owner's reading of AI_069 is that a charge commits its direction on
        /// the state-entry tick and never re-aims, so once it is locked the whole
        /// dodge is "leave the line". The cycle spends one dash on each beat:
        ///
        /// <code>
        ///   beat 0  run away horizontally   + dash away
        ///   beat 1  ascend                  + dash
        ///   beat 2  descend                 + dash
        /// </code>
        ///
        /// The ascend and descend beats are the reason a Shield is in the
        /// loadout: a vertical beat alone leaves along the charge line and never
        /// clears it, but a vertical beat WITH a dash moves diagonally, and the
        /// diagonal is what clears the line. This controller exists to test that
        /// reading rather than assert it -- it is a script, it does not search,
        /// and its result is whatever the fight reports.
        /// </summary>
        private sealed class BeatCycleDodge : IFishronController
        {
            private readonly bool _useDash;
            private readonly DashAim _aim;
            private readonly int _dashAtTimer;
            private int _beat;
            private int _lastState = int.MinValue;
            private bool _dashIssued;

            public BeatCycleDodge(bool useDash, DashAim aim, int dashAtTimer = 1)
            {
                _useDash = useDash;
                _aim = aim;
                _dashAtTimer = dashAtTimer;
            }

            public void Reset()
            {
                _beat = 0;
                _lastState = int.MinValue;
                _dashIssued = false;
            }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                var dashing = IsDashState(world.State);
                if (dashing && !IsDashState(_lastState))
                {
                    // A new charge: it has just committed its line, so this is
                    // the beat the cycle advances on.
                    _beat++;
                    _dashIssued = false;
                }
                _lastState = world.State;

                // Horizontal input always points away from the Boss: it is both
                // the run direction and, on the engine's own terms, the thing
                // that aims a dash. A remembered Sharknado column outranks the
                // Boss because it does not move.
                var away = world.Tornadoes.Count > 0 &&
                    Math.Abs(player.Center.X - world.Tornadoes[0].X) < 240f
                    ? (world.Tornadoes[0].X >= player.Center.X ? -1 : 1)
                    : (boss.Center.X >= player.Center.X ? -1 : 1);
                if (away > 0) controls.Right = true;
                else controls.Left = true;

                if (!dashing)
                {
                    // Between charges: hold altitude on the wings rather than
                    // standing on the floor, so the next charge starts from the
                    // air where the cycle expects it.
                    if (!frame.Grounded) controls.Up = true;
                    return controls;
                }

                switch (_beat % 3)
                {
                    case 1:
                        controls.Up = true;
                        break;
                    case 2:
                        controls.Down = true;
                        break;
                }

                // One dash per charge, fired at the locked-in tick rather than
                // early: the impulse decays over eighteen ticks, so a dash spent
                // before the charge commits is spent before the closest approach.
                // The engine also will not take a request while its own cooldown
                // is running, and this controller does not pretend otherwise --
                // it asks and lets the model answer.
                if (_useDash && !_dashIssued && frame.DashReady &&
                    frame.Dash.DashDelay >= 0 &&
                    world.StateTimer >= _dashAtTimer)
                {
                    controls.Dash = true;
                    _dashIssued = true;
                }
                return controls;
            }
        }

        // ------------------------------------------------------------------ lab
        private static void FishronNoHitLab()
        {
            Console.WriteLine("== fishron no-hit lab ==");
            Console.WriteLine("fidelity: player = PlayerForwardModel (engine-measured), " +
                "boss = AI_069 expert transcription");
            var probe = new PassiveProbe();
            var result = RunFight(probe, 4000, verbose: true);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "passive probe: ticks={0} hits={1} refusal={2} charges={3}",
                result.Ticks, result.Hits, result.Refusal, result.Charges));
            foreach (var line in result.HitLog) Console.WriteLine("  " + line);
            Console.WriteLine();
            RunClimbProbe();
            Console.WriteLine();
            Console.WriteLine("== reviewed three-beat cycle ==");
            Console.WriteLine("  beat 0 run away + dash, beat 1 ascend + dash, " +
                "beat 2 descend + dash");
            foreach (var aim in new[] { DashAim.Flee })
                foreach (var dashAt in new[] { 0, 1, 3, 6, 9, 12, 16 })
                    foreach (var useDash in new[] { false, true })
                    {
                        var fight = RunFight(new BeatCycleDodge(useDash, aim, dashAt),
                            6000, bossOnly: true);
                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "  aim={0,-6} dashAtTimer={1,2} dash={2,-5} " +
                            "ticks={3,5} hits={4,4} charges={5,3} " +
                            "perpAtClosest={6,6:F1} maxPerp={7,6:F1} refusal={8}",
                            aim, dashAt, useDash, fight.Ticks, fight.Hits,
                            fight.Charges, fight.ClosestPerpendicular,
                            fight.MaxPerpendicular, fight.Refusal));
                    }
            Console.WriteLine();
            Console.WriteLine("== one charge, tick by tick (dash on the lock tick) ==");
            var traced = RunFight(new BeatCycleDodge(true, DashAim.Flee, 1), 6000,
                trace: true, traceTicks: 72, maxHits: 3, bossOnly: true);
            Console.WriteLine("  hits=" + traced.Hits + " ticks=" + traced.Ticks);
            foreach (var line in traced.ChargeLog)
                Console.WriteLine("  " + line);
            foreach (var line in traced.HitLog)
                Console.WriteLine("  " + line);
            Console.WriteLine();
            Console.WriteLine("== dodge sweep (lead ticks before charge) ==");
            foreach (var lead in new[] { 0, 5, 10, 15, 20, 25, 30, 40, 60, 120 })
            {
                foreach (var useDash in new[] { false, true })
                {
                    var dodge = new PerpendicularDodge(lead, useDash);
                    var fight = RunFight(dodge, 20000);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  lead={0,3} dash={1,-5} ticks={2,6} hits={3,3} " +
                        "charges={4,4} refusal={5}",
                        lead, useDash, fight.Ticks, fight.Hits, fight.Charges,
                        fight.Refusal));
                    if (fight.Hits > 0 && fight.HitLog.Count > 0)
                        Console.WriteLine("      first hit: " + fight.HitLog[0]);
                }
            }
            Console.WriteLine();
            Console.WriteLine("== trace of the first charge (lead=30, no dash) ==");
            RunFight(new PerpendicularDodge(30, false), 20000, trace: true);

            Console.WriteLine();
            Console.WriteLine("== full fight: geometry dodge, lead sweep ==");
            foreach (var lead in new[] { 0, 10, 20, 30, 60 })
            {
                var fight = RunFight(new GeometryDodge(lead, false), 20000,
                    maxHits: -1);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  lead={0,3} ticks={1,6} hits={2,4} charges={3,3} " +
                    "maxPerp={4,6:F1}",
                    lead, fight.Ticks, fight.Hits, fight.Charges,
                    fight.MaxPerpendicular));
                foreach (var line in fight.HitLog)
                    Console.WriteLine("      " + line);
            }

            Console.WriteLine();
            Console.WriteLine("== ISOLATED boss contact: hover tactic comparison ==");
            foreach (var tactic in new[] { HoverTactic.Climb,
                HoverTactic.HoldGround, HoverTactic.LandBetweenCharges })
            {
                foreach (var lead in new[] { 0, 10, 20, 30 })
                {
                    var fight = RunFight(new GeometryDodge(lead, false, tactic),
                        20000, maxHits: -1, bossOnly: true);
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "  tactic={0,-18} lead={1,3} hits={2,5} charges={3,3} " +
                        "maxPerp={4,6:F1}",
                        tactic, lead, fight.Hits, fight.Charges,
                        fight.MaxPerpendicular));
                }
            }
            Console.WriteLine();
            Console.WriteLine("== best tactic, per-charge detail ==");
            var best = RunFight(new GeometryDodge(20, false,
                HoverTactic.HoldGround), 20000, maxHits: -1, bossOnly: true);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  hits={0} charges={1} maxPerp={2:F1}",
                best.Hits, best.Charges, best.MaxPerpendicular));
            foreach (var line in best.ChargeLog)
                Console.WriteLine("      " + line);

            Console.WriteLine();
            Console.WriteLine("== steady run: banked horizontal speed ==");
            foreach (var jump in new[] { false, true })
            {
                var fight = RunFight(new SteadyRunDodge(jump), 20000,
                    maxHits: -1, bossOnly: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  jump={0,-5} hits={1,5} charges={2,3} maxPerp={3,6:F1}",
                    jump, fight.Hits, fight.Charges, fight.MaxPerpendicular));
            }

            Console.WriteLine();
            Console.WriteLine("== geometry dodge with dash escape ==");
            foreach (var lead in new[] { 0, 10, 20, 30, 45 })
            {
                var fight = RunFight(new GeometryDodge(lead, true), 20000,
                    maxHits: -1, bossOnly: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  lead={0,3} hits={1,5} charges={2,3} maxPerp={3,6:F1}",
                    lead, fight.Hits, fight.Charges, fight.MaxPerpendicular));
            }
            Console.WriteLine();
            Console.WriteLine("== low run (+dash), target altitude sweep ==");
            foreach (var altitude in new[] { 0f, 60f, 150f, 400f })
            {
                foreach (var useDash in new[] { false, true })
                {
                    foreach (var dashAt in new[] { 0, 6, 12 })
                    {
                        var fight = RunFight(new LowRunDashDodge(useDash, dashAt,
                            altitude), 20000, maxHits: -1, bossOnly: true);
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "  alt={0,4:F0} dash={1,-5} at={2,2} hits={3,5} " +
                            "charges={4,3} maxPerp={5,6:F1}",
                            altitude, useDash, dashAt, fight.Hits,
                            fight.Charges, fight.MaxPerpendicular));
                    }
                }
            }
        }

        /// <summary>Measures what holding jump does from the ground, so the
        /// controller is written against the harness's actual numbers instead of
        /// against a hand-derived climb rate.</summary>
        private static void RunClimbProbe()
        {
            Console.WriteLine("== climb probe (hold jump from rest) ==");
            const float floorY = 6000f;
            var world = new FightWorld { FloorY = floorY, BandLeft = 1000f,
                BandRight = 6000f, BossX = -100000f, BossY = -100000f };
            var frame = FishronPlayerStart(3000f, floorY);
            var controls = new PlayerControlFrame { Jump = true };
            for (var i = 0; i < 40; i++)
            {
                PlayerMotionFrame next;
                ForwardModelRefusal refusal;
                if (!PlayerForwardModel.TryAdvance(in frame, in controls,
                        out next, out refusal))
                {
                    Console.WriteLine("  refusal at " + i + ": " + refusal);
                    return;
                }
                frame = next;
                if (i % 2 == 0)
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  t={0} y={1:F2} vy={2:F3} wing={3:F1}",
                        i, frame.Position.Y, frame.Velocity.Y, frame.WingTime));
            }
        }

        /// <summary>The dodge the AI spec points at: a charge commits once, at
        /// state entry, to a straight line aimed at where the player is. So the
        /// only thing that matters is how far the player leaves that line, and
        /// the way to leave it is to be already moving perpendicular when the
        /// charge starts.</summary>
        private sealed class PerpendicularDodge : IFishronController
        {
            private readonly int _lead;
            private readonly bool _useDash;
            private bool _dashIssued;

            public PerpendicularDodge(int lead, bool useDash)
            {
                _lead = lead;
                _useDash = useDash;
            }

            public void Reset() { _dashIssued = false; }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                if (IsDashState(world.State))
                {
                    var dx = player.Center.X - boss.Center.X;
                    var dy = player.Center.Y - boss.Center.Y;
                    var length = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (length < 0.0001f) return controls;
                    var perpX = -dy / length;
                    var perpY = dx / length;
                    if (perpY < 0f && perpX < 0.3f) { perpX = 0.3f; }
                    if (perpX > 0.05f) controls.Right = true;
                    else if (perpX < -0.05f) controls.Left = true;
                    if (perpY < -0.05f) { controls.Jump = true; controls.Up = true; }
                    if (_useDash)
                    {
                        var dashing = frame.Dash.DashDelay < 0;
                        if (!_dashIssued && !dashing && tick > 0)
                        {
                            controls.Dash = true;
                            _dashIssued = true;
                        }
                    }
                    return controls;
                }
                _dashIssued = false;
                var hover = world.State == 0 || world.State == 5 || world.State == 10;
                if (!hover) return controls;
                var remaining = HoverTicks(world) - world.StateTimer;
                if (remaining > _lead) return controls;

                var aimX = player.Center.X - boss.Center.X;
                var aimY = player.Center.Y - boss.Center.Y;
                var aimLength = (float)Math.Sqrt(aimX * aimX + aimY * aimY);
                if (aimLength < 0.0001f) return controls;
                var px = -aimY / aimLength;
                var py = aimX / aimLength;
                if (py < 0f) py = -py;
                if (px * aimX > 0f) px = -px;
                if (px > 0.02f) controls.Right = true;
                else if (px < -0.02f) controls.Left = true;
                if (py > 0.3f && frame.Grounded) controls.Jump = true;
                else if (py > 0.3f) { controls.Jump = true; controls.Up = true; }
                return controls;
            }
        }

        /// <summary>A dodge with an explicit escape direction, so the escape
        /// vector can be swept instead of assumed. `up` is the vertical
        /// component: positive climbs, negative dives. `away` is the horizontal
        /// component: positive runs away from the Boss.</summary>
        private sealed class DirectionalDodge : IFishronController
        {
            private readonly int _lead;
            private readonly float _up;
            private readonly float _away;
            private readonly bool _useDash;

            public DirectionalDodge(int lead, float up, float away, bool useDash)
            {
                _lead = lead;
                _up = up;
                _away = away;
                _useDash = useDash;
            }

            public void Reset() { }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                if (IsDashState(world.State))
                {
                    if (_away > 0f) controls.Right = true;
                    else if (_away < 0f) controls.Left = true;
                    if (_up > 0.3f) { controls.Jump = true; controls.Up = true; }
                    else if (_up < -0.3f && !frame.Grounded) controls.Down = true;
                    if (_useDash && frame.Dash.DashDelay >= 0 && tick > 0)
                        controls.Dash = true;
                    return controls;
                }
                var hover = world.State == 0 || world.State == 5 || world.State == 10;
                if (!hover) return controls;
                var remaining = HoverTicks(world) - world.StateTimer;
                if (remaining > _lead) return controls;
                if (_away > 0f) controls.Right = true;
                else if (_away < 0f) controls.Left = true;
                if (_up > 0.3f) { controls.Jump = true; controls.Up = true; }
                else if (_up < -0.3f && !frame.Grounded) controls.Down = true;
                return controls;
            }
        }

        /// <summary>Geometry-driven dodge with a selectable hover tactic.
        ///
        /// A charge is a straight line aimed at the player's position on the
        /// state-entry tick, so the escape is the component of the player's
        /// motion perpendicular to that line. The hover tactic decides where the
        /// player parks before the charge commits, which is what sets the charge
        /// angle: climbing into the Boss's own tracking column makes the charge
        /// steep, and a steep charge cannot be cleared by climbing.
        /// </summary>
        private sealed class GeometryDodge : IFishronController
        {
            private readonly int _lead;
            private readonly bool _useDash;
            private readonly HoverTactic _tactic;
            private Vec2 _chargeDirection;
            private bool _haveCharge;
            private int _lastState = int.MinValue;
            private bool _dashIssued;

            public GeometryDodge(int lead, bool useDash,
                HoverTactic tactic = HoverTactic.Climb)
            {
                _lead = lead;
                _useDash = useDash;
                _tactic = tactic;
            }

            public void Reset()
            {
                _haveCharge = false;
                _lastState = int.MinValue;
                _dashIssued = false;
            }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                var dashing = IsDashState(world.State);
                if (dashing && !IsDashState(_lastState))
                {
                    _chargeDirection = new Vec2(world.BossVx, world.BossVy);
                    _haveCharge = true;
                    _dashIssued = false;
                }
                _lastState = world.State;

                var px = player.Center.X;
                var py = player.Center.Y;

                if (dashing)
                {
                    var ux = _chargeDirection.X;
                    var uy = _chargeDirection.Y;
                    var l = (float)Math.Sqrt(ux * ux + uy * uy);
                    if (l < 0.0001f) return controls;
                    ux /= l;
                    uy /= l;
                    // Perpendicular escape. The vertical half is forced
                    // downward when the player is airborne, because the wing
                    // cannot push down: Down only bypasses the small fall cap.
                    var perpX = -uy;
                    var perpY = ux;
                    if (perpY < 0f) { perpX = -perpX; perpY = -perpY; }
                    var roomRight = world.BandRight - player.Width - px;
                    var roomLeft = px - world.BandLeft;
                    if (perpX > 0f && roomRight < 160f) perpX = -perpX;
                    else if (perpX < 0f && roomLeft < 160f) perpX = -perpX;
                    if (perpX > 0.05f) controls.Right = true;
                    else if (perpX < -0.05f) controls.Left = true;
                    if (perpY > 0.05f)
                    {
                        controls.Jump = true;
                        controls.Up = true;
                    }
                    // If the only useful escape is downward and the player is
                    // already airborne, stop feeding the wing and let gravity
                    // work instead of holding altitude.
                    else if (!frame.Grounded && frame.Velocity.Y < -1f)
                    {
                        controls.Down = true;
                    }
                    if (_useDash && !_dashIssued && frame.Dash.DashDelay >= 0 &&
                        tick > 0 && Math.Abs(perpX) > 0.5f)
                    {
                        controls.Dash = true;
                        _dashIssued = true;
                    }
                    return controls;
                }

                var hover = world.State == 0 || world.State == 5 || world.State == 10;
                if (!hover) return controls;
                var remaining = HoverTicks(world) - world.StateTimer;
                if (remaining > _lead)
                {
                    if (_tactic == HoverTactic.LandBetweenCharges &&
                        !frame.Grounded)
                        return controls;
                    return controls;
                }

                var spaceRight = world.BandRight - player.Width - px;
                var spaceLeft = px - world.BandLeft;
                var gap = px - boss.Center.X;
                var seekHorizontal = _tactic != HoverTactic.HoldGround;
                if (seekHorizontal)
                {
                    if (gap >= 0f)
                    {
                        if (spaceRight > 80f) controls.Right = true;
                        else controls.Left = true;
                    }
                    else
                    {
                        if (spaceLeft > 80f) controls.Left = true;
                        else controls.Right = true;
                    }
                }
                if (_tactic == HoverTactic.Climb)
                {
                    controls.Jump = true;
                    controls.Up = true;
                }
                return controls;
            }
        }

        private enum HoverTactic
        {
            /// <summary>Climb through the hover so wing speed is already built.
            /// Makes the charge steep.</summary>
            Climb,
            /// <summary>Stay on the runway and take the charge from a shallow
            /// angle, where a jump is a real perpendicular escape.</summary>
            HoldGround,
            /// <summary>Let the wing run out and fall back to the floor
            /// between charges, so every charge starts from a full jump.</summary>
            LandBetweenCharges,
            /// <summary>Run flat out along the runway so full horizontal speed
            /// is already banked when the charge commits, and add a jump during
            /// the charge for the vertical half.</summary>
            SteadyRun,
        }

        /// <summary>Runs flat out along the runway and answers every charge with
        /// a jump. This is the tactic the physics points at: the player's
        /// horizontal speed is the same in air and on ground, a wing cannot be
        /// used to move down, and a charge cannot change direction after it
        /// commits, so a banked 6.75 px/tick is worth more than any wing climb
        /// the player could build from rest inside a 28-tick charge.</summary>
        private sealed class SteadyRunDodge : IFishronController
        {
            private readonly bool _jumpOnCharge;
            private int _runDirection = 1;
            private int _lastState = int.MinValue;

            public SteadyRunDodge(bool jumpOnCharge)
            {
                _jumpOnCharge = jumpOnCharge;
            }

            public void Reset()
            {
                _runDirection = 1;
                _lastState = int.MinValue;
            }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                var px = player.Center.X;
                // Turn around before the wall, never at it.
                var spaceRight = world.BandRight - player.Width - px;
                var spaceLeft = px - world.BandLeft;
                if (spaceRight < 220f) _runDirection = -1;
                else if (spaceLeft < 220f) _runDirection = 1;
                if (_runDirection > 0) controls.Right = true;
                else controls.Left = true;

                var dashing = IsDashState(world.State);
                if (dashing && _jumpOnCharge)
                {
                    // A jump sets velocity.Y straight to -jumpSpeed and refills
                    // the whole flight budget on the next ground contact; a held
                    // wing only adds 0.1 px/tick per tick.
                    var ux = world.BossVx;
                    var uy = world.BossVy;
                    var l = (float)Math.Sqrt(ux * ux + uy * uy);
                    if (l > 0.0001f)
                    {
                        ux /= l;
                        uy /= l;
                        // Perpendicular component of the banked run.
                        var perpX = -uy;
                        var perpY = ux;
                        if (perpY < 0f) { perpX = -perpX; perpY = -perpY; }
                        if (perpX > 0f) { controls.Right = true; controls.Left = false; }
                        else if (perpX < 0f) { controls.Left = true; controls.Right = false; }
                        if (perpY > 0.05f)
                        {
                            controls.Jump = true;
                            controls.Up = true;
                        }
                        else if (!frame.Grounded && frame.Velocity.Y < -1f)
                        {
                            controls.Down = true;
                        }
                    }
                }
                _lastState = world.State;
                return controls;
            }
        }

        /// <summary>Stays low, runs flat out, and spends the dash on the charge.
        ///
        /// Two measured facts drive it. Climbing is counterproductive: the Boss
        /// hovers 200 px above the player and follows, so altitude gain turns a
        /// ~34 degree charge into a ~70 degree one, and the clearance needed to
        /// slip a 150x100 body past a 20x42 one rises from about 80 px to about
        /// 108 px. And a Shield dash is 14.5 px/tick rather than 6.75, which is
        /// the only way the player gets a perpendicular displacement large
        /// enough inside the 28-tick charge.</summary>
        private sealed class LowRunDashDodge : IFishronController
        {
            private readonly bool _useDash;
            private readonly int _dashAtTimer;
            private readonly float _targetAltitude;
            private int _runDirection = 1;

            public LowRunDashDodge(bool useDash, int dashAtTimer,
                float targetAltitude)
            {
                _useDash = useDash;
                _dashAtTimer = dashAtTimer;
                _targetAltitude = targetAltitude;
            }

            public void Reset() { _runDirection = 1; }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                var px = player.Center.X;
                var spaceRight = world.BandRight - player.Width - px;
                var spaceLeft = px - world.BandLeft;
                if (spaceRight < 260f) _runDirection = -1;
                else if (spaceLeft < 260f) _runDirection = 1;
                if (_runDirection > 0) controls.Right = true;
                else controls.Left = true;

                // Hold a low ceiling: any tick above it is spent falling so the
                // next charge starts from a shallow angle.
                var altitude = world.FloorY - (frame.Position.Y + frame.Height);
                if (altitude > _targetAltitude && !frame.Grounded)
                    controls.Down = true;

                if (IsDashState(world.State) && _useDash &&
                    world.StateTimer == _dashAtTimer &&
                    frame.Dash.DashDelay >= 0)
                    controls.Dash = true;
                return controls;
            }
        }

        /// <summary>Stands still. Used to prove the harness actually produces
        /// hits -- a simulator that never reports a hit proves nothing.</summary>
        private sealed class PassiveProbe : IFishronController
        {
            public void Reset() { }
            public PlayerControlFrame Decide(int tick, in PlayerMotionFrame frame,
                PlayerSnapshot player, TargetSnapshot boss, FightWorld world)
            {
                return new PlayerControlFrame();
            }
        }
    }
}
