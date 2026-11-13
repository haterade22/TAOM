# lane-tools findings: Python tools, catalog, tools suite

Baseline `7087fa633` (`bannerlord-1.5.x`, v2.0.38), read in `E:/repos/taom-improve/2026-11-12/wt-baseline`.
Scope: `tools/` (741 tracked `.py`, 252 `tools/tests/test_*.py`; `git ls-files 'tools/*.py'`), `tools/README.md`
(724 lines), `build.ps1` and the `.ps1`/`.sh` under `tools/` (read only for references).
Read-only. I ran no tool that writes. I ran `python -m ruff check tools --no-cache` (clean), a reference
census (scratch script `E:\repos\taom-improve\2026-11-12\scratch\lane-tools\dead.py`), and the whole Python
suite once under a timing wrapper (`timing_runner.py`, same folder) with `TEMP` on the E: scratch folder.
`git status --short` in the baseline worktree was empty before and after.

How the "no reference" census works: every tracked text file outside `factionmap_output` and `Languages/` is
tokenised once (515,331 distinct `\w+` tokens, 10,697 files); a script is referenced when its stem appears as
a token in any other tracked file. 525 scripts under `tools/` (not tests, not `factionmap_output`) were
checked. Only 21 have no reference outside `tools/README.md`, tests and `plans/`/`docs/reviews/`; 12 of those
also lack a README row. Almost everything else is named at least in `docs/modding/file-catalogue.md` or a
changelog archive, so "no reference at all" finds almost nothing; the real waste is finished work that still
sits in the living tree (TOOLS-01, TOOLS-02) and copied helpers (TOOLS-05, TOOLS-06).

