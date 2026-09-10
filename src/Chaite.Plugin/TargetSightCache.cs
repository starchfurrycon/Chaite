using Chaite.Core;

namespace Chaite.Plugin
{
    internal sealed class TargetSightCache
    {
        private struct Entry
        {
            public int Frame, Type;
            public Vec2 Player, Target;
            public bool Visible;
        }
        private readonly Entry[] _entries = new Entry[200];

        public bool TryGet(int key, int type, Vec2 player, Vec2 target, int frame, out bool visible)
        {
            visible = false;
            if (key < 0 || key >= _entries.Length) return false;
            var entry = _entries[key];
            if (entry.Frame <= 0 || entry.Type != type || frame < entry.Frame || frame - entry.Frame > 12 ||
                Vec2.DistanceSquared(entry.Player, player) > 4096f || Vec2.DistanceSquared(entry.Target, target) > 4096f)
                return false;
            visible = entry.Visible;
            return true;
        }

        public void Record(int key, int type, Vec2 player, Vec2 target, int frame, bool visible)
        {
            if (key < 0 || key >= _entries.Length) return;
            _entries[key] = new Entry { Frame = frame, Type = type, Player = player, Target = target, Visible = visible };
        }

        public void Clear() => System.Array.Clear(_entries, 0, _entries.Length);
    }
}
