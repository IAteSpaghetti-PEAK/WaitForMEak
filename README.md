# Wait For MEak

Late joiners catch up with the group instead of restarting at the base camp.

When someone joins a run that's already underway, PEAK drops them at the current base camp:
sometimes dead, sometimes revived, always a long way behind everyone else. This mod holds them
as a ghost and then puts them down **next to the lowest living scout**, the moment that scout is
standing somewhere sane.

**Only the host needs this mod.** Everything is driven by the master client through the game's
own RPCs, so the people joining can be running vanilla.

## If you have Reconnect Catchup installed, remove it

[Reconnect Catchup](https://thunderstore.io/c/peak/p/Ayzax/Reconnect_Catchup/) did a more basic
version of this, and this mod exists because it stopped working and was never fixed. Its only
release went up in June 2025 and has not been touched since, so it no longer works on current PEAK.

**Do not run both.** They are both host only and both act the instant somebody joins, so they end up
fighting over the same player. Reconnect Catchup teleports them immediately, this one wants to hold
them first, and which of those you get depends on which fires soonest. Uninstall it.

None of this is built on its code. WaitForMEak was written from scratch against PEAK's own
assemblies, and it goes further than the original did in several places: it waits for the lowest
scout to be standing somewhere safe rather than teleporting on arrival, it keeps working when the
host hands off to another player, and it deals with reconnecting players, Curse, packs and the
spectate hint.

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

**When the game brings a joiner into the run by itself, the mod stays out of the way.** No ghost,
no teleport, no Curse. That's mostly the base camp campfire: if it has revived the group, joiners
arrive standing with everyone else and there's nothing to fix. The mod doesn't try to predict this,
it just watches whether the joiner comes up alive. The pack rules below still apply either way.

## Settings

These four are the ones on the **WAITFORMEAK** menu tab (and, as ever, in the config file):

| Setting | Default | What it does |
| --- | --- | --- |
| **Curse as if revived** | Off | Joiners arrive with the Curse a revive would have cost them (0.05, or 0.15 on Ascent 7+). See below for why you might want it on. |
| **Pack for late joiners** | Off | `Off`: nothing.<br>`AlwaysFannypack`: a fresh fanny pack every time.<br>`OnlyIfLeftBehind`: only if a backpack or fanny pack is lying on the ground somewhere in the run. That pack, and everything inside it, is handed over. Backpacks win over fanny packs. Note that it doesn't know abandoned from parked, so a pack deliberately left at a campfire is fair game. |
| **Seconds the scout must be standing** | 1 s | How long the lowest scout has to have been on solid ground before a joiner is dropped next to them. Raise it if joiners keep landing on someone who'd only just touched down. |
| **Also move reconnecting players** | Off | Whether someone rejoining a run they were already in is held and moved too. Off because the game restores those players to where they left off, and hauling them down to the lowest scout would throw that away. See below. |

### Why you might want the Curse on

It's off by default, so joining a run in progress costs nothing. That's the friendly setting, and
probably the right one if people are hopping in and out of your lobby casually.

The case for turning it on: landing next to the group is a revive in all but name. The mod is doing
for free what the group would otherwise have walked back down and paid for, and reviving anyone
costs Curse (0.05 normally, 0.15 from Ascent 7 up). Charging a late joiner the same keeps the mod
from being a way around that, and stops a run where somebody hops out and back in from working out
cheaper than one where everybody stayed. It's one toggle, and nothing else about the mod changes.

The **Ascent 7/8 starting Curse is always applied**. That's the Ascent's rule rather than the
mod's, so there's no toggle for it. *Curse as if revived* is the revival Curse on top of that.

Neither one touches a joiner the base camp campfire spawns in by itself (the game has already
handled their Curse) or a reconnecting player (they keep the Curse they'd built up).

Everything else lives in the config file under the `General`, `Arrival` and `Timing` sections: how
steep is too steep, how far to the side joiners land, whether they're held as ghosts at all, and
the various grace periods.

### Reconnecting players keep everything

With *Also move reconnecting players* switched on, being held costs a returning player nothing but
time. They keep their items, their backpack and their statuses, and they keep their own Curse
instead of being given a joiner's. Only their position changes.

One case is deliberately left alone even with the toggle on: if the base camp campfire has revived
the group, the game brings a returning scout back to that campfire, which is exactly where they
should be. Standing with everyone else is the point, so the mod doesn't drag them off it.

Two things had to be worked around for that. The normal revive drops everything you're carrying,
and a held player's body sits in the death zone, so their loot would have landed somewhere
unreachable. Reviving them also clears every status, which would have made leaving and rejoining a
free cure-all. Joiners who really are new still take the ordinary path.

### Spectating while waiting

A waiting joiner spectates whoever they like, exactly as normal. The mod doesn't take their camera
off them.

What it does add is a line on the spectate panel, under the name of whoever they're currently
watching, naming the lowest scout: the one they're actually waiting on. If they're already watching
that scout, the line reads **LOWEST PLAYER** instead of repeating the name. Either way they can
press left and right to look around, and see at a glance who has to find their footing before they
get dropped in.

The spectate panel lives on the spectating player's own machine, so this only shows up if that
player *also* has the mod installed. It's a bonus, not a requirement. Vanilla clients ignore the
message and spectate as usual.

## Found a bug?

Please open an issue at
[github.com/IAteSpaghetti-PEAK/WaitForMEak/issues](https://github.com/IAteSpaghetti-PEAK/WaitForMEak/issues).
This mod steps in at an awkward moment in a run, so odd cases are very much expected. What helps
most is what you were doing when it happened: which biome, whether the campfire was lit, whether
the joiner was new or reconnecting, and anything the mod logged. Its lines are all prefixed
`WAITFORMEAK` in `BepInEx/LogOutput.log`. Feature ideas are welcome there too.

## Building

Requires the .NET 8 SDK and a PEAK install. The project references the game DLLs directly:

```bash
dotnet build WaitForMEak.csproj -c Release
```

The built DLL is copied into `BepInEx/plugins/WaitForMEak` automatically. Pass
`-p:SkipDeploy=true` to skip that. Override the game path with `-p:GameDir=...` or a
`WaitForMEak.csproj.user` file.
