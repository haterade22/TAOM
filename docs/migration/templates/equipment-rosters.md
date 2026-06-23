# Equipment Roster XML Templates (v1.4.5 reference)

> The canonical reference for the 1.4.3 equipment-system migration. Pasted examples below are extracted **verbatim from vanilla 1.4.5 XMLs** at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\{SandBox,SandBoxCore,StoryMode}\ModuleData\`. Anything in this doc that is **not** a direct quote from those files is annotated as such.
>
> See also: [v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md) (migration spec), [v1.4.x-taom-impact.md](../v1.4.x-taom-impact.md) (surface impact), [equipment-roster-coverage.csv](../equipment-roster-coverage.csv) (per-culture audit output).

---

## Overview — what changed in 1.4.3

Three structural changes to how equipment rosters are authored:

1. **`EquipmentFlags` enum** — 13 flags removed, 5 added. Old multi-axis classification (Noble / Civilian / Combatant / Medium / Heavy / Stoic / Flamboyant / Nomad / Woodland / Wanderer / Gentry / RebelHero) collapsed into a clean grid of `IsLordTemplate` × `IsFemaleTemplate` × age-stage (`IsChildEquipmentTemplate` / `IsTeenagerEquipmentTemplate`) × `IsKingdomRulerTemplate`.

2. **`equipmentType` attribute** on `<EquipmentSet>` — replaces `civilian="true"`. Enum-restricted to `"Battle"`, `"Civilian"`, `"Stealth"` (new). Flag-combination syntax (`"Civilian | Battle"`) does NOT work — schema enforces single value.

3. **`culture` attribute required on `<EquipmentRoster>`** — engine warns at load when missing. Enables the new `EquipmentSelectionModel` to resolve rosters by (gender, age-stage, culture, lord/ruler intent) instead of per-character `civilianTemplate=`/`battleTemplate=` lookups.

The matching API change: `EquipmentSelectionModel.GetEquipmentRostersForInitialChildrenGeneration(Hero)` and similar methods now return a single resolved `Equipment` instead of `MBList<MBEquipmentRoster>`. See `Main/Adapters/ChildCreatorAdapter.cs:40` for the only direct TAOM consumer.

### Vanilla 1.4.5 audit (sanity check)

Confirmed by grep against the installed 1.4.5 game files on 2026-05-22:

| Pattern | SandBox/SandBoxCore/StoryMode equipment files | Notes |
|---|---|---|
| **Deprecated `EquipmentFlags` values** (IsNobleTemplate, IsCivilianTemplate, IsCombatantTemplate, IsNoncombatantTemplate, IsWandererEquipment, IsGentryEquipment, IsRebelHeroEquipment, IsMediumTemplate, IsHeavyTemplate, IsFlamboyantTemplate, IsStoicTemplate, IsNomadTemplate, IsWoodlandTemplate) | **0 occurrences in all three modules** | Engine is fully migrated. |
| **`civilian="true"` on `<EquipmentSet>`** | **0 occurrences** | Fully replaced by `equipmentType=`. |
| **`civilian="true"` on inline `<EquipmentRoster>` inside `<NPCCharacter>/<Equipments>`** | **1,097 in `SandBoxCore/spnpccharacters.xml`, 3 in `SandBox/lords.xml`** | ⚠️ This pattern is **still in use in vanilla 1.4.5** — see "Two distinct usage patterns" below. |
| **`equipmentType=` on `<EquipmentSet>`** | 255 in spnpccharacters.xml, 396 in lords.xml, hundreds across sandboxcore/sandbox_equipment_sets, 7 in story_mode_equipments | Canonical new form. |
| **`equipmentType="Stealth"`** | 1 (in `SandBox/sandbox_equipment_sets.xml`, roster `default_stealth_equipment_roster`) | New enum value, sparsely used. |

### Two distinct usage patterns (CRITICAL — they coexist in vanilla 1.4.5)

There are **two** structurally different roster containers, and they use different attributes:

| Pattern | Element | Container | Attribute for civilian | TAOM equivalent |
|---|---|---|---|---|
| **A. Standalone roster** in `<EquipmentRosters>` root | `<EquipmentSet>` (no `id`) | Top-level `<EquipmentRosters>` XML file | `equipmentType="Civilian"` | `taom_equipment_sets_*.xml`, `taom_wanderer_equipment.xml`, `taom_child_equipment_templates.xml`, `taom_char_creation_equipment.xml`, `taom_career_starting_equipment.xml`, `taom_education_equipment_templates.xml` |
| **B. Inline roster** in NPC `<Equipments>` block | `<EquipmentRoster>` (no `id`) | Nested inside `<NPCCharacter>/<Equipments>` | `civilian="true"` (still valid in 1.4.5) | TAOM character files — `characters/npcs_*.xml`, `characters/lords.xml`, `troops/troops_*.xml`, `taom_wanderers.xml`, `named_companions/named_companions.xml` |
| **C. Reference to standalone roster** inside an NPC | `<EquipmentSet id="X">` | Nested inside `<NPCCharacter>/<Equipments>` | `equipmentType="Civilian"` on the reference | Used in vanilla `SandBox/lords.xml` (e.g., line 219–221) |

This is the trap the migration tool already handles correctly: `tools/migrate_equipment_type_1_4_3.py` ONLY touches `<EquipmentSet>` elements, leaving `civilian="true"` on inline `<EquipmentRoster>` elements alone (because that pattern is still valid). The `civilian="true"` references in TAOM character XMLs are **not** the deprecation target; the `civilian="true"` on `<EquipmentSet>` is.

---

## Anatomy of a standalone `<EquipmentRoster>`

```xml
<EquipmentRoster
    id="<unique-string>"
    culture="Culture.<cultureId>">
  <EquipmentSet
      equipmentType="<Battle|Civilian|Stealth>">    <!-- 1..N times -->
    <Equipment slot="<SlotName>" id="Item.<itemId>" />
    <!-- more Equipment children -->
  </EquipmentSet>
  <Flags                                            <!-- 0..1 times -->
      IsLordTemplate="<true|false>"
      IsFemaleTemplate="<true|false>"
      IsChildEquipmentTemplate="<true|false>"
      IsTeenagerEquipmentTemplate="<true|false>"
      IsKingdomRulerTemplate="<true|false>" />
