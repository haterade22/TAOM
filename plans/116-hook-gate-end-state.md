# Plan 116: Transplant the 1.5.x git-gate end state (both shells, Python push judge, visible reminders, config and dangerous-git guards)

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. The plan has three Parts (A, B, C); each is landable on its own and has
> its own done criteria. Part A needs the maintainer's settings edit M1 before its first commit;
> Part C needs M2 before commit C2. When done, do not edit `plans/README.md` (it belongs to the
> integrator): report status, commit hashes and the measured counts in your hand-off. No old-plan
> row is affected by this WP.
>
> **Drift check (run first, Step A0 shows how)**:
> `git -C <worktree> diff --stat 1b00d1acd..HEAD -- .claude/hooks .claude/settings.json .claude/rules/hook-authoring.md .claude/rules/harness-facts.md .claude/rules/external-skill-ports.md .claude/skills tools/test_hooks.sh tools/tests tools/audit_claude_config.py .mcp.json CLAUDE.md docs/reference/hooks-catalog.md docs/reference/mcp-servers.md docs/reference/development-machines.md docs/reference/provenance-register.md docs/modding/validation-and-testing.md docs/ai-includes/external-repo-adoption.md`
> WP101, WP102 and WP103 change many of these files on purpose. Compare the live files with the
> "Current state" facts below and STOP only on a real mismatch (a Step A0 check that fails).

## Status

- **Priority**: P2
- **Effort**: L (three Parts; Part A is the long one)
- **Risk**: MED. A broken gate fails open in silence, so every gate change lands with the 1.5.x test
  rows that pinned it. Nothing touches game code, game data, `Main/` or `Dependencies/`.
- **Depends on**: WP103 (executor-safety harness), and through it WP102 and WP101. Assumes all three
  are merged into local `bannerlord-1.4.5`.
- **Category**: dx (safety gates)
- **Wave / lane**: wave 1, lane H (harness)
- **Executor**: laptop (one session per Part, serial, own worktree)
- **Decisions applied**: D1 (commit body is the changelog entry; never stage `CHANGELOG.md`),
  D3 (Serena edit-tool denies stay as WP103 set them), D7 (no co-op content), D8 (every subject
  carries `v2.0.28`), D10 (an ordinary session executes this file), D19 (git through the Bash tool
  until this WP lands; the `Bash|PowerShell` matcher itself was WP103's)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. Transplant pin: 1.5.x `88e805450` (2026-10-16).
- **Issue**: Port of #661 (plan 013), #682 (plan 027), #680 and #689 (push judge), #677 (ADR path
  normalisation). Plan 011, the ECC re-review items and the session-scoped config override carry no
  issue number: the orchestrator creates one 1.4.x tracking issue for WP116 before implementation
  lands.

## Why this matters

After WP103 the 1.4.x gates speak in the form Claude Code reads, survive a `cd`, and are registered
for the PowerShell tool. What they still get wrong:

1. **Every gate reads a PowerShell command as Bash text.** A PowerShell here-string commit message
   reads as the subject `@` (a wrong deny), and `GIT reset --hard`, `& 'C:\...\git.exe' stash drop`
   or a backtick-continued flag pass the confirm gate (1.5.x plan 027). Sessions on this laptop offer
   PowerShell as the primary shell, which is why D19 keeps executors on the Bash tool until this WP.
