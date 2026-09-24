# Plan 013: Let non-git Bash calls skip the Python start-up in every Bash hook

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> maintains that index.
>
> **Where you work (read this twice)**: all work happens in the worktree `E:/repos/wt-plan-013`,
> never in the main tree `E:/repos/TAOM`. The main tree's hooks run in every live Claude session
> and it holds another session's uncommitted edits. Your Bash working directory resets to
> `E:/repos/TAOM` between calls, so:
> - **Every Bash call begins with `cd /e/repos/wt-plan-013 && `** (the one exception is the
>   `git worktree add` in Step 0, which uses `git -C`). A relative path in a call without that
>   prefix runs against the main tree.
> - **Every Read, Edit and Write path is absolute**: `E:/repos/wt-plan-013/<repo path>`. Never
>   open anything under `E:/repos/TAOM/` for editing.
> - **Scratch files** (test output, the timing and parity scripts) go in
>   `E:/repos/wt-plan-013-scratch/` (Bash: `/e/repos/wt-plan-013-scratch/`), created in Step 0,
>   never inside the worktree. Leave it in place at the end for the orchestrator.
> - **Bash tool timeout**: pass `timeout: 600000` on every call that runs `tools/test_hooks.sh`,
>   the parity or timing script, `dotnet build` or `dotnet test`. The 120 s default can kill them.
>
> **Shell**: every command in this plan is written for the **Bash tool** (Git Bash). Write any
> multi-line script with the Write tool, never a Bash heredoc (heredocs mangle backslashes and
> quotes here). Never spell `python3`; use `python`.
>
> **Drift check (run first, after Step 0 creates the worktree)**:
> `cd /e/repos/wt-plan-013 && git diff --stat b2e387db..HEAD -- .claude/hooks/_pybin.sh .claude/hooks/block-no-verify.sh .claude/hooks/validate-push.sh .claude/hooks/check-changelog-changed.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-native-dll-crt.sh .claude/hooks/check-doc-config-drift.sh .claude/hooks/notify-test-results.sh .claude/hooks/mark-verification-run.sh .claude/hooks/suggest-compact.sh .claude/settings.json tools/test_hooks.sh docs/reference/hooks-catalog.md .claude/rules/hook-authoring.md`
> Expected: no output (at planning time `HEAD` was `4b5662b2`, which touched none of these).
> `CHANGELOG.md` is in scope but deliberately left out of the drift check: every session appends to it.
> If the command prints anything, STOP and report it: the line numbers below are then stale
> (this is expected if plan 011 landed first; see "Depends on").

## Status

