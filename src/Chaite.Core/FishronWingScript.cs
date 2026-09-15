using System;

namespace Chaite.Core
{
    /// <summary>Experimental fixed wing circuit. Native dash edges latch the
    /// escape axis; no threat scoring, candidate search or equipment switching.</summary>
    public sealed class FishronWingScript
    {
        private bool _initialized, _ascending, _dashIssued;
        private int _direction, _dashHorizontal, _dashVertical;
        private int _previousState = -99, _previousTimer;
        private float _left, _right, _top;

        public void Reset()
        {
            _initialized = false;
            _dashIssued = false;
            _previousState = -99;
            _previousTimer = 0;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            MobilitySnapshot mobility)
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
            var stateEdge = input.NativeState != _previousState ||
                input.NativeTimer < _previousTimer;
            var dashEdge = dash && stateEdge;
            if (stateEdge) _dashIssued = false;
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
                _dashHorizontal = _direction;
                // Leave the charge line vertically. The side is selected once
                // from the committed charge velocity and held for the entire
                // native state, including after the Boss crosses the player.
                _dashVertical = boss.Velocity.Y > 1f ? -1 :
                    boss.Velocity.Y < -1f ? 1 : _ascending ? -1 : 1;
            }
            _previousState = input.NativeState;
            _previousTimer = input.NativeTimer;
            output.Accepted = true;
            output.Fire = true;
            if (dash)
            {
                // During a charge, the runway direction is not an escape. Move
                // diagonally away from the Boss center on the charge entry and
                // hold that side until the native state ends.
                output.Horizontal = _dashHorizontal;
                output.Vertical = _dashVertical;
            }
            else
            {
                var hazardRun = input.NativeState == 2 ||
                    input.NativeState == 3 || input.NativeState == 7 ||
                    input.NativeState == 8;
                var postDashSeparation = (input.NativeState == 0 ||
                    input.NativeState == 5 || input.NativeState == 10) &&
                    input.NativeTimer <= 10;
                if (hazardRun)
                {
                    output.Horizontal = _direction;
                    output.Vertical = -1;
                }
                else if (postDashSeparation)
                {
                    output.Horizontal = boss.Center.X >= player.Center.X
                        ? -1 : 1;
                    output.Vertical = boss.Center.Y >= player.Center.Y
                        ? -1 : 1;
                }
                else
                {
                    output.Horizontal = _direction;
                    output.Vertical = _ascending ? -1 : 1;
                }
            }
            output.Jump = output.Vertical < 0;
            // The shield is a mobility source, not a required contact parry.
            // Emit at most one early horizontal edge while the Boss is still
            // distant; close-body counter-dashes proved order-dependent in the
            // pinned native update and can take damage before the bounce.
            output.Dash = false;
            var shieldReady = mobility != null && mobility.CanDash &&
                mobility.DashReady;
            var hazardEdge = (input.NativeState == 2 ||
                input.NativeState == 3 || input.NativeState == 7 ||
                input.NativeState == 8) && input.NativeTimer <= 8;
            if ((dash || hazardEdge) && shieldReady && !_dashIssued &&
                !boss.Invulnerable)
            {
                var dx = boss.Center.X - player.Center.X;
                var gap = Math.Abs(dx) - (boss.Width + player.Width) * .5f;
                if (hazardEdge || gap >= 220f && input.NativeTimer <= 8)
                {
                    output.Horizontal = _dashHorizontal;
                    output.Dash = true;
                    _dashIssued = true;
                    output.Phase = hazardEdge
                        ? "fishron-wing-hazard-run-edge"
                        : "fishron-wing-early-lateral-edge";
                }
            }
            if (!output.Dash) output.Phase = dash ? "fishron-wing-committed-escape" :
                _ascending ? "fishron-wing-ascent" : "fishron-wing-landing";
            return output;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss,
            ArenaSnapshot arena, bool shieldReady = false)
        {
            var mobility = new MobilitySnapshot
            {
                CanDash = shieldReady,
                DashReady = shieldReady
            };
            return Tick(in input, player, in boss, arena, mobility);
        }
    }
}
