# Plan 008: Make the binding gate fail loudly instead of passing by skipping

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. When done, report your status in your final
> message: the orchestrator of this run maintains `plans/README.md` (it is
> uncommitted in the main tree, and `plans/` is not in your worktree), so do
> not edit it.
>
> **This plan file lives only in the main tree** at
> `E:\repos\TAOM\plans\008-binding-gate-no-silent-skips.md` (untracked). Read it from there; do all
> edits in your worktree.
>
> **Worktree path discipline (read before any step).** Another session has uncommitted work in the
> main tree `E:\repos\TAOM`, including `CHANGELOG.md`. You must never edit, build, test, stage or
> commit there. Your Bash working directory resets to `E:\repos\TAOM` on **every** call, so a `cd`
> in one call does not carry to the next. Therefore:
>
> - **Every Bash call** after Step 0.2 starts with `cd /e/repos/wt-008-binding-gate && `. Every
>   command in this plan is written relative to that worktree root; the prefix is implied even
>   where a step shows only the command.
> - **Every Read, Edit and Write path** is absolute and starts with `E:\repos\wt-008-binding-gate\`
>   (for example `E:\repos\wt-008-binding-gate\TAOM.Tests\Migration\GameAssemblies.cs`). A path in
>   this plan such as `TAOM.Tests/Migration/GameAssemblies.cs` means that file inside the worktree.
> - The only `E:\repos\TAOM` paths you may touch are this plan file (read only) and the drift check
>   below (read only).
> - Logs and the commit message go in `E:\repos\wt-008-logs\` (Bash: `/e/repos/wt-008-logs`), which
>   is outside every git tree. Never put scratch files inside the worktree.
>
> **Drift check (run first, from `E:\repos\TAOM`, read only)**:
> `cd /e/repos/TAOM && git diff --stat b2e387db..HEAD -- TAOM.Tests/TAOM.Tests.csproj TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/Migration/HarmonyPatchBindingTests.cs TAOM.Tests/Migration/GameModelOverrideBindingTests.cs .claude/hooks/notify-test-results.sh tools/test_hooks.sh .claude/skills/verify-bindings/SKILL.md docs/reference/taleworlds-api-snapshot/README.md docs/ai-includes/agent-operating-manual.md docs/migration/s6-runtime-punchlist.md docs/reference/hooks-catalog.md .github/workflows/build.yml`
> Expected: no output (verified empty at `4b5662b2` on 2026-09-23). `CHANGELOG.md` is also in scope
> but changes every session; it is not a drift signal. If any listed file changed since this plan was
> written, compare the "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: tests
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: the orchestrator files it before dispatching this plan (AGENTS.md: the issue exists
  before implementation). Executors do not create issues.

## Why this matters

`TestCategory=BindingVerification` (368 tests at `b2e387db`) is the gate every Bannerlord engine bump
relies on: it proves each Harmony patch target, GameModel registration and reflection site still
binds against the installed engine. When the test process cannot find the game it calls
`Assert.Inconclusive`, and MSTest reports Inconclusive as **Skipped with exit code 0**. The audit
measured it on 2026-09-23: the same prebuilt test DLL, run without `BANNERLORD_GAME_DIR`, printed
`Passed!  - Failed: 0, Passed: 33, Skipped: 335, Total: 368` and exited green; the Claude
test-results hook then prints `PASSED (33 tests)`, and the verify-bindings skill tells every agent
the gate "does not falsely pass". The trigger is any test process that lacks the environment
variable while the build had the game: an IDE runner, `dotnet test --no-build` from a fresh shell,
`dotnet test -p:BANNERLORD_GAME_DIR=...`, or a CI step that sets it only on the build. The same
mapping hides the gate's own "discovery floor" guards (fewer than 30 patch types or 20 GameModels
means TAOM types failed to load, the exact failure an engine bump produces). After this plan: the
gate finds the game the build compiled against, the gate's documented command turns any skip into a
failure, the floors fail, and the hook names skipped tests. This closes the binding-gate half of the
open item "`Assert.Inconclusive` is a pass" in `docs/reviews/rca-lord-identity-2026-08-29.md:77-80`
(the other half, `LordFamilyTransformTests` and the default suite, stays open).

**Decided, keep (do NOT overturn):** in the *default* suite a test skips, never fails, when the game
or the Armory is absent. This is recorded in `TAOM.Tests/Migration/GameAssemblies.cs:19-21` and in
`docs/reviews/lessons/data-content-cultures.md:1588-1591` ("it reads the live file and skips, never
fails, when the file is absent", 2026-09-23). Therefore `MapInconclusiveToFailed` must apply **only**
to runs that pass the new settings file explicitly. Never set it globally (no `RunSettingsFilePath`
in any project or props file, no file named `.runsettings` at the repo or solution root).

## Current state

All excerpts are from `git show b2e387db:<path>`.

### Files and their roles

- `TAOM.Tests/Migration/GameAssemblies.cs` (132 lines): loads SandBox, SandBoxCore, CustomBattle,
  StoryMode and Native module DLLs into the test AppDomain for the binding suite. Resolves the game
  folder from environment variables only. Used by 72 test files through `EnsureLoaded()`
  (`git grep -l "GameAssemblies.EnsureLoaded" b2e387db -- TAOM.Tests`).
- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`: the Harmony target gate; has the patch-type
  discovery floor.
- `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`: the GameModel gate; has two model-count
  discovery floors.
- `TAOM.Tests/TAOM.Tests.csproj` (42 lines, tab-indented): the test project. MSTest 3.1.1.
- `Directory.Build.props` (single-owner; NOT edited): computes `$(GameFolder)` for every project;
  sets `<Nullable>enable</Nullable>` (`:6`) and `LangVersion 10.0`, with no warnings-as-errors.
- `.claude/hooks/notify-test-results.sh`: PostToolUse(Bash) hook that prints a
  `=== TEST RESULTS ... ===` banner on stderr after any `dotnet test`. Registered in
  `.claude/settings.json:141-146` with `"timeout": 5`.
- `docs/reference/hooks-catalog.md:42`: the catalog row for that hook; it describes the banner as
  `TEST RESULTS: PASSED/FAILED`, which goes stale once Step 8 adds a third form.
- `tools/test_hooks.sh` (825 lines): the hook contract test; sections numbered `1.` to `8.`. It
  finds the repo from its own location (`:26`, `REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"`),
  so running the worktree's copy tests the worktree's hooks.
- `.claude/skills/verify-bindings/SKILL.md`: the skill every agent follows to run the gate.
- `.github/workflows/build.yml` (278 lines): the only CI job that compiles C#. Triggers (`:3-8`):
  `push` and `pull_request` on `bannerlord-1.4.5`, plus `workflow_dispatch` (a manual run on any
  ref). The C# job is self-hosted and skipped on `pull_request` (`:259`).

### How the game folder is resolved today

`Directory.Build.props:37-38` (MSBuild properties, so an environment variable OR a `-p:` global
property satisfies them):

```xml
<GameFolder Condition="'$(BANNERLORD_OVERRIDE_DIR)' != '' AND Exists('$(BANNERLORD_OVERRIDE_DIR)\bin\Win64_Shipping_Client\Bannerlord.exe')">$(BANNERLORD_OVERRIDE_DIR)</GameFolder>
<GameFolder Condition="'$(GameFolder)' == ''">$(BANNERLORD_GAME_DIR)</GameFolder>
```

`TAOM.Tests/TAOM.Tests.csproj:34-42` (the TaleWorlds references need `$(GameFolder)`, so any test DLL
that compiled had a resolved `GameFolder`):

```xml
	<ItemGroup>
		<Reference Include="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.*.dll" Exclude="$(GameFolder)\bin\$(GameBinariesFolder)\TaleWorlds.Native.dll">
			<HintPath>%(Identity)</HintPath>
			<Private>True</Private>
		</Reference>
	</ItemGroup>


</Project>
```

`TAOM.Tests/Migration/GameAssemblies.cs:19-21` (doc comment; its "mirrors" claim covers only the
environment-variable half):

```csharp
/// Game dir resolution mirrors Directory.Build.props: BANNERLORD_OVERRIDE_DIR (if its bin holds
/// Bannerlord.exe) else BANNERLORD_GAME_DIR. If neither resolves, <see cref="EnsureLoaded"/> returns
/// false and the binding tests should Assert.Inconclusive (e.g. CI without a game install).
```

