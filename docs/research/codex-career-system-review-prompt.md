# Codex Adversarial Review: TAOM Career System vs TOR_Core Career System

> **Purpose:** After implementing the TAOM career system, use this prompt to tear it apart by comparing against TOR_Core's battle-tested career system. Find every gap, missed edge case, silent failure, and architectural weakness.
> **When to use:** After Phase 2 implementation is complete and building green. NOT before.

---

## Setup

Before starting, clone the TOR reference implementation (if not already present):
```bash
git clone --branch development --depth 1 https://github.com/TheOldRealms/TOR_Core /tmp/TOR_Core
```

## Your Role

You are an adversarial code reviewer. Your job is to find problems, not praise good work. Compare TAOM's career system against TOR's mature implementation and flag every place where TAOM cut corners, missed mechanics, or introduced fragility.

## Research Reference

Read `docs/research/tor-career-system.md` first -- it contains our comprehensive reverse-engineering of TOR's career system (class hierarchy, data schemas, flow diagrams, all 22 careers, 44 PassiveEffectTypes, 16 Harmony patches, save/load, UI). This is your ground truth for what TOR does.

## Codebases

### TAOM (our implementation -- the one under review)
All files at `c:/Users/mikew/source/repos/TAOM/`:

**Core career system** (expected location: `Main/Features/CareerSystem/`):
- `Core/` -- CareerObject, CareerChoiceObject, CareerChoiceGroupObject, enums (ChoiceType, PassiveEffectType, OperationType)
- `Services/` -- ICareerService, CareerService (progression, choice selection, passive application)
- `Services/` -- ICareerStorageService (per-hero career data persistence)
- `Services/` -- ICareerConfigProvider (XML career definitions loader)
- `Services/` -- ICareerHelper or CareerHelper (caching, passive application, charge calculation)

**Career abilities** (expected location: `Main/Features/CareerSystem/`):
- Ability template system (XML-driven career ability definitions)
- CareerAbility class (charge types, casting, cooldown)
- AbilityManagerMissionLogic or equivalent (targeting, time slow, crosshair)
- Per-career charge supplier functions

**Entry points:**
- CampaignBehavior (career lifecycle, event hooks, SyncData)
- MissionBehavior (battle-time career effects)
- Harmony patches (ViewModel extension, perk reset, mission patches)
- GameModel overrides (PassiveEffectType integration into existing TAOM models)

**UI:**
- CareerScreen (prefab + GameState + ViewModel hierarchy)
- CharacterDeveloper extension (career button injection)
- PartyScreen extension (career action buttons per troop)
- Battle HUD (career ability charge bar, cooldown display)
- Character creation integration (career selection stage)

**Config:**
- Career definitions XML (careers, choice groups, choices, abilities)
- Troop cost/ability XMLs

**Tests:**
- All test files in `TAOM.Tests/Features/CareerSystem/`

### TOR_Core (the reference implementation)
All files at `/tmp/TOR_Core/`:

**Core career system:**
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerObject.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerChoiceObject.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerChoiceGroupObject.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerHelper.cs` -- **50+ methods, the glue layer**
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerScreen.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerScreenGameState.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerScreenVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/TORCareers.cs` -- 22 career definitions
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/TORCareerChoices.cs` -- choice registry
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/TORCareerChoiceGroups.cs` -- ~154 groups