</EquipmentRoster>
```

### `<EquipmentRoster>` attributes

| Attribute | Required? | Values | Notes |
|---|---|---|---|
| `id` | yes | string, globally unique | Lookup key for `MBObjectManager.GetObject<MBEquipmentRoster>(id)`. |
| `culture` | **yes in 1.4.3+** | `Culture.<id>` (e.g. `Culture.empire`, `Culture.gondor`) | Engine logs a warning at load when missing. Required for the new `EquipmentSelectionModel` to resolve gender + culture filtering. |

### `<EquipmentSet>` attributes

| Attribute | Required? | Values | Notes |
|---|---|---|---|
| `equipmentType` | yes (new in 1.4.3) | `"Battle"` \| `"Civilian"` \| `"Stealth"` | Schema-enforced single value — flag-combination syntax fails. **Omitting it defaults to `"Battle"`** (vanilla rosters without explicit type are battle equipment). |
| `id` | only on references inside NPCs | string | Used in `<Equipments><EquipmentSet id="X" equipmentType="Civilian" /></Equipments>` blocks to attach a named standalone roster to a specific character. |
| `civilian` | **deprecated** | (n/a) | Use `equipmentType="Civilian"`. Schema logs a warning when present. |

### `<Flags>` child element

A **single** `<Flags>` element inside the `<EquipmentRoster>`, after the `<EquipmentSet>` children. Each new flag is an attribute. **All 5 new flags** (verified against `EquipmentSelectionModel` consumers in vanilla 1.4.5 XMLs):

| Flag | Meaning |
|---|---|
| `IsLordTemplate` | Roster targets lord/noble characters (heroes assigned to ruling clans). Combine with `IsKingdomRulerTemplate` for kings/queens. |
| `IsFemaleTemplate` | Female-only. Without this flag, the roster is selected for males. |
| `IsChildEquipmentTemplate` | Pre-teen age stage (engine selects when hero age < teen threshold). |
| `IsTeenagerEquipmentTemplate` | Teen age stage (engine selects on come-of-age event). |
| `IsKingdomRulerTemplate` | Kingdom ruler tier — overrides `IsLordTemplate` selection when the hero is the ruling clan leader. Provides the "ruler swaps to crown" behavior new in 1.4.3. |

### `<Equipment>` slot reference (unchanged from 1.3.15)

```xml
<Equipment slot="<slot>" id="Item.<item>" />
```

Slots used by vanilla and TAOM rosters: `Item0`, `Item1`, `Item2`, `Item3` (weapon slots), `Head`, `Body`, `Cape`, `Gloves`, `Leg`, `Horse`, `HorseHarness`.

---

## Vanilla 1.4.5 examples per category

All examples below are quoted **verbatim** from vanilla 1.4.5 XML; the file:line citation is in the heading.

### 1. Default male lord, battle (`IsLordTemplate`, default Battle)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 5238–5427 (roster `emp_bat_template_medium`, condensed — only one of its many `<EquipmentSet>` children shown):

```xml
<EquipmentRoster
    id="emp_bat_template_medium"
    culture="Culture.empire">
    <EquipmentSet>
        <Equipment slot="Item0" id="Item.empire_noble_sword_1_t5" />
        <Equipment slot="Item1" id="Item.reinforced_kite_shield" />
        <Equipment slot="Head" id="Item.empire_battle_crown_north" />
        <Equipment slot="Body" id="Item.empire_plate_vest_armor" />
        <Equipment slot="Cape" id="Item.varangian_bra_royal" />
        <Equipment slot="Leg" id="Item.decorated_imperial_boots" />
        <Equipment slot="Horse" id="Item.noble_horse_imperial" />
        <Equipment slot="HorseHarness" id="Item.imperial_scale_barding" />
    </EquipmentSet>
    <!-- ... many more battle EquipmentSet children ... -->
    <Flags
        IsLordTemplate="true" />
