# Deep review: plan 017, build identity and the dirty-tree flag (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 017, "Stamp a dirty-tree flag into every build and a structured build field into
         every crash bundle" (#658), branch improve/017-build-identity-dirty-flag,
         diff a39a9c86..ff84e1b8 (16 files, 4 commits)
Date:    2026-09-24 (review lead pass 2026-09-25)

Scope:   C# (crash report Identity, BuildStampReport), MSBuild (Directory.Build.props),
         Python tool (package_release.py and its tests), harness (release skill), docs
Waves:   wave 1: Standards, Engine compatibility, Data flow, Tooling correctness;
         wave 2: Efficiency, Completeness, Design. Codex gpt-6-astra ultra in parallel.

STANDARDS:     PASS for C# checks 1-10; 4 LOW + 3 NIT outside them (all confirmed, 3 fixed,
               1 open to Mike)
COMPATIBILITY: PASS: 0 incompatible, 7 engine APIs verified; 2 wrong toolchain claims (F1 gate
               coverage, F2 stale BuildResources comment), both confirmed and fixed
EFFICIENCY:    PASS: 0 high, 0 medium, 2 low (1 APPLY, blocked by hook: NEEDS MIKE; 1 FOLLOW-UP)
COMPLETENESS:  INCOMPLETE at review time: trunk's GameReferences.targets outside the pathspec,
               the 1.4.5 line uncovered, #658 unreferenced, a stale "only link" claim, no
               crash-report changelog line, Python coverage gaps. All but the first two fixed;
               those two are NEEDS MIKE.
DATA FLOW:     FAIL at review time: 2 gaps (orphan install files, pre-flag commits pass as clean),
               4 inconsistencies. All fixed or documented; the untracked-files one is NEEDS MIKE.
DESIGN:        5 KEEP proposals (4 APPLY, 1 FOLLOW-UP): P1 fixed as a defect, P2 applied,
               P3 and P4 NOT APPLIED (behaviour-changing: needs Mike), P5 applied as a defect fix
XML:           NOT IN SCOPE (no ModuleData or XSLT changed)
TOOLING:       FAIL at review time: 3 HIGH (by the lens rubric), 2 MEDIUM, 5 LOW, 1 FOLLOW-UP.
               All 3 HIGH fixed with tests first.
