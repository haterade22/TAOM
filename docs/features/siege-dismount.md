# Siege Dismount

## Overview

Auto-handles the player's mount when entering siege missions: removes the horse so the player fights on foot, optionally moves it to inventory, and (in the default mode) restores it automatically when the siege ends.

## Why This Exists

In LOTR-themed sieges (Helm's Deep, Minas Tirith, Erebor's gates) it is jarring for the player to fight on horseback inside a fortress courtyard or up a wall ladder. Vanilla Bannerlord makes you remember to manually un-equip your mount and re-equip after — an immersion break and a chore.

- **Vanilla behavior:** Player keeps mount equipped going into siege; mount may glitch through doors and stairs; player must manually un-equip / re-equip via the inventory screen.
- **TAOM requirement:** Optional auto-dismount with four operating modes, configurable per-player via MCM. Default is "auto-restore on siege end" so the only player-visible change is "no horse during siege."
- **Without this feature:** Players who forget to dismount get the buggy on-horseback siege experience or have to interrupt their battle prep to swap equipment.

## Architecture

### Design Challenge

Three constraints, after Codex review:

1. **Detect a siege accurately** — `Mission.IsSiegeBattle` is the authoritative engine flag and the **only** thing this feature trusts. Earlier versions also matched scene-name substrings as a fallback; Codex review found that 24 vanilla settlement `Location id="center"` scenes use names like `empire_siege_001` that can be loaded as non-combat Missions, falsely triggering the fallback. Modded sieges that fail to set the engine flag will not trigger SiegeDismount — that's a documented requirement; future modders can register their own siege detection by extending the service.
2. **Preserve `ItemModifier` on the round-trip** — vanilla `EquipmentElement` carries an `ItemModifier` (durability state, quality prefix like "Sharp"/"Damaged"). The capture path uses the full `EquipmentElement` (not just `StringId`), and the inventory adapter uses the modifier-aware `ItemRoster.AddToCounts(EquipmentElement, int)` overload. A "Sharp" horse goes into siege as Sharp and comes out as Sharp.
3. **Preserve toggle parity with the original** — the developer's tested module had four modes (Vanilla / KeepOnMap / ToInventory / AutoRemount). All four enum values retained for save-compat. Mode 1 (`KeepOnMap`) is documented honestly as Reserved/equivalent-to-Vanilla because the original implementation was a silent no-op and full implementation requires plumbing not in scope for Phase 1.

### Solution Approach

`SiegeDismountMissionBehavior` is a thin `MissionBehavior` that bridges the engine lifecycle into `ISiegeDismountService`. Mission state is read at the boundary (`Mission.Current.IsSiegeBattle`, `Mission.Current.SceneName`) and passed into the service as primitives — the service is fully unit-testable without a live `Mission`.

The service owns the state machine: capture the player's mount/harness via `IPlayerMountAdapter`, optionally move to inventory via `IPartyMountInventoryAdapter`, then on mission end restore if AutoRemount was elected.

### Component Diagram

```
TaomSettings.cs (MCM groups)
       │
SiegeDismountSettingsProvider (reads MCM)
       │
   SiegeDismountMissionBehavior (engine hook)
       │ delegates to
   SiegeDismountService (core state machine)
       │
       ├── IPlayerMountAdapter ── Hero.MainHero.BattleEquipment
       └── IPartyMountInventoryAdapter ── MobileParty.MainParty.ItemRoster
```

`IMountSnapshot` is an opaque token the service stores between mission start and mission end. The service never sees `EquipmentElement` or `ItemObject` (ADR-007).

## Configuration

### MCM Group: `Battle Tactics / Siege Dismount`

| Setting | Type | Default | Description |
|---|---|---|---|
| `Enable Siege Dismount` | bool | `true` | Master toggle. When off, sieges behave vanilla (mount stays equipped). |
| `Siege Mount Behavior` | int 0–3 | `3` (AutoRemount) | 0=Vanilla, 1=KeepOnMap, 2=ToInventory, 3=AutoRemount |
| `Siege Dismount Debug Mode` | bool | `false` | Show diagnostic `[SiegeDismount]` messages on the in-game HUD. Off = file log only. |