</EquipmentRoster>
```

**Notes:**
- No `equipmentType=` attribute on `<EquipmentSet>` → defaults to Battle.
- `<Flags>` element has `IsLordTemplate="true"` ONLY — implicit male (no `IsFemaleTemplate`), implicit non-child/non-teen.

### 2. Default lord civilian, male (`IsLordTemplate`, Civilian)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 6076–6203+ (roster `emp_civ_template_default`, one set shown). The roster contains MANY `<EquipmentSet equipmentType="Civilian">` children and a closing `<Flags>` — quoted shape only:

```xml
<EquipmentRoster
    id="emp_civ_template_default"
    culture="Culture.empire">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Item0" id="Item.empire_noble_sword_1_t5" />
        <Equipment slot="Body" id="Item.imperial_robes" />
        <Equipment slot="Leg" id="Item.fine_town_boots" />
        <Equipment slot="Horse" id="Item.noble_horse_imperial" />
        <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
    <!-- ... more civilian EquipmentSet children ... -->
    <Flags
        IsLordTemplate="true" />
</EquipmentRoster>
```

### 3. Lord child, male (`IsLordTemplate | IsChildEquipmentTemplate`, Civilian)

`SandBox/sandbox_equipment_sets.xml` line 14089–14114:

```xml
<EquipmentRoster
    id="child_template_empire_noble_male"
    culture="Culture.empire">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Body" id="Item.tunic_with_rolled_cloth" />
        <Equipment slot="Leg" id="Item.ladys_shoe" />
    </EquipmentSet>
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Body" id="Item.fine_town_tunic" />
        <Equipment slot="Leg" id="Item.ladys_shoe" />
    </EquipmentSet>
    <Flags
        IsChildEquipmentTemplate="true"
        IsFemaleTemplate="false"
        IsLordTemplate="true" />
</EquipmentRoster>
```

**Notes:**
- All `<EquipmentSet>` children are `equipmentType="Civilian"` (children never wear battle gear).
- `IsFemaleTemplate="false"` is set **explicitly** (vanilla style for child/teen rosters). Setting it to `"false"` is functionally equivalent to omitting the attribute, but vanilla is verbose here for readability.

### 4. Lord child, female (`IsLordTemplate | IsChildEquipmentTemplate | IsFemaleTemplate`, Civilian)

`SandBox/sandbox_equipment_sets.xml` line 14115–14149:

```xml
<EquipmentRoster
    id="child_template_empire_noble_female"
    culture="Culture.empire">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Body" id="Item.empire_dress" />
        <Equipment slot="Leg" id="Item.ladys_shoe" />
    </EquipmentSet>
    <!-- ... more EquipmentSet children ... -->
    <Flags
        IsChildEquipmentTemplate="true"
        IsFemaleTemplate="true"
        IsLordTemplate="true" />
</EquipmentRoster>
```

### 5. Lord teenager, male (`IsLordTemplate | IsTeenagerEquipmentTemplate`, Civilian)

`SandBox/sandbox_equipment_sets.xml` line 14150–14175:

```xml
<EquipmentRoster
    id="teenager_template_empire_noble_male"
    culture="Culture.empire">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Body" id="Item.tunic_with_rolled_cloth" />
        <Equipment slot="Leg" id="Item.leather_shoes" />
    </EquipmentSet>
    <!-- ... -->
    <Flags
        IsTeenagerEquipmentTemplate="true"
        IsFemaleTemplate="false"
        IsLordTemplate="true" />
</EquipmentRoster>
```

### 6. Lord teenager, female (`IsLordTemplate | IsTeenagerEquipmentTemplate | IsFemaleTemplate`, Civilian)

`SandBox/sandbox_equipment_sets.xml` line 14176–14213:

```xml
<EquipmentRoster
    id="teenager_template_empire_noble_female"
    culture="Culture.empire">
    <EquipmentSet
        equipmentType="Civilian">
        <!-- equipment slots -->
    </EquipmentSet>
    <Flags
        IsTeenagerEquipmentTemplate="true"
        IsFemaleTemplate="true"
        IsLordTemplate="true" />
</EquipmentRoster>
```

### 7. Kingdom ruler, male battle (`IsKingdomRulerTemplate`, default Battle)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 14978–15018:

```xml
<EquipmentRoster
    id="ase_king_template_bat_m"
    culture="Culture.aserai">
    <EquipmentSet>
        <Equipment slot="Item0" id="Item.aserai_lance_1_t5" />
        <Equipment slot="Item1" id="Item.ornate_adarga" />
        <Equipment slot="Item2" id="Item.eastern_javelin_3_t4" />
        <Equipment slot="Item3" id="Item.aserai_sword_4_t4" />
        <Equipment slot="Head" id="Item.aserai_battle_crown" />
        <Equipment slot="Body" id="Item.aserai_full_scale_armor_on_chain" />
        <Equipment slot="Cape" id="Item.aserai_scale_shoulder_e" />
        <Equipment slot="Gloves" id="Item.northern_brass_bracers" />
        <Equipment slot="Leg" id="Item.decorated_imperial_boots" />
        <Equipment slot="Horse" id="Item.t3_aserai_horse" />
        <Equipment slot="HorseHarness" id="Item.half_mail_and_plate_barding" />
    </EquipmentSet>
    <Flags
        IsKingdomRulerTemplate="true" />
</EquipmentRoster>
```

### 8. Kingdom ruler, female battle (`IsKingdomRulerTemplate | IsFemaleTemplate`, default Battle)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 15019–15059:

```xml
<EquipmentRoster
    id="ase_king_template_bat_f"
    culture="Culture.aserai">
    <EquipmentSet>
        <!-- same slot suite as male ruler -->
    </EquipmentSet>
    <Flags
        IsFemaleTemplate="true"
        IsKingdomRulerTemplate="true" />
