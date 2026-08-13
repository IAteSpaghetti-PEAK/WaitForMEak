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
        /// Only if a backpack or fanny pack is lying abandoned on the ground somewhere in the run.
        /// That pack, contents and all, is handed to the joiner. Backpacks win over fanny packs.
        /// </summary>
        OnlyIfLeftBehind,
    }

    /// <summary>
    /// All tunables, bound to BepInEx config.
    ///
    /// The settings the mod is actually about are plain entries, so PEAKLib.ModConfig's in-game
    /// Mod Settings menu shows them (the enum becomes a dropdown). Everything else is tagged
    /// "Hidden" so the menu stays clean while the values remain editable in the .cfg. The tag is
    /// a plain BepInEx description tag, so this needs no reference to (or dependency on)
    /// ModConfig. Without it installed the mod behaves exactly the same.
    ///
    /// ModConfig labels a setting with its config KEY verbatim (BepInExOffOn/BepInExEnum
    /// .GetDisplayName() returns Definition.Key; only the mod's own tab name gets camelCase
    /// split). The visible keys are therefore written as readable, spaced sentences. BepInEx only
    /// rejects = \n \t \ " ' [ ] in keys, so spaces are fine, and other PEAK mods do the same.
    ///
    /// Dropdown choices are a different story. They come back from Enum.GetNames() verbatim and
    /// cannot be spaced. The one alternative ModConfig offers, a string entry with an
    /// AcceptableValueList, hits a `return` where `continue` was meant in its ProcessModEntries
    /// loop. That would silently drop every setting registered after ours, other mods' included.
    /// </summary>
    internal static class WaitConfig
    {
        /// <summary>Description that ModConfig's menu skips over.</summary>
        private static ConfigDescription Hidden(string description) =>
            new ConfigDescription(description, null, "Hidden");

        // Shown in the in-game Mod Settings menu. All of them live in the [General] section so
        // they land on one section tab instead of being split across two. That leaves [Arrival]
        // and [Timing] as purely the tuning knobs.
        public static ConfigEntry<bool> CurseAsIfRevived;
        public static ConfigEntry<PackOnJoin> PackForJoiners;
        public static ConfigEntry<float> GroundedSecondsRequired;
        public static ConfigEntry<bool> IncludeReconnectingPlayers;

        // Config-file only.
        public static ConfigEntry<bool> GhostWhileWaiting;
        public static ConfigEntry<bool> TellJoinersWhoIsLowest;

        public static ConfigEntry<float> MaxGroundSlope;
        public static ConfigEntry<float> MaxTargetSpeed;
        public static ConfigEntry<float> ArrivalOffset;

        public static ConfigEntry<float> SettleSeconds;
        public static ConfigEntry<float> SpawnTimeoutSeconds;
        public static ConfigEntry<float> PostReviveDelay;
        public static ConfigEntry<float> PollInterval;

        public static void Bind(ConfigFile cfg)
        {
            CurseAsIfRevived = cfg.Bind("General", "Curse as if revived", false,
                "Late joiners start with the same amount of Curse a revive would have given them " +
                "(0.05, or 0.15 on Ascent 7+). Off by default, so joining a run in progress costs " +
                "nothing. Turn it on if you'd rather it cost what a revive costs, since being put " +
                "down next to the group is a revive in all but name and the group would have paid " +
                "that Curse to bring anyone else back.\n" +
                "The Ascent 7/8 starting Curse is applied either way, so this is the revival " +
                "Curse on top of it. Neither applies to joiners the base camp campfire spawns in " +
                "by itself, or to reconnecting players; the game handles the first and the mod " +
                "leaves the second's own Curse alone.");

            PackForJoiners = cfg.Bind("General", "Pack for late joiners", PackOnJoin.Off,
                "What a late joiner gets once they arrive, campfire spawn or not.\n" +
                "Off: nothing.\n" +
                "AlwaysFannypack: a fresh fanny pack every time.\n" +
                "OnlyIfLeftBehind: only if a backpack or fanny pack is lying abandoned on the " +
                "ground somewhere in the run. That pack, and everything in it, is handed to them. " +
                "Backpacks are preferred over fanny packs.");

            // A slider, so it needs an explicit range. ModConfig falls back to 0-1000 for a float
            // with no AcceptableValueRange, which would make this one unusable.
            GroundedSecondsRequired = cfg.Bind("General", "Seconds the scout must be standing", 1f,
                new ConfigDescription(
                    "How long the lowest scout has to have been standing on the ground before a " +
                    "waiting joiner is dropped next to them. Stops arrivals mid-hop. Raise it if " +
                    "joiners keep landing on someone who was only briefly on solid ground.",
                    new AcceptableValueRange<float>(0f, 5f)));

            IncludeReconnectingPlayers = cfg.Bind("General", "Also move reconnecting players", false,
                "Also hold and move players who are RE-connecting to a run they were already part " +
                "of. Off by default: the game restores those players to where they left off, and " +
                "hauling them down to the lowest scout would undo that. When it is on they keep " +
                "their items, backpack and statuses through the hold.");

            // Everything below is hidden from the in-game menu (still editable here).
            GhostWhileWaiting = cfg.Bind("General", "GhostWhileWaiting", true,
                Hidden("Keep joiners dead (ghost/spectator) until the lowest scout is somewhere safe " +
                       "to drop them. Off: they are left wherever the game spawned them and are just " +
                       "teleported when the time comes."));
            TellJoinersWhoIsLowest = cfg.Bind("General", "TellJoinersWhoIsLowest", true,
                Hidden("While a joiner waits, show them who the lowest scout is on the spectate " +
                       "panel, under whoever they're currently watching. Their camera is left " +
                       "alone; they can switch to that scout themselves if they want. Only does " +
                       "anything if that player also has this mod installed."));

            MaxGroundSlope = cfg.Bind("Arrival", "MaxGroundSlope", 45f,
                Hidden("Steepest ground (degrees) that still counts as a standable place."));
            MaxTargetSpeed = cfg.Bind("Arrival", "MaxTargetSpeed", 6f,
                Hidden("If the target is moving faster than this (m/s) they don't count as settled. " +
                       "Catches sliding, being launched, and rocket rides."));
            ArrivalOffset = cfg.Bind("Arrival", "ArrivalOffset", 1.5f,
                Hidden("How far to the side of the target the joiner lands, in metres."));

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
