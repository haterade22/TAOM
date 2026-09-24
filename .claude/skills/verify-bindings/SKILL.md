---
name: verify-bindings
description: Verify TAOM's Harmony patch, GameModel, and reflection bindings against the installed Bannerlord engine and refresh the committed API snapshot. Use after an engine update or patch change.
argument-hint: [check|refresh|full]
---

# Verify TaleWorlds API Bindings

Re-verify that TAOM's engine touchpoints still bind against the **installed** Bannerlord version, and keep the committed signature snapshot in sync. This is the standing, offline form of the migration S6 smoke-test gate.

**Full background:** [docs/reference/taleworlds-api-snapshot/README.md](../../../docs/reference/taleworlds-api-snapshot/README.md).

## When to invoke

- After a Bannerlord engine update (Steam pushed a new version).
- After adding/changing a Harmony patch, a `Taom*Model` GameModel, or a string-name reflection site against an engine member.
- Before shipping migration work, as the offline half of S6 (the in-game half is the punch-list below).

## Mode selection

- `$ARGUMENTS` = `check` → run the binding gate only (Step 1).
- `$ARGUMENTS` = `refresh` → regenerate the snapshot only (Step 3).
- `$ARGUMENTS` = `full` or empty → all steps.

## Pre-flight

`BANNERLORD_GAME_DIR` (or `BANNERLORD_OVERRIDE_DIR`) points the gate at the install; when neither names a usable folder in the test process (the override needs `bin\Win64_Shipping_Client\Bannerlord.exe`, the game dir only needs to exist), it falls back to the game folder the test DLL was built against. The gate loads the SandBox/CustomBattle/StoryMode module DLLs from there. If no install resolves, or the resolved folder holds no `Bannerlord.exe`, every gate test that needs the module DLLs calls `Assert.Inconclusive` (tests that bind only against the `TaleWorlds.*.dll` copies in the test bin still run and pass), and the Step 1 command's `binding-gate.runsettings` turns each one into a failure, so the run is red. Without that file MSTest reports them as Skipped and exits 0: never quote such a run, or any run with a non-zero `Skipped:` count, as a green gate. A missing install is an environment fact to report, not fix (see `.claude/rules/environment-failures.md`).

## Step 1 — Run the binding gate

```bash
dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"
```

Three test classes under `TAOM.Tests/Migration/`:
- `HarmonyPatchBindingTests` — every `[HarmonyPatch]` / `TargetMethod` target resolves as Harmony resolves it.
- `GameModelOverrideBindingTests` — every `Taom*Model` is registered in `SubModule.cs`, overrides ≥1 base virtual, and doesn't shadow a base virtual without `override`.
- `ReflectionSiteBindingTests` — each catalogued auxiliary reflection member resolves.

## Step 2 — Interpret a failure (do NOT just rerun)

A red gate is an environment gap or a real finding. `Assert.Inconclusive failed. Game assemblies not loaded` (or `unavailable`, or `Game dir unresolved`) means no install resolved: report it (Pre-flight) and change no code. `Main/SubModule.cs not found` is the same kind of precondition: the test process ran outside a `TAOM.sln` tree, so report it and rerun from the repo, never edit code for it. `No test matches the given testcase filter` with a non-zero exit means the filter selected nothing (the runsettings' `TreatNoTestsAsError`): fix the command's category or filter, never the settings. The common finding classes are below; a failure that matches none of them is still a finding, so read its message and the test that raised it. Fix at the source, do not silence the test:

| Failure message | Root cause | Fix |
|---|---|---|
| `Only N [HarmonyPatch] types discovered` / `Only N GameModel subclasses discovered` | the game loaded but TAOM's types failed to load against it: a stale build, or a test-time variable naming a different install than the build used | rebuild (no `--no-build`), check the variables against the build's install, then `/investigate` with the loader exceptions |
| `AmbiguousMatchException` on a patch | name-only `[HarmonyPatch]` on a method overloaded anywhere in the type hierarchy | pin argument types: `[HarmonyPatch(typeof(X), "M", new[] { typeof(...) })]` (the documented HarmonyLib way) |
| target/member `did not resolve` / `not found` | engine renamed/moved/removed the member in this version | `/research` the new signature, update the patch / adapter / GameModel; if a reflection site, update both `reflection-sites.md` and the `[DataRow]` |
| GameModel `never AddModel'd` | a `Taom*Model` compiles but isn't registered | add `campaignStarter.AddModel(new TaomXModel(...))` in `Main/SubModule.cs` |

If the fix needs an unknown TaleWorlds signature, hand off to `/research` first. If it's a deeper "why did this break" question, use `/investigate`.

## Step 3 — Refresh the committed snapshot

```bash
pwsh tools/snapshot_api_surface.ps1
```

Regenerates `docs/reference/taleworlds-api-snapshot/{gamemodel-bases,patch-targets}.md` from the installed DLLs (auto-derives the type list from `TAOM.dll`). Then prove reproducibility:

```bash
pwsh tools/snapshot_api_surface.ps1 -Check   # exit 0 = committed files reproduce
```

## Step 4 — If you added a reflection site

Add a row to [reflection-sites.md](../../../docs/reference/taleworlds-api-snapshot/reflection-sites.md) Category B **and** a matching `[DataRow]` in `ReflectionSiteBindingTests.cs`. Runtime-dynamic sites (`instance.GetType()`) go in Category C (not gated) and onto the punch-list instead.

## Step 5 — In-game residue (flag, don't self-serve)

The gate is offline-only. Patch *application*, prefab visual order, ruler equipment, alliance behaviour, naval safety, and dynamic-reflection CC flow need a running game — itemized in [docs/migration/s6-runtime-punchlist.md](../../../docs/migration/s6-runtime-punchlist.md). Surface these to the user; do not claim them verified from a green gate.

## Output

Report: gate result (pass/fail with the per-finding class for any failure), the run's `Skipped:` count (it must be 0), whether the snapshot reproduced (`-Check` exit code), and any open punch-list items the change implicates. Update `docs/migration/TRACKING.md` and `CHANGELOG.md` if bindings were fixed.
