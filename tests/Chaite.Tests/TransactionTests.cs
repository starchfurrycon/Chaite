using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunTransactionRegressions()
        {
            Run(nameof(BlockedDropRestoresWholeStackAndFavorite), BlockedDropRestoresWholeStackAndFavorite);
            Run(nameof(SuccessfulDropRestoresOnlyRemainder), SuccessfulDropRestoresOnlyRemainder);
            Run(nameof(SingleSuccessfulDropNeverResurrectsItem), SingleSuccessfulDropNeverResurrectsItem);
            Run(nameof(SingleBlockedDropRestoresFavorite), SingleBlockedDropRestoresFavorite);
            Run(nameof(DropExceptionRestoresUnconsumedStack), DropExceptionRestoresUnconsumedStack);
            Run(nameof(DropRestorationNeverOverwritesOccupiedSlot), DropRestorationNeverOverwritesOccupiedSlot);
            Run(nameof(MovedOriginalIsRestoredInPlace), MovedOriginalIsRestoredInPlace);
            Run(nameof(DropRestorationIsIdempotent), DropRestorationIsIdempotent);
            Run(nameof(SummonPulseRequiresIdleActualSelectionAndRelease), SummonPulseRequiresIdleActualSelectionAndRelease);
            Run(nameof(LacewingFireWaitsForWeaponSelectionAndSemiAutoRelease), LacewingFireWaitsForWeaponSelectionAndSemiAutoRelease);
        }

        private sealed class TransactionItem
        {
            public int Type = 267;
            public int Stack = 5;
            public bool Favorite = true;
            public string Metadata = "preserve exact item object";
        }

        private static object NewDropTransaction(object[] inventory)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.SingleItemDropTransaction", true);
            return Activator.CreateInstance(type, new object[]
            {
                inventory, 0,
                new Func<object, int>(x => ((TransactionItem)x).Type),
                new Func<object, int>(x => ((TransactionItem)x).Stack),
                new Func<object, bool>(x => ((TransactionItem)x).Favorite),
                new Action<object, int>((x, value) => ((TransactionItem)x).Stack = value),
                new Action<object, bool>((x, value) => ((TransactionItem)x).Favorite = value)
            });
        }

        private static void PrepareDrop(object transaction) => transaction.GetType().GetMethod("Prepare").Invoke(transaction, null);
        private static bool RestoreDrop(object transaction, object[] inventory) =>
            (bool)transaction.GetType().GetMethod("Restore").Invoke(transaction, new object[] { inventory });
        private static bool DropProperty(object transaction, string name) => (bool)transaction.GetType().GetProperty(name).GetValue(transaction);
        private static TransactionItem EmptyTransactionItem() => new TransactionItem { Type = 0, Stack = 0, Favorite = false };

        private static void BlockedDropRestoresWholeStackAndFavorite()
        {
            var item = new TransactionItem();
            var inventory = new object[] { item };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            Equal(1, item.Stack);
            False(item.Favorite);
            True(RestoreDrop(transaction, inventory));
            Equal(5, item.Stack);
            True(item.Favorite);
            False(DropProperty(transaction, "WasDropped"));
        }

        private static void SuccessfulDropRestoresOnlyRemainder()
        {
            var item = new TransactionItem();
            var inventory = new object[] { item };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            inventory[0] = EmptyTransactionItem(); // Vanilla successful DropSelectedItem replaces the slot.
            True(RestoreDrop(transaction, inventory));
            True(ReferenceEquals(item, inventory[0]));
            Equal(4, item.Stack);
            True(item.Favorite);
            Equal("preserve exact item object", item.Metadata);
            True(DropProperty(transaction, "WasDropped"));
        }

        private static void SingleSuccessfulDropNeverResurrectsItem()
        {
            var inventory = new object[] { new TransactionItem { Stack = 1 } };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            var empty = EmptyTransactionItem();
            inventory[0] = empty;
            True(RestoreDrop(transaction, inventory));
            True(ReferenceEquals(empty, inventory[0]));
            Equal(0, ((TransactionItem)inventory[0]).Type);
            True(DropProperty(transaction, "WasDropped"));
        }

        private static void SingleBlockedDropRestoresFavorite()
        {
            var item = new TransactionItem { Stack = 1 };
            var inventory = new object[] { item };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            True(RestoreDrop(transaction, inventory));
            Equal(1, item.Stack);
            True(item.Favorite);
        }

        private static void DropExceptionRestoresUnconsumedStack()
        {
            var item = new TransactionItem();
            var inventory = new object[] { item };
            var transaction = NewDropTransaction(inventory);
            try
            {
                try { PrepareDrop(transaction); throw new InvalidOperationException("simulated blocked native call"); }
                finally { RestoreDrop(transaction, inventory); }
            }
            catch (InvalidOperationException) { }
            Equal(5, item.Stack);
            True(item.Favorite);
        }

        private static void DropRestorationNeverOverwritesOccupiedSlot()
        {
            var inventory = new object[] { new TransactionItem() };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            var unrelated = new TransactionItem { Type = 100, Stack = 7 };
            inventory[0] = unrelated;
            False(RestoreDrop(transaction, inventory));
            True(ReferenceEquals(unrelated, inventory[0]));
            Equal(7, unrelated.Stack);
            False(DropProperty(transaction, "Completed"));
        }

        private static void MovedOriginalIsRestoredInPlace()
        {
            var original = new TransactionItem();
            var unrelated = new TransactionItem { Type = 100, Stack = 7 };
            var inventory = new object[] { original, EmptyTransactionItem() };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            inventory[0] = unrelated;
            inventory[1] = original;
            True(RestoreDrop(transaction, inventory));
            Equal(5, original.Stack);
            True(original.Favorite);
            True(ReferenceEquals(unrelated, inventory[0]));
        }

        private static void DropRestorationIsIdempotent()
        {
            var original = new TransactionItem();
            var inventory = new object[] { original };
            var transaction = NewDropTransaction(inventory);
            PrepareDrop(transaction);
            inventory[0] = EmptyTransactionItem();
            True(RestoreDrop(transaction, inventory));
            True(RestoreDrop(transaction, inventory));
            Equal(4, original.Stack);
        }

        private static void SummonPulseRequiresIdleActualSelectionAndRelease()
        {
            var gate = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.SummonActionGate", true)
                .GetMethod("ShouldPulse", BindingFlags.Public | BindingFlags.Static);
            Func<bool, bool, bool, int, int, bool> ready = (animation, selected, release, tick, last) =>
                (bool)gate.Invoke(null, new object[] { animation, selected, release, tick, last });
            True(ready(true, true, true, 1, -1));
            False(ready(false, true, true, 1, -1));
            False(ready(true, false, true, 1, -1));
            False(ready(true, true, false, 1, -1));
            False(ready(true, true, true, 6, 1));
            True(ready(true, true, true, 7, 1));
        }

        private static void LacewingFireWaitsForWeaponSelectionAndSemiAutoRelease()
        {
            var gate = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.SummonActionGate", true)
                .GetMethod("ShouldFire", BindingFlags.Public | BindingFlags.Static);
            Func<bool, bool, bool, bool> canFire = (selected, autoReuse, released) =>
                (bool)gate.Invoke(null, new object[] { true, true, selected, autoReuse, false, released });
            False(canFire(false, true, true)); // Actual selection still holds the critter item.
            False(canFire(true, false, false));
            True(canFire(true, false, true));
            True(canFire(true, true, false));
        }
    }
}
