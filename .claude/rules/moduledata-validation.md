---
paths:
  - "**/troops/troops_*.xml"
  - "**/characters/*.xml"
  - "**/equipmentsets/*.xml"
  - "**/taom_spcultures.xml"
  - "**/taom_partyTemplates.xml"
  - "**/named_companions/*.xml"
  - "**/taom_wanderers.xml"
  - "**/taom_education_character_templates.xml"
  - "tools/schemas/*.json"
  - "tools/**/*.py"
  - "tools/**/*.ps1"
---

## Writing a data-mutating script? Read `tools/README.md` "XML I/O convention" FIRST

This rule now loads on `tools/**/*.py` and `tools/**/*.ps1` for one reason: the byte-faithful XML I/O
convention lives in `tools/README.md`, which **nothing auto-loads**, and the paths above previously
covered only *repo* ModuleData — so a script editing `Modules/<Mod>/ModuleData/*.xml` in the game
install loaded no convention at all. That gap has now produced the same defect **three times**
(scene tooling 2026-05-28, a scratchpad one-off, and `fix_uruk_hai_hands_teamcolor.py` 2026-08-06).

**The two sanctioned idioms — pick one, never mix them:**

```python
# A. utf-8-sig decode + explicit BOM re-prepend
had_bom = path.read_bytes().startswith(b"\xef\xbb\xbf")
text = path.read_text(encoding="utf-8-sig")
path.write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))

# B. full binary round-trip (BOM survives inside the string)
text = open(path, "rb").read().decode("utf-8")
open(path, "wb").write(text.encode("utf-8"))
```

**Forbidden:** the mixed shape — plain `utf-8` text read plus a text-mode write. It silently strips a
BOM *and* normalises CRLF→LF, turning a two-attribute edit into a whole-file rewrite.

Also mandatory for any script that writes outside the repo: a backup before the destructive write
(**never** a `*.xml` extension — these folders are globbed, and an `.xml` backup injects duplicate
item ids), a dry-run default with an explicit `--apply`, idempotency on re-run, and exact-token
comparison rather than substring containment when deriving a target set from a report or index.

**And parse the result before writing it.** Any script that transforms XML must run the transformed
text through `ElementTree` and refuse to write a document that no longer parses. On 2026-08-28 a
swap script masked XML comments by byte offset and restored them at those offsets; the swap changed
the text length, so comments were spliced into the middle of item ids and 8 ModuleData files were
written malformed. Nothing detected it until a hand-check afterwards, and the directory-wide revert
used to undo it destroyed a concurrent session's uncommitted work. The parse costs microseconds and
makes the whole class unshippable. Corollary for masking: restore by TOKEN (an indexed sentinel),
never by recorded offset, because any length-changing edit between mask and restore invalidates
every offset.

> **A blocking lint was evaluated and rejected (2026-08-06):** 92 of 124 XML-writing scripts trip a
> naive mixed-shape heuristic, so a build gate would fail on pre-existing debt and the heuristic
> false-positives on read-only analyzers. Loading the convention at authoring time is the effective
> control; the deep-review Tooling correctness lens (`.claude/skills/deep-review/lenses/tooling.md`)
> remains the review-time backstop.

# Validate ModuleData cross-references before committing

When you add, edit, or restructure **troops, characters, lords, cultures, equipment rosters, party templates, or the validator schemas**, run the schema-driven validator before committing:

```bash
python tools/validate_moduledata.py          # full report (errors + warnings)
python tools/validate_moduledata.py --json report.json --warnings-as-errors
```

For *targeted* checks mid-task (rather than a full run), the `taom-moduledata` MCP server exposes the same engine as tools — `mcp__taom-moduledata__item_exists`, `troop_exists`, `culture_exists`, `find_references`, `validate_moduledata`, `list_cultures` (it must be loaded; restart Claude after enabling it — see the feature doc).

It is one read-only pass that consolidates the old per-task validators (`validate_all_troop_refs.py`, `audit_item_refs.py`, the `equipmentType` PowerShell snippet, the duplicate-id-across-Armory-folder checks) and catches the recurring data-integrity bug classes:

