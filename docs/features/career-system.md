# Career System

**Status:** Verified in-game (2026-04-14). Career button with sprite on Character Developer screen, GameState-based screen opening (no crash), career selection in Character Creation. Gondor campaign tested.

**2026-05-04 update.** Ability activation rebuilt as a uniform 30-second cooldown timer. The original charge-based readiness model (DamageDone / Kills / DamageTaken accumulators) was replaced because per-archetype charge types produced confusing UX — defensive careers like Captain of Osgiliath only charged when the player took damage, so back-line players never saw the ability ready. See [Cooldown System](#cooldown-system) and `CHANGELOG.md` (issue #103).

**2026-06-02 update — issues #102 + #104.** `CareerPerkMissionBehavior` refactored from 302 → 139 LOC by extracting three Singleton-lifetime controllers (`IAbilityActivationController` + `IAbilityHudController` + `IAbilityEffectExecutor`) and two adapters (`IAbilityInputAdapter` + `IMissionTimeProvider`); the V-key state machine is now unit-testable end-to-end. The 98 dead `MaxCharge` mutations in `taom_career_choices.xml` were repurposed as `CooldownReduction` (50× -6, 48× -9) targeting a new property on `AbilityTemplateData`; effective cooldown is `max(MinCooldownSeconds=5, 30 - reduction)`. Two Codex adversarial reviews + a 5-dimension Claude deep-review fan-out were applied; all 17 actionable findings triaged.

## Overview

Career/class progression system where each hero can have a career that provides passive bonuses, an active ability, and a 3-tier choice tree. 50 LOTR-themed careers across 16 factions, fully XML-driven. Each career has 31 choices (1 root + 6 groups x 5 choices) with keystones, passives, and ability mutations.

## Why This Exists

- **Vanilla behavior:** Bannerlord has perks but no career/class system with branching choice trees or active abilities
- **TAOM requirement:** Middle-earth factions need distinct playstyles beyond cultural feats — a Mordor Warboss should feel different from a Gondor Knight
- **Without this feature:** All heroes of the same culture play identically after initial perk selection

## Architecture

### Design Challenge

A hardcoded-C#-career design (static singletons + ViewModel Harmony patches) was rejected. TAOM needs XML-driven careers (add without recompilation), DryIoc injection, adapter pattern compliance, and UIExtenderEx integration.

### Solution Approach

- **Data model:** Plain C# classes (not PropertyObject) loaded from XML via `ICareerConfigProvider`
- **Persistence:** `CareerPersistenceBehavior` with `SyncData("_taom_careerData")` storing `Dictionary<string, HeroCareerData>`
- **Passive application:** `ICareerPassiveService` caches per-hero effect magnitudes, `CareerPassiveHelper` wires into 8 existing GameModels
- **Mutations:** Hybrid XML + C# calculator registry — XML defines target/params, C# provides calculator functions by ID
- **UI:** `GauntletCareerScreen` with `CareerScreenVM` hierarchy (expandable panels, portraits, ability icons), `CharacterDeveloperCareerMixin` (UIExtenderEx) for career button with sprite. See [gui-sprite-system.md](gui-sprite-system.md) for full UI details. **Screen revamp 2026-05-30** ([RCA](../reviews/rca-career-ui-revamp-2026-05-30.md)): tiers ordered Tier 3 (top) → Tier 1 (bottom); locked tiers show a **"Requires Level N"** label (level from `CareerRegistry.GetTierUnlockLevel` — T1/1, T2/10, T3/20) instead of the old gate art; each node is an always-visible **point-pip strip** (3 brightness states — taken / available / empty — via `CareerChoiceObjectVM.IsUnavailable`) with perk descriptions on hover, using the shared `CareerSystem\career_point_pip` One Ring sprite; tier headers show per-career **rank titles** (`CareerDefinition.Rank1/2/3Name`, fallback "Tier N"); node headers show per-group **lore names** (`CareerChoiceGroupDefinition.DisplayName`, humanized-id fallback). 294 group names + 147 rank titles are web-researched Tolkien-grounded (`tools/career_group_names.json`, `tools/career_rank_names.json`). **In-game pass 2026-05-31:** names singularized (single-player career → singular titles, e.g. "Warden of the East Bank" not "Wardens"; via `tools/singularize_career_names.py`); tier rank labels set flush-left (`CoverChildren`+Left, no wrap-indent); locked-tier node spacing matched to Tier 1 by reserving the `+`/`−` button column (fixed 70px, buttons gated on `@IsActive`). **Pip sprite — two fixes (bake + render):** the pip first needed the offline sprite generator to bake it into the `ui_taom_career_system` atlas (`AssetSources/GauntletUI/...png` + `Assets/GauntletUI/..._tex.tpac` — **not** a `pack0.tpac`), and then a prefab fix because even baked it rendered invisibly at 22×28px/27% alpha (bumped to 38×38 + brighter opacities); the "Requires Level N" label was re-centered into the gap between the two node columns (`CoverChildren`+Center alone was insufficient — the 70px button reserve shifts the boxes left of row-center, so a `PositionXOffset="-40"` was needed); and the hover perk descriptions were made **inline with the pips** (the parallel pip + description `{Choices}` lists were given matching 46px rows and the description text switched to `CoverChildren`+left, so each description sits on its pip's row). **All confirmed working in-game (user screenshots, 2026-05-31).** **Taken-pip lit-ring follow-up (2026-06-19 → 22, issue #290):** the *taken* state now uses a brighter dedicated sprite `CareerSystem\career_point_pip_lit` (the One Ring ring whitened + a soft glow halo); available/locked stay on the shared hollow `career_point_pip` dimmed to `#FFFFFF55` / `#FFFFFF22`. The old scheme tinted all three states of the *same* hollow ring, so taken vs available was only a ~12% alpha gap — an increased skill never visibly "lit up." Gauntlet `Color` is a multiplicative tint (can't brighten past the sprite's own pixels), so a distinct brighter sprite was required, not just a tint change. Confirmed working in-game 2026-06-22. See [gui-sprite-system.md](gui-sprite-system.md) "The sprite-bake pipeline" (decompile-verified) + "Verifying a sprite (bake + render)" + the 2026-06-19 follow-up, and [RCA post-review in-game findings](../reviews/rca-career-ui-revamp-2026-05-30.md#post-review-in-game-findings-2026-05-31).
- **Battle:** `CareerPerkMissionBehavior` (a 139-line thin entry point per ADR-002) delegates per-frame work to three Singleton-lifetime controllers:
  - [`IAbilityActivationController`](../../Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs) — V-key + ready-state notification + charging-message throttle state machine. Returns an `AbilityActivationResult { JustBecameReady, Activated, Charging }` flags struct so the host can emit BOTH the green "ready" toast and the yellow "activated" toast on the same frame (legacy UX). Fully unit-testable via `IAbilityInputAdapter` + `IMissionTimeProvider` injection — no TaleWorlds statics.
  - [`IAbilityHudController`](../../Main/Features/CareerSystem/UI/IAbilityHudController.cs) — `GauntletLayer` lifecycle (`TryInitialize` / `Refresh` / `Cleanup`). Captures `_attachedScreen` at attach time so `Cleanup` removes from the same screen it attached to (vanilla `ScreenBase.RemoveLayer` calls `HandleFinalize` unconditionally — removing from the wrong screen corrupts both screens). `Cleanup` engine calls are wrapped in `try / catch / finally` so a throw from `RemoveLayer` / `ReleaseMovie` / `OnFinalize` cannot leave the Singleton with `_hudInitialized=true` (which would silently kill the HUD for every subsequent mission this session).
  - [`IAbilityEffectExecutor`](../../Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs) — per-activation pipeline: mutate template, apply CooldownReduction adjustment, allocate `MissionAbilityExecutionContext`, register with host's `_activeContexts` list, dispatch per-archetype effect executor, emit toast + sound + particles.
- **Singleton lifetime + per-step OnEndMission try/catch.** All three controllers are `Reuse.Singleton`; the host `CareerPerkMissionBehavior` is constructed fresh per mission. Cross-mission state (`_abilityReadyNotified`, `_hudInitialized` etc.) lives on the singletons and is cleared by explicit `Reset()` / `Cleanup()` calls in `OnEndMission`. Per the deep-review systemic finding, each cleanup op runs in its own `try/catch` so a throw in one (most plausibly `_hudController.Cleanup`) cannot abort the others. `CareerAbilityService` forces `ChargeType.CooldownOnly` for all 50 careers — readiness is purely cooldown-timer based (see [Cooldown System](#cooldown-system)).
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

Defines careers with: id, display name, description, portrait sprite, ability template ID, eligible cultures, choice group IDs, root choice id, min clan tier. **`rank1_name` / `rank2_name` / `rank3_name`** (added 2026-05-30) — per-career tier-header rank titles shown on the career screen (e.g. `Ohtar → Roquen → Knight of the Golden Flower`); optional, the VM falls back to "Tier N". `max_perk_points` attribute on root element (default 30). (Pre-2026-05-04 the schema also had `charge_type` and `max_charge` — both removed; cooldown is global, not per-career.)

### Choice Trees (`Main/_Module/ModuleData/career_system/taom_career_choices.xml`)

Defines standalone root choices and choice groups. Each group has a tier (1/2/3), an optional **`display_name`** (added 2026-05-30 — the per-group lore "path" name shown as the node header, e.g. `Watchers of Henneth Annûn`; the VM humanizes the id as fallback), and contains choices (Keystone or Passive). Choices can have PassiveEffect (type + magnitude + operation) and Mutations (target template + property + calculator + params).

**Two PassiveEffect schemas are accepted** (`CareerConfigProvider.ParseChoice`):
- **Direct (preferred):** `<Choice ...><PassiveEffect type="X" magnitude="0.10" attack_type_mask="Melee" /></Choice>`
- **Wrapped:** `<Choice ...><PassiveEffects><PassiveEffect type="X" value="0.10" /></PassiveEffects></Choice>` — the parser reads the *first* `<PassiveEffect>` inside the plural `<PassiveEffects>` wrapper (one child only — multi-child wrappers silently drop the rest), and accepts `value=` as an alias for `magnitude=` (`magnitude=` wins when both are present). The wrapped form was historically unparsed (310 dead choices across all 16 cultures); fixed 2026-05-29 — see [RCA](../reviews/rca-career-partysize-2026-05-29.md).

`PassiveEffect` carries `type`, `magnitude`, and `attack_type_mask`. The legacy `operation`/`is_percentage` attributes were **parsed-but-never-read** and were removed 2026-06-25 (ignored if still present in XML).

**Magnitude scale ↔ application method (IMPORTANT).** `CareerPassiveService` stores the summed `Magnitude` per type (`GetPassiveMagnitude`) plus a per-(type, mask) breakdown (`GetMaskedMagnitude`); each consumer chooses flat vs factor by calling `ApplyFlat` (`result.Add`) / `ApplyFactor` (`result.AddFactor`) or applying the magnitude directly. So a passive's authored magnitude scale MUST match its consumer's method:
- **Fractional magnitude (0.10 = +10%)** → `ApplyFactor` / multiplicative. The convention for almost every type (TroopWages, PartyMovementSpeed, TroopMorale, etc.).
- **Whole-count magnitude (2 = +2 units)** → `ApplyFlat`. Only **`PartySize`** (`TaomPartySizeModel`) and the agent-stat flat `Health` / `CompanionLimit`. Applying a whole-count via `ApplyFactor` multiplies the base (`AddFactor(2)` = ×3) — the "+2 → +150" bug fixed 2026-05-29.

**attack_type_mask (Damage + Resistance).** `Damage` and `Resistance` honor the mask — a "+X% ranged damage" pip fires only on ranged hits. The damage path derives the hit's delivery type (`IsMissile ? Ranged : Melee`) and `GetMaskedMagnitude` sums every authored-mask bucket that intersects it (an `All`-masked entry applies to both). `Damage` is applied multiplicatively on the per-hit **amplification** path (`CareerAgentStatService.CalculateDamageAmplification`), NOT as a flat `DamageMultiplierBonus`, so the mask can gate it. `Blunt`/`Cut` masks (5 shipped Resistance pips) aren't representable in the `[Flags]` enum and deliberately degrade to `All` (every hit).

**Effect-type consumers.** Every `PassiveEffectType` used by a shipped pip now has a runtime consumer — `PassiveEffectConsumers` is the compiled source of truth. The six historically-dead/phantom types — `Ammo`, `HorseChargeDamage`, `HorseHealth`, `TroopResistance`, `StealthBonus`, `HealthRegeneration` — were wired 2026-06-25 (HorseHealth + Ammo are multiplicative; consumers span `CareerAgentStatService`, `TaomAgentStatCalculateModel`, `TaomAgentApplyDamageModel`, `TaomMapVisibilityModel`, `TaomPartyHealingModel`, `CareerPerkMissionBehavior.OnAgentBuild`), and their ~211 magnitudes re-tuned to a uniform 10–15% band via `tools/retune_phantom_passives.py`. A load-time gate (`CareerConfigProvider.ValidatePassiveConsumers`) + a shipped-XML regression test (`CareerChoicesIntegrationTests`) now prevent a new phantom type from shipping silently. Pure arithmetic for the Ammo + StealthBonus consumers lives in the testable `CareerPassiveMath`.

### Ability Templates (`Main/_Module/ModuleData/career_system/taom_ability_templates.xml`)

Defines per-ability tunables: id, display name, duration (effect window), radius (AoE), `max_charge` (dead since #103 cooldown rework — present on the model for back-compat but unread by activation logic; designer mutations targeting it were repurposed as `CooldownReduction` in #104), `cooldown_reduction` (per-activation cooldown shortening in seconds, applied AFTER mutations have run; floored at `MinCooldownSeconds`), particle/sound effects, tooltip. Cooldown is *not* per-template; see [Cooldown System](#cooldown-system).

### Cooldown System

`Main/_Module/ModuleData/career_system/taom_ability_tuning.xml` declares a `<Global>` element shared by all 50 careers. Two tunables:

```xml
<AbilityTuning>
  <Global cooldown_seconds="30" min_cooldown_seconds="5" />
  <Infantry .../>
  <Ranged .../>
  <Cavalry .../>
</AbilityTuning>
```

- **`cooldown_seconds` — Default 30s.** Validation: must be in `(0, 3600]`. Non-finite (`NaN`/`±Infinity`), out-of-range, malformed, or missing values fall back to 30s with a `LogWarning` (`CareerConfigProvider.ParseGlobalTuning`). NaN guard precedes the range check because IEEE-754 NaN comparisons always yield false (see `feedback_clamp_nan_infinity_propagates.md` — bug has shipped three times).
- **`min_cooldown_seconds` — Default 5s (#104).** Validation: must be ≥ 0 AND ≤ `cooldown_seconds`. Floor for designer `CooldownReduction` mutations — prevents stack-up cheese where 4+ keystones with `-9` reductions would zero the cooldown. Same NaN/Infinity guards as the cooldown itself.
- **Reload scope:** `CareerConfigProvider` is a `Reuse.Singleton` and caches the parsed config. Changes require a full Bannerlord application restart — not a save-load.
- **Per-career override:** Not supported at the tuning-config layer. **Per-keystone CooldownReduction is supported** — see CooldownReduction Mutations below.

#### CooldownReduction Mutations (#104, Option B)

A keystone choice can shorten the global cooldown for the next activation via a `<Mutation>` targeting the new `CooldownReduction` property on `AbilityTemplateData`:

```xml
<ChoiceGroup id="...">
  <Choice id="..._keystone_t1" type="Keystone">
    <Mutations>
      <Mutation target="cooldown_reduction" property="CooldownReduction" calculator="flat" value="6"/>
    </Mutations>
  </Choice>
</ChoiceGroup>
```

- **`6` for tier-1 keystones, `9` for tier-2/3 keystones** is the convention. Effective cooldown = `max(min_cooldown_seconds, cooldown_seconds - reduction)`. With defaults: tier-1 = `max(5, 30-6) = 24s`; tier-2/3 = `max(5, 30-9) = 21s`. Stacks of 4 tier-2/3 keystones (which would sum to 36) floor at 5s.
- **Application order in `AbilityEffectExecutor.Execute`:** the per-activation pipeline calls `MutateTemplate` to apply all choice mutations to a cloned template, runs the per-archetype effect executor, THEN calls `_abilityService.ApplyCooldownAdjustment(heroId, template.CooldownReduction, min)`. The adjustment runs AFTER `executor.Execute` so a throw from the effect executor (e.g., `ApplyAoeBuff` mid-iteration) does not shorten the cooldown for an activation whose effect did not fully apply. The HUD pip on the next frame still sees the adjusted cooldown.
- **Designer convention — POSITIVE values only.** `<Mutation property="CooldownReduction" ... value="6"/>` means "shorten the next cooldown by 6 seconds." The `flat` calculator is `baseValue + value` (`BuiltInCalculators.cs:7-8`), so the mutated template's `CooldownReduction` is positive 6. `AbilityEffectExecutor` guards on `template.CooldownReduction > 0f` before calling `ApplyCooldownAdjustment`. A negative value would be silently rejected by that guard. Codex pass-2 (2026-06-02) caught a HIGH where the first XML rewrite emitted `-6`/`-9` and the entire feature was a no-op; fixed by re-running the script with the positive-value variant.
- **History.** 98 mutations originally targeted `MaxCharge` (-20 / -30) under the charge-based system. After the #103 cooldown rework `MaxCharge` became unread; #104 Option B rewrote them via `tools/rename_maxcharge_to_cooldownreduction.py` (50 × 6, 48 × 9) preserving the 1.5× tier ratio. The `MaxCharge` property itself remains on `AbilityTemplateData` for back-compat — harmless dead but reflective-mutation pipeline (`MutationService`) would still write to it if a future XML re-introduces a mutation. Removing it is deferred (see Codex pass-2 LOW#2; tracked as future cleanup).

#### In-battle UX

- Abilities start ready at battle open.
- Pressing `V` while ready: yellow *"<Ability> activated!"* message + buff/sound/particle effect.
- Pressing `V` while on cooldown: throttled gray *"Career ability still charging — Ns remaining"* (one message per 2s).
- One-shot green *"Career ability is ready! Press V to activate"* when the cooldown elapses. On the frame the ability becomes ready AND V is pressed simultaneously, BOTH the green ready toast and the yellow activated toast emit (preserving legacy UX — see `AbilityActivationResult` flags struct).

**HUD panel placement** — the on-screen ability panel (icon + career name + "Press V" prompt + charge bar) is the standalone Gauntlet layer loaded from [`Main/_Module/GUI/Prefabs/CareerSystem/AbilityHUD.xml`](../../Main/_Module/GUI/Prefabs/CareerSystem/AbilityHUD.xml) by [`AbilityHudController`](../../Main/Features/CareerSystem/UI/AbilityHudController.cs) (display-only — no buttons, so no `SetInputRestrictions` needed). Position is controlled entirely by the **root `<Widget>`** anchoring attributes; all children lay out relative to the root and move with it. It is **right-anchored beside the player health bar** (`HorizontalAlignment="Right"` / `MarginRight="480"` / `VerticalAlignment="Bottom"` / `MarginBottom="80"`). Right-anchoring is deliberate: the vanilla health/ammo HUD is pinned to the screen's right edge, so a `Center`-anchored panel drifts far left and unreachable on ultrawide displays — anchoring `Right` makes the panel track the same edge at any width. Tuning knobs (GUI-live-only — only the running game confirms placement): `MarginRight` horizontal (decrease → further right toward the health bar, increase → left), `MarginBottom` vertical.

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
| `Main/Features/CareerSystem/Abilities/` (16 files) | CareerAbility (now with `AdjustCooldown`), ability service (now with `ApplyCooldownAdjustment`), effect registry, 3 executors, buff tracker, execution context, + 6 new files for #102: `IAbilityActivationController` / `AbilityActivationController` / `IAbilityHudController` deps `IAbilityInputAdapter` + `IMissionTimeProvider` + impls, `IAbilityEffectExecutor` / `AbilityEffectExecutor` |
| `Main/Features/CareerSystem/UI/AbilityHudController.cs` | HUD lifecycle controller (boundary class — verified in-battle) |
| `Main/Features/CareerSystem/CareerCampaignBehavior.cs` | Campaign lifecycle events |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Thin entry point (139 LOC, ADR-002 compliant — refactored #102) — delegates to the three controllers + owns `_activeContexts` expiration list + `OnEndMission` per-step try/catch teardown |
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
| AbilityActivationControllerTests | 13 | V-key state machine — NoCareer no-op, JustBecameReady one-shot, simultaneous-flag emit, charging throttle window, Reset clears both flags |
| CooldownReductionTests | 15 | AbilityTemplateData copy-ctor, GlobalTuning ctor, AdjustCooldown happy/floor/zero/negative/NaN/Infinity/charge-based no-op, service ApplyCooldownAdjustment unknown-hero + floor |
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

## Effect-Scope Badges in Choice Tree (UX)

`CareerChoiceObjectVM.EffectScopeBadge` renders a small "While active" text next to keystone (mutation) bullets in the choice tree. The prefab gates the badge widget on `@IsKeystone` so passives render with no badge (the always-active default is the convention). The distinction matches the runtime: `PassiveEffect` choices flow through `CareerPassiveService.GetPassiveMagnitude` and are read by GameModel overrides on every relevant calculation (always active); `Mutation` (Keystone) choices are applied to a cloned `AbilityTemplateData` inside `ExecuteAbilityEffect` on V-press only — the clone is discarded once the buff window expires.

## Career-Switch Picker (dialogue → switch-mode screen)

When the player picks "I wish to discuss my career path" on any companion (under vanilla's `hero_main_options` dialogue token), the dialogue gate uses `ICareerRegistry.GetEligibleSwitchTargets(currentCareerId, hero)` to decide whether to show the option. The consequence opens `GauntletCareerScreen` in switch mode (via `CareerScreenGameState.IsSwitchMode = true`). The same screen renders normal-mode (perk tree) or switch-mode (picker) based on the flag; gates in `CareerScreen.xml` use `@IsNormalMode` and `@IsSwitchMode` to swap the middle area.

**Lifecycle gotcha (Codex Review #46):** Vanilla `GameStateManager.CreateState<T>()` invokes the screen ctor synchronously via `HandleCreateState → OnCreateState → CreateScreen → Activator.CreateInstance(type, state)` BEFORE `OpenCareerScreen` can set `state.IsSwitchMode = true`. Any new feature flag plumbed through `CareerScreenGameState` MUST be read in `OnInitialize`, not in the screen ctor. See [`GauntletCareerScreen.cs:OnInitialize`](../../Main/Features/CareerSystem/UI/GauntletCareerScreen.cs).

**Empty-state UX.** The picker's `ScrollablePanel` is gated on `@IsBrowsingTargets` (= `_isSwitchMode && targets.Count > 0`); the empty-state `TextWidget` bound to `@NoTargetsMessage` is gated on `@HasNoSwitchTargets` (= `_isSwitchMode && targets.Count == 0`). The dialogue gate prevents the empty-list state being reached via normal flow, but a static-entry-point or `_heroAdapter == null` reaches it; `RebuildEligibleSwitchTargets` logs a warning so a player report of "blank picker" is triagable. `IsBrowsingTargets` and `HasNoSwitchTargets` are computed expression-body properties; the VM fires `OnPropertyChanged` for both after `_eligibleSwitchTargets.Clear() + Add()` mutations (Gauntlet does not re-evaluate computed bools on collection mutation).

**Switch contract:** `ICareerSwitchService.SwitchCareer(heroStringId, hero, newCareerId)` clears old career + choices + tier unlocks + flags, sets the new career, adds the new root choice, refreshes the passive cache. `CanSwitch` rejects same-career switches at the boundary (`hero.StringId → _dataService.GetCareerStringId → ordinal-ignore-case == newCareerStringId`).

## Changelog

- 2026-06-19 — Career-ability pips now visibly light up when a skill is increased: taken state uses a brighter dedicated `career_point_pip_lit` sprite (One-Ring ring whitened + glow halo) instead of a ~12% alpha bump on the shared hollow ring (#290).
- 2026-06-15 — Right-anchored the in-battle ability HUD beside the player health bar (`MarginRight="480"`) so it tracks the right edge and stays reachable on ultrawide displays.
- 2026-06-02 — Decomposed `CareerPerkMissionBehavior` 302→139 LOC into three Singleton controllers + two adapters; repurposed 98 dead `MaxCharge` mutations as `CooldownReduction` with a `min_cooldown_seconds` floor (closes #102, #104).
- 2026-06-01 — Added the "While active" effect-scope badge in the choice tree + dialogue-driven career-switch picker (#265); added the career-tied quest framework (1.4.5-verified) with Gondor proof-of-life.
- 2026-05-31 — In-game review pass on the career-screen revamp (singularized names, tier-label alignment, node spacing, pip bake + render fixes).
- 2026-05-30 — Career-screen UI revamp: Tier 3→Tier 1 ordering, "Requires Level N" locked-tier labels, always-visible point-pip strips, 441 web-researched lore names + per-career rank titles.
- 2026-05-29 — Fixed party-size passive applied flat instead of as a ×N factor; activated 310 dead wrapped-schema career passives.
- 2026-05-27 — Added starter-equipment rosters for every culture (12 new cultures × 6 archetypes).
- 2026-05-26 — Re-enabled `cave_troll_master` as Gundabad Berserker (Infantry); renamed Gundabad/Rivendell/Dunland/Isengard/Dale careers to Tolkien-flavored display names.
- 2026-05-24 — All 50 ability tooltips now state actual archetype effects + duration; added warg-mount cavalry starters (Isengard/Gundabad/Mordor/Dol Guldur); fixed Gondor cavalry starter.
- 2026-05-21 — Fixed Captain of Osgiliath Keystone descriptions to say "Career Ability" not "Sailing".
- 2026-05-20 — Review-stage 3D preview now matches career selection (#206).
- 2026-05-19 — Added archetype-driven starting equipment at character creation; removed orphan `career_menu.json` entries for disabled WIP careers.

_See the repository-root `CHANGELOG.md` for full chronological history._

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-balance.md](./battle-balance.md)
- [docs/features/career-quest-system.md](./career-quest-system.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)
- [docs/reviews/rca-career-phantom-passives-2026-06-26.md](../reviews/rca-career-phantom-passives-2026-06-26.md)
- [docs/reviews/rca-career-starting-equipment-2026-05-19.md](../reviews/rca-career-starting-equipment-2026-05-19.md)

<!-- backlinks-end -->
