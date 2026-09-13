using System;
using System.Linq.Expressions;
using System.Reflection;
using Chaite.Core;

namespace Chaite.Plugin
{
    /// <summary>
    /// Read-only adapter for the single reviewed vanilla profile: item 84 and
    /// projectile 13 in Terraria 1.4.5.8. ReadAtPlayerUpdateEntry must only be
    /// called by Runtime.Tick's hash-locked Player.Update entry hook; that is the
    /// same grappling-link state consumed by the later GrappleMovement call.
    /// </summary>
    internal sealed class NativeGrappleReader : IBasicHookWorldEvidenceSource
    {
        private readonly Func<int> _myPlayer, _netMode, _maxTilesX, _maxTilesY;
        private readonly Func<object[]> _projectiles;
        private readonly Func<Array> _tiles;
        private readonly Func<Array, int, int, object> _tileAt;
        private readonly bool[] _projectileHooks, _tileSolid, _tileSolidTop;
        private readonly Func<int, object> _projectileSample;

        private readonly Func<object, object> _quickGrappleItem;
        private readonly Func<object, int> _whoAmI, _width, _height;
        private readonly Func<object, float> _positionX, _positionY, _velocityX, _velocityY;
        private readonly Func<object, bool> _playerActive, _dead, _crowdControlled;
        private readonly Func<object, bool> _tongued, _noItems, _cursed, _wet, _pulley;
        private readonly Func<object, bool> _releaseHook, _releaseJump;
        private readonly Func<object, bool> _gravityControl, _gravityControl2, _slowFall;
        private readonly Func<object, float> _gravityDirection;
        private readonly Func<object, int> _grappleCount, _itemAnimation, _itemTime;
        private readonly Func<object, int> _quickGrappleCooldown;
        private readonly Func<object, int[]> _grappling;
        private readonly Func<object, object> _mount;
        private readonly Func<object, bool> _mountActive;
        private readonly Func<bool> _sharedGrappleInput;

        private readonly Func<object, int> _itemType, _itemStack, _itemShoot;
        private readonly Func<object, int> _itemUseStyle, _itemUseAnimation, _itemUseTime;
        private readonly Func<object, float> _itemShootSpeed;
        private readonly Func<object, bool> _itemNoUseGraphic, _itemNoMelee;

        private readonly Func<object, bool> _projectileActive, _projectileNetImportant;
        private readonly Func<object, bool> _projectileTileCollide;
        private readonly Func<object, int> _projectileOwner, _projectileType, _projectileAiStyle;
        private readonly Func<object, float[]> _projectileAi;

        private readonly Func<object, ushort> _tileType;
        private readonly Func<object, bool> _tileNativeActive, _tileHalfBrick;
        private readonly Func<object, byte> _tileSlope;
        private readonly Func<object, int, int, bool> _isBlacklisted;
        private object _worldPlayer;

        public NativeGrappleReader(Assembly game, Type playerType)
            : this(game.GetType("Terraria.Main", true), playerType,
                game.GetType("Terraria.Entity", true),
                game.GetType("Terraria.Projectile", true),
                game.GetType("Terraria.Item", true),
                game.GetType("Terraria.Tile", true),
                game.GetType("Terraria.Mount", true),
                game.GetType("Terraria.GameInput.PlayerInput", true),
                game.GetType("Terraria.ID.ContentSamples", true))
        {
        }

