using System;

namespace Chaite.Core
{
    /// <summary>
    /// A measured, indivisible movement implementation. Values in this record
    /// all come from the same live controller route; callers must never merge
    /// fields from the player, an inactive mount, a grapple, or another route.
    /// </summary>
    public struct BossMobilityCapabilityEnvelope
    {
        public bool Known;
        public bool ProductionClosureCertified;
        public BossLocomotionBaseline Locomotion;
        public BossDashBaseline Dash;
        public BossVerticalMobilityThreshold Vertical;
        public BossBurstMobilityThreshold Burst;
        public float HorizontalTopSpeed;
        public float HorizontalAcceleration;
        public float HorizontalBraking;
        public float ControlledAscentSpeed;
        public int ControlledAirTicks;
        public float BurstStartSpeed;
    }

    /// <summary>
    /// Converts one exact controller implementation into source-independent
    /// kinematics, then compares those kinematics with a Boss capability
    /// threshold. Equipment identity is evidence for the conversion only; it
    /// is never itself the requirement.
    /// </summary>
    public static class BossMobilityCapabilityEvaluator
    {
        /// <summary>
        /// At the Player.Update entry hook, native movement fields describe the
        /// completed prior frame. On the final Slow tick, UpdateBuffs has already
        /// decremented Buff 32 to zero but its public slow aggregate and exact
        /// 0.5 multiplier still describe that completed frame. In 1.4.5.8 only
        /// Buff 32 sets Player.slow, so this bridges that single expiry frame
        /// without treating another impairment as modeled Slow.
        /// </summary>
        public static bool IsModeledSlowEffectPresent(bool exactBuffStateKnown,
            bool exactBuffActive, bool nativeSlowAggregate)
        {
            return exactBuffStateKnown &&
                (exactBuffActive || nativeSlowAggregate);
        }

