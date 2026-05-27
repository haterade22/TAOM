# Character XML Templates (v1.4.5 reference)

> What "right" looks like in 1.4.5 for character-typed XML files. Companion to the [equipment-rosters.md](equipment-rosters.md) doc — together they define the contract a TAOM character XML must satisfy.

> All vanilla snippets in this doc are taken verbatim from a v1.4.5 install at `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\{SandBox,SandBoxCore}\ModuleData\`.

## Overview

There are **two top-level character schemas** in the vanilla data:

| Root element | File(s) | Purpose |
|---|---|---|
| `<NPCCharacters>` | `lords.xml`, `spspecialcharacters.xml`, `obsolete_characters.xml` (both modules), `spnpccharacters.xml`, `caravans.xml`, troop XMLs | Defines a character object — face, skills, traits, equipment, occupation. **This is what TAOM means by "a character XML."** |
| `<Heroes>` | `heroes.xml` | Defines hero *relationships* — faction, spouse, father/mother, alive flag, encyclopedia text. **Does NOT redefine the NPCCharacter.** The actual character is matched by `id=` to an `<NPCCharacter>` in another file. |

This doc covers the four `<NPCCharacter>` sub-types TAOM cares about and one auxiliary `<Hero>` block:

| Sub-type | Vanilla file | TAOM file(s) | Identifying attributes |
|---|---|---|---|
| **Lord** (kingdom rulers, nobles, clan members) | `SandBox/lords.xml` (400 chars) | `Main/_Module/ModuleData/characters/lords.xml` (566 chars) overlaid by `lords.xslt` | `is_hero="true"`, `occupation="Lord"` |
| **Hero relationship** | `SandBox/heroes.xml` (17 `<Hero>` per-line entries — wait, raw count is 17 matches but file has hundreds of full entries) | `Main/_Module/ModuleData/characters/heroes.xml` | Auxiliary `<Hero>` element, not `<NPCCharacter>` |
| **Wanderer** (recruitable tavern companion) | `SandBox/spspecialcharacters.xml` (67 wanderers) | `Main/_Module/ModuleData/taom_wanderers.xml` (170) | `occupation="Wanderer"`, `is_template="true"`, `is_hero="false"` |
| **Notable / NPC** (merchants, preachers, artisans, gang leaders, rural notables, headmen) | `SandBox/spspecialcharacters.xml` | `Main/_Module/ModuleData/characters/npcs_<culture>.xml` (per-culture, e.g. `npcs_gondor.xml`) | `is_template="true"`, `is_hero="false"`, occupation ∈ {Merchant, Preacher, Artisan, GangLeader, RuralNotable, Headman} |

> **Caveat — wanderers location.** The task brief pointed to `SandBox/obsolete_characters.xml` for wanderers; that file actually contains obsolete lord templates + tournament characters. The real wanderer source is `SandBox/spspecialcharacters.xml`. TAOM follows the correct convention (`taom_wanderers.xml` uses `occupation="Wanderer"` matching vanilla).

## Attribute reference (post-1.4.3)

Every attribute observed on `<NPCCharacter>` in vanilla 1.4.5. **Bold = required for the type to load.** Italics = optional. Strikethrough = deprecated.

| Attribute | Type | Required? | Notes / changed in 1.4.3? |
|---|---|---|---|
| **`id`** | string | yes | Unique character id. Must match `<Hero id=>` in heroes.xml if `is_hero="true"`. |
| **`name`** | localized string `{=key}Default` | yes | Engine will accept `{=!}placeholder` for template names. |
| **`culture`** | `Culture.<id>` | yes | Maps to `<Culture id=>` from spcultures.xml. **TAOM custom cultures use LOTR ids (`Culture.gondor`); XSLT cultures use vanilla engine ids (`Culture.empire`, etc.) — see `xml-data.md` rule.** |
| `age` | int | optional | Default age in years. |
| `voice` | enum (`curt`, `earnest`, `softspoken`, `ironic`) | recommended | Used for conversation barks. |
| `default_group` | enum (`Infantry`, `Ranged`, `Cavalry`, `HorseArcher`) | recommended | Battle formation slot. |
| `is_hero` | `true`/`false` | recommended | Distinguishes lords/companions (true) from templates/troops (false). |
| `is_female` | `true`/`false` | optional | Default false. Affects body mesh + voice resolution. |
| `is_template` | `true`/`false` | optional, set on wanderers + notables | Marks the character as a spawn template (engine clones for procgen). |
| `is_basic_troop` | `true`/`false` | optional, on troops only | Marks t1 culture base recruits. |
| `is_obsolete` | `true`/`false` | optional | Kept for save-compat; engine treats as removed. |
| `is_hidden_encyclopedia` | `true`/`false` | optional | Suppresses encyclopedia page. |
| `occupation` | enum | yes for most subtypes | Lord, Wanderer, Merchant, Preacher, Artisan, GangLeader, RuralNotable, Headman, Soldier, Townsfolk, Villager, CaravanGuard, ArenaMaster, Bandit, … |
| `skill_template` | `SkillSet.<id>` | recommended | Refers to spnpcskill_sets / spcoreskill_sets entry. Implicit alternative to inline `<skills>`. |
| `level` | int | optional, troops mainly | Tier level for soldiers. |
| `face_mesh_cache` | `true`/`false` | optional | Cache face mesh for perf. |
| `banner_symbol_mesh_name` | string | optional, main_hero only | Used by char creation banner. |
| `banner_symbol_color` | hex | optional | Same. |
| ~~`civilianTemplate`~~ | id | **DEPRECATED in 1.4.3** | Logs warning at load. Rely on equipment roster flags + `culture` instead. **TAOM never used these — verified 0 occurrences.** |
| ~~`battleTemplate`~~ | id | **DEPRECATED in 1.4.3** | Same. |

### Child elements (under `<NPCCharacter>`)

| Element | Purpose | Notes |
|---|---|---|
| `<face>` containing `<BodyProperties version="4" key="..."/>` or `<face_key_template value="BodyProperty.<id>"/>` | Face / body | `version="4"` is the 1.4.x norm (was `version="3"` in older files; still tolerated for back-compat). `<face_key_template>` is shorthand for "use this referenced body template." |
| `<face>` sub-tags: `<hair_tags>`, `<beard_tags>`, `<tattoo_tags>` | Cosmetic tag filters | Engine matches hair/beard/tattoo meshes against these tags. |
| `<skills>` | Inline skill values | Each `<skill id="X" value="N"/>`. Optional if `skill_template=` provided. |
| `<Traits>` | Personality + skill-derivation traits | `BalancedFightingSkills`, `Commander`, `Politician`, `Manager`, `Honor`, `Generosity`, `Mercy`, `Calculating`, `Valor`, etc. |
| `<feats>` | Culture feats (commented in vanilla main_hero) | Rarely used directly on character. |
| `<Equipments>` | Equipment | See "Equipment block (1.4.3 changes)" below. |
| `<upgrade_targets>` | Troop upgrade chain | Only on troops/Soldiers. |

### Equipment block (1.4.3 changes — the big one)

Inside `<Equipments>` you find three element types:

| Element | Meaning | 1.4.3 attribute usage |
|---|---|---|
| `<EquipmentRoster>` (with `<equipment slot=... id=.../>` children) | Inline equipment set written directly on the character. | **Still uses the old `civilian="true"` attribute in vanilla 1.4.5 lords.xml on 3 stragglers** (e.g. `main_hero` line 156). For new TAOM rosters, prefer `<EquipmentSet equipmentType="Civilian"/>` referencing an external roster. |
| `<EquipmentRoster/>` (self-closing, empty) | Placeholder battle roster — the character will resolve equipment from the referenced `<EquipmentSet>` instead. | Common pattern in lords.xml. |
| `<EquipmentSet id="X" equipmentType="Civilian|Battle|Stealth"/>` | **Reference** to an external `<EquipmentRoster id="X">` defined in `*_equipment_sets.xml`. | **NEW form in 1.4.3.** `equipmentType` is an enum (one of `Battle`, `Civilian`, `Stealth` — `Stealth` is new). NOT flag combinations. Battle is the default if omitted. |
| `<equipment slot=... id=.../>` directly under `<Equipments>` (no roster wrapper) | Single-slot override applied on top of resolved rosters. | Rare. Used on `main_hero` to lock Horse / HorseHarness. |

**Vanilla 1.4.5 counts in `SandBox/lords.xml`:**

| Pattern | Count | Meaning |
|---|---|---|
| `equipmentType=` | 396 | New form is the norm |
| `civilian="true"` on `<EquipmentRoster>` | 3 | Stragglers (main_hero + 2 emperor-tier civilian roster cases) |
| `<EquipmentSet>` references | 792 | Every lord uses external rosters |
| `civilianTemplate=` / `battleTemplate=` on `<NPCCharacter>` | 0 | Fully removed |
| `<NPCCharacter>` total | 400 | — |

So vanilla itself ships with 3 unmigrated `civilian="true"` survivors — the engine still accepts that attribute form, but at warning level. **TAOM has 3,372 occurrences** to migrate (per `v1.4.x-equipment-overhaul.md`).

---

## Lords (kingdom rulers + nobles)

### Vanilla 1.4.5 — kingdom ruler example

`SandBox/lords.xml`, lord `lord_1_1` (Lucon, Empire kingdom ruler):

```xml
<NPCCharacter
    id="lord_1_1"
    name="{=CLqPbdiZ}Lucon"
    age="62"
    voice="curt"
    default_group="Cavalry"
    is_hero="true"
    culture="Culture.empire"
    skill_template="SkillSet.spc_politician_skills_ruler"
    occupation="Lord"
    face_mesh_cache="true">
    <face>
        <BodyProperties
            version="4"
            weight="0.2"
            build="0.7"
            key="00005C0B4000034D649A7797787A473B743A45888527B857869AA27956B54D3C0007A5A6073DE87C00001105000030A30000001F000000000000000001182000" />
        <hair_tags><hair_tag name="empire" /></hair_tags>
        <beard_tags><beard_tag name="empire" /></beard_tags>
        <tattoo_tags><tattoo_tag name="Cleanface" /></tattoo_tags>
    </face>
    <Traits>
        <Trait id="KnightFightingSkills" value="5" />
        <Trait id="Commander" value="16" />
        <Trait id="Politician" value="12" />
        <Trait id="Manager" value="14" />
        <Trait id="Honor" value="1" />
        <Trait id="Generosity" value="-1" />
        <Trait id="Calculating" value="1" />
        <Trait id="Oligarchic" value="1" />
    </Traits>
    <Equipments>
        <EquipmentRoster />
        <EquipmentSet id="n_emp_king_template_bat_m" />
        <EquipmentSet id="n_emp_king_template_civ_m" equipmentType="Civilian" />
    </Equipments>
