using System;
using System.Collections.Generic;
using System.Linq;

namespace Chaite.Core
{
    public enum BossPattern
    {
        HorizontalKite,
        EllipseOrbit,
        CircleOrbit,
        Runway,
        StayCloseJump,
        PerpendicularDashDodge,
        ProjectileLanes,
        Composite
    }

    public sealed class BossRequirements
    {
        public float MinimumHorizontalClearance;
        public float MinimumVerticalClearance;
        public float MinimumRunSpeed;
        public float MinimumWeaponDps;
        public bool RequiresAirMobility;
        public bool RequiresDashOrHook;

        public bool IsMet(CombatSnapshot snapshot, out string reason)
        {
            var scale = snapshot.Difficulty.Zenith ? 1.20f : snapshot.Difficulty.ForTheWorthy ? 1.14f :
                snapshot.Difficulty.Master ? 1.08f : 1f;
            var horizontal = MinimumHorizontalClearance * scale;
            var vertical = MinimumVerticalClearance * scale;
            var speed = MinimumRunSpeed + (snapshot.Difficulty.Zenith ? .8f : snapshot.Difficulty.ForTheWorthy ? .5f :
                snapshot.Difficulty.Master ? .3f : 0f);
            if (snapshot.Arena.HorizontalClearance < horizontal)
            {
                reason = "横向有效场地不足 " + (int)horizontal + " 像素";
                return false;
            }
            if (snapshot.Arena.VerticalClearance < vertical)
            {
                reason = "纵向有效场地不足 " + (int)vertical + " 像素";
                return false;
            }
            if (snapshot.Player.MaxRunSpeed < speed && snapshot.Mobility.MountRunSpeed < speed)
            {
                reason = "奔跑/坐骑速度不足";
                return false;
            }
            if (snapshot.Weapon == null || !snapshot.Weapon.IsUsable || !snapshot.Weapon.HasAmmo || snapshot.Weapon.ApproximateDps < MinimumWeaponDps)
            {
                reason = "快捷栏可用武器的基础输出不足 " + (int)MinimumWeaponDps + " DPS";
                return false;
            }
            if (!snapshot.Weapon.IsProjectile)
            {
                reason = "当前风筝策略不支持纯挥砍武器，请在快捷栏准备远程武器";
                return false;
            }
            if (RequiresAirMobility && snapshot.Mobility.FlightResourceFraction <= 0f && !snapshot.Mobility.MountCanFly && !snapshot.Mobility.CanFlipGravity)
            {
                reason = "缺少翅膀、飞行坐骑或重力控制";
                return false;
            }
            if (RequiresDashOrHook && !snapshot.Mobility.CanDash && !snapshot.Mobility.HasGrapple)
            {
                reason = "缺少冲刺或钩爪脱困手段";
                return false;
            }
            reason = null;
            return true;
        }
    }

    public struct BossDirective
    {
        public BossPattern Pattern;
        public string StrategyId;
        public string PhaseId;
        public float IdealDistance;
        public float VerticalOffset;
        // Optional minimum distance above a real, locally observed floor. The
        // planner's flight-recovery budget still takes precedence over ascent.
        public float FloorClearance;
        public int HorizontalIntent;
        public int VerticalIntent;
        // Zero is an intentional coast/no-jump command for source-specific
        // controllers, not an invitation to infer a generic orbit direction.
        public bool UseExplicitMovement;
        public JumpAction JumpAction;
        public bool PreferDash;
        public bool AllowHook;
        public bool AllowGravityFlip;
        public bool ForceContinuousMovement;
        public bool Fire;
        public float ExtraContactMargin;
    }

    public sealed class BossMemory
    {
        public string StrategyId;
        public string PhaseId;
        public int PhaseTicks;
        public int OrbitDirection = 1;
        public int DashCounter;
        public bool DashActive;
        public int DirectionHoldTicks;
        public float PreviousSpeed;
        public int PreviousTargetKey = -1;

        public void Enter(string strategy, string phase)
        {
            if (StrategyId != strategy)
            {
                DashCounter = 0;
                DashActive = false;
                DirectionHoldTicks = 0;
                PreviousSpeed = 0f;
                PreviousTargetKey = -1;
            }
            if (StrategyId != strategy || PhaseId != phase)
            {
                StrategyId = strategy;
                PhaseId = phase;
                PhaseTicks = 0;
            }
            else
            {
                PhaseTicks++;
            }
        }
    }

    public sealed class BossDecision
    {
        public TargetSnapshot Target;
        public TargetSnapshot PatternTarget;
        public BossDirective Directive;
        public BossRequirements Requirements;
    }

    public interface IBossStrategy
    {
        string Id { get; }
        BossRequirements Requirements { get; }
        bool Matches(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty);
        BossDecision Evaluate(CombatSnapshot snapshot, BossMemory memory);
    }

    public sealed class BossStrategyEngine
    {
        private readonly List<IBossStrategy> _strategies;
        private readonly BossMemory[] _memories;
        private readonly List<TargetSnapshot> _bosses = new List<TargetSnapshot>(200);

        public BossStrategyEngine()
        {
            _strategies = new List<IBossStrategy>
            {
                new MechdusaStrategy(), new MechanicalMayhemStrategy(), new TwinsStrategy(),
                new KingSlimeStrategy(), new EyeStrategy(), new EaterStrategy(), new BrainStrategy(),
                new QueenBeeStrategy(), new SkeletronStrategy(), new DeerclopsStrategy(), new WallStrategy(),
                new QueenSlimeStrategy(), new DestroyerStrategy(), new PrimeStrategy(), new PlanteraStrategy(),
                new GolemStrategy(), new FishronStrategy(), new EmpressStrategy(), new CultistStrategy(),
                new MoonLordStrategy(), new CompositeBossStrategy()
            };
            _memories = new BossMemory[_strategies.Count];
            for (var i = 0; i < _memories.Length; i++) _memories[i] = new BossMemory();
        }

        public BossDecision Evaluate(CombatSnapshot snapshot)
        {
            var bosses = CollectBosses(snapshot);
            if (bosses.Count == 0)
                return null;
            var strategy = SelectStrategy(bosses, snapshot.Difficulty);
            if (strategy == null)
                return null;
            var memory = _memories[_strategies.IndexOf(strategy)];
            // Phase transitions must not erase the attack-cycle counter.
            if (memory.StrategyId == null) memory.StrategyId = strategy.Id;
            var decision = strategy.Evaluate(snapshot, memory);
            memory.Enter(decision.Directive.StrategyId, decision.Directive.PhaseId);
            memory.PreviousSpeed = decision.Target.Velocity.Length;
            memory.PreviousTargetKey = decision.Target.Key;
            return decision;
        }

        public BossRequirements RequirementsFor(CombatSnapshot snapshot)
        {
            if (snapshot == null)
                return null;
            var bosses = CollectBosses(snapshot);
            if (bosses.Count == 0)
                return null;
            var strategy = SelectStrategy(bosses, snapshot.Difficulty);
            return strategy?.Requirements;
        }

        public BossRequirements RequirementsForExpected(DifficultySnapshot difficulty, params int[] bossTypes)
        {
            if (bossTypes == null || bossTypes.Length == 0)
                return null;
            var bosses = bossTypes.Select((type, index) => new TargetSnapshot
            {
                Key = index,
                Type = type,
                Boss = true,
                Life = 1,
                LifeMax = 1,
                Chaseable = true
            }).ToList();
            var strategy = SelectStrategy(bosses, difficulty ?? new DifficultySnapshot());
            return strategy?.Requirements;
        }