        public static bool TryMeasure(CombatSnapshot snapshot,
            in BossMobilityBaseline implementation, bool requireReady,
            out BossMobilityCapabilityEnvelope capability, out string reason)
        {
            capability = default(BossMobilityCapabilityEnvelope);
            if (snapshot == null || snapshot.Player == null ||
                snapshot.Mobility == null)
            {
                reason = "无法从不完整快照测量一条完整机动路线";
                return false;
            }

            capability.Known = true;
            capability.ProductionClosureCertified = true;
            capability.Locomotion = implementation.Locomotion;
            capability.Dash = implementation.Dash;
            capability.Vertical = BossVerticalMobilityThreshold.GroundRoute;
            capability.Burst = BossBurstMobilityThreshold.None;

            switch (implementation.Locomotion)
            {
            case BossLocomotionBaseline.OnFoot:
                // A reviewed flight profile may deliberately remain dormant on
                // an on-foot route. Unknown wings/boots are different: holding
                // Jump can enter unmodelled native flight, so their horizontal
                // stats alone must never authorize an on-foot controller.
                var onFootFlight = snapshot.Player.Flight;
                if (!onFootFlight.Known &&
                    (onFootFlight.WingsLogic != 0 ||
                     onFootFlight.RocketBoots != 0))
                {
                    reason = "unmodelled equipped wings or rocket boots can reinterpret the on-foot Jump input";
                    return false;
                }
                if (!TryMeasurePlayerHorizontal(snapshot.Player,
                        out capability.HorizontalTopSpeed,
                        out capability.HorizontalAcceleration,
                        out capability.HorizontalBraking, out reason))
                    return false;
                if (snapshot.Mobility.MountActive)
                {
                    reason = "当前徒步控制路线要求玩家未乘坐坐骑";
                    return false;
                }
                break;

            case BossLocomotionBaseline.FinitePlayerFlight:
                if (!TryMeasurePlayerHorizontal(snapshot.Player,
                        out capability.HorizontalTopSpeed,
                        out capability.HorizontalAcceleration,
                        out capability.HorizontalBraking, out reason))
                    return false;
                if (snapshot.Mobility.MountActive)
                {
                    reason = "当前有限飞行控制路线使用玩家飞行而非坐骑";
                    return false;
                }
                var nativeFlight = snapshot.Player.Flight;
                var nativeFlightFraction = FlightMotion.ResourceFraction(
                    in nativeFlight);
                if (!nativeFlight.Known || !snapshot.Player.Jump.Known ||
                    !snapshot.Mobility.HasFiniteFlightResource ||
                    !Finite(snapshot.Mobility.FlightResourceFraction) ||
                    snapshot.Mobility.FlightResourceFraction < 0f ||
                    !Finite(nativeFlightFraction) || nativeFlightFraction < 0f ||
                    Math.Abs(snapshot.Mobility.FlightResourceFraction -
                        nativeFlightFraction) > .001f ||
                    snapshot.Player.WingTime != nativeFlight.WingTime ||
                    snapshot.Player.RocketTime != nativeFlight.RocketTime ||
                    !Finite(snapshot.Player.Jump.Speed) ||
                    snapshot.Player.Jump.Speed <= 0f)
                {
                    reason = "玩家当前精确有限飞行状态不能形成受支持的完整空中路线";
                    return false;
                }
                if (requireReady && nativeFlightFraction <= 0f)
                {
                    reason = "有限飞行资源已经耗尽，无法安全启动";
                    return false;
                }
                capability.Vertical =
                    BossVerticalMobilityThreshold.ControlledAirRoute;
                capability.ControlledAscentSpeed =
                    snapshot.Player.Jump.Speed * 1.5f;
                capability.ControlledAirTicks =
                    FlightMotion.CapacityTicks(in nativeFlight);
                break;

            case BossLocomotionBaseline.ActiveWitchBroom:
                if (!snapshot.Mobility.MountActive ||
                    !WitchBroomMotion.MatchesActiveBaseline(
                        in snapshot.Mobility.WitchBroomMotion))
                {
                    reason = "当前女巫扫帚路线要求已激活且原生状态受支持的具体坐骑";
                    return false;
                }
                capability.Vertical =
                    BossVerticalMobilityThreshold.ControlledAirRoute;
                capability.HorizontalTopSpeed = WitchBroomMotion.RunSpeed;
                capability.HorizontalAcceleration =
                    WitchBroomMotion.Acceleration;
                capability.HorizontalBraking = WitchBroomMotion.RunSlowdown;
                capability.ControlledAscentSpeed =
                    Math.Abs(WitchBroomMotion.UpTarget);
                capability.ControlledAirTicks = int.MaxValue;
                // Exact dry motion is not yet a Boss-owned, live collision /
                // threat / return controller. Keep its measured attributes
                // visible without allowing them to authorize production input.
                capability.ProductionClosureCertified = false;
                break;

            default:
                reason = "未知的 Boss 机动实现路线";
                return false;
            }

            switch (implementation.Dash)
            {
            case BossDashBaseline.None:
                break;
            case BossDashBaseline.ShieldOfCthulhu:
                if (implementation.Locomotion ==
                    BossLocomotionBaseline.ActiveWitchBroom)
                {
                    reason = "坐骑与玩家冲刺不能拼接为同一条机动实现路线";
                    return false;
                }
                var shieldSupported = requireReady
                    ? EyeShieldDashMotion.IsReady(
                        in snapshot.Mobility.EyeShieldDash)
                    : EyeShieldDashMotion.IsSupportedState(
                        in snapshot.Mobility.EyeShieldDash);
                if (!shieldSupported)
                {
                    reason = "当前认证冲刺路线要求可用的克苏鲁之盾原生状态";
                    return false;
                }
                capability.Burst =
                    BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn;
                capability.BurstStartSpeed =
                    EyeShieldDashMotion.CertifiedStartSpeed;
                break;
            default:
                reason = "未知的 Boss 冲刺实现路线";
                return false;
            }

            reason = null;
            return true;
        }

