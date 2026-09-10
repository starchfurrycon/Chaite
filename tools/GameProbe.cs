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
    static string difficulty = "classic";
    static int deaths, minLife = int.MaxValue, grappleTicks, maximumGrappleTicks, expectedBossMask;
    static long bossDamage, previousRuntimeTotal;
    static int previousRuntimeFrames, nativeSceneMetricRefreshes;
    static readonly List<double> runtimeSamples = new List<double>(24000);
    static readonly List<double> engineSamples = new List<double>(24000);
    static readonly Dictionary<int, BossLifeSample> previousBosses = new Dictionary<int, BossLifeSample>();
    static readonly List<int> retiredBossSlots = new List<int>(16);
    static readonly HashSet<int> unexpectedBossTypes = new HashSet<int>();
    static Dictionary<string, object> equipmentReport;
    static readonly Type runtimeType = typeof(Chaite.Plugin.Runtime);
    static readonly FieldInfo runtimeTotalField = runtimeType.GetField("_timingTotal", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo runtimeFramesField = runtimeType.GetField("_timingFrames", BindingFlags.Static | BindingFlags.NonPublic);

    sealed class ScenarioSpec
    {
        public string Id;
        public int Summon;
        public int[] BossTypes;
        public bool HardMode, Hallow, Legacy;
        public string Equipment;
    }

    struct BossLifeSample
    {
        public int Type, Life;
    }
    static int ticks;
    static bool booted, failed, captured;
    static int lastLife, hits, lastBossLife;
    static int playerReturnedTick = -1, nativeFrames, maximumBossLife, maximumShots;
    static bool sawBoss, sawBossDamage, sawMovement, sawSummonConsumed;
    static Vector2 initialPosition;
    static int bulletReports;
    static int killedBulletReports;
    public static void ProjectileKilled(Projectile projectile)
    {
        if(projectile.type==14 && killedBulletReports++<5)
            Log("NATIVE_BULLET_KILL pos="+projectile.position+" vel="+projectile.velocity+" time="+projectile.timeLeft+
                " penetration="+projectile.penetrate+" "+Environment.StackTrace);
    }
    static string lastSession;
    static Stopwatch clock = Stopwatch.StartNew();
    static readonly object logLock = new object();

    public static void Log(string text)
    {
        lock(logLock) File.AppendAllText(Path.Combine(Root, "game-probe.log"), DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine);
    }
    public static void ChatMessage(string text) { Log("CHAT "+text); }
    public static void PlayerDeath(Terraria.DataStructures.PlayerDeathReason reason)
    {
        deaths++;
        minLife = 0;
        Log("NATIVE_DEATH "+reason.GetDeathText("Chaite Lab")+" "+Environment.StackTrace);
    }
    public static void ProjectileBeforeUpdate(Projectile projectile)
    {
        if(projectile.active && projectile.friendly && projectile.owner==0 && bulletReports<24)
        {
            bulletReports++;
            Log("NATIVE_SHOT type="+projectile.type+" pos="+projectile.position+" vel="+projectile.velocity+
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
                key!="-difficulty" && key!="-maxticks" && key!="-wallseconds")
                throw new ArgumentException("Unsupported probe argument: "+key);
        string value;
        if(Terraria.Program.LaunchParameters.TryGetValue("-seed",out value))
            seed=BoundedInt(value,0,int.MaxValue,"seed");
        if(Terraria.Program.LaunchParameters.TryGetValue("-maxticks",out value))
            tickLimit=BoundedInt(value,600,24000,"maxticks");
        if(Terraria.Program.LaunchParameters.TryGetValue("-wallseconds",out value))
            wallLimitSeconds=BoundedInt(value,15,90,"wallseconds");
        if(Terraria.Program.LaunchParameters.TryGetValue("-difficulty",out value)) difficulty=value.ToLowerInvariant();
        switch(difficulty)
        {
            case "classic": difficultyCode=0; break;
            case "expert": difficultyCode=1; break;
            case "master": difficultyCode=2; break;
            default: throw new ArgumentException("difficulty must be classic, expert or master");
        }
        var id=Terraria.Program.LaunchParameters.TryGetValue("-scenario",out value)?value.ToLowerInvariant():"eye-baseline";
        scenario=new ScenarioSpec { Id=id };
        switch(id)
        {
            case "eye-baseline": scenario.Summon=ItemID.SuspiciousLookingEye; scenario.BossTypes=new[]{4}; scenario.Legacy=true; break;
            case "eye": scenario.Summon=ItemID.SuspiciousLookingEye; scenario.BossTypes=new[]{4}; break;
            case "king-slime": scenario.Summon=ItemID.SlimeCrown; scenario.BossTypes=new[]{50}; break;
            case "queen-slime": scenario.Summon=ItemID.QueenSlimeCrystal; scenario.BossTypes=new[]{657}; scenario.HardMode=true; scenario.Hallow=true; break;
            case "destroyer": scenario.Summon=ItemID.MechanicalWorm; scenario.BossTypes=new[]{134}; scenario.HardMode=true; break;
            case "twins": scenario.Summon=ItemID.MechanicalEye; scenario.BossTypes=new[]{125,126}; scenario.HardMode=true; break;
            case "prime": scenario.Summon=ItemID.MechanicalSkull; scenario.BossTypes=new[]{127}; scenario.HardMode=true; break;
            default: throw new ArgumentException("Unknown bounded scenario: "+id);
        }
        settingsRead=true;
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
                // Replaces only Main.rand. WorldGen.genRand, local random generators
                // and wall-time-dependent systems are not claimed to be seeded.
                Game.rand=new Terraria.Utilities.UnifiedRandom(seed);
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
                if(!Terraria.Program.LaunchParameters.ContainsKey("-skipbeam")) CompareNativeBeamGeometry();
                else Log("BEAM_COMPARE_SKIPPED diagnostic combat iteration only");
                // Surface initialization problems instead of allowing the outer
                // vanilla per-player exception guard to hide an invalid fixture.
                Game.player[0].Update(0);
                Log("DIRECT_PLAYER_UPDATE completed defense="+Game.player[0].statDefense+" accRun="+Game.player[0].accRunSpeed);
                if(scenario.Hallow)
                {
                    Game.player[0].UpdateSceneMetrics();
                    // Scan updates real tile counts; native UpdateBiomes transfers
                    // those metrics to player zone flags (no forced/fake ZoneHallow).
                    Game.player[0].UpdateBiomes();
                    Log("NATIVE_BIOME hallow="+Game.player[0].ZoneHallow+" holyTiles="+Player.SceneMetrics.HolyTileCount);
                    if(!Game.player[0].ZoneHallow) throw new InvalidOperationException("Queen Slime fixture lacks a native-detected Hallow biome");
                }
                var update = (Action)Delegate.CreateDelegate(typeof(Action),game,typeof(Game).GetMethod("DoUpdateInWorld",BindingFlags.Instance|BindingFlags.NonPublic));
                var counter=typeof(Game).GetField("_gameUpdateCount",BindingFlags.Static|BindingFlags.NonPublic);
                while(!failed)
                {
                    BeforeUpdate();
                    counter.SetValue(null,(uint)ticks);
                    long nativeStart=Stopwatch.GetTimestamp();
                    // A rendered client refreshes nearby tile metrics through its
                    // scene/lighting path, absent in this dedicated presentation.
                    // Use the real scan at the new frame counter, then let native
                    // Player.Update transfer its result through UpdateBiomes.
                    // Never force a Zone flag or keep initial biome counts frozen.
                    Game.player[0].UpdateSceneMetrics();
                    nativeSceneMetricRefreshes++;
                    update();
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
            Game.spawnTileX = 2100;
            Game.spawnTileY = 498;
            Game.worldName = "Chaite isolated engine test";
            Game.dayTime = false;
            Game.time = 1000;
            Game.GameMode = difficultyCode;
            Game.hardMode = scenario.HardMode;
            Game.netMode = 0;
            Game.myPlayer = 0;
            for (int x = 800; x < 3400; x++)
            for (int y = 500; y < 506; y++)
            {
                if (Game.tile[x,y] == null) Game.tile[x,y] = new Tile();
                Game.tile[x,y].active(true);
                Game.tile[x,y].type = scenario.Hallow?TileID.Pearlstone:TileID.GrayBrick;
            }
            // Two ordinary, non-actuated wooden-platform rows. These are real
            // tiles, not altered collision or NPC behavior; baseline is unchanged.
            if(scenario.HardMode)
                foreach(int y in new[]{460,420})
                    for(int x=1850;x<2350;x++)
                    {
                        Game.tile[x,y].active(true);
                        Game.tile[x,y].type=TileID.Platforms;
                        Game.tile[x,y].frameX=0; Game.tile[x,y].frameY=0;
                    }
            var player = new Player();
            player.name = "Chaite Lab";
            player.whoAmI = 0;
            player.active = true;
            player.statLifeMax = player.statLife = 400;
            player.statManaMax = player.statMana = 200;
            player.position = new Vector2(2100 * 16, 500 * 16 - player.height);
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
            Log("ARENA_READY in-memory; "+difficulty+" life400 "+scenario.Equipment+"; no godmode; no user saves");
        }
        catch(Exception e) { Fail(e); }
    }

    static void EquipScenario(Player player)
    {
        player.inventory[0].SetDefaults(scenario.HardMode?ItemID.ClockworkAssaultRifle:ItemID.Minishark);
        player.inventory[1].SetDefaults(scenario.Summon);
        player.inventory[1].stack=1;
        player.inventory[54].SetDefaults(scenario.HardMode?ItemID.CrystalBullet:ItemID.MusketBall);
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
        else if(scenario.HardMode)
        {
            player.armor[0].SetDefaults(ItemID.AdamantiteHelmet);
            player.armor[1].SetDefaults(ItemID.AdamantiteBreastplate);
            player.armor[2].SetDefaults(ItemID.AdamantiteLeggings);
            player.armor[3].SetDefaults(ItemID.LightningBoots);
            player.armor[4].SetDefaults(ItemID.DemonWings);
            player.armor[5].SetDefaults(ItemID.BandofRegeneration);
            player.armor[6].SetDefaults(ItemID.ObsidianShield);
            player.armor[7].SetDefaults(ItemID.RangerEmblem);
            if(difficultyCode>0)
            {
                // A Demon Heart is available after the Wall of Flesh, before these
                // encounters. The shield comes from the earlier Expert Eye fight.
                player.extraAccessory=true;
                player.armor[8].SetDefaults(ItemID.EoCShield);
            }
            player.inventory[10].SetDefaults(ItemID.GreaterHealingPotion);
            player.inventory[10].stack=20;
            scenario.Equipment="early-hardmode adamantite-melee-head+lightning+demon-wings+regen-band+obsidian-shield+ranger-emblem+clockwork+crystal-bullets; greater-healing x20";
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
        var armor=new int[10];
        for(int i=0;i<armor.Length;i++) armor[i]=player.armor[i].type;
        equipmentReport=new Dictionary<string,object>
        {
            {"label",scenario.Equipment},{"life",400},{"mana",200},{"armorAndAccessories",armor},
            {"weaponType",player.inventory[0].type},{"summonType",scenario.Summon},{"summonCount",1},
            {"ammoType",player.inventory[54].type},{"ammoCount",9999},
            {"healingType",player.inventory[10].type},{"healingCount",player.inventory[10].stack},
            {"grappleType",player.miscEquips[4].type},{"consumablesReplenished",false},
            {"legacyBaseline",scenario.Legacy}
        };
    }

    // Synthetic edges ONLY in test copy's poller. No OS keys or mouse are sent.
    public static bool ActivateDown() { return booted && ticks == 120; }
    public static bool CancelDown() { return false; }

    public static void BeforeUpdate()
    {
        if (failed) return;
        try
        {
            if (!booted) return;
            ticks++;
            Game.screenPosition=Game.player[0].Center-new Vector2(Game.screenWidth/2f,Game.screenHeight/2f);
            Game.autoSave = false;
            Game.SettingPlayWhenUnfocused = true;
            if (ticks % 60 == 0)
            {
                var p = Game.player[0];
                var selectedWeapon=p.selectedItem>=0 && p.selectedItem<p.inventory.Length?p.inventory[p.selectedItem]:null;
                int bossCount=0, bossLife=0, shots=0;
                foreach(var npc in Game.npc) if(npc != null && npc.active && npc.boss) { bossCount++; bossLife+=npc.life; }
                foreach(var shot in Game.projectile) if(shot != null && shot.active && shot.friendly && shot.owner==0) shots++;
                string state = SessionState();
                lastBossLife=bossLife;
                Log("FRAME tick="+ticks+" elapsedMs="+clock.ElapsedMilliseconds+" state="+state+" life="+p.statLife+" dead="+p.dead+
                    " pos="+p.position+" vel="+p.velocity+" maxRun="+p.maxRunSpeed+" accRun="+p.accRunSpeed+
                    " bosses="+bossCount+" bossLife="+bossLife+" shots="+shots+" ammo="+p.inventory[54].stack+" summon="+p.inventory[1].stack+
                    " selected="+p.selectedItem+" armor="+p.armor[3].type+" defense="+p.statDefense+" day="+Game.dayTime+" menu="+Game.gameMenu+
                    " grappling="+p.grapCount+" hook="+p.controlHook+" releaseJump="+p.releaseJump+
                    " wingTime="+p.wingTime+" wingMax="+p.wingTimeMax+" wings="+p.wings+" wingsLogic="+p.wingsLogic+
                    " itemAnimation="+p.itemAnimation+" itemAnimationMax="+p.itemAnimationMax+" itemTime="+p.itemTime+" releaseUse="+p.releaseUseItem+
                    " weapon="+(selectedWeapon==null?0:selectedWeapon.type)+" useTime="+(selectedWeapon==null?0:selectedWeapon.useTime)+
                    " useAnimation="+(selectedWeapon==null?0:selectedWeapon.useAnimation)+" autoReuse="+(selectedWeapon!=null && selectedWeapon.autoReuse)+
                    " channel="+(selectedWeapon!=null && selectedWeapon.channel)+
                    " controls="+p.controlLeft+","+p.controlRight+","+p.controlJump+","+p.controlUseItem);
                if(state != lastSession) { Log("STATE "+state); lastSession=state; }
            }
            if(ticks >= tickLimit || processClock.Elapsed.TotalSeconds >= wallLimitSeconds) Finish("test-time-limit");
        }
        catch(Exception e) { Fail(e); }
    }

    static void AfterNativeUpdate()
    {
        var p=Game.player[0];
        if(playerReturnedTick!=ticks) throw new InvalidOperationException("Native Player.Update did not return at tick "+ticks);
        nativeFrames++;
        if(p.statLife<lastLife) hits++;
        lastLife=p.statLife;
        minLife=Math.Min(minLife,Math.Max(0,p.statLife));
        grappleTicks=p.grapCount>0?grappleTicks+1:0;
        maximumGrappleTicks=Math.Max(maximumGrappleTicks,grappleTicks);
        SampleRuntimeTiming();
        sawMovement |= Vector2.DistanceSquared(initialPosition,p.position)>64;
        sawSummonConsumed |= p.inventory[1].stack==0 || p.inventory[1].type==0;
        int bossLife=0, shots=0;
        foreach(var npc in Game.npc)
            if(npc!=null && npc.active && npc.boss)
            {
                sawBoss=true; bossLife+=npc.life;
                bool expected=false;
                for(int i=0;i<scenario.BossTypes.Length;i++)
                    if(npc.type==scenario.BossTypes[i]) { expectedBossMask|=1<<i; expected=true; }
                if(!expected) unexpectedBossTypes.Add(npc.type);
            }
        TrackExpectedBossDamage();
        maximumBossLife=Math.Max(maximumBossLife,bossLife);
        if(bossDamage>0) sawBossDamage=true;
        foreach(var shot in Game.projectile) if(shot!=null && shot.active && shot.friendly && shot.owner==0) shots++;
        maximumShots=Math.Max(maximumShots,shots);
        lastBossLife=bossLife;
        string state=SessionState();
        if(state=="Faulted") throw new InvalidOperationException("Production automation entered fail-closed; inspect Chaite log");
        if(state=="SuccessNoDeath" || state=="SuccessAfterDeath" || state=="FailedAfterDeath" || state=="EncounterInterrupted" || state=="Cancelled") Finish(state);
        if(ticks>=240 && (state=="RejectedNoEncounter" || state=="Idle")) Finish("activation-ended");
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
        if(npc==null) return false;
        foreach(int type in scenario.BossTypes) if(type==npc.type) return true;
        return false;
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
    static void Finish(string outcome)
    {
        if(finishing) return;
        finishing=true;
        bool expectedSeen=scenario!=null && expectedBossMask==((1<<scenario.BossTypes.Length)-1);
        bool battlePassed=(outcome=="SuccessNoDeath" || outcome=="SuccessAfterDeath") &&
            nativeFrames>120 && sawSummonConsumed && expectedSeen && sawBossDamage && sawMovement && maximumShots>0;
        bool reportedSuccess=outcome=="SuccessNoDeath" || outcome=="SuccessAfterDeath";
        string status=battlePassed?"win":reportedSuccess?"harness-error":outcome=="test-time-limit"?"timeout":
            outcome=="activation-ended" && deaths==0?"rejected":"loss";
        int exitCode=battlePassed?0:reportedSuccess?10:20;
        Log("FINISH "+outcome+" battlePassed="+battlePassed+" ticks="+ticks+" hits="+hits+" bossLife="+lastBossLife+
            " nativeFrames="+nativeFrames+" summonConsumed="+sawSummonConsumed+" sawBoss="+sawBoss+
            " bossDamaged="+sawBossDamage+" moved="+sawMovement+" maximumShots="+maximumShots);
        WriteResult(status,outcome,battlePassed,exitCode,battlePassed?null:
            reportedSuccess?"Native session reported success without all required battle evidence":outcome);
        Environment.Exit(exitCode);
    }

    static void WriteResult(string status,string outcome,bool win,int exitCode,string failure)
    {
        bool expectedSeen=scenario!=null && scenario.BossTypes!=null && expectedBossMask==((1<<scenario.BossTypes.Length)-1);
        var unexpected=new int[unexpectedBossTypes.Count]; unexpectedBossTypes.CopyTo(unexpected); Array.Sort(unexpected);
        var result=new Dictionary<string,object>
        {
            {"schema","chaite-boss-result/v1"},{"schemaVersion",1},{"scenario",scenario==null?null:scenario.Id},
            {"seed",seed},{"difficulty",difficulty},{"difficultyCode",difficultyCode},{"status",status},
            {"processExitCode",exitCode},{"outcome",outcome},{"win",win},{"failure",failure},
            {"validBattle",status!="harness-error" && expectedSeen && sawSummonConsumed && nativeFrames>120},
            {"battleStarted",expectedBossMask!=0},{"allExpectedBossesSeen",expectedSeen},
            {"death",deaths>0},{"deaths",deaths},{"hits",hits},{"ticks",ticks},{"nativeFrames",nativeFrames},
            {"minLife",minLife==int.MaxValue?0:minLife},{"bossDamage",bossDamage},{"bossLifeRemaining",lastBossLife},
            {"maxGrappleTicks",maximumGrappleTicks},{"maximumShots",maximumShots},
            {"summonConsumed",sawSummonConsumed},{"bossDamaged",sawBossDamage},{"playerMoved",sawMovement},
            {"unexpectedBossTypes",unexpected},{"equipment",equipmentReport},
            {"arena",new Dictionary<string,object>
                {
                    {"kind","in-memory hand-built fixture; not a generated/saved user world"},
                    {"worldWidthTiles",4200},{"worldHeightTiles",1200},{"groundLeft",800},{"groundRightExclusive",3400},
                    {"groundTop",500},{"groundThickness",6},
                    {"groundTile",scenario!=null && scenario.Hallow?"Pearlstone":"GrayBrick"},
                    {"platformRows",scenario!=null && scenario.HardMode?new[]{460,420}:new int[0]},
                    {"platformLeft",1850},{"platformRightExclusive",2350},{"startingNightTime",1000},
                    {"sceneMetricsPolicy","headless: native Player.UpdateSceneMetrics each tick after frame counter advance; native Player.Update transfers biome state; no forced Zone flags"},
                    {"nativeSceneMetricRefreshes",nativeSceneMetricRefreshes}
                }},
            {"elapsedMs",processClock.Elapsed.TotalMilliseconds},{"combatLoopElapsedMs",clock.Elapsed.TotalMilliseconds},
            {"limits",new Dictionary<string,object>{{"ticks",tickLimit},{"wallSeconds",wallLimitSeconds}}},
            {"runtime",TimingSummary(runtimeSamples,"production snapshot+plan+capture; excludes vanilla update/render/input presentation")},
            {"nativeUpdate",TimingSummary(engineSamples,"native scene-metric refresh plus headless DoUpdateInWorld including production plugin; excludes harness tracking and Sleep")},
            {"randomScope","seed initializes Main.rand before Main.Initialize only; not WorldGen.genRand or every local/wall-time random source; not a world-generation seed"},
            {"scope","one process, one fixed-gear arena fixture, one F8 edge; native boss summon/AI/damage/physics; headless dedicated presentation branches, not a rendered client"},
            {"hitsDefinition","native update frames with decreased player life, including environmental damage; not a damage-event hook"},
            {"bossDamageDefinition","observed expected boss-root life decreases including observed final deaths; shared worm body segments excluded"},
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
        if(value is double && (double.IsNaN((double)value)||double.IsInfinity((double)value))) return "null";
        return Convert.ToString(value,CultureInfo.InvariantCulture);
    }
}
