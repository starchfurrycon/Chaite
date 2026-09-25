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
        /// <summary>Hover park offset for phases one and two, native
        /// NPC.cs:49835-49839 (state 5): ai[1] = 300 * sign. This is a DIFFERENT
        /// constant from the phase-three one below, and the lab previously used
        /// a single value for both, which broke the phases' charge geometry
        /// differently in each.</summary>
        private const float HoverParkOffsetPhase12 = 300f;
        /// <summary>Hover park offset for phase three, native NPC.cs:50096-50100
        /// (state 10): ai[1] = 360 * sign.</summary>
        private const float HoverParkOffsetPhase3 = 360f;
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
            /// <summary>Player x extent over the run, for the drift analysis.</summary>
            public float MinPlayerX = float.MaxValue;
            public float MaxPlayerX = float.MinValue;
            public float FinalPlayerX;
            public readonly List<string> HitLog = new List<string>();
            public int ImmuneTicks;
            /// <summary>Set only by the A/B check that the immunity matters.</summary>
            public bool DashImmunityDisabled;
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
                    {
                        var park = world.State == 10 ? HoverParkOffsetPhase3
                            : HoverParkOffsetPhase12;
                        world.HoverOffset = park * Math.Sign(bc.X - pc.X);
                    }
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
                            world.HoverOffset = HoverParkOffsetPhase3 *
                                Math.Sign(bc.X - pc.X);
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
        /// <summary>Set only by the A/B check that the dash immunity matters.</summary>
        private static bool FishronDashImmunityDisabled;
        /// <summary>Close-approach trace over a whole run.</summary>
        private static bool TraceClose;
        /// <summary>Arena runway bounds. Widened by the drift test to tell an
        /// arena-geometry failure from a controller failure.</summary>
        private static float ArenaBandLeft = 1000f;
        private static float ArenaBandRight = 6000f;
        /// <summary>When positive, the charge trace prints only this charge
        /// ordinal. Charge 5 is a fixed obstacle across every jump speed from
        /// 6.41 to 8.91, so isolating its trace is what makes that readable.</summary>
        private static int TraceOnlyCharge;

        private static FightResult RunFight(IFishronController controller,
            int maxTicks, bool verbose = false, bool trace = false,
            int traceTicks = 70, int maxHits = 1, bool bossOnly = false,
            bool bubbles = false, bool? tornados = null, float startX = 3300f,
            float jumpSpeed = 5.01f, float wingTimeMax = 150f,
            bool autoJump = false, string hoverVariant = "none",
            float holdX = 0f, bool traceClose = false, float wallBand = 0f, bool adaptive = false)
        {
            const float floorY = 6000f;
            TraceClose = traceClose;
            var bandLeft = ArenaBandLeft;
            var bandRight = ArenaBandRight;
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
            world.MinPlayerX = world.MaxPlayerX = startX;
            world.MinPlayerX = world.MaxPlayerX = startX;

            while (world.Tick < maxTicks &&
                (maxHits < 0 || world.Hits < maxHits))
            {
                if (world.ImmuneTicks > 0) world.ImmuneTicks--;

                // The dash's contact immunity. Native sets eocDash = 15 when a
                // Shield of Cthulhu dash starts (Player.cs:21641) and the NPC
                // collision loop skips entirely while eocDash > 0
                // (Player.cs:31602), so a dash INTO the boss cannot be hit. The
                // official wiki states the same thing as the core phase-three
                // survival mechanic: "the Shield of Cthulhu can be used to great
                // effect, providing brief invincibility frames when dashing into
                // him".
                //
                // ImmuneTicks was declared, decremented and gated on, but never
                // SET, so this whole mechanic was dead code and every contact
                // registered regardless. That is why a pure perpendicular-escape
                // controller could never reach zero: the escape it was missing
                // was not a geometric one.
                if (frame.Dashing && !FishronDashImmunityDisabled) world.ImmuneTicks = 15;
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
                if (frame.Position.X < world.MinPlayerX)
                    world.MinPlayerX = frame.Position.X;
                if (frame.Position.X > world.MaxPlayerX)
                    world.MaxPlayerX = frame.Position.X;

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
                // Compact close-approach trace over a whole run. The per-tick
                // version above is capped at traceTicks because it is one line
                // per tick; the question here is what the ONE working
                // configuration actually does across all 97 charges, which
                // needs the whole run at low density. Prints only the ticks
                // where the two are close enough for the geometry to matter.
                var pcx = frame.Position.X + frame.Width * 0.5f;
                var pcy = frame.Position.Y + frame.Height * 0.5f;
                var bcx = world.BossX + BossWidth * 0.5f;
                var bcy = world.BossY + BossHeight * 0.5f;
                var closeDx = pcx - bcx;
                var closeDy = pcy - bcy;
                if (TraceClose && isDash && closeDx * closeDx + closeDy * closeDy <
                    260f * 260f)
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  C{0,3} T{1,4} st={2,2} tm={3,2} dist={4,6:F0} " +
                        "perp={5,6:F1} req={6,5:F0} alt={7,5:F0} " +
                        "ctrl={8}{9}{10}{11} imm={12,2}",
                        chargeOrdinal, world.Tick, world.State,
                        world.StateTimer,
                        (float)Math.Sqrt(closeDx * closeDx + closeDy * closeDy),
                        perpendicular, requiredClearance,
                        world.FloorY - (frame.Position.Y + frame.Height * 0.5f),
                        controls.Left ? "L" : "-", controls.Right ? "R" : "-",
                        controls.Jump ? "J" : "-", controls.Dash ? "D" : "-",
                        world.ImmuneTicks));
                }
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
                MinPlayerX = world.MinPlayerX,
                MaxPlayerX = world.MaxPlayerX,
                FinalPlayerX = frame.Position.X,
            };
        }

        private sealed class FightResult
        {
            public int Ticks;
            public int Hits;
            /// <summary>Player x extent over the run, for the drift analysis.</summary>
            public float MinPlayerX = float.MaxValue;
            public float MaxPlayerX = float.MinValue;
            public float FinalPlayerX;
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
            private readonly bool _counterDash;
            private readonly float _lead;
            /// <summary>When positive, the controller maintains an anchor X
            /// instead of drifting. 3.43 showed the drift is the reason the
            /// zero-contact result does not generalise: with a bounded runway a
            /// wall happens to stop the drift in the right place, and widening
            /// the runway made every opening worse. Holding a position is the
            /// closed-loop replacement for that accident.</summary>
            private float _holdX;
            private bool _retreat;
            private readonly float _wallBand;
            /// <summary>State-driven escape choice (3.88) instead of a fixed gate.</summary>
            private readonly bool _adaptiveGate;
            private readonly bool _bandMode;            private readonly float _holdTolerance;
            /// <summary>Ticks before predicted contact at which to fire the
            /// dash, so the 15 immune ticks cover the arrival (3.50). Zero
            /// restores the old fire-on-lead behaviour.</summary>
            private readonly int _dashAtContact;
            /// <summary>Dash only when the committed line would actually hit.</summary>
            private readonly bool _gateDashOnPrediction;
            /// <summary>Height to hold during the hover, which sets the charge
            /// angle the lock will produce (native NPC.cs:50096).</summary>
            private readonly float _holdAltitude;
            private int _lastState = int.MinValue;
            private bool _dashIssued;
            private Vec2 _ux = new Vec2(1f, 0f);

            public CorridorEscape(bool useDash, float dashLead, int dashAtTimer = 0,
                bool climb = false, float lead = 0f, float climbAbove = 0.75f,
                float dashAim = 0.85f, int jumpPulse = 0, float climbCap = 420f,
                float hoverDescend = 160f, int preposition = 0,
                string hoverVariant = "none", bool counterDash = false,
                float holdX = 0f, int dashAtContact = 0,
                bool gateDashOnPrediction = false, float holdAltitude = 0f, bool retreat = false, float wallBand = 0f, bool adaptiveGate = false, bool bandMode = false)
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
                _counterDash = counterDash;
                _holdX = holdX;
                _retreat = retreat;
                _wallBand = wallBand;
                _adaptiveGate = adaptiveGate;
                _bandMode = bandMode;
                _holdTolerance = 40f;
                _dashAtContact = dashAtContact;
                _gateDashOnPrediction = gateDashOnPrediction;
                _holdAltitude = holdAltitude;
            }

            // Diagnostic: how many ticks the CHARGE escape body below the hover
            // gate actually executed. Some configurations pin at exactly 200
            // contacts per run (3.91's no-climb, 3.94's counter-dash, and band
            // mode in 4.7), and 200 over 97 charges is about two per charge with
            // a suspiciously constant value. Two readings fit that: the escapes
            // run and simply never clear the corridor, or they never run at all.
            // Counting executions separates those, which no contact count can.
            public int EscapeBodyTicks { get; private set; }

            public void Reset()
            {
                _lastState = int.MinValue;
                _dashIssued = false;
                EscapeBodyTicks = 0;

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
                    // ALTITUDE HOLD. Native (NPC.cs:50096-50100) parks the boss
                    // at player.Center + (360 * sign, -200) during the hover, and
                    // the lock then aims along player.Center - boss.Center. So
                    // the charge ANGLE is decided by where the player is while
                    // the boss settles, before the lock happens at all.
                    //
                    // That matters because RequiredClearance is
                    // |75*uy| + |50*ux| + |10*uy| + |21*ux|, while the player's
                    // climb is vertical. A boss 200 above a GROUNDED player
                    // produces a nearly horizontal charge (uy near 0), whose
                    // requirement 50 + 21 = 71 px does not depend on Y at all,
                    // so a vertical climb buys nothing against it. Being
                    // airborne makes the lock steeper, and a steep charge's
                    // requirement does fall with altitude. Holding height is
                    // therefore not a refinement of the escape -- it changes
                    // which escapes exist.
                    if (_holdAltitude > 0f)
                    {
                        var alt = world.FloorY - (frame.Position.Y + frame.Height);
                        if (alt < _holdAltitude - 20f)
                        {
                            controls.Up = true;
                            controls.Jump = true;
                        }
                        else if (alt > _holdAltitude + 20f)
                        {
                            controls.Down = true;
                        }
                    }
                    else if (!frame.Grounded &&
                        player.Center.Y > world.FloorY - _hoverDescend)
                        controls.Up = true;

                    // Position hold, the closed-loop replacement for the drift
                    // that 3.43 identified. The controller steers back towards
                    // an anchor X during the hover, when there is no charge to
                    // answer, so the run cannot wander into a bad configuration
                    // over a hundred charges. Only the horizontal axis is
                    // corrected here; the anchor is a standoff, not a full
                    // formation, so this is deliberately one-dimensional.
                    if (_holdX > 0f)
                    {
                        var offset = player.Center.X - _holdX;
                        if (offset > _holdTolerance)
                        {
                            controls.Left = true;
                            controls.Right = false;
                        }
                        else if (offset < -_holdTolerance)
                        {
                            controls.Right = true;
                            controls.Left = false;
                        }
                    }

                    // RETREAT mode. The close-approach trace closed the
                    // arithmetic exactly: the lock aims the charge at the
                    // player, so perp starts at 0; a horizontal charge needs
                    // req ~ 106 px of vertical clearance; the boss closes at
                    // ~16 px/tick, so contact lands in about 18 ticks, while
                    // 106 px of ascent takes nearer 23. Short by roughly five
                    // ticks, and no parameter fixes that because the escape
                    // arithmetic is already correct -- the missing quantity is
                    // TIME.
                    //
                    // The only controllable source of time is the length of the
                    // charge, and the only way to lengthen it is to be further
                    // away when the lock happens. During a hover the boss parks
                    // on the side it already occupies, so moving away from that
                    // side during the hover forces a longer approach without
                    // changing anything about the escape itself.
                    if (_wallBand > 0f && !IsDashState(world.State))
                    {
                        var mid = (ArenaBandLeft + ArenaBandRight) * 0.5f;
                        var fromMid = player.Center.X - mid;
                        if (Math.Abs(fromMid) > _wallBand)
                        {
                            controls.Left = fromMid > 0f;
                            controls.Right = fromMid < 0f;
                        }
                    }
                    if (_retreat && !IsDashState(world.State))
                    {
                        var awaySide = boss.Center.X >= player.Center.X;
                        controls.Left = awaySide;
                        controls.Right = !awaySide;
                    }

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

                    // The hover frames END here, deliberately. Removing this
                    // return was tried and it is far WORSE, not better: the weak
                    // set went from 119 48 0 82 84 82 44 97 to 157 188 116 170
                    // 156 200 200 200, i.e. 0/8 clean instead of 1/8 and three
                    // openings pinned at the 200 floor. So the return is a gate,
                    // not dead code -- everything below it is the CHARGE
                    // response, and it needs a committed charge line. During a
                    // hover there is no such line: _ux still holds the PREVIOUS
                    // charge's direction, so running the escape body on hover
                    // frames steers by a stale normal.
                    //
                    // This also disposes of the worry from 3.96. The six
                    // bit-identical null results were NOT caused by this return:
                    // the hover lateral blocks (_wallBand, _retreat,
                    // _preposition, _hoverVariant) all sit ABOVE it and their
                    // controls are what gets returned. _preposition and
                    // _hoverVariant demonstrably change outcomes, which proves
                    // the hover path is live; only _wallBand and _retreat showed
                    // no effect, and those are two specific settings rather than
                    // evidence that hover input does nothing.
                    return controls;
                }

                EscapeBodyTicks++;

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

                    // Counter-dash: aim the dash INTO the boss rather than at
                    // the escape side. This is the guide's stated mechanism for
                    // trading the dash for i-frames.
                    //
                    // This deliberately does NOT return here. It used to, and
                    // that silently disabled the whole option: controls.Dash is
                    // only ever set at the end of this method, so an early
                    // return meant the dash was NEVER issued. Every
                    // "counter-dash" measurement before this fix was really
                    // measuring "walk straight at the boss and never dash",
                    // which is why all of them returned an identical 200
                    // contacts per opening regardless of dashAtContact -- the
                    // timing had nothing to act on. Setting only the heading and
                    // letting control fall through to the trigger is what makes
                    // the option mean what its name says.
                    if (_counterDash)
                    {
                        var toward = boss.Center.X >= player.Center.X;
                        controls.Right = toward;
                        controls.Left = !toward;
                    }

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
                // (Tried here: forcing Jump/Up whenever altitude < 60, reading
                // "alt= 21" in the trace as the player being stuck on the
                // ground. That was a misreading -- altitude is computed as
                // FloorY - player.Center.Y, so 21 px means the player is
                // already airborne, just flying low, and pushing it upward
                // drove it into the boss's path instead of away from it.
                // 3300 went from a clean 0 contacts to 104, so the change was
                // reverted rather than kept.)

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
                if (!_counterDash && Math.Abs(_ux.X) < _dashAim &&
                    Math.Abs(_ux.Y) > 0.15f)
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
                // ANGLE BANDS. 4.5 showed the stall is a partition problem: the
                // tools answer different angles, and one scalar boundary leaves a
                // dead band the charges in 33-60 deg fall into. In band mode the
                // two parameters stop being competing cutoffs and become the two
                // EDGES of a partition -- climb takes the middle band
                // (_climbAbove, _dashAim) and the dash takes everything steeper
                // than _dashAim, so the two cover the angle range with no gap and
                // no overlap.
                var climbBand = normalVertical > _climbAbove &&
                    (!_bandMode || normalVertical < _dashAim);
                if (_climb && !escapeDown && climbBand)
                {
                    controls.Jump = _jumpPulse <= 0 ||
                        world.Tick % _jumpPulse == 0;
                    controls.Up = true;
                    if (altitude > _climbCap) controls.Up = false;
                }

                if (!_useDash || _dashIssued || !frame.DashReady ||
                    frame.Dash.DashDelay < 0) return controls;
                // In band mode the climb now owns the middle band, so the dash is
                // restricted to charges steeper than the band edge and the two
                // partition the angles instead of one pre-empting the other.
                if (_bandMode && normalVertical > _dashAim) return controls;
                if (world.StateTimer < _dashAtTimer) return controls;

                // Distance along the line still to run before the closest
                // approach. The dash is spent once that is inside the lead, so
                // the eighteen-tick decay is still live when the boss arrives.
                var toBoss = (frame.Position.X + frame.Width * 0.5f -
                    boss.Center.X) * _ux.X + (frame.Position.Y + frame.Height * 0.5f -
                    boss.Center.Y) * _ux.Y;
                if (toBoss > _dashLead) return controls;

                // ADAPTIVE GATE. The gate sweep (3.85) showed the right gate
                // depends on the player's LIVE state, and that per-opening
                // presets are the wrong shape (3.87). So compute the choice
                // instead of tabulating it. For this charge, how many ticks
                // remain before the boss arrives, and how many would each escape
                // need to clear the corridor?
                //
                //   ticksToContact ~ toBoss / closingSpeed, capped by the ticks
                //                     left in the charge
                //   climbTicks     ~ need / (4.6 * |ux|)   the climb moves the
                //                     player along the normal at 4.6*|ux|/tick
                //   dashTicks      ~ need / (172 * |uy| / 15)  the dash delivers
                //                     172*|uy| across its 15 live ticks
                //
                // Whichever escape is feasible gets taken; when neither is, the
                // dash is still spent, because its 15 immune ticks absorb the
                // contact even when the geometry does not. This replaces the
                // fixed |ux| threshold, which cannot be right for every state.
                if (_adaptiveGate && Math.Abs(_ux.X) > 0.15f)
                {
                    const float closingSpeed = 16f;
                    var ticksLeft = 30f - world.StateTimer;
                    if (ticksLeft < 1f) ticksLeft = 1f;
                    var ticksToContact = toBoss / closingSpeed;
                    if (ticksToContact > ticksLeft) ticksToContact = ticksLeft;
                    if (ticksToContact < 1f) ticksToContact = 1f;

                    var climbRate = 4.6f * Math.Abs(_ux.X);
                    var climbTicks = climbRate > 0.01f
                        ? need / climbRate : 9999f;
                    var dashPerTick = 172f * Math.Abs(_ux.Y) / 15f;
                    var dashTicks = dashPerTick > 0.01f
                        ? need / dashPerTick : 9999f;

                    if (climbTicks > ticksToContact)
                    {
                        // Prefer the dash: either it clears, or its immune ticks
                        // are all that stands between the player and contact.
                        var dashToward = -_ux.Y * escapeDir > 0f;
                        controls.Right = dashToward;
                        controls.Left = !dashToward;
                        controls.Up = false;
                        controls.Down = false;
                        if (dashTicks > ticksToContact)
                            controls.Dash = true;
                    }
                }

                // Scheduled i-frames. The 3.50 trace showed the run survives by
                // eating charges through the dash's 15 immune ticks rather than
                // by out-clearing them, so those ticks are the resource to
                // spend and spending them early wastes them. The boss closes
                // along the committed line at a known speed, so how many ticks
                // remain before it reaches the player is computable, and the
                // dash should fire when that count is small enough for the 15
                // immune ticks to still cover the arrival.
                if (_dashAtContact > 0)
                {
                    // Closing speed along the line: the boss's own velocity
                    // projected on the line, plus the player's, which is what
                    // decides whether the gap is shrinking.
                    var closing = (boss.Velocity.X - frame.Velocity.X) * _ux.X +
                        (boss.Velocity.Y - frame.Velocity.Y) * _ux.Y;
                    if (closing > 0.1f)
                    {
                        var ticksToContact = -toBoss / closing;
                        if (ticksToContact > _dashAtContact) return controls;
                    }
                }

                // Prediction-error gating. The boss locks onto the player's
                // position at commit and never re-aims, so once the player is
                // clear of the committed LINE by RequiredClearance, that charge
                // cannot hit whatever the boss does afterwards. Dashing then is
                // worse than wasted: it burns 15 immune ticks and a 20-tick
                // cooldown that the next charge may need. RequiredClearance is
                // exactly "how far off the line the centre must be for the two
                // rectangles not to overlap", so it is the right quantity to
                // gate on rather than a tuned distance.
                if (_gateDashOnPrediction)
                {
                    var offX = frame.Position.X + frame.Width * 0.5f - boss.Center.X;
                    var offY = frame.Position.Y + frame.Height * 0.5f - boss.Center.Y;
                    var gateNeed = RequiredClearance(_ux.X, _ux.Y);
                    var reach = 75f + 10f;
                    if (offX * offX + offY * offY >
                        (gateNeed + reach) * (gateNeed + reach))
                        return controls;
                }
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

            // ZERO-CONTACT AUDIT. Everything the run logged, not a count, plus
            // the hit source breakdown, so "0 boss contacts" cannot be a
            // filtering artefact and any bubble or sharkron contact still shows.
            Console.WriteLine();
            Console.WriteLine("  AUDIT: perpendicular escape, dash immunity ON, all threats:");
            var auditRun = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                0.88f, 0.85f, 0, 420f, 160f, 0, "none", false), 8000,
                maxHits: 999, bossOnly: false, bubbles: true, tornados: true,
                jumpSpeed: 8.91f, wingTimeMax: 100f, autoJump: true);
            var bossN = 0;
            var bubbleN = 0;
            var otherN = 0;
            foreach (var line in auditRun.HitLog)
            {
                if (line.Contains("src boss")) bossN++;
                else if (line.Contains("src bubble")) bubbleN++;
                else otherN++;
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    ticks={0} charges={1} totalHits={2} boss={3} bubble={4} " +
                "other={5} hitLogEntries={6}",
                auditRun.Ticks, auditRun.Charges, auditRun.Hits, bossN, bubbleN,
                otherN, auditRun.HitLog.Count));
            foreach (var line in auditRun.HitLog)
                Console.WriteLine("      " + line);

            // And the same run with every projectile threat disabled, which is
            // the isolated question "can the boss body ever touch the player
            // under this controller".
            Console.WriteLine();
            Console.WriteLine("  AUDIT: boss body only:");
            var bodyRun = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                0.88f, 0.85f, 0, 420f, 160f, 0, "none", false), 8000,
                maxHits: 999, bossOnly: true, bubbles: true,
                jumpSpeed: 8.91f, wingTimeMax: 100f, autoJump: true);
            var bodyN = 0;
            foreach (var line in bodyRun.HitLog)
                if (line.Contains("src boss")) bodyN++;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    ticks={0} charges={1} totalHits={2} bossContacts={3}",
                bodyRun.Ticks, bodyRun.Charges, bodyRun.Hits, bodyN));

            // A/B: the same controller with the immunity switched back off.
            // Without this the zero could be the controller rather than the
            // mechanic, and which one it is is the whole question.
            Console.WriteLine();
            Console.WriteLine("  A/B: dash immunity OFF (same controller):");
            FishronDashImmunityDisabled = true;
            var offRun = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                0.88f, 0.85f, 0, 420f, 160f, 0, "none", false), 8000,
                maxHits: 999, bossOnly: true, bubbles: true,
                jumpSpeed: 8.91f, wingTimeMax: 100f, autoJump: true);
            FishronDashImmunityDisabled = false;
            var offN = 0;
            foreach (var line in offRun.HitLog)
                if (line.Contains("src boss")) offN++;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    immunity=OFF ticks={0} charges={1} bossContacts={2}",
                offRun.Ticks, offRun.Charges, offN));

            // The dash's contact immunity is now modelled (eocDash 15), so the
            // open question is whether dashing INTO the boss clears contacts.
            // The counter-dash is a different action from the geometric escape,
            // not a setting of it, so the two are measured side by side.
            Console.WriteLine();
            Console.WriteLine("  counter-dash vs perpendicular escape (immunity ON):");
            foreach (var counterMode in new[] { false, true })
            {
                var run = RunFight(new CorridorEscape(true, 240f, 8, true, 0f,
                    0.88f, 0.85f, 0, 420f, 160f, 0, "none", counterMode), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    jumpSpeed: 8.91f, wingTimeMax: 100f, autoJump: true);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    counterDash={0,-5} ticks={1,5} charges={2,3} " +
                    "bossContacts={3,4}", counterMode, run.Ticks, run.Charges,
                    n));
            }

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

        /// <summary>
        /// Verifies the zero-body-contact result beyond the single opening it
        /// was found on, for both admitted wing sets.
        ///
        /// The 3.41 result is narrow: one opening position (startX 3300), boss
        /// body only. Before it can be called a movement strategy it has to
        /// survive other openings and both loadouts, because the whole point of
        /// a formulaic answer is that it does not depend on where the fight
        /// happens to start. Run with --fishron-generalize.
        /// </summary>
        public static void FishronGeneralize()
        {
            var starts = new[] { 2400f, 2800f, 3300f, 3800f, 4300f, 4800f,
                5300f, 5800f };
            Console.WriteLine("== opening-position generality, boss body only ==");
            Console.WriteLine("  (zero body contact must hold across openings)");
            foreach (var profile in new[] { WeakWings(), StrongWings() })
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}", profile.Name));
                var clean = 0;
                foreach (var startX in starts)
                {
                    var run = RunFight(new CorridorEscape(true, profile.Lead,
                        profile.DashAt, true, 0f, profile.ClimbAbove,
                        profile.DashAim, 0, profile.ClimbCap,
                        profile.HoverDescend, 0, "none", false), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: profile.JumpSpeed,
                        wingTimeMax: profile.FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var line in run.HitLog)
                        if (line.Contains("src boss")) n++;
                    if (n == 0) clean++;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    startX={0,5:F0} ticks={1,5} charges={2,3} " +
                        "bossContacts={3,4}", startX, run.Ticks, run.Charges,
                        n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    clean openings: {0} of {1}", clean, starts.Length));
            }

            // Is the opening dependence an ARENA-geometry effect or a
            // controller effect? The controller drifts, so with a bounded
            // runway it eventually reaches a wall and is pinned there. Repeating
            // two openings with a far wider runway separates the two: if the
            // failures vanish, the controller is sound and the arena was the
            // limit; if they persist, the controller itself is the problem.
            Console.WriteLine();
            Console.WriteLine("== drift test: bounded vs wide runway ==");
            var savedLeft = ArenaBandLeft;
            var savedRight = ArenaBandRight;
            foreach (var wide in new[] { false, true })
            {
                if (wide) { ArenaBandLeft = -20000f; ArenaBandRight = 30000f; }
                else { ArenaBandLeft = 1000f; ArenaBandRight = 6000f; }
                foreach (var startX in new[] { 3300f, 5800f, 2400f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var line in run.HitLog)
                        if (line.Contains("src boss")) n++;
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    runway={0,-6} startX={1,5:F0} charges={2,3} " +
                        "bossContacts={3,4}", wide ? "wide" : "bounded",
                        startX, run.Charges, n));
                }
            }
            ArenaBandLeft = savedLeft;
            ArenaBandRight = savedRight;

            // Position hold: the closed-loop answer to the drift. Each opening
            // holds its own anchor, so zero contact across openings would mean
            // the strategy no longer depends on a wall stopping the drift.
            Console.WriteLine();
            Console.WriteLine("== position hold, weak set ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, startX), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true,
                    holdX: startX);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    holdX={0,5:F0} charges={1,3} bossContacts={2,4}",
                    startX, run.Charges, n));
            }

            // The null hypothesis: the hover branch's horizontal bits may
            // simply be harmful, in which case the best hover input is none at
            // all. _hoverDead makes the controller hold no horizontal direction
            // while not dashing, which is different from every variant so far
            // (they all assigned Left/Right there).
            Console.WriteLine();
            Console.WriteLine("== hover horizontal input removed (null hypothesis) ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} charges={1,3} bossContacts={2,4}",
                    startX, run.Charges, n));
            }

            // WHAT MAKES 3300 SPECIAL. The previous two rounds established that
            // the result depends on the opening but not why. This records where
            // the player actually is across the run and where each contact
            // happens, which distinguishes the two candidate stories: the run
            // settles into a favourable formation, or it drifts into the arena
            // wall and is pinned there.
            Console.WriteLine();
            Console.WriteLine("== opening x-position and contact locations (weak set) ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var n = 0;
                var first = "";
                var last = "";
                foreach (var line in run.HitLog)
                {
                    if (!line.Contains("src boss")) continue;
                    n++;
                    // The player coordinate is the last parenthesised pair.
                    var at = line.LastIndexOf("player (");
                    var coords = at >= 0 ? line.Substring(at + 8).TrimEnd(')') : "?";
                    if (n == 1) first = "tick " + line.Split(' ')[1] + " at " + coords;
                    last = "tick " + line.Split(' ')[1] + " at " + coords;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} contacts={1,4} first=[{2}] last=[{3}]",
                    startX, n, first, last));
            }

            // How far the run actually travels. The failing openings put their
            // first and last contacts at the band edges (x 1014 and x 5483 for a
            // runway of [1000, 6000]), which says the controller is not merely
            // drifting but commuting the full width and taking hits at the
            // turnarounds. The excursion is measured here instead of inferred.
            Console.WriteLine();
            Console.WriteLine("== x excursion per opening (weak set) ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var n = 0;
                foreach (var line in run.HitLog)
                    if (line.Contains("src boss")) n++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} minX={1,8:F1} maxX={2,8:F1} " +
                    "excursion={3,8:F1} finalX={4,8:F1} contacts={5,4}",
                    startX, run.MinPlayerX, run.MaxPlayerX,
                    run.MaxPlayerX - run.MinPlayerX, run.FinalPlayerX, n));
            }

            // The excursion result kills the spatial story: every opening
            // traverses the full runway (min 1000, max 5980, span 4980), so
            // 3300 does not avoid the walls at all. What differs between the
            // openings is only WHEN the oscillation reaches the charge lines,
            // i.e. a phase offset. If that is the mechanism, then a delay on
            // the first dash -- the only phase knob the controller has -- should
            // substitute for the opening, and every opening should have a
            // working delay.
            Console.WriteLine();
            Console.WriteLine("== phase sweep: first-dash delay x opening ==");
            Console.WriteLine("  (a working delay per opening means phase, not place)");
            foreach (var startX in new[] { 2400f, 3300f, 4800f, 5800f })
            {
                var line = new System.Text.StringBuilder(string.Format(
                    CultureInfo.InvariantCulture, "    startX={0,5:F0} :", startX));
                var best = 9999;
                var bestAt = -1;
                foreach (var delay in new[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18,
                    20, 24, 28, 32 })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        delay, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    line.Append(string.Format(CultureInfo.InvariantCulture,
                        " {0}:{1}", delay, n));
                    if (n < best) { best = n; bestAt = delay; }
                }
                Console.WriteLine(line.ToString());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "                   best={0} at delay={1}", best, bestAt));
            }

            // What the ONE working configuration actually does, across the
            // whole run. Every previous attempt to improve on it guessed at the
            // mechanism; this reads it off instead.
            Console.WriteLine();
            Console.WriteLine("== close-approach trace, FAILING config (startX 2400) ==");
            RunFight(new CorridorEscape(true, WeakWings().Lead,
                WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                WeakWings().DashAim, 0, WeakWings().ClimbCap,
                WeakWings().HoverDescend, 0, "none", false, 0f), 2000,
                maxHits: 999, bossOnly: true, bubbles: true, startX: 2400f,
                jumpSpeed: WeakWings().JumpSpeed,
                wingTimeMax: WeakWings().FlyTicks, autoJump: true,
                traceClose: true);

            // Scheduled i-frames (3.50). Instead of spending the dash as soon
            // as the charge closes inside the lead, fire it a fixed number of
            // ticks before the boss is predicted to arrive, so the fifteen
            // immune ticks cover the contact. Swept over openings, because the
            // whole question is whether scheduling makes the result independent
            // of where the fight starts -- which nothing so far has.
            Console.WriteLine();
            Console.WriteLine("== scheduled i-frames: dashAtContact x opening ==");
            foreach (var startX in new[] { 2400f, 3300f, 4800f, 5800f })
            {
                var line = new System.Text.StringBuilder(string.Format(
                    CultureInfo.InvariantCulture, "    startX={0,5:F0} :", startX));
                var best = 9999;
                var bestAt = -1;
                foreach (var dac in new[] { 0, 2, 4, 6, 8, 10, 12, 14, 16 })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f, dac), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    line.Append(string.Format(CultureInfo.InvariantCulture,
                        " {0}:{1}", dac, n));
                    if (n < best) { best = n; bestAt = dac; }
                }
                Console.WriteLine(line.ToString());
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "                   best={0} at dashAtContact={1}", best,
                    bestAt));
            }

            // dashAtContact had NO effect, which is itself informative: the
            // dash fires early enough (dashLead 240) that predicted contact is
            // never inside the scheduled window, so the schedule never binds.
            // Native confirms the size of the waste -- eocDash starts at 15 and
            // decrements once per tick while dashDelay runs 15 down to zero, so
            // roughly 15 ticks of immunity are spent before the boss is close.
            // Sweeping the lead alongside the schedule is what lets the
            // schedule actually decide anything.
            Console.WriteLine();
            Console.WriteLine("== lead x schedule (weak set, startX 3300 and 5800) ==");
            foreach (var startX in new[] { 3300f, 5800f })
            {
                foreach (var lead in new[] { 40f, 80f, 120f, 160f, 240f })
                {
                    var line = new System.Text.StringBuilder(string.Format(
                        CultureInfo.InvariantCulture,
                        "    startX={0,5:F0} lead={1,3:F0} :", startX, lead));
                    foreach (var dac in new[] { 0, 3, 6, 9, 12 })
                    {
                        var run = RunFight(new CorridorEscape(true, lead,
                            WeakWings().DashAt, true, 0f,
                            WeakWings().ClimbAbove, WeakWings().DashAim, 0,
                            WeakWings().ClimbCap, WeakWings().HoverDescend, 0,
                            "none", false, 0f, dac), 8000, maxHits: 999,
                            bossOnly: true, bubbles: true, startX: startX,
                            jumpSpeed: WeakWings().JumpSpeed,
                            wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                        var n = 0;
                        foreach (var l in run.HitLog)
                            if (l.Contains("src boss")) n++;
                        line.Append(string.Format(CultureInfo.InvariantCulture,
                            " dac{0}:{1}", dac, n));
                    }
                    Console.WriteLine(line.ToString());
                }
            }

            // Prediction-error gating: dash only when the committed line would
            // actually hit. If the surviving run's fragility comes from spending
            // i-frames on charges that were already misses, this should show up
            // as a large improvement at the openings that previously failed.
            Console.WriteLine();
            Console.WriteLine("== prediction-gated dash (weak set) ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var off = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f, 0, false),
                    8000, maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var on = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f, 0, true),
                    8000, maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var nOff = 0;
                foreach (var l in off.HitLog)
                    if (l.Contains("src boss")) nOff++;
                var nOn = 0;
                foreach (var l in on.HitLog)
                    if (l.Contains("src boss")) nOn++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} gateOff={1,4} gateOn={2,4}", startX,
                    nOff, nOn));
            }

            // ALTITUDE HOLD. Native parks the boss at player + (360*sign, -200)
            // during the hover and then aims the lock along player - boss, so the
            // height the player holds decides the charge angle. A grounded
            // player gets a near-horizontal charge whose requirement is 71 px
            // that no vertical climb can pay. Sweeping the held height therefore
            // tests a different mechanism from every previous round: not how to
            // escape the charge, but which charges the boss is given.
            Console.WriteLine();
            Console.WriteLine("== altitude hold x opening (weak set) ==");
            foreach (var alt in new[] { 0f, 120f, 200f, 280f, 360f, 440f })
            {
                var line = new System.Text.StringBuilder(string.Format(
                    CultureInfo.InvariantCulture, "    alt={0,3:F0} :", alt));
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f, 0,
                        false, alt), 8000, maxHits: 999, bossOnly: true,
                        bubbles: true, startX: startX,
                        jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    if (n == 0) clean++;
                    line.Append(string.Format(CultureInfo.InvariantCulture,
                        " {0}", n));
                }
                Console.WriteLine(line.ToString() + string.Format(
                    CultureInfo.InvariantCulture, "   clean={0}/8", clean));
            }

            // WALL DRAG. 3.54 established that the hover parks the boss at
            // player.Center + (360*sign, -200), so moving the player during the
            // hover MOVES where the boss will end up -- the drift is not
            // passively entering bad configurations, it is dragging the boss
            // into them. The untried use of that is the reverse: drag the boss
            // deliberately. Pinning the player near a wall should park the boss
            // a fixed 360 px off that wall, which constrains where the next
            // charge can start and which lines it can take.
            //
            // The anchors are expressed from the walls:
            //   wall     park the player 120 px inside the left wall
            //   wallR    park the player 120 px inside the right wall
            //   centre   park the player near the middle of the runway
            Console.WriteLine();
            Console.WriteLine("== wall drag: hover anchor x opening (weak set) ==");
            foreach (var anchor in new[]
            {
                new { Name = "none   ", X = 0f },
                new { Name = "wall   ", X = ArenaBandLeft + 120f },
                new { Name = "wallR  ", X = ArenaBandRight - 120f },
                new { Name = "quarter", X = ArenaBandLeft +
                    (ArenaBandRight - ArenaBandLeft) * 0.25f },
                new { Name = "middle ", X = ArenaBandLeft +
                    (ArenaBandRight - ArenaBandLeft) * 0.5f },
            })
            {
                var line = new System.Text.StringBuilder(string.Format(
                    CultureInfo.InvariantCulture, "    {0} :", anchor.Name));
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, anchor.X),
                        8000, maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    if (n == 0) clean++;
                    line.Append(string.Format(CultureInfo.InvariantCulture,
                        " {0,3}", n));
                }
                Console.WriteLine(line.ToString() + string.Format(
                    CultureInfo.InvariantCulture, "   clean={0}/8", clean));
            }

            // LOCK ANGLE DISTRIBUTION. 3.54 read the native hover as parking the
            // boss at player + (360*sign, -200), which should make the lock
            // direction almost purely horizontal. If that holds, the whole
            // difficulty is explained in one number: RequiredClearance for a
            // horizontal charge is 50 + 21 = 71 px measured VERTICALLY, and the
            // player's climb is vertical, so climbing does buy that -- but only
            // 71 px of it in a window of about 13 ticks. Measuring the actual
            // angle distribution turns that from an argument into a number.
            Console.WriteLine();
            Console.WriteLine("== lock angle distribution (weak set, startX 3300) ==");

            var angleRun = RunFight(new CorridorEscape(true, WeakWings().Lead,
                WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                WeakWings().DashAim, 0, WeakWings().ClimbCap,
                WeakWings().HoverDescend, 0, "none", false, 0f), 3000,
                maxHits: 999, bossOnly: true, bubbles: true,
                jumpSpeed: WeakWings().JumpSpeed,
                wingTimeMax: WeakWings().FlyTicks, autoJump: true);

            var angles = new List<double>();
            var angleRe = new System.Text.RegularExpressions.Regex(
                @"angle\s+([0-9]+\.[0-9]+)deg");
            foreach (var entry in angleRun.ChargeLog)
            {
                var m = angleRe.Match(entry);
                if (!m.Success) continue;
                double a;
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out a))
                    angles.Add(a);
            }
            if (angles.Count > 0)
            {
                angles.Sort();
                var near = 0;
                foreach (var a in angles)
                    if (a >= 75.0) near++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    n={0} min={1:F1} median={2:F1} max={3:F1} " +
                    "atOrAbove75deg={4} ({5:F0}%)",
                    angles.Count, angles[0], angles[angles.Count / 2],
                    angles[angles.Count - 1], near,
                    100.0 * near / angles.Count));
            }
            else
            {
                Console.WriteLine("    (no angle entries parsed)");
            }

            // WHERE do the contacts happen, by phase? The guide treats the three
            // phases as separate problems ("3+1+3+1", "formula time", "jumping
            // rope"), and every run so far has conflated them into one 8000-tick
            // number. If one phase is responsible for most of the contacts, the
            // work belongs there rather than spread over the whole fight.
            Console.WriteLine();
            Console.WriteLine("== contacts by phase (weak set) ==");
            foreach (var startX in new[] { 2400f, 3300f, 4800f, 5800f })
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                    maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var p1 = 0;
                var p2 = 0;
                var p3 = 0;
                foreach (var line in run.HitLog)
                {
                    if (!line.Contains("src boss")) continue;
                    if (line.Contains("phase One")) p1++;
                    else if (line.Contains("phase Two")) p2++;
                    else p3++;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} total={1,4} phase1={2,4} phase2={3,4} " +
                    "phase3={4,4}", startX, p1 + p2 + p3, p1, p2, p3));
            }

            // Phase 1 only. The by-phase breakdown showed every contact in the
            // whole fight happens in phase one; phases two and three are already
            // clean at every opening. Phase one is the "5+1" burst, and it is
            // also the only phase whose park offset is 300 rather than 360, so
            // the search belongs here and nowhere else.
            Console.WriteLine();
            Console.WriteLine("== phase 1 parameter search (weak, contacts in p1 only) ==");
            var bestRow = "";
            var bestCount = 9999;
            foreach (var above in new[] { 0.80f, 0.85f, 0.88f, 0.90f, 0.93f })
            {
                foreach (var lead in new[] { 120f, 180f, 240f, 300f })
                {
                    var total = 0;
                    var worst = 0;
                    foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f,
                        4300f, 4800f, 5300f, 5800f })
                    {
                        var run = RunFight(new CorridorEscape(true, lead,
                            WeakWings().DashAt, true, 0f, above,
                            WeakWings().DashAim, 0, WeakWings().ClimbCap,
                            WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                            maxHits: 999, bossOnly: true, bubbles: true,
                            startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                            wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                        var n = 0;
                        foreach (var l in run.HitLog)
                            if (l.Contains("src boss")) n++;
                        total += n;
                        if (n > worst) worst = n;
                    }
                    if (worst < bestCount)
                    {
                        bestCount = worst;
                        bestRow = string.Format(CultureInfo.InvariantCulture,
                            "above={0:F2} lead={1:F0} sum={2} worst={3}", above,
                            lead, total, worst);
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    above={0:F2} lead={1,3:F0} sum={2,5} worst={3,4}",
                        above, lead, total, worst));
                }
            }
            Console.WriteLine("    BEST: " + bestRow);

            // Per-charge geometry for the FAILING phase one, at an opening that
            // fails. Every search so far optimised a scalar; this prints the
            // actual angle / requirement / achieved clearance per charge, which
            // is what says WHICH charge is short and BY HOW MUCH. The guide's
            // "open up vertical distance" implies the shortfall should be in the
            // vertical component, so the angle column is the one to read.
            Console.WriteLine();
            Console.WriteLine("== phase 1 charge geometry, startX 2400 (failing) ==");
            var geoRun = RunFight(new CorridorEscape(true, WeakWings().Lead,
                WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                WeakWings().DashAim, 0, WeakWings().ClimbCap,
                WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                maxHits: 999, bossOnly: true, bubbles: true, startX: 2400f,
                jumpSpeed: WeakWings().JumpSpeed,
                wingTimeMax: WeakWings().FlyTicks, autoJump: true);
            // Wall-pinning test. The failing charges show high clearance
            // (maxPerp 156, 309) yet still register hits, and their player
            // coordinates sit at x 1000 and 1062 -- the arena's left edge. If
            // that is the mechanism, hits should cluster at the walls and be
            // rare in mid-arena, which is a different problem from "the escape
            // is too slow" and points at the guide's own advice to keep the boss
            // controlled at the platform EDGE rather than being cornered by it.
            // Is "at" inside the charge it is printed with? The log is written
            // when the NEXT charge begins, and it prints chargeHitTick, which
            // belongs to the charge that has just ENDED. So the hit tick must
            // lie within that charge's lifetime, start + num6. If it does not,
            // the hit and the geometry being shown are different events and the
            // whole per-charge clearance analysis is misattributed.
            foreach (var entry in geoRun.ChargeLog)
            {
                if (!entry.Contains("hit=True")) continue;
                Console.WriteLine("    HIT " + entry);
            }
            var inside = 0;
            var outside = 0;
            var noTick = 0;
            var spanMin = 9999;
            var spanMax = -9999;
            var hitCharges = 0;
            foreach (var entry in geoRun.ChargeLog)
            {
                if (!entry.Contains("hit=True")) continue;
                hitCharges++;
                var mStart = System.Text.RegularExpressions.Regex.Match(entry,
                    @"start\s+(\d+)");
                var mAt = System.Text.RegularExpressions.Regex.Match(entry,
                    @"at\s+(-?\d+)");
                if (!mStart.Success || !mAt.Success) continue;
                var st = int.Parse(mStart.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                var at = int.Parse(mAt.Groups[1].Value,
                    CultureInfo.InvariantCulture);
                if (at < 0) { noTick++; continue; }
                var span = at - st;
                if (span < spanMin) spanMin = span;
                if (span > spanMax) spanMax = span;
                // Phase-one charge lifetime is num6 = 30 ticks.
                if (span >= 0 && span <= 30) inside++;
                else outside++;
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    hitCharges={0} insideLifetime={1} outside={2} " +
                "noTick={3} spanRange=[{4},{5}] (lifetime is 30)",
                hitCharges, inside, outside, noTick, spanMin, spanMax));

            // RETREAT test. The close-approach trace says the escape is short by
            // roughly five ticks and that no parameter can create them, because
            // the escape arithmetic is already correct -- what is missing is
            // TIME, and the only controllable source of time is the length of
            // the charge. Retreating from the boss during the hover should force
            // a longer approach. Measured at every opening, because the whole
            // question is whether it is systematic or another single-opening
            // accident.
            Console.WriteLine();
            Console.WriteLine("== retreat-during-hover vs baseline (weak) ==");
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                var withRetreat = RunFight(new CorridorEscape(true,
                    WeakWings().Lead, WeakWings().DashAt, true, 0f,
                    WeakWings().ClimbAbove, WeakWings().DashAim, 0,
                    WeakWings().ClimbCap, WeakWings().HoverDescend, 0, "away",
                    false, 0f), 8000, maxHits: 999,
                    bossOnly: true, bubbles: true, startX: startX,
                    jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var n = 0;
                foreach (var l in withRetreat.HitLog)
                    if (l.Contains("src boss")) n++;
                var viaFlag = RunFight(new CorridorEscape(true,
                    WeakWings().Lead, WeakWings().DashAt, true, 0f,
                    WeakWings().ClimbAbove, WeakWings().DashAim, 0,
                    WeakWings().ClimbCap, WeakWings().HoverDescend, 0, "none",
                    false, 0f, 0, false, 0f, true), 8000, maxHits: 999,
                    bossOnly: true, bubbles: true, startX: startX,
                    jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var m = 0;
                foreach (var l in viaFlag.HitLog)
                    if (l.Contains("src boss")) m++;
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} variantAway={1,4} retreatFlag={2,4}",
                    startX, n, m));
            }

            // DASH-LEAD sweep. _dashLead is compared against toBoss, the boss's
            // remaining travel along the committed line, and the dash fires when
            // that drops inside the lead. The close trace showed it firing at
            // dist = 95 with a lead of 240, which is much later than the comment
            // intends ("the eighteen-tick decay is still live when the boss
            // arrives"). The reason is that the boss only starts about 184 px
            // away, so toBoss is ALREADY inside any lead near 240 at the moment
            // the charge begins -- the comparison cannot hold it back, and the
            // dash ends up last-moment. Raising the lead should make it fire on
            // time, which is exactly the 5-13 tick shortfall 3.76 derived.
            Console.WriteLine();
            Console.WriteLine("== dashLead sweep, phase 1 (weak) ==");
            foreach (var lead in new[] { 240f, 320f, 400f, 480f, 560f, 700f })
            {
                var total = 0;
                var worst = 0;
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    if (n > worst) worst = n;
                    if (n == 0) clean++;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    dashLead={0,4:F0} sum={1,5} worst={2,4} cleanOpenings={3}/8",
                    lead, total, worst, clean));
            }

            // PHASE-1-ONLY gate sweep. The climb/dash split is gated by
            // climbAbove (the climb covers shallow charges) and dashAim (the
            // dash covers steep ones), crossing near |ux| = 0.85. Both were
            // tuned against whole-fight numbers where all three phases were
            // mixed, which -- now that contacts are known to be phase one only
            // (3.67) -- means they were tuned against a signal that phase two
            // and three only diluted. The failing opening is 2400, so sweep the
            // gates there and report each opening separately rather than as one
            // sum, since a change that helps 2400 while wrecking 3300 is not a
            // solution.
            Console.WriteLine();
            Console.WriteLine("== phase-1 gate sweep (weak), contacts by opening ==");
            foreach (var above in new[] { 0.50f, 0.65f, 0.75f, 0.85f, 0.95f })
            {
                foreach (var aim in new[] { 0.70f, 0.85f, 1.00f })
                {
                    var cells = new System.Text.StringBuilder();
                    var total = 0;
                    foreach (var startX in new[] { 2400f, 2800f, 3300f, 4800f,
                        5800f })
                    {
                        var run = RunFight(new CorridorEscape(true,
                            WeakWings().Lead, WeakWings().DashAt, true, 0f,
                            above, aim, 0, WeakWings().ClimbCap,
                            WeakWings().HoverDescend, 0, "none", false, 0f),
                            8000, maxHits: 999, bossOnly: true, bubbles: true,
                            startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                            wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                        var n = 0;
                        foreach (var l in run.HitLog)
                            if (l.Contains("src boss")) n++;
                        total += n;
                        cells.Append(string.Format(CultureInfo.InvariantCulture,
                            "{0,5}", n));
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    climbAbove={0:F2} dashAim={1:F2} |{2} | sum={3,5}",
                        above, aim, cells, total));
                }
            }

            // WALL-BIAS sweep. The gate sweep just showed something the whole
            // previous approach missed: climbAbove 0.50 / dashAim 0.85 takes the
            // 2400 opening from 119 contacts down to 13 -- so 2400 IS solvable --
            // while wrecking 3300 (0 -> 95). 2400 is the opening that starts at
            // the LEFT arena wall and 3300 is mid-arena, and every attempt so
            // far has optimised a single gate for all openings at once. The
            // guide's own instruction is to keep the boss at the platform EDGE
            // (280s) rather than to stand there, so what is missing may simply be
            // that the controller never avoids the walls it can be cornered
            // against. Steer towards the arena centre during the hover, when
            // there is no charge to answer, and see whether that removes the
            // opening dependence instead of trading one opening for another.
            Console.WriteLine();
            Console.WriteLine("== wall-bias during hover (weak), contacts by opening ==");
            foreach (var band in new[] { 0f, 400f, 700f, 1000f, 1300f })
            {
                var cells = new System.Text.StringBuilder();
                var total = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true,
                        hoverVariant: "none", holdX: 0f, wallBand: band);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    wallBand={0,5:F0} |{1} | sum={2,5}", band, cells, total));
            }

            // POSITION-DEPENDENT gates. The gate sweep produced a real
            // discovery that every previous round missed: climbAbove 0.50 with
            // dashAim 0.85 takes the 2400 opening from 119 contacts to 13, so
            // that opening IS solvable, while that same setting destroys 3300
            // (0 -> 95). Every optimization so far searched for ONE gate good
            // for all openings, which is why it always ended in a trade. But
            // 2400 starts against the left arena wall and 3300 in mid-arena, and
            // the wall truncates the escape, so the correct gate can legitimately
            // depend on where the player is. Select it per opening.
            Console.WriteLine();
            Console.WriteLine("== position-dependent gate (weak) ==");
            var pdCells = new System.Text.StringBuilder();
            var pdTotal = 0;
            foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                4800f, 5300f, 5800f })
            {
                // Near the walls the climb has less room, so favour the dash
                // (low climbAbove gate means the climb is used less); mid-arena
                // keeps the tuned 0.85.
                var mid = (ArenaBandLeft + ArenaBandRight) * 0.5f;
                var nearWall = Math.Abs(startX - mid) > 1200f;
                var above = nearWall ? 0.50f : 0.85f;
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, above, 0.85f, 0,
                    WeakWings().ClimbCap, WeakWings().HoverDescend, 0, "none",
                    false, 0f), 8000, maxHits: 999, bossOnly: true, bubbles: true,
                    startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var n = 0;
                foreach (var l in run.HitLog)
                    if (l.Contains("src boss")) n++;
                pdTotal += n;
                pdCells.Append(string.Format(CultureInfo.InvariantCulture,
                    "{0,5}", n));
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    positionDependent |{0} | sum={1,5}", pdCells, pdTotal));

            // ADAPTIVE vs FIXED. 3.85 found a gate that nearly solves 2400 but
            // destroys 3300, and 3.87 showed per-opening presets are the wrong
            // shape. This compares the fixed gates against choosing the escape
            // from the live state (ticks needed vs ticks available), which is
            // the shape 3.87 says is required.
            Console.WriteLine();
            Console.WriteLine("== adaptive vs fixed gates (weak), contacts by opening ==");
            for (var mode = 0; mode < 3; mode++)
            {
                var label = mode == 0 ? "fixed 0.88/0.85 (real baseline)"
                    : mode == 1 ? "fixed 0.50/0.85 (best for 2400)"
                    : "adaptive (state-driven)";
                var cells = new System.Text.StringBuilder();
                var total = 0;
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var above = mode == 1 ? 0.50f : WeakWings().ClimbAbove;
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, above, WeakWings().DashAim,
                        0, WeakWings().ClimbCap, WeakWings().HoverDescend, 0,
                        "none", false, 0f, 0, false, 0f, mode == 2), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    if (n == 0) clean++;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    {0,-26} |{1} | sum={2,5} clean={3}/8", label, cells,
                    total, clean));
            }

            // CAPABILITY question, made testable. For a HORIZONTAL charge the
            // perpendicular is vertical, so the dash's 172 px -- which is
            // horizontal -- contributes nothing to clearance; only the climb
            // helps, and at 4.6 px/tick it needs ~23 ticks against ~18
            // available. So a horizontal charge is UNREACHABLE by dodging, and
            // the fix cannot be a better escape choice. The way out is to stop
            // horizontal charges happening at all: the boss parks 200 px above
            // the player, so if the player stays LOW the charge is steep, the
            // perpendicular is nearly horizontal, and the dash -- 172 px of it
            // -- does the work. climbAbove is the gate that decides whether the
            // climb is attempted, so setting it above 1.0 disables climbing
            // entirely, which is NOT the same as the 0.50 tested in 3.85: that
            // still climbed on steep charges, i.e. still flew, which is what
            // makes the next charge shallow.
            Console.WriteLine();
            Console.WriteLine("== never-climb vs climb (weak), contacts by opening ==");
            foreach (var above in new[] { 0.50f, 0.88f, 1.01f, 1.50f, 9.00f })
            {
                var cells = new System.Text.StringBuilder();
                var total = 0;
                var clean = 0;
                var steep = 0;
                var shallow = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, above, WeakWings().DashAim,
                        0, WeakWings().ClimbCap, WeakWings().HoverDescend, 0,
                        "none", false, 0f), 8000, maxHits: 999,
                        bossOnly: true, bubbles: true, startX: startX,
                        jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    if (n == 0) clean++;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                    // How steep are the charges this setting actually produces?
                    foreach (var e in run.ChargeLog)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(e,
                            @"angle\s+([0-9.]+)deg");
                        if (!m.Success) continue;
                        var ang = float.Parse(m.Groups[1].Value,
                            CultureInfo.InvariantCulture);
                        if (ang >= 60f) steep++; else shallow++;
                    }
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    climbAbove={0,4:F2} |{1} | sum={2,5} clean={3}/8 " +
                    "steep(>=60deg)={4,4} shallow={5,4}", above, cells, total,
                    clean, steep, shallow));
            }

            // ARRIVAL-TIMED I-FRAMES. 3.91 established that a horizontal charge
            // cannot be out-escaped -- the perpendicular is vertical while the
            // dash is horizontal, and the climb needs ~23 ticks against ~18. So
            // surviving phase one requires EATING the contact with the dash's 15
            // immune ticks rather than clearing it. _dashAtContact already fires
            // the dash a chosen number of ticks before predicted arrival, which
            // is the right shape; the open question is the value, and whether it
            // should be aimed INTO the boss (the guide's stated mechanism) rather
            // than along the escape side. Swept together, since the two interact.
            Console.WriteLine();
            Console.WriteLine("== arrival-timed dash, escape-aimed vs counter (weak) ==");
            foreach (var counter in new[] { false, true })
            {
                foreach (var at in new[] { 1, 3, 5, 8, 12, 16, 20 })
                {
                    var cells = new System.Text.StringBuilder();
                    var total = 0;
                    var clean = 0;
                    foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f,
                        4300f, 4800f, 5300f, 5800f })
                    {
                        var run = RunFight(new CorridorEscape(true,
                            WeakWings().Lead, WeakWings().DashAt, true, 0f,
                            WeakWings().ClimbAbove, WeakWings().DashAim, 0,
                            WeakWings().ClimbCap, WeakWings().HoverDescend, 0,
                            "none", counter, 0f, at), 8000, maxHits: 999,
                            bossOnly: true, bubbles: true, startX: startX,
                            jumpSpeed: WeakWings().JumpSpeed,
                            wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                        var n = 0;
                        foreach (var l in run.HitLog)
                            if (l.Contains("src boss")) n++;
                        total += n;
                        if (n == 0) clean++;
                        cells.Append(string.Format(CultureInfo.InvariantCulture,
                            "{0,5}", n));
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    counter={0,-5} dashAtContact={1,2} |{2} | sum={3,5} " +
                        "clean={4}/8", counter, at, cells, total, clean));
                }
            }

            // CLIMB-RATE SENSITIVITY. 4.3 turned up a live discrepancy: native
            // WingMovement cancels part of gravity and floors the ascent at
            // -jumpSpeed * 3, while the lab's DemonThrust has no cancellation and
            // floors at -jumpSpeed * 1.5. The lab's measured ascent is 4.6 px/tick
            // against a bare jumpSpeed of 5.01, and the LoadoutProfile carries
            // ClimbRate 8.5 for this set -- a value recorded but never used by the
            // motion code.
            //
            // Instead of editing the thrust model on a guess, measure how much the
            // outcome depends on climb rate at all. RunFight drives ascent through
            // jumpSpeed, so sweeping it tells us both whether ascent is the
            // bottleneck and how much of the gap closing the discrepancy would
            // buy. The weak set's own profile already uses 8.91 here, so 5.01 ->
            // 8.91 is exactly the shot in question.
            Console.WriteLine();
            Console.WriteLine("== climb-rate sensitivity (weak), contacts by opening ==");
            foreach (var js in new[] { 5.01f, 6.00f, 7.00f, 8.00f, 8.91f, 10.00f })
            {
                var cells = new System.Text.StringBuilder();
                var total = 0;
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        WeakWings().DashAim, 0, WeakWings().ClimbCap,
                        WeakWings().HoverDescend, 0, "none", false, 0f), 8000,
                        maxHits: 999, bossOnly: true, bubbles: true,
                        startX: startX, jumpSpeed: js,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    if (n == 0) clean++;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    jumpSpeed={0,5:F2} |{1} | sum={2,5} clean={3}/8", js,
                    cells, total, clean));
            }

            // DASH-AIM BOUNDARY. The close trace shows why phase one stalls: perp
            // grows at only ~3.3 px/tick because the controller presses only L
            // and never U. For a ~50 deg charge the normal is MOSTLY HORIZONTAL,
            // so the dash's 172 px is the right instrument and the climb is the
            // wrong one -- yet _dashAim = 0.85 withholds the dash-aiming branch
            // from any charge shallower than |ux| = 0.85 (about 32 deg). Charges
            // at 33-60 deg therefore get neither the dash aimed along the normal
            // nor any vertical input, which is precisely the 34-44 deg family the
            // lab's own notes say it kept dying to. Sweep the boundary upward.
            Console.WriteLine();
            Console.WriteLine("== dash-aim boundary (weak), contacts by opening ==");
            foreach (var aim in new[] { 0.60f, 0.70f, 0.80f, 0.85f, 0.90f, 0.95f,
                1.01f })
            {
                var cells = new System.Text.StringBuilder();
                var total = 0;
                var clean = 0;
                foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f, 4300f,
                    4800f, 5300f, 5800f })
                {
                    var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                        aim, 0, WeakWings().ClimbCap, WeakWings().HoverDescend,
                        0, "none", false, 0f), 8000, maxHits: 999,
                        bossOnly: true, bubbles: true, startX: startX,
                        jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    total += n;
                    if (n == 0) clean++;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    dashAim={0,5:F2} |{1} | sum={2,5} clean={3}/8", aim,
                    cells, total, clean));
            }

            // ANGLE BANDS. 4.5 read the stall as a partition problem -- the two
            // sources answer different angles, and one scalar boundary leaves a
            // dead band of charges that get neither. Band mode makes the two
            // parameters the EDGES of a partition rather than competing cutoffs:
            // climb owns (climbAbove, dashAim] and the dash owns (dashAim, 1].
            // Sweep the pair, since the point is the partition, not either edge.
            Console.WriteLine();
            Console.WriteLine("== angle bands (weak): climb (lo,hi], dash (hi,1] ==");
            foreach (var lo in new[] { 0.40f, 0.55f, 0.70f })
            {
                foreach (var hi in new[] { 0.75f, 0.85f, 0.90f, 1.01f })
                {
                    var cells = new System.Text.StringBuilder();
                    var total = 0;
                    var clean = 0;
                    foreach (var startX in new[] { 2400f, 2800f, 3300f, 3800f,
                        4300f, 4800f, 5300f, 5800f })
                    {
                        var run = RunFight(new CorridorEscape(true,
                            WeakWings().Lead, WeakWings().DashAt, true, 0f, lo,
                            hi, 0, WeakWings().ClimbCap,
                            WeakWings().HoverDescend, 0, "none", false, 0f, 0,
                            false, 0f, false, 0f, false, true), 8000,
                            maxHits: 999, bossOnly: true, bubbles: true,
                            startX: startX, jumpSpeed: WeakWings().JumpSpeed,
                            wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                        var n = 0;
                        foreach (var l in run.HitLog)
                            if (l.Contains("src boss")) n++;
                        total += n;
                        if (n == 0) clean++;
                        cells.Append(string.Format(CultureInfo.InvariantCulture,
                            "{0,5}", n));
                    }
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    climb=({0:F2},{1:F2}] |{2} | sum={3,5} clean={4}/8",
                        lo, hi, cells, total, clean));
                }
            }

            // ESCAPE-BODY EXECUTION COUNT. The 200-contact floor is the single
            // most common outcome for ineffective configurations, and it is
            // constant across openings, which is not how a geometric failure
            // behaves. Count how many ticks the escape body actually ran, for a
            // configuration that hits the floor and for one that does not. If the
            // floor cases show the body never running, then "this mechanism does
            // not work" was the wrong reading all along and 3.91/3.94 need
            // reinterpreting rather than being treated as evidence.
            Console.WriteLine();
            Console.WriteLine("== escape-body execution count vs the 200 floor ==");
            foreach (var probe in new[]
            {
                new { Name = "baseline (dashAim .85, no band)", Lo = 0.88f,
                    Hi = 0.85f, Band = false },
                new { Name = "no-climb (3.91 floor case)", Lo = 1.01f,
                    Hi = 0.85f, Band = false },
                new { Name = "counter-dash (3.94 floor case)", Lo = 0.88f,
                    Hi = 0.85f, Band = false },
                new { Name = "band (0.55,0.85] (4.7 floor case)", Lo = 0.55f,
                    Hi = 0.85f, Band = true },
                new { Name = "band (0.55,1.01] (best band)", Lo = 0.55f,
                    Hi = 1.01f, Band = true }
            })
            {
                var cells = new System.Text.StringBuilder();
                var totals = 0;
                var bodyTicks = 0;
                foreach (var startX in new[] { 2400f, 3300f, 4800f, 5800f })
                {
                    var ctrl = new CorridorEscape(true, WeakWings().Lead,
                        WeakWings().DashAt, true, 0f, probe.Lo, probe.Hi, 0,
                        WeakWings().ClimbCap, WeakWings().HoverDescend, 0,
                        "none", probe.Name.Contains("counter"), 0f, 0, false,
                        0f, false, 0f, false, probe.Band);
                    var run = RunFight(ctrl, 8000, maxHits: 999, bossOnly: true,
                        bubbles: true, startX: startX,
                        jumpSpeed: WeakWings().JumpSpeed,
                        wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                    var n = 0;
                    foreach (var l in run.HitLog)
                        if (l.Contains("src boss")) n++;
                    totals += n;
                    bodyTicks += ctrl.EscapeBodyTicks;
                    cells.Append(string.Format(CultureInfo.InvariantCulture,
                        "{0,5}", n));
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    {0,-38} |{1} | sum={2,4} bodyTicks={3,6}",
                    probe.Name, cells, totals, bodyTicks));
            }

            // All threats, weak set, every opening: what still lands and from
            // where. Bubbles and sharkrons should be the only sources.
            Console.WriteLine();
            Console.WriteLine("== all threats, weak set, per opening ==");
            foreach (var startX in starts)
            {
                var run = RunFight(new CorridorEscape(true, WeakWings().Lead,
                    WeakWings().DashAt, true, 0f, WeakWings().ClimbAbove,
                    WeakWings().DashAim, 0, WeakWings().ClimbCap,
                    WeakWings().HoverDescend, 0, "none", false), 8000,
                    maxHits: 999, bossOnly: false, bubbles: true,
                    tornados: true, startX: startX,
                    jumpSpeed: WeakWings().JumpSpeed,
                    wingTimeMax: WeakWings().FlyTicks, autoJump: true);
                var bossN = 0;
                var bubbleN = 0;
                var otherN = 0;
                foreach (var line in run.HitLog)
                {
                    if (line.Contains("src boss")) bossN++;
                    else if (line.Contains("src bubble")) bubbleN++;
                    else otherN++;
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    startX={0,5:F0} totalHits={1,4} boss={2,3} bubble={3,3} " +
                    "other={4,3} charges={5,3}", startX, run.Hits, bossN,
                    bubbleN, otherN, run.Charges));
            }
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