</NPCCharacter>
```

Key points:
- **Kingdom rulers** reference equipment-roster ids prefixed `n_emp_king_template_*` (kingdom ruler tier). Non-ruler lords use `emp_bat_template_*` / `emp_civ_template_*`.
- **Faction binding** lives in `heroes.xml`, not here.
- Inline `<skills>` is OMITTED — skills come from `skill_template`. Compare to TAOM's lords.xml which inlines explicit skill values.

### Vanilla 1.4.5 — female noble example

`lord_1_2` (Zerosica, Lucon's wife):

```xml
<NPCCharacter
    id="lord_1_2"
    name="{=wbLqHvjE}Zerosica"
    age="52"
    voice="curt"
    default_group="Cavalry"
    is_hero="true"
    is_female="true"
    culture="Culture.empire"
    skill_template="SkillSet.spc_chatelaine_skills"
    occupation="Lord"
    face_mesh_cache="true">
    <face>
        <BodyProperties version="4" weight="0.2" build="0.3" key="..." />
        <hair_tags><hair_tag name="empire" /></hair_tags>
        <tattoo_tags><tattoo_tag name="Cleanface" /></tattoo_tags>
    </face>
    <Traits>
        <Trait id="Politician" value="6" />
        <Trait id="Manager" value="16" />
        <Trait id="Generosity" value="1" />
        <Trait id="Honor" value="1" />
    </Traits>
    <Equipments>
        <EquipmentSet id="emp_bat_template_lady" />
        <EquipmentSet id="emp_noncom_template_stoic" equipmentType="Civilian" />
    </Equipments>
