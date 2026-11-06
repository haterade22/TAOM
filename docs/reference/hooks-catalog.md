# Hooks Catalog

> Every `.claude/hooks/` script -> event -> purpose. **Recounted when plan 020 retired the two CHANGELOG hooks: 27 scripts on disk, 26 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 31 total.** One script, `_pybin.sh`, is a sourced helper with no registration of its own. The previous count said "27 scripts / 27 registrations, plus the `/freeze` skill-inline hook", which undercounted the frontmatter side as one hook rather than five and is exactly why those five went years without a `timeout`: they were not in anybody's tally. `bash tools/test_hooks.sh` now counts both surfaces and fails on a registration with no timeout, so this line has a gate behind it. Extracted from CLAUDE.md 2026-07-18. Authoring rules: `.claude/rules/hook-authoring.md`. Lifecycle facts: `.claude/rules/harness-facts.md`.


> **`jq` is not on PATH in this Git Bash install** (verified 2026-08-20). Any hook that parses stdin JSON must guard with `command -v jq` and fall back, or it is silently inert: a fail-open hook that never runs looks exactly like a hook with nothing to report. The grep+sed fallback truncates a value at the first escaped quote and leaves Windows backslashes doubled, so the fallback must be Python.
>
> **NEVER write `python3`.** On this machine that name resolves only to
> `C:\Users\mikew\AppData\Local\Microsoft\WindowsApps\python3`, a Microsoft Store App Execution
> Alias which, run from Git Bash, prints nothing, never exits, and ignores SIGTERM. `command -v`
> succeeding proves only that a file exists at that name, so guarding on it does not help.
> **Source `_pybin.sh` and use `"$PYBIN"`**, then honour its contract
> (`[ -n "$PYBIN" ] || { echo '{}'; exit 0; }`). `block-dangerous-git.sh` is the model.
>
> This paragraph used to say "use the python3 fallback". That advice, written 2026-08-20, is what
> wedged every JSON-parsing hook: with no `timeout` on the registrations, a Bash call paid one
> 600s PreToolUse batch plus one 600s PostToolUse batch, which is the 20.0-minute stall seen in the
> 2026-08-31 transcripts. Outside a hook, plain `python` is safe and is the repo convention.

