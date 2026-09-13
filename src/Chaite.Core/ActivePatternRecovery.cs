using System;

namespace Chaite.Core
{
    /// <summary>
    /// A zero-allocation measurement of how far an observed or simulated player
    /// state is from the durable movement loop selected by a Boss strategy.
    /// This is deliberately kinematic: equipment identity is handled by the
    /// mobility-route admission contract, while recovery only asks whether that
    /// admitted route is actually converging.
    /// </summary>
    internal struct ActivePatternRecoveryMeasure
    {
        public bool Valid;
        public bool Closed;
        public float Distance;
    }

    /// <summary>
    /// Stable identity of the movement manifold used while joining an encounter
    /// which was already in progress.  PhaseId is intentionally excluded: a
    /// number of native Boss phase labels contain a live AI timer for diagnostics,
    /// so comparing that display string would reset recovery on every game tick.
    /// </summary>
    internal struct ActivePatternRecoveryRoute
    {
        public string StrategyId;
        public BossPattern Pattern;
        public int PatternTargetKey;
        public int DesiredHorizontal;
        public int DesiredVertical;
        public float IdealDistance;
        public float VerticalOffset;
        public float FloorClearance;
        public bool UseExplicitMovement;
        public bool OwnsMovementClosure;
        public bool OwnsHorizontalClosure;
        public float RecoveryMinimumHorizontalSpeed;
        public bool RecoveryIgnoreHorizontalGeometry;
        public float RecoveryMinimumForwardSeparation;

        public static ActivePatternRecoveryRoute Create(
            in BossDirective directive, int patternTargetKey,
            int desiredHorizontal, int desiredVertical)
        {
            return new ActivePatternRecoveryRoute
            {
                StrategyId = directive.StrategyId,
                Pattern = directive.Pattern,
                PatternTargetKey = patternTargetKey,
                DesiredHorizontal = Math.Sign(desiredHorizontal),
                DesiredVertical = Math.Sign(desiredVertical),
                IdealDistance = directive.IdealDistance,
                VerticalOffset = directive.VerticalOffset,
                FloorClearance = directive.FloorClearance,
                UseExplicitMovement = directive.UseExplicitMovement,
                OwnsMovementClosure = directive.OwnsMovementClosure,
                OwnsHorizontalClosure = directive.OwnsHorizontalClosure,
                RecoveryMinimumHorizontalSpeed =
                    directive.RecoveryMinimumHorizontalSpeed,
                RecoveryIgnoreHorizontalGeometry =
                    directive.RecoveryIgnoreHorizontalGeometry,
                RecoveryMinimumForwardSeparation =
                    directive.RecoveryMinimumForwardSeparation
            };
        }

        public bool SameAs(in ActivePatternRecoveryRoute other)
        {
            return string.Equals(StrategyId, other.StrategyId,
                       StringComparison.Ordinal) &&
                   Pattern == other.Pattern &&
                   PatternTargetKey == other.PatternTargetKey &&
                   DesiredHorizontal == other.DesiredHorizontal &&
                   DesiredVertical == other.DesiredVertical &&
                   SameFinite(IdealDistance, other.IdealDistance) &&
                   SameFinite(VerticalOffset, other.VerticalOffset) &&
                   SameFinite(FloorClearance, other.FloorClearance) &&
                   UseExplicitMovement == other.UseExplicitMovement &&
                   OwnsMovementClosure == other.OwnsMovementClosure &&
                   OwnsHorizontalClosure == other.OwnsHorizontalClosure &&
                   SameFinite(RecoveryMinimumHorizontalSpeed,
                       other.RecoveryMinimumHorizontalSpeed) &&
                   RecoveryIgnoreHorizontalGeometry ==
                       other.RecoveryIgnoreHorizontalGeometry &&
                   SameFinite(RecoveryMinimumForwardSeparation,
                       other.RecoveryMinimumForwardSeparation);
        }

        private static bool SameFinite(float left, float right) =>
            !float.IsNaN(left) && !float.IsInfinity(left) &&
            !float.IsNaN(right) && !float.IsInfinity(right) &&
            Math.Abs(left - right) <= .001f;
    }

    internal static class ActivePatternRecovery
    {
        // Active-F8 recovery may legitimately cross several native attack
        // routes, but it may never own player input indefinitely. Route-local
        // deadlines can rebase; this independent ten-second ceiling cannot.
        public const int AbsoluteRecoveryDeadlineTicks = 600;

        public static ActivePatternRecoveryMeasure Measure(
            CombatSnapshot snapshot, TargetSnapshot target,
            BossDirective directive, Vec2 position, Vec2 velocity,
            int desiredHorizontal, int desiredVertical,
            float targetLeadTicks)
        {
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Mobility == null || snapshot.Arena == null ||
                !Finite(position.X) || !Finite(position.Y) ||
                !Finite(velocity.X) || !Finite(velocity.Y) ||
                !Finite(targetLeadTicks))
                return Invalid();

