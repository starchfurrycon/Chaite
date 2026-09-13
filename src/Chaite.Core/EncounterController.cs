using System.Collections.Generic;

namespace Chaite.Core
{
    public sealed class EncounterController
    {
        private readonly HashSet<int> _seenBosses = new HashSet<int>();
        private readonly HashSet<int> _killedBosses = new HashSet<int>();
        private readonly int _clearGraceTicks;
        private readonly int _hitSoundCooldownTicks;
        private int _missingTicks;
        private int _lastLife = -1;
        private int _hitCueCooldown;
        private bool _died;
        private bool _preparingBoss;

        public EncounterController(int clearGraceTicks, int hitSoundCooldownTicks = 45)
        {
            _clearGraceTicks = clearGraceTicks < 1 ? 1 : clearGraceTicks;
            _hitSoundCooldownTicks = hitSoundCooldownTicks < 1 ? 1 : hitSoundCooldownTicks;
        }

        public SessionState State { get; private set; } = SessionState.Idle;
        public bool IsControlling => State == SessionState.PreparingBoss || State == SessionState.AwaitingBossSpawn ||
                                     State == SessionState.EngagedAlive || State == SessionState.EngagedDeadWaitingRespawn;
        public bool DiedDuringSession => _died;
        public bool IsSessionActive => IsControlling || State == SessionState.Monitoring;

        public StateUpdate ArmMonitoring(EncounterObservation observation)
        {
            if (IsSessionActive)
                return Snapshot(AudioCue.None, false, false);
            ResetSessionData();
            var previous = State;
            if (observation == null || observation.PlayerDead || observation.HasEncounter || !observation.StartAuthorized)
            {
                State = SessionState.RejectedNoEncounter;
                return Result(previous, AudioCue.UntestedLoadout, false, true);
            }
            State = SessionState.Monitoring;
            return Result(previous, AudioCue.MonitorArmed, false, false);
        }

        public StateUpdate Activate(EncounterObservation observation)
        {
            if (IsControlling)
                return Snapshot(AudioCue.None, !observation.PlayerDead, false);

            ResetSessionData();
            State = SessionState.Validating;
            ObserveBosses(observation);

            if (!observation.StartAuthorized)
            {
                var previous = State;
                State = SessionState.RejectedNoEncounter;
                return Result(previous, AudioCue.NoSlimeAng, false, true);
            }

            _lastLife = observation.PlayerLife;
            var before = State;
            _preparingBoss = observation.RequirePreparation || !observation.HasEncounter;
            State = _preparingBoss ? SessionState.PreparingBoss :
                (observation.PlayerDead ? SessionState.EngagedDeadWaitingRespawn : SessionState.EngagedAlive);
            _died = observation.PlayerDead;
            return Result(before, AudioCue.TryMinnie, !observation.PlayerDead, false);
        }