        private IBossStrategy SelectStrategy(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty)
        {
            var family = BossFamily(bosses[0].Type);
            for (var i = 1; i < bosses.Count; i++)
                if (BossFamily(bosses[i].Type) != family)
                    return _strategies[_strategies.Count - 1];
            for (var i = 0; i < _strategies.Count - 1; i++)
                if (_strategies[i].Matches(bosses, difficulty)) return _strategies[i];
            return null;
        }

        private IList<TargetSnapshot> CollectBosses(CombatSnapshot snapshot)
        {
            _bosses.Clear();
            for (var i = 0; i < snapshot.Targets.Count; i++)
                if (snapshot.Targets[i].Boss && snapshot.Targets[i].Life > 0) _bosses.Add(snapshot.Targets[i]);
            return _bosses;
        }

        private static int BossFamily(int type)
        {
            if (MechanicalFamilies.IsTwin(type) || MechanicalFamilies.IsDestroyer(type) || MechanicalFamilies.IsPrime(type))
                return 1000;
            if (type >= 13 && type <= 15) return 13;
            if (type == 113 || type == 114) return 113;
            if (type >= 245 && type <= 249) return 245;
            if (type >= 396 && type <= 398) return 396;
            return type;
        }

        public void Reset()
        {
            _bosses.Clear();
            for (var i = 0; i < _memories.Length; i++)
            {
                var memory = _memories[i];
                memory.StrategyId = null;
                memory.PhaseId = null;
                memory.PhaseTicks = 0;
                memory.OrbitDirection = 1;
                memory.DashCounter = 0;
                memory.DashActive = false;
                memory.DirectionHoldTicks = 0;
                memory.PreviousSpeed = 0f;
                memory.PreviousTargetKey = -1;
            }
        }
    }

    internal abstract class BossStrategyBase : IBossStrategy
    {
        private readonly Dictionary<string, string[]> _phaseNames = new Dictionary<string, string[]>(StringComparer.Ordinal);
        protected BossStrategyBase(string id, float horizontal, float vertical, float speed, bool air = false, bool dashOrHook = false)
        {
            Id = id;
            Requirements = new BossRequirements
            {
                MinimumHorizontalClearance = horizontal,
                MinimumVerticalClearance = vertical,
                MinimumRunSpeed = speed,
                MinimumWeaponDps = MinimumDps(id),
                RequiresAirMobility = air,
                RequiresDashOrHook = dashOrHook
            };
        }

        public string Id { get; }
        public BossRequirements Requirements { get; }
        public abstract bool Matches(IList<TargetSnapshot> bosses, DifficultySnapshot difficulty);
        public abstract BossDecision Evaluate(CombatSnapshot snapshot, BossMemory memory);

        protected BossDecision Decision(CombatSnapshot snapshot, TargetSnapshot target, string phase, BossPattern pattern,
            float distance, float vertical, int horizontalIntent = 0, int verticalIntent = 0, bool dash = false,
            bool hook = true, bool gravity = false, float margin = 28f, bool fire = true, TargetSnapshot? patternTarget = null)
        {
            return new BossDecision
            {
                Target = target,
                PatternTarget = patternTarget ?? target,
                Requirements = Requirements,
                Directive = new BossDirective
                {
                    StrategyId = Id,
                    PhaseId = VariantPhase(snapshot.Difficulty, phase),
                    Pattern = pattern,
                    IdealDistance = distance + VariantDistance(snapshot.Difficulty),
                    VerticalOffset = vertical,
                    HorizontalIntent = horizontalIntent,
                    VerticalIntent = verticalIntent,
                    PreferDash = dash,
                    AllowHook = hook,
                    AllowGravityFlip = gravity,
                    ForceContinuousMovement = true,
                    Fire = fire && !target.Invulnerable,
                    ExtraContactMargin = margin + VariantMargin(snapshot.Difficulty)
                }
            };
        }