`GameAssemblies.cs:27-28` (the existing properties are declared without `?` even though they hold
null; the file predates nullable annotations):

```csharp
    public static string GameDir { get; private set; }
    public static string BinFolder { get; private set; }
```

`GameAssemblies.cs:43-48`:

```csharp
            GameDir = ResolveGameDir();
            if (GameDir == null)
            {
                Diagnostics.Add("Game dir unresolved — BANNERLORD_OVERRIDE_DIR/BANNERLORD_GAME_DIR unset or invalid.");
                return false;
            }
```

`GameAssemblies.cs:50-56` (after a folder resolves, it must hold `bin\<folder>\Bannerlord.exe`, or
`EnsureLoaded` returns false):

```csharp
            BinFolder = ResolveBinFolder(GameDir);
            if (BinFolder == null)
            {
                Diagnostics.Add($"No bin folder with Bannerlord.exe under '{GameDir}'.");
                GameDir = null;
                return false;
            }
```

`GameAssemblies.cs:111-122`:

```csharp
    private static string ResolveGameDir()
    {
        var over = Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR");
        if (!string.IsNullOrEmpty(over) &&
            File.Exists(Path.Combine(over, "bin", "Win64_Shipping_Client", "Bannerlord.exe")))
            return over;

        var game = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        if (!string.IsNullOrEmpty(game) && Directory.Exists(game)) return game;

        return null;
    }
```

The file's usings (`:1-5`) are `System`, `System.Collections.Generic`, `System.IO`, `System.Linq`,
`System.Reflection`. The class is `internal static class GameAssemblies` in namespace
`TAOM.Tests.Migration`, so tests in the same assembly can call its `internal` members.

### The gate's Inconclusive calls

`HarmonyPatchBindingTests.cs:32-33` and `:41-50`:

```csharp
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();
...
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var patchTypes = DiscoverPatchTypes(out var typesFailedToLoad);

        if (patchTypes.Count < 30)
            Assert.Inconclusive(
                $"Only {patchTypes.Count} [HarmonyPatch] types discovered (expected ~70). " +
                $"{typesFailedToLoad} type(s) failed to load. This indicates an assembly-load problem, " +
                "not a genuine pass — investigate before trusting this gate.");
```

`GameModelOverrideBindingTests.cs:48-57` and `:75-80`:

```csharp
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        if (models.Count < 20)
            Assert.Inconclusive($"Only {models.Count} GameModel subclasses discovered (expected ~37) — assembly-load problem, not a genuine pass.");

        var subModule = ReadRepoFile("Main", "SubModule.cs");
        if (subModule == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root.");
...
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var models = DiscoverGameModels();
        if (models.Count < 20)
            Assert.Inconclusive($"Only {models.Count} GameModel subclasses discovered — assembly-load problem.");
```

The three **discovery floors** (`HarmonyPatchBindingTests.cs:46-50`, `GameModelOverrideBindingTests.cs:52-53`
and `:79-80`) run only after `_gameLoaded` is true, i.e. only when the game folder DID resolve. They
are the only non-environment Inconclusive floors in any `BindingVerification` file (checked with a
per-file grep at `b2e387db`). The "game not loaded" calls (`:41-42`, `:48-49`, `:75-76`) and the
`SubModule.cs not found` locator call (`:56-57`) stay Inconclusive: they are the decided per-test skip.
At `b2e387db` the Harmony file has 2 `Assert.Inconclusive` lines and the GameModel file has 5.

### MSTest behaviour (measured by the audit, 2026-09-23, not re-run while planning)

- No runsettings exists: `git grep -n -i runsetting b2e387db` returns nothing.
- Default MSTest 3.1.1 maps Inconclusive to Skipped; exit 0 (`Passed!  - Failed: 0, Passed: 33,
  Skipped: 335, Total: 368`). The trx records each as `outcome="NotExecuted"` with `Counters
  inconclusive="0"`, so the trx does not help a reader either.
- The same run with a settings file holding
  `<RunSettings><MSTest><MapInconclusiveToFailed>true</MapInconclusiveToFailed></MSTest></RunSettings>`
  printed `Failed!  - Failed: 335, Passed: 33, Skipped: 0`, each failure reading
  `Assert.Inconclusive failed. Game assemblies not loaded: Game dir unresolved ...`. MSTest.TestAdapter
  3.1.1 honours the setting. Those runs used `dotnet vstest` on a prebuilt DLL; this plan uses
  `dotnet test --settings`, which passes the same file to the same adapter (Step 6 proves it).
- With the game present (environment set, full suite), the only not-executed results were the two
  `[Ignore]` Warg tests and `DependenciesPairingTests.BuildOutputUnsafe_WhenPresent_CannotRegressTheDeployedPair`;
  none is in `BindingVerification`, and no `BindingVerification` test carries `[Ignore]`. So a strict
  gate run on the desktop is expected to be fully green.
- **Counts are space-padded.** `dotnet test` prints the summary as, for example,
  `Passed!  - Failed:     0, Passed:   368, Skipped:     0, Total:   368, Duration: ...`. Never grep a
  literal `Failed: 0`; every count check in this plan uses a regex such as `grep -E 'Failed:\s+0,'`.

### The hook today

`.claude/hooks/notify-test-results.sh:45-60`:

```bash
  # Branch on the COUNT, not on the word. `dotnet test` prints "Failed: 0, Passed: 6380"
  # on a fully green run, so a substring test for "Failed" reports every passing suite as
  # FAILED. That went unnoticed because the hook was inert until the jq fix above; it
  # would have mislabelled every green run the moment it started working.
  FAILED=$(echo "$RESPONSE" | grep -oP 'Failed:\s*\K[0-9]+' | head -1)
  PASSED=$(echo "$RESPONSE" | grep -oP 'Passed:\s*\K[0-9]+' | head -1)
  if [[ -n "$FAILED" && "$FAILED" -gt 0 ]]; then
    echo "=== TEST RESULTS: FAILED (Failed: ${FAILED}, Passed: ${PASSED:-?}) ===" >&2
  elif [[ -n "$PASSED" ]]; then
    echo "=== TEST RESULTS: PASSED (${PASSED} tests) ===" >&2
  elif echo "$RESPONSE" | grep -q "Failed"; then
    # Counts unavailable (a crash or a truncated response) but the word is there: say so
    # rather than staying silent, which would read as "no test run happened".
    echo "=== TEST RESULTS: FAILED (counts unavailable) ===" >&2
  fi
fi
```

Per-test lines in `dotnet test` output read `  Skipped TestName [1 ms]` (no colon), so a
`Skipped:\s*` pattern matches only the summary line.

`docs/reference/hooks-catalog.md:42` (one table row):

```text
| `notify-test-results.sh` | PostToolUse (Bash) | Summarizes `dotnet test` results prominently to stderr (`TEST RESULTS: PASSED/FAILED` with counts) |
```

`tools/test_hooks.sh` has no behavioural test for this hook: it is only exercised by the generic
contract loop (section `4.`, exit code in {0,2}, no hang) and the starved-environment loop (`5.`).
Section `7b.` (lines 770-797) ends just before the `8.` header block (the `# ----` line at `:799`
preceding `head2 "8. /context-budget scan.sh runs under set -u and measures the launch load"` at
`:800`). The helpers `ok`, `bad`, `head2` and the variables `$REPO` and `$SANDBOX` (created in section
`4.`, removed by an EXIT trap) are in scope for any new section placed there. Its Summary prints
`  N passed, M failed` (`:819`); exit 0 means all checks pass, exit 1 means at least one failed.
Model the new section on `7.` / `7b.`.

### The skill and docs that state the gate command

- `.claude/skills/verify-bindings/SKILL.md:27` (verbatim, one line in the file):

  ```text
  `BANNERLORD_GAME_DIR` (or `BANNERLORD_OVERRIDE_DIR`) must point at the install — the gate loads the SandBox/CustomBattle/StoryMode module DLLs from there. If unset, the gate self-reports `Assert.Inconclusive` (it does not falsely pass). This is an environment fact to report, not fix (see `.claude/rules/environment-failures.md`).
  ```

  The parenthesis "(it does not falsely pass)" is false for every invocation listed in "Why this
  matters".