        internal NativeGrappleReader(Type mainType, Type playerType, Type entityType,
            Type projectileType, Type itemType, Type tileType, Type mountType,
            Type playerInputType, Type contentSamplesType)
        {
            if (mainType == null || playerType == null || entityType == null ||
                projectileType == null || itemType == null || tileType == null ||
                mountType == null || playerInputType == null || contentSamplesType == null)
                throw new ArgumentNullException(nameof(mainType));

            _myPlayer = ReflectionAccess.StaticGetter<int>(mainType, "myPlayer");
            _netMode = ReflectionAccess.StaticGetter<int>(mainType, "netMode");
            _maxTilesX = ReflectionAccess.StaticGetter<int>(mainType, "maxTilesX");
            _maxTilesY = ReflectionAccess.StaticGetter<int>(mainType, "maxTilesY");
            _projectiles = ReflectionAccess.StaticGetter<object[]>(mainType, "projectile");
            _tiles = ReflectionAccess.StaticGetter<Array>(mainType, "tile");
            _tileAt = ReflectionAccess.ArrayElementGetter2D(
                ReflectionAccess.Field(mainType, "tile").FieldType);
            _projectileHooks = ReflectionAccess.StaticGetter<bool[]>(mainType, "projHook")();
            _tileSolid = ReflectionAccess.StaticGetter<bool[]>(mainType, "tileSolid")();
            _tileSolidTop = ReflectionAccess.StaticGetter<bool[]>(mainType, "tileSolidTop")();
            _projectileSample = ReflectionAccess.StaticIntDictionaryValueGetter(
                contentSamplesType, "ProjectilesByType");

            _quickGrappleItem = ReflectionAccess.MethodGetter<object>(playerType,
                "QuickGrapple_GetItemToUse");
            _whoAmI = ReflectionAccess.Getter<int>(entityType, "whoAmI");
            _width = ReflectionAccess.Getter<int>(entityType, "width");
            _height = ReflectionAccess.Getter<int>(entityType, "height");
            _positionX = ReflectionAccess.VectorComponentGetter(entityType, "position", "X");
            _positionY = ReflectionAccess.VectorComponentGetter(entityType, "position", "Y");
            _velocityX = ReflectionAccess.VectorComponentGetter(entityType, "velocity", "X");
            _velocityY = ReflectionAccess.VectorComponentGetter(entityType, "velocity", "Y");
            _wet = ReflectionAccess.Getter<bool>(entityType, "wet");
            _playerActive = ReflectionAccess.Getter<bool>(playerType, "active");
            _dead = ReflectionAccess.Getter<bool>(playerType, "dead");
            _crowdControlled = ReflectionAccess.PropertyGetter<bool>(playerType, "CCed");
            _tongued = ReflectionAccess.Getter<bool>(playerType, "tongued");
            _noItems = ReflectionAccess.Getter<bool>(playerType, "noItems");
            _cursed = ReflectionAccess.Getter<bool>(playerType, "cursed");
            _pulley = ReflectionAccess.Getter<bool>(playerType, "pulley");
            _releaseHook = ReflectionAccess.Getter<bool>(playerType, "releaseHook");
            _releaseJump = ReflectionAccess.Getter<bool>(playerType, "releaseJump");
            _gravityDirection = ReflectionAccess.Getter<float>(playerType, "gravDir");
            _gravityControl = ReflectionAccess.Getter<bool>(playerType, "gravControl");
            _gravityControl2 = ReflectionAccess.Getter<bool>(playerType, "gravControl2");
            _slowFall = ReflectionAccess.Getter<bool>(playerType, "slowFall");
            _grappleCount = ReflectionAccess.Getter<int>(playerType, "grapCount");
            _grappling = ReflectionAccess.Getter<int[]>(playerType, "grappling");
            _itemAnimation = ReflectionAccess.Getter<int>(playerType, "itemAnimation");
            _itemTime = ReflectionAccess.Getter<int>(playerType, "itemTime");
            _quickGrappleCooldown = ReflectionAccess.Getter<int>(playerType,
                "_quickGrappleCooldown");
            _mount = ReflectionAccess.Getter<object>(playerType, "mount");
            _mountActive = ReflectionAccess.PropertyGetter<bool>(mountType, "Active");
            _sharedGrappleInput = ReflectionAccess.StaticGetter<bool>(playerInputType,
                "GrappleAndInteractAreShared");

            _itemType = ReflectionAccess.Getter<int>(itemType, "type");
            _itemStack = ReflectionAccess.Getter<int>(itemType, "stack");
            _itemShoot = ReflectionAccess.Getter<int>(itemType, "shoot");
            _itemShootSpeed = ReflectionAccess.Getter<float>(itemType, "shootSpeed");
            _itemUseStyle = ReflectionAccess.Getter<int>(itemType, "useStyle");
            _itemUseAnimation = ReflectionAccess.Getter<int>(itemType, "useAnimation");
            _itemUseTime = ReflectionAccess.Getter<int>(itemType, "useTime");
            _itemNoUseGraphic = ReflectionAccess.Getter<bool>(itemType, "noUseGraphic");
            _itemNoMelee = ReflectionAccess.Getter<bool>(itemType, "noMelee");

            _projectileActive = ReflectionAccess.Getter<bool>(projectileType, "active");
            _projectileOwner = ReflectionAccess.Getter<int>(projectileType, "owner");
            _projectileType = ReflectionAccess.Getter<int>(projectileType, "type");
            _projectileAiStyle = ReflectionAccess.Getter<int>(projectileType, "aiStyle");
            _projectileAi = ReflectionAccess.Getter<float[]>(projectileType, "ai");
            _projectileNetImportant = ReflectionAccess.Getter<bool>(projectileType,
                "netImportant");
            _projectileTileCollide = ReflectionAccess.Getter<bool>(projectileType,
                "tileCollide");

            _tileType = ReflectionAccess.Getter<ushort>(tileType, "type");
            _tileNativeActive = ReflectionAccess.MethodGetter<bool>(tileType, "nactive");
            _tileSlope = ReflectionAccess.MethodGetter<byte>(tileType, "slope");
            _tileHalfBrick = ReflectionAccess.MethodGetter<bool>(tileType, "halfBrick");
            _isBlacklisted = CompileBlacklistReader(playerType);
        }

