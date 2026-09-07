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
> control; the deep-review Tooling Correctness agent (`.claude/skills/deep-review/SKILL.md` Step 2c)
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
| `INCONSISTENT_ARMOUR_SLOT` (warn) | an armour slot filled in some of a troop's battle sets and empty in others. The engine draws each slot from an **independently chosen** set (`.claude/rules/troops.md`), so this ships a combination nobody authored, and every UI surface renders set #1 and looks correct. 96 exist across 10 cultures, which is why it warns rather than blocks |
| `UPGRADE_ARMOUR_REGRESSION` (warn) | an upgrade edge whose target totals **less armour** than its source (battle-set average over the five armour slots, unfilled = 0). The equipment half of the ladder rule: 62 edges shipped that way (#541), the Rhun ash capstones in light plate over heavy-plate parents the worst. Item values come from the install, so the check is skipped, never faked, without it; warns rather than errors because the Armory is unversioned. Repair with `tools/fix_upgrade_armour_regressions.py --apply`; militia-to-militia and the bare-chested-by-design troops' Body/Cape are exempt |
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

The last column is not a detail. The hook matches on staged `Main/_Module/ModuleData/*.xml`
(`check-moduledata-validation.sh:65`) and neither live module is in git, so editing them stages
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
- A `PreToolUse` hook (`check-moduledata-validation.sh`) auto-runs the **error**-severity checks on every Claude-driven commit that stages ModuleData XML and blocks on failure. It does NOT surface warnings, so run the tool yourself to see `MISSING_CIVILIAN_TYPE` / `INVALID_ENUM` / `DUPLICATE_ITEM_DEF` / `BROKEN_PARTY_TEMPLATE_REF` / `INCONSISTENT_ARMOUR_SLOT`.
- **The hook enforces an explicit `--code` allowlist, not "all errors".** On 2026-09-01 four of the nine error codes were missing from that list and therefore could never block a commit, including one added the same day. The two lists live in different files and neither referred to the other, so nothing detected the drift. `CommitGateCoverageTests` in `tools/tests/test_validate_moduledata.py` is now the reference between them: **add an error code and you must add its `--code` line, or that test fails and names it.** It also fails on a `--code` naming a code the validator cannot emit, which is a silently dead gate line.
- **The validator does NOT enforce engine XSD required-attributes**, and `characters/clans.xml` has **no schema at all** (`tools/schemas/` covers only equipmentsets / npccharacter / spcultures). A `<Faction>` (clan) missing a `Factions.xsd`-required attribute such as `initial_home_settlement` passes both this tool and the commit hook, surfacing only when the engine/editor loads the file. When editing `clans.xml`, verify in the Bannerlord editor (or an engine load) too — a clean `validate_moduledata.py` is necessary but not sufficient for clan data. (RCA: `clan_umbar_3` shipped without its home settlement, fixed 2026-06-22. See [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md) "Coverage boundary".) **That hole is now half of a known CTD, so treat it as load-bearing rather than cosmetic:** a faction with a null `InitialHomeSettlement` is exactly the first of the two faults behind #374, and the second — a lord whose culture owns no settlement — is now gated by `LANDLESS_CULTURE` above. Either alone is harmless; together they throw `InvalidOperationException` out of `Campaign.Tick`. Patch65 repairs the faction at runtime, but a clan shipped without `initial_home_settlement` is still a data defect this tool cannot see. (See [docs/features/lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md).) The same hole is wider for **armory `action_sets.xml` structure**, which neither the validator nor the hook covers at all: the hook fires only on `Main/_Module/ModuleData/*.xml` (`check-moduledata-validation.sh:65`), the live file lives in the game install, and the only copy in this repo — `docs/reference/lotrlome-armory-snapshot/action_sets.xml` — is re-snapshotted with no structural check. Gate it with `python tools/audit_action_set_parity.py` (defaults to the live install; pass `--live <path>` to audit the tracked snapshot), which exits non-zero on any root-level `<action>` — the game client loads such a file silently while the dedicated-server engine throws `KeyNotFoundException` at `/action_sets/action` and dies on boot, so a clean single-player session proves nothing. (2026-08-03: 168 orphaned elements from twelve self-closing tavern sets. See [docs/reference/armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure".)
- **PASS ≠ in-game loaded (the new-file / restart blind spot).** The validator parses the XML files off disk in the Python process — it proves refs *resolve on disk now*, NOT that the running/last-launched engine loaded them. Bannerlord registers each `<XmlName id="Items" path="LOTRLOME_items/<culture>">` **directory** at process launch and globs it (`DirectoryInfo.GetFiles("*.xml")`) at campaign start, with no hot-reload (decompile-verified: `Module.cs:246→1032`; `Campaign.cs:1471 LoadXML("Items")` → `MBObjectManager.cs:894/900/901/903`). So a **NEW** item/equipment XML file added after launch is null in-engine — the character spawns **naked** (the "underwear bug") — even though this validator, the build, and unit tests all pass (none start a campaign). **Any change that adds or edits item/equipment XML is not "done" until a full game RESTART + an in-game visual check** (new campaign, spawn/select the affected character, confirm clothed). This covers the `generate_*_armor.py` family, `/new-culture`, `/author-armor`, and any file dropped into a folder-registered `LOTRLOME_items/<culture>/` dir. Corollary: keep backups on a non-`.xml` extension (`.bak-*`) — the glob is `*.xml`, so a `*.xml` backup left in a registered dir gets globbed and injects a duplicate item id. (RCA: the 12 non-Gondor `starter_armors.xml` shipped naked-until-restart 2026-06-30. See [docs/features/starting-equipment-tuning.md](../../docs/features/starting-equipment-tuning.md) + docs/reviews/LESSONS-LEARNED.md "A NEW item XML file only loads at process launch".)

## Why this rule exists

These bug classes recur (the underwear bug, dup ids across Armory folders, dead troop refs after deletions, `rohan`-vs-`vlandia` typos) and each was previously caught — if at all — by a separate hand-run script. The validator makes them catchable in one pass. Full design: [docs/features/moduledata-validation.md](../../docs/features/moduledata-validation.md). Sibling data rules: [troops.md](troops.md), [xml-data.md](xml-data.md), [vanilla-data-comparison.md](vanilla-data-comparison.md). Matcher-authoring lesson: `feedback_prefix_ref_matchers_are_attribute_agnostic` (memory).
