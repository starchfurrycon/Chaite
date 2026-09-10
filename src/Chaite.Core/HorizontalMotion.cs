using System;

namespace Chaite.Core
{
    /// <summary>
    /// Allocation-free ordinary 1.4.5.8 horizontal movement approximation. Native
    /// base/sprint thresholds and acceleration are separate; tile, cart, portal,
    /// special mount, dash and grappling physics are not replayed here.
    /// </summary>
    public static class HorizontalMotion
    {
        public static float Advance(PlayerSnapshot player, float velocity, int direction,
            bool grounded, int ticks, out float displacement, float mountedSpeed = 0f)
        {
            displacement = 0f;
            if (ticks <= 0) return velocity;
            if (player.BaseRunSpeed <= 0f)
            {
                // Keep old adapter snapshots bit-compatible, including their
                // endpoint-step integration; production supplies the native profile.
                var target = direction * Math.Max(2f, Math.Max(player.MaxRunSpeed, mountedSpeed));
                velocity = MoveTowards(velocity, target, Math.Max(.08f, player.RunAcceleration) * ticks);
                displacement = velocity * ticks;
                return velocity;
            }
            var baseSpeed = player.BaseRunSpeed;
            // Native fields already include the currently active mount and its
            // modifiers. Raw mount stats must not undo a live slowing effect.
            var topSpeed = Math.Max(baseSpeed, player.MaxRunSpeed);
            var acceleration = Math.Max(0f, player.RunAcceleration);
            var sprintAcceleration = Math.Max(0f, player.SprintAcceleration);
            var slowdown = Math.Max(0f, player.RunSlowdown);
            var drag = grounded ? slowdown : slowdown * .5f;
            for (var tick = 0; tick < ticks; tick++)
            {
                if (direction != 0)
                {
                    var forward = velocity * direction;
                    if (forward < baseSpeed)
                    {
                        // Native reversal brakes first, then accelerates. Do not
                        // replace a measured debuffed acceleration with an .08 floor.
                        if (forward < -slowdown) forward += slowdown;
                        forward += acceleration;
                        velocity = forward * direction;
                    }
                    else if (forward < topSpeed && sprintAcceleration > 0f)
                    {
                        // Boots use .2*runAcceleration (wings add another .2).
                        // Without wings/flying mount, airborne input maintains the
                        // already-earned speed but cannot build sprint speed.
                        if (grounded || player.CanSprintInAir)
                            velocity = (forward + sprintAcceleration) * direction;
                    }
                    else velocity = MoveTowards(velocity, 0f, drag);
                }
                else velocity = MoveTowards(velocity, 0f, drag);
                // Integrate each bounded sub-tick. Applying the final velocity to
                // the entire 3-tick sample overstates travel while accelerating.
                displacement += velocity;
            }
            return velocity;
        }

        private static float MoveTowards(float current, float target, float delta)
        {
            if (Math.Abs(target - current) <= delta) return target;
            return current + Math.Sign(target - current) * delta;
        }
    }
}
