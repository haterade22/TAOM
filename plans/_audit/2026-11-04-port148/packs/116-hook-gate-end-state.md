# Input pack: WP 116 hook-gate-end-state

Title (draft): Transplant the 1.5.x git-gate end state: prefilter, trunk guard, PowerShell coverage, config protection, dangerous-git arms
Wave: 1   Lane: H   Draft priority: P2   Groups: 6   Port commits: 28
Depends on (WP ids): 103

## Orchestrator notes for this WP (binding)
- One executor, serial, own worktree. End-state transplant (do not replay commits): validate-push.sh, _pushjudge.py, _shellwords.py, _pybin.sh helpers, gate scripts and PowerShell matchers, adapted (trunk bannerlord-1.4.5, v2.0.28, no CHANGELOG gates after WP102, Serena per D3).
- Take the push-judge half of d01bddf10 and 30abf550b's config-protection normalisation hunk.
- settings.json edits are the maintainer's (exact diff in the plan).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `plan-013-bash-hook-prefilter`  P2  track=harness  commits=7  sim: clean=1 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh
- `90141357d` 2026-09-24 PORT P2 pick=conflict :: perf(hooks): v2.0.30 - skip Python in Bash hooks on non-git calls
  - why: All 13 Bash hooks on 1.4.x still start Python twice before reading the command (none has a prefilter); this adds a raw-payload trigger-text check so calls without git, dotnet or build.ps1 exit early (256-451 ms per hook down to 60-150 ms measured on 1.5.x), with tools/test_hooks.sh section 4c pinning both directions.
  - port notes: Hook hunks should apply (origin/bannerlord-1.4.5:.claude/hooks/block-dangerous-git.sh:31-41 matches the hunk pre-image); only tools/test_hooks.sh conflicts, so add section 4c by hand. check-commit-subject-version.sh's v2.0.28 rule is unaffected. Proof under a live 1.4.x harness is owed (bash tools/test_hooks.sh). The hooks-catalog row applies; the 1.4.x check-changelog-changed hook will require a hand-written CHANGELOG entry for the .claude edits.
  - conflicts: tools/test_hooks.sh
- `787fd3663` 2026-09-24 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - review follow-ups for plan 013
  - why: Review fixes to the plan 013 Bash-hook prefilter (comment premise, a trace-based test_hooks.sh 4c check, new Bash payload rows); harness efficiency that applies to 1.4.x, where all 14 hook files are byte-identical to the fork point.
  - port notes: Apply after 90141357d (plan 013 base, batch 05). Hook hunks are one-line comment edits and apply cleanly. tools/test_hooks.sh conflicts: section 4c comes from 90141357d, 1.5.x also edited the suite in c79a58521 and fa8c06f82, and 1.4.x has ccef172ad's two-line comment change; hand-merge sections 4 and 4c. .claude/rules/hook-authoring.md differs on 1.4.x (ccef172ad), so hand-merge it. Drop the CHANGELOG hunk and write a 1.4.x entry. Review record, RCA, lessons and REVIEW-LOG are optional archival docs carrying 1.5.x hashes.
  - prereqs: 90141357d
  - conflicts: tools/test_hooks.sh
- `b34d615a0` 2026-09-24 PORT P3 pick=conflict :: fix(hooks): v2.0.30 - convergence fixes for plan 013
  - why: Wording-only convergence fixes for plan 013 docs (hooks-catalog before/after timing figures, review record, RCA, lesson count); no hook code.
  - port notes: Take the docs/reference/hooks-catalog.md hunk with the series (that file is identical at fork and on 1.4.x); the review record, RCA and lesson edits only if those files travel with 787fd3663. Drop the CHANGELOG hunk.
  - prereqs: 787fd3663
- `5dcef67ac` 2026-09-24 PORT P3 pick=clean :: docs(reviews): v2.0.30 - Codex prompt for plan 013
  - why: Adds the archived Codex adversarial prompt used for the plan 013 review, a review artifact for in-scope harness work.
  - port notes: Optional archival file; new file, picks clean. Carry only if the plan 013 review records are ported; it cites 1.5.x hashes.
