# Verification without deployment

Run from the repository root. The packet's `required_checks` comes from routed
lanes. `reviewctl` does not execute these commands: run them in an authorized
environment, capture the real output, and complete `checks.template.json`.
Read scripts before running code from an unfamiliar contributor. Never run
untrusted PR code on the personal self-hosted workstation.

| Check id | Command / evidence requirement |
| --- | --- |
| `contract` | `python tools/reviewctl.py lint` |
| `reviewctl-tests` | `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation -v` |
| `python-tests` | `python -m unittest discover -s tools/tests -t .` |
| `managed-build` | After an authorized `dotnet restore TAOM.sln`: `dotnet build TAOM.sln --no-restore -p:DisableModuleCopy=true -p:ModuleId=` |
| `managed-tests` | Only after the above solution build at this SHA: `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --verbosity normal` |
| `mcm-defaults` | `python tools/check_mcm_default_flips.py`: exit 1 when an MCM setting's default changed under an unchanged name since any release tag reachable from HEAD (rename it, or allowlist `Class.Name` with from, to and reason; a stale entry fails). |
| `api-snapshot` | After a Debug build of the tests (`dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`): `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check`. It reflects over `TAOM.Tests/bin/Debug/net472/TAOM.dll` whenever that exists, so a Release-only build leaves it reading a stale assembly. Exit 1 (`DRIFT:`) when a patch target or GameModel base changed and `docs/reference/taleworlds-api-snapshot/` was not regenerated; needs the game install. |
| `loc-orphans` | `python tools/check_orphan_loc_rows.py`: exit 1 when a registered `taom_*` row in TAOM's English strings files has no C# literal, ModuleData reader, declared composed family (`tools/loc_composed_families.json`) or reasoned allowlist entry (`tools/loc_orphan_allowlist.json`), or when a declaration matches nothing or a later row for the same vanilla key shadows an earlier one; exit 2 on an unreadable or malformed input. It skips ids not starting `taom_` (including the dotted `str_key_name.*` rows) and scans all of `Main/_Module`, GUI prefabs included. |
| `moduledata` | `python tools/validate_moduledata.py` with the actual game and required external module roots available. Record roots, versions and skipped-check output. |
| `data-gates` | `python tools/data_gates.py --all`: runs the data gates and prints PASS, FAIL or NOT RUN for each. Exit 1 when a gate failed, else 2 when one did not run (no game install, no lxml, a dotnet gate without `--with-tests`, a manual step), else 0. NOT RUN has not passed: record the gates it names. |
| `provenance` | `python tools/check_provenance.py`: exit 1 when a register row's Derivation or Status is outside its closed set, a tracked non-TAOM binary under `*/_Module/bin/*` has no `redistributed` row, or an `adopt-*.md` record is named nowhere in `docs/reference/provenance-register.md`; exit 2 when it cannot judge. A zero does not prove intent. |
| `hook-contract` | `bash tools/test_hooks.sh` under an available Bash. On Windows select Git Bash explicitly if `bash` otherwise resolves to an unavailable WSL distribution. |

Both MSBuild flags are required on build AND test. Build the solution, not just
`Main`, before testing with `--no-build`; otherwise a stale test DLL can pass.
Do not use the deploying default of `build.ps1` as a review-time check. Without
the game, skip the `managed-build` and `managed-tests` rows and run these three
commands in order, from the repository root: `dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`;
`dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter
"TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"`; and
`dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings
TAOM.Tests/binding-gate.runsettings --filter
"TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"`. The build (its implicit restore
passes `-p:TaomGameRefs=RefAsm`, so BUTR's metadata-only reference assemblies
from `GameReferences.targets` are downloaded; a restore without it downloads
none), the unit tests and the binding gate. All three use the Debug
configuration. A Debug RefAsm build lays BUTR's module assemblies out as a game
folder in `TAOM.Tests/bin/Debug/net472/refasm-game` and records that folder as
the test DLL's `TaomGameFolder` metadata, with its `TaomGameRefs` mode, so both
test commands load only the stub module assemblies: `BANNERLORD_GAME_DIR` and
`BANNERLORD_OVERRIDE_DIR` need not be unset on a machine that has the game.
Adding `-c Release` to one step and not the others tests a stale or missing DLL.
An unfiltered run executes the tests tagged `RequiresGame` on stubs and fails.
The three commands leave out the tests tagged `RequiresGame` (the unit step),
`RequiresGameIL` (the gate) and `LiveInstall` (both), so it is partial evidence.
Missing prerequisites mean not run, not passed.
Symptoms of an untagged test that needs the game: in a RefAsm run it fails with a `NullReferenceException` from a `TaleWorlds` frame (a TaleWorlds attribute
constructor run by `GetCustomAttributes` included), a `TypeInitializationException` wrapping one, a `FileNotFoundException` for a module assembly (`SandBox*`,
`StoryMode*`, `TaleWorlds.MountAndBlade.View`) or a game-bin dependency (`System.Management`, `Steamworks.NET`, `GalaxyCSharp`, `StbSharp`), or a
`ReflectionTypeLoadException` from `Assembly.GetTypes()` on TAOM's assembly (the test output does not print its `LoaderExceptions`). Tag the test; never catch the
exception. `EnsureLoaded()` returns true on the stub `refasm-game` folder, so an `if (!EnsureLoaded()) Assert.Inconclusive` guard does not keep a test off the stubs.
`RequiresGame` removes a test only from the unit step, and `RequiresGameIL` only from the gate.

