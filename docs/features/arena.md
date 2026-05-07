# Arena (Tournament Model)

## Overview

Replaces vanilla's tournament model with a culture-aware variant. Tournament participants wear armor matching their own culture (preventing skeleton-clipping when a dwarf participant gets human armor), and prize pools are filtered to the host town's culture across two tiers (Tier 2-4 for regular rewards, Tier 4+ for elite rewards). Tournament start/end timing constants are also exposed for tuning.

## Why This Exists

- **Vanilla behavior:** [DefaultTournamentModel](E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\GameComponents\DefaultTournamentModel.cs) returns participant-agnostic armor — every participant in a Khuzait town tournament wears the same Khuzait kit regardless of their own culture. Reward items are pulled from a global pool with no cultural filtering.
- **TAOM requirement:** TAOM ships per-race skeletons (Dwarves, Elves, Hobbits) via [hero-race.md](hero-race.md). Putting a human armor mesh on a dwarf skeleton clips the gear through the body. Each culture also has its own LOTR-themed weapon and armor families that tournament rewards should reflect.
- **Without this feature:** Visible armor clipping during tournaments + thematic wrong-faction reward items (e.g., winning a tournament in Mordor and getting a Vlandian sword).

## Architecture

### Design Challenge

`DefaultTournamentModel.GetParticipantArmor` takes a `CharacterObject` but returns a single `Equipment` constructed from the host town's culture. There is no per-participant resolution path in vanilla. Reward generation likewise hardcodes pool selection without exposing a culture filter.

### Solution Approach

Standard GameModel override (see [.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)). [TaomTournamentModel](../../Main/Features/Arena/Models/TaomTournamentModel.cs) inherits `DefaultTournamentModel` and overrides five methods:

| Override | Behavior |
|---|---|
| `GetParticipantArmor(CharacterObject)` | Resolves a `gear_practice_dummy_<culture>` NPCCharacter from `ObjectManager` using the participant's culture (not the town's). Returns its `RandomBattleEquipment`. Falls through to base if the dummy doesn't exist. |
| `GetRegularRewardItems(Town, ...)` | Builds a prize pool from `Items.All` filtered to the town's culture, tier ∈ [2, 4), excluding non-merchandise / non-weapon-armor / horses. Falls through to base if pool is empty. |
| `GetEliteRewardItems(Town, ...)` | Same pool builder, tier ∈ [4, ∞). |
| `GetTournamentStartChance(Town)` | Step function on lord-count: 0 → 0%, 1 → 45%, 2 → 75%, 3 → 90%, 4+ → 100%. Returns 0% if the town is currently under siege. |
| `GetTournamentEndChance(TournamentGame)` | After a 20-day grace period, ramps end-chance by 3.3% per day elapsed. |

Equipment for each culture is data-driven through the existing `gear_practice_dummy_<culture>` NPCs in `npcs_<culture>.xml` — see [tournament-armor-assignment.md](tournament-armor-assignment.md) for the data side. This feature is the **code** that consumes those NPCs; the related doc covers the data authoring.

### Component Diagram

```
SubModule.OnGameStart  (Main/SubModule.cs:280)
        |
campaignStarter.AddModel(new TaomTournamentModel())   ← no constructor args
        |
TaomTournamentModel : DefaultTournamentModel
        |
GetParticipantArmor(participant)
   -> ResolveDummyId(participant.Culture.StringId)    ← "gear_practice_dummy_<culture>"
   -> ObjectManager.GetObject<CharacterObject>(id)
   -> dummy.RandomBattleEquipment                     ← per-culture armor set
        |
GetRegularRewardItems / GetEliteRewardItems
   -> BuildPrizePool(culture, minTier, maxTier)
   -> filter Items.All by culture + tierf range + flags
```

## Configuration

None. All knobs are constants on the model class. Tier thresholds and tournament timing are intentionally hardcoded — they aren't player-tunable.

| Constant | Value | Meaning |
|---|---|---|
| `RegularMinTier` / `RegularMaxTier` | `2f` / `4f` | Regular prize pool tier range |
| `EliteMinTier` | `4f` | Elite prize pool floor (no upper bound) |
| `TournamentStartChance1Lord` / `2Lords` / `3Lords` | `0.45f` / `0.75f` / `0.90f` | Per-frame start probability by lord count |
| `TournamentEndChanceGraceDays` | `20f` | Grace period before end-chance starts ramping |
| `TournamentEndChanceRamp` | `0.033f` | Per-day end-chance increment after grace |

To change armor or rewards, **edit XML, not code** — add/edit `gear_practice_dummy_<culture>` NPCs in `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/Arena/Models/TaomTournamentModel.cs](../../Main/Features/Arena/Models/TaomTournamentModel.cs) | GameModel override (102 lines) — all five method overrides + private `BuildPrizePool` and internal `ResolveDummyId` |
| [Main/SubModule.cs:280](../../Main/SubModule.cs) | `campaignStarter.AddModel(new TaomTournamentModel())` registration |
| `Main/_Module/ModuleData/characters/npcs_<culture>.xml` | `gear_practice_dummy_<culture>` NPCs (data) — see [tournament-armor-assignment.md](tournament-armor-assignment.md) |

No IoC registration — the model has no constructor dependencies.

## Dependencies

- `TaleWorlds.CampaignSystem.GameComponents.DefaultTournamentModel` (base class)
- `TaleWorlds.CampaignSystem.TournamentGames.TournamentGame` (parameter type)
- `Game.Current.ObjectManager` (resolves `gear_practice_dummy_<culture>` NPCs by string ID)
- `Items.All` (item registry) — iterated for prize-pool filtering

## Tests

- [TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs](../../TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs) — **12 tests**: dummy ID fallback chain (participant culture → settlement culture → empire), tier constant invariants, tournament start chance step function, end chance ramp.

The model methods that touch `Game.Current.ObjectManager` and `Items.All` are not directly tested (require a live game) — testable logic lives in the static `ResolveDummyId` and the constant invariants.

## How to Add a Tournament Armor Set for a New Culture

1. Open `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.
2. Add (or edit) a `<NPCCharacter id="gear_practice_dummy_<culture>" ...>` entry with a `RandomBattleEquipments` block listing the desired equipment templates.
3. Verify the equipment template IDs exist in `LOTRLOME_Armory` — see [equipment-armory-system.md](../../C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/equipment-armory-system.md). Missing items will leave the participant in underwear.
4. No code changes needed. The model resolves the new dummy via `ResolveDummyId` on the next tournament.

## How to Tune Prize Pool Tier Ranges

Edit constants on [TaomTournamentModel.cs](../../Main/Features/Arena/Models/TaomTournamentModel.cs):

- Widening regular: lower `RegularMinTier` or raise `RegularMaxTier`.
- Making elite stricter: raise `EliteMinTier` (e.g., `5f` to require master-tier items).

Filtering rules (excludes `NotMerchandise`, requires weapon or armor, excludes horses) are in `BuildPrizePool` and can be relaxed there if desired.

## GitHub Issue

- **Issue:** None — feature predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable.
