# Character Creation

## Overview
Replaces Bannerlord's vanilla character creation content with TAOM-specific cultures, narrative backstory menus, and race assignment. On finalization it sets the player's race based on their chosen culture and teleports them to a culture-appropriate starting settlement.

## Why This Exists
- **Vanilla behavior:** Character creation offers 6 human cultures (empire, vlandia, sturgia, aserai, battania, khuzait) with fixed narrative options tied to those cultures, an unfiltered race dropdown showing all engine races, and a random body silhouette per culture.
- **TAOM requirement:** The mod adds 10+ custom cultures (Gondor, Erebor, Mordor, Rohan, elves, etc.) each needing their own race assignment, starting settlement, narrative backstory options, body proportions, and culture-restricted race dropdown so a Gondor player can't pick "Cave Troll".
- **Without this feature:** Custom cultures would not appear in character creation; the player would start as a human with default vanilla placement; no-mount cultures (Erebor dwarves) would crash with a `NullReferenceException` when the narrative scene tried to spawn a horse actor; the race dropdown would offer lore-inappropriate choices for every culture; the body silhouette would not reflect the culture (Erebor would render at human-default proportions instead of dwarven).

## Architecture

### Design Challenge
Five separate problems must be solved:

1. **Registering custom cultures** — `CharacterCreationManager` is provided by SandBox's handler at priority 800. TAOM must hook in at a higher priority (1050) via `AfterInitializeContent` so it runs after vanilla setup.
2. **Replacing narrative menus** — The parent/childhood/youth narrative menus exist but contain vanilla option objects. TAOM must remove those and substitute JSON-driven TAOM options without breaking the menu structure.
3. **No-mount crash** — Vanilla's `GetYouthMenuNarrativeMenuCharacterArgs`, `GetAdultMenuNarrativeMenuCharacterArgs`, and `GetAgeSelectionMenuNarrativeMenuCharacterArgs` unconditionally read `DefaultEquipment[Horse].Item.StringId` to build a horse `NarrativeMenuCharacterArgs`. When a culture's CC equipment roster has no horse slot the read returns null and the game crashes. A separate crash fires in `SpawnNonHumanNarrativeMenuCharacter` when it tries to look up the null horse item ID in `MBObjectManager`.
4. **Culture-restricted race dropdown** — `FaceGenVM` builds its `RaceSelector` from `FaceGen.GetRaceNames()` and uses the array index as the engine's global race ID. Filtering the array shifts indices and breaks the dropdown's race-mesh lookup. The fix must filter the visible items while preserving the index→race-name contract that vanilla code depends on.
5. **Per-culture body silhouette** — Vanilla initializes `_faceGenerationParams` with culture-driven random ranges. TAOM cultures need a deterministic per-culture default body so a Mordor uruk and a Rivendell elf don't both spawn at human-default proportions. Vanilla has no extension point; we must hook the culture-selection event and apply a culture-specific `BodyProperties` key string to the player character.

