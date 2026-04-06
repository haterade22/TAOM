# Codex Adversarial Review: CharacterCreation

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. The patch targets exist and sampled skill IDs match vanilla `DefaultSkills`, but the current CharacterCreation data still registers two cultures with no narrative content, and the no-horse guard only suppresses crashes on the first null-state path — it does not clear vanilla's persistent horse placeholder when a player switches from a mounted culture/title to a no-horse one.

## Section 1: Vanilla Code

### CharacterCreationCampaignBehavior (decompiled)

`GetYouthMenuNarrativeMenuCharacterArgs()` and `GetAdultMenuNarrativeMenuCharacterArgs()` create persistent horse placeholders with `narrative_character_horse` entries (lines 2171-2172, 2827-2830, 3307-3310).

### CharacterCreationManager.ModifyMenuCharacters (decompiled)

Only updates characters whose IDs appear in the returned args list; unmatched characters are not reset (lines 245-281).

### CharacterCreationNarrativeStageView (decompiled)

`SpawnNonHumanNarrativeMenuCharacter()` blindly iterates every `CurrentMenu.Characters` entry and spawns non-humans (lines 145-156).

### FaceGen.GetRaceNames (decompiled)

Returns string array of race names. The Postfix no-op forces Harmony to re-evaluate the method, which triggers race index recalculation in `FaceGenVM`.

## Section 2: Patch Target Analysis

- **Narrative patches:** Vanilla always expects a horse in the args list. No vanilla path handles null horse items — the prefix is correctly required.
- **SpawnNonHuman finalizer:** Catches `ArgumentNullException` from null horse item ID. Correct exception type for the crash path.
- **FaceGen_GetRaceNames_Patch:** The no-op Postfix works because Harmony's patching forces the JIT to re-emit the method, which picks up TAOM's extended race list and prevents `FaceGenVM` from index-out-of-bounds when iterating races.

## Section 3: JSON Config Validation

- **Skill names sampled:** `TwoHanded`, `OneHanded`, `Athletics`, `Riding`, `Bow` — all match vanilla `DefaultSkills` property names. No typos found in sampled entries.
- **cultures.json vs taom_spcultures.xml:** All CultureId values match actual TAOM culture StringIds.
- **Missing cultures:** `shaghana` and `abanissa` are registered in `cultures.json` but have NO matching entries in any narrative menu JSON. See Finding 1.

## Findings

### [HIGH] shaghana and abanissa registered for CC but have no narrative options in any menu JSON

**File:** `cultures.json:143-160`

**TAOM code:** `cultures.json` registers both cultures. `CharacterCreationContentService.RegisterNarrativeMenus()` removes vanilla options then repopulates from JSON (lines 91-95, 111-114). `NarrativeMenuBuilder.BuildOption()` gates each option on exact `selectedCulture.StringId == cultureId` (lines 84-99).

**Evidence:** `rg '"culture_id": "(shaghana|abanissa)"'` across all five menu JSON files returns zero matches. Feature docs say each culture must add narrative entries to each menu.

**Impact:** Both Harad subcultures can be offered in CC but then have no culture-matching menu options, leaving those paths without a valid character creation experience.

**Remediation:** Add full narrative coverage for `shaghana` and `abanissa` across all five menu JSON files, or stop registering those cultures in CC until their menu data exists.

### [MEDIUM] No-horse prefix patches don't clear vanilla's persistent horse character — stale horse leaks between cultures

**File:** `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:40-163`

**TAOM code:** All three Prefix patches return `NarrativeMenuCharacterArgsList.FromGuardArgs(args)`, containing only the player character and no `narrative_character_horse` entry.

**Vanilla code:** Each menu creates a persistent horse placeholder. `ModifyMenuCharacters()` only updates characters whose IDs appear in the returned args list — unmatched characters are not reset. The stage view then blindly spawns every non-human character.

**Evidence:** If the player first visits a mounted culture (horse placeholder gets valid item IDs), then switches to a no-horse culture, the old horse placeholder retains valid IDs. The finalizer hides the null-key crash but doesn't clear stale mount state.

**Remediation:** When the guard triggers, explicitly update or clear the `narrative_character_horse` slot instead of omitting it. Return a horse args entry with empty/null-safe state plus a view-side guard that skips spawning when no horse item is present.

## Observations

- Sampled skill names across narrative JSONs all match vanilla `DefaultSkills` — no typos found
- `FaceGen_GetRaceNames_Patch` no-op mechanism is correct and well-understood
- All 10 custom culture IDs in `cultures.json` match `taom_spcultures.xml` (excluding the `shaghana`/`abanissa` narrative gap)

## Recommended Next Steps

1. Add narrative JSON entries for `shaghana` and `abanissa` across all 5 menu files, or remove them from `cultures.json`
2. Fix horse-guard flow to actively clear the horse placeholder for no-horse cultures
3. Test the culture-switch path: mounted -> no-horse -> mounted
