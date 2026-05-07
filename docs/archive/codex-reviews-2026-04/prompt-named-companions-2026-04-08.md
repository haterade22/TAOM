# Codex Adversarial Review: Named Companions + Wanderer Race Fix

## Feature Description

Two changes in one review:
1. **Named Companion System** -- XML-driven system for placing lore-significant characters (Aragorn, Legolas, Gimli, etc.) as recruitable wanderer companions in specific settlements. Uses `is_hero="true"` + `occupation="Wanderer"` to be invisible to vanilla CompanionsCampaignBehavior while triggering vanilla recruitment dialog. 18 companions across 7 cultures.
2. **Wanderer Race Fix** -- Added `race="elf"` to 30 elven wanderer templates (Rivendell/Mirkwood/Lothlorien) and fixed 10 Dol Guldur wanderers from `race="orc"` to `race="dg_uruk"` with corrected face templates.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale

NOTE: "rohan" is NOT a valid culture ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- Main/_Module/ModuleData/named_companions/named_companion_config.json -- spawn config
- Main/_Module/ModuleData/named_companions/named_companions.xml -- 18 NPC definitions
- Main/Features/NamedCompanions/NamedCompanionService.cs -- core logic
- Main/Adapters/NamedCompanionAdapter.cs -- TaleWorlds API boundary

## KNOWN SUSPECTS

Codex: for each suspect below, CONFIRM the bug exists or DISPUTE with evidence.

1. UNUSED FIELD: NamedCompanionBehavior.cs injects IModLogger but the `_logger` field is never used. Dead code?

2. HERO STATE ON LOAD: NamedCompanionAdapter.PlaceInSettlement() calls `hero.ChangeState(Hero.CharacterStates.Active)` before `EnterSettlementAction.ApplyForCharacterOnly()`. On game load via EnsureCompanionsPlaced(), is this safe for a hero already in Active state? Does ChangeState to Active when already Active cause issues?

3. IS_PLACED CHECK: NamedCompanionAdapter.IsPlacedInSettlement() checks `StayingInSettlement != null || CurrentSettlement != null`. But a recruited companion traveling with the player party would have `CurrentSettlement != null` when visiting a town. Would EnsureCompanionsPlaced() skip re-placement for recruited companions correctly, or could it try to re-place a companion the player already recruited?

4. RACE DOUBLE-SET: SpawnCompanions() calls SetHeroRace() for race, but the XML already has `race="elf"` which BasicCharacterObject.Deserialize() reads. Is this redundant, or is there a timing issue where the XML race is lost before SpawnCompanions runs?

5. CONFIG SETTLEMENT IDS: Verify all 18 settlement IDs in named_companion_config.json exist in settlements.xml. Specifically check: town_EN2, town_EN3, town_M1, town_E1, town_E2, town_E3, town_EW1, town_EW2, town_A3, town_R1, town_isengard.

6. WANDERER RACE FIX: In taom_wanderers.xml, confirm that ALL 10 Rivendell, 10 Mirkwood, and 10 Lothlorien wanderers have `race="elf"`, and ALL 10 Dolguldur wanderers have `race="dg_uruk"` with `BodyProperty.fighter_dolguldur`.

## FILES TO REVIEW

### C# -- Named Companion Feature
- Main/Features/NamedCompanions/Domain/NamedCompanionDefinition.cs
- Main/Features/NamedCompanions/INamedCompanionConfigProvider.cs
- Main/Features/NamedCompanions/NamedCompanionConfigProvider.cs
- Main/Features/NamedCompanions/INamedCompanionService.cs
- Main/Features/NamedCompanions/NamedCompanionService.cs
- Main/Features/NamedCompanions/NamedCompanionBehavior.cs
- Main/Features/NamedCompanions/NamedCompanionIoC.cs
- Main/Adapters/INamedCompanionAdapter.cs
- Main/Adapters/NamedCompanionAdapter.cs

