using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// Bounded-horizon combat planner. Boss code chooses a durable movement pattern;
    /// the nine-candidate predictor is only the inner collision-avoidance loop.
    /// </summary>
    public sealed class CombatPlanner
    {
        private readonly PlannerSettings _settings;
        private readonly BossStrategyEngine _strategies = new BossStrategyEngine();
        private int _lastHorizontal = 1;
        private int _patternDirection = 1;
        private int _directionHoldTicks;
        private int _stuckTicks;
        private int _recoveryTicks;
        private int _emergencyHoldTicks;
        private int _stableTicks;
        private bool _hasPreviousPosition;
        private Vec2 _previousPosition;
        private readonly List<ThreatSnapshot> _relevantThreats = new List<ThreatSnapshot>(1000);
        private readonly List<ThreatSnapshot> _relevantBeams = new List<ThreatSnapshot>(48);
        private readonly int _horizonTicks;
        private readonly int _stepTicks;
        private int _hookCooldown;
        private int _grappleAttachedTicks;
        private int _grappleReleaseTicks;
        private int _gravityCooldown;
        private int _mountCooldown;
        private bool _restoringFlight;
        private SupportSpan _landingSupport;
        private ThreatStep[] _threatSteps = Array.Empty<ThreatStep>();
        private BeamStep[] _beamSteps = Array.Empty<BeamStep>();

        public int LastCandidateCount { get; private set; }
        public int LastRelevantThreatCount => _relevantThreats.Count + _relevantBeams.Count;

        public CombatPlanner(PlannerSettings settings)
        {
            _settings = settings ?? new PlannerSettings();
            _horizonTicks = Math.Max(6, Math.Min(90, _settings.HorizonTicks));
            _stepTicks = Math.Max(1, Math.Min(6, _settings.SimulationStepTicks));
        }

        public bool RequirementsMet(CombatSnapshot snapshot, out string reason)
        {
            if (!WeaponProfileReady(snapshot, out reason)) return false;
            var requirements = _strategies.RequirementsFor(snapshot);
            if (requirements == null)
            {
                reason = "未识别到受支持的 Boss";
                return false;
            }
            return requirements.IsMet(snapshot, out reason);
        }

        public bool RequirementsMetForExpected(CombatSnapshot snapshot, string planId, int expectedBossType, out string reason)
        {
            if (!WeaponProfileReady(snapshot, out reason)) return false;
            BossRequirements requirements;
            if (!string.IsNullOrEmpty(planId) && planId.IndexOf("mechdusa", StringComparison.OrdinalIgnoreCase) >= 0)
                requirements = _strategies.RequirementsForExpected(snapshot.Difficulty, 125, 127, 134);
            else
                requirements = _strategies.RequirementsForExpected(snapshot.Difficulty, expectedBossType);
            if (requirements == null)
            {
                reason = "没有对应的 Boss 策略";
                return false;
            }
            return requirements.IsMet(snapshot, out reason);
        }

        public ControlPlan Plan(CombatSnapshot snapshot)
        {
            var plan = NewPlan(snapshot);
            if (snapshot?.Player == null || snapshot.Player.Dead)
                return plan;

            var decision = _strategies.Evaluate(snapshot);
            if (decision == null)
                return PlanSurvival(snapshot);
            UpdateMotionMemory(snapshot.Player);

            var directive = decision.Directive;
            var target = decision.Target;
            var patternTarget = decision.PatternTarget;
            PrepareThreats(snapshot, directive);
            var patternHorizontal = PatternHorizontal(snapshot, patternTarget, directive);
            var patternVertical = PatternVertical(snapshot, patternTarget, directive);
            RespectArenaEdges(snapshot, ref patternHorizontal, ref patternVertical, directive.UseExplicitMovement);
            BudgetFlight(snapshot, ref patternHorizontal, ref patternVertical);

            var best = FindBestCandidate(snapshot, patternTarget, directive, patternHorizontal, patternVertical);
            var immediateRisk = ImmediateRisk(snapshot, directive);
            var emergency = immediateRisk >= _settings.EmergencyRiskThreshold || best.Hazard >= _settings.EmergencyRiskThreshold;
            var stuck = _stuckTicks >= _settings.StuckTicksBeforeRecovery;

            TacticalMode mode;
            if (emergency)
            {
                _emergencyHoldTicks = _settings.EmergencyHysteresisTicks;
                _recoveryTicks = _settings.RecoveryTicks;
                _stableTicks = 0;
                mode = TacticalMode.EmergencyEvade;
            }
            else if (_emergencyHoldTicks > 0)
            {
                _emergencyHoldTicks--;
                mode = TacticalMode.EmergencyEvade;
            }
            else if (stuck || _recoveryTicks > 0 || _restoringFlight)
            {
                if (_recoveryTicks > 0)
                    _recoveryTicks--;
                mode = TacticalMode.RecoverToPattern;
            }
            else if (_stableTicks < _settings.StablePatternTicks)
            {
                _stableTicks++;
                mode = TacticalMode.EstablishPattern;
            }
            else
            {
                mode = TacticalMode.StablePattern;
            }

            plan.Horizontal = best.Horizontal;
            plan.Jump = best.Posture > 0;
            plan.Drop = best.Posture < 0;
            plan.TargetKey = target.Key;
            plan.RiskScore = best.Score;
            plan.TacticalMode = mode;
            plan.StrategyId = directive.StrategyId;
            plan.PhaseId = directive.PhaseId;

            // Source-specific controllers (e.g. a committed King run-under)
            // must keep the scored escape, not receive an unscored reversal/jump
            // afterwards. FindBestCandidate already expands for stuck states.
            if (mode == TacticalMode.RecoverToPattern && stuck && !directive.UseExplicitMovement)
            {
                plan.Horizontal = _lastHorizontal == 0 ? _patternDirection : -_lastHorizontal;
                plan.Jump = true;
            }

            plan.Dash = snapshot.Mobility.CanDash && snapshot.Mobility.DashReady && plan.Horizontal != 0 &&
                        (directive.PreferDash || mode == TacticalMode.EmergencyEvade || mode == TacticalMode.RecoverToPattern);
            ApplyMobilityTools(snapshot, directive, mode, ref plan);

            ApplyWeaponAim(snapshot, target, directive.Fire, ref plan);
            ApplyConsumables(snapshot, ref plan);
            RememberPlan(plan);
            return plan;
        }

        /// <summary>Low-cost defensive loop used while a scheduled or issued summon is pending.</summary>
        public ControlPlan PlanSurvival(CombatSnapshot snapshot)
        {
            var plan = NewPlan(snapshot);
            if (snapshot?.Player == null || snapshot.Player.Dead)
                return plan;

            UpdateMotionMemory(snapshot.Player);
            LastCandidateCount = 0;
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            if (snapshot.Targets.Count == 0 && snapshot.Threats.Count == 0)
            {
                plan.Horizontal = ArenaCenterDirection(snapshot);
                plan.TacticalMode = TacticalMode.AwaitingBoss;
                ApplyGrappleRelease(snapshot, ref plan);
                ApplyConsumables(snapshot, ref plan);
                RememberPlan(plan);
                return plan;
            }

            var hasTarget = snapshot.Targets.Count > 0;
            var target = hasTarget ? SelectPrimaryTarget(snapshot) : new TargetSnapshot
            {
                Key = -1, Position = snapshot.Player.Center, Width = 0, Height = 0, Invulnerable = true
            };
            var directive = new BossDirective
            {
                StrategyId = "awaiting-boss-survival",
                PhaseId = "clear-hostiles",
                Pattern = BossPattern.HorizontalKite,
                IdealDistance = hasTarget ? 440f : 0f,
                VerticalOffset = hasTarget ? -70f : 0f,
                HorizontalIntent = hasTarget ? AwayX(snapshot.Player.Center, target.Center) : 0,
                ForceContinuousMovement = hasTarget,
                Fire = true,
                ExtraContactMargin = 48f,
                AllowHook = true
            };
            PrepareThreats(snapshot, directive);
            var h = hasTarget ? PatternHorizontal(snapshot, target, directive) : ArenaCenterDirection(snapshot);
            var v = hasTarget ? PatternVertical(snapshot, target, directive) : 0;
            RespectArenaEdges(snapshot, ref h, ref v);
            BudgetFlight(snapshot, ref h, ref v);
            var best = FindBestCandidate(snapshot, target, directive, h, v);
            plan.Horizontal = best.Horizontal;
            plan.Jump = best.Posture > 0;
            plan.Drop = best.Posture < 0;
            plan.TargetKey = target.Key;
            plan.RiskScore = best.Score;
            plan.TacticalMode = TacticalMode.AwaitingBoss;
            plan.StrategyId = directive.StrategyId;
            plan.PhaseId = directive.PhaseId;
            ApplyWeaponAim(snapshot, target, hasTarget, ref plan);
            plan.Dash = snapshot.Mobility.CanDash && snapshot.Mobility.DashReady && plan.Horizontal != 0 &&
                        best.Score >= _settings.EmergencyRiskThreshold;
            ApplyMobilityTools(snapshot, directive, TacticalMode.AwaitingBoss, ref plan);
            ApplyConsumables(snapshot, ref plan);
            RememberPlan(plan);
            return plan;
        }

        public void Reset()
        {
            _strategies.Reset();
            _lastHorizontal = 1;
            _patternDirection = 1;
            _directionHoldTicks = 0;
            _stuckTicks = 0;
            _recoveryTicks = 0;
            _emergencyHoldTicks = 0;
            _stableTicks = 0;
            _hasPreviousPosition = false;
            _restoringFlight = false;
            _landingSupport = default(SupportSpan);
            _hookCooldown = _gravityCooldown = _mountCooldown = 0;
            _grappleAttachedTicks = _grappleReleaseTicks = 0;
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            LastCandidateCount = 0;
        }

        private static ControlPlan NewPlan(CombatSnapshot snapshot)
        {
            return new ControlPlan
            {
                PreferredWeaponSlot = snapshot?.Weapon?.Slot ?? 0,
                TargetKey = -1,
                TacticalMode = TacticalMode.EstablishPattern
            };
        }

        private void UpdateMotionMemory(PlayerSnapshot player)
        {
            if (_hasPreviousPosition)
            {
                var moved = Vec2.DistanceSquared(player.Position, _previousPosition);
                if (Math.Abs(_lastHorizontal) > 0 && moved < _settings.StuckDistancePixels * _settings.StuckDistancePixels)
                    _stuckTicks++;
                else
                    _stuckTicks = Math.Max(0, _stuckTicks - 2);
            }
            _previousPosition = player.Position;
            _hasPreviousPosition = true;
            if (_directionHoldTicks > 0)
                _directionHoldTicks--;
            if (_hookCooldown > 0) _hookCooldown--;
            if (_gravityCooldown > 0) _gravityCooldown--;
            if (_mountCooldown > 0) _mountCooldown--;
        }

        private int PatternHorizontal(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive)
        {
            if (directive.UseExplicitMovement)
                return ClampIntent(directive.HorizontalIntent);
            if (_directionHoldTicks > 0 && directive.Pattern != BossPattern.PerpendicularDashDodge)
                return _patternDirection;

            var player = snapshot.Player.Center;
            var delta = player - target.Center;
            var absX = Math.Abs(delta.X);
            var ideal = Math.Max(80f, directive.IdealDistance);
            if (directive.HorizontalIntent != 0)
            {
                var intent = ClampIntent(directive.HorizontalIntent);
                if ((directive.Pattern == BossPattern.Runway || directive.Pattern == BossPattern.HorizontalKite ||
                     directive.Pattern == BossPattern.ProjectileLanes) && intent == AwayX(player, target.Center))
                {
                    // Running away forever eventually despawns a slower Boss.
                    // Coast once a safe firing gap is established; recover toward
                    // a distant Boss only outside a wider hysteresis band. The
                    // collision predictor can still override this in an emergency.
                    if (absX > ideal * 1.8f) return -intent;
                    if (absX > ideal * 1.15f) return 0;
                }
                return intent;
            }
            int wanted;
            switch (directive.Pattern)
            {
                case BossPattern.CircleOrbit:
                case BossPattern.EllipseOrbit:
                    wanted = delta.Y * _patternDirection > directive.VerticalOffset ? -_patternDirection : _patternDirection;
                    break;
                case BossPattern.StayCloseJump:
                    wanted = absX > ideal * 1.15f ? -AwayX(player, target.Center) : AwayX(player, target.Center);
                    break;
                case BossPattern.PerpendicularDashDodge:
                    wanted = target.Velocity.X == 0f ? AwayX(player, target.Center) : -Math.Sign(target.Velocity.X);
                    break;
                default:
                    if (absX < ideal * .72f)
                        wanted = AwayX(player, target.Center);
                    else if (absX > ideal * 1.28f)
                        wanted = -AwayX(player, target.Center);
                    else
                        wanted = _lastHorizontal == 0 ? _patternDirection : _lastHorizontal;
                    break;
            }
            return ClampIntent(wanted);
        }

        private int PatternVertical(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive)
        {
            var gravitySign = snapshot.Mobility.GravityInverted ? -1 : 1;
            if (directive.UseExplicitMovement)
                return ClampIntent(directive.VerticalIntent) * gravitySign;
            if (directive.VerticalIntent != 0)
                return ClampIntent(directive.VerticalIntent) * gravitySign;

            var wantedY = target.Center.Y + directive.VerticalOffset;
            if (directive.FloorClearance > 0f && snapshot.Arena.HasFloor && snapshot.Arena.LocalOpenBounds.Height > 0f)
                wantedY = Math.Min(wantedY, snapshot.Arena.LocalOpenBounds.Bottom - directive.FloorClearance);
            var error = wantedY - snapshot.Player.Center.Y;
            if (error < -42f)
                return gravitySign;
            if (error > 70f)
                return -gravitySign;
            if (directive.Pattern == BossPattern.CircleOrbit || directive.Pattern == BossPattern.EllipseOrbit)
                return _patternDirection * (snapshot.Player.Center.X >= target.Center.X ? 1 : -1) * gravitySign;
            return 0;
        }

        private void RespectArenaEdges(CombatSnapshot snapshot, ref int horizontal, ref int vertical, bool explicitMovement = false)
        {
            var arena = snapshot.Arena;
            // Start a turn early enough to brake, instead of steering into a dead end
            // and hoping the immediate evasion layer can undo accumulated momentum.
            var speed = Math.Abs(snapshot.Player.Velocity.X);
            var braking = explicitMovement ? Math.Max(.000001f, snapshot.Player.RunSlowdown) :
                Math.Max(.08f, snapshot.Player.RunAcceleration);
            var turnMargin = _settings.ArenaEdgeMarginPixels + speed * speed /
                (2f * braking) + speed * _settings.DirectionHysteresisTicks;
            if (!explicitMovement)
                turnMargin = Math.Min(turnMargin, Math.Max(_settings.ArenaEdgeMarginPixels, arena.HorizontalClearance * .42f));
            if (arena.ClearanceLeft < turnMargin && horizontal < 0)
            {
                if (explicitMovement) horizontal = 0;
                else ReversePattern(1, ref horizontal);
            }
            else if (arena.ClearanceRight < turnMargin && horizontal > 0)
            {
                if (explicitMovement) horizontal = 0;
                else ReversePattern(-1, ref horizontal);
            }

            var worldVertical = vertical * (snapshot.Mobility.GravityInverted ? -1 : 1);
            if (arena.ClearanceUp < _settings.ArenaVerticalMarginPixels && worldVertical > 0)
                vertical = snapshot.Mobility.GravityInverted ? 1 : -1;
            // A floor is a replenishment destination, not an obstacle to flee forever.
            else if (arena.ClearanceDown < _settings.ArenaVerticalMarginPixels && worldVertical < 0 &&
                     !arena.HasFloor && !snapshot.Player.OnGround)
                vertical = snapshot.Mobility.GravityInverted ? -1 : 1;
        }

        private void ReversePattern(int newDirection, ref int horizontal)
        {
            _patternDirection = newDirection;
            horizontal = newDirection;
            _directionHoldTicks = _settings.DirectionHysteresisTicks;
        }

        private void BudgetFlight(CombatSnapshot snapshot, ref int horizontal, ref int vertical)
        {
            var inverted = snapshot.Mobility.GravityInverted;
            _landingSupport = snapshot.Arena.RecoverySupport;
            if (!_landingSupport.Valid || _landingSupport.Inverted != inverted)
                _landingSupport = inverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
            if (!snapshot.Mobility.HasFiniteFlightResource ||
                snapshot.Mobility.MountActive && snapshot.Mobility.MountCanFly || snapshot.Mobility.CanFlipGravity)
            {
                // A normal ground/double jump is not an exhausted flight cycle.
                // Preserve the Boss strategy's horizontal and vertical intents.
                _restoringFlight = false;
                return;
            }
            if (snapshot.Player.OnGround || snapshot.Mobility.FlightResourceFraction >= _settings.FlightResumeFraction)
                _restoringFlight = false;
            else if (snapshot.Mobility.FlightResourceFraction < _settings.FlightReserveFraction)
                _restoringFlight = true;
            if (_landingSupport.Valid && !snapshot.Player.OnGround && _landingSupport.Inverted == inverted &&
                !(_landingSupport.OneWay && inverted))
            {
                var player = snapshot.Player;
                var halfWidth = player.Width * .5f;
                var speed = Math.Abs(player.Velocity.X);
                var brakeTicks = speed / Math.Max(.08f, player.RunAcceleration + player.RunSlowdown);
                var margin = Math.Min(Math.Max(0f, (_landingSupport.Right - _landingSupport.Left - player.Width) * .25f),
                    32f + speed * (brakeTicks * .5f + 8f));
                var left = _landingSupport.Left + halfWidth + margin;
                var right = _landingSupport.Right - halfWidth - margin;
                if (left > right) left = right = (_landingSupport.Left + _landingSupport.Right) * .5f;
                var projectedX = player.Center.X + player.Velocity.X * 8f;
                var outside = Math.Max(0f, Math.Max(left - projectedX, projectedX - right));
                var returnTicks = outside / Math.Max(1f, player.MaxRunSpeed) + brakeTicks;
                var remainingFlight = Math.Max(player.WingTime, player.RocketTime);
                if (outside > 0f && (snapshot.Mobility.FlightResourceFraction < Math.Min(.5f, _settings.FlightReserveFraction + .25f) ||
                    remainingFlight < returnTicks + 24f)) _restoringFlight = true;
                if (_restoringFlight)
                {
                    // This is the desired landing route, BEFORE the emergency
                    // candidate search. Never overwrite its chosen dodge afterward.
                    horizontal = player.Center.X < left ? 1 : player.Center.X > right ? -1 : 0;
                    if (_landingSupport.OneWay && vertical < 0) vertical = 0;
                }
            }
            if (_restoringFlight && vertical > 0) vertical = 0;
        }

        private Candidate FindBestCandidate(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive, int desiredHorizontal, int desiredVertical)
        {
            LastCandidateCount = 1;
            var best = ScoreCandidate(snapshot, target, directive, desiredHorizontal, desiredVertical, desiredHorizontal, desiredVertical);
            // Stable-pattern controller owns the route; alternative controls are only
            // considered when its predicted trajectory is unsafe or we are recovering.
            if (best.Hazard < _settings.PatternSafeRiskThreshold && _stuckTicks < _settings.StuckTicksBeforeRecovery)
                return best;
            for (var horizontal = -1; horizontal <= 1; horizontal++)
            {
                for (var posture = -1; posture <= 1; posture++)
                {
                    if (horizontal == desiredHorizontal && posture == desiredVertical) continue;
                    LastCandidateCount++;
                    var candidate = ScoreCandidate(snapshot, target, directive, horizontal, posture, desiredHorizontal, desiredVertical, best.Score);
                    if (candidate.Score < best.Score) best = candidate;
                }
            }
            return best;
        }

        private Candidate ScoreCandidate(CombatSnapshot snapshot, TargetSnapshot target, BossDirective directive,
            int horizontal, int posture, int desiredHorizontal, int desiredVertical, float incumbentScore = float.MaxValue)
        {
            var player = snapshot.Player;
            var position = player.Position;
            var velocity = player.Velocity;
            var risk = 0f;
            var step = _stepTicks;
            var mountedSpeed = snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f;
            var gravityDirection = snapshot.Mobility.GravityInverted ? -1f : 1f;
            var flyingMount = snapshot.Mobility.MountActive && snapshot.Mobility.MountCanFly;
            var flightTicks = Math.Max(player.WingTime, player.RocketTime);
            var canInitialJump = player.OnGround || flightTicks > 0f || flyingMount;
            var grounded = player.OnGround && posture <= 0;
            var standingY = player.Position.Y;
            var support = snapshot.Mobility.GravityInverted ? snapshot.Arena.CeilingSupport : snapshot.Arena.FloorSupport;
            var actualFoot = snapshot.Mobility.GravityInverted ? player.Position.Y : player.Position.Y + player.Height;
            if (player.OnGround && (!support.OverlapsBody(player.Position.X, player.Width) ||
                support.Inverted != snapshot.Mobility.GravityInverted || Math.Abs(actualFoot - support.SurfaceY) > 2f))
            {
                // Native OnGround is evidence of current contact, even on shapes
                // the flat-row scanner deliberately excludes. Preserve only this
                // actual footprint, never extrapolate it across open arena space.
                support = new SupportSpan { Valid = true, Inverted = snapshot.Mobility.GravityInverted,
                    OneWay = player.OnOneWaySupport,
                    Left = player.Position.X, Right = player.Position.X + player.Width, SurfaceY = actualFoot };
            }
            var originalSupport = support;
            if (grounded && !SupportGeometry.RetainsFooting(support, position.X, player.Width,
                snapshot.Mobility.GravityInverted, posture < 0)) grounded = false;
            var previousBounds = player.BoundsAt(position);

            if (posture > 0 && canInitialJump)
            {
                if (player.OnGround)
                    velocity.Y = -(5.01f + Math.Max(0f, player.JumpSpeedBoost)) * gravityDirection;
                else
                    velocity.Y -= .55f * step * gravityDirection;
            }

            for (var tick = step; tick <= _horizonTicks; tick += step)
            {
                var beforeStep = position;
                float horizontalTravel;
                velocity.X = HorizontalMotion.Advance(player, velocity.X, horizontal, grounded, step, out horizontalTravel, mountedSpeed);
                position.X += horizontalTravel;
                if (grounded && !SupportGeometry.RetainsFooting(support, position.X, player.Width,
                    snapshot.Mobility.GravityInverted, posture < 0)) grounded = false;
                if (grounded) velocity.Y = 0f;
                else
                {
                    var gravity = Math.Max(.1f, Math.Abs(player.Gravity));
                    velocity.Y += gravity * gravityDirection * step;
                    if (posture > 0 && (flyingMount || tick <= flightTicks))
                        velocity.Y -= (gravity + .35f) * gravityDirection * step;
                    else if (posture < 0 && snapshot.Mobility.FeatherFall)
                        velocity.Y += .12f * gravityDirection * step;
                }
                var maxFall = Math.Max(6f, player.MaxFallSpeed);
                velocity.Y = Math.Max(-maxFall, Math.Min(maxFall, velocity.Y));
                position.Y += velocity.Y * step;
                if (grounded) position.Y = standingY;

                if (!grounded)
                {
                    var first = originalSupport;
                    var second = snapshot.Arena.RecoverySupport;
                    if (second.Valid && second.Inverted == snapshot.Mobility.GravityInverted &&
                        (!first.Valid || (second.SurfaceY - first.SurfaceY) * gravityDirection < 0f))
                    {
                        first = second;
                        second = originalSupport;
                    }
                    var landed = SupportGeometry.TryLand(first, beforeStep, ref position, ref velocity,
                        player.Width, player.Height, snapshot.Mobility.GravityInverted, posture < 0);
                    if (landed) support = first;
                    else if (SupportGeometry.TryLand(second, beforeStep, ref position, ref velocity,
                        player.Width, player.Height, snapshot.Mobility.GravityInverted, posture < 0))
                    {
                        landed = true;
                        support = second;
                    }
                    if (landed)
                    {
                        standingY = position.Y;
                        grounded = true;
                    }
                }

                var bounds = player.BoundsAt(position);
                if (OutsideWorldOrArena(snapshot, bounds))
                    risk += _settings.CollisionPenalty;

                var timeWeight = 1f / (1f + tick * .035f);
                var cacheOffset = (tick / step - 1) * _relevantThreats.Count;
                for (var i = 0; i < _relevantThreats.Count; i++)
                {
                    var cacheIndex = cacheOffset + i;
                    if (!_settings.CacheThreatPrediction)
                        _threatSteps[cacheIndex] = PredictThreat(_relevantThreats[i], tick, directive);
                    ref var predicted = ref _threatSteps[cacheIndex];
                    if (!predicted.Active) continue;
                    var liveBounds = bounds;
                    if (predicted.LifeFraction < 1f)
                    {
                        var fraction = predicted.LifeFraction;
                        liveBounds = new RectF(previousBounds.X + (bounds.X - previousBounds.X) * fraction,
                            previousBounds.Y + (bounds.Y - previousBounds.Y) * fraction, bounds.Width, bounds.Height);
                    }
                    if (SweptIntersects(in previousBounds, in liveBounds, in predicted.Before, in predicted.After))
                        risk += predicted.DamageRisk;
                    else
                    {
                        var separation = liveBounds.SeparationSquared(predicted.After);
                        if (separation < 14400f)
                            risk += _settings.NearMissPenalty * (1f - separation / 14400f) * timeWeight;
                    }
                }
                var beamOffset = (tick / step - 1) * _relevantBeams.Count;
                var playerSweep = BeamGeometry.Union(previousBounds, bounds);
                for (var i = 0; i < _relevantBeams.Count; i++)
                {
                    var beamIndex = beamOffset + i;
                    if (!_settings.CacheThreatPrediction) _beamSteps[beamIndex] = PredictBeam(_relevantBeams[i], tick);
                    ref var beam = ref _beamSteps[beamIndex];
                    if (!beam.Shape.Active) continue;
                    // All three tapered Sun Dance lobes belong to one projectile;
                    // overlapping lobes never triple-count its damage.
                    if (BeamGeometry.Intersects(playerSweep, beam.Shape, _settings.ProjectileSafetyMargin))
                        risk += beam.DamageRisk;
                    else
                    {
                        var separation = BeamGeometry.SeparationSquared(playerSweep, beam.Shape);
                        if (separation < 14400f)
                            risk += _settings.NearMissPenalty * (1f - separation / 14400f) * timeWeight;
                    }
                }
                previousBounds = bounds;
                // All remaining default costs are nonnegative. A losing partial path
                // cannot become the winner later, so do not simulate its unused tail.
                if (_settings.EnableScorePruning && risk >= incumbentScore && _settings.CollisionPenalty >= 0f && _settings.DamagePenalty >= 0f &&
                    _settings.NearMissPenalty >= 0f && _settings.PatternDeviationPenalty >= 0f &&
                    _settings.VerticalPatternDeviationPenalty >= 0f && _settings.MovementChangePenalty >= 0f)
                    return new Candidate { Horizontal = horizontal, Posture = posture, Score = risk, Hazard = risk };
            }

            var hazard = risk;
            var futureCenter = new Vec2(position.X + player.Width * .5f, position.Y + player.Height * .5f);
            var predictedTarget = target.Center + target.Velocity * _horizonTicks;
            var delta = futureCenter - predictedTarget;
            var distanceError = Math.Abs(delta.Length - directive.IdealDistance);
            risk += distanceError * .8f;
            risk += Math.Abs(delta.Y - directive.VerticalOffset) * .18f;
            if (horizontal != desiredHorizontal)
                risk += _settings.PatternDeviationPenalty;
            if (posture != desiredVertical)
                risk += _settings.VerticalPatternDeviationPenalty;
            if (directive.ForceContinuousMovement && horizontal == 0)
                risk += 22f;
            if (horizontal != _lastHorizontal)
                risk += _settings.MovementChangePenalty;
            if (!snapshot.LineOfSightToPrimary)
                risk += distanceError * .1f;
            // Landing/restoration is a deliberate sub-loop, not an accidental failure
            // to follow an airborne preferred path. Emergency candidates may override it.
            if (_restoringFlight && posture > 0) risk += _settings.PatternDeviationPenalty * 2f;
            if (_restoringFlight && _landingSupport.Valid)
            {
                var outside = Math.Max(0f, Math.Max(_landingSupport.Left - position.X,
                    position.X + player.Width - _landingSupport.Right));
                risk += Math.Min(120f, outside * .5f);
            }
            return new Candidate { Horizontal = horizontal, Posture = posture, Score = risk, Hazard = hazard };
        }

        private void PrepareThreats(CombatSnapshot snapshot, BossDirective directive)
        {
            _relevantThreats.Clear();
            _relevantBeams.Clear();
            var player = snapshot.Player;
            var speedX = Math.Max(Math.Abs(player.Velocity.X), Math.Max(player.MaxRunSpeed,
                snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f));
            var speedY = Math.Max(Math.Abs(player.Velocity.Y), Math.Max(12f, player.MaxFallSpeed));
            var reach = new RectF(player.Position.X - speedX * _horizonTicks - 160f,
                player.Position.Y - speedY * _horizonTicks - 160f,
                player.Width + 2f * (speedX * _horizonTicks + 160f),
                player.Height + 2f * (speedY * _horizonTicks + 160f));
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                if (threat.Geometry != ThreatGeometry.Body)
                {
                    if (reach.Intersects(BeamGeometry.ConservativeBounds(threat, _horizonTicks))) _relevantBeams.Add(threat);
                    continue;
                }
                var ticks = threat.TimeLeft > 0 ? Math.Min(_horizonTicks, threat.TimeLeft) : _horizonTicks;
                var travel = threat.Velocity * ticks;
                var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                    _settings.ContactSafetyMargin + directive.ExtraContactMargin;
                // Swept broadphase includes incoming fast projectiles whose present
                // position is far away; no fixed-radius culling at the current tick.
                var swept = new RectF(threat.Position.X + Math.Min(0f, travel.X),
                    threat.Position.Y + Math.Min(0f, travel.Y), threat.Width + Math.Abs(travel.X),
                    threat.Height + Math.Abs(travel.Y)).Inflated(margin + _horizonTicks * .08f);
                if (reach.Intersects(swept)) _relevantThreats.Add(threat);
            }
            var sampleCount = _horizonTicks / _stepTicks;
            var required = sampleCount * _relevantThreats.Count;
            if (_threatSteps.Length < required)
                _threatSteps = new ThreatStep[((required + 1023) / 1024) * 1024];
            if (_settings.CacheThreatPrediction)
                for (var sample = 0; sample < sampleCount; sample++)
                    for (var threat = 0; threat < _relevantThreats.Count; threat++)
                        _threatSteps[sample * _relevantThreats.Count + threat] =
                            PredictThreat(_relevantThreats[threat], (sample + 1) * _stepTicks, directive);
            var beamRequired = sampleCount * _relevantBeams.Count;
            if (_beamSteps.Length < beamRequired) _beamSteps = new BeamStep[((beamRequired + 63) / 64) * 64];
            if (_settings.CacheThreatPrediction)
                for (var sample = 0; sample < sampleCount; sample++)
                    for (var beam = 0; beam < _relevantBeams.Count; beam++)
                        _beamSteps[sample * _relevantBeams.Count + beam] = PredictBeam(_relevantBeams[beam], (sample + 1) * _stepTicks);
        }

        private BeamStep PredictBeam(in ThreatSnapshot threat, int tick)
        {
            return new BeamStep
            {
                Shape = BeamGeometry.Sweep(threat, tick - _stepTicks, tick),
                DamageRisk = (_settings.DamagePenalty + Math.Max(1, threat.Damage) * 90f) * (1f / (1f + tick * .035f))
            };
        }

        private ThreatStep PredictThreat(ThreatSnapshot threat, int tick, BossDirective directive)
        {
            if (threat.TimeLeft > 0 && threat.TimeLeft <= tick - _stepTicks) return default(ThreatStep);
            var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                _settings.ContactSafetyMargin + directive.ExtraContactMargin;
            var aliveUntil = threat.TimeLeft > 0 ? Math.Min(tick, threat.TimeLeft) : tick;
            return new ThreatStep
            {
                Active = true,
                LifeFraction = (aliveUntil - tick + _stepTicks) / (float)_stepTicks,
                Before = threat.BoundsAt(tick - _stepTicks).Inflated(margin + tick * .08f),
                After = threat.BoundsAt(aliveUntil).Inflated(margin + tick * .08f),
                DamageRisk = (_settings.DamagePenalty + Math.Max(1, threat.Damage) * 90f) * (1f / (1f + tick * .035f))
            };
        }

        private static bool SweptIntersects(in RectF playerBefore, in RectF playerAfter, in RectF threatBefore, in RectF threatAfter)
        {
            if (playerBefore.Intersects(threatBefore) || playerAfter.Intersects(threatAfter)) return true;
            var x = playerBefore.Center.X - threatBefore.Center.X;
            var y = playerBefore.Center.Y - threatBefore.Center.Y;
            var dx = playerAfter.X - playerBefore.X - (threatAfter.X - threatBefore.X);
            var dy = playerAfter.Y - playerBefore.Y - (threatAfter.Y - threatBefore.Y);
            var entry = 0f;
            var exit = 1f;
            return ClipSweep(x, dx, (playerBefore.Width + threatBefore.Width) * .5f, ref entry, ref exit) &&
                   ClipSweep(y, dy, (playerBefore.Height + threatBefore.Height) * .5f, ref entry, ref exit);
        }

        private static bool ClipSweep(float origin, float delta, float radius, ref float entry, ref float exit)
        {
            if (Math.Abs(delta) < .00001f) return Math.Abs(origin) <= radius;
            var first = (-radius - origin) / delta;
            var second = (radius - origin) / delta;
            if (first > second) { var swap = first; first = second; second = swap; }
            entry = Math.Max(entry, first);
            exit = Math.Min(exit, second);
            return entry <= exit;
        }

        private bool OutsideWorldOrArena(CombatSnapshot snapshot, RectF bounds)
        {
            var player = snapshot.Player;
            if (bounds.Left < player.WorldLeft || bounds.Right > player.WorldRight ||
                bounds.Top < player.WorldTop || bounds.Bottom > player.WorldBottom)
                return true;
            var arena = snapshot.Arena.LocalOpenBounds;
            if (arena.Width > 0f && (bounds.Left < arena.Left || bounds.Right > arena.Right)) return true;
            // Up/down scan extents are not horizontal collision planes. Only the
            // actually verified flat solid tile rows can obstruct this footprint.
            return IntersectsSolidSupport(snapshot.Arena.FloorSupport, bounds) ||
                IntersectsSolidSupport(snapshot.Arena.CeilingSupport, bounds) ||
                IntersectsSolidSupport(snapshot.Arena.RecoverySupport, bounds);
        }

        private static bool IntersectsSolidSupport(SupportSpan support, RectF bounds)
        {
            if (!support.Valid || support.OneWay) return false;
            var row = new RectF(support.Left, support.SurfaceY - (support.Inverted ? 16f : 0f),
                support.Right - support.Left, 16f);
            return row.Intersects(bounds);
        }

        private float ImmediateRisk(CombatSnapshot snapshot, BossDirective directive)
        {
            var bounds = snapshot.Player.BoundsAt(snapshot.Player.Position);
            var ticks = Math.Max(1, Math.Min(_horizonTicks, _settings.ImmediateThreatTicks));
            var playerFuture = snapshot.Player.BoundsAt(snapshot.Player.Position + snapshot.Player.Velocity * ticks);
            var risk = 0f;
            for (var i = 0; i < _relevantThreats.Count; i++)
            {
                var threat = _relevantThreats[i];
                var margin = threat.Kind == ThreatKind.Projectile ? _settings.ProjectileSafetyMargin :
                    _settings.ContactSafetyMargin + directive.ExtraContactMargin;
                var aliveUntil = threat.TimeLeft > 0 ? Math.Min(ticks, threat.TimeLeft) : ticks;
                var future = threat.BoundsAt(aliveUntil).Inflated(margin);
                var livePlayerFuture = aliveUntil == ticks ? playerFuture :
                    snapshot.Player.BoundsAt(snapshot.Player.Position + snapshot.Player.Velocity * aliveUntil);
                if (SweptIntersects(bounds, livePlayerFuture, threat.BoundsAt(0).Inflated(margin), future))
                    risk += _settings.DamagePenalty + threat.Damage * 90f;
            }
            var playerSweep = BeamGeometry.Union(bounds, playerFuture);
            for (var i = 0; i < _relevantBeams.Count; i++)
            {
                var threat = _relevantBeams[i];
                var beam = BeamGeometry.Sweep(threat, 0, ticks);
                if (BeamGeometry.Intersects(playerSweep, beam, _settings.ProjectileSafetyMargin))
                    risk += _settings.DamagePenalty + threat.Damage * 90f;
            }
            return risk;
        }

        private void ApplyMobilityTools(CombatSnapshot snapshot, BossDirective directive, TacticalMode mode, ref ControlPlan plan)
        {
            if (ApplyGrappleRelease(snapshot, ref plan)) return;
            var emergency = mode == TacticalMode.EmergencyEvade || mode == TacticalMode.RecoverToPattern;
            if (directive.AllowHook && snapshot.Mobility.HasGrapple && !snapshot.Mobility.Grappling &&
                _hookCooldown == 0 && (emergency || _restoringFlight && !snapshot.Player.OnGround) &&
                snapshot.Arena.GrappleAnchors.Count > 0)
            {
                Vec2 anchor;
                if (TryPickHookAnchor(snapshot, plan.Horizontal, out anchor))
                {
                    plan.Hook = true;
                    plan.HookWorld = anchor;
                    _hookCooldown = Math.Max(12, _settings.MobilityActionCooldownTicks);
                }
            }
            var flipClearance = snapshot.Mobility.GravityInverted ? snapshot.Arena.ClearanceDown : snapshot.Arena.ClearanceUp;
            if (directive.AllowGravityFlip && snapshot.Mobility.CanFlipGravity && emergency && !plan.Hook &&
                _gravityCooldown == 0 && flipClearance > _settings.ArenaVerticalMarginPixels * 2f)
            {
                plan.GravityControl = snapshot.Mobility.GravityInverted ? -1 : 1;
                _gravityCooldown = Math.Max(18, _settings.MobilityActionCooldownTicks);
            }

            if (snapshot.Mobility.HasUsableMount && !snapshot.Mobility.MountActive && _mountCooldown == 0 &&
                !plan.Hook && plan.GravityControl == 0 &&
                (snapshot.Mobility.MountRunSpeed > snapshot.Player.MaxRunSpeed * 1.1f ||
                 snapshot.Mobility.MountCanFly && (_restoringFlight || snapshot.Player.WingTime <= 0f && plan.Jump)))
            {
                plan.ToggleMount = true;
                _mountCooldown = Math.Max(18, _settings.MobilityActionCooldownTicks);
            }
        }

        private bool ApplyGrappleRelease(CombatSnapshot snapshot, ref ControlPlan plan)
        {
            if (!snapshot.Mobility.Grappling)
            {
                _grappleAttachedTicks = _grappleReleaseTicks = 0;
                return false;
            }
            _grappleAttachedTicks++;
            plan.Hook = plan.Drop = plan.Dash = plan.ToggleMount = false;
            plan.GravityControl = 0;
            _hookCooldown = Math.Max(_hookCooldown, Math.Max(12, _settings.MobilityActionCooldownTicks));
            var reached = snapshot.Player.OnGround || snapshot.Player.Velocity.LengthSquared < 4f ||
                snapshot.Mobility.FlightResourceFraction >= _settings.FlightResumeFraction;
            if (_grappleReleaseTicks == 0 && !reached && _grappleAttachedTicks < 8)
            {
                plan.Jump = false; // Allow a short pull/reset before detaching.
                return true;
            }
            // Native GrappleMovement requires controlJump && releaseJump. A held
            // jump may have releaseJump=false forever; one release frame arms it.
            // Keep retrying until observation confirms detachment, including during
            // emergency/recovery and while waiting without any nearby threats.
            _grappleReleaseTicks++;
            plan.Jump = (_grappleReleaseTicks & 1) == 0;
            return true;
        }

        private bool TryPickHookAnchor(CombatSnapshot snapshot, int horizontal, out Vec2 best)
        {
            var player = snapshot.Player.Center;
            best = default(Vec2);
            var bestScore = float.MaxValue;
            for (var i = 0; i < snapshot.Arena.GrappleAnchors.Count; i++)
            {
                var anchor = snapshot.Arena.GrappleAnchors[i];
                var delta = anchor - player;
                var reach = Math.Max(0f, snapshot.Mobility.GrappleRangePixels);
                if (delta.LengthSquared > reach * reach) continue;
                var unsafeAnchor = false;
                var landing = new RectF(anchor.X - snapshot.Player.Width * .5f,
                    anchor.Y - snapshot.Player.Height * .5f, snapshot.Player.Width, snapshot.Player.Height);
                for (var j = 0; j < _relevantThreats.Count; j++)
                {
                    var threat = _relevantThreats[j];
                    if (landing.Intersects(threat.BoundsAt(12).Inflated(_settings.ContactSafetyMargin + 32f)))
                    { unsafeAnchor = true; break; }
                }
                for (var j = 0; !unsafeAnchor && j < _relevantBeams.Count; j++)
                {
                    var beam = BeamGeometry.Sweep(_relevantBeams[j], 0, 12);
                    if (BeamGeometry.Intersects(landing, beam, _settings.ProjectileSafetyMargin)) unsafeAnchor = true;
                }
                if (unsafeAnchor) continue;
                var score = delta.LengthSquared;
                if (horizontal != 0 && Math.Sign(delta.X) != horizontal)
                    score += 90000f;
                if (delta.Y > 180f)
                    score += 70000f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = anchor;
                }
            }
            return bestScore < float.MaxValue;
        }

        private void ApplyConsumables(CombatSnapshot snapshot, ref ControlPlan plan)
        {
            plan.QuickHeal = snapshot.Player.MaxLife > 0 && snapshot.Player.Life <= snapshot.Player.MaxLife * _settings.HealAtLifeFraction;
            plan.QuickMana = snapshot.Player.MaxMana > 0 && snapshot.Player.Mana <= snapshot.Player.MaxMana * _settings.ManaAtFraction;
        }

        private void RememberPlan(ControlPlan plan)
        {
            _lastHorizontal = plan.Horizontal;
        }

        private static TargetSnapshot SelectPrimaryTarget(CombatSnapshot snapshot)
        {
            var player = snapshot.Player.Center;
            var best = snapshot.Targets[0];
            var score = TargetScore(best, player);
            for (var i = 1; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                var nextScore = TargetScore(candidate, player);
                if (nextScore < score) { score = nextScore; best = candidate; }
            }
            return best;
        }

        private static bool CanFireAt(CombatSnapshot snapshot, TargetSnapshot target)
        {
            return target.Life > 0 && !target.Invulnerable && snapshot.Weapon.IsUsable && snapshot.Weapon.HasAmmo &&
                (target.LineOfSightKnown ? target.HasLineOfSight : snapshot.LineOfSightToPrimary);
        }

        private static bool WeaponProfileReady(CombatSnapshot snapshot, out string reason)
        {
            var weapon = snapshot?.Weapon;
            if (weapon != null && weapon.NativeProfileRequired && weapon.Profile.Status != WeaponProfileStatus.Supported)
            {
                reason = weapon.Profile.Reason;
                return false;
            }
            reason = null;
            return true;
        }

        private static void ApplyWeaponAim(CombatSnapshot snapshot, TargetSnapshot target, bool requested, ref ControlPlan plan)
        {
            if (snapshot.Weapon.NativeProfileRequired)
            {
                var shot = WeaponAimSolver.Solve(snapshot.Weapon.Profile, snapshot.Player.Center, target.Center, target.Velocity);
                plan.AimWorld = shot.AimWorld;
                plan.Fire = requested && shot.CanFire && CanFireAt(snapshot, target);
                // Report unsupported equipment, not ordinary momentary LOS/range
                // misses. Never emit a message every frame or interrupt a Boss.
                if (snapshot.Weapon.Profile.Status != WeaponProfileStatus.Supported)
                    plan.WeaponIssue = snapshot.Weapon.Profile.Reason;
            }
            else
            {
                plan.Fire = requested && CanFireAt(snapshot, target);
                plan.AimWorld = InterceptSolver.PredictAim(snapshot.Player.Center, target.Center, target.Velocity,
                    snapshot.Weapon.IsProjectile ? snapshot.Weapon.ShootSpeed : 0f);
            }
        }

        private static float TargetScore(TargetSnapshot target, Vec2 player)
        {
            var score = Vec2.DistanceSquared(target.Center, player);
            if (target.Boss) score -= 1000000f;
            if (!target.Chaseable || target.Invulnerable) score += 3000000f;
            if (target.LifeMax > 0) score += target.Life / (float)target.LifeMax * 1500f;
            return score;
        }

        private static int ArenaCenterDirection(CombatSnapshot snapshot)
        {
            var center = snapshot.Arena.SafeCenter;
            if (center.X <= 0f)
                return 0;
            var delta = center.X - snapshot.Player.Center.X;
            return Math.Abs(delta) < 48f ? 0 : Math.Sign(delta);
        }

        private static int AwayX(Vec2 player, Vec2 target) => player.X >= target.X ? 1 : -1;
        private static int ClampIntent(int value) => value < 0 ? -1 : value > 0 ? 1 : 0;

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private struct Candidate
        {
            public int Horizontal;
            public int Posture;
            public float Score;
            public float Hazard;
        }

        private struct ThreatStep
        {
            public RectF Before;
            public RectF After;
            public float LifeFraction;
            public float DamageRisk;
            public bool Active;
        }

        private struct BeamStep
        {
            public BeamSample Shape;
            public float DamageRisk;
        }
    }
}
