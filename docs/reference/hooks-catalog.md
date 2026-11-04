# Hooks Catalog

> What each `.claude/hooks/` script does, which event runs it, how it reaches Claude and what pins it. The detail lives in each script's header. `tools/test_hooks.sh` section 2 counts the registrations. Authoring: `.claude/rules/hook-authoring.md`. Lifecycle: `.claude/rules/harness-facts.md`.

## What binds whom

| Mechanism | Binds | Notes |
|---|---|---|
| `.githooks/commit-msg`, `pre-commit`, `pre-merge-commit` | every committer whose clone sets `core.hooksPath` | Terminal and IDE commits included; see "Git hooks" below. |
| Everything in `.claude/hooks/` | Claude's own commands only | A commit typed in a terminal or an IDE meets none of them. |
| CI | nothing | There is none (the GitHub workflows were removed on 2026-10-05). |

Maintainer decisions behind the rows: D29 (GitHub protects no branch, so `validate-push.sh` is the only force-push guard), D30 (trunks named, not `bannerlord-*`), D39 and D40 (prefilter on the gated word, never skip on a `\u` escape; see `hook-authoring.md`), D41 (the verification marker reads quotes), plan 027 and decision 61 (every git gate registers once for `Bash|PowerShell` and reads the command through `taom_hook_command`). Helpers with no registration of their own: `_pybin.sh`, `_stop_reminder.sh`, `_shellwords.py` (the command reader), `_pushjudge.py` (the push judge).

## Hooks

Every row's last column is "Claude's commands only". Channel is how the result reaches Claude: a `deny` reason, exit-2 stderr, an `ask` (shown to the user), SessionStart stdout, `additionalContext` beside a tool result, or a Stop `decision: block`.