        public StateUpdate Update(EncounterObservation observation)
        {
            if (!IsControlling)
                return Snapshot(AudioCue.None, false, IsTerminal(State));

            var previous = State;
            var cue = AudioCue.None;
            var respawnedThisUpdate = false;
            ObserveBosses(observation);
            // Respawn life is not damage taken relative to pre-death life.
            if (observation.PlayerDead) _lastLife = -1;

            if (_hitCueCooldown > 0)
                _hitCueCooldown--;

            if (!observation.PlayerDead && _lastLife >= 0 && observation.PlayerLife < _lastLife && _hitCueCooldown == 0)
            {
                cue = AudioCue.Man;
                _hitCueCooldown = _hitSoundCooldownTicks;
            }

            if (_preparingBoss)
            {
                if (observation.PlayerDead && !_died)
                {
                    _died = true;
                    cue = AudioCue.Dead;
                }
                if (observation.ExpectedBossArrived && (observation.HasEncounter || observation.KilledBossKeys.Count > 0))
                {
                    _preparingBoss = false;
                    State = observation.PlayerDead ? SessionState.EngagedDeadWaitingRespawn : SessionState.EngagedAlive;
                    _missingTicks = 0;
                    return Result(previous, cue, !observation.PlayerDead, false);
                }
                if (!observation.WaitingStillValid)
                {
                    State = _died ? SessionState.FailedAfterDeath : SessionState.EncounterInterrupted;
                    cue = _died ? AudioCue.LowLevelChaite : AudioCue.None;
                    return Result(previous, cue, false, true);
                }
                if (observation.PlayerLife > 0)
                    _lastLife = observation.PlayerLife;
                return Result(previous, cue, !observation.PlayerDead, false);
            }

            if (observation.PlayerDead && State != SessionState.EngagedDeadWaitingRespawn)
            {
                _died = true;
                State = SessionState.EngagedDeadWaitingRespawn;
                cue = AudioCue.Dead;
            }
            else if (!observation.PlayerDead && State == SessionState.EngagedDeadWaitingRespawn && observation.HasEncounter)
            {
                State = SessionState.EngagedAlive;
                respawnedThisUpdate = true;
            }

            if (observation.PlayerLife > 0)
                _lastLife = observation.PlayerLife;

            if (observation.HasEncounter)
            {
                _missingTicks = 0;
                // Player.Update observes dead/life before vanilla has rebuilt
                // equipment-derived movement fields for the respawned player.
                // Reserve this edge as one neutral settle frame so runtime
                // admission cannot reject a valid loadout from stale defaults.
                return Result(previous, cue,
                    State == SessionState.EngagedAlive &&
                    !respawnedThisUpdate, false);
            }

            _missingTicks++;
            if (_missingTicks <= _clearGraceTicks)
                return Result(previous, cue, false, false);

            var allSeenBossesKilled = _seenBosses.Count > 0 && _killedBosses.IsSupersetOf(_seenBosses);
            var completed = allSeenBossesKilled;

            if (_died)
            {
                State = completed ? SessionState.SuccessAfterDeath : SessionState.FailedAfterDeath;
                cue = completed ? AudioCue.FailedBossDesign : AudioCue.LowLevelChaite;
            }
            else if (completed)
            {
                State = SessionState.SuccessNoDeath;
                cue = AudioCue.MambaOut;
            }
            else
            {
                State = SessionState.EncounterInterrupted;
            }

            return Result(previous, cue, false, true);
        }

        public StateUpdate Cancel()
        {
            var previous = State;
            State = SessionState.Cancelled;
            return Result(previous, AudioCue.None, false, true);
        }

        public void MarkSummonIssued()
        {
            if (_preparingBoss && State == SessionState.PreparingBoss)
                State = SessionState.AwaitingBossSpawn;
        }

        public void ReturnToIdle()
        {
            State = SessionState.Idle;
            ResetSessionData();
        }

        private void ObserveBosses(EncounterObservation observation)
        {
            for (var i = 0; i < observation.ActiveBossKeys.Count; i++)
                _seenBosses.Add(observation.ActiveBossKeys[i]);
            for (var i = 0; i < observation.KilledBossKeys.Count; i++)
            {
                _seenBosses.Add(observation.KilledBossKeys[i]);
                _killedBosses.Add(observation.KilledBossKeys[i]);
            }
        }

        private void ResetSessionData()
        {
            _seenBosses.Clear();
            _killedBosses.Clear();
            _missingTicks = 0;
            _lastLife = -1;
            _hitCueCooldown = 0;
            _died = false;
            _preparingBoss = false;
        }

        private StateUpdate Snapshot(AudioCue cue, bool applyControls, bool terminal)
        {
            return new StateUpdate { Previous = State, Current = State, Cue = cue, ApplyControls = applyControls, BecameTerminal = terminal };
        }

        private StateUpdate Result(SessionState previous, AudioCue cue, bool applyControls, bool terminal)
        {
            return new StateUpdate { Previous = previous, Current = State, Cue = cue, ApplyControls = applyControls, BecameTerminal = terminal };
        }

        private static bool IsTerminal(SessionState state)
        {
            return state == SessionState.RejectedNoEncounter || state == SessionState.SuccessNoDeath ||
                   state == SessionState.SuccessAfterDeath || state == SessionState.FailedAfterDeath ||
                   state == SessionState.Cancelled || state == SessionState.EncounterInterrupted;
        }
    }
}