</EquipmentRoster>
```

### 9. Kingdom ruler, male civilian (`IsKingdomRulerTemplate`, Civilian)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 14475–14507:

```xml
<EquipmentRoster
    id="ase_king_template_civ_m"
    culture="Culture.aserai">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Item0" id="Item.aserai_noble_sword_1_t5" />
        <Equipment slot="Head" id="Item.aserai_crown" />
        <Equipment slot="Body" id="Item.eastern_silk_clothing" />
        <Equipment slot="Cape" id="Item.pauldron_cape_a" />
        <Equipment slot="Gloves" id="Item.strapped_leather_bracers" />
        <Equipment slot="Leg" id="Item.eastern_leather_boots" />
        <Equipment slot="Horse" id="Item.noble_horse_southern" />
        <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
    <Flags
        IsKingdomRulerTemplate="true" />
</EquipmentRoster>
```

### 10. Kingdom ruler, female civilian (`IsKingdomRulerTemplate | IsFemaleTemplate`, Civilian)

`SandBoxCore/sandboxcore_equipment_sets.xml` line 14508–14535:

```xml
<EquipmentRoster
    id="ase_king_template_civ_f"
    culture="Culture.aserai">
    <EquipmentSet
        equipmentType="Civilian">
        <Equipment slot="Item0" id="Item.aserai_noble_sword_2_t5" />
        <Equipment slot="Head" id="Item.aserai_crown" />
        <Equipment slot="Body" id="Item.aserai_tunic_waistcoat" />
        <Equipment slot="Leg" id="Item.eastern_leather_boots" />
        <Equipment slot="Horse" id="Item.noble_horse_southern" />
        <Equipment slot="HorseHarness" id="Item.light_harness" />
    </EquipmentSet>
    <Flags
        IsFemaleTemplate="true"
        IsKingdomRulerTemplate="true" />
</EquipmentRoster>
```

### 11. Stealth equipment (NEW in 1.4.3, `equipmentType="Stealth"`)

`SandBox/sandbox_equipment_sets.xml` line 16780–16807 (the only stealth roster in vanilla 1.4.5):

```xml
<EquipmentRoster
    id="default_stealth_equipment_roster"
    culture="Culture.neutral_culture">
    <EquipmentSet
        equipmentType="Stealth">
        <Equipment slot="Item0" id="Item.seax" />
        <Equipment slot="Item1" id="falchion_sword_t2" />
        <Equipment slot="Item2" id="Item.stealth_throwing_stone" />
        <Equipment slot="Body" id="Item.sleeveless_padded_short_coat" />
        <Equipment slot="Head" id="Item.pilgrim_hood" />
        <Equipment slot="Leg" id="Item.belted_leather_boots" />
        <Equipment slot="Cape" id="Item.battania_civil_cape" />
    </EquipmentSet>
</EquipmentRoster>
```

**Notes:**
- No `<Flags>` element. Stealth equipment is selected by hero-state code (disguised hero), not by template-resolution.
- TAOM does NOT yet use `Stealth` — explicitly out of scope per the migration spec.

### 12. Female-only (no lord) roster

`SandBox/sandbox_equipment_sets.xml` line 6469–6470 (excerpt) — used for the CC "father/mother" character-creation parents:

```xml
<EquipmentRoster
    id="father_char_creation_retainer_empire"
    culture="Culture.empire">
    <!-- EquipmentSet children -->
    <Flags
        IsFemaleTemplate="true" />
</EquipmentRoster>
```

**Notes:** `IsFemaleTemplate="true"` alone, no `IsLordTemplate` — used for non-noble female CC parents.

---

## Education-progression equipment (NO `<Flags>`)

`SandBox/education_equipment_templates.xml` (8,471 lines, entire file) contains rosters with **NO `<Flags>` element and NO `equipmentType=` attribute** on its `<EquipmentSet>` children. Excerpt:

```xml
<EquipmentRoster
    id="child_education_equipments_stage_1_page_0_branch_default_aserai"
    culture="Culture.aserai">
    <EquipmentSet>
        <Equipment slot="Body" id="Item.aserai_civil_f" />
        <Equipment slot="Leg" id="Item.southern_moccasins" />
    </EquipmentSet>
</EquipmentRoster>
```

These rosters are **selected by code** (the education campaign behavior) via roster-id lookup, not by template resolution. They don't need flags. TAOM's `taom_education_equipment_templates.xml` (10,154 lines) follows the same flag/`equipmentType` pattern, which is correct (vanilla also omits `equipmentType=` — the engine treats omitted as Battle, fine for these "stage" rosters which are equipment progression sets, not template selectors).

> ⚠️ **Correction (2026-06-22):** TAOM's education templates did NOT mirror vanilla on the `culture` attribute — vanilla's example above carries `culture="Culture.aserai"`, but all **980** TAOM rosters shipped WITHOUT it, producing 980 `EquipmentRoster ... don't have culture definition` warnings every launch. The attribute is the one part of this pattern that IS required (1.4.3+, see the attribute table above). Fixed by `tools/add_education_roster_cultures.py` (adds `culture="Culture.<id>"` from the `_<culture>` id suffix). So: education rosters need **no flags and no `equipmentType=`, but they DO need `culture=`** like every other standalone roster.

