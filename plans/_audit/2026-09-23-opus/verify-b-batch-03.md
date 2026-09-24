# Verify batch B-03: adversarial re-check of TEST-L5-09, DX-L6-01, DX-L6-02

Checker, run `2026-09-23-opus`, baseline `b2e387db`. The working tree HEAD is `4b5662b2` and carries another
session's edits, so every repo source here is read as `git show b2e387db:<path>` or `git grep ... b2e387db`.
No `dotnet build` or `dotnet test` was run. No hook was executed. Stored lane-5 probe trx files in the session
scratchpad (`lane5-probe\results\`) were re-read, not re-run. Own measurements ran from scratchpad scripts
(`vbb03_*.py`, `vbb03_*.sh`): hook cost components, transcript counts, a prefilter proof. GitHub reads were
read-only `gh run list/view` and `gh api` GETs.

## TEST-L5-09: mutation-testing spike on AlignmentRecruitment (design)

- **Outcome**: CONFIRMED as a design item (every repo link re-read), with four mis-cited facts corrected below.
  Impact today LOW (a spike proposal, not a defect).
- **Re-read, holds**:
  - `git ls-tree -r b2e387db -- Main/Features/AlignmentRecruitment`: 8 files; `wc -l` over `git show` of each sums to
    **244**. `git grep -E "using TaleWorlds|TaleWorlds\." b2e387db -- Main/Features/AlignmentRecruitment`: 0 hits.
  - `RecruitmentAlignmentService.cs:23-43` is `IsRecruitmentBlocked`, six `return`s (`:26,28,30,36,39,42`), pure over
    two injected interfaces.
  - `git log b2e387db -- Main/Features/AlignmentRecruitment`: 2 commits, both 2026-06-17 (`0c5c7b6d`, `3d3ba4aa`).
  - Neither test file carries a `TestCategory` (grep `RequiresGame`: 0); neither class appears in the lane's
    `requiresgame_notw.txt` / `requiresgame_refasm.txt` (grep: 0).
  - `Main/TAOM.csproj:7` sets `<ModuleId>$(MSBuildProjectName)</ModuleId>` unconditionally, so a Stryker-driven
    MSBuild build would deploy without a global `-p:ModuleId=`; `TAOM.sln` exists at the root.
  - Stryker docs (fetched this run, stryker-mutator.io configuration page): "The solution file is required for dotnet
    framework projects", test-runner default `vstest`, `mtp` in preview. As cited.
- **Corrected evidence**:
  1. **Tool version**: "Stryker.NET 4.16.0 is current (updated 2026-09-11)" is wrong. The nuget registration
     (`api.nuget.org/v3/registration5-gz-semver2/dotnet-stryker/index.json`) lists 4.16.0 published 2026-07-03 and
     **5.0.0 published 2026-09-11** (listed). The 2026-09-11 date the lane saw is 5.0.0. The 5.0.0 release notes list
     breaking changes "Target dotnet 10 runtime" (#3571; this desktop has SDK 10.0.401, so not a blocker) and a
     baseline-path change, and add `perTest` coverage for MTP (#3752), which also dates the lane's "MTP runner does not
     combine mutants" remark. The spike should pin 5.0.0 or say why 4.16.0.
  2. **Consumers**: only `TaomVolunteerModel` consumes the service
     (`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs:17,24,50`, resolved at `Main/SubModule.cs:1026`).
     `WandererAllegianceService.cs:22` and `MarriageAlignmentService.cs:8,15` only mention it in doc comments (they read
     the same `IAlignmentService` table). `git grep -E "IRecruitmentAlignmentService|IsRecruitmentBlocked"` outside the
     feature folder: 5 hits, 4 in `TaomVolunteerModel.cs` and 1 in `SubModule.cs`. The "surviving mutant is a real recruitment bug" argument still
     holds through the volunteer model alone.
  3. **Test rows**: `RecruitmentAlignmentServiceTests` yields **26** results (two `DataTestMethod`s x 9 `DataRow` +
     8 `TestMethod`), not 28; with the config provider's 8 the total is **34**, not 36. Confirmed in the stored
     `lane5-probe\results\f1_layout_envset.trx` (26 `TestMethod` elements for the service class, 8 for the provider).
     The lane counted the two `DataTestMethod` headers as rows.
  4. **Runner-up**: `TAOM.Tests/Core/Validation` yields **41** results (16 + 15 + 10 data rows in `EnumNamesTests`),
     not 43 (same double count), per the same trx.
- **Not in the lane, worth a line in the spike**: `RecruitmentAlignmentSettingsProvider.cs` (in the mutate glob) has no
  direct test (no test file names it), so its mutants will read as NoCoverage; and the full suite at `b2e387db` is not
  green (`lane5-probe\f1.log`: 14 failures, Animalia and others), which the initial Stryker run will meet unless the
  `test-case-filter` variant runs first.
- **By-design check**: no ADR, rule or trap-index row rejects mutation testing; `docs/reviews/lessons/testing-qa.md:38`
  already recommends hand mutation testing, so the spike extends a recorded practice.

## DX-L6-01: commit gates bind only Claude; the one CI is red

- **Outcome**: CONFIRMED. Impact today MED (process and triage cost; no runtime defect). Two counts corrected, one
  strengthening fact added.
- **Re-read, holds**:
  - Registrations (`git show b2e387db:.claude/settings.json`, parsed): 10 `PreToolUse` `Bash` + 1 universal
    `suggest-compact.sh` + 2 `PostToolUse` `Bash` = 13 Bash-path hooks. `check-commit-subject-version.sh` is 13,703 B
    (`git cat-file -s`).
  - Workflows at `b2e387db` are only `build.yml` and `doc-budget.yml`. `build.yml:3-8` triggers push and PR on
    `bannerlord-1.4.5` only (plus `workflow_dispatch`); the native-CRT step is `build.yml:81-102`.
    `git grep -E "validate_moduledata|audit_polearm_shield_parity" b2e387db -- .github/workflows`: 0 hits; no subject
    or commit-message step exists. `doc-budget.yml:3-7,9-12` runs on every push and PR and records the IDE-commit
    precedent verbatim.
  - Deny text `check-commit-subject-version.sh:262-263` ("this gate sees only the commits Claude runs");
    `.claude/rules/harness-facts.md:105-108` ("Both fire only on Claude-driven commits").
  - All five "the pre-commit hook" lines read as cited (`external-skill-ports.md:123`, `finish-branch/SKILL.md:48`,
    `deep-review/lenses/1-standards.md:35`, `completion-workflow.md:99`, `external-repo-adoption.md:33`).
  - `git config --show-origin --get-all core.hooksPath`: `file:.git/config c:\Users\mikew\source\repos\TAOM\.git\hooks`;
    `ls` of it fails; `.git/hooks` holds only `*.sample`. As cited.
  - `git rev-list --count a4c6d7c3..b2e387db` = 95; the regex leaves exactly the ten cited misses; 17 subjects exceed
    72 characters (Python `len`, so not a byte artifact); long body lines 7 of 10 misses vs 19 of 85 conforming
    (re-computed, identical).
  - `8dabf4a6` emits the top-level `permissionDecision` form (`:73`), `b2e387db` has 2 `hookSpecificOutput`, and
    `harness-facts.md:61` records the top-level form is ignored. The lane's own caveat is accurate.
  - `c79a5852`: 208 files, 9,534 insertions (`git show --stat`); it adds `docs/adrs/011-knowledge-delivery-tiers.md`
    (`git log --diff-filter=A`).
  - `gh` read-only: both branches `protected: false`, rulesets 0. `gh run list --workflow "Build & Test" --limit 40`:
    40 of 40 `failure`, all `bannerlord-1.4.5`, 2026-09-07 to 2026-09-15. Run `35005853072` jobs: Python Tool Tests
    and Validate XML & XSLT `failure`, Build & Test `skipped`, Hook Harness and Check Build Configuration `success`;
    the failed log carries `No module named 'pytest'`, `'lxml'`, `Path.read_text() got an unexpected keyword argument
    'newline'` and "the documentation graph got structurally worse".
  - At `b2e387db`: 5 `tools/tests` modules `import pytest` (e.g. `test_analyze_melee_ladder.py:15`), 8 tools import
    `lxml`, 4 call `read_text(..., newline="")` (`generate_lord_template_equipment.py:98`, `translate_with_claude.py:748`,
    two under `tools/oneoff/`). `build.yml:202-204` still says "zero pytest imports ... no pip install"; the
    "failing since 2026-08-28" comment is in the `hook-harness` job (`build.yml:162-163`) and refers to the doc-graph
    ratchet, as the lane says.
- **Corrected evidence**:
  1. The red streak is longer than 40: `gh run list --limit 300` shows the last `success` at 2026-09-01T16:53Z and
     **82** consecutive failures since (the lane's 40 is only the window it listed).
  2. `c79a5852` touches **19** files under `.claude/skills/` and **24** under `Main/Features/` (`git show --name-only`),
     not 17 and 15 (13 and 8 distinct directories). Hooks 12 and rules 11 are right.
- **Strengthening fact the lane missed**: two ADRs already promise a git pre-commit hook that has never existed.
  `docs/adrs/003-no-regions.md:119-121` ("Pre-Commit Hook ... Installed via: `./scripts/install-pre-commit-hook.ps1`")
  and `docs/adrs/008-testability-requirements.md:236-240`; `git log --all -- scripts/install-pre-commit-hook.ps1` is
  empty, so the installer was never committed on any ref.
- **By-design check**: not a decided tradeoff. `AGENTS.md:106`, `.ai/scopes.md:25` ("Hooks do not enforce other
  clients") and `.ai/policy.md:73-78` ("Actual merge enforcement needs a trusted controller ... not installed or enabled")
  acknowledge the gap; none rejects committed git hooks or a CI subject check. Fail-open hooks (BRIEF) are respected by
  the fix sketch. The `core.hooksPath` half is local config and the user's to fix (`environment-failures.md`), as the
  lane says.

- **Extension (PLAUSIBLE, harness behavior not exercised)**: the gates do not bind every Claude path either. Every
  commit gate is registered with matcher `Bash` only (parsed `settings.json`: no `PowerShell` matcher, no universal
  gate); this very session runs with the native PowerShell tool enabled (`docs/reference/powershell-tool.md` documents
  the opt-in), so a `git commit` issued through that tool would reach no subject, CHANGELOG, tracked-files or ModuleData
  gate. CLAUDE.md "MCP and shell" names the git and filesystem MCP bypass but not this one. A committed `commit-msg` /
  `pre-commit` hook (the lane's fix) closes it too, which strengthens the case.

## DX-L6-02: bash-only prefilter in front of the Bash hooks

- **Outcome**: CONFIRMED on the cost; the proposed Step 1 prefilter is REFUTED as "a strict superset that changes no
  decision". As written it would silently skip every commit, push and destructive-git gate for a multi-line command
  whose `git` starts a line. Impact today LOW (latency only); impact of shipping the fix as specified MED (gate bypass).
- **Re-read, holds**:
  - All 10 `PreToolUse(Bash)` gates and both `PostToolUse(Bash)` hooks `source .../_pybin.sh` before reading or
    testing the command (e.g. `block-no-verify.sh:4` then parse `:34-44` then the git test `:50`;
    `check-changelog-changed.sh:17`, parse `:25-32`, test `:36-41`; same shape in the other eight).
  - `_pybin.sh:146` runs `PYBIN=$(taom_resolve_python || true)` at file scope (not the last line: `:147,156` export
    after it); `:98` validates then probes the pin; `:88-90` is the `timeout -k 0.2 0.8 ... -S -E -c` probe. The
    "~77ms per hook, ~850ms of CPU across the ten" note is `_pybin.sh:141-145`. As cited.
  - `harness-facts.md:63` (parallel hooks) and `:64` (the `if:` migration deferred because "a mis-scoped `if:`
    silently disables a gate, so it needs a per-gate proof").
  - Timing, re-measured with components only (no hook run; scratchpad `vbb03_timing.sh`, 5-run means, pinned
    `C:/Python314/python.exe`): bare bash 27 ms (lane 29), bash + `source _pybin.sh` 98 ms (lane 100), plus one
    Python JSON parse 143 ms (lane about 163), ten `source+parse` copies in parallel 220 and 235 ms wall. The order of
    magnitude reproduces.
  - Transcript scale (scratchpad `vbb03_prefilter.py`, 20 newest main transcripts, counts only): 5,754 Bash calls;
    `846fe3ce` 2,173, then 836, 669, 358, as cited. First word `git` (or `cd ... && git`): 7.5%, inside the lane's
    5 to 12%.
- **Refuted: the superset claim**. The harness sends the command JSON-encoded, so a newline before `git` arrives as
  the two characters `\n`, and the `n` is alphanumeric, which the proposed leading class `[^[:alnum:]_.-]` rejects.
  Proof (scratchpad `vbb03_prefilter_proof.sh`, the lane's exact bash regex): the payload
  `{"tool_input":{"command":"cd /e/repos/TAOM\ngit commit -m x","description":"Commit the fix"}}` prints SKIP, and so
  do `...\ngit push --force origin bannerlord-1.5.x` and `...\n\tgit reset --hard`; the same decoded command matches
  today's commit-gate glob `*"git commit"*` (`check-commit-subject-version.sh:54-55`) and block-no-verify's
  `(^|[[:space:]])git([[:space:]]|$)` (`block-no-verify.sh:50`, newline is `[[:space:]]`); `block-dangerous-git.sh:64,74`
  and `block-broad-git-add.sh:73,83` also see each line as its own segment. In the 5,754 retained calls, 35 git segments
  were led by a newline with no other `git` token in the payload (`diff` 28, `status` 3, `mv` 2, other 2), none of them
  gated, so no past decision would have flipped; the hole is in the design, not in history. The same reasoning the
  repo used to defer `if:` applies: the prefilter needs a per-gate proof too.
- **Corrected evidence and fix**:
  1. Use a plain substring test on the raw payload (`[[ "$INPUT" == *git* ]]`, and `*dotnet*|*build.ps1*` for the
     PostToolUse pair). Every current trigger needs the literal `git` in the decoded command, and JSON encoding does
     not escape ASCII letters, so this one is a true superset. Pass rate on the same 5,754 calls: 17.8% versus 16.5%
     for the lane's regex.
  2. Savings: the prefilter would skip about 82% of Bash calls, not "about 90%" (16.5 to 17.8% pass it, because the
     token also appears in descriptions and mid-pipeline), so the lane's per-session totals shrink by roughly a tenth.
  3. `suggest-compact.sh` is not pure bash on Bash calls: `:77-81` sources `_pybin.sh` and spawns Python on **every**
     Bash call, then tests for a boundary, so 13 hooks (not 12) pay the probe and parse per Bash call; it can take the
     same prefilter.
  4. The lane's prefilter timing (62 to 72 ms alone) includes `INPUT=$(cat)`, an external `cat` spawn; the regex test
     inside one bash is 35 ms here, so `read -r -d '' INPUT` (a builtin) would cut the floor further. Minor.
- **By-design check**: fail-open hooks are a decided tradeoff (BRIEF); a skip on the non-git path is fail-open and
  compatible. Nothing decides against a prefilter. The deferral of `if:` (`harness-facts.md:64`) is the relevant
  precedent and argues for the same live per-gate proof the lane reserved for Step 2.

## What I did not cover

- No `dotnet build`, `dotnet test`, and no hook script was executed; hook cost was re-timed from its components
  (bash spawn, sourcing `_pybin.sh`, one Python parse), not from the ten gate scripts, so the lane's 383 ms and 267 ms
  gate-level figures were not reproduced directly.
- Stryker was not installed or run; its .NET Framework viability on this csproj stays UNVERIFIED (the spike's own
  question). Only the nuget registration, the 5.0.0 release page and the configuration docs page were read.
- `gh` reads were limited to the run list, one run's jobs and failed log, branch protection and rulesets.
- The PowerShell-tool bypass in DX-L6-01 rests on the matcher list in `settings.json` and the tool being present in this
  session; no commit was attempted through it.
- Transcript analysis covered the 20 newest main-session JSONL files only (the lane's window); subagent transcripts
  and older sessions were not scanned. Command text was not copied into this file beyond the counts.
