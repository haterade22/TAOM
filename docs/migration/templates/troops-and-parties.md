# Troop & Party Template XML Templates (v1.4.5 reference)

> The canonical reference for "what right looks like" for troop and party template XMLs in Bannerlord v1.4.5. Companion to `characters.md` and `equipment-rosters.md`.
>
> **Source surveyed (2026-05-22):**
> - `SandBoxCore/spnpccharactertemplates.xml` (6,153 lines — character templates, used by code lookup)
> - `SandBoxCore/spnpccharacters.xml` (the main vanilla troop file — `imperial_recruit` etc.)
> - `SandBoxCore/sandboxcore_equipment_sets.xml` (reusable EquipmentSet roster definitions)
> - `SandBox/bandits.xml` (4,200 lines — looter / sea_raiders / etc.)
> - `SandBox/spgenericcharacters.xml` (511 lines — `guard_empire` etc.)
> - `SandBox/spspecialcharacters.xml` (wanderers — `spc_wanderer_empire_0` etc.)
> - `SandBox/partyTemplates.xml` (1,792 lines — bandit + lord party templates)
> - `SandBox/caravans.xml` (caravan leaders + escort)
> - `SandBox/education_character_templates.xml` (child education branch templates)
> - `StoryMode/story_mode_party_templates.xml` (conspiracy parties)
>
> **TAOM surveyed:**
> - 13 troop files in `Main/_Module/ModuleData/troops/` — **700 troops total**
> - `taom_partyTemplates.xml` — **288 party templates**
> - 18 NPC files in `Main/_Module/ModuleData/characters/` — notable merchants / gang leaders / headmen
> - `taom_education_character_templates.xml` — child stage templates per culture
> - `taom_wanderers.xml` — TAOM wanderer NPCs

---

## Overview

Three element types are involved:

1. **`<NPCCharacter>`** — defines a troop (soldier with `occupation="Soldier"` or `"Bandit"`), notable NPC (merchant / gang leader), wanderer, hero, or template. Container is `<NPCCharacters>` root.
2. **`<MBPartyTemplate>`** — defines the composition of a party (looters, lord retinue, caravan escort) as a set of `<PartyTemplateStack>` rules. Container is `<partyTemplates>` root.
3. **`<EquipmentRoster id="..." culture="...">`** — defines a reusable equipment loadout. Lives in `*_equipment_sets.xml`. Each `<NPCCharacter>` can either inline its rosters or reference these by ID.

Troops always use inline `<EquipmentRoster>` blocks (plus optional `<EquipmentSet id="..." />` reference to a civilian template). Party templates only reference troops by NPCCharacter ID — they never define equipment.

---

## Troop NPCCharacter anatomy

### Vanilla 1.4.5 example — basic troop (`imperial_recruit`)

From `SandBoxCore/spnpccharacters.xml`:

```xml
<NPCCharacter
    id="imperial_recruit"
    default_group="Infantry"
    level="6"
    name="{=s3IJIFUw}Imperial Recruit"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.empire">
    <face>
        <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
        <skill id="Athletics" value="20" />
        <skill id="Riding" value="0" />
        <skill id="OneHanded" value="20" />
        <skill id="TwoHanded" value="10" />
        <skill id="Polearm" value="20" />
        <skill id="Bow" value="5" />
        <skill id="Crossbow" value="5" />
        <skill id="Throwing" value="10" />
    </skills>
    <upgrade_targets>
        <upgrade_target id="NPCCharacter.imperial_infantryman" />
        <upgrade_target id="NPCCharacter.imperial_archer" />
    </upgrade_targets>
    <Equipments>
        <EquipmentRoster>
            <equipment slot="Item0" id="Item.peasant_pitchfork_2_t1" />
            <equipment slot="Head"  id="Item.leather_cap" />
            <equipment slot="Body"  id="Item.tunic_with_shoulder_pads" />
            <equipment slot="Leg"   id="Item.empire_horseman_boots" />
        </EquipmentRoster>
        <!-- ... 3 more EquipmentRoster blocks for variety ... -->
        <EquipmentSet
            id="empire_troop_civilian_template_t1"
            equipmentType="Civilian" />
    </Equipments>
</NPCCharacter>
```

### Vanilla 1.4.5 example — bandit with civilian roster (`looter`)

From `SandBox/bandits.xml` (note: bandits keep `civilian="true"` on `<EquipmentRoster>` — see "What changed" below):

