using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    [Flags]
    public enum EncounterFlags
    {
        None = 0,
        Boss = 1 << 0,
        Invasion = 1 << 1,
        PumpkinMoon = 1 << 2,
        FrostMoon = 1 << 3,
        Eclipse = 1 << 4,
        BloodMoon = 1 << 5,
        SlimeRain = 1 << 6,
        OldOnesArmy = 1 << 7,
        LunarPillars = 1 << 8
    }

    public enum SessionState
    {
        Idle,
        Validating,
        RejectedNoEncounter,
        PreparingBoss,
        AwaitingBossSpawn,
        EngagedAlive,
        EngagedDeadWaitingRespawn,
        SuccessNoDeath,
        SuccessAfterDeath,
        FailedAfterDeath,
        Cancelled,
        EncounterInterrupted
    }

    public enum AudioCue
    {
        None,
        NoSlimeAng,
        TryMinnie,
        Man,
        Dead,
        MambaOut,
        FailedBossDesign,
        LowLevelChaite
    }

    public enum ThreatKind
    {
        Projectile,
        NpcContact
    }

    public enum ThreatGeometry
    {
        Body,
        MoonLordDeathray,
        EmpressSunDance
    }

    public sealed class EncounterObservation
    {
        public EncounterFlags Flags;
        public bool PlayerDead;
        public int PlayerLife;
        public bool StartAuthorized;
        public bool RequirePreparation;
        public bool ExpectedBossArrived = true;
        public bool WaitingStillValid = true;
        public IList<int> ActiveBossKeys = Array.Empty<int>();
        public IList<int> KilledBossKeys = Array.Empty<int>();

        public bool HasEncounter => (Flags & EncounterFlags.Boss) != 0 || ActiveBossKeys.Count > 0;
    }

    public sealed class StateUpdate
    {
        public SessionState Previous;
        public SessionState Current;
        public AudioCue Cue;
        public bool ApplyControls;
        public bool BecameTerminal;
    }

    public sealed class PlayerSnapshot
    {
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int Life;
        public int MaxLife;
        public int Mana;
        public int MaxMana;
        public float Gravity;
        public float MaxFallSpeed;
        public float MaxRunSpeed;
        public float RunAcceleration;
        // Optional native horizontal profile. Zero BaseRunSpeed preserves the
        // original synthetic/third-party adapter model; MaxRunSpeed remains the
        // attainable sprint ceiling used by requirements and broadphase bounds.
        public float BaseRunSpeed;
        public float SprintAcceleration;
        public float RunSlowdown;
        public bool CanSprintInAir;
        public float JumpSpeedBoost;
        public float WingTime;
        public float RocketTime;
        public bool OnGround;
        public bool Dead;
        public float WorldLeft;
        public float WorldRight;
        public float WorldTop;
        public float WorldBottom;

        public RectF BoundsAt(Vec2 position) => new RectF(position.X, position.Y, Width, Height);
        public Vec2 Center => new Vec2(Position.X + Width * 0.5f, Position.Y + Height * 0.5f);
    }

    public sealed class MobilitySnapshot
    {
        public bool MountActive;
        public bool MountCanFly;
        public bool HasUsableMount;
        public float MountRunSpeed;
        public bool CanDash;
        public bool DashReady;
        public int DashType;
        public bool HasGrapple;
        public float GrappleRangePixels = 300f;
        public bool Grappling;
        public bool CanFlipGravity;
        public bool GravityInverted;
        public bool FeatherFall;
        public float FlightResourceFraction;
    }

    public sealed class ArenaSnapshot
    {
        public RectF LocalOpenBounds;
        public Vec2 SafeCenter;
        public float ClearanceLeft;
        public float ClearanceRight;
        public float ClearanceUp;
        public float ClearanceDown;
        public bool HasFloor;
        public bool HasCeiling;
        public readonly List<Vec2> GrappleAnchors = new List<Vec2>(12);

        public float HorizontalClearance => ClearanceLeft + ClearanceRight;
        public float VerticalClearance => ClearanceUp + ClearanceDown;
    }

    public sealed class DifficultySnapshot
    {
        public bool Expert;
        public bool Master;
        public bool ForTheWorthy;
        public bool Remix;
        public bool Zenith;
        public bool Celebration;
        public bool Constant;
        public bool NoTraps;
        public bool Skyblock;
        public bool DayTime;
    }

    public sealed class WeaponSnapshot
    {
        public int Slot;
        public int Damage;
        public int UseTime;
        public float ShootSpeed;
        public bool IsProjectile;
        public bool IsMelee;
        public bool HasAmmo;
        public bool IsUsable;

        public float ApproximateDps => IsUsable && UseTime > 0 ? Damage * 60f / UseTime : 0f;
    }

    public struct TargetSnapshot
    {
        public int Key;
        public int Type;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int Life;
        public int LifeMax;
        public int Damage;
        public bool Boss;
        public bool Chaseable;
        public bool Invulnerable;
        // Unknown preserves the legacy primary-target hint for older adapters.
        public bool LineOfSightKnown;
        public bool HasLineOfSight;
        public float Ai0;
        public float Ai1;
        public float Ai2;
        public float Ai3;

        public Vec2 Center => new Vec2(Position.X + Width * 0.5f, Position.Y + Height * 0.5f);
    }

    public struct ThreatSnapshot
    {
        public ThreatKind Kind;
        public ThreatGeometry Geometry;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int Damage;
        public int TimeLeft;
        public int Type;
        public Vec2 BeamOrigin;
        public Vec2 BeamSourceVelocity;
        public Vec2 BeamDirection;
        public float BeamAngularVelocity;
        public float BeamBaseAngle;
        public float BeamAngle;
        public float BeamAge;
        public float BeamLength;
        public float BeamScale;
        public float BeamScaleLimit;

        public RectF BoundsAt(float ticks)
        {
            var predicted = Position + Velocity * ticks;
            return new RectF(predicted.X, predicted.Y, Width, Height);
        }
    }

    public sealed class CombatSnapshot
    {
        public PlayerSnapshot Player = new PlayerSnapshot();
        public MobilitySnapshot Mobility = new MobilitySnapshot();
        public ArenaSnapshot Arena = new ArenaSnapshot();
        public DifficultySnapshot Difficulty = new DifficultySnapshot();
        public WeaponSnapshot Weapon = new WeaponSnapshot();
        public readonly List<TargetSnapshot> Targets = new List<TargetSnapshot>(64);
        public readonly List<ThreatSnapshot> Threats = new List<ThreatSnapshot>(256);
        public bool LineOfSightToPrimary;
    }

    public struct ControlPlan
    {
        public int Horizontal;
        public bool Jump;
        public bool Drop;
        public bool Fire;
        public bool QuickHeal;
        public bool QuickMana;
        public bool Dash;
        public bool Hook;
        public bool ToggleMount;
        public int GravityControl;
        public Vec2 AimWorld;
        public Vec2 HookWorld;
        public int TargetKey;
        public float RiskScore;
        public int PreferredWeaponSlot;
        public TacticalMode TacticalMode;
        public string StrategyId;
        public string PhaseId;
    }

    public enum TacticalMode
    {
        EstablishPattern,
        StablePattern,
        EmergencyEvade,
        RecoverToPattern,
        AwaitingBoss
    }

    public enum BossSummonKind
    {
        None,
        DirectItem,
        PrismaticLacewing,
        LihzahrdAltar,
        TruffleWormFishing,
        GuideVoodooDoll,
        NaturalEye,
        NaturalMechanicalBoss,
        NaturalMoonLord
    }

    public sealed class HotbarItemSnapshot
    {
        public int Slot;
        public int Type;
        public int Stack;
    }

    public sealed class BossStartContext
    {
        public bool DayTime;
        public double Time;
        public bool HardMode;
        public bool ZoneCorrupt;
        public bool ZoneCrimson;
        public bool ZoneHallow;
        public bool ZoneJungle;
        public bool ZoneSnow;
        public bool ZoneBeach;
        public bool ZoneOverworld;
        public bool ZoneUnderworld;
        public bool ZenithWorld;
        public bool DownedGolemBoss;
        public bool GuideAlive;
        public bool CultistActive;
        public bool MysteriousTabletActive;
        public bool BlockingInvasion;
        public bool LunarPillarsActive;
        public bool CritterProtection;
        public bool NearLihzahrdAltar;
        public bool NearbyLava;
        public bool OceanWater;
        public int FishingRodHotbarSlot = -1;
        public Vec2 AltarWorld;
        public Vec2 LavaWorld;
        public Vec2 OceanWaterWorld;
        public bool SpawnEyeScheduled;
        public int SpawnHardBoss;
        public int MoonLordCountdown;
        public readonly HashSet<int> ActiveBossTypes = new HashSet<int>();
        public readonly List<HotbarItemSnapshot> Hotbar = new List<HotbarItemSnapshot>(10);
    }

    public sealed class BossStartPlan
    {
        public BossSummonKind Kind;
        public int SummonSlot = -1;
        public int ActionSlot = -1;
        public int ItemType;
        public int ExpectedBossType;
        public int TimeoutTicks;
        public Vec2 InteractionWorld;
        public string Id;

        public bool IsNatural => Kind == BossSummonKind.NaturalEye ||
                                 Kind == BossSummonKind.NaturalMechanicalBoss ||
                                 Kind == BossSummonKind.NaturalMoonLord;
    }

    public struct BossStartTick
    {
        public bool Issued;
        public bool StillValid;
        public bool ControlsApplied;
        public string FailureReason;
    }
}
