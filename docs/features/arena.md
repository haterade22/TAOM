# Arena (Tournament Model + Dwarf Dismount + Exit-Hang Fix)

## Overview

Replaces vanilla's tournament model with a culture-aware, race-aware variant. Four concerns:

1. **Per-participant culture armor** — participants wear armor matching their *own* culture (a dwarf gets dwarf gear, not the host town's human kit), preventing skeleton-clipping. Data-driven via `gear_practice_dummy_<culture>` NPCs — see [tournament-armor-assignment.md](tournament-armor-assignment.md).
2. **Culture-filtered prize pools** — rewards are drawn from the host town's culture across two tiers (Tierf 2–4 regular, Tierf 4+ elite).
3. **Dwarf dismount (Patch46, 2026-06-09)** — dwarves never fight mounted in tournaments. Their custom (shorter) skeleton clips them *inside* the horse mesh. A Harmony postfix strips the horse from dwarf participants.
4. **Tournament-exit hang fix (Patch60 + PatchShield exclusion, 2026-07-06→10, #331)** — exiting any tournament froze the exit 30s–2min (measured 104–109s, three times). **Round 1 (Patch60):** engine defect — `MissionGauntletTournamentView.OnMissionScreenFinalize` nulls its `_gauntletMovie`/`_gauntletLayer` **without releasing them** (the practice view releases correctly). [Patch60_TournamentExitMovieRelease](../../Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs) captures the layer/movie in a Prefix (the original body nulls the fields) and, in a Postfix — after the body has dropped focus + finalized the VM, so `TryLoseFocus` can't NRE — replicates the practice view's `ReleaseMovie` → `RemoveLayer` sequence at `OnEndMission` time. Fail-safe → vanilla leak; drift-guard tests pin the bindings; its per-exit `ReleaseMovie=Nms` log line is the permanent regression canary. **Necessary but not sufficient:** the ~107s moved WITH the relocated release, proving the release itself was the sink. **Round 2 (the real fix):** the `ExitStallSampler` stack-sampled the frozen main thread and named a three-factor interaction — the tournament UI's per-round template re-instantiation accumulates `WidgetTemplate._customTypeChildren` into a ~10^6-call release recursion; UIExtenderEx legitimately patches `WidgetFactory.IsCustomType`/`WidgetTemplate.OnRelease`; and TAOM.Dependencies' **PatchShield** stacked a `__originalMethod`-binding finalizer on every patched method, adding ~50µs of reflection per call. Fix: `PatchShield.ExcludedTargetNamespacePrefixes` never shields `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`. **Measured: 105–109s → 9.5s** (residual = UIExtenderEx's legitimate wrapper at ~10^6 calls; accepted). Full chain: [rca-tournament-exit-hang-2026-07-06.md](../reviews/rca-tournament-exit-hang-2026-07-06.md) round-2 section.

Tournament start/end timing constants are also exposed for tuning.

## Why This Exists

- **Vanilla behavior:** [DefaultTournamentModel](E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\GameComponents\DefaultTournamentModel.cs) returns participant-agnostic armor — every participant in a town tournament wears the host culture's kit regardless of their own. Reward items come from a global pool with no cultural filtering. And the tournament *weapon* templates (`CultureObject.TournamentTeamTemplatesFor{One,Two,Four}Participant`, or the `tournament_template_empire_*` fallback) include mounted loadouts, so some participants are spawned on horseback.
- **TAOM requirement:** TAOM ships per-race skeletons (dwarves, elves, hobbits) via [hero-race.md](hero-race.md). (a) Human armor on a dwarf skeleton clips through the body. (b) A **mounted** dwarf is worse: the dwarf's custom rider bone is misaligned, so the dwarf model spawns *inside* the horse — the same defect the `EyeHeightAdjustmentHook` eye-height workaround exists for. Each culture also has LOTR-themed weapon/armor families that tournament rewards should reflect.
- **Without this feature:** armor clipping during tournaments + thematic wrong-faction reward items + **dwarves visibly stuck inside horses** when their loadout rolls a mounted template.

## Architecture

> **Architecture note (Phase 9b #137):** all decision logic was extracted from the GameModel into [`TournamentService`](../../Main/Features/Arena/TournamentService.cs) to satisfy the rule-4 "no inline branching in GameModel overrides" constraint ([.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)). [`TaomTournamentModel`](../../Main/Features/Arena/Models/TaomTournamentModel.cs) is now a **thin entry point** that converts sealed TaleWorlds params to primitives at the boundary and delegates to the injected `ITournamentService`. Earlier revisions of this doc described logic living on the model — that is no longer accurate.

### Design Challenge

`DefaultTournamentModel.GetParticipantArmor` takes a `CharacterObject` but returns a single `Equipment` built from the host town's culture — no per-participant resolution. **The mount is a separate problem entirely:** `GetParticipantArmor` only governs armor/clothing (slots 5–9). The horse (slot 10) comes from a *different* path — `TournamentFightMissionController.PrepareForMatch` clones the culture weapon template into each `participant.MatchEquipment`, and `AddRandomClothes` (which calls `GetParticipantArmor`) copies only slots 5–9 on top. So overriding `GetParticipantArmor` can never remove a horse — that required a separate Harmony patch (Patch46).

### Solution Approach

Two extension points, both delegating to `ITournamentService`:

**(A) GameModel override** — [TaomTournamentModel](../../Main/Features/Arena/Models/TaomTournamentModel.cs) inherits `DefaultTournamentModel` and overrides five methods:

| Override | Delegates to | Behavior |
|---|---|---|
| `GetParticipantArmor(CharacterObject)` | `ResolveDummyId` | Resolves a `gear_practice_dummy_<culture>` NPC by the participant's culture, returns its `RandomBattleEquipment`; falls through to base if not found. |
| `GetRegularRewardItems(Town, …)` | `BuildPrizePool` | Prize pool from `Items.All` filtered to town culture, Tierf ∈ [2, 4), excluding non-merchandise / non-weapon-armor / horses. Falls through to base if empty. |
| `GetEliteRewardItems(Town, …)` | `BuildPrizePool` | Same builder, Tierf ∈ [4, ∞). |
| `GetTournamentStartChance(Town)` | `CalculateStartChance` | Boundary computes lord count; service maps it: 0→0%, 1→45%, 2→75%, 3→90%, 4+→100%. Returns 0% if the town is under siege or outside a campaign. |
| `GetTournamentEndChance(TournamentGame)` | `CalculateEndChance` | After a 20-day grace period, ramps end-chance by 3.3%/day elapsed. |

**(B) Harmony postfix (Patch46_TournamentDwarfDismount)** — [Patch46_TournamentDwarfDismount](../../Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs) postfixes the public `TournamentFightMissionController.PrepareForMatch`. After vanilla assigns every participant's `MatchEquipment`, it iterates all teams/participants and, for any participant whose race `ShouldDismountInTournament` returns true (currently dwarves), clears `EquipmentIndex.Horse` + `EquipmentIndex.HorseHarness` via `AddEquipmentToSlotWithoutAgent(slot, EquipmentElement.Invalid)`. `PrepareForMatch` is the single chokepoint feeding **both** the visual spawn (`SpawnAgentWithRandomItems`) and the AI simulation (`Simulate` → `GetSimulationAttackPower`), so a dwarf is never treated as cavalry anywhere in the tournament. Keyed on **race, not culture**, so a dwarf competing in *any* town — and the player, if the player is a dwarf — is caught.

`ShouldDismountInTournament(int raceId)` uses the **validate-before-lookup** pattern ([.claude/rules/csharp-architecture.md](../../.claude/rules/csharp-architecture.md#lookup-functions-with-fallbacks-validate-before-lookup)): `IRaceManager.GetRaceNameFromId` returns `"human"` as a fallback for unknown ids, so the service guards with `IsValidRaceId` *before* the name lookup, then compares to `"dwarf"` case-insensitively. It uses the same `IRaceManager` as [`EyeHeightAdjustmentHook`](../../Main/Features/HeroRace/EyeHeightAdjustmentHook.cs) for the `dwarf` check, but additionally guards with `IsValidRaceId` before the lookup (which that hook does not).

### Component Diagram

```
== (A) GameModel ==
SubModule.OnGameStart  (Main/SubModule.cs:385)
   campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<ITournamentService>()))
        |
TaomTournamentModel : DefaultTournamentModel        ← thin: converts sealed→primitive, delegates
        |                                               (logic lives in TournamentService, #137)
   GetParticipantArmor / GetRegularRewardItems / GetEliteRewardItems
   GetTournamentStartChance / GetTournamentEndChance
        |
   ITournamentService (TournamentService, Reuse.Singleton, injects IRaceManager)
        ├─ ResolveDummyId(cultureId)        → "gear_practice_dummy_<culture>"
        ├─ BuildPrizePool(culture, lo, hi)  → filter Items.All
        ├─ CalculateStartChance / CalculateEndChance
        └─ ShouldDismountInTournament(raceId) → IRaceManager validate + "dwarf" check

== (B) Harmony postfix (Patch46) ==
TournamentFightMissionController.PrepareForMatch()   ← vanilla assigns MatchEquipment (incl. horse)
        | [Postfix]
Patch46_TournamentDwarfDismount.Postfix(____match)   // 4 underscores: ___ prefix + field _match
   foreach team → foreach participant:
      if service.ShouldDismountInTournament(participant.Character.Race):
         clear MatchEquipment[Horse] + [HorseHarness] = EquipmentElement.Invalid
        |
SpawnAgentWithRandomItems / Simulate read the now-horse-free MatchEquipment → dwarf on foot
```

## Configuration

None. All knobs are constants on `TournamentService` / `TaomTournamentModel`. The dwarf-dismount race set is a one-line extension point (`DwarfRaceName` constant) — intentionally not config (per the simplicity criterion; spiders/other non-humanoids are recruitable troops, not tournament heroes — see memory `nonhumanoid-creature-troop-not-mount`).

| Constant | Location | Value | Meaning |
|---|---|---|---|
| `RegularMinTier` / `RegularMaxTier` | `TaomTournamentModel` | `2f` / `4f` | Regular prize pool tier range |
| `EliteMinTier` | `TaomTournamentModel` | `4f` | Elite prize pool floor (no upper bound) |
| `TournamentStartChance1Lord` / `2Lords` / `3Lords` | `TournamentService` | `0.45f` / `0.75f` / `0.90f` | Start probability by lord count |
| `TournamentEndChanceGraceDays` | `TournamentService` | `20f` | Grace before end-chance ramps |
| `TournamentEndChanceRamp` | `TournamentService` | `0.033f` | Per-day end-chance increment after grace |
| `DwarfRaceName` | `TournamentService` | `"dwarf"` | Race name that forces dismount in tournaments |

To change armor or rewards, **edit XML, not code** — add/edit `gear_practice_dummy_<culture>` NPCs in `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/Arena/Models/TaomTournamentModel.cs](../../Main/Features/Arena/Models/TaomTournamentModel.cs) | GameModel override (thin) — 5 overrides, each delegates to `ITournamentService` |
| [Main/Features/Arena/ITournamentService.cs](../../Main/Features/Arena/ITournamentService.cs) | Service interface — `CalculateStartChance` / `CalculateEndChance` / `BuildPrizePool` / `ResolveDummyId` / `ShouldDismountInTournament` |
| [Main/Features/Arena/TournamentService.cs](../../Main/Features/Arena/TournamentService.cs) | Service impl (`Reuse.Singleton`); injects `IRaceManager` for the dwarf check |
| [Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs](../../Main/Features/Arena/Hooks/Patch46_TournamentDwarfDismount.cs) | Harmony postfix — clears Horse/HorseHarness for dwarf participants |
| [Main/Features/Arena/ArenaIoC.cs](../../Main/Features/Arena/ArenaIoC.cs) | `container.Register<ITournamentService, TournamentService>(Reuse.Singleton)` |
| [Main/SubModule.cs:385](../../Main/SubModule.cs) | `AddModel(new TaomTournamentModel(IoC.Resolve<ITournamentService>()))` |
| [Main/SubModule.cs](../../Main/SubModule.cs) | `_harmony.PatchCategory("Patch46_TournamentDwarfDismount")` (next to Patch45_SpiderTroopSpawn) |
| `Main/_Module/ModuleData/characters/npcs_<culture>.xml` | `gear_practice_dummy_<culture>` NPCs (armor data) — see [tournament-armor-assignment.md](tournament-armor-assignment.md) |

## Dependencies

- `TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel` (base class)
- `SandBox.Tournaments.MissionLogics.TournamentFightMissionController` (Patch46 target — SandBox.dll; private field `_match` injected as `____match` — **four** underscores: Harmony's `___` prefix + the field name `_match`. Using three (`___match`) crashed the game on load; see RCA 2026-06-09.)
- `TaleWorlds.CampaignSystem.TournamentGames.{TournamentMatch, TournamentTeam, TournamentParticipant}` (iterated in Patch46)
- `TaleWorlds.Core.{Equipment, EquipmentIndex, EquipmentElement}` (slot clearing)
- [`IRaceManager`](../../Main/Core/Domain/IRaceManager.cs) (TAOM, `Reuse.Singleton`) — race-id → race-name resolution for the dwarf check
- `Game.Current.ObjectManager` (resolves `gear_practice_dummy_<culture>` NPCs) + `Items.All` (prize-pool filtering)

## Tests

- [TAOM.Tests/Features/Arena/TournamentServiceTests.cs](../../TAOM.Tests/Features/Arena/TournamentServiceTests.cs) — **21 tests**: start-chance step function, end-chance ramp, `ResolveDummyId` fallback chain, and **6 for `ShouldDismountInTournament`** (dwarf→true, mixed-case "Dwarf"→true, human/elf/orc→false, invalid race id→false with `DidNotReceive().GetRaceNameFromId` asserting validate-before-lookup). `IRaceManager` is mocked via NSubstitute.
- [TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs](../../TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs) — **7 tests**: tier-constant invariants on the model.

The `Patch46` postfix and the model methods that touch `Game.Current.ObjectManager` / `Items.All` are game-only (not unit-tested per ADR-008). The testable decision logic lives in `TournamentService`.

## How to Add a Tournament Armor Set for a New Culture

1. Open `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.
2. Add/edit a `<NPCCharacter id="gear_practice_dummy_<culture>" …>` with an equipment block using skeleton-appropriate items.
3. Verify item IDs exist in `LOTRLOME_Armory` (missing items → underwear). Run `python tools/validate_moduledata.py`.
4. No code changes — the model resolves the new dummy via `ResolveDummyId` on the next tournament.

## How to Add a New Race to the Dwarf-Dismount Set

If another custom-skeleton race is ever a tournament participant and clips inside mounts, extend the check in [TournamentService.ShouldDismountInTournament](../../Main/Features/Arena/TournamentService.cs) (e.g. compare against a small set of race names instead of the single `DwarfRaceName` constant). Add a unit test mirroring `ShouldDismountInTournament_DwarfRace_ReturnsTrue`. Non-humanoid creatures (spider, etc.) are **troops**, not tournament heroes, so they never hit this path — see memory `nonhumanoid-creature-troop-not-mount`.

## Changelog

- 2026-06-09 — Patch46 dwarf dismount added (`fix(arena)`, #277): postfix on `PrepareForMatch` clears Horse/HorseHarness for dwarf participants so they never spawn inside the mount; same-day hotfix corrected the injected `_match` field from three underscores to four (`____match`) after it crashed every campaign load.
- 2026-05-14 — Phase 9b: decision logic extracted from `TaomTournamentModel` into `ITournamentService` (`CalculateStartChance`/`CalculateEndChance`/`BuildPrizePool`/`ResolveDummyId`), registered via new `ArenaIoC`; model is now a thin boundary (#137).
- 2026-03-31 — Tournament model overhaul (#52): increased tournament frequency (lord-count step curve, 20-day end grace), culture-specific prize pools scanned from `Items.All` by culture + Tierf, and per-participant culture armor via `GetParticipantArmor`; the `gear_practice_dummy_<culture>` rosters that feed it had `civilian="true"` removed and a missing Lothlórien entry added (#51).

## GitHub Issue

- **Patch46 dwarf dismount:** [#277 — fix(arena): dwarves spawn inside the horse as tournament cavalry](https://github.com/haterade22/TAOM/issues/277) (2026-06-09). RCA: [docs/reviews/rca-tournament-dwarf-dismount-2026-06-09.md](../reviews/rca-tournament-dwarf-dismount-2026-06-09.md).
- **Original culture-armor model:** predates the mandatory issue-per-feature policy.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/tournament-armor-assignment.md](./tournament-armor-assignment.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
