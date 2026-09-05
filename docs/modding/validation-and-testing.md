# Validation and testing

## What this file is

This is the gate list: every check you can run against TAOM's data without compiling anything, what
each one proves, and what it is structurally unable to see. It exists because the same handful of
data bugs keep shipping (a naked troop, a culture fielding Calradians, a shield paired with a weapon
the AI never draws) and each has a cheap detector nobody ran. Read the category your edit falls into,
run those commands, then run the in-game smoke that the commands cannot replace.

## Six safety categories

Every gate answers exactly one question. Pick the row that matches what you edited.

| # | Category | The question | Run after you edit |
|---|---|---|---|
| 1 | Ids and references | does every `Item.` / `NPCCharacter.` / `Culture.` / `PartyTemplate.` id resolve? | troops, lords, notables, cultures, party templates, equipment rosters |
| 2 | Art and meshes | does every mesh and collision body named in XML exist as a packaged asset? | armour, weapons, crafting pieces, skins, any art deletion |
| 3 | Registration and structure | is the file registered, and is its markup legal to the engine (not just to a parser)? | a NEW file, `SubModule.xml`, `project.mbproj`, any `.xslt`, `action_sets.xml` |
| 4 | Balance and ladders | do the numbers hold as a ladder, and is the gear usable? | troop stats, item stats, upgrade edges, lord skills, career bonuses |
| 5 | World map and scenes | do settlements, scenes and prefabs still resolve? | `settlements.xml`, scene names, prefabs, owners, prosperity |
| 6 | Only the running game | did the engine actually load it, and does it look right? | everything, always, last |

TAOM ships 37 read-only gates across categories 1 to 5.
<!-- measured: counted the tool-inventory rows whose category is validator or audit and whose modder_usable flag is true, then confirmed each named script exists with `ls` 2026-09-05 -->
Everything below names them. Anything not in these tables either writes files (a generator or a
rebalancer, covered in [balance-levers](balance-levers.md)) or is developer-only.

### Category 1: ids and references

| Command | Proves | Cannot see |
|---|---|---|
| `python tools/validate_moduledata.py` | the whole cross-reference sweep plus duplicate ids, enums, civilian tagging, landless cultures, bare-chested troops, upgrade-tier collapse | engine XSD required attributes; whether an id points at the *right* thing |
| `python tools/validate_all_troop_refs.py` | armour ids in ten troop files resolve against the Armory | six troop files, and weapons, arrows, mounts, harnesses (it says so in its own PASS banner) |
| `python tools/validate_gondor_refs.py` | the Gondor-only ancestor of the above | superseded; prefer `validate_moduledata.py` |
| `python tools/audit_item_refs.py` | every `Item.X` reference in TAOM ModuleData against the multi-module item registry | armour-only scope is gone here, but rosters applied at runtime are still invisible |
| `python tools/audit_equipment_roster_coverage.py` | every custom culture provides the eight mandatory v1.4.3 equipment rosters | whether the items inside them are any good |
| `python tools/audit_enlistment_roster_coverage.py` | the enlistment service kits obey the slot allowlist and the culture attribute | anything outside the enlistment rosters |
| `python tools/oneoff/validate_equipment_flags_1_4_3.py` | no deprecated v1.3.15 `EquipmentFlags` names survive | current-schema mistakes |

### Category 2: art and meshes

| Command | Proves | Cannot see |
|---|---|---|
| `python tools/validate_mesh_refs.py` | every mesh and collision body named in items, crafting pieces and `skins.xml` resolves to a packaged asset | art deleted from `Assets/` that is still baked into a cooked pack |
| `python tools/audit_deleted_mesh_impact.py` | which items and which troops a deleted mesh reaches, across five reference shapes | anything the deletion list does not name |
| `python tools/generate_armory_catalogue.py --check` | the committed catalogue still reproduces the Armory's mesh inventory exactly | whether a mesh is *correct*, only whether it moved, was renamed or vanished |
| `python tools/audit_gender_variation_flags.py` | `has_gender_variations` matches the art that ships | the art itself |
| `pwsh tools/Audit-MeshRefs.ps1 -ModulePath "<install>/Modules/LOTRLOME_Armory"` | a whole-module mesh-ref report | body and collision validation, which belongs to `validate_mesh_refs.py` |
| `python tools/verify_mount_assets.py` | a rideable creature's tpac and FBX survived a Kit recompile | anything before the recompile |

