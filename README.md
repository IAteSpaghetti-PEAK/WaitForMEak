# Wait For MEak

Late joiners catch up with the group instead of restarting at the base camp.

When someone joins a run that's already underway, PEAK drops them at the current base camp:
sometimes dead, sometimes revived, always a long way behind everyone else. This mod holds them
as a ghost and then puts them down **next to the lowest living scout**, the moment that scout is
standing somewhere sane.

**Only the host needs this mod.** Everything is driven by the master client through the game's
own RPCs, so the people joining can be running vanilla.

## ModConfig is optional

[ModConfig](https://thunderstore.io/c/peak/p/PEAKModding/ModConfig/) is listed as a dependency, but
it is **not required**. The mod does exactly the same thing with or without it. What it adds is a
**WAITFORMEAK** tab in the in-game mod settings menu, so you can turn the Curse, the pack rules
and the rest on and off from the pause screen, mid-run, instead of alt-tabbing out to edit a text
file. Skip it if you'd rather not install anything extra: every setting is still there in
`BepInEx/config/com.iatespaghetti.waitformeak.cfg`, and the ones that don't fit in the menu live
only in that file anyway.

## What happens

1. A player joins a run that has already left the shore.
2. They're held as a ghost: dead, spectating, no body in the world.
3. The mod watches the lowest living scout. If that scout is climbing, dangling, sliding or
   airborne, it waits.
4. The first time they're standing on solid, non-vertical ground for a moment, the joiner is
   revived right beside them.

There's no timeout and no second-choice target. The joiner waits on the lowest scout for as long
as it takes. Nobody gets stranded by that: if the lowest scout never reaches anywhere standable
they'll die eventually, and then someone else is the lowest scout.

If a held joiner drops out before they've been placed, the mod remembers them. Rejoin and the hold
picks up where it left off, rather than the game quietly restoring them to base camp.

**When the base camp campfire is already spawning joiners in alive, the mod stays out of the way.**
No ghost, no teleport, no Curse. The game has already put them into the run, so there's nothing to
fix. The pack rules below still apply either way.

## Settings

These four are the ones on the **WAITFORMEAK** menu tab (and, as ever, in the config file):

| Setting | Default | What it does |
| --- | --- | --- |
| **Curse as if revived** | Off | Joiners arrive with the Curse a revive would have cost them (0.05, or 0.15 on Ascent 7+). |
| **Pack for late joiners** | Off | `Off`: nothing.<br>`AlwaysFannypack`: a fresh fanny pack every time.<br>`OnlyIfLeftBehind`: only if a backpack or fanny pack is lying abandoned on the ground somewhere in the run. That pack, and everything inside it, is handed over. Backpacks win over fanny packs. |
| **Seconds the scout must be standing** | 1 s | How long the lowest scout has to have been on solid ground before a joiner is dropped next to them. Raise it if joiners keep landing on someone who'd only just touched down. |
| **Also move reconnecting players** | Off | Whether someone rejoining a run they were already in is held and moved too. Off because the game restores those players to where they left off, and hauling them down to the lowest scout would throw that away. See below. |

The **Ascent 7/8 starting Curse is always applied**. That's the Ascent's rule rather than the
mod's, so there's no toggle for it. *Curse as if revived* is the revival Curse on top of that.

Everything else lives in the config file under the `General`, `Arrival` and `Timing` sections: how
steep is too steep, how far to the side joiners land, whether they're held as ghosts at all, and
the various grace periods.

### Reconnecting players keep everything

With *Also move reconnecting players* switched on, being held costs a returning player nothing but
time. They keep their items, their backpack and their statuses, and they keep their own Curse
instead of being given a joiner's. Only their position changes.

Two things had to be worked around for that. The normal revive drops everything you're carrying,
and a held player's body sits in the death zone, so their loot would have landed somewhere
unreachable. Reviving them also clears every status, which would have made leaving and rejoining a
free cure-all. Joiners who really are new still take the ordinary path.

### Spectating while waiting

While a joiner is waiting, the host tells their game to watch the scout they're going to land on.
The spectator camera is chosen entirely on the spectating player's own machine, so this only does
anything if that player *also* has the mod installed. It's a bonus, not a requirement. Vanilla
clients ignore the message and spectate whoever they like.

## Building

Requires the .NET 8 SDK and a PEAK install. The project references the game DLLs directly:

```bash
dotnet build WaitForMEak.csproj -c Release
```

The built DLL is copied into `BepInEx/plugins/WaitForMEak` automatically. Pass
`-p:SkipDeploy=true` to skip that. Override the game path with `-p:GameDir=...` or a
`WaitForMEak.csproj.user` file.