**Per-career choice trees (one class each):**
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/Choices/TORCareerChoicesBase.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/Choices/*.cs` (22 files)

**Career abilities:**
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/CareerAbility.cs`
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/AbilityManagerMissionLogic.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerAbilityChargeSupplier.cs`
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/AbilityTemplate.cs`
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/Scripts/CareerAbilityScript.cs`
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/Scripts/CareerAbilityMissleScript.cs`

**Campaign behaviors:**
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/Careers/TORCareerPerkCampaignBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/Careers/CareerDialogOptionsCampaignBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomDialogs/CareerSwitchCampaignBehavior.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CampaignMechanics/CustomEvents/SimpleCareerQuestBehavior.cs`

**Battle behavior:**
- `/tmp/TOR_Core/CSharpSourceCode/BattleMechanics/CareerPerkMissionBehavior.cs`

**Career buttons (party screen per-troop actions):**
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerButton/CareerButtonBehaviorBase.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerButton/CareerButtons.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerButton/SpecialbuttonHandler.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerButton/*.cs` (13 per-career buttons)

**Persistence:**
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/ExtendedInfoSystem/HeroExtendedInfo.cs` -- CareerID, CareerChoices fields
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/HeroExtensions.cs` -- GetCareer(), AddCareer(), HasCareerChoice()
- `/tmp/TOR_Core/CSharpSourceCode/SaveGameSystem/SaveableTypeDefiners.cs`

**Mutation system:**
- MutationObject in CareerChoiceObject.cs -- runtime modification of ability/effect templates

**Harmony patches:**
- `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/ViewModelPatches.cs` -- 6 patches on ViewModel base for extension system
- `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/PerkResetRelatedPatch.cs`
- `/tmp/TOR_Core/CSharpSourceCode/HarmonyPatches/MissionPatches.cs` -- career-related mission patches

**UI:**
- `/tmp/TOR_Core/GUI/Prefabs/CareerSystem/CareerScreen.xml`
- `/tmp/TOR_Core/GUI/Prefabs/CharacterDeveloper/CharacterDeveloper.xml`
- `/tmp/TOR_Core/GUI/Brushes/TorBrushes.xml`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerObjectVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerChoiceObjectVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerChoiceGroupObjectVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/CharacterDevelopment/CareerSystem/CareerAbilityEffectVM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/AbilitySystem/CareerAbilityHUD_VM.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/UI/CharacterDeveloperVMExtension.cs`
- `/tmp/TOR_Core/CSharpSourceCode/Extensions/UI/PartyCharacterVMExtension.cs`

**GameModels that reference career perks (21 total):**
- `/tmp/TOR_Core/CSharpSourceCode/Models/TOR*.cs` -- grep for `CareerHelper.ApplyBasicCareerPassives`

**SubModule registration:**
- `/tmp/TOR_Core/CSharpSourceCode/SubModule.cs`

---

## Known Architecture Differences (context for fair comparison)

TAOM's career system was designed to differ from TOR in these ways. Do NOT flag these as bugs:

1. **Data-driven vs code-driven:** TAOM defines careers in XML config; TOR hardcodes in C# static classes. This is intentional -- TAOM wants to add careers without recompilation.
2. **Adapter pattern:** TAOM wraps TaleWorlds types behind `IHeroAdapter` etc.; TOR accesses `Hero` directly. TAOM's boundary is stricter by design.
3. **DryIoc vs static singletons:** TAOM uses constructor injection; TOR uses `TORCareers.Instance`. Both work, TAOM is more testable.
4. **UIExtenderEx vs custom VM patches:** If TAOM uses UIExtenderEx for career UI injection instead of TOR's 6-patch ViewModel extension system, that's a valid design choice.
5. **Level-based career points:** Both use `hero.Level + 1` for max choices. This is confirmed identical.

DO flag if TAOM's data-driven approach silently drops capabilities that TOR's code-driven approach provides (e.g., mutation lambdas that can't be expressed in XML).

---

## Review Tasks (in order of importance)

### 1. CRITICAL: Career Progression Correctness

Read TOR's `CareerChoiceObject.cs` (Initialize, MutateAbility), `CareerChoiceGroupObject.cs` (IsActiveForHero, tier gating), and `TORCareerPerkCampaignBehavior.cs`.

Compare against TAOM's equivalent:
- Does TAOM enforce tier gating correctly? (Tier 2/3 locked until attribute/quest unlocks them)
- Does TAOM enforce mutual exclusion within a tier? (Only one keystone per tier)
- Does TAOM enforce the max career points formula: `Math.Min(hero.Level + 1, MaxPerkPoints + 1)`?
- Can a player select the same choice twice? TOR prevents this via `CareerChoices.Contains(StringId)` check.
- Does TAOM auto-select the root node on career assignment?
- Does TAOM refresh the CareerHelper cache after every choice change?

### 2. CRITICAL: Save/Load Career Data

Read TOR's `HeroExtendedInfo.cs` ([SaveableField] for CareerID, CareerChoices list) and `ExtendedInfoManager.cs`.

Compare against TAOM's persistence:
- What fields are persisted per hero? (CareerID, selected choices, acquired abilities?)
- What happens loading a pre-career save? Does the hero start with no career gracefully?
- What happens if a career is removed from config between saves? Does the hero crash on load?
- Does TAOM use Bannerlord's `[SaveableField]` or `SyncData` pattern? What are the trade-offs?

### 3. CRITICAL: Mutation System Completeness

TOR's mutation system is the core innovation -- career choices modify ability templates at runtime via `MutationObject` (target type, property name, calculator function, operation type).

If TAOM implements this:
- Can XML express everything TOR's C# lambdas can? (e.g., `AddSkillEffectToValue` which reads the hero's skill level)
- Does TAOM clone ability templates per-hero before mutating? (TOR does -- mutations must not affect other heroes)
- Does TAOM apply mutations in the correct order? (Root node first, then selected choices)
- Does TAOM support all 3 mutation target types? (AbilityTemplate, TriggeredEffectTemplate, StatusEffectTemplate)

If TAOM does NOT implement mutations:
- Flag this as a CRITICAL gap -- it's the mechanism that makes career choices meaningful beyond passive stats.

### 4. HIGH: Career Ability System

Read TOR's `CareerAbility.cs` (charge mechanics, activation, double-use), `CareerAbilityChargeSupplier.cs` (12+ per-career charge functions), `AbilityManagerMissionLogic.cs` (targeting mode, time slow).

Compare:
- Does TAOM implement all charge types? (CooldownOnly, DamageDone, Kills, DamageTaken, Healed, Custom)
- Does TAOM handle the slow-time targeting mode? (0.3x time scale, weapon sheath/restore)
- Does TAOM handle the crosshair system? (Self, Missile, SingleTarget, Wind, Pointer, TargetedAOE)
- Does TAOM reset charge to 0 after ability activation?
- Does TAOM support double-use keystones? (e.g., SecretsOfTheGrailKeystone allows second consecutive cast)

### 5. HIGH: PassiveEffect Integration into GameModels

TOR has 44 PassiveEffectTypes applied via `CareerHelper.ApplyBasicCareerPassives()` across 21 GameModels.

Compare:
- How many PassiveEffectTypes does TAOM implement? List all.
- Which TAOM GameModels call career passive application? List all.
- For each TOR model that calls `ApplyBasicCareerPassives`, does the corresponding TAOM model also call it?
- Are there TAOM-specific GameModels (e.g., TaomMilitaryPowerModel) that should have career integration but don't?
- Does TAOM use the same `ExplainedNumber` integration pattern? (Add vs AddFactor for percentage vs flat bonuses)

### 6. HIGH: UI Flow Correctness

Read TOR's CareerScreen.xml + CareerScreenVM.cs + CharacterDeveloperVMExtension.cs.

Compare:
- Does TAOM's career screen show all 3 tiers with correct lock/unlock state?
- Does TAOM's career screen update FreeCareerPoints correctly after each selection?
- Does the "Career" button appear in CharacterDeveloper only when hero has a career?
- Does the career screen use a proper GameState (like TOR's CareerScreenGameState)?
- Does career selection during character creation correctly set CareerID + root node + initial attributes?

### 7. MEDIUM: Battle Mission Behavior

Read TOR's `CareerPerkMissionBehavior.cs` (per-second tick, per-career combat effects).

Compare:
- Does TAOM implement per-career battle effects? (Necrarch spell charge, Ironbreaker damage reflect, etc.)
- Does TAOM's mission behavior tick at the correct interval?
- Does TAOM track mission-scoped variables? (kill counts, block counts, etc.)
- Does TAOM handle career ability casting during missions?

### 8. MEDIUM: Career Switching

Read TOR's `CareerSwitchCampaignBehavior.cs`.

Compare:
- Can TAOM players switch careers? If so, through what mechanism?
- Does switching clear all existing choices and reset to root?
- Are there restrictions on switching? (faction match, minimum tier, race checks)

### 9. LOW: Event Coverage

Read TOR's `TORCareerPerkCampaignBehavior.cs` event subscriptions (11 events).

List which TAOM hooks and which are missing:
- OnSessionLaunched, DailyTick, WeeklyTick, ItemsLooted, UnitRecruited, PlayerBattleEnd, ItemDuplicated, EquipmentSmelted, ItemCrafted, ItemsRefined, HourlyTick

---

## TAOM-Specific Checks (things TOR doesn't have)

These are TAOM-specific requirements. Verify even if TOR doesn't do them:

1. **Race-awareness:** Do career definitions respect TAOM's race system? Can an elf career be assigned to a dwarf? Is there validation?
2. **Cultural feat interaction:** Do career passives stack correctly with existing TaomCulturalFeats bonuses? (e.g., both adding to PartySpeed -- do they double-dip or compose correctly via ExplainedNumber?)
3. **Adapter pattern compliance:** Do ALL career services use `IHeroAdapter` etc., never raw `Hero`? (ADR-007)
4. **Existing CharacterCreation flow:** Does career selection break the existing CC stages in `Main/Features/CharacterCreation/`?
5. **LOTRLOME_Armory validation:** If careers grant equipment, do item IDs validate against the Armory module?

## Kingdom/Culture ID Reference

TAOM uses vanilla Bannerlord IDs remapped to LOTR:
- `empire_w`=Gondor, `empire_s`=Mordor, `empire`=Dunland
- `vlandia`=Rohan, `battania`=Khand, `aserai`=Harad
- `khuzait`=Easterlings, `sturgia`=Dale/North
- `erebor`=Erebor, `rivendell`=Rivendell, `lothlorien`=Lothlorien
- `mirkwood`=Mirkwood, `isengard`=Isengard, `gundabad`=Gundabad
- `dolguldur`=DolGuldur (NOT "dol_guldur"), `umbar`=Umbar

"rohan", "gondor", "mordor" are NOT valid kingdom StringIds. They are lore names only.

---

## Output Format

```
## CRITICAL FINDINGS
1. [TITLE]
   TOR does: [what TOR does]
   TAOM does: [what TAOM does or does not do]
   Impact: [what goes wrong]
   Fix: [specific fix with file path and line number]

## HIGH FINDINGS
[...]

## MEDIUM FINDINGS
[...]

## LOW FINDINGS
[...]

## WHAT TAOM DOES BETTER
[Areas where TAOM's approach is cleaner -- be honest]

## ARCHITECTURE COMPARISON
| Aspect | TOR | TAOM | Verdict |
|--------|-----|------|---------|
| Career definitions | Code-driven (22 C# classes) | XML-driven | [trade-offs] |
| Mutation system | C# lambdas | [TAOM approach] | [can XML match?] |
| Passive application | CareerHelper cache + 21 models | [TAOM approach] | [coverage gap] |
| Charge system | 12 per-career suppliers | [TAOM approach] | [completeness] |
| Save/load | [SaveableField] on HeroExtendedInfo | [TAOM approach] | [robustness] |
| UI depth | CareerScreen + CharDev + Party + HUD | [TAOM approach] | [gap] |
| Test coverage | Zero | [TAOM count] | [assessment] |
| Ability casting | Full targeting + time slow + crosshair | [TAOM approach] | [completeness] |
```

## Rules
- Read EVERY file listed above. Do not summarize from file names alone.
- Read `docs/research/tor-career-system.md` as your TOR reference -- it has all class details, flow diagrams, and schemas.
- When you find a gap, cite the specific TOR file/line and the specific TAOM file/line.
- Be specific about fixes -- file paths, line numbers, what to change.
- If TAOM's data-driven approach handles something better than TOR's hardcoded approach, say so.
- For Bannerlord API verification, check decompiled source at `E:\Decompiled_Bannerlord\`.
- TAOM targets Bannerlord v1.3.15. TOR targets v1.3.2. Flag any API differences.
- Do NOT flag the Known Architecture Differences as bugs (section above).
