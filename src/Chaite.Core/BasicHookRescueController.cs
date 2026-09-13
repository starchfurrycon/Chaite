using System;

namespace Chaite.Core
{
    /// <summary>
    /// Stateful hot-path overlay for the verified item-84/projectile-13 route.
    /// It only mutates a base ControlPlan after the complete cold proof has been
    /// accepted. With no exact hook/candidate it returns Ineligible and leaves
    /// every ControlPlan field byte-for-byte untouched.
    /// </summary>
    public sealed class BasicHookRescueController
    {
        private BasicHookVerifiedRoute _route;
        private Vec2 _aim;
        private string _strategyId;
        private string _phaseId;
        private long _lastSequence;
        private int _ticksSinceShot;
        private int _firstLatchTick = -1;
        private bool _active;
        private bool _detachPulseIssued;
        private bool _abortDetach;
        private bool _expectedPlayerKnown;
        private Vec2 _expectedPlayerCenter;
        private Vec2 _expectedPlayerVelocity;
        private BasicHookProjectileObservation _previousProjectile;
        private Vec2 _previousPlayerCenter;
        private bool _previousProjectileKnown;

        public bool Active => _active;

        public void Reset()
        {
            _route = default(BasicHookVerifiedRoute);
            _aim = default(Vec2);
            _strategyId = null;
            _phaseId = null;
            _lastSequence = 0;
            _ticksSinceShot = 0;
            _firstLatchTick = -1;
            _active = false;
            _detachPulseIssued = false;
            _abortDetach = false;
            _expectedPlayerKnown = false;
            _expectedPlayerCenter = default(Vec2);
            _expectedPlayerVelocity = default(Vec2);
            _previousProjectile = default(BasicHookProjectileObservation);
            _previousPlayerCenter = default(Vec2);
            _previousProjectileKnown = false;
        }

