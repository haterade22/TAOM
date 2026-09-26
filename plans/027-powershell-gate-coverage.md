# Plan 027: Make the eight Bash-only PreToolUse gates cover the PowerShell tool, read PowerShell syntax, and close validate-push's remaining bypass shapes

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in the "STOP
> conditions" section occurs, stop and report; do not improvise. Do NOT edit `plans/README.md`:
> the orchestrator maintains that index. You cannot invoke skills or spawn agents.
>
> **Where you work (read this twice)**: all work happens in the worktree
> `E:/repos/taom-improve/wt-027` (branch `improve/027-powershell-gate-coverage`, checked out at
> `96afb6fb`), never in the main tree `E:/repos/TAOM`, whose working tree holds another session's
> uncommitted edits and whose hooks run in every live Claude session. Your Bash working directory
> resets between calls, so:
> - **Every Bash call begins with `cd /e/repos/taom-improve/wt-027 && `.** A relative path in a
>   call without that prefix runs against the wrong tree.
> - **Every Read, Edit and Write path is absolute**: `E:/repos/taom-improve/wt-027/<repo path>`.
> - **Scratch files** (commit message files, captured test output, payload files you write while
>   debugging) go in `E:/repos/taom-improve/scratch/plans/027/` (Bash:
>   `/e/repos/taom-improve/scratch/plans/027/`), never inside the worktree and never on C:.
>   Never create a clone, copy or archive export of the repository.
> - **Temp files stay on E: too.** `tools/test_hooks.sh` makes its sandbox with `mktemp -d`, and
>   `dotnet` and Python write under TEMP, which default to C:. In Step 1 you Write the two-line file
>   `E:/repos/taom-improve/scratch/plans/027/tmpenv.sh`, and every Bash call that runs
>   `tools/test_hooks.sh`, the `tools/tests` unittest discovery, `dotnet build` or `dotnet test`
>   begins `cd /e/repos/taom-improve/wt-027 && . /e/repos/taom-improve/scratch/plans/027/tmpenv.sh && `.
>   (Checked at planning: `mktemp -d` then creates its directory under that E: folder.)
> - **Bash tool timeout**: pass `timeout: 600000` on every call that runs `tools/test_hooks.sh`,
>   the full `tools/tests` unittest discovery, `dotnet build` or `dotnet test`.
>
> **Shell**: every command in this plan is written for the **Bash tool** (Git Bash). Write any
> multi-line file with the Write or Edit tool, never a Bash heredoc. Never spell `python3`; use
> `python`.
>
> **The live gates see your commands.** Your session's hooks are the main tree's (pre-027) hooks.
> Never type a force push to `bannerlord-1.5.x`, `bannerlord-1.4.5`, `master` or `main` in a Bash
> command line, even quoted: the live `validate-push.sh` refuses quoted text by design. A force
> push with no refspec or a placeholder such as `<trunk>` counts too: the live gate resolves it
> to the main tree's branch, which is a trunk. All such
> text belongs in files (written with Write or Edit, which no git gate reads). To run a hook by hand
> on a payload, Write the payload JSON to the scratch directory and run
> `bash .claude/hooks/<hook>.sh < /e/repos/taom-improve/scratch/plans/027/<file>.json`.
>
> **Drift check (run first)**:
> `cd /e/repos/taom-improve/wt-027 && git diff --stat 96afb6fb..HEAD -- .claude/settings.json .claude/hooks tools/test_hooks.sh tools/tests/test_shellwords.py docs/reference/hooks-catalog.md docs/reference/mcp-servers.md .claude/rules/hook-authoring.md .claude/rules/harness-facts.md`
> Expected: no output. Then `git status --short` must show at most ` M .claude/settings.json`
> (the orchestrator's Step 0 edit) and `?? plans/027-powershell-gate-coverage.md` (this file). If
> the diff prints anything, or `git status` shows any other path, STOP: the excerpts below are then
> stale.

## Status

- **Priority**: P2
- **Effort**: L (one new Python reader and its unit tests, one bash helper, nine hook scripts, one
  harness section plus rows in five others, four docs)
- **Risk**: MED. One shared reader now feeds nine gates, so a bug in it touches them all. It is
  bounded three ways: for Bash text the reader changes only a git named by a path or in capitals,
  every existing `tools/test_hooks.sh` row must stay green, and a reader failure falls back to the
  raw command read as Bash text, which is exactly how every gate read it before this plan. The
  reader does not bound `validate-push.sh`, the only force-push guard: that gate also changes how
  it treats a `#` comment in both shells, so its quote-blind split judges the raw command beside
  the reader's text and drops a comment only per segment, after the split (Step 7c), and 7c rows
  pin shapes it refused before this plan and must keep refusing.
- **Depends on**: none (plans 011, 013 and 020 are merged). It shares `.claude/hooks/*`,
  `tools/test_hooks.sh` and `docs/reference/hooks-catalog.md` with any other hooks plan: never run
  it at the same time as one.
- **Category**: dx / safety gates
- **Planned at**: commit `96afb6fb`, 2026-09-25
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

Claude Code on this machine has two shell tools, Bash and PowerShell (pwsh 7). Eight PreToolUse
gates are registered for the Bash tool only, so the same git command run through the PowerShell
tool skips them: an unlabelled commit, an AI co-author trailer, `--no-verify`, `git reset --hard`
and `git add -A` all run unchecked. Registering them is not enough on its own: every gate parses its
command as Bash text, and measured at `96afb6fb` a correctly labelled PowerShell here-string commit
(`git commit -m @'` ... `'@`) is **denied** by `check-commit-subject-version.sh` (it reads the
subject as `@`), while `& 'C:\Program Files\Git\cmd\git.exe' reset --hard`, `GIT add -A` and
`if ($x) { git stash drop }` pass the confirm gates. Maintainer decision 61 (2026-09-25) chose "a
new plan to cover PowerShell: register the gates for PowerShell too, each gate's parsing checked
for PowerShell syntax, test rows per shell". The plan 011 final convergence review also found
force-push shapes `validate-push.sh` still lets through in either shell (glob and `heads/`
refspecs, braces glued to the command, git by path or in capitals, a push option's value taken as
the remote, a Bash backtick substitution) and one false refusal (a trunk named only in a trailing
`#` comment); they are routed here. When this lands, one `Bash|PowerShell` group registers all nine
git gates, each gate reads a PowerShell command as the Bash text of the same command, and every
shape above has a test row under both tool names.

## Current state

All excerpts are from commit `96afb6fb`, read in the worktree.

### Registrations (`.claude/settings.json`, lines 62 to 126)

```json
    "PreToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          { ... ".claude/hooks/block-no-verify.sh", "timeout": 5 },
          { ... ".claude/hooks/validate-push.sh", "timeout": 5 },
          { ... ".claude/hooks/check-claude-files-tracked.sh", "timeout": 5 },
          { ... ".claude/hooks/check-commit-subject-version.sh", "timeout": 10 },
          { ... ".claude/hooks/block-dangerous-git.sh", "timeout": 5 },
          { ... ".claude/hooks/block-broad-git-add.sh", "timeout": 5 },
          { ... ".claude/hooks/check-moduledata-validation.sh", "statusMessage": ..., "timeout": 60 },
          { ... ".claude/hooks/check-native-dll-crt.sh", "statusMessage": ..., "timeout": 5 },
          { ... ".claude/hooks/check-doc-config-drift.sh", "statusMessage": ..., "timeout": 30 }
        ]
      },
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
      {
        "matcher": "Edit|Write",
```

Printed at planning with
`python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));print([(g['matcher'],[h['command'].rsplit('/',1)[-1] for h in g['hooks']]) for g in d['hooks']['PreToolUse']])"`:

```text
[('Bash', ['block-no-verify.sh', 'validate-push.sh', 'check-claude-files-tracked.sh', 'check-commit-subject-version.sh', 'block-dangerous-git.sh', 'block-broad-git-add.sh', 'check-moduledata-validation.sh', 'check-native-dll-crt.sh', 'check-doc-config-drift.sh']), ('PowerShell', ['validate-push.sh']), ('Edit|Write', ['config-protection.sh']), ('mcp__.*', ['mcp-health-check.sh'])]
```

So the eight Bash-only gates are `block-no-verify.sh`, `check-claude-files-tracked.sh`,
`check-commit-subject-version.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`,
`check-moduledata-validation.sh`, `check-native-dll-crt.sh` and `check-doc-config-drift.sh`.
PostToolUse and PostToolUseFailure already run `mark-verification-run.sh` for both shells (lines
164 to 206). `settings.json` holds 27 registrations across 9 events (14 of them 5 s), plus 5 in
skill frontmatter. It is config-protected: `.claude/hooks/config-protection.sh` refuses an Edit or
Write to any `settings.json`, so **only the orchestrator edits it (Step 0)**. The PowerShell tool's
payload has `"tool_name": "PowerShell"` and the command in `tool_input.command` (EMPIRICAL at
planning: this session's PowerShell tool calls were refused by `validate-push.sh` through the
`PowerShell` matcher).

### How each gate reads its command today

Code blocks in this plan sit at column 0 with the file's own indentation, so an excerpt can be
used as an Edit `old_string` exactly as shown.

None of the nine reads `tool_name` except `validate-push.sh`. Each has a raw-payload prefilter on
its own word first (plan 013), sources `_pybin.sh`, runs the degraded check, then extracts
`tool_input.command`. The words: `push` (validate-push), `no-verify` (block-no-verify), `commit`
(the five commit gates) and `git` (the two confirm gates, `block-dangerous-git.sh:44` and
`block-broad-git-add.sh:54`, case sensitive, so a payload whose only git is `GIT` exits there).
Leave every prefilter exactly as it is, except the two `git` tests, which Step 5h makes read any
case.

**Four gates with a jq fallback** prefer jq when it is on PATH (it is not on this machine; it is on
the Linux CI runner): `block-no-verify.sh:43-55`, `block-dangerous-git.sh:52-65`,
`block-broad-git-add.sh:62-74`, `validate-push.sh:32-47`. The `block-no-verify.sh:43-55` block:

```bash
# Prefer jq; fall back to python3 for robust JSON (handles escaped quotes).
# Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
else
  COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    print(json.loads(sys.stdin.read()).get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
fi
```

`block-dangerous-git.sh` and `block-broad-git-add.sh` carry the same `if/else` under a different
comment (quoted in Step 5).

**Five python-only gates** run the same eight-line extraction:
`check-claude-files-tracked.sh:36-43`, `check-commit-subject-version.sh:51-58`,
`check-moduledata-validation.sh:47-54`, `check-native-dll-crt.sh:42-49`,
`check-doc-config-drift.sh:49-56`:

```bash
COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.read())
    print(d.get("tool_input", {}).get("command", ""))
except Exception:
    pass
' 2>/dev/null)
```

Each of the five then runs the two-stage matcher from `.claude/rules/hook-authoring.md` and only
past it changes directory to the project. Verbatim at `check-moduledata-validation.sh:58-66`:

```bash
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;
    *) echo '{}'; exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" 2>/dev/null || { echo '{}'; exit 0; }
```

The other four differ only in ways this plan does not touch: `check-native-dll-crt.sh:54-56`'s
first `case` is `*"git commit-"* | *"git -"*" commit-"*) echo '{}'; exit 0 ;;`; some `case` lines
carry a trailing comment (`check-claude-files-tracked.sh:48`, `check-commit-subject-version.sh:63`
and `:66`); and the `cd` line sits at `check-commit-subject-version.sh:70`,
`check-claude-files-tracked.sh:61` (after a five-line "DO NOT skip --amend" comment block),
`check-native-dll-crt.sh:62` and `check-doc-config-drift.sh:68`. Step 4's commit-test rows rely
only on the `cd` coming after the second `esac`, which holds in all five.

- `block-no-verify.sh:61-66` then needs a `git` word and the text `--no-verify`:
  `[[ ! "$COMMAND" =~ (^|[[:space:]])git([[:space:]]|$) ]] && exit 0`.
- `block-dangerous-git.sh:74` and `block-broad-git-add.sh:83` split with
  `SEGMENTS=$(printf '%s' "$COMMAND" | sed -E 's/&&|\|\||;|\|/\n/g')` and judge a segment only when
  it matches `^git([[:space:]]|$)` after an env-prefix strip.
- `check-commit-subject-version.sh:75-317` passes `COMMAND` to an embedded Python parser (a
  `<<'PY'` heredoc opened at line 75 and closed by the `PY` line at 317; heredoc bodies stashed as
  `__TAOM_HEREDOC_<n>__` placeholders in the list `bodies`, `$'...'` decoded, `shlex` tokens,
  `bash -c` recursion). `commit_arg_lists` sits at lines 128 to 139, at column 0 inside the
  heredoc:

```python
def commit_arg_lists(s, depth=0):
    """The argument list after `commit` of every git commit invocation in s."""
    found = []
    group = []
    for t in tokens(s) + [";"]:
        if t and set(t) <= SEP:
            if group:
                found.extend(_commit_args(group, depth))
            group = []
        else:
            group.append(t)
    return found
```

- `parse_commit` (defined at line 164) already reads `<<` followed by a placeholder as the
  command's stdin at lines 178 to 181 (`got["stdin"] = expand(args[i + 1])`), which is what `-F -`
  uses.
- `check-claude-files-tracked.sh:84` only reports `.md`, `.sh`, `.json` and `.yml`/`.yaml` files:
  `} | grep -E '\.(md|sh|json|ya?ml) \(' | sed 's/^/  - /'`. This plan adds the first `.py` file
  under `.claude/hooks/`.
- The file header comment of each of the eight says `PreToolUse(Bash)` (line 3; line 2 in
  `validate-push.sh`, which already says "Bash and PowerShell").

### Measured at `96afb6fb` (each gate fed a payload on stdin, Git Bash, the worktree as project)

| Gate | Tool | Command | Result today | Correct |
|---|---|---|---|---|
| check-commit-subject-version | PowerShell | `git commit -m @'`, `feat(hooks): v2.0.30 - here-string subject`, blank, `Body.`, `'@` (four lines) | **deny** | allow |
| check-commit-subject-version | PowerShell | `git commit -m "docs: v2.0.30 - x" -m "Body` + backtick-n backtick-n + `Co-Authored-By: Claude <noreply@anthropic.com>"` | allow | deny |
| check-commit-subject-version | PowerShell | `GIT commit -m "docs: no label"` | allow | deny |
| check-commit-subject-version | PowerShell | `if ($true) { git commit -m "docs: no label" }` | allow | deny |
| check-commit-subject-version | Bash | `echo "docs: no label" \| git commit -F -` | allow | deny |
| block-dangerous-git | PowerShell | `& git reset --hard` | allow | ask |
| block-dangerous-git | PowerShell | `if ($true) { git stash drop }` | allow | ask |
| block-dangerous-git | PowerShell | `git reset`, a trailing backtick, newline, `--hard` | allow | ask |
| block-broad-git-add | PowerShell | `& git add -A`, `GIT add .` | allow | ask |
| block-no-verify | PowerShell | `GIT commit --no-verify -m x`; `& 'C:\Program Files\Git\cmd\git.exe' commit --no-verify -m x` | rc 0 | rc 2 |
| validate-push | Bash | `git push --force origin 'refs/heads/*'`; `git push origin '+refs/heads/*:refs/heads/*'`; `git push --force origin 'refs/heads/bannerlord-*'`; `git push --force origin HEAD:heads/bannerlord-1.5.x`; `GIT push --force origin bannerlord-1.5.x`; ``x=`git push --force origin bannerlord-1.5.x` `` | rc 0 | rc 2 |
| validate-push | PowerShell | `if ($true) {git push --force origin bannerlord-1.5.x}`; `& 'C:\Program Files\Git\cmd\git.exe' push --force origin bannerlord-1.5.x`; `GIT push --force origin bannerlord-1.5.x`; `git push --force origin bannerlord-1.5`, backtick, `.x` | rc 0 | rc 2 |
| validate-push | both | `git push --force origin feature # bannerlord-1.5.x later` | **rc 2** | rc 0 |

The plan 011 probe also showed (scratch bare repo, git 2.55.0) that each glob or `heads/` shape
above really force-updated the remote branches, and that `git push --force -o ci.skip origin` run
on a trunk passes because `ci.skip` is taken as the remote (`validate-push.sh:188-196`).

Git facts measured at planning (`git version 2.55.0.windows.4`): `git COMMIT -h` fails with
"'COMMIT' is not a git command" (subcommands are case sensitive); `GIT --version` works in Git Bash
(the command name is not); `git commit --no-veri -h` prints the usage with no "unknown option"
error (an abbreviated `--no-verify` is accepted; see Out of scope).

### PowerShell syntax the gates must read (from PowerShell 7's own parser)

Read at planning with `[System.Management.Automation.Language.Parser]::ParseInput(...)` and the
`CommandAst` elements of each input:

| PowerShell input | What git (or the command) receives |
|---|---|
| `git commit -m @'` newline `feat: v2.0.30 - x` newline newline `body` newline `'@` | `-m` then one argument `feat: v2.0.30 - x\n\nbody` (the closing `'@` must start a line) |
| `@"` ... `"@` holding `$x` and a backtick before `$y` | backtick escapes applied; `$x` expands (this plan keeps it as text) |
| `-m "a<BT>nb"`, where `<BT>` stands for one backtick character | `a\nb` (backtick is the escape; after it `n` `t` `r` `0` `a` `b` `e` `f` `v` `u{hex}` are special, any other character is itself) |
| `'it''s'`, `"a""b"` | `it's`, `a"b` |
| `git -C "E:\R&D\" push ...` | `E:\R&D\` (backslash is literal) |
| `git commit --no-verif''y -m x` | `--no-verify` (adjacent quoted pieces join) |
| `git push ...` then a trailing backtick, newline, `--force origin z` | one command |
| `if ($true) {git push --force origin T}` and `1..1 \| ForEach-Object {git push --force origin T}` | a separate `git push --force origin T` command |
| `$(git push --force origin x)` | a separate `git push` command |
| `& 'C:\Program Files\Git\cmd\git.exe' push ...`, `&git status` | the call operator runs the named program |
| `git status & git log` | `&` after a word runs `git status` in the background, then `git log` |
| `git push --force origin feature # T later` | the comment is dropped; `<# c #>` block comments too |
| `git push --force origin trunkx 2>&1 \| Out-Null` | a redirection `2>&1`, not an argument |
| `git push --force origin trunkx*>$null` (glued) | one argument `trunkx*>$null` (no redirection) |
| `git commit-tree HEAD^{tree} -m "x"` | `HEAD^` then a script block: braces split even inside a word |
| `git log --% --format=%H; git status` | stop-parsing: the rest of the line is passed verbatim |
| `@'` ... `'@ \| git commit -F -` | the here-string is piped to git's stdin |

### `validate-push.sh` (the parts this plan changes)

```bash
# validate-push.sh:54-59
is_protected() {
  case "$1" in
    master|main|bannerlord-1.4.5|bannerlord-1.5.x) return 0 ;;
    *) return 1 ;;
  esac
}

# validate-push.sh:83-121 (the quote-aware splitter at 88-118 is the same Python as
# mark-verification-run.sh:56-86, with the escape picked from tool_name)
COMMAND=${COMMAND//$'\r'/}
COMMAND=${COMMAND//$'\\\n'/ }
COMMAND=${COMMAND//$'`\n'/ }
QSEGS=$COMMAND
if [ -n "$PYBIN" ]; then
  Q=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
...
    esc = "`" if d.get("tool_name") == "PowerShell" else "\\"
...
' 2>/dev/null)
  [ -n "$Q" ] && QSEGS=$Q
fi
COMMAND=${COMMAND//[;&|]/$'\n'}

# validate-push.sh:140-155 (inside judge_command)
  CLEAN=${1//[\"\'()]/ }
  read -r -a TOKENS <<< "$CLEAN"
  ...
  for i in "${!TOKENS[@]}"; do
    case "${TOKENS[$i]}" in
      git | */git | git.exe | */git.exe) GIT_SEEN=1 ;;
      push) if (( GIT_SEEN )); then PUSH_IDX=$i; break; fi ;;
    esac
  done

# validate-push.sh:173-183 (option loop)
    case "$tok" in
      --force | --force-with-lease | --force-with-lease=* | --force-if-includes | -f)
        FORCE=true; continue ;;
      --all | --branches) ALL=true; continue ;;
      --mirror) ALL=true; FORCE=true; continue ;;   # --mirror force-updates every ref
      -*f | -f*)
        # A bundled short flag such as -fu. Still a force push.
        case "$tok" in --*) ;; *) FORCE=true ;; esac
        continue ;;
      -*) continue ;;
    esac

