# Hall Seats

## Overview

A lord's hall shows only as many lords as it has places for. The lords left out are still in the settlement; they
are just not spawned for this visit, so a crowded hall no longer packs 20 to 40 lords onto one spot in front of the
door.

## Why This Exists

- **Vanilla behavior:** every lord in a town or castle is placed in the lord's hall: lords whose party is inside and
  lords with no party who stay there (`DefaultHeroAgentLocationModel.GetLocationForHero`, `:44-48` and `:110-118`,
  v1.5.7). A lord spawns on a free `sp_notable` spot, then `npc_common_limited`, then `npc_common`
  (`MissionAgentHandler.SpawnWanderingAgent`, `:748-822`, v1.5.7). When those run out, the fallback loop over all tags
  (`:768-786`) takes the first machine with a free point in each tag and has no break out of the tag loop (the `break`
  at `:783` leaves only the inner loop), so the last tag that has one wins; if no tag has one, the same loop over the
  paired points runs (`:788-806`). Neither loop marks the machine used (only `GetInitialFrameForSpawnTag` does,
  `:710`), so every extra lord spawns on that one point and they push each other into a block.
- **TAOM requirement:** TAOM has 10 lords per clan in several kingdoms (the Misty Mountain orcs: 15 clans, 150 lords,
  10 settlements), and Misty Mountain, Gundabad and goblin settlements use the Sturgian keep. Its level 1 and 2 hall
  (`sturgia_castle_keep_a_l2_interior`) has no inline `sp_notable`. Its seat count has not been measured: the log line
  `[HallSeats] ...` prints it the first time a visit holds a lord back.
- **Without this feature:** a home castle with 40 lords present shows a wall of identical armoured orcs (their civilian
  outfit is a heavy helmet and armour) standing shoulder to shoulder at the entrance.

## Architecture

### Design Challenge

The number of lord spots is only known once the scene is loaded, inside the mission, while the decision of which
lords are "in the hall" is made on the campaign side when the player enters the settlement. Moving heroes out of the
hall's location list would change campaign state (their location, the overlay, the bribe check). Skipping only the
agent build changes nothing outside the mission.

### Solution Approach

Patch148, category `Patch148_HallSeats`, applied at GameInit, two hooks on `MissionAgentHandler`:

1. A void prefix and void finalizer on `SpawnLocationCharacters` open and close one spawn pass
   (`HallSeatsService.BeginPass` / `EndPass`) when the MCM switch is on.
2. A bool prefix on `SpawnDefaultLocationCharacter` asks `HallSeatsService.ShouldHoldBack` for each character in the
   pass. On the first call the service reads the hall through `HallSeatsAdapter` (location `lordshall` only) and asks
   `HallSeatPlanner` for a plan. A held-back lord gets a null agent, the value vanilla returns when it finds no spawn
   frame.

Seats = the distinct unused usable machines under `sp_notable`, `npc_common_limited` and `npc_common`
(`HallSeatRules.CountSeats`), minus the other characters that will land on them. A machine counts once however many
of those tags it carries: `GetAllProps` lists each machine under every tag its entity has (`MissionAgentHandler.cs:226-239`,
v1.5.7), and the vanilla `sp_npc_wait` carries `npc_common_limited`, `npc_wait` and `npc_common`
(`SandBox/Prefabs/sp_editor_spawnpoints.xml:237-241`), so `FindUnusedUsablePointCount` (`:417-457`) counts it under two of
the three tags. A machine also seats one agent whatever its point count: a spawn takes one random free point of the
machine (`GetSpawnFrameFromUsableMachine`, `:824-834`) and `GetInitialFrameForSpawnTag` then adds the whole machine
to `_usedSpawnPoints` (`:699-716`, the add at `:710`). The adapter lists the machines with
`GetAllUsablePointsWithTag`, skips those in the private `_usedSpawnPoints` and keeps those with a free point by
the static `GetPointCountOfUsableMachine(machine, checkForUnusedOnes: true)` (`:1020`, the same count
`FindUnusedUsablePointCount` uses). If `_usedSpawnPoints` cannot be bound the adapter reads no hall and every lord
spawns as in vanilla. Must-keep lords always spawn: the player's clan, the owner clan's leader, a kingdom leader, the town's governor,
a hero with an issue or an ongoing quest, a hero a quest tracks, and the lord the overlay's Conversation action opened
the hall for (the rule is `HallSeatRules.IsMustKeep`). The ruler and spouse (forced to `sp_throne`) are not planned at all. The other lords fill what is left in
this order: owner clan, clan leader, clan tier, clan renown, hero id.

### Component Diagram

