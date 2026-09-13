using Chaite.Core;
using System;

namespace Chaite.Plugin
{
    /// <summary>
    /// Read-only, version-locked observation bridge for the type-23 contract.
    /// It invokes only QuickMount_GetItemToUse (a selector) and compiled field /
    /// property reads; it never invokes QuickMount, collision or player update.
    /// </summary>
    public sealed class NativeWitchBroomReader
    {
        private readonly Func<object, object> _mount;
        private readonly Func<object, object> _quickMountItem;
        private readonly Func<object, bool> _mountActive;
        private readonly Func<object, int> _mountType;
        private readonly Func<object, int> _mountFrameState;
        private readonly Func<object, int> _itemType;
        private readonly Func<object, int> _itemMountType;
        private readonly Func<object, float> _positionX;
        private readonly Func<object, float> _positionY;
        private readonly Func<object, float> _velocityX;
        private readonly Func<object, float> _velocityY;
        private readonly Func<object, float> _gravity;
        private readonly Func<object, float> _trackBoost;
        private readonly Func<object, float> _gravityDirection;
        private readonly Func<object, bool> _controlMount;
        private readonly Func<object, bool> _releaseMount;
        private readonly Func<object, bool> _releaseUp;
        private readonly Func<object, bool> _releaseUseItem;
        private readonly Func<object, bool> _slowFall;
        private readonly Func<object, bool> _wet;
        private readonly Func<object, bool> _honeyWet;
        private readonly Func<object, bool> _lavaWet;
        private readonly Func<object, bool> _shimmerWet;
        private readonly Func<object, bool> _shimmering;
        private readonly Func<object, bool> _portalPhysicsFlag;
        private readonly Func<object, int> _portalPhysicsTime;
        private readonly Func<object, int> _grappleCount;
        private readonly Func<object, int> _dashDelay;
        private readonly Func<object, int> _eocDash;
        private readonly Func<object, bool> _crowdControlled;
        private readonly Func<object, bool> _tongued;
        private readonly Func<object, bool> _dead;
        private readonly Func<object, bool> _noItems;
        private readonly Func<object, bool> _pulley;
        private readonly Func<object, bool> _sliding;
        private readonly Func<object, bool> _windPushed;
        private readonly Func<object, int> _itemAnimation;
        private readonly Func<object, int> _itemTime;
        private readonly Func<object, bool>[] _forcedMotion;

        public NativeWitchBroomReader(Type playerType, Type mountType, Type itemType)
        {
            _mount = ReflectionAccess.Getter<object>(playerType, "mount");
            _quickMountItem = ReflectionAccess.MethodGetter<object>(playerType,
                "QuickMount_GetItemToUse");
            _mountActive = ReflectionAccess.PropertyGetter<bool>(mountType, "Active");
            _mountType = ReflectionAccess.PropertyGetter<int>(mountType, "Type");
            _mountFrameState = ReflectionAccess.Getter<int>(mountType, "_frameState");
            _itemType = ReflectionAccess.Getter<int>(itemType, "type");
            _itemMountType = ReflectionAccess.Getter<int>(itemType, "mountType");
            _positionX = ReflectionAccess.VectorComponentGetter(playerType, "position", "X");
            _positionY = ReflectionAccess.VectorComponentGetter(playerType, "position", "Y");
            _velocityX = ReflectionAccess.VectorComponentGetter(playerType, "velocity", "X");
            _velocityY = ReflectionAccess.VectorComponentGetter(playerType, "velocity", "Y");
            _gravity = ReflectionAccess.Getter<float>(playerType, "gravity");
            _trackBoost = ReflectionAccess.Getter<float>(playerType, "trackBoost");
            _gravityDirection = ReflectionAccess.Getter<float>(playerType, "gravDir");
            _controlMount = ReflectionAccess.Getter<bool>(playerType, "controlMount");
            _releaseMount = ReflectionAccess.Getter<bool>(playerType, "releaseMount");
            _releaseUp = ReflectionAccess.Getter<bool>(playerType, "releaseUp");
            _releaseUseItem = ReflectionAccess.Getter<bool>(playerType, "releaseUseItem");
            _slowFall = ReflectionAccess.Getter<bool>(playerType, "slowFall");
            _wet = ReflectionAccess.Getter<bool>(playerType, "wet");
            _honeyWet = ReflectionAccess.Getter<bool>(playerType, "honeyWet");
            _lavaWet = ReflectionAccess.Getter<bool>(playerType, "lavaWet");
            _shimmerWet = ReflectionAccess.Getter<bool>(playerType, "shimmerWet");
            _shimmering = ReflectionAccess.Getter<bool>(playerType, "shimmering");
            _portalPhysicsFlag = ReflectionAccess.Getter<bool>(playerType, "portalPhysicsFlag");
            _portalPhysicsTime = ReflectionAccess.Getter<int>(playerType, "_portalPhysicsTime");
            _grappleCount = ReflectionAccess.Getter<int>(playerType, "grapCount");
            _dashDelay = ReflectionAccess.Getter<int>(playerType, "dashDelay");
            _eocDash = ReflectionAccess.Getter<int>(playerType, "eocDash");
            _crowdControlled = ReflectionAccess.PropertyGetter<bool>(playerType, "CCed");
            _tongued = ReflectionAccess.Getter<bool>(playerType, "tongued");
            _dead = ReflectionAccess.Getter<bool>(playerType, "dead");
            _noItems = ReflectionAccess.Getter<bool>(playerType, "noItems");
            _pulley = ReflectionAccess.Getter<bool>(playerType, "pulley");
            _sliding = ReflectionAccess.Getter<bool>(playerType, "sliding");
            _windPushed = ReflectionAccess.Getter<bool>(playerType, "windPushed");
            _itemAnimation = ReflectionAccess.Getter<int>(playerType, "itemAnimation");
            _itemTime = ReflectionAccess.Getter<int>(playerType, "itemTime");
            _forcedMotion = new[]
            {
                ReflectionAccess.Getter<bool>(playerType, "ghost"),
                ReflectionAccess.Getter<bool>(playerType, "frozen"),
                ReflectionAccess.Getter<bool>(playerType, "webbed"),
                ReflectionAccess.Getter<bool>(playerType, "stoned"),
                ReflectionAccess.Getter<bool>(playerType, "onTrack"),
                ReflectionAccess.Getter<bool>(playerType, "vortexDebuff"),
                ReflectionAccess.Getter<bool>(playerType, "sticky"),
                ReflectionAccess.Getter<bool>(playerType, "dazed")
            };
        }

