using System;

namespace Chaite.Plugin
{
    internal static class SummonActionGate
    {
        public static bool ShouldPulse(bool animationReady, bool selectedSlotMatches, bool releaseReady,
            int tick, int lastPulseTick)
        {
            return animationReady && selectedSlotMatches && releaseReady &&
                (lastPulseTick < 0 || tick - lastPulseTick >= 6);
        }

        public static bool ShouldFire(bool usable, bool hasAmmo, bool selectedSlotMatches,
            bool autoReuse, bool channel, bool releaseReady)
        {
            return usable && hasAmmo && selectedSlotMatches && (autoReuse || channel || releaseReady);
        }
    }

    /// <summary>
    /// Preserves the exact original item object across vanilla's synchronous
    /// DropSelectedItem, which replaces the inventory slot after requesting a drop.
    /// No item defaults, synthetic item creation, or blind occupied-slot overwrite.
    /// </summary>
    internal sealed class SingleItemDropTransaction
    {
        private readonly object _original;
        private readonly int _slot;
        private readonly int _count;
        private readonly bool _favorite;
        private readonly Func<object, int> _type;
        private readonly Action<object, int> _setStack;
        private readonly Action<object, bool> _setFavorite;
        public bool Completed { get; private set; }
        public bool WasDropped { get; private set; }

        public SingleItemDropTransaction(object[] inventory, int slot, Func<object, int> getType,
            Func<object, int> getStack, Func<object, bool> getFavorite,
            Action<object, int> setStack, Action<object, bool> setFavorite)
        {
            if (inventory == null || slot < 0 || slot >= inventory.Length || inventory[slot] == null)
                throw new ArgumentException("Invalid drop slot");
            _original = inventory[slot];
            _slot = slot;
            _count = getStack(_original);
            if (_count < 1 || getType(_original) <= 0) throw new ArgumentException("Empty drop slot");
            _favorite = getFavorite(_original);
            _type = getType;
            _setStack = setStack;
            _setFavorite = setFavorite;
        }

        public void Prepare()
        {
            _setStack(_original, 1);
            _setFavorite(_original, false);
        }

        public bool Restore(object[] inventory)
        {
            if (Completed) return true;
            if (inventory == null || _slot >= inventory.Length) return false;
            // Vanilla did not consume the object (or another operation merely moved
            // it): restore ALL N, including a favorited one-item stack.
            for (var i = 0; i < inventory.Length; i++)
            {
                if (!ReferenceEquals(inventory[i], _original)) continue;
                _setStack(_original, _count);
                _setFavorite(_original, _favorite);
                Completed = true;
                return true;
            }
            // An unexpected non-empty replacement is ambiguous. Retain this pending
            // transaction and refuse to overwrite or manufacture compensating items.
            if (inventory[_slot] != null && _type(inventory[_slot]) != 0) return false;
            WasDropped = true;
            if (_count > 1)
            {
                _setStack(_original, _count - 1);
                _setFavorite(_original, _favorite);
                inventory[_slot] = _original;
            }
            Completed = true;
            return true;
        }
    }
}
