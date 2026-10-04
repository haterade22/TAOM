# Plan 035: The release packager refuses a scene shipped without its shader cache and reports each TAOM DLL's JIT optimization

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat dffdf879..HEAD -- tools/package_release.py tools/tests/test_package_release.py .claude/skills/release/SKILL.md docs/reference/release-process.md docs/modding/module-map.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: LOW
- **Depends on**: none
- **Category**: dx
- **Planned at**: commit `dffdf879`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472); failing: `EveryLanguage_DeclaresARowForEveryEnglishKey` (untranslated keys; the paid
  translator run waits on the maintainer). Python suite: not yet recorded (Step 1 records it).
  `python -B -m unittest tools.tests.test_package_release` alone: `Ran 57 tests`, `OK` (measured by
  the plan writer on a tree whose copy of that file is identical to `dffdf879`).
- **Issue**: filed by the orchestrator before execution

## Why this matters

The testing channel's `TAOM_Map/SceneObj/Main_map/ShaderCache/D3D11/` holds only
`terrain_shaders_header_data.bin` (45,832 bytes, 2026-09-28) and no `compressed_shader_cache.sack`,
while the patreon and public channels ship a 1,835,980-byte sack for the same scene (2026-09-14). The
live dev install matches testing, so the next release would ship the campaign map without its
compiled shaders and every player would compile them at runtime. Nothing in the release process
notices: `tools/package_release.py` copies whatever `SceneObj` holds. After this plan the packager
exits 2 on any run (`--dry-run` included, because the release flow only ever dry-runs it) and names
every scene that ships the header without the sack, with a per-scene override for a deliberate
exception. It also prints one line per shipped copy of `TAOM.dll` and `TAOM.Dependencies.dll`
saying whether the JIT optimizes it, so the Debug-versus-Release state of a release is visible
(TAOM ships Debug builds on purpose; that line never refuses).

## Current state

### Files

- `tools/package_release.py` (514 lines, stdlib only): the release packager. `classify` decides each
  module-relative path (COPY, EXCLUDE, UNKNOWN), `plan_module` walks a module into a `ModulePlan`
  whose `_copy_list` holds `(rel, size)` for every COPY path, `require_build` is the opt-in
  `--require-build` gate, `main` wires the CLI.
- `tools/tests/test_package_release.py` (618 lines, stdlib `unittest`): 57 tests, synthetic module
  trees in temp dirs, CLI tests through `subprocess` (`_run`).
- `.claude/skills/release/SKILL.md`: the `/release` skill. Phase 8 step 2 runs the packager.
- `docs/reference/release-process.md`: the release procedure. Step 9 runs the packager.
- `docs/modding/module-map.md`: the TAOM_Map module guide; its packaging step 5 runs the packager.

### How the release flow uses the packager (why the gate must bite in `--dry-run`)

`.claude/skills/release/SKILL.md:144-150` at `dffdf879`:

```
2. Gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --require-build vX.Y.Z --dry-run`
   must print `build stamp OK` and exit 0. It reads every `bin/<platform>/` copy of `TAOM.dll` and
   `TAOM.Dependencies.dll` and refuses one whose stamp says `.dirty` or `nogit`, or names a commit
   other than the tag's; a requested TAOM or TAOM.Dependencies missing from `--source`; and a tag whose
   `Directory.Build.props` predates the `.dirty` flag (the 1.4.5 line until it is ported).
   It proves the DLLs only. Deploys never delete, so the install also holds files from every
   earlier deploy. Before packaging, prune only what neither the tag nor its build owns:
```

`.claude/skills/release/SKILL.md:173-176`:

```
3. Package: **Mike packages through the Modding Kit editor, not Claude** (2026-09-28). Stop after the
   prune and hand over; never write a package with the command above.