- `SKILL.md:32` (inside a ```` ```bash ```` block):
  `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true --filter "TestCategory=BindingVerification"`
- `SKILL.md:74`: "Report: gate result (pass/fail with the per-finding class for any failure), whether
  the snapshot reproduced (`-Check` exit code), and any open punch-list items the change implicates.
  Update `docs/migration/TRACKING.md` and `CHANGELOG.md` if bindings were fixed."
- `docs/reference/taleworlds-api-snapshot/README.md:46`:
  `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"` (in a bash block)
- `docs/reference/taleworlds-api-snapshot/README.md:66`: "... then `dotnet test --filter "TestCategory=BindingVerification"`."
- `docs/ai-includes/agent-operating-manual.md:43`:
  `| Engine-binding gate | \`dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"\` | Verifies patch/GameModel/reflection bindings resolve against the installed engine. |`
- `docs/migration/s6-runtime-punchlist.md:13`: "Run before touching the items below: `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"`."

Historical records (`docs/changelog-archive/`, `docs/migration/api-diff-v1.4.5-hotfix-2026-05-30.md`)
also quote the old command; they describe past runs and are NOT edited.

### CI today

`.github/workflows/build.yml:259-278` (self-hosted job; the environment variable is set at job level,
so build and test share it; `:278` is the last line of the file):

```yaml
    if: ${{ vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request' }}
    env:
      BANNERLORD_GAME_DIR: ${{ vars.BANNERLORD_GAME_DIR }}
    steps:
...
      - name: Build
        run: dotnet build TAOM.sln -c Release --no-restore -p:DisableModuleCopy=true -p:ModuleId=

      - name: Test
        run: dotnet test TAOM.Tests -c Release --no-build -p:DisableModuleCopy=true -p:ModuleId= --verbosity normal
```

Pushes to `bannerlord-1.5.x` do not trigger this workflow, so the new step runs on this branch only
when someone starts the workflow by hand (`workflow_dispatch`). It is still added so the file does
not have to be revisited, and so a port to `bannerlord-1.4.5` carries it.

### Commit gates read the MAIN tree, not your worktree

The repo's PreToolUse commit gates (`.claude/hooks/check-changelog-changed.sh:44`,
`check-commit-subject-version.sh:59`, `check-claude-files-tracked.sh:50`,
`check-moduledata-validation.sh:55`) all run `cd "${CLAUDE_PROJECT_DIR}"`, which is the main tree
`E:\repos\TAOM`, and then inspect **that** tree's index and files. A gate on your worktree commit can
therefore allow or deny based on the other session's state. Never try to satisfy a deny by touching
the main tree (see STOP conditions).

### BCL fact relied on

`System.Reflection.AssemblyMetadataAttribute(string key, string value)` with properties `Key` and
`Value` exists in .NET Framework 4.7.2 `mscorlib` (read in
`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.xml`,
members `M:System.Reflection.AssemblyMetadataAttribute.#ctor(System.String,System.String)`,
`P:...Key`, `P:...Value`). SDK-style projects generate `AssemblyInfo` from `AssemblyAttribute` items
by default, and neither `TAOM.Tests.csproj` nor `Directory.Build.props` disables that. The repo
already uses the mechanism: `Main/TAOM.csproj:119-124` emits `InternalsVisibleTo` this way:

```xml
		<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
			<_Parameter1>TAOM.Tests</_Parameter1>
		</AssemblyAttribute>
```

No TaleWorlds API is touched by this plan, so no engine signature is involved.

### Binding rules (the executor has not read them)

- **ADR-008 (testability):** tests must run without game framework initialisation. The new
  resolution tests use only temp folders; keep them that way (no TaleWorlds type, no real install).
- **`.claude/rules/tests.md`:** TDD (RED, GREEN, REFACTOR, verify RED before writing the fix); names
  are `MethodName_StateUnderTest_ExpectedBehavior`; MSTest `[TestClass]`/`[TestMethod]`/
  `[TestInitialize]`/`[TestCleanup]`; tests mirror the source folder (`GameAssemblies.cs` lives in
  `TAOM.Tests/Migration/`, so its tests do too). Temp-folder exemplar:
  `TAOM.Tests/Core/Logging/FileLoggerTests.cs:32-49` (`Path.Combine(Path.GetTempPath(), "TAOM_..." + Path.GetRandomFileName())`,
  deleted in `[TestCleanup]` inside a try/catch).
- **Nullable:** `<Nullable>enable</Nullable>` applies to the test project. New members that can hold
  null are declared `string?`; the existing `GameDir`/`BinFolder` declarations are not changed.
  Nullable warnings are warnings, not errors (no warnings-as-errors).
- **ADR-003 / ADR-004 / ADR-005:** no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **ADR-002 (entry points under 150 lines) and ADR-007 (adapters for sealed TaleWorlds types):**
  not engaged; this plan touches no entry point, service or adapter in `Main/`.
- **`.claude/rules/hook-authoring.md`:** match the sibling hook's conventions (keep the existing
  `INPUT=$(cat)` preamble, `exit 0` at the end, output only on stderr). Hooks fail open by mandate.
- **Prose rule (AGENTS.md "Human prose"):** no em or en dash (U+2014, U+2013) in any new prose line
  (markdown, comments, CHANGELOG, commit body). Use a comma, colon, semicolon or parentheses. Code
  spans and existing string literals are exempt.
- **XML comments cannot contain `--`.** Do not write `--settings` or `--filter` inside the
  runsettings file's `<!-- -->` comment; the file would not parse.

### Baseline you must not chase

At `b2e387db` the orchestrator measured the full suite at 10,239 tests: 10,235 pass, 2 ignored
(`WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`, deliberate
`[Ignore]`), and **2 fail**: `TheElkItem_DeclaresTheScaleTheReachIsTunedFor`
(`TAOM.Tests/Features/Elk/ElkConfigTests.cs`) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`
(`TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs`). Both assert on the live, unversioned
`LOTRLOME_Armory`, which another session is editing. They are not caused by this plan, are not
`BindingVerification`, and must not be investigated or touched. Because the Armory is live, the
failure set can shift before you run; Step 0.6 records the set your worktree actually sees, and
Step 12 compares against that record, not against these names.

## Commands you will need

Run from the worktree root (prefix every Bash call with `cd /e/repos/wt-008-binding-gate && `).

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test (full) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | only the failures recorded in Step 0.6 |
| Test (filtered) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameAssembliesResolutionTests"` | per step |
| Strict gate | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | exit 0; summary matches `Failed:\s+0,` and `Skipped:\s+0,` |
| Hook tests | `bash tools/test_hooks.sh` | exit 0, Summary `0 failed` |
| Docs | `python tools/lint_docs.py --summary` | always exits 0 (it gates only with `--fail-*` flags), so compare its `dead_links:` and `ai_dashes:` counts to the Step 0.7 record |
| Data | `python tools/validate_moduledata.py` | same exit code and final line as Step 0.7 (this plan touches no ModuleData) |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no finding naming `notify-test-results.sh` |

Never run `./build.ps1` (it deploys into the game install). Every `dotnet` command carries
`-p:DisableModuleCopy=true -p:ModuleId=` (both are needed; the game may be running). Any `dotnet test`
whose output may exceed a few hundred lines is redirected to a log under `/e/repos/wt-008-logs/`
and read with `grep`, because tool output is truncated at about 30 KB and the summary line is last.

## Scope

**In scope** (the only files you may modify or create; 15 paths, all inside the worktree):

- `TAOM.Tests/Migration/GameAssembliesResolutionTests.cs` (new)
- `TAOM.Tests/Migration/GameAssemblies.cs` (resolution fallback, doc comment, one diagnostic string)
- `TAOM.Tests/TAOM.Tests.csproj` (one new `ItemGroup`)
- `TAOM.Tests/binding-gate.runsettings` (new)
- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (the floor at `:46-50` only)
- `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs` (the floors at `:52-53` and `:79-80` only)
- `tools/test_hooks.sh` (new section `7c.`)
- `.claude/hooks/notify-test-results.sh` (the banner block `:45-59`)
- `docs/reference/hooks-catalog.md` (line 42 only)
- `.claude/skills/verify-bindings/SKILL.md` (lines 27, 32, 74)
- `docs/reference/taleworlds-api-snapshot/README.md` (lines 46, 66)
- `docs/ai-includes/agent-operating-manual.md` (line 43)
- `docs/migration/s6-runtime-punchlist.md` (line 13)
- `.github/workflows/build.yml` (one new step after `Test`)
- `CHANGELOG.md` (one entry, **the worktree's copy only**)

**Out of scope** (do NOT touch, even though they look related):

- Anything under `E:\repos\TAOM` (the main tree), including its `CHANGELOG.md`.
- `Directory.Build.props`, `Main/TAOM.csproj`, `Main/IoC.cs`, `Main/SubModule.cs`: single-owner.
  Nothing in this plan needs them. If you believe one must change, STOP and report the exact line.
- `build.ps1`: `-RunTests` keeps running the default suite (the decided skip). Not changed here.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: another session has uncommitted edits to it
  in the main tree. Do not open it for editing.
- Any global Inconclusive mapping: `RunSettingsFilePath`, a `.runsettings` file at the repo root,
  or a props change. Forbidden (see "Decided, keep").
- Every other `Assert.Inconclusive` in `TAOM.Tests` (about 96 files): the decided per-test skip, and
  the locator copies belong to a different plan. That includes `GameModelOverrideBindingTests.cs:56-57`.
- `LordFamilyTransformTests`, `docs/reviews/rca-lord-identity-2026-08-29.md`, and every
  `docs/reviews/lessons/*.md` file (the orchestrator records lessons).
- `.claude/settings.json` (the hook registration and its 5 s timeout stay as they are).

## Git workflow

- Create a worktree from the main tree (another session has uncommitted work there; never stash,
  reset or commit it):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-008-binding-gate -b plan-008-binding-gate HEAD`
  and work only inside `E:/repos/wt-008-binding-gate` (see "Worktree path discipline").
- Commit once, at the end (Step 12), on `plan-008-binding-gate`. Never push, never open a PR, never
  merge. The orchestrator runs `/deep-review` on the branch before it merges; review fixes land as a
  new commit.
- Subject format: `<type>(<scope>): v<Version> - <description>`, where `<Version>` is the `value` of
  `<Version>` in `Main/_Module/SubModule.xml` (it is `v2.0.30` at `b2e387db`; re-read it in Step 0.2:
  `cd /e/repos/wt-008-binding-gate && grep -o 'Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`).
  At most 72 characters. Suggested: `test(bindings): v2.0.30 - make the binding gate fail loudly on skips`
  (68 characters; check with `printf '%s' "<subject>" | wc -c`). Body wrapped at 72 columns, human
  prose, no em or en dash. Suggested trailer:
  `Not-tested: the discovery floors firing (needs an engine that breaks TAOM type loading)`.
- **No AI attribution trailer** (no `Co-Authored-By`), even if a harness reminder suggests one.
- Stage explicit paths only (the in-scope list); never `git add -A`, `git add .` or `git commit -a`.
  Stage and commit through Bash with the worktree prefix, not an MCP git tool (those bypass the
  repo's commit gates).
- Before `git add` and again before `git commit`, confirm where you are:
  `cd /e/repos/wt-008-binding-gate && git rev-parse --show-toplevel --abbrev-ref HEAD` prints
  `E:/repos/wt-008-binding-gate` and `plan-008-binding-gate`. Anything else: STOP.
- A commit that stages `.claude/` files must also stage `CHANGELOG.md` (this plan does). The commit
  gates inspect the main tree (see "Commit gates read the MAIN tree"). **If any gate denies, STOP and
  report the full deny reason.** Never `--no-verify`, never stage, unstage or edit anything in the
  main tree to satisfy a gate; the orchestrator resolves it.

## Steps

### Step 0: Drift check, worktree, baselines

1. Run the drift check at the top of this file. Expected: no output.
2. Create the worktree and the log folder:
   `git -C E:/repos/TAOM worktree add E:/repos/wt-008-binding-gate -b plan-008-binding-gate HEAD && mkdir -p /e/repos/wt-008-logs`.
   Then `cd /e/repos/wt-008-binding-gate && git rev-parse --show-toplevel --abbrev-ref HEAD` prints
   `E:/repos/wt-008-binding-gate` and `plan-008-binding-gate`. Re-read the version (Git workflow
   command) and note it.
3. Confirm the environment: `printenv BANNERLORD_GAME_DIR` prints the install
   (`E:\Steam\steamapps\common\Mount & Blade II Bannerlord` on the desktop), and
   `ls "$(cygpath -u "$BANNERLORD_GAME_DIR")/bin/Win64_Shipping_Client/Bannerlord.exe"` succeeds. If
   either fails, STOP (environment fact; report, do not fix).
4. Baseline gate, default settings:
   `cd /e/repos/wt-008-binding-gate && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" > /e/repos/wt-008-logs/0-gate.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-008-logs/0-gate.log`
5. Hook baseline: `cd /e/repos/wt-008-binding-gate && bash tools/test_hooks.sh > /e/repos/wt-008-logs/0-hooks.log 2>&1; echo "exit=$?"; grep -E 'FAIL|passed, ' /e/repos/wt-008-logs/0-hooks.log`.
   Record the exit code and any `FAIL` lines.
6. Full-suite baseline:
   `cd /e/repos/wt-008-binding-gate && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-008-logs/0-full.log 2>&1; echo "exit=$?"; grep -E '^\s+Failed |Passed!|Failed!' /e/repos/wt-008-logs/0-full.log`.
   Record the Total and the names of the failing tests. The orchestrator's expectation is Total
   10,239 with exactly the two Armory tests from "Baseline you must not chase" failing (exit 1 is
   then expected). A different set of Armory-reading failures is not yours: record it and continue.
7. Docs and data baselines:
   `cd /e/repos/wt-008-binding-gate && python tools/lint_docs.py --summary` (record the
   `dead_links:` and `ai_dashes:` counts) and
   `cd /e/repos/wt-008-binding-gate && python tools/validate_moduledata.py > /e/repos/wt-008-logs/0-data.log 2>&1; echo "exit=$?"; tail -3 /e/repos/wt-008-logs/0-data.log`
   (record the exit code and last line).

**Verify**: step 4 prints `exit=0` and a summary line matching both `Failed:\s+0,` and `Skipped:\s+0,`,
with `Total:\s+368` at the planned-at commit (a different total is fine if Failed and Skipped are
both 0). Record that total; every later gate run must match it. Step 5 is expected to exit 0; if it
does not, keep its `FAIL` lines as the pre-existing list for Step 7. Steps 6 and 7 are recordings,
not gates.

### Step 1: Confirm the issue

Check this file's Status block. If the orchestrator has not recorded an issue URL, continue and say
so in your final report; do not create one yourself.

**Verify**: none (bookkeeping).

### Step 2 (RED): write the resolution tests

Create `E:\repos\wt-008-binding-gate\TAOM.Tests\Migration\GameAssembliesResolutionTests.cs`. Shape
(fill in the bodies exactly as described; keep AAA comments):

```csharp
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// How the binding gate finds the game. The two environment variables win, in the order
/// Directory.Build.props uses; when neither is set in the test process, the gate falls back to
/// the GameFolder this test assembly was compiled against, so a test DLL built against the game
/// never skips the binding suite just because its runner lacks the variables.
/// </summary>
[TestClass]
public class GameAssembliesResolutionTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "TAOM_GameDirTest_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            try { Directory.Delete(_root, true); } catch { }
        }
    }

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ... the six tests below
}
```

The six tests (names are exact):

| Test | Arrange | Assert |
|---|---|---|
| `ResolveGameDir_NoEnvironmentVariables_FallsBackToTheBuildFolder` | `built = MakeDir("built")` | `GameAssemblies.ResolveGameDir(null, null, built)` equals `built` |
| `ResolveGameDir_GameDirSet_WinsOverTheBuildFolder` | `game = MakeDir("game")`, `built = MakeDir("built")` | `ResolveGameDir(null, game, built)` equals `game` |
| `ResolveGameDir_OverrideHoldingBannerlordExe_WinsOverBoth` | `over = MakeDir("over")`; create `over\bin\Win64_Shipping_Client\` and an empty `Bannerlord.exe` in it (`File.WriteAllText(..., "")`); `game`, `built` as above | `ResolveGameDir(over, game, built)` equals `over` |
| `ResolveGameDir_BuildFolderNotOnDisk_ReturnsNull` | `missing = Path.Combine(_root, "missing")` (never created) | `ResolveGameDir(null, null, missing)` is null |
| `ResolveGameDir_BuildFolderEmpty_ReturnsNull` | none | `ResolveGameDir(null, null, "")` is null |
| `BuiltGameFolder_TestAssemblyAsCompiled_CarriesTheTaomGameFolderMetadata` | none | `Assert.IsNotNull(GameAssemblies.BuiltGameFolder, "TAOM.Tests.csproj must emit [assembly: AssemblyMetadata(\"TaomGameFolder\", \"$(GameFolder)\")] so the binding gate can find the game the build used.")` |

The last test asserts presence only, not a path: a future CI build against reference assemblies may
compile with an empty `GameFolder`, and the attribute is still present then.

**Verify (RED)**: `cd /e/repos/wt-008-binding-gate && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameAssembliesResolutionTests"`
fails to build, and the errors name `ResolveGameDir` and `BuiltGameFolder` in
`GameAssembliesResolutionTests.cs` (for example CS0122 or CS1501 for the private zero-argument
`ResolveGameDir`, CS0117 for `BuiltGameFolder`). Any other error: fix your test file, not
production code.

### Step 3 (GREEN, part 1): add the fallback to `GameAssemblies`

In `E:\repos\wt-008-binding-gate\TAOM.Tests\Migration\GameAssemblies.cs`:

1. Replace `ResolveGameDir()` (`:111-122`) with a thin wrapper plus a pure overload:

```csharp
    /// <summary>
    /// The GameFolder TAOM.Tests.csproj compiled this assembly against, from its
    /// <c>TaomGameFolder</c> assembly metadata. Null when the attribute is missing; empty when the
    /// build had no GameFolder.
    /// </summary>
    internal static string? BuiltGameFolder =>
        typeof(GameAssemblies).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "TaomGameFolder")?.Value;

    private static string? ResolveGameDir() => ResolveGameDir(
        Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR"),
        Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"),
        BuiltGameFolder);

    internal static string? ResolveGameDir(string? overrideDir, string? gameDir, string? builtGameFolder)
    {
        if (!string.IsNullOrEmpty(overrideDir) &&
            File.Exists(Path.Combine(overrideDir, "bin", "Win64_Shipping_Client", "Bannerlord.exe")))
            return overrideDir;

        if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir)) return gameDir;

        if (!string.IsNullOrEmpty(builtGameFolder) && Directory.Exists(builtGameFolder))
            return builtGameFolder;

        return null;
    }
```

   The environment variables keep precedence (additive change; today's behaviour is unchanged
   whenever a variable is set). `GetCustomAttributes<T>()` is the `System.Reflection`
   extension already covered by the file's `using System.Reflection;`. Do not change the `GameDir`
   or `BinFolder` declarations (`:27-28`); a nullable warning (for example CS8601) on
   `GameDir = ResolveGameDir();` is expected and acceptable, since the file already assigns null to
   `GameDir` at `:54`.
2. Change the diagnostic at `:46` to
   `"Game dir unresolved: BANNERLORD_OVERRIDE_DIR/BANNERLORD_GAME_DIR unset or invalid, and the build's TaomGameFolder is missing or not on disk."`
3. Replace the doc comment `:19-21` with:

```csharp
/// Game dir resolution: BANNERLORD_OVERRIDE_DIR (if its bin holds Bannerlord.exe), else
/// BANNERLORD_GAME_DIR, else the GameFolder this assembly was compiled against (the TaomGameFolder
/// assembly metadata TAOM.Tests.csproj emits). The last step lets a test process started without
/// the variables (an IDE runner, dotnet test --no-build from a fresh shell, a -p: property on the
/// build) load the install the build used instead of skipping. If nothing resolves,
/// <see cref="EnsureLoaded"/> returns false and the binding tests Assert.Inconclusive (e.g. CI
/// without a game install): Skipped in the default suite, a failure under
/// TAOM.Tests/binding-gate.runsettings.
```

**Verify**: the filtered command from Step 2 builds, and its summary line matches
`grep -E 'Failed:\s+1, Passed:\s+5,'`; the one failure is
`BuiltGameFolder_TestAssemblyAsCompiled_CarriesTheTaomGameFolderMetadata` with the message from
Step 2 (this is the RED for Step 4).

### Step 4 (GREEN, part 2): emit the build's game folder into the test assembly

In `E:\repos\wt-008-binding-gate\TAOM.Tests\TAOM.Tests.csproj`, insert this `ItemGroup` after the
TaleWorlds reference `ItemGroup` (after the `</ItemGroup>` at `:39`), tab-indented like the rest of
the file:

```xml
	<!-- The game folder this test DLL was compiled against. GameAssemblies falls back to it when the
	     test process has neither BANNERLORD_OVERRIDE_DIR nor BANNERLORD_GAME_DIR, so the binding
	     gate loads the install the build referenced instead of skipping. -->
	<ItemGroup>
		<AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
			<_Parameter1>TaomGameFolder</_Parameter1>
			<_Parameter2>$(GameFolder)</_Parameter2>
		</AssemblyAttribute>
	</ItemGroup>
```

**Verify**:
- The filtered command from Step 2 prints a summary matching `grep -E 'Failed:\s+0, Passed:\s+6,'`.
- `cd /e/repos/wt-008-binding-gate && grep -rn "TaomGameFolder" TAOM.Tests/obj --include="*AssemblyInfo.cs"` prints one
  `AssemblyMetadataAttribute("TaomGameFolder", "...")` line whose second argument is the install path
  from Step 0.3 (C# escaping doubles the backslashes).

### Step 5: add the gate-only runsettings and make the floors fail

1. Create `E:\repos\wt-008-binding-gate\TAOM.Tests\binding-gate.runsettings` with exactly this
   content (no `--` inside the comment):

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Settings for the engine-binding gate (TestCategory=BindingVerification) only. The
     verify-bindings skill and CI pass this file to dotnet test explicitly. In the gate an
     Assert.Inconclusive means nothing was checked, so it fails the run here. The default suite
     does not use this file on purpose: there a test skips when the game or the Armory is absent
     (see the GameAssemblies doc comment). -->
<RunSettings>
  <MSTest>
    <MapInconclusiveToFailed>true</MapInconclusiveToFailed>
  </MSTest>
</RunSettings>
```

2. `HarmonyPatchBindingTests.cs:46-50`: change `Assert.Inconclusive(` to `Assert.Fail(` and add a
   comment line above the `if`:
   `// The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.`
   Keep the message text unchanged.
3. `GameModelOverrideBindingTests.cs:52-53` and `:79-80`: the same change (`Assert.Inconclusive(` to
   `Assert.Fail(`, message unchanged) with the same comment line above each `if`.

This step edits test code only; the floors cannot be triggered without an engine that breaks TAOM
type loading, so the proof is the grep below plus Step 6.

**Verify** (each with the worktree prefix):
- `python -c "import xml.dom.minidom as m; m.parse('TAOM.Tests/binding-gate.runsettings'); print('ok')"` prints `ok`.
- `grep -c "Assert.Inconclusive" TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` prints `1`.
- `grep -c "Assert.Inconclusive" TAOM.Tests/Migration/GameModelOverrideBindingTests.cs` prints `3`.
- `git grep -n RunSettingsFilePath -- '*.csproj' '*.props' '*.targets'` prints nothing, and
  `ls .runsettings` fails (no such file).

### Step 6: prove the gate in all four directions

Each command is one line with the worktree prefix, run in Git Bash; output goes to a log because (c)
and (d) print hundreds of per-test lines. Build first so `--no-build` runs use this plan's code:
`cd /e/repos/wt-008-binding-gate && dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (exit 0).

(a) Strict, environment as on the desktop:
`cd /e/repos/wt-008-binding-gate && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification" > /e/repos/wt-008-logs/6a.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-008-logs/6a.log`
**Expected**: `exit=0`; the summary line matches `Failed:\s+0,` and `Skipped:\s+0,`, and its Total
equals Step 0.4's total.

(b) Strict, both variables removed from the test process (proves the fallback):
`cd /e/repos/wt-008-binding-gate && env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification" > /e/repos/wt-008-logs/6b.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-008-logs/6b.log`
**Expected**: `exit=0`; the summary matches `Failed:\s+0,` and `Skipped:\s+0,`; same total. (Before
this plan the audit measured 335 skipped for this situation.)

(c) Strict, a folder with no game (proves the mapping):
`cd /e/repos/wt-008-binding-gate && FAKE=$(cygpath -w "$(mktemp -d)"); env -u BANNERLORD_OVERRIDE_DIR BANNERLORD_GAME_DIR="$FAKE" dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification" > /e/repos/wt-008-logs/6c.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-008-logs/6c.log; grep -c 'Game assemblies not loaded' /e/repos/wt-008-logs/6c.log; grep -c 'No bin folder with Bannerlord.exe under' /e/repos/wt-008-logs/6c.log`
**Expected**: a non-zero `exit=`; a `Failed!` summary whose `Failed:` count is 300 or more and which
matches `Skipped:\s+0,`; both `grep -c` counts are 300 or more (each failure message reads
`Assert.Inconclusive failed. Game assemblies not loaded: No bin folder with Bannerlord.exe under '...'`).

(d) Default settings, the same kind of fake folder (proves the decided skip survives):
`cd /e/repos/wt-008-binding-gate && FAKE=$(cygpath -w "$(mktemp -d)"); env -u BANNERLORD_OVERRIDE_DIR BANNERLORD_GAME_DIR="$FAKE" dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" > /e/repos/wt-008-logs/6d.log 2>&1; echo "exit=$?"; grep -E 'Passed!|Failed!' /e/repos/wt-008-logs/6d.log`
**Expected**: `exit=0`, a `Passed!` summary that matches `Failed:\s+0,` and whose `Skipped:` count is
300 or more.

If `dotnet test --no-build` errors before running any test in (b), (c) or (d) (the log has no
`Passed!`/`Failed!` line), retry that run once with the vstest form the audit used, redirected the
same way:
`cd /e/repos/wt-008-binding-gate && env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet vstest "<path to TAOM.Tests.dll>" --Settings:TAOM.Tests/binding-gate.runsettings --TestCaseFilter:"TestCategory=BindingVerification" > /e/repos/wt-008-logs/6b-vstest.log 2>&1; echo "exit=$?"`
(find the DLL with `cd /e/repos/wt-008-binding-gate && find TAOM.Tests/bin -name TAOM.Tests.dll`; for
(c) and (d) set `BANNERLORD_GAME_DIR` as shown, and drop `--Settings:` for (d)). If that also fails,
STOP.

### Step 7 (RED): pin the hook's skip reporting in `tools/test_hooks.sh`

In `E:\repos\wt-008-binding-gate\tools\test_hooks.sh`, insert a new section between the end of
section `7b.` and the `# ----` line that precedes `head2 "8. /context-budget ...`. Use the Edit tool
(Bash heredocs mangle backslashes and quotes):

```bash
# ---------------------------------------------------------------------------
head2 "7c. notify-test-results: skipped tests are named, never folded into PASSED"
# MSTest reports Assert.Inconclusive as Skipped and still exits 0. A binding-gate run with no
# game printed "Passed! - Failed: 0, Passed: 33, Skipped: 335" and the banner said
# "PASSED (33 tests)". Drive the real hook with the three summary shapes.
ntr_banner() {
    printf '{"tool_name":"Bash","tool_input":{"command":"dotnet test TAOM.Tests"},"tool_response":"%s","hook_event_name":"PostToolUse"}' "$1" \
        | CLAUDE_PROJECT_DIR="$SANDBOX" timeout -k 2 10 bash "$REPO/.claude/hooks/notify-test-results.sh" 2>&1 >/dev/null
}
OUT=$(ntr_banner 'Passed!  - Failed:     0, Passed:    33, Skipped:   335, Total:   368')
if grep -q 'PASSED WITH SKIPS (Passed: 33, Skipped: 335' <<< "$OUT"; then
    ok "a green run with skips names the skips"
else
    bad "notify-test-results.sh folded 335 skipped tests into a pass: $OUT"
fi
OUT=$(ntr_banner 'Passed!  - Failed:     0, Passed:  6380, Skipped:     0, Total:  6380')
if grep -q 'TEST RESULTS: PASSED (6380 tests)' <<< "$OUT"; then
    ok "a green run with no skips keeps the plain PASSED banner"
else
    bad "notify-test-results.sh changed the no-skip banner: $OUT"
fi
OUT=$(ntr_banner 'Failed!  - Failed:     2, Passed: 10235, Skipped:     2, Total: 10239')
if grep -q 'TEST RESULTS: FAILED (Failed: 2, Passed: 10235, Skipped: 2)' <<< "$OUT"; then
    ok "a red run reports its skips too"
else
    bad "notify-test-results.sh dropped the skip count from a red run: $OUT"
fi
```

**Verify (RED)**: `cd /e/repos/wt-008-binding-gate && bash tools/test_hooks.sh > /e/repos/wt-008-logs/7.log 2>&1; echo "exit=$?"; grep -E 'FAIL|passed, ' /e/repos/wt-008-logs/7.log`
prints `exit=1`, and the failure lines contain exactly the two 7c messages
`notify-test-results.sh folded 335 skipped tests into a pass` and
`notify-test-results.sh dropped the skip count from a red run`; the "keeps the plain PASSED banner"
check passes. Any failure outside section 7c must also be in the list you recorded in Step 0.5
(pre-existing, not yours: report it, do not fix it); a new one outside 7c is a STOP.

### Step 8 (GREEN): report skips in `notify-test-results.sh`, and update its catalog row

1. In `E:\repos\wt-008-binding-gate\.claude\hooks\notify-test-results.sh`, replace the block at
   `:45-59` (from the `# Branch on the COUNT` comment through the closing `fi` of the if/elif chain;
   the outer `fi` at `:60` and `exit 0` stay) with:

```bash
  # Branch on the COUNT, not on the word. `dotnet test` prints "Failed: 0, Passed: 6380"
  # on a fully green run, so a substring test for "Failed" reports every passing suite as
  # FAILED. That went unnoticed because the hook was inert until the jq fix above; it
  # would have mislabelled every green run the moment it started working.
  FAILED=$(echo "$RESPONSE" | grep -oP 'Failed:\s*\K[0-9]+' | head -1)
  PASSED=$(echo "$RESPONSE" | grep -oP 'Passed:\s*\K[0-9]+' | head -1)
  # A skipped test checked nothing. MSTest reports Assert.Inconclusive as Skipped and exits 0,
  # so a binding-gate run of 33 passes and 335 skips used to print "PASSED (33 tests)" here.
  # Name the skips whenever there are any (tools/test_hooks.sh section 7c).
  SKIPPED=$(echo "$RESPONSE" | grep -oP 'Skipped:\s*\K[0-9]+' | head -1)
  SKIPNOTE=""
  if [[ -n "$SKIPPED" && "$SKIPPED" -gt 0 ]]; then
    SKIPNOTE=", Skipped: ${SKIPPED}"
  fi
  if [[ -n "$FAILED" && "$FAILED" -gt 0 ]]; then
    echo "=== TEST RESULTS: FAILED (Failed: ${FAILED}, Passed: ${PASSED:-?}${SKIPNOTE}) ===" >&2
  elif [[ -n "$PASSED" && -n "$SKIPNOTE" ]]; then
    echo "=== TEST RESULTS: PASSED WITH SKIPS (Passed: ${PASSED}${SKIPNOTE}; a skipped test checked nothing) ===" >&2
  elif [[ -n "$PASSED" ]]; then
    echo "=== TEST RESULTS: PASSED (${PASSED} tests) ===" >&2
  elif echo "$RESPONSE" | grep -q "Failed"; then
    # Counts unavailable (a crash or a truncated response) but the word is there: say so
    # rather than staying silent, which would read as "no test run happened".
    echo "=== TEST RESULTS: FAILED (counts unavailable) ===" >&2
  fi
```

   Keep the two-space indentation of the surrounding `if echo "$COMMAND" | grep -q "dotnet test"; then`
   block. Do not add a python call (the hook already pays two interpreter starts against a 5 s timeout).

2. In `E:\repos\wt-008-binding-gate\docs\reference\hooks-catalog.md`, line 42, replace
   `` (`TEST RESULTS: PASSED/FAILED` with counts) `` with
   `` (`TEST RESULTS: PASSED`, `PASSED WITH SKIPS` or `FAILED`, with counts; a non-zero skip count is always shown) ``.
   Change nothing else in the row or the file.

**Verify**:
- `cd /e/repos/wt-008-binding-gate && bash tools/test_hooks.sh > /e/repos/wt-008-logs/8.log 2>&1; echo "exit=$?"; grep -E 'FAIL|passed, ' /e/repos/wt-008-logs/8.log`
  prints `exit=0` and a Summary line ending `0 failed` (all three 7c checks ok; sections 4 and 5
  still pass for `notify-test-results.sh`).
- `cd /e/repos/wt-008-binding-gate && grep -c 'PASSED WITH SKIPS' docs/reference/hooks-catalog.md` prints `1`.

### Step 9: correct the skill and the docs that state the gate command

The new gate command, used everywhere below (call it GATE):
`dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"`

1. `E:\repos\wt-008-binding-gate\.claude\skills\verify-bindings\SKILL.md`
   - Line 27: replace the whole line with the text in this block, written as **one line** in the
     file (the block is wrapped here for reading; join it with single spaces, and do not add quote
     marks around it):

     ```text
     `BANNERLORD_GAME_DIR` (or `BANNERLORD_OVERRIDE_DIR`) points the gate at the install; when
     neither is set in the test process, it falls back to the game folder the test DLL was built
     against. The gate loads the SandBox/CustomBattle/StoryMode module DLLs from there. If no install
     resolves, every gate test calls `Assert.Inconclusive`, and the Step 1 command's
     `binding-gate.runsettings` turns each one into a failure, so the run is red. Without that file
     MSTest reports them as Skipped and exits 0: never quote such a run, or any run with a non-zero
     `Skipped:` count, as a green gate. A missing install is an environment fact to report, not fix
     (see `.claude/rules/environment-failures.md`).
     ```

   - Line 32: replace the command with GATE.
   - Line 74: after "(pass/fail with the per-finding class for any failure)" insert
     ", the run's `Skipped:` count (it must be 0)".
   - Do not change the frontmatter (lines 1-5).
2. `E:\repos\wt-008-binding-gate\docs\reference\taleworlds-api-snapshot\README.md:46`: replace the
   command with GATE. `:66`: replace `` `dotnet test --filter "TestCategory=BindingVerification"` ``
   with GATE in backticks.
3. `E:\repos\wt-008-binding-gate\docs\ai-includes\agent-operating-manual.md:43`: replace the command
   in the second cell with GATE (keep the row's other cells).
4. `E:\repos\wt-008-binding-gate\docs\migration\s6-runtime-punchlist.md:13`: replace the command
   with GATE.

**Verify** (each with the worktree prefix):
- `grep -rn "does not falsely pass" .claude docs` prints nothing.
- `grep -n 'TestCategory=BindingVerification"' .claude/skills/verify-bindings/SKILL.md docs/reference/taleworlds-api-snapshot/README.md docs/ai-includes/agent-operating-manual.md docs/migration/s6-runtime-punchlist.md | grep -v "binding-gate.runsettings"` prints nothing.
- `sed -n 27p .claude/skills/verify-bindings/SKILL.md | grep -c '^`BANNERLORD_GAME_DIR`'` prints `1`
  (one line, no leading quote mark).
- `python tools/lint_docs.py --summary`: `dead_links:` is no higher than the Step 0.7 record, and
  `ai_dashes:` equals the Step 0.7 record.
- `bash tools/test_hooks.sh > /e/repos/wt-008-logs/9.log 2>&1; echo "exit=$?"` prints `exit=0`
  (section `3b.` parses skill frontmatter).

### Step 10: run the strict gate in CI

In `E:\repos\wt-008-binding-gate\.github\workflows\build.yml`, after the `Test` step (`:277-278`,
the end of the file), add at the same indentation:

```yaml

      # The engine-binding gate again, with Inconclusive mapped to Failed: a gate that could not
      # load the game checked nothing, and the default Test step above reports that as Skipped.
      - name: Binding gate (a skip fails)
        run: dotnet test TAOM.Tests -c Release --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"
```

**Verify**: `cd /e/repos/wt-008-binding-gate && python -c "import yaml; d=yaml.safe_load(open('.github/workflows/build.yml', encoding='utf-8')); print([s.get('name') for j in d['jobs'].values() for s in j.get('steps', []) if 'Binding gate' in str(s.get('name'))])"`
prints `['Binding gate (a skip fails)']`.

### Step 11: CHANGELOG entry

In `E:\repos\wt-008-binding-gate\CHANGELOG.md` (the worktree's copy; never the main tree's), under
the `## <today's date, YYYY-MM-DD>` heading at the top (create it directly below the archive note if
today has none), add as the first entry of that day, with the heading equal to your commit subject:

```markdown
### test(bindings): v2.0.30 - make the binding gate fail loudly on skips

- **The gate finds the game the build used.** `GameAssemblies` read the install only from the test
  process's `BANNERLORD_OVERRIDE_DIR` and `BANNERLORD_GAME_DIR`, so a test DLL built against the
  game but run without them (an IDE runner, `dotnet test --no-build` from a fresh shell) skipped most
  of the binding suite and still exited green. `TAOM.Tests.csproj` now records the build's
  `GameFolder` as `TaomGameFolder` assembly metadata, and `GameAssemblies` falls back to it after the
  two variables.
- **A skip in the gate is a failure.** `TAOM.Tests/binding-gate.runsettings` maps Inconclusive to
  Failed. The verify-bindings skill, the docs that give the gate command and the CI job run the gate
  with it. The default suite does not: a test there still skips when the game or the Armory is
  absent, as decided. The discovery floors (fewer than 30 patch types, fewer than 20 GameModels) now
  fail instead of skipping, since they only run once the game has loaded.
- **The test banner names skips.** `notify-test-results.sh` printed `PASSED (33 tests)` for a run of
  33 passes and 335 skips; it now prints `PASSED WITH SKIPS` with the count, and a red run carries its
  skip count too. Pinned by `tools/test_hooks.sh` section 7c; `docs/reference/hooks-catalog.md`
  lists the new banner.
- Closes the binding-gate half of "`Assert.Inconclusive` is a pass" in
  `docs/reviews/rca-lord-identity-2026-08-29.md`; `LordFamilyTransformTests` and the rest of the
  default suite stay open.
```

Replace `v2.0.30` if the version you read in Step 0.2 differs.

**Verify**: `cd /e/repos/wt-008-binding-gate && git diff -- CHANGELOG.md | grep -c '^+### test(bindings)'` prints `1`.

### Step 12: full verification, then commit

Every command below carries the `cd /e/repos/wt-008-binding-gate && ` prefix.

1. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
2. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > /e/repos/wt-008-logs/12-full.log 2>&1; echo "exit=$?"; grep -E '^\s+Failed |Passed!|Failed!' /e/repos/wt-008-logs/12-full.log`:
   the failing test names are exactly the set recorded in Step 0.6 (or a subset, if the Armory work
   landed meanwhile); the Total is Step 0.6's total plus 6; the not-executed tests are the two Warg
   `[Ignore]` tests (plus `BuildOutputUnsafe_WhenPresent_CannotRegressTheDeployedPair` if the fresh
   worktree has no Dependencies build output). A new failure not in the Step 0.6 record: STOP unless
   it is one of this plan's six new tests (then fix it).
3. Re-run Step 6 (a). Same result.
4. `bash tools/test_hooks.sh` exits 0 with `0 failed`. `python tools/lint_docs.py --summary` meets the
   Step 9 expectation. `python tools/validate_moduledata.py` gives the same exit code and last line as
   Step 0.7. `python tools/audit_claude_config.py --min HIGH` reports no finding that names
   `notify-test-results.sh`.
5. Location check: `git rev-parse --show-toplevel --abbrev-ref HEAD` prints
   `E:/repos/wt-008-binding-gate` and `plan-008-binding-gate`.
6. Stage the 15 in-scope paths explicitly:
   `cd /e/repos/wt-008-binding-gate && git add TAOM.Tests/Migration/GameAssembliesResolutionTests.cs TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/TAOM.Tests.csproj TAOM.Tests/binding-gate.runsettings TAOM.Tests/Migration/HarmonyPatchBindingTests.cs TAOM.Tests/Migration/GameModelOverrideBindingTests.cs tools/test_hooks.sh .claude/hooks/notify-test-results.sh docs/reference/hooks-catalog.md .claude/skills/verify-bindings/SKILL.md docs/reference/taleworlds-api-snapshot/README.md docs/ai-includes/agent-operating-manual.md docs/migration/s6-runtime-punchlist.md .github/workflows/build.yml CHANGELOG.md`
7. Dash check on staged non-C# prose:
   `cd /e/repos/wt-008-binding-gate && git diff --cached -U0 -- . ':(exclude)*.cs' | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); bad=[l for l in t.splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)]; print('\n'.join(bad)); sys.exit(1 if bad else 0)"`
   exits 0.
8. `git status --short` shows exactly the 15 staged paths and nothing unstaged or untracked (build
   output is git-ignored).
9. Write the commit message with the Write tool to `E:\repos\wt-008-logs\commit-msg.txt`: the
   subject from "Git workflow", a blank line, a short body (what changed and why, the four Step 6
   results as evidence), a blank line, the `Not-tested:` trailer. Then repeat the location check
   (12.5) and commit:
   `cd /e/repos/wt-008-binding-gate && git commit -F E:/repos/wt-008-logs/commit-msg.txt`
   (use the `E:/` form of the path, not `/e/`: the commit-subject gate reads the file from Python
   while its working directory is the main tree). No push. If a gate denies, STOP and report the
   reason verbatim (Git workflow).

**Verify**: `git log -1 --format=%s` prints the subject; `git show --stat HEAD` lists exactly the 15
in-scope files; `git log -1 --format=%B | grep -ci "co-authored-by"` prints `0`;
`git -C E:/repos/TAOM diff --cached --name-only` is unchanged from before your commit (you staged
nothing in the main tree).

## Test plan

- **New tests** in `TAOM.Tests/Migration/GameAssembliesResolutionTests.cs` (6): every cell of the
  resolution order (override wins; game dir wins over build folder; build folder used when both
  variables are unset; build folder missing on disk; build folder empty) plus the metadata presence
  pin. Structural pattern: `TAOM.Tests/Core/Logging/FileLoggerTests.cs` (temp folder per test,
  deleted in `[TestCleanup]`).
- **Hook tests** in `tools/test_hooks.sh` section `7c.` (3 checks): green with skips, green without
  skips (unchanged banner), red with skips.
- **Integration proof** (Step 6): the strict gate green with the environment, green without it
  (fallback), red with a fake game folder (mapping), and the default suite still green with skips on
  the fake folder (the decided skip is untouched).
- **Structurally untestable here:** the discovery floors firing (they need an engine that breaks TAOM
  type loading) and the new CI step executing (pushes to `bannerlord-1.5.x` do not trigger the
  workflow). Name both in the commit's `Not-tested:` trailer.
- Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows only the
  failures recorded in Step 0.6, and the 6 new tests pass.

## Done criteria

ALL must hold, each run with the `cd /e/repos/wt-008-binding-gate && ` prefix:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameAssembliesResolutionTests"` prints a summary matching `Failed:\s+0, Passed:\s+6,`.
- [ ] The full suite's failures are exactly the Step 0.6 record (the orchestrator expects `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`), or a subset of it; Total is the Step 0.6 total plus 6.
- [ ] Step 6 (a) and (b) print `exit=0` and summaries matching `Failed:\s+0,` and `Skipped:\s+0,`; (c) prints a non-zero exit; (d) prints `exit=0` with a `Skipped:` count of 300 or more.
- [ ] `grep -c "Assert.Inconclusive" TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` is `1` and for `GameModelOverrideBindingTests.cs` is `3`.
- [ ] `git grep -n RunSettingsFilePath -- '*.csproj' '*.props' '*.targets'` prints nothing; no `.runsettings` file at the repo root.
- [ ] `bash tools/test_hooks.sh` exits 0.
- [ ] `grep -rn "does not falsely pass" .claude docs` prints nothing.
- [ ] `python tools/lint_docs.py --summary` shows `dead_links:` no higher and `ai_dashes:` equal to the Step 0.7 record.
- [ ] `git show --stat HEAD` lists only the 15 in-scope files; nothing is pushed (`git status -sb` shows no upstream for `plan-008-binding-gate`); the main tree's index is untouched.

## STOP conditions

Stop and report (do not improvise) if:

- The drift check shows a change to an in-scope file and its "Current state" excerpt no longer matches.
- Any command you run shows `E:/repos/TAOM` as its toplevel or `bannerlord-1.5.x` as its branch when
  it should be the worktree, or you notice you edited a file under `E:\repos\TAOM\`. Report which
  file and what changed; do not try to revert the main tree yourself.
- A commit gate denies, or its reason names any file outside the 15 in-scope paths. Report the reason
  verbatim; never edit, stage or unstage anything in the main tree, never `--no-verify`.
- Step 0.4 (default gate, game present) shows any failure or skip: something in the gate is already
  broken or already skipping on this machine; report the test names, do not proceed (a strict run
  would turn a pre-existing skip red and hide this plan's effect).
- Step 6 (a) shows any failure or skip: a `BindingVerification` test goes Inconclusive with the game
  present. Report its name and message; do not convert it and do not weaken the runsettings.
- Step 6 (b) still skips or fails after Step 4: the metadata fallback is not taking effect (check
  Step 4's `obj` grep). Do not reorder precedence to "fix" it.
- Step 6 (c) exits 0: `dotnet test --settings` is not applying `MapInconclusiveToFailed`. Do not
  work around it with a global setting.
- Step 12.2 shows a failure that is neither in the Step 0.6 record nor one of this plan's six tests.
- Any fix seems to need `Directory.Build.props`, `Main/TAOM.csproj`, `Main/IoC.cs`,
  `Main/SubModule.cs`, `build.ps1`, `.claude/settings.json` or `ReflectionSiteBindingTests.cs`.
- You find yourself making `MapInconclusiveToFailed` apply to the default `dotnet test` run in any
  way. That overturns a recorded decision; it needs the maintainer.
- `BANNERLORD_GAME_DIR` is unset in your shell, or the game path from Step 0.3 does not exist
  (environment fact: report, do not fix).
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- **Precedence choice.** The build's folder is the last fallback, so the environment variables keep
  winning, which keeps today's behaviour identical whenever they are set. The audit's checker noted
  the reverse mismatch this leaves open: a DLL built against `BANNERLORD_OVERRIDE_DIR` but tested with
  only `BANNERLORD_GAME_DIR` set loads module DLLs from a different engine than the `TaleWorlds.*.dll`
  copied into the bin. Reading `TaomGameFolder` first (or diagnosing a mismatch) would close it;
  deferred because it changes behaviour for anyone who sets the variables deliberately.
- **Future CI plan.** A hosted-runner job (no game, BUTR reference assemblies) must run the gate with
  `binding-gate.runsettings`, or its binding results go green by skipping. The metadata seam lets such
  a job point `GameFolder` at a reference-assembly folder; the presence-only assertion in
  `BuiltGameFolder_TestAssemblyAsCompiled_CarriesTheTaomGameFolderMetadata` keeps passing with an
  empty `GameFolder`. The new `build.yml` step runs on `bannerlord-1.5.x` only through a manual
  `workflow_dispatch` until the push trigger covers the branch.
- **Reviewer focus (`/deep-review`):** that no path makes the mapping global; that the new
  diagnostic and doc comment match the code; that the hook still exits 0 on every path and adds no
  interpreter start; that `ResolveGameDir` keeps the override's `Bannerlord.exe` check and the
  `Directory.Exists` checks; the nullable warning on `GameDir = ResolveGameDir();` (accepted, see
  Step 3); the CHANGELOG, skill and hooks-catalog prose for dashes.
- **Deferred, out of this plan:** `build.ps1 -RunTests` still checks only the exit code of the default
  suite (it could print the skip count); the roughly 96 other test files that go Inconclusive, and the
  28 private repo-root locators, belong to the locator-consolidation plan; `LordFamilyTransformTests`
  (the other half of the RCA item) needs a vendored fixture; the orchestrator records a lesson in
  `docs/reviews/lessons/testing-qa.md` or `build-tooling-workflow.md` ("a gate's own skip must fail the
  gate") once this lands. The worktree and `E:\repos\wt-008-logs\` are left for the orchestrator to
  remove after merge.
- **Port:** the `bannerlord-1.4.5` branch has the same `GameAssemblies.cs` lineage; porting is a
  separate decision for the maintainer.
