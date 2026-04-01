# Initial Child Generation

## Overview
InitialChildGeneration populates every major-faction clan with a realistic number of child heroes when a new campaign is created. It runs once on `OnNewGameCreatedPartialFollowUpEvent` (index 0) and creates children for each clan based on its adult hero count, with configurable age ranges, gender ratios, and per-culture and per-clan overrides.

## Why This Exists
- **Vanilla behavior:** Bannerlord starts a new campaign with most clans having few or no child heroes. The succession system depends on children existing, but vanilla's child spawn-in happens reactively over campaign time.
- **TAOM requirement:** TAOM's 10 custom cultures each have hand-authored lords. Without initial children, those clans have no heirs at campaign start, which means clans die off rapidly after their lords are killed, breaking faction survival over longer playthroughs.
- **Without this feature:** Custom TAOM clans go extinct within the first few in-game years whenever a lord dies in battle, leaving factions without enough nobles to hold territory.

## Architecture
### Design Challenge
Creating children for heroes at new-game time requires calling an internal Bannerlord function that spawns a child character based on a template hero. `Hero` is a sealed type. The game's child-creation path is not publicly accessible. Additionally, TAOM needs to avoid creating children for vanilla clans that already have them, and needs to support different age distributions for long-lived races (elves) versus short-lived ones (men).

### Solution Approach
`TaomInitialChildGenerationBehavior` is a `CampaignBehaviorBase` that listens to `CampaignEvents.OnNewGameCreatedPartialFollowUpEvent`. It fires `GenerateInitialChildren()` only when `index == 0` to ensure it runs at the correct initialization phase.

`InitialChildGenerationService` holds all logic:
1. Loads config via `IInitialChildGenerationConfigProvider` (cached JSON).
2. Gets all major-faction clans via `IClanPopulationAdapter` (adapter over sealed `Clan` types), which returns `ClanPopulationInfo` POCOs containing adult male/female hero id lists and existing child count.
3. For each clan: checks `ExcludedClans` / `ExcludedCultures`, resolves effective settings by merging global defaults with culture override then clan override, calculates how many children to create (based on adult count and existing child count), then calls `IChildCreatorAdapter.CreateChild(templateId, clanId, isFemale, age)` for each.
4. Template selection: picks a random adult of the matching gender as the template. Falls back to opposite gender if no same-gender adults exist.
5. Gender: index 0 is forced male if the clan has no adult males (ensures at least one male heir); subsequent children use the configured `FemaleRatio`.

Config is read from `initial_child_generation.json` in the mod's config path. If the file is absent, code-default values are used (min age 2, max age 17, female ratio 0.49, multiplier 1.0).

### Component Diagram
```
TaomInitialChildGenerationBehavior.OnNewGameCreatedPartialFollowUp(starter, index=0)
    |-> IInitialChildGenerationService.GenerateInitialChildren()
            |-> IInitialChildGenerationConfigProvider.LoadConfig()    [cached JSON]
            |-> IClanPopulationAdapter.GetMajorFactionClans()          [returns ClanPopulationInfo[]]
            |-> for each clan:
                    |-> IsExcluded(clan, config)   [ExcludedClans / ExcludedCultures]
                    |-> ResolveSettings(clan, config)  [defaults -> culture override -> clan override]
                    |-> CalculateChildCount(clan, settings)
                            ceiling(totalAdults/2) * multiplier - existingChildCount
                    |-> for each child:
                            |-> DetermineGender(index, hasMales, femaleRatio)
                            |-> IRandomSource.Next(minAge, maxAge)
                            |-> SelectTemplate(clan, isFemale)  [random adult of matching gender]
                            |-> IChildCreatorAdapter.CreateChild(templateId, clanId, isFemale, age)
```

## Configuration
JSON file at `{ConfigPath}/initial_child_generation.json`.

Top-level keys:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `defaults.min_age` | int | 2 | Minimum child age in years |
| `defaults.max_age` | int | 17 | Maximum child age in years |
| `defaults.female_ratio` | double | 0.49 | Probability that any given child is female |
| `defaults.child_count_multiplier` | double | 1.0 | Scales the calculated child count |
| `excluded_cultures` | string[] | [] | Culture ids to skip entirely |
| `excluded_clans` | string[] | [] | Clan ids to skip entirely |
| `culture_overrides` | array | [] | Per-culture overrides for any default |
| `clan_overrides` | array | [] | Per-clan overrides; also supports `fixed_child_count` |

Culture override object fields: `culture_id`, `min_age`, `max_age`, `female_ratio`, `child_count_multiplier` (all optional except `culture_id`).

Clan override adds `clan_id` and `fixed_child_count` (bypasses the calculation entirely when set).

If the config file is missing, all defaults apply and no exclusions or overrides are active.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/InitialChildGeneration/InitialChildGenerationService.cs` | All generation logic — exclusion, settings resolution, child count calculation, gender, template selection |
| `Main/Features/InitialChildGeneration/TaomInitialChildGenerationBehavior.cs` | CampaignBehaviorBase; fires service on new game at index 0 |
| `Main/Features/InitialChildGeneration/InitialChildGenerationConfigProvider.cs` | JSON config loader with caching; uses `Newtonsoft.Json.Linq` |
| `Main/Features/InitialChildGeneration/Config/InitialChildGenerationConfig.cs` | Config POCO hierarchy (`InitialChildGenerationConfig`, `GlobalDefaults`, `CultureOverride`, `ClanOverride`) |
| `Main/Features/InitialChildGeneration/InitialChildGenerationIoC.cs` | DryIoc registrations |
| `Main/Features/InitialChildGeneration/IRandomSource.cs` | Abstraction over `System.Random` for testability |
| `Main/Features/InitialChildGeneration/SystemRandomSource.cs` | Live implementation wrapping `System.Random` |
| `TAOM.Tests/Features/InitialChildGeneration/InitialChildGenerationServiceTests.cs` | Full service logic coverage |
| `TAOM.Tests/Features/InitialChildGeneration/TaomInitialChildGenerationBehaviorTests.cs` | Event wiring |
| `TAOM.Tests/Features/InitialChildGeneration/InitialChildGenerationConfigProviderTests.cs` | JSON parsing, missing file fallback |

## Dependencies
- `IClanPopulationAdapter` — wraps `Clan` (sealed) to return `ClanPopulationInfo` POCOs
- `IChildCreatorAdapter` — wraps the Bannerlord child-creation call
- `IInitialChildGenerationConfigProvider` — loads and caches JSON config
- `IRandomSource` — `System.Random` abstraction
- `IModLogger` — logs created child count and exclusions
- `IPathService` — resolves config file path

## Tests
- `InitialChildGenerationServiceTests.cs` — covers: exclusion by clan id and culture id; settings layering (defaults, culture override, clan override, fixed count); child count calculation formula; gender determination with `hasMales=false` guard; template selection with gender fallback.
- `TaomInitialChildGenerationBehaviorTests.cs` — verifies that `GenerateInitialChildren` is called for `index == 0` and not called for `index > 0`.
- `InitialChildGenerationConfigProviderTests.cs` — verifies JSON parsing for all fields, missing file fallback to defaults, and that the result is cached on second call.

## How to Exclude a Clan or Culture
Add the clan id or culture id to the `excluded_clans` or `excluded_cultures` array in `initial_child_generation.json`:

```json
{
  "excluded_clans": ["clan_player_1"],
  "excluded_cultures": ["battania"]
}
```

Changes take effect on the next new game. Existing saves are not affected (the behavior only runs at new-game creation).

## GitHub Issue
- **Issue:** Unknown (commit `0b3a1f6`)
- **Status:** Unknown
