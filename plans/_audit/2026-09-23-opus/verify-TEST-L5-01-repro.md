# Verify TEST-L5-01, lens: does it reproduce (baseline `b2e387db`)

Checker: fresh adversarial, read-only. No dotnet build or test was run. The mechanism was traced
through code at `b2e387db` (`git show`), the lane's raw run artifacts in the session scratchpad
(`scratchpad\lane5-probe\f3.log`, `f4.log`, `c1.log`, `c2.log`, `results\*.trx`, `map.runsettings`),
and the live environment of this desktop. The worktree the artifacts came from
(`scratchpad\wt-opus-head`) is at `b2e387db7e4e`, clean (`git rev-parse HEAD`, `status --short`).

## TEST-L5-01: CONFIRMED (mechanism), impact today LOW, two evidence lines corrected

**Chain, link by link (each re-read this run):**

1. No mapping exists. `git show b2e387db:TAOM.Tests/TAOM.Tests.csproj` is 42 lines (not 43) with no
   `RunSettingsFilePath`; `git grep -n -i runsetting b2e387db` returns nothing (rc 1), and
   `git ls-tree -r b2e387db` holds no `*.runsettings`. MSTest is 3.1.1 (`TAOM.Tests.csproj:10-11`).
2. The game gate reads the test process environment only: `TAOM.Tests/Migration/GameAssemblies.cs:111-122`
   (`Environment.GetEnvironmentVariable` for both names), `:43-48` returns false when neither resolves.
3. The gate turns that into Inconclusive: `GameModelOverrideBindingTests.cs:40,48-49`,
   `HarmonyPatchBindingTests.cs:33,41-42`.
4. Default MSTest maps Inconclusive to Skipped, and the run is green. Same DLL, same filter, only the
   settings differ: `f3.log` ends `Passed!  - Failed: 0, Passed: 33, Skipped: 335, Total: 368`;
   `f4.log` (with `map.runsettings`) ends `Failed!  - Failed: 335, Passed: 33, Skipped: 0` and each
   failure reads `Assert.Inconclusive failed. Game assemblies not loaded: Game dir unresolved`.
5. The trx hides it: `f3.trx` `ResultSummary outcome="Completed"`, `Counters total="368" executed="33"
   passed="33" failed="0" inconclusive="0" notExecuted="0"`, while 335 per-result outcomes are
   `NotExecuted`; `f4.trx` is `outcome="Failed"`. `c1.trx` matches the finding's text exactly.
6. Consumers trust the green: `build.ps1:37-40` checks only `$LASTEXITCODE`. Not cited by the finding
   but a stronger link: `.claude/hooks/notify-test-results.sh:49-54` parses only `Failed:` and
   `Passed:`, so an F3-shaped output yields the banner `=== TEST RESULTS: PASSED (33 tests) ===` with
   the 335 skips never mentioned.
7. The skill sentence is wrong about the mechanism: `.claude/skills/verify-bindings/SKILL.md:27`
   ("self-reports `Assert.Inconclusive` (it does not falsely pass)"); an Inconclusive is a Skipped and
   the run prints `Passed!`.

Counts re-derived from the trx: F1 3 NotExecuted, F2 363 (difference 360); run A 63 NotExecuted (61
plus the two `[Ignore]` warg tests, baseline.md:20). Exit codes are not in the logs; they are
inferred from `ResultSummary` (`Completed` vs `Failed`), which is how vstest decides its exit code.

**Where the trigger does NOT fire today (the reason impact is LOW):**

- On this desktop `BANNERLORD_GAME_DIR` is set at User scope and inherited: PowerShell process and
  User scope both `E:\Steam\steamapps\common\Mount & Blade II Bannerlord`; the Bash tool sees it too.
- `build.ps1:11-15` exits 1 when the User-scope variable is missing, so `-RunTests` cannot reach
  the tests without it.
- The only CI C# job sets it at job level for build and test alike (`.github/workflows/build.yml:260-261`),
  and it runs only for `bannerlord-1.4.5` (`:3-8`).
- The skill's own Step 1 (`SKILL.md:32`) is `dotnet test`, which builds first. With the variable
  unset, `GameFolder` is empty (`Directory.Build.props:37-38`) and Main's game references
  (`Main/TAOM.csproj:40-57`, all `$(GameFolder)\...`) resolve nothing, so the build would fail before
  any test runs [Likely; not built, per the brief].

**Corrected evidence:**

- "The skill's own command exits 0 with `Passed!`" overstates F3. F3 ran `dotnet vstest` on a
  prebuilt DLL (lane-5 line 19, `f3.log` line 1 `VSTest version 18.7.0`), not the skill's
  `dotnet test`. What reproduces is the same filter against an already-built bin in a process
  without the variable: `dotnet test --no-build` (documented at `.ai/verification.md:15` and
  `.claude/skills/verify/SKILL.md:27`), `dotnet vstest`, an IDE runner, or `dotnet test
  -p:BANNERLORD_GAME_DIR=...` (TEST-L5-02).
- "Having run nothing" is overstated: 33 of 368 executed (`f3.trx`), among them real engine
  resolutions against the core DLLs copied into the bin (`Patch43LoadPhaseBindingTests` x6,
  `ScreenManagerEventBindingTests` x2, `ArmyMembershipBindingTests` x7). The three classes the skill
  names as the gate (HarmonyPatch, GameModelOverride, every ReflectionSite row) were skipped except
  two narrow tests (`AnAbstractEngineMethod_ResolvesButHasNoBody_SoTheGateRefusesIt`,
  `TaomCharacterStatsModel_DeclaresMaxHitpointsOverride_ForCareerHealthPassive`).
- Csproj range is `:1-42`, not `:1-43`.

**A trigger the finding missed that fires with the variable set:** the discovery floors also call
Inconclusive, `HarmonyPatchBindingTests.cs:46-50` (under 30 patch types loaded, "an assembly-load
problem, not a genuine pass") and `GameModelOverrideBindingTests.cs:52-53` (under 20 models). A real
engine bump that breaks TAOM type loading, which is the case this gate exists for, would go Skipped
and green by the same mapping. Not reproduced (needs a broken engine), so UNVERIFIED as an
occurrence; the code path is read.

## What I did not cover

- Did not run any test or build (brief), so the exit codes and the "skill command fails to build
  when the variable is unset" claim are inferred, not measured.
- Did not read the design-lens verdict (`verify-TEST-L5-01-design.md`) to keep this check independent.
- Did not re-verify the 73-file and 96-file Inconclusive counts or the RefAsm/NoTw measurements
  (Measurement 3); only the counts this finding cites from F1, F2, F3, F4, C1 and run A.
- Did not check IDE runner behaviour (VS or Rider Test Explorer show Skipped with a warning icon, which
  a human would see; not measured).
