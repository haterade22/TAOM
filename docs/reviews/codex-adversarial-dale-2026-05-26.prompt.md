# Codex Adversarial Review -- Dale culture (armor + 27-troop tree)

You are reviewing the **Dale** feature in TAOM (Tales From the Age of Men), a LOTR total-conversion mod for Mount & Blade II: Bannerlord 1.4.5. Dale = the Bardings of Tolkien, ruling rebuilt Dale and Lake-town. This change adds Dale-themed armor (163 mesh items modeled by Solus) and a 27-troop tree on the vanilla `Culture.sturgia` id (renamed "Barding" via XSLT).

The work is ~95% XML data authoring + ~5% C# (a single ~20-line static initialization method added to an existing service). Five prior Claude `/deep-review` agents already passed Standards/API-Compatibility/Efficiency/Completeness/DataFlow with one MEDIUM follow-up (lord rosters still use vanilla Sturgian items -- documented as known limitation). Your job: find what those five agents missed.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". "dale" is NOT a valid culture id either -- use "sturgia".

## READ FIRST

- `docs/features/dale.md` -- full feature doc with troop tree design, lore sourcing, known limitations
- `CHANGELOG.md` 2026-05-26 entry -- session summary
- `.claude/rules/troops.md` -- 7-step checklist for troop changes (was followed)
- `.claude/rules/xml-data.md` -- ID conventions, EquipmentRosters schema
- Memory file `feedback_multi_folder_id_uniqueness.md` -- canonical-folder rule per item-prefix

## Known Suspects (CONFIRM or DISPUTE each)

1. **`has_gender_variations="true"` auto-slim engine behavior.** 6 chest items have `has_gender_variations="true"` because Solus authored a paired `<id>_slim` mesh. The Python generator skips authoring the `_slim` mesh as a separate item, relying on the engine to auto-swap. Decompile `TaleWorlds.Core.ItemObject` / `Armor` / equipment-resolve path in v1.4.5 to CONFIRM the engine actually looks for `<id>_slim` mesh when `has_gender_variations="true"` and the agent is female. If it does NOT (e.g., needs a separate item entry with different mesh attribute), all 6 affected chest items will fail to render on female agents. The 6 affected items: `sk_dale_chest_archer_a02/b02`, `sk_dale_chest_chivalry_a03/a04/b03/b04`.

2. **Skill curve calibration.** Dale `dale_kings_bowman` (T8 elite archer) has `Bow=230`. Vanilla Bannerlord caps skills at 250 (or 270 for nobles per some sources). Is 230 too high? Does it hit any vanilla skill ceiling? Compare against TAOM Rohan elite archer (`rohan_eastfold_veteran_bowman` in `troops/troops_rohan.xml`) to gauge.

3. **Vanilla bow ID mapping for Dale archer line.** Dale uses these vanilla bows: `hunting_bow`, `mountain_hunting_bow`, `lowland_yew_bow`, `lowland_longbow`, `noble_bow`. Verify each exists in `SandBoxCore/ModuleData/items/weapons.xml` AND that the tier progression is monotonic (hunting < mountain_hunting < lowland_yew < lowland < noble). If any are mis-tiered, an archer T5 might wield a stronger bow than T6.

4. **`<EquipmentSet equipmentType="Civilian">` on standalone rosters.** Per `xml-data.md` "EquipmentRosters Schema" rule: standalone EquipmentRosters in `equipmentsets/taom_equipment_sets_*.xml` need `equipmentType="Civilian"` on civilian sets. This change DID NOT modify `taom_equipment_sets_dale.xml`, but verify the existing civ rosters there (e.g., `dale_civ_template_default_a..`) are tagged correctly. The new XSLT binding `default_civilian_equipment_roster="EquipmentRoster.dale_civ_template_default_a"` will malfunction if the existing roster is missing the tag.

5. **CultureMap["sturgia"] cross-feature collision risk.** The new `InitializeDaleCulture` method in `VolunteerRecruitmentService.cs` writes `CultureMap["sturgia"] = [...]`. Grep the file for any OTHER write to `CultureMap["sturgia"]` (or via a different syntax like `.Add("sturgia", ...)`). Static-ctor execution order would determine the winner. Also check if any settlement-level map (`SettlementMap`) entry exists for a Sturgia settlement that might bypass the culture pool entirely. If a stale Sturgia binding exists from before this work, Dale recruitment would silently fail.

