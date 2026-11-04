# The Long Defeat: Phase 1 plan

Written 2026-11-03 by a design pass on the spike's evidence, then attacked from the source, and settled. A builder works from this file. The feature doc is [long-defeat.md](../long-defeat.md).

**Three facts the attack did not raise, fixed below:**
- The spike changed at 23:19, after the first plan. `Boot` now breaks hidden gates, so section 4 now carries that step. No run has exercised it, because the four runs ended by about 22:25 (risk 16).
- Spike line numbers moved by about 11, so spike code is now cited by method name.
- Under `--lose`, `gold` and `no-rout` could fail on a run that cleared no wave, so both now handle that case.

**Precondition, met:** the spike's last changes are commit `985b0d54e`. Line numbers below are for that commit. Risk 16 is closed by run `20261103-235336`: the hidden gate breaks, and the Middle lane opens.

**Three findings that change the design:**
- **`Mission.IsFriendlyMission` controls both Tab and the order bar.**
  - Its only writer in the dump is `MissionGauntletBattleScore.cs:39`, and the spike's list lacks that view.
  - While it stays true, Tab ends the mission (`Mission.cs:3767-3772`, `:3856-3886`).
  - `DefaultVisualOrderProvider.IsAvailable` also returns false (`:16-23`), so `VisualOrderFactory.GetOrders` returns an empty list with a failed assert (`:40-60`).
  - So from source, the spike's order bar has an owner and no orders. One write in the launcher fixes both. `ViewSubModule.cs:164-165` registers the provider.
- **The shader dialog is a known scene problem, not caused by this mode.**
  - Helm's Deep is one of only 2 of the 50 `*forceatmo*` scene folders with no `compressed_shader_cache.sack` (recounted on disk this turn). The other is `taom_fords_01_forceatmo`.
  - Minas Tirith has an 11 MB sack (`ShaderCache/D3D11/`, 11,024,176 bytes), which fits it never showing the dialog.
  - Same class as #287 (`docs/features/shader-precompilation.md:82`, `precompile_scenes.txt:44`). The scene is also registered for sieges in `custom_battle_scenes.xml`, so this mode adds no new exposure.
- **Fell Foes cannot send anyone off here.**
  - `FellFoesRules.cs:196` needs `scan.IsFieldBattle`.
  - `FellFoesAdapter.cs:39` reads `Mission.IsFieldBattle`, which is false under Siege team AI (`Mission.cs:1375`).

---

## 1. Files

### TAOM (Main)

**`Main/Features/LongDefeat/Domain/`** is pure C#. `LongDefeatDomainPurityTests` pins it, in the shape of `FordsDomainPurityTests`.

| File | Responsibility |
|---|---|
| `RunPhase.cs` | `Preparation, Wave, Intermission, Ended`; `WaveStage { Spawning, Fighting }` |
| `RunResult.cs` | `RunEndReason { DefendersDead, KeepLost, Stalemate, TargetReached, Abandoned }` and the result record |
| `RunRules.cs` | Every number from `waves.json` `rules`; `Default` |
| `RunPlan.cs` | Fortress, wave plan, shop, rules, `RunOptions`: what the `SurvivalRun` constructor takes |
| `RunOptions.cs` | `StopAfterWaves`, `Autoplay`, `GarrisonScale` |
| `WaveSpec.cs` | `WaveSpec`, `WaveTroop(TroopId, Slot, Weight)`, `WaveScaling`, `FormationSlot { Infantry, Ranged }` |
| `WavePlan.cs` | `ForWave(n)`: authored list, then the scaled last wave; expands weights by largest remainder, ranged first |
| `Purse.cs` | Balance, earned per source (`Bounty, WaveBonus, EarlyStart, Granted`), spent |
| `ShopPlan.cs` | `ShopOffer`, starting gold; `BuyResult`, `StartResult` enums |
| `StalemateRule.cs` | Pure form of the spike's `WatchStalemate` (`LongDefeatSpikeRunner.cs:505-547`) |
| `KeepHoldRule.cs` | Capture arithmetic of `CaptureRule.cs:35-56`, with its own numbers. `StagedSiegeTunables` is too wide to build just for this, so it is copied |
| `MusterPlan.cs` | Splits the garrison into timed batches; `CountOverlaps(newPoints, earlierPoints, radius)` |
| `RunSnapshot.cs` | What the engine reads each tick |
| `RunEffects.cs` | What the engine must do; `SoldierBatch`, `RunEvent` |
| `RunView.cs` | Immutable view for the HUD and the bot |
| `RunStats.cs` | Per-wave and total counts for the report, including fled, felled and felled waves |
| `SurvivalRun.cs`, `SurvivalRun.Shop.cs` | The aggregate |
| `ShopBot.cs` | The unattended buyer, pure over `RunView` |
| `BestRun.cs` | Record and `IsBetter`: waves, then kills |

**`Main/Features/LongDefeat/Config/`**

| File | Responsibility |
|---|---|
| `LongDefeatConfigScope.cs` | Copy of `FordsConfigScope.cs` |
| `LongDefeatConfigCatalog.cs` | Copy of the `FordsConfigCatalog.cs` shape: `Lazy`, never throws, `FromTexts` for tests |
| `FortressesReader.cs`, `WavesReader.cs`, `ShopReader.cs`, `FortressEntry.cs` | One reader per file, plus the fortress row |

**`Main/Features/LongDefeat/Hooks/`**

| File | Responsibility |
|---|---|
| `LongDefeatMissionLauncher.cs` | The spike's list (`LongDefeatSpikeLauncher.cs:64-81`) with the new logic; sets `IsFriendlyMission = false` |
| `LongDefeatMissionLogic.cs` | Under 150 lines. `: MissionLogic, IBattlePowerCalculationLogic`; ticks the director, forwards removals and fleeing, tears down on both exits |
| `LongDefeatMorale.cs` | Arm, disarm and the worker-thread question |

**`Main/Features/LongDefeat/Autorun/`**

| File | Responsibility |
|---|---|
| `LongDefeatAutorunPlan.cs` | Armed only by env `TAOM_LONGDEFEAT_AUTORUN`, in the shape of `FordsAutorunPlan.cs` |
| `LongDefeatAutorunDriver.cs` | Menu wait, start, wait for the end, done file, quit |
| `ILongDefeatAutorunHost.cs`, `NullLongDefeatAutorunHost.cs` | ADR-013 seam |

**`Main/Features/LongDefeat/Diagnostics/`** (pure)

| File | Responsibility |
|---|---|
| `LongDefeatChecks.cs` | `const string` check ids and the verdict logic for each |
| `LongDefeatRunReport.cs` | JSON text from `RunStats` |

**`Main/Features/LongDefeat/` root**

| File | Responsibility |
|---|---|
| `LongDefeatDirector.cs` | Domain to adapter translation; no TaleWorlds type in any signature. Owns the `DeferredCallbackQueue` |
| `LongDefeatSession.cs` | What the client may touch, in the `FordsSession.cs` shape, without its gate (see "Attacks declined"). `Result` survives `Clear` and resets at `Fill` |
| `LongDefeatSetup.cs` | Setup record: fortress id, trigger, `RunOptions` |
| `LongDefeatSetupPrompt.cs`, `LongDefeatMenuEntry.cs` | Copies of the Fords shape |
| `LongDefeatBestRunStore.cs` | Documents JSON, atomic, never throws |
| `LongDefeatReportFile.cs` | Copy of `FordsReportFile.cs` |
| `LongDefeatRecorder.cs` | Records file and export, in the `StagedSiegeRecorder.cs` shape |
| `LongDefeatModule.cs`, `ILongDefeatGameManagerFactory.cs`, `NullLongDefeatGameManagerFactory.cs`, `LongDefeatMission.cs` | Stay, edited |

**Other Main files**

