using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Points a waiting joiner's spectator camera at the scout they're going to be dropped on.
    ///
    /// The spectator target is chosen entirely on the spectating player's own machine
    /// (<see cref="MainCameraMovement"/> keeps it in a private static), so the host can't force
    /// it by itself. What the host CAN do is tell them who to watch: a plain Photon event,
    /// which vanilla clients silently ignore because nothing is listening for that event code.
    /// So this is a bonus for lobbies where the joiner also happens to have the mod - the rest
    /// of the mod works host-only regardless.
    /// </summary>
    internal static class SpectateOverride
    {
        /// <summary>Photon event codes 0-199 are free for games to use; PEAK itself only uses 18.</summary>
        internal const byte EventCode = 79;

        /// <summary>How long a pushed target stays valid without a refresh.</summary>
        private const float TargetLifetime = 5f;

        private static int _targetViewId;
        private static float _targetExpires;

        // ------------------------------------------------------------------------ host side

        private static readonly System.Collections.Generic.Dictionary<int, float> _nextPush =
            new System.Collections.Generic.Dictionary<int, float>();

        internal static void PushTarget(int actorNumber, Character target)
        {
            if (target == null || target.photonView == null) return;
            if (_nextPush.TryGetValue(actorNumber, out float next) && Time.time < next) return;
            _nextPush[actorNumber] = Time.time + 1f;
            Raise(actorNumber, target.photonView.ViewID);
        }

        internal static void ClearTarget(int actorNumber)
        {
            _nextPush.Remove(actorNumber);
            Raise(actorNumber, 0);
        }

        private static void Raise(int actorNumber, int viewId)
        {
            if (!PhotonNetwork.InRoom) return;
            var options = new RaiseEventOptions { TargetActors = new[] { actorNumber } };
            PhotonNetwork.RaiseEvent(EventCode, viewId, options, SendOptions.SendReliable);
        }

        // ---------------------------------------------------------------------- client side

        internal static void Register() => PhotonNetwork.NetworkingClient.EventReceived += OnEvent;

        internal static void Unregister()
        {
            if (PhotonNetwork.NetworkingClient != null)
                PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        }

        private static void OnEvent(EventData e)
        {
            if (e.Code != EventCode) return;
            if (!(e.CustomData is int viewId)) return;
            _targetViewId = viewId;
            _targetExpires = Time.time + TargetLifetime;
        }

        /// <summary>The scout the host wants us watching, or null if there isn't one right now.</summary>
        internal static Character ForcedTarget
        {
            get
            {
                if (_targetViewId == 0 || Time.time > _targetExpires) return null;

                Character local = Character.localCharacter;
                if (local == null || !local.data.dead) return null; // we're alive again - back to normal

                PhotonView view = PhotonView.Find(_targetViewId);
                if (view == null) return null;

                Character target = view.GetComponent<Character>();
                if (target == null || !target.data.canBeSpectated) return null;
                return target;
            }
        }
    }

    /// <summary>
    /// Runs after the camera has picked whoever it wanted to and overrides the choice, which
    /// also pins it there (the left/right spectate switch is re-overridden every frame).
    /// </summary>
    [HarmonyPatch(typeof(MainCameraMovement), "HandleSpecSelection")]
    internal static class MainCameraMovement_HandleSpecSelection_Patch
    {
        private static void Postfix(ref bool __result)
        {
            Character forced = SpectateOverride.ForcedTarget;
            if (forced == null) return;
            MainCameraMovement.specCharacter = forced;
            __result = true;
        }
    }
}
