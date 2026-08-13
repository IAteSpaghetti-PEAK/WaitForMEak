using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Hands a late joiner a pack, per <see cref="PackOnJoin"/>.
    ///
    /// Both routes go through the master client's inventory authority: <c>Player.AddItem</c>
    /// refuses to run anywhere else, drops a <see cref="Backpack"/> straight into the worn
    /// backpack slot, and pushes the result to everyone with <c>SyncInventoryRPC</c>. That is
    /// the same path a normal pickup takes, so nothing here needs the joiner to have the mod.
    /// </summary>
    internal static class JoinPack
    {
        internal static void Grant(Character c)
        {
            if (c == null || c.player == null) return;

            switch (WaitConfig.PackForJoiners.Value)
            {
                case PackOnJoin.AlwaysFannypack:
                    GiveFresh(c, "Fannypack");
                    break;
                case PackOnJoin.OnlyIfLeftBehind:
                    GiveLeftBehind(c);
                    break;
            }
        }

        private static void GiveFresh(Character c, string prefabName)
        {
            if (!c.player.backpackSlot.IsEmpty())
            {
                Plugin.Log.LogInfo($"{c.characterName} already has a pack - not adding another.");
                return;
            }
            if (!ItemDatabase.TryGetItem(prefabName, out Item prefab) || prefab == null)
            {
                Plugin.Log.LogWarning($"Couldn't find the '{prefabName}' item in the database.");
                return;
            }
            if (c.player.AddItem(prefab.itemID, null, out _))
                Plugin.Log.LogInfo($"Gave {c.characterName} a {prefabName}.");
            else
                Plugin.Log.LogWarning($"Failed to give {c.characterName} a {prefabName}.");
        }

        /// <summary>
        /// Find a pack lying abandoned in the world and hand it - contents and all - to the
        /// joiner. Backpacks beat fanny packs; jetpacks and rocketpacks are left alone.
        /// </summary>
        private static void GiveLeftBehind(Character c)
        {
            if (!c.player.backpackSlot.IsEmpty())
            {
                Plugin.Log.LogInfo($"{c.characterName} already has a pack - not adding another.");
                return;
            }

            Backpack best = null;
            int bestRank = 0;

            Backpack[] all = Object.FindObjectsByType<Backpack>(FindObjectsSortMode.None);
            foreach (Backpack bp in all)
            {
                if (bp == null || bp.itemState != ItemState.Ground) continue;
                if (bp.holderCharacter != null) continue;
                // Backpack contents are rendered as real items parked far below the map; never
                // treat one of those as a pack somebody dropped.
                if (bp.transform.position.y < -100f) continue;

                int rank = bp.backpackType == BackpackSlot.BackpackType.Backpack ? 2
                         : bp.backpackType == BackpackSlot.BackpackType.Fannypack ? 1
                         : 0;
                if (rank == 0) continue;

                if (rank > bestRank)
                {
                    bestRank = rank;
                    best = bp;
                }
            }

            if (best == null)
            {
                Plugin.Log.LogInfo($"No pack was left behind in this run - {c.characterName} gets nothing.");
                return;
            }

            // Same call the game makes when someone picks an item up: it runs on the master,
            // moves the item (and its instance data, i.e. everything inside it) into the
            // character's inventory, and destroys the world object.
            Plugin.Log.LogInfo($"Handing the abandoned {best.backpackType} at {best.transform.position} " +
                               $"to {c.characterName}.");
            best.RequestPickup(c.photonView);
        }
    }
}
