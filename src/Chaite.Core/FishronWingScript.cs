using System;

namespace Chaite.Core
{
    /// <summary>Experimental fixed wing circuit. Native dash edges latch the
    /// escape axis; no threat scoring, candidate search or equipment switching.</summary>
    public sealed class FishronWingScript
    {
        private bool _initialized, _ascending;
        private int _direction, _dashVertical, _previousState = -99, _previousTimer;
        private float _left, _right, _top;

        public void Reset()
        {
            _initialized = false;
            _previousState = -99;
            _previousTimer = 0;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena)
        {
            var output = new FormulaScriptOutput();
            if (input.BossType != 370 || player == null || arena == null ||
                (input.Route != FormulaRoute.FishronFairyWingsDash &&
                 input.Route != FormulaRoute.FishronStrongWingsDash)) return output;
            if (!_initialized)
            {
                _initialized = true;
                _direction = player.Center.X < boss.Center.X ? -1 : 1;
                _ascending = true;
                // Keep the circuit anchored to the entry arena, never to the
                // moving Boss. Locally observed continuous support narrows it.
                _left = Math.Max(player.WorldLeft + 96f, player.Center.X - 960f);
                _right = Math.Min(player.WorldRight - 96f, player.Center.X + 960f);
                var support = arena.FloorSupport;
                if (support.Valid && support.Right - support.Left >= 640f)
                {
                    _left = Math.Max(_left, support.Left + 96f);
                    _right = Math.Min(_right, support.Right - 96f);
                }
                _top = player.Center.Y - 400f;
            }
            var dash = input.NativeState == 1 || input.NativeState == 6 || input.NativeState == 11;
            var dashEdge = dash && (input.NativeState != _previousState || input.NativeTimer < _previousTimer);
            if (player.OnGround && !_ascending)
                _ascending = true;
            if (player.Center.Y <= _top || (!player.OnGround && player.WingTime <= 0f))
                _ascending = false;
            if (!dash)
            {
                if (player.Center.X <= _left) _direction = 1;
                else if (player.Center.X >= _right) _direction = -1;
            }
            if (dashEdge)
            {
                // Preserve existing vertical momentum to leave a committed
                // charge line. Reversing after the Boss crosses us is too late.
                _dashVertical = player.Velocity.Y < -1f ? -1 : player.Velocity.Y > 1f ? 1 :
                    _ascending ? -1 : 1;
            }
            _previousState = input.NativeState;
            _previousTimer = input.NativeTimer;
            output.Accepted = true;
            output.Fire = true;
            output.Horizontal = _direction;
            output.Vertical = dash ? _dashVertical : _ascending ? -1 : 1;
            output.Jump = output.Vertical < 0;
            // A shield collision needs its own timed, aligned counter-dash.
            // Do not press it continuously or with no explicit direction.
            output.Dash = false;
            output.Phase = dash ? "fishron-wing-committed-escape" :
                _ascending ? "fishron-wing-ascent" : "fishron-wing-landing";
            return output;
        }
    }
}