2. **The push guard reads one line.** `validate-push.sh` judges only the first line of a command
   (`read -r -a TOKENS <<< "$CLEAN"`, 1.4.x validate-push.sh:48), only fully spelled force options and
   no `--mirror`: `cd x` followed by a trunk force push on line 2 passes, and so do `--force-w`,
   `--mir` and a forced `:` matching push (batch 6 safety finding; #689).
3. **Reminders never arrive.** The Stop reminders print to stderr and exit 0, which Claude never sees
   (hooks docs; D2 report 2.2 G), and a failing build raises PostToolUseFailure, where
   `mark-verification-run.sh` is not registered, so a red build never counts as verification.
4. **Two guards have holes.** `config-protection.sh` names its override file from `CLAUDE_SESSION_ID`,
   which hooks never receive (1.4.x config-protection.sh:40), so one `/tmp/claude-config-override-none`
   unlocks protected edits for every session, and its ADR guard matches only `/docs/adrs/` while the
   Edit tool passes Windows paths (#677). `block-dangerous-git.sh` misses `switch --discard-changes`,
   `worktree remove --force`, `reflog expire`, `update-ref -d` and git's long-option prefixes.
5. **Cost.** Each Bash-matched hook starts Python twice before reading the command (1.5.x measured
   256 to 451 ms per hook on an `ls`, 60 to 150 ms with the raw-payload prefilter), and three
   registered hooks (`notify-csharp-edit.sh`, `notify-test-results.sh`, `suggest-compact.sh`, the last
   on every tool call) only ever print to stderr on exit 0.

When this lands, git through either shell tool is judged the same way, the push guard refuses every
force-push shape 1.5.x pinned in tests, reminders reach Claude, and D19's Bash-only rule can lift
once the maintainer's live checks pass.

## Current state

### What WP102 and WP103 leave behind (assumed; Step A0 proves each item)

| # | Expected in the base branch | Owner (1.5.x source) |
|---|---|---|
| P1 | `check-changelog-changed.sh`, `check-changelog-updated.sh` and their registrations deleted | WP102 (`c91bb95f8`) |
| P2 | every `settings.json` hook command anchored on `"$CLAUDE_PROJECT_DIR"` | WP103 (`7b342ed59`, maintainer diff) |
| P3 | the nine git gates in one PreToolUse group whose matcher names both the Bash and the PowerShell tool | WP103 (D19) |
| P4 | no hook prints a top-level `permissionDecision`; gates nest it in `hookSpecificOutput` | WP103 (`c79a58521` harness half) |
| P5 | `_pybin.sh` exports `PYTHONIOENCODING=utf-8` | WP103 (`c79a58521`) |
| P6 | the worktree-aware reader: `taom_git_repo_dir` (with `taom_is_project_repo`, `taom_deny_multi_repo`) defined and called by the commit gates | WP103 (`12d96adc1`, `7e26d77fe`, `407570ece`) |
| P7 | `validate-push.sh` protects `bannerlord-1.5.x` too | WP103 |
| P8 | no here-string in any hook script; `decision_of` defined in `tools/test_hooks.sh` | WP103 (`143f0fa85`, `c79a58521`) |
| P9 | `python tools/lint_docs.py --fail-on-drift` exits 0 (CLAUDE.md under its 46,000 B cap) | WP103 |
| P10 | `.claude/hooks/_shellwords.py` and `tools/tests/test_shellwords.py` exist, written verbatim from `407570ece` (whose copies equal the pin), so Step A2 is expected to be a no-op | WP103 Step B4 |
| P11 | `tools/test_hooks.sh` sections from WP102: 5b2; from WP103: 4b, 4e (from `143f0fa85`), 5c, 5c2, 5c3, 7b, **7c** (WP103's own six-row push section), 7h (Bash rows only), 7j, **7k** (settings matcher, Serena denies, attribution) | WP102, WP103 |
| P12 | `tools/lint_docs.py --drift-only` exists (ported without the ADR-011 budget parts) and `check-doc-config-drift.sh` calls it; the banner in `session-start.sh` names the five python-only gates by file name | WP103 Step B1, WP102 |

These rows follow the sibling plans `plans/102-changelog-at-release.md` and
`plans/103-executor-safety-harness.md` as written on 2026-11-04; Step A0 checks the live tree, not
the plans.

### What `origin/bannerlord-1.4.5` has at 1b00d1acd (verified at planning)

- Every file under `.claude/hooks/` is byte-identical to the fork point `a4c6d7c30`
  (`git diff --stat a4c6d7c30 origin/bannerlord-1.4.5 -- .claude/ tools/test_hooks.sh CLAUDE.md`
  lists only `hook-authoring.md`, `provenance.md`, `verify/SKILL.md`, 2 lines of CLAUDE.md and 4 of
  `test_hooks.sh`). None of
  `_shellwords`, `_pushjudge`, `_stop_reminder`, `taom_hook_command`, `taom_stop_block`, `is_interrupt`,
  `long_is`, `NORM_PATH`, `CLAUDE_CODE_SESSION_ID`, `additionalContext`, `user_targets` or
  `EXIT_UNCHECKED` exists anywhere in `.claude`, `tools`, `docs/reference`, `CLAUDE.md` or `.mcp.json`
  (`git grep`, 0 files each).
- `.claude/settings.json`: PreToolUse group `"matcher": "Bash"` holds the ten git gates (:28-86),
  validate-push at `"timeout": 5` (:37-41); a `""` group runs `suggest-compact.sh` (:97-106);
  PostToolUse `Edit|Write` holds `notify-csharp-edit.sh` (:124) and the polearm hook; PostToolUse
  `Bash` holds `notify-test-results.sh` (:140) and `mark-verification-run.sh` (:145);
  PostToolUseFailure holds only `mcp__.*`. `enabledPlugins` has `github@claude-plugins-official: true`.
- `validate-push.sh` (131 lines): first-line read at :48; `is_protected` names master, main,
  bannerlord-1.4.5 (:113-118); exit 2 at :121-124.
- `config-protection.sh`: override file from `CLAUDE_SESSION_ID` (:40); `basename` (:47); ADR test on
  `*"/docs/adrs/"*` (:70).
- Stop hooks print to stderr: `check-deep-review.sh:28`, `check-verification-evidence.sh:46`,
  `check-version-tagged.sh:49`.
- `block-dangerous-git.sh` arms: reset, clean, branch -D, stash, checkout, restore (:82-102).
- `.mcp.json` declares `github` (:18-21), `filesystem` with desktop `E:\` paths (:22-33) and `git`
  (:34-39). No skill, agent or rule calls `mcp__git__`, `mcp__filesystem__` or `mcp__github__`
  (`git grep` over `.claude/`, `.ai/`, `.agents/`, AGENTS.md, CLAUDE.md, docs/ai-includes: only the
  nine deny entries in the tracked `.claude/settings.local.json`).
- `tools/audit_claude_config.py` and `tools/tests/test_audit_skillspector.py` equal the fork; the
  1.4.x `tools/lint_docs.py` has no `markdown_imports` function and no `--drift-only` flag (it has
  `--fail-on-drift`, `--summary`, `--report`, `--dash-base`; WP103 adds `--drift-only`, not
  `markdown_imports`).
- No C# test reads a hook file, `settings.json` or `.mcp.json` (only comments and the CLAUDE.md
  repo-root probe).
- Laptop tools (this session): GNU bash 5.3.9 (cygwin build), git 2.54.0, jq 1.8.2 (WinGet),
  Python 3.14.7 at `C:/Python314/python`. `*.sh` are `text eol=lf` in `.gitattributes`; `*.py` are not
  pinned (core.autocrlf is true), which Python tolerates.

### The end state and its pin

Every gate file this plan copies is the 1.5.x blob at **`88e805450`** (2026-10-16), the state right
after the config-override fix. Verified per file with `git log d01bddf10..88e805450`:

- `validate-push.sh`, `_pushjudge.py`, `block-no-verify.sh`: no change after `d01bddf10` (the end of
  the push-judge series).
- `block-dangerous-git.sh`: only `fdf9f38cb` (this pack). `check-polearm-shield-parity.sh`,
  `mcp-health-mark.sh`: only `fdf9f38cb`. `config-protection.sh`: only `88e805450`.
- `_shellwords.py`, `_pybin.sh`, `block-broad-git-add.sh`, `check-commit-subject-version.sh`: only
  WP103's reader commits (`12d96adc1`, `7e26d77fe`, `407570ece`), so the pinned copies are a superset
  of what WP103 brings.
- `check-claude-files-tracked.sh`: WP103's reader commits plus `e4c43e59e`, whose only change is `js`
  in the extension list (harmless here; WP138 owns the rest of that commit).
- Between the fork and the pin the copied files were otherwise changed only by `c79a58521` (WP103's
  harness half), `143f0fa85` (WP103), `c91bb95f8` (WP102) and this pack's commits, plus
  `fa8c06f82` in `check-deep-review.sh` (see A4).
- Later 1.5.x edits to the same files belong to WP138 (`caa7ac356`, `09f3126a7`) and WP143
  (`9cfcadd07`, `434d1d795`); they apply on top of this state.

**Cherry-pick simulation (run 2026-11-04,
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`):**
only `5dcef67ac` and `b23dc8999` merge clean. Conflicted paths, bookkeeping included:

| Commits | Conflicted paths |
|---|---|
| `90141357d`, `ee1929fa4` | `tools/test_hooks.sh`, CHANGELOG, the catalog and review docs |
| `787fd3663`, `7aa658e3e`, `6514fdfe7` | all 12 prefiltered hook files, `tools/test_hooks.sh`, hook-authoring.md or the catalog, review docs |
| `43e6780e4` | `check-deep-review.sh`, `notify-test-results.sh`, `suggest-compact.sh`, `validate-push.sh`, harness-facts.md, hook-authoring.md, CLAUDE.md, catalog, `tools/test_hooks.sh` |
| `c80c4108c`, `65b405e42`, `18961c1ec`, `764ab0984` | `_stop_reminder.sh`, the Stop hooks, `mark-verification-run.sh`, `validate-push.sh`, `tools/test_hooks.sh`, harness-facts.md, catalog, review docs |
| `e046bdf09` | `_pybin.sh`, `block-broad-git-add.sh`, `block-dangerous-git.sh`, `block-no-verify.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`, `check-moduledata-validation.sh`, `tools/test_hooks.sh` |
| `3a0614061`, `343c6f488`, `cfd3b48f6` | `validate-push.sh` or `mark-verification-run.sh`, `tools/test_hooks.sh` (`343c6f488` also the catalog) |
| `64b9f3a6c`, `747b6dae8`, `60d85ed7d`, `a4ba6b0b3`, `fd1d5c17d`, `fff6d66e9`, `d01bddf10` | `_shellwords.py`, `_pushjudge.py`, `_pybin.sh`, gates, `validate-push.sh`, `session-start.sh` (a4ba6b0b3), `tools/test_hooks.sh`, `test_shellwords.py`, `test_pushjudge.py`, rules and lessons |
| `b34d615a0`, `2d7770b64`, `6fe4c8fe4` | docs only (catalog, harness-facts.md, mcp-servers.md, review docs, plans files) |
| `fdf9f38cb` | 22 paths: `.claude/settings.json`, `.mcp.json`, `lords.xml`, `tools/audit_claude_config.py`, `test_audit_skillspector.py`, `tools/test_hooks.sh`, two rules, three skills and docs |
| `88e805450` | `tools/test_hooks.sh`, the catalog, `execute-and-review.md` (absent here) |
| `37ff36922` | the upstream-watch files (absent here), plus the catalog, feature-map.md and `tools/README.md` |
| `30abf550b` | 52 paths (PatchShield, validator, troll and formation code, docs); only its config-protection hunk is taken |

None of these is resolved commit by commit: each file takes its pinned blob or its delta from the
table below, which is the series end state the conflicts would converge to.

Not copied, because their pinned copies carry things this line lacks:
`check-moduledata-validation.sh` (1.5.x-only validator codes and `generate_career_kits.py`),
`check-doc-config-drift.sh` (it reads ADR-011 "Context budget" sections and a wider trigger list;
WP103 ports `--drift-only` without those parts and keeps the 1.4.x heading and list),
`check-native-dll-crt.sh` (deleted on 1.5.x by `ad1992b5b`; it stays here), `session-start.sh` and
`detect-docs-gaps.sh`. Those get small deltas (G1 to G5, S1).

### File-by-file transplant table

| File | Step | Method | Source | Adaptation |
|---|---|---|---|---|
| `.claude/hooks/_shellwords.py` | A2 | copy | `88e805450` | none |
| `tools/tests/test_shellwords.py` | A2 | copy | `88e805450` | none |
| `.claude/hooks/_pybin.sh` | A3 | copy | `88e805450` | none |
| `.claude/hooks/_pushjudge.py`, `tools/tests/test_pushjudge.py` | A3 | copy | `88e805450` | none |
| `.claude/hooks/validate-push.sh` | A3 | copy | `88e805450` | line 3: `AGENTS.md "Git and commits"` becomes `CLAUDE.md "Commits"` |
| `block-no-verify.sh`, `block-broad-git-add.sh`, `check-claude-files-tracked.sh`, `mark-verification-run.sh` | A3 | copy | `88e805450` | none |
| `check-commit-subject-version.sh` | A3 | copy | `88e805450` | `AGENTS.md "Git and commits"` (line 25) and `AGENTS.md 'Git and commits'` (lines 219, 264) become `CLAUDE.md "Commits"` / `CLAUDE.md 'Commits'` |
| `block-dangerous-git.sh` | A3, then B1 | copy | `fff6d66e9` in A3, `88e805450` in B1 | none |
| `check-doc-config-drift.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh` | A3 | deltas G1 to G5 | | |
| `session-start.sh` | A3 | delta S1 | `a4ba6b0b3` | |
| `detect-docs-gaps.sh` | A3 | delta G4 | `fff6d66e9` | |
| `_stop_reminder.sh` | A4 | copy | `88e805450` | "the four Stop reminder hooks" becomes "the three Stop reminder hooks" |
| `check-deep-review.sh`, `check-verification-evidence.sh`, `check-version-tagged.sh` | A4 | copy | `88e805450` | none |
| `suggest-compact.sh`, `notify-test-results.sh`, `notify-csharp-edit.sh` | A1 | `git rm` | | |
| `check-polearm-shield-parity.sh`, `mcp-health-mark.sh` | B2 | copy | `88e805450` | none |
| `config-protection.sh` | B3 | copy | `88e805450` | none |
| `tools/test_hooks.sh` | A1, A3, A4, B1 to B3 | section transplant | `88e805450` | per step (T1 to T12) |
| `tools/audit_claude_config.py`, `tools/tests/test_audit_skillspector.py` | C1 | copy, then remove `c79a58521`'s part if the base lacks it | `fdf9f38cb` | |
| `tools/tests/test_audit_user_scope.py` | C1 | copy | `fdf9f38cb` | none |

### Engine members bound

None. Every file here is a Claude Code hook, a Python helper, a test, a settings file or a doc. No
TaleWorlds type is called, so no `taom-src` lookup applies and `/verify-bindings` is not needed.

### Conventions that bind this work

- **No C# changes**, so ADR-007 (services use adapters, never sealed TaleWorlds types), ADR-002 (thin
  entry points under 150 lines), ADR-003 (no `#region`), ADR-004 (no `[Obsolete]`), ADR-005 (no
  `#if DEBUG` outside IoC registration), the MCM json2 rule (rename a persisted setting, never flip
  its default) and `.claude/rules/harmony-patches.md` (MovementOrder postfixes through the shared
  deferred category) do not apply. `Main/IoC.cs`, `Main/SubModule.cs` and `Main/TAOM.csproj` are not
  touched.
- **TDD**: each step adds or copies its tests first, shows them failing (RED), then lands the hooks
  (GREEN).
- `.claude/rules/hook-authoring.md`: mirror a sibling's full convention set; fail open but never fail
  silent; size a timeout against the slow path and bound work inside the script; never a here-string;
  never spell `python3`.
- `.claude/rules/harness-facts.md`: TAOM hooks fail open; PreToolUse decisions go in
  `hookSpecificOutput`.
- **Never turn a gate green by editing it**: no deleting or loosening a test row, threshold or
  allowlist to make it pass. The only test edits allowed are the adaptations this plan lists.
- **Decision ids** such as D29, D30, D38 to D42 and "decision 61" inside copied hook and test comments
  are 1.5.x plan-record ids; leave them verbatim (A5 adds one catalog note saying so). They are not
  this programme's D1 to D24.
- Prose you write (commit bodies, docs): no em or en dash characters; hyphens in flags and paths are
  fine (`.claude/rules/output-style.md`).

## Commands you will need

| Purpose | Command (Bash tool, from anywhere) | Expected |
|---|---|---|
| Restore | `cd <wt> && dotnet restore TAOM.sln` | exit 0 |
| Build | `cd <wt> && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 Patch88 warning is a known false positive) |
| Test | `cd <wt> && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total equals the count the previous merged WP recorded in `plans/README.md`, read from the main checkout (this WP adds no C# test) |
| Hook suite | `source <notes>/wp116.sh && hooks <name>` | summary line `N passed, 0 failed`, or only the failures recorded in the A0 baseline |
| Tool tests | `source <notes>/wp116.sh && pyt` (one file: `pyt test_shellwords.py`) | `OK`, or only baseline failures |
| Docs | `cd <wt> && python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Docs report | `cd <wt> && python tools/lint_docs.py --dash-base bannerlord-1.4.5 --report <notes>/lint-<step>.md` | no new finding naming a file you touched; no em or en dash in a line you added |
| Security | `cd <wt> && python tools/audit_claude_config.py --min HIGH` | no HIGH or CRITICAL finding the A0 baseline lacks |

`<wt>` is `C:/Users/mikew/source/repos/taom-port/116`; `<notes>` is
`C:/Users/mikew/source/repos/taom-port/116-notes` (outside every repository: helper scripts, probe
payloads, logs and commit-message files live there). Both `-p:` flags are mandatory on every dotnet
build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not gate the CopyModule target, so a build
without `-p:ModuleId=` deploys into the game install. Never `./build.ps1`.

The hook suite's runtime on this laptop is UNVERIFIED (the pinned file has about 2,870 lines against
509 today, and section 7i alone sends 24 payloads of up to 2 MB, each up to twice). Run `hooks` as a background Bash
call (`run_in_background: true`), wait for the completion notice, then read
`<notes>/hooks-<name>.log`. A row whose label measures time may fail once under load: re-run the
suite once; if the same timing row fails twice, report the measured numbers, never change the bound.

### Helper file `<notes>/wp116.sh` (create with the Write tool in Step A0)

```bash
# WP116 helpers. In every Bash call: source C:/Users/mikew/source/repos/taom-port/116-notes/wp116.sh
# Paths stay in C:/ form. MSYS_NO_PATHCONV=1 is set only on git reads of REF:PATH (Git Bash mangles
# those otherwise); it must never reach the hook suite or a hook, which pass /tmp paths to Windows
# programs and need the conversion.
WT=C:/Users/mikew/source/repos/taom-port/116
NOTES=C:/Users/mikew/source/repos/taom-port/116-notes
PIN=88e805450
# gshow REF PATH: print PATH as it is at REF
gshow() { MSYS_NO_PATHCONV=1 git -C "$WT" show "$1:$2"; }
# blob REF PATH: overwrite the worktree file PATH with its blob at REF (bytes as stored, LF)
blob() { gshow "$1" "$2" > "$WT/$2"; }
# sec REF LABEL: print one tools/test_hooks.sh section at REF, from the comment block above its
# head2 line through its last line (the next section's comment block is excluded)
sec() {
  gshow "$1" tools/test_hooks.sh | awk -v L="head2 \"$2." '
    { buf[NR] = $0 }
    END {
      h = 0; for (i = 1; i <= NR; i++) if (index(buf[i], L) == 1) { h = i; break }
      if (!h) exit 1
      s = h; while (s > 1 && buf[s-1] ~ /^#/) s--
      nx = NR + 1; for (j = h + 1; j <= NR; j++) if (buf[j] ~ /^head2 "/) { nx = j; break }
      e = nx - 1; while (e > h && buf[e] ~ /^#/) e--
      for (k = s; k <= e; k++) print buf[k]
    }'
}
# later PATH: compare the base branch blob of PATH with the pin and with every later 1.5.x state
later() {
  local b c
  b=$(MSYS_NO_PATHCONV=1 git -C "$WT" rev-parse "bannerlord-1.4.5:$1" 2>/dev/null) || { echo "absent in base"; return 0; }
  [ "$(MSYS_NO_PATHCONV=1 git -C "$WT" rev-parse "$PIN:$1" 2>/dev/null)" = "$b" ] && echo "base equals the pin"
  for c in $(git -C "$WT" log --format=%h "$PIN..origin/bannerlord-1.5.x" -- "$1"); do
    [ "$(MSYS_NO_PATHCONV=1 git -C "$WT" rev-parse "$c:$1" 2>/dev/null)" = "$b" ] && echo "BASE EQUALS LATER STATE $c"
  done
  return 0
}
# hooks NAME: run the hook suite on the worktree, log to NOTES/hooks-NAME.log, print FAIL lines and the summary
hooks() { (cd "$WT" && env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh) > "$NOTES/hooks-$1.log" 2>&1; echo "rc=$?" >> "$NOTES/hooks-$1.log"; grep -E 'FAIL|passed, |^rc=' "$NOTES/hooks-$1.log" | tail -n 80; }
# pyt [PATTERN]: run the tool tests (all, or one file pattern)
pyt() { (cd "$WT" && env -u MSYS_NO_PATHCONV python -m unittest discover -s tools/tests -t . -p "${1:-test*.py}") 2>&1 | tail -n 20; }
# probe HOOK PAYLOADFILE: run one worktree hook on a payload file, print its output and rc
probe() { (cd "$WT" && env -u MSYS_NO_PATHCONV CLAUDE_PROJECT_DIR="$WT" bash ".claude/hooks/$1" < "$2"); echo "rc=$?"; }
# The ranges below hold gate-trigger text, so they live here and never on a command line.
# ecc_rows: the 44 dangerous-git rows fdf9f38cb added to section 7e (B1 inserts them)
ecc_rows() { gshow "$PIN" tools/test_hooks.sh | sed -n '/# ECC re-review 2026-09-29: forms that destroy work/,/why switch -f and reflog expire are gated/p'; }
# sec7e_core: section 7e at the pin without those rows (A3 inserts this)
sec7e_core() { sec "$PIN" 7e | sed '/# ECC re-review 2026-09-29: forms that destroy work/,/why switch -f and reflog expire are gated/d'; }
# rows6: the 11 plan 027 lines of section 6 at the pin (8 rows, 3 comment lines)
rows6() { gshow "$PIN" tools/test_hooks.sh | sed -n '/"git in capitals|deny|GIT commit/,/"git in capitals after an assignment|deny|/p'; }
# hwreg_fn: the pinned hw_reg helper from section 7h
hwreg_fn() { gshow "$PIN" tools/test_hooks.sh | sed -n '/^hw_reg() {/,/^}/p'; }
```

These helpers were run read-only against the pinned file at planning (`<helper> | wc -l`): `sec`
4c 134, 4d 37, 4e 126, 7a 149, 7c 286, 7d 96, 7g 60, 7h 115, 7i 97, 7k 66; `sec7e_core` 383;
`ecc_rows` 44; `rows6` 11; `hwreg_fn` prints the 6-line `hw_reg` function. With `MSYS_NO_PATHCONV=1`
a `/c/...` path fails (`git -C /c/Users/...` reported "cannot change"), which is why every path in
this plan is written `C:/...`.

### Settings check `<notes>/check_settings.py` (create with the Write tool in Step A0)

```python
"""WP116 settings check. Usage: python check_settings.py <settings.json> [--github-off]"""
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8"))
hooks = d.get("hooks", {})
rows = [(ev, g.get("matcher", ""), h.get("command", "").rsplit("/", 1)[-1], h.get("timeout"), h.get("command", ""))
        for ev, groups in hooks.items() for g in groups for h in g.get("hooks", [])]
for r in rows:
    print(r[0], repr(r[1]), r[2], r[3])
bad = []
for ev, m, n, t in [("PreToolUse", "Bash|PowerShell", "validate-push.sh", 10),
                    ("PostToolUse", "Bash", "mark-verification-run.sh", 5),
                    ("PostToolUse", "PowerShell", "mark-verification-run.sh", 5),
                    ("PostToolUseFailure", "Bash|PowerShell", "mark-verification-run.sh", 5)]:
    if not any(r[:4] == (ev, m, n, t) for r in rows):
        bad.append(f"missing: {ev} {m} {n} timeout {t}")
gates = {"block-no-verify.sh", "validate-push.sh", "check-claude-files-tracked.sh",
         "check-commit-subject-version.sh", "block-dangerous-git.sh", "block-broad-git-add.sh",
         "check-moduledata-validation.sh", "check-native-dll-crt.sh", "check-doc-config-drift.sh"}
inpair = {r[2] for r in rows if r[0] == "PreToolUse" and r[1] == "Bash|PowerShell"}
bad += [f"git gate outside the Bash|PowerShell group: {g}" for g in sorted(gates - inpair)]
gone = {"suggest-compact.sh", "notify-test-results.sh", "notify-csharp-edit.sh",
        "check-changelog-changed.sh", "check-changelog-updated.sh"}
bad += [f"still registered: {r[0]} {r[2]}" for r in rows if r[2] in gone]
bad += [f"no timeout: {r[0]} {r[2]}" for r in rows if r[3] is None]
bad += [f"not anchored: {r[4]}" for r in rows if "CLAUDE_PROJECT_DIR" not in r[4]]
gh = d.get("enabledPlugins", {}).get("github@claude-plugins-official")
print("github plugin enabled:", gh, "| registrations:", len(rows), "| events:", len(hooks))
if "--github-off" in sys.argv[2:] and gh is not False:
    bad.append("github@claude-plugins-official is not false")
print("FAIL" if bad else "OK")
for b in bad:
    print(" -", b)
sys.exit(1 if bad else 0)
```

## Scope

**In scope** (the only files you may change):

- `.claude/hooks/`: new `_shellwords.py`, `_pushjudge.py`, `_stop_reminder.sh`; changed `_pybin.sh`,
  `validate-push.sh`, `block-no-verify.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`,
  `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`, `check-doc-config-drift.sh`,
  `check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `mark-verification-run.sh`,
  `check-deep-review.sh`, `check-verification-evidence.sh`, `check-version-tagged.sh`,
  `session-start.sh`, `detect-docs-gaps.sh`, `config-protection.sh`,
  `check-polearm-shield-parity.sh`, `mcp-health-mark.sh`; deleted `suggest-compact.sh`,
  `notify-test-results.sh`, `notify-csharp-edit.sh`.
- `.claude/settings.json`: **maintainer only** (M1, M2). You check, stage and commit the maintainer's edit; you
  never write the file (config-protection blocks Edit/Write, and a `git apply`, `sed` or redirect
  onto it would be exploiting that gap).
- `.claude/rules/hook-authoring.md`, `.claude/rules/harness-facts.md`, `.claude/rules/external-skill-ports.md`.
- `.claude/skills/`: `finish-branch/SKILL.md` (one line), `context-budget/SKILL.md` (one sentence),
  `context-budget/scan.sh`, `security-scan/SKILL.md`, `context-save/SKILL.md`, `context-restore/SKILL.md`.
- `.mcp.json`; `CLAUDE.md` (the "Hooks" paragraph, the "Hook Response Contracts" rows, the
  "MCP Servers" section only).
- `docs/reference/hooks-catalog.md`, `docs/reference/mcp-servers.md`, `docs/reference/development-machines.md`,
  `docs/reference/provenance-register.md`, `docs/modding/validation-and-testing.md`,
  `docs/ai-includes/external-repo-adoption.md`, new `docs/reviews/adopt-ecc-2026-09-29.md`,
  `docs/reviews/adopt-mattpocock-improve-architecture-2026-06-22.md` (correction note).
- `tools/test_hooks.sh`, new `tools/tests/test_shellwords.py`, `tools/tests/test_pushjudge.py`,
  `tools/tests/test_audit_user_scope.py`; changed `tools/audit_claude_config.py`,
  `tools/tests/test_audit_skillspector.py`.

**Out of scope** (do not touch):

- `Main/**`, `Dependencies/**`, `TAOM.Tests/**`, `Main/IoC.cs`, `Main/SubModule.cs`,
  `Main/TAOM.csproj`, `Directory.Build.props`, `docs/adrs/**`, ModuleData of any kind.
- `.claude/settings.local.json` (maintainer file; its nine git/filesystem deny entries become inert
  after Part C and are left for the maintainer), `.githooks/**` and `tools/check_commit_msg.py` (WP103).
- `CHANGELOG.md` (D1), `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`,
  `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`
  (WP142 and WP143 sync these). `plans/README.md` belongs to the integrator.
- The upstream-watch tooling, graphify (WP141), `.claude/skills/improve/**` (WP138), deep-review
  lenses and agents (WP136, WP138), every 1.5.x review record except `adopt-ecc-2026-09-29.md`.
- Sections of `tools/test_hooks.sh` that WP102 or WP103 own (3b, 4b, 5b2, 5c, 5c2, 5c3, 5d, 6's
  worktree rows, 7b, 7j), except the edits T1 to T12 list (T4 and T11 replace 4e and 7h, T7 adds one
  row set to 7b).

## Git workflow

- One worktree, created from the main checkout with the Bash tool:
  `git -C C:/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/116 -b port148/116-hook-gate-end-state bannerlord-1.4.5`.
  Start your Claude session in the main checkout (`C:/Users/mikew/source/repos/TAOM`) and work in the
  worktree by absolute path and `git -C`: the live hooks are then the main checkout's (post-WP103),
  so a half-edited worktree gate can never wedge your own session.
- **Git only through the Bash tool** (D19). Never stash. Never touch `stash@{0}` or the uncommitted
  `.serena/project.yml` in the main checkout.
- **Never type a dangerous git string into a command line**, not even inside quotes, a heredoc or a
  `printf`: a force push, `--no-verify`, `reset --hard`, `clean -f`, `stash drop`, `switch -f`. The
  live gates read the whole command text and block or prompt (an `ask` waits for a human even in
  bypass mode). Put such text in files written with the Write tool (probe payloads, commit messages)
  and pass the file path.
- Copy blobs and edit only with the Bash tool (`blob`, `sec`, `git diff ... | git -C "$WT" apply`)
  or the Edit/Write tools; never PowerShell redirection (it changes encodings and line ends).
- Stage explicit paths (`git -C "$WT" add <paths>`, `git -C "$WT" rm <paths>`); never `git add -A`,
  `-u` or `.`. Stage a step's files before running the hook suite: an unstaged new file under
  `.claude/hooks/` is "untracked" to `check-claude-files-tracked.sh` and its test rows.
- Commit procedure (per-port procedure step 4): write the message with the Write tool to
  `<notes>/msg-<step>.txt`; in the NEXT, separate Bash call run
  `git -C C:/Users/mikew/source/repos/taom-port/116 commit -F C:/Users/mikew/source/repos/taom-port/116-notes/msg-<step>.txt`.
  Never in the same command as the `add`, never as a parallel call, never through PowerShell, never
  `-C`, `-c`, `--amend --no-edit` or `--no-verify`.
- Message template (subject at most 72 characters; body wrapped at 72 for 1.4.x readers; under plan
  020 the body becomes the release note; no Co-Authored-By or "Generated with" line, which CLAUDE.md
  forbids and the transplanted subject gate refuses):

```
<type>(<scope>): v2.0.28 - <description>

<What changed on the 1.4.8 line and why. For a partial port, what was
left out and why. No em or en dashes.>

Research: <e.g. 1.5.x blobs at 88e805450; hooks docs "Stop decision control">
Not-tested: <what needs the maintainer's live check>
Ported-from: <40-hex sha>
```

- Before each commit, if `tools/check_commit_msg.py` exists (WP103 ports it with `.githooks/commit-msg`),
  `cd "$WT" && python tools/check_commit_msg.py "$NOTES/msg-<step>.txt"` must pass; if the maintainer
  set `core.hooksPath`, git runs the same check on commit.
- After each commit: `git -C "$WT" log -1 --format='%s%n%(trailers:key=Ported-from,valueonly)'`
  shows the subject and every Ported-from line. Never push, open a PR, tag or merge; the integrator
  merges `port148/116-hook-gate-end-state`.
- If your session's own instructions require `/verify` before each commit, run it; it does not
  replace the hook and tool suites this plan names.

## Maintainer step M1: settings.json for Part A (before commit A1)

The maintainer edits `C:\Users\mikew\source\repos\taom-port\116\.claude\settings.json` in a text
editor (not through an agent: today's `config-protection.sh` override file is shared by every
session, the hole B3 closes). Leave every other key unchanged (env, statusLine, permissions with
WP103's Serena denies per D3, all other plugins). The fragments assume WP103's anchored form
`"\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/<name>.sh"`; if WP103 spelled the anchor differently, use
its spelling.

```diff
 PreToolUse, group "Bash|PowerShell":
           {
             "type": "command",
             "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/validate-push.sh",
-            "timeout": 5
+            "timeout": 10
           },

 PreToolUse: delete the whole group whose matcher is "":
-      {
-        "matcher": "",
-        "hooks": [
-          {
-            "type": "command",
-            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/suggest-compact.sh",
-            "timeout": 5
-          }
-        ]
-      },

 PostToolUse, group "Edit|Write": delete the notify-csharp-edit.sh entry, keep the polearm entry:
-          {
-            "type": "command",
-            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/notify-csharp-edit.sh",
-            "timeout": 5
-          },

 PostToolUse, group "Bash": delete notify-test-results.sh, keep mark-verification-run.sh, then add
 a PowerShell group right after it:
-          {
-            "type": "command",
-            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/notify-test-results.sh",
-            "timeout": 5
-          },
           ...mark-verification-run.sh entry unchanged...
         ]
-      }
+      },
+      {
+        "matcher": "PowerShell",
+        "hooks": [
+          {
+            "type": "command",
+            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/mark-verification-run.sh",
+            "timeout": 5
+          }
+        ]
+      }
     ],

 PostToolUseFailure: add a group after the "mcp__.*" group:
-      }
+      },
+      {
+        "matcher": "Bash|PowerShell",
+        "hooks": [
+          {
+            "type": "command",
+            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/mark-verification-run.sh",
+            "timeout": 5
+          }
+        ]
+      }
     ],
```

Why each change: plan 011 deleted the two stderr-only hooks and registered the verification marker
for PowerShell (`43e6780e4`); a failing command raises PostToolUseFailure, so the marker is
registered there too (`18961c1ec`); `notify-csharp-edit.sh` only printed to stderr on exit 0
(`fdf9f38cb`). **Validate-push at 10 s is decided in this plan** under the 2026-11-04 delegation: the
transplanted script gives its judge at most 3.0 s from its first command and asks after about 3.3 s,
its own comment calls the 10 s registration a backstop, and 1.5.x has run that pairing since
`fff6d66e9` (2026-09-27); a 5 s kill would fail open. Expected registration total afterwards: 25
across 9 events, if WP102 and WP103 changed no other registration (the check script prints the real
count).

## Maintainer step M2: settings.json for Part C (before commit C2)

```diff
-    "github@claude-plugins-official": true,
+    "github@claude-plugins-official": false,
```

The plugin declares the same `github` MCP server `.mcp.json` declares; deleting the `.mcp.json`
entry alone left the server loaded on 1.5.x (`fdf9f38cb`, recorded in 1.5.x mcp-servers.md).
Optional and not needed by any test: remove the nine `mcp__git__*` and `mcp__filesystem__*` deny
entries and the `github`, `filesystem` names in `enabledMcpjsonServers` from the tracked
`.claude/settings.local.json`; after C2 they name servers that no longer exist.

## Part A: the git-gate core

### Step A0: Worktree, prerequisites, baselines (no commit)

1. Create the worktree (Git workflow above) and `mkdir -p C:/Users/mikew/source/repos/taom-port/116-notes`.
2. Write `<notes>/wp116.sh` and `<notes>/check_settings.py` with the Write tool (contents above).
3. Run the drift check from the header with `git -C "$WT"`; read it.
4. Prerequisite checks (each in a Bash call that starts with `source <notes>/wp116.sh`):

| Check | Command | Expected |
|---|---|---|
| P1 | `ls "$WT"/.claude/hooks/check-changelog-*.sh 2>&1` | `No such file or directory` |
| P2, P3 | `python "$NOTES/check_settings.py" "$WT/.claude/settings.json"` | `FAIL` listing only M1 items (missing validate-push timeout 10, the two mark-verification-run groups, `still registered` for the three hooks M1 drops). No `not anchored`, no `git gate outside`, no `check-changelog` line |
| P4 | `grep -rn '{"permissionDecision"' "$WT/.claude/hooks" "$WT/.claude/skills/freeze/check-freeze.sh"` | no output |
| P5 | `grep -c 'PYTHONIOENCODING=utf-8' "$WT/.claude/hooks/_pybin.sh"` | `1` |
| P6 | `grep -ln 'taom_git_repo_dir' "$WT"/.claude/hooks/*` | lists the file defining it plus `check-commit-subject-version.sh`, `check-claude-files-tracked.sh`, `block-broad-git-add.sh`, `check-doc-config-drift.sh`, `check-moduledata-validation.sh` |
| P7 | `grep -c 'bannerlord-1.5.x' "$WT/.claude/hooks/validate-push.sh"` | 1 or more |
| P8 | `grep -n -E '^[^#]*<<<' "$WT"/.claude/hooks/*.sh` and `grep -c '^decision_of()' "$WT/tools/test_hooks.sh"` | no output; `1` |
| P9 | `cd "$WT" && python tools/lint_docs.py --fail-on-drift >/dev/null; echo rc=$?` | `rc=0` |
| P10 | `for f in .claude/hooks/_shellwords.py .claude/hooks/_pybin.sh .claude/hooks/validate-push.sh .claude/hooks/block-no-verify.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/mark-verification-run.sh tools/tests/test_shellwords.py; do echo "$f: $(later $f)"; done` | no line containing `BASE EQUALS LATER STATE` |

5. Record WP103's helper names: `grep -h -o '^taom_[a-z_]*()' "$WT"/.claude/hooks/*.sh | sort -u > "$NOTES/fns-base.txt"`
   and `grep -h -o 'taom_[a-z_]*' "$WT"/.claude/hooks/*.sh | sort -u > "$NOTES/calls-base.txt"`.
   Record which test_hooks.sh labels exist: `grep '^head2 ' "$WT/tools/test_hooks.sh" > "$NOTES/labels-base.txt"`.
6. Baselines: `hooks base` (background), `pyt > "$NOTES/pyt-base.txt" 2>&1`,
   `cd "$WT" && python tools/lint_docs.py --report "$NOTES/lint-base.md"`,
   `cd "$WT" && python tools/audit_claude_config.py --min HIGH > "$NOTES/audit-base.txt" 2>&1`.
   Note the hook suite's `N passed, M failed` and every FAIL line, and the tool tests' `Ran N tests`.
   Also run `ls /tmp/claude-config-override-* 2>/dev/null` and mention any file it lists to the
   maintainer (today such a file unlocks protected edits for every session).
7. If `git -C "$WT" diff --stat -- .claude/settings.json` shows no change yet, STOP here and ask the
   maintainer to apply M1 (point the maintainer at that section). Resume at A1 when it shows a change.

**Verify**: every row of the table matches; the four baseline files exist in `<notes>`.

### Step A1: Register the marker for failures, drop three silent hooks

Ported-from: `43e6780e4`, `18961c1ec`, `fff6d66e9` (the 10 s registration), `fdf9f38cb` (notify-csharp-edit).

1. `python "$NOTES/check_settings.py" "$WT/.claude/settings.json"` must now print `OK`. If not, STOP
   and show the maintainer the FAIL lines.
2. `git -C "$WT" rm .claude/hooks/suggest-compact.sh .claude/hooks/notify-test-results.sh .claude/hooks/notify-csharp-edit.sh`
3. Docs that name the deleted hooks (Edit tool):
   - `docs/reference/hooks-catalog.md`: delete the three table rows for those hooks.
   - `.claude/rules/hook-authoring.md`: in the "Reference pattern (used by ...)" line, drop
     `suggest-compact.sh` (and `check-changelog-changed.sh` if WP102 left it), leaving
     `check-claude-files-tracked.sh`. Keep the historical "Codex review #29 caught suggest-compact.sh"
     sentence.
   - `.claude/skills/context-budget/SKILL.md`: delete the sentence "The `suggest-compact` hook already
     nudges this."
   - `tools/test_hooks.sh` section 5 comment: `Advisory hooks (notify-*, suggest-compact, mark-verification-run)`
     becomes `Advisory hooks (mark-verification-run)`.
4. Stage: `git -C "$WT" add .claude/settings.json docs/reference/hooks-catalog.md .claude/rules/hook-authoring.md .claude/skills/context-budget/SKILL.md tools/test_hooks.sh`
5. `hooks a1`.
6. Commit with subject `chore(hooks): v2.0.28 - drop three silent hooks, mark failed builds`. Body:
   the three hooks printed only to stderr on exit 0, which Claude never sees, and suggest-compact ran
   on every tool call; mark-verification-run is now registered for the PowerShell tool and on
   PostToolUseFailure (a red build raises that event, not PostToolUse); validate-push's registration
   is 10 s because the judge it gets in A3 keeps its own 3.0 s deadline. Not-tested: that Claude Code
   fires the new registrations (live check L1).

**Verify**: `ls "$WT"/.claude/hooks/{suggest-compact,notify-test-results,notify-csharp-edit}.sh` fails
for all three; the hook suite shows no failure the baseline lacked; check_settings prints `OK`.

### Step A2: Read PowerShell commands as Bash text (expected to be a no-op)

Ported-from: `b23dc8999`, `64b9f3a6c`, `747b6dae8`, `60d85ed7d`, `fff6d66e9`, `d01bddf10`.

**First decide whether this step runs.** If P10 printed `base equals the pin` for both
`.claude/hooks/_shellwords.py` and `tools/tests/test_shellwords.py` (WP103 Step B4 wrote both
verbatim from `407570ece`, and neither file changed between `407570ece` and the pin), there is
nothing to copy: skip to A3, make no A2 commit, and carry the six Ported-from lines above into the
A3 commit, whose body then says the PowerShell reader itself arrived with WP103 and this branch wires
it into the gates. Otherwise run the steps below.

1. RED: `blob $PIN tools/tests/test_shellwords.py`, then `pyt test_shellwords.py`. Expected: an
   import error for `_shellwords`, or failures in the PowerShell classes if WP103 left a partial
   reader (record which).
2. Lost-function check, only if `.claude/hooks/_shellwords.py` exists in the base:
   `comm -23 <(gshow bannerlord-1.4.5 .claude/hooks/_shellwords.py | grep -o '^def [A-Za-z_]*' | sort) <(gshow $PIN .claude/hooks/_shellwords.py | grep -o '^def [A-Za-z_]*' | sort)`.
   For every name printed, `grep -rn '<name>' "$WT/.claude/hooks" "$WT/tools/test_hooks.sh" "$WT/tools/tests"`
   (ignore `_shellwords.py` itself). Any caller is a STOP.
3. GREEN: `blob $PIN .claude/hooks/_shellwords.py`; `pyt test_shellwords.py` shows `OK` and
   `Ran 83 tests` (83 `def test_` methods read at planning).
4. Stage both files; `hooks a2` (WP103's reader rows in sections 6 and 7b must still pass); `pyt`.
5. Commit with subject `feat(hooks): v2.0.28 - read PowerShell commands as Bash text`. Body: a reader
   that hands a PowerShell command back as the Bash text of the same command (here-strings, `''` and
   backtick escapes, continuations, comments, script blocks, the call operator, git by path or in
   capitals) plus the quote-aware split and the push candidates the gates share; nothing calls the new
   modes yet; the git-repo reader WP103 ported is kept unchanged inside the 88e805450 copy.

**Verify**: `pyt test_shellwords.py` prints `OK`; no new hook-suite failure.

### Step A3: The gate core (prefilters, both shells, Python push judge)

Ported-from: `90141357d`, `787fd3663`, `7aa658e3e`, `6514fdfe7`, `ee1929fa4`, `43e6780e4`,
`c80c4108c`, `65b405e42`, `18961c1ec`, `764ab0984`, `e046bdf09`, `3a0614061`, `cfd3b48f6`,
`64b9f3a6c`, `747b6dae8`, `a4ba6b0b3`, `fd1d5c17d`, `343c6f488`, `fff6d66e9`, `d01bddf10`
(40-hex shas in the disposition table).

**RED first.**

1. `blob $PIN tools/tests/test_pushjudge.py`; `pyt test_pushjudge.py` gives an import error for
   `_pushjudge`.
2. With the Write tool create two payload files in `<notes>`:
   - `probe-vp-line2.json`: `{"tool_name":"Bash","tool_input":{"command":"cd x\ngit push --force origin bannerlord-1.4.5"},"hook_event_name":"PreToolUse"}`
   - `probe-csv-ps.json`: `{"tool_name":"PowerShell","tool_input":{"command":"git commit -m @'\ndocs: v2.0.28 - here-string probe\n'@"},"hook_event_name":"PreToolUse"}`

   Then `probe validate-push.sh "$NOTES/probe-vp-line2.json"` (expected today: `rc=0`, no BLOCKED
   line, the line-2 bypass) and `probe check-commit-subject-version.sh "$NOTES/probe-csv-ps.json"`
   (expected today: a `deny`, the here-string read as Bash text). If the first already blocks, WP103
   chose the multi-line split for `143f0fa85`; if the second already allows, WP103 brought a
   PowerShell reader. Record either and continue: the GREEN result is what counts.
3. Make the test edits T1 to T12 below in `tools/test_hooks.sh`, then `hooks a3-red` (background).
   Expected: FAIL rows in at least 4c, 4d, 6, 7c, 7d, 7e and 7i. Record the count.

**Test edits** (write each block to a file in `<notes>` with the helper named, for example
`sec $PIN 4c > "$NOTES/t2.txt"`, edit it there, then insert it with the Edit tool; keep the head2
labels in sorted order: 4, 4b, 4c, 4d, 4e, 5, 5b, 5b2, 5c, 5c2, 5c3, 5d, 6, 7, 7a, 7b, 7c, 7d, 7e, 7g,
7h, 7i, 7j, 7k, 7l, Summary; a section the base lacks is simply skipped in that order. The pinned 7k
arrives in B2 relabelled 7l, because WP103's 7k is a different check):

- **T1, section 4.** (a) Replace the comment above `GATE_HOOKS=` with the pinned five lines starting
  `# Only PreToolUse, PostToolUse and PostToolUseFailure hooks must print JSON`. (b) Insert the five
  pinned comment lines starting `# bash-trigger holds every Bash hook's prefilter text` directly above
  `PAYLOADS=(`, and the two pinned rows `'bash-trigger|...'` and `'powershell-trigger|...'` directly
  after the `'bash|...'` row. (c) `[[ "$name" == "_pybin.sh" ]] && continue` becomes
  `[[ "$name" == _*.sh ]] && continue`.
- **T2, 4c.** Insert `sec $PIN 4c` after 4b. Adapt: in `PF_NARROWED_LIST` add
  `check-native-dll-crt.sh` after `check-moduledata-validation.sh` and remove `check-graphify-usage.sh`;
  delete the two `check-graphify-usage.sh)` case lines (one in the triggers `case`, one in the `esc`
  `case`).
- **T3, 4d.** Insert `sec $PIN 4d` after 4c. Adapt the comment: "six of the ten blocking gates" with
  its list becomes "five of the nine blocking gates (check-commit-subject-version.sh, validate-push.sh,
  block-no-verify.sh, block-dangerous-git.sh, block-broad-git-add.sh)"; "The other three
  (check-claude-files-tracked.sh, check-moduledata-validation.sh, check-doc-config-drift.sh)" becomes
  "The other four (check-claude-files-tracked.sh, check-moduledata-validation.sh,
  check-native-dll-crt.sh, check-doc-config-drift.sh)". Delete the `check-graphify-usage.sh|$SANDBOX|...`
  row; the `block-broad-git-add.sh` row then ends `"; do` instead of `" \`.
- **T4, 4e.** Save `sec bannerlord-1.4.5 4e > "$NOTES/base-4e.txt"`, then replace the base 4e with
  `sec $PIN 4e` (`fff6d66e9` rewrote it after WP103's `143f0fa85` copy). If the base section has a
  check the pinned one lacks, append that check to the new section and name it in the commit body.
- **T5, section 5.** `_pybin.sh` skip becomes `_*.sh` as in T1(c). Replace the starved-environment
  payload line with the pinned one minus its last clause: the command is
  `git push --force origin master && git commit --no-verify -m x` (drop ` && graphify update x`), with
  the two pinned comment lines above it, written as "(commit, push, no-verify, git)".
- **T6, section 6.** Insert the output of `rows6` (11 pinned lines, from the row labelled
  `git in capitals` through the row labelled `git in capitals after an assignment`) directly after
  the row labelled `git -C form unlabelled`.
- **T7, 7b.** `diff <(sec bannerlord-1.4.5 7b) <(sec $PIN 7b)`: if the base lacks the three hunks that
  mention `_helper.py` (from `e046bdf09`: the `printf 'x = 1\n' > ...` line, the widened `if`/`ok`
  pair, and `_helper.py` in the `git add` line), apply exactly those three. Nothing else in 7b.
- **T8, 7c.** WP103 added an interim section with the same label (`head2 "7c. validate-push refuses a
  force push to either trunk, master or main"`, six rows). Save it (`sec bannerlord-1.4.5 7c >
  "$NOTES/base-7c.txt"`), replace it with `sec $PIN 7c` (unchanged), then compare: any push command of
  the saved rows that appears in none of the new section's case arrays is appended to `VP_CASES` in
  the same `"<rc>|<command>"` form. If the base has no 7c, insert the pinned one after 7b.
- **T9, 7d.** Insert `sec $PIN 7d` after 7c, unchanged.
- **T10, 7e.** Insert the output of `sec7e_core` (section 7e without the 44 ECC rows, which B1 adds)
  after 7d. Adapt: in `names = [...]` add `"check-native-dll-crt.sh"` after
  `"check-moduledata-validation.sh"`; `own = ["check-graphify-usage.sh", "validate-push.sh"]` becomes
  `own = ["validate-push.sh"]`; in the comment above, the clause "`own` lists shell hooks that read
  their command themselves: check-graphify-usage.sh is not a git gate, splits both shells in
  tools/graphify_taom.py, and 7f checks it." becomes "`own` lists shell hooks that read their command
  themselves."; the loop `for hook in check-claude-files-tracked.sh check-moduledata-validation.sh check-doc-config-drift.sh; do`
  gains `check-native-dll-crt.sh` before `check-doc-config-drift.sh` (the reverse of `ad1992b5b`'s
  hunks in `git -C "$WT" show ad1992b5b -- tools/test_hooks.sh`).
- **T11, 7h.** Save `sec bannerlord-1.4.5 7h > "$NOTES/base-7h.txt"`, then replace the base 7h with
  `sec $PIN 7h`; in its loop `for hook in check-doc-config-drift.sh check-moduledata-validation.sh; do`
  add `check-native-dll-crt.sh` (again the reverse of `ad1992b5b`). Keep any base-only check as in T4.
- **T12, 7i.** Insert `sec $PIN 7i` after 7h. If `grep -c '^hw_reg()' "$WT/tools/test_hooks.sh"` is
  0, or its body differs from `hwreg_fn` (event, hook; prints the registered timeout), add the
  `hwreg_fn` output to the top of 7i renamed `vpi_reg` and change
  `VPI_REG=$(hw_reg PreToolUse validate-push.sh)` to `VPI_REG=$(vpi_reg PreToolUse validate-push.sh)`.

If a label this plan inserts (4c, 4d, 7a, 7d, 7e, 7g, 7i, 7l) already exists in the base, STOP. (7c,
4e and 7h are replaced on purpose by T8, T4 and T11.)

**GREEN.**

4. Copy: `blob $PIN .claude/hooks/_pybin.sh`, then the same for `_pushjudge.py`, `validate-push.sh`,
   `block-no-verify.sh`, `block-broad-git-add.sh`, `check-claude-files-tracked.sh`,
   `check-commit-subject-version.sh`, `mark-verification-run.sh`; and
   `blob fff6d66e9 .claude/hooks/block-dangerous-git.sh`, unless P10 said `base equals the pin` for
   that file (WP103 already brought the armed copy): then leave it, and B1 only adds its test rows,
   the record and the catalog row.
5. Lost-helper check: `comm -23 "$NOTES/fns-base.txt" <(grep -h -o '^taom_[a-z_]*()' "$WT"/.claude/hooks/*.sh | sort -u)`.
   For each name printed, `grep -rn '<name>' "$WT/.claude/hooks" "$WT/tools/test_hooks.sh"`; a caller
   outside the removed definition is a STOP.
6. Adaptations: in `validate-push.sh` line 3, `(AGENTS.md "Git and commits")` becomes
   `(CLAUDE.md "Commits")`. In `check-commit-subject-version.sh`, change the one
   `AGENTS.md "Git and commits"` and the two `AGENTS.md 'Git and commits'` to `CLAUDE.md "Commits"` and
   `CLAUDE.md 'Commits'`; then `grep -c 'AGENTS.md' .claude/hooks/check-commit-subject-version.sh` is 0.
7. Deltas G1 to G5 on `check-doc-config-drift.sh`, `check-moduledata-validation.sh`,
   `check-native-dll-crt.sh` (Edit tool; `<gate>` is the file name without `.sh`; the code blocks go
   in at column 1, without this list's indentation):
   - **G1**: the header's `PreToolUse(Bash) hook` becomes `PreToolUse (Bash and PowerShell) hook`.
   - **G2**: `INPUT=$(cat)` must be the first statement after `set -uo pipefail`; if it sits below the
     `source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"` line, move it up. Directly after it insert
     (one blank line before and after):
     ```
     # Prefilter: every decision below needs the word `commit` in the command (`git commit` or
     # `git -C <dir> commit`), and Claude Code never escapes an ASCII letter, so a raw payload
     # without the text `commit` cannot concern this gate. Exiting here skips the _pybin.sh probe
     # and the parse (two Python starts) on every Bash call without the word, `git status` and
     # `git log` included. Match the raw text, never a token regex: a newline before a command
     # arrives as \n. tools/test_hooks.sh 4c checks both directions.
     # Never skip on an escape: JSON writes a letter either literally or as a \u escape,
     # so a payload holding any \u takes the full parse, and the raw test is safe
     # whatever writes the payload.
     [[ "$INPUT" == *commit* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }
     ```
   - **G3**: replace the block that sets `COMMAND` from the payload (a `COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '` one-liner and the comment above it) with:
     ```
     # The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
     # command back as the Bash text of the same command and names git `git` wherever it is the
     # command (`GIT`, `git.exe`, a path), so the two-stage matcher below reads both shells.
     COMMAND=$(taom_hook_command posix <gate>)
     ```
   - **G4**: every `65,537 to 65,664 bytes` becomes `65,536 to 65,663 bytes of text` (also in
     `detect-docs-gaps.sh`, if WP103's comment is there).
   - **G5**: `grep -o 'taom_[a-z_]*' <gate file> | sort -u` may name only `taom_pybin_degraded`,
     `taom_hook_command`, `taom_git_repo_dir`, `taom_deny_multi_repo`, `taom_is_project_repo`. If it
     names anything else, replace the gate's repository block with the pinned form
     (`COMMIT_REPO=$(taom_git_repo_dir commit) || { taom_deny_multi_repo <gate>; exit 0; }`,
     `cd "$COMMIT_REPO" 2>/dev/null || { echo '{}'; exit 0; }`,
     `taom_is_project_repo "$COMMIT_REPO" || { echo '{}'; exit 0; }`); for `check-native-dll-crt.sh`
     only if WP103 gave it a repository block at all.
   - Check `check-native-dll-crt.sh` with `git -C "$WT" diff fff6d66e9 -- .claude/hooks/check-native-dll-crt.sh`:
     only comment wording (WP103 re-expressed the here-string fix in its own words) and lines WP103
     added that 1.5.x never had may differ; any other code difference is a STOP.
8. **S1, `session-start.sh`**: in the `if [[ -z "${PYBIN:-}" ]]; then` banner, right after the echo
   line(s) that list the python-only gates, add
   `    echo "    validate-push cannot judge a push: it asks on any force marker and allows the rest."`,
   and make the echo inside `if ! command -v jq` read
   `echo "    jq is absent too, so every other JSON-parsing gate is open."` (`a4ba6b0b3`). validate-push no
   longer has a jq path, so WP102's section 5b2 needs its name in that banner.
9. `.claude/skills/finish-branch/SKILL.md` step 6: replace the "Confirm with the user before pushing
   `bannerlord-1.4.5`" bullet with the `c80c4108c` text
   (`git -C "$WT" show c80c4108c -- .claude/skills/finish-branch/SKILL.md`), which names both trunks.
10. Static checks:
    - `grep -L 'taom_hook_command posix' "$WT"/.claude/hooks/{block-no-verify,block-dangerous-git,block-broad-git-add,check-claude-files-tracked,check-commit-subject-version,check-doc-config-drift,check-moduledata-validation,check-native-dll-crt}.sh` prints nothing.
    - `grep -c '_shellwords.py" verdict' "$WT/.claude/hooks/validate-push.sh"` is 1;
      `grep -c 'PROTECTED=(bannerlord-1.5.x bannerlord-1.4.5 main master)' "$WT/.claude/hooks/validate-push.sh"` is 1.
    - `grep -rn '65,537 to 65,664' "$WT/.claude/hooks"` and `grep -rn '{"permissionDecision"' "$WT/.claude/hooks"` print nothing.
11. Probes: `probe validate-push.sh "$NOTES/probe-vp-line2.json"` now prints a `BLOCKED: force push to
    'bannerlord-1.4.5'` line and `rc=2`; `probe check-commit-subject-version.sh "$NOTES/probe-csv-ps.json"`
    prints `{}` and `rc=0`.
12. Stage every A3 path; `pyt test_pushjudge.py` shows `OK` (`Ran 25 tests`); `pyt` shows no new
    failure; `hooks a3` shows `0 failed` or baseline-only failures; `cd "$WT" && python tools/audit_claude_config.py --min HIGH`
    shows nothing new.
13. Commit with subject `feat(hooks): v2.0.28 - gates read both shells, judge pushes in Python`. Body
    (1.4.x terms): each git gate reads its command through `_shellwords.py`, so a PowerShell command is
    judged as the Bash command it equals; each Bash hook tests the raw payload for its own word before
    starting Python; validate-push judges every line, every refspec, abbreviated force options,
    `--mirror` and forced matching pushes in one Python run under a 3.0 s deadline, and asks when it
    cannot judge; the verification marker ignores quoted mentions and interrupted runs; three gates got
    small deltas because their 1.5.x copies need validator codes and a lint flag this line lacks.
    Not-tested: the PowerShell tool's live payloads (live checks L2 to L6).

**Verify**: steps 10 to 12 all as stated.

### Step A4: Stop reminders reach Claude

Ported-from: `43e6780e4`, `c80c4108c`, `65b405e42`.

1. RED: `grep -n '>&2' "$WT"/.claude/hooks/check-{deep-review,verification-evidence,version-tagged}.sh | grep -v ':[[:space:]]*#'`
   prints one stderr line per hook. Insert `sec $PIN 7a` after section 6 (after section 7 if it
   exists), unchanged.
2. GREEN: `blob $PIN` for `.claude/hooks/_stop_reminder.sh`, `check-deep-review.sh`,
   `check-verification-evidence.sh`, `check-version-tagged.sh`. In `_stop_reminder.sh` line 2,
   "the four Stop reminder hooks" becomes "the three Stop reminder hooks".
3. The grep in step 1 now prints nothing. Stage; `hooks a4` shows `0 failed` or baseline-only.
4. Commit with subject `fix(hooks): v2.0.28 - Stop reminders reach Claude through a block`. Body: the
   verification, version-tag and deep-review reminders printed to stderr on exit 0, which goes to the
   debug log only; they now print one `{"decision":"block","reason":...}` per streak through
   `_stop_reminder.sh`, stay silent when `stop_hook_active` is true, and clear their streak markers
   when the condition clears. `check-deep-review.sh` is taken whole at 88e805450, which also carries
   `fa8c06f82`'s reviewer-type pattern and wider file list: the pattern matches the deep-reviewer agent
   WP138 brings, and until then the reminder behaves as today (the old pattern matched no logged agent
   type). Not-tested: that Claude receives the block reason (live check L7).

**Verify**: 7a passes in `hooks a4`.

### Step A5: Catalogue the gates and the Stop channel

Ported-from: `2d7770b64`, `6fe4c8fe4`, `b34d615a0`, `787fd3663`, `43e6780e4`, `c80c4108c`,
`64b9f3a6c`, `a4ba6b0b3`, `fff6d66e9`, `fdf9f38cb`.

1. `docs/reference/hooks-catalog.md`:
   - Replace the Event and Purpose cells of these rows with the pinned rows
     (`gshow $PIN docs/reference/hooks-catalog.md | grep '^| `<name>`'`): `block-no-verify.sh`,
     `validate-push.sh`, `block-broad-git-add.sh`, `check-commit-subject-version.sh`,
     `check-claude-files-tracked.sh`, `mark-verification-run.sh`, `check-verification-evidence.sh`,
     `check-version-tagged.sh`, `check-deep-review.sh`.
   - Leave the other gate rows (`block-dangerous-git.sh`, which B1 updates, `check-doc-config-drift.sh`,
     `check-moduledata-validation.sh`, `check-native-dll-crt.sh`) as WP103 left them; their Event cell
     must read `PreToolUse (Bash, PowerShell)` (WP103 Step C1 changes it; do it here if it did not).
   - Copy the pinned header blockquote paragraphs on the prefilter and on "Both shell tools" (pinned
     lines 20 to 74) into the header, adapted: drop every mention of `check-graphify-usage.sh` and
     graphify; "all eleven prefiltered shell hooks (the ten PreToolUse gates and
     `mark-verification-run.sh`)" becomes "all ten prefiltered shell hooks (the nine PreToolUse gates
     and `mark-verification-run.sh`)"; "twelve rows" becomes "eleven rows"; "six of the ten blocking
     gates" becomes "five of the nine blocking gates" with the graphify name removed from its list.
     Confirm "nine" with `python "$NOTES/check_settings.py" "$WT/.claude/settings.json" | grep -c "^PreToolUse 'Bash|PowerShell'"`.
   - Merge, do not repeat: WP103 already added a header sentence on `_shellwords.py` and one on the
     anchored registrations; keep each fact once.
   - Rewrite the header's recount sentence from measured numbers:
     `git -C "$WT" ls-files .claude/hooks | wc -l` (files), `git -C "$WT" ls-files '.claude/hooks/*.sh' | wc -l`
     (shell scripts), the check script's registration and event counts, plus the 5 frontmatter
     registrations; name the four helpers without a registration (`_pybin.sh`, `_stop_reminder.sh`,
     `_shellwords.py`, `_pushjudge.py`).
   - Add one blockquote line: "Decision ids in hook and test comments (D29, D30, D38 to D42, decision 61)
     are 1.5.x plan-record ids, carried with the files transplanted by WP116; see `plans/README.md` on
     `origin/bannerlord-1.5.x`."
2. `.claude/rules/hook-authoring.md`: add the pinned "**Output channel**" row to the sibling table
   without its PostToolUse clause (B2 adds it); "When writing a PreToolUse(Bash) hook" becomes "When
   writing a PreToolUse hook"; insert the pinned "**Both shell tools (plan 027).**" paragraph after the
   "MANDATORY for any new hook that detects git commits" paragraph; replace the "**Inside a hook:**"
   paragraph with the pinned one; add the pinned "**Count the bound from the script's first command**"
   row to the timeout table, and make its "Handle rc 124" row match the pinned row.
3. `.claude/rules/harness-facts.md`: copy these pinned rows verbatim into the "Hook lifecycle" table,
   replacing a row with the same opening words if WP103 added one: "**Visibility.**" (then "The four
   Stop reminders" becomes "The three Stop reminders" and its `tools/test_hooks.sh` 7k becomes 7l),
   "**An `ask` prompts even in bypass mode.**",
   "**A deny stops only its own call.**", "The PowerShell tool is its own tool", "PostToolUseFailure
   input:". In the "`if:` migration is DEFERRED" paragraph, "TAOM's 8 PreToolUse Bash gates still use
   bare `matcher: "Bash"`" becomes "TAOM's nine PreToolUse git gates use the `Bash|PowerShell` matcher"
   unless WP103 already reworded it. Set "Last verified" to the execution date.
4. `docs/reference/mcp-servers.md`: "every safety hook in this repo is registered against
   `matcher: "Bash"`" becomes "every git safety hook in this repo is registered against
   `matcher: "Bash|PowerShell"`"; in the git row, "because the safety hooks match Bash only" becomes
   "because the safety hooks match only the Bash and PowerShell tools".
5. `CLAUDE.md`:
   - "Hooks" paragraph: rewrite the counts from the same measurements, and add one sentence: the git
     gates run for the Bash and the PowerShell tool alike (one `Bash|PowerShell` group, each reading
     its command through `_shellwords.py`), and `python -m unittest discover -s tools/tests -t . -p "test_shellwords.py"`
     plus `test_pushjudge.py` gate the reader and the judge.
   - "Hook Response Contracts": the `check-verification-evidence.sh` row names
     `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` or
     `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` instead of `./build.ps1 -RunTests`,
     and says the reminder arrives once per streak as a Stop block reason; the `check-version-tagged.sh`
     row says to ask the user before tagging and pushing (tags and pushes are the maintainer's).
   - MCP Usage Guide: "which matches `Bash` only" becomes "which match only the Bash and PowerShell
     tools".
   - `python tools/lint_docs.py --fail-on-drift` must exit 0; trim your own additions if it does not.
6. Stage; `hooks a5` (3b parses skill and agent frontmatter); `python tools/lint_docs.py --dash-base bannerlord-1.4.5 --report "$NOTES/lint-a5.md"`
   names no new finding in a touched file.
7. Commit with subject `docs(hooks): v2.0.28 - catalogue the PowerShell gates and Stop channel`.

**Part A done criteria**

- [ ] `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD` lists the Part A subjects (four when A2
      was skipped as expected, five otherwise), each with `v2.0.28 - ` and at most 72 characters.
- [ ] `python "$NOTES/check_settings.py" "$WT/.claude/settings.json"` prints `OK`.
- [ ] The static checks of A3 step 10 hold as stated there, and the A4 step 3 grep prints nothing.
- [ ] `hooks partA` ends `N passed, 0 failed` (or only A0-baseline failures, each failing for the same
      reason); `pyt` has no failure the baseline lacked and `Ran` rose by 25 (`test_pushjudge.py`),
      plus 83 only if A2 ran (WP103 already brings the 83 `test_shellwords.py` methods).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0; the audit shows nothing new.
- [ ] Dotnet build 0 errors; dotnet test 0 failed with the previous merged WP's total.
- [ ] `git -C "$WT" status --porcelain` prints nothing.

## Part B: config protection, dangerous-git arms, PostToolUse visibility

Each step: RED probe, change, `hooks <step>`, commit. Ported-from: `fdf9f38cb` for B1 and B2;
`30abf550b` and `88e805450` for B3.

### Step B1: Confirm five more forms that destroy git work

1. RED: Write `<notes>/probe-bdg-switch.json` with
   `{"tool_name":"Bash","tool_input":{"command":"git switch --discard-changes main"},"hook_event_name":"PreToolUse"}`;
   `probe block-dangerous-git.sh "$NOTES/probe-bdg-switch.json"` prints `{}`.
2. Insert the output of `ecc_rows` (the 44 pinned ECC rows; write it to `<notes>/ecc-rows.txt`
   first) into 7e's `G7E_ROWS` array, directly before its closing `)`, which is the line after the
   last PowerShell `block-dangerous-git.sh` row (the one whose command assigns to `$a[0]`).
3. `blob $PIN .claude/hooks/block-dangerous-git.sh`.
4. `blob fdf9f38cb docs/reviews/adopt-ecc-2026-09-29.md`, then (Edit tool) add under its title the
   line "> Ported to `bannerlord-1.4.5` by WP116 (2026-11). Steps 1, 2, 4, 5, 7, 8 and 10 landed there;
   Step 3 arrives with the `/improve` rebuild (WP138); Steps 6 and 11 stay on `bannerlord-1.5.x`.
   Paths and `tools/test_hooks.sh` labels are the 1.5.x ones of 2026-09-29 (its 7k is 7l here).", and
   turn the link `[rca-ecc-adoption-2026-09-29.md](rca-ecc-adoption-2026-09-29.md)`
   into plain text "`rca-ecc-adoption-2026-09-29.md` (on `origin/bannerlord-1.5.x`)".
5. hooks-catalog: replace the `block-dangerous-git.sh` Purpose cell with the pinned one.
6. Probe again: output holds `"permissionDecision":"ask"`. Stage; `hooks b1` green.
7. Commit with subject `fix(hooks): v2.0.28 - confirm five more forms that destroy git work`. Body:
   the confirm gate now asks on `switch --discard-changes`/`-f`, `worktree remove --force`,
   `reflog expire`/`delete`, `update-ref -d`/`--stdin`/an all-zero id, branch delete plus force in any
   spelling, and git's unambiguous long-option prefixes; `restore -S` alone stays allowed; the ECC
   adoption record comes along because the hook cites it.

### Step B2: Advisory hooks report where Claude reads

1. RED: `grep -n '>&2' "$WT/.claude/hooks/check-polearm-shield-parity.sh" | grep -v ':[[:space:]]*#'`
   prints the unguarded stderr report line.
2. `blob $PIN .claude/hooks/check-polearm-shield-parity.sh`; `blob $PIN .claude/hooks/mcp-health-mark.sh`.
3. Insert `sec $PIN 7k | sed 's/7k/7l/g'` after WP103's 7k (or after the last 7-label present),
   so the section reads `head2 "7l. PostToolUse advisories reach Claude: additionalContext, never bare
   stderr"`; nothing else changes.
4. `git -C "$WT" diff fdf9f38cb^ fdf9f38cb -- docs/modding/validation-and-testing.md | git -C "$WT" apply`.
5. hook-authoring.md: the Output channel row gains "; PostToolUse: `check-polearm-shield-parity.sh`
   (`additionalContext`)" and "; the polearm gate did the same until 2026-09-29" (pinned row).
   hooks-catalog: replace the `check-polearm-shield-parity.sh` Purpose cell with the pinned one, with
   its `tools/test_hooks.sh` 7k reference written 7l.
6. Stage; `hooks b2` green (7l checks every PostToolUse and PostToolUseFailure hook for unguarded
   stderr).
7. Commit with subject `fix(hooks): v2.0.28 - polearm gate reports through additionalContext`.
   Not-tested: delivery of `additionalContext` (live check L8).

### Step B3: The config override unlocks one session only

1. RED: Write `<notes>/probe-cp-adr.json` with
   `{"tool_name":"Edit","tool_input":{"file_path":"C:\\Users\\mikew\\source\\repos\\taom-port\\116\\docs\\adrs\\001-probe.md"},"hook_event_name":"PreToolUse","session_id":"wp116-probe"}`;
   `probe config-protection.sh "$NOTES/probe-cp-adr.json"` prints `rc=0` (the Windows path misses the
   ADR guard).
2. `blob $PIN .claude/hooks/config-protection.sh`; insert `sec $PIN 7g` after 7e, unchanged (its path
   strings name 1.5.x ADR files; the hook only reads the strings).
3. hooks-catalog: replace the `config-protection.sh` Purpose cell with the pinned one.
4. Probe again: `BLOCKED: Modifying ADR` and `rc=2`. Stage; `hooks b3` green.
5. Commit with subject `fix(hooks): v2.0.28 - config override unlocks one session only`. Body: the
   override file name now comes from the payload's `session_id` (then `CLAUDE_CODE_SESSION_ID`, then
   `CLAUDE_SESSION_ID`, cut to `[A-Za-z0-9_-]`; no id, no override), so one session's flag no longer
   unlocks every session; paths are normalised (backslashes to `/`, case folded) before matching, so a
   Windows ADR path is guarded (#677). Ported-from: `30abf550b`, `88e805450`.

**Part B done criteria**: three more subjects in the log; the three probes show ask, `rc=2` and the
fixed polearm output; `hooks partB` `0 failed` or baseline-only; `pyt` unchanged; dotnet build and
test as in Part A; `git status --porcelain` empty.

## Part C: security audit scope, MCP trim, context snapshots

Every Part C commit carries `Ported-from: fdf9f38cbdc14713964c1ced280af4686cbcf005`.

### Step C1: The security audit vets whole trees and the user scope

1. RED: `blob fdf9f38cb tools/tests/test_audit_user_scope.py`; `blob fdf9f38cb tools/tests/test_audit_skillspector.py`.
   If `grep -c '_claude_md_imports' "$WT/tools/audit_claude_config.py"` is 0 (WP103 did not port
   `c79a58521`'s import collection), delete the whole `class CollectTests` (from its `class` line to the
   line before `class ExternalLayoutTests`) from `test_audit_skillspector.py`.
   `pyt test_audit_user_scope.py` and `pyt test_audit_skillspector.py` fail (no `--user`, no
   `collect(..., external=...)`). Name these two files: the wider `test_audit_*.py` pattern also picks up
   `test_audit_map_scene_memory.py`, which imports `pytest`, absent on this laptop's Python.
2. GREEN: `blob fdf9f38cb tools/audit_claude_config.py`. If the base lacked `_claude_md_imports`,
   delete from the new file the seven lines in `collect()` that start at
   `# Every file CLAUDE.md @-imports loads into every session` and end at `out.append(p)` (keep
   `return out`), and the whole `def _claude_md_imports(...)` function (it imports
   `lint_docs.markdown_imports`, absent here). Both `pyt` commands from step 1 print `OK`
   (`OK (skipped=4)` for the second where YARA is absent; 16 new methods in `test_audit_user_scope.py`, 12 in `ExternalLayoutTests`).
3. `mkdir -p "$NOTES/empty"`; `cd "$WT" && python tools/audit_claude_config.py --root "$NOTES/empty" --external; echo rc=$?`
   prints an `UNCHECKED` line and `rc=3`. `python tools/audit_claude_config.py --min HIGH` shows nothing
   new. Do not run `--user` against the real home folder; that is the maintainer's choice.
4. Docs: `git -C "$WT" diff fdf9f38cb^ fdf9f38cb -- .claude/skills/security-scan/SKILL.md .claude/rules/external-skill-ports.md docs/reviews/adopt-mattpocock-improve-architecture-2026-06-22.md | git -C "$WT" apply`
   (if `git apply` refuses, use `git apply -3` and resolve; never leave conflict markers).
   `docs/ai-includes/external-repo-adoption.md` by hand: replace the "**Automated supplement to the
   manual read**" bullet with the `fdf9f38cb` version of that bullet, and append the
   "**affaan-m/ECC, re-review** (2026-09-29)" entry from `fdf9f38cb` at the end of the adoption list.
   `docs/reference/provenance-register.md` by hand: add the `fdf9f38cb` "ECC (Everything Claude Code)"
   table row after the "NVIDIA SkillSpector" row and the "### ECC (Everything Claude Code)" section
   before "### graphify", minus every mention of `tools/blender/harness.py` and `stance_height`; for
   each file the section says names ECC, confirm with `grep -il 'ECC\|everything-claude-code\|AgentShield' <file>`
   and drop names that do not.
5. Stage; `pyt`; `hooks c1`; lint report shows nothing new in touched files.
6. Commit with subject `feat(tools): v2.0.28 - security audit vets whole trees and user scope`. Body:
   `--external` reads the whole foreign tree (it scanned almost none of a plugin repo before) and gives
   every script and JSON file the hook rules; `--user` vets the machine-local scope; a run that
   collects nothing prints `UNCHECKED` and exits 3. Note the removed `c79a58521` part if you removed it.

### Step C2: Drop the unused github, git and filesystem MCP servers

1. If `git -C "$WT" diff -- .claude/settings.json` shows no M2 change, STOP and ask the maintainer to
   apply M2. Then `python "$NOTES/check_settings.py" "$WT/.claude/settings.json" --github-off` prints
   `OK`.
2. `.mcp.json`: delete the `github`, `filesystem` and `git` entries (keep valid JSON):
   `python -c "import json;d=json.load(open('C:/Users/mikew/source/repos/taom-port/116/.mcp.json',encoding='utf-8'));print(sorted(d['mcpServers']))"`
   prints `['elevenlabs', 'ilspy', 'imagine', 'serena', 'taom-moduledata']`.
3. `git -C "$WT" diff fdf9f38cb^ fdf9f38cb -- .claude/skills/context-budget/scan.sh docs/reference/development-machines.md | git -C "$WT" apply -3`
   (`-3` is required: a plain apply refuses development-machines.md, whose next bullet differs on
   1.4.x; the three-way merge was run clean at planning, and it stages both files).
   `grep -c '^<<<<<<<' "$WT/docs/reference/development-machines.md"` prints `0`. Then, with the Edit tool,
   in the development-machines.md sentence, "removed 2026-09-29" becomes "removed by WP116 (1.5.x
   removed it 2026-09-29)" and "in its own `settings.local.json`" becomes "in the tracked
   `.claude/settings.local.json`".
4. `docs/reference/mcp-servers.md`: delete the GitHub, filesystem and git rows of the server table and
   add below it: "**Removed by WP116 (from 1.5.x `fdf9f38cb`): `github`, `git`, `filesystem`.** No TAOM
   skill, agent or rule called them; each duplicated a tool with gates of its own (`gh`, git through
   Bash, Read/Glob/Grep). `github@claude-plugins-official` declares the same `github` server, so it is
   disabled in `.claude/settings.json` too. Record: [adopt-ecc-2026-09-29.md](../reviews/adopt-ecc-2026-09-29.md), Step 4."
   In "Denied write tools", say the nine git and filesystem entries in `.claude/settings.local.json` now
   name servers that are gone (inert; the maintainer may delete them) and drop the "Read-only tools are
   untouched: `git_diff` ..." sentence. The Configuration list becomes "(Serena, ilspy,
   taom-moduledata, imagine, elevenlabs)". The plugin-overlap row "GitHub issues/PRs" becomes
   "`gh` CLI" against "`github` plugin (disabled)".
5. `CLAUDE.md` "MCP Servers": the count line becomes "7 servers (5 project via `.mcp.json` incl.
   `imagine`, 2 user)" (5 is the count from step 2); in the MCP Usage Guide, the filesystem row's tool
   becomes **Read/Grep/Glob**, and the git and GitHub rows are deleted and replaced by one sentence under
   the table: "git goes through Bash and issues through the `gh` CLI (the git, filesystem and GitHub
   MCP servers were removed by WP116)." `lint_docs.py --fail-on-drift` exits 0.
6. In `.claude/skills/security-scan/SKILL.md` and in the `_plugin_roots` docstring of
   `tools/audit_claude_config.py`, "enable seven" becomes "enable six"; confirm with
   `python -c "import json;d=json.load(open('C:/Users/mikew/source/repos/taom-port/116/.claude/settings.json',encoding='utf-8'));print(sum(1 for v in d['enabledPlugins'].values() if v is True))"`
   printing `6`.
7. Stage (including `.claude/settings.json`); `pyt`; `hooks c2`.
8. Commit with subject `chore(mcp): v2.0.28 - drop the unused github, git and filesystem servers`.
   Body: no skill, agent or rule called them; the filesystem server pointed at desktop `E:\` folders
   and failed to start on the laptop; the git server's write tools bypassed every gate and were held
   shut only by deny rules; the github plugin declares the same server, so it is disabled as well.
   Not-tested: `/mcp` after a restart (live check L9).

### Step C3: Context snapshots record failed approaches

1. `git -C "$WT" diff fdf9f38cb^ fdf9f38cb -- .claude/skills/context-save/SKILL.md .claude/skills/context-restore/SKILL.md | git -C "$WT" apply -3`
   (`-3` is required: a plain apply refuses context-restore/SKILL.md, whose next line differs on 1.4.x;
   the three-way merge was run clean at planning). `grep -c '^<<<<<<<' "$WT/.claude/skills/context-restore/SKILL.md"` prints `0`.
2. Stage; `hooks c3` (3b parses the skill frontmatter); lint report clean for both files.
3. Commit with subject `docs(skills): v2.0.28 - context snapshots record failed approaches`. Body:
   `/context-save` asks for the approaches tried and abandoned, and `/context-restore` shows them first
   and warns when a snapshot is over 7 days old, behind HEAD, or names a commit git no longer knows.

**Part C done criteria**: three more subjects; the C1 and C2 command checks as stated; `pyt` shows
`OK` or baseline-only; `hooks partC` `0 failed` or baseline-only; `lint_docs.py --fail-on-drift`
exits 0; dotnet build and test as in Part A; `git status --porcelain` empty; the hand-off reports
status, commit hashes and the hook-suite and tool-test counts you measured.

## Disposition table

Every commit in the pack, plus the shared hunks. Full shas for Ported-from lines.

| # | 1.5.x commit (40-hex) | Subject | Disposition | Where, or why not |
|---|---|---|---|---|
| 1 | `90141357d4113e5a5dbd8acd1a3acd862c510afc` | perf(hooks): skip Python in Bash hooks on non-git calls | transplanted | A3 (prefilters via blobs and G2; 4c); A5 catalog figures. Its notify-test-results and suggest-compact edits are moot (deleted in A1) |
| 2 | `787fd3663d5238c45c3e4bf8b08c98d9229e953b` | review follow-ups for plan 013 | transplanted | A3 (comments, 4c trace check, section 4 rows); A5 (hook-authoring). Review record, RCA, REVIEW-LOG, lessons not ported (1.5.x archival; WP142 syncs lessons) |
| 3 | `b34d615a0b878b8f70085b522c8b8189bd0a56e7` | convergence fixes for plan 013 | adapted | A5 (catalog before and after timings); record and lesson hunks not ported |
| 4 | `5dcef67ac6ee9812ea8c999b492b70bdcedea1e3` | Codex prompt for plan 013 | skipped | archival 1.5.x review prompt citing 1.5.x hashes |
| 5 | `7aa658e3e6c12db7a38fffe403ec45a5e0fee2fb` | apply maintainer decisions for plan 013 | transplanted | A3 (each gate filters on its own word, the `\u` arm; 4c, 4d). The CHANGELOG-gate form is moot (WP102 deleted it) |
| 6 | `6514fdfe77d85b9c49e9dfb59bb81b21e4cfb4a4` | decision review follow-ups (#661) | transplanted | A3 (comments; git -C and escaped rows in 4c) |
| 7 | `ee1929fa4c2a9f6fd0476533b1acf14cd1bb1786` | convergence fixes for plan 013 | transplanted | A3 (4c git -C push row, 4d header); A5 catalog wording |
| 8 | `2feb70ad6db8464e02adf11fe1a9fa530fc09e9f` | cite #661 for plan 013 | skipped | CHANGELOG only (D1) |
| 9 | `43e6780e4421fb785ac9bf1c4ee5bc53e8b69a68` | Stop reminders reach Claude, guard both trunks | transplanted | A1 (two hooks deleted, PowerShell marker via M1), A3 (validate-push), A4 (Stop channel), A5 (rules; CLAUDE.md contract rows instead of its 1.5.x CLAUDE.md lines). Its check-changelog-updated hunk is moot (WP102) |
| 10 | `c80c4108c88e9d5e28ec047458542235639f53c7` | review follow-ups for plan 011 | transplanted | A3 (marker split, validate-push end state), A4 (guard placement), A3 step 9 (finish-branch). Review report, RCA, lessons not ported |
| 11 | `65b405e42529bc390c9e9b8eca3e0427948c3389` | convergence fixes for plan 011 | transplanted | A3 (validate-push end state, whose `test_pushjudge.py` keeps these 7c rows as pins); A4 (comments) |
| 12 | `18961c1ec6b92c54b54bd1d25e6cd3ec066c6b03` | record failed runs, plan 011 settings | transplanted | M1 and A1 (PostToolUseFailure), A3 (marker, 7c and 7d rows); catalog counts re-derived on 1.4.x (A5); Codex prompt not ported |
| 13 | `764ab0984eeedd7658a67a3829d46aa338cd1b8d` | final convergence fixes for plan 011 | transplanted | A3 (validate-push end state; 7c rows) |
| 14 | `79792aedcc7df2583277e2307ba8749f7dae22cc` | plan 011 merged | skipped | 1.5.x `plans/README.md` bookkeeping |
| 15 | `b23dc8999a9a0339fcee23a603003f4b60237c88` | read PowerShell commands as Bash text | transplanted | A2 |
| 16 | `e046bdf09a9f4cf9b6924d59444e774710dab75a` | run the eight git gates for the PowerShell tool | transplanted | A3 (`taom_hook_command`, every gate reads through it, block-broad-git-add loop, `py` in the files-tracked filter, T7, 7e). Its matcher change was WP103's D19 diff |
| 17 | `3a061406134188239ee506372aaf1ee5d486485f` | close validate-push glob, option and comment gaps | adapted (partial) | A3: its 7c rows and the segment reading; its bash judge is superseded by `_pushjudge.py`, which pins these verdicts |
| 18 | `cfd3b48f64cbddb658d646fc6b654d32bcc4977e` | leave an interrupted build or test unmarked | transplanted | A3 (marker `is_interrupt`; 7d rows) |
| 19 | `2d7770b647878b845b0b8e77e5695060d1f0b121` | catalogue the PowerShell gate coverage | adapted | A5 (counts re-derived for the 1.4.x hook set) |
| 20 | `64b9f3a6c2b522cdd5ee353f9ce594c7e4e9d5db` | review fixes for plan 027 | transplanted | A2, A3 (gates, `_pybin.sh`, 6 and 7e rows), A5. Review report, Codex prompt, RCA, REVIEW-LOG, lessons not ported |
| 21 | `747b6dae8e68ae9354739cc7a5d696a9733f559b` | convergence fixes for plan 027 | transplanted | A2, A3 |
| 22 | `6fe4c8fe4b7f0f44c5173b115aed265ca95be1b0` | plan 027 merged, issues and a harness fact | adapted (partial) | A5: only the "ask prompts even in bypass mode" row; plans files not ported |
| 23 | `a4ba6b0b3e17145c0d0d58d73fdf98b4be18b258` | judge force pushes in Python within a deadline (#680) | transplanted | A3 (`_pushjudge.py`, validate-push, tests, 7e and 7i, S1); A5 (rule row). Lessons not ported |
| 24 | `60d85ed7d5a98d0a298c492866f26d72cadd1db5` | drop the push line ordering | transplanted | nothing separate: the pinned reader and its tests already lack the ordering (A2) |
| 25 | `fd1d5c17de387bda26a5966396b69c1782e3a9e8` | refuse abbreviated force options, matching pushes (#689) | transplanted | A3 (judge, tests, 7c rows) |
| 26 | `343c6f48881b20e74fd1ca234ab0029f8941d1c7` | scope the coarse push check to the command | transplanted | A3 (validate-push; 7e rows) |
| 27 | `fff6d66e9bd5a2db11c0ef20ab167225efbd1a22` | review fixes for the push guard work | transplanted | A2 and A3 (blobs, G4, 4d, 4e, 7c, 7e, 7h, 7i), M1 (10 s, decided here), A5 |
| 28 | `05dbc0d491beaea609c766215c2847bb0528325b` | plan 027 and its two cold reviews | skipped | 1.5.x plan files |
| 29 | `5f256f704da06728159f59f7f137caa209dd348f` | judge the shortest push line first | skipped (superseded) | removed again by `60d85ed7d`; the pin lacks it |
| 30 | `dad9b169ef8990fe97f0b136736d5c5a66852fa1` | judge the push lines that could force first | skipped (superseded) | same |
| 31 | `d2fa6d08158f1ec3842c570877058ce7d3f4cbe4` | keep the push line hint linear on dash runs | skipped (superseded) | same |
| 32 | `fdf9f38cbdc14713964c1ced280af4686cbcf005` | ECC re-review (mixed commit) | adapted (partial) | A1, A5 (deny row), B1, B2, C1, C2 (with M2), C3. Not ported: lords.xml (content), `tune_face_slider_reach.py` and its test, `tools/blender/*` and the gait test, the skins snapshot and race-face docs, the upper-mesh investigation, module-armory.md, elephant.md, the creature-animation workflow doc and `refine-creature-anim` (Blender toolkit stays on 1.5.x), armory-audit skill and the 4-completeness lens (absent here), the plan-template line (present at the 1.5.x tip, arrives with WP138), git-and-commits.md (absent), INDEX, lessons, LESSONS-LEARNED, tools/README.md (WP142, WP143), `rca-ecc-adoption-2026-09-29.md` (archival) |
| 33 | `88e80545094ad408d8376c66c51ea6ebbb59b781` | config override unlocks one session only | transplanted | B3. Its `execute-and-review.md` hunk not ported (file absent here; WP138) |
| 34 | `37ff369222c336b33470032eff2f83c43b058a13` | watch Coop releases, record Coop versions | skipped | needs the upstream-watch tooling (`tools/upstream_watch.py`, its hook, skill and `docs/reviews/upstream-watch.json`), none of which is on 1.4.x; WP103 leaves that half of `7e26d77fe` and `407570ece` on 1.5.x so one line owns the watch file; its coop-interop.md table is written for TAOM on v1.5.6, and co-op is not supported here (D7) |
| 35 | `d01bddf104e9271f600e5ad5895b71faedcaa888` (shared) | push-judge half only | transplanted | A2 and A3 (the judge docstring, the offset comment, the test-class rename are in the pinned blobs); its 7j and block-no-verify comment half is WP103's |
| 36 | `30abf550b7fa5613984bc3ec55cde80aa86f4ba1` (shared) | config-protection normalisation hunk only (#677) | transplanted | B3 (in the pinned config-protection.sh; its 7f rows are the pinned 7g). The rest of the commit is WP130's and others' |

## Test plan

- **Python unit tests**: new `tools/tests/test_pushjudge.py` (25 methods, table-driven: every 7c
  verdict of the old bash judge, traps, #689 shapes, value prefixes, push.default, CLI),
  `tools/tests/test_audit_user_scope.py` (16), `ExternalLayoutTests` in `test_audit_skillspector.py`
  (12); `tools/tests/test_shellwords.py` (83) arrives with WP103 and is replaced only if A2 runs.
  Counts are `def test_` lines read at planning; `Ran N` counts methods, not subtests.
- **Hook suite sections**: added 4c (no Python on a payload without the hook's word; reach on its word,
  on `git -C`, on an escaped letter), 4d (escaped word answers the same), 7a (Stop block channel, loop
  guard, streak markers), 7d (marker invocations versus mentions, failures, interrupts), 7e
  (PowerShell parity for every gate; dangerous-git arms in B1), 7g (config paths, per-session
  override), 7i (long pushes inside the deadline), 7l (the pinned 7k: PostToolUse output channel);
  replaced 4e, 7c (both trunks, every line, both shells, registrations) and 7h; edited 4, 5, 6, 7b.
  Each is a 1.5.x section copied with `sec`, so nothing new is modelled.
- **Not testable here** (Not-tested trailers and live checks): that Claude Code delivers a Stop block
  reason, an `additionalContext` and an ask prompt; the PowerShell tool's real payload shape; whether
  a timeout sets `is_interrupt` (UNVERIFIED upstream).
- **C# suite**: unchanged; run once per Part as a guard.

## Done criteria (whole WP)

- [ ] Parts A, B and C done criteria all hold.
- [ ] `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD` lists this plan's subjects and nothing
      else: 10 when A2 was skipped, 11 otherwise.
- [ ] `git -C "$WT" log --format='%(trailers:key=Ported-from,valueonly)' bannerlord-1.4.5..HEAD | sed '/^$/d' | sort -u | wc -l`
      is 28: every ported row of the disposition table, the two shared hunks included
      (`d01bddf10` in A2 or A3, `30abf550b` in B3).
- [ ] No file outside the Scope list changed: `git -C "$WT" diff --name-only bannerlord-1.4.5..HEAD`.
- [ ] `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD | grep -vc ': v2.0.28 - '` prints 0, and
      `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD | awk 'length > 72'` prints nothing.
- [ ] `git -C "$WT" diff --name-only bannerlord-1.4.5..HEAD -- plans/README.md` prints nothing, and the
      hand-off reports status, commit hashes and measured counts for the integrator to record.

## STOP conditions

Stop and report (do not improvise) if:

- A Step A0 check fails: WP102 or WP103 is not merged or not complete.
- `later` reports `BASE EQUALS LATER STATE` for any file: WP103 imported a 1.5.x state newer than the
  pin, and copying the pin would roll it back.
- M1 or M2 is not applied, or `check_settings.py` still prints `FAIL` after the maintainer's edit.
  Never edit `.claude/settings.json` yourself by any route.
- A lost-function or lost-helper check finds a caller (A2 step 2, A3 step 5).
- A label this plan inserts (4c, 4d, 7a, 7d, 7e, 7g, 7i, 7l) already exists in the base (7c, 4e and 7h
  are expected and replaced by T8, T4 and T11).
- After a GREEN step, a hook-suite or tool-test row fails that the adaptations here do not explain,
  or a WP102 or WP103 section fails other than through T4, T7, T8 or T11. Never loosen an assertion,
  threshold or allowlist.
- `test_pushjudge.py` fails on this laptop's git 2.54 (the notes cite 2.55): report the row.
- The hook suite hangs (an `rc=124` row at the 10 s bound), or runs over 30 minutes.
- `lint_docs.py --fail-on-drift` stays non-zero after trimming your own CLAUDE.md additions.
- A main-checkout gate denies your commit on untracked `.claude/` files you did not create (another
  session's work), or for a reason you cannot fix in the message file.
- `audit_claude_config.py --min HIGH` reports a new HIGH or CRITICAL finding in a transplanted file.
- Any step seems to need a file outside Scope (`Main/**`, `IoC.cs`, `SubModule.cs`, a csproj,
  `.githooks`, ModuleData).

## In-game checks owed

None. This WP changes no game code and no game data.

## Live harness checks owed (maintainer, after the integrator merges WP116)

Restart Claude Code in the main checkout so the merged hooks and settings load. Each check uses a
throwaway branch or `--dry-run`; record the outcomes in the WP116 tracking issue.

- **L1** (Bash, two lines): `cd .` then `git commit --dry-run -m "no label here"`: denied by the
  subject gate.
- **L2** (PowerShell tool): `git commit --dry-run -m "no label here"`: denied.
- **L3** (PowerShell): `git commit --dry-run -m @'` newline `docs: v2.0.28 - live probe` newline `'@`:
  allowed.
- **L4** (PowerShell): `GIT add --dry-run -A`: an ask; decline it.
- **L5** (PowerShell): `git commit --dry-run --no-verify -m x`: blocked.
- **L6** (PowerShell and Bash, from a non-trunk branch): `git push --dry-run --force origin HEAD:bannerlord-1.4.5`
  and the same to `HEAD:bannerlord-1.5.x`: both blocked before git runs.
- **L7**: in a throwaway worktree session, edit one `.cs` file without building and end the turn:
  Claude answers a `check-verification-evidence` block reason once; revert the edit.
- **L8**: after a throwaway roster write that the polearm audit flags, the session shows the polearm
  report as additional context; revert the write.
- **L9** (after Part C): `/mcp` lists no github, git or filesystem server.
- When L1 to L6 pass, D19's Bash-only rule for executors can be retired.

## Maintenance notes

- **Pin and follow-ups.** The gate files equal 1.5.x `88e805450` except the three delta gates,
  `session-start.sh`, `detect-docs-gaps.sh`, `check-native-dll-crt.sh` (1.4.x keeps it) and the
  comment adaptations. WP138 (`caa7ac356`: git gates read past wrapper words such as `env`,
  `command`, `timeout`; `09f3126a7`) and WP143 (`9cfcadd07`, `434d1d795`, `3ea14a730`) apply their
  1.5.x deltas on top of this state.
- **Known gaps carried over** (1.5.x catalog): HEAD, no refspec and `push.default` resolve in the
  hook's directory; `push.default` set through `--config-env` or `GIT_CONFIG_*`, `remote.<name>.push`
  and `.mirror` are not read; `git commit -n` and `--no-veri` pass block-no-verify; `git commit-graph
  write && git commit` passes the commit gates; git run through another PowerShell command
  (`Start-Process`, `Invoke-Expression`, `& ("git")`) passes the commit and confirm gates, though
  validate-push still judges the raw text.
- **check-deep-review.sh** looks for `agent_type=deep-reviewer`, the agent WP138 brings from
  `fa8c06f82`; until then it reminds once per streak and re-arms on a clean tree.
- **Other WPs on the same files**: WP108 edits `check-moduledata-validation.sh` (its `--code` list)
  and `session-start.sh` (drift block); those hunks do not overlap G1 to G5 or S1.
- **Test labels**: this line's `tools/test_hooks.sh` 7k is WP103's settings check, and the 1.5.x 7k
  (PostToolUse output channel) lives here as 7l; a later sync from 1.5.x must keep that mapping (the
  1.5.x 7l, upstream-watch, is not ported).
- **Reviewer focus** (no C# here, so `/deep-review` is not mandated; recommend `/security-scan`, and
  `/review-codex` only if the maintainer asks, since it costs money): the G1 to G5 deltas, the M1 and
  M2 diffs, the AGENTS.md adaptations, test-section placement and adaptations (T1 to T12), and the
  `c79a58521` removal in the audit tool.
- **Deferred on purpose**: the provenance-register and lessons syncs beyond the ECC row (WP142), the
  plan-template gate-editing line (WP138), the deny entries and trust names in
  `.claude/settings.local.json` (maintainer), Coop version facts from `37ff36922` (only if co-op ever
  becomes supported on 1.4.x).
