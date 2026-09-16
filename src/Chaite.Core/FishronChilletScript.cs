using System;

namespace Chaite.Core
{
    /// <summary>
    /// Fixed single-runway script reviewed from the public Chillet strategy.
    /// It reads only AI_069's state/clock/sequence, the locked runway and the
    /// exact native mount/dash state. It never scans threats or scores routes.
    /// </summary>
    public sealed class FishronChilletScript
    {
        public const int MinimumRunwayPixels = 1600;
        private bool _initialized;
        private int _runDirection;
        private int _previousState = int.MinValue;
        private int _previousSequence = int.MinValue;
        private bool _dashIssued;
        private float _left;
        private float _right;

        public void Reset()
        {
            _initialized = false;
            _previousState = int.MinValue;
            _previousSequence = int.MinValue;
            _dashIssued = false;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena, MobilitySnapshot mobility)
        {
            var output = new FormulaScriptOutput();
            if (input.BossType != 370 || player == null || arena == null ||
                mobility == null ||
                input.Route != FormulaRoute.FishronTrustyChillet &&
                input.Route != FormulaRoute.FishronTrustyChilletIgnis)
                return output;

            var expectedMount = input.Route ==
                FormulaRoute.FishronTrustyChillet ? 64 : 65;
            if (!_initialized)
            {
                var support = arena.FloorSupport;
                if (!support.Valid ||
                    support.Right - support.Left < MinimumRunwayPixels)
                    return output;
                _initialized = true;
                _left = support.Left + 96f;
                _right = support.Right - 96f;
                _runDirection = player.Center.X < boss.Center.X ? -1 : 1;
            }

            output.Accepted = true;
            output.Fire = true;
            if (!mobility.MountActive)
            {
                if (!mobility.SelectedMountIdentityKnown ||
                    mobility.SelectedMountType != expectedMount)
                    return Rejected();
                output.Horizontal = 0;
                output.Fire = false;
                output.ToggleMount = mobility.ActiveMountReleaseReady;
                output.Phase = output.ToggleMount ?
                    "fishron-chillet-mount-edge" :
                    "fishron-chillet-await-mount-release";
                return output;
            }
            if (!mobility.ActiveMountIdentityKnown ||
                mobility.ActiveMountType != expectedMount ||
                mobility.Grappling || mobility.GravityInverted)
                return Rejected();

            var newAttack = input.NativeState != _previousState ||
                input.NativeSequence != _previousSequence;
            if (newAttack) _dashIssued = false;
            _previousState = input.NativeState;
            _previousSequence = input.NativeSequence;

            if (player.Center.X <= _left) _runDirection = 1;
            else if (player.Center.X >= _right) _runDirection = -1;

            var counter = ShouldCounterDash(in input);
            if (counter && !_dashIssued)
            {
                _runDirection = boss.Center.X < player.Center.X ? -1 : 1;
                if (mobility.DashType == 6 && mobility.DashReady &&
                    mobility.EyeShieldDash.ReleaseDash)
                {
                    output.Dash = true;
                    _dashIssued = true;
                }
            }
            // A trained policy for this route sits after the mount release and mount
            // identity admission above, because that path has its own early
            // return and taking it over would stop the mount from ever being
            // summoned.
            output.Horizontal = _runDirection;
            output.Phase = Phase(in input, counter, output.Dash);
            DecideMovement(in input, player, in boss, arena, output);
            return output;
        }

        /// <summary>
        /// Applies the trained residual to the scripted decision computed just
        /// above. The script is the base and the network may only correct it,
        /// so zero weights reproduce the fixed circuit exactly and the search
        /// starts from that circuit's measured behaviour rather than from a
        /// random walk. The mount summon path above returns before reaching
        /// this point. A policy exception is deliberately not caught: a
        /// configured-but-broken policy must fail loudly rather than silently
        /// revert to the fixed machine.
        /// </summary>
        private static void DecideMovement(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            FormulaScriptOutput output)
        {
            var learned = LearnedPolicy.ForRoute(input.Route);
            if (learned == null) return;
            int horizontal, vertical;
            bool jump, dash;
            if (!learned.Adjust(in input, player, in boss, arena,
                    output.Horizontal, output.Vertical, output.Jump,
                    output.Dash, out horizontal, out vertical, out jump,
                    out dash))
                return;
            output.Horizontal = horizontal;
            output.Vertical = vertical;
            output.Jump = jump;
            output.Dash = dash;
            output.Phase = "fishron-chillet-learned";
        }

        private static bool ShouldCounterDash(in FormulaScriptInput input)
        {
            if (input.NativeState == 1)
                return input.NativeSequence / 2 + 1 >= 2;
            if (input.NativeState == 6)
                return input.NativeSequence / 2 + 1 <= 2;
            return input.NativeState == 11;
        }

        private static string Phase(in FormulaScriptInput input,
            bool counter, bool edge)
        {
            if (edge) return "fishron-chillet-counter-edge";
            if (counter) return "fishron-chillet-counter-committed";
            switch (input.NativeState)
            {
                case 2: return "fishron-chillet-clear-p1-bubbles";
                case 3: return "fishron-chillet-pass-p1-tornado";
                case 4: return "fishron-chillet-stage-p2-runway";
                case 7: return "fishron-chillet-clear-p2-bubbles";
                case 8: return "fishron-chillet-pass-cthulhunado";
                case 9: return "fishron-chillet-stage-p3-runway";
                case 12: return "fishron-chillet-track-teleport-side";
                default: return "fishron-chillet-runway";
            }
        }

        private static FormulaScriptOutput Rejected() =>
            new FormulaScriptOutput { Accepted = false };
    }
}