`LOTRLOME_Armory/ModuleData/LOTRLOME_items/gondor/head_armors.xml` and every other path under
`LOTRLOME_Armory` and `TAOM_Map` lives in the game install, not the repo; a module reinstall reverts
hand edits, so land a repo-side validator gate with any fix.

### Category 3: registration and structure

| Command | Proves | Cannot see |
|---|---|---|
| `python tools/audit_mbproj_registration.py` | no `project.mbproj` `<file>` row carries an id the engine silently ignores | `SubModule.xml` registration, which is a separate list |
| `python tools/check_external_xslt.py` | all 17 stylesheets across the three modules are well formed, and compile when lxml is present | what a stylesheet *means*; no tool models XSLT semantics |
| `python tools/audit_action_set_parity.py` | no humanoid `action_set` is short of Native's surface, and no root-level `<action>` exists | animation quality |
| `python tools/audit_civilian_action_set_coverage.py` | every race has the civilian action-set family | battle sets |
| `python tools/audit_mount_parity.py` | a creature mount's Monster, usage and action surfaces match warg / elephant / horse | rein attributes, and it always exits 0 (see the exit-code traps) |

### Category 4: balance and ladders

| Command | Proves | Cannot see |
|---|---|---|
| `python tools/audit_polearm_shield_parity.py` | no roster pairs a shield with a weapon whose primary usage is `requires_no_shield` | two-handers, which it warns about rather than failing |
| `python tools/analyze_troop_balance.py` | per-culture parity heatmap, outliers, level monotonicity | whether the curve is the right curve |
| `python tools/analyze_armor_balance.py` | per-culture armour balance against the same baselines the writer uses | mesh quality, silhouette, lore fit |
| `python tools/derive_armor_tiers.py` | an item's intended tier, derived from which troops actually wear it | items no roster references |
| `python tools/analyze_lord_balance.py` | per-culture lord skills resolved through `skill_template` to the real `SkillSet` | traits expressed only in dialogue |
| `python tools/audit_cc_bonuses.py` | character-creation skill, attribute and focus bonuses per culture | in-play difficulty |
| `python tools/analyze_battle_logs.py` | how the auto-resolve counter and race matrices behaved in recorded battles | anything you have not played yet |
| `python tools/generate_culture_issue_drafts.py` | a per-culture worklist for lord skills and traits | it writes drafts, it checks nothing |

### Category 5: world map and scenes

| Command | Proves | Cannot see |
|---|---|---|
| `python tools/audit_scene_names.py` | every settlement and hideout `scene_name` resolves to a SceneObj folder | whether the scene is playable |
| `python tools/audit_battle_scenes.py` | every Scene id in `sp_battle_scenes.xml` still exists on disk | which scene a given fight will pick |
| `python tools/audit_siege_props.py` | per town and castle, how many siege resupply props are genuinely usable | siege AI behaviour |
| `python tools/check_prefab_budget.py` | `TAOM_Map/Prefabs` against the engine's 131,072 load-queue cap | every other module's prefabs, which share that one global queue |
| `python tools/analyze_settlement_prosperity.py` | starting prosperity, live map against vanilla, with flat-cluster flags | the economy after day one |
| `python tools/dump_settlement_buildings.py` | per-fief starting building levels from the live map | levels players reach later |
| `python tools/analyze_war_theaters.py` | which kingdoms have no enemy inside their march radius | real pathing distance; its numbers are straight-line estimates |
| `pwsh tools/Settlement-Breakdown.ps1 -SettlementsXml "<install>/Modules/TAOM_Map/ModuleData/settlements.xml"` | settlement counts by region and type | pass the live path; the default is the repo's stale shadow |
| `pwsh tools/Generate-SceneEntitiesDoc.ps1` | the world-map scene's settlement entities as a doc | it reads the scene only, never ModuleData |
| `python tools/clan_registry.py` | every clan that exists after `spclans.xslt` runs, with culture and kingdom | whether that clan has lords |