        public BasicHookFrameSnapshot ReadAtPlayerUpdateEntry(object player,
            long sequence, bool playerGrounded)
        {
            var frame = default(BasicHookFrameSnapshot);
            if (player == null || sequence <= 0) return frame;
            var playerIndex = _whoAmI(player);
            var mainPlayer = _myPlayer();
            var netMode = _netMode();
            var width = _width(player);
            var height = _height(player);
            var position = new Vec2(_positionX(player), _positionY(player));
            var velocity = new Vec2(_velocityX(player), _velocityY(player));
            var center = new Vec2(position.X + width * .5f, position.Y + height * .5f);
            var shared = _sharedGrappleInput();
            var mount = _mount(player);
            var mountActive = mount != null && _mountActive(mount);
            var gravityDirection = _gravityDirection(player);

            frame.Known = playerIndex >= 0 && mainPlayer >= 0 && width > 0 && height > 0 &&
                Finite(center) && Finite(velocity) && (netMode == 0 || netMode == 1 || netMode == 2);
            frame.Sequence = sequence;
            frame.PlayerCenter = center;
            frame.PlayerVelocity = velocity;
            frame.PlayerGrounded = playerGrounded;
            frame.ReleaseJump = _releaseJump(player);
            frame.Identity = ReadIdentity(player);
            frame.Context = new BasicHookUseContext
            {
                Known = true,
                LocalPlayerKnown = netMode == 0 && playerIndex == mainPlayer,
                LocalPlayerIndex = playerIndex,
                Dead = _dead(player) || !_playerActive(player),
                CrowdControlled = _crowdControlled(player),
                Tongued = _tongued(player),
                NoItems = _noItems(player),
                GrappleAndInteractShared = shared,
                MountActive = mountActive,
                NormalGravity = gravityDirection == 1f,
                GravityControlActive = _gravityControl(player) || _gravityControl2(player),
                Pulley = _pulley(player),
                Wet = _wet(player),
                SlowFall = _slowFall(player),
                ReleaseHook = _releaseHook(player),
                ItemStartGateKnown = true,
                ItemStartGateOpen = ExactStartGate(player, frame.Identity, shared,
                    mountActive, netMode, playerIndex, mainPlayer)
            };
            ReadProjectileAndLink(player, playerIndex, ref frame);
            return frame;
        }

        public void PrepareWorldEvidence(object player)
        {
            _worldPlayer = player;
        }

        public int MaxTilesX => _maxTilesX();
        public int MaxTilesY => _maxTilesY();