        public BasicHookRescueStep Apply(in BasicHookFrameSnapshot frame,
            string strategyId, string phaseId, bool allowStart, bool rescueRequested,
            ref ControlPlan plan)
        {
            if (!_active)
            {
                if (!allowStart || !rescueRequested || !frame.Known ||
                    !frame.CandidateKnown || !frame.CandidateRoute.Known ||
                    !Finite(frame.CandidateAimWorld) || !frame.ReleaseJump ||
                    frame.Sequence <= 0 ||
                    frame.ProjectileObserved || frame.Link.Known && frame.Link.GrappleCount != 0)
                    return BasicHookRescueStep.Ineligible;

                BasicHookFailure failure;
                if (!BasicHookMotion.CanBeginRescue(in frame.Identity, in frame.Context,
                        in frame.CandidateRoute, out failure))
                    return BasicHookRescueStep.Ineligible;
                var initial = Observation(in frame, false);
                var initialDecision = BasicHookMotion.Decide(in frame.Identity, in frame.Context,
                    in frame.CandidateRoute, in initial);
                if (initialDecision.Step != BasicHookRescueStep.ReadyToFire ||
                    initialDecision.Failure != BasicHookFailure.None)
                    return initialDecision.Step;

                _active = true;
                _route = frame.CandidateRoute;
                _aim = frame.CandidateAimWorld;
                _strategyId = strategyId;
                _phaseId = phaseId;
                _lastSequence = frame.Sequence;
                _ticksSinceShot = 0;
                _firstLatchTick = -1;
                _detachPulseIssued = false;
                _abortDetach = false;
                _previousProjectileKnown = false;
                Neutralize(ref plan);
                plan.Hook = true;
                plan.HookWorld = _aim;
                PredictNeutralPlayer(in frame);
                return BasicHookRescueStep.ReadyToFire;
            }

            if (!frame.Known || frame.Sequence != _lastSequence + 1 ||
                !SameStrategy(strategyId, phaseId) ||
                frame.LowConfigLoopEpoch != _route.LowConfigLoopEpoch ||
                !MatchesExpectedPlayer(in frame))
                return AbortOrDetach(in frame, ref plan);

            _lastSequence = frame.Sequence;
            _ticksSinceShot++;
            if (_abortDetach) return ContinueAbortDetach(in frame, ref plan);
            if (!ValidateProjectileTransition(in frame))
                return AbortOrDetach(in frame, ref plan);

            BasicHookFailure classificationFailure;
            var phase = frame.ProjectileObserved
                ? BasicHookMotion.ClassifyProjectile(in frame.Projectile,
                    frame.Context.LocalPlayerIndex, out classificationFailure)
                : BasicHookProjectilePhase.Unsupported;
            if (frame.ProjectileObserved && phase == BasicHookProjectilePhase.Latched &&
                _firstLatchTick < 0) _firstLatchTick = _ticksSinceShot;

            var observation = Observation(in frame, true);
            observation.FirstLatchTickKnown = _firstLatchTick >= 0;
            observation.FirstLatchTick = _firstLatchTick;
            observation.DetachObserved = _detachPulseIssued && !frame.ProjectileObserved &&
                frame.Link.Known && frame.Link.GrappleCount == 0;
            observation.OriginalLowConfigLoopStillValid = SameStrategy(strategyId, phaseId) &&
                frame.LowConfigLoopEpoch == _route.LowConfigLoopEpoch;
            var decision = BasicHookMotion.Decide(in frame.Identity, in frame.Context,
                in _route, in observation);

            switch (decision.Step)
            {
            case BasicHookRescueStep.AwaitProjectile:
            case BasicHookRescueStep.AwaitLatch:
            case BasicHookRescueStep.FollowCertifiedMissRecovery:
            case BasicHookRescueStep.FollowCertifiedReturn:
                Neutralize(ref plan);
                PredictNeutralPlayer(in frame);
                break;
            case BasicHookRescueStep.Pulling:
                Neutralize(ref plan);
                if (!PredictAttachedPlayer(in frame, false))
                    return AbortOrDetach(in frame, ref plan);
                break;
            case BasicHookRescueStep.ArmJumpRelease:
                Neutralize(ref plan);
                plan.JumpAction = JumpAction.Release;
                if (!PredictAttachedPlayer(in frame, false))
                    return AbortOrDetach(in frame, ref plan);
                break;
            case BasicHookRescueStep.PulseJumpToDetach:
                Neutralize(ref plan);
                plan.Jump = true;
                plan.JumpAction = JumpAction.Hold;
                if (!PredictAttachedPlayer(in frame, true))
                    return AbortOrDetach(in frame, ref plan);
                _detachPulseIssued = true;
                break;
            case BasicHookRescueStep.ReenteredLowConfigLoop:
                Reset();
                break;
            case BasicHookRescueStep.Abort:
            case BasicHookRescueStep.Ineligible:
                return AbortOrDetach(in frame, ref plan);
            default:
                return AbortOrDetach(in frame, ref plan);
            }

            RememberProjectile(in frame);
            return decision.Step;
        }

        private BasicHookRescueObservation Observation(in BasicHookFrameSnapshot frame,
            bool shotIssued)
        {
            return new BasicHookRescueObservation
            {
                Known = frame.Known,
                ShotIssued = shotIssued,
                TicksSinceShot = shotIssued ? _ticksSinceShot : 0,
                ProjectileObserved = frame.ProjectileObserved,
                Projectile = frame.Projectile,
                Link = frame.Link,
                Anchor = frame.Anchor,
                ReleaseJump = frame.ReleaseJump,
                OriginalLowConfigLoopStillValid = true,
                LiveThreatFieldKnown = frame.LiveThreatFieldKnown,
                CertifiedTrajectoryStillSafe = frame.CertifiedTrajectoryStillSafe,
                LiveWorstCaseRiskScore = frame.LiveWorstCaseRiskScore,
                LowConfigLoopEpoch = shotIssued ? frame.LowConfigLoopEpoch :
                    frame.CandidateRoute.LowConfigLoopEpoch,
                PlayerCenter = frame.PlayerCenter,
                PlayerVelocity = frame.PlayerVelocity
            };
        }

