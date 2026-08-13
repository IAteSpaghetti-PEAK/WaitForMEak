using System;
using Photon.Pun;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Curse handed to a late joiner on arrival.
    ///
    /// Two pieces: the Ascent 7/8 starting Curse (always, no toggle - it's the Ascent's rule,
    /// not ours) and, optionally, the Curse a revive would have cost them.
    ///
    /// The amount is applied as a *difference*, because the game may already have given them
    /// some of it: a late joiner spawning at a lit base camp gets the starting Curse from
    /// <c>StartPassedOutOnTheBeach</c>, and the revival Curse if the camp had a revive going.
    /// Working from the delta means the joiner ends up at exactly the configured total no
    /// matter which of those paths the game took.
    /// </summary>
    internal static class JoinCurse
    {
        internal static void Apply(Character c)
        {
            if (c == null || c.refs == null || c.refs.afflictions == null) return;

            float wanted = Ascents.startingCurse
                         + (WaitConfig.CurseAsIfRevived.Value ? Ascents.revivalCurse : 0f);
            float current = c.refs.afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Curse);
            float delta = wanted - current;

            if (Mathf.Abs(delta) < 0.001f)
            {
                Plugin.Log.LogInfo($"{c.characterName} already has the right Curse ({current:0.###}).");
                return;
            }

            int statusCount = Enum.GetNames(typeof(CharacterAfflictions.STATUSTYPE)).Length;
            float[] payload = new float[statusCount];
            payload[(int)CharacterAfflictions.STATUSTYPE.Curse] = delta;

            // Master-client-only RPC on CharacterAfflictions: every client (including the
            // owner, who is the authority for their own statuses) applies it and re-syncs.
            c.refs.afflictions.photonView.RPC("RPC_ApplyStatusesFromFloatArray", RpcTarget.All, payload);
            Plugin.Log.LogInfo($"Curse for {c.characterName}: {current:0.###} -> {wanted:0.###} " +
                               $"(ascent start {Ascents.startingCurse:0.###}, revival " +
                               $"{(WaitConfig.CurseAsIfRevived.Value ? Ascents.revivalCurse : 0f):0.###}).");
        }
    }
}
