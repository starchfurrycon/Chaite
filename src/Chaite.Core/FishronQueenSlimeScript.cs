using System;

namespace Chaite.Core
{
    /// <summary>
    /// Fixed Queen Slime mount circuit for Duke Fishron. The reviewed source
    /// describes a W-shaped runway: high-speed mount drops alternate with a
    /// committed jump after each charge. This script reads only the native
    /// AI_069 clocks and the locked mount identity; it does not scan threats.
    /// </summary>
    public sealed class FishronQueenSlimeScript
    {
        public const int QueenSlimeMountType = 50;
        public const int MinimumRunwayPixels = 1400;

        private bool _initialized;
        private int _runDirection;
        private bool _ascending;
        private int _previousState = int.MinValue;
        private int _previousSequence = int.MinValue;
        private float _left;
        private float _right;

        public void Reset()
        {
            _initialized = false;
            _previousState = int.MinValue;
            _previousSequence = int.MinValue;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena, MobilitySnapshot mobility)
        {
            var output = new FormulaScriptOutput();
            if (input.BossType != 370 || player == null || arena == null ||
                mobility == null ||
                input.Route != FormulaRoute.FishronQueenSlime)
                return output;

            if (!mobility.MountActive)
            {
                if (!mobility.SelectedMountIdentityKnown ||
                    mobility.SelectedMountType != QueenSlimeMountType)
                    return Rejected();
                output.Accepted = true;
                output.Fire = false;
                output.ToggleMount = mobility.ActiveMountReleaseReady;
                output.Phase = output.ToggleMount
                    ? "fishron-queen-slime-mount-edge"
                    : "fishron-queen-slime-await-mount-release";
                return output;
            }

            if (!mobility.ActiveMountIdentityKnown ||
                mobility.ActiveMountType != QueenSlimeMountType ||
                mobility.Grappling || mobility.GravityInverted)
                return Rejected();

            if (!_initialized)
            {
                var support = arena.FloorSupport;
                if (!support.Valid ||
                    support.Right - support.Left < MinimumRunwayPixels)
                    return Rejected();
                _initialized = true;
                _left = support.Left + 96f;
                _right = support.Right - 96f;
                _runDirection = player.Center.X < boss.Center.X ? -1 : 1;
                _ascending = true;
            }

            var newAttack = input.NativeState != _previousState ||
                input.NativeSequence != _previousSequence;
            if (newAttack) _ascending = !_ascending;
            _previousState = input.NativeState;
            _previousSequence = input.NativeSequence;

            if (player.Center.X <= _left) _runDirection = 1;
            else if (player.Center.X >= _right) _runDirection = -1;

            output.Accepted = true;
            output.Fire = true;
            var charge = input.NativeState == 1 ||
                input.NativeState == 6 || input.NativeState == 11;
            if (charge)
            {
                output.Horizontal = boss.Center.X < player.Center.X ? -1 : 1;
                output.Vertical = -1;
                output.Jump = true;
                output.Phase = "fishron-queen-slime-charge-jump";
            }
            else
            {
                output.Horizontal = _runDirection;
                output.Vertical = _ascending ? -1 : 1;
                output.Jump = output.Vertical < 0;
                output.Phase = Phase(in input);
            }
            DecideMovement(in input, player, in boss, arena, output);
            return output;
        }

        /// <summary>
        /// Applies the trained residual to the scripted decision computed just
        /// above. The script is the base and the network may only correct it,
        /// so zero weights reproduce the fixed circuit exactly and the search
        /// starts from that circuit's measured behaviour rather than from a
        /// random walk. Route, mount and arena admission are not reachable from
        /// here. A policy exception is deliberately not caught: a
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
            output.Phase = "fishron-queen-slime-learned";
        }

        private static string Phase(in FormulaScriptInput input)
        {
            switch (input.NativeState)
            {
                case 2: return "fishron-queen-slime-clear-p1-bubbles";
                case 3: return "fishron-queen-slime-pass-p1-tornado";
                case 4: return "fishron-queen-slime-stage-p2";
                case 7: return "fishron-queen-slime-clear-p2-bubbles";
                case 8: return "fishron-queen-slime-pass-cthulhunado";
                case 9: return "fishron-queen-slime-stage-p3";
                case 12: return "fishron-queen-slime-track-teleport-side";
                default: return "fishron-queen-slime-runway";
            }
        }

        private static FormulaScriptOutput Rejected() =>
            new FormulaScriptOutput { Accepted = false };
    }
}