</NPCCharacter>
```

- Note `is_female="true"` is explicit.
- Battle roster uses `_lady` suffix — gendered roster ids.
- `<EquipmentRoster />` empty-placeholder OMITTED for ladies — only refs.
- Females skip `<beard_tags>`.

### TAOM current state — direct lords.xml

`Main/_Module/ModuleData/characters/lords.xml`, `lord_3_3_1` (Tariq, custom Harad lord — culture is `aserai` because Harad uses the aserai XSLT culture):

```xml
<NPCCharacter id="lord_3_3_1" name="{=aom_lord_3_3_1_name}Tariq" age="19" voice="curt"
              default_group="Cavalry" is_hero="true" culture="Culture.aserai"
              occupation="Lord" face_mesh_cache="true"
              skill_template="SkillSet.spc_cavalry_skills_rookie">
    <face>
        <BodyProperties version="4" age="30.2" weight="0" build="0" key="..." />
        <hair_tags><hair_tag name="aserai" /></hair_tags>
        <beard_tags><beard_tag name="aserai" /></beard_tags>
        <tattoo_tags><tattoo_tag name="Cleanface" /></tattoo_tags>
    </face>
    <skills>
        <skill id="OneHanded" value="99" /> ... <skill id="Engineering" value="17" />
    </skills>
    <Traits>
        <Trait id="BalancedFightingSkills" value="3" />
        <Trait id="Commander" value="10" />
        ...
    </Traits>
    <Equipments>
        <EquipmentSet id="harad_bat_template_medium_a" />
        <EquipmentSet id="harad_civ_template_a" civilian="true" /> <!-- ⚠️ deprecated form -->
    </Equipments>
