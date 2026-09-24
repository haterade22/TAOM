# Verify TEST-L5-07, lens: by design or already decided (run `2026-09-23-opus`, baseline `b2e387db`)

Read-only checker. Nothing built or tested. All repo reads are `git show b2e387db:<path>` or
`git grep ... b2e387db`; probe artifacts read from the session scratchpad `lane5-probe\`.

## TEST-L5-07: Build and test on GitHub-hosted Windows against BUTR reference assemblies

- **Outcome:** CONFIRMED (no recorded decision covers it). Impact today: MED.
- **Decision search (what I read):**
  - `docs/adrs/` 001 to 011: nothing about CI runners or reference assemblies. ADR-008
    `008-testability-requirements.md:246-252` has a "CI/CD Gate (Recommended)" section that runs
    `dotnet test` in a workflow, so the ADRs lean toward the finding, not against it.
  - `.claude/rules/`, `docs/ai-includes/orientation.md` trap index: no CI or runner entry.
  - `.ai/policy.md:78-79` and `.ai/verification.md:6-7` forbid untrusted PR code on the personal
    self-hosted workstation. That constraint agrees with the finding's "no self-hosted runner".
  - `.ai/verification.md:21-23` says the game and net472 targeting pack "are local dependencies, not
    provided by a generic hosted runner". A statement of fact, not a decision; the finding supplies
    both (BUTR packages, `Microsoft.NETFramework.ReferenceAssemblies`). The fix must update this line.
  - The only recorded choice is commit `59fa6222` (2026-09-01, "ci: self-host the build job so C#
    compiles") and its comment at `.github/workflows/build.yml:240-259`. Its stated premise: "the
    runner needs both a Bannerlord install and the Framework targeting pack. Only a self-hosted
    Windows box has either." It never weighs BUTR reference assemblies: `git log -S
    "Bannerlord.ReferenceAssemblies" b2e387db` over csproj/props/targets/md/yml finds only
    `2981365e` (a docs link at `docs/reference/external-resources.md:15`), so the option was never
    tried or rejected. The decision also never covered `bannerlord-1.5.x` (`build.yml:3-8` name only
    `bannerlord-1.4.5`), and the job stays skipped until a runner is registered (`:24`, `:259`).
  - `docs/reviews/lessons/build-tooling-workflow.md` "A CI step covers only the branches its
    workflow's `on:` block names" and `docs/reviews/rca-adr011-batch1-2026-09-23.md` F6 record
    single-branch CI coverage as a defect class, not an accepted risk.
  - June `plans/README.md` "Findings considered and NOT planned": "CI dark on this branch" was
    **deferred** to a doc/CI hygiene session, not rejected.
  - BRIEF.md decided tradeoffs: none covers CI or reference assemblies. The "lotraom-assets mirror
    stays local" decision concerns the Armory data, not engine DLLs; BUTR packages are fetched from
    nuget.org at restore, never committed, so no redistribution decision is touched.
  - Docs that assume C# CI exists, which strengthen the gap: `docs/features/troop-skill-balance.md:224`
    ("`TroopUpgradeSkillMonotonicityTests.cs` (runs in CI, no game install)") and
    `docs/migration/dr3-maintenance.md:116` (keeps MSVC off the path for "CI building `TAOM.dll`").
- **Links re-read myself:**
  - `build.yml:3-8` triggers `bannerlord-1.4.5` only; `:253` `runs-on: [self-hosted, windows]`;
    `:259` `if: vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request'`. As cited.
  - `Main/TAOM.csproj:25-59` (committed) HintPaths: System.Management, System.Numerics.Vectors (alias
    `SNV`), `TaleWorlds.*` minus Native, Native, SandBox, SandBoxCore, CustomBattle module bins;
    ProjectReference to Dependencies at `:89`. As cited.
  - BUTR nuspecs in `lane5-probe\butr\`: Core, Native, SandBox, CustomBattle, StoryMode all
    `1.5.3.122374-beta`, tags `buildId:25302170`, description "stripped metadata-only libraries for
    building against Mount & Blade II: Bannerlord". `E:\Steam\steamapps\appmanifest_261550.acf`
    `buildid` is `25302170`: exact match. Core holds 50 `ref/net472/*.dll`.
  - BCL: NuGet `System.Management 4.7.0` `lib/netstandard2.0` is `4.0.1.0` and `System.Numerics.Vectors
    4.4.0` `lib/net46` is `4.1.3.0`, identical to the game bin copies (`AssemblyName.GetAssemblyName`).
    This honours the `TAOM.csproj:17-24` comment that System.Management must bind 4.0.1.0.
  - trx counters: `ci_RefAsm.trx` passed 8,546 / failed 1,330 / total 10,239; `ci_bind_refasm.trx`
    338 passed / 30 failed of 368; `ci_NoTw.trx` passed 7,174. As the lane states.
  - `Bannerlord.BuildResources 1.1.0.129` `build/Basic.targets` (NuGet cache): a missing
    `BANNERLORD_GAME_DIR` gives only Warnings (`:12-16`); every copy target is conditioned on
    `Exists($(GameFolder))` (`:47,53,58,64`); the one Error (`:35-37`, `TestCheckForGameBinaries`,
    `BeforeTargets="Test"`) sits in a package Main references with `PrivateAssets all`
    (`TAOM.csproj:95-98`), so it does not flow to `TAOM.Tests`. Read, not executed.
- **Not verified (by anyone):** an actual compile on a hosted runner with the property switch. The
  lane says so itself (Confidence MED on the build step). This bears on fix effort, not on whether
  the gap exists or whether a decision covers it.
- **Corrected evidence:** citations hold. Add to the fix scope: `.ai/verification.md:21-23` and the
  rationale comment `build.yml:240-252` (both state the hosted-runner impossibility this design
  removes), plus the false "runs in CI" claim at `docs/features/troop-skill-balance.md:224`.
- **Impact MED, not HIGH:** no player-facing failure today; it is a missing safety net on the
  active branch, where merges rely on the local gate alone.

## What I did not cover

- No GitHub state read (`gh` not used): whether a self-hosted runner or `BANNERLORD_GAME_DIR`
  variable exists today is taken from seed F1 and the warning job, not re-checked.
- No build or test run; the hosted compile remains UNVERIFIED as above.
- The `1.4.8.119303` package line for `bannerlord-1.4.5` was not checked (no 1.4.8 install here).
- TaleWorlds' licence position on BUTR's stripped assemblies was not researched; the design only
  downloads them at restore, which no TAOM decision addresses either way.