- `7aa658e3e` 2026-09-24 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - apply maintainer decisions for plan 013
  - why: Narrows each gate's prefilter to the word it gates (commit, push, no-verify) so git status, diff and log start no Python in the commit gates, and makes any JSON backslash-u escape take the full parse.
  - port notes: Hook hunks apply once 90141357d and 787fd3663 are in; tools/test_hooks.sh needs the same hand merge. 1.4.x keeps check-changelog-changed.sh (1.5.x retires it later in c91bb95f8, plan 020), so keep its prefiltered form there. suggest-compact.sh stays at the 90141357d prefilter because 1.5.x plan 011 deletes it; if 1.4.x does not port plan 011, add the backslash-u arm to suggest-compact.sh too or port 011's deletion. D38, D41 and D42 are handled in plan 011.
  - prereqs: 90141357d, 787fd3663
  - conflicts: tools/test_hooks.sh
- `6514fdfe7` 2026-09-24 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - decision review follow-ups for plan 013
  - why: Decision-review fixes for plan 013: git -C commit trigger rows and an escaped row in test_hooks.sh 4c, plus corrected hook comments and hooks-catalog text, with no gate decision changed.
  - port notes: Comments and tests only, on top of 7aa658e3e; tools/test_hooks.sh hand merge; review records optional; drop the CHANGELOG hunk.
  - prereqs: 7aa658e3e
  - conflicts: tools/test_hooks.sh
  - issues: 661
- `ee1929fa4` 2026-09-24 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - convergence fixes for plan 013
  - why: Adds a git -C push row for validate-push.sh to test_hooks.sh 4c and corrects the 4d header and hooks-catalog wording; this is the final code state of plan 013.
  - port notes: Last plan 013 code commit. Do not copy the hook files from the 1.5.x tip: later plans (011, 027, the static-CRT hook retirement ad1992b5b) rewrote or deleted several of them. Drop the CHANGELOG hunk.
  - prereqs: 6514fdfe7
  - conflicts: tools/test_hooks.sh
skipped in this group: `2feb70ad6` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - cite #661 for plan 013

### group `plan-011-stop-reminders-trunk-guard`  P2  track=harness  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh
- `43e6780e4` 2026-09-25 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - Stop reminders reach Claude, guard both trunks
  - why: On 1.4.5 the four Stop reminders print to stderr and exit 0 (check-verification-evidence.sh), which never reaches Claude; suggest-compact.sh is registered on every tool call; and validate-push.sh guards only master, main and bannerlord-1.4.5, for Bash only; this routes the reminders through a JSON block, deletes two dead hooks and extends the push guard to both trunks and the PowerShell tool.
  - port notes: Prerequisites on 1.5.x: c79a58521 (harness-facts Visibility row and the test_hooks.sh base) and the plan 013 prefilter series starting at 90141357d.

Expect conflicts in test_hooks.sh, settings.json, hooks-catalog.md and CLAUDE.md. 1.4.x keeps a Hook Response Contracts table: update its rows rather than importing the 1.5.x CLAUDE.md lines.

The validate-push.sh hunks are later rewritten into a Python judge (a4ba6b0b3, then fff6d66e9, d01bddf10 and caa7ac356). Either replay the series in order or port the final validate-push.sh, _pushjudge.py and _shellwords.py.

9cfcadd07 later deletes check-deep-review.sh and check-version-tagged.sh.
  - prereqs: c79a58521, 90141357d
  - conflicts: tools/test_hooks.sh
- `c80c4108c` 2026-09-25 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - review follow-ups for plan 011
  - why: Review fixes: the Stop hooks' stop_hook_active guard moves just before the block so a streak cannot stay muted, mark-verification-run.sh splits commands in Python (the bash loop took 5.4 s on 100 KB against a 5 s timeout), and validate-push.sh judges every command and refspec on a line.
  - port notes: The validate-push.sh hunks are superseded by a4ba6b0b3's Python judge if that is ported. mark-verification-run.sh changes again in cfd3b48f6 and caa7ac356. The review report and RCA are optional records; the lessons in build-tooling-workflow.md and state-lifecycle-save.md are worth keeping.
  - prereqs: 43e6780e4
  - conflicts: tools/test_hooks.sh
