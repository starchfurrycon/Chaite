using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Chaite.Plugin
{
    /// <summary>Version-locked, compiled reflection bridge for vanilla Terraria 1.4.5.8.</summary>
    internal sealed class TerrariaFacade
    {
        private readonly ChaiteConfig _config;
        private readonly Type _mainType;
        private readonly Func<int> _myPlayer;
        private readonly Func<int> _netMode;
        private readonly Func<object> _activeWorldFileData;
        private readonly Func<object, Guid> _worldUniqueId;
        private readonly Func<object, int> _worldFileId;
        private readonly Func<bool> _gameMenu;
        private readonly Func<bool> _blockInput;
        private readonly Func<object[]> _npcs;
        private readonly Func<object[]> _projectiles;
        private readonly Func<Array> _tiles;
        private readonly Func<int> _maxTilesX;
        private readonly Func<int> _maxTilesY;
        private readonly Func<int> _maxNpcs;
        private readonly Func<double> _worldSurface;
        private readonly Func<bool> _windPhysics;
        private readonly Func<int> _underworldLayer;
        private readonly Func<int> _wofDrawAreaTop;
        private readonly Func<int> _wofDrawAreaBottom;
        private readonly Func<bool> _dayTime;
        private readonly Func<double> _time;
        private readonly Func<bool> _hardMode;
        private readonly Func<int> _gameMode;
        private readonly Func<bool> _expertMode;
        private readonly Func<bool> _masterMode;
        private readonly Func<bool> _drunkWorld;
        private readonly Func<bool> _notTheBeesWorld;
        private readonly Func<bool> _goodWorld;
        private readonly Func<bool> _remixWorld;
        private readonly Func<bool> _zenithWorld;
        private readonly Func<bool> _celebrationWorld;
        private readonly Func<bool> _constantWorld;
        private readonly Func<bool> _noTrapsWorld;
        private readonly Func<bool> _skyblockWorld;
        private readonly Func<int> _invasionType;
        private readonly Func<bool> _pumpkinMoon;
        private readonly Func<bool> _snowMoon;
        private readonly Func<bool> _spawnEye;
        private readonly Func<int> _spawnHardBoss;
        private readonly Func<int> _moonLordCountdown;
        private readonly Func<bool> _downedGolemBoss;
        private readonly Func<bool> _towerSolar;
        private readonly Func<bool> _towerVortex;
        private readonly Func<bool> _towerNebula;
        private readonly Func<bool> _towerStardust;
        private readonly Func<bool> _empressRageMode;

        private readonly Func<object, bool> _npcActive;
        private readonly Func<object, bool> _npcBoss;
        private readonly Func<object, bool> _npcFriendly;
        private readonly Func<object, bool> _npcChaseable;
        private readonly Func<object, bool> _npcInvulnerable;
        private readonly Func<object, bool> _npcImmortal;
        private readonly Func<object, int[]> _npcImmune;
        // AI_071 calls the native predicate instead of merely reading its
        // constituent fields. Keep that exact call separate from the broader
        // target/threat snapshot model.
        private readonly Func<object, bool> _npcCanBeChasedBy;
        private readonly Func<object, int> _npcDamage;
        private readonly Func<object, int> _npcLife;
        private readonly Func<object, int> _npcLifeMax;
        private readonly Func<object, int> _npcTimeLeft;
        private readonly Func<object, int> _npcTypeId;
        private readonly Func<object, int> _whoAmI;
        private readonly Func<object, int> _npcRealLife;
        private readonly Func<object, byte> _npcGeneration;
        private readonly Func<object, int> _npcTarget;
        private readonly Func<object, float[]> _npcAi;
        private readonly Func<object, float[]> _npcLocalAi;

        private readonly Func<object, int> _width;
        private readonly Func<object, int> _height;
        private readonly Func<object, float> _positionX;
        private readonly Func<object, float> _positionY;
        private readonly Func<object, float> _velocityX;
        private readonly Func<object, float> _velocityY;

        private readonly Func<object, bool> _projectileActive;
        private readonly Func<object, bool> _projectileHostile;
        private readonly Func<object, int> _projectileDamage;
        private readonly Func<object, int> _projectileTimeLeft;
        private readonly Func<object, int> _projectileExtraUpdates;
        private readonly Func<object, int> _projectileAlpha;
        private readonly Func<object, object, object> _pickAmmoItem;
        private readonly Func<object, object, int> _weaponDamage;
        private readonly Func<object, object, float> _weaponDamageMultiplier;
        private readonly Func<int, object> _projectileSample;
        private readonly Func<object, int> _projectileTypeId;
        private readonly Func<object, int> _projectileOwner;
        private readonly Func<object, bool> _projectileBobber;
        private readonly Func<object, float[]> _projectileAi;
        private readonly Func<object, float[]> _projectileLocalAi;
        private readonly Func<object, int, float> _projectileOldPosX;
        private readonly Func<object, int, float> _projectileOldPosY;
        private readonly Func<object, int> _projectileOldPosLength;
        private readonly Func<object, float> _projectileScale;
        private readonly Func<object, float> _projectileRotation;

        private readonly Func<object, bool> _playerDead;
        private readonly Func<object, bool> _playerActive;
        private readonly Func<object, bool> _releaseUseItem;
        // Quick health is an edge-triggered vanilla action just like quick
        // mana.  Reading its release latch prevents a continuously low-life
        // plan from holding the key down and silently consuming only the
        // first potion of an encounter.
        private readonly Func<object, bool> _releaseQuickHeal;
        private readonly Func<object, bool> _releaseQuickMana;
        private readonly Func<object, bool> _releaseJump;
        private readonly Func<object, bool> _releaseUp;
        private readonly Func<object, bool> _releaseDash;
        private readonly NativeJumpReader _nativeJumpReader;
        private readonly NativeFlightReader _nativeFlightReader;
        private readonly NativeGrappleReader _nativeGrappleReader;
        private readonly NativeWitchBroomReader _nativeWitchBroomReader;
        private readonly Func<object, int> _playerItemAnimation;
        private readonly Func<object, int> _playerItemTime;
        private readonly Func<object, bool> _playerUsingOrReusingItem;
        private readonly Func<object, bool> _playerItemTimeIsZero;
        private readonly Func<object, bool[]> _inventoryChestStack;
        private readonly Func<object> _mouseItem;
        private readonly Func<bool> _drawingPlayerChat;
        private readonly Func<bool> _gamePaused;
        private readonly Action<object, int> _setPlayerDirection;
        private readonly Action<object> _dropSelectedItem;
        private readonly Func<object, int> _playerLife;
        private readonly Func<object, int> _playerMaxLife;
        private readonly Func<object, int> _playerMana;
        private readonly Func<object, int> _playerMaxMana;
        private readonly Func<object, int> _playerManaRegen;
        private readonly Func<object, int> _playerManaRegenCount;
        private readonly Func<object, float> _playerManaRegenDelay;
        private readonly Func<object, int> _playerManaPotionDelay;
        private readonly Func<object, float> _playerManaCost;
        private readonly Func<object, bool> _playerSpaceGun;
        private readonly Func<object, bool> _playerArchery;
        private readonly Func<object, bool> _playerMagicQuiver;
        private readonly Func<object, bool> _playerHasMoltenQuiver;
        private readonly Func<object, bool> _playerSharpBarb;
        private readonly Func<object, bool> _playerHarpyCharm;
        private readonly bool _arrowStateKnown;
        private readonly Func<object, bool> _playerManaSick;
        private readonly Func<object, float> _playerManaSickReduction;
        private readonly Func<object, int> _playerMaxMinions;
        private readonly Func<object, float> _playerSlotsMinions;
        private readonly Func<object, int[]> _playerOwnedProjectileCounts;
        private readonly Func<object, bool> _playerSilence;
        private readonly Func<object, bool> _playerNoItems;
        private readonly Func<object, bool> _playerCursed;
        private readonly Func<object, object> _quickManaItem;
        private readonly Func<object, float> _playerGravity;
        private readonly Func<object, float> _playerMaxFallSpeed;
        private readonly Func<object, float> _playerMaxRunSpeed;
        private readonly Func<object, float> _playerAccessoryRunSpeed;
        private readonly Func<object, float> _playerRunAcceleration;
        private readonly Func<object, float> _playerRunSlowdown;
        private readonly Func<object, float> _playerStrongestMoveSpeedDebuff;
        private readonly Func<object, bool> _playerSlow;
        private readonly Func<object, int> _playerWingsLogic;
        private readonly Func<object, float> _playerJumpSpeedBoost;
        private readonly Func<object, float> _playerWingTime;
        private readonly Func<object, int> _playerWingTimeMax;
        private readonly Func<object, int> _playerRocketTime;
        private readonly Func<object, int> _playerRocketTimeMax;
        private readonly Func<object, int> _playerRocketBoots;
        private readonly Func<object, int> _playerDashType;
        private readonly Func<object, int> _playerDashDelay;
        private readonly Func<object, int> _playerDash;
        private readonly Func<object, int> _playerDashTime;
        private readonly Func<object, int> _playerTimeSinceLastDashStarted;
        private readonly Func<object, int> _playerEocDash;
        private readonly Func<object, int> _playerEocHit;
        private readonly Func<object, int> _playerGrapCount;
        private readonly Func<object, float> _playerGravDir;
        private readonly Func<object, bool> _playerGravControl;
        private readonly Func<object, bool> _playerGravControl2;
        private readonly Func<object, int> _playerForcedGravity;
        private readonly Func<object, int> _playerJumpTicks;
        private readonly Func<object, int> _playerFallStart;
        private readonly Func<object, bool> _playerCCed;
        private readonly Func<object, bool> _playerPulley;
        private readonly Func<object, bool> _playerTongued;
        private readonly Func<object, bool> _playerOldStyleParkour;
        private readonly Func<object, int> _playerDirection;
        private readonly Func<object, int[]> _playerBuffType;
        private readonly Func<object, int[]> _playerBuffTime;
        private readonly Func<object, bool> _playerSlowFall;
        private readonly Func<object, bool> _playerReleaseMount;
        private readonly Func<object, bool> _playerDontHurtCritters;
        private readonly Func<object, object> _playerMount;
        private readonly Func<object, object[]> _inventory;
        private readonly Func<object, int, object> _getEffectiveArmor;
        private readonly Func<object, int, bool> _isItemSlotUnlockedAndUsable;
        private readonly Func<object, object> _quickMountItem;
        private readonly Func<object, object> _quickGrappleItem;
        private readonly Func<object, int> _selectedItem;
        private readonly Action<object, int> _setSelectedItem;

        private readonly Func<object, bool> _zoneCorrupt;
        private readonly Func<object, bool> _zoneCrimson;
        private readonly Func<object, bool> _zoneHallow;
        private readonly Func<object, bool> _zoneJungle;
        private readonly Func<object, bool> _zoneLihzhardTemple;
        private readonly Func<object, bool> _zoneSnow;
        private readonly Func<object, bool> _zoneBeach;
        private readonly Func<object, bool> _zoneOverworld;
        private readonly Func<object, bool> _zoneUnderworld;

        private readonly Func<object, bool> _mountActive;
        private readonly Func<object, int> _mountTypeId;
        private readonly Func<object, object, bool> _mountCanDismount;
        private readonly Func<object, int> _mountFlyTime;
        private readonly Func<object, float> _mountRunSpeed;
        private readonly Func<object[]> _mounts;
        private readonly Func<object, float> _mountDataRunSpeed;
        private readonly Func<object, int> _mountDataFlightTime;
        private readonly Func<object, bool> _mountDataUsesHover;

        private readonly Func<object, int> _itemTypeId;
        private readonly Func<object, int> _itemStack;
        private readonly FieldInfo _itemStackField;
        private readonly Func<object, bool> _itemFavorited;
        private readonly FieldInfo _itemFavoritedField;
        private readonly Func<object, int> _itemDamage;
        private readonly Func<object, int> _itemUseTime;
        private readonly Func<object, int> _itemUseAnimation;
        private readonly Func<object, int> _itemReuseDelay;
        private readonly Func<object, int> _itemBuffType;
        private readonly Func<object, int> _itemMana;
        private readonly Func<object, int> _itemHealMana;
        private readonly Func<object, bool> _itemAutoReuse;
        private readonly Func<object, bool> _itemChannel;
        private readonly Func<object, int> _itemUseStyle;
        private readonly Func<object, int> _itemPick;
        private readonly Func<object, int> _itemAxe;
        private readonly Func<object, int> _itemHammer;
        private readonly Func<object, int> _itemCreateTile;
        private readonly Func<object, int> _itemShoot;
        private readonly Func<object, float> _itemShootSpeed;
        private readonly Func<object, int> _itemAmmo;
        private readonly Func<object, int> _itemUseAmmo;
        private readonly Func<object, int> _itemFishingPole;
        private readonly Func<object, int> _itemBait;
        private readonly Func<object, int> _itemMountType;
        private readonly Func<object, sbyte> _itemWingSlot;
        private readonly MethodInfo _itemSetDefaults;

        private readonly Func<object, ushort> _tileType;
        private readonly Func<object, bool> _tileActive;
        private readonly Func<object, bool> _tileInactive;
        private readonly Func<object, byte> _tileSlope;
        private readonly Func<object, bool> _tileHalfBrick;
        private readonly Func<object, bool> _tileWater;
        private readonly Func<object, bool> _tileLava;
        private readonly bool[] _tileSolid;
        private readonly bool[] _tileSolidTop;
        private readonly bool[] _projectileHooks;

        private readonly Func<float> _screenX;
        private readonly Func<float> _screenY;
        private readonly Func<int> _screenHeight;
        private readonly Action<int> _mouseX;
        private readonly Action<int> _mouseY;
        private readonly FieldInfo _releaseThrow;
        private readonly Dictionary<string, Action<object, bool>> _controls;
        private readonly Dictionary<string, Func<object, bool>> _controlReaders = new Dictionary<string, Func<object, bool>>();
        private readonly Dictionary<string, bool> _capturedControls = new Dictionary<string, bool>();
        private readonly Func<int> _readMouseX;
        private readonly Func<int> _readMouseY;
        private int _capturedSelection;
        private int _requestedSelection = -1;
        private int _capturedMouseX;
        private int _capturedMouseY;
        private readonly Func<Array, int, int, object> _tileAt;
        private readonly Func<object, object, bool> _canHitLine;
        // Type-409's initial target scan starts at the actual spawned 30x30
        // projectile rectangle, not Player.Center. Both delegates are pure
        // compiled reflection adapters for the locked vanilla build.
        private readonly Func<object, Vec2> _razorbladeSpawnCenter;
        private readonly Func<Vec2, object, bool> _razorbladeCanHit;
        private readonly MethodInfo _newText;
        private readonly CombatSnapshot _combatSnapshot = new CombatSnapshot();
        private readonly EncounterObservation _observation = new EncounterObservation();
        private readonly List<int> _activeBossKeys = new List<int>(32);
        private readonly List<int> _activeBossGenerations =
            new List<int>(32);
        private readonly List<int> _activeBossTypes = new List<int>(32);
        private readonly TargetSightCache _sightCache = new TargetSightCache();
        private readonly DestroyerMotionHistory _destroyerMotionHistory = new DestroyerMotionHistory();
        private readonly WeaponSnapshot _weaponSelectionSnapshot = new WeaponSnapshot();
        private readonly PlayerSnapshot _identityScratch = new PlayerSnapshot();
        private int _sightFrame;
        private int _sightQueryBudget;
        private TargetSightQueryState _sightQueryState;
        private int _destroyerMotionFrame;
        private long _grappleFrameSequence;

        private ArenaSnapshot _cachedArena;
        private Vec2 _cachedArenaAt;
        private int _arenaCacheTicks;
        private bool _cachedArenaInverted;
        private bool _cachedArenaOnGround;
        private bool _cachedArenaOneWay;
        private float _cachedArenaFootY;
        private SupportSpan _recoverySupport;
        private int _recoverySupportAge;
        private SingleItemDropTransaction _voodooTransaction;
        private int _summonInitialStack = -1;
        private int _summonPulseTick = -1;
        private int _fishingSwapSlot = -1;
        private bool _validatePendingGravity;
        private bool _validatePendingDash;
        private bool _validatePendingFeatherFall;
        private bool _pendingFeatherFallRequiresPotionUp;
        private bool _pendingFallbackKnown;
        private bool _pendingFallbackLeft;
        private bool _pendingFallbackRight;
        private bool _pendingFallbackJump;
        private bool _pendingFallbackDrop;
        private int _pendingMobilityFrame;
        private GravityFlipState _pendingGravityState;
        private GravityFlipCandidate _pendingGravityCandidate;
        private EyeShieldDashState _pendingDashState;
        private EyeShieldDashCandidate _pendingDashCandidate;

        public TerrariaFacade(Assembly game, Type playerType, ChaiteConfig config)
        {
            _config = config;
            _mainType = game.GetType("Terraria.Main", true);
            var npcType = game.GetType("Terraria.NPC", true);
            var projectileType = game.GetType("Terraria.Projectile", true);
            var itemType = game.GetType("Terraria.Item", true);
            var entityType = game.GetType("Terraria.Entity", true);
            var worldGenType = game.GetType("Terraria.WorldGen", true);
            var worldFileDataType = game.GetType(
                "Terraria.IO.WorldFileData", true);
            var mountType = game.GetType("Terraria.Mount", true);
            var tileType = game.GetType("Terraria.Tile", true);

            _myPlayer = ReflectionAccess.StaticGetter<int>(_mainType, "myPlayer");
            _netMode = ReflectionAccess.StaticGetter<int>(_mainType, "netMode");
            _activeWorldFileData = ReflectionAccess.StaticGetter<object>(_mainType, "ActiveWorldFileData");
            _worldUniqueId = ReflectionAccess.Getter<Guid>(worldFileDataType, "UniqueId");
            _worldFileId = ReflectionAccess.Getter<int>(worldFileDataType, "WorldId");
            _gameMenu = ReflectionAccess.StaticGetter<bool>(_mainType, "gameMenu");
            _blockInput = ReflectionAccess.StaticGetter<bool>(_mainType, "blockInput");
            _npcs = StaticReferenceArray(_mainType, "npc");
            _projectiles = StaticReferenceArray(_mainType, "projectile");
            _tiles = StaticArray(_mainType, "tile");
            _maxTilesX = ReflectionAccess.StaticGetter<int>(_mainType, "maxTilesX");
            _maxTilesY = ReflectionAccess.StaticGetter<int>(_mainType, "maxTilesY");
            _maxNpcs = ReflectionAccess.StaticGetter<int>(_mainType, "maxNPCs");
            _worldSurface = ReflectionAccess.StaticGetter<double>(_mainType, "worldSurface");
            _windPhysics = ReflectionAccess.StaticGetter<bool>(_mainType, "windPhysics");
            _underworldLayer = ReflectionAccess.StaticPropertyGetter<int>(
                _mainType, "UnderworldLayer");
            _wofDrawAreaTop = ReflectionAccess.StaticGetter<int>(_mainType, "wofDrawAreaTop");
            _wofDrawAreaBottom = ReflectionAccess.StaticGetter<int>(_mainType, "wofDrawAreaBottom");
            _dayTime = ReflectionAccess.StaticGetter<bool>(_mainType, "dayTime");
            _time = ReflectionAccess.StaticGetter<double>(_mainType, "time");
            _hardMode = ReflectionAccess.StaticGetter<bool>(_mainType, "hardMode");
            _gameMode = ReflectionAccess.StaticPropertyGetter<int>(_mainType, "GameMode");
            _expertMode = ReflectionAccess.StaticPropertyGetter<bool>(_mainType, "expertMode");
            _masterMode = ReflectionAccess.StaticPropertyGetter<bool>(_mainType, "masterMode");
            _drunkWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "drunkWorld");
            _notTheBeesWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "notTheBeesWorld");
            _goodWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "getGoodWorld");
            _remixWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "remixWorld");
            _zenithWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "zenithWorld");
            _celebrationWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "tenthAnniversaryWorld");
            _constantWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "dontStarveWorld");
            _noTrapsWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "noTrapsWorld");
            _skyblockWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "skyblockWorld");
            _invasionType = ReflectionAccess.StaticGetter<int>(_mainType, "invasionType");
            _pumpkinMoon = ReflectionAccess.StaticGetter<bool>(_mainType, "pumpkinMoon");
            _snowMoon = ReflectionAccess.StaticGetter<bool>(_mainType, "snowMoon");
            _spawnEye = ReflectionAccess.StaticGetter<bool>(worldGenType, "spawnEye");
            _spawnHardBoss = ReflectionAccess.StaticGetter<int>(worldGenType, "spawnHardBoss");
            _moonLordCountdown = ReflectionAccess.StaticGetter<int>(npcType, "MoonLordCountdown");
            _downedGolemBoss = ReflectionAccess.StaticGetter<bool>(npcType, "downedGolemBoss");
            _towerSolar = ReflectionAccess.StaticGetter<bool>(npcType, "TowerActiveSolar");
            _towerVortex = ReflectionAccess.StaticGetter<bool>(npcType, "TowerActiveVortex");
            _towerNebula = ReflectionAccess.StaticGetter<bool>(npcType, "TowerActiveNebula");
            _towerStardust = ReflectionAccess.StaticGetter<bool>(npcType, "TowerActiveStardust");
            // Reading this field is side-effect free. Never call
            // NPC.ShouldEmpressBeEnraged(), which mutates the Remix rage latch.
            _empressRageMode = ReflectionAccess.StaticGetter<bool>(npcType,
                "empressRageMode");

            _npcActive = ReflectionAccess.Getter<bool>(npcType, "active");
            _npcBoss = ReflectionAccess.Getter<bool>(npcType, "boss");
            _npcFriendly = ReflectionAccess.Getter<bool>(npcType, "friendly");
            _npcChaseable = ReflectionAccess.Getter<bool>(npcType, "chaseable");
            _npcInvulnerable = ReflectionAccess.Getter<bool>(npcType, "dontTakeDamage");
            _npcImmortal = ReflectionAccess.Getter<bool>(npcType, "immortal");
            _npcImmune = ReflectionAccess.Getter<int[]>(npcType, "immune");
            _npcCanBeChasedBy = CompileNpcCanBeChasedBy(npcType);
            _npcDamage = ReflectionAccess.Getter<int>(npcType, "damage");
            _npcLife = ReflectionAccess.Getter<int>(npcType, "life");
            _npcLifeMax = ReflectionAccess.Getter<int>(npcType, "lifeMax");
            _npcTimeLeft = ReflectionAccess.Getter<int>(npcType, "timeLeft");
            _npcTypeId = ReflectionAccess.Getter<int>(npcType, "type");
            _whoAmI = ReflectionAccess.Getter<int>(entityType, "whoAmI");
            _npcRealLife = ReflectionAccess.Getter<int>(npcType, "realLife");
            _npcGeneration = ReflectionAccess.PropertyGetter<byte>(npcType, "generation");
            _npcTarget = ReflectionAccess.Getter<int>(npcType, "target");
            _npcAi = ReflectionAccess.Getter<float[]>(npcType, "ai");
            _npcLocalAi = ReflectionAccess.Getter<float[]>(npcType, "localAI");

            _width = ReflectionAccess.Getter<int>(entityType, "width");
            _height = ReflectionAccess.Getter<int>(entityType, "height");
            _positionX = ReflectionAccess.VectorComponentGetter(entityType, "position", "X");
            _positionY = ReflectionAccess.VectorComponentGetter(entityType, "position", "Y");
            _velocityX = ReflectionAccess.VectorComponentGetter(entityType, "velocity", "X");
            _velocityY = ReflectionAccess.VectorComponentGetter(entityType, "velocity", "Y");

            _projectileActive = ReflectionAccess.Getter<bool>(projectileType, "active");
            _projectileHostile = ReflectionAccess.Getter<bool>(projectileType, "hostile");
            _projectileDamage = ReflectionAccess.Getter<int>(projectileType, "damage");
            _projectileTimeLeft = ReflectionAccess.Getter<int>(projectileType, "timeLeft");
            _projectileExtraUpdates = ReflectionAccess.Getter<int>(projectileType, "extraUpdates");
            _projectileAlpha = ReflectionAccess.Getter<int>(projectileType, "alpha");
            _pickAmmoItem = ReflectionAccess.MethodGetterWithArgument<object>(playerType, "PickAmmo_PickAmmoItem", itemType);
            // Metadata-audited pure arithmetic/getters in the hash-locked build.
            // Unlike PickAmmo, these do not consume RNG or ammunition.
            _weaponDamage = ReflectionAccess.MethodGetterWithArgument<int>(playerType, "GetWeaponDamage", itemType);
            _weaponDamageMultiplier = ReflectionAccess.MethodGetterWithArgument<float>(playerType, "GetWeaponDamageMultiplier", itemType);
            _projectileSample = ReflectionAccess.StaticIntDictionaryValueGetter(game.GetType("Terraria.ID.ContentSamples", true), "ProjectilesByType");
            _projectileTypeId = ReflectionAccess.Getter<int>(projectileType, "type");
            _projectileOwner = ReflectionAccess.Getter<int>(projectileType, "owner");
            _projectileBobber = ReflectionAccess.Getter<bool>(projectileType, "bobber");
            _projectileAi = ReflectionAccess.Getter<float[]>(projectileType, "ai");
            _projectileLocalAi = ReflectionAccess.Getter<float[]>(projectileType, "localAI");
            _projectileOldPosX = ReflectionAccess.VectorArrayComponentGetter(
                projectileType, "oldPos", "X");
            _projectileOldPosY = ReflectionAccess.VectorArrayComponentGetter(
                projectileType, "oldPos", "Y");
            _projectileOldPosLength = ReflectionAccess.ArrayLengthGetter(
                projectileType, "oldPos");
            _projectileScale = ReflectionAccess.Getter<float>(projectileType, "scale");
            _projectileRotation = ReflectionAccess.Getter<float>(projectileType, "rotation");

            _playerDead = ReflectionAccess.Getter<bool>(playerType, "dead");
            _playerActive = ReflectionAccess.Getter<bool>(playerType, "active");
            _releaseUseItem = ReflectionAccess.Getter<bool>(playerType, "releaseUseItem");
            _releaseQuickHeal = ReflectionAccess.Getter<bool>(playerType,
                "releaseQuickHeal");
            _releaseQuickMana = ReflectionAccess.Getter<bool>(playerType,
                "releaseQuickMana");
            _releaseJump = ReflectionAccess.Getter<bool>(playerType, "releaseJump");
            _releaseUp = ReflectionAccess.Getter<bool>(playerType, "releaseUp");
            _releaseDash = ReflectionAccess.Getter<bool>(playerType, "releaseDash");
            _nativeJumpReader = new NativeJumpReader(playerType);
            _nativeFlightReader = new NativeFlightReader(playerType, _nativeJumpReader);
            _nativeGrappleReader = new NativeGrappleReader(game, playerType);
            _nativeWitchBroomReader = new NativeWitchBroomReader(playerType,
                mountType, itemType);
            _playerItemAnimation = ReflectionAccess.Getter<int>(playerType, "itemAnimation");
            _playerItemTime = ReflectionAccess.Getter<int>(playerType, "itemTime");
            _playerUsingOrReusingItem = ReflectionAccess.PropertyGetter<bool>(
                playerType, "UsingOrReusingItem");
            _playerItemTimeIsZero = ReflectionAccess.PropertyGetter<bool>(
                playerType, "ItemTimeIsZero");
            _inventoryChestStack = ReflectionAccess.Getter<bool[]>(playerType, "inventoryChestStack");
            _mouseItem = ReflectionAccess.StaticGetter<object>(_mainType, "mouseItem");
            _drawingPlayerChat = ReflectionAccess.StaticGetter<bool>(_mainType, "drawingPlayerChat");
            _gamePaused = ReflectionAccess.StaticGetter<bool>(_mainType, "gamePaused");
            _setPlayerDirection = ReflectionAccess.Setter<int>(entityType, "direction");
            var dropPlayer = Expression.Parameter(typeof(object), "player");
            var dropMethod = playerType.GetMethod("DropSelectedItem", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (dropMethod == null) throw new MissingMethodException(playerType.FullName, "DropSelectedItem");
            _dropSelectedItem = Expression.Lambda<Action<object>>(
                Expression.Call(Expression.Convert(dropPlayer, playerType), dropMethod), dropPlayer).Compile();
            _playerLife = ReflectionAccess.Getter<int>(playerType, "statLife");
            _playerMaxLife = ReflectionAccess.Getter<int>(playerType, "statLifeMax2");
            _playerMana = ReflectionAccess.Getter<int>(playerType, "statMana");
            _playerMaxMana = ReflectionAccess.Getter<int>(playerType, "statManaMax2");
            _playerManaRegen = ReflectionAccess.Getter<int>(playerType,
                "manaRegen");
            _playerManaRegenCount = ReflectionAccess.Getter<int>(playerType,
                "manaRegenCount");
            _playerManaRegenDelay = ReflectionAccess.Getter<float>(playerType,
                "manaRegenDelay");
            _playerManaPotionDelay = ReflectionAccess.Getter<int>(playerType,
                "manaPotionDelay");
            _playerManaCost = ReflectionAccess.Getter<float>(playerType,
                "manaCost");
            _playerSpaceGun = ReflectionAccess.Getter<bool>(playerType,
                "spaceGun");
            _arrowStateKnown = HasInstanceField(playerType, "archery") &&
                HasInstanceField(playerType, "magicQuiver") &&
                HasInstanceField(playerType, "hasMoltenQuiver") &&
                HasInstanceField(playerType, "accSharpBarb") &&
                HasInstanceField(playerType, "accHarpyCharm");
            if (_arrowStateKnown)
            {
                _playerArchery = ReflectionAccess.Getter<bool>(playerType,
                    "archery");
                _playerMagicQuiver = ReflectionAccess.Getter<bool>(playerType,
                    "magicQuiver");
                _playerHasMoltenQuiver = ReflectionAccess.Getter<bool>(
                    playerType, "hasMoltenQuiver");
                _playerSharpBarb = ReflectionAccess.Getter<bool>(playerType,
                    "accSharpBarb");
                _playerHarpyCharm = ReflectionAccess.Getter<bool>(playerType,
                    "accHarpyCharm");
            }
            else
            {
                _playerArchery = ignored => false;
                _playerMagicQuiver = ignored => false;
                _playerHasMoltenQuiver = ignored => false;
                _playerSharpBarb = ignored => false;
                _playerHarpyCharm = ignored => false;
            }
            _playerManaSick = ReflectionAccess.Getter<bool>(playerType,
                "manaSick");
            _playerManaSickReduction = ReflectionAccess.Getter<float>(
                playerType, "manaSickReduction");
            _playerMaxMinions = ReflectionAccess.Getter<int>(playerType,
                "maxMinions");
            _playerSlotsMinions = ReflectionAccess.Getter<float>(playerType,
                "slotsMinions");
            _playerOwnedProjectileCounts = ReflectionAccess.Getter<int[]>(
                playerType, "ownedProjectileCounts");
            _playerSilence = ReflectionAccess.Getter<bool>(playerType,
                "silence");
            _playerNoItems = ReflectionAccess.Getter<bool>(playerType,
                "noItems");
            _playerCursed = ReflectionAccess.Getter<bool>(playerType,
                "cursed");
            _quickManaItem = ReflectionAccess.MethodGetter<object>(playerType,
                "QuickMana_GetItemToUse");
            _playerGravity = ReflectionAccess.Getter<float>(playerType, "gravity");
            _playerMaxFallSpeed = ReflectionAccess.Getter<float>(playerType, "maxFallSpeed");
            _playerMaxRunSpeed = ReflectionAccess.Getter<float>(playerType, "maxRunSpeed");
            _playerAccessoryRunSpeed = ReflectionAccess.Getter<float>(playerType, "accRunSpeed");
            _playerRunAcceleration = ReflectionAccess.Getter<float>(playerType, "runAcceleration");
            _playerRunSlowdown = ReflectionAccess.Getter<float>(playerType, "runSlowdown");
            _playerStrongestMoveSpeedDebuff = ReflectionAccess.Getter<float>(
                playerType, "strongestMoveSpeedDebuff");
            _playerSlow = ReflectionAccess.Getter<bool>(playerType, "slow");
            _playerWingsLogic = ReflectionAccess.Getter<int>(playerType, "wingsLogic");
            _playerJumpSpeedBoost = ReflectionAccess.Getter<float>(playerType, "jumpSpeedBoost");
            _playerWingTime = ReflectionAccess.Getter<float>(playerType, "wingTime");
            _playerWingTimeMax = ReflectionAccess.Getter<int>(playerType, "wingTimeMax");
            _playerRocketTime = ReflectionAccess.Getter<int>(playerType, "rocketTime");
            _playerRocketTimeMax = ReflectionAccess.Getter<int>(playerType, "rocketTimeMax");
            _playerRocketBoots = ReflectionAccess.Getter<int>(playerType, "rocketBoots");
            _playerDashType = ReflectionAccess.Getter<int>(playerType, "dashType");
            _playerDashDelay = ReflectionAccess.Getter<int>(playerType, "dashDelay");
            _playerDash = ReflectionAccess.Getter<int>(playerType, "dash");
            _playerDashTime = ReflectionAccess.Getter<int>(playerType, "dashTime");
            _playerTimeSinceLastDashStarted = ReflectionAccess.Getter<int>(playerType, "timeSinceLastDashStarted");
            _playerEocDash = ReflectionAccess.Getter<int>(playerType, "eocDash");
            _playerEocHit = ReflectionAccess.Getter<int>(playerType, "eocHit");
            _playerGrapCount = ReflectionAccess.Getter<int>(playerType, "grapCount");
            _playerGravDir = ReflectionAccess.Getter<float>(playerType, "gravDir");
            _playerGravControl = ReflectionAccess.Getter<bool>(playerType, "gravControl");
            _playerGravControl2 = ReflectionAccess.Getter<bool>(playerType, "gravControl2");
            _playerForcedGravity = ReflectionAccess.Getter<int>(playerType, "forcedGravity");
            _playerJumpTicks = ReflectionAccess.Getter<int>(playerType, "jump");
            _playerFallStart = ReflectionAccess.Getter<int>(playerType, "fallStart");
            _playerCCed = ReflectionAccess.PropertyGetter<bool>(playerType, "CCed");
            _playerPulley = ReflectionAccess.Getter<bool>(playerType, "pulley");
            _playerTongued = ReflectionAccess.Getter<bool>(playerType, "tongued");
            _playerOldStyleParkour = ReflectionAccess.Getter<bool>(playerType, "oldStyleParkour");
            _playerDirection = ReflectionAccess.Getter<int>(entityType, "direction");
            _playerBuffType = ReflectionAccess.Getter<int[]>(playerType, "buffType");
            _playerBuffTime = ReflectionAccess.Getter<int[]>(playerType, "buffTime");
            _playerSlowFall = ReflectionAccess.Getter<bool>(playerType, "slowFall");
            _playerReleaseMount = ReflectionAccess.Getter<bool>(playerType,
                "releaseMount");
            _playerDontHurtCritters = ReflectionAccess.Getter<bool>(playerType, "dontHurtCritters");
            _playerMount = ReflectionAccess.Getter<object>(playerType, "mount");
            _inventory = InstanceReferenceArray(playerType, "inventory");
            // These two native helpers are the same read-only path used by
            // Player.UpdateEquips. They account for unlocked slots and shared
            // effective loadout items without invoking any equip/update action.
            _getEffectiveArmor = ReflectionAccess.MethodGetterWithIntArgument<object>(playerType, "GetEffectiveArmor");
            _isItemSlotUnlockedAndUsable = ReflectionAccess.MethodGetterWithIntArgument<bool>(playerType, "IsItemSlotUnlockedAndUsable");
            // These vanilla helpers only select an item. Do not invoke the
            // action-producing QuickMount/QuickGrapple methods here.
            _quickMountItem = ReflectionAccess.MethodGetter<object>(playerType, "QuickMount_GetItemToUse");
            _quickGrappleItem = ReflectionAccess.MethodGetter<object>(playerType, "QuickGrapple_GetItemToUse");
            _selectedItem = ReflectionAccess.PropertyGetter<int>(playerType, "selectedItem");
            _setSelectedItem = ReflectionAccess.StructMethodSetter<int>(playerType, "selectedItemState", "Select");

            _zoneCorrupt = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneCorrupt");
            _zoneCrimson = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneCrimson");
            _zoneHallow = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneHallow");
            _zoneJungle = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneJungle");
            _zoneLihzhardTemple = ReflectionAccess.PropertyGetter<bool>(
                playerType, "ZoneLihzhardTemple");
            _zoneSnow = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneSnow");
            _zoneBeach = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneBeach");
            _zoneOverworld = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneOverworldHeight");
            _zoneUnderworld = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneUnderworldHeight");

            _mountActive = ReflectionAccess.PropertyGetter<bool>(mountType, "Active");
            _mountTypeId = ReflectionAccess.PropertyGetter<int>(mountType, "Type");
            _mountCanDismount = ReflectionAccess.MethodGetterWithArgument<bool>(
                mountType, "CanDismount", playerType);
            _mountFlyTime = ReflectionAccess.PropertyGetter<int>(mountType, "FlyTime");
            _mountRunSpeed = ReflectionAccess.PropertyGetter<float>(mountType, "RunSpeed");
            _mounts = StaticReferenceArray(mountType, "mounts");
            var mountDataType = mountType.GetNestedType("MountData", BindingFlags.Public | BindingFlags.NonPublic);
            _mountDataRunSpeed = ReflectionAccess.Getter<float>(mountDataType, "runSpeed");
            _mountDataFlightTime = ReflectionAccess.Getter<int>(mountDataType, "flightTimeMax");
            _mountDataUsesHover = ReflectionAccess.Getter<bool>(mountDataType, "usesHover");

            _itemTypeId = ReflectionAccess.Getter<int>(itemType, "type");
            _itemStack = ReflectionAccess.Getter<int>(itemType, "stack");
            _itemStackField = ReflectionAccess.Field(itemType, "stack");
            _itemFavorited = ReflectionAccess.Getter<bool>(itemType, "favorited");
            _itemFavoritedField = ReflectionAccess.Field(itemType, "favorited");
            _itemDamage = ReflectionAccess.Getter<int>(itemType, "damage");
            _itemUseTime = ReflectionAccess.Getter<int>(itemType, "useTime");
            _itemUseAnimation = ReflectionAccess.Getter<int>(itemType, "useAnimation");
            _itemReuseDelay = ReflectionAccess.Getter<int>(itemType, "reuseDelay");
            _itemBuffType = ReflectionAccess.Getter<int>(itemType, "buffType");
            _itemMana = ReflectionAccess.Getter<int>(itemType, "mana");
            _itemHealMana = ReflectionAccess.Getter<int>(itemType, "healMana");
            _itemAutoReuse = ReflectionAccess.Getter<bool>(itemType, "autoReuse");
            _itemChannel = ReflectionAccess.Getter<bool>(itemType, "channel");
            _itemUseStyle = ReflectionAccess.Getter<int>(itemType, "useStyle");
            _itemPick = ReflectionAccess.Getter<int>(itemType, "pick");
            _itemAxe = ReflectionAccess.Getter<int>(itemType, "axe");
            _itemHammer = ReflectionAccess.Getter<int>(itemType, "hammer");
            _itemCreateTile = ReflectionAccess.Getter<int>(itemType, "createTile");
            _itemShoot = ReflectionAccess.Getter<int>(itemType, "shoot");
            _itemShootSpeed = ReflectionAccess.Getter<float>(itemType, "shootSpeed");
            _itemAmmo = ReflectionAccess.Getter<int>(itemType, "ammo");
            _itemUseAmmo = ReflectionAccess.Getter<int>(itemType, "useAmmo");
            _itemFishingPole = ReflectionAccess.Getter<int>(itemType, "fishingPole");
            _itemBait = ReflectionAccess.Getter<int>(itemType, "bait");
            _itemMountType = ReflectionAccess.Getter<int>(itemType, "mountType");
            _itemWingSlot = ReflectionAccess.Getter<sbyte>(itemType, "wingSlot");
            _itemSetDefaults = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 &&
                             m.GetParameters()[0].ParameterType == typeof(int));

            _tileType = ReflectionAccess.Getter<ushort>(tileType, "type");
            _tileActive = ReflectionAccess.MethodGetter<bool>(tileType, "active");
            _tileInactive = ReflectionAccess.MethodGetter<bool>(tileType, "inActive");
            _tileSlope = ReflectionAccess.MethodGetter<byte>(tileType, "slope");
            _tileHalfBrick = ReflectionAccess.MethodGetter<bool>(tileType, "halfBrick");
            _tileWater = ReflectionAccess.MethodGetter<bool>(tileType, "water");
            _tileLava = ReflectionAccess.MethodGetter<bool>(tileType, "lava");
            var liquidField = ReflectionAccess.Field(tileType, "liquid");
            _tileSolid = ReflectionAccess.StaticGetter<bool[]>(_mainType, "tileSolid")();
            _tileSolidTop = ReflectionAccess.StaticGetter<bool[]>(_mainType, "tileSolidTop")();
            _projectileHooks = ReflectionAccess.StaticGetter<bool[]>(_mainType, "projHook")();

            _screenX = ReflectionAccess.StaticVectorComponentGetter(_mainType, "screenPosition", "X");
            _screenY = ReflectionAccess.StaticVectorComponentGetter(_mainType, "screenPosition", "Y");
            _screenHeight = ReflectionAccess.StaticGetter<int>(_mainType, "screenHeight");
            _mouseX = ReflectionAccess.StaticSetter<int>(_mainType, "mouseX");
            _mouseY = ReflectionAccess.StaticSetter<int>(_mainType, "mouseY");
            _readMouseX = ReflectionAccess.StaticGetter<int>(_mainType, "mouseX");
            _readMouseY = ReflectionAccess.StaticGetter<int>(_mainType, "mouseY");
            _releaseThrow = ReflectionAccess.Field(playerType, "releaseThrow");
            _controls = new Dictionary<string, Action<object, bool>>();
            foreach (var name in new[] { "controlLeft", "controlRight", "controlUp", "controlDown", "controlJump",
                "controlUseItem", "controlUseTile", "controlHook", "controlQuickHeal", "controlQuickMana",
                "controlThrow", "controlMount", "controlDash" })
            {
                _controls[name] = ReflectionAccess.Setter<bool>(playerType, name);
                _controlReaders[name] = ReflectionAccess.Getter<bool>(playerType, name);
                _capturedControls[name] = false;
            }

            _tileAt = ReflectionAccess.ArrayElementGetter2D(ReflectionAccess.Field(_mainType, "tile").FieldType);
            _canHitLine = CompileLineOfSight(game, entityType);
            _razorbladeSpawnCenter = CompileRazorbladeSpawnCenter(playerType);
            _razorbladeCanHit = CompileRazorbladeCanHit(game, entityType);

            _newText = _mainType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "NewText" && ParametersMatch(m, typeof(string), typeof(byte), typeof(byte), typeof(byte)));
            _liquidField = liquidField;
        }

        public bool IsLocalPlayer(object player, int index) => index == _myPlayer() && _playerActive(player) && !_gameMenu();
        public bool IsDead(object player) => _playerDead(player);
        public bool IsInputBlocked => _blockInput();
        public int GetNpcType(object npc) => _npcTypeId(npc);
        public int GetSelectedItem(object player) => _selectedItem(player);
        public bool CanChangeSelectedItemImmediately(object player) =>
            !_playerUsingOrReusingItem(player) && _playerItemTimeIsZero(player);
        public void BeginInputFrame()
        {
            _requestedSelection = -1;
            ClearPendingMobilityValidation();
            // Activation preflight and same-frame waiting survival can both read
            // a snapshot. Share one budget, rather than giving each three rays.
            _sightQueryBudget = 3;
            _sightQueryState.BeginFrame();
            if (_destroyerMotionFrame >= int.MaxValue - 1)
            {
                _destroyerMotionHistory.Clear();
                _destroyerMotionFrame = 1;
            }
            else _destroyerMotionFrame++;
        }
        public void SetSelectedItem(object player, int slot)
        {
            _requestedSelection = slot;
            _setSelectedItem(player, slot);
        }
        public bool IsBoss(object npc) => npc != null && _npcBoss(npc);

        public bool TryGetSessionIdentity(out object worldToken,
            out Guid uniqueId, out int worldId, out int netMode,
            out int localPlayerIndex)
        {
            worldToken = null;
            uniqueId = Guid.Empty;
            worldId = 0;
            netMode = _netMode();
            localPlayerIndex = _myPlayer();
            var world = _activeWorldFileData();
            if (world == null || netMode < 0 || netMode > 2 ||
                localPlayerIndex < 0 || localPlayerIndex >= 255)
                return false;
            worldToken = world;
            uniqueId = _worldUniqueId(world);
            worldId = _worldFileId(world);
            return uniqueId != Guid.Empty;
        }

        public EncounterObservation BuildObservation(object player, IList<int> killedBosses,
            bool startAuthorized = false, bool waitingStillValid = true)
        {
            var activeBosses = _activeBossKeys;
            activeBosses.Clear();
            var activeBossGenerations = _activeBossGenerations;
            activeBossGenerations.Clear();
            var activeBossTypes = _activeBossTypes;
            activeBossTypes.Clear();
            var npcs = _npcs();
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc != null && _npcActive(npc))
                {
                    int type = _npcTypeId(npc);
                    if (!SupportedBossPolicy.IsEncounterBossRoot(type,
                            _npcBoss(npc)))
                        continue;
                    var entityKey = _whoAmI(npc);
                    var realLife = _npcRealLife(npc);
                    var supported =
                        SupportedBossPolicy.IsSupportedBossType(type);
                    var canonicalIdentity = entityKey == i &&
                        (!supported ||
                         (realLife == -1 && BossKey(npc) == i));
                    // Publish every qualifying native NPC by its array slot.
                    // Unsupported multi-part families are deliberately not
                    // collapsed: any one of them must keep admission closed.
                    var key = i;
                    var generation = -1;
                    if (canonicalIdentity)
                        generation = _npcGeneration(npc);
                    activeBosses.Add(key);
                    activeBossGenerations.Add(generation);
                    // Keep this list parallel with ActiveBossKeys. Repeated
                    // type values intentionally represent distinct roots.
                    activeBossTypes.Add(type);
                }
            }
            _observation.Flags = activeBosses.Count > 0 ? EncounterFlags.Boss : EncounterFlags.None;
            _observation.PlayerDead = _playerDead(player);
            _observation.PlayerLife = _playerLife(player);
            _observation.ActiveBossKeys = activeBosses;
            _observation.ActiveBossGenerations = activeBossGenerations;
            _observation.ActiveBossTypes = activeBossTypes;
            _observation.KilledBossKeys = killedBosses ?? Array.Empty<int>();
            _observation.StartAuthorized = startAuthorized;
            _observation.WaitingStillValid = waitingStillValid;
            _observation.RequirePreparation = false;
            _observation.ExpectedBossArrived = true;
            return _observation;
        }

        public int BossKey(object npc)
        {
            var root = _npcRealLife(npc);
            return root >= 0 ? root : _whoAmI(npc);
        }

        public bool TryGetSupportedBossIdentity(object npc, out int key,
            out int generation, out int type)
        {
            key = -1;
            generation = -1;
            type = 0;
            if (npc == null)
                return false;
            type = _npcTypeId(npc);
            if (!SupportedBossPolicy.IsSupportedBossType(type) ||
                !_npcBoss(npc) || _npcRealLife(npc) != -1)
                return false;
            key = _whoAmI(npc);
            var npcs = _npcs();
            if (key < 0 || key >= npcs.Length ||
                !ReferenceEquals(npcs[key], npc) || BossKey(npc) != key)
                return false;
            generation = _npcGeneration(npc);
            return generation > 0;
        }

        public BossStartPlan FindBossStartPlan(object player)
        {
            // Keep the generic selector here so an unsupported leftmost
            // summon is still identified and refused with the production
            // scope cue before it can be consumed. The Runtime admission
            // gate, not this pure selector, owns the live allowlist.
            return BossStartPlanner.Select(BuildBossStartContext(player));
        }

        private BossStartContext BuildBossStartContext(object player)
        {
            var result = new BossStartContext
            {
                DayTime = _dayTime(),
                Time = _time(),
                HardMode = _hardMode(),
                ZoneCorrupt = _zoneCorrupt(player),
                ZoneCrimson = _zoneCrimson(player),
                ZoneHallow = _zoneHallow(player),
                ZoneJungle = _zoneJungle(player),
                ZoneSnow = _zoneSnow(player),
                ZoneBeach = _zoneBeach(player),
                ZoneOverworld = _zoneOverworld(player),
                ZoneUnderworld = _zoneUnderworld(player),
                ZenithWorld = _zenithWorld(),
                DownedGolemBoss = _downedGolemBoss(),
                CritterProtection = _playerDontHurtCritters(player),
                SpawnEyeScheduled = _spawnEye(),
                SpawnHardBoss = _spawnHardBoss(),
                MoonLordCountdown = _moonLordCountdown(),
                LunarPillarsActive = _towerSolar() || _towerVortex() || _towerNebula() || _towerStardust(),
                BlockingInvasion = _invasionType() > 0 || _pumpkinMoon() || _snowMoon()
            };

            var npcs = _npcs();
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null || !_npcActive(npc))
                    continue;
                var type = _npcTypeId(npc);
                if (SupportedBossPolicy.IsEncounterBossRoot(type,
                        _npcBoss(npc)))
                    result.ActiveBossTypes.Add(type);
                if (type == 22) result.GuideAlive = true;
                if (type == 437) result.MysteriousTabletActive = true;
                if (type == 439) result.CultistActive = true;
            }

            var items = _inventory(player);
            for (var slot = 0; slot < Math.Min(10, items.Length); slot++)
            {
                var item = items[slot];
                if (item == null || _itemTypeId(item) <= 0 || _itemStack(item) <= 0)
                    continue;
                result.Hotbar.Add(new HotbarItemSnapshot { Slot = slot, Type = _itemTypeId(item), Stack = _itemStack(item) });
                if (result.FishingRodHotbarSlot < 0 && _itemFishingPole(item) > 0)
                    result.FishingRodHotbarSlot = slot;
            }

            FindStartInteractionTargets(player, result);
            return result;
        }

        public CombatSnapshot BuildCombatSnapshot(object player, bool useBestHotbarWeapon = false)
        {
            // Borrowed synchronous view: Runtime consumes it before the next
            // tick. Reuse the large target/threat buffers instead of allocating
            // roughly 15 KB of list backing arrays on every game update.
            var snapshot = _combatSnapshot;
            if (++_sightFrame == int.MaxValue) { _sightCache.Clear(); _sightFrame = 1; }
            snapshot.Targets.Clear();
            snapshot.Threats.Clear();
            var priorityBoss = snapshot.PriorityBoss;
            priorityBoss.Clear();
            PopulatePriorityWorldContext(priorityBoss, _worldSurface(),
                _maxTilesX(), _wofDrawAreaTop(), _wofDrawAreaBottom(),
                _empressRageMode());
            var underworldLayer = _underworldLayer();
            priorityBoss.UnderworldLayerTiles = underworldLayer;
            priorityBoss.UnderworldLayerKnown = underworldLayer > 0 &&
                underworldLayer < _maxTilesY();
            snapshot.NetMode = _netMode();
            snapshot.LocalPlayerIndex = _myPlayer();
            snapshot.NativeContextKnown = snapshot.NetMode >= 0 && snapshot.NetMode <= 2 &&
                snapshot.LocalPlayerIndex >= 0 && snapshot.LocalPlayerIndex < 255;
            var px = _positionX(player);
            var py = _positionY(player);
            var vx = _velocityX(player);
            var vy = _velocityY(player);
            var state = snapshot.Player;
            state.Position = new Vec2(px, py);
            state.Velocity = new Vec2(vx, vy);
            state.Width = _width(player);
            state.Height = _height(player);
            state.Life = _playerLife(player);
            state.MaxLife = _playerMaxLife(player);
            state.Mana = _playerMana(player);
            state.MaxMana = _playerMaxMana(player);
            state.Gravity = _playerGravity(player);
            state.MaxFallSpeed = _playerMaxFallSpeed(player);
            // Running boots raise accRunSpeed, not the base maxRunSpeed. Reading
            // only the latter rejects valid gear and underpredicts actual sprinting.
            state.MaxRunSpeed = Math.Max(_playerMaxRunSpeed(player), _playerAccessoryRunSpeed(player));
            state.RunAcceleration = _playerRunAcceleration(player);
            state.BaseRunSpeed = _playerMaxRunSpeed(player);
            state.SprintAcceleration = _playerDashDelay(player) < 0 ? 0f : state.RunAcceleration * (_playerWingsLogic(player) > 0 ? .4f : .2f);
            state.RunSlowdown = _playerRunSlowdown(player);
            state.MoveSpeedDebuffFactor =
                _playerStrongestMoveSpeedDebuff(player);
            state.MoveSpeedDebuffFactorKnown = IsFinite(
                state.MoveSpeedDebuffFactor) &&
                state.MoveSpeedDebuffFactor > 0f &&
                state.MoveSpeedDebuffFactor <= 1f;
            bool slowDebuffActive;
            state.SlowDebuffKnown = TryReadActiveBuff(player, 32,
                out slowDebuffActive);
            state.SlowDebuffActive =
                BossMobilityCapabilityEvaluator.IsModeledSlowEffectPresent(
                    state.SlowDebuffKnown, slowDebuffActive,
                    _playerSlow(player));
            state.JumpSpeedBoost = _playerJumpSpeedBoost(player);
            state.WingTime = _playerWingTime(player);
            state.RocketTime = _playerRocketTime(player);
            state.ZoneCorruptKnown = true;
            state.ZoneCorrupt = _zoneCorrupt(player);
            state.ZoneCrimsonKnown = true;
            state.ZoneCrimson = _zoneCrimson(player);
            state.ZoneJungleKnown = true;
            state.ZoneJungle = _zoneJungle(player);
            state.ZoneLihzhardTempleKnown = true;
            state.ZoneLihzhardTemple = _zoneLihzhardTemple(player);
            bool oneWayFooting;
            var hasFooting = HasFooting(state, _playerGravDir(player) < 0, out oneWayFooting);
            state.OnGround = Math.Abs(vy) < .01f && hasFooting;
            state.OnOneWaySupport = state.OnGround && oneWayFooting;
            state.Dead = _playerDead(player);
            state.WorldLeft = 16f;
            state.WorldRight = _maxTilesX() * 16f - 16f;
            state.WorldTop = 16f;
            state.WorldBottom = _maxTilesY() * 16f - 16f;

            var mount = _playerMount(player);
            var mountActive = mount != null && _mountActive(mount);
            state.Flight = _nativeFlightReader.Read(player, mountActive, out state.Jump);
            var functionalIdentity = ReadFunctionalEquipmentIdentity(player, state);
            var items = _inventory(player);
            float bestMountSpeed;
            bool bestMountCanFly;
            int selectedMountItemType;
            int selectedMountType;
            var hasMount = ReadAvailableMount(player, out bestMountSpeed,
                out bestMountCanFly, out selectedMountItemType,
                out selectedMountType);
            var mobility = snapshot.Mobility;
            mobility.MountActive = mountActive;
            mobility.FormulaAccessoryScanKnown = true;
            mobility.UnexpectedFormulaMobilityItemType =
                functionalIdentity.UnexpectedFormulaMobilityItemType;
            PublishMountIdentities(mobility, mountActive,
                mountActive ? _mountTypeId(mount) : -1, hasMount,
                selectedMountItemType, selectedMountType);
            // CanDismount is the native read-only collision probe used by
            // QuickMount before it would mutate mount/player state. It is only
            // sampled for an active mount, and is consumed solely by the
            // one-shot active-encounter handoff.
            mobility.ActiveMountDismountProbeKnown = mountActive && mount != null;
            mobility.ActiveMountCanDismount = mountActive && mount != null &&
                _mountCanDismount(mount, player);
            mobility.ActiveMountReleaseReady = _playerReleaseMount(player);
            mobility.MountCanFly = mountActive ?
                MountTypeCanFly(mobility.ActiveMountType) : bestMountCanFly;
            state.CanSprintInAir = _playerWingsLogic(player) > 0 || (mountActive && mobility.MountCanFly);
            mobility.HasUsableMount = hasMount;
            mobility.MountRunSpeed = mountActive ? _mountRunSpeed(mount) : bestMountSpeed;
            mobility.HasGrapple = _quickGrappleItem(player) != null;
            mobility.Grappling = _playerGrapCount(player) > 0;
            if (++_grappleFrameSequence <= 0) _grappleFrameSequence = 1;
            mobility.BasicHook = _nativeGrappleReader.ReadAtPlayerUpdateEntry(
                player, _grappleFrameSequence, state.OnGround);
            var hookInFlight = HasOwnedHookProjectile(_myPlayer());
            // Fit/open-path evidence is not guessed here. These observations
            // are sufficient for exact active identity admission; route code
            // must add collision evidence before emitting a mount edge.
            mobility.WitchBroomToggle = _nativeWitchBroomReader.ReadToggle(
                player, false, false, hookInFlight);
            mobility.WitchBroomMotion = _nativeWitchBroomReader.ReadMotion(
                player, false, false, hookInFlight);
            mobility.GravityInverted = _playerGravDir(player) < 0f;
            mobility.FeatherFall = _playerSlowFall(player);
            bool featherFallPotionActive;
            mobility.FeatherFallPotionKnown = TryReadActiveBuff(player, 8,
                out featherFallPotionActive);
            mobility.FeatherFallPotionActive = mobility.FeatherFallPotionKnown &&
                featherFallPotionActive;
            mobility.GravityFlip = ReadGravityFlipState(player, mountActive, functionalIdentity.Gravity);
            mobility.EyeShieldDash = ReadEyeShieldDashState(player, mountActive, functionalIdentity.Dash);
            var tilesForDashProbe = _tiles();
            ReadDashForwardProbe(player, tilesForDashProbe, -1,
                out mobility.DashLeftProbeKnown, out mobility.DashLeftProbeBlocked);
            ReadDashForwardProbe(player, tilesForDashProbe, 1,
                out mobility.DashRightProbeKnown, out mobility.DashRightProbeBlocked);
            mobility.CanDash = mobility.EyeShieldDash.EquipmentIdentity ==
                DashEquipmentIdentity.ShieldOfCthulhuItem3097 && mobility.EyeShieldDash.DashType == 2;
            // Vanilla copies dashType into dash only when delay is exactly zero.
            // -1 is the active dash phase, never a second ready edge.
            mobility.DashReady = mobility.EyeShieldDash.DashDelay == 0;
            mobility.DashType = mobility.EyeShieldDash.DashType;
            mobility.CanFlipGravity = mobility.GravityFlip.Identity != GravityControlIdentity.Unknown &&
                (mobility.GravityFlip.GravControl || mobility.GravityFlip.GravControl2);
            mobility.HasFiniteFlightResource = HasFiniteFlightResource(player);
            // Reviewed wing/boot combinations have convertible resources, not
            // interchangeable counters. Unknown equipment keeps the old estimate.
            mobility.FlightResourceFraction = state.Flight.Known ?
                FlightMotion.ResourceFraction(in state.Flight) : FlightResource(player);
            var difficulty = snapshot.Difficulty;
            difficulty.GameMode = _gameMode();
            difficulty.GameModeKnown = difficulty.GameMode >= 0 && difficulty.GameMode <= 3;
            difficulty.Journey = difficulty.GameMode == 3;
            difficulty.Expert = _expertMode();
            difficulty.Master = _masterMode();
            difficulty.Drunk = _drunkWorld();
            difficulty.NotTheBees = _notTheBeesWorld();
            difficulty.ForTheWorthy = _goodWorld();
            difficulty.Remix = _remixWorld();
            difficulty.Zenith = _zenithWorld();
            difficulty.Celebration = _celebrationWorld();
            difficulty.Constant = _constantWorld();
            difficulty.NoTraps = _noTrapsWorld();
            difficulty.Skyblock = _skyblockWorld();
            difficulty.DayTime = _dayTime();

            snapshot.SummonWhipOutput = ReadSummonWhipOutput(player, items);
            // Target evidence has to be captured before automatic weapon
            // selection.  A finite, source-locked trajectory is not useful
            // merely because its paper DPS is high: it must be able to reach
            // the same visible primary target the planner will engage.
            //
            // Read the currently selected route first because target capture
            // also owns the one optional Razorblade Typhoon pre-fire read.
            // Candidate selection consumes only that already-captured
            // evidence; it never performs another broad NPC scan.
            var selectedSlot = Math.Max(0, Math.Min(items.Length - 1,
                GetSelectedItem(player)));
            ReadWeaponInto(player, items, selectedSlot, snapshot.Weapon);
            snapshot.Arena = ReadArena(snapshot.Player,
                mobility.GravityInverted);
            ReadTargetsAndThreats(player, snapshot);
            if (useBestHotbarWeapon)
            {
                var slot = FindBestWeaponSlot(player, snapshot.Targets,
                    snapshot.Player.Center, snapshot.Weapon);
                if (slot != selectedSlot)
                    ReadWeaponInto(player, items, slot, snapshot.Weapon);
            }
            snapshot.LineOfSightToPrimary = true;
            return snapshot;
        }

        private SummonWhipOutputObservation ReadSummonWhipOutput(
            object player, object[] items)
        {
            var observation = default(SummonWhipOutputObservation);
            if (player == null || items == null) return observation;

            object staffItem = null;
            object whipItem = null;
            VanillaSummonStaffOutputProfile staffProfile = null;
            VanillaWhipOutputProfile whipProfile = null;
            var staffSlot = -1;
            var whipSlot = -1;
            var limit = Math.Min(10, items.Length);
            for (var slot = 0; slot < limit; slot++)
            {
                var item = items[slot];
                if (item == null || _itemStack(item) <= 0) continue;
                var itemId = _itemTypeId(item);
                VanillaSummonStaffOutputProfile staff;
                if (VanillaSummonWhipOutputCatalog.TryGetStaff(itemId,
                        out staff))
                {
                    // More than one reviewed staff is not an unambiguous pair;
                    // do not silently select one by paper DPS or slot order.
                    if (staffSlot >= 0) return observation;
                    staffSlot = slot;
                    staffItem = item;
                    staffProfile = staff;
                }
                VanillaWhipOutputProfile whip;
                if (VanillaSummonWhipOutputCatalog.TryGetWhip(itemId,
                        out whip))
                {
                    if (whipSlot >= 0) return observation;
                    whipSlot = slot;
                    whipItem = item;
                    whipProfile = whip;
                }
            }
            if (staffItem == null || whipItem == null || staffProfile == null ||
                whipProfile == null || staffSlot == whipSlot)
                return observation;

            observation.Known = true;
            observation.Staff = new SummonWhipItemObservation
            {
                Known = true,
                Slot = staffSlot,
                Stack = _itemStack(staffItem),
                ItemId = _itemTypeId(staffItem),
                ProjectileId = _itemShoot(staffItem),
                BuffId = _itemBuffType(staffItem)
            };
            observation.Whip = new SummonWhipItemObservation
            {
                Known = true,
                Slot = whipSlot,
                Stack = _itemStack(whipItem),
                ItemId = _itemTypeId(whipItem),
                ProjectileId = _itemShoot(whipItem),
                BuffId = _itemBuffType(whipItem)
            };
            observation.SelectedSlot = GetSelectedItem(player);
            observation.ReleaseUseItem = _releaseUseItem(player);
            observation.ItemAnimation = _playerItemAnimation(player);
            observation.ItemTime = _playerItemTime(player);
            observation.MaximumMinionSlots = _playerMaxMinions(player);
            observation.UsedMinionSlots = _playerSlotsMinions(player);
            observation.MinionCapacityKnown = true;

            bool buffActive;
            observation.BuffStateKnown = TryReadActiveBuff(player,
                staffProfile.BuffId, out buffActive);
            observation.ExpectedStaffBuffActive =
                observation.BuffStateKnown && buffActive;
            int ownerCount;
            observation.OwnerProjectileCountKnown =
                TryReadOwnedProjectileCount(player, staffProfile,
                    out ownerCount);
            observation.OwnerProjectileCount = ownerCount;

            observation.WhipEffectiveDamage = _weaponDamage(player, whipItem);
            observation.WhipUseAnimation = _itemUseAnimation(whipItem);
            observation.WhipUseTime = _itemUseTime(whipItem);
            observation.WhipOutputKnown = true;
            observation.CanUseItem = CanUseSummonWhipItem(player, items,
                staffItem, whipItem, staffSlot, whipSlot,
                observation.SelectedSlot);
            return observation;
        }

        private bool TryReadOwnedProjectileCount(object player,
            VanillaSummonStaffOutputProfile profile, out int count)
        {
            count = 0;
            var owned = _playerOwnedProjectileCounts(player);
            if (owned == null || profile == null) return false;
            long total = 0;
            for (var index = 0; index < profile.OwnerProjectileTypeCount;
                index++)
            {
                var projectileType = profile.OwnerProjectileIdAt(index);
                if (projectileType <= 0 || projectileType >= owned.Length ||
                    owned[projectileType] < 0)
                    return false;
                total += owned[projectileType];
                if (total > int.MaxValue) return false;
            }
            count = (int)total;
            return true;
        }

        private bool CanUseSummonWhipItem(object player, object[] items,
            object staffItem, object whipItem, int staffSlot, int whipSlot,
            int selectedSlot)
        {
            if (!_playerActive(player) || _playerDead(player) ||
                _playerCCed(player) || _playerNoItems(player) ||
                _playerCursed(player) || _drawingPlayerChat() ||
                _gamePaused())
                return false;
            var chestStack = _inventoryChestStack(player);
            if (chestStack == null || staffSlot >= chestStack.Length ||
                whipSlot >= chestStack.Length || chestStack[staffSlot] ||
                chestStack[whipSlot] || IsNonEmpty(_mouseItem()) ||
                items.Length > 58 && IsNonEmpty(items[58]))
                return false;

            var selected = selectedSlot == staffSlot ? staffItem :
                selectedSlot == whipSlot ? whipItem : null;
            if (selected == null || _itemMana(selected) <= 0) return true;
            var manaMultiplier = _playerManaCost(player);
            var scaledMana = _itemMana(selected) * manaMultiplier;
            return !_playerSilence(player) && manaMultiplier >= 0f &&
                IsFinite(scaledMana) && scaledMana < int.MaxValue &&
                _playerMana(player) >= (int)scaledMana;
        }

        private FunctionalMobilityIdentity ReadFunctionalEquipmentIdentity(object player, PlayerSnapshot state)
        {
            var wingType = 0;
            var wingSources = 0;
            var rocketType = 0;
            var rocketSources = 0;
            var gravityGlobeSources = 0;
            var shieldSources = 0;
            var conflictingDashSources = 0;
            var unexpectedFormulaMobilityItemType = 0;
            // Player.UpdateEquips applies functional accessories from slots
            // 3..9 after IsItemSlotUnlockedAndUsable and GetEffectiveArmor.
            // Mirror that read-only identity path; never call ApplyEquipFunctional.
            for (var slot = 3; slot < 10; slot++)
            {
                if (!_isItemSlotUnlockedAndUsable(player, slot)) continue;
                var item = _getEffectiveArmor(player, slot);
                if (item == null) continue;
                var type = _itemTypeId(item);
                if (type <= 0) continue;
                if (_itemWingSlot(item) > 0)
                {
                    wingSources++;
                    wingType = type;
                }
                if (IsRocketBootSource(type))
                {
                    rocketSources++;
                    rocketType = type;
                }
                if (type == 1131) gravityGlobeSources++;
                if (type == 3097) shieldSources++;
                else if (type == 977 || type == 984) conflictingDashSources++;
                if (FormulaMobilityContract.IsMobilityAccessory(type) &&
                    type != FormulaMobilityContract.DemonWingsItem &&
                    type != FormulaMobilityContract.LightningBootsItem &&
                    type != FormulaMobilityContract.ShieldOfCthulhuItem &&
                    unexpectedFormulaMobilityItemType == 0)
                    unexpectedFormulaMobilityItemType = type;
            }
            state.FunctionalEquipmentIdentityKnown = true;
            state.WingAccessoryItemType = wingSources == 0 ? 0 : wingSources == 1 ? wingType : -1;
            state.RocketBootAccessoryItemType = rocketSources == 0 ? 0 : rocketSources == 1 ? rocketType : -1;

            bool potion;
            var buffIdentityKnown = TryReadActiveGravitationBuff(player, out potion);
            var gravity = GravityControlIdentity.Unknown;
            if (buffIdentityKnown && gravityGlobeSources <= 1)
            {
                if (potion && gravityGlobeSources == 1)
                    gravity = GravityControlIdentity.GravitationBuff18AndGravityGlobe1131;
                else if (potion)
                    gravity = GravityControlIdentity.GravitationBuff18;
                else if (gravityGlobeSources == 1)
                    gravity = GravityControlIdentity.GravityGlobeItem1131;
            }
            var dash = shieldSources == 1 && conflictingDashSources == 0
                ? DashEquipmentIdentity.ShieldOfCthulhuItem3097
                : DashEquipmentIdentity.Unknown;
            return new FunctionalMobilityIdentity
            {
                Gravity = gravity,
                Dash = dash,
                UnexpectedFormulaMobilityItemType =
                    unexpectedFormulaMobilityItemType
            };
        }

        private bool TryReadActiveGravitationBuff(object player, out bool active)
        {
            return TryReadActiveBuff(player, 18, out active);
        }

        private bool TryReadActiveBuff(object player, int buffType, out bool active)
        {
            return TryReadActiveBuff(_playerBuffType(player), _playerBuffTime(player),
                buffType, out active);
        }

        private static bool TryReadActiveBuff(int[] types, int[] times, int buffType,
            out bool active)
        {
            bool present;
            int remainingTicks;
            var known = TryReadBuffSlot(types, times, buffType, out present,
                out remainingTicks);
            active = known && present && remainingTicks > 0;
            return known;
        }

        private static bool TryReadBuffSlot(int[] types, int[] times, int buffType,
            out bool present, out int remainingTicks)
        {
            present = false;
            remainingTicks = 0;
            if (buffType <= 0 || types == null || times == null ||
                types.Length == 0 || types.Length != times.Length) return false;
            for (var index = 0; index < types.Length; index++)
            {
                if (types[index] != buffType) continue;
                // Duplicate identities are not a valid vanilla local-player
                // buff array. Do not choose an arbitrary lineage.
                if (present) return false;
                present = true;
                remainingTicks = times[index];
            }
            return true;
        }

        private GravityFlipState ReadGravityFlipState(object player, bool mountActive,
            GravityControlIdentity identity)
        {
            return new GravityFlipState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                Identity = identity,
                GravControl = _playerGravControl(player),
                GravControl2 = _playerGravControl2(player),
                ForcedGravity = _playerForcedGravity(player),
                MountActive = mountActive,
                ControlUp = _controlReaders["controlUp"](player),
                ReleaseUp = _releaseUp(player),
                GravityDirection = _playerGravDir(player),
                PositionY = _positionY(player),
                VelocityY = _velocityY(player),
                JumpTicks = _playerJumpTicks(player),
                FallStart = _playerFallStart(player)
            };
        }

        private EyeShieldDashState ReadEyeShieldDashState(object player, bool mountActive,
            DashEquipmentIdentity identity)
        {
            return new EyeShieldDashState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                EquipmentIdentity = identity,
                DashType = _playerDashType(player),
                Dash = _playerDash(player),
                DashDelay = _playerDashDelay(player),
                DashTime = _playerDashTime(player),
                TimeSinceLastDashStarted = _playerTimeSinceLastDashStarted(player),
                EocDash = _playerEocDash(player),
                EocHit = _playerEocHit(player),
                CrowdControlled = _playerCCed(player),
                MountActive = mountActive,
                Pulley = _playerPulley(player),
                Grappling = _playerGrapCount(player) > 0,
                Tongued = _playerTongued(player),
                OldStyleParkour = _playerOldStyleParkour(player),
                ControlDash = _controlReaders["controlDash"](player),
                ReleaseDash = _releaseDash(player),
                ControlLeft = _controlReaders["controlLeft"](player),
                ControlRight = _controlReaders["controlRight"](player),
                FacingDirection = _playerDirection(player),
                VelocityX = _velocityX(player),
                VelocityY = _velocityY(player),
                AccRunSpeed = _playerAccessoryRunSpeed(player),
                MaxRunSpeed = _playerMaxRunSpeed(player),
                HostileContactKnown = true,
                HostileContact = false
            };
        }

        private void ReadDashForwardProbe(object player, Array tiles, int direction,
            out bool known, out bool blocked)
        {
            known = false;
            blocked = false;
            var width = _width(player);
            var height = _height(player);
            var gravityDirection = _playerGravDir(player);
            var positionX = _positionX(player);
            var positionY = _positionY(player);
            if ((direction != -1 && direction != 1) || width <= 0 || height <= 0 ||
                (gravityDirection != -1f && gravityDirection != 1f) ||
                !IsFinite(positionX) || !IsFinite(positionY)) return;

            // Exact 1.4.5.8 DashMovement probes: integer width/2 on the leading
            // edge, one near the gravity-opposite edge and one at body center.
            var centerX = positionX + width * .5f;
            var centerY = positionY + height * .5f;
            var probeX = centerX + direction * width / 2 + 2;
            var edgeY = centerY + gravityDirection * -height / 2f + gravityDirection * 2f;
            var firstX = ((int)probeX) >> 4;
            var firstY = ((int)edgeY) >> 4;
            var secondX = firstX;
            var secondY = ((int)centerY) >> 4;
            if (firstX < 0 || secondX < 0 || firstY < 0 || secondY < 0 ||
                firstX >= _maxTilesX() || secondX >= _maxTilesX() ||
                firstY >= _maxTilesY() || secondY >= _maxTilesY()) return;
            known = true;
            blocked = IsSolid(tiles, firstX, firstY, false) || IsSolid(tiles, secondX, secondY, false);
        }

        private static bool IsRocketBootSource(int itemType)
        {
            // All 1.4.5.8 ApplyEquipFunctional branches which write
            // Player.rocketBoots. Keeping the complete source set lets an exact
            // Lightning-Boots fixture reject Spectre/Frostspark/Terraspark and
            // any simultaneous second rocket effect instead of trusting the
            // aggregate integer profile alone.
            switch (itemType)
            {
                case 128:
                case 405:
                case 898:
                case 1862:
                case 3993:
                case 4874:
                case 5000:
                    return true;
                default:
                    return false;
            }
        }

        private void ReadTargetsAndThreats(object player, CombatSnapshot snapshot)
        {
            var playerCenter = snapshot.Player.Center;
            var maxTargetDistanceSquared = _config.MaximumTargetDistancePixels * (float)_config.MaximumTargetDistancePixels;
            var npcs = _npcs();
            bool dukeFishronThreatSource;
            bool empressThreatSource;
            ReadSupportedThreatSources(npcs, out dukeFishronThreatSource,
                out empressThreatSource);
            ReadEmpressRagePredicate(npcs, snapshot);
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null || !_npcActive(npc))
                {
                    _destroyerMotionHistory.Invalidate(i);
                    continue;
                }
                var type = _npcTypeId(npc);
                var entityKey = _whoAmI(npc);
                var destroyerPart = type >= 134 && type <= 136;
                var ai = destroyerPart ? _npcAi(npc) : null;
                if (!destroyerPart) _destroyerMotionHistory.Invalidate(i);
                var friendly = _npcFriendly(npc);
                var life = _npcLife(npc);
                var lifeMax = _npcLifeMax(npc);
                var npcDamage = _npcDamage(npc);
                var position = new Vec2(_positionX(npc), _positionY(npc));
                var width = _width(npc);
                var height = _height(npc);
                var nativeVelocity = new Vec2(_velocityX(npc), _velocityY(npc));
                var realLife = destroyerPart ? _npcRealLife(npc) : -1;
                var rootKey = destroyerPart && realLife >= 0 ? realLife : entityKey;
                // Read the native target once for both the target snapshot and
                // the source-bound contact threat below.  This value used to
                // be declared only inside the target-capture branch, leaving
                // the contact path uncompilable when a priority Boss was not
                // included as a target.
                var nativeTarget = _npcTarget(npc);
                var validEntitySlot = entityKey == i && i < DestroyerMotionHistory.Capacity;
                // Canonical root health is a stronger continuity signal than a
                // body segment's forwarded/local life value.
                var continuityLife = life;
                var continuityLifeMax = lifeMax;
                if (destroyerPart && rootKey >= 0 && rootKey < npcs.Length)
                {
                    var continuityRoot = npcs[rootKey];
                    if (continuityRoot != null && _npcActive(continuityRoot) &&
                        _npcTypeId(continuityRoot) == 134 && _whoAmI(continuityRoot) == rootKey)
                    {
                        continuityLife = _npcLife(continuityRoot);
                        continuityLifeMax = _npcLifeMax(continuityRoot);
                    }
                }
                var predecessorKey = -1;
                var predecessorValid = type == 134 || destroyerPart &&
                    DestroyerLinkIdentity.TryReadSlot(Ai(ai, 1),
                        Math.Min(npcs.Length, DestroyerMotionHistory.Capacity), out predecessorKey);
                var chainConnected = destroyerPart && validEntitySlot && !friendly && life > 0 &&
                    (type == 134 ? rootKey == entityKey : predecessorValid &&
                        DestroyerChainConnected(npcs, i, rootKey, predecessorKey));
                var motion = destroyerPart
                    ? _destroyerMotionHistory.Observe(i, entityKey, type, rootKey,
                        predecessorKey, chainConnected, position, width, height, nativeVelocity,
                        _destroyerMotionFrame, continuityLife, continuityLifeMax)
                    : new DestroyerMotionObservation
                    {
                        Velocity = nativeVelocity,
                        SweepPosition = position,
                        SweepWidth = width,
                        SweepHeight = height
                    };
                if (friendly || life <= 0) continue;
                var center = new Vec2(position.X + width * .5f, position.Y + height * .5f);
                var distanceSquared = Vec2.DistanceSquared(center, playerCenter);
                var boss = _npcBoss(npc);
                // A malformed Destroyer whoAmI is kept as a conservative contact
                // threat but must never become an indexable firing target.
                if ((IsPriorityBossNativeNpc(type) && validEntitySlot) ||
                    NativeTargetCapturePolicy.ShouldInclude(distanceSquared,
                        maxTargetDistanceSquared, boss, validEntitySlot))
                {
                    if (ai == null) ai = _npcAi(npc);
                    var localAi = _npcLocalAi(npc);
                    bool usesWormMovement;
                    var destroyerBranchKnown = DestroyerBranchObservation.TryReadHeadBranch(
                        type, motion.TrustedMotion, localAi, out usesWormMovement);
                    bool visible;
                    var known = _sightCache.TryGet(entityKey, type, playerCenter, center, _sightFrame, out visible);
                    var targetSnapshot = new TargetSnapshot
                    {
                        Key = entityKey,
                        Type = type,
                        Position = position,
                        Velocity = motion.Velocity,
                        Width = width,
                        Height = height,
                        Life = life,
                        LifeMax = lifeMax,
                        Damage = npcDamage,
                        Boss = boss,
                        Chaseable = _npcChaseable(npc),
                        Invulnerable = _npcInvulnerable(npc),
                        LineOfSightKnown = known,
                        HasLineOfSight = visible,
                        NativeTargetKnown = nativeTarget >= -1 && nativeTarget <= 255,
                        NativeTargetPlayerIndex = nativeTarget,
                        NativeRealLifeKnown = _npcRealLife(npc) >= -1 &&
                            _npcRealLife(npc) < npcs.Length,
                        NativeRealLife = _npcRealLife(npc),
                        DestroyerBranchKnown = destroyerBranchKnown,
                        DestroyerUsesWormMovement = usesWormMovement
                    };
                    PopulateNativeTargetFields(ref targetSnapshot,
                        _playerDirection(npc), _npcTimeLeft(npc), ai, localAi);
                    snapshot.Targets.Add(targetSnapshot);
                }
                var contactAi0 = float.NaN;
                var contactAi1 = float.NaN;
                var contactAi2 = float.NaN;
                var contactAi3 = float.NaN;
                var contactLocalAi0 = float.NaN;
                var contactLocalAi1 = float.NaN;
                var contactNativeTarget = _npcTarget(npc);
                var contactAi0Known = false;
                var contactAi1Known = false;
                var contactAi2Known = false;
                var contactAi3Known = false;
                var contactLocalAi0Known = false;
                var contactLocalAi1Known = false;
                if ((dukeFishronThreatSource && type >= 371 && type <= 373) ||
                    (empressThreatSource && type ==
                        PriorityBossThreatGate.EmpressType))
                {
                    if (ai == null) ai = _npcAi(npc);
                    contactAi0Known = TryReadNativeFloat(ai, 0,
                        out contactAi0);
                    contactAi1Known = TryReadNativeFloat(ai, 1,
                        out contactAi1);
                    contactAi2Known = TryReadNativeFloat(ai, 2,
                        out contactAi2);
                    contactAi3Known = TryReadNativeFloat(ai, 3,
                        out contactAi3);
                    var localAi = _npcLocalAi(npc);
                    contactLocalAi0Known = TryReadNativeFloat(localAi, 0,
                        out contactLocalAi0);
                    contactLocalAi1Known = TryReadNativeFloat(localAi, 1,
                        out contactLocalAi1);
                }
                var contactTrajectory =
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(type,
                        contactAi0, contactAi0Known, dukeFishronThreatSource,
                        empressThreatSource);
                var contactSourceBoss =
                    PriorityBossThreatGate.RequiredSourceBossType(
                        contactTrajectory);
                var threatCenter = new Vec2(motion.SweepPosition.X + motion.SweepWidth * .5f,
                    motion.SweepPosition.Y + motion.SweepHeight * .5f);
                if (npcDamage > 0 && (contactTrajectory !=
                        ThreatTrajectory.Linear || WithinThreatHorizon(
                        threatCenter, motion.Velocity, motion.SweepWidth,
                        motion.SweepHeight, playerCenter)))
                {
                    snapshot.Threats.Add(new ThreatSnapshot
                    {
                        Kind = ThreatKind.NpcContact,
                        Trajectory = contactTrajectory,
                        Position = motion.SweepPosition,
                        Velocity = motion.Velocity,
                        Width = motion.SweepWidth,
                        Height = motion.SweepHeight,
                        Damage = npcDamage,
                        TimeLeft = int.MaxValue,
                        Type = type,
                        NativeIdentity = contactTrajectory !=
                            ThreatTrajectory.Linear ? entityKey : 0,
                        SourceBossContextKnown = contactSourceBoss != 0,
                        SourceBossType = contactSourceBoss,
                        TrajectoryAi0Known = contactAi0Known,
                        TrajectoryAi1Known = contactAi1Known,
                        TrajectoryAi2Known = contactAi2Known,
                        TrajectoryAi3Known = contactAi3Known,
                        TrajectoryAi0 = contactAi0,
                        TrajectoryAi1 = contactAi1,
                        TrajectoryAi2 = contactAi2,
                        TrajectoryAi3 = contactAi3,
                        TrajectoryLocalAi0Known = contactLocalAi0Known,
                        TrajectoryLocalAi1Known = contactLocalAi1Known,
                        TrajectoryLocalAi0 = contactLocalAi0,
                        TrajectoryLocalAi1 = contactLocalAi1,
                        NativeDirectionKnown = _playerDirection(npc) == -1 ||
                            _playerDirection(npc) == 1,
                        NativeDirection = _playerDirection(npc),
                        NativeTargetPlayerKnown = contactNativeTarget >= 0 &&
                            contactNativeTarget < 255,
                        NativeTargetPlayerIndex = contactNativeTarget,
                        NativeExpertModeKnown = contactTrajectory ==
                            ThreatTrajectory.EmpressDashContact &&
                            snapshot.NativeContextKnown,
                        NativeExpertMode = snapshot.Difficulty.Expert ||
                            snapshot.Difficulty.Master,
                        NativeShouldBeEnragedKnown = contactTrajectory ==
                            ThreatTrajectory.EmpressDashContact &&
                            snapshot.PriorityBoss.EmpressRagePredicateKnown,
                        NativeShouldBeEnraged = snapshot.PriorityBoss.EmpressShouldBeEnraged
                    });
                }
            }

            // Probe at most three nearby unknown targets. Reserve one shared ray
            // for a Probe, and prioritize unknown Probes while Probe pressure is
            // active, so a dense worm chain cannot permanently starve type 139.
            // Remember blocked targets long enough to move on to another one.
            // ApplyPlan still performs an exact final ray before every shot.
            for (var query = 0; query < 3 && _sightQueryBudget > 0; query++)
            {
                var best = TargetSightQuerySelector.Select(snapshot.Targets, playerCenter, _sightQueryBudget,
                    ref _sightQueryState);
                if (best < 0) break;
                var target = snapshot.Targets[best];
                target.LineOfSightKnown = true;
                _sightQueryBudget--;
                target.HasLineOfSight = _canHitLine(player, npcs[target.Key]);
                _sightCache.Record(target.Key, target.Type, playerCenter, target.Center, _sightFrame, target.HasLineOfSight);
                snapshot.Targets[best] = target;
            }

            ReadPriorityBossNpcContext(snapshot);

            var projectiles = _projectiles();
            for (var i = 0; i < projectiles.Length; i++)
            {
                var projectile = projectiles[i];
                if (projectile == null || !_projectileActive(projectile))
                    continue;
                int projectileType = _projectileTypeId(projectile);
                if (projectileType ==
                        MoonLordProjectile454Observation.ProjectileType ||
                    projectileType ==
                        MoonLordProjectile456Observation.ProjectileType)
                    ReadPriorityMoonLordProjectile(projectiles, npcs, i,
                        projectile, projectileType, snapshot.PriorityBoss);
                var trajectory =
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        projectileType, dukeFishronThreatSource,
                        empressThreatSource);
                var projectileDamage = _projectileDamage(projectile);
                // Important Moon Lord telegraphs above were captured before
                // this filter. Fishron type 385 is also retained, but only
                // under its same-frame source lock: its own damage is zero and
                // Kill creates the damaging 384/386 tornado chain.
                if (!PriorityBossThreatGate.ShouldCaptureProjectile(
                        _projectileHostile(projectile), projectileDamage,
                        projectileType, trajectory))
                    continue;
                var position = new Vec2(_positionX(projectile), _positionY(projectile));
                var center = new Vec2(position.X + _width(projectile) * .5f, position.Y + _height(projectile) * .5f);
                var velocity = new Vec2(_velocityX(projectile), _velocityY(projectile));
                bool isBeam = projectileType == 455 || projectileType == 919 ||
                    projectileType == 923;
                var ai = isBeam || trajectory != ThreatTrajectory.Linear
                    ? _projectileAi(projectile) : null;
                int updates = isBeam ? 1 : Math.Max(1, _projectileExtraUpdates(projectile) + 1);
                var tickVelocity = velocity * updates;
                // The beam's origin may be far away while its damaging segment
                // crosses the player. Curved/native trajectories likewise need
                // Core's version-locked conservative broadphase rather than this
                // ordinary linear prefilter.
                if (!isBeam && trajectory == ThreatTrajectory.Linear &&
                    !WithinThreatHorizon(center, tickVelocity,
                        _width(projectile), _height(projectile), playerCenter))
                    continue;
                var threat = new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Trajectory = trajectory,
                    Position = position,
                    Velocity = velocity,
                    Width = _width(projectile),
                    Height = _height(projectile),
                    Damage = projectileDamage,
                    TimeLeft = _projectileTimeLeft(projectile),
                    Type = projectileType,
                    NativeIdentity = trajectory != ThreatTrajectory.Linear
                        ? _whoAmI(projectile) : 0,
                    TrajectoryAi0 = trajectory != ThreatTrajectory.Linear
                        ? Ai(ai, 0) : 0f,
                    SourceBossContextKnown =
                        PriorityBossThreatGate.RequiredSourceBossType(
                            trajectory) != 0,
                      SourceBossType =
                          PriorityBossThreatGate.RequiredSourceBossType(
                              trajectory)
                  };
                // Projectile 872's native Colliding override does not use its
                // current body position.  It reads the first 50 entries of
                // oldPos (even indices only), so a linear snapshot would leave
                // the planner blind to the rainbow trail.  Capture the fixed
                // history in the same pass; a short/null/non-finite array stays
                // unknown and the Core gate will keep the neutral hold active.
                if (projectileType == 872)
                {
                    RainbowTrailHistory50 history;
                    if (TryReadRainbowTrailHistory(projectile, out history))
                    {
                        // Promote the source-bound unknown only after the
                        // complete native collision history has been proven.
                        // This keeps malformed/short oldPos arrays on the
                        // neutral-hold path while allowing the exact 872
                        // model to participate immediately in this frame.
                        threat.Trajectory = ThreatTrajectory.EmpressRainbowTrail;
                        threat.NativeIdentity = _whoAmI(projectile);
                        threat.TrajectoryAi0 = Ai(ai, 0);
                        threat.SourceBossContextKnown = true;
                        threat.SourceBossType =
                            PriorityBossThreatGate.EmpressType;
                        threat.NativeRainbowHistoryKnown = true;
                        threat.NativeRainbowHistory = history;
                    }
                }
                PopulateProjectileNativeTarget(ref threat, ai);
                if (isBeam)
                {
                    var localAi = _projectileLocalAi(projectile);
                    threat.Geometry = projectileType == 455
                        ? ThreatGeometry.MoonLordDeathray
                        : projectileType == 919
                            ? ThreatGeometry.EmpressLance
                            : ThreatGeometry.EmpressSunDance;
                    threat.BeamOrigin = center;
                    var nativeAngle = Ai(ai, 0);
                    threat.BeamDirection = projectileType == 919
                        ? new Vec2((float)Math.Cos(nativeAngle),
                            (float)Math.Sin(nativeAngle))
                        : velocity;
                    threat.BeamAngularVelocity = Ai(ai,0);
                    threat.BeamBaseAngle = Ai(ai,0);
                    threat.BeamAngle = _projectileRotation(projectile);
                    threat.BeamAge = Ai(localAi,0);
                    threat.BeamLength = Ai(localAi,1);
                    threat.BeamScale = _projectileScale(projectile);
                    threat.BeamScaleLimit = 1f;
                    int owner = (int)Ai(ai,1);
                    if (projectileType != 919 && owner >= 0 &&
                        owner < npcs.Length && npcs[owner] != null &&
                        _npcActive(npcs[owner]))
                    {
                        if (projectileType == 923)
                        {
                            threat.BeamOrigin = new Vec2(
                                _positionX(npcs[owner]),
                                _positionY(npcs[owner])) +
                                new Vec2(_width(npcs[owner]) * .5f,
                                    _height(npcs[owner]) * .5f);
                        }
                        threat.BeamSourceVelocity = new Vec2(_velocityX(npcs[owner]),_velocityY(npcs[owner]));
                        if (projectileType == 455 && _npcTypeId(npcs[owner]) == 400) threat.BeamScaleLimit = .4f;
                    }
                }
                else
                {
                    // Projectile velocity/timeLeft are per sub-update; the planner
                    // horizon is in full game ticks. Beam velocity is a direction.
                    threat.Velocity = tickVelocity;
                    threat.TimeLeft = Math.Max(1, (int)Math.Ceiling(threat.TimeLeft / (double)updates));
                }
                if (trajectory != ThreatTrajectory.Linear)
                {
                    var nativeAi = ai;
                    var nativeLocalAi = _projectileLocalAi(projectile);
                    threat.TrajectoryAi0Known =
                        TryReadNativeFloat(nativeAi, 0,
                            out threat.TrajectoryAi0);
                    threat.TrajectoryAi1Known =
                        TryReadNativeFloat(nativeAi, 1,
                            out threat.TrajectoryAi1);
                    threat.TrajectoryAi2Known =
                        TryReadNativeFloat(nativeAi, 2,
                            out threat.TrajectoryAi2);
                    threat.TrajectoryAi3Known =
                        TryReadNativeFloat(nativeAi, 3,
                            out threat.TrajectoryAi3);
                    threat.TrajectoryLocalAi0Known =
                        TryReadNativeFloat(nativeLocalAi, 0,
                            out threat.TrajectoryLocalAi0);
                    threat.TrajectoryLocalAi1Known =
                        TryReadNativeFloat(nativeLocalAi, 1,
                            out threat.TrajectoryLocalAi1);
                    var projectileDirection = Math.Sign(velocity.X);
                    threat.NativeDirectionKnown = projectileDirection != 0;
                    threat.NativeDirection = projectileDirection;
                }
                snapshot.Threats.Add(threat);
            }

            RefreshWeaponSpecificTargetObservation(player, snapshot.Weapon);
        }

        /// <summary>
        /// Captures item-specific, same-frame target evidence after the
        /// ordinary target/threat pass.  It is deliberately separate from the
        /// pass because automatic selection can replace the initially held
        /// weapon after that pass.  The method does not mutate native game
        /// state and never infers a homing lock when its observation fails.
        /// </summary>
        private void RefreshWeaponSpecificTargetObservation(object player,
            WeaponSnapshot weapon)
        {
            // The ordinary target list deliberately omits distant/non-Boss
            // entries and uses player-origin sight caching. Razorblade Typhoon
            // needs neither approximation: its own AI_071 target scan is
            // reproduced only for this reviewed item and only when a fresh
            // cast could be admitted. A failed observation is kept unknown,
            // which makes the planner decline the cast without ending combat.
            if (weapon == null || weapon.WeaponId !=
                    CommonWeaponOutputCatalog.RazorbladeTyphoonWeaponId ||
                weapon.Profile.Status != WeaponProfileStatus.Supported)
                return;
            RazorbladeTyphoonFireObservation observation;
            if (TryReadRazorbladeTyphoonFireObservation(player,
                    out observation))
                weapon.RazorbladeTyphoon = observation;
        }

        /// <summary>
        /// Reproduces the read-only portion of projectile type 409's next
        /// AI_071 candidate scan. This is a same-frame admission observation,
        /// not a claim that a projectile which is created later in vanilla's
        /// update has already locked the returned NPC. The post-spawn AI[0]
        /// value remains the only definitive lock evidence.
        /// </summary>
        private bool TryReadRazorbladeTyphoonFireObservation(object player,
            out RazorbladeTyphoonFireObservation observation)
        {
            observation = default(RazorbladeTyphoonFireObservation);
            observation.PrefireSelectedNpcKey = -1;
            try
            {
                if (player == null) return false;
                var owner = _whoAmI(player);
                if (owner < 0 || owner >= 256) return false;

                bool noActiveOwnedProjectiles;
                if (!TryVerifyNoOwnedRazorbladeTyphoons(player, owner,
                        out noActiveOwnedProjectiles))
                    return false;

                observation.Known = true;
                observation.NoActiveOwnedProjectiles =
                    noActiveOwnedProjectiles;
                if (!noActiveOwnedProjectiles)
                    return true;

                var spawnCenter = _razorbladeSpawnCenter(player);
                if (!IsFinite(spawnCenter.X) || !IsFinite(spawnCenter.Y))
                    return false;
                observation.SpawnCenter = spawnCenter;
                observation.SpawnCenterKnown = true;

                var npcs = _npcs();
                var nativeCount = _maxNpcs();
                if (npcs == null || nativeCount <= 0 ||
                    nativeCount > npcs.Length)
                    return false;

                var selected = -1;
                var selectedDistance =
                    CommonWeaponOutputCatalog.
                        RazorbladeTyphoonNativeAcquireDistance;
                // AI_071 iterates Main.maxNPCs in array order and retains the
                // first slot when two distances are exactly equal. Preserve
                // its strict < comparison rather than sorting candidates.
                for (var slot = 0; slot < nativeCount; slot++)
                {
                    var npc = npcs[slot];
                    if (npc == null || !_npcCanBeChasedBy(npc))
                        continue;
                    var center = new Vec2(_positionX(npc) +
                        _width(npc) * .5f, _positionY(npc) +
                        _height(npc) * .5f);
                    if (!IsFinite(center.X) || !IsFinite(center.Y))
                        return false;
                    var distance = (center - spawnCenter).Length;
                    if (!IsFinite(distance)) return false;
                    if (!(distance < selectedDistance) ||
                        !_razorbladeCanHit(spawnCenter, npc))
                        continue;
                    selectedDistance = distance;
                    selected = slot;
                }

                observation.PrefireSelectedNpcKey = selected;
                if (selected < 0)
                    return true;

                var selectedNpc = npcs[selected];
                var immunity = _npcImmune(selectedNpc);
                if (immunity == null || owner >= immunity.Length)
                    return false;
                // Type 409's retained homing branch checks active,
                // dontTakeDamage and this owner's shared NPC immunity. An
                // immortal target dummy can be selected only under a debug
                // toggle; reject it rather than treating a non-damaging lock
                // as admissible Boss output.
                observation.PrefireSelectedTargetHomingReady =
                    _whoAmI(selectedNpc) == selected &&
                    _npcActive(selectedNpc) &&
                    !_npcInvulnerable(selectedNpc) &&
                    !_npcImmortal(selectedNpc) && immunity[owner] == 0;
                return true;
            }
            catch
            {
                // Any incompatible reflection member, malformed native array,
                // or collision query disables this optional route only.
                observation = default(RazorbladeTyphoonFireObservation);
                observation.PrefireSelectedNpcKey = -1;
                return false;
            }
        }

        private bool TryVerifyNoOwnedRazorbladeTyphoons(object player,
            int owner, out bool noActiveOwnedProjectiles)
        {
            noActiveOwnedProjectiles = false;
            var owned = _playerOwnedProjectileCounts(player);
            var projectileType =
                CommonWeaponOutputCatalog.RazorbladeTyphoonProjectileId;
            if (owned == null || projectileType < 0 ||
                projectileType >= owned.Length || owned[projectileType] < 0)
                return false;
            // Player.UpdateProjectileCaches already performs an exact scan of
            // active projectiles. A positive count is sufficient to decline
            // a cast cheaply. A zero is rechecked below, so a reset/update
            // ordering gap cannot falsely establish a clear shared-immunity
            // state.
            if (owned[projectileType] > 0)
                return true;

            var projectiles = _projectiles();
            if (projectiles == null) return false;
            for (var index = 0; index < projectiles.Length; index++)
            {
                var projectile = projectiles[index];
                if (projectile != null && _projectileActive(projectile) &&
                    _projectileOwner(projectile) == owner &&
                    _projectileTypeId(projectile) == projectileType)
                    return true;
            }
            noActiveOwnedProjectiles = true;
            return true;
        }

        private static bool IsPriorityBossNativeNpc(int type)
        {
            return type >= 13 && type <= 15 ||
                // Skeletron head/hand AI state must be captured even when a
                // charging hand has crossed the ordinary nearby-target radius.
                // The strategy balances the live hand clocks with the head;
                // silently dropping a distant hand produces an incomplete
                // mid-fight F8 recovery snapshot.
                type == 35 || type == 36 ||
                type == 113 || type == 114 || type == 222 ||
                type >= 245 && type <= 249 ||
                type >= 262 && type <= 267 ||
                type == 370 || type == 396 || type == 397 || type == 398 ||
                type == 400 || type == 439 || type == 440 ||
                type >= 454 && type <= 459 || type == 521 || type == 522 ||
                type == 523 || type == 636 || type == 668;
        }

        private void ReadSupportedThreatSources(object[] npcs,
            out bool dukeFishron, out bool empress)
        {
            dukeFishron = false;
            empress = false;
            if (npcs == null) return;
            for (var slot = 0; slot < npcs.Length; slot++)
            {
                var npc = npcs[slot];
                if (npc == null || !_npcActive(npc)) continue;
                var type = _npcTypeId(npc);
                if (type != PriorityBossThreatGate.DukeFishronType &&
                    type != PriorityBossThreatGate.EmpressType)
                    continue;
                // Only a live hostile Boss in its canonical native array slot
                // can authorize a family tag. Minions, stale objects, and a
                // matching type value in a malformed slot remain untrusted.
                if (_whoAmI(npc) != slot || !_npcBoss(npc) ||
                    _npcFriendly(npc) || _npcLife(npc) <= 0)
                    continue;
                if (type == PriorityBossThreatGate.DukeFishronType)
                    dukeFishron = true;
                else empress = true;
                if (dukeFishron && empress) return;
            }
        }

        private static void PopulatePriorityWorldContext(
            PriorityBossNativeContext context, double worldSurfaceTiles,
            int worldWidthTiles, int wallDrawTopPixels,
            int wallDrawBottomPixels, bool empressRageMode)
        {
            context.WorldSurfaceTiles = worldSurfaceTiles;
            context.WorldWidthTiles = worldWidthTiles;
            context.WorldGeometryKnown = IsFinite(worldSurfaceTiles) &&
                worldSurfaceTiles > 0d && worldWidthTiles > 0;
            context.WallOfFleshDrawAreaTopPixels = wallDrawTopPixels;
            context.WallOfFleshDrawAreaBottomPixels = wallDrawBottomPixels;
            // Both globals are -1 before vanilla has initialized the active
            // Wall's scan. Zero remains a legitimate observed pixel boundary.
            context.WallOfFleshDrawAreaKnown = wallDrawTopPixels >= 0 &&
                wallDrawBottomPixels >= 0;
            context.EmpressRageMode = empressRageMode;
            context.EmpressRageModeKnown = true;
        }

        private static void PopulateNativeTargetFields(ref TargetSnapshot target,
            int nativeDirection, int nativeTimeLeft, float[] ai, float[] localAi)
        {
            target.NativeDirection = nativeDirection;
            target.NativeDirectionKnown = nativeDirection >= -1 &&
                nativeDirection <= 1;
            target.NativeTimeLeft = nativeTimeLeft;
            target.NativeTimeLeftKnown = nativeTimeLeft >= 0;
            target.Ai0Known = TryReadNativeFloat(ai, 0, out target.Ai0);
            target.Ai1Known = TryReadNativeFloat(ai, 1, out target.Ai1);
            target.Ai2Known = TryReadNativeFloat(ai, 2, out target.Ai2);
            target.Ai3Known = TryReadNativeFloat(ai, 3, out target.Ai3);
            target.LocalAi0Known = TryReadNativeFloat(localAi, 0,
                out target.LocalAi0);
            target.LocalAi1Known = TryReadNativeFloat(localAi, 1,
                out target.LocalAi1);
            target.LocalAi2Known = TryReadNativeFloat(localAi, 2,
                out target.LocalAi2);
            target.LocalAi3Known = TryReadNativeFloat(localAi, 3,
                out target.LocalAi3);
            // Compatibility for the two existing teleport consumers.  Zero is
            // native data, not an unavailable sentinel.
            target.LocalAiKnown = target.LocalAi1Known &&
                target.LocalAi2Known;
        }

        private void ReadEmpressRagePredicate(object[] npcs,
            CombatSnapshot snapshot)
        {
            var context = snapshot.PriorityBoss;
            context.EmpressRagePredicateKnown = false;
            context.EmpressPredicateNpcSlotKnown = false;
            context.EmpressPredicateNpcSlot = -1;
            context.EmpressFirstBossAboveWorldSurface = false;
            context.EmpressShouldBeEnraged = false;
            if (!snapshot.NativeContextKnown ||
                !context.EmpressRageModeKnown)
                return;
            if (!snapshot.Difficulty.Remix)
            {
                context.EmpressRagePredicateKnown = true;
                context.EmpressShouldBeEnraged =
                    snapshot.Difficulty.DayTime;
                return;
            }
            if (context.EmpressRageMode)
            {
                context.EmpressRagePredicateKnown = true;
                context.EmpressShouldBeEnraged = true;
                return;
            }
            if (!context.WorldGeometryKnown || npcs == null) return;

            // Exact read-only equivalent of ShouldEmpressBeEnraged's Remix
            // scan: vanilla uses the first type-636 array entry and does not
            // test active. Do not call the original method because it writes
            // NPC.empressRageMode.
            for (var slot = 0; slot < npcs.Length; slot++)
            {
                var npc = npcs[slot];
                if (npc == null || _npcTypeId(npc) != 636) continue;
                var centerY = _positionY(npc) + _height(npc) * .5f;
                if (!IsFinite(centerY)) return;
                context.EmpressPredicateNpcSlotKnown = true;
                context.EmpressPredicateNpcSlot = slot;
                context.EmpressFirstBossAboveWorldSurface =
                    centerY < context.WorldSurfaceTiles * 16d;
                context.EmpressShouldBeEnraged =
                    context.EmpressFirstBossAboveWorldSurface;
                context.EmpressRagePredicateKnown = true;
                return;
            }
            // No type-636 entry is a known false result in the native method.
            context.EmpressRagePredicateKnown = true;
        }

        private static bool TryReadNativeFloat(float[] values, int index,
            out float value)
        {
            value = 0f;
            if (values == null || index < 0 || index >= values.Length)
                return false;
            value = values[index];
            return IsFinite(value);
        }

        /// <summary>
        /// Reads the portion of Projectile.oldPos used by type 872's native
        /// collision routine.  This helper intentionally performs no array
        /// allocation and accepts zero entries (vanilla's uninitialised trail
        /// sentinel); only missing or non-finite coordinates make the history
        /// unavailable.  Reflection delegates are optional in synthetic
        /// fixtures, so a missing delegate/field fails closed instead of
        /// throwing from the per-frame capture loop.
        /// </summary>
        private bool TryReadRainbowTrailHistory(object projectile,
            out RainbowTrailHistory50 history)
        {
            history = default(RainbowTrailHistory50);
            if (projectile == null || _projectileOldPosLength == null ||
                _projectileOldPosX == null || _projectileOldPosY == null)
                return false;
            try
            {
                var length = _projectileOldPosLength(projectile);
                if (length < RainbowTrailHistory50.Length) return false;
                for (var index = 0; index < RainbowTrailHistory50.Length;
                    index++)
                {
                    var x = _projectileOldPosX(projectile, index);
                    var y = _projectileOldPosY(projectile, index);
                    if (!IsFinite(x) || !IsFinite(y))
                    {
                        history = default(RainbowTrailHistory50);
                        return false;
                    }
                    history.Set(index, new Vec2(x, y));
                }
                return true;
            }
            catch (Exception)
            {
                // A modded/older Terraria build may expose a different field
                // shape.  The strict native trajectory must then remain
                // unknown; swallowing the adapter exception preserves the
                // plugin's fail-closed contract and keeps the game thread
                // alive.
                history = default(RainbowTrailHistory50);
                return false;
            }
        }

        private static void PopulateProjectileNativeTarget(
            ref ThreatSnapshot threat, float[] ai)
        {
            threat.NativeTargetPlayerKnown = false;
            threat.NativeTargetPlayerIndex = -1;
            if (threat.Trajectory ==
                    ThreatTrajectory.UnmodeledDukeFishronHazard)
            {
                // Fishron bubble AI_065 stores target+1 in ai[1].  Zero is
                // the untargeted, outward-drifting phase; positive values are
                // one-based player slots exactly as in vanilla.
                float encodedBubbleTarget;
                if (TryReadNativeFloat(ai, 1, out encodedBubbleTarget) &&
                    encodedBubbleTarget >= 1f &&
                    encodedBubbleTarget < 256f &&
                    encodedBubbleTarget == (int)encodedBubbleTarget)
                {
                    threat.NativeTargetPlayerKnown = true;
                    threat.NativeTargetPlayerIndex =
                        (int)encodedBubbleTarget - 1;
                }
                return;
            }
            if (threat.Trajectory !=
                    ThreatTrajectory.EmpressRainbowStreak)
                return;

            float encodedTarget;
            if (!TryReadNativeFloat(ai, 0, out encodedTarget) ||
                encodedTarget < 0f || encodedTarget >= 255f ||
                encodedTarget != (int)encodedTarget)
                return;
            threat.NativeTargetPlayerKnown = true;
            threat.NativeTargetPlayerIndex = (int)encodedTarget;
        }

        private static void ReadPriorityBossNpcContext(
            CombatSnapshot snapshot)
        {
            var context = snapshot.PriorityBoss;
            var player = snapshot.Player;
            for (var index = 0; index < snapshot.Targets.Count; index++)
            {
                var target = snapshot.Targets[index];
                switch (target.Type)
                {
                case 222:
                    var queen = new QueenBeeNativeEnrageObservation
                    {
                        Known = snapshot.NativeContextKnown &&
                            context.WorldGeometryKnown &&
                            player.ZoneJungleKnown && target.Key >= 0 &&
                            target.NativeTargetKnown &&
                            target.NativeTargetPlayerIndex ==
                                snapshot.LocalPlayerIndex,
                        NpcKey = target.Key,
                        BossAboveWorldSurface = context.WorldGeometryKnown &&
                            IsFinite(target.Position.Y) &&
                            target.Position.Y / 16d < context.WorldSurfaceTiles,
                        TargetOutsideJungle = player.ZoneJungleKnown &&
                            !player.ZoneJungle,
                        GetGoodWorld = snapshot.Difficulty.ForTheWorthy
                    };
                    queen.NativeEnrageFactor = queen.ExpectedEnrageFactor;
                    CloseInvalid(ref queen);
                    context.QueenBees.Add(queen);
                    break;

                case 113:
                    var tunnel = new WallOfFleshTunnelObservation
                    {
                        Known = snapshot.NativeContextKnown && target.Key >= 0 &&
                            context.WallOfFleshDrawAreaKnown,
                        NpcKey = target.Key,
                        NativeDirectionKnown = target.NativeDirectionKnown,
                        NativeDirection = target.NativeDirection,
                        DrawAreaTopPixels =
                            context.WallOfFleshDrawAreaTopPixels,
                        DrawAreaBottomPixels =
                            context.WallOfFleshDrawAreaBottomPixels
                    };
                    CloseInvalid(ref tunnel);
                    context.WallOfFleshTunnels.Add(tunnel);
                    break;

                case 114:
                    var eye = new WallOfFleshEyeLaserObservation
                    {
                        Known = snapshot.NativeContextKnown && target.Key >= 0 &&
                            target.Ai0Known && target.LocalAi1Known &&
                            target.LocalAi2Known,
                        NpcKey = target.Key,
                        Ai0EyeSide = target.Ai0,
                        LocalAi1ChargeTimer = target.LocalAi1,
                        LocalAi2BurstStage = target.LocalAi2,
                        LineOfSightKnown = target.LineOfSightKnown,
                        LineOfSight = target.HasLineOfSight
                    };
                    CloseInvalid(ref eye);
                    context.WallOfFleshEyes.Add(eye);
                    break;

                case 370:
                    var fishron = new DukeFishronNativeEnrageObservation
                    {
                        Known = snapshot.NativeContextKnown &&
                            context.WorldGeometryKnown && target.Key >= 0 &&
                            target.NativeTargetKnown &&
                            target.NativeTargetPlayerIndex ==
                                snapshot.LocalPlayerIndex &&
                            IsFinite(player.Position.X) &&
                            IsFinite(player.Position.Y),
                        NpcKey = target.Key,
                        PlayerAboveY800Band = IsFinite(player.Position.Y) &&
                            player.Position.Y < 800f,
                        PlayerBelowWorldSurface =
                            context.WorldGeometryKnown &&
                            player.Position.Y >
                                context.WorldSurfaceTiles * 16d,
                        PlayerInsideCentralHorizontalBand =
                            context.WorldGeometryKnown &&
                            player.Position.X > 6400f &&
                            player.Position.X <
                                context.WorldWidthTiles * 16d - 6400d
                    };
                    fishron.NativeEnraged = fishron.ExpectedEnraged;
                    CloseInvalid(ref fishron);
                    context.DukeFishrons.Add(fishron);
                    break;

                case 636:
                    var empress = new EmpressNativeCombatObservation
                    {
                        Known = snapshot.NativeContextKnown &&
                            context.EmpressRagePredicateKnown && target.Key >= 0 &&
                            target.Ai0Known && target.Ai1Known && target.Ai2Known &&
                            target.Ai3Known && IsFinite(target.Center.Y),
                        NpcKey = target.Key,
                        DayTime = snapshot.Difficulty.DayTime,
                        RemixWorld = snapshot.Difficulty.Remix,
                        RemixRageMode = context.EmpressRageMode,
                        BossAboveWorldSurface =
                            context.EmpressFirstBossAboveWorldSurface,
                        Ai0AttackState = target.Ai0,
                        Ai1AttackTimer = target.Ai1,
                        Ai2AttackIndex = target.Ai2,
                        Ai3PhaseAndRage = target.Ai3
                    };
                    empress.NativeShouldBeEnraged =
                        context.EmpressShouldBeEnraged;
                    CloseInvalid(ref empress);
                    context.Empresses.Add(empress);
                    break;

                case 668:
                    var deer = new DeerclopsNativeTimerObservation
                    {
                        Known = snapshot.NativeContextKnown && target.Key >= 0 &&
                            target.Ai0Known && target.Ai1Known &&
                            target.Ai2Known && target.Ai3Known &&
                            target.LocalAi1Known && target.LocalAi2Known &&
                            target.LocalAi3Known && target.NativeTimeLeftKnown,
                        NpcKey = target.Key,
                        Ai0State = target.Ai0,
                        Ai1StateTimer = target.Ai1,
                        LocalAi1MeleeCounter = target.LocalAi1,
                        LocalAi2ShadowHandTimer = target.LocalAi2,
                        LocalAi3DistanceInvulnerabilityTimer = target.LocalAi3,
                        TimeLeft = target.NativeTimeLeft,
                        HomeTileX = target.Ai2,
                        HomeTileY = target.Ai3,
                        NativeDirectionKnown = target.NativeDirectionKnown,
                        NativeDirection = target.NativeDirection
                    };
                    CloseInvalid(ref deer);
                    context.Deerclopses.Add(deer);
                    break;
                }
            }
        }

        private static void CloseInvalid(
            ref QueenBeeNativeEnrageObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(ref WallOfFleshTunnelObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(
            ref WallOfFleshEyeLaserObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(
            ref DukeFishronNativeEnrageObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(ref EmpressNativeCombatObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(ref DeerclopsNativeTimerObservation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private void ReadPriorityMoonLordProjectile(object[] projectiles,
            object[] npcs, int slot, object projectile, int projectileType,
            PriorityBossNativeContext context)
        {
            var ai = _projectileAi(projectile);
            var localAi = _projectileLocalAi(projectile);
            var entityKey = _whoAmI(projectile);
            float ai0;
            float ai1;
            float localAi0;
            float localAi1;
            var identityKnown = slot >= 0 && slot < projectiles.Length &&
                ReferenceEquals(projectiles[slot], projectile) &&
                entityKey == slot;
            var ai0Known = TryReadNativeFloat(ai, 0, out ai0);
            var ai1Known = TryReadNativeFloat(ai, 1, out ai1);
            var localAi0Known = TryReadNativeFloat(localAi, 0,
                out localAi0);
            var localAi1Known = TryReadNativeFloat(localAi, 1,
                out localAi1);

            if (projectileType ==
                MoonLordProjectile454Observation.ProjectileType)
            {
                var observation = new MoonLordProjectile454Observation
                {
                    Known = identityKnown && ai0Known && ai1Known &&
                        localAi0Known && localAi1Known,
                    ProjectileKey = entityKey,
                    Ai0AgeOrMode = ai0,
                    SourceNpcAi1 = ai1,
                    LocalAi0 = localAi0,
                    LocalAi1 = localAi1,
                    TimeLeft = _projectileTimeLeft(projectile),
                    Alpha = _projectileAlpha(projectile),
                    ExtraUpdates = _projectileExtraUpdates(projectile)
                };
                PopulateSourceNpcIdentity(npcs, observation.SourceNpcKey,
                    out observation.SourceNpcIdentityKnown,
                    out observation.SourceNpcActive,
                    out observation.SourceNpcType);
                CloseInvalid(ref observation);
                if (observation.Known && observation.Ai0AgeOrMode >= 0f &&
                    observation.Ai0AgeOrMode < 30f &&
                    (!observation.SourceNpcIdentityKnown ||
                     !observation.SourceNpcActive ||
                     !IsMoonLordProjectile454Source(
                         observation.SourceNpcType)))
                    observation.Known = false;
                context.MoonLordProjectiles454.Add(observation);
                return;
            }

            var leech = new MoonLordProjectile456Observation
            {
                Known = identityKnown && ai0Known && ai1Known &&
                    localAi0Known && localAi1Known,
                ProjectileKey = entityKey,
                EncodedSourceAi0 = ai0,
                TargetPlayerAi1 = ai1,
                AgeTicks = localAi0,
                ContactLatchAi = localAi1,
                TimeLeft = _projectileTimeLeft(projectile)
            };
            PopulateSourceNpcIdentity(npcs, leech.SourceNpcKey,
                out leech.SourceNpcIdentityKnown,
                out leech.SourceNpcActive, out leech.SourceNpcType);
            CloseInvalid(ref leech);
            if (leech.Known && !leech.HasLiveMoonLordSource)
                leech.Known = false;
            context.MoonLordProjectiles456.Add(leech);
        }

        private static bool IsMoonLordProjectile454Source(int npcType)
        {
            return npcType == 396 || npcType == 397 || npcType == 400;
        }

        private void PopulateSourceNpcIdentity(object[] npcs, int sourceKey,
            out bool known, out bool active, out int type)
        {
            known = false;
            active = false;
            type = 0;
            if (sourceKey == -1)
            {
                known = true;
                return;
            }
            if (sourceKey < 0 || sourceKey >= npcs.Length) return;
            var source = npcs[sourceKey];
            if (source == null || _whoAmI(source) != sourceKey) return;
            known = true;
            active = _npcActive(source);
            type = _npcTypeId(source);
        }

        private static void CloseInvalid(
            ref MoonLordProjectile454Observation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private static void CloseInvalid(
            ref MoonLordProjectile456Observation value)
        {
            string ignored;
            if (!PriorityBossNativeContextContract.TryValidate(in value,
                out ignored)) value.Known = false;
        }

        private ArenaSnapshot ReadArena(PlayerSnapshot player, bool inverted)
        {
            _recoverySupportAge++;
            var footY = inverted ? player.Position.Y : player.Position.Y + player.Height;
            if (_cachedArena != null && _cachedArenaInverted == inverted && _cachedArenaOnGround == player.OnGround && _arenaCacheTicks++ < 20 &&
                (!player.OnGround || _cachedArenaOneWay == player.OnOneWaySupport && Math.Abs(footY - _cachedArenaFootY) < 2f) &&
                Vec2.DistanceSquared(player.Center, _cachedArenaAt) < 64f * 64f)
                return _cachedArena;

            _arenaCacheTicks = 0;
            _cachedArenaInverted = inverted;
            _cachedArenaOnGround = player.OnGround;
            _cachedArenaOneWay = player.OnOneWaySupport;
            _cachedArenaFootY = footY;
            _cachedArenaAt = player.Center;
            var tiles = _tiles();
            var centerX = Clamp((int)(player.Center.X / 16f), 2, _maxTilesX() - 3);
            var centerY = Clamp((int)(player.Center.Y / 16f), 2, _maxTilesY() - 3);
            const int maxHorizontal = 150;
            const int maxVertical = 70;
            var left = ScanHorizontal(tiles, centerX, centerY, -1, maxHorizontal);
            var right = ScanHorizontal(tiles, centerX, centerY, 1, maxHorizontal);
            var up = ScanVertical(tiles, centerX, centerY, -1, maxVertical);
            var down = ScanVertical(tiles, centerX, centerY, 1, maxVertical);
            var arena = new ArenaSnapshot
            {
                ClearanceLeft = left * 16f,
                ClearanceRight = right * 16f,
                ClearanceUp = up * 16f,
                ClearanceDown = down * 16f,
                HasFloor = down < maxVertical,
                HasCeiling = up < maxVertical,
                SafeCenter = new Vec2((centerX + (right - left) * .5f) * 16f, player.Center.Y)
            };
            arena.FloorSupport = ReadSupportSpan(tiles, player, false);
            arena.CeilingSupport = ReadSupportSpan(tiles, player, true);
            arena.RecoverySupport = RefreshRecoverySupport(tiles, player,
                inverted ? arena.CeilingSupport : arena.FloorSupport, inverted);
            arena.LocalOpenBounds = new RectF(player.Center.X - arena.ClearanceLeft + 8f,
                player.Center.Y - arena.ClearanceUp + 8f,
                Math.Max(16f, arena.HorizontalClearance - 16f),
                Math.Max(16f, arena.VerticalClearance - 16f));
            FindGrappleAnchors(tiles, centerX, centerY, arena);
            _cachedArena = arena;
            return arena;
        }

        private SupportSpan ReadSupportSpan(Array tiles, PlayerSnapshot player, bool inverted)
        {
            // Verify ONE continuous flat row below/above the current footprint.
            // Limits are independent of world size, and no empty space is bridged.
            if (player.Width <= 0 || player.Width > 128) return default(SupportSpan);
            var foot = inverted ? player.Position.Y : player.Position.Y + player.Height;
            var firstY = (int)((foot + (inverted ? -1f : 0f)) / 16f);
            var leftX = Clamp((int)(player.Position.X / 16f), 1, _maxTilesX() - 2);
            var rightX = Clamp((int)((player.Position.X + player.Width - 1f) / 16f), leftX,
                Math.Min(_maxTilesX() - 2, leftX + 8));
            for (var distance = 0; distance <= 70; distance++)
            {
                var y = firstY + (inverted ? -distance : distance);
                if (y < 1 || y >= _maxTilesY() - 1) break;
                for (var x = leftX; x <= rightX; x++)
                {
                    bool oneWay;
                    if (IsSupportCell(tiles, x, y, inverted, player.Height, out oneWay))
                        return ReadSupportSpanAt(tiles, x, y, inverted, player.Height);
                    // Shaped or obstructed solids occlude surfaces beyond them.
                    if (IsSolid(tiles, x, y, !inverted)) return default(SupportSpan);
                }
            }
            return default(SupportSpan);
        }

        private SupportSpan ReadSupportSpanAt(Array tiles, int x, int y, bool inverted, int bodyHeight)
        {
            bool oneWay;
            if (!IsSupportCell(tiles, x, y, inverted, bodyHeight, out oneWay)) return default(SupportSpan);
            var left = x;
            var right = x;
            for (var distance = 1; distance <= 96; distance++)
            {
                bool nextOneWay;
                if (!IsSupportCell(tiles, x - distance, y, inverted, bodyHeight, out nextOneWay) || nextOneWay != oneWay) break;
                left = x - distance;
            }
            for (var distance = 1; distance <= 96; distance++)
            {
                bool nextOneWay;
                if (!IsSupportCell(tiles, x + distance, y, inverted, bodyHeight, out nextOneWay) || nextOneWay != oneWay) break;
                right = x + distance;
            }
            return new SupportSpan
            {
                Valid = true, Inverted = inverted, OneWay = oneWay,
                Left = left * 16f, Right = (right + 1) * 16f,
                SurfaceY = (inverted ? y + 1 : y) * 16f
            };
        }

        private bool IsSupportCell(Array tiles, int x, int y, bool inverted, int bodyHeight, out bool oneWay)
        {
            oneWay = false;
            if (bodyHeight <= 0 || bodyHeight > 128 || x < 1 || x >= _maxTilesX() - 1 || y < 1 || y >= _maxTilesY() - 1) return false;
            var tile = TileAt(tiles, x, y);
            if (tile == null || !_tileActive(tile) || _tileInactive(tile) || _tileSlope(tile) != 0 || _tileHalfBrick(tile)) return false;
            var type = _tileType(tile);
            oneWay = type < _tileSolidTop.Length && _tileSolidTop[type];
            if (oneWay ? inverted : type >= _tileSolid.Length || !_tileSolid[type]) return false;
            for (var offset = 1; offset <= Math.Min(8, (bodyHeight + 15) / 16); offset++)
            {
                var clearY = y + (inverted ? offset : -offset);
                if (clearY < 1 || clearY >= _maxTilesY() - 1 || IsFullSolid(tiles, x, clearY)) return false;
            }
            return true;
        }

        private SupportSpan RefreshRecoverySupport(Array tiles, PlayerSnapshot player, SupportSpan current, bool inverted)
        {
            var sign = inverted ? -1f : 1f;
            var foot = inverted ? player.Position.Y : player.Position.Y + player.Height;
            var previous = _recoverySupport;
            // Retain a recently observed higher row after crossing its edge, but
            // never assume ascent to a row already passed during an exhausted fall.
            if (_recoverySupportAge > 240 || previous.Inverted != inverted ||
                (foot - previous.SurfaceY) * sign > 8f ||
                player.Center.X < previous.Left - 960f || player.Center.X > previous.Right + 960f)
                previous = default(SupportSpan);
            if (current.Valid && (!previous.Valid || (current.SurfaceY - previous.SurfaceY) * sign <= 8f))
                previous = current;
            else if (previous.Valid)
            {
                var x = Clamp((int)(player.Center.X / 16f), (int)(previous.Left / 16f), (int)(previous.Right / 16f) - 1);
                var y = (int)(previous.SurfaceY / 16f) - (inverted ? 1 : 0);
                previous = ReadSupportSpanAt(tiles, x, y, inverted, player.Height);
            }
            if (!previous.Valid) previous = current;
            if (previous.Valid) _recoverySupportAge = 0;
            _recoverySupport = previous;
            return previous;
        }

        private int ScanHorizontal(Array tiles, int x, int y, int direction, int maximum)
        {
            for (var distance = 1; distance <= maximum; distance++)
            {
                var tx = x + distance * direction;
                if (tx < 1 || tx >= _maxTilesX() - 1)
                    return distance;
                if (IsFullSolid(tiles, tx, y - 1) || IsFullSolid(tiles, tx, y) || IsFullSolid(tiles, tx, y + 1))
                    return distance;
            }
            return maximum;
        }

        private int ScanVertical(Array tiles, int x, int y, int direction, int maximum)
        {
            for (var distance = 1; distance <= maximum; distance++)
            {
                var ty = y + distance * direction;
                if (ty < 1 || ty >= _maxTilesY() - 1)
                    return distance;
                if (IsSolid(tiles, x - 1, ty, direction > 0) || IsSolid(tiles, x, ty, direction > 0) ||
                    IsSolid(tiles, x + 1, ty, direction > 0))
                    return distance;
            }
            return maximum;
        }

        private void FindGrappleAnchors(Array tiles, int centerX, int centerY, ArenaSnapshot arena)
        {
            for (var radius = 5; radius <= 35 && arena.GrappleAnchors.Count < 12; radius += 5)
            {
                for (var dx = -radius; dx <= radius && arena.GrappleAnchors.Count < 12; dx += 5)
                {
                    foreach (var dy in new[] { -radius, radius / 2 })
                    {
                        var x = centerX + dx;
                        var y = centerY + dy;
                        if (x > 0 && y > 0 && x < _maxTilesX() && y < _maxTilesY() && IsFullSolid(tiles, x, y))
                            arena.GrappleAnchors.Add(new Vec2(x * 16f + 8f, y * 16f + 8f));
                        if (arena.GrappleAnchors.Count >= 12)
                            break;
                    }
                }
            }
        }

        private void FindStartInteractionTargets(object player, BossStartContext context)
        {
            var centerX = Clamp((int)((_positionX(player) + _width(player) * .5f) / 16f), 2, _maxTilesX() - 3);
            var centerY = Clamp((int)((_positionY(player) + _height(player) * .5f) / 16f), 2, _maxTilesY() - 3);
            var tiles = _tiles();
            var bestAltar = float.MaxValue;
            var bestLava = float.MaxValue;
            var bestWater = float.MaxValue;
            var waterTiles = 0;
            for (var dx = -48; dx <= 48; dx++)
            {
                for (var dy = -30; dy <= 30; dy++)
                {
                    var x = centerX + dx;
                    var y = centerY + dy;
                    if (x < 1 || y < 1 || x >= _maxTilesX() - 1 || y >= _maxTilesY() - 1)
                        continue;
                    var tile = TileAt(tiles, x, y);
                    if (tile == null)
                        continue;
                    var distance = dx * dx + dy * dy;
                    if (_tileActive(tile) && _tileType(tile) == 237 && Math.Abs(dx) <= 12 && Math.Abs(dy) <= 12 && distance < bestAltar)
                    {
                        bestAltar = distance;
                        context.NearLihzahrdAltar = true;
                        context.AltarWorld = new Vec2(x * 16f + 8f, y * 16f + 8f);
                    }
                    var liquid = (byte)_liquidField.GetValue(tile);
                    if (liquid == 0)
                        continue;
                    if (_tileLava(tile) && Math.Abs(dx) <= 10 && Math.Abs(dy) <= 14 && distance < bestLava)
                    {
                        bestLava = distance;
                        context.NearbyLava = true;
                        context.LavaWorld = new Vec2(x * 16f + 8f, y * 16f + 8f);
                    }
                    if (_tileWater(tile))
                    {
                        waterTiles++;
                        if (distance < bestWater)
                        {
                            bestWater = distance;
                            context.OceanWaterWorld = new Vec2(x * 16f + 8f, y * 16f + 4f);
                        }
                    }
                }
            }
            context.OceanWater = waterTiles >= 20;
        }

        public BossStartTick ExecuteBossStart(object player, BossStartPlan plan, int tick, bool alreadyIssued)
        {
            var result = new BossStartTick { StillValid = plan != null && tick <= plan.TimeoutTicks };
            if (plan == null || !result.StillValid)
            {
                result.FailureReason = "召唤等待超时";
                return result;
            }

            RestoreVoodooRemainder(player, plan, tick);
            if (plan.IsNatural)
            {
                result.Issued = true;
                if (tick > 2)
                {
                    if (plan.Kind == BossSummonKind.NaturalEye)
                        result.StillValid = _spawnEye() && !_dayTime();
                    else if (plan.Kind == BossSummonKind.NaturalMechanicalBoss)
                        result.StillValid = _spawnHardBoss() > 0 && !_dayTime();
                    else if (plan.Kind == BossSummonKind.NaturalMoonLord)
                        result.StillValid = _moonLordCountdown() > 0;
                    if (!result.StillValid)
                        result.FailureReason = "原版自然 Boss 排程已取消";
                }
                return result;
            }

            switch (plan.Kind)
            {
                case BossSummonKind.DirectItem:
                    return ExecuteSummonPulse(player, plan, tick, alreadyIssued, false,
                        new Vec2(_positionX(player) + _width(player) * .5f + 160f,
                            _positionY(player) + _height(player) * .5f));

                case BossSummonKind.LihzahrdAltar:
                    return ExecuteSummonPulse(player, plan, tick, alreadyIssued, true, plan.InteractionWorld);

                case BossSummonKind.TruffleWormFishing:
                    return ExecuteFishronStart(player, plan, tick, alreadyIssued);
                case BossSummonKind.PrismaticLacewing:
                    return ExecuteLacewingStart(player, plan, tick, alreadyIssued);
                case BossSummonKind.GuideVoodooDoll:
                    return ExecuteWallStart(player, plan, tick, alreadyIssued);
                default:
                    return Invalid("未知召唤流程");
            }
        }

        private BossStartTick ExecuteFishronStart(object player, BossStartPlan plan, int tick, bool alreadyIssued)
        {
            if (alreadyIssued)
            {
                RestoreFishingBaitOrder(player, plan);
                return new BossStartTick { Issued = true, StillValid = tick <= plan.TimeoutTicks };
            }
            var wormSlot = _fishingSwapSlot >= 0 ? _fishingSwapSlot :
                plan.SummonSlot;
            if (!SlotContains(player, wormSlot, 2673))
                return Invalid("松露虫已不在计划的物品栏位置");
            if (!SlotContainsFishingRod(player, plan.ActionSlot))
                return Invalid("钓竿已不在原快捷栏位置");
            EnsureTruffleWormIsFirstBait(player, plan);

            SetSelectedItem(player, plan.ActionSlot);
            ClearCombatControls(player);
            AimAt(player, plan.InteractionWorld);
            var bobber = HasOwnedBobber();
            var ready = GetSelectedItem(player) == plan.ActionSlot && SummonAnimationReady(player) && _releaseUseItem(player);
            var pulse = ready && (bobber ? tick % 12 == 0 : tick % 60 >= 1 && tick % 60 <= 5);
            SetControl(player, "controlUseItem", pulse);
            return new BossStartTick
            {
                Issued = bobber && pulse,
                StillValid = tick <= plan.TimeoutTicks,
                ControlsApplied = true
            };
        }

        private BossStartTick ExecuteSummonPulse(object player, BossStartPlan plan, int tick,
            bool alreadyIssued, bool tileUse, Vec2 aim)
        {
            if (alreadyIssued || HasBossType(plan.ExpectedBossType))
                return new BossStartTick { Issued = true, StillValid = tick <= plan.TimeoutTicks };
            var items = _inventory(player);
            var present = SlotContains(player, plan.SummonSlot, plan.ItemType);
            if (_summonInitialStack >= 0 && (!present || _itemStack(items[plan.SummonSlot]) < _summonInitialStack))
                return new BossStartTick { Issued = true, StillValid = tick <= plan.TimeoutTicks };
            if (!present) return Invalid("召唤物已不在原快捷栏位置");
            if (_summonInitialStack < 0) _summonInitialStack = _itemStack(items[plan.SummonSlot]);
            SetSelectedItem(player, plan.SummonSlot);
            ClearCombatControls(player);
            AimAt(player, aim);
            // selectedItemState.Select may buffer the switch during an old weapon's
            // animation. Wait for both actual selection and usable animation state.
            var pulse = SummonActionGate.ShouldPulse(SummonAnimationReady(player), GetSelectedItem(player) == plan.SummonSlot,
                tileUse || _releaseUseItem(player), tick, _summonPulseTick);
            SetControl(player, tileUse ? "controlUseTile" : "controlUseItem", pulse);
            if (pulse) _summonPulseTick = tick;
            // Input being requested is not evidence of use. Only consumption or an
            // actual Boss confirms it; unsuccessful pulses can retry until the timeout.
            return new BossStartTick { StillValid = tick <= plan.TimeoutTicks, ControlsApplied = true };
        }

        private bool SummonAnimationReady(object player)
        {
            return _playerItemAnimation(player) == 0 && _playerItemTime(player) == 0 &&
                !_drawingPlayerChat() && !_gamePaused() && !_playerDead(player);
        }

        private bool IsNonEmpty(object item) => item != null && _itemTypeId(item) > 0 && _itemStack(item) > 0;

        private bool HasSafeLavaThrow(object player, Vec2 nearbyLava, out int direction)
        {
            var position = new Vec2(_positionX(player) + _width(player) * .5f, _positionY(player) + _height(player) * .5f);
            direction = nearbyLava.X < position.X ? -1 : 1;
            if (_playerGravDir(player) < 0f || Math.Abs(_velocityX(player)) > .35f || Math.Abs(_velocityY(player)) > .35f ||
                Math.Abs(nearbyLava.X - position.X) > 96f || nearbyLava.Y < position.Y - 16f || nearbyLava.Y > position.Y + 128f)
                return false;
            // Read-only validation of ONE vanilla throw arc, not pathfinding. Verified
            // 1.4.5.8 launch=(4*direction+vx,-2), WorldItem gravity=.1, max fall=7.
            // Reject uncertain tight pools, intervening solids/liquids and moving starts.
            var velocity = new Vec2(4f * direction + _velocityX(player), -2f);
            var tiles = _tiles();
            for (var tick = 0; tick < 96; tick++)
            {
                velocity.Y = Math.Min(7f, velocity.Y + .1f);
                position += velocity;
                var x = (int)(position.X / 16f);
                var y = (int)(position.Y / 16f);
                if (x < 2 || y < 2 || x >= _maxTilesX() - 2 || y >= _maxTilesY() - 2) return false;
                var tile = _tileAt(tiles, x, y);
                if (tile != null && (byte)_liquidField.GetValue(tile) > 0)
                {
                    if (!_tileLava(tile)) return false;
                    // A broad, substantially filled lava landing provides tolerance
                    // for native item dimensions and a sub-tick collision difference.
                    for (var offset = -1; offset <= 1; offset++)
                    {
                        var landing = _tileAt(tiles, x + offset, y);
                        if (landing == null || !_tileLava(landing) || (byte)_liquidField.GetValue(landing) < 128) return false;
                    }
                    return true;
                }
                for (var side = -1; side <= 1; side += 2)
                {
                    var edgeX = (int)((position.X + side * 6f) / 16f);
                    if (IsFullSolid(tiles, edgeX, (int)((position.Y - 6f) / 16f)) ||
                        IsFullSolid(tiles, edgeX, (int)((position.Y + 6f) / 16f))) return false;
                }
            }
            return false;
        }

        private BossStartTick ExecuteLacewingStart(object player, BossStartPlan plan, int tick, bool alreadyIssued)
        {
            var lacewing = FindNpc(661);
            if (lacewing == null)
            {
                return ExecuteSummonPulse(player, plan, tick, alreadyIssued, false,
                    new Vec2(_positionX(player) + _width(player) * .5f + 72f,
                        _positionY(player) + _height(player) * .5f - 24f));
            }

            var weaponSlot = plan.CombatWeaponSlot >= 0 &&
                plan.CombatWeaponSlot < 10 ? plan.CombatWeaponSlot : -1;
            if (weaponSlot < 0)
                return Invalid("the admitted combat output slot is unavailable for the lacewing start");
            SetSelectedItem(player, weaponSlot);
            var items = _inventory(player);
            var weapon = _weaponSelectionSnapshot;
            ReadWeaponInto(player, items, weaponSlot, weapon);
            var target = new TargetSnapshot
            {
                Position = new Vec2(_positionX(lacewing), _positionY(lacewing)),
                Velocity = new Vec2(_velocityX(lacewing), _velocityY(lacewing)),
                Width = _width(lacewing),
                Height = _height(lacewing)
            };
            var playerCenter = new Vec2(_positionX(player) + _width(player) * .5f, _positionY(player) + _height(player) * .5f);
            ClearCombatControls(player);
            var shot = WeaponAimSolver.Solve(weapon.Profile, playerCenter, target.Center, target.Velocity);
            AimAt(player, shot.AimWorld);
            var firingItem = weaponSlot >= 0 && weaponSlot < items.Length ? items[weaponSlot] : null;
            var readyToEmit = shot.CanFire && firingItem != null &&
                _canHitLine(player, lacewing) && SummonActionGate.ShouldFire(
                weapon.IsUsable, weapon.HasAmmo, GetSelectedItem(player) == weaponSlot,
                _itemAutoReuse(firingItem), _itemChannel(firingItem),
                _releaseUseItem(player));
            var unholyTridentDryRouteAdmitted = !readyToEmit ||
                !UnholyTridentCatalog.RequiresDryTrajectoryGate(
                    _itemTypeId(firingItem), _itemShoot(firingItem)) ||
                IsUnholyTridentDryTrajectory(player, shot.AimWorld);
            var reviewedDryRouteAdmitted = !readyToEmit ||
                IsReviewedDryTrajectory(player, shot.AimWorld,
                    _itemTypeId(firingItem), _itemShoot(firingItem));
            var nativeWindRouteAdmitted = !readyToEmit ||
                AllowsNativeWindEmission(weapon.Profile.Profile == null ?
                    OutputRouteKind.Unspecified :
                    weapon.Profile.Profile.OutputKind);
            SetControl(player, "controlUseItem", readyToEmit &&
                unholyTridentDryRouteAdmitted && reviewedDryRouteAdmitted &&
                nativeWindRouteAdmitted);
            return new BossStartTick { Issued = true, StillValid = tick <= plan.TimeoutTicks, ControlsApplied = true };
        }

        private BossStartTick ExecuteWallStart(object player, BossStartPlan plan, int tick, bool alreadyIssued)
        {
            if (alreadyIssued)
                return new BossStartTick { Issued = true, StillValid = tick <= plan.TimeoutTicks };
            if (!SlotContains(player, plan.SummonSlot, plan.ItemType))
                return Invalid("向导巫毒娃娃已不在原快捷栏位置");
            var items = _inventory(player);
            var cursor = _mouseItem();
            if (IsNonEmpty(cursor) || items.Length > 58 && IsNonEmpty(items[58]))
                return Invalid("请先放下鼠标上拿着的物品；拆特不会替你丢弃它");
            var chestStack = _inventoryChestStack(player);
            if (chestStack != null && plan.SummonSlot < chestStack.Length && chestStack[plan.SummonSlot])
                return Invalid("该娃娃槽位正在与箱子交换物品，请完成后重试");
            if (_drawingPlayerChat() || _gamePaused())
                return Invalid("请先关闭对话或暂停，再召唤血肉墙");
            SetSelectedItem(player, plan.SummonSlot);
            ClearCombatControls(player);
            if (!SummonAnimationReady(player) || GetSelectedItem(player) != plan.SummonSlot)
                return new BossStartTick { StillValid = tick <= plan.TimeoutTicks, ControlsApplied = true };
            int throwDirection;
            if (!HasSafeLavaThrow(player, plan.InteractionWorld, out throwDirection))
                return Invalid("请停在开阔岩浆池旁再召唤：需要正向重力、静止及无遮挡的落点；不会自动扔向远处岩浆");
            _setPlayerDirection(player, throwDirection);
            _voodooTransaction = new SingleItemDropTransaction(items, plan.SummonSlot,
                _itemTypeId, _itemStack, _itemFavorited,
                (item, count) => _itemStackField.SetValue(item, count),
                (item, favorite) => _itemFavoritedField.SetValue(item, favorite));
            try
            {
                _voodooTransaction.Prepare();
                // Use vanilla's normal request/network/drop behavior, but complete the
                // split and restore synchronously: no save/cancel window with N-1 hidden.
                _dropSelectedItem(player);
            }
            finally
            {
                _voodooTransaction.Restore(_inventory(player));
            }
            if (!_voodooTransaction.Completed)
                return Invalid("丢弃槽位发生意外变化，已保留恢复事务且停止接管，未覆盖其他物品");
            var dropped = _voodooTransaction.WasDropped;
            _voodooTransaction = null;
            return new BossStartTick
            {
                Issued = dropped,
                StillValid = dropped && tick <= plan.TimeoutTicks,
                ControlsApplied = true,
                FailureReason = dropped ? null : "原版没有完成丢弃；娃娃数量与收藏状态已完整恢复"
            };
        }

        private void RestoreVoodooRemainder(object player, BossStartPlan plan, int tick)
        {
            if (_voodooTransaction == null)
                return;
            if (_voodooTransaction.Restore(_inventory(player))) _voodooTransaction = null;
        }

        public void ResetBossStart()
        {
            _sightCache.Clear();
            _sightFrame = 0;
            _sightQueryState.Clear();
            _destroyerMotionHistory.Clear();
            _destroyerMotionFrame = 0;
            _cachedArena = null;
            _recoverySupport = default(SupportSpan);
            _recoverySupportAge = 0;
            if (_voodooTransaction != null && !_voodooTransaction.Completed)
                throw new InvalidOperationException("Pending item restoration must not be discarded");
            _voodooTransaction = null;
            _summonInitialStack = -1;
            _summonPulseTick = -1;
            _fishingSwapSlot = -1;
        }

        public void RestorePendingBossStart(object player, BossStartPlan plan)
        {
            if (player == null || plan == null)
                return;
            RestoreFishingBaitOrder(player, plan);
            RestoreVoodooRemainder(player, plan, 0);
        }

        private void EnsureTruffleWormIsFirstBait(object player, BossStartPlan plan)
        {
            if (_fishingSwapSlot >= 0)
                return;
            var items = _inventory(player);
            var first = FirstBaitSlot(items);
            if (first < 0 || first == plan.SummonSlot || _itemTypeId(items[first]) == 2673)
                return;
            var temporary = items[first];
            items[first] = items[plan.SummonSlot];
            items[plan.SummonSlot] = temporary;
            _fishingSwapSlot = first;
        }

        private void RestoreFishingBaitOrder(object player, BossStartPlan plan)
        {
            if (plan.Kind != BossSummonKind.TruffleWormFishing || _fishingSwapSlot < 0)
                return;
            var items = _inventory(player);
            var temporary = items[_fishingSwapSlot];
            items[_fishingSwapSlot] = items[plan.SummonSlot];
            items[plan.SummonSlot] = temporary;
            _fishingSwapSlot = -1;
        }

        private int FirstBaitSlot(object[] items)
        {
            for (var i = 54; i < Math.Min(58, items.Length); i++)
                if (items[i] != null && _itemStack(items[i]) > 0 && _itemBait(items[i]) > 0)
                    return i;
            for (var i = 0; i < Math.Min(50, items.Length); i++)
                if (items[i] != null && _itemStack(items[i]) > 0 && _itemBait(items[i]) > 0)
                    return i;
            return -1;
        }

        public int FindBestWeaponSlot(object player)
        {
            // This compatibility overload intentionally has no fabricated
            // target. Callers which have a fresh combat snapshot use the
            // overload below; absent live target/velocity/LOS evidence keeps
            // the old paper-DPS ranking rather than pretending an intercept
            // was proved.
            return FindBestWeaponSlot(player, null, default(Vec2), null);
        }

        private int FindBestWeaponSlot(object player,
            IList<TargetSnapshot> targets, Vec2 playerCenter,
            WeaponSnapshot currentWeapon)
        {
            var items = _inventory(player);
            var current = GetSelectedItem(player);
            var best = current >= 0 && current < 10 ? current : 0;

            TargetSnapshot target;
            var evidence = SelectAutoSwitchTarget(targets, playerCenter,
                out target);
            // A live target exists but its exact position, velocity or clear
            // LOS was not established. Do not use a paper-DPS switch to
            // pretend a strict projectile can reach it. The current route is
            // retained until the next fresh snapshot can prove otherwise.
            if (evidence == AutoSwitchTargetEvidence.Insufficient)
                return best;

            var requiresReachProof = evidence ==
                AutoSwitchTargetEvidence.Reliable;
            var bestScore = WeaponScore(player, items, best,
                requiresReachProof, playerCenter, target, currentWeapon);
            for (var slot = 0; slot < Math.Min(10, items.Length); slot++)
            {
                if (slot == best) continue;
                var score = WeaponScore(player, items, slot,
                    requiresReachProof, playerCenter, target,
                    currentWeapon);
                if (score > bestScore * 1.08f)
                {
                    best = slot;
                    bestScore = score;
                }
            }
            return best;
        }

        private enum AutoSwitchTargetEvidence
        {
            None,
            Insufficient,
            Reliable
        }

        /// <summary>
        /// Selects exactly the primary target ranking used by CombatPlanner,
        /// then checks whether this frame has enough native evidence to use it
        /// for a reach proof. Filtering before ranking would let a visible
        /// minion silently replace the Boss the planner will actually target.
        /// </summary>
        private static AutoSwitchTargetEvidence SelectAutoSwitchTarget(
            IList<TargetSnapshot> targets, Vec2 playerCenter,
            out TargetSnapshot target)
        {
            target = default(TargetSnapshot);
            if (targets == null || targets.Count == 0)
                return AutoSwitchTargetEvidence.None;

            target = targets[0];
            var score = AutoSwitchTargetScore(target, playerCenter);
            for (var index = 1; index < targets.Count; index++)
            {
                var candidate = targets[index];
                var nextScore = AutoSwitchTargetScore(candidate,
                    playerCenter);
                if (nextScore < score)
                {
                    target = candidate;
                    score = nextScore;
                }
            }

            if (target.Life <= 0 || !target.Chaseable ||
                target.Invulnerable || !target.LineOfSightKnown ||
                !target.HasLineOfSight || target.Width < 1 ||
                target.Height < 1 || !IsFinite(playerCenter.X) ||
                !IsFinite(playerCenter.Y) || !IsFinite(target.Position.X) ||
                !IsFinite(target.Position.Y) || !IsFinite(target.Velocity.X) ||
                !IsFinite(target.Velocity.Y))
                return AutoSwitchTargetEvidence.Insufficient;
            return AutoSwitchTargetEvidence.Reliable;
        }

        private static float AutoSwitchTargetScore(TargetSnapshot target,
            Vec2 player)
        {
            // Keep this verbatim with CombatPlanner.TargetScore. The selector
            // is intentionally duplicated here because the facade has to
            // choose a route before it hands the snapshot to the planner.
            var score = Vec2.DistanceSquared(target.Center, player);
            if (target.Boss) score -= 1000000f;
            if (!target.Chaseable || target.Invulnerable) score += 3000000f;
            if (target.LifeMax > 0)
                score += target.Life / (float)target.LifeMax * 1500f;
            return score;
        }

        public string ApplyPlan(object player, ControlPlan plan)
        {
            ClearCombatControls(player);
            if (plan.HoldNeutralControls)
            {
                // This is a current-frame fail-safe, not a Runtime handoff.
                // ClearCombatControls also discards pending optional-mobility
                // validation, and returning here prevents aim/output/resource
                // code from reintroducing an action into the held frame.
                return null;
            }
            SetControl(player, "controlLeft", plan.Horizontal < 0);
            SetControl(player, "controlRight", plan.Horizontal > 0);
            var jumpState = _combatSnapshot.Player.Jump;
            jumpState.ReleaseReady = _releaseJump(player);
            SetControl(player, "controlJump", MovementActionGate.ResolveJump(plan.Jump, plan.JumpAction, in jumpState,
                _combatSnapshot.Player.OnGround, _combatSnapshot.Mobility.Grappling));
            SetControl(player, "controlDown", plan.Drop);
            // Gravitation Potion uses the same releaseUp-gated Up edge in both
            // directions. Down is never a gravity-reversal command.
            SetControl(player, "controlUp", plan.FeatherFallUp || plan.GravityControl != 0);
            SetControl(player, "controlDash", plan.Dash);
            SetControl(player, "controlMount", plan.ToggleMount);
            // Vanilla handles quick-heal before the ordinary selected-item
            // route, but an already-running weapon animation can consume the
            // same update and leave the health potion unapplied.  Treat the
            // armed quick-heal edge as a one-frame resource pulse: keep the
            // movement plan, while suppressing every competing use-item pulse
            // below.  Once vanilla clears releaseQuickHeal, normal firing is
            // allowed again on the next frame.
            var quickHealPulse = plan.QuickHeal && _config.AutoQuickHeal &&
                _releaseQuickHeal(player);
            SetControl(player, "controlQuickHeal", quickHealPulse);
            // Health has priority when both resource edges are requested;
            // vanilla cannot reliably consume two quick-use potions in one
            // update and the weapon pulse is already deferred above.
            SetControl(player, "controlQuickMana", !quickHealPulse &&
                plan.QuickMana && _config.AutoQuickMana);
            var summonWhip = plan.OutputRouteKind ==
                OutputRouteKind.MinionAndWhip;
            var summonPulse = false;
            if (summonWhip && !quickHealPulse)
            {
                var summonObservation = ReadSummonWhipOutput(player,
                    _inventory(player));
                int requestedSlot;
                string failure;
                if (!TryAuthorizeSummonWhipAction(in plan,
                        in summonObservation, out requestedSlot,
                        out summonPulse, out failure))
                {
                    ClearCombatControls(player);
                    return failure;
                }
                if (requestedSlot >= 0)
                    SetSelectedItem(player, requestedSlot);
                // A hook owns the cursor this frame. The planner must keep
                // every item pulse in its FSM until a non-hook frame.
                if (plan.Hook && summonPulse)
                {
                    ClearCombatControls(player);
                    return "summon/whip item pulse conflicts with hook aim";
                }
            }
            if (plan.Hook)
            {
                AimAt(player, plan.HookWorld);
                SetControl(player, "controlHook", true);
            }
            else
            {
                AimAt(player, plan.AimWorld);
                // Test the actual chosen NPC, not an assumed primary target.
                // One native ray per plan is bounded; don't raycast all 200 NPCs.
                var npcs = _npcs();
                bool visible = plan.TargetKey >= 0 && plan.TargetKey < npcs.Length &&
                    npcs[plan.TargetKey] != null && _npcActive(npcs[plan.TargetKey]) &&
                    _canHitLine(player, npcs[plan.TargetKey]);
                if (plan.TargetKey >= 0 && plan.TargetKey < npcs.Length && npcs[plan.TargetKey] != null)
                {
                    var target = npcs[plan.TargetKey];
                    _sightCache.Record(plan.TargetKey, _npcTypeId(target), _combatSnapshot.Player.Center,
                        new Vec2(_positionX(target) + _width(target) * .5f, _positionY(target) + _height(target) * .5f), _sightFrame, visible);
                }
                if (summonWhip)
                {
                    var requiresVisibleTarget = plan.SummonWhipOutputAction ==
                        SummonWhipOutputAction.PulseWhipUse;
                    var nativeWindRouteAdmitted = !summonPulse ||
                        AllowsNativeWindEmission(plan.OutputRouteKind);
                    SetControl(player, "controlUseItem", !quickHealPulse &&
                        summonPulse &&
                        (!requiresVisibleTarget || visible) &&
                        nativeWindRouteAdmitted);
                }
                else
                {
                    var inventory = _inventory(player);
                    int slot = GetSelectedItem(player);
                    var weapon = slot >= 0 && slot < inventory.Length ?
                        inventory[slot] : null;
                    var fire = false;
                    var razorbladePrefireAdmitted = true;
                    if (plan.OutputRouteKind == OutputRouteKind.Unspecified)
                    {
                        // Synthetic adapters retain the historical gate.
                        // Production snapshots carry an exact certificate.
                        fire = WeaponActionGate.ShouldFire(plan.Fire,
                            plan.PreferredWeaponSlot, slot,
                            _requestedSelection);
                    }
                    else if (weapon != null)
                    {
                        var actualWeaponId = _itemTypeId(weapon);
                        var actualAmmoId = 0;
                        var actualProjectileId = _itemShoot(weapon);
                        if (_itemUseAmmo(weapon) != 0)
                        {
                            var ammo = _pickAmmoItem(player, weapon);
                            if (ammo != null && _itemStack(ammo) > 0)
                            {
                                actualAmmoId = _itemTypeId(ammo);
                                actualProjectileId = WeaponProfileCatalog.
                                    ResolveProjectileForPair(actualWeaponId,
                                        actualAmmoId, _itemShoot(ammo),
                                        HasMoltenQuiver(player));
                            }
                            else actualProjectileId = 0;
                        }
                        fire = WeaponActionGate.ShouldFireExact(plan.Fire,
                            plan.PreferredWeaponSlot, slot,
                            _requestedSelection, plan.ExpectedWeaponId,
                            actualWeaponId, plan.ExpectedAmmoId, actualAmmoId,
                            plan.ExpectedProjectileId, actualProjectileId);
                    }
                    if (fire && plan.OutputRouteKind ==
                            OutputRouteKind.HomingMagicProjectile &&
                        plan.ExpectedWeaponId ==
                            CommonWeaponOutputCatalog.
                                RazorbladeTyphoonWeaponId &&
                        plan.ExpectedProjectileId ==
                            CommonWeaponOutputCatalog.
                                RazorbladeTyphoonProjectileId)
                    {
                        // Re-read immediately before the native use-item
                        // level. The planner's observation is not reused as
                        // a lock certificate: NPC selection/immunity and an
                        // old type-409 projectile can have changed since the
                        // snapshot. Failure merely pauses this cast.
                        RazorbladeTyphoonFireObservation observation;
                        razorbladePrefireAdmitted =
                            TryReadRazorbladeTyphoonFireObservation(player,
                                out observation) &&
                            observation.PermitsTarget(plan.TargetKey);
                    }
                    var readyToEmit = weapon != null &&
                        WeaponActionGate.ShouldEmitUseItemUnlessQuickHeal(
                            quickHealPulse, fire && razorbladePrefireAdmitted,
                            visible, _itemAutoReuse(weapon),
                            _itemChannel(weapon), _releaseUseItem(player));
                    var unholyTridentDryRouteAdmitted = true;
                    if (readyToEmit &&
                        UnholyTridentCatalog.RequiresDryTrajectoryGate(
                            plan.ExpectedWeaponId,
                            plan.ExpectedProjectileId))
                    {
                        // Type 114 does not ignore liquid. Once wet, native
                        // HandleMovement uses the pre-AI wetVelocity while
                        // AI has already applied this subupdate's 0.98 decay.
                        // The dry recurrence would then be false, so do not
                        // emit a trident unless a same-frame conservative
                        // corridor scan proves that liquid cannot be entered.
                        unholyTridentDryRouteAdmitted =
                            IsUnholyTridentDryTrajectory(player,
                                plan.AimWorld);
                    }
                    var reviewedDryRouteAdmitted = !readyToEmit ||
                        IsReviewedDryTrajectory(player, plan.AimWorld,
                            plan.ExpectedWeaponId,
                            plan.ExpectedProjectileId);
                    var nativeWindRouteAdmitted = !readyToEmit ||
                        plan.OutputRouteKind == OutputRouteKind.Unspecified ||
                        AllowsNativeWindEmission(plan.OutputRouteKind);
                    SetControl(player, "controlUseItem",
                        readyToEmit && unholyTridentDryRouteAdmitted &&
                        reviewedDryRouteAdmitted &&
                        nativeWindRouteAdmitted);
                }
            }
            PreparePendingMobilityValidation(player, plan);
            return null;
        }

        private static bool TryAuthorizeSummonWhipAction(
            in ControlPlan plan,
            in SummonWhipOutputObservation observation,
            out int requestedSlot, out bool pulseUse, out string reason)
        {
            requestedSlot = -1;
            pulseUse = false;
            reason = null;
            var route = plan.SummonWhipOutputRoute;
            if (plan.OutputRouteKind != OutputRouteKind.MinionAndWhip ||
                !SummonWhipOutputRouteContract.ValidateLive(in observation,
                    in route, out reason))
                return false;

            var staffPhase = plan.SummonWhipOutputPhase ==
                    SummonWhipOutputPhase.SelectStaff ||
                plan.SummonWhipOutputPhase ==
                    SummonWhipOutputPhase.DeployOnce ||
                plan.SummonWhipOutputPhase ==
                    SummonWhipOutputPhase.ConfirmDeployment;
            var expectedSlot = staffPhase ? route.StaffSlot : route.WhipSlot;
            var expectedItem = staffPhase ? route.StaffProfile.ItemId :
                route.WhipProfile.ItemId;
            var expectedAuxiliary = staffPhase ? route.StaffProfile.BuffId : 0;
            var expectedProjectile = staffPhase ?
                route.StaffProfile.ProjectileId : route.WhipProfile.ProjectileId;
            if (plan.PreferredWeaponSlot != expectedSlot ||
                plan.ExpectedWeaponId != expectedItem ||
                plan.ExpectedAmmoId != expectedAuxiliary ||
                plan.ExpectedProjectileId != expectedProjectile)
            {
                reason = "summon/whip frame certificate does not match its phase";
                return false;
            }

            var baseline = SummonWhipOutputRouteContract.
                DeploymentBaselineUnchanged(in observation, in route);
            var confirmed = SummonWhipOutputRouteContract.
                DeploymentConfirmed(in observation, in route);
            switch (plan.SummonWhipOutputAction)
            {
            case SummonWhipOutputAction.SelectStaff:
                if (plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.SelectStaff || plan.Fire ||
                    observation.SelectedSlot == route.StaffSlot || !baseline ||
                    !SummonWhipOutputRouteContract.HasFreeSlotForOneDeployment(
                        in observation, in route))
                    return RejectSummonWhipAction(
                        "staff-selection certificate is stale", out reason);
                requestedSlot = route.StaffSlot;
                reason = null;
                return true;

            case SummonWhipOutputAction.PulseStaffUse:
                if (plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.ConfirmDeployment || !plan.Fire ||
                    observation.SelectedSlot != route.StaffSlot || !baseline ||
                    !SummonWhipOutputRouteContract.HasFreeSlotForOneDeployment(
                        in observation, in route) || !observation.CanUseItem ||
                    !observation.ReleaseUseItem ||
                    observation.ItemAnimation != 0 || observation.ItemTime != 0)
                    return RejectSummonWhipAction(
                        "staff use pulse lost its exact fresh-use proof",
                        out reason);
                pulseUse = true;
                reason = null;
                return true;

            case SummonWhipOutputAction.SelectWhip:
                if ((plan.SummonWhipOutputPhase !=
                         SummonWhipOutputPhase.SelectWhip &&
                     plan.SummonWhipOutputPhase !=
                         SummonWhipOutputPhase.WhipLoop) || plan.Fire ||
                    observation.SelectedSlot == route.WhipSlot || !confirmed)
                    return RejectSummonWhipAction(
                        "whip-selection certificate is stale", out reason);
                requestedSlot = route.WhipSlot;
                reason = null;
                return true;

            case SummonWhipOutputAction.PulseWhipUse:
                if (plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.WhipLoop || !plan.Fire ||
                    observation.SelectedSlot != route.WhipSlot || !confirmed ||
                    !observation.CanUseItem || !observation.ReleaseUseItem ||
                    observation.ItemAnimation != 0 || observation.ItemTime != 0)
                    return RejectSummonWhipAction(
                        "whip use pulse lost its exact fresh-use proof",
                        out reason);
                pulseUse = true;
                reason = null;
                return true;

            case SummonWhipOutputAction.ReleaseUseItem:
                if (plan.Fire ||
                    plan.SummonWhipOutputPhase ==
                        SummonWhipOutputPhase.ConfirmDeployment &&
                        observation.SelectedSlot != route.StaffSlot ||
                    plan.SummonWhipOutputPhase ==
                        SummonWhipOutputPhase.WhipLoop &&
                        (observation.SelectedSlot != route.WhipSlot ||
                         !confirmed) ||
                    plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.ConfirmDeployment &&
                    plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.WhipLoop)
                    return RejectSummonWhipAction(
                        "release frame certificate is stale", out reason);
                reason = null;
                return true;

            case SummonWhipOutputAction.None:
                if (plan.Fire ||
                    plan.SummonWhipOutputPhase ==
                        SummonWhipOutputPhase.DeployOnce &&
                        (observation.SelectedSlot != route.StaffSlot ||
                         !baseline) ||
                    plan.SummonWhipOutputPhase ==
                        SummonWhipOutputPhase.WhipLoop &&
                        (observation.SelectedSlot != route.WhipSlot ||
                         !confirmed) ||
                    plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.DeployOnce &&
                    plan.SummonWhipOutputPhase !=
                        SummonWhipOutputPhase.WhipLoop)
                    return RejectSummonWhipAction(
                        "idle summon/whip frame certificate is stale",
                        out reason);
                reason = null;
                return true;

            default:
                return RejectSummonWhipAction(
                    "failed or unknown summon/whip action cannot reach native input",
                    out reason);
            }
        }

        private static bool RejectSummonWhipAction(string failure,
            out string reason)
        {
            reason = failure;
            return false;
        }

        public void CapturePendingInput(object player)
        {
            foreach (var pair in _controlReaders) _capturedControls[pair.Key] = pair.Value(player);
            // Native Select queues the request until SelectedItemState.Update.
            // Capturing selectedItem here would replay the OLD weapon and cancel
            // every pending summon/weapon change before native Update can apply it.
            _capturedSelection = _requestedSelection >= 0 ? _requestedSelection : GetSelectedItem(player);
            _capturedMouseX = _readMouseX();
            _capturedMouseY = _readMouseY();
        }

        public void ApplyPendingInput(object player)
        {
            foreach (var pair in _capturedControls) _controls[pair.Key](player, pair.Value);
            ApplyPendingSelection(player);
        }

        public void ApplyPendingSelection(object player)
        {
            _setSelectedItem(player, _capturedSelection);
            _mouseX(_capturedMouseX);
            _mouseY(_capturedMouseY);
        }

        private void PreparePendingMobilityValidation(object player, ControlPlan plan)
        {
            ClearPendingMobilityValidation();
            _pendingMobilityFrame = _sightFrame;
            PrepareLateMobilityFallback(player, in plan.LateMobilityFallback);
            if (plan.GravityControl != 0)
            {
                var state = _combatSnapshot.Mobility.GravityFlip;
                state.ControlUp = true;
                GravityFlipCandidate candidate;
                if (GravityFlipMotion.TryCreatePotionCandidate(state,
                    state.GravityDirection < 0f ? 1f : -1f, out candidate))
                {
                    _pendingGravityState = state;
                    _pendingGravityCandidate = candidate;
                    _validatePendingGravity = true;
                }
                else SetControl(player, "controlUp", false);
            }
            var mobility = _combatSnapshot.Mobility;
            // slowFall is a prior-frame aggregate at the early planning hook.
            // Even an ordinary (Up-free) candidate must be checked again after
            // ResetEffects/UpdateBuffs, otherwise the first frame after buff 8
            // reaches zero silently follows non-slow-fall gravity.
            if (mobility.FeatherFall)
            {
                _validatePendingFeatherFall = true;
                _pendingFeatherFallRequiresPotionUp = plan.FeatherFallUp;
            }
            if (plan.FeatherFallUp)
            {
                if (mobility.FeatherFallPotionKnown &&
                    mobility.FeatherFallPotionActive && mobility.FeatherFall &&
                    _combatSnapshot.Player.Jump.Known &&
                    _combatSnapshot.Player.Jump.SlowFall &&
                    !mobility.CanFlipGravity && !mobility.GravityInverted &&
                    !mobility.MountActive && !mobility.Grappling)
                {
                    _validatePendingFeatherFall = true;
                    _pendingFeatherFallRequiresPotionUp = true;
                }
                else
                    SetControl(player, "controlUp", false);
            }
            if (plan.Dash)
            {
                var state = _combatSnapshot.Mobility.EyeShieldDash;
                state.ControlLeft = plan.Horizontal < 0;
                state.ControlRight = plan.Horizontal > 0;
                state.ControlDash = true;
                var direction = plan.Horizontal == -state.FacingDirection
                    ? plan.Horizontal : state.FacingDirection;
                ApplyDashProbe(_combatSnapshot.Mobility, direction, ref state);
                EyeShieldDashCandidate candidate;
                if (EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate))
                {
                    _pendingDashState = state;
                    _pendingDashCandidate = candidate;
                    _validatePendingDash = true;
                }
                else SetControl(player, "controlDash", false);
            }
        }

        /// <summary>
        /// Revalidates only a trajectory-scored optional edge after this frame's
        /// ResetEffects/UpdateBuffs/UpdateEquips and before HorizontalMovement.
        /// It never replans. A rejected dash/gravity edge atomically selects the
        /// planner's edge-free safe fallback; without that certificate (or when
        /// passive feather-fall physics changed), every pending input is neutral.
        /// </summary>
        public string ValidatePendingMobility(object player)
        {
            if (player == null || !_validatePendingGravity && !_validatePendingDash &&
                !_validatePendingFeatherFall)
                return null;
            var mount = _playerMount(player);
            var mountActive = mount != null && _mountActive(mount);
            var identity = default(FunctionalMobilityIdentity);
            if (_validatePendingGravity || _validatePendingDash)
                identity = ReadFunctionalEquipmentIdentity(player,
                    _identityScratch);
            string gravityReason = null;
            string dashReason = null;
            string featherFallReason = null;

            if (_validatePendingGravity)
            {
                var current = ReadGravityFlipState(player, mountActive, identity.Gravity);
                GravityFlipCandidate candidate;
                if (!EquivalentGravityState(_pendingGravityState, current, out gravityReason) ||
                    !GravityFlipMotion.TryCreatePotionCandidate(current,
                        _pendingGravityCandidate.ExpectedGravityDirection, out candidate) ||
                    !candidate.Known || candidate.ExpectedGravityDirection !=
                        _pendingGravityCandidate.ExpectedGravityDirection)
                {
                    if (gravityReason == null) gravityReason = "native-gravity-candidate-changed";
                }
            }

            if (_validatePendingDash)
            {
                var current = ReadEyeShieldDashState(player, mountActive, identity.Dash);
                current.ControlLeft = _controlReaders["controlLeft"](player);
                current.ControlRight = _controlReaders["controlRight"](player);
                current.ControlDash = _controlReaders["controlDash"](player);
                var tiles = _tiles();
                bool probeKnown, probeBlocked;
                ReadDashForwardProbe(player, tiles, _pendingDashCandidate.Direction,
                    out probeKnown, out probeBlocked);
                current.ForwardSolidProbeKnown = probeKnown;
                current.ForwardSolidProbeBlocked = probeBlocked;
                EyeShieldDashCandidate candidate;
                if (!EquivalentDashState(_pendingDashState, current, out dashReason) ||
                    !EyeShieldDashMotion.TryCreateDedicatedCandidate(current, out candidate) ||
                    candidate.Direction != _pendingDashCandidate.Direction ||
                    candidate.Phase != _pendingDashCandidate.Phase)
                {
                    if (dashReason == null) dashReason = "native-dash-candidate-changed";
                }
            }

            if (_validatePendingFeatherFall)
            {
                bool present;
                int remainingTicks;
                var exactBuffKnown = TryReadBuffSlot(_playerBuffType(player),
                    _playerBuffTime(player), 8, out present,
                    out remainingTicks);
                var slowFall = _playerSlowFall(player);
                var upConflict = _pendingFeatherFallRequiresPotionUp &&
                    (mountActive || _playerGrapCount(player) > 0 ||
                     _playerGravDir(player) != 1f ||
                     _playerGravControl(player) ||
                     _playerGravControl2(player) ||
                     _playerForcedGravity(player) != 0 ||
                     !_controlReaders["controlUp"](player));
                if (!slowFall || _pendingFeatherFallRequiresPotionUp &&
                    (!exactBuffKnown || !present || remainingTicks < 0) ||
                    upConflict)
                {
                    featherFallReason = !slowFall ?
                        "slow-fall-effect-expired-before-movement" :
                        !exactBuffKnown ? "buff-array-unknown" :
                        !present || remainingTicks < 0 ? "buff-8-lineage-missing" :
                        mountActive ? "mount-became-active" :
                        _playerGrapCount(player) > 0 ? "grapple-became-active" :
                        _playerGravDir(player) != 1f ? "gravity-inverted" :
                        _playerGravControl(player) || _playerGravControl2(player) ||
                            _playerForcedGravity(player) != 0 ? "gravity-control-conflict" :
                        "up-input-changed";
                }
            }

            _validatePendingGravity = false;
            _validatePendingDash = false;
            _validatePendingFeatherFall = false;
            if (gravityReason == null && dashReason == null && featherFallReason == null)
                return null;
            var usedFallback = ResolveRejectedPendingMobility(player,
                gravityReason != null || dashReason != null,
                featherFallReason != null);
            return "mobility validation rejected at pre-frame=" + _pendingMobilityFrame +
                ", post-frame=" + _sightFrame + ": " +
                (gravityReason == null ? string.Empty : "gravity=" + gravityReason) +
                (gravityReason != null && (dashReason != null || featherFallReason != null)
                    ? "; " : string.Empty) +
                (dashReason == null ? string.Empty : "dash=" + dashReason) +
                (dashReason != null && featherFallReason != null ? "; " : string.Empty) +
                (featherFallReason == null ? string.Empty :
                    "feather-fall=" + featherFallReason) +
                "; resolution=" + (usedFallback ?
                    "pre-scored-edge-free-fallback" : "all-controls-neutral");
        }

        private void PrepareLateMobilityFallback(object player,
            in LateMobilityFallback fallback)
        {
            if (!ValidLateMobilityFallback(in fallback)) return;
            var jump = _combatSnapshot.Player.Jump;
            jump.ReleaseReady = _releaseJump(player);
            _pendingFallbackKnown = true;
            _pendingFallbackLeft = fallback.Horizontal < 0;
            _pendingFallbackRight = fallback.Horizontal > 0;
            _pendingFallbackJump = MovementActionGate.ResolveJump(
                fallback.Jump, fallback.JumpAction, in jump,
                _combatSnapshot.Player.OnGround,
                _combatSnapshot.Mobility.Grappling);
            _pendingFallbackDrop = fallback.Drop;
        }

        private static bool ValidLateMobilityFallback(
            in LateMobilityFallback fallback)
        {
            return fallback.Known &&
                fallback.Horizontal >= -1 && fallback.Horizontal <= 1 &&
                !(fallback.Jump && fallback.Drop) &&
                !float.IsNaN(fallback.Hazard) &&
                !float.IsInfinity(fallback.Hazard) && fallback.Hazard >= 0f;
        }

        private bool ApplyLateMobilityFallback(object player)
        {
            if (!_pendingFallbackKnown) return false;
            // The certificate covers only these four ordinary movement inputs.
            // Fire, item/tile use, hook, mount, dash, Up and quick-consumable
            // inputs may alter animation or physics and were not rescored, so
            // atomically neutralize the entire pending frame before restoring
            // the certified subset.
            NeutralizePendingInput(player);
            SetPendingControl(player, "controlLeft", _pendingFallbackLeft);
            SetPendingControl(player, "controlRight", _pendingFallbackRight);
            SetPendingControl(player, "controlJump", _pendingFallbackJump);
            SetPendingControl(player, "controlDown", _pendingFallbackDrop);
            return true;
        }

        private bool ResolveRejectedPendingMobility(object player,
            bool optionalEdgeRejected, bool featherPhysicsRejected)
        {
            var usedFallback = optionalEdgeRejected &&
                !featherPhysicsRejected && ApplyLateMobilityFallback(player);
            if (!usedFallback) NeutralizePendingInput(player);
            return usedFallback;
        }

        private void NeutralizePendingInput(object player)
        {
            foreach (var pair in _controls)
            {
                pair.Value(player, false);
                _capturedControls[pair.Key] = false;
            }
        }

        private void SetPendingControl(object player, string name, bool value)
        {
            _controls[name](player, value);
            _capturedControls[name] = value;
        }

        private static bool EquivalentGravityState(GravityFlipState expected,
            GravityFlipState current, out string reason)
        {
            if (!current.Known || !current.NormalPlayerUpdatePath)
                return Reject("observation-unknown", out reason);
            if (current.Identity != expected.Identity)
                return Reject("identity-changed", out reason);
            if (current.GravControl != expected.GravControl ||
                current.GravControl2 != expected.GravControl2)
                return Reject("aggregate-control-changed", out reason);
            if (current.ForcedGravity != expected.ForcedGravity ||
                current.MountActive != expected.MountActive)
                return Reject("forced-or-mounted-state-changed", out reason);
            if (!current.ControlUp || current.ReleaseUp != expected.ReleaseUp)
                return Reject("up-edge-changed", out reason);
            if (current.GravityDirection != expected.GravityDirection ||
                current.PositionY != expected.PositionY || current.VelocityY != expected.VelocityY)
                return Reject("gravity-trajectory-input-changed", out reason);
            if (current.JumpTicks != expected.JumpTicks || current.FallStart != expected.FallStart)
                return Reject("jump-or-fall-state-changed", out reason);
            reason = null;
            return true;
        }

        private static bool EquivalentDashState(EyeShieldDashState expected,
            EyeShieldDashState current, out string reason)
        {
            if (!current.Known || !current.NormalPlayerUpdatePath)
                return Reject("observation-unknown", out reason);
            if (current.EquipmentIdentity != expected.EquipmentIdentity ||
                current.DashType != expected.DashType || current.Dash != expected.Dash)
                return Reject("identity-or-dash-type-changed", out reason);
            if (current.DashDelay != expected.DashDelay ||
                current.DashTime != expected.DashTime || current.EocDash != expected.EocDash ||
                current.EocHit != expected.EocHit)
                return Reject("dash-phase-changed", out reason);
            var expectedTime = Math.Min(300, expected.TimeSinceLastDashStarted + 1);
            if (current.TimeSinceLastDashStarted != expectedTime)
                return Reject("dash-clock-changed", out reason);
            if (current.CrowdControlled != expected.CrowdControlled ||
                current.MountActive != expected.MountActive || current.Pulley != expected.Pulley ||
                current.Grappling != expected.Grappling || current.Tongued != expected.Tongued ||
                current.OldStyleParkour != expected.OldStyleParkour)
                return Reject("movement-gate-changed", out reason);
            if (!current.ControlDash || current.ReleaseDash != expected.ReleaseDash ||
                current.ControlLeft != expected.ControlLeft ||
                current.ControlRight != expected.ControlRight)
                return Reject("dash-edge-or-direction-changed", out reason);
            if (current.FacingDirection != expected.FacingDirection ||
                current.VelocityX != expected.VelocityX || current.VelocityY != expected.VelocityY ||
                current.AccRunSpeed != expected.AccRunSpeed ||
                current.MaxRunSpeed != expected.MaxRunSpeed)
                return Reject("dash-trajectory-input-changed", out reason);
            if (!current.ForwardSolidProbeKnown ||
                current.ForwardSolidProbeKnown != expected.ForwardSolidProbeKnown ||
                current.ForwardSolidProbeBlocked != expected.ForwardSolidProbeBlocked)
                return Reject("forward-solid-probe-changed", out reason);
            reason = null;
            return true;
        }

        private static bool Reject(string value, out string reason)
        {
            reason = value;
            return false;
        }

        private static void ApplyDashProbe(MobilitySnapshot mobility, int direction,
            ref EyeShieldDashState state)
        {
            if (direction < 0)
            {
                state.ForwardSolidProbeKnown = mobility.DashLeftProbeKnown;
                state.ForwardSolidProbeBlocked = mobility.DashLeftProbeBlocked;
            }
            else
            {
                state.ForwardSolidProbeKnown = mobility.DashRightProbeKnown;
                state.ForwardSolidProbeBlocked = mobility.DashRightProbeBlocked;
            }
        }

        private void ClearPendingMobilityValidation()
        {
            _validatePendingGravity = false;
            _validatePendingDash = false;
            _validatePendingFeatherFall = false;
            _pendingFeatherFallRequiresPotionUp = false;
            _pendingFallbackKnown = false;
            _pendingFallbackLeft = false;
            _pendingFallbackRight = false;
            _pendingFallbackJump = false;
            _pendingFallbackDrop = false;
            _pendingGravityState = default(GravityFlipState);
            _pendingGravityCandidate = default(GravityFlipCandidate);
            _pendingDashState = default(EyeShieldDashState);
            _pendingDashCandidate = default(EyeShieldDashCandidate);
        }

        public void ClearCombatControls(object player)
        {
            ClearPendingMobilityValidation();
            foreach (var pair in _controls)
                pair.Value(player, false);
        }

        public void Chat(string message, byte r = 238, byte g = 196, byte b = 67)
        {
            if (_config.ShowChatStatus && !_gameMenu())
                _newText.Invoke(null, new object[] { "[拆特] " + message, r, g, b });
        }

        private void AimAt(object player, Vec2 world)
        {
            _mouseX((int)Math.Round(world.X - _screenX()));
            var relativeY = world.Y - _screenY();
            // Vanilla MouseWorld and ItemCheck_Shoot mirror the screen-space
            // cursor while gravity is inverted. Invert that mapping here so
            // weapons, hooks and summon interactions keep the intended target.
            _mouseY((int)Math.Round(_playerGravDir(player) == -1f
                ? _screenHeight() - relativeY : relativeY));
        }

        public bool HasBossType(int type)
        {
            return FindNpc(type) != null;
        }

        private object FindNpc(int type)
        {
            var npcs = _npcs();
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc != null && _npcActive(npc) && _npcTypeId(npc) == type && _npcLife(npc) > 0)
                    return npc;
            }
            return null;
        }

        private bool HasOwnedBobber()
        {
            var owner = _myPlayer();
            var projectiles = _projectiles();
            for (var i = 0; i < projectiles.Length; i++)
            {
                var projectile = projectiles[i];
                if (projectile != null && _projectileActive(projectile) &&
                    _projectileOwner(projectile) == owner && _projectileBobber(projectile))
                    return true;
            }
            return false;
        }

        private bool HasOwnedHookProjectile(int owner)
        {
            if (owner < 0) return true;
            var projectiles = _projectiles();
            if (projectiles == null || _projectileHooks == null) return true;
            for (var i = 0; i < projectiles.Length; i++)
            {
                var projectile = projectiles[i];
                if (projectile == null || !_projectileActive(projectile) ||
                    _projectileOwner(projectile) != owner) continue;
                var type = _projectileTypeId(projectile);
                // An out-of-range native identity cannot be proven harmless.
                if (type < 0 || type >= _projectileHooks.Length ||
                    _projectileHooks[type]) return true;
            }
            return false;
        }

        private bool SlotContains(object player, int slot, int type)
        {
            var items = _inventory(player);
            return slot >= 0 && slot < items.Length && items[slot] != null &&
                   _itemTypeId(items[slot]) == type && _itemStack(items[slot]) > 0;
        }

        private bool SlotContainsFishingRod(object player, int slot)
        {
            var items = _inventory(player);
            return slot >= 0 && slot < items.Length && items[slot] != null &&
                   _itemTypeId(items[slot]) > 0 && _itemFishingPole(items[slot]) > 0;
        }

        private bool InventoryContains(object player, int type)
        {
            var items = _inventory(player);
            for (var i = 0; i < items.Length; i++)
                if (items[i] != null && _itemTypeId(items[i]) == type && _itemStack(items[i]) > 0)
                    return true;
            return false;
        }

        private bool ReadAvailableMount(object player, out float runSpeed,
            out bool canFly, out int itemType, out int mountType)
        {
            runSpeed = 0f;
            canFly = false;
            itemType = 0;
            mountType = -1;
            var item = _quickMountItem(player);
            if (item == null || _itemTypeId(item) <= 0 || _itemStack(item) <= 0) return false;
            var mounts = _mounts();
            itemType = _itemTypeId(item);
            mountType = _itemMountType(item);
            if (mountType < 0 || mountType >= mounts.Length ||
                mounts[mountType] == null) return false;
            runSpeed = _mountDataRunSpeed(mounts[mountType]);
            canFly = MountTypeCanFly(mountType);
            return true;
        }

        private static void PublishMountIdentities(MobilitySnapshot mobility,
            bool mountActive, int activeMountType, bool selectedMountAvailable,
            int selectedMountItemType, int selectedMountType)
        {
            mobility.ActiveMountType = mountActive ? activeMountType : -1;
            VanillaMountDescriptor activeMount;
            mobility.ActiveMountIdentityKnown = mountActive &&
                VanillaMountCatalog.TryGetByMountType(mobility.ActiveMountType,
                    out activeMount);
            mobility.SelectedMountItemType = selectedMountItemType;
            mobility.SelectedMountType = selectedMountType;
            VanillaMountDescriptor selectedMount;
            mobility.SelectedMountIdentityKnown = selectedMountAvailable &&
                VanillaMountCatalog.TryGetByMountType(selectedMountType,
                    out selectedMount) &&
                selectedMount.SummonItemType == selectedMountItemType;
        }

        private bool MountTypeCanFly(int type)
        {
            var mounts = _mounts();
            return type >= 0 && type < mounts.Length && mounts[type] != null &&
                (_mountDataFlightTime(mounts[type]) > 0 || _mountDataUsesHover(mounts[type]));
        }

        private bool HasFiniteFlightResource(object player)
        {
            // rocketTimeMax defaults to 7 even without rocket boots, so a positive
            // capacity alone does not establish that finite flight is equipped.
            return _playerWingsLogic(player) > 0 && _playerWingTimeMax(player) > 0 ||
                _playerRocketBoots(player) > 0 && _playerRocketTimeMax(player) > 0;
        }

        private float FlightResource(object player)
        {
            var wingMax = _playerWingTimeMax(player);
            var rocketMax = _playerRocketTimeMax(player);
            var wing = wingMax > 0 ? _playerWingTime(player) / wingMax : 0f;
            var rocket = rocketMax > 0 ? _playerRocketTime(player) / (float)rocketMax : 0f;
            return Math.Max(0f, Math.Min(1f, Math.Max(wing, rocket)));
        }

        private float WeaponScore(object player, object[] items, int slot)
        {
            return WeaponScore(player, items, slot, false, default(Vec2),
                default(TargetSnapshot), null);
        }

        private float WeaponScore(object player, object[] items, int slot,
            bool requireReachProof, Vec2 playerCenter,
            TargetSnapshot target, WeaponSnapshot currentWeapon)
        {
            if (slot < 0 || slot >= items.Length || items[slot] == null)
                return 0f;
            var item = items[slot];
            // Kiting strategies must not choose a high paper-DPS sword over an
            // available ranged weapon, then consume a summon and swing at air.
            // Projectile presence is only a necessary condition, not proof that
            // every spear/yoyo/minion or other short-range projectile is supported.
            if (!IsCombatWeapon(item) || _itemShoot(item) <= 0)
                return 0f;
            ReadWeaponInto(player, items, slot, _weaponSelectionSnapshot);
            if (_weaponSelectionSnapshot.Profile.Status !=
                    WeaponProfileStatus.Supported ||
                _weaponSelectionSnapshot.Profile.Profile == null ||
                _weaponSelectionSnapshot.Profile.Profile.OutputKind ==
                    OutputRouteKind.MeleeProjectile)
                return 0f;

            var profile = _weaponSelectionSnapshot.Profile.Profile;
            if (requireReachProof &&
                RequiresAutoSwitchReachProof(profile))
            {
                var origin = playerCenter;
                if (profile.Ballistics ==
                    WeaponBallisticKind.RazorbladeTyphoonHoming)
                {
                    // Type 409's native AI chooses and locks a target before
                    // its homing path exists. A generic intercept alone is
                    // not evidence that this particular target can be hit.
                    // Only reuse a pre-fire observation that was captured by
                    // this snapshot for the currently held slot. Never do an
                    // extra full-NPC scan during auto-switch scoring.
                    if (currentWeapon == null || slot != currentWeapon.Slot ||
                        currentWeapon.WeaponId != profile.Key.WeaponId ||
                        !currentWeapon.RazorbladeTyphoon.PermitsTarget(
                            target.Key))
                        return 0f;
                    origin = currentWeapon.RazorbladeTyphoon.SpawnCenter;
                }
                var shot = WeaponAimSolver.Solve(
                    _weaponSelectionSnapshot.Profile, origin,
                    target.Center, target.Velocity, 90f, target.Width,
                    target.Height);
                if (!shot.CanFire)
                    return 0f;
            }
            return _weaponSelectionSnapshot.ApproximateDps;
        }

        private static bool RequiresAutoSwitchReachProof(
            WeaponProfile profile)
        {
            // Every non-straight reviewed route has a dedicated, finite or
            // reach-bounded solver. This includes prefix-only, drag,
            // acceleration, converging, homing, and future source-locked
            // paths (such as Unholy Trident). Melee projectiles are excluded
            // above. Ordinary straight primaries retain the legacy ranking:
            // their generic route has no extra strict-path claim to verify.
            return profile != null && profile.Ballistics !=
                WeaponBallisticKind.StraightPrimaryProjectile;
        }

        private bool IsCombatWeapon(object item) => item != null && _itemTypeId(item) > 0 && _itemStack(item) > 0 &&
            _itemDamage(item) > 0 && _itemUseTime(item) > 0 && _itemUseStyle(item) > 0 &&
            _itemPick(item) == 0 && _itemAxe(item) == 0 && _itemHammer(item) == 0 && _itemCreateTile(item) < 0 && _itemFishingPole(item) == 0;

        private void ReadWeaponInto(object player, object[] items, int slot, WeaponSnapshot weapon)
        {
            weapon.Slot = slot;
            weapon.Damage = 0;
            weapon.UseTime = 1;
            weapon.ShootSpeed = 0;
            weapon.NativeProfileRequired = true;
            weapon.WeaponId = weapon.AmmoId = weapon.ProjectileId = 0;
            weapon.Profile = WeaponProfileCatalog.Evaluate(default(WeaponProfileInput));
            weapon.Mana = default(ManaOutputState);
            weapon.RazorbladeTyphoon = default(RazorbladeTyphoonFireObservation);
            weapon.IsProjectile = weapon.IsMelee = weapon.HasAmmo = weapon.IsUsable = false;
            if (slot < 0 || slot >= items.Length || items[slot] == null)
                return;
            var item = items[slot];
            var shoot = _itemShoot(item);
            var useAmmo = _itemUseAmmo(item);
            weapon.Damage = _itemDamage(item);
            weapon.UseTime = Math.Max(1, _itemUseTime(item));
            weapon.ShootSpeed = Math.Max(0f, _itemShootSpeed(item));
            weapon.IsProjectile = shoot > 0;
            weapon.IsMelee = shoot <= 0;
            weapon.HasAmmo = useAmmo == 0 || HasAmmo(items, useAmmo);
            weapon.IsUsable = IsCombatWeapon(item);
            int itemType = _itemTypeId(item);
            weapon.WeaponId = itemType;
            var input = new WeaponProfileInput
            {
                WeaponId = itemType,
                WeaponShootSpeed = _itemShootSpeed(item),
                WeaponDamageAfterModifiers = weapon.IsUsable ? _weaponDamage(player, item) : 0,
                UseTime = weapon.UseTime,
                UseAnimation = _itemUseAnimation(item),
                ReuseDelay = _itemReuseDelay(item),
                AutoReuse = _itemAutoReuse(item),
                ArrowStateKnown = _arrowStateKnown,
                Archery = _arrowStateKnown && _playerArchery(player),
                MagicQuiver = _arrowStateKnown &&
                    _playerMagicQuiver(player),
                HasMoltenQuiver = HasMoltenQuiver(player),
                SharpBarb = _arrowStateKnown && _playerSharpBarb(player),
                HarpyCharm = _arrowStateKnown && _playerHarpyCharm(player),
                // Ordinary gun shooting runs AFTER ItemCheck decrements the
                // existing/new animation. Zero is catalog's new-animation case.
                AnimationRemainingAtShot = GetSelectedItem(player) == slot && _playerItemAnimation(player) > 0 ?
                    Math.Max(0, _playerItemAnimation(player) - 1) : 0,
                ProjectileId = shoot,
                HasAmmo = weapon.HasAmmo
            };
            if (itemType == 744)
            {
                int effectiveArmorType;
                input.GemStaffFeatureStateKnown =
                    TryReadGemStaffFeatureArmor(player,
                        out effectiveArmorType);
                input.GemStaffEffectiveArmorType = effectiveArmorType;
            }
            if (weapon.IsUsable && useAmmo != 0)
            {
                // Native priority includes cycling/equipped ammo; an inventory
                // scan is not an adequate substitute. This selector is read-only.
                var ammo = _pickAmmoItem(player, item);
                weapon.HasAmmo = ammo != null && _itemStack(ammo) > 0;
                if (weapon.HasAmmo)
                {
                    input.AmmoId = weapon.AmmoId = _itemTypeId(ammo);
                    input.AmmoShootSpeed = _itemShootSpeed(ammo);
                    input.AmmoBaseDamage = _itemDamage(ammo);
                    input.AmmoDamageMultiplier = _weaponDamageMultiplier(player, ammo);
                    input.ProjectileId = _itemShoot(ammo);
                }
                input.HasAmmo = weapon.HasAmmo;
            }
            WeaponProfile reviewed;
            if (useAmmo == 0 && WeaponProfileCatalog.TryGet(itemType, 0,
                    out reviewed) &&
                reviewed.ResourceKind == OutputResourceKind.Mana)
            {
                var rawMana = _itemMana(item);
                var manaMultiplier = _playerManaCost(player);
                var freeSpaceWeapon = _playerSpaceGun(player) &&
                    (itemType == 127 || itemType == 514);
                var scaledMana = freeSpaceWeapon ? 0f :
                    rawMana * manaMultiplier;
                var manaMultiplierKnown = manaMultiplier >= 0f &&
                    !float.IsNaN(manaMultiplier) &&
                    !float.IsInfinity(manaMultiplier);
                var manaKnown = rawMana >= 0 && manaMultiplierKnown &&
                    scaledMana >= 0f && scaledMana < int.MaxValue;
                // Most magic profiles only need the native effective cost.
                // Source-locked variant routes (currently Unholy Trident)
                // additionally validate the raw item mana and multiplier, so
                // an arbitrary resource drift cannot masquerade as a known
                // Item.SetDefaults variant.
                input.ManaBaseCostKnown = rawMana >= 0;
                input.ManaBaseCost = rawMana;
                input.ManaCostMultiplierKnown = manaMultiplierKnown;
                input.ManaCostMultiplier = manaMultiplier;
                input.ManaCostKnown = manaKnown;
                input.ManaCostPerUse = manaKnown ? (int)scaledMana : -1;
                var quickMana = _quickManaItem(player);
                var quickHeal = quickMana == null ? 0 :
                    _itemHealMana(quickMana);
                var manaSickReduction = _playerManaSick(player) ?
                    _playerManaSickReduction(player) : 0f;
                var manaSicknessKnown = manaSickReduction >= 0f &&
                    manaSickReduction < 1f &&
                    !float.IsNaN(manaSickReduction) &&
                    !float.IsInfinity(manaSickReduction);
                weapon.Mana = new ManaOutputState
                {
                    Known = manaKnown && manaSicknessKnown,
                    CurrentMana = _playerMana(player),
                    MaximumMana = _playerMaxMana(player),
                    ManaCostPerUse = input.ManaCostPerUse,
                    RegenerationDelay = _playerManaRegenDelay(player),
                    RegenerationCount = _playerManaRegenCount(player),
                    RegenerationRate = _playerManaRegen(player),
                    PotionDelay = _playerManaPotionDelay(player),
                    QuickManaAutomationAllowed = _config.AutoQuickMana,
                    QuickManaReleaseReady = _releaseQuickMana(player),
                    QuickManaUsableNow = !_playerDead(player) &&
                        !_playerCCed(player) && !_playerNoItems(player) &&
                        !_playerCursed(player),
                    QuickManaItemAvailable = quickMana != null &&
                        quickHeal > 0,
                    QuickManaHeal = Math.Max(0, quickHeal),
                    ManaSicknessKnown = manaSicknessKnown,
                    ManaSicknessReduction = manaSicknessKnown ?
                        manaSickReduction : 0f
                };
            }
            input.ProjectileId = WeaponProfileCatalog.ResolveProjectileForPair(
                input.WeaponId, input.AmmoId, input.ProjectileId,
                input.HasMoltenQuiver);
            var sample = _projectileSample(input.ProjectileId);
            // Exact audited defaults only for reviewed supported bullet IDs.
            // Never create projectiles or initialize ContentSamples while reading.
            input.ProjectileExtraUpdates = sample != null ? _projectileExtraUpdates(sample) :
                input.ProjectileId == 260 || input.ProjectileId == 294 ?
                    100 :
                input.ProjectileId == DemonScytheCatalog.ProjectileId ? 0 :
                input.ProjectileId == UnholyTridentCatalog.ProjectileId ?
                    UnholyTridentCatalog.ExtraUpdates :
                input.ProjectileId == AquaScepterCatalog.ProjectileId ?
                    AquaScepterCatalog.ExtraUpdates :
                input.ProjectileId == 242 ? 7 :
                input.ProjectileId == 88 ? 4 :
                input.ProjectileId == 20 || input.ProjectileId == 279 ||
                input.ProjectileId == 283 ? 2 :
                input.ProjectileId == 15 ? 0 :
                input.ProjectileId == 336 ? 1 :
                input.ProjectileId == 166 ? 0 :
                input.ProjectileId == 134 ? 0 :
                input.ProjectileId == 477 ? 1 :
                input.ProjectileId == 985 || input.ProjectileId == 932 ? 0 :
                // Reviewed non-linear melee projectiles use the vanilla
                // default update cadence. Their AI owns the later motion,
                // so this fallback only supplies identity/lifetime data and
                // never attempts to simulate their path here.
                input.ProjectileId == 6 || input.ProjectileId == 19 ||
                input.ProjectileId == 33 || input.ProjectileId == 52 ||
                input.ProjectileId == 47 || input.ProjectileId == 49 ||
                input.ProjectileId == 106 || input.ProjectileId == 113 ||
                input.ProjectileId == 130 || input.ProjectileId == 218 ||
                input.ProjectileId == 272 || input.ProjectileId == 333 ||
                input.ProjectileId == 541 || input.ProjectileId == 730 ||
                input.ProjectileId == 1103 ? 0 :
                input.ProjectileId == 14 || input.ProjectileId == 89 ||
                input.ProjectileId == 981 ? 1 : 0;
            input.ProjectileLifetimeSubupdates = sample != null ? _projectileTimeLeft(sample) :
                input.ProjectileId == 27 ? 1800 :
                input.ProjectileId == DemonScytheCatalog.ProjectileId ?
                    DemonScytheCatalog.LifetimeSubupdates :
                input.ProjectileId == UnholyTridentCatalog.ProjectileId ?
                    UnholyTridentCatalog.LifetimeSubupdates :
                input.ProjectileId == AquaScepterCatalog.ProjectileId ?
                    AquaScepterCatalog.LifetimeSubupdates :
                input.ProjectileId == 95 ? 3600 :
                input.ProjectileId == 955 || input.ProjectileId == 728 ? 3600 :
                input.ProjectileId == 260 ? 200 :
                input.ProjectileId == 294 ? 300 :
                input.ProjectileId == 15 || input.ProjectileId == 336 ?
                    3600 :
                input.ProjectileId == 166 ? 3600 :
                input.ProjectileId == 134 ? RocketProductionCatalog.LifetimeSubupdates :
                input.ProjectileId == 985 ? 90 :
                input.ProjectileId == 932 ? 120 :
                input.ProjectileId == 478 ? 300 :
                input.ProjectileId == 6 || input.ProjectileId == 19 ||
                input.ProjectileId == 33 || input.ProjectileId == 52 ||
                input.ProjectileId == 47 || input.ProjectileId == 49 ||
                input.ProjectileId == 106 || input.ProjectileId == 113 ||
                input.ProjectileId == 130 || input.ProjectileId == 218 ||
                input.ProjectileId == 272 || input.ProjectileId == 333 ||
                input.ProjectileId == 541 || input.ProjectileId == 730 ||
                input.ProjectileId == 1103 ? 3600 :
                input.ProjectileId == 14 || input.ProjectileId == 20 ||
                input.ProjectileId == 88 || input.ProjectileId == 89 ||
                input.ProjectileId == 242 || input.ProjectileId == 279 ||
                input.ProjectileId == 283 || input.ProjectileId == 981 ||
                input.ProjectileId == 51 || input.ProjectileId == 267 ||
                input.ProjectileId == 477 || input.ProjectileId == 479 ?
                    600 : 0;
            if (input.ProjectileId == 126 && sample == null)
                input.ProjectileLifetimeSubupdates = 300;
            weapon.ProjectileId = input.ProjectileId;
            weapon.Profile = WeaponProfileCatalog.Evaluate(input);
            if (weapon.Profile.Status == WeaponProfileStatus.Supported)
            {
                weapon.Damage = weapon.Profile.DirectDamage;
                weapon.ShootSpeed = weapon.Profile.SpeedPixelsPerTick;
            }
        }

        private bool TryReadGemStaffFeatureArmor(object player,
            out int effectiveArmorType)
        {
            effectiveArmorType = -1;
            if (player == null || _getEffectiveArmor == null ||
                _itemTypeId == null)
                return false;
            try
            {
                // PackGemStaffFeatures uses GetEffectiveArmor(1), the body
                // slot. This is a read-only call and mirrors vanilla's
                // effective vanity/armor resolution without invoking equips.
                var armor = _getEffectiveArmor(player, 1);
                if (armor == null) return false;
                effectiveArmorType = _itemTypeId(armor);
                return effectiveArmorType >= 0;
            }
            catch
            {
                // A missing/incompatible reflection member must disable the
                // Diamond route rather than let an unobserved feature through.
                effectiveArmorType = -1;
                return false;
            }
        }

        private bool HasAmmo(object[] items, int ammoType)
        {
            for (var i = 0; i < items.Length; i++)
                if (items[i] != null && _itemStack(items[i]) > 0 && _itemAmmo(items[i]) == ammoType)
                    return true;
            return false;
        }

        /// <summary>
        /// Samples the native flag immediately before a reviewed output is
        /// emitted. The stock hash-locked executable keeps this false; any
        /// live change or read failure invalidates every no-wind certificate.
        /// This is deliberately one static field read, not a map scan.
        /// </summary>
        private bool AllowsNativeWindEmission(OutputRouteKind routeKind)
        {
            try
            {
                return _windPhysics != null &&
                    NativeWindEmissionGate.PermitsSpecifiedOutput(routeKind,
                        true, _windPhysics());
            }
            catch
            {
                // An incompatible/instrumented field must pause output rather
                // than allowing a wind-sensitive route to guess its motion.
                return false;
            }
        }

        /// <summary>
        /// Type 114's dry flight is source-locked in UnholyTridentCatalog, but
        /// its default does not set ignoreWater.  Wet movement uses the
        /// velocity captured before its aiStyle-27 decay, so an ordinary dry
        /// intercept is no longer authoritative after a liquid entry.  This
        /// checks a same-frame, read-only conservative corridor before input
        /// is emitted.  It intentionally rejects shallow, honey, lava, and
        /// shimmer cells alike instead of attempting to infer a liquid path.
        /// </summary>
        private bool IsUnholyTridentDryTrajectory(object player, Vec2 aim)
        {
            // The native WetCollision query receives a 16x16 projectile
            // rectangle.  Sixteen pixels also covers the sampled segment's
            // nearest-point error and either center/top-left spawn convention.
            try
            {
                if (player == null || !IsFinite(aim.X) ||
                    !IsFinite(aim.Y))
                    return false;
                // This is Player.ItemCheck_Shoot's unmodified pointPosition:
                // RotatedRelativePoint(MountedCenter). Item 683 has none of
                // the later item/projectile spawn-position override branches.
                var origin = _razorbladeSpawnCenter(player);
                if (!IsFinite(origin.X) || !IsFinite(origin.Y)) return false;
                var dx = aim.X - origin.X;
                var dy = aim.Y - origin.Y;
                var distanceSquared = dx * dx + dy * dy;
                if (!IsFinite(distanceSquared) || distanceSquared < 1f ||
                    distanceSquared > UnholyTridentCatalog.
                        DryTrajectoryGateMaximumPathPixels *
                        UnholyTridentCatalog.
                            DryTrajectoryGateMaximumPathPixels)
                    return false;
                var distance = (float)Math.Sqrt(distanceSquared);
                var samples = (int)Math.Ceiling(distance /
                    UnholyTridentCatalog.
                        DryTrajectoryGateSampleSpacingPixels);
                if (samples < 1 || samples > UnholyTridentCatalog.
                        DryTrajectoryGateMaximumSamples)
                    return false;
                var tiles = _tiles();
                if (tiles == null) return false;

                for (var sample = 0; sample <= samples; sample++)
                {
                    var fraction = sample / (float)samples;
                    var x = origin.X + dx * fraction;
                    var y = origin.Y + dy * fraction;
                    var firstX = (int)Math.Floor((x -
                        UnholyTridentCatalog.
                            DryTrajectoryGateLiquidMarginPixels) / 16f);
                    var lastX = (int)Math.Floor((x +
                        UnholyTridentCatalog.
                            DryTrajectoryGateLiquidMarginPixels) / 16f);
                    var firstY = (int)Math.Floor((y -
                        UnholyTridentCatalog.
                            DryTrajectoryGateLiquidMarginPixels) / 16f);
                    var lastY = (int)Math.Floor((y +
                        UnholyTridentCatalog.
                            DryTrajectoryGateLiquidMarginPixels) / 16f);
                    if (firstX < 1 || firstY < 1 ||
                        lastX >= _maxTilesX() - 1 ||
                        lastY >= _maxTilesY() - 1)
                        return false;
                    for (var tx = firstX; tx <= lastX; tx++)
                    for (var ty = firstY; ty <= lastY; ty++)
                    {
                        var tile = TileAt(tiles, tx, ty);
                        if (tile == null ||
                            (byte)_liquidField.GetValue(tile) > 0)
                            return false;
                    }
                }
                return true;
            }
            catch
            {
                // A stale/malformed tile array or incompatible reflection
                // member cannot be converted into a trajectory assumption.
                return false;
            }
        }

        /// <summary>
        /// Same-frame, bounded environment proof for the few reviewed paths
        /// whose native motion changes after entering liquid.  This is neither
        /// a world scan nor a generic aiStyle rule: NativeTrajectoryGateCatalog
        /// returns only Flower of Fire, Cursed Flames, Flower of Frost, and
        /// Aqua Scepter exact item/projectile pairs. Razorpine is excluded
        /// because its native multi-projectile spawn and random ai[0] do not
        /// fit this single path. Unknown paths return true
        /// here because their own route has no declared gate; a known path with
        /// any unreadable tile, invalid origin, or unsafe liquid fails closed.
        /// </summary>
        private bool IsReviewedDryTrajectory(object player, Vec2 aim,
            int weaponId, int projectileId)
        {
            NativeTrajectoryGateProfile profile;
            if (!NativeTrajectoryGateCatalog.TryGet(weaponId, projectileId,
                    out profile))
                return true;
            try
            {
                if (player == null || !IsFinite(aim.X) ||
                    !IsFinite(aim.Y))
                    return false;
                // These reviewed items have no ItemCheck_Shoot spawn-position
                // override. Projectile.NewProjectile interprets this exact
                // RotatedRelativePoint(MountedCenter) pointPosition as Center
                // and subtracts half the projectile dimensions internally.
                var origin = _razorbladeSpawnCenter(player);
                if (!IsFinite(origin.X) || !IsFinite(origin.Y)) return false;
                var direction = (aim - origin).Normalized();
                if (!IsFinite(direction.X) || !IsFinite(direction.Y) ||
                    direction.LengthSquared < .999f)
                    return false;
                var position = origin;
                var velocity = direction *
                    profile.InitialSpeedPixelsPerSubupdate;
                var tiles = _tiles();
                if (tiles == null) return false;

                // Check the current position once before each reviewed native
                // update. Aqua's check is the AI-time Center tile before
                // gravity and movement; all-liquid paths are checked at the
                // same position used by Projectile.Update's wet branch.
                for (var subupdate = 1;
                    subupdate <= profile.MaximumSubupdates; subupdate++)
                {
                    if (!IsReviewedTrajectoryCellSafe(tiles, position,
                            velocity, profile))
                        return false;
                    if (!NativeTrajectoryGateCatalog.TryAdvance(ref position,
                            ref velocity, subupdate, profile))
                        return false;
                }
                return true;
            }
            catch
            {
                // Reflection or tile-array drift must pause only this exact
                // environmental route, never turn an unknown path into a
                // guessed projectile certificate.
                return false;
            }
        }

        private bool IsReviewedTrajectoryCellSafe(Array tiles, Vec2 center,
            Vec2 velocity, NativeTrajectoryGateProfile profile)
        {
            if (!profile.IsSpecified || !IsFinite(center.X) ||
                !IsFinite(center.Y) || !IsFinite(velocity.X) ||
                !IsFinite(velocity.Y))
                return false;
            // AI style 12/type 22 first tests its current Center tile for lava
            // only when the pre-AI velocity is descending. It then updates
            // scale, applies delayed gravity, and Projectile.Update moves it.
            if (profile.LiquidPolicy ==
                NativeTrajectoryGateLiquidPolicy.LavaOnly)
            {
                if (velocity.Y <= 0f) return true;
                var tileX = (int)center.X / 16;
                var tileY = (int)center.Y / 16;
                if (tileX < 0 || tileY < 0 || tileX >= _maxTilesX() ||
                    tileY >= _maxTilesY())
                    return false;
                var lavaTile = TileAt(tiles, tileX, tileY);
                return lavaTile != null && !_tileLava(lavaTile);
            }
            if (profile.LiquidPolicy !=
                NativeTrajectoryGateLiquidPolicy.AllLiquids)
                return false;

            var margin = NativeTrajectoryGateProfile.
                TileScanMarginPixels;
            var firstX = (int)Math.Floor((center.X - margin) / 16f);
            var lastX = (int)Math.Floor((center.X + margin) / 16f);
            var firstY = (int)Math.Floor((center.Y - margin) / 16f);
            var lastY = (int)Math.Floor((center.Y + margin) / 16f);
            if (firstX < 1 || firstY < 1 || lastX >= _maxTilesX() - 1 ||
                lastY >= _maxTilesY() - 1)
                return false;
            for (var x = firstX; x <= lastX; x++)
            for (var y = firstY; y <= lastY; y++)
            {
                var tile = TileAt(tiles, x, y);
                if (tile == null) return false;
                if (profile.LiquidPolicy ==
                    NativeTrajectoryGateLiquidPolicy.AllLiquids)
                {
                    // Any liquid amount is deliberately rejected. The native
                    // wet branch sees projectile rectangles, includes honey
                    // and shimmer, and may use a pre-AI wet velocity; this
                    // dry solver must not predict through it.
                    if ((byte)_liquidField.GetValue(tile) > 0) return false;
                }
            }
            return true;
        }

        private object TileAt(Array tiles, int x, int y)
        {
            return _tileAt(tiles, x, y);
        }

        private bool IsFullSolid(Array tiles, int x, int y)
        {
            var tile = TileAt(tiles, x, y);
            if (tile == null || !_tileActive(tile) || _tileInactive(tile))
                return false;
            var type = _tileType(tile);
            return type < _tileSolid.Length && _tileSolid[type] &&
                   (type >= _tileSolidTop.Length || !_tileSolidTop[type]);
        }

        private bool IsSolid(Array tiles, int x, int y, bool includePlatforms)
        {
            var tile = TileAt(tiles, x, y);
            if (tile == null || !_tileActive(tile) || _tileInactive(tile))
                return false;
            var type = _tileType(tile);
            // Native platforms have BOTH tileSolid and tileSolidTop set. They
            // provide footing below, but are passable while rising (or inverted).
            return type < _tileSolidTop.Length && _tileSolidTop[type] ? includePlatforms :
                type < _tileSolid.Length && _tileSolid[type];
        }

        private struct FunctionalMobilityIdentity
        {
            public GravityControlIdentity Gravity;
            public DashEquipmentIdentity Dash;
            public int UnexpectedFormulaMobilityItemType;
        }

        private void SetControl(object player, string name, bool value) => _controls[name](player, value);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static float Ai(float[] ai, int index) => ai != null && index < ai.Length ? ai[index] : 0f;
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static BossStartTick Invalid(string reason)
        {
            return new BossStartTick { StillValid = false, FailureReason = reason };
        }

        private static bool HasInstanceField(Type type, string name) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance) != null;

        private bool HasMoltenQuiver(object player) => _arrowStateKnown &&
            _playerHasMoltenQuiver(player);

        private static bool ParametersMatch(MethodInfo method, params Type[] expected)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != expected.Length)
                return false;
            for (var i = 0; i < expected.Length; i++)
                if (parameters[i].ParameterType != expected[i])
                    return false;
            return true;
        }

        private static Func<object[]> StaticReferenceArray(Type type, string name)
        {
            return ReflectionAccess.StaticGetter<object[]>(type, name);
        }

        private static Func<Array> StaticArray(Type type, string name)
        {
            return ReflectionAccess.StaticGetter<Array>(type, name);
        }

        private static Func<object, object[]> InstanceReferenceArray(Type type, string name)
        {
            return ReflectionAccess.Getter<object[]>(type, name);
        }

        private bool HasFooting(PlayerSnapshot player, bool inverted, out bool oneWay)
        {
            var tiles = _tiles();
            int y = Clamp((int)((inverted ? player.Position.Y - 1 : player.Position.Y + player.Height + 1) / 16f), 1, _maxTilesY() - 2);
            int left = Clamp((int)(player.Position.X / 16f), 1, _maxTilesX() - 2);
            int right = Clamp((int)((player.Position.X + player.Width - 1) / 16f), 1, _maxTilesX() - 2);
            bool found = false;
            oneWay = !inverted;
            for (var x = left; x <= right && x <= left + 8; x++)
            {
                if (!IsSolid(tiles, x, y, !inverted)) continue;
                found = true;
                var type = _tileType(TileAt(tiles, x, y));
                if (type >= _tileSolidTop.Length || !_tileSolidTop[type]) oneWay = false;
            }
            oneWay &= found;
            return found;
        }

        private bool WithinThreatHorizon(Vec2 center, Vec2 velocity, int width, int height, Vec2 playerCenter)
        {
            // Keep fast/large hazards which can reach the arena even when their
            // current center is outside the ordinary observation radius.
            float horizon = Math.Max(1, Math.Min(120, _config.Planner.HorizonTicks));
            float travel = (Math.Abs(velocity.X) + Math.Abs(velocity.Y)) * horizon;
            float radius = _config.MaximumThreatDistancePixels + travel + Math.Max(width, height);
            return Vec2.DistanceSquared(center, playerCenter) <= radius * radius;
        }

        private bool DestroyerChainConnected(object[] npcs, int slot, int rootKey, int predecessorKey)
        {
            if (slot < 0 || slot >= npcs.Length || slot >= DestroyerMotionHistory.Capacity ||
                rootKey < 0 || rootKey >= npcs.Length || rootKey >= DestroyerMotionHistory.Capacity ||
                predecessorKey < 0 || predecessorKey >= npcs.Length ||
                predecessorKey >= DestroyerMotionHistory.Capacity || predecessorKey == slot) return false;
            var current = npcs[slot];
            if (current == null || !_npcActive(current) || _whoAmI(current) != slot || _npcLife(current) <= 0)
                return false;
            var currentType = _npcTypeId(current);
            if (currentType != 135 && currentType != 136) return false;
            var currentRoot = _npcRealLife(current);
            if (currentRoot < 0) currentRoot = _whoAmI(current);
            if (currentRoot != rootKey) return false;
            var root = npcs[rootKey];
            if (root == null || !_npcActive(root) || _npcTypeId(root) != 134 ||
                _whoAmI(root) != rootKey || _npcLife(root) <= 0)
                return false;
            var predecessor = npcs[predecessorKey];
            if (predecessor == null || !_npcActive(predecessor) || _npcLife(predecessor) <= 0) return false;
            var predecessorType = _npcTypeId(predecessor);
            if (predecessorType != 134 && predecessorType != 135) return false;
            var predecessorEntityKey = _whoAmI(predecessor);
            var predecessorRoot = _npcRealLife(predecessor);
            if (predecessorRoot < 0) predecessorRoot = predecessorEntityKey;
            return predecessorEntityKey == predecessorKey && predecessorRoot == rootKey;
        }

        private static Func<object, object, bool> CompileLineOfSight(Assembly game, Type entityType)
        {
            var position = ReflectionAccess.Field(entityType, "position");
            var width = ReflectionAccess.Field(entityType, "width");
            var height = ReflectionAccess.Field(entityType, "height");
            var method = game.GetType("Terraria.Collision", true).GetMethod("CanHitLine", BindingFlags.Public | BindingFlags.Static,
                null, new[] { position.FieldType, typeof(int), typeof(int), position.FieldType, typeof(int), typeof(int) }, null);
            if (method == null) throw new MissingMethodException("Terraria.Collision", "CanHitLine");
            var player = Expression.Parameter(typeof(object), "player");
            var target = Expression.Parameter(typeof(object), "target");
            var sourceEntity = Expression.Convert(player, entityType);
            var targetEntity = Expression.Convert(target, entityType);
            var call = Expression.Call(method, Expression.Field(sourceEntity, position), Expression.Field(sourceEntity, width),
                Expression.Field(sourceEntity, height), Expression.Field(targetEntity, position), Expression.Field(targetEntity, width), Expression.Field(targetEntity, height));
            return Expression.Lambda<Func<object, object, bool>>(call, player, target).Compile();
        }

        private static Func<object, bool> CompileNpcCanBeChasedBy(Type npcType)
        {
            var method = npcType.GetMethod("CanBeChasedBy",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance, null,
                new[] { typeof(object), typeof(bool) }, null);
            if (method == null || method.ReturnType != typeof(bool))
                throw new MissingMethodException(npcType.FullName,
                    "CanBeChasedBy(object,bool)");
            var npc = Expression.Parameter(typeof(object), "npc");
            var call = Expression.Call(Expression.Convert(npc, npcType),
                method, Expression.Constant(null, typeof(object)),
                Expression.Constant(false));
            return Expression.Lambda<Func<object, bool>>(call, npc).Compile();
        }

        private static Func<object, Vec2> CompileRazorbladeSpawnCenter(
            Type playerType)
        {
            var mountedCenter = ReflectionAccess.Property(playerType,
                "MountedCenter");
            var vectorType = mountedCenter.PropertyType;
            var method = playerType.GetMethod("RotatedRelativePoint",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance, null,
                new[] { vectorType, typeof(bool), typeof(bool), typeof(int) },
                null);
            if (method == null || method.ReturnType != vectorType)
                throw new MissingMethodException(playerType.FullName,
                    "RotatedRelativePoint(Vector2,bool,bool,int)");
            var vectorX = ReflectionAccess.Field(vectorType, "X");
            var vectorY = ReflectionAccess.Field(vectorType, "Y");
            var constructor = typeof(Vec2).GetConstructor(new[] {
                typeof(float), typeof(float) });
            if (constructor == null)
                throw new MissingMethodException(typeof(Vec2).FullName,
                    ".ctor(float,float)");
            var player = Expression.Parameter(typeof(object), "player");
            var typedPlayer = Expression.Convert(player, playerType);
            var center = Expression.Property(typedPlayer, mountedCenter);
            var spawn = Expression.Call(typedPlayer, method, center,
                Expression.Constant(false), Expression.Constant(true),
                Expression.Constant(0));
            var result = Expression.New(constructor,
                Expression.Field(spawn, vectorX),
                Expression.Field(spawn, vectorY));
            return Expression.Lambda<Func<object, Vec2>>(result, player).
                Compile();
        }

        private static Func<Vec2, object, bool> CompileRazorbladeCanHit(
            Assembly game, Type entityType)
        {
            var position = ReflectionAccess.Field(entityType, "position");
            var width = ReflectionAccess.Field(entityType, "width");
            var height = ReflectionAccess.Field(entityType, "height");
            var vectorType = position.FieldType;
            var method = game.GetType("Terraria.Collision", true).GetMethod(
                "CanHit", BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static, null,
                new[] { vectorType, typeof(int), typeof(int), vectorType,
                    typeof(int), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(bool))
                throw new MissingMethodException("Terraria.Collision",
                    "CanHit(Vector2,int,int,Vector2,int,int)");
            var vectorConstructor = vectorType.GetConstructor(new[] {
                typeof(float), typeof(float) });
            if (vectorConstructor == null)
                throw new MissingMethodException(vectorType.FullName,
                    ".ctor(float,float)");
            var center = Expression.Parameter(typeof(Vec2), "center");
            var target = Expression.Parameter(typeof(object), "target");
            var targetEntity = Expression.Convert(target, entityType);
            var half = Expression.Constant(
                CommonWeaponOutputCatalog.RazorbladeTyphoonProjectileHalfSize);
            var sourcePosition = Expression.New(vectorConstructor,
                Expression.Subtract(Expression.Field(center, "X"), half),
                Expression.Subtract(Expression.Field(center, "Y"), half));
            var call = Expression.Call(method, sourcePosition,
                Expression.Constant(30), Expression.Constant(30),
                Expression.Field(targetEntity, position),
                Expression.Field(targetEntity, width),
                Expression.Field(targetEntity, height));
            return Expression.Lambda<Func<Vec2, object, bool>>(call, center,
                target).Compile();
        }

        private readonly FieldInfo _liquidField;
    }
}