        public static bool Meets(in BossMobilityCapabilityEnvelope capability,
            in BossMobilityCapabilityThreshold threshold,
            float minimumHorizontalSpeed, out string reason)
        {
            if (!capability.Known)
            {
                reason = "机动实现没有可核验的运动能力数据";
                return false;
            }
            if (!capability.ProductionClosureCertified)
            {
                reason = "该机动实现尚无 Boss 专用生产控制闭环";
                return false;
            }
            if (capability.Locomotion == BossLocomotionBaseline.Unspecified ||
                capability.Dash == BossDashBaseline.Unspecified ||
                capability.Vertical != BossVerticalMobilityThreshold.GroundRoute &&
                capability.Vertical != BossVerticalMobilityThreshold.ControlledAirRoute ||
                capability.Burst != BossBurstMobilityThreshold.None &&
                capability.Burst !=
                    BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn ||
                !FiniteNonNegative(capability.HorizontalTopSpeed) ||
                !FiniteNonNegative(capability.HorizontalAcceleration) ||
                !FiniteNonNegative(capability.HorizontalBraking) ||
                !FiniteNonNegative(capability.ControlledAscentSpeed) ||
                capability.ControlledAirTicks < 0 ||
                !FiniteNonNegative(capability.BurstStartSpeed))
            {
                reason = "已测量的 Boss 机动能力包含未声明或无效的数值";
                return false;
            }
            if (threshold.Vertical != BossVerticalMobilityThreshold.GroundRoute &&
                threshold.Vertical !=
                    BossVerticalMobilityThreshold.ControlledAirRoute)
            {
                reason = "未知的 Boss 垂直机动能力门槛";
                return false;
            }
            if (threshold.Burst != BossBurstMobilityThreshold.None &&
                threshold.Burst !=
                    BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn)
            {
                reason = "未知的 Boss 爆发机动能力门槛";
                return false;
            }
            if (!FiniteNonNegative(minimumHorizontalSpeed) ||
                !FiniteNonNegative(threshold.MinimumHorizontalAcceleration) ||
                !FiniteNonNegative(threshold.MinimumHorizontalBraking) ||
                !FiniteNonNegative(threshold.MinimumControlledAscentSpeed) ||
                threshold.MinimumControlledAirTicks < 0 ||
                !FiniteNonNegative(threshold.MinimumBurstStartSpeed))
            {
                reason = "Boss 机动能力门槛包含无效数值";
                return false;
            }
            if (capability.HorizontalTopSpeed < minimumHorizontalSpeed)
            {
                reason = "当前完整路线的有效水平速度低于该 Boss 的能力门槛";
                return false;
            }
            if (capability.HorizontalAcceleration <
                threshold.MinimumHorizontalAcceleration)
            {
                reason = "当前完整路线的水平加速度低于该 Boss 的能力门槛";
                return false;
            }
            if (capability.HorizontalBraking <
                threshold.MinimumHorizontalBraking)
            {
                reason = "当前完整路线的制动能力低于该 Boss 的能力门槛";
                return false;
            }
            if (threshold.Vertical ==
                    BossVerticalMobilityThreshold.ControlledAirRoute &&
                capability.Vertical !=
                    BossVerticalMobilityThreshold.ControlledAirRoute)
            {
                reason = "当前完整路线不能满足该 Boss 的持续可控空中机动门槛";
                return false;
            }
            if (capability.ControlledAscentSpeed <
                threshold.MinimumControlledAscentSpeed)
            {
                reason = "当前完整路线的可控上升速度低于该 Boss 的能力门槛";
                return false;
            }
            if (capability.ControlledAirTicks <
                threshold.MinimumControlledAirTicks)
            {
                reason = "当前完整路线的连续空中控制时间低于该 Boss 的能力门槛";
                return false;
            }
            if (threshold.Burst ==
                    BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn &&
                capability.Burst !=
                    BossBurstMobilityThreshold.CertifiedDashWithBrakedReturn)
            {
                reason = "当前完整路线不能满足该 Boss 的冲刺、制动与返回闭环门槛";
                return false;
            }
            if (capability.BurstStartSpeed <
                threshold.MinimumBurstStartSpeed)
            {
                reason = "当前完整路线的认证爆发速度低于该 Boss 的能力门槛";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Rechecks one exact player-owned route against its pre-impairment
        /// capability when the owning Boss controller explicitly models
        /// vanilla Slow (Buff 32). Player.UpdateBuffs applies the exact 0.5
        /// strongestMoveSpeedDebuff factor to maxRunSpeed, accRunSpeed and
        /// runAcceleration. Braking and vertical motion are not reconstructed.
        /// Any unknown/different factor, stronger impairment, mount route or
        /// burst route stays on the ordinary fail-closed path.
        /// </summary>
        public static bool MeetsWithModeledSlow(
            in BossMobilityCapabilityEnvelope liveCapability,
            PlayerSnapshot player,
            in BossMobilityCapabilityThreshold threshold,
            float minimumHorizontalSpeed, out string reason)
        {
            if (Meets(in liveCapability, in threshold,
                    minimumHorizontalSpeed, out reason))
                return true;
            if (player == null || !player.SlowDebuffKnown ||
                !player.SlowDebuffActive ||
                !player.MoveSpeedDebuffFactorKnown ||
                Math.Abs(player.MoveSpeedDebuffFactor - .5f) > .000001f ||
                liveCapability.Locomotion !=
                    BossLocomotionBaseline.OnFoot ||
                liveCapability.Dash != BossDashBaseline.None)
                return false;

            var normalized = liveCapability;
            normalized.HorizontalTopSpeed /= player.MoveSpeedDebuffFactor;
            normalized.HorizontalAcceleration /=
                player.MoveSpeedDebuffFactor;
            return Meets(in normalized, in threshold,
                minimumHorizontalSpeed, out reason);
        }

        /// <summary>
        /// Lower values are preferred. Player-owned movement wins over a mount
        /// whenever both independently meet the same threshold. An optional
        /// burst implementation is also kept behind a route which does not
        /// consume that capability, so spare equipment cannot rewrite a stable
        /// lower-bound loop.
        /// </summary>
        public static int Preference(in BossMobilityBaseline implementation,
            in BossMobilityCapabilityThreshold threshold)
        {
            var score = implementation.Locomotion ==
                BossLocomotionBaseline.OnFoot ? 0 :
                implementation.Locomotion ==
                    BossLocomotionBaseline.FinitePlayerFlight ?
                        (threshold.Vertical ==
                            BossVerticalMobilityThreshold.ControlledAirRoute
                            ? 0 : 10) : 20;
            if (threshold.Burst == BossBurstMobilityThreshold.None &&
                implementation.Dash != BossDashBaseline.None)
                score += 100;
            return score;
        }

        private static bool TryMeasurePlayerHorizontal(PlayerSnapshot player,
            out float topSpeed, out float acceleration, out float braking,
            out string reason)
        {
            topSpeed = acceleration = braking = 0f;
            if (player == null || !FiniteNonNegative(player.MaxRunSpeed) ||
                !FiniteNonNegative(player.BaseRunSpeed) ||
                !FiniteNonNegative(player.RunAcceleration) ||
                !FiniteNonNegative(player.SprintAcceleration) ||
                !FiniteNonNegative(player.RunSlowdown))
            {
                reason = "玩家水平运动参数不是有效有限数值";
                return false;
            }
            topSpeed = player.MaxRunSpeed;
            if (player.BaseRunSpeed <= 0f)
            {
                // Legacy/synthetic snapshots use HorizontalMotion's explicit
                // .08 fallback and a single acceleration for both directions.
                acceleration = Math.Max(.08f, player.RunAcceleration);
                braking = acceleration;
            }
            else
            {
                var reachesSprint = player.MaxRunSpeed >
                    player.BaseRunSpeed + .0001f;
                acceleration = reachesSprint
                    ? Math.Min(player.RunAcceleration,
                        player.SprintAcceleration)
                    : player.RunAcceleration;
                braking = player.RunSlowdown;
            }
            reason = null;
            return true;
        }

        private static bool FiniteNonNegative(float value) =>
            Finite(value) && value >= 0f;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