```xml
<NPCCharacter
    id="looter"
    default_group="Infantry"
    name="{=urUwa4bf}Looter"
    level="6"
    occupation="Bandit"
    culture="Culture.looters">
    <face>
        <face_key_template value="BodyProperty.looter" />
    </face>
    <skills>
        <skill id="Athletics" value="30" />
        <skill id="OneHanded" value="40" />
        <!-- ... -->
    </skills>
    <upgrade_targets>
        <upgrade_target id="NPCCharacter.imperial_infantryman" />
    </upgrade_targets>
    <Equipments>
        <EquipmentRoster civilian="true">
            <equipment slot="Item0" id="Item.peasant_2haxe_1_t1" />
            <equipment slot="Body"  id="Item.bandit_envelope_dress_v1" />
            <equipment slot="Leg"   id="Item.wrapped_shoes" />
        </EquipmentRoster>
        <EquipmentRoster>
            <!-- battle variant -->
        </EquipmentRoster>
    </Equipments>
</NPCCharacter>
```

### Required attributes on `<NPCCharacter>`

| Attribute | Required | Type | Notes |
|---|---|---|---|
| `id` | **Yes** | string | Globally unique. Save-compat: NEVER change once shipped. |
| `name` | **Yes** | localized | `{=key}Display Name` form. |
| `culture` | **Yes** | `Culture.<id>` | Must resolve to a culture defined elsewhere. |
| `occupation` | **Yes** | enum | `Soldier`, `Bandit`, `Wanderer`, `Lord`, `Merchant`, `CaravanGuard`, `ArenaMaster`, `GangLeader`, `Villager`, `Townsfolk`, `RuralNotable`, `Headman`, `Preacher`, `Artisan`. |
| `default_group` | **Yes** | enum | `Infantry`, `Ranged`, `Cavalry`, `HorseArcher`. Determines which formation the troop spawns into. |
| `level` | **Yes** for combat troops | int | Approximately tier-correlated. T1=6, T2=11, T3=16, T4=21, T5=26, T6=31, T7=36. |
| `is_basic_troop` | Optional | `"true"` | **Marks a troop as the recruitment entry point** for a culture. Vanilla `imperial_recruit`, TAOM `gondor_loss_lumberman` etc. Set per culture: typically the lowest-tier troop per recruitment line. |
| `is_hero` | Optional | bool | Heroes only. `false` for templates / generic troops; absent on most troops. |
| `is_template` | Optional | `"true"` | Used by `child_education_templates_*` and other prototype rows. Templates are not spawned directly. |
| `is_mercenary` | Optional | bool | Caravan leaders. |
| `is_hidden_encyclopedia` | Optional | `"true"` | Hide from encyclopedia (guards, internal use). |
| `age` | Optional | int | Used on lord / template rows (e.g. `age="45"` on `child_education_templates_stage_2`). |
| `voice` | Optional | string | `curt`, `earnest`, etc. — only for unique wanderers/heroes. |
| `formation_position_preference` | Optional | enum | `Front`, `FrontMiddle`, `Middle`, `Rear`. Used on test templates and some special troops. |
| `skill_template` | Optional | `SkillSet.<id>` | Inherits a pre-defined skill profile. Either `skill_template` OR `<skills>` is used — not both for serious troops. |
| `race` | Optional (TAOM-specific) | string | TAOM only: `goblin`, `orc`, `dg_uruk`, etc. for non-human troops. Not present in vanilla. |

### What changed in v1.4.3 (relevant to troops)

Per `docs/migration/v1.4.x-equipment-overhaul.md`:

