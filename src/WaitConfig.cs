using BepInEx.Configuration;

namespace WaitForMEak
{
    /// <summary>What a late-joining scout is handed once they arrive.</summary>
    public enum PackOnJoin
    {
        /// <summary>Nothing. (default)</summary>
        Off,

        /// <summary>Every late joiner is given a fresh fanny pack.</summary>
        AlwaysFannypack,

        /// <summary>
        /// Only if a backpack or fanny pack is lying abandoned on the ground somewhere in the
        /// run — that pack (contents and all) is handed to the joiner. Backpacks win over
        /// fanny packs.
        /// </summary>
        OnlyIfLeftBehind,
    }

    /// <summary>
    /// All tunables, bound to BepInEx config.
    ///
    /// The two settings the mod is actually about are plain entries, so PEAKLib.ModConfig's
    /// in-game Mod Settings menu shows them (the enum becomes a dropdown). Everything else is
    /// tagged "Hidden" so the menu stays clean while the values remain editable in the .cfg.
    /// The tag is a plain BepInEx description tag, so this needs no reference to (or
    /// dependency on) ModConfig — without it installed the mod behaves exactly the same.
    ///
    /// ModConfig labels a setting with its config KEY verbatim (BepInExOffOn/BepInExEnum
    /// .GetDisplayName() returns Definition.Key; only the mod's own tab name gets camelCase
    /// split). So the two visible keys are written as readable, spaced sentences — BepInEx only
    /// rejects = \n \t \ " ' [ ] in keys, spaces are fine and other PEAK mods do the same.
    /// Dropdown choices, on the other hand, are Enum.GetNames() verbatim and can't be spaced:
    /// the only alternative ModConfig offers (a string entry with an AcceptableValueList) hits
    /// a `return` instead of `continue` in its ProcessModEntries loop, which would silently drop
    /// every setting registered after ours — including other mods'. Not worth it.
    /// </summary>
    internal static class WaitConfig
    {
        /// <summary>Description that ModConfig's menu skips over.</summary>
        private static ConfigDescription Hidden(string description) =>
            new ConfigDescription(description, null, "Hidden");

        // --- Shown in the in-game Mod Settings menu ---
        public static ConfigEntry<bool> CurseAsIfRevived;
        public static ConfigEntry<PackOnJoin> PackForJoiners;

        // --- Config-file only ---
        public static ConfigEntry<bool> IncludeReconnectingPlayers;
        public static ConfigEntry<bool> GhostWhileWaiting;
        public static ConfigEntry<bool> ForceSpectateTarget;

        public static ConfigEntry<float> GroundedSecondsRequired;
        public static ConfigEntry<float> MaxGroundSlope;
        public static ConfigEntry<float> MaxTargetSpeed;
        public static ConfigEntry<float> ArrivalOffset;
        public static ConfigEntry<float> FallbackAfterSeconds;

        public static ConfigEntry<float> SettleSeconds;
        public static ConfigEntry<float> SpawnTimeoutSeconds;
        public static ConfigEntry<float> PostReviveDelay;
        public static ConfigEntry<float> PollInterval;

        public static void Bind(ConfigFile cfg)
        {
            CurseAsIfRevived = cfg.Bind("General", "Curse as if revived", false,
                "Late joiners start with the same amount of Curse a revive would have given them " +
                "(0.05, or 0.15 on Ascent 7+). The Ascent 7/8 starting Curse is applied either way — " +
                "this is the extra revival Curse on top of it. Doesn't apply to joiners the base " +
                "camp campfire spawns in by itself; the game handles their Curse.");

            PackForJoiners = cfg.Bind("General", "Pack for late joiners", PackOnJoin.Off,
                "What a late joiner gets once they arrive — campfire spawn or not.\n" +
                "Off: nothing.\n" +
                "AlwaysFannypack: a fresh fanny pack every time.\n" +
                "OnlyIfLeftBehind: only if a backpack or fanny pack is lying abandoned on the ground " +
                "somewhere in the run — that pack (and everything in it) is handed to them. " +
                "Backpacks are preferred over fanny packs.");

            // Everything below is hidden from the in-game menu (still editable here).
            IncludeReconnectingPlayers = cfg.Bind("General", "IncludeReconnectingPlayers", false,
                Hidden("Also grab players who are RE-connecting to a run they were already part of. " +
                       "Off by default: the game restores those players to where they left off, and " +
                       "yanking them to the lowest scout would undo that."));
            GhostWhileWaiting = cfg.Bind("General", "GhostWhileWaiting", true,
                Hidden("Keep joiners dead (ghost/spectator) until the lowest scout is somewhere safe " +
                       "to drop them. Off: they are left wherever the game spawned them and are just " +
                       "teleported when the time comes."));
            ForceSpectateTarget = cfg.Bind("General", "ForceSpectateTarget", true,
                Hidden("While waiting, ask the joiner's game to spectate the scout they're going to be " +
                       "dropped on. Only does anything if that player also has this mod installed — " +
                       "the spectator camera is picked entirely on their own machine."));

            GroundedSecondsRequired = cfg.Bind("Arrival", "GroundedSecondsRequired", 1f,
                Hidden("How long the target has to have been standing on the ground before we drop " +
                       "the joiner on them. Stops arrivals mid-hop."));
            MaxGroundSlope = cfg.Bind("Arrival", "MaxGroundSlope", 45f,
                Hidden("Steepest ground (degrees) that still counts as a standable place."));
            MaxTargetSpeed = cfg.Bind("Arrival", "MaxTargetSpeed", 6f,
                Hidden("If the target is moving faster than this (m/s) they don't count as settled " +
                       "— catches sliding, being launched, and rocket rides."));
            ArrivalOffset = cfg.Bind("Arrival", "ArrivalOffset", 1.5f,
                Hidden("How far to the side of the target the joiner lands, in metres."));
            FallbackAfterSeconds = cfg.Bind("Arrival", "FallbackAfterSeconds", 90f,
                Hidden("If the lowest scout never reaches a standable place within this many seconds, " +
                       "drop the joiner on the lowest scout who IS standing somewhere. 0 = wait forever."));

            SettleSeconds = cfg.Bind("Timing", "SettleSeconds", 2.5f,
                Hidden("Grace period after a joiner's character appears before this mod touches it, " +
                       "so the game's own late-join spawn routine can finish first."));
            SpawnTimeoutSeconds = cfg.Bind("Timing", "SpawnTimeoutSeconds", 90f,
                Hidden("Give up on a joiner whose character never shows up within this many seconds."));
            PostReviveDelay = cfg.Bind("Timing", "PostReviveDelay", 1f,
                Hidden("Seconds to wait after the revive before applying Curse and handing over a pack."));
            PollInterval = cfg.Bind("Timing", "PollInterval", 0.25f,
                Hidden("Seconds between checks while joiners are waiting."));
        }
    }
}
