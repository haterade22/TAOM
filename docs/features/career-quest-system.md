# Career-Tied Quest System

## Overview

Choosing a career can now drive a **story arc**: a career tier quest with trackable objectives that, when completed, unlocks that tier of the career's choice tree (**in addition to** the level gate — *hybrid*) and grants a unique reward. Adapted from TheOldRealms (TOR_Core, GPLv3, with permission) and rebuilt **data-driven** on TAOM's adapter/service architecture, verified against Bannerlord 1.4.5 (TOR's reference is 1.3.15).

Phase 1 ships the framework + one Gondor proof-of-life quest; all other careers/tiers fall through to the level gate unchanged.

## Why This Exists

The [Career System](career-system.md) was purely mechanical: tiers unlocked by hero level alone (T1@1 / T2@10 / T3@20), with no campaign content tied to a career. This makes the career an identity *journey* — "earn your rank through deeds" — without blocking players who don't quest (the level gate stays a floor).

## Architecture

**Hybrid tier gate.** `CareerScreenVM` consults `ICareerQuestService.IsTierUnlocked(heroLevel, tier, heroId)` = `registry.IsTierAvailable(level, tier)` **OR** `dataService.IsTierUnlocked(heroId, tier)`. A completed quest's `UnlockTier` reward calls the existing `ICareerDataService.UnlockTier` (persisted in `_taom_careerTiers`), so the tier-unlock survives saves for free. The hybrid lives in the **quest service, not the registry** — injecting the service into the registry would be circular (the service depends on the registry); the registry's level gate stays pure. The VM takes the quest service as an *optional* ctor param (null → registry-only fallback, keeping unit tests untouched).

**Layers (TAOM stack):**
- **Data** — `taom_career_quests.xml` → `CareerQuestConfigProvider` (validating; skip-and-warn per the config-validation rule) → domain `CareerQuestDefinition` / `CareerQuestObjectiveDefinition` / `CareerQuestRewardDefinition`.
- **Logic** — `CareerQuestService` (pure, 100% unit-tested): quest lookup, hybrid gate, per-type progress math, completion, reward application.
- **Engine shell** — `CareerQuest : QuestBase` (thin): saveable progress + journal logs, forwards verified 1.4.5 campaign events / ticks to the service. Count objectives are event-driven; threshold objectives (skill/renown/gold) are polled in `DailyTick`. Registered for save via the auto-discovered `CareerQuestSaveableTypeDefiner` (base id 726900701).
- **Entry trigger** — `CareerQuestCampaignBehavior` (thin): on session-launch + daily, offers the lowest not-yet-done tier's quest via inquiry; accept → `StartQuest`. A declined quest is remembered (flat-dict SyncData) so it isn't re-offered.
- **Adapter (ADR-007)** — `IQuestHeroAdapter` (reads skill/renown/gold; sinks renown/influence/item); the service never touches `Hero`.

**1.4.5 verification (TOR is 1.3.15).** Every engine API was decompiled against the installed DLLs before use (4-cluster verification pass). Key drift caught: **`InquiryData` + `InformationManager` moved `TaleWorlds.Core` → `TaleWorlds.Library`**; `TournamentFinished` winner is a `CharacterObject`; `SettlementEntered` (not `OnSettlementEntered`); `SetDialogs`/`InitializeQuestOnGameLoad` are `protected abstract`; `QuestBase` has its own `HourlyTick`/`DailyTick` (poll there, not via `CampaignEvents`); hero lookup via `Campaign.Current.CampaignObjectManager.Find<Hero>`. `QuestDueTime` is absolute → use `CampaignTime.DaysFromNow`, not `CampaignTime.Years`.

## Configuration

`Main/_Module/ModuleData/career_system/taom_career_quests.xml` — read directly by the provider (no `SubModule.xml` registration). One `<CareerQuest>` per `(career_id, tier)`:

| Objective `type` | Mechanism | `param` |
|---|---|---|
| `WinBattles`, `SettlementsCaptured`, `TournamentsWon`, `DefeatEnemyLords` | event count | — |
| `VisitSettlementType` | event count | `Town`/`Castle`/`Village` |
| `SkillThreshold` | daily poll | skill id (e.g. `OneHanded`) |
| `RenownThreshold`, `GoldAccumulated` | daily poll | — |

| Reward `type` | Effect |
|---|---|
| `UnlockTier` (`amount`=1-3) | unlocks the tier (the hybrid hook) |
| `GrantRenown` / `GrantInfluence` (`amount`) | clan renown / influence |
| `GrantItem` (`param`=item id, `amount`) | item to the player's party |
| `GrantAttributeFlag` (`param`) | a flag in `HeroCareerData.Flags` for downstream gating |

Invalid quests/objectives/rewards are skipped with a warning; a quest with no valid objectives is dropped. Careers/tiers with no entry → level gate only.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CareerSystem/Domain/CareerQuest{Definition,ObjectiveDefinition,RewardDefinition}.cs` | data model + enums |
| `Main/Features/CareerSystem/{I,}CareerQuestConfigProvider.cs` | validating XML loader |
| `Main/Features/CareerSystem/{I,}CareerQuestService.cs` | all logic (pure) |
| `Main/Features/CareerSystem/Quests/CareerQuest.cs` | `QuestBase` shell |
| `Main/Features/CareerSystem/Quests/CareerQuestSaveableTypeDefiner.cs` | save registration |
| `Main/Features/CareerSystem/Quests/CareerQuestCampaignBehavior.cs` | offer/start trigger |
| `Main/Adapters/{I,}QuestHeroAdapter.cs`, `{I,}QuestHeroAdapterFactory.cs` | hero boundary |
| `Main/_Module/ModuleData/career_system/taom_career_quests.xml` | quest data (Gondor proof-of-life) |
| `CareerScreenVM.cs` / `GauntletCareerScreen.cs` | hybrid-gate wiring |
| `HeroCareerData.cs` / `CareerDataService.cs` / `CareerPersistenceBehavior.cs` | `Flags` storage + persistence |

## Dependencies

The existing Career System (`ICareerDataService`, `ICareerRegistry`), and vanilla `QuestBase` / `CampaignEvents` / `SaveableTypeDefiner`.

## Tests

`TAOM.Tests/Features/CareerSystem/CareerQuestServiceTests.cs` (service: gate / per-objective-type progress / per-reward-type application / completion), `CareerQuestConfigProviderTests.cs` (validation, one per rule), flag tests in `CareerDataServiceTests.cs` + `CareerPersistenceTests.cs`. The engine shell + behavior are thin entry points (verified in-game). Full suite green (2874 passed).

## How-To: add a career quest

1. Add a `<CareerQuest career_id="…" tier="…">` to `taom_career_quests.xml` with objectives + rewards (table above).
2. Add the `{=key}` strings to `taom_module_strings.xml` and run `/localize xml`.
3. If an objective uses a new mechanic, add the enum value + a `ComputeProgress` branch (count vs threshold) + the event subscription (`RegisterEvents`) or poll (`DailyTick`) in `CareerQuest`, **verifying the `CampaignEvents` signature on the installed engine first**.

## Status / not done

- **In-game testing pending** — the `QuestBase` shell, inquiry offer, event tracking, completion + save/load are only confirmable in the live game (entry points, not unit-tested). `CareerQuest` overrides `SpecialQuestType` so the engine doesn't auto-cancel it on load (it has no associated `IssueBase`).
- **Known limitations:** editing a quest's objective list in XML while a save has that quest *in progress* can soft-lock it (the saved progress slots don't resize on load) — change objectives only between playthroughs. A `SkillThreshold` id that doesn't resolve to a real skill logs a warning at quest start and never progresses (fix the id). `VisitSettlementType` counts each entry, not distinct settlements.
- Phase 2: author quests for the remaining careers; NPC turn-in dialogs (`SetDialogs`) + a `giver_kind`/quest-giver concept; item/companion rewards; an MCM disable toggle; the 11-language translation run.