| Old (1.3.15) | New (1.4.5) | Where this applies | TAOM impact |
|---|---|---|---|
| `<EquipmentSet ... civilian="true" />` (reference inside a troop's `<Equipments>`) | `<EquipmentSet ... equipmentType="Civilian" />` | Inside `<NPCCharacter>/<Equipments>`, referencing a reusable roster | **2017 occurrences across 17 TAOM files** — mechanical migration |
| `<EquipmentRoster civilian="true">` (inline roster on a troop) | **Unchanged** — still valid | Inside `<NPCCharacter>/<Equipments>` | No change needed |
| `<EquipmentSet civilian="true">` block (definition inside `*_equipment_sets.xml`) | `<EquipmentSet equipmentType="Civilian">` | Inside `<EquipmentRoster>` in `sandboxcore_equipment_sets.xml` and TAOM `equipmentsets/*.xml` | Verify each TAOM `equipmentsets/*.xml` |
| `civilianTemplate=` / `battleTemplate=` on `<CharacterObject>` | Removed entirely | Was on character XML | **0 occurrences in TAOM** — no action needed |
| Removed EquipmentFlags (13 flags incl. `IsNoncombatantTemplate`, `IsLordEquipment`, etc.) | New 5-flag set (`IsLordTemplate`, `IsFemaleTemplate`, `IsChildEquipmentTemplate`, `IsTeenagerEquipmentTemplate`, `IsKingdomRulerTemplate`) | `<EquipmentFlags>` element on `<EquipmentRoster>` in `*_equipment_sets.xml` | TAOM has 1 file (`taom_child_equipment_templates.xml`) using deprecated flag names |
| `EquipmentSet` without `culture=` attribute on the roster wrapper | Logs load warning | The wrapper `<EquipmentRoster id="..." culture="...">` in `*_equipment_sets.xml` | Audit needed |
| Bare `civilian="true"` on `<EquipmentSet>` inside `*_equipment_sets.xml` | `equipmentType="Civilian"` | Same place as above | See above |

The single critical pattern change: **the attribute name `civilian="true"` is deprecated on `<EquipmentSet>` but NOT on `<EquipmentRoster>`**. Vanilla 1.4.5 still has 1,097 instances of `<EquipmentRoster civilian="true">` in `SandBoxCore/spnpccharacters.xml` — it remains the valid form for inline rosters.

### Equipment binding — three forms

A troop's `<Equipments>` element accepts a mix of three child kinds:

**1. Inline battle EquipmentRoster** — defines a battle loadout right here:
```xml
<EquipmentRoster>
    <equipment slot="Item0" id="Item.imperial_spear_t2" />
    <equipment slot="Head"  id="Item.leather_cap" />
    <equipment slot="Body"  id="Item.legionary_mail" />
    <equipment slot="Leg"   id="Item.leather_boots" />
</EquipmentRoster>
```

**2. Inline civilian EquipmentRoster** — same but for civilian use (visiting town, etc.):
```xml
<EquipmentRoster civilian="true">
    <equipment slot="Body" id="Item.tunic_with_shoulder_pads" />
    <equipment slot="Leg"  id="Item.empire_horseman_boots" />
</EquipmentRoster>
```

**3. Reference to a reusable EquipmentSet (1.4.5 form)** — pulls in a roster defined in `*_equipment_sets.xml`:
```xml
<EquipmentSet
    id="empire_troop_civilian_template_t1"
    equipmentType="Civilian" />
```

**Allowed `equipmentType` values** (enum — schema-enforced, can NOT be combined):
- `"Battle"`
- `"Civilian"`
- `"Stealth"` *(new in 1.4.3)*

Note: the lowercased `<equipment>` child uses lowercase `<equipment slot="..." id="..." />`, while the reusable EquipmentSet definition file uses capitalized `<Equipment>`. The capitalization is preserved exactly by vanilla — don't normalize.

### Troop upgrade chain

```xml
<upgrade_targets>
    <upgrade_target id="NPCCharacter.imperial_infantryman" />
    <upgrade_target id="NPCCharacter.imperial_archer" />
</upgrade_targets>
```

- Empty `<upgrade_targets></upgrade_targets>` is valid for terminal troops (T6+ and named guards).
- Multiple `<upgrade_target>` children fan the upgrade tree.
- Target IDs reference other troops by `NPCCharacter.<id>` namespace. Dead-end references silently break the upgrade UI.
- **Save-compat:** orphaning a troop (removing from all upgrade trees) is safe; deleting the `<NPCCharacter>` is not.

### Skills vs skill_template

Either:
- `<skills>` with explicit `<skill id="X" value="Y" />` children, OR
- `skill_template="SkillSet.<id>"` attribute referencing a pre-defined set in `skill_sets.xml`.

Vanilla `imperial_recruit` uses explicit `<skills>`. Vanilla `guard_empire` uses both `skill_template="SkillSet.infantry_heavyinfantry_level21_template_skills"` AND `<skills>` (the explicit values override). TAOM uses explicit `<skills>` exclusively for combat troops, and `skill_template` only on notable / template rows.

### Face

Always include `<face>` with one child:

```xml
<face>
    <face_key_template value="BodyProperty.fighter_empire" />
</face>
```

For unique heroes (caravan leaders, named companions), use full `<BodyProperties>`:

```xml
<face>
    <BodyProperties version="4" age="57.06" weight="0.3881" build="0.3792"
        key="001C5C0E563C..." />
    <BodyPropertiesMax version="4" age="57.06" weight="0.3881" build="0.3792"
        key="001C5C0E563C..." />
    <hair_tags>   <hair_tag   name="aserai" /> </hair_tags>
    <beard_tags>  <beard_tag  name="aserai" /> </beard_tags>
    <tattoo_tags> <tattoo_tag name="Cleanface" /> </tattoo_tags>
</face>
```

---

## Party templates

### Vanilla 1.4.5 example — looters

From `SandBox/partyTemplates.xml`:

```xml
<MBPartyTemplate id="looters_template">
    <stacks>
        <PartyTemplateStack
            min_value="4"
            max_value="36"
            troop="NPCCharacter.looter" />
    </stacks>
</MBPartyTemplate>
```

### Vanilla 1.4.5 example — sea raiders (multi-tier)

```xml
<MBPartyTemplate id="sea_raiders_template">
    <stacks>
        <PartyTemplateStack min_value="2" max_value="27" troop="NPCCharacter.sea_raiders_bandit" />
        <PartyTemplateStack min_value="0" max_value="12" troop="NPCCharacter.sea_raiders_raider" />
        <PartyTemplateStack min_value="0" max_value="6"  troop="NPCCharacter.sea_raiders_chief" />
    </stacks>
</MBPartyTemplate>
```

### Required attributes on `<MBPartyTemplate>`

| Attribute | Required | Notes |
|---|---|---|
| `id` | **Yes** | Unique. Referenced by code (e.g. `MBObjectManager.GetObject<PartyTemplateObject>("looters_template")`) and by faction XML (`taom_spclans.xml` patrol templates). |

### Required attributes on `<PartyTemplateStack>`

| Attribute | Required | Notes |
|---|---|---|
| `min_value` | **Yes** | Minimum count of this troop in spawned parties. |
| `max_value` | **Yes** | Maximum count. Reached only when the party's single spawn ratio draws near 1.0 (see the semantics below). |
| `troop` | **Yes** | `NPCCharacter.<id>` reference. Must resolve to a real troop. |

### Stack composition semantics

- **One ratio is drawn per party, not per stack.**
  `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` (v1.4.8 decompile,
  `:427-464`) computes the ratio `r` once at `:430`, before the loop, then applies the same `r` to
  every stack at `:442`: `count = RoundRandomized(min + (max - min) * r)`. One draw moves the whole
  roster together, so a party is uniformly small or uniformly large across all its stacks. Stacks are
  **not** independently sampled; the only per-stack randomness is `RoundRandomized` on the fractional
  result.
- For a kingdom lord party, `r` is `party.RandomFloat()` (`:412`): roughly uniform on `(0, 1)` and
  completely independent of the template. Bandits, player-owned caravans and patrol parties take
  different branches of `GetInitialPartySizeRatioForMobileParty` (`:390-413`).
- `min=0` still makes a stack optional, because a low `r` rounds that stack to 0.
- Min/max counts are absolute, not ratios. A party with two stacks of `min=10, max=10` spawns exactly 20 troops.
- Stack ordering doesn't affect spawn behavior but is convention-ordered low-tier-to-high-tier for readability.
- **The `max_value` sum is the SPAWN ceiling, not the party's size.** Because one shared `r` drives
  every stack, a *lord* party's expected spawn roster is the midpoint of the min sum and the max sum.
  Patrol parties and the player's own caravans are the exception: those branches return `1f` (`:410`,
  `:406`), so they spawn the max sum exactly. Steady-state size belongs to `PartySizeLimit`, a
  separate model: a party spawned above its limit cannot recruit, and
  `DefaultPartyDesertionModel.GetTroopsToDesertDueToWageAndPartySize` (`:50-76`) sheds a quarter of
  the excess on every daily party tick until it is back under.
  `PartyTemplateObject.GetUpperTroopLimit()` / `GetLowerTroopLimit()` are plain sums with no caller
  anywhere in the Campaign assembly, though the shipped NavalDLC module does call both. Full engine
  walkthrough, including the new-game top-up that reads the stacks a second time:
  [`docs/reference/party-template-sizing.md`](../../reference/party-template-sizing.md).

