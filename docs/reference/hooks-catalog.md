# Hooks Catalog

> Every `.claude/hooks/` script -> event -> purpose. **Recounted 2026-09-26 (after plan 027 and the graphify gate): 28 files on disk (27 scripts and the `_shellwords.py` reader), 27 registrations in `settings.json` across 9 events, plus 5 skill-frontmatter registrations (3 in `/freeze`, 2 in `/investigate`, all pointing at `check-freeze.sh`) for 32 total.** Three files, `_pybin.sh`, `_stop_reminder.sh` and `_shellwords.py`, are helpers with no registration of their own. The graphify gate added one script and two registrations (one in the Bash group, one in the PowerShell group, for 29); plan 027 then folded the PreToolUse `PowerShell` group, which by then held `validate-push.sh` and `check-graphify-usage.sh`, into one `Bash|PowerShell` group, which removed those two PowerShell-only registrations and brought the total back to 27. Plan 011 deleted `suggest-compact.sh` and `notify-test-results.sh` (maintainer decision D42): both printed to stderr on exit 0, which never reaches Claude. Plan 020 retired the two CHANGELOG hooks (the staged-entry gate and the Stop reminder) when CHANGELOG.md became generated at release (maintainer decision 18). The previous count said "27 scripts / 27 registrations, plus the `/freeze` skill-inline hook", which undercounted the frontmatter side as one hook rather than five and is exactly why those five went years without a `timeout`: they were not in anybody's tally. `bash tools/test_hooks.sh` now counts both surfaces and fails on a registration with no timeout, so this line has a gate behind it. Extracted from CLAUDE.md 2026-07-18. Authoring rules: `.claude/rules/hook-authoring.md`. Lifecycle facts: `.claude/rules/harness-facts.md`.