### Solution Approach
- `CharacterCreationRegistrationBehavior` (a `CampaignBehaviorBase`) listens for `OnCharacterCreationInitializedEvent` and registers `TaomCharacterCreationContentHandler` at priority 1050.
- `TaomCharacterCreationContentHandler` implements `ICharacterCreationContentHandler`. Its `AfterInitializeContent` calls into `ICharacterCreationContentService` to register cultures and replace narrative menus.
- `CharacterCreationContentService` reads culture data from `ICultureCreationDataProvider` (cultures.json) and narrative options from `INarrativeDataProvider` (parents/childhood/youth JSON files). Vanilla cultures are skipped by a hard-coded allow-list.
- On `OnCharacterCreationFinalize`, the service sets the player's race via `IRaceManager`/`IHeroRosterAdapter` and teleports `MobileParty.MainParty` to the culture's starting settlement. Race assignment honors the player's actual FaceGen choice when valid + in the culture's allow-list (validated via `IRaceManager.IsValidRaceId`); otherwise falls back to `cultureData.Races[0]`. Finally `GrantPlayerStartupResources` invokes (a) `IPlayerStartupGoldService` to grant the configured per-culture `playerGold` from [`startup_resources_config.xml`](../../Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) via the existing `IGoldGiftAdapter`, and (b) `IPlayerEquipmentService` to persist the youth option's equipment roster (`player_char_creation_{culture}_{titleType}_{m|f}`) onto `Hero.MainHero.BattleEquipment` and `CivilianEquipment` via `IPlayerEquipmentAdapter`. Both calls are exception-isolated so a failure in one does not block the other. See [Startup Resources](startup-resources.md) for the gold/equipment configuration.
- **Under a multiplayer join, everything the bullet above grants lands on a hero that is about to be discarded.** Every co-op base replaces the joining client's campaign wholesale with the host's world and hands the player a HOST-authored hero, so the race, career, startup gold and special-resource seed all applied to the character the player built and then lost. `PlayerPossessionBehavior` — added in `SubModule.cs` immediately after `CharacterCreationRegistrationBehavior`, so its listener is in place before creation can finish — captures the picks on `CampaignEvents.OnCharacterCreationIsOverEvent` (a *different* event from `OnCharacterCreationFinalize`): `hero.StringId`, `hero.Culture?.StringId`, `hero.CharacterObject?.Race` and `ICareerMenuService.SelectedCareerStringId`, which makes this feature's own career service the source of the recorded career. Four grants are then re-invoked against the hero the player actually controls — `IHeroRosterAdapter.SetHeroRace`, `IPlayerStartupGoldService.GrantPlayerStartupGold`, `ICareerCreationHandler.OnCareerSelected`, `ISpecialResourceService.InitializeHero`. **The CC equipment roster (`IPlayerEquipmentService`) and the starting-settlement teleport are NOT re-applied**, so a joiner keeps the host's placement and whatever equipment the hand-off gave them. Design, guards, and why an heir succession does not trigger it: [player-possession.md](player-possession.md).
- `Patch20_NarrativeHorseGuard` contains four Harmony patches: three Prefix patches intercept the three `GetXxxMenuNarrativeMenuCharacterArgs` methods and short-circuit to return a player-only list when no horse is present; one Finalizer on `SpawnNonHumanNarrativeMenuCharacter` swallows the resulting `ArgumentNullException(key)`.
- `Patch9_RaceFilter` Postfixes `FaceGenVM.Refresh(bool)`, delegates to `FaceGenRaceSelectorRebuilder.Apply(faceGenVM, filterService)`. The rebuilder constructs a fresh `SelectorVM<SelectorItemVM>` with only allowed races, wires a wrapped `_onChange` that translates the user's filtered selection back to the engine's global race index via reflection on `_selectedIndex`, and assigns through the public `RaceSelector` property setter (which fires the change notification correctly). See [Race Filter](#race-filter-patch9_racefilter) for full details.
- `Patch29_CCBodyProperties` Postfixes `CharacterCreationContent.SetSelectedCulture(CultureObject)`, delegates to `ICCBodyPropertiesService.ApplyForCulture`, which loads the culture's `<BodyProperties version="4" key="..."/>` element from `cc_body_properties.xml` and applies it via `IPlayerBodyPropertiesAdapter` (wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`). See [Per-Culture Default BodyProperties](#per-culture-default-bodyproperties-patch29_ccbodyproperties) for full details.

### Component Diagram
```
CampaignEvents.OnCharacterCreationInitializedEvent
        |
CharacterCreationRegistrationBehavior
        |  registers at priority 1050
TaomCharacterCreationContentHandler (ICharacterCreationContentHandler)
        |  AfterInitializeContent
ICharacterCreationContentService
        |-- ICultureCreationDataProvider --> charactercreation/cultures.json
        |-- ICultureRaceFilterService    --> per-culture races[] allow-lists (from cultures.json)
        |-- INarrativeDataProvider       --> charactercreation/{parents,childhood,youth}_menu.json
        |-- IEquipmentRosterProvider     --> MBEquipmentRoster lookups
        |-- IRaceManager (+ IsValidRaceId gate) / IHeroRosterAdapter --> race assignment on finalize

Harmony Patch9_RaceFilter
        └── Postfix: FaceGenVM.Refresh(bool)
                └── FaceGenRaceSelectorRebuilder.Apply
                        ├── ICultureRaceFilterService.GetAllowedRaces
                        └── faceGenVM.RaceSelector = newSelector  (public setter)

Harmony Patch20_NarrativeHorseGuard
        |-- Prefix: GetYouthMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAdultMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAgeSelectionMenuNarrativeMenuCharacterArgs
        |-- Finalizer: SpawnNonHumanNarrativeMenuCharacter

Harmony Patch29_CCBodyProperties
        └── Postfix: CharacterCreationContent.SetSelectedCulture(CultureObject)
                └── ICCBodyPropertiesService.ApplyForCulture(cultureId)
                        ├── ICCBodyPropertiesProvider.GetBodyPropertiesXml --> cc_body_properties.xml
                        └── IPlayerBodyPropertiesAdapter.TryApplyFromXml
                                └── CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties
```

`PlayerPossessionBehavior` is deliberately not drawn in: its listener hangs off
`OnCharacterCreationIsOverEvent`, not off the `ICharacterCreationContentService` chain above, and an
arrow would overstate the coupling. See [player-possession.md](player-possession.md).

## Configuration

### `Main/_Module/ModuleData/charactercreation/cultures.json`
JSON array of culture entries:
```json
{
  "culture_id": "erebor",
  "races": ["dwarf"],
  "starting_settlement": "town_E1",
  "default_age": 40,
  "default_weight": 0.6,
  "default_build": 0.7,
  "focus_to_add": 1,
  "skill_level_to_add": 10
}
```

### `Main/_Module/ModuleData/charactercreation/parents_menu.json`
### `Main/_Module/ModuleData/charactercreation/childhood_menu.json`
### `Main/_Module/ModuleData/charactercreation/youth_menu.json`
JSON arrays of narrative option entries:
```json
{
  "string_id": "taom_gondor_noble_parents",
  "culture_id": "empire_w",
  "text": "...",
  "description": "...",
  "skills": ["OneHanded", "Leadership"],
  "attribute": "Social",
  "occupation_type": "Noble",
  "title_type": "Lord",
  "focus_to_add": 1,
  "skill_level_to_add": 10,
  "attribute_level_to_add": 1
}
```
Options without `string_id` prefixed with `taom_` are treated as vanilla and removed.

### Bonus budget (skill / attribute / focus) — vanilla-aligned

Each option carries `(focus_to_add, skill_level_to_add, attribute_level_to_add)`. The engine
applies these at menu-selection time via `NarrativeMenuOptionArgs` — the **narrative stages**
through [NarrativeMenuBuilder.BuildOption](../../Main/Features/CharacterCreation/NarrativeMenuBuilder.cs#L61),
and the **career stage** through [CareerMenuService.BuildOptionForCareer](../../Main/Features/CharacterCreation/CareerMenuService.cs#L260)
— so by `OnCharacterCreationFinalize` the Hero already has the points. The per-pick value is
identical to vanilla's defaults `(1, 10, 1)`.

**Total budget = 5 bonus stages (matches vanilla).** TAOM has five bonus-granting narrative
stages — parents, childhood, youth, education, adulthood — each granting the vanilla `(1,10,1)`
bundle. Two additional bonus sources that vanilla lacks were **zeroed (2026-05-30)** to keep the
starting character on vanilla's budget:

| Source | Payload now | Why |
|--------|-------------|-----|
| **Career stage** (`career_menu.json`, 49 entries) | `focus/skill/attr = 0`, `skills`/`attribute` **cleared** | A 6th stage vanilla doesn't have. Career still picks specialization + starting equipment; it just grants no stat bonus. Clearing `skills`/`attribute` (not just zeroing the magnitudes) makes a selected career render the same line as "No specialization" (`0 unspent Focus/Attribute Point`) — without it the engine's `GetPositiveEffectText` shows a confusing `0 Skill Level … to <skills>` on the career menu **and** the final review screen. |
| **Culture base** (`cultures.json` `focus_to_add` / `skill_level_to_add`, 18 entries) | `0 / 0` | An on-top add vanilla doesn't have. |

Result: 5 total focus, 5 total attribute points — matching vanilla. **Concentration is
deliberately left as-is:** because the per-culture themes repeat across stages and aren't gated
on earlier picks, a min-max build can still pour all 5 narrative stages into one skill (`+50`)
or one attribute (`+5`). Vanilla spreads via gating; diversifying TAOM's themes to do the same
is the documented next lever but was out of scope for the 2026-05-30 budget cut.

**Deserialization defaults match the data (no "delete-the-keys" landmine).** The Career +
Culture providers/models (`CareerMenuDataProvider`, `CareerMenuOptionDefinition`,
`CultureCreationDataProvider`, `CultureCreationData`) default these bonus fields to **`0`**, so a
future entry that *omits* the keys gets "no bonus" — matching the zeroed data, not the old
1/10/1. `NarrativeDataProvider`/`NarrativeOptionDefinition` deliberately keep the **`1/10/1`**
default: that *is* the intended per-stage bonus, so a narrative option that omits a key correctly
self-heals to the vanilla bundle. (Editing the JSON to retune: zero the value or omit the key —
both yield 0 for career/culture; for narrative, omitting a key restores 1/10/1.)

**Audit / re-apply:** [`tools/audit_cc_bonuses.py`](../../tools/audit_cc_bonuses.py) reports
per-culture worst-case concentration vs the vanilla budget (deterministic — ties broken
alphabetically), and `--apply` re-runs the zeroing + skills/attribute clear (formatting-preserving,
writes `.bak`). Latest report:
[`docs/reviews/cc-bonus-audit-2026-05-30.md`](../reviews/cc-bonus-audit-2026-05-30.md). Note the
providers are `Reuse.Singleton` — edits to these JSONs require a full Bannerlord restart, not
just a new campaign.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/CharacterCreation/CharacterCreationIoC.cs` | DryIoc registrations for the feature |
| `Main/Features/CharacterCreation/CharacterCreationRegistrationBehavior.cs` | `CampaignBehaviorBase` entry point — registers the handler at priority 1050 |
| `Main/Features/CharacterCreation/TaomCharacterCreationContentHandler.cs` | Thin `ICharacterCreationContentHandler` delegating to the service |
| `Main/Features/CharacterCreation/ICharacterCreationContentService.cs` | Service interface |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Core logic: culture registration, narrative menu replacement, finalization. `SetPlayerRace` honors player's FaceGen choice when valid + in allow-list (gated on `IsValidRaceId`) |
| `Main/Features/CharacterCreation/CultureCreationDataProvider.cs` | Parses cultures.json |
| `Main/Features/CharacterCreation/NarrativeDataProvider.cs` | Parses narrative menu JSON files with concurrent cache |
| `Main/Features/CharacterCreation/NarrativeMenuBuilder.cs` | Builds `NarrativeMenuOption` objects from `NarrativeOptionDefinition` list |
| `Main/Features/CharacterCreation/EquipmentRosterProvider.cs` | Looks up `MBEquipmentRoster` by string ID |
| `Main/Features/CharacterCreation/Models/CultureCreationData.cs` | POCO: culture metadata including race, settlement, body defaults |
| `Main/Features/CharacterCreation/Models/NarrativeOptionDefinition.cs` | POCO: one narrative backstory choice |
| `Main/Features/CharacterCreation/ICultureRaceFilterService.cs` | Race filter interface |
| `Main/Features/CharacterCreation/CultureRaceFilterService.cs` | Reads `cultures.json` `races[]` per culture; one-time fallback warning for unknown cultures |
| `Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs` | Race-filter engine helper. Pure static helpers: `BuildGlobalIndexMap`, `MapFilteredIndexToGlobal`, `MapGlobalIndexToFiltered`. Apply uses public `RaceSelector` property setter (not field reflection) for correct UI rebinding |
| `Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs` | Patch9_RaceFilter — Postfix on `FaceGenVM.Refresh(bool)`. Thin entry point delegating to `FaceGenRaceSelectorRebuilder.Apply` |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | Patch20_NarrativeHorseGuard — 4 patches guarding no-mount culture crashes |
| `Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs` | CC body-properties config interface |
| `Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs` | Parses `cc_body_properties.xml`; validates 128-hex key length, lowercases culture IDs, last-wins on duplicates |
| `Main/Features/CharacterCreation/ICCBodyPropertiesService.cs` | CC body-properties application interface |
| `Main/Features/CharacterCreation/CCBodyPropertiesService.cs` | Resolves XML for a culture and delegates to adapter; logs reject reasons |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs` | Patch29_CCBodyProperties — Postfix on `CharacterCreationContent.SetSelectedCulture` triggers per-culture body-properties application |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationReviewStageVM_AutoFillName_Patch.cs` | Patch44_CCNameAutofill — Postfix on `CharacterCreationReviewStageVM`'s constructor; pre-fills the "Enter your name" field with a culture-appropriate first name (via the VM's `ExecuteRandomizeName()`) when blank |
| `Main/Adapters/IPlayerBodyPropertiesAdapter.cs` + `PlayerBodyPropertiesAdapter.cs` | Wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`; preserves Race + IsFemale |
| `Main/_Module/ModuleData/charactercreation/cultures.json` | Culture metadata + race-filter allow-lists |
| `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml` | Per-culture default `BodyProperties` (key=128 hex chars) for the CC preview |
| `Main/_Module/ModuleData/charactercreation/parents_menu.json` | Parent backstory options |
| `Main/_Module/ModuleData/charactercreation/childhood_menu.json` | Childhood backstory options |
| `Main/_Module/ModuleData/charactercreation/youth_menu.json` | Youth backstory options |

## Dependencies
- `ICultureCreationDataProvider` — reads cultures.json
- `ICultureRaceFilterService` — exposes per-culture race allow-lists from cultures.json
- `ICCBodyPropertiesProvider` — reads cc_body_properties.xml
- `ICCBodyPropertiesService` — applies body XML to player character on culture change
- `IPlayerBodyPropertiesAdapter` — wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`
- `INarrativeDataProvider` — reads narrative JSON files
- `IEquipmentRosterProvider` — looks up CC equipment rosters
- `IRaceManager` — maps race name to race ID; `IsValidRaceId` gates fallback acceptance
- `IHeroRosterAdapter` — gets/sets hero race on finalization
- `IPathService` — resolves `ModuleDataPath`
- `IModLogger` — logging throughout

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs` | `RegisterCustomCultures`, `RegisterNarrativeMenus`, `OnCharacterCreationFinalize`, `SetPlayerRace` (16 tests including FaceGen-preservation, invalid-ID fallback, case-insensitive matching) |
| `TAOM.Tests/Features/CharacterCreation/CultureCreationDataProviderTests.cs` | JSON parsing, missing file handling, `GetCultureData` lookup |
| `TAOM.Tests/Features/CharacterCreation/NarrativeDataProviderTests.cs` | JSON parsing, caching, `GetMenuOptionsForCulture` filtering, missing file handling |
| `TAOM.Tests/Features/CharacterCreation/NarrativeMenuBuilderTests.cs` | Option construction, empty input handling |
| `TAOM.Tests/Features/CharacterCreation/CultureRaceFilterServiceTests.cs` | 24 tests: per-culture allow-lists, fallback, case-insensitive matching, empty-array handling |
| `TAOM.Tests/Features/CharacterCreation/FaceGenRaceSelectorRebuilderTests.cs` | 12 tests for pure helpers: `BuildGlobalIndexMap`, `MapFilteredIndexToGlobal`, `MapGlobalIndexToFiltered`, round-trip property |
| `TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs` | XML parsing, 128-hex validation, missing file handling, duplicate-id last-wins, malformed XML |
| `TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs` | Apply-for-culture happy path, missing-config no-op, adapter-rejection logging |

## Race Filter (Patch9_RaceFilter)

The Character Customization Race dropdown is filtered to the races defined in each culture's `races` array in `cultures.json`. The filter is applied by `FaceGenVM_Refresh_RaceFilter_Patch` as a Postfix on `FaceGenVM.Refresh(bool clearProperties)`.

### Why a postfix on `Refresh`, not on `GetRaceNames`
`FaceGenVM` builds its `RaceSelector` as `new SelectorVM<SelectorItemVM>(FaceGen.GetRaceNames(), _selectedRace, OnSelectRace)`. The VM uses **the array index from `GetRaceNames()`** as the engine's global race ID. Filtering the array shifts indices and decouples the dropdown from the race table — the original Patch9 attempt did exactly that and broke. The current patch instead replaces `_raceSelector` after vanilla construction and translates filtered ↔ global indices in a wrapped `_onChange` callback.

### Index-translation flow
```
User clicks filtered position N
  → SelectorVM.SelectedIndex setter fires (= N)
  → wrapped _onChange (our wrapper):
      saved = N
      mutate s._selectedIndex = globalIndices[N]   (via reflection, bypassing setter)
      call vanilla OnSelectRace(s):
          _selectedRace = s.SelectedIndex          (= globalIndices[N], correct global race ID)
          UpdateRaceAndGenderBasedResources()
              → UpdateFace(-20, _selectedRace) → SetRaceGenderAndAdjustParams updates _faceGenerationParams.CurrentRace
          Refresh(true) → rebuilds vanilla RaceSelector at line 1925 → our Postfix re-applies filter
      restore s._selectedIndex = saved
```

### Dropdown order follows cultures.json, not engine order

`BuildGlobalIndexMap` iterates the **allow-list** (config order) and resolves each race name to its position in the engine's `FaceGen.GetRaceNames()` array. The result is a `globalIndices: List<int>` whose order matches the user's `races[]` config — `[uruk, orc, human]` → `[engineIdxOfUruk, engineIdxOfOrc, engineIdxOfHuman]`. This is what makes the dropdown show `uruk` in position 1 for Mordor instead of `human` (engine puts human at index 0).

### Force-switch logic ([`ShouldForceSwitchToDefault`](../../Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs))

The rebuilder force-switches the dropdown to filtered position 0 (`Races[0]`) under two conditions:

| Trigger | Behavior | Example |
|---------|----------|---------|
| `_selectedRace` is **not in** the culture's allow-list | Always switch | Player switched culture from Mordor (uruk) to Erebor (dwarf only) — uruk no longer allowed → switch to dwarf |
| **First Apply for a culture** AND `_selectedRace` is in the allow-list but NOT at position 0 | Switch to Races[0] | Player picks Isengard culture in narrative menus; vanilla initializes `_selectedRace = 0` (human, the engine default); human IS in Isengard's allow-list at filtered position 2; dropdown would otherwise default to human → switch to uruk_hai |
| Subsequent Apply (gender/age refresh) on the same culture | Preserve player's selection | Player chose berserker for Isengard; gender change triggers `Refresh(true)` → no switch, berserker stays |

Per-`FaceGenVM`-instance session tracking via `ConditionalWeakTable<FaceGenVM, RaceFilterSession>` records the last applied culture id. Switching culture mid-CC re-triggers "first Apply for this culture" → resets to the new Races[0]. A `[ThreadStatic]` recursion guard on the force-switch path prevents the downstream `Refresh(true)` from looping.

### Index-translation flow
```
User clicks filtered position N
  → SelectorVM.SelectedIndex setter fires (= N)
  → wrapped _onChange (our wrapper):
      saved = N
      mutate s._selectedIndex = globalIndices[N]   (via reflection, bypassing setter)
      call vanilla OnSelectRace(s):
          _selectedRace = s.SelectedIndex          (= globalIndices[N], correct global race ID)
          UpdateRaceAndGenderBasedResources()
              → UpdateFace(-20, _selectedRace) → SetRaceGenderAndAdjustParams updates _faceGenerationParams.CurrentRace
          Refresh(true) → rebuilds vanilla RaceSelector at line 1925 → our Postfix re-applies filter
      restore s._selectedIndex = saved
```

### Configuration
- File: [`Main/_Module/ModuleData/charactercreation/cultures.json`](../../Main/_Module/ModuleData/charactercreation/cultures.json)
- Per-culture entry: `"races": ["race_id_1", "race_id_2", ...]`
- **Order matters:** the first race in the array is the canonical default for that culture. Mordor's `["uruk", "orc", "human"]` defaults to uruk on first FaceGen open; Isengard's `["uruk_hai", "berserker", "human"]` defaults to uruk_hai.
- Reload scope: read once at the first `LoadCultures()` call by `CultureCreationDataProvider` (`Reuse.Singleton`). **Edits to cultures.json require a full Bannerlord restart**, not a save-load.
- Adding a new race: add the race ID to the `races` array of every culture that should permit it. Position determines whether it becomes the new default.
- Adding a new culture: add a culture entry with `culture_id` and `races` array. If `races` is empty or the culture is unknown, the filter falls back to showing all races (with a one-time warning per culture in the log).

### Current allow-lists (as of 2026-05-06)
| Culture | Allowed races |
|---------|---------------|
| `erebor` | `dwarf` |
| `mordor` | `uruk`, `orc`, `human` |
| `gundabad` | `pale_uruk`, `goblin`, `orc`, `human` |
| `dolguldur` | `dg_uruk`, `goblin`, `orc`, `human` |
| `mirkwood` / `lothlorien` / `rivendell` | `elf`, `human` |
| `isengard` | `uruk_hai`, `berserker`, `human` |
| `gondor` / `umbar` / `shaghana` / `abanissa` | `human` |
| `empire` / `vlandia` / `sturgia` / `aserai` / `battania` / `khuzait` | `human` |

### Race finalization (review-driven hardening)

`SetPlayerRace` honors the player's actual FaceGen choice rather than always assigning `cultureData.Races[0]` at finalize. Bannerlord assigns `Hero.CharacterObject.Race` from FaceGen output before finalize runs — `SetPlayerRace` reads that value, validates it via `IRaceManager.IsValidRaceId`, and accepts it only when both checks pass:

1. `IsValidRaceId(faceGenRaceId)` is `true` (gates against `GetRaceNameFromId`'s "human" fallback for unknown IDs — the fallback is for logging-and-survival, not for security decisions; see `csharp-architecture.md` "Lookup Functions With Fallbacks").
2. The resolved race name is in the culture's `races[]` allow-list (case-insensitive).

If either check fails, `SetPlayerRace` falls back to `cultureData.Races[0]`. This catches corrupt save migration, mid-mod-update transitions, and the silent-coercion bug where an invalid ID resolves to `"human"` and slips past the allow-list for cultures that allow `human`.

## Per-Culture Default BodyProperties (Patch29_CCBodyProperties)

When the player picks a culture during Character Creation, the player-character preview adopts a TAOM-defined `BodyProperties` key string for that culture instead of the vanilla random-within-min/max default. The body re-applies on every culture change, mirroring vanilla's "switch culture resets body" mental model. Cultures not configured fall back to vanilla behavior with no errors.

### Architecture

```
CharacterCreationContent.SetSelectedCulture(CultureObject)   ← vanilla
        │
        ▼  Postfix (Patch29_CCBodyProperties)
CharacterCreationContent_SetSelectedCulture_Patch
        │
        ▼  IoC.Resolve<ICCBodyPropertiesService>
ICCBodyPropertiesService.ApplyForCulture(string cultureId)
        │
        ├─ ICCBodyPropertiesProvider.GetBodyPropertiesXml(cultureId) → XML string or null
        └─ IPlayerBodyPropertiesAdapter.TryApplyFromXml(xml)
                │
                ├─ BodyProperties.FromString(xml, out parsed)        ← vanilla parser
                └─ CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties(
                        parsed, playerChar.Race, playerChar.IsFemale)
```

Patch is a thin Postfix (≤35 lines) that delegates to the service. Service uses constructor-injected provider + adapter (no service-locator inside the service).

### Configuration

[`Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml)

```xml
<Culture id="erebor">
  <BodyProperties version="4" key="<128-hex-character-key>" />
</Culture>
```

- Paste the `<BodyProperties version="4" key="..."/>` element exactly as produced by the in-game `BodyProperties.ToString()` (or copied from a save / face-customizer export).
- The key must be exactly **128 hex characters**; otherwise the entry is skipped with a warning in `rgl_log.txt`.
- Optional `weight` / `build` attributes on `<BodyProperties .../>` are honored if present; absent values default to `weight=0 build=0` (vanilla parser default).
- The `age` attribute is parsed by vanilla but NOT applied to the player — `Hero.Age` is computed from `BirthDay`, which we do not touch. Including `age=` has no visible effect.
- Cultures not listed fall back to the vanilla per-culture random body.
- The provider is `Reuse.Singleton`: edits to this file require restarting Bannerlord, not just reloading a save.
- Culture ID lookup is case-insensitive (provider lowercases on load); duplicate culture IDs log a warning, last-wins.
- The XML's leading comment block also serves as the editor cheatsheet for which `culture_id` maps to which LOTR faction (vlandia=Rohan, empire=Dunland, battania=Khand, etc.).

### Why Postfix on `SetSelectedCulture` (not on FaceGen open)

`SetSelectedCulture` is the canonical "player picked a culture" event. It fires:
- Once per culture-change in the narrative menus
- Idempotently if the player re-picks the same culture (vanilla setter doesn't gate)
- Before any subsequent CC stage (FaceGen, narrative continuation) renders

Patching the FaceGen open instead would miss the case where the body should update mid-narrative without opening FaceGen. Patching `ApplyCulture` (on finalize) would be too late — the preview wouldn't reflect the chosen body until after the player committed.

### What if the body XML fails to parse?

`PlayerBodyPropertiesAdapter.TryApplyFromXml` returns `false` if `BodyProperties.FromString` fails or if `CharacterObject.PlayerCharacter` is null. The service logs a warning ("adapter rejected body XML for X, could not apply") and the player character keeps its previous body. No crash, no exception bubble — the postfix catches and logs at warning level.

### Adding a new culture's default body

1. Open Bannerlord, enter Character Creation, design the body you want
2. Export the `<BodyProperties .../>` XML (in-game face-customizer's "copy" button, or from a save's `BodyProperties.ToString()` output)
3. Paste into `cc_body_properties.xml` under a new `<Culture id="...">` block
4. Restart Bannerlord (Singleton lifetime — save-load won't re-read)

## Review-Stage Name Autofill (Patch44_CCNameAutofill)

Vanilla leaves the Review-stage "Enter your name" field blank until the player types a name or clicks the randomize dice — `CharacterCreationReviewStageVM` seeds its `Name` from `CharacterCreationContent.MainCharacterName`, which nothing populates by default.

`Patch44_CCNameAutofill` is a Postfix on the review-stage VM's 6-arg constructor: when `Name` is blank it calls the VM's own public `ExecuteRandomizeName()`, which draws a first name from `SelectedCulture` + `Hero.MainHero.IsFemale`. Running at the **Review** stage is deliberate — gender is finalized there, so the generated name matches the chosen sex. The empty-guard means a name the player already typed is never overwritten, and the field stays fully editable (typing or the dice still override it).

This pairs with the family/clan-name fix in [`FactionMap.CultureSettingService`](faction-map.md) (which makes the *clan* name culture-appropriate). Both shipped under issue [#264](https://github.com/haterade22/TAOM/issues/264).

## How to Add a New TAOM Culture to Character Creation

1. Add an entry to `Main/_Module/ModuleData/charactercreation/cultures.json` with `culture_id`, `races`, `starting_settlement`, body defaults, and skill/focus bonuses.
2. Add narrative option entries (prefixed `taom_`) to each of `parents_menu.json`, `childhood_menu.json`, and `youth_menu.json` with `culture_id` matching the new culture.
3. **Add a `<Culture id="…" playerGold="…"/>` row to [`startup_resources_config.xml`](../../Main/_Module/ModuleData/startup_resources/startup_resources_config.xml).** Without this, the player will silently start with 0 denars after picking the new culture (`PlayerStartupGoldService` logs a warning and skips). If the culture is also a kingdom with NPC clans/lords, set `gold` and `influence` so those lords get their startup gold/influence too.
4. **Add equipment roster pairs** for every `title_type` value used by the new culture's youth_menu options. For each option, add a male and female `<EquipmentRoster id="player_char_creation_{culture}_{title_type}_m">` / `..._f` entry to whichever equipment XML covers that culture (vanilla `sandbox_equipment_sets.xml` for vanilla cultures, or `taom_char_creation_equipment.xml` for custom). Without these, the player exits CC with vanilla default equipment and `PlayerEquipmentService` logs a "roster not found" warning.
5. If the culture has no mounts (e.g., dwarves), ensure the CC equipment rosters omit the Horse slot. Patch20_NarrativeHorseGuard will handle the crash guard automatically.
6. The culture must exist in `MBObjectManager` (i.e., defined in `spcultures.xml` or `taom_spcultures.xml`) or it will be skipped with a warning.

## LOTRLOME `as_<race>_facegen` action_set requirement (live in LOTRLOME_Armory, not TAOM)

CC parents + child preview at every narrative stage are rendered by the engine via a lookup of `as_<race>_facegen` (male) and `as_<race>_female_facegen` (female), where `<race>` is whatever name is registered for the player's race in `FaceGen.GetRaceNames()`. The lookup target lives in **LOTRLOME_Armory's `action_sets.xml`**, not in TAOM — TAOM's own `Main/_Module/ModuleData/action_sets.xml` was removed on 2026-05-04 (deliberate dead-duplicate cleanup, commit `307df40`). Any custom race a TAOM culture uses must have a matching `_facegen` action_set in LOTRLOME, fully populated.

**Two failure modes that have shipped before:**

1. **No `as_<race>_facegen` at all.** Engine falls back to a default that doesn't bind to the race's skeleton → contorted-mesh on the parent menu. Caught 2026-05-22: elf (Mirkwood / Rivendell) had no facegen entry in LOTRLOME despite the 2026-05-04 patch claiming to fix elves — the patch only added 1.3 action-type aliases to 12 *pre-existing* facegens (dwarf/uruk/orc/etc.) and never authored the missing elf pair.

2. **Slim facegen entry (declares only the 14 CC parent action types).** Parent menu works but Early Childhood + every subsequent CC stage breaks — child agent renders lying down / T-posed. The engine does NOT fall through `base_set` for `act_childhood_*` / `act_character_creation_toddler_*` / `act_inventory_*` / `act_stand_*` / `act_sit_*` / `act_rider_story_background_*` / `act_horse_story_background_*` action types — they must be declared **directly** in the facegen action_set. Caught 2026-05-22 (same session, v1→v2 same-day iteration on the elf fix).

**Canonical fix recipe** when adding any new race-bearing TAOM culture (e.g., hobbit / halfling / man-of-the-west):

1. Verify LOTRLOME's `monsters.xml` defines the race id, and capture which `action_set=` its monster references (usually `as_<skeleton>_warrior` — `as_human_warrior` for human-skeleton races, `as_dwarf_warrior` for dwarf-skeleton, etc.).
2. Copy LOTRLOME's `as_dwarf_facegen` (lines ~16812-17134) and `as_dwarf_female_facegen` (lines ~17135-17232) blocks **verbatim** from `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml`.
3. Rename two attributes per block, nothing else:
   - Male: `id="as_dwarf_facegen"` → `id="as_<race>_facegen"`; `base_set="as_dwarf_warrior"` → `base_set="<the action_set the monster references>"`.
   - Female: `id="as_dwarf_female_facegen"` → `id="as_<race>_female_facegen"`; `base_set="as_dwarf_facegen"` → `base_set="as_<race>_facegen"`.
4. Append both blocks before the closing `</action_sets>` in BOTH `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml` (live) AND [`docs/reference/lotrlome-armory-snapshot/action_sets.xml`](../reference/lotrlome-armory-snapshot/action_sets.xml) (tracked snapshot — kept in lockstep).
5. Sanity-check with `python -c "import xml.etree.ElementTree as ET; ET.parse('...')"`. Expected size after copy: 106 male actions + 31 female actions per race, matching the dwarf reference exactly.
6. Update the per-race checklist in [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md) so the next restore from snapshot doesn't drop the new entries.
7. **Verify in-game at EVERY CC stage**, not just the parent menu. Parent-menu success does not imply Early Childhood success — they're separate failure modes from the same root cause class.

All animation files referenced in the dwarf block (`anim_male_custom`, `anim_childhood_*`, `anim_father_*`, `anim_mother_*`, `anim_toddler_*`, `anim_rider_story_background_*`) are skeleton-flexible — they work on dwarf, human, orc, uruk, and elf skeletons identically. No re-targeting needed even for non-human-skeleton races.

**Current race coverage (verified 2026-05-22):** all 10 race ids TAOM consumes (`berserker`, `cave_troll`, `dg_uruk`, `dwarf`, `elf`, `goblin`, `orc`, `pale_uruk`, `uruk`, `uruk_hai`) have complete `_facegen` entries with 106/31 action parity. `human` uses the engine default. The 3 LOTRLOME-only races TAOM doesn't consume (`nazghul`, `hill_troll`, `saruman`) are also complete and ride along in the snapshot.

See:
- Memory `feedback_lotrlome_action_set_aliases.md` — recurring-failure notes + recipe.
- [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md) — per-race restoration checklist + post-restore sanity grep.
- [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](../reviews/rca-elf-cc-facegen-2026-05-22.md) — full RCA on the slim-vs-full iteration.

### Vanilla age-30 animation override (Patch20)

Beyond the LOTRLOME data layer, vanilla `CharacterCreationCampaignBehavior.AgeSelectionAdultOptionOnSelect` hard-codes `SetAnimationId("act_childhood_athlete")` at the Starting Age menu's age-30 option, and that animation produces a horizontally-stretched / lying-down pose on the human_skeleton chain in vanilla Bannerlord — affecting **every** TAOM race (orc / dwarf / uruk / elf / human). The other three age handlers (`_focus` at 20, `_sharp` at 40, `_tough` at 50) work correctly on the same skeleton chain. LOTRLOME data is fine: `as_<race>_facegen` blocks declare `act_childhood_athlete → anim_childhood_athlete` identically across races, and the action type is registered in `Native/ModuleData/action_types.xml`. The bug is at the `anim_childhood_athlete ↔ human_skeleton` binding layer at runtime.

`CharacterCreationCampaignBehavior_AgeSelectionAdultOptionOnSelect_Patch` (in [`Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`](../../Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs), Patch20 category) is a thin Harmony Postfix on `AgeSelectionAdultOptionOnSelect` that re-sets the animation to `act_childhood_focus` (the proven-working age-20 anim) post-vanilla. All other vanilla effects — `ChangeAge(30)`, `SetEquipment`, `SetBirthDay(-30y)`, `StartingAge = 30`, focus/attribute bonuses — are preserved; only the visible animation changes.

The Postfix's scope is deliberately limited to the age-30 code path. Vanilla references `act_childhood_athlete` in two other locations (`CharacterCreationCampaignBehavior.cs:1599` + `:2016`, both youth backstory option handlers); those are untouched. If a future report surfaces broken poses at those backstory options, repeat the same Postfix recipe targeting `MerchantsParentsOptionOnSelect` / whichever specific method.

## Changelog

- 2026-08-03 — `feat(possession)`: the finalize grants are re-applied after a multiplayer join hand-off substitutes a host-authored hero for the one character creation produced. No file in this feature changed — the effective outcome of character creation under co-op did. Race, culture startup gold, career and special-resource seed are re-invoked; CC equipment and the starting-settlement teleport are not. See [player-possession.md](player-possession.md).
- 2026-06-01 — `fix(character-creation)` #264: culture-appropriate family/clan name (assign `Hero.MainHero.Culture` before `SetSelectedCulture`; Rohan/`vlandia` "dey Corvand" override) + `Patch44_CCNameAutofill` pre-fills the blank Review-stage name field via `ExecuteRandomizeName()`.
- 2026-05-30 — `balance(cc)`: cut the CC bonus budget back to vanilla (7→5 focus) by zeroing the two TAOM-added sources — the Career stage and the `cultures.json` culture-base bonus.
- 2026-05-22 — `fix(cc)`: authored missing `as_elf_facegen` + `as_elf_female_facegen` so elves render upright on the parent menu; `Patch20` age-30 animation override (re-set `act_childhood_athlete` → `act_childhood_focus`); `migration(ui)` flipped CC narrative + culture stage ListPanel direction after the v1.4.0 layout-fix regression.
- 2026-05-13 — CharacterCreation service-locator → constructor injection (#125); `Patch20` SpawnNonHuman finalizer now logs generic NREs before suppressing (#163); CC × HeroRace race-ID save/load round-trip pinned (#181).
- 2026-05-06 — Re-implemented `Patch9_RaceFilter` (culture-restricted race dropdown via `FaceGenVM.Refresh` postfix); added `Patch29_CCBodyProperties` (per-culture default BodyProperties, #108); `SetPlayerRace` honors the player's FaceGen choice; player startup gold + CC equipment persistence on finalize.
- 2026-04-14 — Added a 6th narrative stage letting players choose a culture-eligible career during character creation.
- 2026-04-08 — Added missing CC parent equipment rosters for `shaghana` and `abanissa`.
- 2026-03-31 — Fixed 3 cascading dwarf-CC crashes via `Patch20_NarrativeHorseGuard` (3 Prefixes + 1 Finalizer covering all no-mount horse-read sites) (#50).
- 2026-03-15 — Fixed non-human races (dwarf/elf/uruk) displaying as human models during character creation (#22).
- 2026-03-12 — Ported the LOTRAOM character-creation narrative system to the 1.3.x handler-based API + added CC equipment rosters for all cultures.

## GitHub Issue
- **Race filter (Patch9_RaceFilter re-implementation):** [#107](https://github.com/haterade22/TAOM/issues/107) — closed 2026-05-06
- **Per-culture default BodyProperties (Patch29_CCBodyProperties):** added in same session, not separately ticketed
- **Elf CC rendering + vanilla age-30 animation override (3-iteration fix chain):** [#227](https://github.com/haterade22/TAOM/issues/227) — closed 2026-05-26 (retroactive ticket; v1 ed1131a + v2 0f3a7c0 + v3 b1c70db shipped 2026-05-22). Full RCA at [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](../reviews/rca-elf-cc-facegen-2026-05-22.md).
- **Review-stage name autofill (Patch44_CCNameAutofill) + culture-appropriate family name:** [#264](https://github.com/haterade22/TAOM/issues/264) — 2026-06-01. Companion fix in `FactionMap.CultureSettingService` (clan name from selected culture; `vlandia`/Rohan "dey Corvand" override).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/race-age-system.md](./race-age-system.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