### Behavior Modes

| Mode | What happens | When player wants this |
|---|---|---|
| `Vanilla` (0) | Feature inert. | They like the on-horseback siege experience or are testing collisions. |
| `DismountKeepOnMap` (1) | **RESERVED — currently equivalent to Vanilla.** The original developer's module advertised "horse spawns on map, player on foot" but the implementation never actually spawned a horse agent or cleared the slot. Phase 1 port preserves the enum value for save-compat but treats it as Vanilla; logs a warning if selected. Full implementation requires `Mission.SpawnAgent` plumbing (Phase 2). | Nobody — pick another mode. Documented for transparency. |
| `DismountToInventory` (2) | Mount + harness moved to inventory **with `ItemModifier` preserved**. NOT restored automatically — player must re-equip manually. | They want fine-grained control. |
| `AutoRemountAfter` (3, default) | Mount + harness moved to inventory at mission start, restored to slots 10/11 at mission end. **`ItemModifier` (durability, quality bonuses) preserved** through the round-trip via the `ItemRoster.AddToCounts(EquipmentElement, int)` overload. | Set-and-forget. Recommended. |

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/SiegeDismount/SiegeDismountService.cs](../../Main/Features/SiegeDismount/SiegeDismountService.cs) | State-machine logic; owns `_capturedSnapshot` + `_pendingRemount` |
| [Main/Features/SiegeDismount/ISiegeDismountService.cs](../../Main/Features/SiegeDismount/ISiegeDismountService.cs) | Service interface |
| [Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs](../../Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs) | Wraps `TaomSettings.Instance` for testability |
| [Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs](../../Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs) | Enum of the four modes |
| [Main/Features/SiegeDismount/Models/IMountSnapshot.cs](../../Main/Features/SiegeDismount/Models/IMountSnapshot.cs) | Opaque token across the service/adapter boundary |
| [Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs](../../Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs) | Thin MissionBehavior; reads `Mission.Current` and delegates |
| [Main/Features/SiegeDismount/SiegeDismountIoC.cs](../../Main/Features/SiegeDismount/SiegeDismountIoC.cs) | DryIoc registrations |
| [Main/Adapters/IPlayerMountAdapter.cs](../../Main/Adapters/IPlayerMountAdapter.cs) | Reads/writes `Hero.MainHero.BattleEquipment[Horse|HorseHarness]` |
| [Main/Adapters/PlayerMountAdapter.cs](../../Main/Adapters/PlayerMountAdapter.cs) | TaleWorlds-side implementation |
| [Main/Adapters/IPartyMountInventoryAdapter.cs](../../Main/Adapters/IPartyMountInventoryAdapter.cs) | Adds/removes items from `MobileParty.MainParty.ItemRoster` |
| [Main/Adapters/PartyMountInventoryAdapter.cs](../../Main/Adapters/PartyMountInventoryAdapter.cs) | TaleWorlds-side implementation |

## Dependencies

- `ISiegeDismountSettingsProvider` (this feature) — wraps `TaomSettings`; testable
- `IPlayerMountAdapter` (Adapters) — wraps `Hero.MainHero.BattleEquipment`
- `IPartyMountInventoryAdapter` (Adapters) — wraps `MobileParty.MainParty.ItemRoster`
- `IModLogger` (Core/Logging) — TAOM's file logger
- No Harmony patches — pure `MissionBehavior` integration

## Tests