| Hook | Event | Purpose |
|------|-------|---------|
| `block-no-verify.sh` | PreToolUse (Bash) | Blocks any git command carrying `--no-verify`. Was `check-build-before-commit.sh` and also ran `dotnet build` before every commit; it never did, because it read the command with a bare `jq` and jq is not on PATH here, so both halves were inert. The build half was dropped rather than re-armed on 2026-08-20: hooks run with cwd = the MAIN tree regardless of where the command runs, so a worktree commit would be gated on another tree's build, and the build lacked `-p:DisableModuleCopy=true` so a running game could block a commit. Verification stays with `check-verification-evidence.sh` (Stop) and `/verify`. |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-version-tagged.sh` | Stop | Reminds to tag + push the release when `<Version>` in `Main/_Module/SubModule.xml` has no matching git tag — the version every crash bundle reports as `TaomVersion`. One condition catches both a bump committed without a tag and a version that never entered git (`v2.0.12`). Marker stores the version, so a new untagged bump re-arms. Stop, not PreToolUse: the tag can only exist after the commit. See `docs/reference/release-process.md` |
| `session-start.sh` | SessionStart | Prints branch and recent commits on startup. **Also warns loudly on game-version drift** (installed `Version.xml` vs `.claude/pinned-game-version.txt`) → run `/engine-bump`. Since 2026-08-10 the drift check falls back to the known install path when `BANNERLORD_GAME_DIR` is set but unresolvable, and prints an explicit *unchecked, not absent* line when neither resolves; it produced total silence on the v1.4.8 bump before that. |
| `pre-compact.sh` | PreCompact | Dumps modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit logs agent invocations to `.claude/logs/agent-audit.log` |
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to Directory.Build.props, settings*.json, ADRs without explicit request. CLAUDE.md removed from the protected list 2026-07-02 (user decision — solo dev; the agent maintains CLAUDE.md as living documentation) |
| `suggest-compact.sh` | PreToolUse (*) | Suggests `/compact` two ways: threshold (after 50 tool calls, then every 25) AND boundary-aware (after a task-transition Bash command — successful commit/verify — so compaction lands between tasks, not mid-task) |
| `mcp-health-check.sh` | PreToolUse (mcp__*) | Blocks MCP calls to servers marked unhealthy in last 60s |
| `mcp-health-mark.sh` | PostToolUseFailure (mcp__*) | Marks MCP server unhealthy after failed tool call, 60s backoff |
| `check-deep-review.sh` | Stop | Reminds to run `/deep-review` if real work was done |
| `post-compact.sh` | PostCompact | Reminds Claude to re-read MEMORY.md + in-flight files after compaction. Resolves the LIVE auto-memory path under `~/.claude/projects/<slug>/` (2026-08-05 — the tracked `.claude/memory/` copy was stale and is gone) |
| `detect-docs-gaps.sh` | SessionStart | Flags `Main/Features/<X>` directories with no matching `docs/features/*.md` |
| `validate-push.sh` | PreToolUse (Bash) | Warns on push to master/main; hard-blocks force push to protected branches |
| `block-dangerous-git.sh` | PreToolUse (Bash) | Prompts (`ask`) before work-destroying git ops (`reset --hard`, `clean -f`, `branch -D`, `checkout`/`restore` discard, `stash drop/clear`). Segment-anchored; excludes push (validate-push owns it); fail-open. |
| `check-commit-subject-version.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when the subject is not `<type>[(scope)]: vX.Y.Z - <description>` with `vX.Y.Z` equal to the `<Version>` of `Main/_Module/SubModule.xml` as committed (staged copy first, so a release commit names its new version). User rule 2026-09-13: between releases every pushed commit reports the same `TaomVersion`, so the subject carries it. Amend without a message, fixup, squash and reuse-message forms pass; the parser reads heredoc bodies, `-m` strings and `-F` files. |
| `check-claude-files-tracked.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the gitignore-blast bug (`bin/check-freeze.sh` shipped non-functional in efbde5b). |
| `session-stop.sh` | SessionEnd | Appends commits + modified files to `.claude/logs/session-log.md` (1 MB size-cap rotation). Moved off Stop 2026-07-18 — Stop fires every turn, SessionEnd once |
| `notify-test-results.sh` | PostToolUse (Bash) | Summarizes `dotnet test` results to stderr (`TEST RESULTS: PASSED/FAILED` with counts). The hook exits 0, so the banner reaches the debug log only and Claude never sees it. It does not report skips: a skipped test checked nothing, so read the `Skipped:` count in `dotnet test`'s own output. The binding gate is strict through `TAOM.Tests/binding-gate.runsettings`, which fails an `Assert.Inconclusive` (the gate's skip when it cannot load the game) and a filter that matches no test. An `[Ignore]`d test still reports Skipped and exits 0, so a gate run is green only at `Skipped: 0`. |
| `mark-verification-run.sh` | PostToolUse (Bash) | Touches `.claude/logs/.verification-ran` when `dotnet build`/`dotnet test`/`build.ps1` runs. Feeds the verification Stop hook. |
| `check-verification-evidence.sh` | Stop | Reminds to build/test when a `.cs` file changed but no verification ran since the last edit. Enforces `.claude/rules/evidence-over-claims.md`. |
| `check-moduledata-validation.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when staged `Main/_Module/ModuleData/**/*.xml` fails the ERROR-severity checks of `tools/validate_moduledata.py` (broken Item/NPCCharacter ref, unknown culture, duplicate id). Fail-open: missing python / game install / validator crash never blocks. Warnings don't block — run the tool to see them. |
| `check-native-dll-crt.sh` | PreToolUse (Bash) | Hard-blocks commit when the staged `TAOM.NativeSkinFixes.dll` links a dynamic/debug CRT (absent on player machines → `LoadLibrary` error 126); must link static CRT (`/MT`). Fail-open |
| `block-broad-git-add.sh` | PreToolUse (Bash) | Confirms (`ask`) before `git add -A/-u/.` and `git commit -a/-am`, listing what the sweep would take. A shared file routinely holds two sessions' edits, so a broad add commits work you do not own. Added 2026-08-09; catalogued 2026-08-20. |
| `check-polearm-shield-parity.sh` | PostToolUse (Edit\|Write) | Runs `tools/audit_polearm_shield_parity.py` after an edit that could pair a shield with a weapon the AI will not draw: `weapon_descriptions.xslt`, anything under `LOTRLOME_items`, or any `.xml` containing an `<EquipmentRoster>` (content test, not a path pattern, because rosters are not confined to `troops/`). FAIL prints the block once per distinct finding set (`.claude/logs/.polearm-gate-reported`), PASS is silent and clears the mute, and SKIP or a missing tool says so rather than passing in silence. Advisory, always exits 0. Exists because the gate needs the game install and so cannot run in CI: `docs/reviews/lessons/build-tooling-workflow.md`, "A gate sitting in an unmerged PR is not a gate". |
| `check-doc-config-drift.sh` | PreToolUse (Bash) | Hard-blocks commit on config-example drift, version mismatch vs the pin, or a CLAUDE.md hard budget violation (size-warn findings report but don't gate, 2026-08-05), via `tools/lint_docs.py --fail-on-drift`. Fail-open |

Skill-inline (not in `.claude/hooks/`): `check-freeze.sh` — PreToolUse (Edit|Write) declared in the `/freeze` + `/investigate` SKILL.md frontmatter; blocks edits outside the frozen directory while one of those skills is active. Fires only during skill invocation (`harness-facts.md` "Hook lifecycle").

Status line (not a hook): `.claude/statusline.sh` renders `ctx: N% | model | branch | Ns/Nu/Nt` (staged, unstaged and untracked counts, omitted when clean). It is registered under `statusLine` in `settings.json` by a relative path, which is safe because it gates nothing.

## Git hooks (every committer)

The hooks above run only for the commands Claude runs. The repository's own git hooks live in the
tracked folder `.githooks/`, which `core.hooksPath` names (set once per clone with
`git config core.hooksPath .githooks`; a worktree whose branch predates the folder runs no hook).

| Hook | What it does |
|---|---|
| `commit-msg` | Runs `tools/check_commit_msg.py` on every commit and merge message, terminal and IDE commits included: the `<type>[(scope)]: vX.Y.Z - <description>` subject with the `<Version>` of the tree being committed, at most 72 characters, and no AI attribution line. A `fixup!`, `squash!` or `amend!` subject passes. Refuses with exit 1 and names each problem; fails open, saying so, when Python or the checker is missing, when the only Python is a Microsoft Store alias, or when the checker crashes. A merge therefore needs a labelled message (`git merge --no-ff -m "chore: vX.Y.Z - merge <branch>" <branch>`). Tests: `tools/tests/test_check_commit_msg.py`. |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->