CODEX:         complete (END OF CODEX REVIEW present): 3 P2 + 1 P3 observation, 0 false positive
```

## Details

### Verification method

Every finding below was re-read against the worktree at `ff84e1b8` before it was classified.
Load-bearing ones were reproduced:

- **Gate coverage (Tooling F1, Codex 1):** `package_release.py:107-110,157-168` read one fixed
  path per module while `classify()` copies every `bin/*/*.dll`. After the fix, a read-only dry
  run on `E:\LOTRAOM_Releases\patreon\Modules` (`--modules TAOM TAOM.Dependencies --require-build
  HEAD`) exited 2 and named all seven copies, including
  `TAOM.Dependencies/bin/Win64_Shipping_Server/TAOM.Dependencies.dll: built at 428709ee3c9a`,
  a copy from a different commit than its Win64 sibling (`0c3234375806`). The old gate never read it.
- **Untracked files and git config (Codex 2, A2 O1, A3 F1, A5 F6, Tooling F4):** with an
  untracked `Main/_probe017_untracked.txt` and `GIT_CONFIG_COUNT=1
  GIT_CONFIG_KEY_0=status.showUntrackedFiles GIT_CONFIG_VALUE_0=no`, `dotnet build
  Main/TAOM.csproj` stamped `build.20260925-175905Z+ff84e1b8...` with no `.dirty` (RED). The
  proposed command (`status --porcelain --untracked-files=normal -- ... GameReferences.targets`)
  prints the `??` line under the same override and exits 0 with the not-yet-present pathspec.
- **Stamp matrix (Codex known suspects 4 to 6, A2 and A4 UNVERIFIED items):**
  `-p:EnableSourceControlManagerQueries=false` built `build.20260925-181527Z+nogit`; a normal
  build with this pass's tracked C# edits built
  `build.20260925-181530Z+ff84e1b88fe01e21fabc920dc8b922c1c474a270.dirty`. Both are now on disk
  under `E:\repos\taom-improve\scratch\review-017-lead\`.
- **Pre-flag commits (A5 F3):** `git show bannerlord-1.4.5:Directory.Build.props | grep -c
  TaomStampWorkingTreeState` prints 0.
- **BuildResources misattribution (A2 F2, A5 F5, A4 4):** `grep -rlE
  "SourceRevisionId|InformationalVersion"` over `bannerlord.buildresources/1.1.0.129/build*`
  matches nothing.
- **Worktree route in step 1 (A1 4, A5 F4, A4 6, Tooling F6):** `git worktree list` shows
  `bannerlord-1.5.x` checked out at `E:/repos/TAOM` and `bannerlord-1.4.5` at `E:/repos/taom-145`.

### Findings and classification

| # | Source | Sev | Finding | Verdict | Outcome |
|---|---|---|---|---|---|
| 1 | Tooling F1, A2 F1, A5 F2, A6 P1, Codex 1 | HIGH | `--require-build` reads only `bin/Win64_Shipping_Client/<dll>`; the Game Pass, server and Kit copies ship unread | CONFIRMED | Fixed: the gate reads every `bin/**/<dll>` in the module's copy list and requires the Win64 copy |
| 2 | Tooling F2 | HIGH | Module lookup is case-sensitive: `--modules taom TAOM.Dependencies` with a dirty TAOM.dll passes | CONFIRMED | Fixed: `SHIPPED_DLLS` keyed casefolded |
| 3 | Tooling F3 | HIGH | `--require-build ""` skips the gate (`if args.require_build:`) | CONFIRMED | Fixed: `is not None`; the empty rev fails to resolve and is refused |
| 4 | Codex 3, Tooling F9 | MEDIUM | A requested module missing from `--source` is skipped before the gate, so the rest certifies | CONFIRMED | Fixed: `require_build` takes the requested names and refuses a missing TAOM or Dependencies |
| 5 | A5 F3 | MEDIUM | No `.dirty` counts as clean even when the building commit has no dirty-flag target (1.4.5, older tags) | CONFIRMED | Fixed: the gate refuses a rev whose `Directory.Build.props` lacks `TaomStampWorkingTreeState` |
| 6 | Codex 2, A2 O1, A3 F1, A5 F6, Tooling F4 | MEDIUM | `git status` honours `status.showUntrackedFiles=no`, so an untracked source can stamp clean | CONFIRMED | NEEDS MIKE: the edit was denied by `config-protection.sh`; RED reproduced; CHANGELOG "Known limitation" |
| 7 | A4 1 | MEDIUM | Trunk's `GameReferences.targets` (imported by both csproj files) is outside the pathspec after merge | CONFIRMED | NEEDS MIKE: same file, same hook; folded into the #6 edit |
| 8 | A5 F1, Tooling F5 | MEDIUM | The gate proves the DLLs only; the additive install ships orphan files (81 or 12 found, by lens) | CONFIRMED | Doc caveat added in Phase 8 and step 8 with the `git ls-tree` comparison; automated check is NEEDS MIKE |
| 9 | A4 2 | MEDIUM | The 1.4.5 release line has none of this work | CONFIRMED | NEEDS MIKE (port); the gate now fails closed there (#5) and the docs say so |
| 10 | A2 F2, A5 F5, A4 4, A6 P5, A1 FU1 | LOW | `BuildStampReport.cs:116` and its test still credit Bannerlord.BuildResources with the SHA suffix, contradicting the corrected summary | CONFIRMED | Fixed (comment-only) |
| 11 | A1 4, A5 F4, A4 6, Tooling F6 | LOW | `release-process.md` step 1 offers a worktree of the release branch, which git refuses | CONFIRMED | Fixed: "stop"; only Phase 8 uses a detached worktree of the tag |
| 12 | A1 1 | LOW | `BuildManifest_CarriesTheTaomBuildStamp` lacks the state segment | CONFIRMED | Fixed: `BuildManifest_IdentityWithBuildStamp_PrintsTaomBuildLine` |
| 13 | A1 2 | LOW | Release skill description omits Phase 8 and is not in "Use when" form | CONFIRMED | Fixed (28 words); `test_hooks.sh` 3b frontmatter check passes |
| 14 | A1 3, A4 3 | LOW | #658 never referenced | CONFIRMED | Fixed in the CHANGELOG heading `(plan 017, #658)`; close-out is Mike's (public action) |
| 15 | A4 5 | LOW | `release-process.md:11-13` "the only link" is stale now that bundles carry the build | CONFIRMED | Fixed |
| 16 | A4 7 | LOW | No `crash-report.md` changelog line | CONFIRMED | Fixed |
| 17 | Tooling F7, A1 NIT, A4 8, Codex 4 | LOW | CLI tests error outside a git checkout; several gate branches untested | CONFIRMED | Fixed: setUp skips; 11 new tests; 13 skip cleanly outside git (run from a two-file scratch copy) |
| 18 | Tooling F8 | LOW | An unreadable DLL exits 1 with a traceback | CONFIRMED | Fixed: `cannot read` refusal, exit 2. The git-missing and several-stamps wording left as is (NOT APPLIED) |
| 19 | A1 NIT | NIT | `release-process.md:62` one 284-character line | CONFIRMED | Rewrapped |
| 20 | A1 NIT | NIT | Gate command repeated in the skill and the doc | CONFIRMED | NOT APPLIED: the skill mirrors the doc phase by phase by design |
| 21 | A1 UNVERIFIED | n/a | Approval for the original hook-protected props edit is not recorded | UNVERIFIED | NEEDS MIKE (confirm) |
| 22 | A2 and A4 UNVERIFIED | n/a | "About 40 ms per project build" | Supported | A1 timed 36 to 44 ms, A3 34 to 35 ms median through cmd.exe; kept |
| 23 | Tooling F10 | LOW | `2>nul` only works under cmd | UNVERIFIED (no Unix host) | FOLLOW-UP |

### Agent 1: Standards

C# checks 1 to 10 pass. Findings 12, 13, 14 and 11 above, the three NITs (19, 20, 17) and the
approval question (21). Harness checks H1 to H6 pass apart from finding 13.

### Agent 2: Engine compatibility

Seven engine APIs verified against the installed DLLs; no incompatibility. Claims: nine verified,
two wrong (findings 1 and 10), one conditional (finding 6), two unverified (22, and the `nogit`
builds, now proven by this pass's probe).

### Agent 3: Efficiency

No runtime cost. Finding 6 (APPLY, blocked). The per-build recompile caused by the timestamp
stamp is pre-existing and deliberate (FOLLOW-UP, no issue: #371 design).

### Agent 4: Completeness

Tests, docs, CHANGELOG and IoC present. Findings 7, 9, 14, 10, 15, 11, 16, 17, 22 above.

### Agent 5: Data flow

Twelve flows traced. Gaps: findings 8 and 5. Inconsistencies: 1, 11, 10, 6.

### Agent 6: Design and elegance

P1 is finding 1. P2 (one regex) applied. P3 and P4 not applied (behaviour-changing). P5 is
finding 10.

### Tooling correctness

Findings 1, 2, 3, 6, 8, 11, 17, 18, 4 (F9's overclaim), 23, and the FOLLOW-UP list below.

## Action items

1. Mike: approve the `Directory.Build.props` edit (findings 6 and 7). The exact line:
   `<Exec Command="git --no-optional-locks status --porcelain --untracked-files=normal -- Main Dependencies Stubs Directory.Build.props GameReferences.targets 2&gt;nul"`,
   plus the comment lines "(untracked files count, whatever status.showUntrackedFiles says;
   ignored files and skip-worktree paths do not)" and "A new file that a project imports or
   compiles from outside these paths must be added to the pathspec." Then re-run the RED probe
   above; it must read `.dirty`. Update the scope list in `SKILL.md` (Gotchas) and the CHANGELOG
   with `GameReferences.targets`, and drop the "Known limitation" bullet.
2. Mike: decide the 1.4.5 port (finding 9).
3. Mike: decide P3 and P4 (below).
4. Close #658 with `triage-needs-ingame` once merged (in-game `IdentityCollector` path owed).
5. Convergence pass (Step 4.6) on the review-fix diff: owed; this lead cannot spawn agents.

## Improvements (Step 4)

APPLIED:
- `tools/package_release.py` `STAMP_RE` (A6 P2, PRESERVING): one regex with named groups serves
  `read_build_stamp` (over a latin-1 decode of the DLL) and `check_build_stamp` (`fullmatch`);
  `STAMP_PARTS_RE` deleted. Proof: the 9 `TestBuildStamp` tests plus new
  `test_two_different_stamps_read_as_none` and `test_a_stamp_without_a_revision_is_unrecognised`,
  green before and after.
- `tools/tests/test_package_release.py` (Tooling F7, PRESERVING): skip outside a git checkout,
  and characterisation tests for "nothing to verify" and "a refused real run writes nothing"
  (both green before the gate fix and after).

NOT APPLIED:
- `Directory.Build.props:57` `--untracked-files=normal` (A3 F1, Tooling F4): denied by
  `config-protection.sh`; needs Mike's approval (action item 1).
- `IdentityCollector.cs:19` bare stamp without the frozen `v2.0.0.0 ` prefix (A6 P3):
  behaviour-changing (the `report.txt`, `manifest.txt` and `report.json` value); needs Mike.
- `PlainTextCrashReportRenderer.cs:68` label `Build:` to `TAOM build:` (A6 P4):
  behaviour-changing (report text); needs Mike.
- `package_release.py` separate wording for "git missing" and "several stamps" (Tooling F8
  remainder): both already refuse with a true message; a reworded message is a tiny win for an
  extra branch (simplicity criterion: reject).
- `SKILL.md:119` and `release-process.md:84` repeated gate command (A1 NIT): the skill mirrors
  the doc phase by phase by design.

FOLLOW-UP (pre-existing code or outside this change; no issue filed from this review lead, since
filing is a public action):
- `BuildStampReport.cs:8-24`: the class summary sits above `enum BuildPairVerdict`; move it above
  the class (A1, A2, A4, A6).
- `docs/reviews/lessons/testing-qa.md:194` BuildResources claim: correction appended as a new
  lesson in this review (see RCA), the archive entry left as written.
- `release-process.md:79-80` two steps numbered 4.
- `tools/README.md` has no `package_release.py` row; the packaging recipes in
  `docs/modding/module-map.md`, `modules-overview.md` and `recipe-new-mod-from-zero.md` never
  mention `--require-build`; their line citations into `package_release.py` are stale.
- `crash-report.md:24` and `CrashBundleWriter.cs:20` say `manifest.txt` lists SHA1s; it does not.
- Two `InformationalVersion` readers with different shapes (`BuildStampReport` and
  `MBSaveLoad_GetSaveMetaData_Patch`).
- The bundle carries only TAOM.dll's stamp; the #371 pair still needs the log line.
- `.claude/hooks/check-version-tagged.sh:9-10` repeats the "only link" claim.
- No `JsonCrashReportRenderer` tests.
- `Directory.Build.props` `2>nul` under a Unix shell (Tooling F10, UNVERIFIED).
- The per-build timestamp forces a recompile of every project (A3 F2; deliberate #371 design).
- Automated orphan-file report in the gate (TAOM.Dependencies needs an allow-list for its vendored
  MCM files); whether `--require-build` should demand both modules whenever either is planned, and
  be on by default for a real run (Tooling F9, F11).
- The live patreon package: TAOM.dll and TAOM.Dependencies.dll about 39 hours apart, and the
  Dependencies server copy from a third commit.
- The untracked Codex prompt file is committed with this review, per the `docs/reviews/*.prompt.md`
  convention.

VERDICT: NEEDS FIXES. Every confirmed defect in files this pass may edit is fixed and the suite
is green; two remain open because they live in the hook-protected `Directory.Build.props`
(findings 6 and 7, action item 1), and the convergence pass is owed.

## Verification

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (TEMP on E:): `Passed! -
  Failed: 0, Passed: 10317, Skipped: 2, Total: 10319`, exit 0.
- `python tools/tests/test_package_release.py`: RED first (`FAILED (failures=5, errors=2)` on the
  7 new defect tests), then `Ran 57 tests ... OK`. Outside a git checkout: `Ran 57 tests ... OK
  (skipped=13)`.
- Real-data dry run of the fixed gate on the patreon package: exit 2, seven copies named.
- `python tools/lint_docs.py --dash-base a39a9c86`: 0 dashes, 0 dead links, exit 0.
- `bash tools/test_hooks.sh`: 280 passed, 3 failed. Section 3b (frontmatter) passes. The three
  failures are timing: `check-version-tagged.sh` took 3449 ms against a 3000 ms budget and
  `scan.sh` hit its timeout twice; no hook or scan script is touched by this branch. Re-run on an
  idle machine.

## Codex review

`docs/reviews/raw/codex-adversarial-017-build-identity-dirty-flag-2026-09-24.md` (gpt-6-astra,
ultra; complete). Codex reviewed through git refs, quoted the vanilla and SDK targets it relied on,
traced nine scenarios and ten known suspects, and ran nothing.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | HIGH | Yes | Reproduced on the real patreon package: the Dependencies server copy is from another commit and the old gate never read it. Fixed |
| 2 | P2 | MEDIUM | Yes | RED reproduced with the config override; latent on this machine (setting unset). Fix blocked by the props hook: NEEDS MIKE |
| 3 | P2 | MEDIUM | Yes | `main` drops a missing module before the gate (`package_release.py:396-400` at ff84e1b8). Fixed |
| 4 | P3 | LOW | Yes | Coverage limits, not sham tests. Addressed by 11 new Python tests and this pass's stamp probes |

- **Confirmed bugs:** 1, 2, 3 (4 is an observation).
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Known suspects:** Codex left 2 to 6 UNVERIFIED for lack of build output. This pass built
  `+nogit`, `+<sha>.dirty` (tracked edit) and the untracked RED case; its disputes of ordering and
  fallback defects hold.
- **Things Codex missed:** the case-sensitive lookup (2), the empty `--require-build` (3), pre-flag
  commits passing as clean (5), orphan install files (8), trunk's `GameReferences.targets` (7), the
  1.4.5 line (9), the surviving BuildResources claim (10), the impossible worktree route (11), and
  the standards and completeness LOWs (12 to 16).

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| C1 | Gate reads one of four shipped copies | Logic error | The plan prescribed a fixed path per module (`plans/017-build-identity-dirty-flag.md:873-877`); nobody compared the gate's read set with the packager's copy set | Lesson "A release gate reads what the packager ships" (build-tooling-workflow); regression tests |
| C2 | Untracked files hidden by user git config | Assumed an API worked a certain way | `git status --porcelain` was treated as a fixed format; its untracked mode is configurable | Lesson "Pin every git option a gate's answer depends on" (build-tooling-workflow) |
| C3 | Requested module skipped before the gate | Missing null guard | The gate took the planned set, which planning had already filtered | Covered by the C1 lesson; regression test |

### AGENTS.md lessons (pending, Phase 3h is consolidated later)

- **Bugs Codex typically misses:** fail-open argument surfaces in Python CLIs (a case-folded name
  on Windows, an empty-string option value); a proof marker that older producers never wrote, so
  its absence proves nothing.
- **What Codex does well:** compared the gate's read set with the packager's copy set, and traced
  a module dropped by planning before validation.
- **False positives:** none new.