- [TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs](../../TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs) — 24 tests covering:
  - Disable / inert paths (4): MCM disabled, not-a-siege, Vanilla mode, player-on-foot
  - Siege detection scene-name fallback (5 data rows + null/empty)
  - `KeepOnMap` mode (2): captures but doesn't move; doesn't auto-remount
  - `DismountToInventory` mode (2): clears + deposits; doesn't auto-remount
  - `AutoRemountAfter` mode (3): clears + deposits + later restores; idempotent end
  - Lifecycle edges (2): no-prior-start; after non-auto mode no remount
  - Logging contracts (4): disabled-message, siege-detected-message, error-on-clear, error-on-restore

Adapters tested via integration only (live `Mission.Current`); see [Verification](#verification) for in-game golden path.

## How to change the default mode

The default is `AutoRemountAfter` (value `3`). To change for new players (existing players keep their MCM choice):

1. Edit [Main/Features/TaomSettings.cs](../../Main/Features/TaomSettings.cs) `SiegeMountBehavior` property — change `= 3;` to the desired index (0=Vanilla, 1=KeepOnMap, 2=ToInventory, 3=AutoRemount).
2. Rebuild — no other code changes needed.

## How to add a new behavior mode

1. Append a new value to [Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs](../../Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs).
2. Add a `case` to the `switch` in [`SiegeDismountService.OnMissionStart`](../../Main/Features/SiegeDismount/SiegeDismountService.cs).
3. Update the `SiegeMountBehavior` MCM bound: in `TaomSettings.cs`, raise the upper int range (currently `0, 3` → bump to `0, 4`) and update the hint text.
4. Add a test for the new mode in `SiegeDismountServiceTests.cs`.
5. Update this doc's "Behavior Modes" table.

## Performance

State is mission-local and minimal (one snapshot, one bool). No per-tick overhead — only `OnBehaviorInitialize` and `OnEndMission` fire.

**No known limitations on modifier preservation.** Earlier Phase 1 docs flagged `ItemModifier` loss as a known limitation; Codex review #1 (2026-05-06) caught that the modifier-aware [`ItemRoster.AddToCounts(EquipmentElement, int)`](../../Main/Adapters/PartyMountInventoryAdapter.cs) overload exists in the current engine API, and the snapshot was switched to carry the full `EquipmentElement`. A "Sharp" or "Damaged" horse round-trips correctly.

## Verification

In-game golden path:

1. Start a campaign, equip the player with a mount and harness.
2. MCM → TAOM → "Battle Tactics / Siege Dismount" → confirm `Enable = true`, `Mode = 3 (AutoRemount)`.
3. Travel to a settlement under siege as defender or attack a town. Enter the assault.
4. Spawn into the siege scene — confirm player is on foot. Confirm a `[SiegeDismount] siege detected — scene='X' behavior=AutoRemountAfter` line in `rgl_log.txt`.
5. Win the siege, return to map.
6. Open inventory — confirm mount + harness are back in slots 10 + 11. Confirm a `[SiegeDismount] mount restored after siege` log line.

Disable round-trip:

1. MCM → set `Enable Siege Dismount = false` → save & reload campaign.
2. Confirm one `[SiegeDismount] disabled via MCM — patches inert` line at next siege.
3. Enter a siege — player remains mounted.

## Changelog

- 2026-05-13 — Added SiegeDismount MissionBehavior wiring tests (closes #193): asserts the `AddMissionBehavior` registration in `OnMissionBehaviorInitialize`, the IoC feature registration, and that the behavior inherits `MissionBehavior`.
- 2026-05-06 — Ported the external SiegeDismount module into `Main/Features/SiegeDismount/` (adapter/service/IoC pattern, MCM under `Battle Tactics / Siege Dismount`, four behavior modes), then fixed deep-review and Codex adversarial HIGH findings — switched siege detection to `Mission.IsSiegeBattle` only (dropped false-positive scene-name matching) and preserved `ItemModifier` on the mount round-trip.

## GitHub Issue

- **Issue:** TBD (create with `/issue feature SiegeDismount integration` before commit)
- **Status:** In progress — Phase 1 (port to Main/Features/) complete; awaiting in-game verification.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
