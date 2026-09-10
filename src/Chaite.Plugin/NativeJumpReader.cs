using System;
using System.Linq.Expressions;
using Chaite.Core;

namespace Chaite.Plugin
{
    /// <summary>
    /// Read-only ordinary/cloud profile for the observed pre-update state.
    /// Never read the shared static jumpSpeed/jumpHeight: another player's
    /// Update can own them. No native update/refresh method is invoked here.
    /// Known is a restricted local transition model, not exact whole-frame
    /// prediction across equipment changes, collision, knockback or teleporting.
    /// </summary>
    internal sealed class NativeJumpReader
    {
        private readonly Func<object, int> _jump, _wings, _rocketBoots, _rocketDelay, _grapCount, _cartRampTime;
        private readonly Func<object, float> _boost, _gravity, _fallSpeed, _gravityDirection;
        private readonly Func<object, bool> _release, _cloudAvailable, _cloudEnabled, _autoJump, _stool;
        private readonly Func<object, bool>[] _unsupported;

        public NativeJumpReader(Type playerType)
        {
            _jump = ReflectionAccess.Getter<int>(playerType, "jump");
            _release = ReflectionAccess.Getter<bool>(playerType, "releaseJump");
            _cloudAvailable = ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Cloud");
            _cloudEnabled = ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Cloud");
            _autoJump = ReflectionAccess.Getter<bool>(playerType, "autoJump");
            _boost = ReflectionAccess.Getter<float>(playerType, "jumpSpeedBoost");
            _gravity = ReflectionAccess.Getter<float>(playerType, "gravity");
            _fallSpeed = ReflectionAccess.Getter<float>(playerType, "maxFallSpeed");
            _gravityDirection = ReflectionAccess.Getter<float>(playerType, "gravDir");
            _wings = ReflectionAccess.Getter<int>(playerType, "wingsLogic");
            _rocketBoots = ReflectionAccess.Getter<int>(playerType, "rocketBoots");
            _rocketDelay = ReflectionAccess.Getter<int>(playerType, "rocketDelay");
            _grapCount = ReflectionAccess.Getter<int>(playerType, "grapCount");
            _cartRampTime = ReflectionAccess.Getter<int>(playerType, "cartRampTime");

            // Compile a nested value-type field read without boxing the stool
            // struct every frame. Keep both literal names available to API audit.
            var stoolField = ReflectionAccess.Field(playerType, "portableStoolInfo");
            var inUseField = ReflectionAccess.Field(stoolField.FieldType, "IsInUse");
            if (stoolField.IsStatic || inUseField.IsStatic || inUseField.FieldType != typeof(bool))
                throw new ArgumentException("Expected instance portable-stool IsInUse boolean.", nameof(playerType));
            var source = Expression.Parameter(typeof(object), "player");
            var stool = Expression.Field(Expression.Convert(source, stoolField.DeclaringType), stoolField);
            _stool = Expression.Lambda<Func<object, bool>>(Expression.Field(stool, inUseField), source).Compile();

            // Literal, typed bindings are intentionally auditable. This fixed
            // array is allocated once; Read only iterates compiled field reads.
            _unsupported = new[]
            {
                ReflectionAccess.Getter<bool>(playerType, "dead"),
                ReflectionAccess.Getter<bool>(playerType, "ghost"),
                ReflectionAccess.Getter<bool>(playerType, "wet"),
                ReflectionAccess.Getter<bool>(playerType, "shimmerWet"),
                ReflectionAccess.Getter<bool>(playerType, "shimmering"),
                ReflectionAccess.Getter<bool>(playerType, "jumpBoost"),
                ReflectionAccess.Getter<bool>(playerType, "wereWolf"),
                ReflectionAccess.Getter<bool>(playerType, "moonLordLegs"),
                ReflectionAccess.Getter<bool>(playerType, "empressBrooch"),
                ReflectionAccess.Getter<bool>(playerType, "frogLegJumpBoost"),
                ReflectionAccess.Getter<bool>(playerType, "sticky"),
                ReflectionAccess.Getter<bool>(playerType, "dazed"),
                ReflectionAccess.Getter<bool>(playerType, "frozen"),
                ReflectionAccess.Getter<bool>(playerType, "webbed"),
                ReflectionAccess.Getter<bool>(playerType, "stoned"),
                ReflectionAccess.Getter<bool>(playerType, "carpet"),
                ReflectionAccess.Getter<bool>(playerType, "sliding"),
                ReflectionAccess.Getter<bool>(playerType, "pulley"),
                ReflectionAccess.Getter<bool>(playerType, "slowFall"),
                ReflectionAccess.Getter<bool>(playerType, "vortexDebuff"),
                ReflectionAccess.Getter<bool>(playerType, "tongued"),
                ReflectionAccess.Getter<bool>(playerType, "onTrack"),
                ReflectionAccess.Getter<bool>(playerType, "hasDeadCellsDownDash"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_DownDash"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Sandstorm"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Sandstorm"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Sandstorm"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Blizzard"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Blizzard"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Blizzard"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Fart"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Fart"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Fart"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Sail"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Sail"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Sail"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Unicorn"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Unicorn"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Unicorn"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Santank"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Santank"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Santank"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_WallOfFleshGoat"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_WallOfFleshGoat"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_WallOfFleshGoat"),
                ReflectionAccess.Getter<bool>(playerType, "hasJumpOption_Basilisk"),
                ReflectionAccess.Getter<bool>(playerType, "canJumpAgain_Basilisk"),
                ReflectionAccess.Getter<bool>(playerType, "isPerformingJump_Basilisk")
            };
        }

        public JumpSnapshot Read(object player, bool mountActive)
        {
            if (player == null) return default(JumpSnapshot);
            var state = new JumpSnapshot
            {
                RemainingTicks = _jump(player), ReleaseReady = _release(player),
                CloudAvailable = _cloudAvailable(player), CloudEnabled = _cloudEnabled(player),
                AutoJump = _autoJump(player)
            };
            var gravity = _gravity(player);
            var fall = _fallSpeed(player);
            var direction = _gravityDirection(player);
            if (mountActive || state.RemainingTicks < 0 || state.RemainingTicks > 15 ||
                state.CloudAvailable && !state.CloudEnabled || _boost(player) != 0f ||
                !(gravity >= 0f) || float.IsInfinity(gravity) || !(fall > 0f) || float.IsInfinity(fall) ||
                direction != 1f && direction != -1f || _wings(player) != 0 || _rocketBoots(player) != 0 ||
                _rocketDelay(player) != 0 || _grapCount(player) != 0 || _cartRampTime(player) != 0 || _stool(player))
                return state;
            for (var index = 0; index < _unsupported.Length; index++)
                if (_unsupported[index](player)) return state;
            state.Known = true;
            state.Speed = 5.01f;
            state.Height = 15;
            return state;
        }
    }
}