### Tests
- TAOM.Tests/Features/NamedCompanions/NamedCompanionConfigProviderTests.cs
- TAOM.Tests/Features/NamedCompanions/NamedCompanionServiceTests.cs

### Config and Data
- Main/_Module/ModuleData/named_companions/named_companion_config.json
- Main/_Module/ModuleData/named_companions/named_companions.xml
- Main/_Module/ModuleData/named_companions/named_companion_strings.xml
- Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_named_companions.xml
- Main/_Module/ModuleData/characters/heroes.xml (named companion Hero entries at bottom)

### Wiring
- Main/IoC.cs (NamedCompanionIoC registration line)
- Main/SubModule.cs (AddBehavior line)
- Main/_Module/SubModule.xml (XmlNode entries for named companions)

### Wanderer Race Fix
- Main/_Module/ModuleData/taom_wanderers.xml (race attributes on elven + DG wanderers)

### Existing Race Infrastructure (context only)
- Main/Features/HeroRace/RacePersistenceService.cs
- Main/Features/HeroRace/RacePersistenceBehavior.cs
- Main/Core/Domain/RaceManager.cs

## REQUIRED SECTIONS

### VANILLA CODE

Decompile and paste these targets:
- CompanionsCampaignBehavior.TryKillCompanion() -- verify HasMet check protects named companions
- HeroCreator.CreateSpecialHero() vs CreateBasicHero() -- verify is_hero="true" NPCs are NOT cloned as templates
- EnterSettlementAction.ApplyForCharacterOnly() -- verify safe for Wanderer-occupation heroes
- Hero.ChangeState() -- verify calling Active on already-Active hero is safe
- Hero.SetHasMet() -- verify it exists and what it does

Vanilla sources at: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\

### NAMED COMPANION LIFECYCLE

Trace these scenarios end-to-end:
1. New game: XML loads -> heroes created -> NamedCompanionBehavior fires at index 1 -> PlaceInSettlement -> MarkAsMet. Does the hero exist in AllAliveHeroes at this point?
2. Game load: EnsureCompanionsPlaced() runs -> checks each companion. What if the companion was recruited by the player? Does IsPlacedInSettlement return true?
3. Companion killed in battle: Is the Hero removed from AllAliveHeroes? Does EnsureCompanionsPlaced try to respawn them?
4. TryKillCompanion daily tick: Does HasMet=true actually protect? What if vanilla changes the wanderer pool iteration?

### CONFIG CROSS-REFERENCE

Cross-reference ALL 18 character_id values in named_companion_config.json against the actual id attributes in named_companions.xml. Any mismatch?

Cross-reference ALL 18 settlement IDs against Main/_Module/ModuleData/settlements.xml.

Cross-reference ALL race values against the race names available in Main/_Module/ModuleData/Races/monsters.xml.

### WANDERER RACE AUDIT

For taom_wanderers.xml, verify:
- Count of `race="elf"` (should be exactly 30)
- Count of `race="dg_uruk"` (should be exactly 10)
- Count of `race="orc"` in dolguldur section (should be 0)
- Count of `BodyProperty.fighter_empire` in dolguldur section (should be 0)
- Count of `BodyProperty.fighter_dolguldur` in dolguldur section (should be 10)

## QUALITY GATES

- Every finding must include file path and line number
- Every finding must show TAOM code AND vanilla code side-by-side where relevant
- Config IDs must be cross-referenced against actual files, not assumed
- Do not flag code that matches vanilla behavior as a bug
- Do not claim something is missing without grepping the full codebase

## PRIOR REVIEW LESSONS

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches (4 reviews). Vanilla decompilation caught missing gates (3 reviews). Lifecycle tracing caught stale caches (2 reviews).

FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections silently. Codex claimed "config looks valid" without actually checking.

## OUTPUT

Write findings to: docs/reviews/codex-adversarial-named-companions-2026-04-08.md