- **Priority**: P3
- **Effort**: S (13 hook scripts get a 3 to 6 line move each, one new test section, three doc lines, one CHANGELOG entry)
- **Risk**: LOW (latency only; the one real hazard, a prefilter narrower than a gate's trigger, is pinned by a new test in both directions and by an old-versus-new parity run)
- **Depends on**: none, but it shares five files with `plans/011-stop-reminders-and-trunk-guard.md` (`validate-push.sh`, `mark-verification-run.sh`, `tools/test_hooks.sh`, `docs/reference/hooks-catalog.md`, `.claude/rules/hook-authoring.md`). Run the two one after the other, never at the same time. Whichever runs second hits its drift-check STOP, and the orchestrator refreshes its line numbers (and re-checks the `hook-authoring.md` byte budget, Step 8) before re-dispatching it.
- **Category**: dx
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

Every Bash tool call in a Claude Code session runs 13 TAOM hook scripts. Each one sources `.claude/hooks/_pybin.sh`, which starts Python once to prove the interpreter is live, and then starts Python a second time to read `tool_input.command` out of the JSON payload, and only then asks whether the command is a `git` (or `dotnet`) command at all. Measured at planning on this desktop with a non-git `ls docs` payload: 200 to 328 ms per hook (5-run means) against a 58 ms bare `bash` spawn, a serial sum of about 2.9 s across the 12 timed hooks for every Bash call (they run in parallel, so the wall cost is lower but the CPU is spent), and the checker's transcript count says about 82% of Bash calls contain no `git` anywhere in the payload. After this plan each hook first tests the raw payload for its own trigger text with a bash builtin and exits with its normal allow output when the text is absent, so those calls start no Python at all (about 60 ms per hook), while every call that could concern a gate takes exactly the path it takes today.

## Current state

All excerpts are from commit `b2e387db`. None of the in-scope files differ between `b2e387db` and the planning-time `HEAD` (`4b5662b2`), and none has uncommitted edits in `E:\repos\TAOM` except `CHANGELOG.md` (another session's work; you edit only your worktree's copy). The main tree's git index was empty at planning.

### Files and roles

Registrations (`.claude/settings.json` at `b2e387db`, not edited by this plan):

| Event | Matcher | Scripts (registered timeout) |
|---|---|---|
| `PreToolUse` | `Bash` | `block-no-verify.sh` (5), `validate-push.sh` (5), `check-changelog-changed.sh` (5), `check-claude-files-tracked.sh` (5), `check-commit-subject-version.sh` (10), `block-dangerous-git.sh` (5), `block-broad-git-add.sh` (5), `check-moduledata-validation.sh` (60), `check-native-dll-crt.sh` (5), `check-doc-config-drift.sh` (30) |
| `PreToolUse` | `""` (every tool) | `suggest-compact.sh` (5) |
| `PostToolUse` | `Bash` | `notify-test-results.sh` (5), `mark-verification-run.sh` (5) |

- `.claude/hooks/_pybin.sh`: sourced helper, **not edited**. Its file scope runs the resolve, which probes the interpreter:
  ```bash
  # _pybin.sh:88-90
  taom_pybin_probe() {
      [ "$(printf '' | timeout -k 0.2 0.8 "$1" -S -E -c 'import sys; sys.stdout.write("taompy")' 2>/dev/null)" = "taompy" ]
  }
  # _pybin.sh:98 (inside taom_resolve_python): a TAOM_PYBIN pin is validated AND probed
      if taom_pybin_is_safe "${TAOM_PYBIN:-}" && taom_pybin_probe "$TAOM_PYBIN"; then
  # _pybin.sh:141-147
  # NOTE: this resolution is per-process and cannot be shared. Each hook is spawned
  # separately by the harness, so an `export` here reaches nothing: every hook pays
  # the probe again. Measured cost of that on a Bash tool call: ~77ms per hook,
  # ~850ms of CPU across the ten PreToolUse hooks. The fix is the TAOM_PYBIN pin in
  # settings.json (validated above), not an export.
  PYBIN=$(taom_resolve_python || true)
  export PYBIN
  ```
  The probe feeds `printf ''` to the candidate, so sourcing `_pybin.sh` never reads the hook's stdin. Reading `INPUT=$(cat)` before the `source` is therefore safe.

- The eight `git commit` gates share one preamble shape. Example, `check-changelog-changed.sh:14-42` (the `...` elides the Python parse body at `:26-31`):
  ```bash
  set -uo pipefail

  # Resolve a safe Python (never a Microsoft Store alias — those hang forever).
  source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

  INPUT=$(cat)

  # Fail open, but never fail silent: for a gate, no output reads as "nothing to report".
  taom_pybin_degraded "check-changelog-changed" "the CHANGELOG-staged requirement" && { echo '{}'; exit 0; }

  # Extract the bash command from tool_input.
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
  ...
  ' 2>/dev/null)

  # Detect `git commit` invocations including `git -C <dir> commit` and
  # `git -c <key>=<val> commit`. Reject `git commit-tree`, `commit-graph`, etc.
  case "$COMMAND" in
      *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree / commit-graph etc — different command
  esac
  case "$COMMAND" in
      *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
      *) echo '{}'; exit 0 ;;
  esac
  ```
  The six-line block `set -uo pipefail` / blank / `# Resolve a safe Python ...` / `source ...` / blank / `INPUT=$(cat)` sits at these lines:

  | Script | Lines | Its non-trigger exit today |
  |---|---|---|
  | `check-changelog-changed.sh` | 14-19 | `echo '{}'; exit 0` (line 41) |
  | `check-claude-files-tracked.sh` | 15-20 | `echo '{}'; exit 0` (line 41) |
  | `check-commit-subject-version.sh` | 29-34 (its comment reads `(never a Microsoft Store alias, those hang forever)`) | `echo '{}'; exit 0` (line 56) |
  | `block-dangerous-git.sh` | 32-37 | `echo '{}'; exit 0` (line 106) |
  | `block-broad-git-add.sh` | 42-47 | `echo '{}'; exit 0` (line 121) |
  | `check-moduledata-validation.sh` | 25-30 | `echo '{}'; exit 0` (line 52) |
  | `check-native-dll-crt.sh` | 20-25 | `echo '{}'; exit 0` (line 48) |
  | `check-doc-config-drift.sh` | 27-32 | `echo '{}'; exit 0` (line 54) |

- `block-no-verify.sh:1-4` and `:27`, `:47-50` (its non-git exit prints nothing):
  ```bash
  #!/bin/bash

  # Resolve a safe Python (never a Microsoft Store alias — those hang forever).
  source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
  # PreToolUse(Bash): refuse any git command carrying --no-verify.
  ...
  INPUT=$(cat)
  ...
  [[ -z "${COMMAND:-}" ]] && exit 0
  # Only git commands. --no-verify means something else entirely to other tools.
  [[ ! "$COMMAND" =~ (^|[[:space:]])git([[:space:]]|$) ]] && exit 0
  ```
- `validate-push.sh:6-13` (its non-git exits at `:54` and `:63` print nothing):
  ```bash
  # Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
  # This MUST stay above the first "$PYBIN" use below. It was previously sourced at the
  # bottom of the flag-parsing block, so PYBIN was empty when line 16 ran, COMMAND came back
  # empty, and the force-push block below was unreachable. Verified dead 2026-08-31: a
  # `git push --force origin bannerlord-1.4.5` payload returned rc=0 with no output.
  source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

  INPUT=$(cat)
  ```
  It needs a token `git`, `*/git`, `git.exe` or `*/git.exe` before `push` (`:57-63`).
- `notify-test-results.sh:1-6` (trigger: `echo "$COMMAND" | grep -q "dotnet test"` at `:39`; otherwise `exit 0` with no output):
  ```bash
  #!/bin/bash

  # Resolve a safe Python (never a Microsoft Store alias — those hang forever).
  source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
  # PostToolUse hook: summarize dotnet test results prominently
  INPUT=$(cat)
  ```
- `mark-verification-run.sh:11-14` (triggers, `:66-73`: a segment whose first word is exactly `dotnet` followed by `dotnet build` or `dotnet test`; a first word `./build.ps1`, `build.ps1` or `*[/\\]build.ps1`; or `pwsh|powershell|powershell.exe` with `*build.ps1*`; otherwise `exit 0`, no output):
  ```bash
  # Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
  source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

  INPUT=$(cat)
  ```
- `suggest-compact.sh`: registered on every tool. The counter logic is pure bash; only the Bash branch loads Python, at `:76-90`:
  ```bash
  COMMAND=""
  if [ "$TOOL_NAME" = "Bash" ]; then
    # Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
    source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
    if [ -n "$PYBIN" ]; then
      COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
  ```
  Its boundary signals (`:114-138`) are `*"git commit"*`, `*"git -"*" commit"*`, `*"./build.ps1"*`, `*"dotnet build"*`, `*"dotnet test"*` and `*"git push"*`; with no signal it prints `{}` exactly as it does with an empty `COMMAND`.
- `tools/test_hooks.sh` (825 lines): the hook contract suite. It `cd`s to its own repo root (`:26-27`), so `bash tools/test_hooks.sh` run from the worktree tests the worktree's hooks. Helpers (`:32-36`): `ok "<msg>"` prints only with `--verbose`; `bad "<msg>"` prints `  FAIL <msg>` inline (with colour codes: the bytes are `FAIL`, ESC`[0m`, a space, the message). **When any check fails, the Summary block (`:818-825`) prints every failure message a second time** as `    - <msg>`, so a plain `grep -c "<failure text>"` over a failing run counts each failure twice. Every failure count in this plan therefore greps `FAIL.*<text>`, which matches the inline form only. Other helpers: `head2 "<title>"`, `$HPY` (a safe Python for the suite itself). Section 4 (`:300-386`) creates `$SANDBOX` and runs every hook on five payloads; 4b (`:388-414`) times each PreToolUse gate on a `git commit` payload against 80% of its registration and ends with `done` at `:414`; line `:415` is blank; section 5's banner is `:416` (a dashes line) and `:417` (`# 5. Starved environment: no jq, no python at all.`); section 5 (`:416-493`) runs every hook with no Python on PATH on a `git push --force origin master` payload and requires each blocking gate to say so on stderr; 5c2 feeds multi-line `git` commands.
- `docs/reference/hooks-catalog.md:12-15` (the authoring preamble; `:14` is a lone `>` separating blockquote paragraphs):
  ```text
  > **Source `_pybin.sh` and use `"$PYBIN"`**, then honour its contract
  > (`[ -n "$PYBIN" ] || { echo '{}'; exit 0; }`). `block-dangerous-git.sh` is the model.
  >
  > This paragraph used to say "use the python3 fallback". That advice, written 2026-08-20, is what
  ```
- `.claude/rules/hook-authoring.md:153` (12,188 bytes at `b2e387db`; `tools/lint_docs.py` warns above 12,288 bytes for a path-scoped rule; this plan adds 24 bytes):
  ```text
  **Inside a hook:** `source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"` at the TOP, above the
  ```

### Why the raw substring test is safe (the superset argument)

Every decision any of the 13 scripts makes, other than "allow", needs its trigger text in the **decoded** command: the literal `git` for the ten PreToolUse gates (`(^|[[:space:]])git(...)`, a `git` token before `push`, `*"git commit"*` / `*"git -"*" commit"*`, or a segment starting `git`), `dotnet test` for `notify-test-results.sh`, `dotnet` or `build.ps1` for `mark-verification-run.sh`, and `git`, `dotnet` or `build.ps1` for `suggest-compact.sh`. The harness's JSON serializer escapes only `"`, `\`, control characters (and optionally non-ASCII); it never escapes an ASCII letter, a digit, `.` or a space. (JSON as a format would permit `\u0067` for `g`; the harness does not emit that. See Maintenance notes.) So when the decoded command contains `git`, the raw payload does too. The converse over-includes (the word in a description, a path or a tool response merely passes the prefilter and takes today's full path), which is the safe direction.

**Do not use a token regex on the raw payload.** The lane's first design, `[[ "$INPUT" =~ (^|[^[:alnum:]_.-])git([^[:alnum:]_-]|$) ]]`, was refuted by the checker: the harness sends a newline as the two characters `\n`, the `n` is alphanumeric, so `cd /e/repos/TAOM\ngit commit -m x` and `...\ngit push --force origin bannerlord-1.5.x` would have skipped every commit and push gate. The plain `*git*` substring has no such hole. The checker measured on 5,754 retained Bash calls that `*git*` passes 17.8% of them.

### Measured at planning (this desktop, 5-run means, `ls docs` payload, `TAOM_PYBIN=C:/Python314/python.exe`)

Bare `bash -c 'exit 0'`: 58 ms. `block-no-verify` 247, `validate-push` 221, `check-changelog-changed` 253, `check-claude-files-tracked` 200, `check-commit-subject-version` 214, `block-dangerous-git` 289, `block-broad-git-add` 245, `check-moduledata-validation` 213, `check-native-dll-crt` 207, `check-doc-config-drift` 207, `notify-test-results` 328, `mark-verification-run` 233 (`suggest-compact` was not timed at planning because it writes a counter). A prefilter-shaped script (`INPUT=$(cat)`, one `[[ == *git* ]]`, exit) measured 58 ms against a 49 ms floor, and 78 ms on a 1 MB payload. Also verified at planning: a `#!/bin/sh` file with `chmod +x` passes `taom_pybin_is_safe` and the probe when pinned through `TAOM_PYBIN` in Git Bash (mounts are `noacl`, so a shebang file reports `-x`), which is what section 4c relies on.

### Conventions that bind this change

- **No C# changes.** ADR-002 (thin entry points under 150 lines), ADR-007 (adapters for sealed TaleWorlds types) and ADR-008 (service testability) do not apply; `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` and `Directory.Build.props` are single-owner files that this plan neither needs nor touches.
- **ADR-011 (knowledge delivery tiers):** a convention a machine can check gets the gate first, then one line naming it. Here: section 4c is the gate; one paragraph in `docs/reference/hooks-catalog.md` names it; the path-scoped rule `hook-authoring.md` gets a minimal wording fix so it no longer says "at the TOP".
- **`.claude/rules/harness-facts.md` "Hook lifecycle":** all hooks matching an event run in parallel; a timed-out hook is killed and its output discarded (a gate then fails open); PreToolUse prints `{}` (or nothing) to allow, and a decision only counts under `hookSpecificOutput`; TAOM hooks fail open. The `if:` handler field (`"Bash(git commit*)"`) is deferred there because "a mis-scoped `if:` silently disables a gate, so it needs a per-gate proof". This plan's 4c section is exactly that kind of per-hook proof for the prefilter.
- **`.claude/rules/hook-authoring.md`:** mirror a sibling's full convention set (keep each hook's own allow output and exit code); source `_pybin.sh` above the first `"$PYBIN"` use; fail open but never fail silent (`taom_pybin_degraded` stays in every blocking gate, check 5b greps for it); time the slow path; "prove a gate live" (a worktree's hooks do not run in the session, so the live proof is an orchestrator step after merge, see Maintenance notes).
- **`.claude/rules/simplicity-criterion.md`:** the lane's Step 2 (one dispatcher script replacing seven gates) was judged a Reject for now: it adds a new failure-coupling surface (one timeout kill silences seven gates) for a further ~100 ms. Not in this plan.
- **Line endings:** `.gitattributes` pins `*.sh text eol=lf`. Edit with the Edit tool; never `sed -i`.

### Decisions already taken (do not reopen)

- Use the plain substring test (`*git*`; `*dotnet*`; `*dotnet*` or `*build.ps1*`), never a regex over the raw payload.
- Keep `INPUT=$(cat)`. Do not switch to `read -r -d ''` in this plan (the checker saw a small further saving; it changes trailing-newline handling and is not needed to hit the target).
- Do not build the single dispatcher, do not add `if:` fields, do not change any registered `timeout` or anything else in `.claude/settings.json`, and do not edit `_pybin.sh`.
- Each hook keeps its own non-trigger allow output: `block-no-verify.sh`, `validate-push.sh`, `notify-test-results.sh` and `mark-verification-run.sh` exit 0 with no output; the other eight gates print `{}`; `suggest-compact.sh` keeps printing `{}` at its end.
- `validate-push.sh` reads only the first line of a multi-line command (found while planning; see Maintenance notes). Do not fix it here: the parity run expects today's behaviour.

## Commands you will need

Every command is run as `cd /e/repos/wt-plan-013 && <command>`. Never `./build.ps1`. "Long" means pass `timeout: 600000` to the Bash tool.

| Purpose | Command (after the `cd` prefix) | Long | Expected on success |
|---|---|---|---|
| Hook suite | `bash tools/test_hooks.sh` | yes | exit 0 (or exactly the Step 0 baseline failures) |
| Config security audit | `python tools/audit_claude_config.py --no-repo-secrets --min HIGH` | no | exit 0, "No findings at or above the requested severity" |
| Docs | `python tools/lint_docs.py` | no | exit 0; no line naming `hook-authoring.md` or `hooks-catalog.md` |
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | yes | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | yes | see the known baseline below |
| Data | `python tools/validate_moduledata.py` | no | 0 errors (not affected; not required) |

**Known test baseline (measured at `b2e387db`):** 10,239 tests, 10,235 passed, 2 failed, 2 ignored. The 2 failures are `ElkConfigTests.TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and `AnimaliaMountWiringTests.AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; both read the live, unversioned Armory install that another session is editing. They are not caused by this or any plan: do not chase them and do not edit either file. The 2 ignored are deliberate `[Ignore]`s in `WargAttackServiceTests`. This plan changes no C#, so the build and test runs are a no-change regression check only.

## Scope

**In scope** (the only files you may modify, all inside `E:/repos/wt-plan-013/`, on your worktree branch):

- `.claude/hooks/block-no-verify.sh`, `validate-push.sh`, `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `check-doc-config-drift.sh`, `notify-test-results.sh`, `mark-verification-run.sh`, `suggest-compact.sh` (13 files, all under `.claude/hooks/`)
- `tools/test_hooks.sh` (new section 4c only)
- `docs/reference/hooks-catalog.md` (one inserted paragraph after line 13)
- `.claude/rules/hook-authoring.md` (one phrase on line 153)
- `CHANGELOG.md` (one new entry; required in the same commit by `.claude/hooks/check-changelog-changed.sh` and AGENTS.md "Documentation duty")

Plus scratch files under `E:/repos/wt-plan-013-scratch/` (never committed).

**Out of scope** (do NOT touch):

- Anything under `E:/repos/TAOM/` (the main tree), including its copies of the files above.
- `.claude/hooks/_pybin.sh`, `.claude/settings.json`, every other hook, every skill.
- `.claude/rules/harness-facts.md` (another session holds uncommitted edits to it in the main tree).
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`, and anything under `Main/` or `TAOM.Tests/`.
- `plans/README.md`.
- The `validate-push.sh` first-line-only parsing (a separate finding; Maintenance notes).

## Git workflow

- Work in a new worktree off `bannerlord-1.5.x`, never in `E:\repos\TAOM` itself:
  `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-013 -b plan/013-bash-hook-prefilter bannerlord-1.5.x`
  If the branch or directory already exists, STOP and report; do not delete, reuse or reset either.
- One commit. Subject (65 characters): `perf(hooks): v2.0.30 - skip Python in Bash hooks on non-git calls`. The version is `<Version value="v2.0.30" />` in `Main/_Module/SubModule.xml`; re-read it (`cd /e/repos/wt-plan-013 && grep -n "Version value" Main/_Module/SubModule.xml`) before committing and use whatever it says. Subject at most 72 characters, body wrapped at 72, **no AI attribution trailer** (no `Co-Authored-By`, no "Generated with" line). Suggested trailer: `Not-tested: the edited hooks under a live harness (a worktree's hooks do not run in the session; proof owed after merge)`, wrapped to 72.
- Stage explicit paths only, the 17 in-scope paths by name (`cd /e/repos/wt-plan-013 && git add .claude/hooks/block-no-verify.sh ...`), never `git add -A`, `git add .` or `git commit -a`. Commit with `cd /e/repos/wt-plan-013 && git commit -m "<subject>" -m "<body>"`.
- The session's hooks run against the MAIN tree (`CLAUDE_PROJECT_DIR`), not your worktree: the commit gates `cd` there and read the main tree's index. If any hook denies or asks on your commit, STOP and report the message verbatim; never bypass a hook and never edit an out-of-scope file to satisfy one.
- Never push, never open a PR, never merge. **Merge note for the orchestrator:** the main tree's `CHANGELOG.md` has another session's uncommitted edits; this branch adds one entry at the top of the newest date section, so a rebase over that work may need a trivial hand merge of the CHANGELOG hunk.

## Steps

### Step 0: Set up and record the baselines

1. Create the worktree (Git workflow above), then:
   `cd /e/repos/wt-plan-013 && git rev-parse --show-toplevel && mkdir -p /e/repos/wt-plan-013-scratch && echo made` → prints `E:/repos/wt-plan-013` then `made`.
2. Run the drift check from the header → no output.
3. `cd /e/repos/wt-plan-013 && git ls-files --eol .claude/hooks/ tools/test_hooks.sh | grep -v "w/lf"` → no output (every hook checks out LF).
4. (Long) `cd /e/repos/wt-plan-013 && bash tools/test_hooks.sh > /e/repos/wt-plan-013-scratch/step0-hooks.txt 2>&1; echo "rc=$?"`. Expected `rc=0`. If it is not, the `FAIL` lines in that file are pre-existing and become the allowed set for every later step.
5. With the Write tool, create `E:/repos/wt-plan-013-scratch/timing.sh` with exactly this content (it `cd`s itself; no backslash escapes are involved):
   ```bash
   #!/usr/bin/env bash
   # Plan 013 timing: 5-run mean per hook on a non-git payload. Not committed.
   # Usage: bash timing.sh        (floor + 13 hooks)
   #        bash timing.sh big    (also the two PostToolUse hooks on a 1 MB payload)
   cd /e/repos/wt-plan-013 || exit 1
   P='{"tool_name":"Bash","session_id":"taom-plan013-timing","tool_input":{"command":"ls docs","description":"List docs"},"tool_response":{"stdout":"INDEX.md","stderr":""},"hook_event_name":"PreToolUse"}'
   t() { local s e tot=0; for i in 1 2 3 4 5; do s=$(date +%s%N); printf '%s' "$P" | env CLAUDE_PROJECT_DIR=/nonexistent-taom TAOM_PYBIN=C:/Python314/python.exe "$@" >/dev/null 2>&1; e=$(date +%s%N); tot=$((tot+(e-s)/1000000)); done; echo $((tot/5)); }
   echo "floor $(t bash -c 'exit 0')"
   for h in block-no-verify validate-push check-changelog-changed check-claude-files-tracked check-commit-subject-version block-dangerous-git block-broad-git-add check-moduledata-validation check-native-dll-crt check-doc-config-drift notify-test-results mark-verification-run suggest-compact; do echo "$h $(t bash .claude/hooks/$h.sh)"; done
   if [[ "${1:-}" == big ]]; then
     big=$(head -c 1000000 /dev/zero | tr '\0' 'a')
     P="{\"tool_name\":\"Bash\",\"tool_input\":{\"command\":\"ls\"},\"tool_response\":{\"stdout\":\"$big\"}}"
     for h in notify-test-results mark-verification-run; do echo "1MB $h $(t bash .claude/hooks/$h.sh)"; done
   fi
   rm -f /tmp/claude-tool-count-taom-plan013-timing /tmp/claude-last-boundary-taom-plan013-timing
   ```
6. (Long) `bash /e/repos/wt-plan-013-scratch/timing.sh > /e/repos/wt-plan-013-scratch/step0-timing.txt; cat /e/repos/wt-plan-013-scratch/step0-timing.txt`. Expected: numbers of the same order as "Measured at planning" (about 200 to 330 ms per hook).

**Verify**:
- `grep -c "passed, " /e/repos/wt-plan-013-scratch/step0-hooks.txt` → `1` (the summary line exists).
- `grep -c . /e/repos/wt-plan-013-scratch/step0-timing.txt` → `14` (floor plus 13 hooks).

### Step 1 (RED): Add section 4c to `tools/test_hooks.sh`

Edit `E:/repos/wt-plan-013/tools/test_hooks.sh` with the Edit tool. Use as `old_string` these two consecutive lines (they are unique in the file; they are the start of the section 5 banner, `:416-417`):

```text
# ---------------------------------------------------------------------------
# 5. Starved environment: no jq, no python at all.
```

and as `new_string` the block below, then one blank line, then those same two lines unchanged. The block therefore lands after 4b's closing `done` (`:414`) and the blank line (`:415`), and the section 5 banner stays whole below it. Copy the block exactly (the single-quoted `\n` must stay the two characters backslash and `n`). Do not write the text `timeout -k` anywhere in a hook comment: section 3 of this suite reads the first such occurrence in each hook as its inner bound.

```bash
# ---------------------------------------------------------------------------
# 4c. A Bash call that cannot concern a hook starts no Python in it.
#     Every Bash-path hook used to source _pybin.sh (one Python start, the probe) and
#     parse the payload (a second) before it looked at the command: 200 to 330 ms per
#     hook on an `ls`, for 13 hooks on every Bash call. Each now tests the RAW payload
#     for its trigger text first. A counting interpreter pinned through TAOM_PYBIN
#     records every start, so the check is deterministic on any platform. The trigger
#     rows are multi-line on purpose: a newline before `git` arrives as the two
#     characters \n, which is how a token regex over the raw JSON would have skipped a
#     real commit. Hooks are discovered from settings.json, so a new Bash hook that
#     parses before it filters fails here.
# ---------------------------------------------------------------------------
head2 "4c. the Bash hooks start no Python on a payload that cannot concern them"
PF="$SANDBOX/prefilter"
mkdir -p "$PF"
FAKEPY="$PF/fakepy"
printf '#!/bin/sh\necho started >> "%s/starts"\nprintf taompy\n' "$PF" > "$FAKEPY"
chmod +x "$FAKEPY" 2>/dev/null
pf_payload() {  # $1 event, $2 command already JSON-escaped; printf %s keeps its backslashes
    printf '{"tool_name":"Bash","session_id":"taom-prefilter-test","hook_event_name":"%s","tool_input":{"command":"%s","description":"prefilter probe"},"tool_response":{"stdout":"ok","stderr":""}}' "$1" "$2"
}
pf_starts() {   # $1 hook file name, $2 payload; prints how many interpreter starts it caused
    rm -f "$PF/starts"
    printf '%s' "$2" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" TAOM_PYBIN="$FAKEPY" \
        bash ".claude/hooks/$1" >/dev/null 2>&1
    if [[ -f "$PF/starts" ]]; then grep -c . "$PF/starts"; else echo 0; fi
}
PF_PIN=$(env TAOM_PYBIN="$FAKEPY" bash -c 'source .claude/hooks/_pybin.sh; printf "%s" "$PYBIN"' 2>/dev/null)
PF_ROWS=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
rows = set()
for ev in ('PreToolUse', 'PostToolUse'):
    for g in d.get('hooks', {}).get(ev, []):
        m = g.get('matcher', '')
        if m == '' or 'Bash' in m.split('|'):
            for h in g.get('hooks', []):
                rows.add(ev + ':' + h['command'].rsplit('/', 1)[-1])
print(' '.join(sorted(rows)))
PY
)
if [[ "$PF_PIN" != "$FAKEPY" ]]; then
    bad "4c premise: _pybin.sh did not accept the counting interpreter (got '$PF_PIN'), so nothing here is proven"
elif [[ -z "$PF_ROWS" ]]; then
    bad "4c discovery found no Bash-matched hooks in settings.json; the check is broken"
else
    for row in $PF_ROWS; do
        ev="${row%%:*}"; name="${row#*:}"
        [[ -f ".claude/hooks/$name" ]] || { bad "4c: $name is registered but missing from .claude/hooks/"; continue; }
        n=$(pf_starts "$name" "$(pf_payload "$ev" 'ls docs')")
        if [[ "$n" == 0 ]]; then
            ok "$name [$ev] no interpreter on a non-trigger payload"
        else
            bad "$name [$ev] started an interpreter $n time(s) on a payload that cannot concern it (ls docs): test the raw payload before sourcing _pybin.sh"
        fi
        if [[ "$ev" == PostToolUse ]]; then
            triggers=('cd /x\ndotnet test TAOM.Tests')
            [[ "$name" == mark-verification-run.sh ]] && triggers+=('cd /x\npwsh ./build.ps1 -RunTests')
        else
            triggers=('cd /x\ngit commit -m x')
        fi
        for cmd in "${triggers[@]}"; do
            n=$(pf_starts "$name" "$(pf_payload "$ev" "$cmd")")
            if [[ "$n" -ge 1 ]]; then
                ok "$name [$ev] still parses a multi-line trigger payload [$cmd]"
            else
                bad "$name [$ev] skipped its parse on a trigger payload [$cmd]: the prefilter is narrower than the hook's own trigger"
            fi
        done
    done
    rm -f /tmp/claude-tool-count-taom-prefilter-test /tmp/claude-last-boundary-taom-prefilter-test
fi
```

**Verify** (RED). Run (Long) `cd /e/repos/wt-plan-013 && bash tools/test_hooks.sh --verbose > /e/repos/wt-plan-013-scratch/red.txt 2>&1; echo "rc=$?"`, then:
- `rc=1`.
- `grep -c "FAIL.*started an interpreter" /e/repos/wt-plan-013-scratch/red.txt` → `13` (one inline FAIL per hook: all 13 still parse before filtering). Do not drop the `FAIL.*` part: without it the Summary's repeat of each message makes the count 26.
- `grep -c "still parses a multi-line trigger payload" /e/repos/wt-plan-013-scratch/red.txt` → `14` (11 PreToolUse rows plus `notify-test-results` once and `mark-verification-run` twice).
- `grep -c "4c premise\|4c discovery\|4c: " /e/repos/wt-plan-013-scratch/red.txt` → `0`.
- `grep "FAIL" /e/repos/wt-plan-013-scratch/red.txt | grep -v "started an interpreter"` prints only lines that also appear in `grep "FAIL" /e/repos/wt-plan-013-scratch/step0-hooks.txt` (none, when Step 0 had `rc=0`).

### Step 2 (GREEN): Prefilter the eight `git commit` gates

In each of these files under `E:/repos/wt-plan-013/.claude/hooks/`: `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, `check-commit-subject-version.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `check-doc-config-drift.sh`, replace the six-line block at the lines in the "Current state" table:

```bash
set -uo pipefail

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)
```

with (keep that script's own `# Resolve a safe Python ...` comment text unchanged; in `check-commit-subject-version.sh` it has a comma instead of the dash):

```bash
set -uo pipefail

INPUT=$(cat)

# Prefilter: every decision below needs the text `git` in the command, and JSON never
# escapes an ASCII letter, so a raw payload without it cannot concern this gate. Exiting
# here skips the _pybin.sh probe and the parse (two Python starts) on most Bash calls.
# Match the raw text, never a token regex: a newline before `git` arrives as \n.
# tools/test_hooks.sh 4c checks both directions.
[[ "$INPUT" == *git* ]] || { echo '{}'; exit 0; }

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
```

Nothing else in these files changes: the `taom_pybin_degraded` line, the parse and every later line stay as they are.

**Verify** (every command with the `cd /e/repos/wt-plan-013 && ` prefix):
- `grep -c 'INPUT=$(cat)' .claude/hooks/<each of the 8>` → `1` in each file.
- For each of the 8: `grep -n 'INPUT=$(cat)\|== \*git\* ]]\|_pybin.sh"$' .claude/hooks/<file>` lists the three lines in that order (INPUT, then the prefilter, then the `source`).
- (Long) `bash tools/test_hooks.sh --verbose > /e/repos/wt-plan-013-scratch/step2.txt 2>&1; grep -c "FAIL.*started an interpreter" /e/repos/wt-plan-013-scratch/step2.txt` → `5`.

### Step 3 (GREEN): Prefilter `block-no-verify.sh` and `validate-push.sh`

These two allow with no output, so their prefilter exits with a bare `exit 0`.

`E:/repos/wt-plan-013/.claude/hooks/block-no-verify.sh`: delete lines 3-4 (`# Resolve a safe Python ...` and the `source` line) so line 2 (blank) is followed by `# PreToolUse(Bash): refuse any git command carrying --no-verify.`. Then replace the single line `INPUT=$(cat)` (line 27 before the deletion) with:

```bash
INPUT=$(cat)

# Prefilter: the check below needs a `git` token, and JSON never escapes an ASCII letter,
# so a raw payload without the text `git` cannot concern this gate. Exiting here skips the
# _pybin.sh probe and the parse (two Python starts) on most Bash calls. Match the raw text,
# never a token regex: a newline before `git` arrives as \n. tools/test_hooks.sh 4c checks it.
[[ "$INPUT" == *git* ]] || exit 0

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
```

`E:/repos/wt-plan-013/.claude/hooks/validate-push.sh`: insert, directly above line 6 (`# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.`):

```bash
INPUT=$(cat)

# Prefilter: a push is only judged after a `git` token (below), and JSON never escapes an
# ASCII letter, so a raw payload without the text `git` cannot concern this gate. Exiting
# here skips the _pybin.sh probe and the parse on most Bash calls. Match the raw text, never
# a token regex: a newline before `git` arrives as \n. tools/test_hooks.sh 4c checks it.
[[ "$INPUT" == *git* ]] || exit 0

```

and delete the old `INPUT=$(cat)` line (old line 13) together with the blank line above it (old line 12). The comment block and the `source` line (old 6-11) stay exactly as they are; they remain above the first `"$PYBIN"` use.

**Verify** (with the `cd /e/repos/wt-plan-013 && ` prefix):
- `grep -c 'INPUT=$(cat)' .claude/hooks/block-no-verify.sh .claude/hooks/validate-push.sh` → `1` for each.
- `head -12 .claude/hooks/block-no-verify.sh | grep -c _pybin` → `0` (the source moved down).
- (Long) `bash tools/test_hooks.sh --verbose > /e/repos/wt-plan-013-scratch/step3.txt 2>&1; grep -c "FAIL.*started an interpreter" /e/repos/wt-plan-013-scratch/step3.txt` → `3`.

### Step 4 (GREEN): Prefilter the two PostToolUse hooks

`E:/repos/wt-plan-013/.claude/hooks/notify-test-results.sh`: replace lines 3-6

```bash
# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
# PostToolUse hook: summarize dotnet test results prominently
INPUT=$(cat)
```

with

```bash
# PostToolUse hook: summarize dotnet test results prominently
INPUT=$(cat)

# Prefilter: the summary below needs `dotnet test` in the command, and JSON never escapes
# an ASCII letter, so a raw payload without the text `dotnet` cannot concern this hook.
# Exiting here skips the _pybin.sh probe and the parse on most Bash calls (test_hooks.sh 4c).
[[ "$INPUT" == *dotnet* ]] || exit 0

# Resolve a safe Python (never a Microsoft Store alias — those hang forever).
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
```

`E:/repos/wt-plan-013/.claude/hooks/mark-verification-run.sh`: replace lines 11-14

```bash
# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"

INPUT=$(cat)
```

with

```bash
INPUT=$(cat)

# Prefilter: a mark needs `dotnet` or `build.ps1` in the command (the segment loop below),
# and JSON never escapes an ASCII letter, so a raw payload with neither cannot concern this
# hook. Exiting here skips the _pybin.sh probe and the parse on most Bash calls
# (tools/test_hooks.sh 4c).
[[ "$INPUT" == *dotnet* || "$INPUT" == *build.ps1* ]] || exit 0

# Resolve a safe Python interpreter. Never a Microsoft Store alias: those hang forever.
source "$(dirname "${BASH_SOURCE[0]}")/_pybin.sh"
```

**Verify**: (Long) `cd /e/repos/wt-plan-013 && bash tools/test_hooks.sh --verbose > /e/repos/wt-plan-013-scratch/step4.txt 2>&1; grep -c "FAIL.*started an interpreter" /e/repos/wt-plan-013-scratch/step4.txt` → `1` (only `suggest-compact.sh` left).

### Step 5 (GREEN): Prefilter the Bash branch of `suggest-compact.sh`

In `E:/repos/wt-plan-013/.claude/hooks/suggest-compact.sh`, replace line 77, `if [ "$TOOL_NAME" = "Bash" ]; then`, with:

```bash
# The raw-text test skips the probe and the parse when no boundary signal below could
# match: each one needs `git`, `dotnet` or `build.ps1` in the command (test_hooks.sh 4c).
if [ "$TOOL_NAME" = "Bash" ] && [[ "$INPUT" == *git* || "$INPUT" == *dotnet* || "$INPUT" == *build.ps1* ]]; then
```

The counter logic above it and the boundary logic below it do not change.

**Verify** (Long for the first command):
- `cd /e/repos/wt-plan-013 && bash tools/test_hooks.sh --verbose > /e/repos/wt-plan-013-scratch/green.txt 2>&1; echo "rc=$?"` → `rc=0` (or only the Step 0 failures).
- `grep -c "no interpreter on a non-trigger payload" /e/repos/wt-plan-013-scratch/green.txt` → `13`.
- `grep -c "still parses a multi-line trigger payload" /e/repos/wt-plan-013-scratch/green.txt` → `14`.
- `grep -c "started an interpreter\|skipped its parse\|4c premise\|4c discovery" /e/repos/wt-plan-013-scratch/green.txt` → `0`.

### Step 6: Prove no decision changed (old versus new parity)

With the Write tool, create `E:/repos/wt-plan-013-scratch/parity.sh` with exactly this content. "Old" is the worktree's committed `HEAD` (the base you branched from; this plan's commit does not exist yet), "new" is the edited working tree.

```bash
#!/usr/bin/env bash
# Plan 013 parity: the base commit's hooks (worktree HEAD) and the edited hooks must answer
# every payload identically (stdout and exit code). Not committed.
set -u
WT=/e/repos/wt-plan-013
cd "$WT" || exit 1
OLD=$(mktemp -d)
POSTDIR=$(mktemp -d)
mkdir -p "$OLD/.claude/hooks"
HOOKS="block-no-verify validate-push check-changelog-changed check-claude-files-tracked check-commit-subject-version block-dangerous-git block-broad-git-add check-moduledata-validation check-native-dll-crt check-doc-config-drift notify-test-results mark-verification-run"
git -C "$WT" show HEAD:.claude/hooks/_pybin.sh > "$OLD/.claude/hooks/_pybin.sh"
for h in $HOOKS; do git -C "$WT" show "HEAD:.claude/hooks/$h.sh" > "$OLD/.claude/hooks/$h.sh"; done
CMDS=(
  'ls docs'
  'echo \"git reset --hard\"'
  'cd /e/repos/TAOM\ngit reset --hard'
  'cd /e/repos/TAOM\n\tgit push --force origin bannerlord-1.4.5'
  'git push --force origin bannerlord-1.4.5'
  'git push origin bannerlord-1.4.5'
  'git commit --no-verify -m \"docs: v2.0.30 - x\"'
  'git add -A'
  'cd /e/repos/TAOM\ngit commit -m \"no label here\"'
  'git status --short'
  'dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId='
  'pwsh ./build.ps1 -RunTests'
  'grep -rn \"dotnet test\" docs/'
)
FAIL=0; N=0
for h in $HOOKS; do
  case "$h" in
    notify-test-results|mark-verification-run) ev=PostToolUse; dir="$POSTDIR" ;;
    *) ev=PreToolUse; dir="$WT" ;;
  esac
  for c in "${CMDS[@]}"; do
    p='{"tool_name":"Bash","tool_input":{"command":"'"$c"'"},"tool_response":{"stdout":"Passed: 3","stderr":""},"hook_event_name":"'"$ev"'"}'
    o_out=$(printf '%s' "$p" | env CLAUDE_PROJECT_DIR="$dir" timeout -k 2 60 bash "$OLD/.claude/hooks/$h.sh" 2>/dev/null); o_rc=$?
    n_out=$(printf '%s' "$p" | env CLAUDE_PROJECT_DIR="$dir" timeout -k 2 60 bash "$WT/.claude/hooks/$h.sh" 2>/dev/null); n_rc=$?
    N=$((N+1))
    if [[ "$o_out" != "$n_out" || $o_rc -ne $n_rc ]]; then
      FAIL=$((FAIL+1))
      echo "DIFF $h [$c]: old rc=$o_rc out=${o_out:0:120} | new rc=$n_rc out=${n_out:0:120}"
    fi
  done
done
rm -rf "$OLD" "$POSTDIR"
echo "parity: $N cases, $FAIL differences"
[[ $FAIL -eq 0 ]]
```

Run it **before staging anything** (the commit gates read `git diff --cached` in the worktree, and both runs must see the same empty index), as a Long call: `bash /e/repos/wt-plan-013-scratch/parity.sh; echo "rc=$?"`.

`suggest-compact.sh` is not in the parity set (its counter changes between the two runs); section 4c covers it.

**Verify**: last two lines `parity: 156 cases, 0 differences` and `rc=0`. For orientation, the decisions both versions must agree on include: `block-no-verify` exits 2 on the `--no-verify` commit; `validate-push` exits 2 on the single-line force push and exits 0 on the multi-line one (today's first-line-only behaviour, not this plan's to fix); `block-dangerous-git` asks on `cd ...\ngit reset --hard` and allows the quoted `echo`; `block-broad-git-add` asks on `git add -A`; `check-commit-subject-version` denies `"no label here"`.

### Step 7: Re-time

(Long) `bash /e/repos/wt-plan-013-scratch/timing.sh big > /e/repos/wt-plan-013-scratch/step7-timing.txt; cat /e/repos/wt-plan-013-scratch/step7-timing.txt` → 16 lines (floor, 13 hooks, two `1MB` lines).

**Verify**:
- This prints nothing (the limit for each of the 12 hooks other than `suggest-compact` is the larger of 80 ms and the floor plus 30 ms; each `1MB` line must be under 150 ms):
  `awk 'NR==1{lim=($2+30>80)?$2+30:80; next} $1=="1MB"{if($3>=150) print "SLOW", $0; next} $1!="suggest-compact"{if($2>=lim) print "SLOW", $0}' /e/repos/wt-plan-013-scratch/step7-timing.txt`
- This prints `lower` (`suggest-compact` keeps its counter work; only the Python part goes):
  `a=$(awk '$1=="suggest-compact"{print $2}' /e/repos/wt-plan-013-scratch/step0-timing.txt); b=$(awk '$1=="suggest-compact"{print $2}' /e/repos/wt-plan-013-scratch/step7-timing.txt); [ "$b" -lt "$a" ] && echo lower`

Timings depend on machine load. If either check fails, run the timing script once more to `step7-timing-2.txt` and apply both checks to that file; it is a STOP only if the rerun fails too.

### Step 8: Docs and CHANGELOG

1. `E:/repos/wt-plan-013/docs/reference/hooks-catalog.md`: directly after line 13 (the line ending ``block-dangerous-git.sh` is the model.``, the only line in the file containing `is the model.`), insert these five lines. The first is a lone `>` so the new text is its own blockquote paragraph; the existing lone `>` (old line 14) then separates it from the paragraph that follows:
   ```text
   >
   > In a Bash-matched hook, read `INPUT=$(cat)` first and exit with the hook's allow output when
   > the raw payload lacks its trigger text (`*git*`, or `*dotnet*` and `*build.ps1*`), and only
   > then source `_pybin.sh`: the probe and the parse are two Python starts, about 200 ms, on every
   > Bash call. `tools/test_hooks.sh` 4c fails a Bash hook that starts Python on a payload without it.
   ```
2. `E:/repos/wt-plan-013/.claude/rules/hook-authoring.md` line 153: replace the words `at the TOP, above the` with `after any raw-payload prefilter and above the`. Change nothing else in that file (it is 100 bytes under its size cap at `b2e387db`; this edit uses 24 of them).
3. `E:/repos/wt-plan-013/CHANGELOG.md` (your worktree's copy, never the main tree's): add a new entry directly under the newest `## YYYY-MM-DD` heading if that date is today, otherwise add a new `## <today>` heading above the newest one and put the entry under it. Fill the numbers from `step0-timing.txt` and `step7-timing.txt` (the lowest and highest hook figure in each, `suggest-compact` included); do not reuse the planning numbers:
   ```text
   ### perf(hooks): v2.0.30 - skip Python in Bash hooks on non-git calls

   Every Bash call ran 13 hook scripts, and each one started Python twice (the `_pybin.sh`
   probe, then a JSON parse) before it looked at the command: <before range> ms per hook on
   an `ls`. Each Bash hook now tests the raw payload for its trigger text first (`git` for
   the ten PreToolUse gates, `dotnet` or `build.ps1` for the two PostToolUse hooks, any of
   the three for `suggest-compact.sh`) and allows without starting Python when it is
   absent: <after range> ms per hook. JSON never escapes an ASCII letter, so the raw test
   is a superset of every hook's own trigger; an old-versus-new run over 156 payload cases
   found no changed decision. A token regex was rejected: a newline before `git` arrives
   as `\n` and would have skipped a multi-line commit. `tools/test_hooks.sh` 4c counts
   interpreter starts with a pinned fake interpreter, in both directions.
   ```

**Verify** (with the `cd /e/repos/wt-plan-013 && ` prefix):
- `python tools/lint_docs.py; echo "rc=$?"` → `rc=0`, and `python tools/lint_docs.py 2>&1 | grep -c "hook-authoring.md\|hooks-catalog.md"` → `0`.
- `grep -c "after any raw-payload prefilter" .claude/rules/hook-authoring.md` → `1`; `grep -c "at the TOP" .claude/rules/hook-authoring.md` → `0`.
- `grep -c "4c fails a Bash hook that starts Python" docs/reference/hooks-catalog.md` → `1`.
- No added line in either prose file carries an em or en dash: `git diff -U0 docs/reference/hooks-catalog.md CHANGELOG.md | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l.startswith('+') and (chr(0x2013) in l or chr(0x2014) in l)))"` → `0`.

### Step 9: Final verification and commit

**Verify**, every command with the `cd /e/repos/wt-plan-013 && ` prefix (Long for the first, fourth and fifth):
- `bash tools/test_hooks.sh > /e/repos/wt-plan-013-scratch/final.txt 2>&1; echo "rc=$?"` → `rc=0` (or exactly the Step 0 failures); the passed count in `final.txt`'s `N passed, M failed` line is the one in `step0-hooks.txt` plus 27.
- `python tools/audit_claude_config.py --no-repo-secrets --min HIGH; echo "rc=$?"` → `rc=0`, "No findings at or above the requested severity".
- `git ls-files --eol .claude/hooks/ tools/test_hooks.sh | grep -v "w/lf"` → no output.
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` → exit 0, 0 errors.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` → only the two known Armory tests may fail; 2 ignored.
- `git status --porcelain` → exactly the 17 in-scope paths, all ` M`. If anything else appears, STOP and report it rather than deleting it.
- Main tree untouched and its index empty (the commit gates read that index): `git -C /e/repos/TAOM status --porcelain -- .claude/hooks tools/test_hooks.sh docs/reference/hooks-catalog.md .claude/rules/hook-authoring.md` → no output, and `git -C /e/repos/TAOM diff --cached --name-only` → no output. If the second prints anything, STOP: another session has staged work there and the gates would judge it, not yours.

Then stage the 17 paths by name and commit as in "Git workflow". Afterwards (with the prefix): `git log -1 --format=%B | grep -ci "co-authored-by\|generated with"` → `0`, and `git diff --name-only HEAD~1..HEAD` lists only in-scope paths.

## Test plan

- **New:** section 4c in `tools/test_hooks.sh`, 27 assertions discovered from `settings.json`: for each of the 13 Bash-path hooks, a non-trigger payload (`ls docs`) causes zero interpreter starts; for each PreToolUse row a multi-line `cd /x\ngit commit -m x` payload still causes at least one; for each PostToolUse row a multi-line `cd /x\ndotnet test TAOM.Tests` does, and for `mark-verification-run.sh` also `cd /x\npwsh ./build.ps1 -RunTests`. Plus a premise assertion that `_pybin.sh` accepts the counting interpreter, so a broken fake can never read as a pass.
- **Structural pattern:** section 4 of the same file (sandbox, `timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$hookfile"`), and section 4b (settings.json discovery through `$HPY`).
- **Unchanged sections that must stay green:** 4 (contract on five payload shapes), 4b (commit payload inside 80% of the registration), 5 and 5b (fail open but say so; its payload is a `git push`, so every gate still reaches `taom_pybin_degraded`), 5c2 (multi-line ask), 6 (subject cases), 7b.
- **Parity (not committed):** Step 6, 12 hooks by 13 commands, old versus new stdout and exit code.
- **Not tested here (commit trailer):** the edited hooks under a live Claude Code harness; the session runs the main tree's hooks, not the worktree's.
- **Known behaviour change, intended:** on a machine with no usable Python, a non-git Bash call no longer prints the `taom_pybin_degraded` warning to stderr (exit-0 stderr never reaches Claude anyway, per `harness-facts.md`). Every git call still does.

## Done criteria

ALL must hold, every command with the `cd /e/repos/wt-plan-013 && ` prefix:

- [ ] `bash tools/test_hooks.sh` exits 0 (or shows exactly the Step 0 failures), and `bash tools/test_hooks.sh --verbose 2>&1 | grep -c "no interpreter on a non-trigger payload"` → `13` and `... | grep -c "still parses a multi-line trigger payload"` → `14`
- [ ] `grep -l '"$INPUT" == \*git\*' .claude/hooks/*.sh | wc -l` → `11` (ten gates plus `suggest-compact.sh`; no hook matched this at `b2e387db`) and `grep -l '"$INPUT" == \*dotnet\*' .claude/hooks/*.sh | wc -l` → `3` (`notify-test-results.sh`, `mark-verification-run.sh`, `suggest-compact.sh`)
- [ ] Parity script: `parity: 156 cases, 0 differences`
- [ ] Both Step 7 checks pass (on the first run or the one allowed rerun), and the before and after numbers are in the CHANGELOG entry
- [ ] `python tools/audit_claude_config.py --no-repo-secrets --min HIGH` exits 0
- [ ] `python tools/lint_docs.py` exits 0 with no line naming `hook-authoring.md` or `hooks-catalog.md`
- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0; `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails only the two named Armory tests
- [ ] `git diff --name-only HEAD~1..HEAD` lists only the 17 in-scope paths; `git status --porcelain` is empty; the commit subject is at most 72 characters and carries no AI attribution
- [ ] `git diff HEAD~1..HEAD -- .claude/settings.json .claude/hooks/_pybin.sh` prints nothing
- [ ] `git -C /e/repos/TAOM status --porcelain -- .claude/hooks tools/test_hooks.sh docs/reference/hooks-catalog.md .claude/rules/hook-authoring.md` prints nothing (the main tree was never edited)

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything, or any excerpt in "Current state" does not match the file (for example the six-line preamble is not at the listed lines, or a hook no longer sources `_pybin.sh` before its parse).
- `git rev-parse --show-toplevel` in Step 0 does not print `E:/repos/wt-plan-013`, or you find you edited or ran a hook under `E:/repos/TAOM/`. Report exactly what was touched; do not try to revert main-tree files yourself.
- Step 1's RED run does not show exactly 13 `FAIL.*started an interpreter` lines and 14 "still parses" lines, or shows a `4c premise` or `4c discovery` failure. The premise failing means the counting interpreter is not accepted on this platform; do not weaken the test to get past it.
- Any "skipped its parse on a trigger payload" line appears at any point: a prefilter is narrower than its hook's trigger. Do not widen the test; fix the prefilter to the plain substring form given here, and if the form given here is what fails, STOP.
- The parity script reports any difference. Report the `DIFF` lines verbatim; do not edit a hook's decision logic to make them match.
- Some Bash-matched hook in `settings.json` is not one of the 13 listed here (a registration was added since planning), or a hook's trigger turns out to need text other than `git`, `dotnet` or `build.ps1` in the command.
- Section 5 starts reporting "failed open SILENTLY" for a blocking gate (its payload must still reach `taom_pybin_degraded`).
- A Step 7 check fails on both the first run and the rerun. Report all three timing files; do not start optimising further (no `read -d ''`, no dispatcher).
- `tools/lint_docs.py` reports `hook-authoring.md` over its size cap. Do not trim other text in that file.
- The change seems to need `_pybin.sh`, `settings.json`, `harness-facts.md` or any file outside the in-scope list.
- `git worktree add` fails because the branch or directory already exists.
- The main tree's index is not empty before the commit, or any hook denies or asks on your commit. Report the message verbatim; never bypass a hook.
- A step's verification fails twice after a reasonable fix attempt.

## Maintenance notes

- **Live proof owed after merge (orchestrator; `hook-authoring.md` "Prove a gate live"):** in a main-tree session, run one Bash call whose command is two lines, `cd /e/repos/TAOM` then `git commit --dry-run -m "no label here"`, and confirm the harness refuses it with the `check-commit-subject-version` deny (the command never runs). Then run a plain `ls` and confirm it completes with no hook message. That shows the prefilter passes multi-line git and skips the rest under the real harness.
- **The premise is a serializer property:** the superset argument rests on the harness never writing an ASCII letter as a `\uXXXX` escape. JSON permits that (`\u0067` is `g`), and the current harness does not do it. If a future harness ever did, a prefilter could skip a real `git` command; the live proof above, repeated after a Claude Code upgrade that changes hook payloads, is the check.
- **For the next hook author:** a new Bash-matched hook must test the raw payload for its trigger text before sourcing `_pybin.sh`; section 4c discovers every Bash-matched registration and fails one that starts Python on `ls docs`. A hook whose trigger is not `git`, `dotnet` or `build.ps1` needs its own substring and its own positive row in 4c.
- **What a reviewer should probe:** (1) each prefilter's substring against that hook's own trigger (the superset argument in "Current state"); (2) the four hooks that allow silently still exit with no output and the eight others still print `{}`; (3) `validate-push.sh` still sources `_pybin.sh` above its first `"$PYBIN"` use; (4) no hook comment gained the text `timeout -k` (section 3 would misread it); (5) the 4c payload contains no trigger text outside `command`.
- **Plan 011 overlap:** `plans/011-stop-reminders-and-trunk-guard.md` edits `validate-push.sh` (protected list and comments), `mark-verification-run.sh` (line 2), `tools/test_hooks.sh` (sections 7a and 7c), `hooks-catalog.md` and `hook-authoring.md` (one table row, which also spends part of that file's byte budget). Neither plan's change depends on the other's, but each one's line numbers go stale when the other lands.
- **Found while planning, not fixed here (report to the orchestrator as its own finding):** `validate-push.sh:48` tokenises with `read -r -a TOKENS <<< "$CLEAN"`, which reads only the first line, so a two-line command `cd /e/repos/TAOM` then `git push --force origin bannerlord-1.4.5` returns rc=0 with no output (verified at planning against the main tree's hook, which is identical to `b2e387db`), while the one-line form is blocked with exit 2. The force-push block is blind to any multi-line Bash call. Fixing it belongs in its own change with its own test row in `tools/test_hooks.sh`.
- **Deferred on purpose:** the lane's Step 2 (one dispatcher for the seven fast gates) and the `if:` handler field both stay deferred (`simplicity-criterion.md`; `harness-facts.md` requires a per-gate proof for `if:`). `INPUT=$(cat)` to `read -r -d ''` is a further small saving, not taken.
