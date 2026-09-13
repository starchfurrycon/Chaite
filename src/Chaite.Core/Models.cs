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
        LowLevelChaite,
        // Production-scope refusal for every Boss outside the two reviewed
        // native controllers (Duke Fishron and Empress of Light).  Keep this
        // new cue at the end so the ordinals of the original local audio
        // slots remain stable for existing configs/adapters.
        // The audio slot is user-supplied; Chaite never generates or
        // downloads the meme clip.
        UnsupportedBoss,
        UntestedLoadout
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
        EmpressSunDance,
        EmpressLance
    }

    /// <summary>
    /// Native, version-locked motion which cannot be represented by the
    /// ordinary Position + Velocity * ticks predictor.  Linear remains the
    /// zero/default value so snapshots from older adapters stay conservative
    /// and do not acquire projectile-specific semantics by type alone.
    /// </summary>
    public enum ThreatTrajectory
    {
        Linear,
        EmpressRainbowTrail,
        EmpressRainbowStreak,
        EmpressDashContact,
        FallingHostileBolt,
        BouncingFallingHostileBolt,
        // Explicit fail-closed sentinels for the two production Bosses.  These
        // values are never an approximate motion model: a native adapter may
        // publish them only with a same-frame Boss source context, and Core
        // must either relinquish control or treat their motion as unknown.
        UnmodeledDukeFishronHazard,
        UnmodeledEmpressRainbowTrail,
        UnmodeledEmpressDashContact
    }

    internal struct RainbowTrailHistoryBlock10
    {
        private Vec2 _p0;
        private Vec2 _p1;
        private Vec2 _p2;
        private Vec2 _p3;
        private Vec2 _p4;
        private Vec2 _p5;
        private Vec2 _p6;
        private Vec2 _p7;
        private Vec2 _p8;
        private Vec2 _p9;

        public Vec2 Get(int index)
        {
            switch (index)
            {
                case 0: return _p0;
                case 1: return _p1;
                case 2: return _p2;
                case 3: return _p3;
                case 4: return _p4;
                case 5: return _p5;
                case 6: return _p6;
                case 7: return _p7;
                case 8: return _p8;
                case 9: return _p9;
                default: return default(Vec2);
            }
        }

        public bool Set(int index, Vec2 value)
        {
            switch (index)
            {
                case 0: _p0 = value; break;
                case 1: _p1 = value; break;
                case 2: _p2 = value; break;
                case 3: _p3 = value; break;
                case 4: _p4 = value; break;
                case 5: _p5 = value; break;
                case 6: _p6 = value; break;
                case 7: _p7 = value; break;
                case 8: _p8 = value; break;
                case 9: _p9 = value; break;
                default: return false;
            }
            return true;
        }
    }

    /// <summary>
    /// The first 50 native oldPos entries used by projectile 872's collision
    /// override. Terraria allocates 120 trail entries for this projectile, but
    /// Colliding reads only even indices 0 through 48. Keeping all first 50 is
    /// necessary because each native update swaps which captured parity will
    /// occupy those damaging slots. The value type avoids per-frame arrays.
    /// </summary>
    public struct RainbowTrailHistory50
    {
        public const int Length = 50;

        private RainbowTrailHistoryBlock10 _b0;
        private RainbowTrailHistoryBlock10 _b1;
        private RainbowTrailHistoryBlock10 _b2;
        private RainbowTrailHistoryBlock10 _b3;
        private RainbowTrailHistoryBlock10 _b4;

        public Vec2 Get(int index)
        {
            if (index < 0 || index >= Length) return default(Vec2);
            var offset = index % 10;
            switch (index / 10)
            {
                case 0: return _b0.Get(offset);
                case 1: return _b1.Get(offset);
                case 2: return _b2.Get(offset);
                case 3: return _b3.Get(offset);
                default: return _b4.Get(offset);
            }
        }

        public bool Set(int index, Vec2 value)
        {
            if (index < 0 || index >= Length) return false;
            var offset = index % 10;
            switch (index / 10)
            {
                case 0: return _b0.Set(offset, value);
                case 1: return _b1.Set(offset, value);
                case 2: return _b2.Set(offset, value);
                case 3: return _b3.Set(offset, value);
                default: return _b4.Set(offset, value);
            }
        }
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
        public IList<int> ActiveBossGenerations = Array.Empty<int>();
        // Native facade publishes the active NPC type identities separately
        // from stable Boss keys.  An empty list remains compatible with older
        // synthetic adapters; production uses it to fail closed before any
        // unsupported Boss can receive controls.
        public IList<int> ActiveBossTypes = Array.Empty<int>();
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
        // Native Player.UpdateBuffs folds several movement impairments into
        // strongestMoveSpeedDebuff before multiplying maxRunSpeed,
        // accRunSpeed and runAcceleration.  The exact factor and Slow(32)
        // identity let a Boss-specific controller distinguish its own modeled
        // temporary impairment from lost equipment or an unrelated stronger
        // debuff.  Legacy/synthetic adapters remain unknown by default.
        public bool MoveSpeedDebuffFactorKnown;
        public float MoveSpeedDebuffFactor;
        public bool SlowDebuffKnown;
        public bool SlowDebuffActive;
        public float JumpSpeedBoost;
        public JumpSnapshot Jump;
        public FlightSnapshot Flight;
        public float WingTime;
        public float RocketTime;
        // Hash-locked native adapters identify the exact effective functional
        // accessories which produced the aggregate wing/rocket fields.  A value
        // of -1 means multiple effect sources were observed; zero means none.
        // Synthetic adapters remain unknown by default and cannot enter a
        // fixture which requires a precise equipment identity.
        public bool FunctionalEquipmentIdentityKnown;
        public int WingAccessoryItemType;
        public int RocketBootAccessoryItemType;
        // Native Boss enrage/despawn predicates consume the targeted player's
        // live biome state. False is a legitimate value; adapters must set a
        // matching Known bit rather than using false as an availability
        // sentinel.
        public bool ZoneCorruptKnown;
        public bool ZoneCorrupt;
        public bool ZoneCrimsonKnown;
        public bool ZoneCrimson;
        public bool ZoneJungleKnown;
        public bool ZoneJungle;
        public bool ZoneLihzhardTempleKnown;
        public bool ZoneLihzhardTemple;
        public bool OnGround;
        public bool OnOneWaySupport;
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
        // Exact catalog identities are separate from aggregate movement
        // properties. Unknown/new/custom identities can be displayed but may
        // not satisfy a vanilla Boss baseline or authorize mount input.
        public bool ActiveMountIdentityKnown;
        public int ActiveMountType;
        // Read-only result of Mount.CanDismount(player), sampled only while a
        // mount is active. This is a handoff safety probe, not a reviewed
        // combat locomotion route.
        public bool ActiveMountDismountProbeKnown;
        public bool ActiveMountCanDismount;
        public bool ActiveMountReleaseReady;
        public bool SelectedMountIdentityKnown;
        public int SelectedMountItemType;
        public int SelectedMountType;
        public bool MountCanFly;
        public bool HasUsableMount;
        public float MountRunSpeed;
        public bool CanDash;
        public bool DashReady;
        public int DashType;
        public bool HasGrapple;
        public float GrappleRangePixels = 300f;
        public bool Grappling;
        // Exact live identity/state for the first reviewed grapple family.
        // Aggregate HasGrapple/Grappling remain informational only and can
        // never authorize an input edge.
        public BasicHookFrameSnapshot BasicHook;
        // Exact type-23 observation. The toggle fit flags and OpenDryPath are
        // deliberately false until a caller supplies real collision evidence.
        public WitchBroomToggleSnapshot WitchBroomToggle;
        public WitchBroomMotionSnapshot WitchBroomMotion;
        public bool CanFlipGravity;
        public bool GravityInverted;
        // Aggregate native slowFall remains part of ordinary ballistic
        // prediction, regardless of whether it came from a potion, equipment,
        // or another vanilla movement branch.  Only the exact active Buff 8
        // identity below may authorize the dedicated Up input candidate.
        public bool FeatherFall;
        public bool FeatherFallPotionKnown;
        public bool FeatherFallPotionActive;
        // Closed, hash-locked native observations. Aggregate CanDash/
        // CanFlipGravity flags describe compatibility only: they neither meet
        // a minimum-loadout requirement nor authorize input. Only exact states
        // may participate in a trajectory-scored optional edge.
        public GravityFlipState GravityFlip;
        public EyeShieldDashState EyeShieldDash;
        // DashMovement probes the leading edge selected by the eventual
        // horizontal plan. Preserve both read-only results so the planner does
        // not guess a tile outcome before it has selected a direction.
        public bool DashLeftProbeKnown;
        public bool DashLeftProbeBlocked;
        public bool DashRightProbeKnown;
        public bool DashRightProbeBlocked;
        public bool FormulaAccessoryScanKnown;
        public int UnexpectedFormulaMobilityItemType;
        // Capability is independent of remaining charge: no wings/rocket boots
        // and genuinely exhausted flight both have a zero resource fraction.
        public bool HasFiniteFlightResource;
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
        // Exact, locally verified support intervals. Open air/wall clearance does
        // not establish the existence of a floor across the same horizontal area.
        public SupportSpan FloorSupport;
        public SupportSpan CeilingSupport;
        public SupportSpan RecoverySupport;
        public readonly List<Vec2> GrappleAnchors = new List<Vec2>(12);

        public float HorizontalClearance => ClearanceLeft + ClearanceRight;
        public float VerticalClearance => ClearanceUp + ClearanceDown;
    }

    public struct SupportSpan
    {
        public bool Valid;
        public bool Inverted;
        public bool OneWay;
        public float Left;
        public float Right;
        public float SurfaceY;

        public bool ContainsBody(float x, int width) => Valid && Right > Left &&
            x >= Left && x + width <= Right;
        public bool OverlapsBody(float x, int width) => Valid && Right > Left &&
            x < Right && x + width > Left;
    }

    /// <summary>Conservative flat-support geometry; never invents unobserved terrain.</summary>
    public static class SupportGeometry
    {
        public static bool RetainsFooting(SupportSpan support, float x, int width, bool inverted, bool drop)
        {
            return support.Inverted == inverted && (!support.OneWay || !inverted && !drop) &&
                support.OverlapsBody(x, width);
        }

        public static bool TryLand(SupportSpan support, Vec2 before, ref Vec2 position, ref Vec2 velocity,
            int width, int height, bool inverted, bool drop)
        {
            // Require the full predicted footprint for a NEW landing. Existing
            // native grounded contact may retain footing until its final overlap.
            if (!support.ContainsBody(position.X, width) || support.Inverted != inverted ||
                support.OneWay && (inverted || drop)) return false;
            var beforeFoot = inverted ? before.Y : before.Y + height;
            var afterFoot = inverted ? position.Y : position.Y + height;
            if (!(inverted ? velocity.Y < 0f && beforeFoot >= support.SurfaceY && afterFoot <= support.SurfaceY :
                velocity.Y > 0f && beforeFoot <= support.SurfaceY && afterFoot >= support.SurfaceY)) return false;
            var fraction = (support.SurfaceY - beforeFoot) / (afterFoot - beforeFoot);
            var crossingX = before.X + (position.X - before.X) * fraction;
            if (!support.ContainsBody(crossingX, width)) return false;
            position.Y = inverted ? support.SurfaceY : support.SurfaceY - height;
            velocity.Y = 0f;
            return true;
        }
    }

    public sealed class DifficultySnapshot
    {
        // Main.GameMode is read independently of expert/master convenience
        // flags.  GameModeKnown prevents a default zero from impersonating a
        // native Classic world in third-party or synthetic adapters.
        public bool GameModeKnown;
        public int GameMode;
        public bool Journey;
        public bool Expert;
        public bool Master;
        public bool Drunk;
        public bool NotTheBees;
        public bool ForTheWorthy;
        public bool Remix;
        public bool Zenith;
        public bool Celebration;
        public bool Constant;
        public bool NoTraps;
        public bool Skyblock;
        public bool DayTime;
    }

    /// <summary>
    /// A same-frame, read-only admission observation for Razorblade Typhoon.
    /// Type 409 selects its own target in AI_071 after the item input has been
    /// applied, so this is deliberately not a promise that a later projectile
    /// update will lock the same NPC or deal damage.  It only lets the planner
    /// avoid issuing a new cast when the currently observed native candidate,
    /// immunity state, or existing same-owner projectile makes the route
    /// unsuitable.  Unknown observations must never authorize a cast.
    /// </summary>
    public struct RazorbladeTyphoonFireObservation
    {
        public bool Known;
        public bool SpawnCenterKnown;
        public Vec2 SpawnCenter;
        public bool NoActiveOwnedProjectiles;
        // This is the winner of a fresh pre-fire reproduction of AI_071's
        // native scan, not an assertion about a projectile that has not yet
        // been created. -1 means no eligible candidate was observed.
        public int PrefireSelectedNpcKey;
        public bool PrefireSelectedTargetHomingReady;

        public bool PermitsTarget(int targetKey) => Known &&
            SpawnCenterKnown && NoActiveOwnedProjectiles &&
            targetKey >= 0 && targetKey == PrefireSelectedNpcKey &&
            PrefireSelectedTargetHomingReady;
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

        // Native adapters must opt into exact identity/profile validation.
        // False is retained only for synthetic/legacy snapshot tests, never set
        // by the production facade even when the selected slot is empty.
        public bool NativeProfileRequired;
        public int WeaponId;
        public int AmmoId;
        public int ProjectileId;
        public WeaponProfileEvaluation Profile;
        public ManaOutputState Mana;
        public RazorbladeTyphoonFireObservation RazorbladeTyphoon;

        public float ApproximateDps => !IsUsable ? 0f : NativeProfileRequired ?
            Profile.ApproximateDirectDps : UseTime > 0 ? Damage * 60f / UseTime : 0f;
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
        public bool Ai0Known;
        public bool Ai1Known;
        public bool Ai2Known;
        public bool Ai3Known;
        // Native local AI is not synchronized like ai[]. Never invent a teleport
        // destination for adapters which did not actually observe these fields.
        public bool LocalAiKnown;
        public bool LocalAi0Known;
        public bool LocalAi1Known;
        public bool LocalAi2Known;
        public bool LocalAi3Known;
        public float LocalAi0;
        public float LocalAi1;
        public float LocalAi2;
        public float LocalAi3;
        // Entity.direction can legitimately be zero during native transitions.
        // Its availability is therefore represented independently.
        public bool NativeDirectionKnown;
        public int NativeDirection;
        public bool NativeTimeLeftKnown;
        public int NativeTimeLeft;
        // Native NPC.target is a player slot, with 255/-1 used by vanilla as
        // invalid/unselected sentinels. Legacy/synthetic adapters remain unknown
        // by default rather than silently claiming that the local player owns it.
        public bool NativeTargetKnown;
        public int NativeTargetPlayerIndex;
        // NPC.realLife is the native parent/root identity for the secondary
        // Boss components which carry it.  It is kept separate from ai[]:
        // zero is a valid slot and an unavailable adapter must not fabricate
        // a parent relationship.
        public bool NativeRealLifeKnown;
        public int NativeRealLife;
        // Destroyer head only: the previous completed NPC update's final worm-
        // movement branch, sourced from localAI[0]. Body/tail localAI[0] has a
        // different meaning and must never set these fields.
        public bool DestroyerBranchKnown;
        public bool DestroyerUsesWormMovement;

        public Vec2 Center => new Vec2(Position.X + Width * 0.5f, Position.Y + Height * 0.5f);
    }

    public struct ThreatSnapshot
    {
        public ThreatKind Kind;
        public ThreatGeometry Geometry;
        public ThreatTrajectory Trajectory;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int Damage;
        public int TimeLeft;
        public int Type;
        public int NativeIdentity;
        public float TrajectoryAi0;
        public bool TrajectoryAi0Known;
        // Additional native ai/localAI values needed by the version-locked
        // Duke Fishron hazard families (384/385/386 projectiles and
        // 371/372/373 NPCs).  Availability is explicit: a default/legacy
        // adapter must not accidentally turn zeroes into a proof of motion.
        public bool TrajectoryAi2Known;
        public float TrajectoryAi2;
        public bool TrajectoryLocalAi0Known;
        public bool TrajectoryLocalAi1Known;
        public float TrajectoryLocalAi0;
        public float TrajectoryLocalAi1;
        public bool NativeDirectionKnown;
        public int NativeDirection;
        // Projectile 872 collision is defined by oldPos rather than its current
        // body. This fixed value-type copy is populated only by the native
        // adapter after all first 50 entries were observed as finite values.
        public bool NativeRainbowHistoryKnown;
        public RainbowTrailHistory50 NativeRainbowHistory;
        // NPC 636 states 8/9 need the exact state clock and phase/rage form.
        // Availability bits prevent default zeroes from becoming native data.
        public bool TrajectoryAi1Known;
        public float TrajectoryAi1;
        public bool TrajectoryAi3Known;
        public float TrajectoryAi3;
        public bool NativeExpertModeKnown;
        public bool NativeExpertMode;
        public bool NativeShouldBeEnragedKnown;
        public bool NativeShouldBeEnraged;
        // AI_171 stores the hostile rainbow streak's target player in ai[0].
        // Keep the validity bit separate: zero is both a real player slot and
        // the default value of older/synthetic snapshots.
        public bool NativeTargetPlayerKnown;
        public int NativeTargetPlayerIndex;
        // Family-level provenance, not a claim that vanilla retains a direct
        // entity-parent link.  The production adapter sets this only when a
        // live canonical Boss of this type was observed in the same NPC array
        // pass.  Unknown/default snapshots cannot open a Boss-specific gate.
        public bool SourceBossContextKnown;
        public int SourceBossType;
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
        // One unambiguous, exact reviewed staff/whip pair from hotbar slots
        // 0..9. Production refreshes this read-only observation every snapshot;
        // default/legacy adapters remain unknown and therefore cannot enter the
        // dual-slot output controller.
        public SummonWhipOutputObservation SummonWhipOutput;
        public readonly List<TargetSnapshot> Targets = new List<TargetSnapshot>(64);
        public readonly List<ThreatSnapshot> Threats = new List<ThreatSnapshot>(256);
        public readonly PriorityBossNativeContext PriorityBoss =
            new PriorityBossNativeContext();
        public bool LineOfSightToPrimary;
        // Production snapshots explicitly opt into this native context. Defaults
        // keep third-party/synthetic adapters fail-closed for single-player-only
        // strategies instead of treating zero-valued fields as observed data.
        public bool NativeContextKnown;
        public int NetMode;
        public int LocalPlayerIndex;
    }

    /// <summary>
    /// A trajectory-scored, optional-edge-free movement candidate captured in
    /// the same planner pass as the selected plan. The native late gate may use
    /// it only when a dash/gravity edge changes before movement; it must never
    /// fabricate a replacement from the post-update state.
    /// </summary>
    public struct LateMobilityFallback
    {
        public bool Known;
        public int Horizontal;
        public bool Jump;
        public JumpAction JumpAction;
        public bool Drop;
        public float Hazard;
    }

    public struct ControlPlan
    {
        public int Horizontal;
        public bool Jump;
        public JumpAction JumpAction;
        public bool Drop;
        public bool Fire;
        public bool QuickHeal;
        public bool QuickMana;
        public bool Dash;
        public bool Hook;
        public bool ToggleMount;
        public int GravityControl;
        // Dedicated feather-fall "up" input. It must stay separate from
        // GravityControl because vanilla uses the same physical Up key to flip
        // gravity on a releaseUp edge. The planner only emits this flag when
        // gravity control, mounts and grappling are absent and the exact
        // slow-fall trajectory participated in candidate scoring.
        public bool FeatherFallUp;
        public LateMobilityFallback LateMobilityFallback;
        public Vec2 AimWorld;
        public Vec2 HookWorld;
        public int TargetKey;
        public float RiskScore;
        public int PreferredWeaponSlot;
        public TacticalMode TacticalMode;
        public string StrategyId;
        public string PhaseId;
        public FormulaRoute FormulaRoute;
        public string WeaponIssue;
        // Exact output-route certificate consumed by the late native input gate.
        // Unspecified is retained only for synthetic adapters and tests.
        public OutputRouteKind OutputRouteKind;
        public int ExpectedWeaponId;
        public int ExpectedAmmoId;
        public int ExpectedProjectileId;
        // Complete two-slot certificate and the one FSM decision authorized for
        // this frame. Facade re-reads native inventory/capacity/buff/count facts
        // before applying it; OutputRouteKind.MinionAndWhip without this exact
        // certificate is always rejected.
        public SummonWhipOutputRoute SummonWhipOutputRoute;
        public SummonWhipOutputPhase SummonWhipOutputPhase;
        public SummonWhipOutputAction SummonWhipOutputAction;
        // A strategy may relinquish control after a bounded loss of its reviewed
        // safety contract. This is a normal, non-fatal session outcome request;
        // Runtime must not apply this otherwise-neutral plan.
        public bool RequestControlReturn;
        public string ControlReturnReason;
        // A one-frame fail-safe hold is not a session outcome. Runtime keeps
        // the encounter engaged and applies no movement, item, consumable, or
        // optional-mobility input until the transient unmodeled hazard clears.
        public bool HoldNeutralControls;
        public string NeutralControlReason;
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
        private bool _plannerSelectionSealed;
        private BossSummonKind _selectedKind;
        private int _selectedSummonSlot;
        private int _selectedActionSlot;
        private int _selectedItemType;
        private int _selectedExpectedBossType;
        private int _selectedTimeoutTicks;
        private Vec2 _selectedInteractionWorld;
        private string _selectedId;
        private int _selectedCombatWeaponSlot;

        public BossSummonKind Kind;
        public int SummonSlot = -1;
        public int ActionSlot = -1;
        public int ItemType;
        public int ExpectedBossType;
        public int TimeoutTicks;
        public Vec2 InteractionWorld;
        public string Id;
        // Filled only after pre-summon output admission. Special summon flows
        // (notably Prismatic Lacewing) must use this slot instead of reranking.
        public int CombatWeaponSlot = -1;

        public bool IsNatural => Kind == BossSummonKind.NaturalEye ||
                                 Kind == BossSummonKind.NaturalMechanicalBoss ||
                                 Kind == BossSummonKind.NaturalMoonLord;

        internal void SealPlannerSelection()
        {
            _plannerSelectionSealed = true;
            _selectedKind = Kind;
            _selectedSummonSlot = SummonSlot;
            _selectedActionSlot = ActionSlot;
            _selectedItemType = ItemType;
            _selectedExpectedBossType = ExpectedBossType;
            _selectedTimeoutTicks = TimeoutTicks;
            _selectedInteractionWorld = InteractionWorld;
            _selectedId = Id;
            _selectedCombatWeaponSlot = CombatWeaponSlot;
        }

        internal bool MatchesPlannerSelection()
        {
            return _plannerSelectionSealed && Kind == _selectedKind &&
                SummonSlot == _selectedSummonSlot &&
                ActionSlot == _selectedActionSlot &&
                ItemType == _selectedItemType &&
                ExpectedBossType == _selectedExpectedBossType &&
                TimeoutTicks == _selectedTimeoutTicks &&
                InteractionWorld.X == _selectedInteractionWorld.X &&
                InteractionWorld.Y == _selectedInteractionWorld.Y &&
                string.Equals(Id, _selectedId, StringComparison.Ordinal) &&
                CombatWeaponSlot == _selectedCombatWeaponSlot;
        }

        public bool TrySetAdmittedCombatWeaponSlot(int slot)
        {
            if (!MatchesPlannerSelection() || slot < -1 || slot >= 10)
                return false;
            CombatWeaponSlot = slot;
            _selectedCombatWeaponSlot = slot;
            return true;
        }
    }

    public struct BossStartTick
    {
        public bool Issued;
        public bool StillValid;
        public bool ControlsApplied;
        public string FailureReason;
    }
}
