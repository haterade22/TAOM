# Lord Skills + Traits Authoring Guide

End-to-end repeatable workflow for giving TAOM lords lore-driven skill values and personality traits, modeled on the May 2026 16-culture sweep (~880 NPCs across 18 GitHub issues #228-#245, commits `feabca2` → `8665ca6` → `c5dc168`). Use this when:

- A canonical Tolkien lord (Boromir, Galadriel, Théoden, etc.) has wrong in-game stats.
- A new TAOM-invented lord needs skills/traits assigned.
- A whole culture's roster needs a balance pass.
- Existing skill values look bad and you don't know why.

If you're authoring a **net-new culture** (armor + troops + recruitment), read [`new-culture-authoring.md`](./new-culture-authoring.md) FIRST — that covers the broader culture work. This guide handles the lord roster's skill values specifically.

## TL;DR

1. **Source of truth**: hand-edited `CULTURES` dict in [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py).
2. **Generator**: `python tools/apply_culture_skills_traits.py --all-cultures --apply` emits 3 outputs — `taom_lord_skill_sets.xml` (SkillSets), updated `lords.xml` + `lords.xslt` (skill_template attr swaps + populated `<skills>`/`<Traits>` blocks).
3. **Engine consumption**: every adult NPCCharacter has `skill_template="SkillSet.taom_..."` that points to a TAOM-owned SkillSet — that's what the engine actually uses. Explicit `<skills>` blocks on hero NPCs are IGNORED (kept as documentation only).
4. **Verify**: XML well-formed (3 files), then in-game Encyclopedia spot-check (Boromir OneHanded=295 + level growth = ~302).
5. **Ship**: commit-split (data + tool separate), one GitHub issue per culture via [`tools/generate_culture_issue_drafts.py`](../../tools/generate_culture_issue_drafts.py).

> **Canonical authenticity**: when assigning a canonical character's identity/skills (Boromir, Théoden, etc.), verify the figure against Tolkien Gateway and pick lore-appropriate names — see [reference/external-resources.md](../reference/external-resources.md) § LOTR/Tolkien.

---

## Architecture — the 3-layer load chain

### Layer 1: Vanilla source

The engine starts with vanilla Bannerlord data:

| File | Role |
|---|---|
| `<game>/Modules/SandBox/ModuleData/lords.xml` | All hero NPCCharacter definitions (Boromir = `lord_1_75`, etc.) with vanilla names/skills/traits/equipment |
| `<game>/Modules/SandBox/ModuleData/sandbox_skill_sets.xml` | 117 vanilla SkillSets (`spc_dandy_skills`, `spc_swordsman_skills_ruler`, etc.) — the actual hero skill-value definitions |
| `<game>/Modules/SandBoxCore/ModuleData/sandboxcore_skill_sets.xml` | More SkillSets, mostly rookie/troop tier |

**Vanilla pattern**: each hero NPCCharacter has `<skills></skills>` (empty) + `skill_template="SkillSet.spc_X_skills"`. The engine reads the named SkillSet and uses those values at hero generation. The empty `<skills>` block is structural — it's NOT where values come from.

### Layer 2: TAOM overlay

TAOM adds three files (all registered in [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml)):

| File | Role | Registered as |
|---|---|---|
| [`Main/_Module/ModuleData/lords.xslt`](../../Main/_Module/ModuleData/lords.xslt) | Transforms vanilla `lords.xml` NPCs (renames, culture flips, gear, skill_template swap) | `<XmlName id="NPCCharacters" path="lords"/>` |
| [`Main/_Module/ModuleData/characters/lords.xml`](../../Main/_Module/ModuleData/characters/lords.xml) | Adds TAOM-NET-NEW NPCs (Ciriel, Dorwen, the entire EW_/WE8_/WE9_/E*/M*/G*/D* ranges) | `<XmlName id="NPCCharacters" path="characters/lords"/>` |
| [`Main/_Module/ModuleData/taom_lord_skill_sets.xml`](../../Main/_Module/ModuleData/taom_lord_skill_sets.xml) | 120 TAOM-owned SkillSets — 35 archetype + 85 canonical-character | `<XmlName id="SkillSets" path="taom_lord_skill_sets"/>` |

**Load order matters.** SubModule.xml line ~60 loads `lords.xslt`-transformed vanilla NPCs FIRST. Line ~120 loads `characters/lords.xml` SECOND. Among additive XML sources with the same ID, **last-loaded wins** — so if `lord_1_40_1` exists in both, the lords.xml version is what the engine uses. The XSLT-transformed version is dead code for that ID.

This is why the Gondor noblewomen rename fix (Catella → Lindariel) lives in `lords.xml`, not in `lords.xslt` — lords.xml is what the player actually sees.

### Layer 3: Engine consumption

At hero generation:
1. Engine instantiates each NPCCharacter as a Hero
2. Reads `skill_template="SkillSet.X"` attribute
3. Looks up SkillSet `X` and copies its `<skill id="..." value="..."/>` entries into the hero's stat sheet
4. **IGNORES** the NPCCharacter's `<skills>` block (yes, even if populated)
5. Reads `<Traits>` block — personality traits (Honor / Generosity / Mercy / etc.) influence AI behavior + diplomacy, vanilla derivation traits (`KnightFightingSkills` / `Commander` / `Politician` / `Manager`) influence skill leveling RATES during gameplay (not initial values)

**Critical fact 1**: explicit `<skills>` on hero NPCCharacters are dead. Only `skill_template` matters for initial values.

**Critical fact 2**: hero skills bake at the moment the engine creates the Hero. Existing save files contain locked-in skill values — XML edits affect NEW campaigns and un-spawned heroes ONLY.

---

## End-to-end workflow

When the user reports a lord-stats problem (or asks for a new pass):

### Step 1 — Identify the NPC

```bash
# What ID does the user mean? Encyclopedia name → NPCCharacter id
grep -rn 'Boromir\|Théoden\|Galadriel' Main/_Module/ModuleData/characters/lords.xml \
    Main/_Module/ModuleData/lords.xslt \
    Main/_Module/ModuleData/heroes.xslt | head
```

For each hit, classify the layer:
- `heroes.xslt` → biographical text only (engine-irrelevant for stats)
- `characters/lords.xml` → TAOM-NET-NEW NPC (wins at runtime)
- `lords.xslt` → vanilla NPC override (wins UNLESS lords.xml has the same ID)

### Step 2 — Classify the problem

| Symptom | Layer to fix | Tool |
|---|---|---|
| Wrong displayed name | lords.xml `name="..."` attribute | Manual edit OR the apply script's culture entry. **If only ENGLISH is wrong, it is fallback drift, not a rename:** `tools/oneoff/sync_lord_name_fallbacks.py` |
| Wrong sex (bearded woman, female lord) | lords.xml `is_female` + `<beard_tags>` + `<BodyProperties key=>`, all three | Manual edit; see the vanilla-id-reuse trap below |
| Wrong culture shown in Info panel | lords.xml `culture="Culture.X"` attribute | Manual edit |
| Wrong race / `race=` attribute | lords.xml `race="..."` attribute | Manual edit |
| Wrong skill values (in Skills panel) | `skill_template` attribute → SkillSet entry | **This guide's workflow** |
| Wrong personality (mercy, honor, etc.) | `<Traits>` block in lords.xml or lords.xslt | This guide's workflow (CULTURES['*']['canonical']) |
| Wrong face / body shape | `<BodyProperties key=...>` | Separate fix (see `feabca2` for the Ciriel/Dorwen pattern) |
| Wrong gear | `<Equipments><EquipmentSet id="..."/></Equipments>` | Out of scope for this guide |

This guide handles ONLY the skill_template + `<Traits>` cases.

### Step 3 — Research (if canonical)

For named Tolkien characters, web-search Tolkien Gateway for lore:

```
WebSearch: "<character name> Tolkien Gateway"
```

Use the findings to inform the skill emphasis (e.g., Boromir = master swordsman + Captain-General → OneHanded 295, Leadership 285; Galadriel = ~8000-yr Noldor + Ring-bearer → Charm 300, Leadership 300; Faramir = Ranger Captain + scholar → Bow 275, Scouting 290, Mercy +2).

If TAOM-invented, look at the heroes.xslt bio text for archetype signals (`"captain"`, `"ranger"`, `"wife of"`, etc.) — the script's `keyword_archetypes` does this automatically.

### Step 4 — Edit `CULTURES` in the script

Open [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py). Find the culture entry. Add or modify the canonical override:

```python
'gondor': {
    'culture_id': 'gondor',  # Vanilla engine ID; LOTR cultures = same name, XSLT cultures = vanilla name
    'lore_name': 'Gondor',
    'race': 'man',           # man / dwarf / elf / orc / uruk_hai / nazgul
    'keyword_archetypes': [  # Bio-keyword → archetype mapping (checked in order)
        (['ranger', 'archer', 'morthond'], 'ranger'),
        # ...
    ],
    'canonical': {           # Per-NPC explicit overrides
        'lord_1_75': dict(    # Boromir
            skills=dict(OneHanded=295, TwoHanded=255, Polearm=240, Bow=160, Crossbow=110, Throwing=170,
                        Riding=260, Athletics=285, Crafting=80, Scouting=210, Tactics=250, Roguery=70,
                        Charm=230, Leadership=285, Trade=110, Steward=180, Medicine=130, Engineering=160),
            traits=dict(Honor=2, Generosity=2, Calculating=0, Mercy=1, Valor=2,
                        Egalitarian=1, Oligarchic=1, Authoritarian=1)),
        # ...
    },
},
```

Three shapes for a canonical entry:
- `dict(skills=..., traits=...)` — full explicit override (most heroes)
- `dict(archetype='lord', skills={...}, traits={...})` — archetype base + partial override (e.g., a knight with one unusual skill)
- `dict(archetype='matriarch')` — archetype-only (no canonical-specific tuning)

### Step 5 — Run the apply script

```bash
python tools/apply_culture_skills_traits.py --all-cultures --apply
```

The script:
- Regenerates `taom_lord_skill_sets.xml` from scratch (35 archetypes + every canonical with `skills=`)
- Walks every NPCCharacter in lords.xml + lords.xslt with `culture="Culture.<id>"`
- For each adult (age ≥14 unless canonical override): swaps the `skill_template` attribute to the matching TAOM SkillSet, populates the `<skills>` and `<Traits>` blocks (documentation only — engine ignores `<skills>`)

Dry-run first if uncertain — omit `--apply` and confirm the touched counts look right.

### Step 6 — Verify XML well-formedness

```bash
python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/characters/lords.xml'); print('lords.xml OK')"
python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/lords.xslt'); print('lords.xslt OK')"
python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/taom_lord_skill_sets.xml'); print('taom_lord_skill_sets.xml OK')"
```

If any fail, the script wrote malformed output — investigate before committing.

### Step 7 — In-game smoke check

The mandatory test. The Bannerlord engine MUST be CLOSED before this — when it's running, `0Harmony.dll` is locked and `./build.ps1` fails. XML data changes don't need a build, but the engine needs a fresh launch to re-read the SkillSets file.

1. Launch Bannerlord, start a new campaign (or use a fresh save).
2. Open Encyclopedia → Heroes → filter to the relevant culture.
3. Click your canonical hero (e.g., Boromir for Gondor work).
4. Check the Skills panel against your SkillSet values + a few levels of growth (typically +5 to +10 above the base).

Boromir verification example: SkillSet base OneHanded=295, expected in-game 295-305. If you see ~145, the SkillSet swap didn't take effect — `skill_template` is still pointing at vanilla `spc_dandy_skills`.

### Step 8 — Commit-split + GitHub issue

Use the `/commit-split` skill. Default split:

| Commit | Files | Type |
|---|---|---|
| 1 | `taom_lord_skill_sets.xml` + `lords.xml` + `lords.xslt` + `SubModule.xml` + `CHANGELOG.md` | `feat(lords-skills): ...` or `fix(lords-skills): ...` |
| 2 (if changed) | `tools/apply_culture_skills_traits.py` + `tools/generate_culture_issue_drafts.py` | `docs(issues): ...` or `chore(tools): ...` |

For brand-new culture work, also create a GitHub issue:

```bash
# Generate per-culture drafts (only if new cultures added)
python tools/generate_culture_issue_drafts.py

# Batch-create
for f in docs/issues-drafts/lords-skills-*.md; do
    title=$(head -1 "$f" | sed 's/^# //')
    gh issue create --title "$title" --body-file "$f" --label 'enhancement' --label 'lords'
done
```

---

## Quick reference — the script

[`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py) has three modes:

| Command | What it does | When to use |
|---|---|---|
| `python tools/apply_culture_skills_traits.py --skillsets-only --apply` | Regenerates `taom_lord_skill_sets.xml` only | After tweaking `BASE_ARCHETYPES` values; no NPCCharacter edits needed |
| `python tools/apply_culture_skills_traits.py --culture gondor --apply` | Re-applies skill_template + skills/Traits blocks for one culture | After editing one culture's canonical or keyword_archetypes |
| `python tools/apply_culture_skills_traits.py --all-cultures --apply` | Full pass across all 17 cultures | After cross-cutting changes; the safe default |

Dry-run (preview without writing): omit `--apply`.

The script is **idempotent** — re-running with no source changes produces no diff.

**MANDATORY pre-flight before ANY `--apply`: verify the generator matches the committed XML.**
`taom_lord_skill_sets.xml` has been hand-edited downstream at least once (the 1f7a7a9a
legendary-lord hierarchy) — a blind regen would have reverted 14 hand-tuned canonical sets and
**deleted** Sauron's/the Witch-King's sets. The generator was synced back in `874e7574`, but the
check stays cheap and permanent: run `--skillsets-only --apply` on a CLEAN tree and require
`git diff` to be empty (or id-sort-only) before layering your change. Non-empty diff = someone
hand-edited the XML again → sync the generator's canonical entries FIRST (see the gotcha table).

**Per-culture `--culture X --apply` is UNSAFE for repointing live lords.** `process_file`
re-resolves every NPC's archetype from bios/keywords, and the live XML carries hand-tuned
assignments it cannot reproduce (the documented 149-lord drift — e.g. the Nazgûl unified on
`taom_nazgul_skills`). For narrow skill_template swaps and inline-doc parity, use
[`tools/repoint_evil_lord_skillsets.py`](../../tools/repoint_evil_lord_skillsets.py) instead:
culture-scoped template swap maps + a full inline-`<skills>` sync read straight from the sets
XML, post-condition + idempotency checked. Reserve `--culture X --apply` for cultures whose
assignments the generator genuinely owns end-to-end.

---

## Archetype catalog

74 archetypes in [`BASE_ARCHETYPES`](../../tools/apply_culture_skills_traits.py): the lore
archetypes below plus **per-culture balance variants** (see the subsection after Other Cultures).
Each defines 18 skill values + 8 personality trait values. The catalog shows the primary
specialty + Leadership + key trait alignment. **Numbers below are as of the 2026-07-03 retune
(#326); `BASE_ARCHETYPES` is the source of truth when they drift.** Army-size mechanics: party
size scales with the leader's **Steward** (+0.25/point, `StewardPartySizeBonus`); Leadership
feeds morale, garrisons, and party size only via perks.

### Men of the West (Gondor / Rohan / Dale)

| Archetype | Primary | Leadership | Key Traits | Use when |
|---|---|---|---|---|
| `lord` | OneHanded 220 / Polearm 210 | 240 | Honor +2, Valor +2 | Region-ruling adult male, military commander |
| `knight` | OneHanded 230 / Riding 250 | 160 | Honor +2, Valor +2 | Adult cavalry warrior, vassal / heir-apparent |
| `ranger` | Bow 270 / Scouting 270 / Athletics 260 | 190 | Mercy +2, Valor +2 | Faramir archetype: woodsman, ranger captain |
| `lady` | Charm 240 / Steward 240 / Medicine 210 | 160 | Generosity +2, Mercy +2 | Adult female noble / wife, court manager |
| `matriarch` | Charm 285 / Steward 285 / Medicine 245 | 220 | Calculating +2, Mercy +2 | Elder female (60+), wisdom peak (Tac 212 — dale-only since the gondor fork) |
| `elder_lord` | Tactics 295 / Leadership 286 | 286 | Calculating +2, Oligarchic +2 | Older male (60+), retired warrior + wise counsel (gondor-exclusive; carries the #326 gondor deltas) |
| `young_lord` | OneHanded 160 / Riding 190 | 120 | Honor +1, Valor +2 | 14-25 male heir, trained but green |
| `young_lady` | Charm 180 / Steward 170 | 120 | Mercy +2 | 14-25 female, courtly training |
| `steward` | Steward 275 / Charm 260 / Trade 240 | 220 | Calculating +2, Oligarchic +2 | Húrioneth archetype: administrator / diplomat |
| `errand_rider` | Riding 280 / Scouting 270 | 140 | Honor +2, Valor +2 | Hirgon archetype: messenger / scout |
| `rider` | Polearm 255 / Riding 270 | 212 | Honor +2, Generosity +2 | Rohirric heavy cavalry (low Crossbow; rohan-exclusive, carries #326 rohan deltas) |
| `shieldmaiden` | OneHanded 240 / Polearm 225 / Riding 255 | 192 | Honor +2, Valor +2 | Éowyn archetype: combat-capable noblewoman |
| `horse_breeder` | Riding 290 / Crafting 190 / Trade 210 | 182 | Honor +2, Generosity +2 | Rohan grasslands horsemaster |
| `dale_lord` | Bow 260 / Trade 210 | 235 | Honor +2, Valor +2 | Bardings noble; bowman-merchant emphasis |
| `dale_bowman` | Bow 270 / Athletics 245 | 180 | Honor +2, Valor +2 | Bardings non-noble bowman |

### Dwarves (Erebor)

| Archetype | Primary | Leadership | Key Traits | Use when |
|---|---|---|---|---|
| `dwarf_king` | TwoHanded 280 / Tactics 405 / Steward 348 | 417 | Honor +2, Oligarchic +2 | Dáin II tier: king under the mountain |
| `dwarf_lord` | TwoHanded 255 / Tactics 355 / Steward 293 | 352 | Honor +2, Oligarchic +2 | Standard dwarven noble |
| `dwarf_warrior` | TwoHanded 265 / Tactics 320 | 282 | Honor +2, Valor +2 | Iron Hills veteran |
| `dwarf_lady` | Steward 333 / Tactics 310 / Crafting 255 | 317 | Generosity +2, Mercy +2 | Adult female dwarf (combat capable but lower) |
| `dwarf_young` | Tactics 280 / TwoHanded 190 | 257 | Honor +2, Valor +2 | Apprentice dwarf (still 60+ years old in lore terms) |

> Dwarves got the #326 command retune (S+73 / L+127 / T+140): resolved averages exactly 280 Stw /
> 300 Led / 310 Tac — the premier non-elf commanders by design. Sets are erebor-exclusive.

### Elves (Mirkwood / Rivendell / Lothlórien)

| Archetype | Primary | Leadership | Key Traits | Use when |
|---|---|---|---|---|
| `elf_king` | Polearm 295 / Bow 295 / Steward 385 | 362 | Honor +2, Calculating +2 | Thranduil tier: regional king |
| `elf_queen` | Charm 295 / Steward 390 / Medicine 285 | 362 | Honor +2, Calculating +2 | Galadriel-tier (note: she gets Charm 300 via canonical override) |
| `elf_lord` | Bow 275 / OneHanded 270 / Steward 355 | 327 | Honor +2, Mercy +2 | Standard centuries-trained noble |
| `elf_warrior` | OneHanded 275 / Polearm 275 / Steward 300 | 292 | Honor +2, Valor +2 | Glorfindel-tier warrior (canonical bumps OneHanded to 295) |
| `elf_archer` | Bow 295 / Scouting 290 / Steward 300 | 282 | Honor +2, Valor +2 | Legolas archetype |
| `elf_lady` | Charm 270 / Steward 365 / Medicine 260 | 272 | Calculating +2, Mercy +2 | Elf noblewoman (still combat-capable) |
| `elf_young` | Bow 230 / Athletics 240 / Steward 290 | 252 | Honor +2, Valor +2 | Junior elf (apparent age 20s — actually centuries) |

> Elf sets carry the full army-stat program: Steward +100 (#323) then Leadership +72 / Tactics +61
> (#326) — culture resolved averages ≈300 Led/Tac, 305–340 Steward. They are exclusive to the three
> elf cultures (verified — safe to edit in place).

### Mordor / Dol Guldur / Gundabad / Isengard

| Archetype | Primary | Leadership | Key Traits | Use when |
|---|---|---|---|---|
| `orc_chieftain` | OneHanded 275 / TwoHanded 265 / Roguery 240 | 270 | Honor -2, Mercy -2, Authoritarian +2 | Uglûk / Lurtz / Grishnâkh tier |
| `orc_warrior` | OneHanded 235 / TwoHanded 220 / Roguery 200 | 160 | Honor -2, Valor +2 | Standard orc combatant |
| `orc_berserker` | TwoHanded 285 / Athletics 275 | 130 | Honor -2, Valor +2 | Frothing close-combat specialist |
| `orc_scout` | Bow 255 / Scouting 270 / Roguery 240 | 140 | Calculating +2, Mercy -2 | Stealth/raid orc |
| `orc_warg` | Riding 285 / Scouting 255 | 180 | Honor -2, Authoritarian +1 | Warg-rider (Sharku-tier) |
| `orc_female` | Crafting 180 / Steward 180 / Roguery 210 | 150 | Calculating +2, Mercy -2 | Female orc (Mordor has many) |
| `nazgul` | Tactics 295 / Leadership 295 / Charm 280 | 295 | Honor -2, Authoritarian +2 | Ringwraith; engine reads Charm as terror not warmth |
| `black_numenorean` | Charm 270 / Leadership 260 / Steward 255 | 260 | Calculating +2, Authoritarian +2 | Fallen Númenórean noble |
| `bn_sorceress` | Charm 275 / Steward 260 / Medicine 250 / Roguery 250 | 230 | Calculating +2, Authoritarian +2 | BN female mage-noble |

### Other Cultures

| Archetype | Primary | Leadership | Use when |
|---|---|---|---|
| `dunland_warrior` | OneHanded 245 / TwoHanded 215 / Athletics 255 | 100 | Norse-themed shieldmaiden (#322 army-size nerf) |
| `dunland_raider` | Bow 200 / Athletics 265 / Roguery 240 | 180 | Hillfolk raid leader (dunland lords moved to `dunland_marauder`; kept at 180 for any future non-dunland user) |
| `dunland_brenin` | OneHanded 265 / Tactics 250 | 130 | Brenin = chieftain (Brenin Wulf tier; #322 nerf) |
| `haradrim_lord` | OneHanded 235 / Riding 260 / Trade 220 | 235 | Desert noble |
| `haradrim_cav` | Polearm 240 / Riding 280 | 180 | Mounted Haradrim |
| `mumak_rider` | Polearm 255 / Bow 240 / Riding 255 | 210 | Mûmakil crew |
| `desert_lady` | Charm 255 / Steward 240 / Trade 235 | 170 | Haradrim noblewoman |
| `variag_lord` | Polearm 245 / Riding 275 / Bow 240 | 225 | Khand cavalry |
| `variag_lady` | Charm 210 / Steward 220 / Riding 235 | 170 | Variag noblewoman |
| `easterling_lord` | Bow 260 / Riding 275 / Tactics 235 | 235 | Wainrider noble |
| `easterling_archer` | Bow 275 / Scouting 255 / Riding 265 | 170 | Horse-archer |
| `easterling_lady` | Charm 215 / Steward 225 / Riding 215 | 160 | Mongol-flavored noblewoman |
| `corsair_lord` | OneHanded 260 / Trade 265 / Roguery 265 | 255 | Umbar BN pirate noble |
| `corsair_captain` | OneHanded 240 / Trade 220 / Roguery 240 | 215 | Umbar ship captain |

### Per-culture balance variants (`archetype_alias`)

The 2026-07 army-size balance arc (#322/#323/#326) introduced **per-culture variant archetypes**
for sets shared across cultures. The base sets stay untouched for every culture NOT in the
balance pass; a variant is an exact copy with only the balance-relevant skills changed, and the
culture's `CULTURES` entry carries an `archetype_alias` dict so archetype resolution lands on the
variant on any future regen:

```python
'gondor': {
    'archetype_alias': {'knight': 'gondor_knight', 'lady': 'gondor_lady', ...},
```

| Variant family | Cultures | Deltas vs base | Why forked |
|---|---|---|---|
| `north_orc_{chieftain,warrior,female}` | gundabad, dolguldur, goblin, mistymountainorcs | Led −95/−85/−90 (#322), Stw −53 (#326) | base orc trio shared with mordor + isengard (untouched) |
| `dunland_{knight,lady,young_lord,young_lady,marauder}` | dunland | Led cuts to 90/80/55/55/80 (#322) | knight/lady/young sets shared with 8 cultures; raider shared history |
| `gondor_{knight,lady,young_lady,young_lord,matriarch,lord}` | gondor | S+8 / L+26 / T+25 (#326) | same shared man sets |
| `dale_{knight,lady,young_lady,young_lord}` | dale | T+22 (#326) | same |
| `rohan_{knight,lady,young_lady,young_lord}` | rohan | S+7 / L+12 / T+23 (#326) | same |

Six cultures currently carry `archetype_alias`: gondor, dale, rohan, dunland, dolguldur,
gundabad. (goblin + mistymountainorcs have NO `CULTURES` entry — their lords were repointed
directly by `tools/repoint_evil_lord_skillsets.py` and no regen path touches them.)

**Rules when balancing a culture whose lords sit on shared sets:**
1. Never edit the shared base set in place — fork a variant, alias, repoint. The #326 pass
   verified shaghana/abanissa/rhun/harad/umbar/khand byte-identical afterward; keep it that way.
2. Canonical (per-NPC) sets are always safe in place — bump them uniformly with the culture's
   delta so the hand-tuned hierarchy's internal ORDER survives (#326 moved 63 canonicals this way).
3. Average targets land exactly only for single-culture sets. Cultures SHARING sets (the 3 elf
   realms; the 4 north-orc cultures) hit the target as a pooled average with a ±10 per-culture
   spread from set-mix composition — accept it; per-culture exactness requires absurd
   per-canonical residuals (Thranduil at Led ~500 to drag mirkwood's warrior-heavy mix up).

### Power thresholds (post-#326 tiering)

The pre-2026-07 "300 is legendary" scale is obsolete — the army-stat program deliberately pushed
command stats (Steward/Leadership/Tactics) past it. Current tiering for the COMMAND axis:

- **≤100** — army-size-nerfed evil cultures (north orcs Led 60–175 / Stw 77–147; dunland Led 55–130)
- **150–250** — baseline human/orc command tier (base shared sets; mordor/isengard unchanged)
- **~280–310** — retuned free-folk command averages (gondor 200/200/190 S/L/T, rohan 180/180/190,
  erebor 280/300/310, elves ≈300 Led/Tac, elf Steward 300–390)
- **340–420** — rulers + canonical legends (Galadriel Stw 415/Led 397, Elrond 400/382, dwarf_king 417 Led)

COMBAT skills still follow the old scale (270–330 = canonical hero specialty; Legolas Bow 330,
Sauron 330s). Party size = leader Steward × 0.25; Leadership feeds morale + garrison + perks
(`UltimateLeader` scales per point above the epic threshold), Tactics drives autoresolve.

---

## Recipe: adding a canonical character

Worked example — "Add Cirdan as recruitable Grey Havens hero `lord_R3_2`":

1. Confirm the NPC exists. Grep `Main/_Module/ModuleData/characters/lords.xml` and `lords.xslt` for `lord_R3_2`. If it doesn't exist, create the NPCCharacter first (out of scope for this guide).
2. Edit [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py). Find `CULTURES['rivendell']['canonical']`. Add:

   ```python
   'lord_R3_2': dict(  # Cirdan the Shipwright — eldest Elf in Middle-earth, ~13,000 years old, master mariner
       skills=dict(OneHanded=265, TwoHanded=225, Polearm=290, Bow=290, Crossbow=200, Throwing=210,
                   Riding=240, Athletics=275, Crafting=295, Scouting=290, Tactics=295, Roguery=110,
                   Charm=290, Leadership=290, Trade=280, Steward=290, Medicine=265, Engineering=290),
       traits=dict(Honor=2, Generosity=2, Calculating=1, Mercy=2, Valor=2,
                   Egalitarian=1, Oligarchic=1, Authoritarian=0)),
   ```

3. Run:

   ```bash
   python tools/apply_culture_skills_traits.py --all-cultures --apply
   ```

   The script auto-generates a new SkillSet `taom_canonical_lord_R3_2_skills` and points Cirdan's `skill_template` at it.

4. Verify XML well-formedness (Step 6 above).
5. In-game Encyclopedia check (Step 7).
6. Commit + open a GitHub issue if this is part of a bigger pass.

---

## Recipe: adding a new archetype

If existing archetypes don't fit (e.g., you need `corsair_admiral` distinct from `corsair_lord`):

1. Edit [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py). Add to `BASE_ARCHETYPES`:

   ```python
   'corsair_admiral': dict(
       skills=dict(OneHanded=280, TwoHanded=230, Polearm=265, Bow=210, Crossbow=190, Throwing=210,
                   Riding=190, Athletics=275, Crafting=170, Scouting=255, Tactics=290, Roguery=275,
                   Charm=270, Leadership=290, Trade=285, Steward=265, Medicine=170, Engineering=240),
       traits=dict(Honor=-1, Generosity=1, Calculating=2, Mercy=-1, Valor=2,
                   Egalitarian=0, Oligarchic=2, Authoritarian=2)),
   ```

2. Reference it from a canonical entry OR a `keyword_archetypes` rule:

   ```python
   'umbar': {
       ...
       'keyword_archetypes': [
           (['admiral', 'high captain'], 'corsair_admiral'),  # new entry first (higher priority)
           (['captain', 'pirate'], 'corsair_captain'),
           ...
       ],
       'canonical': {
           'lord_U_HIGH_CAPTAIN': dict(archetype='corsair_admiral'),
       },
   },
   ```

3. Re-run the script. A new SkillSet `taom_corsair_admiral_skills` is auto-generated.

4. Update [`docs/ai-includes/lord-skills-authoring.md`](./lord-skills-authoring.md) (this file) — add a row to the archetype catalog.

---

## Recipe: adding a new culture

This guide handles the skills layer. For the broader culture (armor + troops + recruitment + map presence), follow [`new-culture-authoring.md`](./new-culture-authoring.md) FIRST. The skills work is the LAST step:

1. Edit [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py). Add to `CULTURES`:

   ```python
   'arnor': {  # hypothetical northern Dúnedain
       'culture_id': 'arnor',         # The Culture.X StringId
       'lore_name': 'Arnor (Dúnedain of the North)',
       'race': 'man',                  # man / dwarf / elf / orc / uruk_hai / nazgul
       'keyword_archetypes': [
           (['ranger', 'dúnedain'], 'ranger'),
           (['chieftain', 'lord of'], 'lord'),
           (['lady of', 'wife of'], 'lady'),
       ],
       'canonical': {
           'lord_AR_1': dict(skills=..., traits=...),  # Aranarth
           # ...
       },
   },
   ```

2. The script auto-discovers NPCs by `culture="Culture.arnor"`. No SubModule.xml change needed for culture additions — only if you're adding a NEW XML file (which `apply_culture_skills_traits.py` doesn't).

3. Run `--all-cultures --apply`. Verify. Commit. Open one GitHub issue using [`tools/generate_culture_issue_drafts.py`](../../tools/generate_culture_issue_drafts.py).

---

## Verification checklist

Before declaring done:

- [ ] All three XML files well-formed (Python `ET.parse` smoke test).
- [ ] Script is idempotent: re-running with no source changes produces no diff (`git status` shows clean after second `--apply`).
- [ ] Generator-vs-committed drift pre-check ran BEFORE your change (regen on clean tree → empty diff).
- [ ] If a balance pass: non-target cultures' averages verified byte-identical (the `analyze_lord_balance`-based table), and shared sets forked rather than edited in place.
- [ ] In-game Encyclopedia spot-check on at least one canonical hero per touched culture.
- [ ] If touched a Leadership-285+ character, confirm the +1 party-size perk shows in-game (Encyclopedia → Perks).
- [ ] Children (age <14) still using `spc_*_skills_rookie` vanilla SkillSets unless they're canonical overrides (Nazgûl with placeholder age 9/11 are the exception — they bypass the child skip).
- [ ] `git status` shows expected files only: `lords.xml`, `lords.xslt`, `taom_lord_skill_sets.xml`, `CHANGELOG.md`, possibly `SubModule.xml` if registering new files, possibly the script itself.
- [ ] CHANGELOG.md updated.
- [ ] If new culture or major refactor, a GitHub issue exists.

---

## Gotchas + memory references

Real failure modes from past sessions. Read these before you ship.

| Trap | Source memory | Mitigation |
|---|---|---|
| `re.sub` with `r'\1'` followed by a digit-starting string silently corrupts output (parses as `\10` backref) — corrupted 24 BodyProperties lines in one apply | `feedback_re_sub_backref_followed_by_digit.md` | Use lambda: `pattern.sub(lambda m: m.group(1) + new + m.group(2), text)` or `\g<N>` |
| Renaming an NPC and forgetting `settlements.xml` lore-text references — "Lady Vanyalos" shipped in town_EW7 flavor text after the rename | `feedback_rename_grep_all_moduledata.md` | After any rename, `grep -rn "OLD_NAME" Main/_Module/ModuleData/` — audit every hit, not just `characters/` |
| **Explicit `<skills>` block on hero NPCs is ignored by the engine** — only `skill_template` matters; this is the bug that led to the SkillSet rewrite | NEW: `feedback_skill_template_overrides_explicit_skills.md` | Always swap `skill_template` to a TAOM SkillSet, never hand-edit `<skills>` blocks for heroes |
| Same-ID NPCs in both `lords.xslt` (vanilla transform) and `characters/lords.xml` (TAOM additions) → last-loaded wins, which is `lords.xml` per SubModule.xml load order | `harness-facts.md` | If a fix isn't taking effect, check whether lords.xml has the same ID and edit there instead of (or in addition to) lords.xslt |
| Children (age <14) skipped by the script — appropriate for toddlers, but breaks for Nazgûl with placeholder ages 9/11 | Script's `process_file` | Canonical entries auto-bypass the age skip. Always add Nazgûl / immortals to `CULTURES[*]['canonical']` even if just `dict(archetype='nazgul')` |
| `0Harmony.dll` lock when Bannerlord is running → `./build.ps1` fails | `.claude/rules/environment-failures.md` | Close Bannerlord OR skip the build — XML data changes don't need it. Use the Python XML parse smoke test instead |
| Save-compat: hero skills bake at hero CREATION | n/a (engine behavior) | Existing campaigns keep old stats. New campaigns + un-spawned heroes use the new SkillSets. Flag this in PR descriptions and CHANGELOG |
| Forgetting to register a new XML file in SubModule.xml → engine doesn't load it → fix has no effect | n/a | If you create a new ModuleData XML file, add an `<XmlNode>` entry to `Main/_Module/SubModule.xml` with the appropriate `id=` (SkillSets / NPCCharacters / etc.) and `path=` (file basename without `.xml`) |
| **Generated XML hand-edited downstream** — 1f7a7a9a hand-tuned `taom_lord_skill_sets.xml`; a later blind regen would have reverted 14 canonical sets and DELETED Sauron's | commit 874e7574 (sync) | Before any `--apply`: regen on a clean tree, require empty `git diff`. Drift found → sync the generator's canonical entries to the live XML FIRST (acceptance: regen == committed semantically) |
| **`--culture X --apply` clobbers hand-tuned assignments** — per-NPC re-resolution can't reproduce the live 149-lord drift (unified Nazgûl etc.) | #322 design | Narrow swaps/parity go through `tools/repoint_evil_lord_skillsets.py` (template swap maps + full inline sync from the sets XML), never through `process_file` on a drifted culture |
| **Editing a shared set in place bleeds into other cultures** — the orc trio feeds 6 cultures, `taom_knight_skills` feeds 8 | #322/#326 design | Fork a per-culture variant + `archetype_alias` + repoint (see "Per-culture balance variants"). Verify non-target culture averages byte-identical afterward |
| **Stale culture maps after reculturing** — `rebalance_lords.CULTURE_MAP` said battania→mirkwood long after battania became Khand: Variag lords wore ELF cultural mods and polluted every "mirkwood" report (71 = 30 elves + 41 Variags) | commit 12b06e47 | When a vanilla culture is re-themed, grep EVERY tool for the old mapping (`CULTURE_MAP`, `CULTURES`, validators). battania=khand, empire=dunland, sturgia=dale, vlandia=rohan, khuzait=rhun |
| **Child lords (age<14) must stay on vanilla `spc_*_rookie` templates** — two are literally named "PlaceHolder Child"; adult sets on a 6-year-old is wrong | #323 follow-up | The generator's age<14 skip is the codified rule; canonical entries bypass it ONLY for placeholder-age immortals (Nazgûl). Template-less ADULTS (lord_R3_1 case) get a real set via `TEMPLATE_ASSIGN` |
| **Diff re-anchoring reads as phantom edits** — a big inline-skill sync made `git diff` present lord_BC2_2's face-tag block as removed+re-added; a "surgical hunk exclusion" based on that presentation nearly corrupted the index | #326 session | Before staging surgery, compare the actual BLOCK content between HEAD and worktree (`git show HEAD:file` + regex), not the diff's presentation of it |
| **Reusing a vanilla id inherits its sex.** `lord_WE8_c` (vanilla Icratia, a woman) became Pelendur son of Golasgil and stayed `is_female="true"`; `lord_1_46_1` (vanilla Seorgys, a man) became Thorwen and stayed male with a beard; `lord_4_6` (Countess Calatild) became Grimbold. The rename lands, the sex does not | `a00086da`, `3c7f4e25` | Treat `is_female`, `<beard_tags>` and `<BodyProperties key=>` as ONE unit. `<beard_tags>` is what actually renders facial hair, so flipping the attribute alone leaves a bearded woman. The bio in `heroes.xslt` says son / daughter / wife and is the cheapest spec to check against. `LordNameAndSexConsistencyTests` pins the female-with-beard half |
| **There is no English `Languages/` folder**, so the inline `{=key}Literal` IS the English text. A rename that updates `taom_xslt_strings.xml` and the 12 locales but not `lords.xml` is invisible in 12 of 13 languages; 13 lords had drifted, one showing "Icratia" to English players only | `a00086da` | `LordNameAndSexConsistencyTests` asserts every fallback equals the registered English text. `LanguageFileCoverageTests` cannot catch it: it checks a key HAS a row, not what the row says. Also note `translate_with_claude.py` refills only rows still equal to English, so CHANGING English under an existing id propagates to nothing |
| **`complete_lords_xslt.py` restores vanilla `is_female` when you DELETE the attribute.** An override is honoured only when present, and the male convention is omission, so deleting the line reads as "never overridden" and the next `--apply` copies vanilla's `true` back | `a00086da`, `3c7f4e25` | Force the absence in `GENDER_OVERRIDES` (`lord_4_6`, `lord_WE8_c` are there). Prove it by running the merge in-process and reading the attribute, not by eyeballing the file. The guard covers `lords.xslt` ONLY; `characters/lords.xml` wins at runtime and has no regenerator, so no guard |

---

## File map

| Path | Role |
|---|---|
| [`tools/apply_culture_skills_traits.py`](../../tools/apply_culture_skills_traits.py) | Generator (source of truth — `BASE_ARCHETYPES` + `CULTURES` dicts incl. `archetype_alias`) |
| [`tools/repoint_evil_lord_skillsets.py`](../../tools/repoint_evil_lord_skillsets.py) | Balance-pass repoint/parity: culture-scoped skill_template swaps, full inline-`<skills>` sync from the sets XML, `TEMPLATE_ASSIGN` for template-less adults (`--dry-run`/`--apply`) |
| [`tools/author_elf_lords.py`](../../tools/author_elf_lords.py) | One-off new-lord authoring reference (#324): full NPCCharacter+Hero+Faction wiring, inline skills from live SkillSets, donor face keys, clan party-slot math |
| [`tools/analyze_lord_balance.py`](../../tools/analyze_lord_balance.py) | Read-only per-culture stats + perk review; the verification lens for every balance pass |
| [`tools/generate_culture_issue_drafts.py`](../../tools/generate_culture_issue_drafts.py) | Per-culture GitHub issue draft generator |
| [`Main/_Module/ModuleData/taom_lord_skill_sets.xml`](../../Main/_Module/ModuleData/taom_lord_skill_sets.xml) | Generated — 120 SkillSets (DO NOT hand-edit) |
| [`Main/_Module/ModuleData/characters/lords.xml`](../../Main/_Module/ModuleData/characters/lords.xml) | TAOM-NET-NEW NPCCharacters (wins over XSLT-transformed vanilla at runtime) |
| [`Main/_Module/ModuleData/lords.xslt`](../../Main/_Module/ModuleData/lords.xslt) | Vanilla NPCCharacter transform |
| [`Main/_Module/ModuleData/heroes.xslt`](../../Main/_Module/ModuleData/heroes.xslt) | Biographical text overrides (engine-irrelevant for stats, but useful for archetype keyword detection) |
| [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) | Load-order registration |
| [`docs/features/lord-skills.md`](../features/lord-skills.md) | Feature doc summary |
| [`docs/issues-drafts/INDEX.md`](../issues-drafts/INDEX.md) | 18 culture issue drafts + batch-create command |

---

## Historical reference

The system was built across three commits:

| Commit | Date | Work |
|---|---|---|
| `feabca2` | 2026-05-26 | Gondor lord review: Amrothos clan fix, 3 noblewomen renames + culture flips, Ciriel body-key, 24-female face-key sweep |
| `8665ca6` | 2026-05-27 | 16-culture skills+traits sweep — ~780 NPCs populated `<skills>` + `<Traits>` (later proved engine-irrelevant for `<skills>`, but still authoritative for `<Traits>`) |
| `c5dc168` | 2026-05-27 | TAOM-owned SkillSets — created `taom_lord_skill_sets.xml`, swapped `skill_template` on every adult lord, in-game verified on Boromir |

GitHub issues: #228-#245 (one per culture).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lord-perk-review.md](../features/lord-perk-review.md)
- [docs/features/lord-skills.md](../features/lord-skills.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/lords-and-heroes.md](../modding/lords-and-heroes.md)
- [docs/modding/skill-sets.md](../modding/skill-sets.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