| File | Responsibility |
|---|---|
| `Main/Features/DevConsole/Cheats/LongDefeatCheats.cs` | `taom.ld_start <fortress> [waves]` (Slice 9, replacing the spike's), `taom.ld_record`, `taom.ld_export` (Slice 11) |
| `Main/Adapters/ILongDefeatBattlefieldAdapter.cs`, `LongDefeatBattlefieldAdapter.cs`, `.Diagnostics.cs` | All engine work on the field |
| `Main/Adapters/ILongDefeatMenuAdapter.cs`, `LongDefeatMenuAdapter.cs` | Copy of `FordsMenuAdapter.cs` with its own notice title and choice type. Fords' adapter hard-codes its title (`:23`) |
| `Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs` | One clause |
| `Main/_Module/ModuleData/long_defeat/{fortresses,waves,shop}.json` | Config |
| `Main/_Module/GUI/PreFabs/LongDefeat/LongDefeatHud.xml`, `LongDefeatPanel.xml` | Prefabs |
| `Main/_Module/ModuleData/taom_module_strings.xml` and the 12 `std_taom_module_strings_*.xml` | `TAOM_LD_*` rows; each language row is the English seed |

### TAOM.Client (`Client/Features/LongDefeat/`)

| File | Responsibility |
|---|---|
| `LongDefeatGameManager.cs`, `LongDefeatGameManagerFactory.cs` | Edited, Fords shape, adds `ReturnToMainMenu` |
| `LongDefeatMissionViews.cs` | Spike list (`:20-40`) with `new LongDefeatHudView()` instead of the unattended view, from Slice 10; `[ViewMethod]` statics only |
| `LongDefeatUnattendedView.cs` | From Slice 8 reads `LongDefeatAutorunPlan.ArmedInThisProcess` or the spike env; after Slice 9 only the former; deleted in Slice 10 |
| `LongDefeatHudView.cs` | Two layers, keys, refresh, focus-loss answer |
| `LongDefeatHudVM.cs`, `LongDefeatOfferVM.cs`, `LongDefeatViewModel.cs`, `LongDefeatHudText.cs` | View models and text |
| `LongDefeatAutorunHost.cs` | Shape of `FordsAutorunHost.cs` |

Also edited:
- `Client/ClientSubModule.cs`.
- `Client/TaomClientComposition.cs`, one line.
- `TAOM.Tests/Composition/ClientSubModuleTests.cs` (`:193-195` list).
- `TAOM.Tests/Infrastructure/MissionViewRegistrationGateTests.cs` allowlist row (`:25`).

`ConsoleCommandBindingTests.MainMenuCommands` (`:58`) already holds `ld_start`, so it needs no edit. No new main-menu command is added.

### Tools and docs

- `tools/run_long_defeat.py`, `tools/tests/test_run_long_defeat.py`, `tools/ingame_checks/1079.toml`.
- A row in `tools/README.md` (Slice 8, with the tool; the spike's row goes in Slice 9).
- A row each in `.claude/skills/game-run/SKILL.md`, `docs/reference/unattended-game-runs.md`.
- `docs/features/dev-console.md`: the `:349` row in Slice 9, new rows in Slice 11.
- `docs/features/long-defeat.md`, `docs/reference/feature-map.md`.

### Deleted

**Slice 9, from `Spike/`:** `LongDefeatSpikeRunner.cs`, `LongDefeatSpikeLogic.cs`, `LongDefeatSpikeLauncher.cs`, `LongDefeatSpikeFortress.cs`, `LongDefeatSpikeOptions.cs`, `LongDefeatCheats.cs`.

**Slice 9, elsewhere:**
- `Client/Features/LongDefeat/LongDefeatSpikeAutostart.cs`.
- `tools/run_long_defeat_spike.py`, `tools/tests/test_run_long_defeat_spike.py`.
- `TAOM.Tests/Features/LongDefeat/LongDefeatSpikeFortressTests.cs`, `LongDefeatSpikeOptionsTests.cs`.
- The S4 row in `ReflectionSiteBindingTests.cs:86-87` and `reflection-sites.md:69`.

**Slice 10:** `LongDefeatUnattendedView.cs`, when `LongDefeatHudView` replaces it. An armed run is never left without a focus guard (`tools/run_fords_autorun.py:563` depends on one).

**Stays until the new wrapper has passed one run (Slices 5 to 8):**
- All of the above, side by side with the new path, under the same mission name.
- The factory gains `Create(LongDefeatSetup)` beside `Create(LongDefeatSpikeOptions)`; the old overload dies in Slice 9.

**Later phases read S4, S5 and S7 from git.** Slice 9 adds this line to the feature doc: "recipes at commit <last spike commit>, `LongDefeatSpikeRunner`: `BreakGate`, `TryBreak`, `RepairGate` (gate), `BuyEngine` (engine buy), `ChangeGear` (gear)".

---

## 2. Domain

**Phases**
- `Preparation` becomes `Wave`.
- A clear goes to `Intermission`, then back to `Wave`.
- Any state can go to `Ended`.

**State**
- Phase, wave number, phase seconds left, `Purse`.
- Foot muster queue and pending purchases.
- Attacker queue, `firstBatch` for this wave and first-batch-done flag.
- `StalemateState`, keep progress, `RunStats`.
- `_fellPending`, player deaths, `RunResult`.

**No random numbers in Phase 1.** Waves are fixed.

**Threads.** `SurvivalRun` is plain state and is only ever touched on the main thread.
- `OnAgentRemoved` can arrive off the main thread (`mission-frame-threads-and-native-costs.md`, the "Either" row). The director routes every removal through `DeferredCallbackQueue.RunOrDefer`, as `FordsMissionDirector.cs:268-275` does.
- `OnAgentFleeing` is main-thread only (`MissionAgentPanicHandler.cs:53`), so it needs no queue.
- The director's tick starts with `MissionThreadGuard.MarkMainThread()` and `_deferred.Drain()`, before `Read` and `SurvivalRun.Tick`. Otherwise a clear can fire before its parked bounties land, and the final wave drops them.
- `MarkMainThread` also runs before `Begin`.
- `Shutdown` drains once more before `Abandon` and the report, then calls `Clear`.
- `[Check]` lines (`IOutcomeLog.cs:15`, main thread only) are written from the director's tick or `Shutdown`, never from a callback.

```csharp
internal sealed class SurvivalRun
{
    public SurvivalRun(RunPlan plan);          // fortress, wave plan, shop, rules, RunOptions
    public RunPhase Phase { get; }
    public int Wave { get; }                   // 0 in Preparation
    public int WavesCleared { get; }
    public int Gold { get; }
    public RunResult? Result { get; }
    public RunEffects Begin();                 // once: first garrison batch, player, FinishInitialSpawn
    public RunEffects Tick(RunSnapshot s);     // !(s.Dt > 0f) returns empty effects
    public void AttackerDown(string troopId);  // bounty unless _fellPending > 0
    public void AttackerFled();                // counts only; the morale clause should keep this at 0
    public void DefenderDown();
    public void PlayerDown();
    public BuyResult TryBuy(string offerId);   // Ok, NotOpen, UnknownOffer, NoGold, GarrisonFull, RunOver
    public StartResult TryStartWave();         // Ok(bonus), NotOpen, RunOver
    public void Abandon();                     // Result = Abandoned unless already ended
    public RunView GetView();
}

internal sealed record RunSnapshot(float Dt, int AttackersStanding, int AttackersFleeing, int DefendersStanding,
    int AttackersAtKeep, int DefendersAtKeep, bool PlayerAlive, int SiegeBattleSize);

internal sealed class RunEffects
{
    public List<SoldierBatch> DefenderBatches { get; }  // TroopId, Slot, Count, FirstIndex, BatchTotal, Reinforcement, Place
    public bool SpawnPlayer { get; set; }
    public bool FinishInitialSpawn { get; set; }
    public WaveStart? StartWave { get; set; }           // wave number, foot and ranged live counts
    public List<SoldierBatch> AttackerBatches { get; }
    public bool RefreshAttackerTactic { get; set; }     // first batch done: forced use and a new TacticBreachWalls
    public bool OrderCharge { get; set; }
    public bool RestoreDefenderTactic { get; set; }
    public int FellAttackers { get; set; }
    public List<RunEvent> Events { get; }
    public RunResult? Ended { get; set; }
}

internal static class StalemateRule
{
    internal static StalemateDecision Step(ref StalemateState state, float dt, bool lossThisTick,
        int attackers, int defenders, int waveCount, RunRules r); // None, Charge, FellStragglers(n), NoResult
}

internal static class KeepHoldRule
{
    internal static float Step(float progress, int attackers, int defenders, float dt, RunRules r);
}

internal static class ShopBot
{
    internal static BotAction Decide(RunView view, float secondsInPhase); // Buy(id), Horn, Wait
}
```

**Rules.** Numbers come from `rules` in `waves.json`. Each rule gets a test, and each float gate gets a NaN test.

1. **Preparation:** lasts `preparationSeconds` (90). Shop open.
2. **Muster:**
   - `Begin` emits all archers, the first `musterBatch` (20) foot, `SpawnPlayer` and `FinishInitialSpawn`.
   - The rest of the foot comes `musterBatch` every `musterBatchSeconds` (4 s).
   - In each batch, `FirstIndex` runs 0 to n-1 and `BatchTotal` is n.
   - `Place` is true once per defender formation, on its first batch. Later muster batches and bought packs set `Place=false`: `SetPositioning` would move the order position of soldiers already standing (`Mission.cs:4427-4434`, `Formation.cs:855`), and vanilla places only a formation with no place yet (`MissionBattleSideSpawnContext.cs:369`).
3. **Shop:**
   - Open only in Preparation and Intermission.
   - `TryBuy` refuses when standing + pending + pack exceeds `garrisonCap` (220).
   - The price is spent at once. The pack comes out on the next `Tick` as `Reinforcement=true`.
4. **Horn:** `TryStartWave` earns `min(earlyGoldCap, ceil(secondsLeft * earlyGoldPerSecond))` (2 per second, cap 120), then starts the wave.
5. **Wave start:**
   - `Wave++`, then `WavePlan.ForWave(Wave)`.
   - Compute once and store `firstBatch = min(count, max(101, min(liveCap, SiegeBattleSize - DefendersStanding)))`.
6. **Spawning:**
   - `spawnsPerTick` (8) leave the queue each tick, capped by live room.
   - Live room is `max(101, min(liveCap, SiegeBattleSize - DefendersStanding)) - AttackersStanding`.
   - The floor of 101 keeps engines in use (`TacticBreachWalls.cs:28`, `:544-551`: at 100 or fewer the new tactic stops every machine and latches the shock assault).
   - Attacker batches are always reinforcement.
   - The first batch's class totals come from the queue's first `firstBatch` entries. `Place=true` on each class's first entry, once per wave, because the last wave's formation is empty and the frame is where the wave must start. The batch stays open across ticks, as the spike's does.
   - When `firstBatch` soldiers have spawned: `EndBatch` on both classes, then `RefreshAttackerTactic`, in the order of the spike's `SpawnSome`, then enter `Fighting`. A test spawns 8 per tick and asserts the refresh sees 101 or more.
   - Any later soldier spawns in its own batch, with `BeginBatch` and `EndBatch` in that tick.
7. **Bounty:** `AttackerDown` pays `bounties[troopId]` whoever killed. A soldier felled by rule pays nothing, through `_fellPending`. This is why `KillAgentCheat` crediting the main agent (`Mission.cs:6220`) cannot pay gold. `_fellPending` resets at a clear, so a count the adapter could not fell never swallows a later bounty.
8. **Clear:**
   - The queue is empty, `Fighting`, and no attacker stands.
   - Pays `waveBonusBase + waveBonusPerWave * Wave` (150, 50).
   - Resets keep progress to 0.
   - If charging, sets `RestoreDefenderTactic`.
   - Enters Intermission for `intermissionSeconds` (60).
   - If `RunOptions.StopAfterWaves` is reached, ends as `TargetReached`.
9. **Stalemate.** Quiet means no `AttackerDown` or `DefenderDown`, which is cleaner than the spike's total, since trickle spawns would disturb that total.
   - Quiet for `quietSeconds` (60) with half the wave down, or `quietAnySeconds` (180) at any count: `Charge`.
   - Then quiet for `strandedSeconds` (90): if `attackers <= stragglerShare * waveCount` (0.10) **and** `attackers < defenders`, then `FellAttackers = attackers`. Otherwise end as `Stalemate`.
   - This is the spike's condition (`LongDefeatSpikeRunner.cs:522`, feature doc: "at most a tenth of the wave, and fewer than the defenders"). A test holds 11 attackers against 2 defenders in a wave of 110: it ends as `Stalemate`, felling nothing and paying no bonus.
   - The wave's stats record `felled`, and `RunStats` counts waves that ended by felling.
10. **Keep:**
    - Rises by `dt / keepHoldSeconds` (45) when attackers ≥ `keepMinAttackers` (12) and attackers ≥ `keepDominance` (2.0) × defenders.
    - Falls by `dt / keepDecaySeconds` (90) when defenders dominate the same way.
    - Otherwise holds. At 1, end as `KeepLost`.
    - Only runs in `Wave`, and resets to 0 at a clear (rule 8).
    - `keepMinAttackers` 12 is chosen so a straggler group can never take the keep from the smallest wave: its share is 11 of 110. Spike run `20261103-211432` at t 421 had 7 attackers up to z 32.5 and 31 defenders alive at median z 20.5. A test holds 11 attackers unopposed in the zone and progress stays 0. A shipped-data test pins `keepMinAttackers > stragglerShare * smallest wave count`.
11. **Loss:** `DefendersStanding == 0` with no pending defender batch ends as `DefendersDead`. This is checked first in `Tick`.
12. **Scaling:** past the list, the last wave's roster with `count = min(maxCount, last.count + countPerWave * extra)`. Health, damage and tier rises stay in Phase 2.
13. **Player:** the player's death changes no run rule, and the run ends only by rules 8 to 11. `PlayerDown` counts a death. Respawning is the director's job from `PlayerAlive` (section 7).
14. **Bot:**
    - Buys in offer order, foot and archers 2:1, while gold reaches and standing + pending < 170.
    - Horn once nothing is left to buy and 30 s of the phase have passed. This proves the early bonus.
15. **Fleeing:** `OnAgentFleeing` calls `AttackerFled`. `Agent.cs:1645` sets `IsRunningAway` without that callback, so `RunStats` also keeps the peak of `s.AttackersFleeing`. `no-rout` reads both.

---

## 3. Config

Every id below was read from `fords_of_war/fords_troops.xml`, and the attack rechecked them this turn. Level and class are from that file, and the culture is `Culture.isengard` or `Culture.vlandia`. `lord_4_1` is at `custom_battle_lords.xml:39`, and the scene at `custom_battle_scenes.xml:45`.

**`long_defeat/fortresses.json`**
```json
{ "fortresses": [ {
    "id": "helms",
    "name": "{=TAOM_LD_Fortress_Helms}Helm's Deep",
    "scene": "taom_rohan_castle_helms_deep_forceatmo",
    "defenderCulture": "vlandia",
    "attackerCulture": "isengard",
    "playerTroop": "lord_4_1",
    "waves": "helms_isengard",
    "shop": "rohan",
    "garrison": [
      { "troop": "fow_rohan_westfold_militiaman", "class": "Infantry", "count": 100 },
      { "troop": "fow_rohan_eastfold_skirmisher", "class": "Ranged", "count": 50 } ],
    "keep": null } ] }
```

Recorded shape, once it exists: `"keep": {"x":0,"y":0,"z":0,"radius":12}`.

A `muster` block has no reader in Phase 1. The commit that ships the first recorded muster point adds its parser and its adapter call (section 7).

**`long_defeat/waves.json`**
```json
{ "rules": {
    "preparationSeconds": 90, "intermissionSeconds": 60,
    "earlyGoldPerSecond": 2.0, "earlyGoldCap": 120,
    "waveBonusBase": 150, "waveBonusPerWave": 50,
    "spawnsPerTick": 8, "liveCap": 150, "garrisonCap": 220,
    "musterBatch": 20, "musterBatchSeconds": 4.0,
    "quietSeconds": 60, "quietAnySeconds": 180, "strandedSeconds": 90, "stragglerShare": 0.10,
    "keepRadius": 12.0, "keepHeightBand": 4.0, "keepHoldSeconds": 45.0, "keepDecaySeconds": 90.0,
    "keepMinAttackers": 12, "keepDominance": 2.0 },
  "bounties": { "fow_urukhai_fighter": 4, "fow_isengard_militia_spearman": 4, "fow_urukhai_skirmisher": 6,
    "fow_urukhai_warrior": 6, "fow_urukhai_spearman": 8, "fow_urukhai_swordman": 8, "fow_urukhai_arbalest": 8,
    "fow_urukhai_crossbowman": 10, "fow_urukhai_infantry": 10, "fow_urukhai_berserker": 13 },
  "rosters": { "helms_isengard": {
    "scaling": { "countPerWave": 10, "maxCount": 300 },
    "waves": [
      { "count": 110, "mix": [ { "troop": "fow_urukhai_fighter", "class": "Infantry", "weight": 50 },
                               { "troop": "fow_isengard_militia_spearman", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_skirmisher", "class": "Ranged", "weight": 25 } ] },
      { "count": 120, "mix": [ { "troop": "fow_urukhai_fighter", "class": "Infantry", "weight": 40 },
                               { "troop": "fow_urukhai_warrior", "class": "Infantry", "weight": 30 },
                               { "troop": "fow_urukhai_skirmisher", "class": "Ranged", "weight": 30 } ] },
      { "count": 130, "mix": [ { "troop": "fow_urukhai_fighter", "class": "Infantry", "weight": 20 },
                               { "troop": "fow_urukhai_warrior", "class": "Infantry", "weight": 30 },
                               { "troop": "fow_urukhai_spearman", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_arbalest", "class": "Ranged", "weight": 25 } ] },
      { "count": 140, "mix": [ { "troop": "fow_urukhai_warrior", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_spearman", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_swordman", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_crossbowman", "class": "Ranged", "weight": 25 } ] },
      { "count": 150, "mix": [ { "troop": "fow_urukhai_spearman", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_infantry", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_berserker", "class": "Infantry", "weight": 25 },
                               { "troop": "fow_urukhai_crossbowman", "class": "Ranged", "weight": 25 } ] } ] } } }
```

**`long_defeat/shop.json`**
```json
{ "shops": { "rohan": { "startingGold": 600, "offers": [
  { "id": "militia", "name": "{=TAOM_LD_Offer_Militia}Westfold Militia", "troop": "fow_rohan_westfold_militiaman", "class": "Infantry", "packSize": 12, "price": 180 },
  { "id": "yeomen", "name": "{=TAOM_LD_Offer_Yeomen}Eastfold Yeomen", "troop": "fow_rohan_eastfold_skirmisher", "class": "Ranged", "packSize": 10, "price": 180 },
  { "id": "axemen", "name": "{=TAOM_LD_Offer_Axemen}Westfold Axemen", "troop": "fow_rohan_westfold_axeman", "class": "Infantry", "packSize": 8, "price": 200 },
  { "id": "bowmen", "name": "{=TAOM_LD_Offer_Bowmen}Eastfold Bowmen", "troop": "fow_rohan_eastfold_bowman", "class": "Ranged", "packSize": 8, "price": 200 },
  { "id": "kings_knights", "name": "{=TAOM_LD_Offer_Knights}King's Knights, on foot", "troop": "fow_rohan_edoras_golden_hall_eorlingas_rider", "class": "Infantry", "packSize": 6, "price": 360 }
] } } }
```

These numbers are a first guess, not a balance measure. Spike wave losses were 36, 24 and 60, against rosters stronger than wave 1 here. A wave should earn about 900 gold, about 55 soldiers.

**Validation.** Each bad value costs one warning naming the file and key. Floats are checked for NaN and infinity before range. A number must be a JSON number, and a whole-number setting a JSON integer.

| Field | Rule | On failure |
|---|---|---|
| Seconds in `rules` | 1 to 3600, finite | default |
| `earlyGoldPerSecond` | 0 to 100 | default |
| Gold amounts | 0 to 1,000,000 | default |
| `spawnsPerTick` | 1 to 50 | default |
| `liveCap` | 101 to 400 | default |
| `garrisonCap` | 1 to 400 | default |
| `musterBatch` | 1 to 50 | default |
| `stragglerShare` | 0 to 0.5 | default |
| `keepRadius` | 2 to 50 | default |
| `keepHeightBand` | 1 to 20 | default |
| `keepDominance` | 1 to 10 | default |
| `keepMinAttackers` | 1 to 100 | default |
| `countPerWave` | 0 to 100 | default |
| `maxCount` | 101 to 1000 | default |
| Wave `count` | 101 to 1000 | wave skipped |
| Mix `weight` | positive integer | wave skipped |
| `class` | `Infantry` or `Ranged` | row skipped |
| Garrison `count` | 1 to 400 | row skipped |
| `packSize` | 1 to 50 | offer skipped |
| `price` | 1 to 1,000,000 | offer skipped |
| Offer `id` | unique | later one skipped |
| Bounty | 0 to 10,000; missing means 0 with one warning | |
| Coordinates | finite, absolute value ≤ 100000 | block skipped, feature falls back |
| `waves` and `shop` references | must exist | fortress skipped |
| Roster | at least one valid wave | fortress skipped |
| Garrison | at least one valid row | fortress skipped |
| No valid fortress | menu option hidden | |

**Shipped-data tests** (`LongDefeatShippedConfigTests`, as `LongDefeatSpikeFortressTests.cs:29-46`):
- Every troop id exists in `fords_troops.xml`.
- `class` equals the XML `default_group`, except that a Cavalry troop may be `Infantry`.
- Every wave troop's culture equals `attackerCulture`, and every offer and garrison troop's culture equals `defenderCulture`. This enforces Mike's own-troops rule.
- `keepMinAttackers > stragglerShare * smallest wave count`.
- The launcher still refuses an unknown id at runtime (`LongDefeatSpikeLauncher.cs:39-45`).

---

## 4. Engine seams

Source: `C:\Users\mikew\.taom-src\v1.5.6\TaleWorlds.MountAndBlade.*`. "Spike" means a method of `LongDefeatSpikeRunner`.

`ILongDefeatBattlefieldAdapter` holds no TaleWorlds type, per `ServiceSignatureGateTests`. It uses its own `LdPoint` and `LdFrame` structs.

| Adapter member | Wraps | Evidence |
|---|---|---|
| `Launcher: open` | `MissionState.OpenNew`, then `SetPlayerCanTakeControlOfAnotherAgentWhenDead`, `IsFriendlyMission=false` | `MissionState.cs:251-277, :302`; `Mission.cs:956, :6402` |
| `Boot(plan sizes)` | `DefaultMissionDeploymentPlan.AddTroops`, `MakeDefaultDeploymentPlans`, frame reads | `DefaultMissionDeploymentPlan.cs:92, :103`; `Mission.cs:3999`; spike `ChooseFrames`, `PlanFrame` |
| `BreakHiddenGates()` | Every `CastleGate` not visible including parents: break through `DestructableComponent.TriggerOnHit` with a `BatteringRam` as attacker. Never removed: a null `InnerGate` throws in `BehaviorAssaultWalls`. Runs before `DeployEngines` | spike `Boot`, `BreakHiddenGate`, `TryBreak`; feature doc "The hidden gate at Helm's Deep" |
| `DeployEngines()` | `SiegeWeaponAutoDeployer`, hit points, ladders hidden by a tower, every defender point disabled | `SiegeDeploymentHandler.cs:104-110`; spike `DeployEngines`, minus the buy-point exception |
| `KeepFromScene()` | Reinforcement foot frame | `Mission.cs:3999`; scene tag `DefaultMissionDeploymentPlan.cs:405` |
| `PlaceFormation(slot, count, attackers)` | `Formation.BeginSpawn`, `SetFormationPositioningFromDeploymentPlan`. Only for a batch with `Place=true`: once per defender formation, once per class per wave | `Formation.cs:2579`; `Mission.cs:4427`; spike `PlaceFormation` |
| `BeginBatch(slot, count, attackers)` | `Formation.BeginSpawn` alone, for a batch with `Place=false` | `Formation.cs:2579` |
| `SpawnBatch(batch, attackers)` | `GetAgentBuildDataToSpawnTroop`, `SpawnTroopWithAgentBuildData`, no position. Returns each new soldier's position, read in the same tick | `Mission.cs:4461, :4517`; spike `Spawn` |
| `StandingDefenderPoints()` | Human active defenders' positions, read before a defender batch spawns | spike `Count` shape |
| `EndBatch(...)` | `Formation.EndSpawn` | `Formation.cs:2586` |
| `SpawnPlayer(respawn)` | Same path; the player troop gets the Player controller. On respawn, chains `.IsReinforcement(true)`, because `GetAgentBuildDataToSpawnTroop` skips that flag for the player troop | `Mission.cs:4477`, `:4504-4507`, `:4043`; `AgentBuildData.cs:162` |
| `FinishInitialSpawn()` | `Mission.OnInitialSpawnCompleted` | `Mission.cs:6777-6795` |
| `RefreshAttackerTactic()` | `SetForcedUse(true)`, `ClearTacticOptions`, `AddTacticOption(new TacticBreachWalls)`, `ResetTactic(false)` | `Team.cs:427, :443`; `TeamAIComponent.cs:184`; spike `SpawnSome` |
| `OrderCharge()` | `DelegateCommandToAI` on the defenders, then `TacticCharge` on both | `Team.cs:479-485`; spike `Charge` |
| `RestoreDefenderTactic()` | New `TacticDefendCastle` | spike `EndCharge` |
| `Fell(n)` | `Mission.KillAgentCheat` | `Mission.cs:6216-6220` |
| `Read(keep)` | Counts human, active agents, fleeing and not, every tick; keep counts every 0.5 s | `Team.cs:114`; spike `Count` |
| `AdoptMain(aiDriven)` | Sets `GeneralAgent`, `PlayerOrderController.Owner`, each `PlayerOwner`, then `DelegateCommandToAI`; optionally `Controller=AI` | `Team.cs:128`; `OrderController.cs:20`; `Formation.cs:283-294`; `Agent.cs:1233-1290` |
| `DescribeEngines()` | Spike state line text | spike `LogEngines` |
| `SiegeBattleSize` | `BannerlordConfig.GetRealBattleSizeForSiege` (150 to 1000) | `BannerlordConfig.cs:62, :634-637` |

**Mission logic overrides:**
- `OnAgentRemoved`, whose engine path also deactivates the agent (`Mission.cs:3000-3015`). It goes through `RunOrDefer`.
- `OnAgentFleeing` (`MissionBehavior`, lifecycle doc line 22).
- `OnRetreatMission` (`MissionLogic.cs:30`, `Mission.cs:2865-2868`).
- `OnEndMission`, `OnRemoveBehavior`.

**No `MissionEnded` override.** Nothing in this mode needs the engine's ended state.

`LongDefeatBindingTests` (`BindingVerification`, in the `FordsOfWarBindingTests` shape) pins every member above.

---

## 5. UI

**Two layers, both in `LongDefeatHudView`, built from the `[ViewMethod]` list:**
- **HUD layer:** `new GauntletLayer("GauntletLayer", 20, false)`, no input restriction, movie `LongDefeatHud`. Fords sets a whole-mission cursor (`FordsCommandView.cs:91-93`) because it has no agent. This mode has one, so it must not.
- **Panel layer:** created on open and removed on close, order 21, movie `LongDefeatPanel`. Exact shape of `MissionGauntletEscapeMenuBase.cs:76-82, :92-95`: `IsFocusLayer=true`, `SetInputRestrictions()`, `RegisterHotKeyCategory("GenericPanelGameKeyCategory")`, `TrySetFocus`. It does not call `PauseGameEngine` (`:73`), so the timer runs. `Exit` or `ToggleEscapeMenu` released on the panel closes it (`:103`).

**Keys,** read from the view's `Input` with Ctrl and Alt skipped, like `FordsCommandHotkeys.cs:15-16`:
- `B` toggles the shop.
- `H` sounds the horn.
- Both letters are free: no `InputKey.B` or `.H` binding in the cached files except `GenericCampaignPanelsGameKeyCategory` and `InputSystem`, and TAOM's other uses are in Fords' own mission view (`FordsCommandHotkeys.cs:39, :53`). 15 hotkey categories are not in the cache, UNVERIFIED. Digits and F keys belong to the order bar (`MissionOrderHotkeyCategory.cs:72-85`).
- Inside the panel, mouse only.

**`LongDefeatHud.xml`**
- Top centre strip, in the `FordsCommand.xml:28-34` style.
- `@WaveText`, `@TimerText`, `@GoldText`, `@AttackersText`, `@DefendersText`, a keep bar with `@KeepFillWidth`, shown only when `@IsKeepVisible`.
- A bottom hint line `@HintText` and a notice `@NoticeText`.
- Every widget has `DoNotAcceptEvents="true"`. No static text, no root DataSource (`FordsCommand.xml:9-10`).
- Read `TAOMSpriteData.xml` before writing any `Sprite=`. The attack did not check sprite references.

**`LongDefeatPanel.xml`**
- Shop group `@IsShopVisible`: `@ShopTitle`, `@GoldText`, list `{Offers}` with `ExecuteBuy` buttons, `ExecuteHorn` with `@HornText`, `ExecuteClose`.
- End group `@IsEndVisible`: `@EndTitle`, `@EndLines`, `@BestText`, `ExecuteLeave`.

**View models**
- `LongDefeatViewModel` is a copy of `FordsViewModel.cs`.
- `LongDefeatOfferVM` has name, pack, price, can-afford.
- `LongDefeatHudVM` takes `(LongDefeatSession, Action leave)` and refreshes every 0.25 s (`FordsCommandView.cs:30, :71-75`).
- Text lives in `LongDefeatHudText` as `TextObject("{=TAOM_LD_...}...")`.

**The panel opens by itself** on run end; the end group's Leave calls `LongDefeatGameManager.ReturnToMainMenu`. It closes by itself at wave start.

**Setup prompt** (`LongDefeatSetupPrompt.Begin`, shape of `FordsSetupPrompt.cs:39-58`): one question.
- **Title:** "The Long Defeat". The choices are the fortress rows.
- **Description:** what the mode is, the best run for each fortress, and "If a shader error window appears while the fortress loads, press Cancel."
- `Start(setup, trigger)` is the prompt-free path for the console and the autorun, and lands in Slice 6. `Begin` lands in Slice 11. Difficulty is a Phase 5 question.

**What the player does**

| Phase | Player |
|---|---|
| Preparation | Stands with the garrison as it musters; `B` shop, `H` horn for bonus gold; timer shown; order bar works |
| Wave | Fights; order bar; formations start under AI, and an order takes the selected formation (`OrderController.cs:722-727`); `B` gives a notice |
| Dead | Spectates; `E` takes a soldier, per `MissionScreen.cs:2890-2892`, game key 16 which is `Kick` at `CombatHotKeyCategory.cs:147` |
| Intermission | As Preparation, for `intermissionSeconds` |
| End | Reads the panel, presses Leave; the battle runs on behind the panel with no new spawns |

---

## 6. Unattended path

**Arming**
- Only `TAOM_LONGDEFEAT_AUTORUN` arms a run.
- Keys: `fortress`, `waves`, `out` (required, absolute), `quit`, `live_limit_seconds`, `wall_limit_seconds`, `screenshots`, `garrison_scale`, `expect`.
- `ArmedInThisProcess` is what `LongDefeatUnattendedView`, then `LongDefeatHudView.IsOpeningEscapeMenuOnFocusChangeAllowed` reads (`FordsCommandView.cs:45`).

**Driver.** Ticked from `ClientSubModule.OnApplicationTick` on wall time, with the same menu gate as `FordsAutorunMenuGate.cs`:
1. Wait until the menu has been up over 4 s, hiding inquiries.
2. `prompt.Start(setup, "autorun")`.
3. Write `long_defeat_status.txt` every 5 s.
4. Watch `session.Result` and the limits. `FordsSession.cs:7-12` is filled at mission start and cleared at mission end, so `LongDefeatSession.Result` must survive `Clear` and reset at `Fill`. A test covers Fill, Clear, read, Fill.
5. Write `long_defeat_done.txt`, then quit. Quitting in a live mission works (feature doc notes, all unattended runs).

**Done reasons:**
- `final`: target waves cleared.
- `lost_defenders`, `lost_keep`, `stalemate`.
- `abandoned`: the mission ended with `RunEndReason.Abandoned`.
- `fault`, `live_limit`, `wall_limit`, `start_refused`, `never_started`.

**Autoplay inside the mission** (`RunOptions.Autoplay=true`):
- The director calls `AdoptMain(aiDriven: true)` whenever the main agent changes, polled each tick.
- `Agent.Controller=AI` makes `AgentHumanAILogic.OnAgentControllerChanged` add the AI component (`:16-30`). Vanilla precedent: `CustomBattleAgentLogic.cs:48-52`.
- `ShopBot.Decide` runs each tick in Preparation and Intermission.
- `BestRunStore` writes under `out`, so an unattended run never overwrites Mike's best.

**`tools/run_long_defeat.py`.** Same monkeypatch shape as `run_long_defeat_spike.py:126-132`, with the shader-dialog `handle_dialogs` carried over:
- `--waves 5`, default `--wall-limit 3900`, default `--live-limit 3600`. The spike took 1,380 s for 3 waves (about 460 s per wave), and its live default is 1800. `DEFAULT_WALL_LIMIT_SECONDS` is 2400.
- `--runs 3` already exists on the Fords runner.
- `--lose` arms `garrison_scale=0.2` and `expect=lost`.
- `collect` copies the report through `done["report"]` (`run_fords_autorun.py:710-717`).

**`judge`.** Exit 0 needs all of these, then `final_verdict`:
- `reason=final` (or `lost_*` under `--lose`).
- Report `final` true, and `wavesCleared ≥ N` (not under `--lose`).
- Every id in the wrapper's `REQUIRED_CHECKS` has a pass line. This tuple grows slice by slice (section 8), and `--lose` uses a shorter one: `boot, general, muster, keep, end, best-run` plus `hud, orders` once they exist.
- No `id=taom.long-defeat.* result=fail`.
- No `spawn level WRONG`.
- No CrashReport block.

Its success text prints how many waves ended by felling, as the spike's does.

Then: `python tools/ingame_verdict.py --issue 1079 --sessions 4`.

**Manifest `tools/ingame_checks/1079.toml`**
- `fixed_in = "758c8f6fc"` (an ancestor of this branch, checked this turn) and `allow_dirty = true` while iterating. A `.dirty` build otherwise gives NOT REACHED. Slice 13 replaces both (below).
- Every `reached` is a `[Check]` line: the row's own line, or `boot`'s for a mission-start row.
- `logs = ["taom"]`.
- Ids are `const string` in `LongDefeatChecks`, except client ones, which sit in `LongDefeatHudView`. `RepoManifestGateTests` finds them as literals.
- **A row lands with its emitter.** `tools/tests/test_ingame_verdict.py:328-337` fails on an id no source file holds.
- Rows that depend on how the session went use `absence = "not-reached"`, because the newest decisive verdict wins (`ingame_verdict.py`, docstring steps 2 and 3). The judge, not the absence value, holds an unattended run to the full set.

| Check id | Proves | Key | absence |
|---|---|---|---|
| `taom.long-defeat.boot` | Deployment finished, siege team AI, main agent, `friendly=false` | mission | fail |
| `taom.long-defeat.general` | `GeneralAgent==main`, `IsPlayerGeneral`, order owner is main | mission | fail |
| `taom.long-defeat.orders` | Client: `VisualOrderFactory.GetOrders().Count > 0` | mission | fail |
| `taom.long-defeat.muster` | Per batch: no soldier far or off the navmesh; `on_point` ≤ 6; `overlap` ≤ 6, where `overlap` counts new soldiers within 0.4 m of a soldier from an earlier batch | mission | fail |
| `taom.long-defeat.keep` | Keep point source and coordinates resolved | mission | fail |
| `taom.long-defeat.wave` | Wave cleared and earned: felled ≤ `stragglerShare` × count. Detail has kills, felled, lost, seconds; `min = 5` | `w<N>` | not-reached |
| `taom.long-defeat.stale` | Stale charge led to a clear | `w<N>` | not-reached |
| `taom.long-defeat.no-rout` | Zero attackers fled and fleeing peak 0, kills > 0. With no kills, Skip | run end | not-reached |
| `taom.long-defeat.gold` | Start + earned - spent == balance. Bounty above 0 if kills > 0; bonus above 0 if `wavesCleared` ≥ 1 | run end | not-reached |
| `taom.long-defeat.shop` | Pack bought and read back at full count. Detail carries `on_point` and `overlap`, which fail nothing | `<offer>` | not-reached |
| `taom.long-defeat.early-start` | Horn paid the formula's amount | `w<N>` | not-reached |
| `taom.long-defeat.player-death` | Death, then respawn adopted at the next intermission | `d<N>` | not-reached |
| `taom.long-defeat.end` | Result reason and report written | run end | not-reached |
| `taom.long-defeat.best-run` | Saved and read back | run end | not-reached |
| `taom.long-defeat.hud` | Client: both movies load, `WaveText` set | mission | fail |
| `taom.long-defeat.menu` | Option installed, not hidden | process | not-reached |

**`[[human]]` entries in the manifest:**
- How the walls look under attack.
- Whether orders move formations.
- `B` shop movement lock and feel.
- `E` takeover.
- Terrain after Cancel.
- Prompt text.
- Tab does nothing.
- Leaving the boundary returns to the main menu.

**Exit:**
- `python tools/run_long_defeat.py --waves 5 --runs 3` passes on three runs.
- One `--lose` run passes.
- Mike plays one, with F5 from the worktree's `TAOM.sln`.
- All on a committed build, then `ingame_verdict` (Slice 13).

---

## 7. Open points answered

**Where the garrison musters, no points recorded yet.**
- The first attempt in code is config-free: time-spaced batches at the scene's own foot frame, with `musterBatch` 20 fitting the ~27 slots (rule 2). 100 foot arrive over 16 s. Archers keep their frame, since only 2 of 56 were on the point.
- **Batches reuse slots.** A slot depends only on index, count, frame and live width (`Mission.cs:3976`, `Formation.GetUnitSpawnFrameWithIndex`), so batch 2's indices 0 to 19 land on batch 1's slots. The design works only if batch 1 has left them in 4 s, and nothing proves that yet (risk 3).
- **What the check measures.** The spike's count within 1 m of the frame cannot see this, since 20 soldiers on 27 slots stay under its limit either way. So `[Check] muster` reads `overlap`: before each defender batch the director takes `StandingDefenderPoints()`, and `MusterPlan.CountOverlaps` compares the batch's returned positions against them. A director test has batch 1 never moving, and the check must fail.
- **If the check fails at Slice 8's gate,** the main session raises `musterBatchSeconds` once and reruns, then takes the result to Mike. The builder does not guess. The 0.4 m radius is my first guess and is below a healthy formation's spacing (risk 17).
- For Mike, in a Long Defeat mission or any siege:
  - `taom.ld_record muster_foot`, `muster_archers`, or `keep [radius]` appends the main agent's position and look to `Documents\Mount and Blade II Bannerlord\TAOM\long_defeat_records.jsonl`.
  - `taom.ld_export` writes the block to paste into `fortresses.json`, in the `StagedSiegeCheats.cs:30-68` shape.
- **The recorded muster frame's consumer is cut from Phase 1,** since no shipped data or exit run would exercise it. The commit that ships the first recorded muster point adds:
  - the `muster` parser, with `fx`, `fy` normalized as in `StagedSiegeSceneCatalog.cs:348-356`;
  - `SetMusterFrame`, wrapping `Formation.SetPositioning` plus `AgentBuildData.SpawnsIntoOwnFormation(true)` (`AgentBuildData.cs:168`, `Mission.cs:3950-3954, :4243-4247`), so native slots still apply;
  - a fallback to batches if the point has no navmesh.

**Bought packs.** They spawn at the reinforcement frame. The old claim that packs of 12 or fewer fit that frame is withdrawn: the same run logged 24 of 24 on the point (log line 3188) and 2 of 16 (line 5206), and `docs/features/long-defeat.md` blames the live formation's shape. `shop` reports `on_point` and `overlap` and judges the count only, because a pile spreads by itself and fails nothing in the spike.

**The keep point on Helm's Deep with no recording.**
- At run start the director uses the defenders' reinforcement foot frame, the scene's `defender_infantry_reinforcement` entity: (138.4, 159.5, 32.5) in the run log, a terrace 32 m behind the (166.4, 191.2, 26.5) foot frame.
- Radius 12 and height band ±4 come from config. The foot frame itself sits 30 m from the gate, so it would fall too easily.
- Attackers do reach height 32.5 (`t 401` line in run `20261103-215943`). Whether this exact spot is a fair keep is risk 5, and the keep line logs attacker coordinates to answer it.

**The player's death.**
- The engine nulls `MainAgent` and delegates the team to AI (`Mission.cs:3017-3022, :3039-3042`), so the wave plays on and the run does not end.
- Vanilla's spectator view offers takeover. `E` takes a non-hero soldier at 25% health or more (`:6393-6400`).
- The director polls `MainAgent` each tick. On any new body it calls `AdoptMain`. `PlayerOwner`'s setter turns AI off (`Formation.cs:292`), so it delegates afterwards.
- At Intermission start, if `MainAgent` is null, it calls `SpawnPlayer(respawn: true)`. The chained `.IsReinforcement(true)` is what puts the lord on the reinforcement frame: `Mission.cs:4477` skips that flag for the player troop, and without it he appears at the foot frame, 30 m from the gate. A player already in a soldier's body stays there.
- Rising at the keep in bought gear is Phase 4.

**How the morale clause reaches only this mission.**
- `LongDefeatMorale` holds one volatile `Mission` reference.
- Logic `AfterStart` calls `Arm(Mission)`. `OnEndMission` and `OnRemoveBehavior` both call `Disarm(Mission)`, since the lifecycle doc requires both exits.
- `HoldsAttacker(agent)` returns true only when `agent.Mission` is that reference and `agent.Team.Side` is Attacker. These are the same reads `CommonAIComponent.cs:180` makes on that thread, with no allocation (`ICultureMoraleService.cs:5-8`).
- `TaomCustomBattleMoraleModel.cs:27` gains `!LongDefeatMorale.HoldsAttacker(agent) &&` first.
- `CommonAIComponent.cs:90-93` then holds morale at 0.01, and defenders can still rout.
- `CustomBattleMoraleModel.cs:82-85` is the only base behaviour. `Unsteady` (`FellFoesAdapter.cs:138`) sets morale 0 and is then held by this clause. `SendOff` cannot fire here (`FellFoesRules.cs:196`).

**Tab and leaving.**
- `IsFriendlyMission=false` makes Tab do nothing.
- Escape's exit already calls `EndGame`.
- Boundary crossing reaches `RetreatMission` (`MissionBoundaryCrossingHandler.cs:133-150`), so `OnRetreatMission` calls the menu adapter's `EndGame` (`MBGameManager.cs:182-214`). `CheckAndSetEnding` is what avoids the Custom Battle screen. `EndMission` is idempotent (`Mission.cs:4629-4636`).
- `Shutdown` drains the queue, runs `Abandon`, writes the report and saves the best run on every exit.

**What the shader dialog means for a player.**
- On a cold cache, a player may meet the same two Retry/Cancel windows, and on some GPUs the same #287 crash. Custom Battle sieges at this scene already carry that risk.
- Phase 1 ships the prompt sentence and does not touch `Shaders/`, which belongs to the install.
- **Decision for Mike, not blocking:** build a scene sack for Helm's Deep in the Kit. Release scene sacks keep shipping as they are (`docs/reference/release-process.md:112-114`). It lives in the unversioned `TAOM_Map`, so a reinstall reverts it. The feature doc's note asks for "a cached variant, or a guarded copy of line 818" before a menu entry lands; this sentence replaces that only if Mike accepts it, and Slice 12 corrects the doc.
- Who wrote the 57 sacks dated 2026-11-03, and how, is UNVERIFIED.

**Remaining "Notes for Phase 1" points:**
- **Archers' start:** keep the authored frame. They do climb: `high 145 of 148` at t 200. Posts wait for S6.
- **Stalemate:** carried over per rules 7 and 9. The feature doc prefers `Agent.Die` over `KillAgentCheat` for its credit. Gold here does not read credit (rule 7), so the spike-proven call stays.
- **Trolls and repaired gates:** no troll in these waves and no repair in Phase 1, so decide in Phase 3.
- **The Nine:** `DreadMissionGate` stays as it is, since no Nine appear here; decide with Phase 2's dread affix.
- **The buy point:** every defender point is disabled, so no placeholder shows. Phase 4 keeps one live at boot.
- **`CoopGateDocCoverageTests`:** it covers module-declared campaign behaviors (`:13-17`) and this module declares none, so no row is owed.

---

## 8. Slices

Builders cannot invoke skills or spawn agents. They use `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~LongDefeat`, never `./build.ps1`, and do not commit. Slices that touch console commands also run `--filter FullyQualifiedName~ConsoleCommandBinding`, which the first filter skips. Game runs are main-session only. Before changing `TaomCustomBattleMoraleModel`, run `python tools/graphify_taom.py refresh --if-stale`, then `python tools/graphify_taom.py affected "TaomCustomBattleMoraleModel" --depth 2`.

| # | Tier | Tests first | Files | Done when |
|---|---|---|---|---|
| 1 | implementer | `PurseTests`, `WavePlanTests`, `StalemateRuleTests` (spike's numbers, NaN, 11 against 2), `KeepHoldRuleTests` (11 unopposed stays 0), `MusterPlanTests` (overlap count), `BestRunTests`, `LongDefeatDomainPurityTests` | `Domain/` except `SurvivalRun`, `ShopBot` | Filter green alone; nothing else touched |
| 2 | implementer | `SurvivalRunTests`: every rule in section 2, every phase, each `BuyResult`, ended no-op, NaN `Dt`, `firstBatch` at 8 per tick, keep reset at a clear, `_fellPending` reset; `ShopBotTests` | `SurvivalRun*.cs`, `ShopBot.cs` | Green; gold invariant test holds across a scripted 5-wave run |
| 3 | implementer | `LongDefeatConfigCatalogTests` (one per validation row, missing file, not JSON), `LongDefeatShippedConfigTests` | `Config/`, three JSON files, module registers catalog | Green; `python tools/lint_docs.py --fail-on-drift` clean |
| 4 | implementer | `LongDefeatMoraleTests` (arm, disarm, other mission, defender, null; uninitialized `Mission` as `DreadMissionGateFordsTests.cs:23`, `RequiresGame`), source pin on the model line | `LongDefeatMorale.cs`, the clause | Green; inert, since nothing arms yet |
| 5 | implementer | `LongDefeatDirectorTests` with a fake adapter (a removal from a background thread parks and lands before the next `Tick`; a clear never fires before parked bounties; batch 1 never moving fails `muster`; `Place` once per defender formation), `LongDefeatSessionTests` (Fill, Clear, read, Fill), `LongDefeatChecksTests` (pass and fail per id), `LongDefeatRunReportTests` | Adapter interface, director, session, checks, report text | Green with no engine code |
| 6 | implementer | `LongDefeatMissionLauncherTests` (list, no `GeneralsAndCaptainsAssignmentLogic`, `IsFriendlyMission` source pin), `LongDefeatBindingTests`, `LongDefeatEntryPointLineCountTests` | Engine adapter, logic, launcher, `LongDefeatSetup`, factory `Create(LongDefeatSetup)`, client manager, menu adapter (`EndGame`), `LongDefeatSetupPrompt.Start` only | Full compile, tests green. Nothing calls the new path yet, so this slice has no game run |
| 7 | implementer | `LongDefeatBestRunStoreTests` (missing, bad JSON, NaN, atomic, armed folder), `LongDefeatReportFile` tests | Store, report file, director wiring | Green |
| 8 | implementer | `LongDefeatAutorunPlanTests`, `LongDefeatAutorunDriverTests`, source pin that the unattended view reads `ArmedInThisProcess`, `test_run_long_defeat.py` (judge, args, shared strings, per-slice required list) | `Autorun/`, client host, `ClientSubModule.cs`, composition line, unattended view edit, `run_long_defeat.py`, `tools/README.md` row, manifest with every row except `orders`, `hud`, `menu`. `REQUIRED_CHECKS`: `boot, general, muster, keep, wave, no-rout, gold, shop, early-start, end, best-run` | Green, including `python -m unittest tools.tests.test_ingame_verdict`; main session: `--waves 2` exit 0 |
| 9 | implementer | Adjust module tests; pin that `Spike` is gone from source; `~ConsoleCommandBinding` | Deletions listed under Slice 9 in section 1, new `LongDefeatCheats.cs` with `taom.ld_start` on `prompt.Start`, `dev-console.md:349` row, docs line on recipes | Full suite green; `--waves 2` again exit 0 |
| 10 | implementer | `LongDefeatHudTextTests`, `LongDefeatHudStringsTests` (copy of `FordsHudStringsTests`), VM tests | Client view, VMs, text, two prefabs, allowlist row, delete `LongDefeatUnattendedView.cs`, manifest rows `hud`, `orders`, `REQUIRED_CHECKS` gains both | Green; main session: `--screenshots` shows the HUD; `[Check] hud, orders` |
| 11 | implementer | `LongDefeatMenuEntryTests` (hidden with no content, off on `IDedicatedServerProvider.IsDedicatedServer`, repeat safe), `LongDefeatSetupPromptTests`, recorder tests, `~ConsoleCommandBinding` | Prompt `Begin`, entry, module `OnPhase` through `MainMenuStep`, recorder, `ld_record`, `ld_export` in the shape of `StagedSiegeCheats`, `dev-console.md` rows, manifest row `menu` | Green. **Mike** clicks through the menu once, since the runner only presses Win32 dialog buttons (`run_fords_autorun.py:457`) |
| 12 | fast-reader | None; lint and manifest gate | Strings seed rows, `game-run` row, unattended doc, feature map, feature doc | `python -m unittest tools.tests.test_ingame_verdict`, `lint_docs` clean |
| 13 | main session | None | See the list below | Three 5-wave runs and one `--lose`; Mike plays one |

**Slice 13, in order:**
1. `/verify`, `/localize`, `/deep-review`.
2. Mike's word: commit as X.
3. Build the clean tree at X.
4. `run_long_defeat.py --waves 5 --runs 3` and one `--lose`, under `/game-run`'s gate.
5. Mike plays one, with F5 from the worktree's `TAOM.sln`.
6. Set `fixed_in = X`, remove `allow_dirty`, and run `python tools/ingame_verdict.py --issue 1079 --sessions 4`.
7. On Mike's word, commit the manifest edit.

**Cuts, each to keep Phase 1 small:**
- The plan's "general option on the runner": the wrapper stays, because `run_fords_autorun.py` is in active use by Fords sessions and the monkeypatch shape passed four runs.
- Difficulty and a fortress-picker beyond one row.
- Tier unlocks: price alone gates.
- Automatic garrison refill.
- Digit hotkeys in the shop.
- Tab scoreboard.
- Match speed.
- Auto-exit after the result, for a person.
- Delivery of scaling beyond count.
- Translations, owed to #763.
- A dedicated reinforcement point: bought packs use the scene's frame until a muster point is recorded.
- The recorded-muster consumer: `SetMusterFrame`, its fallback and the `muster` parser (section 7).
- `taom.ld_gold`: the bot and the scripted gold test prove the purse.
- A temporary `taom.ld_run`: the first version needed it for Slice 6's gate, which a person would have had to type. The first in-game proof is now Slice 8's autorun, and `ld_start` moves over in Slice 9.

**Defaults I chose without asking, each one line to reverse:**
- Formations begin under AI even for a person, so walls are manned and an order takes a formation back.
- Stalemate ends a human run too.
- Keep progress resets at a clear.
- `keepMinAttackers` is 12, so stragglers cannot take the keep from a wave of 110.

---

## 9. Risks not settled from source

| # | Risk | Line that settles it |
|---|---|---|
| 1 | **Closed.** `ViewSubModule.cs:164-165` registers `DefaultVisualOrderProvider`. `[Check] id=taom.long-defeat.orders` stays as the proof | `[Check] id=taom.long-defeat.orders detail sets=N` |
| 2 | Calling `OnInitialSpawnCompleted` before the garrison is whole may affect defender formation splitting | `[LongDefeat][Muster] batch k/n on_point=.. overlap=.. far=.. off_mesh=..`, and the 5 s `high` count against spike's 145 of 148 at t 200 |
| 3 | No run has tested batching at the foot frame. Batches reuse slots, so the design needs batch 1 gone within 4 s | `[Check] muster` with `overlap` |
| 4 | The gate stays broken from wave 2. Every spike run repaired it after wave 1 (spike `RunTests`, `RepairGate`), so this is unmeasured | `[LongDefeat][Engines] rams.. gates.. lanes..` and each wave's `seconds` |
| 5 | Attackers may never stand in the fallback keep zone, or may fill it too easily. At waves above 110, a group of 12 to 15 is within the straggler share and can still take the keep | `[LongDefeat][Keep] attackers=.. defenders=.. progress=.. at=(..)` every 5 s, with up to 8 attacker positions |
| 6 | Respawning after `_initialPlayerAgent` was nulled (`Mission.cs:3023-3026`). The setter is settled (`Mission.cs:4043`); the behaviour of the second player agent is not | `[LongDefeat][Player] respawned main=.. owner=.. controller=.. at=..` |
| 7 | AI on a lord agent is proven only in vanilla's unconscious case, and may leave him idle | `[LongDefeat][Player] controller=AI kills=.. z=..` per wave |
| 8 | Order of `PlayerOwner` then `DelegateCommandToAI` on a body swap | `[LongDefeat][General] ai_formations=N of M` right after adoption |
| 9 | Focus-layer panel may eat keys or leave the agent moving | `[LongDefeat][Hud] shop open/closed` plus the `[[human]]` item |
| 10 | Riders on foot may be useless with lances | `[Check] shop key=kings_knights` plus wave kills; balance only |
| 11 | Shader dialog only on a cold cache? Mike's two by-hand launches showed none, and all three unattended Helm's Deep launches did | `runner_log.txt` "shader cache list differs" with `rgl_log` "Missing shader from sack: pbr_terrain"; compare one by-hand `rgl_log` |
| 12 | Terrain after Cancel has never been looked at | `hud_*.png` from `--screenshots 5,60`, then Mike's eye |
| 13 | Plan sizes set from `liveCap` rather than the wave may change slot layout | `spawn level ok: attackers N ... on the frame point K` |
| 14 | A defender rout may end a run early | 5 s state line `fleeing`, `[Check] end reason=` |
| 15 | Five waves may exceed the limits | `[Check] wave ... seconds=`, `live_seconds`, `wall_seconds` in the done file |
| 16 | Breaking the hidden gate has never run. It should open the Middle lane and may change how often the stalemate rule fires, so the spike's stalemate history may not carry over | `[LongDefeat][Boot] hidden gate at .. broken`, `lanes .. Middle open True` after the outer gate falls, and the judge's felled-wave count |
| 17 | The `overlap` radius of 0.4 m and limit of 6 are first guesses and may trip on, or miss, a real formation | `[LongDefeat][Muster]` lines from the first Slice 8 run |

---

## Attacks declined

**1 (in part): a gate on the session.** The queue and the drain are in the plan. No gate is needed, because after `RunOrDefer` every `SurvivalRun` call runs on the main thread:
- `OnMissionTick`, the panel's `TryBuy` and the HUD refresh are all on that one thread, so they cannot overlap each other.
- The agent tick overlaps the panel, but it reaches the run only through the concurrent queue (`DeferredCallbackQueue.cs:17, :41-49`), and the queue is drained on the main thread.
- Fords takes its gate because "the tower hit hook calls into the same match from an engine thread" (`FordsSession.cs:10`). This mode has no such hook, so by `simplicity-criterion.md` the lock guards nothing.
- `LongDefeatDirectorTests` pins the parking.

**3 (in part): defenders at 2x attackers, and the claim that the spike compares to the wave only.**
- The spike already holds `attackers >= defenders` at `LongDefeatSpikeRunner.cs:522`, and the feature doc states it as "fewer than the defenders". The first plan dropped that clause. Restoring it stops the 11 against 2 case, and a test pins it.
- No run or incident supports 2x. The clause that passed the runs stays.

**4 (in part): a radius of about 1 m.**
- The runner's own comment (`LongDefeatSpikeRunner.cs:379`) and the feature doc say a healthy shield wall can put 5 soldiers within 1 m.
- A 1 m test would measure neighbours, not stacks. The plan uses 0.4 m against earlier batches only, flagged as a first guess in risk 17.

**7 (alternative only).** Its second option, passing the three sessions by path before running `--lose`, is not used. `absence = "not-reached"` also protects Mike's own run, which that option does not.

**9 (fix replaced).** A named file and a removal slice for `ld_run` would keep a command nothing needs once Slice 6's in-game gate moves behind the autorun (finding 16). The proof the test would fail is correct (`ConsoleCommandBindingTests.cs:58`). `ld_run` is cut, `ld_start` keeps its row and moves in Slice 9, and console slices add the `~ConsoleCommandBinding` filter.

**11 (fix replaced): a test on 45 s against 60 s.** The coordinate logging is in. Ordering the two timers would not protect the keep: `TacticCharge` "moves soldiers on some lanes and not on others" (feature doc, `BehaviorCharge.cs:169-201`), so a charge at 60 s does not bring defenders to the zone. `keepMinAttackers` 12 stops stragglers directly. The attack's UNVERIFIED stays open, since that log has no coordinates (risk 5).

**13 (alternative only): correcting the text instead of setting the flag.** `AgentBuildData.IsReinforcement` is public (`AgentBuildData.cs:162`), so the lord can use the terrace frame, which is the safer place to rise.

**19 (no extension to `keep`).** The attack named only the muster consumer, and I cut it. The `keep` parser stays because `fortresses.json` holds recorded points per scene (#1079 body) and it is the only answer to risk 5. Your line "the approved plan names `ld_record` and `ld_export`" is not in the #1079 body by my search. The body names the recorder pattern, which is enough to keep both commands.