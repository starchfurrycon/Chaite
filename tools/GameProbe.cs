using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.IO;
using Terraria.ID;
using Terraria.Social;
using Game = Terraria.Main;

// Test-only instrumentation. Never included in the distributable Plugin.
// Runs native AI, physics and input replay in an isolated desktop, with an
// in-memory test arena and no user character/world loading. Headless mode uses
// dedicated-server presentation branches, not an equivalent rendered client.
public static class ChaiteGameProbe
{
    static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;

    // The lockstep wait falls back to Thread.Sleep(1) once the trainer is more
    // than a few milliseconds behind, and without a raised timer resolution that
    // call sleeps for the default ~15.6 ms tick -- measured 16.04 ms on this
    // machine. During that sleep the engine could have run several native ticks,
    // so the whole round is paced by a timer that is far coarser than the
    // 8-tick lead the lockstep actually allows. timeBeginPeriod(1) makes
    // Sleep(1) mean one millisecond; the process exit restores the default.
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    static extern uint TimeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    static extern uint TimeEndPeriod(uint period);

    static bool timerResolutionRaised;

    static void RaiseTimerResolution()
    {
        try
        {
            uint result = TimeBeginPeriod(1);
            timerResolutionRaised = result == 0;
            Log("TIMER_RESOLUTION period=1 result=" + result +
                " raised=" + timerResolutionRaised);
        }
        catch (Exception e)
        {
            // A missing winmm entry point must not take the round down: the
            // lockstep still works, just at the coarse timer granularity.
            Log("TIMER_RESOLUTION failed: " + e.GetType().Name + " " + e.Message);
        }
    }
    static readonly Stopwatch processClock = Stopwatch.StartNew();
    static ScenarioSpec scenario;
    static bool settingsRead, finishing;
    static int seed = 20260910, difficultyCode, tickLimit = 24000, wallLimitSeconds = 90;
    /// <summary>Hostile projectiles written per bridge observation line. The
    /// trainer's feature vector is sized from the same number, so a mismatch
    /// silently misaligns every projectile feature; CHAITE_PROJ_SLOTS is the one
    /// place both sides read. Default 12 is what every existing checkpoint was
    /// trained with. The true count reaches 43 on the day Empress and 32 on Duke
    /// Fishron (measured from the monitor stream), and the window is sorted by
    /// Manhattan distance rather than threat, so the slots it drops are the fast
    /// closing projectiles rather than the harmless far ones.</summary>
    static int projectileSlots = 12;
    /// <summary>Ordering and de-duplication of the bridge's projectile window.
    ///
    /// The measured problem with the original ordering: the window was sorted by
    /// Manhattan distance and the Sharknado column (384/385/386) is a large,
    /// slow, numerous NPC-ish cloud of hitboxes, so it took every slot. On the
    /// recorded obsb1 stream, 78.8% of all slot appearances at a life-loss frame
    /// were type 384 and 1159 of 2757 hit frames had all 12 slots filled with the
    /// column, while the fast closing projectiles that actually connect (720,
    /// 204/205, 43, 201/202/203) were pushed out of the window entirely.
    ///
    /// Two switches, both off by default so the existing checkpoints keep the
    /// exact row they were trained on:
    /// <list type="bullet">
    /// <item><c>CHAITE_PROJ_SORT=threat</c> orders the window by an estimate of
    /// how soon the projectile can reach the player, not by raw distance.</item>
    /// <item><c>CHAITE_PROJ_COLLAPSE=1</c> keeps only the nearest instance of
    /// each projectile type, so twelve copies of one hitbox cannot crowd out the
    /// rest of the hostile set. The types it drops are counted in <c>pe</c>, so
    /// the omission is visible in the stream rather than silent.</item>
    /// </list>
    /// Both change only the ORDER and the MEMBERSHIP of the window, never its
    /// width, so they cannot misalign the feature vector.</summary>
    static bool projectileSortByThreat;
    static bool projectileCollapseTypes;
    /// <summary>Total native ticks this process may run, 0 for unbounded. A
    /// training round sets it so the 32-bit address-space ceiling is never
    /// approached, independently of how fast the machine happens to be.</summary>
    static int runTickLimit;
    static int takeoverTick = 120, directSpawnTick = -1;
    // Ends a monitor run at the first frame that costs the player life. The
    // objective is no-hit, so a run that has taken a hit is already a failure
    // and the remaining frames cannot change that; under that objective the
    // runs that reach the tick limit are exactly the successes. Off by
    // default, because every other probe consumer wants the whole fight.
    static bool stopOnFirstHit;
    // ---- t-agent style in-engine training: episode mode + observation bridge ----
    // CHAITE_BRIDGE_FILE names the base path of the bridge. The trainer owns
    // movement through the route channel (RouteReplay bridge mode) and the
    // harness appends one compact observation line per tick to
    // <base>.obs.jsonl, so a PPO rollout is exactly the real fight.
    // CHAITE_EPISODES=N turns the probe from one fight per process into N
    // fights per process: Finish is intercepted, the arena is soft-reset the
    // way t-agent's ResetManager does (despawn, teleport, heal, resummon) and
    // the fight restarts inside the same process.
    static string bridgeBase;
    /// <summary>How many ticks the native loop may run ahead of the trainer's
    /// latest action. The trainer rewrites one action per tick, so a small
    /// allowance keeps every (observation, action) pair aligned while letting
    /// the engine run at its own pace instead of a fixed sleep.</summary>
    const int BridgeLagLimit = 8;
    static int bridgeActionTick = -1;
    static long bridgeActionReadAt;
    static int bridgeWaitSpins;
    /// <summary>Set once a few 30 s stalls show the trainer is gone, so the rest
    /// of the round runs free instead of crawling one tick per timeout.</summary>
    static int bridgeWaitTimeouts;
    static bool bridgeLockstepDisabled;
    // True only when CHAITE_BRIDGE_FILE names a trainer, independently of where
    // this probe writes its own stream (CHAITE_PROBE_OUT).
    static bool bridgeDriven;
    static int episodeLimit;
    static int episodeIndex, episodeStartTick, episodeHits, episodeArmTick = -1;
    static long episodeBossDamageStart;
    static System.IO.StreamWriter bridgeObsWriter;
    static int monitorArmedTick = -1, monitorCombatTick = -1, monitorPassiveFrames;
    static int shieldBeforeHit, shieldBeforeDelay, shieldBeforeLife;
    static bool shieldBeforeRequested;
    static int shieldTraceRows, shieldTraceDropped;
    static Dictionary<string,object> pendingShieldTrace;
    static bool IsMonitorFixture { get { return requestedPhase == "monitor"; } }
    static string difficulty = "classic";
    static string requestedPhase = "summon";
    static string formulaRoute;
    static int deaths, minLife = int.MaxValue, grappleTicks, maximumGrappleTicks, expectedBossMask;
    static long bossDamage, previousRuntimeTotal;
    static int previousRuntimeFrames, nativeSceneMetricRefreshes;
    static Terraria.Utilities.UnifiedRandom battleRandom;
    static int[] battleRandomFingerprint;
    static int battleRandomReferenceChecks;
    static ulong initialUnpausedUpdateSeed, expectedUnpausedUpdateSeed;
    static Action<ulong> setUnpausedUpdateSeed;
    static int unpausedUpdateSeedAdvances;
    static bool nativeDifficultyVerified, battleRandomInstalled, battleRandomColdStateVerified;
    static Dictionary<string, object> nativeDifficultyReport;
    // Variant identity is captured at the synthetic F8 edge, rather than at
    // result-write time. A long native fight can cross dawn/dusk; re-reading
    // Main.dayTime at termination would mislabel the encounter actually handed
    // to the controller.
    static VariantObservation variantAtActivation;
    static readonly List<double> runtimeSamples = new List<double>(24000);
    static readonly List<double> engineSamples = new List<double>(24000);
    static readonly Dictionary<int, BossLifeSample> previousBosses = new Dictionary<int, BossLifeSample>();
    static readonly List<int> retiredBossSlots = new List<int>(16);
    static readonly HashSet<int> unexpectedBossTypes = new HashSet<int>();
    static readonly HashSet<int> firstObservedBossTypes = new HashSet<int>();
    static readonly List<Dictionary<string,object>> firstObservedBosses = new List<Dictionary<string,object>>();
    static Dictionary<string, object> equipmentReport;
    static string motionCase;
    static string flightCase;
    const int MotionWarmupFrames=20, MotionTotalFrames=180;
    const int FlightTotalFrames=600;
    static Dictionary<string,object> motionFrame;
    static int motionPlayerCalls, motionJumpCalls, motionInputReplays, motionRecordedFrames;
    static bool motionCloudConsumed, motionSawAirborne, motionReturnedGround;
    static float motionMinimumY=float.MaxValue;
    static bool IsMotion { get { return scenario!=null && scenario.Motion; } }
    static bool IsFlight { get { return scenario!=null && scenario.Flight; } }
    static bool IsScopeNegative { get { return scenario!=null && scenario.ScopeNegative; } }
    // Explicitly scope the additional observers to real Boss cases. Flight is
    // currently also Motion, but keep both exclusions so a future scenario
    // cannot accidentally add observer state/overhead by omitting that coupling.
    static bool IsBattleObservation { get { return scenario!=null && !scenario.Motion && !scenario.Flight &&
        (scenario.ScopeNegative || (scenario.BossTypes!=null && scenario.BossTypes.Length>0)); } }
    static int flightWingCalls, flightTotalWingCalls, flightRecordedFrames;
    static int flightEmptyHeldDescentFrames, flightReleasedAirborneFrames, flightHeldGroundEmptyFrames, flightReleasedGroundFullFrames;
    static int flightRocketConversions, flightFirstRocketConversion=-1, flightFirstExhaustion=-1, flightPoweredAfterRepress;
    static int flightFeatherPoweredFrames, flightFeatherNeutralFrames, flightFeatherUpFrames, flightFeatherDownFrames;
    static bool flightCloudConsumed, flightSawAirborne, flightReturnedGround;
    static float flightMinimumY=float.MaxValue, flightMaximumWingTime;
    static readonly Type runtimeType = typeof(Chaite.Plugin.Runtime);
    static readonly FieldInfo runtimeTotalField = runtimeType.GetField("_timingTotal", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo runtimeFramesField = runtimeType.GetField("_timingFrames", BindingFlags.Static | BindingFlags.NonPublic);
    // Test-copy observers only. ApplyPlan hooks copy values, never replay or
    // alter controls. Serialization/IO happens after native update, not here.
    const int BattleObservationInterval=60, BattleObservationEdgeInterval=15;
    const int BattleObservationMaximumRows=2048, BattleObservationMaximumNpcs=48;
    // A dense frame trace is one row per tick, and a full fight runs well past the
    // sampled cap, so the limit has to move with the mode rather than being fixed.
    const int BattleObservationDenseMaximumRows=65536;
    static int BattleObservationRowLimit
    {
        get { return scenario!=null && scenario.DenseFrames
            ? BattleObservationDenseMaximumRows : BattleObservationMaximumRows; }
    }
    static Chaite.Core.ControlPlan observedPlan;
    static string observedFormulaRoute;
    static int formulaRouteMismatches;
    static PlanTargetObservation observedPlanTarget;
    static bool hasObservedPlan, observedPlanPending, hasObservedPlanReturn;
    static int observedPlanTick=-1, observedPlanReturnTick=-1, observedPlanCalls, observedPlanReturns, observedPlanUnpaired;
    static bool observedControlUseItem, observedControlUseTile, observedControlJump, observedControlHook;
    static bool observedControlLeft, observedControlRight, observedControlUp, observedControlDown;
    static bool observedControlDash, observedControlMount, observedControlQuickHeal, observedControlQuickMana, observedControlThrow;
    static int observedSelectedItem, observedItemTime, observedItemAnimation, observedMouseX, observedMouseY;
    static int battleObservationReasons, battleObservationTransitions, battleObservationPendingTransitions;
    static int battleObservationRows, battleObservationLastTick=-1, battleObservationFlushes, battleObservationBufferedRows;
    static int battleObservationMaxOmittedNpcs, battleObservationTotalOmittedNpcs, battleObservationDroppedRows;
    static int battleObservationPeriodicRows, battleObservationTransitionRows, battleObservationFinalRows;
    static int battleObservationCoalescedTransitions, battleObservationMaximumBufferedCharacters, battleObservationCharactersWritten;
    static int battleObservationNpcSnapshots, battleObservationBossSnapshots, battleObservationQueenMinionSnapshots;
    static int battleObservationPrimeArmSnapshots, battleObservationTargetSnapshots, battleObservationPlanRows, battleObservationActualRows;
    static int poisonedFrames, lifeLossFramesWhilePoisoned;
    static int battlePreviousNativePhaseKey=17;
    static readonly StringBuilder battleObservationBuffer=new StringBuilder(65536);
    // Test-copy Player.Hurt observations. Calls only copy public native scalars,
    // immutable string references and entity snapshots into fixed storage;
    // text cleaning, JSON serialization and file IO happen after the native
    // update has returned. Motion/Flight never enter this scope.
    const int HurtObservationMaximumRows=2048, HurtObservationMaximumDepth=16;
    static readonly HurtObservationPending[] hurtObservationStack=new HurtObservationPending[HurtObservationMaximumDepth];
    static readonly HurtObservationRow[] hurtObservationData=new HurtObservationRow[HurtObservationMaximumRows];
    static readonly StringBuilder hurtObservationBuffer=new StringBuilder(65536);
    static int hurtObservationDepth, hurtObservationSuppressedDepth, hurtObservationMaximumDepth;
    static int hurtObservationCalls, hurtObservationReturns, hurtObservationRows, hurtObservationSerializedRows;
    static int hurtObservationDroppedRows, hurtObservationDepthOverflows, hurtObservationUnpaired, hurtObservationErrors;
    static int hurtObservationSourceReadFailures;
    static int hurtObservationFlushes, hurtObservationBufferedRows, hurtObservationMaximumBufferedCharacters, hurtObservationCharactersWritten;
    // Per-charge escape geometry. The periodic battle observation samples at best
    // one row every 15 ticks, which is far too coarse to see inside a charge that
    // lasts about 30 ticks, so the minimum distance of a charge cannot be
    // recovered from boss-observations.jsonl at all. This observer accumulates
    // the quantity the closed-loop work actually needs -- how close the Boss got
    // during each individual charge, and what the player was doing at that moment
    // -- every tick, and writes one row per charge.
    //
    // Every field is a copy of a native scalar taken after the native update, in
    // the same place the battle observation is taken. Nothing here feeds back
    // into control.
    const int ChargeObservationMaximumRows=4096;
    static readonly StringBuilder chargeObservationBuffer=new StringBuilder(16384);
    static bool chargeObservationActive;
    static int chargeObservationRows, chargeObservationFlushes, chargeObservationBufferedRows;
    static int chargeObservationCharactersWritten, chargeObservationMaximumBufferedCharacters;
    static int chargeObservationStartTick, chargeObservationState, chargeObservationSequence;
    static int chargeObservationHitsAtStart, chargeObservationMinTick;
    static float chargeObservationStartBossX, chargeObservationStartBossY;
    static float chargeObservationStartPlayerX, chargeObservationStartPlayerY;
    static float chargeObservationMinDistance, chargeObservationMinGapX, chargeObservationMinGapY;
    static float chargeObservationStartGapX, chargeObservationStartGapY;
    static float chargeObservationMinAxisTravel, chargeObservationMinPerpendicular;
    static float chargeObservationMinVelocityY, chargeObservationMinWingTime;
    static bool chargeObservationMinAirborne, chargeObservationMinImmune, chargeObservationDashUsed;
    static bool chargeObservationMinImmuneFlag, chargeObservationMinDashFlag;
    static int chargeObservationMinImmuneTime, chargeObservationMinHurtCooldown;
    static int chargeObservationMinDashType, chargeObservationMinEocDash;
    // Pre-hit rolling window. Two thirds of the measured hits land outside a
    // charge -- in the Detonating Bubble line, the Cthulhunado clear and the
    // standoff/personal-space body branches -- and the battle observation is far
    // too sparse to show what those frames look like. This keeps the last few
    // dozen ticks of geometry in memory and writes the window out only when a
    // hurt is actually recorded, so the file stays proportional to the damage
    // taken instead of to the length of the fight.
    //
    // The Fishron threats are NPCs, not projectiles: 371 Detonating Bubble,
    // 372/373 the Sharknado-generating bubbles, 384 the Sharknado column. The
    // instrument therefore scans Game.npc, which is what hurt.source.type has
    // been reporting all along.
    const int PreHitWindowTicks=48;
    const int PreHitMaximumHits=256;
    sealed class PreHitSample
    {
        public int Tick;
        public float PlayerX, PlayerY, PlayerVX, PlayerVY, WingTime;
        public int ImmuneTime;
        // immune (the bool that actually gates Player.Hurt) and hurtCooldowns are
        // separate from immuneTime, and the round that assumed immuneTime meant
        // "recently damaged" was wrong: samples show immuneTime above zero 892
        // and 2007 ticks after the last recorded hit. These fields are what can
        // actually distinguish the gates.
        public bool PlayerImmune;
        public int HurtCooldownMax, DashType, EocDash;
        // The applied controls, not the plan's intent. Without these a zero
        // velocity cannot be told apart from "the circuit chose not to move" and
        // "the circuit asked to move and something stopped it", which is exactly
        // the question the first pre-hit wave could not answer.
        public bool ControlLeft, ControlRight, ControlUp, ControlDown;
        public bool ControlJump, ControlDash, ControlHook;
        public int PlanTick;
        // The player's own input flags after the native update, plus the states
        // that can stop the player moving even though a direction was asked for.
        // The first control wave showed plans pressing left with velocity.X at
        // exactly zero, and those two readings are what separate "something
        // physically stopped it" from "the ask never reached the player".
        public bool PlayerControlLeft, PlayerControlRight, PlayerControlUp, PlayerControlDown;
        public bool Wet, HoneyWet, LavaWet, Slow;
        public float MoveSpeedDebuffFactor;
        public bool MountActive;
        public int MountType;
        public float BossX, BossY, BossVX, BossVY;
        public int BossState, BossTimer, BossSequence;
        public string Phase;
        public int ThreatType, ThreatLife, ThreatWidth, ThreatHeight;
        public float ThreatX, ThreatY, ThreatVX, ThreatVY, ThreatDistance;
        public int ThreatCount;
        public bool BossPresent;
        // This window was first written for Duke Fishron alone: it recognised a boss
        // only as type 370, counted threats only in 371..386, and walked Game.npc
        // without ever looking at Game.projectile. On every Empress run it therefore
        // reported no boss and no threat on all 31392 rows, and the projectile that
        // actually kills there could not appear at all. Reading that as "nothing was
        // coming" would have been wrong. The window is now scenario neutral and can
        // see projectiles, which the Empress fight is decided by.
        public int BossType;
        public float BossAi0, BossAi1, BossAi2, BossAi3;
        public bool ProjectilePresent;
        public int ProjectileType, ProjectileCount, ProjectileOwner, ProjectileTimeLeft;
        public bool ProjectileHostile;
        public float ProjectileX, ProjectileY, ProjectileVX, ProjectileVY, ProjectileDistance;
    }
    static readonly PreHitSample[] preHitRing=new PreHitSample[PreHitWindowTicks];
    static int preHitRingCount, preHitRingNext;
    static int preHitHits, preHitRows, preHitFlushes, preHitBufferedRows;
    static int preHitCharactersWritten, preHitMaximumBufferedCharacters, preHitLastHurtRows;
    static readonly StringBuilder preHitBuffer=new StringBuilder(65536);

    sealed class ScenarioSpec
    {
        public string Id;
        // This is an expected identity, never the reported identity by itself.
        // WriteResult derives result.variant again from observed native flags.
        public string ExpectedVariant;
        public int Summon;
        public int DirectSpawnType;
        public int[] BossTypes;
        public bool HardMode, Hallow, Jungle, Snow, Ocean, Legacy, Motion, Flight, DirectSpawn, Underworld, Daytime, PriorityArena, ScopeNegative;
        // A staged fight runs inside a real world, so natural spawns keep happening
        // around it. The scenarios that opt in stage a single boss that spawns no
        // adds, which makes every other active NPC a stray.
        public bool SuppressStrayNpcs;
        // Boss observations are sampled, which is right for describing a fight but
        // useless for deriving a player forward model: a model needs the state and
        // the applied input on every tick, not every fifteen to sixty. Opting in
        // here records one row per tick instead, which is what the closed-loop
        // search needs to predict motion without running the engine.
        public bool DenseFrames;
        public int[] ScopeNegativeRootTypes;
        public int SpawnLeadTicks;
        public int MaxLife;
        public string[] Phases;
        public string Equipment, EquipmentTier;
    }

    sealed class VariantObservation
    {
        public bool CapturedAtActivation;
        public int Tick=-1, GameMode=-1, NativeDifficulty=-1;
        public bool DayTime, HardMode, ForTheWorthy, ZenithWorld, DrunkWorld,
            NotTheBeesWorld, RemixWorld, CelebrationWorld, ConstantWorld,
            NoTrapsWorld, SkyblockWorld;
    }

    struct BossLifeSample
    {
        public int Type, Life;
    }

    struct BossLifeObservationRoot
    {
        public int Slot, Type, Life, LifeMax;
    }

    struct PlanTargetObservation
    {
        public bool Exists, Active, Boss;
        public int Slot, Type, Life, LifeMax;
    }

    struct HurtObservationPending
    {
        public int Sequence, Tick, PlayerIndex, LifeBefore, RequestedDamage, HitDirection, CooldownCounter;
        public bool Pvp, Quiet, Crit, Dodgeable;
        public string CustomReason, SourceKind;
        public int? SourceOtherIndex, DeclaredProjectileType, SourceEntityIndex, SourceType, SourceOwner;
        public int? SourceLife, SourceLifeMax, SourceDamage;
        public bool? SourceActive, SourceHostile, SourceFriendly;
        public float? SourceX, SourceY, SourceVelocityX, SourceVelocityY;
    }

    struct HurtObservationRow
    {
        public HurtObservationPending Before;
        public int ReturnTick, LifeAfter;
        public double ActualReturn;
    }
    static int ticks;
    static bool booted, failed, captured;
    static int lastLife, hits, lastBossLife, lastBossLifeObservedTick=-1, lastBossLifeExpectedRootCount;
    // Set the moment an active expected-root Boss is observed with zero life.
    // This is a direct observation rather than an inference from lastBossLife,
    // because lastBossLife is ALSO zero right after an episode reset -- so
    // "lastBossLife<=0" alone cannot tell "the Boss died" from "no Boss yet".
    static bool episodeBossKilled;
    static readonly List<BossLifeObservationRoot> lastBossLifeExpectedRoots=new List<BossLifeObservationRoot>(4);
    static int playerReturnedTick = -1, nativeFrames, maximumBossLife, maximumShots;
    static bool sawBoss, sawBossDamage, sawMovement, sawSummonConsumed;
    static bool directSpawnAttempted, directSpawnCompleted, phaseStageAttempted, phaseStaged, phaseVerifiedAtTakeover;
    static int directSpawnRootIndex=-1, actualTakeoverTick=-1;
    static Dictionary<string,object> phaseStageReport, takeoverNativeSnapshot;
    static Vector2 initialPosition;
    static int bulletReports;
    const int ScopeNegativeObservationFrames=1;
    static bool scopeNegativeActivationCaptured, scopeNegativeTopologyMatches;
    static int scopeNegativeActivationTick=-1, scopeNegativeSessionUnexpectedFrames;
    static string scopeNegativeSessionBefore, scopeNegativeSessionAfter;
    static readonly List<Dictionary<string,object>> scopeNegativeRootsAtActivation=new List<Dictionary<string,object>>(4);
    static readonly List<string> scopeNegativeChatMessages=new List<string>(8);
    static int scopeNegativeCueCalls, scopeNegativeUnsupportedCueCalls;
    static int scopeNegativeChatCalls, scopeNegativeExactChatCalls;
    static int scopeNegativeSummonBeforeType, scopeNegativeSummonBeforeStack;
    static int scopeNegativeAmmoBeforeType, scopeNegativeAmmoBeforeStack, scopeNegativeSelectedBefore;
    static int scopeNegativeActionableControlFrames, scopeNegativeOwnedFriendlyProjectileObservations;
    static int scopeNegativeSelectedChangeFrames, scopeNegativeSummonChangeFrames, scopeNegativeAmmoChangeFrames;
    static int scopeNegativeItemUseFrames, scopeNegativeLifeLossFrames;
    static int scopeNegativeLeftFrames, scopeNegativeRightFrames, scopeNegativeUpFrames, scopeNegativeDownFrames;
    static int scopeNegativeJumpFrames, scopeNegativeHookFrames, scopeNegativeDashFrames, scopeNegativeMountFrames;
    static int scopeNegativeUseItemFrames, scopeNegativeUseTileFrames, scopeNegativeThrowFrames;
    static int scopeNegativeQuickHealFrames, scopeNegativeQuickManaFrames;
    static Vector2 scopeNegativePositionBefore;
    static float scopeNegativeMaximumPositionDeltaSquared;
    // Bounded Queen projectile trace used only while calibrating the native
    // contact/escape controller.  It is deliberately kept out of the result
    // schema and is written to the isolated probe log, never to a user save.
    static int queenProjectileReports;
    public static void ProjectileKilled(Projectile projectile)
    {
        // This retained patch point deliberately does no synchronous logging.
        // The old diagnostic captured a formatted call stack for the first
        // five type-14 projectile deaths. That made a test-only kill hook
        // dominate the headless native-frame timing and could turn a combat
        // timeout into a harness timeout.  Shot identities are already
        // captured, with bounded rows, by ProjectileBeforeUpdate; a kill
        // stack is neither needed for correctness nor valid combat evidence.
    }
    static string lastSession;
    static Stopwatch clock = Stopwatch.StartNew();
    static readonly object logLock = new object();

    public static void Log(string text)
    {
        lock(logLock) File.AppendAllText(Path.Combine(Root, "game-probe.log"), DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine);
    }
    public static void ChatMessage(string text)
    {
        Log("CHAT "+text);
        if(!IsScopeNegative || !scopeNegativeActivationCaptured) return;
        scopeNegativeChatCalls++;
        if(scopeNegativeChatMessages.Count<8) scopeNegativeChatMessages.Add(text);
        string expected="[\u62C6\u7279] "+Chaite.Core.SupportedBossPolicy.UnsupportedBossMessage;
        if(string.Equals(text,expected,StringComparison.Ordinal)) scopeNegativeExactChatCalls++;
    }

    public static void ObserveAudioCue(Chaite.Core.AudioCue cue)
    {
        if(!IsScopeNegative || !scopeNegativeActivationCaptured) return;
        scopeNegativeCueCalls++;
        if(cue==Chaite.Core.AudioCue.UnsupportedBoss) scopeNegativeUnsupportedCueCalls++;
    }
    public static void ObservePlayerHurtBefore(Player player,Terraria.DataStructures.PlayerDeathReason reason,
        int requestedDamage,int hitDirection,bool pvp,bool quiet,bool crit,int cooldownCounter,bool dodgeable)
    {
        try
        {
            if(!booted || !IsBattleObservation || player==null || player.whoAmI!=0) return;
            int sequence=++hurtObservationCalls;
            if(hurtObservationSuppressedDepth>0 || hurtObservationDepth>=HurtObservationMaximumDepth)
            {
                hurtObservationSuppressedDepth++;
                hurtObservationDepthOverflows++;
                return;
            }
            var pending=new HurtObservationPending
            {
                Sequence=sequence,Tick=ticks,PlayerIndex=player.whoAmI,LifeBefore=player.statLife,
                RequestedDamage=requestedDamage,HitDirection=hitDirection,Pvp=pvp,Quiet=quiet,Crit=crit,
                CooldownCounter=cooldownCounter,Dodgeable=dodgeable
            };
            CaptureHurtReason(reason,player,ref pending);
            hurtObservationStack[hurtObservationDepth++]=pending;
            hurtObservationMaximumDepth=Math.Max(hurtObservationMaximumDepth,hurtObservationDepth);
        }
        catch(Exception) { hurtObservationErrors++; }
    }
    public static double ObservePlayerHurtAfter(double actualReturn,Player player)
    {
        try
        {
            if(booted && IsBattleObservation && player!=null && player.whoAmI==0)
            {
                hurtObservationReturns++;
                if(hurtObservationSuppressedDepth>0)
                {
                    hurtObservationSuppressedDepth--;
                    hurtObservationDroppedRows++;
                }
                else if(hurtObservationDepth<=0) hurtObservationUnpaired++;
                else
                {
                    var pending=hurtObservationStack[--hurtObservationDepth];
                    if(pending.PlayerIndex!=player.whoAmI) hurtObservationUnpaired++;
                    else if(hurtObservationRows>=HurtObservationMaximumRows) hurtObservationDroppedRows++;
                    else hurtObservationData[hurtObservationRows++]=new HurtObservationRow
                    {
                        Before=pending,ReturnTick=ticks,LifeAfter=player.statLife,ActualReturn=actualReturn
                    };
                }
            }
        }
        catch(Exception) { hurtObservationErrors++; }
        return actualReturn;
    }
    static void CaptureHurtReason(Terraria.DataStructures.PlayerDeathReason reason,Player player,ref HurtObservationPending pending)
    {
        if(reason==null) return;
        try
        {
            pending.CustomReason=reason.CustomReason;
            pending.SourceOtherIndex=reason.SourceOtherIndex;
            pending.DeclaredProjectileType=reason.SourceProjectileType;
            Entity source;
            if(reason.TryGetCausingEntity(out source) && source!=null)
            {
                pending.SourceEntityIndex=source.whoAmI;
                // Copy the public vectors by value before reading components;
                // this keeps generated IL on ldfld rather than taking an
                // address into native Entity storage.
                var sourcePosition=source.position;
                var sourceVelocity=source.velocity;
                pending.SourceX=sourcePosition.X;pending.SourceY=sourcePosition.Y;
                pending.SourceVelocityX=sourceVelocity.X;pending.SourceVelocityY=sourceVelocity.Y;
                var sourcePlayer=source as Player;
                var sourceNpc=source as NPC;
                var sourceProjectile=source as Projectile;
                if(sourcePlayer!=null)
                {
                    pending.SourceKind="player";
                    pending.SourceActive=sourcePlayer.active;
                    pending.SourceHostile=sourcePlayer.hostile;
                }
                else if(sourceNpc!=null)
                {
                    pending.SourceKind="npc";
                    pending.SourceType=sourceNpc.type;pending.SourceActive=sourceNpc.active;
                    pending.SourceLife=sourceNpc.life;pending.SourceLifeMax=sourceNpc.lifeMax;pending.SourceDamage=sourceNpc.damage;
                    pending.SourceFriendly=sourceNpc.friendly;
                }
                else if(sourceProjectile!=null)
                {
                    pending.SourceKind="projectile";
                    pending.SourceType=sourceProjectile.type;pending.SourceOwner=sourceProjectile.owner;
                    pending.SourceActive=sourceProjectile.active;pending.SourceDamage=sourceProjectile.damage;
                    pending.SourceHostile=sourceProjectile.hostile;pending.SourceFriendly=sourceProjectile.friendly;
                }
                // An unrecognized public Entity remains kind=null. Do not infer
                // a source from private PlayerDeathReason implementation fields.
            }
        }
        catch(Exception) { hurtObservationSourceReadFailures++; }
    }
    static string SafeDiagnosticText(string value)
    {
        if(String.IsNullOrEmpty(value)) return value;
        int length=Math.Min(240,value.Length);
        if(length>0 && length<value.Length && Char.IsHighSurrogate(value[length-1])) length--;
        var result=new StringBuilder(length);
        for(int i=0;i<length;i++) result.Append(Char.IsControl(value[i])?' ':value[i]);
        return result.ToString();
    }
    static void DrainHurtObservations()
    {
        if(!IsBattleObservation) return;
        while(hurtObservationSerializedRows<hurtObservationRows)
        {
            var row=hurtObservationData[hurtObservationSerializedRows++];
            var before=row.Before;
            var serialized=Json(new Dictionary<string,object>
            {
                {"schema","chaite-hurt-observation/v1"},{"sequence",before.Sequence},
                {"tickBefore",before.Tick},{"tickAfter",row.ReturnTick},
                {"reason",new Dictionary<string,object>
                    {
                        {"custom",SafeDiagnosticText(before.CustomReason)},
                        {"sourceOtherIndex",(object)before.SourceOtherIndex},
                        {"declaredProjectileType",(object)before.DeclaredProjectileType}
                    }},
                {"request",new Dictionary<string,object>
                    {
                        {"damage",before.RequestedDamage},{"hitDirection",before.HitDirection},{"pvp",before.Pvp},
                        {"quiet",before.Quiet},{"crit",before.Crit},{"cooldownCounter",before.CooldownCounter},{"dodgeable",before.Dodgeable}
                    }},
                {"actualReturn",row.ActualReturn},
                {"player",new Dictionary<string,object>
                    {
                        {"index",before.PlayerIndex},{"lifeBefore",before.LifeBefore},{"lifeAfter",row.LifeAfter},
                        {"lifeDelta",before.LifeBefore-row.LifeAfter}
                    }},
                {"source",new Dictionary<string,object>
                    {
                        {"kind",before.SourceKind},{"entityIndex",(object)before.SourceEntityIndex},{"type",(object)before.SourceType},
                        {"owner",(object)before.SourceOwner},{"active",(object)before.SourceActive},
                        {"life",(object)before.SourceLife},{"lifeMax",(object)before.SourceLifeMax},{"damage",(object)before.SourceDamage},
                        {"hostile",(object)before.SourceHostile},{"friendly",(object)before.SourceFriendly},
                        {"position",before.SourceX.HasValue?new Dictionary<string,object>{{"x",before.SourceX.Value},{"y",before.SourceY.Value}}:null},
                        {"velocity",before.SourceVelocityX.HasValue?new Dictionary<string,object>{{"x",before.SourceVelocityX.Value},{"y",before.SourceVelocityY.Value}}:null}
                    }}
            })+Environment.NewLine;
            if(hurtObservationBuffer.Length>0 && hurtObservationBuffer.Length+serialized.Length>65536) FlushHurtObservations();
            hurtObservationBuffer.Append(serialized);
            hurtObservationBufferedRows++;
            hurtObservationMaximumBufferedCharacters=Math.Max(hurtObservationMaximumBufferedCharacters,hurtObservationBuffer.Length);
            if(hurtObservationBufferedRows>=16 || hurtObservationBuffer.Length>=65536) FlushHurtObservations();
        }
    }
    static void FlushHurtObservations()
    {
        if(hurtObservationBuffer.Length==0) return;
        string payload=hurtObservationBuffer.ToString();
        File.AppendAllText(Path.Combine(Root,"hurt-observations.jsonl"),payload,new UTF8Encoding(false));
        hurtObservationCharactersWritten+=payload.Length;
        hurtObservationBuffer.Clear();
        hurtObservationBufferedRows=0;
        hurtObservationFlushes++;
    }
    static Dictionary<string,object> HurtObservationReport()
    {
        return new Dictionary<string,object>
        {
            {"schema","chaite-hurt-observation-summary/v1"},{"file",hurtObservationSerializedRows>0?"hurt-observations.jsonl":null},
            {"calls",hurtObservationCalls},{"returns",hurtObservationReturns},{"rows",hurtObservationRows},
            {"serializedRows",hurtObservationSerializedRows},{"maximumRows",HurtObservationMaximumRows},{"droppedRows",hurtObservationDroppedRows},
            {"maximumDepth",hurtObservationMaximumDepth},{"depthCapacity",HurtObservationMaximumDepth},
            {"depthOverflows",hurtObservationDepthOverflows},{"unpairedObservations",hurtObservationUnpaired},
            {"pendingDepth",hurtObservationDepth},{"suppressedPendingDepth",hurtObservationSuppressedDepth},
            {"observerErrors",hurtObservationErrors},{"sourceReadFailures",hurtObservationSourceReadFailures},
            {"flushes",hurtObservationFlushes},
            {"bufferFlushRows",16},{"bufferFlushCharacters",65536},{"maximumBufferedCharacters",hurtObservationMaximumBufferedCharacters},
            {"charactersWritten",hurtObservationCharactersWritten},{"bufferedRowsAfterFinalFlush",hurtObservationBufferedRows},
            {"scope","test-only local-player Player.Hurt calls in Boss scenarios; Motion/Flight excluded; fixed row/depth bounds; no game value writes"},
            {"sourcePolicy","only public PlayerDeathReason scalar/string properties, TryGetCausingEntity and public Player/NPC/Projectile fields are read; death-text generation is forbidden; unresolved values remain null"},
            {"timingCaveat","before/after scalar capture is inside native Hurt timing; JSON serialization and buffered file IO occur after native update"}
        };
    }
    public static void ObserveApplyPlanBefore(object player,Chaite.Core.ControlPlan plan)
    {
        if(!booted || !IsBattleObservation || !(player is Player) || ((Player)player).whoAmI!=0) return;
        var planTarget=new PlanTargetObservation { Slot=plan.TargetKey };
        if(Game.npc!=null && plan.TargetKey>=0 && plan.TargetKey<Game.npc.Length)
        {
            var target=Game.npc[plan.TargetKey];
            if(target!=null)
                planTarget=new PlanTargetObservation
                {
                    Exists=true,Slot=plan.TargetKey,Type=target.type,Active=target.active,Boss=target.boss,
                    Life=target.life,LifeMax=target.lifeMax
                };
        }
        if(observedPlanPending) observedPlanUnpaired++;
        if(!hasObservedPlan || observedPlan.FormulaRoute != plan.FormulaRoute ||
            !String.Equals(observedPlan.StrategyId,plan.StrategyId,StringComparison.Ordinal) ||
            !String.Equals(observedPlan.PhaseId,plan.PhaseId,StringComparison.Ordinal)) MarkBattleObservation(2);
        if(!hasObservedPlan || observedPlan.TargetKey!=plan.TargetKey || observedPlanTarget.Exists!=planTarget.Exists ||
            (planTarget.Exists && (observedPlanTarget.Type!=planTarget.Type || observedPlanTarget.Active!=planTarget.Active)))
            MarkBattleObservation(4);
        observedPlan=plan; // ControlPlan is a value type; no game/plan writes.
        var planFormulaRoute=plan.FormulaRoute.ToString();
        if(planFormulaRoute!="None")
        {
            if(observedFormulaRoute==null) observedFormulaRoute=planFormulaRoute;
            else if(observedFormulaRoute!=planFormulaRoute)
                formulaRouteMismatches++;
        }
        observedPlanTarget=planTarget;
        hasObservedPlan=true;
        observedPlanPending=true;
        hasObservedPlanReturn=false;
        observedPlanTick=ticks;
        observedPlanCalls++;
    }
    public static void ObserveApplyPlanAfter(object player)
    {
        if(!booted || !IsBattleObservation || !(player is Player) || ((Player)player).whoAmI!=0) return;
        var p=(Player)player;
        if(!observedPlanPending) { observedPlanUnpaired++; return; }
        if(observedPlanReturns==0 || observedControlUseItem!=p.controlUseItem) MarkBattleObservation(16);
        observedControlUseItem=p.controlUseItem;
        observedControlUseTile=p.controlUseTile;
        observedControlJump=p.controlJump;
        observedControlHook=p.controlHook;
        observedControlLeft=p.controlLeft;
        observedControlRight=p.controlRight;
        observedControlUp=p.controlUp;
        observedControlDown=p.controlDown;
        observedControlDash=p.controlDash;
        observedControlMount=p.controlMount;
        observedControlQuickHeal=p.controlQuickHeal;
        observedControlQuickMana=p.controlQuickMana;
        observedControlThrow=p.controlThrow;
        observedSelectedItem=p.selectedItem;
        observedItemTime=p.itemTime;
        observedItemAnimation=p.itemAnimation;
        observedMouseX=Game.mouseX;
        observedMouseY=Game.mouseY;
        observedPlanPending=false;
        hasObservedPlanReturn=true;
        observedPlanReturnTick=ticks;
        observedPlanReturns++;
    }
    static void MarkBattleObservation(int reason)
    {
        battleObservationReasons|=reason;
        battleObservationTransitions++;
        battleObservationPendingTransitions++;
    }
    static int BattleNativePhaseKey(int key,NPC npc)
    {
        // Only known discrete state fields, never continuously changing AI
        // clocks. Full ai/localAI arrays are retained at each sampled row.
        unchecked
        {
            key=key*31+npc.whoAmI;
            key=key*31+npc.type;
            if(npc.type==657)
            {
                key=key*31+npc.ai[0].GetHashCode();
                key=key*31+npc.ai[2].GetHashCode();
                key=key*31+(npc.life<=npc.lifeMax/2?1:0);
            }
            else if(npc.type==127) key=key*31+npc.ai[1].GetHashCode();
            else if(npc.type==4 || npc.type==125 || npc.type==126)
            {
                key=key*31+npc.ai[0].GetHashCode();
                key=key*31+npc.ai[1].GetHashCode();
            }
            else if(npc.type==50) key=key*31+npc.ai[1].GetHashCode();
            return key;
        }
    }
    /// <summary>Remaining ticks of BuffID.Inferno (116), or 0 when it is not up.</summary>
    static int InfernoTicks(Player p)
    {
        if(p==null || p.buffType==null || p.buffTime==null) return 0;
        for(int i=0;i<p.buffType.Length && i<p.buffTime.Length;i++)
            if(p.buffType[i]==116) return p.buffTime[i];
        return 0;
    }
    static bool BattleNpcIsPriority(NPC npc)
    {
        return npc!=null && ((npc.active && npc.boss) ||
            (hasObservedPlan && observedPlanTarget.Exists && npc.whoAmI==observedPlanTarget.Slot && npc.type==observedPlanTarget.Type));
    }
    static bool BattleNpcIsSecondary(NPC npc)
    {
        return npc!=null && npc.active && ((npc.type>=128 && npc.type<=131) || (npc.type>=658 && npc.type<=660));
    }
    static void CountBattleNpcObservation(NPC npc)
    {
        battleObservationNpcSnapshots++;
        if(npc.active && npc.boss) battleObservationBossSnapshots++;
        if(npc.active && npc.type>=658 && npc.type<=660) battleObservationQueenMinionSnapshots++;
        if(npc.active && npc.type>=128 && npc.type<=131) battleObservationPrimeArmSnapshots++;
        if(hasObservedPlan && observedPlanTarget.Exists && npc.whoAmI==observedPlanTarget.Slot && npc.type==observedPlanTarget.Type)
            battleObservationTargetSnapshots++;
    }
    static Dictionary<string,object> BattleNpcObservation(NPC npc)
    {
        return new Dictionary<string,object>
        {
            {"slot",npc.whoAmI},{"type",npc.type},{"active",npc.active},{"boss",npc.boss},
            {"life",npc.life},{"lifeMax",npc.lifeMax},{"targetPlayer",npc.target},
            {"ai",(float[])npc.ai.Clone()},{"localAI",(float[])npc.localAI.Clone()},
            {"position",new Dictionary<string,object>{{"x",npc.position.X},{"y",npc.position.Y}}},
            {"velocity",new Dictionary<string,object>{{"x",npc.velocity.X},{"y",npc.velocity.Y}}},
            {"width",npc.width},{"height",npc.height},{"timeLeft",npc.timeLeft},{"dontTakeDamage",npc.dontTakeDamage}
        };
    }
    static void ObserveBattleAfterNative(int nativePhaseKey)
    {
        if(!IsBattleObservation) return;
        DrainHurtObservations();
        ObservePreHitWindow();
        ObserveChargeEscape();
        if(nativePhaseKey!=battlePreviousNativePhaseKey) MarkBattleObservation(8);
        battlePreviousNativePhaseKey=nativePhaseKey;
        CaptureBattleObservation(false);
    }

    /// <summary>Pushes one tick of geometry into the rolling window, then writes
    /// the window out for every hurt recorded on this tick. The window ends at
    /// the hurt frame itself, so the rows have negative offsets up to zero and
    /// the last row is the state the damage was applied in.</summary>
    static void ObservePreHitWindow()
    {
        if(!booted || Game.player==null || Game.player.Length==0 || Game.player[0]==null) return;
        var p=Game.player[0];
        float pcx=p.position.X+p.width*0.5f, pcy=p.position.Y+p.height*0.5f;
        var sample=preHitRing[preHitRingNext];
        if(sample==null) sample=preHitRing[preHitRingNext]=new PreHitSample();
        preHitRingNext=(preHitRingNext+1)%PreHitWindowTicks;
        if(preHitRingCount<PreHitWindowTicks) preHitRingCount++;
        sample.Tick=ticks;
        sample.PlayerX=pcx; sample.PlayerY=pcy;
        sample.PlayerVX=p.velocity.X; sample.PlayerVY=p.velocity.Y;
        sample.WingTime=p.wingTime; sample.ImmuneTime=p.immuneTime;
        sample.PlayerImmune=p.immune;
        sample.HurtCooldownMax=MaxHurtCooldown(p);
        sample.DashType=p.dashType; sample.EocDash=p.eocDash;
        // PlanTick lets a reader discard stale controls: the observer copies the
        // last applied plan, and on a tick with no applied plan those bits are
        // from an earlier tick rather than from this one.
        sample.PlanTick=observedPlanTick;
        sample.ControlLeft=observedControlLeft; sample.ControlRight=observedControlRight;
        sample.ControlUp=observedControlUp; sample.ControlDown=observedControlDown;
        sample.ControlJump=observedControlJump; sample.ControlDash=observedControlDash;
        sample.ControlHook=observedControlHook;
        sample.PlayerControlLeft=p.controlLeft; sample.PlayerControlRight=p.controlRight;
        sample.PlayerControlUp=p.controlUp; sample.PlayerControlDown=p.controlDown;
        sample.Wet=p.wet; sample.HoneyWet=p.honeyWet; sample.LavaWet=p.lavaWet;
        sample.Slow=p.slow; sample.MoveSpeedDebuffFactor=p.strongestMoveSpeedDebuff;
        sample.MountActive=p.mount.Active; sample.MountType=p.mount.Type;
        sample.Phase=hasObservedPlan?observedPlan.PhaseId:null;
        sample.BossPresent=false;
        sample.ThreatType=0; sample.ThreatDistance=float.MaxValue;
        sample.ThreatCount=0;
        if(Game.npc!=null)
            foreach(var npc in Game.npc)
            {
                if(npc==null || !npc.active) continue;
                float ncx=npc.position.X+npc.width*0.5f, ncy=npc.position.Y+npc.height*0.5f;
                float dx=ncx-pcx, dy=ncy-pcy;
                float d=(float)Math.Sqrt(dx*dx+dy*dy);
                if(npc.boss)
                {
                    sample.BossPresent=true;
                    sample.BossType=npc.type;
                    sample.BossX=ncx; sample.BossY=ncy;
                    sample.BossVX=npc.velocity.X; sample.BossVY=npc.velocity.Y;
                    sample.BossAi0=npc.ai[0]; sample.BossAi1=npc.ai[1];
                    sample.BossAi2=npc.ai[2]; sample.BossAi3=npc.ai[3];
                    sample.BossState=(int)npc.ai[0]; sample.BossTimer=(int)npc.ai[1];
                    sample.BossSequence=(int)npc.ai[2];
                    continue;
                }
                // This build has no NPC.hostile field, so an enemy is recognised as
                // neither friendly (which covers the player's own minions), nor a
                // town NPC, nor a critter.
                if(npc.friendly || npc.townNPC || npc.CountsAsACritter) continue;
                if(d<400f) sample.ThreatCount++;
                if(d<sample.ThreatDistance)
                {
                    sample.ThreatDistance=d;
                    sample.ThreatType=npc.type; sample.ThreatLife=npc.life;
                    sample.ThreatX=ncx; sample.ThreatY=ncy;
                    sample.ThreatVX=npc.velocity.X; sample.ThreatVY=npc.velocity.Y;
                    sample.ThreatWidth=npc.width; sample.ThreatHeight=npc.height;
                }
            }
        // Projectiles are what actually kill in the Empress fight and the window
        // never looked at them. The nearest hostile one is recorded per tick, so the
        // approach that ends the run can be read back rather than guessed at.
        sample.ProjectilePresent=false;
        sample.ProjectileDistance=float.MaxValue; sample.ProjectileCount=0;
        if(Game.projectile!=null)
            foreach(var pr in Game.projectile)
            {
                if(pr==null || !pr.active || !pr.hostile) continue;
                float qx=pr.position.X+pr.width*0.5f, qy=pr.position.Y+pr.height*0.5f;
                float qdx=qx-pcx, qdy=qy-pcy;
                float qd=(float)Math.Sqrt(qdx*qdx+qdy*qdy);
                if(qd<400f) sample.ProjectileCount++;
                if(qd<sample.ProjectileDistance)
                {
                    sample.ProjectilePresent=true;
                    sample.ProjectileDistance=qd;
                    sample.ProjectileType=pr.type; sample.ProjectileOwner=pr.owner;
                    sample.ProjectileTimeLeft=pr.timeLeft;
                    sample.ProjectileHostile=pr.hostile;
                    sample.ProjectileX=qx; sample.ProjectileY=qy;
                    sample.ProjectileVX=pr.velocity.X; sample.ProjectileVY=pr.velocity.Y;
                }
            }
        if(hurtObservationRows<=preHitLastHurtRows) return;
        for(int index=preHitLastHurtRows;index<hurtObservationRows;index++)
        {
            if(preHitHits>=PreHitMaximumHits) break;
            var row=hurtObservationData[index];
            WritePreHitWindow(row.Before.Sequence,row.ReturnTick);
            preHitHits++;
        }
        preHitLastHurtRows=hurtObservationRows;
    }
    static void WritePreHitWindow(int hurtSequence,int hurtTick)
    {
        int count=preHitRingCount;
        for(int back=count-1;back>=0;back--)
        {
            int slot=((preHitRingNext-1-back)%PreHitWindowTicks+PreHitWindowTicks)%PreHitWindowTicks;
            var s=preHitRing[slot];
            if(s==null) continue;
            var row=new Dictionary<string,object>
            {
                {"schema","chaite-prehit-observation/v2"},
                {"hurtSequence",hurtSequence},{"hurtTick",hurtTick},
                {"tick",s.Tick},{"offsetTicks",s.Tick-hurtTick},
                {"phase",s.Phase},
                {"player",new Dictionary<string,object>
                    {
                        {"x",s.PlayerX},{"y",s.PlayerY},{"vx",s.PlayerVX},{"vy",s.PlayerVY},
                        {"wingTime",s.WingTime},{"immuneTime",s.ImmuneTime},
                        {"immune",s.PlayerImmune},{"hurtCooldownMax",s.HurtCooldownMax},
                        {"dashType",s.DashType},{"eocDash",s.EocDash},
                        {"planTick",s.PlanTick},{"controlsFresh",s.PlanTick==s.Tick},
                        {"left",s.ControlLeft},{"right",s.ControlRight},
                        {"up",s.ControlUp},{"down",s.ControlDown},
                        {"jump",s.ControlJump},{"dash",s.ControlDash},{"hook",s.ControlHook},
                        {"playerLeft",s.PlayerControlLeft},{"playerRight",s.PlayerControlRight},
                        {"playerUp",s.PlayerControlUp},{"playerDown",s.PlayerControlDown},
                        {"wet",s.Wet},{"honeyWet",s.HoneyWet},{"lavaWet",s.LavaWet},
                        {"slow",s.Slow},{"moveSpeedDebuffFactor",s.MoveSpeedDebuffFactor},
                        {"mountActive",s.MountActive},{"mountType",s.MountType}
                    }},
                {"boss",s.BossPresent?new Dictionary<string,object>
                    {
                        {"type",s.BossType},
                        {"x",s.BossX},{"y",s.BossY},{"vx",s.BossVX},{"vy",s.BossVY},
                        {"ai0",s.BossAi0},{"ai1",s.BossAi1},{"ai2",s.BossAi2},{"ai3",s.BossAi3},
                        {"state",s.BossState},{"timer",s.BossTimer},{"sequence",s.BossSequence}
                    }:null},
                {"nearestThreat",s.ThreatType==0?null:new Dictionary<string,object>
                    {
                        {"type",s.ThreatType},{"x",s.ThreatX},{"y",s.ThreatY},
                        {"vx",s.ThreatVX},{"vy",s.ThreatVY},{"distance",s.ThreatDistance},
                        {"life",s.ThreatLife},{"width",s.ThreatWidth},{"height",s.ThreatHeight}
                    }},
                {"threatsWithin400",s.ThreatCount},
                {"nearestProjectile",s.ProjectilePresent?new Dictionary<string,object>
                    {
                        {"type",s.ProjectileType},{"owner",s.ProjectileOwner},
                        {"x",s.ProjectileX},{"y",s.ProjectileY},
                        {"vx",s.ProjectileVX},{"vy",s.ProjectileVY},
                        {"distance",s.ProjectileDistance},{"timeLeft",s.ProjectileTimeLeft},
                        {"hostile",s.ProjectileHostile}
                    }:null},
                {"projectilesWithin400",s.ProjectileCount}
            };
            string serialized=Json(row)+Environment.NewLine;
            if(preHitBuffer.Length>0 && preHitBuffer.Length+serialized.Length>65536) FlushPreHitWindow();
            preHitBuffer.Append(serialized);
            preHitRows++;
            preHitBufferedRows++;
            preHitMaximumBufferedCharacters=Math.Max(preHitMaximumBufferedCharacters,preHitBuffer.Length);
            if(preHitBufferedRows>=16 || preHitBuffer.Length>=65536) FlushPreHitWindow();
        }
    }
    static int MaxHurtCooldown(Player p)
    {
        if(p.hurtCooldowns==null) return 0;
        int max=0;
        foreach(var value in p.hurtCooldowns) if(value>max) max=value;
        return max;
    }
    static void FlushPreHitWindow()
    {
        if(preHitBuffer.Length==0) return;
        string payload=preHitBuffer.ToString();
        File.AppendAllText(Path.Combine(Root,"prehit-observations.jsonl"),payload,new UTF8Encoding(false));
        preHitCharactersWritten+=payload.Length;
        preHitBuffer.Clear();
        preHitBufferedRows=0;
        preHitFlushes++;
    }
    static Dictionary<string,object> PreHitObservationReport()
    {
        return new Dictionary<string,object>
        {
            {"schema","chaite-prehit-observation-summary/v1"},
            {"file",preHitRows>0?"prehit-observations.jsonl":null},
            {"rows",preHitRows},{"hits",preHitHits},{"maximumHits",PreHitMaximumHits},
            {"windowTicks",PreHitWindowTicks},{"flushes",preHitFlushes},
            {"bufferFlushRows",16},{"bufferFlushCharacters",65536},
            {"maximumBufferedCharacters",preHitMaximumBufferedCharacters},
            {"charactersWritten",preHitCharactersWritten}
        };
    }

    /// <summary>One row per AI_069 charge, closing when the charge ends.
    ///
    /// The axes are fixed at the charge's first tick: axisX/axisY are the unit
    /// vector from the Boss to the player then, so "along" is positive when the
    /// player moves away from the Boss on the charge's own line of travel and
    /// "perpendicular" is the direction the charge cannot correct. That is what
    /// distinguishes a flee that works from one that only looks like it works.
    /// Measured on the reviewed circuit, the player's along-axis travel at the
    /// frame of closest approach is 150-355 px while the Boss's own travel is
    /// 476 px in phase one, 567 in phase two and 675 in phase three -- so a
    /// charge is never won by the horizontal flee, it is a give and take inside
    /// the 60-tick cadence.</summary>
    static void ObserveChargeEscape()
    {
        if(!booted || Game.player==null || Game.player.Length==0 || Game.player[0]==null || Game.npc==null) return;
        var p=Game.player[0];
        NPC boss=null;
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active && npc.boss && npc.type==370) { boss=npc; break; }
        if(boss==null)
        {
            if(chargeObservationActive) CloseChargeObservation(false);
            return;
        }
        int state=(int)boss.ai[0];
        bool charging=state==1 || state==6 || state==11;
        if(!charging)
        {
            if(chargeObservationActive) CloseChargeObservation(false);
            return;
        }
        float pcx=p.position.X+p.width*0.5f, pcy=p.position.Y+p.height*0.5f;
        float bcx=boss.position.X+boss.width*0.5f, bcy=boss.position.Y+boss.height*0.5f;
        float gapX=pcx-bcx, gapY=pcy-bcy;
        float distance=(float)Math.Sqrt(gapX*gapX+gapY*gapY);
        if(!chargeObservationActive)
        {
            chargeObservationActive=true;
            chargeObservationStartTick=ticks;
            chargeObservationState=state;
            chargeObservationSequence=(int)boss.ai[3];
            chargeObservationHitsAtStart=hurtObservationRows;
            chargeObservationStartBossX=bcx; chargeObservationStartBossY=bcy;
            chargeObservationStartPlayerX=pcx; chargeObservationStartPlayerY=pcy;
            chargeObservationStartGapX=gapX; chargeObservationStartGapY=gapY;
            chargeObservationDashUsed=p.controlDash;
            // The first sample is the charge's own first tick, so there is no
            // previous distance to compare against and the projections start at
            // zero travel.
            chargeObservationMinDistance=distance;
            chargeObservationMinTick=ticks;
            chargeObservationMinGapX=gapX; chargeObservationMinGapY=gapY;
            chargeObservationMinAxisTravel=0f; chargeObservationMinPerpendicular=0f;
            chargeObservationMinVelocityY=p.velocity.Y;
            chargeObservationMinWingTime=p.wingTime;
            chargeObservationMinAirborne=p.wingTime>0f;
            chargeObservationMinImmune=p.immuneTime>0;
            CaptureChargeImmuneState(p);
            return;
        }
        if(p.controlDash) chargeObservationDashUsed=true;
        if(distance<chargeObservationMinDistance)
        {
            // The direction from the player to the Boss at the charge's first
            // tick is the line the charge committed to.
            float startLength=(float)Math.Sqrt(chargeObservationStartGapX*chargeObservationStartGapX+
                chargeObservationStartGapY*chargeObservationStartGapY);
            float axisX=startLength>0f?chargeObservationStartGapX/startLength:1f;
            float axisY=startLength>0f?chargeObservationStartGapY/startLength:0f;
            float travelX=pcx-chargeObservationStartPlayerX, travelY=pcy-chargeObservationStartPlayerY;
            chargeObservationMinDistance=distance;
            chargeObservationMinTick=ticks;
            chargeObservationMinGapX=gapX; chargeObservationMinGapY=gapY;
            chargeObservationMinAxisTravel=travelX*axisX+travelY*axisY;
            chargeObservationMinPerpendicular=Math.Abs(travelX*-axisY+travelY*axisX);
            chargeObservationMinVelocityY=p.velocity.Y;
            chargeObservationMinWingTime=p.wingTime;
            chargeObservationMinAirborne=p.wingTime>0f;
            chargeObservationMinImmune=p.immuneTime>0;
            CaptureChargeImmuneState(p);
        }
    }
    static void CaptureChargeImmuneState(Player p)
    {
        chargeObservationMinImmuneFlag=p.immune;
        chargeObservationMinImmuneTime=p.immuneTime;
        chargeObservationMinHurtCooldown=MaxHurtCooldown(p);
        chargeObservationMinDashType=p.dashType;
        chargeObservationMinEocDash=p.eocDash;
        chargeObservationMinDashFlag=p.controlDash;
    }
    static void CloseChargeObservation(bool final)
    {
        if(!chargeObservationActive) return;
        chargeObservationActive=false;
        var row=new Dictionary<string,object>
        {
            {"schema","chaite-charge-observation/v1"},{"charge",chargeObservationRows+1},
            {"startTick",chargeObservationStartTick},{"endTick",ticks},
            {"ticks",ticks-chargeObservationStartTick},
            {"state",chargeObservationState},{"sequence",chargeObservationSequence},
            {"hitsDuringCharge",hurtObservationRows-chargeObservationHitsAtStart},
            {"distanceAtStart",(float)Math.Sqrt(chargeObservationStartGapX*chargeObservationStartGapX+
                chargeObservationStartGapY*chargeObservationStartGapY)},
            {"gapXAtStart",chargeObservationStartGapX},{"gapYAtStart",chargeObservationStartGapY},
            {"minDistance",chargeObservationMinDistance},{"minDistanceTick",chargeObservationMinTick},
            {"minGapX",chargeObservationMinGapX},{"minGapY",chargeObservationMinGapY},
            // Travel of the player, measured from the charge's first tick up to
            // the frame of closest approach, split into the component along the
            // charge's committed line and the component across it.
            {"playerTravelAlongAxisAtMin",chargeObservationMinAxisTravel},
            {"playerTravelPerpendicularAtMin",chargeObservationMinPerpendicular},
            {"playerVelocityYAtMin",chargeObservationMinVelocityY},
            {"playerWingTimeAtMin",chargeObservationMinWingTime},
            {"playerAirborneAtMin",chargeObservationMinAirborne},
            {"playerImmuneAtMin",chargeObservationMinImmune},
            {"playerImmuneFlagAtMin",chargeObservationMinImmuneFlag},
            {"playerImmuneTimeAtMin",chargeObservationMinImmuneTime},
            {"playerHurtCooldownAtMin",chargeObservationMinHurtCooldown},
            {"playerDashTypeAtMin",chargeObservationMinDashType},
            {"playerEocDashAtMin",chargeObservationMinEocDash},
            {"dashUsedDuringCharge",chargeObservationDashUsed},
            // The Boss's own displacement over the charge, measured per charge
            // rather than taken from the source, so the reach is a measurement.
            {"bossTravel",ChargeObservationBossTravel()},
            {"final",final}
        };
        string serialized=Json(row)+Environment.NewLine;
        if(chargeObservationBuffer.Length>0 && chargeObservationBuffer.Length+serialized.Length>65536)
            FlushChargeObservations();
        chargeObservationBuffer.Append(serialized);
        chargeObservationRows++;
        chargeObservationBufferedRows++;
        chargeObservationMaximumBufferedCharacters=Math.Max(chargeObservationMaximumBufferedCharacters,
            chargeObservationBuffer.Length);
        if(chargeObservationBufferedRows>=16 || chargeObservationBuffer.Length>=65536)
            FlushChargeObservations();
    }
    static float ChargeObservationBossTravel()
    {
        if(Game.npc==null) return 0f;
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active && npc.boss && npc.type==370)
            {
                float bcx=npc.position.X+npc.width*0.5f, bcy=npc.position.Y+npc.height*0.5f;
                float dx=bcx-chargeObservationStartBossX, dy=bcy-chargeObservationStartBossY;
                return (float)Math.Sqrt(dx*dx+dy*dy);
            }
        return 0f;
    }
    static void FlushChargeObservations()
    {
        if(chargeObservationBuffer.Length==0) return;
        string payload=chargeObservationBuffer.ToString();
        File.AppendAllText(Path.Combine(Root,"charge-observations.jsonl"),payload,new UTF8Encoding(false));
        chargeObservationCharactersWritten+=payload.Length;
        chargeObservationBuffer.Clear();
        chargeObservationBufferedRows=0;
        chargeObservationFlushes++;
    }
    static Dictionary<string,object> ChargeObservationReport()
    {
        return new Dictionary<string,object>
        {
            {"schema","chaite-charge-observation-summary/v1"},
            {"file",chargeObservationRows>0?"charge-observations.jsonl":null},
            {"rows",chargeObservationRows},{"maximumRows",ChargeObservationMaximumRows},
            {"flushes",chargeObservationFlushes},
            {"bufferFlushRows",16},{"bufferFlushCharacters",65536},
            {"maximumBufferedCharacters",chargeObservationMaximumBufferedCharacters},
            {"charactersWritten",chargeObservationCharactersWritten}
        };
    }
    /// <summary>Every active hostile projectile, nearest the player first.
    ///
    /// Bounded, with the count and the omitted count both reported, so a
    /// consumer that finds omissions refuses instead of assuming the field was
    /// complete. A hit test that silently lost a projectile would report a
    /// clean tick, which is the one failure this whole method exists to avoid.
    /// </summary>
    static void CaptureHostileProjectiles(List<Dictionary<string,object>> into,
        out int count,out int omitted)
    {
        const int maximumHostileProjectiles=48;
        count=0; omitted=0;
        if(Game.projectile==null || Game.player==null || Game.player.Length==0 ||
            Game.player[0]==null) return;
        var player=Game.player[0];
        // Nearest first, so truncation drops what cannot reach the player rather
        // than whatever happens to sit at a low slot index.
        var candidates=new List<Projectile>();
        foreach(var projectile in Game.projectile)
        {
            if(projectile==null || !projectile.active || !projectile.hostile ||
                projectile.friendly) continue;
            count++;
            candidates.Add(projectile);
        }
        candidates.Sort((left,right)=>
            Vector2.DistanceSquared(left.Center,player.Center).CompareTo(
                Vector2.DistanceSquared(right.Center,player.Center)));
        foreach(var projectile in candidates)
        {
            if(into.Count>=maximumHostileProjectiles) { omitted++; continue; }
            var entry=new Dictionary<string,object>
            {
                {"slot",projectile.whoAmI},{"type",projectile.type},
                {"x",projectile.position.X},{"y",projectile.position.Y},
                {"vx",projectile.velocity.X},{"vy",projectile.velocity.Y},
                {"width",projectile.width},{"height",projectile.height},
                {"damage",projectile.damage},{"owner",projectile.owner},
                {"timeLeft",projectile.timeLeft},
                {"extraUpdates",projectile.extraUpdates},
                // The first two AI slots drive most hostile motion; the
                // reviewed threat model reads them rather than guessing a
                // trajectory from velocity alone.
                {"ai0",projectile.ai[0]},{"ai1",projectile.ai[1]},
                // The rest of what ThreatSnapshot needs. Without these the
                // reviewed motion model cannot be driven from a trace at all:
                // it fails closed on an unknown trajectory, so an unrecorded
                // field is a threat that cannot be modelled rather than one
                // that is modelled wrongly. Projectile.ai holds three entries
                // and localAI two, which is why there is no ai3 here.
                {"ai2",projectile.ai[2]},
                {"localAI0",projectile.localAI[0]},{"localAI1",projectile.localAI[1]},
                {"direction",projectile.direction},{"scale",projectile.scale}
            };
            into.Add(entry);
        }
    }
    static void CaptureBattleObservation(bool final)
    {        if(!booted || !IsBattleObservation || Game.player==null || Game.player.Length==0 || Game.player[0]==null || Game.npc==null) return;
        bool periodic=ticks%BattleObservationInterval==0;
        bool dense=scenario!=null && scenario.DenseFrames;
        if(final) battleObservationReasons|=32;
        if(!final && !periodic && !dense && (battleObservationReasons==0 || ticks-battleObservationLastTick<BattleObservationEdgeInterval)) return;
        // Reserve one row for the terminal state. A final row may intentionally
        // share a tick with the preceding edge/periodic row; it proves the exact
        // state passed to WriteResult rather than silently losing termination.
        if(!final && ticks==battleObservationLastTick) return;
        if(battleObservationRows>=BattleObservationRowLimit || (!final && battleObservationRows>=BattleObservationRowLimit-1))
        {
            battleObservationDroppedRows++;
            return;
        }
        var p=Game.player[0];
        var npcs=new List<Dictionary<string,object>>(BattleObservationMaximumNpcs);
        int omitted=0;
        for(int pass=0;pass<2;pass++)
            foreach(var npc in Game.npc)
            {
                bool priority=BattleNpcIsPriority(npc);
                if(pass==0 ? !priority : priority || !BattleNpcIsSecondary(npc)) continue;
                if(npcs.Count<BattleObservationMaximumNpcs)
                {
                    npcs.Add(BattleNpcObservation(npc));
                    CountBattleNpcObservation(npc);
                }
                else omitted++;
            }
        battleObservationMaxOmittedNpcs=Math.Max(battleObservationMaxOmittedNpcs,omitted);
        battleObservationTotalOmittedNpcs+=omitted;

        // Hostile projectiles, after the native update.
        //
        // The dense row recorded NPCs and not projectiles, and this project has
        // already been burned once by an observation that ignored
        // Game.projectile: the Empress fight is decided by projectiles, and the
        // report claimed no threat on every one of 31392 rows.
        //
        // The post-update list alone is not enough to score a hit, and that is
        // not a detail. A hit is what removes the projectile, because native
        // kills it on contact, so by the time this row exists the projectile
        // that caused the hit is already gone. Calibrating the post-update list
        // against a real fight found none of eleven hits. The pre-update list is
        // captured in BeforeUpdate for exactly this reason.
        var hostileProjectiles=new List<Dictionary<string,object>>();
        int hostileProjectileCount,omittedHostileProjectiles;
        CaptureHostileProjectiles(hostileProjectiles,out hostileProjectileCount,
            out omittedHostileProjectiles);
        Dictionary<string,object> plan=null,actual=null;
        if(hasObservedPlan)
            plan=new Dictionary<string,object>
            {
                {"tick",observedPlanTick},{"strategy",observedPlan.StrategyId},{"phase",observedPlan.PhaseId},
                {"formulaRoute",observedPlan.FormulaRoute.ToString()},
                {"target",observedPlan.TargetKey},{"fire",observedPlan.Fire},{"hook",observedPlan.Hook},
                {"selectedTargetAtPlan",new Dictionary<string,object>
                    {
                        {"slot",observedPlanTarget.Slot},{"exists",observedPlanTarget.Exists},
                        {"type",observedPlanTarget.Exists?(object)observedPlanTarget.Type:null},
                        {"active",observedPlanTarget.Exists?(object)observedPlanTarget.Active:null},
                        {"boss",observedPlanTarget.Exists?(object)observedPlanTarget.Boss:null},
                        {"life",observedPlanTarget.Exists?(object)observedPlanTarget.Life:null},
                        {"lifeMax",observedPlanTarget.Exists?(object)observedPlanTarget.LifeMax:null}
                    }},
                {"horizontal",observedPlan.Horizontal},{"jump",observedPlan.Jump},{"jumpAction",observedPlan.JumpAction.ToString()},
                {"drop",observedPlan.Drop},{"dash",observedPlan.Dash},{"toggleMount",observedPlan.ToggleMount},
                {"quickHeal",observedPlan.QuickHeal},{"quickMana",observedPlan.QuickMana},
                {"quickBuff",observedPlan.QuickBuff},
                {"gravityControl",observedPlan.GravityControl},{"featherFallUp",observedPlan.FeatherFallUp},
                {"preferredWeaponSlot",observedPlan.PreferredWeaponSlot},
                {"aim",new Dictionary<string,object>{{"x",observedPlan.AimWorld.X},{"y",observedPlan.AimWorld.Y}}},
                {"hookAim",new Dictionary<string,object>{{"x",observedPlan.HookWorld.X},{"y",observedPlan.HookWorld.Y}}},
                {"riskScore",observedPlan.RiskScore},{"tacticalMode",observedPlan.TacticalMode.ToString()},{"weaponIssue",observedPlan.WeaponIssue},
                {"replayFrame",observedPlan.ReplayFrame}
            };
        if(hasObservedPlanReturn)
            actual=new Dictionary<string,object>
            {
                {"tick",observedPlanReturnTick},{"controlUseItem",observedControlUseItem},{"controlJump",observedControlJump},
                {"controlHook",observedControlHook},{"controlLeft",observedControlLeft},{"controlRight",observedControlRight},
                {"controlUp",observedControlUp},{"controlDown",observedControlDown},{"controlDash",observedControlDash},
                {"controlMount",observedControlMount},{"controlQuickHeal",observedControlQuickHeal},
                {"controlQuickMana",observedControlQuickMana},{"controlUseTile",observedControlUseTile},
                {"controlThrow",observedControlThrow},{"selectedItem",observedSelectedItem},
                {"itemTime",observedItemTime},{"itemAnimation",observedItemAnimation},
                {"mouseScreen",new Dictionary<string,object>{{"x",observedMouseX},{"y",observedMouseY}}}
            };
        int reasonMask=battleObservationReasons|(periodic?1:0)|(dense?64:0);
        var row=new Dictionary<string,object>
        {
            {"schema","chaite-boss-observation/v1"},{"tick",ticks},{"nativeFrames",nativeFrames},
            {"reasonMask",reasonMask},{"transitionsSincePreviousRow",battleObservationPendingTransitions},
            {"sessionState",SessionState()},{"plan",plan},{"actualAtApplyReturn",actual},{"applyPending",observedPlanPending},
            {"applyCalls",observedPlanCalls},{"applyReturns",observedPlanReturns},
             {"player",new Dictionary<string,object>
                 {
                     {"position",new Dictionary<string,object>{{"x",p.position.X},{"y",p.position.Y}}},
                     // The hitbox is what turns a predicted velocity into a
                     // predicted resting height, so an offline forward model
                     // cannot replay a ground contact without it. Read from the
                     // live player rather than assumed.
                     {"width",p.width},{"height",p.height},
                     {"velocity",new Dictionary<string,object>{{"x",p.velocity.X},{"y",p.velocity.Y}}},
                     {"life",p.statLife},{"dead",p.dead},{"wingTime",p.wingTime},{"wingTimeMax",p.wingTimeMax},
                     // The mount routes are defined by being mounted, so the
                     // fixture has to report whether the ride actually happened
                     // rather than only that the input was pressed.
                     {"mountActive",p.mount.Active},{"mountType",p.mount.Type},
                     {"poisoned",p.poisoned},
                     // The Inferno ring is the route's stated bubble-clearance
                     // premise, so the run has to show whether the buff is
                     // actually up rather than only that a refresh was asked
                     // for. 116 is BuffID.Inferno.
                     {"infernoTicks",InfernoTicks(p)},
                     {"wingsLogic",p.wingsLogic},{"grapCount",p.grapCount},{"controlUseItem",p.controlUseItem},
                     // A forward model has to be told the input the player actually
                     // received, not only what the planner intended, and the sampled
                     // row never carried the directional controls at all. These are
                     // read after the production replay has written them.
                     {"controlLeft",p.controlLeft},{"controlRight",p.controlRight},
                     {"controlUp",p.controlUp},{"controlDown",p.controlDown},
                     {"controlMount",p.controlMount},{"controlThrow",p.controlThrow},
                     // The quantities that decide the next velocity. Player.jumpSpeed
                     // and jumpHeight are static and frame correct at this point, and
                     // the wing, rocket and liquid flags decide which of the several
                     // vertical regimes the tick is in.
                     {"gravity",p.gravity},{"maxFallSpeed",p.maxFallSpeed},{"gravDir",p.gravDir},
                     {"maxRunSpeed",p.maxRunSpeed},{"accRunSpeed",p.accRunSpeed},
                     {"jumpSpeed",Player.jumpSpeed},{"jumpHeight",Player.jumpHeight},
                     {"jumpSpeedBoost",p.jumpSpeedBoost},{"autoJump",p.autoJump},
                     {"justJumped",p.justJumped},{"jump",p.jump},{"releaseJump",p.releaseJump},
                     {"sliding",p.sliding},{"slowFall",p.slowFall},{"canRocket",p.canRocket},
                     {"rocketTime",p.rocketTime},{"rocketTimeMax",p.rocketTimeMax},
                     {"rocketDelay",p.rocketDelay},{"rocketDelay2",p.rocketDelay2},
                     {"wingAccRunSpeed",p.wingAccRunSpeed},
                     {"wet",p.wet},{"honeyWet",p.honeyWet},{"lavaWet",p.lavaWet},
                     {"pulley",p.pulley},{"frozen",p.frozen},{"webbed",p.webbed},{"stoned",p.stoned},
                     {"dashType",p.dashType},{"dashDelay",p.dashDelay},{"eocDash",p.eocDash},
                     {"eocHit",p.eocHit},{"immuneTime",p.immuneTime},{"controlDash",p.controlDash},
                     // The dash state and the native horizontal profile. Without
                     // these an offline replay can only refuse a dashing tick or
                     // fall back to an adapter speed, and dash plus wings is
                     // where most of a strong-wing fight is spent.
                     {"dash",p.dash},{"dashTime",p.dashTime},
                     {"timeSinceLastDashStarted",p.timeSinceLastDashStarted},
                     {"direction",p.direction},{"releaseDash",p.releaseDash},
                     {"runAcceleration",p.runAcceleration},{"runSlowdown",p.runSlowdown},
                     // Wing and rocket resources. The reviewed flight model
                     // reads the maximum and the rocket tier, and a rocket
                     // release is a separate latch from the jump control.
                     // wingTimeMax is already recorded above; adding it twice
                     // aborts the probe on a duplicate dictionary key.
                     {"rocketBoots",p.rocketBoots},
                     {"rocketRelease",p.rocketRelease},
                     // The multi-jump charges. The reviewed jump model consumes
                     // canJumpAgain_Cloud for a cloud jump and re-arms it from
                     // hasJumpOption_Cloud on the ground, so without both the
                     // model cannot tell a cloud jump from a fall.
                     {"canJumpAgain_Cloud",p.canJumpAgain_Cloud},
                     {"hasJumpOption_Cloud",p.hasJumpOption_Cloud},
                     {"isPerformingJump_Cloud",p.isPerformingJump_Cloud},
                     {"controlJump",p.controlJump},{"controlHook",p.controlHook},{"selectedItem",p.selectedItem},
                     {"selectedItemType",p.HeldItem.type},{"selectedItemStack",p.HeldItem.stack},
                     {"itemAnimation",p.itemAnimation},{"itemAnimationMax",p.itemAnimationMax},
                     {"itemTime",p.itemTime},{"reuseDelay",p.reuseDelay},
                     {"slow",p.slow},{"moveSpeedDebuffFactor",p.strongestMoveSpeedDebuff},
                     {"usingOrReusingItem",p.UsingOrReusingItem},{"itemTimeIsZero",p.ItemTimeIsZero},
                     {"selectionCanChangeImmediately",p.selectedItemState.CanChangeSelectedItemImmediately},
                     {"selectionHotbar",p.selectedItemState.Hotbar},
                     {"selectionHasBufferedChange",p.selectedItemState.HasBufferedChange},
                     {"selectionLastNonOverridden",p.selectedItemState.LastNonOverridenSelection},
                     {"selectionHasActiveOverride",p.selectedItemState.HasActiveOverride}
                 }},
            {"npcs",npcs},{"omittedNpcs",omitted},
            {"hostileProjectiles",hostileProjectiles},
            {"hostileProjectileCount",hostileProjectileCount},
            {"omittedHostileProjectiles",omittedHostileProjectiles},
            // The same field one tick earlier, before the native update ran.
            // This is the one a hit test has to use: the projectile that lands
            // a hit is removed by that hit, so it is present here and absent
            // from the list above. Null when the pre-update capture did not run,
            // which is visible rather than silently equal to an empty list.
            {"hostileProjectilesBeforeUpdate",battleProjectilesBeforeUpdate},
            {"hostileProjectileCountBeforeUpdate",battleProjectileCountBeforeUpdate},
            {"omittedHostileProjectilesBeforeUpdate",battleOmittedProjectilesBeforeUpdate}
        };
        string serialized=Json(row)+Environment.NewLine;
        if(battleObservationBuffer.Length>0 && battleObservationBuffer.Length+serialized.Length>65536) FlushBattleObservations();
        battleObservationBuffer.Append(serialized);
        battleObservationRows++;
        battleObservationBufferedRows++;
        if(periodic) battleObservationPeriodicRows++;
        if((reasonMask&30)!=0) battleObservationTransitionRows++;
        if(final) battleObservationFinalRows++;
        if(battleObservationPendingTransitions>1) battleObservationCoalescedTransitions+=battleObservationPendingTransitions-1;
        if(plan!=null) battleObservationPlanRows++;
        if(actual!=null) battleObservationActualRows++;
        battleObservationMaximumBufferedCharacters=Math.Max(battleObservationMaximumBufferedCharacters,battleObservationBuffer.Length);
        battleObservationLastTick=ticks;
        battleObservationReasons=0;
        battleObservationPendingTransitions=0;
        if(battleObservationBufferedRows>=16 || battleObservationBuffer.Length>=65536) FlushBattleObservations();
    }
    static void FlushBattleObservations()
    {
        if(battleObservationBuffer.Length==0) return;
        string payload=battleObservationBuffer.ToString();
        File.AppendAllText(Path.Combine(Root,"boss-observations.jsonl"),payload,new UTF8Encoding(false));
        battleObservationCharactersWritten+=payload.Length;
        battleObservationBuffer.Clear();
        battleObservationBufferedRows=0;
        battleObservationFlushes++;
    }
    static Dictionary<string,object> BattleObservationReport()
    {
        return new Dictionary<string,object>
        {
            {"schema","chaite-boss-observation-summary/v1"},{"file",battleObservationRows>0?"boss-observations.jsonl":null},
            {"rows",battleObservationRows},{"maximumRows",BattleObservationMaximumRows},{"droppedRows",battleObservationDroppedRows},
            {"reservedTerminalRows",1},{"periodicRows",battleObservationPeriodicRows},{"transitionRows",battleObservationTransitionRows},
            {"terminalRows",battleObservationFinalRows},{"maximumNpcsPerRow",BattleObservationMaximumNpcs},
            {"maximumOmittedNpcs",battleObservationMaxOmittedNpcs},{"totalOmittedNpcs",battleObservationTotalOmittedNpcs},
            {"periodicTicks",BattleObservationInterval},{"edgeMinimumTicks",BattleObservationEdgeInterval},
            {"transitionEvents",battleObservationTransitions},{"unsampledTransitionEvents",battleObservationPendingTransitions},
            {"coalescedTransitionEvents",battleObservationCoalescedTransitions},
            {"applyCalls",observedPlanCalls},{"applyReturns",observedPlanReturns},{"unpairedApplyObservations",observedPlanUnpaired},
            {"applyPending",observedPlanPending},{"planRows",battleObservationPlanRows},{"actualControlRows",battleObservationActualRows},
            {"npcSnapshots",battleObservationNpcSnapshots},{"bossSnapshots",battleObservationBossSnapshots},
            {"queenMinionSnapshots",battleObservationQueenMinionSnapshots},{"primeArmSnapshots",battleObservationPrimeArmSnapshots},
            {"selectedTargetSnapshots",battleObservationTargetSnapshots},{"flushes",battleObservationFlushes},
            {"poisonedFrames",poisonedFrames},{"lifeLossFramesWhilePoisoned",lifeLossFramesWhilePoisoned},
            {"bufferFlushRows",16},{"bufferFlushCharacters",65536},{"maximumBufferedCharacters",battleObservationMaximumBufferedCharacters},
            {"charactersWritten",battleObservationCharactersWritten},{"bufferedRowsAfterFinalFlush",battleObservationBufferedRows},
            {"hurt",HurtObservationReport()},
            {"charge",ChargeObservationReport()},
            {"preHit",PreHitObservationReport()},
            {"reasonMask","1=60-tick sample;2=plan strategy/phase;4=target;8=native Boss identity/discrete phase;16=returned fire edge;32=final"},
            {"coalescingPolicy","plan/native/fire transition events inside the 15-tick edge window are accumulated into the next eligible row; periodic rows remain on exact 60-tick boundaries; one capacity slot is reserved for an explicit terminal row"},
            {"scope","test-only bounded/coalesced snapshots, not a full per-frame trajectory; active Bosses, Queen minions 658..660, Prime arms 128..131 and the same type/slot selected at ApplyPlan entry are eligible; Player.poisoned is read only; no AI, controls, equipment, terrain, damage, buffs or RNG writes"},
            {"timingCaveat","ApplyPlan before/after observer cost is included in production/native timing for this instrumented copy. Post-native capture, serialization and buffered file IO are outside those timers. Do not infer a latency improvement against uninstrumented runs."}
        };
    }
    public static void PlayerDeath(Terraria.DataStructures.PlayerDeathReason reason)
    {
        deaths++;
        minLife = 0;
        Log("NATIVE_DEATH "+Environment.StackTrace);
    }
    public static void ProjectileBeforeUpdate(Projectile projectile)
    {
        if (IsBattleObservation && !finishing && projectile != null &&
            projectile.active && projectile.hostile &&
            (projectile.type == 176 || projectile.type == 719 ||
             projectile.type == 55) && queenProjectileReports < 1200)
        {
            var player = Game.player != null && Game.player.Length > 0 ?
                Game.player[0] : null;
            if (player != null)
            {
                var dx = projectile.Center.X - player.Center.X;
                var dy = projectile.Center.Y - player.Center.Y;
                // Keep the trace bounded to the local combat corridor.  The
                // controller's threat broadphase uses the same scale; distant
                // ambient projectiles add no useful calibration signal.
                if (dx * dx + dy * dy <= 1400f * 1400f)
                {
                    var ai0 = projectile.ai != null && projectile.ai.Length > 0 ? projectile.ai[0] : 0f;
                    var ai1 = projectile.ai != null && projectile.ai.Length > 1 ? projectile.ai[1] : 0f;
                    Log("QUEEN_PROJECTILE tick=" + ticks +
                        " slot=" + projectile.whoAmI + " type=" + projectile.type +
                        " pos=" + projectile.position + " vel=" + projectile.velocity +
                        " size=" + projectile.width + "x" + projectile.height +
                        " timeLeft=" + projectile.timeLeft + " extraUpdates=" + projectile.extraUpdates +
                        " ai=" + ai0.ToString("R", CultureInfo.InvariantCulture) + "," +
                        ai1.ToString("R", CultureInfo.InvariantCulture) +
                        " player=" + player.position + " pvel=" + player.velocity +
                        " life=" + player.statLife + " potionDelay=" + player.potionDelay +
                        " releaseQuickHeal=" + player.releaseQuickHeal +
                        " itemAnimation=" + player.itemAnimation + " itemTime=" + player.itemTime +
                        " controls=" + player.controlLeft + "," + player.controlRight + "," +
                        player.controlJump + "," + player.controlUseItem);
                    queenProjectileReports++;
                }
            }
        }
        if(projectile.active && projectile.friendly && projectile.owner==0 && bulletReports<24)
        {
            bulletReports++;
            Log("NATIVE_SHOT type="+projectile.type+" pos="+projectile.position+" vel="+projectile.velocity+
                " damage="+projectile.damage+" extraUpdates="+projectile.extraUpdates+" timeLeft="+projectile.timeLeft+
                " itemAnimation="+Game.player[0].itemAnimation+
                " mouse="+Game.mouseX+","+Game.mouseY+" screen="+Game.screenPosition+
                " bounds="+Game.leftWorld+","+Game.rightWorld+","+Game.topWorld+","+Game.bottomWorld);
        }
    }
    public static void ValidateLaunch()
    {
        try
        {
            string expected=Path.GetFullPath(Path.Combine(Root,"Save"));
            if(!Path.GetFullPath(Terraria.Program.SavePath).Equals(expected,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsafe save path: refusing to run");
            ReadSettings();
        }
        catch(Exception e)
        {
            Log("LAUNCH_REJECTED "+e);
            WriteResult("harness-error", "launch-rejected", false, 11, e.ToString());
            Environment.Exit(11);
        }
    }

    static void ReadSettings()
    {
        if(settingsRead) return;
        foreach(var key in Terraria.Program.LaunchParameters.Keys)
        {
            // Switches carry no value, so they cannot go through the key/value
            // list below and must be accepted by name here.
            if(key=="-skipbeam" || key=="-stoponhit") continue;
            if(key!="-savedirectory" && key!="-scenario" && key!="-seed" &&
                key!="-difficulty" && key!="-maxticks" && key!="-wallseconds" && key!="-motioncase" && key!="-flightcase" &&
                key!="-phase" && key!="-takeovertick" && key!="-formularoute" && key!="-startside")
                throw new ArgumentException("Unsupported probe argument: "+key);
        }
        string value;
        // Episode mode reads environment rather than launch parameters: the
        // trainer launches the game once and drives many episodes, and the
        // bridge path is per-run state, not a fixture claim.
        //
        // CHAITE_PROBE_OUT overrides WHERE the probe writes, independently of
        // CHAITE_BRIDGE_FILE, which is what the PLUGIN reads to decide that a
        // replay owns movement. The two are different questions and a deployment
        // rehearsal needs them answered differently: with only the bridge
        // variable set, RouteReplay.LoadFromEnvironment() returns a replay even
        // when no <base>.action exists, so the exported policy would be tested
        // with a replay loaded behind it -- not the production configuration.
        // Unset keeps the training behaviour exactly as it was.
        bridgeBase=Environment.GetEnvironmentVariable("CHAITE_PROBE_OUT");
        if(string.IsNullOrEmpty(bridgeBase))
            bridgeBase=Environment.GetEnvironmentVariable(Chaite.Core.RouteReplay.BridgeVariable);
        // Whether a TRAINER is behind this run, which is a different question
        // from where the probe writes. The plugin reads this same variable to
        // decide that a replay owns movement, and the lockstep waits on the
        // trainer's <base>.action file, so both follow CHAITE_BRIDGE_FILE.
        bridgeDriven=!string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(Chaite.Core.RouteReplay.BridgeVariable));
        if(!string.IsNullOrEmpty(bridgeBase))
        {
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(bridgeBase)); }
            catch { }
        }
        int parsedEpisodes;
        if(int.TryParse(Environment.GetEnvironmentVariable("CHAITE_EPISODES"),
            NumberStyles.Integer,CultureInfo.InvariantCulture,out parsedEpisodes) &&
            parsedEpisodes>0)
            episodeLimit=parsedEpisodes;
        // A run-level tick budget. The wall clock alone is not a safe bound for a
        // training round: Terraria is a 32-bit process whose address space is
        // exhausted at roughly 661k ticks, and the tick rate varies with machine
        // load, so a fixed wall budget can overshoot into OutOfMemoryException
        // (measured twice: train7 and esd1 both died within 20 ticks of 661k).
        // Bounding the round in ticks makes the memory peak deterministic.
        int parsedRunTicks;
        if(int.TryParse(Environment.GetEnvironmentVariable("CHAITE_RUN_MAX_TICKS"),
            NumberStyles.Integer,CultureInfo.InvariantCulture,out parsedRunTicks) &&
            parsedRunTicks>0)
            runTickLimit=BoundedInt(parsedRunTicks.ToString(CultureInfo.InvariantCulture),
                1000,2000000,"CHAITE_RUN_MAX_TICKS");
        // Diagnostic override for the simulated-output band. The default stays
        // the reviewed 600-1200; this exists so a probe run can force a kill
        // clock (e.g. a DPS high enough to drop the boss long before the player
        // can die) to test the win accounting without editing the constants and
        // leaking the change into the next training round.
        float parsedDps;
        if(float.TryParse(Environment.GetEnvironmentVariable("CHAITE_SIM_DPS"),
            NumberStyles.Float,CultureInfo.InvariantCulture,out parsedDps) && parsedDps>0f)
            simulatedDpsOverride=parsedDps<1f?1f:(parsedDps>100000f?100000f:parsedDps);
        // CHAITE_PROJ_SLOTS widens the bridge's projectile window for one
        // session without touching the others. The trainer reads the same
        // variable, so the feature vector stays aligned with the row.
        int parsedSlots;
        if(int.TryParse(Environment.GetEnvironmentVariable("CHAITE_PROJ_SLOTS"),
            NumberStyles.Integer,CultureInfo.InvariantCulture,out parsedSlots) && parsedSlots>0)
            projectileSlots=parsedSlots<1?1:(parsedSlots>256?256:parsedSlots);
        // CHAITE_PROJ_SORT=threat orders the window by time-to-contact instead of
        // Manhattan distance; CHAITE_PROJ_COLLAPSE=1 keeps one slot per type.
        // Both default off, and both are read here rather than per tick so a
        // session cannot change ordering halfway through a fight.
        var sortMode=Environment.GetEnvironmentVariable("CHAITE_PROJ_SORT");
        projectileSortByThreat=sortMode!=null &&
            sortMode.Trim().Equals("threat",StringComparison.OrdinalIgnoreCase);
        var collapseMode=Environment.GetEnvironmentVariable("CHAITE_PROJ_COLLAPSE");
        projectileCollapseTypes=collapseMode!=null &&
            (collapseMode.Trim()=="1" ||
             collapseMode.Trim().Equals("true",StringComparison.OrdinalIgnoreCase));
        if(Terraria.Program.LaunchParameters.ContainsKey("-stoponhit")) stopOnFirstHit=true;
        if(Terraria.Program.LaunchParameters.TryGetValue("-seed",out value))
            seed=BoundedInt(value,0,int.MaxValue,"seed");
        if(Terraria.Program.LaunchParameters.TryGetValue("-maxticks",out value))
            tickLimit=BoundedInt(value,600,24000,"maxticks");
        if(Terraria.Program.LaunchParameters.TryGetValue("-wallseconds",out value))
            // Episode mode is a training session, not a fight probe: one
            // process runs many episodes, so the wall budget has to cover the
            // whole session rather than one fight.
            wallLimitSeconds=BoundedInt(value,15,episodeLimit>0?86400:900,"wallseconds");
        if(Terraria.Program.LaunchParameters.TryGetValue("-takeovertick",out value))
            takeoverTick=BoundedInt(value,120,23880,"takeovertick");
        if(Terraria.Program.LaunchParameters.TryGetValue("-phase",out value)) requestedPhase=value.ToLowerInvariant();
        if(Terraria.Program.LaunchParameters.TryGetValue("-formularoute",out value)) formulaRoute=value.ToLowerInvariant();
        if(Terraria.Program.LaunchParameters.TryGetValue("-startside",out value))
        {
            requestedStartSide=value.ToLowerInvariant();
            if(requestedStartSide!="left" && requestedStartSide!="right")
                throw new ArgumentException("startside must be left or right");
        }
        if(Terraria.Program.LaunchParameters.TryGetValue("-difficulty",out value)) difficulty=value.ToLowerInvariant();
        switch(difficulty)
        {
            case "classic": difficultyCode=0; break;
            case "expert": difficultyCode=1; break;
            case "master": difficultyCode=2; break;
            default: throw new ArgumentException("difficulty must be classic, expert or master");
        }
        var id=Terraria.Program.LaunchParameters.TryGetValue("-scenario",out value)?value.ToLowerInvariant():"eye-baseline";
        scenario=new ScenarioSpec { Id=id, SpawnLeadTicks=15, MaxLife=400, EquipmentTier="pre-boss" };
        switch(id)
        {
            case "eye-baseline": scenario.Summon=ItemID.SuspiciousLookingEye; scenario.BossTypes=new[]{4}; scenario.Legacy=true; scenario.Phases=new[]{"summon"}; break;
            case "eye": scenario.Summon=ItemID.SuspiciousLookingEye; scenario.BossTypes=new[]{4}; scenario.Phases=new[]{"summon"}; break;
            case "king-slime": scenario.Summon=ItemID.SlimeCrown; scenario.BossTypes=new[]{50}; scenario.Phases=new[]{"summon"}; break;
            case "queen-slime": scenario.Summon=ItemID.QueenSlimeCrystal; scenario.BossTypes=new[]{657}; scenario.HardMode=true; scenario.Hallow=true; scenario.Phases=new[]{"summon"}; break;
            case "destroyer": scenario.Summon=ItemID.MechanicalWorm; scenario.BossTypes=new[]{134}; scenario.HardMode=true; scenario.Phases=new[]{"summon"}; break;
            case "twins": scenario.Summon=ItemID.MechanicalEye; scenario.BossTypes=new[]{125,126}; scenario.HardMode=true; scenario.Phases=new[]{"summon"}; break;
            case "prime": scenario.Summon=ItemID.MechanicalSkull; scenario.BossTypes=new[]{127}; scenario.HardMode=true; scenario.Phases=new[]{"summon"}; break;
            case "deerclops":
                PriorityItemScenario(5120,668,new[]{668},false,new[]{"summon","spawn-settle","opening","forward-spikes","rubble","slow-roar","double-spikes","shadow-hands","return-home","teleport-home"}); scenario.Snow=true; break;
            case "skeletron":
                DirectScenario(35,new[]{35},false,new[]{"hover","pre-spin","spin-imminent","spin","spin-pursuit","spin-exit","hand-vertical-imminent","hand-vertical-locking","hand-vertical","hand-horizontal-imminent","hand-horizontal-locking","hand-horizontal"}); break;
            case "queen-bee":
                PriorityItemScenario(1133,222,new[]{222},false,new[]{"summon","choose","charge-align","charge","charge-brake","bee-wave","move-above","stinger","reacquire"}); scenario.Jungle=true; break;
            case "wall-of-flesh":
                DirectScenario(113,new[]{113,114},false,new[]{"runway","accelerating","low-health","critical","eye-laser"});
                scenario.Underworld=true; break;
            case "duke-fishron":
                DirectScenario(370,new[]{370},true,new[]{"summon","monitor","spawn-fade","spawn-emerge","p1-hover","p1-dash","p1-bubbles","p1-sharknado","p2-transition-fade","p2-transition-emerge","p2-hover","p2-dash","p2-bubbles","p2-sharknado","p3-transition-fade","p3-transition-hidden","p3-reposition","p3-dash","p3-teleport"}); scenario.Ocean=true; scenario.SuppressStrayNpcs=true; if(requestedPhase=="summon") scenario.Summon=2673; break;
            case "moon-lord":
                DirectScenario(398,new[]{396,397,398},true,new[]{"intro","synchronize-eyes","head-bolts","head-tongue","head-deathray-telegraph","left-sphere-release","right-sphere-release"});
                scenario.SpawnLeadTicks=90; break;
            case "scope-negative-unsupported-summon":
                ScopeNegativeScenario(ItemID.SuspiciousLookingEye); break;
            case "scope-negative-existing-unsupported":
                ScopeNegativeScenario(0,4); break;
            case "scope-negative-fishron-mixed":
                ScopeNegativeScenario(0,370,4); scenario.HardMode=true; scenario.Ocean=true; break;
            case "scope-negative-kingslime-mixed":
                ScopeNegativeScenario(0,50,4); scenario.HardMode=true; break;
            case "scope-negative-fishron-duplicate":
                ScopeNegativeScenario(0,370,370); scenario.HardMode=true; scenario.Ocean=true; break;
            case "scope-negative-kingslime-duplicate":
                ScopeNegativeScenario(0,50,50); scenario.HardMode=true; break;
            case "motion-jump": scenario.Motion=true; scenario.BossTypes=new int[0]; break;
            case "motion-flight": scenario.Motion=true; scenario.Flight=true; scenario.BossTypes=new int[0]; break;
            default: throw new ArgumentException("Unknown bounded scenario: "+id);
        }
        scenario.ExpectedVariant=ExpectedVariantForScenario(id);
        // Episode mode is a training session over a real Boss fixture, driven
        // through the live bridge; every other fixture is outside its contract.
        // A tick-keyed route file is the second legitimate driver: it is how an
        // exported policy is accepted, and refusing it made the acceptance run
        // itself impossible (LAUNCH_REJECTED "Episode mode requires
        // CHAITE_BRIDGE_FILE"), so the guard now accepts either channel.
        bool routeDriven=!string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(
                Chaite.Core.RouteReplay.FileVariable));
        // Episode mode needs a DRIVER, or an explicit deployment-rehearsal opt-in.
        // This used to test bridgeBase, which is now the probe's own output path,
        // so a bare CHAITE_PROBE_OUT satisfied it with nothing driving at all.
        // The rehearsal case is real and has to stay possible: it runs the plugin
        // with no replay and no exported policy precisely to see what the formula
        // script and the safety gate do on their own, so CHAITE_PROBE_OUT is
        // accepted as the explicit "episodes, no driver" opt-in.
        bool policyDriven=!string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("CHAITE_POLICY_FILE"));
        bool rehearsal=!string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("CHAITE_PROBE_OUT"));
        if(episodeLimit>0 && (!bridgeDriven && !routeDriven && !policyDriven &&
            !rehearsal ||
            IsMotion || IsFlight ||
            IsScopeNegative || scenario.BossTypes==null || scenario.BossTypes.Length==0))
            throw new ArgumentException("Episode mode requires CHAITE_BRIDGE_FILE, CHAITE_ROUTE_FILE or CHAITE_POLICY_FILE and a real Boss scenario");
        ValidateFormulaRoute();
        ConfigureScenarioProgression();
        if(!IsMotion)
        {
            if(scenario.DirectSpawn && !Terraria.Program.LaunchParameters.ContainsKey("-phase")) requestedPhase=scenario.Phases[0];
            if(Array.IndexOf(scenario.Phases,requestedPhase)<0)
                throw new ArgumentException("Unreviewed phase '"+requestedPhase+"' for scenario "+id);
            if(takeoverTick>=tickLimit-120)
                throw new ArgumentException("takeovertick must leave at least 120 native frames before maxticks");
            if(id=="duke-fishron" && requestedPhase.StartsWith("p3-",StringComparison.Ordinal) && difficultyCode==0)
                throw new ArgumentException("Duke Fishron phase 3 is not a Classic native phase");
            int spawnLead=scenario.Id=="moon-lord"?(requestedPhase=="intro"?15:90):scenario.SpawnLeadTicks;
            directSpawnTick=scenario.DirectSpawn?(IsMonitorFixture?takeoverTick+120:IsScopeNegative?takeoverTick:Math.Max(1,takeoverTick-spawnLead)):-1;
        }
        if(Terraria.Program.LaunchParameters.TryGetValue("-motioncase",out value)) motionCase=value;
        if(Terraria.Program.LaunchParameters.TryGetValue("-flightcase",out value)) flightCase=value;
        if(IsFlight)
        {
            if(difficultyCode!=0 || motionCase!=null) throw new ArgumentException("motion-flight requires classic difficulty and no -motioncase");
            switch(flightCase)
            {
                case "demon-exhaust-release": case "demon-lightning-exhaust-release":
                case "demon-early-repress": case "demon-lightning-early-repress":
                case "demon-held-landing": case "demon-lightning-held-landing":
                case "demon-cloud-repress": case "demon-lightning-cloud-repress":
                case "demon-feather-neutral": case "demon-feather-up": case "demon-feather-down": break;
                default: throw new ArgumentException("motion-flight requires one reviewed -flightcase");
            }
        }
        else if(IsMotion)
        {
            if(difficultyCode!=0) throw new ArgumentException("motion-jump requires classic difficulty");
            switch(motionCase)
            {
                case "no-cloud-hold": case "no-cloud-tap": case "no-cloud-release-press":
                case "cloud-hold": case "cloud-tap": case "cloud-release-press":
                // Lilith's Necklace cases. "balloon" adds the Bundle of Balloons
                // and "plain" is the mount alone, so the pair isolates the extra
                // jumps from the mount itself.
                case "lilith-balloon-hold": case "lilith-balloon-multijump":
                case "lilith-plain-hold": case "lilith-plain-multijump": break;
                default: throw new ArgumentException("motion-jump requires one reviewed -motioncase");
            }
        }
        else if(motionCase!=null) throw new ArgumentException("-motioncase is valid only with motion-jump");
        if(!IsFlight && flightCase!=null) throw new ArgumentException("-flightcase is valid only with motion-flight");
        settingsRead=true;
    }

    static void ValidateFormulaRoute()
    {
        if(formulaRoute==null) return;
        if(requestedPhase!="monitor")
            throw new ArgumentException("-formularoute requires -phase monitor");
        string[] allowed=scenario.Id=="duke-fishron"
            ?new[]{"fishron-fairy-wing","fishron-strong-wing","fishron-trusty-chillet","fishron-trusty-chillet-ignis","fishron-lilith-wolf"}
            :new string[0];
        if(Array.IndexOf(allowed,formulaRoute)<0)
            throw new ArgumentException("Unreviewed formula route for scenario: "+formulaRoute);
    }

    static void DirectScenario(int spawnType,int[] bossTypes,bool hardMode,string[] phases)
    {
        scenario.DirectSpawn=requestedPhase!="summon";
        scenario.DirectSpawnType=spawnType;
        scenario.BossTypes=bossTypes;
        scenario.HardMode=hardMode;
        scenario.Phases=phases;
    }

    // The flat ground the fight happens on, in tiles. These are the single
    // source for both the tiles that get built and the arena block that
    // publishes them, because the two drifted apart: the block advertised a
    // 50..550 platform span for the Ocean case while the rows were built across
    // 1..399, and nothing could have caught that from the evidence alone.
    //
    // The floor is the whole arena. There are no platform rows: the real fight
    // is on one long straight flat ground, so the fixture gives it one.
    static int ArenaGroundLeft { get { return scenario!=null && scenario.Ocean?1:800; } }
    static int ArenaGroundRightExclusive { get { return scenario!=null && scenario.Ocean?400:3400; } }
    /// <summary>
    /// How far the ocean scenario's flat ground is built to, in tiles, past the
    /// arena's own right edge.
    ///
    /// The measured reason: the policy is not confined to the ocean band, and
    /// 15.0% of the recorded obsb1 frames sat past tile 400 with 3.2% of all
    /// frames below the floor line -- out there the deepest excursions reach
    /// 9-10k px, which is a void fall rather than a landed drop, because
    /// <c>WorldGen.clearWorld</c> leaves no terrain outside the built arena.
    /// The measured maximum player x across that stream was 14,751 px = 922
    /// tiles, so 1200 tiles covers every position the fight has actually
    /// reached with margin.
    ///
    /// This is a SAFETY FLOOR, not an arena extension. <see cref="ArenaGroundLeft"/>
    /// and <see cref="ArenaGroundRightExclusive"/> still define the arena, the
    /// published bounds, the start position and the enrage band, and none of
    /// those move. It exists so that leaving the ocean band costs the policy
    /// position rather than its life, which is the same trade the real coastline
    /// makes: the beach is not a pit.
    /// </summary>
    const int SafetyFloorRightExclusive = 1200;

    // The real Fishron start is close to one end of the runway rather than in
    // the middle of it. Twenty tiles is the measured inset the owner reported;
    // which end varies per fight, so the side is a launch parameter and both
    // are legal openings. Empress keeps the centre of its runway.
    const int RunwayStartInsetTiles = 20;
    static string requestedStartSide = "left";
    // The launch side, remembered so episode mode can alternate away from it
    // and back. Arena geometry is remembered for the same reason: a reset has
    // to rebuild the player position for whichever end the next episode uses.
    static string launchStartSide = "left";
    static int arenaCenterXTiles;
    static int arenaGroundYTiles;
    // The horizontal platform rows actually built, published verbatim into the
    // arena block. A field rather than a local because that block is written from
    // a different method, and a published geometry not read off the tiles that
    // were built is exactly how the earlier platform-row claim drifted from the
    // map.
    static int[] arenaPlatformRows = new int[0];
    // Platform tiles counted immediately after the rows are built. Taken at build
    // time rather than at result-writing time on purpose: a later reset may
    // already have cleared the fixture, which would make the control read zero
    // for every run and prove nothing.
    static int arenaPlatformTileCount;
    // Vertical gap between the arena's horizontal layers, in tiles. The owner
    // specified 60: it is roughly one wing charge of climb, so a layer is
    // reachable from the one below on a full bar.
    const int PlatformRowSpacingTiles = 60;
    // Where the player starts, in tiles. Kept separate from arenaGroundYTiles
    // because that value is also the published arena geometry -- the ground --
    // and conflating the two would make the evidence claim a floor the player
    // never stands on.
    static int arenaStartYTiles;

    static int PlayerStartTileX(int arenaCenterX)
    {
        if(scenario==null || !scenario.Ocean) return arenaCenterX;
        return requestedStartSide=="right"
            ? ArenaGroundRightExclusive-1-RunwayStartInsetTiles
            : ArenaGroundLeft+RunwayStartInsetTiles;
    }

    /// <summary>
    /// Counts the platform tiles actually present, for the evidence block.
    ///
    /// This is the in-engine control for the layered arena: it reads the world
    /// rather than repeating the intent, so if the rows were never built -- or
    /// were built and then removed by a later reset -- it reports zero and the
    /// arena block's claim is visibly false instead of silently assumed.
    /// </summary>
    static int CountPlatformTiles()
    {
        int count = 0;
        for (int r = 0; r < arenaPlatformRows.Length; r++)
        for (int x = ArenaGroundLeft; x < ArenaGroundRightExclusive; x++)
        {
            int y = arenaPlatformRows[r];
            Tile tile = Game.tile[x,y];
            if (tile != null && tile.active() && tile.type == TileID.Platforms) count++;
        }
        return count;
    }

    static void ScopeNegativeScenario(int summonItem,params int[] rootTypes)
    {
        scenario.ScopeNegative=true;
        scenario.Summon=summonItem;
        scenario.BossTypes=new int[0];
        scenario.ScopeNegativeRootTypes=rootTypes??new int[0];
        scenario.DirectSpawn=scenario.ScopeNegativeRootTypes.Length>0;
        scenario.DirectSpawnType=scenario.DirectSpawn?scenario.ScopeNegativeRootTypes[0]:0;
        scenario.Phases=new[]{"reject"};
    }

    static void PriorityItemScenario(int summonItem,int spawnType,int[] bossTypes,bool hardMode,string[] phases)
    {
        scenario.DirectSpawnType=spawnType;
        scenario.BossTypes=bossTypes;
        scenario.HardMode=hardMode;
        scenario.Phases=phases;
        scenario.PriorityArena=true;
        // The organic phase begins with the production F8 path and a real
        // hotbar item. Every other reviewed phase retains the existing direct
        // spawn and native-field staging regression path.
        if(requestedPhase=="summon") scenario.Summon=summonItem;
        else scenario.DirectSpawn=true;
    }

    // The result schema deliberately has a small, named variant vocabulary.
    // A case may never supply an arbitrary label such as "standard" for an
    // Empress day fight or a special-world encounter.  Future native scenarios
    // can opt into the two mechanical rows only when their actual topology and
    // native world flags meet the checks below.
    static string ExpectedVariantForScenario(string id)
    {
        switch(id)
        {
            case "mechanical-mayhem": return "simultaneous-mechanical-trio";
            case "mechdusa": return "getfixedboi-mechdusa";
            default: return "standard";
        }
    }

    static bool IsKnownResultVariant(string value)
    {
        return value=="standard" || value=="night" || value=="day" ||
            value=="simultaneous-mechanical-trio" || value=="getfixedboi-mechdusa";
    }

    // Keep the probe's native-world observation independent of production
    // reflection wrappers.  These fields are version-pinned by the isolated
    // probe; inability to read one makes the result ineligible rather than
    // guessing that an unobserved secret-world flag is false.
    static bool NativeWorldFlag(string name)
    {
        var field=typeof(Game).GetField(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        if(field==null || field.FieldType!=typeof(bool))
            throw new MissingFieldException("Native Main."+name+" Boolean is unavailable");
        return (bool)field.GetValue(null);
    }

    static VariantObservation ObserveVariant(bool capturedAtActivation)
    {
        var observation=new VariantObservation { CapturedAtActivation=capturedAtActivation, Tick=capturedAtActivation?ticks:-1 };
        try
        {
            observation.GameMode=Game.GameMode;
            observation.NativeDifficulty=(int)Game.Difficulty;
            observation.DayTime=Game.dayTime;
            observation.HardMode=Game.hardMode;
            observation.ForTheWorthy=NativeWorldFlag("getGoodWorld");
            observation.ZenithWorld=NativeWorldFlag("zenithWorld");
            observation.DrunkWorld=NativeWorldFlag("drunkWorld");
            observation.NotTheBeesWorld=NativeWorldFlag("notTheBeesWorld");
            observation.RemixWorld=NativeWorldFlag("remixWorld");
            observation.CelebrationWorld=NativeWorldFlag("tenthAnniversaryWorld");
            observation.ConstantWorld=NativeWorldFlag("dontStarveWorld");
            observation.NoTrapsWorld=NativeWorldFlag("noTrapsWorld");
            observation.SkyblockWorld=NativeWorldFlag("skyblockWorld");
        }
        catch(Exception e)
        {
            // Result serialization also runs from the launch/error path. Keep
            // a structured, explicitly unverified observation there instead of
            // replacing the original failure with a reporting exception.
            Log("VARIANT_OBSERVATION_UNAVAILABLE "+SafeDiagnosticText(e.Message));
        }
        return observation;
    }

    static bool HasUnsupportedStandardWorldRule(VariantObservation observation)
    {
        return observation.ForTheWorthy || observation.ZenithWorld || observation.DrunkWorld ||
            observation.NotTheBeesWorld || observation.RemixWorld || observation.CelebrationWorld ||
            observation.ConstantWorld || observation.NoTrapsWorld || observation.SkyblockWorld;
    }

    static bool HasMechanicalTrioExpectedTopology()
    {
        if(scenario==null || scenario.BossTypes==null) return false;
        bool twins=false, destroyer=false, prime=false;
        foreach(int type in scenario.BossTypes)
        {
            if(type==125 || type==126) twins=true;
            else if(type==134) destroyer=true;
            else if(type==127) prime=true;
        }
        return twins && destroyer && prime;
    }

    static bool HasMechanicalTrioObservedTopology()
    {
        return firstObservedBossTypes.Contains(125) && firstObservedBossTypes.Contains(134) &&
            firstObservedBossTypes.Contains(127);
    }

    static bool NativeVariantFlagsMatch(VariantObservation observation)
    {
        return observation!=null && observation.GameMode==difficultyCode &&
            observation.NativeDifficulty==difficultyCode+1 && scenario!=null &&
            observation.HardMode==scenario.HardMode;
    }

    static string DeriveObservedVariant(VariantObservation observation)
    {
        if(!NativeVariantFlagsMatch(observation) || scenario==null) return null;
        switch(scenario.Id)
        {
            case "mechanical-mayhem":
                if(observation.ZenithWorld || HasUnsupportedStandardWorldRule(observation) ||
                    !HasMechanicalTrioExpectedTopology()) return null;
                return "simultaneous-mechanical-trio";
            case "mechdusa":
                // Zenith is the native getfixedboi marker. Do not assume its
                // component secret flags; vanilla may intentionally combine
                // them. The encounter topology is independently recorded.
                if(!observation.ZenithWorld || !HasMechanicalTrioExpectedTopology()) return null;
                return "getfixedboi-mechdusa";
            default:
                return HasUnsupportedStandardWorldRule(observation)?null:"standard";
        }
    }

    static void CaptureVariantAtActivation()
    {
        if(variantAtActivation!=null) return;
        variantAtActivation=ObserveVariant(true);
        string observed=DeriveObservedVariant(variantAtActivation);
        Log("VARIANT_AT_ACTIVATION scenario="+(scenario==null?"null":scenario.Id)+
            " expected="+(scenario==null?"null":scenario.ExpectedVariant)+
            " observed="+(observed??"unverified")+" day="+variantAtActivation.DayTime+
            " ftw="+variantAtActivation.ForTheWorthy+" zenith="+variantAtActivation.ZenithWorld+
            " mode="+variantAtActivation.GameMode+" tick="+variantAtActivation.Tick);
    }

    static Dictionary<string,object> BuildVariantEvidence(bool expectedSeen)
    {
        var observation=variantAtActivation??ObserveVariant(false);
        string observed=DeriveObservedVariant(observation);
        string expected=scenario==null?null:scenario.ExpectedVariant;
        bool nativeFlagsVerified=observation.CapturedAtActivation && NativeVariantFlagsMatch(observation) &&
            IsKnownResultVariant(observed) && expected==observed;
        var observedTypes=new int[firstObservedBossTypes.Count];
        firstObservedBossTypes.CopyTo(observedTypes); Array.Sort(observedTypes);
        return new Dictionary<string,object>
        {
            {"schema","chaite-boss-variant-evidence/v1"},{"scenario",scenario==null?null:scenario.Id},
            {"expectedVariant",expected},{"observedVariant",observed??"unverified"},
            {"reportedVariant",nativeFlagsVerified?observed:(observed??"unverified")},
            {"capturedAtActivation",observation.CapturedAtActivation},{"captureTick",observation.Tick},
            {"requestedTakeoverTick",takeoverTick},{"nativeFlagsVerified",nativeFlagsVerified},
            {"variantMatchesScenario",expected==observed},{"mechanicalTrioExpected",HasMechanicalTrioExpectedTopology()},
            {"mechanicalTrioObserved",HasMechanicalTrioObservedTopology()},
            {"allExpectedBossesSeen",expectedSeen},{"native",new Dictionary<string,object>
                {
                    {"gameMode",observation.GameMode},{"difficulty",observation.NativeDifficulty},
                    {"dayTime",observation.DayTime},{"hardMode",observation.HardMode},
                    {"forTheWorthy",observation.ForTheWorthy},{"zenithWorld",observation.ZenithWorld},
                    {"drunkWorld",observation.DrunkWorld},{"notTheBeesWorld",observation.NotTheBeesWorld},
                    {"remixWorld",observation.RemixWorld},{"celebrationWorld",observation.CelebrationWorld},
                    {"constantWorld",observation.ConstantWorld},{"noTrapsWorld",observation.NoTrapsWorld},
                    {"skyblockWorld",observation.SkyblockWorld}
                }}
        };
    }

    static void ConfigureScenarioProgression()
    {
        // Progression belongs to the encounter, not merely to hardMode.  In
        // particular, a post-Plantera Empress or pre-Moon-Lord fixture must not
        // silently reuse the early-Hardmode mechanical-boss loadout.
        switch(scenario.Id)
        {
            case "wall-of-flesh":
                scenario.EquipmentTier="late-pre-hardmode";
                break;
            case "queen-slime": case "destroyer": case "twins": case "prime":
                scenario.EquipmentTier="early-hardmode";
                break;
            case "duke-fishron":
                // The staged phase fixtures keep the historical early-Hardmode
                // loadout.  The formula monitor fixture instead reuses the
                // hash-reviewed post-Plantera Chain Gun output: 78000 Expert
                // life cannot be removed inside the probe budget by the
                // Clockwork burst, and a route that cannot finish can never be
                // accepted.  Weapons and ammo never select a formula route, so
                // this changes mobility admission in no way.
                scenario.EquipmentTier=IsMonitorFixture?"post-plantera":"early-hardmode";
                break;
            case "moon-lord":
                scenario.EquipmentTier="pre-moon-lord";
                scenario.MaxLife=500;
                break;
        }
    }

    static int BoundedInt(string value,int minimum,int maximum,string name)
    {
        int parsed;
        if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out parsed) || parsed<minimum || parsed>maximum)
            throw new ArgumentException("Invalid "+name+"; expected "+minimum+".."+maximum);
        return parsed;
    }
    public static void PlayerReturned(Player player,string location)
    {
        if(booted && player.whoAmI==0) playerReturnedTick=ticks;
        if(IsMotion && motionFrame!=null && player.whoAmI==0)
        {
            if(motionFrame.ContainsKey("postPlayer")) throw new InvalidOperationException("Multiple native Player.Update returns in one motion frame");
            motionFrame["postPlayer"]=MotionSnapshot(player);
            motionFrame["playerReturnLocation"]=location;
        }
        if(booted && player.whoAmI==0 && (ticks==3 || ticks==121))
            Log("PLAYER_RETURN "+location+" life="+player.statLife+" armorDef="+player.armor[0].defense+" defense="+player.statDefense+
                " dims="+Game.maxTilesX+","+Game.maxTilesY+" cell="+(Game.tile[(int)(player.Center.X/16),(int)(player.Center.Y/16)]==null?"null":"present"));
    }

    static bool denseFramesRequested, denseFramesRequestRead;
    // The hostile projectile field as it stood before the native update, and
    // its counters. Null outside dense mode, so a missing capture is visible
    // rather than indistinguishable from an empty list.
    static List<Dictionary<string,object>> battleProjectilesBeforeUpdate;
    static int battleProjectileCountBeforeUpdate, battleOmittedProjectilesBeforeUpdate;

    /// <summary>Reads the dense-frame request once and applies it to the scenario.
    /// The switch is an environment variable rather than a scenario field because a
    /// scenario is chosen by name in the source, and the same fight has to be
    /// runnable both sampled and dense without editing and revalidating the probe.
    /// </summary>
    static void ApplyDenseFrameRequest()
    {
        if(denseFramesRequestRead) return;
        denseFramesRequestRead=true;
        string value=Environment.GetEnvironmentVariable("CHAITE_PROBE_DENSE_FRAMES");
        denseFramesRequested=!string.IsNullOrEmpty(value) && value!="0";
        if(denseFramesRequested && scenario!=null) scenario.DenseFrames=true;
    }

    public static void RunHeadless()
    {
        try
        {
            ReadSettings();
            ApplyDenseFrameRequest();
            Log("HEADLESS_BEGIN real vanilla assembly; no graphics, no network, no user saves");
            Log("CASE scenario="+scenario.Id+" seed="+seed+" difficulty="+difficulty+" maxTicks="+tickLimit+" wallSeconds="+wallLimitSeconds+" runMaxTicks="+runTickLimit);
            Game.dedServ=true;
            SocialAPI.Initialize(SocialMode.None);
            Terraria.Localization.LanguageManager.Instance.SetLanguage(Terraria.Localization.GameCulture.DefaultCulture);
            using(var game = new Game())
            {
                Terraria.Lang.InitializeLegacyLocalization();
                typeof(Game).GetMethod("Initialize",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
                Log("HEADLESS_INITIALIZED");
                ContentReady();
                // Dedicated Initialize omits the client's pure-data achievement
                // registry. Keep native achievement logic, using only our Save path.
                if (SocialAPI.Achievements != null) throw new InvalidOperationException("Unexpected cloud achievement service");
                typeof(Game).GetField("_achievements",BindingFlags.Instance|BindingFlags.NonPublic)
                    .SetValue(game,new Terraria.Achievements.AchievementManager());
                Terraria.Initializers.AchievementInitializer.Load();
                Log("LOCAL_ACHIEVEMENTS initialized");
                Game.screenWidth=1280;Game.screenHeight=720;
                Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                Game.GameViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);
                Game.GameViewMatrix.SetViewportOverride(new Viewport(0,0,1280,720));
                Game.BackgroundViewMatrix=new Terraria.Graphics.SpriteViewMatrix(null);
                Game.BackgroundViewMatrix.SetViewportOverride(new Viewport(0,0,1280,720));
                Lighting.Initialize();
                if(IsMotion) Log("BEAM_COMPARE_SKIPPED motion-only fixture");
                else if(!Terraria.Program.LaunchParameters.ContainsKey("-skipbeam")) CompareNativeBeamGeometry();
                else Log("BEAM_COMPARE_SKIPPED diagnostic combat iteration only");
                // Surface initialization problems instead of allowing the outer
                // vanilla per-player exception guard to hide an invalid fixture.
                if(!IsMotion)
                {
                    Game.player[0].Update(0);
                    Log("DIRECT_PLAYER_UPDATE completed defense="+Game.player[0].statDefense+" accRun="+Game.player[0].accRunSpeed);
                }
                else Log("MOTION_INITIALIZATION all 20 warmup Player.Update frames will be recorded; no direct unrecorded player update");
                if(scenario.Hallow || scenario.Jungle || scenario.Snow || scenario.Ocean)
                {
                    Game.player[0].UpdateSceneMetrics();
                    // Scan updates real tile counts; native UpdateBiomes transfers
                    // those metrics to player zone flags (no forced/fake ZoneHallow).
                    Game.player[0].UpdateBiomes();
                    Log("NATIVE_BIOME hallow="+Game.player[0].ZoneHallow+" jungle="+Game.player[0].ZoneJungle+
                        " snow="+Game.player[0].ZoneSnow+" beach="+Game.player[0].ZoneBeach+
                        " holyTiles="+Player.SceneMetrics.HolyTileCount+" jungleTiles="+Player.SceneMetrics.JungleTileCount+
                        " snowTiles="+Player.SceneMetrics.SnowTileCount);
                    // The biome-scan reach was measured with a temporary sweep
                    // here (offset 0..100 tiles above the floor, logging
                    // holyTiles and ZoneHallow at each): 1014 up to 55 tiles,
                    // 507 at 60, 0 from 65 on. It is not kept because moving the
                    // player and rescanning perturbs the fixture.
                    if(scenario.Hallow && !Game.player[0].ZoneHallow) throw new InvalidOperationException("Hallow fixture lacks a native-detected Hallow biome");
                    if(scenario.Jungle && !Game.player[0].ZoneJungle) throw new InvalidOperationException("Queen Bee fixture lacks a native-detected Jungle biome");
                    if(scenario.Snow && !Game.player[0].ZoneSnow) throw new InvalidOperationException("Deerclops fixture lacks a native-detected Snow biome");
                    if(scenario.Ocean && !Game.player[0].ZoneBeach) throw new InvalidOperationException("Duke Fishron fixture lacks a native-detected Ocean biome");
                }
                var update = (Action)Delegate.CreateDelegate(typeof(Action),game,typeof(Game).GetMethod("DoUpdateInWorld",BindingFlags.Instance|BindingFlags.NonPublic));
                var counter=typeof(Game).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic);
                // Finish ALL setup, including optional collision comparisons, before
                // installing combat RNGs. Setup must not consume this battle stream.
                InstallBattleRandom();
                while(!failed)
                {
                    BeforeUpdate();
                    counter.SetValue(null,(uint)ticks);
                    VerifyNativeBattleContext();
                    long nativeStart=Stopwatch.GetTimestamp();
                    // Native Main.DoUpdate advances this stream once before
                    // DoUpdateInWorld. Headless mode bypasses only that outer call.
                    expectedUnpausedUpdateSeed=Terraria.Utils.RandomNextSeed(expectedUnpausedUpdateSeed);
                    setUnpausedUpdateSeed(expectedUnpausedUpdateSeed);
                    unpausedUpdateSeedAdvances++;
                    // A rendered client refreshes nearby tile metrics through its
                    // scene/lighting path, absent in this dedicated presentation.
                    // Use the real scan at the new frame counter, then let native
                    // Player.Update transfer its result through UpdateBiomes.
                    // Never force a Zone flag or keep initial biome counts frozen.
                    Game.player[0].UpdateSceneMetrics();
                    nativeSceneMetricRefreshes++;
                    update();
                    VerifyNativeBattleContext();
                    engineSamples.Add((Stopwatch.GetTimestamp()-nativeStart)*1000d/Stopwatch.Frequency);
                    AfterNativeUpdate();
                    // Keep the native loop in step with the trainer. The old
                    // bound was "Thread.Sleep(1) every fourth tick", and on this
                    // machine Sleep(1) really costs 16.04 ms (measured; the
                    // default 15.6 ms timer resolution), which capped a session
                    // at ~250 ticks/s -- measured 196 ticks/s with a static
                    // action file and no trainer at all, i.e. the sleep, not the
                    // engine or the trainer, was the ceiling. Waiting on the
                    // trainer's own action tick instead lets the engine run at
                    // full speed while still refusing to run more than
                    // BridgeLagLimit ticks ahead, which is what keeps the
                    // recorded (observation, action) pairs aligned.
                    // The lockstep is the handshake with the TRAINER's
                    // <base>.action file, so it keys off bridgeDriven (i.e.
                    // CHAITE_BRIDGE_FILE, the same variable the PLUGIN reads to
                    // decide a replay owns movement). It deliberately does NOT
                    // key off bridgeBase: CHAITE_PROBE_OUT only says where this
                    // probe writes, and a deployment rehearsal sets that with no
                    // trainer behind it. Keying the wait off the output path made
                    // such a run stall 3 x 30 s at the round start.
                    if(bridgeDriven && !bridgeLockstepDisabled)
                    {
                        long waitStart = Stopwatch.GetTimestamp();
                        int spins = 0;
                        while(true)
                        {
                            int actionTick = ReadBridgeActionTick();
                            // No action yet (round start, or the trainer is still
                            // loading): hold the loop, but never forever -- a dead
                            // trainer must not wedge the round past its wall clock.
                            if(actionTick >= 0 && ticks - actionTick <= BridgeLagLimit) break;
                            if(++spins > 4000000) break;
                            double waitedMs = (Stopwatch.GetTimestamp()-waitStart)*1000d/Stopwatch.Frequency;
                            if(waitedMs > 30000d)
                            {
                                // Measured: a frozen action file makes every tick
                                // wait the full timeout, i.e. the engine crawls at
                                // one tick per 30 s. Give up on lockstep for the
                                // rest of the run after a few such stalls rather
                                // than burning the whole round on them.
                                if(++bridgeWaitTimeouts >= 3) bridgeLockstepDisabled = true;
                                Log("BRIDGE_LOCKSTEP timeout=" + bridgeWaitTimeouts +
                                    " tick=" + ticks + " actionTick=" + actionTick +
                                    " disabled=" + bridgeLockstepDisabled);
                                break;
                            }
                            // Graduated yield: spin briefly for a tight loop, then
                            // give up the slice, and only fall back to the 16 ms
                            // timer granularity once the trainer is clearly behind.
                            if(waitedMs < 1d) System.Threading.Thread.SpinWait(50);
                            else if(waitedMs < 4d) System.Threading.Thread.Sleep(0);
                            else System.Threading.Thread.Sleep(1);
                        }
                        bridgeWaitSpins += spins;
                    }
                    // Pace the loop only when no trainer is waiting on it; with a
                    // trainer the lockstep above already bounds the lead.
                    else if(!bridgeDriven && (ticks&3)==0) System.Threading.Thread.Sleep(1);
                }
            }
        }
        catch(Exception e) { Fail(e); }
    }

    public static void InitializeSocial(SocialMode? ignored)
    {
        if(IsMotion) throw new InvalidOperationException("Motion microtests require the headless entry point");
        SocialAPI.Initialize(SocialMode.None);
        Log("SOCIAL none; no Steam/cloud/session connection");
    }

    public static void EarlyInitialize()
    {
        try
        {
            string expected = Path.GetFullPath(Path.Combine(Root, "Save"));
            if (!Path.GetFullPath(Terraria.Program.SavePath).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsafe save path: refusing to run");
            Game.autoSave = false;
            Game.autoPause = false;
            Game.SettingPlayWhenUnfocused = true;
            Game.ThrottleWhenInactive = false;
            Game.musicVolume = Game.soundVolume = Game.ambientVolume = 0f;
            RaiseTimerResolution();
            Log("EARLY vanilla=" + typeof(Game).Assembly.GetName().Version + " save=" + expected);
        }
        catch (Exception e) { Fail(e); }
    }

    /// <summary>
    /// Headless mode registers no sky effects, so a long session that crosses
    /// a world-event boundary -- slime rain, a blood moon, an eclipse -- dies
    /// inside EffectManager.Activate with MissingEffectException. Measured:
    /// episode training died at tick 213541, about two and a half game days
    /// in, the moment UpdateTime rolled a slime rain. The headless harness
    /// never draws, so a no-op sky under every name the world clock can
    /// activate is behaviourally invisible; it only makes the activation
    /// find something.
    /// </summary>
    static void RegisterNoopSkies()
    {
        var sky=Terraria.Graphics.Effects.SkyManager.Instance;
        // Terraria.Initializers.ScreenEffectInitializer.LoadSkies is the
        // authoritative registration list -- IL-dumped, 19 names. The earlier
        // revision carried only 11 guesses and missed 15 of them, so a Lantern
        // Night killed a training session at ~200k ticks with
        // MissingEffectException "Unable to find effect named: Lantern", the
        // same way the slime rain did before it. The extra names that LoadSkies
        // does not register are harmless: a lookup for a name the clock never
        // activates simply never happens.
        string[] names={"Slime","BloodMoon","Eclipse","MoonLord","PumpkinMoon",
            "ChristmasMoon","Rain","Blizzard","Sandstorm","Hallow","Tower",
            "Party","Martian","Nebula","Stardust","Vortex","Solar","CreditsRoll",
            "Aurora","MonolithNebula","MonolithStardust","MonolithVortex",
            "MonolithSolar","MonolithMoonLord","Ambience","Lantern"};
        int added=0;
        foreach(var name in names)
        {
            try
            {
                var existing=sky[name];
                if(existing!=null) continue;
            }
            catch
            {
                // Not registered: the lookup itself is the presence test.
            }
            sky[name]=new NoopSky();
            added++;
        }
        if(added>0) Log("NOOP_SKIES added="+added+" names="+string.Join(",",names));
    }

    sealed class NoopSky : Terraria.Graphics.Effects.CustomSky
    {
        public override void Update(Microsoft.Xna.Framework.GameTime gameTime) { }
        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) { }
        public override bool IsActive() { return false; }
        public override void Reset() { }
        public override bool IsVisible() { return false; }
        public override void Activate(Vector2 position, object[] args) { }
        public override void Deactivate(object[] args) { }
    }

    public static void ContentReady()
    {
        try
        {
            ReadSettings();
            RegisterNoopSkies();
            Log("CONTENT_READY");
            Game.autoSave = false;
            Game.autoPause = false;
            Game.SettingPlayWhenUnfocused = true;
            Game.ThrottleWhenInactive = false;
            Game.musicVolume = Game.soundVolume = Game.ambientVolume = 0f;
            Game.maxTilesX = 4200;
            Game.maxTilesY = 1200;
            WorldGen.setWorldSize();
            WorldGen.clearWorld();
            // clearWorld deliberately leaves unloaded cells null. Vanilla Player
            // early-outs before equipment/movement unless neighboring cells exist.
            for(int x=0;x<Game.maxTilesX;x++)
            for(int y=0;y<Game.maxTilesY;y++)
                if(Game.tile[x,y]==null) Game.tile[x,y]=new Tile();
            Game.worldSurface = 500;
            Game.rockLayer = 750;
            int arenaCenterX=scenario.Ocean?200:2100;
            int arenaGroundY=scenario.Underworld?Game.maxTilesY-140:scenario.Jungle?700:500;
            int arenaStartY=arenaGroundY;
            arenaCenterXTiles=arenaCenterX;
            arenaGroundYTiles=arenaGroundY;
            arenaStartYTiles=arenaStartY;
            launchStartSide=requestedStartSide;
            Game.spawnTileX = arenaCenterX;
            Game.spawnTileY = arenaStartY-2;
            Game.worldName = "Chaite isolated engine test";
            Game.dayTime = scenario.Daytime;
            Game.time = scenario.Daytime?27000:1000;
            // No reviewed route is rain-gated any more: the Shrimpy Truffle route
            // was withdrawn as out of scope, so the fixture is always dry and the
            // native weather fields are left at their defaults.
            Game.hardMode = scenario.HardMode;
            Game.wofNPCIndex = -1;
            Game.netMode = 0;
            Game.myPlayer = 0;
            // AI_069 defines the ocean band as the first/last 400 tiles. Keep
            // the entire Fishron fixture inside that band and give it a real
            // shoreline floor all the way to the world edge. The old 80-tile
            // empty gap let the player fall below worldSurface and activated
            // native enrage for a reason unrelated to the strategy.
            int groundLeft=ArenaGroundLeft;
            int groundRight=ArenaGroundRightExclusive;
            int groundThickness=scenario.Snow?12:6;
            ushort groundType=scenario.Hallow?TileID.Pearlstone:scenario.Jungle?TileID.JungleGrass:
                scenario.Snow?TileID.IceBlock:TileID.GrayBrick;
            // The ground is built past the arena's own right edge, to
            // SafetyFloorRightExclusive. Measured on the recorded obsb1 stream:
            // 15.0% of frames sat past x=6400 (tile 400, the end of the ocean
            // band) because nothing stops the player from flying out over the
            // beach, and the below-floor excursions out there go 9-10k px deep
            // -- that is a void fall, and it was 3.2% of all frames. Both
            // recorded zero-hit wins stayed inside 511 tiles of x, but the deep
            // falls are a failure mode the policy can trivially avoid by not
            // flying out, so leaving unfloored ground there teaches it nothing
            // except that the region is lethal. The safety floor does NOT move
            // the fight: the arena's published bounds, the start position and
            // the enrage band are all unchanged, and the ocean band (tiles
            // 1..400) still carries the fight.
            int groundRightExclusive=Math.Max(groundRight,
                scenario.Ocean?SafetyFloorRightExclusive:groundRight);
            for (int x = groundLeft; x < groundRightExclusive; x++)
            for (int y = arenaGroundY; y < arenaGroundY+groundThickness; y++)
            {
                if (Game.tile[x,y] == null) Game.tile[x,y] = new Tile();
                Game.tile[x,y].active(true);
                Game.tile[x,y].type = groundType;
            }
            if (scenario.Ocean && !IsMonitorFixture)
            {
                // Legacy fishing fixture only. The monitor combat fixture
                // models a dry coastal runway above the ocean, not a player
                // submerged in 18 tiles of water (which halves motion speed).
                for (int x = groundLeft; x < groundRight; x++)
                for (int y = arenaGroundY - 18; y < arenaGroundY; y++)
                {
                    if (Game.tile[x,y] == null) Game.tile[x,y] = new Tile();
                    // Keep the legacy basin free of solid blocks.
                    Game.tile[x,y].active(false);
                    Game.tile[x,y].liquid = 255;
                    Game.tile[x,y].liquidType(0);
                }
            }
            // Refuted, and left here as the measurement: lining the arena with
            // pearlstone BACKGROUND WALLS changes nothing. holyTiles stayed
            // exactly 1014/0 at the same offsets with and without them, so walls
            // do not feed HolyTileCount and cannot make a hollow airspace Hallow.
            // No ceiling is built either: the arena is open sky above the top
            // platform row.
            //
            // Platform rows ARE built, for the combat fixture only. An earlier
            // revision had two rows and removed them as invented, because the
            // fight the plugin drove happened on one flat ground -- and for the
            // strong wing that is still true, so it keeps its flat-ground fight.
            // It is not true for the other three loadouts: measured wing time is
            // 100 (Trusty Chillet, Lilith's Necklace) and 130 (Fairy Wings)
            // against 180 for the strong wing, and those arms died around tick
            // 1,100 with the Boss still above 80% life. On a single flat surface a
            // spent wing is a fall with nothing to catch it, so they never got to
            // fly the fight at all. The owner's fix is what is built here: three
            // horizontal layers including the ground, 60 tiles apart, which is
            // about one wing charge of climb between layers.
            //
            // The rows span the arena's own horizontal bounds, not the safety
            // floor, and their y values are published straight from this array.
            // The old revision built rows across 1..399 while the arena block
            // claimed a different span, so a reader of the evidence could not
            // have caught the drift; publishing the same expression removes that
            // possibility.
            if (scenario.Ocean && IsMonitorFixture)
            {
                arenaPlatformRows = new int[]
                {
                    arenaGroundY - PlatformRowSpacingTiles,
                    arenaGroundY - 2 * PlatformRowSpacingTiles
                };
                for (int r = 0; r < arenaPlatformRows.Length; r++)
                for (int x = groundLeft; x < groundRight; x++)
                {
                    int rowY = arenaPlatformRows[r];
                    if (Game.tile[x,rowY] == null) Game.tile[x,rowY] = new Tile();
                    Game.tile[x,rowY].active(true);
                    Game.tile[x,rowY].type = TileID.Platforms;
                    // Wooden style. The frame only selects the sprite and the
                    // fixture is headless, but leaving it at the style's own
                    // value keeps the tile well formed.
                    Game.tile[x,rowY].frameY = 0;
                }
            }
            else
            {
                arenaPlatformRows = new int[0];
            }
            // Read straight back out of the world, so the evidence block reports
            // what was actually placed rather than what was intended.
            arenaPlatformTileCount = CountPlatformTiles();
            var player = new Player();
            player.name = "Chaite Lab";
            player.whoAmI = 0;
            player.active = true;
            player.statLifeMax = player.statLife = scenario.MaxLife;
            player.statManaMax = player.statMana = 200;
            // The real fight does not start the player in the middle of the
            // ground. Empress starts at the centre of its runway; Duke Fishron
            // starts about twenty tiles from one end, and which end varies per
            // fight, so the plugin identifies the side at run time and mirrors
            // its route. A fixture that always spawned at the centre therefore
            // measured a start position the game never produces, and for Fishron
            // it handed the policy two hundred tiles of retreat room on both
            // sides where the real start leaves almost none on one.
            int playerStartTileX = PlayerStartTileX(arenaCenterX);
            player.position = new Vector2(playerStartTileX * 16, arenaStartY * 16 - player.height);
            player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            EquipScenario(player);
            // The loadout's mount has to exist in the first episode too. The
            // per-episode reset summons it (SummonLoadoutMount), and that was
            // believed to cover the whole session, but the reset only runs from
            // episode 1 onward: measured on fishron-queen-slime, episode 0 had
            // mountActive=0 for all 899 of its ticks while episode 1 had the
            // mount for all 151 of its own. Episode 0 is the episode an exported
            // route is replayed in, so the acceptance verdict was being produced
            // with the loadout's mount missing -- and the 653 airborne ticks that
            // episode 0 showed under a held jump were the wings, not the mount
            // (wingTime fell 100 -> 0). Summon it here, on the boot path, so
            // every episode flies the loadout as equipped.
            SummonLoadoutMount(player);
            Game.player[0] = player;
            initialPosition=player.position;
            var pfd = new PlayerFileData(Path.Combine(Root,"Save","Players","ChaiteLab.plr"), false);
            pfd.Player = player;
            Game.ActivePlayerFileData = pfd;
            Game.ActiveWorldFileData = new WorldFileData(Path.Combine(Root,"Save","Worlds","ChaiteLab.wld"), false);
            Game.ActiveWorldFileData.SetWorldSize(4200,1200);
            Game.ActiveWorldFileData.Name = Game.worldName;
            Game.ActiveWorldFileData.SetSeed(seed.ToString(CultureInfo.InvariantCulture));
            // Persisted vanilla worlds always carry both native identity
            // fields. This in-memory fixture is never saved, so the constructor
            // cannot populate them from a world header; give it a deterministic
            // per-case identity rather than weakening the production session
            // gate for Guid.Empty.
            Game.ActiveWorldFileData.UniqueId = new Guid(seed, 0x4348, 0x4149,
                0x54, 0x45, 0x4c, 0x41, 0x42, 0, 0, 1);
            Game.ActiveWorldFileData.WorldId = seed == 0 ? 1 : seed;
            // Main.GameMode is a view over the current WorldFileData, not an
            // independent global field. Set it only AFTER installing the final WFD.
            Game.GameMode = difficultyCode;
            VerifyNativeDifficulty();
            Game.Map = new Terraria.Map.WorldMap(4200,1200);
            Game.sectionManager = new WorldSections(Game.maxSectionsX,Game.maxSectionsY);
            Game.sectionManager.SetAllSectionsLoaded();
            Game.gameMenu = false;
            Game.gamePaused = false;
            Game.menuMode = 0;
            Game.playerInventory = false;
            Game.showItemText = false;
            lastLife = player.statLife;
            minLife = player.statLife;
            booted = true;
            clock.Restart();
            Log("ARENA_READY in-memory; "+difficulty+" life"+scenario.MaxLife+" "+scenario.Equipment+"; no godmode; no user saves");
        }
        catch(Exception e) { Fail(e); }
    }

    static void VerifyNativeDifficulty()
    {
        var expectedLevel=difficultyCode==2?Terraria.DataStructures.GameDifficultyLevel.Master:
            difficultyCode==1?Terraria.DataStructures.GameDifficultyLevel.Expert:Terraria.DataStructures.GameDifficultyLevel.Classic;
        bool forTheWorthy=NativeWorldFlag("getGoodWorld");
        bool zenithWorld=NativeWorldFlag("zenithWorld");
        bool drunkWorld=NativeWorldFlag("drunkWorld");
        bool notTheBeesWorld=NativeWorldFlag("notTheBeesWorld");
        bool remixWorld=NativeWorldFlag("remixWorld");
        bool celebrationWorld=NativeWorldFlag("tenthAnniversaryWorld");
        bool constantWorld=NativeWorldFlag("dontStarveWorld");
        bool noTrapsWorld=NativeWorldFlag("noTrapsWorld");
        bool skyblockWorld=NativeWorldFlag("skyblockWorld");
        bool expectsZenith=scenario!=null && scenario.ExpectedVariant=="getfixedboi-mechdusa";
        nativeDifficultyReport=new Dictionary<string,object>
        {
            {"gameMode",Game.GameMode},{"difficulty",Game.Difficulty},{"expertMode",Game.expertMode},
            {"masterMode",Game.masterMode},{"hardMode",Game.hardMode},{"forTheWorthy",forTheWorthy},
            {"zenithWorld",zenithWorld},{"drunkWorld",drunkWorld},{"notTheBeesWorld",notTheBeesWorld},
            {"remixWorld",remixWorld},{"celebrationWorld",celebrationWorld},{"constantWorld",constantWorld},
            {"noTrapsWorld",noTrapsWorld},{"skyblockWorld",skyblockWorld},
            {"worldFileGameMode",Game.ActiveWorldFileData==null?-1:Game.ActiveWorldFileData.GameMode},
            {"worldFileSeed",Game.ActiveWorldFileData==null?-1:Game.ActiveWorldFileData.Seed}
        };
        if(Game.ActiveWorldFileData==null || Game.GameMode!=difficultyCode ||
            Game.ActiveWorldFileData.GameMode!=difficultyCode || Game.Difficulty!=expectedLevel ||
            Game.expertMode!=(difficultyCode>0) || Game.masterMode!=(difficultyCode==2) ||
            Game.hardMode!=scenario.HardMode || Game.ActiveWorldFileData.Seed!=seed ||
            (expectsZenith ? !zenithWorld :
                forTheWorthy || zenithWorld || drunkWorld || notTheBeesWorld || remixWorld ||
                celebrationWorld || constantWorld || noTrapsWorld || skyblockWorld))
            throw new InvalidOperationException("Native difficulty/seed mismatch: "+Json(nativeDifficultyReport));
        nativeDifficultyVerified=true;
        Log("NATIVE_DIFFICULTY "+Json(nativeDifficultyReport));
    }

    static void InstallBattleRandom()
    {
        VerifyNativeDifficulty();
        var namedRngs=typeof(Game).GetField("_rngs",BindingFlags.Static|BindingFlags.NonPublic);
        if(namedRngs==null) throw new MissingFieldException("Native named random-stream registry unavailable");
        // Native Main.SwapRandom creates each named stream from the WFD seed.
        // Discard setup streams just as native Initialize creates a new registry.
        namedRngs.SetValue(null,new Dictionary<string,Terraria.Utilities.UnifiedRandom>());
        battleRandom=new Terraria.Utilities.UnifiedRandom(seed);
        Game.rand=battleRandom;
        initialUnpausedUpdateSeed=(ulong)(uint)seed;
        expectedUnpausedUpdateSeed=initialUnpausedUpdateSeed;
        var unpausedProperty=typeof(Game).GetProperty("UnpausedUpdateSeed",BindingFlags.Public|BindingFlags.Static);
        var unpausedSetter=unpausedProperty==null?null:unpausedProperty.GetSetMethod(true);
        if(unpausedSetter==null) throw new MissingMethodException("Native UnpausedUpdateSeed setter unavailable");
        setUnpausedUpdateSeed=(Action<ulong>)Delegate.CreateDelegate(typeof(Action<ulong>),unpausedSetter);
        setUnpausedUpdateSeed(initialUnpausedUpdateSeed);
        var fingerprintTwin=new Terraria.Utilities.UnifiedRandom(seed);
        VerifyFreshRandomState(battleRandom,fingerprintTwin);
        using(Game.SwapRandom("DoUpdateInWorld")) VerifyFreshRandomState(Game.rand,fingerprintTwin);
        if(!ReferenceEquals(Game.rand,battleRandom)) throw new InvalidOperationException("Native random swap did not restore the battle RNG");
        battleRandomColdStateVerified=true;
        battleRandomFingerprint=new int[8];
        for(int i=0;i<battleRandomFingerprint.Length;i++) battleRandomFingerprint[i]=fingerprintTwin.Next();
        battleRandomInstalled=true;
        Log("BATTLE_RNG_INSTALLED seed="+seed+" independentTwinFingerprint="+Json(battleRandomFingerprint)+
            " unpausedInitial="+initialUnpausedUpdateSeed+
            " namedStreams=native-WFD-seed fresh-registry; actual battle RNG not sampled");
    }

    static void VerifyFreshRandomState(Terraria.Utilities.UnifiedRandom actual,Terraria.Utilities.UnifiedRandom expected)
    {
        // Read native state without drawing even one value from the battle RNG.
        // This binds the displayed independent-twin fingerprint to the real seed.
        var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var cursor=typeof(Terraria.Utilities.UnifiedRandom).GetField("inext",flags);
        var state=typeof(Terraria.Utilities.UnifiedRandom).GetField("SeedArray",flags);
        if(actual==null || cursor==null || state==null || !Equals(cursor.GetValue(actual),cursor.GetValue(expected)))
            throw new InvalidOperationException("Native RNG initial cursor mismatch");
        var actualState=state.GetValue(actual) as int[];
        var expectedState=state.GetValue(expected) as int[];
        if(actualState==null || expectedState==null || actualState.Length!=expectedState.Length)
            throw new InvalidOperationException("Native RNG initial state shape mismatch");
        for(int i=0;i<actualState.Length;i++)
            if(actualState[i]!=expectedState[i]) throw new InvalidOperationException("Native RNG initial state differs at index "+i);
    }

    static void VerifyNativeBattleContext()
    {
        battleRandomReferenceChecks++;
        if(!battleRandomInstalled || !ReferenceEquals(Game.rand,battleRandom))
            throw new InvalidOperationException("Native Main.rand changed outside a scoped swap at tick "+ticks);
        if(Game.UnpausedUpdateSeed!=expectedUnpausedUpdateSeed)
            throw new InvalidOperationException("Native UnpausedUpdateSeed changed outside its expected outer-frame advance at tick "+ticks);
        if(Game.ActiveWorldFileData==null || Game.ActiveWorldFileData.Seed!=seed || Game.GameMode!=difficultyCode ||
            Game.expertMode!=(difficultyCode>0) || Game.masterMode!=(difficultyCode==2))
            throw new InvalidOperationException("Native difficulty/world seed changed during battle at tick "+ticks);
    }

    static void EquipScenario(Player player)
    {
        if(IsMotion)
        {
            // Only fixture equipment changes here. Jump counters, release edges,
            // cloud availability, gravity and velocity are NEVER manufactured.
            foreach(var item in player.inventory) item.SetDefaults(0);
            foreach(var item in player.armor) item.SetDefaults(0);
            foreach(var item in player.miscEquips) item.SetDefaults(0);
            bool cloud=IsFlight?FlightHasCloud:motionCase.StartsWith("cloud-",StringComparison.Ordinal);
            // Loadout 1 of the seven reviewed Fishron sets. The wiki says the wolf
            // cannot fly or double jump and cannot use wings, boots or carpets,
            // but CAN use every extra-jump accessory, which is the entire reason
            // this loadout exists. "plain" is the mount with no extra-jump item
            // and is the control that separates the mount from the balloons.
            bool lilith=!IsFlight&&motionCase.StartsWith("lilith-",StringComparison.Ordinal);
            bool balloons=lilith&&motionCase.IndexOf("balloon",StringComparison.Ordinal)>=0;
            // Featherfall is a separate axis rather than part of every Lilith
            // case. Measured in this fixture, a featherfall descent covers only
            // about 3.3 px/tick in its last thirty frames against normal
            // gravity's much larger terminal speed, so a jump arc that lands
            // inside the 180-frame budget without it does not land with it. The
            // cases that include it are named and need a longer budget, which is
            // deferred rather than smuggled in by weakening the landing check.
            bool featherfall=lilith&&motionCase.IndexOf("featherfall",StringComparison.Ordinal)>=0;
            if(IsFlight)
            {
                player.armor[3].SetDefaults(ItemID.DemonWings);
                if(FlightHasLightning) player.armor[4].SetDefaults(ItemID.LightningBoots);
                if(cloud) player.armor[5].SetDefaults(ItemID.CloudinaBottle);
                // Use the native buff API once during isolated fixture setup.
                // Never assign slowFall directly: UpdateBuffs must derive it on
                // every measured frame exactly as it does for a real potion.
                if(FlightHasFeatherfall) player.AddBuff(BuffID.Featherfall,36000);
            }
            else if(lilith)
            {
                // Staged through the mount equipment slot exactly as the reviewed
                // Queen Slime and chillet fixtures do, because equipping the item
                // is the production path and it is what the facade reads back.
                // Mount 52 is item 5130 per VanillaMountCatalog.
                player.miscEquips[3].SetDefaults(5130);
                if(balloons) player.armor[3].SetDefaults(1164);
                // Featherfall is applied through the same native buff call the
                // flight fixture uses, so UpdateBuffs derives the fall behaviour
                // every frame exactly as it would for a real potion. It is only
                // added for the cases that name it; see the note above.
                if(featherfall) player.AddBuff(BuffID.Featherfall,36000);
            }
            else if(cloud) player.armor[3].SetDefaults(ItemID.CloudinaBottle);
            var equipped=new int[player.armor.Length];
            for(int i=0;i<equipped.Length;i++) equipped[i]=player.armor[i].type;
            scenario.Equipment=IsFlight?"flight: naked + unprefixed "+FlightProfile:
                lilith?"motion: naked + Lilith's Necklace (mount 52)"+(balloons?" + Bundle of Balloons (1164)":"")+(featherfall?" + featherfall":""):
                cloud?"motion: naked + unprefixed Cloud in a Bottle only":"motion: naked, no accessories";
            equipmentReport=new Dictionary<string,object>
            {
                {"label",scenario.Equipment},{"life",400},{"mana",200},{"armorAndAccessories",equipped},
                {"cloudEquipped",cloud},{"cloudItemType",cloud?ItemID.CloudinaBottle:0},{"cloudPrefix",player.armor[IsFlight?5:3].prefix},
                // The mount is staged, so the old blanket claim that the fixture
                // carries no mount would now contradict the staging. It stays
                // true for every pre-existing case and turns false only for the
                // Lilith cases, and the mount is reported explicitly alongside.
                {"mountItemType",lilith?5130:0},{"mountExpectedType",lilith?52:0},
                {"bundleOfBalloonsItemType",balloons?1164:0},
                {"featherfallActive",IsFlight?FlightHasFeatherfall:featherfall},
                {"noWeaponsAmmoConsumablesOrMount",!lilith},{"noDirectJumpStateOverrides",true}
            };
            if(IsFlight)
            {
                equipmentReport["flightProfile"]=FlightProfile;
                equipmentReport["wingItemType"]=ItemID.DemonWings;
                equipmentReport["wingPrefix"]=player.armor[3].prefix;
                equipmentReport["lightningEquipped"]=FlightHasLightning;
                equipmentReport["bootsItemType"]=FlightHasLightning?ItemID.LightningBoots:0;
                equipmentReport["bootsPrefix"]=player.armor[4].prefix;
                equipmentReport["featherfallActive"]=FlightHasFeatherfall;
                equipmentReport["featherfallBuffType"]=FlightHasFeatherfall?BuffID.Featherfall:0;
                equipmentReport["featherfallSourceItemType"]=FlightHasFeatherfall?ItemID.FeatherfallPotion:0;
                equipmentReport["featherfallSetupViaNativeAddBuff"]=FlightHasFeatherfall;
                equipmentReport["noDirectFlightStateOverrides"]=true;
            }
            return;
        }
        foreach(var item in player.inventory) item.SetDefaults(0);
        foreach(var item in player.armor) item.SetDefaults(0);
        foreach(var item in player.miscEquips) item.SetDefaults(0);
        player.extraAccessory=false;

        int weaponType=ItemID.Minishark;
        int ammoType=ItemID.MusketBall;
        int healingType=ItemID.HealingPotion;
        switch(scenario.EquipmentTier)
        {
            case "late-pre-hardmode":
                weaponType=ItemID.PhoenixBlaster;
                ammoType=ItemID.MeteorShot;
                break;
            case "early-hardmode":
                // The exact Clockwork burst route conservatively credits all
                // three native shots and clears Fishron's production output
                // threshold without relying on Onyx secondary projectiles.
                weaponType=ItemID.ClockworkAssaultRifle;
                ammoType=ItemID.IchorBullet;
                healingType=ItemID.GreaterHealingPotion;
                break;
            case "post-plantera":
                weaponType=ItemID.ChainGun;
                ammoType=ItemID.IchorBullet;
                healingType=ItemID.GreaterHealingPotion;
                break;
            case "pre-moon-lord":
                // A Chain Gun is a legal post-Plantera, pre-Moon-Lord weapon
                // already covered by the exact ballistic catalogue. Replace it
                // only when the native Vortex Beater family is source-locked;
                // an unsupported stronger weapon would make the controller
                // fail closed and invalidate the fixture rather than improve it.
                weaponType=ItemID.ChainGun;
                ammoType=ItemID.IchorBullet;
                healingType=ItemID.GreaterHealingPotion;
                break;
        }
        player.inventory[0].SetDefaults(weaponType);
        player.inventory[1].SetDefaults(scenario.Summon);
        player.inventory[1].stack=scenario.Summon==0?0:1;
        if (scenario.Id=="duke-fishron" && scenario.Summon==2673)
        {
            // Golden Fishing Rod is a fully native rod identity; the fixture
            // keeps it in the hotbar so the real Truffle Worm path can select
            // it before the special bait is consumed.
            player.inventory[2].SetDefaults(ItemID.GoldenFishingRod);
            Log("FISHRON_ROD_FIXTURE type="+player.inventory[2].type+" fishingPole="+player.inventory[2].fishingPole+" bait="+player.inventory[1].type);
        }
        player.inventory[54].SetDefaults(ammoType);
        player.inventory[54].stack=9999;
        player.miscEquips[4].SetDefaults(ItemID.GrapplingHook);
        if(scenario.Legacy)
        {
            // Preserve the exact final02/final03 successful Eye fixture. Its later
            // pre-Hardmode gear is labeled legacy and excluded from new-case claims.
            player.armor[0].SetDefaults(ItemID.MoltenHelmet);
            player.armor[1].SetDefaults(ItemID.MoltenBreastplate);
            player.armor[2].SetDefaults(ItemID.MoltenGreaves);
            player.armor[3].SetDefaults(ItemID.SpectreBoots);
            player.armor[4].SetDefaults(ItemID.CloudinaBottle);
            player.armor[5].SetDefaults(ItemID.EoCShield);
            scenario.Equipment="legacy molten+spectre+cloud+shield+minishark; no healing items";
        }
        else if(scenario.EquipmentTier=="late-pre-hardmode")
        {
            player.armor[0].SetDefaults(ItemID.NecroHelmet);
            player.armor[1].SetDefaults(ItemID.NecroBreastplate);
            player.armor[2].SetDefaults(ItemID.NecroGreaves);
            player.armor[3].SetDefaults(ItemID.SpectreBoots);
            player.armor[4].SetDefaults(ItemID.CloudinaBottle);
            player.armor[5].SetDefaults(ItemID.ObsidianShield);
            player.armor[6].SetDefaults(ItemID.BandofRegeneration);
            player.armor[7].SetDefaults(ItemID.Aglet);
            scenario.Equipment="late-pre-Hardmode necro+spectre+cloud+obsidian-shield+regen-band+aglet+phoenix-blaster+meteor-shot; healing x20";
        }
        else if(scenario.EquipmentTier=="early-hardmode")
        {
            player.armor[0].SetDefaults(ItemID.AdamantiteMask);
            player.armor[1].SetDefaults(ItemID.AdamantiteBreastplate);
            player.armor[2].SetDefaults(ItemID.AdamantiteLeggings);
            player.armor[3].SetDefaults(ItemID.LightningBoots);
            player.armor[4].SetDefaults(ItemID.DemonWings);
            player.armor[5].SetDefaults(ItemID.CharmofMyths);
            player.armor[6].SetDefaults(ItemID.ObsidianShield);
            player.armor[7].SetDefaults(ItemID.SorcererEmblem);
            if(difficultyCode>0)
            {
                // A Demon Heart is available after the Wall of Flesh, before these
                // encounters. The shield comes from the earlier Expert Eye fight.
                player.extraAccessory=true;
                player.armor[8].SetDefaults(ItemID.EoCShield);
            }
            scenario.Equipment="early-Hardmode adamantite-ranger+lightning+demon-wings+charm+obsidian-shield+ranger-emblem+clockwork-rifle+ichor-bullets; greater-healing x20";
            if (scenario.Id=="duke-fishron")
            {
                // Fishron's reviewed minimum route requires Fairy Wings (or
                // equivalent) plus a reliable dash/evade source; the generic
                // early-Hardmode fixture's Demon Wings alone is insufficient.
                //
                // 2026-09-21: this block is the TRAINING path (EquipmentTier
                // "early-hardmode", selected by L2030 for every non-monitor run).
                // The same corrections had already been written into the
                // post-plantera block below, but that block only runs for the
                // monitor fixture, so training kept using the older gear:
                //   * the mount sets carried no Bundle of Balloons and no
                //     featherfall, against the user's confirmed 2026-09-19 ruling;
                //   * fishron-lilith-wolf matched no branch at all and fell into
                //     the wing `else`, so that loadout was trained riding wings
                //     instead of Lilith's Wolf.
                // Both are now mirrored here so the training fight carries the
                // same set the real loadout does.
                if(formulaRoute=="fishron-lilith-wolf")
                {
                    // Lilith's Necklace is item 5130 and summons MountID.Wolf
                    // (mount 52). Reviewed loadout: necklace + Bundle of Balloons
                    // + featherfall.
                    player.miscEquips[3].SetDefaults(5130);
                    player.armor[3].SetDefaults(1164);
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: fishron-lilith-wolf";
                }
                else if(formulaRoute=="fishron-trusty-chillet" ||
                    formulaRoute=="fishron-trusty-chillet-ignis")
                {
                    player.miscEquips[3].SetDefaults(formulaRoute==
                        "fishron-trusty-chillet"?ItemID.PalworldMountTrustyChillet:
                        ItemID.PalworldMountTrustyChilletIgnis);
                    player.armor[3].SetDefaults(1164);
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: "+formulaRoute;
                }
                else
                {
                    player.armor[4].SetDefaults(formulaRoute==
                        "fishron-strong-wing"?ItemID.FishronWings:ItemID.FairyWings);
                    player.armor[6].SetDefaults(ItemID.FrogLeg);
                    player.armor[7].SetDefaults(ItemID.EoCShield);
                    if(difficultyCode>0)
                        player.armor[8].SetDefaults(ItemID.RangerEmblem);
                    // Every non-broom loadout carries featherfall (user ruling
                    // 2026-09-21); applied through the native buff call so
                    // UpdateBuffs derives the fall behaviour every frame.
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: "+
                        (formulaRoute??"fishron-fairy-wing");
                }
            }
        }
        else if(scenario.EquipmentTier=="post-plantera")
        {
            player.armor[0].SetDefaults(ItemID.ShroomiteMask);
            player.armor[1].SetDefaults(ItemID.ShroomiteBreastplate);
            player.armor[2].SetDefaults(ItemID.ShroomiteLeggings);
            // Reuse the hash-reviewed Demon-Wings/Lightning-Boots movement
            // implementation. Hoverboard and Master Ninja Gear are stronger
            // items, but do not yet have this probe's exact production closure.
            player.armor[3].SetDefaults(ItemID.LightningBoots);
            player.armor[4].SetDefaults(ItemID.DemonWings);
            player.armor[5].SetDefaults(ItemID.FrozenTurtleShell);
            player.armor[6].SetDefaults(ItemID.CharmofMyths);
            player.armor[7].SetDefaults(ItemID.RangerEmblem);
            // The reviewed Shield-of-Cthulhu dash is the minimum defensive
            // edge used by the daytime Empress formulaic dodge.  Keep the
            // classic fixture from being denied that same baseline mobility.
            player.extraAccessory=true;
            player.armor[8].SetDefaults(ItemID.EoCShield);
            // The headless fixture does not run the full vanity/functional
            // accessory refresh that a rendered client applies every frame, so
            // expose the reviewed Shield-of-Cthulhu dash state explicitly.
            player.dashType=2;
            player.dashDelay=0;
            player.dashTime=0;
            scenario.Equipment="post-Plantera shroomite-bullet+lightning+demon-wings+frozen-turtle-shell+charm+ranger-emblem+chain-gun+ichor-bullets; greater-healing x20";
            if (IsMonitorFixture && scenario.Id == "duke-fishron")
            {
                // Reviewed Fishron formula identities. Amphibian Boots (3990) is
                // the literal "frog boots" source; Fishron Wings (2609) and
                // Fairy Wings (761) are the two reviewed wing tiers, and the
                // Shield of Cthulhu (3097) is the reviewed dash source.
                //
                // A reviewed loadout is a whole SET, not one item, and the boots
                // belong to the wing sets only. The user confirmed on
                // 2026-09-19 that the mount sets carry no boots, and that set 4
                // is the mount plus the Bundle of Balloons (1164) and
                // featherfall. The fixture previously gave every Fishron route
                // the boots and gave set 4 neither the balloons nor the potion,
                // so both mount sets were training against gear nobody wears.
                if(formulaRoute=="fishron-lilith-wolf")
                {
                    // 2026-09-21 user ruling: the mount sets carry the Bundle of
                    // Balloons too, and every non-broom loadout carries
                    // featherfall.
                    player.armor[3].SetDefaults(1164);
                    player.miscEquips[3].SetDefaults(5130);
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: fishron-lilith-wolf";
                }
                else if(formulaRoute=="fishron-trusty-chillet" ||
                    formulaRoute=="fishron-trusty-chillet-ignis")
                {
                    player.miscEquips[3].SetDefaults(formulaRoute==
                        "fishron-trusty-chillet"?ItemID.PalworldMountTrustyChillet:
                        ItemID.PalworldMountTrustyChilletIgnis);
                    // Set 4's other two parts, in the same shapes set 1 already
                    // uses: the Bundle of Balloons in the accessory slot and
                    // featherfall through the native buff call so UpdateBuffs
                    // derives the fall behaviour every frame.
                    player.armor[3].SetDefaults(1164);
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: "+formulaRoute;
                }
                else
                {
                    player.armor[3].SetDefaults(ItemID.AmphibianBoots);
                    player.armor[4].SetDefaults(formulaRoute=="fishron-strong-wing"?
                        ItemID.FishronWings:ItemID.FairyWings);
                    // The declared route identity is frog-boots + wing + dash.
                    // A plain Frog Leg is accepted as the same source for real
                    // players; the fixture pins the amphibian-boots reading.
                    player.armor[5].SetDefaults(0);
                    player.armor[8].SetDefaults(ItemID.EoCShield);
                    // 2026-09-21 user ruling: the weak-wing set (Fairy Wings and
                    // its same-tier equivalents) carries featherfall, like every
                    // other non-broom loadout. Applied through the native buff
                    // call so UpdateBuffs derives the fall behaviour every frame.
                    player.AddBuff(BuffID.Featherfall,36000);
                    scenario.Equipment="Fishron formula fixture: "+
                        (formulaRoute??"fishron-fairy-wing");
                }
            }
        }
        else if(scenario.EquipmentTier=="pre-moon-lord")
        {
            player.armor[0].SetDefaults(ItemID.ShroomiteMask);
            player.armor[1].SetDefaults(ItemID.ShroomiteBreastplate);
            player.armor[2].SetDefaults(ItemID.ShroomiteLeggings);
            player.armor[3].SetDefaults(ItemID.FrostsparkBoots);
            player.armor[4].SetDefaults(ItemID.BeetleWings);
            player.armor[5].SetDefaults(ItemID.MasterNinjaGear);
            player.armor[6].SetDefaults(ItemID.CharmofMyths);
            player.armor[7].SetDefaults(ItemID.SniperScope);
            if(difficultyCode>0)
            {
                player.extraAccessory=true;
                player.armor[8].SetDefaults(ItemID.AnkhShield);
            }
            scenario.Equipment="pre-Moon-Lord shroomite-bullet+frostspark+beetle-wings+master-ninja+charm+sniper-scope+chain-gun+ichor-bullets; greater-healing x20";
        }
        else
        {
            player.armor[0].SetDefaults(ItemID.PlatinumHelmet);
            player.armor[1].SetDefaults(ItemID.PlatinumChainmail);
            player.armor[2].SetDefaults(ItemID.PlatinumGreaves);
            player.armor[3].SetDefaults(ItemID.HermesBoots);
            player.armor[4].SetDefaults(ItemID.CloudinaBottle);
            player.armor[5].SetDefaults(ItemID.BandofRegeneration);
            player.armor[6].SetDefaults(ItemID.Shackle);
            player.armor[7].SetDefaults(ItemID.Aglet);
            player.inventory[10].SetDefaults(ItemID.HealingPotion);
            player.inventory[10].stack=20;
            scenario.Equipment="pre-boss platinum+hermes+cloud+regen-band+shackle+aglet+minishark; healing x20";
        }
        if(!scenario.Legacy)
        {
            player.inventory[10].SetDefaults(healingType);
            player.inventory[10].stack=20;
        }
        if(IsMonitorFixture && scenario.Id=="duke-fishron")
        {
            // Reviewed bubble clearance for the formula fixture: the stock the
            // admission requires, so the run can refresh the ring in flight.
            player.inventory[11].SetDefaults(ItemID.InfernoPotion);
            player.inventory[11].stack=Chaite.Core.FishronThreatCatalog.RequiredInfernoPotionStock;
            scenario.Equipment+="; inferno-potion x"+Chaite.Core.FishronThreatCatalog.RequiredInfernoPotionStock;
        }
        if(scenario.EquipmentTier=="early-hardmode" ||
           scenario.EquipmentTier=="post-plantera" ||
           scenario.EquipmentTier=="pre-moon-lord")
        {
            // Standard pre-boss buffs reviewed by every common hardmode guide.
            // They are a preparation baseline, not a build-specific cheese.
            player.AddBuff(BuffID.Ironskin, 36000);
            player.AddBuff(BuffID.Regeneration, 36000);
            player.AddBuff(BuffID.Swiftness, 36000);
            player.AddBuff(BuffID.Endurance, 36000);
            player.AddBuff(BuffID.Lifeforce, 36000);
            player.AddBuff(BuffID.WellFed, 36000);
            player.AddBuff(BuffID.Wrath, 36000);
            player.AddBuff(BuffID.Rage, 36000);
        }
        var armor=new int[10];
        for(int i=0;i<armor.Length;i++) armor[i]=player.armor[i].type;
        equipmentReport=new Dictionary<string,object>
        {
            {"label",scenario.Equipment},{"tier",scenario.EquipmentTier},{"life",scenario.MaxLife},{"mana",200},{"armorAndAccessories",armor},
            {"formulaRoute",formulaRoute},
            {"weaponType",player.inventory[0].type},{"summonType",scenario.Summon},{"summonCount",scenario.Summon==0?0:1},
            {"ammoType",player.inventory[54].type},{"ammoCount",9999},
            {"weaponBallisticFields",new Dictionary<string,object>
                {
                    {"damage",player.inventory[0].damage},{"shootSpeed",player.inventory[0].shootSpeed},
                    {"useTime",player.inventory[0].useTime},{"useAnimation",player.inventory[0].useAnimation},
                    {"reuseDelay",player.inventory[0].reuseDelay},{"projectile",player.inventory[0].shoot},
                    {"prefix",player.inventory[0].prefix}
                }},
            {"ammoBallisticFields",new Dictionary<string,object>
                {
                    {"damage",player.inventory[54].damage},{"shootSpeed",player.inventory[54].shootSpeed},
                    {"projectile",player.inventory[54].shoot}
                }},
            {"healingType",player.inventory[10].type},{"healingCount",player.inventory[10].stack},
            {"grappleType",player.miscEquips[4].type},{"consumablesReplenished",false},
            {"legacyBaseline",scenario.Legacy}
        };
    }

    // A staged fight runs inside a real world, so natural spawns keep happening
    // around it. A stray enemy that touches the player adds a hit with nothing to
    // do with the script, and because a day Empress hit is lethal it ends the run
    // outright: two of nine day seeds died that way, one to type 75 and one to
    // type 1, while the observation channel's filtered npc list showed only the
    // Empress and could not reveal it. The scenarios that opt in stage a boss with
    // no adds, so anything that is not an expected root can be retired. The count
    // is recorded so a probe can prove this branch actually ran.
    static int strayNpcsRetired;

    static void RetireStrayNpcs()
    {
        if(scenario==null || !scenario.SuppressStrayNpcs) return;
        if(!booted || IsMotion || ticks<takeoverTick) return;
        if(Game.npc==null) return;
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc==null || !npc.active || IsExpectedRoot(npc)) continue;
            npc.active=false;
            strayNpcsRetired++;
        }
    }

    // Synthetic edges ONLY in test copy's poller. No OS keys or mouse are sent.
    public static bool ActivateDown()
    {
        // Episode mode re-arms the monitor with the same synthetic edge the
        // initial takeover uses, so the plugin's session restarts without a
        // process relaunch. Variant identity stays captured once, at the first
        // arm: the re-arms drive the same fixture.
        bool down=!IsMotion && booted && (ticks==takeoverTick ||
            (episodeArmTick>=0 && ticks==episodeArmTick));
        if(down)
        {
            actualTakeoverTick=ticks;
            if(IsScopeNegative) CaptureScopeNegativeActivation();
            if(episodeIndex==0 && ticks==takeoverTick) CaptureVariantAtActivation();
        }
        RetireStrayNpcs();
        return down;
    }
    public static bool CancelDown() { return false; }

    static NPC FirstActiveNpc(int type)
    {
        foreach(var npc in Game.npc) if(npc!=null && npc.active && npc.type==type) return npc;
        return null;
    }

    static void CaptureScopeNegativeActivation()
    {
        if(!IsScopeNegative || scopeNegativeActivationCaptured)
            throw new InvalidOperationException("Invalid repeated scope-negative activation capture");
        var p=Game.player[0];
        scopeNegativeActivationCaptured=true;
        scopeNegativeActivationTick=ticks;
        scopeNegativeSessionBefore=SessionState();
        scopeNegativePositionBefore=p.position;
        scopeNegativeSelectedBefore=p.selectedItem;
        scopeNegativeSummonBeforeType=p.inventory[1].type;
        scopeNegativeSummonBeforeStack=p.inventory[1].stack;
        scopeNegativeAmmoBeforeType=p.inventory[54].type;
        scopeNegativeAmmoBeforeStack=p.inventory[54].stack;
        for(int slot=0;slot<Game.npc.Length;slot++)
        {
            var npc=Game.npc[slot];
            if(npc==null || !npc.active || npc.life<=0 ||
                !Chaite.Core.SupportedBossPolicy.IsEncounterBossRoot(npc.type,npc.boss)) continue;
            int rootKey=npc.realLife>=0?npc.realLife:npc.whoAmI;
            scopeNegativeRootsAtActivation.Add(new Dictionary<string,object>
            {
                {"slot",slot},{"rootKey",rootKey},{"type",npc.type},{"active",npc.active},
                {"boss",npc.boss},{"life",npc.life},{"lifeMax",npc.lifeMax},{"realLife",npc.realLife}
            });
        }
        var remaining=new List<int>(scenario.ScopeNegativeRootTypes);
        bool identitiesValid=scopeNegativeRootsAtActivation.Count==remaining.Count;
        foreach(var root in scopeNegativeRootsAtActivation)
        {
            int type=(int)root["type"];
            int index=remaining.IndexOf(type);
            if(index<0 || (int)root["slot"]!=(int)root["rootKey"] || !(bool)root["boss"])
                identitiesValid=false;
            else remaining.RemoveAt(index);
        }
        scopeNegativeTopologyMatches=identitiesValid && remaining.Count==0;
        Log("SCOPE_NEGATIVE_ACTIVATION scenario="+scenario.Id+" tick="+ticks+
            " topologyMatches="+scopeNegativeTopologyMatches+" roots="+Json(scopeNegativeRootsAtActivation));
    }

    static void SpawnDirectEncounter()
    {
        if(!scenario.DirectSpawn || directSpawnAttempted) return;
        directSpawnAttempted=true;
        var player=Game.player[0];
        if(player==null || !player.active || player.dead) throw new InvalidOperationException("Direct Boss fixture player is unavailable");
        if(IsScopeNegative)
        {
            bool allCreated=true;
            for(int i=0;i<scenario.ScopeNegativeRootTypes.Length;i++)
            {
                int type=scenario.ScopeNegativeRootTypes[i];
                int index=NPC.NewNPC(NPC.GetBossSpawnSource(0),(int)player.Center.X+640+i*160,
                    (int)player.Center.Y-260,type,0,0f,0f,0f,0f,0);
                if(i==0) directSpawnRootIndex=index;
                var spawnedRoot=index>=0 && index<Game.npc.Length?Game.npc[index]:null;
                allCreated&=spawnedRoot!=null && spawnedRoot.active && spawnedRoot.type==type && spawnedRoot.whoAmI==index;
            }
            directSpawnCompleted=allCreated;
            Log("DIRECT_SCOPE_NEGATIVE_SPAWN scenario="+scenario.Id+" tick="+ticks+
                " completed="+directSpawnCompleted+" rootTypes="+Json(scenario.ScopeNegativeRootTypes));
            if(!directSpawnCompleted) throw new InvalidOperationException("Scope-negative spawn did not create every exact root");
            return;
        }
        if(scenario.Id=="wall-of-flesh")
        {
            NPC.SpawnWOF(player.Center);
            var wall=FirstActiveNpc(113);
            directSpawnRootIndex=wall==null?-1:wall.whoAmI;
        }
        else if(scenario.Id=="moon-lord")
        {
            NPC.SpawnOnPlayer(0,398);
            var core=FirstActiveNpc(398);
            directSpawnRootIndex=core==null?-1:core.whoAmI;
        }
        else
        {
            int spawnX=(int)player.Center.X+640;
            int spawnY=(int)player.Center.Y-260;
            directSpawnRootIndex=NPC.NewNPC(NPC.GetBossSpawnSource(0),spawnX,spawnY,
                scenario.DirectSpawnType,0,0f,0f,0f,0f,0);
        }
        var root=directSpawnRootIndex>=0 && directSpawnRootIndex<Game.npc.Length?Game.npc[directSpawnRootIndex]:null;
        directSpawnCompleted=root!=null && root.active && root.type==scenario.DirectSpawnType;
        Log("DIRECT_BOSS_SPAWN scenario="+scenario.Id+" requestedType="+scenario.DirectSpawnType+
            " tick="+ticks+" rootIndex="+directSpawnRootIndex+" completed="+directSpawnCompleted+
            " entry="+(scenario.Id=="wall-of-flesh"?"NPC.SpawnWOF":scenario.Id=="moon-lord"?"NPC.SpawnOnPlayer":"NPC.NewNPC"));
        if(!directSpawnCompleted) throw new InvalidOperationException("Reviewed direct Boss spawn did not create its expected root");
    }

    static void SetLifeFraction(NPC npc,int numerator,int denominator)
    {
        if(npc==null || !npc.active || npc.lifeMax<=0) throw new InvalidOperationException("Cannot stage life on a missing native Boss component");
        npc.life=Math.Max(1,npc.lifeMax*numerator/denominator);
    }

    static void SetAllExpectedLifeFraction(int numerator,int denominator)
    {
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active && IsExpectedRoot(npc)) SetLifeFraction(npc,numerator,denominator);
    }

    static void SetNativeAi(NPC npc,float ai0,float ai1,float ai2,float ai3)
    {
        if(npc==null || !npc.active) throw new InvalidOperationException("Cannot stage AI on a missing native Boss component");
        npc.ai[0]=ai0;npc.ai[1]=ai1;npc.ai[2]=ai2;npc.ai[3]=ai3;
        npc.target=0;npc.targetSetFrame=Game.EverLastingTicker;
    }

    static void StageRequestedPhase()
    {
        if(!scenario.DirectSpawn || phaseStageAttempted) return;
        phaseStageAttempted=true;
        var root=FirstActiveNpc(scenario.DirectSpawnType);
        if(root==null) throw new InvalidOperationException("Cannot stage phase before the native Boss root exists");
        var mutation=new Dictionary<string,object>();
        switch(scenario.Id)
        {
            case "deerclops": StageDeerclops(root,mutation); break;
            case "skeletron": StageSkeletron(root,mutation); break;
            case "queen-bee": StageQueenBee(root,mutation); break;
            case "wall-of-flesh": StageWall(root,mutation); break;
            case "duke-fishron": StageFishron(root,mutation); break;
            case "moon-lord": StageMoonLord(root,mutation); break;
            default: throw new InvalidOperationException("Direct Boss scenario lacks a reviewed phase stager");
        }
        RebaseExpectedBossDamageObservation();
        phaseStaged=true;
        phaseVerifiedAtTakeover=PhaseMatchesRequested();
        phaseStageReport=new Dictionary<string,object>
        {
            {"schema","chaite-priority-phase-stage/v1"},{"scenario",scenario.Id},{"phase",requestedPhase},
            {"tick",ticks},{"rootIndex",root.whoAmI},{"mutations",mutation},
            {"syntheticNativeFieldFixture",mutation.Count>0},{"verifiedBeforeActivation",phaseVerifiedAtTakeover},
            {"scope","test-copy NPC life/ai/localAI/position/velocity only; native AI resumes on the same frame; never a user world or an organic phase-transition claim"}
        };
        takeoverNativeSnapshot=CaptureNativeBossSnapshot();
        Log("PHASE_STAGE "+Json(phaseStageReport));
        if(!phaseVerifiedAtTakeover) throw new InvalidOperationException("Staged native Boss fields do not match the requested phase contract");
    }

    // Test-only native tuples. These values are source-contract tested by
    // test-priority-phase-fixture-contract.ps1 against vanilla 1.4.5.8 AI_043,
    // AI_069, AI_123, and Skeletron aiStyle 11/12. The first native update runs
    // after staging, so Deerclops jump lead-ins deliberately use firstTick - 1.
    static readonly Dictionary<string,int[]> DeerclopsStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"spawn-settle",new[]{-1,0}}, {"opening",new[]{0,0}},
        {"forward-spikes",new[]{1,19}}, {"rubble",new[]{2,21}},
        {"slow-roar",new[]{3,27}}, {"double-spikes",new[]{4,35}},
        {"shadow-hands",new[]{5,20}}, {"return-home",new[]{6,0}},
        {"teleport-home",new[]{7,30}}
    };

    // head ai[1], head ai[2], selected hand ai[2] (-1 means no selected
    // hand), selected hand ai[3]. Head ai[3] is always normal/non-Red-Hat 0.
    static readonly Dictionary<string,int[]> SkeletronStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"hover",new[]{0,600,-1,0}}, {"pre-spin",new[]{0,730,-1,0}},
        {"spin-imminent",new[]{0,790,-1,0}}, {"spin",new[]{1,0,-1,0}},
        {"spin-pursuit",new[]{1,200,-1,0}}, {"spin-exit",new[]{1,370,-1,0}},
        {"hand-vertical-imminent",new[]{0,200,0,270}},
        {"hand-vertical-locking",new[]{0,200,1,0}},
        {"hand-vertical",new[]{0,200,2,0}},
        {"hand-horizontal-imminent",new[]{0,200,3,270}},
        {"hand-horizontal-locking",new[]{0,200,4,0}},
        {"hand-horizontal",new[]{0,200,5,0}}
    };

    // root ai[0], ai[1], ai[2]. AI_043's chooser resets ai[1] before entering
    // state 2, and that branch targets player.Center.X / player.Y - 200 without
    // writing ai[1], so move-above must retain the exact zero clock.
    static readonly Dictionary<string,int[]> QueenBeeStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"choose",new[]{-1,0,0}}, {"charge-align",new[]{0,0,0}},
        {"charge",new[]{0,1,0}}, {"charge-brake",new[]{0,1,1}},
        {"bee-wave",new[]{1,0,0}}, {"move-above",new[]{2,0,0}},
        {"stinger",new[]{3,31,0}}, {"reacquire",new[]{4,0,0}}
    };

    // root ai[0], ai[2] attack clock, ai[3] sequence. ai[1] is reset to zero
    // exactly as each native transition does before the staged branch resumes.
    static readonly Dictionary<string,int[]> FishronStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"spawn-fade",new[]{-1,20,0}}, {"spawn-emerge",new[]{-1,60,0}},
        {"p1-hover",new[]{0,0,0}}, {"p1-dash",new[]{1,0,0}},
        {"p1-bubbles",new[]{2,0,1}}, {"p1-sharknado",new[]{3,50,0}},
        {"p2-transition-fade",new[]{4,60,0}}, {"p2-transition-emerge",new[]{4,140,0}},
        {"p2-hover",new[]{5,0,0}}, {"p2-dash",new[]{6,0,0}},
        {"p2-bubbles",new[]{7,0,1}}, {"p2-sharknado",new[]{8,50,0}},
        {"p3-transition-fade",new[]{9,60,0}}, {"p3-transition-hidden",new[]{9,140,0}},
        {"p3-reposition",new[]{10,0,1}}, {"p3-dash",new[]{11,0,0}},
        {"p3-teleport",new[]{12,10,1}}
    };

    static Player FixturePlayer()
    {
        var player=Game.player==null || Game.player.Length==0?null:Game.player[0];
        if(player==null || !player.active || player.dead)
            throw new InvalidOperationException("Native phase fixture player is unavailable");
        return player;
    }

    static Vector2 UnitToward(Vector2 from,Vector2 to)
    {
        var delta=to-from;
        if(delta.LengthSquared()<0.0001f) return Vector2.UnitX;
        delta.Normalize();
        return delta;
    }

    static bool Near(float actual,float expected,float tolerance)
    {
        return !Single.IsNaN(actual) && !Single.IsInfinity(actual) && Math.Abs(actual-expected)<=tolerance;
    }

    static bool VelocityAlong(NPC npc,Vector2 target,float speed,bool toward)
    {
        if(npc==null || !Near(npc.velocity.Length(),speed,0.05f)) return false;
        var line=target-npc.Center;
        if(line.LengthSquared()<1f) return false;
        float dot=Vector2.Dot(npc.velocity,line);
        return toward?dot>0.995f*speed*line.Length():dot< -0.995f*speed*line.Length();
    }

    static void RecordNpcMotion(Dictionary<string,object> mutation,string prefix,NPC npc)
    {
        mutation[prefix+".position"]=new Dictionary<string,object>{{"x",npc.position.X},{"y",npc.position.Y}};
        mutation[prefix+".velocity"]=new Dictionary<string,object>{{"x",npc.velocity.X},{"y",npc.velocity.Y}};
    }

    static void StageDeerclops(NPC root,Dictionary<string,object> mutation)
    {
        int[] tuple;
        if(!DeerclopsStageTuples.TryGetValue(requestedPhase,out tuple))
            throw new InvalidOperationException("Missing Deerclops native phase tuple");
        var player=FixturePlayer();
        root.ai[0]=tuple[0];root.ai[1]=tuple[1];root.target=0;root.targetSetFrame=Game.EverLastingTicker;
        if(tuple[0]==1 || tuple[0]==2)
        {
            int facing=root.direction==-1||root.direction==1?root.direction:Math.Sign(player.Center.X-root.Center.X);
            if(facing==0) facing=-1;
            root.direction=facing;root.spriteDirection=facing;
            root.Center=player.Center-new Vector2(facing*520f,0f);
        }
        else if(tuple[0]==6)
        {
            // AI_123 state 6 returns immediately when its target is still near
            // the Snow/home region. Keep the Boss, not the player, 2600 px away
            // so this synthetic frame really exercises return-home.
            root.Center=player.Center+new Vector2(2600f,0f);
            root.velocity=Vector2.Zero;
        }
        mutation["root.ai0"]=tuple[0];mutation["root.ai1"]=tuple[1];
        mutation["root.direction"]=root.direction;
        RecordNpcMotion(mutation,"root",root);
    }

    static float SkeletronHandSpeed(int state)
    {
        if(state==2) return difficultyCode>0?21f:18f;
        if(state==5) return difficultyCode>0?22f:17f;
        return 0f;
    }

    static void StageSkeletronHandGeometry(NPC root,NPC hand,int state)
    {
        var player=FixturePlayer();
        int side=(int)hand.ai[0];
        if(side!=-1 && side!=1) throw new InvalidOperationException("Native Skeletron hand lacks a valid side identity");
        if((int)hand.ai[1]!=root.whoAmI) throw new InvalidOperationException("Native Skeletron hand lacks its head parent identity");
        if(state==2)
        {
            hand.Center=player.Center+new Vector2(0f,-360f);
            hand.velocity=UnitToward(hand.Center,player.Center)*SkeletronHandSpeed(state);
        }
        else if(state==5)
        {
            hand.Center=player.Center+new Vector2(-side*420f,0f);
            hand.velocity=UnitToward(hand.Center,player.Center)*SkeletronHandSpeed(state);
        }
        else
        {
            hand.Center=root.Center+new Vector2(-200f*side,230f);
            hand.velocity=Vector2.Zero;
        }
    }

    static bool SkeletronHandGeometryMatches(NPC root,NPC hand,int state)
    {
        if(hand==null || (int)hand.ai[1]!=root.whoAmI || ((int)hand.ai[0]!=-1 && (int)hand.ai[0]!=1)) return false;
        var player=FixturePlayer();
        if(state==2)
            return hand.Center.Y<player.Center.Y-300f && VelocityAlong(hand,player.Center,SkeletronHandSpeed(state),true);
        if(state==5)
            return Math.Abs(hand.Center.X-player.Center.X)>360f && VelocityAlong(hand,player.Center,SkeletronHandSpeed(state),true);
        return Vector2.DistanceSquared(hand.Center,root.Center)<600f*600f && hand.velocity.LengthSquared()<0.01f;
    }

    static void StageSkeletron(NPC root,Dictionary<string,object> mutation)
    {
        int[] tuple;
        if(!SkeletronStageTuples.TryGetValue(requestedPhase,out tuple))
            throw new InvalidOperationException("Missing Skeletron native phase tuple");
        root.ai[1]=tuple[0];root.ai[2]=tuple[1];root.ai[3]=0f;
        root.target=0;root.targetSetFrame=Game.EverLastingTicker;
        mutation["head.ai1"]=tuple[0];mutation["head.ai2"]=tuple[1];mutation["head.ai3"]=0;
        if(tuple[2]>=0)
        {
            var hand=FirstActiveNpc(36);
            if(hand==null) throw new InvalidOperationException("Native Skeletron hands were not present for the requested hand phase");
            hand.ai[2]=tuple[2];hand.ai[3]=tuple[3];hand.target=0;hand.targetSetFrame=Game.EverLastingTicker;
            StageSkeletronHandGeometry(root,hand,tuple[2]);
            mutation["hand.index"]=hand.whoAmI;mutation["hand.ai0"]=hand.ai[0];mutation["hand.ai1"]=hand.ai[1];
            mutation["hand.ai2"]=tuple[2];mutation["hand.ai3"]=tuple[3];
            RecordNpcMotion(mutation,"hand",hand);
        }
    }

    static float QueenBeeNativeEnrage(NPC root,Player player)
    {
        float factor=0f;
        if(root.position.Y/16f<Game.worldSurface) factor+=1f;
        if(!player.ZoneJungle) factor+=1f;
        if(Game.getGoodWorld) factor+=0.5f;
        return factor;
    }

    static float QueenBeeChargeSpeed(NPC root,Player player)
    {
        float speed=difficultyCode>0?16f:12f;
        if(difficultyCode>0)
        {
            float life=root.lifeMax<=0?1f:(float)root.life/root.lifeMax;
            if(life<0.75f) speed+=2f;
            if(life<0.5f) speed+=2f;
            if(life<0.25f) speed+=2f;
            if(life<0.1f) speed+=2f;
        }
        return speed+7f*QueenBeeNativeEnrage(root,player);
    }

    static void StageQueenBeeGeometry(NPC root,int state,int sequence,int brake)
    {
        var player=FixturePlayer();
        if(state==0 && sequence==0)
        {
            root.Center=player.Center+new Vector2(520f,-260f);
            root.velocity=Vector2.Zero;
        }
        else if(state==0 && sequence==1 && brake==0)
        {
            root.Center=player.Center+new Vector2(-700f,0f);
            root.velocity=UnitToward(root.Center,player.Center)*QueenBeeChargeSpeed(root,player);
        }
        else if(state==0 && sequence==1 && brake==1)
        {
            root.Center=player.Center+new Vector2(700f,0f);
            root.velocity=Vector2.UnitX*QueenBeeChargeSpeed(root,player);
        }
        else if(state==2)
        {
            root.Center=player.Center+new Vector2(700f,200f);
            root.velocity=Vector2.Zero;
        }
        else if(state==4)
        {
            root.Center=player.Center+new Vector2(2400f,-200f);
            root.velocity=UnitToward(root.Center,player.Center)*14f;
        }
        if(root.velocity.X<0f) root.direction=root.spriteDirection=-1;
        else if(root.velocity.X>0f) root.direction=root.spriteDirection=1;
    }

    static bool QueenBeeGeometryMatches(NPC root,int state,int sequence,int brake)
    {
        var player=FixturePlayer();
        if(state==0 && sequence==0)
            return Math.Abs(root.Center.Y-player.Center.Y)>40f && root.velocity.LengthSquared()<0.01f;
        if(state==0 && sequence==1 && brake==0)
            return VelocityAlong(root,player.Center,QueenBeeChargeSpeed(root,player),true);
        if(state==0 && sequence==1 && brake==1)
            return VelocityAlong(root,player.Center,QueenBeeChargeSpeed(root,player),false);
        if(state==2)
        {
            var desired=player.Center+new Vector2(0f,-200f);
            return sequence==0 && root.velocity.LengthSquared()<0.01f &&
                Vector2.DistanceSquared(root.Center,desired)>200f*200f;
        }
        if(state==4)
        {
            float distance=Vector2.Distance(root.Center,player.Center);
            return distance>2000f && distance<3000f && VelocityAlong(root,player.Center,14f,true);
        }
        return true;
    }

    static void StageQueenBee(NPC root,Dictionary<string,object> mutation)
    {
        int[] tuple;
        if(!QueenBeeStageTuples.TryGetValue(requestedPhase,out tuple))
            throw new InvalidOperationException("Missing Queen Bee native phase tuple");
        SetNativeAi(root,tuple[0],tuple[1],tuple[2],0f);
        StageQueenBeeGeometry(root,tuple[0],tuple[1],tuple[2]);
        mutation["root.ai0"]=tuple[0];mutation["root.ai1"]=tuple[1];mutation["root.ai2"]=tuple[2];mutation["root.ai3"]=0;
        mutation["root.nativeEnrageFactor"]=QueenBeeNativeEnrage(root,FixturePlayer());
        RecordNpcMotion(mutation,"root",root);
    }

    static void StageWall(NPC root,Dictionary<string,object> mutation)
    {
        int percent=requestedPhase=="critical"?8:requestedPhase=="low-health"?20:
            requestedPhase=="accelerating"?40:80;
        SetAllExpectedLifeFraction(percent,100);
        mutation["expectedParts.lifePercent"]=percent;
        if(requestedPhase=="eye-laser")
        {
            bool found=false;
            foreach(var npc in Game.npc) if(npc!=null && npc.active && npc.type==114)
            {
                npc.localAI[1]=590f;npc.localAI[2]=0f;found=true;
            }
            if(!found) throw new InvalidOperationException("Native Wall of Flesh eyes were not present for the laser phase");
            mutation["eyes.localAi1"]=590;mutation["eyes.localAi2"]=0;
        }
    }

    static void StageFishron(NPC root,Dictionary<string,object> mutation)
    {
        int[] tuple;
        if(!FishronStageTuples.TryGetValue(requestedPhase,out tuple))
            throw new InvalidOperationException("Missing Duke Fishron native phase tuple");
        int state=tuple[0],clock=tuple[1],sequence=tuple[2];
        SetNativeAi(root,state,0f,clock,sequence);
        SetLifeFraction(root,state>=9?10:state>=4?40:80,100);
        StageFishronMotion(root,state);
        mutation["root.ai0"]=state;mutation["root.ai2"]=clock;mutation["root.ai3"]=sequence;
        mutation["root.lifePercent"]=state>=9?10:state>=4?40:80;
        RecordNpcMotion(mutation,"root",root);
    }

    static float FishronStageSpeed(int state)
    {
        if(state==1) return difficultyCode>0?17f:16f;
        if(state==6) return difficultyCode>0?21f:16f;
        if(state==7) return 20f;
        if(state==11) return 27f;
        return 0f;
    }

    static void StageFishronMotion(NPC root,int state)
    {
        float speed=FishronStageSpeed(state);
        if(speed<=0f) return;
        var player=FixturePlayer();
        root.Center=player.Center+new Vector2(-720f,state==7?0f:-80f);
        root.velocity=UnitToward(root.Center,player.Center)*speed;
        root.direction=root.velocity.X<0f?-1:1;
        root.spriteDirection=-root.direction;
    }

    static bool FishronMotionMatches(NPC root,int state)
    {
        float speed=FishronStageSpeed(state);
        return speed<=0f || VelocityAlong(root,FixturePlayer().Center,speed,true);
    }

    static void StageMoonLord(NPC root,Dictionary<string,object> mutation)
    {
        if(requestedPhase=="intro") return;
        var head=FirstActiveNpc(396);
        if(head==null || FirstActiveNpc(397)==null) throw new InvalidOperationException("Native Moon Lord head/hands were not present before phase staging");
        if(requestedPhase=="synchronize-eyes") return;
        if(requestedPhase.StartsWith("head-",StringComparison.Ordinal))
        {
            int clock=requestedPhase=="head-bolts"?120:requestedPhase=="head-tongue"?300:985;
            int state=requestedPhase=="head-bolts"?3:requestedPhase=="head-tongue"?2:1;
            head.ai[0]=state;head.ai[1]=clock;
            mutation["head.ai0"]=state;mutation["head.ai1"]=clock;
            return;
        }
        int side=requestedPhase=="left-sphere-release"?0:1;
        NPC selected=null;
        foreach(var npc in Game.npc) if(npc!=null && npc.active && npc.type==397 && (int)npc.ai[2]==side) { selected=npc;break; }
        if(selected==null) throw new InvalidOperationException("Native Moon Lord hand side was not present before sphere staging");
        selected.ai[0]=2f;selected.ai[1]=side==0?390f:540f;
        mutation[side==0?"leftHand.ai0":"rightHand.ai0"]=2;
        mutation[side==0?"leftHand.ai1":"rightHand.ai1"]=side==0?390:540;
    }

    static bool PhaseMatchesRequested()
    {
        var root=FirstActiveNpc(scenario.DirectSpawnType);
        if(root==null) return false;
        switch(scenario.Id)
        {
            case "deerclops":
                int[] deerTuple;
                if(!DeerclopsStageTuples.TryGetValue(requestedPhase,out deerTuple)) return false;
                if((int)root.ai[0]!=deerTuple[0] || (int)root.ai[1]!=deerTuple[1]) return false;
                if((deerTuple[0]==1 || deerTuple[0]==2) &&
                    (root.direction!=1 && root.direction!=-1 ||
                     (FixturePlayer().Center.X-root.Center.X)*root.direction<=0f)) return false;
                return deerTuple[0]!=6 || Vector2.Distance(root.Center,FixturePlayer().Center)>=2400f;
            case "skeletron":
                int[] skeletonTuple;
                if(!SkeletronStageTuples.TryGetValue(requestedPhase,out skeletonTuple) ||
                    (int)root.ai[1]!=skeletonTuple[0] || (int)root.ai[2]!=skeletonTuple[1] ||
                    (int)root.ai[3]!=0) return false;
                if(skeletonTuple[2]<0) return true;
                var hand=FirstActiveNpc(36);
                return hand!=null && (int)hand.ai[2]==skeletonTuple[2] &&
                    Near(hand.ai[3],skeletonTuple[3],0.001f) &&
                    SkeletronHandGeometryMatches(root,hand,skeletonTuple[2]);
            case "queen-bee":
                int[] queenTuple;
                return QueenBeeStageTuples.TryGetValue(requestedPhase,out queenTuple) &&
                    (int)root.ai[0]==queenTuple[0] && Near(root.ai[1],queenTuple[1],0.001f) &&
                    (int)root.ai[2]==queenTuple[2] && (int)root.ai[3]==0 &&
                    QueenBeeGeometryMatches(root,queenTuple[0],queenTuple[1],queenTuple[2]);
            case "wall-of-flesh":
                if(requestedPhase!="eye-laser") return root.lifeMax>0;
                var eye=FirstActiveNpc(114);return eye!=null && (int)eye.localAI[1]==590 && (int)eye.localAI[2]==0;
            case "duke-fishron":
                int[] fishTuple;
                return FishronStageTuples.TryGetValue(requestedPhase,out fishTuple) &&
                    (int)root.ai[0]==fishTuple[0] && Near(root.ai[1],0f,0.001f) &&
                    (int)root.ai[2]==fishTuple[1] && (int)root.ai[3]==fishTuple[2] &&
                    FishronMotionMatches(root,fishTuple[0]);
            case "moon-lord":
                if(requestedPhase=="intro") return (int)root.ai[0]==-1;
                var moonHead=FirstActiveNpc(396);if(moonHead==null || FirstActiveNpc(397)==null) return false;
                if(requestedPhase=="synchronize-eyes") return true;
                if(requestedPhase.StartsWith("head-",StringComparison.Ordinal))
                    return (int)moonHead.ai[0]==(requestedPhase=="head-bolts"?3:requestedPhase=="head-tongue"?2:1);
                int wantedSide=requestedPhase=="left-sphere-release"?0:1;
                foreach(var npc in Game.npc) if(npc!=null && npc.active && npc.type==397 && (int)npc.ai[2]==wantedSide && (int)npc.ai[0]==2) return true;
                return false;
        }
        return false;
    }

    static Dictionary<string,object> CaptureNativeBossSnapshot()
    {
        var rows=new List<Dictionary<string,object>>();
        foreach(var npc in Game.npc) if(npc!=null && npc.active && (IsExpectedRoot(npc) || npc.type==36))
            rows.Add(new Dictionary<string,object>
            {
                {"index",npc.whoAmI},{"type",npc.type},{"life",npc.life},{"lifeMax",npc.lifeMax},
                {"position",new Dictionary<string,object>{{"x",npc.position.X},{"y",npc.position.Y}}},
                {"velocity",new Dictionary<string,object>{{"x",npc.velocity.X},{"y",npc.velocity.Y}}},
                {"ai",new[]{npc.ai[0],npc.ai[1],npc.ai[2],npc.ai[3]}},
                {"localAi",new[]{npc.localAI[0],npc.localAI[1],npc.localAI[2],npc.localAI[3]}},
                {"target",npc.target},{"dontTakeDamage",npc.dontTakeDamage}
            });
        return new Dictionary<string,object>
        {
            {"schema","chaite-takeover-native-snapshot/v1"},{"tick",ticks},{"requestedTakeoverTick",takeoverTick},
            {"scenario",scenario.Id},{"requestedPhase",requestedPhase},{"bosses",rows.ToArray()}
        };
    }

    public static void BeforeUpdate()
    {
        if (failed) return;
        try
        {
            if (!booted) return;
            ticks++;
            // The pre-update projectile field, captured at the only moment it
            // exists: after the previous tick finished and before this one's
            // native update can kill a projectile for landing a hit. Only in
            // dense mode, because only a per-tick trace can use it.
            if(denseFramesRequested && IsBattleObservation &&
                Game.player!=null && Game.player.Length!=0)
            {
                battleProjectilesBeforeUpdate=new List<Dictionary<string,object>>();
                CaptureHostileProjectiles(battleProjectilesBeforeUpdate,
                    out battleProjectileCountBeforeUpdate,
                    out battleOmittedProjectilesBeforeUpdate);
            }
            else battleProjectilesBeforeUpdate=null;
            if(scenario.DirectSpawn && ticks==directSpawnTick) SpawnDirectEncounter();
            if(scenario.DirectSpawn && !IsScopeNegative && !IsMonitorFixture && ticks==takeoverTick) StageRequestedPhase();
            Game.screenPosition=Game.player[0].Center-new Vector2(Game.screenWidth/2f,Game.screenHeight/2f);
            Game.autoSave = false;
            Game.SettingPlayWhenUnfocused = true;
            if(IsMotion)
            {
                if(ticks>(IsFlight?FlightTotalFrames:MotionTotalFrames) || clock.Elapsed.TotalSeconds>=wallLimitSeconds)
                    throw new TimeoutException("Bounded native motion fixture exceeded its time limit");
                motionPlayerCalls=motionJumpCalls=motionInputReplays=0;
                flightWingCalls=0;
                motionFrame=new Dictionary<string,object>
                {
                    {"schema",IsFlight?"chaite-native-flight-frame/v1":"chaite-native-motion-frame/v1"},{"tick",ticks},{"nativeFrameBefore",nativeFrames},
                    {"requestedJump",MotionJumpRequested()},{"phase",MotionPhase()}
                };
                if(IsFlight)
                {
                    motionFrame["requestedUp"]=FlightUpRequested();
                    motionFrame["requestedDown"]=FlightDownRequested();
                    motionFrame["preWing"]=null; motionFrame["postWing"]=null;
                }
                ReplayMotionControls(Game.player[0]);
                return;
            }
            if (ticks % 60 == 0)
            {
                var p = Game.player[0];
                var selectedWeapon=p.selectedItem>=0 && p.selectedItem<p.inventory.Length?p.inventory[p.selectedItem]:null;
                int bossCount=0, bossLife=0, shots=0;
                foreach(var npc in Game.npc) if(npc != null && npc.active && npc.boss) { bossCount++; bossLife+=npc.life; }
                foreach(var shot in Game.projectile) if(shot != null && shot.active && shot.friendly && shot.owner==0) shots++;
                string state = SessionState();
                Log("FRAME tick="+ticks+" elapsedMs="+clock.ElapsedMilliseconds+" state="+state+" life="+p.statLife+" dead="+p.dead+
                    " pos="+p.position+" vel="+p.velocity+" maxRun="+p.maxRunSpeed+" accRun="+p.accRunSpeed+
                    " slow="+p.slow+" moveDebuff="+p.strongestMoveSpeedDebuff+
                    " bosses="+bossCount+" bossLife="+bossLife+" shots="+shots+" ammo="+p.inventory[54].stack+" summon="+p.inventory[1].stack+
                    " selected="+p.selectedItem+" armor="+p.armor[3].type+" defense="+p.statDefense+" day="+Game.dayTime+" menu="+Game.gameMenu+
                    " grappling="+p.grapCount+" hook="+p.controlHook+" releaseJump="+p.releaseJump+
                    " wingTime="+p.wingTime+" wingMax="+p.wingTimeMax+" wings="+p.wings+" wingsLogic="+p.wingsLogic+
                     " itemAnimation="+p.itemAnimation+" itemAnimationMax="+p.itemAnimationMax+" itemTime="+p.itemTime+" releaseUse="+p.releaseUseItem+
                     " reuseDelay="+p.reuseDelay+" usingOrReusing="+p.UsingOrReusingItem+" itemTimeZero="+p.ItemTimeIsZero+
                     " selectionCanChange="+p.selectedItemState.CanChangeSelectedItemImmediately+
                     " selectionHotbar="+p.selectedItemState.Hotbar+" selectionBuffered="+p.selectedItemState.HasBufferedChange+
                     " selectionLast="+p.selectedItemState.LastNonOverridenSelection+
                     " weapon="+(selectedWeapon==null?0:selectedWeapon.type)+" useTime="+(selectedWeapon==null?0:selectedWeapon.useTime)+
                    " useAnimation="+(selectedWeapon==null?0:selectedWeapon.useAnimation)+" autoReuse="+(selectedWeapon!=null && selectedWeapon.autoReuse)+
                    " channel="+(selectedWeapon!=null && selectedWeapon.channel)+
                    " controls="+p.controlLeft+","+p.controlRight+","+p.controlJump+","+p.controlUseItem);
                if(state != lastSession) { Log("STATE "+state); lastSession=state; }
            }
            if((episodeLimit>0 ? ticks-episodeStartTick : ticks) >= tickLimit ||
                (runTickLimit>0 && ticks >= runTickLimit) ||
                clock.Elapsed.TotalSeconds >= wallLimitSeconds) Finish("test-time-limit");
        }
        catch(Exception e) { Fail(e); }
    }

    public static void BeforeShieldDash(Player player)
    {
        if (!booted || !IsMonitorFixture || player.whoAmI != 0) return;
        shieldBeforeHit=player.eocHit;
        shieldBeforeDelay=player.dashDelay;
        shieldBeforeLife=player.statLife;
        shieldBeforeRequested=player.controlDash;
    }

    public static void AfterShieldDash(Player player)
    {
        if (!booted || !IsMonitorFixture || player.whoAmI != 0 || player.dashType != 2) return;
        var contact=player.eocHit>=0 && player.eocHit!=shieldBeforeHit;
        var started=shieldBeforeDelay==0 && player.dashDelay==-1;
        if (!contact && !started && !shieldBeforeRequested) return;
        if (shieldTraceRows >= 1024) { shieldTraceDropped++; return; }
        var npc=contact && player.eocHit<Game.npc.Length ? Game.npc[player.eocHit] : null;
        pendingShieldTrace=new Dictionary<string,object>
        {
            {"tick",ticks},{"requested",shieldBeforeRequested},{"started",started},{"contact",contact},
            {"npcSlot",contact?player.eocHit:-1},{"npcType",npc==null?0:npc.type},
            {"npcActive",npc!=null && npc.active},{"eocDash",player.eocDash},
            {"dashDelay",player.dashDelay},{"immuneTime",player.immuneTime},
            {"hurtCooldowns",(int[])player.hurtCooldowns.Clone()},
            {"vx",player.velocity.X},{"vy",player.velocity.Y},
            {"lifeBefore",shieldBeforeLife},{"lifeAfterDash",player.statLife}
        };
        shieldTraceRows++;
    }

    /// <summary>Simulated player output.
    ///
    /// The takeover is a movement claim: the policy flies the player and nothing
    /// else, so weapon use, aiming and firing are out of scope and the boss's
    /// health is driven here instead of by the policy. Each episode draws a DPS
    /// in [Min,Max] from (seed, episode) -- deterministic, so an episode is
    /// reproducible -- and drains it straight off the boss's life. No projectiles
    /// are simulated: the point is a realistic kill clock, not ballistics.
    ///
    /// The band was 300-1000 and is now 600-1200 by the user's call. Measured,
    /// only 60-97% of the nominal DPS actually lands (the drain needs an active
    /// boss, so the spawn and phase transitions do not count), which at 300 DPS
    /// meant a 17333 tick kill -- a 4.3 minute no-hit run for the slowest draw,
    /// the single hardest thing in the whole training set. 600-1200 puts the
    /// longest fight near 9500 ticks while still varying the kill clock enough
    /// that a policy cannot memorise one.
    ///
    /// The one threat that has to be modelled by hand is Duke Fishron's
    /// Detonating Bubble (NPC 371). A real player shoots those out of the air,
    /// and it is the only projectile in these fights that is trivially broken,
    /// so each one is destroyed with a high per-tick probability rather than
    /// being left as a threat the movement policy cannot answer. The Sharknado
    /// bubbles and column (372/373/384) stay real threats.</summary>
    const float SimulatedDpsMin = 600f;
    const float SimulatedDpsMax = 1200f;
    const double DetonatingBubbleBreakChance = 0.35;
    const int DetonatingBubbleType = 371;
    static float simulatedDps;
    static int simulatedDpsEpisode = -1;
    static double simulatedDamageCarry;
    static System.Random simulatedOutputRandom;
    static float simulatedDpsOverride;
    static readonly System.Collections.Generic.HashSet<int> reportedKilledBossSlots=new System.Collections.Generic.HashSet<int>();
    static int simulatedBubbleBreaks, simulatedDamageApplied;
    static int simulatedDamageEpisodeStart, simulatedBubbleEpisodeStart;

    static void ApplySimulatedPlayerOutput()
    {
        if(episodeLimit<=0) return;
        if(simulatedDpsEpisode!=episodeIndex)
        {
            simulatedDpsEpisode=episodeIndex;
            // System.Random is seeded from the run seed and the episode, so the
            // same (seed, episode) always produces the same DPS and the same
            // bubble rolls: acceptance has to be able to replay this exactly.
            simulatedOutputRandom=new System.Random(unchecked(seed*7919+episodeIndex*104729+17));
            simulatedDps=simulatedDpsOverride>0f?simulatedDpsOverride:
                SimulatedDpsMin+(float)simulatedOutputRandom.NextDouble()*(SimulatedDpsMax-SimulatedDpsMin);
            simulatedDamageCarry=0d;
            Log("SIM_OUTPUT episode="+episodeIndex+" dps="+simulatedDps.ToString("F1",CultureInfo.InvariantCulture));
        }
        if(simulatedOutputRandom==null) return;
        // Terraria's native tick is 60 Hz, so DPS/60 per tick, with the
        // fractional part carried so the total dealt matches the DPS exactly.
        simulatedDamageCarry+=simulatedDps/60d;
        int whole=(int)simulatedDamageCarry;
        simulatedDamageCarry-=whole;
        if(whole>0)
        {
            for(int i=0;i<Game.npc.Length;i++)
            {
                var npc=Game.npc[i];
                if(npc==null || !npc.active || !IsExpectedRoot(npc)) continue;
                int applied=Math.Min(whole,Math.Max(0,npc.life));
                if(applied<=0) continue;
                npc.life-=applied;
                simulatedDamageApplied+=applied;
            }
        }
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc==null || !npc.active || npc.type!=DetonatingBubbleType) continue;
            if(simulatedOutputRandom.NextDouble()>=DetonatingBubbleBreakChance) continue;
            npc.life=0;
            simulatedBubbleBreaks++;
        }
    }

    /// <summary>Report a dead Boss to the plugin.
    ///
    /// Runtime.OnNpcKilled is the plugin's only writer of PendingKilledBosses,
    /// and the observation's KilledBossKeys come from DrainKilledBosses, so the
    /// encounter can only reach SuccessNoDeath if this call happens. Nothing in
    /// the probe, the patcher or the plugin ever called it -- it was dead code --
    /// which is why a Boss whose life reached 0 was reported as
    /// "Unsupported Boss rejected: no verifiable active Boss root" and the
    /// session ended Cancelled instead of won. Measured on an isolated probe with
    /// a forced 20000 DPS: bossDamage 77667 (its whole bar), bossLife=0,
    /// FINISH Cancelled, win=false.
    ///
    /// A despawn is not a kill: the episode reset deactivates the Boss without
    /// dropping its life, so requiring life&lt;=0 separates a real kill from the
    /// harness's own teardown. Each slot is reported once per activation.</summary>
    static void ReportBossKills()
    {
        if(Game.npc==null) return;
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc==null || npc.life>0) continue;
            if(!IsExpectedRoot(npc)) continue;
            if(!reportedKilledBossSlots.Add(npc.whoAmI)) continue;
            Log("BOSS_KILL_REPORT slot="+npc.whoAmI+" type="+npc.type+" life="+npc.life+" tick="+ticks);
            try { Chaite.Plugin.Runtime.OnNpcKilled(npc); }
            catch(Exception ex) { Log("BOSS_KILL_REPORT failed: "+ex.Message); }
        }
    }

    static void AfterNativeUpdate()
    {
        var p=Game.player[0];
        if(playerReturnedTick!=ticks) throw new InvalidOperationException("Native Player.Update did not return at tick "+ticks);
        nativeFrames++;
        if (pendingShieldTrace != null)
        {
            pendingShieldTrace["lifeAfterFrame"]=p.statLife;
            File.AppendAllText(Path.Combine(Root,"shield-events.jsonl"),Json(pendingShieldTrace)+Environment.NewLine,new UTF8Encoding(false));
            pendingShieldTrace=null;
        }
        if (IsMonitorFixture)
        {
            var monitorState = SessionState();
            if (monitorState == "Monitoring")
            {
                if (monitorArmedTick < 0) monitorArmedTick = ticks;
                monitorPassiveFrames++;
                var applied = typeof(Chaite.Plugin.Runtime).GetField("_frameApplied", BindingFlags.NonPublic | BindingFlags.Static);
                if ((bool)applied.GetValue(null))
                    throw new InvalidOperationException("Monitor replayed player controls at tick " + ticks);
            }
            if (monitorCombatTick < 0 && monitorState == "EngagedAlive")
            {
                monitorCombatTick = ticks;
                Log("MONITOR_TAKEOVER armed=" + monitorArmedTick + " spawn=" + directSpawnTick + " combat=" + ticks + " passiveFrames=" + monitorPassiveFrames);
            }
        }
        if(IsScopeNegative)
        {
            AfterScopeNegativeUpdate(p);
            return;
        }
        if(IsFlight)
        {
            AfterFlightUpdate(p);
            return;
        }
        if(IsMotion)
        {
            AfterMotionUpdate(p);
            return;
        }
        bool lostLifeThisFrame=p.statLife<lastLife;
        if(lostLifeThisFrame) { hits++; episodeHits++; }
        // The simulated player's output lands before the boss bookkeeping below
        // reads native life, so the drain is counted as damage and the boss's
        // death flows through the ordinary state machine as a real kill.
        ApplySimulatedPlayerOutput();
        // Report the kill in the same frame the drain drops the Boss to zero.
        // Runtime.Tick runs at Player.Update entry and its live-scope check
        // rejects the session as soon as no active Boss root is left, so a
        // report filed any later loses the race: measured with a forced
        // 20000 DPS, reporting at the top of this method still ended
        // "FINISH Cancelled win=false" while the same kill reported here
        // completes the encounter.
        ReportBossKills();
        // Ends the probe at the first hit when asked. Finish is idempotent and
        // Exits the process, so a later frame cannot restart anything, and the
        // status this produces is the same "loss" a full-length doomed run
        // reports -- only the tick count differs, which is exactly the field a
        // no-hit run must not be judged on.
        if(stopOnFirstHit && lostLifeThisFrame && IsMonitorFixture) Finish("stop-on-hit");
        if(p.poisoned)
        {
            poisonedFrames++;
            if(lostLifeThisFrame) lifeLossFramesWhilePoisoned++;
        }
        lastLife=p.statLife;
        minLife=Math.Min(minLife,Math.Max(0,p.statLife));
        grappleTicks=p.grapCount>0?grappleTicks+1:0;
        maximumGrappleTicks=Math.Max(maximumGrappleTicks,grappleTicks);
        SampleRuntimeTiming();
        sawMovement |= Vector2.DistanceSquared(initialPosition,p.position)>64;
        if(scenario.Summon>0)
            sawSummonConsumed |= p.inventory[1].type!=scenario.Summon || p.inventory[1].stack<1;
         int bossLife=0, shots=0, nativePhaseKey=17;
         unchecked
         {
             nativePhaseKey=nativePhaseKey*31+p.selectedItem;
             nativePhaseKey=nativePhaseKey*31+(p.selectedItemState.HasBufferedChange?1:0);
             nativePhaseKey=nativePhaseKey*31+(p.selectedItemState.CanChangeSelectedItemImmediately?1:0);
         }
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active)
            {
                bool expected=false;
                for(int i=0;i<scenario.BossTypes.Length;i++)
                    if(npc.type==scenario.BossTypes[i]) { expectedBossMask|=1<<i; expected=true; }
                if(npc.boss)
                {
                    sawBoss=true; bossLife+=npc.life;
                    if(!expected) unexpectedBossTypes.Add(npc.type);
                }
                if(!expected) continue;
                nativePhaseKey=BattleNativePhaseKey(nativePhaseKey,npc);
                if(firstObservedBossTypes.Add(npc.type))
                {
                    var observed=new Dictionary<string,object>
                    {
                        {"type",npc.type},{"key",npc.whoAmI},{"tick",ticks},{"life",npc.life},
                        {"lifeMax",npc.lifeMax},{"damage",npc.damage},{"defense",npc.defense},{"bossFlag",npc.boss},
                        {"gameMode",Game.GameMode},{"difficulty",Game.Difficulty},{"npcDifficulty",npc.difficulty},
                        {"expertMode",Game.expertMode},{"masterMode",Game.masterMode}
                    };
                    firstObservedBosses.Add(observed);
                    Log("NATIVE_EXPECTED_FIRST "+Json(observed));
                    if(npc.lifeMax<=0 || npc.difficulty!=Game.Difficulty)
                        throw new InvalidOperationException("Native spawned expected NPC difficulty/lifeMax mismatch: "+Json(observed));
                }
            }
        CaptureActiveExpectedRootLife();
        TrackExpectedBossDamage();
        maximumBossLife=Math.Max(maximumBossLife,bossLife);
        if(bossDamage>0) sawBossDamage=true;
        foreach(var shot in Game.projectile) if(shot!=null && shot.active && shot.friendly && shot.owner==0) shots++;
        maximumShots=Math.Max(maximumShots,shots);
        ObserveBattleAfterNative(nativePhaseKey);
        // Normalised before the observation is written, because the terminal
        // observation row is what the trainer reads its `win` flag from -- the
        // episode summary alone would not reach the reward.
        string state=NormalizeTerminalState(SessionState());
        // The bridge observation is the trainer's rollout: one compact line
        // per native tick, written before the terminal check so the final
        // tick of an episode carries its done flag.
        if(bridgeBase!=null) WriteBridgeObservation(p, state);
        if(state=="Faulted") throw new InvalidOperationException("Production automation entered fail-closed; inspect Chaite log");
        if(state=="SuccessNoDeath" || state=="SuccessAfterDeath" || state=="FailedAfterDeath" || state=="EncounterInterrupted" || state=="Cancelled") Finish(state);
        // The activation-ended contract is about a first activation that never
        // produced an encounter. Between an episode reset and its re-arm the
        // production session is legitimately Idle, and that gap must not be
        // mistaken for a rejected activation.
        if(ticks>=240 && (state=="RejectedNoEncounter" || state=="Idle") &&
            episodeArmTick<0) Finish("activation-ended");
    }

    static void AfterScopeNegativeUpdate(Player p)
    {
        if(!scopeNegativeActivationCaptured) return;
        bool actionable=p.controlLeft || p.controlRight || p.controlUp || p.controlDown || p.controlJump ||
            p.controlHook || p.controlDash || p.controlMount || p.controlUseItem || p.controlUseTile ||
            p.controlThrow || p.controlQuickHeal || p.controlQuickMana;
        if(actionable) scopeNegativeActionableControlFrames++;
        if(p.controlLeft) scopeNegativeLeftFrames++;
        if(p.controlRight) scopeNegativeRightFrames++;
        if(p.controlUp) scopeNegativeUpFrames++;
        if(p.controlDown) scopeNegativeDownFrames++;
        if(p.controlJump) scopeNegativeJumpFrames++;
        if(p.controlHook) scopeNegativeHookFrames++;
        if(p.controlDash) scopeNegativeDashFrames++;
        if(p.controlMount) scopeNegativeMountFrames++;
        if(p.controlUseItem) scopeNegativeUseItemFrames++;
        if(p.controlUseTile) scopeNegativeUseTileFrames++;
        if(p.controlThrow) scopeNegativeThrowFrames++;
        if(p.controlQuickHeal) scopeNegativeQuickHealFrames++;
        if(p.controlQuickMana) scopeNegativeQuickManaFrames++;
        if(p.selectedItem!=scopeNegativeSelectedBefore) scopeNegativeSelectedChangeFrames++;
        if(p.inventory[1].type!=scopeNegativeSummonBeforeType || p.inventory[1].stack!=scopeNegativeSummonBeforeStack)
            scopeNegativeSummonChangeFrames++;
        if(p.inventory[54].type!=scopeNegativeAmmoBeforeType || p.inventory[54].stack!=scopeNegativeAmmoBeforeStack)
            scopeNegativeAmmoChangeFrames++;
        if(p.itemTime>0 || p.itemAnimation>0 || p.UsingOrReusingItem) scopeNegativeItemUseFrames++;
        if(p.statLife<lastLife) scopeNegativeLifeLossFrames++;
        lastLife=p.statLife;
        scopeNegativeMaximumPositionDeltaSquared=Math.Max(scopeNegativeMaximumPositionDeltaSquared,
            Vector2.DistanceSquared(scopeNegativePositionBefore,p.position));
        foreach(var projectile in Game.projectile)
            if(projectile!=null && projectile.active && projectile.friendly && projectile.owner==0)
                scopeNegativeOwnedFriendlyProjectileObservations++;
        scopeNegativeSessionAfter=SessionState();
        if(scopeNegativeSessionAfter=="Faulted")
            throw new InvalidOperationException("Production automation faulted during scope-negative rejection");
        if(scopeNegativeSessionAfter!="Idle" && scopeNegativeSessionAfter!="RejectedNoEncounter")
            scopeNegativeSessionUnexpectedFrames++;
        if(ticks-scopeNegativeActivationTick+1>=ScopeNegativeObservationFrames)
            FinishScopeNegative("UnsupportedBoss-rejected");
    }

    static bool MotionJumpRequested()
    {
        if(IsFlight) return FlightJumpRequested();
        if(ticks<=MotionWarmupFrames || ticks>80) return false;
        if(motionCase.EndsWith("-multijump",StringComparison.Ordinal))
        {
            // Four separated one-tick presses. The wolf cannot double jump, so a
            // second upward impulse can only come from an extra-jump accessory.
            // Fifteen ticks between presses lets each arc settle far enough that
            // an impulse can be attributed to the press that caused it.
            return ticks==21||ticks==36||ticks==51||ticks==66;
        }
        if(motionCase.EndsWith("-tap",StringComparison.Ordinal)) return ticks==21;
        if(motionCase.EndsWith("-release-press",StringComparison.Ordinal)) return ticks!=26;
        return true;
    }

    static string MotionPhase()
    {
        if(IsFlight) return FlightPhase();
        if(ticks<=MotionWarmupFrames) return "warmup-release";
        if(ticks>80) return "final-release-and-land";
        if(motionCase.EndsWith("-multijump",StringComparison.Ordinal))
        {
            if(ticks==21) return "multijump-press-1";
            if(ticks==36) return "multijump-press-2";
            if(ticks==51) return "multijump-press-3";
            if(ticks==66) return "multijump-press-4";
            return "multijump-between";
        }
        if(ticks==21) return "ground-initial-press";
        if(motionCase.EndsWith("-tap",StringComparison.Ordinal)) return "released-after-one-frame-tap";
        if(motionCase.EndsWith("-release-press",StringComparison.Ordinal))
            return ticks==26?"airborne-release":ticks==27?"airborne-repress":"hold";
        return "hold";
    }

    static void ReplayMotionControls(Player player)
    {
        // The native ResetControls helper is PRIVATE in the original reference
        // assembly. Write only its public input fields; never jump/release state.
        player.controlLeft=player.controlRight=player.controlUp=player.controlDown=false;
        player.controlUseItem=player.controlUseTile=player.controlThrow=player.controlInv=false;
        player.controlHook=player.controlTorch=player.controlSmart=player.controlMount=false;
        player.controlQuickHeal=player.controlQuickMana=player.controlCreativeMenu=false;
        player.controlDash=player.controlArmorSetAbility=false;
        player.controlJump=MotionJumpRequested();
        if(IsFlight)
        {
            player.controlUp=FlightUpRequested();
            player.controlDown=FlightDownRequested();
        }
    }

    // These observer/replay hooks exist only in the isolated test copy. All are
    // strict no-ops in every existing Boss scenario, including timing counters.
    public static void MotionBeforePlayerUpdate(Player player)
    {
        if(!IsMotion || motionFrame==null || player.whoAmI!=0) return;
        if(++motionPlayerCalls!=1) throw new InvalidOperationException("Multiple Player.Update calls in one motion frame");
        motionFrame["prePlayer"]=MotionSnapshot(player);
    }

    public static void MotionAfterInput(Player player)
    {
        if(!IsMotion || motionFrame==null || player.whoAmI!=0) return;
        ReplayMotionControls(player);
        motionInputReplays++;
        motionFrame["afterInputReplay"]=MotionSnapshot(player);
    }

    public static void MotionBeforeJump(Player player)
    {
        if(!IsMotion || motionFrame==null || player.whoAmI!=0) return;
        if(++motionJumpCalls!=1) throw new InvalidOperationException("Multiple JumpMovement calls in one motion frame");
        // This is AFTER native equipment/UpdateJumpHeight and BEFORE the real
        // JumpMovement body. Static Player.jumpSpeed/jumpHeight are frame-correct.
        motionFrame["preJump"]=MotionSnapshot(player);
        if(player.controlJump!=MotionJumpRequested() || IsFlight &&
            (player.controlUp!=FlightUpRequested() || player.controlDown!=FlightDownRequested()))
            throw new InvalidOperationException("Native motion control replay was lost before JumpMovement");
    }

    public static void MotionAfterJump(Player player)
    {
        if(!IsMotion || motionFrame==null || player.whoAmI!=0) return;
        if(motionFrame.ContainsKey("postJump")) throw new InvalidOperationException("Multiple JumpMovement returns in one motion frame");
        motionFrame["postJump"]=MotionSnapshot(player);
    }

    public static void FlightBeforeWing(Player player)
    {
        if(!IsFlight || motionFrame==null || player.whoAmI!=0) return;
        if(++flightWingCalls!=1) throw new InvalidOperationException("Multiple WingMovement calls in one flight frame");
        motionFrame["preWing"]=MotionSnapshot(player);
    }

    public static void FlightAfterWing(Player player)
    {
        if(!IsFlight || motionFrame==null || player.whoAmI!=0) return;
        if(motionFrame["postWing"]!=null) throw new InvalidOperationException("Multiple WingMovement returns in one flight frame");
        motionFrame["postWing"]=MotionSnapshot(player);
    }

    static Dictionary<string,object> MotionSnapshot(Player p)
    {
        var snapshot=new Dictionary<string,object>
        {
            {"tick",ticks},{"gameUpdateCount",Game.GameUpdateCount},{"nativeFramesCompleted",nativeFrames},
            {"position",new Dictionary<string,object>{{"x",p.position.X},{"y",p.position.Y}}},
            {"velocity",new Dictionary<string,object>{{"x",p.velocity.X},{"y",p.velocity.Y}}},
            {"width",p.width},{"height",p.height},{"bottomY",p.Bottom.Y},
            {"jump",p.jump},{"releaseJump",p.releaseJump},{"canJumpAgain_Cloud",p.canJumpAgain_Cloud},
            {"hasJumpOption_Cloud",p.hasJumpOption_Cloud},{"isPerformingJump_Cloud",p.isPerformingJump_Cloud},
            {"jumpSpeed",Player.jumpSpeed},{"jumpHeight",Player.jumpHeight},{"jumpSpeedBoost",p.jumpSpeedBoost},
            {"gravity",p.gravity},{"maxFallSpeed",p.maxFallSpeed},{"gravDir",p.gravDir},
            {"maxRunSpeed",p.maxRunSpeed},{"accRunSpeed",p.accRunSpeed},
            {"autoJump",p.autoJump},{"justJumped",p.justJumped},{"life",p.statLife},{"dead",p.dead},
            {"grapCount",p.grapCount},{"wingTime",p.wingTime},{"wingTimeMax",p.wingTimeMax},
            {"controls",new Dictionary<string,object>
                {
                    {"left",p.controlLeft},{"right",p.controlRight},{"up",p.controlUp},{"down",p.controlDown},
                    {"jump",p.controlJump},{"useItem",p.controlUseItem},{"useTile",p.controlUseTile},
                    {"hook",p.controlHook},{"mount",p.controlMount},{"dash",p.controlDash}
                }}
        };
        if(IsFlight)
        {
            snapshot["wings"]=p.wings; snapshot["wingsLogic"]=p.wingsLogic;
            snapshot["rocketBoots"]=p.rocketBoots; snapshot["rocketTime"]=p.rocketTime; snapshot["rocketTimeMax"]=p.rocketTimeMax;
            snapshot["rocketDelay"]=p.rocketDelay; snapshot["rocketDelay2"]=p.rocketDelay2;
            snapshot["canRocket"]=p.canRocket; snapshot["rocketRelease"]=p.rocketRelease;
            snapshot["sliding"]=p.sliding; snapshot["slowFall"]=p.slowFall; snapshot["noFallDmg"]=p.noFallDmg;
            snapshot["wingAccRunSpeed"]=p.wingAccRunSpeed; snapshot["wingRunAccelerationMult"]=p.wingRunAccelerationMult;
            snapshot["wet"]=p.wet; snapshot["honeyWet"]=p.honeyWet; snapshot["lavaWet"]=p.lavaWet; snapshot["shimmerWet"]=p.shimmerWet;
            snapshot["pulley"]=p.pulley; snapshot["frozen"]=p.frozen; snapshot["webbed"]=p.webbed; snapshot["stoned"]=p.stoned;
        }
        return snapshot;
    }

    static void AfterMotionUpdate(Player p)
    {
        if(motionFrame==null || motionPlayerCalls!=1 || motionJumpCalls!=1 || !motionFrame.ContainsKey("postPlayer") ||
            !motionFrame.ContainsKey("preJump") || !motionFrame.ContainsKey("postJump"))
            throw new InvalidOperationException("Missing paired native motion observations at tick "+ticks);
        var preJump=(Dictionary<string,object>)motionFrame["preJump"];
        var postJump=(Dictionary<string,object>)motionFrame["postJump"];
        int hostileNpcs=0;
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active)
            {
                if(npc.boss) throw new InvalidOperationException("Unexpected Boss in no-Boss motion fixture");
                if(!npc.friendly && npc.damage>0) hostileNpcs++;
            }
        if(p.dead || p.statLife!=400 || deaths!=0)
            throw new InvalidOperationException("Damage/death contaminated the isolated motion trajectory");
        if(SessionState()!="Idle") throw new InvalidOperationException("Production takeover must remain Idle throughout motion fixture");
        if(Math.Abs(p.position.X-initialPosition.X)>.01f || Math.Abs(p.velocity.X)>.0001f)
            throw new InvalidOperationException("Unexpected horizontal movement in jump-only fixture");
        motionCloudConsumed|=(bool)preJump["canJumpAgain_Cloud"] && !(bool)postJump["canJumpAgain_Cloud"];
        motionSawAirborne|=p.Bottom.Y<8000-.1f;
        motionReturnedGround|=ticks>80 && Math.Abs(p.Bottom.Y-8000)<.01f && p.velocity.Y==0;
        motionMinimumY=Math.Min(motionMinimumY,p.position.Y);
        motionFrame["nativeFrameAfter"]=nativeFrames;
        motionFrame["playerUpdateCalls"]=motionPlayerCalls;
        motionFrame["jumpMovementCalls"]=motionJumpCalls;
        motionFrame["afterCopyInputReplays"]=motionInputReplays;
        motionFrame["hostileNpcCount"]=hostileNpcs;
        motionFrame["productionSessionState"]=SessionState();
        motionFrame["nativeUpdateMs"]=engineSamples[engineSamples.Count-1];
        File.AppendAllText(Path.Combine(Root,"motion-frames.jsonl"),Json(motionFrame)+Environment.NewLine,new UTF8Encoding(false));
        motionRecordedFrames++;
        motionFrame=null;
        if(ticks==MotionTotalFrames)
        {
            bool expectCloud=motionCase=="cloud-release-press";
            if(motionRecordedFrames!=MotionTotalFrames || !motionSawAirborne || !motionReturnedGround || motionCloudConsumed!=expectCloud)
                throw new InvalidOperationException("Motion fixture did not exercise its declared initial jump/cloud/landing sequence");
            finishing=true;
            WriteMotionResult("complete",0,null);
            Log("MOTION_FINISH case="+motionCase+" frames="+motionRecordedFrames+" cloudConsumed="+motionCloudConsumed);
            Environment.Exit(0);
        }
    }

    static bool FlightHasLightning { get { return flightCase.IndexOf("-lightning-",StringComparison.Ordinal)>=0; } }
    static bool FlightHasCloud { get { return flightCase.IndexOf("-cloud-",StringComparison.Ordinal)>=0; } }
    static bool FlightHasFeatherfall { get { return flightCase.IndexOf("-feather-",StringComparison.Ordinal)>=0; } }
    static string FlightProfile { get { return "demon"+(FlightHasLightning?"-lightning":"")+(FlightHasCloud?"-cloud":"")+(FlightHasFeatherfall?"-featherfall":""); } }

    // These controls begin only after the 100-tick Demon wing resource is
    // exhausted. Keeping the window bounded also proves exit back to neutral
    // feather fall and leaves enough frames for a real native landing.
    static bool FlightUpRequested() { return flightCase=="demon-feather-up" && ticks>=230 && ticks<=330; }
    static bool FlightDownRequested() { return flightCase=="demon-feather-down" && ticks>=230 && ticks<=270; }

    static bool FlightJumpRequested()
    {
        if(ticks<=MotionWarmupFrames) return false;
        if(flightCase.EndsWith("-held-landing",StringComparison.Ordinal)) return ticks<=580;
        if(ticks>220) return false;
        if(flightHasEarlyRelease()) return ticks<=45 || ticks>=66;
        if(FlightHasCloud) return ticks!=26;
        return true;
    }

    static bool flightHasEarlyRelease() { return flightCase.EndsWith("-early-repress",StringComparison.Ordinal); }

    static string FlightPhase()
    {
        if(ticks<=MotionWarmupFrames) return "warmup-release";
        if(ticks==21) return "ground-initial-press";
        if(flightCase.EndsWith("-held-landing",StringComparison.Ordinal))
            return ticks<=580?"held-exhaustion-and-landing-window":"released-ground-recovery-window";
        if(FlightHasFeatherfall)
        {
            if(FlightUpRequested()) return "exhausted-featherfall-up";
            if(FlightDownRequested()) return "exhausted-featherfall-down-bypass";
            return ticks<=220?"held-wing-then-featherfall":"released-neutral-featherfall";
        }
        if(ticks>220) return "released-final-landing-window";
        if(flightHasEarlyRelease()) return ticks<=45?"initial-held-flight":ticks<=65?"early-release":ticks==66?"airborne-repress":"repressed-exhaustion-window";
        if(FlightHasCloud) return ticks==26?"cloud-release":ticks==27?"cloud-repress":"held-cloud-then-flight-window";
        return "held-exhaustion-glide-window";
    }

    static void AfterFlightUpdate(Player p)
    {
        if(motionFrame==null || motionPlayerCalls!=1 || motionJumpCalls!=1 || !motionFrame.ContainsKey("postPlayer") ||
            !motionFrame.ContainsKey("preJump") || !motionFrame.ContainsKey("postJump") ||
            (flightWingCalls==1)!=(motionFrame["preWing"]!=null && motionFrame["postWing"]!=null))
            throw new InvalidOperationException("Missing paired native flight observations at tick "+ticks);
        var preJump=(Dictionary<string,object>)motionFrame["preJump"];
        var postJump=(Dictionary<string,object>)motionFrame["postJump"];
        bool requested=FlightJumpRequested();
        bool grounded=Math.Abs(p.Bottom.Y-8000)<.01f && p.velocity.Y==0;
        int hostileNpcs=0;
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active)
            {
                if(npc.boss) throw new InvalidOperationException("Unexpected Boss in no-Boss flight fixture");
                if(!npc.friendly && npc.damage>0) hostileNpcs++;
            }
        if(p.dead || p.statLife!=400 || deaths!=0) throw new InvalidOperationException("Damage/death contaminated the flight trajectory");
        if(SessionState()!="Idle") throw new InvalidOperationException("Production takeover must remain Idle throughout flight fixture");
        if(Math.Abs(p.position.X-initialPosition.X)>.01f || Math.Abs(p.velocity.X)>.0001f)
            throw new InvalidOperationException("Unexpected horizontal motion in vertical-flight fixture");
        if(p.gravDir!=1 || p.wet || p.honeyWet || p.lavaWet || p.shimmerWet || p.pulley || p.frozen || p.webbed || p.stoned ||
            p.sliding || p.slowFall!=FlightHasFeatherfall || (p.FindBuffIndex(BuffID.Featherfall)>=0)!=FlightHasFeatherfall ||
            p.grapCount!=0 || p.mount.Active)
            throw new InvalidOperationException("Unsupported state contaminated the declared flight profile");
        if(p.wingsLogic!=1 || p.wingTimeMax!=100 || p.rocketBoots!=(FlightHasLightning?2:0) || p.rocketTimeMax!=7 ||
            p.hasJumpOption_Cloud!=FlightHasCloud || Player.jumpSpeed!=5.01f || Player.jumpHeight!=15)
            throw new InvalidOperationException("Actual native equipment or motion parameters differ from declared flight profile");
        flightTotalWingCalls+=flightWingCalls;
        flightCloudConsumed|=(bool)preJump["canJumpAgain_Cloud"] && !(bool)postJump["canJumpAgain_Cloud"];
        flightSawAirborne|=p.Bottom.Y<7999.9f;
        flightReturnedGround|=ticks>220 && grounded;
        flightMinimumY=Math.Min(flightMinimumY,p.position.Y);
        flightMaximumWingTime=Math.Max(flightMaximumWingTime,p.wingTime);
        if(flightFirstExhaustion<0 && flightTotalWingCalls>0 && p.wingTime==0 && !grounded) flightFirstExhaustion=ticks;
        if(flightFirstExhaustion>=0 && requested && flightWingCalls==0 && p.wingTime==0 && p.velocity.Y>0 && !grounded)
            flightEmptyHeldDescentFrames++;
        if(!requested && !grounded && ticks>220) flightReleasedAirborneFrames++;
        if(requested && grounded && flightFirstExhaustion>=0 && p.wingTime==0) flightHeldGroundEmptyFrames++;
        if(!requested && grounded && ticks>220 && p.wingTime==p.wingTimeMax) flightReleasedGroundFullFrames++;
        if(flightHasEarlyRelease() && ticks>=66 && flightWingCalls==1) flightPoweredAfterRepress++;
        if(FlightHasFeatherfall && flightWingCalls==1) flightFeatherPoweredFrames++;
        if(FlightHasFeatherfall && flightWingCalls==0 && p.wingTime==0 && !grounded && p.velocity.Y>0)
        {
            if(FlightUpRequested()) flightFeatherUpFrames++;
            else if(FlightDownRequested()) flightFeatherDownFrames++;
            else flightFeatherNeutralFrames++;
        }
        if(FlightHasLightning && Convert.ToInt32(preJump["rocketTime"],CultureInfo.InvariantCulture)>0 && p.rocketTime==0 &&
            p.wingTime>p.wingTimeMax)
        {
            flightRocketConversions++;
            if(flightFirstRocketConversion<0) flightFirstRocketConversion=ticks;
        }
        motionFrame["nativeFrameAfter"]=nativeFrames;
        motionFrame["playerUpdateCalls"]=motionPlayerCalls;
        motionFrame["jumpMovementCalls"]=motionJumpCalls;
        motionFrame["wingMovementCalls"]=flightWingCalls;
        motionFrame["afterCopyInputReplays"]=motionInputReplays;
        motionFrame["hostileNpcCount"]=hostileNpcs;
        motionFrame["productionSessionState"]=SessionState();
        motionFrame["nativeUpdateMs"]=engineSamples[engineSamples.Count-1];
        File.AppendAllText(Path.Combine(Root,"flight-frames.jsonl"),Json(motionFrame)+Environment.NewLine,new UTF8Encoding(false));
        flightRecordedFrames++;
        motionFrame=null;
        if(ticks==FlightTotalFrames)
        {
            bool heldLanding=flightCase.EndsWith("-held-landing",StringComparison.Ordinal);
            if(flightRecordedFrames!=FlightTotalFrames || !flightSawAirborne || !flightReturnedGround ||
                flightFirstExhaustion<0 || flightEmptyHeldDescentFrames==0 || flightReleasedGroundFullFrames==0 ||
                flightCloudConsumed!=FlightHasCloud || flightTotalWingCalls!=(FlightHasLightning?142:100) ||
                (FlightHasLightning && (flightRocketConversions!=1 || flightMaximumWingTime!=142)) ||
                (!FlightHasLightning && (flightRocketConversions!=0 || flightMaximumWingTime!=100)) ||
                (heldLanding && flightHeldGroundEmptyFrames==0) || (!heldLanding && flightReleasedAirborneFrames==0) ||
                (flightHasEarlyRelease() && flightPoweredAfterRepress==0) ||
                (FlightHasFeatherfall && (flightFeatherPoweredFrames==0 || flightFeatherNeutralFrames==0)) ||
                (flightCase=="demon-feather-up" && flightFeatherUpFrames==0) ||
                (flightCase=="demon-feather-down" && flightFeatherDownFrames==0) ||
                (flightCase=="demon-feather-neutral" && (flightFeatherUpFrames!=0 || flightFeatherDownFrames!=0)))
                throw new InvalidOperationException("Flight fixture did not exercise every declared native resource/control/landing boundary");
            finishing=true;
            WriteFlightResult("complete",0,null);
            Log("FLIGHT_FINISH case="+flightCase+" frames="+flightRecordedFrames+" wingCalls="+flightTotalWingCalls);
            Environment.Exit(0);
        }
    }

    static void WriteFlightResult(string status,int exitCode,string failure)
    {
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-native-flight-result/v1"},{"scenario",scenario.Id},{"flightCase",flightCase},
            {"flightProfile",flightCase==null?null:FlightProfile},{"status",status},{"processExitCode",exitCode},
            {"validFlight",status=="complete"},{"failure",failure},{"seed",seed},{"difficulty",difficulty},{"difficultyCode",difficultyCode},
            {"ticks",ticks},{"nativeFrames",nativeFrames},{"recordedFrames",flightRecordedFrames},
            {"warmupFrames",MotionWarmupFrames},{"totalFrames",FlightTotalFrames},{"equipment",equipmentReport},
            {"initialPosition",new Dictionary<string,object>{{"x",initialPosition.X},{"y",initialPosition.Y}}},
            {"maximumRisePixels",flightMinimumY==float.MaxValue?0:initialPosition.Y-flightMinimumY},
            {"nativeDifficultyVerified",nativeDifficultyVerified},{"nativeDifficulty",nativeDifficultyReport},
            {"nativeRandom",new Dictionary<string,object>
                {
                    {"installedAfterSetup",battleRandomInstalled},{"seed",seed},{"independentTwinFingerprint",battleRandomFingerprint},
                    {"referenceChecks",battleRandomReferenceChecks},{"actualAndNativeNamedColdStateVerified",battleRandomColdStateVerified},
                    {"unpausedUpdateSeedInitial",initialUnpausedUpdateSeed},{"unpausedUpdateSeedFinal",expectedUnpausedUpdateSeed},
                    {"unpausedUpdateSeedAdvances",unpausedUpdateSeedAdvances},{"actualStreamConsumedForFingerprint",false}
                }},
            {"arena",new Dictionary<string,object>
                {
                    {"kind","in-memory fixture; no generated or saved user world"},{"groundTile","GrayBrick"},
                    {"groundTop",500},{"groundLeft",800},{"groundRightExclusive",3400},{"groundThickness",6},
                    {"platformRows",new int[0]},{"nativeSceneMetricRefreshes",nativeSceneMetricRefreshes}
                }},
            {"coverage",new Dictionary<string,object>
                {
                    {"sawAirborne",flightSawAirborne},{"returnedGround",flightReturnedGround},{"wingMovementCalls",flightTotalWingCalls},
                    {"firstExhaustionTick",flightFirstExhaustion},{"emptyWingHeldDescentFrames",flightEmptyHeldDescentFrames},
                    {"releasedAirborneFrames",flightReleasedAirborneFrames},{"heldGroundEmptyWingFrames",flightHeldGroundEmptyFrames},
                    {"releasedGroundFullWingFrames",flightReleasedGroundFullFrames},{"poweredAfterRepressFrames",flightPoweredAfterRepress},
                    {"cloudConsumed",flightCloudConsumed},{"rocketConversions",flightRocketConversions},
                    {"firstRocketConversionTick",flightFirstRocketConversion},{"maximumWingTime",flightMaximumWingTime},
                    {"featherPoweredFrames",flightFeatherPoweredFrames},{"featherNeutralFrames",flightFeatherNeutralFrames},
                    {"featherUpFrames",flightFeatherUpFrames},{"featherDownBypassFrames",flightFeatherDownFrames}
                }},
            {"inputSchedule","release 1..20; exhaust/feather: jump21..220; feather-up: Up230..330; feather-down: Down230..270; early: jump21..45/release46..65/jump66..220; held-landing: jump21..580; cloud: jump21..25/release26/jump27..220"},
            {"frameFile","flight-frames.jsonl"},{"partialUncommittedFrame",motionFrame},
            {"observationOrder","prePlayer -> native input copy/test-only replay -> native equipment/UpdateJumpHeight -> preJump -> real JumpMovement -> postJump -> optional preWing/real WingMovement/postWing -> native conversion/rocket/gravity/collision -> postPlayer"},
            {"scope","no-Boss native vertical-flight microtest, not Boss victory or horizontal/adapter/latency acceptance; production stays Idle; no F8 or direct jump/flight resource/velocity changes"},
            {"nativeUpdate",TimingSummary(engineSamples,"native headless update plus instrumentation; not production or end-to-end latency")},
            {"createdUtc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)}
        };
        File.WriteAllText(Path.Combine(Root,"result.json"),Json(result),new UTF8Encoding(false));
    }

    static void WriteMotionResult(string status,int exitCode,string failure)
    {
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-native-motion-result/v1"},{"scenario",scenario.Id},{"motionCase",motionCase},
            {"status",status},{"processExitCode",exitCode},{"validMotion",status=="complete"},{"failure",failure},
            {"seed",seed},{"difficulty",difficulty},{"difficultyCode",difficultyCode},
            {"ticks",ticks},{"nativeFrames",nativeFrames},{"recordedFrames",motionRecordedFrames},
            {"warmupFrames",MotionWarmupFrames},{"totalFrames",MotionTotalFrames},{"equipment",equipmentReport},
            {"initialPosition",new Dictionary<string,object>{{"x",initialPosition.X},{"y",initialPosition.Y}}},
            {"minimumY",motionMinimumY==float.MaxValue?0:motionMinimumY},
            {"maximumRisePixels",motionMinimumY==float.MaxValue?0:initialPosition.Y-motionMinimumY},
            {"cloudConsumed",motionCloudConsumed},{"sawAirborne",motionSawAirborne},{"returnedGroundAfterRelease",motionReturnedGround},
            {"nativeDifficultyVerified",nativeDifficultyVerified},{"nativeDifficulty",nativeDifficultyReport},
            {"nativeRandom",new Dictionary<string,object>
                {
                    {"installedAfterSetup",battleRandomInstalled},{"seed",seed},
                    {"independentTwinFingerprint",battleRandomFingerprint},{"referenceChecks",battleRandomReferenceChecks},
                    {"actualAndNativeNamedColdStateVerified",battleRandomColdStateVerified},
                    {"unpausedUpdateSeedInitial",initialUnpausedUpdateSeed},{"unpausedUpdateSeedFinal",expectedUnpausedUpdateSeed},
                    {"unpausedUpdateSeedAdvances",unpausedUpdateSeedAdvances},{"actualStreamConsumedForFingerprint",false}
                }},
            {"arena",new Dictionary<string,object>
                {
                    {"kind","in-memory fixture; no generated or saved user world"},{"groundTile","GrayBrick"},
                    {"groundTop",500},{"groundLeft",800},{"groundRightExclusive",3400},{"groundThickness",6},
                    {"platformRows",new int[0]},{"nativeSceneMetricRefreshes",nativeSceneMetricRefreshes}
                }},
            {"inputSchedule","frames 1..20 released warmup; initial press 21; hold through 80, OR tap only 21, OR hold 21..25/release 26/repress 27..80; release 81..180"},
            {"frameFile","motion-frames.jsonl"},{"partialUncommittedFrame",motionFrame},
            {"observationOrder","prePlayer -> native input copy/test-only replay -> native equipment/UpdateJumpHeight -> preJump -> real JumpMovement -> postJump -> remaining native Player.Update -> postPlayer; all warmup frames retained"},
            {"scope","no-Boss native motion microtest, not Boss victory evidence or rendered-client latency; production remains Idle, no F8; controls only, no jump/velocity/capability overrides"},
            {"nativeUpdate",TimingSummary(engineSamples,"native headless world update plus instrumentation; not production or end-to-end latency")},
            {"createdUtc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)}
        };
        File.WriteAllText(Path.Combine(Root,"result.json"),Json(result),new UTF8Encoding(false));
    }

    static void SampleRuntimeTiming()
    {
        if(runtimeTotalField==null || runtimeFramesField==null)
            throw new MissingFieldException("Production runtime timing counters unavailable");
        long total=(long)runtimeTotalField.GetValue(null);
        int count=(int)runtimeFramesField.GetValue(null);
        if(count<previousRuntimeFrames || total<previousRuntimeTotal)
        { previousRuntimeFrames=0; previousRuntimeTotal=0; }
        if(count>previousRuntimeFrames)
            runtimeSamples.Add((total-previousRuntimeTotal)*1000d/Stopwatch.Frequency/(count-previousRuntimeFrames));
        previousRuntimeFrames=count;
        previousRuntimeTotal=total;
    }

    static bool IsExpectedRoot(NPC npc)
    {
        if(npc==null || scenario==null || scenario.BossTypes==null) return false;
        foreach(int type in scenario.BossTypes) if(type==npc.type) return true;
        return false;
    }

    static int CaptureActiveExpectedRootLife()
    {
        if(Game.npc==null) return 0;
        int count=0,total=0;
        foreach(var npc in Game.npc)
        {
            if(npc==null || !npc.active || !IsExpectedRoot(npc)) continue;
            if(count==0) lastBossLifeExpectedRoots.Clear();
            count++;
            total+=Math.Max(0,npc.life);
            lastBossLifeExpectedRoots.Add(new BossLifeObservationRoot
            {
                Slot=npc.whoAmI,Type=npc.type,Life=Math.Max(0,npc.life),LifeMax=npc.lifeMax
            });
        }
        if(count>0)
        {
            lastBossLife=total;
            lastBossLifeObservedTick=ticks;
            lastBossLifeExpectedRootCount=count;
            if(total<=0) episodeBossKilled=true;
        }
        return count;
    }

    static Dictionary<string,object> BuildBossLifeObservation(bool win,out int reportedLife)
    {
        int activeExpectedRootCountAtTermination=CaptureActiveExpectedRootLife();
        bool activeAtTermination=activeExpectedRootCountAtTermination>0;
        reportedLife=win?0:lastBossLife;
        string kind=win?"confirmed-victory":activeAtTermination?"active-expected-roots":
            lastBossLifeObservedTick>=0?"last-active-expected-roots":"not-observed";
        int observedTick=win?ticks:lastBossLifeObservedTick;
        var roots=new List<Dictionary<string,object>>(lastBossLifeExpectedRoots.Count);
        foreach(var root in lastBossLifeExpectedRoots)
            roots.Add(new Dictionary<string,object>
            {
                {"slot",root.Slot},{"type",root.Type},{"life",root.Life},{"lifeMax",root.LifeMax}
            });
        return new Dictionary<string,object>
        {
            {"schema","chaite-boss-life-observation/v1"},{"life",reportedLife},{"observedTick",observedTick},{"kind",kind},
            {"activeAtTermination",activeAtTermination},{"activeExpectedRootCountAtTermination",activeExpectedRootCountAtTermination},
            {"lastActiveExpectedRootTick",lastBossLifeObservedTick},{"lastActiveExpectedRootLife",lastBossLife},
            {"lastActiveExpectedRootCount",lastBossLifeExpectedRootCount},{"lastActiveExpectedRoots",roots.ToArray()}
        };
    }

    static void TrackExpectedBossDamage()
    {
        retiredBossSlots.Clear();
        foreach(var pair in previousBosses)
        {
            var npc=pair.Key<Game.npc.Length?Game.npc[pair.Key]:null;
            if(npc==null || !npc.active || npc.type!=pair.Value.Type)
            {
                // Count the final killing blow, but do NOT count a healthy despawn
                // or a reused NPC index as damage. Shared worm segments are excluded.
                if(npc!=null && npc.type==pair.Value.Type && npc.life<=0) bossDamage+=pair.Value.Life;
                retiredBossSlots.Add(pair.Key);
            }
        }
        foreach(int key in retiredBossSlots) previousBosses.Remove(key);
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc==null || !npc.active || !IsExpectedRoot(npc)) continue;
            BossLifeSample previous;
            if(previousBosses.TryGetValue(i,out previous) && previous.Type==npc.type && previous.Life>npc.life)
                bossDamage+=previous.Life-Math.Max(0,npc.life);
            previousBosses[i]=new BossLifeSample { Type=npc.type, Life=Math.Max(0,npc.life) };
        }
    }

    static void RebaseExpectedBossDamageObservation()
    {
        // Phase fixtures deliberately mutate native Boss life before takeover.
        // That setup is not player output. Re-seed the per-slot baseline after
        // staging so damage from the first controlled native frame is retained
        // without counting the fixture's life-band transition.
        previousBosses.Clear();
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc==null || !npc.active || !IsExpectedRoot(npc)) continue;
            previousBosses[i]=new BossLifeSample
            {
                Type=npc.type,
                Life=Math.Max(0,npc.life)
            };
        }
    }

    static string SessionState()
    {
        Type rt=typeof(Chaite.Plugin.Runtime);
        var fault=(bool)rt.GetField("_faulted",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        if(fault) return "Faulted";
        var encounter=rt.GetField("_encounter",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        return encounter==null?"Uninitialized":encounter.GetType().GetProperty("State").GetValue(encounter,null).ToString();
    }

    /// <summary>
    /// A dead Boss is a won fight, whatever the session state says.
    ///
    /// MEASURED 2026-09-21: fsw121d recorded 70 episodes as "Cancelled" carrying
    /// bossLifeRemaining==0 and bossDamage at the full 77,980 -- the Boss was
    /// dead and the fight was won, but the plugin's safety abort had already set
    /// the session state to Cancelled, so the win reached the trainer as neither
    /// a win nor a death. Those 70 were 26% of that arm's recorded wins, and the
    /// reward is what teaches the policy, so denying them is a training defect
    /// rather than a fact about the fight. The Boss's own health is the fact; the
    /// session state is a race the abort can win.
    ///
    /// `lastBossLife` is reset to 0 alongside `lastBossLifeObservedTick=-1` at
    /// every episode reset, so requiring an observation is what forbids matching
    /// a Boss that has not spawned yet -- without it, the first abort of an
    /// episode would be scored as a win.
    /// </summary>
    static string NormalizeTerminalState(string state)
    {
        if(state!="Cancelled" && state!="EncounterInterrupted") return state;
        if(!episodeBossKilled) return state;
        bool playerDead=Game.player[0]!=null && Game.player[0].dead;
        return playerDead?"SuccessAfterDeath":"SuccessNoDeath";
    }

    public static void AfterDraw()
    {
        if(!booted || failed || captured || ticks < 180) return;
        try
        {
            var device=Game.instance.GraphicsDevice;
            int width=device.PresentationParameters.BackBufferWidth, height=device.PresentationParameters.BackBufferHeight;
            var pixels=new Color[width*height];
            device.GetBackBufferData(pixels);
            using(var texture=new Texture2D(device,width,height))
            using(var file=File.Create(Path.Combine(Root,"actual-client-frame.png")))
            { texture.SetData(pixels); texture.SaveAsPng(file,width,height); }
            captured=true;
            Log("BACKBUFFER_CAPTURED "+width+"x"+height);
        }
        catch(Exception e) { Log("CAPTURE_ERROR "+e); captured=true; }
    }

    public static void FatalException(Exception e) { Fail(e); }
    public static void FatalCaughtObject(object e) { Fail(e as Exception ?? new InvalidOperationException("Native catch: "+e)); }

    static void CompareNativeBeamGeometry()
    {
        var random=new Random(455923);
        var damageMethod=typeof(Projectile).GetMethod("Damage_CanDealDamage",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(damageMethod==null) throw new MissingMethodException("Projectile.Damage_CanDealDamage");
        var canDealDamage=(Func<Projectile,bool>)Delegate.CreateDelegate(typeof(Func<Projectile,bool>),damageMethod);
        int compared=0, falseNegative=0, conservative=0, scenarios=0;
        int sourceWarmupHits=0, expiredChecks=0;
        foreach(int type in new[]{455})
        {
            var ages=new[]{0,1,19,20,21,90,170,179,180};
            foreach(int age in ages)
            for(int variant=0;variant<2;variant++)
            for(int phase=0;phase<12;phase++)
            {
                var projectile=new Projectile();projectile.SetDefaults(type);
                float angle=(float)(phase*Math.PI/6), limit=variant==0?1f:.4f;
                float length=phase%3==0?2400f:phase%3==1?600f:48f;
                var center=new Vector2(5000+(phase%2==0?0f:.375f),5000+(phase%2==0?0f:.625f));
                projectile.Center=center;
                projectile.velocity=new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle));
                projectile.localAI[0]=age;
                projectile.localAI[1]=length;
                projectile.ai[0]=angle;
                projectile.rotation=angle+.3490659f*Math.Max(0f,Math.Min(1f,(age-50f)/130f));
                projectile.scale=Math.Max(0f,Math.Min(limit,(float)Math.Sin(age*3.141593f/180f)*10f*limit));
                // The original AI routine kills at age>=180. This is a lifecycle
                // fixture, not an assertion that this comparison executes its AI.
                projectile.active=age<180;
                bool nativeDamageGate=canDealDamage(projectile);
                if(!nativeDamageGate)
                    throw new InvalidOperationException("Unexpected native beam damage gate: type="+type+" age="+age);
                var threat=new Chaite.Core.ThreatSnapshot { Type=type, Geometry=Chaite.Core.ThreatGeometry.MoonLordDeathray,
                    Position=new Chaite.Core.Vec2(projectile.position.X,projectile.position.Y),Width=projectile.width,Height=projectile.height,
                    BeamOrigin=new Chaite.Core.Vec2(center.X,center.Y),BeamDirection=new Chaite.Core.Vec2(projectile.velocity.X,projectile.velocity.Y),
                    BeamAge=age,BeamLength=length,BeamScale=projectile.scale,BeamScaleLimit=limit,
                    BeamAngle=projectile.rotation,BeamBaseAngle=angle,TimeLeft=600 };
                var beam=Chaite.Core.BeamGeometry.AtTime(in threat,0);
                float collisionAngle=angle;
                var axis=new Vector2((float)Math.Cos(collisionAngle),(float)Math.Sin(collisionAngle));
                var normal=new Vector2(-axis.Y,axis.X);
                for(int sample=0;sample<160;sample++)
                {
                    Rectangle box;
                    if(sample==0) box=new Rectangle((int)center.X-21,(int)center.Y+1,20,42); // Original behind-emitter regression.
                    else if(sample<40) box=new Rectangle((int)center.X-50+random.Next(81),(int)center.Y-60+random.Next(81),20,42);
                    else if(sample<120)
                    {
                        // Concentrate on segment edges/tips as well as the unscaled
                        // source body; a uniform full-world draw rarely hits either.
                        float reach=length;
                        float width=36f*projectile.scale;
                        float along=(float)(random.NextDouble()*(reach+80f)-40f);
                        float across=(float)((random.NextDouble()*2f-1f)*(width*.5f+45f));
                        var targetCenter=center+axis*along+normal*across;
                        box=new Rectangle((int)(targetCenter.X-10f),(int)(targetCenter.Y-21f),20,42);
                    }
                    else box=new Rectangle(2500+random.Next(5000),2500+random.Next(5000),20,42);
                    // Deathray's age<20 branch suppresses its line only, not
                    // source-body contact.
                    bool nativeShape=projectile.Colliding(projectile.Hitbox,box);
                    bool native=projectile.active&&nativeDamageGate&&nativeShape;
                    var bounds=new Chaite.Core.RectF(box.X,box.Y,box.Width,box.Height);
                    bool actual=Chaite.Core.BeamGeometry.Intersects(in bounds,in beam,0);
                    if(age<20&&native) sourceWarmupHits++;
                    if(!projectile.active) expiredChecks++;
                    if(native&&!actual)
                    {
                        falseNegative++;
                        if(falseNegative<=8) Log("BEAM_MISS type="+type+" age="+age+" limit="+limit+" scale="+projectile.scale+" angle="+angle+" length="+length+" box="+box);
                    }
                    if(!native&&actual)
                    {
                        conservative++;
                        if(conservative<=8) Log("BEAM_EXTRA type="+type+" age="+age+" limit="+limit+" scale="+projectile.scale+" angle="+angle+" length="+length+" box="+box);
                    }
                    compared++;
                }
                scenarios++;
            }
        }
        Log("NATIVE_BEAM_COMPARE scenarios="+scenarios+" samples="+compared+" falseNegative="+falseNegative+" conservativeExtra="+conservative+
            " sourceWarmupHits="+sourceWarmupHits+" expiredChecks="+expiredChecks);
        if(sourceWarmupHits==0||expiredChecks==0)
            throw new InvalidOperationException("Native beam comparison missed a required gate/lifecycle category");
        if(falseNegative>0) throw new InvalidOperationException("Beam model missed native collisions");
    }
    static void Fail(Exception e)
    {
        failed=true;
        try
        {
            Log("FAIL "+e);
            WriteResult("harness-error","harness-exception",false,10,e.ToString());
        }
        finally { Environment.Exit(10); }
    }
    static bool EncounterFixtureReady()
    {
        if (IsMonitorFixture)
            return monitorArmedTick == (episodeLimit>0 ? episodeArmTick : takeoverTick) && monitorPassiveFrames >= 120 &&
                monitorCombatTick >= directSpawnTick && monitorCombatTick <= directSpawnTick + 1 &&
                directSpawnCompleted && !phaseStageAttempted;
        // Episode mode re-arms through the synthetic edge, so the takeover the
        // fixture compares against is the arm that started the last fight.
        int armTick = episodeLimit>0 ? episodeArmTick : takeoverTick;
        return scenario!=null && (scenario.DirectSpawn?
            directSpawnAttempted && directSpawnCompleted && phaseStageAttempted && phaseStaged &&
                phaseVerifiedAtTakeover && actualTakeoverTick==armTick:
            sawSummonConsumed && actualTakeoverTick==armTick);
    }
    static int ScopeNegativeControlFrameTotal()
    {
        return scopeNegativeLeftFrames+scopeNegativeRightFrames+scopeNegativeUpFrames+scopeNegativeDownFrames+
            scopeNegativeJumpFrames+scopeNegativeHookFrames+scopeNegativeDashFrames+scopeNegativeMountFrames+
            scopeNegativeUseItemFrames+scopeNegativeUseTileFrames+scopeNegativeThrowFrames+
            scopeNegativeQuickHealFrames+scopeNegativeQuickManaFrames;
    }
    static bool ScopeNegativePassed()
    {
        if(!IsScopeNegative || !scopeNegativeActivationCaptured || scopeNegativeActivationTick!=takeoverTick ||
            actualTakeoverTick!=takeoverTick || !scopeNegativeTopologyMatches ||
            scopeNegativeUnsupportedCueCalls!=1 || scopeNegativeCueCalls!=1 || scopeNegativeExactChatCalls!=1 ||
            scopeNegativeSessionBefore!="Idle" || scopeNegativeSessionAfter!="Idle" ||
            scopeNegativeSessionUnexpectedFrames!=0 || observedPlanCalls!=0 || observedPlanReturns!=0 ||
            observedPlanUnpaired!=0 || observedPlanPending || scopeNegativeActionableControlFrames!=0 ||
            ScopeNegativeControlFrameTotal()!=0 || scopeNegativeOwnedFriendlyProjectileObservations!=0 ||
            scopeNegativeSelectedChangeFrames!=0 || scopeNegativeSummonChangeFrames!=0 ||
            scopeNegativeAmmoChangeFrames!=0 || scopeNegativeItemUseFrames!=0 || scopeNegativeLifeLossFrames!=0 ||
            scopeNegativeMaximumPositionDeltaSquared>.01f || deaths!=0 || !nativeDifficultyVerified)
            return false;
        bool expectsRoots=scenario.ScopeNegativeRootTypes.Length>0;
        if(directSpawnAttempted!=expectsRoots || directSpawnCompleted!=expectsRoots) return false;
        var p=Game.player[0];
        return p!=null && p.inventory[1].type==scopeNegativeSummonBeforeType &&
            p.inventory[1].stack==scopeNegativeSummonBeforeStack &&
            p.inventory[54].type==scopeNegativeAmmoBeforeType && p.inventory[54].stack==scopeNegativeAmmoBeforeStack;
    }
    static void FinishScopeNegative(string outcome)
    {
        if(finishing) return;
        finishing=true;
        bool passed=ScopeNegativePassed();
        int exitCode=passed?0:21;
        Log("SCOPE_NEGATIVE_FINISH outcome="+outcome+" passed="+passed+" ticks="+ticks+
            " cue="+scopeNegativeUnsupportedCueCalls+" chat="+scopeNegativeExactChatCalls+
            " controls="+ScopeNegativeControlFrameTotal()+" applyCalls="+observedPlanCalls+
            " projectiles="+scopeNegativeOwnedFriendlyProjectileObservations);
        WriteResult(passed?"passed":"failed",outcome,passed,exitCode,
            passed?null:"Scope-negative rejection contract was not satisfied");
        Environment.Exit(exitCode);
    }
    static void Finish(string outcome)
    {
        if(finishing) return;
        finishing=true;
        if(timerResolutionRaised)
        {
            try { TimeEndPeriod(1); } catch { }
            timerResolutionRaised=false;
        }
        // Episode mode: the probe is a training session, not one fight. The
        // episode that just ended is summarised into the bridge's own log and
        // the arena is soft-reset the way t-agent's ResetManager does, inside
        // the same process, so the trainer keeps its stream and the engine
        // keeps its identity. The final episode falls through to the ordinary
        // result path so the run still ends with a first-class result.json.
        if(episodeLimit>0 && episodeIndex+1<episodeLimit)
        {
            AppendEpisodeSummary(outcome);
            ResetEpisode();
            return;
        }
        bool expectedSeen=scenario!=null && expectedBossMask==((1<<scenario.BossTypes.Length)-1);
        bool fixtureReady=EncounterFixtureReady();
        bool battlePassed=(outcome=="SuccessNoDeath" || outcome=="SuccessAfterDeath") &&
            nativeFrames>120 && fixtureReady && expectedSeen && sawBossDamage && sawMovement && maximumShots>0;
        bool reportedSuccess=outcome=="SuccessNoDeath" || outcome=="SuccessAfterDeath";
        string status=battlePassed?"win":reportedSuccess?"harness-error":outcome=="test-time-limit"?"timeout":
            outcome=="activation-ended" && deaths==0?"rejected":"loss";
        int exitCode=battlePassed?0:reportedSuccess?10:20;
        Log("FINISH "+outcome+" battlePassed="+battlePassed+" ticks="+ticks+" hits="+hits+" bossLife="+lastBossLife+
            " nativeFrames="+nativeFrames+" fixtureReady="+fixtureReady+" summonConsumed="+sawSummonConsumed+" sawBoss="+sawBoss+
            " bossDamaged="+sawBossDamage+" moved="+sawMovement+" maximumShots="+maximumShots);
        WriteResult(status,outcome,battlePassed,exitCode,battlePassed?null:
            reportedSuccess?"Native session reported success without all required battle evidence":outcome);
        Environment.Exit(exitCode);
    }

    /// <summary>
    /// One line per finished training episode, appended to the bridge's own
    /// log so the trainer sees episode boundaries and outcomes without parsing
    /// production result files.
    /// </summary>
    static void AppendEpisodeSummary(string outcome)
    {
        var row=new Dictionary<string,object>
        {
            {"schema","chaite-bridge-episode/v1"},
            {"e",episodeIndex},{"outcome",outcome},{"win",outcome=="SuccessNoDeath"||outcome=="SuccessAfterDeath"},
            {"hits",episodeHits},{"deaths",Game.player[0]!=null && Game.player[0].dead?1:0},
            {"ticks",ticks-episodeStartTick},{"tickLimit",tickLimit},
            {"bossDamage",bossDamage-episodeBossDamageStart},
            {"simulatedDps",simulatedDps},
            {"simulatedDamage",simulatedDamageApplied-simulatedDamageEpisodeStart},
            {"bubblesBroken",simulatedBubbleBreaks-simulatedBubbleEpisodeStart},
            {"bossLifeRemaining",lastBossLife},{"elapsedWallMs",(long)clock.ElapsedMilliseconds},
            // Diagnostics for the dead-Boss normalization. A fix that silently
            // does not fire is exactly the failure this session kept finding, so
            // the evidence travels with the row instead of being inferred later.
            {"dbgBossKilled",episodeBossKilled},
            {"dbgObsTick",lastBossLifeObservedTick},
            {"dbgRootCount",lastBossLifeExpectedRootCount}
        };
        try
        {
            File.AppendAllText(bridgeBase+".episodes.jsonl",Json(row)+Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch(Exception error)
        {
            Log("BRIDGE_EPISODE_LOG_FAILED "+error.Message);
        }
    }

    /// <summary>
    /// Summons whatever mount the loadout's mount-slot item provides. The item
    /// is the single source: Item.mountType is the native mapping vanilla
    /// itself uses when the mount key is pressed, so no loadout-specific id
    /// table can drift away from the equipped gear.
    /// </summary>
    static void SummonLoadoutMount(Player player)
    {
        try
        {
            var item=player.miscEquips[3];
            int mountType=item==null?0:item.mountType;
            if(mountType<=0) return;
            if(player.mount.Active && player.mount.Type==mountType) return;
            player.mount.SetMount(mountType,player);
            Log("BRIDGE_MOUNT_SUMMONED type="+mountType+" active="+player.mount.Active);
        }
        catch(Exception error)
        {
            Log("BRIDGE_MOUNT_FAILED "+error.GetType().Name+" "+error.Message);
        }
    }

    /// <summary>
    /// The t-agent reset, ported onto the vanilla harness: despawn everything
    /// hostile, clear the world's loose items, restore the player to the
    /// fixture start with full life and the fixture's buffs, reset the
    /// per-episode counters, and schedule the synthetic re-arm edge that
    /// restarts the production monitor and the boss spawn.
    /// </summary>
    static void ResetEpisode()
    {
        var p=Game.player[0];
        // 1) Despawn every NPC that is not a town NPC and every projectile,
        //    ours included: leftover shots would hit the respawned boss for
        //    free, and natural spawns fill the 200 NPC slots at about twenty
        //    per episode. Clearing active alone is not enough: the 1.4.5 slot
        //    allocator keeps a per-slot protection counter
        //    (NPC.spawnSlotProtected, set to 2 by NewNPC), so a retired slot
        //    still reads as "in use", the boss spawn climbs one index per
        //    episode and the eleventh returns 200 and kills the harness.
        //    Measured: root indices 107, 128, 151, 173, 195, then 200.
        //    protection counter is itself bounded below Main.npc's length, so
        //    it is cleared by its own size, never by the NPC array's.
        for(int i=0;i<Game.npc.Length;i++)
        {
            var npc=Game.npc[i];
            if(npc!=null && npc.active && !npc.townNPC) npc.active=false;
            if(npc!=null && !npc.townNPC && i<NPC.spawnSlotProtected.Length)
                NPC.spawnSlotProtected[i]=0;
        }
        for(int i=0;i<Game.projectile.Length;i++)
        {
            var shot=Game.projectile[i];
            if(shot!=null && shot.active) shot.active=false;
        }
        for(int i=0;i<Game.item.Length;i++)
        {
            var drop=Game.item[i];
            if(drop!=null && drop.active) drop.TurnToAir();
        }
        // 2) Restore the player: revive, heal, return to the fixture start
        //    with the fixture's buffs, and replenish the consumables the fight
        //    consumes so every episode is the same fight.
        //
        //    The real Fishron fight opens about twenty tiles from ONE end of
        //    the runway and which end varies per fight, so an episode loop that
        //    only ever used the launch side would never show the policy the
        //    mirrored opening and it would be unusable on the other side. The
        //    observation carries absolute positions, so alternating the end
        //    per episode costs no observation change and one policy can learn
        //    both openings.
        if(scenario!=null && scenario.Ocean && episodeLimit>0)
        {
            bool launchSide=(episodeIndex%2==0);
            requestedStartSide=launchSide?launchStartSide:
                (launchStartSide=="right"?"left":"right");
            int startTileX=PlayerStartTileX(arenaCenterXTiles);
            initialPosition=new Vector2(startTileX*16, arenaGroundYTiles*16 - p.height);
        }
        p.dead=false;
        p.ghost=false;
        p.respawnTimer=0;
        p.statLife=p.statLifeMax2;
        p.statMana=p.statManaMax2;
        p.velocity=Vector2.Zero;
        p.position=initialPosition;
        p.fallStart=p.fallStart2=(int)(p.position.Y/16f);
        for(int i=p.buffType.Length-1;i>=0;i--)
        {
            int type=p.buffType[i];
            if(type>0 && Main.debuff[type]) p.DelBuff(i);
        }
        p.AddBuff(BuffID.Ironskin,36000);
        p.AddBuff(BuffID.Regeneration,36000);
        p.AddBuff(BuffID.Swiftness,36000);
        p.AddBuff(BuffID.Endurance,36000);
        p.AddBuff(BuffID.Lifeforce,36000);
        p.AddBuff(BuffID.WellFed,36000);
        p.AddBuff(BuffID.Wrath,36000);
        p.AddBuff(BuffID.Rage,36000);
        p.potionDelay=0;
        p.itemAnimation=0;
        p.itemTime=0;
        p.reuseDelay=0;
        p.inventory[10].stack=20;
        p.inventory[54].stack=9999;
        if(formulaRoute!=null && formulaRoute.StartsWith("fishron",StringComparison.Ordinal) &&
            p.inventory[11].type==ItemID.InfernoPotion)
            p.inventory[11].stack=Chaite.Core.FishronThreatCatalog.RequiredInfernoPotionStock;
        // 2b) Summon the loadout's mount. Four of the six reviewed Fishron
        //     loadouts carry their mount as the miscEquips[3] item, and the
        //     bridge owns movement, so nothing ever pressed the mount key.
        //     Measured on fishron-trusty-chillet: mountActive=False and
        //     mountType=-1 for the whole session, per-episode damage stuck at
        //     285 while the wing loadouts reached 17156 over the same number of
        //     episodes. Mounting up is part of the loadout, not a decision the
        //     fight asks for, so the fixture summons the mount the equipped
        //     item provides, exactly as the real player would before engaging.
        //     Death dismounts, so this runs on every episode, not once at boot.
        SummonLoadoutMount(p);
        // 3) Reset the per-episode counters the way the boot path set them.
        episodeIndex++;
        episodeStartTick=ticks;
        episodeHits=0;
        episodeBossDamageStart=bossDamage;
    reportedKilledBossSlots.Clear();
    // The simulation counters are session totals; the episode record reports
    // per-episode figures, so the baselines are captured here. Reporting the
    // running total made a single episode look like it had dealt 469078 damage
    // to a boss with 78000 health.
    simulatedDamageEpisodeStart=simulatedDamageApplied;
    simulatedBubbleEpisodeStart=simulatedBubbleBreaks;
        episodeArmTick=ticks+30;
        directSpawnTick=episodeArmTick+120;
        directSpawnAttempted=false;
        directSpawnCompleted=false;
        directSpawnRootIndex=-1;
        monitorArmedTick=-1;
        monitorCombatTick=-1;
        monitorPassiveFrames=0;
        lastLife=p.statLife;
        minLife=p.statLife;
        sawBoss=false;
        sawBossDamage=false;
        sawMovement=false;
        sawSummonConsumed=false;
        maximumShots=0;
        maximumBossLife=0;
        expectedBossMask=0;
        previousBosses.Clear();
        lastBossLifeExpectedRoots.Clear();
        lastBossLife=0;
        lastBossLifeObservedTick=-1;
        lastBossLifeExpectedRootCount=0;
        episodeBossKilled=false;
        // 4) Release the terminal lock so the next terminal transition can
        //    become the next episode boundary.
        finishing=false;
        Log("BRIDGE_EPISODE_RESET e="+episodeIndex+" armAt="+episodeArmTick+" spawnAt="+directSpawnTick+
            " bossDamageCarried="+(bossDamage-episodeBossDamageStart)+
            " startSide="+requestedStartSide+" startX="+(int)initialPosition.X);
    }

    /// <summary>
    /// One compact observation per native tick, appended to the bridge's
    /// stream. This is the rollout a policy trains on: the player, the boss
    /// and the hostile projectiles that matter for dodging, with the episode
    /// index and the done flag that mark episode boundaries.
    /// </summary>
    /// <summary>The tick carried by the trainer's latest action line, or -1 when
    /// no action is readable yet. Read on every native tick, uncached: the cache
    /// this used to keep (1 ms) added up to a millisecond of latency to the
    /// round trip that the trainer is waiting on, and a small file read from the
    /// page cache costs far less than that. Measured: sessions sat at 137-165
    /// ticks/s with ~0.2 ms of real work per step, i.e. the two polling
    /// granularities, not the work, set the rate.</summary>
    static int ReadBridgeActionTick()
    {
        try
        {
            string path = bridgeBase + ".action";
            if(!System.IO.File.Exists(path)) return bridgeActionTick;
            string text;
            // Share delete as well: the trainer may replace the file, and a
            // reader that holds it without FileShare.Delete makes that fail.
            using(var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using(var reader = new StreamReader(stream))
                text = reader.ReadToEnd();
            int comma = text.IndexOf(',');
            int parsed;
            if(comma > 0 && int.TryParse(text.Substring(0, comma),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                bridgeActionTick = parsed;
        }
        catch { }
        return bridgeActionTick;
    }

    /// <summary>
    /// How soon this projectile can reach the player, in ticks, for the
    /// <c>CHAITE_PROJ_SORT=threat</c> ordering.
    ///
    /// The measured failure of the distance ordering: the Sharknado column is a
    /// large, slow, numerous set of hitboxes, so it filled all 12 slots on 1159
    /// of 2757 recorded life-loss frames while the fast closing projectiles that
    /// actually connect were pushed out. Distance cannot separate them, because
    /// "next to the player" is exactly where a column also is.
    ///
    /// The score is a time, not a distance, so a fast far projectile outranks a
    /// slow near one:
    /// <code>
    ///   gap    = |relative position| - half the projectile's larger extent
    ///   closing= -(relative position . relative velocity) / |relative position|
    ///   score  = gap / max(closing, 8)   +   0.25 * gap / 8
    /// </code>
    /// The first term is the time to closest approach at the current closing
    /// speed, floored at 8 px/tick so a projectile that is receding, or drifting
    /// sideways, still gets a finite rank instead of sorting last by accident.
    /// The second is a nominal time-to-cover-the-gap and keeps a stationary
    /// hitbox the player is about to fly into ranked ahead of a distant one. The
    /// half-extent subtraction is what makes a big hitbox count as closer, since
    /// it can connect from further away.
    /// </summary>
    static float ThreatScore(Projectile shot,Vector2 playerCenter)
    {
        float rx=shot.Center.X-playerCenter.X;
        float ry=shot.Center.Y-playerCenter.Y;
        float rvx=shot.velocity.X;
        float rvy=shot.velocity.Y;
        float range=(float)Math.Sqrt(rx*rx+ry*ry);
        float halfExtent=Math.Max(shot.width,shot.height)*0.5f;
        float gap=range-halfExtent;
        if(gap<0f) gap=0f;
        float closing;
        if(range<1f) closing=8f;
        else closing=-(rx*rvx+ry*rvy)/range;
        if(closing<8f) closing=8f;
        // Both terms are bounded so a far-away projectile cannot produce an
        // unbounded score that would overflow the comparison.
        float timeToClosest=gap/closing;
        float timeToGap=gap/8f;
        if(timeToClosest>10000f) timeToClosest=10000f;
        if(timeToGap>10000f) timeToGap=10000f;
        return timeToClosest+0.25f*timeToGap;
    }

    static void WriteBridgeObservation(Player p, string state)
    {
        if(bridgeObsWriter==null)
        {
            bridgeObsWriter=new System.IO.StreamWriter(
                new FileStream(bridgeBase+".obs.jsonl",FileMode.Append,FileAccess.Write,
                    FileShare.Read),new UTF8Encoding(false));
            bridgeObsWriter.AutoFlush=true;
        }
        NPC boss=null;
        foreach(var npc in Game.npc)
        {
            if(npc!=null && npc.active && IsExpectedRoot(npc)) { boss=npc; break; }
        }
        var playerCenter=p.Center;
        // Nearest hostile projectiles, bounded so the line stays cheap to
        // write and to parse even when the screen is full.
        var nearest=new List<Projectile>(24);
        foreach(var shot in Game.projectile)
        {
            if(shot==null || !shot.active || shot.friendly) continue;
            nearest.Add(shot);
        }
        // Threat-density aggregates over the FULL hostile set, not the truncated
        // window. The slot list is capped, so a policy reading only the slots
        // goes blind exactly when the screen is fullest; these counts are
        // cumulative within 200/400/800 px and are computed from the same
        // distance the sort uses (Manhattan). The owner warned that the Empress
        // can put well over a hundred projectiles up, and the measured per-tick
        // maximum here is 44, so a fixed slot count alone has no margin: the
        // aggregates stay meaningful however far past the cap the real count goes.
        //
        // These are computed BEFORE the ordering and the type-collapse below,
        // because they are documented as counts over the whole hostile set and
        // collapsing would silently shrink them.
        int hostileTotal=nearest.Count;
        int near200=0,near400=0,near800=0;
        foreach(var shot in nearest)
        {
            float d=Math.Abs(shot.Center.X-playerCenter.X)+Math.Abs(shot.Center.Y-playerCenter.Y);
            if(d<200f) near200++;
            if(d<400f) near400++;
            if(d<800f) near800++;
        }
        // Ordering. Manhattan distance is the historical default and is what
        // every recorded stream and every existing checkpoint was built with.
        // `threat` replaces it with an estimate of how soon the projectile can
        // reach the player, because distance alone ranks a large slow hitbox
        // sitting next to the player above a fast one that is about to arrive.
        // See ThreatScore for the formula.
        nearest.Sort((a,b)=>
        {
            if(!projectileSortByThreat)
            {
                float da=Math.Abs(a.Center.X-playerCenter.X)+Math.Abs(a.Center.Y-playerCenter.Y);
                float db=Math.Abs(b.Center.X-playerCenter.X)+Math.Abs(b.Center.Y-playerCenter.Y);
                return da.CompareTo(db);
            }
            return ThreatScore(a,playerCenter).CompareTo(ThreatScore(b,playerCenter));
        });
        // Membership. Twelve slots filled with twelve copies of one hitbox tell
        // the policy nothing that one slot does not, and the measured stream
        // shows exactly that: type 384 alone took 2.48M slot appearances. This
        // keeps the nearest instance of each type and counts the rest, so the
        // truncation is reported in `pe` instead of being silent.
        int collapsedOut=0;
        if(projectileCollapseTypes)
        {
            var seenType=new Dictionary<int,bool>();
            var kept=new List<Projectile>(nearest.Count);
            foreach(var shot in nearest)
            {
                if(seenType.ContainsKey(shot.type)) { collapsedOut++; continue; }
                seenType[shot.type]=true;
                kept.Add(shot);
            }
            nearest=kept;
        }
        var projectileRows=new List<object>(Math.Min(nearest.Count,projectileSlots));
        for(int i=0;i<nearest.Count && i<projectileSlots;i++)
        {
            var shot=nearest[i];
            projectileRows.Add(new Dictionary<string,object>
            {
                {"x",shot.position.X},{"y",shot.position.Y},{"vx",shot.velocity.X},{"vy",shot.velocity.Y},
                {"w",shot.width},{"h",shot.height},{"ty",shot.type}
            });
        }
        // NPC-class threats (the Fishron bubbles and the Sharknado column are
        // NPCs, not projectiles -- see the note above CaptureHostileProjectiles).
        // The projectile window below therefore never contained them, which made
        // the policy blind to Fishron's primary threat. The classification here
        // mirrors the boss-state sampler's (L838-850): an enemy is neither
        // friendly (which covers the player's own minions), nor a town NPC, nor a
        // critter. Diagnostic keys only -- chaite_env builds its vector from
        // named keys, so adding them does not change OBS_DIM by itself.
        int npcNear200=0, npcNear400=0, npcNear800=0;
        float npcThreatDistance=float.MaxValue;
        float npcThreatRelX=0f, npcThreatRelY=0f, npcThreatVX=0f, npcThreatVY=0f;
        int npcThreatType=-1, npcThreatLife=0;
        float npcThreatWidth=0f, npcThreatHeight=0f;
        for(int i=0;i<Main.npc.Length;i++)
        {
            NPC npc=Main.npc[i];
            if(npc==null || !npc.active || npc.boss) continue;
            if(npc.friendly || npc.townNPC || npc.CountsAsACritter) continue;
            float ndx=npc.Center.X-p.Center.X, ndy=npc.Center.Y-p.Center.Y;
            float nd=(float)Math.Sqrt(ndx*ndx+ndy*ndy);
            if(nd<200f) npcNear200++;
            if(nd<400f) npcNear400++;
            if(nd<800f) npcNear800++;
            if(nd<npcThreatDistance)
            {
                npcThreatDistance=nd;
                npcThreatRelX=ndx; npcThreatRelY=ndy;
                npcThreatVX=npc.velocity.X; npcThreatVY=npc.velocity.Y;
                npcThreatType=npc.type; npcThreatLife=npc.life;
                npcThreatWidth=npc.width; npcThreatHeight=npc.height;
            }
        }
        bool terminal=state=="SuccessNoDeath" || state=="SuccessAfterDeath" ||
            state=="FailedAfterDeath" || state=="EncounterInterrupted" || state=="Cancelled";
        var row=new Dictionary<string,object>
        {
            {"schema","chaite-bridge-obs/v1"},
            {"e",episodeIndex},{"t",ticks},{"et",ticks-episodeStartTick},
            {"hits",episodeHits},{"pl",p.statLife},{"plm",p.statLifeMax2},
            {"px",p.position.X},{"py",p.position.Y},{"vx",p.velocity.X},{"vy",p.velocity.Y},
            {"wt",p.wingTime},{"wm",p.wingTimeMax},{"dd",p.dashDelay},{"eo",p.eocDash},{"jt",p.jump},
            {"dead",p.dead},
            {"ma",p.mount.Active},{"mt",p.mount.Active?p.mount.Type:-1},
            {"bl",boss!=null?boss.life:0},{"blm",boss!=null?boss.lifeMax:0},
            {"bx",boss!=null?boss.position.X:0f},{"by",boss!=null?boss.position.Y:0f},
            {"bvx",boss!=null?boss.velocity.X:0f},{"bvy",boss!=null?boss.velocity.Y:0f},
            {"bs",boss!=null?boss.ai[0]:-1f},{"bi",boss!=null?boss.ai[1]:0f},
            // The Boss's hitbox. MEASURED 2026-09-21: contact with the Boss body
            // is the dominant damage source -- at hit frames the gap to the Boss
            // centre is p50 104 px and 48.8% of hits land within 100 px, while
            // P(this tick is a hit) falls from 4.80% inside 50 px to 0.04% beyond
            // 200 px. The policy could see the Boss position but not its extent,
            // so it could not turn a centre distance into the surface gap that
            // decides contact. `bw`/`bh` are the missing half of that quantity;
            // zero when no Boss is alive, so the trainer can tell "absent" from
            // "present and tiny".
            {"bw",boss!=null?boss.width:0},{"bh",boss!=null?boss.height:0},
            // The Boss's own attack clock, and the two fields the reviewed
            // formula scripts branch on. FormulaScriptController reads Duke
            // Fishron's ai[0] as the state, ai[2] as the timer and ai[3] as the
            // sequence; FishronFormulaStateContract.TimerLimit turns
            // (state, sequence, difficulty, enraged) into the exact tick the
            // state ends on, and the sequence selects which attack starts next
            // (state 2 emits 20 Detonating Bubbles, state 3 drops Sharknado
            // projectiles, state 7 circles, state 8 drops the Cthulhunado).
            // Without them the policy can only guess how far into an attack the
            // Boss is. -1 when no Boss is alive, exactly like `bs`, so the
            // trainer's (value + 1) scaling puts "absent" at zero.
            {"bs2",boss!=null?boss.ai[2]:-1f},{"bs3",boss!=null?boss.ai[3]:-1f},
            // The multi-jump charges, one key per balloon type, in the assembly's
            // own field order. The audit flagged this as the last unobservable
            // mobility resource: the Lilith's Wolf loadout is the only one that
            // carries the Bundle of Balloons (1164), and its whole mobility is
            // the multi-jump, so a policy that cannot read how many are left
            // cannot decide whether to spend one. `jt` (p.jump) is the jump
            // hold counter and does NOT carry the charge count -- measured in
            // current-focus C122: a held jump reaches jt 0-5 with wingTime
            // untouched, a tapped one reaches jt 11-18 with wingTime spent, so
            // the two are distinguishable by their consequences but the
            // remaining charges are not readable at all.
            //
            // Nine keys rather than one sum: MEASURED on a recorded lilith-wolf
            // stream (254,957 rows, balloons equipped throughout), the Bundle of
            // Balloons (1164) sets jc0, jc1 AND jc2 -- cloud, sandstorm and
            // blizzard -- on every row, and leaves jc3..jc8 false. A single
            // count would report "3" and leave the policy unable to tell which
            // jump it is about to spend; the nine booleans cost nothing next to
            // a 121-wide vector.
            {"jc0",p.canJumpAgain_Cloud},
            {"jc1",p.canJumpAgain_Sandstorm},
            {"jc2",p.canJumpAgain_Blizzard},
            {"jc3",p.canJumpAgain_Fart},
            {"jc4",p.canJumpAgain_Sail},
            {"jc5",p.canJumpAgain_Basilisk},
            {"jc6",p.canJumpAgain_Santank},
            {"jc7",p.canJumpAgain_Unicorn},
            {"jc8",p.canJumpAgain_WallOfFleshGoat},
            {"pr",projectileRows},
            // Diagnostic only: the TRUE hostile projectile count, before the
            // 12-slot truncation above. The observation's projectile window is
            // capped at 12 and nothing recorded how often that cap binds, so the
            // policy's blindness to the rest could not be sized. chaite_env
            // builds its feature vector from named keys, so an extra key here
            // does not change OBS_DIM or the observation.
            {"pc",hostileTotal},
            {"p2",near200},{"p4",near400},{"p8",near800},
            // How many hostiles the type-collapse removed from the window, and
            // which ordering the window is in. Both are diagnostic only: the
            // trainer reads named keys, so neither changes OBS_DIM. `pe` exists
            // so the omission cannot be silent the way the old 12-slot cap was.
            {"pe",collapsedOut},
            {"ps",projectileSortByThreat?"threat":"manhattan"},
            // NPC-class threats: counts plus the nearest one's full state, so the
            // policy can actually see Fishron's Detonating Bubbles and the
            // Sharknado column (NPCs, absent from the projectile window).
            {"nt2",npcNear200},{"nt4",npcNear400},{"nt8",npcNear800},
            {"nrx",npcThreatDistance<float.MaxValue?npcThreatRelX:0f},
            {"nry",npcThreatDistance<float.MaxValue?npcThreatRelY:0f},
            {"nrvx",npcThreatDistance<float.MaxValue?npcThreatVX:0f},
            {"nrvy",npcThreatDistance<float.MaxValue?npcThreatVY:0f},
            {"nrt",npcThreatType},{"nrl",npcThreatLife},
            {"nrw",npcThreatWidth},{"nrh",npcThreatHeight},
            {"done",terminal || p.dead},{"win",state=="SuccessNoDeath"||state=="SuccessAfterDeath"},
            // The terminal STATE and the episode's own tick budget. MEASURED
            // 2026-09-21: 13-26% of fights end with the plugin's safety abort
            // ("safe conditions persistently lost"), which the trainer saw as
            // done=true, win=false, dead=false -- exactly the shape of a genuine
            // tick-limit timeout -- and therefore charged the timeout penalty for.
            // Those episodes are not timeouts: they end when Fishron's native AI
            // leaves the reviewed formula table, which the measured stream shows
            // happens with the Boss at 60-100% damage, i.e. while the policy is
            // winning. Charging a penalty there teaches the policy to avoid the
            // endgame. With the state and the budget in the row the trainer can
            // tell the two apart and stop punishing a truncation it did not cause.
            {"st",state},{"etl",ticks-episodeStartTick},{"tlim",tickLimit}
        };
        try { bridgeObsWriter.WriteLine(Json(row)); }
        catch(Exception error) { Log("BRIDGE_OBS_FAILED "+error.Message); }
    }

    static void WriteResult(string status,string outcome,bool win,int exitCode,string failure)
    {
        if(IsScopeNegative) { WriteScopeNegativeResult(status,outcome,win,exitCode,failure); return; }
        if(IsFlight) { WriteFlightResult(status,exitCode,failure); return; }
        if(IsMotion) { WriteMotionResult(status,exitCode,failure); return; }
        DrainHurtObservations();
        FlushHurtObservations();
        CloseChargeObservation(true);
        FlushChargeObservations();
        FlushPreHitWindow();
        CaptureBattleObservation(true);
        FlushBattleObservations();
        bool expectedSeen=scenario!=null && scenario.BossTypes!=null && expectedBossMask==((1<<scenario.BossTypes.Length)-1);
        var unexpected=new int[unexpectedBossTypes.Count]; unexpectedBossTypes.CopyTo(unexpected); Array.Sort(unexpected);
        var variantEvidence=BuildVariantEvidence(expectedSeen);
        string reportedVariant=(string)variantEvidence["reportedVariant"];
        // Direct/staged priority cases are deliberately valuable regression
        // fixtures, but are not organic encounter samples. Keep their native
        // result separate from a campaign win rate all the way into readiness
        // evaluation.
        bool stagedPhaseFixture=scenario!=null && scenario.DirectSpawn;
        string evidenceKind=IsMonitorFixture?"native-monitor-arrival-fixture":stagedPhaseFixture?"staged-native-phase-regression":"isolated-native-encounter";
        bool readinessEligible=!stagedPhaseFixture;
        int reportedBossLife;
        var bossLifeObservation=BuildBossLifeObservation(win,out reportedBossLife);
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-boss-result/v1"},{"schemaVersion",1},{"scenario",scenario==null?null:scenario.Id},
            {"seed",seed},{"difficulty",difficulty},{"difficultyCode",difficultyCode},{"variant",reportedVariant},
            {"formulaRoute",formulaRoute},
            {"observedFormulaRoute",observedFormulaRoute},{"formulaRouteMismatches",formulaRouteMismatches},
            {"variantEvidence",variantEvidence},{"evidenceKind",evidenceKind},{"readinessEligible",readinessEligible},{"status",status},
            {"requestedPhase",requestedPhase},{"requestedTakeoverTick",takeoverTick},{"actualTakeoverTick",actualTakeoverTick},
            {"directSpawn",scenario!=null && scenario.DirectSpawn},{"directSpawnTick",directSpawnTick},
            {"directSpawnAttempted",directSpawnAttempted},{"directSpawnCompleted",directSpawnCompleted},
            {"monitorArmedTick",monitorArmedTick},{"monitorCombatTick",monitorCombatTick},{"monitorPassiveFrames",monitorPassiveFrames},
            {"shieldTraceRows",shieldTraceRows},{"shieldTraceDropped",shieldTraceDropped},
            {"phaseStageAttempted",phaseStageAttempted},{"phaseStaged",phaseStaged},
            {"phaseVerifiedAtTakeover",phaseVerifiedAtTakeover},{"phaseStage",phaseStageReport},
            {"takeoverNativeSnapshot",takeoverNativeSnapshot},{"encounterFixtureReady",EncounterFixtureReady()},
            {"processExitCode",exitCode},{"outcome",outcome},{"win",win},{"failure",failure},
            {"validBattle",status!="harness-error" && status!="rejected" &&
                expectedSeen && EncounterFixtureReady() && nativeFrames>120 &&
                nativeDifficultyVerified && battleRandomInstalled},
            {"battleStarted",expectedBossMask!=0},{"allExpectedBossesSeen",expectedSeen},
            {"death",deaths>0},{"deaths",deaths},{"hits",hits},{"ticks",ticks},{"nativeFrames",nativeFrames},
            {"minLife",minLife==int.MaxValue?0:minLife},{"bossDamage",bossDamage},{"bossLifeRemaining",reportedBossLife},
            {"bossLifeObservation",bossLifeObservation},
            {"maxGrappleTicks",maximumGrappleTicks},{"maximumShots",maximumShots},
            {"strayNpcsRetired",strayNpcsRetired},
            {"summonConsumed",sawSummonConsumed},{"bossDamaged",sawBossDamage},{"playerMoved",sawMovement},
            {"unexpectedBossTypes",unexpected},{"equipment",equipmentReport},
            {"nativeDifficultyVerified",nativeDifficultyVerified},{"nativeDifficulty",nativeDifficultyReport},
            {"firstObservedBosses",firstObservedBosses},
            {"diagnostics",BattleObservationReport()},
            {"battleRandom",new Dictionary<string,object>
                {
                    {"installedAfterSetup",battleRandomInstalled},{"seed",seed},
                    {"independentTwinFingerprint",battleRandomFingerprint},{"referenceChecks",battleRandomReferenceChecks},
                    {"actualAndNativeNamedColdStateVerified",battleRandomColdStateVerified},
                    {"unpausedUpdateSeedInitial",initialUnpausedUpdateSeed},{"unpausedUpdateSeedFinal",expectedUnpausedUpdateSeed},
                    {"unpausedUpdateSeedAdvances",unpausedUpdateSeedAdvances},
                    {"unpausedUpdateSeedPolicy","initial = zero-extended declared numeric seed; native Utils.RandomNextSeed once before each DoUpdateInWorld, matching native DoUpdate order"},
                    {"actualStreamConsumedForFingerprint",false},{"namedStreams","fresh native registry; Main.SwapRandom derives streams from verified WorldFileData.Seed"}
                }},
            {"arena",new Dictionary<string,object>
                {
                    {"kind","in-memory hand-built fixture; not a generated/saved user world"},
                    {"worldWidthTiles",4200},{"worldHeightTiles",1200},{"groundLeft",ArenaGroundLeft},{"groundRightExclusive",ArenaGroundRightExclusive},
                    // Published because it is a second, wider number that builds
                    // real tiles: without it a reader comparing the published
                    // arena to the map would find floor where the arena says
                    // there is none, which is exactly the kind of drift the
                    // platform-row note below was written about.
                    {"safetyFloorRightExclusive",scenario!=null && scenario.Ocean?SafetyFloorRightExclusive:ArenaGroundRightExclusive},
                    {"oceanBasinFilled",scenario!=null && scenario.Ocean && !IsMonitorFixture},
                    {"groundTop",scenario!=null && scenario.Underworld?Game.maxTilesY-140:scenario!=null && scenario.Jungle?700:500},{"groundThickness",scenario!=null && scenario.Snow?12:6},
                    {"groundTile",scenario!=null && scenario.Hallow?"Pearlstone":scenario!=null && scenario.Jungle?"JungleGrass":scenario!=null && scenario.Snow?"IceBlock":"GrayBrick"},
                    // Published from the same expression that builds the tiles,
                    // so the two cannot disagree again. platformTileCount is the
                    // in-engine control: it counts the platform tiles still
                    // present in the world, so a block that claims rows while the
                    // world has none is caught by reading the evidence.
                    {"platformRows",arenaPlatformRows},
                    {"platformRowSpacingTiles",PlatformRowSpacingTiles},
                    {"platformTileCount",arenaPlatformTileCount},
                    {"startSide",scenario!=null && scenario.Ocean?requestedStartSide:"centre"},
                    {"playerStartTileX",scenario!=null?PlayerStartTileX(scenario.Ocean?200:2100):0},
                    {"arenaShape",scenario!=null && scenario.Ocean && IsMonitorFixture
                        ? "three horizontal layers including the ground, 60 tiles apart: the ground plus two wooden-platform rows spanning the arena's own horizontal bounds. The strong wing fights the same layered arena; its flat-ground behaviour is unchanged because it does not need to land"
                        : "one long straight flat ground; no platform rows are built for this fixture"},
                    {"startingDay",scenario!=null && scenario.Daytime},{"startingWorldTime",scenario!=null && scenario.Daytime?27000:1000},
                    {"sceneMetricsPolicy","headless: native Player.UpdateSceneMetrics each tick after frame counter advance; native Player.Update transfers biome state; no forced Zone flags"},
                    {"nativeSceneMetricRefreshes",nativeSceneMetricRefreshes}
                }},
            {"elapsedMs",processClock.Elapsed.TotalMilliseconds},{"combatLoopElapsedMs",clock.Elapsed.TotalMilliseconds},
            {"limits",new Dictionary<string,object>{{"ticks",tickLimit},{"wallSeconds",wallLimitSeconds},{"runTicks",runTickLimit}}},
            {"runtime",TimingSummary(runtimeSamples,"production snapshot+plan+capture; excludes vanilla update/render/input presentation")},
            {"nativeUpdate",TimingSummary(engineSamples,"native scene-metric refresh plus headless DoUpdateInWorld including production plugin; excludes harness tracking and Sleep")},
            {"randomScope","after all setup, seed installs Main.rand and a fresh native named-stream registry; verified WorldFileData.Seed drives Main.SwapRandom; Main.UnpausedUpdateSeed deterministically initialized and advanced by native Utils.RandomNextSeed each frame; cold states verified without consumption and references checked at every native-frame boundary; WorldGen.genRand and other thread/local/wall-time sources are not controlled; arena is not world-generated"},
            {"scope","one process, one fixed-gear arena fixture, one synthetic F8 edge; Deerclops/Queen Bee summon phases use the production hotbar-item path and native item consumption; reviewed direct phases use native spawn plus explicitly disclosed test-only phase fields; native AI/damage/physics then continue; headless dedicated presentation branches, not a rendered client"},
            {"hitsDefinition","native update frames with decreased player life, including environmental damage; not a damage-event hook"},
            {"poisonedFramesDefinition","native battle-update frames whose post-update local Player.poisoned field is true; read-only and not a claim that poison caused damage"},
            {"bossDamageDefinition","observed expected boss-root life decreases including observed final deaths; shared worm body segments excluded"},
            {"bossLifeRemainingDefinition","confirmed victory reports zero; otherwise the last frame containing active expected root type/slot identities is retained across despawn and slot reuse"},
            {"createdUtc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)}
        };
        File.WriteAllText(Path.Combine(Root,"result.json"),Json(result),new UTF8Encoding(false));
    }

    static void WriteScopeNegativeResult(string status,string outcome,bool passed,int exitCode,string failure)
    {
        var p=Game.player!=null && Game.player.Length>0?Game.player[0]:null;
        int finalSummonType=p==null?-1:p.inventory[1].type;
        int finalSummonStack=p==null?-1:p.inventory[1].stack;
        int finalAmmoType=p==null?-1:p.inventory[54].type;
        int finalAmmoStack=p==null?-1:p.inventory[54].stack;
        var controls=new Dictionary<string,object>
        {
            {"left",scopeNegativeLeftFrames},{"right",scopeNegativeRightFrames},
            {"up",scopeNegativeUpFrames},{"down",scopeNegativeDownFrames},
            {"jump",scopeNegativeJumpFrames},{"hook",scopeNegativeHookFrames},
            {"dash",scopeNegativeDashFrames},{"mount",scopeNegativeMountFrames},
            {"useItem",scopeNegativeUseItemFrames},{"useTile",scopeNegativeUseTileFrames},
            {"throw",scopeNegativeThrowFrames},{"quickHeal",scopeNegativeQuickHealFrames},
            {"quickMana",scopeNegativeQuickManaFrames}
        };
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-boss-scope-negative-result/v1"},{"schemaVersion",1},
            {"scenario",scenario==null?null:scenario.Id},{"seed",seed},{"difficulty",difficulty},
            {"status",status},{"processExitCode",exitCode},{"outcome",outcome},{"passed",passed},{"failure",failure},
            {"evidenceKind","isolated-native-admission-negative"},{"readinessEligible",false},
            {"requestedTakeoverTick",takeoverTick},{"actualTakeoverTick",actualTakeoverTick},
            {"activationCaptured",scopeNegativeActivationCaptured},{"activationTick",scopeNegativeActivationTick},
            {"nativeFrames",nativeFrames},{"observationFrames",scopeNegativeActivationCaptured?ticks-scopeNegativeActivationTick+1:0},
            {"expectedRootTypes",scenario==null?null:scenario.ScopeNegativeRootTypes},
            {"rootsAtActivation",scopeNegativeRootsAtActivation.ToArray()},{"topologyMatches",scopeNegativeTopologyMatches},
            {"spawn",new Dictionary<string,object>
                {
                    {"required",scenario!=null && scenario.ScopeNegativeRootTypes.Length>0},
                    {"tick",directSpawnTick},{"attempted",directSpawnAttempted},{"completed",directSpawnCompleted}
                }},
            {"rejection",new Dictionary<string,object>
                {
                    {"kind","UnsupportedBoss"},{"expectedCue","UnsupportedBoss"},
                    {"allCueCalls",scopeNegativeCueCalls},{"unsupportedBossCueCalls",scopeNegativeUnsupportedCueCalls},
                    {"expectedMessage",Chaite.Core.SupportedBossPolicy.UnsupportedBossMessage},
                    {"expectedNativeChat","[\u62C6\u7279] "+Chaite.Core.SupportedBossPolicy.UnsupportedBossMessage},
                    {"allChatCalls",scopeNegativeChatCalls},{"exactMessageCalls",scopeNegativeExactChatCalls},
                    {"messages",scopeNegativeChatMessages.ToArray()},{"sessionBefore",scopeNegativeSessionBefore},
                    {"sessionAfter",scopeNegativeSessionAfter},{"unexpectedSessionStateFrames",scopeNegativeSessionUnexpectedFrames}
                }},
            {"summon",new Dictionary<string,object>
                {
                    {"slot",1},{"expectedType",scenario==null?0:scenario.Summon},
                    {"beforeType",scopeNegativeSummonBeforeType},{"beforeStack",scopeNegativeSummonBeforeStack},
                    {"afterType",finalSummonType},{"afterStack",finalSummonStack},
                    {"consumed",finalSummonType!=scopeNegativeSummonBeforeType || finalSummonStack<scopeNegativeSummonBeforeStack}
                }},
            {"sideEffects",new Dictionary<string,object>
                {
                    {"controlFrameCounts",controls},{"actionableControlFrames",scopeNegativeActionableControlFrames},
                    {"applyPlanCalls",observedPlanCalls},{"applyPlanReturns",observedPlanReturns},
                    {"unpairedApplyPlanObservations",observedPlanUnpaired},{"applyPlanPending",observedPlanPending},
                    {"selectedItemBefore",scopeNegativeSelectedBefore},{"selectedItemAfter",p==null?-1:p.selectedItem},
                    {"selectedItemChangeFrames",scopeNegativeSelectedChangeFrames},{"itemUseFrames",scopeNegativeItemUseFrames},
                    {"summonSlotChangeFrames",scopeNegativeSummonChangeFrames},
                    {"ammoBeforeType",scopeNegativeAmmoBeforeType},{"ammoBeforeStack",scopeNegativeAmmoBeforeStack},
                    {"ammoAfterType",finalAmmoType},{"ammoAfterStack",finalAmmoStack},{"ammoChangeFrames",scopeNegativeAmmoChangeFrames},
                    {"ownedFriendlyProjectileObservations",scopeNegativeOwnedFriendlyProjectileObservations},
                    {"maximumPositionDeltaSquared",scopeNegativeMaximumPositionDeltaSquared},
                    {"lifeLossFrames",scopeNegativeLifeLossFrames},{"deaths",deaths}
                }},
            {"nativeDifficultyVerified",nativeDifficultyVerified},{"nativeDifficulty",nativeDifficultyReport},
            {"limits",new Dictionary<string,object>{{"ticks",tickLimit},{"wallSeconds",wallLimitSeconds},{"runTicks",runTickLimit}}},
            {"scope","fixed vanilla-engine admission rejection fixture; one synthetic F8 edge; exact native Boss-root topology captured before production admission; one post-activation native frame; no readiness or win-rate claim"},
            {"createdUtc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)}
        };
        File.WriteAllText(Path.Combine(Root,"result.json"),Json(result),new UTF8Encoding(false));
    }

    static Dictionary<string,object> TimingSummary(List<double> samples,string scope)
    {
        var sorted=samples.ToArray(); Array.Sort(sorted);
        double sum=0; foreach(double value in sorted) sum+=value;
        return new Dictionary<string,object>
        {
            {"frames",sorted.Length},{"meanMs",sorted.Length==0?0:sum/sorted.Length},
            {"p50Ms",Percentile(sorted,.50)},{"p95Ms",Percentile(sorted,.95)},
            {"p99Ms",Percentile(sorted,.99)},{"maxMs",sorted.Length==0?0:sorted[sorted.Length-1]},
            {"scope",scope}
        };
    }

    static double Percentile(double[] sorted,double fraction)
    { return sorted.Length==0?0:sorted[Math.Max(0,(int)Math.Ceiling(sorted.Length*fraction)-1)]; }

    static string Json(object value)
    {
        if(value==null) return "null";
        if(value is string)
        {
            var text=(string)value;
            var builder=new StringBuilder("\"");
            foreach(char c in text)
            {
                if(c=='"' || c=='\\') builder.Append('\\').Append(c);
                else if(c<32) builder.Append("\\u").Append(((int)c).ToString("x4",CultureInfo.InvariantCulture));
                else builder.Append(c);
            }
            return builder.Append('"').ToString();
        }
        if(value is bool) return (bool)value?"true":"false";
        var dictionary=value as IDictionary<string,object>;
        if(dictionary!=null)
        {
            var builder=new StringBuilder("{"); bool first=true;
            foreach(var pair in dictionary)
            { if(!first) builder.Append(','); first=false; builder.Append(Json(pair.Key)).Append(':').Append(Json(pair.Value)); }
            return builder.Append('}').ToString();
        }
        var sequence=value as System.Collections.IEnumerable;
        if(sequence!=null)
        {
            var builder=new StringBuilder("["); bool first=true;
            foreach(var item in sequence)
            { if(!first) builder.Append(','); first=false; builder.Append(Json(item)); }
            return builder.Append(']').ToString();
        }
        // General-format Single output keeps only seven significant digits and
        // can lose subpixel coordinates (e.g. 7957.760742 -> 7957.761). The
        // motion oracle needs the ORIGINAL native IEEE values, not rounded G7.
        if(value is float)
        {
            float number=(float)value;
            return float.IsNaN(number)||float.IsInfinity(number)?"null":number.ToString("R",CultureInfo.InvariantCulture);
        }
        if(value is double)
        {
            double number=(double)value;
            return double.IsNaN(number)||double.IsInfinity(number)?"null":number.ToString("R",CultureInfo.InvariantCulture);
        }
        return Convert.ToString(value,CultureInfo.InvariantCulture);
    }
}