---

## Mapping: deprecated flags → 1.4.3 replacement

**THIS IS THE LOAD-BEARING TABLE.** Every deprecated flag in TAOM XMLs must be translated using this map. The rows are derived from:
- `docs/migration/v1.4.x-equipment-overhaul.md` (the migration spec, derived from TaleWorlds dev posts)
- Cross-checking against vanilla 1.4.5 to verify no deprecated flag still ships (confirmed: zero occurrences)

| Deprecated flag (1.3.15) | 1.4.5 replacement | Notes |
|---|---|---|
| `IsNobleTemplate="true"` | `IsLordTemplate="true"` | 1:1 rename. |
| `IsNobleTemplate="false"` | (omit `IsLordTemplate` attribute OR set `IsLordTemplate="false"`) | Vanilla 1.4.5 commonly uses explicit `IsLordTemplate="false"` for clarity on child/teen non-lord rosters. |
| `IsCivilianTemplate="true"` | (drop from `<Flags>`; set `equipmentType="Civilian"` on each `<EquipmentSet>` in the roster) | `<Flags>` no longer carries civilian/battle classification. |
| `IsCombatantTemplate="true"` | (drop from `<Flags>`; set `equipmentType="Battle"` on each `<EquipmentSet>` OR omit `equipmentType` — defaults to Battle) | Same — equipmentType lives on EquipmentSet now. |
| `IsNoncombatantTemplate="true"` | (drop from `<Flags>`; set `equipmentType="Civilian"` on each `<EquipmentSet>`) | "Noncombatant" semantically maps to civilian — verified by vanilla pattern (every child/teen roster uses civilian equipmentType). |
| `IsWandererEquipment="true"` | (drop entirely; rely on the roster's `id` + culture for selection) | Vanilla 1.4.5 has wanderer rosters (e.g., `npc_wanderer_equipment_template_battania`) with no Flags element at all — selection is by roster ID lookup in code. |
| `IsGentryEquipment="true"` | (drop entirely) | No replacement — concept collapsed. Selection happens via roster ID + culture. |
| `IsRebelHeroEquipment="true"` | (drop entirely) | Same — concept collapsed. |
| `IsMediumTemplate="true"` | (drop entirely; encode tier intent in roster `id` only) | Armor-weight tier was never a template-selection axis; it was a naming/grouping hint. Vanilla 1.4.5 keeps the IDs (`emp_bat_template_medium`, `emp_bat_template_heavy`) but drops the flag. |
| `IsHeavyTemplate="true"` | (drop entirely) | Same — id-only. |
| `IsFlamboyantTemplate="true"` | (drop entirely) | Stylistic axis collapsed. |
| `IsStoicTemplate="true"` | (drop entirely) | Same. |
| `IsNomadTemplate="true"` | (drop entirely) | Same. |
| `IsWoodlandTemplate="true"` | (drop entirely) | Same. |

### Special TAOM combinations encountered

TAOM's `taom_child_equipment_templates.xml` uses these multi-flag combinations (from the `/validate` tool output) — concrete replacements:

| TAOM current (1.3.15) | TAOM target (1.4.5) |
|---|---|
| `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsNobleTemplate="true" IsNoncombatantTemplate="true" />`<br>(noble male child)<br>+ children using `<EquipmentSet civilian="true">` | `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsLordTemplate="true" />`<br>+ children using `<EquipmentSet equipmentType="Civilian">` |
| `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="true" IsNobleTemplate="true" IsNoncombatantTemplate="true" />` (noble female child) | `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="true" IsLordTemplate="true" />` |
| `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsNobleTemplate="false" IsCivilianTemplate="true" IsNoncombatantTemplate="true" />` (townsman/villager male child) | `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsLordTemplate="false" />` |
| `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="true" IsNobleTemplate="false" IsCivilianTemplate="true" IsNoncombatantTemplate="true" />` (townsman/villager female child) | `<Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="true" IsLordTemplate="false" />` |

**Rule:** `Noble → Lord`; `Civilian + Noncombatant → equipmentType="Civilian"` on the EquipmentSet; `IsNoncombatantTemplate` and `IsCivilianTemplate` are dropped from `<Flags>` (their semantics migrate to the EquipmentSet's `equipmentType=` attribute).

---

## TAOM current state per file

Counts from grep on `bannerlord-1.4.5` branch HEAD, 2026-05-22.

### `taom_equipment_sets_<culture>.xml` (15 files — all 12 custom cultures + dale/rohan/dunland)

**Current state:** All 15 files have `<EquipmentRoster id="..._bat_template_*" culture="Culture.<id>">` and `..._civ_template_*` IDs, with NO `<Flags>` element and NO `equipmentType=` on `<EquipmentSet>`. The `culture=` attribute IS present (correct).

**Issues to fix:**
1. No `<Flags>` element → engine cannot resolve via `EquipmentSelectionModel` for any of:
   - Male/female lord battle (currently no `IsLordTemplate` + `IsFemaleTemplate` differentiation)
   - Male/female lord civilian (same)
   - Male/female lord child/teen (same — and missing entirely; covered only in `taom_child_equipment_templates.xml`)
   - Kingdom ruler tier (entirely absent)
2. `<EquipmentSet>` children omit `equipmentType=` — defaults to Battle. Civilian rosters (`gondor_civ_template_default_a`) have NO `equipmentType="Civilian"` set, so they will be wrongly classified as battle.

**Excerpt — current state (`taom_equipment_sets_gondor.xml` line 75–87, `gondor_civ_template_default_a`):**

```xml
<EquipmentRoster id="gondor_civ_template_default_a" culture="Culture.gondor">
    <EquipmentSet>          <!-- ⚠️ missing equipmentType="Civilian" -->
        <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
        <!-- ... -->
    </EquipmentSet>
    <!-- ⚠️ missing <Flags IsLordTemplate="true" /> -->
</EquipmentRoster>
```

**Target state:**

```xml
<EquipmentRoster id="gondor_civ_template_default_a" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian">
        <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
        <!-- ... -->
    </EquipmentSet>
    <Flags IsLordTemplate="true" />
</EquipmentRoster>
```

Plus authoring NEW rosters for the missing matrix combos (see "Mandatory per-culture matrix" below — `audit_equipment_roster_coverage.py` reports 96 missing-mandatory rosters across 12 cultures, 8 each).

### ⚠️ HIGHEST PRIORITY: `taom_child_equipment_templates.xml`

**Current state:** 60 rosters, 160 occurrences of deprecated flags (per `validate_equipment_flags_1_4_3.py`):
- `IsNobleTemplate`: 60 (every roster uses it — half `true`, half `false`)
- `IsCivilianTemplate`: 40 (on the townsman/villager rosters)
- `IsNoncombatantTemplate`: 60 (every roster — all children are noncombatant)

114 occurrences of `<EquipmentSet civilian="true">`.

**Excerpt — current state (line 5–15):**

```xml
<EquipmentRoster id="child_template_gondor_noble_male" culture="Culture.gondor">
    <EquipmentSet civilian="true">
        <Equipment slot="Body" id="Item.ithilien_jerkin_short" />
        <Equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
    </EquipmentSet>
    <EquipmentSet civilian="true">
        <Equipment slot="Body" id="Item.ithilien_jerkin_long" />
        <Equipment slot="Leg" id="Item.ithilien_boots" />
    </EquipmentSet>
    <Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsNobleTemplate="true" IsNoncombatantTemplate="true" />
</EquipmentRoster>
```

**Target state (per the mapping table above):**

```xml
<EquipmentRoster id="child_template_gondor_noble_male" culture="Culture.gondor">
    <EquipmentSet equipmentType="Civilian">
        <Equipment slot="Body" id="Item.ithilien_jerkin_short" />
        <Equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
    </EquipmentSet>
    <EquipmentSet equipmentType="Civilian">
        <Equipment slot="Body" id="Item.ithilien_jerkin_long" />
        <Equipment slot="Leg" id="Item.ithilien_boots" />
    </EquipmentSet>
    <Flags IsChildEquipmentTemplate="true" IsFemaleTemplate="false" IsLordTemplate="true" />
</EquipmentRoster>
```

### `taom_wanderer_equipment.xml`

**Current state:** Wanderer (companion) equipment rosters, 40 occurrences of `<EquipmentSet civilian="true">`. No deprecated flags. No `<Flags>` element on any roster.

**Excerpt — current state (line 5–14):**

```xml
<EquipmentRoster id="npc_companion_equipment_template_gondor" culture="Culture.gondor">
    <EquipmentSet>          <!-- battle — fine, defaults to Battle -->
        <!-- equipment slots -->
    </EquipmentSet>
    <EquipmentSet civilian="true">    <!-- ⚠️ needs equipmentType="Civilian" -->
        <Equipment slot="Item0" id="Item.wm_gondor_sword_a01" />
        <Equipment slot="Body" id="Item.ithilien_jerkin_short" />
        <Equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
    </EquipmentSet>
</EquipmentRoster>
```

**Target state:** Mechanical `civilian="true" → equipmentType="Civilian"` via the migrate tool. **No `<Flags>` element needed** — wanderer rosters are selected by ID lookup in code (`MBObjectManager.GetObject<MBEquipmentRoster>("npc_companion_equipment_template_gondor")`), not by template resolution. Vanilla 1.4.5 wanderer rosters confirm this pattern (`npc_wanderer_equipment_template_battania` has no `<Flags>`).

### `taom_char_creation_equipment.xml`

**Current state:** 14,102 lines, 204 occurrences of `<EquipmentSet civilian="true">`. No deprecated flags. No `<Flags>` element.

**Issue:** Char-creation parent (`mother_char_creation_*`, `father_char_creation_*`) rosters should have `<Flags IsFemaleTemplate="true" />` on mother rosters per vanilla pattern (`SandBox/sandbox_equipment_sets.xml` line 6469 — `father_char_creation_retainer_empire` has `<Flags IsFemaleTemplate="true" />`).

**Wait — vanilla 1.4.5 has `IsFemaleTemplate="true"` on the FATHER roster.** This is either a vanilla typo, or "father" rosters in vanilla CC actually mean "for father-occupation NPCs (female peer/companion)". Verify against `StoryModeCharacterCreationContent` in S0 decompile before committing changes.

**Target state for the mechanical pass:** `civilian="true" → equipmentType="Civilian"` (handled by migrate tool). `<Flags>` authoring deferred until S5b after verifying vanilla intent.

### `taom_career_starting_equipment.xml`

**Current state:** 98 lines, 0 deprecated patterns. 6 rosters (Gondor archetype × male/female). All `<EquipmentSet>` omit `equipmentType` → default Battle (correct: career starting gear is battle equipment).

**Issue:** No `<Flags>` element. These are career starter overlays consumed by `CareerStartingEquipmentService` (Gondor only authored; 15 other cultures fall through to culture-default). They're selected by ID lookup, not template resolution, so **no `<Flags>` element needed**.

**Target state:** No changes required. File is 1.4.5-compatible as-is.

### `taom_education_equipment_templates.xml`

**Current state:** 10,154 lines, NO deprecated flags, NO `<EquipmentSet civilian=...>`. Selected by `EducationCampaignBehavior` via roster ID lookup.

**Target state:** ~~No changes required.~~ **Corrected 2026-06-22:** flags/`equipmentType` need no migration (vanilla `SandBox/education_equipment_templates.xml` confirms education rosters have no `<Flags>` and no `equipmentType=`), BUT the file was missing the required `culture="Culture.<id>"` on all 980 `<EquipmentRoster>` elements (980 startup warnings). Fixed via `tools/add_education_roster_cultures.py`. No further changes required.

### `taom_equipment_sets_named_companions.xml`

**Current state:** 5 lines, stub file with comment "Named companion equipment is defined inline in named_companions.xml — this file exists for future per-companion equipment roster overrides."

**Target state:** When/if populated, named companions (Aragorn, Legolas, etc.) should use:
- For standalone rosters: `<EquipmentRoster id="companion_eq_aragorn" culture="Culture.gondor">` with `<Flags IsLordTemplate="true" />` (they're hero-tier characters).
- The actual companion characters in `named_companions.xml` use inline `<EquipmentRoster civilian="true">` (Pattern B above) — which is still valid in 1.4.5, no change needed.

---

## Mandatory per-culture matrix

`tools/audit_equipment_roster_coverage.py` audits TAOM's 12 custom cultures against this required matrix. Per spec ([v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md)):

### Mandatory rosters (8 per culture × 12 cultures = 96 required)

| # | Flag combination | equipmentType | Purpose |
|---|---|---|---|
| 1 | `IsLordTemplate` | `Battle` | Male lord battle |
| 2 | `IsLordTemplate` + `IsFemaleTemplate` | `Battle` | Female lord battle |
| 3 | `IsLordTemplate` | `Civilian` | Male lord civilian |
| 4 | `IsLordTemplate` + `IsFemaleTemplate` | `Civilian` | Female lord civilian |
| 5 | `IsLordTemplate` + `IsChildEquipmentTemplate` | `Civilian` | Male lord child |
| 6 | `IsLordTemplate` + `IsChildEquipmentTemplate` + `IsFemaleTemplate` | `Civilian` | Female lord child |
| 7 | `IsLordTemplate` + `IsTeenagerEquipmentTemplate` | `Civilian` | Male lord teen |
| 8 | `IsLordTemplate` + `IsTeenagerEquipmentTemplate` + `IsFemaleTemplate` | `Civilian` | Female lord teen |

### Optional but recommended for kingdom-tier cultures (4 per culture × 12 cultures = 48 optional)

| # | Flag combination | equipmentType | Purpose |
|---|---|---|---|
| 9 | `IsKingdomRulerTemplate` | `Battle` | Male king battle |
| 10 | `IsKingdomRulerTemplate` + `IsFemaleTemplate` | `Battle` | Female queen battle |
| 11 | `IsKingdomRulerTemplate` | `Civilian` | Male king civilian |
| 12 | `IsKingdomRulerTemplate` + `IsFemaleTemplate` | `Civilian` | Female queen civilian |

### Current TAOM coverage (audit output 2026-05-22)

| Culture | Mandatory pass | Mandatory miss | Optional pass | Optional miss | Status |
|---|---|---|---|---|---|
| erebor | 0 | 8 | 0 | 4 | FAIL |
| rivendell | 0 | 8 | 0 | 4 | FAIL |
| mirkwood | 0 | 8 | 0 | 4 | FAIL |
| lothlorien | 0 | 8 | 0 | 4 | FAIL |
| isengard | 0 | 8 | 0 | 4 | FAIL |
| gundabad | 0 | 8 | 0 | 4 | FAIL |
| umbar | 0 | 8 | 0 | 4 | FAIL |
| dolguldur | 0 | 8 | 0 | 4 | FAIL |
| gondor | 0 | 8 | 0 | 4 | FAIL |
| mordor | 0 | 8 | 0 | 4 | FAIL |
| shaghana | 0 | 8 | 0 | 4 | FAIL |
| abanissa | 0 | 8 | 0 | 4 | FAIL |
| **Total** | **0/96** | **96** | **0/48** | **48** | — |

**100% of mandatory rosters missing** — because TAOM rosters lack the `<Flags>` element entirely. Once Phase 1 mechanical migration adds `<Flags>` to the existing rosters, this audit will pass for the configurations that already exist. Net new authoring required: probably ~40 rosters (child/teen/female-lord/ruler) per culture — see S5b authoring queue.

Note: cultures `shaghana` and `abanissa` are nominally Aserai sub-cultures (per `memory/feedback_classify_by_grep_not_by_assumption.md`); their LotR mapping to a specific kingdom is tracked elsewhere. Treat them as full first-class cultures for the matrix.

---

## Migration recipes per TAOM file

### Phase 1 — Mechanical migration (run the tools)

```bash
# 1. Verify current state
python tools/validate_equipment_flags_1_4_3.py
python tools/audit_equipment_roster_coverage.py

# 2. Dry-run the migrate tool to see proposed diffs
python tools/migrate_equipment_type_1_4_3.py --dry-run --path Main/_Module/ModuleData/equipmentsets

# 3. Apply mechanical migration (civilian="true" → equipmentType="Civilian" on EquipmentSet)
python tools/migrate_equipment_type_1_4_3.py --apply --path Main/_Module/ModuleData/equipmentsets

# 4. Manually fix the deprecated EquipmentFlags values in taom_child_equipment_templates.xml
#    (per the mapping table above — no tool yet for this; TODO: extend migrate tool)

# 5. Re-validate
python tools/validate_equipment_flags_1_4_3.py
# expected: 0 hits
```

### Phase 2 — Add missing `<Flags>` elements (manual / scripted)

For each `taom_equipment_sets_<culture>.xml`:
- Battle rosters (`<id>_bat_template_*`): add `<Flags IsLordTemplate="true" />` before `</EquipmentRoster>`.
- Civilian rosters (`<id>_civ_template_*`): add `equipmentType="Civilian"` on each `<EquipmentSet>` + `<Flags IsLordTemplate="true" />` before `</EquipmentRoster>`.

### Phase 3 — Author missing matrix rosters (S5b)

For each culture × missing-combo from the audit, author a new `<EquipmentRoster>` using LOTRLOME items. Cross-reference against `tools/validate_gondor_refs.py` (extend to all cultures) to avoid the underwear bug.

---

## Common pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| Engine logs `EquipmentSet 'civilian' attribute is deprecated, use 'equipmentType' instead.` | `civilian="true"` still present on `<EquipmentSet>` | Run `migrate_equipment_type_1_4_3.py --apply`. |
| Engine logs `EquipmentRoster '<id>' has no culture attribute.` | Missing `culture=` on `<EquipmentRoster>` | Add `culture="Culture.<id>"` — required in 1.4.3+. |
| Engine logs `Equipment flag 'IsNobleTemplate' is no longer supported.` | Deprecated flag still in `<Flags>` | Apply the mapping table above. |
| NPC appears naked / underwear | Missing roster for that NPC's (culture, gender, age, lord-or-not) combination → engine falls back to neutral and item IDs don't resolve | Author the missing roster from the matrix; cross-reference items via `validate_gondor_refs.py`. |
| Lord aging into adulthood spawns with wrong-culture clothes | Missing `IsLordTemplate` + `IsTeenagerEquipmentTemplate` roster for that culture | Author both teen variants (M/F) per the matrix. |
| King/Queen identical to other lords (no crown swap) | Missing `IsKingdomRulerTemplate` rosters | Author the 4 optional ruler rosters per kingdom-tier culture. |
| Vanilla 1.4.5 `<EquipmentRoster civilian="true">` inside `<NPCCharacter>/<Equipments>` triggers no warning | This is Pattern B (inline roster) — `civilian="true"` is STILL valid there | Leave it alone. The migrate tool already skips this. |

---

## Cross-references

| Document | Purpose |
|---|---|
| [v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md) | The dev migration notes (source of truth for the change). |
| [v1.4.x-overview.md](../v1.4.x-overview.md) | Migration master plan. |
| [v1.4.x-taom-impact.md](../v1.4.x-taom-impact.md) | Surface impact matrix. |
| [v1.4.x-changes.md](../v1.4.x-changes.md) | Full TaleWorlds changelog analysis. |
| [equipment-roster-coverage.csv](../equipment-roster-coverage.csv) | Per-culture audit output (generated by `audit_equipment_roster_coverage.py`). |
| `tools/migrate_equipment_type_1_4_3.py` | Mechanical XML migration tool. |
| `tools/audit_equipment_roster_coverage.py` | Per-culture mandatory-matrix audit. |
| `tools/validate_equipment_flags_1_4_3.py` | Deprecated-flag scanner. |
| `tools/generate_char_creation_equipment.py` | Author tool — extend to emit `equipmentType="Civilian"` on output. |
| `Main/Adapters/ChildCreatorAdapter.cs:40` | Only direct TAOM consumer of `GetEquipmentRostersForInitialChildrenGeneration` (return-type change). |

---

## Sources

All vanilla examples in this document are quoted directly from the installed 1.4.5 game files at:

- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\sandbox_equipment_sets.xml` (16,807 lines)
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBoxCore\ModuleData\sandboxcore_equipment_sets.xml` (16,837 lines)
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\education_equipment_templates.xml` (8,471 lines)
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\StoryMode\ModuleData\story_mode_equipments.xml` (352 lines)

Counts verified via Grep on 2026-05-22.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/migration/templates/characters.md](./characters.md)
- [docs/migration/templates/README.md](./README.md)

<!-- backlinks-end -->