- `65b405e42` 2026-09-25 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - convergence fixes for plan 011
  - why: Convergence fixes for the push guard: a quote-aware split so a quoted -C path holding '&' no longer cuts git from push, refspecs glued to a redirection still count, and the Stop hook comments are corrected.
  - port notes: Almost entirely validate-push.sh. a4ba6b0b3 replaces that body, and its tools/tests/test_pushjudge.py keeps these 7c rows as pins, so a final-state port subsumes this commit. Replaying the series in order keeps test_hooks.sh conflicts small.
  - prereqs: c80c4108c
  - conflicts: tools/test_hooks.sh
- `18961c1ec` 2026-09-25 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - record failed runs, plan 011 settings
  - why: A failing command raises PostToolUseFailure, and 1.4.5 registers mark-verification-run.sh on PostToolUse Bash only, so a red build never marks and the verification reminder misfires; this registers the hook on PostToolUseFailure for Bash and PowerShell and adds the test rows.
  - port notes: settings.json needs a hand-merge because the 1.4.x registrations differ. Recount the hooks catalog on 1.4.x instead of copying 28, 29 and 34. The Codex prompt and review-record hunks are optional.
  - prereqs: 43e6780e4
  - conflicts: tools/test_hooks.sh
- `764ab0984` 2026-09-25 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - final convergence fixes for plan 011
  - why: Final push-guard fixes: the judge anchors on the first push token with git before it (an apostrophe in a comment let a later trunk force push pass), and the branch is resolved once per run (100 push lines took 7.9 s against the 5 s timeout, and a killed gate fails open).
  - port notes: Superseded in the same way as 65b405e42: a4ba6b0b3 rewrites the judge. The remaining parsing gaps continue in plan 027 (3a0614061 onward).
  - prereqs: 65b405e42
  - conflicts: tools/test_hooks.sh
skipped in this group: `79792aedc` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 011 merged

### group `plan-027-powershell-gate-coverage`  P2  track=harness  commits=13  sim: clean=1 conflict=12
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh, tools/tests/test_pushjudge.py, tools/tests/test_shellwords.py
- `b23dc8999` 2026-09-26 PORT P2 pick=clean :: feat(hooks): v2.0.30 - read PowerShell commands as Bash text
  - why: Adds .claude/hooks/_shellwords.py, which hands a PowerShell command back as the equivalent Bash text so the git gates can read both shells; nothing calls it yet.
  - port notes: Clean pick (two new files, the reader and tools/tests/test_shellwords.py). It goes live with e046bdf09.
- `e046bdf09` 2026-09-26 PORT P2 pick=conflict :: feat(hooks): v2.0.30 - run the eight git gates for the PowerShell tool
  - why: Registers the git gates for Bash|PowerShell and makes each read its command through _shellwords.py, so a commit, reset --hard, add -A or --no-verify run through the PowerShell tool is checked.
  - port notes: On the 1.4.x tip no PreToolUse group matches PowerShell at all (.claude/settings.json groups: Bash, Edit|Write, all tools, mcp__.*), a wider gap than 1.5.x had before this commit, where plan 011 already put validate-push on PowerShell. Port after plan 013 (90141357d and follow-ups: the prefilters these hunks edit) and plan 011 (43e6780e4 and follow-ups). The settings.json change is the maintainer's (config-protection). The 1.4.x Bash group also holds check-changelog-changed.sh: retire it with plan 020 (c91bb95f8) or give it taom_hook_command before widening the matcher. Keep the block-broad-git-add sed-to-loop change (a 100-line PowerShell payload took over 5 s with sed, and a killed ask gate allows). tools/test_hooks.sh conflicts heavily: the 1.5.x copy carries about 900 more lines by this point.
  - prereqs: b23dc8999, 90141357d, 43e6780e4
  - conflicts: tools/test_hooks.sh
- `3a0614061` 2026-09-26 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.30 - close validate-push glob, option and comment gaps
  - why: Closes validate-push bypasses in both shells: glob refspecs, heads/ destinations, option values read as the remote, git spelled by path or in capitals, and a trunk named only in a trailing comment.
  - port notes: Take the 47 new 7c test rows, the move to _shellwords.py segments and the refusal semantics, but do not replay this bash judge: a4ba6b0b3 (next batch) deletes it and ports the same semantics to .claude/hooks/_pushjudge.py, pinned by tools/tests/test_pushjudge.py against these 7c verdicts. Port validate-push.sh from the series end state. Requires plan 011's validate-push rewrite (43e6780e4, 764ab0984) and b23dc8999.
  - prereqs: b23dc8999, 43e6780e4, 764ab0984
  - conflicts: tools/test_hooks.sh
