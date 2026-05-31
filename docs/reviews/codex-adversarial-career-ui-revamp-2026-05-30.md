OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, $TMPDIR, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019e7c0f-c1f3-70b1-a9e5-fb565b6c9ba6
--------
user
# Adversarial review -- TAOM CareerSystem screen revamp (2026-05-30)

You are an adversarial reviewer. TAOM is a Lord of the Rings total-conversion mod of Mount & Blade II: Bannerlord v1.4.5. This session revamped the Career screen UI and adopted per-tier rank titles from TOR_Core (permission granted; GPLv3). Find real bugs. Confirm or DISPUTE each Known Suspect with evidence from the actual files. Do not flag vanilla-matching code as bugs. Use `--` not em-dash.

## TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
"rohan" is NOT valid (use vlandia). "dol_guldur" is NOT valid (use dolguldur).

## READ FIRST
- docs/reviews/tor-career-ui-comparison-2026-05-30.md (what was adopted from TOR and why)
- docs/features/career-system.md (feature overview)
- Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml (the rewritten prefab)

## What changed this session
C#:
- Main/Features/CareerSystem/UI/CareerScreenVM.cs -- removed Tier2GateBottomHalf/Tier3GateTopHalf/Tier3GateFull/Tier1Locked props; added Tier2RequirementText/Tier3RequirementText; tier labels Tier1/2/3Label now set from career.Rank1/2/3Name with a "{=taom_career_tierN}Tier N" fallback.
- Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs -- new computed `IsUnavailable => !_isTaken && !_isFreeToTake`; IsTaken/IsFreeToTake setters now also call OnPropertyChanged(nameof(IsUnavailable)).
- Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs -- GroupName now returns DisplayName (localized) or a HumanizeId(id) fallback ("ranger_of_ithilien_t1_a" -> "Path A").
- Main/Features/CareerSystem/Domain/CareerChoiceGroupDefinition.cs -- new optional `displayName` ctor param + DisplayName prop.
- Main/Features/CareerSystem/Domain/CareerDefinition.cs -- new optional `rank1Name`/`rank2Name`/`rank3Name` ctor params + Rank1/2/3Name props.
- Main/Features/CareerSystem/CareerConfigProvider.cs -- parse `display_name` on <ChoiceGroup> and `rank1_name`/`rank2_name`/`rank3_name` on <Career>.
- Main/Features/CareerSystem/CareerRegistry.cs + ICareerRegistry.cs -- new `int GetTierUnlockLevel(int tier)` (1/10/20, int.MaxValue otherwise); `IsTierAvailable` refactored to `tier in 1..3 && heroLevel >= GetTierUnlockLevel(tier)`.
Data:
- Main/_Module/ModuleData/career_system/taom_career_choices.xml -- 294 `display_name="{=taom_career_grp_<groupId>}<name>"` on <ChoiceGroup> (288 active groups + 6 inside a pre-existing DISABLED comment block for cave_troll_master).
- Main/_Module/ModuleData/career_system/taom_careers.xml -- 147 `rank1/2/3_name="{=taom_career_rank{N}_<careerId>}<title>"` on all 49 <Career>.
- Main/_Module/ModuleData/taom_module_strings.xml -- +441 `<string id="taom_career_grp_*"/>` and `taom_career_rank{1,2,3}_*` keys (and taom_career_tier_requirement).
- Main/_Module/GUI/TAOMSpriteData.xml -- ui_taom_career_system category SpriteSheetCount 1 -> 2, added `<SpriteSheetSize ID="2" Width="256" Height="256"/>`, added a <SpritePart> (Name=CareerSystem\career_point_pip, SheetID=2, SheetX=0, SheetY=0, Width=256, Height=256) and a <GenericSprite> for it. New PNG at Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/career_point_pip.png (256x256, alpha).
Tooling (review for data-corruption risk only): tools/apply_career_group_names.py, tools/apply_career_rank_names.py, tools/career_group_names.json, tools/career_rank_names.json.

## KNOWN SUSPECTS -- confirm or DISPUTE each with evidence
1. SPRITE-ATLAS SHEET-2 EDIT (highest risk). TAOM ships loose PNGs under GUI/SpriteParts/<category>/ with NO pre-baked sheet textures in the repo; TAOMSpriteData.xml carries per-sprite SheetID/SheetX/SheetY rectangles. The edit adds a SECOND sheet to the `ui_taom_career_system` category sized 256x256 (sheet 1 is 4096x4096) and puts career_point_pip alone on it at (0,0). QUESTION: in Bannerlord v1.4.5, is SheetID scoped per-category (so sheet "2" is valid within this category), and does the engine's runtime atlas assembly accept multiple sheets of DIFFERENT sizes within one category? Decompile/inspect TaleWorlds.TwoDimension / GauntletUI SpriteData + SpriteCategory loading (e.g. SpriteData.Deserialize, SpriteCategory, Sprite/SpritePart) to confirm the manifest shape is valid and will not break loading of the WHOLE category (which would also break plus_sign_icon/minus_sign_icon/portraits/abilities already on sheet 1). If multi-size-per-category is NOT supported, this is HIGH. If it is, DISPUTE.
2. VM<->PREFAB BINDING COMPLETENESS. For every `@Prop`, `{Collection}`, and `Command.X` in CareerScreen.xml, confirm a matching public [DataSourceProperty] / public method exists on the bound VM (screen-level on CareerScreenVM; node-level on CareerChoiceGroupObjectVM; choice-level on CareerChoiceObjectVM). Flag any dangling binding (renders blank) AND any removed gate property still referenced. Confirm NO `@Tier2GateBottomHalf`/`@Tier3GateTopHalf`/`@Tier3GateFull`/`@Tier1Locked` remain in the prefab now that the VM props are gone.
3. LOCALIZATION KEY CONSISTENCY. The `{=key}` prefixes in the display_name (taom_career_choices.xml) and rank_name (taom_careers.xml) attributes must EXACTLY match the `<string id=...>`/`{=...}` entries harvested into taom_module_strings.xml. Spot-check several. Also: are there any DUPLICATE `<string id>` entries introduced in taom_module_strings.xml? Is the file still well-formed? Are all 441 new keys unique?
4. ISTIERAVAILABLE REFACTOR EQUIVALENCE. Old: switch(tier){1:true; 2:>=10; 3:>=20; default:false}. New: `tier<1||tier>3 ? false : heroLevel>=GetTierUnlockLevel(tier)` with GetTierUnlockLevel 1/10/20. Confirm behavior is identical for all (heroLevel, tier) the game passes. Note GetTierUnlockLevel(1)=1 means IsTierAvailable(0,1)=false where the old code returned true at tier 1 regardless of level -- is heroLevel ever 0 or negative in practice (heroes start at level 1)? Confirm this is benign or flag.
5. OPTIONAL-PARAM CTOR BACK-COMPAT. CareerDefinition and CareerChoiceGroupDefinition gained trailing optional params (default ""). Confirm all existing call sites (CareerConfigProvider, tests) still compile/behave, and that the parser passes the new attrs. Confirm no positional-arg call site silently shifts.
6. IsUnavailable NOTIFICATION. CareerChoiceObjectVM.IsUnavailable is computed from _isTaken/_isFreeToTake; the setters call OnPropertyChanged(nameof(IsUnavailable)). The VMs are also fully rebuilt on RefreshValues. Confirm there is no stale-binding path where a pip shows the wrong state, and that OnPropertyChanged(string) is the correct v1.4.5 ViewModel API.
7. COMMENTED-BLOCK INJECTION. The 6 cave_troll_master `<ChoiceGroup>` display_name attrs were injected INSIDE a pre-existing `<!-- DISABLED ... -->` comment in taom_career_choices.xml. Confirm the file is still well-formed XML (the injected attrs are inside the comment, inert) and that this does not break parsing.

## REQUIRED SECTIONS in your output
- SPRITE ATLAS: paste the relevant TaleWorlds SpriteData/SpriteCategory deserialization code you inspected; state whether multi-sheet, mixed-size-per-category is valid in v1.4.5. Verdict on Suspect 1.
- BINDING TABLE: every prefab binding -> backing member -> CONNECTED/GAP.
- LOC CONSISTENCY: spot-check results + duplicate-id check + well-formedness.
- FINDINGS: numbered, each with file:line, severity (HIGH/MED/LOW), evidence, and fix. If none, say so explicitly per section.

## QUALITY GATES
- Decompile/inspect the actual installed v1.4.5 TaleWorlds DLLs for Suspect 1 (sprite atlas) -- do not guess. Path: E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/.
- Read the actual TAOM files before claiming a binding is missing -- grep, don't assume.
- Tests are green (2698 passed). A prior Claude deep-review found no HIGH; verify independently and say where you agree/disagree.

## PRIOR REVIEW LESSONS
SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections. Do NOT repeat these.

ERROR: The process "16140" not found.
ERROR: The process "73008" not found.
codex
I’ll review this as read-only and start with the requested source files plus the TaleWorlds sprite loading path, since Suspect 1 depends on engine behavior rather than TAOM assumptions.
  → Read requested docs and TAOM UI/data files
  • Inspect TaleWorlds sprite loading code for atlas validity
  • Build binding table against VM members
  • Verify localization keys, XML well-formedness, optional constructors, tier behavior, notifications
  • Report confirmed findings and disputes with evidence
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/UI/CareerScreenVM.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'docs/features/career-system.md'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'docs/reviews/tor-career-ui-comparison-2026-05-30.md'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 804ms:
using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceObjectVM : ViewModel
{
    private readonly CareerChoiceDefinition _choice;
    private readonly Func<string, bool> _selectChoice;
    private readonly Func<string, bool> _deselectChoice;
    private bool _isTaken;
    private bool _isFreeToTake;

    public CareerChoiceObjectVM(
        CareerChoiceDefinition choice,
        bool isTaken,
        bool isFreeToTake,
        Func<string, bool> selectChoice = null,
        Func<string, bool> deselectChoice = null)
    {
        _choice = choice;
        _isTaken = isTaken;
        _isFreeToTake = isFreeToTake && !isTaken;
        _selectChoice = selectChoice;
        _deselectChoice = deselectChoice;
    }

    public void SelectChoice()
    {
        if (_selectChoice != null && _selectChoice(_choice.Id))
            IsTaken = true;
    }

    public void DeSelectChoice()
    {
        if (_deselectChoice != null && _deselectChoice(_choice.Id))
            IsTaken = false;
    }

    [DataSourceProperty]
    public string Name => new TextObject(_choice.Id).ToString();

    [DataSourceProperty]
    public string Description => new TextObject(_choice.Description).ToString();

    [DataSourceProperty]
    public string IconSprite => _choice.IconSprite;

    [DataSourceProperty]
    public bool IsKeystone => _choice.Type == ChoiceType.Keystone;

    // Empty/locked pip state: shown dim when a slot is neither taken nor currently takeable.
    // The prefab renders three tinted copies of the point-pip gated on IsTaken / IsFreeToTake /
    // IsUnavailable so every slot always shows a pip (gold / brown / dim) instead of a blank gap.
    [DataSourceProperty]
    public bool IsUnavailable => !_isTaken && !_isFreeToTake;

    [DataSourceProperty]
    public bool IsTaken
    {
        get => _isTaken;
        set
        {
            if (_isTaken != value)
            {
                _isTaken = value;
                OnPropertyChangedWithValue(value, nameof(IsTaken));
                OnPropertyChanged(nameof(IsUnavailable));
            }
        }
    }

    [DataSourceProperty]
    public bool IsFreeToTake
    {
        get => _isFreeToTake;
        set
        {
            if (_isFreeToTake != value)
            {
                _isFreeToTake = value;
                OnPropertyChangedWithValue(value, nameof(IsFreeToTake));
                OnPropertyChanged(nameof(IsUnavailable));
            }
        }
    }

    public string ChoiceId => _choice.Id;
}


 succeeded in 805ms:
# Career System

**Status:** Verified in-game (2026-04-14). Career button with sprite on Character Developer screen, GameState-based screen opening (no crash), career selection in Character Creation. Gondor campaign tested.

**2026-05-04 update.** Ability activation rebuilt as a uniform 30-second cooldown timer. The original charge-based readiness model (DamageDone / Kills / DamageTaken accumulators) was replaced because per-archetype charge types produced confusing UX — defensive careers like Captain of Osgiliath only charged when the player took damage, so back-line players never saw the ability ready. See [Cooldown System](#cooldown-system) and `CHANGELOG.md` (issue #103).

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. 50 LOTR-themed careers across 16 factions, fully XML-driven. Each career has 31 choices (1 root + 6 groups x 5 choices) with keystones, passives, and ability mutations.

## Why This Exists

- **Vanilla behavior:** Bannerlord has perks but no career/class system with branching choice trees or active abilities
- **TAOM requirement:** Middle-earth factions need distinct playstyles beyond cultural feats — a Mordor Warboss should feel different from a Gondor Knight
- **Without this feature:** All heroes of the same culture play identically after initial perk selection

## Architecture

### Design Challenge

TOR_Core's career system uses hardcoded C# classes, static singletons, and 6 Harmony patches on ViewModel. TAOM needs XML-driven careers (add without recompilation), DryIoc injection, adapter pattern compliance, and UIExtenderEx integration.

### Solution Approach

- **Data model:** Plain C# classes (not PropertyObject) loaded from XML via `ICareerConfigProvider`
- **Persistence:** `CareerPersistenceBehavior` with `SyncData("_taom_careerData")` storing `Dictionary<string, HeroCareerData>`
- **Passive application:** `ICareerPassiveService` caches per-hero effect magnitudes, `CareerPassiveHelper` wires into 8 existing GameModels
- **Mutations:** Hybrid XML + C# calculator registry — XML defines target/params, C# provides calculator functions by ID
- **UI:** `GauntletCareerScreen` with `CareerScreenVM` hierarchy (TOR-pattern expandable panels, portraits, ability icons, lock chains), `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button with sprite. See [gui-sprite-system.md](gui-sprite-system.md) for full UI details.
- **Battle:** `CareerPerkMissionBehavior` for per-second cooldown tick + `V`-key activation handling. `CareerAbilityService` injects `ICareerConfigProvider` and forces `ChargeType.CooldownOnly` for all 50 careers — readiness is purely cooldown-timer based (see [Cooldown System](#cooldown-system)).
- **Ability effects:** `CareerAbilityEffectRegistry` dispatches to per-career `ICareerAbilityEffectExecutor` implementations. 3 role-based archetypes (Infantry/Ranged/Cavalry) serve all 50 careers with XML-tunable values via `taom_ability_tuning.xml`. All three archetypes apply AoE friendly-troop buffs within a 50-unit radius (standardized in templates): Infantry (damage + damage reduction), Ranged (speed + ranged damage + draw speed), Cavalry (mount speed + charge damage + damage). Buffs applied via `CareerAbilityBuffTracker` with separate hero and ally buff dictionaries (read by `TaomAgentStatCalculateModel` — survives stat recalc).

### Component Diagram

```
taom_careers.xml / taom_career_choices.xml
        |
  CareerConfigProvider (loads XML)
        |
  CareerRegistry (lookup/eligibility/tier gating)
        |
  ┌─────┴──────┐
  |            |
CareerDataService   CareerPassiveService (cache)
(per-hero CRUD)         |
  |            CareerPassiveHelper → 8 GameModels
  |
CareerCampaignBehavior  CareerPerkMissionBehavior
(session/level/death)   (battle tick/charge/ability effects)
  |                           |
CareerCreationHandler   CareerAbilityEffectRegistry
(CC integration)        → InfantryAbilityExecutor (AoE damage + reduction)
  |                     → RangedAbilityExecutor (speed + ranged dmg + draw)
  |                     → CavalryAbilityExecutor (mount speed + charge + dmg)
  |                     → CareerAbilityBuffTracker (hero + ally buffs, read by stat model)
CareerSwitchService     → MissionAbilityExecutionContext (boundary adapter)
(NPC dialogue switching)
  |
GauntletCareerScreen → CareerScreenVM → CareerChoiceGroupObjectVM → CareerChoiceObjectVM
                                      → CareerAbilityEffectVM (ability effects list)
```

## Configuration

### Career Definitions (`Main/_Module/ModuleData/career_system/taom_careers.xml`)

Defines careers with: id, display name, description, portrait sprite, ability template ID, eligible cultures, choice group IDs, root choice id, min clan tier. `max_perk_points` attribute on root element (default 30). (Pre-2026-05-04 the schema also had `charge_type` and `max_charge` — both removed; cooldown is global, not per-career.)

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3) and contains choices (Keystone or Passive). Choices can have PassiveEffect (type + magnitude + operation) and Mutations (target template + property + calculator + params).

**Two PassiveEffect schemas are accepted** (`CareerConfigProvider.ParseChoice`):
- **Direct (preferred):** `<Choice ...><PassiveEffect type="X" magnitude="0.10" operation="Add" is_percentage="true" /></Choice>`
- **Wrapped:** `<Choice ...><PassiveEffects><PassiveEffect type="X" value="0.10" /></PassiveEffects></Choice>` — the parser reads the *first* `<PassiveEffect>` inside the plural `<PassiveEffects>` wrapper (one child only — multi-child wrappers silently drop the rest), and accepts `value=` as an alias for `magnitude=` (`magnitude=` wins when both are present). The wrapped form was historically unparsed (310 dead choices across all 16 cultures); fixed 2026-05-29 — see [RCA](../reviews/rca-career-partysize-2026-05-29.md).

**Magnitude scale ↔ application method (IMPORTANT).** The cache (`CareerPassiveService`) stores only the summed `Magnitude` float and discards `Operation`/`IsPercentage`; each consuming GameModel chooses flat vs factor by calling `ApplyFlat` (`result.Add`) or `ApplyFactor` (`result.AddFactor`). So a passive's authored magnitude scale MUST match its consumer's method:
- **Fractional magnitude (0.10 = +10%)** → consumer uses `ApplyFactor`. This is the convention for almost every effect type (TroopWages, PartyMovementSpeed, TroopMorale, etc.).
- **Whole-count magnitude (2 = +2 units)** → consumer uses `ApplyFlat`. Only **`PartySize`** (`TaomPartySizeModel`) and the agent-stat flat types (`Health`, `CompanionLimit`) are flat. `PartySize` magnitudes are 2–6 (e.g. "+4 party size"). Applying a whole-count via `ApplyFactor` multiplies the base (`AddFactor(2)` = ×3) — this was the "+2 → +150" bug fixed 2026-05-29.
- **Deferred refactor:** make the cache `IsPercentage`-aware so flat-vs-factor is data-driven per entry rather than a per-call-site decision. Tracked as a known design-debt note.

**Dead-consumer effect types (known limitation).** Five `PassiveEffectType` values are authored in the XML but have **no consumer**, so choices advertising them are no-ops: `Ammo`, `HorseChargeDamage`, `HorseHealth`, `TroopResistance`, `StealthBonus`. Pre-existing; made more visible by the wrapper activation. Implementing consumers is a separate per-type balance/feature decision.

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines per-ability tunables: id, display name, duration (effect window), radius (AoE), max charge (used by mutation system to scale charge thresholds — internal value, not consumed by readiness logic), particle/sound effects, tooltip. Cooldown is *not* per-template; see [Cooldown System](#cooldown-system).

### Cooldown System

`Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` declares a single `<Global cooldown_seconds="30" />` element shared by all 50 careers. Edit to retune.

```xml
<AbilityTuning>
  <Global cooldown_seconds="30" />
  <Infantry .../>
  <Ranged .../>
  <Cavalry .../>
</AbilityTuning>
```

- **Default:** 30 seconds.
- **Validation:** Must be in `(0, 3600]`. Out-of-range, malformed, or missing values fall back to 30s with a `LogWarning` (`CareerConfigProvider.ParseGlobalTuning`).
- **Reload scope:** `CareerConfigProvider` is a `Reuse.Singleton` and caches the parsed config. Changes require a full Bannerlord application restart — not a save-load.
- **Per-career override:** Not supported. Readiness is uniform across all 50 careers by design (UX simplification — see #103 motivation).

In-battle UX:
- Abilities start ready at battle open.
- Pressing `V` while ready: yellow *"<Ability> activated!"* message + buff/sound/particle effect.
- Pressing `V` while on cooldown: throttled gray *"Career ability still charging — Ns remaining"* (one message per 2s).
- One-shot green *"Career ability is ready! Press V to activate"* when the cooldown elapses.

### Starting Equipment Override (per-archetype)

After the culture-default starting roster is applied at `OnCharacterCreationFinalize`, the player's career archetype drives a second roster application that overwrites the loadout. The archetype is one of three values:

| Archetype | Weapons | Armor |
|-----------|---------|-------|
| **Ranged** | bow + arrows + sword | light (low armor, very low weight) |
| **Cavalry** | spear + shield + sword + horse + harness | medium (chainmail) |
| **Infantry** | 1H + shield + (2H or spear — culture-decides) | heavy (plate-tier weight) |

**Single source of truth:** [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) maps each careerId to a `CareerArchetype`. The same dictionary is consumed by the ability executor registry (Infantry/Ranged/Cavalry executors) and by [`ICareerArchetypeService`](../../Main/Features/CareerSystem/ICareerArchetypeService.cs). Cached in a static field — one allocation per app lifetime.

**Roster ID convention:** `player_career_{cultureId}_{infantry|ranged|cavalry}_{f|m}`. Built by [`CareerEquipmentRosterIds.Build`](../../Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs), looked up via `MBObjectManager.GetObject<MBEquipmentRoster>`. Rosters are authored in [`Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml).

**Graceful fallback:** When no roster exists for a given (culture, archetype, gender) combination, [`CareerStartingEquipmentService`](../../Main/Features/CharacterCreation/CareerStartingEquipmentService.cs) logs a warning and leaves the already-applied culture default in place. This lets new cultures ship incrementally without code changes.

**Live preview during CC** ([`CareerMenuService.UpdateCareerEquipmentPreview`](../../Main/Features/CharacterCreation/CareerMenuService.cs)): when the player clicks a career option, two preview updates fire because two surfaces render the character from different sources:

1. **Career menu 3D agent** reads from the menu's `NarrativeMenuCharacter` buffer. Updated via `NarrativeMenuCharacter.SetEquipment(roster)` on the `player_career_character` — same pattern as `NarrativeMenuBuilder.UpdateYouthEquipment`.
2. **Review stage 3D agent** ([`CharacterCreationReviewStageView.AddCharacterEntity`](file:///E:/Decompiled_Bannerlord/Modules/SandBox.GauntletUI/SandBox.GauntletUI.CharacterCreation/CharacterCreationReviewStageView.cs)) reads from `Hero.MainHero.CharacterObject.Equipment` directly. Updated by running the same two-step apply chain that `OnCharacterCreationFinalize` does: `IPlayerEquipmentService.ApplyPlayerStartingEquipment` (resets to culture+title default) → `ICareerStartingEquipmentService.ApplyCareerStartingEquipment` (overlays career roster). This way switching careers (cavalry → ranged) starts from a clean culture-default slate rather than inheriting the previous career's overrides.

Same fallback policy as the runtime grant: missing roster → log + leave the youth/culture-default preview in place.

**Critical: `FillFrom` does NOT clear unspecified slots.** `Equipment.FillFrom(source)` copies only the slots that are present in the source roster — it does not zero-clear the target's other slots first. This means if your culture-default roster sets a Horse and your career roster does not mention Horse, the horse persists. For archetypes that should be on foot (ranged, infantry), include explicit empty overrides:

```xml
<Equipment slot="Horse" id="" />
<Equipment slot="HorseHarness" id="" />
```

The empty `id=""` resolves to a null `ItemObject`, which `Equipment.DeserializeNode` accepts as an empty slot.

### How to add a new culture's career rosters

1. Create starter armor items in LOTRLOME_Armory at `LOTRLOME_items/<culture>/starter_armors.xml` — 15 items total (3 archetypes × 5 slots: head/body/leg/cape/gloves). Reuse existing meshes; vary weight + armor stats per archetype (ranged ≈ 0.5× source weight, cavalry ≈ 0.75×, infantry ≈ 1.0×). Use the `starter_{archetype}_{culture}_{slot}_a` naming convention — see Gondor [`starter_armors.xml`](file:///E:/Steam/steamapps/common/Mount%20%26%20Blade%20II%20Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/starter_armors.xml) as the template.
2. **Required cover attributes** — LOTRLOME armor items render their mesh only when the `Armor` element declares it covers the slot:
   - Head items: `hair_cover_type="..."` + `beard_cover_type="..."` (cloth → `type1`/`type2`, plate → `type1`/`all`)
   - Body items: `covers_body="true"` (required) plus optionally `covers_legs="true"` for long robes / `covers_hands="true"` for full gauntlets that extend past the arm
   - **Leg items: `covers_legs="true"` is REQUIRED** — without it the leg mesh does not render, the player appears with bare legs even though the item is equipped
   - **Glove items: `covers_hands="true"` is REQUIRED** — same failure mode for hands
   - Cape items: no cover attribute needed
   - Source-of-truth: cross-check against any existing LOTRLOME `{leg,arm}_armors.xml` entries — every leg item has `covers_legs="true"` and every glove item has `covers_hands="true"`. Don't omit these on duplicates.
3. **Path encoding trap** — the LOTRLOME_Armory path on Windows contains `&` (`Mount & Blade II Bannerlord`). The Write tool has been observed entity-encoding `&` → `&amp;` and silently writing to a phantom directory. After authoring, `ls` the real path to confirm. See `feedback_write_tool_ampersand_path_encoding.md`.
4. Append 6 rosters to [`taom_career_starting_equipment.xml`](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) — one per (archetype, gender). Reference existing low-tier culture weapons + the new starter armor. **Don't forget the explicit Horse/HorseHarness clears for ranged + infantry** — `Equipment.FillFrom` is a slot-by-slot merge and will leave culture-default horses in place if you don't override.
5. Verify the archetype for each career in [`CareerSystemIoC.GetCareerArchetypeMap()`](../../Main/Features/CareerSystem/CareerSystemIoC.cs) — adjust if needed.
6. No code change required — `ICareerStartingEquipmentService` looks up by string-id at runtime.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CareerSystem/Domain/` (11 files) | Enums + immutable data classes |
| `Main/Features/CareerSystem/ICareerDataService.cs` | Per-hero career state CRUD |
| `Main/Features/CareerSystem/CareerConfigProvider.cs` | XML config loading |
| `Main/Features/CareerSystem/CareerRegistry.cs` | Career lookup, eligibility, tier gating |
| `Main/Features/CareerSystem/CareerPassiveService.cs` | Session-scoped passive effect cache |
| `Main/Features/CareerSystem/CareerPassiveHelper.cs` | Static helper wiring passives into GameModels |
| `Main/Features/CareerSystem/Mutations/` (6 files) | Calculator registry + built-in calculators + mutation service |
| `Main/Features/CareerSystem/Abilities/` (10 files) | CareerAbility, ability service, effect registry, 3 executors, buff tracker, execution context |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Battle tick + V-key activation + HUD lifecycle (302 LOC; refactor tracked in #102) |
| `Main/Features/CareerSystem/CareerCreationHandler.cs` | Character creation integration |
| `Main/Features/CareerSystem/CareerSwitchService.cs` | Career switching with validation |
| `Main/Features/CareerSystem/UI/` (7 files) | Career screen + VM hierarchy + UIExtenderEx mixin + ability HUD + prefab. See [gui-sprite-system.md](gui-sprite-system.md) |
| `Main/Features/CareerSystem/Models/` (3 files) | TaomAgentApplyDamageModel, TaomAgentStatCalculateModel, TaomClanTierModel |
| `Main/Adapters/ICareerHeroAdapter.cs` | Wraps Hero for service boundary |
| `Main/Adapters/ICareerHeroAdapterFactory.cs` | Factory for GameModel boundary |
| `Main/Features/CareerSystem/Domain/CareerArchetype.cs` | `enum CareerArchetype { Infantry, Ranged, Cavalry }` |
| `Main/Features/CareerSystem/CareerArchetypeService.cs` | careerId → archetype lookup; backed by static map in `CareerSystemIoC` |
| `Main/Features/CharacterCreation/CareerStartingEquipmentService.cs` | Applies archetype roster at end of CC over the culture default |
| `Main/Features/CharacterCreation/CareerEquipmentRosterIds.cs` | Roster ID builder: `player_career_{culture}_{archetype}_{f\|m}` |
| `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` | Per-(culture, archetype, gender) rosters; Gondor only as of 2026-05-19 |

## Dependencies

- DryIoc (IoC container)
- UIExtenderEx (UI injection)
- TaleWorlds.CampaignSystem (Hero, CampaignEvents, ExplainedNumber)
- TaleWorlds.MountAndBlade (MissionBehavior, Agent)
- TaleWorlds.Engine.GauntletUI (GauntletLayer, GlobalLayer)

## Tests

| Test File | Methods | Coverage |
|-----------|---------|----------|
| HeroCareerDataTests | 12 | Domain data class |
| CareerDataServiceTests | 17 | CRUD + persistence round-trip |
| CareerConfigProviderTests | 5 | XML parsing + missing file |
| CareerRegistryTests | 16 | Lookup + eligibility + tier gating |
| MutationCalculatorRegistryTests | 8 | All 5 built-in calculators |
| CareerPassiveServiceTests | 7 | Cache refresh + magnitude aggregation |
| MutationServiceTests | 5 | Template cloning + mutation application |
| CareerAbilityTests | 20 | Charge types + cooldown + activation + ReadyProgress01 |
| CareerAbilityServiceTests | 10 | Force-CooldownOnly + configured cooldown duration + GetCooldownRemaining (hero present/absent) + IsAbilityReady transitions |
| CareerCreationHandlerTests | 4 | CC flow + root choice |
| CareerSwitchServiceTests | 5 | Switch validation + choice reset |
| CareerScreenVMTests | 5 | VM state + choice selection |

## How-To

### Add a new career
1. Add `<Career>` element to `taom_careers.xml` with unique id, eligible cultures, choice groups
2. Add `<ChoiceGroup>` elements to `taom_career_choices.xml` (6 groups: 2 per tier, each with 1 keystone + 4 passives)
3. Add `<Choice id="xxx_root">` as the root choice
4. Add ability template to `taom_ability_templates.xml`
5. No C# changes required

### Add a new mutation calculator
1. Add function to `BuiltInCalculators.RegisterAll()`
2. Reference by id in XML `<Mutation calculator="your_id" ... />`

### Add a new PassiveEffectType
1. Add enum value to `PassiveEffectType.cs`
2. Add `CareerPassiveHelper.ApplyFactor/ApplyFlat` call in the relevant GameModel

### Retune the global ability cooldown
1. Edit `Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` `<Global cooldown_seconds="N" />` (must be in `(0, 3600]`)
2. Restart Bannerlord (provider caches via `Reuse.Singleton`; save-load is NOT enough)

### Add a new ability icon
See #101 — currently 41 of 50 careers have no PNG. Drop a 256x256 PNG into `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/<career_id>_ability.png` and add the corresponding `<Name>CareerSystem\Abilities\<career_id>_ability</Name>` registration in `Main/_Module/GUI/TAOMSpriteData.xml`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-balance.md](./battle-balance.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)
- [docs/reviews/rca-career-starting-equipment-2026-05-19.md](../reviews/rca-career-starting-equipment-2026-05-19.md)

<!-- backlinks-end -->


 succeeded in 815ms:
<Prefab>
  <Constants>
  </Constants>
  <VisualDefinitions>
    <VisualDefinition Name="BottomMenu" EaseIn="true" TransitionDuration="0.45">
      <VisualState PositionYOffset="6" State="Default" />
    </VisualDefinition>
    <VisualDefinition Name="TopPanel" EaseIn="true" TransitionDuration="0.45">
      <VisualState PositionYOffset="-6" State="Default" />
    </VisualDefinition>
    <VisualDefinition Name="ExtendablePanel" EaseIn="true" TransitionDuration="0.2">
      <VisualState SuggestedWidth="80" SuggestedHeight="220" State="Default" />
      <VisualState SuggestedWidth="750" SuggestedHeight="220" State="Hovered" />
    </VisualDefinition>
  </VisualDefinitions>
  <Window>
    <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent">
      <Children>

        <Standard.Background />

        <!--Top Panel - Title-->
        <Widget VisualDefinition="TopPanel" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedWidth="755" SuggestedHeight="182" HorizontalAlignment="Center" PositionYOffset="-182" Sprite="StdAssets\tabbar_standart" ValueFactor="-30">
          <Children>
            <RichTextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="18" Brush="Quest.CenterPanel.Title.Text" Brush.FontSize="45" Text="@ScreenTitle" />
          </Children>
        </Widget>

        <!--Middle Area-->
        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="920" HorizontalAlignment="Center" VerticalAlignment="Center" MarginTop="60">
          <Children>

            <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" StackLayout.LayoutMethod="HorizontalLeftToRight">
              <Children>

                <!-- LEFT PANEL: Career Info -->
                <Widget WidthSizePolicy="Fixed" SuggestedWidth="500" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" Sprite="StdAssets\Popup\canvas">
                  <Children>
                    <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                      <Children>
                        <!-- Career Name -->
                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="400" HorizontalAlignment="Center" Text="@CareerName" MarginTop="15" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />

                        <!-- Career Portrait -->
                        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="200" SuggestedWidth="400" HorizontalAlignment="Center" MarginTop="15" Sprite="@CareerPortraitSprite" IsVisible="@HasCareer">
                          <Children>
                            <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                          </Children>
                        </Widget>

                        <!-- Career Description -->
                        <ScrollablePanel WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="180" MarginLeft="20" MarginRight="20" MarginTop="20" ClipRect="CareerDescriptionClip" InnerPanel="CareerDescriptionInner" AutoHideScrollBars="true">
                          <Children>
                            <Widget Id="CareerDescriptionClip" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" ClipContents="true">
                              <Children>
                                <Widget Id="CareerDescriptionInner" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren">
                                  <Children>
                                    <RichTextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" Text="@CareerDescription" Brush="CharacterDeveloper.DescriptionText" Brush.FontSize="18" />
                                  </Children>
                                </Widget>
                              </Children>
                            </Widget>
                          </Children>
                        </ScrollablePanel>

                        <!-- Divider -->
                        <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="4" HorizontalAlignment="Center" Sprite="Clan\divider_mid" />

                        <!-- Ability Section -->
                        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="10" HorizontalAlignment="Center" Text="@AbilityLabel" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />

                        <!-- Ability Icon -->
                        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="120" SuggestedWidth="120" HorizontalAlignment="Center" MarginTop="15" Sprite="@AbilitySpriteName" IsVisible="@HasAbilitySprite">
                          <Children>
                            <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                          </Children>
                        </Widget>

                        <!-- Ability Name -->
                        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="10" HorizontalAlignment="Center" Text="@AbilityName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />

                        <!-- Ability Effects List -->
                        <ListPanel DataSource="{AbilityEffects}" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" MarginTop="10" MarginLeft="20" MarginRight="20" StackLayout.LayoutMethod="VerticalTopToBottom">
                          <ItemTemplate>
                            <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" Text="@LineText" Brush="CharacterDeveloper.DescriptionText" Brush.FontSize="18" MarginTop="3" />
                          </ItemTemplate>
                        </ListPanel>
                      </Children>
                    </ListPanel>
                    <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                  </Children>
                </Widget>

                <!-- RIGHT PANEL: Choice Groups (Tier 3 top -> Tier 1 bottom) -->
                <Widget WidthSizePolicy="Fixed" SuggestedWidth="1420" HeightSizePolicy="StretchToParent" HorizontalAlignment="Right">
                  <Children>

                    <ListPanel WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" StackLayout.LayoutMethod="VerticalTopToBottom">
                      <Children>

                        <!-- TIER 3 (top) -->
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="300" HorizontalAlignment="Center" VerticalAlignment="Center">
                          <Children>
                            <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="4" HorizontalAlignment="Center" Sprite="Clan\divider_mid" />
                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="200" Text="@Tier3Label" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />
                            <ListPanel DataSource="{ChoiceGroupsTier3}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" StackLayout.LayoutMethod="HorizontalCentered">
                              <ItemTemplate>
                                <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="HorizontalLeftToRight" MarginTop="35" MarginLeft="40" MarginRight="40">
                                  <Children>
                                    <ImageWidget VisualDefinition="ExtendablePanel" DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">
                                      <Children>
                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />
                                        <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" VerticalAlignment="Center" MarginTop="10" StackLayout.LayoutMethod="HorizontalLeftToRight">
                                          <Children>
                                            <!-- Pip strip (always visible): one pip per choice, tinted by state -->
                                            <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" StackLayout.LayoutMethod="VerticalTopToBottom">
                                              <ItemTemplate>
                                                <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="44" MarginLeft="20">
                                                  <Children>
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFFF" IsVisible="@IsTaken" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFF45" IsVisible="@IsUnavailable" />
                                                  </Children>
                                                </Widget>
                                              </ItemTemplate>
                                            </ListPanel>
                                            <!-- Description column (revealed on hover) -->
                                            <Widget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">
                                              <Children>
                                                <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                                  <ItemTemplate>
                                                    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="500">
                                                      <Children>
                                                        <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" />
                                                      </Children>
                                                    </Widget>
                                                  </ItemTemplate>
                                                </ListPanel>
                                              </Children>
                                            </Widget>
                                          </Children>
                                        </ListPanel>
                                        <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                                      </Children>
                                    </ImageWidget>
                                    <!-- +/- buttons -->
                                    <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">
                                      <Children>
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickIncrease" />
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickDecrease" />
                                      </Children>
                                    </ListPanel>
                                  </Children>
                                </ListPanel>
                              </ItemTemplate>
                            </ListPanel>
                            <!-- Locked: "Requires Level N" label -->
                            <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Center" Text="@Tier3RequirementText" IsVisible="@Tier3Locked" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="28" />
                          </Children>
                        </Widget>

                        <!-- TIER 2 (middle) -->
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="300" HorizontalAlignment="Center" VerticalAlignment="Center">
                          <Children>
                            <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="4" HorizontalAlignment="Center" Sprite="Clan\divider_mid" />
                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="200" Text="@Tier2Label" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />
                            <ListPanel DataSource="{ChoiceGroupsTier2}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" StackLayout.LayoutMethod="HorizontalCentered">
                              <ItemTemplate>
                                <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="HorizontalLeftToRight" MarginTop="35" MarginLeft="40" MarginRight="40">
                                  <Children>
                                    <ImageWidget VisualDefinition="ExtendablePanel" DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">
                                      <Children>
                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />
                                        <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" VerticalAlignment="Center" MarginTop="10" StackLayout.LayoutMethod="HorizontalLeftToRight">
                                          <Children>
                                            <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" StackLayout.LayoutMethod="VerticalTopToBottom">
                                              <ItemTemplate>
                                                <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="44" MarginLeft="20">
                                                  <Children>
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFFF" IsVisible="@IsTaken" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFF45" IsVisible="@IsUnavailable" />
                                                  </Children>
                                                </Widget>
                                              </ItemTemplate>
                                            </ListPanel>
                                            <Widget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">
                                              <Children>
                                                <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                                  <ItemTemplate>
                                                    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="500">
                                                      <Children>
                                                        <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" />
                                                      </Children>
                                                    </Widget>
                                                  </ItemTemplate>
                                                </ListPanel>
                                              </Children>
                                            </Widget>
                                          </Children>
                                        </ListPanel>
                                        <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                                      </Children>
                                    </ImageWidget>
                                    <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">
                                      <Children>
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickIncrease" />
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickDecrease" />
                                      </Children>
                                    </ListPanel>
                                  </Children>
                                </ListPanel>
                              </ItemTemplate>
                            </ListPanel>
                            <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Center" Text="@Tier2RequirementText" IsVisible="@Tier2Locked" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="28" />
                          </Children>
                        </Widget>

                        <!-- TIER 1 (bottom) -->
                        <Widget WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="300" HorizontalAlignment="Center" VerticalAlignment="Center">
                          <Children>
                            <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed" SuggestedHeight="4" HorizontalAlignment="Center" Sprite="Clan\divider_mid" />
                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="200" Text="@Tier1Label" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />
                            <ListPanel DataSource="{ChoiceGroupsTier1}" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" StackLayout.LayoutMethod="HorizontalCentered">
                              <ItemTemplate>
                                <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="HorizontalLeftToRight" MarginTop="35" MarginLeft="40" MarginRight="40">
                                  <Children>
                                    <ImageWidget VisualDefinition="ExtendablePanel" DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">
                                      <Children>
                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />
                                        <ListPanel WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" VerticalAlignment="Center" MarginTop="10" StackLayout.LayoutMethod="HorizontalLeftToRight">
                                          <Children>
                                            <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" StackLayout.LayoutMethod="VerticalTopToBottom">
                                              <ItemTemplate>
                                                <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="44" MarginLeft="20">
                                                  <Children>
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFFF" IsVisible="@IsTaken" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />
                                                    <Widget DoNotAcceptEvents="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" Color="#FFFFFF45" IsVisible="@IsUnavailable" />
                                                  </Children>
                                                </Widget>
                                              </ItemTemplate>
                                            </ListPanel>
                                            <Widget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">
                                              <Children>
                                                <ListPanel DataSource="{Choices}" WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom">
                                                  <ItemTemplate>
                                                    <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="40" SuggestedWidth="500">
                                                      <Children>
                                                        <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" />
                                                      </Children>
                                                    </Widget>
                                                  </ItemTemplate>
                                                </ListPanel>
                                              </Children>
                                            </Widget>
                                          </Children>
                                        </ListPanel>
                                        <BrushWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" Brush="Popup.Frame" IsEnabled="false" />
                                      </Children>
                                    </ImageWidget>
                                    <ListPanel HeightSizePolicy="CoverChildren" WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">
                                      <Children>
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickIncrease" />
                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" MarginLeft="10" Command.Click="ExecuteClickDecrease" />
                                      </Children>
                                    </ListPanel>
                                  </Children>
                                </ListPanel>
                              </ItemTemplate>
                            </ListPanel>
                          </Children>
                        </Widget>

                      </Children>
                    </ListPanel>

                    <!-- Free Points Display -->
                    <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Right" VerticalAlignment="Bottom" Text="@FreeCareerPointsText" MarginRight="100" />

                  </Children>
                </Widget>

              </Children>
            </ListPanel>

          </Children>
        </Widget>

        <!--Close Screen Button-->
        <Standard.DialogCloseButtons VisualDefinition="BottomMenu" HorizontalAlignment="Center" VerticalAlignment="Bottom" PositionYOffset="100" Parameter.DoneButtonAction="ExecuteClose" Parameter.DoneButtonText="@DoneLbl" Parameter.ShowCancel="false" />

      </Children>
    </Widget>
  </Window>
</Prefab>


 succeeded in 836ms:
using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceGroupObjectVM : ViewModel
{
    private readonly CareerChoiceGroupDefinition _group;
    private readonly Action _choiceChangedAction;
    private bool _isExpanded;
    private bool _isLocked;
    private bool _isActive;
    private bool _buttonsVisible;
    private MBBindingList<CareerChoiceObjectVM> _choices;

    public CareerChoiceGroupObjectVM(CareerChoiceGroupDefinition group, bool isLocked, Action choiceChangedAction = null)
    {
        _group = group;
        _isLocked = isLocked;
        _isActive = !isLocked;
        _buttonsVisible = false;
        _choiceChangedAction = choiceChangedAction;
        _choices = new MBBindingList<CareerChoiceObjectVM>();
    }

    public void ExecuteBeginHover()
    {
        ButtonsVisible = true;
    }

    public void ExecuteEndHover()
    {
        ButtonsVisible = false;
    }

    public void ExecuteClickIncrease()
    {
        if (!_isActive) return;

        for (int i = 0; i < _choices.Count; i++)
        {
            if (!_choices[i].IsTaken)
            {
                _choices[i].SelectChoice();
                _choiceChangedAction?.Invoke();
                return;
            }
        }
    }

    public void ExecuteClickDecrease()
    {
        if (!_isActive) return;

        for (int i = _choices.Count - 1; i >= 0; i--)
        {
            if (_choices[i].IsTaken)
            {
                _choices[i].DeSelectChoice();
                _choiceChangedAction?.Invoke();
                return;
            }
        }
    }

    [DataSourceProperty]
    public string GroupName => !string.IsNullOrEmpty(_group.DisplayName)
        ? new TextObject(_group.DisplayName).ToString()
        : HumanizeId(_group.Id);

    // Fallback when a group has no authored display_name yet: turn "ranger_of_ithilien_t1_a"
    // into "Path A" (or title-cased words if the id doesn't carry a "_t<N>_<letter>" suffix),
    // so headers are never raw ids even before lore names are authored.
    private static string HumanizeId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var parts = id.Split('_');
        var last = parts[parts.Length - 1];
        if (parts.Length >= 2 && last.Length == 1 && char.IsLetter(last[0]))
        {
            var tierTok = parts[parts.Length - 2];
            if (tierTok.Length >= 2 && tierTok[0] == 't' && char.IsDigit(tierTok[1]))
                return "Path " + char.ToUpperInvariant(last[0]);
        }
        return string.Join(" ", System.Array.ConvertAll(parts, p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }

    [DataSourceProperty]
    public int Tier => _group.Tier;

    [DataSourceProperty]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
            }
        }
    }

    [DataSourceProperty]
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (_isLocked != value)
            {
                _isLocked = value;
                OnPropertyChangedWithValue(value, nameof(IsLocked));
            }
        }
    }

    [DataSourceProperty]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChangedWithValue(value, nameof(IsActive));
            }
        }
    }

    [DataSourceProperty]
    public bool ButtonsVisible
    {
        get => _buttonsVisible;
        set
        {
            if (_buttonsVisible != value)
            {
                _buttonsVisible = value;
                OnPropertyChangedWithValue(value, nameof(ButtonsVisible));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceObjectVM> Choices
    {
        get => _choices;
        set
        {
            if (_choices != value)
            {
                _choices = value;
                OnPropertyChangedWithValue(value, nameof(Choices));
            }
        }
    }

    public void ExecuteToggleExpand()
    {
        if (!_isLocked)
            IsExpanded = !IsExpanded;
    }

    public string GroupId => _group.Id;
}


 succeeded in 849ms:
# TOR_Core Career UI — reference review & comparison (2026-05-30)

Reference review of [TheOldRealms/TOR_Core](https://github.com/TheOldRealms/TOR_Core) (the Warhammer "Old Realms" total-conversion) career-screen UI, compared against TAOM's career screen after the 2026-05-30 revamp. Read-only review of a blobless shallow clone (since deleted); no TOR build/scripts were run.

## License / attribution

TOR_Core is **GPL v3**. TAOM's career system shares TOR's exact file structure (`CareerScreenVM.cs`, `CareerChoiceGroupObjectVM.cs`, `CareerChoiceObjectVM.cs`, `GUI/Prefabs/CareerSystem/CareerScreen.xml`) — it was originally **ported from TOR**. The TOR team granted TAOM explicit permission to reuse their code, and the repo is public. **Attribution:** TAOM's CareerSystem UI derives from TOR_Core (GPLv3), used with permission. The per-tier rank-title convention (`tor_career_rank{1,2,3}_name`) was adopted here; TAOM's names are authored fresh for Middle-earth.

## How TOR builds the Career UI (verified from source)

- **Screen stack:** `CareerScreenGameState → CareerScreen (ScreenBase) → CareerScreenVM → CareerObjectVM (CurrentCareer) → 3 tier lists → CareerChoiceGroupObjectVM → CareerChoiceObjectVM`. `CareerScreenVM` is thin; all screen logic lives in the `CareerObjectVM` "CurrentCareer" sub-layer.
- **Prefab layout:** split pane — left 500px (name, illustration, description, ability icon + effect lines), right 1420px (three tier rows). Tier rows stack `VerticalBottomToTop`, so **Tier 1 = bottom, Tier 3 = top**. Per-tier `VerticalAlignment` Bottom/Center/Top.
- **Each node:** `ExtendablePanel` 80px → 750px on hover; vertical pip strip (`@IconSprite` tinted brown `#7f695c` if `IsFreeToTake`, gold `#dfc395` if `IsTaken`) + per-choice descriptions clipped at 80px and revealed when the panel expands on hover; `+`/`−` buttons gated on `@ButtonsVisible` (hover) and `@IsActive`.
- **Naming (3 layers):** per-tier **rank name** (`tor_career_rank1/2/3_name` per career — e.g. *Knight Errant / Questing Knight / Grail Knight*) + **condition** + **unlock** text per tier (`GetConditionText`/`GetUnlockText` → "Required renown: N"); plus a per-node **GroupName** (*Monster Slayer*, *Master Horseman*…).
- **Gating:** `group.IsActiveForHero(hero)` — condition-based (renown etc.); a `locked_chains` sprite overlays locked tiers (`@TierNActive`).
- **Free points:** `Min(MaxPerkPoints, Hero.Level) − (CareerChoices.Count − 1)`.
- **Pip availability:** `CareerChoiceObjectVM.IsFreeToTake = !IsTaken` — **not** gated on free points (every untaken slot shows a brown pip). VMs read `Hero.MainHero` directly.

## Side-by-side

| Aspect | TOR | TAOM (post-revamp) |
|---|---|---|
| Tier order | T1 bottom → T3 top (`VerticalBottomToTop`) | T1 bottom → T3 top (reordered blocks) — same result |
| Screen VM | thin → `CareerObjectVM` sub-layer | flattened into `CareerScreenVM` |
| Locked-tier visual | `locked_chains` overlay | **"Requires Level N"** label |
| Tier header naming | per-career **rank name** + condition + unlock | per-career **rank name** (adopted) + "Requires Level N" |
| Per-node naming | GroupName (Warhammer) | GroupName — **294 web-researched LOTR lore names** |
| Gating basis | renown/condition (`IsActiveForHero`) | level (T1 / 10 / 20) |
| Pip states | 2 (gold taken / brown untaken) → **blank at 0 pts** | **3** (gold / brown affordable / **dim unavailable**) |
| Coupling | reads `Hero.MainHero` in VMs | service + adapter, unit-tested (ADR-007) |

## Where TAOM is ahead of TOR

1. **No blank-node bug.** TOR's pips vanish when `FreeCareerPoints = 0` (only looked fine in screenshots that had spare points). TAOM's `IsUnavailable` dim-pip keeps the strip always readable.
2. **Decoupled + tested.** TAOM's registry/service/adapter split is unit-tested; TOR's VMs hardwire `Hero.MainHero`.
3. **Cleaner locked state.** "Requires Level N" label vs. a chains overlay.
4. **Lore-name depth.** 294 sourced group names + 147 sourced rank titles, each with an `attested` flag.

## Adopted this pass (clean-room, LOTR-authored)

- **Per-tier rank names** — `CareerDefinition.Rank{1,2,3}Name` + `rank{1,2,3}_name` XML attrs + VM tier-header binding (fallback to "Tier N"). 147 web-researched Tolkien-grounded titles applied to all 49 careers (`tools/career_rank_names.json`, `tools/apply_career_rank_names.py`). This is the feature that gives the reference its readable "Knight Errant → Grail Knight" progression.

## Deliberately NOT adopted

- TOR's `CareerObjectVM` sub-layer (ours is flatter, tested, equivalent UX) — needless churn.
- Renown/condition gating + chains overlay — TAOM uses level gating + "Requires Level N" text by design.
- Battle-Prayers button — Warhammer-specific.
- TOR's 2-state pip + `Hero.MainHero` coupling — TAOM's 3-state + service split is strictly better.


 succeeded in 864ms:
using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerScreenVM : ViewModel
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerConfigProvider _configProvider;
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;
    private readonly Action _onClose;

    private string _screenTitle;
    private string _doneLbl;
    private string _careerName;
    private string _careerDescription;
    private string _careerPortraitSprite;
    private string _abilityName;
    private string _abilitySpriteName;
    private string _abilityLabel;
    private string _freeCareerPointsText;
    private string _tier1Label;
    private string _tier2Label;
    private string _tier3Label;
    private bool _tier2Locked;
    private bool _tier3Locked;
    private string _tier2RequirementText;
    private string _tier3RequirementText;
    private int _freeCareerPoints;
    private bool _hasCareer;
    private bool _hasAbilitySprite;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier1;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier2;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier3;
    private MBBindingList<CareerAbilityEffectVM> _abilityEffects;

    public CareerScreenVM(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        ICareerConfigProvider configProvider,
        IModLogger logger,
        string heroStringId,
        int heroLevel,
        Action onClose)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _configProvider = configProvider;
        _logger = logger;
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
        _onClose = onClose;

        _screenTitle = new TextObject("{=taom_career_screen_title}Career").ToString();
        _doneLbl = new TextObject("{=taom_career_done}Done").ToString();
        _abilityLabel = new TextObject("{=taom_career_ability_label}Career Ability").ToString();
        _tier1Label = new TextObject("{=taom_career_tier1}Tier 1").ToString();
        _tier2Label = new TextObject("{=taom_career_tier2}Tier 2").ToString();
        _tier3Label = new TextObject("{=taom_career_tier3}Tier 3").ToString();

        _choiceGroupsTier1 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier2 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier3 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _abilityEffects = new MBBindingList<CareerAbilityEffectVM>();

        RefreshValues();
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        var careerId = _dataService.GetCareerStringId(_heroStringId);
        if (string.IsNullOrEmpty(careerId))
        {
            HasCareer = false;
            return;
        }

        var career = _registry.GetCareer(careerId);
        if (career == null)
        {
            HasCareer = false;
            return;
        }

        HasCareer = true;
        CareerName = new TextObject(career.DisplayName).ToString();
        CareerDescription = new TextObject(career.Description).ToString();
        CareerPortraitSprite = $"CareerSystem\\Portraits\\{career.PortraitSprite}";

        // Tier headers show the career's per-tier RANK title (e.g. "Captain of Ithilien") when
        // authored, falling back to the generic "Tier N" label. (Adopted from TOR_Core.)
        Tier1Label = !string.IsNullOrEmpty(career.Rank1Name)
            ? new TextObject(career.Rank1Name).ToString()
            : new TextObject("{=taom_career_tier1}Tier 1").ToString();
        Tier2Label = !string.IsNullOrEmpty(career.Rank2Name)
            ? new TextObject(career.Rank2Name).ToString()
            : new TextObject("{=taom_career_tier2}Tier 2").ToString();
        Tier3Label = !string.IsNullOrEmpty(career.Rank3Name)
            ? new TextObject(career.Rank3Name).ToString()
            : new TextObject("{=taom_career_tier3}Tier 3").ToString();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        AbilityName = abilityTemplate != null
            ? new TextObject(abilityTemplate.DisplayName).ToString()
            : new TextObject(career.AbilityTemplateId).ToString();
        AbilitySpriteName = $"CareerSystem\\Abilities\\{career.AbilityTemplateId}";
        HasAbilitySprite = !string.IsNullOrEmpty(career.AbilityTemplateId);

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        var currentChoices = _dataService.GetChoiceCount(_heroStringId);
        FreeCareerPoints = System.Math.Max(0, maxChoices - currentChoices);
        FreeCareerPointsText = new TextObject("{=taom_career_free_points}Free Points: {COUNT}")
            .SetTextVariable("COUNT", FreeCareerPoints).ToString();

        Tier2Locked = !_registry.IsTierAvailable(_heroLevel, 2);
        Tier3Locked = !_registry.IsTierAvailable(_heroLevel, 3);

        // Locked tiers show a "Requires Level N" label (level sourced from the registry, the single
        // source of truth) in place of the old stretched gate art.
        Tier2RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(2)).ToString();
        Tier3RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(3)).ToString();

        RebuildAbilityEffects(career);
        RebuildChoiceGroups(career);
    }

    private void RebuildAbilityEffects(CareerDefinition career)
    {
        _abilityEffects.Clear();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        if (abilityTemplate != null && !string.IsNullOrEmpty(abilityTemplate.TooltipDescription))
        {
            var description = new TextObject(abilityTemplate.TooltipDescription).ToString();
            _abilityEffects.Add(new CareerAbilityEffectVM(description));
        }
    }

    private void RebuildChoiceGroups(CareerDefinition career)
    {
        _choiceGroupsTier1.Clear();
        _choiceGroupsTier2.Clear();
        _choiceGroupsTier3.Clear();

        foreach (var groupId in career.ChoiceGroupIds)
        {
            var group = _registry.GetGroup(groupId);
            if (group == null) continue;

            var isLocked = !_registry.IsTierAvailable(_heroLevel, group.Tier);
            var groupVM = new CareerChoiceGroupObjectVM(group, isLocked, () => RefreshValues());

            var choices = _registry.GetChoicesForGroup(groupId);
            foreach (var choice in choices)
            {
                var isTaken = _dataService.GetOrCreateData(_heroStringId).HasChoice(choice.Id);
                var isFreeToTake = FreeCareerPoints > 0 && !isLocked;
                groupVM.Choices.Add(new CareerChoiceObjectVM(choice, isTaken, isFreeToTake, TrySelectChoice, TryDeselectChoice));
            }

            switch (group.Tier)
            {
                case 1: _choiceGroupsTier1.Add(groupVM); break;
                case 2: _choiceGroupsTier2.Add(groupVM); break;
                case 3: _choiceGroupsTier3.Add(groupVM); break;
            }
        }
    }

    public void ExecuteSelectChoice(string choiceId)
    {
        _logger?.LogInfo($"CareerSystem: ExecuteSelectChoice — choiceId='{choiceId}' freePoints={FreeCareerPoints}");
        if (FreeCareerPoints <= 0) return;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))
                return;

            if (choice.Type == Domain.ChoiceType.Keystone && group != null)
            {
                var heroData = _dataService.GetOrCreateData(_heroStringId);
                var careerId = _dataService.GetCareerStringId(_heroStringId);
                var career = careerId != null ? _registry.GetCareer(careerId) : null;
                if (career != null)
                {
                    foreach (var gId in career.ChoiceGroupIds)
                    {
                        var otherGroup = _registry.GetGroup(gId);
                        if (otherGroup == null || otherGroup.Tier != group.Tier) continue;
                        var otherChoices = _registry.GetChoicesForGroup(gId);
                        foreach (var oc in otherChoices)
                        {
                            if (oc.Type == Domain.ChoiceType.Keystone && heroData.HasChoice(oc.Id))
                                return;
                        }
                    }
                }
            }
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
        }
    }

    // Invoked by CareerChoiceObjectVM.SelectChoice via callback (+ button in UI)
    private bool TrySelectChoice(string choiceId)
    {
        if (FreeCareerPoints <= 0) return false;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return false;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))
                return false;

            // Enforce one Keystone per tier
            if (choice.Type == Domain.ChoiceType.Keystone && group != null)
            {
                var heroData = _dataService.GetOrCreateData(_heroStringId);
                var careerId = _dataService.GetCareerStringId(_heroStringId);
                var career = careerId != null ? _registry.GetCareer(careerId) : null;
                if (career != null)
                {
                    foreach (var gId in career.ChoiceGroupIds)
                    {
                        var otherGroup = _registry.GetGroup(gId);
                        if (otherGroup == null || otherGroup.Tier != group.Tier) continue;
                        foreach (var oc in _registry.GetChoicesForGroup(gId))
                        {
                            if (oc.Type == Domain.ChoiceType.Keystone && heroData.HasChoice(oc.Id))
                                return false;
                        }
                    }
                }
            }
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
            return true;
        }
        return false;
    }

    // Invoked by CareerChoiceObjectVM.DeSelectChoice via callback (- button in UI)
    private bool TryDeselectChoice(string choiceId)
    {
        var heroData = _dataService.GetOrCreateData(_heroStringId);
        if (!heroData.HasChoice(choiceId)) return false;

        _dataService.RemoveChoice(_heroStringId, choiceId);
        _passiveService.RefreshCache(_dataService, _registry);
        RefreshValues();
        return true;
    }

    public void ExecuteClose()
    {
        _onClose?.Invoke();
    }

    // ── DataSource Properties ──

    [DataSourceProperty]
    public string ScreenTitle
    {
        get => _screenTitle;
        set { if (_screenTitle != value) { _screenTitle = value; OnPropertyChangedWithValue(value, nameof(ScreenTitle)); } }
    }

    [DataSourceProperty]
    public string DoneLbl
    {
        get => _doneLbl;
        set { if (_doneLbl != value) { _doneLbl = value; OnPropertyChangedWithValue(value, nameof(DoneLbl)); } }
    }

    [DataSourceProperty]
    public bool HasCareer
    {
        get => _hasCareer;
        set { if (_hasCareer != value) { _hasCareer = value; OnPropertyChangedWithValue(value, nameof(HasCareer)); } }
    }

    [DataSourceProperty]
    public string CareerName
    {
        get => _careerName;
        set { if (_careerName != value) { _careerName = value; OnPropertyChangedWithValue(value, nameof(CareerName)); } }
    }

    [DataSourceProperty]
    public string CareerDescription
    {
        get => _careerDescription;
        set { if (_careerDescription != value) { _careerDescription = value; OnPropertyChangedWithValue(value, nameof(CareerDescription)); } }
    }

    [DataSourceProperty]
    public string CareerPortraitSprite
    {
        get => _careerPortraitSprite;
        set { if (_careerPortraitSprite != value) { _careerPortraitSprite = value; OnPropertyChangedWithValue(value, nameof(CareerPortraitSprite)); } }
    }

    [DataSourceProperty]
    public string AbilityName
    {
        get => _abilityName;
        set { if (_abilityName != value) { _abilityName = value; OnPropertyChangedWithValue(value, nameof(AbilityName)); } }
    }

    [DataSourceProperty]
    public string AbilitySpriteName
    {
        get => _abilitySpriteName;
        set { if (_abilitySpriteName != value) { _abilitySpriteName = value; OnPropertyChangedWithValue(value, nameof(AbilitySpriteName)); } }
    }

    [DataSourceProperty]
    public string AbilityLabel
    {
        get => _abilityLabel;
        set { if (_abilityLabel != value) { _abilityLabel = value; OnPropertyChangedWithValue(value, nameof(AbilityLabel)); } }
    }

    [DataSourceProperty]
    public bool HasAbilitySprite
    {
        get => _hasAbilitySprite;
        set { if (_hasAbilitySprite != value) { _hasAbilitySprite = value; OnPropertyChangedWithValue(value, nameof(HasAbilitySprite)); } }
    }

    [DataSourceProperty]
    public int FreeCareerPoints
    {
        get => _freeCareerPoints;
        set { if (_freeCareerPoints != value) { _freeCareerPoints = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPoints)); } }
    }

    [DataSourceProperty]
    public string FreeCareerPointsText
    {
        get => _freeCareerPointsText;
        set { if (_freeCareerPointsText != value) { _freeCareerPointsText = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPointsText)); } }
    }

    [DataSourceProperty]
    public string Tier1Label
    {
        get => _tier1Label;
        set { if (_tier1Label != value) { _tier1Label = value; OnPropertyChangedWithValue(value, nameof(Tier1Label)); } }
    }

    [DataSourceProperty]
    public string Tier2Label
    {
        get => _tier2Label;
        set { if (_tier2Label != value) { _tier2Label = value; OnPropertyChangedWithValue(value, nameof(Tier2Label)); } }
    }

    [DataSourceProperty]
    public string Tier3Label
    {
        get => _tier3Label;
        set { if (_tier3Label != value) { _tier3Label = value; OnPropertyChangedWithValue(value, nameof(Tier3Label)); } }
    }

    [DataSourceProperty]
    public bool Tier2Locked
    {
        get => _tier2Locked;
        set { if (_tier2Locked != value) { _tier2Locked = value; OnPropertyChangedWithValue(value, nameof(Tier2Locked)); } }
    }

    [DataSourceProperty]
    public bool Tier3Locked
    {
        get => _tier3Locked;
        set { if (_tier3Locked != value) { _tier3Locked = value; OnPropertyChangedWithValue(value, nameof(Tier3Locked)); } }
    }

    [DataSourceProperty]
    public string Tier2RequirementText
    {
        get => _tier2RequirementText;
        set { if (_tier2RequirementText != value) { _tier2RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier2RequirementText)); } }
    }

    [DataSourceProperty]
    public string Tier3RequirementText
    {
        get => _tier3RequirementText;
        set { if (_tier3RequirementText != value) { _tier3RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier3RequirementText)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier1
    {
        get => _choiceGroupsTier1;
        set { if (_choiceGroupsTier1 != value) { _choiceGroupsTier1 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier1)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier2
    {
        get => _choiceGroupsTier2;
        set { if (_choiceGroupsTier2 != value) { _choiceGroupsTier2 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier2)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier3
    {
        get => _choiceGroupsTier3;
        set { if (_choiceGroupsTier3 != value) { _choiceGroupsTier3 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier3)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerAbilityEffectVM> AbilityEffects
    {
        get => _abilityEffects;
        set { if (_abilityEffects != value) { _abilityEffects = value; OnPropertyChangedWithValue(value, nameof(AbilityEffects)); } }
    }
}


codex
The prefab does not show the removed gate names in the initial read; next I’m going through the sprite loader path and extracting all bindings mechanically so the table is driven by the actual XML rather than visual scanning.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"class SpriteData|class SpriteCategory|class SpritePart|SpriteSheetSize|Deserialize\\(\" E:\\Decompiled_Bannerlord E:\\Steam\\steamapps\\common\\\"Mount & Blade II Bannerlord\\\"\\bin\\Win64_Shipping_Client -g \"*.cs\"" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"Tier2GateBottomHalf|Tier3GateTopHalf|Tier3GateFull|Tier1Locked|@|DataSource=\\\"\\{|Command\\.\" 'Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/Domain/CareerDefinition.cs'; Get-Content -Raw 'Main/Features/CareerSystem/Domain/CareerChoiceGroupDefinition.cs'; Get-Content -Raw 'Main/Features/CareerSystem/CareerRegistry.cs'; Get-Content -Raw 'Main/Features/CareerSystem/ICareerRegistry.cs'; Get-Content -Raw 'Main/Features/CareerSystem/CareerConfigProvider.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/_Module/GUI/TAOMSpriteData.xml'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/UI/CareerAbilityEffectVM.cs'; Get-Content -Raw 'TAOM.Tests/Features/CareerSystem/CareerRegistryTests.cs'; Get-Content -Raw 'TAOM.Tests/Features/CareerSystem/CareerConfigProviderTests.cs'; Get-Content -Raw 'TAOM.Tests/Features/CareerSystem/CareerScreenVMTests.cs'" in C:\Users\mikew\source\repos\TAOM
2026-05-31T03:25:18.715068Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . Tier3GateTopHalf|Tier3GateFull|Tier1Locked|@|DataSource=\"\{[36;1m|[0mCommand\ .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                               ~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAn empty pipe element is not allowed.[0m

 exited 1 in 524ms:
[31;1mParserError: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m . Tier3GateTopHalf|Tier3GateFull|Tier1Locked|@|DataSource=\"\{[36;1m|[0mCommand\ .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m                                                               ~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAn empty pipe element is not allowed.[0m

 succeeded in 544ms:
using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string PortraitSprite { get; }
    public string AbilityTemplateId { get; }
    public int MinClanTier { get; }
    public string RootChoiceId { get; }
    public IReadOnlyList<string> EligibleCultureIds { get; }
    public IReadOnlyList<string> ChoiceGroupIds { get; }

    /// <summary>
    /// Per-tier rank/title names (localized, e.g. "{=key}Captain of Ithilien"). Shown as the
    /// tier headers on the career screen — the career's rank at tiers 1/2/3, escalating in
    /// prestige. Empty when the XML omits them; the screen VM falls back to "Tier N" then.
    /// (Adopted from TOR_Core's tor_career_rank{1,2,3}_name convention.)
    /// </summary>
    public string Rank1Name { get; }
    public string Rank2Name { get; }
    public string Rank3Name { get; }

    public CareerDefinition(
        string id,
        string displayName,
        string description,
        string portraitSprite,
        string abilityTemplateId,
        int minClanTier,
        string rootChoiceId,
        IReadOnlyList<string> eligibleCultureIds,
        IReadOnlyList<string> choiceGroupIds,
        string rank1Name = "",
        string rank2Name = "",
        string rank3Name = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
        PortraitSprite = portraitSprite;
        AbilityTemplateId = abilityTemplateId;
        MinClanTier = minClanTier;
        RootChoiceId = rootChoiceId;
        EligibleCultureIds = eligibleCultureIds ?? new List<string>();
        ChoiceGroupIds = choiceGroupIds ?? new List<string>();
        Rank1Name = rank1Name ?? "";
        Rank2Name = rank2Name ?? "";
        Rank3Name = rank3Name ?? "";
    }
}

using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

public sealed class CareerChoiceGroupDefinition
{
    public string Id { get; }
    public string CareerId { get; }
    public int Tier { get; }
    public IReadOnlyList<string> ChoiceIds { get; }

    /// <summary>
    /// Player-facing path name (localized, e.g. "{=key}Path of the Ranger"). Empty when the
    /// XML omits <c>display_name</c>; the screen VM falls back to a humanized id in that case.
    /// </summary>
    public string DisplayName { get; }

    public CareerChoiceGroupDefinition(
        string id,
        string careerId,
        int tier,
        IReadOnlyList<string> choiceIds,
        string displayName = "")
    {
        Id = id;
        CareerId = careerId;
        Tier = tier;
        ChoiceIds = choiceIds ?? new List<string>();
        DisplayName = displayName ?? "";
    }
}

using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerRegistry : ICareerRegistry
{
    private readonly ICareerConfigProvider _configProvider;
    private readonly IModLogger _logger;

    private Dictionary<string, CareerDefinition> _careers;
    private Dictionary<string, CareerChoiceDefinition> _choices;
    private Dictionary<string, CareerChoiceGroupDefinition> _groups;
    private List<CareerDefinition> _allCareers;
    private int _maxPerkPoints;

    private static readonly IReadOnlyList<CareerChoiceDefinition> EmptyChoices = new List<CareerChoiceDefinition>();

    public CareerRegistry(ICareerConfigProvider configProvider, IModLogger logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public CareerDefinition GetCareer(string careerStringId)
    {
        EnsureLoaded();
        return _careers.TryGetValue(careerStringId, out var career) ? career : null;
    }

    public IReadOnlyList<CareerDefinition> GetAllCareers()
    {
        EnsureLoaded();
        return _allCareers;
    }

    public CareerChoiceDefinition GetChoice(string choiceStringId)
    {
        EnsureLoaded();
        return _choices.TryGetValue(choiceStringId, out var choice) ? choice : null;
    }

    public CareerChoiceGroupDefinition GetGroup(string groupStringId)
    {
        EnsureLoaded();
        return _groups.TryGetValue(groupStringId, out var group) ? group : null;
    }

    public IReadOnlyList<CareerChoiceDefinition> GetChoicesForGroup(string groupStringId)
    {
        EnsureLoaded();
        if (!_groups.TryGetValue(groupStringId, out var group)) return EmptyChoices;

        var result = new List<CareerChoiceDefinition>(group.ChoiceIds.Count);
        foreach (var choiceId in group.ChoiceIds)
        {
            if (_choices.TryGetValue(choiceId, out var choice))
                result.Add(choice);
        }
        return result;
    }

    public bool IsEligible(string careerStringId, ICareerHeroAdapter hero)
    {
        EnsureLoaded();
        if (hero == null)
        {
            _logger.LogDebug($"CareerSystem: IsEligible — hero is null for career '{careerStringId}'");
            return false;
        }
        if (!_careers.TryGetValue(careerStringId, out var career))
        {
            _logger.LogWarning($"CareerSystem: IsEligible — career '{careerStringId}' not found in registry");
            return false;
        }

        if (hero.ClanTier < career.MinClanTier)
        {
            _logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' clanTier={hero.ClanTier} < minClanTier={career.MinClanTier} for career '{careerStringId}'");
            return false;
        }

        if (career.EligibleCultureIds.Count > 0)
        {
            var heroCulture = hero.CultureStringId;
            var found = false;
            foreach (var cultureId in career.EligibleCultureIds)
            {
                if (string.Equals(cultureId, heroCulture, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                _logger.LogDebug($"CareerSystem: IsEligible — hero culture '{heroCulture}' not in [{string.Join(", ", career.EligibleCultureIds)}] for career '{careerStringId}'");
                return false;
            }
        }

        _logger.LogDebug($"CareerSystem: IsEligible — hero culture='{hero.CultureStringId}' IS eligible for career '{careerStringId}'");
        return true;
    }

    public int GetMaxChoicesForHero(int heroLevel)
    {
        EnsureLoaded();
        // Budget: 1 root (auto-added at CC) + N free points for level N
        // Hero starts at level 1 with 1 free point, gains 1 per level.
        var effectiveLevel = Math.Max(1, heroLevel);
        return Math.Min(effectiveLevel + 1, _maxPerkPoints + 1);
    }

    public bool IsTierAvailable(int heroLevel, int tier)
    {
        if (tier < 1 || tier > 3) return false;
        return heroLevel >= GetTierUnlockLevel(tier);
    }

    public int GetTierUnlockLevel(int tier)
    {
        switch (tier)
        {
            case 1: return 1;
            case 2: return 10;
            case 3: return 20;
            default: return int.MaxValue;
        }
    }

    private void EnsureLoaded()
    {
        if (_careers != null) return;

        _careers = new Dictionary<string, CareerDefinition>();
        _choices = new Dictionary<string, CareerChoiceDefinition>();
        _groups = new Dictionary<string, CareerChoiceGroupDefinition>();

        _maxPerkPoints = _configProvider.GetMaxPerkPoints();

        var careers = _configProvider.LoadCareers();
        _allCareers = new List<CareerDefinition>(careers);
        foreach (var career in careers)
            _careers[career.Id] = career;

        foreach (var group in _configProvider.LoadChoiceGroups())
            _groups[group.Id] = group;

        foreach (var choice in _configProvider.LoadChoices())
            _choices[choice.Id] = choice;

        _logger.LogInfo($"CareerSystem: Registry initialized: {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
    }
}

using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerRegistry
{
    CareerDefinition GetCareer(string careerStringId);
    IReadOnlyList<CareerDefinition> GetAllCareers();
    CareerChoiceDefinition GetChoice(string choiceStringId);
    CareerChoiceGroupDefinition GetGroup(string groupStringId);
    IReadOnlyList<CareerChoiceDefinition> GetChoicesForGroup(string groupStringId);
    bool IsEligible(string careerStringId, ICareerHeroAdapter hero);
    int GetMaxChoicesForHero(int heroLevel);
    bool IsTierAvailable(int heroLevel, int tier);

    /// <summary>Hero level at which the given tier (1-3) unlocks. Returns int.MaxValue for unknown tiers.</summary>
    int GetTierUnlockLevel(int tier);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerConfigProvider : ICareerConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private List<CareerDefinition> _careers;
    private List<CareerChoiceGroupDefinition> _groups;
    private List<CareerChoiceDefinition> _choices;
    private int _maxPerkPoints = 30;
    private Dictionary<string, AbilityTemplateData> _abilityTemplates;
    private AbilityTuningConfig _abilityTuning;

    public CareerConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<CareerDefinition> LoadCareers()
    {
        EnsureLoaded();
        return _careers;
    }

    public IReadOnlyList<CareerChoiceGroupDefinition> LoadChoiceGroups()
    {
        EnsureLoaded();
        return _groups;
    }

    public IReadOnlyList<CareerChoiceDefinition> LoadChoices()
    {
        EnsureLoaded();
        return _choices;
    }

    public int GetMaxPerkPoints()
    {
        EnsureLoaded();
        return _maxPerkPoints;
    }

    public AbilityTemplateData GetAbilityTemplate(string templateId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(templateId)) return null;
        return _abilityTemplates.TryGetValue(templateId, out var t) ? t : null;
    }

    public AbilityTuningConfig GetAbilityTuning()
    {
        EnsureLoaded();
        return _abilityTuning;
    }

    private void EnsureLoaded()
    {
        if (_careers != null) return;

        _logger.LogInfo("CareerSystem: Loading career config...");

        _careers = new List<CareerDefinition>();
        _groups = new List<CareerChoiceGroupDefinition>();
        _choices = new List<CareerChoiceDefinition>();
        _abilityTemplates = new Dictionary<string, AbilityTemplateData>();

        LoadCareersXml();
        LoadChoicesXml();
        LoadAbilityTemplatesXml();
        LoadAbilityTuningXml();

        _logger.LogInfo($"CareerSystem: Loaded {_careers.Count} careers, {_groups.Count} groups, {_choices.Count} choices, maxPerkPoints={_maxPerkPoints}");
    }

    private void LoadCareersXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_careers.xml");
        _logger.LogInfo($"CareerSystem: Loading careers from '{path}'");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: careers file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            _maxPerkPoints = ParseInt(root, "max_perk_points", 30);

            foreach (var el in root.Elements("Career"))
            {
                try
                {
                    var cultureIds = new List<string>();
                    var culturesEl = el.Element("EligibleCultures");
                    if (culturesEl != null)
                    {
                        foreach (var c in culturesEl.Elements("Culture"))
                        {
                            var id = c.Attribute("id")?.Value;
                            if (!string.IsNullOrEmpty(id))
                                cultureIds.Add(id);
                        }
                    }

                    var groupIds = new List<string>();
                    var groupsEl = el.Element("ChoiceGroups");
                    if (groupsEl != null)
                    {
                        foreach (var g in groupsEl.Elements("Group"))
                        {
                            var id = g.Attribute("id")?.Value;
                            if (!string.IsNullOrEmpty(id))
                                groupIds.Add(id);
                        }
                    }

                    var career = new CareerDefinition(
                        id: el.Attribute("id")?.Value ?? "",
                        displayName: el.Attribute("display_name")?.Value ?? "",
                        description: el.Attribute("description")?.Value ?? "",
                        portraitSprite: el.Attribute("portrait_sprite")?.Value ?? "",
                        abilityTemplateId: el.Attribute("ability_template_id")?.Value ?? "",
                        minClanTier: ParseInt(el, "min_clan_tier", 0),
                        rootChoiceId: el.Attribute("root_choice_id")?.Value ?? "",
                        eligibleCultureIds: cultureIds,
                        choiceGroupIds: groupIds,
                        rank1Name: el.Attribute("rank1_name")?.Value ?? "",
                        rank2Name: el.Attribute("rank2_name")?.Value ?? "",
                        rank3Name: el.Attribute("rank3_name")?.Value ?? "");

                    _careers.Add(career);
                    _logger.LogDebug($"CareerSystem: Parsed career '{career.Id}' — cultures=[{string.Join(", ", cultureIds)}], groups=[{string.Join(", ", groupIds)}], rootChoice='{career.RootChoiceId}'");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerSystem: Failed to parse career element: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load careers XML: {ex.Message}");
        }
    }

    private void LoadChoicesXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_career_choices.xml");
        _logger.LogInfo($"CareerSystem: Loading choices from '{path}'");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: choices file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            // Parse standalone choices (root nodes)
            foreach (var choiceEl in root.Elements("Choice"))
            {
                var choice = ParseChoice(choiceEl);
                if (choice != null)
                    _choices.Add(choice);
            }

            // Parse choice groups
            foreach (var groupEl in root.Elements("ChoiceGroup"))
            {
                try
                {
                    var choiceIds = new List<string>();
                    foreach (var choiceEl in groupEl.Elements("Choice"))
                    {
                        var choice = ParseChoice(choiceEl);
                        if (choice != null)
                        {
                            _choices.Add(choice);
                            choiceIds.Add(choice.Id);
                        }
                    }

                    var group = new CareerChoiceGroupDefinition(
                        id: groupEl.Attribute("id")?.Value ?? "",
                        careerId: groupEl.Attribute("career_id")?.Value ?? "",
                        tier: ParseInt(groupEl, "tier", 1),
                        choiceIds: choiceIds,
                        displayName: groupEl.Attribute("display_name")?.Value ?? "");

                    _groups.Add(group);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerConfig: failed to parse choice group: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load choices XML: {ex.Message}");
        }
    }

    private CareerChoiceDefinition ParseChoice(XElement el)
    {
        try
        {
            PassiveEffect passive = null;
            // Two authoring schemas exist: a direct singular child <PassiveEffect ... magnitude=.../>
            // and a plural wrapper <PassiveEffects><PassiveEffect ... value=.../></PassiveEffects>.
            // The wrapper form (310 choices) was historically unparsed. Read it as a fallback; all
            // wrappers carry exactly one child (verified). Direct child wins when both are present.
            var passiveEl = el.Element("PassiveEffect") ?? el.Element("PassiveEffects")?.Element("PassiveEffect");
            if (passiveEl != null)
            {
                passive = new PassiveEffect(
                    effectType: ParseEnum<PassiveEffectType>(passiveEl, "type", PassiveEffectType.Special),
                    // Accept value= as an alias for magnitude= (the wrapper schema uses value=).
                    // magnitude= takes precedence when both are present.
                    magnitude: ParseFloat(passiveEl, "magnitude", ParseFloat(passiveEl, "value", 0f)),
                    operation: ParseEnum<OperationType>(passiveEl, "operation", OperationType.Add),
                    isPercentage: ParseBool(passiveEl, "is_percentage", false),
                    attackTypeMask: ParseEnum<AttackTypeMask>(passiveEl, "attack_type_mask", AttackTypeMask.All));
            }

            var mutations = new List<MutationDefinition>();
            var mutationsEl = el.Element("Mutations");
            if (mutationsEl != null)
            {
                foreach (var mutEl in mutationsEl.Elements("Mutation"))
                {
                    var parameters = new Dictionary<string, string>();
                    foreach (var attr in mutEl.Attributes())
                    {
                        var name = attr.Name.LocalName;
                        if (name != "target_id" && name != "property" && name != "calculator" && name != "operation")
                            parameters[name] = attr.Value;
                    }

                    mutations.Add(new MutationDefinition(
                        targetTemplateId: mutEl.Attribute("target_id")?.Value ?? "",
                        propertyName: mutEl.Attribute("property")?.Value ?? "",
                        calculatorId: mutEl.Attribute("calculator")?.Value ?? "",
                        operation: ParseEnum<OperationType>(mutEl, "operation", OperationType.Add),
                        parameters: parameters));
                }
            }

            return new CareerChoiceDefinition(
                id: el.Attribute("id")?.Value ?? "",
                groupId: el.Attribute("group_id")?.Value ?? "",
                type: ParseEnum<ChoiceType>(el, "type", ChoiceType.Passive),
                description: el.Attribute("description")?.Value ?? "",
                iconSprite: el.Attribute("icon_sprite")?.Value ?? "",
                passive: passive,
                mutations: mutations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to parse choice: {ex.Message}");
            return null;
        }
    }

    private void LoadAbilityTemplatesXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_ability_templates.xml");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: ability templates file not found at {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null) return;

            foreach (var el in root.Elements("AbilityTemplate"))
            {
                try
                {
                    var id = el.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var template = new AbilityTemplateData
                    {
                        Id = id,
                        DisplayName = el.Attribute("display_name")?.Value ?? "",
                        Duration = ParseFloat(el, "duration", 8f),
                        Radius = ParseFloat(el, "radius", 10f),
                        MaxCharge = ParseFloat(el, "max_charge", 0f),
                        ParticleEffect = el.Attribute("particle_effect")?.Value ?? "",
                        SoundEffect = el.Attribute("sound_effect")?.Value ?? "",
                        TooltipDescription = el.Attribute("tooltip")?.Value ?? "",
                    };

                    _abilityTemplates[id] = template;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"CareerConfig: failed to parse ability template element: {ex.Message}");
                }
            }

            _logger.LogInfo($"CareerSystem: Loaded {_abilityTemplates.Count} ability templates");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load ability templates XML: {ex.Message}");
        }
    }

    private void LoadAbilityTuningXml()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_ability_tuning.xml");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"CareerConfig: ability tuning file not found at {path} — using defaults");
            _abilityTuning = AbilityTuningConfig.Default;
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null)
            {
                _abilityTuning = AbilityTuningConfig.Default;
                return;
            }

            var globalEl = root.Element("Global");
            var global = ParseGlobalTuning(globalEl);

            var infEl = root.Element("Infantry");
            var infantry = infEl != null
                ? new InfantryTuning(
                    ParseFloat(infEl, "damage_bonus", 15f),
                    ParseFloat(infEl, "damage_reduction", 10f),
                    ParseFloat(infEl, "radius", 50f))
                : InfantryTuning.Default;

            var rngEl = root.Element("Ranged");
            var ranged = rngEl != null
                ? new RangedTuning(
                    ParseFloat(rngEl, "speed_bonus", 15f),
                    ParseFloat(rngEl, "ranged_damage_bonus", 20f),
                    ParseFloat(rngEl, "draw_speed_bonus", 20f))
                : RangedTuning.Default;

            var cavEl = root.Element("Cavalry");
            var cavalry = cavEl != null
                ? new CavalryTuning(
                    ParseFloat(cavEl, "mount_speed_bonus", 20f),
                    ParseFloat(cavEl, "charge_damage_bonus", 25f),
                    ParseFloat(cavEl, "damage_bonus", 10f))
                : CavalryTuning.Default;

            _abilityTuning = new AbilityTuningConfig(global, infantry, ranged, cavalry);
            _logger.LogInfo($"CareerSystem: Loaded ability tuning — Global(cooldown={global.CooldownSeconds}s) Infantry(dmg={infantry.DamageBonus},red={infantry.DamageReduction},r={infantry.Radius}) Ranged(spd={ranged.SpeedBonus},dmg={ranged.RangedDamageBonus},draw={ranged.DrawSpeedBonus}) Cavalry(mspd={cavalry.MountSpeedBonus},chrg={cavalry.ChargeDamageBonus},dmg={cavalry.DamageBonus})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerConfig: failed to load ability tuning XML: {ex.Message}");
            _abilityTuning = AbilityTuningConfig.Default;
        }
    }

    private const float MaxCooldownSeconds = 3600f;

    private GlobalTuning ParseGlobalTuning(XElement globalEl)
    {
        if (globalEl == null) return GlobalTuning.Default;

        var raw = globalEl.Attribute("cooldown_seconds")?.Value;
        if (raw == null) return GlobalTuning.Default;

        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{raw}\"> is not a number — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        // float.TryParse admits NaN, +Infinity, -Infinity. The range checks below evaluate false for NaN
        // (NaN comparisons always yield false), which would let a NaN cooldown reach CareerAbility and
        // permanently break the activation gate. Reject non-finite values explicitly.
        if (float.IsNaN(seconds) || float.IsInfinity(seconds))
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{raw}\"> is not a finite number — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        if (seconds <= 0f)
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{seconds}\"> must be > 0 — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        if (seconds > MaxCooldownSeconds)
        {
            _logger.LogWarning($"CareerConfig: <Global cooldown_seconds=\"{seconds}\"> exceeds maximum of {MaxCooldownSeconds}s — falling back to {GlobalTuning.Default.CooldownSeconds}s");
            return GlobalTuning.Default;
        }

        return new GlobalTuning(seconds);
    }

    private static int ParseInt(XElement el, string attrName, int defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : defaultValue;
    }

    private static float ParseFloat(XElement el, string attrName, float defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        // Phase 9b #128 P2 — reject NaN/Infinity. Pre-fix only CooldownSeconds had this guard
        // (Career #31 fix); generic ParseFloat fed Duration/Radius/MaxCharge/DamageBonus etc.
        // NaN propagates: ExpiresAt = currentTime + NaN → IsExpired always false; NaN Radius →
        // all distance comparisons false. See feedback_clamp_nan_infinity_propagates.md.
        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return defaultValue;
        if (float.IsNaN(result) || float.IsInfinity(result))
            return defaultValue;
        return result;
    }

    private static bool ParseBool(XElement el, string attrName, bool defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return bool.TryParse(val, out var result) ? result : defaultValue;
    }

    private static T ParseEnum<T>(XElement el, string attrName, T defaultValue) where T : struct
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return Enum.TryParse(val, true, out T result) ? result : defaultValue;
    }
}


 succeeded in 554ms:
<?xml version="1.0" encoding="UTF-8"?>
<SpriteData>
  <SpriteCategories>
    <SpriteCategory>
      <Name>ui_custom_fonts</Name>
      <AlwaysLoad />
      <SpriteSheetCount>3</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="2048" Height="2048" />
      <SpriteSheetSize ID="2" Width="2048" Height="2048" />
      <SpriteSheetSize ID="3" Width="2048" Height="2048" />
    </SpriteCategory>
    <SpriteCategory>
      <Name>ui_loading</Name>
      <AlwaysLoad />
      <SpriteSheetCount>2</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="4096" Height="4096" />
      <SpriteSheetSize ID="2" Width="4096" Height="4096" />
    </SpriteCategory>
    <SpriteCategory>
      <Name>ui_taom</Name>
      <AlwaysLoad />
      <SpriteSheetCount>5</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="4096" Height="4096" />
      <SpriteSheetSize ID="2" Width="4096" Height="4096" />
      <SpriteSheetSize ID="3" Width="4096" Height="4096" />
      <SpriteSheetSize ID="4" Width="4096" Height="4096" />
      <SpriteSheetSize ID="5" Width="4096" Height="4096" />
    </SpriteCategory>
    <SpriteCategory>
      <Name>ui_taom_bannericons</Name>
      <AlwaysLoad />
      <SpriteSheetCount>25</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="4096" Height="4096" />
      <SpriteSheetSize ID="2" Width="4096" Height="4096" />
      <SpriteSheetSize ID="3" Width="4096" Height="4096" />
      <SpriteSheetSize ID="4" Width="4096" Height="4096" />
      <SpriteSheetSize ID="5" Width="4096" Height="4096" />
      <SpriteSheetSize ID="6" Width="4096" Height="4096" />
      <SpriteSheetSize ID="7" Width="4096" Height="4096" />
      <SpriteSheetSize ID="8" Width="4096" Height="4096" />
      <SpriteSheetSize ID="9" Width="4096" Height="4096" />
      <SpriteSheetSize ID="10" Width="4096" Height="4096" />
      <SpriteSheetSize ID="11" Width="4096" Height="4096" />
      <SpriteSheetSize ID="12" Width="4096" Height="4096" />
      <SpriteSheetSize ID="13" Width="4096" Height="4096" />
      <SpriteSheetSize ID="14" Width="4096" Height="4096" />
      <SpriteSheetSize ID="15" Width="4096" Height="4096" />
      <SpriteSheetSize ID="16" Width="4096" Height="4096" />
      <SpriteSheetSize ID="17" Width="4096" Height="4096" />
      <SpriteSheetSize ID="18" Width="4096" Height="4096" />
      <SpriteSheetSize ID="19" Width="4096" Height="4096" />
      <SpriteSheetSize ID="20" Width="4096" Height="4096" />
      <SpriteSheetSize ID="21" Width="4096" Height="4096" />
      <SpriteSheetSize ID="22" Width="4096" Height="4096" />
      <SpriteSheetSize ID="23" Width="4096" Height="4096" />
      <SpriteSheetSize ID="24" Width="4096" Height="4096" />
      <SpriteSheetSize ID="25" Width="4096" Height="4096" />
    </SpriteCategory>
    <SpriteCategory>
      <Name>ui_taom_career_system</Name>
      <AlwaysLoad />
      <SpriteSheetCount>2</SpriteSheetCount>
      <SpriteSheetSize ID="1" Width="4096" Height="4096" />
      <SpriteSheetSize ID="2" Width="256" Height="256" />
    </SpriteCategory>
  </SpriteCategories>
  <SpriteParts>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>aniron</Name>
      <Width>2048</Width>
      <Height>2048</Height>
      <SheetX>0</SheetX>
      <SheetY>0</SheetY>
      <CategoryName>ui_custom_fonts</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>minionpro</Name>
      <Width>2048</Width>
      <Height>2048</Height>
      <SheetX>0</SheetX>
      <SheetY>0</SheetY>
      <CategoryName>ui_custom_fonts</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>ringbearer</Name>
      <Width>2048</Width>
      <Height>2048</Height>
      <SheetX>0</SheetX>
      <SheetY>0</SheetY>
      <CategoryName>ui_custom_fonts</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_10</Name>
      <Width>1500</Width>
      <Height>1000</Height>
      <SheetX>1932</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>ui_loading_2</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>ui_loading_3</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>1932</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_4</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_5</Name>
      <Width>1500</Width>
      <Height>1000</Height>
      <SheetX>1932</SheetX>
      <SheetY>1012</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_6</Name>
      <Width>1500</Width>
      <Height>1000</Height>
      <SheetX>4</SheetX>
      <SheetY>1092</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>ui_loading_7</Name>
      <Width>3840</Width>
      <Height>2160</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_8</Name>
      <Width>1500</Width>
      <Height>1000</Height>
      <SheetX>1512</SheetX>
      <SheetY>2020</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ui_loading_9</Name>
      <Width>1500</Width>
      <Height>1000</Height>
      <SheetX>4</SheetX>
      <SheetY>2100</SheetY>
      <CategoryName>ui_loading</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>background_1</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>background_2</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>1932</SheetX>
      <SheetY>2720</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>background_3</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>background_4</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>1932</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>clan_banner</Name>
      <Width>296</Width>
      <Height>650</Height>
      <SheetX>3646</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>default_cursor</Name>
      <Width>32</Width>
      <Height>32</Height>
      <SheetX>1888</SheetX>
      <SheetY>3345</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>FastForward@2x</Name>
      <Width>24</Width>
      <Height>22</Height>
      <SheetX>4052</SheetX>
      <SheetY>4067</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>FastForward@2x_selected</Name>
      <Width>24</Width>
      <Height>22</Height>
      <SheetX>3821</SheetX>
      <SheetY>2667</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>InventoryBackground</Name>
      <Width>1915</Width>
      <Height>1062</Height>
      <SheetX>1932</SheetX>
      <SheetY>2162</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>PartyBackground</Name>
      <Width>1915</Width>
      <Height>1062</Height>
      <SheetX>1932</SheetX>
      <SheetY>1092</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Pause@2x</Name>
      <Width>18</Width>
      <Height>23</Height>
      <SheetX>1888</SheetX>
      <SheetY>3641</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Pause@2x_selected</Name>
      <Width>18</Width>
      <Height>23</Height>
      <SheetX>1888</SheetX>
      <SheetY>3610</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Play@2x</Name>
      <Width>20</Width>
      <Height>24</Height>
      <SheetX>1888</SheetX>
      <SheetY>3543</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Play@2x_selected</Name>
      <Width>20</Width>
      <Height>24</Height>
      <SheetX>1888</SheetX>
      <SheetY>3511</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SaveLoadBackground</Name>
      <Width>1920</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>1092</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>ArmyManagement\army_card</Name>
      <Width>160</Width>
      <Height>140</Height>
      <SheetX>3690</SheetX>
      <SheetY>3182</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>ArmyManagement\header_gather_army_1</Name>
      <Width>139</Width>
      <Height>55</Height>
      <SheetX>3950</SheetX>
      <SheetY>599</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>ArmyManagement\header_gather_army_2</Name>
      <Width>156</Width>
      <Height>55</Height>
      <SheetX>863</SheetX>
      <SheetY>3972</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>ArmyManagement\header_gather_army_3</Name>
      <Width>76</Width>
      <Height>55</Height>
      <SheetX>1842</SheetX>
      <SheetY>3162</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>ArmyManagement\header_gather_army_4</Name>
      <Width>85</Width>
      <Height>55</Height>
      <SheetX>2991</SheetX>
      <SheetY>2927</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>ArmyManagement\header_gather_army_5</Name>
      <Width>71</Width>
      <Height>55</Height>
      <SheetX>4020</SheetX>
      <SheetY>2895</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>ArmyManagement\header_gather_army_6</Name>
      <Width>110</Width>
      <Height>55</Height>
      <SheetX>604</SheetX>
      <SheetY>3897</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>ArmyManagement\panel_tabbar</Name>
      <Width>782</Width>
      <Height>95</Height>
      <SheetX>2914</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>ArmyManagement\voter_tuple</Name>
      <Width>605</Width>
      <Height>88</Height>
      <SheetX>1847</SheetX>
      <SheetY>2026</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>ArmyManagement\voter_tuple_selected</Name>
      <Width>605</Width>
      <Height>88</Height>
      <SheetX>621</SheetX>
      <SheetY>1923</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_collapser</Name>
      <Width>523</Width>
      <Height>140</Height>
      <SheetX>2914</SheetX>
      <SheetY>1459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_collapser_hover</Name>
      <Width>523</Width>
      <Height>140</Height>
      <SheetX>3445</SheetX>
      <SheetY>1459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Barter\barter_collapser_pressed</Name>
      <Width>523</Width>
      <Height>140</Height>
      <SheetX>3020</SheetX>
      <SheetY>810</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_header</Name>
      <Width>496</Width>
      <Height>68</Height>
      <SheetX>3566</SheetX>
      <SheetY>1931</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_item_tuple</Name>
      <Width>467</Width>
      <Height>66</Height>
      <SheetX>3616</SheetX>
      <SheetY>607</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_item_tuple_hover</Name>
      <Width>467</Width>
      <Height>66</Height>
      <SheetX>3616</SheetX>
      <SheetY>385</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_item_tuple_pressed</Name>
      <Width>467</Width>
      <Height>66</Height>
      <SheetX>3616</SheetX>
      <SheetY>533</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_item_tuple_selected</Name>
      <Width>467</Width>
      <Height>66</Height>
      <SheetX>3616</SheetX>
      <SheetY>459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Barter\barter_panel</Name>
      <Width>912</Width>
      <Height>555</Height>
      <SheetX>3100</SheetX>
      <SheetY>3177</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\barter_portrait</Name>
      <Width>118</Width>
      <Height>58</Height>
      <SheetX>3714</SheetX>
      <SheetY>2851</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\deal_button</Name>
      <Width>94</Width>
      <Height>94</Height>
      <SheetX>3422</SheetX>
      <SheetY>2702</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\deal_button_enabled</Name>
      <Width>94</Width>
      <Height>94</Height>
      <SheetX>3524</SheetX>
      <SheetY>2702</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Barter\Gold</Name>
      <Width>63</Width>
      <Height>63</Height>
      <SheetX>4029</SheetX>
      <SheetY>1111</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Barter\header_stone</Name>
      <Width>495</Width>
      <Height>104</Height>
      <SheetX>3591</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Barter\Peace</Name>
      <Width>63</Width>
      <Height>63</Height>
      <SheetX>4029</SheetX>
      <SheetY>1182</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Barter\War</Name>
      <Width>63</Width>
      <Height>63</Height>
      <SheetX>4020</SheetX>
      <SheetY>3570</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\add_attribute_point_button</Name>
      <Width>35</Width>
      <Height>35</Height>
      <SheetX>4057</SheetX>
      <SheetY>1013</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\add_focus</Name>
      <Width>160</Width>
      <Height>160</Height>
      <SheetX>3862</SheetX>
      <SheetY>3350</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\add_focus_button_circle</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3716</SheetX>
      <SheetY>3075</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\add_focus_button_glow</Name>
      <Width>568</Width>
      <Height>168</Height>
      <SheetX>3521</SheetX>
      <SheetY>3867</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\add_focus_hover</Name>
      <Width>160</Width>
      <Height>160</Height>
      <SheetX>3862</SheetX>
      <SheetY>3182</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\attribute_point_icon</Name>
      <Width>38</Width>
      <Height>38</Height>
      <SheetX>2369</SheetX>
      <SheetY>2469</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\character_developer_switcher</Name>
      <Width>70</Width>
      <Height>30</Height>
      <SheetX>1475</SheetX>
      <SheetY>4057</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\character_perk_background</Name>
      <Width>80</Width>
      <Height>86</Height>
      <SheetX>3538</SheetX>
      <SheetY>2851</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\character_perk_background_active</Name>
      <Width>80</Width>
      <Height>86</Height>
      <SheetX>3626</SheetX>
      <SheetY>2895</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\character_progress_bar</Name>
      <Width>244</Width>
      <Height>14</Height>
      <SheetX>1842</SheetX>
      <SheetY>4076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\character_progress_bar_glow</Name>
      <Width>244</Width>
      <Height>14</Height>
      <SheetX>2094</SheetX>
      <SheetY>4076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\character_progress_frame</Name>
      <Width>321</Width>
      <Height>27</Height>
      <SheetX>3556</SheetX>
      <SheetY>1983</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\character_progress_panel</Name>
      <Width>2042</Width>
      <Height>206</Height>
      <SheetX>1888</SheetX>
      <SheetY>3808</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\chooser_arrow</Name>
      <Width>22</Width>
      <Height>42</Height>
      <SheetX>3821</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\cp_icon</Name>
      <Width>39</Width>
      <Height>39</Height>
      <SheetX>2369</SheetX>
      <SheetY>2328</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_big_empty</Name>
      <Width>20</Width>
      <Height>64</Height>
      <SheetX>4057</SheetX>
      <SheetY>678</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_big_filled</Name>
      <Width>20</Width>
      <Height>64</Height>
      <SheetX>4057</SheetX>
      <SheetY>750</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_empty</Name>
      <Width>11</Width>
      <Height>30</Height>
      <SheetX>4078</SheetX>
      <SheetY>257</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_filled</Name>
      <Width>11</Width>
      <Height>30</Height>
      <SheetX>4078</SheetX>
      <SheetY>295</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_large_empty</Name>
      <Width>38</Width>
      <Height>59</Height>
      <SheetX>2623</SheetX>
      <SheetY>2311</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\focus_point_large_full</Name>
      <Width>38</Width>
      <Height>59</Height>
      <SheetX>2623</SheetX>
      <SheetY>2378</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_1</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3621</SheetX>
      <SheetY>3075</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_10</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3336</SheetX>
      <SheetY>3032</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_2</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3526</SheetX>
      <SheetY>3032</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_3</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3431</SheetX>
      <SheetY>3052</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_4</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3811</SheetX>
      <SheetY>3161</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_5</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3716</SheetX>
      <SheetY>3151</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_6</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3906</SheetX>
      <SheetY>3161</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_7</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3526</SheetX>
      <SheetY>3083</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_8</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3431</SheetX>
      <SheetY>3001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\focus_point_small_9</Name>
      <Width>87</Width>
      <Height>43</Height>
      <SheetX>3621</SheetX>
      <SheetY>3126</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\how_to_learn</Name>
      <Width>41</Width>
      <Height>41</Height>
      <SheetX>2369</SheetX>
      <SheetY>2179</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\indicator</Name>
      <Width>40</Width>
      <Height>70</Height>
      <SheetX>2623</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\learning_rate_limit_triangle</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3200</SheetX>
      <SheetY>3053</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\left_panel_coverup</Name>
      <Width>556</Width>
      <Height>260</Height>
      <SheetX>4</SheetX>
      <SheetY>3100</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\left_panel_stone</Name>
      <Width>612</Width>
      <Height>91</Height>
      <SheetX>626</SheetX>
      <SheetY>1611</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\little_progressbar_fill</Name>
      <Width>25</Width>
      <Height>9</Height>
      <SheetX>1888</SheetX>
      <SheetY>3701</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\little_progressbar_frame</Name>
      <Width>31</Width>
      <Height>17</Height>
      <SheetX>1888</SheetX>
      <SheetY>3461</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\little_progressbar_glow</Name>
      <Width>50</Width>
      <Height>9</Height>
      <SheetX>1252</SheetX>
      <SheetY>4079</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\notification</Name>
      <Width>37</Width>
      <Height>40</Height>
      <SheetX>2369</SheetX>
      <SheetY>2421</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\perk_bg</Name>
      <Width>176</Width>
      <Height>313</Height>
      <SheetX>3855</SheetX>
      <SheetY>2092</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\perk_frame</Name>
      <Width>53</Width>
      <Height>53</Height>
      <SheetX>4039</SheetX>
      <SheetY>2208</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\perk_selection_popup_arrow</Name>
      <Width>22</Width>
      <Height>26</Height>
      <SheetX>3821</SheetX>
      <SheetY>2633</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\progress_bar_empty</Name>
      <Width>950</Width>
      <Height>185</Height>
      <SheetX>3100</SheetX>
      <SheetY>3740</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\progress_bar_fill</Name>
      <Width>950</Width>
      <Height>185</Height>
      <SheetX>3100</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\progress_bar_glow</Name>
      <Width>130</Width>
      <Height>188</Height>
      <SheetX>3950</SheetX>
      <SheetY>222</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\progress_bar_stick</Name>
      <Width>31</Width>
      <Height>202</Height>
      <SheetX>4057</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\selected_perk_indicator</Name>
      <Width>57</Width>
      <Height>62</Height>
      <SheetX>4032</SheetX>
      <SheetY>3017</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\selected_perk_indicator_hover</Name>
      <Width>57</Width>
      <Height>62</Height>
      <SheetX>4032</SheetX>
      <SheetY>3087</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\skills_divider_diamond</Name>
      <Width>1008</Width>
      <Height>27</Height>
      <SheetX>4</SheetX>
      <SheetY>4046</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\skills_icon_shadow</Name>
      <Width>92</Width>
      <Height>94</Height>
      <SheetX>3626</SheetX>
      <SheetY>2733</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\skill_description</Name>
      <Width>41</Width>
      <Height>41</Height>
      <SheetX>2799</SheetX>
      <SheetY>4006</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\skill_frame</Name>
      <Width>95</Width>
      <Height>97</Height>
      <SheetX>3985</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\skill_icon_large_frame</Name>
      <Width>151</Width>
      <Height>151</Height>
      <SheetX>3822</SheetX>
      <SheetY>877</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\UnselectedPerksIcon</Name>
      <Width>34</Width>
      <Height>34</Height>
      <SheetX>1888</SheetX>
      <SheetY>3303</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_canvas</Name>
      <Width>533</Width>
      <Height>627</Height>
      <SheetX>3521</SheetX>
      <SheetY>3232</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_divider</Name>
      <Width>565</Width>
      <Height>55</Height>
      <SheetX>2325</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_frame</Name>
      <Width>576</Width>
      <Height>675</Height>
      <SheetX>3100</SheetX>
      <SheetY>1136</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\AttributePopup\plus_button</Name>
      <Width>77</Width>
      <Height>77</Height>
      <SheetX>3624</SheetX>
      <SheetY>2989</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\AttributePopup\plus_button_hover</Name>
      <Width>77</Width>
      <Height>77</Height>
      <SheetX>951</SheetX>
      <SheetY>3700</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\AttributePopup\plus_button_pressed</Name>
      <Width>77</Width>
      <Height>77</Height>
      <SheetX>3184</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\attribute_card</Name>
      <Width>73</Width>
      <Height>138</Height>
      <SheetX>3995</SheetX>
      <SheetY>3933</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\attribute_card_selection</Name>
      <Width>73</Width>
      <Height>138</Height>
      <SheetX>2191</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_card</Name>
      <Width>209</Width>
      <Height>162</Height>
      <SheetX>3604</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_card_selection</Name>
      <Width>209</Width>
      <Height>162</Height>
      <SheetX>2452</SheetX>
      <SheetY>2529</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate1</Name>
      <Width>152</Width>
      <Height>152</Height>
      <SheetX>3822</SheetX>
      <SheetY>717</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate2</Name>
      <Width>152</Width>
      <Height>152</Height>
      <SheetX>3822</SheetX>
      <SheetY>397</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate3</Name>
      <Width>152</Width>
      <Height>152</Height>
      <SheetX>3822</SheetX>
      <SheetY>557</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate_selected_state</Name>
      <Width>155</Width>
      <Height>156</Height>
      <SheetX>3862</SheetX>
      <SheetY>3808</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\clan_collapser</Name>
      <Width>585</Width>
      <Height>96</Height>
      <SheetX>2405</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\clan_collapser_pressed</Name>
      <Width>585</Width>
      <Height>96</Height>
      <SheetX>626</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Clan\clan_portrait_shadow</Name>
      <Width>392</Width>
      <Height>123</Height>
      <SheetX>612</SheetX>
      <SheetY>3569</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\clan_tuple</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>597</SheetX>
      <SheetY>2019</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Clan\clan_tuple_dropdown_slot</Name>
      <Width>108</Width>
      <Height>27</Height>
      <SheetX>3885</SheetX>
      <SheetY>1983</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\clan_tuple_selected</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>1190</SheetX>
      <SheetY>2049</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\clan_tuple_tip</Name>
      <Width>605</Width>
      <Height>88</Height>
      <SheetX>1234</SheetX>
      <SheetY>1953</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Clan\fiefs_sort_header_fief_name</Name>
      <Width>296</Width>
      <Height>98</Height>
      <SheetX>612</SheetX>
      <SheetY>3802</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\fiefs_sort_header_governor</Name>
      <Width>164</Width>
      <Height>98</Height>
      <SheetX>863</SheetX>
      <SheetY>3340</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\fiefs_sort_header_profit</Name>
      <Width>128</Width>
      <Height>98</Height>
      <SheetX>1023</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Clan\finance_collapser</Name>
      <Width>851</Width>
      <Height>119</Height>
      <SheetX>4</SheetX>
      <SheetY>3919</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\finance_collapser_hover</Name>
      <Width>851</Width>
      <Height>119</Height>
      <SheetX>2961</SheetX>
      <SheetY>3874</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\finance_collapser_pressed</Name>
      <Width>851</Width>
      <Height>119</Height>
      <SheetX>4</SheetX>
      <SheetY>3368</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\finance_tuple</Name>
      <Width>817</Width>
      <Height>88</Height>
      <SheetX>2961</SheetX>
      <SheetY>4001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\header</Name>
      <Width>601</Width>
      <Height>98</Height>
      <SheetX>2999</SheetX>
      <SheetY>1713</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\members_sort_header_location</Name>
      <Width>251</Width>
      <Height>98</Height>
      <SheetX>3822</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Clan\members_sort_header_name</Name>
      <Width>337</Width>
      <Height>98</Height>
      <SheetX>3100</SheetX>
      <SheetY>3061</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\other_sort_header_income</Name>
      <Width>128</Width>
      <Height>98</Height>
      <SheetX>887</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\other_sort_header_location</Name>
      <Width>173</Width>
      <Height>98</Height>
      <SheetX>3847</SheetX>
      <SheetY>2565</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\other_sort_header_workshop_name</Name>
      <Width>287</Width>
      <Height>98</Height>
      <SheetX>3690</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\panel_header</Name>
      <Width>601</Width>
      <Height>98</Height>
      <SheetX>1850</SheetX>
      <SheetY>1499</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\parties_sort_header_location</Name>
      <Width>205</Width>
      <Height>98</Height>
      <SheetX>3860</SheetX>
      <SheetY>823</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\parties_sort_header_name</Name>
      <Width>250</Width>
      <Height>98</Height>
      <SheetX>3822</SheetX>
      <SheetY>110</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\parties_sort_header_size</Name>
      <Width>133</Width>
      <Height>98</Height>
      <SheetX>583</SheetX>
      <SheetY>2567</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Clan\portrait_slot_right_panel</Name>
      <Width>113</Width>
      <Height>84</Height>
      <SheetX>2572</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Clan\progress_bar</Name>
      <Width>244</Width>
      <Height>14</Height>
      <SheetX>2346</SheetX>
      <SheetY>4076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Clan\progress_bar_frame</Name>
      <Width>266</Width>
      <Height>32</Height>
      <SheetX>1002</SheetX>
      <SheetY>4057</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Clan\progress_bar_glow</Name>
      <Width>244</Width>
      <Height>14</Height>
      <SheetX>2143</SheetX>
      <SheetY>2692</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\bottom_crafting</Name>
      <Width>461</Width>
      <Height>85</Height>
      <SheetX>3616</SheetX>
      <SheetY>292</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Crafting\crafted_popup</Name>
      <Width>485</Width>
      <Height>771</Height>
      <SheetX>3604</SheetX>
      <SheetY>1092</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Crafting\crafting_item_civillian</Name>
      <Width>35</Width>
      <Height>35</Height>
      <SheetX>1888</SheetX>
      <SheetY>3260</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\crafting_leave_button</Name>
      <Width>142</Width>
      <Height>69</Height>
      <SheetX>2272</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\crafting_leave_button_hover</Name>
      <Width>142</Width>
      <Height>69</Height>
      <SheetX>2422</SheetX>
      <SheetY>2589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Crafting\crafting_popup_portrait</Name>
      <Width>212</Width>
      <Height>116</Height>
      <SheetX>3862</SheetX>
      <SheetY>3684</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Crafting\crafting_weapon_type_stone</Name>
      <Width>576</Width>
      <Height>49</Height>
      <SheetX>591</SheetX>
      <SheetY>4040</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\header_button1</Name>
      <Width>169</Width>
      <Height>51</Height>
      <SheetX>3726</SheetX>
      <SheetY>2733</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\header_button1_pressed</Name>
      <Width>165</Width>
      <Height>50</Height>
      <SheetX>2919</SheetX>
      <SheetY>2707</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\header_button2</Name>
      <Width>163</Width>
      <Height>51</Height>
      <SheetX>2748</SheetX>
      <SheetY>2685</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\header_button2_pressed</Name>
      <Width>237</Width>
      <Height>50</Height>
      <SheetX>2043</SheetX>
      <SheetY>2417</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Crafting\header_button3</Name>
      <Width>247</Width>
      <Height>51</Height>
      <SheetX>2071</SheetX>
      <SheetY>3001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\header_button3_pressed</Name>
      <Width>168</Width>
      <Height>50</Height>
      <SheetX>2572</SheetX>
      <SheetY>2656</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Crafting\left_panel_crafting</Name>
      <Width>140</Width>
      <Height>470</Height>
      <SheetX>3938</SheetX>
      <SheetY>3589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\main_tab_default</Name>
      <Width>444</Width>
      <Height>136</Height>
      <SheetX>3616</SheetX>
      <SheetY>148</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\main_tab_hovered</Name>
      <Width>444</Width>
      <Height>136</Height>
      <SheetX>3616</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Crafting\main_tab_pressed</Name>
      <Width>444</Width>
      <Height>136</Height>
      <SheetX>568</SheetX>
      <SheetY>3100</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Crafting\main_tab_selected</Name>
      <Width>444</Width>
      <Height>136</Height>
      <SheetX>3646</SheetX>
      <SheetY>662</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Crafting\refine_panel_header</Name>
      <Width>579</Width>
      <Height>51</Height>
      <SheetX>4</SheetX>
      <SheetY>4040</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Crafting\right_panel_crafting</Name>
      <Width>714</Width>
      <Height>1124</Height>
      <SheetX>3100</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\smelt_tuple_default</Name>
      <Width>559</Width>
      <Height>80</Height>
      <SheetX>1243</SheetX>
      <SheetY>1710</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\smelt_tuple_pressed</Name>
      <Width>559</Width>
      <Height>80</Height>
      <SheetX>2974</SheetX>
      <SheetY>2129</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\smelt_tuple_selected</Name>
      <Width>559</Width>
      <Height>80</Height>
      <SheetX>2407</SheetX>
      <SheetY>2129</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\tuple_refine</Name>
      <Width>567</Width>
      <Height>135</Height>
      <SheetX>1764</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\tuple_refine_pressed</Name>
      <Width>567</Width>
      <Height>135</Height>
      <SheetX>1189</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Crafting\tuple_refine_selected</Name>
      <Width>567</Width>
      <Height>135</Height>
      <SheetX>2339</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Encyclopedia\banner</Name>
      <Width>130</Width>
      <Height>210</Height>
      <SheetX>3950</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Encyclopedia\canvas</Name>
      <Width>1500</Width>
      <Height>960</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Encyclopedia\clan</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3860</SheetX>
      <SheetY>1048</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Encyclopedia\concept</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3860</SheetX>
      <SheetY>3067</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Encyclopedia\hero</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3860</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Encyclopedia\icon_search</Name>
      <Width>25</Width>
      <Height>24</Height>
      <SheetX>4057</SheetX>
      <SheetY>1056</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Encyclopedia\kingdom</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3860</SheetX>
      <SheetY>526</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Encyclopedia\navbar</Name>
      <Width>508</Width>
      <Height>48</Height>
      <SheetX>3521</SheetX>
      <SheetY>4043</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Encyclopedia\search_bar</Name>
      <Width>331</Width>
      <Height>94</Height>
      <SheetX>612</SheetX>
      <SheetY>3700</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Encyclopedia\settlements</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3855</SheetX>
      <SheetY>1570</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Encyclopedia\subpage_notable_portrait</Name>
      <Width>88</Width>
      <Height>63</Height>
      <SheetX>3464</SheetX>
      <SheetY>2567</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Encyclopedia\subpage_slick_frame</Name>
      <Width>53</Width>
      <Height>51</Height>
      <SheetX>4035</SheetX>
      <SheetY>3602</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Encyclopedia\troop_tree_side</Name>
      <Width>25</Width>
      <Height>30</Height>
      <SheetX>3821</SheetX>
      <SheetY>2595</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Encyclopedia\troop_tree_straight</Name>
      <Width>3</Width>
      <Height>30</Height>
      <SheetX>4086</SheetX>
      <SheetY>3589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Encyclopedia\units</Name>
      <Width>223</Width>
      <Height>514</Height>
      <SheetX>3860</SheetX>
      <SheetY>2545</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Button\button_cancel</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3679</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Button\button_cancel_hover</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3771</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Button\main_button_done</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3863</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Button\main_button_done_hover</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3955</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Button\main_button_regular</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>3814</SheetX>
      <SheetY>2194</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Button\main_button_regular_big</Name>
      <Width>480</Width>
      <Height>64</Height>
      <SheetX>3608</SheetX>
      <SheetY>1607</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Button\main_button_regular_big_hover</Name>
      <Width>480</Width>
      <Height>64</Height>
      <SheetX>3608</SheetX>
      <SheetY>1679</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Button\main_button_regular_hover</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>3684</SheetX>
      <SheetY>1556</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\CharacterCreation\character_creation_panel</Name>
      <Width>694</Width>
      <Height>1080</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>General\CharacterCreation\character_creation_panel_extension</Name>
      <Width>83</Width>
      <Height>516</Height>
      <SheetX>3855</SheetX>
      <SheetY>2653</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\CharacterCreation\culture_flag_small</Name>
      <Width>163</Width>
      <Height>349</Height>
      <SheetX>2452</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Frame1\canvas</Name>
      <Width>618</Width>
      <Height>798</Height>
      <SheetX>3020</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>General\Frame1\frame</Name>
      <Width>663</Width>
      <Height>847</Height>
      <SheetX>2850</SheetX>
      <SheetY>3232</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Frame1\shadow</Name>
      <Width>350</Width>
      <Height>350</Height>
      <SheetX>612</SheetX>
      <SheetY>3211</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Frame1.Broken\canvas</Name>
      <Width>512</Width>
      <Height>512</Height>
      <SheetX>1932</SheetX>
      <SheetY>2172</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Frame1.Broken\frame</Name>
      <Width>512</Width>
      <Height>512</Height>
      <SheetX>3556</SheetX>
      <SheetY>1463</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Army</Name>
      <Width>39</Width>
      <Height>35</Height>
      <SheetX>2369</SheetX>
      <SheetY>2603</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Icons\Coin@2x</Name>
      <Width>45</Width>
      <Height>45</Height>
      <SheetX>1204</SheetX>
      <SheetY>3920</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Food</Name>
      <Width>64</Width>
      <Height>64</Height>
      <SheetX>4020</SheetX>
      <SheetY>3498</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Icons\Food@2x</Name>
      <Width>80</Width>
      <Height>80</Height>
      <SheetX>3997</SheetX>
      <SheetY>3073</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Icons\Garrison</Name>
      <Width>68</Width>
      <Height>54</Height>
      <SheetX>4023</SheetX>
      <SheetY>1361</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Health@2x</Name>
      <Width>55</Width>
      <Height>55</Height>
      <SheetX>970</SheetX>
      <SheetY>3405</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Health_Sick@2x</Name>
      <Width>55</Width>
      <Height>55</Height>
      <SheetX>970</SheetX>
      <SheetY>3468</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Icons\Influence@2x</Name>
      <Width>55</Width>
      <Height>55</Height>
      <SheetX>4035</SheetX>
      <SheetY>3417</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Icons\Level@2x</Name>
      <Width>80</Width>
      <Height>80</Height>
      <SheetX>3804</SheetX>
      <SheetY>3063</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Icons\Militia</Name>
      <Width>64</Width>
      <Height>54</Height>
      <SheetX>4025</SheetX>
      <SheetY>3879</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Morale</Name>
      <Width>64</Width>
      <Height>64</Height>
      <SheetX>4020</SheetX>
      <SheetY>3426</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Icons\Morale@2x</Name>
      <Width>80</Width>
      <Height>80</Height>
      <SheetX>3997</SheetX>
      <SheetY>2985</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Icons\Party@2x</Name>
      <Width>54</Width>
      <Height>54</Height>
      <SheetX>4035</SheetX>
      <SheetY>3480</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\PartyCost@2x</Name>
      <Width>54</Width>
      <Height>54</Height>
      <SheetX>2356</SheetX>
      <SheetY>2853</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Prosperity</Name>
      <Width>64</Width>
      <Height>64</Height>
      <SheetX>4020</SheetX>
      <SheetY>3354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Speed@2x</Name>
      <Width>45</Width>
      <Height>44</Height>
      <SheetX>863</SheetX>
      <SheetY>3908</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\TroopCost@2x</Name>
      <Width>54</Width>
      <Height>54</Height>
      <SheetX>2356</SheetX>
      <SheetY>2915</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Icons\Walls</Name>
      <Width>58</Width>
      <Height>54</Height>
      <SheetX>970</SheetX>
      <SheetY>3343</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\InitialMenu\hover_indicator</Name>
      <Width>46</Width>
      <Height>20</Height>
      <SheetX>3998</SheetX>
      <SheetY>4067</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\InitialMenu\main_menu_logo</Name>
      <Width>480</Width>
      <Height>131</Height>
      <SheetX>2511</SheetX>
      <SheetY>1607</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\InitialMenu\main_menu_logo_960</Name>
      <Width>480</Width>
      <Height>131</Height>
      <SheetX>2198</SheetX>
      <SheetY>3729</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\InitialMenu\main_menu_texture</Name>
      <Width>445</Width>
      <Height>1080</Height>
      <SheetX>3604</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Mission\health_bar_fill_glow</Name>
      <Width>49</Width>
      <Height>13</Height>
      <SheetX>3860</SheetX>
      <SheetY>3781</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Mission\health_bar_fill_glow_small</Name>
      <Width>47</Width>
      <Height>13</Height>
      <SheetX>2623</SheetX>
      <SheetY>2498</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Mission\health_bar_frame</Name>
      <Width>249</Width>
      <Height>54</Height>
      <SheetX>3464</SheetX>
      <SheetY>2640</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>General\Mission\hero_canvas</Name>
      <Width>249</Width>
      <Height>54</Height>
      <SheetX>3721</SheetX>
      <SheetY>2671</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Mission\hero_icon</Name>
      <Width>68</Width>
      <Height>69</Height>
      <SheetX>3860</SheetX>
      <SheetY>3704</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Mission\horse_canvas</Name>
      <Width>191</Width>
      <Height>26</Height>
      <SheetX>2397</SheetX>
      <SheetY>4064</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Mission\horse_frame</Name>
      <Width>191</Width>
      <Height>26</Height>
      <SheetX>1276</SheetX>
      <SheetY>4057</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Mission\mission_health_bar_fill</Name>
      <Width>31</Width>
      <Height>17</Height>
      <SheetX>1888</SheetX>
      <SheetY>3486</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>General\Mission\mission_health_bar_fill_small</Name>
      <Width>24</Width>
      <Height>13</Height>
      <SheetX>3821</SheetX>
      <SheetY>2697</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Mission\mount_icon</Name>
      <Width>58</Width>
      <Height>58</Height>
      <SheetX>970</SheetX>
      <SheetY>3277</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Mission\shield_canvas</Name>
      <Width>191</Width>
      <Height>26</Height>
      <SheetX>2596</SheetX>
      <SheetY>4064</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>General\Mission\shield_frame</Name>
      <Width>191</Width>
      <Height>26</Height>
      <SheetX>2198</SheetX>
      <SheetY>4064</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>General\Mission\shield_icon</Name>
      <Width>58</Width>
      <Height>58</Height>
      <SheetX>970</SheetX>
      <SheetY>3211</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\armorSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3157</SheetX>
      <SheetY>2354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\armorSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2949</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\bannerSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2741</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\bannerSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2533</SheetX>
      <SheetY>2354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\bootSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3365</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\bootSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2667</SheetX>
      <SheetY>1499</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\cloakSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2459</SheetX>
      <SheetY>1499</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\cloakSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3860</SheetX>
      <SheetY>929</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\extension</Name>
      <Width>546</Width>
      <Height>158</Height>
      <SheetX>635</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterAll</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>3352</SheetX>
      <SheetY>2910</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterAllDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>3456</SheetX>
      <SheetY>2804</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterAllDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2882</SheetX>
      <SheetY>2026</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterAllHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>3010</SheetX>
      <SheetY>2765</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterArmors</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2354</SheetX>
      <SheetY>2666</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterArmorsDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2285</SheetX>
      <SheetY>2749</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterArmorsDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2203</SheetX>
      <SheetY>2749</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterArmorsHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2121</SheetX>
      <SheetY>2691</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterFood</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2039</SheetX>
      <SheetY>2641</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterFoodDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>1957</SheetX>
      <SheetY>2559</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterFoodDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2272</SheetX>
      <SheetY>2622</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterFoodHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2039</SheetX>
      <SheetY>2558</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterMounts</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>1957</SheetX>
      <SheetY>2476</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterMountsDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>1875</SheetX>
      <SheetY>2558</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterMountsDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2367</SheetX>
      <SheetY>2827</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterMountsHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>1793</SheetX>
      <SheetY>2476</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterShields</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2928</SheetX>
      <SheetY>2765</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterShieldsDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2846</SheetX>
      <SheetY>2765</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterShieldsDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2764</SheetX>
      <SheetY>2765</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterShieldsHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2449</SheetX>
      <SheetY>2797</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterWeapons</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2682</SheetX>
      <SheetY>2744</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterWeaponsDark</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2600</SheetX>
      <SheetY>2744</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterWeaponsDown</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2518</SheetX>
      <SheetY>2714</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\filterWeaponsHighlight</Name>
      <Width>74</Width>
      <Height>75</Height>
      <SheetX>2436</SheetX>
      <SheetY>2666</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\gloveSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3860</SheetX>
      <SheetY>1253</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\gloveSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3819</SheetX>
      <SheetY>2351</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_1</Name>
      <Width>177</Width>
      <Height>51</Height>
      <SheetX>2813</SheetX>
      <SheetY>2626</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_1_hover</Name>
      <Width>177</Width>
      <Height>51</Height>
      <SheetX>2813</SheetX>
      <SheetY>2567</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_1_pressed</Name>
      <Width>177</Width>
      <Height>51</Height>
      <SheetX>2998</SheetX>
      <SheetY>2589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_1_selected</Name>
      <Width>177</Width>
      <Height>51</Height>
      <SheetX>2998</SheetX>
      <SheetY>2648</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_2</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1543</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_2_hover</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1293</SheetX>
      <SheetY>2417</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_2_pressed</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1793</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_2_selected</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>2043</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_3</Name>
      <Width>58</Width>
      <Height>51</Height>
      <SheetX>4001</SheetX>
      <SheetY>3161</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\header_button_left_3_hover</Name>
      <Width>58</Width>
      <Height>51</Height>
      <SheetX>4025</SheetX>
      <SheetY>3941</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_3_pressed</Name>
      <Width>58</Width>
      <Height>51</Height>
      <SheetX>4027</SheetX>
      <SheetY>2346</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_3_selected</Name>
      <Width>58</Width>
      <Height>51</Height>
      <SheetX>4028</SheetX>
      <SheetY>2565</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_4</Name>
      <Width>106</Width>
      <Height>52</Height>
      <SheetX>2645</SheetX>
      <SheetY>2848</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_4_hover</Name>
      <Width>106</Width>
      <Height>52</Height>
      <SheetX>2531</SheetX>
      <SheetY>2848</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_4_pressed</Name>
      <Width>106</Width>
      <Height>52</Height>
      <SheetX>3934</SheetX>
      <SheetY>2286</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_left_4_selected</Name>
      <Width>106</Width>
      <Height>52</Height>
      <SheetX>3714</SheetX>
      <SheetY>2917</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_1</Name>
      <Width>168</Width>
      <Height>51</Height>
      <SheetX>3903</SheetX>
      <SheetY>2777</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_1_hover</Name>
      <Width>168</Width>
      <Height>51</Height>
      <SheetX>3726</SheetX>
      <SheetY>2792</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_1_pressed</Name>
      <Width>168</Width>
      <Height>51</Height>
      <SheetX>3902</SheetX>
      <SheetY>2836</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_1_selected</Name>
      <Width>168</Width>
      <Height>51</Height>
      <SheetX>3183</SheetX>
      <SheetY>2703</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_2</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1543</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_2_hover</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1793</SheetX>
      <SheetY>2417</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_2_pressed</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1533</SheetX>
      <SheetY>2144</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_2_selected</Name>
      <Width>242</Width>
      <Height>51</Height>
      <SheetX>1293</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\header_button_right_3</Name>
      <Width>61</Width>
      <Height>51</Height>
      <SheetX>4030</SheetX>
      <SheetY>3181</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\header_button_right_3_hover</Name>
      <Width>61</Width>
      <Height>51</Height>
      <SheetX>4030</SheetX>
      <SheetY>3240</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\header_button_right_3_pressed</Name>
      <Width>61</Width>
      <Height>51</Height>
      <SheetX>4030</SheetX>
      <SheetY>3299</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\header_button_right_3_selected</Name>
      <Width>61</Width>
      <Height>51</Height>
      <SheetX>4030</SheetX>
      <SheetY>3358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_4</Name>
      <Width>108</Width>
      <Height>51</Height>
      <SheetX>2991</SheetX>
      <SheetY>2868</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_4_hover</Name>
      <Width>108</Width>
      <Height>51</Height>
      <SheetX>2875</SheetX>
      <SheetY>2848</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_4_pressed</Name>
      <Width>108</Width>
      <Height>51</Height>
      <SheetX>2759</SheetX>
      <SheetY>2868</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_button_right_4_selected</Name>
      <Width>108</Width>
      <Height>51</Height>
      <SheetX>2875</SheetX>
      <SheetY>2907</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_left</Name>
      <Width>609</Width>
      <Height>91</Height>
      <SheetX>626</SheetX>
      <SheetY>1710</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\header_right</Name>
      <Width>609</Width>
      <Height>91</Height>
      <SheetX>4</SheetX>
      <SheetY>1847</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\helmetSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3403</SheetX>
      <SheetY>2351</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\helmetSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3611</SheetX>
      <SheetY>2351</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Inventory\icon_equip</Name>
      <Width>53</Width>
      <Height>51</Height>
      <SheetX>4039</SheetX>
      <SheetY>2329</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Inventory\icon_gold</Name>
      <Width>45</Width>
      <Height>45</Height>
      <SheetX>1204</SheetX>
      <SheetY>3973</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Inventory\icon_inspect</Name>
      <Width>54</Width>
      <Height>52</Height>
      <SheetX>4035</SheetX>
      <SheetY>3542</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Inventory\icon_inventory_weight</Name>
      <Width>33</Width>
      <Height>41</Height>
      <SheetX>4057</SheetX>
      <SheetY>579</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Inventory\icon_unequip</Name>
      <Width>35</Width>
      <Height>36</Height>
      <SheetX>4057</SheetX>
      <SheetY>822</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Inventory\inspect</Name>
      <Width>40</Width>
      <Height>38</Height>
      <SheetX>2369</SheetX>
      <SheetY>2375</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\modCivillian</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>2436</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Inventory\modCivillianDown</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>3946</SheetX>
      <SheetY>3135</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\modCivillianHighlight</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>2288</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\modWar</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>3000</SheetX>
      <SheetY>2354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\modWarDown</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>2584</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\modWarHighlight</Name>
      <Width>140</Width>
      <Height>75</Height>
      <SheetX>2043</SheetX>
      <SheetY>2475</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\mountArmorSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3616</SheetX>
      <SheetY>823</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\mountArmorSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>2325</SheetX>
      <SheetY>2354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\mountSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3860</SheetX>
      <SheetY>1145</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\mountSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3860</SheetX>
      <SheetY>1037</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\portrait</Name>
      <Width>155</Width>
      <Height>81</Height>
      <SheetX>724</SheetX>
      <SheetY>2358</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\portrait_cart</Name>
      <Width>172</Width>
      <Height>81</Height>
      <SheetX>3284</SheetX>
      <SheetY>2567</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Inventory\production_icon</Name>
      <Width>44</Width>
      <Height>53</Height>
      <SheetX>2623</SheetX>
      <SheetY>2250</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\ShieldIcon</Name>
      <Width>90</Width>
      <Height>90</Height>
      <SheetX>3184</SheetX>
      <SheetY>2762</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\tuple_left</Name>
      <Width>554</Width>
      <Height>59</Height>
      <SheetX>639</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\tuple_left_pressed</Name>
      <Width>554</Width>
      <Height>59</Height>
      <SheetX>1201</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\tuple_right</Name>
      <Width>554</Width>
      <Height>59</Height>
      <SheetX>1763</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\tuple_right_pressed</Name>
      <Width>554</Width>
      <Height>59</Height>
      <SheetX>2405</SheetX>
      <SheetY>1746</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\tuple_shadow</Name>
      <Width>112</Width>
      <Height>53</Height>
      <SheetX>3976</SheetX>
      <SheetY>1537</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\weaponSlot</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3616</SheetX>
      <SheetY>931</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\weaponSlotHighlight</Name>
      <Width>200</Width>
      <Height>100</Height>
      <SheetX>3616</SheetX>
      <SheetY>1039</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Inventory\WeightIcon</Name>
      <Width>119</Width>
      <Height>119</Height>
      <SheetX>3157</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\army_button_background</Name>
      <Width>95</Width>
      <Height>70</Height>
      <SheetX>3804</SheetX>
      <SheetY>2985</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>MapBar\army_button_front</Name>
      <Width>69</Width>
      <Height>46</Height>
      <SheetX>1175</SheetX>
      <SheetY>4040</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>MapBar\arrow_looking_up</Name>
      <Width>41</Width>
      <Height>34</Height>
      <SheetX>2799</SheetX>
      <SheetY>4055</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>MapBar\bottom_left_button1_exit</Name>
      <Width>34</Width>
      <Height>52</Height>
      <SheetX>4057</SheetX>
      <SheetY>519</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\bottom_left_button2_character</Name>
      <Width>76</Width>
      <Height>52</Height>
      <SheetX>3168</SheetX>
      <SheetY>2945</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\bottom_left_button3_inventory</Name>
      <Width>76</Width>
      <Height>52</Height>
      <SheetX>3084</SheetX>
      <SheetY>2927</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\bottom_left_button4_party</Name>
      <Width>76</Width>
      <Height>52</Height>
      <SheetX>3626</SheetX>
      <SheetY>2835</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>MapBar\bottom_left_button5_quest</Name>
      <Width>76</Width>
      <Height>52</Height>
      <SheetX>2326</SheetX>
      <SheetY>3001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\bottom_left_button6_clan</Name>
      <Width>76</Width>
      <Height>52</Height>
      <SheetX>3252</SheetX>
      <SheetY>2993</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\bottom_left_button7_kingdom</Name>
      <Width>90</Width>
      <Height>52</Height>
      <SheetX>3183</SheetX>
      <SheetY>2589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>MapBar\gather_army_icon</Name>
      <Width>39</Width>
      <Height>35</Height>
      <SheetX>2369</SheetX>
      <SheetY>2646</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>MapBar\mapbar_banner_choose_update</Name>
      <Width>50</Width>
      <Height>108</Height>
      <SheetX>4039</SheetX>
      <SheetY>2092</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_circle_canvas</Name>
      <Width>78</Width>
      <Height>79</Height>
      <SheetX>3538</SheetX>
      <SheetY>2945</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_circle_daynight</Name>
      <Width>190</Width>
      <Height>52</Height>
      <SheetX>3898</SheetX>
      <SheetY>1751</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_circle_frame</Name>
      <Width>75</Width>
      <Height>75</Height>
      <SheetX>3269</SheetX>
      <SheetY>2910</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_circle_glow_overlay</Name>
      <Width>78</Width>
      <Height>79</Height>
      <SheetX>3892</SheetX>
      <SheetY>3074</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_frame</Name>
      <Width>430</Width>
      <Height>59</Height>
      <SheetX>3616</SheetX>
      <SheetY>756</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_center_frame_silhouette</Name>
      <Width>430</Width>
      <Height>67</Height>
      <SheetX>3616</SheetX>
      <SheetY>681</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>MapBar\mapbar_icon1</Name>
      <Width>13</Width>
      <Height>35</Height>
      <SheetX>4078</SheetX>
      <SheetY>214</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>MapBar\mapbar_icon2</Name>
      <Width>32</Width>
      <Height>42</Height>
      <SheetX>4057</SheetX>
      <SheetY>628</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>MapBar\mapbar_icon3</Name>
      <Width>37</Width>
      <Height>42</Height>
      <SheetX>2369</SheetX>
      <SheetY>2278</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>MapBar\mapbar_icon4</Name>
      <Width>50</Width>
      <Height>45</Height>
      <SheetX>4037</SheetX>
      <SheetY>4043</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>MapBar\mapbar_icon5</Name>
      <Width>37</Width>
      <Height>38</Height>
      <SheetX>2369</SheetX>
      <SheetY>2515</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>MapBar\mapbar_icon6</Name>
      <Width>41</Width>
      <Height>34</Height>
      <SheetX>2369</SheetX>
      <SheetY>2561</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_icon7</Name>
      <Width>44</Width>
      <Height>42</Height>
      <SheetX>4048</SheetX>
      <SheetY>2286</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_left_canvas</Name>
      <Width>497</Width>
      <Height>55</Height>
      <SheetX>2898</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_left_frame</Name>
      <Width>554</Width>
      <Height>77</Height>
      <SheetX>597</SheetX>
      <SheetY>1522</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_left_frame_silhouette</Name>
      <Width>532</Width>
      <Height>57</Height>
      <SheetX>3541</SheetX>
      <SheetY>2129</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_right_frame</Name>
      <Width>541</Width>
      <Height>99</Height>
      <SheetX>3515</SheetX>
      <SheetY>2022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\mapbar_right_frame_silhouette</Name>
      <Width>541</Width>
      <Height>99</Height>
      <SheetX>2966</SheetX>
      <SheetY>2022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>MapBar\monocular_icon</Name>
      <Width>45</Width>
      <Height>44</Height>
      <SheetX>3092</SheetX>
      <SheetY>2707</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\melee_unit_troop_card_default</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>3982</SheetX>
      <SheetY>726</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\melee_unit_troop_card_selected</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>3982</SheetX>
      <SheetY>556</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\melee_unit_troop_card_transfer_selected</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>3978</SheetX>
      <SheetY>1374</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Order\MinimalOrderTroop</Name>
      <Width>146</Width>
      <Height>218</Height>
      <SheetX>3946</SheetX>
      <SheetY>2909</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>Order\OrderTroopBackground</Name>
      <Width>146</Width>
      <Height>248</Height>
      <SheetX>3946</SheetX>
      <SheetY>2653</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\ranged_unit_troop_card_default</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>3982</SheetX>
      <SheetY>386</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\ranged_unit_troop_card_selected</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>3982</SheetX>
      <SheetY>216</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Order\ranged_unit_troop_card_transfer_selected</Name>
      <Width>106</Width>
      <Height>162</Height>
      <SheetX>916</SheetX>
      <SheetY>3802</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Order\SiegeItemBackground</Name>
      <Width>146</Width>
      <Height>146</Height>
      <SheetX>3690</SheetX>
      <SheetY>3330</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>PartyScreen\button_collapser</Name>
      <Width>686</Width>
      <Height>140</Height>
      <SheetX>4</SheetX>
      <SheetY>3495</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>PartyScreen\button_collapser_hover</Name>
      <Width>686</Width>
      <Height>140</Height>
      <SheetX>4</SheetX>
      <SheetY>3643</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>PartyScreen\button_collapser_pressed</Name>
      <Width>686</Width>
      <Height>140</Height>
      <SheetX>1662</SheetX>
      <SheetY>2853</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_left</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>1274</SheetX>
      <SheetY>2213</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>PartyScreen\button_left_hover</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>3262</SheetX>
      <SheetY>4022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_left_pressed</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>639</SheetX>
      <SheetY>2144</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_left_selected</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>4</SheetX>
      <SheetY>2144</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_right</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>1909</SheetX>
      <SheetY>2217</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_right_hover</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>2544</SheetX>
      <SheetY>2217</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_right_pressed</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>3179</SheetX>
      <SheetY>2217</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\button_right_selected</Name>
      <Width>627</Width>
      <Height>66</Height>
      <SheetX>4</SheetX>
      <SheetY>2218</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>PartyScreen\divider</Name>
      <Width>93</Width>
      <Height>24</Height>
      <SheetX>3897</SheetX>
      <SheetY>4067</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\header_left</Name>
      <Width>653</Width>
      <Height>104</Height>
      <SheetX>1850</SheetX>
      <SheetY>1607</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\header_right</Name>
      <Width>653</Width>
      <Height>104</Height>
      <SheetX>1189</SheetX>
      <SheetY>1499</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>PartyScreen\portrait</Name>
      <Width>155</Width>
      <Height>81</Height>
      <SheetX>2719</SheetX>
      <SheetY>2026</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>PartyScreen\recruit_prisoner</Name>
      <Width>32</Width>
      <Height>31</Height>
      <SheetX>1888</SheetX>
      <SheetY>3385</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>PartyScreen\talk_icon</Name>
      <Width>39</Width>
      <Height>35</Height>
      <SheetX>2369</SheetX>
      <SheetY>2689</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>PartyScreen\upgrade_icon</Name>
      <Width>45</Width>
      <Height>37</Height>
      <SheetX>4029</SheetX>
      <SheetY>1253</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>PartyScreen\upgrade_troops_popup_tuple</Name>
      <Width>752</Width>
      <Height>66</Height>
      <SheetX>1888</SheetX>
      <SheetY>4022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Quest\background_quest</Name>
      <Width>600</Width>
      <Height>700</Height>
      <SheetX>4</SheetX>
      <SheetY>3211</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>Quest\big_canvas</Name>
      <Width>550</Width>
      <Height>1014</Height>
      <SheetX>1640</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>Quest\header left</Name>
      <Width>606</Width>
      <Height>69</Height>
      <SheetX>2648</SheetX>
      <SheetY>4022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>Quest\questlist_header</Name>
      <Width>496</Width>
      <Height>68</Height>
      <SheetX>1036</SheetX>
      <SheetY>2985</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Quest\quest_collapser_default</Name>
      <Width>585</Width>
      <Height>96</Height>
      <SheetX>1812</SheetX>
      <SheetY>1746</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>Quest\quest_screen_tuple_default</Name>
      <Width>592</Width>
      <Height>91</Height>
      <SheetX>2966</SheetX>
      <SheetY>1923</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SaveLoad\flag_base</Name>
      <Width>45</Width>
      <Height>45</Height>
      <SheetX>2623</SheetX>
      <SheetY>2445</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_fiefs</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3359</SheetX>
      <SheetY>2703</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_food</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3142</SheetX>
      <SheetY>3005</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_gold</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3258</SheetX>
      <SheetY>3053</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_influence</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3084</SheetX>
      <SheetY>2987</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_level</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3107</SheetX>
      <SheetY>2868</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\icon_party</Name>
      <Width>50</Width>
      <Height>50</Height>
      <SheetX>3284</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SaveLoad\load_button_canvas</Name>
      <Width>384</Width>
      <Height>64</Height>
      <SheetX>3684</SheetX>
      <SheetY>1302</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\load_collapser</Name>
      <Width>585</Width>
      <Height>96</Height>
      <SheetX>2998</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\load_tuple</Name>
      <Width>569</Width>
      <Height>95</Height>
      <SheetX>2389</SheetX>
      <SheetY>1923</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SaveLoad\load_tuple_selected</Name>
      <Width>569</Width>
      <Height>95</Height>
      <SheetX>1812</SheetX>
      <SheetY>1850</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SaveLoad\main_hero_save_silhouette</Name>
      <Width>478</Width>
      <Height>666</Height>
      <SheetX>3604</SheetX>
      <SheetY>1871</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SpecialResources\taom_caster_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2418</SheetX>
      <SheetY>2029</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SpecialResources\taom_dunlending_ale_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2179</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SpecialResources\taom_elven_wine_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>3061</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SpecialResources\taom_gems_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>3061</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_lake_fish_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_marks_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_plunder_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_tribal_relics_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_war_banners_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_war_drums_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SpecialResources\taom_war_spoils_icon</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\ambush_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3892</SheetX>
      <SheetY>3262</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\assault_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>4020</SheetX>
      <SheetY>3220</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\besiege_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3892</SheetX>
      <SheetY>3212</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\craft_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3956</SheetX>
      <SheetY>3220</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\defend_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3956</SheetX>
      <SheetY>3270</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\escape_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>4027</SheetX>
      <SheetY>2405</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\hostileaction_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>4020</SheetX>
      <SheetY>3270</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\keep_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3700</SheetX>
      <SheetY>3212</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\leave_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3636</SheetX>
      <SheetY>3177</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\raid_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3572</SheetX>
      <SheetY>3177</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\recruit_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3444</SheetX>
      <SheetY>3134</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\retreat_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3764</SheetX>
      <SheetY>3212</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\submenu_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3380</SheetX>
      <SheetY>3103</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\trade_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3316</SheetX>
      <SheetY>3103</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\wait_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3508</SheetX>
      <SheetY>3134</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPGeneral\GameMenu\warehouse_icon</Name>
      <Width>56</Width>
      <Height>42</Height>
      <SheetX>3828</SheetX>
      <SheetY>3220</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\drop_shadow_below_header</Name>
      <Width>231</Width>
      <Height>39</Height>
      <SheetX>3183</SheetX>
      <SheetY>2656</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\edit_name_icon</Name>
      <Width>29</Width>
      <Height>37</Height>
      <SheetX>3897</SheetX>
      <SheetY>4022</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\header_policies</Name>
      <Width>601</Width>
      <Height>98</Height>
      <SheetX>2999</SheetX>
      <SheetY>1607</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\influence_icon_small</Name>
      <Width>17</Width>
      <Height>27</Height>
      <SheetX>1888</SheetX>
      <SheetY>3575</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SPKingdom\kingdom_collapser</Name>
      <Width>949</Width>
      <Height>112</Height>
      <SheetX>1842</SheetX>
      <SheetY>3956</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\kingdom_header_button_left_selected</Name>
      <Width>282</Width>
      <Height>56</Height>
      <SheetX>3608</SheetX>
      <SheetY>1751</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\kingdom_top_header</Name>
      <Width>1920</Width>
      <Height>196</Height>
      <SheetX>1932</SheetX>
      <SheetY>1152</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\kingdom_tuple</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>1246</SheetX>
      <SheetY>1611</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SPKingdom\king_seal</Name>
      <Width>232</Width>
      <Height>232</Height>
      <SheetX>3855</SheetX>
      <SheetY>2413</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\policy_active_tuple_default</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>4</SheetX>
      <SheetY>1508</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\policy_collapser_default</Name>
      <Width>585</Width>
      <Height>96</Height>
      <SheetX>1219</SheetX>
      <SheetY>1819</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\portrait_slot_square</Name>
      <Width>82</Width>
      <Height>81</Height>
      <SheetX>3907</SheetX>
      <SheetY>2985</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\portrait_slot_wide</Name>
      <Width>112</Width>
      <Height>81</Height>
      <SheetX>2693</SheetX>
      <SheetY>2567</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\voter_header_bottom</Name>
      <Width>623</Width>
      <Height>144</Height>
      <SheetX>4</SheetX>
      <SheetY>1356</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Armies\header_armies_1</Name>
      <Width>137</Width>
      <Height>98</Height>
      <SheetX>295</SheetX>
      <SheetY>2656</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Armies\header_armies_2</Name>
      <Width>109</Width>
      <Height>98</Height>
      <SheetX>3978</SheetX>
      <SheetY>2671</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Armies\header_armies_3</Name>
      <Width>74</Width>
      <Height>98</Height>
      <SheetX>3456</SheetX>
      <SheetY>2895</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Armies\header_armies_4</Name>
      <Width>80</Width>
      <Height>98</Height>
      <SheetX>3282</SheetX>
      <SheetY>2804</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Armies\header_armies_5</Name>
      <Width>201</Width>
      <Height>98</Height>
      <SheetX>3573</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Clans\header_clans_1</Name>
      <Width>94</Width>
      <Height>98</Height>
      <SheetX>3991</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SPKingdom\Clans\header_clans_2</Name>
      <Width>69</Width>
      <Height>98</Height>
      <SheetX>4020</SheetX>
      <SheetY>3177</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Clans\header_clans_3</Name>
      <Width>135</Width>
      <Height>98</Height>
      <SheetX>440</SheetX>
      <SheetY>2589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Clans\header_clans_4</Name>
      <Width>108</Width>
      <Height>98</Height>
      <SheetX>3976</SheetX>
      <SheetY>1431</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Clans\header_clans_5</Name>
      <Width>110</Width>
      <Height>98</Height>
      <SheetX>3981</SheetX>
      <SheetY>896</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Clans\header_clans_6</Name>
      <Width>85</Width>
      <Height>98</Height>
      <SheetX>4003</SheetX>
      <SheetY>1696</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\ClanTypes\leader_clan</Name>
      <Width>66</Width>
      <Height>38</Height>
      <SheetX>863</SheetX>
      <SheetY>3446</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SPKingdom\ClanTypes\mercenary_clan</Name>
      <Width>66</Width>
      <Height>38</Height>
      <SheetX>3855</SheetX>
      <SheetY>3177</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\ClanTypes\normal_clan</Name>
      <Width>66</Width>
      <Height>38</Height>
      <SheetX>937</SheetX>
      <SheetY>3446</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SPKingdom\Decision\decision_popup_bg</Name>
      <Width>1830</Width>
      <Height>911</Height>
      <SheetX>4</SheetX>
      <SheetY>3162</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SPKingdom\Decision\decision_popup_divider</Name>
      <Width>55</Width>
      <Height>948</Height>
      <SheetX>4032</SheetX>
      <SheetY>1983</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Decision\namestone</Name>
      <Width>436</Width>
      <Height>88</Height>
      <SheetX>568</SheetX>
      <SheetY>3244</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Decision\option</Name>
      <Width>478</Width>
      <Height>607</Height>
      <SheetX>2718</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Decision\option_selected</Name>
      <Width>478</Width>
      <Height>607</Height>
      <SheetX>3204</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>SPKingdom\Decision\outer_frame</Name>
      <Width>1894</Width>
      <Height>974</Height>
      <SheetX>4</SheetX>
      <SheetY>2180</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Decision\progress_bar_base</Name>
      <Width>266</Width>
      <Height>67</Height>
      <SheetX>3822</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Decision\progress_bar_fill_positive</Name>
      <Width>203</Width>
      <Height>20</Height>
      <SheetX>1932</SheetX>
      <SheetY>2692</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Decision\progress_bar_frame</Name>
      <Width>266</Width>
      <Height>67</Height>
      <SheetX>3573</SheetX>
      <SheetY>2565</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Decision\progress_bar_indicator</Name>
      <Width>8</Width>
      <Height>23</Height>
      <SheetX>4084</SheetX>
      <SheetY>4067</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Decision\relation_arrow</Name>
      <Width>16</Width>
      <Height>21</Height>
      <SheetX>1888</SheetX>
      <SheetY>3672</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Diplomacy\bar_seperator</Name>
      <Width>2</Width>
      <Height>83</Height>
      <SheetX>4090</SheetX>
      <SheetY>1871</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Diplomacy\diplomacy_peace_icon</Name>
      <Width>63</Width>
      <Height>63</Height>
      <SheetX>4025</SheetX>
      <SheetY>3808</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SPKingdom\Diplomacy\diplomacy_war_icon</Name>
      <Width>63</Width>
      <Height>63</Height>
      <SheetX>4020</SheetX>
      <SheetY>3641</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Diplomacy\divider_left</Name>
      <Width>267</Width>
      <Height>3</Height>
      <SheetX>2395</SheetX>
      <SheetY>2699</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SPKingdom\Diplomacy\paying_tribute</Name>
      <Width>39</Width>
      <Height>42</Height>
      <SheetX>2369</SheetX>
      <SheetY>2228</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>SPKingdom\Diplomacy\receiving_tribute</Name>
      <Width>38</Width>
      <Height>31</Height>
      <SheetX>2369</SheetX>
      <SheetY>2732</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>SPKingdom\Diplomacy\stats_compare_icon</Name>
      <Width>34</Width>
      <Height>29</Height>
      <SheetX>1888</SheetX>
      <SheetY>3424</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Diplomacy\wars_at_peace_tuple</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>4</SheetX>
      <SheetY>1603</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Diplomacy\wars_at_war_tuple</Name>
      <Width>585</Width>
      <Height>87</Height>
      <SheetX>4</SheetX>
      <SheetY>1946</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Fiefs\header_fiefs_1</Name>
      <Width>84</Width>
      <Height>98</Height>
      <SheetX>3092</SheetX>
      <SheetY>2762</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Fiefs\header_fiefs_2</Name>
      <Width>201</Width>
      <Height>98</Height>
      <SheetX>3782</SheetX>
      <SheetY>2459</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>SPKingdom\Fiefs\header_fiefs_3</Name>
      <Width>112</Width>
      <Height>98</Height>
      <SheetX>604</SheetX>
      <SheetY>3791</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Fiefs\header_fiefs_4</Name>
      <Width>78</Width>
      <Height>98</Height>
      <SheetX>3370</SheetX>
      <SheetY>2804</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>SPKingdom\Fiefs\header_fiefs_5</Name>
      <Width>126</Width>
      <Height>98</Height>
      <SheetX>1159</SheetX>
      <SheetY>2462</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\arrow_large_pointing_left</Name>
      <Width>54</Width>
      <Height>70</Height>
      <SheetX>4032</SheetX>
      <SheetY>2939</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>StdAssets\arrow_little</Name>
      <Width>30</Width>
      <Height>41</Height>
      <SheetX>4057</SheetX>
      <SheetY>866</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>StdAssets\arrow_little_disabled</Name>
      <Width>30</Width>
      <Height>41</Height>
      <SheetX>4057</SheetX>
      <SheetY>915</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>StdAssets\arrow_little_thiqq</Name>
      <Width>42</Width>
      <Height>42</Height>
      <SheetX>2799</SheetX>
      <SheetY>3956</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>StdAssets\arrow_pointing_left</Name>
      <Width>30</Width>
      <Height>41</Height>
      <SheetX>4057</SheetX>
      <SheetY>964</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\arrow_thiqq_large_pointing_left</Name>
      <Width>67</Width>
      <Height>63</Height>
      <SheetX>4020</SheetX>
      <SheetY>3283</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>StdAssets\arrow_thiqq_pointing_left</Name>
      <Width>52</Width>
      <Height>52</Height>
      <SheetX>4039</SheetX>
      <SheetY>2269</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\banner_flat</Name>
      <Width>136</Width>
      <Height>173</Height>
      <SheetX>3950</SheetX>
      <SheetY>418</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>StdAssets\banner_flat_small</Name>
      <Width>68</Width>
      <Height>107</Height>
      <SheetX>3860</SheetX>
      <SheetY>3589</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\banner_new</Name>
      <Width>103</Width>
      <Height>144</Height>
      <SheetX>3963</SheetX>
      <SheetY>1544</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\banner_tuple</Name>
      <Width>136</Width>
      <Height>173</Height>
      <SheetX>3822</SheetX>
      <SheetY>216</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\close_button</Name>
      <Width>82</Width>
      <Height>82</Height>
      <SheetX>3840</SheetX>
      <SheetY>2895</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\close_button_hover</Name>
      <Width>82</Width>
      <Height>82</Height>
      <SheetX>3930</SheetX>
      <SheetY>2895</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\close_button_pressed</Name>
      <Width>82</Width>
      <Height>82</Height>
      <SheetX>3714</SheetX>
      <SheetY>2985</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\delete_button_hovered</Name>
      <Width>251</Width>
      <Height>60</Height>
      <SheetX>1274</SheetX>
      <SheetY>2144</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\delete_button_regular</Name>
      <Width>251</Width>
      <Height>60</Height>
      <SheetX>639</SheetX>
      <SheetY>2218</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_done</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>3684</SheetX>
      <SheetY>1464</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_done_below</Name>
      <Width>337</Width>
      <Height>75</Height>
      <SheetX>3684</SheetX>
      <SheetY>1136</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_done_below_hover</Name>
      <Width>337</Width>
      <Height>75</Height>
      <SheetX>3684</SheetX>
      <SheetY>1219</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_done_hover</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>3684</SheetX>
      <SheetY>1648</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_regular</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3495</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_regular_below</Name>
      <Width>337</Width>
      <Height>75</Height>
      <SheetX>3690</SheetX>
      <SheetY>3601</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_regular_below_hover</Name>
      <Width>337</Width>
      <Height>75</Height>
      <SheetX>3690</SheetX>
      <SheetY>3518</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\main_button_regular_hover</Name>
      <Width>271</Width>
      <Height>84</Height>
      <SheetX>725</SheetX>
      <SheetY>3587</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\page_button_center</Name>
      <Width>283</Width>
      <Height>53</Height>
      <SheetX>898</SheetX>
      <SheetY>2218</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\page_button_center_selected</Name>
      <Width>283</Width>
      <Height>53</Height>
      <SheetX>4</SheetX>
      <SheetY>2640</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\page_button_left</Name>
      <Width>311</Width>
      <Height>62</Height>
      <SheetX>3704</SheetX>
      <SheetY>1361</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\page_button_left_selected</Name>
      <Width>311</Width>
      <Height>62</Height>
      <SheetX>3684</SheetX>
      <SheetY>1740</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\standart_popup</Name>
      <Width>1500</Width>
      <Height>960</Height>
      <SheetX>1512</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\standart_popup_button</Name>
      <Width>286</Width>
      <Height>82</Height>
      <SheetX>3786</SheetX>
      <SheetY>4001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\standart_popup_button_hover</Name>
      <Width>286</Width>
      <Height>82</Height>
      <SheetX>3684</SheetX>
      <SheetY>1374</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\tabbar_long</Name>
      <Width>887</Width>
      <Height>150</Height>
      <SheetX>3100</SheetX>
      <SheetY>3933</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\tabbar_long_namebox</Name>
      <Width>434</Width>
      <Height>47</Height>
      <SheetX>3100</SheetX>
      <SheetY>2012</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\tabbar_popup</Name>
      <Width>592</Width>
      <Height>161</Height>
      <SheetX>4</SheetX>
      <SheetY>3791</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\tabbar_standart</Name>
      <Width>755</Width>
      <Height>182</Height>
      <SheetX>2198</SheetX>
      <SheetY>3874</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\top_header</Name>
      <Width>1920</Width>
      <Height>196</Height>
      <SheetX>4</SheetX>
      <SheetY>1152</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\triple_button_frame</Name>
      <Width>713</Width>
      <Height>126</Height>
      <SheetX>4</SheetX>
      <SheetY>3960</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\triple_popup_button_frame</Name>
      <Width>614</Width>
      <Height>93</Height>
      <SheetX>4</SheetX>
      <SheetY>1746</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\wide_rectangle_button_default</Name>
      <Width>269</Width>
      <Height>41</Height>
      <SheetX>725</SheetX>
      <SheetY>4047</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\wide_rectangle_button_hovered</Name>
      <Width>269</Width>
      <Height>41</Height>
      <SheetX>3819</SheetX>
      <SheetY>2012</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\wide_rectangle_button_pressed</Name>
      <Width>269</Width>
      <Height>41</Height>
      <SheetX>3542</SheetX>
      <SheetY>2012</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\database\armours</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>2452</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\database\background-watermark</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>1870</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\database\background</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>1288</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\database\border</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>3450</SheetX>
      <SheetY>2029</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\database\lords</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>3034</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\database\troops</Name>
      <Width>574</Width>
      <Height>1140</Height>
      <SheetX>706</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\Popup\button_default</Name>
      <Width>251</Width>
      <Height>64</Height>
      <SheetX>1847</SheetX>
      <SheetY>1953</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\Popup\button_default_hover</Name>
      <Width>251</Width>
      <Height>64</Height>
      <SheetX>2741</SheetX>
      <SheetY>2354</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>StdAssets\Popup\canvas</Name>
      <Width>512</Width>
      <Height>645</Height>
      <SheetX>2198</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\Popup\canvas_dark</Name>
      <Width>699</Width>
      <Height>666</Height>
      <SheetX>1662</SheetX>
      <SheetY>2179</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>StdAssets\Popup\canvas_gradient</Name>
      <Width>512</Width>
      <Height>645</Height>
      <SheetX>3556</SheetX>
      <SheetY>810</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\Popup\done_button</Name>
      <Width>251</Width>
      <Height>64</Height>
      <SheetX>2106</SheetX>
      <SheetY>1953</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>StdAssets\Popup\done_button_hover</Name>
      <Width>251</Width>
      <Height>64</Height>
      <SheetX>2460</SheetX>
      <SheetY>2026</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>StdAssets\Popup\order_popup_canvas</Name>
      <Width>1245</Width>
      <Height>652</Height>
      <SheetX>4</SheetX>
      <SheetY>3260</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>TAOM\CharacterScreen\menuoptions_border</Name>
      <Width>523</Width>
      <Height>52</Height>
      <SheetX>1540</SheetX>
      <SheetY>3001</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>TAOM\CharacterScreen\menuoptions_mask</Name>
      <Width>523</Width>
      <Height>52</Height>
      <SheetX>3403</SheetX>
      <SheetY>2291</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>TAOM\Encyclopedia\banner</Name>
      <Width>596</Width>
      <Height>973</Height>
      <SheetX>1036</SheetX>
      <SheetY>3076</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>TAOM\Encyclopedia\clan_diamond_border</Name>
      <Width>1199</Width>
      <Height>1199</Height>
      <SheetX>1211</SheetX>
      <SheetY>972</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>TAOM\Encyclopedia\clan_diamond_mask</Name>
      <Width>1000</Width>
      <Height>1000</Height>
      <SheetX>2044</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>TAOM\Encyclopedia\fief_square_border</Name>
      <Width>1000</Width>
      <Height>1000</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>TAOM\Encyclopedia\home_rectangle_border</Name>
      <Width>916</Width>
      <Height>2708</Height>
      <SheetX>2680</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>TAOM\Encyclopedia\lord_rectangle_border</Name>
      <Width>1000</Width>
      <Height>716</Height>
      <SheetX>1842</SheetX>
      <SheetY>3232</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>TAOM\GameMenu\game_menu_frame_border</Name>
      <Width>623</Width>
      <Height>811</Height>
      <SheetX>1257</SheetX>
      <SheetY>3260</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>TAOM\GameMenu\game_menu_frame_canvas</Name>
      <Width>618</Width>
      <Height>798</Height>
      <SheetX>1036</SheetX>
      <SheetY>2179</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>TAOM\GameMenu\game_menu_inner_panel</Name>
      <Width>13</Width>
      <Height>297</Height>
      <SheetX>4057</SheetX>
      <SheetY>214</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>TAOM\Nameplates\player_ring_icon</Name>
      <Width>1130</Width>
      <Height>1049</Height>
      <SheetX>2418</SheetX>
      <SheetY>972</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>TAOM\Nameplates\settlement_banner_large</Name>
      <Width>1000</Width>
      <Height>1000</Height>
      <SheetX>3052</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>TAOM\Nameplates\settlement_banner_large_background</Name>
      <Width>1199</Width>
      <Height>1199</Height>
      <SheetX>4</SheetX>
      <SheetY>972</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>TAOM\Nameplates\settlement_nameplate_background</Name>
      <Width>1136</Width>
      <Height>175</Height>
      <SheetX>2718</SheetX>
      <SheetY>3691</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>TAOM\Nameplates\settlement_nameplate_large</Name>
      <Width>616</Width>
      <Height>76</Height>
      <SheetX>1783</SheetX>
      <SheetY>2129</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>TAOM\SPScoreboard\background_overlay</Name>
      <Width>2668</Width>
      <Height>2160</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>TAOM\SPScoreboard\divider_nameplate</Name>
      <Width>1192</Width>
      <Height>112</Height>
      <SheetX>4</SheetX>
      <SheetY>3920</SheetY>
      <CategoryName>ui_taom</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>10000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>10001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>10002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>17</SheetID>
      <Name>10011</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10012</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>10013</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10014</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10015</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10016</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10017</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10018</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10019</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10020</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>10021</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>10022</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>10023</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>10024</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>10025</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>18</SheetID>
      <Name>10026</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>10027</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10028</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>10029</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>10030</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>10500</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10501</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10502</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10503</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10504</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10505</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>10506</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>11000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>14</SheetID>
      <Name>11001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>11002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>11003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>11004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>11005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>11006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>12000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>12001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>12002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>12003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>15</SheetID>
      <Name>12004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>12005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>12006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>13000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>16</SheetID>
      <Name>13001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>13002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>13003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>13004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>23</SheetID>
      <Name>14008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14011</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14012</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14013</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14014</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14015</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14500</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>24</SheetID>
      <Name>14501</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>14502</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>14503</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>15000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>15001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>15002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>15003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>15004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>15005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>15006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>15007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>15008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>19</SheetID>
      <Name>15009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>20</SheetID>
      <Name>16008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>16009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>17001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>21</SheetID>
      <Name>17007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>17008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>18000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>18001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>18002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>22</SheetID>
      <Name>18003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>19000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>19001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>19002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>19003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>19010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>19011</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>19012</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>19013</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>19014</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>19015</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>20000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>20001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>20002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>20003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>20004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>20005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>20006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>21000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>21001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>21002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>21003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>6</SheetID>
      <Name>21004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>21005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>5</SheetID>
      <Name>21006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>4</SheetID>
      <Name>21007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>21008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>21009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>21010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>22000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>22001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>22002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>22003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>22004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>23000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>23001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>23002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>23003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>24000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>24002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>24003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>24004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>24005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24011</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24012</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>3</SheetID>
      <Name>24013</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>24014</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>24015</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>24016</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>24017</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>24018</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>24019</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>24020</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>24021</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24022</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24023</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24024</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24025</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24026</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24027</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24028</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24029</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>11</SheetID>
      <Name>24030</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24500</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24501</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24502</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24503</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24504</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24505</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24506</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24507</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>12</SheetID>
      <Name>24508</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>24509</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>24510</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>25000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>25001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>25002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>7</SheetID>
      <Name>25009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25011</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25012</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25013</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25014</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>25015</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>10</SheetID>
      <Name>26000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>26001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>8</SheetID>
      <Name>26002</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26003</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26004</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26005</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26006</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26007</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26008</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26009</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>26010</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>1036</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>9</SheetID>
      <Name>27000</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>13</SheetID>
      <Name>27001</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>4</SheetX>
      <SheetY>1036</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>25</SheetID>
      <Name>27500</Name>
      <Width>1024</Width>
      <Height>1024</Height>
      <SheetX>2068</SheetX>
      <SheetY>2068</SheetY>
      <CategoryName>ui_taom_bannericons</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\career_button_placeholder</Name>
      <Width>1024</Width>
      <Height>434</Height>
      <SheetX>2060</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\locked_gate_bottom</Name>
      <Width>2048</Width>
      <Height>512</Height>
      <SheetX>4</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\locked_gate_full</Name>
      <Width>2048</Width>
      <Height>512</Height>
      <SheetX>4</SheetX>
      <SheetY>524</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\locked_gate_top</Name>
      <Width>2048</Width>
      <Height>512</Height>
      <SheetX>4</SheetX>
      <SheetY>1044</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\minus_sign_icon</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>2428</SheetX>
      <SheetY>1670</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\plus_sign_icon</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3764</SheetX>
      <SheetY>3124</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CareerSystem\career_point_pip</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>0</SheetX>
      <SheetY>0</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\captain_of_osgiliath_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3500</SheetX>
      <SheetY>3124</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\crossbow_master_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3236</SheetX>
      <SheetY>3124</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\eotheod_windrider_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3764</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\ironguard_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3500</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\knight_of_belfalas_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3236</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\marksman_of_aldburg_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3676</SheetX>
      <SheetY>1612</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\ram_rider_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3676</SheetX>
      <SheetY>1348</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\ranger_of_ithilien_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3676</SheetX>
      <SheetY>1084</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\watchman_of_stangard_ability</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3676</SheetX>
      <SheetY>820</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_balchoth_kan_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>812</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_black_uruk_captain_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>4</SheetX>
      <SheetY>2486</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_blade_dancer_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>3236</SheetX>
      <SheetY>2452</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_captain_of_osgiliath_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>1620</SheetX>
      <SheetY>2486</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_cave_troll_master_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2428</SheetX>
      <SheetY>2452</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_codyan_legionaire_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2868</SheetX>
      <SheetY>1636</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_elven_archer_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>3092</SheetX>
      <SheetY>4</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_eotheod_windrider_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>3092</SheetX>
      <SheetY>412</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_ironguard_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2060</SheetX>
      <SheetY>446</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_knight_of_belfalas_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2868</SheetX>
      <SheetY>820</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_lokhas_drus_marksman_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2060</SheetX>
      <SheetY>854</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_mahud_beast_rider_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2868</SheetX>
      <SheetY>1228</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_marksman_of_aldburg_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>1620</SheetX>
      <SheetY>2078</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_pezarsani_javelineer_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2060</SheetX>
      <SheetY>1262</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_ranger_of_ithilien_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>4</SheetX>
      <SheetY>1670</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_shadow_warrior_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>812</SheetX>
      <SheetY>2044</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_silvan_archer_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>1620</SheetX>
      <SheetY>1670</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_tribesman_of_jelut_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2428</SheetX>
      <SheetY>2044</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_uruk_berserker_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>3236</SheetX>
      <SheetY>2044</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_uruk_crossbow_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>812</SheetX>
      <SheetY>1564</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Portraits\career_watchman_of_stangard_portrait</Name>
      <Width>800</Width>
      <Height>400</Height>
      <SheetX>2428</SheetX>
      <SheetY>2860</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
  </SpriteParts>
  <Sprites>
    <GenericSprite>
      <Name>aniron</Name>
      <SpritePartName>aniron</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>minionpro</Name>
      <SpritePartName>minionpro</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ringbearer</Name>
      <SpritePartName>ringbearer</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_10</Name>
      <SpritePartName>ui_loading_10</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_2</Name>
      <SpritePartName>ui_loading_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_3</Name>
      <SpritePartName>ui_loading_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_4</Name>
      <SpritePartName>ui_loading_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_5</Name>
      <SpritePartName>ui_loading_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_6</Name>
      <SpritePartName>ui_loading_6</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_7</Name>
      <SpritePartName>ui_loading_7</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_8</Name>
      <SpritePartName>ui_loading_8</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ui_loading_9</Name>
      <SpritePartName>ui_loading_9</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>background_1</Name>
      <SpritePartName>background_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>background_2</Name>
      <SpritePartName>background_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>background_3</Name>
      <SpritePartName>background_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>background_4</Name>
      <SpritePartName>background_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>clan_banner</Name>
      <SpritePartName>clan_banner</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>default_cursor</Name>
      <SpritePartName>default_cursor</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>FastForward@2x</Name>
      <SpritePartName>FastForward@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>FastForward@2x_selected</Name>
      <SpritePartName>FastForward@2x_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>InventoryBackground</Name>
      <SpritePartName>InventoryBackground</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyBackground</Name>
      <SpritePartName>PartyBackground</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Pause@2x</Name>
      <SpritePartName>Pause@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Pause@2x_selected</Name>
      <SpritePartName>Pause@2x_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Play@2x</Name>
      <SpritePartName>Play@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Play@2x_selected</Name>
      <SpritePartName>Play@2x_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoadBackground</Name>
      <SpritePartName>SaveLoadBackground</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\army_card</Name>
      <SpritePartName>ArmyManagement\army_card</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_1</Name>
      <SpritePartName>ArmyManagement\header_gather_army_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_2</Name>
      <SpritePartName>ArmyManagement\header_gather_army_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_3</Name>
      <SpritePartName>ArmyManagement\header_gather_army_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_4</Name>
      <SpritePartName>ArmyManagement\header_gather_army_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_5</Name>
      <SpritePartName>ArmyManagement\header_gather_army_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\header_gather_army_6</Name>
      <SpritePartName>ArmyManagement\header_gather_army_6</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\panel_tabbar</Name>
      <SpritePartName>ArmyManagement\panel_tabbar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\voter_tuple</Name>
      <SpritePartName>ArmyManagement\voter_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>ArmyManagement\voter_tuple_selected</Name>
      <SpritePartName>ArmyManagement\voter_tuple_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_collapser</Name>
      <SpritePartName>Barter\barter_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_collapser_hover</Name>
      <SpritePartName>Barter\barter_collapser_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_collapser_pressed</Name>
      <SpritePartName>Barter\barter_collapser_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_header</Name>
      <SpritePartName>Barter\barter_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_item_tuple</Name>
      <SpritePartName>Barter\barter_item_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_item_tuple_hover</Name>
      <SpritePartName>Barter\barter_item_tuple_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_item_tuple_pressed</Name>
      <SpritePartName>Barter\barter_item_tuple_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_item_tuple_selected</Name>
      <SpritePartName>Barter\barter_item_tuple_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_panel</Name>
      <SpritePartName>Barter\barter_panel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\barter_portrait</Name>
      <SpritePartName>Barter\barter_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\deal_button</Name>
      <SpritePartName>Barter\deal_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\deal_button_enabled</Name>
      <SpritePartName>Barter\deal_button_enabled</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\Gold</Name>
      <SpritePartName>Barter\Gold</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\header_stone</Name>
      <SpritePartName>Barter\header_stone</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\Peace</Name>
      <SpritePartName>Barter\Peace</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Barter\War</Name>
      <SpritePartName>Barter\War</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\add_attribute_point_button</Name>
      <SpritePartName>CharacterDeveloper\add_attribute_point_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\add_focus</Name>
      <SpritePartName>CharacterDeveloper\add_focus</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\add_focus_button_circle</Name>
      <SpritePartName>CharacterDeveloper\add_focus_button_circle</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\add_focus_button_glow</Name>
      <SpritePartName>CharacterDeveloper\add_focus_button_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\add_focus_hover</Name>
      <SpritePartName>CharacterDeveloper\add_focus_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\attribute_point_icon</Name>
      <SpritePartName>CharacterDeveloper\attribute_point_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_developer_switcher</Name>
      <SpritePartName>CharacterDeveloper\character_developer_switcher</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_perk_background</Name>
      <SpritePartName>CharacterDeveloper\character_perk_background</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_perk_background_active</Name>
      <SpritePartName>CharacterDeveloper\character_perk_background_active</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_progress_bar</Name>
      <SpritePartName>CharacterDeveloper\character_progress_bar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_progress_bar_glow</Name>
      <SpritePartName>CharacterDeveloper\character_progress_bar_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_progress_frame</Name>
      <SpritePartName>CharacterDeveloper\character_progress_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\character_progress_panel</Name>
      <SpritePartName>CharacterDeveloper\character_progress_panel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\chooser_arrow</Name>
      <SpritePartName>CharacterDeveloper\chooser_arrow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\cp_icon</Name>
      <SpritePartName>CharacterDeveloper\cp_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_big_empty</Name>
      <SpritePartName>CharacterDeveloper\focus_point_big_empty</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_big_filled</Name>
      <SpritePartName>CharacterDeveloper\focus_point_big_filled</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_empty</Name>
      <SpritePartName>CharacterDeveloper\focus_point_empty</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_filled</Name>
      <SpritePartName>CharacterDeveloper\focus_point_filled</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_large_empty</Name>
      <SpritePartName>CharacterDeveloper\focus_point_large_empty</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_large_full</Name>
      <SpritePartName>CharacterDeveloper\focus_point_large_full</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_1</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_10</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_10</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_2</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_3</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_4</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_5</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_6</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_6</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_7</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_7</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_8</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_8</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\focus_point_small_9</Name>
      <SpritePartName>CharacterDeveloper\focus_point_small_9</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\how_to_learn</Name>
      <SpritePartName>CharacterDeveloper\how_to_learn</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\indicator</Name>
      <SpritePartName>CharacterDeveloper\indicator</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\learning_rate_limit_triangle</Name>
      <SpritePartName>CharacterDeveloper\learning_rate_limit_triangle</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\left_panel_coverup</Name>
      <SpritePartName>CharacterDeveloper\left_panel_coverup</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\left_panel_stone</Name>
      <SpritePartName>CharacterDeveloper\left_panel_stone</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\little_progressbar_fill</Name>
      <SpritePartName>CharacterDeveloper\little_progressbar_fill</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\little_progressbar_frame</Name>
      <SpritePartName>CharacterDeveloper\little_progressbar_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\little_progressbar_glow</Name>
      <SpritePartName>CharacterDeveloper\little_progressbar_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\notification</Name>
      <SpritePartName>CharacterDeveloper\notification</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\perk_bg</Name>
      <SpritePartName>CharacterDeveloper\perk_bg</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\perk_frame</Name>
      <SpritePartName>CharacterDeveloper\perk_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\perk_selection_popup_arrow</Name>
      <SpritePartName>CharacterDeveloper\perk_selection_popup_arrow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\progress_bar_empty</Name>
      <SpritePartName>CharacterDeveloper\progress_bar_empty</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\progress_bar_fill</Name>
      <SpritePartName>CharacterDeveloper\progress_bar_fill</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\progress_bar_glow</Name>
      <SpritePartName>CharacterDeveloper\progress_bar_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\progress_bar_stick</Name>
      <SpritePartName>CharacterDeveloper\progress_bar_stick</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\selected_perk_indicator</Name>
      <SpritePartName>CharacterDeveloper\selected_perk_indicator</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\selected_perk_indicator_hover</Name>
      <SpritePartName>CharacterDeveloper\selected_perk_indicator_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\skills_divider_diamond</Name>
      <SpritePartName>CharacterDeveloper\skills_divider_diamond</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\skills_icon_shadow</Name>
      <SpritePartName>CharacterDeveloper\skills_icon_shadow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\skill_description</Name>
      <SpritePartName>CharacterDeveloper\skill_description</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\skill_frame</Name>
      <SpritePartName>CharacterDeveloper\skill_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\skill_icon_large_frame</Name>
      <SpritePartName>CharacterDeveloper\skill_icon_large_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\UnselectedPerksIcon</Name>
      <SpritePartName>CharacterDeveloper\UnselectedPerksIcon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_canvas</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\attribute_popup_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_divider</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\attribute_popup_divider</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\attribute_popup_frame</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\attribute_popup_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\plus_button</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\plus_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\plus_button_hover</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\plus_button_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\AttributePopup\plus_button_pressed</Name>
      <SpritePartName>CharacterDeveloper\AttributePopup\plus_button_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\attribute_card</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\attribute_card</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\attribute_card_selection</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\attribute_card_selection</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_card</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_card</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_card_selection</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_card_selection</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate1</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_plate1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate2</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_plate2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate3</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_plate3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CharacterDeveloper\SkillBackgrounds\skill_plate_selected_state</Name>
      <SpritePartName>CharacterDeveloper\SkillBackgrounds\skill_plate_selected_state</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_collapser</Name>
      <SpritePartName>Clan\clan_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_collapser_pressed</Name>
      <SpritePartName>Clan\clan_collapser_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_portrait_shadow</Name>
      <SpritePartName>Clan\clan_portrait_shadow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_tuple</Name>
      <SpritePartName>Clan\clan_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_tuple_dropdown_slot</Name>
      <SpritePartName>Clan\clan_tuple_dropdown_slot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_tuple_selected</Name>
      <SpritePartName>Clan\clan_tuple_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\clan_tuple_tip</Name>
      <SpritePartName>Clan\clan_tuple_tip</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\fiefs_sort_header_fief_name</Name>
      <SpritePartName>Clan\fiefs_sort_header_fief_name</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\fiefs_sort_header_governor</Name>
      <SpritePartName>Clan\fiefs_sort_header_governor</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\fiefs_sort_header_profit</Name>
      <SpritePartName>Clan\fiefs_sort_header_profit</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\finance_collapser</Name>
      <SpritePartName>Clan\finance_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\finance_collapser_hover</Name>
      <SpritePartName>Clan\finance_collapser_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\finance_collapser_pressed</Name>
      <SpritePartName>Clan\finance_collapser_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\finance_tuple</Name>
      <SpritePartName>Clan\finance_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\header</Name>
      <SpritePartName>Clan\header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\members_sort_header_location</Name>
      <SpritePartName>Clan\members_sort_header_location</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\members_sort_header_name</Name>
      <SpritePartName>Clan\members_sort_header_name</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\other_sort_header_income</Name>
      <SpritePartName>Clan\other_sort_header_income</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\other_sort_header_location</Name>
      <SpritePartName>Clan\other_sort_header_location</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\other_sort_header_workshop_name</Name>
      <SpritePartName>Clan\other_sort_header_workshop_name</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\panel_header</Name>
      <SpritePartName>Clan\panel_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\parties_sort_header_location</Name>
      <SpritePartName>Clan\parties_sort_header_location</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\parties_sort_header_name</Name>
      <SpritePartName>Clan\parties_sort_header_name</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\parties_sort_header_size</Name>
      <SpritePartName>Clan\parties_sort_header_size</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\portrait_slot_right_panel</Name>
      <SpritePartName>Clan\portrait_slot_right_panel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\progress_bar</Name>
      <SpritePartName>Clan\progress_bar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\progress_bar_frame</Name>
      <SpritePartName>Clan\progress_bar_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Clan\progress_bar_glow</Name>
      <SpritePartName>Clan\progress_bar_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\bottom_crafting</Name>
      <SpritePartName>Crafting\bottom_crafting</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafted_popup</Name>
      <SpritePartName>Crafting\crafted_popup</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafting_item_civillian</Name>
      <SpritePartName>Crafting\crafting_item_civillian</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafting_leave_button</Name>
      <SpritePartName>Crafting\crafting_leave_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafting_leave_button_hover</Name>
      <SpritePartName>Crafting\crafting_leave_button_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafting_popup_portrait</Name>
      <SpritePartName>Crafting\crafting_popup_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\crafting_weapon_type_stone</Name>
      <SpritePartName>Crafting\crafting_weapon_type_stone</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button1</Name>
      <SpritePartName>Crafting\header_button1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button1_pressed</Name>
      <SpritePartName>Crafting\header_button1_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button2</Name>
      <SpritePartName>Crafting\header_button2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button2_pressed</Name>
      <SpritePartName>Crafting\header_button2_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button3</Name>
      <SpritePartName>Crafting\header_button3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\header_button3_pressed</Name>
      <SpritePartName>Crafting\header_button3_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\left_panel_crafting</Name>
      <SpritePartName>Crafting\left_panel_crafting</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\main_tab_default</Name>
      <SpritePartName>Crafting\main_tab_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\main_tab_hovered</Name>
      <SpritePartName>Crafting\main_tab_hovered</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\main_tab_pressed</Name>
      <SpritePartName>Crafting\main_tab_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\main_tab_selected</Name>
      <SpritePartName>Crafting\main_tab_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\refine_panel_header</Name>
      <SpritePartName>Crafting\refine_panel_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\right_panel_crafting</Name>
      <SpritePartName>Crafting\right_panel_crafting</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\smelt_tuple_default</Name>
      <SpritePartName>Crafting\smelt_tuple_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\smelt_tuple_pressed</Name>
      <SpritePartName>Crafting\smelt_tuple_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\smelt_tuple_selected</Name>
      <SpritePartName>Crafting\smelt_tuple_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\tuple_refine</Name>
      <SpritePartName>Crafting\tuple_refine</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\tuple_refine_pressed</Name>
      <SpritePartName>Crafting\tuple_refine_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Crafting\tuple_refine_selected</Name>
      <SpritePartName>Crafting\tuple_refine_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\banner</Name>
      <SpritePartName>Encyclopedia\banner</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\canvas</Name>
      <SpritePartName>Encyclopedia\canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\clan</Name>
      <SpritePartName>Encyclopedia\clan</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\concept</Name>
      <SpritePartName>Encyclopedia\concept</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\hero</Name>
      <SpritePartName>Encyclopedia\hero</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\icon_search</Name>
      <SpritePartName>Encyclopedia\icon_search</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\kingdom</Name>
      <SpritePartName>Encyclopedia\kingdom</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\navbar</Name>
      <SpritePartName>Encyclopedia\navbar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\search_bar</Name>
      <SpritePartName>Encyclopedia\search_bar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\settlements</Name>
      <SpritePartName>Encyclopedia\settlements</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\subpage_notable_portrait</Name>
      <SpritePartName>Encyclopedia\subpage_notable_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\subpage_slick_frame</Name>
      <SpritePartName>Encyclopedia\subpage_slick_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\troop_tree_side</Name>
      <SpritePartName>Encyclopedia\troop_tree_side</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\troop_tree_straight</Name>
      <SpritePartName>Encyclopedia\troop_tree_straight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Encyclopedia\units</Name>
      <SpritePartName>Encyclopedia\units</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\button_cancel</Name>
      <SpritePartName>General\Button\button_cancel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\button_cancel_hover</Name>
      <SpritePartName>General\Button\button_cancel_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_done</Name>
      <SpritePartName>General\Button\main_button_done</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_done_hover</Name>
      <SpritePartName>General\Button\main_button_done_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_regular</Name>
      <SpritePartName>General\Button\main_button_regular</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_regular_big</Name>
      <SpritePartName>General\Button\main_button_regular_big</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_regular_big_hover</Name>
      <SpritePartName>General\Button\main_button_regular_big_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Button\main_button_regular_hover</Name>
      <SpritePartName>General\Button\main_button_regular_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\CharacterCreation\character_creation_panel</Name>
      <SpritePartName>General\CharacterCreation\character_creation_panel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\CharacterCreation\character_creation_panel_extension</Name>
      <SpritePartName>General\CharacterCreation\character_creation_panel_extension</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\CharacterCreation\culture_flag_small</Name>
      <SpritePartName>General\CharacterCreation\culture_flag_small</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Frame1\canvas</Name>
      <SpritePartName>General\Frame1\canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Frame1\frame</Name>
      <SpritePartName>General\Frame1\frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Frame1\shadow</Name>
      <SpritePartName>General\Frame1\shadow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Frame1.Broken\canvas</Name>
      <SpritePartName>General\Frame1.Broken\canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Frame1.Broken\frame</Name>
      <SpritePartName>General\Frame1.Broken\frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Army</Name>
      <SpritePartName>General\Icons\Army</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Coin@2x</Name>
      <SpritePartName>General\Icons\Coin@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Food</Name>
      <SpritePartName>General\Icons\Food</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Food@2x</Name>
      <SpritePartName>General\Icons\Food@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Garrison</Name>
      <SpritePartName>General\Icons\Garrison</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Health@2x</Name>
      <SpritePartName>General\Icons\Health@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Health_Sick@2x</Name>
      <SpritePartName>General\Icons\Health_Sick@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Influence@2x</Name>
      <SpritePartName>General\Icons\Influence@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Level@2x</Name>
      <SpritePartName>General\Icons\Level@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Militia</Name>
      <SpritePartName>General\Icons\Militia</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Morale</Name>
      <SpritePartName>General\Icons\Morale</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Morale@2x</Name>
      <SpritePartName>General\Icons\Morale@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Party@2x</Name>
      <SpritePartName>General\Icons\Party@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\PartyCost@2x</Name>
      <SpritePartName>General\Icons\PartyCost@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Prosperity</Name>
      <SpritePartName>General\Icons\Prosperity</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Speed@2x</Name>
      <SpritePartName>General\Icons\Speed@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\TroopCost@2x</Name>
      <SpritePartName>General\Icons\TroopCost@2x</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Icons\Walls</Name>
      <SpritePartName>General\Icons\Walls</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\InitialMenu\hover_indicator</Name>
      <SpritePartName>General\InitialMenu\hover_indicator</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\InitialMenu\main_menu_logo</Name>
      <SpritePartName>General\InitialMenu\main_menu_logo</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\InitialMenu\main_menu_logo_960</Name>
      <SpritePartName>General\InitialMenu\main_menu_logo_960</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\InitialMenu\main_menu_texture</Name>
      <SpritePartName>General\InitialMenu\main_menu_texture</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\health_bar_fill_glow</Name>
      <SpritePartName>General\Mission\health_bar_fill_glow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\health_bar_fill_glow_small</Name>
      <SpritePartName>General\Mission\health_bar_fill_glow_small</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\health_bar_frame</Name>
      <SpritePartName>General\Mission\health_bar_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\hero_canvas</Name>
      <SpritePartName>General\Mission\hero_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\hero_icon</Name>
      <SpritePartName>General\Mission\hero_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\horse_canvas</Name>
      <SpritePartName>General\Mission\horse_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\horse_frame</Name>
      <SpritePartName>General\Mission\horse_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\mission_health_bar_fill</Name>
      <SpritePartName>General\Mission\mission_health_bar_fill</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\mission_health_bar_fill_small</Name>
      <SpritePartName>General\Mission\mission_health_bar_fill_small</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\mount_icon</Name>
      <SpritePartName>General\Mission\mount_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\shield_canvas</Name>
      <SpritePartName>General\Mission\shield_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\shield_frame</Name>
      <SpritePartName>General\Mission\shield_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>General\Mission\shield_icon</Name>
      <SpritePartName>General\Mission\shield_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\armorSlot</Name>
      <SpritePartName>Inventory\armorSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\armorSlotHighlight</Name>
      <SpritePartName>Inventory\armorSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\bannerSlot</Name>
      <SpritePartName>Inventory\bannerSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\bannerSlotHighlight</Name>
      <SpritePartName>Inventory\bannerSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\bootSlot</Name>
      <SpritePartName>Inventory\bootSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\bootSlotHighlight</Name>
      <SpritePartName>Inventory\bootSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\cloakSlot</Name>
      <SpritePartName>Inventory\cloakSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\cloakSlotHighlight</Name>
      <SpritePartName>Inventory\cloakSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\extension</Name>
      <SpritePartName>Inventory\extension</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterAll</Name>
      <SpritePartName>Inventory\filterAll</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterAllDark</Name>
      <SpritePartName>Inventory\filterAllDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterAllDown</Name>
      <SpritePartName>Inventory\filterAllDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterAllHighlight</Name>
      <SpritePartName>Inventory\filterAllHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterArmors</Name>
      <SpritePartName>Inventory\filterArmors</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterArmorsDark</Name>
      <SpritePartName>Inventory\filterArmorsDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterArmorsDown</Name>
      <SpritePartName>Inventory\filterArmorsDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterArmorsHighlight</Name>
      <SpritePartName>Inventory\filterArmorsHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterFood</Name>
      <SpritePartName>Inventory\filterFood</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterFoodDark</Name>
      <SpritePartName>Inventory\filterFoodDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterFoodDown</Name>
      <SpritePartName>Inventory\filterFoodDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterFoodHighlight</Name>
      <SpritePartName>Inventory\filterFoodHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterMounts</Name>
      <SpritePartName>Inventory\filterMounts</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterMountsDark</Name>
      <SpritePartName>Inventory\filterMountsDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterMountsDown</Name>
      <SpritePartName>Inventory\filterMountsDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterMountsHighlight</Name>
      <SpritePartName>Inventory\filterMountsHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterShields</Name>
      <SpritePartName>Inventory\filterShields</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterShieldsDark</Name>
      <SpritePartName>Inventory\filterShieldsDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterShieldsDown</Name>
      <SpritePartName>Inventory\filterShieldsDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterShieldsHighlight</Name>
      <SpritePartName>Inventory\filterShieldsHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterWeapons</Name>
      <SpritePartName>Inventory\filterWeapons</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterWeaponsDark</Name>
      <SpritePartName>Inventory\filterWeaponsDark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterWeaponsDown</Name>
      <SpritePartName>Inventory\filterWeaponsDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\filterWeaponsHighlight</Name>
      <SpritePartName>Inventory\filterWeaponsHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\gloveSlot</Name>
      <SpritePartName>Inventory\gloveSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\gloveSlotHighlight</Name>
      <SpritePartName>Inventory\gloveSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_1</Name>
      <SpritePartName>Inventory\header_button_left_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_1_hover</Name>
      <SpritePartName>Inventory\header_button_left_1_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_1_pressed</Name>
      <SpritePartName>Inventory\header_button_left_1_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_1_selected</Name>
      <SpritePartName>Inventory\header_button_left_1_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_2</Name>
      <SpritePartName>Inventory\header_button_left_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_2_hover</Name>
      <SpritePartName>Inventory\header_button_left_2_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_2_pressed</Name>
      <SpritePartName>Inventory\header_button_left_2_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_2_selected</Name>
      <SpritePartName>Inventory\header_button_left_2_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_3</Name>
      <SpritePartName>Inventory\header_button_left_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_3_hover</Name>
      <SpritePartName>Inventory\header_button_left_3_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_3_pressed</Name>
      <SpritePartName>Inventory\header_button_left_3_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_3_selected</Name>
      <SpritePartName>Inventory\header_button_left_3_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_4</Name>
      <SpritePartName>Inventory\header_button_left_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_4_hover</Name>
      <SpritePartName>Inventory\header_button_left_4_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_4_pressed</Name>
      <SpritePartName>Inventory\header_button_left_4_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_left_4_selected</Name>
      <SpritePartName>Inventory\header_button_left_4_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_1</Name>
      <SpritePartName>Inventory\header_button_right_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_1_hover</Name>
      <SpritePartName>Inventory\header_button_right_1_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_1_pressed</Name>
      <SpritePartName>Inventory\header_button_right_1_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_1_selected</Name>
      <SpritePartName>Inventory\header_button_right_1_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_2</Name>
      <SpritePartName>Inventory\header_button_right_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_2_hover</Name>
      <SpritePartName>Inventory\header_button_right_2_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_2_pressed</Name>
      <SpritePartName>Inventory\header_button_right_2_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_2_selected</Name>
      <SpritePartName>Inventory\header_button_right_2_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_3</Name>
      <SpritePartName>Inventory\header_button_right_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_3_hover</Name>
      <SpritePartName>Inventory\header_button_right_3_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_3_pressed</Name>
      <SpritePartName>Inventory\header_button_right_3_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_3_selected</Name>
      <SpritePartName>Inventory\header_button_right_3_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_4</Name>
      <SpritePartName>Inventory\header_button_right_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_4_hover</Name>
      <SpritePartName>Inventory\header_button_right_4_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_4_pressed</Name>
      <SpritePartName>Inventory\header_button_right_4_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_button_right_4_selected</Name>
      <SpritePartName>Inventory\header_button_right_4_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_left</Name>
      <SpritePartName>Inventory\header_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\header_right</Name>
      <SpritePartName>Inventory\header_right</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\helmetSlot</Name>
      <SpritePartName>Inventory\helmetSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\helmetSlotHighlight</Name>
      <SpritePartName>Inventory\helmetSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\icon_equip</Name>
      <SpritePartName>Inventory\icon_equip</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\icon_gold</Name>
      <SpritePartName>Inventory\icon_gold</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\icon_inspect</Name>
      <SpritePartName>Inventory\icon_inspect</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\icon_inventory_weight</Name>
      <SpritePartName>Inventory\icon_inventory_weight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\icon_unequip</Name>
      <SpritePartName>Inventory\icon_unequip</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\inspect</Name>
      <SpritePartName>Inventory\inspect</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modCivillian</Name>
      <SpritePartName>Inventory\modCivillian</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modCivillianDown</Name>
      <SpritePartName>Inventory\modCivillianDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modCivillianHighlight</Name>
      <SpritePartName>Inventory\modCivillianHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modWar</Name>
      <SpritePartName>Inventory\modWar</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modWarDown</Name>
      <SpritePartName>Inventory\modWarDown</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\modWarHighlight</Name>
      <SpritePartName>Inventory\modWarHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\mountArmorSlot</Name>
      <SpritePartName>Inventory\mountArmorSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\mountArmorSlotHighlight</Name>
      <SpritePartName>Inventory\mountArmorSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\mountSlot</Name>
      <SpritePartName>Inventory\mountSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\mountSlotHighlight</Name>
      <SpritePartName>Inventory\mountSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\portrait</Name>
      <SpritePartName>Inventory\portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\portrait_cart</Name>
      <SpritePartName>Inventory\portrait_cart</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\production_icon</Name>
      <SpritePartName>Inventory\production_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\ShieldIcon</Name>
      <SpritePartName>Inventory\ShieldIcon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\tuple_left</Name>
      <SpritePartName>Inventory\tuple_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\tuple_left_pressed</Name>
      <SpritePartName>Inventory\tuple_left_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\tuple_right</Name>
      <SpritePartName>Inventory\tuple_right</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\tuple_right_pressed</Name>
      <SpritePartName>Inventory\tuple_right_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\tuple_shadow</Name>
      <SpritePartName>Inventory\tuple_shadow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\weaponSlot</Name>
      <SpritePartName>Inventory\weaponSlot</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\weaponSlotHighlight</Name>
      <SpritePartName>Inventory\weaponSlotHighlight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Inventory\WeightIcon</Name>
      <SpritePartName>Inventory\WeightIcon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\army_button_background</Name>
      <SpritePartName>MapBar\army_button_background</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\army_button_front</Name>
      <SpritePartName>MapBar\army_button_front</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\arrow_looking_up</Name>
      <SpritePartName>MapBar\arrow_looking_up</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button1_exit</Name>
      <SpritePartName>MapBar\bottom_left_button1_exit</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button2_character</Name>
      <SpritePartName>MapBar\bottom_left_button2_character</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button3_inventory</Name>
      <SpritePartName>MapBar\bottom_left_button3_inventory</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button4_party</Name>
      <SpritePartName>MapBar\bottom_left_button4_party</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button5_quest</Name>
      <SpritePartName>MapBar\bottom_left_button5_quest</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button6_clan</Name>
      <SpritePartName>MapBar\bottom_left_button6_clan</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\bottom_left_button7_kingdom</Name>
      <SpritePartName>MapBar\bottom_left_button7_kingdom</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\gather_army_icon</Name>
      <SpritePartName>MapBar\gather_army_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_banner_choose_update</Name>
      <SpritePartName>MapBar\mapbar_banner_choose_update</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_circle_canvas</Name>
      <SpritePartName>MapBar\mapbar_center_circle_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_circle_daynight</Name>
      <SpritePartName>MapBar\mapbar_center_circle_daynight</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_circle_frame</Name>
      <SpritePartName>MapBar\mapbar_center_circle_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_circle_glow_overlay</Name>
      <SpritePartName>MapBar\mapbar_center_circle_glow_overlay</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_frame</Name>
      <SpritePartName>MapBar\mapbar_center_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_center_frame_silhouette</Name>
      <SpritePartName>MapBar\mapbar_center_frame_silhouette</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon1</Name>
      <SpritePartName>MapBar\mapbar_icon1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon2</Name>
      <SpritePartName>MapBar\mapbar_icon2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon3</Name>
      <SpritePartName>MapBar\mapbar_icon3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon4</Name>
      <SpritePartName>MapBar\mapbar_icon4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon5</Name>
      <SpritePartName>MapBar\mapbar_icon5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon6</Name>
      <SpritePartName>MapBar\mapbar_icon6</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_icon7</Name>
      <SpritePartName>MapBar\mapbar_icon7</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_left_canvas</Name>
      <SpritePartName>MapBar\mapbar_left_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_left_frame</Name>
      <SpritePartName>MapBar\mapbar_left_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_left_frame_silhouette</Name>
      <SpritePartName>MapBar\mapbar_left_frame_silhouette</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_right_frame</Name>
      <SpritePartName>MapBar\mapbar_right_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\mapbar_right_frame_silhouette</Name>
      <SpritePartName>MapBar\mapbar_right_frame_silhouette</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>MapBar\monocular_icon</Name>
      <SpritePartName>MapBar\monocular_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\melee_unit_troop_card_default</Name>
      <SpritePartName>Order\melee_unit_troop_card_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\melee_unit_troop_card_selected</Name>
      <SpritePartName>Order\melee_unit_troop_card_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\melee_unit_troop_card_transfer_selected</Name>
      <SpritePartName>Order\melee_unit_troop_card_transfer_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\MinimalOrderTroop</Name>
      <SpritePartName>Order\MinimalOrderTroop</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\OrderTroopBackground</Name>
      <SpritePartName>Order\OrderTroopBackground</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\ranged_unit_troop_card_default</Name>
      <SpritePartName>Order\ranged_unit_troop_card_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\ranged_unit_troop_card_selected</Name>
      <SpritePartName>Order\ranged_unit_troop_card_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\ranged_unit_troop_card_transfer_selected</Name>
      <SpritePartName>Order\ranged_unit_troop_card_transfer_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Order\SiegeItemBackground</Name>
      <SpritePartName>Order\SiegeItemBackground</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_collapser</Name>
      <SpritePartName>PartyScreen\button_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_collapser_hover</Name>
      <SpritePartName>PartyScreen\button_collapser_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_collapser_pressed</Name>
      <SpritePartName>PartyScreen\button_collapser_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_left</Name>
      <SpritePartName>PartyScreen\button_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_left_hover</Name>
      <SpritePartName>PartyScreen\button_left_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_left_pressed</Name>
      <SpritePartName>PartyScreen\button_left_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_left_selected</Name>
      <SpritePartName>PartyScreen\button_left_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_right</Name>
      <SpritePartName>PartyScreen\button_right</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_right_hover</Name>
      <SpritePartName>PartyScreen\button_right_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_right_pressed</Name>
      <SpritePartName>PartyScreen\button_right_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\button_right_selected</Name>
      <SpritePartName>PartyScreen\button_right_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\divider</Name>
      <SpritePartName>PartyScreen\divider</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\header_left</Name>
      <SpritePartName>PartyScreen\header_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\header_right</Name>
      <SpritePartName>PartyScreen\header_right</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\portrait</Name>
      <SpritePartName>PartyScreen\portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\recruit_prisoner</Name>
      <SpritePartName>PartyScreen\recruit_prisoner</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\talk_icon</Name>
      <SpritePartName>PartyScreen\talk_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\upgrade_icon</Name>
      <SpritePartName>PartyScreen\upgrade_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>PartyScreen\upgrade_troops_popup_tuple</Name>
      <SpritePartName>PartyScreen\upgrade_troops_popup_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\background_quest</Name>
      <SpritePartName>Quest\background_quest</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\big_canvas</Name>
      <SpritePartName>Quest\big_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\header left</Name>
      <SpritePartName>Quest\header left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\questlist_header</Name>
      <SpritePartName>Quest\questlist_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\quest_collapser_default</Name>
      <SpritePartName>Quest\quest_collapser_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>Quest\quest_screen_tuple_default</Name>
      <SpritePartName>Quest\quest_screen_tuple_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\flag_base</Name>
      <SpritePartName>SaveLoad\flag_base</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_fiefs</Name>
      <SpritePartName>SaveLoad\icon_fiefs</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_food</Name>
      <SpritePartName>SaveLoad\icon_food</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_gold</Name>
      <SpritePartName>SaveLoad\icon_gold</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_influence</Name>
      <SpritePartName>SaveLoad\icon_influence</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_level</Name>
      <SpritePartName>SaveLoad\icon_level</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\icon_party</Name>
      <SpritePartName>SaveLoad\icon_party</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\load_button_canvas</Name>
      <SpritePartName>SaveLoad\load_button_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\load_collapser</Name>
      <SpritePartName>SaveLoad\load_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\load_tuple</Name>
      <SpritePartName>SaveLoad\load_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\load_tuple_selected</Name>
      <SpritePartName>SaveLoad\load_tuple_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SaveLoad\main_hero_save_silhouette</Name>
      <SpritePartName>SaveLoad\main_hero_save_silhouette</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_caster_icon</Name>
      <SpritePartName>SpecialResources\taom_caster_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_dunlending_ale_icon</Name>
      <SpritePartName>SpecialResources\taom_dunlending_ale_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_elven_wine_icon</Name>
      <SpritePartName>SpecialResources\taom_elven_wine_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_gems_icon</Name>
      <SpritePartName>SpecialResources\taom_gems_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_lake_fish_icon</Name>
      <SpritePartName>SpecialResources\taom_lake_fish_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_marks_icon</Name>
      <SpritePartName>SpecialResources\taom_marks_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_plunder_icon</Name>
      <SpritePartName>SpecialResources\taom_plunder_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_tribal_relics_icon</Name>
      <SpritePartName>SpecialResources\taom_tribal_relics_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_war_banners_icon</Name>
      <SpritePartName>SpecialResources\taom_war_banners_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_war_drums_icon</Name>
      <SpritePartName>SpecialResources\taom_war_drums_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SpecialResources\taom_war_spoils_icon</Name>
      <SpritePartName>SpecialResources\taom_war_spoils_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\ambush_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\ambush_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\assault_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\assault_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\besiege_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\besiege_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\craft_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\craft_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\defend_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\defend_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\escape_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\escape_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\hostileaction_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\hostileaction_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\keep_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\keep_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\leave_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\leave_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\raid_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\raid_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\recruit_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\recruit_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\retreat_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\retreat_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\submenu_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\submenu_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\trade_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\trade_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\wait_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\wait_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPGeneral\GameMenu\warehouse_icon</Name>
      <SpritePartName>SPGeneral\GameMenu\warehouse_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\drop_shadow_below_header</Name>
      <SpritePartName>SPKingdom\drop_shadow_below_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\edit_name_icon</Name>
      <SpritePartName>SPKingdom\edit_name_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\header_policies</Name>
      <SpritePartName>SPKingdom\header_policies</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\influence_icon_small</Name>
      <SpritePartName>SPKingdom\influence_icon_small</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\kingdom_collapser</Name>
      <SpritePartName>SPKingdom\kingdom_collapser</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\kingdom_header_button_left_selected</Name>
      <SpritePartName>SPKingdom\kingdom_header_button_left_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\kingdom_top_header</Name>
      <SpritePartName>SPKingdom\kingdom_top_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\kingdom_tuple</Name>
      <SpritePartName>SPKingdom\kingdom_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\king_seal</Name>
      <SpritePartName>SPKingdom\king_seal</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\policy_active_tuple_default</Name>
      <SpritePartName>SPKingdom\policy_active_tuple_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\policy_collapser_default</Name>
      <SpritePartName>SPKingdom\policy_collapser_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\portrait_slot_square</Name>
      <SpritePartName>SPKingdom\portrait_slot_square</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\portrait_slot_wide</Name>
      <SpritePartName>SPKingdom\portrait_slot_wide</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\voter_header_bottom</Name>
      <SpritePartName>SPKingdom\voter_header_bottom</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Armies\header_armies_1</Name>
      <SpritePartName>SPKingdom\Armies\header_armies_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Armies\header_armies_2</Name>
      <SpritePartName>SPKingdom\Armies\header_armies_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Armies\header_armies_3</Name>
      <SpritePartName>SPKingdom\Armies\header_armies_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Armies\header_armies_4</Name>
      <SpritePartName>SPKingdom\Armies\header_armies_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Armies\header_armies_5</Name>
      <SpritePartName>SPKingdom\Armies\header_armies_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_1</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_2</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_3</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_4</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_5</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Clans\header_clans_6</Name>
      <SpritePartName>SPKingdom\Clans\header_clans_6</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\ClanTypes\leader_clan</Name>
      <SpritePartName>SPKingdom\ClanTypes\leader_clan</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\ClanTypes\mercenary_clan</Name>
      <SpritePartName>SPKingdom\ClanTypes\mercenary_clan</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\ClanTypes\normal_clan</Name>
      <SpritePartName>SPKingdom\ClanTypes\normal_clan</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\decision_popup_bg</Name>
      <SpritePartName>SPKingdom\Decision\decision_popup_bg</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\decision_popup_divider</Name>
      <SpritePartName>SPKingdom\Decision\decision_popup_divider</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\namestone</Name>
      <SpritePartName>SPKingdom\Decision\namestone</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\option</Name>
      <SpritePartName>SPKingdom\Decision\option</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\option_selected</Name>
      <SpritePartName>SPKingdom\Decision\option_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\outer_frame</Name>
      <SpritePartName>SPKingdom\Decision\outer_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\progress_bar_base</Name>
      <SpritePartName>SPKingdom\Decision\progress_bar_base</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\progress_bar_fill_positive</Name>
      <SpritePartName>SPKingdom\Decision\progress_bar_fill_positive</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\progress_bar_frame</Name>
      <SpritePartName>SPKingdom\Decision\progress_bar_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\progress_bar_indicator</Name>
      <SpritePartName>SPKingdom\Decision\progress_bar_indicator</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Decision\relation_arrow</Name>
      <SpritePartName>SPKingdom\Decision\relation_arrow</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\bar_seperator</Name>
      <SpritePartName>SPKingdom\Diplomacy\bar_seperator</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\diplomacy_peace_icon</Name>
      <SpritePartName>SPKingdom\Diplomacy\diplomacy_peace_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\diplomacy_war_icon</Name>
      <SpritePartName>SPKingdom\Diplomacy\diplomacy_war_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\divider_left</Name>
      <SpritePartName>SPKingdom\Diplomacy\divider_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\paying_tribute</Name>
      <SpritePartName>SPKingdom\Diplomacy\paying_tribute</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\receiving_tribute</Name>
      <SpritePartName>SPKingdom\Diplomacy\receiving_tribute</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\stats_compare_icon</Name>
      <SpritePartName>SPKingdom\Diplomacy\stats_compare_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\wars_at_peace_tuple</Name>
      <SpritePartName>SPKingdom\Diplomacy\wars_at_peace_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Diplomacy\wars_at_war_tuple</Name>
      <SpritePartName>SPKingdom\Diplomacy\wars_at_war_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Fiefs\header_fiefs_1</Name>
      <SpritePartName>SPKingdom\Fiefs\header_fiefs_1</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Fiefs\header_fiefs_2</Name>
      <SpritePartName>SPKingdom\Fiefs\header_fiefs_2</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Fiefs\header_fiefs_3</Name>
      <SpritePartName>SPKingdom\Fiefs\header_fiefs_3</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Fiefs\header_fiefs_4</Name>
      <SpritePartName>SPKingdom\Fiefs\header_fiefs_4</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>SPKingdom\Fiefs\header_fiefs_5</Name>
      <SpritePartName>SPKingdom\Fiefs\header_fiefs_5</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_large_pointing_left</Name>
      <SpritePartName>StdAssets\arrow_large_pointing_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_little</Name>
      <SpritePartName>StdAssets\arrow_little</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_little_disabled</Name>
      <SpritePartName>StdAssets\arrow_little_disabled</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_little_thiqq</Name>
      <SpritePartName>StdAssets\arrow_little_thiqq</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_pointing_left</Name>
      <SpritePartName>StdAssets\arrow_pointing_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_thiqq_large_pointing_left</Name>
      <SpritePartName>StdAssets\arrow_thiqq_large_pointing_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\arrow_thiqq_pointing_left</Name>
      <SpritePartName>StdAssets\arrow_thiqq_pointing_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\banner_flat</Name>
      <SpritePartName>StdAssets\banner_flat</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\banner_flat_small</Name>
      <SpritePartName>StdAssets\banner_flat_small</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\banner_new</Name>
      <SpritePartName>StdAssets\banner_new</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\banner_tuple</Name>
      <SpritePartName>StdAssets\banner_tuple</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\close_button</Name>
      <SpritePartName>StdAssets\close_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\close_button_hover</Name>
      <SpritePartName>StdAssets\close_button_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\close_button_pressed</Name>
      <SpritePartName>StdAssets\close_button_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\delete_button_hovered</Name>
      <SpritePartName>StdAssets\delete_button_hovered</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\delete_button_regular</Name>
      <SpritePartName>StdAssets\delete_button_regular</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_done</Name>
      <SpritePartName>StdAssets\main_button_done</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_done_below</Name>
      <SpritePartName>StdAssets\main_button_done_below</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_done_below_hover</Name>
      <SpritePartName>StdAssets\main_button_done_below_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_done_hover</Name>
      <SpritePartName>StdAssets\main_button_done_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_regular</Name>
      <SpritePartName>StdAssets\main_button_regular</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_regular_below</Name>
      <SpritePartName>StdAssets\main_button_regular_below</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_regular_below_hover</Name>
      <SpritePartName>StdAssets\main_button_regular_below_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\main_button_regular_hover</Name>
      <SpritePartName>StdAssets\main_button_regular_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\page_button_center</Name>
      <SpritePartName>StdAssets\page_button_center</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\page_button_center_selected</Name>
      <SpritePartName>StdAssets\page_button_center_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\page_button_left</Name>
      <SpritePartName>StdAssets\page_button_left</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\page_button_left_selected</Name>
      <SpritePartName>StdAssets\page_button_left_selected</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\standart_popup</Name>
      <SpritePartName>StdAssets\standart_popup</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\standart_popup_button</Name>
      <SpritePartName>StdAssets\standart_popup_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\standart_popup_button_hover</Name>
      <SpritePartName>StdAssets\standart_popup_button_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\tabbar_long</Name>
      <SpritePartName>StdAssets\tabbar_long</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\tabbar_long_namebox</Name>
      <SpritePartName>StdAssets\tabbar_long_namebox</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\tabbar_popup</Name>
      <SpritePartName>StdAssets\tabbar_popup</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\tabbar_standart</Name>
      <SpritePartName>StdAssets\tabbar_standart</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\top_header</Name>
      <SpritePartName>StdAssets\top_header</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\triple_button_frame</Name>
      <SpritePartName>StdAssets\triple_button_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\triple_popup_button_frame</Name>
      <SpritePartName>StdAssets\triple_popup_button_frame</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\wide_rectangle_button_default</Name>
      <SpritePartName>StdAssets\wide_rectangle_button_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\wide_rectangle_button_hovered</Name>
      <SpritePartName>StdAssets\wide_rectangle_button_hovered</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\wide_rectangle_button_pressed</Name>
      <SpritePartName>StdAssets\wide_rectangle_button_pressed</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\armours</Name>
      <SpritePartName>StdAssets\database\armours</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\background-watermark</Name>
      <SpritePartName>StdAssets\database\background-watermark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\background</Name>
      <SpritePartName>StdAssets\database\background</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\border</Name>
      <SpritePartName>StdAssets\database\border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\lords</Name>
      <SpritePartName>StdAssets\database\lords</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\database\troops</Name>
      <SpritePartName>StdAssets\database\troops</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\button_default</Name>
      <SpritePartName>StdAssets\Popup\button_default</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\button_default_hover</Name>
      <SpritePartName>StdAssets\Popup\button_default_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\canvas</Name>
      <SpritePartName>StdAssets\Popup\canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\canvas_dark</Name>
      <SpritePartName>StdAssets\Popup\canvas_dark</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\canvas_gradient</Name>
      <SpritePartName>StdAssets\Popup\canvas_gradient</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\done_button</Name>
      <SpritePartName>StdAssets\Popup\done_button</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\done_button_hover</Name>
      <SpritePartName>StdAssets\Popup\done_button_hover</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>StdAssets\Popup\order_popup_canvas</Name>
      <SpritePartName>StdAssets\Popup\order_popup_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\CharacterScreen\menuoptions_border</Name>
      <SpritePartName>TAOM\CharacterScreen\menuoptions_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\CharacterScreen\menuoptions_mask</Name>
      <SpritePartName>TAOM\CharacterScreen\menuoptions_mask</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\banner</Name>
      <SpritePartName>TAOM\Encyclopedia\banner</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\clan_diamond_border</Name>
      <SpritePartName>TAOM\Encyclopedia\clan_diamond_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\clan_diamond_mask</Name>
      <SpritePartName>TAOM\Encyclopedia\clan_diamond_mask</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\fief_square_border</Name>
      <SpritePartName>TAOM\Encyclopedia\fief_square_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\home_rectangle_border</Name>
      <SpritePartName>TAOM\Encyclopedia\home_rectangle_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Encyclopedia\lord_rectangle_border</Name>
      <SpritePartName>TAOM\Encyclopedia\lord_rectangle_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\GameMenu\game_menu_frame_border</Name>
      <SpritePartName>TAOM\GameMenu\game_menu_frame_border</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\GameMenu\game_menu_frame_canvas</Name>
      <SpritePartName>TAOM\GameMenu\game_menu_frame_canvas</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\GameMenu\game_menu_inner_panel</Name>
      <SpritePartName>TAOM\GameMenu\game_menu_inner_panel</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Nameplates\player_ring_icon</Name>
      <SpritePartName>TAOM\Nameplates\player_ring_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Nameplates\settlement_banner_large</Name>
      <SpritePartName>TAOM\Nameplates\settlement_banner_large</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Nameplates\settlement_banner_large_background</Name>
      <SpritePartName>TAOM\Nameplates\settlement_banner_large_background</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Nameplates\settlement_nameplate_background</Name>
      <SpritePartName>TAOM\Nameplates\settlement_nameplate_background</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\Nameplates\settlement_nameplate_large</Name>
      <SpritePartName>TAOM\Nameplates\settlement_nameplate_large</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\SPScoreboard\background_overlay</Name>
      <SpritePartName>TAOM\SPScoreboard\background_overlay</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>TAOM\SPScoreboard\divider_nameplate</Name>
      <SpritePartName>TAOM\SPScoreboard\divider_nameplate</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10000</Name>
      <SpritePartName>10000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10001</Name>
      <SpritePartName>10001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10002</Name>
      <SpritePartName>10002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10003</Name>
      <SpritePartName>10003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10004</Name>
      <SpritePartName>10004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10005</Name>
      <SpritePartName>10005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10006</Name>
      <SpritePartName>10006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10007</Name>
      <SpritePartName>10007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10008</Name>
      <SpritePartName>10008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10009</Name>
      <SpritePartName>10009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10010</Name>
      <SpritePartName>10010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10011</Name>
      <SpritePartName>10011</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10012</Name>
      <SpritePartName>10012</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10013</Name>
      <SpritePartName>10013</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10014</Name>
      <SpritePartName>10014</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10015</Name>
      <SpritePartName>10015</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10016</Name>
      <SpritePartName>10016</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10017</Name>
      <SpritePartName>10017</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10018</Name>
      <SpritePartName>10018</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10019</Name>
      <SpritePartName>10019</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10020</Name>
      <SpritePartName>10020</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10021</Name>
      <SpritePartName>10021</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10022</Name>
      <SpritePartName>10022</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10023</Name>
      <SpritePartName>10023</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10024</Name>
      <SpritePartName>10024</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10025</Name>
      <SpritePartName>10025</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10026</Name>
      <SpritePartName>10026</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10027</Name>
      <SpritePartName>10027</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10028</Name>
      <SpritePartName>10028</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10029</Name>
      <SpritePartName>10029</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10030</Name>
      <SpritePartName>10030</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10500</Name>
      <SpritePartName>10500</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10501</Name>
      <SpritePartName>10501</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10502</Name>
      <SpritePartName>10502</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10503</Name>
      <SpritePartName>10503</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10504</Name>
      <SpritePartName>10504</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10505</Name>
      <SpritePartName>10505</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>10506</Name>
      <SpritePartName>10506</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11000</Name>
      <SpritePartName>11000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11001</Name>
      <SpritePartName>11001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11002</Name>
      <SpritePartName>11002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11003</Name>
      <SpritePartName>11003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11004</Name>
      <SpritePartName>11004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11005</Name>
      <SpritePartName>11005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>11006</Name>
      <SpritePartName>11006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12000</Name>
      <SpritePartName>12000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12001</Name>
      <SpritePartName>12001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12002</Name>
      <SpritePartName>12002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12003</Name>
      <SpritePartName>12003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12004</Name>
      <SpritePartName>12004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12005</Name>
      <SpritePartName>12005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>12006</Name>
      <SpritePartName>12006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>13000</Name>
      <SpritePartName>13000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>13001</Name>
      <SpritePartName>13001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>13002</Name>
      <SpritePartName>13002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>13003</Name>
      <SpritePartName>13003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>13004</Name>
      <SpritePartName>13004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14000</Name>
      <SpritePartName>14000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14001</Name>
      <SpritePartName>14001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14002</Name>
      <SpritePartName>14002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14003</Name>
      <SpritePartName>14003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14004</Name>
      <SpritePartName>14004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14005</Name>
      <SpritePartName>14005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14006</Name>
      <SpritePartName>14006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14007</Name>
      <SpritePartName>14007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14008</Name>
      <SpritePartName>14008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14009</Name>
      <SpritePartName>14009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14010</Name>
      <SpritePartName>14010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14011</Name>
      <SpritePartName>14011</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14012</Name>
      <SpritePartName>14012</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14013</Name>
      <SpritePartName>14013</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14014</Name>
      <SpritePartName>14014</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14015</Name>
      <SpritePartName>14015</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14500</Name>
      <SpritePartName>14500</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14501</Name>
      <SpritePartName>14501</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14502</Name>
      <SpritePartName>14502</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>14503</Name>
      <SpritePartName>14503</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15000</Name>
      <SpritePartName>15000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15001</Name>
      <SpritePartName>15001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15002</Name>
      <SpritePartName>15002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15003</Name>
      <SpritePartName>15003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15004</Name>
      <SpritePartName>15004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15005</Name>
      <SpritePartName>15005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15006</Name>
      <SpritePartName>15006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15007</Name>
      <SpritePartName>15007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15008</Name>
      <SpritePartName>15008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>15009</Name>
      <SpritePartName>15009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16000</Name>
      <SpritePartName>16000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16001</Name>
      <SpritePartName>16001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16002</Name>
      <SpritePartName>16002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16003</Name>
      <SpritePartName>16003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16004</Name>
      <SpritePartName>16004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16005</Name>
      <SpritePartName>16005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16006</Name>
      <SpritePartName>16006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16007</Name>
      <SpritePartName>16007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16008</Name>
      <SpritePartName>16008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>16009</Name>
      <SpritePartName>16009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17000</Name>
      <SpritePartName>17000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17001</Name>
      <SpritePartName>17001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17002</Name>
      <SpritePartName>17002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17003</Name>
      <SpritePartName>17003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17004</Name>
      <SpritePartName>17004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17005</Name>
      <SpritePartName>17005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17006</Name>
      <SpritePartName>17006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17007</Name>
      <SpritePartName>17007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>17008</Name>
      <SpritePartName>17008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>18000</Name>
      <SpritePartName>18000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>18001</Name>
      <SpritePartName>18001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>18002</Name>
      <SpritePartName>18002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>18003</Name>
      <SpritePartName>18003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19000</Name>
      <SpritePartName>19000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19001</Name>
      <SpritePartName>19001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19002</Name>
      <SpritePartName>19002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19003</Name>
      <SpritePartName>19003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19004</Name>
      <SpritePartName>19004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19005</Name>
      <SpritePartName>19005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19006</Name>
      <SpritePartName>19006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19007</Name>
      <SpritePartName>19007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19008</Name>
      <SpritePartName>19008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19009</Name>
      <SpritePartName>19009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19010</Name>
      <SpritePartName>19010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19011</Name>
      <SpritePartName>19011</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19012</Name>
      <SpritePartName>19012</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19013</Name>
      <SpritePartName>19013</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19014</Name>
      <SpritePartName>19014</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>19015</Name>
      <SpritePartName>19015</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20000</Name>
      <SpritePartName>20000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20001</Name>
      <SpritePartName>20001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20002</Name>
      <SpritePartName>20002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20003</Name>
      <SpritePartName>20003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20004</Name>
      <SpritePartName>20004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20005</Name>
      <SpritePartName>20005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>20006</Name>
      <SpritePartName>20006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21000</Name>
      <SpritePartName>21000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21001</Name>
      <SpritePartName>21001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21002</Name>
      <SpritePartName>21002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21003</Name>
      <SpritePartName>21003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21004</Name>
      <SpritePartName>21004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21005</Name>
      <SpritePartName>21005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21006</Name>
      <SpritePartName>21006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21007</Name>
      <SpritePartName>21007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21008</Name>
      <SpritePartName>21008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21009</Name>
      <SpritePartName>21009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>21010</Name>
      <SpritePartName>21010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>22000</Name>
      <SpritePartName>22000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>22001</Name>
      <SpritePartName>22001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>22002</Name>
      <SpritePartName>22002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>22003</Name>
      <SpritePartName>22003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>22004</Name>
      <SpritePartName>22004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>23000</Name>
      <SpritePartName>23000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>23001</Name>
      <SpritePartName>23001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>23002</Name>
      <SpritePartName>23002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>23003</Name>
      <SpritePartName>23003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24000</Name>
      <SpritePartName>24000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24001</Name>
      <SpritePartName>24001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24002</Name>
      <SpritePartName>24002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24003</Name>
      <SpritePartName>24003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24004</Name>
      <SpritePartName>24004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24005</Name>
      <SpritePartName>24005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24006</Name>
      <SpritePartName>24006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24007</Name>
      <SpritePartName>24007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24008</Name>
      <SpritePartName>24008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24009</Name>
      <SpritePartName>24009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24010</Name>
      <SpritePartName>24010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24011</Name>
      <SpritePartName>24011</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24012</Name>
      <SpritePartName>24012</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24013</Name>
      <SpritePartName>24013</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24014</Name>
      <SpritePartName>24014</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24015</Name>
      <SpritePartName>24015</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24016</Name>
      <SpritePartName>24016</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24017</Name>
      <SpritePartName>24017</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24018</Name>
      <SpritePartName>24018</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24019</Name>
      <SpritePartName>24019</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24020</Name>
      <SpritePartName>24020</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24021</Name>
      <SpritePartName>24021</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24022</Name>
      <SpritePartName>24022</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24023</Name>
      <SpritePartName>24023</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24024</Name>
      <SpritePartName>24024</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24025</Name>
      <SpritePartName>24025</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24026</Name>
      <SpritePartName>24026</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24027</Name>
      <SpritePartName>24027</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24028</Name>
      <SpritePartName>24028</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24029</Name>
      <SpritePartName>24029</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24030</Name>
      <SpritePartName>24030</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24500</Name>
      <SpritePartName>24500</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24501</Name>
      <SpritePartName>24501</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24502</Name>
      <SpritePartName>24502</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24503</Name>
      <SpritePartName>24503</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24504</Name>
      <SpritePartName>24504</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24505</Name>
      <SpritePartName>24505</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24506</Name>
      <SpritePartName>24506</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24507</Name>
      <SpritePartName>24507</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24508</Name>
      <SpritePartName>24508</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24509</Name>
      <SpritePartName>24509</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>24510</Name>
      <SpritePartName>24510</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25000</Name>
      <SpritePartName>25000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25001</Name>
      <SpritePartName>25001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25002</Name>
      <SpritePartName>25002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25003</Name>
      <SpritePartName>25003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25004</Name>
      <SpritePartName>25004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25005</Name>
      <SpritePartName>25005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25006</Name>
      <SpritePartName>25006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25007</Name>
      <SpritePartName>25007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25008</Name>
      <SpritePartName>25008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25009</Name>
      <SpritePartName>25009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25010</Name>
      <SpritePartName>25010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25011</Name>
      <SpritePartName>25011</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25012</Name>
      <SpritePartName>25012</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25013</Name>
      <SpritePartName>25013</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25014</Name>
      <SpritePartName>25014</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>25015</Name>
      <SpritePartName>25015</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26000</Name>
      <SpritePartName>26000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26001</Name>
      <SpritePartName>26001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26002</Name>
      <SpritePartName>26002</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26003</Name>
      <SpritePartName>26003</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26004</Name>
      <SpritePartName>26004</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26005</Name>
      <SpritePartName>26005</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26006</Name>
      <SpritePartName>26006</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26007</Name>
      <SpritePartName>26007</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26008</Name>
      <SpritePartName>26008</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26009</Name>
      <SpritePartName>26009</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>26010</Name>
      <SpritePartName>26010</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>27000</Name>
      <SpritePartName>27000</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>27001</Name>
      <SpritePartName>27001</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>27500</Name>
      <SpritePartName>27500</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\career_button_placeholder</Name>
      <SpritePartName>CareerSystem\career_button_placeholder</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\locked_gate_bottom</Name>
      <SpritePartName>CareerSystem\locked_gate_bottom</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\locked_gate_full</Name>
      <SpritePartName>CareerSystem\locked_gate_full</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\locked_gate_top</Name>
      <SpritePartName>CareerSystem\locked_gate_top</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\minus_sign_icon</Name>
      <SpritePartName>CareerSystem\minus_sign_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\plus_sign_icon</Name>
      <SpritePartName>CareerSystem\plus_sign_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\career_point_pip</Name>
      <SpritePartName>CareerSystem\career_point_pip</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\captain_of_osgiliath_ability</Name>
      <SpritePartName>CareerSystem\Abilities\captain_of_osgiliath_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\crossbow_master_ability</Name>
      <SpritePartName>CareerSystem\Abilities\crossbow_master_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\eotheod_windrider_ability</Name>
      <SpritePartName>CareerSystem\Abilities\eotheod_windrider_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\ironguard_ability</Name>
      <SpritePartName>CareerSystem\Abilities\ironguard_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\knight_of_belfalas_ability</Name>
      <SpritePartName>CareerSystem\Abilities\knight_of_belfalas_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\marksman_of_aldburg_ability</Name>
      <SpritePartName>CareerSystem\Abilities\marksman_of_aldburg_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\ram_rider_ability</Name>
      <SpritePartName>CareerSystem\Abilities\ram_rider_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\ranger_of_ithilien_ability</Name>
      <SpritePartName>CareerSystem\Abilities\ranger_of_ithilien_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\watchman_of_stangard_ability</Name>
      <SpritePartName>CareerSystem\Abilities\watchman_of_stangard_ability</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_balchoth_kan_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_balchoth_kan_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_black_uruk_captain_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_black_uruk_captain_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_blade_dancer_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_blade_dancer_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_captain_of_osgiliath_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_captain_of_osgiliath_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_cave_troll_master_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_cave_troll_master_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_codyan_legionaire_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_codyan_legionaire_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_elven_archer_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_elven_archer_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_eotheod_windrider_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_eotheod_windrider_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_ironguard_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_ironguard_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_knight_of_belfalas_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_knight_of_belfalas_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_lokhas_drus_marksman_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_lokhas_drus_marksman_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_mahud_beast_rider_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_mahud_beast_rider_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_marksman_of_aldburg_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_marksman_of_aldburg_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_pezarsani_javelineer_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_pezarsani_javelineer_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_ranger_of_ithilien_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_ranger_of_ithilien_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_shadow_warrior_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_shadow_warrior_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_silvan_archer_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_silvan_archer_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_tribesman_of_jelut_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_tribesman_of_jelut_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_uruk_berserker_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_uruk_berserker_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_uruk_crossbow_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_uruk_crossbow_portrait</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Portraits\career_watchman_of_stangard_portrait</Name>
      <SpritePartName>CareerSystem\Portraits\career_watchman_of_stangard_portrait</SpritePartName>
    </GenericSprite>
  </Sprites>
</SpriteData>

 succeeded in 562ms:
using TaleWorlds.Library;

namespace TAOM.Features.CareerSystem.UI;

public class CareerAbilityEffectVM : ViewModel
{
    private string _lineText;

    public CareerAbilityEffectVM(string text)
    {
        _lineText = text;
    }

    [DataSourceProperty]
    public string LineText
    {
        get => _lineText;
        set
        {
            if (_lineText != value)
            {
                _lineText = value;
                OnPropertyChangedWithValue(value, nameof(LineText));
            }
        }
    }
}

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerRegistryTests
{
    private ICareerConfigProvider _config;
    private ICareerHeroAdapter _hero;
    private CareerRegistry _registry;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss",
        displayName: "Warboss",
        description: "A brute.",
        portraitSprite: "wb_sprite",
        abilityTemplateId: "rally_horde",
        minClanTier: 1,
        rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string> { "wb_brutality" });

    private static readonly CareerChoiceDefinition RootChoice = new CareerChoiceDefinition(
        id: "wb_root", groupId: "", type: ChoiceType.Passive,
        description: "Root", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.TroopDamage, 0.05f),
        mutations: null);

    private static readonly CareerChoiceDefinition KeystoneChoice = new CareerChoiceDefinition(
        id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
        description: "Keystone", iconSprite: "icon", passive: null, mutations: null);

    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
        id: "wb_brutality", careerId: "warboss", tier: 1,
        choiceIds: new List<string> { "wb_brut_key" });

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ICareerConfigProvider>();
        _config.LoadCareers().Returns(new List<CareerDefinition> { WarbossCareer });
        _config.LoadChoiceGroups().Returns(new List<CareerChoiceGroupDefinition> { BrutalityGroup });
        _config.LoadChoices().Returns(new List<CareerChoiceDefinition> { RootChoice, KeystoneChoice });
        _config.GetMaxPerkPoints().Returns(30);

        _hero = Substitute.For<ICareerHeroAdapter>();
        _hero.CultureStringId.Returns("mordor");
        _hero.ClanTier.Returns(2);
        _hero.Level.Returns(5);

        _registry = new CareerRegistry(_config, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void GetCareer_ExistingId_ReturnsCareer()
    {
        var career = _registry.GetCareer("warboss");
        Assert.IsNotNull(career);
        Assert.AreEqual("warboss", career.Id);
    }

    [TestMethod]
    public void GetCareer_UnknownId_ReturnsNull()
    {
        Assert.IsNull(_registry.GetCareer("nonexistent"));
    }

    [TestMethod]
    public void GetAllCareers_ReturnsAllLoaded()
    {
        Assert.AreEqual(1, _registry.GetAllCareers().Count);
    }

    [TestMethod]
    public void GetChoice_ExistingId_ReturnsChoice()
    {
        var choice = _registry.GetChoice("wb_root");
        Assert.IsNotNull(choice);
        Assert.AreEqual(ChoiceType.Passive, choice.Type);
    }

    [TestMethod]
    public void GetGroup_ExistingId_ReturnsGroup()
    {
        var group = _registry.GetGroup("wb_brutality");
        Assert.IsNotNull(group);
        Assert.AreEqual(1, group.Tier);
    }

    [TestMethod]
    public void GetChoicesForGroup_ReturnsGroupChoices()
    {
        var choices = _registry.GetChoicesForGroup("wb_brutality");
        Assert.AreEqual(1, choices.Count);
        Assert.AreEqual("wb_brut_key", choices[0].Id);
    }

    [TestMethod]
    public void GetChoicesForGroup_UnknownGroup_ReturnsEmpty()
    {
        Assert.AreEqual(0, _registry.GetChoicesForGroup("unknown").Count);
    }

    [TestMethod]
    public void IsEligible_HeroMatchesCulture_ReturnsTrue()
    {
        Assert.IsTrue(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_HeroWrongCulture_ReturnsFalse()
    {
        _hero.CultureStringId.Returns("gondor");
        Assert.IsFalse(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_HeroBelowMinClanTier_ReturnsFalse()
    {
        _hero.ClanTier.Returns(0);
        Assert.IsFalse(_registry.IsEligible("warboss", _hero));
    }

    [TestMethod]
    public void IsEligible_UnknownCareer_ReturnsFalse()
    {
        Assert.IsFalse(_registry.IsEligible("nonexistent", _hero));
    }

    [TestMethod]
    public void GetMaxChoicesForHero_Level5_Returns6()
    {
        Assert.AreEqual(6, _registry.GetMaxChoicesForHero(5));
    }

    [TestMethod]
    public void GetMaxChoicesForHero_Level50_CapsAtMaxPerkPoints()
    {
        Assert.AreEqual(31, _registry.GetMaxChoicesForHero(50));
    }

    [TestMethod]
    public void IsTierAvailable_Tier1_AlwaysTrue()
    {
        Assert.IsTrue(_registry.IsTierAvailable(1, 1));
    }

    [TestMethod]
    public void IsTierAvailable_Tier2_RequiresLevel10()
    {
        Assert.IsFalse(_registry.IsTierAvailable(9, 2));
        Assert.IsTrue(_registry.IsTierAvailable(10, 2));
    }

    [TestMethod]
    public void IsTierAvailable_Tier3_RequiresLevel20()
    {
        Assert.IsFalse(_registry.IsTierAvailable(19, 3));
        Assert.IsTrue(_registry.IsTierAvailable(20, 3));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier1_Returns1()
    {
        Assert.AreEqual(1, _registry.GetTierUnlockLevel(1));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier2_Returns10()
    {
        Assert.AreEqual(10, _registry.GetTierUnlockLevel(2));
    }

    [TestMethod]
    public void GetTierUnlockLevel_Tier3_Returns20()
    {
        Assert.AreEqual(20, _registry.GetTierUnlockLevel(3));
    }

    [TestMethod]
    public void IsTierAvailable_ConsistentWithUnlockLevel()
    {
        for (int tier = 1; tier <= 3; tier++)
        {
            var lvl = _registry.GetTierUnlockLevel(tier);
            Assert.IsFalse(_registry.IsTierAvailable(lvl - 1, tier), $"tier {tier} should be locked below its unlock level");
            Assert.IsTrue(_registry.IsTierAvailable(lvl, tier), $"tier {tier} should unlock at its unlock level");
        }
    }
}

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerConfigProviderTests
{
    private IPathService _pathService;
    private IModLogger _logger;
    private CareerConfigProvider _provider;
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "career_system"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);

        _logger = Substitute.For<IModLogger>();
        _provider = new CareerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadCareers_ValidXml_ReturnsCareers()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""25"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        Assert.AreEqual(1, careers.Count);
        Assert.AreEqual("warboss", careers[0].Id);
        Assert.AreEqual("Warboss", careers[0].DisplayName);
        Assert.AreEqual("wb_root", careers[0].RootChoiceId);
        Assert.AreEqual(1, careers[0].EligibleCultureIds.Count);
        Assert.AreEqual("mordor", careers[0].EligibleCultureIds[0]);
        Assert.AreEqual(1, careers[0].ChoiceGroupIds.Count);
    }

    [TestMethod]
    public void LoadCareers_WithRankNames_ParsesPerTierRankNames()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root""
          rank1_name=""{=k1}Boy"" rank2_name=""{=k2}Boss"" rank3_name=""{=k3}Warlord"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        Assert.AreEqual(1, careers.Count);
        Assert.AreEqual("{=k1}Boy", careers[0].Rank1Name);
        Assert.AreEqual("{=k2}Boss", careers[0].Rank2Name);
        Assert.AreEqual("{=k3}Warlord", careers[0].Rank3Name);
    }

    [TestMethod]
    public void LoadCareers_WithoutRankNames_DefaultsToEmpty()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"" />
</Careers>");

        var careers = _provider.LoadCareers();
        Assert.AreEqual("", careers[0].Rank1Name);
        Assert.AreEqual("", careers[0].Rank2Name);
        Assert.AreEqual("", careers[0].Rank3Name);
    }

    [TestMethod]
    public void GetMaxPerkPoints_FromXml_ReturnsConfiguredValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""25""></Careers>");
        Assert.AreEqual(25, _provider.GetMaxPerkPoints());
    }

    [TestMethod]
    public void LoadCareers_MissingFile_ReturnsEmpty()
    {
        var careers = _provider.LoadCareers();
        Assert.AreEqual(0, careers.Count);
    }

    [TestMethod]
    public void LoadChoices_ValidXml_ReturnsChoicesAndGroups()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""wb_root"" group_id="""" type=""Passive"" description=""Root"" icon_sprite=""icon"">
    <PassiveEffect type=""TroopDamage"" magnitude=""0.05"" operation=""Add"" is_percentage=""true"" />
    <Mutations>
      <Mutation target_id=""rally_horde"" property=""Duration""
                calculator=""skill_scaling"" skill=""Leadership"" factor=""0.02"" operation=""Add"" />
    </Mutations>
  </Choice>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
    <Choice id=""wb_brut_p1"" type=""Passive"" description=""Passive 1"" icon_sprite=""icon"">
      <PassiveEffect type=""Damage"" magnitude=""0.10"" operation=""Add"" is_percentage=""true"" attack_type_mask=""Melee"" />
    </Choice>
  </ChoiceGroup>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        var groups = _provider.LoadChoiceGroups();

        Assert.AreEqual(3, choices.Count);

        var root = choices[0];
        Assert.AreEqual("wb_root", root.Id);
        Assert.AreEqual(ChoiceType.Passive, root.Type);
        Assert.IsNotNull(root.Passive);
        Assert.AreEqual(PassiveEffectType.TroopDamage, root.Passive.EffectType);
        Assert.AreEqual(0.05f, root.Passive.Magnitude, 0.001f);
        Assert.IsTrue(root.Passive.IsPercentage);
        Assert.AreEqual(1, root.Mutations.Count);
        Assert.AreEqual("skill_scaling", root.Mutations[0].CalculatorId);
        Assert.AreEqual("Leadership", root.Mutations[0].Params["skill"]);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("wb_brutality", groups[0].Id);
        Assert.AreEqual("warboss", groups[0].CareerId);
        Assert.AreEqual(1, groups[0].Tier);
        Assert.AreEqual(2, groups[0].ChoiceIds.Count);
    }

    [TestMethod]
    public void LoadChoices_GroupWithDisplayName_ParsesDisplayName()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"" display_name=""{=k}Path of Brutality"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var groups = _provider.LoadChoiceGroups();
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("{=k}Path of Brutality", groups[0].DisplayName);
    }

    [TestMethod]
    public void LoadChoices_GroupWithoutDisplayName_DefaultsToEmpty()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var groups = _provider.LoadChoiceGroups();
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("", groups[0].DisplayName);
    }

    [TestMethod]
    public void LoadChoices_PassiveWithAttackTypeMask_ParsesMelee()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Damage"" magnitude=""0.1"" operation=""Add"" attack_type_mask=""Melee"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(AttackTypeMask.Melee, choices[0].Passive.AttackTypeMask);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownInXml_ReturnsConfiguredValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""45"" />
  <Infantry damage_bonus=""15"" damage_reduction=""10"" radius=""50"" />
  <Ranged speed_bonus=""15"" ranged_damage_bonus=""20"" draw_speed_bonus=""20"" />
  <Cavalry mount_speed_bonus=""20"" charge_damage_bonus=""25"" damage_bonus=""10"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(45f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalElementMissing_ReturnsDefaultThirtySeconds()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Infantry damage_bonus=""15"" damage_reduction=""10"" radius=""50"" />
  <Ranged speed_bonus=""15"" ranged_damage_bonus=""20"" draw_speed_bonus=""20"" />
  <Cavalry mount_speed_bonus=""20"" charge_damage_bonus=""25"" damage_bonus=""10"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_TuningFileMissing_ReturnsDefaultGlobalCooldown()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        // No tuning XML written.

        var tuning = _provider.GetAbilityTuning();
        Assert.IsNotNull(tuning.Global);
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownInvalid_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""nonsense"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNegative_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""-5"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownExceedsMaximum_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""99999"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownAtMaximum_AcceptsValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""3600"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(3600f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNaN_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""NaN"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownPositiveInfinity_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""Infinity"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNegativeInfinity_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""-Infinity"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    // ── Issue B: <PassiveEffects> wrapper + value= alias (310 wrapped choices were dead) ──

    [TestMethod]
    public void LoadChoices_PassiveEffectsWrapper_ParsesNestedPassive()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffects>
      <PassiveEffect type=""TroopDamage"" value=""0.10"" />
    </PassiveEffects>
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual(1, choices.Count);
        Assert.IsNotNull(choices[0].Passive, "Nested <PassiveEffects><PassiveEffect/> must be read");
        Assert.AreEqual(PassiveEffectType.TroopDamage, choices[0].Passive.EffectType);
        Assert.AreEqual(0.10f, choices[0].Passive.Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_PassiveEffectValueAttribute_ParsedAsMagnitude()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" value=""0.10"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(0.10f, choices[0].Passive.Magnitude, 0.001f, "value= must alias magnitude=");
    }

    [TestMethod]
    public void LoadChoices_MagnitudeWinsOverValueWhenBothPresent()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" magnitude=""4"" value=""0.10"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(4f, choices[0].Passive.Magnitude, 0.001f, "magnitude= takes precedence over value=");
    }

    [TestMethod]
    public void LoadChoices_DirectPassiveWithMagnitude_StillParses()
    {
        // Regression: the direct (singular, magnitude=) schema must be unaffected by the
        // wrapper/value fallback.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" magnitude=""2"" operation=""Add"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(PassiveEffectType.PartySize, choices[0].Passive.EffectType);
        Assert.AreEqual(2f, choices[0].Passive.Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_EmptyPassiveEffectsWrapper_YieldsNullPassiveNoThrow()
    {
        // Defensive: a wrapper with no <PassiveEffect> child (or a foreign child) must parse to a
        // null passive without throwing — the choice is still valid, it just grants no passive.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffects></PassiveEffects>
  </Choice>
  <Choice id=""c2"" type=""Passive"" description=""test2"" icon_sprite=""icon"">
    <PassiveEffects><Unrelated foo=""bar"" /></PassiveEffects>
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(2, choices.Count);
        Assert.IsNull(choices[0].Passive);
        Assert.IsNull(choices[1].Passive);
    }

    private void WriteCareersXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_careers.xml"), content);
    }

    private void WriteChoicesXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_career_choices.xml"), content);
    }

    private void WriteAbilityTuningXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_ability_tuning.xml"), content);
    }
}

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerScreenVMTests
{
    private CareerDataService _dataService;
    private ICareerRegistry _registry;
    private ICareerPassiveService _passiveService;
    private ICareerConfigProvider _configProvider;
    private IModLogger _logger;
    private bool _closeCalled;

    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "A brute.",
        portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
        minClanTier: 0,
        rootChoiceId: "wb_root",
        eligibleCultureIds: new List<string> { "mordor" },
        choiceGroupIds: new List<string> { "wb_brutality" });

    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
        id: "wb_brutality", careerId: "warboss", tier: 1,
        choiceIds: new List<string> { "wb_brut_key", "wb_brut_p1" });

    private static readonly CareerChoiceDefinition KeystoneChoice = new CareerChoiceDefinition(
        id: "wb_brut_key", groupId: "wb_brutality", type: ChoiceType.Keystone,
        description: "Keystone", iconSprite: "icon", passive: null, mutations: null);

    private static readonly CareerChoiceDefinition PassiveChoice = new CareerChoiceDefinition(
        id: "wb_brut_p1", groupId: "wb_brutality", type: ChoiceType.Passive,
        description: "Passive", iconSprite: "icon",
        passive: new PassiveEffect(PassiveEffectType.Damage, 0.1f),
        mutations: null);

    [TestInitialize]
    public void Setup()
    {
        _dataService = new CareerDataService();
        _registry = Substitute.For<ICareerRegistry>();
        _passiveService = Substitute.For<ICareerPassiveService>();
        _configProvider = Substitute.For<ICareerConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _closeCalled = false;

        _registry.GetCareer("warboss").Returns(WarbossCareer);
        _registry.GetGroup("wb_brutality").Returns(BrutalityGroup);
        _registry.GetChoice("wb_brut_key").Returns(KeystoneChoice);
        _registry.GetChoice("wb_brut_p1").Returns(PassiveChoice);
        _registry.GetChoicesForGroup("wb_brutality").Returns(new List<CareerChoiceDefinition> { KeystoneChoice, PassiveChoice });
        _registry.GetMaxChoicesForHero(5).Returns(6);
        _registry.IsTierAvailable(5, 1).Returns(true);
        _registry.IsTierAvailable(5, 2).Returns(false);
        _registry.IsTierAvailable(5, 3).Returns(false);
    }

    [TestMethod]
    public void HasCareer_NoCareerSet_ReturnsFalse()
    {
        _dataService.GetOrCreateData("hero1");
        var vm = CreateVM();
        Assert.IsFalse(vm.HasCareer);
    }

    [TestMethod]
    public void HasCareer_CareerSet_ReturnsTrue()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.IsTrue(vm.HasCareer);
    }

    [TestMethod]
    public void FreeCareerPoints_TwoChoicesFromLevel5_Returns4()
    {
        SetupHeroWithCareer();
        _dataService.TryAddChoice("hero1", "wb_root", 10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);

        var vm = CreateVM();
        Assert.AreEqual(4, vm.FreeCareerPoints); // 6 max - 2 taken = 4
    }

    [TestMethod]
    public void ChoiceGroupsTier1_HasGroups()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        Assert.AreEqual(1, vm.ChoiceGroupsTier1.Count);
        Assert.AreEqual(2, vm.ChoiceGroupsTier1[0].Choices.Count);
    }

    [TestMethod]
    public void ExecuteClose_CallsCloseAction()
    {
        SetupHeroWithCareer();
        var vm = CreateVM();
        vm.ExecuteClose();
        Assert.IsTrue(_closeCalled);
    }

    [TestMethod]
    public void ExecuteSelectChoice_ValidChoice_AddsAndRefreshes()
    {
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        var vm = CreateVM();

        vm.ExecuteSelectChoice("wb_brut_key");

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"));
        _passiveService.Received().RefreshCache(_dataService, _registry);
    }

    // ── Preventive: Tier Gating (root cause: missing integration test for composed selection flow) ──

    [TestMethod]
    public void ExecuteSelectChoice_Tier2ChoiceAtLevel5_Rejected()
    {
        // Tier 2 requires level 10. Hero is level 5 → IsTierAvailable(5, 2) returns false.
        SetupHeroWithCareer();

        var tier2Group = new CareerChoiceGroupDefinition(
            id: "wb_scavenger", careerId: "warboss", tier: 2,
            choiceIds: new List<string> { "wb_scav_key" });
        var tier2Keystone = new CareerChoiceDefinition(
            id: "wb_scav_key", groupId: "wb_scavenger", type: ChoiceType.Keystone,
            description: "Tier 2 keystone", iconSprite: "icon", passive: null, mutations: null);

        _registry.GetGroup("wb_scavenger").Returns(tier2Group);
        _registry.GetChoice("wb_scav_key").Returns(tier2Keystone);
        _registry.GetMaxChoicesForHero(5).Returns(10);

        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_scav_key");

        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_scav_key"),
            "Tier 2 choice should be rejected when hero level is below threshold");
    }

    [TestMethod]
    public void ExecuteSelectChoice_SecondKeystoneInSameTier_Rejected()
    {
        // Set up career with 2 groups in tier 1, each with a keystone.
        // Selecting the first keystone should succeed, selecting the second should be rejected.
        SetupHeroWithCareer();

        var dominionGroup = new CareerChoiceGroupDefinition(
            id: "wb_dominion", careerId: "warboss", tier: 1,
            choiceIds: new List<string> { "wb_dom_key" });
        var dominionKeystone = new CareerChoiceDefinition(
            id: "wb_dom_key", groupId: "wb_dominion", type: ChoiceType.Keystone,
            description: "Dominion keystone", iconSprite: "icon", passive: null, mutations: null);

        // Expand career to have both groups
        var expandedCareer = new CareerDefinition(
            id: "warboss", displayName: "Warboss", description: "A brute.",
            portraitSprite: "wb_sprite", abilityTemplateId: "rally_horde",
            minClanTier: 0,
            rootChoiceId: "wb_root",
            eligibleCultureIds: new List<string> { "mordor" },
            choiceGroupIds: new List<string> { "wb_brutality", "wb_dominion" });
        _registry.GetCareer("warboss").Returns(expandedCareer);

        _registry.GetGroup("wb_dominion").Returns(dominionGroup);
        _registry.GetChoice("wb_dom_key").Returns(dominionKeystone);
        _registry.GetChoicesForGroup("wb_dominion").Returns(new List<CareerChoiceDefinition> { dominionKeystone });
        _registry.GetMaxChoicesForHero(5).Returns(10);

        var vm = CreateVM();

        // First keystone succeeds
        vm.ExecuteSelectChoice("wb_brut_key");
        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_key"),
            "First keystone in tier should be accepted");

        // Second keystone in same tier rejected
        vm.ExecuteSelectChoice("wb_dom_key");
        Assert.IsFalse(_dataService.GetOrCreateData("hero1").HasChoice("wb_dom_key"),
            "Second keystone in same tier should be rejected (mutual exclusion)");
    }

    [TestMethod]
    public void ExecuteSelectChoice_PassiveInSameTierAsExistingKeystone_Allowed()
    {
        // Passives should still be selectable even after a keystone in the same tier
        SetupHeroWithCareer();
        _registry.GetMaxChoicesForHero(5).Returns(10);
        _dataService.TryAddChoice("hero1", "wb_brut_key", 10);

        var vm = CreateVM();
        vm.ExecuteSelectChoice("wb_brut_p1");

        Assert.IsTrue(_dataService.GetOrCreateData("hero1").HasChoice("wb_brut_p1"),
            "Passive choices in same tier should still be selectable");
    }

    // ── Preventive: Serialization Safety (root cause: vanilla API not researched) ──
    // These tests are in CareerPersistenceTests below.

    private void SetupHeroWithCareer()
    {
        _dataService.SetCareer("hero1", "warboss");
    }

    private CareerScreenVM CreateVM()
    {
        return new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, _logger, "hero1", 5, () => _closeCalled = true);
    }
}


2026-05-31T03:25:19.977281Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1.9 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class SpriteData|class SpriteCategory|class SpritePart|SpriteS …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 1874ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class SpriteData|class SpriteCategory|class SpritePart|SpriteS …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not available in this environment, so I’m switching to `Get-ChildItem` plus `Select-String`. The first C# read confirms the new constructor params are trailing optionals and the parser does pass the new XML attributes.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' | Select-String -Pattern 'class SpriteData','class SpriteCategory','class SpritePart','SpriteSheetSize','void Deserialize','static .*Deserialize' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml' -Pattern 'Tier2GateBottomHalf','Tier3GateTopHalf','Tier3GateFull','Tier1Locked','@','DataSource=\"\\{','Command\\.' | Select-Object LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerDefinition\\(','new CareerChoiceGroupDefinition\\(' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'TAOM.Tests' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerDefinition\\(','new CareerChoiceGroupDefinition\\(' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/GUI/TAOMSpriteData.xml' -Pattern '<Name>ui_taom_career_system</Name>','<SpriteSheetCount>2</SpriteSheetCount>','<SpriteSheetSize ID=\"2\" Width=\"256\" Height=\"256\" />','CareerSystem\\career_point_pip' -Context 0,8 | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 567ms:

[32;1mLineNumber : [0m25
[32;1mLine       : [0m            <RichTextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
             HorizontalAlignment="Center" VerticalAlignment="Top" MarginTop="18" Brush="Quest.CenterPanel.Title.Text" 
             Brush.FontSize="45" Text="@ScreenTitle" />

[32;1mLineNumber : [0m42
[32;1mLine       : [0m                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="50" 
             SuggestedWidth="400" HorizontalAlignment="Center" Text="@CareerName" MarginTop="15" 
             Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="24" />

[32;1mLineNumber : [0m45
[32;1mLine       : [0m                        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="200" 
             SuggestedWidth="400" HorizontalAlignment="Center" MarginTop="15" Sprite="@CareerPortraitSprite" 
             IsVisible="@HasCareer">

[32;1mLineNumber : [0m58
[32;1mLine       : [0m                                    <RichTextWidget WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="CoverChildren" Text="@CareerDescription" Brush="CharacterDeveloper.DescriptionText" 
             Brush.FontSize="18" />

[32;1mLineNumber : [0m70
[32;1mLine       : [0m                        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" 
             MarginTop="10" HorizontalAlignment="Center" Text="@AbilityLabel" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="24" />

[32;1mLineNumber : [0m73
[32;1mLine       : [0m                        <Widget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="120" 
             SuggestedWidth="120" HorizontalAlignment="Center" MarginTop="15" Sprite="@AbilitySpriteName" 
             IsVisible="@HasAbilitySprite">

[32;1mLineNumber : [0m80
[32;1mLine       : [0m                        <TextWidget WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" 
             MarginTop="10" HorizontalAlignment="Center" Text="@AbilityName" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="24" />

[32;1mLineNumber : [0m83
[32;1mLine       : [0m                        <ListPanel DataSource="{AbilityEffects}" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="CoverChildren" MarginTop="10" MarginLeft="20" MarginRight="20" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m85
[32;1mLine       : [0m                            <TextWidget WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="CoverChildren" Text="@LineText" Brush="CharacterDeveloper.DescriptionText" 
             Brush.FontSize="18" MarginTop="3" />

[32;1mLineNumber : [0m105
[32;1mLine       : [0m                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="200" Text="@Tier3Label" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="24" />

[32;1mLineNumber : [0m106
[32;1mLine       : [0m                            <ListPanel DataSource="{ChoiceGroupsTier3}" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" 
             StackLayout.LayoutMethod="HorizontalCentered">

[32;1mLineNumber : [0m110
[32;1mLine       : [0m                                    <ImageWidget VisualDefinition="ExtendablePanel" 
             DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" 
             Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">

[32;1mLineNumber : [0m112
[32;1mLine       : [0m                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" 
             PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />

[32;1mLineNumber : [0m116
[32;1mLine       : [0m                                            <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m120
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFFF" IsVisible="@IsTaken" />

[32;1mLineNumber : [0m121
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />

[32;1mLineNumber : [0m122
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFF45" IsVisible="@IsUnavailable" />

[32;1mLineNumber : [0m128
[32;1mLine       : [0m                                            <Widget WidthSizePolicy="CoverChildren" 
             HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">

[32;1mLineNumber : [0m130
[32;1mLine       : [0m                                                <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m134
[32;1mLine       : [0m                                                        <TextWidget DoNotAcceptEvents="true" 
             WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" 
             VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" 
             />

[32;1mLineNumber : [0m147
[32;1mLine       : [0m                                    <ListPanel HeightSizePolicy="CoverChildren" 
             WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">

[32;1mLineNumber : [0m149
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickIncrease" />

[32;1mLineNumber : [0m150
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickDecrease" />

[32;1mLineNumber : [0m158
[32;1mLine       : [0m                            <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Center" 
             Text="@Tier3RequirementText" IsVisible="@Tier3Locked" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="28" />

[32;1mLineNumber : [0m166
[32;1mLine       : [0m                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="200" Text="@Tier2Label" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="24" />

[32;1mLineNumber : [0m167
[32;1mLine       : [0m                            <ListPanel DataSource="{ChoiceGroupsTier2}" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" 
             StackLayout.LayoutMethod="HorizontalCentered">

[32;1mLineNumber : [0m171
[32;1mLine       : [0m                                    <ImageWidget VisualDefinition="ExtendablePanel" 
             DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" 
             Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">

[32;1mLineNumber : [0m173
[32;1mLine       : [0m                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" 
             PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />

[32;1mLineNumber : [0m176
[32;1mLine       : [0m                                            <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m180
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFFF" IsVisible="@IsTaken" />

[32;1mLineNumber : [0m181
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />

[32;1mLineNumber : [0m182
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFF45" IsVisible="@IsUnavailable" />

[32;1mLineNumber : [0m187
[32;1mLine       : [0m                                            <Widget WidthSizePolicy="CoverChildren" 
             HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">

[32;1mLineNumber : [0m189
[32;1mLine       : [0m                                                <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m193
[32;1mLine       : [0m                                                        <TextWidget DoNotAcceptEvents="true" 
             WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" 
             VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" 
             />

[32;1mLineNumber : [0m205
[32;1mLine       : [0m                                    <ListPanel HeightSizePolicy="CoverChildren" 
             WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">

[32;1mLineNumber : [0m207
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickIncrease" />

[32;1mLineNumber : [0m208
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickDecrease" />

[32;1mLineNumber : [0m215
[32;1mLine       : [0m                            <TextWidget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="CoverChildren" HorizontalAlignment="Center" VerticalAlignment="Center" 
             Text="@Tier2RequirementText" IsVisible="@Tier2Locked" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="28" />

[32;1mLineNumber : [0m223
[32;1mLine       : [0m                            <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="200" Text="@Tier1Label" Brush="CharacterDeveloper.SkillNameText" 
             Brush.FontSize="24" />

[32;1mLineNumber : [0m224
[32;1mLine       : [0m                            <ListPanel DataSource="{ChoiceGroupsTier1}" WidthSizePolicy="StretchToParent" 
             HeightSizePolicy="StretchToParent" HorizontalAlignment="Center" VerticalAlignment="Center" 
             StackLayout.LayoutMethod="HorizontalCentered">

[32;1mLineNumber : [0m228
[32;1mLine       : [0m                                    <ImageWidget VisualDefinition="ExtendablePanel" 
             DoNotPassEventsToChildren="true" WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             VerticalAlignment="Center" MarginTop="5" Sprite="StdAssets\Popup\canvas" 
             Command.HoverBegin="ExecuteBeginHover" Command.HoverEnd="ExecuteEndHover">

[32;1mLineNumber : [0m230
[32;1mLine       : [0m                                        <TextWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="30" SuggestedWidth="220" VerticalAlignment="Top" HorizontalAlignment="Center" 
             PositionYOffset="-35" Text="@GroupName" Brush="CharacterDeveloper.SkillNameText" Brush.FontSize="18" />

[32;1mLineNumber : [0m233
[32;1mLine       : [0m                                            <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m237
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFFF" IsVisible="@IsTaken" />

[32;1mLineNumber : [0m238
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFFA0" IsVisible="@IsFreeToTake" />

[32;1mLineNumber : [0m239
[32;1mLine       : [0m                                                    <Widget DoNotAcceptEvents="true" 
             WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="28" SuggestedWidth="22" 
             HorizontalAlignment="Center" VerticalAlignment="Center" Sprite="CareerSystem\career_point_pip" 
             Color="#FFFFFF45" IsVisible="@IsUnavailable" />

[32;1mLineNumber : [0m244
[32;1mLine       : [0m                                            <Widget WidthSizePolicy="CoverChildren" 
             HeightSizePolicy="CoverChildren" HorizontalAlignment="Left" IsVisible="@ButtonsVisible">

[32;1mLineNumber : [0m246
[32;1mLine       : [0m                                                <ListPanel DataSource="{Choices}" 
             WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
             StackLayout.LayoutMethod="VerticalTopToBottom">

[32;1mLineNumber : [0m250
[32;1mLine       : [0m                                                        <TextWidget DoNotAcceptEvents="true" 
             WidthSizePolicy="StretchToParent" HeightSizePolicy="StretchToParent" HorizontalAlignment="Left" 
             VerticalAlignment="Center" MarginLeft="15" Text="@Description" Brush="CharacterDeveloper.DescriptionText" 
             />

[32;1mLineNumber : [0m262
[32;1mLine       : [0m                                    <ListPanel HeightSizePolicy="CoverChildren" 
             WidthSizePolicy="CoverChildren" StackLayout.LayoutMethod="VerticalTopToBottom" IsVisible="@IsActive">

[32;1mLineNumber : [0m264
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\plus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickIncrease" />

[32;1mLineNumber : [0m265
[32;1mLine       : [0m                                        <ButtonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" 
             SuggestedHeight="50" SuggestedWidth="50" HorizontalAlignment="Left" Sprite="CareerSystem\minus_sign_icon" 
             MarginLeft="10" Command.Click="ExecuteClickDecrease" />

[32;1mLineNumber : [0m279
[32;1mLine       : [0m                    <TextWidget WidthSizePolicy="CoverChildren" HeightSizePolicy="CoverChildren" 
             HorizontalAlignment="Right" VerticalAlignment="Bottom" Text="@FreeCareerPointsText" MarginRight="100" />

[32;1mLineNumber : [0m291
[32;1mLine       : [0m        <Standard.DialogCloseButtons VisualDefinition="BottomMenu" HorizontalAlignment="Center" 
             VerticalAlignment="Bottom" PositionYOffset="100" Parameter.DoneButtonAction="ExecuteClose" 
             Parameter.DoneButtonText="@DoneLbl" Parameter.ShowCancel="false" />


 succeeded in 571ms:

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m15
[32;1mLine       : [0m      <SpriteSheetCount>2</SpriteSheetCount>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0m<SpriteSheetCount>2</SpriteSheetCount>
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m60
[32;1mLine       : [0m      <Name>ui_taom_career_system</Name>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0m<Name>ui_taom_career_system</Name>
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m62
[32;1mLine       : [0m      <SpriteSheetCount>2</SpriteSheetCount>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0m<SpriteSheetCount>2</SpriteSheetCount>
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m64
[32;1mLine       : [0m      <SpriteSheetSize ID="2" Width="256" Height="256" />
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0m<SpriteSheetSize ID="2" Width="256" Height="256" />
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}


 succeeded in 816ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m132
[32;1mLine       : [0m                    var career = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m201
[32;1mLine       : [0m                    var group = new CareerChoiceGroupDefinition(


 succeeded in 1760ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerAbilityServiceTests.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m    private static readonly CareerDefinition Career = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m35
[32;1mLine       : [0m    private static readonly CareerDefinition RangerCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCreationHandlerTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerPassiveServiceTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m22
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m30
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m131
[32;1mLine       : [0m        var tier2Group = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m156
[32;1mLine       : [0m        var dominionGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m        var expandedCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m46
[32;1mLine       : [0m        _registry.GetCareer("ranger").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m51
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m69
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m93
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m117
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CharacterCreation\CareerMenuServiceTests.cs
[32;1mLineNumber : [0m222
[32;1mLine       : [0m        return new CareerDefinition(


 succeeded in 29748ms:

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterObject.cs
[32;1mLineNumber : [0m536
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs
[32;1mLineNumber : [0m859
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Concept.cs
[32;1mLineNumber : [0m36
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CultureObject.cs
[32;1mLineNumber : [0m264
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1803
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs
[32;1mLineNumber : [0m753
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation\Convers
             ationSentence.cs
[32;1mLineNumber : [0m253
[32;1mLine       : [0m	public void Deserialize(XmlNode node, Type typeOfConversationCallbacks, ConversationManager 
             conversationManager, int defaultPriority)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus\WaitMenuOp
             tion.cs
[32;1mLineNumber : [0m79
[32;1mLine       : [0m	public void Deserialize(XmlNode node, Type typeOfWaitMenusCallbacks)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map.DistanceCache\Na
             vigationCache.cs
[32;1mLineNumber : [0m517
[32;1mLine       : [0m	public void Deserialize(string path)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyTemplateO
             bject.cs
[32;1mLineNumber : [0m26
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.
             cs
[32;1mLineNumber : [0m162
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Retireme
             ntSettlementComponent.cs
[32;1mLineNumber : [0m42
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settleme
             nt.cs
[32;1mLineNumber : [0m944
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settleme
             ntComponent.cs
[32;1mLineNumber : [0m139
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs
[32;1mLineNumber : [0m681
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.
             cs
[32;1mLineNumber : [0m282
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Location
             s\LocationComplexTemplate.cs
[32;1mLineNumber : [0m15
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements.Workshop
             s\WorkshopType.cs
[32;1mLineNumber : [0m112
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ArmorComponent.cs
[32;1mLineNumber : [0m146
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Banner.cs
[32;1mLineNumber : [0m240
[32;1mLine       : [0m	public void Deserialize(string message)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BannerComponent.cs
[32;1mLineNumber : [0m32
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BannerIconGroup.cs
[32;1mLineNumber : [0m34
[32;1mLine       : [0m	public void Deserialize(XmlNode xmlNode, MBList<BannerIconGroup> previouslyAddedGroups)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BasicCharacterObject.cs
[32;1mLineNumber : [0m315
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BasicCultureObject.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\BladeData.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode childNode)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Crafting.cs
[32;1mLineNumber : [0m1066
[32;1mLine       : [0m	public static ItemObject CreatePreCraftedWeaponOnDeserialize(ItemObject itemObject, 
             WeaponDesignElement[] usedPieces, string templateId, TextObject craftedWeaponName, ItemModifierGroup 
             itemModifierGroup)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\CraftingPiece.cs
[32;1mLineNumber : [0m153
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\CraftingTemplate.cs
[32;1mLineNumber : [0m144
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Equipment.cs
[32;1mLineNumber : [0m196
[32;1mLine       : [0m	public void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Equipment.cs
[32;1mLineNumber : [0m204
[32;1mLine       : [0m	public void DeserializeNode(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\HorseComponent.cs
[32;1mLineNumber : [0m141
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ItemComponent.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ItemModifier.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ItemModifierGroup.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ItemObject.cs
[32;1mLineNumber : [0m418
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBBodyProperty.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBCharacterSkills.cs
[32;1mLineNumber : [0m22
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MBEquipmentRoster.cs
[32;1mLineNumber : [0m56
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MissionShipObject.cs
[32;1mLineNumber : [0m96
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\MissionShipObject.cs
[32;1mLineNumber : [0m129
[32;1mLine       : [0m	private void DeserializeAux(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Monster.cs
[32;1mLineNumber : [0m189
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\Monster.cs
[32;1mLineNumber : [0m654
[32;1mLine       : [0m	private void DeserializeBoneIndexArray(List<sbyte> boneIndices, XmlNode node, bool hasBaseMonster, 
             string attributeNamePrefix, sbyte invalidBoneIndex, bool validateHasParentBone)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\PropertyOwner.cs
[32;1mLineNumber : [0m71
[32;1mLine       : [0m	public void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\PropertyOwnerF.cs
[32;1mLineNumber : [0m84
[32;1mLine       : [0m	public void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ShipHull.cs
[32;1mLineNumber : [0m87
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ShipPhysicsReference.cs
[32;1mLineNumber : [0m47
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ShipSlot.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\ShipUpgradePiece.cs
[32;1mLineNumber : [0m90
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\SiegeEngineType.cs
[32;1mLineNumber : [0m56
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\SkeletonScale.cs
[32;1mLineNumber : [0m27
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\TradeItemComponent.cs
[32;1mLineNumber : [0m36
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\WeaponComponent.cs
[32;1mLineNumber : [0m47
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\WeaponComponentData.cs
[32;1mLineNumber : [0m346
[32;1mLine       : [0m	public void Deserialize(ItemObject item, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\WeaponDescription.cs
[32;1mLineNumber : [0m26
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Core.ViewModelCollection\TaleWorlds.Core.ViewModelCollection.Info
             rmation\TooltipProperty.cs
[32;1mLineNumber : [0m234
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\AmbientInformation.cs
[32;1mLineNumber : [0m13
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\AreaInformation.cs
[32;1mLineNumber : [0m9
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\AtmosphereInfo.cs
[32;1mLineNumber : [0m45
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\Common.cs
[32;1mLineNumber : [0m207
[32;1mLine       : [0m	public static T DeserializeObjectFromJson<T>(string json)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\FogInformation.cs
[32;1mLineNumber : [0m11
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\ISerializableObject.cs
[32;1mLineNumber : [0m5
[32;1mLine       : [0m	void DeserializeFrom(IReader reader);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\NauticalInformation.cs
[32;1mLineNumber : [0m19
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\PostProcessInformation.cs
[32;1mLineNumber : [0m13
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\RainInformation.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\SkyInformation.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\SnowInformation.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\SunInformation.cs
[32;1mLineNumber : [0m19
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library\TimeInformation.cs
[32;1mLineNumber : [0m15
[32;1mLine       : [0m	public void DeserializeFrom(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Library\TaleWorlds.Library.NewsManager\NewsManager.cs
[32;1mLineNumber : [0m69
[32;1mLine       : [0m	public static Task<T> DeserializeObjectAsync<T>(string json)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Localization\TaleWorlds.Localization\LanguageData.cs
[32;1mLineNumber : [0m85
[32;1mLine       : [0m	private void Deserialize(XmlNode node, string modulePath)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Localization\TaleWorlds.Localization\LocalizedTextManager.cs
[32;1mLineNumber : [0m275
[32;1mLine       : [0m	private static void DeserializeStrings(XmlNode node, string languageId)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.Localization\TaleWorlds.Localization\VoiceObject.cs
[32;1mLineNumber : [0m34
[32;1mLine       : [0m	public static VoiceObject Deserialize(XmlNode node, string modulePath)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.ObjectSystem\TaleWorlds.ObjectSystem\MBObjectBase.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	public virtual void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.SaveSystem\TaleWorlds.SaveSystem\LegacyGameDataDeserializer.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0mpublic static class LegacyGameDataDeserializer

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.SaveSystem\TaleWorlds.SaveSystem\LegacyGameDataDeserializer.cs
[32;1mLineNumber : [0m9
[32;1mLine       : [0m	public static GameData Deserialize(Stream stream)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.SaveSystem\TaleWorlds.SaveSystem\MetaData.cs
[32;1mLineNumber : [0m54
[32;1mLine       : [0m	public static MetaData Deserialize(Stream stream)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Core\TaleWorlds.SaveSystem\TaleWorlds.SaveSystem.Load\LoadContext.cs
[32;1mLineNumber : [0m256
[32;1mLine       : [0m	private static string LoadString(ArchiveDeserializer saveArchive, int id)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m6
[32;1mLine       : [0mpublic class SpriteCategory

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m16
[32;1mLine       : [0m	public const int SpriteSheetSize = 4096;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m9
[32;1mLine       : [0mpublic class SpriteData

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m89
[32;1mLine       : [0m				if (childNode.Name == "SpriteSheetSize")

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m5
[32;1mLine       : [0mpublic class SpritePart

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects\InstrumentData.cs
[32;1mLineNumber : [0m42
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Objects\SettlementMusicData.cs
[32;1mLineNumber : [0m25
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode\TrainingField.cs
[32;1mLineNumber : [0m23
[32;1mLine       : [0m	public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MPOnSpawnPerkEffe
             ctBase.cs
[32;1mLineNumber : [0m20
[32;1mLine       : [0m	protected override void Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MPPerkCondition.c
             s
[32;1mLineNumber : [0m59
[32;1mLine       : [0m	protected abstract void Deserialize(XmlNode node);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MPPerkEffectBase.
             cs
[32;1mLineNumber : [0m147
[32;1mLine       : [0m	protected abstract void Deserialize(XmlNode node);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MPPerkObject.cs
[32;1mLineNumber : [0m1248
[32;1mLine       : [0m	public static IReadOnlyPerkObject Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MultiplayerClassD
             ivisions.cs
[32;1mLineNumber : [0m106
[32;1mLine       : [0m		public override void Deserialize(MBObjectManager objectManager, XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade.Network.Gameplay.
             Perks.Conditions\BannerBearerCondition.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m	protected override void Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.Diamond\TaleWorlds.MountAndBlade.Diamond\I
             temInnerData.cs
[32;1mLineNumber : [0m14
[32;1mLine       : [0m	internal void Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.Diamond\TaleWorlds.MountAndBlade.Diamond.M
             ultiplayerBadges\Badge.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	public virtual void Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.Diamond\TaleWorlds.MountAndBlade.Diamond.M
             ultiplayerBadges\ConditionalBadge.cs
[32;1mLineNumber : [0m15
[32;1mLine       : [0m	public override void Deserialize(XmlNode node)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Network\TaleWorlds.Diamond\TaleWorlds.Diamond.Socket\SocketMessage.cs
[32;1mLineNumber : [0m31
[32;1mLine       : [0m	public override void DeserializeFromNetworkMessage(INetworkMessageReader networkMessage)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Network\TaleWorlds.Network\TaleWorlds.Network\INetworkSerializable.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	void DeserializeFromNetworkMessage(INetworkMessageReader networkMessage);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Network\TaleWorlds.Network\TaleWorlds.Network\MessageContract.cs
[32;1mLineNumber : [0m74
[32;1mLine       : [0m	public abstract void DeserializeFromNetworkMessage(INetworkMessageReader networkMessage);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Network\TaleWorlds.PlayerServices\TaleWorlds.PlayerServices\PlayerId.cs
[32;1mLineNumber : [0m183
[32;1mLine       : [0m	public void Deserialize(IReader reader)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m348
[32;1mLine       : [0m	public static object? DeserializeObject(string value)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m354
[32;1mLine       : [0m	public static object? DeserializeObject(string value, JsonSerializerSettings settings)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m360
[32;1mLine       : [0m	public static object? DeserializeObject(string value, Type type)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m366
[32;1mLine       : [0m	public static T? DeserializeObject<T>(string value)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m372
[32;1mLine       : [0m	public static T? DeserializeAnonymousType<T>(string value, T anonymousTypeObject)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m378
[32;1mLine       : [0m	public static T? DeserializeAnonymousType<T>(string value, T anonymousTypeObject, JsonSerializerSettings 
             settings)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m384
[32;1mLine       : [0m	public static T? DeserializeObject<T>(string value, params JsonConverter[] converters)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m390
[32;1mLine       : [0m	public static T? DeserializeObject<T>(string value, JsonSerializerSettings? settings)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m396
[32;1mLine       : [0m	public static object? DeserializeObject(string value, Type type, params JsonConverter[] converters)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m405
[32;1mLine       : [0m	public static object? DeserializeObject(string value, Type? type, JsonSerializerSettings? settings)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m461
[32;1mLine       : [0m	public static XmlDocument? DeserializeXmlNode(string value)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m466
[32;1mLine       : [0m	public static XmlDocument? DeserializeXmlNode(string value, string? deserializeRootElementName)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m471
[32;1mLine       : [0m	public static XmlDocument? DeserializeXmlNode(string value, string? deserializeRootElementName, bool 
             writeArrayAttribute)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m476
[32;1mLine       : [0m	public static XmlDocument? DeserializeXmlNode(string value, string? deserializeRootElementName, bool 
             writeArrayAttribute, bool encodeSpecialCharacters)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m504
[32;1mLine       : [0m	public static XDocument? DeserializeXNode(string value)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m509
[32;1mLine       : [0m	public static XDocument? DeserializeXNode(string value, string? deserializeRootElementName)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m514
[32;1mLine       : [0m	public static XDocument? DeserializeXNode(string value, string? deserializeRootElementName, bool 
             writeArrayAttribute)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json\JsonConvert.cs
[32;1mLineNumber : [0m519
[32;1mLine       : [0m	public static XDocument? DeserializeXNode(string value, string? deserializeRootElementName, bool 
             writeArrayAttribute, bool encodeSpecialCharacters)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json.Converters\XmlNodeConverter.cs
[32;1mLineNumber : [0m489
[32;1mLine       : [0m	private void DeserializeValue(JsonReader reader, IXmlDocument document, XmlNamespaceManager manager, 
             string propertyName, IXmlNode currentNode)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json.Converters\XmlNodeConverter.cs
[32;1mLineNumber : [0m922
[32;1mLine       : [0m	private void DeserializeNode(JsonReader reader, IXmlDocument document, XmlNamespaceManager manager, 
             IXmlNode currentNode)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Newtonsoft.Json\Newtonsoft.Json.Serialization\DefaultContractResolver.
             cs
[32;1mLineNumber : [0m612
[32;1mLine       : [0m	private static bool ShouldSkipDeserialized(Type t)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Steamworks.NET\Steamworks\NativeMethods.cs
[32;1mLineNumber : [0m1344
[32;1mLine       : [0m	public static extern bool ISteamInventory_DeserializeResult(IntPtr instancePtr, out 
             SteamInventoryResult_t pOutResultHandle, byte[] pBuffer, uint unBufferSize, [MarshalAs(UnmanagedType.I1)] 
             bool bRESERVED_MUST_BE_FALSE);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Steamworks.NET\Steamworks\SteamGameServerInventory.cs
[32;1mLineNumber : [0m71
[32;1mLine       : [0m	public static bool DeserializeResult(out SteamInventoryResult_t pOutResultHandle, byte[] pBuffer, uint 
             unBufferSize, bool bRESERVED_MUST_BE_FALSE = false)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\ThirdParty\Steamworks.NET\Steamworks\SteamInventory.cs
[32;1mLineNumber : [0m71
[32;1mLine       : [0m	public static bool DeserializeResult(out SteamInventoryResult_t pOutResultHandle, byte[] pBuffer, uint 
             unBufferSize, bool bRESERVED_MUST_BE_FALSE = false)


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension\\TaleWorlds.TwoDimension\\SpriteData.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension\\TaleWorlds.TwoDimension\\SpritePart.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension\\TaleWorlds.TwoDimension\\SpriteCategory.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension\\TaleWorlds.TwoDimension\\SpriteSheet.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Command ilspycmd -ErrorAction SilentlyContinue | Format-List Source,Version; Get-ChildItem 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client' -Filter 'TaleWorlds.TwoDimension.dll' | Select-Object FullName,Length,LastWriteTime | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 528ms:
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpriteData
{
	private struct SpriteDataLoadResult
	{
		public Dictionary<string, SpritePart> SpritePartNames;

		public Dictionary<string, Sprite> SpriteNames;

		public Dictionary<string, SpriteCategory> SpriteCategories;
	}

	public Dictionary<string, SpritePart> SpriteParts { get; private set; }

	public Dictionary<string, Sprite> Sprites { get; private set; }

	public Dictionary<string, SpriteCategory> SpriteCategories { get; private set; }

	public string Name { get; private set; }

	public SpriteData(string name)
	{
		Name = name;
		SpriteParts = new Dictionary<string, SpritePart>();
		Sprites = new Dictionary<string, Sprite>();
		SpriteCategories = new Dictionary<string, SpriteCategory>();
	}

	public Sprite GetSprite(string name)
	{
		if (Sprites.TryGetValue(name, out var value))
		{
			return value;
		}
		return null;
	}

	public bool SpriteExists(string spriteName)
	{
		return GetSprite(spriteName) != null;
	}

	private static SpriteDataLoadResult LoadFromDepot(ResourceDepot resourceDepot, string name)
	{
		XmlDocument spriteData = new XmlDocument();
		SpriteDataLoadResult loadResult = new SpriteDataLoadResult
		{
			SpriteCategories = new Dictionary<string, SpriteCategory>(),
			SpriteNames = new Dictionary<string, Sprite>(),
			SpritePartNames = new Dictionary<string, SpritePart>()
		};
		foreach (string item in resourceDepot.GetFilesEndingWith(name + ".xml"))
		{
			try
			{
				LoadSpriteDataFromFile(spriteData, item, ref loadResult);
			}
			catch (Exception)
			{
				Debug.FailedAssert("Failed to load sprite data from file: " + item, "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.TwoDimension\\SpriteData.cs", "LoadFromDepot", 72);
			}
		}
		return loadResult;
	}

	private static SpriteDataLoadResult LoadSpriteDataFromFile(XmlDocument spriteData, string filePath, ref SpriteDataLoadResult loadResult)
	{
		StreamReader txtReader = new StreamReader(filePath);
		spriteData.Load(txtReader);
		XmlElement xmlElement = spriteData["SpriteData"];
		XmlNode xmlNode = xmlElement["SpriteCategories"];
		XmlNode xmlNode2 = xmlElement["SpriteParts"];
		XmlNode xmlNode3 = xmlElement["Sprites"];
		foreach (XmlNode item in xmlNode)
		{
			string innerText = item["Name"].InnerText;
			int num = Convert.ToInt32(item["SpriteSheetCount"].InnerText);
			bool alwaysLoad = false;
			Vec2i[] array = new Vec2i[num];
			foreach (XmlNode childNode in item.ChildNodes)
			{
				if (childNode.Name == "SpriteSheetSize")
				{
					int num2 = Convert.ToInt32(childNode.Attributes["ID"].InnerText);
					int x = Convert.ToInt32(childNode.Attributes["Width"].InnerText);
					int y = Convert.ToInt32(childNode.Attributes["Height"].InnerText);
					array[num2 - 1] = new Vec2i(x, y);
				}
				else if (childNode.Name == "AlwaysLoad")
				{
					alwaysLoad = true;
				}
			}
			SpriteCategory spriteCategory = new SpriteCategory(innerText, num, alwaysLoad)
			{
				SheetSizes = array
			};
			loadResult.SpriteCategories[spriteCategory.Name] = spriteCategory;
		}
		foreach (XmlNode item2 in xmlNode2)
		{
			string innerText2 = item2["Name"].InnerText;
			int width = Convert.ToInt32(item2["Width"].InnerText);
			int height = Convert.ToInt32(item2["Height"].InnerText);
			string innerText3 = item2["CategoryName"].InnerText;
			SpriteCategory category = loadResult.SpriteCategories[innerText3];
			SpritePart spritePart = new SpritePart(innerText2, category, width, height)
			{
				SheetID = Convert.ToInt32(item2["SheetID"].InnerText),
				SheetX = Convert.ToInt32(item2["SheetX"].InnerText),
				SheetY = Convert.ToInt32(item2["SheetY"].InnerText)
			};
			loadResult.SpritePartNames[spritePart.Name] = spritePart;
			spritePart.UpdateInitValues();
		}
		foreach (XmlNode item3 in xmlNode3)
		{
			Sprite sprite = null;
			if (item3.Name == "GenericSprite")
			{
				string innerText4 = item3["Name"].InnerText;
				string innerText5 = item3["SpritePartName"].InnerText;
				SpritePart spritePart2 = loadResult.SpritePartNames[innerText5];
				sprite = new SpriteGeneric(innerText4, spritePart2, in SpriteNinePatchParameters.Empty);
			}
			else if (item3.Name == "NineRegionSprite")
			{
				string innerText6 = item3["Name"].InnerText;
				string innerText7 = item3["SpritePartName"].InnerText;
				int leftWidth = Convert.ToInt32(item3["LeftWidth"].InnerText);
				int rightWidth = Convert.ToInt32(item3["RightWidth"].InnerText);
				int topHeight = Convert.ToInt32(item3["TopHeight"].InnerText);
				int bottomHeight = Convert.ToInt32(item3["BottomHeight"].InnerText);
				SpriteNinePatchParameters ninePatchParameters = new SpriteNinePatchParameters(leftWidth, rightWidth, topHeight, bottomHeight);
				sprite = new SpriteGeneric(innerText6, loadResult.SpritePartNames[innerText7], in ninePatchParameters);
			}
			loadResult.SpriteNames[sprite.Name] = sprite;
		}
		return loadResult;
	}

	public void Load(ResourceDepot resourceDepot)
	{
		SpriteDataLoadResult spriteDataLoadResult = LoadFromDepot(resourceDepot, Name);
		SpriteCategories = spriteDataLoadResult.SpriteCategories;
		Sprites = spriteDataLoadResult.SpriteNames;
		SpriteParts = spriteDataLoadResult.SpritePartNames;
	}

	public void Reload(ResourceDepot resourceDepot, ITwoDimensionResourceContext resourceContext)
	{
		SpriteDataLoadResult spriteDataLoadResult = LoadFromDepot(resourceDepot, Name);
		Sprites = spriteDataLoadResult.SpriteNames;
		SpriteParts = spriteDataLoadResult.SpritePartNames;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory2 in SpriteCategories)
		{
			bool flag = false;
			foreach (KeyValuePair<string, SpriteCategory> spriteCategory3 in spriteDataLoadResult.SpriteCategories)
			{
				if (spriteCategory3.Key == spriteCategory2.Key)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(spriteCategory2.Key);
			}
		}
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory4 in spriteDataLoadResult.SpriteCategories)
		{
			bool flag2 = false;
			foreach (KeyValuePair<string, SpriteCategory> spriteCategory5 in SpriteCategories)
			{
				if (spriteCategory4.Key == spriteCategory5.Key)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				list2.Add(spriteCategory4.Key);
			}
		}
		foreach (string item in list)
		{
			SpriteCategories[item].Unload();
			SpriteCategories.Remove(item);
		}
		foreach (string item2 in list2)
		{
			SpriteCategory spriteCategory = spriteDataLoadResult.SpriteCategories[item2];
			SpriteCategories.Add(item2, spriteCategory);
			if (spriteCategory.AlwaysLoad)
			{
				spriteCategory.Load(resourceContext, resourceDepot);
			}
		}
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory6 in SpriteCategories)
		{
			if (spriteDataLoadResult.SpriteCategories.TryGetValue(spriteCategory6.Key, out var value))
			{
				spriteCategory6.Value.Reload(resourceContext, resourceDepot, value);
			}
		}
		foreach (KeyValuePair<string, SpritePart> spritePart in SpriteParts)
		{
			if (SpriteCategories.TryGetValue(spritePart.Value.Category.Name, out var value2))
			{
				spritePart.Value.Category = value2;
			}
		}
	}
}


 succeeded in 512ms:
using System.Collections.Generic;
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpriteCategory
{
	protected class SpriteSizeComparer : IComparer<SpritePart>
	{
		public int Compare(SpritePart x, SpritePart y)
		{
			return y.Width * y.Height - x.Width * x.Height;
		}
	}

	public const int SpriteSheetSize = 4096;

	public readonly bool AlwaysLoad;

	private SpriteSizeComparer _spritePartComparer;

	public string Name { get; private set; }

	public List<SpritePart> SpriteParts { get; private set; }

	public List<SpritePart> SortedSpritePartList { get; private set; }

	public List<Texture> SpriteSheets { get; private set; }

	public int SpriteSheetCount { get; set; }

	public bool IsLoaded { get; private set; }

	public bool IsPartiallyLoaded { get; private set; }

	public Vec2i[] SheetSizes { get; set; }

	public SpriteCategory(string name, int spriteSheetCount, bool alwaysLoad = false)
	{
		Name = name;
		SpriteSheetCount = spriteSheetCount;
		AlwaysLoad = alwaysLoad;
		SpriteSheets = new List<Texture>();
		SpriteParts = new List<SpritePart>();
		SortedSpritePartList = new List<SpritePart>();
		SheetSizes = new Vec2i[spriteSheetCount];
		_spritePartComparer = new SpriteSizeComparer();
	}

	public void Load(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot)
	{
		if (!IsLoaded)
		{
			IsLoaded = true;
			IsPartiallyLoaded = false;
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				Texture item = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" + i);
				SpriteSheets.Add(item);
			}
		}
	}

	public void Unload()
	{
		if (IsLoaded)
		{
			SpriteSheets.ForEach(delegate(Texture s)
			{
				s.PlatformTexture.Release();
			});
			SpriteSheets.Clear();
			IsLoaded = false;
			IsPartiallyLoaded = false;
		}
	}

	public void Reload(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot, SpriteCategory newCategoryInfo)
	{
		if (!IsLoaded)
		{
			return;
		}
		SpriteParts = newCategoryInfo.SpriteParts;
		SheetSizes = newCategoryInfo.SheetSizes;
		SortList();
		if (IsPartiallyLoaded)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < SpriteSheetCount; i++)
			{
				if (SpriteSheets[i] != null)
				{
					list.Add(i + 1);
					PartialUnloadAtIndex(i + 1);
				}
			}
			for (int j = 0; j < list.Count; j++)
			{
				PartialLoadAtIndex(resourceContext, resourceDepot, list[j]);
			}
		}
		else
		{
			Unload();
			Load(resourceContext, resourceDepot);
		}
	}

	public void InitializePartialLoad()
	{
		if (!IsLoaded)
		{
			IsLoaded = true;
			IsPartiallyLoaded = true;
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				SpriteSheets.Add(null);
			}
		}
	}

	public void ReleasePartialLoad()
	{
		if (IsLoaded)
		{
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				PartialUnloadAtIndex(i);
			}
			SpriteSheets.Clear();
			IsLoaded = false;
			IsPartiallyLoaded = false;
		}
	}

	public void PartialLoadAtIndex(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot, int sheetIndex)
	{
		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] == null)
		{
			Texture value = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" + sheetIndex);
			SpriteSheets[sheetIndex - 1] = value;
		}
	}

	public void PartialUnloadAtIndex(int sheetIndex)
	{
		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] != null)
		{
			SpriteSheets[sheetIndex - 1].PlatformTexture.Release();
			SpriteSheets[sheetIndex - 1] = null;
		}
	}

	public void SortList()
	{
		SortedSpritePartList.Clear();
		SortedSpritePartList.AddRange(SpriteParts);
		SortedSpritePartList.Sort(_spritePartComparer);
	}

	public bool IsCategoryFullyLoaded()
	{
		for (int i = 0; i < SpriteSheets.Count; i++)
		{
			Texture texture = SpriteSheets[i];
			if (texture == null || !texture.IsLoaded())
			{
				return false;
			}
		}
		return true;
	}
}


 succeeded in 526ms:
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpritePart
{
	private SpriteCategory _category;

	public string Name { get; private set; }

	public int Width { get; private set; }

	public int Height { get; private set; }

	public int SheetID { get; set; }

	public int SheetX { get; set; }

	public int SheetY { get; set; }

	public float MinU { get; private set; }

	public float MinV { get; private set; }

	public float MaxU { get; private set; }

	public float MaxV { get; private set; }

	public int SheetWidth { get; private set; }

	public int SheetHeight { get; private set; }

	public Texture Texture
	{
		get
		{
			SpriteCategory category = _category;
			if (category != null && category.IsLoaded && _category.SpriteSheets?.Count >= SheetID)
			{
				return _category.SpriteSheets[SheetID - 1];
			}
			return null;
		}
	}

	public SpriteCategory Category
	{
		get
		{
			return _category;
		}
		internal set
		{
			_category = value;
		}
	}

	public SpritePart(string name, SpriteCategory category, int width, int height)
	{
		Name = name;
		Width = width;
		Height = height;
		_category = category;
		_category.SpriteParts.Add(this);
	}

	public void UpdateInitValues()
	{
		Vec2i vec2i = _category.SheetSizes[SheetID - 1];
		SheetWidth = vec2i.X;
		SheetHeight = vec2i.Y;
		double num = 1.0 / (double)SheetWidth;
		double num2 = 1.0 / (double)SheetHeight;
		double num3 = (double)SheetX * num;
		double num4 = (double)(SheetX + Width) * num;
		double num5 = (double)SheetY * num2;
		double num6 = (double)(SheetY + Height) * num2;
		MinU = (float)num3;
		MaxU = (float)num4;
		MinV = (float)num5;
		MaxV = (float)num6;
	}
}


 succeeded in 554ms:

[32;1mSource  : [0mC:\Users\mikew\.dotnet\tools\ilspycmd.exe
[32;1mVersion : [0m10.0.1.8346


[32;1mFullName      : [0mE:\Steam\steamapps\common\Mount & Blade II 
                Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.TwoDimension.dll
[32;1mLength        : [0m97216
[32;1mLastWriteTime : [0m5/30/2026 6:53:00 AM


2026-05-31T03:26:18.700788Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-Content -Raw 'E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimen[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteSheet.cs' because it does not exist.[0m

 exited 1 in 566ms:
[31;1mGet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-Content -Raw 'E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimen[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCannot find path 'E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteSheet.cs' because it does not exist.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.TwoDimension.dll' -t TaleWorlds.TwoDimension.SpriteData" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.TwoDimension.dll' -t TaleWorlds.TwoDimension.SpriteCategory" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Engine' -Recurse -Filter '*.cs' | Select-String -Pattern 'LoadTexture\\(','SpriteSheets\\\\' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.TwoDimension.dll' -t TaleWorlds.TwoDimension.SpritePart" in C:\Users\mikew\source\repos\TAOM
 succeeded in 579ms:

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine\Texture.cs
[32;1mLineNumber : [0m90
[32;1mLine       : [0m	public void PreloadTexture(bool blocking)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\TwoDimensionEngi
             neResourceContext.cs
[32;1mLineNumber : [0m8
[32;1mLine       : [0m	TaleWorlds.TwoDimension.Texture ITwoDimensionResourceContext.LoadTexture(ResourceDepot resourceDepot, 
             string name)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\TwoDimensionEngi
             neResourceContext.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m		fromResource.PreloadTexture(flag);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\ITwoDimensionResourceConte
             xt.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	Texture LoadTexture(ResourceDepot resourceDepot, string name);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m				Texture item = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" 
             + i);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m141
[32;1mLine       : [0m			Texture value = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" 
             + sheetIndex);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\TwoDimensionContext.cs
[32;1mLineNumber : [0m94
[32;1mLine       : [0m	public Texture LoadTexture(string name)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\TwoDimensionContext.cs
[32;1mLineNumber : [0m96
[32;1mLine       : [0m		return ResourceContext.LoadTexture(ResourceDepot, name);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension.Standalone\TaleWorlds.TwoDimension.Standalone\Grap
             hicsContext.cs
[32;1mLineNumber : [0m564
[32;1mLine       : [0m	public OpenGLTexture LoadTexture(ResourceDepot resourceDepot, string name)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension.Standalone\TaleWorlds.TwoDimension.Standalone\TwoD
             imensionPlatform.cs
[32;1mLineNumber : [0m59
[32;1mLine       : [0m	Texture ITwoDimensionResourceContext.LoadTexture(ResourceDepot resourceDepot, string name)


 succeeded in 1289ms:
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpritePart
{
	private SpriteCategory _category;

	public string Name { get; private set; }

	public int Width { get; private set; }

	public int Height { get; private set; }

	public int SheetID { get; set; }

	public int SheetX { get; set; }

	public int SheetY { get; set; }

	public float MinU { get; private set; }

	public float MinV { get; private set; }

	public float MaxU { get; private set; }

	public float MaxV { get; private set; }

	public int SheetWidth { get; private set; }

	public int SheetHeight { get; private set; }

	public Texture Texture
	{
		get
		{
			SpriteCategory category = _category;
			if (category != null && category.IsLoaded && _category.SpriteSheets?.Count >= SheetID)
			{
				return _category.SpriteSheets[SheetID - 1];
			}
			return null;
		}
	}

	public SpriteCategory Category
	{
		get
		{
			return _category;
		}
		internal set
		{
			_category = value;
		}
	}

	public SpritePart(string name, SpriteCategory category, int width, int height)
	{
		Name = name;
		Width = width;
		Height = height;
		_category = category;
		_category.SpriteParts.Add(this);
	}

	public void UpdateInitValues()
	{
		Vec2i vec2i = _category.SheetSizes[SheetID - 1];
		SheetWidth = vec2i.X;
		SheetHeight = vec2i.Y;
		double num = 1.0 / (double)SheetWidth;
		double num2 = 1.0 / (double)SheetHeight;
		double num3 = (double)SheetX * num;
		double num4 = (double)(SheetX + Width) * num;
		double num5 = (double)SheetY * num2;
		double num6 = (double)(SheetY + Height) * num2;
		MinU = (float)num3;
		MaxU = (float)num4;
		MinV = (float)num5;
		MaxV = (float)num6;
	}
}

 succeeded in 1406ms:
using System.Collections.Generic;
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpriteCategory
{
	protected class SpriteSizeComparer : IComparer<SpritePart>
	{
		public int Compare(SpritePart x, SpritePart y)
		{
			return y.Width * y.Height - x.Width * x.Height;
		}
	}

	public const int SpriteSheetSize = 4096;

	public readonly bool AlwaysLoad;

	private SpriteSizeComparer _spritePartComparer;

	public string Name { get; private set; }

	public List<SpritePart> SpriteParts { get; private set; }

	public List<SpritePart> SortedSpritePartList { get; private set; }

	public List<Texture> SpriteSheets { get; private set; }

	public int SpriteSheetCount { get; set; }

	public bool IsLoaded { get; private set; }

	public bool IsPartiallyLoaded { get; private set; }

	public Vec2i[] SheetSizes { get; set; }

	public SpriteCategory(string name, int spriteSheetCount, bool alwaysLoad = false)
	{
		Name = name;
		SpriteSheetCount = spriteSheetCount;
		AlwaysLoad = alwaysLoad;
		SpriteSheets = new List<Texture>();
		SpriteParts = new List<SpritePart>();
		SortedSpritePartList = new List<SpritePart>();
		SheetSizes = new Vec2i[spriteSheetCount];
		_spritePartComparer = new SpriteSizeComparer();
	}

	public void Load(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot)
	{
		if (!IsLoaded)
		{
			IsLoaded = true;
			IsPartiallyLoaded = false;
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				Texture item = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" + i);
				SpriteSheets.Add(item);
			}
		}
	}

	public void Unload()
	{
		if (IsLoaded)
		{
			SpriteSheets.ForEach(delegate(Texture s)
			{
				s.PlatformTexture.Release();
			});
			SpriteSheets.Clear();
			IsLoaded = false;
			IsPartiallyLoaded = false;
		}
	}

	public void Reload(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot, SpriteCategory newCategoryInfo)
	{
		if (!IsLoaded)
		{
			return;
		}
		SpriteParts = newCategoryInfo.SpriteParts;
		SheetSizes = newCategoryInfo.SheetSizes;
		SortList();
		if (IsPartiallyLoaded)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < SpriteSheetCount; i++)
			{
				if (SpriteSheets[i] != null)
				{
					list.Add(i + 1);
					PartialUnloadAtIndex(i + 1);
				}
			}
			for (int j = 0; j < list.Count; j++)
			{
				PartialLoadAtIndex(resourceContext, resourceDepot, list[j]);
			}
		}
		else
		{
			Unload();
			Load(resourceContext, resourceDepot);
		}
	}

	public void InitializePartialLoad()
	{
		if (!IsLoaded)
		{
			IsLoaded = true;
			IsPartiallyLoaded = true;
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				SpriteSheets.Add(null);
			}
		}
	}

	public void ReleasePartialLoad()
	{
		if (IsLoaded)
		{
			for (int i = 1; i <= SpriteSheetCount; i++)
			{
				PartialUnloadAtIndex(i);
			}
			SpriteSheets.Clear();
			IsLoaded = false;
			IsPartiallyLoaded = false;
		}
	}

	public void PartialLoadAtIndex(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot, int sheetIndex)
	{
		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] == null)
		{
			Texture value = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" + sheetIndex);
			SpriteSheets[sheetIndex - 1] = value;
		}
	}

	public void PartialUnloadAtIndex(int sheetIndex)
	{
		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] != null)
		{
			SpriteSheets[sheetIndex - 1].PlatformTexture.Release();
			SpriteSheets[sheetIndex - 1] = null;
		}
	}

	public void SortList()
	{
		SortedSpritePartList.Clear();
		SortedSpritePartList.AddRange(SpriteParts);
		SortedSpritePartList.Sort(_spritePartComparer);
	}

	public bool IsCategoryFullyLoaded()
	{
		for (int i = 0; i < SpriteSheets.Count; i++)
		{
			Texture texture = SpriteSheets[i];
			if (texture == null || !texture.IsLoaded())
			{
				return false;
			}
		}
		return true;
	}
}

 succeeded in 1519ms:
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class SpriteData
{
	private struct SpriteDataLoadResult
	{
		public Dictionary<string, SpritePart> SpritePartNames;

		public Dictionary<string, Sprite> SpriteNames;

		public Dictionary<string, SpriteCategory> SpriteCategories;
	}

	public Dictionary<string, SpritePart> SpriteParts { get; private set; }

	public Dictionary<string, Sprite> Sprites { get; private set; }

	public Dictionary<string, SpriteCategory> SpriteCategories { get; private set; }

	public string Name { get; private set; }

	public SpriteData(string name)
	{
		Name = name;
		SpriteParts = new Dictionary<string, SpritePart>();
		Sprites = new Dictionary<string, Sprite>();
		SpriteCategories = new Dictionary<string, SpriteCategory>();
	}

	public Sprite GetSprite(string name)
	{
		if (Sprites.TryGetValue(name, out var value))
		{
			return value;
		}
		return null;
	}

	public bool SpriteExists(string spriteName)
	{
		return GetSprite(spriteName) != null;
	}

	private static SpriteDataLoadResult LoadFromDepot(ResourceDepot resourceDepot, string name)
	{
		XmlDocument spriteData = new XmlDocument();
		SpriteDataLoadResult loadResult = new SpriteDataLoadResult
		{
			SpriteCategories = new Dictionary<string, SpriteCategory>(),
			SpriteNames = new Dictionary<string, Sprite>(),
			SpritePartNames = new Dictionary<string, SpritePart>()
		};
		foreach (string item in resourceDepot.GetFilesEndingWith(name + ".xml"))
		{
			try
			{
				LoadSpriteDataFromFile(spriteData, item, ref loadResult);
			}
			catch (Exception)
			{
				Debug.FailedAssert("Failed to load sprite data from file: " + item, "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.TwoDimension\\SpriteData.cs", "LoadFromDepot", 72);
			}
		}
		return loadResult;
	}

	private static SpriteDataLoadResult LoadSpriteDataFromFile(XmlDocument spriteData, string filePath, ref SpriteDataLoadResult loadResult)
	{
		StreamReader txtReader = new StreamReader(filePath);
		spriteData.Load(txtReader);
		XmlElement xmlElement = spriteData["SpriteData"];
		XmlNode xmlNode = xmlElement["SpriteCategories"];
		XmlNode xmlNode2 = xmlElement["SpriteParts"];
		XmlNode xmlNode3 = xmlElement["Sprites"];
		foreach (XmlNode item in xmlNode)
		{
			string innerText = item["Name"].InnerText;
			int num = Convert.ToInt32(item["SpriteSheetCount"].InnerText);
			bool alwaysLoad = false;
			Vec2i[] array = new Vec2i[num];
			foreach (XmlNode childNode in item.ChildNodes)
			{
				if (childNode.Name == "SpriteSheetSize")
				{
					int num2 = Convert.ToInt32(childNode.Attributes["ID"].InnerText);
					int x = Convert.ToInt32(childNode.Attributes["Width"].InnerText);
					int y = Convert.ToInt32(childNode.Attributes["Height"].InnerText);
					array[num2 - 1] = new Vec2i(x, y);
				}
				else if (childNode.Name == "AlwaysLoad")
				{
					alwaysLoad = true;
				}
			}
			SpriteCategory spriteCategory = new SpriteCategory(innerText, num, alwaysLoad)
			{
				SheetSizes = array
			};
			loadResult.SpriteCategories[spriteCategory.Name] = spriteCategory;
		}
		foreach (XmlNode item2 in xmlNode2)
		{
			string innerText2 = item2["Name"].InnerText;
			int width = Convert.ToInt32(item2["Width"].InnerText);
			int height = Convert.ToInt32(item2["Height"].InnerText);
			string innerText3 = item2["CategoryName"].InnerText;
			SpriteCategory category = loadResult.SpriteCategories[innerText3];
			SpritePart spritePart = new SpritePart(innerText2, category, width, height)
			{
				SheetID = Convert.ToInt32(item2["SheetID"].InnerText),
				SheetX = Convert.ToInt32(item2["SheetX"].InnerText),
				SheetY = Convert.ToInt32(item2["SheetY"].InnerText)
			};
			loadResult.SpritePartNames[spritePart.Name] = spritePart;
			spritePart.UpdateInitValues();
		}
		foreach (XmlNode item3 in xmlNode3)
		{
			Sprite sprite = null;
			if (item3.Name == "GenericSprite")
			{
				string innerText4 = item3["Name"].InnerText;
				string innerText5 = item3["SpritePartName"].InnerText;
				SpritePart spritePart2 = loadResult.SpritePartNames[innerText5];
				sprite = new SpriteGeneric(innerText4, spritePart2, in SpriteNinePatchParameters.Empty);
			}
			else if (item3.Name == "NineRegionSprite")
			{
				string innerText6 = item3["Name"].InnerText;
				string innerText7 = item3["SpritePartName"].InnerText;
				int leftWidth = Convert.ToInt32(item3["LeftWidth"].InnerText);
				int rightWidth = Convert.ToInt32(item3["RightWidth"].InnerText);
				int topHeight = Convert.ToInt32(item3["TopHeight"].InnerText);
				int bottomHeight = Convert.ToInt32(item3["BottomHeight"].InnerText);
				SpriteNinePatchParameters ninePatchParameters = new SpriteNinePatchParameters(leftWidth, rightWidth, topHeight, bottomHeight);
				sprite = new SpriteGeneric(innerText6, loadResult.SpritePartNames[innerText7], in ninePatchParameters);
			}
			loadResult.SpriteNames[sprite.Name] = sprite;
		}
		return loadResult;
	}

	public void Load(ResourceDepot resourceDepot)
	{
		SpriteDataLoadResult spriteDataLoadResult = LoadFromDepot(resourceDepot, Name);
		SpriteCategories = spriteDataLoadResult.SpriteCategories;
		Sprites = spriteDataLoadResult.SpriteNames;
		SpriteParts = spriteDataLoadResult.SpritePartNames;
	}

	public void Reload(ResourceDepot resourceDepot, ITwoDimensionResourceContext resourceContext)
	{
		SpriteDataLoadResult spriteDataLoadResult = LoadFromDepot(resourceDepot, Name);
		Sprites = spriteDataLoadResult.SpriteNames;
		SpriteParts = spriteDataLoadResult.SpritePartNames;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory2 in SpriteCategories)
		{
			bool flag = false;
			foreach (KeyValuePair<string, SpriteCategory> spriteCategory3 in spriteDataLoadResult.SpriteCategories)
			{
				if (spriteCategory3.Key == spriteCategory2.Key)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(spriteCategory2.Key);
			}
		}
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory4 in spriteDataLoadResult.SpriteCategories)
		{
			bool flag2 = false;
			foreach (KeyValuePair<string, SpriteCategory> spriteCategory5 in SpriteCategories)
			{
				if (spriteCategory4.Key == spriteCategory5.Key)
				{
					flag2 = true;
					break;
				}
			}
			if (!flag2)
			{
				list2.Add(spriteCategory4.Key);
			}
		}
		foreach (string item in list)
		{
			SpriteCategories[item].Unload();
			SpriteCategories.Remove(item);
		}
		foreach (string item2 in list2)
		{
			SpriteCategory spriteCategory = spriteDataLoadResult.SpriteCategories[item2];
			SpriteCategories.Add(item2, spriteCategory);
			if (spriteCategory.AlwaysLoad)
			{
				spriteCategory.Load(resourceContext, resourceDepot);
			}
		}
		foreach (KeyValuePair<string, SpriteCategory> spriteCategory6 in SpriteCategories)
		{
			if (spriteDataLoadResult.SpriteCategories.TryGetValue(spriteCategory6.Key, out var value))
			{
				spriteCategory6.Value.Reload(resourceContext, resourceDepot, value);
			}
		}
		foreach (KeyValuePair<string, SpritePart> spritePart in SpriteParts)
		{
			if (SpriteCategories.TryGetValue(spritePart.Value.Category.Name, out var value2))
			{
				spritePart.Value.Category = value2;
			}
		}
	}
}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' | Select-String -Pattern 'SpriteParts','SpriteSheets','Generate.*Sprite','SpriteData\\(','SpriteCategory' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.Engine.GauntletUI\\TaleWorlds.Engine.GauntletUI\\TwoDimensionEngineResourceContext.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension\\TaleWorlds.TwoDimension\\TwoDimensionContext.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'docs/features/gui-sprite-system.md'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 513ms:
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace TaleWorlds.Engine.GauntletUI;

public class TwoDimensionEngineResourceContext : ITwoDimensionResourceContext
{
	TaleWorlds.TwoDimension.Texture ITwoDimensionResourceContext.LoadTexture(ResourceDepot resourceDepot, string name)
	{
		Texture fromResource = Texture.GetFromResource(name.Split(new char[1] { '\\' })[^1]);
		if (fromResource == null)
		{
			return null;
		}
		fromResource.SetTextureAsAlwaysValid();
		bool flag = true;
		flag = true;
		fromResource.PreloadTexture(flag);
		return new TaleWorlds.TwoDimension.Texture(new EngineTexture(fromResource));
	}
}


 succeeded in 471ms:
using System.Runtime.CompilerServices;
using TaleWorlds.Library;

namespace TaleWorlds.TwoDimension;

public class TwoDimensionContext
{
	public float Width => Platform.Width;

	public float Height => Platform.Height;

	public ITwoDimensionPlatform Platform { get; private set; }

	public ITwoDimensionResourceContext ResourceContext { get; private set; }

	public ResourceDepot ResourceDepot { get; private set; }

	public bool IsDebugModeEnabled => Platform.IsDebugModeEnabled();

	public TwoDimensionContext(ITwoDimensionPlatform platform, ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot)
	{
		ResourceDepot = resourceDepot;
		Platform = platform;
		ResourceContext = resourceContext;
	}

	public void PlaySound(string soundName)
	{
		Platform.PlaySound(soundName);
	}

	public void CreateSoundEvent(string soundName)
	{
		Platform.CreateSoundEvent(soundName);
	}

	public void StopAndRemoveSoundEvent(string soundName)
	{
		Platform.StopAndRemoveSoundEvent(soundName);
	}

	public void PlaySoundEvent(string soundName)
	{
		Platform.PlaySoundEvent(soundName);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DrawImage(SimpleMaterial material, in ImageDrawObject drawObject2D, int layer = 0)
	{
		Platform.DrawImage(material, in drawObject2D, layer);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DrawText(TextMaterial material, in TextDrawObject drawObject2D, int layer = 0)
	{
		Platform.DrawText(material, in drawObject2D, layer);
	}

	public void BeginDebugPanel(string panelTitle)
	{
		Platform.BeginDebugPanel(panelTitle);
	}

	public void EndDebugPanel()
	{
		Platform.EndDebugPanel();
	}

	public void DrawDebugText(string text)
	{
		Platform.DrawDebugText(text);
	}

	public bool DrawDebugTreeNode(string text)
	{
		return Platform.DrawDebugTreeNode(text);
	}

	public void PopDebugTreeNode()
	{
		Platform.PopDebugTreeNode();
	}

	public void DrawCheckbox(string label, ref bool isChecked)
	{
		Platform.DrawCheckbox(label, ref isChecked);
	}

	public bool IsDebugItemHovered()
	{
		return Platform.IsDebugItemHovered();
	}

	public Texture LoadTexture(string name)
	{
		return ResourceContext.LoadTexture(ResourceDepot, name);
	}

	public void SetScissor(ScissorTestInfo scissor)
	{
		Platform.SetScissor(scissor);
	}

	public void ResetScissor()
	{
		Platform.ResetScissors();
	}
}


 succeeded in 501ms:
# GUI & Sprite System

**Status:** Verified in-game (2026-04-14, Gondor campaign). Career button sprite, map bar resource display with tooltip, and shader precompilation all confirmed working.

## Overview

TAOM's UI layer uses Bannerlord's Gauntlet UI framework with UIExtenderEx for injecting into vanilla screens. Sprites are PNG source images compiled into sprite sheets by the game engine. All UI data binding uses the `@PropertyName` / `{DataSourceName}` pattern from Gauntlet XML prefabs backed by `ViewModel` subclasses with `[DataSourceProperty]` attributes.

## Why This Exists

- **Vanilla behavior:** No career screen, no special resource display, no per-faction UI elements
- **TAOM requirement:** Career choice trees, resource tracking on map bar, faction-specific icons
- **Without this feature:** Players have no way to interact with careers or see resource status

## Sprite Pipeline

### How It Works

```
GUI/SpriteParts/ui_taom/<subfolder>/*.png    ← Source PNGs (any resolution)
        ↓ (game engine sprite compiler)
GUI/SpriteData/ui_taom/                      ← Compiled sprite sheets
        ↓ (referenced by)
GUI/TAOMSpriteData.xml                       ← Declares categories + sheet dimensions
        ↓ (loaded at runtime by)
Context.SpriteData.GetSprite("sprite_name")  ← C# or XML Sprite="sprite_name"
```

### Sprite Name = Filename Without Extension

A PNG at `SpriteParts/ui_taom/SpecialResources/taom_gems_icon.png` becomes sprite ID `taom_gems_icon`. Reference it in XML as `Sprite="taom_gems_icon"` or in C# as `Context.SpriteData.GetSprite("taom_gems_icon")`.

### Folder Structure

```
GUI/
├── Brushes/                          ← Brush XML definitions (colors, fonts, states)
├── Fonts/                            ← Custom fonts
├── Prefabs/                          ← Gauntlet XML UI layouts
│   └── CareerSystem/
│       ├── CareerScreen.xml          ← Main career screen prefab
│       └── AbilityHUD.xml            ← Battle HUD for active ability
├── SpriteData/                       ← Compiled sprite sheets (auto-generated)
├── SpriteParts/                      ← Source PNGs (organized by category)
│   ├── Config.xml                    ← Sprite compiler config
│   └── ui_taom/                      ← TAOM's sprite category
│       ├── CareerSystem/             ← Career UI sprites
│       │   └── career_button_placeholder.png
│       ├── MapBar/                   ← Map bar icons (existing vanilla overrides)
│       ├── SpecialResources/         ← Special resource icons
│       │   ├── taom_gems_icon.png    ← Erebor (gemstone wheel)
│       │   ├── taom_caster_icon.png  ← Gondor (White Tree coin)
│       │   └── taom_marks_icon.png   ← Rohan (horse coin)
│       └── ... (other subfolders)
└── TAOMSpriteData.xml                ← Master sprite category declaration
```

### TAOMSpriteData.xml

Declares the `ui_taom` sprite category with `AlwaysLoad` — all PNGs in `SpriteParts/ui_taom/` are compiled into this category's sprite sheets automatically.

```xml
<SpriteCategory>
  <Name>ui_taom</Name>
  <AlwaysLoad />
  <SpriteSheetCount>4</SpriteSheetCount>
  <SpriteSheetSize ID="1" Width="4096" Height="4096" />
  ...
</SpriteCategory>
```

If adding many new sprites, increase `SpriteSheetCount` or sheet dimensions.

### Adding a New Sprite

1. Place PNG in `GUI/SpriteParts/ui_taom/<subfolder>/your_sprite_name.png`
2. Launch the game — sprite compiler picks it up automatically
3. Reference in XML: `Sprite="your_sprite_name"`
4. Reference in C#: `Context.SpriteData.GetSprite("your_sprite_name")`
5. No `TAOMSpriteData.xml` changes needed unless you exceed sheet capacity

## Gauntlet UI Architecture

### Binding Model

```
Prefab XML (declarative layout)
    @PropertyName    → ViewModel.[DataSourceProperty] string/bool/int
    {CollectionName} → ViewModel.[DataSourceProperty] MBBindingList<T>
    Command.Click    → ViewModel.ExecuteMethodName()
    Command.HoverBegin/End → ViewModel.ExecuteBeginHover()/ExecuteEndHover()
```

### Screen Lifecycle

```
ScreenManager.PushScreen(new GauntletXxxScreen(...))
    → OnInitialize()
        → new GauntletLayer("name", order)
        → layer.LoadMovie("PrefabName", viewModel)
        → AddLayer(layer)
    → OnFrameTick(dt)  [per-frame update]
    → OnFinalize()
        → layer.ReleaseMovie(movie)
        → viewModel.OnFinalize()
        → ScreenManager.PopScreen()
```

### UIExtenderEx Integration

TAOM uses UIExtenderEx for two types of injection:

**1. ViewModel Mixin** — adds properties/methods to existing ViewModels:
```csharp
[ViewModelMixin("RefreshValues")]
internal class MyMixin : BaseViewModelMixin<TargetVM>
{
    [DataSourceProperty]
    public string MyProperty { get; set; }
    
    public void ExecuteMyCommand() { ... }
}
```

**2. Prefab Extension** — injects widgets into existing XML prefabs:
```csharp
[PrefabExtension("TargetPrefab", "descendant::Widget[@Id='Target']")]
internal class MyPrefab : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;
    
    [PrefabExtensionXmlDocument]
    public XmlDocument GetDocument() { ... }
}
```

## Career Screen UI

### Prefab: `CareerScreen.xml`

Modeled on TOR's career screen with expandable choice panels.

**Layout:**
```
┌──────────────────────────────────────────┐
│              Career (title)               │
├────────────┬─────────────────────────────┤
│            │  Tier 1: [group_a] [group_b]│
│  Portrait  │  ─────────────────────────  │
│  Name      │  Tier 2: [group_a] [group_b]│
│  Desc      │  ─────────────────────────  │
│  ───────   │  Tier 3: [group_a] [group_b]│
│  Ability   │                             │
│  Icon      │          Free Points: 5     │
│  Effects   │                             │
├────────────┴─────────────────────────────┤
│                [Done]                     │
└──────────────────────────────────────────┘
```

**Key features:**
- `VisualDefinition="ExtendablePanel"` — choice groups expand 80px→750px on hover
- `@IsTaken` / `@IsFreeToTake` — different icon colors for taken vs available
- `@IsActive` + `@ButtonsVisible` — +/- buttons appear on hover, hidden when locked
- `CareerSystem\locked_chains` sprite overlay on locked tiers
- `<Standard.Background />` and `<Standard.DialogCloseButtons />` for native look

### Binding Chain

```
CareerScreenVM
├── @ScreenTitle, @DoneLbl, @CareerName, @CareerDescription
├── @CareerPortraitSprite (career portrait image)
├── @AbilityName, @AbilitySpriteName, @AbilityLabel
├── @FreeCareerPointsText ("Free Points: 5")
├── @Tier1/2/3Label, @Tier1/2/3Locked
├── {AbilityEffects} → MBBindingList<CareerAbilityEffectVM>
│   └── @LineText
├── {ChoiceGroupsTier1/2/3} → MBBindingList<CareerChoiceGroupObjectVM>
│   ├── @GroupName, @IsActive, @ButtonsVisible, @IsLocked
│   ├── ExecuteBeginHover(), ExecuteEndHover()
│   ├── ExecuteClickIncrease(), ExecuteClickDecrease()
│   └── {Choices} → MBBindingList<CareerChoiceObjectVM>
│       ├── @Description, @IconSprite, @IsKeystone
│       ├── @IsTaken (gold icon), @IsFreeToTake (brown icon)
│       └── ChoiceId (non-binding, used by parent)
└── ExecuteClose()
```

### Career Button on Character Developer

**Status:** Verified in-game (2026-04-14). Dark steel banner sprite with "Career" text overlay.

Injected via `CareerButtonPrefab.cs` → `PrefabExtensionInsertPatch` on `CharacterDeveloper` prefab's `TopPanelParent`. Uses `Sprite="CareerSystem\career_button_placeholder"` (233x75). Visibility gated by `@HasCareer` from `CharacterDeveloperCareerMixin`.

**Opening flow (TOR pattern):** `Patch27` Harmony postfix on `ViewModel.ExecuteCommand` catches `"ExecuteOpenCareerScreen"` → calls `charDevVM.ExecuteDone()` to close Character Developer first → then `Game.Current.GameStateManager.PushState<CareerScreenGameState>()`. The `[GameStateScreen]` attribute on `GauntletCareerScreen` properly deactivates the map bar input layer.

**Critical:** Must close CharacterDeveloper BEFORE pushing career state. Without `ExecuteDone()`, the map bar global layer continues ticking input with invalid context → `IndexOutOfRangeException`.

### Ability HUD in Battle

`AbilityHUD.xml` displayed via `CareerPerkMissionBehavior` on a `GauntletLayer("CareerAbilityHUD", 50)`. Shows charge percentage and ready state via `CareerAbilityHudVM`.

## Special Resource Map Bar

**Status:** Verified in-game (2026-04-14). Gondor Caster showing with tooltip on map bar.

### Architecture (TOR Pattern)

Uses `SecondaryInfoItems.Add()` with proper `MapInfoItemVM` objects — the same approach TOR uses successfully. A `SpecialResourceSpriteWidget` (extends `IconBrushWidget`) replaces the default icon in the item template to dynamically load the resource's sprite.

```
SpecialResourceMapBarMixin (ViewModelMixin on MapInfoVM, hooks "Refresh")
├── Creates MapInfoItemVM("special_resource", GetTooltipProperties)
├── Adds to SecondaryInfoItems once (_baseInitialized guard)
├── Updates Value, IntValue, HasWarning per frame
└── GetTooltipProperties() → rich tooltip with tier, daily change, earning rates

SpecialResourceIconPrefab (PrefabExtension on MapBar)
└── Replaces IconBrushWidget in BottomInfoBar ItemTemplate
    with SpecialResourceSpriteWidget (dynamic icon loading)
```

**Critical:** The mixin MUST hook `"Refresh"` (per-frame), NOT `"RefreshValues"` (one-time init). TOR uses the same pattern.

### Tooltip Content

The hover tooltip shows:
- Resource name + amount/cap (title)
- Current tier name + description (if tier active)
- Next tier threshold (if below all tiers)
- Daily change breakdown: income (N towns) vs elite upkeep
- Per-event earning rates: battle, raid, siege, prisoner

## Sprites Needed (Not Yet Created)

| Sprite ID | Size | Count | Used By |
|-----------|------|-------|---------|
| `career_{id}_portrait` | 400x200 | 50 | CareerScreen left panel |
| `ability_{id}_icon` | 120x120 | 50 | CareerScreen ability section |
| `CareerSystem\locked_chains` | Full tier width | 1 | Tier lock overlay |
| `CareerSystem\plus_sign_icon` | 50x50 | 1 | Choice group add button |
| `CareerSystem\minus_sign_icon` | 50x50 | 1 | Choice group remove button |
| `taom_{resource}_icon` | 45x45 | 8 remaining | Map bar resource icons |

The 3 completed resource icons (gems, caster, marks) are in `SpriteParts/ui_taom/SpecialResources/`. The remaining 8 resources need icons generated (ComfyUI at `E:\ComfyUI_windows_portable_nvidia`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` | Career screen layout |
| `Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml` | Battle ability HUD |
| `Main/_Module/GUI/TAOMSpriteData.xml` | Sprite category declaration |
| `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs` | Screen creation + lifecycle |
| `Main/Features/CareerSystem/UI/CareerScreenVM.cs` | Career screen ViewModel (root) |
| `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs` | Choice group VM (expandable, hover/click) |
| `Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs` | Individual choice VM (taken/free state) |
| `Main/Features/CareerSystem/UI/CareerAbilityEffectVM.cs` | Ability effect line item VM |
| `Main/Features/CareerSystem/UI/CareerAbilityHudVM.cs` | Battle HUD VM |
| `Main/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs` | Mixin for career button on CharDev |
| `Main/Features/CareerSystem/UI/CareerButtonPrefab.cs` | Prefab injection for career button |
| `Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs` | Mixin for resource on map bar |
| `Main/Features/SpecialResources/UI/SpecialResourcePrefab.cs` | Prefab injection for resource widget |
| `Main/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs` | Custom widget for dynamic sprite |

## How-To

### Add a sprite for a new resource
1. Create PNG icon (recommended 45x45 or higher)
2. Place at `GUI/SpriteParts/ui_taom/SpecialResources/taom_{resource_id}_icon.png`
3. Set `icon_sprite="taom_{resource_id}_icon"` in `special_resources_config.xml`
4. Launch game — sprite compiler picks it up

### Add a career portrait
1. Create PNG (400x200)
2. Place at `GUI/SpriteParts/ui_taom/CareerSystem/career_{career_id}_portrait.png`
3. Set `portrait_sprite="career_{career_id}_portrait"` in `taom_careers.xml`

### Inject a widget into a vanilla screen
1. Create a `PrefabExtensionInsertPatch` class with `[PrefabExtension("TargetPrefab", "xpath")]`
2. Return XML from `GetDocument()` with `@PropertyName` bindings
3. Create a `BaseViewModelMixin<TargetVM>` with `[DataSourceProperty]` for each binding
4. **Do NOT add to collection properties** (SecondaryInfoItems, etc.) — use bound properties + prefab injection

### Debug UI bindings
1. Check Bannerlord's `rgl_log.txt` for Gauntlet binding errors
2. Property name in XML must EXACTLY match `[DataSourceProperty]` name (case-sensitive)
3. `Command.Click="ExecuteX"` requires a public `void ExecuteX()` method on the ViewModel
4. `{CollectionName}` requires `MBBindingList<T>` — not `List<T>` or `IReadOnlyList<T>`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](./career-system.md)
- [docs/features/special-resources.md](./special-resources.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->


 succeeded in 1264ms:

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\Extensions.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	public static void Load(this SpriteCategory category)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m49
[32;1mLine       : [0m		RefreshSpriteData();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m54
[32;1mLine       : [0m	public static SpriteCategory GetSpriteCategory(string spriteCategoryName)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m			Debug.FailedAssert("Trying to get sprite category but sprite data was not initialized", 
             "C:\\BuildAgent\\work\\mb3\\Source\\Engine\\TaleWorlds.Engine.GauntletUI\\UIResourceManager.cs", 
             "GetSpriteCategory", 54);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m61
[32;1mLine       : [0m		if (SpriteData.SpriteCategories.TryGetValue(spriteCategoryName, out var value))

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m68
[32;1mLine       : [0m	public static SpriteCategory LoadSpriteCategory(string spriteCategoryName)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m70
[32;1mLine       : [0m		SpriteCategory spriteCategory = GetSpriteCategory(spriteCategoryName);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m71
[32;1mLine       : [0m		spriteCategory.Load(ResourceContext, ResourceDepot);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m72
[32;1mLine       : [0m		return spriteCategory;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m147
[32;1mLine       : [0m	private static void RefreshSpriteData()

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\UIResourceManage
             r.cs
[32;1mLineNumber : [0m151
[32;1mLine       : [0m			SpriteData = new SpriteData("SpriteData");

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m6
[32;1mLine       : [0mpublic class SpriteCategory

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m16
[32;1mLine       : [0m	public const int SpriteSheetSize = 4096;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m24
[32;1mLine       : [0m	public List<SpritePart> SpriteParts { get; private set; }

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m	public List<Texture> SpriteSheets { get; private set; }

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m38
[32;1mLine       : [0m	public SpriteCategory(string name, int spriteSheetCount, bool alwaysLoad = false)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m		SpriteSheets = new List<Texture>();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m44
[32;1mLine       : [0m		SpriteParts = new List<SpritePart>();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m				Texture item = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" 
             + i);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m59
[32;1mLine       : [0m				SpriteSheets.Add(item);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m68
[32;1mLine       : [0m			SpriteSheets.ForEach(delegate(Texture s)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m72
[32;1mLine       : [0m			SpriteSheets.Clear();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m78
[32;1mLine       : [0m	public void Reload(ITwoDimensionResourceContext resourceContext, ResourceDepot resourceDepot, 
             SpriteCategory newCategoryInfo)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m84
[32;1mLine       : [0m		SpriteParts = newCategoryInfo.SpriteParts;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m92
[32;1mLine       : [0m				if (SpriteSheets[i] != null)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m118
[32;1mLine       : [0m				SpriteSheets.Add(null);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m131
[32;1mLine       : [0m			SpriteSheets.Clear();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m139
[32;1mLine       : [0m		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] == 
             null)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m141
[32;1mLine       : [0m			Texture value = resourceContext.LoadTexture(resourceDepot, "SpriteSheets\\" + Name + "\\" + Name + "_" 
             + sheetIndex);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m142
[32;1mLine       : [0m			SpriteSheets[sheetIndex - 1] = value;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m148
[32;1mLine       : [0m		if (sheetIndex >= 1 && sheetIndex <= SpriteSheetCount && IsLoaded && SpriteSheets[sheetIndex - 1] != 
             null)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m150
[32;1mLine       : [0m			SpriteSheets[sheetIndex - 1].PlatformTexture.Release();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m151
[32;1mLine       : [0m			SpriteSheets[sheetIndex - 1] = null;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m158
[32;1mLine       : [0m		SortedSpritePartList.AddRange(SpriteParts);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m		for (int i = 0; i < SpriteSheets.Count; i++)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteCategory.cs
[32;1mLineNumber : [0m166
[32;1mLine       : [0m			Texture texture = SpriteSheets[i];

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m		public Dictionary<string, SpriteCategory> SpriteCategories;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m20
[32;1mLine       : [0m	public Dictionary<string, SpritePart> SpriteParts { get; private set; }

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m24
[32;1mLine       : [0m	public Dictionary<string, SpriteCategory> SpriteCategories { get; private set; }

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m	public SpriteData(string name)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m31
[32;1mLine       : [0m		SpriteParts = new Dictionary<string, SpritePart>();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m33
[32;1mLine       : [0m		SpriteCategories = new Dictionary<string, SpriteCategory>();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m55
[32;1mLine       : [0m			SpriteCategories = new Dictionary<string, SpriteCategory>(),

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m79
[32;1mLine       : [0m		XmlNode xmlNode2 = xmlElement["SpriteParts"];

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m89
[32;1mLine       : [0m				if (childNode.Name == "SpriteSheetSize")

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m101
[32;1mLine       : [0m			SpriteCategory spriteCategory = new SpriteCategory(innerText, num, alwaysLoad)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m105
[32;1mLine       : [0m			loadResult.SpriteCategories[spriteCategory.Name] = spriteCategory;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m113
[32;1mLine       : [0m			SpriteCategory category = loadResult.SpriteCategories[innerText3];

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m154
[32;1mLine       : [0m		SpriteParts = spriteDataLoadResult.SpritePartNames;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m161
[32;1mLine       : [0m		SpriteParts = spriteDataLoadResult.SpritePartNames;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m		foreach (KeyValuePair<string, SpriteCategory> spriteCategory2 in SpriteCategories)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m167
[32;1mLine       : [0m			foreach (KeyValuePair<string, SpriteCategory> spriteCategory3 in spriteDataLoadResult.SpriteCategories)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m169
[32;1mLine       : [0m				if (spriteCategory3.Key == spriteCategory2.Key)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m177
[32;1mLine       : [0m				list.Add(spriteCategory2.Key);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m180
[32;1mLine       : [0m		foreach (KeyValuePair<string, SpriteCategory> spriteCategory4 in spriteDataLoadResult.SpriteCategories)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m183
[32;1mLine       : [0m			foreach (KeyValuePair<string, SpriteCategory> spriteCategory5 in SpriteCategories)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m185
[32;1mLine       : [0m				if (spriteCategory4.Key == spriteCategory5.Key)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m193
[32;1mLine       : [0m				list2.Add(spriteCategory4.Key);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m203
[32;1mLine       : [0m			SpriteCategory spriteCategory = spriteDataLoadResult.SpriteCategories[item2];

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m204
[32;1mLine       : [0m			SpriteCategories.Add(item2, spriteCategory);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m205
[32;1mLine       : [0m			if (spriteCategory.AlwaysLoad)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m207
[32;1mLine       : [0m				spriteCategory.Load(resourceContext, resourceDepot);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m210
[32;1mLine       : [0m		foreach (KeyValuePair<string, SpriteCategory> spriteCategory6 in SpriteCategories)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m212
[32;1mLine       : [0m			if (spriteDataLoadResult.SpriteCategories.TryGetValue(spriteCategory6.Key, out var value))

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m214
[32;1mLine       : [0m				spriteCategory6.Value.Reload(resourceContext, resourceDepot, value);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpriteData.cs
[32;1mLineNumber : [0m217
[32;1mLine       : [0m		foreach (KeyValuePair<string, SpritePart> spritePart in SpriteParts)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m7
[32;1mLine       : [0m	private SpriteCategory _category;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m37
[32;1mLine       : [0m			SpriteCategory category = _category;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m38
[32;1mLine       : [0m			if (category != null && category.IsLoaded && _category.SpriteSheets?.Count >= SheetID)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m40
[32;1mLine       : [0m				return _category.SpriteSheets[SheetID - 1];

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m46
[32;1mLine       : [0m	public SpriteCategory Category

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	public SpritePart(string name, SpriteCategory category, int width, int height)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension\TaleWorlds.TwoDimension\SpritePart.cs
[32;1mLineNumber : [0m64
[32;1mLine       : [0m		_category.SpriteParts.Add(this);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI\UIContext.cs
[32;1mLineNumber : [0m289
[32;1mLine       : [0m			SpriteData = new SpriteData("SpriteData");

[32;1mPath       : [0mE:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.CodeGenerator\TaleWorlds.GauntletUI.CodeGenerator\UICode
             GenerationContext.cs
[32;1mLineNumber : [0m50
[32;1mLine       : [0m		SpriteData = new SpriteData("SpriteData");


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/_Module/GUI/SpriteParts/Config.xml'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules','E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\GUI' -Recurse -Filter '*SpriteData*.xml' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/_Module/GUI/SpriteParts/ui_taom_career_system' -Recurse | Select-Object FullName,Length | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/_Module/GUI/SpriteData' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 514ms:
<Config>
  <SpriteCategory Name="ui_taom_bannericons">
    <AlwaysLoad />
  </SpriteCategory>
  <SpriteCategory Name="ui_loading">
    <AlwaysLoad />
  </SpriteCategory>
  <SpriteCategory Name="ui_taom">
		<AlwaysLoad />
	</SpriteCategory>
  <SpriteCategory Name="ui_taom_career_system">
    <AlwaysLoad />
  </SpriteCategory>
  <SpriteCategory Name="ui_custom_fonts">
        <PackAllSpritesToUniqueTextures/>
        <SingleChannel/>
        <AlwaysLoad/>
        <EdgeSize Value="0" />
    </SpriteCategory>
</Config>

 succeeded in 504ms:

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\career_butt
           on_placeholder.png
[32;1mLength   : [0m1091005

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\career_poin
           t_pip.png
[32;1mLength   : [0m46231

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\locked_gate
           _bottom.png
[32;1mLength   : [0m651648

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\locked_gate
           _full.png
[32;1mLength   : [0m738688

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\locked_gate
           _top.png
[32;1mLength   : [0m656469

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\minus_sign_
           icon.png
[32;1mLength   : [0m156205

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\plus_sign_i
           con.png
[32;1mLength   : [0m158008

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\c
           aptain_of_osgiliath_ability.png
[32;1mLength   : [0m134584

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\c
           rossbow_master_ability.png
[32;1mLength   : [0m113761

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\e
           otheod_windrider_ability.png
[32;1mLength   : [0m124761

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\i
           ronguard_ability.png
[32;1mLength   : [0m106619

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\k
           night_of_belfalas_ability.png
[32;1mLength   : [0m91380

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\m
           arksman_of_aldburg_ability.png
[32;1mLength   : [0m107853

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\r
           am_rider_ability.png
[32;1mLength   : [0m108672

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\r
           anger_of_ithilien_ability.png
[32;1mLength   : [0m123445

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Abilities\w
           atchman_of_stangard_ability.png
[32;1mLength   : [0m96239

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_balchoth_kan_portrait.png
[32;1mLength   : [0m537117

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_black_uruk_captain_portrait.png
[32;1mLength   : [0m575576

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_blade_dancer_portrait.png
[32;1mLength   : [0m554036

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_captain_of_osgiliath_portrait.png
[32;1mLength   : [0m526066

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_cave_troll_master_portrait.png
[32;1mLength   : [0m509283

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_codyan_legionaire_portrait.png
[32;1mLength   : [0m543154

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_elven_archer_portrait.png
[32;1mLength   : [0m556644

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_eotheod_windrider_portrait.png
[32;1mLength   : [0m479708

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_ironguard_portrait.png
[32;1mLength   : [0m550371

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_knight_of_belfalas_portrait.png
[32;1mLength   : [0m464344

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_lokhas_drus_marksman_portrait.png
[32;1mLength   : [0m523435

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_mahud_beast_rider_portrait.png
[32;1mLength   : [0m457159

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_marksman_of_aldburg_portrait.png
[32;1mLength   : [0m608586

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_pezarsani_javelineer_portrait.png
[32;1mLength   : [0m407064

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_ranger_of_ithilien_portrait.png
[32;1mLength   : [0m614972

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_shadow_warrior_portrait.png
[32;1mLength   : [0m560001

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_silvan_archer_portrait.png
[32;1mLength   : [0m518264

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_tribesman_of_jelut_portrait.png
[32;1mLength   : [0m409226

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_uruk_berserker_portrait.png
[32;1mLength   : [0m466458

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_uruk_crossbow_portrait.png
[32;1mLength   : [0m580936

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\c
           areer_watchman_of_stangard_portrait.png
[32;1mLength   : [0m481807


 succeeded in 550ms:

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\ui_factionmap
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres
[32;1mLength   : [0m

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_anduin_vale.png
[32;1mLength   : [0m29209

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_aru_thani_of_bellakar.png
[32;1mLength   : [0m28857

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_clans_of_dunland.png
[32;1mLength   : [0m28970

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_clans_of_enedwaith.png
[32;1mLength   : [0m26074

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_clans_of_forochel.png
[32;1mLength   : [0m30131

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dark_lands_of_mordor.png
[32;1mLength   : [0m27762

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dominion_of_isengard.png
[32;1mLength   : [0m26838

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dominion_of_mordor.png
[32;1mLength   : [0m27762

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dwarves_of_erebor.png
[32;1mLength   : [0m28890

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dwarves_of_ered_luin.png
[32;1mLength   : [0m26793

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_dwarves_of_orocarni.png
[32;1mLength   : [0m26200

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_easterlings_of_rhun.png
[32;1mLength   : [0m28382

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_elves_of_lindon.png
[32;1mLength   : [0m28114

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_elves_of_lothlorien.png
[32;1mLength   : [0m28461

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_elves_of_mirkwood.png
[32;1mLength   : [0m30000

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_elves_of_neldoreth.png
[32;1mLength   : [0m30811

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_elves_of_rivendell.png
[32;1mLength   : [0m29897

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_faithful_of_bellakar.png
[32;1mLength   : [0m28857

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_fangorn_forest.png
[32;1mLength   : [0m28038

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_forodwaith.png
[32;1mLength   : [0m26544

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_goblins_of_goblin_town.png
[32;1mLength   : [0m30968

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_golden_realm_of_rhun.png
[32;1mLength   : [0m28382

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_havens_of_umbar.png
[32;1mLength   : [0m27719

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_high_kingdom_of_lindon.png
[32;1mLength   : [0m28114

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_hill_men_of_dunland.png
[32;1mLength   : [0m28970

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_hill_men_of_rhudaur.png
[32;1mLength   : [0m29631

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_iron_hills.png
[32;1mLength   : [0m29859

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_khudorom_of_khand.png
[32;1mLength   : [0m29388

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_arthedain.png
[32;1mLength   : [0m28425

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_cardolan.png
[32;1mLength   : [0m31096

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_dale.png
[32;1mLength   : [0m28171

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_dorwinion.png
[32;1mLength   : [0m29344

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_erebor.png
[32;1mLength   : [0m28890

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_ered_duin.png
[32;1mLength   : [0m26793

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_gondor.png
[32;1mLength   : [0m29034

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_imladris.png
[32;1mLength   : [0m29897

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_lasgalen.png
[32;1mLength   : [0m30000

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_lothlorien.png
[32;1mLength   : [0m28461

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_neldoreth.png
[32;1mLength   : [0m30811

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_rhudaur.png
[32;1mLength   : [0m29631

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_kingdom_of_rohan.png
[32;1mLength   : [0m27991

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_ered_luin.png
[32;1mLength   : [0m29587

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_forochel.png
[32;1mLength   : [0m30131

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_gundabad.png
[32;1mLength   : [0m27468

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_gwaer.png
[32;1mLength   : [0m30235

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_narager.png
[32;1mLength   : [0m29983

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_orcs_of_the_misty_mountains.p
           ng
[32;1mLength   : [0m30968

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_overlordship_of_dol_guldur.pn
           g
[32;1mLength   : [0m39411

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_overlordship_of_gundabad.png
[32;1mLength   : [0m27468

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_realm_of_angmar.png
[32;1mLength   : [0m28448

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_realm_of_cardolan.png
[32;1mLength   : [0m31096

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_realm_of_dorwinion.png
[32;1mLength   : [0m29344

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_remnants_of_angmar.png
[32;1mLength   : [0m28448

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_shadow_of_dol_guldur.png
[32;1mLength   : [0m39411

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_stewardship_of_gondor.png
[32;1mLength   : [0m29034

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_stronghold_of_ered_gwaer.png
[32;1mLength   : [0m30235

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_stronghold_of_narager.png
[32;1mLength   : [0m29983

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_taskralan_of_harwan.png
[32;1mLength   : [0m29141

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_tribes_of_harad.png
[32;1mLength   : [0m29141

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_vale_of_anduin.png
[32;1mLength   : [0m29209

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_variags_of_khand.png
[32;1mLength   : [0m29388

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\banner_wildmen_of_enedwaith.png
[32;1mLength   : [0m26074

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_anduin_vale.png
[32;1mLength   : [0m100710

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_aru_thani_of_bellakar.png
[32;1mLength   : [0m105045

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_clans_of_dunland.png
[32;1mLength   : [0m25010283

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_clans_of_enedwaith.png
[32;1mLength   : [0m107171

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_clans_of_forochel.png
[32;1mLength   : [0m113396

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dark_lands_of_mordor.png
[32;1mLength   : [0m24054147

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dominion_of_isengard.png
[32;1mLength   : [0m29853685

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dominion_of_mordor.png
[32;1mLength   : [0m24054147

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dwarves_of_erebor.png
[32;1mLength   : [0m28793270

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dwarves_of_ered_luin.png
[32;1mLength   : [0m107094

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_dwarves_of_orocarni.png
[32;1mLength   : [0m107753

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_easterlings_of_rhun.png
[32;1mLength   : [0m29132029

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_elves_of_lindon.png
[32;1mLength   : [0m101645

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_elves_of_lothlorien.png
[32;1mLength   : [0m30572551

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_elves_of_mirkwood.png
[32;1mLength   : [0m28656805

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_elves_of_neldoreth.png
[32;1mLength   : [0m117133

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_elves_of_rivendell.png
[32;1mLength   : [0m31548921

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_emblem.png
[32;1mLength   : [0m294

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_faithful_of_bellakar.png
[32;1mLength   : [0m105045

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_fangorn_forest.png
[32;1mLength   : [0m116618

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_forodwaith.png
[32;1mLength   : [0m100279

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_goblins_of_goblin_town.png
[32;1mLength   : [0m103329

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_golden_realm_of_rhun.png
[32;1mLength   : [0m29132029

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_havens_of_umbar.png
[32;1mLength   : [0m28440953

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_high_kingdom_of_lindon.png
[32;1mLength   : [0m101645

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_hill_men_of_dunland.png
[32;1mLength   : [0m25010283

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_hill_men_of_rhudaur.png
[32;1mLength   : [0m100621

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_iron_hills.png
[32;1mLength   : [0m128341

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_khudorom_of_khand.png
[32;1mLength   : [0m125343

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_arthedain.png
[32;1mLength   : [0m5445003

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_cardolan.png
[32;1mLength   : [0m92205

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_dale.png
[32;1mLength   : [0m119235

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_dolguldur.png
[32;1mLength   : [0m26931683

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_dorwinion.png
[32;1mLength   : [0m105110

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_dunland.png
[32;1mLength   : [0m25010283

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_erebor.png
[32;1mLength   : [0m28793270

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_ered_duin.png
[32;1mLength   : [0m107094

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_gondor.png
[32;1mLength   : [0m27765584

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_gundabad.png
[32;1mLength   : [0m30203946

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_harad.png
[32;1mLength   : [0m30995098

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_imladris.png
[32;1mLength   : [0m31548921

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_isengard.png
[32;1mLength   : [0m29853685

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_lasgalen.png
[32;1mLength   : [0m28656805

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_lothlorien.png
[32;1mLength   : [0m30572551

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_mordor.png
[32;1mLength   : [0m24054147

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_neldoreth.png
[32;1mLength   : [0m117133

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_rhudaur.png
[32;1mLength   : [0m100621

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_rhun.png
[32;1mLength   : [0m29132029

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_rivendell.png
[32;1mLength   : [0m31548921

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_rohan.png
[32;1mLength   : [0m30142191

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_kingdom_of_umbar.png
[32;1mLength   : [0m28440953

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_ered_luin.png
[32;1mLength   : [0m109169

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_forochel.png
[32;1mLength   : [0m113396

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_gundabad.png
[32;1mLength   : [0m30203946

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_gwaer.png
[32;1mLength   : [0m108720

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_narager.png
[32;1mLength   : [0m105135

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_orcs_of_the_misty_mountains.
           png
[32;1mLength   : [0m103329

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_overlordship_of_dol_guldur.p
           ng
[32;1mLength   : [0m26931683

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_overlordship_of_gundabad.png
[32;1mLength   : [0m30203946

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_realm_of_angmar.png
[32;1mLength   : [0m97080

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_realm_of_cardolan.png
[32;1mLength   : [0m92205

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_realm_of_dorwinion.png
[32;1mLength   : [0m105110

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_remnants_of_angmar.png
[32;1mLength   : [0m97080

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_shadow_of_dol_guldur.png
[32;1mLength   : [0m26931683

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_stewardship_of_gondor.png
[32;1mLength   : [0m27765584

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_stronghold_of_ered_gwaer.png
[32;1mLength   : [0m108720

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_stronghold_of_narager.png
[32;1mLength   : [0m105135

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_taskralan_of_harwan.png
[32;1mLength   : [0m30995098

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_tribes_of_harad.png
[32;1mLength   : [0m30995098

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_vale_of_anduin.png
[32;1mLength   : [0m100710

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_variags_of_khand.png
[32;1mLength   : [0m125343

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\faction_wildmen_of_enedwaith.png
[32;1mLength   : [0m107171

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_anduin_vale.png
[32;1mLength   : [0m75966

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_aru_thani_of_bellakar.png
[32;1mLength   : [0m230205

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_browlands.png
[32;1mLength   : [0m125790

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_chajaphan_of_abanissa.png
[32;1mLength   : [0m464287

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_clans_of_andrast.png
[32;1mLength   : [0m51562

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_clans_of_druwaith_iaur.png
[32;1mLength   : [0m46665

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_clans_of_dunland.png
[32;1mLength   : [0m50647

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_clans_of_forochel.png
[32;1mLength   : [0m66660

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_dominion_of_isengard.png
[32;1mLength   : [0m10529

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_dominion_of_mordor.png
[32;1mLength   : [0m422874

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_eregion.png
[32;1mLength   : [0m46859

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_fangorn_forest.png
[32;1mLength   : [0m56147

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_goblins_of_blue_craig.png
[32;1mLength   : [0m27540

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_goblins_of_goblin_town.png
[32;1mLength   : [0m32582

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_golden_realm_of_rhun.png
[32;1mLength   : [0m560262

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_havens_of_umbar.png
[32;1mLength   : [0m176003

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_high_kingdom_of_lindon.png
[32;1mLength   : [0m180338

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_khudorom_of_khand.png
[32;1mLength   : [0m404374

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_angaladh.png
[32;1mLength   : [0m43744

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_arthedain.png
[32;1mLength   : [0m279232

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_cardolan.png
[32;1mLength   : [0m128826

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_dale.png
[32;1mLength   : [0m104314

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_dorwinion.png
[32;1mLength   : [0m77070

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_erebor.png
[32;1mLength   : [0m192720

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_ered_duin.png
[32;1mLength   : [0m94486

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_ered_mithrin.png
[32;1mLength   : [0m78959

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_imladris.png
[32;1mLength   : [0m38906

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_lasgalen.png
[32;1mLength   : [0m61067

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_lothlorien.png
[32;1mLength   : [0m37312

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_moria.png
[32;1mLength   : [0m47847

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_neldoreth.png
[32;1mLength   : [0m101996

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_rhudaur.png
[32;1mLength   : [0m106103

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_rohan.png
[32;1mLength   : [0m98661

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_south_rhovanion.pn
           g
[32;1mLength   : [0m168702

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_kingdom_of_zigalnara.png
[32;1mLength   : [0m188006

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_map_boundary.png
[32;1mLength   : [0m2745861

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_nurunkhizdin.png
[32;1mLength   : [0m26683

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_overlordship_of_dol_guldur.pn
           g
[32;1mLength   : [0m41832

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_overlordship_of_gundabad.png
[32;1mLength   : [0m47071

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_remnants_of_angmar.png
[32;1mLength   : [0m91390

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_stewardship_of_gondor.png
[32;1mLength   : [0m354489

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_stronghold_of_ered_gwaer.png
[32;1mLength   : [0m165520

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_stronghold_of_narager.png
[32;1mLength   : [0m162210

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_taskralan_of_harwan.png
[32;1mLength   : [0m510644

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_taskralan_of_shaghana.png
[32;1mLength   : [0m614520

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\region_wildmen_of_enedwaith.png
[32;1mLength   : [0m101164

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_anduin_vale_full.png
[32;1mLength   : [0m95985

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_aru_thani_of_bellakar
           _full.png
[32;1mLength   : [0m256654

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_browlands_full.png
[32;1mLength   : [0m146807

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_chajaphan_of_abanissa
           _full.png
[32;1mLength   : [0m519988

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_clans_of_andrast_full
           .png
[32;1mLength   : [0m67629

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_clans_of_druwaith_iau
           r_full.png
[32;1mLength   : [0m63024

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_clans_of_dunland_full
           .png
[32;1mLength   : [0m68849

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_clans_of_forochel_ful
           l.png
[32;1mLength   : [0m83717

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_dominion_of_isengard_
           full.png
[32;1mLength   : [0m23440

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_dominion_of_mordor_fu
           ll.png
[32;1mLength   : [0m457408

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_eregion_full.png
[32;1mLength   : [0m63968

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_fangorn_forest_full.p
           ng
[32;1mLength   : [0m73566

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_goblins_of_blue_craig
           _full.png
[32;1mLength   : [0m41805

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_goblins_of_goblin_tow
           n_full.png
[32;1mLength   : [0m49429

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_golden_realm_of_rhun_
           full.png
[32;1mLength   : [0m600345

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_havens_of_umbar_full.
           png
[32;1mLength   : [0m200958

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_high_kingdom_of_lindo
           n_full.png
[32;1mLength   : [0m202865

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_khudorom_of_khand_ful
           l.png
[32;1mLength   : [0m436939

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_angaladh_f
           ull.png
[32;1mLength   : [0m61124

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_arthedain_
           full.png
[32;1mLength   : [0m305759

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_cardolan_f
           ull.png
[32;1mLength   : [0m148642

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_dale_full.
           png
[32;1mLength   : [0m124121

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_dorwinion_
           full.png
[32;1mLength   : [0m96642

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_erebor_ful
           l.png
[32;1mLength   : [0m214634

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_ered_duin_
           full.png
[32;1mLength   : [0m113592

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_ered_mithr
           in_full.png
[32;1mLength   : [0m94852

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_imladris_f
           ull.png
[32;1mLength   : [0m55541

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_lasgalen_f
           ull.png
[32;1mLength   : [0m79121

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_lothlorien
           _full.png
[32;1mLength   : [0m52825

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_moria_full
           .png
[32;1mLength   : [0m64877

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_neldoreth_
           full.png
[32;1mLength   : [0m120987

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_rhudaur_fu
           ll.png
[32;1mLength   : [0m126291

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_rohan_full
           .png
[32;1mLength   : [0m117519

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_south_rhov
           anion_full.png
[32;1mLength   : [0m189296

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_kingdom_of_zigalnara_
           full.png
[32;1mLength   : [0m211149

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_map_boundary_full.png
[32;1mLength   : [0m2888451

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_nurunkhizdin_full.png
[32;1mLength   : [0m42539

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_overlordship_of_dol_g
           uldur_full.png
[32;1mLength   : [0m59165

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_overlordship_of_gunda
           bad_full.png
[32;1mLength   : [0m63140

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_remnants_of_angmar_fu
           ll.png
[32;1mLength   : [0m109070

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_stewardship_of_gondor
           _full.png
[32;1mLength   : [0m380341

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_stronghold_of_ered_gw
           aer_full.png
[32;1mLength   : [0m185719

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_stronghold_of_narager
           _full.png
[32;1mLength   : [0m183968

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_taskralan_of_harwan_f
           ull.png
[32;1mLength   : [0m544949

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_taskralan_of_shaghana
           _full.png
[32;1mLength   : [0m669550

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\FactionMap\fullres\region_wildmen_of_enedwaith_
           full.png
[32;1mLength   : [0m120226

[32;1mFullName : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\SpriteData\ui_factionmap\ui_factionmap.xml
[32;1mLength   : [0m1116


 succeeded in 1183ms:
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ADOD_Beasts\GUI\ADOD_BeastsSpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\GUI\NativeSpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\LauncherGUI\LauncherGUISpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\NavalDLC\GUI\NavalDLCSpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\GUI\SandBoxSpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM\GUI\TAOMSpriteData.xml
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM.Dependencies\GUI\Bannerlord.MBOptionScreenSpriteData.xml

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/_Module/GUI' -Directory | Select-Object Name | Format-Table -HideTableHeaders; Get-ChildItem -Path 'Main/_Module/GUI/SpriteSheets' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM\\GUI' -Directory -ErrorAction SilentlyContinue | Select-Object Name | Format-Table -HideTableHeaders; Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM\\GUI\\SpriteSheets' -Recurse -ErrorAction SilentlyContinue | Select-Object FullName,Length | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\Native\\GUI' -Directory | Select-Object Name | Format-Table -HideTableHeaders; Get-ChildItem -Path 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\Native\\GUI\\SpriteSheets' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 10 FullName,Length | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 564ms:

.vs
Brushes
Fonts
Prefabs
SpriteData
SpriteParts


 succeeded in 857ms:

.vs
Brushes
Fonts
Prefabs
SpriteData
SpriteParts


 succeeded in 1077ms:

Brushes
Fonts
Prefabs


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$files = Get-ChildItem -Path '"'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules' -Recurse -Filter '*SpriteData.xml' -ErrorAction SilentlyContinue
foreach ("'$file in $files) {
  try { [xml]$xml = Get-Content -Raw $file.FullName } catch { continue }
  foreach ($cat in $xml.SpriteData.SpriteCategories.SpriteCategory) {
    $sizes = @($cat.SpriteSheetSize | ForEach-Object { "$($_.Width)x$($_.Height)" } | Sort-Object -Unique)
    if ($sizes.Count -gt 1) {
      "$($file.FullName): $($cat.Name) -> $($sizes -join '"', ')\"
    }
  }
}
'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1149ms:
codex
The TaleWorlds v1.4.5 `TaleWorlds.TwoDimension.dll` from the installed Bannerlord path matches the pre-decompiled source: sheet sizes are stored as a `Vec2i[]` per category and every sprite part reads by `SheetID - 1`. I’m now running XML-level consistency checks for the career data and strings.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$ErrorActionPreference = '"'Stop'
"'$choicesPath = '"'Main/_Module/ModuleData/career_system/taom_career_choices.xml'
"'$careersPath = '"'Main/_Module/ModuleData/career_system/taom_careers.xml'
"'$stringsPath = '"'Main/_Module/ModuleData/taom_module_strings.xml'

[xml]"'$choices = Get-Content -Raw $choicesPath
[xml]$careers = Get-Content -Raw $careersPath
[xml]$strings = Get-Content -Raw $stringsPath

$stringNodes = @($strings.SelectNodes('"'//*[@id]'))
"'$stringIds = @{}
$dups = @()
foreach ($node in $stringNodes) {
  $id = $node.id
  if ($stringIds.ContainsKey($id)) { $dups += $id } else { $stringIds[$id] = $true }
}

function KeyFromLocalized($s) {
  if ($s -match '"'"'^'"\\{=(["'^}]+)'"\\}') { return "'$matches[1] }
  return $null
}
$groupAttrs = @($choices.SelectNodes('"'//ChoiceGroup[@display_name]'))
"'$rankAttrs = @($careers.SelectNodes('"'//Career[@rank1_name or @rank2_name or @rank3_name]'))
"'$missing = @()
$keys = New-Object System.Collections.Generic.HashSet[string]
foreach ($g in $groupAttrs) {
  $k = KeyFromLocalized $g.display_name
  if ($k) { [void]$keys.Add($k); if (-not $stringIds.ContainsKey($k)) { $missing += "group $($g.id) -> $k" } }
}
foreach ($c in $rankAttrs) {
  foreach ($attr in @('"'rank1_name','rank2_name','rank3_name')) {
    "'$raw = $c.GetAttribute($attr)
    $k = KeyFromLocalized $raw
    if ($k) { [void]$keys.Add($k); if (-not $stringIds.ContainsKey($k)) { $missing += "career $($c.id) $attr -> $k" } }
  }
}

"XML well formed: choices/careers/strings parsed"
"ChoiceGroup display_name attrs: $($groupAttrs.Count)"
"Career rank attrs: $($rankAttrs.Count) careers, $($rankAttrs.Count * 3) attrs"
"Unique career/group/rank keys referenced: $($keys.Count)"
"Duplicate string ids in taom_module_strings.xml: $($dups.Count)"
if ($dups.Count -gt 0) { $dups | Sort-Object | Get-Unique | Select-Object -First 20 | ForEach-Object { "DUP $_" } }
"Missing referenced keys: $($missing.Count)"
if ($missing.Count -gt 0) { $missing | Select-Object -First 50 }
"Sample group attrs:"
$groupAttrs | Select-Object -First 8 | ForEach-Object { "$($_.id) => $($_.display_name) => present=$($stringIds.ContainsKey((KeyFromLocalized $_.display_name)))" }
"Sample rank attrs:"
$rankAttrs | Select-Object -First 8 | ForEach-Object { "$($_.id) => $($_.rank1_name) | $($_.rank2_name) | $($_.rank3_name)" }
"String ids with taom_career_grp/rank/tier_requirement prefixes:"
$prefixCount = @($stringNodes | Where-Object { $_.id -match '"'"'^(taom_career_grp_|taom_career_rank[123]_|taom_career_tier_requirement$)'"' }).Count
\"prefixCount="'$prefixCount"
'"'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1068ms:
XML well formed: choices/careers/strings parsed
ChoiceGroup display_name attrs: 288
Career rank attrs: 49 careers, 147 attrs
Unique career/group/rank keys referenced: 435
Duplicate string ids in taom_module_strings.xml: 0
Missing referenced keys: 0
Sample group attrs:
ranger_of_ithilien_t1_a => {=taom_career_grp_ranger_of_ithilien_t1_a}Hunters of the Ithilien Road => present=True
ranger_of_ithilien_t1_b => {=taom_career_grp_ranger_of_ithilien_t1_b}Watchers of Henneth AnnÃ»n => present=True
ranger_of_ithilien_t2_a => {=taom_career_grp_ranger_of_ithilien_t2_a}Scouts of North Ithilien => present=True
ranger_of_ithilien_t2_b => {=taom_career_grp_ranger_of_ithilien_t2_b}Bowmen of Faramir => present=True
ranger_of_ithilien_t3_a => {=taom_career_grp_ranger_of_ithilien_t3_a}Arrows of the Forbidden Pool => present=True
ranger_of_ithilien_t3_b => {=taom_career_grp_ranger_of_ithilien_t3_b}Shadows of Emyn Arnen => present=True
captain_of_osgiliath_t1_a => {=taom_career_grp_captain_of_osgiliath_t1_a}Wardens of the East Bank => present=True
captain_of_osgiliath_t1_b => {=taom_career_grp_captain_of_osgiliath_t1_b}Keepers of the Anduin Crossing => present=True
Sample rank attrs:
ranger_of_ithilien => {=taom_career_rank1_ranger_of_ithilien}Ithilien Scout | {=taom_career_rank2_ranger_of_ithilien}Ranger of Ithilien | {=taom_career_rank3_ranger_of_ithilien}Captain of the White Company
captain_of_osgiliath => {=taom_career_rank1_captain_of_osgiliath}Ohtar of the Crossing | {=taom_career_rank2_captain_of_osgiliath}Warden of Osgiliath | {=taom_career_rank3_captain_of_osgiliath}Captain of Osgiliath
knight_of_belfalas => {=taom_career_rank1_knight_of_belfalas}Squire of Dol Amroth | {=taom_career_rank2_knight_of_belfalas}Swan-Knight | {=taom_career_rank3_knight_of_belfalas}Knight of the Silver Swan
black_uruk_captain => {=taom_career_rank1_black_uruk_captain}Uruk Soldier | {=taom_career_rank2_black_uruk_captain}Black Uruk | {=taom_career_rank3_black_uruk_captain}Captain of the Red Eye
mulkerhili_cultist => {=taom_career_rank1_mulkerhili_cultist}Whisper-Sworn | {=taom_career_rank2_mulkerhili_cultist}Voice of LugbÃºrz | {=taom_career_rank3_mulkerhili_cultist}Herald of the Lidless Eye
snaga_rider => {=taom_career_rank1_snaga_rider}Snaga | {=taom_career_rank2_snaga_rider}Smoke-Raider | {=taom_career_rank3_snaga_rider}Shadow of LugbÃºrz
olog_hai_warchief => {=taom_career_rank1_olog_hai_warchief}War-Troll | {=taom_career_rank2_olog_hai_warchief}Olog-hai | {=taom_career_rank3_olog_hai_warchief}Warchief of LugbÃºrz
marksman_of_aldburg => {=taom_career_rank1_marksman_of_aldburg}Bowman of the Eastfold | {=taom_career_rank2_marksman_of_aldburg}Marksman of Aldburg | {=taom_career_rank3_marksman_of_aldburg}Arrow-warden of the Mark
String ids with taom_career_grp/rank/tier_requirement prefixes:
prefixCount=442

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$path = '"'Main/_Module/ModuleData/career_system/taom_career_choices.xml'
"'$raw = Get-Content -Raw $path
$totalRaw = ([regex]::Matches($raw, '"'display_name=')).Count
[xml]"'$xml = $raw
$xmlAttrs = @($xml.SelectNodes('"'//ChoiceGroup[@display_name]')).Count
\"raw display_name attrs="'$totalRaw"
"xml ChoiceGroup display_name attrs=$xmlAttrs"
"comment-inert attrs=$($totalRaw - $xmlAttrs)"
$idx = $raw.IndexOf('"'cave_troll_master')
if ("'$idx -ge 0) {
  $start = [Math]::Max(0, $idx - 250)
  $len = [Math]::Min(1200, $raw.Length - $start)
  $raw.Substring($start, $len)
}
'"'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 782ms:
raw display_name attrs=594
xml ChoiceGroup display_name attrs=288
comment-inert attrs=306

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$path = '"'Main/_Module/ModuleData/career_system/taom_career_choices.xml'
"'$raw = Get-Content -Raw $path
$rawChoiceGroup = ([regex]::Matches($raw, '"'<ChoiceGroup\\b["'^>]*display_name='"')).Count
[xml]"'$xml = $raw
$xmlAttrs = @($xml.SelectNodes('"'//ChoiceGroup[@display_name]')).Count
\"raw ChoiceGroup display_name attrs="'$rawChoiceGroup"
"xml active ChoiceGroup display_name attrs=$xmlAttrs"
"comment-inert ChoiceGroup attrs=$($rawChoiceGroup - $xmlAttrs)"
Select-String -Path $path -Pattern '"'cave_troll_master','DISABLED','display_name=\\\"\\{=taom_career_grp_cave' -Context 2,2 | Select-Object -First 20 | Format-List
'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 791ms:
raw ChoiceGroup display_name attrs=294
xml active ChoiceGroup display_name attrs=288
comment-inert ChoiceGroup attrs=6


IgnoreCase : True
LineNumber : 4171
Line       :   <!-- DISABLED 2026-05-14: Far Harad Halftroll choice tree WIP; not ready for live game yet. Re-enable 
             by uncommenting.
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : DISABLED
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6770
Line       :   <!-- DISABLED 2026-05-14: Cave Troll Master choice tree WIP; not ready for live game yet. Re-enable by 
             uncommenting.
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : DISABLED
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6771
Line       :   <Choice id="cave_troll_master_root" group_id="" type="Passive"
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6772
Line       :           description="{=taom_cave_troll_master_root_desc}Troll Frenzy grants +25 health and drives your 
             trolls berserk."
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6773
Line       :           icon_sprite="career_choice_cave_troll_master_root">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6777
Line       :   <ChoiceGroup id="cave_troll_master_t1_a" 
             display_name="{=taom_career_grp_cave_troll_master_t1_a}Pit-Whip Driver" career_id="cave_troll_master" 
             tier="1">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6778
Line       :     <Choice id="cave_troll_master_t1_a_key" type="Keystone" description="{=taom_ctm_t1_a_key}Troll Frenzy 
             radius increased, empowering more warriors." icon_sprite="career_choice_ctm_t1_a_key">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6779
Line       :       <Mutations><Mutation target_id="cave_troll_master_ability" property="Radius" calculator="flat" 
             value="3" operation="Add" /></Mutations>
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6781
Line       :     <Choice id="cave_troll_master_t1_a_p1" type="Passive" description="{=taom_ctm_t1_a_p1}+8% melee 
             damage." icon_sprite="career_choice_ctm_t1_a_p1">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6784
Line       :     <Choice id="cave_troll_master_t1_a_p2" type="Passive" description="{=taom_ctm_t1_a_p2}+5% troop 
             damage." icon_sprite="career_choice_ctm_t1_a_p2">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6787
Line       :     <Choice id="cave_troll_master_t1_a_p3" type="Passive" description="{=taom_ctm_t1_a_p3}+30 max 
             health." icon_sprite="career_choice_ctm_t1_a_p3">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6790
Line       :     <Choice id="cave_troll_master_t1_a_p4" type="Passive" description="{=taom_ctm_t1_a_p4}+7% swing 
             speed." icon_sprite="career_choice_ctm_t1_a_p4">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6795
Line       :   <ChoiceGroup id="cave_troll_master_t1_b" 
             display_name="{=taom_career_grp_cave_troll_master_t1_b}Cave-Mouth Warden" career_id="cave_troll_master" 
             tier="1">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6796
Line       :     <Choice id="cave_troll_master_t1_b_key" type="Keystone" description="{=taom_ctm_t1_b_key}Troll Frenzy 
             duration extended, fortifying the defense." icon_sprite="career_choice_ctm_t1_b_key">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6797
Line       :       <Mutations><Mutation target_id="cave_troll_master_ability" property="Duration" calculator="flat" 
             value="4" operation="Add" /></Mutations>
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6799
Line       :     <Choice id="cave_troll_master_t1_b_p1" type="Passive" description="{=taom_ctm_t1_b_p1}+8% 
             resistance." icon_sprite="career_choice_ctm_t1_b_p1">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6802
Line       :     <Choice id="cave_troll_master_t1_b_p2" type="Passive" description="{=taom_ctm_t1_b_p2}+5% troop 
             morale." icon_sprite="career_choice_ctm_t1_b_p2">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6805
Line       :     <Choice id="cave_troll_master_t1_b_p3" type="Passive" description="{=taom_ctm_t1_b_p3}+5% troop 
             resistance." icon_sprite="career_choice_ctm_t1_b_p3">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6808
Line       :     <Choice id="cave_troll_master_t1_b_p4" type="Passive" description="{=taom_ctm_t1_b_p4}+5% troop 
             regeneration." icon_sprite="career_choice_ctm_t1_b_p4">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}

IgnoreCase : True
LineNumber : 6813
Line       :   <ChoiceGroup id="cave_troll_master_t2_a" 
             display_name="{=taom_career_grp_cave_troll_master_t2_a}Goblin-town Tyrant" career_id="cave_troll_master" 
             tier="2">
Filename   : taom_career_choices.xml
Path       : C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\career_system\taom_career_choices.xml
Pattern    : cave_troll_master
Context    : Microsoft.PowerShell.Commands.MatchInfoContext
Matches    : {0}




exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerScreenVM|HeroLevel|\\.Level|IsTierAvailable\\(' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs' -Pattern 'public int Level','Level =>','GetSkillValue','HeroDeveloper' -Context 3,5 | Select-Object -First 30 | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main','TAOM.Tests' -Recurse -Filter '*.cs' | Select-String -Pattern 'GetTierUnlockLevel\\(','IsTierAvailable\\(' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Campaign','E:\\Decompiled_Bannerlord\\Modules' -Recurse -Filter '*.cs' | Select-String -Pattern 'Level = 1','Level\\s*\\{ get;','DefaultLevel','HeroDeveloper','CharacterObject.Level' | Select-Object -First 80 Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 557ms:

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m78
[32;1mLine       : [0m	public int Level;
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mpublic int Level
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m166
[32;1mLine       : [0m	private HeroDeveloper _heroDeveloper;
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m883
[32;1mLine       : [0m	public HeroDeveloper HeroDeveloper => _heroDeveloper;
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m931
[32;1mLine       : [0m		collectedObjects.Add(_heroDeveloper);
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1229
[32;1mLine       : [0m	internal static object AutoGeneratedGetMemberValue_heroDeveloper(object o)
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1231
[32;1mLine       : [0m		return ((Hero)o)._heroDeveloper;
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1366
[32;1mLine       : [0m	public int GetSkillValue(SkillObject skill)
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mGetSkillValue
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1387
[32;1mLine       : [0m		_heroDeveloper?.AddSkillXp(skill, xpAmount);
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1496
[32;1mLine       : [0m		_heroDeveloper = new HeroDeveloper(this);
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1603
[32;1mLine       : [0m			if (item == null || item.IsTrash || (float)GetSkillValue(item.Skill) < item.RequiredSkillValue)
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mGetSkillValue
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1616
[32;1mLine       : [0m		HeroDeveloper?.AfterLoad();
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1851
[32;1mLine       : [0m		HeroDeveloper.InitializeHeroDeveloper();
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m1958
[32;1mLine       : [0m		_heroDeveloper = null;
[32;1mFilename   : [0mHero.cs
[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mPattern    : [0mHeroDeveloper
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}


 succeeded in 581ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs
[32;1mLineNumber : [0m36
[32;1mLine       : [0m        CampaignEvents.HeroLevelledUp.AddNonSerializedListener(this, OnHeroLeveledUp);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs
[32;1mLineNumber : [0m95
[32;1mLine       : [0m    private void OnHeroLeveledUp(Hero hero, bool shouldNotify)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs
[32;1mLineNumber : [0m100
[32;1mLine       : [0m            _logger.LogDebug($"CareerSystem: OnHeroLeveledUp — hero '{hero.StringId}' has no career, 
             skipping");

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs
[32;1mLineNumber : [0m104
[32;1mLine       : [0m        var maxChoices = _registry.GetMaxChoicesForHero(hero.Level);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m109
[32;1mLine       : [0m    public int GetMaxChoicesForHero(int heroLevel)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m114
[32;1mLine       : [0m        var effectiveLevel = Math.Max(1, heroLevel);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m118
[32;1mLine       : [0m    public bool IsTierAvailable(int heroLevel, int tier)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m121
[32;1mLine       : [0m        return heroLevel >= GetTierUnlockLevel(tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs
[32;1mLineNumber : [0m54
[32;1mLine       : [0m            var maxChoices = _registry.GetMaxChoicesForHero(hero.Level);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerRegistry.cs
[32;1mLineNumber : [0m15
[32;1mLine       : [0m    int GetMaxChoicesForHero(int heroLevel);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerRegistry.cs
[32;1mLineNumber : [0m16
[32;1mLine       : [0m    bool IsTierAvailable(int heroLevel, int tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\BuiltInCalculators.cs
[32;1mLineNumber : [0m14
[32;1mLine       : [0m            baseValue + hero.Level * p.GetFloat("factor", 0.01f));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m    private readonly int _heroLevel;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m51
[32;1mLine       : [0m        int heroLevel,

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m60
[32;1mLine       : [0m        _heroLevel = heroLevel;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m120
[32;1mLine       : [0m        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m126
[32;1mLine       : [0m        Tier2Locked = !_registry.IsTierAvailable(_heroLevel, 2);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m127
[32;1mLine       : [0m        Tier3Locked = !_registry.IsTierAvailable(_heroLevel, 3);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m163
[32;1mLine       : [0m            var isLocked = !_registry.IsTierAvailable(_heroLevel, group.Tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m194
[32;1mLine       : [0m            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m219
[32;1mLine       : [0m        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m238
[32;1mLine       : [0m            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m263
[32;1mLine       : [0m        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs
[32;1mLineNumber : [0m27
[32;1mLine       : [0m    private readonly int _heroLevel;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs
[32;1mLineNumber : [0m38
[32;1mLine       : [0m        _heroLevel = hero?.Level ?? 0;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs
[32;1mLineNumber : [0m53
[32;1mLine       : [0m        logger?.LogInfo($"CareerSystem: Opening career screen for hero '{hero.StringId}' 
             level={hero.Level}");

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs
[32;1mLineNumber : [0m64
[32;1mLine       : [0m        _viewModel = new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, 
             _logger, _heroStringId, _heroLevel, CloseScreen);


 succeeded in 562ms:

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs
[32;1mLineNumber : [0m609
[32;1mLine       : [0m			if (characterObject.Level > baseTroop.Level && SearchForFormationInTroopTree(characterObject, 
             formation))

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m36
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Vigor, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m40
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Control, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m44
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Endurance, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m48
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveFocus(DefaultSkills.OneHanded, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m49
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveFocus(DefaultSkills.TwoHanded, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m53
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Endurance, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m57
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Control, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PerkHelper.cs
[32;1mLineNumber : [0m61
[32;1mLine       : [0m				hero.HeroDeveloper.RemoveAttribute(DefaultCharacterAttributes.Vigor, 1);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m151
[32;1mLine       : [0m		CollectObjectsDelegate collectObjectsDelegate11 = 
             HeroDeveloper.AutoGeneratedStaticCollectObjectsHeroDeveloper;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m153
[32;1mLine       : [0m		obj11.GetPropertyDefinitionWithId(new MemberTypeId(2, 
             101)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValueUnspentFocusPoints);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m154
[32;1mLine       : [0m		obj11.GetPropertyDefinitionWithId(new MemberTypeId(2, 
             102)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValueUnspentAttributePoints);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m155
[32;1mLine       : [0m		obj11.GetPropertyDefinitionWithId(new MemberTypeId(2, 
             103)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValueHero);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m156
[32;1mLine       : [0m		obj11.GetFieldDefinitionWithId(new MemberTypeId(2, 
             0)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValue_skillXps);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m157
[32;1mLine       : [0m		obj11.GetFieldDefinitionWithId(new MemberTypeId(2, 
             100)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValue_newFocuses);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m158
[32;1mLine       : [0m		obj11.GetFieldDefinitionWithId(new MemberTypeId(2, 
             130)).InitializeForAutoGeneration(HeroDeveloper.AutoGeneratedGetMemberValue_totalXp);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveMan
             ager.cs
[32;1mLineNumber : [0m296
[32;1mLine       : [0m		obj22.GetFieldDefinitionWithId(new MemberTypeId(3, 
             770)).InitializeForAutoGeneration(Hero.AutoGeneratedGetMemberValue_heroDeveloper);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m384
[32;1mLine       : [0m						obj.HeroDeveloper.AddSkillXp(item, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m385
[32;1mLine       : [0m						int num = (int)(obj.HeroDeveloper.GetFocusFactor(item) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m406
[32;1mLine       : [0m						obj.HeroDeveloper.AddSkillXp(item2, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m407
[32;1mLine       : [0m						int num2 = (int)(obj.HeroDeveloper.GetFocusFactor(item2) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m427
[32;1mLine       : [0m								obj.HeroDeveloper.AddSkillXp(item3, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m428
[32;1mLine       : [0m								int num2 = (int)(obj.HeroDeveloper.GetFocusFactor(item3) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m440
[32;1mLine       : [0m					obj.HeroDeveloper.AddSkillXp(item4, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m441
[32;1mLine       : [0m					int num2 = (int)(obj.HeroDeveloper.GetFocusFactor(item4) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m468
[32;1mLine       : [0m							obj.HeroDeveloper.AddSkillXp(item5, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m469
[32;1mLine       : [0m							int num3 = (int)(obj.HeroDeveloper.GetFocusFactor(item5) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m487
[32;1mLine       : [0m							obj.HeroDeveloper.AddSkillXp(item6, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m488
[32;1mLine       : [0m							int num4 = (int)(obj.HeroDeveloper.GetFocusFactor(item6) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m506
[32;1mLine       : [0m					obj.HeroDeveloper.AddSkillXp(current7, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m507
[32;1mLine       : [0m					int num5 = (int)(obj.HeroDeveloper.GetFocusFactor(current7) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m531
[32;1mLine       : [0m						obj.HeroDeveloper.AddSkillXp(item7, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m532
[32;1mLine       : [0m						int num6 = (int)(obj.HeroDeveloper.GetFocusFactor(item7) * (float)result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m610
[32;1mLine       : [0m				obj.CharacterObject.Level = 0;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m611
[32;1mLine       : [0m				obj.HeroDeveloper.ClearHero();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m616
[32;1mLine       : [0m					if (obj.HeroDeveloper.GetFocus(item2) + num > maxFocusPerSkill)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m620
[32;1mLine       : [0m					obj.HeroDeveloper.AddFocus(item2, num, checkUnspentFocusPoints: false);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m621
[32;1mLine       : [0m					obj.HeroDeveloper.SetInitialSkillLevel(item2, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m641
[32;1mLine       : [0m				obj.HeroDeveloper.UnspentFocusPoints = 0;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m694
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.SetInitialSkillLevel(obj, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m695
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.InitializeSkillXp(obj);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m737
[32;1mLine       : [0m			foreach (HeroDeveloper item in Clan.PlayerClan.Companions.Select((Hero x) => x.HeroDeveloper))

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m776
[32;1mLine       : [0m				companion.HeroDeveloper.SetInitialSkillLevel(item, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m777
[32;1mLine       : [0m				companion.HeroDeveloper.InitializeSkillXp(item);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m811
[32;1mLine       : [0m				item.HeroDeveloper.SetInitialSkillLevel(item2, result);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m812
[32;1mLine       : [0m				item.HeroDeveloper.InitializeSkillXp(item2);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2023
[32;1mLine       : [0m		hero.HeroDeveloper.InitializeHeroDeveloper();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2377
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.UnspentFocusPoints = 
             MBMath.ClampInt(Hero.MainHero.HeroDeveloper.UnspentFocusPoints + 1, 0, int.MaxValue);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2390
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.UnspentFocusPoints = 
             MBMath.ClampInt(Hero.MainHero.HeroDeveloper.UnspentFocusPoints + result, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2407
[32;1mLine       : [0m				mainHero.HeroDeveloper.UnspentFocusPoints = MBMath.ClampInt(mainHero.HeroDeveloper.UnspentFocusPoints 
             + result, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2417
[32;1mLine       : [0m				mainHero.HeroDeveloper.UnspentFocusPoints = MBMath.ClampInt(mainHero.HeroDeveloper.UnspentFocusPoints 
             + 1, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2445
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.UnspentAttributePoints = 
             MBMath.ClampInt(Hero.MainHero.HeroDeveloper.UnspentAttributePoints + 1, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2458
[32;1mLine       : [0m			Hero.MainHero.HeroDeveloper.UnspentAttributePoints = 
             MBMath.ClampInt(Hero.MainHero.HeroDeveloper.UnspentAttributePoints + result, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2475
[32;1mLine       : [0m				mainHero.HeroDeveloper.UnspentAttributePoints = 
             MBMath.ClampInt(mainHero.HeroDeveloper.UnspentAttributePoints + result2, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs
[32;1mLineNumber : [0m2485
[32;1mLine       : [0m				mainHero.HeroDeveloper.UnspentAttributePoints = 
             MBMath.ClampInt(mainHero.HeroDeveloper.UnspentAttributePoints + 1, 0, 10000);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m149
[32;1mLine       : [0m		characterData.UnspentAttributePoints = hero.HeroDeveloper.UnspentAttributePoints;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m150
[32;1mLine       : [0m		characterData.UnspentFocusPoints = hero.HeroDeveloper.UnspentFocusPoints;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m156
[32;1mLine       : [0m			characterData.SkillsArray[i] = new SkillObjectData(Skills.All[i].StringId, 
             hero.GetSkillValue(Skills.All[i]), hero.HeroDeveloper.GetSkillXpProgress(Skills.All[i]), 
             hero.HeroDeveloper.GetFocus(Skills.All[i]));

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m234
[32;1mLine       : [0m		target.HeroDeveloper.UnspentFocusPoints = characterData.UnspentFocusPoints;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m235
[32;1mLine       : [0m		target.HeroDeveloper.UnspentAttributePoints = characterData.UnspentAttributePoints;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m246
[32;1mLine       : [0m				int focus2 = target.HeroDeveloper.GetFocus(skillObject);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m251
[32;1mLine       : [0m				target.HeroDeveloper.SetSkillXp(skillObject, num2);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m256
[32;1mLine       : [0m					target.HeroDeveloper.AddFocus(skillObject, focus - focus2, checkUnspentFocusPoints: false);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m260
[32;1mLine       : [0m					target.HeroDeveloper.RemoveFocus(skillObject, focus2 - focus);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m285
[32;1mLine       : [0m				target.HeroDeveloper.AddAttribute(characterAttribute, changeAmount, checkUnspentPoints: false);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m295
[32;1mLine       : [0m				target.HeroDeveloper.AddPerk(perkObject);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterData.cs
[32;1mLineNumber : [0m298
[32;1mLine       : [0m		target.HeroDeveloper.SetInitialLevel(target.Level);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterObject.cs
[32;1mLineNumber : [0m412
[32;1mLine       : [0m		Level = 1;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m166
[32;1mLine       : [0m	private HeroDeveloper _heroDeveloper;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m883
[32;1mLine       : [0m	public HeroDeveloper HeroDeveloper => _heroDeveloper;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m931
[32;1mLine       : [0m		collectedObjects.Add(_heroDeveloper);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1229
[32;1mLine       : [0m	internal static object AutoGeneratedGetMemberValue_heroDeveloper(object o)

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1231
[32;1mLine       : [0m		return ((Hero)o)._heroDeveloper;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1387
[32;1mLine       : [0m		_heroDeveloper?.AddSkillXp(skill, xpAmount);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1496
[32;1mLine       : [0m		_heroDeveloper = new HeroDeveloper(this);

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1616
[32;1mLine       : [0m		HeroDeveloper?.AfterLoad();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1851
[32;1mLine       : [0m		HeroDeveloper.InitializeHeroDeveloper();

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m1958
[32;1mLine       : [0m		_heroDeveloper = null;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
[32;1mLineNumber : [0m2242
[32;1mLine       : [0m		Level = characterObject.Level;

[32;1mPath       : [0mE:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\HeroCreator.cs
[32;1mLineNumber : [0m27
[32;1mLine       : [0m		public int Level { get; private set; }


 succeeded in 4759ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m118
[32;1mLine       : [0m    public bool IsTierAvailable(int heroLevel, int tier)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m121
[32;1mLine       : [0m        return heroLevel >= GetTierUnlockLevel(tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs
[32;1mLineNumber : [0m124
[32;1mLine       : [0m    public int GetTierUnlockLevel(int tier)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerRegistry.cs
[32;1mLineNumber : [0m16
[32;1mLine       : [0m    bool IsTierAvailable(int heroLevel, int tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerRegistry.cs
[32;1mLineNumber : [0m19
[32;1mLine       : [0m    int GetTierUnlockLevel(int tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m126
[32;1mLine       : [0m        Tier2Locked = !_registry.IsTierAvailable(_heroLevel, 2);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m127
[32;1mLine       : [0m        Tier3Locked = !_registry.IsTierAvailable(_heroLevel, 3);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m132
[32;1mLine       : [0m            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(2)).ToString();

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m134
[32;1mLine       : [0m            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(3)).ToString();

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m163
[32;1mLine       : [0m            var isLocked = !_registry.IsTierAvailable(_heroLevel, group.Tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m194
[32;1mLine       : [0m            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m238
[32;1mLine       : [0m            if (group != null && !_registry.IsTierAvailable(_heroLevel, group.Tier))

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m151
[32;1mLine       : [0m        Assert.IsTrue(_registry.IsTierAvailable(1, 1));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m157
[32;1mLine       : [0m        Assert.IsFalse(_registry.IsTierAvailable(9, 2));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m158
[32;1mLine       : [0m        Assert.IsTrue(_registry.IsTierAvailable(10, 2));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m        Assert.IsFalse(_registry.IsTierAvailable(19, 3));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m165
[32;1mLine       : [0m        Assert.IsTrue(_registry.IsTierAvailable(20, 3));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m171
[32;1mLine       : [0m        Assert.AreEqual(1, _registry.GetTierUnlockLevel(1));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m177
[32;1mLine       : [0m        Assert.AreEqual(10, _registry.GetTierUnlockLevel(2));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m183
[32;1mLine       : [0m        Assert.AreEqual(20, _registry.GetTierUnlockLevel(3));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m191
[32;1mLine       : [0m            var lvl = _registry.GetTierUnlockLevel(tier);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m192
[32;1mLine       : [0m            Assert.IsFalse(_registry.IsTierAvailable(lvl - 1, tier), $"tier {tier} should be locked below 
             its unlock level");

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m193
[32;1mLine       : [0m            Assert.IsTrue(_registry.IsTierAvailable(lvl, tier), $"tier {tier} should unlock at its unlock 
             level");

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m60
[32;1mLine       : [0m        _registry.IsTierAvailable(5, 1).Returns(true);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m61
[32;1mLine       : [0m        _registry.IsTierAvailable(5, 2).Returns(false);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m62
[32;1mLine       : [0m        _registry.IsTierAvailable(5, 3).Returns(false);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m128
[32;1mLine       : [0m        // Tier 2 requires level 10. Hero is level 5 → IsTierAvailable(5, 2) returns false.


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs' | Select-Object -Skip 2230 -First 25" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CharacterObject.cs' | Select-Object -Skip 390 -First 35" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'Main/Features/CareerSystem/UI/GauntletCareerScreen.cs'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 565ms:

	public override int MaxHitPoints()
	{
		return TaleWorlds.Library.MathF.Round(Campaign.Current.Models.CharacterStatsModel.MaxHitpoints(this).ResultNumber);
	}

	public CharacterObject()
	{
		Init();
	}

	[LoadInitializationCallback]
	private void OnLoad(MetaData metaData, ObjectLoadData objectLoadData)
	{
		Init();
	}

	private void Init()
	{
		_occupation = Occupation.NotAssigned;
		_characterTraits = new PropertyOwner<TraitObject>();
		Level = 1;
		_characterRestrictionFlags = CharacterRestrictionFlags.None;
	}

	public static CharacterObject CreateFrom(CharacterObject character, StaticBodyProperties? staticBodyProperties = null)
	{
		CharacterObject characterObject = MBObjectManager.Instance.CreateObject<CharacterObject>();
		characterObject._originCharacter = character._originCharacter ?? character;
		if (characterObject.IsHero)
		{
			if (staticBodyProperties.HasValue)
			{
				characterObject.HeroObject.StaticBodyProperties = staticBodyProperties.Value;
			}

 succeeded in 508ms:
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.UI;

// Uses GameStateScreen attribute so GameStateManager.PushState properly
// deactivates map bar input processing (avoids IndexOutOfRangeException
// in GauntletMapBarGlobalLayer.HandlePanelSwitchingInput).
[GameStateScreen(typeof(CareerScreenGameState))]
public class GauntletCareerScreen : ScreenBase, IGameStateListener
{
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _movie;
    private CareerScreenVM _viewModel;

    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerConfigProvider _configProvider;
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;

    public GauntletCareerScreen(CareerScreenGameState state)
    {
        var hero = Hero.MainHero;
        _dataService = IoC.Resolve<ICareerDataService>();
        _registry = IoC.Resolve<ICareerRegistry>();
        _passiveService = IoC.Resolve<ICareerPassiveService>();
        _configProvider = IoC.Resolve<ICareerConfigProvider>();
        _logger = IoC.Resolve<IModLogger>();
        _heroStringId = hero?.StringId ?? "";
        _heroLevel = hero?.Level ?? 0;
    }

    public static void OpenCareerScreen()
    {
        var logger = IoC.Resolve<IModLogger>();
        logger?.LogInfo("CareerSystem: OpenCareerScreen called");

        var hero = Hero.MainHero;
        if (hero == null)
        {
            logger?.LogWarning("CareerSystem: OpenCareerScreen — MainHero is null");
            return;
        }

        logger?.LogInfo($"CareerSystem: Opening career screen for hero '{hero.StringId}' level={hero.Level}");
        var state = Game.Current.GameStateManager.CreateState<CareerScreenGameState>();
        Game.Current.GameStateManager.PushState(state);
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();

        _gauntletLayer = new GauntletLayer("CareerScreen", 1);
        _gauntletLayer.IsFocusLayer = true;
        _viewModel = new CareerScreenVM(_dataService, _registry, _passiveService, _configProvider, _logger, _heroStringId, _heroLevel, CloseScreen);
        _movie = _gauntletLayer.LoadMovie("CareerScreen", _viewModel);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        AddLayer(_gauntletLayer);
        ScreenManager.TrySetFocus(_gauntletLayer);
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);

        if (_gauntletLayer.Input.IsKeyPressed(InputKey.Escape))
        {
            CloseScreen();
        }
    }

    private void CloseScreen()
    {
        _logger?.LogInfo("CareerSystem: Closing career screen");
        _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
        if (_movie != null)
            _gauntletLayer?.ReleaseMovie(_movie);
        _viewModel?.OnFinalize();
        Game.Current.GameStateManager.PopState();
    }

    protected override void OnFinalize()
    {
        base.OnFinalize();
        _viewModel = null;
        _gauntletLayer = null;
    }

    // IGameStateListener — required by GameStateScreenManager.OnCreateState
    // which registers null if the screen doesn't implement this interface.
    void IGameStateListener.OnInitialize() { }
    void IGameStateListener.OnFinalize() { }
    void IGameStateListener.OnActivate() { }
    void IGameStateListener.OnDeactivate() { }
}


 succeeded in 572ms:
			return PartyBelongedToAsPrisoner.Settlement;
		}
		return PartyBelongedTo;
	}

	private void SetInitialValuesFromCharacter(CharacterObject characterObject)
	{
		foreach (TraitObject item in TraitObject.All)
		{
			SetTraitLevel(item, characterObject.GetTraitLevel(item));
		}
		Level = characterObject.Level;
		SetName(characterObject.Name, characterObject.Name);
		Culture = characterObject.Culture;
		_defaultAge = characterObject.Age;
		if (_birthDay == CampaignTime.Zero)
		{
			_birthDay = HeroHelper.GetRandomBirthDayForAge(_defaultAge);
		}
		HitPoints = characterObject.MaxHitPoints();
		IsFemale = characterObject.IsFemale;
		Occupation = CharacterObject.GetDefaultOccupation();
		List<Equipment> list = characterObject.BattleEquipments.Where((Equipment x) => !x.IsEmpty()).ToList();
		List<Equipment> list2 = characterObject.CivilianEquipments.Where((Equipment x) => !x.IsEmpty()).ToList();
		List<Equipment> list3 = characterObject.StealthEquipments.Where((Equipment x) => !x.IsEmpty()).ToList();

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "ilspycmd 'E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\bin\\Win64_Shipping_Client\\TaleWorlds.Library.dll' -t TaleWorlds.Library.ViewModel | Select-String -Pattern 'OnPropertyChanged','OnPropertyChangedWithValue','PropertyChanged' -Context 2,4 | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'E:\\Decompiled_Bannerlord\\Core\\TaleWorlds.Library\\TaleWorlds.Library\\ViewModel.cs'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main/Features/CareerSystem/UI','TAOM.Tests/Features/CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'IsUnavailable|IsFreeToTake|IsTaken|OnPropertyChanged\\(' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1241ms:

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m9
[32;1mLine       : [0mpublic abstract class ViewModel : IViewModel, INotifyPropertyChanged
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m44
[32;1mLine       : [0m	private List<PropertyChangedEventHandler> _eventHandlers;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m46
[32;1mLine       : [0m	private List<PropertyChangedWithValueEventHandler> _eventHandlersWithValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m48
[32;1mLine       : [0m	private List<PropertyChangedWithBoolValueEventHandler> _eventHandlersWithBoolValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m50
[32;1mLine       : [0m	private List<PropertyChangedWithIntValueEventHandler> _eventHandlersWithIntValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m52
[32;1mLine       : [0m	private List<PropertyChangedWithFloatValueEventHandler> _eventHandlersWithFloatValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m54
[32;1mLine       : [0m	private List<PropertyChangedWithUIntValueEventHandler> _eventHandlersWithUIntValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m56
[32;1mLine       : [0m	private List<PropertyChangedWithColorValueEventHandler> _eventHandlersWithColorValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m58
[32;1mLine       : [0m	private List<PropertyChangedWithDoubleValueEventHandler> _eventHandlersWithDoubleValue;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m60
[32;1mLine       : [0m	private List<PropertyChangedWithVec2ValueEventHandler> _eventHandlersWithVec2Value;
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m68
[32;1mLine       : [0m	public event PropertyChangedEventHandler PropertyChanged
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m74
[32;1mLine       : [0m				_eventHandlers = new List<PropertyChangedEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m87
[32;1mLine       : [0m	public event PropertyChangedWithValueEventHandler PropertyChangedWithValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m93
[32;1mLine       : [0m				_eventHandlersWithValue = new List<PropertyChangedWithValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m106
[32;1mLine       : [0m	public event PropertyChangedWithBoolValueEventHandler PropertyChangedWithBoolValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m112
[32;1mLine       : [0m				_eventHandlersWithBoolValue = new List<PropertyChangedWithBoolValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m125
[32;1mLine       : [0m	public event PropertyChangedWithIntValueEventHandler PropertyChangedWithIntValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m131
[32;1mLine       : [0m				_eventHandlersWithIntValue = new List<PropertyChangedWithIntValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m144
[32;1mLine       : [0m	public event PropertyChangedWithFloatValueEventHandler PropertyChangedWithFloatValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m150
[32;1mLine       : [0m				_eventHandlersWithFloatValue = new List<PropertyChangedWithFloatValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m163
[32;1mLine       : [0m	public event PropertyChangedWithUIntValueEventHandler PropertyChangedWithUIntValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m169
[32;1mLine       : [0m				_eventHandlersWithUIntValue = new List<PropertyChangedWithUIntValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m182
[32;1mLine       : [0m	public event PropertyChangedWithColorValueEventHandler PropertyChangedWithColorValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m188
[32;1mLine       : [0m				_eventHandlersWithColorValue = new List<PropertyChangedWithColorValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m201
[32;1mLine       : [0m	public event PropertyChangedWithDoubleValueEventHandler PropertyChangedWithDoubleValue
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m207
[32;1mLine       : [0m				_eventHandlersWithDoubleValue = new List<PropertyChangedWithDoubleValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m220
[32;1mLine       : [0m	public event PropertyChangedWithVec2ValueEventHandler PropertyChangedWithVec2Value
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m226
[32;1mLine       : [0m				_eventHandlersWithVec2Value = new List<PropertyChangedWithVec2ValueEventHandler>();
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m270
[32;1mLine       : [0m		OnPropertyChanged(propertyName);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m274
[32;1mLine       : [0m	public void OnPropertyChanged([CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m280
[32;1mLine       : [0m				PropertyChangedEventHandler propertyChangedEventHandler = _eventHandlers[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m281
[32;1mLine       : [0m				PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m282
[32;1mLine       : [0m				propertyChangedEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m287
[32;1mLine       : [0m	public void OnPropertyChangedWithValue<T>(T value, [CallerMemberName] string propertyName = null) where 
             T : class
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m293
[32;1mLine       : [0m				PropertyChangedWithValueEventHandler propertyChangedWithValueEventHandler = 
             _eventHandlersWithValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m294
[32;1mLine       : [0m				PropertyChangedWithValueEventArgs e = new PropertyChangedWithValueEventArgs(propertyName, value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m295
[32;1mLine       : [0m				propertyChangedWithValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m300
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(bool value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m306
[32;1mLine       : [0m				PropertyChangedWithBoolValueEventHandler propertyChangedWithBoolValueEventHandler = 
             _eventHandlersWithBoolValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m307
[32;1mLine       : [0m				PropertyChangedWithBoolValueEventArgs e = new PropertyChangedWithBoolValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m308
[32;1mLine       : [0m				propertyChangedWithBoolValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m313
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(int value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m319
[32;1mLine       : [0m				PropertyChangedWithIntValueEventHandler propertyChangedWithIntValueEventHandler = 
             _eventHandlersWithIntValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m320
[32;1mLine       : [0m				PropertyChangedWithIntValueEventArgs e = new PropertyChangedWithIntValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m321
[32;1mLine       : [0m				propertyChangedWithIntValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m326
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(float value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m332
[32;1mLine       : [0m				PropertyChangedWithFloatValueEventHandler propertyChangedWithFloatValueEventHandler = 
             _eventHandlersWithFloatValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m333
[32;1mLine       : [0m				PropertyChangedWithFloatValueEventArgs e = new PropertyChangedWithFloatValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m334
[32;1mLine       : [0m				propertyChangedWithFloatValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m339
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(uint value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m345
[32;1mLine       : [0m				PropertyChangedWithUIntValueEventHandler propertyChangedWithUIntValueEventHandler = 
             _eventHandlersWithUIntValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m346
[32;1mLine       : [0m				PropertyChangedWithUIntValueEventArgs e = new PropertyChangedWithUIntValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m347
[32;1mLine       : [0m				propertyChangedWithUIntValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m352
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(Color value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m358
[32;1mLine       : [0m				PropertyChangedWithColorValueEventHandler propertyChangedWithColorValueEventHandler = 
             _eventHandlersWithColorValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m359
[32;1mLine       : [0m				PropertyChangedWithColorValueEventArgs e = new PropertyChangedWithColorValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m360
[32;1mLine       : [0m				propertyChangedWithColorValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m365
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(double value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m371
[32;1mLine       : [0m				PropertyChangedWithDoubleValueEventHandler propertyChangedWithDoubleValueEventHandler = 
             _eventHandlersWithDoubleValue[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m372
[32;1mLine       : [0m				PropertyChangedWithDoubleValueEventArgs e = new PropertyChangedWithDoubleValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m373
[32;1mLine       : [0m				propertyChangedWithDoubleValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m378
[32;1mLine       : [0m	public void OnPropertyChangedWithValue(Vec2 value, [CallerMemberName] string propertyName = null)
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mOnPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m384
[32;1mLine       : [0m				PropertyChangedWithVec2ValueEventHandler propertyChangedWithVec2ValueEventHandler = 
             _eventHandlersWithVec2Value[i];
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m385
[32;1mLine       : [0m				PropertyChangedWithVec2ValueEventArgs e = new PropertyChangedWithVec2ValueEventArgs(propertyName, 
             value);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m386
[32;1mLine       : [0m				propertyChangedWithVec2ValueEventHandler(this, e);
[32;1mFilename   : [0mInputStream
[32;1mPath       : [0mInputStream
[32;1mPattern    : [0mPropertyChanged
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}


 succeeded in 1483ms:
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TaleWorlds.Library;

public abstract class ViewModel : IViewModel, INotifyPropertyChanged
{
	public interface IViewModelGetterInterface
	{
		bool IsValueSynced(string name);

		Type GetPropertyType(string name);

		object GetPropertyValue(string name);

		void OnFinalize();
	}

	public interface IViewModelSetterInterface
	{
		void SetPropertyValue(string name, object value);

		void OnFinalize();
	}

	private class DataSourceTypeBindingPropertiesCollection
	{
		public Dictionary<string, PropertyInfo> Properties { get; set; }

		public Dictionary<string, MethodInfo> Methods { get; set; }

		public DataSourceTypeBindingPropertiesCollection(Dictionary<string, PropertyInfo> properties, Dictionary<string, MethodInfo> methods)
		{
			Properties = properties;
			Methods = methods;
		}
	}

	public static bool UIDebugMode;

	private List<PropertyChangedEventHandler> _eventHandlers;

	private List<PropertyChangedWithValueEventHandler> _eventHandlersWithValue;

	private List<PropertyChangedWithBoolValueEventHandler> _eventHandlersWithBoolValue;

	private List<PropertyChangedWithIntValueEventHandler> _eventHandlersWithIntValue;

	private List<PropertyChangedWithFloatValueEventHandler> _eventHandlersWithFloatValue;

	private List<PropertyChangedWithUIntValueEventHandler> _eventHandlersWithUIntValue;

	private List<PropertyChangedWithColorValueEventHandler> _eventHandlersWithColorValue;

	private List<PropertyChangedWithDoubleValueEventHandler> _eventHandlersWithDoubleValue;

	private List<PropertyChangedWithVec2ValueEventHandler> _eventHandlersWithVec2Value;

	private Type _type;

	private DataSourceTypeBindingPropertiesCollection _propertiesAndMethods;

	private static Dictionary<Type, DataSourceTypeBindingPropertiesCollection> _cachedViewModelProperties = new Dictionary<Type, DataSourceTypeBindingPropertiesCollection>();

	public event PropertyChangedEventHandler PropertyChanged
	{
		add
		{
			if (_eventHandlers == null)
			{
				_eventHandlers = new List<PropertyChangedEventHandler>();
			}
			_eventHandlers.Add(value);
		}
		remove
		{
			if (_eventHandlers != null)
			{
				_eventHandlers.Remove(value);
			}
		}
	}

	public event PropertyChangedWithValueEventHandler PropertyChangedWithValue
	{
		add
		{
			if (_eventHandlersWithValue == null)
			{
				_eventHandlersWithValue = new List<PropertyChangedWithValueEventHandler>();
			}
			_eventHandlersWithValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithValue != null)
			{
				_eventHandlersWithValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithBoolValueEventHandler PropertyChangedWithBoolValue
	{
		add
		{
			if (_eventHandlersWithBoolValue == null)
			{
				_eventHandlersWithBoolValue = new List<PropertyChangedWithBoolValueEventHandler>();
			}
			_eventHandlersWithBoolValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithBoolValue != null)
			{
				_eventHandlersWithBoolValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithIntValueEventHandler PropertyChangedWithIntValue
	{
		add
		{
			if (_eventHandlersWithIntValue == null)
			{
				_eventHandlersWithIntValue = new List<PropertyChangedWithIntValueEventHandler>();
			}
			_eventHandlersWithIntValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithIntValue != null)
			{
				_eventHandlersWithIntValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithFloatValueEventHandler PropertyChangedWithFloatValue
	{
		add
		{
			if (_eventHandlersWithFloatValue == null)
			{
				_eventHandlersWithFloatValue = new List<PropertyChangedWithFloatValueEventHandler>();
			}
			_eventHandlersWithFloatValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithFloatValue != null)
			{
				_eventHandlersWithFloatValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithUIntValueEventHandler PropertyChangedWithUIntValue
	{
		add
		{
			if (_eventHandlersWithUIntValue == null)
			{
				_eventHandlersWithUIntValue = new List<PropertyChangedWithUIntValueEventHandler>();
			}
			_eventHandlersWithUIntValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithUIntValue != null)
			{
				_eventHandlersWithUIntValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithColorValueEventHandler PropertyChangedWithColorValue
	{
		add
		{
			if (_eventHandlersWithColorValue == null)
			{
				_eventHandlersWithColorValue = new List<PropertyChangedWithColorValueEventHandler>();
			}
			_eventHandlersWithColorValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithColorValue != null)
			{
				_eventHandlersWithColorValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithDoubleValueEventHandler PropertyChangedWithDoubleValue
	{
		add
		{
			if (_eventHandlersWithDoubleValue == null)
			{
				_eventHandlersWithDoubleValue = new List<PropertyChangedWithDoubleValueEventHandler>();
			}
			_eventHandlersWithDoubleValue.Add(value);
		}
		remove
		{
			if (_eventHandlersWithDoubleValue != null)
			{
				_eventHandlersWithDoubleValue.Remove(value);
			}
		}
	}

	public event PropertyChangedWithVec2ValueEventHandler PropertyChangedWithVec2Value
	{
		add
		{
			if (_eventHandlersWithVec2Value == null)
			{
				_eventHandlersWithVec2Value = new List<PropertyChangedWithVec2ValueEventHandler>();
			}
			_eventHandlersWithVec2Value.Add(value);
		}
		remove
		{
			if (_eventHandlersWithVec2Value != null)
			{
				_eventHandlersWithVec2Value.Remove(value);
			}
		}
	}

	protected ViewModel()
	{
		_type = GetType();
		_cachedViewModelProperties.TryGetValue(_type, out var value);
		if (value == null)
		{
			_propertiesAndMethods = GetPropertiesOfType(_type);
			_cachedViewModelProperties.Add(_type, _propertiesAndMethods);
		}
		else
		{
			_propertiesAndMethods = value;
		}
	}

	private PropertyInfo GetProperty(string name)
	{
		if (_propertiesAndMethods != null && _propertiesAndMethods.Properties.TryGetValue(name, out var value))
		{
			return value;
		}
		return null;
	}

	protected bool SetField<T>(ref T field, T value, string propertyName)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		OnPropertyChanged(propertyName);
		return true;
	}

	public void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		if (_eventHandlers != null)
		{
			for (int i = 0; i < _eventHandlers.Count; i++)
			{
				PropertyChangedEventHandler propertyChangedEventHandler = _eventHandlers[i];
				PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
				propertyChangedEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue<T>(T value, [CallerMemberName] string propertyName = null) where T : class
	{
		if (_eventHandlersWithValue != null)
		{
			for (int i = 0; i < _eventHandlersWithValue.Count; i++)
			{
				PropertyChangedWithValueEventHandler propertyChangedWithValueEventHandler = _eventHandlersWithValue[i];
				PropertyChangedWithValueEventArgs e = new PropertyChangedWithValueEventArgs(propertyName, value);
				propertyChangedWithValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(bool value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithBoolValue != null)
		{
			for (int i = 0; i < _eventHandlersWithBoolValue.Count; i++)
			{
				PropertyChangedWithBoolValueEventHandler propertyChangedWithBoolValueEventHandler = _eventHandlersWithBoolValue[i];
				PropertyChangedWithBoolValueEventArgs e = new PropertyChangedWithBoolValueEventArgs(propertyName, value);
				propertyChangedWithBoolValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(int value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithIntValue != null)
		{
			for (int i = 0; i < _eventHandlersWithIntValue.Count; i++)
			{
				PropertyChangedWithIntValueEventHandler propertyChangedWithIntValueEventHandler = _eventHandlersWithIntValue[i];
				PropertyChangedWithIntValueEventArgs e = new PropertyChangedWithIntValueEventArgs(propertyName, value);
				propertyChangedWithIntValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(float value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithFloatValue != null)
		{
			for (int i = 0; i < _eventHandlersWithFloatValue.Count; i++)
			{
				PropertyChangedWithFloatValueEventHandler propertyChangedWithFloatValueEventHandler = _eventHandlersWithFloatValue[i];
				PropertyChangedWithFloatValueEventArgs e = new PropertyChangedWithFloatValueEventArgs(propertyName, value);
				propertyChangedWithFloatValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(uint value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithUIntValue != null)
		{
			for (int i = 0; i < _eventHandlersWithUIntValue.Count; i++)
			{
				PropertyChangedWithUIntValueEventHandler propertyChangedWithUIntValueEventHandler = _eventHandlersWithUIntValue[i];
				PropertyChangedWithUIntValueEventArgs e = new PropertyChangedWithUIntValueEventArgs(propertyName, value);
				propertyChangedWithUIntValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(Color value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithColorValue != null)
		{
			for (int i = 0; i < _eventHandlersWithColorValue.Count; i++)
			{
				PropertyChangedWithColorValueEventHandler propertyChangedWithColorValueEventHandler = _eventHandlersWithColorValue[i];
				PropertyChangedWithColorValueEventArgs e = new PropertyChangedWithColorValueEventArgs(propertyName, value);
				propertyChangedWithColorValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(double value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithDoubleValue != null)
		{
			for (int i = 0; i < _eventHandlersWithDoubleValue.Count; i++)
			{
				PropertyChangedWithDoubleValueEventHandler propertyChangedWithDoubleValueEventHandler = _eventHandlersWithDoubleValue[i];
				PropertyChangedWithDoubleValueEventArgs e = new PropertyChangedWithDoubleValueEventArgs(propertyName, value);
				propertyChangedWithDoubleValueEventHandler(this, e);
			}
		}
	}

	public void OnPropertyChangedWithValue(Vec2 value, [CallerMemberName] string propertyName = null)
	{
		if (_eventHandlersWithVec2Value != null)
		{
			for (int i = 0; i < _eventHandlersWithVec2Value.Count; i++)
			{
				PropertyChangedWithVec2ValueEventHandler propertyChangedWithVec2ValueEventHandler = _eventHandlersWithVec2Value[i];
				PropertyChangedWithVec2ValueEventArgs e = new PropertyChangedWithVec2ValueEventArgs(propertyName, value);
				propertyChangedWithVec2ValueEventHandler(this, e);
			}
		}
	}

	public object GetViewModelAtPath(BindingPath path, bool isList)
	{
		return GetViewModelAtPath(path);
	}

	public object GetViewModelAtPath(BindingPath path)
	{
		BindingPath subPath = path.SubPath;
		if (subPath != null)
		{
			PropertyInfo property = GetProperty(subPath.FirstNode);
			if (property != null)
			{
				object obj = property.GetGetMethod().InvokeWithLog(this, null);
				if (obj is ViewModel viewModel)
				{
					return viewModel.GetViewModelAtPath(subPath);
				}
				if (obj is IMBBindingList)
				{
					return GetChildAtPath(obj as IMBBindingList, subPath);
				}
			}
			return null;
		}
		return this;
	}

	private static object GetChildAtPath(IMBBindingList bindingList, BindingPath path)
	{
		BindingPath subPath = path.SubPath;
		if (subPath == null)
		{
			return bindingList;
		}
		if (bindingList.Count > 0)
		{
			int num = -1;
			num = Convert.ToInt32(subPath.FirstNode);
			if (num >= 0 && num < bindingList.Count)
			{
				object obj = bindingList[num];
				if (obj is ViewModel)
				{
					return (obj as ViewModel).GetViewModelAtPath(subPath);
				}
				if (obj is IMBBindingList)
				{
					return GetChildAtPath(obj as IMBBindingList, subPath);
				}
			}
		}
		return null;
	}

	public object GetPropertyValue(string name, PropertyTypeFeeder propertyTypeFeeder)
	{
		return GetPropertyValue(name);
	}

	public object GetPropertyValue(string name)
	{
		PropertyInfo property = GetProperty(name);
		object result = null;
		if (property != null)
		{
			result = property.GetGetMethod().InvokeWithLog(this, null);
		}
		return result;
	}

	public Type GetPropertyType(string name)
	{
		PropertyInfo property = GetProperty(name);
		if (property != null)
		{
			return property.PropertyType;
		}
		return null;
	}

	public void SetPropertyValue(string name, object value)
	{
		PropertyInfo property = GetProperty(name);
		if (property != null)
		{
			property.GetSetMethod()?.InvokeWithLog(this, value);
		}
	}

	public virtual void OnFinalize()
	{
	}

	public void ExecuteCommand(string commandName, object[] parameters)
	{
		MethodInfo methodInfo = null;
		if (_propertiesAndMethods != null && _propertiesAndMethods.Methods.TryGetValue(commandName, out var value))
		{
			methodInfo = value;
		}
		else
		{
			Type type = _type;
			while (type != null && methodInfo == null)
			{
				methodInfo = type.GetMethod(commandName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				type = type.BaseType;
			}
		}
		if (!(methodInfo != null))
		{
			return;
		}
		ParameterInfo[] parameters2 = methodInfo.GetParameters();
		if (parameters2.Length == parameters.Length)
		{
			object[] array = new object[parameters.Length];
			for (int i = 0; i < parameters.Length; i++)
			{
				object obj = parameters[i];
				Type parameterType = parameters2[i].ParameterType;
				array[i] = obj;
				if (obj is string && parameterType != typeof(string))
				{
					object obj2 = ConvertValueTo((string)obj, parameterType);
					array[i] = obj2;
				}
			}
			if (AreParametersCompatibleWithMethod(array, parameters2))
			{
				methodInfo.InvokeWithLog(this, array);
			}
		}
		else if (parameters2.Length == 0)
		{
			methodInfo.InvokeWithLog(this, null);
		}
	}

	private bool AreParametersCompatibleWithMethod(object[] parameters, ParameterInfo[] methodParameters)
	{
		if (parameters.Length != methodParameters.Length)
		{
			return false;
		}
		for (int i = 0; i < parameters.Length; i++)
		{
			object obj = parameters[i];
			ParameterInfo parameterInfo = methodParameters[i];
			if (obj != null && !parameterInfo.ParameterType.IsAssignableFrom(obj.GetType()))
			{
				return false;
			}
		}
		return true;
	}

	private static object ConvertValueTo(string value, Type parameterType)
	{
		object result = null;
		if (parameterType == typeof(string))
		{
			result = value;
		}
		else if (parameterType == typeof(int))
		{
			result = Convert.ToInt32(value);
		}
		else if (parameterType == typeof(float))
		{
			result = Convert.ToSingle(value);
		}
		return result;
	}

	public virtual void RefreshValues()
	{
	}

	public static void RefreshPropertyAndMethodInfos()
	{
		_cachedViewModelProperties.Clear();
		Assembly[] viewModelAssemblies = GetViewModelAssemblies();
		for (int i = 0; i < viewModelAssemblies.Length; i++)
		{
			List<Type> typesSafe = viewModelAssemblies[i].GetTypesSafe();
			for (int j = 0; j < typesSafe.Count; j++)
			{
				Type type = typesSafe[j];
				if (typeof(IViewModel).IsAssignableFrom(type) && typeof(IViewModel) != type)
				{
					DataSourceTypeBindingPropertiesCollection propertiesOfType = GetPropertiesOfType(type);
					_cachedViewModelProperties[type] = propertiesOfType;
				}
			}
		}
	}

	private static Assembly[] GetViewModelAssemblies()
	{
		List<Assembly> list = new List<Assembly>();
		Assembly assembly = typeof(ViewModel).Assembly;
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		list.Add(assembly);
		Assembly[] array = assemblies;
		foreach (Assembly assembly2 in array)
		{
			if (!(assembly2 != assembly))
			{
				continue;
			}
			AssemblyName[] referencedAssemblies = assembly2.GetReferencedAssemblies();
			for (int j = 0; j < referencedAssemblies.Length; j++)
			{
				if (referencedAssemblies[j].ToString() == assembly.GetName().ToString())
				{
					list.Add(assembly2);
					break;
				}
			}
		}
		return list.ToArray();
	}

	private static DataSourceTypeBindingPropertiesCollection GetPropertiesOfType(Type t)
	{
		_ = t.Name;
		Dictionary<string, PropertyInfo> dictionary = new Dictionary<string, PropertyInfo>();
		Dictionary<string, MethodInfo> dictionary2 = new Dictionary<string, MethodInfo>();
		PropertyInfo[] properties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (PropertyInfo propertyInfo in properties)
		{
			dictionary.Add(propertyInfo.Name, propertyInfo);
		}
		MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo in methods)
		{
			if (!dictionary2.ContainsKey(methodInfo.Name))
			{
				dictionary2.Add(methodInfo.Name, methodInfo);
			}
		}
		return new DataSourceTypeBindingPropertiesCollection(dictionary, dictionary2);
	}
}


 succeeded in 1689ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceGroupObjectVM.cs
[32;1mLineNumber : [0m44
[32;1mLine       : [0m            if (!_choices[i].IsTaken)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceGroupObjectVM.cs
[32;1mLineNumber : [0m59
[32;1mLine       : [0m            if (_choices[i].IsTaken)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m13
[32;1mLine       : [0m    private bool _isTaken;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m14
[32;1mLine       : [0m    private bool _isFreeToTake;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m        bool isTaken,

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m19
[32;1mLine       : [0m        bool isFreeToTake,

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m24
[32;1mLine       : [0m        _isTaken = isTaken;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m25
[32;1mLine       : [0m        _isFreeToTake = isFreeToTake && !isTaken;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m33
[32;1mLine       : [0m            IsTaken = true;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m            IsTaken = false;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m55
[32;1mLine       : [0m    // The prefab renders three tinted copies of the point-pip gated on IsTaken / IsFreeToTake /

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m56
[32;1mLine       : [0m    // IsUnavailable so every slot always shows a pip (gold / brown / dim) instead of a blank gap.

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m58
[32;1mLine       : [0m    public bool IsUnavailable => !_isTaken && !_isFreeToTake;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m61
[32;1mLine       : [0m    public bool IsTaken

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m63
[32;1mLine       : [0m        get => _isTaken;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m66
[32;1mLine       : [0m            if (_isTaken != value)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m68
[32;1mLine       : [0m                _isTaken = value;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m69
[32;1mLine       : [0m                OnPropertyChangedWithValue(value, nameof(IsTaken));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m70
[32;1mLine       : [0m                OnPropertyChanged(nameof(IsUnavailable));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m76
[32;1mLine       : [0m    public bool IsFreeToTake

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m78
[32;1mLine       : [0m        get => _isFreeToTake;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m81
[32;1mLine       : [0m            if (_isFreeToTake != value)

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m83
[32;1mLine       : [0m                _isFreeToTake = value;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m84
[32;1mLine       : [0m                OnPropertyChangedWithValue(value, nameof(IsFreeToTake));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs
[32;1mLineNumber : [0m85
[32;1mLine       : [0m                OnPropertyChanged(nameof(IsUnavailable));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m169
[32;1mLine       : [0m                var isTaken = _dataService.GetOrCreateData(_heroStringId).HasChoice(choice.Id);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m170
[32;1mLine       : [0m                var isFreeToTake = FreeCareerPoints > 0 && !isLocked;

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs
[32;1mLineNumber : [0m171
[32;1mLine       : [0m                groupVM.Choices.Add(new CareerChoiceObjectVM(choice, isTaken, isFreeToTake, 
             TrySelectChoice, TryDeselectChoice));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs
[32;1mLineNumber : [0m53
[32;1mLine       : [0m                OnPropertyChanged(nameof(HasCareer));

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m15
[32;1mLine       : [0m    public void IsUnavailable_NotTakenNotFree_ReturnsTrue()

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m        var vm = new CareerChoiceObjectVM(Choice(), isTaken: false, isFreeToTake: false);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m        Assert.IsTrue(vm.IsUnavailable);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m19
[32;1mLine       : [0m        Assert.IsFalse(vm.IsTaken);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m20
[32;1mLine       : [0m        Assert.IsFalse(vm.IsFreeToTake);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m24
[32;1mLine       : [0m    public void IsUnavailable_Taken_ReturnsFalse()

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m26
[32;1mLine       : [0m        var vm = new CareerChoiceObjectVM(Choice(), isTaken: true, isFreeToTake: false);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m27
[32;1mLine       : [0m        Assert.IsFalse(vm.IsUnavailable);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m31
[32;1mLine       : [0m    public void IsUnavailable_FreeToTake_ReturnsFalse()

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m33
[32;1mLine       : [0m        var vm = new CareerChoiceObjectVM(Choice(), isTaken: false, isFreeToTake: true);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m34
[32;1mLine       : [0m        Assert.IsTrue(vm.IsFreeToTake);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m35
[32;1mLine       : [0m        Assert.IsFalse(vm.IsUnavailable);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m    public void IsUnavailable_TakenAndFreeRequested_TreatedAsTakenNotUnavailable()

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m41
[32;1mLine       : [0m        // ctor coerces isFreeToTake to (isFreeToTake && !isTaken); taken wins.

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m42
[32;1mLine       : [0m        var vm = new CareerChoiceObjectVM(Choice(), isTaken: true, isFreeToTake: true);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m43
[32;1mLine       : [0m        Assert.IsTrue(vm.IsTaken);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m44
[32;1mLine       : [0m        Assert.IsFalse(vm.IsFreeToTake);

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerChoiceObjectVMTests.cs
[32;1mLineNumber : [0m45
[32;1mLine       : [0m        Assert.IsFalse(vm.IsUnavailable);


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/GUI/TAOMSpriteData.xml' -Pattern '<Name>CareerSystem\\career_point_pip</Name>' -Context 5,8 | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 468ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main/_Module/GUI/TAOMSpriteData.xml' -Pattern 'career_point' -Context 5,8 | Select-Object -First 4 | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 481ms:

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m7288
[32;1mLine       : [0m      <Name>CareerSystem\career_point_pip</Name>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0mcareer_point
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m10776
[32;1mLine       : [0m      <Name>CareerSystem\career_point_pip</Name>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0mcareer_point
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}

[32;1mIgnoreCase : [0mTrue
[32;1mLineNumber : [0m10777
[32;1mLine       : [0m      <SpritePartName>CareerSystem\career_point_pip</SpritePartName>
[32;1mFilename   : [0mTAOMSpriteData.xml
[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\_Module\GUI\TAOMSpriteData.xml
[32;1mPattern    : [0mcareer_point
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext
[32;1mMatches    : [0m{0}


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content 'Main/_Module/GUI/TAOMSpriteData.xml' | Select-Object -Skip 7278 -First 20; Get-Content 'Main/_Module/GUI/TAOMSpriteData.xml' | Select-Object -Skip 10770 -First 10" in C:\Users\mikew\source\repos\TAOM
 succeeded in 546ms:
      <Name>CareerSystem\plus_sign_icon</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>3764</SheetX>
      <SheetY>3124</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>2</SheetID>
      <Name>CareerSystem\career_point_pip</Name>
      <Width>256</Width>
      <Height>256</Height>
      <SheetX>0</SheetX>
      <SheetY>0</SheetY>
      <CategoryName>ui_taom_career_system</CategoryName>
    </SpritePart>
    <SpritePart>
      <SheetID>1</SheetID>
      <Name>CareerSystem\Abilities\captain_of_osgiliath_ability</Name>
      <Width>256</Width>
    <GenericSprite>
      <Name>CareerSystem\plus_sign_icon</Name>
      <SpritePartName>CareerSystem\plus_sign_icon</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\career_point_pip</Name>
      <SpritePartName>CareerSystem\career_point_pip</SpritePartName>
    </GenericSprite>
    <GenericSprite>
      <Name>CareerSystem\Abilities\captain_of_osgiliath_ability</Name>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'tools/apply_career_group_names.py'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'tools/apply_career_rank_names.py'" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'tools/career_group_names.json' | Select-Object -First 1" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content -Raw 'tools/career_rank_names.json' | Select-Object -First 1" in C:\Users\mikew\source\repos\TAOM
 succeeded in 518ms:
#!/usr/bin/env python3
"""Inject web-researched lore display names onto career <ChoiceGroup> elements.

Phase B of the career-screen revamp. Reads a reviewed mapping
(tools/career_group_names.json, produced by collating the
`career-group-lore-names` workflow output) and writes a
`display_name="{=taom_career_grp_<id>}<Name>"` attribute onto each matching
<ChoiceGroup> in taom_career_choices.xml. Also emits the companion
localization-string snippet for harvesting into taom_module_strings.xml.

Text/regex based (mirrors the other tools/apply_*.py scripts) so the 8800-line
choices file keeps its exact formatting, encoding, and CRLF line endings.

Idempotent: re-running replaces an existing display_name rather than duplicating.

Mapping JSON shape:
    { "<groupId>": { "name": "Wardens of Henneth Annun",
                     "sourceNote": "...", "attested": true }, ... }

Usage:
    python tools/apply_career_group_names.py --dry-run
    python tools/apply_career_group_names.py --apply
"""
import argparse
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_JSON = os.path.join(REPO, "tools", "career_group_names.json")
DEFAULT_XML = os.path.join(
    REPO, "Main", "_Module", "ModuleData", "career_system", "taom_career_choices.xml"
)
DEFAULT_STRINGS_SNIPPET = os.path.join(REPO, "tools", "career_group_strings.snippet.xml")


def xml_attr_escape(value):
    # Order matters: ampersand first. Escapes the set that is unsafe inside a
    # double-quoted XML attribute value.
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def key_for(group_id):
    return "taom_career_grp_" + group_id


def set_display_name(xml, group_id, name):
    """Insert/replace display_name on the <ChoiceGroup id="group_id"> opening tag.

    Returns (new_xml, status) where status is 'set', 'unchanged', or 'missing'.
    """
    tag_re = re.compile(
        r'<ChoiceGroup\b[^>]*\bid="' + re.escape(group_id) + r'"[^>]*?>'
    )
    m = tag_re.search(xml)
    if not m:
        return xml, "missing"

    tag = m.group(0)
    attr_value = "{=" + key_for(group_id) + "}" + xml_attr_escape(name)
    new_attr = 'display_name="' + attr_value + '"'

    # Strip any existing display_name on this tag.
    stripped = re.sub(r'\s+display_name="[^"]*"', "", tag)

    # Insert the fresh attribute immediately after the id="..." token. Use a
    # function replacement (NOT a \1 backref string) so a name containing
    # backslashes/digits can never corrupt the output. (feedback_re_sub_backref)
    def _ins(mm):
        return mm.group(0) + " " + new_attr

    new_tag = re.sub(
        r'\bid="' + re.escape(group_id) + r'"', _ins, stripped, count=1
    )

    if new_tag == tag:
        return xml, "unchanged"
    return xml[: m.start()] + new_tag + xml[m.end():], "set"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--json", default=DEFAULT_JSON)
    ap.add_argument("--xml", default=DEFAULT_XML)
    ap.add_argument("--strings-snippet", default=DEFAULT_STRINGS_SNIPPET)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", default=True)
    g.add_argument("--apply", dest="dry_run", action="store_false")
    args = ap.parse_args()

    if not os.path.exists(args.json):
        sys.exit("ERROR: mapping not found: %s (run/collate the workflow first)" % args.json)
    if not os.path.exists(args.xml):
        sys.exit("ERROR: choices xml not found: %s" % args.xml)

    with open(args.json, "r", encoding="utf-8") as f:
        mapping = json.load(f)

    # Read as bytes so we can preserve a leading BOM (if any) and CRLF byte-for-byte
    # on write (tools/README.md "XML I/O convention").
    raw = open(args.xml, "rb").read()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    xml = (raw[3:] if had_bom else raw).decode("utf-8")

    set_n = unchanged_n = missing_n = 0
    missing = []
    string_lines = []
    examples = []

    for group_id, entry in sorted(mapping.items()):
        if group_id.startswith("_"):  # skip _meta and any other metadata keys
            continue
        name = entry["name"] if isinstance(entry, dict) else str(entry)
        xml, status = set_display_name(xml, group_id, name)
        if status == "set":
            set_n += 1
            if len(examples) < 8:
                examples.append((group_id, name))
        elif status == "unchanged":
            unchanged_n += 1
        else:
            missing_n += 1
            missing.append(group_id)
        string_lines.append(
            '\t<string id="%s" text="{=%s}%s" />'
            % (key_for(group_id), key_for(group_id), xml_attr_escape(name))
        )

    print("groups in mapping : %d" % len(mapping))
    print("display_name set  : %d" % set_n)
    print("already current   : %d" % unchanged_n)
    print("NOT FOUND in xml  : %d" % missing_n)
    if missing:
        print("  missing ids: " + ", ".join(missing[:20]) + (" ..." if len(missing) > 20 else ""))
    if examples:
        print("examples:")
        for gid, name in examples:
            print("  %s -> %s" % (gid, name))

    if args.dry_run:
        print("\n[DRY-RUN] no files written. Re-run with --apply to write.")
        return

    out = (b"\xef\xbb\xbf" if had_bom else b"") + xml.encode("utf-8")
    open(args.xml, "wb").write(out)
    with open(args.strings_snippet, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("<!-- Career choice-group path names (harvest into taom_module_strings.xml) -->\n")
        f.write("\n".join(string_lines) + "\n")
    print("\n[APPLIED] wrote %s" % args.xml)
    print("[APPLIED] wrote loc snippet %s (%d strings)" % (args.strings_snippet, len(string_lines)))


if __name__ == "__main__":
    main()


 succeeded in 516ms:
#!/usr/bin/env python3
"""Inject web-researched per-tier RANK titles onto <Career> elements.

Phase C of the career-screen revamp (TOR_Core adoption). Reads the reviewed
mapping (tools/career_rank_names.json) and writes
rank1_name/rank2_name/rank3_name="{=taom_career_rank{N}_<id>}<Name>" onto each
matching <Career> in taom_careers.xml. Emits the companion localization snippet.

Text/regex based (mirrors tools/apply_career_group_names.py) so the file keeps
its exact formatting, encoding (UTF-8 BOM), and CRLF line endings. Idempotent.

Usage:
    python tools/apply_career_rank_names.py --dry-run
    python tools/apply_career_rank_names.py --apply
"""
import argparse
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_JSON = os.path.join(REPO, "tools", "career_rank_names.json")
DEFAULT_XML = os.path.join(
    REPO, "Main", "_Module", "ModuleData", "career_system", "taom_careers.xml"
)
DEFAULT_STRINGS_SNIPPET = os.path.join(REPO, "tools", "career_rank_strings.snippet.xml")

RANKS = ("rank1", "rank2", "rank3")


def xml_attr_escape(value):
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def key_for(rank, career_id):
    return "taom_career_%s_%s" % (rank, career_id)


def set_rank_names(xml, career_id, ranks):
    """Insert/replace rank{1,2,3}_name on the <Career id="career_id"> opening tag."""
    # The <Career ...> opening tag spans multiple lines; [^>] matches newlines so
    # this captures the whole tag up to its first '>'.
    tag_re = re.compile(r'<Career\b[^>]*\bid="' + re.escape(career_id) + r'"[^>]*?>')
    m = tag_re.search(xml)
    if not m:
        return xml, "missing"

    tag = m.group(0)
    # Strip any existing rank attributes (idempotent).
    stripped = re.sub(r'\s+rank[123]_name="[^"]*"', "", tag)

    attrs = []
    for r in RANKS:
        name = ranks[r]["name"] if isinstance(ranks[r], dict) else str(ranks[r])
        attrs.append('%s_name="{=%s}%s"' % (r, key_for(r, career_id), xml_attr_escape(name)))
    insert = " " + " ".join(attrs)

    # Insert right after the id="..." token (function repl avoids backref hazards).
    def _ins(mm):
        return mm.group(0) + insert

    new_tag = re.sub(r'\bid="' + re.escape(career_id) + r'"', _ins, stripped, count=1)
    if new_tag == tag:
        return xml, "unchanged"
    return xml[: m.start()] + new_tag + xml[m.end():], "set"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--json", default=DEFAULT_JSON)
    ap.add_argument("--xml", default=DEFAULT_XML)
    ap.add_argument("--strings-snippet", default=DEFAULT_STRINGS_SNIPPET)
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--dry-run", action="store_true", default=True)
    g.add_argument("--apply", dest="dry_run", action="store_false")
    args = ap.parse_args()

    if not os.path.exists(args.json):
        sys.exit("ERROR: mapping not found: %s" % args.json)
    if not os.path.exists(args.xml):
        sys.exit("ERROR: careers xml not found: %s" % args.xml)

    with open(args.json, "r", encoding="utf-8") as f:
        mapping = json.load(f)
    # Read as bytes to preserve a leading BOM (if any) + CRLF byte-for-byte on write
    # (tools/README.md "XML I/O convention").
    raw = open(args.xml, "rb").read()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    xml = (raw[3:] if had_bom else raw).decode("utf-8")

    set_n = unchanged_n = missing_n = 0
    missing = []
    string_lines = []
    examples = []

    for career_id, ranks in sorted(mapping.items()):
        if career_id.startswith("_"):
            continue
        xml, status = set_rank_names(xml, career_id, ranks)
        if status == "set":
            set_n += 1
            if len(examples) < 6:
                trio = " / ".join(ranks[r]["name"] for r in RANKS)
                examples.append((career_id, trio))
        elif status == "unchanged":
            unchanged_n += 1
        else:
            missing_n += 1
            missing.append(career_id)
        for r in RANKS:
            name = ranks[r]["name"] if isinstance(ranks[r], dict) else str(ranks[r])
            string_lines.append(
                '\t<string id="%s" text="{=%s}%s" />'
                % (key_for(r, career_id), key_for(r, career_id), xml_attr_escape(name))
            )

    print("careers in mapping : %d" % len([k for k in mapping if not k.startswith("_")]))
    print("rank names set     : %d careers (%d attrs)" % (set_n, set_n * 3))
    print("already current    : %d" % unchanged_n)
    print("NOT FOUND in xml   : %d" % missing_n)
    if missing:
        print("  missing: " + ", ".join(missing))
    for cid, trio in examples:
        print("  %s -> %s" % (cid, trio))

    if args.dry_run:
        print("\n[DRY-RUN] no files written. Re-run with --apply.")
        return

    out = (b"\xef\xbb\xbf" if had_bom else b"") + xml.encode("utf-8")
    open(args.xml, "wb").write(out)
    with open(args.strings_snippet, "w", encoding="utf-8", newline="\r\n") as f:
        f.write("<!-- Career per-tier rank titles (harvest into taom_module_strings.xml) -->\n")
        f.write("\n".join(string_lines) + "\n")
    print("\n[APPLIED] wrote %s" % args.xml)
    print("[APPLIED] wrote loc snippet %s (%d strings)" % (args.strings_snippet, len(string_lines)))


if __name__ == "__main__":
    main()


 succeeded in 557ms:
{
  "_meta": {
    "purpose": "Reviewed web-researched Middle-earth lore names for career choice-group sub-paths (TAOM career screen revamp, Phase B).",
    "format": "<group_id>: { name, attested, note }. attested=true means the term/figure/place is canonical Tolkien lore; attested=false is an honest original coined in-keeping (or derived from a TAOM-original id).",
    "applied_by": "tools/apply_career_group_names.py (writes display_name=\"{=taom_career_grp_<id>}<name>\" onto each <ChoiceGroup>)",
    "sources": "Per-faction research via tolkiengateway.net, lotr.fandom.com, encyclopedia-of-arda.com",
    "count": 294
  },

  "ranger_of_ithilien_t1_a": { "name": "Hunters of the Ithilien Road", "attested": false, "note": "Rangers ambushed enemy columns on the Ithilien roads" },
  "ranger_of_ithilien_t1_b": { "name": "Watchers of Henneth Annûn", "attested": true, "note": "Henneth Annûn = Faramir's hidden refuge behind the waterfall" },
  "ranger_of_ithilien_t2_a": { "name": "Scouts of North Ithilien", "attested": false, "note": "Rangers ranged/scouted across North Ithilien" },
  "ranger_of_ithilien_t2_b": { "name": "Bowmen of Faramir", "attested": false, "note": "Faramir was Captain of the bow-armed Rangers" },
  "ranger_of_ithilien_t3_a": { "name": "Arrows of the Forbidden Pool", "attested": false, "note": "Forbidden Pool below Henneth Annûn" },
  "ranger_of_ithilien_t3_b": { "name": "Shadows of Emyn Arnen", "attested": false, "note": "Emyn Arnen = hills of Ithilien, Faramir's princedom" },
  "captain_of_osgiliath_t1_a": { "name": "Wardens of the East Bank", "attested": false, "note": "Gondor contested the eastern shore of the Anduin at Osgiliath" },
  "captain_of_osgiliath_t1_b": { "name": "Keepers of the Anduin Crossing", "attested": false, "note": "Osgiliath's garrison held the river crossing" },
  "captain_of_osgiliath_t2_a": { "name": "Vanguard of the Rammas", "attested": false, "note": "Rammas Echor = Gondor's outer wall about the Pelennor" },
  "captain_of_osgiliath_t2_b": { "name": "Swords of the Last Bridge", "attested": false, "note": "Boromir & Faramir held Osgiliath's great bridge until its fall" },
  "captain_of_osgiliath_t3_a": { "name": "Host of Faramir", "attested": false, "note": "Faramir commanded the defence of Osgiliath" },
  "captain_of_osgiliath_t3_b": { "name": "Lords of the Dome of Stars", "attested": false, "note": "Dome of Stars housed Osgiliath's chief palantír" },
  "knight_of_belfalas_t1_a": { "name": "Lancers of Dol Amroth", "attested": false, "note": "Dol Amroth, chief city of Belfalas, fielded mounted knights" },
  "knight_of_belfalas_t1_b": { "name": "Wardens of the Bay", "attested": false, "note": "Dol Amroth guarded the Bay of Belfalas" },
  "knight_of_belfalas_t2_a": { "name": "Riders of the Silver Swan", "attested": false, "note": "Swan-knights bore a silver swan-ship banner" },
  "knight_of_belfalas_t2_b": { "name": "Shieldwall of Imrahil", "attested": false, "note": "Imrahil led the swan-knights; sortie saved Faramir" },
  "knight_of_belfalas_t3_a": { "name": "Knights of the Swan-Prow", "attested": true, "note": "Swan-Knights of Dol Amroth (House of Dol Amroth)" },
  "knight_of_belfalas_t3_b": { "name": "Charge of Dol Amroth", "attested": false, "note": "Imrahil's charge at the Pelennor; matches keystone text" },

  "black_uruk_captain_t1_a": { "name": "Forge-Tempered Hide", "attested": false, "note": "Black Uruks bred by Sauron; durability theme" },
  "black_uruk_captain_t1_b": { "name": "First Blood-Frenzy", "attested": false, "note": "tracks 'Blood Frenzy' rage-duration keystone" },
  "black_uruk_captain_t2_a": { "name": "Berserkers of Lugbúrz", "attested": false, "note": "Lugbúrz = Black Speech for Barad-dûr (attested term)" },
  "black_uruk_captain_t2_b": { "name": "Unending Wrath", "attested": false, "note": "sustain path; coined" },
  "black_uruk_captain_t3_a": { "name": "Wrath of the Lidless Eye", "attested": false, "note": "'Lidless Eye' attested imagery; keystone 'Eye of Sauron'" },
  "black_uruk_captain_t3_b": { "name": "Captains of the Black Gate", "attested": false, "note": "Black Gate (Morannon) attested; leadership phrasing coined" },
  "mulkerhili_cultist_t1_a": { "name": "Venomed Litany", "attested": false, "note": "coined for TAOM-original cult; poisoned-archery theme" },
  "mulkerhili_cultist_t1_b": { "name": "Sauron's Whispered Word", "attested": false, "note": "Ring's whispers attested theme; phrasing coined" },
  "mulkerhili_cultist_t2_a": { "name": "Congregation of Dread", "attested": false, "note": "matches keystone name" },
  "mulkerhili_cultist_t2_b": { "name": "Sermon of the Eye", "attested": false, "note": "the Eye attested; ritual phrasing coined" },
  "mulkerhili_cultist_t3_a": { "name": "Gaze of the Dark Lord", "attested": false, "note": "'Dark Lord' attested for Sauron; phrasing coined" },
  "mulkerhili_cultist_t3_b": { "name": "The Ring's Dark Covenant", "attested": false, "note": "One Ring's binding power attested; covenant coined" },
  "snaga_rider_t1_a": { "name": "Shadow Gallop", "attested": false, "note": "matches keystone; Snaga = attested Black Speech 'slave/lesser orc'" },
  "snaga_rider_t1_b": { "name": "Riders of the Dark", "attested": false, "note": "coined; shadow-cloaked raiders" },
  "snaga_rider_t2_a": { "name": "Strike from Shadow", "attested": false, "note": "tracks 'Ambush Surge' keystone" },
  "snaga_rider_t2_b": { "name": "Ghosts of Cirith Ungol", "attested": false, "note": "Cirith Ungol attested (garrisoned by Snaga); ghost phrasing coined" },
  "snaga_rider_t3_a": { "name": "Stampede of Nurn", "attested": false, "note": "Nurn attested (southern Mordor); stampede coined" },
  "snaga_rider_t3_b": { "name": "Vanguard of Mordor", "attested": false, "note": "Mordor attested; vanguard coined" },
  "olog_hai_warchief_t1_a": { "name": "Earth-Render", "attested": false, "note": "coined; ground-stomp AoE" },
  "olog_hai_warchief_t1_b": { "name": "Tireless Hammer", "attested": false, "note": "coined; relentless smasher" },
  "olog_hai_warchief_t2_a": { "name": "Rampaging Colossus", "attested": false, "note": "matches keystone; Olog-hai surpassed Uruk-hai in size" },
  "olog_hai_warchief_t2_b": { "name": "Sunlit Onslaught", "attested": false, "note": "Olog-hai's attested trait: enduring sunlight; phrasing coined" },
  "olog_hai_warchief_t3_a": { "name": "Hammer of the Morannon", "attested": false, "note": "Morannon attested; Olog-hai shock troops there; hammer coined" },
  "olog_hai_warchief_t3_b": { "name": "Troll-folk of Sauron", "attested": false, "note": "'Olog-hai'=troll-folk bred by Sauron (attested terms arranged)" },

  "marksman_of_aldburg_t1_a": { "name": "Hawks of the Eastfold", "attested": false, "note": "Aldburg = seat of the Eastfold (attested); 'Hawks' coined" },
  "marksman_of_aldburg_t1_b": { "name": "Wardens of the Folde", "attested": false, "note": "the Folde = central Eastfold (attested); epithet coined" },
  "marksman_of_aldburg_t2_a": { "name": "Captains of the Muster", "attested": false, "note": "Muster of Edoras attested; phrasing coined" },
  "marksman_of_aldburg_t2_b": { "name": "Shadow of Fangorn", "attested": false, "note": "from the career's own 'Forged in Fangorn's Shadow'; Fangorn attested" },
  "marksman_of_aldburg_t3_a": { "name": "Bowmen of the Mark", "attested": false, "note": "'the Mark' attested (Rohan); descriptor coined" },
  "marksman_of_aldburg_t3_b": { "name": "Host of the Riddermark", "attested": false, "note": "Riddermark attested; phrasing coined" },
  "eotheod_windrider_t1_a": { "name": "Heirs of Eorl", "attested": false, "note": "Eorl the Young attested; title coined in Rohirric style" },
  "eotheod_windrider_t1_b": { "name": "Riders of the Wold", "attested": false, "note": "the Wold = N. Rohan plain (attested); phrasing coined" },
  "eotheod_windrider_t2_a": { "name": "Oath of Eorl", "attested": true, "note": "Oath of Eorl/Cirion = the eternal Rohan-Gondor alliance (attested)" },
  "eotheod_windrider_t2_b": { "name": "Lords of the Mearas", "attested": false, "note": "Mearas = noble horse-line (attested); from 'Mearas-Bred' passive" },
  "eotheod_windrider_t3_a": { "name": "Charge of the Pelennor", "attested": true, "note": "Charge of the Rohirrim at the Pelennor (attested battle)" },
  "eotheod_windrider_t3_b": { "name": "Song of the Riddermark", "attested": false, "note": "Riddermark attested; from 'Songs of the Riddermark' passive" },
  "watchman_of_stangard_t1_a": { "name": "Fords of the Limlight", "attested": false, "note": "Limlight attested (N. border river); phrasing coined" },
  "watchman_of_stangard_t1_b": { "name": "Watch of the Wold", "attested": false, "note": "the Wold attested; phrasing coined" },
  "watchman_of_stangard_t2_a": { "name": "Shieldmen of the Entwash", "attested": false, "note": "Entwash attested; from career's 'Entwash Hold' passives" },
  "watchman_of_stangard_t2_b": { "name": "Wardens of the Anduin", "attested": false, "note": "Anduin attested; career patrols its crossings" },
  "watchman_of_stangard_t3_a": { "name": "Bastion of the Mark", "attested": false, "note": "'the Mark' attested; from keystone 'Stangard's Bastion'" },
  "watchman_of_stangard_t3_b": { "name": "Wardens of the Riddermark", "attested": false, "note": "Riddermark attested; from 'Warden of the Mark' passive" },

  "avanc_luth_raider_t1_a": { "name": "Hill-Clan Berserker", "attested": false, "note": "Dunlendings = hill-clans (attested lore); berserker coined" },
  "avanc_luth_raider_t1_b": { "name": "Sworn Axe-Thrower", "attested": false, "note": "from the Axe Thrower passive; Celtic-flavored" },
  "avanc_luth_raider_t2_a": { "name": "Blood-Debt Reaver", "attested": false, "note": "keystone 'Blood Debt'; Dunlending feuds attested" },
  "avanc_luth_raider_t2_b": { "name": "Warchant Hillman", "attested": false, "note": "from Warchant-of-the-Hills passive; 'Wild Men of the hills'" },
  "avanc_luth_raider_t3_a": { "name": "Avanc-lúth War-Chieftain", "attested": false, "note": "Avanc-lúth = TAOM-coined clan (avanc = Welsh water-monster)" },
  "avanc_luth_raider_t3_b": { "name": "Lord of the Warhost", "attested": false, "note": "Wulf marshalled a Dunlending host (attested); title coined" },
  "wolfskin_hunter_t1_a": { "name": "Pack-Marked Stalker", "attested": false, "note": "from Pack Leader's Mark; wolf-pack motif is TAOM theme" },
  "wolfskin_hunter_t1_b": { "name": "Wolfskin Skulker", "attested": false, "note": "from Wolfskin Cloak passive" },
  "wolfskin_hunter_t2_a": { "name": "Throat-Shot Hunter", "attested": false, "note": "from the Throat Shot armour-pen passive" },
  "wolfskin_hunter_t2_b": { "name": "Ghost of the Hills", "attested": false, "note": "career moves 'like a ghost' through the foothills" },
  "wolfskin_hunter_t3_a": { "name": "Apex Wolfskin", "attested": false, "note": "from keystone 'Apex Predator'" },
  "wolfskin_hunter_t3_b": { "name": "Master of the Great Pack", "attested": false, "note": "from 'Great Pack' +party-size; wolf-pack motif TAOM-coined" },
  "clanguard_rider_t1_a": { "name": "Clan Thunder-Rider", "attested": false, "note": "from keystone 'Clan Thunder'" },
  "clanguard_rider_t1_b": { "name": "Sworn Clan-Escort", "attested": false, "note": "from Clan Escort passive; 'clanguard' identity" },
  "clanguard_rider_t2_a": { "name": "Warpack Vanguard", "attested": false, "note": "from 'Warpack Unleashed' keystone" },
  "clanguard_rider_t2_b": { "name": "Banner of the Hills", "attested": false, "note": "rallying support line; no canon Dunland heraldry" },
  "clanguard_rider_t3_a": { "name": "Lord of All Riders", "attested": false, "note": "from keystone 'Clan of Riders'" },
  "clanguard_rider_t3_b": { "name": "Everlasting Warpack", "attested": false, "note": "from keystone 'Ever Charging'/'Endless Warpack'" },

  "blademaster_of_ren_t1_a": { "name": "Court Duelists", "attested": false, "note": "Rên = TAOM-original Khand place; warrior-court duelists" },
  "blademaster_of_ren_t1_b": { "name": "Steadfast Blades", "attested": false, "note": "original; defensive dueling" },
  "blademaster_of_ren_t2_a": { "name": "Relentless Edge", "attested": false, "note": "original; aggressive precision" },
  "blademaster_of_ren_t2_b": { "name": "Wardens of Rên", "attested": false, "note": "Rên = TAOM-original place" },
  "blademaster_of_ren_t3_a": { "name": "Whirlwind Slayers", "attested": false, "note": "original; expanded Twin Strike radius" },
  "blademaster_of_ren_t3_b": { "name": "Unbroken Swordlords", "attested": false, "note": "original; peak durability" },
  "steppe_bowmaster_t1_a": { "name": "Plains Skirmishers", "attested": false, "note": "Khand = steppe land (attested); descriptor" },
  "steppe_bowmaster_t1_b": { "name": "Steady Volley", "attested": false, "note": "original; sustained fire" },
  "steppe_bowmaster_t2_a": { "name": "Endless Quiver", "attested": false, "note": "mirrors 'neverending quiver' keystone" },
  "steppe_bowmaster_t2_b": { "name": "Volley Wardens", "attested": false, "note": "original; troop fire support" },
  "steppe_bowmaster_t3_a": { "name": "Stormcallers of Khand", "attested": false, "note": "Khand attested; 'Stormcaller' coined" },
  "steppe_bowmaster_t3_b": { "name": "Unceasing Hail", "attested": false, "note": "original; sustained suppression" },
  "chariot_warlord_t1_a": { "name": "Scythe-Riders", "attested": false, "note": "Variags' scythed chariots; descriptor coined" },
  "chariot_warlord_t1_b": { "name": "Iron Wheels", "attested": false, "note": "original; chariot momentum" },
  "chariot_warlord_t2_a": { "name": "Reaping Charge", "attested": false, "note": "mirrors 'wheels never stop' keystone" },
  "chariot_warlord_t2_b": { "name": "Warded Teams", "attested": false, "note": "chariot draft-team resilience; coined" },
  "chariot_warlord_t3_a": { "name": "Pelennor Reapers", "attested": false, "note": "Variags fought at the Pelennor (attested battle); 'Reapers' coined" },
  "chariot_warlord_t3_b": { "name": "Wheels of Khand", "attested": false, "note": "Khand attested; sustained momentum" },

  "tribesman_of_jelut_t1_a": { "name": "Dune Raiders", "attested": false, "note": "Haradrim desert dwellers; Jelut = TAOM-coined place" },
  "tribesman_of_jelut_t1_b": { "name": "Desert Survivors", "attested": false, "note": "Haradwaith survival; generic imagery" },
  "tribesman_of_jelut_t2_a": { "name": "Serpent-Fang Strikers", "attested": false, "note": "serpent/venom echoes Harad's black-serpent banner" },
  "tribesman_of_jelut_t2_b": { "name": "Wearers of the Sands", "attested": false, "note": "desert-tribe endurance; coined" },
  "tribesman_of_jelut_t3_a": { "name": "Venom of Haradwaith", "attested": false, "note": "Haradwaith attested; poison theme" },
  "tribesman_of_jelut_t3_b": { "name": "Scourge of the Southrons", "attested": false, "note": "'Southrons' attested Westron name; title coined" },
  "pezarsani_javelineer_t1_a": { "name": "Sun-Spear Skirmishers", "attested": false, "note": "sun/desert imagery; Pezarsan = TAOM-coined place" },
  "pezarsani_javelineer_t1_b": { "name": "Roving Outriders", "attested": false, "note": "fast screening skirmishers; coined" },
  "pezarsani_javelineer_t2_a": { "name": "Sunfire Slingers", "attested": false, "note": "from 'Sunfire Volley' ability + scarlet/gold motif" },
  "pezarsani_javelineer_t2_b": { "name": "Scarlet Screen", "attested": false, "note": "scarlet = attested Haradrim colour" },
  "pezarsani_javelineer_t3_a": { "name": "Rain of Far Harad", "attested": false, "note": "Far Harad attested; 'Rain' = javelin barrage" },
  "pezarsani_javelineer_t3_b": { "name": "Shadow of the Dunes", "attested": false, "note": "elusive desert harriers; coined" },
  "mahud_beast_rider_t1_a": { "name": "Tusk Vanguard", "attested": false, "note": "mûmakil tusks; 'Mahûd' is a GW term used by TAOM, not Tolkien" },
  "mahud_beast_rider_t1_b": { "name": "Beast-Wardens", "attested": false, "note": "Far Harad beast-tamers" },
  "mahud_beast_rider_t2_a": { "name": "Trampling Charge", "attested": false, "note": "enraged mûmak trampling (Pelennor) attested; title coined" },
  "mahud_beast_rider_t2_b": { "name": "Howdah Guardians", "attested": false, "note": "'howdah' = the war-tower on the mûmak's back" },
  "mahud_beast_rider_t3_a": { "name": "Wrath of Mûmakan", "attested": false, "note": "'mûmak' attested; 'Mûmakan' coined homeland; from the ability" },
  "mahud_beast_rider_t3_b": { "name": "Lords of the Oliphaunts", "attested": false, "note": "'oliphaunt' = Hobbits' attested name for the mûmak" },

  "codyan_legionaire_t1_a": { "name": "Côdyan Vanguard", "attested": false, "note": "Côdya/Côdyan = TAOM-coined Easterling formation" },
  "codyan_legionaire_t1_b": { "name": "Steadfast Shieldmen", "attested": false, "note": "generic Easterling shieldwall; defensive duration" },
  "codyan_legionaire_t2_a": { "name": "Wall of Rhûn", "attested": true, "note": "Rhûn attested (East-lands); shieldwall imagery TAOM's" },
  "codyan_legionaire_t2_b": { "name": "Ironclad Phalanx", "attested": false, "note": "TAOM-flavored; not canon terminology" },
  "codyan_legionaire_t3_a": { "name": "The Unbroken Line", "attested": false, "note": "elite offensive; coined" },
  "codyan_legionaire_t3_b": { "name": "The Wall That Never Falls", "attested": false, "note": "elite defensive; coined" },
  "lokhas_drus_marksman_t1_a": { "name": "Volley of Rhûn", "attested": true, "note": "Rhûn attested" },
  "lokhas_drus_marksman_t1_b": { "name": "Steady Bowmen", "attested": false, "note": "generic Easterling archers" },
  "lokhas_drus_marksman_t2_a": { "name": "Stormbow Marksmen", "attested": false, "note": "TAOM-coined" },
  "lokhas_drus_marksman_t2_b": { "name": "Plate-Piercers", "attested": false, "note": "Lôkhas Drûs = TAOM-coined armour-piercing corps" },
  "lokhas_drus_marksman_t3_a": { "name": "The Arrow-Storm", "attested": false, "note": "elite offensive; coined" },
  "lokhas_drus_marksman_t3_b": { "name": "The Unceasing Hail", "attested": false, "note": "elite sustained; coined" },
  "balchoth_kan_t1_a": { "name": "Balchoth Riders", "attested": true, "note": "Balchoth = attested fierce Easterlings who overran Calenardhon" },
  "balchoth_kan_t1_b": { "name": "Fearless Outriders", "attested": false, "note": "generic Easterling light cavalry" },
  "balchoth_kan_t2_a": { "name": "Wainriders of Rhûn", "attested": true, "note": "Wainriders = attested Easterling chariot/wagon confederation" },
  "balchoth_kan_t2_b": { "name": "Iron Horsemen", "attested": false, "note": "TAOM-flavored heavy cavalry" },
  "balchoth_kan_t3_a": { "name": "The Unstoppable Tide", "attested": false, "note": "echoes the Balchoth horde; phrasing coined" },
  "balchoth_kan_t3_b": { "name": "The Dread Horde", "attested": true, "note": "'Balchoth' canonically means 'cruel/horrible horde'" },

  "dale_guardsman_t1_a": { "name": "Gate Wardens of Dale", "attested": false, "note": "gate defense canonical to the Battle of Dale; coined" },
  "dale_guardsman_t1_b": { "name": "Shieldwall of Esgaroth", "attested": false, "note": "Esgaroth attested; Northman shieldwall" },
  "dale_guardsman_t2_a": { "name": "Spears of Girion", "attested": false, "note": "Girion = last Lord of Dale before Smaug (attested)" },
  "dale_guardsman_t2_b": { "name": "Bulwark of the Running", "attested": false, "note": "River Running/Celduin attested; bulwark coined" },
  "dale_guardsman_t3_a": { "name": "Vanguard of King Brand", "attested": true, "note": "King Brand fell leading Dale at the Battle of Dale (attested)" },
  "dale_guardsman_t3_b": { "name": "The Last Stand at Erebor", "attested": true, "note": "Dale's allies besieged in Erebor until Sauron's fall (attested)" },
  "dale_marksman_t1_a": { "name": "Bowmen of Esgaroth", "attested": true, "note": "Bard was captain of the archers of Esgaroth (attested)" },
  "dale_marksman_t1_b": { "name": "Watch of the Long Lake", "attested": false, "note": "Long Lake attested; watch coined" },
  "dale_marksman_t2_a": { "name": "Heirs of the Black Arrow", "attested": true, "note": "Black Arrow = heirloom that slew Smaug (attested)" },
  "dale_marksman_t2_b": { "name": "Thrush-Sighted Archers", "attested": false, "note": "thrush revealed Smaug's weak spot to Bard (attested); title coined" },
  "dale_marksman_t3_a": { "name": "Dragonslayers of Dale", "attested": true, "note": "Bard slew Smaug, founding the kingly line (attested)" },
  "dale_marksman_t3_b": { "name": "The Bow of Girion's Line", "attested": true, "note": "Bard descended from Girion (attested)" },
  "dale_outrider_t1_a": { "name": "Riders of the Running", "attested": false, "note": "River Running road attested; mounted patrols coined" },
  "dale_outrider_t1_b": { "name": "Wardens of the Caravan Road", "attested": false, "note": "Dale flourished on Erebor trade (attested)" },
  "dale_outrider_t2_a": { "name": "Lances of the Long Valley", "attested": false, "note": "Dale lay in the valley by Erebor; coined" },
  "dale_outrider_t2_b": { "name": "Outriders of Erebor's Friendship", "attested": false, "note": "Bardings' attested friendship with Erebor" },
  "dale_outrider_t3_a": { "name": "The Northman Charge", "attested": false, "note": "Bardings were Northmen, kin to Rohirrim (attested); coined" },
  "dale_outrider_t3_b": { "name": "Couriers of the Two Kings", "attested": true, "note": "Dale & Erebor under Bard II and Thorin III after the siege (attested)" },

  "ironguard_t1_a": { "name": "Hammer of the Deeps", "attested": false, "note": "Erebor's 'deeps' attested; hammer coined" },
  "ironguard_t1_b": { "name": "Gatewall Wardens", "attested": false, "note": "stone wall across Erebor's Front Gate (attested); coined" },
  "ironguard_t2_a": { "name": "Wrath of Durin's Folk", "attested": true, "note": "Durin's Folk = attested name of Erebor's dwarves" },
  "ironguard_t2_b": { "name": "Shieldwall of Erebor", "attested": false, "note": "Erebor attested; shieldwall coined" },
  "ironguard_t3_a": { "name": "Mithril Bulwark", "attested": true, "note": "mithril attested; matches 'Mithril Bastion' ability" },
  "ironguard_t3_b": { "name": "Guard of the Arkenstone", "attested": true, "note": "Arkenstone = attested Heart of the Mountain" },
  "crossbow_master_t1_a": { "name": "Iron Hills Volley", "attested": true, "note": "Iron Hills = attested realm of Dáin" },
  "crossbow_master_t1_b": { "name": "Watchers of Dale", "attested": true, "note": "Dale attested; defended with the dwarves" },
  "crossbow_master_t2_a": { "name": "Relentless Bolts", "attested": false, "note": "coined; constant crossbow pressure" },
  "crossbow_master_t2_b": { "name": "Ballista of the Mountain", "attested": false, "note": "Iron Hills ballistae (adaptation/companion lore)" },
  "crossbow_master_t3_a": { "name": "Storm of Steel", "attested": false, "note": "coined; dwarven steel-craft" },
  "crossbow_master_t3_b": { "name": "Engines of Erebor", "attested": false, "note": "Erebor attested; 'engines' = war-engines" },
  "ram_rider_t1_a": { "name": "Goatherds of the Hills", "attested": false, "note": "Iron Hills war-rams (films/adaptation)" },
  "ram_rider_t1_b": { "name": "Outriders of Erebor", "attested": false, "note": "Erebor attested; outriders coined" },
  "ram_rider_t2_a": { "name": "Charge of the Iron Hills", "attested": true, "note": "Iron Hills = attested source of Dáin's mounted host" },
  "ram_rider_t2_b": { "name": "Stout War-Rams", "attested": false, "note": "'war rams' term for the Iron Hills mounts" },
  "ram_rider_t3_a": { "name": "Ironfoot's Vanguard", "attested": true, "note": "Dáin II Ironfoot = attested King under the Mountain" },
  "ram_rider_t3_b": { "name": "Riders of the Lonely Mountain", "attested": true, "note": "Lonely Mountain (Erebor) attested" },

  "blade_dancer_t1_a": { "name": "Starlit Steel", "attested": false, "note": "First Age Noldor blades + starlight motif (canonical); phrasing coined" },
  "blade_dancer_t1_b": { "name": "Guard of Imladris", "attested": true, "note": "Imladris (Rivendell) attested" },
  "blade_dancer_t2_a": { "name": "Dance of Gondolin", "attested": true, "note": "Gondolin attested; lore passed to Rivendell via Glorfindel" },
  "blade_dancer_t2_b": { "name": "Ward of the Hidden Valley", "attested": true, "note": "'Hidden Valley' = canonical epithet for Imladris" },
  "blade_dancer_t3_a": { "name": "Sword of the First Age", "attested": false, "note": "First Age canonical; career cites First Age blades" },
  "blade_dancer_t3_b": { "name": "Light of Eärendil", "attested": true, "note": "Eärendil's star (Silmaril light) canonical" },
  "elven_archer_t1_a": { "name": "Keen-Eyed Watcher", "attested": false, "note": "from 'Ñoldor Sentinel' + Elven Sight; descriptor" },
  "elven_archer_t1_b": { "name": "Watch of Rivendell", "attested": true, "note": "Rivendell attested" },
  "elven_archer_t2_a": { "name": "Unceasing Volley", "attested": false, "note": "descriptor; constant-pressure keystone" },
  "elven_archer_t2_b": { "name": "Bows of Eregion", "attested": true, "note": "Eregion (Ost-in-Edhil) attested; survivors fled to Rivendell" },
  "elven_archer_t3_a": { "name": "Storm of Gil-galad", "attested": true, "note": "Gil-galad = attested last High King of the Noldor" },
  "elven_archer_t3_b": { "name": "Vigil of Vilya", "attested": true, "note": "Vilya = attested Ring of Air borne by Elrond" },
  "rivendell_knight_t1_a": { "name": "Charge of Imladris", "attested": true, "note": "Imladris attested" },
  "rivendell_knight_t1_b": { "name": "Riders of the Ford", "attested": true, "note": "Ford of Bruinen attested (Glorfindel routed the Nazgûl)" },
  "rivendell_knight_t2_a": { "name": "Hooves of Asfaloth", "attested": true, "note": "Asfaloth = Glorfindel's attested horse" },
  "rivendell_knight_t2_b": { "name": "Aegis of the Valar", "attested": false, "note": "Valar attested (re-embodied Glorfindel); 'aegis' is English coinage" },
  "rivendell_knight_t3_a": { "name": "Wrath of Glorfindel", "attested": true, "note": "Glorfindel = attested Balrog-slayer & Elf-lord of Rivendell" },
  "rivendell_knight_t3_b": { "name": "Host of the Last Alliance", "attested": true, "note": "Last Alliance of Elves and Men (named in the ability)" },

  "warden_t1_a": { "name": "Border Striders", "attested": true, "note": "march-wardens patrolled Lórien's borders/Northern Fences" },
  "warden_t1_b": { "name": "Wardens of Nimrodel", "attested": true, "note": "Nimrodel = attested river & elf-maiden; ties to the ability" },
  "warden_t2_a": { "name": "Blades of the Naith", "attested": false, "note": "Naith = attested spearhead-heart of Lórien; martial title coined" },
  "warden_t2_b": { "name": "Shieldwall of Caras Galadhon", "attested": false, "note": "Caras Galadhon = attested chief city; title coined" },
  "warden_t3_a": { "name": "Wardens of the Golden Wood", "attested": true, "note": "'Golden Wood' = attested epithet for Lothlórien" },
  "warden_t3_b": { "name": "Guard of Cerin Amroth", "attested": false, "note": "Cerin Amroth = attested hallowed mound; guard coined" },
  "galadhrim_archer_t1_a": { "name": "Silver Bowmen", "attested": false, "note": "celeb='silver' (Celebrant) + Silver Rain ability" },
  "galadhrim_archer_t1_b": { "name": "Eyes of the Fences", "attested": false, "note": "Northern Fences watched by hidden marchwardens (attested); title coined" },
  "galadhrim_archer_t2_a": { "name": "Mallorn Marksmen", "attested": false, "note": "mallorn = attested golden trees/flets; unit title coined" },
  "galadhrim_archer_t2_b": { "name": "Watchers of the Flets", "attested": false, "note": "flet/talan = attested mallorn platforms; title coined" },
  "galadhrim_archer_t3_a": { "name": "Arrows of Caras Galadhon", "attested": false, "note": "Caras Galadhon attested; title coined" },
  "galadhrim_archer_t3_b": { "name": "Wardens of the Silverlode", "attested": false, "note": "Silverlode (Celebrant) attested; title coined" },
  "sentinel_t1_a": { "name": "Outriders of Egladil", "attested": false, "note": "Egladil = attested 'Elven-point' of the Naith; title coined" },
  "sentinel_t1_b": { "name": "Wardens of Elanor", "attested": true, "note": "elanor = attested golden flower of Cerin Amroth; ties to ability" },
  "sentinel_t2_a": { "name": "Riders of the Naith", "attested": false, "note": "Naith attested; mounted-warrior title coined" },
  "sentinel_t2_b": { "name": "Guardians of Celebrant", "attested": true, "note": "Celebrant (Silverlode) attested" },
  "sentinel_t3_a": { "name": "Lances of Laurelindórenan", "attested": true, "note": "Laurelindórenan = attested ancient name of Lórien" },
  "sentinel_t3_b": { "name": "Sentinels of Nenya", "attested": true, "note": "Nenya = Galadriel's attested Ring of Water" },

  "shadow_walker_t1_a": { "name": "Shadow of the Path", "attested": false, "note": "hidden forest paths of Mirkwood; coined" },
  "shadow_walker_t1_b": { "name": "Cloak of Leaves", "attested": false, "note": "elven concealment under the canopy; coined" },
  "shadow_walker_t2_a": { "name": "Hunter in the Dark", "attested": false, "note": "Thranduil's folk hunted spiders/orcs; coined" },
  "shadow_walker_t2_b": { "name": "Veil of the Wood", "attested": false, "note": "Greenwood's shadow; coined" },
  "shadow_walker_t3_a": { "name": "Bane of the Spiders", "attested": true, "note": "Great Spiders = attested chief foe of the Wood-elves" },
  "shadow_walker_t3_b": { "name": "Unseen of Eryn Lasgalen", "attested": true, "note": "Eryn Lasgalen = attested 'Wood of Greenleaves' (post-Shadow name)" },
  "silvan_archer_t1_a": { "name": "Greenwood Marksman", "attested": true, "note": "Greenwood the Great = Mirkwood's attested original name" },
  "silvan_archer_t1_b": { "name": "Watch of the Canopy", "attested": false, "note": "treetop strikes; from Canopy Strike" },
  "silvan_archer_t2_a": { "name": "Hunter of the Glades", "attested": false, "note": "Silvan glade-hunters; coined" },
  "silvan_archer_t2_b": { "name": "Wardens of the River", "attested": false, "note": "Forest/Enchanted River crossings (attested); title coined" },
  "silvan_archer_t3_a": { "name": "Storm of Arrows", "attested": false, "note": "matches 'inescapable volleys' keystone" },
  "silvan_archer_t3_b": { "name": "Bowmen of the Elvenking", "attested": true, "note": "archers of Thranduil the Elvenking (attested)" },
  "elk_rider_t1_a": { "name": "Antlered Charge", "attested": true, "note": "Thranduil's great-elk mount + Antler Crash ability" },
  "elk_rider_t1_b": { "name": "Rider of the Reaches", "attested": false, "note": "woodland-reaches patrol; coined" },
  "elk_rider_t2_a": { "name": "Thunder of the Forest", "attested": false, "note": "elk crashing through the wood; coined" },
  "elk_rider_t2_b": { "name": "Guard of Thranduil", "attested": true, "note": "career = warrior of Thranduil's guard (attested king)" },
  "elk_rider_t3_a": { "name": "Wrath under the Trees", "attested": true, "note": "Battle under the Trees (T.A. 3019) attested" },
  "elk_rider_t3_b": { "name": "Lord of the Elvenking's Halls", "attested": true, "note": "Elvenking's Halls = Thranduil's attested stronghold" },

  "uruk_berserker_t1_a": { "name": "Blood-Maddened", "attested": false, "note": "berserker rage of the fighting Uruk-hai; coined" },
  "uruk_berserker_t1_b": { "name": "Pit-Hardened", "attested": false, "note": "Uruk-hai 'bred in the pits of Isengard' (attested)" },
  "uruk_berserker_t2_a": { "name": "White Hand Reavers", "attested": true, "note": "White Hand = Saruman's attested emblem" },
  "uruk_berserker_t2_b": { "name": "Shieldwall of Orthanc", "attested": true, "note": "Orthanc = attested tower of Isengard" },
  "uruk_berserker_t3_a": { "name": "Scourge of Nan Curunír", "attested": true, "note": "Nan Curunír = attested 'Wizard's Vale'" },
  "uruk_berserker_t3_b": { "name": "Unbroken of the Tower", "attested": false, "note": "Tower=Orthanc (attested); 'unbroken' coined" },
  "uruk_crossbow_t1_a": { "name": "Iron Volley", "attested": false, "note": "Isengard's cast-iron crossbows; coined" },
  "uruk_crossbow_t1_b": { "name": "Forge-Forged", "attested": false, "note": "weapons cast in Orthanc's foundries (attested); coined" },
  "uruk_crossbow_t2_a": { "name": "Bolts of the White Hand", "attested": true, "note": "White Hand = Saruman's attested emblem" },
  "uruk_crossbow_t2_b": { "name": "Wardens of the Ring-wall", "attested": true, "note": "Isengard's great ring-wall attested" },
  "uruk_crossbow_t3_a": { "name": "Stormcrows of Orthanc", "attested": false, "note": "'Stormcrow' attested (Gandalf's epithet) re-cast; coined application" },
  "uruk_crossbow_t3_b": { "name": "Iron Rain Unending", "attested": false, "note": "names the in-game 'Iron Rain' ability" },
  "warg_scout_t1_a": { "name": "Fang-Runners", "attested": false, "note": "Wargs as Orc mounts (attested); 'Fang' coined" },
  "warg_scout_t1_b": { "name": "Grey Pack", "attested": true, "note": "Wargs answered a 'grey chief' & hunted in packs (attested)" },
  "warg_scout_t2_a": { "name": "Misty Mountain Hunters", "attested": true, "note": "Wargs dwelt in the Misty Mountains (attested)" },
  "warg_scout_t2_b": { "name": "Wolf-Wardens", "attested": false, "note": "'warg' < Old Norse vargr 'wolf' (attested etymology); coined" },
  "warg_scout_t3_a": { "name": "Outriders of Isengard", "attested": true, "note": "Saruman gathered Wargs at Isengard (attested); 'outriders' coined" },
  "warg_scout_t3_b": { "name": "The Tireless Hunt", "attested": false, "note": "endless-pursuit tier; coined" },

  "cave_troll_master_t1_a": { "name": "Pit-Whip Driver", "attested": false, "note": "Gundabad chieftains drove trolls/beasts; coined" },
  "cave_troll_master_t1_b": { "name": "Cave-Mouth Warden", "attested": false, "note": "goblins held the deep cave-mouths; coined" },
  "cave_troll_master_t2_a": { "name": "Goblin-town Tyrant", "attested": true, "note": "Goblin-town under the High Pass attested (The Hobbit)" },
  "cave_troll_master_t2_b": { "name": "Deep-Hold Breaker", "attested": false, "note": "orcs seized the Dwarves' deep holds; coined" },
  "cave_troll_master_t3_a": { "name": "Scourge of Gundabad", "attested": true, "note": "Mount Gundabad = attested orc stronghold" },
  "cave_troll_master_t3_b": { "name": "Dread of the Northern Halls", "attested": false, "note": "seized northern Dwarf-halls; coined" },
  "goblin_sniper_t1_a": { "name": "Crag-Stalker", "attested": false, "note": "goblins lurked in the crags near the High Pass; coined" },
  "goblin_sniper_t1_b": { "name": "Tunnel Skulker", "attested": false, "note": "Goblin-town tunnels (attested); coined" },
  "goblin_sniper_t2_a": { "name": "High Pass Ambusher", "attested": true, "note": "High Pass goblin ambushes attested (The Hobbit)" },
  "goblin_sniper_t2_b": { "name": "Poison-Fletcher", "attested": false, "note": "career's poisoned-dart theme; Tolkien doesn't attest goblin poison" },
  "goblin_sniper_t3_a": { "name": "Front Porch Reaver", "attested": true, "note": "'Front Porch' = attested Goblin-town gate at the High Pass" },
  "goblin_sniper_t3_b": { "name": "Shadow of the High Pass", "attested": false, "note": "High Pass attested; 'Shadow' coined" },
  "warg_pack_leader_t1_a": { "name": "Warg-Rider", "attested": true, "note": "warg-riders of Gundabad attested" },
  "warg_pack_leader_t1_b": { "name": "Pack-Keeper", "attested": false, "note": "Gundabad orcs bred Wargs (attested); coined" },
  "warg_pack_leader_t2_a": { "name": "Grey Pack Outrider", "attested": true, "note": "'great grey chief wolf' & pack attested (The Hobbit)" },
  "warg_pack_leader_t2_b": { "name": "Fell Wolf-Master", "attested": false, "note": "Wargs = 'fell' monstrous wolves (attested); coined" },
  "warg_pack_leader_t3_a": { "name": "Alpha of Gundabad", "attested": false, "note": "Gundabad attested; matches Alpha Howl ability" },
  "warg_pack_leader_t3_b": { "name": "Bolg's Wolf-Captain", "attested": true, "note": "Bolg led Wargs at the Battle of Five Armies (attested)" },

  "shadow_warrior_t1_a": { "name": "Shade of Amon Lanc", "attested": false, "note": "Amon Lanc = attested hill on which Dol Guldur was built" },
  "shadow_warrior_t1_b": { "name": "Warden of the Pits", "attested": false, "note": "Dol Guldur's pits/dungeons attested" },
  "shadow_warrior_t2_a": { "name": "Reaver of Mirkwood", "attested": false, "note": "Mirkwood attested; 'reaver' coined" },
  "shadow_warrior_t2_b": { "name": "Bulwark of Dol Guldur", "attested": false, "note": "Dol Guldur = attested 'Hill of Sorcery'; bulwark coined" },
  "shadow_warrior_t3_a": { "name": "Scourge of the Necromancer", "attested": false, "note": "Necromancer (Sauron at Dol Guldur) attested; title coined" },
  "shadow_warrior_t3_b": { "name": "Deathless Shadow", "attested": false, "note": "Nazgûl wraith-existence attested theme; coined" },
  "necromancer_acolyte_t1_a": { "name": "Caster of Dark Bolts", "attested": false, "note": "names the 'Dark Bolt' ability" },
  "necromancer_acolyte_t1_b": { "name": "Whisperer of Decay", "attested": false, "note": "decay/shadow over Mirkwood attested theme; coined" },
  "necromancer_acolyte_t2_a": { "name": "Weaver of Black Arts", "attested": false, "note": "'Guldur' = 'black arts/sorcery' (attested Sindarin)" },
  "necromancer_acolyte_t2_b": { "name": "Breather of Shadow", "attested": false, "note": "evokes the attested Black Breath; coined" },
  "necromancer_acolyte_t3_a": { "name": "Lord of the Hill of Sorcery", "attested": false, "note": "'Hill of Sorcery' = attested meaning of Dol Guldur" },
  "necromancer_acolyte_t3_b": { "name": "Voice of the Necromancer", "attested": false, "note": "Necromancer attested; title coined" },
  "fell_rider_t1_a": { "name": "Rider of the Shadow Host", "attested": false, "note": "from career's 'shadow host'; Mirkwood shadow attested" },
  "fell_rider_t1_b": { "name": "Keeper of Fell Steeds", "attested": false, "note": "fell beasts near Dol Guldur as Nazgûl steeds (attested); coined" },
  "fell_rider_t2_a": { "name": "Herald of Dread", "attested": false, "note": "Nazgûl spread attested fear/dread; coined" },
  "fell_rider_t2_b": { "name": "Rider of the Black Breath", "attested": false, "note": "Black Breath = attested Nazgûl weapon; title coined" },
  "fell_rider_t3_a": { "name": "Black Rider of Dol Guldur", "attested": false, "note": "'Black Riders' = attested Nazgûl name; Dol Guldur garrison attested" },
  "fell_rider_t3_b": { "name": "Shadow of the East", "attested": true, "note": "'Shadow of the East' = Khamûl's attested title, lord of Dol Guldur" },

  "corsair_boarder_t1_a": { "name": "Deck Reaver", "attested": false, "note": "corsair boarders stormed decks; coined" },
  "corsair_boarder_t1_b": { "name": "Sea-hardened", "attested": false, "note": "toughened raiding crews; coined" },
  "corsair_boarder_t2_a": { "name": "Ravager of Pelargir", "attested": true, "note": "Corsairs sacked Pelargir (T.A. 1634), slaying King Minardil" },
  "corsair_boarder_t2_b": { "name": "Bulwark of the Haven", "attested": false, "note": "Haven of Umbar attested; bulwark coined" },
  "corsair_boarder_t3_a": { "name": "Scourge of the Coasts", "attested": true, "note": "Corsairs ravaged Gondor's coasts for centuries (Corsair Wars)" },
  "corsair_boarder_t3_b": { "name": "Black Númenórean Warlord", "attested": true, "note": "Umbar = realm of the Black Númenóreans (attested)" },
  "corsair_crossbow_t1_a": { "name": "Rigging Marksman", "attested": false, "note": "career: shooters in the rigging; coined" },
  "corsair_crossbow_t1_b": { "name": "Crow's-nest Watch", "attested": false, "note": "lookout/fire perch; coined nautical term" },
  "corsair_crossbow_t2_a": { "name": "Hunter of Helmsmen", "attested": false, "note": "career: pick off helmsmen/officers; coined" },
  "corsair_crossbow_t2_b": { "name": "Pikes of the Black Sails", "attested": false, "note": "Corsairs' attested black sails; phrase coined" },
  "corsair_crossbow_t3_a": { "name": "Quarrel of the Black Star", "attested": false, "note": "Corsair red sails bore an attested black star/eye; phrasing coined" },
  "corsair_crossbow_t3_b": { "name": "Unceasing Volley", "attested": false, "note": "relentless-fire tier; coined" },
  "corsair_captain_t1_a": { "name": "Raid-leader of Umbar", "attested": true, "note": "corsair captains led raiding fleets from Umbar (attested)" },
  "corsair_captain_t1_b": { "name": "Master of the Crew", "attested": false, "note": "captains commanded crews/rowers (attested role); title coined" },
  "corsair_captain_t2_a": { "name": "Heir of Castamir", "attested": true, "note": "Corsairs led by descendants of Castamir the Usurper (attested)" },
  "corsair_captain_t2_b": { "name": "Helm of the Black Fleet", "attested": false, "note": "Black Fleet attested (captured by Aragorn); 'helm' coined" },
  "corsair_captain_t3_a": { "name": "Lord of the Corsairs", "attested": true, "note": "sons of Castamir made Umbar a corsair lordship (attested)" },
  "corsair_captain_t3_b": { "name": "Lord of the Great Haven", "attested": false, "note": "Umbar's Haven/great cape attested; title coined" }
}


 succeeded in 511ms:
{
  "_meta": {
    "purpose": "Reviewed web-researched per-tier RANK titles for careers (tier headers on the career screen). Adopted from TOR_Core's tor_career_rank{1,2,3}_name convention, authored clean for TAOM/LOTR.",
    "format": "<careerId>: { rank1:{name,attested}, rank2:{...}, rank3:{...} }. attested=true = canonical Tolkien term/title.",
    "applied_by": "tools/apply_career_rank_names.py (writes rank{1,2,3}_name=\"{=taom_career_rank{N}_<id>}<name>\" onto each <Career>)",
    "count": 147
  },

  "ranger_of_ithilien": { "rank1": {"name":"Ithilien Scout","attested":false}, "rank2": {"name":"Ranger of Ithilien","attested":true}, "rank3": {"name":"Captain of the White Company","attested":true} },
  "captain_of_osgiliath": { "rank1": {"name":"Ohtar of the Crossing","attested":false}, "rank2": {"name":"Warden of Osgiliath","attested":false}, "rank3": {"name":"Captain of Osgiliath","attested":true} },
  "knight_of_belfalas": { "rank1": {"name":"Squire of Dol Amroth","attested":false}, "rank2": {"name":"Swan-Knight","attested":true}, "rank3": {"name":"Knight of the Silver Swan","attested":false} },

  "black_uruk_captain": { "rank1": {"name":"Uruk Soldier","attested":true}, "rank2": {"name":"Black Uruk","attested":true}, "rank3": {"name":"Captain of the Red Eye","attested":false} },
  "mulkerhili_cultist": { "rank1": {"name":"Whisper-Sworn","attested":false}, "rank2": {"name":"Voice of Lugbúrz","attested":false}, "rank3": {"name":"Herald of the Lidless Eye","attested":false} },
  "snaga_rider": { "rank1": {"name":"Snaga","attested":true}, "rank2": {"name":"Smoke-Raider","attested":false}, "rank3": {"name":"Shadow of Lugbúrz","attested":false} },
  "olog_hai_warchief": { "rank1": {"name":"War-Troll","attested":true}, "rank2": {"name":"Olog-hai","attested":true}, "rank3": {"name":"Warchief of Lugbúrz","attested":false} },

  "marksman_of_aldburg": { "rank1": {"name":"Bowman of the Eastfold","attested":false}, "rank2": {"name":"Marksman of Aldburg","attested":false}, "rank3": {"name":"Arrow-warden of the Mark","attested":false} },
  "eotheod_windrider": { "rank1": {"name":"Rider of the Mark","attested":true}, "rank2": {"name":"Captain of the Éored","attested":true}, "rank3": {"name":"Marshal of the Riddermark","attested":true} },
  "watchman_of_stangard": { "rank1": {"name":"Watchman of Stangard","attested":false}, "rank2": {"name":"Ford-warden of the Limlight","attested":false}, "rank3": {"name":"Warden of the East-marches","attested":false} },

  "avanc_luth_raider": { "rank1": {"name":"Painted Reaver","attested":false}, "rank2": {"name":"Clan Berserker","attested":false}, "rank3": {"name":"Avanc-lúth War-Chief","attested":false} },
  "wolfskin_hunter": { "rank1": {"name":"Wolf-Pelt Stalker","attested":false}, "rank2": {"name":"Shadow-Marksman","attested":false}, "rank3": {"name":"Grey Huntsman of the Hills","attested":false} },
  "clanguard_rider": { "rank1": {"name":"Clan Outrider","attested":false}, "rank2": {"name":"Boar-Banner Horseman","attested":false}, "rank3": {"name":"Master of the Wild Hunt","attested":false} },

  "blademaster_of_ren": { "rank1": {"name":"Blade of Rên","attested":false}, "rank2": {"name":"Sworn Blademaster","attested":false}, "rank3": {"name":"Kân of the Crimson Edge","attested":false} },
  "steppe_bowmaster": { "rank1": {"name":"Plains Outrider","attested":false}, "rank2": {"name":"Steppe Bowmaster","attested":false}, "rank3": {"name":"Stormbow Kân","attested":false} },
  "chariot_warlord": { "rank1": {"name":"Scythe-Driver","attested":false}, "rank2": {"name":"Chariot Warlord","attested":false}, "rank3": {"name":"Wainkân of Khand","attested":false} },

  "tribesman_of_jelut": { "rank1": {"name":"Jelut Tribesman","attested":false}, "rank2": {"name":"Sandstorm Raider","attested":false}, "rank3": {"name":"Serpent-Banner Chieftain","attested":true} },
  "pezarsani_javelineer": { "rank1": {"name":"Pezarsan Javelineer","attested":false}, "rank2": {"name":"Throwing-Spear Skirmisher","attested":false}, "rank3": {"name":"Scarlet Spear-Master","attested":false} },
  "mahud_beast_rider": { "rank1": {"name":"Mahûd Beast-Tamer","attested":false}, "rank2": {"name":"Mûmak Driver","attested":false}, "rank3": {"name":"Mûmak War-Leader","attested":false} },

  "codyan_legionaire": { "rank1": {"name":"Côdyan Shieldman","attested":false}, "rank2": {"name":"Côdyan Lamellar Veteran","attested":false}, "rank3": {"name":"Ironwall of Rhûn","attested":false} },
  "lokhas_drus_marksman": { "rank1": {"name":"Lôkhas Drûs Bowman","attested":false}, "rank2": {"name":"Lôkhas Drûs Plate-Piercer","attested":false}, "rank3": {"name":"Sunfletched Death of the East","attested":false} },
  "balchoth_kan": { "rank1": {"name":"Balchoth Horseman","attested":true}, "rank2": {"name":"Balchoth Charioteer-Captain","attested":false}, "rank3": {"name":"Balchoth Warlord-Kân","attested":false} },

  "dale_guardsman": { "rank1": {"name":"Gate-Warden of Dale","attested":false}, "rank2": {"name":"Shieldman of Girion","attested":false}, "rank3": {"name":"Knight of Dale","attested":false} },
  "dale_marksman": { "rank1": {"name":"Bowman of Esgaroth","attested":true}, "rank2": {"name":"Dragon-Shooter","attested":true}, "rank3": {"name":"Bearer of the Black Arrow","attested":true} },
  "dale_outrider": { "rank1": {"name":"Road-Rider of Dale","attested":false}, "rank2": {"name":"Warden of the North-Road","attested":false}, "rank3": {"name":"Marshal of the Bardings","attested":false} },

  "ironguard": { "rank1": {"name":"Deep Warden","attested":false}, "rank2": {"name":"Ironguard of Erebor","attested":false}, "rank3": {"name":"Shieldwall of Durin","attested":false} },
  "crossbow_master": { "rank1": {"name":"Bolt-Smith","attested":false}, "rank2": {"name":"Master Quarreler","attested":false}, "rank3": {"name":"Engine-Lord of the Iron Hills","attested":false} },
  "ram_rider": { "rank1": {"name":"Ram-Breaker","attested":false}, "rank2": {"name":"Goatback Charger","attested":false}, "rank3": {"name":"Vanguard of Dáin","attested":false} },

  "blade_dancer": { "rank1": {"name":"Ñoldor Bladesman","attested":false}, "rank2": {"name":"Blade of the Golden Flower","attested":false}, "rank3": {"name":"Blademaster of Imladris","attested":false} },
  "elven_archer": { "rank1": {"name":"Ñoldor Bowman","attested":false}, "rank2": {"name":"Sentinel of the Heavenly Arch","attested":false}, "rank3": {"name":"Warden of Imladris","attested":false} },
  "rivendell_knight": { "rank1": {"name":"Ohtar of Imladris","attested":true}, "rank2": {"name":"Roquen of Imladris","attested":true}, "rank3": {"name":"Knight of the Golden Flower","attested":false} },

  "warden": { "rank1": {"name":"Warden of the Eaves","attested":false}, "rank2": {"name":"Marchwarden of Lórien","attested":true}, "rank3": {"name":"Captain of the Marchwardens","attested":false} },
  "galadhrim_archer": { "rank1": {"name":"Bowman of the Galadhrim","attested":false}, "rank2": {"name":"Archer of the Mallorn-Eaves","attested":false}, "rank3": {"name":"Bowmaster of the Golden Wood","attested":false} },
  "sentinel": { "rank1": {"name":"Rider of the Egladil","attested":false}, "rank2": {"name":"Tirith of the Golden Wood","attested":false}, "rank3": {"name":"Thalion of Caras Galadhon","attested":false} },

  "shadow_walker": { "rank1": {"name":"Forest Stalker","attested":false}, "rank2": {"name":"Shadow of Taur-nu-Fuin","attested":false}, "rank3": {"name":"Bane of Spiders","attested":false} },
  "silvan_archer": { "rank1": {"name":"Greenwood Bowman","attested":false}, "rank2": {"name":"Silvan Marksman","attested":false}, "rank3": {"name":"Archer of the Elvenking","attested":false} },
  "elk_rider": { "rank1": {"name":"Woodland Outrider","attested":false}, "rank2": {"name":"Elk-Warden of Mirkwood","attested":false}, "rank3": {"name":"Antlered Lord of Eryn Lasgalen","attested":false} },

  "uruk_berserker": { "rank1": {"name":"Pit-Spawned Uruk","attested":false}, "rank2": {"name":"Fighting Uruk-hai","attested":true}, "rank3": {"name":"White Hand Reaver","attested":false} },
  "uruk_crossbow": { "rank1": {"name":"Forge-Armed Uruk","attested":false}, "rank2": {"name":"Orthanc Marksman","attested":false}, "rank3": {"name":"White Hand Sharpshooter","attested":false} },
  "warg_scout": { "rank1": {"name":"Wolf-Rider","attested":true}, "rank2": {"name":"Warg-Scout of Isengard","attested":true}, "rank3": {"name":"Hunter of the White Hand","attested":false} },

  "cave_troll_master": { "rank1": {"name":"Pit-Driver","attested":false}, "rank2": {"name":"Troll-Goad of Gundabad","attested":false}, "rank3": {"name":"Maker of Broken Trolls","attested":false} },
  "goblin_sniper": { "rank1": {"name":"Crag-Lurker","attested":false}, "rank2": {"name":"High Pass Stalker","attested":false}, "rank3": {"name":"Eye of Gundabad","attested":false} },
  "warg_pack_leader": { "rank1": {"name":"Warg-Rider of the Pack","attested":false}, "rank2": {"name":"Fell Wolf-Captain","attested":false}, "rank3": {"name":"Wolf-Lord of the Wild Hunt","attested":false} },

  "shadow_warrior": { "rank1": {"name":"Thrall of the Shadow","attested":false}, "rank2": {"name":"Servant of Dol Guldur","attested":true}, "rank3": {"name":"Black Hand of the Necromancer","attested":false} },
  "necromancer_acolyte": { "rank1": {"name":"Acolyte of Sorcery","attested":false}, "rank2": {"name":"Weaver of the Black Breath","attested":true}, "rank3": {"name":"Master of Dol Guldur","attested":true} },
  "fell_rider": { "rank1": {"name":"Black Rider","attested":true}, "rank2": {"name":"Fell Rider","attested":true}, "rank3": {"name":"Shadow of the East","attested":true} },

  "corsair_boarder": { "rank1": {"name":"Deck Reaver","attested":false}, "rank2": {"name":"Corsair of the Haven","attested":true}, "rank3": {"name":"Black Reaver of Umbar","attested":false} },
  "corsair_crossbow": { "rank1": {"name":"Rigging Marksman","attested":false}, "rank2": {"name":"Corsair Sharpshooter","attested":false}, "rank3": {"name":"Throng-cleaver of the Fleet","attested":true} },
  "corsair_captain": { "rank1": {"name":"Master of the Black Fleet","attested":false}, "rank2": {"name":"Captain of Ships","attested":true}, "rank3": {"name":"Lord of Umbar","attested":true} }
}


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main','TAOM.Tests' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerDefinition\\(','new CareerChoiceGroupDefinition\\(' -Context 0,12 | Select-Object Path,LineNumber,Line,Context | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 644ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m132
[32;1mLine       : [0m                    var career = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m201
[32;1mLine       : [0m                    var group = new CareerChoiceGroupDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerAbilityServiceTests.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m    private static readonly CareerDefinition Career = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m35
[32;1mLine       : [0m    private static readonly CareerDefinition RangerCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCreationHandlerTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerPassiveServiceTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m22
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m30
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m131
[32;1mLine       : [0m        var tier2Group = new CareerChoiceGroupDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m156
[32;1mLine       : [0m        var dominionGroup = new CareerChoiceGroupDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m        var expandedCareer = new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m46
[32;1mLine       : [0m        _registry.GetCareer("ranger").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m51
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m69
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m93
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m117
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CharacterCreation\CareerMenuServiceTests.cs
[32;1mLineNumber : [0m222
[32;1mLine       : [0m        return new CareerDefinition(
[32;1mContext    : [0mMicrosoft.PowerShell.Commands.MatchInfoContext


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main','TAOM.Tests' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerDefinition\\(\\s*"'$'"','new CareerChoiceGroupDefinition\\(\\s*"'$'"' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main','TAOM.Tests' -Recurse -Filter '*.cs' | Select-String -Pattern 'new CareerDefinition\\(\\s*\"','new CareerChoiceGroupDefinition\\(\\s*\"' | Select-Object Path,LineNumber,Line | Format-List" in C:\Users\mikew\source\repos\TAOM
 succeeded in 720ms:

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m132
[32;1mLine       : [0m                    var career = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs
[32;1mLineNumber : [0m201
[32;1mLine       : [0m                    var group = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerAbilityServiceTests.cs
[32;1mLineNumber : [0m17
[32;1mLine       : [0m    private static readonly CareerDefinition Career = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCampaignBehaviorTests.cs
[32;1mLineNumber : [0m35
[32;1mLine       : [0m    private static readonly CareerDefinition RangerCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerCreationHandlerTests.cs
[32;1mLineNumber : [0m28
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerPassiveServiceTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m18
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerRegistryTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m22
[32;1mLine       : [0m    private static readonly CareerDefinition WarbossCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m30
[32;1mLine       : [0m    private static readonly CareerChoiceGroupDefinition BrutalityGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m131
[32;1mLine       : [0m        var tier2Group = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m156
[32;1mLine       : [0m        var dominionGroup = new CareerChoiceGroupDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerScreenVMTests.cs
[32;1mLineNumber : [0m164
[32;1mLine       : [0m        var expandedCareer = new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m39
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\CareerSwitchServiceTests.cs
[32;1mLineNumber : [0m46
[32;1mLine       : [0m        _registry.GetCareer("ranger").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m51
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m69
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m93
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CareerSystem\MutationServiceTests.cs
[32;1mLineNumber : [0m117
[32;1mLine       : [0m        _registry.GetCareer("warboss").Returns(new CareerDefinition(

[32;1mPath       : [0mC:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CharacterCreation\CareerMenuServiceTests.cs
[32;1mLineNumber : [0m222
[32;1mLine       : [0m        return new CareerDefinition(


 succeeded in 1113ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$files = Get-ChildItem -Path '"'Main','TAOM.Tests' -Recurse -Filter '*.cs'
foreach ("'$file in $files) {
  $lines = Get-Content $file.FullName
  for ($i=0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '"'new Career(Definition|ChoiceGroupDefinition)\\(') {
      "'$snippet = ($lines[$i..([Math]::Min($i+4,$lines.Count-1))] -join '"' ')
      "'$hasNamed = $snippet -match '"'\\bid\\s*:'
      if (-not "'$hasNamed) { "$($file.FullName):$($i+1): $snippet" }
    }
  }
}
'"'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 798ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$stringsPath = '"'Main/_Module/ModuleData/taom_module_strings.xml'
[xml]"'$strings = Get-Content -Raw $stringsPath
$mismatch=@()
$nodes = @($strings.SelectNodes('"'//*[@id]') | Where-Object { "'$_.id -match '"'"'^(taom_career_grp_|taom_career_rank[123]_|taom_career_tier_requirement$)'"' })
foreach ("'$n in $nodes) {
  $text = $n.text
  if ($text -match '"'"'^'"\\{=(["'^}]+)'"\\}') {
    if ("'$matches[1] -ne $n.id) { $mismatch += "$($n.id) textToken=$($matches[1])" }
  } elseif ($n.id -ne '"'taom_career_tier_requirement') {
    "'$mismatch += "$($n.id) has no text token"
  }
}
"career-prefixed string nodes=$($nodes.Count)"
"id/text-token mismatches=$($mismatch.Count)"
$mismatch | Select-Object -First 20
'"'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 720ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
"'$ErrorActionPreference='"'Stop'
[xml]"'$strings = Get-Content -Raw '"'Main/_Module/ModuleData/taom_module_strings.xml'
"'$all = @($strings.SelectNodes('"'//*[@id]'))
\"all ids="'$($all.Count)"
$nodes = @($all | Where-Object { $_.id -match '"'"'^(taom_career_grp_|taom_career_rank[123]_|taom_career_tier_requirement$)'"' })
"'$mismatch=@()
foreach ($n in $nodes) {
  $text = $n.GetAttribute('"'text')
  if ("'$text -match '"'"'^'"\\{=(["'^}]+)'"\\}') {
    if ("'$matches[1] -ne $n.id) { $mismatch += "$($n.id) textToken=$($matches[1])" }
  } elseif ($n.id -ne '"'taom_career_tier_requirement') {
    "'$mismatch += "$($n.id) has no text token"
  }
}
"career-prefixed string nodes=$($nodes.Count)"
"id/text-token mismatches=$($mismatch.Count)"
$mismatch | Select-Object -First 20
'"'@ | powershell -NoProfile -Command -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 763ms:
all ids=1230

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "@'
import re
import xml.etree.ElementTree as ET
p='Main/_Module/ModuleData/taom_module_strings.xml'
root=ET.parse(p).getroot()
nodes=[]
for e in root.iter():
    idv=e.attrib.get('id')
    if idv and re.match(r'"'^(taom_career_grp_|taom_career_rank[123]_|taom_career_tier_requirement$)'"', idv):
        nodes.append(e)
mis=[]
for e in nodes:
    idv=e.attrib['id']; text=e.attrib.get('text','')
    m=re.match(r'"'^'"\\{=(["'^}]+)'"\\}', text)
    if m:
        if m.group(1)"'!=idv: mis.append((idv,m.group(1)))
    elif idv!='"'taom_career_tier_requirement':
        mis.append((idv,'NO_TOKEN'))
print('career-prefixed string nodes=', len(nodes))
print('id/text-token mismatches=', len(mis))
for x in mis[:20]: print(x)
'@ | python -" in C:\Users\mikew\source\repos\TAOM
 succeeded in 547ms:
career-prefixed string nodes= 442
id/text-token mismatches= 0

codex
**SPRITE ATLAS**
Verdict on Suspect 1: **DISPUTED**.

Inspected installed v1.4.5 DLL with `ilspycmd`:
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.TwoDimension.dll`

Relevant runtime loader evidence:
```csharp
array[num2 - 1] = new Vec2i(x, y);
SheetSizes = array
```
```csharp
for (int i = 1; i <= SpriteSheetCount; i++)
    LoadTexture(... Name + "_" + i)
```
```csharp
_category.SheetSizes[SheetID - 1]
_category.SpriteSheets[SheetID - 1]
```

This means `SheetID` is scoped through the sprite part's `CategoryName`, and each sheet has its own `Vec2i` size. I found no v1.4.5 C# runtime requirement that every sheet in a category must share the same dimensions. TAOM's manifest has `ui_taom_career_system` sheet 1 as `4096x4096`, sheet 2 as `256x256`, and `CareerSystem\career_point_pip` on sheet 2 at `0,0` with `256x256`, which fits exactly.

**BINDING TABLE**
All unique prefab bindings are connected. Removed gate bindings are absent.

| Binding | Scope | Backing member | Status |
|---|---|---|---|
| `@ScreenTitle` | `CareerScreenVM` | property | CONNECTED |
| `@CareerName` | `CareerScreenVM` | property | CONNECTED |
| `@CareerPortraitSprite` | `CareerScreenVM` | property | CONNECTED |
| `@HasCareer` | `CareerScreenVM` | property | CONNECTED |
| `@CareerDescription` | `CareerScreenVM` | property | CONNECTED |
| `@AbilityLabel` | `CareerScreenVM` | property | CONNECTED |
| `@AbilitySpriteName` | `CareerScreenVM` | property | CONNECTED |
| `@HasAbilitySprite` | `CareerScreenVM` | property | CONNECTED |
| `@AbilityName` | `CareerScreenVM` | property | CONNECTED |
| `{AbilityEffects}` | `CareerScreenVM` | `MBBindingList<CareerAbilityEffectVM>` | CONNECTED |
| `@LineText` | `CareerAbilityEffectVM` | property | CONNECTED |
| `@Tier1Label` | `CareerScreenVM` | property | CONNECTED |
| `@Tier2Label` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3Label` | `CareerScreenVM` | property | CONNECTED |
| `{ChoiceGroupsTier1}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `{ChoiceGroupsTier2}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `{ChoiceGroupsTier3}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `@GroupName` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `{Choices}` | `CareerChoiceGroupObjectVM` | `MBBindingList<CareerChoiceObjectVM>` | CONNECTED |
| `@IsTaken` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsFreeToTake` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsUnavailable` | `CareerChoiceObjectVM` | computed property | CONNECTED |
| `@ButtonsVisible` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `@Description` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsActive` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `Command.HoverBegin=ExecuteBeginHover` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.HoverEnd=ExecuteEndHover` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.Click=ExecuteClickIncrease` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.Click=ExecuteClickDecrease` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `@Tier2RequirementText` | `CareerScreenVM` | property | CONNECTED |
| `@Tier2Locked` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3RequirementText` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3Locked` | `CareerScreenVM` | property | CONNECTED |
| `@FreeCareerPointsText` | `CareerScreenVM` | property | CONNECTED |
| `Parameter.DoneButtonAction=ExecuteClose` | `CareerScreenVM` | public method | CONNECTED |
| `@DoneLbl` | `CareerScreenVM` | property | CONNECTED |

No references remain to `@Tier2GateBottomHalf`, `@Tier3GateTopHalf`, `@Tier3GateFull`, or `@Tier1Locked`.

**LOC CONSISTENCY**
DISPUTED as a bug.

Checks performed:
- `taom_career_choices.xml`, `taom_careers.xml`, and `taom_module_strings.xml` all parse as well-formed XML.
- Active `ChoiceGroup display_name` attrs: `288`.
- Raw `ChoiceGroup display_name` attrs: `294`.
- Difference: `6`, all inside the pre-existing `cave_troll_master` disabled comment block.
- Careers with rank attrs: `49`, rank attrs total: `147`.
- Active referenced group/rank localization keys: `435`.
- Missing referenced keys: `0`.
- Duplicate `<string id>` entries in `taom_module_strings.xml`: `0`.
- Career-prefixed string nodes including disabled cave-troll group keys plus `taom_career_tier_requirement`: `442`.
- ID/text-token mismatches: `0`.

Spot checks passed for `ranger_of_ithilien_t1_a`, `captain_of_osgiliath_t1_b`, `ranger_of_ithilien` rank 1/2/3, and `eotheod_windrider` rank 1/2/3.

**KNOWN SUSPECTS**
1. SPRITE-ATLAS SHEET-2 EDIT: **DISPUTED**. Runtime loader supports per-category sheet indexes and per-sheet sizes.
2. VM<->PREFAB BINDING COMPLETENESS: **DISPUTED**. All unique bindings connect; removed gate props are absent.
3. LOCALIZATION KEY CONSISTENCY: **DISPUTED**. XML well-formed, no duplicate string IDs, no missing active keys.
4. ISTIERAVAILABLE REFACTOR EQUIVALENCE: **DISPUTED for game-real inputs**. It differs only for `heroLevel <= 0` tier 1. Vanilla `CharacterObject.Init()` sets `Level = 1`, and `Hero.SetInitialValuesFromCharacter()` copies `Level = characterObject.Level`. `GauntletCareerScreen.OpenCareerScreen()` also returns early if `Hero.MainHero` is null. So real heroes should not hit the changed case.
5. OPTIONAL-PARAM CTOR BACK-COMPAT: **DISPUTED**. New params are trailing optionals; call sites use named args; parser passes `rank1_name`, `rank2_name`, `rank3_name`, and `display_name`.
6. IsUnavailable NOTIFICATION: **DISPUTED**. `IsTaken` and `IsFreeToTake` both notify `IsUnavailable`; v1.4.5 `ViewModel` has public `OnPropertyChanged(string)`. Screen refresh rebuilds the VM list after selection changes.
7. COMMENTED-BLOCK INJECTION: **DISPUTED**. The 6 cave-troll `ChoiceGroup display_name` attrs are inert inside the XML comment; active XML node count confirms they are not parsed.

**FINDINGS**
No confirmed bugs found.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 0  
VERDICT: CLEAN
SUCCESS: The process with PID 22276 (child process of PID 47832) has been terminated.
SUCCESS: The process with PID 47832 (child process of PID 57556) has been terminated.
SUCCESS: The process with PID 57556 (child process of PID 66884) has been terminated.
SUCCESS: The process with PID 66884 (child process of PID 51940) has been terminated.
tokens used
226,266
**SPRITE ATLAS**
Verdict on Suspect 1: **DISPUTED**.

Inspected installed v1.4.5 DLL with `ilspycmd`:
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.TwoDimension.dll`

Relevant runtime loader evidence:
```csharp
array[num2 - 1] = new Vec2i(x, y);
SheetSizes = array
```
```csharp
for (int i = 1; i <= SpriteSheetCount; i++)
    LoadTexture(... Name + "_" + i)
```
```csharp
_category.SheetSizes[SheetID - 1]
_category.SpriteSheets[SheetID - 1]
```

This means `SheetID` is scoped through the sprite part's `CategoryName`, and each sheet has its own `Vec2i` size. I found no v1.4.5 C# runtime requirement that every sheet in a category must share the same dimensions. TAOM's manifest has `ui_taom_career_system` sheet 1 as `4096x4096`, sheet 2 as `256x256`, and `CareerSystem\career_point_pip` on sheet 2 at `0,0` with `256x256`, which fits exactly.

**BINDING TABLE**
All unique prefab bindings are connected. Removed gate bindings are absent.

| Binding | Scope | Backing member | Status |
|---|---|---|---|
| `@ScreenTitle` | `CareerScreenVM` | property | CONNECTED |
| `@CareerName` | `CareerScreenVM` | property | CONNECTED |
| `@CareerPortraitSprite` | `CareerScreenVM` | property | CONNECTED |
| `@HasCareer` | `CareerScreenVM` | property | CONNECTED |
| `@CareerDescription` | `CareerScreenVM` | property | CONNECTED |
| `@AbilityLabel` | `CareerScreenVM` | property | CONNECTED |
| `@AbilitySpriteName` | `CareerScreenVM` | property | CONNECTED |
| `@HasAbilitySprite` | `CareerScreenVM` | property | CONNECTED |
| `@AbilityName` | `CareerScreenVM` | property | CONNECTED |
| `{AbilityEffects}` | `CareerScreenVM` | `MBBindingList<CareerAbilityEffectVM>` | CONNECTED |
| `@LineText` | `CareerAbilityEffectVM` | property | CONNECTED |
| `@Tier1Label` | `CareerScreenVM` | property | CONNECTED |
| `@Tier2Label` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3Label` | `CareerScreenVM` | property | CONNECTED |
| `{ChoiceGroupsTier1}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `{ChoiceGroupsTier2}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `{ChoiceGroupsTier3}` | `CareerScreenVM` | `MBBindingList<CareerChoiceGroupObjectVM>` | CONNECTED |
| `@GroupName` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `{Choices}` | `CareerChoiceGroupObjectVM` | `MBBindingList<CareerChoiceObjectVM>` | CONNECTED |
| `@IsTaken` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsFreeToTake` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsUnavailable` | `CareerChoiceObjectVM` | computed property | CONNECTED |
| `@ButtonsVisible` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `@Description` | `CareerChoiceObjectVM` | property | CONNECTED |
| `@IsActive` | `CareerChoiceGroupObjectVM` | property | CONNECTED |
| `Command.HoverBegin=ExecuteBeginHover` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.HoverEnd=ExecuteEndHover` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.Click=ExecuteClickIncrease` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `Command.Click=ExecuteClickDecrease` | `CareerChoiceGroupObjectVM` | public method | CONNECTED |
| `@Tier2RequirementText` | `CareerScreenVM` | property | CONNECTED |
| `@Tier2Locked` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3RequirementText` | `CareerScreenVM` | property | CONNECTED |
| `@Tier3Locked` | `CareerScreenVM` | property | CONNECTED |
| `@FreeCareerPointsText` | `CareerScreenVM` | property | CONNECTED |
| `Parameter.DoneButtonAction=ExecuteClose` | `CareerScreenVM` | public method | CONNECTED |
| `@DoneLbl` | `CareerScreenVM` | property | CONNECTED |

No references remain to `@Tier2GateBottomHalf`, `@Tier3GateTopHalf`, `@Tier3GateFull`, or `@Tier1Locked`.

**LOC CONSISTENCY**
DISPUTED as a bug.

Checks performed:
- `taom_career_choices.xml`, `taom_careers.xml`, and `taom_module_strings.xml` all parse as well-formed XML.
- Active `ChoiceGroup display_name` attrs: `288`.
- Raw `ChoiceGroup display_name` attrs: `294`.
- Difference: `6`, all inside the pre-existing `cave_troll_master` disabled comment block.
- Careers with rank attrs: `49`, rank attrs total: `147`.
- Active referenced group/rank localization keys: `435`.
- Missing referenced keys: `0`.
- Duplicate `<string id>` entries in `taom_module_strings.xml`: `0`.
- Career-prefixed string nodes including disabled cave-troll group keys plus `taom_career_tier_requirement`: `442`.
- ID/text-token mismatches: `0`.

Spot checks passed for `ranger_of_ithilien_t1_a`, `captain_of_osgiliath_t1_b`, `ranger_of_ithilien` rank 1/2/3, and `eotheod_windrider` rank 1/2/3.

**KNOWN SUSPECTS**
1. SPRITE-ATLAS SHEET-2 EDIT: **DISPUTED**. Runtime loader supports per-category sheet indexes and per-sheet sizes.
2. VM<->PREFAB BINDING COMPLETENESS: **DISPUTED**. All unique bindings connect; removed gate props are absent.
3. LOCALIZATION KEY CONSISTENCY: **DISPUTED**. XML well-formed, no duplicate string IDs, no missing active keys.
4. ISTIERAVAILABLE REFACTOR EQUIVALENCE: **DISPUTED for game-real inputs**. It differs only for `heroLevel <= 0` tier 1. Vanilla `CharacterObject.Init()` sets `Level = 1`, and `Hero.SetInitialValuesFromCharacter()` copies `Level = characterObject.Level`. `GauntletCareerScreen.OpenCareerScreen()` also returns early if `Hero.MainHero` is null. So real heroes should not hit the changed case.
5. OPTIONAL-PARAM CTOR BACK-COMPAT: **DISPUTED**. New params are trailing optionals; call sites use named args; parser passes `rank1_name`, `rank2_name`, `rank3_name`, and `display_name`.
6. IsUnavailable NOTIFICATION: **DISPUTED**. `IsTaken` and `IsFreeToTake` both notify `IsUnavailable`; v1.4.5 `ViewModel` has public `OnPropertyChanged(string)`. Screen refresh rebuilds the VM list after selection changes.
7. COMMENTED-BLOCK INJECTION: **DISPUTED**. The 6 cave-troll `ChoiceGroup display_name` attrs are inert inside the XML comment; active XML node count confirms they are not parsed.

**FINDINGS**
No confirmed bugs found.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 0  
VERDICT: CLEAN
