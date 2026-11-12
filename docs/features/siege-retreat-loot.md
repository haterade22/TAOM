# Siege Retreat Loot

## Overview

When the player defends a siege and the attackers retreat from the assault, the player now receives the gear of the
attackers who fell, through the inventory loot screen. Vanilla pays gold, renown, influence and morale on that path
and nothing else (#1121).

## Why This Exists

- **Vanilla behavior:** a defender's `EnemyRetreated` result sends the encounter through
  `SiegeAssaultEventComponent.OnPlayerEncounterContinueBattle` (`SiegeAssaultEventComponent.cs:155-169`, v1.5.7). It
  commits gold, renown, influence, morale and xp, sets the winner to none, finishes and finalizes the battle and
  opens `menu_siege_strategies`. It never reaches `MapEvent.CalculateAndCommitMapEventResults`
  (`MapEvent.cs:1198-1227`), where the loot steps run.
- **TAOM requirement:** a player who repels an assault should receive what the fallen attackers carried.
- **Without this feature:** the loot screen never opens and the fallen attackers' gear is lost. A 1.4.8 player
  reported it; the same path exists in 1.5.6 and 1.5.7.

## Architecture

### Design Challenge

- The loot step is private (`MapEvent.LootDefeatedPartyCasualties`), so the adapter calls it by reflection.
- **The map event is already a defender victory when the prefix runs.** The mission end builds the result with
  `MissionResult.CreateSuccessful` and `CampaignMissionComponent.OnMissionResultReady` calls
  `PlayerEncounter.SetPlayerVictorious`, which sets the winner. The step's own checks (the player's side is the
  winner) therefore hold with no override. Vanilla's own `SetOverrideWinner(None)` (`SiegeAssaultEventComponent.cs:161`)
  then clears the winner before the battle is finished, so `FinalizeEventAux` moves nobody and the siege continues.
- **A stale result must not loot.** Neither `ContinueBattle` nor `FinalizeBattleFromComponent` clears the
  encounter's `EnemyRetreated` result (`PlayerEncounter.cs:1221-1236`, `:1572-1576`); only the next mission start
  (`MenuHelper.cs:408`) or `InitSimulation` (`:1665`) resets it. A fresh assault can then reach the same branch with
  no winner (#1021 records this for the enlisted join). UNVERIFIED: whether a non-enlisted defender can reach it. The
  adapter answers false unless the state is `DefenderVictory`, which covers both cases.
- **Only the casualty step runs.** The item step would take about half of the baggage of besiegers who survive and
  keep the siege (`DefaultBattleRewardModel.cs:313`), and the prisoner step would free their captives. Vanilla itself
  captures no one from a retreating side (`MapEvent.cs:1799`). Widening the scope is a design decision, not a fix.
- A second full commit would pay renown and influence twice (`MapEventParty.CommitRenownChanges` and
  `CommitInfluenceChanges` do not reset their values), so the commit step is not repeated.

### Solution Approach

A prefix on the target runs the loot, then vanilla runs unchanged, then a postfix opens the screen.

1. The prefix calls `SiegeRetreatLootService.CollectLoot(enemyRetreated)`. The service acts only when
   `EnemyRetreated` is set, the adapter confirms a siege defender at `DefenderVictory` whose component allows casualty
   loot, and this peer is not a co-op client.
2. `SiegeRetreatLootAdapter.RunCasualtyLoot` calls the private step with the winner and defeated party lists that
   `CalculateAndCommitMapEventResults` passes.
3. After vanilla's menu switch, the postfix calls `OpenLootScreen`. It copies the pending items, clears the
   encounter's roster (the siege encounter is reused, so stale loot would show after the next assault) and pushes
   `InventoryScreenHelper.OpenScreenAsLoot`.
4. The outcome line is written when the screen opens, so the check proves delivery.

### Component Diagram

```
Patch146 prefix (void, __state)
        |
SiegeRetreatLootService  (decides; co-op gate; reports the outcome line)
        |
ISiegeRetreatLootAdapter -> MapEvent private casualty step, PlayerEncounter roster, loot screen
        |
vanilla OnPlayerEncounterContinueBattle runs unchanged
        |
Patch146 postfix -> OpenLootScreen
```

## Configuration

None. There is no MCM switch: the change restores loot that a normal win already gives.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SiegeRetreatLoot/SiegeRetreatLootService.cs` | When the loot runs, the co-op gate, the outcome line |
| `Main/Features/SiegeRetreatLoot/SiegeRetreatLootModule.cs` | DI and the `Patch146_SiegeRetreatLoot` category (GameInit) |
| `Main/Features/SiegeRetreatLoot/Hooks/Patch146_SiegeRetreatLoot.cs` | Prefix and postfix, thin |
| `Main/Adapters/ISiegeRetreatLootAdapter.cs`, `SiegeRetreatLootAdapter.cs` | Engine reads, the reflected step, the screen |
| `tools/ingame_checks/1121.toml` | The in-game check |

## Dependencies

- `ICoopSessionProvider` and `CoopSessionPolicy` (CoopInterop): a co-op client moves no item between rosters.
- `IOutcomeLog` and `IModLogger` (Core): the `taom.siege-retreat-loot.collected` line and error lines.
- `InventoryScreenHelper.OpenScreenAsLoot` (TaleWorlds.CampaignSystem).

## Tests

- `SiegeRetreatLootServiceTests`: each decision (no retreat, no siege defender victory, co-op client, step missing, the
  three screen outcomes) and the manifest patterns against the real log text.
- `Patch146SiegeRetreatLootBindingTests`: the private step, the target's parameter names, the public members the
  adapter reads. Tagged `BindingVerification`. The step also has a row in `reflection-sites.md` Category B.
- `SiegeRetreatLootWiringTests`: the module line, the category and phase, the statics handshake.

## How to verify in game

**Status: built, not yet seen in a game.** The check is owed (label `triage-needs-ingame`).

1. Start a siege where the player is the defender, with attackers who carry gear.
2. Fight the assault until the attackers retreat. Do not wipe them out: that is the normal win path.
3. Expect the inventory loot screen after the battle, the siege to continue and the besiegers to stay in place.
4. Read the log line `[Check] id=taom.siege-retreat-loot.collected result=pass items=N`.
5. Start a second assault in the same siege. Expect no loot screen and no new check line before that fight is won.

## Out of scope

- Item and prisoner steps: see Design Challenge. Both change what surviving besiegers keep.
- Hero prisoners, ships (a siege assault never loots ships: `MapEventComponent.cs:89-93`) and field battle retreats
  (`FieldBattleEventComponent.cs:152`, a different path).
- Co-op: a client skips the loot. Whether the co-op siege mission ever produces an `EnemyRetreated` result is UNVERIFIED.
