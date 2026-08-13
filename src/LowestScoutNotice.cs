using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Tells a waiting joiner who they're waiting on, without touching their camera.
    ///
    /// The spectator target is chosen entirely on the spectating player's own machine
    /// (<see cref="MainCameraMovement"/> keeps it in a private static), so the host can't steer it
    /// and shouldn't try. What it can do is say who the lowest scout is and let the player press
    /// left/right themselves. The host sends a plain Photon event while it's holding someone;
    /// vanilla clients ignore it, because nothing is listening for that event code. So the hint is
    /// a bonus for joiners who also have the mod, and the rest of it stays host-only.
    /// </summary>
    internal static class LowestScoutNotice
    {
        /// <summary>Photon event codes 0-199 are free for games to use; PEAK itself only uses 18.</summary>
        internal const byte EventCode = 79;

        /// <summary>How long a notice stays live without a refresh.</summary>
        private const float NoticeLifetime = 5f;

        private static int _lowestViewId;
        private static float _expires;

        private static readonly System.Collections.Generic.Dictionary<int, float> _nextPush =
            new System.Collections.Generic.Dictionary<int, float>();

        // ------------------------------------------------------------------------- host side

        internal static void Push(int actorNumber, Character lowest)
        {
            if (lowest == null || lowest.photonView == null) return;
            if (_nextPush.TryGetValue(actorNumber, out float next) && Time.time < next) return;
            _nextPush[actorNumber] = Time.time + 1f;
            Raise(actorNumber, lowest.photonView.ViewID);
        }

        internal static void Clear(int actorNumber)
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

        // ----------------------------------------------------------------------- client side

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
            _lowestViewId = viewId;
            _expires = Time.time + NoticeLifetime;
        }

        /// <summary>
        /// The scout the host is waiting on for us, or null when we aren't being held.
        /// </summary>
        internal static Character Lowest
        {
            get
            {
                if (_lowestViewId == 0 || Time.time > _expires) return null;

                Character local = Character.localCharacter;
                if (local == null || !local.data.dead) return null; // alive again, nothing to wait for

                PhotonView view = PhotonView.Find(_lowestViewId);
                return view != null ? view.GetComponent<Character>() : null;
            }
        }
    }

    /// <summary>
    /// Draws the hint as a second line under the spectated player's name.
    ///
    /// The label is a stripped copy of vanilla's own <c>spectatingNameText</c>, parented to it
    /// rather than to its container. A child isn't touched by any layout group driving the
    /// container's children, so it stays put under the name wherever that name ends up.
    /// </summary>
    internal class LowestScoutNoticeUI : MonoBehaviour
    {
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _clonedFrom;

        private void LateUpdate()
        {
            Character lowest = LowestScoutNotice.Lowest;
            if (lowest == null)
            {
                Hide();
                return;
            }

            GUIManager gui = GUIManager.instance;
            if (gui == null || gui.spectatingNameText == null || gui.spectatingObject == null) return;

            // Only ride along when the game is already showing the spectate panel.
            if (!gui.spectatingObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            if (!EnsureLabel(gui.spectatingNameText)) return;

            _label.text = (MainCameraMovement.specCharacter == lowest)
                ? "LOWEST PLAYER"
                : "LOWEST PLAYER: " + lowest.characterName;
            _label.color = gui.spectatingNameColor;

            if (!_label.gameObject.activeSelf) _label.gameObject.SetActive(true);
        }

        private void Hide()
        {
            if (_label != null && _label.gameObject.activeSelf)
                _label.gameObject.SetActive(false);
        }

        private bool EnsureLabel(TextMeshProUGUI nameText)
        {
            // The panel is rebuilt between runs, so re-clone if the source changed out from under us.
            if (_label != null && _clonedFrom == nameText) return true;
            if (_label != null) Destroy(_label.gameObject);

            GameObject go = Instantiate(nameText.gameObject);
            go.name = "WaitForMEak_LowestScoutNotice";

            // Anything else riding on the original (localisation, animation, layout) would fight
            // us for the text, so keep only what draws it.
            foreach (Component comp in go.GetComponents<Component>())
            {
                if (comp is RectTransform || comp is TextMeshProUGUI || comp is CanvasRenderer) continue;
                Destroy(comp);
            }
            for (int i = go.transform.childCount - 1; i >= 0; i--)
                Destroy(go.transform.GetChild(i).gameObject);

            _label = go.GetComponent<TextMeshProUGUI>();
            if (_label == null)
            {
                Destroy(go);
                return false;
            }

            RectTransform rt = _label.rectTransform;
            rt.SetParent(nameText.transform, worldPositionStays: false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(Mathf.Max(320f, nameText.rectTransform.rect.width), 28f);

            _label.enableAutoSizing = false;
            _label.fontSize = Mathf.Max(10f, nameText.fontSize * 0.55f);
            _label.alignment = TextAlignmentOptions.Top;
            _label.raycastTarget = false;
            _label.gameObject.SetActive(false);

            _clonedFrom = nameText;
            return true;
        }

        private void OnDestroy()
        {
            if (_label != null) Destroy(_label.gameObject);
        }
    }
}