</NPCCharacter>
```

### TAOM current state — lords.xslt overlay

`Main/_Module/ModuleData/lords.xslt` rewrites vanilla `lord_1_1` (Empire kingdom ruler) into TAOM's Dunland warlord:

```xml
<xsl:template match="NPCCharacter[@id='lord_1_1']">
    <xsl:copy>
        <xsl:attribute name="id">lord_1_1</xsl:attribute>
        <xsl:attribute name="name">{=aom_lord_1_1_name}Brenin Wulf, the Ironhand</xsl:attribute>
        ...
        <xsl:attribute name="culture">Culture.empire</xsl:attribute>
        <xsl:attribute name="skill_template">SkillSet.spc_politician_skills_ruler</xsl:attribute>
        <xsl:attribute name="occupation">Lord</xsl:attribute>
        ...
        <Equipments>
            <EquipmentSet id="dunland_bat_template_medium_a" />
            <EquipmentSet id="dunland_civ_template_default_a" civilian="true" /> <!-- ⚠️ deprecated -->
        </Equipments>
        <xsl:apply-templates select="node()[not(self::face or self::skills or self::Traits or self::Equipments)]"/>
    </xsl:copy>
</xsl:template>
```

### Differences from vanilla 1.4.5

| # | TAOM | Vanilla 1.4.5 | Severity |
|---|---|---|---|
| 1 | `<EquipmentSet ... civilian="true" />` | `<EquipmentSet ... equipmentType="Civilian" />` | 🔥 Migration required (the headline 1.4.3 change) |
| 2 | TAOM inlines full `<skills>` block in addition to `skill_template=` | Vanilla uses `skill_template=` alone, no inline `<skills>` for most lords | 🟢 Both forms work; inline takes precedence |
| 3 | TAOM lords use kingdom-ruler-tier rosters with `_bat_template_medium_a` (not `n_<culture>_king_template_bat_m`) | Vanilla rulers use `n_<culture>_king_template_*` prefixed rosters | 🟡 TAOM doesn't yet distinguish ruler-tier from regular-lord-tier rosters per the 1.4.3 `IsKingdomRulerTemplate` flag pattern |
| 4 | No `<EquipmentRoster />` empty placeholder | Vanilla often opens with `<EquipmentRoster />` empty placeholder | 🟢 Optional cosmetic |
| 5 | TAOM 566 NPCCharacters vs vanilla 400 — additional LOTR lords | — | 🟢 Intentional |

### Migration checklist for TAOM lords

- [ ] Mechanically replace `<EquipmentSet ... civilian="true" />` with `<EquipmentSet ... equipmentType="Civilian" />` across `lords.xml` and `lords.xslt`. Use `tools/migrate_equipment_type_1_4_3.py` (to be authored — see v1.4.x-equipment-overhaul.md).
- [ ] For each TAOM kingdom (Mordor, Gondor, Erebor, Rivendell, Lothlorien, Mirkwood, Isengard, Gundabad, Dol Guldur, Umbar — plus the 6 XSLT kingdoms), ensure the rulers (`clan_<kingdom>_1` heads) reference dedicated ruler-tier equipment rosters tagged `IsKingdomRulerTemplate`. Currently TAOM rulers share `_bat_template_*` with regular lords.
- [ ] Verify no `civilianTemplate=` / `battleTemplate=` regressions slip in via copy-paste (these never existed in TAOM but are easy to add wrongly).
- [ ] Check `lords.xslt` passthrough — vanilla 1.4.5 may have added new attributes since 1.3.15 that the XSLT silently drops. Run `tools/complete_lords_xslt.py` against the 1.4.5 dump to refresh the explicit-attribute list.
- [ ] Confirm `is_hero="true"` and `occupation="Lord"` are set on every NPCCharacter in TAOM lords.xml (566 entries).

---

## Heroes (the `<Hero>` relationship file)

**`heroes.xml` is a different schema** — `<Hero>` elements bind faction / spouse / father / mother / alive / encyclopedia text to an id defined in another file's `<NPCCharacter>`.

### Vanilla 1.4.5

```xml
<Heroes>
    <Hero id="main_hero" faction="Faction.player_faction" />
    <Hero id="dead_lord_2_1"
          alive="false"
          faction="Faction.clan_sturgia_2"
          text="{=bsOLRZyS}Olek the Old was boyar of the Kuloving..." />
    <Hero id="lord_1_1"
          spouse="Hero.lord_1_2"
          faction="Faction.clan_empire_north_1"
          text="{=WBl5hS0e}The northern third of the Empire is ruled by Lucon..." />
    <Hero id="lord_1_31"
          father="Hero.lord_1_1"
          mother="Hero.lord_1_2"
          faction="Faction.clan_empire_north_1" />
