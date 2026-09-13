using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    // Native aiStyle 32: hover 0, spin 1, daytime enrage 2, leaving 3.
    // A bounded, terrain-referenced runway; never chase head.Y minus an offset.
    internal sealed class PrimeStrategy : BossStrategyBase
    {
        private int _headKey = -1, _direction, _groundRetry, _liftTicks;
        private float _lastAi1 = -1f, _liftFloorY;
        private bool _lifting, _landing, _returning;

        public PrimeStrategy() : base("skeletron-prime", 1150, 500, 5.5f, true)
        {
            Requirements.RequiresNormalGravity = true;
        }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) =>
            HasType(b, 127, 131) && MechanicalFamilies.Count(b) == 1;

        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = Pick(s, 127);
            var p = s.Player;
            if (_headKey != head.Key || m.PreviousTargetKey < 0)
            {
                _headKey = head.Key;
                _direction = AwayX(p, head);
                _lastAi1 = -1f;
                _lifting = _landing = _returning = false;
                _groundRetry = _liftTicks = 0;
            }
            if (_groundRetry > 0) _groundRetry--;
            var known = head.Type == 127 && (head.Ai1 == 0f || head.Ai1 == 1f || head.Ai1 == 2f || head.Ai1 == 3f);
            var spin = head.Ai1 == 1f;
            var enraged = head.Ai1 == 2f;
            var leaving = head.Ai1 == 3f;
            var phase = !known ? "unrecognized-native-state" : enraged ? "day-enrage-escape" : leaving ?
                "native-departure" : spin ? "spin-runway" : head.Ai2 >= 540f ? "hover-prepare-spin" : "hover-low-runway";
            if ((spin || enraged) && _lastAi1 != head.Ai1) _direction = AwayX(p, head);

            var footing = CurrentFooting(s);
            var landingSupport = LandingSupport(s);
            var gap = Math.Abs(p.Center.X - head.Center.X);
            var speed = Math.Abs(p.Velocity.X);
            // Explicit zero means brake, not a request for a generic orbit.
            // Include stopping distance so distance management starts BEFORE
            // losing weapon range/native NPC active range.
            var brake = speed * speed / (2f * Math.Max(.000001f, p.RunSlowdown));
            var ideal = spin || enraged ? 560f : 420f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(260f, s.Weapon.Profile.ConservativeRangePixels * .60f));
            var horizontal = _direction;
            var reserve = Math.Max(220f, brake + speed * 32f);
            var returnWindow = known && !spin && !enraged && !leaving && head.Ai1 == 0f && head.Ai2 < 540f;
            if (!returnWindow && _returning)
            {
                _returning = false;
                horizontal = 0;
                _direction = AwayX(p, head);
            }
            if (returnWindow)
            {
                var away = AwayX(p, head);
                var toward = -away;
                if (_returning)
                {
                    // A return is a guarded closed loop, not a latched direction.
                    // Stop before braking could carry the player through the head,
                    // and revalidate both terrain and threats every native tick.
                    if (_direction != toward || gap - StoppingTravel(p, _direction) <= ideal ||
                        !SafeReturn(s, head, footing, _direction))
                    {
                        _returning = false;
                        horizontal = 0;
                        _direction = away;
                    }
                    else horizontal = _direction;
                }
                else
                {
                    var lane = Lane(s, footing, _direction);
                    var reverse = -_direction;
                    if (lane < reserve && Lane(s, footing, reverse) > lane + 120f &&
                        SafeReturn(s, head, footing, reverse))
                    {
                        horizontal = _direction = reverse;
                        // Most edge turns establish a new away lane. If this one
                        // actually heads back toward Prime, guard it as a return.
                        _returning = reverse == toward;
                    }
                    else if (gap > ideal + 260f && SafeReturn(s, head, footing, toward))
                    {
                        horizontal = _direction = toward;
                        _returning = true;
                    }
                }
            }
            if (horizontal == AwayX(p, head) && gap + brake > ideal && !enraged)
                horizontal = 0;
            if (Lane(s, footing, horizontal) < brake + 24f)
            {
                if (_returning)
                {
                    _returning = false;
                    _direction = AwayX(p, head);
                }
                horizontal = 0;
            }

            var vertical = 0;
            var foot = p.Position.Y + p.Height;
            if (p.OnGround && _landing)
            {
                _landing = false;
                _groundRetry = 180;
            }
            // On a solid floor take ONE bounded excursion to search upward for
            // a platform. Record the launch row once; newly observed higher rows
            // must not move the altitude goal upwards each tick.
            if (!_lifting && !_landing && !_returning && _groundRetry == 0 && p.OnGround && footing.Valid && !footing.OneWay &&
                !s.Mobility.GravityInverted && s.Mobility.HasFiniteFlightResource &&
                s.Mobility.FlightResourceFraction >= .85f && known && !leaving && !enraged)
            {
                _lifting = true;
                _liftFloorY = foot;
                _liftTicks = 0;
            }
            if (_lifting)
            {
                if (++_liftTicks > 180 || s.Mobility.GravityInverted || !known || leaving ||
                    foot <= _liftFloorY - 720f || s.Mobility.FlightResourceFraction <= .28f || s.Arena.ClearanceUp < 100f)
                {
                    _lifting = false;
                    _landing = true;
                }
                else
                {
                    vertical = 1;
                    phase += "-bounded-lift";
                }
            }
            if (_landing || !p.OnGround && !_lifting)
            {
                // A current or cached recovery row may guide an airborne landing;
                // unlike CurrentFooting, it is allowed to be below the player.
                // Do not press Down: catch one-way platforms and release Jump.
                if (landingSupport.Valid)
                {
                    var x = p.Position.X + p.Velocity.X * 12f;
                    if (x < landingSupport.Left + reserve) horizontal = 1;
                    else if (x + p.Width > landingSupport.Right - reserve) horizontal = -1;
                }
                phase += "-landing-release";
            }
            if (!known) horizontal = vertical = 0;
            var target = SelectFireTarget(s, head, m.PreviousTargetKey);
            var visible = target.LineOfSightKnown ? target.HasLineOfSight : target.Key == head.Key && s.LineOfSightToPrimary;
            var decision = Decision(s, target, phase, BossPattern.Runway, ideal, 0f,
                horizontal, vertical, spin && gap < 300f || enraged,
                !p.OnGround && s.Mobility.FlightResourceFraction < .25f, false, spin || enraged ? 80f : 52f,
                known && target.Life > 0 && target.Chaseable && visible, head);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.JumpAction = vertical > 0 ? JumpAction.Default : JumpAction.Release;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            _lastAi1 = head.Ai1;
            return decision;
        }

        private static SupportSpan CurrentFooting(CombatSnapshot s)
        {
            if (s.Mobility.GravityInverted) return default(SupportSpan);
            var foot = s.Player.Position.Y + s.Player.Height;
            var f = s.Arena.FloorSupport;
            if (AtFeet(f, s.Player, foot)) return f;
            var r = s.Arena.RecoverySupport;
            return AtFeet(r, s.Player, foot) ? r : default(SupportSpan);
        }

        private static SupportSpan LandingSupport(CombatSnapshot s)
        {
            if (s.Mobility.GravityInverted) return default(SupportSpan);
            var foot = s.Player.Position.Y + s.Player.Height;
            var r = s.Arena.RecoverySupport;
            if (BelowOrAtFeet(r, foot)) return r;
            var f = s.Arena.FloorSupport;
            return BelowOrAtFeet(f, foot) ? f : default(SupportSpan);
        }

        private static bool AtFeet(SupportSpan support, PlayerSnapshot player, float foot) =>
            support.Valid && !support.Inverted && support.ContainsBody(player.Position.X, player.Width) &&
            Math.Abs(support.SurfaceY - foot) <= 8f;

        private static bool BelowOrAtFeet(SupportSpan support, float foot) =>
            support.Valid && !support.Inverted && support.Right > support.Left && support.SurfaceY >= foot - 8f;

        private static float StoppingTravel(PlayerSnapshot player, int direction)
        {
            if (direction == 0) return 0f;
            var towardSpeed = player.Velocity.X * direction;
            if (float.IsNaN(towardSpeed)) return float.PositiveInfinity;
            if (towardSpeed <= 0f) return 0f;
            var drag = player.RunSlowdown;
            if (!(drag > 0f) || float.IsNaN(drag)) return float.PositiveInfinity;
            return towardSpeed * towardSpeed / (2f * drag);
        }

        private static float Lane(CombatSnapshot s, SupportSpan floor, int direction)
        {
            if (direction == 0) return float.MaxValue;
            var available = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            if (floor.ContainsBody(s.Player.Position.X, s.Player.Width))
                available = Math.Min(available, direction > 0 ? floor.Right - s.Player.Position.X - s.Player.Width :
                    s.Player.Position.X - floor.Left);
            return Math.Max(0f, available);
        }

        private static bool SafeReturn(CombatSnapshot s, TargetSnapshot head, SupportSpan floor, int direction)
        {
            if (!s.Player.OnGround || !floor.ContainsBody(s.Player.Position.X, s.Player.Width) ||
                Math.Abs(floor.SurfaceY - s.Player.Position.Y - s.Player.Height) > 8f ||
                head.Ai1 != 0f || head.Ai2 >= 540f || s.Mobility.GravityInverted) return false;
            var x = s.Player.Position.X;
            var vx = s.Player.Velocity.X;
            for (var tick = 1; tick <= 24; tick++)
            {
                float dx;
                vx = HorizontalMotion.Advance(s.Player, vx, direction, true, 1, out dx);
                x += dx;
                if (!floor.ContainsBody(x, s.Player.Width)) return false;
                var body = s.Player.BoundsAt(new Vec2(x, s.Player.Position.Y)).Inflated(24f);
                for (var i = 0; i < s.Threats.Count; i++)
                {
                    var t = s.Threats[i];
                    if (t.Damage <= 0) continue;
                    if (t.Geometry != ThreatGeometry.Body)
                    {
                        if (BeamGeometry.Intersects(body, BeamGeometry.Sweep(t, 0, tick), 24f)) return false;
                    }
                    else
                    {
                        var margin = t.Kind == ThreatKind.NpcContact ? 40f : 16f;
                        if (t.Trajectory != ThreatTrajectory.Linear)
                        {
                            ProjectileMotionSample sample;
                            if (!HostileProjectileMotion.TrySample(t, tick, out sample)) return false;
                            if (sample.Active && body.Intersects(sample.Bounds.Inflated(margin))) return false;
                        }
                        else if (body.Intersects(t.BoundsAt(tick).Inflated(margin))) return false;
                    }
                }
                // Even synthetic adapters lacking a contact threat for the head
                // must not authorize turning through its observed flight path.
                var future = head.Position + head.Velocity * tick;
                if (body.Intersects(new RectF(future.X, future.Y, head.Width, head.Height).Inflated(80f))) return false;
            }
            return true;
        }

        private static TargetSnapshot SelectFireTarget(CombatSnapshot s, TargetSnapshot head, int previous)
        {
            var selected = head;
            var visibilityBest = int.MaxValue;
            var rankBest = int.MaxValue;
            var distanceBest = float.MaxValue;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var t = s.Targets[i];
                if (t.Life <= 0 || t.Invulnerable || !t.Chaseable || t.LineOfSightKnown && !t.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(t.Center, s.Player.Center);
                if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported &&
                    distance > s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels) continue;
                // Prefer an already verified shot over an unknown ray even when
                // the latter is a Laser. Unknown remains a one-frame probe only
                // when no known-visible supported target exists.
                var visibility = t.LineOfSightKnown ? 0 : 1;
                // Guides identify Laser as persistent pressure; it has lower
                // HP/defense than Saw/Vice. Do not require all four arms to die.
                var rank = t.Type == 131 ? 0 : t.Type == 127 ? 1 : t.Type >= 128 && t.Type <= 130 ? 2 :
                    distance < 200f * 200f && t.Damage > 0 ? 1 : 10;
                if (rank == 10) continue;
                if (t.Key == previous) distance -= 80f * 80f;
                if (visibility < visibilityBest || visibility == visibilityBest &&
                    (rank < rankBest || rank == rankBest && distance < distanceBest))
                {
                    visibilityBest = visibility;
                    rankBest = rank;
                    distanceBest = distance;
                    selected = t;
                }
            }
            return selected;
        }
    }
}