> **`jq` is not on PATH in this Git Bash install** (verified 2026-08-20). A git gate reads its command through `taom_hook_command` (`_pybin.sh`), which runs Python first and jq only when there is no Python; any other hook that parses stdin JSON must not depend on jq alone, or it is silently inert: a fail-open hook that never runs looks exactly like a hook with nothing to report. The grep+sed fallback truncates a value at the first escaped quote and leaves Windows backslashes doubled, so the fallback must be Python.
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
>
> In a hook for the shell tools, read `INPUT=$(cat)` first and exit with the hook's allow output when
> the raw payload lacks the word the hook gates, and only then source `_pybin.sh`: the probe and
> the parse are two Python starts on every Bash call, and a hook took 256 to 451 ms on an `ls`
> before the prefilter and 60 to 150 ms after. Filter on the gated word, not on `git`
> (maintainer decision D39, 2026-09-24): `*commit*` for the five commit gates, `*push*` for
> `validate-push.sh`, `*no-verify*` for `block-no-verify.sh`, `git` in any case (`*[Gg][Ii][Tt]*`) only for the two confirm
> gates, which judge several git subcommands, and `*dotnet*` or `*build.ps1*` for a build or test
> hook. A commit gate then starts no Python on `git status`, `git diff` or `git log` either,
> unless the call's description holds `commit`: the raw test reads the whole payload.
> `tools/test_hooks.sh` 4c fails a Bash hook that sources `_pybin.sh` on a payload without its
> word, and a narrowed gate that does so on any of those three git calls.
>
> JSON writes a letter either literally or as a `\uXXXX` escape (its short escapes stand only for
> `"`, `\`, `/` and five control characters), so every prefiltered hook also sends a payload
> holding any `\u` escape down the full parse (maintainer decision D40): an escaped letter can
> cost a parse but can never hide the gated word, whatever writes the payload. How Claude Code
> writes it now decides cost only; it writes letters literally (raw UTF-8 in #647). Any `\u`
> opens all eleven prefiltered shell hooks (the ten PreToolUse gates and
> `mark-verification-run.sh`): most often literal `\u` text in a command (a search for `\u2014`
> arrives as `\\u2014`), and in the PostToolUse hook a tool response with colour codes
> (`\u001b`). 4c feeds each of them its word with one letter escaped (twelve rows: the mark hook
> runs on PostToolUse and PostToolUseFailure), and 4d checks that six of the ten blocking gates
> (`check-commit-subject-version.sh`, `validate-push.sh`, `block-no-verify.sh`,
> `block-dangerous-git.sh`, `block-broad-git-add.sh`, `check-graphify-usage.sh`) answer the
> escaped form as they answer the plain one. After a Claude Code upgrade,
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
> and `.` call operators, an assignment `$r = git ...`, `${name}`, the typographic quotes, and a
> string or variable that opens a statement, which is a value PowerShell prints and reads as
> `echo <value>`), and in both shells a git named by a path, in capitals, in quotes or after
> `VAR=value` comes back as `git`, so each gate keeps its Bash logic. That rename also reaches
> heredoc body text, so a body line such as `Git reset --hard is gated` can draw an extra confirm:
> a known over-block, on the safe side. Text it cannot follow (an unclosed quote) comes back
> unchanged, and if the reader fails the gate reads the raw command as Bash text with a note on
> stderr, which reaches Claude only from a gate that exits 2. `tools/test_hooks.sh` 7e holds
> PowerShell rows for every gate and fails a gate that is not registered once for each tool or does
> not read through `taom_hook_command posix`; the Bash rows for the commit gate are in section 6 and
> validate-push's in 7c. `tools/tests/test_shellwords.py` pins the reader to the argument lists
> PowerShell 7's own parser gave for each input. Not read
> by the commit and confirm gates: a `$(...)` inside a double-quoted PowerShell string; a gated
> word spelled with an escape inside it (`--no-verif''y`), which the raw prefilter cannot see; and
> git run through another command, such as `& ("git") commit ...`,
> `Start-Process git -ArgumentList ...`, `pwsh -c "git commit ..."` or
> `Invoke-Expression 'git reset --hard'`. `validate-push.sh` also judges the raw command, so it
> still refuses such a force push. Deliberate obfuscation is out of scope.

| Hook | Event | Purpose |
|------|-------|---------|
| `block-no-verify.sh` | PreToolUse (Bash, PowerShell) | Blocks any git command carrying `--no-verify`. Was `check-build-before-commit.sh` and also ran `dotnet build` before every commit; it never did, because it read the command with a bare `jq` and jq is not on PATH here, so both halves were inert. The build half was dropped rather than re-armed on 2026-08-20: hooks run with cwd = the MAIN tree regardless of where the command runs, so a worktree commit would be gated on another tree's build, and the build lacked `-p:DisableModuleCopy=true` so a running game could block a commit. Verification stays with `check-verification-evidence.sh` (Stop) and `/verify`. |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-version-tagged.sh` | Stop | Reminds Claude (JSON `decision: block` via `_stop_reminder.sh`, silent when `stop_hook_active` is true) to ask about tagging and pushing the release when `<Version>` in `Main/_Module/SubModule.xml` has no matching git tag, the version every crash bundle reports as `TaomVersion`. One condition catches both a bump committed without a tag and a version that never entered git (`v2.0.12`). Marker stores the version, so a new untagged bump re-arms. Stop, not PreToolUse: the tag can only exist after the commit. See `docs/reference/release-process.md` |
| `session-start.sh` | SessionStart | Prints branch and recent commits on startup. **Also warns loudly on game-version drift** (installed `Version.xml` vs `.claude/pinned-game-version.txt`) → run `/engine-bump`. Since 2026-08-10 the drift check falls back to the known install path when `BANNERLORD_GAME_DIR` is set but unresolvable, and prints an explicit *unchecked, not absent* line when neither resolves; it produced total silence on the v1.4.8 bump before that. **Also prints `ARMORY ART DRIFT` (2026-09-15, #599)** when a `*_geo.tpac` under the live `LOTRLOME_Armory/Assets` is newer than the committed `docs/reference/armory-catalogue/catalogue.tsv`: one `find` bounded at 4 s, `_geo` only (every catalogue row lives in one; `_tex`/`_mtl`/`_anm` are re-saved constantly), UNCHECKED line when the install does not resolve or the scan overruns. Response: `/armory-audit` before any battle smoke. |
| `pre-compact.sh` | PreCompact | Dumps modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit logs agent invocations to `.claude/logs/agent-audit.log` |
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to Directory.Build.props, settings*.json, ADRs without explicit request. CLAUDE.md removed from the protected list 2026-07-02 (user decision — solo dev; the agent maintains CLAUDE.md as living documentation) |
| `mcp-health-check.sh` | PreToolUse (mcp__*) | Blocks MCP calls to servers marked unhealthy in last 60s |
| `mcp-health-mark.sh` | PostToolUseFailure (mcp__*) | Marks MCP server unhealthy after failed tool call, 60s backoff |
| `check-deep-review.sh` | Stop | Reminds Claude to run `/deep-review` when C#, C++, XML, XSLT or JSON files are dirty and no `deep-reviewer` agent is logged in the last 8 h. JSON `decision: block` via `_stop_reminder.sh`, silent when `stop_hook_active` is true; one-shot per streak (`.deep-review-reminded`, since plan 011), re-armed by a logged review or a clean tree |
| `post-compact.sh` | PostCompact | Reminds Claude to re-read MEMORY.md + in-flight files after compaction. Resolves the LIVE auto-memory path under `~/.claude/projects/<slug>/` (2026-08-05 — the tracked `.claude/memory/` copy was stale and is gone) |
| `detect-docs-gaps.sh` | SessionStart | Flags `Main/Features/<X>` directories with no matching `docs/features/*.md` |
| `validate-push.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks (exit 2) a force push (`--force`, `-f`, `--force-with-lease`, a `+` refspec) to master, main, `bannerlord-1.5.x` or `bannerlord-1.4.5`, named rather than `bannerlord-*` (maintainer decision D30). It judges every command of a multi-line command, joining a line continued with `\` or a PowerShell backtick (D38) and splitting each line at `;`, `&` and `\|`, so a tail such as `2>&1 \| tail -5` cannot hide the push; it judges every refspec, not only the last, and refuses `--all` with a force flag and `--mirror` (plan 011 review). It judges several splits and blocks when any does, all returned by one `_shellwords.py push` run (plan 027 and its review): the command read as POSIX-shell text and the raw command, each cut at those characters everywhere; the POSIX text split outside quotes only, so a separator inside a quoted value (`git -C "E:/R&D" push`, `-o "a;b"`) does not cut `git` from `push`, and each such segment again with its argument boundaries kept; and the raw command split outside quotes with the tool's own escape, the split it judged before plan 027, so reading PowerShell never loses a push the raw text showed (a comma argument list such as `Start-Process git -ArgumentList 'push','--force',...`, `& ("git") push`, a refspec in parentheses). The quote-blind split drops a `#` comment from a segment only where no quote (ASCII, typographic or backtick) comes anywhere before it in the command, so a quoted ` #` never hides the push, even where that split cuts inside the quoted value (`X="a;b #c" git push ...` after a heredoc line holding one quote; convergence of plan 027). Neither split is safe alone: an apostrophe in a heredoc line (`Don't push`) opens a quote that never closes and glues the later lines into one segment, so the push is anchored on the first `push` token with a `git` token before it, which also covers the jq-only fallback (plan 011 final convergence). The same glue can refuse a later, unrelated command after such a line; that over-block stays, since it errs on the safe side and ending a quote at a newline would let a quoted value spanning lines cut `git` from `push`. The current branch is asked once per run and each distinct segment is judged once, so 100 push lines stay well inside the 5 s registration; a refspec glued to a redirection (`bannerlord-1.5.x>/dev/null`) still counts (plan 011 convergence). It over-blocks quoted text by design: quoted or heredoc text that reads as a trunk force push is refused too, since `bash -c` and `bash <<EOF` run such text; write that commit message with `git commit -F <file>`. Known gaps: `HEAD` or no refspec resolves against the main tree's branch even for a push run in a worktree, and deleting a trunk (`--delete`, `:branch`) is not a force push, so it passes. A plain push to one of them gets a stderr warning, which Claude does not see. Registered for both shell tools since plan 011. Since plan 027 it judges a pattern refspec (`refs/heads/*`, `refs/heads/bannerlord-*`) as every protected name it matches and a `heads/<name>` destination as `<name>`, takes the next word as the value of `-o`, `--push-option`, `--repo`, `--receive-pack`, `--recurse-submodules` or `--exec` (or a prefix git accepts for one, or a short cluster ending in `o` such as `-fo`) and judges the push both with and without that skip (a lost empty value made the skip take the remote), reads a force flag anywhere in a short cluster (`-vfu`), and ignores a trunk named only in a `#` comment with no quote anywhere before it. Without Python, or when the reader fails, it judges the raw command as it did before plan 027, comments included. GitHub protects no branch (no ruleset, maintainer decision D29) |
| `block-dangerous-git.sh` | PreToolUse (Bash, PowerShell) | Prompts (`ask`) before work-destroying git ops (`reset --hard`, `clean -f`, `branch -D`, `checkout`/`restore` discard, `stash drop/clear`). Segment-anchored; excludes push (validate-push owns it); fail-open. |
| `check-commit-subject-version.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks `git commit` when the subject is not `<type>[(scope)]: vX.Y.Z - <description>` with `vX.Y.Z` equal to the `<Version>` of `Main/_Module/SubModule.xml` as committed (staged copy first, so a release commit names its new version). User rule 2026-09-13: between releases every pushed commit reports the same `TaomVersion`, so the subject carries it. Amend without a message, fixup, squash and reuse-message forms pass; the parser reads heredoc bodies, `-m` strings and `-F` files. Since 2026-09-23 it also refuses an AI attribution line anywhere in the message (a `Co-Authored-By` naming an AI, or "Generated with Claude Code"); a human co-author passes. |
| `check-claude-files-tracked.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the gitignore-blast bug (`bin/check-freeze.sh` shipped non-functional in efbde5b). |
| `session-stop.sh` | SessionEnd | Appends commits + modified files to `.claude/logs/session-log.md` (1 MB size-cap rotation). Moved off Stop 2026-07-18 — Stop fires every turn, SessionEnd once |
| `mark-verification-run.sh` | PostToolUse and PostToolUseFailure (Bash, PowerShell) | Touches `.claude/logs/.verification-ran` when `dotnet build`/`dotnet test`/`build.ps1` runs. Feeds the verification Stop hook. Since plan 011 (maintainer decision D41) it marks the repo's own `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, which its env-prefix strip used to drop, and it splits a command into segments outside quotes only, so a quoted mention never marks. The split is `_shellwords.py segments` (plan 027): the command is read as POSIX-shell text first, so a PowerShell command splits as its Bash twin, linear in the command's length, and the first word is read without quotes, so PowerShell's `.\build.ps1` still marks, while a quoted path standing alone (`'.\build.ps1'`, a string PowerShell prints) does not. Known limitation: a script block that is only assigned (`$v = { dotnet test }`) marks, because the reader reads a block's body as commands so the git gates see git inside one. A PostToolUseFailure with `is_interrupt` true (an aborted call, so no result) does not mark; whether a timed-out command sets it is UNVERIFIED (plan 027). A command that exits non-zero raises PostToolUseFailure, not PostToolUse, so the hook is registered on both and a failed build or test marks too (plan 011; `tools/test_hooks.sh` 7c checks the registration, 7d the failure payload). |
| `check-verification-evidence.sh` | Stop | Reminds Claude to build/test when a `.cs` file changed but no verification ran since the last edit, through a JSON `decision: block` (`_stop_reminder.sh`); silent when `stop_hook_active` is true; one-shot per unbuilt streak. Enforces `.claude/rules/evidence-over-claims.md`. |
| `check-moduledata-validation.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks `git commit` when staged `Main/_Module/ModuleData/**/*.xml` or `*.xslt` (since #626, for `lords.xslt`) fails the ERROR-severity checks of `tools/validate_moduledata.py` (broken Item/NPCCharacter ref, unknown culture, duplicate id). Fail-open: missing python / game install / validator crash never blocks. Warnings don't block; run the tool to see them. |
| `check-native-dll-crt.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks commit when the staged `TAOM.NativeSkinFixes.dll` links a dynamic/debug CRT (absent on player machines → `LoadLibrary` error 126); must link static CRT (`/MT`). Fail-open |
| `block-broad-git-add.sh` | PreToolUse (Bash, PowerShell) | Confirms (`ask`) before `git add -A/-u/.` and `git commit -a/-am`, listing what the sweep would take. A shared file routinely holds two sessions' edits, so a broad add commits work you do not own. Added 2026-08-09; catalogued 2026-08-20. |
| `check-polearm-shield-parity.sh` | PostToolUse (Edit\|Write) | Runs `tools/audit_polearm_shield_parity.py` after an edit that could pair a shield with a weapon the AI will not draw: `weapon_descriptions.xslt`, anything under `LOTRLOME_items`, or any `.xml` containing an `<EquipmentRoster>` (content test, not a path pattern, because rosters are not confined to `troops/`). FAIL prints the block once per distinct finding set (`.claude/logs/.polearm-gate-reported`), PASS is silent and clears the mute, and SKIP or a missing tool says so rather than passing in silence. Advisory, always exits 0. Exists because the gate needs the game install and so cannot run in CI: `docs/reviews/lessons/build-tooling-workflow.md`, "A gate sitting in an unmerged PR is not a gate". |
| `check-doc-config-drift.sh` | PreToolUse (Bash, PowerShell) | Hard-blocks a commit that stages a feature doc, a shipped config, a version marker, an entry doc or a rule when `tools/lint_docs.py --drift-only` finds config-example drift, a version mismatch vs the pin, or an ADR-011 context-budget breach (size-warn findings report but never gate). About 0.15 s; `.github/workflows/doc-budget.yml` runs the full `--fail-on-drift` on every branch. `tools/test_hooks.sh` 5d pins its file list to the entry docs. Fail-open |
| `check-graphify-usage.sh` | PreToolUse (Bash, PowerShell) | Keeps every graphify write going through `tools/graphify_taom.py` (#677). An allow-list: the query verbs, `diagnose`, `benchmark`, `help` and `version` pass; any other `graphify` verb (`extract`, `update`, `cluster-only`, `label`, `watch`, the install verbs, ...) and `graphify-mcp` are denied, and a command that only mentions graphify as data passes. Its judge is `tools/graphify_taom.py gate`, which splits both shells itself, so it does not read through `taom_hook_command`. A judge that runs past 5 s answers `ask`; with no safe Python, or a failed judge, it fails open and says so (no jq path). `tools/test_hooks.sh` 7f; the case table is `tools/tests/test_graphify_taom.py` ([code graph](../features/graphify-code-graph.md)) |

## Responding to a hook

Every gate and banner states what to do in its own message (ADR-011), so follow the message; nothing
here restates it. What reaches Claude: SessionStart stdout, a PreToolUse `deny` reason, exit-2
stderr, and a Stop hook's JSON `{"decision":"block","reason":...}`. Claude answers that reason in
one more response; the next Stop payload carries `stop_hook_active: true`, and every TAOM Stop
hook stays silent then (though it still clears its marker). In a headless or SDK run, that extra
response replaces the run's final message (plan 011; unverified live). An `ask` reason goes to the user. Plain stdout from Stop, PreCompact and
PostCompact hooks, and stderr from a hook that exits 0, go to the debug log only. A PreToolUse gate prints its decision
under `hookSpecificOutput`; Claude Code ignores a top-level `permissionDecision`, which left nine
gates inert until #647 (`harness-facts.md` "PreToolUse output contract", `tools/test_hooks.sh` 5c).

Skill-inline (not in `.claude/hooks/`): `check-freeze.sh` — PreToolUse (Edit|Write) declared in the `/freeze` + `/investigate` SKILL.md frontmatter; blocks edits outside the frozen directory while one of those skills is active. Fires only during skill invocation (`harness-facts.md` "Hook lifecycle").

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/validation-and-testing.md](../modding/validation-and-testing.md)

<!-- backlinks-end -->