Python suite at the baseline, from my own run: `ran 8578 fail 7 err 2 skip 13`, 427.8 s wall while other
agents were busy (the orchestrator's `baseline.md` has 363 s uncontended and the same 7 + 2 failing tests).
Per-test times are in `scratch\lane-tools\timing.json`. Sum of test times 418 s; 47 tests over 1 s sum to 157 s;
173 tests over 0.5 s sum to 241 s.

## Ranked findings

### [TOOLS-01] Delete the five scripts the catalog itself marks retired or superseded

- **Evidence**:
  - `tools/README.md:173`: `generate_xslt.py` is "**RETIRED, do not run.**" (303 lines, `tools/generate_xslt.py:295`
    writes the shipped `Main/_Module/ModuleData/spcultures.xslt` through the hardcoded
    `c:/Users/mikew/source/repos/TAOM/...` path, reads `E:/LOTRAOMAssets/...`). Last commit 2026-03-06. No code,
    test, hook or skill references it; only the README, `docs/modding/file-catalogue.md:50`,
    `docs/reference/provenance-register.md:301` and two changelog archives do.
  - `tools/README.md:174`: `generate_batch2_wanderers.py` (687 lines, last commit 2026-10-16) "Stale: it opens
    `ModuleData/taom_wanderer_equipment.xml`, but the file lives in `equipmentsets/`; do not run it." Its only
    code reference is the gate list `tools/check_generator_item_refs.py:95`, which then guards a script nobody may run.
  - `tools/README.md:52-54`: `validate_all_troop_refs.py` (107 lines), `validate_gondor_refs.py` (74 lines) and
    `audit_item_refs.py` (135 lines) are "superseded by `validate_moduledata.py`'s `BROKEN_ITEM_REF`". Code references
    are comments (`tools/taom_schema.py:14`, `tools/validate_moduledata.py:3-4`, `tools/audit_polearm_shield_parity.py:24`),
    one printed hint (`tools/triage_battle_load.py:1208`, "python tools/validate_all_troop_refs.py") and
    `validate_all_troop_refs.py` importing `validate_gondor_refs.py`. No test references any of the five.
  - Command: `python -B scratch\lane-tools\dead.py` then `dead.json` rows for these stems; `git grep -n -E "audit_item_refs|validate_all_troop_refs|validate_gondor_refs|generate_batch2_wanderers" -- tools/*.py .claude ':!tools/README.md'`.
    Scope searched: every tracked text file (see census above), not the maintainer's untracked scratch.
- **Impact**: 1,306 lines of scripts that the catalog says never to run, one of which would overwrite a hand-corrected
  stylesheet if run on a machine where the C: path exists. They stay in every grep and in the `GENERATORS` gate.
- **Effort**: S.
- **Risk**: LOW. Edits needed in the same change: drop the `generate_batch2_wanderers.py` entry from
  `tools/check_generator_item_refs.py:95` (and its test expectations in `tools/tests/test_check_generator_item_refs.py`
  if they count entries), repoint the hint at `tools/triage_battle_load.py:1208` to `python tools/validate_moduledata.py`,
  update `docs/reference/provenance-register.md:301` and the README rows 52-54, 173-174 and the line 712 path list.
  `tools/check_provenance.py` may read the register: run it. A maintainer who runs `audit_item_refs.py --show-locations`
  by hand loses a per-reference location dump; `validate_moduledata.py` has the `BROKEN_ITEM_REF` finding. UNVERIFIED
  whether `--show-locations` has an equivalent there.
- **Confidence**: HIGH that the code is unreferenced; MED that the maintainer wants the location dump gone.
- **Fix sketch**: `git rm` the five files, apply the edits above, run `python tools/lint_docs.py --fail-on-drift`,
  `python tools/check_provenance.py`, the tools suite.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (deletion, clean verification: the suite plus the doc linter).
- **Issue**: none.

### [TOOLS-02] Move the 25 finished one-shot scripts from `tools/` into `tools/oneoff/` as the README convention says

- **Evidence**:
  - `tools/README.md:309-313`: "one-off scripts land in `tools/oneoff/` ... The top-level `tools/` dir holds only
    living, recurring tools." Yet 25 top-level scripts are labelled finished one-offs, one-shots or completed revamps
    by their own README rows (sections "Cleanup (One-Shot)", lines 244-257, and "Troop revamps ... completed one-offs",
    lines 259-276). Total 9,264 lines. Names: `apply_{gondor,mordor,isengard,dolguldur,gundabad,erebor}_troop_revamp.py`,
    `apply_gondor_polish_224.py`, `apply_erebor_equipment_sweep.py`, `apply_umbar_equipment.py`, `cleanup_deleted_{gondor_armor,gondor_items,troops_212}.py`,
    `expand_party_templates_212.py`, `rollback_erebor_iron_misfile.py`, `add_education_roster_cultures.py`,
    `apply_career_rank_names.py`, `fix_promoted_culture_flavor.py`, `rename_maxcharge_to_cooldownreduction.py`,
    `repaint_kingdom_colors.py`, `retune_phantom_{passives,descriptions}.py`, `generate_new_factions.py`,
    `raise_party_template_maxes.py`, `patch_quad_movement.py`, `translation_status.sh`.
    List built by `scratch\lane-tools` inline script: README rows matching "Finished one-off|completed one-off|One-off:|One-shot|refuses since|no longer runs" or sitting under those two section headings, whose script exists under `tools/`.
  - `tools/README.md:265,273`: `apply_gondor_troop_revamp.py` and `apply_gondor_polish_224.py` refuse `--apply` since 2026-09-13.
  - None of the 25 has a test in `tools/tests` (checked: the census `tests` column is empty for every troop-revamp, cleanup and
    retune script; `generate_new_factions.py` and `raise_party_template_maxes.py` need a quick `git grep -l <stem> -- tools/tests` re-check by the executor).
    Their only code references are one another: `apply_mordor_troop_revamp.py` names `apply_gondor_polish_224.py`, `retune_phantom_passives.py` names `retune_phantom_descriptions.py`, `validate_gondor_refs.py` names `cleanup_deleted_gondor_items.py`.
  - Duplication that disappears with the move out of the living tree: the six revamp scripts each define their own `find_npc_block` (`tools/apply_dolguldur_troop_revamp.py:292`, `apply_erebor_troop_revamp.py:765`, `apply_gondor_troop_revamp.py:1162`, `apply_gundabad_troop_revamp.py:272`, `apply_isengard_troop_revamp.py:669`, `apply_mordor_troop_revamp.py:906`) and `replace_equipment` (same six files, e.g. `apply_gondor_troop_revamp.py:1205`).
    Command: `git grep -n -E "^def (find_npc_block|replace_equipment)\(" -- tools ':!tools/tests'`.
- **Impact**: every grep, graphify scan, ruff run and new-contributor read of the living tools meets 9,264 lines that nothing runs. It also blurs the catalog: a tool in `tools/` is supposed to be recurring.
- **Effort**: M (the moves are mechanical; the cost is repairing path mentions).
- **Risk**: LOW for behaviour. `docs/modding/file-catalogue.md`, `docs/features/troop-tree-revamp.md`, `.claude/rules/xml-data.md`, `docs/modding/recipe-retire-content.md` and the README name these paths; `python tools/lint_docs.py` owns dead links, so run it. `tools/README.md:315-316` says a `tools/oneoff/` row exists only when the script has a named re-run condition, so the moved rows can shrink to one line or go.
- **Confidence**: HIGH (the README states the convention and the status; the census shows no living caller).
- **Fix sketch**: `git mv` the 25 files to `tools/oneoff/`, rewrite the paths in the docs above (or delete the ones with no re-run condition and keep a one-line row naming the commit that held them), run `lint_docs.py` and the tools suite. Deleting instead of moving is also defensible for the six troop-revamp scripts (git history holds them); that is the maintainer's call, since the README keeps them "for the mechanical-swap pattern".
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (move), with the delete-versus-move question put to the maintainer for the six revamp scripts.
- **Issue**: none.

### [TOOLS-03] Cut three whole-tree tests that rebuild the same data: about 40 s of 418 s

- **Evidence** (per-test times from `timing.json`, same wrapper run):
  - `tools/tests/test_ingame_verdict.py:328-337` (`RepoManifestGateTests.test_every_named_check_id_appears_in_a_taom_source_file`, 17.1 s, the slowest test in the suite): `taom_source_literals()` at `:267-271` runs the `CS_TOKEN` regex (`:243`) over every `.cs` file under `Main`, `Client` and `CoopCompat` (`rglob`, `:260-264`), though only the handful of check ids named by 22 manifests in `tools/ingame_checks/*.toml` are looked up. `git ls-files Main Client CoopCompat -- '*.cs' | wc -l` printed 10154 for the three folders plus anything else tracked there.
  - `tools/tests/test_doc_graph_ratchet.py:114,121-126`: `test_orphan_docs_agrees_with_the_orphan_count` (5.5 s) and `LiveTreeTests.test_the_live_tree_is_within_the_baseline` (6.5 s) each rebuild the whole docs graph through `tools/check_doc_graph_ratchet.py:58-67`, where `current_metrics()` and `orphan_docs()` both call `_orphans_and_components()`.
  - `tools/tests/test_pack_banner_sheets.py:402-404` (10.0 s, `pbs.audit(pbs.REPO_MODULE)`) is a real image audit; left alone.
- **Impact**: 17.1 + 5.5 s removable with identical coverage, about 22 s (5% of the run); more if the graph build is shared with `lint_docs` tests (not checked).
- **Effort**: S.
- **Risk**: LOW. The prefilter is a necessary condition: a string literal equal to `taom.x.y` must contain the text `taom.x.y` in the raw source (the ids carry no escapes), so skipping files that lack every wanted id cannot hide a hit. Build the wanted set before reading, read each file once as text, call `cs_string_literals` only when a wanted id is a substring. The ratchet pair: compute `(orphans, components)` once in `setUpClass` and compare both views to it, or memoise `_orphans_and_components` for the process if no other test patches `DOCS_DIR` (check `FailureTextTests` first).
- **Confidence**: HIGH on the timings and the cause; MED on the exact saving until re-measured.
- **Fix sketch**: add the substring prefilter to `taom_source_literals(wanted)`; share one graph build per class in the ratchet tests. Re-time with the wrapper in `scratch\lane-tools`.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (tests only, same coverage, existing tests as proof).
- **Issue**: none. Overlaps the tests lane's TST-01 in spirit (that one is the C# suite).

### [TOOLS-04] `skip_ids_for` re-parses every sibling language file for every source: 16 s per suite run and every translator run

- **Evidence**: `tools/translate_with_claude.py:343-363`: for each call it globs `lang_dir/"std_taom_*.xml"` and runs `ET.parse` on each file other than `target`. `tools/tests/test_translate_batching.py:453-467` (`ShippedKeyCoverageTests`, 16.1 s, second slowest test) calls it for every (language, English source) pair: 12 languages times the TAOM sources, so each language directory is parsed once per source instead of once. The same test also calls `t.english_only_override_ids(src)` and `t._parse_string_xml(src, strip_keys=True)` inside the language loop (`:462,464`), both language-independent.
  - Count to check: `python -c "import sys; sys.path.insert(0,'tools'); import translate_with_claude as t; print(len(t.english_source_files('TAOM')), len(t.LANGUAGES))"`.
- **Impact**: 16 s of the suite, and the same quadratic parse in the real `--sync-ids` and diff passes of the translator (it is called per source file there too, per its docstring).
- **Effort**: S.
- **Risk**: LOW if the cache is keyed by `(lang_dir, other)` and lives for one run: the function's contract ("A sibling file that exists but does not parse raises") must still raise on the first parse. A stale cache inside a long run that writes siblings between calls would be wrong, so the cache must not outlive a `--apply` write; the simplest safe form is to hoist the sibling-id sets out of the per-source loop in the callers and pass them in.
- **Confidence**: MED (cause read, saving not re-measured; the translator callers are UNVERIFIED, I did not trace them).
- **Fix sketch**: split `skip_ids_for` into "ids of the siblings of this language directory" (computed once per language) and the per-source remainder; hoist the language-independent source parsing out of the language loop in the test.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, after the callers are traced.
- **Issue**: none.

### [TOOLS-05] 31 top-level scripts still hardcode the install path; the shared `_gamedir.py` serves 91 files

- **Evidence**:
  - `tools/_gamedir.py` (docstring `:1-26`) is the shared resolver: `git grep -l -E "(from|import) +_gamedir" -- 'tools/*.py' ':!tools/tests'` plus path variants gives 91 importing files (my token scan: 91, 86 top-level).
  - 140 non-test files contain the literal `Mount & Blade II Bannerlord`; 68 of those do not import `_gamedir`, and 60 of those 68 do not mention `BANNERLORD_GAME_DIR` either (`git grep -l "Mount & Blade II Bannerlord" -- 'tools/*.py' ':!tools/tests'` against the two other lists). 28 of the 60 are in `tools/oneoff/`; 31 are living top-level scripts, e.g. `tools/check_prefab_budget.py:23`, `tools/validate_mesh_refs.py:89`, `tools/audit_siege_props.py:48`, `tools/check_rdc_entries.py:30`, `tools/generate_dale_armor.py:45`, `tools/rebalance_weapons.py:33`, `tools/tpac_clipinfo.py:57`, plus nine `generate_*_armor*.py` generators. (`tools/taom_schema.py:3043` is a docstring, a false positive.) Some of these take a path flag, which `tools/README.md:709` calls "override enough" for six.
- **Impact**: on the laptop or any other install path these tools fail or read the wrong tree, and the blank-variable and `ensure_exists` protections of `_gamedir.py` do not apply. The README promises that `setup-dev-env.ps1`'s variable steers the tools.
- **Effort**: M.
- **Risk**: LOW. `game_dir(default)` returns the old literal when the variable is unset or blank (`tools/_gamedir.py:36-46`), so behaviour on this desktop is unchanged. A script that already has a path flag keeps it: the flag's default becomes `game_dir(literal)`. Keep generators' two write targets (Steam install and `lotraom-assets`) as they are.
- **Confidence**: HIGH on the counts, MED on "unused by choice" for the six that README names as deliberate.
- **Fix sketch**: change each literal default to `game_dir(r"E:\Steam\...")` (a one-line edit plus the import), executed in batches with each script's existing test or `--help` as the check; after TOOLS-02 the oneoff half drops out of scope anyway.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, in batches by directory.
- **Issue**: none.

### [TOOLS-06] Byte-faithful XML read and write is copied into dozens of scripts; add one helper

- **Evidence**:
  - `tools/README.md:7-33` makes the BOM-preserving idiom mandatory and prescribes it inline, so the convention is the copy. Counts from `scratch\lane-tools\dup.py`: 45 non-test files contain the literal byte string `\xef\xbb\xbf`; 90 contain `utf-8-sig`; 18 define their own `read_text`/`_read`/`read_file`; 5 define `read_xml`/`write_xml`.
  - Near-identical (text, had_bom) pairs: `tools/apply_dead_mesh_item_swaps.py:317`, `tools/audit_gender_variation_flags.py:63`, `tools/generate_starter_kit.py:166`, `tools/retune_career_health.py:165`, `tools/sync_lord_inline_skills.py:68`; `register_armory_names.py:66`, `register_one_handed_polearms.py:106`, `harvest_literal_loc_keys.py:60` and `harvest_duty_strings.py:241` carry text-only variants.
  - Backups: 106 non-test files write a `.bak` copy, under at least eight spellings (`.bak`, `.xml.bak`, `.bak-<tag>-<stamp>`, `.bak_assignments_<stamp>`, ...): `git grep -h -o -E "[\"'f]+[^\"' ]*\.bak[^\"' ]*" -- 'tools/*.py' ':!tools/tests'`. Only `tools/_generated_files.py` (53 lines, 2 users) and `lod_fill_batch.py:289 backup_once` factor it.
  - Shared modules that do exist and their reach, by `import` count: `_gamedir` 91, `_loc_sources` 7, `_marker_region` 5, `taom_schema` 16, `_generated_files` 2.
- **Impact**: every new writer re-copies the idiom; the divergence risk is real (`retune_career_health.py:165` documents a `\r\r\n` file that a text-mode read doubles; `register_one_handed_polearms.py:106` decodes with `errors="replace"`, the others do not). Nothing found is a live bug.
- **Effort**: M (helper plus moving the five identical pairs; leave the 106 backup sites alone).
- **Risk**: LOW for the five identical pairs (each has a test or a dry run); MED if the variants with `errors="replace"` are unified, since that changes failure behaviour.
- **Confidence**: HIGH on the duplication, MED on how much the executor should touch.
- **Fix sketch**: add `tools/_xmlio.py` with `read_xml(path) -> (text, had_bom)` and `write_xml(path, text, had_bom)`; switch the five scripts above first; make `README.md:7-33` point to it for new scripts. Do not rewrite the finished one-offs.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (small first step), conditional on the tests of the five scripts passing.
- **Issue**: none.

### [TOOLS-07] Per-test `git init` fixtures cost about 100 s of the suite

- **Evidence** (per-module sums from `timing.json`): `test_githooks_pre_commit` 30.5 s / 40 tests, `test_integrate_branch` 22.3 s / 38, `test_reviewctl` 22.2 s / 43, `test_improve_ctl` 19.3 s / 62, `test_worktree_audit` 5.1 s / 13, `test_loop_progress` 6.1 s / 14. `tools/tests/test_integrate_branch.py:81-95` and `tools/tests/test_reviewctl.py:15-25` build a fresh temp repo in `setUp` (init, three `config` calls, commits), one `git` process per step. Median test in those modules: 0.59 s and 0.52 s.
- **Impact**: roughly a quarter of the suite is process start-up for repo fixtures, on a platform where each `git` spawn is slow.
- **Effort**: M.
- **Risk**: MED. A template repo copied per test must be byte-isolated (the tests mutate refs); the gain per test is UNVERIFIED (copying 50 small files may cost 0.05 to 0.1 s on this disk, against about 0.4 s of spawns). Measure on one module (`test_reviewctl`) before touching the rest.
- **Confidence**: LOW on the size of the gain, HIGH on the pattern.
- **Fix sketch**: build the base repo once in `setUpClass`, then `shutil.copytree` it per test (config files and ignored paths included), keeping the `GIT_CONFIG_GLOBAL` isolation the tests already set.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no for now; an "investigate" plan with a measurement gate.
- **Issue**: none.

### [TOOLS-08] The catalog's environment section is stale and one tool has no row

- **Evidence**:
  - `tools/README.md:685-686`: "29 tools honour it: 25 resolve it themselves"; `:692` "22 of the 25 do" resolve through `_gamedir.py`; `:707` "The thirteen top-level tools that had no override of any kind now all read `BANNERLORD_GAME_DIR`". Measured at the baseline: 51 non-test files mention `BANNERLORD_GAME_DIR` (`git grep -l "BANNERLORD_GAME_DIR" -- 'tools/*.py' ':!tools/tests'`), 91 import `_gamedir`.
  - `tools/README.md:711-713`: "Other hardcoded paths ... `generate_xslt.py`, `blender/rebuild_anim_from_json.py`" under `c:\Users\mikew\...`. `git grep -n -i -E 'Users.{1,3}mikew' -- 'tools/*.py'` finds that path in `generate_xslt.py:295` and `oneoff/lords-migration/replace_equipment_templates.py:12` (not `rebuild_anim_from_json.py`), plus Blender how-to docstrings in `tools/blender/` and `tools/painter/painter_run.py:4`.
  - `tools/package_quad_toolkit.py` is the only top-level script with no README row (`python scratch\lane-tools\catalog.py`: 319 top-level scripts, 2 unnamed, one being `__init__.py`). It is named only in `LICENSE-CONTENT.md:51` and `tools/quad_toolkit/NOTICE.txt:13`.
  - The rest of the catalog is accurate: all 135 `tools/tests/*.py` names it cites exist; all 375 first-column script names exist (`catalog.py`). The 82 "missing" tokens the same script lists are runtime artifacts (`DONE.txt`, `*_report.json`) and external paths, not tools.
  - 106 scripts under subdirectories have no README row (`oneoff` 51, `blender` 18, `hair_engine` 16, `weapon_xml` 7, `retopo_engine` 4, ...), which the README intentionally allows for `oneoff`.
- **Impact**: a reader sizing the `BANNERLORD_GAME_DIR` work is told the wrong coverage; one packager has no entry.
- **Effort**: S.
- **Risk**: LOW (doc only). Do it after TOOLS-01 and TOOLS-05 so the numbers are written once.
- **Confidence**: HIGH.
- **Fix sketch**: replace the three counts with the command that prints them (the repo's own rule for counts that code can change), delete the `generate_xslt.py` path item, add a row for `package_quad_toolkit.py` modelled on the `package_hair_toolkit.py` row.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (bundle with TOOLS-01).
- **Issue**: none.

### [TOOLS-09] Eleven `tools/oneoff/` scripts have no caller, test, README row or doc beyond a path list

- **Evidence**: census rows with no non-README, non-test, non-`plans/` reference and no README row:
  - `tools/oneoff/lords-migration/{add-face-tags,add-skill-templates,extract_lords,generate_lords_xslt,generate_vanilla_lords_xslt,lowercase-pngs,merge_lords_xslt}.ps1`: LOTRAOM to TAOM lord import steps ("PowerShell script to extract specific lords from LOTRAOM lords.xml and create a new lords.xml file for TAOM", etc.), last commit 2026-07-12, which is the date the sweep moved them (`git log -1 --format=%ad -- <file>`); `docs/reference/provenance-register.md:301` names the folder as `tools/oneoff/lords-migration/*.ps1`.
  - `tools/oneoff/Add-RosterVariants.ps1` (2026-07-12), `tools/oneoff/assign_gondor_scenes.py` ("Distribute the real generic Gondor scenes across Gondor settlements", 2026-07-12), `tools/oneoff/restore_dao_rock_transforms_from_backup.py` (restores dao_rock entity transforms from an older backup of `Main_map/scene.xscene`, 2026-07-12), `tools/oneoff/scout_groupa_loadout.py` ("Scout: dump skill_template + equipment for Group A lords", read-only, 2026-07-12).
  - Command: `scratch\lane-tools\dead2.py` (the 23-row list); `git log -1 --format=%ad -- <file>` per row.
- **Impact**: low; 11 files and a few thousand lines in a folder already set aside. The question is policy: the folder says "kept for reference", the register names it as provenance evidence, so deleting needs the maintainer.
- **Effort**: S.
- **Risk**: LOW for behaviour; the provenance register and its checker (`tools/check_provenance.py`) must be updated if the folder goes. A script the maintainer or an artist runs by hand would have no in-repo caller by definition, so this is a "confirm with the owner" deletion, not a proven-dead one.
- **Confidence**: MED.
- **Fix sketch**: ask the maintainer; if yes, `git rm tools/oneoff/lords-migration` and the four others, edit `provenance-register.md:301`.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no until the maintainer decides (put it in the run's decision list).
- **Issue**: none.

### [TOOLS-10] The tools suite fails seven tests and errors on two at the baseline, all reading shipped data that has drifted

- **Evidence**: my run and `baseline.md`: `ran 8578 fail 7 err 2 skip 13`. Errors: `tools.tests.test_make_faction_emblems.ShippedDataTests.*` raise `ValueError: high_kingdom_of_lindon: sigil 505 has no ui_taom_bannericons sprite` from `tools/make_faction_emblems.py:110`. Failures: `test_clan_heraldry_specs.py:65` ("clans.xml would change", colours `FF7FAFC9` vs `FF758DA2`), `test_generator_regions_in_sync` (three generator `--check` subtests), three `test_lindon_clan_expansion` data tests. Traces in `scratch\lane-tools\timing_out.txt`.
- **Impact**: `/verify` Step 2b is red on a clean trunk, so a real tools regression hides among the known failures; executors must compare by name.
- **Effort**: UNVERIFIED (the cause may be live-install state: several of these tests read the live `TAOM_Map` or `LOTRLOME_Armory` and compare it with the repo; I did not check which side is stale).
- **Risk**: game data is outside this run's scope (BRIEF "Not audited"), so this is a lead.
- **Confidence**: HIGH that they fail, LOW on the cause.
- **Fix sketch**: none from this lane; the maintainer or the session that owns the Lindon work should confirm whether the live install or the repo lags.
- **Delta**: pre-existing at the baseline (orchestrator measured the same set).
- **P1**: no.
- **Plan candidate**: no (behaviour change / data).
- **Issue**: none found by me (not searched; `gh` not used).

## Considered and rejected

- Ruff style classes: `ruff.toml:8-10` decides F541/F401/F841 are churn; the counts are now 129 F541, 62 F401, 52 F841 (`python -m ruff check tools --no-cache --isolated --select F401,F841,F811,F541 --statistics`) against the 132/50/31 the file quotes, so the unused-variable count has grown by 21. Decided; recorded only so the number is not re-measured. Not a finding.
- `.githooks/pre-commit` and the Claude `check-moduledata-validation.sh`/`check-doc-config-drift.sh` both run `validate_moduledata.py` and `lint_docs.py` on a commit: decided and dated, "they run beside the Claude commit gates for two weeks of agreement, and then those gates go" (`.githooks/pre-commit:20-21`, 2026-11-03). The harness lane owns it.
- C# suite versus tools tests running the same work: `git grep -n -E "python|\.py" -- TAOM.Tests --include=*.cs | grep -i -E "Process|Start|Arguments"` found no test that starts a Python process, so nothing is executed twice across the two suites. Several C# tests read the same shipped XML as `tools/tests` (for example `StarterKitCoverageTests`, `PlayerStartCoverageTests`, `CareerCultureCoverageTests`), which is a deliberate second guard, not a repeat run.
- Merging `current_metrics`-style graph builds with `lint_docs` graph work: not read far enough to claim a saving.
- Deleting `tools/blender/creature_fit_view.py` (no README row, no caller): its docstring says it opens a creature-fit scene in the Blender window; a by-hand artist viewer, last commit 2026-09-30. Keep.
- A shared argparse (`--dry-run`/`--apply`) helper: `--apply` is declared in 186 files and `--dry-run` in 81 (`dup.py`), but the semantics differ per script (README: some default to apply, some to dry run). Not a safe merge.
- `import data_gates` count of 0: it is a CLI named by `.ai/verification.md`, `.claude/skills/verify/SKILL.md` and `.claude/rules/moduledata-validation.md`; not dead.
- `except: pass` in 49 tool files: correctness is not this run's lane; not examined.

## What I did not cover

- The 101 scripts in `tools/oneoff/` were censused for references and last-touch only; I read no script body beyond docstrings, and I did not run any `--help`.
- Subdirectory engines (`tools/blender`, `hair_engine`, `retopo_engine`, `weapon_xml`, `painter`, `petik`, `quad_engine`) were only counted, not audited for dead functions. A function-level dead-code scan (for example with `vulture`) was not run.
- Duplicated XML-loading logic beyond the byte-faithful read/write pair: I did not compare the item, NPC and troop loaders across validators against `taom_schema.py` (3,222 lines, 16 importers). Count of scripts that parse the same files without it: UNVERIFIED.
- PowerShell and shell scripts under `tools/` (26 `.ps1`, 4 `.sh` at top level) and `build.ps1` were checked only for catalog presence and references, not read; the harness lane owns `build.ps1`.
- The 13 skipped tests were not examined; `TST-07` of the tests lane covers skip-instead-of-fail.
- My timing run overlapped other agents' work (427.8 s against the orchestrator's 363 s), so absolute seconds are inflated; the ranking and the per-test shares are what I rely on. The savings in TOOLS-03, 04 and 07 were not re-measured after a change (read-only).
- I did not check GitHub issues for overlaps (`gh` not used), so every "Issue: none" means "none known to me".
