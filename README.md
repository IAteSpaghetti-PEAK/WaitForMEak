# Wait For MEak

Late joiners catch up with the group instead of restarting at the base camp.

When someone joins a run that's already underway, PEAK drops them at the current base camp —
sometimes dead, sometimes revived, always a long way behind everyone else. This mod holds them
as a ghost and then puts them down **next to the lowest living scout**, the moment that scout is
standing somewhere sane.

**Only the host needs this mod.** Everything is driven by the master client through the game's
own RPCs, so the people joining can be running vanilla.

## What happens

1. A player joins a run that has already left the shore.
2. They're held as a ghost — dead, spectating, no body in the world.
3. The mod watches the lowest living scout. If that scout is climbing, dangling, sliding or
   airborne, it waits.
4. The first time they're standing on solid, non-vertical ground for a moment, the joiner is
   revived right beside them.

If the lowest scout is stuck somewhere unstandable for a long time (90 s by default), the joiner
is dropped on the lowest scout who *is* standing somewhere instead, so nobody waits forever.

**When the base camp campfire is already spawning joiners in alive, the mod stays out of the way** —
no ghost, no teleport, no Curse. The game has already put them into the run, so there's nothing to
fix. The pack rules below still apply either way.

## Settings

Two settings show up in the in-game mod settings menu (via
[ModConfig](https://thunderstore.io/c/peak/p/PEAKModding/PEAKLib_ModConfig/); without it they're
still editable in `BepInEx/config/com.iatespaghetti.waitformeak.cfg`):

| Setting | Default | What it does |
| --- | --- | --- |
| **CurseAsIfRevived** | Off | Joiners arrive with the Curse a revive would have cost them (0.05, or 0.15 on Ascent 7+). |
| **PackForJoiners** | Off | `Off` — nothing.<br>`AlwaysFannypack` — a fresh fanny pack every time.<br>`OnlyIfLeftBehind` — only if a backpack or fanny pack is lying abandoned on the ground somewhere in the run; that pack, and everything inside it, is handed over. Backpacks win over fanny packs. |

The **Ascent 7/8 starting Curse is always applied** — that's the Ascent's rule, not the mod's, so
there's no toggle for it. `CurseAsIfRevived` is the revival Curse *on top* of that.

Everything else (how long the target has to be grounded, how steep is too steep, the fallback
timeout, whether joiners are held as ghosts at all, reconnecting players) lives in the config file
under the `Arrival`, `Timing` and `General` sections.

### Reconnecting players

Off by default. Someone rejoining a run they were already in gets restored to where they left off
by the game, and hauling them down to the lowest scout would throw that away. Flip
`IncludeReconnectingPlayers` if you want them treated like fresh joiners.

### Spectating while waiting

While a joiner is waiting, the host tells their game to watch the scout they're going to land on.
The spectator camera is chosen entirely on the spectating player's own machine, so this only does
anything if that player *also* has the mod installed — it's a bonus, not a requirement. Vanilla
clients ignore the message and spectate whoever they like.

## Building

Requires the .NET 8 SDK and a PEAK install. The project references the game DLLs directly:

```bash
dotnet build WaitForMEak.csproj -c Release
```

The built DLL is copied into `BepInEx/plugins/WaitForMEak` automatically. Pass
`-p:SkipDeploy=true` to skip that. Override the game path with `-p:GameDir=...` or a
`WaitForMEak.csproj.user` file.