- `cfd3b48f6` 2026-09-26 PORT P3 pick=conflict :: fix(hooks): v2.0.30 - leave an interrupted build or test unmarked
  - why: mark-verification-run.sh no longer counts an interrupted build or test as verification (PostToolUseFailure is_interrupt) and splits commands with _shellwords.py.
  - port notes: The is_interrupt exit only matters once mark-verification-run.sh is registered on PostToolUseFailure, which plan 011 added in 18961c1ec; the 1.4.x tip registers it on PostToolUse Bash only. Whether a timeout sets is_interrupt is UNVERIFIED upstream.
  - prereqs: b23dc8999, 18961c1ec
  - conflicts: tools/test_hooks.sh
- `2d7770b64` 2026-09-26 PORT P3 pick=conflict :: docs(hooks): v2.0.30 - catalogue the PowerShell gate coverage
  - why: Documents the PowerShell gate coverage in hooks-catalog.md, hook-authoring.md, harness-facts.md and mcp-servers.md.
  - port notes: Re-derive the counts for the 1.4.x hook set (it still has suggest-compact, notify-test-results and the two CHANGELOG hooks); the PowerShell payload shape and is_interrupt facts carry over.
  - prereqs: e046bdf09
- `64b9f3a6c` 2026-09-26 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - review fixes for plan 027
  - why: Review fixes for plan 027: validate-push refused less than before in eight shapes, PowerShell statement forms hid git from three gates, piped commit producers got invented text, and timings sat near the 5 s registration.
  - port notes: Take the _shellwords.py reader additions (assignments, the . operator, ${name}, typographic quotes, value statements), the edits to block-broad-git-add, block-dangerous-git, block-no-verify, check-commit-subject-version and _pybin.sh, and the tests; take validate-push.sh from the series end state (a4ba6b0b3 replaces its bash judge). Review report, Codex prompt, RCA, REVIEW-LOG and lessons rows are optional records.
  - prereqs: e046bdf09, 3a0614061
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py
- `747b6dae8` 2026-09-26 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - convergence fixes for plan 027
  - why: Convergence fixes: a quoted value after an open quote can no longer hide a force push, the quadratic shlex pass is capped at 4 KB segments, and PowerShell assignment forms no longer hide git.
  - port notes: Take the _shellwords.py changes and tests; the validate-push.sh hunk follows the series end state (a4ba6b0b3).
  - prereqs: 64b9f3a6c
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py
- `6fe4c8fe4` 2026-09-26 PORT-PARTIAL P3 pick=conflict :: docs(plans): v2.0.30 - plan 027 merged, issues and a harness fact
  - why: Records plan 027's merge in 1.5.x plan bookkeeping and adds one harness-facts row (a PreToolUse ask prompts even in bypass mode and the tool call waits) that describes Claude Code on either line.
  - port notes: Take only the .claude/rules/harness-facts.md row 'An ask prompts even in bypass mode' (EMPIRICAL 2026-09-26) and its Last verified bump. Leave plans/README.md (the 1.4.x index has no plan 027 row) and plans/_audit/2026-09-23-opus/PROGRESS.md (absent on 1.4.x).
  - issues: 680, 681, 682