# validate-push.sh:205-220 (refspec loop)
  for ref in "${REFS[@]}"; do
    f=$FORCE
    case "$ref" in +*) f=true; ref="${ref#+}" ;; esac
    ref="${ref##*:}"
    ref="${ref#refs/heads/}"
    if [[ -z "$ref" || "$ref" == "HEAD" || "$ref" == "@" ]]; then
      # Asked once per run: ...
      [[ -n ${CUR_BRANCH+x} ]] || CUR_BRANCH=$(git branch --show-current 2>/dev/null)
      ref=$CUR_BRANCH
    fi
    if is_protected "$ref"; then
      if [[ "$f" == true ]]; then BLOCK_TARGET="$ref"; return 0; fi
      WARN_TARGET="$ref"
    fi
  done
```

`HEAD`, `@` or no refspec resolves to the branch checked out in the hook's cwd (the main tree for
a live call; the directory the test runs it in for `tools/test_hooks.sh`). Decisions that bind it:
**decision 30**, the protected names are exactly `master`, `main`, `bannerlord-1.4.5`,
`bannerlord-1.5.x` (never a `bannerlord-*` pattern); **decision 60**, it stays force-push only (a
trunk deletion is not blocked).

### `mark-verification-run.sh` and the `is_interrupt` question

`mark-verification-run.sh:20` prefilters on `dotnet` or `build.ps1`, lines 54 to 87 run the same
quote-aware splitter as `validate-push.sh`, and lines 101 to 129 touch
`.claude/logs/.verification-ran` when a segment starts with `dotnet build`, `dotnet test` or a
`build.ps1` call, on PostToolUse and PostToolUseFailure alike (a failed build is still evidence).

Evidence read at planning from the installed Claude Code 2.1.241 binary
(`grep -a -o '.\{0,300\}is_interrupt.\{0,300\}' /c/Users/mikew/.local/bin/claude.exe`; minified
names change between versions):

- The PostToolUseFailure input schema:
  `hook_event_name:Tt("PostToolUseFailure"),tool_name:L(),tool_input:Dn(),tool_use_id:L(),error:L(),is_interrupt:Ut().optional(),duration_ms:Xe().optional()`,
  and the builder sends
  `hook_event_name:"PostToolUseFailure",tool_name:e,tool_input:r,tool_use_id:t,error:n,is_interrupt:i,duration_ms:c`.
  The event's description string names `error_type` and `is_timeout` as well, but 2.1.241 sends
  neither.
- The value is `de||se` at the call site `Yoo(n,e,t,s,w,ge,de||se,...)`, where `let de=urn(X)`,
  `function urn(e){return zl(e)||$v(e)}` and `zl` is true for an `AbortError` or a cancel error, and
  `let se=E3e(n.abortController.signal)` with
  `function E3e(e){return e.aborted&&KE(e.reason)===osi}`, `osi="server-fallback-tombstone"`.

So `is_interrupt` is true when the tool call was aborted: the command did not finish and there is
no result, so marking would mute the verification reminder with nothing in hand. **Decision taken
by this plan:** a PostToolUseFailure payload with `"is_interrupt": true` does not mark. It is safe
whatever the live behaviour: if the field is never true nothing changes, and if it is, the only
effect is one more Stop reminder. A timed-out command: the shell path appends
`Command timed out after ...` to stderr, and whether that also counts as an abort is **UNVERIFIED**,
so a timeout keeps marking as today; record it as UNVERIFIED in the docs.

### Test harness conventions

- `tools/test_hooks.sh` (1,377 lines) is the hook contract suite; its baseline at `96afb6fb` is
  **482 passed, 0 failed** (the orchestrator's run, repeated at plan revision with TMP, TEMP and
  TMPDIR on E:; a check-8 `scan.sh` timeout or a `took <N>ms` timing row is a known flake under
  load: rerun once). Passing rows print only with `--verbose`. Helpers: `ok`, `bad`, `head2`, `decision_of` (prints `allow`, `deny`, `ask`,
  `invalid` or `BADSHAPE`), `$HPY` (the harness's Python), `$SANDBOX` (a `mktemp -d` directory),
  `$REPO` (the checkout; the script `cd`s there). Sections that matter here: 2 (every registration
  has a timeout; it prints "all N registrations have a timeout"), 4 (contract: every hook, every
  payload in `PAYLOADS`, lines 349 to 356), 4b (each PreToolUse gate answers a commit inside 80% of
  its registration), 4c (prefilter: `pf_payload` at 444 to 446 hardcodes `"tool_name":"Bash"`,
  `pf_run` counts `source _pybin.sh` lines in a `bash -x` trace), 4d (escaped words), 5 and 5b
  (fail open, never silent), 6 (`CSV_CASES`, lines 828 to 903, run against the real
  `SubModule.xml`; `CSV_VER`, `CSV_MSGFILE`, `CSV_BADFILE` defined at 819 to 826), 7b
  (`check-claude-files-tracked`, lines 1122 to 1150), 7c (`VP_CASES` at 1159, `vp_run` at 1226,
  `VP_TOOL_CASES` at 1244, 100-line timing at 1257, registration check at 1262), 7d (`MVR_CASES`
  at 1288, `MVR_TOOL_CASES` at 1317, the PostToolUseFailure row at 1342 to 1349), 8 (scan.sh, from
  line 1351).
- A matcher is a regex: section 4c's discovery uses `re.fullmatch(matcher, 'Bash')`, section 5
  tests `'Bash' in matcher`, 7c splits on `|`. All three already accept `Bash|PowerShell`
  (PostToolUseFailure uses that matcher at `settings.json:197`).
- `tools/tests/*.py` are stdlib `unittest` modules that insert their import path themselves (model:
  `tools/tests/test_bind_hill_troll_action_set.py:15-17`). CI runs
  `python3 -m unittest discover -s tools/tests -t .` (`.github/workflows/build.yml:205`) and
  `bash tools/test_hooks.sh` (line 178) on ubuntu, where jq IS on PATH.
- Baseline of the unittest discovery in the worktree (run at planning, and again at plan revision
  with the temp variables on E:): **Ran 2126 tests, FAILED
  (failures=2)**, the two being
  `test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op` and
  `test_generate_career_kits.TestCommittedCareerFile.test_the_committed_career_file_is_what_the_rule_derives`
  (live-data tests, unrelated to hooks).
- `python tools/audit_claude_config.py --min HIGH --no-repo-secrets` at planning: "118 files
  scanned", "No findings at or above the requested severity", exit 0.

### Binding rules and decisions (one line each)

- **Maintainer decision 61**: cover the PowerShell tool: register the gates for it, check each
  gate's parsing for PowerShell syntax, add test rows per shell.
- **Decisions 30 and 60**: see validate-push above. **Decisions 39 and 40** (plan 013): each gate's
  raw prefilter stays on its own word, and any `\u` escape takes the full parse; do not touch them
  beyond Step 5h, which lets the two confirm gates' `git` test match any case (still their word).
- **`.claude/rules/hook-authoring.md`**: mirror a sibling's full convention set; fail open but
  never fail silent; bound slow work inside the hook and time the slow path; the two-stage
  git-commit matcher; prove a gate live.
- **`.claude/rules/harness-facts.md`**: PreToolUse decisions go under `hookSpecificOutput`; TAOM
  hooks fail open on their own errors; a timed-out hook is killed and its output discarded.
- **ADR-011** (`docs/adrs/011-knowledge-delivery-tiers.md`): durable knowledge goes to the owning
  doc (`hooks-catalog.md`, the path-scoped rules), never as uncomputed counts in always-loaded text.
- **ADR-002, ADR-007, ADR-008**: thin C# entry points, adapters for TaleWorlds types, test coverage
  for C#. **Not applicable**: this plan changes no C#. No engine signature is involved.
- **`.claude/rules/simplicity-criterion.md`**: win, one reader lets nine gates read both shells and
  also fixes git by path or in capitals in Bash; cost, a 290-line Python helper and a 20-line bash
  function. Keep: the improvement dominates, and the trade-off goes in the commit body.
- **AGENTS.md**: no em or en dash in prose (commit bodies and docs); no AI attribution trailer;
  stage explicit paths.

## Commands you will need

Prefix each with `cd /e/repos/taom-improve/wt-027 && `, and the hook suite, the unittest
discovery, build and test also with `. /e/repos/taom-improve/scratch/plans/027/tmpenv.sh && `
(Step 1 writes that file).

| Purpose | Command | Expected on success |
|---|---|---|
| Hook suite | `bash tools/test_hooks.sh` | last lines `Summary` then `N passed, 0 failed`, exit 0 |
| Reader unit tests | `python -m unittest tools.tests.test_shellwords -v` | `Ran 33 tests`, `OK` |
| All tool tests | `python -m unittest discover -s tools/tests -t .` | `Ran 2159 tests`, `FAILED (failures=2)`, only the two baseline failures |
| Docs | `python tools/lint_docs.py` | exit 0 |
| Doc drift gate | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Config security | `python tools/audit_claude_config.py --min HIGH --no-repo-secrets` | `No findings at or above the requested severity`, exit 0 |
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Passed 10767, Failed 0, Skipped 2, Total 10769` |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | not needed (no C#) |
| Data | `python tools/validate_moduledata.py` | not needed (no ModuleData change) |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify, all under the worktree):

- `.claude/hooks/_shellwords.py` (new): the shared reader.
- `tools/tests/test_shellwords.py` (new): its unit tests.
- `.claude/hooks/_pybin.sh`: one comment line (80) and a new helper `taom_hook_command` at the end.
- The eight gates: `.claude/hooks/block-no-verify.sh`, `check-claude-files-tracked.sh`,
  `check-commit-subject-version.sh`, `block-dangerous-git.sh`, `block-broad-git-add.sh`,
  `check-moduledata-validation.sh`, `check-native-dll-crt.sh`, `check-doc-config-drift.sh`.
- `.claude/hooks/validate-push.sh`, `.claude/hooks/mark-verification-run.sh`.
- `tools/test_hooks.sh`.
- Docs: `docs/reference/hooks-catalog.md`, `.claude/rules/hook-authoring.md`,
  `.claude/rules/harness-facts.md`, `docs/reference/mcp-servers.md` (one sentence).
- `.claude/settings.json`: **edited by the orchestrator in Step 0 only**. You stage and commit it
  (Step 5) but never edit it; if it needs any other change, STOP.

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props` (single-owner;
  nothing here needs them) and every other C# file.
- `CHANGELOG.md` (generated at `/release` since plan 020; the commit bodies are the entry) and
  `plans/README.md` (orchestrator).
- Blocking a trunk deletion (decision 60) or widening the protected names (decision 30).
- Merging the two PostToolUse groups for `mark-verification-run.sh` (cosmetic).
- Pre-existing gaps in both shells, deferred with evidence (see Maintenance notes):
  `git commit -n` and an abbreviated `--no-veri`; the commit gates' `*"git commit-"*` short
  circuit, which lets `git commit-graph write && git commit -m "no label"` through; a quoted `-C`
  path holding a space in the two confirm gates; a Bash `\`-newline continuation, `(git ...)` or
  `"$(git ...)"` in the two confirm gates; a gated word spelled with an escape inside it
  (`com''mit` in Bash, a backtick inside `commit` in PowerShell, `--no-verif''y` in PowerShell,
  which 7e pins as `rc=0 allow`), which the raw prefilter cannot see: the gates stop accidents,
  not deliberate obfuscation.
- The `if:` handler field migration (deferred in `harness-facts.md`).
- A live proof in the real harness: your session runs the main tree's hooks, so it is owed after
  merge (Maintenance notes).

## Git workflow

- Branch: `improve/027-powershell-gate-coverage`, already checked out in
  `E:/repos/taom-improve/wt-027`. Do not create another branch or worktree. **Never push.**
- Subject format: `<type>(<scope>): v2.0.30 - <description>`, at most 72 characters (the version is
  `<Version value="v2.0.30" />` in `Main/_Module/SubModule.xml:6`), body wrapped at 72, no
  `Co-Authored-By` or any AI attribution line, no em or en dash in the body.
- Write each message to a file with the Write tool, e.g.
  `E:/repos/taom-improve/scratch/plans/027/msg-1.txt`, and commit with
  `git commit -F /e/repos/taom-improve/scratch/plans/027/msg-1.txt`. Stage explicit paths only
  (`git add <path> <path>`), never `-A`, `-u`, `.` or `commit -a`.
- The five commits, each made only at a green boundary (the step that ends in it lists the paths):
  1. `feat(hooks): v2.0.30 - read PowerShell commands as Bash text` (60 characters)
  2. `feat(hooks): v2.0.30 - run the eight git gates for the PowerShell tool` (70)
  3. `fix(hooks): v2.0.30 - close validate-push glob, option and comment gaps` (71)
  4. `fix(hooks): v2.0.30 - leave an interrupted build or test unmarked` (65)
  5. `docs(hooks): v2.0.30 - catalogue the PowerShell gate coverage` (61)
- Optional trailers: `Not-tested:` (see each step), `Constraint:`, `Rejected:`.

## Steps

### Step 0 (orchestrator, before dispatch): register the eight gates for PowerShell

The orchestrator applies this to `E:/repos/taom-improve/wt-027/.claude/settings.json` and leaves it
uncommitted (the executor commits it in Step 5, together with the parsing, so no commit registers
a gate that cannot read PowerShell):

1. Line 64: `"matcher": "Bash",` becomes `"matcher": "Bash|PowerShell",` (the first PreToolUse
   group, the one holding the nine hooks).
2. Delete lines 116 to 125, the whole PreToolUse group whose matcher is exactly `PowerShell`
   (keeping it would register `validate-push.sh` twice for PowerShell). The ten lines, verbatim:

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

Nothing else changes; the PostToolUse and PostToolUseFailure groups stay as they are.

**Verify** (executor, first thing):
`cd /e/repos/taom-improve/wt-027 && python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));print([(g['matcher'],[h['command'].rsplit('/',1)[-1] for h in g['hooks']]) for g in d['hooks']['PreToolUse']])"`
prints exactly
`[('Bash|PowerShell', ['block-no-verify.sh', 'validate-push.sh', 'check-claude-files-tracked.sh', 'check-commit-subject-version.sh', 'block-dangerous-git.sh', 'block-broad-git-add.sh', 'check-moduledata-validation.sh', 'check-native-dll-crt.sh', 'check-doc-config-drift.sh']), ('Edit|Write', ['config-protection.sh']), ('mcp__.*', ['mcp-health-check.sh'])]`
and `git diff --stat .claude/settings.json` shows `1 file changed, 1 insertion(+), 11 deletions(-)`.
If either differs, STOP.

### Step 1: Drift check and baseline

Run the drift check at the top of this plan. Then Write
`E:/repos/taom-improve/scratch/plans/027/tmpenv.sh` with exactly these two lines:

```bash
mkdir -p /e/repos/taom-improve/scratch/plans/027/tmp
export TMPDIR=E:/repos/taom-improve/scratch/plans/027/tmp TMP=E:/repos/taom-improve/scratch/plans/027/tmp TEMP=E:/repos/taom-improve/scratch/plans/027/tmp
```

Check it: `cd /e/repos/taom-improve/wt-027 && . /e/repos/taom-improve/scratch/plans/027/tmpenv.sh && d=$(mktemp -d) && echo "$d" && rmdir "$d"`
prints a path starting `E:/repos/taom-improve/scratch/plans/027/tmp/`. Any other path: STOP.

Then run these and save each output to the scratch directory. The hook suite runs once here with
`--verbose`, because it prints passing rows (the section 2 line below is one) only then:
`cd /e/repos/taom-improve/wt-027 && . /e/repos/taom-improve/scratch/plans/027/tmpenv.sh && bash tools/test_hooks.sh --verbose > /e/repos/taom-improve/scratch/plans/027/base-hooks.txt 2>&1; grep -a -E '[0-9]+ passed, [0-9]+ failed|registrations have a timeout' /e/repos/taom-improve/scratch/plans/027/base-hooks.txt`

- The hook suite: expect `all 29 registrations have a timeout` and `482 passed, 0 failed`. The
  29 is 26 in `settings.json` after Step 0 plus 3 frontmatter registrations: section 2's
  frontmatter scan misses the last command block of each skill, so it counts 3 of the 5 (measured
  at planning: `all 30 registrations have a timeout` before Step 0, with 27 in `settings.json`;
  the undercount is pre-existing and out of scope). If
  only load-sensitive rows fail (a check-8 `scan.sh` `exit 124`, or a `took <N>ms` timing row),
  rerun once with nothing else running on the machine: at planning, a run beside other CPU work
  gave `478 passed, 4 failed`, all four of that kind. If the count is not 482 but 0 failed, record the
  number and use it as your baseline in the arithmetic below.
- `python tools/audit_claude_config.py --min HIGH --no-repo-secrets`: expect no findings, exit 0.
- `python tools/lint_docs.py --fail-on-drift`: expect exit 0.

**Verify**: 0 failed in the hook suite; the other two exit 0. Any failure: STOP.

### Step 2 (RED): unit tests for the reader

Create `E:/repos/taom-improve/wt-027/tools/tests/test_shellwords.py` with exactly this content:

```python
"""_shellwords.py (plan 027): a PowerShell tool command read as the Bash text of the same command,
git named by a path or in capitals read as `git`, and the quote-aware split the hooks share. The
PowerShell argument lists below were read from PowerShell 7's own parser
([System.Management.Automation.Language.Parser]::ParseInput) when the plan was written."""
import json
import os
import shlex
import subprocess
import sys
import unittest

HOOKS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".claude", "hooks")
sys.path.insert(0, HOOKS)
sys.dont_write_bytecode = True  # no __pycache__ folder inside .claude/hooks
import _shellwords as sw  # noqa: E402

BT = "`"  # PowerShell's escape character


def argvs(posix_text):
    """Each simple command of POSIX-shell text as its argument list, split at ; & | and newlines."""
    lexer = shlex.shlex(posix_text, posix=True, punctuation_chars=";&|\n")
    lexer.whitespace = " \t"
    lexer.whitespace_split = True
    commands, current = [], []
    for token in lexer:
        if token and set(token) <= set(";&|\n"):
            if current:
                commands.append(current)
            current = []
        else:
            current.append(token)
    if current:
        commands.append(current)
    return commands


def ps(command):
    return argvs(sw.to_posix(command, "PowerShell"))


def run_cli(mode, stdin):
    return subprocess.run([sys.executable, os.path.join(HOOKS, "_shellwords.py"), mode],
                          input=stdin, capture_output=True, timeout=30)


class PowerShellQuotingTests(unittest.TestCase):
    def test_single_quotes_double_an_apostrophe(self):
        self.assertEqual(ps("git commit -m 'it''s'"), [["git", "commit", "-m", "it's"]])

    def test_double_quotes_take_backtick_escapes(self):
        self.assertEqual(ps('git commit -m "a' + BT + 'nb"'), [["git", "commit", "-m", "a\nb"]])

    def test_double_quotes_double_a_quote(self):
        self.assertEqual(ps('git commit -m "a""b"'), [["git", "commit", "-m", 'a"b']])

    def test_backslash_is_literal(self):
        self.assertEqual(ps('git -C "E:\\R&D\\" fetch origin y'),
                         [["git", "-C", "E:\\R&D\\", "fetch", "origin", "y"]])

    def test_here_string_is_one_word(self):
        self.assertEqual(ps("git commit -m @'\nfeat: v2.0.30 - x\n\nbody\n'@"),
                         [["git", "commit", "-m", "feat: v2.0.30 - x\n\nbody"]])

    def test_expandable_here_string_takes_escapes_and_keeps_variables(self):
        self.assertEqual(ps('git commit -m @"\nfeat: $x ' + BT + '$y\n"@'),
                         [["git", "commit", "-m", "feat: $x $y"]])

    def test_quoted_pieces_join_into_one_word(self):
        self.assertEqual(ps("git commit --no-verif''y -m x"),
                         [["git", "commit", "--no-verify", "-m", "x"]])

    def test_bare_backtick_escape(self):
        self.assertEqual(ps("git fetch origin trunk-1.5" + BT + ".x"),
                         [["git", "fetch", "origin", "trunk-1.5.x"]])

    def test_unreadable_text_comes_back_unchanged(self):
        for text in ("git commit -m 'unclosed", "git commit -m @'\nno end", "git status <# no end"):
            with self.subTest(text=text):
                self.assertEqual(sw.to_posix(text, "PowerShell"), text)


class PowerShellStatementTests(unittest.TestCase):
    def test_backtick_continuation_joins_lines(self):
        self.assertEqual(ps("git fetch " + BT + "\n --prune origin z"),
                         [["git", "fetch", "--prune", "origin", "z"]])

    def test_comment_is_dropped(self):
        self.assertEqual(ps("git fetch origin feature # trunk later"),
                         [["git", "fetch", "origin", "feature"]])

    def test_block_comment_is_dropped(self):
        self.assertEqual(ps("git reset --hard <# c #> HEAD"), [["git", "reset", "--hard", "HEAD"]])

    def test_braces_and_parentheses_separate_commands(self):
        self.assertEqual(ps("if ($true) {git fetch origin t}"),
                         [["if"], ["$true"], ["git", "fetch", "origin", "t"]])
        self.assertEqual(ps("1..1 | ForEach-Object {git fetch origin t}"),
                         [["1..1"], ["ForEach-Object"], ["git", "fetch", "origin", "t"]])
        self.assertEqual(ps("$(git fetch origin x)"), [["git", "fetch", "origin", "x"]])

    def test_statement_separators(self):
        self.assertEqual(ps('git add -A && git commit -am "x" || echo no'),
                         [["git", "add", "-A"], ["git", "commit", "-am", "x"], ["echo", "no"]])

    def test_call_operator_is_dropped(self):
        self.assertEqual(ps("&git status"), [["git", "status"]])
        self.assertEqual(ps("x = 1; & git log"), [["x", "=", "1"], ["git", "log"]])

    def test_ampersand_after_a_word_runs_in_the_background(self):
        self.assertEqual(sw.to_posix("git status & git log", "PowerShell"), "git status & git log")

    def test_redirection_stays_an_operator(self):
        self.assertEqual(sw.to_posix("git fetch origin x 2>&1 | Out-Null", "PowerShell"),
                         "git fetch origin x 2>&1 | Out-Null")
        self.assertEqual(sw.to_posix("git status 2>$null", "PowerShell"), "git status 2> '$null'")

    def test_glued_redirection_stays_in_the_word(self):
        # PowerShell's parser hands git `trunkx*>$null` as one argument.
        self.assertEqual(ps("git fetch origin trunkx*>$null"),
                         [["git", "fetch", "origin", "trunkx*>$null"]])

    def test_stop_parsing_keeps_the_rest_of_the_line(self):
        self.assertEqual(ps("git log --% --format=%H; git status"),
                         [["git", "log", "--%", "--format=%H;", "git", "status"]])

    def test_braces_inside_a_word_split_it(self):
        # PowerShell reads HEAD^{tree} as HEAD^ followed by a script block.
        self.assertEqual(ps('git commit-tree HEAD^{tree} -m "x"')[0], ["git", "commit-tree", "HEAD^"])


class GitWordTests(unittest.TestCase):
    def test_powershell_git_by_path_or_capitals(self):
        self.assertEqual(ps("& 'C:\\Program Files\\Git\\cmd\\git.exe' fetch origin x"),
                         [["git", "fetch", "origin", "x"]])
        self.assertEqual(ps("GIT commit -m x"), [["git", "commit", "-m", "x"]])
        self.assertEqual(ps("& git.exe -C 'E:\\a b' add -A"), [["git", "-C", "E:\\a b", "add", "-A"]])

    def test_powershell_git_word_only_as_the_command(self):
        self.assertEqual(ps("echo GIT commit"), [["echo", "GIT", "commit"]])
        self.assertEqual(ps("git log -- C:\\x\\git.exe"), [["git", "log", "--", "C:\\x\\git.exe"]])

    def test_bash_git_by_path_or_capitals(self):
        for before, after in (("GIT fetch origin x", "git fetch origin x"),
                              ("cd /x && GIT commit -m 'a'", "cd /x && git commit -m 'a'"),
                              ('"/c/Program Files/Git/cmd/git.exe" reset --hard', "git reset --hard"),
                              ("/mingw64/bin/git add -A", "git add -A"),
                              ("  Git.EXE status", "  git status"),
                              ("x=$(GIT rev-parse HEAD)", "x=$(git rev-parse HEAD)"),
                              ("(GIT stash drop)", "(git stash drop)"),
                              ("git status\nGIT reset --hard", "git status\ngit reset --hard")):
            with self.subTest(before=before):
                self.assertEqual(sw.to_posix(before, "Bash"), after)

    def test_bash_text_is_otherwise_unchanged(self):
        for text in ("echo GIT commit", "git commit -m 'GIT commit'", "GIT_TRACE=1 git fetch",
                     "git log -- .git", "legit status", "git commit -F - <<'EOF'\nDon't stop yet\nEOF",
                     'bash -c "git fetch origin x"', "git commit -m $'a\\nb'"):
            with self.subTest(text=text):
                self.assertEqual(sw.to_posix(text, "Bash"), text)


class SegmentsTests(unittest.TestCase):
    def test_split_outside_quotes_only(self):
        self.assertEqual(sw.segments('git -C "E:/R&D" fetch; git status'),
                         'git -C "E:/R&D" fetch\n git status')

    def test_newline_inside_quotes_becomes_a_space(self):
        self.assertEqual(sw.segments('git commit -m "a\nb"'), 'git commit -m "a b"')

    def test_escaped_newline_joins(self):
        self.assertEqual(sw.segments("git fetch \\\n origin x"), "git fetch   origin x")

    def test_comment_drops_the_rest_of_its_line(self):
        self.assertEqual(sw.segments("git fetch origin feature # trunk later\necho a#b; x=${#y}"),
                         "git fetch origin feature \necho a#b\n x=${#y}")

    def test_powershell_text_is_split_after_posix(self):
        text = sw.to_posix('Set-Location "E:\\repos\\TAOM\\"; dotnet test', "PowerShell")
        self.assertEqual(sw.segments(text), "Set-Location 'E:\\repos\\TAOM\\' \n dotnet test")


class CliTests(unittest.TestCase):
    def test_posix_mode_writes_utf8_with_lf_only(self):
        payload = json.dumps({"tool_name": "PowerShell",
                              "tool_input": {"command": "GIT commit -m @'\nfeat: x \u2192 y\n'@"}})
        r = run_cli("posix", payload.encode("ascii"))
        self.assertEqual(r.returncode, 0)
        self.assertEqual(r.stdout, "git commit -m 'feat: x \u2192 y'\n".encode("utf-8"))
        self.assertNotIn(b"\r", r.stdout)

    def test_segments_mode(self):
        payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": "a; b"}})
        self.assertEqual(run_cli("segments", payload.encode("ascii")).stdout, b"a\n b\n")

    def test_bad_json_prints_an_empty_line(self):
        r = run_cli("posix", b'{"tool_name":')
        self.assertEqual((r.returncode, r.stdout), (0, b"\n"))

    def test_unknown_mode_exits_2(self):
        self.assertEqual(run_cli("bogus", b"{}").returncode, 2)


if __name__ == "__main__":
    unittest.main()
```

**Verify**: `cd /e/repos/taom-improve/wt-027 && python -m unittest tools.tests.test_shellwords -v`
fails with `ModuleNotFoundError: No module named '_shellwords'` (reported as one error, `FAILED
(errors=1)`). That is the RED state.

### Step 3 (GREEN): the reader

Create `E:/repos/taom-improve/wt-027/.claude/hooks/_shellwords.py` with exactly this content (it
was run against every expectation in Step 2 while the plan was written):

```python
"""The command a Bash or PowerShell tool call runs, read the way the TAOM gates need it (plan 027).

Every PreToolUse git gate was written for Bash text. The PowerShell tool sends PowerShell text, so
to_posix() rewrites it as the POSIX-shell (Bash) text of the same command, and each gate keeps its
Bash logic. In both shells a git named by a path or in capitals (GIT, git.exe, a Windows path to
git.exe) becomes `git` where it is the command.

Usage: <python> _shellwords.py posix|segments < the hook payload (JSON)
  posix     the command as POSIX-shell text
  segments  the posix text split at ; & | and newlines outside quotes, one segment per line, with a
            # comment dropped (validate-push.sh and mark-verification-run.sh)
It writes UTF-8 with LF line ends and exits 0; an unknown mode exits 2. PowerShell text it cannot
follow (an unclosed quote, here-string or block comment) comes back unchanged, which is how every
gate read a command before plan 027.

Not read, by design: a $(...) inside a double-quoted PowerShell string stays text, a $variable is
never expanded, and PowerShell's comma array syntax stays inside one word.
"""
import json
import re
import sys

GIT = re.compile(r"(?:.*[\\/])?git(?:\.exe)?", re.I)
SAFE = re.compile(r"[\w@%+=:,./^~-]+")
PS_ESCAPES = {"0": "", "a": "\a", "b": "\b", "e": "\x1b", "f": "\f", "n": "\n", "r": "\r",
              "t": "\t", "v": "\v"}
HERE_OPEN = re.compile(r"@(['\"])[ \t]*\n")
REDIRECT = re.compile(r"[<>]{1,2}(?:&[12])?")
FD_PREFIX = {"*", "1", "2", "3", "4", "5", "6"}
BASH_GIT = re.compile(r"""((?:^|[;&|(){}`\n])[ \t]*|\$\([ \t]*)("[^"\n]*"|'[^'\n]*'|[^\s;&|(){}'"`<>]+)""")


class Unreadable(ValueError):
    """PowerShell text this reader cannot follow."""


def _escape(s, i):
    """The text a PowerShell backtick at s[i] stands for, and the index after it."""
    nxt = s[i + 1:i + 2]
    if nxt == "u" and s[i + 2:i + 3] == "{":
        end = s.find("}", i + 3)
        if end > 0:
            try:
                return chr(int(s[i + 3:end], 16)), end + 1
            except ValueError:
                pass
    return PS_ESCAPES.get(nxt, nxt), i + 2


def ps_tokens(s):
    """PowerShell source as (kind, text) pairs: w a word, op a separator, call the & call
    operator, redir a redirection operator."""
    s = s.replace("\r", "")
    # word is None between words, else the current word's non-empty pieces. A list, so a long
    # word grows in linear time: string += on this closure variable was quadratic.
    out, word, i, n = [], None, 0, len(s)

    def add(text):
        nonlocal word
        if word is None:
            word = []
        if text:
            word.append(text)

    def flush():
        nonlocal word
        if word is not None:
            out.append(("w", "".join(word)))
            word = None

    while i < n:
        c = s[i]
        if c in " \t":
            flush()
            i += 1
        elif c == "`":
            if s[i + 1:i + 2] == "\n":           # a line continuation
                flush()
                i += 2
            else:
                ch, i = _escape(s, i)
                add(ch)
        elif c == "#" and word is None:           # a comment, to the end of the line
            j = s.find("\n", i)
            i = n if j < 0 else j
        elif word is None and s.startswith("<#", i):
            j = s.find("#>", i + 2)
            if j < 0:
                raise Unreadable("an unclosed <# comment")
            i = j + 2
        elif c == "'":
            j, buf = i + 1, []
            while True:
                if j >= n:
                    raise Unreadable("an unclosed ' string")
                if s[j] == "'":
                    if s[j + 1:j + 2] == "'":
                        buf.append("'")
                        j += 2
                        continue
                    break
                buf.append(s[j])
                j += 1
            add("".join(buf))
            i = j + 1
        elif c == '"':
            j, buf = i + 1, []
            while True:
                if j >= n:
                    raise Unreadable('an unclosed " string')
                if s[j] == "`":
                    ch, j = _escape(s, j)
                    buf.append(ch)
                    continue
                if s[j] == '"':
                    if s[j + 1:j + 2] == '"':
                        buf.append('"')
                        j += 2
                        continue
                    break
                buf.append(s[j])
                j += 1
            add("".join(buf))
            i = j + 1
        elif word is None and HERE_OPEN.match(s, i):
            m = HERE_OPEN.match(s, i)
            quote_char = m.group(1)
            end = re.compile("^" + re.escape(quote_char) + "@", re.M).search(s, m.end())
            if not end:
                raise Unreadable("an unclosed here-string")
            body = s[m.end():end.start()]
            if body.endswith("\n"):
                body = body[:-1]
            if quote_char == '"':
                parts, k = [], 0
                while k < len(body):
                    if body[k] == "`":
                        ch, k = _escape(body, k)
                        parts.append(ch)
                    else:
                        parts.append(body[k])
                        k += 1
                body = "".join(parts)
            out.append(("w", body))
            i = end.end()
        elif c in "$@" and s[i + 1:i + 2] == "(":  # a subexpression runs its commands
            flush()
            out.append(("op", "("))
            i += 2
        elif c in "(){};\n":
            flush()
            out.append(("op", c))
            i += 1
        elif c == "|":
            flush()
            t = "||" if s.startswith("||", i) else "|"
            out.append(("op", t))
            i += len(t)
        elif c == "&":
            flush()
            if s.startswith("&&", i):
                out.append(("op", "&&"))
                i += 2
            else:                                 # after a word: run in the background
                out.append(("op", "&") if out and out[-1][0] in ("w", "redir") else ("call", "&"))
                i += 1
        elif c in "<>" and (word is None or (len(word) == 1 and word[0] in FD_PREFIX)):
            fd, word = (word or [""])[0], None
            m = REDIRECT.match(s, i)
            out.append(("redir", fd + m.group(0)))
            i = m.end()
        elif word is None and s.startswith("--%", i) and s[i + 3:i + 4] in ("", " ", "\t", "\n"):
            j = s.find("\n", i)                   # stop-parsing: the rest of the line is verbatim
            j = n if j < 0 else j
            out.append(("w", "--%"))
            out.extend(("w", p) for p in s[i + 3:j].split())
            i = j
        else:
            add(c)
            i += 1
    flush()
    return out


def quote(word):
    """One word as a POSIX-shell word."""
    if word and SAFE.fullmatch(word):
        return word
    return "'" + word.replace("'", "'\\''") + "'"


def ps_to_posix(s):
    parts, command_position = [], True
    for kind, text in ps_tokens(s):
        if kind == "w":
            if command_position and GIT.fullmatch(text):
                text = "git"
            parts.append(quote(text))
            command_position = False
        elif kind == "call":
            command_position = True
        elif kind == "redir":
            parts.append(text)
        else:
            parts.append(";" if text in "(){}" else text)
            command_position = True
    return " ".join(parts)


def bash_to_posix(s):
    """Bash text with a git named by a path or in capitals renamed `git` where it is the command;
    every other character is left as it is."""
    def fix(m):
        word = m.group(2)
        bare = word[1:-1] if word[:1] in "\"'" else word
        return m.group(1) + "git" if bare != "git" and GIT.fullmatch(bare) else m.group(0)
    return BASH_GIT.sub(fix, s)


def to_posix(cmd, tool):
    if tool == "PowerShell":
        try:
            return ps_to_posix(cmd)
        except Unreadable:
            return cmd
    return bash_to_posix(cmd)


def segments(text):
    """POSIX text split at ; & | and newlines outside quotes, one segment per line. A newline
    inside quotes becomes a space, an escaped newline joins its lines, and a # that starts a word
    outside quotes drops the rest of its line."""
    text = text.replace("\r", "")
    out, q, i, n = [], "", 0, len(text)
    while i < n:
        c = text[i]
        if c == "\\" and q != "'":
            nxt = text[i + 1:i + 2]
            out.append(" " if nxt == "\n" else c + nxt)
            i += 2
            continue
        if q:
            if c == q:
                q = ""
            out.append(" " if c == "\n" else c)
        elif c in "\"'":
            q = c
            out.append(c)
        elif c == "#" and (i == 0 or text[i - 1] in " \t\n;&|()"):
            j = text.find("\n", i)
            i = n if j < 0 else j
            continue
        elif c in ";&|\n":
            out.append("\n")
        else:
            out.append(c)
        i += 1
    return "".join(out)


def read_payload(raw):
    """(tool_name, tool_input.command) of a hook payload; empty strings when it does not parse."""
    try:
        d = json.loads(raw)
    except ValueError:
        return "", ""
    if not isinstance(d, dict):
        return "", ""
    ti = d.get("tool_input")
    cmd = ti.get("command") if isinstance(ti, dict) else None
    return str(d.get("tool_name") or ""), cmd if isinstance(cmd, str) else ""


def main(argv):
    mode = argv[1] if len(argv) > 1 else ""
    if mode not in ("posix", "segments"):
        sys.stderr.write("usage: _shellwords.py posix|segments < payload.json\n")
        return 2
    tool, cmd = read_payload(sys.stdin.buffer.read().decode("utf-8", "replace"))
    text = to_posix(cmd, tool)
    if mode == "segments":
        text = segments(text)
    sys.stdout.buffer.write((text + "\n").encode("utf-8", "replace"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
```

**Verify**: `cd /e/repos/taom-improve/wt-027 && python -m unittest tools.tests.test_shellwords -v`
prints `Ran 33 tests` and `OK`. If one fails, fix the reader, never an expectation (they come from
PowerShell's parser); if a second attempt still fails, STOP.

Then commit 1: `git add .claude/hooks/_shellwords.py tools/tests/test_shellwords.py` and commit
with subject `feat(hooks): v2.0.30 - read PowerShell commands as Bash text` (body: what the reader
does, that nothing calls it yet, decision 61). Verify with `git show --stat HEAD` (exactly those
two files).

### Step 4 (RED): harness rows for the eight gates under both tool names

All edits in `E:/repos/taom-improve/wt-027/tools/test_hooks.sh`. Anchor each Edit on the quoted
existing text (line numbers are for orientation only). No existing row's expectation changes.

**4a. Section 4 contract payload.** After the line
`  'bash-trigger|{"tool_name":"Bash","tool_input":{"command":"git status && dotnet --info && echo committed pushed no-verify"},"hook_event_name":"PreToolUse"}'`
add:
```bash
  'powershell-trigger|{"tool_name":"PowerShell","tool_input":{"command":"git status; dotnet --info; Write-Output committed pushed no-verify"},"hook_event_name":"PreToolUse"}'
```
and extend the comment above `PAYLOADS=(` with one line:
`# powershell-trigger runs the same parse paths through the PowerShell reader (plan 027).`

**4b. Section 4c `pf_payload`.** Replace
```bash
pf_payload() {  # $1 event, $2 command already JSON-escaped; printf %s keeps its backslashes
    printf '{"tool_name":"Bash","session_id":"taom-prefilter-test","hook_event_name":"%s","tool_input":{"command":"%s","description":"prefilter probe"},"tool_response":{"stdout":"ok","stderr":""}}' "$1" "$2"
}
```
with
```bash
pf_payload() {  # $1 event, $2 command already JSON-escaped, $3 tool (default Bash); printf %s keeps its backslashes
    printf '{"tool_name":"%s","session_id":"taom-prefilter-test","hook_event_name":"%s","tool_input":{"command":"%s","description":"prefilter probe"},"tool_response":{"stdout":"ok","stderr":""}}' "${3:-Bash}" "$1" "$2"
}
```

**4c. Section 6 rows.** In `CSV_CASES`, after the line
`      "git -C form unlabelled|deny|git -C $REPO commit -m \"test: harness case\""` add:
```bash
      "git in capitals|deny|GIT commit -m \"docs: no label\""
      "one word piped to -F -, unlabelled|deny|echo \"docs: no label\" | git commit -F -"
      "one word piped to -F -, labelled|allow|printf '%s\n' \"docs: $CSV_VER - x\" | git commit -F -"
```

**4d. Section 7b.** After `printf 'echo hi\n' > "$CFT_REPO/.claude/hooks/bin/check.sh"` add
`printf 'x = 1\n' > "$CFT_REPO/.claude/hooks/_helper.py"`. In the first assertion, replace
`&& grep -q 'check.sh (gitignored' <<< "$OUT"; then` with
`&& grep -q 'check.sh (gitignored' <<< "$OUT" && grep -q '_helper.py (untracked' <<< "$OUT"; then`
and its `ok` text `"denies an untracked and a gitignored harness file in ${MS}ms"` with
`"denies an untracked and a gitignored harness file, a .py helper included, in ${MS}ms"`. Replace
`git -C "$CFT_REPO" add .claude/skills/demo/SKILL.md 2>/dev/null` with
`git -C "$CFT_REPO" add .claude/skills/demo/SKILL.md .claude/hooks/_helper.py 2>/dev/null`.

**4e. New section 7e.** Insert immediately after the line `rm -rf "$MVR_DIR"` that ends section 7d
(just before the `# ----` line above `head2 "8. /context-budget scan.sh ...`):

```bash
# ---------------------------------------------------------------------------
head2 "7e. the git gates read a PowerShell command as they read its Bash twin"
# Maintainer decision 61 (plan 027): eight PreToolUse gates were registered for the Bash tool only,
# so a git command run through the PowerShell tool skipped them, and every gate read its command as
# Bash text (a labelled PowerShell here-string commit read as the subject `@` and was denied). One
# `Bash|PowerShell` group now registers all nine, and each gate reads its command through _pybin.sh
# taom_hook_command, which runs _shellwords.py: PowerShell comes back as the Bash text of the same
# command, and a git named by a path or in capitals comes back as `git` in both shells.
BT='`'; NL=$'\n'; V=${CSV_VER:-v0.0.0}
G7E_GATES=$("$HPY" - <<'PY' | tr -d '\r'
import json
d = json.load(open('.claude/settings.json', encoding='utf-8'))
pre = d.get('hooks', {}).get('PreToolUse', [])
names = sorted({h['command'].rsplit('/', 1)[-1] for g in pre
                if 'Bash' in g.get('matcher', '').split('|') for h in g.get('hooks', [])})
for n in names:
    tools = [t for g in pre if any(h['command'].endswith('/' + n) for h in g.get('hooks', []))
             for t in g.get('matcher', '').split('|')]
    print(n, tools.count('Bash'), tools.count('PowerShell'))
PY
)
[[ -z "$G7E_GATES" ]] && bad "7e found no PreToolUse hook registered for Bash; the discovery is broken"
G7E_NAMES=""
while read -r name nb np; do
    [[ -z "$name" ]] && continue
    G7E_NAMES+="$name "
    if [[ "$nb" == 1 && "$np" == 1 ]]; then
        ok "$name is registered once for Bash and once for PowerShell"
    else
        bad "$name is registered for Bash ${nb}x and for PowerShell ${np}x; a git gate needs one Bash|PowerShell registration"
    fi
done <<< "$G7E_GATES"

# The prefilter reads the raw payload whatever the tool: a PowerShell call without the gate's word
# starts no Python, and one holding it reaches the parse.
for name in $G7E_NAMES; do
    read -r s n <<< "$(pf_run "$name" "$(pf_payload PreToolUse 'Get-ChildItem docs | Select-Object -First 3' PowerShell)")"
    if [[ "$s" == 0 && "$n" == 0 ]]; then
        ok "$name [PowerShell] no interpreter on a non-trigger payload"
    else
        bad "$name [PowerShell] reached _pybin.sh ($s source, $n start) on Get-ChildItem docs"
    fi
    case "$name" in
        validate-push.sh)   trig='git push origin x' ;;
        block-no-verify.sh) trig='git commit --no-verify -m x' ;;
        block-dangerous-git.sh | block-broad-git-add.sh) trig='git status' ;;
        *)                  trig="git commit -m @'\\nx\\n'@" ;;
    esac
    read -r s n <<< "$(pf_run "$name" "$(pf_payload PreToolUse "$trig" PowerShell)")"
    if [[ "$s" -ge 1 ]]; then
        ok "$name [PowerShell] reaches _pybin.sh on [$trig]"
    else
        bad "$name [PowerShell] never reached _pybin.sh on [$trig]"
    fi
done

g7e_verdict() {  # $1 hook, $2 tool, $3 project dir, $4 command: "rc=<n> <decision>"
    local payload out rc
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$2" "$4")
    out=$(printf '%s' "$payload" | timeout -k 2 30 env CLAUDE_PROJECT_DIR="$3" bash "$REPO/.claude/hooks/$1" 2>/dev/null)
    rc=$?
    echo "rc=$rc $(decision_of "$out")"
}
G7E_OKFILE=${CSV_MSGFILE:-}; G7E_BADFILE=${CSV_BADFILE:-}
if command -v cygpath >/dev/null 2>&1; then
    G7E_OKFILE=$(cygpath -w "$G7E_OKFILE"); G7E_BADFILE=$(cygpath -w "$G7E_BADFILE")
fi
# hook|tool|project (S sandbox, R repo)|expected "rc=<n> <decision>"|command
G7E_ROWS=(
  # block-no-verify.sh blocks with exit 2 and prints no decision
  "block-no-verify.sh|PowerShell|S|rc=2 allow|git commit --no-verify -m \"x\""
  "block-no-verify.sh|PowerShell|S|rc=2 allow|GIT commit --no-verify -m x"
  "block-no-verify.sh|PowerShell|S|rc=2 allow|& 'C:\\Program Files\\Git\\cmd\\git.exe' push --no-verify origin feature"
  # A known gap, pinned (see Out of scope): the raw prefilter never sees the text no-verify here.
  "block-no-verify.sh|PowerShell|S|rc=0 allow|git commit --no-verif''y -m x"
  "block-no-verify.sh|PowerShell|S|rc=2 allow|git status; git commit ${BT}${NL}  --no-verify -m x"
  "block-no-verify.sh|PowerShell|S|rc=0 allow|npm publish --no-verify"
  "block-no-verify.sh|Bash|S|rc=2 allow|GIT commit --no-verify -m x"
  "block-no-verify.sh|Bash|S|rc=2 allow|git commit --no-verify -m x"
  # block-dangerous-git.sh confirms (ask) a command that can destroy work
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|& git reset --hard HEAD~1"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|GIT clean -fd"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|& 'C:\\Program Files\\Git\\cmd\\git.exe' stash drop"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|if (\$true) { git stash clear }"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|1..1 | ForEach-Object {git checkout -- .}"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch; git reset --hard origin/feature"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch && git reset --hard origin/feature"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git fetch || git clean -f"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git reset ${BT}${NL}  --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 ask|git -C \"E:\\repos\\x\" reset --hard"
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|git restore --staged a.txt"
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|Write-Output \"git reset --hard\""
  "block-dangerous-git.sh|PowerShell|S|rc=0 allow|git commit -m \"docs: say why git reset --hard is gated\""
  "block-dangerous-git.sh|Bash|S|rc=0 ask|GIT reset --hard"
  "block-dangerous-git.sh|Bash|S|rc=0 ask|\"/c/Program Files/Git/cmd/git.exe\" clean -fd"
  # block-broad-git-add.sh confirms (ask) a command that stages everything
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git add -A"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|& git add --all"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|GIT add ."
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git status; git add -u"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git commit -am \"docs: x\""
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|if (\$true) {git add -A}"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|git add ${BT}${NL}  -A"
  "block-broad-git-add.sh|PowerShell|S|rc=0 ask|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -a -m 'x'"
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git add a.txt b.txt"
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git commit -m \"fix: add -a flag\""
  "block-broad-git-add.sh|PowerShell|S|rc=0 allow|git commit -m @'${NL}fix: add -A handling${NL}'@"
  "block-broad-git-add.sh|Bash|S|rc=0 ask|GIT add -A"
  # check-commit-subject-version.sh denies an unlabelled subject or an AI attribution line
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m @'${NL}feat(hooks): $V - here-string subject${NL}${NL}Body line.${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m @'${NL}feat(hooks): here-string subject${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m @'${NL}feat(hooks): $V - x${NL}${NL}Co-Authored-By: Claude <noreply@anthropic.com>${NL}'@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m @\"${NL}feat(hooks): $V - expandable here-string${NL}\"@"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m \"docs: $V - x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m 'docs: $V - Mike''s harbor ships'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -m \"docs: $V - x\" -m \"Body${BT}n${BT}nCo-Authored-By: Claude <noreply@anthropic.com>\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|GIT commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git add a.txt; git commit -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git add a.txt && git commit -m \"docs: $V - x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit ${BT}${NL}  -m \"docs: no label\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|if (\$true) { git commit -m \"docs: no label\" }"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|@'${NL}docs: no label${NL}'@ | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|@'${NL}docs: $V - piped here-string${NL}'@ | git commit -F -"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -F '$G7E_OKFILE'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 deny|git commit -F '$G7E_BADFILE'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit -m \"docs: $V - x\" # a trailing comment"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|Write-Output 'git commit -m \"docs: no label\"'"
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit-tree HEAD^{tree} -m \"x\""
  "check-commit-subject-version.sh|PowerShell|R|rc=0 allow|git commit --amend --no-edit"
)
for entry in "${G7E_ROWS[@]}"; do
    hook="${entry%%|*}"; rest="${entry#*|}"
    tool="${rest%%|*}"; rest="${rest#*|}"
    dirkey="${rest%%|*}"; rest="${rest#*|}"
    want="${rest%%|*}"; cmd="${rest#*|}"
    dir=$SANDBOX; [[ "$dirkey" == R ]] && dir=$REPO
    got=$(g7e_verdict "$hook" "$tool" "$dir" "$cmd")
    shown="${cmd//$'\n'/\\n}"
    if [[ "$got" == "$want" ]]; then
        ok "$hook [$tool] $got for: $shown"
    else
        bad "$hook [$tool] expected '$want', got '$got' for: $shown"
    fi
done

# The four gates that judge what is staged only need to know a commit is coming. Each reaches its
# `cd` to the project only past its two-stage commit test, so a bash -x trace shows the answer.
g7e_reaches() {  # $1 hook, $2 tool, $3 command: 1 when the gate got past its commit test
    local payload
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$2" "$3")
    printf '%s' "$payload" | timeout -k 2 60 env PS4='+ ' CLAUDE_PROJECT_DIR="$SANDBOX" bash -x "$REPO/.claude/hooks/$1" >/dev/null 2>"$SANDBOX/g7e.trace"
    if grep -qE '^\+ cd ' "$SANDBOX/g7e.trace"; then echo 1; else echo 0; fi
}
G7E_REACH=(
  "PowerShell|1|GIT commit -m x"
  "PowerShell|1|& 'C:\\Program Files\\Git\\cmd\\git.exe' commit -m x"
  "PowerShell|1|git commit -m @'${NL}docs: x${NL}'@"
  "PowerShell|1|if (\$true) { git -C \"E:\\repos\\x\" commit --amend }"
  "PowerShell|0|git commit-tree HEAD -m x"
  "PowerShell|0|git log --grep commit"
  "Bash|1|GIT commit -m x"
)
for hook in check-claude-files-tracked.sh check-moduledata-validation.sh check-native-dll-crt.sh check-doc-config-drift.sh; do
    for entry in "${G7E_REACH[@]}"; do
        tool="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"
        got=$(g7e_reaches "$hook" "$tool" "$cmd")
        shown="${cmd//$'\n'/\\n}"
        if [[ "$got" == "$want" ]]; then
            ok "$hook [$tool] commit test $got for: $shown"
        else
            bad "$hook [$tool] commit test expected $want, got $got for: $shown"
        fi
    done
done
G7E_CFT="$SANDBOX/g7e-cft"
mkdir -p "$G7E_CFT/.claude/skills/demo"
git -C "$G7E_CFT" init -q 2>/dev/null
printf '# demo\n' > "$G7E_CFT/.claude/skills/demo/SKILL.md"
got=$(g7e_verdict check-claude-files-tracked.sh PowerShell "$G7E_CFT" "git commit -m @'${NL}docs: x${NL}'@")
if [[ "$got" == "rc=0 deny" ]]; then
    ok "check-claude-files-tracked denies a PowerShell here-string commit over an untracked skill"
else
    bad "check-claude-files-tracked answered '$got' to a PowerShell commit over an untracked skill; expected 'rc=0 deny'"
fi

# Large payloads under both tools stay inside 80% of each gate's registration (the plan 011 review
# saw a 5 s registration crossed under load, and a killed gate fails open).
for kind in ps-big ps-lines bash-big; do
    "$HPY" - "$kind" > "$SANDBOX/g7e-$kind.json" <<'PY'
import json, sys
big = "x" * 100000
kind = sys.argv[1]
if kind == "ps-big":
    tool, cmd = "PowerShell", "Write-Output '" + big + "'; git status --no-verify; git push origin feature; git commit -m @'\ndocs: no label\n'@"
elif kind == "ps-lines":
    tool, cmd = "PowerShell", "\n".join('git -C E:\\x\\r%d commit -m "docs: no label"' % i for i in range(100))
else:
    tool, cmd = "Bash", "echo '" + big + "'; git status --no-verify; git push origin feature; git commit -m \"docs: no label\""
sys.stdout.write(json.dumps({"tool_name": tool, "tool_input": {"command": cmd}, "hook_event_name": "PreToolUse"}))
PY
done
for name in $G7E_NAMES; do
    REG=$("$HPY" - "$name" <<'PYEOF'
import json, sys
d = json.load(open('.claude/settings.json', encoding='utf-8'))
print(next((h.get('timeout', 600) for g in d['hooks'].get('PreToolUse', []) for h in g['hooks']
            if h['command'].endswith(sys.argv[1])), 0))
PYEOF
)
    REG=${REG%$'\r'}
    for kind in ps-big ps-lines bash-big; do
        S=$(date +%s%N)
        timeout -k 2 65 env CLAUDE_PROJECT_DIR="$REPO" bash ".claude/hooks/$name" < "$SANDBOX/g7e-$kind.json" >/dev/null 2>&1
        MS=$(( ($(date +%s%N) - S) / 1000000 ))
        if (( MS * 10 >= REG * 1000 * 8 )); then
            bad "$name took ${MS}ms on the $kind payload against its ${REG}s registration: the harness kills it (silently) under load"
        else
            ok "$name ${MS}ms of ${REG}s on the $kind payload"
        fi
    done
done
```

**Verify**: `bash tools/test_hooks.sh > /e/repos/taom-improve/scratch/plans/027/step4.txt 2>&1; grep -c 'FAIL' /e/repos/taom-improve/scratch/plans/027/step4.txt; tail -3 /e/repos/taom-improve/scratch/plans/027/step4.txt`.
Expected: exactly **40** failures, all of them rows this step added, namely:

- 7e verdict rows (25): `block-no-verify.sh` [PowerShell] `GIT commit ...`, `& 'C:\...\git.exe' push --no-verify ...`, and [Bash] `GIT commit ...`; `block-dangerous-git.sh` [PowerShell] `& git reset --hard HEAD~1`, `GIT clean -fd`, `& '...git.exe' stash drop`, `if ($true) { git stash clear }`, `ForEach-Object {git checkout -- .}`, the backtick-continued `git reset`, and [Bash] `GIT reset --hard`, `"/c/Program Files/Git/cmd/git.exe" clean -fd`; `block-broad-git-add.sh` [PowerShell] `& git add --all`, `GIT add .`, `if ($true) {git add -A}`, the backtick-continued `git add`, `& '...git.exe' commit -a -m 'x'`, and [Bash] `GIT add -A`; `check-commit-subject-version.sh` [PowerShell] the labelled `@'` here-string, the labelled `@"` here-string, the backtick-n `Co-Authored-By` row, `GIT commit`, `& '...git.exe' commit`, the backtick-continued commit, `if ($true) { git commit ... }`, and the unlabelled here-string piped to `git commit -F -`.
- 7e commit-test rows (12): for each of the four staged-file gates, [PowerShell] `GIT commit -m x`, [PowerShell] `& '...git.exe' commit -m x`, [Bash] `GIT commit -m x`.
- Section 6 (2): `git in capitals`, `one word piped to -F -, unlabelled`.
- Section 7b (1): the `.py helper included` assertion.

One more row may fail, depending on load: the 7e timing row
`block-broad-git-add.sh took <N>ms on the ps-lines payload`. The unchanged hook forks `sed` once
per git segment (Step 5g removes that); at plan revision it took 4436 ms here against the 4000 ms
limit. So the count is **40, or 41 with that timing row as the 41st**. The 7e section as a whole
was run at plan revision against the unchanged hooks (with the Step 0 settings): its 25 verdict
and 12 commit-test failures are exactly the ones listed above, plus that timing row.

Every other row passes, including all 482 baseline rows. A row this plan says passes today that
fails: STOP (the plan's reading of today's behaviour is wrong, or you changed something else). A
row listed above that already passes: note it in your report and continue.

### Step 5 (GREEN): read every gate's command through the reader

**5a. `.claude/hooks/_pybin.sh`.** Line 80 reads
```text
# the smallest registered timeout: 12 of the 27 registrations are 5s. `-k 0.2 0.8` means a
```
(the count was already stale: 14 of 27 at `96afb6fb`). Replace it with
```text
# the smallest registered timeout, which is 5s for most registrations. `-k 0.2 0.8` means a
```
Then append after the last line (`export PYTHONIOENCODING=utf-8`):

```bash

# The directory holding this helper and _shellwords.py, made absolute so a hook that later changes
# directory can still reach the reader. Every hook sources this file by a path with a slash in it.
TAOM_HOOKS_DIR=${BASH_SOURCE[0]%/*}
case "$TAOM_HOOKS_DIR" in /* | [A-Za-z]:*) ;; *) TAOM_HOOKS_DIR="$PWD/$TAOM_HOOKS_DIR" ;; esac

# The tool call's command as a gate reads it (plan 027, maintainer decision 61). Every gate was
# written for Bash text; _shellwords.py hands a PowerShell command back as the Bash text of the same
# command, and names git `git` wherever it is the command (`GIT`, `git.exe`, a path) in both shells.
# $1 is the reader's mode (posix), $2 the gate's name for the stderr note; it reads the hook's
# $INPUT. If the reader fails, the raw command comes back, read as Bash text as every gate read it
# before plan 027, and the gate says so rather than go quiet.
taom_hook_command() {
    local out
    if out=$(printf '%s' "$INPUT" | "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" "$1" 2>/dev/null); then
        printf '%s' "$out"
        return 0
    fi
    printf '%s: _shellwords.py failed, so the command is read as Bash text\n' "${2:-hook}" >&2
    printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    sys.stdout.buffer.write(str((d.get("tool_input") or {}).get("command") or "").encode("utf-8", "replace"))
except Exception:
    pass
' 2>/dev/null
}
```

**5b. The three jq-capable gates.** The order changes on purpose: Python (the reader) first, jq
only when there is no Python. On the Linux CI runner jq is on PATH, and jq first would never read
PowerShell there.

In `block-no-verify.sh`, replace the block quoted in "Current state" (lines 43 to 55, the comment
`# Prefer jq; fall back to python3 for robust JSON (handles escaped quotes).` through its `fi`)
with this block, exactly as shown (column 0, two-space body):

```bash
# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path). Python first, so the CI runner (which has jq) reads
# PowerShell too; jq only without Python, reading the raw command as Bash text.
if [[ -n "${PYBIN:-}" ]]; then
  COMMAND=$(taom_hook_command posix block-no-verify)
else
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
fi
```

In `block-dangerous-git.sh`, replace lines 52 to 65, from

```bash
# Extract tool_input.command. Prefer jq; fall back to python3 for robust JSON
# (handles escaped quotes — the grep+sed fallback truncated those). Mirrors the
# parser in check-claude-files-tracked.sh.
if command -v jq >/dev/null 2>&1; then
```

through the matching `fi`, with the same block, `block-no-verify` changed to `block-dangerous-git`.

In `block-broad-git-add.sh`, replace lines 62 to 74, from

```bash
# Extract tool_input.command. Prefer jq; fall back to python3 for robust JSON
# (handles escaped quotes). Mirrors block-dangerous-git.sh.
if command -v jq >/dev/null 2>&1; then
```

through the matching `fi`, with the same block, `block-no-verify` changed to `block-broad-git-add`.

(`validate-push.sh` is Step 7.) The two confirm gates put `COMMAND` in their ask message; it now
shows the command as POSIX text, which is intended.

**5c. The five python-only gates.** In each of `check-claude-files-tracked.sh`,
`check-commit-subject-version.sh`, `check-moduledata-validation.sh`, `check-native-dll-crt.sh`
and `check-doc-config-drift.sh`, replace the eight-line `COMMAND=$(printf '%s' "$INPUT" | "$PYBIN" -c '`
... `' 2>/dev/null)` block quoted in "Current state" (and its one-line comment above it, if it has
one: `# Extract the bash command from tool_input.` or
`# Extract the bash command from tool_input (mirrors check-moduledata-validation.sh).`) with:

```bash
# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command and names git `git` wherever it is the
# command (`GIT`, `git.exe`, a path), so the two-stage matcher below reads both shells.
COMMAND=$(taom_hook_command posix <gate name without .sh>)
```

**5d. `check-claude-files-tracked.sh:84`.** Replace `grep -E '\.(md|sh|json|ya?ml) \('` with
`grep -E '\.(md|sh|py|json|ya?ml) \('` (plan 027 adds the first `.py` helper under
`.claude/hooks/`, and an untracked one would break every gate in a fresh clone).

**5e. `check-commit-subject-version.sh`: one word piped to `-F -`.** Inside the Python heredoc,
replace the `commit_arg_lists` function quoted in "Current state" with:

```python
def piped_text(group):
    """What a command that only prints one word hands the next command through a pipe: the word
    itself (a PowerShell string or here-string alone), echo or Write-Output with one word, or
    printf with a format and one word. None for anything else."""
    if len(group) == 1:
        return group[0]
    name = os.path.basename(group[0]).lower() if group else ""
    if len(group) == 2 and name in ("echo", "write-output"):
        return group[1]
    if len(group) == 3 and name == "printf":
        return group[2]
    return None

def commit_arg_lists(s, depth=0):
    """The argument list after `commit` of every git commit invocation in s. A commit whose stdin
    is piped from a command that only prints one word (a PowerShell here-string piped to
    `git commit -F -`, plan 027) gets that word first as a heredoc placeholder, so `-F -` reads it."""
    found = []
    group, prev, sep = [], [], ""
    for t in tokens(s) + [";"]:
        if t and set(t) <= SEP:
            if group:
                lists = _commit_args(group, depth)
                piped = piped_text(prev) if sep == "|" else None
                if piped is not None:
                    bodies.append(piped)
                    mark = ["<<", "__TAOM_HEREDOC_%d__" % (len(bodies) - 1)]
                    lists = [mark + args for args in lists]
                found.extend(lists)
            prev, group, sep = group, [], t
        else:
            group.append(t)
    return found
```
(`sep == "|"` is an exact comparison, so `||` never counts as a pipe; the mark goes first so a
heredoc on the commit itself still wins, as in bash.)

**5f. Header comments.** In each of the eight gates change `PreToolUse(Bash)` on line 3 to
`PreToolUse (Bash and PowerShell)`.

**5g. `block-broad-git-add.sh`: strip quoted spans without a fork.** Line 106, inside the
per-segment loop, forks `sed` once per git segment:

```bash
  rest=$(printf '%s' "$rest" | sed -E "s/\"[^\"]*\"//g; s/'[^']*'//g")
```

Step 4's 7e `ps-lines` timing row sends this gate 100 git segments. Measured at plan review on a
loaded machine (three runs each): the **unchanged** hook took 5313, 5512 and 7413 ms on the Bash
twin of that payload, against the row's 4000 ms limit (80% of 5 s); a killed ask gate allows. The
cost is the fork, not the reader. Replace that one line (keep the three comment lines above it)
with:

```bash
  # Without a fork (plan 027): the sed this replaces ran once per git segment, and 100 git
  # segments took over 5 s on a loaded machine, past the 5 s registration (a killed gate allows).
  # Same result as sed -E "s/\"[^\"]*\"//g; s/'[^']*'//g": double-quoted spans, then single.
  for q in '"' "'"; do
    kept=""
    while [[ "$rest" == *"$q"*"$q"* ]]; do
      kept+=${rest%%"$q"*}
      rest=${rest#*"$q"}
      rest=${rest#*"$q"}
    done
    rest=$kept$rest
  done
```

(`q` and `kept` are used nowhere else in the file. Each pass keeps the text before the first quote
and drops through the next one, which is what sed's leftmost `"[^"]*"` does; an unpaired quote is
left in place, as sed leaves it. Checked at plan revision: 5,000 random one-line segments over
the characters `a b " ' - * ? [ ] \ $ #` and space (3,758 of them holding a quote) gave
byte-identical output from this loop and from the sed; with 5g applied the hook took
264 to 544 ms on `ps-lines` and on its Bash twin, where the same hook with the sed took 4968 to
7377 ms (three sessions of three runs each, same loaded machine, measured side by side).)

**5h. The two confirm gates' prefilter reads `git` in any case.** `block-dangerous-git.sh:44` and
`block-broad-git-add.sh:54` both read, verbatim:

```bash
[[ "$INPUT" == *git* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }
```

A payload whose only git is `GIT` holds no lowercase `git`, so both gates exit here before the
reader runs, and Step 4's four 7e rows `GIT clean -fd` and `GIT add .` [PowerShell], `GIT reset
--hard` and `GIT add -A` [Bash] stay `allow` (checked at plan revision: all four answer `{}` with
Steps 5a to 5g applied). In both files replace that line with:

```bash
[[ "$INPUT" == *[Gg][Ii][Tt]* || "$INPUT" == *'\u'* ]] || { echo '{}'; exit 0; }
```

and insert this comment line immediately above the new line:
`# git in any case (plan 027): the reader reads GIT as git, so the raw test must let it through.`
The test stays on the gate's own word (decision 39), and the `\u` arm is untouched (decision 40).
The other seven gates filter on `push`, `no-verify` or `commit`, which a capitalised `GIT` does not
change.

**Verify**:
- `bash -n .claude/hooks/_pybin.sh && for f in block-no-verify check-claude-files-tracked check-commit-subject-version block-dangerous-git block-broad-git-add check-moduledata-validation check-native-dll-crt check-doc-config-drift; do bash -n .claude/hooks/$f.sh || echo "SYNTAX $f"; done` prints nothing.
- `grep -L 'taom_hook_command posix' .claude/hooks/block-no-verify.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-native-dll-crt.sh .claude/hooks/check-doc-config-drift.sh` prints nothing.
- `git grep -n 'PreToolUse(Bash)' -- .claude/hooks` prints nothing.
- `grep -c 'rest=$(printf' .claude/hooks/block-broad-git-add.sh` prints `0` (5g).
- `grep -cF '*[Gg][Ii][Tt]*' .claude/hooks/block-dangerous-git.sh .claude/hooks/block-broad-git-add.sh`
  prints `.claude/hooks/block-dangerous-git.sh:1` and `.claude/hooks/block-broad-git-add.sh:1` (5h).
- `bash tools/test_hooks.sh`: `0 failed`, and the passed count is the Step 1 baseline plus the rows
  added in Step 4 (482 + 25 section 4 + 3 section 6 + 141 section 7e = **651**; 7b adds none).

Then commit 2: `git add .claude/settings.json .claude/hooks/_pybin.sh .claude/hooks/block-no-verify.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/block-broad-git-add.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-native-dll-crt.sh .claude/hooks/check-doc-config-drift.sh tools/test_hooks.sh`,
subject `feat(hooks): v2.0.30 - run the eight git gates for the PowerShell tool`. The body names
decision 61, the measured false deny of a labelled here-string commit, the Python-first order
change, the fork-free quote strip in `block-broad-git-add.sh` (5g, with its timing) and the
any-case `git` prefilter of the two confirm gates (5h), and `Not-tested: a live PowerShell tool call through the real harness (owed after merge)`.
`git show --stat HEAD` lists exactly those 11 files.

### Step 6 (RED): validate-push rows

In `tools/test_hooks.sh` section 7c:

**6a.** In `VP_CASES`, after the line `  "0|echo push"` (the last entry) add:
```bash
  # Plan 027: a pattern or DWIM refspec, git by path or in capitals, a bash backtick
  # substitution, a trunk named only in a trailing comment, and a push option's value.
  "2|git push --force origin 'refs/heads/*'"
  "2|git push origin '+refs/heads/*:refs/heads/*'"
  "2|git push --force origin 'refs/heads/bannerlord-*'"
  "2|git push --force origin HEAD:heads/bannerlord-1.5.x"
  "2|git push --prune --force origin 'refs/heads/*:refs/heads/*'"
  "0|git push --force origin 'refs/heads/feature-*'"
  "0|git push --force origin refs/tags/v1"
  "2|GIT push --force origin bannerlord-1.5.x"
  "2|\"/c/Program Files/Git/cmd/git.exe\" push --force origin bannerlord-1.5.x"
  "2|x=\`git push --force origin bannerlord-1.5.x\`"
  "0|git push --force origin feature # bannerlord-1.5.x later"
  "0|git push --force -o bannerlord-1.5.x origin feature"
```

**6b.** In `VP_TOOL_CASES`, after the line
`  'Bash|2|git -c "user.name=a\" b" -C "E:/R&D" push --force origin bannerlord-1.5.x'` add:
```bash
  # Plan 027: PowerShell braces, the call operator, capitals, escapes and comments.
  'PowerShell|2|if ($true) {git push --force origin bannerlord-1.5.x}'
  'PowerShell|2|1..1 | ForEach-Object {git push --force origin bannerlord-1.5.x}'
  "PowerShell|2|& 'C:\\Program Files\\Git\\cmd\\git.exe' push --force origin bannerlord-1.5.x"
  'PowerShell|2|GIT push --force origin bannerlord-1.5.x'
  'PowerShell|2|git push --force origin bannerlord-1.5`.x'
  'PowerShell|2|$(git push --force origin bannerlord-1.5.x)'
  "PowerShell|2|git push --force origin 'refs/heads/*'"
  "PowerShell|2|git push \`"$'\n'"  --force origin bannerlord-1.5.x"
  'PowerShell|0|git push --force origin feature # bannerlord-1.5.x later'
  'PowerShell|2|git push --force origin bannerlord-1.4.5 # note'
  "PowerShell|0|git commit -m @'"$'\n'"Don't force push the trunk"$'\n'"'@; git push origin feature"
  'PowerShell|0|git push --force -o ci.skip origin feature'
  # Plan 027 review: refused before plan 027 and still refused. A # inside a quoted value after a
  # heredoc apostrophe must not hide the push, and PowerShell shapes the reader alone would miss
  # (a comma argument list, a parenthesised command) are judged on the raw command too.
  "Bash|2|echo \$'it\\'s'; echo \"a #b\"; git push --force origin bannerlord-1.5.x; echo done"
  "Bash|2|cat <<EOF"$'\n'"it's"$'\n'"EOF"$'\n'"echo \"a #b\"; git push --force origin bannerlord-1.5.x; echo done"
  "Bash|2|git commit -F - <<'EOF'"$'\n'"Don't stop"$'\n'"EOF"$'\n'"git log --grep \"fix #1\"; git push --force origin bannerlord-1.5.x; git log -1"
  "Bash|2|cat <<EOF"$'\n'"it's"$'\n'"EOF"$'\n'"git commit -m \"x"$'\n'"#1\"; git push --force origin bannerlord-1.5.x; echo"
  "PowerShell|2|Start-Process git -ArgumentList 'push','--force','origin','bannerlord-1.5.x' -Wait"
  "PowerShell|2|Start-Process git -ArgumentList 'push', '--force', 'origin', 'bannerlord-1.5.x'"
  "PowerShell|2|[Diagnostics.Process]::Start('git', 'push --force origin bannerlord-1.5.x')"
  "PowerShell|2|& (\"git\") push --force origin bannerlord-1.5.x"
  "PowerShell|2|& (Get-Command git) push --force origin bannerlord-1.5.x"
```
and in the `VP_TOOL_CASES` loop change the two message lines' `$cmd` to a `shown` form: add
`shown="${cmd//$'\n'/\\n}"` after `cmd="${rest#*|}"` and use `$shown` in both `ok` and `bad` texts.

**6c.** After that loop's `done` (before the comment `# Every segment is judged under both
splits...`) add the branch-dependent rows:
```bash
# A push with no refspec pushes the checked-out branch, so a push option's value (-o ci.skip) must
# never be taken for the remote: run on a trunk, `git push --force -o ci.skip origin` passed
# (plan 027). The hook asks git for the branch in its own directory, so these run in scratch repos.
VP_TRUNK="$SANDBOX/vp-trunk"; VP_FEAT="$SANDBOX/vp-feature"
for pair in "$VP_TRUNK|bannerlord-1.5.x" "$VP_FEAT|feature"; do
    d="${pair%%|*}"; b="${pair#*|}"
    git init -q -b "$b" "$d" 2>/dev/null
    git -C "$d" -c user.name=t -c user.email=t@example.invalid commit -q --allow-empty -m init 2>/dev/null
done
vp_run_in() {  # $1 directory to run in, $2 tool, $3 command; returns the hook's rc
    local payload
    payload=$("$HPY" -c 'import json,sys; print(json.dumps({"tool_name":sys.argv[1],"tool_input":{"command":sys.argv[2]},"hook_event_name":"PreToolUse"}))' "$2" "$3")
    ( cd "$1" && printf '%s' "$payload" | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$SANDBOX" bash "$REPO/.claude/hooks/validate-push.sh" >/dev/null 2>&1 )
}
VP_BRANCH_CASES=(
  "$VP_TRUNK|2|git push --force -o ci.skip origin"
  "$VP_TRUNK|2|git push --force --push-option ci.skip origin"
  "$VP_TRUNK|2|git push --force origin"
  "$VP_TRUNK|0|git push -o ci.skip origin"
  "$VP_FEAT|0|git push --force -o ci.skip origin"
  # Plan 027 review: a trunk refspec in parentheses. The reader makes ( ) a statement break, so
  # its text alone holds a push with no refspec, which is the feature branch here.
  "$VP_FEAT|2|git push --force origin (\"bannerlord-1.5.x\")"
  "$VP_FEAT|2|git push --force origin \$(\"bannerlord-1.5.x\")"
)
for tool in Bash PowerShell; do
    for entry in "${VP_BRANCH_CASES[@]}"; do
        dir="${entry%%|*}"; rest="${entry#*|}"; want="${rest%%|*}"; cmd="${rest#*|}"
        vp_run_in "$dir" "$tool" "$cmd"; got=$?
        [[ "$got" == "$want" ]] && ok "validate-push [$tool] rc=$got on branch ${dir##*/} for: $cmd" \
            || bad "validate-push [$tool] expected rc=$want, got $got on branch ${dir##*/} for: $cmd"
    done
done
```

**Verify**: `bash tools/test_hooks.sh > /e/repos/taom-improve/scratch/plans/027/step6.txt 2>&1; grep -c 'FAIL' /e/repos/taom-improve/scratch/plans/027/step6.txt`.
Expected: exactly **19** failures, all new 7c rows: from 6a the eight rows for `'refs/heads/*'`,
`'+refs/heads/*:refs/heads/*'`, `'refs/heads/bannerlord-*'`, `HEAD:heads/...`, the `--prune` row,
`GIT push`, the backtick substitution and the `# bannerlord-1.5.x later` comment; from 6b the seven
PowerShell rows `if ($true) {...}`, `ForEach-Object {...}`, `& '...git.exe'`, `GIT push`, the
backtick-escaped `.x`, `'refs/heads/*'` and the `# ... later` comment; from 6c the first two
`VP_TRUNK` rows under both tools (4). The nine "Plan 027 review" rows of 6b and the two of 6c
(four checks) pass today: they guard behaviour Step 7 must keep. A row this plan says passes today
that fails: STOP.

### Step 7 (GREEN): validate-push

All in `E:/repos/taom-improve/wt-027/.claude/hooks/validate-push.sh`.

**7a.** Replace lines 32 to 47 (from `# Prefer jq; fall back to "$PYBIN" for robust JSON. The
grep+sed fallback this used to` through its `fi`) with:
```bash
# The command as POSIX-shell text (plan 027): _pybin.sh taom_hook_command hands a PowerShell
# command back as the Bash text of the same command (braces, the & call operator, comments and
# backtick escapes resolved) and names git `git` wherever it is the command (`GIT`, `git.exe`, a
# path). Python first, so the CI runner (which has jq) reads PowerShell too; with jq and no Python
# the raw command is judged as Bash text. The grep+sed fallback this once carried truncated the
# command at the first escaped quote, which could drop a trailing --force.
if [ -n "$PYBIN" ]; then
  COMMAND=$(taom_hook_command posix validate-push)
else
  COMMAND=$(printf '%s' "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)
fi
```

**7b.** Replace the `is_protected` function (lines 54 to 59; keep the comment lines 49 to 53 above
it) with:
```bash
PROTECTED=(master main bannerlord-1.4.5 bannerlord-1.5.x)
is_protected() {
  local p
  for p in "${PROTECTED[@]}"; do [[ "$1" == "$p" ]] && return 0; done
  return 1
}
```

**7c.** Replace the quote-aware splitter, from the line `if [ -n "$PYBIN" ]; then` right after
`QSEGS=$COMMAND` (line 87) through `COMMAND=${COMMAND//[;&|]/$'\n'}` (line 121), with:
```bash
RAW=""
if [ -n "$PYBIN" ]; then
  Q=$(printf '%s' "$INPUT" | "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" segments 2>/dev/null)
  [ -n "$Q" ] && QSEGS=$Q
  # The raw command too, read as before plan 027 (CR dropped, continued lines joined). The
  # reader turns PowerShell ( ) into statement breaks and keeps a comma list in one word, so on
  # its text alone a Start-Process argument list ('push','--force',...), `& ("git") push ...` or
  # a refspec in parentheses passed, and all of them were refused before (plan 027 review).
  # Judging the raw text as well means reading PowerShell never makes this gate refuse less.
  RAW=$(printf '%s' "$INPUT" | "$PYBIN" -c '
import sys, json
try:
    d = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    c = str((d.get("tool_input") or {}).get("command") or "")
    c = c.replace("\r", "").replace("\\\n", " ").replace("`\n", " ")
    sys.stdout.buffer.write(c.encode("utf-8", "replace"))
except Exception:
    pass
' 2>/dev/null)
  [[ "$RAW" == "$COMMAND" ]] && RAW=""
fi
# The quote-blind split: both texts are cut at every ; & | first, and only then is a # comment
# dropped, per segment. A # that starts a word opens a comment in both shells, so a trunk named
# only in a trailing comment (`git push --force origin feature # bannerlord-1.5.x later`) is no
# longer refused (plan 027). Never strip per line before the split: a # inside a quoted value
# (`git log --grep "fix #1"; git push ...`) would delete every later command on its line, and
# after a heredoc line holding an apostrophe the quoted split cannot back that up. One tr and one
# sed over the whole text keep it linear (a bash read loop over both texts of a 1 MB PowerShell
# command took over 5 s). A command that is only a comment leaves the segments output empty, so
# QSEGS keeps the whole text and a trunk named there is still refused: a harmless over-block.
COMMAND=$(printf '%s\n%s\n' "$COMMAND" "$RAW" | tr ';&|' '\n\n\n' | sed -E 's/^#.*//; s/[[:space:]]#.*//')
```
(`RAW` is empty when it equals the POSIX text, which is every Bash command without a git named by
a path or in capitals, so a large Bash command is not split twice.)

Then update the comment block above it. Lines 72 to 82 read:
```text
# separator inside a quoted value (`git -C "E:/R&D" push --force ...`, `-o "a;b"`) cut git
# from push. So the command is also split at ; & | outside quotes only (QSEGS), with each
# shell's own escape (picked from tool_name), the splitter mark-verification-run.sh uses. With
# jq and no Python, QSEGS keeps whole lines. Neither a whole line nor a quoted split is safe on
# its own: an apostrophe in a comment or heredoc line (`# don't push`) opens a quote that never
# closes and glues the later lines into one segment, where the first `push` word is not the
# push. judge_command therefore anchors on the first `push` with a `git` before it (plan 011
# final convergence). The same glue can also refuse a later, unrelated command (`# it's a
# feature branch`, then an ordinary push and a `git log` naming a trunk); that over-block stays,
# since it errs on the safe side and ending a quote at a newline would let a quoted value that
# spans lines cut git from push.
```
Replace those eleven lines with:
```text
# separator inside a quoted value (`git -C "E:/R&D" push --force ...`, `-o "a;b"`) cut git
# from push. So the command is also split at ; & | outside quotes only (QSEGS): _shellwords.py
# segments, shared with mark-verification-run.sh, over the command already read as POSIX-shell
# text, so its escape is always \ (plan 027). With jq and no Python, QSEGS keeps whole lines.
# The quote-blind split reads both that POSIX text and the raw command (plan 027 review), so
# reading PowerShell never makes this gate refuse less than it did before.
# Neither a whole line nor a quoted split is safe on its own: an apostrophe in a heredoc line
# (`Don't push`) opens a quote that never closes and glues the later lines into one segment,
# where the first `push` word is not the push (a # comment is dropped from both splits since plan
# 027, so a comment's apostrophe no longer does this). judge_command therefore anchors on the
# first `push` with a `git` before it (plan 011 final convergence). The same glue can also refuse
# a later, unrelated command after such a heredoc line; that over-block stays, since it errs on
# the safe side and ending a quote at a newline would let a quoted value spanning lines cut git
# from push.
```

**7d.** In `judge_command`: add `p` to the `local` line (`local CLEAN PUSH_IDX GIT_SEEN FORCE ALL f ref i j tok skip p`);
replace `CLEAN=${1//[\"\'()]/ }` with
```bash
  # Braces and backticks go too (plan 027): PowerShell's `{git push ...}` and bash's
  # `` x=`git push ...` `` still show their `git` token. The closing brace in the bracket is
  # escaped (\}): a bare } there ends ${...} early, bash -n still passes, and every call then
  # prints "}: command not found" with CLEAN empty, which allows every force push.
  CLEAN=${1//[\"\'(){\}\`]/ }
```
Copy the `CLEAN=` line character for character: `{\}` inside the brackets, never `{}`.
replace the token loop
```bash
  for i in "${!TOKENS[@]}"; do
    case "${TOKENS[$i]}" in
      git | */git | git.exe | */git.exe) GIT_SEEN=1 ;;
      push) if (( GIT_SEEN )); then PUSH_IDX=$i; break; fi ;;
    esac
  done
```
with
```bash
  for i in "${!TOKENS[@]}"; do
    tok=${TOKENS[$i]}
    # git in any case and by any path is git (`GIT`, `git.exe`, C:\...\git.exe). The reader names it
    # `git` already; this keeps the jq-only fallback honest (plan 027). The subcommand stays exact:
    # git rejects `PUSH`.
    case "${tok,,}" in
      git | */git | *\\git | git.exe | */git.exe | *\\git.exe) GIT_SEEN=1; continue ;;
    esac
    if [[ "$tok" == push ]] && (( GIT_SEEN )); then PUSH_IDX=$i; break; fi
  done
```
in the option `case`, after the `--mirror) ...` line add
```bash
      -o | --push-option | --repo | --receive-pack | --exec)
        # The next word is this option's value, never the remote: `-o ci.skip origin` on a trunk
        # took ci.skip for the remote and origin for the refspec, and passed (plan 027).
        skip=1; continue ;;
```
and in the refspec loop replace `    ref="${ref#refs/heads/}"` with
```bash
    ref="${ref#refs/}"
    ref="${ref#heads/}"     # git reads a destination `heads/x` as refs/heads/x (plan 027)
```
and insert, between the `HEAD`/`@` block's closing `fi` and `if is_protected "$ref"; then`:
```bash
    if [[ "$ref" == *'*'* ]]; then
      # A pattern refspec (refs/heads/*, refs/heads/bannerlord-*) updates every branch it matches,
      # so it is judged as each protected name it matches (plan 027).
      for p in "${PROTECTED[@]}"; do
        # shellcheck disable=SC2053  # $ref is the refspec's glob, matched as a pattern on purpose
        if [[ "$p" == $ref ]]; then ref=$p; break; fi
      done
    fi
```

**Verify** (in this order):

1. `bash -n .claude/hooks/validate-push.sh` prints nothing, and
   `grep -c 'tool_name' .claude/hooks/validate-push.sh` prints `0` (the reader owns the shell
   choice now). `bash -n` cannot catch an expansion that breaks only at runtime, so 2 runs the hook.
2. **Runtime smoke.** Write three payload files with the Write tool (never type these commands in
   a Bash line; see "The live gates see your commands"), each one line:
   - `E:/repos/taom-improve/scratch/plans/027/vp-smoke-bash.json`:
     `{"tool_name":"Bash","tool_input":{"command":"git push --force origin bannerlord-1.5.x"},"hook_event_name":"PreToolUse"}`
   - `E:/repos/taom-improve/scratch/plans/027/vp-smoke-ps.json`:
     `{"tool_name":"PowerShell","tool_input":{"command":"if ($true) {git push --force origin 'refs/heads/*'}"},"hook_event_name":"PreToolUse"}`
   - `E:/repos/taom-improve/scratch/plans/027/vp-smoke-ok.json`:
     `{"tool_name":"Bash","tool_input":{"command":"git push --force origin feature"},"hook_event_name":"PreToolUse"}`

   Then run
   `cd /e/repos/taom-improve/wt-027 && S=/e/repos/taom-improve/scratch/plans/027 && for f in bash ps ok; do bash .claude/hooks/validate-push.sh < $S/vp-smoke-$f.json > /dev/null 2> $S/vp-smoke-$f.err; echo "$f rc=$?"; done; cat $S/vp-smoke-*.err | grep -c 'command not found'`.
   Expected, exactly: `bash rc=2`, `ps rc=2`, `ok rc=0`, then `0`. Checked at planning against
   this step's text spliced into a scratch copy of the hook: that is the output with `{\}` in the
   `CLEAN=` line, while `{}` there gives `bash rc=0`, `ps rc=0` and a count of `2`. Anything else:
   fix the step's text you copied (start with the `CLEAN=` line), never the expectation.
3. `bash tools/test_hooks.sh` (with the `tmpenv.sh` prefix): `0 failed`, passed = the Step 5 count
   + 47 (12 in 6a, 21 in 6b, 7 branch rows under 2 tools in 6c) = **698**. If one of the eleven
   "Plan 027 review" rows fails here, the 7c block was not copied exactly (the `RAW` read, or the
   split before the `#` strip): fix the copy, never the row.

Then commit 3: `git add .claude/hooks/validate-push.sh tools/test_hooks.sh`, subject
`fix(hooks): v2.0.30 - close validate-push glob, option and comment gaps`. The body lists each
shape, cites decisions 30 and 60 (still force-push only, still the four named branches) and the
plan 011 probe that proved the glob shapes against a real remote, and says the quote-blind split
also reads the raw command, so reading PowerShell never makes the gate refuse less than before.

### Step 8 (RED): mark-verification-run rows

In `tools/test_hooks.sh` section 7d:

**8a.** In `MVR_TOOL_CASES`, after `  "PowerShell|1|cd E:/x"$'\n'"./build.ps1"$'\n'"echo done"` add:
```bash
  # Plan 027: the PowerShell reader splits a script block and drops a comment.
  'PowerShell|1|if ($true) { dotnet test TAOM.Tests }'
  "PowerShell|0|Write-Output @'"$'\n'"dotnet test TAOM.Tests"$'\n'"'@"
  'PowerShell|1|git status; dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= # built here'
  # The reader quotes a word holding \, so PowerShell's everyday .\build.ps1 arrives as '.\build.ps1'.
  'PowerShell|1|.\build.ps1 -RunTests'
```

**8b.** Immediately before the final `rm -rf "$MVR_DIR"` of section 7d add:
```bash
# PostToolUseFailure carries "is_interrupt": true when the tool call was aborted, so there is no
# result to count (plan 027; Claude Code 2.1.241 sets it from an abort error). A command that only
# mentions the field still marks: inside a JSON string its quotes are escaped.
for pair in '0|true' '1|false'; do
    want="${pair%%|*}"; irq="${pair#*|}"
    rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
    printf '{"tool_name":"PowerShell","tool_input":{"command":"dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId="},"hook_event_name":"PostToolUseFailure","error":"Interrupted","is_interrupt":%s}' "$irq" \
        | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
    got=0; [[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && got=1
    [[ "$got" == "$want" ]] && ok "mark-verification-run marked=$got for a PostToolUseFailure with is_interrupt $irq" \
        || bad "mark-verification-run expected marked=$want, got $got for a PostToolUseFailure with is_interrupt $irq"
done
rm -rf "$MVR_DIR"; mkdir -p "$MVR_DIR"
"$HPY" -c 'import json; print(json.dumps({"tool_name":"Bash","tool_input":{"command":"dotnet test TAOM.Tests; echo \"is_interrupt\":true"},"hook_event_name":"PostToolUse"}))' \
    | timeout -k 2 10 env CLAUDE_PROJECT_DIR="$MVR_DIR" bash .claude/hooks/mark-verification-run.sh >/dev/null 2>&1
[[ -f "$MVR_DIR/.claude/logs/.verification-ran" ]] && ok "mark-verification-run still marks a command that mentions is_interrupt" \
    || bad "mark-verification-run did not mark a command whose text mentions is_interrupt"
```

**Verify**: `bash tools/test_hooks.sh > /e/repos/taom-improve/scratch/plans/027/step8.txt 2>&1; grep -c 'FAIL' /e/repos/taom-improve/scratch/plans/027/step8.txt`.
Expected: exactly **2** failures: `if ($true) { dotnet test TAOM.Tests }` and the
`is_interrupt true` row. The `.\build.ps1 -RunTests` row passes today (the raw command's first
word matches `*[/\\]build.ps1`); 9c keeps it passing once the reader feeds the split.

### Step 9 (GREEN): mark-verification-run

In `E:/repos/taom-improve/wt-027/.claude/hooks/mark-verification-run.sh`:

**9a.** After the prefilter line
`[[ "$INPUT" == *dotnet* || "$INPUT" == *build.ps1* || "$INPUT" == *'\u'* ]] || exit 0` add:
```bash

# PostToolUseFailure carries "is_interrupt": true when the tool call was aborted: the build or test
# never finished, so there is no result to count, and marking would mute the Stop reminder with
# nothing in hand (plan 027). Claude Code 2.1.241 sets it from the thrown error being an abort;
# whether a timed-out command sets it is UNVERIFIED, so a timeout still marks. Inside a JSON string
# a quote is escaped, so a command that merely mentions the field cannot match.
IRQ='"is_interrupt"[[:space:]]*:[[:space:]]*true'
[[ "$INPUT" =~ $IRQ ]] && exit 0
```

**9b.** Replace the splitter, from `SEGMENTS=""` (line 54) through the closing `fi` (line 87),
with:
```bash
SEGMENTS=""
if [ -n "$PYBIN" ]; then
  SEGMENTS=$(printf '%s' "$INPUT" | "$PYBIN" "$TAOM_HOOKS_DIR/_shellwords.py" segments 2>/dev/null)
fi
```
and in the comment above it replace these six lines (48 to 53):
```text
# The split runs in Python (plan 011 review): a per-character bash loop was quadratic (9.5 s for
# a 100 KB command, past the 5 s registration), and it read `\` as the escape in PowerShell,
# where the escape is a backtick, so `Write-Output "x`"; dotnet test"` marked and
# `Set-Location "E:\x\"; dotnet test` did not. An escaped newline is a continuation, so it joins.
# CR goes: Python's print writes CRLF on Windows, which hid a command on a non-final line.
# Output is written as bytes, LF only.
```
with:
```text
# The split is _shellwords.py segments (plan 027), shared with validate-push.sh: the command is
# first read as POSIX-shell text (a PowerShell command as the Bash text of the same command), then
# split outside quotes with \ as the escape, in time linear in its length (a per-character bash
# loop took 9.5 s on a 100 KB command, plan 011 review), and a # comment is dropped. An escaped
# newline joins its lines, and the output is bytes with LF only, so a CR never hides a command.
```

**9c.** The reader quotes any word holding a `\` (`quote()` in `_shellwords.py`), so PowerShell's
`.\build.ps1 -RunTests` reaches the `case` as `'.\build.ps1' -RunTests` and no longer matches
`*[/\\]build.ps1` (measured at plan review: `.\build.ps1 -RunTests`, `& .\build.ps1 -RunTests`,
`E:\repos\TAOM\build.ps1` and `Set-Location E:\repos\TAOM; .\build.ps1` marked before and not
after). After the line `  first="${seg%% *}"` add:
```bash
  first=${first//\'/}                            # the reader quotes a word holding \ (plan 027)
```

**Verify**: `bash -n .claude/hooks/mark-verification-run.sh` prints nothing; `bash tools/test_hooks.sh`:
`0 failed`, passed = **705** (698 + 4 + 3).

Then commit 4: `git add .claude/hooks/mark-verification-run.sh tools/test_hooks.sh`, subject
`fix(hooks): v2.0.30 - leave an interrupted build or test unmarked`. The body quotes the evidence
(the 2.1.241 schema and the `de||se` call site, in words) and states the timeout case as
UNVERIFIED.

### Step 10: Docs

**10a. `docs/reference/hooks-catalog.md`.** Seven Edits. Each `old` block below is exact file
text: Edits 1 and 4 to 7 are pieces of one long line each (the file does not wrap them), Edit 2
spans two lines. Copy each `old` block exactly into the Edit `old_string` and its `new` block into
`new_string`.

Edit 1, line 3. Prove the numbers first: `git ls-files .claude/hooks | wc -l` prints 27 (tracked
files, so a gitignored `__pycache__` cannot count);
`grep -c 'type: command' .claude/skills/freeze/SKILL.md .claude/skills/investigate/SKILL.md`
prints `.claude/skills/freeze/SKILL.md:3` and `.claude/skills/investigate/SKILL.md:2` (the 5
frontmatter registrations; section 2's own line says 29 because its scan counts 3 of them); and
`python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));print(sum(len(g['hooks']) for gs in d['hooks'].values() for g in gs))"`
prints 26. Then old:

```text
**Recounted 2026-09-25 (after plans 011 and 020): 26 scripts on disk, 27 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 32 total.** Two scripts, `_pybin.sh` and `_stop_reminder.sh`, are sourced helpers with no registration of their own.
```

new:

```text
**Recounted 2026-09-25 (after plan 027): 27 files on disk (26 scripts and the `_shellwords.py` reader), 26 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 31 total.** Three files, `_pybin.sh`, `_stop_reminder.sh` and `_shellwords.py`, are helpers with no registration of their own. Plan 027 folded the PreToolUse `PowerShell` group (it held only `validate-push.sh`) into one `Bash|PowerShell` group, which is why the count fell by one.
```

Edit 2, the end of the blockquote above the table (lines 43 and 44; this one spans two lines).
old:

```text
> prove the gates under the real harness: a two-line `cd` then
> `git commit --dry-run -m "no label here"` must still be denied.
```

new:

```text
> prove the gates under the real harness: a two-line `cd` then
> `git commit --dry-run -m "no label here"` must still be denied, from the Bash tool and from the
> PowerShell tool.
>
> **Both shell tools (plan 027, maintainer decision 61).** Every PreToolUse git gate is
> registered in one `Bash|PowerShell` matcher group; a hook matched on `Bash` alone never sees a
> PowerShell tool call. Each gate reads its command through `taom_hook_command posix`
> (`_pybin.sh`), which runs `.claude/hooks/_shellwords.py`: a PowerShell command comes back as
> the Bash text of the same command (here-strings, `''` and `""` quoting, backtick escapes and
> continuations, `#` and `<# #>` comments, `{ }` and `( )` as statement breaks, the `&` call
> operator), and in both shells a git named by a path or in capitals comes back as `git`, so
> each gate keeps its Bash logic. Text it cannot follow (an unclosed quote) comes back unchanged,
> and if the reader fails the gate reads the raw command as Bash text and says so on stderr.
> `tools/test_hooks.sh` 7e runs each gate's rows under both tool names;
> `tools/tests/test_shellwords.py` pins the reader to PowerShell 7's own parser output. Not read
> by the commit and confirm gates: a `$(...)` inside a double-quoted PowerShell string; a gated
> word spelled with an escape inside it (`--no-verif''y`), which the raw prefilter cannot see; and
> git run through another command, such as `& ("git") commit ...`,
> `Start-Process git -ArgumentList ...`, `pwsh -c "git commit ..."` or
> `Invoke-Expression 'git reset --hard'`. `validate-push.sh` also judges the raw command, so it
> still refuses such a force push. Deliberate obfuscation is out of scope.
```

Edit 3: `replace_all` `| PreToolUse (Bash) |` with `| PreToolUse (Bash, PowerShell) |` (8 rows).

Edits 4 to 6 are inside the `validate-push.sh` row (line 60). Edit 4 old:

```text
the quoted split uses each shell's own escape, picked from `tool_name`.
```

new:

```text
the quoted split is `_shellwords.py segments` over the command read as POSIX-shell text (plan 027), so its escape is always `\`. The quote-blind split reads that text and the raw command both, so reading PowerShell never makes the gate refuse less than before (a comma argument list such as `Start-Process git -ArgumentList 'push','--force',...` or `& ("git") push` stays refused), and it drops a `#` comment per segment, after the split, never per line (a `#` inside a quoted value must not hide a later push).
```

Edit 5 old:

```text
an apostrophe in a comment or heredoc line (`# don't push`)
```

new:

```text
an apostrophe in a heredoc line (`Don't push`; a `#` comment is dropped from both splits since plan 027)
```

Edit 6 old:

```text
Registered for both shell tools since plan 011.
```

new:

```text
Registered for both shell tools since plan 011. Since plan 027 it judges a pattern refspec (`refs/heads/*`, `refs/heads/bannerlord-*`) as every protected name it matches and a `heads/<name>` destination as `<name>`, never takes the value of `-o`, `--push-option`, `--repo`, `--receive-pack` or `--exec` for the remote, and ignores a trunk named only in a `#` comment.
```

Edit 7, inside the `mark-verification-run.sh` row (line 65). old:

```text
The split runs in Python with each shell's own escape (a backtick for PowerShell, `\` for Bash), linear in the command's length (plan 011 review).
```

new:

```text
The split is `_shellwords.py segments` (plan 027): the command is read as POSIX-shell text first, so a PowerShell command splits as its Bash twin, linear in the command's length, and the first word is read without quotes, so PowerShell's `.\build.ps1` still marks. A PostToolUseFailure with `is_interrupt` true (an aborted call, so no result) does not mark; whether a timed-out command sets it is UNVERIFIED (plan 027).
```

**10b. `.claude/rules/hook-authoring.md`.** Line 28 reads
```text
When writing a PreToolUse(Bash) hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch — substring matching `*"git commit"*` MISSES the following real-world forms (Codex review 2026-04-26 found this gap):
```
Replace it with (the same sentence, both shells, and a colon where the dash was, since you are
rewriting the line):
```text
When writing a PreToolUse hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch: substring matching `*"git commit"*` MISSES the following real-world forms (Codex review 2026-04-26 found this gap):
```
After line 56, the paragraph that ends "The `/skill-stocktake` checklist now includes this check."
(leave that paragraph as it is), add:

```markdown
**Both shell tools (plan 027).** Register a git gate in the `Bash|PowerShell` matcher group and
read its command with `COMMAND=$(taom_hook_command posix <gate>)` (`_pybin.sh`), never from
`tool_input.command` directly: it hands PowerShell back as Bash text, so the forms above cover both
shells, and it names git `git` when it is called by a path or in capitals. What it resolves, each
with a `tools/test_hooks.sh` 7e row under both tool names:

| PowerShell form | Reads as |
|---|---|
| `@'`...`'@` and `@"`...`"@` here-strings | one quoted word |
| `'it''s'`, `"a""b"`, a backtick escape | `it's`, `a"b`, the escaped character |
| a trailing backtick | the next line joined |
| `& git`, `& 'C:\...\git.exe'`, `GIT`, `git.exe` | `git` |
| `if ($x) { git ... }`, `ForEach-Object {git ...}`, `$(git ...)` | a separate command |
| `# ...`, `<# ... #>` | dropped |
```

**10c. `.claude/rules/harness-facts.md`.** In the "Hook lifecycle" table, after the row that
starts "| 30 hook events exist", add two rows:

```markdown
| The PowerShell tool is its own tool: `tool_name` `"PowerShell"`, the command in `tool_input.command`. A hook matched on `Bash` never sees it, so every git gate registers in one `Bash\|PowerShell` group. | EMPIRICAL 2026-09-25: PowerShell tool calls were refused by `validate-push.sh` through the `PowerShell` matcher | The gates read either shell through `_shellwords.py` (`hook-authoring.md`, plan 027). |
| PostToolUseFailure input: `tool_name`, `tool_input`, `tool_use_id`, `error`, and optional `is_interrupt` and `duration_ms`. `is_interrupt` is true when the tool call was aborted. 2.1.241 sends no `is_timeout` or `error_type`, though the event's description names them; whether a timeout sets `is_interrupt` is UNVERIFIED. | EMPIRICAL 2026-09-25: the PostToolUseFailure schema and builder in the 2.1.241 binary (`grep -a -o '.\{0,300\}is_interrupt.\{0,300\}' ~/.local/bin/claude.exe`) | `mark-verification-run.sh` leaves an interrupted build or test unmarked. |
```

**10d. `docs/reference/mcp-servers.md`.** Replace
``**Why:** every safety hook in this repo is registered against `matcher: "Bash"`, and`` with
``**Why:** every git safety hook in this repo is registered against `matcher: "Bash|PowerShell"` (plan 027), and``.

**Verify**:
- `python tools/lint_docs.py` exits 0 and `python tools/lint_docs.py --fail-on-drift` exits 0 (a
  `size-warn` for `hook-authoring.md` or `harness-facts.md` is report-only; any failing finding:
  STOP).
- `grep -c '| PreToolUse (Bash) |' docs/reference/hooks-catalog.md` prints `0` and
  `grep -c '| PreToolUse (Bash, PowerShell) |' docs/reference/hooks-catalog.md` prints `9`.
- `git diff -U0 -- docs .claude/rules | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print('\n'.join(l for l in t.splitlines() if l.startswith('+') and (chr(0x2013) in l or chr(0x2014) in l)))"`
  prints an empty line only (no added line holds an em or en dash).