```
SpawnLocationCharacters            (prefix: BeginPass, finalizer: EndPass)
  LocationCharactersAreReadyToSpawn (guards, musicians added)
  for each location character:
    SpawnDefaultLocationCharacter  (prefix: ShouldHoldBack?)
      first call: HallSeatsAdapter.ReadLordsHall -> HallSeatPlanner.Plan
      held back: __result = null, original skipped
```

## Configuration

MCM `Fief Management/Hall Seats`, switch `Hall Seats` (`TaomSettings.EnableHallSeats`), default on, no restart: read
once per spawn pass, so it applies from the next visit.

## Key Files

| File | Role |
|---|---|
| `Main/Features/HallSeats/Domain/HallSeatPlanner.cs` | The seat rule, engine-free |
| `Main/Features/HallSeats/Domain/HallSeatRules.cs` | Seat counting (a machine once) and the must-keep rule, engine-free |
| `Main/Features/HallSeats/HallSeatsService.cs` | Spawn-pass state, plans once per pass, fails open |
| `Main/Adapters/HallSeatsAdapter.cs` | Reads the hall: spots, lords, must-keep flags |
| `Main/Features/HallSeats/Hooks/` | The two Patch148 hooks |
| `Main/Features/HallSeats/HallSeatsModule.cs` | Registration, category, statics |
| `Main/Features/HallSeats/HallSeatsSettingsProvider.cs` | The MCM switch |

## Dependencies

- `MissionAgentHandler.SpawnLocationCharacters`, `SpawnDefaultLocationCharacter`, `GetAllUsablePointsWithTag`,
  `GetPointCountOfUsableMachine`, `HasUsablePointWithTag` (SandBox).
- The private `MissionConversationLogic._teleportNearCharacter` and `MissionAgentHandler._usedSpawnPoints`
  (reflection-sites Category B).
- `QuestManager.IsQuestGiver`, `QuestManager.TrackedObjects`, `Hero.Issue`.

## Tests

- `HallSeatPlannerTests`: seat count, must-keep past the count, ranking, ties, reference identity.
- `HallSeatRulesTests`: a machine under two tags counts once, one seat per machine, one test per must-keep term.
- `HallSeatsServiceTests`: pass lifecycle, one plan per pass, switch off, fail open, the log line.
- `HallSeatsWiringTests`: module listed, category on both hooks, statics, the switch ships on.
- `Patch148HallSeatsBindingTests`: target signatures and parameter names, the two private fields.
- `CoopVetoClassificationTests`: the bool prefix is `ReviewedSafe`.

## How to verify in game

1. Load a save and enter a castle or town where many lords are present (a Misty Mountain castle such as Caradhras, or
   any settlement where an army has gathered). Go to the lord's hall.
2. Expected: the lords stand on separate places around the hall; there is no block of armoured lords at the door.
   The TAOM log (`Logs/taom_debug_*.log`, not `rgl_log`) has one line `[HallSeats] <scene>: N lords, M free lord spots,
   ...; seated S, held back H`, and only when the visit held someone back.
3. From the town menu overlay, pick a lord who was held back and choose Conversation: the hall opens and that lord is
   there.
4. Switch `Fief Management/Hall Seats` off in MCM and re-enter the hall: every lord spawns as in vanilla.

## Known limits

- **A character with its own tag counts as "not on a lord spot" when the scene has that tag.**
  `HallSeatsAdapter.LandsOnALordSpot` asks `HasUsablePointWithTag(tag)`, which only says the tag exists
  (`MissionAgentHandler.cs:718-725`), not that a free machine is left. The engine has no cheap exact answer: telling
  whether the character reaches its own machine means replaying the spawn order (several characters share one tag,
  and a machine may also carry a lord tag), so the code stays as it is. A hall whose special-tag machines run out
  seats one lord too many. A machine that also carries a lord tag does the same: the character takes a machine the
  count gave to a lord, and the extra lord lands on the fallback point.
- **The seat count and the spawn use slightly different free-point tests.** The count uses
  `GetPointCountOfUsableMachine` (skips instant-use points and points an agent is walking to); the spawn needs only an
  active point with no user (`GetSpawnFrameFromUsableMachine`, `:827`). A machine whose only free points are
  instant-use counts as no seat here but can still take a spawn.

## Out of scope

- The civilian outfits of orc, goblin and Gundabad lords (heavy helmet and armour). That is data in the
  `*_civ_template_*` equipment rosters; a lighter outfit would make even a seated court read as lords.
- Adding `sp_notable` spots to the Sturgian keep halls (a scene change).
- Fewer lords per clan (a balance decision).
