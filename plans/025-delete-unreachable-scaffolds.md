# Plan 025: Delete two unreachable scaffolds (IEditorSceneAdapter, the EditorCacheRebuild path cache) and the tests and binding row that keep them alive

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> keeps that index and updates the 025 row from your report.
>
> **Where you work**: the worktree `E:/repos/taom-improve/wt-025` on branch
> `improve/025-delete-unreachable-scaffolds`, created from `a39a9c86`. If the orchestrator gave you
> a different worktree path or branch name, use those instead everywhere this plan names these.
> Every Read, Edit and Write path below is relative to that worktree (for example
> `E:/repos/taom-improve/wt-025/Main/Adapters/IEditorSceneAdapter.cs`). The main checkout
> `E:/repos/TAOM` holds another session's uncommitted work: never edit, build, stage or commit
> anything there. Run every command with the **Bash tool** and pass `timeout: 600000` to every
> `dotnet` command.
>
> **Copy every command whole, prefix included.** The Bash tool's working directory resets between
> calls, and it may reset to the main checkout `E:/repos/TAOM`. There, a `git rm` would delete
> tracked files in a shared tree and a `git commit` would commit another session's staged hunks.
> So every command in "Steps" and "Done criteria" (except the drift check and the worktree-add
> fallback, which use `git -C E:/repos/TAOM` on purpose, and the `mkdir` of absolute scratch paths)
> begins with `cd E:/repos/taom-improve/wt-025 && `. The `git grep ... a39a9c86` lines under
> "Current state" are planning evidence that reads a commit, not steps you must run. If you ever type a command yourself,
> start it the same way. Never run `git` without that prefix.
>
> The C: drive is nearly full: every `dotnet` command below also carries
> `TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp ` (Step 0 creates
> that folder). Scratch files (commit message, test logs, recorded numbers) go in
> `E:/repos/taom-improve/scratch/025/`, never in the repo, never under `C:` or `/tmp`. Commands
> that pipe `dotnet` into `tee` start with `set -o pipefail` and end with `; echo "exit=$?"`, so the
> printed `exit=` is dotnet's own exit code, not `tail`'s.
>
> **Where this plan lives**: it is untracked and absent at `a39a9c86`. Read it from the main tree
> (`E:/repos/TAOM/plans/025-delete-unreachable-scaffolds.md`, read-only) or from a copy the
> orchestrator may place in the worktree. Such a copy shows in `git status` as
> `?? plans/025-delete-unreachable-scaffolds.md`; that is expected. Never stage, commit, edit or
> delete it.
>
> **Drift check (run first, from the main tree, before you touch the worktree)**:
>
> ```bash
> git -C E:/repos/TAOM diff --stat a39a9c86..bannerlord-1.5.x -- Main/Adapters/IEditorSceneAdapter.cs Main/Features/EditorCacheRebuild/Caching Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs Main/_Module/ModuleData/configs/cache_rebuild_config.json TAOM.Tests/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs TAOM.Tests/Migration/GameAssemblies.cs docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/features/editor-cache-rebuild.md
> ```
>
> This compares the planned commit with the branch tip (running it inside a worktree made at
> `a39a9c86` would always print nothing). Empty output: go on. Any output: compare the "Current
> state" excerpts below against `git -C E:/repos/TAOM show bannerlord-1.5.x:<path>` for each listed
> file; on any mismatch, treat it as a STOP condition. `CHANGELOG.md` churns every session and is
> deliberately left out of the check (you only add an entry at its top).

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: LOW (pure deletion of code with no caller; the build and the full suite prove parity)
- **Depends on**: none. Independent of plan 022, which wires `HeroAutoAssigner`; this plan must not touch that feature.
- **Category**: tech-debt
- **Planned at**: commit `a39a9c86`, 2026-09-24 (branch `bannerlord-1.5.x`)
- **Issue**: create before implementation lands (orchestrator). If the orchestrator gives you an issue number, put `Refs #<number>` in the commit body; otherwise leave it out.
- **Maintainer decision**: decision 21 of the 2026-09-24 audit sprint (the maintainer's numbered answers to the audit). Its text: "**Wire Auto-Assign** (feature, own plan); still delete the other unreachable scaffolding", with the follow-through "a cleanup branch for `IEditorSceneAdapter`, `EditorCacheRebuild/Caching` and the reserved config fields (ARCH-02)". This plan is that cleanup branch. The reserved config fields it deletes are the two that belong to the path cache (`EnablePathReuse`, `EnablePersistentPathCache`); the other six reserved fields are deferred (see "Maintenance notes"). **Orchestrator, before dispatch:** confirm with the maintainer that "the reserved config fields" may be read as these two; the executor does not decide this and follows the plan as written.

## Why this matters

Two pieces of code have been built, tested and carried since 2026-05-12 (commit `6a80bac6`) with no caller at all. `IEditorSceneAdapter` is an adapter interface with no implementation and no reference. The `EditorCacheRebuild/Caching/` folder (a path-reuse cache and its on-disk sidecar, 6 files, 292 lines) is registered in DryIoc but never resolved or injected, so it never runs. They cost 406 lines of tests (26 test methods), two reserved config properties that nothing reads, and one row in the engine binding gate (`ReflectionSiteBindingTests`) that is mislabelled as an engine type. The project's simplicity rule (`.claude/rules/simplicity-criterion.md`) says a deletion that holds parity (tests green, behaviour unchanged) is "Always keep", and code "in case we need it later" is "Reject". After this plan the scaffold is gone, the suite and binding gate are smaller and honest, and git history (`6a80bac6`) is the recovery path if path reuse is ever actually built.

## Current state

All excerpts were read at `a39a9c86`. Line numbers are at that commit.

### Files and roles

| File | Lines | Role | Action |
|---|---|---|---|
| `Main/Adapters/IEditorSceneAdapter.cs` | 29 | Adapter interface, zero implementations, zero references | delete |
| `Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs` | 16 | Interface of the in-memory path cache | delete |
| `Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs` | 8 | Interface of the on-disk sidecar | delete |
| `Main/Features/EditorCacheRebuild/Caching/NavigationPathCloner.cs` | 16 | Clone helper used only by `PathReuseCache` | delete |
| `Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs` | 42 | In-memory cache, `ConcurrentDictionary<SortedPathKey, NavigationPath>` | delete |
| `Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs` | 160 | `.paths.bin` sidecar; reflects on `PathReuseCache._store` at line 149 | delete |
| `Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs` | 50 | Key struct used only by the two caches | delete |
| `TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs` | 62 | 4 test methods | delete |
| `TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs` | 92 | 6 test methods | delete |
| `TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs` | 169 | 8 test methods | delete |
| `TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs` | 83 | 8 test methods | delete |
| `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs` | 30 | Feature DryIoc registrations (NOT a single-owner file) | remove 1 `using` and 2 registrations |
| `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs` | 62 | JSON deserialization target | remove 2 reserved properties, trim 1 doc-comment phrase |
| `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs` | 301 | Config provider tests | drop 2 JSON keys and 2 asserts; add 2 tests |
| `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` | 163 | Binding gate, one `[DataRow]` per catalogued reflection site | remove 1 comment + 1 `[DataRow]` |
| `TAOM.Tests/Migration/GameAssemblies.cs` | n/a | Test helper; a comment names the nonexistent engine type | reword 1 comment line |
| `docs/reference/taleworlds-api-snapshot/reflection-sites.md` | 134 | Catalogue that the binding gate mirrors | remove 1 row, add 1 status line |
| `docs/features/editor-cache-rebuild.md` | 240 | Feature doc | edit 5 places, add 1 changelog bullet |
| `CHANGELOG.md` | n/a | Project changelog | add 1 entry at the top |

Commit history of every file you delete: exactly one commit,
`6a80bac6 2026-05-12 feat(EditorCacheRebuild): parallel + incremental + resumable cache builder`
(from `git log --format="%h %ad %s" --date=short a39a9c86 -- Main/Adapters/IEditorSceneAdapter.cs Main/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/Caching`).

### `Main/Adapters/IEditorSceneAdapter.cs` (whole file, 29 lines)

```csharp
using TaleWorlds.Library;

namespace TAOM.Adapters;

public interface IEditorSceneAdapter
{
    bool TryGetPath(
        int fromFace,
        int toFace,
        Vec2 fromPos,
        Vec2 toPos,
        float agentRadius,
        int[] excludedFaceIds,
        int regionSwitchCostTo0,
        int regionSwitchCostTo1,
        NavigationPath outPath);

    bool TryGetPathDistance(
        int fromFace,
        int toFace,
        Vec2 fromPos,
        Vec2 toPos,
        float agentRadius,
        float distanceLimit,
        int[] excludedFaceIds,
        int regionSwitchCostTo0,
        int regionSwitchCostTo1,
        out float distance);
}
```

`git grep -n -w -e IEditorSceneAdapter a39a9c86 -- Main TAOM.Tests tools` prints exactly one line:
`a39a9c86:Main/Adapters/IEditorSceneAdapter.cs:5:public interface IEditorSceneAdapter`.

### `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs:1-18`

```csharp
using DryIoc;
using TAOM.Features.EditorCacheRebuild.Caching;
using TAOM.Features.EditorCacheRebuild.Checkpoint;
using TAOM.Features.EditorCacheRebuild.Diff;
using TAOM.Features.EditorCacheRebuild.Phase1;
using TAOM.Features.EditorCacheRebuild.Phase2;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Features.EditorCacheRebuild;

public static class EditorCacheRebuildIoC
{
    public static void RegisterEditorCacheRebuildFeature(IContainer container)
    {
        container.Register<ICacheRebuildConfigProvider, CacheRebuildConfigProvider>(Reuse.Singleton);
        container.Register<IPathReuseCache, PathReuseCache>(Reuse.Singleton);
        container.Register<IPersistentPathCache, PersistentPathCache>(Reuse.Singleton);
        container.Register<SerialPhase1Builder>(Reuse.Singleton);
```

This method is called once, from `Main/IoC.cs:160`
(`EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);`). That call stays; you do
not edit `Main/IoC.cs`.

### The only reach into the caches: `PersistentPathCache.cs:145-149` (self-reflection on TAOM's own type)

```csharp
    private static (SortedPathKey Key, NavigationPath Path)[] ExtractEntries(IPathReuseCache source)
    {
        // PathReuseCache uses ConcurrentDictionary internally; we access via reflection on the
        // private _store field to avoid expanding IPathReuseCache's surface with enumeration.
        var storeField = typeof(PathReuseCache).GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance);
```

`git grep -l -w -e IPathReuseCache -e PathReuseCache -e IPersistentPathCache -e PersistentPathCache -e NavigationPathCloner -e SortedPathKey a39a9c86 -- Main TAOM.Tests tools` prints exactly these 14 files:

```text
Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs          (doc comments at :42 and :45 only)
Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs
Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs
Main/Features/EditorCacheRebuild/Caching/NavigationPathCloner.cs
Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs
Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs
Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs
Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs        (lines 16-17)
TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs
TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs
TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs
TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs
TAOM.Tests/Migration/GameAssemblies.cs                          (comment at :62 only)
TAOM.Tests/Migration/ReflectionSiteBindingTests.cs              (DataRow at :65)
```

Nothing in `Main/_Module/` (XML, prefabs, JSON) or `tools/` names any of them:
`git grep -n -i -e enablePathReuse -e enablePersistentPathCache -e PathReuseCache -e "paths.bin" a39a9c86 -- Main/_Module tools` prints nothing.

### `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`

Lines 3-14 (class doc comment; line 8 is the one you trim):

```csharp
/// <summary>
/// Strongly-typed deserialization target for <c>Main/_Module/ModuleData/configs/cache_rebuild_config.json</c>.
///
/// <para>
/// Some fields here are <strong>not</strong> exposed in the shipped JSON because they correspond
/// to scaffolding for unreleased phases (path-reuse v2, spatial-indexed Phase 2, multi-pass
/// DEBUG quality check, in-editor UI overlay). They retain sensible defaults so the deserializer
/// is happy and so future phases can wire them without an API change — but the JSON file is kept
/// clean to avoid misleading users with knobs that silently do nothing. Codex review on 2026-05-12
/// confirmed this user-facing debt; we strip the JSON, keep the class shape.
/// </para>
/// </summary>
```

Lines 37-49 (the two properties you delete are lines 42-46):

```csharp
    // ── Reserved fields (NOT in shipped JSON — wired in future phases) ──────────

    /// <summary>Reserved. Checkpoint cadence in Phase-1-completed-indices between writes. Currently hardcoded.</summary>
    public int CheckpointEvery { get; set; } = 20;

    /// <summary>Reserved for path-reuse v2 (Phase 12 of original design). PathReuseCache scaffolding exists in `Caching/`.</summary>
    public bool EnablePathReuse { get; set; } = true;

    /// <summary>Reserved for persistent path-cache sidecar (.paths.bin). PersistentPathCache scaffolding exists in `Caching/`.</summary>
    public bool EnablePersistentPathCache { get; set; } = true;

    /// <summary>Reserved for spatial-index-driven incremental Phase 2 (Phase 9 of original design).</summary>
    public float IncrementalSpatialRadius { get; set; } = 5.0f;
```

Readers of the two properties: `git grep -n -w -e EnablePathReuse -e EnablePersistentPathCache a39a9c86 -- Main TAOM.Tests tools` prints only `CacheRebuildConfig.cs:43`, `CacheRebuildConfig.cs:46`, `CacheRebuildConfigProviderTests.cs:106` and `:107`. `CacheRebuildConfigProvider.Validate` (lines 59-123) never mentions them.

The shipped config, `Main/_Module/ModuleData/configs/cache_rebuild_config.json` (whole file), does not name either field, so it needs no edit:

```json
{
  "enabled": true,
  "forceVanilla": false,
  "parallelism": 4,
  "enableIncremental": true,
  "incrementalMaxChanged": 30,
  "smokeTestPairs": 10,
  "smokeTestDistanceTolerance": 0.0001,
  "validationReportRelativePath": "TAOM_Map/ModuleData/DistanceCaches/last_rebuild_report.json",
  "enableCheckpoint": true,
  "checkpointRelativeDirectory": "TAOM_Map/ModuleData/DistanceCaches",
  "settlementSnapshotRelativePath": "TAOM_Map/ModuleData/DistanceCaches/settlements_snapshot.json"
}
```

The loader, `CacheRebuildConfigProvider.cs:47-48`, deserializes with no settings, so Newtonsoft's
default applies to unknown JSON keys (ignored). No `MissingMemberHandling` or
`JsonConvert.DefaultSettings` appears anywhere in `Main/` or `TAOM.Tests/`
(`git grep -n "MissingMemberHandling\|JsonConvert.DefaultSettings" a39a9c86 -- Main TAOM.Tests`
prints nothing). A new test in Step 2 proves that a config still carrying the two keys loads.

```csharp
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CacheRebuildConfig>(json) ?? new CacheRebuildConfig();
```

### `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`

Usings (lines 1-7) already include `System.Reflection`, `NSubstitute` and
`TAOM.Features.EditorCacheRebuild`. Helpers you reuse: `WriteConfig(string json)` (line 53),
the `_sut` provider built in `Setup` with a fake processor count of 8, and the `_logger`
substitute. Lines 79-117, the test you trim:

```csharp
    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""enabled"": true,
  ""forceVanilla"": false,
  ""parallelism"": 6,
  ""checkpointEvery"": 50,
  ""enablePathReuse"": false,
  ""enablePersistentPathCache"": false,
  ""enableIncremental"": false,
  ...
}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsFalse(config.ForceVanilla);
        Assert.AreEqual(6, config.Parallelism);
        Assert.AreEqual(50, config.CheckpointEvery);
        Assert.IsFalse(config.EnablePathReuse);
        Assert.IsFalse(config.EnablePersistentPathCache);
        Assert.IsFalse(config.EnableIncremental);
        ...
    }
```

### `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:64-66`

```csharp
    // --- EditorCacheRebuild path cache (PersistentPathCache.cs) ---
    [DataRow("TaleWorlds.Engine.PathReuseCache", "PathReuseCache", "_store", "Field", "PersistentPathCache.cs:149")]
    // --- EditorCacheRebuild distance-cache reflection web (NavigationCacheAdapter.cs) ---
```

This row is not an engine binding. `TaleWorlds.Engine.PathReuseCache` does not exist: during
planning, a byte scan for the string `PathReuseCache` across the 51
`bin/Win64_Shipping_Client/TaleWorlds.*.dll` files and every `Modules/*/bin/Win64_Shipping_Client/*.dll`
under `E:\Steam\steamapps\common\Mount & Blade II Bannerlord` found it in exactly one file,
`Modules/TAOM/bin/Win64_Shipping_Client/TAOM.dll`. The row passes only through the test's
simple-name fallback (`ResolveType`, lines 138-146, "Fallback: simple-name search"), which finds
TAOM's own `TAOM.Features.EditorCacheRebuild.Caching.PathReuseCache`. Deleting the row removes
no engine coverage. The `NavigationCacheAdapter` rows directly below it are real engine bindings
used by the live distance-cache rebuild: keep every one of them.

### `TAOM.Tests/Migration/GameAssemblies.cs:60-62`

```csharp
            // Eagerly load every TaleWorlds.*.dll already copied into the test bin so simple-name
            // type resolution sees the full engine surface up front (otherwise lazily-referenced
            // types like TaleWorlds.Engine's PathReuseCache aren't in GetAssemblies() until touched).
```

### `docs/reference/taleworlds-api-snapshot/reflection-sites.md`

Line 50 (Category B table row you delete):

```text
| `TaleWorlds.Engine.PathReuseCache` | `_store` | field | `PersistentPathCache.cs:149` | EditorCacheRebuild path-cache extract |
```

Line 82 is the first of the dated status lines under the Category B table and begins
`Status (2026-09-15): Patch80 seam D (#550) adds`. You insert one new status line directly above it.

### `docs/features/editor-cache-rebuild.md`

- Line 84 begins `**Reserved fields (in `CacheRebuildConfig.cs`, not in shipped JSON):** `checkpointEvery`, `enablePathReuse`, `enablePersistentPathCache`, ...` and contains an em dash.
- Lines 104-105 are the Key Files rows for `Caching/PathReuseCache.cs` and `Caching/PersistentPathCache.cs`.
- Line 119 is `` `TAOM.Tests/Features/EditorCacheRebuild/` — 103+ tests covering: `` (em dash, and a count nothing computes).
- Line 122 is `- Path cache + persistent sidecar (24 tests)`.
- Line 184 begins `**Why ~30 min and not 5 min:**` and says the scaffold "is in `Caching/PathReuseCache.cs` + `PersistentPathCache.cs`, not yet wired into the builders".
- Line 208 is `## Changelog`; line 210 is its newest bullet, `- 2026-08-10 — **v1.4.8 engine bump: verified, no code change.** ...`.

### Conventions that bind this change

- **`.claude/rules/simplicity-criterion.md`**: "Deletion that holds parity (tests green, behaviour unchanged): **Always keep**"; "Code 'in case we need it later': **Reject**". This plan is the first case.
- **AGENTS.md "Banned constructs"**: no `[Obsolete]`; delete and migrate every use in the same change. Do not mark anything obsolete; delete it.
- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): entry points (patches, GameModels, behaviors) stay under 150 lines and delegate to services. No entry point is touched here.
- **ADR-007** (`docs/adrs/007-adapter-pattern.md`): services take adapters over sealed TaleWorlds types. The adapter pattern itself is decided and stays; this plan deletes one adapter interface that nothing implements or uses, which removes no adapter any service depends on.
- **ADR-008** (`docs/adrs/008-testability-requirements.md`): services and engines need full unit coverage. The deleted tests cover only deleted code; every surviving EditorCacheRebuild service keeps its tests.
- **`.claude/rules/csharp-architecture.md` "Config Providers MUST Validate"**: every user-editable config field is validated after deserialization. The two deleted fields are bools with no validation branch in `CacheRebuildConfigProvider.Validate`, so no validation code changes.
- **AGENTS.md "Human prose"**: no em or en dash (U+2014, U+2013) in any prose you write (docs, CHANGELOG, commit body). Use commas, colons, parentheses or a new sentence. Code comments and code spans are exempt, but do not add new dashes anyway.
- **Single-owner files** `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: **not touched**. Neither project lists source files (SDK globbing: `Main/TAOM.csproj` has only `<Compile Remove="_Module\bin\**" />`, `TAOM.Tests/TAOM.Tests.csproj` has no `Compile` item), so deleting files needs no project edit. `Main/IoC.cs:160` keeps calling `RegisterEditorCacheRebuildFeature`. If the build ever seems to need an edit to one of these four files, that is a STOP condition.

### Engine facts

This plan relies on no TaleWorlds signature: it only deletes TAOM code. The one engine claim it
makes (no `TaleWorlds.Engine.PathReuseCache` type exists) was checked by the DLL byte scan quoted
above. Do not decompile anything.

## Commands you will need

The table shows the bare forms. The steps print each one in full, with the
`cd E:/repos/taom-improve/wt-025 && ` prefix and (for `dotnet`) the `TEMP=... TMP=...` prefix;
run the step's full form, never the bare one.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test (full) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `Failed: 0` |
| Test (filtered) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | exit 0, `Failed: 0` |
| Data | `python tools/validate_moduledata.py` | not required: this plan changes no ModuleData file (listed so you do not reach for another command) |
| Docs | `python tools/lint_docs.py` | report-only; see Step 7 for what to compare |

Both `-p:DisableModuleCopy=true` and `-p:ModuleId=` are required on build AND test: without them
the post-build target deploys into the game install, which may be running. Never run
`./build.ps1`. Baseline at `a39a9c86`: the full suite is **10,313 or more passed, 2 skipped, 0
failed**. Any failure you see, before or after your change, is yours to explain in your report.
Several tests skip (`Assert.Inconclusive`) when an install path is missing, so your worktree's skip
count may differ from 2: Step 0 records it as **S0**, and the end state must match S0 (none of the
deleted tests can skip).

## Scope

**In scope** (the only files you may modify, create or delete):

- Delete: `Main/Adapters/IEditorSceneAdapter.cs`
- Delete: the whole folder `Main/Features/EditorCacheRebuild/Caching/` (the 6 files listed above)
- Delete: the whole folder `TAOM.Tests/Features/EditorCacheRebuild/Caching/` (the 4 files listed above)
- Edit: `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`
- Edit: `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`
- Edit: `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`
- Edit: `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`
- Edit: `TAOM.Tests/Migration/GameAssemblies.cs` (the one comment line only)
- Edit: `docs/reference/taleworlds-api-snapshot/reflection-sites.md`
- Edit: `docs/features/editor-cache-rebuild.md`
- Edit: `CHANGELOG.md` (one new entry at the top only)

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`, `TAOM.Tests/TAOM.Tests.csproj`: single-owner or project files; nothing here needs them.
- `Main/Features/CompanionTactics/**` (including `HeroAutoAssigner`, `OOBButtonsVM`, `CompanionTacticsIoC.cs`) and `Main/_Module/GUI/PreFabs/OOBButtonsOverlay.xml`: the same audit finding named them, but the maintainer chose to wire that feature in plan 022. Do not delete or edit them.
- The six other reserved `CacheRebuildConfig` fields (`CheckpointEvery`, `IncrementalSpatialRadius`, `EnableDebugQualityCheck`, `EnableUiOverlay`, `Phase1SkipReversePathfind`, `LogVerbosity`) and their validation branches in `CacheRebuildConfigProvider.cs`: deferred (see "Maintenance notes").
- `Main/Features/EditorCacheRebuild/CacheRebuildConfigProvider.cs`, `Main/_Module/ModuleData/configs/cache_rebuild_config.json`: read only.
- `Main/Adapters/NavigationCacheAdapter.cs` and its `ReflectionSiteBindingTests` section (comment at line 66, rows 67-82): live engine bindings of the working rebuild.
- `docs/features/editor-cache-rebuild.md:195`, which cites `ReflectionSiteBindingTests.cs:65-80` (already stale at `a39a9c86`, where the rows are 67-82, and one line further off after Step 6). It sits in a dated historical section: leave it.
- `docs/reference/taleworlds-api-snapshot/README.md` (its "The 32 auxiliary" count is already stale and is not this plan's), historical records (`docs/changelog-archive/**`, `docs/reviews/**`, `plans/**`), and the older dated status lines in `reflection-sites.md` and the older changelog bullets in `editor-cache-rebuild.md`.
- `plans/README.md`: the orchestrator maintains it.

## Git workflow

- **Branch**: `improve/025-delete-unreachable-scaffolds` in the worktree `E:/repos/taom-improve/wt-025`, based on `a39a9c86`. The orchestrator normally creates both. If the worktree does not exist and the orchestrator told you to create it, run `git -C E:/repos/TAOM worktree add -b improve/025-delete-unreachable-scaffolds E:/repos/taom-improve/wt-025 a39a9c86`; otherwise STOP and report. Never create a clone or archive copy of the repo.
- **One commit** at the end (Step 8). Subject, exactly this format, 72 characters at most:
  `refactor(cache-rebuild): <version> - delete two unreachable scaffolds`, where `<version>` is the
  value of `<Version value="...">` in `Main/_Module/SubModule.xml`, copied as is (it already
  starts with `v`; at `a39a9c86` the line is `<Version value="v2.0.30" />`, giving
  `refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds`, 67 characters).
- Body wrapped at 72 columns, human prose, no em or en dashes, **no `Co-Authored-By` or any AI attribution trailer**.
- **Stage explicit paths only** (`cd E:/repos/taom-improve/wt-025 && git add <path> ...`, and the same prefix on `git rm <path> ...`). Never `git add -A`, `git add .` or `git commit -a`.
- Never push, merge, rebase, stash, reset, switch or delete branches. Never pass `--no-verify`. If a commit hook denies the commit, read its reason; fix it if the cause is in your change; if the hook is confused by the worktree, STOP and report the hook text. Known cause of confusion: `.claude/hooks/check-commit-subject-version.sh` and `check-changelog-changed.sh` `cd "${CLAUDE_PROJECT_DIR}"` (the main checkout `E:/repos/TAOM`) before reading `SubModule.xml` and the staged set, so a denial may describe the main tree's state rather than your worktree's.

## Steps

### Step 0: Confirm the worktree and record the baseline

1. Run the drift check at the top of this plan. Expected: empty output (otherwise follow its instructions).
2. `cd E:/repos/taom-improve/wt-025 && git rev-parse --short HEAD && git branch --show-current && git status --porcelain`
   Expected: `a39a9c86`, `improve/025-delete-unreachable-scaffolds`, and a clean status (the only allowed line is `?? plans/025-delete-unreachable-scaffolds.md`).
3. `mkdir -p E:/repos/taom-improve/scratch/tmp E:/repos/taom-improve/scratch/025`
4. Full suite baseline, saved to a log:
   `cd E:/repos/taom-improve/wt-025 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tee E:/repos/taom-improve/scratch/025/baseline.log | tail -5; echo "exit=$?"`
   Expected: `exit=0` and a totals line with `Failed: 0`, `Skipped: 2`, `Passed:` 10,313 or more. The `Passed:` number is **P0** and the `Skipped:` number is **S0**. A skip count other than 2 is not a STOP: keep your own S0 and name it in your report. If anything fails here (`exit=1`), record every failing test name from the log; the same set (and no new name) may fail at the end, and you explain it in your report.
5. Docs baseline: `cd E:/repos/taom-improve/wt-025 && python tools/lint_docs.py > E:/repos/taom-improve/scratch/025/lint-before.txt; grep -c -E "editor-cache-rebuild|reflection-sites" E:/repos/taom-improve/scratch/025/lint-before.txt`
   The printed count is **L0** (any number is fine here).
6. Record the three numbers in a file:
   `cd E:/repos/taom-improve/wt-025 && { grep -E "(Passed|Failed)! " E:/repos/taom-improve/scratch/025/baseline.log | tail -1; echo "L0=$(grep -c -E 'editor-cache-rebuild|reflection-sites' E:/repos/taom-improve/scratch/025/lint-before.txt)"; } > E:/repos/taom-improve/scratch/025/baseline-numbers.txt; cat E:/repos/taom-improve/scratch/025/baseline-numbers.txt`

**Verify**: step 2 printed a clean status, and step 6 printed two lines: the totals line (holding P0 and S0) and `L0=<n>`. If the totals line is missing, the test run did not finish: re-run step 4 once, then STOP.

### Step 1: Prove that nothing references the scaffold (the STOP gate)

Run each command exactly as printed and compare with the expected output exactly.

1. `cd E:/repos/taom-improve/wt-025 && git grep -n -w -e IEditorSceneAdapter -- Main TAOM.Tests tools`
   Expected, one line: `Main/Adapters/IEditorSceneAdapter.cs:5:public interface IEditorSceneAdapter`
2. `cd E:/repos/taom-improve/wt-025 && git grep -l -w -e IPathReuseCache -e PathReuseCache -e IPersistentPathCache -e PersistentPathCache -e NavigationPathCloner -e SortedPathKey -- Main TAOM.Tests tools`
   Expected: exactly the 14 files listed under "Current state" (no others).
3. `cd E:/repos/taom-improve/wt-025 && git grep -n -w -e EnablePathReuse -e EnablePersistentPathCache -- Main TAOM.Tests tools`
   Expected: exactly 4 lines, at `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs:43`, `...CacheRebuildConfig.cs:46`, `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs:106` and `...CacheRebuildConfigProviderTests.cs:107`.
4. `cd E:/repos/taom-improve/wt-025 && git grep -n -i -e enablePathReuse -e enablePersistentPathCache -e PathReuseCache -e IEditorSceneAdapter -e "paths.bin" -e "EditorCacheRebuild.Caching" -- Main/_Module tools`
   Expected: no output.
5. `cd E:/repos/taom-improve/wt-025 && git grep -n "EditorCacheRebuild.Caching" -- Main TAOM.Tests`
   Expected: only lines inside the 6 `Caching/` source files, the 4 `Caching/` test files, and `Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs:2`.
6. Count the test methods you are about to delete:
   `cd E:/repos/taom-improve/wt-025 && grep -cE "^\s*\[(TestMethod|DataTestMethod|DataRow)" TAOM.Tests/Features/EditorCacheRebuild/Caching/*.cs`
   Expected, exactly these 4 lines (26 in total):
   ```text
   TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs:4
   TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs:6
   TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs:8
   TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs:8
   ```
   Save the list of method names for your report:
   `cd E:/repos/taom-improve/wt-025 && grep -hA1 -E "^\s*\[TestMethod\]" TAOM.Tests/Features/EditorCacheRebuild/Caching/*.cs | grep -oE "void [A-Za-z_0-9]+" | tee E:/repos/taom-improve/scratch/025/deleted-tests.txt | wc -l`
   Expected: `26`.

**Verify**: all six outputs match. Any extra hit (a file, a string lookup, an XML or JSON mention) is a STOP condition: something may load the type by name.

### Step 2: RED: add the absence test and the old-config compatibility test

In `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`, directly after the
closing brace of `GetConfig_ValidJson_ParsesAllFields` (line 117 at `a39a9c86`), insert these two
tests (no new `using` is needed):

```csharp
    [TestMethod]
    public void GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields()
    {
        // A hand-edited config written before the path-reuse scaffold was deleted must keep loading.
        WriteConfig(@"{ ""parallelism"": 6, ""enablePathReuse"": false, ""enablePersistentPathCache"": false }");

        var config = _sut.GetConfig();

        Assert.AreEqual(6, config.Parallelism);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded cache_rebuild_config.json")));
    }

    [TestMethod]
    public void RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly()
    {
        // Deleted unwired in plan 025 (added in 6a80bac6). Restore it from git history only
        // together with a real caller, and delete this test in the same change.
        var taom = typeof(CacheRebuildConfig).Assembly;
        foreach (var typeName in new[]
        {
            "TAOM.Adapters.IEditorSceneAdapter",
            "TAOM.Features.EditorCacheRebuild.Caching.IPathReuseCache",
            "TAOM.Features.EditorCacheRebuild.Caching.PathReuseCache",
            "TAOM.Features.EditorCacheRebuild.Caching.IPersistentPathCache",
            "TAOM.Features.EditorCacheRebuild.Caching.PersistentPathCache",
            "TAOM.Features.EditorCacheRebuild.Caching.NavigationPathCloner",
            "TAOM.Features.EditorCacheRebuild.Caching.SortedPathKey",
        })
        {
            Assert.IsNull(taom.GetType(typeName, throwOnError: false), typeName + " is unreachable scaffolding and was deleted.");
        }

        Assert.IsNull(typeof(CacheRebuildConfig).GetProperty("EnablePathReuse"), "EnablePathReuse had no reader and was deleted.");
        Assert.IsNull(typeof(CacheRebuildConfig).GetProperty("EnablePersistentPathCache"), "EnablePersistentPathCache had no reader and was deleted.");
    }
```

Run:
`cd E:/repos/taom-improve/wt-025 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CacheRebuildConfigProviderTests" 2>&1 | tee E:/repos/taom-improve/scratch/025/red.log | tail -3; echo "exit=$?"`

Then read the failures out of the full log (the tail alone can cut the message off):
`cd E:/repos/taom-improve/wt-025 && grep -E "Failed [A-Za-z_0-9]+ \[|Assert\.[A-Za-z]+ failed" E:/repos/taom-improve/scratch/025/red.log`

Then confirm that every name the absence test checks exists today, spelled as the test spells it
(the RED run proves only the first name, because the loop stops at the first failing assert):
`cd E:/repos/taom-improve/wt-025 && git grep -h -E "^namespace |^public [a-z ]*(interface|class|struct) [A-Za-z]+|public bool (EnablePathReuse|EnablePersistentPathCache) " -- Main/Adapters/IEditorSceneAdapter.cs Main/Features/EditorCacheRebuild/Caching Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`

**Verify**:
- The run printed `exit=1` and a totals line with `Failed: 1`.
- The failure grep printed exactly one `Failed RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly [...]`
  line and the message `Assert.IsNull failed. TAOM.Adapters.IEditorSceneAdapter is unreachable scaffolding and was deleted.`
  (quote it in your report). `GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields` and
  every other test in the class pass (no other `Failed` line). If the compatibility test fails, STOP
  (the loader does not ignore unknown keys as this plan assumes).
- The name check printed lines that include `namespace TAOM.Adapters;`,
  `namespace TAOM.Features.EditorCacheRebuild.Caching;`, and a declaration for each of
  `IEditorSceneAdapter`, `IPathReuseCache`, `IPersistentPathCache`, `NavigationPathCloner`,
  `PathReuseCache`, `PersistentPathCache`, `SortedPathKey`, plus
  `public bool EnablePathReuse { get; set; } = true;` and
  `public bool EnablePersistentPathCache { get; set; } = true;`. Compare each against the strings
  in your new test character by character; any difference is a typo in the test: fix the test.

### Step 3: GREEN, part 1: delete `IEditorSceneAdapter`

`cd E:/repos/taom-improve/wt-025 && git rm Main/Adapters/IEditorSceneAdapter.cs`
Expected: `rm 'Main/Adapters/IEditorSceneAdapter.cs'`.

**Verify**: `cd E:/repos/taom-improve/wt-025 && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with `0 Error(s)`.

### Step 4: GREEN, part 2: delete the `Caching/` scaffold, its tests and its registrations

1. `cd E:/repos/taom-improve/wt-025 && git rm -r Main/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/Caching`
   Expected: 10 `rm '...'` lines (6 source, 4 test).
2. In `E:/repos/taom-improve/wt-025/Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`, delete line 2
   (`using TAOM.Features.EditorCacheRebuild.Caching;`) and lines 16-17:
   ```csharp
           container.Register<IPathReuseCache, PathReuseCache>(Reuse.Singleton);
           container.Register<IPersistentPathCache, PersistentPathCache>(Reuse.Singleton);
   ```
   Change nothing else in the file; the other 12 registrations stay in their order.

**Verify**: the build command from Step 3 exits 0 with `0 Error(s)`, and
`cd E:/repos/taom-improve/wt-025 && ls Main/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/Caching 2>&1`
reports both as missing (`No such file or directory`).

### Step 5: GREEN, part 3: delete the two reserved config properties

1. In `Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`, delete these lines (42-47 at `a39a9c86`: two summaries, two properties and the blank line after each), so that the `CheckpointEvery` property is followed by one blank line and then the `IncrementalSpatialRadius` summary:
   ```csharp
       /// <summary>Reserved for path-reuse v2 (Phase 12 of original design). PathReuseCache scaffolding exists in `Caching/`.</summary>
       public bool EnablePathReuse { get; set; } = true;

       /// <summary>Reserved for persistent path-cache sidecar (.paths.bin). PersistentPathCache scaffolding exists in `Caching/`.</summary>
       public bool EnablePersistentPathCache { get; set; } = true;

   ```
2. In the same file, line 8, replace
   `/// to scaffolding for unreleased phases (path-reuse v2, spatial-indexed Phase 2, multi-pass`
   with
   `/// to scaffolding for unreleased phases (spatial-indexed Phase 2, multi-pass`
   and leave lines 9-14 as they are.
3. In `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`, inside `GetConfig_ValidJson_ParsesAllFields`, delete the two JSON lines
   ```text
     ""enablePathReuse"": false,
     ""enablePersistentPathCache"": false,
   ```
   and the two asserts
   ```csharp
           Assert.IsFalse(config.EnablePathReuse);
           Assert.IsFalse(config.EnablePersistentPathCache);
   ```
   Leave every other line of that test unchanged.

**Verify**:
- Build command from Step 3: exit 0, `0 Error(s)`.
- `cd E:/repos/taom-improve/wt-025 && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EditorCacheRebuild"` exits 0 with `Failed: 0`; `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly` now passes (GREEN).

### Step 6: Remove the mislabelled binding row and its catalogue line

1. In `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, delete lines 64-65:
   ```csharp
       // --- EditorCacheRebuild path cache (PersistentPathCache.cs) ---
       [DataRow("TaleWorlds.Engine.PathReuseCache", "PathReuseCache", "_store", "Field", "PersistentPathCache.cs:149")]
   ```
   The next line, `// --- EditorCacheRebuild distance-cache reflection web (NavigationCacheAdapter.cs) ---`, and all its rows stay.
2. In `TAOM.Tests/Migration/GameAssemblies.cs`, replace line 62
   `            // types like TaleWorlds.Engine's PathReuseCache aren't in GetAssemblies() until touched).`
   with
   `            // engine types aren't in GetAssemblies() until touched).`
   (lines 60-61 stay as they are, so the comment still reads as one sentence).
3. In `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, delete line 50 (the `TaleWorlds.Engine.PathReuseCache` row quoted under "Current state"), then insert this line directly above the line that begins `Status (2026-09-15): Patch80 seam D (#550)`:
   ```text
   Status (2026-09-24): the `PathReuseCache._store` row is removed together with the unwired path-reuse scaffold it covered (plan 025). It was never engine reflection: no `TaleWorlds.Engine.PathReuseCache` exists, and the row resolved only through the simple-name fallback to TAOM's own class. Recover both from `6a80bac6` if path reuse is ever built.
   ```

**Verify**:
- `cd E:/repos/taom-improve/wt-025 && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ReflectionSiteBindingTests"` exits 0 with `Failed: 0` and `Skipped: 0` (a skip means the game assemblies did not load: STOP and report the diagnostics line).
- `cd E:/repos/taom-improve/wt-025 && grep -c '\[DataRow(' TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` prints `47` (48 at `a39a9c86`).
- `cd E:/repos/taom-improve/wt-025 && grep -c "PathReuseCache" TAOM.Tests/Migration/ReflectionSiteBindingTests.cs TAOM.Tests/Migration/GameAssemblies.cs` prints `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs:0` and `TAOM.Tests/Migration/GameAssemblies.cs:0`.

### Step 7: Update the feature doc and the CHANGELOG

In `docs/features/editor-cache-rebuild.md` (line numbers at `a39a9c86`; apply from the bottom up
so earlier numbers stay valid, or match on the text):

1. **Changelog section**: directly below `## Changelog` (line 208) and its blank line, above the bullet that begins `- 2026-08-10`, insert:
   ```text
   - 2026-09-24: **Deleted the unwired path-reuse scaffold (plan 025).** `Caching/` (`PathReuseCache`, `PersistentPathCache`, their interfaces, `NavigationPathCloner`, `SortedPathKey`), the reserved `enablePathReuse` and `enablePersistentPathCache` fields, their 26 tests and the mislabelled `PathReuseCache._store` binding row are gone, along with the unused `Main/Adapters/IEditorSceneAdapter.cs`. None of it was ever resolved or called, so the rebuild behaves exactly as before. Recover it from `6a80bac6` if Phase 2 path memoization is ever built.
   ```
2. **Line 184**: replace the sentence `A future optimization would memoize Phase 1's paths for Phase 2 reuse (scaffold is in `Caching/PathReuseCache.cs` + `PersistentPathCache.cs`, not yet wired into the builders).` with
   `A future optimization would memoize Phase 1's paths for Phase 2 reuse. An unwired scaffold for it (`Caching/PathReuseCache.cs`, `PersistentPathCache.cs`) was deleted in 2026-09 (plan 025) and can be recovered from commit `6a80bac6`.`
   Keep the rest of the line (`**Why ~30 min and not 5 min:** ...` before it, `That alone is a 2-3× win ...` after it) unchanged.
3. **Line 122**: delete the bullet `- Path cache + persistent sidecar (24 tests)`.
4. **Line 119**: replace `` `TAOM.Tests/Features/EditorCacheRebuild/` — 103+ tests covering: `` with `` `TAOM.Tests/Features/EditorCacheRebuild/` covers: ``.
5. **Lines 104-105**: delete the two Key Files rows that begin `` | `Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs` `` and `` | `Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs` ``.
6. **Line 84**: replace the whole line with:
   ```text
   **Reserved fields (in `CacheRebuildConfig.cs`, not in shipped JSON):** `checkpointEvery`, `incrementalSpatialRadius`, `enableDebugQualityCheck`, `enableUiOverlay`, `phase1SkipReversePathfind`, `logVerbosity`. They correspond to dropped phases (Phase 9 spatial index, Phase 13 multi-pass quality check, the UI overlay) or to concerns that are mod-wide rather than per-feature, and they will be wired into JSON when a future phase actually consumes them. The path-reuse pair (`enablePathReuse`, `enablePersistentPathCache`) was deleted with its scaffold in 2026-09 (plan 025).
   ```

In `CHANGELOG.md`: at `a39a9c86` the newest date heading is `## 2026-09-23` (line 5). If the file
has no `## 2026-09-24` heading, insert `## 2026-09-24` plus a blank line directly above
`## 2026-09-23`. Then insert this entry directly below the `## 2026-09-24` heading (after its blank
line), replacing `<P1>` and `<S1>` with the passed and skipped counts from Step 8's full run once
you have them (write the entry now with the placeholders, fill them in Step 8 before staging):

```text
### refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds

Two pieces of code that nothing ever called are gone. `Main/Adapters/IEditorSceneAdapter.cs` was
an adapter interface with no implementation and no reference. `Main/Features/EditorCacheRebuild/Caching/`
(`PathReuseCache`, `PersistentPathCache`, their interfaces, `NavigationPathCloner` and
`SortedPathKey`) was a Phase 2 path-memoization scaffold that `EditorCacheRebuildIoC` registered
but nothing resolved or injected. Both came in with `6a80bac6` on 2026-05-12 and never gained a
caller. Also removed: the reserved `EnablePathReuse` and `EnablePersistentPathCache` config
properties (never in the shipped `cache_rebuild_config.json`, never read), the 26 tests in
`TAOM.Tests/Features/EditorCacheRebuild/Caching/`, and the `ReflectionSiteBindingTests` row for
`PathReuseCache._store`, which named a `TaleWorlds.Engine.PathReuseCache` that does not exist and
only ever resolved to TAOM's own class. The distance-cache rebuild behaves exactly as before; a
hand-edited config that still carries the two keys loads as before (new test). Recover the
scaffold from `6a80bac6` if path reuse is ever built. Plan 025.

Full suite in the worktree: <P1> passed, <S1> skipped, 0 failed. Nothing smoked in game (no
runtime path changed).
```

If the `<Version>` you read in `Main/_Module/SubModule.xml` is not `v2.0.30`, use the real value
in this heading and in the commit subject.

**Verify**:
- `cd E:/repos/taom-improve/wt-025 && git grep -n -e "Caching/" -e PathReuseCache -e PersistentPathCache -- docs/features/editor-cache-rebuild.md` prints exactly 3 lines: the new reserved-fields line (it contains `enablePersistentPathCache`), the rewritten `**Why ~30 min and not 5 min:**` line, and the new `- 2026-09-24:` changelog bullet. No Key Files row matches any more.
- No new dashes in prose you wrote:
  `cd E:/repos/taom-improve/wt-025 && git diff -U0 -- docs CHANGELOG.md | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(1 for l in t.splitlines() if l.startswith('+') and not l.startswith('+++') and (chr(0x2013) in l or chr(0x2014) in l)))"` prints `0`.
- `cd E:/repos/taom-improve/wt-025 && python tools/lint_docs.py > E:/repos/taom-improve/scratch/025/lint-after.txt; grep -c -E "editor-cache-rebuild|reflection-sites" E:/repos/taom-improve/scratch/025/lint-after.txt` prints a number no greater than L0.

### Step 8: Full verification, then commit

1. Build: `cd E:/repos/taom-improve/wt-025 && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` → exit 0, `0 Error(s)`.
2. Full suite: `cd E:/repos/taom-improve/wt-025 && set -o pipefail && TEMP=E:/repos/taom-improve/scratch/tmp TMP=E:/repos/taom-improve/scratch/tmp dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tee E:/repos/taom-improve/scratch/025/after.log | tail -5; echo "exit=$?"` → `exit=0`, `Failed: 0`, `Skipped:` equal to S0 (from `baseline-numbers.txt`), and `Passed:` equal to **P0 − 25** (26 deleted test methods and 1 deleted `[DataRow]` result, 2 new tests). If it is P0 − 26 or P0 − 24, check how your MSTest run counts `[DataRow]` results against `baseline.log` and explain the difference of one in your report; any other difference is a STOP condition. Record the passed and skipped numbers as P1 and S1.
3. Fill `<P1>` and `<S1>` in the CHANGELOG entry.
4. Final reference sweep (in the worktree):
   - `cd E:/repos/taom-improve/wt-025 && git grep -l -w -e IEditorSceneAdapter -e IPathReuseCache -e PathReuseCache -e IPersistentPathCache -e PersistentPathCache -e NavigationPathCloner -e SortedPathKey -e EnablePathReuse -e EnablePersistentPathCache -- Main TAOM.Tests tools` → exactly one file, `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs` (the string literals of the new absence test).
5. Status check: `cd E:/repos/taom-improve/wt-025 && git status --porcelain --untracked-files=all | grep -v "plans/025-delete-unreachable-scaffolds.md" | cut -c4- | sort` prints exactly these 19 paths (11 deletions, 8 modifications) and nothing else:
   ```text
   CHANGELOG.md
   Main/Adapters/IEditorSceneAdapter.cs
   Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs
   Main/Features/EditorCacheRebuild/Caching/IPathReuseCache.cs
   Main/Features/EditorCacheRebuild/Caching/IPersistentPathCache.cs
   Main/Features/EditorCacheRebuild/Caching/NavigationPathCloner.cs
   Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs
   Main/Features/EditorCacheRebuild/Caching/PersistentPathCache.cs
   Main/Features/EditorCacheRebuild/Caching/SortedPathKey.cs
   Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs
   TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs
   TAOM.Tests/Features/EditorCacheRebuild/Caching/NavigationPathClonerTests.cs
   TAOM.Tests/Features/EditorCacheRebuild/Caching/PathReuseCacheTests.cs
   TAOM.Tests/Features/EditorCacheRebuild/Caching/PersistentPathCacheTests.cs
   TAOM.Tests/Features/EditorCacheRebuild/Caching/SortedPathKeyTests.cs
   TAOM.Tests/Migration/GameAssemblies.cs
   TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
   docs/features/editor-cache-rebuild.md
   docs/reference/taleworlds-api-snapshot/reflection-sites.md
   ```
   (The sort order your shell prints may differ slightly; the set must match.) Any other path is a STOP condition.
6. Stage explicit paths only:
   ```bash
   cd E:/repos/taom-improve/wt-025 && git add CHANGELOG.md Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/editor-cache-rebuild.md docs/reference/taleworlds-api-snapshot/reflection-sites.md
   ```
   (The 11 deletions are already staged by `git rm`.) Then `cd E:/repos/taom-improve/wt-025 && git diff --cached --name-status | wc -l` prints `19`.
7. Write the commit message with the Write tool to `E:/repos/taom-improve/scratch/025/commit-msg.txt` (not a heredoc), then run `cd E:/repos/taom-improve/wt-025 && git commit -F E:/repos/taom-improve/scratch/025/commit-msg.txt`. Message:
   ```text
   refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds

   IEditorSceneAdapter had no implementation and no reference. The
   EditorCacheRebuild Caching/ folder (PathReuseCache,
   PersistentPathCache, their interfaces, NavigationPathCloner,
   SortedPathKey) was registered in EditorCacheRebuildIoC but never
   resolved or injected, so it never ran. Both came in with 6a80bac6
   (2026-05-12) and never gained a caller.

   Removed with them: the two registrations, the reserved
   EnablePathReuse and EnablePersistentPathCache config properties
   (not in the shipped JSON, never read), their 26 tests, and the
   ReflectionSiteBindingTests row for PathReuseCache._store, which
   named a TaleWorlds.Engine type that does not exist and resolved
   only to TAOM's own class. A new test pins that a config still
   carrying the two keys loads; another pins the types' absence.

   Git history is the recovery path: 6a80bac6 holds the scaffold if
   Phase 2 path reuse is ever built. Plan 025, maintainer decision 21.

   Full suite: <P1> passed, <S1> skipped, 0 failed.

   Not-tested: in-game distance cache rebuild; no runtime path changed.
   ```
   Replace `<P1>`/`<S1>`; add a `Refs #<n>` line above the `Not-tested:` trailer only if the orchestrator gave you an issue number; use the real version if it is not `v2.0.30`. Keep every body line at 72 columns or less and add no other trailer.

**Verify** (each command as printed):
- `cd E:/repos/taom-improve/wt-025 && git log -1 --format=%s | awk '{print length}'` prints a number no greater than 72.
- `cd E:/repos/taom-improve/wt-025 && git log -1 --format=%B | grep -ci "co-authored-by"` prints `0`.
- `cd E:/repos/taom-improve/wt-025 && git show --stat HEAD | tail -1` reports 19 files changed.
- `cd E:/repos/taom-improve/wt-025 && git status --porcelain` shows nothing but the allowed plan copy.

## Test plan

- **New tests**, both in `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs` (the class is the structural pattern; reuse its `WriteConfig`, `_sut` and `_logger`):
  - `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly`: RED before Steps 3-5 (the types and properties exist), GREEN after. Cells: each of the 7 type names returns null from `Assembly.GetType`; each of the 2 property names returns null from `GetProperty`.
  - `GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields`: green before and after; proves the save-compat of user config files (a config carrying `enablePathReuse` / `enablePersistentPathCache` still loads, `parallelism` 6 is applied, no error is logged, the success summary is logged).
- **Trimmed test**: `GetConfig_ValidJson_ParsesAllFields` loses 2 JSON keys and 2 asserts; its other 13 asserts stay.
- **Deleted tests** (26 methods, all testing only deleted code): `NavigationPathClonerTests` (4), `PathReuseCacheTests` (6), `PersistentPathCacheTests` (8), `SortedPathKeyTests` (8), plus the `ReflectionSiteBindingTests` data row for `PathReuseCache._store`. List the 26 method names from Step 1.6 in your report.
- **Structurally untestable**: nothing new. The live rebuild path (`RuntimeCacheRebuildService` and the builders) never touched the deleted types, so no in-game check is owed; the commit's `Not-tested:` trailer says so.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (run as Step 8.2 prints it) passes with P0 − 25 passed, S0 skipped, 0 failed.

## Done criteria

Machine-checkable. ALL must hold:

Every command here is run exactly as printed, prefix included.

- [ ] The Step 8.1 build command exits 0 with `0 Error(s)`.
- [ ] The Step 8.2 full-suite command prints `exit=0`, `Failed: 0`, `Skipped:` = S0, `Passed:` = P0 − 25 (or an off-by-one explained by `[DataRow]` counting).
- [ ] `cd E:/repos/taom-improve/wt-025 && grep -E "RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly|GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields" E:/repos/taom-improve/scratch/025/after.log` prints no `Failed` line, and `cd E:/repos/taom-improve/wt-025 && git grep -c -e "public void RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly" -e "public void GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields" -- TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs` prints `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs:2`.
- [ ] `cd E:/repos/taom-improve/wt-025 && test ! -e Main/Adapters/IEditorSceneAdapter.cs && test ! -e Main/Features/EditorCacheRebuild/Caching && test ! -e TAOM.Tests/Features/EditorCacheRebuild/Caching && echo gone` prints `gone`.
- [ ] The Step 8.4 `git grep -l -w ...` sweep lists only `TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`.
- [ ] `cd E:/repos/taom-improve/wt-025 && grep -c '\[DataRow(' TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` prints `47`; `cd E:/repos/taom-improve/wt-025 && grep -c "TaleWorlds.Engine.PathReuseCache" docs/reference/taleworlds-api-snapshot/reflection-sites.md` prints `0`.
- [ ] `cd E:/repos/taom-improve/wt-025 && git diff a39a9c86 --stat -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj Directory.Build.props TAOM.Tests/TAOM.Tests.csproj Main/Features/CompanionTactics Main/_Module` prints nothing.
- [ ] `cd E:/repos/taom-improve/wt-025 && git diff -U0 a39a9c86 HEAD -- docs CHANGELOG.md | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(1 for l in t.splitlines() if l.startswith('+') and not l.startswith('+++') and (chr(0x2013) in l or chr(0x2014) in l)))"` prints `0` (the Step 7 dash check, re-run against the commit).
- [ ] `cd E:/repos/taom-improve/wt-025 && git show --stat HEAD` lists exactly the 19 in-scope paths; the subject is at most 72 characters and matches `^refactor\(cache-rebuild\): v[0-9.]+ - delete two unreachable scaffolds$`; no AI attribution trailer.
- [ ] Nothing pushed: `cd E:/repos/taom-improve/wt-025 && git rev-parse --abbrev-ref "@{u}" 2>&1` reports that no upstream is configured for the branch.

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints output and any "Current state" excerpt no longer matches the branch tip.
- Any Step 1 grep returns a hit beyond the listed ones: another file, a string-based lookup (`Type.GetType`, `AccessTools.TypeByName`, `TypeByName`), an XML, prefab, JSON or Python mention. Something could load a deleted type by name at runtime.
- `GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields` fails at Step 2 (the config loader rejects unknown keys, so deleting the properties would break a user's hand-edited config).
- The RED run at Step 2 fails in any test other than `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly`, or that test fails for a reason other than the `IEditorSceneAdapter` assertion.
- The build after Step 3, 4 or 5 fails with an error naming a file outside the in-scope list (a hidden reference), or seems to need an edit to `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props` or `TAOM.Tests/TAOM.Tests.csproj`.
- The `ReflectionSiteBindingTests` filtered run in Step 6 skips (game assemblies not loaded) or any `NavigationCacheAdapter` row fails.
- The final passed count differs from P0 − 25 by more than one, the final skipped count differs from S0, or any test fails that did not fail in `baseline.log`.
- You find you ran a `git` or file-changing command without the `cd E:/repos/taom-improve/wt-025 && ` prefix: stop at once, change nothing in `E:/repos/TAOM` (no restore, no reset, no unstage), and report the exact command and its output.
- A step's verification fails twice after a reasonable fix attempt.
- You find yourself about to touch `HeroAutoAssigner`, `OOBButtonsVM`, `CompanionTacticsIoC.cs`, the six other reserved config fields or `CacheRebuildConfigProvider.cs`.
- A commit hook denies the commit for a reason you cannot fix inside the in-scope files.

## Maintenance notes

- **Merge interaction with `improve/010-ci-on-hosted-windows`**: that branch adds one `[TestCategory(...)]` line to three of the test files this plan deletes (`NavigationPathClonerTests.cs`, `PathReuseCacheTests.cs`, `PersistentPathCacheTests.cs`) and lists all four `Caching/` test classes in its category manifest. Merging both gives a modify/delete conflict on those files: resolve it by keeping the deletion (`git rm`), and drop the manifest rows for the four deleted classes if the manifest is committed. `improve/008-binding-gate-no-silent-skips` edits `GameAssemblies.cs` at lines 16-28, 43-56 and from 108 on, and `reflection-sites.md` line 10; `improve/006-crash-capture-boot-cost` edits `reflection-sites.md` near lines 18-21 and 110-112. None overlaps this plan's `GameAssemblies.cs:62`, `reflection-sites.md:50` or new status line, so they should merge cleanly, but the merger should look.
- **Deferred: the six other reserved `CacheRebuildConfig` fields** (`CheckpointEvery`, `IncrementalSpatialRadius`, `EnableDebugQualityCheck`, `EnableUiOverlay`, `Phase1SkipReversePathfind`, `LogVerbosity`). None has a consumer outside the config class and its provider, but three of them (`CheckpointEvery`, `IncrementalSpatialRadius`, `LogVerbosity`) have validation branches in `CacheRebuildConfigProvider.Validate` (lines 71-76, 85-90, 106-115) and their own tests, so removing them changes provider code and its warnings rather than deleting an unreachable scaffold. Maintainer decision 21 says "the reserved config fields"; if the maintainer meant all eight, that is a follow-up plan of the same shape (delete the properties, the validation branches, their tests and the doc line 84 list).
- **Reviewer focus (`/deep-review`)**: that the Step 1 greps really cover every string-based lookup (the scaffold's own `typeof(PathReuseCache).GetField("_store")` goes with it); that `EditorCacheRebuildIoC` keeps its other 12 registrations in order; that the `NavigationCacheAdapter` binding rows are untouched; that the new absence test's type names are spelled exactly (a typo would make it pass vacuously; the Step 2 RED run proves only `IEditorSceneAdapter`, and the Step 2 name check compares the rest against the source by eye, so spot-check them against the deleted namespaces). Whether a permanent absence test earns its place under `simplicity-criterion.md` is a fair question for the review: it exists to give this deletion a RED step and to force a deliberate decision if the scaffold is restored. If the reviewer or maintainer rejects it, delete it; the compatibility test stays either way.
- **Known stale count left alone**: `docs/reference/taleworlds-api-snapshot/README.md:43` says "The 32 auxiliary static-engine reflection members"; the test had 48 rows at `a39a9c86` and has 47 after this plan. Fixing that count belongs to whoever next re-derives the catalogue, not to this plan. Likewise `docs/features/editor-cache-rebuild.md:195` cites `ReflectionSiteBindingTests.cs:65-80`, already stale (rows 67-82 at `a39a9c86`) and one line further off after this plan; it is a dated historical section and is left alone.
- **Recovery**: `git show 6a80bac6 -- Main/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/Caching Main/Adapters/IEditorSceneAdapter.cs` holds everything this plan deletes. If path reuse is ever built, restore it with a real caller and delete `RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly` in the same change.