### v1.4.0 party visibility note

Per dev notes: in 1.4.0 the engine started factoring party size **plus terrain** into spotting checks. This does NOT change the XML schema, but it does change the gameplay feel of small bandit parties (they hide better in forests/hills). No action needed in TAOM party templates, but consider tuning `min_value` floors on lookout-type parties if visibility feels off post-migration.

### TAOM party-template inventory (288 templates)

Per `.claude/rules/troops.md`, each TAOM culture should have these templates:

| Template ID pattern | Purpose |
|---|---|
| `kingdom_hero_party_{culture}_template` | Lord retinue (full T1-T9 range) |
| `kingdom_hero_party_mercenary_{culture}_template` | Mercenary band (mid-tier professional) |
| `kingdom_hero_party_outlaw_{culture}_template` | Outlaw / raider mix |
| `patrol_party_{culture}_template_level_1` | Weak patrol |
| `patrol_party_{culture}_template_level_2` | Medium patrol |
| `patrol_party_{culture}_template_level_3` | Elite patrol |
| `rebels_{culture}_template` | Uprising parties |
| `vassal_reward_troops_{culture}` | Granted on vassalage |
| `militia_{culture}_template` | Town garrison |
| `villager_{culture}_template` | Village trade caravans |
| `caravan_template_{culture}` | Player-owned caravan |
| `elite_caravan_template_{culture}` | Upgraded caravan |

---

## NPC character templates (SandBoxCore/spnpccharactertemplates.xml)

These are **slot-based mappings** — abstract "face N" templates referenced by the face-gen system. They never spawn directly.

### Vanilla 1.4.5 example

```xml
<NPCCharacter
    id="facgen_template_test_char_0"
    is_template="true"
    default_group="Infantry"
    formation_position_preference="Front"
    level="11"
    culture="Culture.empire"
    occupation="Townsfolk"
    name="{=!}Serdar"
    skill_template="SkillSet.infantry_heavyinfantry_level11_template_skills">
    <face>
        <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <upgrade_targets></upgrade_targets>
    <Equipments>
        <EquipmentRoster civilian="true" />
        <EquipmentRoster />
    </Equipments>
</NPCCharacter>
```

