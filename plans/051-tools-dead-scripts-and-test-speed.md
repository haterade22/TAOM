# Plan 051: Three retired tool scripts are gone, the catalog's environment section names its commands, and two tool tests stop re-parsing the same files

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: from your worktree,
> `git diff --stat 7087fa633..HEAD -- tools/generate_xslt.py tools/generate_batch2_wanderers.py tools/validate_gondor_refs.py tools/check_generator_item_refs.py tools/tests/test_check_generator_item_refs.py tools/README.md tools/validate_all_troop_refs.py tools/translate_with_claude.py tools/tests/test_translate_batching.py tools/check_doc_graph_ratchet.py tools/tests/test_doc_graph_ratchet.py docs/modding/file-catalogue.md docs/modding/skill-sets.md docs/modding/validation-and-testing.md docs/modding/wanderers-and-named-companions.md docs/reference/provenance-register.md docs/features/gondor-armor-revamp.md docs/features/multi-culture-armor-revamp.md docs/migration/templates/equipment-rosters.md tools/package_quad_toolkit.py tools/extract_wanderers.py Main/_Module/SubModule.xml tools/_loc_sources.py tools/analyze_armor_balance.py tools/apply_settlement_buildings.py tools/derive_armor_tiers.py tools/generate_enlistment_rosters.py`.
> Any output means a file this plan edits or quotes changed since this plan was written: compare every
> "Current state" excerpt you rely on with the live file; a mismatch is a STOP condition. Then re-run the
> three stem greps of Step 2 and compare them with the hit table there; a new hit is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: dx
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: dotnet `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259`
  (game-backed desktop run), failing: none. Build: 11 warnings, 0 errors. Python suite:
  `Ran 8578 tests`, `FAILED (failures=7, errors=2, skipped=13)`. The nine failing names, all
  heraldry and kingdom-generator data checks that fail on the trunk and are not yours to fix:
  - `tools.tests.test_make_faction_emblems.ShippedDataTests.test_check_is_clean_after_apply` (ERROR)
  - `tools.tests.test_make_faction_emblems.ShippedDataTests.test_every_listed_faction_resolves_to_a_256_emblem` (ERROR)
  - `tools.tests.test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op`
  - `tools.tests.test_generator_regions_in_sync.GeneratorRegionsInSyncTests.test_every_owned_region_matches_a_fresh_run`
    (three subtests; counts once or more, compare by name)
  - `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_every_lindon_clan_flies_its_own_elven_banner`
  - `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_new_clans_names_tiers_homes_and_owners`
  - `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_the_lindon_kingdom_flies_an_elven_banner_of_its_own`

  `python -m ruff check tools --no-cache`: `All checks passed!`.
  `python -B tools/lint_docs.py --fail-on-drift --summary`: exit 0, `total_findings: 5`
  (`context_budget: 5`), `ai_dashes: 0`.
- **Issue**: filed by the orchestrator before execution.

## Why this matters

Three scripts under `tools/` are ones the tool catalog itself says never to run: `generate_xslt.py`
would overwrite the hand-corrected shipped `spcultures.xslt` through a hardcoded path,
`generate_batch2_wanderers.py` writes to a file the game does not load, and `validate_gondor_refs.py`
is the Gondor-only predecessor of `validate_all_troop_refs.py`. They stay in every grep, and one of
them sits in a gate's registry. The catalog's environment section quotes tool counts that are now
wrong (29, 25, 22 against 51 and 91 measured) and lists a hardcoded path in a file that has none. Two
tool tests re-parse the same files many times: the doc-graph ratchet tests build the docs graph three
times (about 3 s each), and `ShippedKeyCoverageTests` parses every language file 23 times per
language (24.95 s measured at the base, 6.58 s with the hoist). After this plan the dead scripts are
gone with every live mention repaired, the catalog names the commands that print its counts, and the
tools suite runs about 25 s faster with the same coverage. Three commits, each one droppable on its
own: the maintainer merges this branch later and may drop the deletion commit.

## Current state

All excerpts are from `7087fa633`, read in a worktree at that commit. Every file below has CRLF line
terminators (`file <path>` says "with CRLF line terminators"); keep them CRLF. Never `sed -i`.

### Part A: the three scripts and their mentions

- `tools/generate_xslt.py` (303 lines): `:295` opens the shipped `Main/_Module/ModuleData/spcultures.xslt`
  for writing through a hardcoded path under a user profile (`c:/Users/<user>/source/repos/TAOM/...`).
- `tools/generate_batch2_wanderers.py` (687 lines).
- `tools/validate_gondor_refs.py` (74 lines): checks `Item.sk_gd_*` / `Item.sk_dg_*` in
  `troops_gondor.xml` against the Armory's `LOTRLOME_items/gondor/*.xml`.
- Its successor, which stays: `tools/validate_all_troop_refs.py` checks every
  `troops/troops_*.xml` (globbed, so `troops_gondor.xml` is included) for refs matching
  `^(sk_[a-z]+_|sm_[a-z]+_|ar_[a-z]+_|harad|clo_urukscout_|urukscout_)` against the whole
  `LOTRLOME_items` tree. Per culture it prints
  `  gondor         troops=... armor_refs=... missing=0   PASS`, and on success ends
  `PASS: all armor refs resolve across all cultures.` Its docstring, `tools/validate_all_troop_refs.py:5`:

  ```
  This is the multi-culture generalization of `validate_gondor_refs.py`. It is the
  ```
- `tools/check_generator_item_refs.py:90-99`, the gate registry:

  ```python
  GENERATORS = (
      GeneratorSpec("tools/generate_gondor_troops.py", "run"),
      GeneratorSpec("tools/generate_rhun_troops.py", "run"),
      GeneratorSpec("tools/generate_char_creation_equipment.py", "tables", ("CULTURES",)),
      GeneratorSpec("tools/extract_wanderers.py", "tables", ("KINGDOM_EQUIPMENT",)),
      GeneratorSpec("tools/generate_batch2_wanderers.py", "tables", ("KINGDOM_EQUIPMENT",)),
      GeneratorSpec("tools/generate_starter_armor.py", "tables", ("CULTURES",), tuple_from=2),
  ```
- `tools/tests/test_check_generator_item_refs.py:131-135` asserts every registered path exists (it
  counts no entries). This is the RED for the deletion:

  ```python
      def test_every_registered_generator_exists(self):
          for spec in cg.GENERATORS:
              with self.subTest(spec.path):
                  self.assertTrue((cg.REPO_ROOT / spec.path).is_file(), spec.path)
                  self.assertIn(spec.mode, ("run", "tables"))
  ```
- No test, hook, skill, rule or C# file names any of the three stems (whole-repo `git grep -n -w`,
  Step 2's table is the complete hit list).
- **No gate checks a backticked path.** `tools/lint_docs.py` checks only markdown links, and nearly
  every mention below is a backticked path. A green `lint_docs.py` proves nothing for Part A; the
  stem greps in Step 5 are the proof.

### Part B: `tools/README.md` environment section

Lines 684 to 692 (the user path at 712 is shown here with `<user>`; the live line holds a real name):

```
684 **Game install — set `BANNERLORD_GAME_DIR`.** `README.md` lists it as a prerequisite and
685 `setup-dev-env.ps1` sets it. 29 tools honour it: 25 resolve it themselves, and `analyze_armor_balance.py`,
686 `apply_settlement_buildings.py`, `derive_armor_tiers.py` and `generate_enlistment_rosters.py` inherit it
687 by importing one that does. Every one falls back to `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`, so leaving it unset changes
688 nothing on a machine where that literal is correct. **The two decompile scripts opt out** — neither
...
692 **Resolve it through `tools/_gamedir.py`** — 22 of the 25 do. `game_dir(default)` treats a set-but-blank
...
708 The thirteen top-level tools that had no override of any kind now all read `BANNERLORD_GAME_DIR` (#404).
709 Six others were left alone deliberately: they take a path flag, which is override enough.
710
711 **Other hardcoded paths** (update if your environment differs):
712 - TAOM repo: `c:\Users\<user>\source\repos\TAOM\` — `generate_xslt.py`, `blender/rebuild_anim_from_json.py`
713 - LOTRAOM assets: `E:\LOTRAOMAssets\`
```

Facts measured at the base, from the worktree root:

