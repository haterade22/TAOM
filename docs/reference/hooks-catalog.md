# Hooks Catalog

> Every `.claude/hooks/` script -> event -> purpose (25 scripts / 25 registrations across 9 events, plus the `/freeze` skill-inline hook — audited 2026-08-05, `check-version-tagged.sh` added 2026-08-08). Extracted from CLAUDE.md 2026-07-18. Authoring rules: `.claude/rules/hook-authoring.md`. Lifecycle facts: `.claude/rules/harness-facts.md`.


| Hook | Event | Purpose |
|------|-------|---------|
| `check-build-before-commit.sh` | PreToolUse (Bash) | Blocks `git commit` if build fails |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md when source is dirty. One-shot per streak (`.changelog-reminded` marker, added 2026-08-05); re-arms when CHANGELOG becomes dirty/staged |
| `check-version-tagged.sh` | Stop | Reminds to tag + push the release when `<Version>` in `Main/_Module/SubModule.xml` has no matching git tag — the version every crash bundle reports as `TaomVersion`. One condition catches both a bump committed without a tag and a version that never entered git (`v2.0.12`). Marker stores the version, so a new untagged bump re-arms. Stop, not PreToolUse: the tag can only exist after the commit. See `docs/reference/release-process.md` |
| `session-start.sh` | SessionStart | Prints branch, recent commits, CHANGELOG summary on startup. **Also warns loudly on game-version drift** (installed `Version.xml` vs `.claude/pinned-game-version.txt`) → run `/engine-bump`. |
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
| `check-changelog-changed.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when `.claude/`, `CLAUDE.md`, or `AGENTS.md` is staged but `CHANGELOG.md` is not. Catches the recurring "forgot to update CHANGELOG" process violation. |
| `check-claude-files-tracked.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the gitignore-blast bug (`bin/check-freeze.sh` shipped non-functional in efbde5b). |
| `session-stop.sh` | SessionEnd | Appends commits + modified files to `.claude/logs/session-log.md` (1 MB size-cap rotation). Moved off Stop 2026-07-18 — Stop fires every turn, SessionEnd once |
| `notify-test-results.sh` | PostToolUse (Bash) | Summarizes `dotnet test` results prominently to stderr (`TEST RESULTS: PASSED/FAILED` with counts) |
| `mark-verification-run.sh` | PostToolUse (Bash) | Touches `.claude/logs/.verification-ran` when `dotnet build`/`dotnet test`/`build.ps1` runs. Feeds the verification Stop hook. |
| `check-verification-evidence.sh` | Stop | Reminds to build/test when a `.cs` file changed but no verification ran since the last edit. Enforces `.claude/rules/evidence-over-claims.md`. |
| `check-moduledata-validation.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when staged `Main/_Module/ModuleData/**/*.xml` fails the ERROR-severity checks of `tools/validate_moduledata.py` (broken Item/NPCCharacter ref, unknown culture, duplicate id). Fail-open: missing python / game install / validator crash never blocks. Warnings don't block — run the tool to see them. |
| `check-native-dll-crt.sh` | PreToolUse (Bash) | Hard-blocks commit when the staged `TAOM.NativeSkinFixes.dll` links a dynamic/debug CRT (absent on player machines → `LoadLibrary` error 126); must link static CRT (`/MT`). Fail-open |
| `check-doc-config-drift.sh` | PreToolUse (Bash) | Hard-blocks commit on config-example drift, version mismatch vs the pin, or a CLAUDE.md hard budget violation (size-warn findings report but don't gate, 2026-08-05), via `tools/lint_docs.py --fail-on-drift`. Fail-open |

Skill-inline (not in `.claude/hooks/`): `check-freeze.sh` — PreToolUse (Edit|Write) declared in the `/freeze` + `/investigate` SKILL.md frontmatter; blocks edits outside the frozen directory while one of those skills is active. Fires only during skill invocation (`harness-facts.md` "Hook lifecycle").

