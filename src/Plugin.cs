using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WaitForMEak
{
    /// <summary>
    /// Wait For MEak - late joiners catch up with the group instead of the base camp.
    ///
    /// Someone joining a run that's already underway is held as a ghost and then dropped next
    /// to the lowest living scout, as soon as that scout is standing somewhere sane (so nobody
    /// materialises on top of a player mid-climb). Optionally they arrive carrying the Curse a
    /// revive would have cost them, and/or a pack.
    ///
    /// HOST ONLY: everything is driven by the master client through vanilla RPCs, so the people
    /// joining don't need the mod. (If they do have it, they also get their spectator camera
    /// pinned to the scout they're waiting on.)
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.iatespaghetti.waitformeak";
        public const string PluginName = "WaitForMEak";
        public const string PluginVersion = "0.1.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        private Harmony _harmony;
        private GameObject _director;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            WaitConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            SpectateOverride.Register();

            _director = new GameObject(PluginName + "Director");
            _director.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(_director);
            _director.AddComponent<LateJoinDirector>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Only the host needs this.");
        }

        private void OnDestroy()
        {
            SpectateOverride.Unregister();
            if (_director != null) Destroy(_director);
            _harmony?.UnpatchSelf();
        }
    }
}