- `git grep -l "BANNERLORD_GAME_DIR" -- 'tools/*.py' ':!tools/tests' | wc -l` prints `51`.
- `git grep -lE "(from|import) +_gamedir|from tools\._gamedir|import tools\._gamedir" -- 'tools/*.py' ':!tools/tests' | wc -l` prints `91`.
- The "inherit" sentence: `analyze_armor_balance.py:43` and `derive_armor_tiers.py:49` do
  `import rebalance_armor as ra`, and `apply_settlement_buildings.py:42` does
  `import dump_settlement_buildings as dsb`; both imported tools resolve the variable themselves.
  `generate_enlistment_rosters.py:87` now does `from _gamedir import ENV_VAR, game_modules`, so it
  resolves it itself and no longer belongs in the inherit list.
- Line 708's sentence was added by commit `a932bd677` on 2026-08-07
  (`git log -1 --format='%h %ad' --date=short -S"thirteen top-level tools" -- tools/README.md`).
- `git grep -n -i -E 'c:.{1,2}Users' -- 'tools/*.py' ':!tools/tests'` prints exactly 10 lines at the
  base. Two are code: `tools/generate_xslt.py:295` (deleted in Part A) and
  `tools/oneoff/lords-migration/replace_equipment_templates.py:12`
  (`LORDS_XML = r'c:\Users\<user>\source\repos\TAOM\...\lords.xml'`). The other eight sit inside module
  docstrings (usage lines and worked examples): `tools/blender/creature_anim_ops.py:5`,
  `tools/blender/measure_animalia_clips.py:5`, `tools/blender/reskin_animalia_to_horse.py:5`,
  `tools/blender/retarget_animalia_to_horse.py:5`, `tools/blender/skeleton_spec.py:12`,
  `tools/check_public_text.py:11`, `tools/oneoff/blender_prep_tripo_prop.py:41`,
  `tools/painter/painter_run.py:4`. `tools/blender/rebuild_anim_from_json.py` exists and contains no
  such path.
- `tools/package_quad_toolkit.py` has no catalog row. Its docstring: "Build the shareable quad-toolkit
  ZIP from an explicit source file list." `PACKAGE_FILES` lists `tools/__init__.py`,
  `tools/quad_engine/__init__.py`, `tools/quad_engine/cube_map.py`, `tools/blender/cube_map_remesh.py`,
  `tools/blender/quad_remesh_job.py`, `tools/examples/cube_map_active.py`,
  `tools/examples/remesh_active.py`, `tools/tests/__init__.py`, `tools/tests/test_cube_map_remesh.py`,
  `tools/tests/test_quad_remesh_job.py`, `tools/quad_toolkit/README.md`, `tools/quad_toolkit/LICENSE.txt`,
  `tools/quad_toolkit/NOTICE.txt`,
  the script itself, `docs/features/cube-map-remesh.md`, `docs/features/quad-remesher-job.md`,
  `docs/research/quad-remesher-engine.md`. `build_package` raises `FileNotFoundError` on a missing
  listed file and returns the ZIP's SHA-256; `main()` takes `--repo` (default `.`) and a required
  `--out`, and prints `Wrote <path>` and `SHA-256: <digest>`. No test file exists for it.
- The model row, `tools/README.md:357` (a two-column `| Script | Purpose |` table):

  ```
  | `package_hair_toolkit.py` | Builds the source-only Hair Toolkit ZIP for KEYforce and Kysaro: the independent engine, optional Hair Tool bridge, kitbash and registration tools, tests, grooming skill, docs, and separate MIT and GPL notices. Excludes the add-on, donor meshes, generated assets, and activation data. Requires an explicit `--out`. Tests: `tools/tests/test_package_hair_toolkit.py`. |
  ```

### Part C1: the doc-graph ratchet tests

`tools/check_doc_graph_ratchet.py:52-67`:

```python
def _orphans_and_components() -> tuple[list[Path], int]:
    nodes, edges, inbound, adj = dg.load_doc_graph(eligible=ratchet_eligible)
    deg = dg.degrees(nodes, edges, inbound)
    return [n for n in nodes if deg[n]["total"] == 0], len(dg.connected_components(nodes, adj))


def current_metrics() -> dict:
    """The two ratcheted counts, from the live docs tree without docs/reviews/."""
    orphans, components = _orphans_and_components()
    return {"orphans": len(orphans), "components": components}


def orphan_docs() -> list[str]:
    """The orphans current_metrics counts, as sorted repo-relative paths."""
    root = dg.ld.DOCS_DIR.parent
    return sorted(p.relative_to(root).as_posix() for p in _orphans_and_components()[0])
```

`tools/tests/test_doc_graph_ratchet.py:113-126`, the three live builds:

```python
    def test_orphan_docs_agrees_with_the_orphan_count(self):
        self.assertEqual(len(ratchet.orphan_docs()), ratchet.current_metrics()["orphans"])


class LiveTreeTests(unittest.TestCase):
    """The ratchet against the real docs/ tree: nothing else runs it, so without this test the
    orphan and component counts could grow while every suite stays green."""

    def test_the_live_tree_is_within_the_baseline(self):
        baseline = json.loads(ratchet.BASELINE.read_text(encoding="utf-8"))
        problems = ratchet.compare(ratchet.current_metrics(), baseline)
```

`FailureTextTests.run_main` (`:89-101`) patches `ratchet.current_metrics` and `ratchet.orphan_docs`
with `mock.patch.object`, not `_orphans_and_components`. Nothing in the repo patches `DOCS_DIR` of the
ratchet. **The tool stays unchanged**: production `main()` calls `current_metrics()` once and
`orphan_docs()` only on a failure, so a memo there saves little and would be wrong for a long-lived
process that edits docs between calls. The memo goes in the test module only.

Measured at the base: `python -B -m unittest tools.tests.test_doc_graph_ratchet` gives `Ran 15 tests`,
`OK`, in 5.1 s to 10.1 s across three runs (10.101 s, 5.107 s, 8.893 s). The absolute time varies with
the machine's load; only the 60 percent rule in "Done criteria" and "STOP conditions" is a gate.

### Part C2: `skip_ids_for` and its callers

`tools/translate_with_claude.py:343-363`:

```python
def skip_ids_for(source: Path, target: Path, owners: dict, lang_dir: Path) -> set:
    """Keys the TAOM pass must neither seed into nor translate for `target`: ...
    ...
    A sibling file that exists but does not parse raises: read as empty, its keys would look absent
    and --sync-ids would seed duplicates of them."""
    elsewhere = {k for k, owner in owners.items() if owner != source}
    for other in lang_dir.glob("std_taom_*.xml"):
        if other != target:
            try:
                tree = ET.parse(other)
            except ET.ParseError as exc:
                raise SystemExit(f"{other} does not parse ({exc}); fix it before a translator run")
            elsewhere.update(el.get("id", "") for el in tree.iter("string") if el.get("id", ""))
    return (elsewhere - set(_parse_string_xml(target, strip_keys=False))) | override_ids_to_skip(source, target)
```

Note the target is excluded from the parse loop, and `_parse_string_xml` (`:405-423`) returns `{}`
for a missing file AND for a file that does not parse. So **a malformed target never raises; only a
malformed sibling does.** The hoisted form must keep both halves.

Callers:

- `discover_entries` (`:253-267`), one language per run, reads only; this is where the hoist goes:

  ```python
      if module_filter in ("TAOM", "all"):
          taom_lang_dir = TAOM_LANG_DIR / lang
          sources = english_source_files("TAOM")
          owners = key_owners([src for _, src, _ in sources])
          for _, source_xml, tgt_template in sources:
              target_file = taom_lang_dir / tgt_template.format(locale=locale)
              elsewhere = skip_ids_for(source_xml, target_file, owners, taom_lang_dir)
              entries.extend(_diff_files(source_xml, target_file, skip_ids=elsewhere))
  ```
- The `--sync-ids` loop in `main()` (`:1016-1027`) calls `skip_ids_for(src, target, owners, TAOM_LANG_DIR / lang)`
  and then `sync_missing_ids(...)`, which **writes `target`** when not a dry run. The next iteration
  reads that file as a sibling. **Do not hoist there**: leave that call exactly as it is.
- Tests calling it: `tools/tests/test_translate_batching.py:330, 354, 408, 417, 437, 444, 463`.
  `:431-437` is the contract test `test_a_sibling_file_that_does_not_parse_stops_the_run`.