Notes:
- Always `is_template="true"`.
- Equipment rosters are **empty** — slot exists but is filled at spawn time via the face/skin system.
- `name="{=!}Serdar"` uses `{=!}` (non-localized) placeholder — these are internal IDs not displayed to players.

### How they're consumed

TaleWorlds code looks up templates by ID (e.g. `MBObjectManager.GetObject<CharacterObject>("facgen_template_test_char_0")`) during faceregen and clan member generation. The slot count + culture coverage matters; the names don't.

**TAOM does not currently override or extend `spnpccharactertemplates.xml`** — confirmed no `taom_npccharactertemplates.xml` exists. If you need to author per-culture templates for the LOTR cultures, model after the vanilla format.

---

## Education character templates (SandBox/education_character_templates.xml)

These templates back the "child-raising" / coming-of-age system. The TAOM equivalent is `taom_education_character_templates.xml`.

### Vanilla 1.4.5 example

```xml
<NPCCharacter
    id="child_education_templates_stage_2_page_0_branch_0_empire"
    name="{=!}stage_2_page_0_branch_0_empire"
    age="45"
    default_group="Infantry"
    is_hero="false"
    occupation="Lord"
    culture="Culture.empire"
    skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"
    is_template="true">
    <face>
        <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills />
    <Equipments>
        <EquipmentRoster>
            <equipment slot="Head" id="Item.ironlame_feathered_spangenhelm" />
            <equipment slot="Cape" id="Item.imperial_studded_strip_shoulders" />
            <equipment slot="Body" id="Item.legionary_mail" />
            <equipment slot="Gloves" id="Item.woven_leather_bracers" />
            <equipment slot="Leg" id="Item.decorated_imperial_boots" />
        </EquipmentRoster>
        <EquipmentRoster civilian="true" />
    </Equipments>
</NPCCharacter>
```

Key markers:
- `is_template="true"`, `is_hero="false"`.
- `age="45"` (lord age for branch outcomes).
- ID pattern: `child_education_templates_stage_<n>_page_<p>_branch_<b>_<culture>`.
- One battle `<EquipmentRoster>` with full body coverage, one civilian placeholder `<EquipmentRoster civilian="true" />`.

The civilian roster being empty is a vanilla convention — it tells the engine to fall through to the culture's default civilian roster.

---

## TAOM current state per file

### `Main/_Module/ModuleData/troops/troops_gondor.xml` (179 troops, 7,205 lines)

**Representative troop** (line 6-69, T1 basic troop):

```xml
<NPCCharacter
    id="gondor_loss_lumberman"
    default_group="Infantry"
    level="6"
    name="{=aom_gondor_loss_lumberman_name}[Gondor] Lossarnach Lumberman"
    occupation="Soldier"
    is_basic_troop="true"
    culture="Culture.gondor">
    <face>
        <face_key_template value="BodyProperty.fighter_empire" />
    </face>
    <skills>
        <skill id="Athletics" value="38" />
        <!-- ... -->
    </skills>
    <upgrade_targets>
        <upgrade_target id="NPCCharacter.gondor_loss_woodsman" />
    </upgrade_targets>
    <Equipments>
        <EquipmentRoster>
            <equipment slot="Item0" id="Item.wm_gondor_lossarnach_1h_axe_a" />
            <equipment slot="Item1" id="Item.gond_shield_one_greyscale" />
            <equipment slot="Body" id="Item.sk_gd_los_inf_chainmail_a" />
            <equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
        </EquipmentRoster>
        <!-- 5 more EquipmentRoster variants -->
        <EquipmentSet id="battania_troop_civilian_template_t2" civilian="true" />
        <!-- ⚠️ 1.4.3 DEPRECATED — needs equipmentType="Civilian" -->
    </Equipments>
</NPCCharacter>
```

**Issues vs vanilla 1.4.5:**
- **178 occurrences** of `<EquipmentSet ... civilian="true" />` — mechanical migration to `equipmentType="Civilian"` required.
- `face_key_template value="BodyProperty.fighter_empire"` on Gondor troops — fine; Gondor inherits empire body templates. Audit: should TAOM ship `BodyProperty.fighter_gondor`? Out of scope for this migration.
- `<EquipmentSet id="battania_troop_civilian_template_t2" ...>` — references a vanilla SandBoxCore template by ID. Cross-check: still resolves in 1.4.5 (battania_troop_civilian_template_t2 exists at spnpccharacters.xml line 7585).

### `Main/_Module/ModuleData/troops/troops_rhun_new.xml` (117 troops, 4,847 lines)

