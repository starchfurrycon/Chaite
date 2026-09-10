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
            { AudioCue.LowLevelChaite, "low_level_chaite.wav" }
        };
        private readonly HashSet<string> _reportedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<AudioCue, SoundPlayer> _players = new ConcurrentDictionary<AudioCue, SoundPlayer>();
        private readonly ConcurrentDictionary<AudioCue, string> _warnings = new ConcurrentDictionary<AudioCue, string>();

        public AudioCuePlayer(string audioDirectory)
        {
            _audioDirectory = audioDirectory;
            // File access and decoding must not stall a hit/death combat frame.
            // No generated/downloaded audio: preload only the user's local slots.
            ThreadPool.QueueUserWorkItem(_ => Preload());
        }

        public string Play(AudioCue cue)
        {
            if (cue == AudioCue.None || !_files.TryGetValue(cue, out var file))
                return null;
            if (!_players.TryGetValue(cue, out var player))
            {
                string warning;
                if (_warnings.TryGetValue(cue, out warning) && _reportedMissing.Add(file)) return warning;
                return null;
            }

            try
            {
                player.Play();
                return null;
            }
            catch (Exception ex)
            {
                return _reportedMissing.Add(file) ? "音频播放失败 " + file + ": " + ex.Message : null;
            }
        }

        private void Preload()
        {
            foreach (var pair in _files)
            {
                var path = Path.Combine(_audioDirectory, pair.Value);
                if (!File.Exists(path))
                {
                    _warnings[pair.Key] = "缺少音频 Audio/" + pair.Value + "（功能继续运行）";
                    continue;
                }
                SoundPlayer player = null;
                try
                {
                    player = new SoundPlayer(path);
                    player.Load();
                    _players[pair.Key] = player;
                }
                catch (Exception ex)
                {
                    player?.Dispose();
                    _warnings[pair.Key] = "音频加载失败 " + pair.Value + ": " + ex.Message;
                }
            }
        }
    }
}