4. Once his editor package exists (in `E:\LOTRAOM_Releases\<channel>\Modules\`), offer step 2's dry
   run with `--source` pointing at that folder before he uploads it.
```

So the packager now runs only with `--dry-run`. Its two existing check conventions:

- **Unknown entries** (`package_release.py:440-449`, `:492-496`): listed always, but a `--dry-run`
  returns 0 (`:488-490`) before the refusal at `:492`. A check built this way would never refuse in
  the release flow.
- **`--require-build`** (`:451-462`): refuses with exit 2 in `--dry-run` too, errors on stderr.

This plan follows the `--require-build` convention (exit 2, stderr, `--dry-run` included) and adds
an escape hatch in the unknown-entries style (`--allow-unknown`): `--allow-missing-shader-cache
SCENE`, repeatable, because vanilla itself ships header-only scenes (below), so a deliberate one must
not be unshippable.

### The code this plan touches, excerpts at `dffdf879`

`tools/package_release.py:33-35` (docstring tail):

```python
  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release --require-build v2.0.31 --dry-run

Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination / failed --require-build.
```

`tools/package_release.py:39-47` (imports): `argparse, json, os, re, shutil, subprocess, sys`, then
`from dataclasses import dataclass, field` and `from pathlib import Path, PurePosixPath`. No `struct`.

`tools/package_release.py:97-102`:

```python
# The Modding Kit writes SceneObj/Backups/<scene> and SceneEditData/Backups/<scene> on every
# scene save (437 MB for Main_map on 2026-09-14). Both parents are KNOWN_TOP_DIRS because
# vanilla ships them, so the include-list alone would wave the backups through.
SCENE_BACKUP_PARENTS = frozenset({"SceneObj", "SceneEditData"})

CANDIDATE_RULES = ("EM_ASSET_PACKAGES", "RACE_TEST")
```

`tools/package_release.py:106-120`: `SHIPPED_DLLS = {"taom": "TAOM.dll", "taom.dependencies":
"TAOM.Dependencies.dll"}` (keyed casefolded), then `STAMP_RE`, then:

```python
# The Directory.Build.props target that writes the .dirty flag. A commit without it stamps a bare
# SHA whatever the tree held, so a clean-looking stamp from such a commit proves nothing.
DIRTY_FLAG_TARGET = "TaomStampWorkingTreeState"
```

`tools/package_release.py:159-165`: `stamps_dirty_trees` ends at line 162, then
`def require_build(plans, rev: str, requested=()) -> tuple:` at line 165. Inside it,
`:179-186`:

```python
    for p in plans:
        dll_name = SHIPPED_DLLS.get(p.name.casefold())
        if dll_name is None:
            continue
        copies = [rel for rel, _size in p._copy_list
                  if rel.casefold().startswith("bin/")
                  and PurePosixPath(rel).name.casefold() == dll_name.casefold()]
        client = f"bin/Win64_Shipping_Client/{dll_name}"
```

`tools/package_release.py:241-242` (`classify`): `SceneObj/Backups/...` and `SceneEditData/Backups/...`
are EXCLUDE with rule `SCENE_BACKUPS`, so they never enter `_copy_list`.

`tools/package_release.py:397-400`: `_report` ends at line 397, lines 398 and 399 are blank, and
`def main(argv=None) -> int:` is at line 400.

`tools/package_release.py:409-411`:

```python
    ap.add_argument("--allow-unknown", action="store_true",
                    help="proceed even though unrecognised entries were found (they are still not copied)")
    ap.add_argument("--dry-run", action="store_true", help="report only; writes nothing")
```

`tools/package_release.py:438-462`:

```python
    _report(plans, args)

    unknown_total = sum(p.unknown_bytes for p in plans)
    has_unknown = any(p.unknown for p in plans)
    if has_unknown:
        ...
                print(f"  ... and {len(p.unknown) - 20} more in {p.name}")

    if args.require_build is not None:
        problems, checked = require_build(plans, args.require_build, names)
        if problems:
            print(f"\nERROR: not a clean build of {args.require_build}; refusing to package:",
                  file=sys.stderr)
            for msg in problems:
                print(f"  {msg}", file=sys.stderr)
            return 2
        print(f"\nbuild stamp OK: {len(checked)} TAOM assembly copies are clean builds of "
              f"{args.require_build}:")
        for rel in checked:
            print(f"  {rel}")

    if args.json:
```

### Facts about scene shader caches (measured read-only by the plan writer, 2026-10-02)

- Layout: `<Module>/SceneObj/<scene>/ShaderCache/D3D11/` holds `terrain_shaders_header_data.bin` and
  `compressed_shader_cache.sack`. Only `D3D11` folders exist under TAOM_Map's `ShaderCache`.
- Live dev install `Modules/TAOM_Map/SceneObj`: 47 entries. 44 scenes have both files; `Main_map`
  and `taom_rohan_edoras_town_forceatmo` (folder dated 2026-09-30) have the header only; `Backups`
  holds header-only copies of those two, and the packager already excludes `SceneObj/Backups`.
- Release channels: `testing` matches the dev install (same two header-only scenes); `patreon` and
  `public` (45 entries) have both files for every scene, `Main_map`'s sack being 1,835,980 bytes.
- `LOTRLOME_Armory/SceneObj`: 8 scenes, all with both files. `TAOM` and `TAOM.Dependencies` have no
  `SceneObj`. The check therefore runs over every planned module (it is path-shaped, no module
  branch); today it fires only for the two TAOM_Map scenes.
- Vanilla ships header-only scenes too: `Native` 2 of 82 (`__default_new_editor_scene_`,
  `braidentest`), `SandBoxCore` 8 of 206 (`battle_terrain_020`, `_026`, `_030`, `_031`,
  `battle_terrain_biome_094`, `_130`, `battle_terrain_s`, `battle_terrain_u`). No vanilla scene has
  a sack without a header. This is why the rule is a TAOM release rule with an override, not an
  engine invariant.
- Cost of a missing sack: `docs/features/shader-precompilation.md:5` records that "TAOM_Map battle
  scenes ship no `compressed_shader_cache.sack`, so their terrain/atmosphere shaders runtime-compile
  on entry". (That doc's claim that every TAOM_Map scene is header-only, lines 130 and 146, measured
  2026-08-10, is stale today: 44 now carry sacks. Not this plan's to fix; see Maintenance notes.)
- Issue #448 (open) reported that TAOM's three module-level sacks (`<Module>/Shaders/D3D11/`) carried
  format `0x0782` while v1.4.8 writes `0x0783`. The 44 per-scene TAOM_Map sacks in the dev install
  and the public `Main_map` sack all read `83 07 00 00` at byte offset 4 today. The check therefore
  tests presence only and its message says nothing about format versions; module-level
  `Shaders/D3D11/` sacks are out of scope.

### Facts about the JIT optimization report (measured by the plan writer, 2026-10-02)

- `System.Diagnostics.DebuggableAttribute` has two constructors, `.ctor(Boolean, Boolean)` and
  `.ctor(DebuggingModes)`; `DebuggingModes`: `None=0`, `Default=1`,
  `IgnoreSymbolStoreSequencePoints=2`, `EnableEditAndContinue=4`, `DisableOptimizations=0x100` (read
  from PowerShell 7's reflection of the enum).
- The value blob of the assembly-level attribute, read with `System.Reflection.Metadata`:
  `01-00-07-01-00-00-00-00` (0x107, optimization off) in the dev install's `TAOM.dll` and
  `TAOM.Dependencies.dll`; `01-00-02-00-00-00-00-00` (0x2) in a Release `TAOM.dll`, `DryIoc.dll`,
  `Newtonsoft.Json.dll`, `TaleWorlds.Core.dll`, `0Harmony.dll`; `TAOM.NativeSkinFixes.dll` has no
  .NET metadata.
- The plan's reader (Step 3) was run by the plan writer on those files and returned True, True,
  False, False, False, False, False, None respectively, matching the oracle. It is stdlib only
  (`struct`): the CI job "Python Tool Tests" runs `python3 -m unittest discover -s tools/tests -t .`
  with no pip install (`.github/workflows/build.yml:180-216`), so no third-party PE library is
  available.
- The existing `--require-build` tests write stand-in DLLs (`_dll_bytes`, `b"MZ\x90\x00\x01\x00" +
  stamp + ...`) that have no PE structure. The report must read those as "unknown" without raising
  and without changing any exit code.
- Which DLLs: `SHIPPED_DLLS` (TAOM's own two assemblies), every `bin/<platform>/` copy, the same set
  `require_build` reads. Third-party DLLs are not TAOM's and are not reported.

### Conventions that bind this change

- AGENTS.md "TDD": the failing tests (Step 2) come before the implementation (Step 3).
- `.claude/rules/simplicity-criterion.md`: the override flag earns its keep because vanilla ships
  header-only scenes; nothing else is added "for later" (no JSON manifest field, no new module list).
- ADR-002 (thin entry points under 150 lines), ADR-007 (adapters for sealed TaleWorlds types) and
  ADR-008 (services testable without the game) bind C# only; this plan changes no C#, so they do not
  apply, and no `graphify_taom.py affected` blast radius exists for Python. `taom-src` is not needed:
  no TaleWorlds type is involved.
- `tools/package_release.py` keeps its existing style: module-level constants with a comment saying
  why, stdlib only, errors to stderr, `from __future__ import annotations` already at the top (so
  `bool | None` hints are fine).
- AGENTS.md "Human prose": no em or en dash in the docs you edit or the commit body.

## Commands you will need

Prefix every command with the `TEMP` and `TMP` your dispatch rules give (the tests create temp dirs).

| Purpose | Command | Expected on success |
|---|---|---|
| Packager tests | `python -B -m unittest tools.tests.test_package_release` | `Ran 77 tests`, `OK` after Step 3 |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | Step 1's failure set, no new names; total 20 higher |
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline totals above (no C# changes) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | the exit code Step 1 recorded (0 expected) |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify):

- `tools/package_release.py`
- `tools/tests/test_package_release.py`
- `.claude/skills/release/SKILL.md` (Phase 8 step 2 only)
- `docs/reference/release-process.md` (step 9 only)
- `docs/modding/module-map.md` (packaging step 5 only)

**Out of scope** (do NOT touch, even though they look related):

- Any sack, scene or DLL: rebuilding, copying or restoring a `compressed_shader_cache.sack` is the
  maintainer's editor step. Never write into the game install or the release-channel folder; Step 5's
  smoke run only reads them, with `--dry-run`.
- The build configuration (`build.ps1`, `Main/TAOM.csproj`, `Directory.Build.props`): the report
  never refuses a Debug build and this plan changes nothing about it.
- `docs/features/shader-precompilation.md` (its stale "zero sacks" lines; Maintenance notes).
- Module-level `<Module>/Shaders/D3D11/` sacks and issue #448's format question.
- `CHANGELOG.md`, `plans/README.md`, `tools/README.md`.
- The gates themselves: never turn a test green by loosening an assertion or deleting it. STOP
  instead.

## Git workflow

- This work goes on a branch of its own (the maintainer's requirement): commit only on the branch
  you were given for this plan, never on a trunk or a branch shared with another plan (STOP
  conditions); never push or open a PR.
- Subject `feat(release): <version> - packager refuses a scene missing its sack`, where `<version>`
  is the `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at
  planning, which gives 66 characters); at most 72 characters (a hook refuses any other shape).
- Body wrapped at 72, written for a release-note reader: what the packager now refuses and why
  (Main_map shipping without its sack), the override flag, the JIT line and that it never refuses.
  No AI attribution trailer. Add `Not-tested: a packaged build played in game; the gate reads files
  only`.
- Write the message to a file and `git commit -F <file>`; stage the five in-scope paths by name.

## Steps

### Step 1: record the base

Before any edit, run and keep the output for your report:

1. `python -B -m unittest discover -s tools/tests -t .` (with `timeout 900`): write down the
   `Ran N tests` line and the result line, and every `FAIL:` and `ERROR:` test name.
2. `python -B -m unittest tools.tests.test_package_release`: expect `Ran 57 tests` and `OK`.
3. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: expect the baseline totals line
   in Status.
4. `python tools/lint_docs.py --fail-on-drift`; write down its exit code.

**Verify**: item 2 prints `Ran 57 tests` and `OK`; item 3 matches the Status baseline (or the
difference is explained in your report).

### Step 2: add the failing tests (RED)

Edit `tools/tests/test_package_release.py`:

1. Add `import struct` on its own line after `import shutil` (line 28).
2. Insert the block below between the end of `class TestRequireBuildCli` (its last line is
   `self.assertIn("cannot resolve", r.stderr)` in `test_refuses_an_unresolvable_rev`, line 614) and
   the `if __name__ == "__main__":` line, keeping two blank lines before each top-level definition.
   It uses the file's existing `_write`, `_run`, `STAMP`, `SHA` and `_dll_bytes`.

```python
# --------------------------------------------------------------------------- #
# Scene shader caches: a header without its sack is refused                    #
# --------------------------------------------------------------------------- #
SHADER_DIR = "SceneObj/{}/ShaderCache/D3D11"


def _scene(root: Path, scene: str, header: bool, sack: bool):
    d = SHADER_DIR.format(scene)
    if header:
        _write(root, f"{d}/terrain_shaders_header_data.bin", 10)
    if sack:
        _write(root, f"{d}/compressed_shader_cache.sack", 20)
    _write(root, f"SceneObj/{scene}/scene.xscene", 5)


class TestSceneShaderCache(unittest.TestCase):
    def _missing(self, build):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td) / "TAOM_Map"
            _write(root, "SubModule.xml", 10)
            build(root)
            return pr.scenes_missing_shader_cache(pr.plan_module(root))

    def test_scene_with_header_and_sack_passes(self):
        self.assertEqual(self._missing(lambda r: _scene(r, "Main_map", True, True)), [])

    def test_header_without_sack_names_the_scene(self):
        self.assertEqual(self._missing(lambda r: _scene(r, "Main_map", True, False)), ["Main_map"])

    def test_scene_with_neither_file_passes(self):
        self.assertEqual(self._missing(lambda r: _scene(r, "Main_map", False, False)), [])

    def test_sack_without_header_passes(self):
        self.assertEqual(self._missing(lambda r: _scene(r, "Main_map", False, True)), [])

    def test_only_the_scene_missing_its_sack_is_named(self):
        def build(r):
            _scene(r, "Main_map", True, False)
            _scene(r, "mod_taom_erebor_kitbash", True, True)
            _scene(r, "taom_rohan_edoras_town_forceatmo", True, False)
        self.assertEqual(self._missing(build), ["Main_map", "taom_rohan_edoras_town_forceatmo"])

    def test_scene_backups_are_not_judged(self):
        # SceneObj/Backups is excluded (SCENE_BACKUPS) and never ships, so a header-only
        # backup must not block the release.
        def build(r):
            _write(r, "SceneObj/Backups/Main_map/ShaderCache/D3D11/terrain_shaders_header_data.bin", 10)
        self.assertEqual(self._missing(build), [])


class TestSceneShaderCacheCli(unittest.TestCase):
    def _modules(self, td):
        src = Path(td) / "Modules"
        _write(src, "TAOM_Map/SubModule.xml", 10)
        _scene(src / "TAOM_Map", "Main_map", True, False)
        _scene(src / "TAOM_Map", "mod_taom_erebor_kitbash", True, True)
        return src

    def _gate(self, src, td, *extra, dry_run=True):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", "TAOM_Map",
                    *(["--dry-run"] if dry_run else []), *extra)

    def test_dry_run_refuses_a_scene_without_its_sack(self):
        # The release flow only ever runs the packager with --dry-run (the maintainer
        # packages in the editor), so the refusal must bite there.
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td), td)
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM_Map/SceneObj/Main_map", r.stderr)
            self.assertNotIn("mod_taom_erebor_kitbash", r.stderr)

    def test_a_refused_real_run_writes_nothing(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td), td, dry_run=False)
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM_Map/SceneObj/Main_map", r.stderr)
            self.assertFalse((Path(td) / "out").exists())

    def test_allowing_the_named_scene_lets_the_run_through(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._gate(self._modules(td), td, "--allow-missing-shader-cache", "Main_map")
            self.assertEqual(r.returncode, 0, r.stderr)

    def test_allowing_one_scene_does_not_allow_another(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td)
            _scene(src / "TAOM_Map", "Second", True, False)
            r = self._gate(src, td, "--allow-missing-shader-cache", "Main_map")
            self.assertEqual(r.returncode, 2, r.stdout)
            self.assertIn("TAOM_Map/SceneObj/Second", r.stderr)
            self.assertNotIn("TAOM_Map/SceneObj/Main_map", r.stderr)


# --------------------------------------------------------------------------- #
# JIT optimization report: read DebuggableAttribute, never refuse              #
# --------------------------------------------------------------------------- #
def _pad4(b: bytes) -> bytes:
    return b + b"\0" * (-len(b) % 4)


def _managed_dll(ca_value=None) -> bytes:
    """A minimal x64 managed PE whose only custom attribute is
    [assembly: System.Diagnostics.DebuggableAttribute(...)] with the value blob `ca_value`
    (prolog included). None builds the assembly without the attribute.

    Tables: Module, TypeRef (DebuggableAttribute), MemberRef (.ctor), CustomAttribute,
    Assembly, one row each; every heap and coded index is 2 bytes wide. The layout follows
    ECMA-335 II.24 and II.25; System.Reflection.Metadata reads these files back."""
    strings = b"\0fixture.dll\0fixture\0DebuggableAttribute\0System.Diagnostics\0.ctor\0"
    s_mod, s_asm, s_name, s_ns, s_ctor = (strings.index(x) for x in (
        b"fixture.dll\0", b"fixture\0", b"DebuggableAttribute\0", b"System.Diagnostics\0",
        b".ctor\0"))
    sig = b"\x20\x02\x01\x02\x02" if ca_value is not None and len(ca_value) == 6 else b"\x20\x01\x01\x08"
    blob = b"\0" + bytes([len(sig)]) + sig
    val_idx = len(blob)
    if ca_value is not None:
        blob += bytes([len(ca_value)]) + ca_value
    present = [0x00, 0x01, 0x0A, 0x20] + ([0x0C] if ca_value is not None else [])
    rows = {
        0x00: struct.pack("<HHHHH", 0, s_mod, 1, 0, 0),                     # Module
        0x01: struct.pack("<HHH", (1 << 2) | 0, s_name, s_ns),               # TypeRef in Module 1
        0x0A: struct.pack("<HHH", (1 << 3) | 1, s_ctor, 1),                  # MemberRef on TypeRef 1
        0x0C: struct.pack("<HHH", (1 << 5) | 14, (1 << 3) | 3, val_idx),     # CustomAttribute
        0x20: struct.pack("<IHHHHIHHH", 0x8004, 1, 0, 0, 0, 0, 0, s_asm, 0),  # Assembly
    }
    tables = struct.pack("<IBBBBQQ", 0, 2, 0, 0, 1, sum(1 << t for t in present), 0)
    tables += b"".join(struct.pack("<I", 1) for _t in sorted(present))
    tables += b"".join(rows[t] for t in sorted(present))
    heaps = [(b"#~", _pad4(tables)), (b"#Strings", _pad4(strings)), (b"#GUID", b"\x11" * 16),
             (b"#Blob", _pad4(blob))]
    version = _pad4(b"v4.0.30319\0")
    offset = 16 + len(version) + 4 + sum(8 + len(_pad4(n + b"\0")) for n, _d in heaps)
    headers, body = b"", b""
    for name, data in heaps:
        headers += struct.pack("<II", offset, len(data)) + _pad4(name + b"\0")
        body += data
        offset += len(data)
    md = (struct.pack("<IHHII", 0x424A5342, 1, 1, 0, len(version)) + version
          + struct.pack("<HH", 0, len(heaps)) + headers + body)

    rva, file_align, headers_size, pe_off = 0x2000, 0x200, 0x200, 0x80
    cli = struct.pack("<IHHII", 72, 2, 5, rva + 72, len(md)) + b"\0" * (72 - 16)
    section = cli + md
    raw = section + b"\0" * (-len(section) % file_align)
    dirs = [(0, 0)] * 16
    dirs[14] = (rva, 72)                                    # the CLI header directory
    opt = struct.pack("<HBBIIIII", 0x20B, 11, 0, len(raw), 0, 0, 0, rva)
    opt += struct.pack("<QIIHHHHHHIIIIHHQQQQII", 0x180000000, 0x2000, file_align, 4, 0, 0, 0, 4, 0,
                       0, rva + (len(raw) + 0x1FFF) // 0x2000 * 0x2000, headers_size, 0, 3, 0x8540,
                       0x100000, 0x1000, 0x100000, 0x1000, 0, 16)
    opt += b"".join(struct.pack("<II", *x) for x in dirs)
    coff = struct.pack("<HHIIIHH", 0x8664, 1, 0, 0, 0, len(opt), 0x2022)
    sect = struct.pack("<8sIIIIIIHHI", b".text", len(section), rva, len(raw), headers_size,
                       0, 0, 0, 0, 0x60000020)
    dos = b"MZ" + b"\0" * 0x3A + struct.pack("<I", pe_off)
    head = dos + b"\0" * (pe_off - len(dos)) + b"PE\0\0" + coff + opt + sect
    return head + b"\0" * (headers_size - len(head)) + raw


DEBUG_MODES = b"\x01\x00\x07\x01\x00\x00\x00\x00"    # Default|IgnoreSymbolStore|EnC|DisableOptimizations
RELEASE_MODES = b"\x01\x00\x02\x00\x00\x00\x00\x00"  # IgnoreSymbolStoreSequencePoints only


class TestJitOptimization(unittest.TestCase):
    def _read(self, data: bytes):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "TAOM.dll"
            p.write_bytes(data)
            return pr.jit_optimizer_disabled(p)

    def test_debug_modes_disable_optimization(self):
        self.assertIs(self._read(_managed_dll(DEBUG_MODES)), True)

    def test_release_modes_keep_optimization(self):
        self.assertIs(self._read(_managed_dll(RELEASE_MODES)), False)

    def test_bool_constructor_with_optimizer_disabled(self):
        self.assertIs(self._read(_managed_dll(b"\x01\x00\x01\x01\x00\x00")), True)

    def test_bool_constructor_with_optimizer_enabled(self):
        self.assertIs(self._read(_managed_dll(b"\x01\x00\x01\x00\x00\x00")), False)

    def test_no_debuggable_attribute_means_optimized(self):
        self.assertIs(self._read(_managed_dll(None)), False)

    def test_bytes_without_metadata_read_as_unknown(self):
        # The --require-build tests' stand-in DLLs are exactly this shape.
        self.assertIsNone(self._read(_dll_bytes(f"{STAMP}+{SHA}")))
        self.assertIsNone(self._read(b"\0" * 300))

    def test_a_missing_file_reads_as_unknown(self):
        self.assertIsNone(pr.jit_optimizer_disabled(Path(tempfile.gettempdir()) / "no-such-035.dll"))


class TestJitReportCli(unittest.TestCase):
    def _modules(self, td, data, rel="TAOM/bin/Win64_Shipping_Client/TAOM.dll"):
        src = Path(td) / "Modules"
        dll = src / rel
        dll.parent.mkdir(parents=True, exist_ok=True)
        dll.write_bytes(data)
        return src

    def _dry(self, src, td):
        return _run("--source", str(src), "--dest", str(Path(td) / "out"), "--modules", "TAOM",
                    "--dry-run")

    def test_a_debug_build_is_reported_and_not_refused(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, _managed_dll(DEBUG_MODES)), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization OFF", r.stdout)

    def test_every_copy_gets_its_own_line(self):
        with tempfile.TemporaryDirectory() as td:
            src = self._modules(td, _managed_dll(DEBUG_MODES))
            self._modules(td, _managed_dll(RELEASE_MODES), "TAOM/bin/Win64_Shipping_Server/TAOM.dll")
            r = self._dry(src, td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization OFF", r.stdout)
            self.assertIn("TAOM/bin/Win64_Shipping_Server/TAOM.dll: JIT optimization ON", r.stdout)

    def test_an_unreadable_dll_is_reported_as_unknown(self):
        with tempfile.TemporaryDirectory() as td:
            r = self._dry(self._modules(td, b"\0" * 300), td)
            self.assertEqual(r.returncode, 0, r.stderr)
            self.assertIn("TAOM/bin/Win64_Shipping_Client/TAOM.dll: JIT optimization unknown",
                          r.stdout)
```

3. In the module docstring's "Each test maps to one part of the contract" list, after its last
   bullet (line 24, `  - --require-build: a DLL whose stamp is dirty, git-less or from another commit is refused`),
   add these two bullets:

```
  - a scene shipping its terrain shader header without its sack is refused, --dry-run included,
    unless --allow-missing-shader-cache names it; SceneObj/Backups is never judged
  - one JIT optimization line per shipped TAOM DLL copy (OFF, ON or unknown), never a refusal
```

Run `python -B -m unittest tools.tests.test_package_release`.

**Verify**: `Ran 77 tests` and `FAILED (failures=7, errors=13)`. The 13 errors are
`AttributeError` on `pr.scenes_missing_shader_cache` (6 tests in `TestSceneShaderCache`) and
`pr.jit_optimizer_disabled` (7 tests in `TestJitOptimization`); the 7 failures are the 4
`TestSceneShaderCacheCli` and 3 `TestJitReportCli` tests (exit 0 where 2 is expected, argparse's
exit 2 where 0 is expected, or a missing `JIT optimization` line). All 57 existing tests still pass.
Quote the result line in your report. Any other count is a STOP condition (an existing test broke,
or a new one passes against the old code and so guards nothing).

### Step 3: implement the gate and the report (GREEN)

Edit `tools/package_release.py`, in this order. Every edit is anchored on text quoted in Current
state.

**3a. Docstring.** Replace the line
`Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination / failed --require-build.`
and add a usage line above it, so the tail of the docstring reads:

```
  python tools/package_release.py --source "<game>/Modules" --dest D:/taom-release --require-build v2.0.31 --dry-run
  python tools/package_release.py ... --dry-run --allow-missing-shader-cache Main_map

Every run also refuses a scene that ships its terrain shader header without its compressed
shader cache (--dry-run included), and prints whether the JIT optimizes each shipped copy of
TAOM.dll and TAOM.Dependencies.dll (a report, never a refusal).

Exit codes: 0 ok · 1 nothing to do · 2 bad input / unknown entries / non-empty destination /
failed --require-build / a scene without its shader cache.
"""
```

**3b. Import.** Add `import struct` between `import shutil` and `import subprocess`.

**3c. Scene constants.** Directly after `SCENE_BACKUP_PARENTS = frozenset({"SceneObj", "SceneEditData"})`
and before `CANDIDATE_RULES`, insert:

```python

# A scene's compiled shaders live in SceneObj/<scene>/ShaderCache/D3D11/: the terrain shader
# header and the compressed shader cache (the "sack"). A scene that ships the header without the
# sack compiles its terrain shaders on the player's machine (docs/features/
# shader-precompilation.md). Vanilla ships a few header-only scenes, so this is a TAOM release
# rule, not an engine invariant: --allow-missing-shader-cache SCENE ships a named scene anyway.
SHADER_HEADER = "terrain_shaders_header_data.bin"
SHADER_SACK = "compressed_shader_cache.sack"
```

**3d. The metadata reader.** Directly after `DIRTY_FLAG_TARGET = "TaomStampWorkingTreeState"` and
before `def read_build_stamp`, insert (two blank lines before `def read_build_stamp` afterwards):

```python

# DebuggableAttribute.DebuggingModes.DisableOptimizations. A Debug build stamps 0x107 on the
# assembly (Default | IgnoreSymbolStoreSequencePoints | EnableEditAndContinue | this bit), a
# Release build 0x2 (both read off real TAOM builds, 2026-10-02).
DISABLE_OPTIMIZATIONS = 0x100

# ECMA-335 II.24.2.6: the tables a HasCustomAttribute coded index can name, in tag order.
# Assembly (0x20) is tag 14.
_HAS_CUSTOM_ATTRIBUTE = (0x06, 0x04, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x00, 0x0E, 0x17, 0x14, 0x11,
                         0x1A, 0x1B, 0x20, 0x23, 0x26, 0x27, 0x28, 0x2A, 0x2C, 0x2B)


class _NoMetadata(Exception):
    """The file is not a managed assembly this reader understands."""


def jit_optimizer_disabled(dll: Path) -> bool | None:
    """True when the assembly's DebuggableAttribute turns JIT optimization off (a Debug build),
    False when it does not or the attribute is absent, None when the file holds no readable .NET
    metadata (a native DLL, a stand-in, a truncated file). Reads the metadata tables directly
    (ECMA-335 II.24), so the assembly is never loaded."""
    try:
        return _read_jit_optimizer_disabled(Path(dll).read_bytes())
    except (OSError, _NoMetadata, struct.error, IndexError, ValueError):
        return None


def _read_jit_optimizer_disabled(d: bytes) -> bool:
    def u16(o):
        return struct.unpack_from("<H", d, o)[0]

    def u32(o):
        return struct.unpack_from("<I", d, o)[0]

    if d[:2] != b"MZ":
        raise _NoMetadata("no MZ header")
    pe = u32(0x3C)
    if d[pe:pe + 4] != b"PE\0\0":
        raise _NoMetadata("no PE signature")
    nsec, opt_size, opt = u16(pe + 6), u16(pe + 20), pe + 24
    cli_rva = u32(opt + (112 if u16(opt) == 0x20B else 96) + 14 * 8)   # data directory 14
    if cli_rva == 0:
        raise _NoMetadata("no CLI header")
    sections = [struct.unpack_from("<IIII", d, opt + opt_size + i * 40 + 8) for i in range(nsec)]

    def at(rva):
        for vsize, vaddr, rsize, roff in sections:
            if vaddr <= rva < vaddr + max(vsize, rsize):
                return roff + rva - vaddr
        raise _NoMetadata(f"RVA {rva:#x} is in no section")

    root = at(u32(at(cli_rva) + 8))
    if u32(root) != 0x424A5342:                                        # "BSJB"
        raise _NoMetadata("no metadata signature")
    p = root + 16 + u32(root + 12)                                     # past the version string
    count, p, streams = u16(p + 2), p + 4, {}
    for _ in range(count):
        end = d.index(b"\0", p + 8)
        streams[d[p + 8:end].decode("ascii")] = root + u32(p)
        p = (end + 4) & ~3
    tables = streams.get("#~", streams.get("#-"))
    if tables is None or "#Strings" not in streams or "#Blob" not in streams:
        raise _NoMetadata("no table, string or blob stream")
    heap_sizes = d[tables + 6]
    valid = struct.unpack_from("<Q", d, tables + 8)[0]
    rows, p = {}, tables + 24
    for t in range(64):
        if valid >> t & 1:
            rows[t], p = u32(p), p + 4
    str_w, guid_w, blob_w = (4 if heap_sizes & bit else 2 for bit in (1, 2, 4))

    def index_w(t):
        return 2 if rows.get(t, 0) < 1 << 16 else 4

    def coded_w(tag_bits, targets):
        return 2 if max(rows.get(t, 0) for t in targets) < 1 << (16 - tag_bits) else 4

    scope_w = coded_w(2, (0x00, 0x1A, 0x23, 0x01))          # ResolutionScope
    tdor_w = coded_w(2, (0x02, 0x01, 0x1B))                 # TypeDefOrRef
    parent_w = coded_w(3, (0x02, 0x01, 0x1A, 0x06, 0x1B))   # MemberRefParent
    const_w = coded_w(2, (0x04, 0x08, 0x17))                # HasConstant
    hca_w = coded_w(5, _HAS_CUSTOM_ATTRIBUTE)
    cat_w = coded_w(3, (0x06, 0x0A))                        # CustomAttributeType
    row_w = [
        2 + str_w + 3 * guid_w,                             # 0x00 Module
        scope_w + 2 * str_w,                                # 0x01 TypeRef
        4 + 2 * str_w + tdor_w + index_w(0x04) + index_w(0x06),  # 0x02 TypeDef
        index_w(0x04),                                      # 0x03 FieldPtr
        2 + str_w + blob_w,                                 # 0x04 Field
        index_w(0x06),                                      # 0x05 MethodPtr
        8 + str_w + blob_w + index_w(0x08),                 # 0x06 MethodDef
        index_w(0x08),                                      # 0x07 ParamPtr
        4 + str_w,                                          # 0x08 Param
        index_w(0x02) + tdor_w,                             # 0x09 InterfaceImpl
        parent_w + str_w + blob_w,                          # 0x0A MemberRef
        2 + const_w + blob_w,                               # 0x0B Constant
        hca_w + cat_w + blob_w,                             # 0x0C CustomAttribute
    ]
    start, p = [], tables + 24 + 4 * len(rows)
    for t, w in enumerate(row_w):
        start.append(p)
        p += w * rows.get(t, 0)

    def read(o, w):
        return u16(o) if w == 2 else u32(o)

    def string(i):
        o = streams["#Strings"] + i
        return d[o:d.index(b"\0", o)].decode("utf-8", "replace")

    def blob(i):
        o = streams["#Blob"] + i
        if d[o] & 0x80 == 0:
            return d[o + 1:o + 1 + d[o]]
        if d[o] & 0xC0 == 0x80:
            return d[o + 2:o + 2 + (((d[o] & 0x3F) << 8) | d[o + 1])]
        n = ((d[o] & 0x1F) << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3]
        return d[o + 4:o + 4 + n]

    for r in range(rows.get(0x0C, 0)):
        o = start[0x0C] + r * row_w[0x0C]
        parent, ctor = read(o, hca_w), read(o + hca_w, cat_w)
        if parent & 0x1F != 14 or ctor & 0x7 != 3:          # on the Assembly, via a MemberRef
            continue
        member = start[0x0A] + ((ctor >> 3) - 1) * row_w[0x0A]
        cls = read(member, parent_w)
        if cls & 0x7 != 1:                                  # declared on a TypeRef
            continue
        typeref = start[0x01] + ((cls >> 3) - 1) * row_w[0x01]
        namespace = string(read(typeref + scope_w + str_w, str_w))
        name = string(read(typeref + scope_w, str_w))
        if (namespace, name) != ("System.Diagnostics", "DebuggableAttribute"):
            continue
        value = blob(read(o + hca_w + cat_w, blob_w))
        if len(value) == 8:     # prolog 01 00, DebuggingModes as int32, no named arguments
            return bool(struct.unpack_from("<i", value, 2)[0] & DISABLE_OPTIMIZATIONS)
        if len(value) == 6:     # prolog 01 00, isJITTrackingEnabled, isJITOptimizerDisabled
            return value[3] != 0
        raise _NoMetadata(f"unexpected DebuggableAttribute value {value.hex()}")
    return False
```

**3e. Shared helpers.** Directly before `def require_build(plans, rev: str, requested=()) -> tuple:`
insert:

```python
def shipped_dll_copies(plan) -> list:
    """Module-relative path of every bin/<platform>/ copy of the TAOM assembly this module ships
    (none for a module outside SHIPPED_DLLS)."""
    dll_name = SHIPPED_DLLS.get(plan.name.casefold())
    if dll_name is None:
        return []
    return [rel for rel, _size in plan._copy_list
            if rel.casefold().startswith("bin/")
            and PurePosixPath(rel).name.casefold() == dll_name.casefold()]


def scenes_missing_shader_cache(plan) -> list:
    """Every scene this module ships with SceneObj/<scene>/ShaderCache/D3D11/ holding
    terrain_shaders_header_data.bin but no compressed_shader_cache.sack, sorted. Reads the copy
    list, so an excluded path (SceneObj/Backups) is never judged; a scene with neither file, or
    with the sack alone, passes."""
    shipped = {}
    for rel, _size in plan._copy_list:
        parts = PurePosixPath(rel).parts
        if (len(parts) == 5 and parts[0].casefold() == "sceneobj"
                and parts[2].casefold() == "shadercache" and parts[3].casefold() == "d3d11"):
            shipped.setdefault(parts[1], set()).add(parts[4].casefold())
    return sorted(scene for scene, names in shipped.items()
                  if SHADER_HEADER in names and SHADER_SACK not in names)


```

Then, inside `require_build`, replace the three-line `copies = [rel for rel, _size in p._copy_list
... == dll_name.casefold()]` expression (excerpt above) with `copies = shipped_dll_copies(p)`. Keep
the `dll_name` lookup above it: the next line still uses it for `client`.

**3f. The report.** Directly before `def main(argv=None) -> int:` insert:

```python
def _jit_report(plans) -> None:
    """One line per shipped copy of a TAOM assembly: whether the JIT optimizes it. A report:
    TAOM ships Debug builds on purpose, so this never refuses."""
    verdicts = {True: "OFF (DebuggableAttribute disables optimization: a Debug build)",
                False: "ON", None: "unknown (no readable .NET metadata)"}
    lines = [f"  {p.name}/{rel}: JIT optimization {verdicts[jit_optimizer_disabled(p.root / rel)]}"
             for p in plans for rel in shipped_dll_copies(p)]
    if lines:
        print("\nTAOM assemblies (report only, never refuses):")
        print("\n".join(lines))


```

**3g. The flag.** Between the `--allow-unknown` and `--dry-run` `add_argument` calls insert:

```python
    ap.add_argument("--allow-missing-shader-cache", action="append", default=[], metavar="SCENE",
                    help="ship this scene although it has the terrain shader header and no "
                         "compressed_shader_cache.sack (repeatable)")
```

**3h. Wiring in `main`.** Three edits:

1. After `_report(plans, args)` add the line `    _jit_report(plans)`.
2. Directly before `    if args.require_build is not None:` insert:

```python
    allowed = {s.casefold() for s in args.allow_missing_shader_cache}
    missing_cache = [f"{p.name}/SceneObj/{scene}" for p in plans
                     for scene in scenes_missing_shader_cache(p) if scene.casefold() not in allowed]
    if missing_cache:
        print(f"\nERROR: these scenes ship {SHADER_HEADER} without {SHADER_SACK}, so players "
              "would compile their terrain shaders at runtime. Restore each sack, or pass "
              "--allow-missing-shader-cache SCENE to ship it anyway:", file=sys.stderr)
        for scene in missing_cache:
            print(f"  {scene}", file=sys.stderr)

```

3. After the `--require-build` block (after its `for rel in checked: print(f"  {rel}")` loop) and
   before `    if args.json:` insert:

```python
    if missing_cache:
        return 2

```

The order matters: the scene error prints before the `--require-build` check, so one run reports
both problems; the return comes after it, and before the JSON manifest, the `--dry-run` return and
any copying, exactly like the `--require-build` refusal.

Run `python -B -m unittest tools.tests.test_package_release`.

**Verify**: `Ran 77 tests` and `OK`. Then `python -B tools/package_release.py --help` exits 0 and
lists `--allow-missing-shader-cache SCENE`.

### Step 4: document the new refusal where the procedure runs the packager

Edit only these spots; no em or en dashes.

1. `.claude/skills/release/SKILL.md`, Phase 8 step 2. Replace these three lines (148 to 150 at
   `dffdf879`):

```
   `Directory.Build.props` predates the `.dirty` flag (the 1.4.5 line until it is ported).
   It proves the DLLs only. Deploys never delete, so the install also holds files from every
   earlier deploy. Before packaging, prune only what neither the tag nor its build owns:
```

   with these ten lines (same 3-space indent):

```
   `Directory.Build.props` predates the `.dirty` flag (the 1.4.5 line until it is ported).
   Every run, `--dry-run` included, also exits 2 naming each `<module>/SceneObj/<scene>` that ships
   `ShaderCache/D3D11/terrain_shaders_header_data.bin` without `compressed_shader_cache.sack`
   beside it. Restoring the sack is Mike's editor step; `--allow-missing-shader-cache <scene>`
   ships one anyway, only on his word. The run also prints one `JIT optimization` line per shipped
   copy of `TAOM.dll` and `TAOM.Dependencies.dll`: OFF is expected (TAOM ships Debug builds on
   purpose), and that line never refuses.
   It proves the DLLs and the scene shader caches only. Deploys never delete, so the install also
   holds files from every earlier deploy. Before packaging, prune only what neither the tag nor its
   build owns:
```

2. `docs/reference/release-process.md`, step 9. Replace these two lines (90 and 91 at `dffdf879`):

```
   is ported). It proves the DLLs only. Deploys never delete, so the install also holds files from
   every earlier deploy. Before packaging, prune only what neither the tag nor its build owns:
```

   with these eight lines:

```
   is ported). Every run, `--dry-run` included, also exits 2 naming each `<module>/SceneObj/<scene>`
   that ships `ShaderCache/D3D11/terrain_shaders_header_data.bin` without
   `compressed_shader_cache.sack` beside it; restoring the sack is the maintainer's editor step, and
   `--allow-missing-shader-cache <scene>` ships one anyway on his word. The run also prints one
   `JIT optimization` line per shipped TAOM assembly copy (OFF for the Debug builds TAOM ships on
   purpose; a report, never a refusal). It proves the DLLs and the scene shader caches only.
   Deploys never delete, so the install also holds files from every earlier deploy. Before
   packaging, prune only what neither the tag nor its build owns:
```

3. `docs/modding/module-map.md`, packaging step 5 (line 485 onward). After the sentence ending
   ``Atmospheres`, `NavMeshPrefabs`, `Prefabs`, `Shaders` and `bin`.`` add, as continuation lines of
   the same list item (3-space indent):

```
   It refuses (exit 2, `--dry-run` included) a scene whose `SceneObj/<scene>/ShaderCache/D3D11/`
   holds `terrain_shaders_header_data.bin` without `compressed_shader_cache.sack`, and names it;
   `--allow-missing-shader-cache <scene>` ships one anyway.
```

Run `python tools/lint_docs.py --fail-on-drift` and
`git grep -n "proves the DLLs only" -- .claude/skills/release/SKILL.md docs/reference/release-process.md docs/modding/module-map.md`.
Keep the path limit: two historical review files
(`docs/reviews/deep-review-017-build-identity-dirty-flag-2026-09-24.md:78`,
`docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md:31`) quote the phrase and stay untouched.

**Verify**: lint exits with Step 1's code (0 expected); the path-limited grep prints nothing.

### Step 5: smoke the real installs (read only)

Both runs only read; `--dest` must name a folder that does not exist (use one under your scratch
folder), and `--dry-run` writes nothing. Each walks gigabytes, so prefix `timeout 900`.

1. `python -B tools/package_release.py --source "<game>/Modules" --dest "<scratch>/out-035" --modules TAOM TAOM_Map TAOM.Dependencies --dry-run`
   where `<game>` is the Bannerlord install the build resolves (`$BANNERLORD_GAME_DIR`, per
   `Directory.Build.props:27-38`; quote the path, it contains spaces and `&`).
   Expected at planning time: exit 2; stderr lists `TAOM_Map/SceneObj/Main_map`
   and `TAOM_Map/SceneObj/taom_rohan_edoras_town_forceatmo`; stdout has six
   `JIT optimization OFF` lines (TAOM and TAOM.Dependencies, each in
   `Gaming.Desktop.x64_Shipping_Client`, `Win64_Shipping_Client`, `Win64_Shipping_Server`). If the
   maintainer has since restored those sacks, exit 0 with no scene listed is also correct; report
   which you saw.
2. The same command with `--source "<releases>/patreon/Modules" --modules TAOM_Map TAOM`, where
   `<releases>` is the release-channel folder the skill excerpt above names for
   `<channel>\Modules\`:
   expected exit 0, no scene listed, four `TAOM/bin/.../TAOM.dll: JIT optimization OFF` lines.

**Verify**: neither `<scratch>/out-035` exists afterwards; the results match or the difference is
explained by a changed install (report the listing). If this machine has no game install, write
"Step 5 not run: no install" in the report; it is not a failure.

### Step 6: full suites, then commit

1. `python -B -m unittest discover -s tools/tests -t .` (with `timeout 900`): Step 1's failure set,
   no new names, and `Ran` exactly 20 more tests than Step 1.
2. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` then
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: build exit 0; test totals equal
   Step 1's (this plan changes no C#).
3. `git status --porcelain`: only the five in-scope paths.
4. Commit per "Git workflow".

**Verify**: `git log -1 --format=%s` shows the subject; `git show --stat HEAD` lists exactly the five
files.

## Test plan

- New tests, all in `tools/tests/test_package_release.py` (Step 2):
  - `TestSceneShaderCache` (6, unit on `scenes_missing_shader_cache`): both files ok; header without
    sack names the scene; neither ok; sack without header ok; only the header-only scenes of a mix
    are named, sorted; `SceneObj/Backups` is never judged.
  - `TestSceneShaderCacheCli` (4, through the CLI): `--dry-run` exits 2 and names the scene on
    stderr; a real run is refused before anything is written; `--allow-missing-shader-cache
    Main_map` lets that scene through; allowing one scene does not allow another.
  - `TestJitOptimization` (7, unit on `jit_optimizer_disabled`): `DebuggingModes` 0x107 is True,
    0x2 is False; the `(bool, bool)` constructor both ways; no attribute is False; stand-in and
    zero bytes are None; a missing file is None.
  - `TestJitReportCli` (3, through the CLI): a Debug DLL prints `JIT optimization OFF` and exits 0;
    each `bin/<platform>/` copy gets its own line; an unreadable DLL prints `unknown` and exits 0.
- Pattern: the existing `TestRequireBuildCli` and `TestPlan` classes in the same file (temp module
  trees, `_run` for the CLI).
- The fixture `_managed_dll` builds a real (minimal) managed PE; the plan writer confirmed that
  `System.Reflection.Metadata` reads all five fixture shapes back with the intended attribute blob.
- Not testable here: what a player's machine does with a header-only scene (named in the commit's
  `Not-tested:` trailer).

## Done criteria

ALL must hold:

- [ ] `python -B -m unittest tools.tests.test_package_release` prints `Ran 77 tests` and `OK`
- [ ] Step 2's RED result `FAILED (failures=7, errors=13)` with `Ran 77 tests` is quoted in the report
- [ ] `python -B -m unittest discover -s tools/tests -t .` shows Step 1's failure set and 20 more tests
- [ ] `dotnet build ...` exits 0 and `dotnet test ...` matches Step 1's totals
- [ ] `python tools/lint_docs.py --fail-on-drift` exits with Step 1's code
- [ ] `git grep -n "proves the DLLs only" -- .claude/skills/release/SKILL.md docs/reference/release-process.md docs/modding/module-map.md`
      prints nothing
- [ ] `git grep -l "allow-missing-shader-cache" -- tools/package_release.py tools/tests/test_package_release.py .claude/skills/release/SKILL.md docs/reference/release-process.md docs/modding/module-map.md`
      lists all five of those paths
- [ ] `git status --porcelain` is empty after the commit, and `git show --stat HEAD` lists only the
      five in-scope files
- [ ] The claims in the comments this plan supplied were re-checked: "Vanilla ships a few header-only
      scenes" (list `Native/SceneObj/__default_new_editor_scene_/ShaderCache/D3D11/` in the game
      install: a header and no sack) and "0x107" (Step 5 printed OFF for the dev install's Debug
      DLLs). Step 5 prints only TAOM's own Debug copies, so it cannot show the literal Release value
      0x2: report "0x2: UNVERIFIED by Step 5" (the `TestJitOptimization` 0x2 case covers the
      reader's ON branch). Mark any other claim you could not check UNVERIFIED in the report

## STOP conditions

Stop and report (do not improvise) if:

- The drift check shows an in-scope file changed and the Current state excerpts no longer match.
- Step 2's RED is not exactly `Ran 77 tests` with `FAILED (failures=7, errors=13)`: an existing test
  broke, or a new test passes against the old code and guards nothing.
- Step 3's GREEN fails twice after a reasonable fix, especially any `TestJitOptimization` case: do
  not rewrite the reader's offsets by guesswork.
- Step 5 shows the reader returning `unknown` for the dev install's `TAOM.dll`, or `ON` for it while
  `TAOM.dll` was built Debug: the metadata walk disagrees with a real assembly.
- Making a test pass seems to need changing an existing test's assertion, `classify`, the
  `SCENE_BACKUPS` rule or `--require-build`'s behaviour.
- The work seems to need a file outside Scope, any write to the game install or
  release-channel folder, or a build configuration change.
- The assumption "the release flow runs the packager with `--dry-run`, so the scene check must
  refuse there" turns out false. The one anchor to check is `.claude/skills/release/SKILL.md`
  Phase 8 step 3, "Mike packages through the Modding Kit editor, not Claude" (line 173 at
  `dffdf879`): STOP only if that sentence is gone at your HEAD. `docs/reference/release-process.md`
  step 9 never said it (see Maintenance notes), so its wording is not a STOP.
- The branch you were given is not this plan's own: the maintainer requires this work on a branch
  of its own, so STOP if `git branch --show-current` names a trunk (`bannerlord-1.5.x`,
  `bannerlord-1.4.5`) or your prompt says the branch also carries another plan's work. (Commits
  the orchestrator placed on your base are fine; the drift check covers them.)

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (the gate, the JIT report, the two header-only scenes found).
- No `/localize`: no player-facing text.
- No feature doc: this is release tooling; the procedure docs are updated in Step 4.
- Tell the maintainer, with the merge, that the next Phase 8 dry run will exit 2 until `Main_map`
  and `taom_rohan_edoras_town_forceatmo` carry their sacks (or he chooses to ship one with
  `--allow-missing-shader-cache`). This is the FOR-MIKE item for the run.

## After merge: the maintainer's actions

- Restore or regenerate the `compressed_shader_cache.sack` of `TAOM_Map/SceneObj/Main_map` and
  `taom_rohan_edoras_town_forceatmo` before the next release, or decide to ship either header-only
  with `--allow-missing-shader-cache <scene>`.
- Nothing else: no hooks or settings change.

## Maintenance notes

- Plain `--dry-run` size measurements (`docs/modding/modules-overview.md:195-199`) now exit 2 while a
  scene lacks its sack; the full report still prints first, so the numbers stay readable.
- `docs/features/shader-precompilation.md:130` and `:146` still say every TAOM_Map scene ships
  header-only (measured 2026-08-10); 44 of 46 carry sacks today. Worth a doc follow-up; deliberately
  not in this plan's scope.
- `docs/reference/release-process.md:87` (step 9) still says "then package without `--dry-run` (the
  skill's Phase 8)", while the skill's Phase 8 step 3 says Mike packages in the editor and Claude
  never writes a package. That contradiction predates this plan and is out of scope; worth a doc
  follow-up.
- Issue #448's module-level sacks (`<Module>/Shaders/D3D11/`) are not checked. If they ever matter,
  that is a separate rule.
- Review should probe: the order in `main` (scene error printed before `--require-build`, return 2
  after it and before the `--dry-run` return); that `scenes_missing_shader_cache` reads `_copy_list`
  and so honours every exclusion; that `_jit_report` can never raise or change the exit code; the
  reader's row widths against ECMA-335 II.22 for tables 0x00 to 0x0C.
- Deferred: adding the JIT state to the `--json` manifest (nothing reads it yet).

## Amendment (orchestrator, 2026-10-02; binding; overrides "refuse" above)

The maintainer answered on 2026-10-02 that he does not ship shader sacks because they have been
problematic for players (plans/_audit/2026-10-02-perf/DECISIONS.md D8). The packager therefore never
refuses or warns about a missing sack. The shader part of this plan becomes a report only: per packaged
scene, whether it carries `terrain_shaders_header_data.bin`, a `compressed_shader_cache.sack`, and the
sack's format version (the dword at bytes 4 to 7; every sack shipped on 2026-10-02 reads `0x0783`), plus
one summary line (scenes with a sack, without one, and any format version that differs from the
majority, which is the one case worth a WARNING line, since a stale-format sack is the risk #448
describes). Exit codes never change because of sacks. The DLL optimization report stays as planned.
Logging (D6): the report goes to the packager's normal output and its log, in full.