        protected static TargetSnapshot Pick(CombatSnapshot snapshot, int type1, int type2 = -1, int type3 = -1, int type4 = -1)
        {
            var selected = default(TargetSnapshot);
            var bestScore = float.MaxValue;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < snapshot.Targets.Count; i++)
                {
                    var target = snapshot.Targets[i];
                    if (target.Life <= 0 || (pass == 0
                        ? target.Type != type1 && target.Type != type2 && target.Type != type3 && target.Type != type4
                        : !target.Boss)) continue;
                    var score = Vec2.DistanceSquared(target.Center, snapshot.Player.Center);
                    if (bestScore == float.MaxValue || selected.Invulnerable && !target.Invulnerable ||
                        target.Invulnerable == selected.Invulnerable && score < bestScore)
                    {
                        selected = target;
                        bestScore = score;
                    }
                }
                if (bestScore != float.MaxValue) break;
            }
            return selected;
        }

        protected static float Life(TargetSnapshot target) => target.LifeMax > 0 ? target.Life / (float)target.LifeMax : 1f;
        protected static int AwayX(PlayerSnapshot player, TargetSnapshot target) => player.Center.X >= target.Center.X ? 1 : -1;
        protected static int PerpendicularY(PlayerSnapshot player, TargetSnapshot target) => player.Center.Y >= target.Center.Y ? -1 : 1;
        protected static bool HasThreat(CombatSnapshot snapshot, int type1, int type2 = -1)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
                if (snapshot.Threats[i].Kind == ThreatKind.Projectile &&
                    (snapshot.Threats[i].Type == type1 || snapshot.Threats[i].Type == type2)) return true;
            return false;
        }

        protected static bool HasNpcThreat(CombatSnapshot snapshot, int type)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
                if (snapshot.Threats[i].Kind == ThreatKind.NpcContact && snapshot.Threats[i].Type == type) return true;
            return false;
        }

        protected static bool HasType(IList<TargetSnapshot> targets, int type, int lastType = -1)
        {
            for (var i = 0; i < targets.Count; i++)
                if (targets[i].Life > 0 && (lastType < 0 ? targets[i].Type == type :
                    targets[i].Type >= type && targets[i].Type <= lastType)) return true;
            return false;
        }

        protected static TargetSnapshot LowestLife(CombatSnapshot snapshot, int firstType, int lastType, out bool found)
        {
            var selected = default(TargetSnapshot);
            found = false;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Type < firstType || target.Type > lastType || target.Life <= 0 || target.Invulnerable) continue;
                if (!found || target.Life < selected.Life) { selected = target; found = true; }
            }
            return selected;
        }

        protected static TargetSnapshot MechanicalPriority(CombatSnapshot snapshot)
        {
            var selected = default(TargetSnapshot);
            var best = int.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                if (target.Life <= 0) continue;
                var priority = target.Type == 126 ? 0 : target.Type == 127 ? 1 :
                    target.Type == 125 ? 2 : target.Type == 134 ? 3 : int.MaxValue;
                if (priority == int.MaxValue) continue;
                if (target.Invulnerable) priority += 10;
                if (priority < best) { selected = target; best = priority; }
            }
            return best == int.MaxValue ? Pick(snapshot, 125, 126, 127, 134) : selected;
        }

        protected static int CompositeEscape(CombatSnapshot snapshot, BossMemory memory)
        {
            var left = EscapeScore(snapshot, -1);
            var right = EscapeScore(snapshot, 1);
            var preferred = right >= left ? 1 : -1;
            var held = memory.OrbitDirection;
            var heldScore = held > 0 ? right : left;
            var nextScore = preferred > 0 ? right : left;
            // Keep the corridor unless a competing boss actually closes it; tiny
            // fluctuations in health/target selection must not reverse the whole train.
            if (memory.PhaseTicks == 0 || nextScore > heldScore * 1.25f + 18000f)
                memory.OrbitDirection = preferred;
            return memory.OrbitDirection;
        }

        private static float EscapeScore(CombatSnapshot snapshot, int direction)
        {
            var speed = Math.Max(snapshot.Player.MaxRunSpeed,
                snapshot.Mobility.MountActive ? snapshot.Mobility.MountRunSpeed : 0f);
            var clearance = direction > 0 ? snapshot.Arena.ClearanceRight : snapshot.Arena.ClearanceLeft;
            var distance = Math.Min(speed * 72f, Math.Max(0f, clearance - 128f));
            var future = snapshot.Player.Center + new Vec2(direction * distance, 0);
            var worst = float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var boss = snapshot.Targets[i];
                if (!boss.Boss || boss.Life <= 0) continue;
                // Short threat extrapolation plus a longer player escape corridor.
                // Boss AI can turn: this is a risk envelope, not an exact future replay.
                var danger = boss.Center + boss.Velocity * 18f;
                worst = Math.Min(worst, Vec2.DistanceSquared(future, danger));
            }
            return Math.Min(worst, 4000000f) + Math.Min(clearance, 1400f) * 120f;
        }

        private static float MinimumDps(string id)
        {
            switch (id)
            {
                case "king-slime": return 12f;
                case "eye-of-cthulhu": return 18f;
                case "eater-of-worlds":
                case "brain-of-cthulhu": return 20f;
                case "queen-bee": return 25f;
                case "skeletron": return 30f;
                case "deerclops": return 22f;
                case "wall-of-flesh": return 55f;
                case "queen-slime": return 65f;
                case "twins":
                case "destroyer":
                case "skeletron-prime": return 90f;
                case "plantera": return 110f;
                case "golem": return 120f;
                case "duke-fishron": return 150f;
                case "empress-of-light": return 170f;
                case "lunatic-cultist": return 160f;
                case "moon-lord": return 220f;
                case "mechanical-mayhem": return 220f;
                case "mechdusa": return 260f;
                case "multi-boss-composite": return 200f;
                default: return 20f;
            }
        }

        private string VariantPhase(DifficultySnapshot difficulty, string phase)
        {
            string[] names;
            if (!_phaseNames.TryGetValue(phase, out names))
            {
                names = new[] { "classic-" + phase, "expert-" + phase, "master-" + phase,
                    "ftw-" + phase, "zenith-" + phase, "remix-" + phase };
                _phaseNames.Add(phase, names);
            }
            return names[difficulty.Zenith ? 4 : difficulty.ForTheWorthy ? 3 :
                difficulty.Master ? 2 : difficulty.Expert ? 1 : difficulty.Remix ? 5 : 0];
        }

        private static float VariantMargin(DifficultySnapshot difficulty)
        {
            if (difficulty.Zenith) return 30f;
            if (difficulty.ForTheWorthy) return 22f;
            if (difficulty.Master) return 14f;
            if (difficulty.Expert) return 8f;
            return 0f;
        }

        private static float VariantDistance(DifficultySnapshot difficulty)
        {
            if (difficulty.Zenith) return 100f;
            if (difficulty.ForTheWorthy) return 70f;
            if (difficulty.Master) return 45f;
            if (difficulty.Expert) return 25f;
            return 0f;
        }
    }

    internal sealed class KingSlimeStrategy : BossStrategyBase
    {
        private int _kingKey = -1;
        private int _runDirection;
        private int _crossDirection;
        private int _teleportDirection;
        private int _lastStage = -1;
        private bool _crossing;
        private bool _crossedThisJump;

        // Ground-runway policy, not the separate raised-platform/rope strategy.
        // These entry margins are conservative policy thresholds, not a victory proof.
        public KingSlimeStrategy() : base("king-slime", 1000, 280, 5.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 50);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var king = Pick(s, 50);
            var player = s.Player;
            if (m.PreviousTargetKey < 0 || _kingKey != king.Key)
            {
                _kingKey = king.Key;
                _runDirection = AwayX(player, king);
                _crossing = false;
                _crossedThisJump = false;
                _crossDirection = 0;
                _teleportDirection = 0;
                _lastStage = -1;
            }
            var stage = (int)king.Ai1;
            var dx = player.Center.X - king.Center.X;
            var contact = (player.Width + king.Width) * .5f + 32f;
            var gap = Math.Abs(dx);
            var ideal = 340f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(180f, s.Weapon.Profile.ConservativeRangePixels * .65f));
            var target = SelectPressureTarget(s, king);
            string phase;
            int horizontal;

            // Native ai[1], NOT stationary velocity or dontTakeDamage, identifies
            // the 60-tick shrink / 30-tick reform stages in singleplayer.
            if (stage == 5)
            {
                _crossing = false;
                if (_lastStage != 5)
                {
                    _teleportDirection = _runDirection;
                    if (king.LocalAiKnown)
                    {
                        var destinationDelta = player.Center.X - king.LocalAi1;
                        if (Math.Abs(destinationDelta) > 8f)
                            _teleportDirection = Math.Sign(destinationDelta);
                    }
                }
                phase = king.LocalAiKnown ? "teleport-leave-known-destination" : "teleport-destination-unobserved";
                horizontal = KeepInsideObservedLane(s, _teleportDirection);
                _runDirection = _teleportDirection;
            }
            else if (stage == 6)
            {
                _crossing = false;
                if (_lastStage != 6 || gap > contact)
                    _runDirection = AwayX(player, king);
                phase = "teleport-reform-regain-gap";
                horizontal = gap < ideal ? KeepInsideObservedLane(s, _runDirection) : 0;
            }
            else
            {
                var airborne = Math.Abs(king.Velocity.Y) > .1f;
                // ai[1] resets to zero at the HIGH launch, ai[0] to -200. A
                // newly spawned falling King with ai[1]==0 is not a high jump.
                var highJump = airborne && stage == 0 && king.Ai0 <= -150f;
                if (!highJump) _crossedThisJump = false;
                if (_crossing)
                {
                    if (dx * _crossDirection >= contact)
                    {
                        _runDirection = _crossDirection;
                        _crossing = false;
                    }
                    else if (Math.Abs(dx) > contact && !CanRunUnder(s, king, _crossDirection, false))
                    {
                        // Before entering the hitbox corridor, a lost opening is
                        // cancellable. Once underneath, keep the committed exit;
                        // the immediate collision layer can still override it.
                        _crossing = false;
                    }
                }
                if (!_crossing && gap > contact)
                    _runDirection = AwayX(player, king);
                var remainingLane = LaneRemaining(s, _runDirection);
                if (!_crossing && !_crossedThisJump && highJump && (remainingLane < 650f || gap < contact + 80f) &&
                    CanRunUnder(s, king, -_runDirection, true))
                {
                    _crossDirection = -_runDirection;
                    _crossing = true;
                    _crossedThisJump = true;
                }
                if (_crossing)
                {
                    phase = "high-jump-committed-run-under";
                    horizontal = _crossDirection;
                }
                else
                {
                    phase = NativeHopPhase(king, airborne, highJump);
                    var predictedGap = gap - Math.Max(0f, king.Velocity.X * _runDirection) * 18f;
                    var pressure = target.Type == 535;
                    // Coast/fire at a useful range, saving the finite runway for
                    // later cycles. Do not jump merely to match Boss center Y.
                    horizontal = predictedGap < ideal || pressure ? _runDirection : 0;
                    if (remainingLane < 48f && gap > contact + 48f) horizontal = 0;
                    horizontal = KeepInsideObservedLane(s, horizontal);
                }
            }
            _lastStage = stage;
            var visible = target.LineOfSightKnown ? target.HasLineOfSight : target.Key == king.Key && s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(target.Center, player.Center) <=
                    s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            var decision = Decision(s, target, phase, BossPattern.Runway, ideal, 0f,
                horizontal, 0, false, false, false, 32f,
                visible && inRange && target.Life > 0 && target.Chaseable, king);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            // Ordinary classic/expert/master have the same native hop program.
            // Actual body dimensions and actual spawned 535s carry the differences;
            // don't inflate the useful weapon distance merely from a mode suffix.
            decision.Directive.IdealDistance = ideal;
            return decision;
        }

        private static string NativeHopPhase(TargetSnapshot king, bool airborne, bool highJump)
        {
            var stage = (int)king.Ai1;
            if (airborne)
                return highJump ? "high-jump-wait-for-clearance" : stage == 3 ? "short-hop-retreat" : "normal-hop-retreat";
            // Match native AI_015_KingSlime: conv.r4 life, conv.r4 lifeMax,
            // ldc.r4 threshold, mul, bge.un. Dividing life by lifeMax changes
            // exact boundary behavior on the supported x86/x87 runtime.
            var life = (float)king.Life;
            var maximum = (float)king.LifeMax;
            var countdown = 2f + (life < maximum * .8f ? 1f : 0f) + (life < maximum * .6f ? 1f : 0f) +
                (life < maximum * .4f ? 2f : 0f) + (life < maximum * .2f ? 3f : 0f) + (life < maximum * .1f ? 4f : 0f);
            var imminent = king.Ai0 >= -30f || -king.Ai0 <= countdown * 12f;
            if (stage == 3) return imminent ? "ground-high-windup" : "ground-high-wait";
            if (stage == 2) return imminent ? "ground-short-windup" : "ground-short-wait";
            return imminent ? "ground-normal-windup" : "ground-normal-wait";
        }

        private static TargetSnapshot SelectPressureTarget(CombatSnapshot s, TargetSnapshot king)
        {
            if (!s.Difficulty.Expert && !s.Difficulty.Master) return king;
            var result = king;
            var nearest = 240f * 240f;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var target = s.Targets[i];
                if (target.Type != 535 || target.Life <= 0 || target.Invulnerable || !target.Chaseable ||
                    !target.LineOfSightKnown || !target.HasLineOfSight) continue;
                var distance = Vec2.DistanceSquared(s.Player.Center, target.Center);
                if (distance >= nearest) continue;
                nearest = distance;
                result = target;
            }
            return result;
        }

        private static float LaneRemaining(CombatSnapshot s, int direction)
        {
            var player = s.Player;
            var clearance = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            var support = s.Arena.FloorSupport;
            if (!s.Mobility.GravityInverted && support.ContainsBody(player.Position.X, player.Width))
                clearance = Math.Min(clearance, direction > 0 ? support.Right - player.Position.X - player.Width : player.Position.X - support.Left);
            return Math.Max(0f, clearance);
        }

        private static int KeepInsideObservedLane(CombatSnapshot s, int direction)
        {
            if (direction == 0) return 0;
            // A zero command brakes instead of reversing blindly through King.
            var speed = Math.Max(0f, s.Player.Velocity.X * direction);
            // Ice/slowing effects can make native slowdown far below .08. A
            // convenience floor would dangerously invent extra braking power.
            var braking = speed * speed / (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            return LaneRemaining(s, direction) < Math.Max(20f, braking + 12f) ? 0 : direction;
        }

        private static bool CanRunUnder(CombatSnapshot s, TargetSnapshot king, int direction, bool starting)
        {
            var player = s.Player;
            var floor = s.Arena.FloorSupport;
            if (!player.OnGround || s.Mobility.GravityInverted || s.Mobility.Grappling ||
                !floor.Valid || floor.Inverted || !floor.ContainsBody(player.Position.X, player.Width) ||
                Math.Abs(floor.SurfaceY - player.Position.Y - player.Height) > 6f ||
                s.Arena.HasCeiling && s.Arena.ClearanceUp < 320f ||
                s.Difficulty.ForTheWorthy || s.Difficulty.Zenith || s.Difficulty.Remix) return false;
            var bottom = king.Position.Y + king.Height;
            if (starting && (king.Velocity.Y >= -1f || bottom > player.Position.Y - 48f)) return false;
            var x = player.Position.X;
            var vx = player.Velocity.X;
            var kingX = king.Center.X;
            var vy = king.Velocity.Y;
            var halfContact = (player.Width + king.Width) * .5f + 32f;
            var mountedSpeed = s.Mobility.MountActive ? s.Mobility.MountRunSpeed : 0f;
            // Bounded scalar rollout only: no search tree, allocations, tiles or
            // RNG. Ordinary dry King gravity is <= .3, fall speed <= 10. Ceiling
            // collisions remain an explicit unverified-world limitation.
            for (var tick = 0; tick < 72; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(player, vx, direction, true, 1, out travel, mountedSpeed);
                x += travel;
                if (!floor.ContainsBody(x, player.Width)) return false;
                kingX += king.Velocity.X;
                vy = Math.Min(10f, vy + .3f);
                bottom += vy;
                var separation = x + player.Width * .5f - kingX;
                if (Math.Abs(separation) < halfContact && bottom > player.Position.Y - 32f) return false;
                if (separation * direction >= halfContact) return true;
            }
            return false;
        }
    }

    internal sealed class EyeStrategy : BossStrategyBase
    {
        private enum NativePhase
        {
            FirstTrack, FirstLaunch, FirstCharge, FirstBrake, TransformPending,
            TransformAccelerate, TransformDecelerate, SecondTrack, SecondLaunch,
            SecondCharge, SecondBrake, FastLaunch, FastCharge, FastBrake, FastReposition, Unknown
        }

        private int _eyeKey = -1;
        private int _runDirection;
        private bool _hasChargeVector;
        private bool _holdingHop;
        private Vec2 _chargeVector;
        private float _previousAi0 = -1f;
        private float _previousAi1 = -1f;
        private float _previousAi2;
        private float _previousAi3;

        // Ground runway with boots-class running. Raised platform switching,
        // slime-mount bouncing and secret-seed templates are separate policies.
        public EyeStrategy() : base("eye-of-cthulhu", 1200, 240, 5.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 4);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var eye = Pick(s, 4);
            if (_eyeKey != eye.Key || m.PreviousTargetKey < 0)
            {
                _eyeKey = eye.Key;
                _runDirection = AwayX(s.Player, eye);
                _hasChargeVector = false;
                _holdingHop = false;
                _previousAi0 = _previousAi1 = -1f;
                _previousAi2 = _previousAi3 = 0f;
            }
            var expert = s.Difficulty.Expert || s.Difficulty.Master;
            // Unlike the transform threshold, these exact native checks use r8.
            var frantic = expert && (double)eye.Life < (double)eye.LifeMax * .12;
            var desperate = expert && (double)eye.Life < (double)eye.LifeMax * .04;
            var phase = ObservePhase(eye, expert, frantic, desperate);
            var launch = phase == NativePhase.FirstLaunch || phase == NativePhase.SecondLaunch || phase == NativePhase.FastLaunch;
            var charge = phase == NativePhase.FirstCharge || phase == NativePhase.SecondCharge || phase == NativePhase.FastCharge;
            var brake = phase == NativePhase.FirstBrake || phase == NativePhase.SecondBrake || phase == NativePhase.FastBrake;
            var transformation = phase == NativePhase.TransformPending || phase == NativePhase.TransformAccelerate || phase == NativePhase.TransformDecelerate;
            var newCharge = charge && (!_hasChargeVector || eye.Ai0 != _previousAi0 || eye.Ai1 != _previousAi1 ||
                eye.Ai2 < _previousAi2 || eye.Ai3 != _previousAi3);
            if (newCharge)
            {
                // ai1==launch still contains the preceding motion. Capture only
                // the actual post-launch vector, never a guessed random dash.
                _chargeVector = eye.Velocity;
                _hasChargeVector = true;
            }
            else if (!charge) _hasChargeVector = false;

            var remainingLane = EyeLaneRemaining(s, _runDirection);
            var speed = Math.Abs(s.Player.Velocity.X);
            var brakingDistance = speed * speed / (2f * Math.Max(.000001f, s.Player.RunSlowdown));
            var turnReserve = Math.Max(300f, brakingDistance + speed * 30f);
            var mayRecover = !launch && !charge && phase != NativePhase.Unknown;
            var turned = false;
            if (mayRecover && remainingLane < turnReserve && EyeLaneRemaining(s, -_runDirection) > remainingLane + 160f &&
                CanRecoverAcrossLane(s, eye, -_runDirection))
            {
                _runDirection = -_runDirection;
                remainingLane = EyeLaneRemaining(s, _runDirection);
                turned = true;
            }

            var ideal = 420f;
            if (s.Weapon != null && s.Weapon.NativeProfileRequired && s.Weapon.Profile.IsSupported)
                ideal = Math.Min(ideal, Math.Max(200f, s.Weapon.Profile.ConservativeRangePixels * .65f));
            var horizontal = _runDirection;
            var gap = Math.Abs(s.Player.Center.X - eye.Center.X);
            // Preserve running speed through bait and committed charges. Coast
            // only in a real recovery window when Boss is already far away, not
            // whenever generic distance tracking happens to cross one threshold.
            if (mayRecover && !turned && (gap > ideal * 1.7f || transformation && gap > ideal)) horizontal = 0;
            if (phase == NativePhase.Unknown || remainingLane < Math.Max(20f, brakingDistance + 12f)) horizontal = 0;

            var jump = JumpAction.Release;
            if (_holdingHop)
            {
                if (s.Player.Jump.Known && !s.Player.OnGround && s.Player.Jump.RemainingTicks > 0)
                    jump = JumpAction.Hold;
                else _holdingHop = false;
            }
            if (!_holdingHop && charge && _hasChargeVector && horizontal != 0 && CanStartGroundHop(s, eye, phase, expert, desperate))
            {
                _holdingHop = true;
                jump = JumpAction.Hold;
            }
            var visible = eye.LineOfSightKnown ? eye.HasLineOfSight : s.LineOfSightToPrimary;
            var inRange = s.Weapon == null || !s.Weapon.NativeProfileRequired || !s.Weapon.Profile.IsSupported ||
                Vec2.DistanceSquared(eye.Center, s.Player.Center) <= s.Weapon.Profile.ConservativeRangePixels * s.Weapon.Profile.ConservativeRangePixels;
            var decision = Decision(s, eye, PhaseName(phase), BossPattern.Runway, ideal, 0f,
                horizontal, jump == JumpAction.Hold ? 1 : 0, false, false, false, 30f,
                eye.Life > 0 && eye.Chaseable && visible && inRange);
            decision.Directive.UseExplicitMovement = true;
            decision.Directive.JumpAction = jump;
            decision.Directive.ForceContinuousMovement = horizontal != 0;
            decision.Directive.IdealDistance = ideal;
            _previousAi0 = eye.Ai0;
            _previousAi1 = eye.Ai1;
            _previousAi2 = eye.Ai2;
            _previousAi3 = eye.Ai3;
            return decision;
        }

        private static NativePhase ObservePhase(TargetSnapshot eye, bool expert, bool frantic, bool desperate)
        {
            if (eye.Ai0 == 1f) return NativePhase.TransformAccelerate;
            if (eye.Ai0 == 2f) return NativePhase.TransformDecelerate;
            if (eye.Ai0 == 0f)
            {
                // Native NPC.AI 11a4..11ca: r4 life < r4 lifeMax * r4 limit.
                // Do not replace this with a normalized ratio or expert .5.
                if (eye.Ai1 == 0f && (float)eye.Life < (float)eye.LifeMax * (expert ? .65f : .5f))
                    return NativePhase.TransformPending;
                if (eye.Ai1 == 0f) return NativePhase.FirstTrack;
                if (eye.Ai1 == 1f) return NativePhase.FirstLaunch;
                if (eye.Ai1 == 2f) return eye.Ai2 < 40f ? NativePhase.FirstCharge : NativePhase.FirstBrake;
                return NativePhase.Unknown;
            }
            if (eye.Ai0 != 3f) return NativePhase.Unknown;
            if (eye.Ai1 == 0f) return frantic ? NativePhase.FastReposition : NativePhase.SecondTrack;
            if (eye.Ai1 == 1f) return NativePhase.SecondLaunch;
            if (eye.Ai1 == 2f) return eye.Ai2 < (expert ? 50f : 40f) ? NativePhase.SecondCharge : NativePhase.SecondBrake;
            if (eye.Ai1 == 3f) return NativePhase.FastLaunch;
            if (eye.Ai1 == 4f)
                // Native ai2 holds at threshold-1 while within 200px; trusting
                // the observed timer automatically preserves that extra dash.
                return eye.Ai2 < (desperate ? 10f : 20f) ? NativePhase.FastCharge : NativePhase.FastBrake;
            if (eye.Ai1 == 5f) return NativePhase.FastReposition;
            return NativePhase.Unknown;
        }

        private static string PhaseName(NativePhase phase)
        {
            switch (phase)
            {
                case NativePhase.FirstTrack: return "first-hover-bait";
                case NativePhase.FirstLaunch: return "first-launch-prepare-exit";
                case NativePhase.FirstCharge: return "first-charge-committed-exit";
                case NativePhase.FirstBrake: return "first-brake-recover-runway";
                case NativePhase.TransformPending: return "transform-pending-recover-runway";
                case NativePhase.TransformAccelerate: return "transform-spin-up-recover";
                case NativePhase.TransformDecelerate: return "transform-spin-down-recover";
                case NativePhase.SecondTrack: return "second-track-bait";
                case NativePhase.SecondLaunch: return "second-launch-prepare-exit";
                case NativePhase.SecondCharge: return "second-charge-committed-exit";
                case NativePhase.SecondBrake: return "second-brake-recover-runway";
                case NativePhase.FastLaunch: return "fast-launch-observe-vector";
                case NativePhase.FastCharge: return "fast-charge-committed-exit";
                case NativePhase.FastBrake: return "fast-brake-recover-runway";
                case NativePhase.FastReposition: return "fast-reposition-bait";
                default: return "unrecognized-native-state";
            }
        }

        private static float EyeLaneRemaining(CombatSnapshot s, int direction)
        {
            var clearance = direction > 0 ? s.Arena.ClearanceRight : s.Arena.ClearanceLeft;
            var support = s.Arena.FloorSupport;
            if (!s.Mobility.GravityInverted && support.ContainsBody(s.Player.Position.X, s.Player.Width))
                clearance = Math.Min(clearance, direction > 0 ? support.Right - s.Player.Position.X - s.Player.Width : s.Player.Position.X - support.Left);
            return Math.Max(0f, clearance);
        }

        private static bool CanRecoverAcrossLane(CombatSnapshot s, TargetSnapshot eye, int direction)
        {
            var p = s.Player;
            var floor = s.Arena.FloorSupport;
            if (!p.OnGround || s.Mobility.GravityInverted || s.Mobility.Grappling ||
                !floor.Valid || floor.Inverted || !floor.ContainsBody(p.Position.X, p.Width) ||
                Math.Abs(floor.SurfaceY - p.Position.Y - p.Height) > 6f) return false;
            var x = p.Position.X;
            var vx = p.Velocity.X;
            for (var tick = 1; tick <= 24; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, direction, true, 1, out travel,
                    s.Mobility.MountActive ? s.Mobility.MountRunSpeed : 0f);
                x += travel;
                if (!floor.ContainsBody(x, p.Width)) return false;
                var boss = eye.Position + eye.Velocity * tick;
                if (x + p.Width + 40f > boss.X && x - 40f < boss.X + eye.Width &&
                    p.Position.Y + p.Height + 40f > boss.Y && p.Position.Y - 40f < boss.Y + eye.Height) return false;
            }
            return true;
        }

        private bool CanStartGroundHop(CombatSnapshot s, TargetSnapshot eye, NativePhase phase, bool expert, bool desperate)
        {
            var p = s.Player;
            var jump = p.Jump;
            if (!jump.Known || !jump.ReleaseReady || jump.Speed <= 0f || jump.Height <= 0 || !p.OnGround ||
                s.Mobility.GravityInverted || s.Mobility.Grappling || s.Mobility.MountActive ||
                s.Difficulty.ForTheWorthy || s.Difficulty.Zenith || s.Difficulty.Remix ||
                Math.Abs(_chargeVector.Y) > Math.Abs(_chargeVector.X) * .65f ||
                eye.Center.Y < p.Center.Y - 48f) return false;
            var floor = s.Arena.FloorSupport;
            if (!floor.ContainsBody(p.Position.X, p.Width) || floor.Inverted ||
                Math.Abs(floor.SurfaceY - p.Position.Y - p.Height) > 6f) return false;
            var straightUntil = phase == NativePhase.FirstCharge ? 40f : phase == NativePhase.SecondCharge ?
                (expert ? 50f : 40f) : (desperate ? 10f : 20f);
            // Only rely on the still-observed straight segment. The native
            // near-player fast-dash timer extension is not assumed in advance.
            var horizon = Math.Min(24, (int)Math.Max(0f, straightUntil - eye.Ai2));
            var x = p.Position.X;
            var vx = p.Velocity.X;
            var hopX = x;
            var hopY = p.Position.Y;
            var hopVx = vx;
            var hopVy = p.Velocity.Y;
            var hopState = jump;
            var groundThreat = false;
            for (var tick = 1; tick <= horizon; tick++)
            {
                float travel;
                vx = HorizontalMotion.Advance(p, vx, _runDirection, true, 1, out travel);
                x += travel;
                // Native horizontal movement precedes JumpMovement, so the
                // launch tick still earns grounded sprint acceleration.
                hopVx = HorizontalMotion.Advance(p, hopVx, _runDirection, tick == 1, 1, out travel);
                hopX += travel;
                var hold = JumpMotion.ResolveControl(true, JumpAction.Hold, in hopState, tick == 1, false);
                JumpMotion.ApplyJump(ref hopState, ref hopVy, hold, false);
                hopVy = JumpMotion.ApplyGravity(hopVy, Math.Abs(p.Gravity), p.MaxFallSpeed, false);
                hopY += hopVy;
                if (!floor.ContainsBody(x, p.Width) || !floor.ContainsBody(hopX, p.Width) ||
                    p.Position.Y - hopY + 48f > s.Arena.ClearanceUp) return false;
                var boss = eye.Position + _chargeVector * tick;
                if (hopX + p.Width + 24f > boss.X && hopX - 24f < boss.X + eye.Width &&
                    hopY + p.Height + 24f > boss.Y && hopY - 24f < boss.Y + eye.Height) return false;
                if (x + p.Width + 24f > boss.X && x - 24f < boss.X + eye.Width &&
                    p.Position.Y + p.Height + 24f > boss.Y && p.Position.Y - 24f < boss.Y + eye.Height)
                {
                    if (tick < 4) return false;
                    groundThreat = true;
                }
            }
            return groundThreat;
        }
    }

    internal sealed class EaterStrategy : BossStrategyBase
    {
        public EaterStrategy() : base("eater-of-worlds", 720, 320, 3.8f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 13, 15);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 13, 14, 15);
            var headCharge = t.Type == 13 && t.Velocity.Length > 5f;
            // VileSpitEaterOfWorlds is NPC 666; projectile 666 is a friendly sentry shot.
            var spit = HasType(s.Targets, 666) || HasNpcThreat(s, 666);
            return Decision(s, t, headCharge ? "head-emerge" : spit ? "expert-vile-spit" : "burrow-cycle",
                BossPattern.ProjectileLanes, 260, -210, -Math.Sign(t.Velocity.X), 0, headCharge, true, false, 52);
        }
    }

    internal sealed class BrainStrategy : BossStrategyBase
    {
        public BrainStrategy() : base("brain-of-cthulhu", 620, 320, 4f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 266);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var brain = Pick(s, 266);
            var phaseOne = brain.Invulnerable || HasType(s.Targets, 267);
            var target = phaseOne ? Pick(s, 267) : brain;
            return Decision(s, target, phaseOne ? "creeper-sweep" : s.Difficulty.Expert ? "mirror-teleports" : "teleports",
                phaseOne ? BossPattern.CircleOrbit : BossPattern.HorizontalKite,
                phaseOne ? 260 : 330, phaseOne ? -30 : -65, phaseOne ? 0 : AwayX(s.Player, target), 0,
                !phaseOne && Vec2.DistanceSquared(target.Center, s.Player.Center) < 180 * 180, true, false, 42, patternTarget: brain);
        }
    }

    internal sealed class QueenBeeStrategy : BossStrategyBase
    {
        public QueenBeeStrategy() : base("queen-bee", 820, 380, 4.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 222);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 222);
            var charging = Math.Abs(t.Velocity.X) > 7f;
            var enraged = t.Velocity.Length > 13f;
            return Decision(s, t, enraged ? "enraged" : charging ? "horizontal-charge" : "stinger-bee-volley",
                charging ? BossPattern.PerpendicularDashDodge : BossPattern.HorizontalKite,
                440, -100, charging ? -Math.Sign(t.Velocity.X) : AwayX(s.Player, t), charging ? PerpendicularY(s.Player, t) : 0,
                charging, true, false, enraged ? 62 : 40);
        }
    }

    internal sealed class SkeletronStrategy : BossStrategyBase
    {
        public SkeletronStrategy() : base("skeletron", 900, 430, 4.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 35);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = Pick(s, 35);
            bool hands;
            var hand = LowestLife(s, 36, 36, out hands);
            var target = s.Difficulty.Expert && hands ? hand : head;
            var spin = head.Velocity.Length > 5.5f || head.Ai1 == 1f;
            return Decision(s, target, spin ? "spin-retreat" : hands ? "remove-hands" : "skull-cycle",
                spin ? BossPattern.PerpendicularDashDodge : BossPattern.EllipseOrbit,
                spin ? 560 : 430, -90, spin ? AwayX(s.Player, head) : 0, spin ? PerpendicularY(s.Player, head) : 0,
                spin, true, false, spin ? 70 : 42, patternTarget: head);
        }
    }

    internal sealed class DeerclopsStrategy : BossStrategyBase
    {
        public DeerclopsStrategy() : base("deerclops", 620, 220, 4f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 668);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 668);
            var spikes = HasThreat(s, 961);
            var debris = HasThreat(s, 962);
            var hands = HasThreat(s, 965);
            var phase = spikes ? "ice-spike-wave" : debris ? "rubble-arc" : hands ? "expert-shadow-hands" : "close-control";
            return Decision(s, t, phase, BossPattern.StayCloseJump, 190, -95, 0, spikes ? 1 : 0,
                hands, true, false, 46);
        }
    }

    internal sealed class WallStrategy : BossStrategyBase
    {
        public WallStrategy() : base("wall-of-flesh", 1800, 170, 5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 113, 114);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 113, 114);
            var direction = AwayX(s.Player, t);
            var low = Life(t) < .25f;
            return Decision(s, t, low ? "low-health-sprint" : Life(t) < .5f ? "accelerating" : "runway",
                BossPattern.Runway, low ? 680 : 560, 0, direction, 0, low, false, false, low ? 78 : 52);
        }
    }

    internal sealed class QueenSlimeStrategy : BossStrategyBase
    {
        public QueenSlimeStrategy() : base("queen-slime", 900, 430, 5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 657);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 657);
            var phaseTwo = Life(t) < .5f || t.Ai0 >= 4f;
            var smash = HasThreat(s, 922);
            var gel = HasThreat(s, 926);
            return Decision(s, t, smash ? "queenly-smash" : gel ? "regal-gel" : phaseTwo ? "winged-phase" : "ground-phase",
                phaseTwo ? BossPattern.EllipseOrbit : BossPattern.HorizontalKite,
                phaseTwo ? 480 : 390, phaseTwo ? -120 : -35, phaseTwo ? 0 : AwayX(s.Player, t), smash ? 1 : 0,
                smash, true, false, phaseTwo ? 55 : 42);
        }
    }

    internal static class MechanicalFamilies
    {
        public static bool IsTwin(int type) => type == 125 || type == 126;
        public static bool IsDestroyer(int type) => type >= 134 && type <= 136;
        public static bool IsPrime(int type) => type >= 127 && type <= 131;
        public static int Count(IList<TargetSnapshot> bosses)
        {
            var twin = false;
            var destroyer = false;
            var prime = false;
            for (var i = 0; i < bosses.Count; i++)
            {
                var type = bosses[i].Type;
                if (IsTwin(type)) twin = true;
                else if (IsDestroyer(type)) destroyer = true;
                else if (IsPrime(type)) prime = true;
            }
            return (twin ? 1 : 0) + (destroyer ? 1 : 0) + (prime ? 1 : 0);
        }
    }

    internal sealed class TwinsStrategy : BossStrategyBase
    {
        public TwinsStrategy() : base("twins", 1300, 520, 6f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 125, 126) && MechanicalFamilies.Count(b) == 1;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = HasType(s.Targets, 126) ? Pick(s, 126) : Pick(s, 125);
            var second = Life(target) < .4f || target.Ai0 >= 1f;
            var charge = target.Velocity.Length > 10f;
            return Decision(s, target, charge ? "spaz-charge" : second ? (target.Type == 126 ? "cursed-flame" : "laser-barrage") : "paired-phase-one",
                charge ? BossPattern.PerpendicularDashDodge : BossPattern.Runway,
                second ? 650 : 560, -120, AwayX(s.Player, target), charge ? PerpendicularY(s.Player, target) : 0,
                charge, true, s.Mobility.CanFlipGravity, second ? 68 : 48);
        }
    }

    internal sealed class DestroyerStrategy : BossStrategyBase
    {
        public DestroyerStrategy() : base("destroyer", 1100, 500, 5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 134, 136) && MechanicalFamilies.Count(b) == 1;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = default(TargetSnapshot);
            var foundHead = false;
            var probeCount = 0;
            var closeProbe = false;
            var player = s.Player.Center;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (candidate.Life <= 0) continue;
                if (candidate.Type == 134 && (!foundHead ||
                    Vec2.DistanceSquared(candidate.Center, player) < Vec2.DistanceSquared(head.Center, player)))
                {
                    head = candidate;
                    foundHead = true;
                }
                else if (candidate.Type == 139 && !candidate.Invulnerable)
                {
                    var distance = Vec2.DistanceSquared(candidate.Center, player);
                    if (distance < 900f * 900f) probeCount++;
                    if (distance < 320f * 320f) closeProbe = true;
                }
            }

            var probePressure = closeProbe || probeCount > 3;
            var target = SelectExposedTarget(s, m.PreviousTargetKey, probePressure);
            // Body and tail share realLife with the head, but choosing an exposed
            // segment for damage must not hide the independently approaching head.
            var headPressure = false;
            var crossing = head.Center;
            if (foundHead && head.Velocity.LengthSquared > 8f * 8f)
            {
                var separation = head.Center - player;
                var relativeVelocity = head.Velocity - s.Player.Velocity;
                var closing = Vec2.Dot(separation, relativeVelocity);
                if (closing < 0f)
                {
                    var ticks = Math.Min(30f, -closing / Math.Max(.01f, relativeVelocity.LengthSquared));
                    headPressure = (separation + relativeVelocity * ticks).LengthSquared < 320f * 320f;
                    crossing = head.Center + head.Velocity * ticks;
                }
            }
            if (m.DirectionHoldTicks > 0) m.DirectionHoldTicks--;
            if (headPressure && m.DirectionHoldTicks == 0)
            {
                var direction = Math.Sign(player.X - crossing.X);
                if (direction != 0 && direction != m.OrbitDirection)
                {
                    m.OrbitDirection = direction;
                    m.DirectionHoldTicks = 40;
                }
            }

            var decision = Decision(s, target, headPressure ? "head-emerge" : target.Type == 139 ? "probe-clear" : "laser-lanes",
                BossPattern.ProjectileLanes, 380, -260, m.OrbitDirection, 0, headPressure, true, false, 68,
                patternTarget: foundHead ? head : target);
            // Native AI counts platforms/liquid as burrowing material too: this is
            // breathing room for evasion, never an assertion of a safe-box exploit.
            decision.Directive.FloorClearance = 320f;
            return decision;
        }

        private static TargetSnapshot SelectExposedTarget(CombatSnapshot snapshot, int previousTarget, bool probePressure)
        {
            var selected = default(TargetSnapshot);
            var bestRank = int.MaxValue;
            var bestScore = float.MaxValue;
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var candidate = snapshot.Targets[i];
                if (candidate.Life <= 0 || (candidate.Type != 134 && candidate.Type != 135 &&
                    candidate.Type != 136 && candidate.Type != 139)) continue;
                var distance = Vec2.DistanceSquared(candidate.Center, snapshot.Player.Center);
                if (candidate.Type == 139 && distance > 900f * 900f) continue;

                // Unknown is not blocked. The adapter supplies a bounded LOS cache,
                // and still performs its native final ray before actually firing.
                var rank = candidate.LineOfSightKnown ? candidate.HasLineOfSight ? 0 : 2 : 1;
                if (candidate.Invulnerable) rank += 6;
                if (!candidate.Chaseable) rank += 3;
                var score = distance;
                if (candidate.Type == 139)
                    score += probePressure ? -600f * 600f : 300f * 300f;
                // Keep a viable target while neighboring worm segments exchange
                // nearest-distance order; never retain a blocked target over clear.
                if (candidate.Key == previousTarget) score -= 100f * 100f;
                if (rank < bestRank || rank == bestRank && score < bestScore)
                {
                    selected = candidate;
                    bestRank = rank;
                    bestScore = score;
                }
            }
            return bestRank == int.MaxValue ? Pick(snapshot, 134, 135, 136) : selected;
        }
    }

    internal sealed class PrimeStrategy : BossStrategyBase
    {
        public PrimeStrategy() : base("skeletron-prime", 1150, 500, 5.5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 127, 131) && MechanicalFamilies.Count(b) == 1;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var head = Pick(s, 127);
            bool armFound;
            var arm = LowestLife(s, 129, 130, out armFound);
            if (!armFound) arm = LowestLife(s, 128, 131, out armFound);
            var target = armFound ? arm : head;
            var spin = head.Velocity.Length > 7f || head.Ai1 == 1f;
            return Decision(s, target, spin ? "prime-spin" : armFound ? "disable-melee-arms" : "head-finish",
                spin ? BossPattern.PerpendicularDashDodge : BossPattern.EllipseOrbit,
                spin ? 650 : 520, -130, spin ? AwayX(s.Player, head) : 0, spin ? PerpendicularY(s.Player, head) : 0,
                spin, true, false, spin ? 80 : 52, patternTarget: head);
        }
    }

    internal sealed class MechanicalMayhemStrategy : BossStrategyBase
    {
        public MechanicalMayhemStrategy() : base("mechanical-mayhem", 1800, 700, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => MechanicalFamilies.Count(b) >= 2 && !d.Zenith;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = MechanicalPriority(s);
            return Decision(s, target, "three-boss-kite", BossPattern.Composite, 720, -180,
                CompositeEscape(s, m), 0, true, true, s.Mobility.CanFlipGravity, 95);
        }
    }

    internal sealed class MechdusaStrategy : BossStrategyBase
    {
        public MechdusaStrategy() : base("mechdusa", 1900, 760, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => d.Zenith && MechanicalFamilies.Count(b) >= 2;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = MechanicalPriority(s);
            return Decision(s, target, "attached-stack-kite", BossPattern.Runway, 760, -220,
                AwayX(s.Player, target), 0, true, true, true, 110);
        }
    }

    internal sealed class PlanteraStrategy : BossStrategyBase
    {
        public PlanteraStrategy() : base("plantera", 850, 650, 5f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 262);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 262);
            var second = Life(t) < .5f;
            return Decision(s, t, second ? "phase-2-tight-circle" : "phase-1-wide-circle",
                BossPattern.CircleOrbit, second ? 330 : 430, 0, 0, 0,
                second && Vec2.DistanceSquared(t.Center, s.Player.Center) < 230 * 230, true, false, second ? 82 : 52);
        }
    }

    internal sealed class GolemStrategy : BossStrategyBase
    {
        public GolemStrategy() : base("golem", 680, 380, 4.5f) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 245, 249);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            bool fists;
            var fist = LowestLife(s, 247, 248, out fists);
            var target = fists ? fist : Pick(s, 249, 246, 245);
            var detached = HasType(s.Targets, 249);
            return Decision(s, target, detached ? "detached-head" : fists ? "remove-fists" : "body-jumps",
                BossPattern.HorizontalKite, detached ? 460 : 330, -75, AwayX(s.Player, target), 0,
                target.Velocity.Length > 8f, true, false, detached ? 58 : 44);
        }
    }

    internal sealed class FishronStrategy : BossStrategyBase
    {
        private static readonly string[] ThirdPhaseDashes = { "expert-teleport-dash-1", "expert-teleport-dash-2", "expert-teleport-dash-3" };
        public FishronStrategy() : base("duke-fishron", 1500, 700, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 370);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 370);
            var ratio = Life(t);
            // 1.4.5.8 AI_069_DukeFishron uses 15%, not the old-version 10% rule.
            var phase3 = (s.Difficulty.Expert || s.Difficulty.Master) && (ratio <= .15f || t.Ai0 >= 10f);
            var phase2 = ratio < .5f;
            if (m.PreviousTargetKey >= 0 && m.PreviousTargetKey != t.Key)
            {
                m.DashCounter = 0;
                m.DashActive = false;
            }
            // Original AI branches 1 / 6 / 11 are the charge states, so react on
            // entry instead of waiting a frame for measured velocity to become large.
            var charge = t.Ai0 == 1f || t.Ai0 == 6f || t.Ai0 == 11f || t.Velocity.Length > (phase3 ? 13f : 10f);
            if (charge && !m.DashActive) m.DashCounter++;
            m.DashActive = charge;
            var phase = phase3 ? (charge ? ThirdPhaseDashes[Math.Max(0, m.DashCounter - 1) % 3] : "expert-teleport-reposition") :
                phase2 ? (charge ? "phase-2-three-dashes" : "cthulunado-cycle") :
                (charge ? "phase-1-five-dashes" : "sharknado-bubble-cycle");
            return Decision(s, t, phase, charge ? BossPattern.PerpendicularDashDodge : BossPattern.EllipseOrbit,
                phase3 ? 500 : 620, -160, charge ? -Math.Sign(t.Velocity.X) : 0,
                charge ? PerpendicularY(s.Player, t) : 0, charge, true, s.Mobility.CanFlipGravity, phase3 ? 105 : phase2 ? 82 : 62);
        }
    }

    internal sealed class EmpressStrategy : BossStrategyBase
    {
        public EmpressStrategy() : base("empress-of-light", 1700, 850, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 636);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var t = Pick(s, 636);
            var dash = t.Velocity.Length > 10f;
            string phase;
            BossPattern pattern;
            int vertical;
            if (HasThreat(s, 923)) { phase = "sun-dance"; pattern = BossPattern.CircleOrbit; vertical = 1; }
            else if (HasThreat(s, 919)) { phase = "ethereal-lances"; pattern = BossPattern.ProjectileLanes; vertical = -1; }
            else if (HasThreat(s, 872, 873)) { phase = "everlasting-rainbow"; pattern = BossPattern.CircleOrbit; vertical = 0; }
            else if (dash) { phase = "telegraphed-dash"; pattern = BossPattern.PerpendicularDashDodge; vertical = PerpendicularY(s.Player, t); }
            else { phase = Life(t) < .5f ? "phase-2-bolt-sequence" : "phase-1-bolt-sequence"; pattern = BossPattern.EllipseOrbit; vertical = 0; }
            if (s.Difficulty.DayTime) phase = "daytime-enraged-" + phase;
            return Decision(s, t, phase, pattern, 680, -190, dash ? -Math.Sign(t.Velocity.X) : 0, vertical,
                dash || s.Difficulty.DayTime, true, true, s.Difficulty.DayTime ? 145 : 88);
        }
    }

    internal sealed class CultistStrategy : BossStrategyBase
    {
        public CultistStrategy() : base("lunatic-cultist", 1100, 620, 5.5f, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 439);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var real = Pick(s, 439);
            var ritual = HasThreat(s, 490) || real.Invulnerable;
            var doom = HasType(s.Targets, 523) || HasThreat(s, 593);
            var phase = ritual ? "ritual-hit-real" : doom ? "destroy-ancient-doom" : Life(real) < .5f ? "extended-seven-attack-cycle" : "six-attack-cycle";
            return Decision(s, real, phase, ritual ? BossPattern.ProjectileLanes : BossPattern.EllipseOrbit,
                520, -140, 0, 0, doom, true, false, doom ? 82 : 55, !real.Invulnerable);
        }
    }

    internal sealed class MoonLordStrategy : BossStrategyBase
    {
        public MoonLordStrategy() : base("moon-lord", 2100, 900, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => HasType(b, 396, 398);
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            bool openEyes;
            var eye = LowestLife(s, 396, 397, out openEyes);
            var target = openEyes ? eye : Pick(s, 398, 396, 397);
            var deathray = HasThreat(s, 455);
            var spheres = HasThreat(s, 454);
            return Decision(s, target, deathray ? "sweep-deathray" : spheres ? "sphere-release" : openEyes ? "open-eye-priority" : "core-finish",
                deathray ? BossPattern.PerpendicularDashDodge : BossPattern.Runway,
                760, deathray ? -310 : -180, AwayX(s.Player, target), deathray ? 1 : 0,
                deathray, true, true, deathray ? 130 : 82);
        }
    }

    internal sealed class CompositeBossStrategy : BossStrategyBase
    {
        public CompositeBossStrategy() : base("multi-boss-composite", 1800, 760, 7f, true, true) { }
        public override bool Matches(IList<TargetSnapshot> b, DifficultySnapshot d) => true;
        public override BossDecision Evaluate(CombatSnapshot s, BossMemory m)
        {
            var target = default(TargetSnapshot);
            var found = false;
            for (var i = 0; i < s.Targets.Count; i++)
            {
                var candidate = s.Targets[i];
                if (!candidate.Boss || candidate.Life <= 0) continue;
                if (!found || target.Invulnerable && !candidate.Invulnerable ||
                    target.Invulnerable == candidate.Invulnerable && candidate.Life < target.Life)
                { target = candidate; found = true; }
            }
            return Decision(s, target, "maximin-composite", BossPattern.Composite, 720, -170,
                CompositeEscape(s, m), 0, true, true, s.Mobility.CanFlipGravity, 110);
        }
    }
}
