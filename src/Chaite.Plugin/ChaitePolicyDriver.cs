using Chaite.Core;
using System;
using System.Globalization;

namespace Chaite.Plugin
{
    /// <summary>
    /// Drives the fight from an exported policy, in process.
    ///
    /// The plugin already has two ways to be told what to press. The formula
    /// scripts decide from reviewed native state, and the bridge channel
    /// (<c>CHAITE_BRIDGE_FILE</c>) lets a trainer outside the game decide, one
    /// action file per tick. Neither can run a trained policy by itself: the
    /// scripts are not the trained policy, and the bridge needs a Python
    /// process on the other end of the file. This is the third way, and the one
    /// the export exists for -- the game loads the weights and acts on them
    /// locally, with no trainer attached.
    ///
    /// It is inert unless <c>CHAITE_POLICY_FORMAT=exported</c> names it, so an
    /// environment that has never heard of it behaves exactly as before.
    ///
    /// <para><b>Why the observation is held for one tick.</b> The trainer's
    /// rollout is a strict ping-pong: the probe writes the observation for tick
    /// T after that tick's native update, the trainer answers with an action,
    /// and the engine applies it during tick T+1. In process there is no file
    /// to wait on, so the natural shortcut is to build the observation and act
    /// on it in the same frame. That shortcut reads the wrong state. The
    /// plugin's tick entry runs at the top of <c>Player.Update</c>, before the
    /// tick's own simulation, so the state visible there is the end of tick T-1
    /// -- one tick behind the row the trainer would have paired with this
    /// action. Building the row at tick entry and applying its action one tick
    /// later reproduces the trainer's pairing exactly, at the cost of the same
    /// one tick of latency the file channel has. The alternative is a policy
    /// that was trained on state it never sees, which is worse than a tick of
    /// delay.</para>
    ///
    /// <para><b>What it owns.</b> Movement only, exactly like the bridge
    /// channel: the twelve-action alphabet is direction, jump and dash, so the
    /// plan's horizontal, jump and dash come from the policy while the weapon
    /// route, the aim and the consumables stay the planner's. The probe drives
    /// the Boss's health from its own simulated output, so nothing here may
    /// emit a shot.</para>
    /// </summary>
    internal sealed class ChaitePolicyDriver
    {
        /// <summary>The mode that selects this driver. Shared with
        /// <see cref="ExportedPolicy"/>, which owns the variable.</summary>
        public const string FormatVariable = ExportedPolicy.FormatVariable;

        public const string ExportedFormatName = ExportedPolicy.ExportedFormatName;

        private readonly TerrariaFacade _game;
        private readonly ExportedPolicy _policy;
        private readonly ChaiteObservation _observation;
        private readonly float[] _values;

        // The observation built at the previous tick entry, and the absolute
        // game tick it was built for. -1 means nothing is pending.
        private float[] _pending;
        private long _pendingTick = -1L;

        // Life observed at the previous tick entry, and the hits counted so
        // far. See CountHit for why this is counted rather than read.
        private float _previousLife;
        private float _hits;

        private long _actions;
        private long _waits;

        private ChaitePolicyDriver(TerrariaFacade game, ExportedPolicy policy,
            ChaiteObservation observation)
        {
            _game = game;
            _policy = policy;
            _observation = observation;
            _values = new float[observation.ObservationCount];
        }

        /// <summary>
        /// The driver named by the environment, or null when the exported
        /// format is not selected.
        ///
        /// The observation configuration comes from the same variables the
        /// training session used (<c>CHAITE_PROJ_SLOTS</c>,
        /// <c>CHAITE_OBS_AGG</c>, <c>CHAITE_OBS_WORLD_BOUND</c>), so the vector
        /// this builds has the width and the clip the loaded checkpoint was
        /// trained with. The file's own internal consistency is checked by
        /// <see cref="ExportedPolicy"/> while it loads; the width agreement
        /// between that file and this configuration is checked here, before a
        /// single tick runs.
        ///
        /// Every failure path throws. A configured-but-broken policy must never
        /// silently fall back to the script, because that would blend two
        /// different controllers into one run -- the same rule
        /// <see cref="LearnedPolicy"/> follows.
        /// </summary>
        public static ChaitePolicyDriver FromEnvironment(TerrariaFacade game)
        {
            var policy = ExportedPolicy.ForConfiguredFile();
            if (policy == null) return null;
            if (game == null)
                throw new ArgumentNullException("game");
            var observation = ChaiteObservation.FromEnvironment();
            // A checkpoint trained on a wider window than the plugin is
            // configured for would be handed a vector its first layer cannot
            // read. The two numbers are both configuration, so this has to fail
            // at startup, naming the variables, rather than on the first tick.
            observation.RequireObservationCount(policy.ObservationDimension,
                "the exported policy at " + policy.SourcePath);
            return new ChaitePolicyDriver(game, policy, observation);
        }

        /// <summary>Observation width this driver builds.</summary>
        public int ObservationCount
        {
            get { return _observation.ObservationCount; }
        }