        private bool ValidateProjectileTransition(in BasicHookFrameSnapshot frame)
        {
            if (!frame.ProjectileObserved)
            {
                if (_detachPulseIssued) return true;
                if (!_previousProjectileKnown)
                    return _ticksSinceShot <= BasicHookMotion.ProjectileObservationGraceTicks;
                BasicHookFailure previousFailure;
                var previousPhase = BasicHookMotion.ClassifyProjectile(in _previousProjectile,
                    frame.Context.LocalPlayerIndex, out previousFailure);
                if (previousPhase != BasicHookProjectilePhase.ReturningAfterMiss) return false;
                double distance;
                return TryDistance(_previousProjectile.Center, _previousPlayerCenter,
                    out distance) && distance < BasicHookMotion.ReturnKillDistance;
            }

            BasicHookFailure failure;
            var phase = BasicHookMotion.ClassifyProjectile(in frame.Projectile,
                frame.Context.LocalPlayerIndex, out failure);
            if (phase == BasicHookProjectilePhase.Unsupported) return false;
            if (phase == BasicHookProjectilePhase.Latched)
            {
                if (!NearlySame(frame.Projectile.Center, _route.IntendedAnchorCenter, .1f) ||
                    _ticksSinceShot < _route.ExpectedLatchTick ||
                    _ticksSinceShot > _route.ExpectedDetachTick ||
                    _firstLatchTick >= 0 && _firstLatchTick != _route.ExpectedLatchTick ||
                    _firstLatchTick < 0 && _ticksSinceShot != _route.ExpectedLatchTick ||
                    !frame.Link.Known || !frame.Link.AtGrappleMovementEntry ||
                    frame.Link.GrappleCount != BasicHookMotion.MaximumSimultaneousHooks ||
                    frame.Link.FirstProjectileIndex != frame.Projectile.Index ||
                    !BasicHookMotion.IsSafetyAnchor(in frame.Anchor) ||
                    frame.Anchor.TileX != _route.IntendedAnchor.TileX ||
                    frame.Anchor.TileY != _route.IntendedAnchor.TileY ||
                    frame.Anchor.TileType != _route.IntendedAnchor.TileType ||
                    !NearlySame(frame.Anchor.HookCenter, _route.IntendedAnchorCenter, .1f))
                    return false;

                if (!_previousProjectileKnown) return false;
                BasicHookFailure previousLatchFailure;
                var previousLatchPhase = BasicHookMotion.ClassifyProjectile(
                    in _previousProjectile, frame.Context.LocalPlayerIndex,
                    out previousLatchFailure);
                return _firstLatchTick < 0
                    ? previousLatchPhase == BasicHookProjectilePhase.Outbound
                    : previousLatchPhase == BasicHookProjectilePhase.Latched &&
                      NearlySame(_previousProjectile.Center,
                          _route.IntendedAnchorCenter, .1f);
            }

            if (!_previousProjectileKnown)
            {
                if (phase != BasicHookProjectilePhase.Outbound) return false;
                Vec2 expected;
                return TryScaleAdd(_route.FireCenter, _route.LaunchVelocity,
                    _ticksSinceShot, out expected) &&
                    NearlySame(frame.Projectile.Center, expected, .15f);
            }

            BasicHookFailure previousFailure2;
            var previousPhase2 = BasicHookMotion.ClassifyProjectile(in _previousProjectile,
                frame.Context.LocalPlayerIndex, out previousFailure2);
            if (previousPhase2 == BasicHookProjectilePhase.Outbound)
            {
                Vec2 expected;
                if (!TryAdd(_previousProjectile.Center, _route.LaunchVelocity,
                        out expected) || !NearlySame(frame.Projectile.Center, expected, .15f))
                    return false;
                if (phase == BasicHookProjectilePhase.Outbound) return true;
                if (phase != BasicHookProjectilePhase.ReturningAfterMiss) return false;
                double distance;
                return TryDistance(_previousProjectile.Center, _previousPlayerCenter,
                    out distance) && distance > BasicHookMotion.OutboundRange;
            }
            if (previousPhase2 != BasicHookProjectilePhase.ReturningAfterMiss ||
                phase != BasicHookProjectilePhase.ReturningAfterMiss) return false;
            Vec2 returnVelocity;
            Vec2 returnCenter;
            return TryNormalizedVector(_previousPlayerCenter, _previousProjectile.Center,
                       BasicHookMotion.ReturnSpeed, out returnVelocity) &&
                   TryAdd(_previousProjectile.Center, returnVelocity, out returnCenter) &&
                   NearlySame(frame.Projectile.Center, returnCenter, .15f);
        }

