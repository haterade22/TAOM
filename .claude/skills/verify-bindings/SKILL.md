---
name: verify-bindings
description: Verify TAOM's Harmony patch, GameModel, and reflection bindings against the installed Bannerlord engine and refresh the committed API snapshot. Use after an engine update or patch change.
argument-hint: [check|refresh|full]
---

# Verify TaleWorlds API Bindings

Re-verify that TAOM's engine touchpoints still bind against the **installed** Bannerlord version, and keep the committed v1.4.5 signature snapshot in sync. This is the standing, offline form of the migration S6 smoke-test gate.

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

`BANNERLORD_GAME_DIR` (or `BANNERLORD_OVERRIDE_DIR`) must point at the install — the gate loads the SandBox/CustomBattle/StoryMode module DLLs from there. If unset, the gate self-reports `Assert.Inconclusive` (it does not falsely pass). This is an environment fact to report, not fix (see `.claude/rules/environment-failures.md`).

## Step 1 — Run the binding gate

```bash
dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true --filter "TestCategory=BindingVerification"
```

Three test classes under `TAOM.Tests/Migration/`:
- `HarmonyPatchBindingTests` — every `[HarmonyPatch]` / `TargetMethod` target resolves as Harmony resolves it.
- `GameModelOverrideBindingTests` — every `Taom*Model` is registered in `SubModule.cs`, overrides ≥1 base virtual, and doesn't shadow a base virtual without `override`.
- `ReflectionSiteBindingTests` — each catalogued auxiliary reflection member resolves.

## Step 2 — Interpret a failure (do NOT just rerun)

A red gate is a real finding — one of three classes. Fix at the source, do not silence the test:

| Failure message | Root cause | Fix |
|---|---|---|
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

Report: gate result (pass/fail with the per-finding class for any failure), whether the snapshot reproduced (`-Check` exit code), and any open punch-list items the change implicates. Update `docs/migration/TRACKING.md` and `CHANGELOG.md` if bindings were fixed.