| Code (severity) | Bug class it catches |
|---|---|
| `BROKEN_ITEM_REF` (error) | a `Item.X` ref to no defined item — the "underwear bug" (troop spawns naked) |
| `BROKEN_TROOP_REF` (error) | an `NPCCharacter.X` ref (upgrade_target, party-stack `troop=`, culture `basic_troop=`) to a deleted troop |
| `UNKNOWN_CULTURE` (error) | a `culture="Culture.X"` that is not a real StringId (e.g. `rohan` instead of `vlandia`, `dale` instead of `sturgia`) |
| `LANDLESS_CULTURE` (error) | a culture on a `Lord`-occupation `NPCCharacter`, `<Faction>` or `<Kingdom>` that owns **no settlement** — vanilla `SpawnLordParty` ends with an unguarded `Settlement.All.First(x => x.Culture == hero.Culture)`, so its lords CTD the daily clan tick (#374). Known-landless cultures live in `_LANDLESS_BY_DESIGN` with a stated reason |
| `SETTLEMENT_ECONOMY_FLOOR` (error) | a settlement of a culture named in `tools/settlement_economy_floor.json` sits below that spec's town/castle/`hearth` floor in the **LIVE** `TAOM_Map` module. That module is unversioned, so a reinstall silently reverts the 2026-08-14 faction-economy pass and nothing else in the repo would notice. Also fires when the spec is missing, declares nothing, or names a culture that owns no settlement (a retag or typo leaves the gate covering nothing, which reads exactly like a clean run). Re-apply with `python tools/rebalance_settlement_prosperity.py --culture-floor-file tools/settlement_economy_floor.json --apply` |
| `MOUNTED_DWARF` (error) | a `race="dwarf"` `NPCCharacter` able to reach a `slot="Horse"` item that is **not** a Dwarven war ram (own inline roster, or a standalone roster it names), or tagged `default_group="Cavalry"`/`"HorseArcher"` while carrying no ram. The dwarf skeleton's rider bone is misaligned, so a mounted dwarf spawns inside the horse mesh: the same invariant `Patch46_TournamentDwarfDismount` enforces at runtime. **Both halves are needed:** `CharacterObject.GetFormationClass()` ignores `default_group` when `IsHero` and reads `BattleEquipment` instead, so an enum-only check passes a lord who still spawns mounted. **The war ram is the one carve-out** (`WAR_RAM_MOUNT_IDS` in `tools/taom_schema.py`, exactly `taom_war_ram_a` and `taom_war_ram_b`, #515): a dwarf on a ram is legal and is genuinely Cavalry, so the group rule relaxes with it, but only for a character who actually carries one |
| `MOUNT_WITHOUT_HARNESS` (error) | a `slot="Horse"` entry with no `slot="HorseHarness"` beside it in the same equipment set. **A harness is required, not optional.** It is not always armour: on some mounts it carries the rider's SEAT, and nothing in the XML distinguishes those, because where the saddle is modelled is a property of a mesh the validator cannot see. `sk_eb_goat_a`/`_b` are bare pelts and every ram saddle lives on one of the eight `sk_eb_goat_bard_*` harness meshes, so `ironpass_ram_herder` shipped four sets putting a dwarf on bare hide. Judged per equipment set, because the engine draws each slot from an independently chosen set, so filling three sets of four still spawns bare rams. Exemptions are named in `_HARNESSLESS_BY_DESIGN` with a reason each, in the `_BODYLESS_BY_DESIGN` style: currently the mumakil (a harness suppresses the Horse item's `<AdditionalMeshes>`, where its war-platform lives) and the spider rider (an OPEN GAP, no spider harness item has ever been authored) |
| `MISSING_BODY_ARMOUR` (error) | a troop in `troops_*.xml` whose **battle** sets never fill the `Body` slot, so it spawns bare-chested. No reference is broken and no mesh is missing, which is why every other gate passes it. Three troops are bare-chested on purpose (`dg_goblin_slave`, the two Uruk-hai capstones) and live in `_BODYLESS_BY_DESIGN` with a reason; a test asserts those ids still exist, because an allowlist entry for a renamed troop rots silently. Found 2026-09-01: 15 of 16 Umbar troops in peasant rags with a green board |
| `UPGRADE_TIER_COLLAPSE` (error) | an upgrade edge whose target does not reach a higher tier than its source. Vanilla `DefaultPartyTroopUpgradeModel.GetXpCostForUpgrade` sums a per-tier table over `for (i = source.Tier + 1; i <= target.Tier; i++)`, so such an edge exits the loop immediately and the cost is **0**. `CampaignUIHelper.GetTroopXPTooltip` then evaluates `troop.Xp % cost` unguarded, which is a hard CTD on a party-screen hover (bundle a7dc3a20, #537); `PartyUpgraderCampaignBehavior` reads the same zero as "free" and promotes the whole stack for gold alone; `PartyBase.OnXpChanged` clamps roster XP to `Number * maxCost` and wipes it every tick. Tier is `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)`, a pure function of `level=`, so this is a data defect. `TaomPartyTroopUpgradeModel.GetXpCostForUpgrade` floors the cost at runtime, so a new collapse is no longer a crash; this gate exists so it stays a decision someone made. Ten deliberate same-level laterals (the elf tier-10 capstone fan-outs, the two `chosen_of_tharzog` capstones, the uruk ranged branch, the Dol Guldur villager entry) live in `_LATERAL_BY_DESIGN` with a stated reason each |
| `UPGRADE_INDEX_EMPTY` (error) | the shared index behind `UPGRADE_SKILL_REGRESSION` and `UPGRADE_TIER_COLLAPSE` came back with no troops, or with none carrying a `level=`, so both gates checked nothing that run. Those two read `troops/troops_*.xml` and `characters/npcs_*.xml` literally and non-recursively, unlike every sibling pass which `rglob`s, so a renamed folder or a file moved one directory deeper empties them silently. Added with #537 because a gate that quietly checks nothing reads exactly like a clean run |
| `SKILL_TEMPLATE_MISMATCH` (error) | an inline `<skills>` row beside a `skill_template` that differs from the template's value, or a template with inline rows that names no SkillSet, in any XML or XSLT file of the repo module's ModuleData (the Armory and `TAOM_Map` are not scanned); a run that finds no templated character at all is an error too. Since v1.5.2 `BasicCharacterObject.Deserialize` lays the inline rows over a copy of the template, so a differing row changes the character while the SkillSet (the source of truth) and every tool reading it say otherwise; v1.4.8 discarded the rows. 64 lords in `lords.xml` and 19 in `lords.xslt` had drifted when the bump landed (`331032a1`). Detection is `tools/sync_lord_inline_skills.py`'s (vanilla SkillSets first, then TAOM's), so the fixer and the gate agree; skipped, never faked, without the install (the `spc_*` templates live in SandBox). Repair: `python tools/sync_lord_inline_skills.py --apply`. Replaced `SKILL_TEMPLATE_SHADOWS_SKILLS`, which refused any character declaring both (#626) |
| `INCONSISTENT_ARMOUR_SLOT` (warn) | an armour slot filled in some of a troop's battle sets and empty in others. The engine draws each slot from an **independently chosen** set (`.claude/rules/troops.md`), so this ships a combination nobody authored, and every UI surface renders set #1 and looks correct. 96 exist across 10 cultures, which is why it warns rather than blocks |
| `UPGRADE_ARMOUR_REGRESSION` (warn) | an upgrade edge whose target totals **less armour** than its source (battle-set average over the five armour slots, unfilled = 0). The equipment half of the ladder rule: 62 edges shipped that way (#541), the Rhun ash capstones in light plate over heavy-plate parents the worst. Item values come from the install, so the check is skipped, never faked, without it; warns rather than errors because the Armory is unversioned. Repair with `tools/fix_upgrade_armour_regressions.py --apply`; militia-to-militia and the bare-chested-by-design troops' Body/Cape are exempt |
| `CROSS_CULTURE_ARMOUR_INVERSION` (warn) | a culture's ENGINE-tier cell (median armour total of its troops at `clamp(ceil((level - 5) / 5), 0, 10)`, every ITEM first scaled to the 57 reference chest cap of the line it belongs to, because kingdoms differ in armour power by design since #583) sitting more than 20 points under what the other kingdoms field two tiers lower (median of their medians, at least three of them). The cross-kingdom half of the ladder rule (#581): the edge gate above never compares kingdoms, and the old curve had one row for L31 to L51, so Gondor's tier 9 (194) shipped under the field's tier 7 (220). Same pure function as the report's gate preview (`taom_schema.cross_culture_armour_inversions`); skipped without the install; `_ARMOUR_LADDER_EXEMPT` names the deliberate off-ladder kits with a reason each. What it finds now is a kingdom dressed below its own cap (a capstone in shared low-band kit): `python tools/analyze_kingdom_armour.py` for the picture, then a roster decision |
| `RANGED_LADDER_INVERSION` (warn) | a troop that beats one it must not, per launcher class and per stat (speed, damage, accuracy, the troop's Bow/Crossbow): inside a LINE a lower tier over a higher tier, or at the same TIER a worse-ranked kingdom line over a better-ranked one on that stat's rank list. Reach is the launcher's `missile_speed`, the hit almost all its `thrust_damage`, the spread mostly its `accuracy` (the decompile cites are in `tools/ranged_ladder.py`), so every cell lives in `tools/ranged_ladders.json` (per-tier curves, lines ranked overall / damage / accuracy, the tiers each line fields), and the gate, the report and the roster tool share one pure function (`ranged_ladder.inversions`). 1,741 inverted speed pairs shipped on 2026-09-12 (#582); on 2026-09-18 every tier still carried its donor's damage and accuracy, 709 pairs (#617). Also a finding: a troop at a tier its line lists no cell for. Launcher stats come from the install, so the check is skipped, never faked, without it; a missing or self-contradicting spec and a ranged troop no line claims are findings, because a gate that quietly checks nothing reads like a clean run. Repair: `python tools/generate_ranged_ladder_items.py --apply` then `python tools/rebalance_ranged_ladders.py --apply` (it writes the skill too). Exemptions go in `_RANGED_LADDER_EXEMPT` with a reason each |
| `RANGED_DAMAGE_CEILING` (warn) | a bow or crossbow a Lord, Wanderer or `is_hero` character can carry (its own kit, its battle `EquipmentSet` rosters, or a template `lords.xslt` hands a retagged vanilla lord), or that a roster the game applies to the player at runtime hands out (`player_char_creation_*`, `player_career_*`, the enlistment quartermaster's `enlist_*`; civilian sets skipped), that hits above the spec's `hero_ceiling` (Bow 90, Crossbow 105). Before #617 the Armory's own 38 launchers ran 62 to 130 damage at accuracy 70 to 100, and nine hero-reachable ones hit 92 to 112. Not covered: Field Commission copies a troop's battle kit onto a companion in C# (#625). Repair: the item's row in the spec's `donor_stats`, then `python tools/restat_ranged_donors.py --apply` |
| `MELEE_LADDER_INVERSION` (warn) | a troop armed outside the sustained-damage band its tier and kingdom call for (`tools/melee_ladders.json`). The unit is DPS, not one blow: a lighter blade that swings faster out-damages a heavier one, and 10 of the 49 apparent line regressions measured on 2026-09-20 were weapons trading weight for speed rather than faults. Bannerlord SIMULATES a crafted weapon's damage from its pieces rather than storing it, so the numbers come from `tools/melee_damage.py`, a port of the v1.5.3 engine; the check needs the install and is skipped, never faked, without it. Before #631 the ladder was flat (median best-DPS by tier 66, 64, 66, 67, 69, 69, 81, 82, 82, 69, a 1.04x climb against vanilla's 2.23x over five tiers), so troop tier barely changed melee output. Warns rather than errors because the weapon half of the fix lands in the unversioned Armory and a reinstall can revert it while the rosters stay right. Repair: `python tools/fix_melee_ladder.py` (dry-run, then `--apply`); where a culture owns no weapon in band the fix is a blade restat, not a roster swap, because damage lives on the blade piece. Exemptions go in the spec's `exempt_troops` with a reason each, and an entry naming a troop that no longer exists is itself a finding. A noble-line troop (`_NOBLE_LINE_TROOPS`) is judged one tier up |
| `ARMOUR_MESH_TIER_LADDER` (warn) | a troop wearing an armour mesh whose id tier (`_light_`/`_med_`/`_heavy_`/`_elite_`/`_lord_`) its level may not wear (`rebalance_armor.MESH_TIER_LADDER`: light to 6, light/medium to 16, medium to 21, heavy to 26, heavy/elite to 31, elite to 36, elite/lord from 41). **Over-dressed** is the harmful direction: the kingdom-cap curve prices a mesh by its LOWEST wearer, so a level-11 snaga in the six Gundabad `_lord_` chests anchored the whole lord line at 20 body armour under a 25 kg plate mesh while the medium line sat at 31 (#609; 251 pairs on 2026-09-16). **Under-dressed** is cosmetic and reported for the roster pass (1,283 pairs). Needs no install, the tier is in the id; troop files only, the cross-culture gate's exempt troops skipped; noble lines (`_NOBLE_LINE_TROOPS`) are judged one tier up and anchor a band up where one exists (never below the item's own tier), and a `(troop, item)` pair in `_ARMOUR_LADDER_EXEMPT_ITEMS` excuses that one piece. Repair: `python tools/fix_armour_mesh_ladder.py` (dry-run, then `--apply`), then `derive_armor_tiers.py` and the roster-first restat, because the anchors move. A line with no variant at an allowed tier is a hand decision the fixer lists |
| `GENERATOR_RETIRED_ITEM_REF` (warn) | a generator under `tools/` would WRITE an item id the live install does not define. Every other pass reads the XML that ships; this one reads the Python that writes it, because a retired id in a generator table is invisible until a re-run spawns a naked troop or hangs a battle on a missing body (#352). 67 such ids across seven generators on 2026-09-13. Resolves against `Registries.items`, so the live Armory is the authority; skipped, never faked, without the install. CLI: `python tools/check_generator_item_refs.py`. Register a new writer in its `GENERATORS`; swap maps that name retired ids on their FROM side stay out |
| `MISSING_COLLISION_BODY` (error) | an item or crafting piece whose `body_name` / holster body / collision body names a `PhysicsShape` no loaded tpac ships. `PreloadHelper.WaitForMeshesToBeLoaded` polls every registered body name in a do/while with no exit, so one unresolvable name spins the game thread forever on the first frame of any mission that preloads a carrier; the player's own kit is first in every tournament preload set (#352 twice, #599 the elf start, two days). `validate_mesh_refs.py` Tier C is the engine; this pass turns its `MISSING_BODY` into an error over the WHOLE Armory ModuleData plus the repo's, skipped never faked without the install, and a run that finds no tpacs to scan is itself the finding. Repair the REF to the art that ships, never restore a tpac; `python tools/audit_armory_refs.py` names the troops |
| `MISSING_VISUAL_MESH` (warn) | the same for `mesh` / `holster_mesh`: the item renders invisible. Warns rather than errors because it cannot hang the game |
| `COLLISION_BODY_BORROWED` (error) | a weapon or crafting piece whose `body_name` is ANOTHER kit's twin (`bo_<M2>` or `bo_cap_<M2>` for a shipped mesh M2 that is not its own). The name resolves, so `MISSING_COLLISION_BODY` passes it, and the item now depends on art that can be renamed or retired without it: three Rhun longbows shipped on the elven bow's body and #599 had already renamed that once (#633). The convention is `weapon-creation-workflow.md` Step D: a body is `bo_` + the exact mesh id, authored in the mesh's own FBX, and a borrow is a placeholder with a deadline. Sharing WITHIN a kit is design (recolours and variants share one geometry: the ruby and topaz Aranruth blades, the `_a2` Erebor axe on `_a`'s body), a body under a variant name is nobody's twin and passes, and cross-kit shares that are authorised live in `_SHARED_BODY_BY_DESIGN` with a reason each (Dragon and Khamul are re-textured Loke). Vanilla bodies and shields are exempt; a body no pack ships stays `MISSING_COLLISION_BODY`'s. 0 findings on 2026-09-21 (58 piece shares, all in-kit or Rhun). Repair: author the twin with `tools/blender/add_collision_body.py`, then a Modding Kit import, never a different borrow |
| `FORTIFICATION_WITHOUT_VILLAGE` (warn) | a town or castle in the LIVE world that no village is bound to: no village trade, no villager parties, no rural notables, food on the fief alone. `town_EW10` and `town_EW11` shipped that way until #597 and only a hand count noticed. Reads the `bound` field the settlement registry now records per village, so it is skipped, never faked, without the install. Exemptions go in `_VILLAGELESS_BY_DESIGN` with a reason each (none today; `castle_G4` sat there for an hour on 2026-09-13 until its two villages were placed); an entry naming no fortification, or one that has villages again, is itself a finding. Fix: place entities in the editor, then `tools/add_map_villages.py`, or rebind a neighbour with `tools/rename_map_settlements.py` |
| `SCHEMA_INVALID` (error) | a repo ModuleData file the engine loads that its own XSD rejects (a missing required attribute such as `clan_umbar_3`'s `initial_home_settlement`, an undeclared attribute, a DOCTYPE), or a SubModule.xml registration that loads nothing or that the engine throws on at startup. The engine validates on load but only logs the failure and loads the file anyway, so these were silent. From `tools/validate_xml_schemas.py` (#621), repo module only; one WARNING when lxml or the engine's `XmlSchemas` folder is absent, never a silent pass. An XSD can lag the code: confirm a failure against the element's deserializer before editing |
| `DUPLICATE_{NPC,CULTURE,ROSTER}_ID` (error) | the same id defined twice |
| `DUPLICATE_ITEM_DEF` (warn) | an Armory item id defined in >1 `LOTRLOME_items` folder (engine silently shadows one) |
| `MISSING_CIVILIAN_TYPE` (warn) | a civilian roster whose `<EquipmentSet>` lacks `equipmentType="Civilian"` (Faramir/Boromir wrong-outfit) |
| `INVALID_ENUM` (warn) | `default_group` not Infantry/Ranged/Cavalry/HorseArcher |
| `BROKEN_PARTY_TEMPLATE_REF` (warn) | a `PartyTemplate.X` ref to an undefined template |

## What actually gets scanned: three modules, not one

Two of TAOM's three data modules live in the game install, are unversioned, and are covered very
unevenly. Counts measured 2026-08-18.

| Module | ModuleData XML | Cross-ref sweep | Schema checks (dup id, enum, civilian) | Commit hook fires? |
|---|---|---|---|---|
| TAOM: `Main/_Module/ModuleData` | 259 | yes, all 5 ref kinds | yes | yes |
| `<game>/Modules/LOTRLOME_Armory/ModuleData` | 382 | yes, all 5 ref kinds (`extra_ref_roots`) | no, by design | no |
| `<game>/Modules/TAOM_Map/ModuleData` | 44 | yes, all 5 ref kinds (added #462) | no | no |

The last column is not a detail. The hook matches on staged `Main/_Module/ModuleData/*.xml` or `*.xslt`
(`check-moduledata-validation.sh:73`) and neither live module is in git, so editing them stages
nothing and gates nothing. Run `python tools/validate_moduledata.py` by hand after any edit there.

- **Editing TAOM_Map's `settlements.xml`?** Its 1,012 `Culture.` refs are checked since #462, but
  only when you run the validator yourself: the file is not in git, so the commit hook never fires on
  it. That file is the sole input to `settled_cultures`, so a bad id there corrupts the
  `LANDLESS_CULTURE` verdict with no other diagnostic. Run `python tools/validate_moduledata.py`
  after any edit, or check a new id with `mcp__taom-moduledata__culture_exists` first.
- **Authoring into LOTRLOME_Armory?** Its refs are checked, its structure is not. Duplicate ids,
  enums, the civilian rule and `MOUNTED_DWARF` apply to repo files only, so a troop or an equipment
  roster authored there would be entirely unvalidated. Today it defines items and monsters and
  nothing else; keep it that way, or extend the schema passes first.
- **Touching any XSLT?** Run `python tools/check_external_xslt.py`. It is the only gate that reaches
  all 16 stylesheets across the three modules (#462): well-formedness always, plus a real stylesheet
  compile when lxml is present. CI cannot do this, because the two live modules are not in the
  checkout. Note no pass *interprets* a stylesheet: `TAOM_Map/ModuleData/settlements.xslt` is opened
  only to regex for the empty `<xsl:template match="Settlement"/>` strip, and `/xslt-check` reads
  from `Main/_Module/ModuleData/` only, its mapping table covering 6 of the repo's 8
  (`action_strings.xslt` and `comment_strings.xslt` are absent). For `spcultures.xslt` run
  `CulturePartyTemplateTests`; everything else is a manual transform-and-diff.

Full matrix, per-kind ref counts and the named gaps:
[docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md)
"Module coverage at a glance".

## Gate per file kind

Run the gates for every kind of file you touched, as an author before committing and as the
`/deep-review` XML lens (`.claude/skills/deep-review/lenses/7-xml.md`) after. One list, so a new
gate is registered here once. A gate that cannot run (no install, no lxml) has not passed.

| Kind | Read first | Run |
|---|---|---|
| Any engine-registered XML, repo or live | `tools/validate_xml_schemas.py` docstring | `python tools/validate_xml_schemas.py <files>` (finds each file's module; add SubModule.xml when it changed; `--live` for whole live modules) |
| troops, characters, equipment rosters, cultures, clans, party templates, lords | this rule, `troops.md`, `xml-data.md` | `python tools/validate_moduledata.py`, reading the warnings as well as the errors |
| XSLT anywhere | `xslt.md` | `python tools/check_external_xslt.py`; transform and diff against SandBoxCore vanilla (`/xslt-check`); `CulturePartyTemplateTests` for `spcultures.xslt` |
| Armory items, crafting pieces, weapon descriptions | `docs/reference/armory-guide.md` | `python tools/audit_armory_refs.py`; `python tools/audit_polearm_shield_parity.py` for weapons and pieces |
| action sets, monsters, monster usage | `armory-guide.md` "action_sets structure" | `python tools/audit_action_set_parity.py`; `python tools/audit_mount_parity.py` for creature mounts |
| `TAOM_Map` settlements | `docs/reference/taom-map-settlement-naming.md` | `python tools/validate_moduledata.py` (`LANDLESS_CULTURE`, `FORTIFICATION_WITHOUT_VILLAGE`); `python tools/add_map_villages.py --check` |
| GUI prefabs, PrefabExtension XML | `gui-ui.md` | sprite ids against `TAOMSpriteData.xml`; every `Command.*` handler resolves to a TAOM method with the right parameter count |
| language files, `{=KEY}` text | `docs/reference/localization-map.md` | `python tools/check_external_loc_coverage.py`; `LanguageDataXmlTests` |
| `project.mbproj` | the tool's docstring | `python tools/audit_mbproj_registration.py` |
| scenes, prefabs | `docs/modding/module-map.md` (prefab entity cap) | `python tools/check_prefab_budget.py` (counts `TAOM_Map` only; sum all modules) |
| XML a `tools/` generator wrote | `tools/README.md` "XML I/O convention" | the generator's own `--verify` or `--check`; `python tools/check_generator_item_refs.py`; hand-read three generated entries |

## Discipline

- **Schemas are the source of truth.** Field types, enums, cross-ref targets, and the civilian rule live in `tools/schemas/*.json` — add new fields/enums there, never hardcode them in Python.
- **When you add a NEW file that defines `<NPCCharacter>`** (a new wanderer/companion/template file), add its path to `taom_npccharacter.json` `applies_to`, or its duplicate-id + enum checks silently won't run (Codex review 2026-05-30 found 3 such files uncovered).
- **The `MOUNTED_DWARF` war-ram allowlist has two edges worth knowing before you edit dwarf data.**
  It resolves **every** Horse-slot mount a dwarf can reach, inline equipment first and then every
  standalone `<EquipmentRoster>` he names, not just the first one found: with an allowlist in play a
  ram listed ahead of a horse would otherwise take the pass and hide the horse behind it. And the
  allowlist is matched against what the item-ref regex actually captured, which requires the `Item.`
  prefix (`_ITEM_REF_ATTR_RE`), so a Horse slot written `id="taom_war_ram_a"` without it is never
  allowlisted: it surfaces as `(unnamed mount)` and errors. The two ids are pinned rather than
  prefix-matched, so a future `taom_war_ram_c` has to be reviewed into `WAR_RAM_MOUNT_IDS` on purpose
  instead of arriving by name alone.
- **This pass only sees data an `NPCCharacter` names, and that hole shipped a real bug.**
  `MOUNTED_DWARF` walks `NPCCharacter` definitions plus the rosters they reference. The career
  starting rosters in `equipmentsets/taom_career_starting_equipment.xml` are applied to the player at
  **runtime** by `CareerStartingEquipmentService.ApplyCareerStartingEquipment` (roster id built from
  culture + archetype + sex, then applied to the player hero) and are never named by any
  `NPCCharacter`, so the sweep cannot reach them. `player_career_erebor_cavalry_m`/`_f` sat there
  equipping `Item.saddle_horse`, a vanilla horse on a dwarf, and every validator run passed. It was
  found by hand during #515 and is now covered by shipped-data tests in
  `TAOM.Tests/Features/CharacterCreation/CareerCultureCoverageTests.cs`, not by this validator. Treat
  any other XML applied to a character at runtime as having the same blind spot.
- A `PreToolUse` hook (`check-moduledata-validation.sh`) auto-runs the **error**-severity checks on every Claude-driven commit that stages ModuleData XML or XSLT and blocks on failure. It does NOT surface warnings, so run the tool yourself to see `MISSING_CIVILIAN_TYPE` / `INVALID_ENUM` / `DUPLICATE_ITEM_DEF` / `BROKEN_PARTY_TEMPLATE_REF` / `INCONSISTENT_ARMOUR_SLOT`.
- **The hook enforces an explicit `--code` allowlist, not "all errors".** On 2026-09-01 four of the nine error codes were missing from that list and therefore could never block a commit, including one added the same day. The two lists live in different files and neither referred to the other, so nothing detected the drift. `CommitGateCoverageTests` in `tools/tests/test_validate_moduledata.py` is now the reference between them: **add an error code and you must add its `--code` line, or that test fails and names it.** It also fails on a `--code` naming a code the validator cannot emit, which is a silently dead gate line.
- **Until 2026-09-18 the validator did NOT enforce engine XSD required-attributes; `SCHEMA_INVALID` does now** (#621). `characters/clans.xml` still has **no TAOM schema** (`tools/schemas/` covers only equipmentsets / npccharacter / spcultures), so its cross-field rules stay unchecked, but a `<Faction>` (clan) missing a `Factions.xsd`-required attribute such as `initial_home_settlement` now fails `SCHEMA_INVALID` and the commit hook instead of surfacing only when the engine/editor loads the file. (RCA: `clan_umbar_3` shipped without its home settlement, fixed 2026-06-22. See [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md) "Coverage boundary".) **That hole is now half of a known CTD, so treat it as load-bearing rather than cosmetic:** a faction with a null `InitialHomeSettlement` is exactly the first of the two faults behind #374, and the second (a lord whose culture owns no settlement) is now gated by `LANDLESS_CULTURE` above. Either alone is harmless; together they throw `InvalidOperationException` out of `Campaign.Tick`. Patch65 repairs the faction at runtime, but a clan shipped without `initial_home_settlement` is still a data defect, which `SCHEMA_INVALID` now catches before commit. (See [docs/features/lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md).) The same hole is wider for **armory `action_sets.xml` structure**, which neither the validator nor the hook covers at all: the hook fires only on `Main/_Module/ModuleData/*.xml` and `*.xslt` (`check-moduledata-validation.sh:73`), the live file lives in the game install, and the only copy in this repo, `docs/reference/lotrlome-armory-snapshot/action_sets.xml`, is re-snapshotted with no structural check. Gate it with `python tools/audit_action_set_parity.py` (defaults to the live install; pass `--live <path>` to audit the tracked snapshot), which exits non-zero on any root-level `<action>`: the game client loads such a file silently while the dedicated-server engine throws `KeyNotFoundException` at `/action_sets/action` and dies on boot, so a clean single-player session proves nothing. (2026-08-03: 168 orphaned elements from twelve self-closing tavern sets. See [docs/reference/armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure".)
- **The XSD layer has its own gate since 2026-09-18: `python tools/validate_xml_schemas.py`** (pass the files you touched, repo or live; each is validated under its own module; `--live` scans whole live modules). It uses the engine's own file-to-schema mapping and also runs inside `validate_moduledata.py` as `SCHEMA_INVALID` for the repo module, so the commit hook gates repo files; live-install files are gated only when you run it. A failure is strong evidence, not proof: an XSD can lag the code, so confirm against the element's deserializer before editing. It does not cover `project.mbproj` native data (`action_sets.xml` stays with `audit_action_set_parity.py`).
- **PASS ≠ in-game loaded (the new-file / restart blind spot).** The validator parses the XML files off disk in the Python process — it proves refs *resolve on disk now*, NOT that the running/last-launched engine loaded them. Bannerlord registers each `<XmlName id="Items" path="LOTRLOME_items/<culture>">` **directory** at process launch and globs it (`DirectoryInfo.GetFiles("*.xml")`) at campaign start, with no hot-reload (decompile-verified: `Module.cs:246→1032`; `Campaign.cs:1471 LoadXML("Items")` → `MBObjectManager.cs:894/900/901/903`). So a **NEW** item/equipment XML file added after launch is null in-engine — the character spawns **naked** (the "underwear bug") — even though this validator, the build, and unit tests all pass (none start a campaign). **Any change that adds or edits item/equipment XML is not "done" until a full game RESTART + an in-game visual check** (new campaign, spawn/select the affected character, confirm clothed). This covers the `generate_*_armor.py` family, `/new-culture`, `/author-armor`, and any file dropped into a folder-registered `LOTRLOME_items/<culture>/` dir. Corollary: keep backups on a non-`.xml` extension (`.bak-*`) — the glob is `*.xml`, so a `*.xml` backup left in a registered dir gets globbed and injects a duplicate item id. (RCA: the 12 non-Gondor `starter_armors.xml` shipped naked-until-restart 2026-06-30. See [docs/features/starting-equipment-tuning.md](../../docs/features/starting-equipment-tuning.md) + docs/reviews/LESSONS-LEARNED.md "A NEW item XML file only loads at process launch".)

## Why this rule exists

These bug classes recur (the underwear bug, dup ids across Armory folders, dead troop refs after deletions, `rohan`-vs-`vlandia` typos) and each was previously caught — if at all — by a separate hand-run script. The validator makes them catchable in one pass. Full design: [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md). Sibling data rules: [troops.md](troops.md), [xml-data.md](xml-data.md), [vanilla-data-comparison.md](vanilla-data-comparison.md). Matcher-authoring lesson: `feedback_prefix_ref_matchers_are_attribute_agnostic` (memory).