        public bool TryReadTile(int tileX, int tileY,
            out BasicHookAnchorObservation tile)
        {
            tile = default(BasicHookAnchorObservation);
            var maxX = MaxTilesX;
            var maxY = MaxTilesY;
            var tiles = _tiles();
            if (_worldPlayer == null || tiles == null || tileX < 0 || tileY < 0 ||
                tileX >= maxX || tileY >= maxY) return false;
            var native = _tileAt(tiles, tileX, tileY);
            if (native == null) return false;
            var type = _tileType(native);
            if (type >= _tileSolid.Length || type >= _tileSolidTop.Length) return false;
            tile = new BasicHookAnchorObservation
            {
                Known = true,
                TileX = tileX,
                TileY = tileY,
                TileType = type,
                NativeActive = _tileNativeActive(native),
                NativeSolid = _tileSolid[type],
                SolidTop = _tileSolidTop[type],
                Platform = _tileSolidTop[type],
                MinecartTrack = type == 314,
                Shaped = _tileSlope(native) != 0 || _tileHalfBrick(native),
                Blacklisted = _isBlacklisted(_worldPlayer, tileX, tileY),
                HookCenter = new Vec2(tileX * 16f + 8f, tileY * 16f + 8f)
            };
            return true;
        }

        public bool TrySweepPlayer(in RectF before, in RectF after,
            out bool pathClear, out bool platformFree)
        {
            pathClear = false;
            platformFree = false;
            if (!Finite(before) || !Finite(after) || before.Width <= 0f || before.Height <= 0f ||
                after.Width <= 0f || after.Height <= 0f) return false;
            var left = Math.Min(before.Left, after.Left);
            var top = Math.Min(before.Top, after.Top);
            var right = Math.Max(before.Right, after.Right);
            var bottom = Math.Max(before.Bottom, after.Bottom);
            var startX = (int)Math.Floor(left / 16f);
            var startY = (int)Math.Floor(top / 16f);
            var endX = (int)Math.Floor((right - .001f) / 16f);
            var endY = (int)Math.Floor((bottom - .001f) / 16f);
            if (startX < 0 || startY < 0 || endX >= MaxTilesX || endY >= MaxTilesY)
                return false;
            pathClear = true;
            platformFree = true;
            for (var x = startX; x <= endX; x++)
            for (var y = startY; y <= endY; y++)
            {
                BasicHookAnchorObservation tile;
                if (!TryReadTile(x, y, out tile)) return false;
                if (!tile.NativeActive) continue;
                if (tile.SolidTop || tile.Platform) platformFree = false;
                if (tile.NativeSolid || tile.MinecartTrack) pathClear = false;
            }
            return true;
        }

        private BasicHookIdentity ReadIdentity(object player)
        {
            var item = _quickGrappleItem(player);
            if (item == null) return default(BasicHookIdentity);
            var projectileType = _itemShoot(item);
            var sample = projectileType >= 0 ? _projectileSample(projectileType) : null;
            var marked = projectileType >= 0 && projectileType < _projectileHooks.Length &&
                _projectileHooks[projectileType];
            var identity = new BasicHookIdentity
            {
                ResolvedByQuickGrapple = true,
                ProjectileMarkedAsHook = marked,
                ItemType = _itemType(item),
                ProjectileType = projectileType,
                ShootSpeed = _itemShootSpeed(item),
                UseStyle = _itemUseStyle(item),
                UseAnimation = _itemUseAnimation(item),
                UseTime = _itemUseTime(item),
                NoUseGraphic = _itemNoUseGraphic(item),
                NoMelee = _itemNoMelee(item)
            };
            if (sample != null)
            {
                identity.ProjectileAiStyle = _projectileAiStyle(sample);
                identity.ProjectileWidth = _width(sample);
                identity.ProjectileHeight = _height(sample);
                identity.ProjectileNetImportant = _projectileNetImportant(sample);
                identity.ProjectileTileCollide = _projectileTileCollide(sample);
            }
            identity.MaximumSimultaneousHooks = identity.ItemType == BasicHookMotion.ItemType &&
                projectileType == BasicHookMotion.ProjectileType ? 1 : 0;
            identity.Known = item != null && sample != null && marked &&
                identity.ItemType == BasicHookMotion.ItemType &&
                projectileType == BasicHookMotion.ProjectileType;
            return identity;
        }

