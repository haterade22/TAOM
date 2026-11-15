# Mount Reins Guard

## Overview

A harness with reins no longer builds cloth reins on a mount that has no rein skeleton. The warg, fell warg, dire wolf
and spider Monsters have none, yet every family 1 harness fits them. One transpiler makes the engine's cloth-reins call
only when the mount's Monster names a rein skeleton. Horses, mules, camels, chariots and the Armory's horse-skeleton
mounts (elk, ram, WildMesh) keep vanilla's reins.

## Why This Exists

- **Vanilla behavior:** `MountVisualCreator.AddMountMesh` (public static, `TaleWorlds.MountAndBlade.View`, v1.5.7,
  unchanged from v1.5.6) builds the harness's rope mesh at `NativeConfig.CharacterDetail > 2` and, when the harness has
  a `reins_mesh`, calls `MBAgentVisuals.AddHorseReinsClothMesh(rein, rope)`. Nothing checks the mount. The cloth hangs
  on the Monster's `rein_skeleton`, and the creature mounts have none (#1081 RCA row 12, "Five rein attributes" in
  [dire-wolf.md](dire-wolf.md)).
- **When it runs:** for every mounted agent's visuals (mission mount agents, through `MissionScreen` and `AddMountMeshToAgentVisual`), never for the inventory or
  portrait entity, which takes the `agentVisual == null` branch and builds no cloth.
- **Player case (2026-11-15):** a Rivendell elf hero riding a dire wolf crashed natively at every mounted field-battle
  spawn on v2.0.38 (log ends at `[CareerPerks] mount stats for rider 'main_hero'`), and stopped crashing once the
  player took off the wolf and the harness. The player named the pair: `dire_wolf_brown` with the vanilla
  `light_harness` (`reins_mesh`, family 1). The elf is incidental: the elf Monster uses `as_human_warrior` and the human
  skeleton. The battle lab reproduced the crash with that pair and none with the guard (see "How to verify in game").
- **The class:** 4 Monsters without a rein skeleton in family 1 (`warg`, `fell_warg`, `dire_wolf`, `spider`, 13 items)
  and 45 installed harnesses with reins, all family 1. No troop or hero in the data pairs them; only a player's own
  choice in the inventory (for the hero or a companion) does. The elephants are family 10 and no harness with reins
  fits them.
- **Without this feature:** any of those 45 harnesses on any of those 13 mounts reaches the cloth call at character
  detail above 2.

## Architecture

### Solution Approach

The cause is that vanilla builds cloth on a mount that cannot carry it, so the guard removes only that call. A call-site
swap: the transpiler finds exactly one `callvirt MBAgentVisuals.AddHorseReinsClothMesh(MetaMesh, MetaMesh)` in
`AddMountMesh`, pushes `ldarg.1` (the mount item) before it, and turns it into `call MountReinsHook.AddReinsCloth(
MBAgentVisuals, MetaMesh, MetaMesh, ItemObject)`. Labels on the call move to the inserted load. The hook reads
`mountItem.HorseComponent.Monster.ReinSkeleton`, asks `MountReinsRule.CanBuildReinsCloth`, and makes the engine call when
it is allowed. Otherwise it logs once per Monster and returns. If it cannot read the Monster, it skips the cloth: the
reins are looks only. Vanilla still adds the plain rein mesh and the harness mesh, as it does at character detail 2
and below, and still invalidates the rope mesh. If the match count is not exactly one, the transpiler returns the
original instructions with one warning and a self-disable report.

Patch153, category `Patch153_MountReinsGuard`, applied at GameInit, `[ClientOnly]`. No model, setting, save data or
new text.

```
MountVisualCreator.AddMountMesh
   |  (transpiled call site, + ldarg.1)
MountReinsHook.AddReinsCloth --> Monster has a rein skeleton? engine cloth reins : skip, log once
```

## Configuration

None.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MountReins/MountReinsRule.cs` | The decision |
| `Main/Features/MountReins/Hooks/MountReinsHook.cs` | The replacement call |
| `Main/Features/MountReins/Hooks/MountReinsTranspiler.cs` | The IL swap |
| `Main/Features/MountReins/Hooks/Patch153_MountReinsGuard.cs` | The patch class |
| `Main/Features/MountReins/MountReinsModule.cs` | Category and logger wiring |

## Testing

- `TAOM.Tests/Features/MountReins/MountReinsTranspilerTests.cs`: the rule per input, the swap on synthetic IL (one site,
  labels, zero and two sites).
- `MountReinsBindingTests.cs` (`RequiresGameIL`): the installed target is static with the mount item as argument 1 and
  one cloth call; the swap on the real IL; the hook still reads the rein skeleton, asks the rule and calls the engine;
  the applied patch passes the JIT.
- `MountReinsLiveDataTests.cs` (`LiveInstall`): the four creature Monsters still have no rein skeleton and family 1;
  `light_harness` and `saddle_of_aeneas` still have reins and family 1.
- Mutation check: inverting the rule and dropping the inserted load fail 8 of the 19 tests.

## How to verify in game

**Battle lab, 2026-11-15** (hidden, TAOM 9e57fb94c, character detail 4, 1 vs 1 on `battle_terrain_a`): without the
guard, a goblin on `dire_wolf_brown` with `light_harness` crashed the game at spawn (access violation 0xC0000005), after
three clean control fights (elf on its own horse, elf on the wolf with `dire_wolf_saddle`, goblin on the wolf bare).
With Patch153 and Patch154 all five fights ran, both `light_harness` fights included, and the log had the
`[MountReins]` line below. The lab spawns AI troops with an equipment override, so it does not exercise the inventory.

The steps below are still owed in a campaign. Character detail must be above 2 (Options, Graphics).

1. A campaign field battle: the player on `dire_wolf_brown` with `light_harness` (an old save; Patch154 keeps the
   inventory from making the pair now). The battle starts, the log has one `[MountReins] mount 'dire_wolf_brown'
   (Monster 'dire_wolf') has no rein skeleton` line.
2. The same on `warg_low` (or any warg) and a spider with `light_harness`.
3. A horse with `saddle_of_aeneas`: reins swing as before and no `[MountReins]` line appears.
4. How the plain rein mesh looks on the wolf (it is skinned for a horse); a look issue only.

## Out of scope

Which harnesses a creature mount accepts in the inventory: that is Patch154, [creature-saddles.md](creature-saddles.md).
This guard stays as the safety net for pairs that still reach a spawn (old saves before their repair, presets, other
mods).

## Changelog

- 2026-11-15: first version, player case 2026-11-15 (elf hero on a dire wolf).
