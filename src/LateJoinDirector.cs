using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>One scout who joined mid-run and hasn't been dropped off yet.</summary>
    internal class PendingJoin
    {
        public int ActorNumber;
        public string Nickname;
        public float JoinedAt;

        public Character Character;
        public float CharacterSeenAt;

        /// <summary>
        /// The base camp campfire was going to spawn them in alive by itself. We stay out of the
        /// way for those - no ghost, no teleport, no Curse - and only hand over a pack.
        /// </summary>
        public bool CampfireSpawn;

        /// <summary>Set once we've forced them into the ghost state, so we don't spam RPCs.</summary>
        public bool GhostForced;

        public bool Completing;
    }

    /// <summary>
    /// Host-side brain. Watches for players who join a run that's already underway, holds them
    /// as ghosts, and drops them next to the lowest living scout as soon as that scout is
    /// standing somewhere sane.
    ///
    /// Every client tracks pending joiners (so the mod survives host migration onto another
    /// modded player), but only the master client acts on them.
    /// </summary>
    internal class LateJoinDirector : MonoBehaviour, IInRoomCallbacks
    {
        internal static LateJoinDirector Instance { get; private set; }

        private readonly Dictionary<int, PendingJoin> _pending = new Dictionary<int, PendingJoin>();
        private readonly List<PendingJoin> _scratch = new List<PendingJoin>();
        private float _nextPoll;

        private void Awake()
        {
            Instance = this;
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- Photon room callbacks

        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (newPlayer == null || newPlayer.IsLocal) return;
            if (!GameHandler.IsOnIsland || !GameHandler.PlayersHaveLeftShore)
            {
                // Run hasn't started climbing yet - the game spawns them on the shore with
                // everyone else, which is exactly where they should be.
                return;
            }

            if (!WaitConfig.IncludeReconnectingPlayers.Value && HasReconnectData(newPlayer))
            {
                Plugin.Log.LogInfo($"{newPlayer.NickName} is reconnecting to this run - leaving them to the game.");
                return;
            }

            bool campfire = CampfireWillSpawnThemIn();

            _pending[newPlayer.ActorNumber] = new PendingJoin
            {
                ActorNumber = newPlayer.ActorNumber,
                Nickname = newPlayer.NickName,
                JoinedAt = Time.time,
                CharacterSeenAt = -1f,
                CampfireSpawn = campfire,
            };

            Plugin.Log.LogInfo(campfire
                ? $"{newPlayer.NickName} joined a run in progress, but the base camp campfire spawns them in - leaving that alone."
                : $"{newPlayer.NickName} joined a run in progress - holding them until the lowest scout is safe.");
        }

        /// <summary>
        /// The same test <see cref="CharacterSpawner"/> uses to decide whether a fresh joiner
        /// gets revived at the base camp on arrival. When it's true the game already puts them
        /// into the run alive, so there's nothing for this mod to fix.
        /// </summary>
        private static bool CampfireWillSpawnThemIn()
        {
            return GameHandler.IsOnIsland
                && MapHandler.BaseCampHasRevived
                && MapHandler.LastSeenCampfireIsSafe;
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (otherPlayer == null) return;
            if (_pending.Remove(otherPlayer.ActorNumber))
                Plugin.Log.LogInfo($"{otherPlayer.NickName} left before we could place them.");
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient) { }
        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) { }

        private static bool HasReconnectData(Photon.Realtime.Player player)
        {
            // Only the host keeps reconnect records; on other clients this is simply false,
            // which is fine - they aren't acting on pending joins anyway.
            return ReconnectHandler.TryGetReconnectData(player, out _, out _);
        }

        // ---------------------------------------------------------------------------- main loop

        private void Update()
        {
            if (!PhotonNetwork.InRoom || !GameHandler.IsOnIsland)
            {
                if (_pending.Count > 0) _pending.Clear();
                return;
            }
            if (_pending.Count == 0) return;
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + Mathf.Max(0.05f, WaitConfig.PollInterval.Value);

            if (!PhotonNetwork.IsMasterClient) return; // clients only keep the bookkeeping

            Tick();
        }

        private void Tick()
        {
            // Recomputed every tick, so if the lowest scout dies (or someone drops below them)
            // the joiner simply starts waiting on whoever the lowest scout is now.
            Character lowest = FindLowest();

            _scratch.Clear();
            _scratch.AddRange(_pending.Values);

            foreach (PendingJoin p in _scratch)
            {
                if (p.Completing) continue;

                if (p.Character == null)
                {
                    if (PlayerHandler.TryGetCharacter(p.ActorNumber, out Character c) && c != null)
                    {
                        p.Character = c;
                        p.CharacterSeenAt = Time.time;
                    }
                    else
                    {
                        if (Time.time - p.JoinedAt > WaitConfig.SpawnTimeoutSeconds.Value)
                        {
                            Plugin.Log.LogWarning($"{p.Nickname}'s character never showed up - giving up on them.");
                            _pending.Remove(p.ActorNumber);
                        }
                        continue;
                    }
                }

                // Let the game's own late-join spawn routine finish before we touch anything.
                if (Time.time - p.CharacterSeenAt < WaitConfig.SettleSeconds.Value) continue;

                if (p.CampfireSpawn)
                {
                    HandleCampfireJoiner(p);
                    continue;
                }

                if (WaitConfig.GhostWhileWaiting.Value && !p.Character.data.dead)
                    MakeGhost(p);

                if (lowest == null) continue; // nobody alive to go to; stay a ghost

                if (WaitConfig.ForceSpectateTarget.Value)
                    SpectateOverride.PushTarget(p.ActorNumber, lowest);

                // No timeout and no second-choice target: the joiner waits for the lowest scout
                // for as long as it takes. If that scout never reaches anywhere standable they
                // will eventually die, and then someone else is the lowest scout.
                if (!Arrival.IsStandable(lowest, out Vector3 ground)) continue;

                p.Completing = true;
                StartCoroutine(PlaceJoiner(p, lowest, ground));
            }
        }

        /// <summary>
        /// The campfire put them into the run by itself, so the only thing left to do is the
        /// pack. Wait until the game's revive has finished (it drops everything you're carrying
        /// on the way through, backpack included) before handing anything over.
        /// </summary>
        private void HandleCampfireJoiner(PendingJoin p)
        {
            if (p.Character.data.dead || p.Character.warping)
            {
                if (Time.time - p.JoinedAt > WaitConfig.SpawnTimeoutSeconds.Value)
                {
                    Plugin.Log.LogInfo($"{p.Nickname} never came round after the campfire spawn - " +
                                       "leaving them to the game.");
                    _pending.Remove(p.ActorNumber);
                }
                return;
            }

            JoinPack.Grant(p.Character);
            _pending.Remove(p.ActorNumber);
        }

        /// <summary>The lowest living scout, ignoring anyone we're still holding.</summary>
        private Character FindLowest()
        {
            Character best = null;
            float bestY = float.MaxValue;

            foreach (Character c in PlayerHandler.GetAllPlayerCharacters())
            {
                if (c == null || c.isBot || c.isScoutmaster) continue;
                if (c.data.dead || c.data.fullyPassedOut) continue;

                PhotonView view = c.photonView;
                if (view == null || view.Owner == null) continue;
                if (_pending.ContainsKey(view.Owner.ActorNumber)) continue;

                float y = c.Center.y;
                if (y < bestY)
                {
                    bestY = y;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>
        /// Put a joiner into the plain dead state - ghost, spectator camera - without the noise
        /// of an actual death (no skeleton, no dropped loot, no end-of-run check). Their body
        /// gets dragged off to the death zone by <c>Character.FixedUpdate</c> on its own, the
        /// same as any other corpse.
        /// </summary>
        private void MakeGhost(PendingJoin p)
        {
            Character c = p.Character;
            c.photonView.RPC("RPCA_SetDead", RpcTarget.All);
            if (!p.GhostForced)
            {
                p.GhostForced = true;
                Plugin.Log.LogInfo($"{p.Nickname} is waiting as a ghost.");
            }
        }

        private IEnumerator PlaceJoiner(PendingJoin p, Character target, Vector3 groundPoint)
        {
            Character c = p.Character;
            Vector3 pos = Arrival.PickArrivalPosition(groundPoint);

            Plugin.Log.LogInfo($"Dropping {p.Nickname} next to {target.characterName} at {pos}.");
            c.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, pos, false,
                             (int)MapHandler.CurrentSegmentNumber);

            yield return new WaitForSeconds(Mathf.Max(0f, WaitConfig.PostReviveDelay.Value));

            if (c != null)
            {
                JoinCurse.Apply(c);
                JoinPack.Grant(c);
            }

            SpectateOverride.ClearTarget(p.ActorNumber);
            _pending.Remove(p.ActorNumber);
        }
    }
}