6. **Solus's mesh-name typos.** The generator deliberately preserves `chivlary` (typo for chivalry, used on boots/gauntlets/helmets/shoulders) and `chivalry` (correct spelling, used only on chest). The engine binds meshes by EXACT name. If the Armory `.tpac` files have different spellings than the authored XML, the mesh will not render. Grep all 5 dale armor XML files for `chivlary` and `chivalry` counts and verify them against `tools/dale_armor_meshes.txt`.

7. **Inline-equipment `Item.xxx` namespace vs standalone EquipmentRoster `Item.xxx`.** Inline equipment in `troops_dale.xml` uses `<equipment slot="..." id="Item.xxx" />`. Standalone rosters in `taom_equipment_sets_dale.xml` use `<Equipment slot="..." id="Item.xxx" />`. Two distinct schemas; verify the inline form in troops_dale.xml uses lower-case `<equipment>` (matching vanilla `spnpccharacters.xml`) and not capital-E. (One of the Erebor pattern examples in CLAUDE.md uses lower-case.)

## Files to Review

### TAOM C# (1 file modified, 1 file with new tests)

- `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` — `InitializeDaleCulture()` static method added at lines ~50-70, call added at line ~47 in static ctor
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` — 4 Dale-culture tests added at the end of the file

### TAOM XML data (new)

- `Main/_Module/ModuleData/troops/troops_dale.xml` — 27 NPCCharacters

### TAOM XML data (modified)

- `Main/_Module/ModuleData/spcultures.xslt` lines 1138-1235 -- the `Culture[@id='sturgia']` block, 9 new military attributes added inside the existing XSLT template
- `Main/_Module/ModuleData/taom_partyTemplates.xml` lines 962-1056 -- 9 new MBPartyTemplate entries
- `Main/_Module/SubModule.xml` -- one new `<XmlNode><XmlName id="NPCCharacters" path="troops/troops_dale"/></XmlNode>` block

### Python generators (author-time tools)

- `tools/generate_dale_armor.py` -- parses mesh manifest, writes 5 Armory XML files
- `tools/generate_dale_troops.py` -- 27-troop hardcoded data structure, writes troops_dale.xml
- `tools/dale_armor_meshes.txt` -- mesh ID manifest
- `tools/validate_all_troop_refs.py` -- one-line change adding "dale" to cultures list

### LOTRLOME_Armory (external module)

- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\SubModule.xml` -- one new Items XmlNode for `LOTRLOME_items/dale`
- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\dale\*.xml` -- 5 new files (head/body/leg/arm/shoulder armors), 163 items total

### Existing files referenced (not modified, but data flow depends on them)

- `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_dale.xml` -- 11 existing lord rosters (`dale_bat_template_medium_a..e`, `dale_civ_template_default_a..`)
- `Main/_Module/ModuleData/characters/npcs_dale.xml` -- 26 existing Dale notables
- `tools/tpac_skeleton_scan.py` -- existing tool (unchanged); used as mesh-ID source for the manifest

### Docs

- `docs/features/dale.md` -- full feature doc

## REQUIRED SECTIONS

### VANILLA CODE

Paste decompiled v1.4.5 vanilla code for the following, as code blocks. **Use `taom-src.ps1` for authoritative signatures, do NOT trust `E:\Decompiled_Bannerlord\` for signature claims (it is v1.4.5 but the installed-DLL `taom-src` cache is canonical):**

1. `TaleWorlds.Core.CultureObject.Deserialize` (or wherever `basic_troop`, `elite_basic_troop`, `melee_militia_troop`, `ranged_militia_troop`, `melee_elite_militia_troop`, `ranged_elite_militia_troop`, `default_party_template`, `default_battle_equipment_roster`, `default_civilian_equipment_roster` get parsed from XML) -- so you can confirm all 9 XSLT-set attributes deserialize correctly.

2. `TaleWorlds.Core.Armor` (the `ItemComponent` armor data class) -- specifically how `has_gender_variations` is consumed. Where does the engine look for the `_slim` mesh? Is it derived from the item id, the mesh attribute, or something else?

3. `TaleWorlds.CampaignSystem.PartyTemplateObject.Deserialize` and `PartyTemplateStack` -- to confirm the `min_value` / `max_value` / `troop` attribute names are correct for v1.4.5.

If any of these signatures differ from what the change assumes, raise a HIGH finding.

### CONFIG CROSS-REFERENCE

Run these checks against the troop tree:
- Every `<upgrade_target id="NPCCharacter.dale_xxx" />` in `troops_dale.xml` -- does the referenced troop exist in the same file?
- Every troop referenced in the 9 new `taom_partyTemplates.xml` entries -- does it exist in `troops_dale.xml`?
- Every `Item.xxx` reference in `troops_dale.xml` -- does it exist in (a) `LOTRLOME_items/dale/*.xml` for `sk_dale_*`, (b) `SandBoxCore/ModuleData/items/weapons.xml` for `sturgia_*`, `northern_spear_*`, bow IDs, javelin IDs, (c) `LOTRLOME_items/LOTRAOM_horses.xml` for `rohan_horse_armor_scalemail`, or (d) somewhere for `charger`, `sturgia_horse`, `chain_horse_harness`, `heavy_horsemans_kite_shield`, `horsemans_heater_shield`?
- The 6 chest items with `has_gender_variations="true"` -- does Solus's `.tpac` actually contain the corresponding `_slim` meshes? The manifest at `tools/dale_armor_meshes.txt` should list all 6 `_slim` entries.

### DEEP ANALYSIS

1. **Skill-curve fairness.** Read the 27 troops in `troops_dale.xml` and the Rohan/Erebor equivalents. Is the "Excellent Archers" claim (+10-15 Bow over baseline) actually realized in the numbers? Pick 3 tier-matched Rohan or Erebor archer/infantry/cavalry troops and table-compare. Flag any tier where Dale is over- or under-tuned.

2. **Save-compat risk.** The change adds new troop IDs only (no renames, no deletes). But it ALSO swaps the recruitment pool for `Culture.sturgia` from "whatever vanilla used" to the Dale pool. For an existing save with Sturgia kingdom settlements, what happens on next volunteer tick? Will existing player parties / lord parties lose troops? Or just stop generating new vanilla Sturgians?

3. **Visual / lore consistency.** Per Tolkien (cited in `docs/features/dale.md`), Bardings are "armed with long swords and tall spears" -- but Dale infantry T4 (dale_man_at_arms) uses `sturgia_sword_4_t4` + shield, no spear. Dale T4-T5 footmen DO have northern spears. Is the lore claim load-bearing for any tier specifically, or is "long swords AND tall spears" satisfied across the whole line?

### FINDINGS

For each finding, use this format:

```
## Finding N: <title>
Severity: <P1/P2/P3>
File: <path>:<line>
What it does now:
<2-3 line description>
Why it's wrong:
<2-3 line evidence -- cite vanilla code if relevant>
Suggested fix:
<concrete change>
```

## QUALITY GATES

- Every "missing X" claim must be verified by grep, NOT guessed
- Vanilla API claims must come from `taom-src` or `ilspycmd` -- NOT inferred from old TOR/Calradia knowledge
- "Dale should do X" critiques where X is not in the change scope (e.g., custom Dale kingdom, custom Dale clans) should be flagged as DESIGN ALTERNATIVE, not BUG
- If a Known Suspect is DISPUTED, explain WHY the original concern was unfounded

## LESSONS FROM PRIOR REVIEWS

SUCCESSES: Config ID cross-ref catches rohan/dol_guldur/dale mismatches. Vanilla decompilation catches missing gates. Per-folder grep for item prefix catches canonical-folder mistakes (per `feedback_multi_folder_id_uniqueness.md`).

FAILURES: Codex has assumed `empire=Rohan` (it is Dunland), `dale` is a valid culture id (it is `sturgia`), flagged vanilla-matching code as bugs, skipped hard sections. Codex has also dismissed work as "just XML, nothing to review" -- treat every config cross-reference as load-bearing.

## OUTPUT

Write findings to: docs/reviews/codex-adversarial-dale-2026-05-26.md
