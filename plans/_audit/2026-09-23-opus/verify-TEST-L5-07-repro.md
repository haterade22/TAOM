# Verify TEST-L5-07, lens: does it reproduce

Checker: fresh adversarial pass, baseline `b2e387db` (working HEAD is now `4b5662b2`; `git diff --stat
b2e387db HEAD -- .github Main/TAOM.csproj Dependencies TAOM.Tests/TAOM.Tests.csproj Directory.Build.props`
is empty, so every CI and project citation below holds at both).

## Trigger-to-harm chain (in progress)

1. Trigger: a push of C# to `bannerlord-1.5.x`. `git show b2e387db:.github/workflows/build.yml` lines
   3-8: `push` and `pull_request` name only `bannerlord-1.4.5`. HOLDS.
2. The only other workflow, `doc-budget.yml` lines 9-35, runs `lint_docs.py --fail-on-drift` on every
   push; it compiles nothing. HOLDS.
3. Even on 1.4.5 the C# job is skipped: `build.yml:253` `runs-on: [self-hosted, windows]`, `:259`
   `if: vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request'`. Read-only `gh` this run:
   `gh api repos/:owner/:repo/actions/runners` total_count 0; `actions/variables` returns no variable;
   run 35005853072 (1.4.5 push, 2026-09-15) shows job `Build & Test` conclusion `skipped`. HOLDS.
4. The branch is pushed and receives C# from outside the Claude hooks: `origin/bannerlord-1.5.x` =
   `c79a5852`, subject "Add scripts for generating and validating animal animation clips" (no version
   label, so the Claude commit-subject hook did not gate it), touching 43 `.cs`/project files
   (`git show --name-only c79a5852 | grep -cE '\.(cs|csproj|props|targets)$'`). `gh run list --branch
   bannerlord-1.5.x` returns exactly one run ever: `Doc budget`, success, on that push. HOLDS.
5. No local gate compiles before push: `core.hooksPath` is `c:\Users\mikew\source\repos\TAOM\.git\hooks`
   (from `.git/config`), a directory that does not exist, and `.git/hooks` holds only samples;
   `.claude/settings.json` PreToolUse(Bash) hooks include `validate-push.sh`, which only warns or
   blocks force pushes (lines 1-80 read), none runs `dotnet build`. HOLDS.
6. Harm today: latent, not realized. `baseline.md` records `b2e387db` builds with 0 errors; the suite
   has 2 failures, both on live-Armory state that the proposed hosted CI would exclude anyway
   (`LiveInstall`). So "a broken build reaches the branch" is a proven open path, not an observed event.
7. No server-side gate either: `gh api repos/:owner/:repo/branches/bannerlord-1.5.x` returns
   `protected: false`, required checks `enforcement_level: off`; `rulesets` length 0. The repo is
   public (`visibility: public`). HOLDS.

## Design evidence re-checked (load-bearing for the fix, not for the harm)

- `Main/TAOM.csproj` at `b2e387db`: System.Management `:25-28`, SNV alias `:35-39`, TaleWorlds.* minus
  Native `:40-43`, Native `:44-47`, SandBox `:48-51`, SandBoxCore `:52-55`, CustomBattle `:56-59`,
  ProjectReference `:89` (`:86` is the comment). All as cited.
- `Dependencies/TAOM.Dependencies.csproj:16-17` excludes Native and CampaignSystem; `TAOM.Tests/Migration/
  GameAssemblies.cs:33-34` lists SandBox, SandBoxCore, CustomBattle, StoryMode, Native. As cited.
- Package coverage, re-measured with `scratchpad\l507_check.py` over the lane's downloaded nupkgs versus
  the installed game: Core ref/net472 holds all 50 installed `TaleWorlds.*.dll` minus Native (plus 4
  `.exe`); Native 5/5, SandBox 6/6, CustomBattle 2/2, StoryMode 5/5; installed SandBoxCore bin has no
  DLL. Every nuspec is `1.5.3.122374-beta` with tag `buildId:25302170`, equal to `buildid` in
  `appmanifest_261550.acf`. nuget.org flat container lists both `1.5.3.122374-beta` and `1.4.8.119303`
  (126 versions); `bannerlord.referenceassemblies.sandboxcore` returns 404. HOLDS.
- Probe trx counters (`lane5-probe\results\`): `ci_RefAsm.trx` total 10,239, executed 9,876, passed
  **8,546**, failed 1,330; `ci_NoTw.trx` total 10,055 (184 fewer discovered), passed 7,174;
  `ci_bind_refasm.trx` 368 executed, 338 passed, 30 failed. The lane's numbers reproduce exactly.
- UNVERIFIED, as the lane itself says: compiling against the BUTR packages with an empty `GameFolder`
  under `Bannerlord.BuildResources 1.1.0.129`. The probe swapped DLLs into a test bin that was compiled
  against the real install, so it proves runtime behavior and coverage, not the compile step.

## Verdict: TEST-L5-07

- **Outcome**: CONFIRMED (the defect: no CI job compiles C# on the working branch, and the path from a
  push to an unchecked branch tip holds at every link). The remedy's compile step stays UNVERIFIED.
- **Correction / sharpening**: the gap is wider than "on `bannerlord-1.5.x`": no C# is compiled on
  ANY branch today, because the self-hosted job is skipped on every 1.4.5 run too (0 runners registered,
  no `BANNERLORD_GAME_DIR` variable; run 35005853072 `Build & Test` = skipped). The only run ever on
  1.5.x is `Doc budget` (35898096921). Evidence: `build.yml:3-8,253,259` plus the `gh` reads above.
- **Impact today**: MED, not HIGH. The path is open, but no broken build has been observed at the tip
  (`baseline.md`: `b2e387db` builds with 0 errors), and the sole maintainer builds locally to play, which
  surfaces a compile break quickly. The realized exposure is commits made outside the Claude hooks
  (`c79a5852`, 43 C#/project files, pushed, unlabelled) landing with only a doc lint behind them.

## What I did not cover

- Did not build or test (brief). Did not attempt a hosted compile, so the `RefAsm` build step and the
  net472 targeting pack on `windows-latest` remain UNVERIFIED.
- Did not check whether any past 1.5.x commit failed to compile (would need builds at each commit).
- The `1.4.8.119303` packages were not compared against a 1.4.8 install (none here).
- The design's security reasoning about self-hosted runners was not exercised; with 0 runners
  registered it is latent today.