Same pattern as Gondor. `EquipmentSet id="aserai_troop_civilian_template_t3" civilian="true"` — **116 occurrences** to migrate. References vanilla aserai templates (Khand maps to khuzait, Rhûn troops inherit aserai civilian template — that's the design intent).

### Per-troop-file civilian="true" census

(From `docs/migration/v1.4.x-equipment-overhaul.md` grep, validated 2026-05-22 — these counts are *all* `civilian="true"` including both `<EquipmentRoster>` AND `<EquipmentSet>` forms; the migration tool needs to migrate only the `<EquipmentSet>` cases.)

| File | civilian="true" count | Action |
|---|---|---|
| `troops_gondor.xml` | 178 | Migrate `<EquipmentSet>` cases |
| `troops_rhun_new.xml` | 116 | Same |
| `troops_dolguldur.xml` | 57 | Same |
| `troops_rohan.xml` | 41 (EquipmentSet) + 57 total | Same |
| `troops_dunland.xml` | 45 | Same |
| `troops_isengard.xml` | 7 (EquipmentSet) + 38 total | Same |
| `troops_harad.xml` | 27 (EquipmentSet) + 27 total | Same |
| `troops_gundabad.xml` | 26 (EquipmentSet) + 30 total | Same |
| `troops_erebor.xml` | 16 (EquipmentSet) + 45 total | Same |
| `troops_mordor.xml` | 9 (EquipmentSet) + 28 total | Same |
| `troops_mirkwood.xml` | 13 (EquipmentSet) + 17 total | Same |
| `troops_rivendell.xml` | 24 total — verify | Same |
| `troops_umbar.xml` | 14 total — verify | Same |

### `Main/_Module/ModuleData/taom_partyTemplates.xml` (288 templates)

**Representative template** (line 28-47, kingdom hero party):

```xml
<MBPartyTemplate id="kingdom_hero_party_gundabad_template">
    <stacks>
        <PartyTemplateStack min_value="10" max_value="30" troop="NPCCharacter.gundabad_snaga" />
        <PartyTemplateStack min_value="8"  max_value="20" troop="NPCCharacter.gundabad_grunt" />
        <PartyTemplateStack min_value="8"  max_value="20" troop="NPCCharacter.gundabad_hunter" />
        <!-- ... 13 more stacks ... -->
        <PartyTemplateStack min_value="1"  max_value="3"  troop="NPCCharacter.gundabad_dread_rider_of_the_tower" />
    </stacks>
</MBPartyTemplate>
```

**Vanilla comparison:** schema is identical. Same `<partyTemplates>` root, same `<MBPartyTemplate id>/<stacks>/<PartyTemplateStack>` hierarchy.

**Issues:**
- **No schema migration needed.** Party templates are unaffected by the 1.4.3 equipment overhaul.
- **Reference integrity** — every `troop="NPCCharacter.<id>"` must resolve to a troop in the matching `troops_<culture>.xml`. Run a cross-reference check as part of the migration; an orphaned reference silently spawns nothing.
- **v1.4.0 party visibility** affects gameplay feel only — no XML change.

### `Main/_Module/ModuleData/characters/npcs_gondor.xml` (77 lines of `civilian="true"`)

**Representative NPC** (line 8-17, Arena Master):

```xml
<NPCCharacter id="tournament_master_gondor" default_group="Infantry"
        is_hero="false" occupation="ArenaMaster" culture="Culture.gondor"
        name="{=aom_gd_arena}Arena Master">
    <face><face_key_template value="BodyProperty.fighter_gondor" /></face>
    <skills></skills>
    <Equipments>
        <EquipmentRoster civilian="true">
            <equipment slot="Body" id="Item.gondor_noble_coat_a" />
            <equipment slot="Leg"  id="Item.sk_gd_ano_boots_a" />
        </EquipmentRoster>
    </Equipments>
</NPCCharacter>
```

**Comparison with vanilla `spnpccharacters.xml`:**
- Same `<NPCCharacter>` element, same nested structure.
- TAOM uses `<EquipmentRoster civilian="true">` — this is the **inline roster form**, still valid in 1.4.5. No migration needed for these.
- 77 occurrences of `civilian="true"` in `npcs_gondor.xml` — verify the breakdown of `<EquipmentRoster>` vs `<EquipmentSet>` cases. The migration tool must only touch `<EquipmentSet>`.

### `Main/_Module/ModuleData/taom_education_character_templates.xml` (60 lines of `civilian="true"`)

**Representative template** (line 4-31):

```xml
<NPCCharacter
    id="child_education_templates_stage_2_page_0_branch_0_gundabad"
    name="{=!}stage_2_page_0_branch_0_gundabad"
    age="45"
    default_group="Infantry"
    is_hero="false"
    occupation="Lord"
    culture="Culture.gundabad"
    skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"
    is_template="true">
    <face><face_key_template value="BodyProperty.fighter_gundabad" /></face>
    <skills />
    <Equipments>
        <EquipmentRoster>
            <equipment slot="Body" id="Item.nordic_hauberk" />
            <equipment slot="Leg"  id="Item.mail_cavalier_boots" />
        </EquipmentRoster>
        <EquipmentRoster civilian="true" />
    </Equipments>
</NPCCharacter>
```

**Vanilla equivalent:** `SandBox/education_character_templates.xml` — identical schema. The empty `<EquipmentRoster civilian="true" />` placeholder is the vanilla convention (engine falls through to culture's default civilian roster).

**Issues:** TAOM uses `<EquipmentRoster civilian="true">` (inline form) — still valid. No mechanical migration needed in this file unless it also contains `<EquipmentSet>` references; cross-check.

---

## Migration checklist per TAOM file

The driver is `tools/migrate_equipment_type_1_4_3.py` (to be written in S0 per equipment overhaul doc). Per-file expectations:

### Troop files (13 files)

**Step 1 — automated migration of `<EquipmentSet>` references:**
```bash
python tools/migrate_equipment_type_1_4_3.py --dry-run --files Main/_Module/ModuleData/troops/
python tools/migrate_equipment_type_1_4_3.py --apply  --files Main/_Module/ModuleData/troops/
```

Expected diff per troop:
```diff
-<EquipmentSet id="battania_troop_civilian_template_t2" civilian="true" />
+<EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
```

**Step 2 — verify referenced templates still exist in 1.4.5:**

TAOM troops reference these vanilla templates (per grep):
- `aserai_troop_civilian_template_t3` — used by Rhûn troops (vanilla — confirmed exists in 1.4.5)
- `battania_troop_civilian_template_t2` — used by Gondor (vanilla — confirmed)
- `vlandia_troop_civilian_template_t2` — likely Rohan
- `empire_troop_civilian_template_t1` — likely TAOM Empire-renamed cultures
- `khuzait_*` / `sturgia_*` / others

Run `tools/validate_troop_refs.py` (extends `tools/validate_gondor_refs.py` pattern) against all 13 troop files.

**Step 3 — `is_basic_troop` audit:**

Each culture must have exactly **one or two** troops marked `is_basic_troop="true"` (recruitment entry points). Reference: `troops_gondor.xml:13` (`gondor_loss_lumberman` is basic). Verify per culture's `taom_spcultures.xml` `basic_troop=` attribute matches.

**Step 4 — `upgrade_targets` orphan audit:**

Cross-reference every `<upgrade_target id="NPCCharacter.X" />` against troop IDs in the same culture file. Orphan IDs silently break upgrades — players can recruit T1 troops but can't upgrade them.

### `taom_partyTemplates.xml`

**Step 1 — no schema migration needed.** The 1.4.3 changes don't touch party-template XML.

**Step 2 — reference integrity check:**

```bash
python tools/validate_party_template_refs.py  # to author
```

Every `<PartyTemplateStack troop="NPCCharacter.X" />` must resolve to a real troop somewhere. Orphan stacks spawn nothing — small lord parties or empty bandits.

**Step 3 — per-culture coverage audit per `.claude/rules/troops.md` template-type table:**

For each of the 10 TAOM custom cultures (+5 XSLT-renamed), confirm the 12 expected templates exist (`kingdom_hero_party_{culture}_template`, `kingdom_hero_party_mercenary_{culture}_template`, etc.). Missing patrol_party_level_3 → no elite patrols for that culture.

### NPC files (18 files in `characters/`)

**Step 1 — verify civilian="true" cases are on `<EquipmentRoster>` not `<EquipmentSet>`:**

Grep per file for `<EquipmentSet[^/]*civilian` (multiline). If hits, migrate to `equipmentType="Civilian"`. Per the v1.4.x-equipment-overhaul doc, NPC files mainly use inline rosters, so action here should be light.

**Step 2 — verify the 26-NPC convention** (per `.claude/rules/xml-data.md`):
- 10 merchants (`spc_notable_{culture}_0` … `_4b`)
- 3 preachers (`_5`, `_6`, `_7`)
- 2 artisans (`_8`, `_9`)
- 6 gang leaders (`_gl1`, `_10`, `_11`, `_gl4`, `_12`, `_13`)
- 2 rural notables (`_21`, `_22`)
- 3 headmen (`spc_{culture}_headman_1`, `_2`, `_3`)

Any culture missing a category will have generic vanilla NPCs appearing in its towns/villages.

### `taom_education_character_templates.xml`

**Step 1 — verify civilian="true" cases are on `<EquipmentRoster>` (60 occurrences):**

This file follows the vanilla `education_character_templates.xml` pattern of `<EquipmentRoster civilian="true" />` empty placeholders. No migration expected. Confirm via grep before assuming.

**Step 2 — coverage audit:**

Vanilla covers 6 cultures × `stage_<n>_page_<p>_branch_<b>`. TAOM extends to LOTR cultures. Confirm coverage exists for each TAOM custom culture × stage matrix the child-education system iterates.

---

## Common pitfalls

### 1. Underwear bug (item ID typo)

**Symptom:** Troop appears in underwear when fighting.

**Cause:** Equipment `<equipment slot="Body" id="Item.sk_gd_los_inf_chainmail_z" />` references an item ID that doesn't exist in LOTRLOME_Armory.

**Fix:** Run `tools/validate_gondor_refs.py` (extend to all cultures). Every `Item.X` in `<equipment>` must resolve to an `<Item id="X">` in the Armory XML files.

**Reference:** `CLAUDE.md` equipment validation section.

### 2. Tier-skill mismatch

**Symptom:** T6 elite troop performs like T3 because skills weren't updated when tier was bumped.

**Cause:** Hand-edited `level="36"` (T7) but left skills at T3 values.

**Fix:** Run `tools/rebalance_troops.py --dry-run` per file. Compare against the per-tier skill baseline.

### 3. Upgrade chain dead-end

**Symptom:** Player can't upgrade T2 troop because the T3 target was renamed but the upgrade_target wasn't updated.

**Cause:** `<upgrade_target id="NPCCharacter.gondor_old_name" />` references a deleted/renamed troop.

**Fix:** Cross-reference all `upgrade_target` IDs against troop IDs in the same culture file.

### 4. Party template stack with wrong troop ID

**Symptom:** Bandit party spawns with 0 troops; lord retinue empty.

**Cause:** `<PartyTemplateStack troop="NPCCharacter.X" />` where X doesn't exist.

**Fix:** Validate every party template reference against the troops registry.

### 5. EquipmentSet reference to deleted vanilla template

**Symptom:** Civilian outfit is the vanilla default (looks generic) instead of the cultural template.

**Cause:** `<EquipmentSet id="battania_troop_civilian_template_t99" ... />` where _t99 was never a real template, or a vanilla template was renamed between game versions.

**Fix:** Cross-reference TAOM `<EquipmentSet id="...">` against `spnpccharacters.xml` template IDs in the *current* installed game version (v1.4.5). The `v1.4.0 lowered Vlandian / raised Battanian` clan tier change does NOT rename equipment templates; the template IDs are stable across the 1.3.15→1.4.5 transition.

### 6. v1.4.0 Vlandia / Battania clan tier change (TAOM impact)

**v1.4.0 change:** Vanilla reduced Vlandia's initial clan tiers; raised Battania's. TAOM XSLT-renames `vlandia` → Rohan and `battania` → Khand (per CLAUDE.md mapping).

**Impact:** TAOM's Rohan now starts at *lower* tiers; Khand starts at *higher* tiers. This affects how often these clans field full-strength armies in early game.

**Mitigation:** Doesn't affect troop/party XML schema. If gameplay feels off post-migration, audit the clan tier values in `spclans.xslt`.

### 7. v1.4.0 party visibility (terrain-based)

**v1.4.0 change:** Engine factors party size + terrain when computing visibility / spotting checks. Smaller parties in forests/hills are harder to spot.

**Impact:** TAOM bandit/forest-ranger parties may feel different. No XML change.

**Mitigation:** If certain TAOM patrol parties become invisible in mid-game, raise their min/max stack sizes in `taom_partyTemplates.xml`.

---

## Cross-references

- [`docs/reference/party-template-sizing.md`](../../reference/party-template-sizing.md): what `max_value` actually controls. The shared per-party spawn ratio, why the max sum is a spawn ceiling rather than a party size, and where `PartySizeLimit` takes over. Read before retuning any stack values.
- `docs/migration/templates/characters.md` — wanderer / hero / notable NPC schema details
- `docs/migration/templates/equipment-rosters.md` — `*_equipment_sets.xml` schema details (the **definition** side of the reference)
- `docs/migration/v1.4.x-equipment-overhaul.md` — full equipment-system migration narrative (3,372 `civilian="true"` mass migration)
- `docs/migration/v1.4.x-changes.md` — full v1.4.0-v1.4.5 changelog
- `docs/migration/v1.4.x-taom-impact.md` — TAOM-specific impact matrix
- `.claude/rules/troops.md` — per-culture troop-management checklist (file fanout when adding/restructuring troops)
- `.claude/rules/xml-data.md` — NPC naming convention, region codes, culture StringId table
- `tools/rebalance_troops.py` — skill-level rebalancing
- `tools/generate_gondor_troops.py`, `tools/generate_rhun_troops.py` — per-culture generators
- `tools/apply_gondor_troop_revamp.py` — mechanical EquipmentRoster swap (issue #99 precedent)
- `tools/validate_gondor_refs.py` — underwear-bug gate (cross-checks armor refs against Armory) — extend to other cultures pre-migration
- `tools/complete_lords_xslt.py` — XSLT-level reference for character XML migration

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/migration/templates/README.md](./README.md)

<!-- backlinks-end -->