            var player = snapshot.Player;
            var center = new Vec2(position.X + player.Width * .5f,
                position.Y + player.Height * .5f);
            var targetCenter = target.Center + target.Velocity * targetLeadTicks;
            var delta = center - targetCenter;
            var ideal = Math.Max(80f, directive.IdealDistance);
            var geometryError = 0f;
            var verticalError = 0f;
            var geometryTolerance = Math.Max(64f, ideal * .18f);
            var verticalTolerance = Math.Max(72f, Math.Min(180f,
                ideal * .25f));

            // Source-specific controllers own their anchor and phase closure.
            // Re-imposing a generic target-relative ring would corrupt routes
            // such as Destroyer's runway acquire/return. For those controllers,
            // observed input/velocity alignment and braking room are the common
            // stable manifold; their own directive supplies the actual route.
            if (!directive.UseExplicitMovement)
            {
                var relativeY = delta.Y - directive.VerticalOffset;
                if (directive.RecoveryIgnoreHorizontalGeometry)
                {
                    // A translating runway (notably Wall of Flesh) has no
                    // single correct target-relative X coordinate. It still
                    // has a non-negotiable signed lead: the player must remain
                    // in front of the moving source by the source-reviewed
                    // contact/braking margin. Matching horizontal speed after
                    // the source passed the player is not a safe recovery.
                    var forwardDirection = Math.Sign(desiredHorizontal);
                    var minimumForward =
                        directive.RecoveryMinimumForwardSeparation;
                    if (forwardDirection == 0 || !Finite(minimumForward) ||
                        minimumForward < 0f)
                        return Invalid();
                    var forwardSeparation = forwardDirection *
                        (center.X - target.Center.X);
                    geometryError = Math.Max(0f, minimumForward -
                        forwardSeparation);
                    verticalError = Math.Abs(relativeY);
                    geometryTolerance = 0f;
                }
                else
                {
                switch (directive.Pattern)
                {
                case BossPattern.CircleOrbit:
                case BossPattern.EllipseOrbit:
                    var verticalScale = directive.Pattern ==
                        BossPattern.EllipseOrbit ? 1.45f : 1f;
                    var radius = (float)Math.Sqrt(delta.X * delta.X +
                        relativeY * relativeY * verticalScale * verticalScale);
                    geometryError = Math.Abs(radius - ideal);
                    geometryTolerance = Math.Max(80f, ideal * .22f);
                    // An orbit intentionally traverses both sides of its
                    // vertical offset. Radius, not a fixed Y line, is its loop.
                    verticalTolerance = float.MaxValue;
                    break;
                case BossPattern.StayCloseJump:
                    geometryError = Math.Abs(Math.Abs(delta.X) - ideal);
                    verticalError = Math.Abs(relativeY);
                    geometryTolerance = Math.Max(56f, ideal * .28f);
                    verticalTolerance = Math.Max(80f, ideal * .55f);
                    break;
                case BossPattern.PerpendicularDashDodge:
                    // A charge/deathray response is a transient escape lane,
                    // not a target-relative orbit. Increasing separation can be
                    // exactly correct, so closure is the commanded velocity and
                    // verified stopping room rather than an artificial radius.
                    geometryError = 0f;
                    verticalError = 0f;
                    geometryTolerance = verticalTolerance = 0f;
                    break;
                default:
                    geometryError = Math.Abs(Math.Abs(delta.X) - ideal);
                    verticalError = Math.Abs(relativeY);
                    break;
                }
                }
            }

            var runSpeed = Math.Max(2f, player.MaxRunSpeed);
            var horizontalAlignment = HorizontalAlignmentError(velocity.X,
                desiredHorizontal, runSpeed,
                directive.RecoveryMinimumHorizontalSpeed);
            var gravitySign = snapshot.Mobility.GravityInverted ? -1f : 1f;
            var verticalAlignment = VerticalAlignmentError(velocity.Y,
                desiredVertical, gravitySign);
            if (directive.UseExplicitMovement &&
                directive.OwnsMovementClosure && desiredVertical > 0 &&
                player.OnGround && (directive.JumpAction == JumpAction.Hold ||
                                     directive.JumpAction == JumpAction.Cloud))
            {
                // A native phase which owns the complete ground launch is
                // already on its executable route before vertical velocity is
                // applied. Requiring an airborne end-of-horizon velocity would
                // reject ordinary jumps after they arc back toward the floor.
                verticalAlignment = 0f;
            }
            if (directive.RecoveryIgnoreHorizontalGeometry &&
                directive.VerticalIntent == 0 &&
                verticalError <= verticalTolerance)
            {
                // A runway controller may softly recenter inside its certified
                // vertical lane. That optional correction is not a prerequisite
                // for joining the loop; explicit attack/dodge vertical intents
                // still have to establish their commanded velocity.
                verticalAlignment = 0f;
            }
            var edgeError = EdgeError(snapshot, position, velocity);

            if (!Finite(geometryError) || !Finite(verticalError) ||
                !Finite(horizontalAlignment) || !Finite(verticalAlignment) ||
                !Finite(edgeError)) return Invalid();

