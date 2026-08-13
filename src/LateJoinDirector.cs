using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>What we've settled on doing with a joiner.</summary>
    internal enum HoldDecision
    {
        /// <summary>Still working out which of the two below applies.</summary>
        Undecided,

        /// <summary>The game put them into the run itself. Hands off, apart from the pack.</summary>
        LeaveToTheGame,

        /// <summary>Ours to hold and place next to the lowest scout.</summary>
        Hold,
    }

    /// <summary>One scout who joined mid-run and hasn't been dealt with yet.</summary>
    internal class PendingJoin
    {
        public int ActorNumber;
        public string UserId;
        public string Nickname;
        public float JoinedAt;

        public Character Character;
        public float CharacterSeenAt;

        /// <summary>They were already part of this run before they joined it again.</summary>
        public bool IsReconnect;

        /// <summary>We were already holding them when they dropped out, and now they're back.</summary>
        public bool Resumed;

        /// <summary>
        /// The hold must not cost them anything, because they were already in the run. See
        /// <see cref="HeldBelongings"/>. Only ever set when "Also move reconnecting players" is on.
        /// </summary>
        public bool PreserveBelongings;

        public HoldDecision Decision;

        /// <summary>Set once we've forced them into the ghost state, so we don't spam RPCs.</summary>
        public bool GhostForced;

        public bool Completing;
    }

    /// <summary>
    /// Host-side brain. Watches for players who join a run that's already underway, holds them
    /// as ghosts, and drops them next to the lowest living scout as soon as that scout is
    /// standing somewhere sane.
    ///
    /// Every client keeps the same bookkeeping and only the master client acts on it, so if the
    /// host leaves and another player running the mod inherits the room, held joiners aren't
    /// abandoned as permanent ghosts. That works because the inputs are the same everywhere:
    /// room callbacks fire on all clients, and <see cref="ReconnectHandler"/> builds its records
    /// on every client, not just the host.
    /// </summary>
    internal class LateJoinDirector : MonoBehaviour, IInRoomCallbacks
    {
        internal static LateJoinDirector Instance { get; private set; }

        private readonly Dictionary<int, PendingJoin> _pending = new Dictionary<int, PendingJoin>();
        private readonly List<PendingJoin> _scratch = new List<PendingJoin>();

        /// <summary>
        /// People who dropped out while we were still holding them, keyed by user id because
        /// actor numbers change on rejoin. The value is their
        /// <see cref="PendingJoin.PreserveBelongings"/> flag. Without this they'd come back
        /// carrying reconnect data that says "dead at base camp", the reconnect check would wave
        /// them through, and they'd be stranded there having never been taken to anyone.
        /// </summary>
        private readonly Dictionary<string, bool> _interrupted = new Dictionary<string, bool>();

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
                // Run hasn't started climbing yet. The game spawns them on the shore with
                // everyone else, which is exactly where they should be.
                return;
            }

            string userId = newPlayer.UserId;
            bool wasPreserving = false;
            bool resuming = userId != null && _interrupted.TryGetValue(userId, out wasPreserving);
            if (resuming) _interrupted.Remove(userId);

            bool reconnecting = HasReconnectData(newPlayer);

            if (!resuming && !WaitConfig.IncludeReconnectingPlayers.Value && reconnecting)
            {
                Plugin.Log.LogInfo($"{newPlayer.NickName} is reconnecting to this run - leaving them to the game.");
                return;
            }

            _pending[newPlayer.ActorNumber] = new PendingJoin
            {
                ActorNumber = newPlayer.ActorNumber,
                UserId = userId,
                Nickname = newPlayer.NickName,
                JoinedAt = Time.time,
                CharacterSeenAt = -1f,
                IsReconnect = resuming ? wasPreserving : reconnecting,
                Resumed = resuming,
                PreserveBelongings = resuming ? wasPreserving : reconnecting,
                Decision = HoldDecision.Undecided,
            };

            Plugin.Log.LogInfo(resuming
                ? $"{newPlayer.NickName} is back after dropping out mid-hold - picking up where we left off."
                : $"{newPlayer.NickName} joined a run in progress - working out what to do with them.");
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (otherPlayer == null) return;
            LowestScoutNotice.Forget(otherPlayer.ActorNumber);

            if (!_pending.TryGetValue(otherPlayer.ActorNumber, out PendingJoin p)) return;
            _pending.Remove(otherPlayer.ActorNumber);

            // Remember the ones we were actually holding, so rejoining resumes the hold instead
            // of dumping them at base camp as an ordinary reconnect.
            if (!p.Completing && p.Decision == HoldDecision.Hold && p.UserId != null)
            {
                _interrupted[p.UserId] = p.PreserveBelongings;
                Plugin.Log.LogInfo($"{otherPlayer.NickName} left mid-hold - we'll pick it back up if they return.");
            }
            else
            {
                Plugin.Log.LogInfo($"{otherPlayer.NickName} left before we could place them.");
            }
        }

        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (_pending.Count == 0) return;
            Plugin.Log.LogInfo($"Inherited the room with {_pending.Count} joiner(s) still in hand. Carrying on.");
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) { }

        /// <summary>
        /// Whether the game has a record of this player from earlier in the run. Every client
        /// builds these records, not just the host: ReconnectHandler.Update registers every
        /// player it sees and keeps their record current. That's what lets the bookkeeping here
        /// survive the host handing off.
        /// </summary>
        private static bool HasReconnectData(Photon.Realtime.Player player)
        {
            return ReconnectHandler.TryGetReconnectData(player, out _, out _);
        }

        /// <summary>
        /// Whether the base camp campfire has gathered the group. This is the game's own test for
        /// whether a returning scout gets brought back to the current base camp, so when it's true
        /// a reconnecting player ends up standing with everyone else and there's nothing to fix.
        /// </summary>
        private static bool CampfireGathersTheGroup()
        {
            if (!GameHandler.IsOnIsland) return false;
            return CharacterSpawner.ScoutsWereRevivedAtCurrentBaseCamp;
        }

        // ---------------------------------------------------------------------------- main loop

        private void Update()
        {
            if (!PhotonNetwork.InRoom || !GameHandler.IsOnIsland)
            {
                if (_pending.Count > 0) _pending.Clear();
                if (_interrupted.Count > 0) _interrupted.Clear();
                return;
            }
            if (_pending.Count == 0) return;
            if (Time.time < _nextPoll) return;
            _nextPoll = Time.time + Mathf.Max(0.05f, WaitConfig.PollInterval.Value);

            if (!PhotonNetwork.IsMasterClient) return; // clients only keep the bookkeeping

            Arrival.ForgetDeadEntries();
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
                if (!ResolveCharacter(p)) continue;

                if (p.Decision == HoldDecision.Undecided)
                {
                    Decide(p);
                    if (p.Decision == HoldDecision.Undecided) continue;
                }

                if (p.Decision == HoldDecision.LeaveToTheGame)
                {
                    HandleGamesJoiner(p);
                    continue;
                }

                if (WaitConfig.GhostWhileWaiting.Value && !p.Character.data.dead)
                    MakeGhost(p);

                if (lowest == null) continue; // nobody alive to go to; stay a ghost

                if (WaitConfig.TellJoinersWhoIsLowest.Value)
                    LowestScoutNotice.Push(p.ActorNumber, lowest);

                // No timeout and no second-choice target: the joiner waits for the lowest scout
                // for as long as it takes. If that scout never reaches anywhere standable they
                // will eventually die, and then someone else is the lowest scout.
                if (!Arrival.IsStandable(lowest, out Vector3 ground)) continue;

                p.Completing = true;
                StartCoroutine(PlaceJoiner(p, lowest, ground));
            }
        }

        /// <summary>False while we're still waiting for their character to turn up.</summary>
        private bool ResolveCharacter(PendingJoin p)
        {
            if (p.Character != null) return true;

            if (PlayerHandler.TryGetCharacter(p.ActorNumber, out Character c) && c != null)
            {
                p.Character = c;
                p.CharacterSeenAt = Time.time;
                return true;
            }

            if (Time.time - p.JoinedAt > WaitConfig.SpawnTimeoutSeconds.Value)
            {
                Plugin.Log.LogWarning($"{p.Nickname}'s character never showed up - giving up on them.");
                _pending.Remove(p.ActorNumber);
            }
            return false;
        }

        /// <summary>
        /// Work out whether this joiner is ours to place or the game's to handle.
        ///
        /// For someone genuinely new, this watches what actually happened rather than predicting
        /// it. The game makes that call inside CharacterSpawner up to two seconds after they join,
        /// behind a retry cooldown, so anything that lights or leaves a campfire in the meantime
        /// used to make us disagree with it and leave a joiner stranded. Coming up alive means the
        /// game placed them; still dead once the settle period is up means they're ours.
        /// </summary>
        private void Decide(PendingJoin p)
        {
            bool settled = Time.time - p.CharacterSeenAt >= WaitConfig.SettleSeconds.Value;

            if (p.Resumed)
            {
                if (!settled) return;
                p.Decision = HoldDecision.Hold;
                Plugin.Log.LogInfo($"Resuming the hold on {p.Nickname}.");
                return;
            }

            if (p.IsReconnect)
            {
                if (!settled) return;
                bool gathered = CampfireGathersTheGroup();
                p.Decision = gathered ? HoldDecision.LeaveToTheGame : HoldDecision.Hold;
                Plugin.Log.LogInfo(gathered
                    ? $"{p.Nickname} is reconnecting and the base camp campfire has gathered the group, so they come back to it with everyone else."
                    : $"{p.Nickname} is reconnecting with no campfire to come back to - holding them for the lowest scout.");
                return;
            }

            if (!p.Character.data.dead && !p.Character.warping)
            {
                p.Decision = HoldDecision.LeaveToTheGame;
                Plugin.Log.LogInfo($"The game brought {p.Nickname} into the run itself - leaving that alone.");
                return;
            }

            if (!settled) return;

            p.Decision = HoldDecision.Hold;
            Plugin.Log.LogInfo($"{p.Nickname} was left dead on arrival - holding them until the lowest scout is safe.");
        }

        /// <summary>
        /// The game put them into the run, so the only thing left is the pack. Wait until its
        /// revive has finished (it drops everything you're carrying on the way through, backpack
        /// included) before handing anything over.
        /// </summary>
        private void HandleGamesJoiner(PendingJoin p)
        {
            if (p.Character.data.dead || p.Character.warping)
            {
                if (Time.time - p.JoinedAt > WaitConfig.SpawnTimeoutSeconds.Value)
                {
                    Plugin.Log.LogInfo($"{p.Nickname} never came round - leaving them to the game.");
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
                if (IsBeingHeld(view.Owner.ActorNumber)) continue;

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
        /// Joiners the game placed itself are ordinary scouts as far as everyone else is
        /// concerned, so they can be the lowest one. Only the ones we're holding are excluded.
        /// </summary>
        private bool IsBeingHeld(int actorNumber)
        {
            return _pending.TryGetValue(actorNumber, out PendingJoin p)
                && p.Decision != HoldDecision.LeaveToTheGame;
        }

        /// <summary>
        /// Put a joiner into the plain dead state (ghost, spectator camera) without the noise of
        /// an actual death: no skeleton, no dropped loot, no end-of-run check. Their body gets
        /// dragged off to the death zone by <c>Character.FixedUpdate</c> on its own, the same as
        /// any other corpse.
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

            // Never make someone drop what they're carrying just because we moved them. That
            // matters for reconnecting players, and for anyone who picked something up while
            // waiting with GhostWhileWaiting switched off.
            bool keepItems = p.PreserveBelongings || HeldBelongings.IsCarryingAnything(c);

            float[] statuses = p.PreserveBelongings ? HeldBelongings.SnapshotStatuses(c) : null;

            if (keepItems)
            {
                HeldBelongings.Revive(c, pos);
            }
            else
            {
                c.photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, pos, false,
                                 (int)MapHandler.CurrentSegmentNumber);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, WaitConfig.PostReviveDelay.Value));

            if (c != null)
            {
                if (p.PreserveBelongings)
                {
                    // Their own Curse comes back with the rest of their statuses. The join Curse
                    // is for people arriving fresh, not for someone we interrupted.
                    HeldBelongings.RestoreStatuses(c, statuses);
                }
                else
                {
                    JoinCurse.Apply(c);
                }
                JoinPack.Grant(c);
            }

            LowestScoutNotice.Clear(p.ActorNumber);
            _pending.Remove(p.ActorNumber);
        }
    }
}