        /// <summary>Shapes of the loaded policy, for a startup log.</summary>
        public string LayerShapes { get { return _policy.LayerShapes; } }

        /// <summary>Path the weights came from, for a startup log.</summary>
        public string SourcePath { get { return _policy.SourcePath; } }

        /// <summary>Decisions applied since the process started.</summary>
        public long AppliedActions { get { return _actions; } }

        /// <summary>Game ticks that carried no action because the observation
        /// for them had not been built yet (the first tick of a session, and
        /// the tick after the game clock jumps).</summary>
        public long WaitedTicks { get { return _waits; } }

        /// <summary>
        /// Records the state the observation is built from, and returns the
        /// action for the tick that is starting now.
        ///
        /// Call once per tick, before the plan is applied. The two halves are
        /// deliberately not independent: <paramref name="player"/> is the state
        /// at this tick's entry, which is what the trainer's tick-T row
        /// described, so the action returned here is the one that row's policy
        /// chose, and the row built now becomes the action for the next tick.
        /// </summary>
        public void Observe(object player, in TargetSnapshot target, long tick)
        {
            // Count the hit this tick's state shows before deciding anything,
            // so the observation built below carries the same hit flag the
            // trainer's row would have.
            CountHit(_game.PlayerLife(player));
            var next = _observation.ObsVector(_game.BuildChaiteObservationRow(
                player, in target, _observation, _hits));
            _pending = next;
            _pendingTick = tick;
        }

        /// <summary>
        /// Writes the policy's decision for the tick that is starting into
        /// <paramref name="plan"/>, or returns false when no decision covers it.
        ///
        /// False means "leave the plan alone", and there is exactly one
        /// situation that produces it: the observation for the previous tick is
        /// not available (the first controlled tick of a session, or a tick
        /// after the game clock jumped). Holding the planner's own controls for
        /// that single frame is the same hold-the-last-decision behaviour the
        /// bridge channel documents, and it is a frame the policy has no
        /// observation for either way.
        /// </summary>
        public bool Apply(long tick, ref ControlPlan plan)
        {
            if (_pending == null || _pendingTick != tick - 1L)
            {
                _waits++;
                return false;
            }
            var action = _policy.ChooseAction(_pending);
            _pending = null;
            _pendingTick = -1L;
            _actions++;
            ApplyAction(action, ref plan);
            return true;
        }

        /// <summary>
        /// The action alphabet, decoded through the one place the plugin writes
        /// it down (<see cref="ExportedPolicy.DecodeAction"/>) and then applied
        /// to the plan. The width comes from the loaded file, so both the legacy
        /// 12-action alphabet and the 24-action one the bridge-trained policies
        /// use go through here.
        ///
        /// The plan fields are the same ones the bridge replay overwrites, and
        /// the same ones it clears. JumpAction follows the requested value:
        /// <see cref="JumpAction.Hold"/> forces true inside the resolver, so a
        /// decision to release the key would otherwise still hold it.
        /// </summary>
        private void ApplyAction(int action, ref ControlPlan plan)
        {
            int direction;
            bool up;
            bool down;
            bool dash;
            ExportedPolicy.DecodeAction(_policy.OutputCount, action,
                out direction, out up, out down, out dash);
            plan.Horizontal = direction;
            plan.Jump = up;
            plan.Dash = dash;
            // The learned action is a direct control command, so the facade must
            // apply it as written instead of validating it against the formula
            // route's dash contract. See ControlPlan.PolicyOwnsMobility.
            plan.PolicyOwnsMobility = true;
            plan.JumpAction = up ? JumpAction.Hold : JumpAction.Release;
            plan.Drop = down;
            plan.FeatherFallUp = false;
            plan.GravityControl = 0;
            plan.ToggleMount = false;
            plan.Hook = false;
            plan.HoldNeutralControls = false;
            // Movement only. The probe drives the Boss's health from its own
            // simulated output, so the policy must not emit a shot, and the
            // output certificate is dropped rather than left stale.
            plan.Fire = false;
            plan.OutputRouteKind = OutputRouteKind.Unspecified;
            plan.ExpectedWeaponId = 0;
            plan.ExpectedAmmoId = 0;
            plan.ExpectedProjectileId = 0;
        }

        /// <summary>
        /// Counts a hit from the life the tick entry observes.
        ///
        /// The observation's <c>hits</c> flag ("has this episode taken a hit at
        /// all") has no native field behind it: the probe counts hits itself,
        /// by watching the player's life fall, and publishes its running total.
        /// The plugin has no copy of that counter, so it counts the same way,
        /// from the same signal. Life only falls within an episode, so the count
        /// is monotone and the flag flips once, exactly as the trainer's does.
        /// The first observation of a session establishes the baseline without
        /// counting: a plugin that attached mid-fight has no way to know how
        /// many hits preceded it, and the flag it feeds is "a hit has happened
        /// since we started watching", which is the conservative reading.
        /// </summary>
        private void CountHit(float life)
        {
            if (life < _previousLife) _hits++;
            _previousLife = life;
        }

        /// <summary>Hits counted since the first observation of this session.</summary>
        public float Hits { get { return _hits; } }
    }
}