</Heroes>
```

### TAOM current state

`Main/_Module/ModuleData/characters/heroes.xml`:

```xml
<Heroes>
    <Hero id="lord_E1_1"
          faction="Faction.clan_erebor_1"
          text="{=dain_ironfoot_description}Dáin Ironfoot..." />
    <Hero id="lord_E1_2"
          father="Hero.lord_E1_1"
          mother="Hero.lord_E1_6"
          faction="Faction.clan_erebor_1" />
</Heroes>
```

### Differences from vanilla

| # | TAOM | Vanilla 1.4.5 | Severity |
|---|---|---|---|
| 1 | Same schema, no deprecated attributes | Same | 🟢 No migration needed for heroes.xml itself |
| 2 | TAOM heroes.xml has no `<NPCCharacter>` blocks — relationships only | Same | 🟢 Correct pattern |

### Migration checklist for TAOM heroes.xml

- [ ] No action required — schema unchanged in 1.4.3/1.4.5. Verify every `<Hero id=>` has a matching `<NPCCharacter id=>` in lords.xml.

---

## Wanderers (recruitable tavern companions)

### Vanilla 1.4.5 example

`SandBox/spspecialcharacters.xml`, `spc_wanderer_empire_0`:

```xml
<NPCCharacter
    id="spc_wanderer_empire_0"
    name="{=bvjFhiDr}{FIRSTNAME} the Scholar"
    voice="curt"
    age="25"
    default_group="Infantry"
    is_template="true"
    is_hero="false"
    culture="Culture.empire"
    occupation="Wanderer"
    skill_template="SkillSet.spc_wanderer_empire_0_skills">
    <face>
        <face_key_template value="BodyProperty.townsman_empire" />
    </face>
    <Traits>
        <Trait id="Mercy" value="1" />
        <Trait id="Generosity" value="-1" />
    </Traits>
    <Equipments>
        <EquipmentSet id="npc_companion_equipment_template_empire"
                      equipmentType="Civilian" />
        <EquipmentSet id="npc_companion_equipment_template_empire" />
    </Equipments>
</NPCCharacter>
```

Key points:
- **`is_template="true"` + `is_hero="false"`** — engine instantiates per save (procedural names via `{FIRSTNAME}`).
- **Face uses `<face_key_template>`** referencing a shared `BodyProperty.townsman_<culture>` body, not a direct BodyProperties key.
- **Two `<EquipmentSet>` lines referencing the SAME roster id** — one with `equipmentType="Civilian"`, one without (defaults to Battle). The roster authors both modes inside.
- **No `<skills>` inline** — `skill_template=` covers it.
- **No `<Hero>` block** — wanderers become heroes only when recruited, dynamically.

> **1.4.3 wanderer-specific note.** Per the 1.4.3 dev notes, a Nord wanderer regression was fixed (correct equipment on culture). Confirm Nord-equivalent (any TAOM custom-culture wanderers) loads correctly — the fix is in `EquipmentSelectionModel`, not in XML.

### Vanilla 67 wanderer counts by culture (sampled)

Vanilla wanderers are 8-per-culture across 7 cultures + a few extras. Mostly `_0` through `_7`, with traits + equipment-template-id varying.

### TAOM current state

`Main/_Module/ModuleData/taom_wanderers.xml`, `spc_wanderer_gondor_0`:

```xml
<NPCCharacter
    id="spc_wanderer_gondor_0"
    name="{=aom_spc_wanderer_gondor_0_name}{FIRSTNAME} Calendorionath"
    voice="softspoken"
    age="28"
    is_template="true"
    default_group="Infantry"
    is_hero="false"
    culture="Culture.gondor"
    occupation="Wanderer"
    skill_template="SkillSet.spc_wanderer_gondor_0_skills">
    <face>
        <face_key_template value="BodyProperty.fighter_gondor" />
    </face>
    <Traits>
        <Trait id="Calculating" value="1" />
        <Trait id="Mercy" value="-1" />
    </Traits>
    <Equipments>
        <EquipmentSet id="npc_companion_equipment_template_gondor" civilian="true" /> <!-- ⚠️ deprecated -->
        <EquipmentSet id="npc_companion_equipment_template_gondor" />
    </Equipments>