Then commit 5: `git add docs/reference/hooks-catalog.md docs/reference/mcp-servers.md .claude/rules/hook-authoring.md .claude/rules/harness-facts.md`,
subject `docs(hooks): v2.0.30 - catalogue the PowerShell gate coverage`.

### Step 11: Final verification

Run each and save the output under the scratch directory (items 1, 3, 6 and 7 with the
`tmpenv.sh` prefix):

1. `bash tools/test_hooks.sh`: `705 passed, 0 failed` (Step 1 baseline B = 482, plus 223; if Step 1
   recorded another B, the line is `<B + 223> passed, 0 failed`).
2. `python -m unittest tools.tests.test_shellwords -v`: `Ran 33 tests`, `OK`.
3. `python -m unittest discover -s tools/tests -t .`: `Ran 2159 tests`, `FAILED (failures=2)`, the
   two failures exactly the baseline pair named in "Current state".
4. `python tools/lint_docs.py --fail-on-drift`: exit 0.
5. `python tools/audit_claude_config.py --min HIGH --no-repo-secrets`: no findings, exit 0.
6. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
7. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: `Passed 10767, Failed 0,
   Skipped 2` (no C# changed; any failure: STOP and report, do not fix C#).
8. `git status --short`: only `?? plans/027-powershell-gate-coverage.md`.
9. `git log --format=%s 96afb6fb..HEAD`: the five subjects of the Git workflow section, newest
   first.
10. `git ls-files .claude/hooks/_shellwords.py tools/tests/test_shellwords.py`: both paths.
11. The commits touch exactly the 19 in-scope files:
    `diff <(git diff --name-only 96afb6fb..HEAD | LC_ALL=C sort) <(printf '%s\n' .claude/hooks/_pybin.sh .claude/hooks/_shellwords.py .claude/hooks/block-broad-git-add.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/block-no-verify.sh .claude/hooks/check-claude-files-tracked.sh .claude/hooks/check-commit-subject-version.sh .claude/hooks/check-doc-config-drift.sh .claude/hooks/check-moduledata-validation.sh .claude/hooks/check-native-dll-crt.sh .claude/hooks/mark-verification-run.sh .claude/hooks/validate-push.sh .claude/rules/harness-facts.md .claude/rules/hook-authoring.md .claude/settings.json docs/reference/hooks-catalog.md docs/reference/mcp-servers.md tools/test_hooks.sh tools/tests/test_shellwords.py | LC_ALL=C sort) && echo SCOPE-OK`
    prints only `SCOPE-OK`.

Report the outputs, the step-4, step-6 and step-8 RED counts you saw, and any row that passed
before its implementation step.

## Test plan

- **Unit** (`tools/tests/test_shellwords.py`, 33 tests, modelled on the stdlib-unittest shape of
  `tools/tests/test_bind_hill_troll_action_set.py`): PowerShell quoting (9), statements and
  operators (11), the git word in both shells (4), the shared splitter (5), the command-line
  interface (4). Every PowerShell expectation was read from PowerShell 7's parser at planning.
- **Harness** (`tools/test_hooks.sh`, 223 new rows): section 4 +25 (the `powershell-trigger`
  contract payload over every hook), section 6 +3 (git in capitals, one word piped to `-F -` both
  ways), section 7b (the `.py` helper assertion, no new row), section 7c +47 (12 Bash shapes; 21
  tool rows: 12 PowerShell shapes, then 4 Bash and 5 PowerShell shapes refused before plan 027
  that must stay refused; 7 branch rows under 2 tools), section 7d +7 (4 PowerShell splits, the
  `.uild.ps1` spelling among them, 3 `is_interrupt` rows), section 7e +141 (9 registration
  parity, 18 PowerShell prefilter, 58 verdicts (one pins the known `--no-verif''y` gap), 28 commit-test traces, 1 PowerShell deny over an untracked skill, 27 timings of three
  large payloads per gate).
- **Every cell covered per gate**: each confirm gate has an ask and an allow under PowerShell and a
  Bash parity row for git in capitals or by path; each commit gate has a detection row per
  PowerShell shape and a `commit-tree` or `log --grep commit` non-detection row.
- **Structurally untestable here**: a real tool call through the live harness (your session runs
  the main tree's hooks). Commit trailer: `Not-tested: a live PowerShell tool call through the
  real harness (owed after merge)`.

## Done criteria

ALL must hold:

- [ ] `bash tools/test_hooks.sh` prints `705 passed, 0 failed` (with a Step 1 baseline B other
      than 482: `<B + 223> passed, 0 failed`). Never add or drop a row to hit a number.
- [ ] `python -m unittest tools.tests.test_shellwords -v` prints `Ran 33 tests` and `OK`.
- [ ] `python -m unittest discover -s tools/tests -t .` prints `Ran 2159 tests` with only the two
      baseline failures.
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0; `python tools/lint_docs.py` exits 0.
- [ ] `python tools/audit_claude_config.py --min HIGH --no-repo-secrets` reports no findings.
- [ ] The settings check of Step 0 prints the `('Bash|PowerShell', [...nine...])` list.
- [ ] `grep -L 'taom_hook_command posix' .claude/hooks/{block-no-verify,check-claude-files-tracked,check-commit-subject-version,block-dangerous-git,block-broad-git-add,check-moduledata-validation,check-native-dll-crt,check-doc-config-drift,validate-push}.sh`
      prints nothing, and `grep -c 'tool_name' .claude/hooks/validate-push.sh` prints `0`.
- [ ] `git grep -n 'PreToolUse(Bash)' -- .claude/hooks` prints nothing.
- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 and
      `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` reports Failed 0.
- [ ] `git status --short` shows only `?? plans/027-powershell-gate-coverage.md`;
      `git log --format=%s 96afb6fb..HEAD` prints exactly the five subjects; the Step 11 item 11
      `diff` prints only `SCOPE-OK`.

## STOP conditions

Stop and report back (do not improvise) if:

- The drift check prints anything, `git status` shows a path other than `.claude/settings.json` and
  this plan file before you start, or the Step 0 check does not print the expected list.
- The baseline hook suite has any failure after one rerun (the check-8 `scan.sh` flake excepted).
- A Step 2 expectation fails and the only fix would be to change the expectation: those values are
  PowerShell's own parse, so the reader is wrong, not the test.
- Any pre-existing hook-suite row (one this plan did not add) fails at any step, or a fix seems to
  need a pre-existing row's expected value changed. The only edits to existing test code are the
  ones Step 4 and Step 6 spell out.
- A RED count differs from 40 (41 with the one timing row Step 4 names), 19 or 2 because a row
  this plan says passes today fails. (When the
  extra failures are only load-sensitive rows, a `scan.sh` `exit 124` or a `took <N>ms` timing
  row, rerun once with nothing else running before you decide.)
- A timing row (4b or 7e) exceeds 80% of its registration twice in a row at Step 5 or later: the
  reader or a gate is too slow, and the fix is a design question. (At Step 4 the one
  `block-broad-git-add.sh ... ps-lines` timing row may fail; Step 5g removes its cause.)
- The fix appears to need `.claude/settings.json` changed again, or any out-of-scope file (above
  all `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`).
- `git init -b` fails in the Step 6c sandbox (a git older than 2.28).
- One of your own tool calls is refused by a hook message you did not expect: your session may be
  running the worktree's half-edited hooks. Do not work around it.
- `python tools/audit_claude_config.py --min HIGH --no-repo-secrets` reports a finding, or
  `lint_docs.py --fail-on-drift` fails.
- The assumption "the PowerShell tool payload carries `tool_name` `PowerShell` and the command in
  `tool_input.command`" turns out false anywhere you look.

## Maintenance notes

- **What a reviewer should probe** (the orchestrator runs `/deep-review` and, for hooks and
  settings, `/security-scan`): the here-string terminator rule (`'@` must start a line); `&` as
  call operator versus background (decided by the previous token); a redirection only at a word's
  start or after a lone fd (`2>`, `*>`), matching PowerShell's parser; the reader's fallback (raw
  text on an unreadable input, raw command plus a stderr note on a reader failure); the jq-first to
  Python-first order change in four gates; `BASH_GIT` touching only a command-position git word
  (known over-reach, checked at planning: it anchors on any newline, so a line starting `Git` or
  `GIT` inside a heredoc or a multi-line quoted commit body is renamed too, and
  `git commit -F - <<'EOF'`, `Git reset --hard is gated`, `EOF` reads as holding
  `git reset --hard`; the confirm gates' per-line split already asked on the lowercase form before
  plan 027, so this only widens an over-block to the capitalised spelling, and no row tests it);
  the validate-push glob loop (a refspec pattern matched against the four names), the `heads/`
  strip, the option-value skip (`--repo` included: git 2.55.0 takes the next positional as the
  repository, `git push --repo origin --force b2` failed with "'b2' does not appear to be a git
  repository", so the rc 0 for `git push --repo origin --force <trunk>` is not a hole); the
  quote-blind split (Step 7c: it judges the raw command beside the reader's text, and cuts at
  `; & |` BEFORE it drops a ` #` comment per segment; the second plan review showed a per-line
  strip letting `git log --grep "fix #1"; git push --force origin <trunk>` through after a
  heredoc `Don't` line, and PowerShell `( )` and comma lists passing on the reader's text alone;
  the eleven "Plan 027 review" rows in 6b and 6c pin both); a command that is only a comment
  (`# git push --force origin <trunk>`) is still refused in Bash, because the segments output is
  empty and QSEGS keeps the whole text: a harmless over-block, do not "fix" that fallback (in
  PowerShell it passes, checked at plan revision); the `ps_tokens` word buffer (a list of pieces, timed at
  planning at 0.44 s for a 1 MB unquoted word, where the string-append version took 7.3 s); the
  `is_interrupt` regex on the raw payload; the `-F -` pipe support in
  `check-commit-subject-version.sh`.
- **Live checks owed after merge** (restart the session so the main tree's settings reload), each
  through the PowerShell tool, expecting the stated refusal: `git commit --dry-run -m "no label here"`
  (denied by the label gate); `git commit --dry-run -m @'` newline `docs: v2.0.30 - live probe`
  newline `'@` (allowed); `GIT add --dry-run -A` (an ask: decline it);
  `git commit --dry-run --no-verify -m x` (blocked); `git push --dry-run --force origin HEAD:bannerlord-1.5.x`
  (blocked). Record the outcomes in the hooks catalog's review trail.
- **Deferred, both shells, with evidence**: `git commit -n` is `--no-verify` for commit, and git
  2.55.0 accepts the abbreviation `--no-veri` (`git commit --no-veri -h` printed the usage with no
  "unknown option" error), so `block-no-verify.sh`'s substring test misses both; the commit gates'
  first `case` allows any command holding `git commit-` (so `git commit-graph write && git commit
  -m "no label"` passes every commit gate); the confirm gates' global-option regex misses a quoted
  `-C` value holding a space; the confirm gates do not join a Bash `\`-newline continuation or
  look inside `(git ...)` or `"$(git ...)"`; in Bash, `git  commit` with two spaces or a tab
  between the words passes `check-commit-subject-version.sh` before and after this plan (its
  two-stage matcher wants one space; checked at plan revision: `git  commit -m "docs: no label"`
  allowed in Bash, denied through the PowerShell reader, which rejoins words with one space); in
  PowerShell, git run through another command (`& ("git") commit ...`,
  `Start-Process git -ArgumentList ...`, `pwsh -c "git commit ..."`,
  `Invoke-Expression 'git reset --hard'`) passes the commit and confirm gates (they were not
  registered for PowerShell before, so no regression; `validate-push.sh` judges the raw command
  and still refuses the force-push forms). Each is a candidate follow-up issue.
- **Timing headroom**: the 7e rows cover 100 KB and 100-line payloads. Not a planned row: a 1 MB
  unquoted PowerShell word took `validate-push.sh` 3.6 to 4.3 s at plan revision on a loaded
  machine (2.3 to 2.6 s with the unchanged hook; its registration is 5 s). The remaining cost is the
  final `while read` loop over the split text, which predates this plan.
- **Future interaction**: any new PreToolUse git gate goes in the `Bash|PowerShell` group and reads
  `taom_hook_command posix`; 7e's parity rows fail if it is registered for one shell only. If the
  PowerShell tool's `tool_name` changes in a Claude Code upgrade, 7e still passes (it builds its own
  payloads), so re-run the live checks above after every upgrade.
- **CHANGELOG**: none; it is generated at `/release` from the commit bodies (plan 020).
