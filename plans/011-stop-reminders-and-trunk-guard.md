# Plan 011: Make the four Stop reminders reach Claude and guard the live trunk against force pushes

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. When done, update the status row for this plan
> in `plans/README.md` (if the orchestrator has not added a row for 011 yet,
> leave the file alone and say so in your report), unless a reviewer
> dispatched you and told you they maintain the index.
>
> **Where you work**: every Read, Edit and Write path in this plan is relative to the worktree,
> so the absolute path is always `E:/repos/wt-011-stop-reminders/<path>` (for example
> `E:/repos/wt-011-stop-reminders/tools/test_hooks.sh`). Your default cwd is the main tree
> `E:\repos\TAOM`, where the live hooks run and another session has uncommitted edits: never
> edit or write any file under `E:/repos/TAOM` (see STOP conditions). Run every command with
> the **Bash tool** (the commands use `tail`, `grep` and pipes, which PowerShell lacks), prefixed
> with `cd E:/repos/wt-011-stop-reminders && ` unless it uses `git -C`, and pass
> `timeout: 600000` for `bash tools/test_hooks.sh` and every `dotnet` command.
>
> **Drift check (run first, from the main tree `E:\repos\TAOM`)**:
> `git diff --stat b2e387db..HEAD -- .claude/hooks/check-verification-evidence.sh .claude/hooks/check-deep-review.sh .claude/hooks/check-version-tagged.sh .claude/hooks/check-changelog-updated.sh .claude/hooks/validate-push.sh .claude/hooks/mark-verification-run.sh .claude/settings.json tools/test_hooks.sh docs/reference/hooks-catalog.md .claude/rules/harness-facts.md .claude/rules/hook-authoring.md CLAUDE.md`
> Expected: no output (verified empty at `4b5662b2` while planning). If any
> in-scope file changed since this plan was written, compare the "Current
> state" excerpts against the live code before proceeding; on a mismatch,
> treat it as a STOP condition. `CHANGELOG.md` is left out of the drift check
> on purpose: every commit touches it. `plans/README.md` is left out too: the
> orchestrator edits it while plans are written, and you only touch its 011 row.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: MED (changes what happens at the end of every Claude turn in Mike's sessions; see "Maintenance notes")
- **Depends on**: none
- **Category**: dx
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)
- **Needs before dispatch**: Step 0, done by Mike or by the orchestrator on Mike's explicit
  request, never by the executor. `config-protection.sh` (`b2e387db:.claude/hooks/config-protection.sh:39-67`)
  refuses the Edit and Write tools on any file named `settings.json` with exit 2 unless the
  user-approved override file `/tmp/claude-config-override-${CLAUDE_SESSION_ID}` exists for the
  calling session. An executor subagent has no way to receive Mike's OK, so the two
  `settings.json` insertions are made in the worktree before dispatch, and the executor only
  verifies them (Step 1) and commits them (Step 13). The executor never edits `settings.json`,
  never creates an override file, and never rewrites the file through a shell command.

## Why this matters

Claude Code sends a Stop hook's stderr (on exit 0) and its plain stdout to the debug log only;
Claude never sees either. TAOM's four Stop hooks (build before "done", `/deep-review` before
commit, tag the release, update CHANGELOG) print their reminder with `echo ... >&2` and `exit 0`,
so not one of them has ever reached Claude, while `docs/reference/hooks-catalog.md`,
`block-no-verify.sh` and `docs/reference/release-process.md` all present them as the working
backstops. Three of them also write a mute marker when they fire, so the single reminder per
streak is spent on the debug log. Separately, `validate-push.sh` hard-blocks a force push only to
`master`, `main` and `bannerlord-1.4.5`, while the live trunk is `bannerlord-1.5.x` (the `v2.0.29`
and `v2.0.30` release tags are on it and on neither other branch), the hook is registered only
for the Bash tool (a push through the PowerShell tool is never checked), and GitHub protects no
branch at all. After this plan, each reminder arrives once per streak as a Stop block that Claude
answers, a force push to any `bannerlord-*` branch is refused from either shell tool, and Mike
has the exact steps to add the server-side ruleset.

## Current state

All excerpts below were read at `b2e387db` with `git show b2e387db:<path>`. None of the in-scope
files differs between `b2e387db` and `4b5662b2` (the branch tip while planning). In the main tree
`E:\repos\TAOM`, `CLAUDE.md`, `.claude/rules/harness-facts.md` and `CHANGELOG.md` carry another
live session's UNCOMMITTED edits (a model line in each of the first two, entries in the third).
You work in a separate worktree, so you never see or touch those hunks.

### The vendor contract (hooks reference, fetched 2026-09-23 from `https://code.claude.com/docs/en/hooks.md`)

Quoted verbatim:

- "Stderr from a hook that exits 0 goes to the debug log only, never the transcript, and Claude
  never sees it."
- "For most events, Claude Code writes stdout to the debug log and doesn't show it in the
  transcript. The exceptions are `UserPromptSubmit`, `UserPromptExpansion`, `SessionStart`, and
  `PostModelSwitch`"
- Stop decision control table: "`decision` | `"block"` prevents Claude from stopping. Omit to
  allow Claude to stop" and "`reason` | Required when `decision` is `"block"`. Tells Claude why it
  should continue". Example: `{"decision": "block", "reason": "Must be provided when Claude is blocked from stopping"}`
- Stop input: "The `stop_hook_active` field is `true` when Claude Code is already continuing as a
  result of a stop hook. Check this value or process the transcript to avoid blocking on a
  condition that will never resolve. Claude Code overrides the hook and ends the turn after 8
  consecutive blocks."
- JSON output table: "`systemMessage` | Warning message shown to the user." (so `systemMessage`
  does NOT reach Claude; do not use it).
