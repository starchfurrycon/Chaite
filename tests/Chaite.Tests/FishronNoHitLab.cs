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
        // The player's box, from PlayerSnapshot's own default in the fixture.
        private const float PlayerHalfWidth = 10f;
        private const float PlayerHalfHeight = 21f;

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
            /// <summary>Right edge of the enraged band: maxTilesX*16 - 6400 in
            /// the native predicate. The fixture runway sits well inside the
            /// middle of a world, so the native X term never fires here; it is
            /// carried explicitly rather than implied, because the predicate is
            /// what the enraged charge speed is gated on.</summary>
            public float EnrageBandRight = 9999999f;
            /// <summary>worldSurface * 16 in the native predicate.</summary>
            public float EnrageSurfaceY = 9999999f;
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
        private static PlayerMotionFrame FishronPlayerStart(float x, float floorY,
            float jumpSpeed = 5.01f, float wingTimeMax = 150f, bool autoJump = false)
        {
            var jump = new JumpSnapshot
            {
                Known = true,
                RemainingTicks = 15,
                Speed = jumpSpeed,
                Height = 15,
                ReleaseReady = true,
                CloudAvailable = false,
                CloudEnabled = false,
                AutoJump = autoJump,
            };
            var flight = new FlightSnapshot
            {
                Known = true,
                WingsLogic = 45,
                RocketBoots = 0,
                WingTime = wingTimeMax,
                WingTimeMax = (int)wingTimeMax,
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
            // num3, per the native cascade. Difficulty is expert throughout.
            //
            // State 0 is only ever observed with ai[0] == 0, so flag3
            // (ai[0] > 4) is false and flag5 && !flag3 reduces to
            // ai[3] < 10. For the dash sequences 0..8 that is the 30-tick
            // reduced cadence; sequences 10 and 11 are the attack markers
            // that follow the dash group, where flag5 is false and the base
            // 40 applies. Both arms used to be written out as 30.
            //
            // State 5 is the phase-two hover and carries ai[0] == 5, so
            // flag3 is true: flag3 && flag5 gives expert 40 for the dash
            // sequences 0..5, and the base 40 for the two attack markers.
            // State 10 carries ai[0] == 10, so flag4 makes num3 a flat 30.
            if (world.Enraged) return EnragedHoverTicks;
            switch (world.State)
            {
                case 0: return world.AttackCounter < 10 ? 30 : 40;
                case 5: return 40;
                case 10: return 30;
                default: return 30;
            }
        }

        private static void AdvanceBoss(FightWorld world, PlayerMotionFrame player)
        {
            var pc = new Vec2(player.Position.X + player.Width * 0.5f,
                player.Position.Y + player.Height * 0.5f);
            var bc = world.BossCenter;

            // Enrage, transcribed from the native predicate. The pinned form is
            //   y < 800 || y > worldSurface*16 ||
            //   (x > 6400 && x < maxTilesX*16 - 6400)
            // -- the X term is an AND, meaning "inside the far-from-spawn
            // band", not an OR. The earlier form here had it as an OR against
            // the fixture's own runway, which made the boss permanently enraged
            // on a runway where the native predicate never fires at all.
            var enragedByX = player.Position.X > OceanBandPixels &&
                player.Position.X < world.EnrageBandRight;
            var flag6 = player.Position.Y < SkyEnrageCeiling ||
                player.Position.Y > world.EnrageSurfaceY || enragedByX;
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
                        // The burst length is not a flat number. Native AI_069
                        // sets ai[3] and num2 = flag3 ? 3 : 5, so phase one
                        // lunges FIVE times per burst and phase two only three,
                        // before a projectile attack resets the counter. The
                        // wiki states the same independently: "lunges at the
                        // player exactly five times ... before using one of two
                        // projectile attacks" in phase one, and "three times
                        // instead of five" in phase two. A flat nine was used
                        // here, which is neither, and it is why the run reported
                        // a contact at "charge 5" with a timer past the
                        // hover-sequence bound: the state really was mid-burst
                        // at a position the flat cycle put it.
                        var burst = world.Phase == FightPhase.One ? 5 : 3;
                        if (world.AttackCounter >= burst) world.AttackCounter = 0;
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
            // The spawn site has to honour the switch too, not just the update
            // loop: with only the update guarded, the attack still created
            // bubbles every four ticks and they were removed the moment they
            // were added, so a "bubbles off" run still logged bubble hits from
            // the same tick the spray fired.
            if (!world.BubblesEnabled) return;
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
                // BubblesEnabled was declared and never read, so the switch did
                // nothing and every "bubbles off" experiment silently ran with
                // them on. Tornadoes already guard this way.
                if (!world.BubblesEnabled) { world.Bubbles.RemoveAt(i); continue; }
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
        /// <summary>When set, every tick of a charge prints its along-line
        /// projection, the boss's, the perpendicular clearance and the room
        /// left over. A per-charge summary cannot say WHICH tick went wrong,
        /// and the interesting failures are one or two ticks wide.</summary>
        private static bool TraceCharge;
        /// <summary>When positive, the charge trace prints only this charge
        /// ordinal. Charge 5 is a fixed obstacle across every jump speed from
        /// 6.41 to 8.91, so isolating its trace is what makes that readable.</summary>
        private static int TraceOnlyCharge;

        private static FightResult RunFight(IFishronController controller,
            int maxTicks, bool verbose = false, bool trace = false,
            int traceTicks = 70, int maxHits = 1, bool bossOnly = false,
            bool bubbles = false, bool? tornados = null, float startX = 3300f,
            float jumpSpeed = 5.01f, float wingTimeMax = 150f,
            bool autoJump = false, string hoverVariant = "none")
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
                // bossOnly removes every projectile; `bubbles` re-enables just
                // the bubble spray on top of that. Written as !bossOnly || bubbles
                // it was true on every call that did not set bossOnly, so the
                // flag could not turn bubbles OFF at all and every isolation
                // experiment reported the same six bubble hits.
                BubblesEnabled = bubbles && !bossOnly,
                TornadoesEnabled = tornados ?? !bossOnly,
                BossContactEnabled = true,
            };
            var frame = FishronPlayerStart(startX, floorY, jumpSpeed, wingTimeMax,
                autoJump);
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
            var chargeCorridorFirst = -1;
            var chargeCorridorLast = -1;
            var chargeDangerTicks = 0;
            // Charge geometry that has to survive from tick to tick. These are
            // deliberately declared out here: as loop-body locals they were
            // re-zeroed on every tick, so the direction read back as zero on
            // every tick after the one that set it.
            var chargeUx = 0f;
            var chargeUy = 0f;
            var chargeLength = 0f;
            var bossAlong = 0f;
            var requiredClearance = 0f;
            var chargeNearTick = -1;
            var chargeNearBossX = 0f;
            var chargeNearBossY = 0f;
            var chargeNearPlayerX = 0f;
            var chargeNearPlayerY = 0f;
            var chargeHit = false;
            var chargeHitTick = -1;
            var chargeLockAlong = 0f;
            var chargeLockGap = 0f;
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
                    // Log the charge that has just ended, then start the new
                    // one. The angle, the required clearance and the corridor
                    // window are all facts about that charge, so they are
                    // captured before the new line overwrites them.
                    if (chargeOrdinal > 1)
                    {
                        var angleRad = Math.Atan2(Math.Abs(chargeLine.Y),
                            Math.Abs(chargeLine.X));
                        var endedRequired = RequiredClearance(chargeUx, chargeUy);
                        chargeEvents.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "charge {0,3} start {1,5} angle {2,5:F1}deg " +
                            "req {3,5:F0} maxPerp {4,6:F1} perpAtClosest " +
                            "{5,6:F1} closest {6,5:F0} hit={7,-5} at {16,4} " +
                            "lockAlong {17,6:F0} lockGap {18,6:F0} " +
                            "danger={8,3} " +
                            "corridor=[{9},{10}] near tick {11} " +
                            "boss=({12:F0},{13:F0}) ply=({14:F0},{15:F0})",
                            chargeOrdinal - 1, chargeStartTick,
                            angleRad * 180.0 / Math.PI, endedRequired,
                            chargeMaxPerpendicular, chargeClosestPerpendicular,
                            chargeClosestDistance, chargeHit, chargeDangerTicks,
                            chargeCorridorFirst, chargeCorridorLast, chargeNearTick,
                            chargeNearBossX, chargeNearBossY, chargeNearPlayerX,
                            chargeNearPlayerY, chargeHitTick, chargeLockAlong,
                            chargeLockGap));
                        if (chargeClosestPerpendicular < closestPerpendicular)
                            closestPerpendicular = chargeClosestPerpendicular;
                    }
                    chargeOrdinal++;
                    chargeLine = new Vec2(world.BossVx, world.BossVy);
                    chargeOrigin = world.BossCenter;
                    chargeLength = (float)Math.Sqrt(chargeLine.X * chargeLine.X +
                        chargeLine.Y * chargeLine.Y);
                    chargeUx = chargeLength > 0.0001f ? chargeLine.X / chargeLength : 0f;
                    chargeUy = chargeLength > 0.0001f ? chargeLine.Y / chargeLength : 0f;
                    chargeStartTick = world.Tick;
                    chargePlayer = new Vec2(frame.Position.X + frame.Width * 0.5f,
                        frame.Position.Y + frame.Height * 0.5f);
                    chargeLockAlong = (chargePlayer.X - chargeOrigin.X) * chargeUx +
                        (chargePlayer.Y - chargeOrigin.Y) * chargeUy;
                    chargeLockGap = chargeLength -
                        Math.Abs(chargePlayer.X - chargeOrigin.X) * Math.Abs(chargeUx) -
                        Math.Abs(chargePlayer.Y - chargeOrigin.Y) * Math.Abs(chargeUy);
                    chargeMaxPerpendicular = 0f;
                    chargeClosestDistance = float.MaxValue;
                    chargeClosestPerpendicular = 0f;
                    chargeHit = false;
                    chargeHitTick = -1;
                    chargeCorridorFirst = -1;
                    chargeCorridorLast = -1;
                    chargeDangerTicks = 0;
                    hadCharge = true;
                }
                if (hadCharge && isDash)
                {
                    // The player's position AFTER this tick's movement. The
                    // frame is advanced above, so reading it before that would
                    // measure the previous tick's geometry against this tick's
                    // boss position -- which silently reported the closest
                    // approach as zero while the peak read correctly.
                    var playerX = frame.Position.X + frame.Width * 0.5f;
                    var playerY = frame.Position.Y + frame.Height * 0.5f;
                    if (chargeLength > 0.0001f)
                    {
                        // Signed projection of the player onto the line, and
                        // the perpendicular distance, both measured from the
                        // line's own origin.
                        var px = playerX - chargeOrigin.X;
                        var py = playerY - chargeOrigin.Y;
                        numerator = px * chargeUx + py * chargeUy;
                        perpendicular = Math.Abs(px * -chargeUy + py * chargeUx);
                        bossAlong = ((world.BossX + BossWidth * 0.5f - chargeOrigin.X) *
                            chargeUx +
                            (world.BossY + BossHeight * 0.5f - chargeOrigin.Y) * chargeUy);
                        requiredClearance = RequiredClearance(chargeUx, chargeUy);
                    }
                    if (perpendicular > chargeMaxPerpendicular)
                        chargeMaxPerpendicular = perpendicular;
                    if (perpendicular > maxPerpendicular)
                        maxPerpendicular = perpendicular;
                    if (TraceCharge &&
                        (TraceOnlyCharge <= 0 || chargeOrdinal == TraceOnlyCharge))
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "    c{0} t{1,3} along {2,7:F1} boss {3,7:F1} " +
                            "perp {4,6:F1} req {5,4:F0} room {6,7:F1} " +
                            "ply=({7:F0},{8:F0}) v=({9:F1},{10:F1}) gnd={11} " +
                            "wing={12:F0} ctrl={13}{14}{15}{16}{17}",
                            chargeOrdinal, world.Tick, numerator, bossAlong,
                            perpendicular, requiredClearance,
                            perpendicular - requiredClearance,
                            playerX, playerY, frame.Velocity.X,
                            frame.Velocity.Y, frame.Grounded, frame.WingTime,
                            controls.Left ? "L" : "-", controls.Right ? "R" : "-",
                            controls.Up ? "U" : "-", controls.Down ? "D" : "-",
                            controls.Jump ? "J" : "-"));
                    // The closest approach is where the player's and the boss's
                    // projections onto the charge line meet -- not where the two
                    // bodies happen to be nearest each other in general, which
                    // can be somewhere the charge never reaches.
                    var approach = Math.Abs(bossAlong - numerator);
                    if (approach < chargeClosestDistance)
                    {
                        chargeClosestDistance = approach;
                        chargeClosestPerpendicular = perpendicular;
                        // The exact state at the moment of closest approach: the
                        // one tick that decides the charge. Recorded because a
                        // clearance figure alone cannot say whether the player
                        // was beside the boss or in front of it.
                        chargeNearTick = world.Tick;
                        chargeNearBossX = world.BossX;
                        chargeNearBossY = world.BossY;
                        chargeNearPlayerX = frame.Position.X;
                        chargeNearPlayerY = frame.Position.Y;
                    }
                    if (chargeClosestPerpendicular < closestPerpendicular)
                        closestPerpendicular = chargeClosestPerpendicular;
                    // Note: the fight-wide figure is NOT the minimum over every
                    // tick. On the tick a charge commits, the player is on the
                    // line by construction, so the perpendicular is zero there
                    // and a running minimum over ticks is always zero. The
                    // meaningful fight-wide figure is the smallest
                    // per-charge clearance AT the closest approach, which is
                    // what each charge contributes to.
                    // The collision corridor: the set of ticks on which the
                    // player's box can touch the boss's box at all. A dodge does
                    // not have to keep clearance for the whole charge, only
                    // through this window, so the window -- not the peak and not
                    // the global minimum -- is what a dash has to be timed
                    // against. Measured with the engine's own SAT clearance for
                    // two axis-aligned boxes, not with a hand-picked number.
                    if (InsideCollisionCorridor(perpendicular, chargeUx, chargeUy,
                            numerator, bossAlong))
                    {
                        chargeDangerTicks++;
                        if (chargeCorridorFirst < 0) chargeCorridorFirst = world.Tick;
                        chargeCorridorLast = world.Tick;
                    }
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
                        "perp={15,6:F1} sgn={16,7:F1} need={17,5:F0} req={18,5:F0}",
                        world.Tick, world.State, world.StateTimer,
                        world.BossCenter.X, world.BossCenter.Y, world.BossVx,
                        world.BossVy, frame.Position.X + frame.Width * 0.5f,
                        frame.Position.Y + frame.Height * 0.5f, frame.Velocity.X,
                        frame.Velocity.Y, controls.Left ? "L" : "-",
                        controls.Right ? "R" : "-", controls.Jump ? "J" : "-",
                        controls.Dash ? "D" : "-", perpendicular,
                        isDash ? (frame.Position.X + frame.Width * 0.5f -
                            (world.BossX + BossWidth * 0.5f)) * -chargeUy +
                            (frame.Position.Y + frame.Height * 0.5f -
                            (world.BossY + BossHeight * 0.5f)) * chargeUx : 0f,
                        isDash ? numerator : 0f, requiredClearance));
                AdvanceThreats(world, frame);

                if (world.BossContactEnabled && world.ImmuneTicks == 0 &&
                    Overlaps(frame.Position, frame.Width, frame.Height,
                        new Vec2(world.BossX, world.BossY), BossWidth,
                        BossHeight))
                {
                    chargeHit = true;
                    if (chargeHitTick < 0) chargeHitTick = world.Tick;
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
            // The charge the fight ended on never gets a "next charge" to log
            // it, and that is precisely the charge that took the hit. Recorded
            // here so the account of a losing fight is complete.
            if (chargeOrdinal > 1)
            {
                var angleRad = Math.Atan2(Math.Abs(chargeLine.Y),
                    Math.Abs(chargeLine.X));
                chargeEvents.Add(string.Format(CultureInfo.InvariantCulture,
                    "charge {0,3} start {1,5} angle {2,5:F1}deg req {3,5:F0} " +
                    "maxPerp {4,6:F1} perpAtClosest {5,6:F1} closest {6,5:F0} " +
                    "hit={7,-5} at {11,4} danger={8,3} corridor=[{9},{10}] (final)",
                    chargeOrdinal - 1, chargeStartTick, angleRad * 180.0 / Math.PI,
                    RequiredClearance(chargeUx, chargeUy), chargeMaxPerpendicular,
                    chargeClosestPerpendicular, chargeClosestDistance, chargeHit,
                    chargeDangerTicks, chargeCorridorFirst, chargeCorridorLast,
                    chargeHitTick));
                if (chargeClosestPerpendicular < closestPerpendicular)
                    closestPerpendicular = chargeClosestPerpendicular;
            }
            return new FightResult
            {
                Ticks = world.Tick,
                Hits = world.Hits,
                Refusal = "None",
                HitLog = world.HitLog,
                Charges = world.ChargeCount,
                MaxPerpendicular = maxPerpendicular,
                ClosestPerpendicular = closestPerpendicular == float.MaxValue
                    ? 0f : closestPerpendicular,
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

        /// <summary>
        /// Whether the player's box can touch the boss's box on this tick, given
        /// the charge line and the player's position relative to it.
        ///
        /// This is the collision corridor, and it is what a dash actually has to
        /// be timed against. The player is 20x42 and the boss 150x100, both
        /// axis-aligned, so "can they touch" is the engine's own separating-axis
        /// test on the two axis projections. Writing it out rather than using a
        /// rule of thumb matters: a hand-picked clearance number would bake the
        /// answer into the measurement.
        ///
        /// Along the charge line the boss's own 150-wide box means a touch is
        /// possible for a window wider than the player's body, which is why the
        /// danger window is not simply "the ticks where the projections are
        /// equal". Across the line, the boss half-extent projected onto the
        /// normal is what sets the required clearance, and it grows as the
        /// charge gets steeper -- the half-width dominates a shallow charge and
        /// the half-height a steep one.
        /// </summary>
        private static bool InsideCollisionCorridor(float perpendicular,
            float ux, float uy, float playerAlong, float bossAlong)
        {
            // Across the line: the boss's half-extent projected onto the normal
            // is |halfWidth * uy| + |halfHeight * ux| for an axis-aligned box
            // under a separating axis along that normal, plus the player's own
            // half-extent along the same axis.
            var bossHalfAcross = Math.Abs(BossWidth * 0.5f * uy) +
                Math.Abs(BossHeight * 0.5f * ux);
            var playerHalfAcross = Math.Abs(PlayerHalfWidth * uy) +
                Math.Abs(PlayerHalfHeight * ux);
            if (perpendicular >= bossHalfAcross + playerHalfAcross) return false;
            // Along the line: both bodies are point centres on this axis, so the
            // test compares their two projections and their two half-extents.
            // Comparing the player against the charge ORIGIN instead would be
            // wrong the moment the boss has flown past it, which on a 476px
            // charge is almost immediately.
            var bossHalfAlong = Math.Abs(BossWidth * 0.5f * ux) +
                Math.Abs(BossHeight * 0.5f * uy);
            var playerHalfAlong = Math.Abs(PlayerHalfWidth * ux) +
                Math.Abs(PlayerHalfHeight * uy);
            return Math.Abs(playerAlong - bossAlong) <=
                bossHalfAlong + playerHalfAlong;
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

        /// <summary>The clearance a charge demands, on the engine's own terms.
        ///
        /// The boss is a 150x100 axis-aligned box moving along the charge
        /// direction; the player is a 20x42 box. A contact is possible while the
        /// player's centre is within the sum of the two boxes' half-extents
        /// projected onto the line's normal. That sum is the entire requirement:
        /// nothing here is a tuned threshold. A shallow charge is governed by the
        /// boss's half-width, a steep one by its half-height, and the crossing
        /// point is at about 54 degrees.
        /// </summary>
        private static float RequiredClearance(float ux, float uy)
        {
            var bossHalf = Math.Abs(BossWidth * 0.5f * uy) +
                Math.Abs(BossHeight * 0.5f * ux);
            var playerHalf = Math.Abs(PlayerHalfWidth * uy) +
                Math.Abs(PlayerHalfHeight * ux);
            return bossHalf + playerHalf;
        }

        /// <summary>Closed-loop escape from a committed charge.
        ///
        /// The owner's reading is that a charge commits its line and never
        /// re-aims, so the only thing that matters is the player's displacement
        /// along the line's normal. This controller therefore steers on that
        /// coordinate directly instead of walking a fixed beat schedule: it works
        /// out which way along the normal the player is already leaving, and then
        /// pushes that way for the whole charge -- up if the clearance grows
        /// upward, down if it grows downward.
        ///
        /// The vertical input is what moves along the normal; the horizontal
        /// input is free to keep running away from the boss, which is also what
        /// aims the dash, so the two do not fight each other. A dash is spent
        /// once per charge, timed by how far away the boss still is rather than
        /// by a fixed tick, because its impulse decays over eighteen ticks and
        /// what matters is that the decay covers the closest approach.
        /// </summary>
        private sealed class CorridorEscape : IFishronController
        {
            private readonly bool _useDash;
            private readonly float _dashLead;
            private readonly int _dashAtTimer;
            private readonly bool _climb;
            private readonly float _climbAbove;
            private readonly float _dashAim;
            private readonly int _jumpPulse;
            private readonly float _climbCap;
            private readonly float _hoverDescend;
            private readonly int _preposition;
            private readonly string _hoverVariant;
            private readonly float _lead;
            private int _lastState = int.MinValue;
            private bool _dashIssued;
            private Vec2 _ux = new Vec2(1f, 0f);

            public CorridorEscape(bool useDash, float dashLead, int dashAtTimer = 0,
                bool climb = false, float lead = 0f, float climbAbove = 0.75f,
                float dashAim = 0.85f, int jumpPulse = 0, float climbCap = 420f,
                float hoverDescend = 160f, int preposition = 0,
                string hoverVariant = "none")
            {
                _useDash = useDash;
                _dashLead = dashLead;
                _dashAtTimer = dashAtTimer;
                _climb = climb;
                _lead = lead;
                _climbAbove = climbAbove;
                _dashAim = dashAim;
                _jumpPulse = jumpPulse;
                _climbCap = climbCap;
                _hoverDescend = hoverDescend;
                _preposition = preposition;
                _hoverVariant = hoverVariant;
            }

            public void Reset()
            {
                _lastState = int.MinValue;
                _dashIssued = false;

                _ux = new Vec2(1f, 0f);
            }

            public PlayerControlFrame Decide(int tick,
                in PlayerMotionFrame frame, PlayerSnapshot player,
                TargetSnapshot boss, FightWorld world)
            {
                var controls = new PlayerControlFrame();
                var dashing = IsDashState(world.State);
                if (dashing && !IsDashState(_lastState))
                {
                    _dashIssued = false;

                    _ux = Normalize(world.BossVx, world.BossVy);
                }
                _lastState = world.State;

                var away = boss.Center.X >= player.Center.X ? -1 : 1;

                // Detonating Bubbles are the second threat class and they need
                // their own answer. A bubble is committed on spawn -- it aims at
                // the player's position at that instant and then only bends at
                // (v*40 + toPlayer*20)/41 per tick, about 0.5 rad/tick, so over
                // its 150-tick life it does eventually curve back. What it
                // cannot do quickly is change which SIDE of its line the player
                // is on, so the useful escape is lateral, exactly like a charge.
                // This overrides the run-away-from-the-boss bit because a bubble
                // parked between the player and the boss is precisely how the
                // earlier runs died: the player ran straight into a charging
                // bubble column while running away from the boss.
                var bubbleSide = 0;
                var bubbleDist = float.MaxValue;
                foreach (var bubble in world.Bubbles)
                {
                    if (bubble.Detonated) continue;
                    var bx = bubble.Position.X - player.Center.X;
                    var by = bubble.Position.Y - player.Center.Y;
                    var bl = (float)Math.Sqrt(bx * bx + by * by);
                    // Imminent only. The first version steered away from any
                    // bubble within 320 px, which during the eighty-tick spray
                    // is always, so it overrode the charge escape for the whole
                    // fight and the charge dodge regressed from zero boss hits
                    // to six. A bubble that is not yet close cannot out-turn
                    // the player anyway, and the ones that matter detonate
                    // within about twenty px.
                    if (bl > 150f || bl >= bubbleDist) continue;
                    var vx = bubble.Velocity.X;
                    var vy = bubble.Velocity.Y;
                    var vl = (float)Math.Sqrt(vx * vx + vy * vy);
                    if (vl < 0.01f) continue;
                    // Signed perpendicular of the player about the bubble's
                    // heading: the side the player is already on is the side to
                    // keep, because a bubble bends at only about 0.5 rad/tick
                    // and cannot follow a lateral crossing quickly.
                    bubbleDist = bl;
                    var perp = bx * (-vy / vl) + by * (vx / vl);
                    bubbleSide = perp >= 0f ? 1 : -1;
                }
                // Bubbles are NOT a standing threat in the real fight: NPC 371
                // is a one-hit projectile, and a run that only models movement
                // dies to them purely because nothing shoots back. The
                // controller still nudges laterally when one is about to
                // detonate within 150 px, but that must never outrank the
                // charge escape for the whole fight -- doing so regressed the
                // charge dodge from zero boss contacts to six.
                if (bubbleSide != 0 && dashing)
                {
                    controls.Left = bubbleSide < 0;
                    controls.Right = bubbleSide > 0;
                }
                else if (away > 0) controls.Right = true;
                else controls.Left = true;

                // Hover is the safe window, so it is spent resetting altitude.
                // Getting high is easy and getting back down is the part that
                // costs a fight: the ascent that clears a shallow charge leaves
                // the player above the boss, which is the worst place to be when
                // the next charge locks, because the lock aims at the player and
                // a charge taken from below is steeper. The cap is therefore
                // measured from the FLOOR, not in world Y -- an earlier version
                // compared player.Center.Y against a bare -380, which is above
                // any real arena origin and so was true on every tick.
                if (!dashing)
                {
                    if (!frame.Grounded &&
                        player.Center.Y > world.FloorY - _hoverDescend)
                        controls.Up = true;

                    // The surviving contacts sit in the hover's last frames
                    // (state 0, seq 10, timer 14-18), which is the transition
                    // into a charge rather than the charge itself. During those
                    // frames the controller has no _ux yet -- it only learns the
                    // lock direction on the state change -- so the entire
                    // hover escape is this branch. Pre-positioning is therefore
                    // the only thing that can act here, and it is what the
                    // contact cluster says is missing.
                    //
                    // The charge will lock along the boss-to-player direction,
                    // and the escape is perpendicular to that, so the useful
                    // move is to stand off the boss's approach line: put the
                    // player on whichever side of it already has more room.
                    if (_preposition > 0 || !string.IsNullOrEmpty(_hoverVariant) &&
                        _hoverVariant != "none")
                    {
                        var ax = boss.Center.X - player.Center.X;
                        var ay = boss.Center.Y - player.Center.Y;
                        var al = (float)Math.Sqrt(ax * ax + ay * ay);
                        if (al > 1f)
                        {
                            // Approach direction, the future charge line.
                            var ux = ax / al;
                            var uy = ay / al;
                            // Signed distance from that line, normal (-uy, ux).
                            var sd = (player.Center.X - boss.Center.X) * -uy +
                                (player.Center.Y - boss.Center.Y) * ux;
                            var want = sd >= 0f ? 1f : -1f;
                            var mode = _preposition > 0
                                ? (_preposition >= 2 ? "both" : "horiz")
                                : _hoverVariant;
                            var gain = Math.Abs(ux) * want;
                            if (mode == "horizFlip") gain = -gain;
                            if (mode == "horiz" || mode == "horizFlip" ||
                                mode == "both")
                            {
                                controls.Right = gain > 0f;
                                controls.Left = gain < 0f;
                            }
                            if (mode == "vert" || mode == "both")
                            {
                                var vertical = Math.Abs(uy) * want;
                                if (vertical < 0f) controls.Down = true;
                                else if (vertical > 0f) controls.Up = true;
                            }
                            if (mode == "away")
                            {
                                controls.Right = ux < 0f;
                                controls.Left = ux > 0f;
                            }
                        }
                    }
                    return controls;
                }

                // Which way along the normal to leave, chosen by which side
                // needs LESS travel rather than by which side happens to be
                // positive. The corridor is symmetric about the charge line and
                // |signed| is exactly how far the player already is from it, so
                // the near side always costs less:
                //
                //   need = requiredClearance - |signed|
                //
                // Taking the sign of `signed` instead makes the player travel
                // the LONG way round whenever it is on the far side, and that is
                // what broke charge 8 of the bubble-free run. That charge was
                // shallow (15.2 deg, |ux|=0.965, requirement 91 px) and there
                // were only nine ticks before impact, so a 91 px ascent needed
                // 10 px/tick; escaping the long way round, about 101 px, needed
                // more than the available time outright.
                //
                // Being exactly on the line has no near side, so the sign of
                // the existing displacement is used as the tie-break.
                var dx = player.Center.X - boss.Center.X;
                var dy = player.Center.Y - boss.Center.Y;
                var signed = dx * -_ux.Y + dy * _ux.X;
                var need = RequiredClearance(_ux.X, _ux.Y) - Math.Abs(signed);
                var escapeDir = Math.Abs(signed) > 0.5f ? Math.Sign(signed) : 1f;
                var escapeDown = escapeDir > 0f;
                var altitude = world.FloorY - player.Center.Y;
                // The floor truncates the downward escape: the player cannot
                // travel below the ground, so a downward need that large is
                // really an upward one.
                if (escapeDown && need > altitude * 0.5f) escapeDown = false;

                // Committing the side once per charge was tried twice and is
                // WORSE, so it is not done. Holding the first tick's side took
                // the bubble-free run from 921 ticks to 116, and holding a
                // side chosen by which has legal room took it to 114. The flip
                // this was meant to cure -- the steep charge turning from
                // L-U-J to L--D- at tick 508 and landing three px short -- is
                // therefore adaptive rather than harmful: recomputing follows
                // the player as it crosses, and freezing it strands the run far
                // earlier. The three px must come from somewhere else.
                if (escapeDown) controls.Down = true;
                else controls.Up = true;

                // The dash's 172 px is HORIZONTAL, so it only helps if it is
                // taken towards the side being escaped. Steering body-left or
                // body-right purely to get away from the boss can therefore
                // spend the dash along the charge line, where it buys no
                // clearance at all, or even straight into the corridor.
                //
                // Since the normal is (-uy, ux), its horizontal component is
                // -uy, so the horizontal direction that increases the escape is
                // sign(-uy * escapeDir). Splitting the two sources this way is
                // what closes the arithmetic for the steep family:
                //
                //   dash  172 * |uy|   (a 37.9 deg charge: 172*0.615 = 105.8)
                //   climb  46 * |ux|   (about ten ticks at 4.6:  46*0.788 = 36.2)
                //   total                       142.0  vs requirement 108
                //
                // Neither term alone clears it -- the dash alone is 105.8 and
                // the climb alone is 36.2 -- but together they do, and only if
                // the dash is aimed at the escape side rather than merely away
                // from the boss.
                // ...and it is only worth aiming on a STEEP charge. The dash
                // delivers 172*|uy| of clearance and the climb 46*|ux|, so the
                // two terms swap dominance at |ux| = 0.79 (about 38 deg).
                // Steering horizontally on a shallow charge spends the dash
                // where it buys almost nothing while pulling the player off the
                // vertical line the climb actually needs: applying it to charge
                // 8 of the run (15.2 deg, requirement 91) dropped its clearance
                // from a passing value to 59.2 and turned a clean charge into
                // the run's first contact. Charge 5 (33.7 deg) went the other
                // way, 142.8 -> 195.6. The gate therefore sits at |ux| < 0.85,
                // where the dash term starts to dominate.
                if (Math.Abs(_ux.X) < _dashAim && Math.Abs(_ux.Y) > 0.15f)
                {
                    var dashToward = -_ux.Y * escapeDir > 0f;
                    controls.Right = dashToward;
                    controls.Left = !dashToward;
                }

                // Wing ascent and the dash are two independent sources of
                // perpendicular displacement and they answer opposite charges.
                // The dash is a horizontal impulse: for a steep charge -- the
                // line running up-and-back, normal mostly horizontal -- it
                // delivers most of its 172 px straight along the normal. For a
                // shallow charge the normal is nearly vertical and the dash
                // contributes almost nothing, so height is the whole escape.
                //
                // Which branch applies is decided by how vertical the normal
                // is. The charge line is (ux, uy), so its normal is (-uy, ux)
                // and the normal's VERTICAL component is |ux|: a shallow line
                // (ux near 1) has an almost straight-up normal, and a steep one
                // (ux near 0.5) has a mostly horizontal normal.
                //
                // This is NOT the same question as "is the escape direction
                // up". Both charge 3 (57.4 deg, |ux|=0.54) and charge 4
                // (51.2 deg, |ux|=0.63) escaped upwards, yet routing both to
                // the climb left 4 fifty-six px short of the 111 px it needed
                // while 3 cleared its 110 px by sixty. The steep pair is what
                // the dash is for, so they must not also be given the climb.
                //
                // Both bits are required when climbing and they are NOT the
                // The threshold is set from the measured climb rate rather than
                // guessed. Wing ascent settles at 4.6 px/tick -- DemonThrust
                // caps at jump.Speed, and the lab fixture's jump speed is 5.01,
                // which the trace confirms at a constant v.Y of -4.6 for the
                // whole climb. Measured perpendicular gain is therefore
                //
                //   4.6 * |normal.Y| = 4.6 * |ux|
                //
                // px/tick, so the ticks a climb needs are
                // RequiredClearance(ux,uy) / (4.6*|ux|), and a charge lasts 12
                // to 21 ticks. Working that against the measured table:
                //
                //   10 deg -> 18.7 ticks (fits)    25 deg -> 24.1 (does not)
                //   15 deg -> 20.4 ticks (marginal) 40 deg -> 30.9 (no)
                //
                // so the climb only wins for shallow charges and the crossing
                // sits near |ux| = 0.91, about 25 degrees. The previous 0.75 let
                // the climb cover everything up to 41 degrees, which is exactly
                // the 34-44 degree family this run kept dying to.
                var normalVertical = Math.Abs(_ux.X);
                if (_climb && !escapeDown && normalVertical > _climbAbove)
                {
                    controls.Jump = _jumpPulse <= 0 ||
                        world.Tick % _jumpPulse == 0;
                    controls.Up = true;
                    if (altitude > _climbCap) controls.Up = false;
                }

                if (!_useDash || _dashIssued || !frame.DashReady ||
                    frame.Dash.DashDelay < 0) return controls;
                if (world.StateTimer < _dashAtTimer) return controls;

                // Distance along the line still to run before the closest
                // approach. The dash is spent once that is inside the lead, so
                // the eighteen-tick decay is still live when the boss arrives.
                var toBoss = (frame.Position.X + frame.Width * 0.5f -
                    boss.Center.X) * _ux.X + (frame.Position.Y + frame.Height * 0.5f -
                    boss.Center.Y) * _ux.Y;
                if (toBoss > _dashLead) return controls;
                controls.Dash = true;
                _dashIssued = true;
                return controls;
            }

            private static Vec2 Normalize(float x, float y)
            {
                var length = (float)Math.Sqrt(x * x + y * y);
                return length > 0.0001f ? new Vec2(x / length, y / length)
                    : new Vec2(1f, 0f);
            }
        }

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

        /// <summary>
        /// One admitted wing set. The two admitted routes differ in vertical
        /// mobility, not merely in taste, so they get two parameter sets rather
        /// than one strong-wing set that the weak wing is then expected to
        /// imitate. FormulaRouteCatalog.Select is the admission rule being
        /// mirrored: both routes gate on the Frog Leg, and they split on
        /// wingItem -- 761 (Fairy Wings) is the weak route, IsStrongWingItem is
        /// the strong route.
        ///
        /// JumpSpeed is derived, not chosen: 5.01 base (Player.cs:2513) plus the
        /// Frog Leg's 2.4 (19741) plus the 1.6 the Amphibious Boots contribute
        /// through the 14523 equipment family. ClimbRate is the measured
        /// steady-state ascent at that speed, which the jump probe shows to be
        /// jump.Speed minus DemonThrust's decrement -- 7.0 at 7.41 and 8.6 at
        /// 9.01. FlyTicks is the wing budget, which is the one figure still not
        /// read from the assembly (ArmorIDs.Wing.Sets.Stats is not in the
        /// decompiled output) and is therefore marked as taken from the reviewed
        /// routes rather than measured.
        /// </summary>
        public readonly struct LoadoutProfile
        {
            public readonly string Name;
            public readonly float JumpSpeed;
            public readonly float ClimbRate;
            public readonly float FlyTicks;
            public readonly float ClimbAbove;
            public readonly float DashAim;
            public readonly float ClimbCap;
            public readonly float HoverDescend;
            public readonly float Lead;
            public readonly int DashAt;

            public LoadoutProfile(string name, float jumpSpeed, float climbRate,
                float flyTicks, float climbAbove, float dashAim, float climbCap,
                float hoverDescend, float lead, int dashAt)
            {
                Name = name; JumpSpeed = jumpSpeed; ClimbRate = climbRate;
                FlyTicks = flyTicks; ClimbAbove = climbAbove; DashAim = dashAim;
                ClimbCap = climbCap; HoverDescend = hoverDescend; Lead = lead;
                DashAt = dashAt;
            }
        }

        /// <summary>Weak route: Fairy Wings (761) on the admitted set.
        ///
        /// ClimbAbove 0.88 and JumpSpeed 8.91 are the best-known values, not
        /// guesses: a joint sweep scored on the eighth contact found
        /// climbAbove=0.88 / jumpSpeed=8.91 surviving 8000 ticks and 97 charges
        /// without reaching the eighth contact at all, against 20 charges for
        /// the previous 0.75 / 7.41 pair. Note 8.91 is not the weak set's
        /// derived speed (7.41) -- it is the speed that the controller flies
        /// best at, which is a property of the controller, and is recorded as
        /// such rather than presented as the loadout's value.</summary>
        public static LoadoutProfile WeakWings()
        {
            return new LoadoutProfile("weak (fairy wings 761)", 8.91f, 8.5f, 100f,
                0.88f, 0.85f, 420f, 160f, 240f, 8);
        }
        /// <summary>Strong route: Fishron Wings and the IsStrongWingItem set.
        ///
        /// The strong set's own derived speed is 9.01 and its climb 8.6. Its
        /// optimum under this controller has not been found yet; it inherits the
        /// weak set's tuned ClimbAbove only so the two can be compared, and that
        /// inheritance is the thing the next round has to replace with a real
        /// search.</summary>
        public static LoadoutProfile StrongWings()
        {
            return new LoadoutProfile("strong (fishron wings)", 9.01f, 8.6f, 150f,
                0.88f, 0.85f, 420f, 160f, 240f, 8);
        }

        /// <summary>
        /// Traces one charge of the admitted weak set so the constant obstacle
        /// can be read directly. Charge 5 is the first contact for every jump
        /// speed from 6.41 to 8.91, which makes it the highest-value single
        /// fix, and "the first contact" is the Nth-contact metric's blind spot.
        /// Run with --fishron-charge-trace [ordinal] [jumpSpeed].
        /// </summary>
        public static void FishronChargeTrace(int ordinal, float jumpSpeed)
        {
            // The winning config is climbAbove 0.88 at jump speed 8.91. Tracing
            // the old 0.75 / 7.41 pair would answer a question about a run that
            // no longer exists, so those values are read from the profile.
            var profile = WeakWings();
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "== charge {0} trace, {1}, jumpSpeed={2:F2} climbAbove={3:F2} ==",
                ordinal, profile.Name, jumpSpeed, profile.ClimbAbove));
            TraceCharge = true;
            TraceOnlyCharge = ordinal;
            var fight = RunFight(new CorridorEscape(true, profile.Lead,
                profile.DashAt, true, 0f, profile.ClimbAbove, profile.DashAim, 0,
                profile.ClimbCap, profile.HoverDescend), 8000, maxHits: 999,
                bossOnly: true, bubbles: true, jumpSpeed: jumpSpeed,
                wingTimeMax: profile.FlyTicks, autoJump: true, trace: true,
                traceTicks: 8000);
            TraceCharge = false;
            TraceOnlyCharge = 0;
            var contactCount = 0;
            foreach (var line in fight.HitLog)
                if (line.Contains("src boss")) contactCount++;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  result: ticks={0} charges={1} bossContacts={2}",
                fight.Ticks, fight.Charges, contactCount));
            foreach (var line in fight.HitLog)
                Console.WriteLine("    " + line);

            // Charge 5 clears by about ten pixels (perp 114 against a 104
            // requirement), which is a 10% margin, so it is the escape's
            // tightest point rather than a wall the escape cannot pass. The
            // threshold that decides when the climb is allowed to start is
            // therefore the thing worth varying here.
            Console.WriteLine();
            Console.WriteLine("  climbAbove sweep (weak set, first contact):");
            foreach (var above in new[] { 0.00f, 0.25f, 0.45f, 0.60f, 0.70f, 0.75f,
                0.82f, 0.88f, 0.94f })
            {
                var probe = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, above, 0.85f), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true, jumpSpeed: jumpSpeed, wingTimeMax: 100f,
                    autoJump: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    climbAbove={0,4:F2} firstHit={1,5} atCharge={2,3}",
                    above, probe.Ticks, probe.Charges));
            }

            // The first-contact metric is a poor objective: the best run so far
            // is touched at charge 5 and still survives to charge 20, while
            // configs with a later first contact die sooner. Total survival is
            // what the fight actually rewards, so the joint sweep is scored on
            // the eighth contact.
            Console.WriteLine();
            Console.WriteLine("  joint sweep, scored on 8th contact (weak set):");
            var gridBest = 0;
            var gridLabel = "none";
            foreach (var above in new[] { 0.00f, 0.25f, 0.45f, 0.60f, 0.70f, 0.75f,
                0.82f, 0.88f, 0.94f })
            foreach (var speed in new[] { 6.41f, 6.91f, 7.41f, 7.91f, 8.41f,
                8.91f })
            {
                var run = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, above, 0.85f), 8000, maxHits: 8, bossOnly: true,
                    bubbles: true, jumpSpeed: speed, wingTimeMax: 100f,
                    autoJump: true);
                if (run.Ticks > gridBest)
                {
                    gridBest = run.Ticks;
                    gridLabel = string.Format(CultureInfo.InvariantCulture,
                        "climbAbove={0:F2} jumpSpeed={1:F2} charges={2}",
                        above, speed, run.Charges);
                }
            }
            Console.WriteLine("    best: " + gridLabel + " at " + gridBest);

            // If that config never reaches the eighth contact, the fight was
            // still running when the tick cap expired -- which is only
            // interesting if it also took no hits at all. Both are measured
            // here rather than inferred from the tick count.
            Console.WriteLine();
            Console.WriteLine("  best-config audit (maxHits high, so the cap is the tick limit):");
            var audit = RunFight(new CorridorEscape(true, 240f, 8, true,
                0f, 0.88f, 0.85f), 8000, maxHits: 999, bossOnly: true,
                bubbles: true, jumpSpeed: 8.91f, wingTimeMax: 100f,
                autoJump: true);
            var auditContacts = 0;
            foreach (var line in audit.HitLog)
                if (line.Contains("src boss")) auditContacts++;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    ticks={0} charges={1} totalHitLog={2} bossContacts={3}",
                audit.Ticks, audit.Charges, audit.HitLog.Count, auditContacts));
            foreach (var line in audit.HitLog) Console.WriteLine("      " + line);

            // The audit shows a seven-contact cluster at charge 5, ticks
            // 303-307, so the config is close but not clean. The contacts are
            // consecutive and come from the boss body, which means a small
            // change of position at that moment decides it. Sweeping the
            // remaining controller knobs is how to look for a zero-contact
            // point rather than a longer run.
            Console.WriteLine();
            Console.WriteLine("  zero-hit hunt (weak set, contacts must be 0):");
            var clean = 0;
            var cleanLabel = "none";
            foreach (var above in new[] { 0.84f, 0.86f, 0.88f, 0.90f, 0.92f })
            foreach (var lead in new[] { 180f, 210f, 240f, 270f, 300f })
            foreach (var desc in new[] { 120f, 160f, 220f })
            {
                var run = RunFight(new CorridorEscape(true, lead, 8, true, 0f,
                    above, 0.85f, 0, 420f, desc), 8000, maxHits: 999,
                    bossOnly: true, bubbles: true, jumpSpeed: 8.91f,
                    wingTimeMax: 100f, autoJump: true);
                var contacts = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) contacts++;
                if (contacts == 0)
                {
                    clean++;
                    cleanLabel = string.Format(CultureInfo.InvariantCulture,
                        "above={0:F2} lead={1:F0} desc={2:F0} ticks={3} " +
                        "charges={4}", above, lead, desc, run.Ticks, run.Charges);
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    zero-contact configs: {0}", clean));
            Console.WriteLine("    first: " + cleanLabel);

            // The first hunt only varied climbAbove/lead/hoverDescend. The
            // contacts are a hover-phase cluster, so the hover geometry itself
            // -- how high the controller lets the player sit between charges --
            // is the more likely lever, and it is varied here with the ascent
            // cap. A clean config is required to have zero boss contacts, not
            // merely a long run.
            Console.WriteLine();
            Console.WriteLine("  wider zero-hit hunt (climbCap x hoverDescend x climbAbove):");
            var wide = 0;
            var wideLabel = "none";
            var wideBest = 0;
            var wideBestLabel = "none";
            foreach (var cap in new[] { 200f, 300f, 420f, 600f, 900f })
            foreach (var desc in new[] { 80f, 120f, 160f, 220f, 320f })
            foreach (var above in new[] { 0.80f, 0.86f, 0.88f, 0.92f })
            {
                var run = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                    above, 0.85f, 0, cap, desc), 8000, maxHits: 999,
                    bossOnly: true, bubbles: true, jumpSpeed: 8.91f,
                    wingTimeMax: 100f, autoJump: true);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                if (n == 0)
                {
                    wide++;
                    wideLabel = string.Format(CultureInfo.InvariantCulture,
                        "cap={0:F0} desc={1:F0} above={2:F2} ticks={3} ch={4}",
                        cap, desc, above, run.Ticks, run.Charges);
                }
                if (run.Ticks > wideBest)
                {
                    wideBest = run.Ticks;
                    wideBestLabel = string.Format(CultureInfo.InvariantCulture,
                        "cap={0:F0} desc={1:F0} above={2:F2} contacts={3} ch={4}",
                        cap, desc, above, n, run.Charges);
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    zero-contact: {0} of 100", wide));
            Console.WriteLine("    first clean: " + wideLabel);
            Console.WriteLine("    longest run: " + wideBestLabel);

            // Pre-positioning is the one action available in the hover's last
            // frames, where the surviving contacts actually are, so it is
            // measured against the no-prepositioning baseline on the same
            // parameters.
            Console.WriteLine();
            Console.WriteLine("  hover pre-positioning (weak set, contacts counted):");
            foreach (var prep in new[] { 0, 1, 2 })
            {
                var run = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                    0.88f, 0.85f, 0, 420f, 160f, prep), 8000, maxHits: 999,
                    bossOnly: true, bubbles: true, jumpSpeed: 8.91f,
                    wingTimeMax: 100f, autoJump: true);                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    preposition={0} ticks={1,5} charges={2,3} " +
                    "bossContacts={3,4}", prep, run.Ticks, run.Charges, n));
            }

            // Pre-positioning regressed 7 contacts to 200, which is a large
            // enough change that the sign or the axis is likely wrong rather
            // than merely unhelpful. These variants isolate that: pure
            // horizontal steering, the opposite horizontal sign, and vertical
            // only.
            Console.WriteLine();
            Console.WriteLine("  pre-positioning variants:");
            foreach (var variant in new[] { "none", "horiz", "horizFlip",
                "vert", "away" })
            {
                var run = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                    0.88f, 0.85f, 0, 420f, 160f, 0, variant), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    jumpSpeed: 8.91f, wingTimeMax: 100f, autoJump: true);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    variant={0,-10} ticks={1,5} charges={2,3} " +
                    "bossContacts={3,4}", variant, run.Ticks, run.Charges, n));
            }

            // Where the baseline's contacts sit, since "8009 ticks" alone does
            // not say whether the run is clean early and dies late or dies
            // immediately. Printing the first few is what makes the comparison
            // against the variants meaningful.
            Console.WriteLine();
            Console.WriteLine("  baseline contact detail (preposition=0):");
            var baseRun = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                0.88f, 0.85f, 0, 420f, 160f, 0), 8000, maxHits: 999,
                bossOnly: true, bubbles: true, jumpSpeed: 8.91f,
                wingTimeMax: 100f, autoJump: true);
            foreach (var line in baseRun.HitLog)
                Console.WriteLine("    " + line);
        }

        // ------------------------------------------------------------------ lab
        /// <summary>
        /// Runs the two admitted wing sets as two separate configurations and
        /// reports them side by side, so the split is visible rather than
        /// averaged away. Run with --fishron-loadouts.
        /// </summary>
        public static void FishronLoadoutCompare()
        {
            Console.WriteLine("== admitted wing sets, run separately ==");
            // The wing budget does deplete: the probe shows WingTime leaving
            // 150 around tick 17 and falling about one per tick, so the strong
            // wing's longer flight is a real difference and not, as 3.29 said,
            // an inert field. That earlier reading came from a trace that only
            // printed the first 26 ticks, which are still at 150.
            Console.WriteLine("== wing budget (WingTimeMax) ==");
            // "last hit tick" is the Nth contact and hides what actually
            // changed, so the first contact is measured alongside it. The
            // jumpSpeed curve is non-monotone (7.41 far better than 9.01), and
            // a first-contact number is what distinguishes "clears more
            // charges" from "fails later in the same place".
            Console.WriteLine("  first-contact comparison:");
            foreach (var js in new[] { 5.01f, 7.41f, 9.01f, 11.01f, 12.01f })
            {
                var first = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true, jumpSpeed: js, wingTimeMax: 150f,
                    autoJump: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    jumpSpeed={0,5:F2} firstHit={1,5} chargesAtHit={2,3}",
                    js, first.Ticks, first.Charges));
            }
            Console.WriteLine("  failure charge map (finer jumpSpeed sweep):");
            // autoJump is swept alongside speed because it is not a free
            // upgrade: the last block above shows a run where EVERY charge
            // contacts with maxPerp of only 17-28 px against a ~105 px
            // requirement, which is the signature of the ascent being cut short
            // rather than of the speed being wrong.
            foreach (var auto in new[] { false, true })
            foreach (var speed in new[] { 6.41f, 6.91f, 7.41f, 7.91f, 8.41f, 8.91f })
            {
                var first = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true, jumpSpeed: speed, wingTimeMax: 150f,
                    autoJump: auto);
                var full = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f), 8000, maxHits: 8, bossOnly: true,
                    bubbles: true, jumpSpeed: speed, wingTimeMax: 150f,
                    autoJump: auto);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    autoJump={0,-5} jumpSpeed={1,5:F2} firstHit={2,5} " +
                    "atCharge={3,3} then8th={4,5} totalCharges={5,3}",
                    auto, speed, first.Ticks, first.Charges, full.Ticks,
                    full.Charges));
            }
            foreach (var js in new[] { 7.41f, 9.01f })
            foreach (var wt in new[] { 50f, 100f, 150f, 220f })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f), 8000, maxHits: 8, bossOnly: true,
                    bubbles: true, jumpSpeed: js, wingTimeMax: wt, autoJump: true);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  jumpSpeed={0,5:F2} wingTimeMax={1,5:F0} lastHit={2,5} " +
                    "charges={3,3} bossHits={4,3}", js, wt, fight.Ticks,
                    fight.Charges, contacts));
            }
            Console.WriteLine();
            foreach (var profile in new[] { WeakWings(), StrongWings() })
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}: jumpSpeed={1:F2} climb={2:F1} fly={3:F0}",
                    profile.Name, profile.JumpSpeed, profile.ClimbRate,
                    profile.FlyTicks));
                foreach (var js in new[] { 5.01f, profile.JumpSpeed, 8.01f, 10.01f,
                    11.01f, 12.01f })
                {
                    var fight = RunFight(new CorridorEscape(true, profile.Lead,
                        profile.DashAt, true, 0f, profile.ClimbAbove,
                        profile.DashAim, 0, profile.ClimbCap, profile.HoverDescend),
                        8000, maxHits: 8, bossOnly: true, bubbles: true,
                        jumpSpeed: js);
                    var contacts = 0;
                    foreach (var line in fight.HitLog)
                        if (line.Contains("src boss")) contacts++;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    jumpSpeed={0,5:F2} lastHit={1,5} charges={2,3} " +
                        "bossHits={3,3}", js, fight.Ticks, fight.Charges,
                        contacts));
                }
            }
        }

        /// <summary>
        /// Measures the accoladed loadout's vertical mobility directly instead
        /// of inferring it. Earlier rounds concluded "the climb is capped at a
        /// hard 4.6 px/tick" from runs that used the bare jump speed and never
        /// enabled autoJump, which is not the admitted loadout: both wing routes
        /// gate on the Frog Leg (jumpSpeedBoost +2.4, Player.cs:19741) and the
        /// Amphibious Boots add autoJump plus +1.6 (Player.cs:14523 family), so
        /// the real jump.Speed is 5.01 + 2.4 + 1.6 = 9.01 and holding Jump keeps
        /// re-jumping from the ground. This probe prints the actual velocity.Y
        /// trace for each combination so the ceiling is measured, not assumed.
        /// Run with --fishron-jump-probe.
        /// </summary>
        public static void FishronJumpProbe()
        {
            Console.WriteLine("== vertical mobility probe ==");
            Console.WriteLine("  jumpSpeed  autoJump  tick:velocity.Y (first 26 ticks)");
            foreach (var js in new[] { 5.01f, 7.41f, 9.01f })
            foreach (var auto in new[] { false, true })
            {
                var frame = FishronPlayerStart(3300f, 6000f, js);
                if (auto)
                {
                    var j = frame.Jump;
                    j.AutoJump = true;
                    frame.Jump = j;
                }
                var shape = new System.Text.StringBuilder();
                var peak = 0f;
                var wingTrace = new System.Text.StringBuilder();
                for (var tick = 1; tick <= 160; tick++)
                {
                    PlayerMotionFrame next;
                    ForwardModelRefusal refusal;
                    if (!PlayerForwardModel.TryAdvance(in frame,
                        new PlayerControlFrame { Jump = true, Up = true },
                        out next, out refusal))
                    {
                        shape.Append("REFUSED:" + refusal);
                        break;
                    }
                    frame = next;
                    if (frame.Velocity.Y < peak) peak = frame.Velocity.Y;
                    if (tick <= 26)
                        shape.Append(frame.Velocity.Y.ToString("F1",
                            CultureInfo.InvariantCulture)).Append(' ');
                    if (tick <= 40 || tick % 20 == 0)
                        wingTrace.Append(frame.WingTime.ToString("F0",
                            CultureInfo.InvariantCulture)).Append(' ');
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0,9:F2}  {1,-8}  peakVY={2,6:F2}  {3}",
                    js, auto, peak, shape.ToString().Trim()));
                Console.WriteLine("             wingTime: " + wingTrace.ToString().Trim());
            }
        }

        /// <summary>
        /// Fine sweep of the opening position, looking for a start whose locked
        /// charge angles all fall where the movement model can clear them. The
        /// hover tracks the player, so the start is the only place the fight's
        /// angle sequence can be chosen at all: a single zero-hit start would
        /// demonstrate the escape end to end, and a sweep with none shows the
        /// steep band is a property of the fight rather than of the tuning.
        /// Run with --fishron-start-sweep [first] [last] [step].
        /// </summary>
        public static void FishronStartSweep(float first, float last, float step)
        {
            Console.WriteLine("== fine opening-position sweep ==");
            var best = 0;
            var bestStart = 0f;
            var zeros = 0;
            for (var sx = first; sx <= last; sx += step)
            {
                // maxHits must be 1 here. With maxHits = 8 every failing start
                // stops on its eighth contact and reports exactly 8, so the
                // count cannot tell a start that dies at charge 2 from one that
                // survives to charge 12. Stopping on the FIRST contact turns
                // the metric into "how long until anything lands", which is
                // strictly more informative and is what the sweep needs.
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true),
                    8000, maxHits: 1, bossOnly: false, bubbles: false,
                    startX: sx);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                if (fight.Ticks > best)
                {
                    best = fight.Ticks;
                    bestStart = sx;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  startX={0,7:F1} firstHitTick={1,5} charges={2,3} " +
                        "bossHits={3,3}   <- best so far", sx, fight.Ticks,
                        fight.Charges, contacts));
                }
                if (contacts == 0)
                {
                    zeros++;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  ZERO-HIT START startX={0:F1} ticks={1} charges={2}",
                        sx, fight.Ticks, fight.Charges));
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  zero-hit starts: {0}, latest first contact: tick {1} at " +
                "startX={2:F1}", zeros, best, bestStart));
        }

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
            Console.WriteLine("== corridor escape (closed loop, dash led by px) ==");
            foreach (var lead in new[] { -1f, 60f, 120f, 180f, 240f, 300f, 380f,
                460f, 560f, 700f })
            {
                var fight = RunFight(new CorridorEscape(lead > 0f, lead),
                    8000, bossOnly: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  dashLead={0,6:F0} dash={1,-5} ticks={2,5} hits={3,4} " +
                    "charges={4,3} perpAtClosest={5,6:F1} maxPerp={6,6:F1} " +
                    "refusal={7}",
                    lead, lead > 0f, fight.Ticks, fight.Hits, fight.Charges,
                    fight.ClosestPerpendicular, fight.MaxPerpendicular,
                    fight.Refusal));
            }
            Console.WriteLine();
            // The lead decides how much of the dash's eighteen-tick decay is
            // still live when the boss arrives, and the timer decides which
            // tick of the charge spends it. Both matter and they interact:
            // a long lead spent on tick zero is the same as a short one.
            // Searching only the lead (as the sweep above does) cannot tell
            // those apart, so the two are searched together here.
            Console.WriteLine("== corridor search (dash lead x dash tick) ==");
            var bestHits = int.MaxValue;
            var bestLabel = "none";
            foreach (var climb in new[] { false, true })
            foreach (var lead in new[] { 40f, 80f, 120f, 160f, 200f, 260f,
                320f, 380f, 440f, 500f })
                foreach (var at in new[] { 0, 2, 4, 6, 8, 10, 12, 14 })
                {
                    var fight = RunFight(new CorridorEscape(true, lead, at, climb),
                        8000, bossOnly: true);
                    if (fight.Hits < bestHits)
                    {
                        bestHits = fight.Hits;
                        bestLabel = string.Format(CultureInfo.InvariantCulture,
                            "climb={0} lead={1:F0} at={2}", climb, lead, at);
                    }
                    if (fight.Hits <= 1 && climb)
                        Console.WriteLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "  climb={0,-5} lead={1,6:F0} at={2,2} ticks={3,5} " +
                            "hits={4,3} charges={5,3} perpAtClosest={6,6:F1} " +
                            "maxPerp={7,6:F1}",
                            climb, lead, at, fight.Ticks, fight.Hits,
                            fight.Charges, fight.ClosestPerpendicular,
                            fight.MaxPerpendicular));
                }
            Console.WriteLine("  best: hits=" + bestHits + " at " + bestLabel);
            Console.WriteLine();
            // Separation is the primary objective (see the lock-geometry note
            // in CorridorEscape), so it is searched on its own axis rather than
            // folded into the dash sweep above.
            Console.WriteLine("== separation search (range held before the lock) ==");
            var bestSep = int.MaxValue;
            var bestSepLabel = "none";
            foreach (var keep in new[] { 420f, 460f, 500f, 540f, 580f, 620f,
                700f, 800f })
            foreach (var climb in new[] { false, true })
            {
                var fight = RunFight(new CorridorEscape(true, 200f, 2, climb, keep),
                    8000, bossOnly: true);
                if (fight.Hits < bestSep)
                {
                    bestSep = fight.Hits;
                    bestSepLabel = string.Format(CultureInfo.InvariantCulture,
                        "keep={0:F0} climb={1}", keep, climb);
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  keep={0,6:F0} climb={1,-5} ticks={2,5} hits={3,3} " +
                    "charges={4,3} perpAtClosest={5,6:F1} maxPerp={6,6:F1}",
                    keep, climb, fight.Ticks, fight.Hits, fight.Charges,
                    fight.ClosestPerpendicular, fight.MaxPerpendicular));
            }
            Console.WriteLine("  best: hits=" + bestSep + " at " + bestSepLabel);
            Console.WriteLine();
            // With the angle-aware escape in place the fight survives four of
            // five charges, so this is now a search for the last one rather
            // than for the first. Both dimensions matter: how much of the
            // eighteen-tick dash decay is left when the boss arrives, and how
            // late the ascent starts (a shallow charge wants it immediately).
            Console.WriteLine("== full search (climb x dash lead x dash tick) ==");
            var fullBest = int.MaxValue;
            var fullBestLabel = "none";            foreach (var climb in new[] { false, true })
            foreach (var dl in new[] { 120f, 160f, 200f, 240f, 280f, 340f })
            foreach (var at in new[] { 0, 4, 8, 12, 16, 20 })
            {
                var fight = RunFight(new CorridorEscape(true, dl, at, climb),
                    12000, bossOnly: true);
                if (fight.Hits < fullBest)
                {
                    fullBest = fight.Hits;
                    fullBestLabel = string.Format(CultureInfo.InvariantCulture,
                        "climb={0} dl={1:F0} at={2}", climb, dl, at);
                }
                if (fight.Hits == 0)
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "  ZERO HITS: climb={0} dl={1:F0} at={2} ticks={3} " +
                        "charges={4}", climb, dl, at, fight.Ticks,
                        fight.Charges));
            }
            Console.WriteLine("  best: hits=" + fullBest + " at " + fullBestLabel);
            Console.WriteLine();
            Console.WriteLine("== one charge, tick by tick (dash on the lock tick) ==");
            TraceCharge = true;
            var traced = RunFight(new BeatCycleDodge(true, DashAim.Flee, 1), 6000,
                trace: true, traceTicks: 72, maxHits: 3, bossOnly: true);
            TraceCharge = false;
            Console.WriteLine("  hits=" + traced.Hits + " ticks=" + traced.Ticks);
            foreach (var line in traced.ChargeLog)
                Console.WriteLine("  " + line);
            foreach (var line in traced.HitLog)
                Console.WriteLine("  " + line);
            Console.WriteLine();
            // Same closed-loop escape, but with the wing ascent held. Printed
            // tick by tick because "climb changed nothing" is only meaningful
            // once it is visible whether the vertical velocity ever left zero.
            Console.WriteLine("== corridor escape with wing ascent held ==");
            TraceCharge = true;
            var climbing = RunFight(new CorridorEscape(true, 240f, 8, true), 12000,
                maxHits: 6, bossOnly: false, bubbles: false);
            TraceCharge = false;
            Console.WriteLine("  hits=" + climbing.Hits + " ticks=" + climbing.Ticks +
                " charges=" + climbing.Charges);
            var bossHits = 0;
            foreach (var line in climbing.HitLog)
                if (line.Contains("src boss")) bossHits++;
            Console.WriteLine("  BOSS-CONTACT HITS: " + bossHits + " of " +
                climbing.HitLog.Count + " total hits");
            // The charge dodge and the projectile threat are separable, and
            // they have to be reported separately: a run can dodge every charge
            // and still die to a bubble, which would otherwise read as "the
            // dodge failed". bossOnly=true removes every projectile so the
            // charge answer stands on its own.
            Console.WriteLine("== charge-only audit (projectiles removed) ==");
            foreach (var dl in new[] { 160f, 200f, 240f, 280f })
                foreach (var at in new[] { 4, 8, 12 })
                {
                    var bossFight = RunFight(
                        new CorridorEscape(true, dl, at, true), 4000,
                        bossOnly: true, maxHits: 8);
                    var contacts = 0;
                    foreach (var line in bossFight.HitLog)
                        if (line.Contains("src boss")) contacts++;
                    Console.WriteLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "  dl={0,5:F0} at={1,2} ticks={2,5} charges={3,3} " +
                        "bossHits={4,3} hits={5,3} perpAtClosest={6,6:F1}",
                        dl, at, bossFight.Ticks, bossFight.Charges, contacts,
                        bossFight.Hits, bossFight.ClosestPerpendicular));
                }
            Console.WriteLine();
            // The threat classes have to be separable to be answerable, and
            // each switch is now actually honoured. This walks them one at a
            // time so the remaining work is attributable to a named source
            // rather than to "projectiles".
            Console.WriteLine();
            // The climb/dash threshold is the one free parameter with a real
            // physical meaning (see the derivation at CorridorEscape), and the
            // two extremes both fail: climbing everything dies to the steep
            // family, dashing everything dies to the shallow one. The optimum
            // is interior, so it is swept rather than reasoned about.
            Console.WriteLine("== climb/dash threshold sweep ==");
            foreach (var th in new[] { 0.60f, 0.70f, 0.75f, 0.80f, 0.85f,
                0.90f, 0.95f })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true, 0f, th),
                    8000, maxHits: 8, bossOnly: false, bubbles: false);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  climbAbove={0:F2} ticks={1,5} charges={2,3} bossHits={3,3}",
                    th, fight.Ticks, fight.Charges, contacts));
            }
            Console.WriteLine();
            // jump.Speed is the ONE input that directly raises the climb
            // ceiling, because DemonThrust clamps to exactly it. The native
            // values are not guesses -- Player.cs line 2513 sets the base to
            // 5.01 and line 19759 does `jumpSpeed += jumpSpeedBoost`, where the
            // Frog Leg adds 2.4 (line 19741) and the Empress Brooch adds 1.8
            // (line 19737). The lab fixture had been running the bare 5.01,
            // i.e. no Frog Leg, so this sweep asks whether the item the fight
            // is actually fought with changes the answer.
            Console.WriteLine("== jump speed (wing climb ceiling) ==");
            foreach (var js in new[] { 5.01f, 6.61f, 7.41f, 9.21f, 10.41f })
            {
                var tag = js < 5.02f ? "bare"
                    : js < 7.4f ? "base+1.6"
                    : js < 9.2f ? "frog leg" : "frog leg+brooch+";
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true),
                    8000, maxHits: 1, bossOnly: true, bubbles: true,
                    jumpSpeed: js);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  jumpSpeed={0,5:F2} ({1,-16}) firstHit={2,5} charges={3,3}",
                    js, tag, fight.Ticks, fight.Charges));
            }
            Console.WriteLine();
            // The dash is horizontal, so aiming it at the escape side is worth
            // doing only when the horizontal term dominates. This sweep finds
            // where that is, using first contact rather than the eighth as the
            // metric (see the maxHits note below).
            Console.WriteLine("== dash-alignment gate (aim the dash at the escape) ==");
            foreach (var gate in new[] { 0f, 0.75f, 0.80f, 0.85f, 0.90f, 1.01f })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, gate), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  gate|ux|<{0:F2} firstHit={1,5} charges={2,3}",
                    gate, fight.Ticks, fight.Charges));
            }
            Console.WriteLine("== dash fire delay (is the dash spent too early?) ==");
            // Dash aim was proven to reach the impulse (FrameDashDirectionFoll-
            // owsTheHeldHorizontalBit pins rightVX 14.5 / leftVX -14.5), so a
            // flat gate sweep means the direction is decided too late to matter.
            // _dashIssued makes the dash a once-per-charge event, and at=8
            // spends it eight ticks into the charge -- well before the escape
            // side is settled. These delays move the spend later.
            foreach (var at in new[] { 8, 12, 16, 20, 24, 28 })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, at, true,
                    0f, 0.75f, 0.85f), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  dashAt={0,2} firstHit={1,5} charges={2,3}",
                    at, fight.Ticks, fight.Charges));
            }
            Console.WriteLine();
            // The admitted routes require the Frog Leg (FormulaRouteCatalog.Select
            // gates both wing routes on frogLeg) and it is worth +2.4 jump speed,
            // but the fixture had been running the bare 5.01. Worse, the climb
            // controller holds Jump continuously, and native only refills the
            // wing budget on a RELEASE edge while airborne. Holding forever
            // therefore spends the wing once and never refills it, which is a
            // candidate explanation for the flat 4.6 px/tick ceiling that every
            // earlier sweep ran into.
            Console.WriteLine("== frog leg + jump release (does the wing refill?) ==");
            foreach (var js in new[] { 5.01f, 7.41f })
            foreach (var pulse in new[] { 0, 2, 3, 4 })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f, pulse), 8000, maxHits: 1, bossOnly: true,
                    bubbles: true, jumpSpeed: js);
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  jumpSpeed={0,5:F2} pulse={1} firstHit={2,5} charges={3,3}",
                    js, pulse, fight.Ticks, fight.Charges));
            }
            Console.WriteLine();
            // The admitted loadout, end to end. Both wing routes gate on the
            // Frog Leg and the set carries Amphibious Boots, so jump.Speed is
            // 5.01 + 2.4 + 1.6 = 9.01 (Player.cs 19741 and the 14523 boot
            // family) and autoJump is on, which starts the ascent a tick
            // earlier. The probe shows that combination climbing at a constant
            // -8.6 px/tick, against -4.6 for the bare fixture every earlier
            // sweep used.
            Console.WriteLine("== admitted loadout (frog leg + boots, jumpSpeed 9.01) ==");
            foreach (var js in new[] { 5.01f, 7.41f, 9.01f })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true),
                    8000, maxHits: 8, bossOnly: true, bubbles: true,
                    jumpSpeed: js);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  jumpSpeed={0,5:F2} lastHitTick={1,5} charges={2,3} " +
                    "bossHits={3,3} perpAtClosest={4,6:F1}",
                    js, fight.Ticks, fight.Charges, contacts,
                    fight.ClosestPerpendicular));
            }
            Console.WriteLine();
            // The 420 px ascent cap and the 160 px hover descent were both
            // chosen against the old 4.6 px/tick climb. At the admitted 8.6 the
            // player reaches either bound far sooner, so the geometry those
            // numbers encode no longer holds and they have to be re-chosen
            // rather than inherited.
            Console.WriteLine("== loadout re-tune (climb cap x hover descent, 9.01) ==");
            var tuneBest = 0;
            var tuneLabel = "none";
            foreach (var cap in new[] { 260f, 420f, 700f, 1100f })
            foreach (var desc in new[] { 100f, 160f, 300f, 520f })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true,
                    0f, 0.75f, 0.85f, 0, cap, desc), 8000, maxHits: 8,
                    bossOnly: true, bubbles: true, jumpSpeed: 9.01f);
                var contacts = 0;
                foreach (var line in fight.HitLog)
                    if (line.Contains("src boss")) contacts++;
                if (fight.Ticks > tuneBest)
                {
                    tuneBest = fight.Ticks;
                    tuneLabel = string.Format(CultureInfo.InvariantCulture,
                        "cap={0:F0} desc={1:F0}", cap, desc);
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  cap={0,5:F0} desc={1,4:F0} lastHit={2,5} charges={3,3} " +
                    "bossHits={4,3}", cap, desc, fight.Ticks, fight.Charges,
                    contacts));
            }
            Console.WriteLine("  best: " + tuneLabel + " at " + tuneBest);
            Console.WriteLine();
            Console.WriteLine("== threat-class isolation (charges always live) ==");
            // A full phase one is ten charges (ai[0] 0..9) plus the phase-two
            // states, so the charge run is given enough hits and ticks to get
            // well past that before it is judged.
            foreach (var bubbles2 in new[] { false, true })
            foreach (var tornados2 in new[] { false, true })
            {
                var fight = RunFight(new CorridorEscape(true, 240f, 8, true),
                    8000, maxHits: 8, bossOnly: false, bubbles: bubbles2,
                    tornados: tornados2);
                var contacts = 0;
                var bubbleHits = 0;
                var others = 0;
                foreach (var line in fight.HitLog)
                {
                    if (line.Contains("src boss")) contacts++;
                    else if (line.Contains("src bubble")) bubbleHits++;
                    else others++;
                }
                Console.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  bubbles={0,-5} tornados={1,-5} ticks={2,5} charges={3,3} " +
                    "bossHits={4,3} bubbleHits={5,3} other={6,3}",
                    bubbles2, tornados2, fight.Ticks, fight.Charges, contacts,
                    bubbleHits, others));
            }
            foreach (var line in climbing.ChargeLog)
                Console.WriteLine("  " + line);
            foreach (var line in climbing.HitLog)
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