- `a4ba6b0b3` 2026-09-26 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - judge force pushes in Python within a deadline
  - why: Moves the force-push judge into Python under a 3 s deadline so a long command can no longer outrun the hook and fail open (#680); validate-push exists on both lines but 1.4.x still runs the old bash judge.
  - port notes: 1.4.x validate-push.sh is the 131-line judge that reads only the first push token and full-spelling force flags, with no _shellwords.py, so this lands only on top of plans 013, 011 and 027. Prefer an end-state transplant over replaying about 25 commits: take _shellwords.py, _pushjudge.py, validate-push.sh, tools/tests/test_pushjudge.py, test_shellwords.py and the test_hooks.sh 7c/7e/7i rows as of d01bddf10, or a later 1.5.x state after reviewing 7e26d77fe, 407570ece, caa7ac356, 09f3126a7, 9cfcadd07 and 434d1d795. Protect master, main, bannerlord-1.4.5 and bannerlord-1.5.x; keep 1.4.x's CHANGELOG gates in the 7e gate lists; drop graphify rows. Needs a safe Python via _pybin.sh (present on 1.4.x).
  - prereqs: 90141357d, 43e6780e4, e046bdf09, b23dc8999, 3a0614061, 64b9f3a6c, 747b6dae8, 5f256f704, 143f0fa85
  - conflicts: tools/test_hooks.sh
  - issues: 680
- `60d85ed7d` 2026-09-26 PORT P3 pick=conflict :: refactor(hooks): v2.0.30 - drop the push line ordering
  - why: Drops the push-line sort that 5f256f704, dad9b169e and d2fa6d081 built, now that the Python judge answers in time in any order; it nets out to nothing in an end-state transplant.
  - port notes: Only needed in a commit-by-commit replay; an end-state copy of _shellwords.py at or after this commit already lacks the sort and FORCE_HINT. Message-only differences (which branch a refusal names) are expected.
  - prereqs: a4ba6b0b3, 5f256f704
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py
  - issues: 680
- `fd1d5c17d` 2026-09-26 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - refuse abbreviated force options, matching pushes
  - why: Refuses abbreviated force options (git runs --force-w, --mir and -f --al as their long forms) and forced matching pushes (#689); the 1.4.x judge matches only full spellings and has no --mirror case at all.
  - port notes: Rides with the push-judge transplant. The tests pin git's option-prefix grammar; this laptop runs git 2.54 where the 1.5.x notes cite 2.55, so run test_pushjudge.py here before trusting it. The catalog's listed gaps (push.default via --config-env or GIT_CONFIG_*, remote.<name>.push and .mirror) carry over.
  - prereqs: a4ba6b0b3
  - conflicts: tools/test_hooks.sh, tools/tests/test_pushjudge.py
  - issues: 689
- `343c6f488` 2026-09-26 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - scope the coarse push check to the command
  - why: Scopes validate-push's no-verdict fallback to the payload from tool_input on, so the session id's UUID stops making nearly every push ask.
  - port notes: Part of the push-judge transplant; its 7e rows depend on the no-reader harness that a4ba6b0b3 introduced.
  - prereqs: a4ba6b0b3
  - conflicts: tools/test_hooks.sh
  - issues: 680, 689
- `fff6d66e9` 2026-09-27 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - review fixes for the push guard work
  - why: Review fixes across the #680, #681 and #689 work: value-option prefixes (--e as --exec), non-JSON payloads, a bounded 2 KB key scan, a capped judge budget, a 10 s validate-push registration and harder tests.
  - port notes: The judge, validate-push, _pybin.sh comment and test changes travel with the push-judge transplant. The per-hook one-liners are only comment corrections to the #681 byte window; take them with 143f0fa85. The 5 s to 10 s registration change was a 1.5.x maintainer decision (1.4.x registers validate-push at 5 s): confirm with the maintainer. hook-authoring.md was trimmed under a 12,288 B cap; check the 1.4.x file against its own lint budget.
  - prereqs: a4ba6b0b3, fd1d5c17d, 343c6f488, 143f0fa85
  - conflicts: tools/test_hooks.sh, tools/tests/test_pushjudge.py, tools/tests/test_shellwords.py
  - issues: 680, 681, 689
skipped in this group: `05dbc0d49` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 027 and its two cold reviews; `5f256f704` SKIP-SUPERSEDED fix(hooks): v2.0.30 - judge the shortest push line first; `dad9b169e` SKIP-SUPERSEDED fix(hooks): v2.0.30 - judge the push lines that could force first; `d2fa6d081` SKIP-SUPERSEDED fix(hooks): v2.0.30 - keep the push line hint linear on dash runs

### group `harness-ecc-rereview`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .mcp.json, Main/_Module/ModuleData/characters/lords.xml, tools/audit_claude_config.py, tools/test_hooks.sh, tools/tests/test_audit_skillspector.py
- `fdf9f38cb` 2026-09-29 PORT-PARTIAL P2 pick=conflict :: feat: Add deep review findings for ECC re-review and implement new tuning tool for face sliders
  - why: The ECC re-review harness pass applies to the 1.4.x harness: a wider git-safety gate, the inert C# edit hook removed, desktop-only MCP servers dropped and the config audit hardened. The same commit also carries content tooling that stays behind.
  - port notes: Take: the block-dangerous-git.sh gate arms (long_is prefix rule, branch -d -f, switch --discard-changes/-f, worktree remove --force, reflog expire/delete, update-ref -d/--stdin/all-zero id, restore -S/-W bundles) with their tools/test_hooks.sh rows; delete notify-csharp-edit.sh and its settings.json registration; the settings.json deny-list and github plugin edits; remove the github, git and filesystem servers from .mcp.json (the 1.4.x copy points filesystem at desktop E:\ paths absent on this laptop); the tools/audit_claude_config.py --user/--external hardening with test_audit_skillspector.py and test_audit_user_scope.py; the check-polearm-shield-parity.sh mute fix; the rule and doc edits (harness-facts, hook-authoring, external-skill-ports, mcp-servers.md, hooks-catalog.md, development-machines.md); the context-save, context-restore and security-scan skill edits. Leave: the lords.xml line (content), tools/oneoff/tune_face_slider_reach.py and its test, the lotrlome skins.xml snapshot and race-face docs, tools/blender/harness.py, spider_cfg.py and the gait test, the armory-audit SKILL and deep-review lenses/4-completeness.md (both absent on 1.4.x), INDEX and lessons bookkeeping. The 1.4.x block-dangerous-git.sh still reads stdin itself (it has no taom_hook_command), so re-apply the arms by hand unless e046bdf09 and c79a58521 are ported first. The audit tool change builds on c79a58521. The .mcp.json edit can be made by hand (1.5.x also touched that file in 470ee64e9 and 7d13adc36).
  - prereqs: c79a58521, e046bdf09
  - conflicts: .mcp.json, Main/_Module/ModuleData/characters/lords.xml, tools/audit_claude_config.py, tools/test_hooks.sh, tools/tests/test_audit_skillspector.py

### group `harness-config-protection`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh
- `88e805450` 2026-10-16 PORT P2 pick=conflict :: fix(hooks): v2.0.34 - config override unlocks one session only
  - why: On 1.4.x config-protection.sh:40 names the override file from CLAUDE_SESSION_ID, which the harness never sets, so one /tmp/claude-config-override-none file unlocks protected config edits for every session; the fix keys it on the hook payload's session_id.
  - port notes: config-protection.sh applies near-cleanly (its parse block matches the pre-commit 1.5.x file); tools/test_hooks.sh conflicts. Leave the .claude/skills/improve/references/execute-and-review.md hunk (file absent on 1.4.x). Related and also missing on 1.4.x: 30abf550b added the #677 Windows path normalisation to the same hook.
  - conflicts: tools/test_hooks.sh

### group `harness-upstream-watch`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/tests/test_upstream_watch.py, tools/upstream_watch.py
- `37ff36922` 2026-10-30 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - watch Coop releases, record Coop versions
  - why: Extends the upstream-watch harness to flag new BannerlordCoop releases; that matters to 1.4.x because Coop v0.1.6 and v0.1.7 target Bannerlord v1.4.8.
  - port notes: Needs the upstream-review harness first: tools/upstream_watch.py and the upstream-watch hook and skill are absent on 1.4.x. Take the releases list support, the seen command, the tests and the skill, hook and catalog edits. Rewrite the coop-interop.md version table from the 1.4.x side: v0.1.6 and v0.1.7 are the MATCHING Coop builds for TAOM 1.4.8, and Coop's bannerlord-1.5.x branch is not. Drop the coop-dedicated-server.md link, which is a 1.5.x-only doc. The yotthani entries in upstream-watch.json depend on whether their adoption records exist on 1.4.x.
  - prereqs: 7e26d77fe, 407570ece
  - conflicts: tools/tests/test_upstream_watch.py, tools/upstream_watch.py

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 5, batch 6, batch 8, batch 9, batch 10, batch 12, batch 19, batch 31