</NPCCharacter>
```

TAOM ships **170 wanderers** in `taom_wanderers.xml` (vs vanilla's 67).

### Differences from vanilla 1.4.5

| # | TAOM | Vanilla 1.4.5 | Severity |
|---|---|---|---|
| 1 | `civilian="true"` on `<EquipmentSet>` | `equipmentType="Civilian"` | 🔥 Migrate |
| 2 | Face template = `BodyProperty.fighter_<culture>` | Face template = `BodyProperty.townsman_<culture>` | 🟡 Cosmetic — TAOM uses fighter physique for companions; vanilla uses lighter townsman. Both work; choose intentionally. |
| 3 | TAOM has 170 wanderers vs vanilla 67 | — | 🟢 Intentional |
| 4 | NamedCompanions feature adds 18 more wanderers with `is_hero="true"` + spawn-config JSON (Aragorn, Legolas, Gimli, etc. — see `Main/_Module/ModuleData/named_companions/`) | Vanilla has no named-companion equivalent | 🟢 Intentional |

### Migration checklist for TAOM wanderers

- [ ] Mechanical: `civilian="true"` → `equipmentType="Civilian"` on all `<EquipmentSet>` in `taom_wanderers.xml` and `named_companions.xml` (170 + 18 = 188 entries × ~2 rosters each).
- [ ] Verify named-companion overrides (e.g. Aragorn) still load — the 1.4.3 EquipmentSelectionModel changes affect companions promoted to lord.
- [ ] If any TAOM wanderer is intended as a noble (Aragorn, etc.), audit whether they need `IsLordTemplate` flagged rosters available for promotion (per the 1.4.3 "companions promoted to lord" behavior).

---

## NPCs (notables: merchants, gang leaders, headmen)

> **TAOM convention.** Each of TAOM's 10 custom + 6 XSLT cultures has 26 notable templates in `characters/npcs_<culture>.xml`, per the naming convention in `xml-data.md`:
> `_0`–`_4b` merchants (10), `_5`/`_6`/`_7` preachers (3), `_8`/`_9` artisans (2), `_gl1`/`_10`/`_11`/`_gl4`/`_12`/`_13` gang leaders (6), `_21`/`_22` rural notables (2), `<culture>_headman_1/_2/_3` headmen (3).

### Vanilla 1.4.5 example — Merchant

`SandBox/spspecialcharacters.xml`, `spc_notable_empire_0`:

```xml
<NPCCharacter
    id="spc_notable_empire_0"
    name="{=!}Cautious imperial merchant"
    is_template="true"
    voice="ironic"
    default_group="Infantry"
    is_hero="false"
    culture="Culture.empire"
    skill_template="SkillSet.spc_notable_empire_0"
    occupation="Merchant">
    <face>
        <face_key_template value="BodyProperty.villager_empire" />
    </face>
    <skills></skills>
    <Traits>
        <Trait id="Valor" value="-1" />
        <Trait id="Calculating" value="1" />
    </Traits>
    <Equipments>
        <EquipmentSet id="npc_armed_wanderer_equipment_template_empire" />
        <EquipmentSet id="npc_wanderer_equipment_template_empire" equipmentType="Civilian" />
    </Equipments>
</NPCCharacter>
```

### Vanilla 1.4.5 example — Preacher (female mystic, more decorated)

```xml
<NPCCharacter
    id="spc_notable_empire_7"
    name="{=!}Female imperial mystic"
    voice="earnest"
    is_template="true"
    is_female="true"
    culture="Culture.empire"
    skill_template="SkillSet.spc_notable_empire_7"
    occupation="Preacher">
    <face>
        <face_key_template value="BodyProperty.townswoman_empire" />
    </face>
    <Traits>
        <Trait id="Mercy" value="1" />
        <Trait id="Generosity" value="0" />
    </Traits>
    <Equipments>
        <EquipmentSet id="npc_armed_wanderer_equipment_template_empire" />
        <EquipmentSet id="npc_wanderer_equipment_template_empire" equipmentType="Civilian" />
        <EquipmentSet id="spc_notable_empire_7" />  <!-- per-character armor flair -->
    </Equipments>