        private bool ExactStartGate(object player, BasicHookIdentity identity,
            bool shared, bool mountActive, int netMode, int playerIndex, int mainPlayer)
        {
            var item = _quickGrappleItem(player);
            return BasicHookMotion.MatchesExactIdentity(in identity) && item != null &&
                _itemType(item) == BasicHookMotion.ItemType && _itemStack(item) > 0 &&
                netMode == 0 && playerIndex == mainPlayer && _playerActive(player) &&
                !_dead(player) && !_crowdControlled(player) && !_tongued(player) &&
                !_noItems(player) && !_cursed(player) && !shared && !mountActive &&
                _releaseHook(player) && _releaseJump(player) &&
                _itemAnimation(player) == 0 && _itemTime(player) == 0 &&
                _quickGrappleCooldown(player) == 0;
        }

        private void ReadProjectileAndLink(object player, int playerIndex,
            ref BasicHookFrameSnapshot frame)
        {
            var grappling = _grappling(player);
            var count = _grappleCount(player);
            frame.Link = new BasicHookLinkObservation
            {
                Known = grappling != null && grappling.Length > 0 && count >= 0 && count <= 1,
                AtGrappleMovementEntry = true,
                GrappleCount = count,
                FirstProjectileIndex = grappling != null && grappling.Length > 0
                    ? grappling[0] : -1
            };
            if (!frame.Link.Known) frame.Known = false;

            var projectiles = _projectiles();
            if (projectiles == null) { frame.Known = false; return; }
            var found = -1;
            object observed = null;
            for (var index = 0; index < projectiles.Length; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null || !_projectileActive(projectile) ||
                    _projectileOwner(projectile) != playerIndex ||
                    _projectileType(projectile) != BasicHookMotion.ProjectileType ||
                    _projectileAiStyle(projectile) != BasicHookMotion.ProjectileAiStyle)
                    continue;
                if (found >= 0) { frame.Known = false; return; }
                found = index;
                observed = projectile;
            }
            if (found < 0) return;
            var ai = _projectileAi(observed);
            var center = new Vec2(_positionX(observed) + _width(observed) * .5f,
                _positionY(observed) + _height(observed) * .5f);
            if (ai == null || ai.Length < 1 || !Finite(ai[0]) || !Finite(center))
            {
                frame.Known = false;
                return;
            }
            frame.ProjectileObserved = true;
            frame.Projectile = new BasicHookProjectileObservation
            {
                Known = true,
                Active = true,
                Index = found,
                Owner = _projectileOwner(observed),
                Type = _projectileType(observed),
                AiStyle = _projectileAiStyle(observed),
                AiState = ai[0],
                Center = center
            };
            if (ai[0] != 2f) return;
            var tileX = (int)Math.Floor(center.X / 16f);
            var tileY = (int)Math.Floor(center.Y / 16f);
            BasicHookAnchorObservation anchor;
            PrepareWorldEvidence(player);
            if (!TryReadTile(tileX, tileY, out anchor)) { frame.Known = false; return; }
            frame.Anchor = anchor;
        }

        private static Func<object, int, int, bool> CompileBlacklistReader(Type playerType)
        {
            var method = playerType.GetMethod("IsBlacklistedForGrappling",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null || method.ReturnType != typeof(bool))
                throw new MissingMethodException(playerType.FullName,
                    "IsBlacklistedForGrappling");
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
                throw new MissingMethodException(playerType.FullName,
                    "IsBlacklistedForGrappling(Point)");
            var constructor = parameters[0].ParameterType.GetConstructor(
                new[] { typeof(int), typeof(int) });
            if (constructor == null)
                throw new MissingMethodException(parameters[0].ParameterType.FullName,
                    ".ctor(int,int)");
            var player = Expression.Parameter(typeof(object), "player");
            var x = Expression.Parameter(typeof(int), "x");
            var y = Expression.Parameter(typeof(int), "y");
            var point = Expression.New(constructor, x, y);
            var call = Expression.Call(Expression.Convert(player, method.DeclaringType),
                method, point);
            return Expression.Lambda<Func<object, int, int, bool>>(call,
                player, x, y).Compile();
        }

        private static bool Finite(in RectF value)
        {
            return Finite(value.Left) && Finite(value.Top) && Finite(value.Width) &&
                Finite(value.Height) && Finite(value.Right) && Finite(value.Bottom);
        }

        private static bool Finite(Vec2 value) => Finite(value.X) && Finite(value.Y);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