- The docs also offer `hookSpecificOutput.additionalContext` for Stop ("non-error feedback ...
  shown in the transcript as hook feedback rather than a hook error"). This plan does NOT use it:
  the page gives no minimum version, the installed CLI is `2.1.241` (`claude --version`), and
  `decision: "block"` is the long-standing form. See "Maintenance notes".
- PowerShell tool input (section "PowerShell"): "The fields match the Bash tool, with the command
  string in `command`", and "Match `Bash|PowerShell` in hooks that inspect shell commands ... A
  hook that matches only `Bash` never fires there."

The repo records the same visibility fact at `.claude/rules/harness-facts.md:60` ("Stderr from a
hook that exits 0 never reaches Claude") and `docs/reference/hooks-catalog.md:53-56`.

### The four Stop hooks (registered at `.claude/settings.json:204-229`, each `"timeout": 10`)

`.claude/settings.json:204-229`:

```json
    "Stop": [
      {
        "matcher": "",
        "hooks": [
          { "type": "command", "command": ".claude/hooks/check-changelog-updated.sh", "timeout": 10 },
          { "type": "command", "command": ".claude/hooks/check-deep-review.sh", "timeout": 10 },
          { "type": "command", "command": ".claude/hooks/check-verification-evidence.sh", "timeout": 10 },
          { "type": "command", "command": ".claude/hooks/check-version-tagged.sh", "timeout": 10 }
        ]
      }
    ],
```

(shown compacted; in the file each hook object spans five lines). None of the four reads stdin.
Three of them never `cd` and rely on the harness cwd; `check-version-tagged.sh` does
`cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"`.

`.claude/hooks/check-verification-evidence.sh` (56 lines). Header lines 6-7:
`# Mirrors check-deep-review.sh conventions: reads git state (NOT stdin), emits a` /
`# soft reminder to stderr, always exits 0 (non-blocking — never traps a Stop).` Body:

```bash
21: MARKER=".claude/logs/.verification-ran"
22: REMINDED=".claude/logs/.verification-reminded"
...
44: if [[ $NEEDS_REMINDER -eq 1 ]]; then
45:   if [[ ! -f "$REMINDED" ]]; then
46:     echo "REMINDER: C# source changed but no build/test has run since your last edit. Per .claude/rules/evidence-over-claims.md (verification before \"done\"), run ./build.ps1 -RunTests (or dotnet build Main/TAOM.csproj + dotnet test TAOM.Tests) and read the output before claiming the work complete. A subagent's self-report does not count as verification." >&2
47:     mkdir -p .claude/logs 2>/dev/null
48:     touch "$REMINDED" 2>/dev/null || true
49:   fi
50: else
51:   # Nothing left to verify (built since the edit, or edits reverted) — re-arm the
52:   # reminder for the next fresh edit.
53:   rm -f "$REMINDED" 2>/dev/null || true
54: fi
55:
56: exit 0
```

Note line 46 also recommends `./build.ps1` and a plain `dotnet build`, both of which deploy into
the game install; the new reason text uses the non-deploying forms.

`.claude/hooks/check-deep-review.sh` (36 lines). It has NO mute marker: it re-emits on every Stop
until a `deep-reviewer` run appears in the audit log within 8 hours.

```bash
 1: #!/bin/bash
 2: # Stop hook: Remind to run /deep-review if real work was done but review wasn't run
 3: # This is a soft reminder, not a hard block
 4:
 5: # Check if deep-review was already run RECENTLY by looking at the agent audit log.
 6: # Recency-scoped (last 8h, matching session-stop.sh's window): before 2026-07-12 this
 7: # grepped the whole never-rotated log, so months-old runs permanently muted the reminder.
 8: # Fail-open: if date arithmetic is unavailable, fall back to the old whole-file grep.
 9: AUDIT_LOG=".claude/logs/agent-audit.log"
10: # log-agent.sh writes "[TS] agent_type=<type> agent_id=<id>" and nothing else, so the only
11: # evidence of a review is its reviewer's type. tools/test_hooks.sh section 7 drives both
12: # hooks together; change this pattern and that line format only as a pair.
13: PATTERN="agent_type=deep-reviewer"
14: if [[ -f "$AUDIT_LOG" ]]; then
15:   CUTOFF=$(date -d '-8 hours' '+%Y-%m-%d %H:%M:%S' 2>/dev/null)
16:   if [[ -n "$CUTOFF" ]]; then
17:     if awk -v c="$CUTOFF" -F'[][]' '$2 >= c' "$AUDIT_LOG" 2>/dev/null | grep -q "$PATTERN" 2>/dev/null; then
18:       exit 0
19:     fi
20:   elif grep -q "$PATTERN" "$AUDIT_LOG" 2>/dev/null; then
21:     exit 0
22:   fi
23: fi
24:
25: # Check if any C# or XML files were modified (indicating real work was done)
26: CHANGED_FILES=$(git diff --name-only 2>/dev/null)
27: UNTRACKED_FILES=$(git ls-files --others --exclude-standard 2>/dev/null)
28: ALL_FILES="$CHANGED_FILES"$'\n'"$UNTRACKED_FILES"
29:
30: # C# and C++ code, and XML is code too (Mike, 2026-09-18). git cannot see edits in the live
31: # TAOM_Map / Armory installs, so those never trigger this; /deep-review Step 1 sweeps them.
32: if echo "$ALL_FILES" | grep -qE '\.(cs|cpp|h|xml|xsl|xslt|mbproj|json)$'; then
33:   echo "REMINDER: Run /deep-review before closing out. It launches parallel agents to check standards, engine compatibility, efficiency, completeness, data flow, design and XML integrity, then applies the better ways it finds." >&2
34: fi
35:
36: exit 0
```

`.claude/hooks/check-version-tagged.sh` (54 lines). Header lines 5-7:
`# Mirrors check-changelog-updated.sh / check-verification-evidence.sh conventions: reads` /
`# git state (NOT stdin), emits a soft reminder to stderr, always exits 0 (non-blocking),` /
`# and mutes itself after one reminder per streak.` Body:

```bash
22: REMINDED=".claude/logs/.version-tag-reminded"
23: SUBMODULE="Main/_Module/SubModule.xml"
24:
25: cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0
...
44: # Already reminded about THIS version — stay quiet.
45: if [[ -f "$REMINDED" ]] && [[ "$(cat "$REMINDED" 2>/dev/null)" == "$VERSION" ]]; then
46:   exit 0
47: fi
48:
49: echo "REMINDER: module version $VERSION in $SUBMODULE has no git tag. A version with no tag cannot be resolved from a player's crash report — that is how v2.0.12 became unresolvable. Tag the release commit (git tag -a $VERSION -m '...') and push it (git push origin $VERSION). See docs/reference/release-process.md; /release runs the full sequence." >&2
50:
51: mkdir -p .claude/logs 2>/dev/null
52: echo "$VERSION" > "$REMINDED" 2>/dev/null || true
53:
54: exit 0
```

`.claude/hooks/check-changelog-updated.sh` (44 lines). Header lines 5-6:
`# Mirrors check-verification-evidence.sh conventions: reads git state (NOT` /
`# stdin), emits a soft reminder to stderr, always exits 0 (non-blocking).` Body:

```bash
18: REMINDED=".claude/logs/.changelog-reminded"
...
31: # Check if any C# or XML files were modified (indicating real work was done)
32: CHANGED_FILES=$(git diff --name-only 2>/dev/null)
33: if echo "$CHANGED_FILES" | grep -qE '\.(cs|xml|xslt|json)$'; then
34:   if [[ ! -f "$REMINDED" ]]; then
35:     echo "REMINDER: CHANGELOG.md has not been updated this session. Project rules require updating CHANGELOG.md after every change session." >&2
36:     mkdir -p .claude/logs 2>/dev/null
37:     touch "$REMINDED" 2>/dev/null || true
38:   fi
39: else
```

`.claude/logs/` is gitignored (`.gitignore:98`), so markers never show in `git status`.

### The push guard

`.claude/hooks/validate-push.sh` (131 lines). It sources `_pybin.sh` (line 11), reads stdin
(`INPUT=$(cat)`, line 13), extracts `.tool_input.command` (lines 27-37; jq is not on PATH here, so
the Python branch runs), tokenises the command, detects `--force`, `-f`, `--force-with-lease`,
`--force-if-includes`, bundled `-fu` and a leading `+` on the refspec, and then:

```bash
110: # Protected branches. bannerlord-1.4.5 is this repo's actual trunk and was missing until
111: # 2026-08-20, so a force push to the branch everyone works on passed unchallenged while
112: # master and main, which this repo does not use, were the only names guarded.
113: is_protected() {
114:   case "$1" in
115:     master|main|bannerlord-1.4.5) return 0 ;;
116:     *) return 1 ;;
117:   esac
118: }
119:
120: # Hard-block force push to a protected branch
121: if [[ "$FORCE" == true ]] && is_protected "$TARGET"; then
122:   echo "BLOCKED: force push to '$TARGET' is not allowed. Do not retry with --no-verify or as a plain push; explain the block and ask the user whether to push to a non-protected branch." >&2
123:   exit 2
124: fi
```

Exit 2 with stderr is a valid PreToolUse block that Claude does see. Line 4 of the header reads
`# Non-blocking warning for regular pushes to master/main.` The hook never reads `tool_name`, so a
PowerShell payload (same `tool_input.command` field) is parsed exactly like a Bash one.
PowerShell separators (`;`), the `&` call operator and backtick continuations only add harmless
tokens. Registration, `.claude/settings.json:33-46`:

```json
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          { ... ".claude/hooks/block-no-verify.sh" ... },
          {
            "type": "command",
            "command": ".claude/hooks/validate-push.sh",
            "timeout": 5
          },
```

The Bash group ends at line 91 (`      },`), followed by the `"Edit|Write"` group at line 92.
`git grep -n "1\.5\.x" b2e387db -- .claude/hooks` returns nothing. `git grep -n PowerShell
b2e387db -- .claude/settings.json` returns nothing. Server side (lane evidence, `gh api`, not
re-run while planning): `repos/haterade22/TAOM/branches/bannerlord-1.5.x` and `bannerlord-1.4.5`
both `"protected": false`; `repos/haterade22/TAOM/rulesets` is empty; the repo is public.

### The verification marker writer (why it must also see PowerShell)

`.claude/hooks/mark-verification-run.sh` (81 lines, "PostToolUse(Bash) hook" in its line 2)
touches `${CLAUDE_PROJECT_DIR:-$(pwd)}/.claude/logs/.verification-ran` when a segment of
`tool_input.command`, split on `;`, `&` and `|` (line 74), starts with `dotnet build`,
`dotnet test`, `build.ps1` or `pwsh`/`powershell` running `build.ps1` (lines 66-73). It is
registered only in the PostToolUse `"Bash"` group (`.claude/settings.json:140-154`, together with
`notify-test-results.sh`). On this Windows machine the PowerShell tool is the primary shell, so
a build run through PowerShell never touches the marker. Today that only feeds a reminder nobody
sees; once `check-verification-evidence.sh` reaches Claude it would fire falsely after every
PowerShell build. Its parser is already tool-agnostic (`tool_input.command`), so adding a
PowerShell registration is safe. `notify-test-results.sh` stays Bash-only (out of scope).

### The test harness

`tools/test_hooks.sh` (825 lines) is the contract test for every hook; run it as
`bash tools/test_hooks.sh` (`--verbose` prints every ok line). Relevant parts:

- `:26-27` `REPO=...; cd "$REPO"`, so it tests the checkout it lives in (your worktree).
- `:34-36` helpers `ok`, `bad`, `head2`; `:43-49` `HPY`, a safe Python for the harness itself.
- `:56-72` `decision_of`, which reads a PreToolUse decision.
- Section 2 (`:128-162`) fails on any registration without a `timeout` and prints
  `all N registrations have a timeout` (the catalog says 33: 28 in settings.json plus 5 skill
  frontmatter registrations; Step 1 measures it).
- Section 4 (`:304-386`) runs every `.claude/hooks/*.sh` with five payloads and
  `env CLAUDE_PROJECT_DIR="$SANDBOX"`, from cwd `$REPO`. It skips `_pybin.sh` by name at `:354`
  (`[[ "$name" == "_pybin.sh" ]] && continue`). Because three Stop hooks never `cd`, they
  currently read the REAL checkout's git state and write the real markers here.
- Section 5 (`:421-493`) repeats that loop in a starved PATH; same skip line at `:470`.
- Section 7 (`:738-767`) drives `log-agent.sh` and `check-deep-review.sh` together:

```bash
748: cdr_log() {
749:     printf '{"agent_type":"%s","agent_id":"t"}' "$1" \
750:         | CLAUDE_PROJECT_DIR="$CDR_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/log-agent.sh" >/dev/null 2>&1
751: }
752: cdr_reminds() {
753:     ( cd "$CDR_REPO" && timeout -k 2 10 bash "$REPO/.claude/hooks/check-deep-review.sh" </dev/null 2>&1 >/dev/null ) \
754:         | grep -q 'REMINDER: Run /deep-review'
755: }
```

  It pins stderr as the channel, which is the defect this plan removes.
- Section 7b starts at `:769` (`# ----...` then `head2 "7b. check-claude-files-tracked: ..."`).
- The script ends with `head2 "Summary"` and `exit 1` when any check failed.

### Conventions that bind this change (no C# is touched)

- **No C#, so the C# ADRs do not bind**: ADR-002 (entry points under 150 lines), ADR-007 (services
  take adapters, never sealed TaleWorlds types) and ADR-008 (test coverage for C#) have nothing to
  act on here. The test-first rule still applies to the hook work: Step 2 writes the failing
  `tools/test_hooks.sh` checks before any hook changes.
- **`.claude/rules/harness-facts.md` "TAOM hooks fail open"**: a hook's own bug never blocks;
  swallow internal errors; non-deny hooks exit 0. For these reminders: any internal failure means
  `exit 0` with no output.
- **`.claude/rules/hook-authoring.md` "Mirror the sibling's FULL convention set"**: the four Stop
  hooks must share one detection preamble, one output channel and one muting shape. The I/O
  preamble is `INPUT=$(cat)` verbatim.
- **`hook-authoring.md` "Prove a gate live"**: a hook change is done when a real session has shown
  it working, not when a test read its output. A subagent cannot end a main session's turn, so the
  live proof is a documented manual check for Mike (Step 14, "For Mike" section).
- **`hook-authoring.md` "Never spell it `python3`"**: the new code uses no Python at all.
- **`harness-facts.md` rule 3 / `check-changelog-changed.sh`**: a commit that stages anything under
  `.claude/`, `CLAUDE.md` or `AGENTS.md` is refused unless `CHANGELOG.md` is staged too.
- **ADR-011 knowledge tiers** (`CLAUDE.md` "Where new knowledge goes"): the vendor fact goes in
  `harness-facts.md` with its source; per-hook descriptions go in `hooks-catalog.md`; `CLAUDE.md`
  gets only the one-line correction to its "Hooks reach you only as ..." bullet.
- **AGENTS.md "Human prose"**: no em or en dash in prose you write (CHANGELOG, docs, commit body,
  reason strings); use commas, colons, parentheses. Hyphens in flags and paths are fine.

## Commands you will need

Run these with the Bash tool from the worktree root (`E:/repos/wt-011-stop-reminders`) unless a
step says otherwise. Your shell's working directory resets between tool calls, so prefix each
command with `cd E:/repos/wt-011-stop-reminders && ` (or use `git -C` for git). Give
`bash tools/test_hooks.sh` and every `dotnet` command `timeout: 600000`; the default 120 s is too
short for both. Every Read, Edit and Write path is `E:/repos/wt-011-stop-reminders/<path>`.

| Purpose | Command | Expected on success |
|---|---|---|
| Hook contract suite (the main gate here) | `bash tools/test_hooks.sh` | last lines `N passed, 0 failed`, exit 0 |
| Hook suite, one area | `bash tools/test_hooks.sh --verbose 2>&1 \| grep -E "<pattern>"` | the named `ok` lines, no `FAIL` |
| Config security audit (stands in for `/security-scan`, which you cannot invoke) | `python tools/audit_claude_config.py --min MED` | exit 0, exactly `HIGH:1  MED:1` (the known `tools/tests/test_audit_repo_secrets.py:178` fixture and the known `.mcp.json` filesystem `npx -y` finding); nothing new |
| Docs | `python tools/lint_docs.py` and `python tools/lint_docs.py --fail-on-drift` | the second exits 0 (it did at `b2e387db`) |
| JSON sanity | `python -c "import json;json.load(open('.claude/settings.json',encoding='utf-8'));print('ok')"` | `ok` |
| Build (sanity only, no C# changes) | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests (sanity only) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | see note below |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | not needed by this plan |
| Data | `python tools/validate_moduledata.py` | not applicable (no ModuleData change) |

**Test baseline at `b2e387db`** (measured by the orchestrator): 10,239 tests; 10,235 pass, 2 fail,
2 ignored. The two failures, `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (in the Elk tests and
`AnimaliaMountWiringTests`), assert on the live, unversioned Armory install that another session
is editing. They are not caused by this plan: do not chase them. The two ignored are deliberate
`[Ignore]`s in `WargAttackServiceTests.cs`. `dotnet test` therefore exits nonzero. Because the
Armory keeps changing, Step 1 re-measures this baseline in the worktree, and the pass condition
in Step 12 and the done criteria is "every failing test also failed in the Step 1 run".

Never run `./build.ps1` (it deploys into the game install). Never run the `dotnet` commands
without both `-p:DisableModuleCopy=true` and `-p:ModuleId=`.

## Scope

**In scope** (the only files you modify):

- `.claude/hooks/_stop_reminder.sh` (new: the shared emitter and loop guard)
- `.claude/hooks/check-verification-evidence.sh`
- `.claude/hooks/check-deep-review.sh`
- `.claude/hooks/check-version-tagged.sh`
- `.claude/hooks/check-changelog-updated.sh`
- `.claude/hooks/validate-push.sh` (protected list and comments only)
- `.claude/hooks/mark-verification-run.sh` (header comment line 2 only)
- `.claude/settings.json` (two new PowerShell groups, exact JSON in Step 0; applied before dispatch
  by Mike or the orchestrator; you verify and commit them, you never edit this file)
- `tools/test_hooks.sh` (new sections 7a and 7c, section 7 rewritten probe, two skip lines)
- `docs/reference/hooks-catalog.md`
- `.claude/rules/harness-facts.md` (the "Visibility" row only)
- `.claude/rules/hook-authoring.md` (one table row added, one cell reworded)
- `CLAUDE.md` (the "Hooks reach you only as ..." bullet only)
- `CHANGELOG.md` (one new entry)
- `plans/README.md` (the 011 status row only, if it exists)

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props` (single-owner;
  this plan needs none of them).
- Any C# file.
- `notify-test-results.sh` (same stderr class on PostToolUse, harmless: the test output is already
  in the tool result) and every other PreToolUse gate's Bash-only matcher (a separate finding; plan
  013 also edits the Bash hooks for start-up cost).
- `validate-push.sh`'s non-force warning (`:127-129`, stderr on exit 0, invisible). A visible
  warning on every ordinary push would be noise; leave it.
- `.claude/hooks/session-start.sh` (its untagged-version banner at `:176-190` already reaches
  Claude through SessionStart stdout).
- The GitHub ruleset. It is an outward-facing repository setting: you write nothing to GitHub. The
  exact steps for Mike are in the "For Mike" section at the end; you only relay them.
- Any `git push`, including `--dry-run`.

## Git workflow

- The worktree `E:/repos/wt-011-stop-reminders` on branch `plan/011-stop-reminders` (off the
  `bannerlord-1.5.x` tip) is created in Step 0, before dispatch. Work only inside it. Never stage,
  stash, reset, commit or edit anything in `E:/repos/TAOM` (another session's uncommitted work
  lives there). This plan file is untracked, so it exists only in the main tree: read it there.
- One commit at the end (Step 13). Subject format
  `<type>(<scope>): v<version> - <description>`, at most 72 characters, where `<version>` is the
  `<Version value="...">` of `Main/_Module/SubModule.xml` in the worktree (`v2.0.30` at planning).
  Planned subject (69 characters at `v2.0.30`):
  `fix(hooks): v2.0.30 - Stop reminders reach Claude, guard bannerlord-*`
- Body wrapped at 72, human prose, no em or en dashes, NO AI attribution trailer of any kind
  (`check-commit-subject-version.sh` refuses one).
- Stage explicit paths only (the in-scope list). Never `git add -A`, `git add .`, `git commit -a`.
- Never push. Never tag.

## Steps

Line numbers in Steps 4 to 10 are the pre-edit numbers at `b2e387db`. An insertion earlier in the
same file shifts them, so always locate a target by the quoted text, never by the number alone.

### Step 0: Before dispatch (Mike, or the orchestrator on Mike's explicit request; NOT the executor)

1. From the main tree:
   `git -C E:/repos/TAOM worktree add E:/repos/wt-011-stop-reminders -b plan/011-stop-reminders bannerlord-1.5.x`
2. In `E:/repos/wt-011-stop-reminders/.claude/settings.json` only (never the main tree's copy),
   make two insertions. Mike can make them in an editor. The orchestrator may make them with the
   Edit tool only after Mike explicitly asks it to, using the user-approved override that
   `config-protection.sh:39-44` checks (`/tmp/claude-config-override-${CLAUDE_SESSION_ID}` for the
   orchestrator's own session), and deletes the override file right after.
   - In `"PreToolUse"`, after the Bash group's closing `      },` (line 91) and before the
     `"Edit|Write"` group (line 92), insert:
     ```json
           {
             "matcher": "PowerShell",
             "hooks": [
               {
                 "type": "command",
                 "command": ".claude/hooks/validate-push.sh",
                 "timeout": 5
               }
             ]
           },
     ```
   - In `"PostToolUse"`, the Bash group ends at line 154 with `      }` followed by `    ],`.
     Change that `      }` to `      },` and insert after it:
     ```json
           {
             "matcher": "PowerShell",
             "hooks": [
               {
                 "type": "command",
                 "command": ".claude/hooks/mark-verification-run.sh",
                 "timeout": 5
               }
             ]
           }
     ```
   Separate groups, not `"Bash|PowerShell"` on the existing groups, so no other Bash gate and not
   `notify-test-results.sh` starts receiving PowerShell payloads in this change.
3. Check: `cd E:/repos/wt-011-stop-reminders && python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));print(sum('PowerShell' in g['matcher'] for ev in ('PreToolUse','PostToolUse') for g in d['hooks'][ev]))"`
   prints `2`. Then dispatch the executor.

### Step 1: Check Step 0 and record both baselines

Run the drift check (top of this file). Then check that Step 0 was done:

- `git -C E:/repos/wt-011-stop-reminders branch --show-current` prints `plan/011-stop-reminders`.
- The Step 0.3 `python -c` check, run in the worktree, prints `2`.
- `git -C E:/repos/wt-011-stop-reminders status --porcelain` prints exactly ` M .claude/settings.json`.

If any of the three fails, STOP before editing anything and report which one: Step 0 is not
yours to do. Then record the hook-suite baseline (Bash tool, `timeout: 600000`), writing the
output to your session scratchpad directory (the Read tool cannot see a Git Bash `/tmp` path):

```bash
cd E:/repos/wt-011-stop-reminders && bash tools/test_hooks.sh --verbose > "<scratchpad>/th-baseline.txt" 2>&1; echo "rc=$?"; tail -5 "<scratchpad>/th-baseline.txt"; grep -E "FAIL|registrations have a timeout" "<scratchpad>/th-baseline.txt"
```

Record the `N passed, M failed` line, every `FAIL` line, and N in the
`all N registrations have a timeout` line (it already counts Step 0's two new registrations; the
hooks catalog's 33 plus 2 gives 35, but trust the measured number). Then record the test baseline:

```bash
cd E:/repos/wt-011-stop-reminders && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > "<scratchpad>/dt-baseline.txt" 2>&1; echo "rc=$?"; grep -E "^\s*Failed |Passed!|Failed!|Total tests" "<scratchpad>/dt-baseline.txt"
```

Expected: about 10,239 tests with the two known Armory failures; write down every failing test
name, whatever the count.

**Verify**: you have the hook-suite counts and FAIL lines and the list of failing `dotnet test`
names written down. If the hook baseline already has failures, list them in your report; from
here on the rule is "no failure that is not in the baseline list, and every check this plan adds
passes".

### Step 2: Write the failing checks in `tools/test_hooks.sh` (RED)

Make four edits.

**2a.** At `:354` and `:470`, change `[[ "$name" == "_pybin.sh" ]] && continue` to
`[[ "$name" == _*.sh ]] && continue` (both loops skip sourced helpers, which the new
`_stop_reminder.sh` is). The line is identical in both places, so use the Edit tool with
`replace_all: true`, then confirm with `grep -c '\[\[ "$name" == _\*.sh \]\] && continue' tools/test_hooks.sh`,
which prints `2`.

**2b.** Replace the body of `cdr_reminds` in section 7 (`:752-755`) with a probe that reads the
new channel and clears the new streak marker first, so each probe is independent (without the
`rm`, the second probe would pass merely because the first one set the marker):

```bash
cdr_reminds() {
    rm -f "$CDR_REPO/.claude/logs/.deep-review-reminded"
    printf '%s' '{"hook_event_name":"Stop","session_id":"t","stop_hook_active":false}' \
        | CLAUDE_PROJECT_DIR="$CDR_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/check-deep-review.sh" 2>/dev/null \
        | grep -q '"decision":"block".*/deep-review'
}
```

**2c.** Insert a new section 7a immediately before the `# ---...` line that precedes
`head2 "7b. check-claude-files-tracked: ..."` (`:769`):

```bash
# ---------------------------------------------------------------------------
head2 "7a. Stop reminders reach Claude: one JSON block per streak, never bare stderr"
# Claude Code sends a Stop hook's exit-0 stderr and its plain stdout to the debug log only. The
# four reminders wrote there until plan 011 and none ever arrived. The one Stop channel Claude
# reads is {"decision":"block","reason":...} on stdout (hooks docs, "Stop decision control").
STOP_HOOKS=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(' '.join(sorted({h['command'].rsplit('/', 1)[-1]
                       for g in d.get('hooks', {}).get('Stop', [])
                       for h in g.get('hooks', [])})))
PY
)
[[ -z "$STOP_HOOKS" ]] && bad "no Stop registrations found in settings.json; the 7a discovery is broken"
for name in $STOP_HOOKS; do
    f=".claude/hooks/$name"
    [[ -f "$f" ]] || { bad "$name is registered on Stop but missing from .claude/hooks/"; continue; }
    hit=$(grep -n '>&2' "$f" | grep -vE '^[0-9]+:[[:space:]]*#' | head -1 | cut -d: -f1)
    if [[ -n "$hit" ]]; then
        bad "$name:$hit writes to stderr, which Claude never sees from a Stop hook; use taom_stop_block"
    else
        ok "$name writes nothing to stderr"
    fi
done

# The helper: JSON-escapes the reason, and the loop guard reads only the real key.
STOP_SAMPLE='quote " and backslash \ end'
HELPER_OUT=$(bash -c 'source .claude/hooks/_stop_reminder.sh && taom_stop_block "$1"' _ "$STOP_SAMPLE" 2>/dev/null)
if printf '%s' "$HELPER_OUT" | "$HPY" -c 'import json,sys; d=json.loads(sys.stdin.read()); sys.exit(0 if d.get("decision")=="block" and d.get("reason")==sys.argv[1] else 1)' "$STOP_SAMPLE" 2>/dev/null; then
    ok "taom_stop_block emits valid JSON and keeps quotes and backslashes"
else
    bad "taom_stop_block output is not a valid block with the reason intact: $(printf '%s' "$HELPER_OUT" | head -c 120)"
fi
for pair in '0|{"stop_hook_active":true}' '0|{"stop_hook_active": true}' \
            '1|{"stop_hook_active":false}' \
            '1|{"last_assistant_message":"x \"stop_hook_active\":true","stop_hook_active":false}'; do
    want="${pair%%|*}"; js="${pair#*|}"
    bash -c 'source .claude/hooks/_stop_reminder.sh && taom_stop_hook_active "$1"' _ "$js" 2>/dev/null
    got=$?
    [[ "$got" == "$want" ]] && ok "taom_stop_hook_active $want for $js" || bad "taom_stop_hook_active returned $got, expected $want, for $js"
done

# Each hook against a sandbox repo that triggers all four: a tracked, modified .cs, an untagged
# version, an untouched CHANGELOG, no build marker, no logged review.
STOP_REPO="$SANDBOX/stop-repo"
mkdir -p "$STOP_REPO/.claude/logs" "$STOP_REPO/Main/_Module"
git -C "$STOP_REPO" init -q 2>/dev/null
printf 'class Foo {}\n' > "$STOP_REPO/Main/Foo.cs"
printf '<Module>\n  <Version value="v9.9.9" />\n</Module>\n' > "$STOP_REPO/Main/_Module/SubModule.xml"
printf '# CHANGELOG\n' > "$STOP_REPO/CHANGELOG.md"
git -C "$STOP_REPO" add Main CHANGELOG.md 2>/dev/null
git -C "$STOP_REPO" -c user.name=t -c user.email=t@example.invalid commit -qm init 2>/dev/null
printf 'class Foo { int x; }\n' > "$STOP_REPO/Main/Foo.cs"
stop_run() {
    printf '%s' "$2" | CLAUDE_PROJECT_DIR="$STOP_REPO" timeout -k 2 10 bash "$REPO/.claude/hooks/$1" 2>"$SANDBOX/stop.err"
}
stop_shape() {
    printf '%s' "$1" | "$HPY" -c '
import json, sys
raw = sys.stdin.read().strip()
if not raw:
    print("silent"); sys.exit()
try:
    d = json.loads(raw)
except Exception:
    print("invalid"); sys.exit()
ok = isinstance(d, dict) and d.get("decision") == "block" and isinstance(d.get("reason"), str) and d["reason"].strip()
print("block" if ok else "BADSHAPE")'
}
STOP_LIVE='{"hook_event_name":"Stop","session_id":"t","stop_hook_active":false}'
STOP_LOOP='{"hook_event_name":"Stop","session_id":"t","stop_hook_active":true}'
for name in check-verification-evidence.sh check-deep-review.sh check-version-tagged.sh check-changelog-updated.sh; do
    rm -f "$STOP_REPO"/.claude/logs/.*-reminded "$STOP_REPO/.claude/logs/agent-audit.log"
    got=$(stop_shape "$(stop_run "$name" "$STOP_LOOP")")
    [[ "$got" == silent ]] && ok "$name is silent when stop_hook_active is true" \
        || bad "$name answered '$got' with stop_hook_active true: a reminder could loop"
    got=$(stop_shape "$(stop_run "$name" "$STOP_LIVE")")
    if [[ "$got" == block && ! -s "$SANDBOX/stop.err" ]]; then
        ok "$name reminds through a JSON block"
    else
        bad "$name gave '$got' on a triggering tree (stderr: $(head -c 100 "$SANDBOX/stop.err" 2>/dev/null)); expected a JSON block and no stderr"
    fi
    got=$(stop_shape "$(stop_run "$name" "$STOP_LIVE")")
    [[ "$got" == silent ]] && ok "$name mutes after one reminder" \
        || bad "$name reminded twice in one streak ('$got')"
done
```

**2d.** Insert a new section 7c after the end of section 7b (immediately before the `# ---...`
line that precedes `head2 "8. /context-budget scan.sh ..."`):

```bash
# ---------------------------------------------------------------------------
head2 "7c. validate-push refuses a force push to every bannerlord-* branch, from either shell tool"
# The protected list named only master, main and bannerlord-1.4.5 while the release tags moved to
# bannerlord-1.5.x, and the hook was registered for Bash only (plan 011).
VP_CASES=(
  "2|git push --force origin bannerlord-1.5.x"
  "2|git push origin +bannerlord-1.5.x"
  "2|git push --force-with-lease origin bannerlord-1.6.x"
  "2|git push -f origin bannerlord-1.4.5"
  "2|git push --force origin main"
  "0|git push origin bannerlord-1.5.x"
  "0|git push --force origin plan/011-stop-reminders"
)
for tool in Bash PowerShell; do
    for entry in "${VP_CASES[@]}"; do
        want="${entry%%|*}"; cmd="${entry#*|}"
        payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$tool" "$cmd")
        printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash .claude/hooks/validate-push.sh >/dev/null 2>&1
        got=$?
        [[ "$got" == "$want" ]] && ok "validate-push [$tool] rc=$got for: $cmd" \
            || bad "validate-push [$tool] expected rc=$want, got $got for: $cmd"
    done
done
VP_REG=$("$HPY" - <<'PY'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
def has(ev, hook):
    return any('PowerShell' in g.get('matcher', '').split('|')
               and any(h['command'].endswith(hook) for h in g.get('hooks', []))
               for g in d.get('hooks', {}).get(ev, []))
print('ok' if has('PreToolUse', 'validate-push.sh') and has('PostToolUse', 'mark-verification-run.sh') else 'missing')
PY
)
[[ "$VP_REG" == ok ]] && ok "validate-push (PreToolUse) and mark-verification-run (PostToolUse) are registered for PowerShell" \
    || bad "settings.json lacks a PowerShell registration for validate-push.sh (PreToolUse) or mark-verification-run.sh (PostToolUse)"
```

Write these with the Edit tool (never a Bash heredoc: heredocs mangle backslashes and quotes).

**Verify (RED)**:
`cd E:/repos/wt-011-stop-reminders && bash tools/test_hooks.sh --verbose > "<scratchpad>/th-red.txt" 2>&1; echo "rc=$?"; grep FAIL "<scratchpad>/th-red.txt"`
Expected: `rc=1`, and the FAIL lines are the baseline ones plus exactly these 18 new ones:

- 7a static, 4 FAILs: `writes to stderr` for `check-changelog-updated.sh:35`,
  `check-deep-review.sh:33`, `check-verification-evidence.sh:46`, `check-version-tagged.sh:49`.
- 7a helper, 3 FAILs: `taom_stop_block output is not a valid block` and the two
  `taom_stop_hook_active returned 1, expected 0` lines (`_stop_reminder.sh` does not exist yet).
  The two `want 1` cases PASS at RED, because a failed `source` also returns 1.
- 7a runtime, 4 FAILs: `gave 'silent' on a triggering tree`, one per hook. The four
  `is silent when stop_hook_active is true` and the four `mutes after one reminder` checks PASS
  at RED (the old hooks print nothing on stdout); they turn meaningful once the hooks emit JSON.
- 7, 1 FAIL: `check-deep-review.sh stayed silent on a dirty .cs with no deep-reviewer run logged`.
  The second section 7 check (`a deep-reviewer run ... mutes the reminder`) passes.
- 7c, 6 FAILs: `bannerlord-1.5.x` force, `+bannerlord-1.5.x` and `bannerlord-1.6.x`
  force-with-lease, each for Bash and for PowerShell. The other four cases pass for both tools,
  and the registration check passes (Step 0 added the registrations).

If any section errors out instead of printing FAIL lines (a bash syntax error, a Python
traceback), fix the test code before going on: a check that cannot run is not a RED test.

### Step 3: Add the shared helper `.claude/hooks/_stop_reminder.sh`

Create the file with exactly this content (LF line endings, like its siblings):

```bash
#!/usr/bin/env bash
# _stop_reminder.sh: sourced by the four Stop reminder hooks. Never registered itself.
#
# WHY THIS EXISTS
# Claude Code sends a Stop hook's stderr (on exit 0) and its plain stdout to the debug log only;
# Claude never sees either (hooks docs, "Exit code 0"; .claude/rules/harness-facts.md
# "Visibility"). Until plan 011 all four reminders printed there and none ever arrived. The Stop
# channel Claude does read is JSON on stdout:
#   {"decision":"block","reason":"..."}
# Claude then answers the reason in one more response (hooks docs, "Stop decision control").
#
# LOOP GUARD
# The Stop payload's stop_hook_active is true when Claude is already continuing because a Stop
# hook blocked. Every caller exits silently then, so a reminder never blocks twice in a row. The
# harness also ends the turn after 8 consecutive blocks.
#
# USAGE (the top of every Stop reminder, in this order)
#   source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
#   INPUT=$(cat)
#   taom_stop_hook_active "$INPUT" && exit 0
#   cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0
#   ...
#   taom_stop_block "<hook name>: <what is true> <what to do> <how to decline in one line>"
#   ...then write the streak marker.
#
# No Python here: Stop fires on every turn, and a regex on the raw payload is enough. An escaped
# copy of the key inside last_assistant_message (\"stop_hook_active\") cannot match, because the
# closing quote must follow the key directly.

# Returns 0 when the payload says Claude is already continuing from a Stop hook block.
taom_stop_hook_active() {
    local re='"stop_hook_active"[[:space:]]*:[[:space:]]*true'
    [[ "$1" =~ $re ]]
}

# Prints the block decision with $1 as the reason. Escapes backslash and double quote, and turns
# CR, LF and TAB into spaces, which covers every character an authored reason contains.
# tools/test_hooks.sh 7a checks the output parses as JSON with the reason intact.
taom_stop_block() {
    local r="$1"
    r=${r//\\/\\\\}
    r=${r//\"/\\\"}
    r=${r//$'\r'/ }
    r=${r//$'\n'/ }
    r=${r//$'\t'/ }
    printf '{"decision":"block","reason":"%s"}\n' "$r"
}
```

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "taom_stop_block (emits|output)|taom_stop_hook_active (returned|[01] for)"`
shows five `ok` lines and no `FAIL`. (This pattern matches only the helper checks. The four 7a
`writes to stderr ...; use taom_stop_block` FAILs are still expected at this point and clear one
by one in Steps 4 to 7.)

### Step 4: Convert `check-verification-evidence.sh`

1. Replace header lines 6-7 with:
   ```bash
   # Channel: one JSON {"decision":"block","reason":...} on stdout per unbuilt streak, through
   # _stop_reminder.sh, the only Stop output Claude reads (exit-0 stderr goes to the debug log;
   # until plan 011 this hook wrote there and nothing arrived). Silent when stop_hook_active is
   # true. Detection reads git state; stdin is read only for that flag. Always exits 0, and any
   # internal failure exits 0 with no output (fail open).
   ```
2. Insert, directly above `MARKER=".claude/logs/.verification-ran"` (line 21), a blank-line
   separated block:
   ```bash
   source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
   INPUT=$(cat)
   taom_stop_hook_active "$INPUT" && exit 0
   # Anchor to the project, not the inherited cwd: mark-verification-run.sh writes its marker
   # under CLAUDE_PROJECT_DIR, and tools/test_hooks.sh runs this hook against sandboxes.
   cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0
   ```
3. Replace line 46 (the `echo "REMINDER: ..." >&2`) with:
   ```bash
       taom_stop_block "check-verification-evidence: a C# file in this tree changed after the last recorded build or test (nothing newer in .claude/logs/.verification-ran). Before calling the work done, run dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= or dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= and read the output (.claude/rules/evidence-over-claims.md); a subagent's report does not count. If the changed files belong to another session, or you are not claiming the work is done, say so in one line. This fires once per unbuilt streak."
   ```
4. In line 51 replace the em dash in the existing comment with a colon:
   `# Nothing left to verify (built since the edit, or edits reverted): re-arm the`.
   Leave the marker logic (lines 44-54) otherwise unchanged: the marker is written after the
   block is printed.

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "check-verification-evidence"`
shows `ok` for `writes nothing to stderr`, `is silent when stop_hook_active is true`,
`reminds through a JSON block`, `mutes after one reminder`, and no `FAIL` line for this hook.

### Step 5: Convert `check-deep-review.sh` and give it a streak marker

Without a marker, a visible reminder would block every Stop, because the main tree almost always
holds dirty files (often another session's). Replace the whole file with:

```bash
#!/bin/bash
# Stop hook: remind Claude to run /deep-review when reviewable work is dirty and no review ran
# recently. A reminder, not a gate: it cannot stop a commit.
#
# Channel: one JSON {"decision":"block","reason":...} on stdout per streak, through
# _stop_reminder.sh (see check-verification-evidence.sh). Silent when stop_hook_active is true.
# Always exits 0; any internal failure exits 0 with no output (fail open).
#
# Muting, two layers:
#  1. A deep-reviewer run in the agent audit log within the last 8 h mutes it and clears the
#     streak marker. Recency-scoped (matching session-stop.sh's window): before 2026-07-12 this
#     grepped the whole never-rotated log, so months-old runs permanently muted the reminder.
#     If date arithmetic is unavailable, fall back to the whole-file grep.
#  2. .deep-review-reminded (plan 011): one reminder per streak. It clears when nothing
#     reviewable is dirty or a review is logged, which re-arms the reminder.

source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
INPUT=$(cat)
taom_stop_hook_active "$INPUT" && exit 0
cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0

REMINDED=".claude/logs/.deep-review-reminded"
AUDIT_LOG=".claude/logs/agent-audit.log"
# log-agent.sh writes "[TS] agent_type=<type> agent_id=<id>" and nothing else, so the only
# evidence of a review is its reviewer's type. tools/test_hooks.sh section 7 drives both
# hooks together; change this pattern and that line format only as a pair.
PATTERN="agent_type=deep-reviewer"
if [[ -f "$AUDIT_LOG" ]]; then
  CUTOFF=$(date -d '-8 hours' '+%Y-%m-%d %H:%M:%S' 2>/dev/null)
  if [[ -n "$CUTOFF" ]]; then
    if awk -v c="$CUTOFF" -F'[][]' '$2 >= c' "$AUDIT_LOG" 2>/dev/null | grep -q "$PATTERN" 2>/dev/null; then
      rm -f "$REMINDED" 2>/dev/null || true
      exit 0
    fi
  elif grep -q "$PATTERN" "$AUDIT_LOG" 2>/dev/null; then
    rm -f "$REMINDED" 2>/dev/null || true
    exit 0
  fi
fi

# Any reviewable file modified or untracked means real work was done.
CHANGED_FILES=$(git diff --name-only 2>/dev/null)
UNTRACKED_FILES=$(git ls-files --others --exclude-standard 2>/dev/null)
ALL_FILES="$CHANGED_FILES"$'\n'"$UNTRACKED_FILES"

# C# and C++ code, and XML is code too (Mike, 2026-09-18). git cannot see edits in the live
# TAOM_Map / Armory installs, so those never trigger this; /deep-review Step 1 sweeps them.
if echo "$ALL_FILES" | grep -qE '\.(cs|cpp|h|xml|xsl|xslt|mbproj|json)$'; then
  if [[ ! -f "$REMINDED" ]]; then
    taom_stop_block "check-deep-review: C#, C++, XML, XSLT or JSON files are modified or untracked in this tree and no deep-reviewer agent has run in the last 8 hours (.claude/logs/agent-audit.log). Run /deep-review before committing that work. If the files belong to another session, or the work is not ready to commit, say so in one line. This fires once per streak; a logged deep-reviewer run re-arms it."
    mkdir -p .claude/logs 2>/dev/null
    touch "$REMINDED" 2>/dev/null || true
  fi
else
  # Nothing reviewable is dirty: re-arm for the next streak.
  rm -f "$REMINDED" 2>/dev/null || true
fi

exit 0
```

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "check-deep-review|deep-reviewer run"`
shows `ok` for the four 7a lines of this hook, `ok reminds on a dirty .cs when no deep-reviewer
run is logged` and `ok a deep-reviewer run logged by log-agent.sh mutes the reminder`, and no
`FAIL`.

### Step 6: Convert `check-version-tagged.sh`

1. Replace header lines 5-7 with:
   ```bash
   # Mirrors check-verification-evidence.sh: one JSON {"decision":"block","reason":...} on
   # stdout through _stop_reminder.sh (the only Stop output Claude reads; until plan 011 this
   # hook wrote to stderr and nothing arrived), silent when stop_hook_active is true, always
   # exits 0, and mutes itself after one reminder per version.
   ```
2. Insert, directly above `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0` (line 25):
   ```bash
   source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
   INPUT=$(cat)
   taom_stop_hook_active "$INPUT" && exit 0
   ```
   (The `source` must come before the `cd`: its path is relative to where the hook was invoked.)
3. Replace line 49 (the `echo "REMINDER: ..." >&2`) with:
   ```bash
   taom_stop_block "check-version-tagged: module version $VERSION in $SUBMODULE has no git tag, so a player's crash report naming $VERSION cannot be traced to a commit (that is how v2.0.12 became unresolvable). Tagging and pushing need the user's go-ahead: ask whether to tag the release commit (git tag -a $VERSION -m 'Release $VERSION') and push the tag (git push origin $VERSION); /release runs the full sequence (docs/reference/release-process.md). If the bump is not committed yet, say so in one line. This fires once per version."
   ```
4. Replace the em dash with a colon in three comment lines:
   `# Not a TAOM checkout (or the file moved) — nothing to assert.` (line 27),
   `# Unreadable or unexpected shape — fail open.` (line 35) and
   `# Already reminded about THIS version — stay quiet.` (line 44). Leave the "Why" block
   (lines 9-17, including its two dashes on line 11) as it is.

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "check-version-tagged"` shows the
four 7a `ok` lines and no `FAIL`.

### Step 7: Convert `check-changelog-updated.sh`

1. Replace header lines 5-6 with:
   ```bash
   # Mirrors check-verification-evidence.sh: one JSON {"decision":"block","reason":...} on
   # stdout per streak through _stop_reminder.sh (until plan 011 it wrote to stderr, which
   # Claude never sees), silent when stop_hook_active is true, always exits 0 (fail open).
   ```
2. Insert, directly above `REMINDED=".claude/logs/.changelog-reminded"` (line 18):
   ```bash
   source "$(dirname "${BASH_SOURCE[0]}")/_stop_reminder.sh" 2>/dev/null || exit 0
   INPUT=$(cat)
   taom_stop_hook_active "$INPUT" && exit 0
   cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || exit 0
   ```
3. Replace line 35 (the `echo "REMINDER: ..." >&2`) with:
   ```bash
       taom_stop_block "check-changelog-updated: tracked source files (.cs, .xml, .xslt or .json) are modified but CHANGELOG.md is unchanged in both the working tree and the index. AGENTS.md requires a CHANGELOG entry for every change session: add one before committing, or say in one line why this work needs none (for example, the files belong to another session). This fires once per streak."
   ```
4. Replace the em dashes in comment lines 14 (`Added 2026-08-05 — this hook predated the`) and
   40 (`# No relevant work dirty — re-arm for the next streak.`) with a colon or a comma.

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "check-changelog-updated"` shows the
four 7a `ok` lines and no `FAIL`.

### Step 8: Protect every `bannerlord-*` branch in `validate-push.sh`

1. Replace header line 4 with:
   `# Non-blocking stderr warning (which Claude does not see) for a plain push to a protected branch.`
2. Replace lines 110-118 with:
   ```bash
   # Protected branches: every bannerlord-* line, plus master and main (unused here, kept).
   # bannerlord-1.4.5 was missing until 2026-08-20, and when the live trunk moved to
   # bannerlord-1.5.x (the v2.0.29 and v2.0.30 tags are on it) the list did not follow, so a
   # force push to it passed unchallenged until plan 011. A pattern follows the next line too.
   is_protected() {
     case "$1" in
       master|main|bannerlord-*) return 0 ;;
       *) return 1 ;;
     esac
   }
   ```
Nothing else in the file changes (the tokenizer already handles PowerShell payloads; see
"Current state").

**Verify**: `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "validate-push \["` shows 14 `ok`
lines (7 cases x Bash and PowerShell) and no `FAIL`.

### Step 9: Confirm the PowerShell registrations and fix the marker writer's header

The two `settings.json` groups were added in Step 0 (before dispatch). Do not edit
`.claude/settings.json` in this step or any other; if the check below fails, STOP.

1. In `.claude/hooks/mark-verification-run.sh` line 2, change `PostToolUse(Bash) hook` to
   `PostToolUse(Bash and PowerShell) hook` (comment only).

**Verify**:
- `python -c "import json;json.load(open('.claude/settings.json',encoding='utf-8'));print('ok')"` prints `ok`.
- `bash tools/test_hooks.sh --verbose 2>&1 | grep -E "registered for PowerShell|registrations have a timeout"`
  shows `ok ... registered for PowerShell` and `ok all N registrations have a timeout` with the
  same N as in the Step 1 baseline (35 if the catalog's 33 was right; the new registrations were
  already counted there).

### Step 10: Correct the docs that describe these hooks

Use the Edit tool on the worktree copies (`E:/repos/wt-011-stop-reminders/<path>`). Each
**Old** block below is the exact file text at `b2e387db` (backticks included), so it can be the
Edit tool's `old_string` as it stands; each **New** block is the `new_string`. No em or en dashes
in new text. Line numbers are for orientation only.

**10a. `docs/reference/hooks-catalog.md`**

First prove the counts: `ls .claude/hooks/*.sh | wc -l` prints `30` (29 at `b2e387db` plus
`_stop_reminder.sh`), and the Step 1 section 2 line said `35` (28 `settings.json` registrations at
`b2e387db`, plus Step 0's 2, plus 5 frontmatter). If either differs, write the measured numbers
into the New text instead and say so in your report.

- Line 3 (a substring of that long line). Old:
  ```text
  **Recounted 2026-09-13 (after `check-commit-subject-version.sh`): 29 scripts on disk, 28 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 33 total.** One script, `_pybin.sh`, is a sourced helper with no registration of its own.
  ```
  New (put today's date, `YYYY-MM-DD`, in place of `<today>`):
  ```text
  **Recounted <today> (plan 011): 30 scripts on disk, 30 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 35 total.** Two scripts, `_pybin.sh` and `_stop_reminder.sh`, are sourced helpers with no registration of their own.
  ```
- Line 24. Old:
  ```text
  | `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md when source is dirty. One-shot per streak (`.changelog-reminded` marker, added 2026-08-05); re-arms when CHANGELOG becomes dirty/staged |
  ```
  New:
  ```text
  | `check-changelog-updated.sh` | Stop | Reminds Claude to update CHANGELOG.md when tracked source is modified and CHANGELOG is not, through a JSON `decision: block` (`_stop_reminder.sh`) that Claude reads and answers once; silent when `stop_hook_active` is true. One-shot per streak (`.changelog-reminded` marker, added 2026-08-05); re-arms when CHANGELOG becomes dirty/staged |
  ```
- Line 25, two substrings in the same row (the rest stays). Old 1: `Reminds to tag + push the release when`
  New 1: ``Reminds Claude (JSON `decision: block` via `_stop_reminder.sh`, silent when `stop_hook_active` is true) to ask about tagging and pushing the release when``
  Old 2: `has no matching git tag — the version every crash bundle reports`
  New 2: `has no matching git tag, the version every crash bundle reports`
- Line 33. Old:
  ```text
  | `check-deep-review.sh` | Stop | Reminds to run `/deep-review` if real work was done |
  ```
  New:
  ```text
  | `check-deep-review.sh` | Stop | Reminds Claude to run `/deep-review` when C#, C++, XML, XSLT or JSON files are dirty and no `deep-reviewer` agent is logged in the last 8 h. JSON `decision: block` via `_stop_reminder.sh`, silent when `stop_hook_active` is true; one-shot per streak (`.deep-review-reminded`, since plan 011), re-armed by a logged review or a clean tree |
  ```
- Line 36. Old:
  ```text
  | `validate-push.sh` | PreToolUse (Bash) | Warns on push to master/main; hard-blocks force push to protected branches |
  ```
  New:
  ```text
  | `validate-push.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks (exit 2) a force push (`--force`, `-f`, `--force-with-lease`, a `+` refspec) to master, main or any `bannerlord-*` branch; a plain push to one gets a stderr warning, which Claude does not see. Registered for both shell tools since plan 011. GitHub protects no branch until the owner adds a ruleset |
  ```
- Line 43 (a substring). Old: ``| `mark-verification-run.sh` | PostToolUse (Bash) |``
  New: ``| `mark-verification-run.sh` | PostToolUse (Bash, PowerShell) |``
- Line 44. Old:
  ```text
  | `check-verification-evidence.sh` | Stop | Reminds to build/test when a `.cs` file changed but no verification ran since the last edit. Enforces `.claude/rules/evidence-over-claims.md`. |
  ```
  New:
  ```text
  | `check-verification-evidence.sh` | Stop | Reminds Claude to build/test when a `.cs` file changed but no verification ran since the last edit, through a JSON `decision: block` (`_stop_reminder.sh`); silent when `stop_hook_active` is true; one-shot per unbuilt streak. Enforces `.claude/rules/evidence-over-claims.md`. |
  ```
- "Responding to a hook" paragraph, lines 54-56 (the Old text starts mid-line 54 and spans three
  lines; the paragraph's first clause and its last sentence, about PreToolUse `hookSpecificOutput`
  and #647, stay unchanged). Old:
  ```text
  What reaches Claude: SessionStart stdout, a PreToolUse `deny` reason, and exit-2
  stderr. An `ask` reason goes to the user. Stdout from Stop, PreCompact and PostCompact hooks, and
  stderr from a hook that exits 0, go to the debug log only.
  ```
  New:
  ```text
  What reaches Claude: SessionStart stdout, a PreToolUse `deny` reason, exit-2
  stderr, and a Stop hook's JSON `{"decision":"block","reason":...}`. Claude answers that reason in
  one more response; the next Stop payload carries `stop_hook_active: true`, and every TAOM Stop
  hook stays silent then. An `ask` reason goes to the user. Plain stdout from Stop, PreCompact and
  PostCompact hooks, and stderr from a hook that exits 0, go to the debug log only.
  ```

**10b. `.claude/rules/harness-facts.md` line 60 ("Visibility" row).** Three substrings of that
one line. Change nothing else in this file (the main tree holds another session's edit to line 53;
keeping your change to line 60 keeps the two hunks apart).

- Old 1: ``an `ask` reason is shown to the user, not to Claude. |``
  New 1: ``an `ask` reason is shown to the user, not to Claude. A Stop hook reaches Claude only through `{"decision":"block","reason":"..."}` on stdout, or exit 2: Claude continues for one more response with the reason, the next Stop payload carries `stop_hook_active: true`, and the harness ends the turn after 8 consecutive blocks. `systemMessage` goes to the user, not Claude. |``
- Old 2: `| hooks docs (DOC, 2026-09-23); EMPIRICAL 2026-09-23: `
  New 2: `| hooks docs, "Exit code 0" and "Stop decision control" (DOC, 2026-09-23); EMPIRICAL 2026-09-23: `
- Old 3: `make it a gate, or move it to a visible channel. |`
  New 3: ``make it a gate, or move it to a visible channel. The four Stop reminders use `.claude/hooks/_stop_reminder.sh` (`tools/test_hooks.sh` 7a). |``

**10c. `.claude/rules/hook-authoring.md`.**

- After the "Exit semantics" row (line 21), which reads
  ``| Exit semantics (`exit 0` non-blocking vs `exit 2`/JSON `deny`) | the sibling in the same event | (got this right) |``,
  insert this row:
  ```text
  | **Output channel** (what Claude actually reads) | Stop: `_stop_reminder.sh`; PreToolUse: `block-dangerous-git.sh` (`hookSpecificOutput`) | the four Stop reminders wrote to stderr, which Claude never sees, until plan 011 |
  ```
- Line 128, "Handle rc 124 explicitly" row. Old: `or write to stderr for an advisory hook`
  New: ``or, for an advisory hook, use its event's visible channel (`harness-facts.md` "Visibility"; exit-0 stderr reaches no one)``

**Expected size warnings.** Both rules are path-scoped. `hook-authoring.md` is 12,188 B at
`b2e387db` and ends near 12,490 B; `harness-facts.md` is 11,794 B and ends near 12,220 B. The
per-file cap is `SCOPED_RULE_MAX_BYTES = 12_288` (`tools/lint_docs.py:69`) with
`SCOPED_RULE_BUDGET_ENFORCE = False` (`:71`), so crossing it prints a report-only `size-warn` and
`--fail-on-drift` still exits 0. Measure both with `wc -c`, and name any `size-warn` in your
report. Do not trim other rows to get under the cap.

**10d. `CLAUDE.md` lines 23-24.** Old:
```text
- **Hooks** reach you only as SessionStart output, a deny reason or exit-2 stderr (an ask reason goes
  to the user); follow the instruction in the message ([hooks catalog](docs/reference/hooks-catalog.md)).
```
New:
```text
- **Hooks** reach you only as SessionStart output, a deny reason, exit-2 stderr or a Stop hook's
  block reason (an ask reason goes to the user); follow the instruction in the message
  ([hooks catalog](docs/reference/hooks-catalog.md)).
```
Change nothing else in `CLAUDE.md` (the main tree holds another session's edit at line 95).

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0 (a `size-warn` for the two rules
is allowed, see 10c). Then check for new dashes with a byte pattern (a `grep -P '\x{...}'` form
errors out in this Git Bash and would pass silently):
`cd E:/repos/wt-011-stop-reminders && git diff -U0 -- docs/reference/hooks-catalog.md .claude/rules CLAUDE.md | grep '^+' | grep -n $'\xe2\x80\x94\|\xe2\x80\x93'; echo "rc=$?"`
Expected: no match lines and `rc=1`.

### Step 11: Add the CHANGELOG entry

Read `Main/_Module/SubModule.xml` in the worktree for the version (`grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`).
In `CHANGELOG.md`, under the newest `## YYYY-MM-DD` heading at the top (add a heading for today's
date above it if it is not today's), insert as the first entry:

```markdown
### fix(hooks): <version> - Stop reminders reach Claude, force pushes to bannerlord-* blocked

- **The four Stop reminders were silent.** `check-verification-evidence.sh`, `check-deep-review.sh`,
  `check-version-tagged.sh` and `check-changelog-updated.sh` printed to stderr and exited 0, which
  Claude Code sends to the debug log only, so no reminder ever reached Claude and each one-shot
  marker was spent on nothing. They now print `{"decision":"block","reason":...}` through the new
  `.claude/hooks/_stop_reminder.sh`, stay silent when `stop_hook_active` is true, and still fire
  once per streak. `check-deep-review.sh` gained the streak marker it never had
  (`.deep-review-reminded`); without it a visible reminder would block every turn in a dirty tree.
  The verification reason now names the non-deploying build and test commands.
- **Force pushes to the live trunk.** `validate-push.sh` protected `bannerlord-1.4.5` but not
  `bannerlord-1.5.x`, where the release tags live; it now protects every `bannerlord-*` branch,
  and it and `mark-verification-run.sh` are registered for the PowerShell tool too.
- `tools/test_hooks.sh` 7a and 7c pin both. Docs: hooks catalog, `harness-facts.md` Visibility,
  `hook-authoring.md`, the CLAUDE.md hooks bullet.
- **Owed:** Mike's live check of a Stop reminder and a PowerShell force push, and the GitHub
  ruleset on `bannerlord-*` (steps in `plans/011-stop-reminders-and-trunk-guard.md`, "For Mike").
```

**Verify**: `git diff --stat -- CHANGELOG.md` shows one file with only added lines (no deletions).

### Step 12: Full verification

Run, in order, with the Bash tool from the worktree (`timeout: 600000` for items 1, 4 and 5), and
read each result:

1. `bash tools/test_hooks.sh` : exit 0, `0 failed` (or only the baseline failures from Step 1,
   none of them in sections 2, 4, 5, 7, 7a, 7c).
2. `python tools/audit_claude_config.py --min MED` : exit 0, `HIGH:1  MED:1`, the same two
   findings as in "Commands you will need", nothing new.
3. `python tools/lint_docs.py --fail-on-drift` : exit 0.
4. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` : exit 0, 0 errors.
5. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` : every failing test name
   also failed in the Step 1 `dotnet test` run (normally the two known Armory tests named in
   "Commands you will need"). This plan changes no C#, so a new failure means the Armory moved
   again or the environment changed: report it, do not chase it, and do not count it as yours.
6. `git status --porcelain` : only the in-scope paths, with `.claude/hooks/_stop_reminder.sh` as
   `??` (untracked, not ignored).
7. `grep -n '>&2' .claude/hooks/check-verification-evidence.sh .claude/hooks/check-deep-review.sh .claude/hooks/check-version-tagged.sh .claude/hooks/check-changelog-updated.sh`
   prints nothing.

### Step 13: Commit (explicit paths, in the worktree)

First, if the worktree's `plans/README.md` has a row for plan 011, set its Status cell to
`DONE on plan/011-stop-reminders, not pushed; live check and GitHub ruleset owed to Mike` and add
`plans/README.md` to the `git add` line below (15 paths). If it has no 011 row, leave the file
alone and say so in your report.

```bash
git -C E:/repos/wt-011-stop-reminders add .claude/hooks/_stop_reminder.sh .claude/hooks/check-verification-evidence.sh .claude/hooks/check-deep-review.sh .claude/hooks/check-version-tagged.sh .claude/hooks/check-changelog-updated.sh .claude/hooks/validate-push.sh .claude/hooks/mark-verification-run.sh .claude/settings.json tools/test_hooks.sh docs/reference/hooks-catalog.md .claude/rules/harness-facts.md .claude/rules/hook-authoring.md CLAUDE.md CHANGELOG.md
```

Then commit with a message file written by the Write tool (subject line from "Git workflow", a
blank line, a body of 4 to 8 wrapped lines saying what was silent, what now reaches Claude, and
what is owed to Mike). Use `git -C E:/repos/wt-011-stop-reminders commit -F <file>`.

The commit gates run from the MAIN tree's hooks. If one refuses the commit, read its reason: fix
the cause if it is about your commit (missing CHANGELOG, subject label, an untracked `.claude/`
file); STOP if the reason describes state you do not own (see STOP conditions).

**Verify**: `git -C E:/repos/wt-011-stop-reminders log -1 --format=%s` prints the subject;
`git -C E:/repos/wt-011-stop-reminders show --stat HEAD` lists exactly the 14 staged paths (15
with `plans/README.md`);
`git -C E:/repos/wt-011-stop-reminders log -1 --format=%B | grep -ci "co-authored-by"` prints `0`.

### Step 14: Report, and hand Mike the live checks

Report the commit SHA and whether `plans/README.md` had a 011 row (Step 13). In your report,
paste the "For Mike" section below verbatim, with the worktree path and commit SHA filled
in. Do not run any of it yourself.

## Test plan

- **New checks, all in `tools/test_hooks.sh`:**
  - 7a static: no Stop-registered hook writes to stderr (discovered from `settings.json`, so a
    fifth Stop hook is covered automatically).
  - 7a helper: `taom_stop_block` output parses as JSON with a reason containing `"` and `\` intact;
    `taom_stop_hook_active` is true for compact and spaced `true`, false for `false`, and false for
    an escaped copy of the key inside `last_assistant_message`.
  - 7a runtime, per hook (4 hooks x 3 cells): `stop_hook_active: true` gives no output (and writes
    no marker, which the next cell proves); a triggering tree gives a valid block and no stderr;
    a second Stop in the same streak gives no output.
  - 7 (rewritten probe): the deep-review reminder arrives as a block with no review logged, and a
    `deep-reviewer` line written by the real `log-agent.sh` mutes it (marker cleared between
    probes so the mute is proven by the log, not the marker).
  - 7c: seven push cases x two tool names (Bash, PowerShell) with exact exit codes, plus the
    PowerShell registrations in `settings.json`.
- **Existing checks that must stay green:** sections 2 (timeouts, now 35 registrations), 4
  (contract; the Stop hooks now run against the sandbox instead of the real checkout, because they
  `cd` to `CLAUDE_PROJECT_DIR`), 5 (starved PATH, fail open), 5b/5c (validate-push is still a
  blocking Bash gate with `taom_pybin_degraded`).
- **Pattern to model on:** section 7b (`:769-797`) for a sandbox git repo and a hook driven with
  `CLAUDE_PROJECT_DIR`; section 6 (`:704`) for building a payload with `json.dumps` through `$HPY`.
- **Structurally untestable here:** that Claude Code 2.1.241 actually delivers a Stop block reason
  to Claude, and that the harness fires `validate-push.sh` for a PowerShell tool call. Both need a
  live main session; they are the manual checks in "For Mike". Name them in the commit body as
  `Not-tested:`.

## Done criteria

ALL must hold:

- [ ] `bash tools/test_hooks.sh` exits 0 in the worktree (or fails only on baseline failures
      recorded in Step 1, none in sections 2, 4, 5, 7, 7a, 7c).
- [ ] `grep -c '>&2' .claude/hooks/check-verification-evidence.sh .claude/hooks/check-deep-review.sh .claude/hooks/check-version-tagged.sh .claude/hooks/check-changelog-updated.sh` prints `:0` for all four.
- [ ] `grep -n 'bannerlord-\*' .claude/hooks/validate-push.sh` finds the `is_protected` case line.
- [ ] `python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));print(sum('PowerShell' in g['matcher'] for ev in ('PreToolUse','PostToolUse') for g in d['hooks'][ev]))"` prints `2`.
- [ ] `python tools/audit_claude_config.py --min MED` exits 0 with `HIGH:1  MED:1` (unchanged).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0; `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails only tests that also failed in the Step 1 baseline run (normally the two known Armory tests).
- [ ] `git -C E:/repos/wt-011-stop-reminders show --stat HEAD` lists exactly the 14 in-scope paths (15 with `plans/README.md`, nothing else); `git -C E:/repos/wt-011-stop-reminders status --porcelain` is empty.
- [ ] The commit subject matches `^fix\(hooks\): v[0-9]+\.[0-9]+\.[0-9]+ - ` and is at most 72 characters; no attribution trailer.
- [ ] Nothing was pushed: `git -C E:/repos/wt-011-stop-reminders status -sb` shows no upstream for `plan/011-stop-reminders`.
- [ ] `plans/README.md` 011 row updated in the commit (or its absence reported).

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything, or an excerpt in "Current state" does not match the file (line
  numbers or text).
- Step 1 finds Step 0 not done (no worktree, wrong branch, the PowerShell count is not `2`, or
  `git status --porcelain` shows anything besides ` M .claude/settings.json`). Report which check
  failed and the two JSON blocks from Step 0 as the edit awaiting Mike. Never edit
  `.claude/settings.json` yourself, never create an override file, and never rewrite the file
  through a shell command.
- Any Edit or Write would land under `E:/repos/TAOM` (the main tree) instead of
  `E:/repos/wt-011-stop-reminders`. If you find you already changed a main-tree file, stop at once
  and report the path; do not try to revert it (another session's edits may share the file).
- A commit gate refuses the commit for a reason about state you do not own (for example, it names
  files staged in `E:/repos/TAOM`, or a version that differs from the worktree's `SubModule.xml`).
  The gates run from the main tree, so they can see another session's index.
- The RED run in Step 2 does not produce the listed failures (for example a Stop hook already
  emits JSON, or `validate-push.sh` already blocks `bannerlord-1.5.x`): the plan's premise has
  changed.
- Section 4 or 5 of `tools/test_hooks.sh` reports a Stop hook hanging, exiting other than 0, or
  writing stderr after your change.
- A step's verification fails twice after a reasonable fix attempt.
- The fix appears to need any out-of-scope file, especially `Main/IoC.cs`, `Main/SubModule.cs`,
  `Main/TAOM.csproj` or `Directory.Build.props` (none should be needed).
- Any instruction would have you push, tag, run `gh`, or change a GitHub setting.
- You find evidence that `decision: "block"` from a Stop hook is not delivered to Claude on the
  installed Claude Code (for example a newer `harness-facts.md` fact saying so). That assumption is
  load-bearing; report it rather than switching the output form yourself.

## Maintenance notes

- **Riskiest assumption:** that Claude Code 2.1.241 delivers a Stop hook's `decision: "block"`
  reason to Claude as the current docs describe. No test in the repo can prove it; only Mike's live
  check A below does. If it fails, the fallback is exit 2 with the reason on stderr (the docs say
  exit 2 on Stop routes stderr to Claude the same way); that is a one-function change in
  `taom_stop_block` plus the 7a static check.
- **Noise budget:** each reminder now costs one extra Claude response once per streak, and a block
  shows as a Stop hook notice in Mike's transcript. The markers bound it: verification re-arms after
  a build, deep review after a logged review or a clean tree, changelog when CHANGELOG is touched,
  the tag reminder per version. If it still reads as nagging, tune the re-arm conditions, not the
  channel. `hookSpecificOutput.additionalContext` (the docs' "non-error feedback" form, no error
  notice) is the cosmetic upgrade once a live check shows 2.1.241 honours it for Stop.
- **Headless and orchestrated runs:** a Stop block also fires in a `claude -p` main session and
  makes its final message a reply to the reminder. Subagents are not affected (they raise
  SubagentStop, which TAOM does not hook). If a script that reads a headless run's last message
  starts returning reminder replies, that is this change; the markers limit it to once per streak.
- **Shared markers:** `.claude/logs/` is per checkout, so parallel sessions in one checkout share a
  streak marker (one reminder for all of them). That was already true before this plan.
- **Merge interaction:** plan 013 edits the top of every Bash-matched hook (including
  `validate-push.sh` and `mark-verification-run.sh`) to skip Python start-up; expect a trivial
  textual conflict near line 11 of `validate-push.sh` if both land. `CLAUDE.md` and
  `harness-facts.md` carry another session's uncommitted edits in the main tree on different lines
  (95 and 53); the merge keeps both hunks.
- **Branch naming:** `bannerlord-*` protects every branch with that prefix, including port branches
  such as `bannerlord-1.5.0-port`. Name work branches without the prefix, or lift the rule
  deliberately.
- **Known gap left alone:** `validate-push.sh` resolves `HEAD` and an empty refspec with
  `git branch --show-current` in the main tree, so `git -C <worktree> push --force origin HEAD`
  checks the main tree's branch, not the worktree's. Pre-existing; not changed here.
- **Reviewer focus** (the orchestrator runs `/deep-review` and may run `/review-codex`): the
  escaping in `taom_stop_block`; that every Stop hook sources the helper before its `cd`; that no
  marker is written on the `stop_hook_active` path; the deep-review marker's re-arm conditions;
  that the reason strings are factual, name non-deploying commands, and give Claude a one-line way
  to decline.

## For Mike (the executor relays this; nobody runs it for you)

**A. Live check: a Stop reminder reaches Claude.** In a terminal:

```bash
cd E:/repos/wt-011-stop-reminders
rm -f .claude/logs/.verification-ran .claude/logs/.verification-reminded .claude/logs/.deep-review-reminded
printf 'class LiveProbe011 {}\n' > Main/LiveProbe011.cs
claude
```

Accept the trust prompt for the folder if asked, then type `Say hi.` Expected: after Claude's
reply, a Stop hook notice appears and Claude writes a second short reply that addresses the
`check-verification-evidence` reason (and the `check-deep-review` one; both trigger on the untracked
`.cs`). Then type `Say hi again.` Expected: one reply, no reminder (muted for the streak). Quit,
then clean up: `rm Main/LiveProbe011.cs .claude/logs/.verification-reminded .claude/logs/.deep-review-reminded`.
If no reminder arrives in the first step, the riskiest assumption failed: report it and do not merge.

**B. Live check: the PowerShell tool cannot force-push the trunk.** In the same session, before
quitting, type: `Using the PowerShell tool, run exactly: git push --force --dry-run origin bannerlord-1.5.x`.
Expected: the call is refused with `BLOCKED: force push to 'bannerlord-1.5.x' is not allowed`. If a
permission prompt appears instead, choose No: the hook did not fire for PowerShell. (`--dry-run`
never changes the remote, so the check is harmless either way.)

**C. Server-side guard (a GitHub ruleset; applies to every client, not just Claude).** Either in
the browser: repository Settings, Rules, Rulesets, New ruleset, New branch ruleset; name
`protect-bannerlord-branches`; Enforcement status Active; Target branches, Add target, Include by
pattern, `bannerlord-*`; tick "Restrict deletions" and "Block force pushes"; leave required checks
off (CI does not compile C# on this branch yet); Create. Or with `gh`: save this as
`ruleset.json`

```json
{
  "name": "protect-bannerlord-branches",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["refs/heads/bannerlord-*"], "exclude": [] } },
  "rules": [ { "type": "deletion" }, { "type": "non_fast_forward" } ]
}
```

and run `gh api -X POST repos/haterade22/TAOM/rulesets --input ruleset.json`. Check it with
`gh api repos/haterade22/TAOM/rules/branches/bannerlord-1.5.x --jq '.[].type'`, which should print
`deletion` and `non_fast_forward`. With no bypass list, a deliberate history rewrite later needs
the ruleset switched off first; add yourself as a bypass actor in the browser if you would rather
keep that door open.
