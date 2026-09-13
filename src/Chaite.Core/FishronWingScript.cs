using System;

namespace Chaite.Core
{
    /// <summary>Experimental fixed wing circuit. Native dash edges latch the
    /// escape axis; no threat scoring, candidate search or equipment switching.</summary>
    public sealed class FishronWingScript
    {
        private bool _initialized, _ascending, _counterDashIssued;
        private int _direction, _dashVertical, _previousState = -99, _previousTimer;
        private float _left, _right, _top;

        public void Reset()
        {
            _initialized = false;
            _counterDashIssued = false;
            _previousState = -99;
            _previousTimer = 0;
        }

        public FormulaScriptOutput Tick(in FormulaScriptInput input,
            PlayerSnapshot player, in TargetSnapshot boss, ArenaSnapshot arena,
            bool shieldReady = false)
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
                _counterDashIssued = false;
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
            if (dash && shieldReady && !_counterDashIssued && !boss.Invulnerable)
            {
                var dx = boss.Center.X - player.Center.X;
                var towardBoss = dx < 0f ? -1 : 1;
                var closingSpeed = 14.5f - boss.Velocity.X * towardBoss;
                var gap = Math.Abs(dx) - (boss.Width + player.Width) * .5f;
                // Native type-2 dash has a 15-tick contact window. Only
                // request the single edge for an incoming, aligned body;
                // never dash toward a receding Boss or a distant diagonal.
                var contactTicks = closingSpeed > 0f ? Math.Max(0f, gap) / closingSpeed : 99f;
                var contactDy = boss.Center.Y - player.Center.Y +
                    (boss.Velocity.Y - player.Velocity.Y) * contactTicks;
                var dy = boss.Center.Y - player.Center.Y;
                var verticalClosing = dy * (boss.Velocity.Y - player.Velocity.Y) < 0f;
                // Diagonal charges can overlap X before Y. The old gap>=-12
                // test rejected the entire remaining contact window as though
                // horizontal passage meant the body could no longer hit us.
                var overlappingDiagonal = gap < 0f && verticalClosing &&
                    Math.Abs(dy) <= (boss.Height + player.Height) * .5f + 20f;
                if (overlappingDiagonal || boss.Velocity.X * towardBoss < -1f && gap >= -12f &&
                    contactTicks <= 4f &&
                    Math.Abs(contactDy) < (boss.Height + player.Height) * .5f - 8f)
                {
                    output.Horizontal = towardBoss;
                    output.Dash = true;
                    _counterDashIssued = true;
                    output.Phase = "fishron-wing-shield-contact";
                }
            }
            if (!output.Dash) output.Phase = dash ? "fishron-wing-committed-escape" :
                _ascending ? "fishron-wing-ascent" : "fishron-wing-landing";
            return output;
        }
    }
}