- `tools/tests/test_translate_batching.py:453-467`, the slow test:

  ```python
      def test_every_declared_key_is_examined_by_some_pass_in_every_language(self):
          sources = t.english_source_files("TAOM")
          owners = t.key_owners([s for _, s, _ in sources])
          self.assertGreater(len(owners), 1000, "the English sources were not found")
          problems = []
          for lang, (locale, _) in t.LANGUAGES.items():
              lang_dir = t.TAOM_LANG_DIR / lang
              hidden = set(owners)
              for _, src, template in sources:
                  hidden -= t.english_only_override_ids(src)   # English-only overrides carry no language row on purpose
                  skip = t.skip_ids_for(src, lang_dir / template.format(locale=locale), owners, lang_dir)
                  hidden -= set(t._parse_string_xml(src, strip_keys=True)) - skip
              if hidden:
                  problems.append("%s: %s" % (lang, ", ".join(sorted(hidden)[:5])))
          self.assertEqual(problems, [], "move each key's rows into its owner's language file (key_owners)")
  ```

  `english_only_override_ids(src)` and `_parse_string_xml(src, strip_keys=True)` do not depend on the
  language. 23 sources, 12 languages, 23 `std_taom_*.xml` files in each language folder (measured).
- Measured at the base: `python -B -m unittest tools.tests.test_translate_batching.ShippedKeyCoverageTests`
  gives `Ran 1 test in 24.953s`, `OK`. A scratch prototype of the hoist below gave, over all 276
  (language, source) pairs, `pairs 276 equal True old 24.82s new 6.58s`.

### Conventions that bind this plan

- No ADR binds this change: ADR-002 (entry points under 150 lines), ADR-007 (adapters for sealed
  TaleWorlds types) and ADR-008 (test coverage) govern C#, and this plan touches no C#.
- `.claude/rules/data-scripts.md` (paths `tools/**/*.py`): the XML I/O convention for scripts that
  edit XML. This plan adds no XML writer; read it anyway, since it loads for the files you touch.
- `.claude/rules/tests.md` (paths `tools/tests/**`): RED, GREEN, REFACTOR; a Python test method must
  be named `test_*` or it never runs.
- `tools/README.md` and AGENTS.md "Human prose": no em or en dash (U+2014, U+2013) on any line you
  write or edit. Several lines you edit already hold one (`tools/README.md:684, 692, 712`,
  `docs/features/gondor-armor-revamp.md:110, 117`); the replacement text below removes them.
- CLAUDE.md "Where new knowledge goes": a count the code can change names the command that prints it,
  or gives its date. That is why Part B replaces counts with commands.
- Nothing you write that may become public carries a local absolute path or a user name.

### Blast radius

No C# type changes, so `graphify_taom.py affected` does not apply. Python reach, checked with
whole-repo `git grep`: `skip_ids_for` is called only at the sites listed in C2; `sibling_string_ids`
is new; `_orphans_and_components` is called only by the ratchet itself. C# test comments name
`tools/translate_with_claude.py` (`TAOM.Tests/Infrastructure/Localization/AccentStrippedTranslationTests.cs:16`
cites `:298-308`, already stale at the base); no C# test reads or runs it.

## Commands you will need