</NPCCharacter>
```

Key points:
- **Three rosters**: armed-wanderer (battle), wanderer (civilian), and a character-specific roster for visual flair.
- `default_group` omitted on female (defaults to Infantry).
- `<skills></skills>` empty self-closing is tolerated (templates rely on `skill_template=`).

### TAOM current state — Merchant

`Main/_Module/ModuleData/characters/npcs_gondor.xml`, `spc_notable_gondor_0`:

```xml
<NPCCharacter id="spc_notable_gondor_0"
              default_group="Infantry"
              is_template="true"
              is_hero="false"
              voice="ironic"
              culture="Culture.gondor"
              name="{=aom_gd_notable_0}Prudent Gondorian merchant"
              skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"
              occupation="Merchant">
    <face><face_key_template value="BodyProperty.fighter_gondor" /></face>
    <skills></skills>
    <Traits><Trait id="Valor" value="-1" /><Trait id="Calculating" value="1" /></Traits>
    <Equipments>
        <EquipmentRoster civilian="true">  <!-- ⚠️ inline roster + deprecated attr -->
            <equipment slot="Body" id="Item.gondor_noble_coat_a" />
            <equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
        </EquipmentRoster>
    </Equipments>
</NPCCharacter>
```

### Differences from vanilla 1.4.5

| # | TAOM | Vanilla 1.4.5 | Severity |
|---|---|---|---|
| 1 | TAOM uses inline `<EquipmentRoster civilian="true">` with explicit slot/id | Vanilla refs external `<EquipmentSet id="..." equipmentType="Civilian"/>` | 🔥 Both issues at once: deprecated attribute AND inline-vs-referenced. The deprecated attribute is the urgent fix; the inline-vs-referenced is a design pattern choice. |
| 2 | TAOM uses `skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"` (generic troop skill set) on a Merchant notable | Vanilla uses `skill_template="SkillSet.spc_notable_empire_0"` (dedicated notable skill set) | 🟡 TAOM notables share a generic skill set — works, but ignores the trait-driven skill derivation pattern vanilla uses |
| 3 | TAOM uses `BodyProperty.fighter_<culture>` for notables | Vanilla uses `BodyProperty.villager_<culture>` / `townsman_<culture>` / `townswoman_<culture>` | 🟡 Cosmetic — notables look like fighters in TAOM (intentional?), softer civilians in vanilla |
| 4 | Only ONE EquipmentRoster — only civilian | Vanilla has battle + civilian + flair | 🟡 TAOM notables can't appear armed/in battle context |
| 5 | TAOM has 26 notables per culture × ~16 cultures = ~416 notables | Vanilla has ~10 per main culture × 7 = ~70 | 🟢 Intentional LOTR breadth |

### Migration checklist for TAOM NPCs

- [ ] **HIGH PRIORITY:** Mechanical `civilian="true"` → `equipmentType="Civilian"` migration across 14 `npcs_<culture>.xml` files. **But:** `<EquipmentRoster civilian="true">` (inline) is a different element from `<EquipmentSet civilian="true">` (reference). Vanilla 1.4.5 still has 3 surviving `<EquipmentRoster civilian="true">` in lords.xml, so the engine probably still accepts that form. The migration tool must distinguish:
  - `<EquipmentSet civilian="true">` → `<EquipmentSet equipmentType="Civilian">` (mandatory)
  - `<EquipmentRoster civilian="true">` → arguable; can leave as-is or migrate to a hoisted EquipmentSet reference
- [ ] Consider hoisting inline rosters into named `<EquipmentRoster id="...">` definitions in `sandboxcore_equipment_sets.xml` (or a TAOM equivalent), then referencing them via `<EquipmentSet id="..." equipmentType="Civilian"/>`. This matches vanilla's pattern and survives 1.4.3+ flag-based selection cleanly.
- [ ] Audit `skill_template=` choices — replacing generic troop sets with notable-tier sets (`spc_notable_<culture>_<n>`) for trait-driven skill derivation.
- [ ] Add battle rosters to notables that can appear in conversation+conflict (gang leaders especially — they should look armed).

---

## Common pitfalls

