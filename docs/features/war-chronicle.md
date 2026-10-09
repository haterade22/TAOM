# War Chronicle

## Overview

The War Chronicle is a feature module (`Main/Features/WarChronicle/`) that keeps the AI-versus-AI War of the Ring from snowballing, and measures whether it does. Today it ships four working parts: a registry of timed, kingdom-keyed effects (volunteer rate and prisoner escape), a rally layer that grants small effects to AI kingdoms that have lost a large share of their towns and castles, a daily war ledger written to the TAOM debug log, and `tools/analyze_war_ledger.py`, which reads that ledger.

The event chain (lore events at set points), the army-focus window and the Eye of Sauron hunt are planned work under #765 and are not built yet.

## Why This Exists

- **Vanilla behavior:** Bannerlord has one built-in catch-up, small and permanent. `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` scores the fiefs of the notable's settlement's faction: a town counts 1 to 3 by prosperity plus its villages, and a castle counts its villages. Below a score of 46 it raises the per-slot base from 0.7 toward 0.9, so a faction with few fiefs refills volunteer slots faster. At the extreme the first slot goes from 0.525 to 0.675 a day and the last from 0.088 to about 0.40. The term keys on absolute size, not on losses, has no time limit and also applies to player-ruled kingdoms. TAOM's phase machine ([war-of-the-ring.md](war-of-the-ring.md)) adds nothing of its own. There is no timed, kingdom-keyed modifier and no way to see how lopsided the AI war has become.
- **TAOM requirement:** Mike's decisions for #765 (2026-10-08):
  1. **No player gate.** Nothing waits on the player. The player matters only through what happens on the map.
  2. **Small effects only.** Increased recruitment and faster lord prisoner escapes, not combat power.
  3. **Catch-up for losing AI kingdoms.** A kingdom that has lost significantly gets a small, temporary lift.
  4. **No bonus for the player.** No effect is keyed to the player's own clan: its settlements get no volunteer change and its lords no extra escape roll. The rally skips a kingdom the player rules; a kingdom the player serves as vassal or mercenary can still rally (the indirect paths are under Known limitations).
- **Without this feature:** no way to tell a stalemate from a runaway except by watching the map, and no shared place for later War of the Ring mechanics (events, the Ring) to hang timed effects.

## Architecture

### Module and per-campaign behavior

`WarChronicleModule` (`Main/Features/WarChronicle/WarChronicleModule.cs`) is a `TaomFeatureModule`, appended in `Main/Composition/FeatureModules.cs`. It declares one campaign behavior, `WarChronicleBehavior`, and `OwnsSaveData` is true. The behavior is always registered, so the save payload survives a toggle.

The module builds the behavior with `CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(...))`, a lambda body rather than a container singleton, so a new instance is made in every campaign's `OnGameStart`. The behavior's constructor calls `WarChronicleStateService.ResetForNewSession()`, which empties the effect registry, the baselines and the rally tiers. `OnGameStart` runs before any campaign event and before `SyncData` on a load, so a load restores its saved record over the reset and a save with no record stays reset. There is no latch and no reset from `OnSessionLaunched` (the rule and its history: `.claude/rules/csharp-architecture.md`, "Singleton Services Holding Per-Campaign State"). `WarChronicleWiringTests` pins the `new` shape.

The behavior listens to `DailyTickEvent` and `KingdomDestroyedEvent`. The daily handler returns unless `ICoopSessionProvider.IsAuthority` and `CoopSessionPolicy.MayWriteSaveBackedState` both hold, then calls `WarChronicleTickService.RunDaily()`. The kingdom-destroyed handler needs only `IsAuthority` and writes the `ev=destroyed` ledger line.

### Tick order

`WarChronicleTickService.RunDaily()` (`WarChronicleTickService.cs`) runs, in this order:

1. `IWarEffectService.Expire(nowHours)`: drop effects whose end time has passed and re-bake the multipliers, so a changed MCM strength applies from this tick.
2. `WarBaselineService.EnsureBaselines(kingdoms)`: baseline for any kingdom not seen before. When none were held and the day is past 1, the take is late and logs one warning (see the rally's **Baseline**).
3. `RallyService.RunDaily(kingdoms, nowHours)`: tiers and rally effects.
4. `WarEscapeDailyPass.Run()`: the extra prisoner escape rolls.
5. `WarLedgerService.WriteDaily(kingdoms)`: the day's `[WarLedger]` lines.

The whole body is wrapped in one try/catch that logs a warning and skips the rest of the day, because a throwing engine getter must never fail the campaign's daily tick: a throwing snapshot read skips the baselines, the rally, the escape pass and the ledger together. The escape pass and the rally also catch their own failures. The escape pass runs after the rally, so a boost the rally grants, refreshes or removes reaches that same day's roll, with no lag in either direction; turning the rally off ends its escape boost before the next roll.

### The timed-effect registry

`WarEffectService` (`Effects/WarEffectService.cs`, `IWarEffectService`) holds `WarEffect` rows: source id, kingdom id, `WarEffectKind`, magnitude, end time in campaign hours. `WarEffectKind` has two values, `VolunteerRate` (0) and `PrisonerEscape` (1). Kinds are saved by name and the values are dense from zero, because the service bakes one array slot per kind.

- **Keying:** `Apply` upserts by (kind, source id, kingdom id). `RemoveSource` removes every row of a source.
- **Validation:** `Apply` rejects, with a warning, a null effect, an empty source or kingdom id, an undefined kind, and a non-finite magnitude or end time.
- **Math:** the multiplier for a kingdom and kind is `clamp(1 + strength x sum of magnitudes)` (`Effects/WarEffectMath.cs`). Clamps are code constants: `VolunteerRate` 0.5 to 1.5, `PrisonerEscape` 1.0 to 3.0. The strength is the MCM `War Effect Strength`, sanitized to 0 to 2 (a non-finite value becomes 1).
- **Hot path:** every multiplier is baked on `Apply`, `RemoveSource`, `Expire`, `RestoreFromSave` and `ResetForNewSession`, and swapped in as one new dictionary. `GetMultiplier` is a lock-free dictionary lookup plus an array read, and returns 1 for a null or unknown kingdom.

### The player rule

Each consumer's engine boundary passes facts, and a service decides (decision D4). The volunteer boundaries (`TaomVolunteerModel` for towns and villages, `CastleNotableMaintainer` for castles) pass the owner clan's kingdom `StringId` and whether the owner is the player's own clan; `VolunteerProductionService.Compute` then skips the war step for the player's clan. The escape adapter flags a lord of the player's clan, and `WarEscapeService` skips him. A player-ruled kingdom's AI clans still get their kingdom's multiplier, so only the player's own clan is excluded. The rally adds its own rule: it never writes effects for a kingdom the player rules (`KingdomWarSnapshot.IsPlayerRuled`).

### Consumer 1: volunteers

`TaomVolunteerModel.GetDailyVolunteerProductionProbability` (`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`) passes vanilla's value, the settlement owner clan's culture adapter, the owner clan's kingdom id and whether the owner is the player's clan to `VolunteerProductionService.Compute` (`Main/Features/TroopProgression/VolunteerProductionService.cs`). The service applies the culture respawn feats as the model did before (clamped to 0 to 1), then, unless the owner is the player's clan, multiplies by the `VolunteerRate` multiplier and clamps to 0 to 1. A multiplier of exactly 1, or a non-finite value, returns the value before the war step, so with no active effect the result is the pre-existing result. A non-finite base or feats result deliberately returns vanilla's value and skips the war step; the old model body returned NaN there (or 1 for an infinite base under a positive feat), which `RecruitmentCampaignBehavior` read as "never produce a volunteer". The model body holds no branch (`gamemodels.md` rule 4).

Vanilla's value already includes its low-fief boost (see **Why This Exists**), so the rally's multiplier stacks on it multiplicatively, before the 0 to 1 clamp. The same daily roll gates both outcomes in `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`: it fills an empty slot, and for an occupied slot it opens the chance that the waiting recruit is promoted. So the multiplier fills slots faster and also lets waiting recruits reach higher tiers sooner, up to the volunteer tier cap.

**Castles.** Vanilla's volunteer update returns early for castles, and TAOM fills castle notables through `CastleNotableMaintainer.FillCastleVolunteers` with the fixed curve of `CastleRecruitmentService.GetSlotProductionProbability`. That curve now goes through the same `Compute`, with the castle owner's kingdom and the player-clan fact read once per castle, and no culture: castles never had the respawn feats, so they get the war multiplier only. Villages bound to a castle were always covered through the model.

### Consumer 2: prisoner escape

`WarEscapeDailyPass` (`Effects/WarEscapeDailyPass.cs`) finds every kingdom whose `PrisonerEscape` multiplier is above 1, asks `IPrisonerEscapeAdapter` (`Main/Adapters/PrisonerEscapeAdapter.cs`) for the captured lords of those kingdoms' clans, and gives each eligible lord one extra roll. `WarEscapeService` (`Effects/WarEscapeService.cs`) decides:

- **Chance:** the extra chance is `p x (multiplier - 1)`. `p` starts at 0.04, is multiplied by `5 - min(81, healthy members)^0.25` when the captor is a mobile party that is not in a settlement, by 0.5 when the player holds the prisoner (the captor is the main party, or a settlement or the current settlement owned by the player clan), and by the perk factor. This mirrors vanilla's private daily roll in `PrisonerReleaseCampaignBehavior` (v1.5.4 :201-245). The perk factor is vanilla's relative terms, which the adapter reads with vanilla's own calls under vanilla's conditions: the governor's Sweet Talker, Dungeon Architect and Mounted Patrols for a town or castle captor; for a mobile captor the captive's Fleet Footed, the party's Mounted Patrols, Ransom Broker and Keen Sight, and the Valor of the party's or its army's leader. It is `1 + the sum of those factors`; at or below 0 vanilla's chance is 0, and so is the extra one, so a captor's anti-escape perks hold for the boost too.
- **Exclusions:** a lord is skipped when not alive, not a prisoner, the main hero, or in the player clan; when the captor is in a map event or a siege (these two are TAOM's own exclusions, stricter than vanilla); when vanilla's `CanHeroBeReleased` veto says no (the adapter fails closed if there is no event dispatcher); when the perk factor is not above 0; and when the multiplier is not finite or not above 1. Eliminated kingdoms are not scanned.
- **Effect:** `IPrisonerEscapeAdapter.Escape` re-finds the hero and calls `EndCaptivityAction.ApplyByEscape`. A throwing read or escape is logged and skipped.

### The rally

`RallyService` (`Rally/RallyService.cs`) runs once per tick over the kingdom snapshots.

- **Measure:** `KingdomWarSnapshotAdapter` (`Main/Adapters/KingdomWarSnapshotAdapter.cs`) reads each non-eliminated kingdom's `Fiefs`. Fortification points are 2 per town and 1 per castle (`KingdomWarSnapshot.FortificationPoints`). Loss is `max(0, (baseline - points) / baseline)`, and is null without a baseline above zero. The rally reads live state each day, so it subscribes to no ownership event.
- **Baseline:** `WarBaselineService` (`Rally/WarBaselineService.cs`) takes a kingdom's points the first time a daily tick sees it: every kingdom on the first tick of a new campaign (day 1, after the campaign clock's one-day initial wait), a later rebel or player-founded kingdom when it first appears. Baselines are saved. Lateness is derived from the data, not saved: when no baseline is held and the day is past 1, the take is late, and `WarChronicleTickService` logs one warning ("loss is measured from today's map"). That covers a save from before this feature (the engine never calls `SyncData` for a behavior the save has no record of, `CampaignBehaviorDataStore.LoadBehaviorData`), a corrupt baselines section and a skipped first tick. `WarBaselineService.LossOf` is the one loss derivation; the rally and the ledger's `loss` key both use it.
- **Tiers and hysteresis:** `RallyTierMachine.Next` (`Rally/RallyTierMachine.cs`). With defaults: tier 2 at 50% lost (a jump from tier 0 is allowed), tier 1 at 25% lost; tier 2 falls to 1 below 40%; any tier falls to 0 below 15%. Between an exit and its enter the tier holds. A non-finite loss gives tier 0. A kingdom with no loss to measure has tier 0.
- **Tracked always, applied conditionally:** the tier is stored (`RallyTierStore`) for every kingdom with a baseline, whether the rally is on or the kingdom is eligible, so a control run's ledger shows what would have fired. A tier change writes an `ev=tier` ledger line.
- **Eligibility:** AI-ruled (not `IsPlayerRuled`), at war with at least one other non-eliminated kingdom, and either `includeNeutral` or an alignment side other than Neutral. The rally is active only when both the `enabled` field in `rally.json` and the MCM `Enable Rally` are true.
- **Writing:** for an eligible kingdom at tier 1 or 2, two effects with source `rally:<kingdomId>`: `VolunteerRate` and `PrisonerEscape`, with the tier's magnitudes (a zero magnitude is skipped), ending `effectTtlHours` from now. They are refreshed daily. On a tier change the source is removed first. Any `rally:` source not kept this tick is removed, so a tier drop, the end of a war, a destroyed kingdom or the switch turned off all clear the effects the same way. A kingdom that left the map has its tier row dropped.
- **Guards:** an empty snapshot list is ignored (it would wipe every tier), and a non-finite clock skips the day.

### The war ledger

`WarLedgerService` (`Ledger/WarLedgerService.cs`) and `WarLedgerFormatter` (`Ledger/WarLedgerFormatter.cs`) write through `IModLogger.LogInfo`, so the lines land in `taom_debug_*.log`. The ledger is stateless: every number comes from the snapshots, the baselines, the tier store, the effect registry and the phase machine, so a reload re-emits a day and the analyzer keeps the last. It has no MCM switch; it runs on every authority tick. Lines are space-separated `key=value` tokens, numbers in the invariant culture, and any id goes through `Token` (whitespace and `=` become `_`). Header: `[WarLedger] v=1`.

| Line | Keys, in order |
|---|---|
| `t=kingdom` (one per kingdom per day) | `cid day phase k side ai war towns castles pts base loss cap str pris tier vr pe` |
| `t=side` (free, evil, neutral) | `cid day side pts base alive` |
| `t=share` | `cid day share` (Free points over Free plus Evil points, `na` when both are 0) |
| `t=event ev=tier` | `cid day k from to loss` |
| `t=event ev=destroyed` | `cid day k` |
| `t=event ev=chronicle` | `cid day id outcome` |

`cid` is `Campaign.UniqueGameId`, `day` the elapsed days since campaign start, `phase` the War of the Ring phase name. `ai` and `war` are 0 or 1; `cap` is 1, 0, or `na` (whether the kingdom's initial home settlement is still held); `str` is the kingdom's total strength as a whole number, or -1 when the engine value is not finite or negative; `pris` counts the kingdom's clan heroes who are prisoners; `vr` and `pe` are the current volunteer and escape multipliers to two decimals, written after that tick's rally and escape pass, so `pe` is the multiplier the day's roll used. `pris` is read before the escape pass, so it still counts lords who escaped that tick. `ev=chronicle` has a formatter and a `LogChronicleResolution` method, but nothing calls it yet. A change to a line is a format change: bump `v` and teach the analyzer.

### The save payload

`WarChronicleStateService` (`WarChronicleStateService.cs`) assembles one JSON payload through `WarChronicleSaveCodec` (`WarChronicleSaveCodec.cs`): `{"v":1,"effects":[{"s","k","kind","m","end"}],"baselines":{},"rally":{"tiers":{}},"chronicle":[]}`. The `chronicle` array is reserved: it is carried through a load and a save unchanged and nothing interprets it yet.

- **Chunking:** `MomentumSyncChunker.Split` cuts the string into chunks of at most 10,000 characters, because one `SyncData` string over 32,767 UTF-8 bytes corrupts the save at write time. `WarChronicleBehavior` writes `_taom_war_chronicle_v1_count` and `_taom_war_chronicle_v1_{i}`. A count below 0 or above 4,096 is ignored with a warning.
- **Forgiving parse:** an unreadable payload, a non-object or an unknown `v` resets every section. A section that does not parse resets alone, and a bad row inside a section (an unknown effect kind, a non-finite number, a rally tier outside 0 to 2) is skipped alone; each leaves a `[WarChronicle] Save:` warning. An empty payload (a corrupt or out-of-range chunk count) restores nothing; a save from before this feature never reaches `SyncData` at all. A development save that still carries the dropped `baselinesMidCampaign` key loads fine, because unknown keys are ignored.

### Component flow

```
rally.json            MCM (War of the Ring/Rally)
    |                       |
RallyConfigProvider   WarChronicleSettingsProvider
        \                  /
   DailyTickEvent -> WarChronicleBehavior -> WarChronicleTickService
                                   |
        effects.Expire -> WarBaselineService -> RallyService -> IWarEffectService
        WarEscapeDailyPass -> IPrisonerEscapeAdapter
        WarLedgerService -> taom_debug_*.log -> tools/analyze_war_ledger.py

TaomVolunteerModel ------------------------\
CastleNotableMaintainer -> CastleRecruitmentService -> VolunteerProductionService -> IWarEffectService.GetMultiplier
```

## Configuration

### Config file: `Main/_Module/ModuleData/war_chronicle/rally.json`

Loaded by `RallyConfigProvider` (`Rally/RallyConfigProvider.cs`). A missing file or a parse failure uses the compiled defaults (`Rally/RallyConfig.cs`). Every float is finite-checked before its range check; a bad field reverts to its default with a warning, and one summary warning follows any revert.

| Field | Type | Default | Validation |
|---|---|---|---|
| `enabled` | bool | true | Master switch in the file; the MCM `Enable Rally` must also be on |
| `includeNeutral` | bool | true | Whether Neutral-side kingdoms can receive effects |
| `effectTtlHours` | float | 48 | Finite, 24 to 168 |
| `tier1.enterLoss` | float | 0.25 | Finite, above 0 and below 1 |
| `tier1.exitLoss` | float | 0.15 | Same |
| `tier1.volunteerRate` | float | 0.10 | Finite, 0 to 2 |
| `tier1.prisonerEscape` | float | 0.50 | Finite, 0 to 2 |
| `tier2.enterLoss` | float | 0.50 | As tier 1 |
| `tier2.exitLoss` | float | 0.40 | As tier 1 |
| `tier2.volunteerRate` | float | 0.20 | As tier 1 |
| `tier2.prisonerEscape` | float | 1.00 | As tier 1 |

Ordering rules cannot be repaired field by field, so a broken ordering reverts its whole group. For the four loss thresholds: each tier's `exitLoss` must be below its `enterLoss`, tier 1's `enterLoss` must not exceed tier 2's, and tier 1's `exitLoss` must not exceed tier 2's. For the four magnitudes: tier 2's `volunteerRate` and `prisonerEscape` must not be below tier 1's.

Magnitudes are added to the registry sums before the MCM strength, so with strength 1 the defaults mean tier 1: volunteer roll x1.10, escape x1.50; tier 2: volunteer roll x1.20, escape x2.00 (inside the 0.5 to 1.5 and 1.0 to 3.0 clamps). The volunteer multiplier scales a per-slot daily probability, not a volunteer count; the escape multiplier scales vanilla's own chance, perks and Valor included.

### MCM settings (`Main/Features/TaomSettings.cs`)

Both sit in the group `War of the Ring/Rally`, with `RequireRestart = false`.

| Setting | Property | Default | Notes |
|---|---|---|---|
| Enable Rally | `WarRallyEnabled` | true | Takes effect at the next daily tick; off removes every rally effect |
| War Effect Strength | `WarEffectStrength` | 1.0 | Slider 0 to 2; scales every effect, so it applies to a future event chain as well. 0 turns effects off |

`WarChronicleSettingsProvider` re-validates the strength (non-finite to 1, else clamp 0 to 2) and takes the settings object once, lazily, on first read; it is registered in `CampaignHotPathSettingsProvidersTests`.

### Reload scope

`RallyConfigProvider` is a `Reuse.Singleton` with a `Lazy` load, so it reads `rally.json` once per Bannerlord process. An edit needs a full game restart, not a new campaign or a save and load. The MCM values are live: both are read on the daily tick (the strength by the re-bake in `Expire`, the switch by `RallyService.IsActive`), so a change applies at the next daily tick without a restart.

## Console commands

Registered in `Main/Features/WarChronicle/Cheats/WarChronicleCheats.cs`, all through `TaomConsole.RunInCampaign` (campaign plus cheat mode). Full rows in [dev-console.md](dev-console.md).

| Command | Effect |
|---|---|
| `taom.war_effects` | Lists every active effect with its time left, and each affected kingdom's volunteer and escape multiplier (MCM strength applied) |
| `taom.war_effect_add <kingdomId> <VolunteerRate\|PrisonerEscape> <magnitude> <days>` | Adds an effect from source `console`: magnitude -1 to 1, days above 0 up to 365; the kingdom must exist in the campaign. Echoes the multiplier before and after. Refused on a BannerlordCoop client |
| `taom.rally_status` | Per kingdom: points, baseline, loss, stored tier and eligibility, plus whether the rally is on |
| `taom.print_war_ledger` | Prints the ledger lines today's tick would write; logs and changes nothing |

## How to measure

The ledger is the proof that the rally changes anything.

1. **Run an observer campaign.** Start a campaign with the War of the Ring test mode (short phase days, see [war-of-the-ring.md](war-of-the-ring.md)), park the player somewhere harmless, and fast-forward with the TAOM time tiers for a few hundred in-game days. One run per setting: for example the MCM `Enable Rally` off for a control and on for the test.
2. **Read the log.** The default is the newest `taom_debug_*.log` under the game's `bin/Win64_Shipping_Client/Logs/`; `BANNERLORD_GAME_DIR` overrides the install path. Run `python tools/analyze_war_ledger.py [logs ...] [--cid ID]`. The tool is read-only, groups by campaign id, reads the logs oldest first, and keeps the last line per kingdom and day, so reloading the same save does not double-count. A reload of an OLDER save still leaves the abandoned run's later days and events in the report, because the log carries no marker for the rewind yet. A malformed line is skipped and counted, and the first one is printed as a warning. Exit codes: 0 success; 1 no ledger lines, an unknown cid, an unreadable log or an unwritable `--csv` path; 2 an unknown `v` or a usage error.
3. **Metrics in the summary:** days covered; the Free share series (mean deviation from 0.5, maximum deviation and its day, fraction of days in 0.4 to 0.6, lead changes with a dead band of 0.48 to 0.52); first capital loss per kingdom and per side; kingdoms destroyed; fief churn per 10-day bucket; days at tier 1 and tier 2 and tier changes per kingdom; net change per side; chronicle outcomes (empty today); and a target check, (a) share in 0.4 to 0.6 on at least 80% of non-na days, (b) no capital lost before day 120, with four verdicts: FAIL (a loss seen before day 120), PASS (capital states cover every day 1 to 119 and none was lost), NO DATA (no line carries a capital state) and UNKNOWN (a hole in days 1 to 119).
4. **Compare two runs.** `--compare off.log on.log` (each argument is `path` or `path:cid`; with no cid the campaign with the most days is used) prints each metric for A, B and B minus A, then the target verdicts for each. `--compare` reads one file per side (concatenate a run's logs, oldest first) and refuses `--csv`, `--cid` or extra log paths. `--csv PATH` writes the per-day series.

The analyzer's tests are `tools/tests/test_analyze_war_ledger.py` (run `python -m unittest tools.tests.test_analyze_war_ledger` from the repo root; `/verify`'s `python -m unittest discover -s tools/tests -t .` covers them too).

## Key files

| File | Purpose |
|---|---|
| `Main/Features/WarChronicle/WarChronicleModule.cs` | Feature module, behavior declaration |
| `Main/Features/WarChronicle/WarChronicleBehavior.cs` | Per-campaign behavior: daily tick, destroyed line, SyncData |
| `Main/Features/WarChronicle/WarChronicleTickService.cs` | Tick order |
| `Main/Features/WarChronicle/WarChronicleStateService.cs`, `WarChronicleSaveCodec.cs`, `WarChroniclePayload.cs` | Save payload, chunking, parsing |
| `Main/Features/WarChronicle/Effects/` | Registry, math, escape service and daily pass |
| `Main/Features/WarChronicle/Rally/` | Baselines, tier machine and store, rally service, config provider |
| `Main/Features/WarChronicle/Ledger/` | Ledger service and formatter |
| `Main/Features/WarChronicle/Cheats/` | Console commands and their report text |
| `Main/Features/WarChronicle/WarChronicleIoC.cs`, `WarChronicleSettingsProvider.cs` | Registrations, MCM provider |
| `Main/Adapters/KingdomWarSnapshotAdapter.cs`, `KingdomWarSnapshot.cs` | Kingdom read model |
| `Main/Adapters/PrisonerEscapeAdapter.cs`, `PrisonerEscapeSnapshot.cs` | Captured-lord read model and the escape call |
| `Main/Features/TroopProgression/VolunteerProductionService.cs`, `Models/TaomVolunteerModel.cs` | Volunteer consumer, towns and villages |
| `Main/Features/CastleRecruitment/CastleRecruitmentService.cs`, `Hooks/CastleNotableMaintainer.cs` | Volunteer consumer, castles |
| `Main/_Module/ModuleData/war_chronicle/rally.json` | Rally data |
| `tools/analyze_war_ledger.py` | Ledger analyzer |

## Dependencies

- `IWarOfTheRingService` (Diplomacy): the current phase, for the ledger's `phase` key.
- `IAlignmentService` (Execution): `ResolveSide`, for the ledger's `side` key and the rally's Neutral eligibility check.
- `ICoopSessionProvider` and `CoopSessionPolicy` (CoopInterop): the authority and save-write gates in the daily tick and in `taom.war_effect_add`.
- `MomentumSyncChunker` (WarOfTheRingMomentum): SyncData chunking.
- `TaomConsole` (DevConsole): the four commands.
- `IKingdomWarSnapshotAdapter` and `IPrisonerEscapeAdapter` (Adapters, registered by this feature): kingdom fiefs and strength; captured lords, their escape factors and `EndCaptivityAction.ApplyByEscape`.
- `IPathService` and `IModLogger` (Core), and `TaomSettings` through `WarChronicleSettingsProvider` (MCM).

**Used by:** `TaomVolunteerModel` (TroopProgression) and `CastleRecruitmentService` (CastleRecruitment) read `IWarEffectService` through `VolunteerProductionService`. Only `WarChronicleIoC` registers that service, so `WarChronicleModule` must stay in `FeatureModules.All`, or resolving `VolunteerProductionService` in SubModule fails. `VolunteerProductionWiringTests` validates the graph.

## Tests

Counts measured by grepping `[TestMethod]` and `[DataTestMethod]` attribute lines; they exclude `[DataRow]` expansions. The 316 under `TAOM.Tests/Features/WarChronicle/` plus 35 outside it make 351 C# tests, of which 59 are `[DataTestMethod]`.

| Class | File | Tests |
|---|---|---|
| `WarEffectServiceTests` | `TAOM.Tests/Features/WarChronicle/Effects/` | 33 |
| `WarEscapeServiceTests` | same | 32 |
| `WarEscapeDailyPassTests` | same | 13 |
| `RallyServiceTests` | `.../Rally/` | 41 |
| `RallyConfigProviderTests` | same | 29 |
| `RallyTierMachineTests` | same | 14 |
| `RallyTierStoreTests` | same | 10 |
| `WarBaselineServiceTests` | same | 16 |
| `WarLedgerFormatterTests` | `.../Ledger/` | 18 |
| `WarLedgerServiceTests` | same | 16 |
| `WarChronicleCheatReportTests` | `.../Cheats/` | 19 |
| `WarChronicleSaveCodecTests` | `.../WarChronicle/` | 25 |
| `WarChronicleStateServiceTests` | same | 10 |
| `WarChronicleTickServiceTests` | same | 15 |
| `WarChronicleBehaviorTests` | same | 8 |
| `WarChronicleSettingsProviderTests` | same | 7 |
| `WarChronicleIoCTests` | same | 5 |
| `WarChronicleWiringTests` | same | 5 |
| `VolunteerProductionServiceTests` | `TAOM.Tests/Features/TroopProgression/` | 21 |
| `VolunteerProductionWiringTests` | same | 1 |
| `PrisonerEscapeAdapterTests` | `TAOM.Tests/Adapters/` | 4 |
| `KingdomWarSnapshotAdapterTests` | same | 9 |

`WarChronicleSettingsProvider` is also a row in `CampaignHotPathSettingsProvidersTests`, and `CastleRecruitmentServiceTests` covers the castle consumer. The analyzer has 64 Python tests (`def test_` count) in `tools/tests/test_analyze_war_ledger.py`.

## Known limitations

- **Old saves get a mid-campaign baseline.** A save from before this feature has no baselines, so they are taken at the first tick after the load (the engine never calls `SyncData` for it), and the rally measures loss from that day's map. It under-fires on such a campaign; a warning is logged once, because the take happens past day 1 with no baselines held.
- **The player still meets effects indirectly.** No effect is keyed to the player's own clan, but:
  - A kingdom the player serves as vassal or mercenary can rally: only `RulingClan == PlayerClan` is skipped, and mercenary service sets `clan.Kingdom`. The rule is ruler-only, as in `PlayerKingdomHelper`.
  - The extra volunteers sit in shared notable slots, so the player can recruit volunteers a rallying kingdom produced.
  - A rallying kingdom's lord held by the player escapes faster: at half the extra chance in vanilla's three player-held cases, and at the full extra chance when another party of the player's clan holds him outside a player-clan settlement.
  - A kingdom whose throne passes to the player keeps its rally effects until that tick's rally pass, which now runs before the escape pass, so its lords get no further boosted roll.
- **Co-op: host-only under BannerlordCoop.** Under BannerlordCoop the tick, the escape pass, the rally, the baselines and the ledger run only on the host (`IsAuthority` and `MayWriteSaveBackedState`), `taom.war_effect_add` is refused on a client, and a client gets the host's state from the save at join and nothing after (see [coop-interop.md](coop-interop.md), "No sync of TAOM's own campaign state"). The volunteer consumer does not run on a BannerlordCoop client either: it is reached only from `DailyTickSettlementEvent` (and new-game creation), and that per-entity ticker never reaches a client. Under BannerlordTogether, which TAOM cannot probe, `IsAuthority` fails open and `IsCoopClient` reads false: every peer then runs the daily tick with its own escape rolls (`EndCaptivityAction.ApplyByEscape`) and its own rally writes, and the console refusal does not fire (UNVERIFIED whether that mod's client-side suppression contains it). This follows the `IsAuthority` policy in coop-interop.md.
- **Effects show no tooltip line.** The volunteer probability is a plain float and the escape is an internal roll, so neither has a tooltip to carry a line. `taom.war_effects` and the ledger are the only views. (The planned event announcements will cover the player-facing side.)
- **Small by design.** Effects touch volunteers and escapes only. They do not change a troop's combat power or auto-resolve scoring, though waiting recruits reach higher tiers sooner (the volunteer roll also gates promotion), so they cushion a skewed war rather than reverse it. Vanilla's own low-fief boost for a small kingdom can exceed the rally's x1.10 and x1.20 volunteer multipliers; the `vr` ledger column shows only the War Chronicle multiplier.
- **Not built yet (#765):** the event chain, the army-focus window and the Eye of Sauron hunt. The `chronicle` section of the save and the `ev=chronicle` ledger line are reserved for them.

## Changelog

- 2026-10-08: first slice (#765): the timed-effect registry with its volunteer and escape consumers, the rally, the war ledger and analyzer, the save payload and four console commands.

## GitHub Issue

- **Issue:** #765
- **Status:** Open (the event chain, army focus and Eye of Sauron hunt remain)
