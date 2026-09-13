using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Media;
using System.Threading;

namespace Chaite.Plugin
{
    internal sealed class AudioCuePlayer
    {
        private readonly string _audioDirectory;
        private readonly Dictionary<AudioCue, string> _files = new Dictionary<AudioCue, string>
        {
            { AudioCue.NoSlimeAng, "no_slime_ang.wav" },
            { AudioCue.TryMinnie, "try_minnie.wav" },
            { AudioCue.Man, "man.wav" },
            { AudioCue.Dead, "dead.wav" },
            { AudioCue.MambaOut, "mamba_out.wav" },
            { AudioCue.FailedBossDesign, "failed_boss_design.wav" },
            { AudioCue.LowLevelChaite, "low_level_chaite.wav" },
            { AudioCue.UnsupportedBoss, "boss_too_hard_for_me.wav" },
            { AudioCue.UntestedLoadout, "never_tried_this_loadout.wav" }
        };
        private readonly HashSet<string> _reportedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<AudioCue> _pending = new HashSet<AudioCue>();
        private readonly ConcurrentDictionary<AudioCue, SoundPlayer> _players = new ConcurrentDictionary<AudioCue, SoundPlayer>();
        private readonly ConcurrentDictionary<AudioCue, string> _warnings = new ConcurrentDictionary<AudioCue, string>();
        private readonly object _stateGate = new object();
        private readonly Action<SoundPlayer> _playLoaded;

        public AudioCuePlayer(string audioDirectory)
            : this(audioDirectory,
                preload => ThreadPool.QueueUserWorkItem(_ => preload()),
                player => player.Play())
        {
        }

        internal AudioCuePlayer(string audioDirectory,
            Action<Action> queuePreload, Action<SoundPlayer> playLoaded)
        {
            _audioDirectory = audioDirectory;
            _playLoaded = playLoaded ?? throw new ArgumentNullException(nameof(playLoaded));
            if (queuePreload == null) throw new ArgumentNullException(nameof(queuePreload));
            // File access and decoding must not stall a hit/death combat frame.
            // Until a slot finishes loading, collapse any number of requests
            // into one pending play. The worker consumes it after publishing
            // the loaded player, so the first cue cannot lose the preload race.
            // No generated/downloaded audio: preload only the user's local slots.
            queuePreload(Preload);
        }

        public string Play(AudioCue cue)
        {
            if (cue == AudioCue.None || !_files.TryGetValue(cue, out var file))
                return null;
            SoundPlayer player;
            lock (_stateGate)
            {
                if (!_players.TryGetValue(cue, out player))
                {
                    string warning;
                    if (_warnings.TryGetValue(cue, out warning))
                        return _reportedMissing.Add(file) ? warning : null;
                    _pending.Add(cue);
                    return null;
                }
            }

            return PlayLoaded(cue, file, player, true);
        }

        private string PlayLoaded(AudioCue cue, string file,
            SoundPlayer player, bool reportImmediately)
        {
            try
            {
                _playLoaded(player);
                return null;
            }
            catch (Exception ex)
            {
                var warning = "音频播放失败 " + file + ": " + ex.Message;
                lock (_stateGate)
                {
                    _warnings[cue] = warning;
                    return reportImmediately && _reportedMissing.Add(file) ?
                        warning : null;
                }
            }
        }

        private void Preload()
        {
            foreach (var pair in _files)
            {
                var path = Path.Combine(_audioDirectory, pair.Value);
                if (!File.Exists(path))
                {
                    lock (_stateGate)
                    {
                        _warnings[pair.Key] = "缺少音频 Audio/" + pair.Value + "（功能继续运行）";
                        _pending.Remove(pair.Key);
                    }
                    continue;
                }
                SoundPlayer player = null;
                try
                {
                    player = new SoundPlayer(path);
                    player.Load();
                    bool playPending;
                    lock (_stateGate)
                    {
                        _players[pair.Key] = player;
                        playPending = _pending.Remove(pair.Key);
                    }
                    if (playPending)
                        PlayLoaded(pair.Key, pair.Value, player, false);
                }
                catch (Exception ex)
                {
                    player?.Dispose();
                    lock (_stateGate)
                    {
                        _warnings[pair.Key] = "音频加载失败 " + pair.Value + ": " + ex.Message;
                        _pending.Remove(pair.Key);
                    }
                }
            }
        }
    }
}
