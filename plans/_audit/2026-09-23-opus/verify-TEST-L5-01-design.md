# Verify TEST-L5-01, lens: by design or already decided

Checker: fresh adversarial pass, baseline `b2e387db`. No build or test run. Sources read this run are
cited per line; the lane-5 probe outputs were read from
`scratchpad\lane5-probe\` (`f3.log`, `f4.log`, `c1.log`, `c2.log`, `results\f3.trx`, `f4.trx`, `c1.trx`,
`map.runsettings`).

## TEST-L5-01: Map Inconclusive to Failed (the engine-drift gate can report "Passed!" having run nothing)

- **Outcome**: CONFIRMED (mechanism real, no recorded decision accepts it), with corrected evidence and
  one design conflict in the fix sketch.
- **Impact today**: LOW. The force of the finding is forward-looking (hosted CI) plus a false sentence
  in a skill.

### What a decision DOES cover (the per-test skip)

Skipping, rather than failing, when the game or the Armory is absent is a recorded design, not an
accident:

- `TAOM.Tests/Migration/GameAssemblies.cs:19-21` (doc comment): "If neither resolves, EnsureLoaded
  returns false and the binding tests should Assert.Inconclusive (e.g. CI without a game install)."
- `.claude/skills/verify-bindings/SKILL.md:27`: "If unset, the gate self-reports `Assert.Inconclusive`
  (it does not falsely pass). This is an environment fact to report, not fix."
- `docs/reviews/lessons/data-content-cultures.md` at `b2e387db` lines 1588-1591 (the working-tree copy
  is another session's, lines shifted): Armory-reading gates are "Inconclusive without the install",
  and a new Armory reference gets "the same shape of gate ... it reads the live file and skips, never
  fails, when the file is absent" (2026-09-23).
- Same file, line 276: `BannerBearerReplacementWeaponDataTests` is "the template", with
  "`Assert.Inconclusive` off-machine".

### What no decision covers (the skip reading as a pass)

- The stated intent is that Inconclusive does NOT read as a pass (SKILL.md:27). The probe shows it
  does: `f3.log` ends `Passed!  - Failed: 0, Passed: 33, Skipped: 335, Total: 368`; `results\f3.trx`
  has `ResultSummary outcome="Completed"`, `Counters total="368" executed="33" ... inconclusive="0"
  notExecuted="0"`, and 335 per-result `outcome="NotExecuted"`. With `map.runsettings`
  (`<MSTest><MapInconclusiveToFailed>true`), `f4.log` ends `Failed! - Failed: 335` and the trx
  `ResultSummary outcome="Failed"`. `c1.log` / `c2.log` show the same for `MountDespawnWiringTests`
  (2 skipped then 2 failed, the message "Main/SubModule.cs not found").
- The project itself records this as OPEN, not accepted: `docs/reviews/rca-lord-identity-2026-08-29.md:72`
  "Still open, recorded not fixed", `:77-80` "`Assert.Inconclusive` is a pass".
- House counter-rule for data gates: `TAOM.Tests/Features/TroopProgression/TroopUpgradeSkillMonotonicityTests.cs:101-105`
  ("Assert.Inconclusive would leave `dotnet test` green") and `docs/features/troop-skill-balance.md:257`.
- `docs/reviews/lessons/build-tooling-workflow.md:1142-1160` ("fail-open must still be fail-LOUD") is
  the general lesson this violates.
- `.ai/verification.md:25-28` ("Capture test totals, failures and skips ... record skips affecting the
  assigned scope as incomplete evidence") is a manual process mitigation for review packets, not a
  decision that exit 0 is acceptable; `build.ps1` and the verify-bindings Output step do not apply it.
- No ADR (`docs/adrs/000` to `011`) or `.claude/rules/*.md` mentions Inconclusive or runsettings
  (`git grep -n -i inconclusive b2e387db -- docs/adrs .claude/rules` returns nothing). No runsettings
  anywhere: `git grep -l -E "runsettings|MapInconclusiveToFailed" b2e387db` exits 1.

### Corrected evidence

1. `TAOM.Tests/TAOM.Tests.csproj` is 42 lines (`:1-42`), not `:1-43`; it has no `RunSettingsFilePath`.
2. `build.ps1:37-42`, not `:37-40`: `:37` runs `dotnet test`, `:38` checks only `$LASTEXITCODE`, and
   `:42` prints "All tests passed!" on exit 0.
3. "The skill's own command exits 0 with `Passed!`" is overstated. F3 ran `dotnet vstest` on a
   prebuilt bin. The skill's Step 1 is `dotnet test ... --filter "TestCategory=BindingVerification"`,
   which builds first, and `Directory.Build.props:37-38` has no fallback path, so in a shell without
   the variable that command most likely fails at build rather than going green (UNVERIFIED for an
   incremental build). The green-by-skip path needs a test process without the variable and a bin
   built with it: `-p:BANNERLORD_GAME_DIR=...` (TEST-L5-02), `--no-build`, an IDE runner, or a CI step
   env. The skill's sentence "(it does not falsely pass)" is still false for those invocations.
4. Today's CI does not trigger it: the self-hosted job sets `BANNERLORD_GAME_DIR` at job level
   (`.github/workflows/build.yml:260-261`), so build and test share it. The hosted-runner case is
   future (F1).

### Design conflict in the fix sketch

The sketch frames the exception as "the few tests that legitimately cannot run somewhere". The
recorded decision above covers the whole `GameAssemblies` family (73 files per the lane) and every
Armory-reading gate (10 files), so a global `MapInconclusiveToFailed` reverses a decision written the
same day (lessons `data-content-cultures.md:1588-1591` at `b2e387db`). That needs an owner decision
and a rewrite of that lesson and of `GameAssemblies.cs:19-21`, or a narrower fix (for example a
runsettings used only by the binding gate and CI, or `TreatNoTestsAsError`-style skip-count checks
in `build.ps1` and the verify-bindings Output step). The finding stands; the sketch must name the
decision it overturns.

## What I did not cover

- Did not run any build or test; exit codes for F3, F4 and C1 are from the lane's report plus the
  `Passed!` / `Failed!` summary lines and trx `ResultSummary`, not from a captured exit code.
- Did not verify whether an incremental `dotnet test` build without `BANNERLORD_GAME_DIR` fails or is
  skipped by MSBuild's up-to-date check.
- Did not re-derive the 73 / 10 / 360 / 61 counts; they are the lane's.
- Did not read the uncommitted working-tree lessons file (another session's).