The thirty-seventh gate is `python tools/lint_docs.py --quick`, which checks documentation health
rather than data. Category 6 has no scripts at all: see "In-game smoke" below.

## The three-module coverage matrix

TAOM's data spans three modules and only one of them is in the repo. Counts measured today.

<!-- measured: find <module>/ModuleData -name '*.xml' | wc -l ; find <module>/ModuleData -name '*.xslt' | wc -l 2026-09-05 -->

| Module | Where | ModuleData XML | XSLT | Cross-ref sweep | Schema checks | Commit hook fires |
|---|---|---|---|---|---|---|
| TAOM | this repo, `Main/_Module/ModuleData` | 284 (169 of them under `Languages/`) | 8 | yes | yes | yes, for Claude-driven commits |
| `LOTRLOME_Armory` | game install | 425 | 8 | yes, via `extra_ref_roots` | no, by design | no |
| `TAOM_Map` | game install | 44 | 1 | yes, since #462 | no | no |
| total | | 753 | 17 | | | |

Three consequences the table cannot show on its own.

- **Editing either live module gates nothing automatically.** The hook matches staged
  `Main/_Module/ModuleData/*.xml` (`check-moduledata-validation.sh:71`) and neither live module is
  in git, so an Armory or map edit stages nothing and is checked by nothing until you run
  `python tools/validate_moduledata.py` yourself. The RCA is blunt about it: "The Armory is
  untracked, so the commit hook that would have caught it never fires"
  ([rca-armoury-keyforce-cleanup-2026-09-01.md](../reviews/rca-armoury-keyforce-cleanup-2026-09-01.md)).
- **The Armory gets reference checks, not structure checks.** Duplicate ids, enums, the civilian
  rule and `MOUNTED_DWARF` apply to repo files only. That is free today because the Armory defines
  items and monsters and nothing else; author a troop or an equipment roster there and it is
  entirely unvalidated ([moduledata-validation rule](../../.claude/rules/moduledata-validation.md)).