| Pitfall | Cause | Symptom |
|---|---|---|
| Naked NPC fallback | Missing mandatory roster for `(culture, gender, age-band)` after 1.4.3 flag system | Character appears in underwear or in wrong-culture clothing |
| `civilianTemplate=` reused | Copy-pasted from an old guide | Engine logs deprecation warning at load; data ignored |
| Reused battle equipment as civilian | Same `<EquipmentSet id=>` referenced twice without `equipmentType=` on one of them | Both contexts get battle gear; civilian NPCs walk around armored |
| `equipmentType="Civilian | Battle"` (flag combo) | Old-style EquipmentFlags reflex applied to new enum | Schema validation fails — load error, character vanishes |
| Old `EquipmentFlags` enum values (`IsCivilianTemplate`, `IsNobleTemplate`, etc.) on `<EquipmentRoster flags=>` | Pre-1.4.3 flag set | Unknown-flag warnings; flag ignored in selection |
| `<NPCCharacter>` referenced by `<Hero id=>` but missing from any `*.xml` | Removed lord whose hero record stayed | Faction loads without leader; clan goes leaderless on save load |
| Vanilla XSLT passthrough drops new 1.4.5 attribute | `lords.xslt` was authored against 1.3.15 schema | Subtle behavior loss with no warning (see `xslt.md` rule) |
| `<face_key_template>` references non-existent BodyProperty | Body templates renamed in 1.4.x | Character spawns with default face |
| Wanderer `is_template="true"` missing | Author thought wanderers were fixed-id characters | Engine treats as a single fixed character, not a procgen template — no per-save instantiation |
| Notable without `is_template="true"` | Same as above | Same — only one notable spawns ever |
| Equipment set without `culture=` attribute | Cross-culture roster | Warning at load (1.4.3 changed this from silent to warning) |

---

## Migration checklist (consolidated)

Cross-reference: [v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md) **§ TAOM mandatory roster matrix** (10 cultures × 8 rosters = 80 required) is the authoritative target for the per-culture equipment side. This doc covers the **character-XML** side.

### Mechanical (`tools/migrate_equipment_type_1_4_3.py`)

- [ ] All files under `Main/_Module/ModuleData/`: replace `<EquipmentSet ... civilian="true" ... />` with `<EquipmentSet ... equipmentType="Civilian" ... />`.
- [ ] Decide policy for inline `<EquipmentRoster ... civilian="true">` (vanilla still has 3 stragglers in 1.4.5 lords.xml) — recommend leaving alone unless re-authoring as referenced sets.
- [ ] Grep verify zero `civilianTemplate=` / `battleTemplate=` on any `<NPCCharacter>` (already verified 0 in TAOM but re-run on every commit touching characters).

### Schema-level (`tools/audit_equipment_roster_coverage.py`)

- [ ] For each of TAOM's 10 custom cultures + 6 XSLT cultures, every lord-tier character has access to:
  - Battle roster matching `(culture, gender)` flagged `IsLordTemplate` (+ `IsFemaleTemplate` for ladies)
  - Civilian roster same
  - Child + teen civilian rosters per gender (4 combinations)
- [ ] Each kingdom's ruling clan (clan_<kingdom>_1) heads have ruler-tier rosters tagged `IsKingdomRulerTemplate`.

### XSLT (`lords.xslt`, `spnpccharacters.xslt` if any)

- [ ] Re-run `tools/complete_lords_xslt.py` against the 1.4.5 vanilla dump to refresh explicit-attribute coverage — new vanilla attributes between 1.3.15 and 1.4.5 must be passed through, not dropped.
- [ ] Verify `<xsl:apply-templates select="@*|node()"/>` identity transform is intact (see `xslt.md` rule).

### Verification

- [ ] Run `/xslt-check` on `lords.xslt` against the 1.4.5 SandBoxCore vanilla XML.
- [ ] Boot the game with TAOM loaded; check the log for `civilian="true"` / `civilianTemplate=` / `battleTemplate=` deprecation warnings — there should be zero by end of S5.
- [ ] Visual smoke test: in-game encyclopedia open at least one lord, one wanderer, one merchant per TAOM culture; verify they're clothed correctly (no underwear, no wrong-culture).

### Out of scope (per v1.4.x-equipment-overhaul.md)

- [ ] **Do NOT** adopt `equipmentType="Stealth"` for TAOM disguise system — separate enhancement.
- [ ] **Do NOT** restructure duplicate battle+civilian sets — TaleWorlds noted this is intentionally unresolved in 1.4.

---

## Cross-references

- [v1.4.x-equipment-overhaul.md](../v1.4.x-equipment-overhaul.md) — full 1.4.3 changelog + impact matrix
- [v1.4.x-overview.md](../v1.4.x-overview.md) — migration high-level plan
- [v1.4.x-taom-impact.md](../v1.4.x-taom-impact.md) — TAOM-specific impact summary
- [xml-data.md](../../../.claude/rules/xml-data.md) — culture/kingdom/settlement id cross-reference + TAOM notable naming convention
- [xslt.md](../../../.claude/rules/xslt.md) — XSLT passthrough rules

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/migration/templates/README.md](./README.md)

<!-- backlinks-end -->