| Hook | Event | Channel | Does | Fails how | Pinned by | Who it covers |
|---|---|---|---|---|---|---|
| `session-start.sh` | SessionStart (startup; compact) | stdout | Prints branch, recent commits, hook-toolchain health, game-version and Armory art drift, graphify freshness, native-build archive, worktree and stash counts, an untagged module version, a `core.hooksPath` that misses `.githooks`, a `MEMORY.md` over ADR-011's limits, and `HARNESS RETRO DUE`. On compact: recent Dead ends only. | Fail-open; a spent 7.5 s probe budget or an overrun prints UNCHECKED | 3, 5b2, 7l, 7n | Claude's commands only |
| `upstream-watch.sh` | SessionStart (startup) | stdout | `UPSTREAM AHEAD` and `UPSTREAM RELEASE` for repos in `docs/reviews/upstream-watch.json`, via `tools/upstream_watch.py` (one read per repo, 20 h cache). Response: `/upstream-review`. | Fail-open; every failure or overrun prints UNCHECKED; ends by 9 s | 3, 7l | Claude's commands only |
| `detect-docs-gaps.sh` | SessionStart (startup) | stdout | Flags a `Main/Features/<X>` folder with no `docs/features/<x>.md`. | Fail-open; about 0.5 s | 7l2 | Claude's commands only |
| `block-no-verify.sh` | PreToolUse Bash, PowerShell | exit-2 stderr | Refuses `--no-verify` (and git's prefixes of it) on a command that takes it, `git commit -n` and a one-call `-c core.hooksPath` or `--config-env`, read through `_shellwords.py bypass` as bash reads them, so quoting cannot hide a flag and a message cannot trip it; `git push -n` passes. | Fail-open; no reader: the raw command, which over-blocks a mention | 4c, 4d, 7e; `test_shellwords.py` | Claude's commands only |
| `validate-push.sh` | PreToolUse Bash, PowerShell | exit-2 stderr; `ask` on no verdict | Refuses a force push (any force flag, a `+` refspec, `--mirror`, `--all`, matching refs) to `master`, `main`, `bannerlord-1.5.x`, `bannerlord-1.4.5`. A plain push passes silently. | Asks after about 3.3 s on an overrun; no verdict: asks on a force marker | 4c, 4d, 7c, 7e, 7i, `test_pushjudge.py` | Claude's commands only |
| `check-claude-files-tracked.sh` | PreToolUse Bash, PowerShell | `deny` | Refuses `git commit` while a file under `.claude/{skills,agents,rules,hooks}/` is gitignored, so it would not ship. Judges the repository the commit runs in. | Fail-open; asks on a 3 s overrun | 4c, 7b, 7e | Claude's commands only |
| `check-commit-subject-version.sh` | PreToolUse Bash, PowerShell | `deny` | Refuses a subject that is not `<type>[(scope)]: vX.Y.Z - <description>` with the committed `<Version>` of `SubModule.xml`, and any AI attribution line in the message. | Fail-open | 4c, 4d, 6, 7b, 7e | Claude's commands only |
| `block-dangerous-git.sh` | PreToolUse Bash, PowerShell | `ask` | Confirms before `reset --hard`, `clean -f`, `branch -D`, a discarding `checkout` or `restore`, `stash drop`, `worktree remove --force`, `reflog expire`, `update-ref -d`. Push belongs to `validate-push.sh`. | Fail-open; no Python: a `sed` split | 4c, 4d, 5c2, 7e, 7h | Claude's commands only |
| `block-broad-git-add.sh` | PreToolUse Bash, PowerShell | `ask` | Confirms before `git add -A`, `-u`, `.` and `git commit -a`, listing what the sweep would stage. | Fail-open; no Python: a `sed` split | 4c, 4d, 5c2, 7b, 7e | Claude's commands only |
| `check-moduledata-validation.sh` | PreToolUse Bash, PowerShell | `deny` | Refuses a commit with staged TAOM ModuleData XML or XSLT while `tools/validate_moduledata.py` or `generate_career_kits.py --verify` reports an ERROR. It also reads the live `TAOM_Map` and Armory, so the finding may not be in your files: fix it or ask the owner. | Fail-open; a validator crash is allowed with a note | 4c, 7b, 7e, 7h, 7m | Claude's commands only |
| `check-doc-config-drift.sh` | PreToolUse Bash, PowerShell | `deny` | Refuses a commit when `tools/lint_docs.py --drift-only --staged` finds config-example drift, a version mismatch, a context-budget breach, a bare RCA `FOLLOW-UP`, a feature doc with no verify section, or rule text drift. | Fail-open; 20 s inner bound; an exit 1 with no finding to quote allows and says so | 4c, 5d, 7b, 7e, 7h | Claude's commands only |
| `check-graphify-usage.sh` | PreToolUse Bash, PowerShell | `deny` | Denies a raw graphify write verb and `graphify-mcp`, naming `tools/graphify_taom.py`; query verbs pass (ADR-012, #677). | Fail-open; asks on a 5 s overrun | 4c, 4d, 7e, 7f, `test_graphify_taom.py` | Claude's commands only |
| `check-bash-length.sh` | PreToolUse Bash | exit-2 stderr | Refuses a Bash command of 7,500 or more characters (each `'` counted as 4), which the tool on this machine cuts short (#962). The reason names the Write tool. | Fail-open; payloads under 5,500 characters exit before Python | 4c, 7l3 | Claude's commands only |
| `config-protection.sh` | PreToolUse Edit\|Write | exit-2 stderr | Blocks edits to `Directory.Build.props`, `settings.json`, `settings.local.json` and an existing ADR. A new ADR path and the ADR README pass. The flag file `/tmp/claude-config-override-<session id>` unlocks one session; the caller deletes it. | Fail-open; prefilter exits before Python | 7g | Claude's commands only |
| `check-polearm-shield-parity.sh` | PostToolUse Edit\|Write | `additionalContext` | After an edit that could pair a shield with a polearm the AI never draws, runs `tools/audit_polearm_shield_parity.py` in the edited file's work tree and reports a FAIL once per finding set. | Advisory; SKIP or an overrun is reported, never silent | 7k | Claude's commands only |
| `check-feedback-memory.sh` | PostToolUse Edit\|Write | `additionalContext` | When a Write creates `memory/feedback_*.md`, asks where the rule belongs in the repo (ADR-011). It informs and never blocks. | Silent on anything else; starts no interpreter. | `tools/tests/test_feedback_memory_hook.py` | Claude's commands only |
| `mark-verification-run.sh` | PostToolUse Bash, PowerShell; PostToolUseFailure | none | Touches `.claude/logs/.verification-ran` when `dotnet build`, `dotnet test` or `build.ps1` runs (quotes read, D41), failed runs included; an interrupted call does not mark. | Fail-open; a reader overrun marks nothing | 4c, 7a, 7c, 7d, 7e | Claude's commands only |
| `check-verification-evidence.sh` | Stop | Stop `decision: block` | Reminds once per streak when a `.cs` file is newer than the last build or test marker, in the payload cwd's work tree. It stays because, with no CI, it is the only automatic nudge before a done claim. | Fail-open; silent when `stop_hook_active` | 7a | Claude's commands only |
| `log-agent.sh` | SubagentStart | none | Appends each agent invocation to `.claude/logs/agent-audit.log`, rotated at 256 KB. | Fail-open | 2 | Claude's commands only |
| `check-freeze.sh` (skill-inline) | PreToolUse Edit, Write, NotebookEdit | `deny` | Blocks edits outside the frozen directory. Declared in the `/freeze` and `/investigate` `SKILL.md` frontmatter; registered from the first invocation to session end, and inert while there is no freeze state file. | Fail-open | 5c3 | Claude's commands only |

## Git hooks (every committer)

The hooks above run only for the commands Claude runs. The repository's own git hooks live in the
tracked folder `.githooks/`, which `core.hooksPath` names (set once per clone with
`git config core.hooksPath .githooks`). With an absolute value, an older worktree runs the main
checkout's hook, which prints that the message was NOT checked and commits; with the relative value,
a worktree whose branch lacks the folder runs no hook. Session start warns when `core.hooksPath` does
not name a folder holding `commit-msg`.

| Hook | What it does |
|---|---|
| `commit-msg` | Runs `tools/check_commit_msg.py` on every commit message, terminal and IDE commits included: the `<type>[(scope)]: vX.Y.Z - <description>` subject with the `<Version>` of the tree being committed, at most 72 characters, and no AI attribution line. A `fixup!`, `squash!` or `amend!` subject passes. Refuses with exit 1 and names each problem; fails open, saying so, when Python or the checker is missing. Tests: `tools/tests/test_check_commit_msg.py`. |
| `pre-commit` | Refuses a commit when a file under `.claude/` is gitignored, when `tools/lint_docs.py --drift-only --staged` fails on a surface it reads, or when a commit holding ModuleData fails `tools/validate_moduledata.py` or `tools/generate_career_kits.py --verify`. Those two read the working tree, not the index, so the hook names any ModuleData file with unstaged edits. It cannot see an amend, so it judges only what an amend newly stages. It fails open and says so when Python or a tool is missing, crashes, overruns or cannot judge. POSIX sh, because the git inside an IDE ships no bash; with no GNU timeout the gates run unbounded. `tools/tests/test_githooks_pre_commit.py` pins it, through the IDE gits when they are installed. It runs beside the Claude commit gates until about 2026-11-17, when the two are compared. |
| `pre-merge-commit` | Runs `pre-commit`, because a clean merge skips it, with `--clean-merge` for the linter, because git writes `MERGE_HEAD` only after this hook. |

## Prefilter

A hook for the shell tools reads `INPUT=$(cat)` first and exits with its allow output when the raw payload lacks the word it gates, and only then sources `_pybin.sh`, because the probe and the parse are two Python starts per call (maintainer decision D39). Filter on the gated word, not on `git`: `*commit*` for the four commit gates, `*push*` for `validate-push.sh`, `*no-ver*`, `*commit*` or a `hooksPath` key in any case for `block-no-verify.sh`, `git` in any case (`*[Gg][Ii][Tt]*`) for the two confirm gates, `*dotnet*` or `*build.ps1*` for a build or test hook. Never skip a payload that holds a `\u` escape: JSON may write any letter that way, so it takes the full parse, and the raw test stays safe whatever writes the payload (D40). `tools/test_hooks.sh` 4c fails a Bash hook that sources `_pybin.sh` on a payload without its word and feeds each prefiltered hook its word with one letter escaped; 4d checks that the blocking gates answer the escaped form as they answer the plain one.

## Responding to a hook

Every gate and banner states what to do in its own message (ADR-011), so follow the message; nothing
here restates it. What reaches Claude: SessionStart stdout, a PreToolUse `deny` reason, exit-2
stderr, and a Stop hook's JSON `{"decision":"block","reason":...}`. Claude answers that reason in
one more response; the next Stop payload carries `stop_hook_active: true`, and every TAOM Stop
hook stays silent then (though it still clears its marker). In a headless or SDK run, that extra
response replaces the run's final message (plan 011; unverified live). An `ask` reason goes to the user. Plain stdout from every other event TAOM registers, and stderr from a hook that exits 0, go to the debug log only. A PreToolUse gate prints its decision
under `hookSpecificOutput`; Claude Code ignores a top-level `permissionDecision`, which left nine
gates inert until #647 (`harness-facts.md` "PreToolUse output contract", `tools/test_hooks.sh` 5c).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/git-and-commits.md](../ai-includes/git-and-commits.md)
- [docs/features/upstream-review.md](../features/upstream-review.md)
- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->