        public WitchBroomToggleSnapshot ReadToggle(object player, bool canFitMount,
            bool canFitDismount, bool hookInFlight)
        {
            var mount = _mount(player);
            var active = mount != null && _mountActive(mount);
            var selected = _quickMountItem(player);
            return new WitchBroomToggleSnapshot
            {
                Known = true,
                ControlMount = _controlMount(player),
                ReleaseMount = _releaseMount(player),
                MountActive = active,
                ActiveMountType = active ? _mountType(mount) : -1,
                QuickMountItemType = selected == null ? 0 : _itemType(selected),
                QuickMountType = selected == null ? -1 : _itemMountType(selected),
                CrowdControlled = _crowdControlled(player),
                Tongued = _tongued(player),
                Dead = _dead(player),
                NoItems = _noItems(player),
                GravityInverted = _gravityDirection(player) != 1f,
                Grappling = _grappleCount(player) > 0 || hookInFlight,
                ItemUseStartEdgeReady = _releaseUseItem(player) &&
                    _itemAnimation(player) == 0 && _itemTime(player) == 0,
                CanFitMount = canFitMount,
                CanFitDismount = canFitDismount
            };
        }

        public WitchBroomMotionSnapshot ReadMotion(object player, bool activationEntry,
            bool openDryPath, bool hookInFlight)
        {
            var mount = _mount(player);
            var active = mount != null && _mountActive(mount);
            var forcedMotion = false;
            for (var i = 0; i < _forcedMotion.Length; i++)
                forcedMotion |= _forcedMotion[i](player);
            var dry = !_wet(player) && !_honeyWet(player) && !_lavaWet(player) &&
                !_shimmerWet(player) && !_shimmering(player);
            return new WitchBroomMotionSnapshot
            {
                Known = true,
                MountActive = activationEntry || active,
                MountType = activationEntry ? WitchBroomMotion.WitchBroomMountType :
                    active ? _mountType(mount) : -1,
                FrameState = activationEntry ? 0 : active ? _mountFrameState(mount) : -1,
                PositionX = _positionX(player),
                PositionY = _positionY(player),
                VelocityX = _velocityX(player),
                VelocityY = _velocityY(player),
                Gravity = _gravity(player),
                TrackBoost = _trackBoost(player),
                ReleaseUp = _releaseUp(player),
                SlowFall = _slowFall(player),
                NormalGravity = _gravityDirection(player) == 1f,
                Dry = dry,
                OpenDryPath = openDryPath,
                PortalPhysicsDisabled = !_portalPhysicsFlag(player) &&
                    _portalPhysicsTime(player) <= 0,
                Grappling = _grappleCount(player) > 0,
                HookInFlight = hookInFlight,
                DashInProgress = _dashDelay(player) == -1 || _eocDash(player) != 0,
                CrowdControlled = _crowdControlled(player),
                Tongued = _tongued(player),
                Dead = _dead(player),
                Pulley = _pulley(player),
                Sliding = _sliding(player),
                WindPushed = _windPushed(player),
                ForcedMotion = forcedMotion
            };
        }
    }
}