Prefix every dotnet command with the TEMP and TMP your dispatch rules give. Python always `python -B`,
never `python3`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals (no C# changes) |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | `Ran 8581 tests`, the nine baseline names and no others failing |
| One Python module | `python -B -m unittest tools.tests.<module>[.<Class>]` | the named tests run and pass |
| Lint | `python -m ruff check tools --no-cache` | `All checks passed!` |
| Docs | `python -B tools/lint_docs.py --fail-on-drift --summary` | exit 0, `ai_dashes: 0`, no finding the baseline lacked |
| Data | `python tools/validate_moduledata.py` | 0 errors (not needed; no data changes) |

Never `./build.ps1`: it deploys into the game install. Never run `generate_xslt.py`,
`generate_batch2_wanderers.py` or `translate_with_claude.py` itself (it writes language files);
import its functions only.

## Scope

**In scope** (the only files you modify):

- Delete: `tools/generate_xslt.py`, `tools/generate_batch2_wanderers.py`, `tools/validate_gondor_refs.py`
- `tools/check_generator_item_refs.py`
- `tools/validate_all_troop_refs.py` (docstring line 5 only)
- `tools/README.md`
- `docs/modding/file-catalogue.md`, `docs/modding/skill-sets.md`, `docs/modding/validation-and-testing.md`,
  `docs/modding/wanderers-and-named-companions.md`, `docs/reference/provenance-register.md`,
  `docs/features/gondor-armor-revamp.md`
- `tools/translate_with_claude.py`, `tools/tests/test_translate_batching.py`
- `tools/tests/test_doc_graph_ratchet.py`

**Out of scope** (do NOT touch, even though they look related):

- `tools/audit_item_refs.py` and `tools/validate_all_troop_refs.py` as files to delete. About 25 live
  procedure lines in `docs/modding` and `docs/ai-includes` tell authors to run them, and their
  equivalence with `validate_moduledata.py` is unverified. That is the maintainer's decision.
- `tools/check_doc_graph_ratchet.py`: the memo lives in the test module only.
- The `--sync-ids` loop in `translate_with_claude.py` `main()` (`:1016-1027`).
- `tools/tests/test_ingame_verdict.py`: a substring prefilter there was measured slower than the
  shipped lexer. Do not add one.
- History, left as it is: `docs/migration/**` (templates included), `docs/changelog-archive/**`,
  `docs/archive/**`, `docs/reviews/**`, `plans/**`, `CHANGELOG.md`.
- The dated counts "seven generators" in `tools/README.md:41`, `tools/validate_moduledata.py:100` and
  `docs/reference/moduledata-validator-codes.md:44`, and "both wanderer generators" in
  `tools/README.md:41` and `docs/features/moduledata-validation.md:614`: they describe 2026-09-13.
- `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`,
  `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: nothing here needs them.
- The gates themselves: never turn a test green by deleting, skipping or loosening it.

## Git workflow

- Commit on the branch you were given; never push or open a PR. Three commits, one per part, in the
  order A, B, C.
- Subject `<type>(tools): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value="...">` in `Main/_Module/SubModule.xml` when you commit (`v2.0.38` at planning).
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Write it to a file with the Write tool and run `git commit -F "<file>"`. Never edit
  `CHANGELOG.md`. No AI attribution trailer. Never `--no-verify`.
- Stage explicit paths only (never `-A`, `-u`, `.` or `commit -a`). Use `git rm <path>` with the three
  explicit paths for the deletions.
- `<DATE>` below means the date you commit, as `YYYY-MM-DD`.

## Steps

### Step 1: record the base

From your worktree, before any edit, run and record in your report:

1. `python -B -m unittest discover -s tools/tests -t .` (use `timeout 900`; about 9 to 10 minutes,
   552 s on the reviewer's base run).
   Record the `Ran N tests` line, the `FAILED (...)` line, and every failing or erroring test name.
2. `python -m ruff check tools --no-cache`.
3. `python -B tools/lint_docs.py --fail-on-drift --summary`.
4. The dotnet test command (use `timeout 900`), with its `Passed!` or `Failed!` line.
5. The before timings, each run twice:
   `python -B -m unittest tools.tests.test_doc_graph_ratchet` and
   `python -B -m unittest tools.tests.test_translate_batching.ShippedKeyCoverageTests`.

**Verify**: totals match "Baseline at that commit" (the dotnet run may skip RequiresGame tests if no
game is installed: record what you get), and the failing Python names are exactly the nine listed.
A different failing set is a STOP condition. The timings are informational (5 to 10 s and about 25 s
were measured at the base); they are the "before" figures for the 60 percent rule, not a check here.

### Step 2: Part A, confirm the hit list

Run, from the worktree root:

```
git grep -n -w generate_xslt
git grep -n -w generate_batch2_wanderers
git grep -n -w validate_gondor_refs
```

Compare with this table (made at the base). Every hit is listed with its disposition. A hit missing
from the table is a STOP condition.

| Hit | Disposition |
|---|---|
| `tools/README.md:173` (`generate_xslt.py` row) | edit: delete the row (Step 4) |
| `tools/README.md:712` (path list) | edit in Part B (Step 7) |
| `docs/modding/file-catalogue.md:50` | edit (Step 4) |
| `docs/reference/provenance-register.md:301` | edit: keep the name, add the removal (Step 4) |
| `docs/reviews/rca-tokens-of-esteem-sides-2026-10-20.md:150` | history (a review record), leave |
| `docs/changelog-archive/CHANGELOG-2026-H1.md:10473`, `CHANGELOG-2026-H2-handwritten.md:15014` | history, leave |
| `tools/check_generator_item_refs.py:95` (`GENERATORS` entry) | edit: delete the line (Step 3) |
| `tools/README.md:174` (`generate_batch2_wanderers.py` row) | edit: delete the row (Step 4) |
| `docs/modding/file-catalogue.md:65, :66, :67, :117` | edit (Step 4) |
| `docs/modding/skill-sets.md:23` | edit (Step 4) |
| `docs/modding/validation-and-testing.md:206` (and its heading `:199`, comment `:204`) | edit (Step 4) |
| `docs/modding/wanderers-and-named-companions.md:259` | edit (Step 4) |
| `docs/migration/v1.4.x-equipment-overhaul.md:166`, `docs/migration/v1.4.x-taom-impact.md:210` | history, leave |
| `docs/archive/SESSION-S5a-S5b-PROMPT.md:87` | history, leave |
| `docs/changelog-archive/CHANGELOG-2026-H1.md:5706, :6515, :10450`, `CHANGELOG-2026-H2-handwritten.md:19511` | history, leave |
| `tools/README.md:53` (`validate_gondor_refs.py` row) | edit: delete the row (Step 4) |
| `tools/validate_all_troop_refs.py:5` (docstring) | edit (Step 4) |
| `docs/modding/file-catalogue.md:93` ("checked by") | edit (Step 4) |
| `docs/features/gondor-armor-revamp.md:30, :51, :98, :110, :117, :131` | edit (Step 4) |
| `docs/features/gondor-armor-revamp.md:167, :170, :176, :186` (dated 2026-07 change-log bullets: "`validate_gondor_refs` + `validate_moduledata` PASS") | history, leave |
| `docs/features/multi-culture-armor-revamp.md:71` ("replaces Gondor-only `validate_gondor_refs.py`") | history (true as stated), leave |
| `docs/migration/templates/equipment-rosters.md:659, :670`, `docs/migration/templates/troops-and-parties.md:595, :657, :724`, `docs/migration/v1.4.x-equipment-overhaul.md:269`, `docs/migration/v1.4.x-taom-impact.md:216` | history, leave |
| `docs/reviews/lessons/build-tooling-workflow.md:1025` | history (a lesson), leave |
| `docs/archive/SESSION-S5a-S5b-PROMPT.md:175, :182, :196` | history, leave |
| `docs/changelog-archive/**` (15 `validate_gondor_refs` lines at the base) | history, leave |
| `plans/**`: at the base `plans/_audit/2026-06-12-harvest.json`, `plans/_audit/2026-06-12-harvest.md`, `plans/_audit/2026-09-23-opus/triage-B.md`; also this plan and its review files if your branch carries them | history, leave |

**Verify**: every hit of the three greps appears in the table above.

### Step 3: Part A, delete the scripts (RED), then fix the registry (GREEN)

1. `git rm tools/generate_xslt.py tools/generate_batch2_wanderers.py tools/validate_gondor_refs.py`
2. RED: `python -B -m unittest tools.tests.test_check_generator_item_refs.RegistryContractTests`.
   It must FAIL in `test_every_registered_generator_exists`, subtest `tools/generate_batch2_wanderers.py`
   (the assertion message names that path), and `test_tables_mode_generators_expose_their_tables`
   must fail or error for the same path. Quote the failure in your report: this is the deletion's RED.
3. In `tools/check_generator_item_refs.py`, delete line 95 only:
   `    GeneratorSpec("tools/generate_batch2_wanderers.py", "tables", ("KINGDOM_EQUIPMENT",)),`
4. GREEN: `python -B -m unittest tools.tests.test_check_generator_item_refs` gives `OK`
   (18 tests at the base).

**Verify**: the RED failure quoted, then `OK`.

### Step 4: Part A, repair the live mentions

Make each edit exactly. Re-read each line first; a mismatch with the "old" text is a STOP condition.

1. `tools/README.md`: delete the whole row at line 53 (starts ``| `validate_gondor_refs.py` |``), the
   row at 173 (``| `generate_xslt.py` |``) and the row at 174 (``| `generate_batch2_wanderers.py` |``).
   Line numbers shift after the first deletion: find the rows by their first cell.
2. `tools/validate_all_troop_refs.py:5`: replace
   ``This is the multi-culture generalization of `validate_gondor_refs.py`. It is the``
   with
   `This is the multi-culture generalization of the Gondor-only validator it replaced. It is the`
   (the next line, `gate against the underwear bug for the full TAOM faction lineup.`, stays).
3. `docs/modding/file-catalogue.md`:
   - `:50`: replace ``| `tools/generate_xslt.py`; rewritten by`` with
     ``| first generated by `tools/generate_xslt.py` (deleted <DATE>); rewritten by``.
   - `:65`, `:66`, `:67`: remove ``, `tools/generate_batch2_wanderers.py` `` (the comma, the space and the
     backticked path) from each row, leaving the other tools in order.
   - `:93`: replace ``checked by `tools/validate_gondor_refs.py` `` with ``checked by `tools/validate_all_troop_refs.py` ``.
   - `:117`: replace ``(`tools/extract_wanderers.py`, `tools/generate_batch2_wanderers.py`, `tools/insert_new_factions.py`)``
     with ``(`tools/extract_wanderers.py`, `tools/insert_new_factions.py`)``.
4. `docs/modding/skill-sets.md:23`: replace
   ``two tools name it, `tools/extract_wanderers.py` and `tools/generate_batch2_wanderers.py`, and the second one appends rather than regenerating (its docstring, line 3; the [tools README](../../tools/README.md) marks it stale, so do not run it).``
   with
   ``one tool names it, `tools/extract_wanderers.py` (a second, `generate_batch2_wanderers.py`, appended rather than regenerating and was deleted on <DATE>).``
5. `docs/modding/validation-and-testing.md`:
   - `:199`: `## Seven scripts with no dry run` becomes `## Six scripts with no dry run`
     (no link targets this heading: `git grep -n -i "seven-scripts"` returns nothing at the base).
   - `:204`: in the comment ending `seven still return 0 on 2026-10-27 -->`, insert before ` -->`:
     `; generate_batch2_wanderers.py deleted <DATE>`.
   - `:206`: ``tools/generate_batch2_wanderers.py` · `tools/extract_wanderers.py` ·`` becomes
     ``tools/extract_wanderers.py` ·``. Count the remaining backticked paths in the list (lines 206 to
     209: one on 206, two each on 207 and 208, one on 209): six.
6. `docs/modding/wanderers-and-named-companions.md:259`: replace the whole bullet with the text below.
   First re-derive the two citations it carries: `grep -n "taom_wanderer_equipment" Main/_Module/SubModule.xml`
   (the `EquipmentRosters` row was line 1124 at the base, not the 1106 the old bullet cites) and
   `grep -n -E "TAOM_BASE|taom_wanderer_equipment" tools/extract_wanderers.py` (it prints lines 7, 18,
   461, 466, 470 and 475 at the base: cite 18, the `TAOM_BASE =` line, and the three lines of the
   `write_equipment(` call that holds the `taom_wanderer_equipment.xml` hit, 469 to 471 at the base),
   and confirm `grep -c -E "argparse|sys.argv" tools/extract_wanderers.py` prints `0`. Put the line
   numbers you find in place of 1124, 18 and 469-471:

   ```
   - **The wanderer generator overwrites four files with no dry run and no backup.** `tools/extract_wanderers.py` takes no arguments and writes in place. Worse, it writes `Main/_Module/ModuleData/taom_wanderer_equipment.xml`, at the ModuleData root, while the file the game actually loads is `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml` (`SubModule.xml:1124`). Running it leaves a stray root-level file the engine never reads and the real roster untouched. `tools/extract_wanderers.py:18, 469-471`. A second generator with the same fault, `generate_batch2_wanderers.py`, was deleted on <DATE>.
   ```
   And at `:287`, `for the wanderer generators and every other data tool.` becomes
   `for the wanderer generator and every other data tool.`
7. `docs/reference/provenance-register.md:301`: replace
   ``tools/extract_wanderers.py`, `tools/generate_xslt.py`, and `tools/oneoff/lords-migration/*.ps1` ``
   with
   ``tools/extract_wanderers.py`, `tools/generate_xslt.py` (deleted on <DATE>; git history keeps it), and `tools/oneoff/lords-migration/*.ps1` ``.
   Do not drop the name: this is provenance evidence.
8. `docs/features/gondor-armor-revamp.md`:
   - `:30` (history of the revamp): after ``**Phase C: Cross-reference validation.** `tools/validate_gondor_refs.py` ``
     insert ` (deleted <DATE>; its multi-culture successor is `tools/validate_all_troop_refs.py`)`.
   - `:51` (inside a code block diagram): replace
     `                            validate_gondor_refs.py  -->  PASS / FAIL gate` with
     `                         validate_all_troop_refs.py  -->  PASS / FAIL gate` (25 leading spaces
     instead of 28: the new name is three characters longer, so `-->` stays in column 54).
   - `:98`: ``| `tools/validate_gondor_refs.py` | Cross-reference gate for the underwear bug |`` becomes
     ``| `tools/validate_gondor_refs.py` | Cross-reference gate for the underwear bug; deleted <DATE>, replaced by `tools/validate_all_troop_refs.py` |``.
   - `:110`: replace the bullet with
     ``- Cross-reference validation (`python tools/validate_all_troop_refs.py`), equivalent to a runtime smoke test for the underwear bug. Expected: the `gondor` row reads `missing=0` and `PASS`, and the run ends `PASS: all armor refs resolve across all cultures.` ``
   - `:117`: the step that starts `4. Run` and ends `must show 0 missing references.` (it holds an em
     dash) becomes
     ``4. Run `python tools/validate_all_troop_refs.py`: the `gondor` row must show `missing=0`.``
   - `:131`: ``4. Run `tools/validate_gondor_refs.py` and `./build.ps1 -RunTests`.`` becomes
     ``4. Run `python tools/validate_all_troop_refs.py` and `./build.ps1 -RunTests`.``

**Verify**: `git diff --stat` lists only the Part A files; `file <path>` on each edited file still says
"with CRLF line terminators" (not "CRLF, LF"); the dash check of Step 5 passes.

### Step 5: Part A, prove the mentions are history only, then commit

1. Run the stem grep outside the history folders, from the worktree root:

   ```
   git grep -n -w -E "generate_xslt|generate_batch2_wanderers|validate_gondor_refs" -- ':!docs/changelog-archive' ':!docs/archive' ':!docs/migration' ':!docs/reviews' ':!plans' | cut -d: -f1,2
   ```
   It must print exactly these 13 lines (27 at the base), in this order:

   ```
   docs/features/gondor-armor-revamp.md:30
   docs/features/gondor-armor-revamp.md:98
   docs/features/gondor-armor-revamp.md:167
   docs/features/gondor-armor-revamp.md:170
   docs/features/gondor-armor-revamp.md:176
   docs/features/gondor-armor-revamp.md:186
   docs/features/multi-culture-armor-revamp.md:71
   docs/modding/file-catalogue.md:50
   docs/modding/skill-sets.md:23
   docs/modding/validation-and-testing.md:204
   docs/modding/wanderers-and-named-companions.md:259
   docs/reference/provenance-register.md:301
   tools/README.md:709
   ```
   `gondor-armor-revamp.md:30, :98`, `file-catalogue.md:50`, `skill-sets.md:23`,
   `validation-and-testing.md:204`, `wanderers-and-named-companions.md:259` and
   `provenance-register.md:301` are the lines you wrote that say "deleted". `gondor-armor-revamp.md:167,
   :170, :176, :186` and `multi-culture-armor-revamp.md:71` are history left as it is.
   `tools/README.md:709` is the base's line 712 (three rows above it were deleted), which Part B
   replaces. Paste the output into your report. Any other line is a STOP condition.
2. Dash check. Write this file with the Write tool as `<scratch>\dashcheck.py` and run
   `git diff HEAD -U0 | python -B "<scratch>\dashcheck.py"`; it must print nothing and exit 0. It
   decodes the diff as UTF-8 itself, so it does not depend on the shell's encoding settings:

   ```python
   import sys
   text = sys.stdin.buffer.read().decode("utf-8")
   bad = [l for l in text.splitlines()
          if l.startswith("+") and not l.startswith("+++") and ("\u2013" in l or "\u2014" in l)]
   for l in bad:
       print(l.encode("ascii", "backslashreplace").decode("ascii"))
   sys.exit(1 if bad else 0)
   ```
   Checked at planning: a sample diff adding a U+2014 line printed it and exited 1, a clean sample
   printed nothing and exited 0, both with `PYTHONIOENCODING` unset.
3. `python -m ruff check tools --no-cache`: `All checks passed!`.
4. `python -B tools/lint_docs.py --fail-on-drift --summary`: exit 0, `ai_dashes: 0`.
5. `python -B -m unittest tools.tests.test_check_generator_item_refs tools.tests.test_python_lint_gate`: OK.
6. Commit with explicit paths (the three deleted files are already staged by `git rm`). Subject, for
   example: `chore(tools): v2.0.38 - Delete three retired data scripts`. The body names the three
   files and why each went: `generate_xslt.py` (the catalog said RETIRED, do not run; it would
   overwrite the hand-corrected `spcultures.xslt` through a hardcoded path), `generate_batch2_wanderers.py`
   (the catalog said stale, do not run; it writes a file the game does not load), and
   `validate_gondor_refs.py` (Gondor-only predecessor of `validate_all_troop_refs.py`, which covers
   `troops_gondor.xml`). Say that the registry entry in `check_generator_item_refs.py` and the doc
   mentions were repointed or marked deleted, that `audit_item_refs.py` and `validate_all_troop_refs.py`
   were kept on purpose, and that the commit can be dropped on its own.

**Verify**: `git show --stat HEAD` lists the three deletions plus only the Part A files.

### Step 6: Part B, the environment counts

Line numbers in Steps 6 and 7 are the base's. Part A deleted rows 53, 173 and 174 of
`tools/README.md`, so in your tree the base's line 357 is 356, and 684, 692, 708 and 712 are 681, 689,
705 and 709. Find each line by its text, never by number alone.

In `tools/README.md`, replace lines 684 to 687 (from `**Game install` through
`...so leaving it unset changes`) with:

```
**Game install: set `BANNERLORD_GAME_DIR`.** `README.md` lists it as a prerequisite and
`setup-dev-env.ps1` sets it. `git grep -l BANNERLORD_GAME_DIR -- 'tools/*.py' ':!tools/tests'` lists
every tool that names it (51 files on 2026-11-12, some only in a help line), and
`analyze_armor_balance.py`, `apply_settlement_buildings.py` and `derive_armor_tiers.py` inherit it by
importing one that does. Every one falls back to `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`, so leaving it unset changes
```

Line 688 onward (`nothing on a machine where...`) stays as it is.

Replace the opening of line 692,
``**Resolve it through `tools/_gamedir.py`** — 22 of the 25 do. `game_dir(default)` treats a set-but-blank``
with these two lines:

```
**Resolve it through `tools/_gamedir.py`**: `git grep -lE "(from|import) +_gamedir|from tools\._gamedir|import tools\._gamedir" -- 'tools/*.py' ':!tools/tests'`
lists the tools that do (91 files on 2026-11-12). `game_dir(default)` treats a set-but-blank
```

Before writing, re-run both commands from the worktree root with `| wc -l` after Part A. Part A
deleted `validate_gondor_refs.py`, which both mentions the variable and imports `_gamedir`, so expect
`50` and `90`: write the numbers you get, dated `<DATE>`, in place of 51, 91 and 2026-11-12. Also
re-check the three inherit claims (`grep -n "^import rebalance_armor\|^import dump_settlement_buildings" tools/analyze_armor_balance.py tools/apply_settlement_buildings.py tools/derive_armor_tiers.py`
gives three hits); a missing one is a STOP condition.

Line 708: append the date, so the sentence ends `(#404, 2026-08-07).` Line 709 stays.

**Verify**: `grep -n -E "29 tools|25 resolve|22 of the 25" tools/README.md` returns nothing.

### Step 7: Part B, the hardcoded-path list and the quad packager row

1. Replace line 712 (the path list item that names `generate_xslt.py` and
   `blender/rebuild_anim_from_json.py`) with:

   ```
   - TAOM repo, an old checkout path under a user profile (`c:\Users\<user>\source\repos\TAOM\`): `oneoff/lords-migration/replace_equipment_templates.py`
   ```
   First run `git grep -n -i -E 'c:.{1,2}Users' -- 'tools/*.py' ':!tools/tests' | cut -d: -f1,2`. After
   Part A it must print exactly these nine lines (the base's ten minus `tools/generate_xslt.py:295`):

   ```
   tools/blender/creature_anim_ops.py:5
   tools/blender/measure_animalia_clips.py:5
   tools/blender/reskin_animalia_to_horse.py:5
   tools/blender/retarget_animalia_to_horse.py:5
   tools/blender/skeleton_spec.py:12
   tools/check_public_text.py:11
   tools/oneoff/blender_prep_tripo_prop.py:41
   tools/oneoff/lords-migration/replace_equipment_templates.py:12
   tools/painter/painter_run.py:4
   ```
   `replace_equipment_templates.py:12` is the only code line; the other eight are inside module
   docstrings (usage lines and worked examples), which the path list does not cover. A different
   result is a STOP condition. Write `<user>` literally; do not write the real user name.
2. Insert a new row directly after the `package_hair_toolkit.py` row (line 357 at the base; find it by
   its first cell):

   ```
   | `package_quad_toolkit.py` | Builds the shareable quad-toolkit ZIP from the explicit `PACKAGE_FILES` list: the cube-map engine (`quad_engine/`), the cube-map and Exoside bridge scripts (`blender/cube_map_remesh.py`, `blender/quad_remesh_job.py`), two examples, their tests, the `quad_toolkit/` README, LICENSE and NOTICE, and the cube-map, remesher-job and remesher-engine docs. A listed file that is missing stops the build (`FileNotFoundError`); prints the ZIP's SHA-256. Requires an explicit `--out`; `--repo` defaults to `.`. No test file. |
   ```
   Re-read `tools/package_quad_toolkit.py` first and correct the row if the file list or the flags
   differ from "Current state".
3. Run the dash check (Step 5.2), `python -B tools/lint_docs.py --fail-on-drift --summary` (exit 0,
   `ai_dashes: 0`) and `file tools/README.md` (CRLF only).
4. Commit `tools/README.md` alone. Subject, for example:
   `docs(tools): v2.0.38 - Name the commands behind the game-dir counts`. The body says the
   environment section's three counts had gone stale (29, 25, 22 against the numbers you measured), so
   each is now the command that prints it with a dated figure; the inherit list dropped
   `generate_enlistment_rosters.py`, which imports `_gamedir` itself; the hardcoded-path list dropped
   the deleted `generate_xslt.py` and `rebuild_anim_from_json.py` (which has no such path) and now names
   `replace_equipment_templates.py`; and `package_quad_toolkit.py` got its catalog row.

**Verify**: `git show --stat HEAD` lists `tools/README.md` only.

### Step 8: Part C1, share one docs-graph build in the ratchet tests

This is a test-only refactor with no behaviour change, so its RED is the mutation check below, not a
new failing test.

1. In `tools/tests/test_doc_graph_ratchet.py`, add `from unittest import mock` on its own line
   directly after `from pathlib import Path` (line 16), and after
   `import check_doc_graph_ratchet as ratchet  # noqa: E402` add the block below. `_BUILD` captures the
   real builder at import, before any test patches it: inside `with _shared_graph():` the name
   `ratchet._orphans_and_components` IS the mock, so a memo that called it there would re-enter itself
   (`RecursionError`; the round-1 review ran that form and got `FAILED (errors=2)`).

   ```python
   _BUILD = ratchet._orphans_and_components  # the real builder, captured before any test patches it
   _LIVE_GRAPH = None


   def _live_graph():
       """One build of the live docs graph for this module; each build reads the whole docs tree."""
       global _LIVE_GRAPH
       if _LIVE_GRAPH is None:
           _LIVE_GRAPH = _BUILD()
       return _LIVE_GRAPH


   def _shared_graph():
       """Serve the tool's graph builder from the one build above, for the tests that read the live tree."""
       return mock.patch.object(ratchet, "_orphans_and_components", side_effect=_live_graph)
   ```
2. Wrap the body of `test_orphan_docs_agrees_with_the_orphan_count` and of
   `test_the_live_tree_is_within_the_baseline` in `with _shared_graph():`. Change nothing else:
   `FailureTextTests.run_main` keeps its own patches.
3. `python -B -m unittest tools.tests.test_doc_graph_ratchet`, twice: `Ran 15 tests`, `OK`, no
   `RecursionError`, and the time at most 60 percent of the Step 1 figure. A scratch copy with exactly
   these edits gave `Ran 15 tests in 1.680s` and `in 1.659s`, `OK`, against `8.893s` for the base module
   on the same machine (informational), and ruff with the repo's `ruff.toml` printed
   `All checks passed!` on it.
4. Mutation check (proves the live test still reads the graph and can fail). Write this as
   `<scratch>\ratchet_mutation.py` and run it from the worktree root with `python -B`:

   ```python
   import sys
   import unittest
   sys.path.insert(0, ".")
   import tools.tests.test_doc_graph_ratchet as m
   m._LIVE_GRAPH = ([], 0)
   suite = unittest.defaultTestLoader.loadTestsFromName("tools.tests.test_doc_graph_ratchet.LiveTreeTests")
   result = unittest.TextTestRunner(verbosity=2).run(suite)
   print("failures", len(result.failures))
   ```
   Expected: `FAILED (failures=1)`, one failure whose message holds `no docs were read`, and
   `failures 1` (the scratch copy printed exactly that). Quote it. This check presets `_LIVE_GRAPH`
   and never builds, so it cannot see a memo that recurses: step 3's `OK` is the proof that the real
   build runs.

**Verify**: steps 3 and 4 as stated. Do not commit yet (Part C is one commit).

### Step 9: Part C2, the failing tests for the hoist (RED)

In `tools/tests/test_translate_batching.py`, add a class after `EnglishOnlyOverrideTests` (before
`ShippedKeyCoverageTests`), modelled on that class's `tempfile` fixtures:

```python
class SiblingIdsHoistTests(unittest.TestCase):
    """discover_entries parses a language's files once (sibling_string_ids) and hands them to skip_ids_for.
    The hoisted form must give the per-call form's set for every target, raise on a sibling that does not
    parse, and, like the per-call form, not raise when only the target itself does not parse."""

    def _lang(self, d):
        d = Path(d)
        module_src, xslt_src = d / "taom_module_strings.xml", d / "taom_xslt_strings.xml"
        module_src.write_text('<strings><string id="a" text="{=k_shared}Woses" />'
                              '<string id="b" text="{=0CsbbwtN}Call {KINGDOM} to War" />'
                              '<string id="c" text="{=k_mod}Mod" /></strings>', encoding="utf-8")
        xslt_src.write_text('<strings><string id="a" text="{=k_shared}Woses" />'
                            '<string id="d" text="{=k_xslt}Xslt" /></strings>', encoding="utf-8")
        lang = d / "DE"
        lang.mkdir()
        module_tgt = lang / "std_taom_module_strings_deu-DE.xml"
        xslt_tgt = lang / "std_taom_xslt_strings_deu-DE.xml"
        module_tgt.write_text('<base><strings><string id="k_shared" text="Woses" /></strings></base>', encoding="utf-8")
        xslt_tgt.write_text('<base><strings><string id="k_xslt" text="X" /></strings></base>', encoding="utf-8")
        (lang / "std_taom_keybind_strings_deu-DE.xml").write_text(
            '<base><strings><string id="k_mod" text="M" /></strings></base>', encoding="utf-8")
        owners = t.key_owners([module_src, xslt_src])
        return [(module_src, module_tgt), (xslt_src, xslt_tgt)], owners, lang

    def test_hoisted_siblings_give_the_per_call_skip_set_for_every_target(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            pairs, owners, lang = self._lang(d)
            siblings = t.sibling_string_ids(lang)
            for src, tgt in pairs:
                with self.subTest(tgt.name):
                    self.assertEqual(t.skip_ids_for(src, tgt, owners, lang, siblings=siblings),
                                     t.skip_ids_for(src, tgt, owners, lang))

    def test_a_hoisted_sibling_that_does_not_parse_stops_the_run(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            pairs, owners, lang = self._lang(d)
            (lang / "std_taom_keybind_strings_deu-DE.xml").write_text("<base><strings>", encoding="utf-8")
            siblings = t.sibling_string_ids(lang)  # collecting defers the error to the first use
            src, tgt = pairs[0]
            with self.assertRaises(SystemExit):
                t.skip_ids_for(src, tgt, owners, lang, siblings=siblings)

    def test_a_target_that_does_not_parse_is_not_a_sibling_error(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            pairs, owners, lang = self._lang(d)
            src, tgt = pairs[0]
            tgt.write_text("<base><strings>", encoding="utf-8")
            self.assertEqual(t.skip_ids_for(src, tgt, owners, lang, siblings=t.sibling_string_ids(lang)),
                             t.skip_ids_for(src, tgt, owners, lang))
```

The fixture's `0CsbbwtN` key exercises `override_ids_to_skip` (`OVERRIDE_SOURCE` is
`"taom_module_strings.xml"`, `tools/_loc_sources.py:45`), `k_shared` the "row the target holds" rule,
and `k_mod` a row held by a third sibling. Run against the base's per-call form at planning, this
fixture gave `['0CsbbwtN', 'k_mod', 'k_xslt']` for the module target, `['0CsbbwtN', 'k_mod', 'k_shared']`
for the xslt target, and `['0CsbbwtN', 'k_mod', 'k_xslt']` with a malformed module target (no raise).
If the per-call form returns an empty set for either target in your run, the first test proves
little: STOP and report rather than weaken it.

RED: `python -B -m unittest tools.tests.test_translate_batching.SiblingIdsHoistTests`. All three must
ERROR with `AttributeError` naming `sibling_string_ids`. Quote it.

**Verify**: three errors, all `AttributeError: module 'translate_with_claude' has no attribute 'sibling_string_ids'`.

### Step 10: Part C2, implement the hoist (GREEN)

In `tools/translate_with_claude.py`:

1. Add, directly above `def skip_ids_for`:

   ```python
   def sibling_string_ids(lang_dir: Path) -> dict:
       """Each std_taom_*.xml of one language folder, parsed once: its set of string ids, or the
       ParseError it raised. skip_ids_for raises that error only when the file is used as a sibling, so
       a target that does not parse stays as harmless as in the per-call form. For a pass that writes no
       language file between calls (discover_entries); --sync-ids writes and must not reuse it."""
       found = {}
       for other in lang_dir.glob("std_taom_*.xml"):
           try:
               tree = ET.parse(other)
           except ET.ParseError as exc:
               found[other] = exc
               continue
           found[other] = {el.get("id", "") for el in tree.iter("string") if el.get("id", "")}
       return found
   ```
   Keep the glob order (do not sort): it decides which file a two-error run names first, as today.
2. Change `skip_ids_for` to take `siblings=None` and use it:

   ```python
   def skip_ids_for(source: Path, target: Path, owners: dict, lang_dir: Path, siblings=None) -> set:
       """...(keep the existing docstring, and add this paragraph at its end)...

       siblings: sibling_string_ids(lang_dir), computed once by a caller that writes nothing between
       calls; None parses the folder now, as --sync-ids needs."""
       if siblings is None:
           siblings = sibling_string_ids(lang_dir)
       elsewhere = {k for k, owner in owners.items() if owner != source}
       for other, ids in siblings.items():
           if other != target:
               if isinstance(ids, ET.ParseError):
                   raise SystemExit(f"{other} does not parse ({ids}); fix it before a translator run")
               elsewhere.update(ids)
       return (elsewhere - set(_parse_string_xml(target, strip_keys=False))) | override_ids_to_skip(source, target)
   ```
   The `SystemExit` message text is unchanged.
3. In `discover_entries`, compute `siblings = sibling_string_ids(taom_lang_dir)` once, after the
   `owners = ...` line, and pass `siblings=siblings` to the `skip_ids_for` call in the loop.
4. Leave the `--sync-ids` call in `main()` unchanged.

GREEN: `python -B -m unittest tools.tests.test_translate_batching` gives `OK` (all classes, the old
contract test `test_a_sibling_file_that_does_not_parse_stops_the_run` included).

**Verify**: `OK`, and `git grep -n "skip_ids_for(" -- tools/translate_with_claude.py` shows the
`--sync-ids` call without a `siblings=` argument.

### Step 11: Part C2, hoist in the slow test, and prove equality over the shipped tree

1. Rewrite the body of `test_every_declared_key_is_examined_by_some_pass_in_every_language` so the
   language-independent work runs once and each language folder is parsed once:

   ```python
       def test_every_declared_key_is_examined_by_some_pass_in_every_language(self):
           sources = t.english_source_files("TAOM")
           owners = t.key_owners([s for _, s, _ in sources])
           self.assertGreater(len(owners), 1000, "the English sources were not found")
           # English-only overrides carry no language row on purpose; neither set depends on the language.
           per_source = [(src, template, t.english_only_override_ids(src), set(t._parse_string_xml(src, strip_keys=True)))
                         for _, src, template in sources]
           problems = []
           for lang, (locale, _) in t.LANGUAGES.items():
               lang_dir = t.TAOM_LANG_DIR / lang
               siblings = t.sibling_string_ids(lang_dir)
               hidden = set(owners)
               for src, template, overrides, declared in per_source:
                   hidden -= overrides
                   skip = t.skip_ids_for(src, lang_dir / template.format(locale=locale), owners, lang_dir,
                                         siblings=siblings)
                   hidden -= declared - skip
               if hidden:
                   problems.append("%s: %s" % (lang, ", ".join(sorted(hidden)[:5])))
           self.assertEqual(problems, [], "move each key's rows into its owner's language file (key_owners)")
   ```
   The set arithmetic is the same as the old loop: the overrides are subtracted once per source, and
   subtraction is idempotent.
2. Can-fail check of the rewritten test: a scratch script replaces `t.skip_ids_for` (in memory
   only) with one that skips every key, so no pass examines any key and the test must fail. Write
   this as `<scratch>\coverage_can_fail.py` and run it from the worktree root with `python -B`:

   ```python
   import sys
   import unittest
   sys.path.insert(0, ".")
   import tools.tests.test_translate_batching as m
   t = m.t
   owners = t.key_owners([s for _, s, _ in t.english_source_files("TAOM")])
   t.skip_ids_for = lambda *a, **k: set(owners)  # every key skipped: no pass examines any key
   suite = unittest.defaultTestLoader.loadTestsFromName(
       "tools.tests.test_translate_batching.ShippedKeyCoverageTests")
   result = unittest.TextTestRunner(verbosity=2).run(suite)
   print("failures", len(result.failures),
         "message_ok", any("move each key's rows" in text for _, text in result.failures))
   ```
   Expected: `FAILED (failures=1)` and `failures 1 message_ok True`. Quote both. (At planning the same
   script against the base's test printed exactly that; `*a, **k` also accepts the new `siblings=`
   keyword.)
3. Shipped-tree equality for every (language, source) pair, run once and logged (a permanent test
   would cost the 24 s back). Write `<scratch>\hoist_equality.py` and run it from your worktree's
   `tools` folder with `python -B`:

   ```python
   import sys
   sys.path.insert(0, ".")
   import translate_with_claude as t

   sources = t.english_source_files("TAOM")
   owners = t.key_owners([s for _, s, _ in sources])
   pairs = mismatches = 0
   for lang, (locale, _) in t.LANGUAGES.items():
       d = t.TAOM_LANG_DIR / lang
       siblings = t.sibling_string_ids(d)
       for _, src, tpl in sources:
           tgt = d / tpl.format(locale=locale)
           pairs += 1
           if t.skip_ids_for(src, tgt, owners, d) != t.skip_ids_for(src, tgt, owners, d, siblings=siblings):
               mismatches += 1
               print("MISMATCH", lang, src.name)
   print("pairs", pairs, "mismatches", mismatches)
   ```
   Expected: `pairs 276 mismatches 0`.
4. `discover_entries` against the base, for every language. Create the folder with
   `mkdir -p "<scratch>\old_twc"`, then write the base file to it with
   `git show 7087fa633:tools/translate_with_claude.py > "<scratch>\old_twc\translate_with_claude.py"`,
   then write and run (from your worktree's `tools` folder) `<scratch>\discover_equality.py`:

   ```python
   import importlib.util
   import sys
   sys.path.insert(0, ".")
   import translate_with_claude as new
   spec = importlib.util.spec_from_file_location("twc_old", sys.argv[1])
   old = importlib.util.module_from_spec(spec)
   spec.loader.exec_module(old)
   old.REPO_ROOT, old.TAOM_LANG_DIR = new.REPO_ROOT, new.TAOM_LANG_DIR
   def rows(m, lang):
       return [(e.file_path, e.string_id, e.english_text, e.current_text) for e in m.discover_entries(lang, "TAOM")]
   bad = [lang for lang in new.LANGUAGES if rows(old, lang) != rows(new, lang)]
   print("languages", len(new.LANGUAGES), "different", bad)
   ```
   Run it as `python -B "<scratch>\discover_equality.py" "<scratch>\old_twc\translate_with_claude.py"`.
   Expected: `languages 12 different []`. The `TAOM` filter never touches the install.
5. Timing: `python -B -m unittest tools.tests.test_translate_batching.ShippedKeyCoverageTests`, twice:
   `Ran 1 test`, `OK`, and at most 60 percent of the Step 1 figure (the planning prototype gave about
   6.6 s against about 25 s; informational).

**Verify**: each expected line above, quoted in your report.

### Step 12: full checks and the Part C commit

1. `python -m ruff check tools --no-cache`: `All checks passed!`.
2. `python -B -m unittest discover -s tools/tests -t .` (`timeout 900`): `Ran 8581 tests` (8578 plus
   the three new tests) and exactly the nine baseline failing names.
3. `python -B tools/lint_docs.py --fail-on-drift --summary`: exit 0, `ai_dashes: 0`, no finding the
   Step 1 run lacked.
4. The dotnet test command: the Step 1 totals.
5. Dash check (Step 5.2) on the uncommitted diff; `file` on the three Part C files says CRLF only.
6. Commit `tools/translate_with_claude.py`, `tools/tests/test_translate_batching.py` and
   `tools/tests/test_doc_graph_ratchet.py`. Subject, for example:
   `refactor(tools): v2.0.38 - Parse each language file and docs graph once` (the history uses
   `feat`, `fix`, `docs`, `chore`, `test`, `refactor`, `data`, `tune`; if a hook refuses the subject,
   read its reason). The body says:
   `discover_entries` now parses a language's 23 files once instead of once per source (the
   `--sync-ids` pass still parses per call, because it writes between calls); a malformed sibling
   still stops the run with the same message and a malformed target still does not; the shipped-key
   coverage test and the doc-graph ratchet tests reuse one parse and one graph build, with the before
   and after timings you measured. Add `Not-tested:` for anything you could not run (for example the
   dotnet RequiresGame tests, if no game is installed).

**Verify**: `git log --oneline -3` shows the three commits; `git status --porcelain` is empty.

## Test plan

- New, in `tools/tests/test_translate_batching.py`, class `SiblingIdsHoistTests`: hoisted equals
  per-call for every target of a synthetic language folder (three files, an override key, a shared key
  and a third-sibling key); a malformed sibling raises `SystemExit` through the hoisted form after a
  silent collection; a malformed target raises in neither form and both agree.
- Changed: `ShippedKeyCoverageTests` (hoisted, same arithmetic), proven by the can-fail check (Step 11.2) and the
  one-time `pairs 276 mismatches 0` and `languages 12 different []` scratch runs.
- Changed: `test_doc_graph_ratchet.py` (one shared graph build), proven by the unchanged 15 tests and
  the mutation check that turns `LiveTreeTests` red.
- Deletion RED: `test_every_registered_generator_exists` failing after `git rm` and before the
  registry edit, in the log (no permanent test that a file is absent).
- Pattern to follow: `EnglishOnlyOverrideTests` in the same file (temp folders, `t.key_owners`).
- Not testable here: nothing in this plan touches the engine.

## Done criteria

ALL must hold:

- [ ] `git log --oneline 7087fa633..HEAD` shows exactly three commits (Part A, B, C), each subject at
      most 72 characters with the `SubModule.xml` version.
- [ ] `git ls-files tools/generate_xslt.py tools/generate_batch2_wanderers.py tools/validate_gondor_refs.py` prints nothing.
- [ ] The Step 5.1 grep (`git grep -n -w -E "generate_xslt|generate_batch2_wanderers|validate_gondor_refs" -- ':!docs/changelog-archive' ':!docs/archive' ':!docs/migration' ':!docs/reviews' ':!plans' | cut -d: -f1,2`)
      prints exactly the first 12 lines of Step 5.1's expected list, from
      `docs/features/gondor-armor-revamp.md:30` to `docs/reference/provenance-register.md:301`: the
      `tools/README.md` line is gone after Part B, and no hit is under `tools/`.
- [ ] `grep -n -E "29 tools|25 resolve|22 of the 25|rebuild_anim_from_json" tools/README.md` prints nothing,
      and `grep -c "package_quad_toolkit.py" tools/README.md` prints at least `1`.
- [ ] `python -B -m unittest discover -s tools/tests -t .` gives `Ran 8581 tests` and exactly the
      nine baseline failing names.
- [ ] `python -m ruff check tools --no-cache` prints `All checks passed!`.
- [ ] `python -B tools/lint_docs.py --fail-on-drift --summary` exits 0 with `ai_dashes: 0`.
- [ ] The dotnet test totals equal the Step 1 totals.
- [ ] Your report quotes: the deletion RED, the `SiblingIdsHoistTests` RED, the ratchet mutation
      failure, `failures 1 message_ok True` from Step 11.2, `pairs 276 mismatches 0`,
      `languages 12 different []`, and before and after timings of
      `tools.tests.test_doc_graph_ratchet` and `ShippedKeyCoverageTests` (after at most 60 percent of before).
- [ ] `git grep -n "skip_ids_for(" -- tools/translate_with_claude.py` shows the `--sync-ids` call with no `siblings=`.
- [ ] `git status --porcelain` is empty.
- [ ] Every doc line and comment this plan supplied was re-checked against the code it describes
      (the `SubModule.xml` and `extract_wanderers.py` line numbers, the game-dir counts, the inherit
      list, the quad packager's file list).

## STOP conditions

Stop and report (do not improvise) if:

- A stem grep in Step 2 finds a hit the table lacks, above all in a hook, skill, rule, test or C# file.
- The deletion RED in Step 3 does not fail as described (the registry test may have changed).
- `SiblingIdsHoistTests` does not fail with `AttributeError` before Step 10, or its first test would
  pass with empty sets.
- `hoist_equality.py` prints any `MISMATCH`, or `discover_equality.py` reports a different language.
- The Python suite's failing set differs by name from Step 1's after any part.
- A timing does not improve (after more than 60 percent of before): commit nothing for Part C and
  report the numbers.
- The fix seems to need the `--sync-ids` loop, `tools/check_doc_graph_ratchet.py`, or any out-of-scope file.
- A step's verification fails twice after a reasonable fix.
- The assumption "no gate, hook or skill runs `generate_xslt.py`, `generate_batch2_wanderers.py` or
  `validate_gondor_refs.py`" is false.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (or record the maintainer's waiver).
- No `/localize`: no player-facing text.
- No feature doc: this is tooling hygiene; the catalog rows are the doc.
- `/deep-review`: not required by CLAUDE.md's gate (it covers C# and XML/XSLT; this branch is
  Python and docs only). `/verify` before merge as usual.

## After merge: the maintainer's actions

- Pull. The three deleted scripts disappear from his checkout. If he wants any of them back, they are
  in git history at `7087fa633`, or he can drop the Part A commit before merging.
- Decide separately whether `tools/audit_item_refs.py` and `tools/validate_all_troop_refs.py` go too
  (about 25 live procedure lines name them; a per-location report would be lost).

## Maintenance notes

- A future caller of `skip_ids_for` that writes a language file between calls must not pass a reused
  `siblings` value; the docstrings of `sibling_string_ids` and `skip_ids_for` say so.
- The review should probe: the deferred-raise path for a malformed target, the unchanged
  `--sync-ids` call, the glob order kept, and that the ratchet memo is module-level in the test only.
- Line numbers cited in C# comments (`AccentStrippedTranslationTests.cs:16` cites
  `translate_with_claude.py:298-308`) were already stale at the base and shift further; out of scope.
- Deferred: `tools/README.md:708`'s "now all read" claim was not re-verified, only dated; the
  remaining hardcoded installs (`Mount & Blade II Bannerlord` literals without `_gamedir`) are a
  separate finding.