        private bool PredictAttachedPlayer(in BasicHookFrameSnapshot frame, bool jump)
        {
            BasicHookAttachedTick attached;
            if (!BasicHookMotion.TryApplyAttachedTick(in frame.Projectile, in frame.Link,
                    in frame.Anchor, frame.Context.LocalPlayerIndex, frame.PlayerCenter,
                    frame.PlayerVelocity, false, _route.JumpSpeed, _route.JumpHeight,
                    _route.OldStyleParkour, jump, false, false, frame.ReleaseJump,
                    out attached)) return false;
            if (jump != attached.Detached) return false;
            var center = default(Vec2);
            if (!TryAdd(frame.PlayerCenter, attached.VelocityAfterGrapple, out center))
                return false;
            _expectedPlayerCenter = center;
            _expectedPlayerVelocity = attached.VelocityAfterGrapple;
            _expectedPlayerKnown = true;
            return true;
        }

        private void PredictNeutralPlayer(in BasicHookFrameSnapshot frame)
        {
            var velocity = frame.PlayerVelocity;
            var drag = _route.HorizontalSlowdown * (frame.PlayerGrounded ? 1f : .5f);
            if (Math.Abs(velocity.X) <= drag) velocity.X = 0f;
            else velocity.X -= Math.Sign(velocity.X) * drag;
            if (frame.PlayerGrounded) velocity.Y = 0f;
            else
            {
                velocity.Y = Math.Min(_route.MaximumFallSpeed,
                    velocity.Y + _route.Gravity / (_route.ReturnProfileSlowFall ? 3f : 1f));
                if (_route.ReturnProfileSlowFall && velocity.Y > _route.MaximumFallSpeed / 3f)
                    velocity.Y = _route.MaximumFallSpeed / 3f;
            }
            var center = default(Vec2);
            _expectedPlayerKnown = Finite(velocity) &&
                TryAdd(frame.PlayerCenter, velocity, out center);
            _expectedPlayerVelocity = velocity;
            _expectedPlayerCenter = _expectedPlayerKnown ? center : default(Vec2);
        }

        private bool MatchesExpectedPlayer(in BasicHookFrameSnapshot frame)
        {
            return !_expectedPlayerKnown ||
                NearlySame(frame.PlayerCenter, _expectedPlayerCenter, .2f) &&
                NearlySame(frame.PlayerVelocity, _expectedPlayerVelocity, .2f);
        }

        private BasicHookRescueStep AbortOrDetach(in BasicHookFrameSnapshot frame,
            ref ControlPlan plan)
        {
            if (CanSafelyDetach(in frame))
            {
                _abortDetach = true;
                _lastSequence = frame.Sequence;
                return ContinueAbortDetach(in frame, ref plan);
            }
            Reset();
            return BasicHookRescueStep.Abort;
        }

        private BasicHookRescueStep ContinueAbortDetach(in BasicHookFrameSnapshot frame,
            ref ControlPlan plan)
        {
            if (!CanSafelyDetach(in frame))
            {
                Reset();
                return BasicHookRescueStep.Abort;
            }
            Neutralize(ref plan);
            plan.Jump = frame.ReleaseJump;
            plan.JumpAction = frame.ReleaseJump ? JumpAction.Hold : JumpAction.Release;
            _expectedPlayerKnown = false;
            RememberProjectile(in frame);
            return BasicHookRescueStep.Abort;
        }