- **No tool models XSLT.** `check_external_xslt.py` proves 17 stylesheets are well formed and
  nothing more. `TAOM_Map/ModuleData/settlements.xslt` is opened only to regex one boolean, and
  `/xslt-check` reads the repo's copies alone. A stylesheet that rewrites a vanilla culture in place
  is invisible to every gate here ([tools/README.md](../../tools/README.md) "No tool in this section
  models XSLT").

## What the commit hook covers, and what it does not

`check-moduledata-validation.sh` runs the error-severity checks before a `git commit` and blocks on
failure. Four limits, all of them load-bearing.

1. **It is an explicit `--code` allowlist, not "all errors".** The hook names 18 codes.
   <!-- measured: grep -oE '\-\-code [A-Z_]+' .claude/hooks/check-moduledata-validation.sh | wc -l 2026-09-05 -->
   A new error check is silently non-blocking until it is added there. On 2026-08-31 five live error
   checks were missing from that list; `CommitGateCoverageTests` in
   `tools/tests/test_validate_moduledata.py` is now the reference between the two files.
2. **Warnings never block.** `MISSING_CIVILIAN_TYPE`, `INVALID_ENUM`, `DUPLICATE_ITEM_DEF`,
   `BROKEN_PARTY_TEMPLATE_REF`, `INCONSISTENT_ARMOUR_SLOT` and `UPGRADE_ARMOUR_REGRESSION` are
   warnings. Run the tool yourself to see them.
3. **It fires only inside a Claude Code session.** These hooks run when Claude invokes Bash through
   the tool dispatch; they do not fire when you type `git commit` in your own shell
   ([harness-facts.md:93](../../.claude/rules/harness-facts.md)). If you edit by hand and commit by
   hand, no hook has ever seen your change.
4. **It fails open on purpose.** Missing python, missing game install, validator crash or an inner
   timeout all let the commit through rather than blocking on the gate's own fault
   ([hooks-catalog.md](../reference/hooks-catalog.md)).

The second data hook, `check-polearm-shield-parity.sh`, runs after an edit to
`weapon_descriptions.xslt`, anything under `LOTRLOME_items`, or any XML whose contents include an
`<EquipmentRoster>`. It is advisory and always exits 0.

## Exit-code traps

A green exit is not the same claim in every tool.

| Tool | Exit code behaviour |
|---|---|
| `validate_moduledata.py` | 1 on any ERROR, 0 when clean, 2 on bad input. Warnings do not change the code |
| `audit_mount_parity.py` | report-only, **always 0**. Read the output; checking the return code proves nothing (`tools/README.md:56`) |
| `audit_polearm_shield_parity.py` | 1 on a polearm violation, WARN on two-handers, and SKIP at exit 0 when there is no game install |
| `check_prefab_budget.py` | prints `OK` while the real cross-module total sits near the cap, because it counts `TAOM_Map/Prefabs` alone |
| `audit_action_set_parity.py` | non-zero on either failure class, so it is safe to chain |
| `triage_battle_load.py` | 1 for any diagnosed hang, 0 for COMPLETED or UNKNOWN, 2 for a bad path. UNKNOWN means diagnostics were off, not that the load was fine |
| `Settlement-Breakdown.ps1` | exits 2 when the file is missing |

**A zero needs a positive control before you believe it.** Auditing whether any dwarf equipment
roster carried a horse, an XPath of `.//equipment[@slot='Horse']` returned zero hits in every
culture. The element is `<Equipment>` and XPath is case sensitive, so the query could not match
anything, and a clean zero is indistinguishable from the answer you wanted. It was caught only
because a second pass counted 582 non-dwarf rosters that did carry horses in the same loop
([testing-qa.md](../reviews/lessons/testing-qa.md) "An audit query that reports zero found needs a
positive control"). Run the same query against a case you know is positive before trusting a
negative. The dev console does this the other way round: its discovery audit asks the engine for a
vanilla control command alongside every `taom.*` one, which is what makes a negative reading
decisive ([dev-console.md](../features/dev-console.md) "The launch gate").

## When to re-run after a live-module update

Five fixes are patched into the two unversioned live modules and an update silently reverts them.
Re-run these after any Armory or map reinstall, and after Steam updates the game.

| Script | Re-run condition |
|---|---|
| `tools/register_one_handed_polearms.py` | any `LOTRLOME_Armory` update |
| `tools/oneoff/fix_uruk_hai_hands_teamcolor.py` | any `LOTRLOME_Armory` update |
| `tools/oneoff/fix_dwarf_female_underwear_mesh.py` | any `LOTRLOME_Armory` update |
| `tools/oneoff/fix_orphaned_tavern_conversation_actions.py` | any `LOTRLOME_Armory` update |
| `tools/oneoff/retag_khand_to_variag.py` | any `TAOM_Map` reinstall |

`SETTLEMENT_ECONOMY_FLOOR` is the one error code that watches a live module for you: it fires when a
settlement drops below the floor in `tools/settlement_economy_floor.json`, which is exactly what a
map reinstall causes. Re-apply with
`python tools/rebalance_settlement_prosperity.py --culture-floor-file tools/settlement_economy_floor.json --apply`.

## Ten scripts with no dry run

These write unconditionally. None of them has an `--apply` or a `--dry-run` flag at all, so there is
no preview and no confirmation step. Commit or copy the target files first.

<!-- measured: grep -cE '\-\-apply|\-\-dry-run' on each of the ten files, all returned 0 2026-09-05 -->

`tools/generate_batch2_wanderers.py` · `tools/extract_wanderers.py` ·
`tools/harvest_factionmap_strings.py` · `tools/generate_new_factions.py` ·
`tools/generate_new_faction_kingdoms.py` · `tools/insert_new_factions.py` ·
`tools/insert_new_faction_cc_menus.py` · `tools/oneoff/generate_tavern_mercenaries.py` ·
`tools/oneoff/apply_hero_bios.py` · `tools/oneoff/_harvest_lotr_issue_strings.py`

Two more are stdout-only and just as destructive in practice: `tools/generate_gondor_troops.py` and
`tools/generate_rhun_troops.py` have no write mode, so the documented usage is a shell redirect
printed in their own docstring (`tools/generate_gondor_troops.py:5`) and pointed at the real file by
`tools/README.md:83-84`. That regenerates the whole troop file and destroys every hand edit made
since. Confirmed in the source: without `--dry-run`, `generate_gondor_troops.py:1917` prints the
entire XML and writes nothing itself. `tools/generate_dale_troops.py` is the modern shape
(`--dry-run` / `--apply` / `--output`) and is the one to copy.

**There is no table anywhere mapping a generated file to the tool that owns it.** TAOM has never
written one, so do not assume a file is safe to hand-edit. The only whole-file regenerations proven
here are `troops_gondor.xml` and `troops_rhun.xml`; every other writer must be checked one at a time.
To find the owning tool for a file, grep the tools folder for the file's name and read each
docstring: `rg -l "taom_partyTemplates.xml" tools/*.py` returns eight scripts today, and
`tools/generate_clan_heraldry.py:27-29` shows the flag shape you want to see (a dry run by default,
an explicit `--apply`). The nearest thing to an index is [tools/README.md](../../tools/README.md),
which has no row for 58 of the 183 top-level scripts.
<!-- measured: counted every tools/*.py and tools/*.ps1 basename with no backticked mention in tools/README.md, 58 of 183 2026-09-05 -->

## Worked example: after editing armour

The sequence below is what to run after touching an Armory item file or a troop roster. Output is
real, captured today, with lines removed rather than reworded: the three "Loaded 3 schemas" lines are
dropped, and the two "Also sweeping refs in" lines had their absolute install path replaced.

<!-- measured: the five commands below, run in order from the repo root 2026-09-05 -->

```text
$ python tools/validate_moduledata.py
Registry: 5,900 items, 5,291 NPCCharacters, 40 cultures, 476 party templates, 121 body properties
Also sweeping refs in: <install>/Modules/LOTRLOME_Armory/ModuleData
Also sweeping refs in: <install>/Modules/TAOM_Map/ModuleData
  WARNING INCONSISTENT_ARMOUR_SLOT   troops/troops_dunland.xml:3174 [dunland_militia_spearman]
            slot "Head" is filled in 1 of 3 battle sets. ...

=== SUMMARY ===
  0 error(s), 94 warning(s)
    INCONSISTENT_ARMOUR_SLOT     94

$ python tools/validate_all_troop_refs.py
  gondor         troops=189  armor_refs=291  missing=0   PASS
  ...
  umbar          troops=16   armor_refs=44   missing=0   PASS

PASS: all armor refs resolve across all cultures.
       Scope: ARMOR ids only. Weapons, arrows, mounts and harnesses are NOT
       checked here. For those run: python tools/audit_item_refs.py

$ python tools/audit_item_refs.py
  5910 item IDs defined across all modules
  2955 distinct items referenced from TAOM
=== BROKEN REFERENCES: 0 distinct items ===
Total broken refs: 0 sites across 0 item IDs

$ python tools/validate_mesh_refs.py
=== SUMMARY ===
  0 error(s), 3 warning(s), 0 info
    KNOWN_DEAD_MESH            3

$ python tools/audit_polearm_shield_parity.py
PASS: no shield roster carries a Polearm its primary usage forbids.
```

Three things to read out of that transcript rather than skim past.

1. **The registry line is the install check.** `Registry: 5,900 items ...` is how you know the game
   install was found. A tiny item count means the validator fell back to TAOM-only registries and
   every `BROKEN_ITEM_REF` verdict that run is worthless.
2. **`0 error(s), 94 warning(s)` is a pass, not a clean bill.** Those 94 are
   `INCONSISTENT_ARMOUR_SLOT`: a slot filled in some of a troop's battle sets and empty in others.
   The engine picks each slot from an independently chosen set, so the troop can spawn with a gap
   nobody authored, and every UI surface renders set number one and looks fine.
3. **`validate_all_troop_refs.py` prints its own scope, and it is narrower than it sounds.** Armour
   ids only, and ten cultures only. Weapons, arrows, mounts and harnesses need `audit_item_refs.py`.

**Check:** `python tools/validate_moduledata.py` returns `0 error(s)`.
**Takes effect:** full game restart (a NEW item or equipment XML file is registered only at process
launch; an edit to an existing file loads at new campaign).
**Code:** No code changes needed.

## Green validator, naked troop

This is the single most common false-clean result, and the diagnosis is mechanical.

The validator parses XML off disk in a Python process. It proves references resolve **on disk, now**.
It does not prove the engine loaded anything. Bannerlord registers each
`<XmlName id="Items" path="LOTRLOME_items/<culture>"/>` **directory** at process launch and globs it
at campaign start, with no hot reload (`Module.cs:246`, `Campaign.cs:1471`, `MBObjectManager.cs:894`).
So a NEW item file added while the game is running is null in engine and the character spawns naked,
while the validator, the build and the whole unit-test suite pass.

Work through it in this order.

1. **Did you add a new file, or edit an existing one?** New file plus no restart is the answer more
   often than not. Quit Bannerlord fully and relaunch. Alt-tabbing back is not a restart.
2. **Is the file in a registered location?** Items are directory-registered and globbed, so any
   `.xml` in a registered folder loads. Troops are the opposite: registered one file at a time.
   `Main/_Module/SubModule.xml` carries 16 `<XmlName id="NPCCharacters" path="troops/...">` rows
   against 16 `troops_*.xml` on disk, so a seventeenth troop file loads only after you add its row.
   The Armory carries 21 `<XmlName id="Items">` directory rows.
   <!-- measured: rg -c 'XmlName id="NPCCharacters" path="troops/' Main/_Module/SubModule.xml ; ls Main/_Module/ModuleData/troops/troops_*.xml | wc -l ; rg -c 'XmlName id="Items"' <install>/Modules/LOTRLOME_Armory/SubModule.xml 2026-09-05 -->
3. **Did a backup get globbed?** A backup saved as `foo.bak.xml` inside a registered items folder is
   real data to the engine and injects duplicate ids. Backups must not end in `.xml`; the Armory's
   own backups use `.bak-deadpieces-20260901` and similar. See [editing-safely](editing-safely.md).
4. **Did your repo edit reach the install at all?** The repo's `Main/_Module` is copied into
   `<install>/Modules/TAOM` by the build, not by saving the file. Nothing copies it for you. Also
   note the copy is additive: the deployed `Modules/TAOM/ModuleData` holds 286 XML against the
   repo's 284, the two extra being `troops/troops_bluecraig.xml` and
   `troops/troops_mistymountainorcs.xml`, deleted from the repo and still sitting in the install.
   Those two are harmless only because no `<XmlName>` row names them.
   <!-- measured: diff of `find . -name '*.xml' | sort` under Main/_Module/ModuleData and <install>/Modules/TAOM/ModuleData 2026-09-05 -->
5. **Only now suspect the data.** Run `python tools/validate_mesh_refs.py`: a resolving item id with
   a dead mesh renders as nothing at all.

## In-game smoke: what no script can answer

Load a **fresh campaign** and check what you changed. The full tester script is
[tester-checklist-discord.md](../testing/tester-checklist-discord.md); the short version for a data
edit is this.

- **Recruit the troop you touched and look at it.** No underwear anywhere, in any culture. The
  checklist repeats "No faction should have underwear troops" per culture for a reason.
- **Walk the upgrade path to the top.** A tier that never arrives is a data defect the validator now
  catches as `UPGRADE_TIER_COLLAPSE`, but a path that costs the wrong XP is not.
- **Open the party screen and hover an upgrade.** That hover is where a zero-cost upgrade edge used
  to crash the game.
- **Save after ten in-game days and reload.** Custom data (race assignments, diplomacy state, troop
  weights) has to survive the round trip.
- **In a battle, use the console.** `taom.print_agent_info <name>` reports race, monster, action set,
  skeleton, mount and spawn equipment for a live agent, and `taom.spawn_troops <id> <n> enemy`
  composes the exact fight you want to see. `taom.print_battle_scene` tells you which terrain a fight
  here would load, and zero candidates is the finding. Full table:
  [dev-console.md](../features/dev-console.md).
- **If a battle hangs on load**, do not read the log by hand. Run
  `python tools/triage_battle_load.py <taom_debug_*.log>` for a one-line verdict, or point it at the
  whole crash bundle with `--bundle`. `EQUIPMENT` names the stuck agent's items;
  `SCENE` and `PRE_SCENE` mean the fault is code, not your data
  ([battle-load-diagnostics.md](../features/battle-load-diagnostics.md)).

## Ask a developer

These cannot be checked with the tools above and are not a data modder's job to fix.

| Situation | Why it needs a developer |
|---|---|
| The engine XSD layer (a missing required attribute, an undeclared extra attribute) | the TAOM validator does not model it, and `characters/clans.xml` has no schema at all. Only the Bannerlord editor or an engine load will say. This is how `clan_umbar_3` shipped with no home settlement |
| Anything a stylesheet does | no gate interprets XSLT. `spcultures.xslt` rewriting a vanilla culture in place is invisible to every check here; `CulturePartyTemplateTests` is the only detector and it is a C# test |
| A culture fielding another faction's troops | the ref sweep proves an id resolves, never that it points at the right faction. That gap hid nine cultures' worth of Calradian troops for months |
| A lord with the wrong name or sex for his body | free text behind a localization key; `LordNameAndSexConsistencyTests` owns it |
| The C# suite | `dotnet test TAOM.Tests` needs the build. Shipped-data tests such as `CultureLordTemplateTests` and `CulturePartyTemplateTests` read ModuleData off disk and are the layer between the validator and the game ([testing-guide.md](../ai-includes/testing-guide.md) "Shipped-Data Tests") |
| Harmony patches, GameModels, MissionLogics, CampaignBehaviors | cannot be unit tested at all; they need a running game (`testing-guide.md` "What Cannot Be Unit Tested") |
| Deploying your repo edit into the install | the copy happens during a build, not on save |

## The ordered check sequence

Run these in order. Each one is cheap; the expensive step is the campaign at the end.

1. `python tools/validate_moduledata.py` (always, whatever you edited).
2. The category tables above, for the thing you actually touched.
3. `python tools/check_external_xslt.py` if any `.xslt` changed.
4. `python tools/audit_mbproj_registration.py` if you added a file.
5. Ask a developer for `dotnet test TAOM.Tests` if you touched cultures, party templates or lords.
6. Full game restart, new campaign, the in-game smoke above.

For a targeted question mid-edit rather than a full run, the `taom-moduledata` MCP server exposes the
same engine as nine tools (`item_exists`, `troop_exists`, `culture_exists`, `find_references`,
`list_cultures`, `registry_sizes` and the rest). It needs a Claude restart to load
([mcp-servers.md](../reference/mcp-servers.md)); the command-line equivalent is always
`python tools/validate_moduledata.py`.

## Rehearsal runs, 2026-09-05

Every gate quoted in this chapter was run against the tree as it stands today. This is the record, so
a future reader can tell drift from breakage.

<!-- measured: each command in the first column, run from the repo root 2026-09-05 -->

| Command | Exit | Result |
|---|---|---|
| `python tools/validate_moduledata.py` | 0 | 0 errors, 94 warnings, all `INCONSISTENT_ARMOUR_SLOT` |
| `python tools/validate_all_troop_refs.py` | 0 | 10 cultures swept, 647 troops, 1,713 armour refs, 0 missing |
| `python tools/audit_item_refs.py` | 0 | 5,910 item ids, 2,955 referenced, 0 broken |
| `python tools/validate_mesh_refs.py` | 0 | 0 errors, 3 `KNOWN_DEAD_MESH` warnings (the troll set) |
| `python tools/audit_polearm_shield_parity.py` | 0 | PASS, with two-hander warnings on the Mordor Numenorean rosters |
| `python tools/check_external_xslt.py` | 0 | PASS, 17 stylesheets clean |
| `python tools/generate_armory_catalogue.py --check` | 0 | catalogue reproduces exactly, 4,839 rows |
| `python tools/audit_action_set_parity.py` | 0 | humanoid sets clean; 9 creature sets excluded from the human audit |

The 94 warnings and the 3 dead meshes are known and accepted, not new. If your run shows more,
compare against this table before assuming the tool broke.

## Numbers in this chapter

All measured 2026-09-05 from the repo root.

| Number | Command |
|---|---|
| 37 read-only gates | `inv-tools.json` rows with `category` in (`validator`, `audit`) and `modder_usable: true` |
| 284 TAOM ModuleData XML, 169 under `Languages/` | `find Main/_Module/ModuleData -name '*.xml' \| wc -l` and the same under `Languages` |
| 425 Armory ModuleData XML, 44 TAOM_Map | `find <module>/ModuleData -name '*.xml' \| wc -l` |
| 8 + 8 + 1 = 17 stylesheets | `find <module>/ModuleData -name '*.xslt' \| wc -l`, confirmed by `check_external_xslt.py`'s `PASS: 17 stylesheet(s) clean` |
| 0 errors, 94 warnings | `python tools/validate_moduledata.py` |
| 5,900 items / 5,291 NPCCharacters / 40 cultures / 476 party templates / 121 body properties | the `Registry:` line of the same run |
| 5,910 item ids, 2,955 referenced, 0 broken | `python tools/audit_item_refs.py` |
| 3 `KNOWN_DEAD_MESH` warnings | `python tools/validate_mesh_refs.py` |
| 4,839 catalogue rows | `python tools/generate_armory_catalogue.py --check` |
| 18 `--code` entries in the commit hook | `grep -oE '\-\-code [A-Z_]+' .claude/hooks/check-moduledata-validation.sh \| wc -l` |
| 16 troop files on disk, 16 registered, 10 swept, 6 unswept | `ls Main/_Module/ModuleData/troops/troops_*.xml \| wc -l`; `rg -c 'XmlName id="NPCCharacters" path="troops/' Main/_Module/SubModule.xml`; the `cultures` list at `tools/validate_all_troop_refs.py:80-97` |
| 21 Armory `<XmlName id="Items">` rows | `rg -c 'XmlName id="Items"' <install>/Modules/LOTRLOME_Armory/SubModule.xml` |
| 286 deployed XML against 284 in the repo, 2 orphans | `diff` of `find . -name '*.xml' \| sort` under both trees |
| 165 `tools/*.py`, 18 `tools/*.ps1`, 64 `tools/oneoff/*.py` | `ls tools/*.py \| wc -l` and the two siblings |
| 10 scripts with no dry run | `grep -cE '\-\-apply\|\-\-dry-run'` on each of the ten, all 0 |

The six unswept troop files are `dunland`, `goblin`, `harad`, `mirkwood`, `rivendell` and `rohan`.
The tool says so itself at `tools/validate_all_troop_refs.py:90-92`: a new culture must be appended to
that hardcoded list or its troop file is never opened. Any doc claiming the tool covers "all 7
culture troop XMLs" is wrong on both numbers.

## Read next

- [Editing safely](editing-safely.md) for BOM, line endings, backup naming and which copy is live;
  [File catalogue](file-catalogue.md) and [Modules overview](modules-overview.md) for what each file
  is and which module owns it.
- [Troubleshooting](troubleshooting.md) for symptom-first diagnosis once a check has failed.
- [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md) for the validator's
  design, the schema model and the full coverage boundary.
- [`.claude/rules/moduledata-validation.md`](../../.claude/rules/moduledata-validation.md) for the
  issue-code table and the per-module matrix in rule form.
- [`tools/README.md`](../../tools/README.md) for every script, and its mandatory XML I/O convention
  at the top before you write or modify one.
- [`docs/reference/hooks-catalog.md`](../reference/hooks-catalog.md) for what each hook does.
- [`docs/ai-includes/testing-guide.md`](../ai-includes/testing-guide.md) for the three test types and
  what cannot be unit tested.
- [`docs/reviews/lessons/testing-qa.md`](../reviews/lessons/testing-qa.md) and
  [`docs/reviews/lessons/build-tooling-workflow.md`](../reviews/lessons/build-tooling-workflow.md)
  for the failures these gates were built from.
- [`tools/BannerlordCraftingTool/README.md`](../../tools/BannerlordCraftingTool/README.md) for the
  standalone crafting-piece offset previewer, the one graphical tool here: it reproduces the engine's
  piece positioning, so a haft can be aligned without launching the game and with no build required.
