using System;
using Chaite.Core;

namespace Chaite.Plugin
{
    /// <summary>
    /// Explicit observed-state reader for Demon Wings (logic 1), optionally
    /// rocket-boot mechanism 2, dry normal gravity and base ordinary/cloud jump,
    /// including the observed feather-fall flag and its reviewed gravity branch.
    /// It never invokes native update/flight methods or reads shared jump statics.
    /// Out jump is the SAME authoritative counter/input state used by the caller,
    /// not an additional flight-owned copy to advance separately.
    /// </summary>
    internal sealed class NativeFlightReader
    {
        private readonly NativeJumpReader _jumps;
        private readonly Func<object, int> _wings, _boots, _wingMax, _rocket, _rocketMax, _delay;
        private readonly Func<object, float> _wing;
        private readonly Func<object, bool> _canRocket, _rocketRelease, _justJumped, _merman, _hoverDown, _hoverUp;

        public NativeFlightReader(Type playerType) : this(playerType, new NativeJumpReader(playerType)) { }

        internal NativeFlightReader(Type playerType, NativeJumpReader jumps)
        {
            _jumps = jumps ?? throw new ArgumentNullException(nameof(jumps));
            _wings = ReflectionAccess.Getter<int>(playerType, "wingsLogic");
            _boots = ReflectionAccess.Getter<int>(playerType, "rocketBoots");
            _wing = ReflectionAccess.Getter<float>(playerType, "wingTime");
            _wingMax = ReflectionAccess.Getter<int>(playerType, "wingTimeMax");
            _rocket = ReflectionAccess.Getter<int>(playerType, "rocketTime");
            _rocketMax = ReflectionAccess.Getter<int>(playerType, "rocketTimeMax");
            _delay = ReflectionAccess.Getter<int>(playerType, "rocketDelay");
            _canRocket = ReflectionAccess.Getter<bool>(playerType, "canRocket");
            _rocketRelease = ReflectionAccess.Getter<bool>(playerType, "rocketRelease");
            _justJumped = ReflectionAccess.Getter<bool>(playerType, "justJumped");
            _merman = ReflectionAccess.Getter<bool>(playerType, "merman");
            _hoverDown = ReflectionAccess.Getter<bool>(playerType, "tryKeepingHoveringDown");
            _hoverUp = ReflectionAccess.Getter<bool>(playerType, "tryKeepingHoveringUp");
        }

        public FlightSnapshot Read(object player, bool mountActive, out JumpSnapshot jump)
        {
            jump = default(JumpSnapshot);
            if (player == null) return default(FlightSnapshot);
            var state = new FlightSnapshot
            {
                WingsLogic = _wings(player), RocketBoots = _boots(player),
                WingTime = _wing(player), WingTimeMax = _wingMax(player),
                RocketTime = _rocket(player), RocketTimeMax = _rocketMax(player), RocketDelay = _delay(player),
                CanRocket = _canRocket(player), RocketRelease = _rocketRelease(player), JustJumped = _justJumped(player)
            };
            // Exact baseline capacities and whole-tick resources only. Fractional
            // time left by another wing profile/gear transition is not silently
            // treated as this reviewed fixture. Converted wing time can exceed 100.
            if (state.WingsLogic != 1 || (state.RocketBoots != 0 && state.RocketBoots != 2) ||
                state.RocketDelay != 0 || state.WingTimeMax != 100 || state.RocketTimeMax != 7 ||
                state.RocketTime < 0 || state.RocketTime > 7 ||
                !(state.WingTime >= 0f && state.WingTime <= 142f) || state.WingTime != (int)state.WingTime ||
                _merman(player) || _hoverDown(player) || _hoverUp(player))
            {
                // Existing ordinary/cloud support is preserved without a second
                // shared restriction scan. With any wing/rocket equipment this
                // ordinary reader fails its early equipment gate, as before.
                jump = _jumps.Read(player, mountActive);
                return state;
            }
            jump = _jumps.ReadForDemon(player, mountActive);
            state.Known = jump.Known;
            return state;
        }
    }
}