        private bool CanSafelyDetach(in BasicHookFrameSnapshot frame)
        {
            if (!frame.Known || !frame.ProjectileObserved || !frame.Link.Known ||
                !frame.Anchor.Known) return false;
            BasicHookFailure failure;
            return BasicHookMotion.ClassifyProjectile(in frame.Projectile,
                       frame.Context.LocalPlayerIndex, out failure) ==
                   BasicHookProjectilePhase.Latched &&
                   frame.Link.AtGrappleMovementEntry && frame.Link.GrappleCount == 1 &&
                   frame.Link.FirstProjectileIndex == frame.Projectile.Index &&
                   BasicHookMotion.IsSafetyAnchor(in frame.Anchor) &&
                   NearlySame(frame.Anchor.HookCenter, frame.Projectile.Center, .1f);
        }

        private void RememberProjectile(in BasicHookFrameSnapshot frame)
        {
            _previousProjectileKnown = frame.ProjectileObserved;
            _previousProjectile = frame.Projectile;
            _previousPlayerCenter = frame.PlayerCenter;
        }

        private bool SameStrategy(string strategyId, string phaseId)
            => string.Equals(_strategyId, strategyId, StringComparison.Ordinal) &&
               string.Equals(_phaseId, phaseId, StringComparison.Ordinal);

        private static void Neutralize(ref ControlPlan plan)
        {
            plan.Horizontal = 0;
            plan.Jump = false;
            plan.JumpAction = JumpAction.Default;
            plan.Drop = false;
            plan.Dash = false;
            plan.Hook = false;
            plan.ToggleMount = false;
            plan.GravityControl = 0;
            plan.FeatherFallUp = false;
        }

        private static bool TryScaleAdd(Vec2 origin, Vec2 velocity, int ticks,
            out Vec2 result)
        {
            result = default(Vec2);
            if (ticks < 0 || !Finite(origin) || !Finite(velocity)) return false;
            var x = (double)origin.X + velocity.X * ticks;
            var y = (double)origin.Y + velocity.Y * ticks;
            if (!Finite(x) || !Finite(y) || Math.Abs(x) > float.MaxValue ||
                Math.Abs(y) > float.MaxValue) return false;
            result = new Vec2((float)x, (float)y);
            return true;
        }

        private static bool TryNormalizedVector(Vec2 target, Vec2 origin, float length,
            out Vec2 result)
        {
            result = default(Vec2);
            double distance;
            if (!TryDistance(target, origin, out distance) || distance <= 0d ||
                !Finite(length) || length <= 0f) return false;
            var scale = length / distance;
            result = new Vec2((float)((target.X - origin.X) * scale),
                (float)((target.Y - origin.Y) * scale));
            return Finite(result);
        }

        private static bool TryAdd(Vec2 left, Vec2 right, out Vec2 result)
        {
            var x = (double)left.X + right.X;
            var y = (double)left.Y + right.Y;
            result = default(Vec2);
            if (!Finite(left) || !Finite(right) || !Finite(x) || !Finite(y) ||
                Math.Abs(x) > float.MaxValue || Math.Abs(y) > float.MaxValue)
                return false;
            result = new Vec2((float)x, (float)y);
            return true;
        }

        private static bool NearlySame(Vec2 left, Vec2 right, float tolerance)
        {
            double distance;
            return TryDistance(left, right, out distance) && distance <= tolerance;
        }

        private static bool TryDistance(Vec2 left, Vec2 right, out double distance)
        {
            distance = double.NaN;
            if (!Finite(left) || !Finite(right)) return false;
            var x = (double)left.X - right.X;
            var y = (double)left.Y - right.Y;
            distance = Math.Sqrt(x * x + y * y);
            return Finite(distance);
        }

        private static bool Finite(Vec2 value) => Finite(value.X) && Finite(value.Y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