            var distance = geometryError + verticalError * .65f +
                horizontalAlignment + verticalAlignment + edgeError * 2f;
            var closed = geometryError <= geometryTolerance &&
                verticalError <= verticalTolerance &&
                horizontalAlignment <= 1f && verticalAlignment <= 1f &&
                edgeError <= .5f;
            return new ActivePatternRecoveryMeasure
            {
                Valid = true,
                Closed = closed,
                Distance = distance
            };
        }

        public static bool MakesProjectedProgress(
            in ActivePatternRecoveryMeasure current,
            in ActivePatternRecoveryMeasure projected)
        {
            if (!current.Valid || !projected.Valid) return false;
            if (projected.Closed) return true;
            var required = Math.Max(1f, current.Distance * .01f);
            return projected.Distance <= current.Distance - required;
        }

        public static bool MakesObservedProgress(float previousDistance,
            in ActivePatternRecoveryMeasure current)
        {
            if (!current.Valid || !Finite(previousDistance) ||
                previousDistance == float.MaxValue) return current.Valid;
            var required = Math.Max(.25f, previousDistance * .001f);
            return current.Distance <= previousDistance - required;
        }

        public static int RecoveryDeadline(float distance, float speed,
            int configuredRecoveryTicks)
        {
            if (!Finite(distance) || distance < 0f || !Finite(speed) ||
                speed <= 0f) return 0;
            // Allow acceleration, braking, one overshoot correction, and a
            // short closed-loop confirmation. The upper bound prevents a bad
            // phase/terrain observation from owning input indefinitely.
            var travelTicks = (int)Math.Ceiling(distance /
                Math.Max(2f, speed));
            var minimum = Math.Max(90, configuredRecoveryTicks * 4);
            return Math.Max(minimum, Math.Min(600,
                travelTicks * 3 + 90));
        }

        private static float HorizontalAlignmentError(float velocity,
            int desired, float runSpeed, float requiredAlongSpeed)
        {
            if (desired == 0)
            {
                // A deliberate coast is closed only after accumulated momentum
                // has been braked to a speed from which the next route edge can
                // be taken without a long hidden transient.
                var coastLimit = Math.Max(.75f, runSpeed * .2f);
                return Math.Max(0f, Math.Abs(velocity) - coastLimit) * 10f;
            }
            var minimumAlong = Math.Max(.5f, Math.Min(2f, runSpeed * .25f));
            if (Finite(requiredAlongSpeed) && requiredAlongSpeed > 0f)
                minimumAlong = Math.Max(minimumAlong, requiredAlongSpeed);
            return Math.Max(0f, minimumAlong - velocity * Math.Sign(desired)) *
                10f;
        }

        private static float VerticalAlignmentError(float velocity,
            int desired, float gravitySign)
        {
            if (desired == 0) return 0f;
            // Positive posture is Up in player-gravity coordinates. World Y is
            // positive down, hence the leading minus sign.
            var worldDirection = -Math.Sign(desired) * gravitySign;
            return Math.Max(0f, .5f - velocity * worldDirection) * 8f;
        }

        private static float EdgeError(CombatSnapshot snapshot, Vec2 position,
            Vec2 velocity)
        {
            var player = snapshot.Player;
            var displacementX = position.X - player.Position.X;
            var displacementY = position.Y - player.Position.Y;
            var left = snapshot.Arena.ClearanceLeft + displacementX;
            var right = snapshot.Arena.ClearanceRight - displacementX;
            var up = snapshot.Arena.ClearanceUp + displacementY;
            var down = snapshot.Arena.ClearanceDown - displacementY;
            var braking = player.RunSlowdown > 0f ? player.RunSlowdown :
                Math.Max(.08f, player.RunAcceleration);
            var stop = velocity.X * velocity.X / (2f * braking);
            var horizontalMargin = Math.Max(48f, player.Width * 2f);
            var error = velocity.X < -.01f ?
                Math.Max(0f, stop + horizontalMargin - left) :
                velocity.X > .01f ?
                    Math.Max(0f, stop + horizontalMargin - right) :
                    Math.Max(0f, horizontalMargin - Math.Min(left, right));

            var verticalMargin = Math.Max(36f, player.Height);
            if (velocity.Y < -.01f)
                error += Math.Max(0f, verticalMargin - up);
            else if (velocity.Y > .01f && !snapshot.Arena.HasFloor)
                error += Math.Max(0f, verticalMargin - down);

            var bounds = snapshot.Arena.LocalOpenBounds;
            if (bounds.Width > 0f && bounds.Height > 0f)
            {
                error += Math.Max(0f, bounds.Left - position.X);
                error += Math.Max(0f, position.X + player.Width - bounds.Right);
                error += Math.Max(0f, bounds.Top - position.Y);
                error += Math.Max(0f, position.Y + player.Height - bounds.Bottom);
            }
            return error;
        }

        private static ActivePatternRecoveryMeasure Invalid() =>
            new ActivePatternRecoveryMeasure
            {
                Valid = false,
                Closed = false,
                Distance = float.MaxValue
            };

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
