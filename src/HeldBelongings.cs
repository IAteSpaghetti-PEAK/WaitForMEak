using System;
using Photon.Pun;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Keeps a reconnecting player whole across the hold.
    ///
    /// Two things would otherwise be taken off them. Their items go first: the opening line of
    /// <c>RPCA_ReviveAtPosition</c> is <c>DropAllItems(includeBackpack: true)</c>, and a held
    /// player's body is parked at <c>DeathPos()</c>, so a run's worth of loot ends up somewhere
    /// unreachable. Their statuses go next, because <c>ReviveCharacter</c> calls
    /// <c>ClearAllStatus</c>. That would turn "leave and rejoin" into a free cure-all.
    ///
    /// Items are handled by never dropping them. <see cref="Revive"/> sends the two RPCs that
    /// <c>RPCA_ReviveAtPosition</c> is built from and leaves the drop out. That beats snapshotting
    /// the inventory and pushing it back afterwards, because the owner's client is the one that
    /// runs <c>DropAllItems</c>, and its <c>DropItemFromSlotRPC</c> calls would race the restore.
    /// The loser of that race duplicates items into the world and empties the slots again.
    ///
    /// Statuses do need a snapshot. That bookkeeping is why none of this runs unless the joiner
    /// was reconnecting, which only happens with "Also move reconnecting players" switched on.
    /// </summary>
    internal static class HeldBelongings
    {
        /// <summary>Every status the character is carrying, indexed by STATUSTYPE.</summary>
        internal static float[] SnapshotStatuses(Character c)
        {
            if (c == null || c.refs == null || c.refs.afflictions == null) return null;

            int count = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;
            float[] snapshot = new float[count];
            for (int i = 0; i < count; i++)
                snapshot[i] = c.refs.afflictions.GetCurrentStatus((CharacterAfflictions.STATUSTYPE)i);
            return snapshot;
        }

        /// <summary>
        /// Revive without the item drop, using the same two steps <c>RPCA_ReviveAtPosition</c>
        /// performs after it. Both are ordinary PunRPCs.
        ///
        /// What gets left behind along with the drop is bookkeeping: <c>justRevived</c> on their
        /// scout report, <c>lastRevivedSegment</c>, and the explicit call to
        /// <c>ReconnectHandler.UpdateFromRevive</c>. None of that is wanted here. They weren't
        /// revived, they were put back where they should have been. Their reconnect record
        /// refreshes itself regardless, off the <c>CharacterStateUpdated</c> and
        /// <c>WarpCompleted</c> hooks the handler already subscribes to.
        /// </summary>
        internal static void Revive(Character c, Vector3 position)
        {
            c.photonView.RPC("ReviveCharacter", RpcTarget.All, false);
            c.photonView.RPC("WarpPlayerRPC", RpcTarget.All, position, true);
        }

        /// <summary>
        /// Put back what <c>ClearAllStatus</c> took. This is sent as a difference from what they
        /// are carrying right now, so Curse (which the clear leaves alone) nets out to zero
        /// instead of being applied twice.
        /// </summary>
        internal static void RestoreStatuses(Character c, float[] snapshot)
        {
            if (c == null || snapshot == null || c.refs == null || c.refs.afflictions == null) return;

            float[] delta = new float[snapshot.Length];
            bool any = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                float d = snapshot[i] - c.refs.afflictions.GetCurrentStatus((CharacterAfflictions.STATUSTYPE)i);
                if (Mathf.Abs(d) < 0.001f) continue;
                delta[i] = d;
                any = true;
            }

            if (!any)
            {
                Plugin.Log.LogInfo($"{c.characterName} came back with their statuses already intact.");
                return;
            }

            c.refs.afflictions.photonView.RPC("RPC_ApplyStatusesFromFloatArray", RpcTarget.All, delta);
            Plugin.Log.LogInfo($"Restored {c.characterName}'s statuses from before they were held.");
        }
    }
}
