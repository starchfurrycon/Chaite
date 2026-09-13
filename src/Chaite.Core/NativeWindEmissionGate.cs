namespace Chaite.Core
{
    /// <summary>
    /// Final, allocation-free guard for a reviewed projectile-output action.
    /// The locked vanilla executable initializes Main.windPhysics to false.
    /// A caller must nevertheless sample that native state in the same frame
    /// in which it emits controlUseItem: a changed/inaccessible value is not a
    /// license to reuse a no-wind trajectory certificate.
    /// </summary>
    public static class NativeWindEmissionGate
    {
        /// <summary>
        /// Returns true only for one of the explicitly declared output route
        /// kinds while the native wind flag was read successfully and is off.
        /// This deliberately covers every reviewed projectile route rather
        /// than maintaining a fragile partial list of aiStyle values. Routes
        /// that do not use a projectile never call this gate.
        /// </summary>
        public static bool PermitsSpecifiedOutput(OutputRouteKind routeKind,
            bool windPhysicsKnown, bool windPhysicsEnabled)
        {
            return IsSpecifiedOutputRoute(routeKind) && windPhysicsKnown &&
                !windPhysicsEnabled;
        }

        public static bool IsSpecifiedOutputRoute(OutputRouteKind routeKind)
        {
            switch (routeKind)
            {
                case OutputRouteKind.StraightRanged:
                case OutputRouteKind.StraightMagic:
                case OutputRouteKind.MinionAndWhip:
                case OutputRouteKind.MeleeProjectile:
                case OutputRouteKind.DirectedMeleeWave:
                case OutputRouteKind.ConvergingRangedBurst:
                case OutputRouteKind.HomingMagicProjectile:
                    return true;
                default:
                    return false;
            }
        }
    }
}
