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
        private readonly Func<bool> _gameMenu;
        private readonly Func<bool> _blockInput;
        private readonly Func<object[]> _npcs;
        private readonly Func<object[]> _projectiles;
        private readonly Func<Array> _tiles;
        private readonly Func<int> _maxTilesX;
        private readonly Func<int> _maxTilesY;
        private readonly Func<bool> _dayTime;
        private readonly Func<double> _time;
        private readonly Func<bool> _hardMode;
        private readonly Func<bool> _expertMode;
        private readonly Func<bool> _masterMode;
        private readonly Func<bool> _goodWorld;
        private readonly Func<bool> _remixWorld;
        private readonly Func<bool> _zenithWorld;
        private readonly Func<bool> _celebrationWorld;
        private readonly Func<bool> _constantWorld;
        private readonly Func<bool> _noTrapsWorld;
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

        private readonly Func<object, bool> _npcActive;
        private readonly Func<object, bool> _npcBoss;
        private readonly Func<object, bool> _npcFriendly;
        private readonly Func<object, bool> _npcChaseable;
        private readonly Func<object, bool> _npcInvulnerable;
        private readonly Func<object, int> _npcDamage;
        private readonly Func<object, int> _npcLife;
        private readonly Func<object, int> _npcLifeMax;
        private readonly Func<object, int> _npcTypeId;
        private readonly Func<object, int> _whoAmI;
        private readonly Func<object, int> _npcRealLife;
        private readonly Func<object, float[]> _npcAi;

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
        private readonly Func<object, object, object> _pickAmmoItem;
        private readonly Func<int, object> _projectileSample;
        private readonly Func<object, int> _projectileTypeId;
        private readonly Func<object, int> _projectileOwner;
        private readonly Func<object, bool> _projectileBobber;
        private readonly Func<object, float[]> _projectileAi;
        private readonly Func<object, float[]> _projectileLocalAi;
        private readonly Func<object, float> _projectileScale;
        private readonly Func<object, float> _projectileRotation;

        private readonly Func<object, bool> _playerDead;
        private readonly Func<object, bool> _playerActive;
        private readonly Func<object, bool> _releaseUseItem;
        private readonly Func<object, bool> _releaseJump;
        private readonly Func<object, int> _playerItemAnimation;
        private readonly Func<object, int> _playerItemTime;
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
        private readonly Func<object, float> _playerGravity;
        private readonly Func<object, float> _playerMaxFallSpeed;
        private readonly Func<object, float> _playerMaxRunSpeed;
        private readonly Func<object, float> _playerAccessoryRunSpeed;
        private readonly Func<object, float> _playerRunAcceleration;
        private readonly Func<object, float> _playerRunSlowdown;
        private readonly Func<object, int> _playerWingsLogic;
        private readonly Func<object, float> _playerJumpSpeedBoost;
        private readonly Func<object, float> _playerWingTime;
        private readonly Func<object, int> _playerWingTimeMax;
        private readonly Func<object, int> _playerRocketTime;
        private readonly Func<object, int> _playerRocketTimeMax;
        private readonly Func<object, int> _playerDashType;
        private readonly Func<object, int> _playerDashDelay;
        private readonly Func<object, int> _playerGrapCount;
        private readonly Func<object, float> _playerGravDir;
        private readonly Func<object, bool> _playerGravControl;
        private readonly Func<object, bool> _playerSlowFall;
        private readonly Func<object, bool> _playerDontHurtCritters;
        private readonly Func<object, object> _playerMount;
        private readonly Func<object, object[]> _inventory;
        private readonly Func<object, object> _quickMountItem;
        private readonly Func<object, object> _quickGrappleItem;
        private readonly Func<object, int> _selectedItem;
        private readonly Action<object, int> _setSelectedItem;

        private readonly Func<object, bool> _zoneCorrupt;
        private readonly Func<object, bool> _zoneCrimson;
        private readonly Func<object, bool> _zoneHallow;
        private readonly Func<object, bool> _zoneJungle;
        private readonly Func<object, bool> _zoneSnow;
        private readonly Func<object, bool> _zoneBeach;
        private readonly Func<object, bool> _zoneOverworld;
        private readonly Func<object, bool> _zoneUnderworld;

        private readonly Func<object, bool> _mountActive;
        private readonly Func<object, int> _mountTypeId;
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
        private readonly MethodInfo _itemSetDefaults;

        private readonly Func<object, ushort> _tileType;
        private readonly Func<object, bool> _tileActive;
        private readonly Func<object, bool> _tileInactive;
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
        private readonly MethodInfo _newText;
        private readonly CombatSnapshot _combatSnapshot = new CombatSnapshot();
        private readonly EncounterObservation _observation = new EncounterObservation();
        private readonly List<int> _activeBossKeys = new List<int>(32);
        private readonly TargetSightCache _sightCache = new TargetSightCache();
        private int _sightFrame;
        private int _sightQueryBudget;

        private ArenaSnapshot _cachedArena;
        private Vec2 _cachedArenaAt;
        private int _arenaCacheTicks;
        private SingleItemDropTransaction _voodooTransaction;
        private int _summonInitialStack = -1;
        private int _summonPulseTick = -1;
        private int _fishingSwapSlot = -1;

        public TerrariaFacade(Assembly game, Type playerType, ChaiteConfig config)
        {
            _config = config;
            _mainType = game.GetType("Terraria.Main", true);
            var npcType = game.GetType("Terraria.NPC", true);
            var projectileType = game.GetType("Terraria.Projectile", true);
            var itemType = game.GetType("Terraria.Item", true);
            var entityType = game.GetType("Terraria.Entity", true);
            var worldGenType = game.GetType("Terraria.WorldGen", true);
            var mountType = game.GetType("Terraria.Mount", true);
            var tileType = game.GetType("Terraria.Tile", true);

            _myPlayer = ReflectionAccess.StaticGetter<int>(_mainType, "myPlayer");
            _gameMenu = ReflectionAccess.StaticGetter<bool>(_mainType, "gameMenu");
            _blockInput = ReflectionAccess.StaticGetter<bool>(_mainType, "blockInput");
            _npcs = StaticReferenceArray(_mainType, "npc");
            _projectiles = StaticReferenceArray(_mainType, "projectile");
            _tiles = StaticArray(_mainType, "tile");
            _maxTilesX = ReflectionAccess.StaticGetter<int>(_mainType, "maxTilesX");
            _maxTilesY = ReflectionAccess.StaticGetter<int>(_mainType, "maxTilesY");
            _dayTime = ReflectionAccess.StaticGetter<bool>(_mainType, "dayTime");
            _time = ReflectionAccess.StaticGetter<double>(_mainType, "time");
            _hardMode = ReflectionAccess.StaticGetter<bool>(_mainType, "hardMode");
            _expertMode = ReflectionAccess.StaticPropertyGetter<bool>(_mainType, "expertMode");
            _masterMode = ReflectionAccess.StaticPropertyGetter<bool>(_mainType, "masterMode");
            _goodWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "getGoodWorld");
            _remixWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "remixWorld");
            _zenithWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "zenithWorld");
            _celebrationWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "tenthAnniversaryWorld");
            _constantWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "dontStarveWorld");
            _noTrapsWorld = ReflectionAccess.StaticGetter<bool>(_mainType, "noTrapsWorld");
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

            _npcActive = ReflectionAccess.Getter<bool>(npcType, "active");
            _npcBoss = ReflectionAccess.Getter<bool>(npcType, "boss");
            _npcFriendly = ReflectionAccess.Getter<bool>(npcType, "friendly");
            _npcChaseable = ReflectionAccess.Getter<bool>(npcType, "chaseable");
            _npcInvulnerable = ReflectionAccess.Getter<bool>(npcType, "dontTakeDamage");
            _npcDamage = ReflectionAccess.Getter<int>(npcType, "damage");
            _npcLife = ReflectionAccess.Getter<int>(npcType, "life");
            _npcLifeMax = ReflectionAccess.Getter<int>(npcType, "lifeMax");
            _npcTypeId = ReflectionAccess.Getter<int>(npcType, "type");
            _whoAmI = ReflectionAccess.Getter<int>(entityType, "whoAmI");
            _npcRealLife = ReflectionAccess.Getter<int>(npcType, "realLife");
            _npcAi = ReflectionAccess.Getter<float[]>(npcType, "ai");

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
            _pickAmmoItem = ReflectionAccess.MethodGetterWithArgument<object>(playerType, "PickAmmo_PickAmmoItem", itemType);
            _projectileSample = ReflectionAccess.StaticIntDictionaryValueGetter(game.GetType("Terraria.ID.ContentSamples", true), "ProjectilesByType");
            _projectileTypeId = ReflectionAccess.Getter<int>(projectileType, "type");
            _projectileOwner = ReflectionAccess.Getter<int>(projectileType, "owner");
            _projectileBobber = ReflectionAccess.Getter<bool>(projectileType, "bobber");
            _projectileAi = ReflectionAccess.Getter<float[]>(projectileType, "ai");
            _projectileLocalAi = ReflectionAccess.Getter<float[]>(projectileType, "localAI");
            _projectileScale = ReflectionAccess.Getter<float>(projectileType, "scale");
            _projectileRotation = ReflectionAccess.Getter<float>(projectileType, "rotation");

            _playerDead = ReflectionAccess.Getter<bool>(playerType, "dead");
            _playerActive = ReflectionAccess.Getter<bool>(playerType, "active");
            _releaseUseItem = ReflectionAccess.Getter<bool>(playerType, "releaseUseItem");
            _releaseJump = ReflectionAccess.Getter<bool>(playerType, "releaseJump");
            _playerItemAnimation = ReflectionAccess.Getter<int>(playerType, "itemAnimation");
            _playerItemTime = ReflectionAccess.Getter<int>(playerType, "itemTime");
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
            _playerGravity = ReflectionAccess.Getter<float>(playerType, "gravity");
            _playerMaxFallSpeed = ReflectionAccess.Getter<float>(playerType, "maxFallSpeed");
            _playerMaxRunSpeed = ReflectionAccess.Getter<float>(playerType, "maxRunSpeed");
            _playerAccessoryRunSpeed = ReflectionAccess.Getter<float>(playerType, "accRunSpeed");
            _playerRunAcceleration = ReflectionAccess.Getter<float>(playerType, "runAcceleration");
            _playerRunSlowdown = ReflectionAccess.Getter<float>(playerType, "runSlowdown");
            _playerWingsLogic = ReflectionAccess.Getter<int>(playerType, "wingsLogic");
            _playerJumpSpeedBoost = ReflectionAccess.Getter<float>(playerType, "jumpSpeedBoost");
            _playerWingTime = ReflectionAccess.Getter<float>(playerType, "wingTime");
            _playerWingTimeMax = ReflectionAccess.Getter<int>(playerType, "wingTimeMax");
            _playerRocketTime = ReflectionAccess.Getter<int>(playerType, "rocketTime");
            _playerRocketTimeMax = ReflectionAccess.Getter<int>(playerType, "rocketTimeMax");
            _playerDashType = ReflectionAccess.Getter<int>(playerType, "dashType");
            _playerDashDelay = ReflectionAccess.Getter<int>(playerType, "dashDelay");
            _playerGrapCount = ReflectionAccess.Getter<int>(playerType, "grapCount");
            _playerGravDir = ReflectionAccess.Getter<float>(playerType, "gravDir");
            _playerGravControl = ReflectionAccess.Getter<bool>(playerType, "gravControl");
            _playerSlowFall = ReflectionAccess.Getter<bool>(playerType, "slowFall");
            _playerDontHurtCritters = ReflectionAccess.Getter<bool>(playerType, "dontHurtCritters");
            _playerMount = ReflectionAccess.Getter<object>(playerType, "mount");
            _inventory = InstanceReferenceArray(playerType, "inventory");
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
            _zoneSnow = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneSnow");
            _zoneBeach = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneBeach");
            _zoneOverworld = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneOverworldHeight");
            _zoneUnderworld = ReflectionAccess.PropertyGetter<bool>(playerType, "ZoneUnderworldHeight");

            _mountActive = ReflectionAccess.PropertyGetter<bool>(mountType, "Active");
            _mountTypeId = ReflectionAccess.PropertyGetter<int>(mountType, "Type");
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
            _itemSetDefaults = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 &&
                             m.GetParameters()[0].ParameterType == typeof(int));

            _tileType = ReflectionAccess.Getter<ushort>(tileType, "type");
            _tileActive = ReflectionAccess.MethodGetter<bool>(tileType, "active");
            _tileInactive = ReflectionAccess.MethodGetter<bool>(tileType, "inActive");
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

            _newText = _mainType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "NewText" && ParametersMatch(m, typeof(string), typeof(byte), typeof(byte), typeof(byte)));
            _liquidField = liquidField;
        }

        public bool IsLocalPlayer(object player, int index) => index == _myPlayer() && _playerActive(player) && !_gameMenu();
        public bool IsDead(object player) => _playerDead(player);
        public bool IsInputBlocked => _blockInput();
        public int GetNpcType(object npc) => _npcTypeId(npc);
        public int GetSelectedItem(object player) => _selectedItem(player);
        public void BeginInputFrame()
        {
            _requestedSelection = -1;
            // Activation preflight and same-frame waiting survival can both read
            // a snapshot. Share one budget, rather than giving each three rays.
            _sightQueryBudget = 3;
        }
        public void SetSelectedItem(object player, int slot)
        {
            _requestedSelection = slot;
            _setSelectedItem(player, slot);
        }
        public bool IsBoss(object npc) => npc != null && _npcBoss(npc);

        public EncounterObservation BuildObservation(object player, IList<int> killedBosses,
            bool startAuthorized = false, bool waitingStillValid = true)
        {
            var activeBosses = _activeBossKeys;
            activeBosses.Clear();
            var npcs = _npcs();
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc != null && _npcActive(npc) && _npcBoss(npc))
                {
                    int key = BossKey(npc);
                    if (!activeBosses.Contains(key)) activeBosses.Add(key);
                }
            }
            _observation.Flags = activeBosses.Count > 0 ? EncounterFlags.Boss : EncounterFlags.None;
            _observation.PlayerDead = _playerDead(player);
            _observation.PlayerLife = _playerLife(player);
            _observation.ActiveBossKeys = activeBosses;
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

        public BossStartPlan FindBossStartPlan(object player)
        {
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
                if (_npcBoss(npc)) result.ActiveBossTypes.Add(type);
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
            state.JumpSpeedBoost = _playerJumpSpeedBoost(player);
            state.WingTime = _playerWingTime(player);
            state.RocketTime = _playerRocketTime(player);
            state.OnGround = Math.Abs(vy) < .01f && HasFooting(state, _playerGravDir(player) < 0);
            state.Dead = _playerDead(player);
            state.WorldLeft = 16f;
            state.WorldRight = _maxTilesX() * 16f - 16f;
            state.WorldTop = 16f;
            state.WorldBottom = _maxTilesY() * 16f - 16f;

            var mount = _playerMount(player);
            var mountActive = mount != null && _mountActive(mount);
            var items = _inventory(player);
            float bestMountSpeed;
            bool bestMountCanFly;
            var hasMount = ReadAvailableMount(player, out bestMountSpeed, out bestMountCanFly);
            var mobility = snapshot.Mobility;
            mobility.MountActive = mountActive;
            mobility.MountCanFly = mountActive ? MountTypeCanFly(_mountTypeId(mount)) : bestMountCanFly;
            state.CanSprintInAir = _playerWingsLogic(player) > 0 || (mountActive && mobility.MountCanFly);
            mobility.HasUsableMount = hasMount;
            mobility.MountRunSpeed = mountActive ? _mountRunSpeed(mount) : bestMountSpeed;
            mobility.CanDash = _playerDashType(player) != 0;
            mobility.DashReady = _playerDashDelay(player) <= 0;
            mobility.DashType = _playerDashType(player);
            mobility.HasGrapple = _quickGrappleItem(player) != null;
            mobility.Grappling = _playerGrapCount(player) > 0;
            mobility.CanFlipGravity = _playerGravControl(player);
            mobility.GravityInverted = _playerGravDir(player) < 0f;
            mobility.FeatherFall = _playerSlowFall(player);
            mobility.FlightResourceFraction = FlightResource(player);
            var difficulty = snapshot.Difficulty;
            difficulty.Expert = _expertMode();
            difficulty.Master = _masterMode();
            difficulty.ForTheWorthy = _goodWorld();
            difficulty.Remix = _remixWorld();
            difficulty.Zenith = _zenithWorld();
            difficulty.Celebration = _celebrationWorld();
            difficulty.Constant = _constantWorld();
            difficulty.NoTraps = _noTrapsWorld();
            difficulty.DayTime = _dayTime();

            var slot = useBestHotbarWeapon ? FindBestWeaponSlot(player) :
                Math.Max(0, Math.Min(items.Length - 1, GetSelectedItem(player)));
            ReadWeaponInto(player, items, slot, snapshot.Weapon);
            snapshot.Arena = ReadArena(snapshot.Player);
            ReadTargetsAndThreats(player, snapshot);
            snapshot.LineOfSightToPrimary = true;
            return snapshot;
        }

        private void ReadTargetsAndThreats(object player, CombatSnapshot snapshot)
        {
            var playerCenter = snapshot.Player.Center;
            var maxTargetDistanceSquared = _config.MaximumTargetDistancePixels * (float)_config.MaximumTargetDistancePixels;
            var npcs = _npcs();
            for (var i = 0; i < npcs.Length; i++)
            {
                var npc = npcs[i];
                if (npc == null || !_npcActive(npc) || _npcFriendly(npc) || _npcLife(npc) <= 0)
                    continue;
                var position = new Vec2(_positionX(npc), _positionY(npc));
                var width = _width(npc);
                var height = _height(npc);
                var center = new Vec2(position.X + width * .5f, position.Y + height * .5f);
                var distanceSquared = Vec2.DistanceSquared(center, playerCenter);
                var boss = _npcBoss(npc);
                if (distanceSquared <= maxTargetDistanceSquared)
                {
                    var ai = _npcAi(npc);
                    bool visible;
                    var known = _sightCache.TryGet(_whoAmI(npc), _npcTypeId(npc), playerCenter, center, _sightFrame, out visible);
                    snapshot.Targets.Add(new TargetSnapshot
                    {
                        Key = _whoAmI(npc),
                        Type = _npcTypeId(npc),
                        Position = position,
                        Velocity = new Vec2(_velocityX(npc), _velocityY(npc)),
                        Width = width,
                        Height = height,
                        Life = _npcLife(npc),
                        LifeMax = _npcLifeMax(npc),
                        Damage = _npcDamage(npc),
                        Boss = boss,
                        Chaseable = _npcChaseable(npc),
                        Invulnerable = _npcInvulnerable(npc),
                        LineOfSightKnown = known,
                        HasLineOfSight = visible,
                        Ai0 = Ai(ai, 0), Ai1 = Ai(ai, 1), Ai2 = Ai(ai, 2), Ai3 = Ai(ai, 3)
                    });
                }
                var npcVelocity = new Vec2(_velocityX(npc), _velocityY(npc));
                if (_npcDamage(npc) > 0 && WithinThreatHorizon(center, npcVelocity, width, height, playerCenter))
                {
                    snapshot.Threats.Add(new ThreatSnapshot
                    {
                        Kind = ThreatKind.NpcContact,
                        Position = position,
                        Velocity = new Vec2(_velocityX(npc), _velocityY(npc)),
                        Width = width,
                        Height = height,
                        Damage = _npcDamage(npc),
                        TimeLeft = int.MaxValue,
                        Type = _npcTypeId(npc)
                    });
                }
            }

            // Probe at most three nearby unknown targets. Remember blocked worm
            // segments long enough to try another one, instead of alternating
            // forever between two underground segments with a one-entry cache.
            // ApplyPlan still performs an exact final ray before every shot.
            for (var query = 0; query < 3 && _sightQueryBudget > 0; query++)
            {
                int best = -1;
                float distance = float.MaxValue;
                for (int i = 0; i < snapshot.Targets.Count; i++)
                {
                    var candidate = snapshot.Targets[i];
                    if (candidate.LineOfSightKnown || candidate.Invulnerable || !candidate.Chaseable) continue;
                    float next = Vec2.DistanceSquared(candidate.Center, playerCenter);
                    if (next < distance) { best = i; distance = next; }
                }
                if (best < 0) break;
                var target = snapshot.Targets[best];
                target.LineOfSightKnown = true;
                _sightQueryBudget--;
                target.HasLineOfSight = _canHitLine(player, npcs[target.Key]);
                _sightCache.Record(target.Key, target.Type, playerCenter, target.Center, _sightFrame, target.HasLineOfSight);
                snapshot.Targets[best] = target;
            }

            var projectiles = _projectiles();
            for (var i = 0; i < projectiles.Length; i++)
            {
                var projectile = projectiles[i];
                if (projectile == null || !_projectileActive(projectile) || !_projectileHostile(projectile) || _projectileDamage(projectile) <= 0)
                    continue;
                var position = new Vec2(_positionX(projectile), _positionY(projectile));
                var center = new Vec2(position.X + _width(projectile) * .5f, position.Y + _height(projectile) * .5f);
                var velocity = new Vec2(_velocityX(projectile), _velocityY(projectile));
                int projectileType = _projectileTypeId(projectile);
                bool isBeam = projectileType == 455 || projectileType == 923;
                int updates = isBeam ? 1 : Math.Max(1, _projectileExtraUpdates(projectile) + 1);
                var tickVelocity = velocity * updates;
                // The beam's origin may be far away while its damaging segment
                // crosses the player. Let Core's beam broadphase test its full reach.
                if (!isBeam && !WithinThreatHorizon(center, tickVelocity, _width(projectile), _height(projectile), playerCenter))
                    continue;
                var threat = new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Position = position,
                    Velocity = velocity,
                    Width = _width(projectile),
                    Height = _height(projectile),
                    Damage = _projectileDamage(projectile),
                    TimeLeft = _projectileTimeLeft(projectile),
                    Type = projectileType
                };
                if (isBeam)
                {
                    var ai = _projectileAi(projectile);
                    var localAi = _projectileLocalAi(projectile);
                    threat.Geometry = projectileType == 455 ? ThreatGeometry.MoonLordDeathray : ThreatGeometry.EmpressSunDance;
                    threat.BeamOrigin = center;
                    threat.BeamDirection = velocity;
                    threat.BeamAngularVelocity = Ai(ai,0);
                    threat.BeamBaseAngle = Ai(ai,0);
                    threat.BeamAngle = _projectileRotation(projectile);
                    threat.BeamAge = Ai(localAi,0);
                    threat.BeamLength = Ai(localAi,1);
                    threat.BeamScale = _projectileScale(projectile);
                    threat.BeamScaleLimit = 1f;
                    int owner = (int)Ai(ai,1);
                    if (owner >= 0 && owner < npcs.Length && npcs[owner] != null && _npcActive(npcs[owner]))
                    {
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
                snapshot.Threats.Add(threat);
            }
        }

        private ArenaSnapshot ReadArena(PlayerSnapshot player)
        {
            if (_cachedArena != null && _arenaCacheTicks++ < 20 &&
                Vec2.DistanceSquared(player.Center, _cachedArenaAt) < 64f * 64f)
                return _cachedArena;

            _arenaCacheTicks = 0;
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
            arena.LocalOpenBounds = new RectF(player.Center.X - arena.ClearanceLeft + 8f,
                player.Center.Y - arena.ClearanceUp + 8f,
                Math.Max(16f, arena.HorizontalClearance - 16f),
                Math.Max(16f, arena.VerticalClearance - 16f));
            FindGrappleAnchors(tiles, centerX, centerY, arena);
            _cachedArena = arena;
            return arena;
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
            if (!InventoryContains(player, 2673))
                return Invalid("松露虫已不在物品栏");
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

            var weaponSlot = FindBestWeaponSlot(player);
            SetSelectedItem(player, weaponSlot);
            var items = _inventory(player);
            var weapon = ReadWeapon(player, items, weaponSlot);
            var target = new TargetSnapshot
            {
                Position = new Vec2(_positionX(lacewing), _positionY(lacewing)),
                Velocity = new Vec2(_velocityX(lacewing), _velocityY(lacewing)),
                Width = _width(lacewing),
                Height = _height(lacewing)
            };
            var playerCenter = new Vec2(_positionX(player) + _width(player) * .5f, _positionY(player) + _height(player) * .5f);
            ClearCombatControls(player);
            AimAt(player, InterceptSolver.PredictAim(playerCenter, target.Center, target.Velocity,
                weapon.IsProjectile ? weapon.ShootSpeed : 0f));
            var firingItem = items[weaponSlot];
            SetControl(player, "controlUseItem", firingItem != null && SummonActionGate.ShouldFire(
                weapon.IsUsable, weapon.HasAmmo, GetSelectedItem(player) == weaponSlot,
                _itemAutoReuse(firingItem), _itemChannel(firingItem), _releaseUseItem(player)));
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
            var items = _inventory(player);
            var current = GetSelectedItem(player);
            var best = current >= 0 && current < 10 ? current : 0;
            var bestScore = WeaponScore(items, best);
            for (var slot = 0; slot < Math.Min(10, items.Length); slot++)
            {
                var score = WeaponScore(items, slot);
                if (score > bestScore * 1.08f)
                {
                    best = slot;
                    bestScore = score;
                }
            }
            return best;
        }

        public void ApplyPlan(object player, ControlPlan plan)
        {
            ClearCombatControls(player);
            SetControl(player, "controlLeft", plan.Horizontal < 0);
            SetControl(player, "controlRight", plan.Horizontal > 0);
            SetControl(player, "controlJump", MovementActionGate.ShouldHoldJump(plan.Jump,
                _combatSnapshot.Player.OnGround, _releaseJump(player), _combatSnapshot.Mobility.Grappling));
            SetControl(player, "controlDown", plan.Drop || plan.GravityControl < 0);
            SetControl(player, "controlUp", plan.GravityControl > 0);
            SetControl(player, "controlDash", plan.Dash);
            SetControl(player, "controlMount", plan.ToggleMount);
            SetControl(player, "controlQuickHeal", plan.QuickHeal && _config.AutoQuickHeal);
            SetControl(player, "controlQuickMana", plan.QuickMana && _config.AutoQuickMana);
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
                var inventory = _inventory(player);
                int slot = GetSelectedItem(player);
                var weapon = slot >= 0 && slot < inventory.Length ? inventory[slot] : null;
                bool hold = weapon != null && (_itemAutoReuse(weapon) || _itemChannel(weapon) || _releaseUseItem(player));
                SetControl(player, "controlUseItem", plan.Fire && visible && hold);
            }
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

        public void ClearCombatControls(object player)
        {
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

        private bool ReadAvailableMount(object player, out float runSpeed, out bool canFly)
        {
            runSpeed = 0f;
            canFly = false;
            var item = _quickMountItem(player);
            if (item == null || _itemTypeId(item) <= 0 || _itemStack(item) <= 0) return false;
            var mounts = _mounts();
            int type = _itemMountType(item);
            if (type < 0 || type >= mounts.Length || mounts[type] == null) return false;
            runSpeed = _mountDataRunSpeed(mounts[type]);
            canFly = MountTypeCanFly(type);
            return true;
        }

        private bool MountTypeCanFly(int type)
        {
            var mounts = _mounts();
            return type >= 0 && type < mounts.Length && mounts[type] != null &&
                (_mountDataFlightTime(mounts[type]) > 0 || _mountDataUsesHover(mounts[type]));
        }

        private float FlightResource(object player)
        {
            var wingMax = _playerWingTimeMax(player);
            var rocketMax = _playerRocketTimeMax(player);
            var wing = wingMax > 0 ? _playerWingTime(player) / wingMax : 0f;
            var rocket = rocketMax > 0 ? _playerRocketTime(player) / (float)rocketMax : 0f;
            return Math.Max(0f, Math.Min(1f, Math.Max(wing, rocket)));
        }

        private float WeaponScore(object[] items, int slot)
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
            int ammo = _itemUseAmmo(item);
            if (ammo != 0 && !HasAmmo(items, ammo)) return 0f;
            return _itemDamage(item) * 60f / _itemUseTime(item) * (_itemShoot(item) > 0 ? 1.18f : 1f);
        }

        private WeaponSnapshot ReadWeapon(object player, object[] items, int slot)
        {
            var result = new WeaponSnapshot();
            ReadWeaponInto(player, items, slot, result);
            return result;
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
            // Verified standard-bullet paths. Other weapons keep their previous
            // approximation until their ammo conversions/ballistics are verified.
            int itemType = _itemTypeId(item);
            if ((itemType == 98 || itemType == 434) && useAmmo == 97)
            {
                var ammo = _pickAmmoItem(player, item);
                weapon.HasAmmo = ammo != null && _itemStack(ammo) > 0;
                if (weapon.HasAmmo)
                {
                    int shotType = _itemShoot(ammo);
                    var sample = _projectileSample(shotType);
                    int extra = sample != null ? _projectileExtraUpdates(sample) :
                        shotType == 14 || shotType == 89 ? 1 : shotType == 104 || shotType == 279 ? 2 : shotType == 242 ? 7 : 0;
                    weapon.ShootSpeed = Math.Max(0f, _itemShootSpeed(item) + _itemShootSpeed(ammo)) * Math.Max(1, extra + 1);
                }
            }
        }

        private bool HasAmmo(object[] items, int ammoType)
        {
            for (var i = 0; i < items.Length; i++)
                if (items[i] != null && _itemStack(items[i]) > 0 && _itemAmmo(items[i]) == ammoType)
                    return true;
            return false;
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

        private void SetControl(object player, string name, bool value) => _controls[name](player, value);
        private static float Ai(float[] ai, int index) => ai != null && index < ai.Length ? ai[index] : 0f;
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static BossStartTick Invalid(string reason)
        {
            return new BossStartTick { StillValid = false, FailureReason = reason };
        }

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

        private bool HasFooting(PlayerSnapshot player, bool inverted)
        {
            var tiles = _tiles();
            int y = Clamp((int)((inverted ? player.Position.Y - 1 : player.Position.Y + player.Height + 1) / 16f), 1, _maxTilesY() - 2);
            int left = Clamp((int)(player.Position.X / 16f), 1, _maxTilesX() - 2);
            int right = Clamp((int)((player.Position.X + player.Width - 1) / 16f), 1, _maxTilesX() - 2);
            return IsSolid(tiles, left, y, !inverted) || IsSolid(tiles, right, y, !inverted);
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

        private readonly FieldInfo _liquidField;
    }
}
