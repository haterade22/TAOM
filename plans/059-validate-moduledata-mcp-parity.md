# Plan 059: validate_moduledata runs one pass composition for the CLI and the MCP tool, and the #623 follow-ups are closed

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat db5680c8a..HEAD -- tools/validate_moduledata.py tools/taom_query.py tools/taom_mcp_server.py tools/validate_mesh_refs.py tools/tests/test_validate_moduledata.py tools/tests/test_taom_query.py tools/tests/test_taom_mcp_server.py tools/tests/test_validate_mesh_refs.py tools/test_hooks.sh .claude/hooks/check-moduledata-validation.sh docs/features/moduledata-validation.md docs/features/mesh-ref-validation.md docs/features/armory-ref-audit.md docs/reference/moduledata-validator-codes.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (it reshapes the composition behind the ModuleData commit gate)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `db5680c8a`, 2026-11-19
- **Baseline at that commit** (the orchestrator's run): build exit 0; dotnet 43028 passed, 3 failed,
  9 skipped; failing: `EveryKey_IsSeededInAllTwelveLanguageFiles`,
  `EveryLanguage_DeclaresARowForEveryEnglishKey`, `NoTranslatedString_MixesWritingSystems` (language
  files, not this plan's). Python suite: 10549 ran, 0 failed, 13 skipped. The orchestrator also names
  three `test_run_long_defeat` sally tests as known failures that may appear on this machine; if your
  base run shows them, they are expected and not yours.
- **Issue**: #623 (open). The maintainer approved on 2026-11-19 (digest #1457, option A) that this plan
  edits `/tools/`, and `.claude/hooks/check-moduledata-validation.sh` and `tools/test_hooks.sh` if
  item 2 needs it. This plan edits `tools/test_hooks.sh` and does NOT edit the hook.

## Why this matters

The `taom-moduledata` MCP tool `validate_moduledata` runs only the schema engine
(`ts.Validator(...).run()`), while the CLI and the commit hook also run nine install-backed passes
(collision bodies, engine XSD, generator ids, skill templates, slot fit, prison scenes, armour data)
and sweep the live Armory and `TAOM_Map` for dangling refs. An agent that trusts the MCP result gets
no verdict at all on those classes, and the MCP also passes no install path to the engine, so even
some engine checks differ. After this plan one function composes every pass; the CLI and the MCP tool
both call it, a test pins that they report the same codes, and a pass that could not run comes back
to the MCP caller as a WARNING instead of a stderr line it never sees. The plan also closes #623's
smaller items: the mesh tool's CLI byte-scans only the packs that failed to parse, ref extraction
stops rescanning each file's prefix per match and skips `Languages/`, the pack set includes every
loaded module (TAOM and `TAOM_Map` were missing), the commit hook's exit codes 0, 2 and 3 get test
cases, and the test file's script entry point moves to the end so every class runs.

## Current state

All excerpts were read at `db5680c8a`. Every Python file below is CRLF; edit with the Edit tool,
never `sed -i`. This plan changes no C# type, so no `graphify_taom.py affected` output applies; the
Python callers below come from `git grep` at `db5680c8a`.

### Files and roles

- `tools/validate_moduledata.py` (963 lines): the CLI. `main()` (:802 to :945) composes every pass;
  `entry()` (:948) turns a crash into exit 3.
- `tools/taom_query.py` (137 lines): the query API behind the MCP server. `validate()` (:125 to :137,
  under the `# -- validation --` header at :124) runs the schema engine alone.
- `tools/taom_mcp_server.py` (142 lines): the FastMCP stdio server. `_build_query()` (:43 to :65)
  builds the registries once and constructs `ModuleDataQuery`.
- `tools/validate_mesh_refs.py` (1279 lines): the mesh and body ref engine and its CLI.
- `tools/tests/test_validate_moduledata.py` (5346 lines), `tools/tests/test_taom_query.py`,
  `tools/tests/test_taom_mcp_server.py`, `tools/tests/test_validate_mesh_refs.py`: the unit tests.
- `tools/test_hooks.sh`: the hook suite; section `7m` (:3982 to :4054) tests
  `check-moduledata-validation.sh` against a stub validator.
- `.claude/hooks/check-moduledata-validation.sh`: the commit gate. Read only; this plan does not edit it.

### Item 1: the MCP runs the schema engine alone

`tools/taom_query.py:36-39` and `:125-137`:

```python
# The shipped-troop-id baseline describes the repo's own troops (validate_moduledata.py applies it
# only to the repo's ModuleData), so validate() passes it for that tree alone.
_REPO_MODULEDATA = Path(__file__).resolve().parent.parent / "Main" / "_Module" / "ModuleData"
_SHIPPED_TROOP_IDS = Path(__file__).resolve().parent / "shipped_troop_ids.txt"
...
    def validate(self, codes=None) -> dict:
        baseline = _SHIPPED_TROOP_IDS if self.moduledata.resolve() == _REPO_MODULEDATA.resolve() else None
        issues = ts.Validator(self.moduledata, self.schemas, self.reg, shipped_troop_ids_file=baseline).run()
        if codes:
            wanted = set(codes)
            issues = [i for i in issues if i.code in wanted]
        return {
            "error_count": sum(1 for i in issues if i.severity is ts.Severity.ERROR),
            "warning_count": sum(1 for i in issues if i.severity is ts.Severity.WARNING),
            "issues": [{"severity": i.severity.value, "code": i.code, "file": i.file,
                        "line": i.line, "entry_id": i.entry_id, "message": i.message}
                       for i in issues],
        }
```

`ModuleDataQuery.__init__(self, moduledata, registries: ts.Registries, schemas=None)` (:45) stores no
install path. `_REPO_MODULEDATA` and `_SHIPPED_TROOP_IDS` are used only at `taom_query.py:126` and in
`tools/tests/test_taom_query.py:165-166` (`git grep` at `db5680c8a`).

`tools/taom_mcp_server.py:52-59`:

```python
    try:
        schemas = ts.load_schemas(SCHEMA_DIR)
        registries = ts.build_registries(
            MODULEDATA, GAME_MODULES if GAME_MODULES.exists() else None)
        # The build's read cache holds every ModuleData file (about 200 MB) and nothing reads it again.
        # Clearing it also means a rebuild in this process reads the files as they are now.
        ts._read_stripped.cache_clear()
        return tq.ModuleDataQuery(MODULEDATA, registries, schemas)
```

The MCP tool `validate_moduledata(codes)` (:73-79) returns `_query.validate(codes)`.

`tools/validate_moduledata.py` `main()`, the part this plan moves (:826 to :923; the full text is in the
file, read it):

- :826-828 `ModuleData not found` returns 2 (stays in `main()`).
- :829-843 the `WARNING: Bannerlord Modules folder not found: {game_modules}` block, printed to stderr.
- :845-856 schema loading and the `Loaded ... schemas` and `Registry: ...` stderr lines (stay in `main()`).
- :858 `extra_roots = build_extra_ref_roots(game_modules)`.
- :860-863 the shipped-troop baseline and `ts.Validator(moduledata, schemas, registries,
  extra_ref_roots=extra_roots, game_modules=game_modules, shipped_troop_ids_file=shipped_ids)`.
- :864-865 `Also sweeping refs in: {root}` per swept root.
- :869-872 `WARNING: extra ref root NOT FOUND, sweep SKIPPED for {root.parent.name}: {root}` plus a
  second line, per missing root.
- :873-874 `WARNING: {warning}` per `registries.suspect_registries` entry.
- :876-878 `issues = validator.run()` and `issues += schema_invalid_issues(moduledata.parent,
  game_modules.parent / "XmlSchemas")`.
- :879-896 with the install: `generator_item_ref_issues`, `armour_class_table_issues` (skipped when
  `args.code` filters it out), `armour_acquisition_ref_issues`, `missing_collision_body_issues`,
  `borrowed_body_issues` (both sharing `functools.lru_cache(maxsize=None)(_item_refs)`),
  `slot_rejection_issues`, `skill_template_mismatch_issues(game_modules.parent, moduledata)`,
  `prison_scene_issues`.
- :897-918 without the install: ten `WARNING: <CODE> SKIPPED: ...` stderr prints.
- :919-923 sort, then the `--code` filter.

The schema pass needs the install's `XmlSchemas` folder (`tools/validate_moduledata.py:586-616`); today
`Path(game_schemas).is_dir()` at :606 raises `TypeError` for `None`:

```python
    if not vx.HAVE_LXML:
        unverified = "lxml is not installed"
    elif not Path(game_schemas).is_dir():
        unverified = f"the engine schema folder {game_schemas} was not found"
    elif not submodule.is_file():
        unverified = f"there is no {submodule}"
```

`taom_schema.Validator.__init__` (`tools/taom_schema.py:477-494`) takes `game_modules=None` and
records `missing_ref_roots` for every requested root that does not exist. With `game_modules=None`
its couchable lookup is skipped (`taom_schema.py:1710`, `if not self.game_modules or not
self.game_modules.is_dir()`), so the MCP's engine run differs from the CLI's today as well.

Two gate tests pin the composition and must move with it
(`tools/tests/test_validate_moduledata.py:5053-5066`):

```python
    @staticmethod
    def _unreached_passes(src):
        """Every module-level `def <x>_issues(` whose call does not appear in main()'s body."""
        passes = re.findall(r"^def (\w+_issues)\(", src, flags=re.M)
        main_body = re.search(r"^def main\(\).*?(?=^\S)", src, flags=re.M | re.S).group(0)
        return passes, [p for p in passes if f"{p}(" not in main_body]

    def test_every_issue_pass_is_reached_from_main(self):
```

and `test_every_emitted_code_has_a_row_in_the_codes_doc` (:5150) requires a row in
`docs/reference/moduledata-validator-codes.md` (rows are `| \`CODE\` (severity) | ... |`) for every code
emitted as `severity=ts.Severity.WARNING, code=<NAME>_CODE` in `validate_moduledata.py`.

Tests that read the CLI's stderr text, which must keep passing: `:2539` (`WORN_ARMOUR_CEILING SKIPPED`),
`:2706`, `:3232` (`extra ref root NOT FOUND, sweep SKIPPED for TAOM_Map`), `:4158`
(`MISSING_COLLISION_BODY SKIPPED`), `:4169-4170` (`faction`, `body-property`), `:4181`, `:4372-4373`,
`:4537`, `:4771`, and `NoInstallWarningTests` (:4775), which takes the stderr text before the first
`SKIPPED` and requires `BROKEN_ITEM_REF`, `BROKEN_ROSTER_REF` and `BROKEN_SETTLEMENT_REF` in it. No
schema description under `tools/schemas/` contains `SKIPPED` (checked with `grep -ril skipped tools/schemas`).

`tools/tests/test_taom_query.py:156-161` asserts that `validate(codes=["UNKNOWN_CULTURE"])` returns only
`UNKNOWN_CULTURE` issues, and `:163-170` patches `tq.ts.Validator` (the same module object as
`validate_moduledata.ts`) and compares `shipped_troop_ids_file` with `tq._SHIPPED_TROOP_IDS`.

Other consumers of the CLI's output: `.claude/hooks/check-moduledata-validation.sh` reads stdout and
the exit code, and on exit 3 it also quotes the last 15 stderr lines in its `additionalContext`
(`check-moduledata-validation.sh:170`; a crash's traceback, which no moved text changes);
`.githooks/pre-commit:105` and `tools/data_gates.py:143` run it by exit code;
`tools/audit_armory_refs.py:105` and `:340` flag any stdout or stderr line containing `SKIPPED`. Stderr
line ORDER is read by nobody (the moved lines keep their text).

The `/verify` half of #623 item 1 is already closed: `.claude/skills/verify/SKILL.md` Step 2e runs
`python tools/data_gates.py`, whose `moduledata` gate runs `python tools/validate_moduledata.py`, the
full CLI with every pass (`tools/data_gates.py:143`), whenever the tree changes ModuleData, Armory,
`TAOM_Map` or validator files. That gate exists since `f1675c058` and `/verify` Step 2e since
`69d2e8c0a` (both 2026-11-03, v2.0.36). So this plan changes the MCP tool only and leaves the skill alone.

`docs/features/armory-ref-audit.md:83-86` (Architecture, "Solution Approach", current tense, not the
Changelog) still states the gap and names the scan this plan moves:

```markdown
  its hook line. **Correction, 2026-09-18 (#622):** `main()` never called the pass, so the hook line
  blocked nothing from 2026-09-15 to 2026-09-18. It is wired now, `CommitGateCoverageTests` checks
  that every `*_issues` pass is reached from `main()`, and the MCP tool and `/verify` still do not
  run it (#623).
```

### Item 2: the hook's exit codes (already handled; only test cases are missing)

`validate_moduledata.py:948-963` already ends in `entry()`, which returns 3 on a crash. The hook
answers exit 3 with an allow that carries the traceback as `additionalContext`
(`check-moduledata-validation.sh:166-182`), not with `permissionDecision: ask` as the issue proposed;
its header (:18-23) states that design ("allowed, but never silently"), and
`.claude/rules/hook-authoring.md:80` ("A detection hook must fail open, but NEVER fail silent")
backs it. The issue's concern was a SILENT allow, which `additionalContext` removes. This plan keeps
that design and does not edit the hook. `:185-191`: any rc other than 1 (and not 3 or 124) answers
`{}` when the career kit check is clean.

`tools/test_hooks.sh` 7m covers: a raise through `entry()` (exit 3, allow with traceback, :3994-4025),
a findings exit 1 (deny, :4027-4033), an exit 1 with no output (allow, UNCHECKED, :4035-4039), then the
career kit tool cases (:4040-4053). Missing: a clean exit 0, a no-install exit 2, and a plain exit 3
with no traceback. At :4040 no `tools/generate_career_kits.py` stub exists yet in `$VM_REPO`.

`:4034-4041`:

```bash
# An exit 1 with nothing to quote: the validator died while it loaded. Allow, and say so.
printf 'import sys\nsys.exit(1)\n' > "$VM_REPO/tools/validate_moduledata.py"
VM_OUT=$(printf '%s' "$VM_PAYLOAD" | vm_run)
[[ "$VM_OUT" != *permissionDecision* ]] && grep -q 'UNCHECKED' <<< "$VM_OUT" && grep -q 'additionalContext' <<< "$VM_OUT" \
    && ok "a validator exit 1 with no findings allows the commit and says UNCHECKED" \
    || bad "a validator exit 1 with no findings: $(printf '%s' "$VM_OUT" | head -c 200)"
# The career kit tool: a crash is no drift, and it is not silent; real drift still denies.
printf 'import sys\nsys.exit(0)\n' > "$VM_REPO/tools/validate_moduledata.py"
```

`tools/test_hooks.sh` is LF.

### Item 3: the mesh CLI byte-scans every pack in the fallback

`tools/validate_mesh_refs.py:836-851`:

```python
    # ---- Tier C: collision bodies vs present PhysicsShape TOC -------------- #
    if scan_bodies:
        scan_paths = body_tpac_paths
        if scan_paths is None:
            scan_paths = [Path(p) for p in (present.tpac_paths if present else [])]
        if present is not None and present.physicsshapes:
            # Exact: bodies are first-class PhysicsShape TOC items (#352).
            present_bodies = referenced_bodies & present.physicsshapes
            unresolved = referenced_bodies - present_bodies
            # Only if a pack we needed soft-failed do we fall back to the coarse
            # byte scan — never assert MISSING on evidence we couldn't read.
            if unresolved and not present.fully_parsed:
                present_bodies |= bodies_present_in_tpacs(unresolved, scan_paths)
        else:
            # No TOC available (Tier B skipped) — coarse byte scan is all we have.
            present_bodies = bodies_present_in_tpacs(referenced_bodies, scan_paths)
```

The CLI calls `classify(refs, present, rgl, scan_bodies, body_tpac_paths=tpac_paths or None,
check_allowlist=True)` (:1237-1238) with every pack. `validate_moduledata.missing_collision_body_issues`
already passes only the unparsed packs (`validate_moduledata.py:366-367`), and
`tools/audit_armory_refs.py:357` passes every pack. `PresentSet.unparsed` holds `(path_string, error)`
pairs (`validate_mesh_refs.py:426`, filled at :543 from `TpacScanResult.path`, a `str`).

### Item 4: the quadratic line count and the `Languages/` walk

`tools/validate_mesh_refs.py:214-215`:

```python
def _lineno(text: str, pos: int) -> int:
    return text.count("\n", 0, pos) + 1
```

It is called at :281, :288 and :295, once per match in each of three `finditer` loops of
`extract_refs_from_text` (:266-301), so each call rescans the prefix. No other module uses
`validate_mesh_refs._lineno` (`tools/taom_schema.py:202` and `tools/audit_deleted_mesh_impact.py:200`
define their own). `extract_refs` (:304-318) walks `items_root.rglob("*.xml")` and reads every file,
including the top-level `Languages/` folder that both scanned roots have (`Main/_Module/ModuleData/Languages`
and the Armory's `ModuleData/Languages`). Callers of `extract_refs`: `validate_moduledata._item_refs`
(:320-326), `audit_armory_refs.py:352-354`, `audit_deleted_mesh_impact.py:781`,
`audit_fbx_lods.py:330`, `generate_armory_catalogue.py:456`, and the mesh CLI (:1183). Existing tests
assert line numbers for the attribute loop only (`tools/tests/test_validate_mesh_refs.py:76-80`); the
`<Mesh name>` and `prefab=` loops have no line assertion.

### Item 5: three pack-module lists

`tools/validate_moduledata.py:308-317`:

```python
BODY_CODE = "MISSING_COLLISION_BODY"
MESH_CODE = "MISSING_VISUAL_MESH"
# Every module whose packs the client loads for item art; which tree of each the
# engine reads is validate_mesh_refs.tpac_paths_for_modules's to decide.
_ART_MODULES = ("LOTRLOME_Armory", "Native", "SandBoxCore", "SandBox")


def _loaded_tpacs(game_modules: Path) -> list:
    import validate_mesh_refs as vmr
    return [p for m in _ART_MODULES for p in vmr.module_tpacs(Path(game_modules) / m, m)]
```

`_ART_MODULES` is also used in the message at :358. `tools/validate_mesh_refs.py:97-100`:

```python
# Shared meshes live outside the Armory (Native/SandBoxCore), so the present-set
# is the UNION across these modules' packages (each module's loose Assets/ tree,
# else its AssetPackages/: tpac_paths_for_modules).
DEFAULT_TPAC_MODULES = ["LOTRLOME_Armory", "Native", "SandBoxCore"]
```

with the help text at :1137-1140 hard-coding `(default: LOTRLOME_Armory Native SandBoxCore)`.
`DEFAULT_TPAC_MODULES` is also read by `tools/audit_deleted_mesh_impact.py:805` and
`tools/audit_gender_variation_flags.py:159` (both report-only by default). A third list sits in
`tools/audit_armory_refs.py:355`; it is out of scope (see Maintenance notes).

The loaded modules: `Main/_Module/SubModule.xml:21-25` depends on `TAOM.Dependencies`, `Native`,
`SandBoxCore`, `Sandbox`, `CustomBattle`; the dedicated server's module list
(`docs/features/coop-dedicated-server.md:44`) adds `LOTRLOME_Armory`, `TAOM_Map` and `TAOM`. Measured
on this machine on 2026-11-19 (`find` over the install): `LOTRLOME_Armory` 5676 loose and 11 cooked
packs, `TAOM` 80 loose and 5 cooked, `TAOM_Map` 2085 loose and 6 cooked, `Native` 151 cooked, `SandBox`
1 cooked, `SandBoxCore`, `TAOM.Dependencies` and `CustomBattle` none. A writer probe on the same day
(`module_tpacs` plus `build_present_set`) parsed all 2085 `TAOM_Map` packs (0 unparsed, 2150
Metameshes, 1092 PhysicsShapes, 0.6 s warm) and the repo module's 80 loose packs (0 unparsed, no
Metamesh or PhysicsShape). These are leads: Step 10 measures them again.

`slot_rejection_issues` (`validate_moduledata.py:663-665`) already takes TAOM from the repo module:
`folders = [(name, moduledata.parent if name == "TAOM" else game_modules / name) ...]`. Match it.

### Item 6: the mid-file entry point

`tools/tests/test_validate_moduledata.py:3168-3169`:

```python
if __name__ == "__main__":
    unittest.main(verbosity=2)
```

sits after `BandCloneSourceWornLowTests`; 22 test classes follow it (`ExtraRefRootTests` at :3172
onward; `tail -n +3169 tools/tests/test_validate_moduledata.py | grep -c "^class "` prints 22), and the file ends at :5346 with `ArmourAcquisitionRefTests` and no entry point, so
`python tools/tests/test_validate_moduledata.py` stops before them. `unittest discover` is unaffected.

### Conventions that apply

- `.claude/rules/tests.md` (for `tools/tests/**`): a test method must be named `test_*`; a gate test is
  proven on the states it will meet (an empty input must not read as a pass).
- `.claude/rules/data-scripts.md` (for `tools/**/*.py`): no change here writes XML.
- `.claude/rules/hook-authoring.md` "A detection hook must fail open, but NEVER fail silent": why the
  hook allows exit 3 with context.
- `.claude/rules/simplicity-criterion.md`: no helper, flag or abstraction beyond what is listed.
- ADR-002, ADR-007 and ADR-008 bind C# only; this plan changes no C#.
- Lesson `docs/reviews/lessons/build-tooling-workflow.md` "Read validator warnings as a diff against the
  base commit": every differential below is a set diff against the base run, never a name search.
- Lesson same file, "Rebinding sys.stdout at import time": never rebind a standard stream at import.

## Step 0: the maintainer's edit

None. No protected file changes.

## Commands you will need

Run every command from your worktree root (`cd "<your worktree>" && ...`). Prefix dotnet with the
TEMP and TMP your dispatch rules give, and run Python with `-B`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the base's totals (no C# changes) |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | base "Ran N" plus 11, the base failure set, no new names |
| One Python file | `python -B -m unittest discover -s tools/tests -t . -p test_validate_mesh_refs.py -v` | the named tests listed with ok |
| Data | `python tools/validate_moduledata.py` | the base's ERROR set (Step 1 records it) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Hooks | `CLAUDE_PROJECT_DIR="<your worktree>" timeout 1500 bash tools/test_hooks.sh > "<scratch>/hooks.log" 2>&1`, with `run_in_background` | "N passed, 0 failed" under Summary, N = base plus 3 |

Never `./build.ps1`.

## Scope

**In scope** (the only files you modify):
- `tools/validate_moduledata.py`
- `tools/taom_query.py`
- `tools/taom_mcp_server.py`
- `tools/validate_mesh_refs.py`
- `tools/tests/test_validate_moduledata.py`
- `tools/tests/test_taom_query.py`
- `tools/tests/test_taom_mcp_server.py`
- `tools/tests/test_validate_mesh_refs.py`
- `tools/test_hooks.sh` (section 7m only)
- `docs/features/moduledata-validation.md`, `docs/features/mesh-ref-validation.md`,
  `docs/features/armory-ref-audit.md` (lines 84 to 86 and 104 only),
  `docs/reference/moduledata-validator-codes.md` (one new row)

Scratch scripts and logs go under your scratch folder, never in the worktree.

**Out of scope** (do NOT touch, even though they look related):
- `.claude/hooks/check-moduledata-validation.sh`: its exit-3 design stays (Current state, item 2).
- `.claude/skills/**`: the `armory-audit` skill's stale sentence is an orchestrator step, and
  `.claude/skills/verify/SKILL.md` already runs the full CLI through `tools/data_gates.py` (Current
  state, item 1).
- `tools/audit_armory_refs.py` (its own module list at :355; Maintenance notes).
- `tools/taom_schema.py`, `.githooks/pre-commit`, `tools/data_gates.py`.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, every C# file.
- `CHANGELOG.md`, `plans/README.md`, and every dated entry in the docs' Changelog sections (you add a
  new entry; you never rewrite an old one).
- The gates themselves. Never turn a gate green by editing it: deleting or `@unittest.skip`-ing a test,
  loosening an assertion, or adding to an allowlist (`KNOWN_DEAD_MESHES`, `_SHARED_BODY_BY_DESIGN` and
  the like). The two test edits this plan prescribes (retargeting `_unreached_passes`, and the
  `test_taom_query.py` changes in Step 4) are the only exceptions; anything else, STOP and report.

## Git workflow

- Commit on the branch you were given; never push or open a PR. Five commits, one per stage below.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.40` at `db5680c8a`;
  a hook refuses any other).
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. No AI attribution trailer. Write the message to a file in your scratch folder and run
  `git commit -F "<file>"`. Stage explicit paths only. Never `--no-verify`.
- Optional trailers: `Not-tested:`, `Rejected:`, `Constraint:`.

## Steps

### Step 1: record the base

Before any edit, from your worktree root:

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` and
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`; record the totals line and the
   failing test names.
2. `python -B -m unittest discover -s tools/tests -t . > "<scratch>/py_base.log" 2>&1` (timeout 900);
   record the `Ran N tests` line and every FAIL or ERROR name.
3. Start the hook suite in the background (Commands table, log `<scratch>/hooks_base.log`); when it
   finishes, record the "N passed, M failed" line under Summary and every failed case name.
4. `python tools/lint_docs.py --fail-on-drift`; record the exit code.
5. Differential baselines (each with timeout 900; the mesh CLI can take minutes):
   - `<Modules>` below is the install's Modules folder, the path this prints (quote it everywhere: it
     holds spaces and `&`): `python -B -c "import sys; sys.path.insert(0, 'tools'); import validate_moduledata as vm; print(vm.DEFAULT_GAME_MODULES)"`.
   - `python -B tools/validate_moduledata.py --json "<scratch>/base_vm.json" > "<scratch>/base_vm.out" 2> "<scratch>/base_vm.err"`; record the exit code.
   - `python -B tools/validate_mesh_refs.py --no-rgl-log --scan-bodies --json "<scratch>/base_vmr.json" > "<scratch>/base_vmr.out" 2> "<scratch>/base_vmr.err"`; record the exit code and the `Tier B:` lines in the `.err` file (pack count, unparsed count).
   - Write `<scratch>/refs_dump.py` with the Write tool. Arguments: the tools folder, the game Modules
     folder, the repo ModuleData folder, an output JSON path. It puts the tools folder first on
     `sys.path`, imports `validate_mesh_refs`, and for each of the two roots
     (`<Modules>/LOTRLOME_Armory/ModuleData` and `<worktree>/Main/_Module/ModuleData`) times
     `extract_refs(root)` with `time.perf_counter()`, prints `root, ref count, seconds, count of refs
     whose file starts with "Languages/"`, and writes the sorted list of
     `[root label, r.file, r.line, r.attr, r.name, r.kind, r.item_id, r.culture]` rows to the JSON.
     Run it twice (warm the cache) and keep the second run as `<scratch>/base_refs.json`.
   - Write `<scratch>/hook_timing.py`: it reads every `--code\s+([A-Z_]+)` from
     `.claude/hooks/check-moduledata-validation.sh`, runs
     `[sys.executable, "-B", "tools/validate_moduledata.py", "--code", c1, "--code", c2, ...]` with
     cwd your worktree twice, and prints the exit code and wall seconds of each run. Record the second
     (warm) time as T_base.

**Verify**: the dotnet totals match "Baseline at that commit" (or the difference is explained); the
Python `Ran N` and failure set are recorded; the refs dump shows 0 refs under `Languages/` for both
roots. If either root has a `Languages/` ref, STOP (item 4's skip would change findings).

### Step 2: move the test file's entry point to the end (item 6)

1. RED, from the log: run `python -B tools/tests/test_validate_moduledata.py > "<scratch>/script_before.log" 2>&1`
   (timeout 900) and `python -B -m unittest discover -s tools/tests -t . -p test_validate_moduledata.py > "<scratch>/discover_vm.log" 2>&1`.
   Record both `Ran N tests` lines: the script form must run fewer tests than discover. If they are
   equal, STOP (the finding is already fixed).
2. In `tools/tests/test_validate_moduledata.py`, delete the two lines at :3168-3169 (and the blank line
   that follows them, so two blank lines separate the classes) and append at the end of the file:

```python


if __name__ == "__main__":
    unittest.main(verbosity=2)
```

3. Commit: stage `tools/tests/test_validate_moduledata.py`; subject
   `test(validator): <version> - run every validator test from the script`. Body: the script form
   stopped at the mid-file `unittest.main()` and never ran the classes after it, among them
   `ExtraRefRootTests`, `MissingCollisionBodyTests` and `CommitGateCoverageTests`; discover was
   unaffected (#623 item 6). Give the class count you measured with the `tail ... | grep -c "^class "`
   command in Current state (22 at `db5680c8a`) and the two `Ran N tests` numbers from 1.

**Verify**: `python -B tools/tests/test_validate_moduledata.py` reports the same `Ran N tests` as the
discover run of that file, and `grep -n '^if __name__ == "__main__":' tools/tests/test_validate_moduledata.py`
prints exactly one line, whose number is within the last three lines of the file (compare `wc -l`).
Do not grep the bare `__main__`: a `runpy.run_path(..., run_name='__main__')` line in the file also
matches it.

### Step 3: write the failing tests for one composition (item 1)

Add these tests. Each must fail now, for the reason named.

In `tools/tests/test_validate_moduledata.py`, add `import contextlib` to the imports, then:

a. In `CommitGateCoverageTests`, retarget the reachability scan and rename its test (this is the one
   permitted gate edit: it moves the scan to the function that now composes the passes):

```python
    @staticmethod
    def _unreached_passes(src):
        """Every module-level `def <x>_issues(` whose call does not appear in run_all_passes()'s body,
        the one composition main() and the MCP tool share (#623)."""
        passes = re.findall(r"^def (\w+_issues)\(", src, flags=re.M)
        body = re.search(r"^def run_all_passes\(.*?(?=^\S)", src, flags=re.M | re.S).group(0)
        return passes, [p for p in passes if f"{p}(" not in body]

    def test_every_issue_pass_is_reached_from_run_all_passes(self):
```

   Keep the old test's docstring and body; change only its name and the assertion message
   (`f"run_all_passes() never calls: {unreached}"`). Then add:

```python
    def test_main_and_the_mcp_tool_both_call_run_all_passes(self):
        """#623: the MCP's validate ran the schema engine alone while main() ran every pass. Both
        compose through one function, so a pass added there reaches both."""
        src = self.VALIDATOR.read_text(encoding="utf-8")
        main_body = re.search(r"^def main\(\).*?(?=^\S)", src, flags=re.M | re.S).group(0)
        self.assertIn("run_all_passes(", main_body)
        query = (Path(__file__).resolve().parents[1] / "taom_query.py").read_text(encoding="utf-8")
        body = re.search(r"^    def validate\(.*?(?=^    \S|^\S|\Z)", query, flags=re.M | re.S)
        self.assertIsNotNone(body, "taom_query.py has no ModuleDataQuery.validate")
        self.assertIn("run_all_passes(", body.group(0))
```

b. In `SchemaInvalidPassTests`:

```python
    @unittest.skipUnless(vx.HAVE_LXML, "lxml not installed")
    def test_no_install_given_is_a_not_verified_warning(self):
        """The MCP may run with no install path at all; the layer must say it was not checked."""
        issues = vm.schema_invalid_issues(self.module, None)
        self.assertEqual([(i.code, i.severity) for i in issues], [(vm.SCHEMA_CODE, ts.Severity.WARNING)])
        self.assertIn("no game install", issues[0].message)
        self.assertIn("NOT verified", issues[0].message)
```

c. A new class at the end of the file, before the `if __name__` block:

```python
class RunAllPassesParityTests(unittest.TestCase):
    """#623: the MCP tool's validate_moduledata ran ts.Validator alone, so an agent trusting it got no
    collision-body, engine-XSD, generator or skill-template verdict. main() and
    taom_query.ModuleDataQuery.validate now both call run_all_passes; these pin that they agree."""

    PASSES = re.findall(r"^def (\w+_issues)\(", Path(vm.__file__).read_text(encoding="utf-8"), flags=re.M)

    def test_the_cli_and_the_mcp_tool_report_the_same_codes(self):
        import taom_query as tq
        self.assertGreaterEqual(len(self.PASSES), 9, "the pass scan found almost nothing")
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            md = root / "ModuleData"
            _write(md / "empty.xml", "<NPCCharacters />")
            mods = root / "Game" / "Modules"
            for name in ("LOTRLOME_Armory", "TAOM_Map"):
                (mods / name / "ModuleData").mkdir(parents=True)
            cli_json = root / "cli.json"
            with contextlib.ExitStack() as stack:
                for name in self.PASSES:
                    stack.enter_context(mock.patch.object(vm, name, return_value=[ts.Issue(
                        severity=ts.Severity.WARNING, code=f"STUB_{name.upper()}", file="", line=0,
                        entry_id="", message=name)]))
                _run_main("--moduledata", str(md), "--game-modules", str(mods), "--json", str(cli_json))
                registries = ts.build_registries(md, mods)
                mcp = tq.ModuleDataQuery(md, registries, ts.load_schemas(SCHEMA_DIR),
                                         game_modules=mods).validate()
            cli_codes = {i["code"] for i in json.loads(cli_json.read_text(encoding="utf-8"))}
        mcp_codes = {i["code"] for i in mcp["issues"]} - {vm.PARTIAL_RUN_CODE}
        self.assertEqual(mcp_codes, cli_codes)
        self.assertLessEqual({f"STUB_{n.upper()}" for n in self.PASSES}, cli_codes,
                             "a pass was not run with the install present")

    def test_the_mcp_tool_returns_what_it_did_not_check_as_warnings(self):
        """The MCP has no stderr: a pass skipped for want of an install must come back as a WARNING."""
        import taom_query as tq
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp) / "ModuleData"
            _write(md / "empty.xml", "<NPCCharacters />")
            registries = ts.Registries(items=set(), item_def_files={}, npccharacters=set(),
                                       cultures={"vlandia"}, party_templates=set())
            out = tq.ModuleDataQuery(md, registries, [], game_modules=Path(tmp) / "nowhere").validate(
                codes=[vm.BODY_CODE])
        notices = [i for i in out["issues"] if i["code"] == vm.PARTIAL_RUN_CODE]
        self.assertTrue(any(f"{vm.BODY_CODE} SKIPPED" in i["message"] for i in notices), out)
        self.assertTrue(all(i["severity"] == "WARNING" for i in notices))
```

In `tools/tests/test_taom_mcp_server.py`, in `McpServerTests`:

```python
    def test_the_server_query_knows_the_install(self):
        # Without it the MCP ran none of the install-backed passes the CLI runs (#623).
        self.assertEqual(self.srv._query.game_modules, self.srv.GAME_MODULES)
```

Run: `python -B -m unittest discover -s tools/tests -t . -p test_validate_moduledata.py -k RunAllPassesParityTests -k test_main_and_the_mcp_tool -k test_every_issue_pass_is_reached -k test_no_install_given -v`
and the same with `-p test_taom_mcp_server.py -k test_the_server_query_knows_the_install -v`.

**Verify** (RED): the named tests run (a `-k` that matches nothing prints `Ran 0 tests`: STOP and fix
the filter) and fail: the two parity tests ERROR with a `TypeError` naming `game_modules`;
`test_every_issue_pass_is_reached_from_run_all_passes` ERRORs (`AttributeError`, no `run_all_passes`);
`test_main_and_the_mcp_tool_both_call_run_all_passes` FAILs on the `assertIn`;
`test_no_install_given_is_a_not_verified_warning` ERRORs with `TypeError` from `Path(None)` (or is
skipped when lxml is absent: then say so in your report); the server test ERRORs with
`AttributeError` (or skips when the `mcp` SDK is absent: say so). Quote each failure line.

### Step 4: implement `run_all_passes` and route both front ends through it (item 1)

1. `tools/validate_moduledata.py`:
   - Add `from dataclasses import dataclass` to the imports.
   - In `schema_invalid_issues` (:604-611), handle `None` before `Path(game_schemas)`:

```python
    if not vx.HAVE_LXML:
        unverified = "lxml is not installed"
    elif game_schemas is None:
        unverified = "no game install was given"
    elif not Path(game_schemas).is_dir():
```

   - Insert, immediately before `def main()`, the composition. The texts marked "verbatim" are moved
     from `main()` unchanged except that each loses its leading `WARNING: ` and its `print(...,
     file=sys.stderr)` wrapper (keep the `\n` and the continuation indentation exactly). The no-install
     text at :842 holds an em dash (U+2014, between `not 0` and `a degraded sweep`) and CLI wording ("This run will exit
     2", "pass --game-modules <path>"). Keep both verbatim: it is moved code, not prose you write, so
     the no-dash rule does not apply, and the stderr-text tests and Step 5's stderr comparison expect
     the old bytes. The MCP caller now receives that CLI wording as a notice (Maintenance notes):

```python
PARTIAL_RUN_CODE = "PARTIAL_RUN"


@dataclass
class PassRun:
    """One validator run. `issues`: the findings, sorted, filtered by `codes`. `notices`: what this
    run did NOT check (no install, a live module missing, a registry that looks too small), one
    WARNING each under PARTIAL_RUN_CODE; the CLI prints them to stderr and the MCP tool, which has
    no stderr, returns them as issues. `swept_roots`: the live ModuleData roots the ref sweep read."""
    issues: list
    notices: list
    swept_roots: list


def run_all_passes(moduledata, game_modules, schemas, registries, codes=None) -> PassRun:
    """Every validator pass, composed once (#623). main() and taom_query.ModuleDataQuery.validate (the
    MCP tool) both call this, so the two cannot drift apart again: until #623 the MCP ran the schema
    engine alone. `game_modules` None, or a folder that does not exist, means no install: each
    install-backed pass is skipped with a notice, never silently."""
    moduledata = Path(moduledata)
    game_modules = Path(game_modules) if game_modules is not None else None
    have_install = game_modules is not None and game_modules.exists()
    notices = []

    def notice(text):
        notices.append(ts.Issue(severity=ts.Severity.WARNING, code=PARTIAL_RUN_CODE, file="", line=0,
                                entry_id="", message=text))

    if not have_install:
        # Per environment-failures.md: report, don't guess. The item/culture
        # registry will be TAOM-only, so external refs will look broken.
        notice(...)  # verbatim: main() :832-843, starting "Bannerlord Modules folder not found: {game_modules}\n"
    extra_roots = build_extra_ref_roots(game_modules) if game_modules is not None else []
    # The baseline describes the repo's own troops; a --moduledata elsewhere has no shipped ids to lose.
    shipped_ids = SHIPPED_TROOP_IDS if moduledata.resolve() == MODULEDATA.resolve() else None
    validator = ts.Validator(moduledata, schemas, registries, extra_ref_roots=extra_roots,
                             game_modules=game_modules, shipped_troop_ids_file=shipped_ids)
    # Never let a vanished root pass as a clean run (main() :866-868's comment, moved verbatim).
    for root in validator.missing_ref_roots:
        notice(...)  # verbatim: main() :870-871, "extra ref root NOT FOUND, sweep SKIPPED for ..."
    for warning in registries.suspect_registries:
        notice(warning)

    issues = validator.run()
    # The engine's XmlSchemas folder sits beside Modules in the install root.
    issues += schema_invalid_issues(
        moduledata.parent, game_modules.parent / "XmlSchemas" if game_modules is not None else None)
    if have_install:
        ...  # main() :880-896 verbatim, comments included, with `args.code` replaced by `codes`
    else:
        for text in (...):  # main() :898-918 verbatim, the ten texts after "WARNING: "
            notice(text)
    issues.sort(key=lambda i: i.sort_key())
    if codes:
        wanted = set(codes)
        issues = [i for i in issues if i.code in wanted]
    return PassRun(issues=issues, notices=notices, swept_roots=list(validator.extra_ref_roots))
```

   - Rewrite `main()` from :829 to :923 so it reads, in order: the schema loading and the two existing
     stderr lines (:845-856, unchanged), then

```python
    run = run_all_passes(moduledata, game_modules, schemas, registries, codes=args.code)
    for root in run.swept_roots:
        print(f"Also sweeping refs in: {root}", file=sys.stderr)
    for n in run.notices:
        print(f"WARNING: {n.message}", file=sys.stderr)
    issues = run.issues
```

     followed by the unchanged `print(ts.format_report(issues))`, the `--json` block and the exit
     codes (:925-945). The :829-843 block and :858-923 are gone from `main()` (moved, not copied).
   - Update the two docstrings that state the gap. In `missing_collision_body_issues`, lines 341-342
     read `commit hook runs (wired into main() by #622; the MCP tool and /verify do not` and
     `run it yet, #623).`: make the parenthesis read `(wired into main() by #622; the MCP tool runs
     it too since #623, through run_all_passes)`, rewrapping the two lines. In
     `skill_template_mismatch_issues`, lines 529-530, delete the sentence that begins `The MCP's
     validate_moduledata runs the Validator only` and ends `never reaches this pass (#623).`
   - `PARTIAL_RUN_CODE` is emitted as `severity=ts.Severity.WARNING, code=PARTIAL_RUN_CODE`, so
     `CommitGateCoverageTests.test_every_emitted_code_has_a_row_in_the_codes_doc` now requires its row.
     In `docs/reference/moduledata-validator-codes.md`, insert this row on its own line directly after
     the row that starts `` | `SCHEMA_INVALID` (error) | `` (line 63 at `db5680c8a`, the last of the
     install-backed pass rows):
     `` | `PARTIAL_RUN` (warn) | a part of the run that checked nothing: no game install (each install-backed pass), a live module whose ModuleData is missing, or a registry that looks too small. The CLI prints these on stderr and never in its report; the MCP tool `validate_moduledata` returns them as issues whatever its `codes` filter, because it has no stderr (#623). Repair the install path, never the data | ``
     The row is staged with the Step 6 commit.
2. `tools/taom_query.py`:
   - `import validate_moduledata as vm` after `import taom_schema as ts`; delete `_REPO_MODULEDATA`,
     `_SHIPPED_TROOP_IDS` and their comment (:36-39).
   - `__init__(self, moduledata, registries: ts.Registries, schemas=None, game_modules=None)`, storing
     `self.game_modules = Path(game_modules) if game_modules is not None else None`.
   - `validate()`:

```python
    def validate(self, codes=None) -> dict:
        """Every pass the CLI runs (validate_moduledata.run_all_passes, #623). What the run could not
        check comes back as PARTIAL_RUN warnings whatever `codes` says: the MCP has no stderr."""
        run = vm.run_all_passes(self.moduledata, self.game_modules, self.schemas, self.reg, codes=codes)
        issues = run.issues + run.notices
        return { ... }   # the existing dict, unchanged
```

   - Update the module docstring's last paragraph only if it claims the MCP runs less than the CLI
     (at `db5680c8a` it does not; leave it).
3. `tools/taom_mcp_server.py:59`: `return tq.ModuleDataQuery(MODULEDATA, registries, schemas, game_modules=GAME_MODULES)`.
   In the `validate_moduledata` tool docstring (:75-78) add one sentence: it runs every pass the CLI
   runs, and a pass that could not run is a `PARTIAL_RUN` WARNING.
4. `tools/tests/test_taom_query.py` (the permitted contract change):
   - add `import validate_moduledata as vm  # noqa: E402` after the `taom_query` import;
   - in `test_validate_returns_counts_and_issues`, change the filter assertion to
     `self.assertTrue(all(i["code"] in ("UNKNOWN_CULTURE", vm.PARTIAL_RUN_CODE) for i in only["issues"]))`
     and add `self.assertTrue(any(i["code"] == vm.PARTIAL_RUN_CODE for i in out["issues"]), "no install was given, and the result must say so")`;
   - in `test_validate_checks_the_shipped_troop_baseline_for_the_repo_tree_only`, replace
     `tq._REPO_MODULEDATA` with `vm.MODULEDATA` and `tq._SHIPPED_TROOP_IDS` with `vm.SHIPPED_TROOP_IDS`.

**Verify**: the Step 3 command lines now report every named test ok;
`python -B -m unittest discover -s tools/tests -t . -p test_taom_query.py -v` all ok;
`python -B -m unittest discover -s tools/tests -t . -p test_validate_moduledata.py` shows `OK`
(or only the base's own failures in that file). Without the codes-doc row it fails
`test_every_emitted_code_has_a_row_in_the_codes_doc` with `Lists differ: ['PARTIAL_RUN'] != []`: add
the row, never edit the test.

### Step 5: prove the CLI is unchanged and the MCP now matches it on the real install

1. `python -B tools/validate_moduledata.py --json "<scratch>/c2_vm.json" > "<scratch>/c2_vm.out" 2> "<scratch>/c2_vm.err"` (timeout 900).
   Write `<scratch>/json_diff.py` (two JSON paths; loads both lists; compares the sets of
   `(severity, code, file, line, entry_id, message)` tuples; prints `added N`, `removed N` and up to
   20 of each). Run it on `base_vm.json` and `c2_vm.json`. Expected: `added 0`, `removed 0`, and the
   same exit code as the base. `diff` the two `.out` files: identical. Compare the SORTED lines of
   `base_vm.err` and `c2_vm.err`, leaving out the line that starts `Wrote ` (it names the JSON path):
   equal. Only their order may change (a pass's own stderr line, such as `WARNING: module 'SandBoxCore'
   contributes no .tpac at all ...`, now prints before the `Also sweeping refs in:` lines).
2. Write `<scratch>/mcp_probe.py`: it puts `<worktree>/tools` first on `sys.path`, imports
   `taom_schema as ts`, `taom_query as tq`, `validate_moduledata as vm`; builds
   `ts.build_registries(vm.MODULEDATA, vm.DEFAULT_GAME_MODULES if vm.DEFAULT_GAME_MODULES.exists() else None)`,
   calls `ts._read_stripped.cache_clear()`, builds
   `tq.ModuleDataQuery(vm.MODULEDATA, registries, ts.load_schemas(vm.SCHEMA_DIR), game_modules=vm.DEFAULT_GAME_MODULES)`,
   runs `validate()` inside `contextlib.redirect_stdout(buf)` with `buf = io.StringIO()`, then prints
   the number of characters written to stdout, the wall seconds, the `PARTIAL_RUN` notices, and the
   tuple-set diff of the non-`PARTIAL_RUN` issues against `<scratch>/base_vm.json`. Run it (timeout 900).
   Expected: `added 0`, `removed 0` against the base CLI run.
3. If the probe wrote any character to stdout: a pass prints to stdout, which would corrupt the MCP's
   stdio channel. Then wrap the `run_all_passes` call in `validate()` in
   `with contextlib.redirect_stdout(sys.stderr):` (import `contextlib` and `sys`), add the test
   `test_validate_writes_nothing_to_stdout` to `tools/tests/test_taom_query.py` (patch
   `vm.generator_item_ref_issues` with a `side_effect` that prints to stdout and returns `[]`, give the
   query an existing empty `game_modules` folder, capture stdout with `redirect_stdout`, assert it is
   empty), show it fail before the wrap and pass after, and say so in your report. If the probe wrote
   0 characters, do none of this.

**Verify**: both diffs read `added 0` and `removed 0`; the sorted stderr lines are equal; the probe's
stdout count is 0 (or step 3 was done and its test passes). If a diff is not empty, first check whether the live install changed since
Step 1 (`find "<Modules>/LOTRLOME_Armory/ModuleData" "<Modules>/TAOM_Map/ModuleData" -newer "<scratch>/base_vm.json" -name "*.xml" | head`);
either way, STOP and report the diff.

### Step 6: document item 1 and commit it

1. The `PARTIAL_RUN` row of `docs/reference/moduledata-validator-codes.md` is already in place (Step 4).
2. `docs/features/moduledata-validation.md`:
   - In "MCP server (interactive querying)", after the tools table, add: `validate_moduledata` runs
     `validate_moduledata.run_all_passes`, the same composition as the CLI (#623), so it takes about as
     long as a CLI run and returns the same findings; what the run could not check comes back as
     `PARTIAL_RUN` warnings.
   - Add a Changelog entry at the top of "## Changelog": `- 2026-11-19 (#623): ...` saying the MCP
     tool now runs every pass through `run_all_passes`, which `main()` also calls;
     `CommitGateCoverageTests` checks that every `*_issues` pass is reached from `run_all_passes` and
     that `main()` and `ModuleDataQuery.validate` both call it; `RunAllPassesParityTests` pins equal
     codes; `PARTIAL_RUN` added; the CLI's stderr lines keep their text, and the no-install and
     missing-root warnings now print after the `Registry:` line. Do not edit the dated 2026-09-15,
     2026-09-19 and 2026-09-21 entries that say the MCP does not run a pass: they were true then.
3. `docs/features/armory-ref-audit.md`:
   - :104: replace `(MCP, /verify: #623)` with `(and the MCP tool, #623)`.
   - :84-86 (excerpt in Current state, item 1): replace the text from `It is wired now,` through
     `run it (#623).` with the text below, rewrapped to the paragraph's width and two-space indent.
     Leave the `**Correction, 2026-09-18 (#622):**` sentence before it as it is.

```markdown
It is wired now. Since #623 (2026-11-19) every `*_issues` pass is composed in `run_all_passes()`,
which `main()` and the MCP tool both call, and `CommitGateCoverageTests` checks that each pass is
reached from there; `/verify` runs the full CLI through `tools/data_gates.py`.
```
4. In `tools/tests/test_validate_moduledata.py:4052`, the `MissingCollisionBodyTests` docstring:
   replace `(the MCP tool and /verify do not yet, #623)` with `(the MCP tool too since #623)`.
5. Run the full Python suite and `python tools/lint_docs.py --fail-on-drift`.
6. Commit: stage, by explicit path, each `tools/` file changed in Steps 3 to 5 (from
   `tools/validate_moduledata.py`, `tools/taom_query.py`, `tools/taom_mcp_server.py`,
   `tools/tests/test_validate_moduledata.py`, `tools/tests/test_taom_query.py`,
   `tools/tests/test_taom_mcp_server.py`) and the three docs `docs/reference/moduledata-validator-codes.md`
   (Step 4), `docs/features/moduledata-validation.md` and `docs/features/armory-ref-audit.md`; subject
   `fix(validator): <version> - MCP validate_moduledata runs every pass`.
   Body: the MCP tool ran the schema engine alone and passed no install path, so it never reported a
   collision body, an engine-XSD failure, a generator's retired id, a skill template, slot fit or a
   prison scene; one function now composes every pass for the CLI and the MCP tool; a pass the MCP run
   could not do is a `PARTIAL_RUN` warning; the CLI's report and exit codes are unchanged (the
   differential on the live install found no change). Say that the `/verify` half of #623 item 1 needed
   no change: `/verify` Step 2e has run the full CLI through `tools/data_gates.py` since v2.0.36. Trailer:
   `Not-tested: the taom-moduledata server in a live Claude session (in-process call_tool test only)`.

**Verify**: the Python suite prints `Ran N` = base plus 5 so far (4 new in
`test_validate_moduledata.py`, the renamed test not counted; 1 in `test_taom_mcp_server.py`; plus 1 if
Step 5.3 added a test, say which) with the base failure set;
lint exits 0; `git status --porcelain` is empty after the commit.

### Step 7: pin the hook's answer to exits 0, 2 and 3 (item 2)

1. In `tools/test_hooks.sh`, insert between the "exit 1 with no findings" case (ends at :4039) and the
   `# The career kit tool:` comment (:4040):

```bash
# Exit 0 (clean) and exit 2 (no install: a degraded sweep) answer {}: only a findings exit 1 blocks.
# A plain exit 3 with no traceback still says CRASHED (#623 asked for every exit code to be pinned).
printf 'import sys\nsys.exit(0)\n' > "$VM_REPO/tools/generate_career_kits.py"
for vm_rc in 0 2; do
    printf 'import sys\nsys.exit(%s)\n' "$vm_rc" > "$VM_REPO/tools/validate_moduledata.py"
    VM_OUT=$(printf '%s' "$VM_PAYLOAD" | vm_run)
    [[ "$VM_OUT" == '{}' ]] \
        && ok "a validator exit $vm_rc allows the commit with {}" \
        || bad "a validator exit $vm_rc: $(printf '%s' "$VM_OUT" | head -c 200)"
done
printf 'import sys\nsys.stderr.write("plain crash\\n")\nsys.exit(3)\n' > "$VM_REPO/tools/validate_moduledata.py"
VM_OUT=$(printf '%s' "$VM_PAYLOAD" | vm_run)
[[ "$VM_OUT" != *permissionDecision* ]] && grep -q 'CRASHED' <<< "$VM_OUT" && grep -q 'additionalContext' <<< "$VM_OUT" \
    && ok "a validator exit 3 allows the commit and says CRASHED" \
    || bad "a validator exit 3: $(printf '%s' "$VM_OUT" | head -c 200)"
```

2. RED by mutation (these pin shipped behaviour, so they pass on the shipped hook; show each can
   fail). Copy the whole `.claude/hooks/` folder into `<scratch>/hookcopy/.claude/hooks/` (a scratch
   copy of code under test, which your dispatch rules allow). Write `<scratch>/hook_red.py` that, for
   each mutation below, writes a mutated copy of `check-moduledata-validation.sh` in that folder (a
   Python `str.replace` that must match exactly once, else it prints an error), builds a sandbox git
   repository in scratch as 7m does (`git init`; `tools/validate_moduledata.py` and
   `tools/generate_career_kits.py` stubs; one staged `Main/_Module/ModuleData/x.xml`; use
   `GIT_CONFIG_GLOBAL=<an empty file>` and `GIT_CONFIG_NOSYSTEM=1`), pipes the payload
   `{"tool_name":"Bash","tool_input":{"command":"git commit -m \"data: v0.0.0 - stub\""},"hook_event_name":"PreToolUse"}`
   into `<scratch>/hookcopy/.claude/hooks/check-moduledata-validation.sh`, run by the bash that
   `shutil.which("bash")` names (Git Bash), with `CLAUDE_PROJECT_DIR` set to the sandbox and cwd the
   sandbox, and prints the hook's exit code and answer. Never pass the bare name `"bash"` to
   `subprocess`: on this machine Windows resolves it to the WSL `bash.exe` in System32, which exits 1
   with empty output (measured: `shutil.which("bash")` printed the Git Bash path, and
   `subprocess.run(["bash", "-c", "echo hi"])` gave rc 1 and empty stdout). An empty answer means the
   hook did not run: treat it as "RED not demonstrated", never as a non-expected answer:
   - `if [[ $RC -ne 1 ]]; then` becomes `if [[ $RC -ne 1 && $RC -ne 0 ]]; then`; validator stub exit 0:
     the answer must NOT be `{}`.
   - `if [[ $RC -ne 1 ]]; then` becomes `if [[ $RC -eq 0 ]]; then`; validator stub exit 2: the answer
     must NOT be `{}`.
   - `if [[ $RC -eq 3 ]]; then` becomes `if [[ $RC -eq 99 ]]; then`; validator stub exit 3: the answer
     must NOT contain `CRASHED`.
   Also run it once unmutated per case and confirm the expected answer (the control). If the scratch
   copy cannot run at all (a sourced file missing), record "RED not demonstrated: <reason>" and go on;
   do not edit anything in your worktree to make it run.
3. Run the hook suite in the background (Commands table, log `<scratch>/hooks_c3.log`).
4. Commit: stage `tools/test_hooks.sh`; subject
   `test(hooks): <version> - pin the ModuleData hook answer to exits 0, 2, 3`. Body: #623 asked for a
   case per exit code; 1 and a raise were covered, a clean exit, a no-install exit 2 and a plain exit
   3 were not; the hook itself is unchanged and keeps allowing a crash with the traceback as context.
   Trailer: `Rejected: answering exit 3 with ask; the hook's documented design allows with context, which removes the silent allow #623 named`.

**Verify**: `<scratch>/hooks_c3.log` shows "N passed, 0 failed" under Summary with N = the base's
passed count plus 3 (if the base had failures, the same failure names and no new ones); the three new
`ok` texts appear in the log only if the suite runs verbose, so count, do not grep; the mutation runs
printed the non-expected answers (or the report says why not).

### Step 8: write the failing tests for the mesh tool (items 3 and 4)

In `tools/tests/test_validate_mesh_refs.py` add `from unittest import mock` to the imports, then:

a. In `TierCBodyScanTests`:

```python
    def test_fallback_byte_scans_only_the_packs_that_failed_to_parse(self):
        """#623: a parsed pack's TOC already lists its bodies (#352), so only a pack that failed to
        parse can hide one. The CLI passed every pack, and byte-scanning 4,611 of them took 110-119 s."""
        good = self._tpac_with_token(b"bo_elsewhere")
        bad = self._tpac_with_token(b"bo_hidden")
        refs = vm.extract_refs_from_text('<Item id="sw" body_name="bo_hidden" />\n', "x.xml")
        present = vm.PresentSet(physicsshapes={"bo_other"}, tpac_paths=[str(good), str(bad)],
                                unparsed=[(str(bad), "suspicious udep_count")])
        for paths in (None, [good, bad]):
            with self.subTest(body_tpac_paths=paths), \
                 mock.patch.object(vm, "bodies_present_in_tpacs", wraps=vm.bodies_present_in_tpacs) as scan:
                issues = vm.classify(refs, present, None, scan_bodies=True, body_tpac_paths=paths)
                self.assertEqual([Path(p) for p in scan.call_args.args[1]], [bad])
                self.assertNotIn("MISSING_BODY", _codes(issues))
```

b. In `ExtractRefsTests`:

```python
    def test_extract_refs_skips_the_languages_folder(self):
        """A language file holds <string> rows, never an item's art (#623)."""
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            (root / "Languages" / "DE").mkdir(parents=True)
            (root / "Languages" / "DE" / "strings.xml").write_text('<a mesh="from_languages" />\n', encoding="utf-8")
            (root / "items.xml").write_text('<Item id="i" mesh="from_items" />\n', encoding="utf-8")
            names = sorted(r.name for r in vm.extract_refs(root))
        self.assertEqual(names, ["from_items"])

    def test_mesh_element_and_prefab_refs_carry_their_source_line(self):
        # Pins the <Mesh name> and prefab= loops' line numbers, which no test read before the #623
        # line-count change; a comment over three lines sits in front of them.
        xml = ('<Items>\n'                                    # 1
               '  <!-- a\n   b\n   c -->\n'                    # 2-4
               '  <Item id="a" prefab="pf_a">\n'               # 5
               '    <meshes><Mesh name="m_a" /></meshes>\n'    # 6
               '  </Item>\n'                                   # 7
               '  <Item id="b" mesh="sk_b" />\n'               # 8
               '</Items>\n')
        lines = {(r.kind, r.name): r.line for r in vm.extract_refs_from_text(xml, "x.xml")}
        self.assertEqual(lines, {("prefab", "pf_a"): 5, ("visual_mesh", "m_a"): 6, ("visual_mesh", "sk_b"): 8})
```

Run: `python -B -m unittest discover -s tools/tests -t . -p test_validate_mesh_refs.py -k test_fallback_byte_scans -k test_extract_refs_skips_the_languages -k test_mesh_element_and_prefab -v`.

**Verify**: 3 tests ran (not 0). RED: `test_fallback_byte_scans_only_the_packs_that_failed_to_parse`
FAILs on both subtests (the scan got `[good, bad]`); `test_extract_refs_skips_the_languages_folder`
FAILs (`['from_items', 'from_languages']`). `test_mesh_element_and_prefab_refs_carry_their_source_line`
PASSES now: it pins behaviour the next step must keep (if it fails on the base code, the excerpt's
line arithmetic is wrong: correct the expected lines to what the base code returns, and say so).

### Step 9: implement items 3 and 4, prove parity, commit

1. `tools/validate_mesh_refs.py` `classify` (:845-848: the two comment lines that start `# Only if a
   pack we needed soft-failed`, the `if` and its call), the TOC branch's fallback, becomes:

```python
            # Only if a pack we needed soft-failed do we fall back to the coarse byte scan, and then
            # only of the packs that failed: a parsed pack's TOC already lists its bodies (#352), and
            # byte-scanning every pack took 110-119 s over 4,611 packs (#623).
            if unresolved and not present.fully_parsed:
                present_bodies |= bodies_present_in_tpacs(
                    unresolved, [Path(p) for p, _ in present.unparsed])
```

   Leave the `else` branch (no TOC: scan `scan_paths`) unchanged. Update the `body_tpac_paths` line of
   the docstring (:772-773) to say it is the byte-scan list when no TOC is available; with a TOC only
   the packs that failed to parse are byte-scanned.
2. Replace `_lineno` (:214-215) with:

```python
def _line_index(text: str):
    """Position -> 1-based line for `text`. The newline offsets are found once, so each lookup is a
    binary search, not a rescan of the prefix (that rescan was quadratic over a file's refs, #623)."""
    newlines = [m.start() for m in re.finditer("\n", text)]
    return lambda pos: bisect.bisect_left(newlines, pos) + 1
```

   add `import bisect` to the imports, and in `extract_refs_from_text` set
   `line_of = _line_index(stripped)` once and replace the three `_lineno(stripped, m.start())` calls
   with `line_of(m.start())`. Then `git grep -n "_lineno" -- tools/validate_mesh_refs.py` must print nothing.
3. In `extract_refs`, compute `rel` before reading the file and skip a language file:

```python
    for xml in sorted(items_root.rglob("*.xml")):
        try:
            rel = xml.relative_to(items_root).as_posix()
        except ValueError:
            rel = xml.as_posix()
        # A language file holds <string> rows, never an item's mesh or body (#623).
        if rel.split("/", 1)[0] == "Languages":
            continue
        try:
            text = xml.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        refs.extend(extract_refs_from_text(text, rel))
```

4. Run the Step 8 command: all 3 ok. Run the whole `test_validate_mesh_refs.py` and
   `test_validate_moduledata.py` files: OK (or the base's failures only).
5. Differentials: rerun `refs_dump.py` twice and keep the second as `<scratch>/c4_refs.json`;
   `python -B tools/validate_mesh_refs.py --no-rgl-log --scan-bodies --json "<scratch>/c4_vmr.json" > "<scratch>/c4_vmr.out" 2> "<scratch>/c4_vmr.err"`;
   `json_diff.py` of `base_vmr.json` against `c4_vmr.json`; a plain file compare of `base_refs.json`
   and `c4_refs.json` (identical: the base had 0 `Languages/` refs, Step 1). Report both
   `extract_refs` timings, base and now, per root.
6. Commit: stage `tools/validate_mesh_refs.py` and `tools/tests/test_validate_mesh_refs.py`; subject
   `perf(mesh-refs): <version> - scan only unparsed packs; count lines once`. Body: the CLI's Tier C
   fallback byte-scanned every pack whenever one pack failed to parse (the validator pass already
   scanned only the failed ones); ref extraction counted each match's line by rescanning the file from
   the start, and walked the `Languages/` folders, which hold no item art; the timings measured in
   step 5; refs and findings identical on the live install.

**Verify**: both diffs empty (`added 0`, `removed 0`; the refs files identical); the per-root
`extract_refs` time is not higher than the base's. If a diff is not empty, or a timing is higher,
STOP and report.

### Step 10: one pack-module list with every loaded module (item 5)

1. RED tests.
   - `tools/tests/test_validate_mesh_refs.py`, in `TpacModuleFallbackTests`:

```python
    def test_the_default_modules_are_every_module_loaded_for_art(self):
        """#623: the engine resolves a mesh or a body from every loaded module; TAOM and TAOM_Map were
        not scanned, so art only they ship would read as missing."""
        self.assertEqual(sorted(vm.DEFAULT_TPAC_MODULES),
                         sorted(["LOTRLOME_Armory", "TAOM", "TAOM_Map", "Native", "SandBoxCore", "SandBox"]))
```

   - `tools/tests/test_validate_moduledata.py`, in `MissingCollisionBodyTests`:

```python
    def test_loaded_tpacs_reads_taom_map_packs(self):
        with tempfile.TemporaryDirectory() as d:
            mods = Path(d) / "Modules"
            for name in ("LOTRLOME_Armory", "TAOM_Map", "Native"):
                (mods / name / "Assets").mkdir(parents=True)
                (mods / name / "Assets" / f"{name}.tpac").write_bytes(b"x")
            got = {p.name for p in vm._loaded_tpacs(mods)}
        self.assertEqual(got, {"LOTRLOME_Armory.tpac", "TAOM_Map.tpac", "Native.tpac"})

    def test_loaded_tpacs_takes_taom_from_the_repo_module(self):
        # The commit gate judges the repo's tree, as slot_rejection_issues does, not the last deploy.
        with tempfile.TemporaryDirectory() as d:
            mods = Path(d) / "Modules"
            (mods / "TAOM" / "Assets").mkdir(parents=True)
            (mods / "TAOM" / "Assets" / "installed.tpac").write_bytes(b"x")
            repo = Path(d) / "repo" / "_Module"
            (repo / "Assets").mkdir(parents=True)
            (repo / "Assets" / "repo.tpac").write_bytes(b"x")
            (repo / "ModuleData").mkdir()
            got = {p.name for p in vm._loaded_tpacs(mods, repo / "ModuleData")}
        self.assertEqual(got, {"repo.tpac"})
```

   Run them by name:
   - `python -B -m unittest discover -s tools/tests -t . -p test_validate_mesh_refs.py -k test_the_default_modules_are_every_module_loaded_for_art -v`:
     `Ran 1 test`; it FAILs (the old list).
   - `python -B -m unittest discover -s tools/tests -t . -p test_validate_moduledata.py -k test_loaded_tpacs -v`:
     `Ran 3 tests`, because `-k` also matches the existing
     `test_loaded_tpacs_keeps_the_modules_path_it_was_given`, which passes (now and after the step).
     `test_loaded_tpacs_reads_taom_map_packs` FAILs (`TAOM_Map.tpac` missing) and
     `test_loaded_tpacs_takes_taom_from_the_repo_module` ERRORs (`TypeError`, one positional argument).
2. Implement.
   - `tools/validate_mesh_refs.py:97-100`: replace the comment and list with

```python
# Every module the client loads that ships packs an item's art can resolve from: TAOM's dependency chain
# (Main/_Module/SubModule.xml: Native, SandBoxCore, Sandbox; TAOM.Dependencies and CustomBattle ship no
# packs), TAOM itself, and the two live modules TAOM authors into. The present-set is their UNION
# (each module's loose Assets/ tree, else its AssetPackages/: tpac_paths_for_modules).
# validate_moduledata reads this same list (#623).
DEFAULT_TPAC_MODULES = ["LOTRLOME_Armory", "TAOM", "TAOM_Map", "Native", "SandBoxCore", "SandBox"]
```

     and make the `--tpac-modules` help text derive its default:
     `f"(default: {' '.join(DEFAULT_TPAC_MODULES)})"`.
   - `tools/validate_moduledata.py:310-317`: delete `_ART_MODULES` and its comment; replace
     `_loaded_tpacs` with

```python
def _loaded_tpacs(game_modules: Path, moduledata: Path = None) -> list:
    """The packs of every module the client loads for item art (validate_mesh_refs.DEFAULT_TPAC_MODULES),
    each tree as validate_mesh_refs.module_tpacs chooses it. TAOM is read from the repo module this run
    validates (`moduledata.parent`) when `moduledata` is given, as slot_rejection_issues does."""
    import validate_mesh_refs as vmr
    out = []
    for name in vmr.DEFAULT_TPAC_MODULES:
        folder = Path(moduledata).parent if name == "TAOM" and moduledata is not None else Path(game_modules) / name
        out.extend(vmr.module_tpacs(folder, name))
    return out
```

     In `missing_collision_body_issues` call `_loaded_tpacs(game_modules, moduledata)` and change the
     message's `' / '.join(_ART_MODULES)` to `' / '.join(vmr.DEFAULT_TPAC_MODULES)` (the function
     already imports `vmr`). `git grep -n "_ART_MODULES" -- tools` must then print nothing.
3. Run the three tests: ok. Run the full `test_validate_moduledata.py` and `test_validate_mesh_refs.py`: OK.
4. Differentials and timing:
   - `python -B tools/validate_moduledata.py --json "<scratch>/c5_vm.json" ...` and `json_diff.py`
     against `c2_vm.json`; `validate_mesh_refs.py --no-rgl-log --scan-bodies --json "<scratch>/c5_vmr.json" ...`
     and `json_diff.py` against `c4_vmr.json`; read the `Tier B:` lines of `c5_vmr.err` for the pack
     and unparsed counts.
   - Expected: `added 0` in both. A removed finding is allowed only when its mesh or body ships in a
     pack of a module this step added. The validator gains `TAOM_Map` and the repo's TAOM; the mesh
     CLI gains `TAOM_Map`, `SandBox` and the INSTALLED TAOM (it reads every module from the install).
     Check each removed name against
     `vmr.build_present_set(vmr.module_tpacs(<Modules>/TAOM_Map, "TAOM_Map") + vmr.module_tpacs(<Modules>/SandBox, "SandBox") + vmr.module_tpacs(<Modules>/TAOM, "TAOM") + vmr.module_tpacs(<worktree>/Main/_Module, "TAOM"))`
     (metameshes for a mesh, physicsshapes for a body) with a scratch script, and list each in your
     report. (On 2026-11-19 the mesh CLI found 0 findings both before and after this change, so no
     removal is expected.)
   - The unparsed count must not rise: a new unparsed pack downgrades every missing visual mesh to
     `UNVERIFIED_MESH`, which the validator drops, so `MISSING_VISUAL_MESH` warnings would vanish for
     the wrong reason.
   - `hook_timing.py`: record the warm time T_c5.
5. The measurement gate: if T_c5 > T_base + 5 s or T_c5 > 30 s, or any finding was added, or a removed
   finding's art is not in the added modules' set above, or the unparsed count rose: undo this step's
   edits with the Edit tool (restore the excerpts above), confirm `git status --porcelain` is empty,
   and report the numbers. That stop is a successful outcome of the plan: items 1 to 4 and 6 stand.
6. Docs: `docs/features/mesh-ref-validation.md`, the Dependencies bullet (:149): the module set
   becomes `{LOTRLOME_Armory,TAOM,TAOM_Map,Native,SandBoxCore,SandBox}`; add a dated Changelog entry
   at the top of its "## Changelog" (2026-11-19, #623): the default module list now holds every module
   loaded for art and is the one list `validate_moduledata` reads; the Tier C fallback byte-scans only
   packs that failed to parse; extraction skips `Languages/` and counts lines once; quote the measured
   timings and the differential result. In `docs/features/moduledata-validation.md`, extend the
   2026-11-19 entry from Step 6 with one sentence: `MISSING_COLLISION_BODY` and `MISSING_VISUAL_MESH`
   now resolve against TAOM and `TAOM_Map` packs too (the repo module for TAOM), with the
   differential's result.
7. `python tools/lint_docs.py --fail-on-drift`: exit 0.
8. Commit: stage `tools/validate_mesh_refs.py`, `tools/validate_moduledata.py`,
   `tools/tests/test_validate_mesh_refs.py`, `tools/tests/test_validate_moduledata.py`,
   `docs/features/mesh-ref-validation.md`, `docs/features/moduledata-validation.md`; subject
   `fix(validator): <version> - check art against every loaded module`. Body: the collision-body gate
   scanned the Armory, Native, SandBoxCore and SandBox, and the mesh tool omitted SandBox as well, while
   the engine resolves art from every loaded module, TAOM and `TAOM_Map` included; a body only
   `TAOM_Map` ships would have read as a commit-blocking MISSING; one list now serves both tools (and
   `audit_deleted_mesh_impact.py` and `audit_gender_variation_flags.py`, which read it); the measured
   hook time and the differential.

**Verify**: the gate in 5 passed (or was handled as written); `git status --porcelain` is empty.

### Step 11: final verification

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` and
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: the base's totals and failures.
2. `python -B -m unittest discover -s tools/tests -t .`: `Ran` = base plus 11 (plus 1 if Step 5.3
   added a test; minus 3 if Step 10 was undone), the base failure set and no new names.
3. `python tools/validate_moduledata.py`: the ERROR set equals the base's (from `base_vm.json`).
4. `python tools/lint_docs.py --fail-on-drift`: exit 0.
5. `git log --oneline db5680c8a..HEAD`: four or five commits, each subject at most 72 characters.

**Verify**: every line above holds, quoted in your report with its totals line.

## Test plan

- `tools/tests/test_validate_moduledata.py`: `RunAllPassesParityTests` (2 tests: equal codes with every
  pass stubbed and the install present; PARTIAL_RUN warnings when it is absent);
  `CommitGateCoverageTests.test_main_and_the_mcp_tool_both_call_run_all_passes` (new) and
  `test_every_issue_pass_is_reached_from_run_all_passes` (renamed, retargeted);
  `SchemaInvalidPassTests.test_no_install_given_is_a_not_verified_warning`;
  `MissingCollisionBodyTests.test_loaded_tpacs_reads_taom_map_packs` and
  `test_loaded_tpacs_takes_taom_from_the_repo_module`. Pattern: `MissingCollisionBodyTests.test_main_runs_the_pass`
  (drives `main()` through `_run_main` with `mock.patch.object(vm, ...)`).
- `tools/tests/test_taom_mcp_server.py`: `test_the_server_query_knows_the_install` (skips without the `mcp` SDK).
- `tools/tests/test_taom_query.py`: two existing tests adapted to the new contract (no new test unless Step 5.3).
- `tools/tests/test_validate_mesh_refs.py`: the fallback narrowing (both `body_tpac_paths` forms), the
  `Languages/` skip, the `<Mesh>` and `prefab` line numbers (a characterization pin for the line-count
  refactor; its parity proof is the Step 9 differential), and the default module list.
- `tools/test_hooks.sh` 7m: exits 0, 2 and a plain 3, proven failable by the Step 7 mutations.
- The live-data proofs: the four differentials (Steps 5, 9, 10) and the MCP probe (Step 5).
- Not testable here: the MCP server inside a live Claude session (the in-process `call_tool` test
  stands in), named in the Step 6 commit's `Not-tested:` trailer.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The build command exits 0 and the dotnet totals equal the base's
- [ ] The Python suite: `Ran` = base plus the new tests (Step 11.2), no failure name the base lacked
- [ ] The hook suite log: base passed plus 3, 0 new failures
- [ ] `python -B -m unittest discover -s tools/tests -t . -p test_validate_moduledata.py -k RunAllPassesParityTests -v` runs 2 tests, both ok
- [ ] `git grep -n -E "run_all_passes\(" -- tools/validate_moduledata.py tools/taom_query.py` shows the definition, the call in `main()` and the call in `ModuleDataQuery.validate`
- [ ] `git grep -n -E "_ART_MODULES|_REPO_MODULEDATA|_SHIPPED_TROOP_IDS" -- tools` prints nothing (or only `_ART_MODULES` lines if Step 10 was undone), and `git grep -n "_lineno" -- tools/validate_mesh_refs.py` prints nothing
- [ ] `git grep -n -E "run it yet, #623|never reaches this pass|MCP, /verify: #623|and /verify do not yet" -- tools docs/features/armory-ref-audit.md` prints nothing
- [ ] `git grep -n -E "reached from .main\(\)|/verify. still do not" -- docs/features/armory-ref-audit.md` prints nothing (at `db5680c8a` it prints line 85)
- [ ] `git grep -n "PARTIAL_RUN" -- docs/reference/moduledata-validator-codes.md` prints one row, starting `` | `PARTIAL_RUN` (warn) | ``
- [ ] Step 5's diffs and the MCP probe: `added 0`, `removed 0`, 0 stdout characters (or Step 5.3 done)
- [ ] Step 9's diffs: identical; Step 10's: as its gate requires
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `git status --porcelain` is empty and every commit touches only in-scope files
- [ ] Every comment, docstring, doc line and test oracle this plan supplied was re-checked against the code it describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at a "Current state" location does not match its excerpt.
- Step 2.1 shows the script form already runs every test (item 6 already fixed).
- Step 1 finds a ref under `Languages/` in either root.
- The Step 5 differential shows any change in the CLI's findings, or the MCP probe differs from the
  base CLI run: the refactor changed a verdict.
- A pass writes to stdout and the Step 5.3 guard does not silence it.
- Any test outside the ones this plan names must be edited to pass, or a stderr-text test fails after
  Step 4 (a moved text changed).
- A RED test passes before its implementation (other than the characterization test Step 8 names).
- `lint_docs` fails on a budget: shorten the text you added; never raise a budget. If it still fails, STOP.
- The work seems to need the hook, a skill, `taom_schema.py`, `audit_armory_refs.py` or any C# file.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: #623. After merge, comment the commits on it and close it when the maintainer verifies; no
  in-game check is owed (tooling only). The comment says the `/verify` half of item 1 needed no change
  (`/verify` Step 2e runs the full CLI through `tools/data_gates.py` since v2.0.36) and that item 2's
  hook keeps allowing a crash with context instead of `ask`.
- Deep-review lens routing: tooling.
- Recommend to the maintainer one edit outside this plan's approved paths,
  `.claude/skills/armory-audit/SKILL.md:79`: replace
  `the MCP tool and \`/verify\` still do not run it, #623` with `the MCP tool runs it too since #623`.
- No `/localize` (no player-facing text); no new feature doc (the existing feature docs are updated).

## After merge: the maintainer's actions

Pull, then restart Claude Code sessions: the `taom-moduledata` MCP server is a long-lived process and
keeps the old `validate` until it restarts. Its `validate_moduledata` now takes about as long as a CLI
run (15.8 s on the live install in a probe of the planned code, 2026-11-19).

## Maintenance notes

- A new validator pass goes into `run_all_passes`, not `main()`; `CommitGateCoverageTests` fails until
  it is reached from there.
- `tools/audit_armory_refs.py:355` keeps its own module list `[module, "Native", "SandBoxCore",
  "SandBox"]`. Deferred: that tool writes the committed `docs/audits/armory-ref-audit.md`, so widening
  its pack set changes that report and belongs to an `/armory-audit` run. It gains the Step 9
  fallback narrowing for free (it calls `classify` with every pack).
- The hook header's timing ledger (`check-moduledata-validation.sh:112-117`, dated 2026-09-21) names
  the quadratic line count that Step 9 removes. It stays as dated history; the next re-measure should
  replace it.
- The hook still answers a validator crash with an allow plus context, not `ask` as the issue first
  proposed; changing that is the maintainer's decision.
- Review should probe: that every text moved into `run_all_passes` is byte-identical to the old
  stderr text; that `run_all_passes` handles `game_modules=None` on every path (`build_extra_ref_roots`,
  `schema_invalid_issues`, `Validator`); the MCP's stdout cleanliness; and that `_loaded_tpacs` reads
  TAOM from the repo module only when `moduledata` is given.
- `armour_class_table_issues` and `validate_mesh_refs.module_tpacs` still print some notices straight
  to stderr inside a pass (no Armory, a module with no packs); the MCP does not see those. A follow-up
  could turn them into notices.
- The no-install notice keeps its CLI wording ("This run will exit 2", "pass --game-modules <path>")
  and its em dash, because the CLI's stderr must stay byte-identical. An MCP caller reads the same
  text; the server always passes its install path, so it meets this notice only when that path is
  gone. Rewording it for both callers is a separate change that must update the stderr-text tests.
- After Step 10, `_loaded_tpacs(game_modules, moduledata)` always includes the repo module's packs
  (85 tracked `*.tpac` under `Main/_Module` at `db5680c8a`), so the `if not tpacs` guard in
  `missing_collision_body_issues` ("no *.tpac found ... NOT verified") no longer fires on the commit
  path when the install's art modules hold no packs. Such a run reports each body ref as MISSING
  instead, which still denies the commit. Counting only the install's packs for that guard is a
  possible follow-up.
- The MCP validate does not refill `ts._read_stripped` (the read cache of about 200 MB the server clears after
  its registry build): no pass calls `ts.build_registries`, and a probe of the planned code on the live
  install read `currsize=0` after `validate()` (2026-11-19). A pass that starts building registries
  must clear that cache in `ModuleDataQuery.validate`.
- Step 10 lets the commit gate accept a collision body that only `TAOM_Map` ships. That holds only while
  `TAOM_Map` is always loaded with TAOM: `Main/_Module/SubModule.xml` does not declare it as a
  dependency (and the live `TAOM_Map` SubModule.xml names TAOM only in a load-order
  `DependedModuleMetadata`, not a `DependedModule`). If TAOM ever runs without
  `TAOM_Map`, such a body hangs preload and the gate passes it; re-weigh the module list then.
- Executor's measurement, 2026-11-19, contradicting the `_read_stripped` note above: after
  `validate()` on the live install the cache held 4398 entries, and the base MCP's
  `ts.Validator(...).run()` alone (no install, no extra roots) leaves the same 4398, so the cache refill
  predates this plan; it is `Validator.run`, not a pass. Clearing it after `validate()` is a follow-up.
- The `Languages/` skip in `validate_mesh_refs.extract_refs` (Step 9) assumes no SubModule.xml
  `XmlNode` loads items from a `ModuleData/Languages/` path. A module that did would have its refs there
  go unchecked.