Capture test totals, failures and skips. Empty discovery is not success. The
Python tool tests (`/verify` Step 2b) treat fewer than 6,500 discovered tests as broken; record skips affecting the
assigned scope as incomplete evidence. The ModuleData validator can auto-skip
engine-dependent checks without the game install: exit zero alone is insufficient.
Review the validator's documented coverage limits before asserting correctness.

## Documentation and skill checks

For changes to AI instructions or the Codex workflow skills, run:

```powershell
python tools/reviewctl.py lint
python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation -v
python tools/check_doc_graph_ratchet.py
```

The documentation tests check the required skills, metadata, onboarding entry
points and local links. The graph check is read-only unless `--update` is passed;
do not raise its baseline to hide disconnected documentation. When the local
Skill Creator skill is available, also run its `scripts/quick_validate.py` with
each `.agents/skills/taom-*/` directory as the argument. This is a local authoring
tool, not a repository dependency; it requires PyYAML in the selected Python
environment. Report a missing dependency rather than silently installing into
the user's environment. Static checks do not prove that a particular Codex session
discovered or followed a skill. Confirm discovery in a fresh client session;
launching another model to test behavior needs dispatch authorization.

## Shared-worktree test caution

Inspect fixture writes before running the full Python suite in a shared checkout.
As checked on 2026-09-11, `SettlementEconomyFloorTests` in
[`test_validate_moduledata.py`](../tools/tests/test_validate_moduledata.py)
temporarily rewrites the repository's `tools/settlement_economy_floor.json`
inside `test_floor_above_the_writer_cap_does_not_demand_the_impossible`, then
restores it in `finally`. That can collide with another editor or fail under a
read-only boundary. A `finally` block does not make shared-file writes isolated.

Use an authorized disposable checkout for that suite, with the candidate changes
actually included. A checkout of an older commit does not test uncommitted work.
For scoped documentation verification, the focused checks above avoid that test.
Do not overwrite someone else's config or escalate a fixture write to obtain a
green result. Record the full suite as not run or incomplete when isolation is
unavailable; focused success does not satisfy a full-suite packet obligation.

## Reporting limits

The standard-library validator checks record structure, SHA, check IDs, pass
status, zero exit code and a nonempty command/evidence reference. It cannot prove
execution, inspect linked CI logs, enforce command ordering, authenticate identities,
or validate claimed coverage. The maintainer must inspect those records. Use exact
commands and attach logs, environment versions, external artifact
hashes, discovery totals and skips. Keep any private logs in ignored artifacts;
redact before sharing. Never present manual inspections as process exit codes.

Native, asset and in-game assertions require appropriate review evidence beyond
these checks. `provenance` checks the register's form, not intent. There is no universal native or runtime test here, and no
new blanket gameplay smoke gate. Agree task-specific runtime expectations and
document what static checks cannot establish.

The Python tool tests (`python -m unittest discover -s tools/tests -t .`, `/verify`
Step 2b) include the review-tool and documentation tests and three lint gates (#1020): Ruff on
bug classes, ShellCheck on every `.sh`, and the Claude config lint. Those gates need Ruff, PyYAML
and ShellCheck (`docs/reference/development-machines.md` "Lint gates"); a missing tool fails its
gate, and a run with `TAOM_ALLOW_NO_RUFF`, `TAOM_ALLOW_NO_YAML` or `TAOM_ALLOW_NO_SHELLCHECK` set
reports that gate as skipped, never passed. That tests the tools; it
does not run AI reviewers, verify their identity or create a protected merge check.
