using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    static readonly Stopwatch processClock = Stopwatch.StartNew();
    static ScenarioSpec scenario;
    static bool settingsRead, finishing;
    static int seed = 20260910, difficultyCode, tickLimit = 24000, wallLimitSeconds = 90;
    static int takeoverTick = 120, directSpawnTick = -1;
    static string difficulty = "classic";
    static string requestedPhase = "summon";
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
    static Chaite.Core.ControlPlan observedPlan;
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
        if(!hasObservedPlan || !String.Equals(observedPlan.StrategyId,plan.StrategyId,StringComparison.Ordinal) ||
            !String.Equals(observedPlan.PhaseId,plan.PhaseId,StringComparison.Ordinal)) MarkBattleObservation(2);
        if(!hasObservedPlan || observedPlan.TargetKey!=plan.TargetKey || observedPlanTarget.Exists!=planTarget.Exists ||
            (planTarget.Exists && (observedPlanTarget.Type!=planTarget.Type || observedPlanTarget.Active!=planTarget.Active)))
            MarkBattleObservation(4);
        observedPlan=plan; // ControlPlan is a value type; no game/plan writes.
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
        if(nativePhaseKey!=battlePreviousNativePhaseKey) MarkBattleObservation(8);
        battlePreviousNativePhaseKey=nativePhaseKey;
        CaptureBattleObservation(false);
    }
    static void CaptureBattleObservation(bool final)
    {
        if(!booted || !IsBattleObservation || Game.player==null || Game.player.Length==0 || Game.player[0]==null || Game.npc==null) return;
        bool periodic=ticks%BattleObservationInterval==0;
        if(final) battleObservationReasons|=32;
        if(!final && !periodic && (battleObservationReasons==0 || ticks-battleObservationLastTick<BattleObservationEdgeInterval)) return;
        // Reserve one row for the terminal state. A final row may intentionally
        // share a tick with the preceding edge/periodic row; it proves the exact
        // state passed to WriteResult rather than silently losing termination.
        if(!final && ticks==battleObservationLastTick) return;
        if(battleObservationRows>=BattleObservationMaximumRows || (!final && battleObservationRows>=BattleObservationMaximumRows-1))
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
        Dictionary<string,object> plan=null,actual=null;
        if(hasObservedPlan)
            plan=new Dictionary<string,object>
            {
                {"tick",observedPlanTick},{"strategy",observedPlan.StrategyId},{"phase",observedPlan.PhaseId},
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
                {"gravityControl",observedPlan.GravityControl},{"featherFallUp",observedPlan.FeatherFallUp},
                {"preferredWeaponSlot",observedPlan.PreferredWeaponSlot},
                {"aim",new Dictionary<string,object>{{"x",observedPlan.AimWorld.X},{"y",observedPlan.AimWorld.Y}}},
                {"hookAim",new Dictionary<string,object>{{"x",observedPlan.HookWorld.X},{"y",observedPlan.HookWorld.Y}}},
                {"riskScore",observedPlan.RiskScore},{"tacticalMode",observedPlan.TacticalMode.ToString()},{"weaponIssue",observedPlan.WeaponIssue}
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
        int reasonMask=battleObservationReasons|(periodic?1:0);
        var row=new Dictionary<string,object>
        {
            {"schema","chaite-boss-observation/v1"},{"tick",ticks},{"nativeFrames",nativeFrames},
            {"reasonMask",reasonMask},{"transitionsSincePreviousRow",battleObservationPendingTransitions},
            {"sessionState",SessionState()},{"plan",plan},{"actualAtApplyReturn",actual},{"applyPending",observedPlanPending},
            {"applyCalls",observedPlanCalls},{"applyReturns",observedPlanReturns},
             {"player",new Dictionary<string,object>
                 {
                     {"position",new Dictionary<string,object>{{"x",p.position.X},{"y",p.position.Y}}},
                     {"velocity",new Dictionary<string,object>{{"x",p.velocity.X},{"y",p.velocity.Y}}},
                     {"life",p.statLife},{"dead",p.dead},{"wingTime",p.wingTime},{"wingTimeMax",p.wingTimeMax},
                     {"poisoned",p.poisoned},
                     {"wingsLogic",p.wingsLogic},{"grapCount",p.grapCount},{"controlUseItem",p.controlUseItem},
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
            {"npcs",npcs},{"omittedNpcs",omitted}
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
            if(key!="-savedirectory" && key!="-skipbeam" && key!="-scenario" && key!="-seed" &&
                key!="-difficulty" && key!="-maxticks" && key!="-wallseconds" && key!="-motioncase" && key!="-flightcase" &&
                key!="-phase" && key!="-takeovertick")
                throw new ArgumentException("Unsupported probe argument: "+key);
        string value;
        if(Terraria.Program.LaunchParameters.TryGetValue("-seed",out value))
            seed=BoundedInt(value,0,int.MaxValue,"seed");
        if(Terraria.Program.LaunchParameters.TryGetValue("-maxticks",out value))
            tickLimit=BoundedInt(value,600,24000,"maxticks");
        if(Terraria.Program.LaunchParameters.TryGetValue("-wallseconds",out value))
            wallLimitSeconds=BoundedInt(value,15,900,"wallseconds");
        if(Terraria.Program.LaunchParameters.TryGetValue("-takeovertick",out value))
            takeoverTick=BoundedInt(value,120,23880,"takeovertick");
        if(Terraria.Program.LaunchParameters.TryGetValue("-phase",out value)) requestedPhase=value.ToLowerInvariant();
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
                DirectScenario(370,new[]{370},true,new[]{"spawn-fade","spawn-emerge","p1-hover","p1-dash","p1-bubbles","p1-sharknado","p2-transition-fade","p2-transition-emerge","p2-hover","p2-dash","p2-bubbles","p2-sharknado","p3-transition-fade","p3-transition-hidden","p3-reposition","p3-dash","p3-teleport"}); scenario.Ocean=true; break;
            case "empress-night":
                DirectScenario(636,new[]{636},true,new[]{"p1-reposition","p1-bolts","p1-rainbow","p1-sun-dance","p1-dash","transition","p2-reposition","p2-lance-wall","p2-predictive-lances","p2-spiral"}); scenario.Hallow=true; break;
            case "empress-day":
                DirectScenario(636,new[]{636},true,new[]{"p1-reposition","p1-bolts","p1-rainbow","p1-sun-dance","p1-dash","transition","p2-reposition","p2-lance-wall","p2-predictive-lances","p2-spiral"});
                scenario.Daytime=true; scenario.Hallow=true; break;
            case "moon-lord":
                DirectScenario(398,new[]{396,397,398},true,new[]{"intro","synchronize-eyes","head-bolts","head-tongue","head-deathray-telegraph","left-sphere-release","right-sphere-release"});
                scenario.SpawnLeadTicks=90; break;
            case "scope-negative-unsupported-summon":
                ScopeNegativeScenario(ItemID.SuspiciousLookingEye); break;
            case "scope-negative-existing-unsupported":
                ScopeNegativeScenario(0,4); break;
            case "scope-negative-fishron-mixed":
                ScopeNegativeScenario(0,370,4); scenario.HardMode=true; scenario.Ocean=true; break;
            case "scope-negative-empress-mixed":
                ScopeNegativeScenario(0,636,4); scenario.HardMode=true; scenario.Hallow=true; break;
            case "scope-negative-fishron-duplicate":
                ScopeNegativeScenario(0,370,370); scenario.HardMode=true; scenario.Ocean=true; break;
            case "scope-negative-empress-duplicate":
                ScopeNegativeScenario(0,636,636); scenario.HardMode=true; scenario.Hallow=true; break;
            case "motion-jump": scenario.Motion=true; scenario.BossTypes=new int[0]; break;
            case "motion-flight": scenario.Motion=true; scenario.Flight=true; scenario.BossTypes=new int[0]; break;
            default: throw new ArgumentException("Unknown bounded scenario: "+id);
        }
        scenario.ExpectedVariant=ExpectedVariantForScenario(id);
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
            directSpawnTick=scenario.DirectSpawn?(IsScopeNegative?takeoverTick:Math.Max(1,takeoverTick-spawnLead)):-1;
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
                case "cloud-hold": case "cloud-tap": case "cloud-release-press": break;
                default: throw new ArgumentException("motion-jump requires one reviewed -motioncase");
            }
        }
        else if(motionCase!=null) throw new ArgumentException("-motioncase is valid only with motion-jump");
        if(!IsFlight && flightCase!=null) throw new ArgumentException("-flightcase is valid only with motion-flight");
        settingsRead=true;
    }

    static void DirectScenario(int spawnType,int[] bossTypes,bool hardMode,string[] phases)
    {
        scenario.DirectSpawn=true;
        scenario.DirectSpawnType=spawnType;
        scenario.BossTypes=bossTypes;
        scenario.HardMode=hardMode;
        scenario.Phases=phases;
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
            case "empress-night": return "night";
            case "empress-day": return "day";
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
            case "empress-night": case "empress-day":
                if(HasUnsupportedStandardWorldRule(observation)) return null;
                return observation.DayTime?"day":"night";
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
            case "duke-fishron":
                scenario.EquipmentTier="early-hardmode";
                break;
            case "empress-night": case "empress-day":
                scenario.EquipmentTier="post-plantera";
                scenario.MaxLife=500;
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
        if(booted && player.whoAmI==0 && scenario!=null && scenario.Id=="empress-day")
        {
            // Player.Update has already run its accessory functional pass and
            // reset the dash fields in the dedicated-server headless path.
            // Re-publish the reviewed Shield-of-Cthulhu edge before ApplyPlan
            // captures the mobility snapshot, so the dash controller has a
            // real native state to score this frame.
            player.dashType=2;
            player.dashDelay=0;
            player.dashTime=0;
        }
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

    public static void RunHeadless()
    {
        try
        {
            ReadSettings();
            Log("HEADLESS_BEGIN real vanilla assembly; no graphics, no network, no user saves");
            Log("CASE scenario="+scenario.Id+" seed="+seed+" difficulty="+difficulty+" maxTicks="+tickLimit+" wallSeconds="+wallLimitSeconds);
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
                    // Bound background CPU. Game mechanics still advance in native ticks;
                    // elapsed wall time is not an end-to-end latency benchmark here.
                    if((ticks&3)==0) System.Threading.Thread.Sleep(1);
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
            Log("EARLY vanilla=" + typeof(Game).Assembly.GetName().Version + " save=" + expected);
        }
        catch (Exception e) { Fail(e); }
    }

    public static void ContentReady()
    {
        try
        {
            ReadSettings();
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
            int arenaCenterX=scenario.Ocean?300:2100;
            int arenaGroundY=scenario.Underworld?Game.maxTilesY-140:scenario.Jungle?700:500;
            Game.spawnTileX = arenaCenterX;
            Game.spawnTileY = arenaGroundY-2;
            Game.worldName = "Chaite isolated engine test";
            Game.dayTime = scenario.Daytime;
            Game.time = scenario.Daytime?27000:1000;
            Game.hardMode = scenario.HardMode;
            Game.wofNPCIndex = -1;
            Game.netMode = 0;
            Game.myPlayer = 0;
            int groundLeft=scenario.Ocean?80:800;
            int groundRight=scenario.Ocean?1900:3400;
            int groundThickness=scenario.Snow?12:6;
            ushort groundType=scenario.Hallow?TileID.Pearlstone:scenario.Jungle?TileID.JungleGrass:
                scenario.Snow?TileID.IceBlock:TileID.GrayBrick;
            for (int x = groundLeft; x < groundRight; x++)
            for (int y = arenaGroundY; y < arenaGroundY+groundThickness; y++)
            {
                if (Game.tile[x,y] == null) Game.tile[x,y] = new Tile();
                Game.tile[x,y].active(true);
                Game.tile[x,y].type = groundType;
            }
            // Two ordinary, non-actuated wooden-platform rows. Priority Bosses
            // receive the disclosed 500-tile multi-row arena assumed by their
            // minimum-mobility contract. The legacy baseline and Wall runway
            // retain their historical geometry.
            if((scenario.HardMode || scenario.PriorityArena || scenario.DirectSpawn) && !scenario.Underworld)
                foreach(int y in new[]{arenaGroundY-40,arenaGroundY-80})
                    for(int x=arenaCenterX-250;x<arenaCenterX+250;x++)
                    {
                        Game.tile[x,y].active(true);
                        Game.tile[x,y].type=TileID.Platforms;
                        Game.tile[x,y].frameX=0; Game.tile[x,y].frameY=0;
                    }
            var player = new Player();
            player.name = "Chaite Lab";
            player.whoAmI = 0;
            player.active = true;
            player.statLifeMax = player.statLife = scenario.MaxLife;
            player.statManaMax = player.statMana = 200;
            player.position = new Vector2(arenaCenterX * 16, arenaGroundY * 16 - player.height);
            player.fallStart=player.fallStart2=(int)(player.position.Y/16);
            EquipScenario(player);
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
            else if(cloud) player.armor[3].SetDefaults(ItemID.CloudinaBottle);
            var equipped=new int[player.armor.Length];
            for(int i=0;i<equipped.Length;i++) equipped[i]=player.armor[i].type;
            scenario.Equipment=IsFlight?"flight: naked + unprefixed "+FlightProfile:
                cloud?"motion: naked + unprefixed Cloud in a Bottle only":"motion: naked, no accessories";
            equipmentReport=new Dictionary<string,object>
            {
                {"label",scenario.Equipment},{"life",400},{"mana",200},{"armorAndAccessories",equipped},
                {"cloudEquipped",cloud},{"cloudItemType",cloud?ItemID.CloudinaBottle:0},{"cloudPrefix",player.armor[IsFlight?5:3].prefix},
                {"noWeaponsAmmoConsumablesOrMount",true},{"noDirectJumpStateOverrides",true}
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

    // Synthetic edges ONLY in test copy's poller. No OS keys or mouse are sent.
    public static bool ActivateDown()
    {
        bool down=!IsMotion && booted && ticks==takeoverTick;
        if(down)
        {
            actualTakeoverTick=ticks;
            if(IsScopeNegative) CaptureScopeNegativeActivation();
            CaptureVariantAtActivation();
        }
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
            case "empress-night": case "empress-day": StageEmpress(root,mutation); break;
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

    // Complete root ai[0..3] tuples from AI_120's fixed attack tables.  The
    // selected attack increments ai[2], so these are post-selection indices.
    // Classic-night predictive lances are the legal day-table attack carried
    // across sunset (form 3), as covered by the native schedule regressions.
    static readonly Dictionary<string,int[]> EmpressClassicNightStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"p1-reposition",new[]{1,0,0,0}}, {"p1-bolts",new[]{2,1,1,0}},
        {"p1-rainbow",new[]{5,5,5,0}}, {"p1-sun-dance",new[]{6,3,3,0}},
        {"p1-dash",new[]{8,50,2,0}}, {"transition",new[]{10,20,1,0}},
        {"p2-reposition",new[]{1,0,0,1}}, {"p2-lance-wall",new[]{7,80,1,1}},
        {"p2-predictive-lances",new[]{11,40,4,3}}, {"p2-spiral",new[]{12,70,9,1}}
    };

    static readonly Dictionary<string,int[]> EmpressExpertNightStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"p1-reposition",new[]{1,0,0,0}}, {"p1-bolts",new[]{2,1,1,0}},
        {"p1-rainbow",new[]{5,5,5,0}}, {"p1-sun-dance",new[]{6,3,3,0}},
        {"p1-dash",new[]{8,50,2,0}}, {"transition",new[]{10,20,1,0}},
        {"p2-reposition",new[]{1,0,0,1}}, {"p2-lance-wall",new[]{7,80,1,1}},
        {"p2-predictive-lances",new[]{11,40,4,1}}, {"p2-spiral",new[]{12,70,10,1}}
    };

    static readonly Dictionary<string,int[]> EmpressDayStageTuples = new Dictionary<string,int[]>(StringComparer.Ordinal)
    {
        {"p1-reposition",new[]{1,0,0,2}}, {"p1-bolts",new[]{2,1,1,2}},
        {"p1-rainbow",new[]{5,5,5,2}}, {"p1-sun-dance",new[]{6,3,3,2}},
        {"p1-dash",new[]{8,50,2,2}}, {"transition",new[]{10,20,1,2}},
        {"p2-reposition",new[]{1,0,0,3}}, {"p2-lance-wall",new[]{7,80,1,3}},
        {"p2-predictive-lances",new[]{11,40,4,3}}, {"p2-spiral",new[]{12,70,10,3}}
    };

    static bool TryGetEmpressStageTuple(out int[] tuple)
    {
        if(scenario.Daytime)
            return EmpressDayStageTuples.TryGetValue(requestedPhase,out tuple);
        if(difficultyCode>0)
            return EmpressExpertNightStageTuples.TryGetValue(requestedPhase,out tuple);
        return EmpressClassicNightStageTuples.TryGetValue(requestedPhase,out tuple);
    }

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

    static void StageEmpress(NPC root,Dictionary<string,object> mutation)
    {
        int[] tuple;
        if(!TryGetEmpressStageTuple(out tuple))
            throw new InvalidOperationException("Missing Empress native phase tuple");
        bool second=requestedPhase=="transition"||requestedPhase.StartsWith("p2-",StringComparison.Ordinal);
        SetNativeAi(root,tuple[0],tuple[1],tuple[2],tuple[3]);
        SetLifeFraction(root,second?45:80,100);
        mutation["root.ai0"]=tuple[0];mutation["root.ai1"]=tuple[1];
        mutation["root.ai2"]=tuple[2];mutation["root.ai3"]=tuple[3];
        mutation["root.lifePercent"]=second?45:80;mutation["world.dayTime"]=scenario.Daytime;
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
            case "empress-night": case "empress-day":
                int[] empressTuple;
                return TryGetEmpressStageTuple(out empressTuple) &&
                    Near(root.ai[0],empressTuple[0],0.001f) &&
                    Near(root.ai[1],empressTuple[1],0.001f) &&
                    Near(root.ai[2],empressTuple[2],0.001f) &&
                    Near(root.ai[3],empressTuple[3],0.001f);
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
            if(scenario.DirectSpawn && ticks==directSpawnTick) SpawnDirectEncounter();
            if(scenario.DirectSpawn && !IsScopeNegative && ticks==takeoverTick) StageRequestedPhase();
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
            if(ticks >= tickLimit || clock.Elapsed.TotalSeconds >= wallLimitSeconds) Finish("test-time-limit");
        }
        catch(Exception e) { Fail(e); }
    }

    static void AfterNativeUpdate()
    {
        var p=Game.player[0];
        if(playerReturnedTick!=ticks) throw new InvalidOperationException("Native Player.Update did not return at tick "+ticks);
        nativeFrames++;
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
        if(lostLifeThisFrame) hits++;
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
        string state=SessionState();
        if(state=="Faulted") throw new InvalidOperationException("Production automation entered fail-closed; inspect Chaite log");
        if(state=="SuccessNoDeath" || state=="SuccessAfterDeath" || state=="FailedAfterDeath" || state=="EncounterInterrupted" || state=="Cancelled") Finish(state);
        if(ticks>=240 && (state=="RejectedNoEncounter" || state=="Idle")) Finish("activation-ended");
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
        if(motionCase.EndsWith("-tap",StringComparison.Ordinal)) return ticks==21;
        if(motionCase.EndsWith("-release-press",StringComparison.Ordinal)) return ticks!=26;
        return true;
    }

    static string MotionPhase()
    {
        if(IsFlight) return FlightPhase();
        if(ticks<=MotionWarmupFrames) return "warmup-release";
        if(ticks>80) return "final-release-and-land";
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
        int sourceWarmupHits=0, suppressedSunWarmupHits=0, expiredChecks=0;
        foreach(int type in new[]{455,923})
        {
            var ages=type==455?new[]{0,1,19,20,21,90,170,179,180}:new[]{0,1,19,20,59,60,61,90,150,179,180};
            foreach(int age in ages)
            for(int variant=0;variant<(type==455?2:1);variant++)
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
                projectile.scale=type==455
                    ?Math.Max(0f,Math.Min(limit,(float)Math.Sin(age*3.141593f/180f)*10f*limit))
                    :Math.Max(0f,Math.Min(1f,age/20f))*Math.Max(0f,Math.Min(1f,(180f-age)/60f));
                // Both original AI routines kill at age>=180. This is a lifecycle
                // fixture, not an assertion that this comparison executes their AI.
                projectile.active=age<180;
                bool nativeDamageGate=canDealDamage(projectile);
                if(nativeDamageGate!=(type==455||age>60))
                    throw new InvalidOperationException("Unexpected native beam damage gate: type="+type+" age="+age);
                var threat=new Chaite.Core.ThreatSnapshot { Type=type, Geometry=type==455?Chaite.Core.ThreatGeometry.MoonLordDeathray:Chaite.Core.ThreatGeometry.EmpressSunDance,
                    Position=new Chaite.Core.Vec2(projectile.position.X,projectile.position.Y),Width=projectile.width,Height=projectile.height,
                    BeamOrigin=new Chaite.Core.Vec2(center.X,center.Y),BeamDirection=new Chaite.Core.Vec2(projectile.velocity.X,projectile.velocity.Y),
                    BeamAge=age,BeamLength=length,BeamScale=projectile.scale,BeamScaleLimit=limit,
                    BeamAngle=projectile.rotation,BeamBaseAngle=angle,TimeLeft=600 };
                var beam=Chaite.Core.BeamGeometry.AtTime(in threat,0);
                float collisionAngle=type==455?angle:projectile.rotation;
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
                        int lobe=sample%3;
                        float reach=type==455?length:(lobe==0?510f:lobe==1?660f:800f)*projectile.scale;
                        float width=type==455?36f*projectile.scale:(lobe==0?70f:lobe==1?42f:7f)*projectile.scale;
                        float along=(float)(random.NextDouble()*(reach+80f)-40f);
                        float across=(float)((random.NextDouble()*2f-1f)*(width*.5f+45f));
                        var targetCenter=center+axis*along+normal*across;
                        box=new Rectangle((int)(targetCenter.X-10f),(int)(targetCenter.Y-21f),20,42);
                    }
                    else box=new Rectangle(2500+random.Next(5000),2500+random.Next(5000),20,42);
                    // Sun Dance's <=60 damage gate is OUTSIDE Colliding. Deathray's
                    // age<20 branch suppresses its line only, not source-body contact.
                    bool nativeShape=projectile.Colliding(projectile.Hitbox,box);
                    bool native=projectile.active&&nativeDamageGate&&nativeShape;
                    var bounds=new Chaite.Core.RectF(box.X,box.Y,box.Width,box.Height);
                    bool actual=Chaite.Core.BeamGeometry.Intersects(in bounds,in beam,0);
                    if(type==455&&age<20&&native) sourceWarmupHits++;
                    if(type==923&&age<=60&&nativeShape&&!native) suppressedSunWarmupHits++;
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
            " sourceWarmupHits="+sourceWarmupHits+" suppressedSunWarmupHits="+suppressedSunWarmupHits+" expiredChecks="+expiredChecks);
        if(sourceWarmupHits==0||suppressedSunWarmupHits==0||expiredChecks==0)
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
        return scenario!=null && (scenario.DirectSpawn?
            directSpawnAttempted && directSpawnCompleted && phaseStageAttempted && phaseStaged &&
                phaseVerifiedAtTakeover && actualTakeoverTick==takeoverTick:
            sawSummonConsumed && actualTakeoverTick==takeoverTick);
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

    static void WriteResult(string status,string outcome,bool win,int exitCode,string failure)
    {
        if(IsScopeNegative) { WriteScopeNegativeResult(status,outcome,win,exitCode,failure); return; }
        if(IsFlight) { WriteFlightResult(status,exitCode,failure); return; }
        if(IsMotion) { WriteMotionResult(status,exitCode,failure); return; }
        DrainHurtObservations();
        FlushHurtObservations();
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
        string evidenceKind=stagedPhaseFixture?"staged-native-phase-regression":"isolated-native-encounter";
        bool readinessEligible=!stagedPhaseFixture;
        int reportedBossLife;
        var bossLifeObservation=BuildBossLifeObservation(win,out reportedBossLife);
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-boss-result/v1"},{"schemaVersion",1},{"scenario",scenario==null?null:scenario.Id},
            {"seed",seed},{"difficulty",difficulty},{"difficultyCode",difficultyCode},{"variant",reportedVariant},
            {"variantEvidence",variantEvidence},{"evidenceKind",evidenceKind},{"readinessEligible",readinessEligible},{"status",status},
            {"requestedPhase",requestedPhase},{"requestedTakeoverTick",takeoverTick},{"actualTakeoverTick",actualTakeoverTick},
            {"directSpawn",scenario!=null && scenario.DirectSpawn},{"directSpawnTick",directSpawnTick},
            {"directSpawnAttempted",directSpawnAttempted},{"directSpawnCompleted",directSpawnCompleted},
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
                    {"worldWidthTiles",4200},{"worldHeightTiles",1200},{"groundLeft",scenario!=null && scenario.Ocean?80:800},{"groundRightExclusive",scenario!=null && scenario.Ocean?1900:3400},
                    {"groundTop",scenario!=null && scenario.Underworld?Game.maxTilesY-140:scenario!=null && scenario.Jungle?700:500},{"groundThickness",scenario!=null && scenario.Snow?12:6},
                    {"groundTile",scenario!=null && scenario.Hallow?"Pearlstone":scenario!=null && scenario.Jungle?"JungleGrass":scenario!=null && scenario.Snow?"IceBlock":"GrayBrick"},
                    {"platformRows",scenario!=null && (scenario.HardMode || scenario.PriorityArena || scenario.DirectSpawn) && !scenario.Underworld?
                        new[]{(scenario.Jungle?700:500)-40,(scenario.Jungle?700:500)-80}:new int[0]},
                    {"platformLeft",scenario!=null && scenario.Ocean?50:1850},{"platformRightExclusive",scenario!=null && scenario.Ocean?550:2350},
                    {"startingDay",scenario!=null && scenario.Daytime},{"startingWorldTime",scenario!=null && scenario.Daytime?27000:1000},
                    {"sceneMetricsPolicy","headless: native Player.UpdateSceneMetrics each tick after frame counter advance; native Player.Update transfers biome state; no forced Zone flags"},
                    {"nativeSceneMetricRefreshes",nativeSceneMetricRefreshes}
                }},
            {"elapsedMs",processClock.Elapsed.TotalMilliseconds},{"combatLoopElapsedMs",clock.Elapsed.TotalMilliseconds},
            {"limits",new Dictionary<string,object>{{"ticks",tickLimit},{"wallSeconds",wallLimitSeconds}}},
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
            {"limits",new Dictionary<string,object>{{"ticks",tickLimit},{"wallSeconds",wallLimitSeconds}}},
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
